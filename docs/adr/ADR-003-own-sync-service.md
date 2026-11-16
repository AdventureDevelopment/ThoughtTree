# ADR-003: Own ASP.NET Core sync service

id: ADR-003 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Server*/**`

- **MUST** implement the sync service in ASP.NET Core
- **MUST NOT** depend on a backend-as-a-service (Firebase, Supabase, Appwrite, …)
- **SHOULD** keep the server a thin store and relay of encrypted envelopes

verify:

```bash
grep -riE 'firebase|supabase|appwrite' --include=*.csproj src | wc -l   # expect 0
```

why: Full control over the event model and merge rules; stays in .NET; self-hostable.
