// Generates the tables of the spike 1 report from the result files (plan section 4: tables are generated, not typed).
// Replaces the answers table between <!-- answers:start --> and <!-- answers:end --> in docs/spikes/spike-1-report.md
// and writes the detailed tables T1 to T9 to docs/spikes/spike-1/tables.md (kept out of the report for its length limit). Usage: node summarise.mjs [--print]
import fs from 'node:fs';
import path from 'node:path';
import { resultsDir, repoDir } from './common.mjs';

const read = (f) => { const p = path.join(resultsDir, f); return fs.existsSync(p) ? JSON.parse(fs.readFileSync(p, 'utf8')) : null; };
const files = fs.existsSync(resultsDir) ? fs.readdirSync(resultsDir).filter(f => f.endsWith('.json')).sort() : [];
const s = (x) => x == null ? '–' : x >= 10000 ? (x / 1000).toFixed(1) + ' s' : x >= 1000 ? (x / 1000).toFixed(2) + ' s' : Math.round(x) + ' ms';
const mb = (b) => b == null ? '–' : (b / 1e6).toFixed(2) + ' MB';
const kb = (b) => b == null ? '–' : Math.round(b / 1000) + ' kB';
const med = (sum, k) => sum?.[k]?.median ?? null;
const p95 = (sum, k) => sum?.[k]?.p95 ?? null;
const pair = (sum, k) => sum?.[k] ? `${s(med(sum, k))} (p95 ${s(p95(sum, k))})` : '–';
const table = (head, rows) => [`| ${head.join(' | ')} |`, `| ${head.map(() => '---').join(' | ')} |`, ...rows.map(r => `| ${r.join(' | ')} |`)].join('\n');
const devOrder = ['desktop', 'throttled4x', 'throttled6x', 'phone'];
const devLabel = { desktop: 'desktop', throttled4x: 'desktop 4x throttled¹', throttled6x: 'desktop 6x throttled¹', phone: 'phone' };

// ---- Q1 and Q2 series ----
const q1 = files.filter(f => /^q[12]-(desktop|throttled\dx|phone)-(chromium|firefox)-(interpreted|aot)\.json$/.test(f)).map(f => ({ f, ...read(f) }));
const key = (f) => { const m = f.match(/^q\d-([a-z0-9]+)-([a-z]+)-([a-z]+)\.json$/); return { dev: m[1], browser: m[2], build: m[3] }; };
const combos = [...new Set(q1.map(r => { const k = key(r.f); return `${k.dev}|${k.browser}|${k.build}`; }))]
  .sort((a, b) => devOrder.indexOf(a.split('|')[0]) - devOrder.indexOf(b.split('|')[0]) || a.localeCompare(b));
const find = (dev, browser, build, scenario, dataset = 'events-100000') => {
  for (const r of q1) { const k = key(r.f); if (k.dev === dev && k.browser === browser && k.build === build) { const x = r.series.find(x => x.scenario === scenario && x.dataset === dataset && !x.extra); if (x) return x; } }
  return null;
};
const replay = (x, cache) => { const v = x?.[cache]; if (!v?.length) return null; const a = v.map(r => r.treeVisibleMs - r.appReadyMs).sort((p, q) => p - q); return a[Math.floor((a.length - 1) / 2)]; };

const t1 = [], t2 = [], t3 = [], answers = {};
for (const c of combos) {
  const [dev, browser, build] = c.split('|');
  const snap = find(dev, browser, build, 'snapshot'), full = find(dev, browser, build, 'full'), imp = find(dev, browser, build, 'import'), empty = find(dev, browser, build, 'empty');
  t1.push([devLabel[dev], browser, build, pair(snap?.summary.cold, 'treeVisibleMs'), pair(snap?.summary.warm, 'treeVisibleMs'), pair(full?.summary.cold, 'treeVisibleMs'), pair(full?.summary.warm, 'treeVisibleMs'), pair(imp?.summary.cold, 'treeVisibleMs'), pair(empty?.summary.cold, 'appReadyMs')]);
  const w = full?.summary.warm;
  if (w) t2.push([devLabel[dev], browser, build, s(med(w, 'jsPhases.openMs')), s(med(w, 'jsPhases.readMs')), s(med(w, 'jsPhases.decryptMs')), s(med(w, 'jsPhases.unpadJoinMs') + med(w, 'phases.copyMs')), s(med(w, 'phases.parseMs')), s(med(w, 'phases.applyMs')), mb(med(w, 'memoryAfterTree.wasmMemory')), mb(med(w, 'memoryAfterTree.jsHeapUsed'))]);
  const r1 = replay(find(dev, browser, build, 'full', 'events-1000'), 'warm'), r10 = replay(find(dev, browser, build, 'full', 'events-10000'), 'warm'), r100 = replay(full, 'warm');
  if (r10 && r100) t3.push([devLabel[dev], browser, build, s(r1), s(r10), s(r100), (r10 / r1).toFixed(1) + '×', (r100 / r10).toFixed(1) + '×', r100 / r10 <= 10 ? 'yes' : '**no**']);
  answers[c] = { snapCold: med(snap?.summary.cold, 'treeVisibleMs'), snapWarm: med(snap?.summary.warm, 'treeVisibleMs'), fullCold: med(full?.summary.cold, 'treeVisibleMs'), fullWarm: med(full?.summary.warm, 'treeVisibleMs'), ratio: r10 && r100 ? r100 / r10 : null, emptyCold: med(empty?.summary.cold, 'appReadyMs'), importCold: med(imp?.summary.cold, 'treeVisibleMs') };
}

// ---- Q2 sizes ----
const q2 = read('q2-sizes-desktop.json');
const t4 = (q2?.builds ?? []).map(b => {
  const a6 = answers[`throttled6x|chromium|${b.build}`], a1 = answers[`desktop|chromium|${b.build}`];
  return [b.build, mb(b.firstLoad.brotli), mb(b.firstLoad.gzip), mb(b.firstLoad.raw), b.firstLoad.measuredTransferBrotli ? mb(b.firstLoad.measuredTransferBrotli.median) : '–', s(a1?.emptyCold), s(a6?.emptyCold), Math.round(b.publishSeconds) + ' s'];
});

// ---- Q3 durability ----
const q3 = files.filter(f => f.startsWith('q3-')).map(f => read(f));
const t5 = q3.map(r => [r.device.startsWith('desktop') ? 'desktop' : 'phone', r.browser + ' ' + (r.browserVersion || '').replace(/^[A-Za-z ]+/, '').trim(), r.variant, r.mode === 'close' ? 'SIGTERM close' : r.mode === 'open' ? 'SIGKILL during open' : 'SIGKILL while writing', r.kills, r.summary.findings === 0 ? '0' : `**${r.summary.findings}**`, r.summary.missingEvents, r.summary.partialBatches, r.summary.snapshotLost + r.summary.snapshotCorrupt, r.summary.draftLost, r.summary.openFailures, s(r.summary.recoveryMs?.median)]);

// ---- Q4 search ----
const q4 = files.filter(f => /^q4-/.test(f)).map(f => ({ f, ...read(f) }));
const t6 = [];
for (const r of q4.sort((a, b) => devOrder.indexOf(key(a.f).dev) - devOrder.indexOf(key(b.f).dev) || a.f.localeCompare(b.f))) {
  const k = key(r.f);
  for (const x of r.series.filter(x => x.scenario === 'snapshot')) {
    const w = x.summary.warm, v = x.extra.replace('&search=', '');
    const qs = ['oneWord', 'twoWords', 'threeWords', 'prefix', 'germanEquivalent', 'accentFree'];
    const worstMed = Math.max(...qs.map(q => med(w, `queries.${q}.medianMs`) ?? 0)), worstP95 = Math.max(...qs.map(q => med(w, `queries.${q}.p95Ms`) ?? 0));
    const stored = v === 'A' ? '–' : kb(r.setup?.[ 'events-100000']?.[v === 'B' ? 'indexStoredBytesB' : 'indexStoredBytesC']);
    t6.push([devLabel[k.dev], k.browser, k.build, v, s(med(w, 'treeVisibleMs')), s(med(w, 'indexReadyMs')), s(med(w, 'treeVisibleToFirstHitsMs')), s(med(w, 'startToFirstHitsMs')), worstMed.toFixed(2) + ' / ' + worstP95.toFixed(2) + ' ms', stored, (med(w, 'indexUpdatePerEventUs') ?? 0).toFixed(0) + ' µs']);
  }
  const rebuild = r.series.find(x => x.scenario === 'full');
  if (rebuild) t6.push([devLabel[k.dev], k.browser, k.build, 'A after update', s(med(rebuild.summary.warm, 'treeVisibleMs')), s(med(rebuild.summary.warm, 'indexReadyMs')), s(med(rebuild.summary.warm, 'treeVisibleToFirstHitsMs')), s(med(rebuild.summary.warm, 'startToFirstHitsMs')), '', '', '']);
}

// ---- Q5 sizes ----
const q5 = read('q5-sizes-computed.json');
const t7 = (q5?.bucketLists ?? []).map(b => [b.list, b.sizesUsed, mb(b.paddedBytes), b.overheadPercent + ' %', Math.round(b.meanPaddedBytesPerEvent) + ' B']);
const q5n = files.filter(f => /^q5-desktop-/.test(f)).map(f => ({ f, ...read(f) }));
const t8 = [];
for (const r of q5n) for (const x of r.series) {
  const c = x.summary.cold;
  const ds = x.dataset.replace('longnote-', '');
  t8.push([r.build, ds, s(med(c, 'jsPhases.importMs')), s(med(c, 'jsPhases.decryptMs')), s(med(c, 'phases.parseMs') + med(c, 'phases.applyMs')), s(med(c, 'treeVisibleMs')), s(med(c, 'noteIo.saveMedianMs')), s(med(c, 'noteIo.loadMedianMs')), mb(med(c, 'memoryAfterTree.wasmMemory')), mb(med(c, 'storage.usage'))]);
}
const pc = q5?.pairingCode ?? [];
const t9 = pc.filter(p => p.trees === 1 || (p.nameChars === 64 && p.addressChars === 100)).map(p => [p.trees, p.nameChars, p.addressChars, p.binaryBytes, p.base32Chars, Object.entries(p.fits).filter(([, v]) => v.byteM).map(([k]) => k)[0] ?? 'none', Object.entries(p.fits).filter(([, v]) => v.base32AlnumM).map(([k]) => k)[0] ?? 'none']);

// ---- answers ----
const a = (c) => answers[c] ?? {};
const ok = (v, lim) => v == null ? '–' : v <= lim ? 'meets' : '**misses**';
const q3total = q3.filter(r => r.device.startsWith('desktop')).reduce((acc, r) => ({ kills: acc.kills + r.kills, findings: acc.findings + r.summary.findings }), { kills: 0, findings: 0 });
const ansRows = [];
for (const build of ['interpreted', 'aot']) for (const dev of ['desktop', 'throttled4x', 'throttled6x', 'phone']) {
  const x = a(`${dev}|chromium|${build}`);
  if (!x.snapCold) continue;
  ansRows.push([`Q1/Q2 ${devLabel[dev]}, ${build}`, `snapshot start ${s(x.snapCold)} cold, ${s(x.snapWarm)} warm (PF1 1 s: ${ok(x.snapCold, 1000)}); full replay ${s(x.fullCold)} cold (10 s: ${ok(x.fullCold, 10000)}); 10× data ${x.ratio?.toFixed(1)}× time (PF6: ${ok(x.ratio, 10)}); import ${s(x.importCold)}`]);
}
for (const b of q2?.builds ?? []) ansRows.push([`Q2 download, ${b.build}`, `${mb(b.firstLoad.brotli)} Brotli, ${mb(b.firstLoad.gzip)} gzip, ${mb(b.firstLoad.raw)} raw (${b.firstLoad.files} files); publish ${Math.round(b.publishSeconds)} s`]);
if (q3.length) ansRows.push(['Q3 durability (desktop)', `${q3total.kills} kills or closes over ${q3.filter(r => r.device.startsWith('desktop')).length} variants; findings (lost or half-visible data, unreadable store): ${q3total.findings}`]);

const answersMd = table(['Question', 'Measured (generated from the result files)'], ansRows);
const tablesMd = [
  '### T1 Start and replay, 100,000 events (median, p95 in brackets)',
  'Tree visible = from navigation start to the first render of the top level, including app start.',
  '',
  table(['Device', 'Browser', 'Build', 'Snapshot start, cold', 'Snapshot start, warm', 'Full replay, cold', 'Full replay, warm', 'Import into empty store', 'Empty app ready, cold'], t1),
  '',
  '### T2 Where the full replay goes (warm medians) and memory after it',
  '',
  table(['Device', 'Browser', 'Build', 'Open', 'Read', 'Decrypt', 'Unpad and copy', 'Parse', 'Apply', 'WASM memory', 'JS heap'], t2),
  '',
  '### T3 Complexity ratio (replay only: tree visible minus app ready, warm medians)',
  '',
  table(['Device', 'Browser', 'Build', '1,000 events', '10,000', '100,000', '10k/1k', '100k/10k', 'PF6 (≤ 10×)'], t3),
  '',
  '### T4 Download size of the first load and app start (Q2)',
  '',
  table(['Build', 'Brotli', 'gzip', 'Raw', 'Measured transfer', 'Empty app ready, cold, desktop', 'Same, 6x throttled¹', 'Publish time'], t4),
  '',
  '### T5 Durability (Q3)',
  '',
  table(['Device', 'Browser', 'Variant', 'Kill', 'Kills', 'Findings', 'Events lost', 'Half batches', 'Snapshot lost or corrupt', 'Draft lost', 'Open failed', 'Recovery (open and read all)'], t5),
  '',
  '### T6 Search on the snapshot start, 10,000 items (Q4; warm medians)',
  '',
  table(['Device', 'Browser', 'Build', 'Variant', 'Tree visible', 'Index ready', 'Tree visible to first hits', 'Start to first hits', 'Query median / p95 (worst kind)', 'Stored', 'Update per event'], t6),
  '',
  '### T7 Bucket lists on the 100,000-event dataset (Q5)',
  '',
  table(['Bucket list', 'Sizes used', 'Padded bytes', 'Overhead', 'Mean per event'], t7),
  '',
  '### T8 Long notes: 1,000 events, 100 notes of the given length, import into an empty store (Q5; cold medians, desktop Chromium)',
  '',
  table(['Build', 'Note length (chars)', 'Import', 'Decrypt', 'Parse and apply', 'Tree visible', 'Save one note', 'Load one note', 'WASM memory', 'Stored'], t8),
  '',
  '### T9 Pairing code size against QR capacity (Q5, ADR-028; layout is a spike assumption)',
  '',
  table(['Trees', 'Name chars', 'Address chars', 'Binary bytes', 'Base32 chars', 'Smallest QR version, byte mode, level M', 'Same, base32 alphanumeric, level M'], t9),
  '',
  '¹ CPU throttling of desktop Chromium as the phone fallback: phone unverified, estimated error about a factor of two (plan section 2).',
].join('\n');

const reportPath = path.join(repoDir, 'docs/spikes/spike-1-report.md');
const tablesPath = path.join(repoDir, 'docs/spikes/spike-1/tables.md');
const tablesDoc = `# Spike 1: result tables\n\nGenerated by \`node spikes/spike-1/harness/summarise.mjs\` from \`data/results/\`; do not edit by hand. Report: [spike-1-report.md](../spike-1-report.md).\n\n${tablesMd}\n`;
if (process.argv.includes('--print') || !fs.existsSync(reportPath)) { console.log(answersMd + '\n\n' + tablesMd); }
else {
  let r = fs.readFileSync(reportPath, 'utf8');
  r = r.replace(/<!-- answers:start -->[\s\S]*<!-- answers:end -->/, `<!-- answers:start -->\n${answersMd}\n<!-- answers:end -->`);
  fs.writeFileSync(reportPath, r);
  fs.writeFileSync(tablesPath, tablesDoc);
  console.log('updated', reportPath, 'and', tablesPath);
}
