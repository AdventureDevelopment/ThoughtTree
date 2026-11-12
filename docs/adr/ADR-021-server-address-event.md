# ADR-021: Server address is a property of the tree

id: ADR-021 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** record the tree's server address, and its join code if any, by `tree.server-changed`
- **MUST** sync a tree with a server only from the installation loaded from that server; in any other installation, the tree syncs by file only and the app names the right installation
- **MUST** move a tree to another server by pairing it into that server's installation and switching server sync on there
- **MUST** keep whether a device uses the server, and its sync mode, on the device
- **MAY** include the server address and join code in the pairing and sharing code

verify:

```bash
dotnet test --filter "ADR=ADR-021"
```

why: All devices of a tree must meet on the same server; one installation per server keeps the Content Security Policy strict.
