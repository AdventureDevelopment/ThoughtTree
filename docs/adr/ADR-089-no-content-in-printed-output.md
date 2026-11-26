# ADR-089: No user content in printed output

id: ADR-089 · status: Accepted · kind: constraint

applies_to: `src/**`

Proposed by an agent 2026-11-26 (clean-code review T9, option c), accepted by the owner 2026-11-26; amends no ADR.

- **MUST NOT** put user content into text that code prints: `ToString()`, exception messages, log and console output. User content is titles, notes and every other text the user typed, such as names of trees, tags, relation types and views, and device labels
- **MUST** override `ToString()` (or `PrintMembers`) on every record that holds user content, because the compiler-generated one prints every property
- **MAY** print ids, event types, kinds and counts, so local debugging stays possible; reports leave out ids as well (ADR-051)

verify:

```bash
dotnet test --filter "ADR=ADR-089"   # printed events, items and trees carry no titles or notes
```

why: Printed text leaves the device without the user choosing to share content (screenshots, browser consoles, bug reports, server logs), so end-to-end encryption (ADR-015, ADR-030) must not leak through it.
