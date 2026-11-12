# ADR-033: Accessible, direction-neutral UI

id: ADR-033 · status: Accepted

applies_to: `src/ThoughtTree.Client/**`

- **MUST** make every component usable with screen readers and keyboard only
- **MUST** offer an alternative (menu or keyboard command) for every drag or gesture
- **MUST** use CSS logical properties; MUST NOT use `left`/`right` for layout direction
- **MUST** render titles and note paragraphs with `dir="auto"`
- **MUST** give elements used by tests a stable `data-testid`

verify:

```bash
grep -rE '(margin|padding)-(left|right)' src/ThoughtTree.Client | wc -l   # expect 0
dotnet test --filter "ADR=ADR-033"
```

why: Accessibility and a mirrored interface cannot be added later without reworking every component.
