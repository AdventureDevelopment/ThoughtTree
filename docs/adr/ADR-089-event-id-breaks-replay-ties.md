# ADR-089: The event id breaks replay ties

id: ADR-089 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`

Amends ADR-014 (a third sort key after `clock` and `deviceId`); proposed 2026-11-25, owner decision pending.

- **MUST** order events that are equal in `clock` and `deviceId` by `EventId`, ordinal
- **MUST** apply every such event in that order; **MUST NOT** drop, reject or report an event for the tie alone
- **MUST NOT** change this order in a later app version; every device and version replays ties the same way

verify:

```bash
dotnet test --filter "ADR=ADR-089"
```

why: A copied device id (browser profile copy, device backup restored on a second device) or a faulty client can stamp two events with the same clock and device; without a fixed last key, arrival order decides and devices show different trees (INV-1).
