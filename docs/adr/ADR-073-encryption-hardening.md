# ADR-073: Encryption hardening before release 1

id: ADR-073 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Amends ADR-015 (authenticated data), ADR-012 (extractable key) and ADR-028 (key handed in for each operation); owner decision 2026-11-15.

- **MUST** bind every plaintext envelope field except `ServerSequence` (envelope version, `KeyId`, `EventId`) plus the account id and a fixed context label as additional authenticated data
- **MUST** encrypt a payload wrapper object with room for optional parts (for example a later device signature), not the bare event
- **MUST** encrypt and decrypt only through a non-extractable key handle; **MUST** store the raw tree key only wrapped by a non-extractable device key, unwrapped only for pairing, sharing or key backup
- **MUST NOT** pass the raw tree key into .NET memory, logs or reports outside pairing, sharing or key backup
- **MUST** take production keys and nonces only from the browser's cryptographic random source; a test checks the production composition
- **MUST** encrypt snapshots, search index and drafts at rest with the tree key
- **MUST** keep the exchange file header to format version and `KeyId`, bound to the contents; account id, device labels and tree names go inside the encrypted part (the account id is also authenticated data of every envelope, taken from the tree access on both sides)
- **SHOULD** batch Web Crypto calls during replay

verify:

```bash
dotnet test --filter "ADR=ADR-073"
```

why: Cheap now, impossible after release 1 freezes the formats; keeps doors open for signatures and an app lock.
