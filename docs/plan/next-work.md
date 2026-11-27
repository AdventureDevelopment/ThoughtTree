# Next work

The queue of work items, first item next. It only orders the work and points to where it is decided: the step order in
`docs/architecture.md` section 9 ("Release 1 scope", decision 7, "Order of foundation work"), the open findings in `docs/reviews/`, the open
decisions in `docs/architecture.md` section 9. When an item is done, remove it; add new work you find, with its source
(AGENTS.md section 4).

1. **Spike 2: content blobs and replay speed** (owner, 2026-11-27; open decision "Archive, content and erasure" in
   `docs/architecture.md` section 9). In `spikes/`, reusing spike 1's harness, 100,000 events on the owner's phone and
   tablet (Chrome, Firefox, Safari), measure: (1) today's format as baseline, replay from local blocks (ADR-086); (2)
   events and content blobs, local blocks carrying the text; (3) separate event and blob blocks joined in replay; (4)
   structure first, text of visible items on demand (time until the tree shows, until text shows); (5) erasing one
   item: delete its blobs and rebuild the affected blocks; (6) writing a sync batch of 1,000 events with and without
   blobs. Decision rule, set before measuring: blobs go ahead if (2) or (3) stays within about 10 to 20 % of (1) on the
   phone; otherwise (4) decides, as a UX question for the owner. The agent builds the spike and a desktop run; the
   device runs need the owner.
2. **Rest of the clean-code review** (`docs/reviews/2026-11-26-clean-code.md`): its refactoring order is done except
   H1, which waits for "Decide where doc comments stay". Left outside that order, not protected: T4 (one naming form for securing tests), H3 (one
   public type per file), H4 (spec conventions as a rule); each sets a convention, so the owner answers its questions
   first.
3. **Open findings for the next spec and property change** (`docs/reviews/2026-11-26-domain-core.md`, rows marked "Next
   spec change" and "Next property change"), together with S3 to S7 of `docs/reviews/2026-11-26-clean-code.md` (one
   spec helper, one tree renderer, scenarios in the class of their topic, the snapshot class name, Given/When/Then in
   the INV-11 specs; owner, 2026-11-26). Spec changes need the owner's approval (ADR-035).
4. **Rest of foundation step 2a**: the payload wrapper and INV-9 to INV-14, INV-13 first (same event id with different
   content), after the open decision "Archive, content and erasure" (spike 2), which changes what "content" means;
   `docs/architecture.md` section 9 ("Release 1 scope", decision 7) and the invariant catalogue in section 8.
5. **Flaky growth budget** (found with INV-11, 2026-11-26): `PF6_a_sync_batch_of_late_events_grows_about_linearly` in
   `tests/secure/ThoughtTree.Domain.Budgets` fails in about half of the runs in a cloud worker container, also on commit
   `8741775` before INV-11; make the ratio check stable without weakening it (ADR-035).
6. **Decide where doc comments stay** (owner, 2026-11-26, after the clean-code review): the owner wants code readable at
   first glance and finds summaries clutter. Open: summaries in spec classes, secure tests and `src/`; what carries the
   ADR and INV links instead (traits, an attribute, `applies_to`); timing open. The clean-code review's H1 (summary
   check) and S2 (spec summaries) depend on it.
7. **One approved sweep over the protected test files** (owner, 2026-11-26): write out the one-letter names in
   `tests/specify/**` and the reference model (the files left in `eng/pending-renames.txt`, AGENTS.md section 4b), and
   turn `Item.Deleted` into the state `Lifecycle { Live, Archived }` (owner, 2026-11-27; the event types are renamed
   with the ADR of the open decision "Archive, content and erasure"), changed through one method per value instead of
   `WithDeleted(bool)` (AGENTS.md section 4b; its line in `eng/approved-primitives.txt` goes with it). In the reference
   model also (owner, 2026-11-26): `sb` written out, `IdOrNone` without `id.Value.Value`, `Quote` without its unreachable
   `null` branch, the `Report` field `Item` named `ItemId`; better names only, no restructuring towards the Domain's
   code, so the referee stays independent (ADR-035). Needs the owner's approval before it starts (ADR-035).
