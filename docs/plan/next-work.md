# Next work

The queue of work items, first item next. It only orders the work and points to where it is decided: the step order in
`docs/architecture.md` section 9 ("Release 1 scope", decision 7, "Order of foundation work"), the open findings in `docs/reviews/`, the open
decisions in `docs/architecture.md` section 9. When an item is done, remove it; add new work you find, with its source
(AGENTS.md section 4).

1. **INV-11 text normalisation** (ADR-029, foundation step 2a). Spec first. Includes the owner decision whether titles and
   notes get a text type (they are listed in `eng/approved-primitives.txt`). Done when the specs, the properties and the
   reference model rules cover INV-11 and the approved-primitives entries for titles and notes are resolved.
   Decided by the owner 2026-11-26 ("Text normalisation details (INV-11)", `docs/architecture.md` section 9).
2. **Open findings for the next spec and property change** (`docs/reviews/2026-11-26-domain-core.md`, rows marked "Next
   spec change" and "Next property change"). Spec changes need the owner's approval (ADR-035).
3. **Rest of foundation step 2a**: the payload wrapper and INV-9 to INV-14, INV-13 first (same event id with different
   content); `docs/architecture.md` section 9 ("Release 1 scope", decision 7) and the invariant catalogue in section 8.
