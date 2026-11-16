# ADR-007: Device-to-device sync by encrypted file

id: ADR-007 · status: Accepted · kind: bet ([bet card](bets.md#adr-007))

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Contracts/**`

- **MUST** export the whole encrypted log by default; import by merging with duplicates skipped
- **MUST** give the export file its own format version and a header with only the format version and `KeyId` (ADR-073); optional parts go inside the encrypted part
- **SHOULD** offer an incremental export later, based on what the other device had at its last import

verify:

```bash
dotnet test --filter "ADR=ADR-007"
```

why: Must work without internet; doubles as a backup.
