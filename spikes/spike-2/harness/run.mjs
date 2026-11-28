// Desktop series of spike 2 in headless Chromium (Playwright): one scenario per page load in a persistent profile, so
// the three layouts written by the setup stay in IndexedDB between runs. Scenarios run interleaved (seeded order per
// round), after one warm-up round. Writes docs/spikes/spike-2/data/results/<device>-chromium-<build>.json.
// Usage: node harness/run.mjs [--build aot] [--runs 10] [--throttle 1] [--scenarios replay1,replay2,replay3,structure4,erase,sync] [--setup]
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { createServer } from '../../spike-1/harness/server.mjs';
import { summarise, seededShuffle } from '../Spike2App/wwwroot/spike/stats.js';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : d; };
const here = path.dirname(new URL(import.meta.url).pathname);
const spikeDir = path.resolve(here, '..');
const repoDir = path.resolve(spikeDir, '../..');
const build = opt('--build', 'aot');
const runs = Number(opt('--runs', '10'));
const throttle = Number(opt('--throttle', '1'));
const scenarios = opt('--scenarios', 'replay1,replay2,replay3,structure4,erase,sync').split(',');
const port = 5200 + (build === 'aot' ? 1 : 0);
const log = (...a) => console.log(new Date().toISOString().slice(11, 19), ...a);

const root = path.join(spikeDir, 'publish', build, 'wwwroot');
const server = await createServer({ port, root, data: path.join(spikeDir, 'out') });
const ctx = await chromium.launchPersistentContext(path.join(spikeDir, 'out', 'profile-' + build), { args: ['--enable-precise-memory-info'] });
const version = ctx.browser()?.version() ?? null;

async function run(scenario) {
  const page = await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: throttle });
  await page.goto(`http://localhost:${port}/?scenario=${scenario}`, { waitUntil: 'commit', timeout: 120000 });
  await page.waitForFunction(() => window.spikeDone === true, null, { timeout: 3600000, polling: 250 });
  const result = await page.evaluate(() => window.spikeResult);
  await page.close();
  if (result.error) throw new Error(`${scenario}: ${result.error.slice(0, 500)}`);
  return result;
}

let setup = null;
const probe = await run('replay1').catch(e => e);
if (args.includes('--setup') || probe instanceof Error) {
  log('setup: generating and writing the three layouts');
  setup = await run('setup');
  log('setup done', JSON.stringify(setup.sizes));
}

const samples = Object.fromEntries(scenarios.map(s => [s, []]));
log('warm-up round');
for (const s of scenarios) await run(s);
for (let round = 0; round < runs; round++) {
  for (const s of seededShuffle(scenarios, `spike-2|${build}|${throttle}|${round}`)) {
    const r = await run(s);
    samples[s].push(r);
    log(`round ${round + 1}/${runs} ${s}: replay ${r.replayMs} ms${r.textMs ? `, text ${r.textMs} ms` : ''}, checksum ${r.checksumOk ?? '-'}`);
  }
}
await ctx.close();
server.close();

const commit = (() => { try { const head = execSync('git rev-parse HEAD', { cwd: repoDir }).toString().trim(); return head + (execSync('git status --porcelain -- spikes/spike-2', { cwd: repoDir }).toString().trim() ? '+dirty' : ''); } catch { return 'unknown'; } })();
const device = throttle === 1 ? 'desktop' : `throttled${throttle}x`;
const out = {
  spike: 2, device: throttle === 1 ? 'desktop (agent cloud container)' : `desktop Chromium with CPU throttling ${throttle}x (phone fallback, unverified)`,
  browser: 'chromium', browserVersion: version, build, throttle, gitCommit: commit,
  machine: { os: `${os.type()} ${os.release()} ${os.arch()}`, cpu: os.cpus()[0]?.model ?? 'unknown', cpuCount: os.cpus().length, memoryGB: Math.round(os.totalmem() / 2 ** 30) },
  runProtocol: `headless Chromium, persistent profile, warm page loads (HTTP cache), one warm-up round, then ${runs} rounds with the scenarios in seeded order`,
  setup, series: scenarios.map(s => ({ scenario: s, samples: samples[s], summary: summarise(samples[s]) })),
};
const dir = path.join(repoDir, 'docs/spikes/spike-2/data/results');
fs.mkdirSync(dir, { recursive: true });
const file = path.join(dir, `${device}-chromium-${build}.json`);
fs.writeFileSync(file, JSON.stringify(out, null, 1) + '\n');
log('wrote', path.relative(repoDir, file));
