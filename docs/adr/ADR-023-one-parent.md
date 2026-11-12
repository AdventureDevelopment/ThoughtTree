# ADR-023: One parent per node

id: ADR-023 · status: Accepted · kind: bet

applies_to: `src/ThoughtTree.Domain/**`

- **MUST** give every node at most one parent
- **MUST NOT** implement clones or nodes in several places; use links or relations instead
- **MUST** place a node at top level when a move would create a cycle

verify:

```bash
dotnet test --filter "ADR=ADR-023"
```

why: Moves, cycle checks, delete conflicts, sibling order, zoom path and export rely on one parent.

outcome: (to be filled)

assumption: (to be filled)

signal: (to be filled)

review: (to be filled)
