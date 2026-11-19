# ADR-084: Relation types are items; a new tree has only `related`

id: ADR-084 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Supersedes ADR-025 (with its bet card), ADR-026 and ADR-081 once accepted, and amends ADR-018 (`relation-type.set` replaces `relation-type.defined` and `display-name.set` among the events that carry `replaces`; display names leave the conflict kinds), ADR-019 (no type is reused by a fixed id; no display names), ADR-066 (the tree name also skips the Relation types branch) and ADR-069 (types matched by title); the ADR-025 bet card is retired on acceptance; owner decisions 2026-11-19 (session "Tags and relation types as items", decisions 3, 4 and 5), proposed for acceptance in a pull request review. Settles the format before release 1, while stored formats are still free to change (ADR-085, Proposed).

- **MUST** create a branch "Relation types" as an ordinary item when a tree is created and mark it with `tree.relation-types-set` (`itemId`); the latest marking in the agreed order wins; **MUST NOT** let the branch be moved or deleted; **MUST** skip it when taking the tree name (ADR-066)
- **MUST** treat every item inside that branch as a relation type, by its place: its title is the type's name, its note says what the type means; `relation-type.set` (`itemId`, `symmetric`, `reverseName`, `replaces`) keeps whether the type is symmetric and its reverse name; a type item without it is directed and has no reverse name
- **MUST** reference a type by its item id, never by its name; **MUST** hide the relations of a type while its item is deleted or outside the branch, and show them again when it comes back
- **MUST** create in a new tree exactly one type, `related` (symmetric), as an ordinary type item with a new id, and mark it as the tree's default relation type by `tree.default-relation-type-set` (`itemId`); a new relation gets the default type (D-13); the latest marking in the agreed order wins; when importing a tree, the importing device writes the marking for the target's default in the same action; the user can make another type the default; **MUST NOT** give any item a fixed id
- **MUST NOT** build other relation types into the app or into new trees; the tutorial offers further types as ordinary items to copy (new ids, matched by title, ADR-069); duplicate types after an import are allowed; an import as a copy moves the imported type items into the target's Relation types branch in the same action, without matching, `related` included; when importing a tree, the importing device writes `tree.relation-types-set` for the target's own branch in the same action, and the imported tree's former branch becomes an ordinary item; an update import (ADR-078) matches type items by their `thoughttree` id
- **MUST** write the titles of the items made at tree creation ("Tags", "Relation types", "related", the inbox) in the app language chosen on the creating device; they are content like any title and are never translated by the app
- **MUST NOT** use display names: no structural element with a name of its own is left (`display-name.set` / `display-name.removed` are not events of the format)
- **MUST** export every type to Obsidian by its title, `related` included, and also as a note in the folder of the Relation types branch with its meaning, its id, `thoughttree-symmetric` and `thoughttree-reverse`; on import a type key resolves to the type note by its id when the files carry one, else by title, defining a new type item for an unknown key ([zettelkasten-claim.md](../zettelkasten-claim.md))
- **MUST** make relations a tree feature (`tree.feature-changed`), switched on by default; off hides relations, the map's relations and the Relation types branch, and keeps them

verify:

```bash
dotnet test --filter "ADR=ADR-084"
```

why: A type that is an item gets a note saying what it means, history and copying by copy and paste, with the same mechanism as tags; one predefined type keeps D-13 without filling new trees, and names written as content need no translation mechanism.
