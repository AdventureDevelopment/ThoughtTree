# ADR-028: Tree access

id: ADR-028 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Contracts/**`

- **MUST** keep key, `KeyId`, account id and account secret (the tree access) outside the event log, stored only by the Tree Manager
- **MUST** hand over exactly the tree access in pairing, sharing and key backup; pairing adds the user identity
- **MUST NOT** put tree access, device tokens or device settings into events
- **MUST** give the pairing and sharing code its own format version
- **MUST** limit the length of user name and server address so that the largest possible code fits into a QR code that phones read reliably
- **MUST** keep Crypto stateless: the key handle (non-extractable, ADR-073) is handed in for each operation

verify:

```bash
dotnet test --filter "ADR=ADR-028"
```

why: The log restores content, the tree access restores the right to read and sync it.
