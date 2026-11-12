# ADR-016: Downward-compatible event versioning

id: ADR-016 · status: Accepted

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`

- **MUST NOT** change or rewrite a stored event; only its envelope may be re-encrypted
- **MUST** only add fields to existing events; MUST NOT remove a field or change its meaning
- **MUST** express structure only through the basic events (`node.created`, `node.moved`, `node.deleted`, `node.restored`, `node.title-changed`, `node.note-changed`)
- **MUST** put new kinds of information into new event types
- **MUST** treat a missing field as "unchanged", never as "reset to default"
- **MUST** store, sync and skip unknown events, keep the tree editable and show a warning
- **MUST** rebuild snapshots and search index when the app version changes

verify:

```bash
dotnet test --filter "ADR=ADR-016"
git diff --diff-filter=DM origin/main -- tests/data/formats | wc -l   # expect 0
```

why: Devices run different app versions; older devices must never miss nodes or texts.
