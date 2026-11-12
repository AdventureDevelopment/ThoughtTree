# ADR-006: Event exchange behind ISyncTransport

id: ADR-006 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client/**`, `src/ThoughtTree.Server*/**`

- **MUST** exchange events with server or file only through `ISyncTransport`
- **MUST** follow the four steps: find out what the other side has, send what it lacks, receive what this side lacks, skip duplicates by `EventId`
- **MUST** deliver the events of a device without gaps: never a later event of device D without all earlier ones
- **MUST** track sync progress per destination, not by one synced flag per event
- **MUST** store each `EventId` only once on the server, so repeated pushes are harmless

verify:

```bash
dotnet test --filter "ADR=ADR-006"
```

why: One exchange rule for every transport; conflict detection relies on the no-gap rule.
