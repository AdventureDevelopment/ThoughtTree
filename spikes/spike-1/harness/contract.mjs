// Runs the storage contract tests (plan section 2) in Chromium and Firefox and writes the result file.
import path from 'node:path';
import { chromium, firefox } from './pw.mjs';
import { createServer } from './server.mjs';
import { spikeDir, firefoxPath, gitCommit, machine, writeResult } from './common.mjs';

const root = path.join(spikeDir, 'SpikeApp/wwwroot');
const srv = await createServer({ port: 5098, root, data: path.join(spikeDir, 'out') });
const out = { question: 'S2 storage contract', device: 'desktop', machine: machine(), gitCommit: gitCommit(), browsers: [] };
for (const [name, type, opts] of [['chromium', chromium, {}], ['firefox', firefox, { executablePath: firefoxPath }]]) {
  const b = await type.launch(opts);
  const page = await b.newPage();
  await page.goto('http://localhost:5098/spike/contract.html');
  await page.waitForFunction(() => window.contractResults, null, { timeout: 120000 });
  const results = await page.evaluate(() => window.contractResults);
  out.browsers.push({ browser: name, version: b.version(), passed: results.every(r => r.ok), results });
  console.log(name, b.version(), results.filter(r => r.ok).length + '/' + results.length);
  for (const r of results.filter(r => !r.ok)) console.log('  FAIL', r.name, r.detail);
  await b.close();
}
srv.close();
writeResult('s2-contract-desktop.json', out);
