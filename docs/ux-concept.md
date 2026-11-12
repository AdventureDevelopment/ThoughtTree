# ThoughtTree – UX Concept

Status: outline, to be worked out step by step. Linked from section 8 of [architecture.md](architecture.md); UI specifications refer to this document.

## Approach

- First the topics that are foundation points (expensive to change later), then the topics that are refined while features are built
- Topics marked "partly" are decided only as far as they affect the architecture

## Topics

| # | Topic | Foundation? | Why |
| --- | --- | --- | --- |
| 1 | UX principles (for example: capture first, calm, honest about privacy, accessible, touch first) | Yes | Guide every later decision, including the agents' |
| 2 | Undo | Yes | Undo on an event log writes new events; its scope (per device, per session, across sync) affects Domain and the application layer |
| 3 | Component library: own components or a library (for example MudBlazor, Fluent UI Blazor) | Yes | Affects accessibility, download size, the Content Security Policy, direction-neutral layout and the look of everything; hard to switch later |
| 4 | Design tokens and theming: colours, typography, spacing, light and dark mode | Yes | Used by every component; contrast tests build on them |
| 5 | Layout model per screen size: phone (one column), tablet (outline and detail side by side?), desktop | Partly | Decides how navigation and state are modelled in the application layer |
| 6 | Touch interaction for the outliner: gestures, toolbar above the keyboard, drag and drop | Partly | Conflicts with system gestures (for example the back swipe from the screen edge on iPhone); principles must be settled before the specs are written |
| 7 | Information architecture: trees, outline, node detail (note, relations, backlinks, history), search, conflicts, settings, feedback | Partly | Navigation structure and the application layer's state |
| 8 | States and feedback: offline, sync status, unsynced changes, conflicts, warnings, errors | Partly | One consistent pattern for all states, used by every feature |
| 9 | Onboarding: first setup, privacy explanation, pairing flow | No | Feature work |
| 10 | Microcopy and tone | No | Feature work, guided by the principles |
| 11 | UX with agents: UI specifications as the UX specification, prototypes, reviews | Yes | Determines how UX intent reaches the agents |

## Order

Worked out in detail now: 1, 2, 3, 4, 11. Only the foundation parts of 5 to 8. Topics 9 and 10 later.

## How this concept is worked out

- Worked out interactively with the owner, one topic and one decision at a time, following the UX workflow of the BMAD Method (UX designer role); BMAD's split is kept as sections of this document: how it looks (visual identity, tokens) and how it works (information architecture, behaviour, states, flows)
- Principles are derived from journeys: real moments told by the owner, structured into numbered steps with a key moment
- Every agreed decision is recorded in the decision log below before the next topic starts
- Decisions that would change the architecture or an ADR are proposed as new ADRs with status `Proposed` in [adr/](adr/)

## Journeys

### Journey A – The idea on waking

The owner wakes up in bed, still half asleep; an idea comes up that must not be lost. Phone, probably one hand.

1. Opens ThoughtTree from the home screen; it shows the place last worked on, with a clearly visible capture button (decision D-4)
2. Taps the capture button: the inbox opens with a fresh, empty line
3. Types the title right away, often starting with an emoji
4. **Key moment:** the idea is saved; from now on nothing can be lost
5. Optionally adds context: more text (the note) and a tag
6. If it is a chain of ideas, enters the next idea right away, without detours
7. Where the idea belongs in the tree is decided later, not now

Notes:

- The idea lands in an inbox (decision D-1); no decision about its place is needed before typing
- The next idea of a chain is a sibling; a space typed into its empty title nests it under the previous idea (decision D-2)
- Backspace in an empty title moves it one level up; at the top level of the inbox it removes the empty line (decision D-3)
- Tags are planned in the architecture only as a later tree feature (section 8, switches); this journey is a wish for them
- Emojis in titles are already supported (section 8, text)
- Design target U1 (new node within two taps of opening the app) applies directly

## Decision log

| # | Date | Topic | Decision | Source |
| --- | --- | --- | --- | --- |
| D-1 | 2026-11-12 | 1 Principles (journey A) | A quickly captured idea lands in an inbox, to be sorted into the tree later; the user does not choose a place before typing | Journey A |
| D-2 | 2026-11-12 | 1 Principles (journey A) | In a chain of ideas the next idea is a sibling by default; typing a space as the first character of an empty title nests it under the previous idea (as in MLO); accessible alternatives: a menu command, and Tab on a keyboard (ADR-033) | Journey A |
| D-3 | 2026-11-12 | 1 Principles (journey A) | Backspace in an empty title moves the idea one level up; only at the top level (of the inbox or the current zoom) does it remove the empty line; accessible alternatives: a menu command, and Shift+Tab on a keyboard (ADR-033) | Journey A |
| D-4 | 2026-11-12 | 1 Principles (journey A) | On opening, the app shows the place last worked on (device state, ADR-013) together with a clearly visible capture button; one tap on it opens the inbox with a fresh, empty line, so typing starts after one tap (within U1) | Journey A |
