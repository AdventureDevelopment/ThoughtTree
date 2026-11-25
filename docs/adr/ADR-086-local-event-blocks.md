# ADR-086: Local event blocks for replay

id: ADR-086 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

Owner decision 2026-11-25 (spike 1 decision 4, option A), proposed; the owner reviews this text before accepting it. Meets the "SHOULD batch Web Crypto calls during replay" rule of ADR-073; amends no ADR. Part of the local store layout (ADR-074), versioned by ADR-038 and free to change before release 1 (ADR-085).

- **MUST** keep the per-event envelopes as the source of truth; blocks are a local copy that can be rebuilt from them at any time
- **MUST NOT** sync, export or send blocks to a server; sync and the envelope format stay unchanged
- **MUST** also store a tree's events locally in encrypted blocks of about 1,000 events
- **MUST** encrypt a block with the tree key only through the non-extractable key handle (ADR-073), binding as additional authenticated data a fixed context label for blocks, the tree id, the account id, the `KeyId`, the block's event range and the local store layout version
- **MUST** pad a block's plaintext to a multiple of 16 KiB
- **MUST** treat an event as saved once its envelope is stored (ADR-088); writing the block that holds it never delays this
- **MUST** replay from the blocks plus the events not yet in a block, giving the same tree as a replay from the envelopes alone
- **MUST** rebuild a missing, unreadable or incomplete block from the envelopes; a broken block never prevents the start
- **MAY** discard and rebuild all blocks, for example after a layout migration

verify:

```bash
dotnet test --filter "ADR=ADR-086"
```

why: Spike 1 measured that one read and one Web Crypto call per event dominate a full replay (phone, Chrome: read and decrypt 11.6 s with one call each per event against 1.2 s in blocks of 1,000; replay from blocks estimated at 5 to 6 s); as derived data the blocks cost only local storage (about 70 % more, report estimate) and leave the stored server-ready formats unchanged.
