# ADR-019: Unencrypted export only as readable tree

id: ADR-019 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client/**`

- **MUST** export unencrypted only the current tree (Markdown, OPML, JSON), without history, deleted nodes or device information
- **MUST NOT** export the event log unencrypted
- **MUST NOT** make readable export a default or automatic; MUST require a confirmed warning and mark the file name as unencrypted
- **MUST** import readable files as a new tree or into a node, with ordinary `node.created` events

verify:

```bash
dotnet test --filter "ADR=ADR-019"
```

why: A backup without the key and a way in and out of ThoughtTree; history stays protected.
