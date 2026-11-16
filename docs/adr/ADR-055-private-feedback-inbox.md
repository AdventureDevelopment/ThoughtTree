# ADR-055: Private feedback inbox

id: ADR-055 · status: Accepted · kind: bet ([bet card](bets.md#adr-055))

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**`

- **MUST** encrypt feedback on the device with the operator's public key; the server stores only ciphertext
- **MUST** delete feedback from the server once the operator has collected it
- **MUST** offer the inbox only to accounts with the permission flag; assign it at first as an experiment to a share of accounts
- **MUST** provide an operations flag that switches the inbox off
- **MUST** send only on the user's initiative, with the same preview as other feedback; **MUST NOT** store IP addresses
- **MUST** treat what arrives as untrusted input; only the operator turns it into GitHub issues
- **MUST NOT** add spam protection beyond the general request limits until a signal shows the need

verify:

```bash
dotnet test --filter "ADR=ADR-055"   # when: server
```

why: A private, easy channel lowers the barrier for feedback without the server being able to read it.
