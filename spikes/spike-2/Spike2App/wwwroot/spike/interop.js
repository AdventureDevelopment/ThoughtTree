// Browser adapter of spike 2: IndexedDB, Web Crypto and timing, reached from .NET through [JSImport].
// Three databases, one per layout, each holding exactly what that layout would store:
//   L1 today's format: per-event envelopes with the text inside, plus local event blocks (ADR-086).
//   L2 content blobs (ADR-091): per-event envelopes without text, per-blob envelopes, blocks carrying event and text.
//   L3 content blobs, separate blocks: the same envelopes and blobs, event blocks without text plus content blocks.
// The raw test key is test data; production keys are non-extractable handles unwrapped by a device key (ADR-073).
import { saveRun } from './device-store.js';

const enc = new TextEncoder();
const BUCKETS = [512, 1024, 2048, 4096, 8192, 16384];   // ADR-087
const BLOCK_PAD = [16384];                               // ADR-086: a block's plaintext to a multiple of 16 KiB
const LABEL = { event: 'thoughttree/event/v1', blob: 'thoughttree/content/v1', block: 'thoughttree/event-block/v1', cblock: 'thoughttree/content-block/v1' };
const S = { key: null, keyId: '', accountId: '', treeId: '', db: {}, staged: {}, bufs: [], stats: {}, saved: null };

function stat(name, ms) { S.stats[name] = Math.round(((S.stats[name] || 0) + ms) * 1000) / 1000; }
function aad(...parts) { return enc.encode(parts.join('\u001f')); }
function paddedSize(len, buckets) { const need = len + 1; for (const b of buckets) if (need <= b) return b; const l = buckets[buckets.length - 1]; return Math.ceil(need / l) * l; }
function pad(plain, buckets) { const p = new Uint8Array(paddedSize(plain.length, buckets)); p.set(plain); p[plain.length] = 0x80; return p; }
function unpaddedLength(u8) { let i = u8.length - 1; while (i >= 0 && u8[i] === 0) i--; if (i < 0 || u8[i] !== 0x80) throw new Error('bad padding'); return i; }
function req(r) { return new Promise((res, rej) => { r.onsuccess = () => res(r.result); r.onerror = () => rej(r.error); }); }
function done(tx) { return new Promise((res, rej) => { tx.oncomplete = () => res(); tx.onerror = () => rej(tx.error); tx.onabort = () => rej(tx.error || new Error('aborted')); }); }
function split(staged, lens) { const out = []; let o = 0; for (const n of lens) { out.push(staged.subarray(o, o + n)); o += n; } return out; }
const nums = (csv) => csv ? csv.split(',').map(Number) : [];
const strs = (s) => s ? s.split('\n') : [];

export function now() { return performance.now(); }
export function mark(name) { performance.mark(name); }
export function stats() { return JSON.stringify(S.stats); }
export function query(name) { return new URLSearchParams(location.search).get(name) || ''; }

export async function initKey(seedLabel, keyId, accountId, treeId) {
  const raw = await crypto.subtle.digest('SHA-256', enc.encode('thoughttree-spike-1 test key|' + seedLabel));
  S.key = await crypto.subtle.importKey('raw', raw, { name: 'AES-GCM' }, false, ['encrypt', 'decrypt']);
  S.keyId = keyId; S.accountId = accountId; S.treeId = treeId;
}

// ---- Databases ----
const dbName = (layout) => 'tt-spike2-' + layout;
function openDb(layout) {
  return new Promise((resolve, reject) => {
    const r = indexedDB.open(dbName(layout), 1);
    r.onupgradeneeded = () => {
      const db = r.result;
      db.createObjectStore('events', { keyPath: 'seq', autoIncrement: true });
      db.createObjectStore('blocks');
      db.createObjectStore('meta');
      if (layout !== 'L1') db.createObjectStore('blobs');
      if (layout === 'L3') db.createObjectStore('cblocks');
    };
    r.onsuccess = () => resolve(r.result);
    r.onerror = () => reject(r.error);
    r.onblocked = () => reject(new Error('open blocked'));
  });
}
function deleteDb(layout) {
  return new Promise((resolve, reject) => { const r = indexedDB.deleteDatabase(dbName(layout)); r.onsuccess = () => resolve(); r.onerror = () => reject(r.error); r.onblocked = () => resolve(); });
}
export async function open(layoutsCsv) {
  const t = performance.now();
  for (const l of layoutsCsv.split(',')) if (!S.db[l]) S.db[l] = await openDb(l);
  stat('openMs', performance.now() - t);
}
export async function resetAll() {
  for (const l of Object.keys(S.db)) { S.db[l].close(); delete S.db[l]; }
  for (const l of ['L1', 'L2', 'L3']) await deleteDb(l);
}
export async function getMeta(layout, key) { const v = await req(S.db[layout].transaction('meta').objectStore('meta').get(key)); return v == null ? '' : JSON.stringify(v); }
export async function putMeta(layout, key, json) { const tx = S.db[layout].transaction('meta', 'readwrite'); tx.objectStore('meta').put(JSON.parse(json), key); await done(tx); }

// ---- Crypto ----
const nonce = () => crypto.getRandomValues(new Uint8Array(12));
async function encryptEnvelope(kind, id, padded) {
  const iv = nonce();
  const ct = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: aad(LABEL[kind], '1', S.keyId, id, S.accountId) }, S.key, padded);
  return { iv, ct: new Uint8Array(ct) };
}
async function decryptEnvelope(kind, id, rec) {
  return new Uint8Array(await crypto.subtle.decrypt({ name: 'AES-GCM', iv: rec.nonce, additionalData: aad(LABEL[kind], '1', S.keyId, id, S.accountId) }, S.key, rec.payload));
}
const blockAad = (kind, layout, first, last) => aad(LABEL[kind], S.treeId, S.accountId, S.keyId, `${first}-${last}`, layout + '/1');
async function encryptBlock(kind, layout, first, last, plain) {
  const iv = nonce();
  const ct = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: blockAad(kind, layout, first, last) }, S.key, pad(plain, BLOCK_PAD));
  return { first, last, iv, ct: new Uint8Array(ct) };
}
async function decryptBlock(kind, layout, rec) {
  const p = new Uint8Array(await crypto.subtle.decrypt({ name: 'AES-GCM', iv: rec.iv, additionalData: blockAad(kind, layout, rec.first, rec.last) }, S.key, rec.ct));
  return p.subarray(0, unpaddedLength(p));
}
const envelopeBytes = (r, idLen) => r.payload.length + r.nonce.length + idLen + S.keyId.length + 1;   // stored or sent fields, no IndexedDB overhead

// ---- Staging: .NET hands over byte buffers by slot name ----
export function stage(slot, span) { S.staged[slot] = span.slice(); }
export function bufferLength(i) { return S.bufs[i] ? S.bufs[i].length : 0; }
export function takeBuffer(i, span) { span.set(S.bufs[i]); S.bufs[i] = null; }

// ---- Setup: one batch of 1,000 events into all three layouts ----
// Slots: full (L1 payload wrappers), struct (payload wrappers without text), content (blob payloads),
// b1 (L1 block lines), b2 (L2 block lines), e3 (L3 event block lines), c3 (L3 content block lines).
const sizes = {};
function addSize(name, plain, padded, stored, count) { const s = sizes[name] ||= { count: 0, plainBytes: 0, paddedBytes: 0, storedBytes: 0 }; s.count += count; s.plainBytes += plain; s.paddedBytes += padded; s.storedBytes += stored; }
export function setupSizes() { return JSON.stringify(sizes); }

export async function setupBatch(block, firstSeq, eventIdsJoined, blobIdsJoined, fullLens, structLens, contentLens) {
  const ids = strs(eventIdsJoined), blobIds = strs(blobIdsJoined);
  const full = split(S.staged.full, nums(fullLens)), struct = split(S.staged.struct, nums(structLens)), content = split(S.staged.content, nums(contentLens));
  const lastSeq = firstSeq + ids.length - 1;
  let t = performance.now();
  const encAll = (kind, idList, parts, name) => Promise.all(parts.map(async (p, i) => {
    const padded = pad(p, BUCKETS);
    const { iv, ct } = await encryptEnvelope(kind, idList[i], padded);
    const rec = { v: 1, keyId: S.keyId, nonce: iv, payload: ct };
    if (kind === 'event') rec.eventId = idList[i]; else rec.blobId = idList[i];
    addSize(name, p.length, padded.length, envelopeBytes(rec, idList[i].length), 1);
    return rec;
  }));
  const [fullRecs, structRecs, blobRecs] = await Promise.all([encAll('event', ids, full, 'L1.events'), encAll('event', ids, struct, 'L23.events'), encAll('blob', blobIds, content, 'L23.blobs')]);
  const [b1, b2, e3, c3] = await Promise.all([
    encryptBlock('block', 'L1', firstSeq, lastSeq, S.staged.b1), encryptBlock('block', 'L2', firstSeq, lastSeq, S.staged.b2),
    encryptBlock('block', 'L3', firstSeq, lastSeq, S.staged.e3), encryptBlock('cblock', 'L3', firstSeq, lastSeq, S.staged.c3)]);
  for (const [name, plain, b] of [['L1.blocks', S.staged.b1, b1], ['L2.blocks', S.staged.b2, b2], ['L3.eblocks', S.staged.e3, e3], ['L3.cblocks', S.staged.c3, c3]])
    addSize(name, plain.length, b.ct.length - 16, b.ct.length + 12, 1);
  stat('setupEncryptMs', performance.now() - t);
  t = performance.now();
  const write = async (layout, stores, fill) => { const tx = S.db[layout].transaction(stores, 'readwrite'); fill(tx); await done(tx); };
  await Promise.all([
    write('L1', ['events', 'blocks'], tx => { for (const r of fullRecs) tx.objectStore('events').add(r); tx.objectStore('blocks').put(b1, block); }),
    write('L2', ['events', 'blobs', 'blocks'], tx => { for (const r of structRecs) tx.objectStore('events').add(r); for (const r of blobRecs) tx.objectStore('blobs').put(r, r.blobId); tx.objectStore('blocks').put(b2, block); }),
    write('L3', ['events', 'blobs', 'blocks', 'cblocks'], tx => { for (const r of structRecs) tx.objectStore('events').add(r); for (const r of blobRecs) tx.objectStore('blobs').put(r, r.blobId); tx.objectStore('blocks').put(e3, block); tx.objectStore('cblocks').put(c3, block); }),
  ]);
  stat('setupWriteMs', performance.now() - t);
  S.staged = {};
}

// ---- Replay from blocks (cases 1 to 4) ----
// Reads `count` blocks from `from` of each named store in one transaction, decrypts them all (one Web Crypto call
// per block, in parallel), and joins each store's block plaintexts into one buffer (buffer i = store i).
export async function readBlocks(layout, storesCsv, from, count) {
  const stores = storesCsv.split(',');
  let t = performance.now();
  const tx = S.db[layout].transaction(stores);
  const range = IDBKeyRange.bound(from, from + count - 1);
  const recs = await Promise.all(stores.map(s => req(tx.objectStore(s).getAll(range))));
  stat('readMs', performance.now() - t);
  if (recs[0].length === 0) { S.bufs = stores.map(() => new Uint8Array(0)); return 0; }
  t = performance.now();
  const plains = await Promise.all(stores.map((s, i) => Promise.all(recs[i].map(r => decryptBlock(s === 'cblocks' ? 'cblock' : 'block', layout, r)))));
  stat('decryptMs', performance.now() - t);
  t = performance.now();
  S.bufs = plains.map(list => { const b = new Uint8Array(list.reduce((a, p) => a + p.length, 0)); let o = 0; for (const p of list) { b.set(p, o); o += p.length; } return b; });
  stat('joinMs', performance.now() - t);
  S.lastSeq = recs[0][recs[0].length - 1].last;
  return recs[0].length;
}
export function lastSeq() { return S.lastSeq || 0; }

// Case 4: fetches single blobs by id from the per-blob store, in one transaction, one Web Crypto call each.
// Buffer 0 holds one line per id, in the order asked (empty line: blob missing).
export async function getBlobs(layout, idsJoined) {
  const ids = strs(idsJoined);
  let t = performance.now();
  const os = S.db[layout].transaction('blobs').objectStore('blobs');
  const recs = await Promise.all(ids.map(id => req(os.get(id))));
  stat('blobReadMs', performance.now() - t);
  t = performance.now();
  const plains = await Promise.all(recs.map((r, i) => r ? decryptEnvelope('blob', ids[i], r).then(p => p.subarray(0, unpaddedLength(p))) : new Uint8Array(0)));
  stat('blobDecryptMs', performance.now() - t);
  const b = new Uint8Array(plains.reduce((a, p) => a + p.length + 1, 0)); let o = 0;
  for (const p of plains) { b.set(p, o); o += p.length; b[o++] = 10; }
  S.bufs = [b];
  return recs.filter(Boolean).length;
}

// ---- Case 5: erase ----
// Reads the named blocks (decrypted, buffer 0, lengths returned) so .NET can drop the erased text.
export async function readBlocksAt(layout, store, indicesCsv) {
  const idx = nums(indicesCsv);
  let t = performance.now();
  const os = S.db[layout].transaction(store).objectStore(store);
  const recs = await Promise.all(idx.map(i => req(os.get(i))));
  stat('eraseReadMs', performance.now() - t);
  t = performance.now();
  const plains = await Promise.all(recs.map(r => decryptBlock(store === 'cblocks' ? 'cblock' : 'block', layout, r)));
  stat('eraseDecryptMs', performance.now() - t);
  S.blockRanges = recs.map(r => [r.first, r.last]);
  const b = new Uint8Array(plains.reduce((a, p) => a + p.length, 0)); let o = 0;
  for (const p of plains) { b.set(p, o); o += p.length; }
  S.bufs = [b];
  return plains.map(p => p.length).join(',');
}

// Keeps the original records so the measured erase can be undone afterwards (the setup data stays reusable). Not timed.
export async function eraseSave(layout, store, blobIdsJoined, indicesCsv) {
  const tx = S.db[layout].transaction(['blobs', store]);
  const blobs = await Promise.all(strs(blobIdsJoined).map(id => req(tx.objectStore('blobs').get(id)).then(r => [id, r])));
  const blocks = await Promise.all(nums(indicesCsv).map(i => req(tx.objectStore(store).get(i)).then(r => [i, r])));
  S.saved = { layout, store, blobs, blocks };
}

// The erase itself: the staged buffer holds the rebuilt block plaintexts (slot "blocks"). One transaction deletes the
// blobs, replaces the blocks and appends the erase event (item.content-erased, ADR-091).
export async function eraseCommit(layout, store, blobIdsJoined, indicesCsv, lengthsCsv, eraseEventId) {
  const idx = nums(indicesCsv), parts = split(S.staged.blocks, nums(lengthsCsv)), ids = strs(blobIdsJoined);
  let t = performance.now();
  const kind = store === 'cblocks' ? 'cblock' : 'block';
  const recs = await Promise.all(parts.map((p, i) => encryptBlock(kind, layout, S.blockRanges[i][0], S.blockRanges[i][1], p)));
  const ev = await encryptEnvelope('event', eraseEventId, pad(enc.encode(`{"v":1,"event":{"type":"item.content-erased","schema":1,"data":{"itemId":"spike"}}}`), BUCKETS));
  stat('eraseEncryptMs', performance.now() - t);
  t = performance.now();
  const tx = S.db[layout].transaction(['blobs', store, 'events'], 'readwrite');
  for (const id of ids) tx.objectStore('blobs').delete(id);
  idx.forEach((i, k) => tx.objectStore(store).put(recs[k], i));
  const added = req(tx.objectStore('events').add({ v: 1, eventId: eraseEventId, keyId: S.keyId, nonce: ev.iv, payload: ev.ct }));
  await done(tx);
  S.saved.eventSeq = await added;
  stat('eraseWriteMs', performance.now() - t);
  S.staged = {};
}

export async function eraseRestore() {
  const { layout, store, blobs, blocks, eventSeq } = S.saved;
  const tx = S.db[layout].transaction(['blobs', store, 'events'], 'readwrite');
  for (const [id, r] of blobs) if (r) tx.objectStore('blobs').put(r, id);
  for (const [i, r] of blocks) tx.objectStore(store).put(r, i);
  if (eventSeq != null) tx.objectStore('events').delete(eventSeq);
  await done(tx);
  S.saved = null;
}

// ---- Case 6: writing a sync batch of 1,000 events ----
// Slots: events (padded per envelope here), blobs (may be empty), b (block lines), c (content block lines, L3 only).
// Envelopes are encrypted and stored in one transaction (the acknowledgement); the block follows in its own
// transaction, as ADR-086 says writing a block never delays saving an event.
export async function syncWrite(layout, block, firstSeq, eventIdsJoined, blobIdsJoined, eventLens, blobLens) {
  const ids = strs(eventIdsJoined), blobIds = strs(blobIdsJoined);
  const events = split(S.staged.events, nums(eventLens)), blobs = split(S.staged.blobs || new Uint8Array(0), nums(blobLens));
  const r = { events: ids.length, blobs: blobIds.length, wireBytes: 0, paddedBytes: 0 };
  let t = performance.now();
  const encAll = (kind, idList, parts) => Promise.all(parts.map(async (p, i) => {
    const padded = pad(p, BUCKETS); r.paddedBytes += padded.length;
    const { iv, ct } = await encryptEnvelope(kind, idList[i], padded);
    const rec = { v: 1, keyId: S.keyId, nonce: iv, payload: ct };
    if (kind === 'event') rec.eventId = idList[i]; else rec.blobId = idList[i];
    r.wireBytes += envelopeBytes(rec, idList[i].length);
    return rec;
  }));
  const [eventRecs, blobRecs] = await Promise.all([encAll('event', ids, events), encAll('blob', blobIds, blobs)]);
  r.encryptMs = performance.now() - t;
  t = performance.now();
  const stores = blobIds.length ? ['events', 'blobs'] : ['events'];
  const tx = S.db[layout].transaction(stores, 'readwrite');
  const seqs = eventRecs.map(rec => req(tx.objectStore('events').add(rec)));
  for (const rec of blobRecs) tx.objectStore('blobs').put(rec, rec.blobId);
  await done(tx);
  r.storeMs = performance.now() - t;
  t = performance.now();
  const lastSeq = firstSeq + ids.length - 1;
  const blk = await encryptBlock('block', layout, firstSeq, lastSeq, S.staged.b);
  const cblk = S.staged.c ? await encryptBlock('cblock', layout, firstSeq, lastSeq, S.staged.c) : null;
  const btx = S.db[layout].transaction(cblk ? ['blocks', 'cblocks'] : ['blocks'], 'readwrite');
  btx.objectStore('blocks').put(blk, block);
  if (cblk) btx.objectStore('cblocks').put(cblk, block);
  await done(btx);
  r.blockMs = performance.now() - t;
  S.syncCleanup = { layout, seqs: await Promise.all(seqs), blobIds, block, cblk: !!cblk };
  S.staged = {};
  return JSON.stringify(r);
}

export async function syncCleanup() {
  const { layout, seqs, blobIds, block, cblk } = S.syncCleanup;
  const stores = ['events', 'blocks', ...(blobIds.length ? ['blobs'] : []), ...(cblk ? ['cblocks'] : [])];
  const tx = S.db[layout].transaction(stores, 'readwrite');
  tx.objectStore('events').delete(IDBKeyRange.bound(seqs[0], seqs[seqs.length - 1]));
  for (const id of blobIds) tx.objectStore('blobs').delete(id);
  tx.objectStore('blocks').delete(block);
  if (cblk) tx.objectStore('cblocks').delete(block);
  await done(tx);
}

// ---- Environment and results ----
export async function storageEstimate() {
  const e = navigator.storage && navigator.storage.estimate ? await navigator.storage.estimate() : {};
  return JSON.stringify({ usage: e.usage ?? null, quota: e.quota ?? null, usageDetails: e.usageDetails ?? null });
}
export function environment() {
  return JSON.stringify({ userAgent: navigator.userAgent, deviceMemory: navigator.deviceMemory ?? null, hardwareConcurrency: navigator.hardwareConcurrency ?? null, serviceWorkerAvailable: 'serviceWorker' in navigator });
}
export function memory() {
  const m = performance.memory ? { jsHeapUsed: performance.memory.usedJSHeapSize } : {};
  try { const rt = globalThis.getDotnetRuntime && globalThis.getDotnetRuntime(0); if (rt && rt.Module && rt.Module.HEAPU8) m.wasmMemory = rt.Module.HEAPU8.length; } catch { }
  return JSON.stringify(m);
}
export function report(json) { window.spikeResult = JSON.parse(json); window.spikeDone = true; }
export async function deviceHandOff(json) {
  const p = new URLSearchParams(location.search);
  await saveRun(p.get('run'), JSON.parse(json));
  const back = p.get('back');
  if (back && new URL(back, location.href).origin === location.origin) setTimeout(() => location.replace(back), 300);
}
export function download(json, name) {
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
  a.download = name; a.click();
}
