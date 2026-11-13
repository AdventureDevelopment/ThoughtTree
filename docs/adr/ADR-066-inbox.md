# ADR-066: The inbox is a marked item, always last

id: ADR-066 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** create the inbox as an ordinary item when a tree is created and mark it with `tree.inbox-set`
- **MUST** give a new inbox the predefined theme "Inbox" (fixed id) by an ordinary `theme.set`, changeable like any theme
- **MUST** create captured thoughts inside the inbox
- **MUST** show the inbox last at the top level, whatever its stored position; "top level, last" places items before it
- **MUST** let the inbox expand, be focused, themed and tagged like any branch; **MUST NOT** let it be moved or deleted
- **MUST** skip the inbox when taking the tree name from the first visible top-level item
- **MUST** keep one inbox after merging trees: the marking that comes first in the agreed order wins; the other inbox becomes an ordinary item, and the app offers to move its contents
- **MUST** offer the inbox's full item menu from the global menu (More › Inbox)

verify:

```bash
dotnet test --filter "ADR=ADR-066"
```

why: An inbox that behaves like a branch can be expanded and sorted by drag and drop; last at the top level keeps the tree's own content first (prototype feedback, UX decisions D-1, D-5).
