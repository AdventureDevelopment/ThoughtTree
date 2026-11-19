# ADR-018: Conflicts are visible and resolved by the user

id: ADR-018 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

Amended by ADR-077 (`relation.note-changed` carries `replaces`; relation notes are a kind of conflict), ADR-083 (`relation.type-changed` carries `replaces`) and ADR-084 (`relation-type.set` replaces `relation-type.defined` and `display-name.set` in the list; display names leave the kinds of conflicts); owner decisions 2026-11-17 to 2026-11-19.

- **MUST** let exactly these events name the changes they replace (`replaces`): `item.title-changed`, `item.note-changed`, `item.moved`, `relation-type.defined`, `display-name.set`, `tree.feature-changed`, `tree.server-changed`, `view.set` (amended by ADR-077, ADR-083, ADR-084: `relation.note-changed`, `relation.type-changed` and `relation-type.set` are in the list; `relation-type.defined` and `display-name.set` are not events of the format)
- **MUST NOT** treat concurrent changes with the same value as a conflict
- **MUST** list all kinds of conflicts (items, relation types, display names, tree features, server address, views) in the Conflict View (amended by ADR-077, ADR-083, ADR-084: relation notes and a relation's type are kinds of conflicts; display names are not)
- **MUST** let `item.deleted` carry `seen`, the highest clock received per device
- **MUST** derive conflicts from the log; MUST NOT store them
- **MUST** resolve a conflict by an ordinary event that replaces all competing changes

verify:

```bash
dotnet test --filter "ADR=ADR-018"
```

why: Last-writer-wins can still pick the wrong value; the user sees and fixes it.
