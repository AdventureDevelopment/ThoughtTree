# ADR-024: Relations separate from the tree

id: ADR-024 · status: Accepted

applies_to: `src/ThoughtTree.Domain/**`

- **MUST** model relations with `relation.added` / `relation.removed`, identified by from node, type id and to node
- **MUST NOT** let relations change where a node sits
- **MUST** store the node ids of symmetric relations in a fixed order
- **MUST** hide relations of deleted nodes and show them again on restore

verify:

```bash
dotnet test --filter "ADR=ADR-024"
```

why: Any number of typed connections without touching the single-parent tree.
