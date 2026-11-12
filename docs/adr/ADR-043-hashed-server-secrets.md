# ADR-043: Server secrets only as hashes

id: ADR-043 · status: Accepted

applies_to: `src/ThoughtTree.Server*/**`

- **MUST** store account secrets, device tokens and join codes only as hashes
- **MUST** compare them in constant time
- **MUST NOT** log secrets, tokens or join codes

verify:

```bash
dotnet test --filter "ADR=ADR-043"
```

why: Stolen server data must not let anyone join an account or push events.
