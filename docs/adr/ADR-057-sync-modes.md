# ADR-057: Sync modes, live preselected

id: ADR-057 · status: Accepted · kind: bet ([bet card](bets.md#adr-057))

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** offer live, manual and scheduled sync per tree and device
- **MUST** let the user choose the mode when server sync is switched on, with live preselected and the trade-offs explained
- **MUST** show unsynced changes and remind the user in manual and scheduled mode after a time the user sets

verify:

```bash
dotnet test --filter "ADR=ADR-057"
```

why: Reliability comes first; users who care more about activity patterns can still choose manual or scheduled sync.
