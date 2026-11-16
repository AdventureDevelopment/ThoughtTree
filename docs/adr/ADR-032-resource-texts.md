# ADR-032: App texts in resource files

id: ADR-032 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** keep every user-visible text in resource files (`.resx`, `IStringLocalizer`), English and German
- **MUST NOT** write user-visible text directly into components
- **MUST** load culture data for all languages
- **MUST** keep Easy-language variants as their own sets of resource files (for example `de-x-easy`, `en-x-easy`), chosen in the settings like a language; the tutorial exists in Easy language from the start

verify:

```bash
dotnet test --filter "ADR=ADR-032"
```

why: Retrofitting translations touches every component; warnings must be exact in every language.
