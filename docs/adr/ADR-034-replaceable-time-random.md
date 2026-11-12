# ADR-034: Replaceable time and randomness

id: ADR-034 · status: Accepted · kind: constraint

applies_to: `src/**`

- **MUST** obtain time only through `TimeProvider`
- **MUST** obtain ids, nonces and random values only through a replaceable generator
- **MUST NOT** call `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, `Guid.NewGuid` or `new Random` outside the provider implementations
- **MUST** list these APIs in the banned API analyzer (`BannedSymbols.txt`), with the provider implementations as the only exemption

verify:

```bash
dotnet build -warnaserror   # banned APIs are compile errors
```

why: Every test must be exactly reproducible.
