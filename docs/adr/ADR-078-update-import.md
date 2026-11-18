# ADR-078: Re-importing a readable export updates the tree it came from

id: ADR-078 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Amends ADR-019 (new ids only for items the tree does not know) and ADR-059 (quick undo and the history skip export events, which record a fact, not a change); owner decisions 2026-11-17 (Zettelkasten decision 4b) and 2026-11-18 (export events), proposed for acceptance in a pull request review.

- **MUST** write into every readable export the id of each item (Obsidian form: a `thoughttree` property in each file's frontmatter; JSON: the item id; the one-document Markdown and OPML forms carry no ids and always import as a copy) and once per export the id of its export event
- **MUST** write, for every readable export, an export event into the log: `item.exported` (`itemId`, `form`, `format`, `seen`) when exported from an item's actions, `tree.exported` (`form`, `format`, `seen`) for the whole tree; `seen` is the export point (the highest clock received per device, as on `item.deleted`), `form` is folders or flat files with an `up` property
- **MUST NOT** put device information into the exported files (ADR-019): the export point stays in the encrypted log, the files carry only the export event's id
- **MUST NOT** undo an export event with undo; it records a fact, takes part in no conflict and is shown in the history of its item or tree
- **MUST** preselect the form of the latest export event of the same item or tree for the next export
- **MUST** offer, when readable files carry ids of items of the tree they are imported into, "Update from these files" besides importing a copy
- **MUST**, on update, write ordinary events for known items (title, note, move, tags, relations, relation notes), and create items with new ids only for content without a known id (ADR-019)
- **MUST** let each update event replace only the changes known at the export point, so a change made in the app after the export becomes a visible conflict (ADR-018), never an overwrite; when the export event has not reached this device (or the files carry none), an update replaces nothing known, and the app suggests exchanging with the exporting device first
- **MUST** add or remove tags and relations only by the difference between the file and the state at the export point, so connections made in the app after the export stay
- **MUST NOT** delete items whose files are missing; list them in the import report and delete them only after the user confirms
- **MUST** write the whole update as one action (ADR-067), undone in one step
- **MUST** read the export's form from its export event, without asking again
- **MUST** import a vault that carries no ids as a copy; notes are not matched by file path or foreign properties

verify:

```bash
dotnet test --filter "ADR=ADR-078"
```

why: A round trip through other tools (Obsidian) must update the thoughts it came from without duplicates and without losing changes made meanwhile; the existing conflict mechanism does the safety work. Export events keep device information out of the unencrypted files; in a shared tree co-authors can see that and when someone exported (owner decision 2026-11-18).
