# ADR-084: Relation types are items; a new tree has only `related`

id: ADR-084 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Supersedes ADR-025 (with its bet card), ADR-026 and ADR-081 once accepted, and amends ADR-018 (`relation-type.set` replaces `relation-type.defined` and `display-name.set` among the events that carry `replaces`); owner decisions 2026-11-19 (session "Tags and relation types as items", decisions 3, 4 and 5), proposed for acceptance in a pull request review. Settles the format before release 1, while nothing is stored.

- **MUST** create a branch "Relation types" as an ordinary item when a tree is created and mark it with `tree.relation-types-set` (`itemId`); the latest marking in the agreed order wins
- **MUST** treat every item inside that branch as a relation type, by its place: its title is the type's name, its note says what the type means; `relation-type.set` (`itemId`, `symmetric`, `reverseName`, `replaces`) keeps whether the type is symmetric and its reverse name; a type item without it is directed and has no reverse name
- **MUST** reference a type by its item id, never by its name; **MUST** hide the relations of a type while its item is deleted or outside the branch, and show them again when it comes back
- **MUST** create in a new tree exactly one type, `related` (symmetric), as an item with a fixed id that is the same in every tree; it is the type of a new relation (D-13); an import or paste maps it onto the target's `related` instead of creating another
- **MUST NOT** build other relation types into the app or into new trees; the tutorial offers further types as ordinary items to copy (new ids, matched by title, ADR-069); duplicate types after an import are allowed
- **MUST** write the titles of the items made at tree creation ("Tags", "Relation types", "related", the inbox) in the app language chosen on the creating device; they are content and are not translated afterwards (ADR-071)
- **MUST NOT** use display names: no structural element with a name of its own is left (`display-name.set` / `display-name.removed` are not events of the format)
- **MUST** export every type to Obsidian by its title, `related` included, and import a type key by title, defining a new type item for an unknown key ([zettelkasten-claim.md](../zettelkasten-claim.md))
- **MUST** make relations a tree feature; off hides relations, the map's relations and the Relation types branch, and keeps them

verify:

```bash
dotnet test --filter "ADR=ADR-084"
```

why: A type that is an item gets a note saying what it means, history and copying by copy and paste, with the same mechanism as tags; one predefined type keeps D-13 without filling new trees, and names written as content need no translation mechanism.
