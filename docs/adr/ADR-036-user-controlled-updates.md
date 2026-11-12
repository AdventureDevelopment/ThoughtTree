# ADR-036: User-controlled app updates

id: ADR-036 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** check for and apply app updates only with the user's consent; exception: an installed PWA opened online lets the browser check for a new service worker by itself (about once a day), which the app cannot prevent; the app still applies updates only with consent
- **MUST** rebuild snapshots and search index after an update

verify:

```bash
dotnet test --filter "ADR=ADR-036"
```

why: Update checks reveal the IP address; users in risky places must control them.
