# ADR-026: Translatable display names for structural elements

id: ADR-026 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** give user-definable structural elements their names only through `display-name.set` / `display-name.removed`, per language
- **MUST** show display names in this order: device language, its base language, app resource files (predefined elements), any display name
- **MUST NOT** translate content (titles, notes) in the log
- **MUST** translate content on the fly, if ever, on the device or only after explicit consent with a warning

verify:

```bash
dotnet test --filter "ADR=ADR-026"
```

why: One translation mechanism for all structural elements; content stays as written.
