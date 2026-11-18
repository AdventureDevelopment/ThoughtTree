# ADR-078: Re-importing a readable export updates the tree it came from

id: ADR-078 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Amends ADR-019 (new ids only for items the tree does not know); owner decision 2026-11-17 (Zettelkasten decision 4b), proposed for acceptance in a pull request review.

- **MUST** write into every readable export the id of each item (Markdown: a `thoughttree` property in the file's frontmatter; JSON: the item id) and once per export the export point: the tree's id, the highest clock received per device at the time of export, and the form of the export (folders or parent property)
- **MUST** offer, when readable files carry ids of items of the tree they are imported into, "Update from these files" besides importing a copy
- **MUST**, on update, write ordinary events for known items (title, note, move, tags, relations, relation notes), and create items with new ids only for content without a known id (ADR-019)
- **MUST** let each update event replace only the changes known at the export point, so a change made in the app after the export becomes a visible conflict (ADR-018), never an overwrite; without an export point an update replaces nothing known
- **MUST** add or remove tags and relations only by the difference between the file and the state at the export point, so connections made in the app after the export stay
- **MUST NOT** delete items whose files are missing; list them in the import report and delete them only after the user confirms
- **MUST** write the whole update as one action (ADR-067), undone in one step
- **MUST** read the export's form from the export point, without asking again; **MUST NOT** record an export as an event (the device remembers the last form per branch as device state)
- **MUST** import a vault that carries no ids as a copy; notes are not matched by file path or foreign properties

verify:

```bash
dotnet test --filter "ADR=ADR-078"
```

why: A round trip through other tools (Obsidian) must update the thoughts it came from without duplicates and without losing changes made meanwhile; the existing conflict mechanism does the safety work.
