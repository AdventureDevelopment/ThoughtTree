# Architecture Decision Records

Agent-optimised ADRs: scoped, imperative, verifiable, short. One file per decision; the index is in section 9 of [architecture.md](../architecture.md).

## Format

```markdown
# ADR-0xx: <title>

id: ADR-0xx · status: Accepted | Proposed | Superseded by ADR-0yy

applies_to: `<glob>`, …

- **MUST** …
- **MUST NOT** …
- **SHOULD** …
- **MAY** …

verify:

    <command>   # expected result

why: <one line>
```

## Conventions

- `applies_to` names the paths the rules govern; before changing a file, read every ADR whose `applies_to` matches it
- `verify` is run by CI; most checks run the tests tagged with the ADR id, simple "must never appear" rules use `grep`
- Every test that secures an ADR carries its id: `[Trait("ADR", "ADR-0xx")]`, so `dotnet test --filter "ADR=ADR-0xx"` runs exactly those tests
- `why` gives the reason in one line, for cases the rules do not cover
- Creating, changing or superseding an ADR is a human decision; agents may propose one with status `Proposed`
- Project layout assumed by `applies_to`: `src/ThoughtTree.Domain`, `src/ThoughtTree.Contracts`, `src/ThoughtTree.Client`, `src/ThoughtTree.Server`, `tests/…`, stored test data in `tests/data/formats`
