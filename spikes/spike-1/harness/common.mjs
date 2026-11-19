// Shared helpers of the spike harness: manifest, environment record, statistics, seeded order.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execSync } from 'node:child_process';

export const here = path.dirname(new URL(import.meta.url).pathname);
export const spikeDir = path.resolve(here, '..');
export const repoDir = path.resolve(spikeDir, '../..');
export const resultsDir = path.join(repoDir, 'docs/spikes/spike-1/data/results');
export const manifest = () => JSON.parse(fs.readFileSync(path.join(spikeDir, 'out/manifest.json'), 'utf8'));
export const firefoxPath = process.env.SPIKE_FIREFOX || '/root/pw-firefox/firefox-1543/firefox/firefox';

export function datasetInfo(name) {
  const m = manifest();
  const d = [...m.datasets, ...(m.longNoteDatasets ?? [])].find(x => x.name === name);
  if (!d) throw new Error('dataset not in manifest: ' + name);
  return { m, d };
}

/** Query string with the test key label, key id, account id and expected checksums of a dataset. */
export function datasetQuery(name) {
  const { m, d } = datasetInfo(name);
  return new URLSearchParams({ dataset: name, seedLabel: m.seedLabel, keyId: m.format.keyId, accountId: m.format.accountId,
    checksum: d.checksumFinal, snapshotChecksum: d.checksumAtSnapshot, snapshotAt: String(d.snapshotAfterEvent) }).toString();
}

export function gitCommit() {
  try {
    const head = execSync('git rev-parse HEAD', { cwd: repoDir }).toString().trim();
    const dirty = execSync('git status --porcelain -- spikes/spike-1', { cwd: repoDir }).toString().trim().length > 0;
    return dirty ? head + '+dirty' : head;
  } catch { return 'unknown'; }
}

export function machine() {
  const cpus = os.cpus();
  return { os: `${os.type()} ${os.release()} ${os.arch()}`, cpu: cpus[0]?.model ?? 'unknown', cpuCount: cpus.length, memoryGB: Math.round(os.totalmem() / 2 ** 30), kind: 'cloud container (agent environment)' };
}

export function stats(values) {
  const v = values.filter(x => typeof x === 'number' && Number.isFinite(x)).sort((a, b) => a - b);
  if (v.length === 0) return null;
  const q = p => v[Math.min(v.length - 1, Math.ceil(p * v.length) - 1)];
  const r = x => Math.round(x * 1000) / 1000;
  return { n: v.length, median: r(v.length % 2 ? v[(v.length - 1) / 2] : (v[v.length / 2 - 1] + v[v.length / 2]) / 2), p95: r(q(0.95)), min: r(v[0]), max: r(v[v.length - 1]) };
}

/** Flattens numeric fields of a result to dotted paths. */
export function flatten(obj, prefix = '', out = {}) {
  for (const [k, val] of Object.entries(obj || {})) {
    const key = prefix ? prefix + '.' + k : k;
    if (typeof val === 'number') out[key] = val;
    else if (val && typeof val === 'object' && !Array.isArray(val)) flatten(val, key, out);
  }
  return out;
}

export function summarise(samples) {
  const flat = samples.map(s => flatten(s));
  const keys = [...new Set(flat.flatMap(f => Object.keys(f)))].filter(k => !k.startsWith('params.') && !k.startsWith('environment.resources'));
  const out = {};
  for (const k of keys.sort()) { const s = stats(flat.map(f => f[k])); if (s) out[k] = s; }
  return out;
}

/** SplitMix64-based seeded shuffle (record order randomised by the seed, plan section 2). */
export function seededShuffle(list, seedLabel) {
  let h = 14695981039346656037n;
  for (const b of Buffer.from(seedLabel)) { h ^= BigInt(b); h = (h * 1099511628211n) & 0xffffffffffffffffn; }
  let s = h;
  const next = () => { s = (s + 0x9E3779B97F4A7C15n) & 0xffffffffffffffffn; let z = s; z = ((z ^ (z >> 30n)) * 0xBF58476D1CE4E5B9n) & 0xffffffffffffffffn; z = ((z ^ (z >> 27n)) * 0x94D049BB133111EBn) & 0xffffffffffffffffn; return z ^ (z >> 31n); };
  const a = [...list];
  for (let i = a.length - 1; i > 0; i--) { const j = Number(next() % BigInt(i + 1)); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}

export function writeResult(file, obj) {
  fs.mkdirSync(resultsDir, { recursive: true });
  fs.writeFileSync(path.join(resultsDir, file), JSON.stringify(obj, null, 1) + '\n');
  console.log('wrote', path.join('docs/spikes/spike-1/data/results', file));
}
