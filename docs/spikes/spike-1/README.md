# Spike 1: how to reproduce and how to run the phone part

Plan: [spike-1-storage-and-aot.md](../spike-1-storage-and-aot.md). Report: [spike-1-report.md](../spike-1-report.md). Tables: [tables.md](tables.md) (generated). Code: `spikes/spike-1/` (disposable, not part of `src/` or the gate). Results: `data/results/`, one JSON file per question, device, browser and build, with raw samples, median, p95, browser version, OS, device, build mode, git commit and seed.

## Status (updated after each stage)

Last update: 2026-11-19 16:10 UTC, branch `claude/spike-1`. Use it to resume if the agent session ends; each series writes its own result file, so a missing file means the series has not run.

| Stage | State | Result files |
| --- | --- | --- |
| S1 dataset generator, manifest, determinism | done | `data/manifest.json` |
| S2 storage interface, contract tests (Chromium, Firefox) | done | `s2-contract-desktop.json` |
| S3 tuning of chunk and batch sizes (chosen: chunk 10,000, batch 1,000, parallel) | done | `s3-tuning-desktop-chromium-interpreted.json` |
| Q5 bucket lists and pairing code sizes (computed) | done | `q5-sizes-computed.json` |
| Q1/Q2 Chromium interpreted, 1x, 4x, 6x | done | `q1-…-chromium-interpreted.json`, `q2-…` |
| Q1/Q2 Chromium AOT, 1x, 4x, 6x | done | `q1-…-chromium-aot.json`, `q2-…` |
| Q1/Q2 Firefox, both builds, 1x | done | `q1-desktop-firefox-*.json` |
| Q4 search A, B, C (Chromium 1x, 4x, 6x; Firefox 1x) | done | `q4-*.json` |
| Q5 long notes (Chromium 1x, both builds) | done | `q5-desktop-chromium-*.json` |
| Q3 kill tests (11 variants, Chromium and Firefox) | done, 940 kills or closes, 0 findings | `q3-desktop-*.json` |
| Q2 file sizes with the measured first-load file list | done | `q2-sizes-desktop.json` |
| Report `spike-1-report.md` and generated tables `tables.md` | written | – |
| S8 phone run (owner) | waiting for the owner | `*-phone-*.json` |

To resume in a new session: install the toolchain, generate the datasets (their SHA-256 must match the manifest), run `harness/publish.sh`, then run only the lines of `harness/run-all.sh` whose result files are missing, then `node harness/sizes.mjs` and `node harness/summarise.mjs`, and write the report.

## Toolchain

- .NET SDK 10.0.404 (`global.json`) and the WebAssembly workload: `dotnet workload install wasm-tools` (needed for the AOT build).
- Node.js 20 or later and Playwright (any recent version; the agent environment had Playwright 1.63 with Chromium 157 preinstalled). Elsewhere: `cd spikes/spike-1/harness && npm install playwright && npx playwright install chromium firefox`.
- Firefox for the kill tests: Playwright's Firefox build (`npx playwright install firefox`); set `SPIKE_FIREFOX=/path/to/firefox` if it is not at `/root/pw-firefox/firefox-1543/firefox/firefox`.

## Regenerate everything (desktop)

Run from `spikes/spike-1`:

| Step | Command |
| --- | --- |
| Generate the datasets (into `out/`, not committed) and the manifest | `cd DatasetGenerator && dotnet run -c Release -- generate --out ../out --manifest ../../../docs/spikes/spike-1/data/manifest.json && cd ..` |
| Check the determinism: the SHA-256 of `out/events-100000.jsonl` equals `datasets[2].sha256` in the manifest | `sha256sum out/events-100000.jsonl` |
| Decrypt and replay in .NET, compare with `checksumFinal` | `cd DatasetGenerator && dotnet run -c Release -- verify --file ../out/events-100000.jsonl && cd ..` |
| Q5 bucket lists and pairing code sizes | `cd DatasetGenerator && dotnet run -c Release -- sizes --out ../out --result ../../../docs/spikes/spike-1/data/results/q5-sizes-computed.json --commit $(git rev-parse HEAD) && cd ..` |
| Publish the interpreted and the AOT build (Brotli and gzip) | `harness/publish.sh` |
| Storage contract tests in Chromium and Firefox | `node harness/contract.mjs` |
| Batch size tuning (S3) | `node harness/run-series.mjs --plan tuning --builds interpreted` |
| All timing and durability series (several hours) | `harness/run-all.sh` (log in `out/run-all.log`) |
| Q2 file sizes (after the empty-app series exist) | `node harness/sizes.mjs` |
| Regenerate the tables of the report | `node harness/summarise.mjs` (replaces the answers table in `spike-1-report.md` and rewrites `tables.md`; `--print` only prints) |

One page load runs one scenario: `http://localhost:5000/?scenario=snapshot&...` (5000: interpreted, 5001: AOT). The harness builds the query from the manifest (`harness/common.mjs`, `datasetQuery`). A setup run (`scenario=setup`) loads a dataset into IndexedDB and writes the snapshot at event 99,000 and the search indexes, encrypted with the test key.

## Phone run (owner, step S8, about two hours)

Needs: one mid-range Android phone with Chrome (2 to 4 years old, 4 to 6 GB RAM), USB cable, developer options and USB debugging on, a computer with `adb` (Android platform tools), .NET and Node as above. Phone: screen on, battery above 50 %, not in power-saver mode. No account, cloud or app store is involved; the phone only talks to the computer through `adb reverse`.

1. On the computer, in `spikes/spike-1`: generate the datasets and publish both builds (first table, rows 1 and 5).
2. Connect the phone, accept the debugging prompt, check with `adb devices`.
3. Make the computer's ports reachable as `localhost` on the phone (a secure context, so Web Crypto and IndexedDB work):

   ```bash
   adb reverse tcp:5000 tcp:5000   # interpreted build
   adb reverse tcp:5001 tcp:5001   # AOT build
   adb reverse tcp:5090 tcp:5090   # durability test server
   ```

4. Timing series (Q1, Q2, Q4). The harness restarts Chrome on the phone for each cold run (`am force-stop`) and drives it over Chrome remote debugging (`adb forward tcp:9222 localabstract:chrome_devtools_remote`, done by the script). Close other Chrome tabs first. Name the phone model in `--device` (model only, no names):

   ```bash
   cd harness
   node run-series.mjs --target android --device "Pixel 6a" --plan main --builds interpreted,aot --chunk 10000 --batch 1000
   node run-series.mjs --target android --device "Pixel 6a" --plan search --builds interpreted,aot --chunk 10000 --batch 1000
   ```

   The first run of each build imports the 100,000-event dataset into the phone's IndexedDB (about 170 MB on disk) and takes a few minutes. Results appear as `data/results/q1-phone-chromium-<build>.json`, `q2-…`, `q4-…`.

   If the remote-debugging driver fails (it is untested, no phone was available to the agent): open the page by hand in Chrome on the phone, for example `http://localhost:5000/?scenario=setup&…` once and then `scenario=snapshot`, `scenario=full`, `scenario=empty` (print the full links with `node -e "import('./common.mjs').then(m=>console.log(m.datasetQuery('events-100000')))"` and prefix `http://localhost:5000/?scenario=snapshot&chunk=10000&batch=1000&`). Each page shows "Save results"; it downloads a JSON file, fetched with `adb pull /sdcard/Download/` and handed to the agent.

5. Durability on the phone (Q3, 20 kills by `am force-stop`):

   ```bash
   node kill.mjs --browser android-chrome --device "Pixel 6a" --variant phone-relaxed --durability relaxed --kills 20
   ```

   Swipe-away from recents, by hand, 20 times: start the same command with `--mode manual --kills 20 --variant phone-swipe`. When the script prints "swipe Chrome away", swipe Chrome away from the recent apps; the script continues once the writes stop. The script reports any lost or half-visible write.

6. Optional, the owner's own Firefox on the desktop (Q3, 10 manual kills): run `node server.mjs --port 5090 --root ../SpikeApp/wwwroot --log ../out/q3-manual-firefox.jsonl` and open `http://localhost:5090/spike/durability.html?run=1&variant=manual&seed=7`. Kill Firefox with the task manager (`kill -9`) while it writes. Reopen it with `run=2` (then 3, and so on). Each start posts what it found; compare it with the acknowledged writes in the log, as `kill.mjs` does.
7. Commit the new result files on a branch (or hand them to the agent), then run `node harness/summarise.mjs` to update the report tables.

Results hold the device model, the Chrome version, `navigator.deviceMemory` and whether the phone was charging, and no names or accounts (ADR-049).
