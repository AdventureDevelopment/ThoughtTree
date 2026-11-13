# ADR-023: One parent per item

id: ADR-023 · status: Accepted · kind: bet ([bet card](bets.md#adr-023))

applies_to: `src/ThoughtTree.Domain/**`

- **MUST** give every item at most one parent
- **MUST NOT** implement clones or items in several places; use links or relations instead
- **MUST** place an item at top level when a move would create a cycle

verify:

```bash
dotnet test --filter "ADR=ADR-023"
```

why: Moves, cycle checks, delete conflicts, sibling order, zoom path and export rely on one parent.
