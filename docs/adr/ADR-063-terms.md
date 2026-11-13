# ADR-063: Terms: note, node, text, branch

id: ADR-063 · status: Proposed · kind: constraint

applies_to: `docs/**`, `src/**`, `tests/**`

- **MUST** use these terms in documents, code, tests and the test id catalogue:
  - **Note:** the thing the user writes and works with: title, text, tags, and optionally theme and views
  - **Node:** the graphical representation of a note: its line in the outline and its place in the tree
  - **Text:** the optional longer content of a note (replaces the glossary's current meaning of "note")
  - **Branch:** a note together with everything below it
- **MUST** update the glossary of `architecture.md` (Node, Note, Title, Top-level Node, Zoom, Tree Name) on acceptance
- **SHOULD** rename event types and fields to match before the first release, while nothing is stored yet (for example `node.created` → `note.created`, field `note` → `text`, `node.note-changed` → `note.text-changed`); the architect decides the final names

verify:

```bash
grep -rniE '\bnode\.note-changed\b' src tests | wc -l   # expect 0 once renamed
```

why: The owner thinks of a note as the whole thing and of a node as how it is shown; documents, UI specifications and code should speak the owner's language (UX decision D-44).
