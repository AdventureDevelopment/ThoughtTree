# ADR-074: Release 1 scope: standalone, file exchange, final formats

id: ADR-074 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`

Scope and order are in section 9 of [architecture.md](../architecture.md) ("Release 1 scope").

- **MUST** write in release 1 the final, server-ready stored formats (envelope version 1, payload wrapper, pairing and sharing code, export file, readable formats, local store layout); later releases only add event types and fields (ADR-016)
- **MUST** implement and test the Domain rules and formats of sharing (`device.joined` with user identity, code kind, private or shared; pairing content and user identity as in ADR-028, key and account identity as in ADR-008) even though the sharing UI is not in release 1
- **MUST NOT** ship server sync, sync modes, sharing UI, the private feedback inbox or experiment and permission flags in release 1
- **MUST** hide every feature that is not complete behind a release flag, including in the tutorial
- **MUST** leave server tests out of the release 1 push gate; they join it with the first server release

verify:

```bash
dotnet test --filter "ADR=ADR-074"
```

why: Release 1 stays small but freezes nothing that later server sync or sharing would have to migrate.
