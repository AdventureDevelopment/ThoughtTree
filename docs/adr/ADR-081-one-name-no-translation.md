# ADR-081: One name per structural element, no translation inside the tree

id: ADR-081 · status: Superseded by ADR-084 · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Amends ADR-026 (display names per language) and the translation part of ADR-058's reason; owner decision 2026-11-18 (Zettelkasten session), accepted by the owner 2026-11-19. Settles the format before release 1, while nothing is stored.

- **MUST** give a user-definable structural element (relation type, tag) one name per field (`name`, `reverseName`), set by `display-name.set` / `display-name.removed` without a `language` field
- **MUST** show the name stored in the tree; for a predefined element without a stored name, the name from the app's resource files in the device's language
- **MUST NOT** store names of one element in several languages, and **MUST NOT** translate names or content (ADR-071)
- **MUST** write no stored name when a tree is created, so predefined elements follow the device's language until the user renames them

verify:

```bash
dotnet test --filter "ADR=ADR-081"
```

why: A name in the user's own words is what the user typed; per-language names add UI, conflicts and import/export cases for a rare need (co-authors of different languages), while predefined names still follow the device's language through the app's resources.
