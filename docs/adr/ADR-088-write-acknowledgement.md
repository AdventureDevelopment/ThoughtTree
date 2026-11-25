# ADR-088: When a local write counts as saved

id: ADR-088 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

Owner decision 2026-11-25 (spike 1 decision 8, option B), accepted by the owner 2026-11-25; test rule and why line corrected by the owner the same day after the independent review; amends no ADR.

- **MUST** treat an event as saved (shown as saved, its draft cleared, its undo step recorded) only after the transaction that stores its envelope reports `complete`
- **MUST** write events and tree access with `strict` durability where the browser offers the setting
- **MAY** write rebuildable or short-lived data (snapshots, event blocks, search index, drafts, UI state) with `relaxed` durability
- **MUST** secure this with end-to-end scenario 8: after the app is killed while writing, no saved event is lost, no batch is half visible and the store opens; push gate in Chromium, nightly in Chromium, WebKit and Firefox

verify:

```bash
dotnet test --filter "ADR=ADR-088"
```

why: No data loss is quality goal 1; only `strict` covers an OS crash or power loss, which cannot be tested. Spike 1 found 0 losses in 940 desktop kills or closes and 20 phone swipe-aways, but only 200 desktop Chromium kills ran with `strict` (the rest with the browser default); there `strict` cost about 13 % write throughput. Editing writes events at the end of an editing session, so the cost is small there, while drafts written on every keystroke stay fast; the cost for imports, which write many events at once, is not measured (open decision "Durability of imports").
