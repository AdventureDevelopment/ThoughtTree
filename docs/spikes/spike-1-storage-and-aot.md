# Spike 1 plan: local storage engine and Blazor AOT

Status: plan, proposed 2026-11-15. The owner agreed the scope of the spike (open decisions "Blazor AOT compilation and spike" and "Local storage engine" in [architecture.md](../architecture.md) section 9); this document only says how it is done. Nothing here reopens a decision. No spike code exists yet.

## 1. Purpose and questions

IndexedDB is the release 1 engine (decided). The spike measures whether it, together with encryption (ADR-073), is fast and safe enough, and whether the app needs AOT. It answers six questions:

| # | Question | Decides |
| --- | --- | --- |
| Q1 | How long do start and replay take with 100,000 events in IndexedDB, with batched Web Crypto decryption and snapshot loading, on a mid-range Android phone and on desktop? | Whether PF1 (tree visible within 1 s) holds; review trigger of the engine row |
| Q2 | The same for an AOT build and an interpreted build, and how large is each download? | AOT yes or no; first-load size budget (PF5, decision 5) |
| Q3 | Is any acknowledged write lost when the browser is closed or killed during writing, in Chromium and Firefox, over repeated kills? | Review trigger of the engine row |
| Q4 | Search: persisted encrypted index against an index built in memory at start; numbers for the optional unencrypted index | Search design; open decision "Optional unencrypted search index" |
| Q5 | Which numbers should the stored size limits have? | Proposal for the ADR on size limits |
| Q6 | What is the recommendation? | Owner decides AOT from the report |

Out of scope: Safari (no cloud accounts, open decision "Safari support"), SQLite on OPFS (revisit only through its review triggers), sync, UI.

## 2. Shared method and test data

**Throwaway code.** The spike is disposable code in `spikes/spike-1/` at the repository root, outside `src/` and `tests/`, not one of the six projects and not part of the push gate. It uses a minimal replay (dictionary of items, sibling order by reference as in ADR-004, move, delete, tag, relation) with the same data structures and complexity as the planned Domain, but not the real Domain, which does not exist yet. Spike rules: the real Domain is not referenced; the AGENTS.md "Never" rules still guide the shape (time and randomness from fakes; browser APIs behind interfaces) so measurements stay representative, but the spike is not held to the architecture tests. Timing uses `performance.now` and `performance.mark` from the browser, read through one small adapter.

**Engine-neutral storage interface (only as far as the spike needs).** Five operations: append a batch of envelopes atomically; read envelopes after a sequence number in chunks; put and get a snapshot; put and get index chunks; put and get a draft. One IndexedDB implementation. Contract tests: append then read gives the same order and bytes; a batch is all or nothing; reading chunks equals reading all; the kill test of Q3 runs against the interface. A second engine is not built. The real interface and its full contract tests belong to foundation step 4.

**Reference dataset (deterministic).** Generator `spikes/spike-1/DatasetGenerator`, one fixed seed per dataset.

| Aspect | Rule |
| --- | --- |
| Shape | 10,000 items, 100,000 events (reference dataset of architecture.md section 8); also 1,000 and 10,000 events for complexity ratios (same generator and seed) |
| Event mix | About 12 % create, 45 % edit title, 15 % edit note, 12 % move, 5 % delete and undo pairs, 6 % tag, 5 % relation; fixed in the manifest. Notes 0 to 4,000 characters, mostly short, a long tail; German and English text with accents and emoji |
| Randomness | Own seeded generator (SplitMix64) written in the spike; ids from a counter; times from a fake clock; no `Guid.NewGuid`, `Random` or system time (ADR-034) |
| Encryption | Fixed test key (32 bytes, derived from the seed label); nonce derived from the event counter (test data only, never production, ADR-073); additional authenticated data = envelope version, `KeyId`, `EventId`, account id, fixed context label; payload wrapper object, then padding to the provisional bucket list (256 B, 1 KiB, 4 KiB, 16 KiB, then multiples of 16 KiB) |
| Output | `events.jsonl` (one envelope per line, JSON as ADR-017), `manifest.json` (seed, counts, mix, bucket list, SHA-256 of the file, generator version) |
| Proof of determinism | Two runs on two machines give the same SHA-256; checked in S1 |
| Storage | The 100,000-event file is about 30 to 60 MB and is not committed; it is generated on demand. The generator, the manifest and the expected SHA-256 are committed |

Snapshots for the tests are built by the spike from the dataset (encrypted with the same test key). Three states are measured: snapshot at event 99,000 plus 1,000 tail events (normal start); no snapshot, full replay of 100,000 events (after an app update, when snapshots are discarded); empty store with first import (import of 100,000 events, PF4 scaled).

**Devices and browsers.**

| Device | Use | Provided by |
| --- | --- | --- |
| Mid-range Android phone, 2 to 4 years old, 4 to 6 GB RAM, Chrome (for example class Galaxy A5x, Pixel 6a) | Q1, Q2, Q4, Q3 (kill by `am force-stop` and swipe away) | Owner: one phone, USB cable, developer options and USB debugging on |
| Desktop, current Chromium and Firefox | All questions | Agent environment (Chromium preinstalled; Firefox via Playwright's build if the proxy allows) and the owner's own desktop for one reference run |
| Throttled desktop Chromium (CPU 4x and 6x) | Fallback only, see below | Agent |

**Reading timings on the phone without cloud accounts.** The spike page serves from a local web server on the owner's computer. `adb reverse tcp:5000 tcp:5000` makes it `http://localhost:5000` on the phone, which is a secure context, so Web Crypto, service worker and IndexedDB work. Each run writes its marks into a results object; the page shows it and offers a "Save results" button that downloads a JSON file, which `adb pull` fetches. DevTools over USB (`chrome://inspect`) is optional for tracing. No account, cloud or app store is involved. Android runs also record the Chrome version, memory class (`navigator.deviceMemory`), and whether the phone was plugged in; the owner runs them with screen on, battery above 50 %, not in power-saver mode, after one warm-up run.

**If no real phone is available.** Fallback: desktop Chromium with CPU throttling 4x and 6x through the DevTools protocol, plus a memory cap. Lost validity: storage speed, memory pressure, thermal throttling, process kills and the real Chrome-on-Android storage stack are not modelled, so Q3 on a phone is not answered and Q1 and Q2 become estimates with a stated error of roughly a factor of two. The recommendation then carries the label "phone unverified" and the owner decides AOT knowing that. An Android emulator in the cloud container is not recommended: no hardware acceleration, timings meaningless.

**Run protocol for every measurement.** One warm-up run, then at least 10 measured runs on a fresh browser profile (cold cache) plus 10 warm; report median, p95 and min/max; record order randomised by the seed. Browser, version, OS, device, build mode and git commit are stored with every result.

## 3. The six items

### Q1 Start and replay with IndexedDB

- **Method:** load the dataset into IndexedDB once (batches of 1,000 per transaction, tuned in S3), then run the cold start scenario, a fresh page load per run.
- **Measurements (marks):** page load to app ready; open database; read envelopes (chunks of 1,000, 5,000, 10,000: choose the best); Web Crypto decryption per batch (batch sizes 100, 1,000, 5,000; one `decrypt` per envelope in parallel against sequential); JSON parse and payload unwrap; replay into the projection; snapshot read, decrypt and parse; tree visible (first render of the top level); peak JS heap and WebAssembly memory; bytes stored per event; time to import 100,000 events (PF4 scaled).
- **Scenarios:** snapshot plus 1,000 tail events; full replay without snapshot; import into an empty store; 1,000 and 10,000 events for the complexity ratio.
- **Devices:** phone, desktop Chromium, desktop Firefox. Interpreted build and AOT build both (Q2).
- **Acceptance (proposed, for the owner to confirm in the report):** with snapshot, tree visible within 1 s on the phone (PF1, a design target, report-only otherwise); full replay of 100,000 events within 10 s on the phone with a progress display; time for 10,000 events versus 100,000 events at most 12 times (PF6). Misses are reported, not hidden; they trigger the engine review in the table.
- **Changes in the architecture if:** the snapshot path is fast but full replay is slow, snapshots become more frequent and rebuilding after updates is done in the background (Persistence section); both slow, the engine row is reviewed (SQLite on OPFS) and the decision goes to the owner; decryption dominates, the batch size becomes a stored tuning value in Client.

### Q2 AOT against interpreted

- **Method:** the same spike app published twice (`PublishTrimmed`, interpreted; `RunAOTCompilation`, AOT), both with Brotli precompression; same dataset, same scenarios as Q1.
- **Measurements:** transferred size of the first load (Brotli and gzip, exact bytes of all files including `dotnet.native.wasm` and the assembly bundle); size on disk after trimming; time from navigation to app ready (cold, warm, service worker cache); Q1 numbers for each build; AOT build time; memory of each build; start of an empty app as the baseline of the framework.
- **Acceptance:** report only. The report proposes the budgets: first-load budget = measured transfer size of the chosen build plus about 10 % headroom, and a load budget (time to app ready, cold, on the phone). The owner decides.
- **Changes if:** AOT saves more than about a factor of 2 on full replay for a size growth the owner accepts, AOT is chosen and the budget in decision 5 is set from it; interpreted is fast enough, the smaller download wins and AOT stays a documented option with the numbers. A mixed form (AOT only for the replay assembly) is noted if the tooling allows, not built.
- Note: the spike app is a stand-in. Real size depends on the finished app; the budget is therefore set with headroom and re-checked by the size gate.

### Q3 Durability in Chromium and Firefox

- **Method:** a writer in the page appends batches of events (sizes 1, 10, 100) in a loop and reports every acknowledged write (transaction `complete` event) to an external log file through a local test server, before the next write. A supervisor kills the browser process tree with SIGKILL at random moments (seeded) while writing, then restarts the same profile and compares the store with the log.
- **Variants:** Chromium with transaction durability `relaxed` (default) and `strict`; Firefox default; kill during a write, kill during a snapshot write, kill during a draft write, orderly close (tab close, browser close), kill during database open after a previous kill. On the phone (if available): `adb shell am force-stop`, swipe away from recents, 20 repeats each.
- **Measurements:** per kill: acknowledged events, events found, events missing, events found beyond the log (allowed, in flight), database opens without error, corruption (unreadable store), recovery time.
- **Repeats:** at least 200 kills per browser and variant on desktop, 20 on the phone.
- **Acceptance (hard):** no acknowledged event is ever lost or corrupted, the database always opens, a partially written batch is never half visible. Any loss is a finding that triggers the engine review. `relaxed` durability that loses acknowledged data decides `strict` (with its cost measured in Q1) or a different acknowledgement rule.
- **Changes if:** loss in one browser only, an acknowledgement rule "confirm after strict commit" is written into the storage interface contract; loss in both, the engine row is reviewed and SQLite on OPFS is measured in a follow-up spike.
- Note: Playwright's Firefox is a patched build; the owner's own Firefox is checked in a short manual run of the same page (10 kills by hand).

### Q4 Search index

| Variant | Description |
| --- | --- |
| A | Index built in memory from the projection at every start (no persisted index) |
| B | Persisted index, encrypted with the tree key in chunks, loaded and decrypted at start, updated incrementally by events (ADR-073) |
| C | Persisted index unencrypted (numbers only, for the optional device setting) |

- **Index:** inverted index of titles and notes with the matching rules of the Search section of architecture.md (case, accents, German equivalents, Markdown markers ignored); the same code builds all three variants.
- **Measurements, at 10,000 items:** build time from the projection; index size (memory and stored); read, decrypt and parse time for B and C; start to first hits (PF3: within 200 ms); query latency for 1-, 2- and 3-word queries and a prefix query (median and p95); incremental update cost per event; rebuild time after an app update; effect on start time of Q1; memory.
- **Devices:** as Q1, interpreted and AOT.
- **Acceptance:** PF3 for the chosen variant on the phone; the rest report only. Variant C is reported but never chosen by the spike; the decision on the setting stays open and uses these numbers.
- **Changes if:** A meets PF3 and adds under about 1 s to start, no persisted index is built in release 1 (simpler: the "search index" store stays unused); B is clearly faster and costs less than A, it becomes the design; if only C meets PF3, the owner gets the numbers for the unencrypted setting and its privacy text.

### Q5 Proposal for the stored size limits

Rules are fixed (open decision "Size limits"); the spike proposes numbers only. Measured on the reference dataset and variants: stored bytes and overhead for several bucket lists (for example 256 B to 16 KiB in factors of 4, in factors of 2, 5 buckets against 8) and the above-largest rule; replay and decryption time with maximum-length notes (1,000 events with notes at 10 KB, 50 KB, 200 KB, 1 MB); memory per item at those lengths; time to save and load such a note; the pairing code size against the QR capacity named in ADR-028 for several name and address lengths. Output: one table of proposed numbers with the measurement behind each (bucket list, maximum title, maximum note, maximum user name and address length, font background budget left to the owner) for the owner. Report only; the numbers become an ADR through the owner (AGENTS.md section 2).

### Q6 Report

A short report `docs/spikes/spike-1-report.md` (at most about 150 lines): answers Q1 to Q5 in a table, the recommendation (AOT or not, budgets, snapshot policy, search variant, durability rule, size numbers), what was not measured and why, validity limits (phone or fallback), and the change list for `docs/architecture.md` and ADR proposals (status `Proposed`). The owner decides AOT from it.

## 4. How results are stored (reproducible data)

| What | Where | Rule |
| --- | --- | --- |
| Generator, spike code, run scripts | `spikes/spike-1/` | Disposable; removed or archived after the report; not part of `src/` or the gate |
| Dataset manifest and SHA-256 | `docs/spikes/spike-1/data/manifest.json` | Committed; the 100,000-event file itself is generated on demand |
| Measurements | `docs/spikes/spike-1/data/results/<question>-<device>-<browser>-<build>.json` | One JSON file per series: raw samples, median and p95, browser, version, OS, device, build mode, git commit, seed |
| Reproduction | `docs/spikes/spike-1/README.md` | Exact commands: generate, serve, run, summarise; the same commands regenerate the tables of the report |
| Report | `docs/spikes/spike-1-report.md` | Tables are generated from the result files, not typed by hand |

The data is deliberately not placed in `tests/data/formats` or `tests/data/regression-seeds` (append-only, ADR-035), and the spike writes nothing under `tests/specify`, the reference model or any `*.verified.*` file. `docs/**` needs the owner's review (ADR-035), which suits a report that the owner decides from. No telemetry or automatic reports (ADR-051); the files contain device model and browser versions of the owner's own test devices, no user content.

## 5. Order of work

| Step | Task | Effort | By |
| --- | --- | --- | --- |
| S1 | Seeded generator, encrypted dataset, manifest, determinism check | 1 day | Agent |
| S2 | Storage interface (five operations), IndexedDB implementation, contract tests | 1 day | Agent |
| S3 | Replay, snapshot, decrypt batches, marks, results adapter; tuning of batch sizes | 1.5 days | Agent |
| S4 | Run scripts and desktop series for Q1 in Chromium and Firefox (Playwright, throttled fallback) | 1 day | Agent |
| S5 | Interpreted and AOT builds, size measurement, Q2 desktop series | 1 day | Agent |
| S6 | Kill harness and Q3 desktop series (both browsers) | 1.5 days | Agent |
| S7 | Search index variants A, B, C and Q4 desktop series | 1.5 days | Agent |
| S8 | Phone runs: setup, Q1, Q2, Q4, Q3 on Android | 0.5 day owner time plus 0.5 day agent | Owner and agent |
| S9 | Size limit measurements and proposal table (Q5) | 1 day | Agent |
| S10 | Result summary, report, architecture change list, ADR proposals | 1 day | Agent, owner reads |
| S11 | Owner decision on AOT and budgets; docs and ADR changes follow as separate pull requests | owner | Owner |

About 11 agent days in small steps (S1 to S7 and S9 are independent after S3 and can be done one by one), owner effort: provide the phone, run S8 with the agent's script (about two hours, and 10 manual Firefox kills), read the report. Steps S1 to S7, S9 and S10 need no owner; S8 and S11 do.

**What the owner must provide:** one mid-range Android phone with Chrome, a USB cable, a computer to which it connects (or the agent's instructions for the owner's desktop), developer options with USB debugging, and about two hours; optionally the owner's own Firefox for the manual kill run. Accounts: none. If there is no phone, the fallback in section 2 applies and the report says so.

## 6. Risks

| Risk | Effect | Handling |
| --- | --- | --- |
| The throwaway replay is simpler than the real Domain | Real replay may be slower | State the factor in the report; re-run the two key scenarios on the real Domain once it exists (step 2a), as a small follow-up task |
| No phone | Q1 to Q3 on a phone unanswered | Fallback and label "phone unverified" (section 2) |
| Timings noisy (thermal, background load) | Wrong conclusions | Warm-up, 10+ runs, median and p95, record conditions; absolute numbers stay design targets (section 8 of architecture.md) |
| Playwright Firefox differs from release Firefox | Durability result may not transfer | Manual kill run in the owner's Firefox |
| Kill harness cannot reproduce a power loss | Durability is only tested for process kills | State this in the report; power loss is not testable here |
| Dataset size makes the repository heavy | Slow checkout | Generated on demand, only manifest committed |
| Provisional bucket list changes sizes | Q1 and Q5 numbers shift | Q5 measures several lists; Q1 repeated for the chosen list if it differs |

## 7. Done when

All of Q1 to Q5 have result files and a table in the report, the phone limitation is stated, the recommendation is written, and the owner has decided AOT from the report.
