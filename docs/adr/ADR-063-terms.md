# ADR-063: Terms: item, note, branch, node

id: ADR-063 · status: Proposed · kind: constraint

applies_to: `docs/**`, `src/**`, `tests/**`

- **MUST** use these terms in documents, code, tests and the test id catalogue:
  - **Item:** the thing in the tree: a title, a note, tags, and optionally a theme and views (later pictures and files); what it is (thought, idea, project, topic, vision) is the user's to decide, and an item can grow
  - **Note:** the optional longer text of an item (unchanged from today's glossary)
  - **Branch:** an item together with everything below it
  - **Node:** how an item appears in the tree, its line and place; a technical term for rendering and specifications
- **MUST** update the glossary of `architecture.md` on acceptance: "Node" as the thing becomes "Item" (also in Top-level Node, Zoom, Tree Name), "Branch" is added
- **SHOULD** rename event types and fields to match before the first release, while nothing is stored yet (for example `node.created` → `item.created`, `node.note-changed` → `item.note-changed`); the architect decides the final names

verify:

```bash
grep -rnE '"node\.(created|moved|deleted|title-changed|note-changed)"' src tests | wc -l   # expect 0 once renamed
```

why: Items grow from a single thought into projects or visions; a neutral term common to outliners leaves the meaning to the user (UX decision D-45).
