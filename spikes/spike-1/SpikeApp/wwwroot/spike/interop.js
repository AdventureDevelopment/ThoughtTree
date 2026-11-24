// Browser adapter of the spike app: storage, Web Crypto and timing, reached from .NET through [JSImport].
// The raw test key is test data; production keys are non-extractable handles unwrapped by a device key (ADR-073).
import { IdbStore } from './storage.js';
import { Sha256 } from './sha256.js';
import { saveRun } from './device-store.js';

const enc = new TextEncoder();
const S = { store: null, key: null, keyId: '', accountId: '', buf: null, stats: {}, marks: {}, lastSeq: 0, info: {} };

function stat(name, ms) { S.stats[name] = (S.stats[name] || 0) + ms; }
function b64(s) { const b = atob(s); const a = new Uint8Array(b.length); for (let i = 0; i < b.length; i++) a[i] = b.charCodeAt(i); return a; }
function toB64(u8) { let s = ''; for (let i = 0; i < u8.length; i += 0x8000) s += String.fromCharCode.apply(null, u8.subarray(i, i + 0x8000)); return btoa(s); }
function aad(...parts) { return enc.encode(parts.join('\u001f')); }
function unpaddedLength(u8) { let i = u8.length - 1; while (i >= 0 && u8[i] === 0) i--; if (i < 0 || u8[i] !== 0x80) throw new Error('bad padding'); return i; }

export function now() { return performance.now(); }
export function mark(name) { S.marks[name] = performance.now(); performance.mark(name); }
export function stats() { return JSON.stringify(S.stats); }
export function resetStats() { S.stats = {}; }
export function query(name) { return new URLSearchParams(location.search).get(name) || ''; }
export function lastSeq() { return S.lastSeq; }

/** Derives the fixed test key from the seed label (test data only) and keeps it as a non-extractable handle. */
export async function initKey(seedLabel, keyId, accountId) {
  const raw = await crypto.subtle.digest('SHA-256', enc.encode('thoughttree-spike-1 test key|' + seedLabel));
  S.key = await crypto.subtle.importKey('raw', raw, { name: 'AES-GCM' }, false, ['encrypt', 'decrypt']);
  S.keyId = keyId; S.accountId = accountId;
}

export async function fetchText(url) { const r = await fetch(url); if (!r.ok) throw new Error(url + ' ' + r.status); return await r.text(); }

export async function openDb(name, durability) {
  const t = performance.now();
  S.store = await IdbStore.open(name, durability);
  stat('openMs', performance.now() - t);
}

export async function deleteDb(name) { if (S.store) { S.store.close(); S.store = null; } await IdbStore.delete(name); }

/** Imports a JSON-lines envelope file into an empty store in batches (PF4 scaled). Returns the event count. */
export async function importFile(url, batch) {
  let t = performance.now();
  const text = await fetchText(url);
  stat('importFetchMs', performance.now() - t);
  t = performance.now();
  const lines = text.split('\n');
  let n = 0, pending = [], parseMs = 0, writeMs = 0;
  for (const line of lines) {
    if (!line) continue;
    const t0 = performance.now();
    const e = JSON.parse(line);
    pending.push({ v: e.v, eventId: e.eventId, keyId: e.keyId, nonce: b64(e.nonce), payload: b64(e.payload) });
    parseMs += performance.now() - t0;
    if (pending.length >= batch) { const t1 = performance.now(); await S.store.appendBatch(pending); writeMs += performance.now() - t1; n += pending.length; pending = []; }
  }
  if (pending.length) { const t1 = performance.now(); await S.store.appendBatch(pending); writeMs += performance.now() - t1; n += pending.length; }
  stat('importParseMs', parseMs); stat('importWriteMs', writeMs); stat('importMs', performance.now() - t);
  return n;
}

async function decryptOne(r) {
  const plain = await crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: r.nonce, additionalData: aad('thoughttree/event/v1', String(r.v), r.keyId, r.eventId, S.accountId) },
    S.key, r.payload);
  return new Uint8Array(plain);
}

/**
 * Reads up to `chunk` envelopes after `afterSeq`, decrypts them (batches of `batch` decrypt calls in parallel, or one
 * after the other), removes the padding and joins the payload wrappers with '\n' into one buffer for .NET.
 * Returns the number of envelopes (0 at the end); the buffer is fetched with takeBuffer.
 */
export async function readDecrypt(afterSeq, chunk, batch, parallel) {
  let t = performance.now();
  const recs = await S.store.readAfter(afterSeq, chunk);
  stat('readMs', performance.now() - t);
  if (recs.length === 0) { S.buf = new Uint8Array(0); return 0; }
  t = performance.now();
  const plains = new Array(recs.length);
  if (parallel) {
    for (let i = 0; i < recs.length; i += batch) {
      const part = recs.slice(i, i + batch);
      const out = await Promise.all(part.map(decryptOne));
      for (let j = 0; j < out.length; j++) plains[i + j] = out[j];
    }
  } else {
    for (let i = 0; i < recs.length; i++) plains[i] = await decryptOne(recs[i]);
  }
  stat('decryptMs', performance.now() - t);
  t = performance.now();
  let total = 0; const lens = new Array(recs.length);
  for (let i = 0; i < plains.length; i++) { lens[i] = unpaddedLength(plains[i]); total += lens[i] + 1; }
  const buf = new Uint8Array(total); let o = 0;
  for (let i = 0; i < plains.length; i++) { buf.set(plains[i].subarray(0, lens[i]), o); o += lens[i]; buf[o++] = 10; }
  S.buf = buf;
  S.lastSeq = recs[recs.length - 1].seq;
  stat('unpadJoinMs', performance.now() - t);
  return buf.length;
}

/** Length of the buffer prepared by the last read; copy it with takeBuffer. */
export function bufferLength() { return S.buf ? S.buf.length : 0; }
export function takeBuffer(span) { span.set(S.buf); S.buf = null; }

async function encryptBlob(label, name, bytes) {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const ct = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: aad(label, S.keyId, S.accountId, name) }, S.key, bytes);
  return { iv, ct: new Uint8Array(ct) };
}

async function decryptBlob(label, name, rec) {
  return new Uint8Array(await crypto.subtle.decrypt({ name: 'AES-GCM', iv: rec.iv, additionalData: aad(label, S.keyId, S.accountId, name) }, S.key, rec.ct));
}

/** Stores the snapshot (encrypted with the tree key, ADR-073) with the sequence number it covers. */
export function stageBytes(span) { S.staged = span.slice(); }
export async function putSnapshot(seq) {
  const bytes = S.staged; S.staged = null;
  let t = performance.now();
  const blob = await encryptBlob('thoughttree/snapshot/v1', 'latest', bytes);
  stat('snapshotEncryptMs', performance.now() - t);
  t = performance.now();
  await S.store.putSnapshot({ seq, ...blob });
  stat('snapshotWriteMs', performance.now() - t);
  return bytes.length;
}

/** Reads and decrypts the snapshot into the buffer; returns its covered sequence number (0: none). */
export async function getSnapshot() {
  let t = performance.now();
  const rec = await S.store.getSnapshot();
  stat('snapshotReadMs', performance.now() - t);
  if (!rec) return 0;
  t = performance.now();
  S.buf = await decryptBlob('thoughttree/snapshot/v1', 'latest', rec);
  stat('snapshotDecryptMs', performance.now() - t);
  S.info.snapshotBytes = rec.ct.length;
  return rec.seq;
}

/** Encrypts the staged padded payload wrapper as a new envelope (random nonce) and appends it; returns its sequence number. */
export async function encryptAppend(eventId) {
  const padded = S.staged; S.staged = null;
  const nonce = crypto.getRandomValues(new Uint8Array(12));
  const ct = await crypto.subtle.encrypt({ name: 'AES-GCM', iv: nonce, additionalData: aad('thoughttree/event/v1', '1', S.keyId, eventId, S.accountId) }, S.key, padded);
  await S.store.appendBatch([{ v: 1, eventId, keyId: S.keyId, nonce, payload: new Uint8Array(ct) }]);
  return await S.store.countEvents();
}

let pendingChunks = [];
export function addIndexChunk(span) { pendingChunks.push(span.slice()); }

/** Stores the pending index chunks, encrypted (variant B) or not (variant C), under a store name per variant. */
export async function flushIndexChunks(variant) {
  const t = performance.now();
  const out = [];
  let bytes = 0;
  for (let i = 0; i < pendingChunks.length; i++) {
    const c = pendingChunks[i];
    if (variant === 'B') { const b = await encryptBlob('thoughttree/search-index/v1', 'chunk-' + i, c); out.push({ variant, ...b }); bytes += b.ct.length; }
    else { out.push({ variant, plain: c }); bytes += c.length; }
  }
  pendingChunks = [];
  const tx = S.store.db.transaction(['meta'], 'readwrite');
  tx.objectStore('meta').put(out, 'index-' + variant);
  await new Promise((res, rej) => { tx.oncomplete = res; tx.onerror = () => rej(tx.error); });
  stat('indexWriteMs', performance.now() - t);
  return bytes;
}

let loadedChunks = [];
/** Loads (and for B decrypts) all index chunks of a variant; returns the chunk count. Fetch each with takeIndexChunk. */
export async function loadIndexChunks(variant) {
  let t = performance.now();
  const recs = (await S.store.get('meta', 'index-' + variant)) || [];
  stat('indexReadMs', performance.now() - t);
  t = performance.now();
  if (variant === 'B') loadedChunks = await Promise.all(recs.map((r, i) => decryptBlob('thoughttree/search-index/v1', 'chunk-' + i, r)));
  else loadedChunks = recs.map(r => r.plain);
  stat('indexDecryptMs', performance.now() - t);
  S.info['indexStoredBytes' + variant] = recs.reduce((a, r) => a + (r.ct ? r.ct.length : r.plain.length), 0);
  return loadedChunks.length;
}
export function indexChunkLength(i) { return loadedChunks[i].length; }
export function takeIndexChunk(i, span) { span.set(loadedChunks[i]); }

export async function storageEstimate() {
  const e = navigator.storage && navigator.storage.estimate ? await navigator.storage.estimate() : {};
  let battery = null;
  try { if (navigator.getBattery) { const b = await navigator.getBattery(); battery = { charging: b.charging, level: b.level }; } } catch { }
  return JSON.stringify({ usage: e.usage ?? null, quota: e.quota ?? null, usageDetails: e.usageDetails ?? null, battery });
}

export function memory() {
  const m = performance.memory ? { jsHeapUsed: performance.memory.usedJSHeapSize, jsHeapTotal: performance.memory.totalJSHeapSize } : {};
  try { const rt = globalThis.getDotnetRuntime && globalThis.getDotnetRuntime(0); if (rt && rt.Module && rt.Module.HEAPU8) m.wasmMemory = rt.Module.HEAPU8.length; } catch { }
  return JSON.stringify(m);
}

export function environment() {
  const nav = performance.getEntriesByType('navigation')[0];
  const res = performance.getEntriesByType('resource');
  const sum = (f) => res.reduce((a, r) => a + (r[f] || 0), 0) + (nav ? nav[f] || 0 : 0);
  return JSON.stringify({
    userAgent: navigator.userAgent, deviceMemory: navigator.deviceMemory ?? null, hardwareConcurrency: navigator.hardwareConcurrency ?? null,
    navigation: nav ? { responseEnd: nav.responseEnd, domContentLoaded: nav.domContentLoadedEventEnd, loadEventEnd: nav.loadEventEnd } : null,
    transfer: { transferSize: sum('transferSize'), encodedBodySize: sum('encodedBodySize'), decodedBodySize: sum('decodedBodySize'), resources: res.length + 1 },
    resources: res.filter(r => r.name.includes('/_framework/')).map(r => ({ name: r.name.replace(location.origin, ''), transferSize: r.transferSize, encodedBodySize: r.encodedBodySize, decodedBodySize: r.decodedBodySize })),
    info: S.info,
  });
}

// ---- On-device generation (scenario=generate) ----
// Same bytes as the desktop generator (DatasetGenerator/Program.cs): test nonce 0x54545331 + 64-bit counter, AAD of
// ADR-073, envelope line {"v":1,"eventId":..,"keyId":..,"nonce":..,"payload":..} and '\n'; the lines are hashed
// with SHA-256 and compared with the manifest, so the device holds the identical dataset.
let gen = null;
export function generateBegin() { gen = { sha: new Sha256() }; }

export async function encryptStoreBatch(eventIds, lengths, firstCounter) {
  const padded = S.staged; S.staged = null;
  const ids = eventIds.split('\n'), lens = lengths.split(',').map(Number);
  let t = performance.now();
  const ivs = ids.map((_, i) => { const iv = new Uint8Array(12); const v = new DataView(iv.buffer); v.setUint32(0, 0x54545331); v.setBigUint64(4, BigInt(firstCounter + i)); return iv; });
  let o = 0;
  const parts = lens.map(n => { const p = padded.subarray(o, o + n); o += n; return p; });
  const cts = await Promise.all(parts.map((p, i) => crypto.subtle.encrypt(
    { name: 'AES-GCM', iv: ivs[i], additionalData: aad('thoughttree/event/v1', '1', S.keyId, ids[i], S.accountId) }, S.key, p)));
  stat('generateEncryptMs', performance.now() - t);
  t = performance.now();
  const recs = ids.map((id, i) => ({ v: 1, eventId: id, keyId: S.keyId, nonce: ivs[i], payload: new Uint8Array(cts[i]) }));
  await S.store.appendBatch(recs);
  stat('importWriteMs', performance.now() - t);
  t = performance.now();
  for (const r of recs) gen.sha.update(enc.encode(`{"v":1,"eventId":"${r.eventId}","keyId":"${r.keyId}","nonce":"${toB64(r.nonce)}","payload":"${toB64(r.payload)}"}\n`));
  stat('generateHashMs', performance.now() - t);
}

export function generateEnd() { const h = gen.sha.hex(); gen = null; return h; }

/** Device mode: keeps the result in the device runner's store and returns to the runner page (spike/device.html). */
export async function deviceHandOff(json) {
  const p = new URLSearchParams(location.search);
  await saveRun(p.get('run'), JSON.parse(json));
  const back = p.get('back');
  if (back && new URL(back, location.href).origin === location.origin) setTimeout(() => location.replace(back), 300);
}

/** Publishes the result: window.spikeResult for Playwright, and a "Save results" download for phone runs. */
export function report(json) {
  window.spikeResult = JSON.parse(json);
  window.spikeDone = true;
  const post = new URLSearchParams(location.search).get('post');
  if (post) fetch(post, { method: 'POST', headers: { 'content-type': 'application/json' }, body: json }).catch(() => {});
}

export function download(json, name) {
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
  a.download = name; a.click();
}
