# ADR-070: Feedback offered where the user is, with the place it came from

id: ADR-070 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

Extends ADR-051 (Accepted, unchanged); proposed by the UX concept (decision D-123).

- **MUST** ask the user once whether they want to give feedback from inside the app (for example at the end of onboarding); the answer is device state and can be changed any time in More › Settings ("Offer feedback": on or off)
- **MUST**, while the setting is on, end every menu and sheet with a quiet line "Something not right? Tell us", except the capture sheet, which holds only writing
- **MUST**, while the setting is on, show one small, unobtrusive feedback button at the top of the screen; it never covers content and is not part of the bottom bar
- **MUST** add to the report's diagnostics block the place the report was started from, as a fixed identifier from a closed list (for example `item-menu`, `move-to`, `tags`, `theme`, `history`, `links`, `from-template`, `more`, `trees`, `settings`, `top-button`, `error`); never titles, search text or any other content
- **MUST** keep all other rules of ADR-051: the report is shown before sending and can be edited or cancelled, nothing is sent automatically
- **MUST** still offer More › Send feedback when the setting is off, so feedback stays possible for everyone

verify:

```bash
dotnet test --filter "ADR=ADR-070"   # origin is from the closed list; no entry or button while the setting is off
```

why: Feedback is the project's only signal from users; it has to be one tap away at the moment something annoys them, and knowing the place makes a report useful without carrying any content.
