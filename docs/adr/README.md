# Architecture Decision Records

Agent-optimised ADRs: scoped, imperative, verifiable, short. One file per decision; the index is in section 9 of [architecture.md](../architecture.md).

## Format

```markdown
# ADR-0xx: <title>

id: ADR-0xx · status: Accepted | Proposed | Superseded by ADR-0yy · kind: constraint | bet ([bet card](bets.md#adr-0xx))

applies_to: `<glob>`, …

- **MUST** …
- **MUST NOT** …
- **SHOULD** …
- **MAY** …

verify:

```bash
<command>   # expected result
```

why: <one line>
```

## Conventions

- An ADR that changes another has a line under its header: `Amends ADR-0xx (what); owner decision <date>` (as ADR-073)
- `kind: constraint`: required by logic, security or earlier decisions; firm; no outcome block
- `kind: bet`: rests on an assumption that may turn out wrong; its bet card in [bets.md](bets.md) holds a one-line TL;DR, outcome, assumption, signal and review trigger, kept out of the ADR so the ADR stays short; agents treat its rules as firm until a review changes them, but may point out when a signal or review trigger has occurred
- Signals must respect privacy: no telemetry; signals come only from the owner's own use, feedback issues and their categories, performance reports, voluntary signal answers and usage summaries in the feedback form (ADR-051), server counters (counts only, no IP addresses or account ids, kept in memory or as daily totals at most, visible only to the operator), and CI numbers (gate time, size budget, mutation score, unreliable tests)
- Each signal in a bet card lists exactly the numbers it collects; adding a signal needs human approval
- Reviews are triggered by events (for example: the repository goes public, the store app exists, the first feedback on a topic arrives), not by dates

- `applies_to` names the paths the rules govern; before changing a file, read every ADR whose `applies_to` matches it
- `verify` is run by CI (see the protection rules for how changes on `main` are compared); most checks run the tests tagged with the ADR id, simple "must never appear" rules use `grep`
- Every test that secures an ADR carries its id: `[Trait("ADR", "ADR-0xx")]`, so `dotnet test --filter "ADR=ADR-0xx"` runs exactly those tests
- `why` gives the reason in one line, for cases the rules do not cover
- Creating, changing or superseding an ADR is a human decision; agents may propose one with status `Proposed`
- Project layout assumed by `applies_to` (ADR-054): `src/ThoughtTree.Domain`, `src/ThoughtTree.Contracts`, `src/ThoughtTree.Client.App`, `src/ThoughtTree.Client`, `src/ThoughtTree.Server.Core`, `src/ThoughtTree.Server`; tests under `tests/specify`, `tests/secure`, `tests/confirm`, `tests/support`; stored test data in `tests/data`
- Globs use `src/ThoughtTree.Client*/**` and `src/ThoughtTree.Server*/**` to cover both projects of a side; a CI check reports any `src/` project not covered by the ADRs of its layer
- A `verify` with `dotnet test --filter` must match at least one test. Until release 1, a zero match does not fail CI, because the code an ADR secures may not exist yet; the CI report lists every unmatched line (owner decision, 2026-11-17). The release check for release 1 fails on zero matches, except for a line marked `# when: server`
- A `verify` line runs in the push gate unless it is marked `# when: after deployment`, `# when: release` or `# when: server` (skipped until the first server release, ADR-074; the marker is then removed and the filter must match); the branch check (small set on every push to a branch) also runs every `verify` line that needs no browser, container, Azure account or network
