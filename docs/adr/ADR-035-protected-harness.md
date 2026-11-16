# ADR-035: Protected test harness

id: ADR-035 · status: Accepted · kind: constraint

applies_to: `tests/**`, `.github/**`

- **MUST NOT** change or delete stored test data in `tests/data/formats` or regression seeds in `tests/data/regression-seeds`; both are append-only
- **MUST NOT** change the reference model (`tests/secure/ThoughtTree.Domain.Model`) or the invariant catalogue without human approval
- **MUST NOT** accept new or changed snapshot files (`*.verified.*`) automatically, in CI or by an agent; changes need human approval
- **MUST NOT** change or delete specs in `tests/specify` without human approval
- **MUST** bring every change to `main` through a pull request; branch protection forbids direct pushes and requires the push gate
- **MUST** require the owner's review (CODEOWNERS) for changes to `tests/specify/**`, `tests/data/**`, the reference model, `*.verified.*`, `docs/**` and `.github/CODEOWNERS`, and for every change to the reviewer workflow and prompt (`.github/workflows/agent-review*`, `.github/agent-review/**`), the branch protection settings, anything holding secrets and the ADRs (`docs/adr/**`, human decision); an approval counts only for the latest push (new push, new approval)
- **MAY** let agents change `AGENTS.md`, `CLAUDE.md` and the rest of `.github/**` (pull request template, other CI jobs) without the owner's review, if an independent reviewer agent approves the pull request as a required check; the reviewer runs from the workflow on `main`, never from the pull request, sees only the diff and the rules, and must reject any change that weakens a safeguard, contradicts an ADR or widens agent permissions; until it exists the owner reviews these files too
- **MUST NOT** delete gate tests or weaken assertions without human approval
- **MUST** tag every test that secures an ADR with its id (`[Trait("ADR", "ADR-0xx")]`)
- **MUST** run the whole gate suite without Azure accounts, secrets or network
- **MUST** make every test deterministic; report seed and minimal counterexample on failure
- **SHOULD** add tests freely

verify:

```bash
git diff --diff-filter=DM origin/main...HEAD -- tests/data/formats tests/data/regression-seeds | wc -l   # expect 0; runs on every pull request against its base
git diff origin/main...HEAD -- tests/secure/ThoughtTree.Domain.Model | wc -l   # expect 0 unless approved
git diff --diff-filter=ADM origin/main...HEAD -- '*.verified.*' | wc -l   # expect 0 unless approved
dotnet test --filter "ADR=ADR-035"   # reports removed or renamed test methods and changed assertions in the pull request
```

why: The harness is the specification agents work towards; weakening it hides defects.
