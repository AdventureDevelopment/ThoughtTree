# ADR-088: When a local write counts as saved

id: ADR-088 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

Owner decision 2026-11-25 (spike 1 decision 8, option B), accepted by the owner 2026-11-25; amends no ADR.

- **MUST** treat an event as saved (shown as saved, its draft cleared, its undo step recorded) only after the transaction that stores its envelope reports `complete`
- **MUST** write events and tree access with `strict` durability where the browser offers the setting
- **MAY** write rebuildable or short-lived data (snapshots, event blocks, search index, drafts, UI state) with `relaxed` durability
- **MUST** keep the durability tests: no saved event is lost, no batch is half visible and the store opens after the app is killed while writing, in Chromium and Firefox

verify:

```bash
dotnet test --filter "ADR=ADR-088"
```

why: No data loss is quality goal 1; spike 1 found 0 losses in 940 kills or closes and 20 phone swipe-aways with both settings, `strict` cost about 13 % write throughput, and only `strict` covers an OS crash or power loss, which cannot be tested; events are written at the end of an editing session, so the cost is small where it matters, while drafts written on every keystroke stay fast.
