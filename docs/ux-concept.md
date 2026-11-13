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
| 7 | Information architecture: trees, outline, item detail (note, relations, backlinks, history), search, conflicts, settings, feedback | Partly | Navigation structure and the application layer's state |
| 8 | States and feedback: offline, sync status, unsynced changes, conflicts, warnings, errors | Partly | One consistent pattern for all states, used by every feature |
| 9 | Onboarding: first setup, privacy explanation, pairing flow | No | Feature work |
| 10 | Microcopy and tone | No | Feature work, guided by the principles |
| 11 | UX with agents: UI specifications as the UX specification, prototypes, reviews | Yes | Determines how UX intent reaches the agents |

## Order

Worked out in detail now: 1, 2, 3, 4, 11. Only the foundation parts of 5 to 8. Topics 9 and 10 later.

## Terms

This concept uses the following terms (decision D-45, replacing D-44). The change to the architecture's glossary is proposed as [ADR-063](adr/ADR-063-terms.md) (Proposed).

| Term | Meaning |
| --- | --- |
| Tree | One independent outline with its own key and event log, for example "Private" or "Work" |
| Item | The thing in the tree: a title, a note, tags, and optionally a theme and views; later also pictures and files. What it is (a thought, an idea, a seed, a project, a topic, a vision) is the user's to decide, for example with tags; an item can grow |
| Note | The optional longer text of an item, written in the detail view |
| Branch | An item together with everything below it |
| Node | How an item appears in the tree: its line in the outline and its place; a technical term for code and specifications |

What the app calls these things in its own wording is decided with microcopy (topic 10).

## Vision in the owner's words

> It's a structured thought trap. You have an idea, put it in there and sort it out now or later. Inspired by GTD: clear your mind, be sure everything is caught and processed later. Don't forget a thing and structure them as you wish. Work out details, be creative with your thinking, take notes from a speech or a meeting, put them in context now quickly, structure them afterwards. Structuring can lead to complex projects, not as tasks or todos, but as thoughts, which can evolve or spawn todos, appointments (mostly with myself to reserve time slots), delegation.

- Items grow: an idea becomes a project, a topic or a vision by working on it, without changing what it is in the app
- Spawning todos, appointments and delegations is part of the vision but not decided; it is exactly where the risk of drifting into task management begins (architecture section 11, "the MLO trap"); for the architecture and a later product decision

## UX principles

Derived from journeys A to D (decisions D-16, D-24). They guide every later decision, including the agents'.

1. **Capture first, sort later.** Nothing has to be decided before typing: not the place, not the tree, not the kind. (D-1, D-4, D-5)
2. **Create in the tree, go deeper in the detail view.** New items are created and named in place in the tree; titles take inline Markdown (emphasis, links, inline code) and emojis. Shortcuts live in the text itself (space, Backspace, `##`, `@@`), each with a visible alternative. Longer text is written in the item's detail view. Quick capture opens a light sheet whose lines behave exactly like the outline, so typing works the same everywhere. (D-2, D-3, D-10, D-11, D-14, D-18)
3. **Stay in the flow.** After every action the user stays where they were, and the next step is right there. (D-2, D-7, D-13)
4. **Effort fits the distance.** Near by hand (drag), far by choosing (search); finding the right place must be easy. (D-6, D-9)
5. **Calm, no pressure.** No badges, no warning colours, no reminders that push. (D-8)
6. **Several dimensions.** The tree is one order; tags and relations add more. (D-13, D-14)
7. **Saved at once and reliably.** The user never wonders whether something was stored. (Journey A; quality goal reliability)
8. **Start simple.** A feature comes when use shows it is missed. (D-9, D-15)
9. **Nothing happens behind the user's back.** The app shows what arrived and what clashed, and never decides silently for the user; when something needs the user, it waits calmly until they have time. (D-21, D-22, D-23; ADR-018)

Inherited from the architecture and applying on top: accessible (ADR-033), and honest about what is protected (section 1).

## Undo (topic 2)

The owner thinks of undo like a git repository: every change is a commit that is never erased, and undo is a revert (decision D-25).

| Need | Git equivalent | In ThoughtTree |
| --- | --- | --- |
| Reverse the last slip | `git revert HEAD` | Undo right after the action (also in the confirmation after a move, D-7) |
| Step back several actions | Several reverts | Undo button or Ctrl+Z, repeated, through the user's own actions on this device |
| Go back much later | `git log`, restore a file | The item's history: every version with device and time, any change revertible, whoever made it |

- Quick undo does not go strictly by time: with live sync, a change from another device can arrive between the user's action and the undo; undoing that instead of the user's own slip would be surprising
- Undo is not limited to the current view (changing the focus would lose it, and a move belongs to two places); instead undo always shows what it reverses
- While text is being edited, it is a local draft (ADR-013): the editor's usual undo and redo apply within the draft, character by character or word by word; once editing ends, the whole text change is one step for quick undo; for all other actions there is undo but no redo, a regretted undo is reverted from the history (decision D-26)
- Quick undo needs no list of its own: it is read from the log (this device's events, newest first); an undo event names the event it reverts, like git's "This reverts commit …", so the history can show "reverted by …" and undo survives closing the app (decision D-27)
- When the item was changed since on another device or by another user, quick undo stops and asks instead of overwriting: "This item has been changed since on Tablet ('C'). Open history?" (decision D-28)
- Quick undo reaches back without limit; since every undo shows what it reverses, an old step cannot happen unnoticed (decision D-29)
- Architecture change proposed as [ADR-059](adr/ADR-059-undo.md) (Proposed); the item history brings forward what the architecture names as "possible later"

## Component library (topic 3)

The library is chosen by a spike (decision D-30).

- Candidates: MudBlazor, Fluent UI Blazor, Radzen Blazor (all MIT), and own components; commercial libraries (Telerik, Syncfusion, DevExpress) are excluded by the licence rule (ADR-050)
- The spike also checks the libraries' tree components (MudTreeView, FluentTreeView, RadzenTree): whether one can carry the outliner or serve as its base
- Criteria:
  - works under the strict Content Security Policy without `unsafe-inline` (ADR-045)
  - accessible: no axe findings, keyboard only, screen readers (ADR-033)
  - direction-neutral: CSS logical properties, works mirrored (ADR-033)
  - download size within the budget (PF5)
  - themeable through our own design tokens, light and dark (topic 4), far enough to carry ThoughtTree's own calm look instead of the library's (D-31)
  - touch targets of at least 24×24 CSS pixels (ADR-033)
  - for the note: an editor that formats Markdown as you type, converts pasted HTML to Markdown cleanly, is accessible and works in both writing directions (D-61)
  - for the tree component: typing in place with Enter, leading space and Backspace (D-2, D-3), the `##` and `@@` shortcuts (D-11, D-14), 10,000 items stay responsive (PF1, PF2), dragging with an accessible alternative (D-6)

## Design tokens and theming (topic 4)

- Reference for the look: MyLifeOrganized (light, plain, a fresh accent colour), in ThoughtTree's own calm look (D-31)
- The first theme is calm; more colourful themes may come later, so tokens are semantic (for example `surface`, `text`, `accent`, `conflict-marker`), and a theme only swaps the palette behind them (decision D-32)
- First theme: **Paper**, warm paper tones with a rust ochre accent (decision D-33); compared against Fresh, Slate and Quiet mono, and against a terracotta, a burnt sienna and a warm brown accent

| Token (draft) | Light | Dark |
| --- | --- | --- |
| `surface` (page, bars) | `#f7f3ec` | `#1c1915` |
| `surface-content` (list) | `#fffdf8` | `#23201b` |
| `text` | `#2a2520` | `#ece5da` |
| `text-muted` | `#6d645a` | `#a89e90` |
| `line` | `#e9e1d4` | `#363029` |
| `accent` (capture button, buttons) | `#9a5e28` | `#e3a35f` |
| `on-accent` (text on accent) | `#ffffff` | `#2a160d` |
| `accent-text` (links, chips) | `#84511f` | `#ecb985` |
| `selection` | `#f3e7d5` | `#3b2e1e` |
| `conflict-marker` [ASSUMPTION: calm violet, to be confirmed] | `#7a6bb0` | `#a99be0` |

- Themes can be chosen per tree and per item, to support the vibe of a project; an item's theme applies to its whole branch (shown for all its children, unless a child has its own theme), and shows when the user focuses on it (decision D-34)
- In the overview a branch with its own theme is tinted in that theme, so its mood is visible before focusing on it; focused on it, the whole screen takes on the theme (decision D-35; sketch: dot versus tinted branch versus zoomed in)
- Themes of trees and branches are shared: the same on every device and for every user of a tree, recorded by an event; proposed as [ADR-060](adr/ADR-060-shared-themes.md) (Proposed) (decision D-36)
- Typography: the base font is the device's system font, so startup downloads nothing; a theme may bring its own font (for example Literata for writing, Nunito for creative, IBM Plex Mono for technical projects), downloaded in the background after start and kept offline; proposed as [ADR-061](adr/ADR-061-shipped-fonts.md) (Proposed), which also approves the SIL Open Font License for fonts (decision D-37)
- Custom views: views belong to an item (decision D-39, revising D-38):
  - every item, and the tree itself, can carry one or more views; the default is the outline
  - a view is a saved search over the item's branch (for example "🔎 Open questions", "Changed recently") and, later, a layout such as a board or columns
  - focused on the item, a view switcher (like tabs) appears when it has more than one view; the item's theme applies to all its views
  - at the tree level, a view "All views" lists every view of the tree with the path of its item
  - the start view of a device is an item plus one of its views
  - start icons with their own view (decision D-40): the app understands a start parameter "item plus view" in its address from the start; Android: "Add to home screen" from such a link (spike: does it open in the app window or a tab); Windows and desktop: links with the start parameter; iPhone and iPad: with the store app, because each home-screen web app there has its own storage
  - proposed as [ADR-062](adr/ADR-062-views-on-items.md) (Proposed)
- A theme brings more than colours: palette, optionally a font (D-37), the density of the outline and whether the first line of an item's note is previewed under its title. Paper: density comfortable (rows about 44 px, text 16 px, system font) with the note preview shown (decision D-42; sketch: airy, comfortable, compact)
- Text size follows the device: the theme's sizes are the starting point, and the device's text-size setting scales them (sizes in relative units; on iPhone and iPad the system text style is used so Dynamic Type applies); rows grow with the text, layouts must hold at 200 % text size; no slider of its own in the app (decision D-43)
- **Signature feature:** a theme for a part of the tree, tinted in the overview and taking over the whole screen when focused on; the owner has not seen this anywhere else

All text and accent pairs above meet WCAG AA contrast; the contrast unit test (architecture section 8) will check them once they are code.


## UX with agents (topic 11)

How the owner's UX intent reaches the agents, and how the owner checks the result (decision D-46):

- Before: for a new screen or a visibly new part of one, the agent first makes an HTML sketch (as in this concept: variants side by side, real wording, Paper theme); the owner chooses or corrects it; only then the agent writes the UI specification and the code
- The owner does not approve UI specifications separately beforehand; changes to specifications are reviewed anyway in the pull request, because `tests/specify/**` is a protected path (architecture section 2)
- After: the pull request shows screenshots generated from the UI specifications, for phone and tablet, light and dark
- After: the owner tries the feature in the test environment on their own devices, through a link in the pull request
- Sketches are shown as artifacts for choosing; only the chosen variant is kept in the repository as reference, in `docs/ux/sketches/`, linked from its decision; rejected variants are not kept; where a sketch and the text of this concept differ, the text wins (decision D-47)
- Binding, as a definition of done for UI work, by three means together (decision D-48): a rule for agents, proposed as [ADR-064](adr/ADR-064-ui-follows-ux-concept.md) (Proposed); a UI checklist in the pull request template; and a pointer to the UX concept in the agents' entry file (`CLAUDE.md` or `AGENTS.md`). The pull request template and the entry file do not exist yet; they are created when ADR-064 is accepted

Definition of done for UI work (decision D-49): a UI feature is done when all three hold:

| Part | What it covers | Checked by |
| --- | --- | --- |
| Push gate (exists) | All tests, UI specifications, accessibility scans and ADR checks are green; ADR-064's automatable rules (tokens only, every UI specification names a UX decision) | CI |
| Pull request checklist (D-48) | What cannot be automated: principles checked, terms used, sketch linked, screenshots attached, keyboard and screen reader paths covered | The agent fills it, the owner checks it |
| Design review (D-46) | The owner looks at the screenshots, tries the feature in the test environment and approves | The owner |


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
5. Optionally adds context: a note and a tag
6. If it is a chain of ideas, enters the next idea right away, without detours
7. Closes the sheet and is back where they were
8. Where the idea belongs in the tree is decided later, not now

Remarks:

- The idea lands in an inbox (decision D-1); no decision about its place is needed before typing
- The next idea of a chain is a sibling; a space typed into its empty title nests it under the previous idea (decision D-2)
- Backspace in an empty title moves it one level up; at the top level of the inbox it removes the empty line (decision D-3)
- The capture view shows the tree the idea goes into (the open tree); one tap switches to another tree (decision D-5)
- Inside the app, capture happens directly in the outline: a new line in the tree, typed in place; there is one way of entering titles, the note is written in the item's detail view (decision D-10)
- Fast typers write `Title ## Note` in one go; what follows `##` becomes the item's note (decision D-11)
- The light capture sheet of MLO comes from its home-screen widget; a PWA cannot offer widgets, so this waits for the later store app or package; until then app shortcuts (press and hold the app icon: "New idea", Android and Windows only, not iPhone) are an option to consider
- Tags are added while typing with `@@` (decision D-14)
- Emojis in titles are already supported (section 8, text)
- Design target U1 (new item within two taps of opening the app) applies directly

Principle candidates heard in this journey (to be confirmed with the other journeys):

- Capture before order: writing comes first, sorting later
- No friction when half awake: no decision before typing
- Keep the flow: one idea leads to the next without detours
- Saved at once and reliably: the user never wonders whether something was stored

### Journey B – Sorting with coffee

The next morning the owner has made a coffee and drinks it in bed; awake, relaxed, with time. Phone in one hand, coffee in the other.

1. Opens the inbox with the ideas of the night
2. Sorts them one by one into the tree, choosing the way of moving by distance (decision D-6):
   - next to another item nearby: drag and drop
   - into a project the owner has in mind: "Move to…", choose the project, see what is already there, place the idea where it belongs
   - something new: its own root, or wherever it belongs
3. After each move the owner stays in the inbox; a short confirmation shows where the idea went, with Undo and Go there (decision D-7)
4. **Key moment:** the next idea belongs to the same project: one tap places it right below the idea just moved, no searching again (decision D-7)
5. Stops when the mind is empty of ideas, not when the inbox is empty; ideas may stay in the inbox for a while (decision D-8)

Pain point: searching for the place where a new item belongs. In MLO the target picker offers fixed targets (inbox, top and bottom of the root level), the five targets last used for moving, and browsing the whole tree, but no search; a target outside the recent five means digging through the tree. Answered by the target picker (decision D-9).

Remarks:

- A swipe on an idea as a shortcut to "Move to…" is a wish; gestures are topic 6 and an open decision of the architecture

Principle candidates heard in journey B:

- The way of acting fits the distance: near by hand, far by choosing
- Stay in the flow: after an action the user stays where they were
- No pressure: the inbox is a parking place, not a to-do list
- Finding the place is the hard part; the app must make it easy
- Start simple: features nobody has missed yet (for example pinned places) wait until use shows the need

### Journey C – I know there was something

The owner is planning something and knows there are already items about it somewhere in the tree; one word is remembered.

1. Starts a search and types the word
2. **Key moment:** the tree itself is filtered: only the matching items and their parents stay visible, the word is highlighted (decision D-12)
3. Links the found items to the plan instead of moving them; they stay where they are and the plan points to them (decision D-13)
4. Stays in the filtered view after linking and continues with the next match [ASSUMPTION, by analogy with D-7]

Principle candidate heard in journey C and in the wish for tags: several dimensions for items; the tree is one order (where an item belongs), tags and relations add more (what kind, which context, which status, what it relates to).

### Journey D – From phone to tablet, offline

The owner has captured ideas on the phone and wants to continue on the tablet; there is no internet at all.

1. Captured ideas on the phone
2. Wants to continue with them on the tablet
3. Hands the changes from the phone to the tablet as an encrypted file (ADR-007; decision D-19)
4. The tablet reads the file in and offers right away to send its own changes back; one more transfer and both devices are equal (decision D-20)
5. **Key moment:** wants to continue with the items that just arrived; a short confirmation names how many arrived and offers Show, which filters the tree to them (decision D-21)
6. An item changed on both devices while offline is named in the confirmation ("1 conflict") and marked on the item until it is resolved (decision D-22)
7. Resolves it: a small, unconnected item by picking a version or writing a new one; an item connected to many things only after looking at its context again (decision D-23)

Remarks:

- Sync over the local Wi-Fi is not possible for a PWA by local discovery; a direct WebRTC connection with a handshake by QR codes, without a server, would be possible but needs an ADR (Content Security Policy, ADR-045) and a spike; kept as an idea for later, besides the native wrapper with local discovery named in the architecture

## Open questions

Tags (core decided in D-14; details parked for feature work, none is a foundation decision):

1. Duplicates: two devices that define the same tag name offline create two tags; whether and how the app offers to join them
2. Names with spaces: where `@@name` ends, and whether a tag name can contain spaces
3. Managing tags: renaming is decided (D-64); deleting a tag everywhere and a list of all tags are still open
4. Structure: flat tags only, or nested tags (for example `@@garden/vegetables`)
5. Look: colours for tags or not (touches the design tokens, topic 4)

Bottom toolbar (D-17, revised by D-52):

6. Contents and order decided in D-52; to be checked with a tappable prototype on the owner's phone
7. Reach with one hand: answered by the hand setting and the order by reach (D-52)

Custom views (owner's idea, to be expanded):

8. Themes could bring more than colours, for example their own font; the owner wants to expand this into "custom views" for projects. Started with views on items (D-39); layouts per project such as a board or columns are parked (D-41; watch the risk of drifting into task management, architecture section 11)

Release 1 scope (owner's wish, input to the open decision "Release 1 scope" of the architecture; decision D-50):

10. Phone, tablet and desktop (D-51); no sync server, every device standalone (ADR-008); sync between devices only by file with the guided round trip (D-19, D-20); themes per tree and per item (D-41)
11. Consequences for the architecture: the app is still served from a web address (static hosting over HTTPS) to be installable; server store, sync API, sync modes (ADR-057), registration and abuse protection can wait; the reminder to export becomes the main protection against losing a device
12. Owner's need (D-51): try every change quickly on their own phone, tablet and desktop, straight from the pull request. Recommendation for the architecture (ADR-046): a preview deployment per pull request (for example Azure Static Web Apps, free tier, which can send the strict security headers of ADR-045); later public distribution from the repository via GitHub Pages and/or packages in GitHub Releases (native wrapper). A downloaded copy cannot run as a PWA from a file; it needs an https address or `localhost`

Store app (for the architecture):

9. Native wrapper: .NET MAUI Blazor Hybrid looks like the most fitting route (same Razor components in C#, native shortcuts, widgets, background sync and shared-file sync; browser adapters swapped thanks to ADR-042); alternatives Capacitor and PWABuilder/TWA; Apple's minimum-functionality review and developer accounts to consider. Not decided here

## Decision log

| # | Date | Topic | Decision | Source |
| --- | --- | --- | --- | --- |
| D-1 | 2026-11-12 | 1 Principles (journey A) | A quickly captured idea lands in an inbox, to be sorted into the tree later; the user does not choose a place before typing | Journey A |
| D-2 | 2026-11-12 | 1 Principles (journey A) | In a chain of ideas the next idea is a sibling by default; typing a space as the first character of an empty title nests it under the previous idea (as in MLO); accessible alternatives: a menu command, and Tab on a keyboard (ADR-033) | Journey A |
| D-3 | 2026-11-12 | 1 Principles (journey A) | Backspace in an empty title moves the idea one level up; only at the top level (of the inbox or the current focus) does it remove the empty line; accessible alternatives: a menu command, and Shift+Tab on a keyboard (ADR-033) | Journey A |
| D-4 | 2026-11-12 | 1 Principles (journey A) | On opening, the app shows the place last worked on (device state, ADR-013) together with a clearly visible capture button; one tap on it opens the inbox with a fresh, empty line, so typing starts after one tap (within U1) | Journey A |
| D-5 | 2026-11-12 | 1 Principles (journey A) | A captured idea goes into the inbox of the open tree; the capture view shows that tree's name, and one tap switches to another tree's inbox | Journey A |
| D-6 | 2026-11-12 | 1 Principles (journey B) | Moving an item offers three ways, chosen by distance: drag and drop for nearby places; "Move to…" that finds the target by search or in a compact tree, shows the target's children and places the item at a chosen position; and moving it to the top level (its own root). "Move to…" is also the accessible alternative to dragging (ADR-033) | Journey B |
| D-7 | 2026-11-12 | 1 Principles (journey B) | After a move the user stays where they were (the inbox); a short confirmation names the target and offers Undo and Go there. The app remembers the last placed item (device state, ADR-013): for the next item there are two one-tap choices, keep it in the inbox or place it right below the last placed item; a space typed at the start of an empty title (D-2) nests it under that item instead | Journey B |
| D-8 | 2026-11-12 | 1 Principles (journey B) | No pressure to empty the inbox: no badges, no counts in warning colours, no reminders; at most a calm, neutral number of entries next to the inbox | Journey B |
| D-9 | 2026-11-12 | 1 Principles (journey B) | One target picker for "Move to…" and for choosing a target: a search field at the top, focused at once, listing matching items with their path (using the on-device search and its matching rules); below it the fixed targets inbox, start and end of the top level; then about five recent targets (device state, ADR-013); then browsing the whole tree as a compact, collapsed view. After choosing, the target's children are shown so the item can be placed at a chosen position (D-6). Pinned places are left out until use shows the need | Journey B |
| D-10 | 2026-11-12 | 1 Principles (journey A, B) | Inside the app there is one way of entering text: a new item is added in the outline and typed in place (also in the inbox, D-4); no separate capture form or sheet. Its note is edited in the item's detail view. A light capture form from the home screen (like MLO's widget) waits for the store app or package; a separator for typing title and note in one field is decided in D-11 | Journey A, B, MLO screenshots |
| D-11 | 2026-11-12 | 1 Principles (journey A) | While typing a title in the outline, `##` splits it: the text before is the title, the text after becomes the item's note (appended on a new line if the item already has a note), applied when editing the title ends; `\##` keeps two literal `#` in the title. Works the same everywhere a title is typed (as in MLO). [ASSUMPTION] appending to an existing note and the escape are to be confirmed in the UI specification | Owner tried it in MLO |
| D-12 | 2026-11-12 | 1 Principles (journey C) | Search filters the outline in place: only matching nodes and their ancestors stay visible, matches are highlighted, a match in an item's note is marked on its node; clearing the search returns to the previous view. Uses the on-device search and its matching rules (architecture section 8) | Journey C |
| D-13 | 2026-11-12 | 1 Principles (journey C) | A relation can be created from either end: "Link to…" on any item opens the target picker (D-9), the same picker for both directions; the new relation gets the type `related`, which can be changed afterwards [ASSUMPTION: default type]; relations and backlinks as in ADR-024 | Journey C |
| D-14 | 2026-11-12 | 1 Principles (wish for tags) | Items can carry several tags, now rather than later; uses: finding, grouping across branches, context and status. Typing `@@name` in a title adds the tag (suggesting existing tags while typing); when editing ends the text is removed from the title and the tag is shown as a chip; tapping a chip filters the tree to the items with that tag, like search (D-12); a tag picker in the detail view is the alternative without typing. Architecture change proposed as [ADR-058](adr/ADR-058-tags.md) (Proposed) | Owner, Trello and MLO experience |
| D-15 | 2026-11-12 | 1 Principles (wish for tags) | The details of tags (duplicates, names with spaces, managing, nesting, colours) are parked as open questions for feature work | Owner |
| D-16 | 2026-11-12 | 1 Principles | The eight UX principles in section "UX principles" are agreed, condensed from journeys A to C; accessible and honest about protection are inherited from the architecture | Journeys A–C |
| D-17 | 2026-11-12 | 1 Principles (journeys A, B) | The phone layout has a bottom toolbar with the main actions; the capture button 📥 (capture into the inbox, D-4) sits in its middle, ＋ adds a new item at the current place. Only one floating-looking button (📥, docked in the bar); no second floating button. Contents of the bar and one-handed reach are open questions. Sketches of the variants compared: bottom toolbar, select then act, tap empty space, one button with inbox in the top bar | Owner, sketch comparison |
| D-18 | 2026-11-12 | 1 Principles (journey A) | Revises D-10 for quick capture: 📥 opens a light sheet from below over the dimmed current view, with an empty line and the keyboard; it shows the target (the open tree's inbox, switchable, D-5); its lines behave exactly like the outline (Enter, leading space, Backspace, `##`, `@@`), so it is a small window onto the inbox, not a second kind of form; closing it returns to the place the user was; a button opens the full detail view of the current idea. Everywhere else new items are typed in place in the tree (D-10) [ASSUMPTION: details of the sheet's behaviour to be confirmed in the UI specification] | Owner, MLO widget |
| D-19 | 2026-11-13 | 1 Principles (journey D) | Without internet, devices exchange changes by encrypted file as in the architecture (ADR-007); sync over the local Wi-Fi (WebRTC with a QR-code handshake, or the native wrapper) is an idea for later, no ADR proposed now | Journey D |
| D-20 | 2026-11-13 | 1 Principles (journey D) | Exchange by file is guided as a round trip: after a device has read in a file from another device, it offers at once to send its own changes back ("Send back to Phone"), so both devices end up equal; the user can decline. Uses the existing flow ("Import from another device", whole log exported by default) | Journey D |
| D-21 | 2026-11-13 | 1 Principles (journey D) | After an exchange a short confirmation names how many items arrived ("7 new or changed items from Phone") and offers Show, which filters the tree to those items and their ancestors, like search (D-12). Search also offers a filter "Changed recently" at any time, based on the time of the last change of each item. A filter by device is left out until use shows the need | Journey D |
| D-22 | 2026-11-13 | 1 Principles (journey D) | Conflicts (ADR-018) are reported in two places: in the confirmation after an exchange or sync ("1 conflict", with a way to resolve it), and as a marker on the affected item that stays until the conflict is resolved; all conflicts are also listed in the Conflict View. The marker is noticeable but calm, no alarm colours (principle 5) [ASSUMPTION: look decided with the tokens, topic 4] | Journey D |
| D-23 | 2026-11-13 | 1 Principles (journey D) | The resolve view shows both (or all) versions side by side with device and time; the user picks one or writes a new version starting from one of them (an ordinary event replacing all competing changes, ADR-018); it shows the item's context to help remember what was meant: path, children, relations and backlinks, each tappable; "Later" leaves the conflict open and the marker stays | Journey D |
| D-24 | 2026-11-13 | 1 Principles | Principle 9 added: nothing happens behind the user's back | Journey D |
| D-25 | 2026-11-13 | 2 Undo | Undo writes new events that restore the previous state, like a git revert; every change can be reverted from the item's history, whoever made it; quick undo (button, Ctrl+Z) steps back through the user's own recent actions on this device, in one sequence across views; undo always shows what it reverses (brings the item into view and highlights it, or names the change with Go there). Proposed as [ADR-059](adr/ADR-059-undo.md) | Owner (git model), discussion |
| D-26 | 2026-11-13 | 2 Undo | Two levels: while editing a title or note, the usual undo and redo of a text editor apply within the local draft; when editing ends, the whole text change becomes one step of quick undo. For other actions (move, nest, delete, finished text changes) quick undo has no redo; a regretted undo is reverted from the item's history | Owner |
| D-27 | 2026-11-13 | 2 Undo | Quick undo is derived from the log, without a separate list: this device's own events, newest first, skipping events already reverted; every undo event names the event it reverts (`reverts`), part of the event format from the start; the history shows "reverted by …"; undo therefore survives closing the app | Owner |
| D-28 | 2026-11-13 | 2 Undo | Quick undo never overwrites a later change from another device or user: when the item was changed since, undo stops, names the later change (device and value) and offers the item's history | Owner, principle 9 |
| D-29 | 2026-11-13 | 2 Undo | Quick undo has no limit: it reaches back through all of the user's actions on this device; no session needs to be defined; showing what each undo reverses (D-25) protects against surprises | Owner |
| D-30 | 2026-11-13 | 3 Component library | The component library is chosen by a spike comparing MudBlazor, Fluent UI Blazor, Radzen Blazor and own components against the criteria in section "Component library"; the spike includes the libraries' tree components and whether one can carry the outliner; commercial libraries are out because of ADR-050 | Owner (challenge: tree components exist) |
| D-31 | 2026-11-13 | 3 Component library, 4 Tokens | ThoughtTree has its own calm look, plain and quiet, made for thinking (in the spirit of iA Writer, Bear, Things), not a standard look such as Material or Fluent; a library must be restylable to it | Owner |
| D-32 | 2026-11-13 | 4 Tokens | MLO is the reference for the look; the first theme is calm; more colourful themes may follow later, so all components use semantic tokens and a theme only swaps the palette behind them | Owner |
| D-33 | 2026-11-13 | 4 Tokens | The first theme is Paper: warm paper tones with a rust ochre accent (`#9a5e28` light, `#e3a35f` dark); draft token values in section "Design tokens and theming" | Owner, colour comparison |
| D-34 | 2026-11-13 | 4 Tokens | The theme can be changed, and chosen per tree and per item: an item's theme applies to its branch (shown for all its children unless a child has its own) and shows while the user is focused on it; otherwise the tree's theme applies. Whether the choice is synced is still open | Owner |
| D-35 | 2026-11-13 | 4 Tokens | A branch with its own theme is shown tinted in that theme in the overview (preferred over a small dot); focused on it, every bar, button and list takes on its theme. Every theme must meet the same contrast rules, also where a tinted branch sits inside another theme | Owner, sketch |
| D-36 | 2026-11-13 | 4 Tokens | Themes of trees and branches are shared through an event, not kept as device state: they belong to the content like a tree's name, so every device and every user of a shared tree sees the same mood. Proposed as [ADR-060](adr/ADR-060-shared-themes.md) | Owner |
| D-37 | 2026-11-13 | 4 Tokens | Base font: the system font of each device, for a fast start; themes may bring their own open-licence font, downloaded in the background after start and kept for offline use, the system font shows until then. Proposed as [ADR-061](adr/ADR-061-shipped-fonts.md), including approval of the SIL Open Font License for fonts | Owner, font suggestions |
| D-38 | 2026-11-13 | 4 Tokens (custom views) | A saved search is an item of its own in the tree: placed anywhere, opened like a place, showing the tree filtered as in D-12, themeable like any item. Any item, including a saved search, can be chosen as the start view of the app on a device (device state, ADR-013); "the place last worked on" (D-4) stays the default. Proposed as ADR-062; revised by D-39 | Owner |
| D-39 | 2026-11-13 | 4 Tokens (custom views) | Revises D-38: views belong to items, not to saved-search items of their own. Every item, and the tree, carries one or more views (outline by default; saved searches over its branch now; layouts such as board or columns later), switched like tabs when focused on the item; the tree level offers "All views" listing every view; the start view of a device is an item plus one of its views. Views are shared like content. Proposed as [ADR-062](adr/ADR-062-views-on-items.md) | Owner, discussion |
| D-40 | 2026-11-13 | 4 Tokens (custom views) | Start icons with their own view: the app accepts a start parameter "item plus view" in its address from the start; Android uses "Add to home screen" from such a link (spike: app window or browser tab); desktop uses links; iPhone and iPad wait for the store app (separate storage per home-screen web app; automatic sync through a shared file or cloud folder also comes with the store app, as in the architecture) | Owner, discussion |
| D-41 | 2026-11-13 | 4 Tokens (custom views) | Layouts per project (board, columns) are parked. The owner's wish for the release 1 scope (an open decision of the architecture): themes per tree and per item (D-34 to D-36) are in the first version | Owner |
| D-42 | 2026-11-13 | 4 Tokens | Density and the note preview are part of a theme. The outline can show the first line of an item's note in small, muted type under the title. Paper uses density comfortable (rows about 44 px, text 16 px) with the preview shown; other themes may choose airy (about 56 px / 17 px) or compact (about 32 px / 14 px, touch targets still at least 24 px, ADR-033) | Owner, density sketch |
| D-43 | 2026-11-13 | 4 Tokens | Text size follows the device's setting: theme sizes are the starting point and scale with it (relative units; system text style on iPhone and iPad for Dynamic Type); rows grow with the text; every layout holds at 200 % text size (WCAG 1.4.4); no in-app text-size slider | Owner |
| D-44 | 2026-11-13 | Terms | [Replaced by D-45] The concept uses the owner's terms: a note is the thing (title, text, tags, theme, views), a node is its graphical representation (its line and place in the tree), text is a note's longer content, a branch is a note with everything below it; a theme applies to a branch and is shown for all its children rather than "inherited". Applied throughout this document; the change of the architecture's glossary is proposed as [ADR-063](adr/ADR-063-terms.md) | Owner |
| D-45 | 2026-11-13 | Terms | Replaces D-44 after comparing the terms of other apps and of thinking methods: an item is the thing in the tree (title, note, tags, theme, views; later pictures and files) and can grow into whatever the user makes of it (thought, idea, project, topic, vision); a note is an item's longer text, as in outliners and today's architecture; a branch is an item with everything below it; node is the technical representation. The owner's description of ThoughtTree is recorded as its vision. Proposed for the architecture's glossary as [ADR-063](adr/ADR-063-terms.md) | Owner, exercise "describe ThoughtTree to a friend" |
| D-46 | 2026-11-13 | 11 UX with agents | Before building a new screen, the agent makes an HTML sketch that the owner chooses or corrects; UI specifications are not approved separately beforehand (they are reviewed in the pull request as a protected path); after building, the pull request shows screenshots generated from the UI specifications (phone and tablet, light and dark) and a link to try the feature in the test environment | Owner |
| D-47 | 2026-11-13 | 11 UX with agents | Sketches are shown as artifacts to choose from; only the chosen variant is stored as reference in `docs/ux/sketches/` and linked from its decision; rejected variants are not kept; the concept's text wins over a sketch | Owner |
| D-48 | 2026-11-13 | 11 UX with agents | The UX concept is made binding for agents by three means together, like a definition of done: ADR-064 (Proposed: read the concept before UI work, sketch first, tokens only, specifications name the UX decisions they secure, screenshots and test link in the pull request), a UI checklist in the pull request template, and a pointer in the agents' entry file | Owner, usual UX practice (design system, handoff, design QA) |
| D-49 | 2026-11-13 | 11 UX with agents | Definition of done for UI work: the push gate is green, the pull request checklist is complete, and the owner has approved after the design review (screenshots, test environment) | Owner |
| D-50 | 2026-11-13 | Release 1 scope (input) | The owner's wish for release 1: phone and tablet (desktop added by D-51), no sync server, sync between devices only by file (guided round trip), themes per tree and per item; the app is still served by static hosting; server-related parts can wait; the export reminder matters more. Input to the architecture's open decision "Release 1 scope", not decided here | Owner |
| D-51 | 2026-11-13 | Release 1 scope (input) | Desktop belongs to release 1 as well. The owner's main need for distribution is trying every change quickly on their own devices from the pull request; recommended to the architecture: a preview deployment per pull request, later public distribution via GitHub Pages and/or GitHub Releases; not decided here (ADR-046) | Owner |
| D-52 | 2026-11-13 | 5 Layout (phone) | Revises D-17. Capture 📥 is a floating button at the thumb's corner, above the bar; the bottom bar holds four actions ordered by reach from the thumb: ＋ Add here (nearest), 🔍 Search, ↩ Undo, ⋯ More (farthest). "Back to selection" has no button: a label naming the selected item appears above the bar only while it is scrolled out of sight, and scrolls back to it on tap. A device setting "Hand: right / left" (default right, device state, ADR-013) mirrors the floating button and the bar; it is independent of the reading direction. Sketch: bottom bar, revised | Owner (right-handed, one-handed use) |
| D-53 | 2026-11-13 | 5 Layout (phone) | Actions are shown where their subject is. Item actions (Move to…, Link to…, Tag, Theme, Views, History, Focus, Delete) open from a small ⋯ on the selected item's row, which is also their accessible path (ADR-033); press and hold is not used for this menu, since it is likely needed for dragging (topic 6). ⋯ More in the bottom bar holds only global actions (Switch tree, All views, Exchange with another device, Export, Settings, Feedback) [ASSUMPTION: both lists to be refined with the information architecture, topic 7] | Owner |
| D-54 | 2026-11-13 | 5 Layout (phone), 6 Touch | While typing (keyboard up), the bottom bar gives way to an editing toolbar above the keyboard: ↩ undo and ↪ redo of the text (D-26), outdent, indent, move up, move down, `##`, `@@`, and ✓ Done at the thumb's end [ASSUMPTION: exact contents decided in topic 6]. The floating capture button 📥 stays in its corner just above the toolbar; tapping it saves the current text, opens the capture sheet, and closing the sheet returns to the text with the cursor where it was, with a short confirmation. Only the line being edited gets extra space on the button's side, so its text wraps before the button (covers long titles, right-to-left titles with `dir="auto"`, and the button on the left for left-handed use); other lines may run under the button. The hand setting (D-52) mirrors the toolbar too. Sketch: capture while typing | Owner |
| D-55 | 2026-11-13 | 5 Layout (phone) | The bottom bar and the floating capture button stay visible while scrolling; they do not hide: calm (principle 5), capture and undo always one tap away, no controls that disappear for screen readers, keyboard users or people sensitive to motion. Revisit only if space feels tight in use | Owner, on advice |
| D-56 | 2026-11-13 | 10 Microcopy (early) | In the app, zooming into an item is labelled "Focus" ("Zoom in" was unclear); "Open" is reserved for the item's detail page. The architecture's term Zoom is replaced by Focus as well (D-57) | Owner, item menu sketch |
| D-57 | 2026-11-13 | Terms | "Zoom" is replaced by "Focus" everywhere, also in the architecture: zoom suggests magnifying (pinch), not concentrating on one item and its branch. Focus: make an item the temporary top of the view. Added to the proposed glossary change [ADR-063](adr/ADR-063-terms.md) | Owner |
| D-58 | 2026-11-13 | 5 Layout (phone) | The item menu (D-53) opens as a popover anchored at the ⋯ of the selected row, so the tree stays visible; the ⋯ and the popover sit on the side of the hand setting (D-52), independent of the reading direction. Order, by frequency and purpose: Open (the item's detail page), Focus (go deeper), Move to…, Link to… (put in order), Tags, Theme, Views (describe), History (look back), and Delete set apart at the end (remove; restorable, ADR-005); the order stays the same when the popover opens upwards, for muscle memory (decision D-59). Sketch: item menu, variant popover | Owner |
| D-59 | 2026-11-13 | 5 Layout (phone) | Order of the item menu: Open, Focus, Move to…, Link to…, Tags, Theme, Views, History, then Delete set apart; grouped by purpose and frequency; the same order whether the popover opens down or up | Owner |
| D-60 | 2026-11-13 | 7 Information architecture (item detail) | "Open" shows the item's detail page note-first: the title at the top, the note large below it and ready to write; tags, links, backlinks and history are folded away below or behind a tab [ASSUMPTION: how they are folded away is decided with the detail page] | Owner |
| D-61 | 2026-11-13 | 7 Information architecture (item detail) | The note is formatted as you type: Markdown markers show faintly only on the line with the cursor; a formatting toolbar above the keyboard (bold, italic, heading, list, link) inserts markers. Paste: Markdown is taken as is; HTML (also what Word, Pages or Outlook put next to RTF) is converted to Markdown and never stored as HTML, remote pictures become placeholders (ADR-052); pure RTF without HTML falls back to plain text. Copy: the clipboard gets the Markdown as plain text and HTML as formatted text, so both kinds of apps receive something useful. Adds an editor criterion to the component library spike (D-30). Sketch: writing a note, variant B | Owner |
| D-62 | 2026-11-13 | 7 Information architecture (item detail) | Pasting formatted text always keeps the formatting (converted to Markdown, D-61), without asking; "Paste as plain text" is available for the exception, in the text field's own menu and on desktop as Ctrl+Shift+V | Owner |
| D-63 | 2026-11-13 | 7 Information architecture (item detail) | On the detail page, tags are chips under the title; links, backlinks and history sit in a strip at the bottom with their counts and the last change (🔗 2 · ↩ 3 · 🕘 06:12), each opening as a sheet over the note. The floating capture button is on this page too, above the strip. There is no save button: everything is saved at once (principle 7); while typing, ✓ Done in the editing toolbar ends the editing (D-54); leaving the page is the back arrow at the top and the device's back (Android); a reachable way back on iPhone and iPad, which have no system back button, is decided with touch interaction (topic 6). Sketch: item detail, variant C | Owner |
| D-64 | 2026-11-13 | 7 Information architecture (item detail) | Tags on the detail page: chips under the title with a ＋ at the end. ＋ opens the tag picker (search field focused, matching tags as you type with their number of items, most used first, several can be ticked, "Create …" when none fits). Tapping a tag opens a popover, ordered by frequency: "Remove from this item" first (used most; undoable), then "Show all with …" (filters the tree, D-14), then "Rename … (n items)…" which renames the tag everywhere (a tag has one name, ADR-058; a rename is a `label.set`, concurrent renames become a visible conflict, ADR-018). Swapping a tag on one item is removing plus adding, no entry of its own. `@@name` in the title keeps working. Sketch: editing tags | Owner |
| D-65 | 2026-11-13 | 7 Information architecture (item detail), 4 Tokens | The detail page appears in the theme that applies to the item: its own, else its branch's, else the tree's; a new tree uses Paper. The theme picker (item menu › Theme) starts with "Same as above" (no theme of its own), naming the theme it follows and where from, then lists the themes; tapping one previews it live on the page, Apply or Cancel confirms. On the detail page the item's ⋯ sits in the bottom strip at the thumb's end (hand setting, D-52), not at the top; strip order from the far side: 🕘 history, ↩ linked from, 🔗 links, ⋯ item menu. Sketch: detail page themes | Owner |
| D-66 | 2026-11-13 | 5 Layout, 7 Information architecture | On the detail page the item menu opens upwards from the ⋯ in the strip, in the same order as in the tree (D-59) without "Open". While any menu or popover is open, it covers the floating capture button: one menu at a time; capture is available again as soon as the menu closes | Owner |
