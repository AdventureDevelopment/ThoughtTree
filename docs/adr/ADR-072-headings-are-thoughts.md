# ADR-072: A heading is a thought; Focus shows a thought as a page

id: ADR-072 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

Proposed by the UX concept (decisions D-151, D-153, D-155). Touches section 8 (text: "notes are full Markdown") and ADR-065 (Focus).

- **MUST** show, when focused on an item, its title, tags, note and its branch on one page; there is no separate detail page
- **MUST** treat a heading entered in a note as a new item: on confirming the heading line, create an item inside the focused item (first position), with the heading text as its title and the text that followed the heading as its note, as one action (ADR-067); only existing events are used (`item.created`, `item.note-changed`)
- **MUST NOT** store Markdown headings in notes written in the app; all other Markdown of notes stays as it is
- **MUST** ask before converting when text containing headings is pasted or imported into a note: turn the headings into items (deeper headings become items inside them; text before the first heading stays the note), or keep one note with the heading lines kept as bold text
- **MUST** keep notes that already contain headings readable (events are immutable): they are rendered, and the user is offered the same conversion
- **SHOULD** write items as headings in the readable Markdown export and read headings as items in the readable import (ADR-019), so a document and its tree round-trip
- **MUST** write levels deeper than Markdown's six heading levels as nested lists in the readable export, and read them back as items, so deep branches round-trip too

verify:

```bash
dotnet test --filter "ADR=ADR-072"
```

why: The structure of a text is the tree. A thought that grows headings grows into a branch without a separate step, and the page reads like a document (the owner's model: Word's outline view).
