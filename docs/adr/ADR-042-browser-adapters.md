# ADR-042: Browser APIs behind interfaces

id: ADR-042 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** reach every browser API (local store, Web Crypto, file and share APIs, QR scanner, clipboard, service worker) only through a C# interface
- **MUST** keep JavaScript interop implementations thin, without logic
- **MUST** provide a fake implementation of every interface for tests
- **MUST NOT** call `IJSRuntime` outside the adapter implementations; adapters and UI share the Client project, so this is enforced by architecture tests per namespace, not by the banned API analyzer
- **MUST NOT** suppress analyzer or architecture rules (`#pragma warning disable`, `SuppressMessage`) outside an approved list of files, checked in CI

verify:

```bash
dotnet test --filter "ADR=ADR-042"   # architecture tests: IJSRuntime only in the adapters namespace; no unapproved suppressions
```

why: Logic stays testable without a browser; the simulation needs every adapter faked.
