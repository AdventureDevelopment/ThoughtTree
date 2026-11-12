# ADR-056: Server store migrations

id: ADR-056 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Server*/**`, `infra/**`

- **MUST** version the server store schema and migrate it forward only
- **MUST** keep every schema readable and writable by the previous release (N-1), so an automatic rollback of the app never meets a store it cannot handle
- **MUST** split a change that N-1 could not handle into two releases: first add, later remove
- **MUST** test each migration against stored data of the previous release in the store contract tests

verify:

```bash
dotnet test --filter "ADR=ADR-056"
```

why: Production rolls back automatically when checks fail; a rollback must never corrupt the store.
