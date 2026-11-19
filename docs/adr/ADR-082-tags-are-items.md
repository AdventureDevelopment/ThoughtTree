# ADR-082: Tags are items in the tree's Tags branch

id: ADR-082 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Supersedes ADR-058 (keeps its `@@` shortcut, picker, tree feature, one-tree and join rules) and amends ADR-066 (the tree name also skips the Tags branch), ADR-069 (tags matched by title) and ADR-019 (tags imported as items); owner decisions 2026-11-19 (session "Tags and relation types as items", decisions 2 and 5), accepted by the owner 2026-11-19. Settles the format before release 1, while stored formats are still free to change (ADR-085).

- **MUST** create a branch "Tags" as an ordinary item when a tree is created and mark it with `tree.tags-set` (`itemId`); its title is written in the app language chosen on the creating device; the latest `tree.tags-set` in the agreed order wins, as for the inbox (ADR-066); **MUST NOT** let the Tags branch be moved or deleted; **MUST** skip it when taking the tree name (ADR-066)
- **MUST** treat every item inside the Tags branch as a tag, by its place, with no mark event: its title is the tag's name, its note the tag's description, its child items its nested tags
- **MUST** assign tags with `item.tagged` / `item.untagged` (`itemId`, `tagId`), where `tagId` is the tag item's id; adding the same tag twice yields one assignment; concurrent adding and removing is decided by the agreed event order, without conflict marking
- **MUST** hide the assignments of a tag while its item is deleted or outside the Tags branch, and show them again when it is restored or moved back; **MUST** hide the tags of deleted items and show them again on restore
- **MUST** let a parent tag include its nested tags when filtering, searching and counting; a chip shows only the tag that was assigned
- **MUST NOT** keep tags in an item's title or note; `@@name` typed in a title is an input shortcut that the application layer turns into an assignment when editing ends, matching a tag by its title with the search matching rules, or creating a new tag item in the Tags branch
- **MUST** offer the same assignment without typing (tag picker), as the accessible alternative (ADR-033)
- **MUST** offer to join tags with the same title (for example made offline on two devices) as one action (ADR-067): `tag.joined` (`fromTagId`, `intoTagId`), the joined tag's children moved under the target, its note appended to the target's note below a separator line, the joined tag item deleted; assignments of the joined tag count for the target while the `tag.joined` holds, although the joined item is deleted; undoing the join (ADR-059) restores the item and its own assignments; the app never joins tags without asking
- **MUST** make tags a tree feature (`tree.feature-changed`), switched on by default; off hides chips, `@@`, the picker and the Tags branch (outline, navigation, search) and keeps all assignments
- **MUST NOT** connect trees: a tag belongs to one tree; copy and paste matches tags by title (ADR-069)
- **MUST**, when importing a tree, a JSON export or an Obsidian vault as a copy, move its tag items into the target's Tags branch in the same action as the imported content, without matching, and then offer to join tags with equal titles; when importing a tree, the importing device writes `tree.tags-set` for the target's own branch in the same action, and the imported tree's former branch becomes an ordinary item (as ADR-066 does for the inbox); an update import (ADR-078) matches tag items by their `thoughttree` id
- **MUST** export tags to Obsidian as `tags:` with nested tags as paths (`a/b`), and import nested tags `#a/b` as nested tag items; **MAY** offer to import folder names as nested tag items when folders do not build the tree, whether and by default is decided in the import-flow sketch (owner decision 2026-11-19); each tag is also written as a note in the folder of the Tags branch, carrying its description and its id ([zettelkasten-claim.md](../zettelkasten-claim.md))

verify:

```bash
dotnet test --filter "ADR=ADR-082"
```

why: A tag that is an item gets a description, nesting, a place for navigation (D-150) and history without a second mechanism; its place in a marked branch makes it a tag, as the inbox is marked (owner's drivers: folders as tags, navigable tags, a tag with a description, nested tags).
