# ADR-037: Versioned, compatible sync API

id: ADR-037 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Server*/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

- **MUST** carry the API version in the address (`/v1/…`)
- **MUST** keep the server able to serve every released client version of the same major API version
- **MUST** let the client work with older servers, or tell the user clearly that the server needs updating
- **MUST** add recorded requests and responses of each release to `tests/data/formats/<version>/`
- **MUST NOT** change the meaning of an existing endpoint, field or status code within a major version

verify:

```bash
dotnet test --filter "ADR=ADR-037"
```

why: Self-hosted servers and apps update independently; an unversioned API cannot get versioning later without breaking clients.
