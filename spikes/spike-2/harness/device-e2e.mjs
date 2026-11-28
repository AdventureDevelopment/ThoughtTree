// Runs the on-device flow of spike 2 in headless Chromium with phone emulation, against the assembled device site
// served with its own headers (strict CSP): prepare the test data, a quick series (3 rounds), save the result file.
// Fails on a policy violation, a failed run or a wrong checksum. Usage: node harness/device-e2e.mjs (after publish-device.sh)
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import { createServer } from '../../spike-1/harness/server.mjs';

const require = createRequire(import.meta.url);
const { chromium, devices } = require('playwright');
const here = path.dirname(new URL(import.meta.url).pathname);
const site = path.resolve(here, '../publish/device-site');
const headers = JSON.parse(fs.readFileSync(path.join(site, 'staticwebapp.config.json'), 'utf8')).globalHeaders;
const server = await createServer({ port: 5280, root: site, data: site, extraHeaders: headers });
const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'spike2-e2e-'));
const ctx = await chromium.launchPersistentContext(profile, { ...devices['Pixel 7'], acceptDownloads: true });
const page = ctx.pages()[0] || await ctx.newPage();
const problems = [];
page.on('console', m => { if (m.type() === 'error' || /Content Security Policy|Refused/.test(m.text())) problems.push(m.text()); });
const status = () => page.textContent('#status');
const waitStatus = async (re, ms) => { const end = Date.now() + ms; while (Date.now() < end) { try { const s = await status(); if (re.test(s)) return s; if (/failed|Error/.test(s)) throw new Error(s); } catch (e) { if (/failed|Error/.test(e.message)) throw e; } await page.waitForTimeout(1000); } throw new Error('timeout, status: ' + await status()); };

await page.goto('http://localhost:5280/');
await page.waitForURL(/device\.html/);
await waitStatus(/Ready/, 30000);
await page.fill('#model', 'Emulated Pixel 7');
await page.dispatchEvent('#model', 'change');
await page.click('#prepare');
console.log(await waitStatus(/Test data ready/, 900000));
await page.selectOption('#runs', '3');
await page.click('#start');
console.log(await waitStatus(/^Done/, 1800000));
const [download] = await Promise.all([page.waitForEvent('download'), page.click('#save')]);
const file = path.join(profile, download.suggestedFilename());
await download.saveAs(file);
const result = JSON.parse(fs.readFileSync(file, 'utf8'));
const bad = result.series.filter(s => s.samples.length !== 3 || s.samples.some(x => x.checksumOk === false || x.error));
console.log(download.suggestedFilename(), result.series.map(s => `${s.scenario}: ${s.samples.length}`).join(', '));
console.log('setup ok:', result.setup?.generatorChecksumOk, 'policy problems:', problems.length);
await ctx.close();
server.close();
fs.rmSync(profile, { recursive: true, force: true });
if (problems.length || bad.length || !result.setup) { console.error('FAILED', problems.slice(0, 5), bad.map(s => s.scenario)); process.exit(1); }
console.log('device flow ok');
