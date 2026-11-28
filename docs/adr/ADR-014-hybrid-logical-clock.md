# ADR-014: Event order by hybrid logical clock

id: ADR-014 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`

Rewritten by owner decision 2026-11-25: the clock is one number (was UTC time plus a counter) and the event id breaks ties (was the device id).

- **MUST** order events for replay by `clock`, then `EventId` compared as text, ordinal
- **MUST** give a new event the clock `max(now, highest + 1)`: `now` is the device's UTC time in milliseconds, `highest` the highest clock in the tree's log
- **MUST** give a new event the largest clock value if the log already holds it; the event id then orders it
- **MUST** derive the clock from the local log; MUST NOT store it separately
- **MUST** accept events from clocks far ahead and warn; MUST NOT reject or cap them
- **MUST** apply every event that ties on `clock`; MUST NOT drop, reject or report an event for the tie alone
- **MUST NOT** use `timestamp`, arrival order or sequence numbers to decide order or conflicts, nor `deviceId` to decide order
- **MUST NOT** change this order in a later app version

verify:

```bash
dotnet test --filter "ADR=ADR-014"
```

why: Every device computes the same tree; a change always sorts after everything its device had seen, and the latest edit in real time usually wins.
