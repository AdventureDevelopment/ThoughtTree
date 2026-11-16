# ADR-006: Event exchange behind ISyncTransport

id: ADR-006 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**`

- **MUST** exchange events with server or file only through `ISyncTransport`
- **MUST** follow the four steps: find out what the other side has, send what it lacks, receive what this side lacks, skip duplicates by `EventId`
- **MUST** deliver the events of a device without gaps: never a later event of device D without all earlier ones
- **MUST** track sync progress per destination, not by one synced flag per event
- **MUST** store each `EventId` only once on the server, so repeated pushes are harmless
- **MUST** reject an envelope whose `EventId` is known but whose content differs, on the server and on every device, and report it as a defect (INV-13)
- **MUST** give `ServerSequence` numbers gap-free per account and make them readable only in commit order
- **MUST** store a pushed batch all or nothing, in the order sent; **MUST** send batches strictly one after another, each only after the previous one is confirmed

verify:

```bash
dotnet test --filter "ADR=ADR-006"
```

why: One exchange rule for every transport; conflict detection relies on the no-gap rule.
