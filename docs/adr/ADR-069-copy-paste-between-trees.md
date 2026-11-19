# ADR-069: Copy and paste between trees

id: ADR-069 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

Amended by ADR-082 and ADR-084 (tags and relation types are items, matched by title); owner decision 2026-11-19.

- **MUST** let the user copy items with their branches and paste them into another tree (or another place of the same tree)
- **MUST** create the pasted items with new ids, as one action (ADR-067), so one undo removes the whole paste
- **MUST** carry over titles, notes, format, structure, tags (matched by title in the target tree, created there as items in the Tags branch when missing; amended by ADR-082: tags are items; amended by ADR-084: relation types are matched by title in the same way), themes and template markings
- **MUST** rewrite `item:` links and relations within the copied branches to the copies; **MUST** drop relations to items outside them and leave such links as they are (shown as not found in another tree)
- **MUST NOT** carry over history, devices or authorship: a paste starts fresh, like a readable import
- **MUST** also put Markdown and formatted text on the system clipboard, so pasting into other apps works (UX decision D-61)
- **MUST** show a calm hint the first time content is pasted into a shared tree ("This tree is shared with …")

verify:

```bash
dotnet test --filter "ADR=ADR-069"
```

why: Copies move content between trees without connecting them; templates and any other branch can be reused across trees.
