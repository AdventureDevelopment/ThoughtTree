# ADR-045: Strict Content Security Policy

id: ADR-045 · status: Accepted

applies_to: `src/**`, `infra/**`

- **MUST** serve the app with a strict Content Security Policy: scripts, styles, fonts and images only from its own origin; connections only to its own origin and the tree's sync server
- **MUST** send `Referrer-Policy: no-referrer`
- **MUST NOT** fetch link previews automatically
- **MUST NOT** load anything from third parties (trackers, fonts, analytics, remote pictures in Markdown)
- **MUST** let a self-hosted package serve its own app with its own policy, instead of widening the policy of the hosted app
- **MUST NOT** weaken the policy with `unsafe-inline` or `unsafe-eval` beyond what Blazor WebAssembly requires (`wasm-unsafe-eval`)

verify:

```bash
dotnet test --filter "ADR=ADR-045"   # headers in deployment and end-to-end tests
```

why: The browser itself blocks every request to third parties, whatever code creeps in.
