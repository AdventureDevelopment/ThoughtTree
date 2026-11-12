# ADR-044: Abuse protection and registration modes

id: ADR-044 · status: Accepted · kind: bet

applies_to: `src/ThoughtTree.Server*/**`

- **MUST** enforce a maximum request size and maximum events and storage per account
- **MUST** rate-limit registrations and requests in memory only; **MUST NOT** store the IP addresses used for it
- **MUST** support the registration modes "open" and "join code only", set per server
- **MUST** reject oversized or malformed requests without storing them

verify:

```bash
dotnet test --filter "ADR=ADR-044"
```

why: Without user accounts anyone can register; limits keep a server, and its free tier, from being abused.

outcome: (to be filled)

assumption: (to be filled)

signal: (to be filled)

review: (to be filled)
