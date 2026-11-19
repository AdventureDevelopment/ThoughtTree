# ADR-085: Beta data before release 1

id: ADR-085 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`

Amends ADR-016 and ADR-038 (they bind stored data from release 1 on); owner decision 2026-11-19 (session "Tags and relation types as items"), accepted by the owner 2026-11-19.

- **MAY** change stored formats (events, envelope, local store layout) before release 1; from release 1 on ADR-016 and ADR-038 apply without exception
- **MUST** give every build before release 1 a stored-format version and keep it with each tree on the device
- **MUST**, before an update that changes the stored-format version, tell the tester so and offer to export every tree in the Obsidian form ([zettelkasten-claim.md](../zettelkasten-claim.md)); the update waits until the tester has exported or declined (ADR-036)
- **MUST**, after such an update, keep the old trees' data untouched, show them as not readable by this version, and offer to import the exported files as new trees (a copy; history, conflicts and devices are lost)
- **MUST NOT** promise testers that their data survives; the prompt says "kept as far as possible"; the prompt and the import offer are new UI and need a sketch first (ADR-064)

verify:

```bash
dotnet test --filter "ADR=ADR-085"
```

why: Formats must stay free to change while testers use the app before release 1; the Obsidian form is the most stable bridge, since its conventions are not ours and it carries everything but history (owner: "should not lose data on the way to v1, if possible, but it is not guaranteed").
