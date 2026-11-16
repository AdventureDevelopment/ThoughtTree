# ADR-065: "Focus" instead of "Zoom"

id: ADR-065 · status: Accepted · kind: constraint

applies_to: `docs/**`, `src/ThoughtTree.Client*/**`, `tests/**`

- **MUST** call making an item the temporary top of the view, showing only its branch, **Focus** in documents, code, tests, the test id catalogue and the app's wording; **MUST NOT** call it "zoom"
- **MUST** keep "zoom" only for magnifying (pinch or browser zoom), which the app does not redefine

verify:

```bash
grep -rniE '\bzoom(ed|ing)?\b' docs/architecture.md docs/adr src tests | grep -v ADR-065 | grep -v 'magnifying' | wc -l   # expect 0
```

why: "Zoom" suggests magnifying; the owner means concentrating on one item and its branch (UX decisions D-56, D-57).
