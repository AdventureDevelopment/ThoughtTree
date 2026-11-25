import { chromium } from './pw.mjs';
import { createServer } from './server.mjs';
import { datasetQuery, spikeDir } from './common.mjs';
import path from 'node:path';
const build = process.argv[2] || 'interpreted', ds = process.argv[3] || 'events-1000';
const srv = await createServer({ port: 5099, root: path.join(spikeDir, 'publish', build, 'wwwroot'), data: path.join(spikeDir, 'out') });
const ctx = await chromium.launchPersistentContext(path.join(spikeDir, 'profiles/smoke-' + build), {});
const page = ctx.pages()[0] || await ctx.newPage();
page.on('console', m => { if (m.type() === 'error') console.log('console:', m.text().slice(0, 300)); });
for (const sc of (process.argv[4] || 'empty,setup,snapshot&search=A,snapshot&search=B,snapshot&search=C,full,import&noteio=1').split(',')) {
  await page.goto(`http://localhost:5099/?scenario=${sc}&${datasetQuery(ds)}`);
  await page.waitForFunction(() => window.spikeDone === true, null, { timeout: 600000 });
  const r = await page.evaluate(() => window.spikeResult);
  console.log(sc, JSON.stringify({ err: r.error?.slice(0, 400), app: r.appReadyMs, vis: r.treeVisibleMs, ok: r.checksumOk, snapOk: r.checksumAtSnapshotOk, phases: r.phases, js: r.jsPhases, q: r.queries?.oneWord, fh: r.treeVisibleToFirstHitsMs, ix: [r.indexTerms, r.indexPlainBytes, r.indexStoredBytesB], upd: r.indexUpdatePerEventUs, noteIo: r.noteIo, mem: r.memoryAfterTree, xfer: r.environment?.transfer }));
}
await ctx.close(); srv.close();
