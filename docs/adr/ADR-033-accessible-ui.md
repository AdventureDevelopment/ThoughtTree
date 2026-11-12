# ADR-033: Accessible, direction-neutral UI

id: ADR-033 · status: Accepted

applies_to: `src/ThoughtTree.Client/**`

- **MUST** meet WCAG 2.2, level AA
- **MUST** make every component usable with screen readers and keyboard only
- **MUST** offer an alternative (menu or keyboard command) for every drag or gesture
- **MUST** use CSS logical properties; MUST NOT use `left`/`right` for layout direction
- **MUST** render titles and note paragraphs with `dir="auto"`
- **MUST** give elements showing a domain concept a `data-testid` from the catalogue; controls are found by role and accessible name
- **MUST** announce changes that are not immediately visible through a live region
- **MUST** keep touch targets at least 24×24 CSS pixels

verify:

```bash
grep -rE '(margin|padding)-(left|right)' src/ThoughtTree.Client | wc -l   # expect 0
dotnet test --filter "ADR=ADR-033"
```

why: Accessibility and a mirrored interface cannot be added later without reworking every component.
