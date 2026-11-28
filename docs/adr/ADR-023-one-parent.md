# ADR-023: One parent per item

id: ADR-023 · status: Accepted · kind: bet ([bet card](bets.md#adr-023))

applies_to: `src/ThoughtTree.Domain/**`

Rule 3 changed by owner decision 2026-11-26 (was: place the item at top level).

- **MUST** give every item at most one parent
- **MUST NOT** implement clones or items in several places; use links or relations instead
- **MUST NOT** apply a move that would put an item under itself or one of its descendants; the item stays where it was and the move is reported

verify:

```bash
dotnet test --filter "ADR=ADR-023"
```

why: Moves, cycle checks, delete conflicts, sibling order, focus path and export rely on one parent.
