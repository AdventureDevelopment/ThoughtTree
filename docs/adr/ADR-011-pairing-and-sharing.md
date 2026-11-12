# ADR-011: Pairing and sharing are separate

id: ADR-011 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client/**`

- **MUST** keep pairing (own devices) and sharing (other users) as separate actions, with a warning before sharing
- **MUST** include the user identity in the pairing code; MUST NOT include it in the sharing code
- **MUST** write `device.joined` as the first event of a new tree and whenever a device joins
- **MUST** derive private or shared from `device.joined` events (latest per device counts); MUST NOT store it as a flag
- **MUST** ask, when a device with another user identity adopts one by pairing, whether both are the same person; if so write a new `device.joined` in each of its trees

verify:

```bash
dotnet test --filter "ADR=ADR-011"
```

why: Shows where changes come from and whether a tree is private or shared.
