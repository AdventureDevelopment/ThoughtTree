# ADR-017: JSON with fixed type names

id: ADR-017 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`

- **MUST** serialize events as JSON inside the encrypted payload
- **MUST** use fixed type names such as `item.created`
- **MUST NOT** derive type names from .NET type or member names

verify:

```bash
dotnet test --filter "ADR=ADR-017"
```

why: Readable and debuggable; code can be renamed without breaking stored logs.
