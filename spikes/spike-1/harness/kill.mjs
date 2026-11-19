// Q3 durability supervisor (plan section 3): starts a browser on the writer page with a persistent profile, kills its
// whole process tree with SIGKILL (or closes it with SIGTERM) at seeded random moments, restarts it on the same
// profile and checks what the restarted page finds against every acknowledged write.
// Usage: node kill.mjs --browser chromium|firefox --variant name --durability relaxed|strict|default
//        --workload mixed|snapshot|draft --kills 200 [--mode kill|open|close]
import { spawn, execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { createServer } from './server.mjs';
import { spikeDir, firefoxPath, gitCommit, machine, stats, writeResult, seededShuffle } from './common.mjs';
import { chromium } from './pw.mjs';

const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : d; };
const browser = opt('--browser', 'chromium'), variant = opt('--variant', 'mixed-relaxed'), durability = opt('--durability', 'relaxed');
const workload = opt('--workload', 'mixed'), kills = Number(opt('--kills', '200')), mode = opt('--mode', 'kill');
const port = Number(opt('--port', '5090'));
const seed = 'thoughttree-spike-1-q3|' + browser + '|' + variant;
const profile = path.join(spikeDir, 'profiles', `q3-${browser}-${variant}`);
fs.rmSync(profile, { recursive: true, force: true });
fs.mkdirSync(profile, { recursive: true });
const rawLog = path.join(spikeDir, 'out', `q3-${browser}-${variant}.jsonl`);
fs.writeFileSync(rawLog, '');

// Seeded delays: kill 100 to 1,500 ms after the first acknowledged write (mode kill/close), or 0 to 600 ms after
// the process start (mode open, kill during database open).
const delays = seededShuffle(Array.from({ length: kills + 1 }, (_, i) => i), seed).map(i => mode === 'open' ? Math.round(i * 600 / kills) : 100 + Math.round(i * 1400 / kills));

let waiters = [];
const events = [];
const srv = await createServer({ port, root: path.join(spikeDir, 'SpikeApp/wwwroot'), data: path.join(spikeDir, 'out'), onPost: (p, body) => {
  const m = JSON.parse(body); m.t = Date.now(); events.push(m); fs.appendFileSync(rawLog, JSON.stringify(m) + '\n');
  waiters = waiters.filter(w => !w(m));
} });
const waitFor = (pred, ms) => new Promise(resolve => { const timer = setTimeout(() => { waiters = waiters.filter(w => w !== f); resolve(null); }, ms); const f = (m) => { if (pred(m)) { clearTimeout(timer); resolve(m); return true; } return false; }; waiters.push(f); });
const sleep = (ms) => new Promise(r => setTimeout(r, ms));

function start(run) {
  const url = `http://localhost:${port}/spike/durability.html?run=${run}&durability=${durability}&workload=${workload}&variant=${variant}&seed=7`;
  const exe = browser === 'chromium' ? chromium.executablePath() : firefoxPath;
  const a = browser === 'chromium'
    ? ['--headless=new', '--no-sandbox', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--disable-background-networking', `--user-data-dir=${profile}`, url]
    : ['-headless', '-no-remote', '-profile', profile, url];
  return spawn(exe, a, { detached: true, stdio: 'ignore' });
}
function killTree(child, signal) { try { process.kill(-child.pid, signal); } catch { } }
const exited = (child, ms) => new Promise(r => { if (child.exitCode !== null || child.signalCode) return r(true); const t = setTimeout(() => r(false), ms); child.once('exit', () => { clearTimeout(t); r(true); }); });

if (browser === 'firefox') fs.writeFileSync(path.join(profile, 'user.js'), [
  'user_pref("browser.shell.checkDefaultBrowser", false);', 'user_pref("datareporting.policy.dataSubmissionEnabled", false);',
  'user_pref("toolkit.telemetry.reportingpolicy.firstRun", false);', 'user_pref("browser.startup.homepage_override.mstone", "ignore");',
  'user_pref("app.update.enabled", false);', 'user_pref("browser.sessionstore.resume_from_crash", false);', 'user_pref("browser.tabs.warnOnClose", false);',
].join('\n'));

const acked = { batches: new Map(), snapshot: 0, draft: 0 };  // batchId -> size
const kills_ = [];
let browserVersion = null;
try { browserVersion = browser === 'chromium' ? execSync(`${chromium.executablePath()} --version`).toString().trim() : execSync(`${firefoxPath} --version`).toString().trim(); } catch { }

for (let run = 1; run <= kills + 1; run++) {
  const child = start(run);
  const t0 = Date.now();
  const verify = await waitFor(m => m.run === run && m.type === 'verify', 60000);
  // Check what this start found against everything acknowledged before.
  if (run > 1) {
    const k = kills_[kills_.length - 1];
    k.openOk = !!verify?.openOk; k.recoveryMs = verify?.recoveryMs ?? null; k.timeToVerifyMs = verify ? verify.t - t0 : null;
    if (verify?.openOk) {
      const foundSet = new Set();
      for (const [r, nos] of Object.entries(verify.found)) for (const no of nos) foundSet.add(`r${r}-b${no}`);
      let missingEvents = 0, missingBatches = 0;
      for (const [id, size] of acked.batches) if (!foundSet.has(id)) { missingBatches++; missingEvents += size; }
      const ackedEvents = [...acked.batches.values()].reduce((a, b) => a + b, 0);
      k.ackedEvents = ackedEvents; k.foundEvents = verify.events; k.missingEvents = missingEvents; k.missingBatches = missingBatches;
      k.beyondLogBatches = foundSet.size - (acked.batches.size - missingBatches);
      k.partialBatches = verify.partial.length;
      k.snapshotLost = (verify.snapshot ?? 0) < acked.snapshot; k.snapshotCorrupt = verify.snapshotCorrupt;
      k.draftLost = (verify.draft ?? 0) < acked.draft;
      // Unacknowledged batches found by this start are now durable facts of the store; count them as acknowledged from here.
      for (const [r, nos] of Object.entries(verify.found)) for (const no of nos) { const id = `r${r}-b${no}`; if (!acked.batches.has(id)) acked.batches.set(id, null); }
    }
    k.lost = !k.openOk || k.missingEvents > 0 || k.partialBatches > 0 || k.snapshotLost || k.snapshotCorrupt || k.draftLost;
    if (k.lost) console.log('FINDING', JSON.stringify(k));
  }
  if (run === kills + 1) { killTree(child, 'SIGKILL'); await exited(child, 5000); break; }
  // Write, then kill (or close) at the seeded moment.
  const delay = delays[run - 1];
  if (mode === 'open') await sleep(delay);
  else { await waitFor(m => m.run === run && m.type === 'ack', 30000); await sleep(delay); }
  const before = events.filter(m => m.run === run && m.type === 'ack').length;
  killTree(child, mode === 'close' ? 'SIGTERM' : 'SIGKILL');
  const gone = await exited(child, 15000);
  if (!gone) { killTree(child, 'SIGKILL'); await exited(child, 5000); }
  await sleep(200); // late acknowledgements in flight are not counted: they were sent after the kill signal
  for (const m of events.filter(m => m.run === run && m.type === 'ack' && m.t <= Date.now())) {
    if (m.kind === 'events') acked.batches.set(`r${run}-b${m.batchNo}`, m.size);
    if (m.kind === 'snapshot') acked.snapshot = Math.max(acked.snapshot, m.version);
    if (m.kind === 'draft') acked.draft = Math.max(acked.draft, m.version);
  }
  kills_.push({ run, delayMs: delay, acksBeforeKill: before, acksInRun: events.filter(m => m.run === run && m.type === 'ack').length, orderlyCloseOk: mode === 'close' ? gone : undefined });
  if (run % 20 === 0) console.log(`${browser} ${variant}: ${run}/${kills} kills, findings ${kills_.filter(k => k.lost).length}`);
}
srv.close();
// fix the acked map: batches recorded as null are those found beyond the log; sizes are not needed further.
const done = kills_.filter(k => k.openOk !== undefined);
const result = {
  question: 'q3', device: 'desktop (agent cloud container)', browser, browserVersion, build: 'n/a (storage is JavaScript)', os: machine().os, machine: machine(),
  gitCommit: gitCommit(), seed, variant, durability, workload, mode, kills: done.length,
  method: 'browser process group started on durability.html with a persistent profile; ' + (mode === 'close' ? 'SIGTERM (orderly browser close)' : 'SIGKILL') + (mode === 'open' ? ' 0 to 600 ms after start (during database open)' : ' 100 to 1,500 ms after the first acknowledged write') + '; the next start checks the store against all acknowledged writes',
  summary: {
    findings: done.filter(k => k.lost).length, openFailures: done.filter(k => !k.openOk).length, missingEvents: done.reduce((a, k) => a + (k.missingEvents || 0), 0),
    partialBatches: done.reduce((a, k) => a + (k.partialBatches || 0), 0), snapshotLost: done.filter(k => k.snapshotLost).length, snapshotCorrupt: done.filter(k => k.snapshotCorrupt).length,
    draftLost: done.filter(k => k.draftLost).length, beyondLogBatches: done.reduce((a, k) => a + (k.beyondLogBatches || 0), 0),
    acksPerRun: stats(done.map(k => k.acksInRun)), recoveryMs: stats(done.map(k => k.recoveryMs)), timeToVerifyMs: stats(done.map(k => k.timeToVerifyMs)),
    finalStoredEvents: done.at(-1)?.foundEvents ?? null,
  },
  samples: done,
};
writeResult(`q3-desktop-${browser}-${variant}.json`, result);
console.log(JSON.stringify(result.summary));
