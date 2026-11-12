# ADR-014: Event order by hybrid logical clock

id: ADR-014 · status: Accepted

applies_to: `src/ThoughtTree.Domain/**`

- **MUST** order events for replay by `clock` (UTC milliseconds plus counter), then `deviceId`
- **MUST** compare `deviceId` as the lowercase text form of the GUID, ordinal
- **MUST** give a new event the device's UTC time, or the highest clock in the tree's log with the counter raised by one if that is equal or later
- **MUST** derive the clock from the local log; MUST NOT store it separately
- **MUST** accept events from clocks far ahead and warn; MUST NOT reject or cap them
- **MUST NOT** use `timestamp`, arrival order or sequence numbers to decide order or conflicts

verify:

```bash
dotnet test --filter "ADR=ADR-014"
```

why: Every device computes the same tree; the latest edit in real time usually wins.
