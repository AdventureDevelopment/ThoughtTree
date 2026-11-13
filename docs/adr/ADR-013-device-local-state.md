# ADR-013: Device state is never synced

id: ADR-013 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** save text being edited as a local draft on every change and turn it into an event at the end of the editing session or after a crash
- **MUST** keep collapsed items, focus, open tree, language, sync mode, use of the server and experimental-features switch on the device, per tree where applicable
- **MUST NOT** write device state into the event log
- The theme of a tree or item is content, not device state (ADR-060); device settings such as text size and hand side stay on the device

verify:

```bash
dotnet test --filter "ADR=ADR-013"
```

why: The log records content changes; each device keeps its own view.
