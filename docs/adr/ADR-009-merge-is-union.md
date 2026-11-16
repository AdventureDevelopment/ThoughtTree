# ADR-009: Merging is the union of logs; no special root

id: ADR-009 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`

- **MUST** merge by taking the union of event logs, identified by `EventId`
- **MUST NOT** merge projections or trees directly
- **MUST** treat an item without parent as a top-level item; a tree may have several
- **MUST NOT** introduce a root item, a fixed root id or any item not created by `item.created`
- **MUST** take the tree name from the title of the first visible top-level item other than the inbox (ADR-066), with no event of its own

verify:

```bash
dotnet test --filter "ADR=ADR-009"
```

why: One merge rule for server sync and file sync of devices that hold the same tree; trees of devices that started on their own are brought together only by importing a tree into another (ADR-010).
