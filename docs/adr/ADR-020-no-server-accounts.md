# ADR-020: No user accounts on the server

id: ADR-020 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Server*/**`, `src/ThoughtTree.Client*/**`

- **MUST** authenticate only by device tokens, one per device and tree
- **MUST NOT** add user accounts or logins (OpenID Connect, OAuth, …)

verify:

```bash
grep -rE 'AddOpenIdConnect|AddMicrosoftIdentity|AddOAuth' src | wc -l   # expect 0
```

why: A login would link all trees of a user to one identity on the server.
