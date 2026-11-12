# ADR-026: Translatable labels for structural elements

id: ADR-026 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client/**`

- **MUST** give user-definable structural elements their names only through `label.set` / `label.removed`, per language
- **MUST** show labels in this order: device language, its base language, app resource files (predefined elements), any label
- **MUST NOT** translate content (titles, notes) in the log
- **MUST** translate content on the fly, if ever, on the device or only after explicit consent with a warning

verify:

```bash
dotnet test --filter "ADR=ADR-026"
```

why: One translation mechanism for all structural elements; content stays as written.
