# ADR-013: Device state is never synced

id: ADR-013 · status: Accepted

applies_to: `src/ThoughtTree.Client/**`

- **MUST** keep collapsed nodes, zoom, open tree, language, sync mode, use of the server and experimental-features switch on the device, per tree where applicable
- **MUST NOT** write device state into the event log

verify:

```bash
dotnet test --filter "ADR=ADR-013"
```

why: The log records content changes; each device keeps its own view.
