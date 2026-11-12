# ADR-027: Domain and Contracts projects

id: ADR-027 · status: Accepted

applies_to: `src/**`

- **MUST** keep domain logic (events, projection, conflicts, clock, and the command logic that decides which events a user action writes) in ThoughtTree.Domain, used by the client only
- **MUST** share only ThoughtTree.Contracts (envelope, export format, API data) between client and server
- **MUST NOT** reference ThoughtTree.Domain from the server
- **MUST NOT** use browser, JavaScript interop, storage, network or async I/O in ThoughtTree.Domain; these APIs are listed in Domain's banned API file

verify:

```bash
dotnet build -warnaserror   # banned APIs in Domain are compile errors
dotnet test --filter "ADR=ADR-027"   # architecture rules (ArchUnitNET)
```

why: The server cannot decrypt events; domain logic stays pure and testable.
