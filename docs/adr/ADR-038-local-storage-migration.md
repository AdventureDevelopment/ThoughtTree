# ADR-038: Local storage migrations without loss

id: ADR-038 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

Amended by ADR-085 (the local store layout may change before release 1; the rules bind from release 1 on); owner decision 2026-11-19.

- **MUST** give the local store layout (IndexedDB or SQLite) a version and migrate it step by step on upgrade
- **MUST NOT** delete or overwrite events or tree access during a migration; a failed migration leaves the old data untouched
- **MUST** test every migration in real browsers: a database created by the previous release is opened by the new one and nothing is missing

verify:

```bash
dotnet test --filter "ADR=ADR-038"   # end-to-end migration tests
```

why: A failed migration loses notes that may exist on no other device.
