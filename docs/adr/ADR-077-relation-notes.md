# ADR-077: A note on a relation

id: ADR-077 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Amends ADR-018 (adds `relation.note-changed` to the events that carry `replaces`, and relation notes to the kinds of conflicts in the Conflict View); owner decision 2026-11-17 (Zettelkasten decision 3), proposed for acceptance in a pull request review.

- **MUST** store the note of a relation (why two items are connected) only in `relation.note-changed` with `fromItemId`, `typeId`, `toItemId`, `note`, `format`, `replaces`; the full text in each event, one event per editing session
- **MUST NOT** put a note on `relation.added`, and **MUST NOT** use `item.note-changed` for it
- **MUST** key the note by the relation's three parts, with the item ids of symmetric types in the relation's fixed order (ADR-024); an empty note removes it
- **MUST** hide the note while its relation is removed, its type removed or one of its items deleted, and show it again when the relation comes back
- **MUST** treat concurrent changes of the same note as a visible conflict, resolved like item notes (ADR-018); undo writes a new event naming the one it reverts (ADR-059)
- **MUST** apply the text rules of item notes: Markdown without headings (ADR-072), normalised (ADR-029), rendered as untrusted content (ADR-052), found by the on-device search
- **MUST** carry relation notes in the readable JSON export and import (ADR-019); their Markdown form is fixed with the Zettelkasten export ([zettelkasten-claim.md](../zettelkasten-claim.md))

verify:

```bash
dotnet test --filter "ADR=ADR-077"
```

why: The reason for a connection is often the actual thought (Zettelkasten practice: "builds on X because …"); a separate event built like item notes keeps conflicts, undo, history and older apps (which skip unknown events, ADR-016) working without a special case.
