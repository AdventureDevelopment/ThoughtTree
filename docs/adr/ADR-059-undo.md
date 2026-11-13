# ADR-059: Undo writes new events; quick undo covers the user's own actions on this device

id: ADR-059 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** undo a change by writing ordinary new events that restore the previous state (for a delete: the restore of ADR-005); **MUST NOT** remove or alter events in the log
- **MUST** let every change be reverted from the node's history, whichever device or user made it
- **MUST** let quick undo (undo button, Ctrl+Z) step back only through the actions of the user on this device, in one sequence across all views, never through changes that arrived from other devices or users
- **MUST** keep the quick undo sequence as device state, never in the event log (ADR-013)
- **MUST** show what an undo reverses: bring the affected node into view and highlight it, or name the change with a way to go there; announce it through the live region (ADR-033)
- **MUST** decide undo in the application layer (ADR-041), so every component behaves the same

verify:

```bash
dotnet test --filter "ADR=ADR-059"
```

why: The log is never rewritten, so undo is a revert like in git; limiting quick undo to the user's own actions keeps it predictable while sync brings in other changes (UX decision D-25).

Open: redo; whether the quick undo sequence survives closing the app; how far back it reaches; what undo does when the node was changed since by another device or user.
