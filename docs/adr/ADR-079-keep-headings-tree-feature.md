# ADR-079: Headings become thoughts is a tree feature

id: ADR-079 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

Amends ADR-072 (headings in notes), ADR-029 (notes without headings) and ADR-019 (reading headings as items on import); owner decision 2026-11-17 (Zettelkasten session), accepted by the owner 2026-11-19.

- **MUST** make "Headings become thoughts" a tree feature (`tree.feature-changed`), switched on by default, so all devices and users of a tree write the same way
- **MUST**, while it is on, behave as ADR-072 says, except that "Keep as one note" when pasting or importing keeps the headings as headings instead of bold lines
- **MUST**, while it is off, keep headings typed, pasted or imported in the note, without asking, and store them with `format: markdown`
- **MUST** keep "Turn headings into thoughts" (D-153) available for a single item's note whatever the feature's state
- **MUST NOT** change the projection or any stored event when the feature changes; it only changes what the editor and the import do (section 9, tree features)
- **SHOULD** offer to switch the feature off when an Obsidian vault is imported as a new tree
- **MUST** keep relation notes without headings (ADR-077)

verify:

```bash
dotnet test --filter "ADR=ADR-079"
```

why: Zettelkasten notes often keep their headings (and `[[note#heading]]` links point at them); forcing them into thoughts or bold lines would break the round trip with Obsidian. The shape of a tree's content belongs to the tree, so the choice is shared, not per device.
