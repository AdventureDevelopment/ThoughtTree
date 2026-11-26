# Next work

The queue of work items, first item next. It only orders the work and points to where it is decided: the step order in
`docs/architecture.md` section 9 ("Release 1 scope", decision 7, "Order of foundation work"), the open findings in `docs/reviews/`, the open
decisions in `docs/architecture.md` section 9. When an item is done, remove it; add new work you find, with its source
(AGENTS.md section 4).

1. **Open findings for the next spec and property change** (`docs/reviews/2026-11-26-domain-core.md`, rows marked "Next
   spec change" and "Next property change"). Spec changes need the owner's approval (ADR-035).
2. **Rest of foundation step 2a**: the payload wrapper and INV-9 to INV-14, INV-13 first (same event id with different
   content); `docs/architecture.md` section 9 ("Release 1 scope", decision 7) and the invariant catalogue in section 8.
3. **Flaky growth budget** (found with INV-11, 2026-11-26): `PF6_a_sync_batch_of_late_events_grows_about_linearly` in
   `tests/secure/ThoughtTree.Domain.Budgets` fails in about half of the runs in a cloud worker container, also on commit
   `8741775` before INV-11; make the ratio check stable without weakening it (ADR-035).
