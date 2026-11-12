# ADR-002: Event sourcing as data model

id: ADR-002 · status: Accepted

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client/**`

- **MUST** record every change of a tree's content as an immutable event
- **MUST** derive the tree only by replaying events (projection)
- **MUST NOT** store, sync or export tree state as the source of truth; snapshots are local caches that can be thrown away

verify:

```bash
dotnet test --filter "ADR=ADR-002"
```

why: Enables offline-first work and log-based sync with small, mergeable changes.
