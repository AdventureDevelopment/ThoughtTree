// Q3 on a phone or tablet without a computer (plan section 3, manual protocol). The writer is the desktop writer
// (durability.html, mixed workload: event batches of 1, 10 or 100 records, snapshots of 1 to 4 MiB, drafts), but every
// acknowledged write (transaction 'complete') is recorded in a second database, written with durability 'strict'
// before the next write, instead of being posted to a test server. On every start the page checks the data store
// against that log. Limit: a loss that hits both databases at once is not visible (the desktop test logs outside the
// browser); a log entry lost while its data survived shows up as "found beyond the log", as on the desktop.
import { IdbStore } from './storage.js';
import { getState } from './device-store.js';
import { stats } from './stats.js';

const $ = (id) => document.getElementById(id);
const DATA = 'tt-durability-device', LOG = 'tt-durability-device-log';
const sum = (u8) => { let s = 0; for (let i = 0; i < u8.length; i += 997) s = (s * 31 + u8[i]) >>> 0; return s; };

function openLog() {
  return new Promise((resolve, reject) => {
    const r = indexedDB.open(LOG, 1);
    r.onupgradeneeded = () => { r.result.createObjectStore('acks', { autoIncrement: true }); r.result.createObjectStore('state'); };
    r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error);
  });
}
function logTx(db, stores, mode, fn) {
  return new Promise((resolve, reject) => {
    const tx = mode === 'readwrite' ? db.transaction(stores, mode, { durability: 'strict' }) : db.transaction(stores, mode);
    const req = fn(tx); tx.oncomplete = () => resolve(req ? req.result : undefined); tx.onerror = () => reject(tx.error);
  });
}
const getLogState = (db) => logTx(db, ['state'], 'readonly', tx => tx.objectStore('state').get('test'));
const putLogState = (db, v) => logTx(db, ['state'], 'readwrite', tx => tx.objectStore('state').put(v, 'test'));
const addAck = (db, a) => logTx(db, ['acks'], 'readwrite', tx => tx.objectStore('acks').add(a));
const allAcks = (db) => logTx(db, ['acks'], 'readonly', tx => tx.objectStore('acks').getAll());

function browserOf(ua) {
  const pick = (name, re) => { const m = ua.match(re); return m ? { browser: name, browserVersion: m[1] } : null; };
  return pick('samsung', /SamsungBrowser\/([\d.]+)/) || pick('firefox', /(?:Firefox|FxiOS)\/([\d.]+)/) || pick('edge', /EdgA?\/([\d.]+)/)
    || pick('chrome', /(?:CriOS|Chrome)\/([\d.]+)/) || pick('safari', /Version\/([\d.]+).*Safari/) || { browser: 'unknown', browserVersion: null };
}

let log, test;

async function verify() {
  const t0 = performance.now();
  let store;
  try { store = await IdbStore.open(DATA, test.durability); } catch (e) { return { openOk: false, error: String(e) }; }
  const openMs = performance.now() - t0;
  const counts = new Map(); let total = 0;
  for (let after = 0; ;) {
    const recs = await store.readAfter(after, 20000);
    if (!recs.length) break;
    for (const r of recs) { const c = counts.get(r.batchId); if (c) c.n++; else counts.set(r.batchId, { n: 1, size: r.batchSize }); }
    total += recs.length; after = recs[recs.length - 1].seq;
  }
  let snapshot = 0, draft = 0, snapshotCorrupt = false;
  try { const s = await store.getSnapshot(); if (s) { snapshot = s.version; snapshotCorrupt = sum(s.bytes) !== s.sum || s.bytes.length !== s.size; } } catch { snapshotCorrupt = true; }
  try { const d = await store.getDraft('draft'); if (d) draft = d.version; } catch { }
  store.close?.();
  const recoveryMs = performance.now() - t0;
  const acks = await allAcks(log);
  const ackedBatches = new Map(acks.filter(a => a.kind === 'events').map(a => [a.batchId, a.size]));
  let missingEvents = 0, missingBatches = 0;
  for (const [id, size] of ackedBatches) if (!counts.has(id)) { missingBatches++; missingEvents += size; }
  const partial = [...counts].filter(([, c]) => c.n !== c.size).map(([id]) => id);
  const maxAck = (kind) => acks.filter(a => a.kind === kind).reduce((m, a) => Math.max(m, a.version), 0);
  return { openOk: true, openMs, recoveryMs, foundEvents: total, ackedEvents: [...ackedBatches.values()].reduce((a, b) => a + b, 0), missingEvents, missingBatches,
    beyondLogBatches: [...counts.keys()].filter(id => !ackedBatches.has(id)).length, partialBatches: partial.length,
    snapshotLost: snapshot < maxAck('snapshot'), snapshotCorrupt, draftLost: draft < maxAck('draft') };
}

async function writeLoop() {
  const store = await IdbStore.open(DATA, test.durability);
  const run = test.round;
  let state = BigInt(run) * 1000003n + 7n;
  const rnd = () => { state = (state + 0x9E3779B97F4A7C15n) & 0xffffffffffffffffn; let z = state; z = ((z ^ (z >> 30n)) * 0xBF58476D1CE4E5B9n) & 0xffffffffffffffffn; z = ((z ^ (z >> 27n)) * 0x94D049BB133111EBn) & 0xffffffffffffffffn; return Number((z ^ (z >> 31n)) >> 11n) / 2 ** 53; };
  let batchNo = 0, acks = 0, snapVersion = run * 1000, draftVersion = run * 1000000;
  test.writing = true; await putLogState(log, test);
  $('status').textContent = 'Writing. Swipe the browser away from the recent apps now.';
  for (;;) {
    const r = rnd();
    const kind = r < 0.70 ? 'events' : r < 0.85 ? 'snapshot' : 'draft';
    if (kind === 'events') {
      const size = [1, 10, 100][Math.floor(rnd() * 3)], no = batchNo++, batchId = `r${run}-b${no}`, records = [];
      for (let i = 0; i < size; i++) { const p = new Uint8Array(64); crypto.getRandomValues(p); records.push({ v: 1, eventId: `${batchId}-e${i}`, keyId: 'k', nonce: p.slice(0, 12), payload: p, batchId, batchSize: size, run, batchNo: no }); }
      await store.appendBatch(records);
      await addAck(log, { kind, batchId, size, run });
    } else if (kind === 'snapshot') {
      const size = (1 + Math.floor(rnd() * 4)) * 1024 * 1024, bytes = new Uint8Array(size);
      for (let i = 0; i < size; i += 65536) crypto.getRandomValues(bytes.subarray(i, Math.min(size, i + 65536)));
      const version = ++snapVersion;
      await store.putSnapshot({ version, size, sum: sum(bytes), bytes });
      await addAck(log, { kind, version, run });
    } else {
      const version = ++draftVersion;
      await store.putDraft('draft', { version, text: 'Entwurf Grüße 🌳 '.repeat(1 + Math.floor(rnd() * 50)) });
      await addAck(log, { kind, version, run });
    }
    $('counter').textContent = `${test.kills.length} of ${test.target} · ${++acks} writes`;
  }
}

async function resultFile() {
  const meta = (await getState('device')) || {};
  const b = browserOf(navigator.userAgent);
  const done = test.kills.filter(k => k.openOk !== undefined);
  const kind = meta.kind || 'phone';
  const variant = `swipe-${test.durability}`;
  const obj = {
    question: 'q3', device: `${meta.model || 'unknown model'} (${kind}, on-device runner, no adb)`, browser: b.browser, browserVersion: b.browserVersion,
    build: 'n/a (storage is JavaScript)', os: navigator.userAgent, machine: { userAgent: navigator.userAgent, deviceMemory: navigator.deviceMemory ?? null, privateModeLikely: !('serviceWorker' in navigator), kind },
    gitCommit: 'see build-info.json of the deployed spike', seed: 'round number', variant, durability: test.durability, workload: 'mixed', mode: 'manual', kills: done.length,
    method: 'swipe-away from the recent apps by hand while the page writes; on the next start the page checks the store against a log of acknowledged writes kept in a second IndexedDB database (durability strict); a loss that hits both databases at once is not visible',
    summary: {
      findings: done.filter(k => k.lost).length, openFailures: done.filter(k => !k.openOk).length, missingEvents: done.reduce((a, k) => a + (k.missingEvents || 0), 0),
      partialBatches: done.reduce((a, k) => a + (k.partialBatches || 0), 0), snapshotLost: done.filter(k => k.snapshotLost).length, snapshotCorrupt: done.filter(k => k.snapshotCorrupt).length,
      draftLost: done.filter(k => k.draftLost).length, beyondLogBatches: done.reduce((a, k) => a + (k.beyondLogBatches || 0), 0),
      recoveryMs: stats(done.map(k => k.recoveryMs)), finalStoredEvents: done.at(-1)?.foundEvents ?? null,
    },
    samples: done,
  };
  return new File([JSON.stringify(obj, null, 1) + '\n'], `q3-${kind}-${b.browser}-${variant}.json`, { type: 'application/json' });
}

function show() {
  $('counter').textContent = `${test.kills.length} of ${test.target} swipes`;
  const last = test.kills.at(-1);
  $('last').textContent = last ? `Last check: ${last.lost ? 'FINDING: ' : 'no loss; '}${last.foundEvents ?? 0} events found, ${last.missingEvents ?? 0} acknowledged events missing, ${last.partialBatches ?? 0} half batches, database opened: ${last.openOk}, recovery ${Math.round(last.recoveryMs ?? 0)} ms` : '';
  $('last').className = last?.lost ? 'bad' : 'ok';
  $('durability').value = test.durability; $('target').value = test.target;
}

async function init() {
  log = await openLog();
  test = (await getLogState(log)) || { round: 0, kills: [], durability: /Chrome/.test(navigator.userAgent) ? 'relaxed' : 'default', target: 20, writing: false };
  if (test.writing) {
    // The previous round was interrupted (swiped away): check the store against the log.
    const k = await verify();
    k.round = test.round;
    k.lost = !k.openOk || k.missingEvents > 0 || k.partialBatches > 0 || k.snapshotLost || k.snapshotCorrupt || k.draftLost;
    test.kills.push(k); test.writing = false;
    await putLogState(log, test);
  }
  show();
  if (!('serviceWorker' in navigator)) {
    // Firefox private window: the session's storage is deleted when it ends, so a swipe would look like total loss.
    $('status').textContent = 'Private mode likely: this test needs a normal (not private) tab, because private storage is deleted when the browser is swiped away.';
    $('status').className = 'bad'; $('start').disabled = true;
    return;
  }
  $('status').textContent = test.kills.length >= test.target ? 'Target reached. Save or share the result.' : 'Ready for the next round.';
  $('durability').addEventListener('change', async () => { if (test.kills.length) { $('status').textContent = 'Start a new test to change the durability.'; show(); return; } test.durability = $('durability').value; await putLogState(log, test); });
  $('target').addEventListener('change', async () => { test.target = Math.max(1, Number($('target').value) || 20); await putLogState(log, test); show(); });
  $('start').addEventListener('click', async () => { $('start').disabled = true; test.round++; await putLogState(log, test); writeLoop().catch(e => { $('status').textContent = 'Write error: ' + e.message; }); });
  $('save').addEventListener('click', async () => { const f = await resultFile(); const a = document.createElement('a'); a.href = URL.createObjectURL(f); a.download = f.name; document.body.append(a); a.click(); a.remove(); });
  $('share').addEventListener('click', async () => { const f = await resultFile(); try { if (navigator.canShare?.({ files: [f] })) await navigator.share({ files: [f] }); else $('status').textContent = 'Sharing is not supported here; use Save result.'; } catch { } });
  $('reset').addEventListener('click', async () => {
    if (!confirm('Delete this test\'s data and counter? Save the result first.')) return;
    log.close(); await Promise.all([DATA, LOG].map(n => new Promise(r => { const q = indexedDB.deleteDatabase(n); q.onsuccess = q.onerror = q.onblocked = () => r(); })));
    location.reload();
  });
}

init().catch(e => { $('status').textContent = 'Error: ' + e.message; });
