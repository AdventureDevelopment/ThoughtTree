# ADR-029: Text normalisation before writing

id: ADR-029 · status: Accepted · kind: bet ([bet card](bets.md#adr-029))

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

Amended by ADR-079 (headings in notes depend on the tree feature "Headings become thoughts") and ADR-084 (display names no longer exist); owner decisions 2026-11-17 and 2026-11-19.

- **MUST** normalise all text to Unicode NFC before it enters an event (titles, notes, display names, names) (amended by ADR-084: there are no display names)
- **MUST** store line breaks in notes as `\n`; replace line breaks in titles by spaces
- **MUST** normalise again in search and comparisons
- **MUST** store titles as single-line Markdown with inline formatting and notes as Markdown without headings (ADR-072), with `format: markdown` (amended by ADR-079: with the tree feature "Headings become thoughts" off, headings stay in the note)

verify:

```bash
dotnet test --filter "ADR=ADR-029"
```

why: Events are never rewritten; mixed forms would break search, comparisons and conflicts forever.
