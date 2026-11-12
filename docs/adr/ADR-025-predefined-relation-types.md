# ADR-025: Relation types defined per tree, predefined set

id: ADR-025 · status: Accepted · kind: bet

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client/**`

- **MUST** reference relation types by id, never by name
- **MUST** define types per tree by `relation-type.defined`; write the predefined set as events when a tree is created, with fixed ids
- **MUST NOT** build relation types into the app

verify:

```bash
dotnet test --filter "ADR=ADR-025"
```

why: One mechanism for all types; the user owns every type; fixed ids avoid duplicates on merge.

outcome: (to be filled)

assumption: (to be filled)

signal: (to be filled)

review: (to be filled)
