# ADR-035: Protected test harness

id: ADR-035 · status: Accepted

applies_to: `tests/**`, `.github/**`

- **MUST NOT** change or delete stored test data in `tests/data/formats`; it is append-only
- **MUST NOT** change the reference model (`tests/ThoughtTree.Domain.Model`) or the invariant catalogue without human approval
- **MUST NOT** delete gate tests or weaken assertions without human approval
- **MUST** tag every test that secures an ADR with its id (`[Trait("ADR", "ADR-0xx")]`)
- **MUST** run the whole gate suite without Azure accounts, secrets or network
- **MUST** make every test deterministic; report seed and minimal counterexample on failure
- **SHOULD** add tests freely

verify:

```bash
git diff --diff-filter=DM origin/main -- tests/data/formats | wc -l   # expect 0
git diff origin/main -- tests/ThoughtTree.Domain.Model | wc -l   # expect 0 unless approved
```

why: The harness is the specification agents work towards; weakening it hides defects.
