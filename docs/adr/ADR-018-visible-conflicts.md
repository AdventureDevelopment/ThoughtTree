# ADR-018: Conflicts are visible and resolved by the user

id: ADR-018 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client/**`

- **MUST** let changes of title, note, position, server address and relation types name the changes they replace (`replaces`)
- **MUST** let `node.deleted` carry `seen`, the highest clock received per device
- **MUST** derive conflicts from the log; MUST NOT store them
- **MUST** resolve a conflict by an ordinary event that replaces all competing changes

verify:

```bash
dotnet test --filter "ADR=ADR-018"
```

why: Last-writer-wins can still pick the wrong value; the user sees and fixes it.
