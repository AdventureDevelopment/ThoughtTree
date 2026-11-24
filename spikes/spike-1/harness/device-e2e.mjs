// End-to-end check of the on-device runner in headless Chromium with phone emulation, served with the device site's
// own staticwebapp.config.json headers (strict CSP). Not a measurement: it checks that the phone steps work.
// Usage: node device-e2e.mjs [--runs 3] [--out dir] [--firefox] [--port 5080]   (after harness/publish-device.sh)
import fs from 'node:fs';
import path from 'node:path';
import { chromium, firefox, devices } from './pw.mjs';
import { createServer } from './server.mjs';
import { spikeDir, firefoxPath } from './common.mjs';

const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : d; };
const site = path.join(spikeDir, 'publish/device-site');
const outDir = path.resolve(opt('--out', path.join(spikeDir, 'out/device-e2e')));
fs.mkdirSync(outDir, { recursive: true });
const headers = JSON.parse(fs.readFileSync(path.join(site, 'staticwebapp.config.json'), 'utf8')).globalHeaders;
const server = await createServer({ port: Number(opt('--port', '5080')), root: site, data: site, extraHeaders: headers });
const useFirefox = args.includes('--firefox');
const browser = useFirefox ? await firefox.launch({ executablePath: firefoxPath }) : await chromium.launch();
const ctx = await browser.newContext(useFirefox ? { acceptDownloads: true, viewport: { width: 412, height: 915 } } : { ...devices['Pixel 7'], acceptDownloads: true });
const violations = [];
await ctx.addInitScript(() => document.addEventListener('securitypolicyviolation', e => console.log(`CSP-VIOLATION ${e.violatedDirective} ${e.blockedURI}`)));
const page = await ctx.newPage();
page.on('console', m => { if (m.text().startsWith('CSP-VIOLATION') || /Content Security Policy/.test(m.text())) violations.push(`${page.url().slice(0, 80)} ${m.text()}`); });
page.on('pageerror', e => console.log('page error', page.url().slice(0, 80), e.message));
const status = () => page.locator('[data-testid=device-status]').textContent();
async function waitDone(label, minutes) {
  const t0 = Date.now();
  for (;;) {
    await page.waitForTimeout(3000);
    let s = ''; try { if (page.url().includes('device.html')) s = await status(); } catch { }
    if (/^Done|did not finish|failed|Error/.test(s)) { console.log(`${label}: ${s} (${Math.round((Date.now() - t0) / 1000)} s)`); return s; }
    if (Date.now() - t0 > minutes * 60000) throw new Error(label + ' timed out');
  }
}

const base = `http://localhost:${opt('--port', '5080')}`;
await page.goto(base + '/');
await page.waitForURL(/device\.html/);
await page.fill('[data-testid=model]', 'desktop Chromium, Pixel 7 emulation (e2e check)');
await page.locator('[data-testid=model]').dispatchEvent('change');
await page.click('[data-testid=prepare]');
await waitDone('prepare', 30);
console.log((await page.locator('#data').innerText()).replace(/\n/g, ' | '));
await page.selectOption('[data-testid=runs]', opt('--runs', '3'));
await page.click('[data-testid=start]');
await waitDone('series', 90);
const files = [];
page.on('download', async d => { const f = path.join(outDir, d.suggestedFilename()); await d.saveAs(f); files.push(f); });
await page.click('[data-testid=save-all]');
await page.waitForTimeout(3000);
console.log('files:', files.map(f => path.basename(f)).join(', '));
for (const f of files) {
  const r = JSON.parse(fs.readFileSync(f, 'utf8'));
  for (const s of r.series) {
    const all = [...s.cold, ...s.warm];
    const tr = (k) => s.summary[k] ? Math.round(s.summary[k]['environment.transfer.transferSize']?.median ?? -1) : '-';
    console.log(`  ${path.basename(f)} ${s.scenario} ${s.dataset}${s.extra}: cold ${s.cold.length} warm ${s.warm.length}, checksum ok ${all.every(x => x.checksumOk !== false)}, errors ${all.filter(x => x.error).length}, transfer cold ${tr('cold')} warm ${tr('warm')} B`);
  }
}

// Durability page: three rounds with a reload instead of a swipe (checks the bookkeeping, not durability).
await page.goto(base + '/interpreted/spike/durability-device.html');
for (let i = 0; i < 3; i++) {
  await page.waitForSelector('[data-testid=q3-start]:not([disabled])');
  await page.click('[data-testid=q3-start]');
  await page.waitForTimeout(1500);
  await page.reload();
  await page.waitForFunction(() => /Ready|Target/.test(document.getElementById('status').textContent));
}
console.log('q3:', await page.locator('[data-testid=q3-counter]').textContent(), '|', await page.locator('[data-testid=q3-last]').textContent());
console.log('CSP violations:', violations.length ? violations : 'none');
await browser.close(); server.close();
