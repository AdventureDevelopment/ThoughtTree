# ADR-067: One action, one undo step

id: ADR-067 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Contracts/**`

- **MUST** let every event carry an action id (`actionId`) that is shared by all events written for one user action (for example a bulk move of several items, pasting indented text as several items, duplicating a branch, a template); a single-event action gets its own action id
- **MUST** let quick undo (ADR-059) revert all events of the most recent action of this device together, as one step, with one undo event per reverted event (each naming it in `reverts`)
- **MUST** show and announce the undone action as one change (for example "Undone: moved 7 items to 'Ideas'"), following ADR-059's rule that undo shows what it reverts
- **MUST** treat an action as stopped by ADR-059's rule as a whole: if any of its items was changed since by another device or user, quick undo stops and offers the history
- **MUST** add `actionId` to the event format before the first release, so no stored event lacks it

verify:

```bash
dotnet test --filter "ADR=ADR-067"
```

why: Bulk actions on touch (multi-select, paste as items) write many events at once; undoing them one by one would take as many taps as items (UX decision D-83, outliner comparison); the field is cheap now and hard to add once events are stored.
