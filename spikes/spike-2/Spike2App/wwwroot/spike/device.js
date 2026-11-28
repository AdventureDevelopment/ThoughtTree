// On-device runner of spike 2 (no computer needed): prepares the three layouts on the device, runs the six cases as a
// queue of page loads (each scenario page hands its result back and returns here), and assembles one result file in
// the format of the desktop series (harness/run.mjs), named spike2-<phone|tablet>-<browser>-aot.json.
import { allRuns, clearRuns, getRun, getState, setState } from './device-store.js';
import { summarise, seededShuffle } from './stats.js';

const $ = (id) => document.getElementById(id);
const SCENARIOS = ['replay1', 'replay2', 'replay3', 'structure4', 'erase', 'sync'];
const appRoot = new URL('../', location.href);              // device.html lives at /aot/spike/
const here = new URL('device.html', location.href);
let buildInfo = {};

function browserOf(ua) {
  const pick = (name, re) => { const m = ua.match(re); return m ? { browser: name, browserVersion: m[1] } : null; };
  return pick('samsung', /SamsungBrowser\/([\d.]+)/) || pick('firefox', /(?:Firefox|FxiOS)\/([\d.]+)/) || pick('edge', /EdgA?\/([\d.]+)/)
    || pick('chrome', /(?:CriOS|Chrome)\/([\d.]+)/) || pick('safari', /Version\/([\d.]+).*Safari/) || { browser: 'unknown', browserVersion: null };
}
function osOf(ua) { const m = ua.match(/Android [\d.]+|iPhone OS [\d_]+|CPU OS [\d_]+|Mac OS X [\d_]+|Windows NT [\d.]+/); return m ? m[0].replace(/_/g, '.') : 'unknown'; }
const env = () => ({ ...browserOf(navigator.userAgent), os: osOf(navigator.userAgent), userAgent: navigator.userAgent,
  deviceMemory: navigator.deviceMemory ?? null, hardwareConcurrency: navigator.hardwareConcurrency ?? null, privateModeLikely: !('serviceWorker' in navigator) });

function runUrl(item) {
  const back = new URL(here); back.searchParams.set('continue', '1');
  const u = new URL(`?scenario=${item.scenario}`, appRoot);
  u.searchParams.set('device', '1'); u.searchParams.set('run', item.id); u.searchParams.set('back', back.href);
  return u.href;
}

function planQueue(rounds) {
  const items = SCENARIOS.map(s => ({ scenario: s, kind: 'warmup' }));
  for (let r = 0; r < rounds; r++) for (const s of seededShuffle(SCENARIOS, `spike-2|device|${r}`)) items.push({ scenario: s, kind: 'run', round: r });
  return items.map((it, i) => ({ ...it, id: `r${Date.now().toString(36)}-${i}` }));
}

async function startQueue(items) {
  await setState('queue', { items, next: 0, env: env(), model: $('model').value.trim(), deviceKind: $('kind').value });
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
  if (r.result.error) { setStatus(`Run ${st.next} (${item.scenario}) failed: ${r.result.error.slice(0, 200)}. Stopped; tap Continue to go on.`); await render(); return; }
  if (st.next >= st.items.length) { setStatus(item.kind === 'setup' ? 'Test data ready.' : `Done: ${st.items.length} runs. Save or share the result file below.`); await render(); return; }
  setStatus(`Run ${st.next + 1} of ${st.items.length}…`);
  location.replace(runUrl(st.items[st.next]));
}

async function queues() {
  const st = await getState('queue');
  return [...((await getState('history')) || []), ...(st ? [st] : [])];
}

async function setupResult() {
  let found = null;
  for (const q of await queues()) for (const it of q.items) if (it.kind === 'setup') { const r = await getRun(it.id); if (r && !r.result.error && r.result.generatorChecksumOk) found = r.result; }
  return found;
}

async function resultFile() {
  const meta = (await getState('device')) || {};
  const byId = new Map();
  let first = null;
  for (const q of await queues()) for (const it of q.items) { byId.set(it.id, it); first ||= q; }
  const e = first?.env || env();
  const kind = first?.deviceKind || meta.kind || 'phone';
  const samples = Object.fromEntries(SCENARIOS.map(s => [s, []]));
  for (const run of (await allRuns()).sort((a, b) => a.savedAt - b.savedAt)) {
    const it = byId.get(run.id);
    if (it && it.kind === 'run' && !run.result.error) samples[it.scenario].push(run.result);
  }
  const name = `spike2-${kind}-${e.browser}-aot.json`;
  return { name, count: Object.values(samples).reduce((a, l) => a + l.length, 0), body: {
    spike: 2, device: `${first?.model || meta.model || 'unknown model'} (${kind}, on-device runner, no adb)`, browser: e.browser, browserVersion: e.browserVersion, build: 'aot', throttle: 1,
    gitCommit: buildInfo.gitCommit ?? 'unknown', os: e.os,
    machine: { userAgent: e.userAgent, deviceMemory: e.deviceMemory, hardwareConcurrency: e.hardwareConcurrency, privateModeLikely: e.privateModeLikely, kind },
    runProtocol: 'on-device runner: warm page loads (normal reload), one warm-up round, then rounds with the six scenarios in seeded order; no CPU throttling',
    setup: await setupResult(), series: SCENARIOS.map(s => ({ scenario: s, samples: samples[s], summary: summarise(samples[s]) })),
  } };
}

const fileOf = (f) => new File([JSON.stringify(f.body, null, 1) + '\n'], f.name, { type: 'application/json' });
function download(file) { const a = document.createElement('a'); a.href = URL.createObjectURL(file); a.download = file.name; document.body.append(a); a.click(); a.remove(); }
function setStatus(t) { $('status').textContent = t; }
const ms = (s, key) => { const v = s.summary?.[key]?.median; return v == null ? '–' : `${Math.round(v)} ms`; };

async function render() {
  const e = env();
  $('env').textContent = `${e.browser} ${e.browserVersion ?? ''}, ${e.os}, memory class ${e.deviceMemory ?? 'not reported'} GB, ${e.hardwareConcurrency ?? '?'} cores`;
  if (e.privateModeLikely) { $('env').textContent += '. Private mode likely: storage is temporary here; use a normal tab.'; $('env').className = 'bad'; }
  const setup = await setupResult();
  $('data').textContent = setup ? `Ready (dataset checksum matches spike 1; prepared in ${Math.round((setup.phases?.generateMs ?? 0) / 1000)} s generation plus writing).` : 'Not prepared yet.';
  $('data').className = setup ? 'ok' : 'hint';
  const f = await resultFile();
  const table = document.createElement('table');
  table.innerHTML = '<tr><th>Case</th><th>Runs</th><th>Median</th></tr>';
  for (const s of f.body.series) {
    const tr = document.createElement('tr');
    const label = { replay1: '1 today, replay', replay2: '2 blobs, blocks with text', replay3: '3 blobs, joined blocks', structure4: '4 structure / text', erase: '5 erase', sync: '6 sync batch' }[s.scenario];
    const median = s.scenario === 'structure4' ? `${ms(s, 'structureMs')} / ${ms(s, 'textMs')}` : s.scenario === 'erase' ? `branch L2 ${ms(s, 'erase.L2-branch.eraseMs')}, L3 ${ms(s, 'erase.L3-branch.eraseMs')}`
      : s.scenario === 'sync' ? `without ${ms(s, 'sync.A-L1.savedMs')}, with ${ms(s, 'sync.B-L3.savedMs')}` : ms(s, 'replayMs');
    for (const t of [label, String(s.samples.length), median]) { const td = document.createElement('td'); td.textContent = t; tr.append(td); }
    table.append(tr);
  }
  $('results').replaceChildren(table);
}

async function init() {
  try { buildInfo = await (await fetch('build-info.json', { cache: 'no-cache' })).json(); } catch { }
  const meta = (await getState('device')) || {};
  $('model').value = meta.model || ''; $('kind').value = meta.kind || 'phone';
  const saveMeta = () => setState('device', { model: $('model').value.trim(), kind: $('kind').value });
  $('model').addEventListener('change', saveMeta); $('kind').addEventListener('change', saveMeta);
  $('persist').addEventListener('click', async () => setStatus(navigator.storage?.persist ? `Persistent storage: ${await navigator.storage.persist()}` : 'Persistent storage is not available here.'));
  const needModel = () => { if (!$('model').value.trim()) { setStatus('Enter the device model first.'); return true; } return false; };
  const archive = async () => { const st = await getState('queue'); if (st) { const h = (await getState('history')) || []; h.push(st); await setState('history', h); await setState('queue', null); } };
  $('prepare').addEventListener('click', async () => { if (needModel()) return; await saveMeta(); await archive(); await startQueue([{ scenario: 'setup', kind: 'setup', id: `s${Date.now().toString(36)}` }]); });
  $('start').addEventListener('click', async () => {
    if (needModel()) return;
    if (!(await setupResult())) { setStatus('Prepare the test data first (step 2).'); return; }
    await saveMeta(); await archive(); await startQueue(planQueue(Number($('runs').value)));
  });
  $('resume').addEventListener('click', async () => { const st = await getState('queue'); if (st && st.next < st.items.length) location.href = runUrl(st.items[st.next]); else setStatus('No run pending.'); });
  $('skip').addEventListener('click', async () => { const st = await getState('queue'); if (st && st.next < st.items.length) { st.next++; await setState('queue', st); setStatus(`Skipped; ${st.items.length - st.next} runs left. Tap Continue.`); } });
  $('stop').addEventListener('click', async () => { await archive(); setStatus('Stopped. Results so far are kept.'); });
  $('save').addEventListener('click', async () => download(fileOf(await resultFile())));
  $('share').addEventListener('click', async () => {
    const file = fileOf(await resultFile());
    try {
      if (navigator.canShare && navigator.canShare({ files: [file] })) await navigator.share({ files: [file], title: 'Spike 2 results' });
      else { download(file); setStatus('Sharing files is not supported here; the file was downloaded instead.'); }
    } catch (err) { if (err.name !== 'AbortError') setStatus('Share failed: ' + err.message); }
  });
  $('clear').addEventListener('click', async () => { if (confirm('Delete all results on this device? Save them first.')) { await clearRuns(); await setState('history', []); await setState('queue', null); await render(); } });
  await render();
  const st = await getState('queue');
  if (new URLSearchParams(location.search).get('continue') === '1') await continueQueue();
  else setStatus(st && st.next < st.items.length ? `${st.items.length - st.next} runs pending; tap Continue.` : 'Ready.');
}

init().catch(err => setStatus('Error: ' + err.message));
