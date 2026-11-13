# ADR-065: "Focus" instead of "Zoom"

id: ADR-065 · status: Proposed · kind: constraint

applies_to: `docs/**`, `src/ThoughtTree.Client*/**`, `tests/**`

- **MUST** call making an item the temporary top of the view, showing only its branch, **Focus** in documents, code, tests, the test id catalogue and the app's wording; **MUST NOT** call it "zoom"
- **MUST** keep "zoom" only for magnifying (pinch or browser zoom), which the app does not redefine
- **MUST** update on acceptance: the glossary entry "Zoom" in `architecture.md` and its other uses (section 1, section 8 "UI / UX", search scope, the usability gate), ADR-013, ADR-023 and the bet card of ADR-023 (for example "focus path" instead of "zoom path")

verify:

```bash
grep -rniE '\bzoom(ed|ing)?\b' docs/architecture.md docs/adr src tests | grep -v ADR-065 | wc -l   # expect 0 once applied
```

why: "Zoom" suggests magnifying; the owner means concentrating on one item and its branch (UX decisions D-56, D-57).
