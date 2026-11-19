# ADR-083: Relations have an id

id: ADR-083 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Amends ADR-024 (identity by from item, type and to item), ADR-077 (the note is keyed by the relation's id) and ADR-018 (adds `relation.type-changed` to the events that carry `replaces`); owner decision 2026-11-19 (session "Tags and relation types as items", decision 3a), accepted by the owner 2026-11-19. Settles the format before release 1, while stored formats are still free to change (ADR-085): an id cannot be added cleanly to relations stored by release 1.

- **MUST** give every relation an id from the replaceable id generator (ADR-034): `relation.added` (`relationId`, `fromItemId`, `typeId`, `toItemId`)
- **MUST** change a relation's type only by `relation.type-changed` (`relationId`, `typeId`, `replaces`); concurrent changes of the type are a visible conflict (ADR-018)
- **MUST** remove and restore a relation by `relation.removed` / `relation.restored` (`relationId`); undo restores the same relation (ADR-059); removing a relation shown as one (see below) removes every relation id it stands for, in one action (ADR-067)
- **MUST** key the note of a relation by its id: `relation.note-changed` (`relationId`, `note`, `format`, `replaces`), so the note and its history stay when the type changes
- **MUST** show relations with equal from item, type and to item (for symmetric types in either order) as one; if more than one of them carries a note, the notes are shown as a visible conflict
- **MUST NOT** make relations items: a relation has no place in the tree (ADR-023, ADR-024)
- **MUST** give copies new relation ids when copying, pasting or creating from a template (ADR-068, ADR-069); the Obsidian files carry no relation ids, so the update import (ADR-078) matches relations by from item, type and to item

verify:

```bash
dotnet test --filter "ADR=ADR-083"
```

why: A reassignable type with its note and history needs an identity apart from the type; duplicates made offline are rare and are shown as one.
