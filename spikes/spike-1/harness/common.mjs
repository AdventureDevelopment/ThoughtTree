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

// stats, flatten, summarise and seededShuffle are shared with the on-device runner.
export { stats, flatten, summarise, seededShuffle } from '../SpikeApp/wwwroot/spike/stats.js';

export function writeResult(file, obj) {
  fs.mkdirSync(resultsDir, { recursive: true });
  fs.writeFileSync(path.join(resultsDir, file), JSON.stringify(obj, null, 1) + '\n');
  console.log('wrote', path.join('docs/spikes/spike-1/data/results', file));
}
