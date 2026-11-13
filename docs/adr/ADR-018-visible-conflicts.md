# ADR-018: Conflicts are visible and resolved by the user

id: ADR-018 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** let exactly these events name the changes they replace (`replaces`): `item.title-changed`, `item.note-changed`, `item.moved`, `relation-type.defined`, `label.set`, `tree.feature-changed`, `tree.server-changed`
- **MUST NOT** treat concurrent changes with the same value as a conflict
- **MUST** list all kinds of conflicts (items, relation types, labels, tree features, server address) in the Conflict View
- **MUST** let `item.deleted` carry `seen`, the highest clock received per device
- **MUST** derive conflicts from the log; MUST NOT store them
- **MUST** resolve a conflict by an ordinary event that replaces all competing changes

verify:

```bash
dotnet test --filter "ADR=ADR-018"
```

why: Last-writer-wins can still pick the wrong value; the user sees and fixes it.
