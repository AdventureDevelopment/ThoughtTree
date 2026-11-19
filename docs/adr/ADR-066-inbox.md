# ADR-066: The inbox is a marked item, always last

id: ADR-066 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

Amended by ADR-082 and ADR-084 (the tree name also skips the Tags and Relation types branches); owner decision 2026-11-19.

- **MUST** create the inbox as an ordinary item when a tree is created and mark it with `tree.inbox-set`
- **MUST** give a new inbox the predefined theme "Inbox" (fixed id) by an ordinary `theme.set`, changeable like any theme
- **MUST** create captured thoughts inside the inbox
- **MUST** show the inbox last at the top level, whatever its stored position; "top level, last" places items before it
- **MUST** let the inbox expand, be focused, themed and tagged like any branch; **MUST NOT** let it be moved or deleted
- **MUST** skip the inbox when taking the tree name from the first visible top-level item (amended by ADR-082, ADR-084: also the Tags and Relation types branches)
- **MUST** let the latest `tree.inbox-set` in the agreed order win; after importing a tree into another, the importing device writes `tree.inbox-set` for the target's own inbox, the other inbox becomes an ordinary item, and the app offers to move its contents
- **MUST** offer the inbox's full item menu from the global menu (More › Inbox)

verify:

```bash
dotnet test --filter "ADR=ADR-066"
```

why: An inbox that behaves like a branch can be expanded and sorted by drag and drop; last at the top level keeps the tree's own content first (prototype feedback, UX decisions D-1, D-5).
