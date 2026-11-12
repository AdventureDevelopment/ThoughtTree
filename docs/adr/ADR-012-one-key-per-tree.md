# ADR-012: One key per tree, equal access

id: ADR-012 · status: Accepted · kind: bet ([bet card](bets.md#adr-012))

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Domain/**`

- **MUST** use one random key per tree, the same for every holder
- **MUST NOT** implement read-only roles, per-user keys or revocation (deferred)
- **MUST** store the key extractable, because pairing and sharing put it into a code

verify:

```bash
dotnet test --filter "ADR=ADR-012"
```

why: Keeps the encryption design simple; per-user keys are a much larger design.
