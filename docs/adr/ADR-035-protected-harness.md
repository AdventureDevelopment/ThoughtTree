# ADR-035: Protected test harness

id: ADR-035 · status: Accepted · kind: constraint

applies_to: `tests/**`, `.github/**`

- **MUST NOT** change or delete stored test data in `tests/data/formats` or regression seeds in `tests/data/regression-seeds`; both are append-only
- **MUST NOT** change the reference model (`tests/secure/ThoughtTree.Domain.Model`) or the invariant catalogue without human approval
- **MUST NOT** accept new or changed snapshot files (`*.verified.*`) automatically, in CI or by an agent; changes need human approval
- **MUST NOT** change or delete specs in `tests/specify` without human approval
- **MUST** bring every change to `main` through a pull request; branch protection forbids direct pushes and requires the push gate
- **MUST** require the owner's review (CODEOWNERS) for changes to `tests/specify/**`, `tests/data/**`, the reference model, `*.verified.*` and `docs/**`
- **MUST NOT** delete gate tests or weaken assertions without human approval
- **MUST** tag every test that secures an ADR with its id (`[Trait("ADR", "ADR-0xx")]`)
- **MUST** run the whole gate suite without Azure accounts, secrets or network
- **MUST** make every test deterministic; report seed and minimal counterexample on failure
- **SHOULD** add tests freely

verify:

```bash
git diff --diff-filter=DM origin/main...HEAD -- tests/data/formats tests/data/regression-seeds | wc -l   # expect 0; runs on every pull request against its base
git diff origin/main...HEAD -- tests/secure/ThoughtTree.Domain.Model | wc -l   # expect 0 unless approved
git diff --diff-filter=DM origin/main...HEAD -- '*.verified.*' | wc -l   # expect 0 unless approved
dotnet test --filter "ADR=ADR-035"   # reports removed or renamed test methods and changed assertions in the pull request
```

why: The harness is the specification agents work towards; weakening it hides defects.
