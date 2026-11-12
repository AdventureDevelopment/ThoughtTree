# ADR-005: Tombstones instead of hard deletes

id: ADR-005 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client/**`, `src/ThoughtTree.Server*/**`

- **MUST** mark deleted nodes as deleted (tombstone) and keep their data, children and relations
- **MUST NOT** physically remove nodes or events

verify:

```bash
dotnet test --filter "ADR=ADR-005"
```

why: Deterministic merges; restore and undo stay possible.
