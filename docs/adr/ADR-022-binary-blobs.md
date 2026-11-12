# ADR-022: Binary data as separate blobs

id: ADR-022 · status: Accepted

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client/**`, `src/ThoughtTree.Server/**`

- **MUST NOT** put binary data (pictures, files) inside events
- **MUST** store binary data, once supported, as separate encrypted blobs referenced by id

verify:

```bash
dotnet test --filter "ADR=ADR-022"
```

why: Events are replayed, synced and kept forever and padded for small sizes.
