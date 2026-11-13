# ADR-062: Saved searches are nodes of the tree

id: ADR-062 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** store a saved search as an ordinary node whose query is set by an event (`node.search-set` with `nodeId`, the query and `replaces`); it is placed, moved, themed, deleted and restored like any node
- **MUST** store the query as structured data with its own format version (words, tags, "changed recently" and later filters), never as code
- **MUST** compute the results on the device from the projection and the search index; **MUST NOT** store results in the log
- **MUST** show the results of a saved search like a search: the tree filtered in place (UX decision D-12)
- **MUST** let concurrent changes of the same query follow ADR-018 (last writer wins, conflict listed); on acceptance, `node.search-set` is added to the events of ADR-018 that carry `replaces`

verify:

```bash
dotnet test --filter "ADR=ADR-062"
```

why: Searches become places the owner can put anywhere in the tree, theme and open as start view (UX decision D-38); as nodes they need no second structure next to the tree.

Open: whether a saved search node can also have children of its own.
