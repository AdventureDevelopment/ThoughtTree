# ADR-082: Tags are items in the tree's Tags branch

id: ADR-082 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Supersedes ADR-058 once accepted (keeps its `@@` shortcut, picker, tree feature, one-tree and join rules); owner decisions 2026-11-19 (session "Tags and relation types as items", decisions 2 and 5), proposed for acceptance in a pull request review. Settles the format before release 1, while nothing is stored.

- **MUST** create a branch "Tags" as an ordinary item when a tree is created and mark it with `tree.tags-set` (`itemId`); its title is written in the app language chosen on the creating device; the latest `tree.tags-set` in the agreed order wins, as for the inbox (ADR-066)
- **MUST** treat every item inside the Tags branch as a tag, by its place, with no mark event: its title is the tag's name, its note the tag's description, its child items its nested tags
- **MUST** assign tags with `item.tagged` / `item.untagged` (`itemId`, `tagId`), where `tagId` is the tag item's id; adding the same tag twice yields one assignment; concurrent adding and removing is decided by the agreed event order, without conflict marking
- **MUST** hide the assignments of a tag while its item is deleted or outside the Tags branch, and show them again when it is restored or moved back; **MUST** hide the tags of deleted items and show them again on restore
- **MUST** let a parent tag include its nested tags when filtering, searching and counting; a chip shows only the tag that was assigned
- **MUST NOT** keep tags in an item's title or note; `@@name` typed in a title is an input shortcut that the application layer turns into an assignment when editing ends, matching a tag by its title with the search matching rules, or creating a new tag item in the Tags branch
- **MUST** offer the same assignment without typing (tag picker), as the accessible alternative (ADR-033)
- **MUST** offer to join tags with the same title (for example made offline on two devices) as one action (ADR-067): `tag.joined` (`fromTagId`, `intoTagId`), the joined tag's children moved under the target, its note appended to the target's note below a separator line, the joined tag item deleted; assignments of the joined tag then count for the target; the app never joins tags without asking
- **MUST** make tags a tree feature (`tree.feature-changed`), switched on by default; off hides chips, `@@`, the picker and the Tags branch (outline, navigation, search) and keeps all assignments
- **MUST NOT** connect trees: a tag belongs to one tree; copy and paste matches tags by title (ADR-069)
- **MUST** export tags to Obsidian as `tags:` with nested tags as paths (`a/b`), and import folder names and nested tags `#a/b` as nested tag items ([zettelkasten-claim.md](../zettelkasten-claim.md))

verify:

```bash
dotnet test --filter "ADR=ADR-082"
```

why: A tag that is an item gets a description, nesting, a place for navigation (D-150) and history without a second mechanism; its place in a marked branch makes it a tag, as the inbox is marked (owner's drivers: folders as tags, navigable tags, a tag with a description, nested tags).
