# Review of the Domain core (foundation step 2a), 2026-11-25 to 2026-11-26

TL;DR: an independent five-lens review (correctness against the ADRs, test strength, design, maintainability, history), each finding checked by a skeptic agent, found 64 real issues. 38 are fixed, 7 are settled by owner decisions, 19 are open and listed below with the step that takes them. The owner re-decided every behaviour-defining rule from neutral decision cards; the reference model was then rewritten from the approved rule list by an agent that never saw the Domain code.

## How the review ran

- Five reviewer agents, one lens each, saw only the code, the ADRs and AGENTS.md, not the reasoning of the sessions that built the code. A skeptic agent per lens tried to refute each finding; one was refuted.
- Stryker.NET measured the tests: 82.7 % before, 94.3 % on changed Domain code after.
- A fresh agent wrote 17 neutral decision cards (options with consequences and risks, no recommendation) from the ADRs and the code. The owner decided each card; see the record below.

## Open findings

| Finding | What is open | Taken by |
| --- | --- | --- |
| test-strength/L5, adr-correctness/L7, history/L9 | The tie spec checks the order on the comparer, not on the tree; a spec where the device with the lower device id has the higher event id, asserted on the replayed tree, is missing | Next spec change |
| maintainability/L9 | `Tree.ToOutline` is a test-only rendering in the Domain API (listed in `eng/approved-primitives.txt`); move it to the spec helpers | Next spec change |
| test-strength/L13 | Some specs name code types or test mechanisms in step texts, or have empty Given or When steps (AGENTS.md section 4a) | Next spec change |
| test-strength/L12 | The "compared as text, ordinal" spec does not tell ordinal from culture-aware or case-insensitive comparison | Next spec change |
| maintainability/L8 | The INV-2 property duplicates only the first eight events, mostly creations | Next property change |
| test-strength/L11, adr-correctness/L10 | The generator never produces negative clocks or cross-device ties at the largest clock; malformed ids (empty, not lowercase GUID text) are still accepted by the projection until intake rejects them (card 6) | INV-13 and intake (step 3) |
| maintainability/L1 | The spec helper `Log` builds event ids from device and count, so two logs of one device in a scenario collide | Next spec change |
| adr-correctness/L12, design/L8 | `Tree` and `Item` are records whose generated equality compares the persistent collections by reference, so equal trees can compare unequal | Before the application layer compares trees (step 4) |
| design/L12 | `VisibleOutline` recurses once per tree level; very deep trees could exhaust the stack | Before the client shell (step 4) |
| design/L7 | No API gives the highest clock in the local log, which every new event needs (ADR-014) | Command logic |
| design/L4, design/L5 | Persisted snapshots (the projector holds every applied event and id) and the real cost of the immutable replay on a phone | Client shell (step 4), measured on devices |
| test-strength/L7 | No stored regression data pins the replay order, although ADR-014 forbids changing it in later versions | Formats and regression seeds (step 3) |
| adr-correctness/L3, test-strength/L8 | The warning for clocks far ahead (ADR-014) is not built; its threshold is open | Sync step; open decisions "Protection against far-ahead clocks" and "Copied device ids" in architecture.md section 9 |

Also open from the owner decisions: the text type for titles and notes (decided with INV-11, entries in `eng/approved-primitives.txt`), a glossary term and a sketch for the reports (architecture.md section 9, "Reports of events the projection could not apply").

## Settled by owner decisions

- history/L1, L4, L5, L8, L10 (broken intermediate commit `fc9b580`, commit labels, spec-first order, subject length): the history stays as it is; the pull request is squash-merged (owner, 2026-11-26).
- adr-correctness/L11 (restore under a deleted ancestor): keep architecture.md section 5, "in a deleted part" (card 10).
- maintainability/L6 (tie scenarios assert that nothing is reported): matches ADR-014, "MUST NOT report an event for the tie alone" (card 8).

## Fixed

adr-correctness/L1, L2, L4, L5, L6, L8, L9, L13; test-strength/L1, L2, L3, L4, L6, L9, L10; design/L1, L2, L3, L6, L9, L10, L11; maintainability/L2, L3, L4, L5, L7, L10, L11, L12, L13, L15; history/L2, L3, L6, L7, L11, L12. The commits on the branch name the finding each one fixes.

## Owner decisions from the decision cards (2026-11-26)

Each card gave the options with their consequences and risks, without a recommendation. The rules that follow from them are in `tests/secure/ThoughtTree.Domain.Model/RULES.md` (rules 1 to 25), ADR-004 rules 4 and 5, ADR-023 rule 3 and ADR-014.

1. A second creation of an existing item id is not applied and is reported. Rejected: applying it as changes, because anyone holding the key could then overwrite an item without a visible conflict.
2. A change to an item that does not exist at that point is not applied and is reported. Rejected: holding it until the creation arrives, because pre-written changes could then override the creator.
3. A move to a parent that does not exist is not applied (the item stays) and is reported; a creation with an unknown parent goes to top level and is reported. In normal sync this heals on rebuild; the report is short-lived.
4. A move into the item's own subtree is not applied and is reported (ADR-023 rule 3 changed; was: top level).
5. A move whose afterItemId does not exist or is the item itself is not applied and is reported; a creation with such an afterItemId goes first and is reported.
6. Malformed ids are rejected at intake, never stored or synced on, and reported without content. Every later version must reject exactly the same.
7. The clock stays one number with the event id as tie-break (ADR-014); no bound at a far-future clock. Protection against far-ahead clocks is an open decision for the conflicts step.
8. Equal clocks from one device are applied silently (ADR-014); copied device ids are detected at the sync step.
9. Reports are shown as a calm warning for the tree (D-128) once sync has settled, listed grouped by item.
10. Restoring an item under a deleted ancestor keeps it hidden until the ancestor is restored (architecture.md section 5).
11. `Tree` and `Item` stay immutable persistent values (AGENTS.md section 4b).
12. A late event rewinds the projector to a checkpoint instead of a full rebuild.
13. The build-enforced code style rules stay.
14. If two events with the same event id but different content both reach the projection, the first arrived wins; intake prevents it (INV-13).
15. Events of an unknown type are skipped and reported under their own kind (ADR-016).
16. Changes to deleted items, and creations or moves under a deleted parent, are applied and stay hidden; delete conflicts follow with ADR-018.
17. A missing title or note changes nothing; a creation without one starts with empty text (ADR-016).

## What changed in the process

- AGENTS.md: a refactoring step before every commit, a rule for own types instead of bare values, a stricter comment rule, the reusable-setup trigger.
- Automatic checks: `CommentRules`, `PrimitiveRules`, `ReusableSetupRules`, mutation tests in the push gate, complexity checks at 1,000, 10,000 and 100,000 events, deterministic property tests.
