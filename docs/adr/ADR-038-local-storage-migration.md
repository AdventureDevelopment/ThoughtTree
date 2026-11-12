# ADR-038: Local storage migrations without loss

id: ADR-038 · status: Accepted

applies_to: `src/ThoughtTree.Client/**`

- **MUST** give the IndexedDB layout a version and migrate it step by step on upgrade
- **MUST NOT** delete or overwrite events or tree access during a migration; a failed migration leaves the old data untouched
- **MUST** test every migration in real browsers: a database created by the previous release is opened by the new one and nothing is missing

verify:

```bash
dotnet test --filter "ADR=ADR-038"   # end-to-end migration tests
```

why: A failed migration loses notes that may exist on no other device.
