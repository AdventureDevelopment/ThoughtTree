# ADR-090: Release 1 fixes the formats own devices need; sharing's come with sharing

id: ADR-090 · status: Proposed · kind: bet ([bet card](bets.md#adr-090))

applies_to: `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`

Amends ADR-074 (sharing's formats and Domain rules in release 1; its why) and ADR-011 (its rules on the sharing code and on private or shared are fixed when sharing is built); owner decision 2026-11-28 (option S2, release 1 scope decision 2 in [architecture.md](../architecture.md) section 9).

- **MUST** write in release 1 the final stored formats a tree on one user's own devices needs: envelope version 1, payload wrapper, pairing code, export file, readable formats, local store layout; later releases only add event types, fields and code format versions (ADR-016)
- **MUST** keep `device.joined` with user identity, `user.name-changed` and `device.label-changed` in release 1, because own devices use them
- **MUST NOT** fix in release 1 what only sharing needs: the sharing code kind, the derivation of private or shared, rules on names and erasure of other users, vouching for another user's device; they are designed and fixed when sharing is built
- **MUST**, before a format is fixed for release 1, check whether it closes a door for sharing, and record the answer where the format is decided
- **MAY** give sharing a new code format version; an app that does not know it says it cannot open the shared tree and offers the update (ADR-016, D-128)

verify:

```bash
dotnet test --filter "ADR=ADR-090"   # release 1 writes no sharing code kind
```

why: Sharing pulls in identity, trust, owner and threat questions that need more learning; fixing them now would delay any release, and the door check keeps adding sharing later cheap.
