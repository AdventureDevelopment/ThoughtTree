# ADR-031: Development flags, tree features, device settings

id: ADR-031 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client/**`, `src/ThoughtTree.Server*/**`, `src/ThoughtTree.Domain/**`

- **MUST** load development flags only from configuration shipped with the app; MUST NOT use a remote flag service or user targeting
- **MUST NOT** write development flags into events; remove a flag once its feature is complete
- **MUST** record tree features by `tree.feature-changed`
- **MUST** let switches only hide commands and UI; the projection always applies every known event

verify:

```bash
dotnet test --filter "ADR=ADR-031"
```

why: No switch may make trees differ or add server contacts.
