# ADR-009: Merging is the union of logs; no special root

id: ADR-009 · status: Accepted

applies_to: `src/ThoughtTree.Domain/**`

- **MUST** merge by taking the union of event logs, identified by `EventId`
- **MUST NOT** merge projections or trees directly
- **MUST** treat a node without parent as a top-level node; a tree may have several
- **MUST NOT** introduce a root node, a fixed root id or any node not created by `node.created`
- **MUST** take the tree name from the title of the first top-level node, with no event of its own

verify:

```bash
dotnet test --filter "ADR=ADR-009"
```

why: One merge rule for server sync, file sync and pairing of devices that started on their own.
