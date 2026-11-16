# ADR-031: Kinds of switches

id: ADR-031 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**`, `src/ThoughtTree.Domain/**`

- **MUST** keep switches apart by kind: release flags, experiment flags, operations flags, permission flags, tree features, device settings
- **MUST** load release flags only from configuration shipped with the app; remove them once their feature is complete
- **MUST** assign experiment flags only per account on the server or by a local draw on the device; each experiment has a bet card and ends by its review
- **MUST** store permission flags and server-assigned experiment flags only as flags per account, and report them to the device during sync; **MUST** keep a locally drawn experiment flag as device state (ADR-013), never synced and never sent to the server
- **MUST NOT** use a third-party flag service, tracking or telemetry; **MUST NOT** write flags into events
- **MUST** record tree features by `tree.feature-changed`
- **MUST** let switches only hide commands and UI; the projection always applies every known event

verify:

```bash
dotnet test --filter "ADR=ADR-031"
```

why: Switches serve development, experiments, operations and permissions without making trees differ or collecting data.
