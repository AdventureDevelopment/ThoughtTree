// Q2: exact download sizes of both builds (all files of the first load, raw, Brotli and gzip) and the publish time.
// The files of the first load are taken from a cold run of the empty app (resource timing), so the list is what the
// browser fetched; the transfer size measured in that run is reported as well.
import fs from 'node:fs';
import path from 'node:path';
import { spikeDir, resultsDir, gitCommit, machine, writeResult } from './common.mjs';

const times = JSON.parse(fs.readFileSync(path.join(spikeDir, 'publish/build-times.json'), 'utf8'));
const out = { question: 'q2', device: 'n/a (files of the published builds)', gitCommit: gitCommit(), machine: machine(), builds: [] };
const size = (f) => fs.existsSync(f) ? fs.statSync(f).size : null;
for (const build of ['interpreted', 'aot']) {
  const root = path.join(spikeDir, 'publish', build, 'wwwroot');
  const q2 = path.join(resultsDir, `q2-desktop-chromium-${build}.json`);
  let requested = null, measuredTransfer = null;
  if (fs.existsSync(q2)) {
    const s = JSON.parse(fs.readFileSync(q2, 'utf8')).series.find(x => x.scenario === 'empty');
    const r = s?.cold?.[0];
    if (r) { requested = r.environment.resources.map(x => new URL(x.name, 'http://x').pathname); measuredTransfer = s.summary.cold['environment.transfer.transferSize']; }
  }
  const files = [];
  const fw = path.join(root, '_framework');
  const all = fs.readdirSync(fw).filter(f => !f.endsWith('.br') && !f.endsWith('.gz')).map(f => '/_framework/' + f);
  for (const f of ['/index.html', '/spike/interop.js', '/spike/storage.js', ...all]) {
    const p = path.join(root, f);
    files.push({ file: f, raw: size(p), br: size(p + '.br') ?? size(p), gz: size(p + '.gz') ?? size(p), loadedAtStart: requested ? (f === '/index.html' || requested.includes(f)) : null });
  }
  const loaded = files.filter(f => f.loadedAtStart !== false);
  const sum = (list, k) => list.reduce((a, f) => a + (f[k] || 0), 0);
  out.builds.push({
    build, publishSeconds: times[build],
    firstLoad: { files: loaded.length, raw: sum(loaded, 'raw'), brotli: sum(loaded, 'br'), gzip: sum(loaded, 'gz'), measuredTransferBrotli: measuredTransfer },
    allFrameworkFiles: { files: all.length, raw: sum(files, 'raw'), brotli: sum(files, 'br'), gzip: sum(files, 'gz') },
    largest: [...files].sort((a, b) => b.br - a.br).slice(0, 6),
    files,
  });
}
writeResult('q2-sizes-desktop.json', out);
for (const b of out.builds) console.log(b.build, JSON.stringify(b.firstLoad), 'publish s', b.publishSeconds);
