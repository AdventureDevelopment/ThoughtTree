# ADR-019: Unencrypted export only as readable tree

id: ADR-019 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

Amended by ADR-078 (new ids only for items the tree does not know; update import), ADR-079 (reading headings as items is a tree feature), ADR-080 (a branch, not only the whole tree), ADR-082 (tags imported as items) and ADR-084 (no type is reused by a fixed id; no display names); owner decisions 2026-11-17 to 2026-11-19.

- **MUST** export unencrypted only the current tree (Markdown, OPML, JSON), without history, deleted items or device information (amended by ADR-080: also one branch)
- **MUST NOT** export the event log unencrypted
- **MUST NOT** make readable export a default or automatic; MUST require a confirmed warning and mark the file name as unencrypted
- **MUST** import readable files as a new tree or into an item, with ordinary events of the current event types (for example `item.created`, and for JSON also relations, relation types, display names, tags, themes, views, template markings and note changes) (amended by ADR-082, ADR-084: tags and relation types are items in the marked branches; display names do not exist)
- **SHOULD** write items as headings in the Markdown export and read headings as items (ADR-072) (amended by ADR-079: reading headings as items is a tree feature)
- **MUST** give every imported item a new id and rewrite `item:` links within the imported content; reuse predefined relation types by their fixed ids (amended by ADR-078: only items the tree does not know get new ids, an update import matches by id; amended by ADR-084: no item has a fixed id, imported type items move into the target's Relation types branch)

verify:

```bash
dotnet test --filter "ADR=ADR-019"
```

why: A backup without the key and a way in and out of ThoughtTree; history stays protected.
