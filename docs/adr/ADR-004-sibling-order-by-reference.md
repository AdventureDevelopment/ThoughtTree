# ADR-004: Sibling order by reference

id: ADR-004 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`

Rules 4 and 5 added by owner decision 2026-11-26.

- **MUST** place an item by `afterItemId` (empty: first child, or first at top level)
- **MUST NOT** store order keys, indices or positions on items or in events
- **MUST** place an item first under its parent if the referenced sibling is under another parent at replay time; deleted siblings stay resolvable as tombstones
- **MUST NOT** apply a move whose parent or `afterItemId` does not exist at that point of the replay, or whose `afterItemId` is the item itself; the item stays where it was and the move is reported
- **MUST** place a new item whose parent does not exist at that point of the replay at top level, and one whose `afterItemId` does not exist or is the item itself first under its parent; both are reported

verify:

```bash
dotnet test --filter "ADR=ADR-004"
```

why: The order follows from replaying the log; no growing keys.
