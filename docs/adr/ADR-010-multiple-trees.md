# ADR-010: Multiple independent trees per device

id: ADR-010 · status: Accepted

applies_to: `src/ThoughtTree.Client/**`

- **MUST** partition all local data by tree id: events, snapshots, tree access, device token, search index, settings
- **MUST NOT** share keys, events or indexes between trees

verify:

```bash
dotnet test --filter "ADR=ADR-010"
```

why: Separate trees and sharing one tree without exposing the others.
