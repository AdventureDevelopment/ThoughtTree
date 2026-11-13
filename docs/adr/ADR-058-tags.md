# ADR-058: Tags as tree elements, typed with `@@`

id: ADR-058 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** define tags per tree as structural elements with an id (`tag.defined`), named only by `display-name.set` / `display-name.removed` (ADR-026)
- **MUST** assign tags to items with `item.tagged` / `item.untagged`, identified by item id and tag id; adding the same tag twice yields one assignment; concurrent adding and removing is decided by the agreed event order, without conflict marking (as for relations, ADR-024)
- **MUST NOT** keep tags in an item's title or note; `@@name` typed in a title is an input shortcut that the application layer turns into an assignment when editing ends, matching an existing tag by its display name with the search matching rules, or defining a new tag
- **MUST** hide the tags of deleted items and show them again on restore
- **MUST** offer the same assignment without typing (tag picker in the item's detail view), as the accessible alternative (ADR-033)
- **MUST NOT** connect trees: a tag belongs to one tree

verify:

```bash
dotnet test --filter "ADR=ADR-058"
```

why: Tags give items further dimensions besides the tree (UX decision D-14); one mechanism with relations and display names keep renaming, translation and merging consistent.

Open: two devices that define a tag with the same name offline create two tags; whether the app offers to join them (and by which event) is still to be decided.
