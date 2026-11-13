# ADR-062: Views belong to items

id: ADR-062 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** let every item, and the tree itself, carry an ordered list of views; without one, the item has only the outline view
- **MUST** define a view by events (`view.set` with the owner (an item or the tree), a view id, its name, its query, its layout and `replaces`; `view.removed`), so views are shared like content
- **MUST** store the query as structured data with its own format version (words, tags, "changed recently", later filters), never as code; the scope is the owner's branch, or the whole tree for views of the tree
- **MUST** reference layouts by fixed ids shipped with the app (outline now; board, columns and others later); **MUST** fall back to the outline for an unknown layout
- **MUST** compute the results on the device from the projection and the search index; **MUST NOT** store results in the log
- **MUST** offer at the tree level a view that lists all views of the tree, with the path of their items
- **MUST** hide the views of a deleted item and show them again on restore
- **MUST** let concurrent changes of the same view follow ADR-018 (last writer wins, conflict listed); on acceptance, `view.set` is added to the events of ADR-018 that carry `replaces`

verify:

```bash
dotnet test --filter "ADR=ADR-062"
```

why: A project carries its own perspectives (saved searches now, layouts later) like its theme, without extra nodes in the tree (UX decisions D-38, D-39).
