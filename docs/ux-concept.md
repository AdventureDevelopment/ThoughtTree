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

## UX principles

Derived from journeys A to C (decision D-16). They guide every later decision, including the agents'.

1. **Capture first, sort later.** Nothing has to be decided before typing: not the place, not the tree, not the kind. (D-1, D-4, D-5)
2. **Create in the tree, go deeper in the detail view.** New notes are created and named in place in the tree; titles take inline Markdown (emphasis, links, inline code) and emojis. Shortcuts live in the text itself (space, Backspace, `##`, `@@`), each with a visible alternative. Longer text is written in the node's detail view. Quick capture opens a light sheet whose lines behave exactly like the outline, so typing works the same everywhere. (D-2, D-3, D-10, D-11, D-14, D-18)
3. **Stay in the flow.** After every action the user stays where they were, and the next step is right there. (D-2, D-7, D-13)
4. **Effort fits the distance.** Near by hand (drag), far by choosing (search); finding the right place must be easy. (D-6, D-9)
5. **Calm, no pressure.** No badges, no warning colours, no reminders that push. (D-8)
6. **Several dimensions.** The tree is one order; tags and relations add more. (D-13, D-14)
7. **Saved at once and reliably.** The user never wonders whether something was stored. (Journey A; quality goal reliability)
8. **Start simple.** A feature comes when use shows it is missed. (D-9, D-15)

Inherited from the architecture and applying on top: accessible (ADR-033), and honest about what is protected (section 1).

## How this concept is worked out

- Worked out interactively with the owner, one topic and one decision at a time, following the UX workflow of the BMAD Method (UX designer role); BMAD's split is kept as sections of this document: how it looks (visual identity, tokens) and how it works (information architecture, behaviour, states, flows)
- Principles are derived from journeys: real moments told by the owner, structured into numbered steps with a key moment
- Every agreed decision is recorded in the decision log below before the next topic starts
- Decisions that would change the architecture or an ADR are proposed as new ADRs with status `Proposed` in [adr/](adr/)

## Journeys

### Journey A – The idea on waking

The owner wakes up in bed, still half asleep; an idea comes up that must not be lost. Phone, probably one hand.

1. Opens ThoughtTree from the home screen; it shows the place last worked on, with the bottom toolbar and its capture button 📥 in the middle (decisions D-4, D-17)
2. Taps 📥: a light sheet slides up over the dimmed current view, with an empty line, the keyboard and the name of the target tree (decision D-18)
3. Types the title right away, often starting with an emoji
4. **Key moment:** the idea is saved; from now on nothing can be lost
5. Optionally adds context: more text (the note) and a tag
6. If it is a chain of ideas, enters the next idea right away, without detours
7. Closes the sheet and is back where they were
8. Where the idea belongs in the tree is decided later, not now

Notes:

- The idea lands in an inbox (decision D-1); no decision about its place is needed before typing
- The next idea of a chain is a sibling; a space typed into its empty title nests it under the previous idea (decision D-2)
- Backspace in an empty title moves it one level up; at the top level of the inbox it removes the empty line (decision D-3)
- The capture view shows the tree the idea goes into (the open tree); one tap switches to another tree (decision D-5)
- Inside the app, capture happens directly in the outline: a new line in the tree, typed in place; there is one way of entering text, the note is written in the node's detail view (decision D-10)
- Fast typers write `Title ## Note` in one go; the text after `##` becomes the note (decision D-11)
- The light capture sheet of MLO comes from its home-screen widget; a PWA cannot offer widgets, so this waits for the later store app or package; until then app shortcuts (press and hold the app icon: "New idea", Android and Windows only, not iPhone) are an option to consider
- Tags are added while typing with `@@` (decision D-14)
- Emojis in titles are already supported (section 8, text)
- Design target U1 (new node within two taps of opening the app) applies directly

Principle candidates heard in this journey (to be confirmed with the other journeys):

- Capture before order: writing comes first, sorting later
- No friction when half awake: no decision before typing
- Keep the flow: one idea leads to the next without detours
- Saved at once and reliably: the user never wonders whether something was stored

### Journey B – Sorting with coffee

The next morning the owner has made a coffee and drinks it in bed; awake, relaxed, with time. Phone in one hand, coffee in the other.

1. Opens the inbox with the ideas of the night
2. Sorts them one by one into the tree, choosing the way of moving by distance (decision D-6):
   - next to another note nearby: drag and drop
   - into a project the owner has in mind: "Move to…", choose the project, see what is already there, place the idea where it belongs
   - something new: its own root, or wherever it belongs
3. After each move the owner stays in the inbox; a short confirmation shows where the idea went, with Undo and Go there (decision D-7)
4. **Key moment:** the next idea belongs to the same project: one tap places it right below the idea just moved, no searching again (decision D-7)
5. Stops when the mind is empty of ideas, not when the inbox is empty; ideas may stay in the inbox for a while (decision D-8)

Pain point: searching for the node where a new note belongs. In MLO the target picker offers fixed targets (inbox, top and bottom of the root level), the five targets last used for moving, and browsing the whole tree, but no search; a target outside the recent five means digging through the tree. Answered by the target picker (decision D-9).

Notes:

- A swipe on an idea as a shortcut to "Move to…" is a wish; gestures are topic 6 and an open decision of the architecture

Principle candidates heard in journey B:

- The way of acting fits the distance: near by hand, far by choosing
- Stay in the flow: after an action the user stays where they were
- No pressure: the inbox is a parking place, not a to-do list
- Finding the place is the hard part; the app must make it easy
- Start simple: features nobody has missed yet (for example pinned places) wait until use shows the need

### Journey C – I know there was something

The owner is planning something and knows there are already notes about it somewhere in the tree; one word is remembered.

1. Starts a search and types the word
2. **Key moment:** the tree itself is filtered: only the matching nodes and their parents stay visible, the word is highlighted (decision D-12)
3. Links the found notes to the plan instead of moving them; they stay where they are and the plan points to them (decision D-13)
4. Stays in the filtered view after linking and continues with the next match [ASSUMPTION, by analogy with D-7]

Principle candidate heard in journey C and in the wish for tags: several dimensions for notes; the tree is one order (where a note belongs), tags and relations add more (what kind, which context, which status, what it relates to).

### Journey D – From phone to tablet, offline

The owner has captured ideas on the phone and wants to continue on the tablet; there is no internet at all.

1. Captured ideas on the phone
2. Wants to continue with them on the tablet
3. Hands the changes from the phone to the tablet as an encrypted file (ADR-007; decision D-19)
4. The tablet reads the file in and offers right away to send its own changes back; one more transfer and both devices are equal (decision D-20)
5. **Key moment:** wants to continue with the notes that just arrived; a short confirmation names how many arrived and offers Show, which filters the tree to them (decision D-21)
6. A note changed on both devices while offline is named in the confirmation ("1 conflict") and marked on the note until it is resolved (decision D-22)
7. Resolves it: a small, unconnected note by picking a version or writing a new one; a note connected to many things only after looking at its context again (decision D-23)

Notes:

- Sync over the local Wi-Fi is not possible for a PWA by local discovery; a direct WebRTC connection with a handshake by QR codes, without a server, would be possible but needs an ADR (Content Security Policy, ADR-045) and a spike; kept as an idea for later, besides the native wrapper with local discovery named in the architecture

## Open questions

Tags (core decided in D-14; details parked for feature work, none is a foundation decision):

1. Duplicates: two devices that define the same tag name offline create two tags; whether and how the app offers to join them
2. Names with spaces: where `@@name` ends, and whether a tag name can contain spaces
3. Managing tags: renaming, deleting, a list of all tags; where this lives
4. Structure: flat tags only, or nested tags (for example `@@garden/vegetables`)
5. Look: colours for tags or not (touches the design tokens, topic 4)

Bottom toolbar (D-17):

6. Which actions it holds besides 📥 capture and ＋ add here (search, move and more are candidates); decided with the layout model and touch interaction (topics 5 and 6)
7. Reach with one hand: the outermost buttons may be too far for the thumb; to be checked on a real phone

## Decision log

| # | Date | Topic | Decision | Source |
| --- | --- | --- | --- | --- |
| D-1 | 2026-11-12 | 1 Principles (journey A) | A quickly captured idea lands in an inbox, to be sorted into the tree later; the user does not choose a place before typing | Journey A |
| D-2 | 2026-11-12 | 1 Principles (journey A) | In a chain of ideas the next idea is a sibling by default; typing a space as the first character of an empty title nests it under the previous idea (as in MLO); accessible alternatives: a menu command, and Tab on a keyboard (ADR-033) | Journey A |
| D-3 | 2026-11-12 | 1 Principles (journey A) | Backspace in an empty title moves the idea one level up; only at the top level (of the inbox or the current zoom) does it remove the empty line; accessible alternatives: a menu command, and Shift+Tab on a keyboard (ADR-033) | Journey A |
| D-4 | 2026-11-12 | 1 Principles (journey A) | On opening, the app shows the place last worked on (device state, ADR-013) together with a clearly visible capture button; one tap on it opens the inbox with a fresh, empty line, so typing starts after one tap (within U1) | Journey A |
| D-5 | 2026-11-12 | 1 Principles (journey A) | A captured idea goes into the inbox of the open tree; the capture view shows that tree's name, and one tap switches to another tree's inbox | Journey A |
| D-6 | 2026-11-12 | 1 Principles (journey B) | Moving a node offers three ways, chosen by distance: drag and drop for nearby places; "Move to…" that finds the target by search or in a compact tree, shows the target's children and places the node at a chosen position; and moving it to the top level (its own root). "Move to…" is also the accessible alternative to dragging (ADR-033) | Journey B |
| D-7 | 2026-11-12 | 1 Principles (journey B) | After a move the user stays where they were (the inbox); a short confirmation names the target and offers Undo and Go there. The app remembers the last placed node (device state, ADR-013): for the next node there are two one-tap choices, keep it in the inbox or place it right below the last placed node; a space typed at the start of an empty title (D-2) nests it under that node instead | Journey B |
| D-8 | 2026-11-12 | 1 Principles (journey B) | No pressure to empty the inbox: no badges, no counts in warning colours, no reminders; at most a calm, neutral number of entries next to the inbox | Journey B |
| D-9 | 2026-11-12 | 1 Principles (journey B) | One target picker for "Move to…" and for choosing a target: a search field at the top, focused at once, listing matching nodes with their path (using the on-device search and its matching rules); below it the fixed targets inbox, start and end of the top level; then about five recent targets (device state, ADR-013); then browsing the whole tree as a compact, collapsed view. After choosing, the target's children are shown so the node can be placed at a chosen position (D-6). Pinned places are left out until use shows the need | Journey B |
| D-10 | 2026-11-12 | 1 Principles (journey A, B) | Inside the app there is one way of entering text: a new node is added in the outline and typed in place (also in the inbox, D-4); no separate capture form or sheet. The note is edited in the node's detail view. A light capture form from the home screen (like MLO's widget) waits for the store app or package; a separator for typing title and note in one field is decided in D-11 | Journey A, B, MLO screenshots |
| D-11 | 2026-11-12 | 1 Principles (journey A) | While typing a title in the outline, `##` splits it: the text before is the title, the text after becomes the note (appended on a new line if the node already has a note), applied when editing the title ends; `\##` keeps two literal `#` in the title. Works the same everywhere a title is typed (as in MLO). [ASSUMPTION] appending to an existing note and the escape are to be confirmed in the UI specification | Owner tried it in MLO |
| D-12 | 2026-11-12 | 1 Principles (journey C) | Search filters the outline in place: only matching nodes and their ancestors stay visible, matches are highlighted, a match in a note is marked on its node; clearing the search returns to the previous view. Uses the on-device search and its matching rules (architecture section 8) | Journey C |
| D-13 | 2026-11-12 | 1 Principles (journey C) | A relation can be created from either end: "Link to…" on any node opens the target picker (D-9), the same picker for both directions; the new relation gets the type `related`, which can be changed afterwards [ASSUMPTION: default type]; relations and backlinks as in ADR-024 | Journey C |
| D-14 | 2026-11-12 | 1 Principles (wish for tags) | Notes can carry several tags, now rather than later; uses: finding, grouping across branches, context and status. Typing `@@name` in a title adds the tag (suggesting existing tags while typing); when editing ends the text is removed from the title and the tag is shown as a chip; tapping a chip filters the tree to the nodes with that tag, like search (D-12); a tag picker in the detail view is the alternative without typing. Architecture change proposed as [ADR-058](adr/ADR-058-tags.md) (Proposed) | Owner, Trello and MLO experience |
| D-15 | 2026-11-12 | 1 Principles (wish for tags) | The details of tags (duplicates, names with spaces, managing, nesting, colours) are parked as open questions for feature work | Owner |
| D-16 | 2026-11-12 | 1 Principles | The eight UX principles in section "UX principles" are agreed, condensed from journeys A to C; accessible and honest about protection are inherited from the architecture | Journeys A–C |
| D-17 | 2026-11-12 | 1 Principles (journeys A, B) | The phone layout has a bottom toolbar with the main actions; the capture button 📥 (capture into the inbox, D-4) sits in its middle, ＋ adds a new note at the current place. Only one floating-looking button (📥, docked in the bar); no second floating button. Contents of the bar and one-handed reach are open questions. Sketches of the variants compared: bottom toolbar, select then act, tap empty space, one button with inbox in the top bar | Owner, sketch comparison |
| D-18 | 2026-11-12 | 1 Principles (journey A) | Revises D-10 for quick capture: 📥 opens a light sheet from below over the dimmed current view, with an empty line and the keyboard; it shows the target (the open tree's inbox, switchable, D-5); its lines behave exactly like the outline (Enter, leading space, Backspace, `##`, `@@`), so it is a small window onto the inbox, not a second kind of form; closing it returns to the place the user was; a button opens the full detail view of the current idea. Everywhere else new notes are typed in place in the tree (D-10) [ASSUMPTION: details of the sheet's behaviour to be confirmed in the UI specification] | Owner, MLO widget |
| D-19 | 2026-11-13 | 1 Principles (journey D) | Without internet, devices exchange changes by encrypted file as in the architecture (ADR-007); sync over the local Wi-Fi (WebRTC with a QR-code handshake, or the native wrapper) is an idea for later, no ADR proposed now | Journey D |
| D-20 | 2026-11-13 | 1 Principles (journey D) | Exchange by file is guided as a round trip: after a device has read in a file from another device, it offers at once to send its own changes back ("Send back to Phone"), so both devices end up equal; the user can decline. Uses the existing flow ("Import from another device", whole log exported by default) | Journey D |
| D-21 | 2026-11-13 | 1 Principles (journey D) | After an exchange a short confirmation names how many notes arrived ("7 new or changed notes from Phone") and offers Show, which filters the tree to those notes and their ancestors, like search (D-12). Search also offers a filter "Changed recently" at any time, based on the time of the last change of each node. A filter by device is left out until use shows the need | Journey D |
| D-22 | 2026-11-13 | 1 Principles (journey D) | Conflicts (ADR-018) are reported in two places: in the confirmation after an exchange or sync ("1 conflict", with a way to resolve it), and as a marker on the affected node that stays until the conflict is resolved; all conflicts are also listed in the Conflict View. The marker is noticeable but calm, no alarm colours (principle 5) [ASSUMPTION: look decided with the tokens, topic 4] | Journey D |
| D-23 | 2026-11-13 | 1 Principles (journey D) | The resolve view shows both (or all) versions side by side with device and time; the user picks one or writes a new version starting from one of them (an ordinary event replacing all competing changes, ADR-018); it shows the node's context to help remember what was meant: path, children, relations and backlinks, each tappable; "Later" leaves the conflict open and the marker stays | Journey D |
