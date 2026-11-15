# ADR-015: Minimal plaintext in the envelope

id: ADR-015 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**`

- **MUST** keep only `EventId`, envelope version, `KeyId` (a short random id assigned when the key is created), nonce and `ServerSequence` in plaintext
- **MUST** encrypt the whole event with AES-GCM (256-bit key, fresh random nonce per envelope), binding every plaintext field except `ServerSequence`, plus the account id and a fixed context label, as additional authenticated data (amended by ADR-073; the account id is authenticated but not stored in the envelope)
- **MUST** pad the payload to a size bucket before encryption
- **MUST NOT** add a plaintext field without a new ADR

verify:

```bash
dotnet test --filter "ADR=ADR-015"
```

why: The server only stores, deduplicates and relays; anything more would reveal structure or activity.
