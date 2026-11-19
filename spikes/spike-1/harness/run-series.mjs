// Runs measurement series of spike 1 (plan section 2 "Run protocol", Q1, Q2, Q4, Q5) with Playwright and writes one
// JSON file per question, device, browser and build to docs/spikes/spike-1/data/results.
// Usage: node run-series.mjs --plan main|tuning|search|longnotes [--browsers chromium,firefox] [--builds interpreted,aot]
//        [--throttles 1,4,6] [--runs 10] [--chunk 5000 --batch 1000]
import fs from 'node:fs';
import path from 'node:path';
import { chromium, firefox } from './pw.mjs';
import { createServer } from './server.mjs';
import { spikeDir, firefoxPath, datasetQuery, gitCommit, machine, summarise, seededShuffle, writeResult } from './common.mjs';

const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : d; };
const plan = opt('--plan', 'main');
const browsers = opt('--browsers', 'chromium').split(',');
const builds = opt('--builds', 'interpreted,aot').split(',');
const throttles = opt('--throttles', '1').split(',').map(Number);
const RUNS = Number(opt('--runs', '10'));
const tuning = { chunk: opt('--chunk', '5000'), batch: opt('--batch', '1000'), parallel: opt('--parallel', '1') };
const SEED = 'thoughttree-spike-1-reference-v1';
const ports = { interpreted: 5000, aot: 5001 };
const profiles = path.join(spikeDir, 'profiles');
const log = (...a) => console.log(new Date().toISOString().slice(11, 19), ...a);

const servers = {};
for (const b of builds) servers[b] = await createServer({ port: ports[b], root: path.join(spikeDir, 'publish', b, 'wwwroot'), data: path.join(spikeDir, 'out') });

const deviceName = (t) => t === 1 ? 'desktop' : `throttled${t}x`;
const deviceLabel = (t) => t === 1 ? 'desktop (agent cloud container)' : `desktop Chromium with CPU throttling ${t}x as phone fallback (phone unverified, error about a factor of two)`;

async function launch(browser, profile) {
  const type = browser === 'chromium' ? chromium : firefox;
  const options = browser === 'chromium' ? { args: ['--enable-precise-memory-info'] } : { executablePath: firefoxPath };
  const ctx = await type.launchPersistentContext(profile, options);
  const page = ctx.pages()[0] || await ctx.newPage();
  return { ctx, page, version: ctx.browser()?.version() ?? null };
}

async function prepare(browser, page, throttle, cold, port) {
  if (browser === 'chromium') {
    const cdp = await page.context().newCDPSession(page);
    if (cold) {
      await cdp.send('Network.clearBrowserCache');
      await cdp.send('Storage.clearDataForOrigin', { origin: `http://localhost:${port}`, storageTypes: 'cache_storage,service_workers' });
    }
    await cdp.send('Emulation.setCPUThrottlingRate', { rate: throttle });
  }
}

function clearFirefoxCache(profile) {
  fs.rmSync(path.join(profile, 'cache2'), { recursive: true, force: true });
  const storage = path.join(profile, 'storage/default');
  if (fs.existsSync(storage)) for (const d of fs.readdirSync(storage)) fs.rmSync(path.join(storage, d, 'cache'), { recursive: true, force: true });
}

async function load(page, url) {
  await page.goto(url, { waitUntil: 'commit', timeout: 120000 });
  await page.waitForFunction(() => window.spikeDone === true, null, { timeout: 1800000, polling: 250 });
  const r = await page.evaluate(() => window.spikeResult);
  if (r.error) log('  ERROR', r.error.slice(0, 300));
  return r;
}

const urlFor = (build, scenario, dataset, extra = '') =>
  `http://localhost:${ports[build]}/?scenario=${scenario}&${datasetQuery(dataset)}&chunk=${tuning.chunk}&batch=${tuning.batch}&parallel=${tuning.parallel}${extra}`;

async function ensureSetup(browser, build, profile, datasets) {
  const marker = path.join(profile, 'spike-setup.json');
  const done = fs.existsSync(marker) ? JSON.parse(fs.readFileSync(marker, 'utf8')) : {};
  const todo = datasets.filter(d => !done[d]);
  if (!todo.length) return done;
  const { ctx, page } = await launch(browser, profile);
  for (const d of todo) {
    log(`setup ${browser} ${build} ${d}`);
    const r = await load(page, urlFor(build, 'setup', d));
    if (r.error || !r.checksumOk || !r.checksumAtSnapshotOk) throw new Error('setup failed: ' + JSON.stringify(r).slice(0, 500));
    done[d] = { treeVisibleMs: r.treeVisibleMs, snapshotPlainBytes: r.snapshotPlainBytes, indexTerms: r.indexTerms, indexPostings: r.indexPostings, indexPlainBytes: r.indexPlainBytes, indexStoredBytesB: r.indexStoredBytesB, indexStoredBytesC: r.indexStoredBytesC, storage: r.storage, phases: r.phases, jsPhases: r.jsPhases };
  }
  await ctx.close();
  fs.writeFileSync(marker, JSON.stringify(done));
  return done;
}

/** One warm-up, then `runs` cold loads (new browser process, empty HTTP and cache storage) and `runs` warm reloads. */
async function series(browser, build, profile, throttle, scenario, dataset, extra, { cold = true, warm = true, runs = RUNS } = {}) {
  const url = urlFor(build, scenario, dataset, extra);
  const out = { scenario, dataset, extra, throttle, url: url.replace(/checksum=[^&]+&?/g, ''), cold: [], warm: [] };
  let version = null;
  if (cold) {
    for (let i = 0; i <= runs; i++) {
      if (browser === 'firefox') clearFirefoxCache(profile);
      const { ctx, page, version: v } = await launch(browser, profile);
      version = v;
      await prepare(browser, page, throttle, true, ports[build]);
      const r = await load(page, url);
      await ctx.close();
      if (i > 0) out.cold.push(r); // run 0 is the warm-up
    }
  }
  if (warm) {
    const { ctx, page, version: v } = await launch(browser, profile);
    version = v;
    await prepare(browser, page, throttle, false, ports[build]);
    await load(page, url); // warm-up
    for (let i = 0; i < runs; i++) out.warm.push(await load(page, url));
    await ctx.close();
  }
  out.browserVersion = version;
  out.summary = { cold: out.cold.length ? summarise(out.cold) : null, warm: out.warm.length ? summarise(out.warm) : null };
  const vis = (k) => out.summary[k]?.treeVisibleMs?.median ?? out.summary[k]?.appReadyMs?.median;
  log(`  ${browser} ${build} ${throttle}x ${scenario} ${dataset}${extra}: cold ${vis('cold')} warm ${vis('warm')} ms; checksum ok ${[...out.cold, ...out.warm].every(r => r.checksumOk !== false)}`);
  return out;
}

function save(question, browser, build, throttle, seriesList, setup) {
  const file = `${question}-${deviceName(throttle)}-${browser}-${build}.json`;
  const existing = fs.existsSync(path.join(spikeDir, '../../docs/spikes/spike-1/data/results', file))
    ? JSON.parse(fs.readFileSync(path.join(spikeDir, '../../docs/spikes/spike-1/data/results', file), 'utf8')) : null;
  const keep = (existing?.series ?? []).filter(s => !seriesList.some(n => n.scenario === s.scenario && n.dataset === s.dataset && n.extra === s.extra));
  writeResult(file, {
    question, device: deviceLabel(throttle), browser, browserVersion: seriesList.find(s => s.browserVersion)?.browserVersion ?? existing?.browserVersion ?? null,
    build, os: machine().os, machine: machine(), gitCommit: gitCommit(), seed: SEED, runProtocol: `1 warm-up, ${RUNS} cold (new browser process, HTTP cache and cache storage cleared), ${RUNS} warm (reload in the same process); series order shuffled with the seed`,
    tuning, setup: setup ?? existing?.setup ?? null, series: [...keep, ...seriesList],
  });
}

const t0 = Date.now();
for (const browser of browsers) {
  for (const build of builds) {
    const profile = path.join(profiles, `${browser}-${build}`);
    if (plan === 'tuning') {
      const setup = await ensureSetup(browser, build, profile, ['events-100000']);
      const combos = [];
      for (const chunk of ['1000', '5000', '10000']) for (const batch of ['100', '1000', '5000']) combos.push({ chunk, batch, parallel: '1' });
      combos.push({ chunk: '5000', batch: '1', parallel: '0' });
      const list = [];
      for (const c of seededShuffle(combos, SEED + 'tuning')) {
        Object.assign(tuning, c);
        const s = await series(browser, build, profile, 1, 'full', 'events-100000', `&tune=${c.chunk}-${c.batch}-${c.parallel}`, { cold: false, runs: 5 });
        s.tuning = { ...c };
        list.push(s);
      }
      save('s3-tuning', browser, build, 1, list, setup);
      continue;
    }
    for (const throttle of throttles) {
      if (browser === 'firefox' && throttle !== 1) continue;
      if (plan === 'main') {
        const setup = await ensureSetup(browser, build, profile, ['events-100000', 'events-10000', 'events-1000']);
        const items = seededShuffle([
          ['q2', 'empty', 'events-100000', '', {}],
          ['q1', 'snapshot', 'events-100000', '', {}],
          ['q1', 'full', 'events-100000', '', {}],
          ['q1', 'full', 'events-10000', '', { cold: false }],
          ['q1', 'full', 'events-1000', '', { cold: false }],
        ], `${SEED}|${browser}|${build}|${throttle}`);
        for (const [q, sc, ds, extra, o] of items) save(q, browser, build, throttle, [await series(browser, build, profile, throttle, sc, ds, extra, o)], setup);
        // Import into an empty store: a separate profile, because it replaces the database (PF4 scaled).
        save('q1', browser, build, throttle, [await series(browser, build, profile + '-import', throttle, 'import', 'events-100000', '', { warm: false })], setup);
      } else if (plan === 'search') {
        const setup = await ensureSetup(browser, build, profile, ['events-100000']);
        const list = [];
        for (const v of seededShuffle(['A', 'B', 'C'], `${SEED}|search|${browser}|${build}|${throttle}`))
          list.push(await series(browser, build, profile, throttle, 'snapshot', 'events-100000', `&search=${v}`, { cold: false }));
        if (throttle === 1) list.push(await series(browser, build, profile, throttle, 'full', 'events-100000', '&search=A', { cold: false, runs: 5 }));
        save('q4', browser, build, throttle, list, setup);
      } else if (plan === 'longnotes') {
        const list = [];
        for (const size of ['10k', '50k', '200k', '1m'])
          list.push(await series(browser, build, profile + '-longnotes', throttle, 'import', `longnote-${size}`, '&noteio=1', { warm: false, runs: 3 }));
        save('q5', browser, build, throttle, list, null);
      }
    }
  }
}
for (const s of Object.values(servers)) s.close();
log(`done in ${Math.round((Date.now() - t0) / 60000)} min`);
