# ADR-047: One container image everywhere

id: ADR-047 · status: Accepted · kind: bet ([bet card](bets.md#adr-047))

applies_to: `src/ThoughtTree.Server*/**`, `infra/**`, `.github/**`

- **MUST** serve release 1, which has no sync server, as a static app from Azure Static Web Apps, with a preview address per pull request and the security headers of ADR-045 from its configuration file
- **MUST**, once a sync server exists, ship one image containing the server and the static client app, served from one origin
- **MUST** run the same image in test, production (Azure Container Apps) and self-hosting; differences only by configuration
- **MUST** select the store by configuration: the Azure store on Azure, SQLite on a volume for self-hosting
- **MUST NOT** run SQLite on Azure file shares
- **MUST** speak plain HTTP in the container and rely on a reverse proxy for HTTPS; **MUST** provide a Docker Compose example with a reverse proxy that obtains certificates automatically

verify:

```bash
dotnet test --filter "ADR=ADR-047"   # deployment smoke tests run against the image; when: server
```

why: What is tested is exactly what runs; one origin keeps the Content Security Policy simple.
