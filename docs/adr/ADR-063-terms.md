# ADR-063: Terms: item, note, branch, node

id: ADR-063 · status: Accepted · kind: constraint

applies_to: `docs/**`, `src/**`, `tests/**`

- **MUST** use these terms in documents, code, tests and the test id catalogue:
  - **Item:** the thing in the tree: a title, a note, tags, and optionally a theme and views (later pictures and files); what it is (thought, idea, project, topic, vision) is the user's to decide, and an item can grow
  - **Thought:** the user-facing word for an item, used in the texts the user reads (UX decision D-140); documents, code, tests and test ids say item, except when quoting a user-facing text or giving an example
  - **Note:** the optional longer text of an item (unchanged from today's glossary)
  - **Branch:** an item together with everything below it
  - **Node:** how an item appears in the tree, its line and place; a technical term for rendering and specifications
- The glossary of `architecture.md` uses these terms
- **MUST** use the renamed event types, fields and link scheme (done before the first release, while nothing is stored): `item.created`, `item.title-changed`, `item.note-changed`, `item.moved`, `item.deleted`, `item.restored`, `item.tagged`, `item.untagged`; fields `itemId`, `afterItemId`, `fromItemId`, `toItemId`; links `[text](item:<id>)`; test ids such as `item-title`

verify:

```bash
grep -rnE 'node\.(created|moved|deleted|restored|title-changed|note-changed|tagged|untagged)|\(node:' src tests docs/architecture.md | wc -l   # expect 0
```

why: Items grow from a single thought into projects or visions; a neutral term common to outliners leaves the meaning to the user (UX decision D-45).
