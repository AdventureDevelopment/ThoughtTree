# ADR-010: Multiple independent trees per device

id: ADR-010 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** partition all local data by tree id: events, snapshots, tree access, device token, search index, settings
- **MUST NOT** share keys, events or indexes between trees; the only exception is importing a whole tree into another as a new branch, which adds its events to the target tree's log, re-encrypted with the target's key, followed by events that keep the target's name, theme and inbox

verify:

```bash
dotnet test --filter "ADR=ADR-010"
```

why: Separate trees and sharing one tree without exposing the others.
