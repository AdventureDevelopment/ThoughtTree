# Next work

The queue of work items, first item next. It only orders the work and points to where it is decided: the step order in
`docs/architecture.md` section 9 ("Release 1 scope", decision 7, "Order of foundation work"), the open findings in `docs/reviews/`, the open
decisions in `docs/architecture.md` section 9. When an item is done, remove it; add new work you find, with its source
(AGENTS.md section 4).

1. **Decide on spike 2's results** (`docs/spikes/spike-2-report.md`, desktop measured 2026-11-28; ADR-091 bet card).
   Desktop: (2) +1 %, (3) +5 % against (1); phone and tablet estimated from spike 1 at +1 to +9 % and +5 to +13 %, inside
   the decision rule. For the owner: accept the estimate or run 3 rounds on the Fairphone 5 in Chrome (report section 5);
   layout (2) or (3) (the report recommends (3): erasing 3 to 4 times cheaper); the cost of padding blobs to ADR-087's
   buckets (stored size +30 %, text +310 %, sync batch +53 %); whether Safari must be measured.
2. **Spike 3: signed events** (owner, 2026-11-28; open decision "Identity and signed events" in `docs/architecture.md`
   section 9). Can run alongside spike 2, reusing spike 1's harness. Measure on the owner's phone and tablet (Chrome,
   Firefox, Safari): (1) Web Crypto support for Ed25519 and ECDSA P-256 with a non-extractable private key; (2) replay of
   100,000 events with a signature check per event and per content blob, against spike 1's baseline; (3) signing a sync
   batch of 1,000 events; (4) the pairing code with a single-use, expiring vouching token for the new device, against the QR size of ADR-087.
   The agent builds the spike and a desktop run; the device runs need the owner.
3. **Rest of the clean-code review** (`docs/reviews/2026-11-26-clean-code.md`): its refactoring order is done except
   H1, which waits for "Decide where doc comments stay". Left outside that order, not protected: T4 (one naming form for securing tests), H3 (one
   public type per file), H4 (spec conventions as a rule); each sets a convention, so the owner answers its questions
   first.
4. **Open findings for the next spec and property change** (`docs/reviews/2026-11-26-domain-core.md`, rows marked "Next
   spec change" and "Next property change"), together with S3 to S7 of `docs/reviews/2026-11-26-clean-code.md` (one
   spec helper, one tree renderer, scenarios in the class of their topic, the snapshot class name, Given/When/Then in
   the INV-11 specs; owner, 2026-11-26). Spec changes need the owner's approval (ADR-035).
5. **Rest of foundation step 2a**: the payload wrapper and INV-9 to INV-14, INV-13 first (same event id with different
   content), after the open decisions "Archive, content and erasure" (spike 2), which changes what "content" means, and "Identity
   and signed events" (spike 3), which adds a signature to the wrapper;
   `docs/architecture.md` section 9 ("Release 1 scope", decision 7) and the invariant catalogue in section 8.
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
8. **Sketch: archived trees page** (owner, 2026-11-28; open decision "Archive, content and erasure" in
   `docs/architecture.md` section 9): More › Trees › Archived trees…, with restoring a tree (ADR-064). Before release 1,
   not needed for the first preview deployment.
9. **Sketch: restore view** (open decision "Archive, content and erasure" in
   `docs/architecture.md` section 9, ADR-091; found by the independent review, 2026-11-28): the list of archived items
   with Restore and Erase permanently (ADR-064). Before release 1, not needed for the first preview deployment; the question
   before erasing needs no sketch (owner, 2026-11-28).
