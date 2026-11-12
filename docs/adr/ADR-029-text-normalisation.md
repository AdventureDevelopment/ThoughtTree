# ADR-029: Text normalisation before writing

id: ADR-029 · status: Accepted · kind: bet

applies_to: `src/ThoughtTree.Domain/**`

- **MUST** normalise all text to Unicode NFC before it enters an event (titles, notes, labels, names)
- **MUST** store line breaks in notes as `\n`; replace line breaks in titles by spaces
- **MUST** normalise again in search and comparisons
- **MUST** store titles as single-line Markdown with inline formatting and notes as Markdown, with `format: markdown`

verify:

```bash
dotnet test --filter "ADR=ADR-029"
```

why: Events are never rewritten; mixed forms would break search, comparisons and conflicts forever.

outcome: (to be filled)

assumption: (to be filled)

signal: (to be filled)

review: (to be filled)
