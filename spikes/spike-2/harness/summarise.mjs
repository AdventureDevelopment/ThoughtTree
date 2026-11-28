// Builds the result tables of docs/spikes/spike-2-report.md from every result file in docs/spikes/spike-2/data/results
// (desktop series of harness/run.mjs and the device runner's files alike) and replaces the part between
// <!-- results:start --> and <!-- results:end -->. Usage: node harness/summarise.mjs [--print]
import fs from 'node:fs';
import path from 'node:path';

const here = path.dirname(new URL(import.meta.url).pathname);
const repoDir = path.resolve(here, '../../..');
const dir = path.join(repoDir, 'docs/spikes/spike-2/data/results');
const reportFile = path.join(repoDir, 'docs/spikes/spike-2-report.md');
const files = fs.existsSync(dir) ? fs.readdirSync(dir).filter(f => f.endsWith('.json')).sort() : [];
const results = files.map(f => ({ file: f, ...JSON.parse(fs.readFileSync(path.join(dir, f), 'utf8')) }));

const sec = (v) => v == null ? '–' : v >= 1000 ? `${(v / 1000).toFixed(2)} s` : `${Math.round(v)} ms`;
const pct = (v, base) => v == null || base == null ? '–' : `${v >= base ? '+' : ''}${Math.round((v / base - 1) * 100)} %`;
const mb = (b) => `${(b / 1e6).toFixed(1)} MB`;
const med = (r, scenario, key) => r.series.find(s => s.scenario === scenario)?.summary?.[key]?.median;
const p95 = (r, scenario, key) => r.series.find(s => s.scenario === scenario)?.summary?.[key]?.p95;
const runs = (r, scenario) => r.series.find(s => s.scenario === scenario)?.samples.length ?? 0;
const label = (r) => `${r.device.replace(/ \(agent cloud container\)/, '').replace(/desktop Chromium with CPU throttling (\d)x.*/, 'desktop, CPU throttled $1x¹')}, ${r.browser} ${(r.browserVersion ?? '').split('.')[0]}, ${r.build}`;
const lines = [];

lines.push('**Replay of 100,000 events (cases 1 to 3) and structure first (case 4).** Median (p95) from start of reading to the tree on screen; change against case 1.', '');
lines.push('| Configuration | Runs | (1) today | (2) blobs, blocks with text | (3) blobs, joined blocks | (4) tree without text | (4) text of 50 visible items | (4) all text joined |');
lines.push('| --- | --- | --- | --- | --- | --- | --- | --- |');
for (const r of results) {
  const base = med(r, 'replay1', 'replayMs');
  const cell = (s, k) => `${sec(med(r, s, k))} (${sec(p95(r, s, k))})`;
  lines.push(`| ${label(r)} | ${runs(r, 'replay1')} | ${cell('replay1', 'replayMs')} | ${cell('replay2', 'replayMs')}, **${pct(med(r, 'replay2', 'replayMs'), base)}** | ${cell('replay3', 'replayMs')}, **${pct(med(r, 'replay3', 'replayMs'), base)}** | ${cell('structure4', 'structureMs')}, ${pct(med(r, 'structure4', 'structureMs'), base)} | ${cell('structure4', 'textMs')} | ${cell('structure4', 'allTextMs')} |`);
}

lines.push('', '**Where the replay time goes** (medians; JavaScript: IndexedDB read and Web Crypto; .NET: JSON parsing and applying, case 3 including the join).', '');
lines.push('| Configuration | Case | Read | Decrypt | Parse | Apply (and join) |');
lines.push('| --- | --- | --- | --- | --- | --- |');
for (const r of results) for (const s of ['replay1', 'replay2', 'replay3']) {
  lines.push(`| ${label(r)} | ${s.slice(-1)} | ${sec(med(r, s, 'jsPhases.readMs'))} | ${sec(med(r, s, 'jsPhases.decryptMs'))} | ${sec(med(r, s, 'phases.parseMs'))} | ${sec(med(r, s, s === 'replay3' ? 'phases.joinApplyMs' : 'phases.applyMs'))} |`);
}

lines.push('', '**Erasing (case 5).** One item without children (median text history) and one top-level branch of about 100 items; blobs deleted and affected blocks rebuilt in one transaction. Median time; blobs and blocks touched.', '');
lines.push('| Configuration | Leaf, (2) blocks with text | Leaf, (3) content blocks | Branch, (2) blocks with text | Branch, (3) content blocks |');
lines.push('| --- | --- | --- | --- | --- |');
for (const r of results) {
  const s = r.series.find(x => x.scenario === 'erase')?.samples[0]?.erase;
  const c = (k) => `${sec(med(r, 'erase', `erase.${k}.eraseMs`))}${s ? ` (${s[k].blobs} blobs, ${s[k].blocks} blocks)` : ''}`;
  lines.push(`| ${label(r)} | ${c('L2-leaf')} | ${c('L3-leaf')} | ${c('L2-branch')} | ${c('L3-branch')} |`);
}

lines.push('', '**Writing a sync batch of 1,000 events (case 6).** "Saved": serialise, pad, encrypt and store the envelopes in one transaction (the acknowledgement); "block": the block written afterwards. Wire: envelope bytes a sync would send.', '');
lines.push('| Configuration | Without blobs: saved / block | With blobs, (2): saved / block | With blobs, (3): saved / block | Wire without / with blobs |');
lines.push('| --- | --- | --- | --- | --- |');
for (const r of results) {
  const c = (v) => `${sec(med(r, 'sync', `sync.${v}.savedMs`))} / ${sec(med(r, 'sync', `sync.${v}.blockMs`))}`;
  const w = (v) => med(r, 'sync', `sync.${v}.wireBytes`);
  lines.push(`| ${label(r)} | ${c('A-L1')} | ${c('B-L2')} | ${c('B-L3')} | ${w('A-L1') ? `${mb(w('A-L1'))} / ${mb(w('B-L3'))} (${pct(w('B-L3'), w('A-L1'))}, ${med(r, 'sync', 'sync.B-L3.blobs')} blobs)` : '–'} |`);
}

const setup = results.find(r => r.setup?.sizes)?.setup;
if (setup) {
  const z = setup.sizes;
  const layout = {
    '(1) today': ['L1.events', 'L1.blocks'],
    '(2) blobs, blocks with text': ['L23.events', 'L23.blobs', 'L2.blocks'],
    '(3) blobs, joined blocks': ['L23.events', 'L23.blobs', 'L3.eblocks', 'L3.cblocks'],
  };
  const sum = (keys, f) => keys.reduce((a, k) => a + z[k][f], 0);
  lines.push('', `**Stored size of the 100,000 events** (computed by the setup run: ciphertext, nonce and ids; IndexedDB's own overhead not included). Padding overhead: padded against unpadded plaintext. ${setup.blobs} of 100,000 events carry text.`, '');
  lines.push('| Layout | Envelopes and blobs | Padding overhead of envelopes and blobs | Local blocks | Total stored | Against (1) |');
  lines.push('| --- | --- | --- | --- | --- | --- |');
  const base = sum(layout['(1) today'], 'storedBytes');
  for (const [name, keys] of Object.entries(layout)) {
    const env = keys.filter(k => /events|blobs/.test(k)), blk = keys.filter(k => /blocks/.test(k));
    lines.push(`| ${name} | ${mb(sum(env, 'storedBytes'))} | ${pct(sum(env, 'paddedBytes'), sum(env, 'plainBytes'))} | ${mb(sum(blk, 'storedBytes'))} | ${mb(sum(keys, 'storedBytes'))} | ${pct(sum(keys, 'storedBytes'), base)} |`);
  }
  lines.push('', `Detail: event envelopes today ${mb(z['L1.events'].plainBytes)} plaintext, padded ${mb(z['L1.events'].paddedBytes)}; without text ${mb(z['L23.events'].plainBytes)}, padded ${mb(z['L23.events'].paddedBytes)}; ${z['L23.blobs'].count} blobs ${mb(z['L23.blobs'].plainBytes)}, padded ${mb(z['L23.blobs'].paddedBytes)} (${pct(z['L23.blobs'].paddedBytes, z['L23.blobs'].plainBytes)}).`);
}
lines.push('', `Result files: ${files.map(f => `\`${f}\``).join(', ') || 'none'}.`);

const table = lines.join('\n');
if (process.argv.includes('--print') || !fs.existsSync(reportFile)) { console.log(table); process.exit(0); }
const report = fs.readFileSync(reportFile, 'utf8');
const updated = report.replace(/<!-- results:start -->[\s\S]*<!-- results:end -->/, `<!-- results:start -->\n${table}\n<!-- results:end -->`);
fs.writeFileSync(reportFile, updated);
console.log('updated', path.relative(repoDir, reportFile));
