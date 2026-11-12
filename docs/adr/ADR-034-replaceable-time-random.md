# ADR-034: Replaceable time and randomness

id: ADR-034 · status: Accepted

applies_to: `src/**`

- **MUST** obtain time only through `TimeProvider`
- **MUST** obtain ids, nonces and random values only through a replaceable generator
- **MUST NOT** call `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `Guid.NewGuid` or `new Random` outside the provider implementations

verify:

```bash
grep -rnE 'DateTime(Offset)?\.(Utc)?Now|Guid\.NewGuid|new Random\(' src --include=*.cs | grep -v Providers/ | wc -l   # expect 0
```

why: Every test must be exactly reproducible.
