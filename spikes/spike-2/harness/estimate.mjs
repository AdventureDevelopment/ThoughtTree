// Estimates phone and tablet numbers of spike 2 from its desktop Chromium series, part by part, with spike 1's device
// measurements on the same devices as reference (docs/spikes/spike-1/tables.md T2 and T10). An estimate, not a
// measurement. Replaces the part of docs/spikes/spike-2-report.md between <!-- estimate:start --> and <!-- estimate:end -->.
// Usage: node harness/estimate.mjs [--print]
import fs from 'node:fs';
import path from 'node:path';

const here = path.dirname(new URL(import.meta.url).pathname);
const repoDir = path.resolve(here, '../../..');
const dir = path.join(repoDir, 'docs/spikes/spike-2/data/results');
const reportFile = path.join(repoDir, 'docs/spikes/spike-2-report.md');

// Spike 1, T2: warm medians of the full replay per event (ms). Desktop reference is Chromium, as spike 2's desktop runs.
const T2 = {
  desktop: { aot: { read: 1460, decrypt: 709, copy: 199, parse: 919, apply: 61 }, interpreted: { read: 1530, decrypt: 743, copy: 195, parse: 3440, apply: 290 } },
  'phone, Chrome': { aot: { read: 3940, decrypt: 7000, copy: 274, parse: 2360, apply: 142 }, interpreted: { read: 4140, decrypt: 7150, copy: 264, parse: 7350, apply: 530 } },
  'phone, Firefox': { aot: { read: 2250, decrypt: 1910, copy: 183, parse: 2170, apply: 128 }, interpreted: { read: 2360, decrypt: 1880, copy: 183, parse: 9810, apply: 505 } },
  'tablet, Chrome': { aot: { read: 3430, decrypt: 3810, copy: 209, parse: 1840, apply: 119 }, interpreted: { read: 3450, decrypt: 3830, copy: 183, parse: 6170, apply: 513 } },
  'desktop, CPU throttled 4x (check of the method)': { aot: { read: 3410, decrypt: 4150, copy: 926, parse: 4570, apply: 333 }, interpreted: { read: 3290, decrypt: 3900, copy: 909, parse: 17800, apply: 1350 } },
};
// Spike 1, T10: blocks of 1,000 events on the phone (ms), holding 115,999,744 bytes of padded payloads.
const T10 = { 'phone, Chrome': { read: 279, decrypt: 896 }, 'phone, Firefox': { read: 646, decrypt: 185 } };
const T10_BYTES = 115_999_744;
const DEVICES = ['phone, Chrome', 'phone, Firefox', 'tablet, Chrome', 'desktop, CPU throttled 4x (check of the method)'];

const results = fs.readdirSync(dir).filter(f => /^desktop-chromium-(aot|interpreted)\.json$/.test(f)).map(f => JSON.parse(fs.readFileSync(path.join(dir, f), 'utf8')));
const setup = results.find(r => r.setup?.sizes)?.setup ?? JSON.parse(fs.readFileSync(path.join(dir, 'desktop-chromium-aot.json'), 'utf8')).setup;
const blockBytes = {
  replay1: setup.sizes['L1.blocks'].paddedBytes, replay2: setup.sizes['L2.blocks'].paddedBytes,
  replay3: setup.sizes['L3.eblocks'].paddedBytes + setup.sizes['L3.cblocks'].paddedBytes, structure4: setup.sizes['L3.eblocks'].paddedBytes,
};
const med = (r, s, k) => r.series.find(x => x.scenario === s)?.summary?.[k]?.median ?? 0;
const ratio = (device, build, part) => T2[device][build][part] / T2.desktop[build][part];

/** Desktop parts of a replay case (ms): read, decrypt, copy (JavaScript join plus .NET copy), parse, apply (with join), rest. */
function parts(r, s) {
  const p = { read: med(r, s, 'jsPhases.readMs'), decrypt: med(r, s, 'jsPhases.decryptMs'), copy: med(r, s, 'jsPhases.joinMs') + med(r, s, 'phases.copyMs'),
    parse: med(r, s, 'phases.parseMs'), apply: med(r, s, s === 'replay3' ? 'phases.joinApplyMs' : 'phases.applyMs') };
  const total = med(r, s, s === 'structure4' ? 'structureMs' : 'replayMs');
  p.rest = Math.max(0, total - Object.values(p).reduce((a, b) => a + b, 0));
  return p;
}

/** Low and high estimate (ms) of a replay case on a device: read and decrypt by spike 1's per-event ratio and by its block measurement (T10, scaled by bytes); the rest by its own ratio. */
function estimate(r, s, device, build) {
  const p = parts(r, s);
  const scaled = (part) => p[part] * ratio(device, build, part);
  const common = scaled('copy') + scaled('parse') + scaled('apply') + p.rest * ratio(device, build, 'apply');
  const perEvent = scaled('read') + scaled('decrypt');
  const t10 = T10[device] ?? Object.fromEntries(['read', 'decrypt'].map(k => [k, T10['phone, Chrome'][k] * T2[device][build][k] / T2['phone, Chrome'][build][k]]));
  const blocks = (t10.read + t10.decrypt) * blockBytes[s] / T10_BYTES;
  return { low: common + Math.min(perEvent, blocks), high: common + Math.max(perEvent, blocks), parseShare: scaled('parse') / (common + Math.min(perEvent, blocks)) };
}

const sec = (v) => v >= 1000 ? `${(v / 1000).toFixed(1)} s` : `${Math.round(v)} ms`;
const range = (e) => `${sec(e.low)} to ${sec(e.high)}`;
const pct = (v) => `${v >= 0 ? '+' : ''}${Math.round(v * 100)} %`;
const lines = [];
for (const build of ['aot', 'interpreted']) {
  const r = results.find(x => x.build === build);
  if (!r) continue;
  lines.push('', `**${build === 'aot' ? 'AOT (the release build)' : 'Interpreted (for comparison)'}**, from \`desktop-chromium-${build}.json\`. Estimated time to the tree on screen, low to high, and the change of (2) and (3) against (1) at the low and the high end.`, '');
  lines.push('| Device (spike 1 reference) | (1) today | (2) blobs, blocks with text | (3) blobs, joined blocks | (2) against (1) | (3) against (1) | (4) tree without text | Desktop measured: (2), (3) against (1) |');
  lines.push('| --- | --- | --- | --- | --- | --- | --- | --- |');
  const d1 = med(r, 'replay1', 'replayMs');
  const desk = `${pct(med(r, 'replay2', 'replayMs') / d1 - 1)}, ${pct(med(r, 'replay3', 'replayMs') / d1 - 1)}`;
  for (const device of DEVICES) {
    const [e1, e2, e3, e4] = ['replay1', 'replay2', 'replay3', 'structure4'].map(s => estimate(r, s, device, build));
    const rel = (e) => `${pct(e.low / e1.low - 1)} / ${pct(e.high / e1.high - 1)}`;
    lines.push(`| ${device} | ${range(e1)} | ${range(e2)} | ${range(e3)} | **${rel(e2)}** | **${rel(e3)}** | ${range(e4)} | ${desk} |`);
  }
}

const aot = results.find(x => x.build === 'aot');
if (aot) {
  lines.push('', '**Cases 4 to 6 on the phone (AOT, Chrome / Firefox), rougher.** Each recorded part scaled by the nearest spike 1 ratio (read, decrypt, parse); writes have no spike 1 reference and are scaled like reads.', '');
  lines.push('| Case | Desktop | Phone, Chrome | Phone, Firefox |');
  lines.push('| --- | --- | --- | --- |');
  const scale = (device, partsMs) => Object.entries(partsMs).reduce((a, [kind, v]) => a + v * ratio(device, 'aot', kind), 0);
  const v = (k) => med(aot, 'structure4', k);
  const textParts = { read: v('jsPhases.blobReadMs'), decrypt: v('jsPhases.blobDecryptMs'), parse: Math.max(0, v('phases.visibleTextMs') - v('jsPhases.blobReadMs') - v('jsPhases.blobDecryptMs')) };
  const row = (name, desktop, fn) => lines.push(`| ${name} | ${sec(desktop)} | ${sec(fn('phone, Chrome'))} | ${sec(fn('phone, Firefox'))} |`);
  row('(4) text of 50 visible items after the tree', v('phases.visibleTextMs'), d => scale(d, textParts));
  row('(4) all text joined in the background, after the tree', v('phases.backgroundTextMs'), d => v('phases.backgroundTextMs') * ratio(d, 'aot', 'parse'));
  for (const k of ['L2-leaf', 'L3-leaf', 'L2-branch', 'L3-branch']) {
    const e = (f) => med(aot, 'erase', `erase.${k}.${f}`);
    const erasedParts = { read: e('readMs') + e('writeMs'), decrypt: e('decryptMs') + e('encryptMs'), parse: e('rewriteMs') };
    row(`(5) erase ${k.replace('L2', '(2)').replace('L3', '(3)').replace('-', ', ')}`, e('eraseMs'), d => scale(d, erasedParts));
  }
  for (const [k, name] of [['A-L1', 'without blobs'], ['B-L3', 'with blobs, (3)']]) {
    const e = (f) => med(aot, 'sync', `sync.${k}.${f}`);
    row(`(6) sync batch ${name}: saved`, e('savedMs'), d => scale(d, { parse: e('serializeMs'), decrypt: e('encryptMs'), read: e('storeMs') }));
  }
}

const table = lines.join('\n').trim();
if (process.argv.includes('--print')) { console.log(table); process.exit(0); }
const report = fs.readFileSync(reportFile, 'utf8');
fs.writeFileSync(reportFile, report.replace(/<!-- estimate:start -->[\s\S]*<!-- estimate:end -->/, `<!-- estimate:start -->\n${table}\n<!-- estimate:end -->`));
console.log('updated', path.relative(repoDir, reportFile));
