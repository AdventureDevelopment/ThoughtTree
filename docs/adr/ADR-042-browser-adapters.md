# ADR-042: Browser APIs behind interfaces

id: ADR-042 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** reach every browser API (IndexedDB, Web Crypto, file and share APIs, QR scanner, clipboard, service worker) only through a C# interface
- **MUST** keep JavaScript interop implementations thin, without logic
- **MUST** provide a fake implementation of every interface for tests
- **MUST NOT** call `IJSRuntime` outside the adapter implementations

verify:

```bash
dotnet build -warnaserror   # IJSRuntime is a banned API outside the adapters
```

why: Logic stays testable without a browser; the simulation needs every adapter faked.
