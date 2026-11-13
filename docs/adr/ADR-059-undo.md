# ADR-059: Undo writes new events; quick undo covers the user's own actions on this device

id: ADR-059 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** undo a change by writing ordinary new events that restore the previous state (for a delete: the restore of ADR-005); **MUST NOT** remove or alter events in the log
- **MUST** let every change be reverted from the note's history, whichever device or user made it
- **MUST** let quick undo (undo button, Ctrl+Z) step back only through the actions of the user on this device, in one sequence across all views, never through changes that arrived from other devices or users
- **MUST** derive the quick undo sequence from the log: this device's own events, newest first, skipping events already reverted; **MUST NOT** keep a separate undo list
- **MUST** let an undo event name the event it reverts (`reverts`, like git's "This reverts commit …"), so the history can show it and quick undo can skip reverted events
- **MUST** show what an undo reverses: bring the affected note into view and highlight it, or name the change with a way to go there; announce it through the live region (ADR-033)
- **MUST** offer the usual text undo and redo within a draft while text is being edited, and turn the whole text change into one step of quick undo when editing ends; **MUST NOT** offer redo for other actions
- **MUST NOT** overwrite a later change from another device or user by quick undo: when the note was changed since, quick undo stops, names the later change and offers the note's history
- **MUST NOT** limit how far back quick undo reaches
- **MUST** decide undo in the application layer (ADR-041), so every component behaves the same

verify:

```bash
dotnet test --filter "ADR=ADR-059"
```

why: The log is never rewritten, so undo is a revert like in git; limiting quick undo to the user's own actions keeps it predictable while sync brings in other changes (UX decision D-25).
