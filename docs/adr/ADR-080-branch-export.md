# ADR-080: Readable export and import of a branch, where the user is

id: ADR-080 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Contracts/**`

Amends ADR-019 (a branch, not only the whole tree); owner decision 2026-11-17 (Zettelkasten session), proposed for acceptance in a pull request review.

- **MUST** offer readable export of a thought with its whole branch from the thought's actions (its ⋯ menu and Focus), in every readable format, the Obsidian form included
- **MUST** write a single thought without children as one `.md` file, and a branch as a zip of files
- **MUST** keep every rule of ADR-019 for a branch: unencrypted only after a confirmed warning, never automatic, no history, deleted items or device information, file name marked as unencrypted
- **MUST** offer "Import into this thought…" in the same place, accepting one or several files or a zip; files carrying ids of this tree update their thoughts (ADR-078)
- **MUST** write links to thoughts outside the exported branch so that Obsidian can resolve them against earlier exports (the form is fixed with the Zettelkasten export, [zettelkasten-claim.md](../zettelkasten-claim.md))

verify:

```bash
dotnet test --filter "ADR=ADR-080"
```

why: A user who remembers "I want this in Obsidian too" exports it where they are, without exporting the whole tree; a branch reveals less than the tree.
