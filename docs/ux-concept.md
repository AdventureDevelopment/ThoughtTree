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
