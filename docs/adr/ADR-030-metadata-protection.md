# ADR-030: Metadata protection

id: ADR-030 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**`

- **MUST NOT** store receive times or IP addresses on the server, or record IP addresses in logs
- **MUST** push events in batches after a pause in editing
- **MUST** send a neutral `Accept-Language` header with sync requests
- **MUST** run scheduled sync at a random moment within a window fixed in UTC
- **MUST NOT** add analytics, telemetry or third-party requests to the client or the server (the server writes plain logs only); user-initiated feedback encrypted to the operator (ADR-055) is not telemetry

verify:

```bash
dotnet test --filter "ADR=ADR-030"
```

why: Nobody operating or observing the server should learn where the user is or how they work.
