// On-device runner of spike 1 (no computer, no adb): prepares the datasets on the device, runs the timing series as a
// queue of page loads (each scenario page hands its result back and returns here), and assembles result files in the
// format of the desktop series (harness/run-series.mjs), named <q>-<phone|tablet>-<browser>-<build>.json.
import { allRuns, clearRuns, getRun, getState, setState } from './device-store.js';
import { summarise, seededShuffle } from './stats.js';
import { zip } from './zip.js';

const $ = (id) => document.getElementById(id);
const tuning = { chunk: '10000', batch: '1000', parallel: '1' };
const siteRoot = new URL('../../', location.href);            // device.html lives at /interpreted/spike/
const here = new URL('device.html', location.href);
let manifest = null, buildInfo = {};

function browserOf(ua) {
  const pick = (name, re) => { const m = ua.match(re); return m ? { browser: name, browserVersion: m[1] } : null; };
  return pick('samsung', /SamsungBrowser\/([\d.]+)/) || pick('firefox', /(?:Firefox|FxiOS)\/([\d.]+)/) || pick('edge', /EdgA?\/([\d.]+)/)
    || pick('chrome', /(?:CriOS|Chrome)\/([\d.]+)/) || pick('safari', /Version\/([\d.]+).*Safari/) || { browser: 'unknown', browserVersion: null };
}
function osOf(ua) { const m = ua.match(/Android [\d.]+|iPhone OS [\d_]+|CPU OS [\d_]+|Mac OS X [\d_]+|Windows NT [\d.]+/); return m ? m[0].replace(/_/g, '.') : 'unknown'; }
const env = () => ({ ...browserOf(navigator.userAgent), os: osOf(navigator.userAgent), userAgent: navigator.userAgent,
  deviceMemory: navigator.deviceMemory ?? null, hardwareConcurrency: navigator.hardwareConcurrency ?? null,
  connection: navigator.connection ? navigator.connection.effectiveType : null, privateModeLikely: !('serviceWorker' in navigator) });

function datasetQuery(name) {
  const d = [...manifest.datasets, ...(manifest.longNoteDatasets ?? [])].find(x => x.name === name);
  return new URLSearchParams({ dataset: name, seedLabel: manifest.seedLabel, keyId: manifest.format.keyId, accountId: manifest.format.accountId,
    checksum: d.checksumFinal, snapshotChecksum: d.checksumAtSnapshot, snapshotAt: String(d.snapshotAfterEvent), sha256: d.sha256 }).toString();
}

function runUrl(item) {
  const back = new URL(here); back.searchParams.set('continue', '1');
  const u = new URL(`${item.build}/?scenario=${item.scenario}&${datasetQuery(item.dataset)}&chunk=${tuning.chunk}&batch=${tuning.batch}&parallel=${tuning.parallel}${item.extra}`, siteRoot);
  u.searchParams.set('device', '1'); u.searchParams.set('run', item.id); u.searchParams.set('cold', item.kind === 'cold' || item.kind === 'warmup-cold' ? '1' : '0');
  u.searchParams.set('back', back.href);
  return u.href;
}

// ---- Queue ----
function seriesItems(q, build, scenario, dataset, extra, { cold = true, warm = true, runs }) {
  const items = [];
  const base = { q, build, scenario, dataset, extra };
  if (cold) { items.push({ ...base, kind: 'warmup-cold' }); for (let i = 0; i < runs; i++) items.push({ ...base, kind: 'cold' }); }
  if (warm) { items.push({ ...base, kind: 'warmup-warm' }); for (let i = 0; i < runs; i++) items.push({ ...base, kind: 'warm' }); }
  return items;
}

function planQueue() {
  const runs = Number($('runs').value);
  const builds = ['interpreted', 'aot'].filter(b => $('b-' + b).checked);
  const seed = manifest.seedLabel;
  let items = [];
  for (const build of builds) {
    if ($('p-main').checked) {
      for (const [q, sc, ds, extra, o] of seededShuffle([
        ['q2', 'empty', 'events-100000', '', {}],
        ['q1', 'snapshot', 'events-100000', '', {}],
        ['q1', 'full', 'events-100000', '', {}],
        ['q1', 'full', 'events-10000', '', { cold: false }],
        ['q1', 'full', 'events-1000', '', { cold: false }],
      ], `${seed}|device|${build}`)) items.push(...seriesItems(q, build, sc, ds, extra, { ...o, runs }));
    }
    if ($('p-search').checked) {
      for (const v of seededShuffle(['A', 'B', 'C'], `${seed}|device-search|${build}`))
        items.push(...seriesItems('q4', build, 'snapshot', 'events-100000', `&search=${v}`, { cold: false, runs }));
      items.push(...seriesItems('q4', build, 'full', 'events-100000', '&search=A', { cold: false, runs: Math.min(runs, 5) }));
    }
    if ($('p-longnotes').checked)
      for (const size of ['10k', '50k', '200k'])
        items.push(...seriesItems('q5', build, 'generate', `longnote-${size}`, '&noteio=1', { warm: false, runs: 3 }));
  }
  // Import into an empty store replaces the 100,000-event database, so it runs last and regenerates it.
  if ($('p-import').checked)
    for (const build of builds) items.push(...seriesItems('q1', build, 'generate', 'events-100000', '', { warm: false, runs: Math.min(runs, 3) }));
  return items.map((it, i) => ({ ...it, id: `s${Date.now().toString(36)}-${i}` }));
}

async function prepareQueue() {
  const done = await generatedDatasets();
  const build = $('b-aot').checked ? 'aot' : 'interpreted';
  return ['events-1000', 'events-10000', 'events-100000'].filter(d => !done[d])
    .map((d, i) => ({ q: 'setup', build, scenario: 'generate', dataset: d, extra: '', kind: 'setup', id: `g${Date.now().toString(36)}-${i}` }));
}

async function startQueue(items) {
  if (!items.length) { setStatus('Nothing to run.'); return; }
  await setState('queue', { items, next: 0, startedAt: Date.now(), env: env(), model: $('model').value.trim(), deviceKind: $('kind').value });
  location.href = runUrl(items[0]);
}

async function continueQueue() {
  const st = await getState('queue');
  if (!st || st.next >= st.items.length) { setStatus('No run pending.'); return; }
  const item = st.items[st.next];
  const r = await getRun(item.id);
  if (!r) { setStatus(`Run ${st.next + 1} of ${st.items.length} did not finish (the page was closed or ran out of memory). Tap Continue to retry it or Skip.`); return; }
  st.next++;
  await setState('queue', st);
  if (r.result.error) { setStatus(`Run ${st.next} failed: ${r.result.error.slice(0, 200)}. Stopped; tap Continue to go on.`); return; }
  if (st.next >= st.items.length) { setStatus(`Done: ${st.items.length} runs. Save the result files below.`); await render(); return; }
  setStatus(`Run ${st.next + 1} of ${st.items.length}…`);
  location.replace(runUrl(st.items[st.next]));
}

// ---- Results ----
async function generatedDatasets() {
  const out = {};
  for (const r of await allRuns()) {
    const x = r.result;
    if (x.scenario === 'generate' && x.dataset?.startsWith('events-') && x.datasetShaOk && x.checksumOk) out[x.dataset] = x;
  }
  return out;
}

async function resultFiles() {
  const st = await getState('queue');
  const meta = (await getState('device')) || {};
  const queues = (await getState('history')) || [];
  const byId = new Map();
  for (const q of [...queues, ...(st ? [st] : [])]) for (const it of q.items) byId.set(it.id, { ...it, model: q.model, deviceKind: q.deviceKind, env: q.env });
  const files = new Map();
  const setup = {};
  for (const run of (await allRuns()).sort((a, b) => a.savedAt - b.savedAt)) {
    const it = byId.get(run.id), r = run.result;
    if (!it) continue;
    if (it.kind === 'setup') {
      setup[r.dataset] = { treeVisibleMs: r.treeVisibleMs, datasetShaOk: r.datasetShaOk, checksumOk: r.checksumOk, snapshotPlainBytes: r.snapshotPlainBytes, indexTerms: r.indexTerms,
        indexPostings: r.indexPostings, indexPlainBytes: r.indexPlainBytes, indexStoredBytesB: r.indexStoredBytesB, indexStoredBytesC: r.indexStoredBytesC, storage: r.storage, phases: r.phases, jsPhases: r.jsPhases };
      continue;
    }
    if (it.kind.startsWith('warmup')) continue;
    const e = it.env || env();
    const dk = it.deviceKind || meta.kind || 'phone';
    const name = `${it.q}-${dk}-${e.browser}-${it.build}.json`;
    if (!files.has(name)) files.set(name, {
      question: it.q, device: `${it.model || meta.model || 'unknown model'} (${dk}, on-device runner, no adb)`, browser: e.browser, browserVersion: e.browserVersion,
      build: it.build, os: e.os, machine: { userAgent: e.userAgent, deviceMemory: e.deviceMemory, hardwareConcurrency: e.hardwareConcurrency, connection: e.connection, privateModeLikely: e.privateModeLikely ?? null, kind: dk },
      gitCommit: buildInfo.gitCommit ?? 'unknown', seed: manifest.seedLabel,
      runProtocol: '1 warm-up, then cold runs (page load with every boot resource fetched with cache no-store: no HTTP cache and no compiled-code cache; browser process and OS file cache stay warm), 1 warm-up, then warm runs (normal reload); one scenario per page load; no CPU throttling; on-device runner without adb',
      tuning, setup: null, series: [] });
    const f = files.get(name);
    let s = f.series.find(x => x.scenario === it.scenario && x.dataset === it.dataset && x.extra === it.extra);
    if (!s) { s = { scenario: it.scenario, dataset: it.dataset, extra: it.extra, throttle: 1, url: `${it.build}/?scenario=${it.scenario}&dataset=${it.dataset}${it.extra}`, cold: [], warm: [] }; f.series.push(s); }
    s[it.kind].push(r);
  }
  for (const f of files.values()) {
    f.setup = Object.keys(setup).length ? setup : null;
    for (const s of f.series) s.summary = { cold: s.cold.length ? summarise(s.cold) : null, warm: s.warm.length ? summarise(s.warm) : null };
  }
  return files;
}

function fileBlob(name, obj) { return new File([JSON.stringify(obj, null, 1) + '\n'], name, { type: 'application/json' }); }
function download(file) { const a = document.createElement('a'); a.href = URL.createObjectURL(file); a.download = file.name; document.body.append(a); a.click(); a.remove(); }

async function render() {
  const e = env();
  $('env').textContent = `${e.browser} ${e.browserVersion ?? ''}, ${e.os}, memory class ${e.deviceMemory ?? 'not reported'} GB, ${e.hardwareConcurrency ?? '?'} cores`;
  if (e.privateModeLikely) { $('env').textContent += '. Private mode likely: storage is temporary here; use a normal tab for the series and the swipe test.'; $('env').className = 'bad'; }
  const done = await generatedDatasets();
  $('data').replaceChildren(...['events-1000', 'events-10000', 'events-100000'].map(d => {
    const p = document.createElement('p');
    p.textContent = `${d}: ${done[d] ? `ready (SHA-256 matches the desktop file; generated in ${Math.round(done[d].treeVisibleMs / 1000)} s)` : 'not generated yet'}`;
    p.className = done[d] ? 'ok' : 'hint';
    return p;
  }));
  const files = await resultFiles();
  const table = document.createElement('table');
  table.innerHTML = '<tr><th>File</th><th>Series</th><th></th></tr>';
  for (const [name, f] of files) {
    const tr = document.createElement('tr');
    const td = (t) => { const c = document.createElement('td'); c.textContent = t; tr.append(c); return c; };
    td(name);
    td(f.series.map(s => `${s.scenario} ${s.dataset}${s.extra}: ${['cold', 'warm'].filter(k => s.summary[k]).map(k => `${k} ${Math.round(s.summary[k].treeVisibleMs?.median ?? s.summary[k].appReadyMs?.median ?? 0)} ms (n=${s[k].length})`).join(', ')}`).join('; '));
    const c = td('');
    for (const [label, fn] of [['Save', () => download(fileBlob(name, f))], ['Share', () => share([fileBlob(name, f)])], ['Copy', () => copy(f)]]) {
      const b = document.createElement('button'); b.textContent = label; b.addEventListener('click', fn); c.append(b);
    }
    table.append(tr);
  }
  $('results').replaceChildren(files.size ? table : document.createTextNode('No results yet.'));
  return files;
}

async function share(fileList) {
  try {
    if (navigator.canShare && navigator.canShare({ files: fileList })) await navigator.share({ files: fileList, title: 'Spike 1 results' });
    else { fileList.forEach(download); setStatus('Sharing files is not supported here; the files were downloaded instead.'); }
  } catch (err) { if (err.name !== 'AbortError') setStatus('Share failed: ' + err.message); }
}
async function copy(obj) {
  try { await navigator.clipboard.writeText(JSON.stringify(obj)); setStatus('Copied to the clipboard.'); } catch (err) { setStatus('Copy failed: ' + err.message); }
}
function setStatus(t) { $('status').textContent = t; }

async function init() {
  manifest = await (await fetch('manifest.json', { cache: 'no-cache' })).json();
  try { buildInfo = await (await fetch('build-info.json', { cache: 'no-cache' })).json(); } catch { }
  const meta = (await getState('device')) || {};
  $('model').value = meta.model || ''; $('kind').value = meta.kind || 'phone';
  const saveMeta = () => setState('device', { model: $('model').value.trim(), kind: $('kind').value });
  $('model').addEventListener('change', saveMeta); $('kind').addEventListener('change', saveMeta);
  $('persist').addEventListener('click', async () => setStatus(navigator.storage?.persist ? `Persistent storage: ${await navigator.storage.persist()}` : 'Persistent storage is not available here.'));
  const needModel = () => { if (!$('model').value.trim()) { setStatus('Enter the device model first.'); return true; } return false; };
  const archive = async () => { const st = await getState('queue'); if (st) { const h = (await getState('history')) || []; h.push(st); await setState('history', h); await setState('queue', null); } };
  $('prepare').addEventListener('click', async () => { if (needModel()) return; await saveMeta(); await archive(); await startQueue(await prepareQueue()); });
  $('start').addEventListener('click', async () => {
    if (needModel()) return;
    const ready = await generatedDatasets();
    if (!ready['events-100000'] || !ready['events-10000'] || !ready['events-1000']) { setStatus('Generate the datasets first (step 2).'); return; }
    await saveMeta(); await archive(); await startQueue(planQueue());
  });
  $('resume').addEventListener('click', async () => { const st = await getState('queue'); if (st && st.next < st.items.length) location.href = runUrl(st.items[st.next]); else setStatus('No run pending.'); });
  $('skip').addEventListener('click', async () => { const st = await getState('queue'); if (st && st.next < st.items.length) { st.next++; await setState('queue', st); setStatus(`Skipped; ${st.items.length - st.next} runs left. Tap Continue.`); } });
  $('stop').addEventListener('click', async () => { await archive(); setStatus('Stopped. Results so far are kept.'); });
  // One zip file with every result file: browsers block a burst of downloads, and not every browser shares several files.
  const allZip = async () => {
    const files = await resultFiles();
    const meta = (await getState('device')) || {};
    const label = `${meta.model || 'device'}-${meta.kind || 'phone'}-${env().browser}`.toLowerCase().replace(/[^a-z0-9]+/g, '-');
    return new File([zip([...files].map(([n, f]) => ({ name: n, text: JSON.stringify(f, null, 1) + '\n' })))], `spike1-results-${label}.zip`, { type: 'application/zip' });
  };
  $('save-all').addEventListener('click', async () => download(await allZip()));
  $('share-all').addEventListener('click', async () => share([await allZip()]));
  $('clear').addEventListener('click', async () => { if (confirm('Delete all results on this device? Save them first.')) { await clearRuns(); await setState('history', []); await setState('queue', null); await render(); } });
  await render();
  const st = await getState('queue');
  if (new URLSearchParams(location.search).get('continue') === '1') await continueQueue();
  else setStatus(st && st.next < st.items.length ? `${st.items.length - st.next} runs pending; tap Continue.` : 'Ready.');
}

init().catch(err => setStatus('Error: ' + err.message));
