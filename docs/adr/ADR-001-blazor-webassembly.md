# ADR-001: Client in Blazor WebAssembly

id: ADR-001 · status: Accepted

applies_to: `src/ThoughtTree.Client/**`

- **MUST** build the client in C# with Blazor WebAssembly, delivered as a PWA
- **MUST NOT** add a JavaScript UI framework (React, Vue, Angular, Svelte, …)
- **SHOULD** limit JavaScript interop to browser APIs without .NET equivalent (IndexedDB, Web Crypto, service worker, file and share APIs)

verify:

```bash
grep -rlE '"(react|vue|@angular/core|svelte)"' --include=package.json src | wc -l   # expect 0
```

why: The developer works in C#; one language across client and server.
