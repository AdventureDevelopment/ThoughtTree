# Next work

The queue of work items, first item next. It only orders the work and points to where it is decided: the step order in
`docs/architecture.md` section 9 ("Release 1 scope", decision 7, "Order of foundation work"), the open findings in `docs/reviews/`, the open
decisions in `docs/architecture.md` section 9. When an item is done, remove it; add new work you find, with its source
(AGENTS.md section 4).

1. **Rest of the clean-code review** (`docs/reviews/2026-11-26-clean-code.md`): its refactoring order is done except
   H1, which waits for item 5. Left outside that order, not protected: C10 (`Tree.Anomalies` sorts on every read), T4
   (one naming form for securing tests), T7 (the hard-case counter), H3 (one public type per file), H4 (spec
   conventions as a rule). Needs the owner first: T9 (which ADR, if any, the printing rule secures).
2. **Open findings for the next spec and property change** (`docs/reviews/2026-11-26-domain-core.md`, rows marked "Next
   spec change" and "Next property change"), together with S3 to S7 of `docs/reviews/2026-11-26-clean-code.md` (one
   spec helper, one tree renderer, scenarios in the class of their topic, the snapshot class name, Given/When/Then in
   the INV-11 specs; owner, 2026-11-26). Spec changes need the owner's approval (ADR-035).
3. **Rest of foundation step 2a**: the payload wrapper and INV-9 to INV-14, INV-13 first (same event id with different
   content); `docs/architecture.md` section 9 ("Release 1 scope", decision 7) and the invariant catalogue in section 8.
4. **Flaky growth budget** (found with INV-11, 2026-11-26): `PF6_a_sync_batch_of_late_events_grows_about_linearly` in
   `tests/secure/ThoughtTree.Domain.Budgets` fails in about half of the runs in a cloud worker container, also on commit
   `8741775` before INV-11; make the ratio check stable without weakening it (ADR-035).
5. **Decide where doc comments stay** (owner, 2026-11-26, after the clean-code review): the owner wants code readable at
   first glance and finds summaries clutter. Open: summaries in spec classes, secure tests and `src/`; what carries the
   ADR and INV links instead (traits, an attribute, `applies_to`); timing open. The clean-code review's H1 (summary
   check) and S2 (spec summaries) depend on it.
6. **One approved sweep over the protected test files** (owner, 2026-11-26): write out the one-letter names in
   `tests/specify/**` and the reference model (the files left in `eng/pending-renames.txt`, AGENTS.md section 4b), and
   turn `Item.Deleted` into the state `Lifecycle { Live, Deleted }` with `Deleted()` and `Restored()` instead of
   `WithDeleted(bool)` (AGENTS.md section 4b; its line in `eng/approved-primitives.txt` goes with it). In the reference
   model also (owner, 2026-11-26): `sb` written out, `IdOrNone` without `id.Value.Value`, `Quote` without its unreachable
   `null` branch, the `Report` field `Item` named `ItemId`; better names only, no restructuring towards the Domain's
   code, so the referee stays independent (ADR-035). The owner can
   imagine `Archived` as a further `Lifecycle` value: that needs its own events and an ADR first. Needs the owner's
   approval before it starts (ADR-035).
