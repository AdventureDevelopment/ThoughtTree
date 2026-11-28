# Reference model: rule list (approved by the owner 2026-11-26)

The reference model replays a log by exactly these rules and nothing else. Each rule names its source. Malformed ids never reach it: they are rejected at intake (card 6), so it assumes well-formed ids; an empty parent or after means "none" (architecture.md, event table).

## Order and identity

1. Of several events with the same event id, the first in input order is kept (card 14; INV-2, INV-13).
2. Events replay sorted by clock, then event id as text, ordinal (ADR-014).
3. Events with equal clocks all apply in that order; the tie is not reported (ADR-014, card 8).

## Items

4. `item.created` makes a new item with its title and note; a missing (null) title or note starts as empty text (card 17).
5. A second creation of an existing item id is not applied and is reported (card 1).
6. A title, note, move, delete or restore event for an item that does not exist at that point is not applied and is reported (card 2).
7. A title or note change sets the value; a missing (null) value changes nothing (card 17, ADR-016).
8. A delete marks the item deleted, a restore clears its own mark; deleting a deleted item or restoring a live one changes nothing and is not reported (ADR-005, card 16).
9. Changes to deleted items apply as usual; an item under a deleted ancestor is hidden, also after its own restore, until the ancestor is restored (card 16, card 10, architecture.md "in a deleted part").

## Placement

10. An item is placed directly after the sibling its `afterItemId` names, or first under its parent when `afterItemId` is empty (ADR-004 rule 1).
11. If that sibling is under another parent at that point, the item goes first; deleted siblings still count as siblings (ADR-004 rules 2 and 3).
12. A new item whose parent does not exist at that point goes to top level and is reported (ADR-004 rule 5).
13. A new item whose `afterItemId` does not exist or is the item itself goes first under its parent and is reported (ADR-004 rule 5).
14. A move whose parent or `afterItemId` does not exist at that point, or whose `afterItemId` is the item itself, is not applied and is reported (ADR-004 rule 4).
15. A move that would put the item under itself or one of its descendants is not applied and is reported (ADR-023 rule 3).

## Unknown event types

16. An event of a type the model does not know is skipped and reported as "unknown event type" (ADR-016, card 15).

## Reports

17. Every report names its kind, its event and, where known, its item.
18. Reports are grouped by item; reports without a known item form their own group (card 9).
19. Groups are ordered by item id, text ordinal, the group without item last; inside a group, reports follow the agreed event order.

## Details (confirmed by the owner 2026-11-26)

20. A move that fails several checks gets one report, from the first failing check in this order: unknown item, unknown parent, unknown `afterItemId` (including the item itself), cycle.
21. A new item with both an unknown parent and an unknown `afterItemId` gets both reports, unknown parent first.
22. A repeated event id (rule 1) is dropped without a report; intake reports a different content under a known id (INV-13).
23. A deleted item counts as existing when it is named as parent or `afterItemId` (ADR-004, ADR-005 tombstones).
24. A move to the place the item already has is applied, without a report.
25. A new item that names itself as its parent has an unknown parent (rule 12).

## Text (owner decisions 2026-11-26, INV-11)

26. Every title and note is replayed in Unicode NFC; in a title each line break becomes one space, in a note one line feed. The line breaks are CR LF (counted as one), CR, LF, U+0085, U+2028, U+2029, vertical tab and form feed (ADR-029; architecture.md section 9, "Text normalisation details (INV-11)").
27. Before rule 26, each unpaired UTF-16 surrogate in a title or note becomes U+FFFD; text is never rejected for it (owner 2026-11-26; architecture.md section 9, "Unpaired surrogates in text").

## Not in the model

- Clock stamping (ADR-014, INV-8): checked by its own property, not by replay.
- Checkpoints and rewinding (card 12): how the Domain gets the tree, not what the tree is; INV-6 and INV-7 compare it with the model.
- Rejecting malformed ids (card 6): intake, not replay.
