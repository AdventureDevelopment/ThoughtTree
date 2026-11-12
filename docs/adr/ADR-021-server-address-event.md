# ADR-021: Server address is a property of the tree

id: ADR-021 · status: Accepted

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client/**`

- **MUST** record the tree's server address, and its join code if any, by `tree.server-changed`
- **MUST** keep whether a device uses the server, and its sync mode, on the device
- **MAY** include the server address and join code in the pairing and sharing code

verify:

```bash
dotnet test --filter "ADR=ADR-021"
```

why: All devices of a tree must meet on the same server; each device can still stay on file sync.
