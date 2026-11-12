# ADR-004: Sibling order by reference

id: ADR-004 · status: Accepted

applies_to: `src/ThoughtTree.Domain/**`

- **MUST** place a node by `afterNodeId` (empty: first child, or first at top level)
- **MUST NOT** store order keys, indices or positions on nodes or in events
- **MUST** place a node first under its parent if the referenced sibling is under another parent at replay time; deleted siblings stay resolvable as tombstones

verify:

```bash
dotnet test --filter "ADR=ADR-004"
```

why: The order follows from replaying the log; no growing keys.
