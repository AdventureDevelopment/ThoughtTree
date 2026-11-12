# ADR-008: Standalone first

id: ADR-008 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** create key and account identity of a tree on the device
- **MUST** work fully with no network, server, account or login
- **MUST NOT** require the server for anything except server sync

verify:

```bash
dotnet test --filter "ADR=ADR-008"
```

why: A device must work on its own; server sync is optional per tree.
