# ADR-045: Strict Content Security Policy

id: ADR-045 · status: Accepted · kind: constraint

applies_to: `src/**`, `infra/**`

- **MUST** serve the app with a strict Content Security Policy: scripts, styles, fonts, images and connections only to its own origin, which is also its sync server (one installation per server, ADR-021)
- **MUST** send `Referrer-Policy: no-referrer`
- **MUST NOT** fetch link previews automatically
- **MUST NOT** load anything from third parties (trackers, fonts, analytics, remote pictures in Markdown)
- **MUST** send the policy as HTTP headers on every hosting (Azure Static Web Apps configuration file in release 1, the server later), not only as a page tag
- **MUST** let a self-hosted package serve its own app with its own policy, instead of widening the policy of the hosted app
- **MUST NOT** weaken the policy with `unsafe-inline` or `unsafe-eval` beyond what Blazor WebAssembly requires (`wasm-unsafe-eval`)

verify:

```bash
dotnet test --filter "ADR=ADR-045"   # headers in deployment and end-to-end tests
```

why: The browser itself blocks every request to third parties, whatever code creeps in.
