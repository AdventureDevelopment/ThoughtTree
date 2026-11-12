# ADR-040: Server logic as a library

id: ADR-040 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Server.Core/**`, `src/ThoughtTree.Server/**`

- **MUST** keep the server logic (registration, device tokens, duplicate detection, event store) in ThoughtTree.Server.Core
- **MUST NOT** reference ASP.NET Core from ThoughtTree.Server.Core
- **MUST** keep ThoughtTree.Server a thin layer of endpoints around the core

verify:

```bash
grep -l 'Microsoft.AspNetCore' src/ThoughtTree.Server.Core/*.csproj | wc -l   # expect 0
dotnet test --filter "ADR=ADR-040"   # architecture rules
```

why: The simulation uses the real server logic directly, without HTTP and without non-determinism.
