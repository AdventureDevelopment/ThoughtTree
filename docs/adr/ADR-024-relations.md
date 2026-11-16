# ADR-024: Relations separate from the tree

id: ADR-024 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`

- **MUST** model relations with `relation.added` / `relation.removed`, identified by from item, type id and to item
- **MUST NOT** let relations change where an item sits
- **MUST NOT** connect trees: relations only point to items of the same tree; an `item:` text link whose target is in another tree (for example after copy and paste, ADR-069) stays as text and is shown as not found, never as a working link
- **MUST** store the item ids of symmetric relations in a fixed order
- **MUST** hide relations of deleted items and show them again on restore

verify:

```bash
dotnet test --filter "ADR=ADR-024"
```

why: Any number of typed connections without touching the single-parent tree.
