# ADR-019: Unencrypted export only as readable tree

id: ADR-019 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** export unencrypted only the current tree (Markdown, OPML, JSON), without history, deleted items or device information
- **MUST NOT** export the event log unencrypted
- **MUST NOT** make readable export a default or automatic; MUST require a confirmed warning and mark the file name as unencrypted
- **MUST** import readable files as a new tree or into an item, with ordinary events (`item.created`, and for JSON also `relation.added`, `relation-type.defined`, `display-name.set`)
- **SHOULD** write items as headings in the Markdown export and read headings as items (ADR-072)
- **MUST** give every imported item a new id and rewrite `item:` links within the imported content; reuse predefined relation types by their fixed ids

verify:

```bash
dotnet test --filter "ADR=ADR-019"
```

why: A backup without the key and a way in and out of ThoughtTree; history stays protected.
