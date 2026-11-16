# Outliner comparison: ideas for ThoughtTree

Compares five outliners (OmniOutliner 6, MyLifeOrganized, Workflowy, Dynalist, Logseq), with a short look at Tana and Checkvist, so the owner can pick ideas (functionality only, never look or name) for ThoughtTree. Each idea is judged against the UX principles 1–9 and the decisions D-1 … D-81 in `docs/ux-concept.md`, and against the architecture (ADR index, risk "the MLO trap"). Research date: 2026-11-13.

**Sources**

- OmniOutliner 6 release notes: https://www.omnigroup.com/releasenotes/omnioutliner/6.0
- OmniOutliner feature page: https://www.omnigroup.com/omnioutliner/features
- OmniOutliner 6.2.1 manual, Welcome: https://support.omnigroup.com/documentation/omnioutliner/universal/6.2.1/en/welcome/
- OmniOutliner 6.2.1 manual, The Outline (notes, structural/level styles, checkboxes, filters, attachments): https://support.omnigroup.com/documentation/omnioutliner/universal/6.2.1/en/outline/
- Omni Automation, status checkbox states (checked / unchecked / mixed): https://omni-automation.com/omnioutliner/tutorial/oo-macos/editor-02.html
- Omni forum, number column with Total summary: https://discourse.omnigroup.com/t/how-does-one-create-a-math-column/25204
- MLO blog, December 2024 (web version in development, widget per folder, voice input, contexts): https://blog.mylifeorganized.net/2024/12
- MLO User's Guide (computed-score priority, Goals and Review views): https://groups.google.com/group/mylifeorganized/attach/42cbddd161093/MLO%20User%20Guide%202018c.pdf?part=0.1
- Workflowy what's new (2025–2026): https://workflowy.com/whats-new/
- Workflowy get-started overview: https://community.workflowy.com/t/get-started/1650
- Workflowy update 2026.07: https://blog.workflowy.com/update-2026-07-improvements-and-fixes/
- Dynalist full feature list: https://dynalist.io/features/full
- Dynalist development status (forum, user reports): https://talk.dynalist.io/t/what-is-the-current-state-of-the-developement-of-dynalist/8472
- Logseq DB, what's new May 2026: https://discuss.logseq.com/t/whats-new-with-logseq-db-may-16th-2026/35020
- Logseq OG / DB split and beta status (forum): https://www.outlinersoftware.com/topics/viewt/12614
- Logseq docs index (block refs, embeds, linked/unlinked references, right sidebar, PDF highlights, flashcards): https://docs.logseq.com/
- Tana supertags: https://outliner.tana.inc/docs/supertags
- Checkvist feature summary: https://checkvist.com/llms.txt

Note: MLO's own website refused automated access (HTTP 403); MLO facts come from its blog and user guide, which are partly older (2018). The MLO web version was still announced as "in development" in the latest source found.

## 1. Product profiles

### OmniOutliner 6 (Mac, iPad, iPhone, Apple Vision Pro)
Document-based outliner with rows, optional typed columns and a note per row; Essentials and Pro tiers, one universal purchase since version 6. Known for rich styling (structural styles per level, named styles, now on all devices), status checkboxes whose parent shows a mixed state, number columns with a Total summary, saved filters with nested AND/OR/NOT rules, a sidebar with Sections, Styles, Filters and Batch Find, notes shown directly below the row (or in a pane on Mac), image attachments and audio recording, Dynamic Themes that follow dark mode, and Omni Links to a document or a specific row. Sync through iCloud Drive (conflict resolution added in 6.0.3); export to OPML, HTML, RTF, Word, CSV and others.
Sources: [release notes 6.0](https://www.omnigroup.com/releasenotes/omnioutliner/6.0), [features](https://www.omnigroup.com/omnioutliner/features), [manual: The Outline](https://support.omnigroup.com/documentation/omnioutliner/universal/6.2.1/en/outline/), [Omni Automation](https://omni-automation.com/omnioutliner/tutorial/oo-macos/editor-02.html)

### MyLifeOrganized (MLO) (Windows, Android, iOS, macOS; web version announced)
A task manager built on an unlimited outline: the outline holds goals, projects and tasks; a computed To-Do list ranks actions by importance, urgency, dates, contexts and dependencies. Known for contexts (with open hours and groups), Goals and Review views, a home-screen widget that remembers the last parent and can be tied to a folder, voice input that also sets importance and urgency, and cloud sync. ThoughtTree's outline feel (space to nest, `##` to split title and note) comes from here; its task engine is exactly "the MLO trap".
Sources: [MLO blog 2024-12](https://blog.mylifeorganized.net/2024/12), [MLO User's Guide](https://groups.google.com/group/mylifeorganized/attach/42cbddd161093/MLO%20User%20Guide%202018c.pdf?part=0.1)

### Workflowy (web, desktop apps, iOS, Android)
One endless outline per account; click a bullet to zoom, breadcrumb to go back. Actively developed in 2025–2026: mirrors (live copies of a node), boards and tables, node types (todo, heading, paragraph, numbered list, quote, code block), calendar with "found dates", Google Calendar sync and a daily email digest, Quick Add to the inbox (desktop, Android, iOS Shortcut/Siri), templates via `/template`, starred searches as sidebar shortcuts, search that filters the outline in place and is editable, side-by-side Panes (2026.08, desktop only), many themes, AI nodes.
Sources: [what's new](https://workflowy.com/whats-new/), [get started](https://community.workflowy.com/t/get-started/1650), [update 2026.07](https://blog.workflowy.com/update-2026-07-improvements-and-fixes/)

### Dynalist (web, Windows, macOS, Linux, iOS, Android)
Outliner organised into documents and folders; feature-rich but in long-term maintenance (user reports: development paused since about 2021, the team builds Obsidian). Known for "move to" via search without leaving the place, multi-select, `[[` item links, tags pane with click-to-filter, bookmarks for items, documents or searches, color labels, headings, checklists, dates and Google Calendar sync, inbox and web clippers, file attachments, version history, daily backups to Dropbox or Google Drive, density settings, toggling notes and checked items, article view and mind-map view, custom CSS.
Sources: [full feature list](https://dynalist.io/features/full), [forum on development status](https://talk.dynalist.io/t/what-is-the-current-state-of-the-developement-of-dynalist/8472)

### Logseq (desktop, web, iOS/Android in testing; open source)
Block-based outliner and knowledge graph. In 2026 it split into Logseq OG (Markdown files, maintenance only) and the new database version (2.0 beta, mid-2026). Known for daily journals as the default writing place, block references and embeds, linked and unlinked references, properties, tags as classes, queries, right sidebar for side-by-side blocks, whiteboards, flashcards, PDF annotation, encrypted sync (in limited testing), and a "Markdown Mirror" that keeps a plain Markdown copy in a folder up to date.
Sources: [What's new May 2026](https://discuss.logseq.com/t/whats-new-with-logseq-db-may-16th-2026/35020), [OG/DB split thread](https://www.outlinersoftware.com/topics/viewt/12614), [docs index](https://docs.logseq.com/)

### Briefly: Tana and Checkvist
Tana turns nodes into typed objects with "supertags" and fields, viewable as tables; strong for structured data, but a schema is a decision before typing ([supertags](https://outliner.tana.inc/docs/supertags)). Checkvist is a keyboard-first outliner and task list with a command palette, hoisting, tags, due dates and backlinks ([feature summary](https://checkvist.com/llms.txt)).

## 2. Idea table

Principles referred to: P1 Capture first, sort later · P2 Create in the tree, go deeper in the detail view · P3 Stay in the flow · P4 Effort fits the distance · P5 Calm, no pressure · P6 Several dimensions · P7 Saved at once and reliably · P8 Start simple · P9 Nothing happens behind the user's back. **MLO trap** marks ideas that pull towards task management (architecture section 11).

Abbreviations for products: OO = OmniOutliner, MLO, WF = Workflowy, DL = Dynalist, LS = Logseq, Tana, CV = Checkvist.

### Capture & inbox

| # | Idea | Found in | What it is | Serves / violates | Architecture impact | Already covered? | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Capture from outside the app (home-screen widget, OS quick add, shortcut) | MLO, WF, LS (QuickCapture) | Type a thought into the inbox without opening the app. | Serves P1, P3 | UI only (store app / package) | D-10, D-18, D-40 (waits for store app) | **Take** (as planned) – already the direction; keep it on the roadmap for the store app. |
| 2 | Capture target remembered per widget | MLO | Each widget remembers the parent it last saved into, or is tied to a fixed item. | Serves P1, P3; risk to P1 if the user must pick a place first | UI only (device state, ADR-013) | Partly D-7 (last placed item) | **Consider later** – only together with idea 1; default must stay the inbox. |
| 3 | Share target / web clipper | DL (browser clippers), OO (paste from other apps) | Other apps and the browser can "share to" the inbox (text, link, title). | Serves P1, P3 | UI only (PWA Web Share Target in the manifest; works on Android, not iOS PWA) | no | **Consider later** – cheap on Android, gives capture from reading without copy and paste. |
| 4 | Voice input that also fills fields | MLO | Spoken capture parsed into title plus properties (importance, urgency, effort). | Dictation serves P1; parsing properties is MLO trap | UI only (system dictation already works) | no | **Leave** – system dictation suffices; parsing fields leads into task metadata. |
| 5 | Daily journal page as default capture place | LS | Every day gets a page; new thoughts land there by date. | Conflicts with P1 as decided (inbox, D-1); date-centric | new events or ADR | D-1, ADR-066 (inbox) | **Leave** – the inbox already answers "where does it go"; a date view can come from search ("changed recently", D-21). |
| 6 | Templates inserted in place | WF (`/template`), OO (templates) | Copy a prepared branch (e.g. "meeting notes" skeleton) at the cursor. | Serves P3; P8 says wait for need | UI only (copy = ordinary create events) | no | **Consider later** – useful for repeated meeting notes; a "Duplicate branch" command may be enough first. |

### Editing & structure

| # | Idea | Found in | What it is | Serves / violates | Architecture impact | Already covered? | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 7 | Paste multi-line / indented text as several items | OO (paste options), WF, DL | Pasting an indented or bulleted list into the outline creates one item per line with nesting. | Serves P1, P3 (notes from a meeting written elsewhere) | UI only (parser creating ordinary events; one undo step, ADR-059) | Paste into notes only (D-61, D-62) | **Take** – removes manual retyping; must be one quick-undo step and say what it created (P9). |
| 8 | Multi-select with bulk actions | DL, WF (long-press multi-select), MLO (multiselect toolbar) | Select several items, then move, tag, theme or delete them together. | Serves P4 (sorting the inbox, journey B), P3 | UI only; undo must group several events (ADR-059 detail) | no | **Take** – sorting many inbox items one by one is slow on touch. |
| 9 | Move via search without leaving the place | DL | Pick a target by search, item moves, user stays. | Serves P3, P4 | none | D-6, D-7, D-9 | **Take** (done) – already decided. |
| 10 | Mirrors / live clones (one item in several places) | WF (mirrors), LS (block embeds), Tana | The same item appears and is editable in several places of the tree. | Serves P6 at first sight; conflicts with one-parent model | new events or ADR (contradicts ADR-023) | ADR-023, relations ADR-024 cover the need | **Leave** – relations and backlinks give the second place without breaking one parent per item. |
| 11 | Node types (heading, quote, code block, divider, paragraph) | WF, DL (headings) | Each line can be typed as heading, quote, code etc. | Pulls the outline towards a document editor; against P8 | new events or ADR | Markdown in titles and notes (D-70) | **Leave** – inline Markdown in titles and full Markdown in notes already cover it. |
| 12 | Sort children once (by title, by last change) | DL | A command that reorders an item's children. | Serves P4 when tidying | UI only (writes move events) | no | **Consider later** – small, but only when use shows the need (P8). |
| 13 | Folded editing / typewriter / distraction-free mode | OO | Collapse everything except the row being edited; keep the line at a fixed height. | Serves P3; overlaps Focus | UI only | Focus (D-57, D-77) | **Leave** – Focus already gives the concentrated view. |
| 14 | Document statistics (items, words) | OO | Counts of rows, words, characters for a document or selection. | Neutral; counts can feel like pressure (P5) | UI only | D-8 (calm neutral count for inbox) | **Leave** – no clear need for thinking. |

### Navigation & focus

| # | Idea | Found in | What it is | Serves / violates | Architecture impact | Already covered? | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 15 | Zoom/hoist into an item with breadcrumb | all five, CV | Show one item's branch as if it were the document. | Serves P3, P4 | none | D-57, D-77, ADR-065 | **Take** (done) – Focus is a ThoughtTree killer feature already. |
| 16 | Structure sidebar on tablet and desktop | OO (Sections tab; selecting a section focuses it), DL (document pane), LS (left sidebar) | A narrow, always visible list of top-level (or chosen-depth) items; tapping one focuses it, the main pane shows the branch. | Serves P4 (far by choosing), P3 | UI only (collapsed state as device state, ADR-013) | no (layout for tablet not yet decided) | **Take** – uses the wide screen, makes Focus one tap away, and shows branch themes at a glance. |
| 17 | Side-by-side panes | WF (Panes, 2026.08, desktop), LS (right sidebar), OO (several windows on one document) | Two parts of the tree open at once, edits show in both. | Serves P4 (drag far without "Move to…"), P6 | UI only (device state) | no | **Consider later** – valuable on tablet landscape and desktop after the sidebar exists. |
| 18 | Bookmarks / starred places and searches | DL (bookmarks), WF (starred searches in sidebar) | Quick list of chosen items or searches. | Serves P4 | UI only (device state) or shared events | D-9 (pinned places parked), D-39 (views on items), D-40 (start view) | **Consider later** – views on items cover saved searches; revisit pinned places with the sidebar (idea 16). |
| 19 | Item finder / command palette | DL (item finder), CV (command palette) | Keyboard field to jump to any item or run any command by name. | Serves P4 on desktop; accessibility (ADR-033) | UI only | Search D-12, target picker D-9 | **Consider later** – desktop add-on; reuse the target picker. |
| 20 | Complete keyboard shortcut set with "?" help | CV, DL (custom shortcuts), OO | Every action reachable by keys, with an overlay listing them. | Serves accessibility (ADR-033), P3 on desktop | UI only | Partly D-2, D-3 (Tab, Shift+Tab) | **Consider later** – needed for desktop in release 1 (D-51); custom remapping can wait. |
| 21 | Expand to level n | OO (expand/collapse all levels), WF | Show the tree down to a chosen depth in one step. | Serves P4 for overview | UI only (device state) | D-79 (collapse all / expand all) | **Consider later** – natural extension of D-79 once in use. |

### Display & themes

| # | Idea | Found in | What it is | Serves / violates | Architecture impact | Already covered? | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 22 | Styles per nesting level | OO (structural styles "Level n Rows") | Font size, weight or colour follow the depth: level 1 large, deeper levels smaller. | Serves P5 (calm hierarchy) if fixed by theme; per-user style editing would break P8 | UI only (part of theme tokens, ADR-060) | Themes D-34 to D-36, D-42 | **Consider later** – as a property each theme defines, never as a user style editor. |
| 23 | Notes shown fully in the outline | OO (notes below the row, spanning columns), DL (toggle notes) | Option to show the whole note under each title instead of only its first line. | Serves P3 (read without opening), P2 stays (writing in detail view) | UI only (device-state toggle or view option) | Partly D-42 (first line preview) | **Take** – a "Show notes" toggle for reading and review sessions; editing stays in the detail view. |
| 24 | Density choices | DL (cozy, comfortable, compact) | Row height and text size presets. | Serves accessibility, P5 | none | D-42, D-43 | **Take** (done) – already in themes. |
| 25 | Theme follows system light/dark | OO (Dynamic Themes) | Theme swaps its palette when the device switches dark mode. | Serves P5 | none | D-33 (light and dark values) | **Take** (done). |
| 26 | Color labels on items | DL (color labels), Tana (supertag colours) | A colour marker on a single item. | P6 if used for meaning; risk: becomes priority colour (MLO trap), P5 | new events or ADR | D-15 (tag colours parked), themes per branch D-34 | **Leave** – branch themes and tags carry mood and meaning; a later tag colour is the safer route. |
| 27 | Automatic numbering of children | WF (numbered lists), DL, OO | Children show 1., 2., 3. in their order. | Neutral; helps arguments and sequences | UI only (display option per item, could live in its view) | no | **Consider later** – small, fits as an option of an item's outline view (ADR-062). |
| 28 | Custom CSS | DL | User-written stylesheet for the app. | Against accessible, consistent themes; security | new ADR (conflicts with ADR-045 strict CSP, ADR-052) | no | **Leave** – themes are the controlled way. |

### Search, tags & links

| # | Idea | Found in | What it is | Serves / violates | Architecture impact | Already covered? | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 29 | Search filters the outline in place, results editable | WF, OO (filter bar), DL | Typing filters the tree to matches and ancestors; matches can be edited right there. | Serves P3, P4 | none | D-12 | **Take** (done) – check that results stay editable in place. |
| 30 | Inline item links typed in text (`[[`) | DL, LS, WF (links and backlinks) | Typing `[[` in a title or note suggests items; the link shows the target's current title and creates a backlink. | Serves P2 (shortcut in the text, like `##` and `@@`), P6 | new events or ADR (how text links relate to relations, ADR-024) | D-13 (Link to… from the menu), D-63 (backlinks) | **Take** – makes linking as fast as tagging; needs an ADR on links in text vs. relations. |
| 31 | Unlinked mentions | LS (unlinked references) | The detail page lists other items whose text mentions this item's title without a link, with "Link" to confirm. | Serves P6, journey C; P9 because the user confirms each link | UI only (on-device search index) | no | **Consider later** – good for finding connections between thoughts, after idea 30. |
| 32 | Tag overview with counts | DL (tag pane, click to filter) | A list of all tags with item counts; tapping filters the tree. | Serves P6, P4 | UI only | Partly D-64 (picker with counts), D-15 (managing parked) | **Consider later** – part of the parked tag management (D-15). |
| 33 | Saved filters with AND/OR/NOT and operators | OO 6 (nested rules), WF (`in:backlink`, `link:`, day-of-week) | Structured queries combining words, tags, links and dates. | Serves P6; complexity against P8 | new events or ADR (new query format version, ADR-062) | ADR-062 (structured query with format version) | **Consider later** – extend the query format when single filters are not enough. |
| 34 | Typed fields / supertags / properties | Tana, LS (properties), OO (typed columns) | Items of a kind get named fields with types (date, number, link). | Against P1 (decide the kind before typing) and P8; pulls to databases and tasks | new events or ADR | Tags D-14 cover "kind" | **Leave** – an item's kind is the user's tag, not a schema. |
| 35 | Graph view of links | LS (graph view) | Picture of items as dots joined by links. | Little everyday use; P8 | UI only (layout under ADR-062) | no | **Leave** – attractive but rarely helps to think; backlinks on the detail page suffice. |

### Views

| # | Idea | Found in | What it is | Serves / violates | Architecture impact | Already covered? | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 36 | Article view: a branch read as continuous text | DL (article view) | Titles become headings and notes become paragraphs, read like a document. | Serves the vision "work out details"; P3 when reviewing a grown idea | UI only (new layout id, ADR-062) | Layouts later (D-39, D-41) | **Consider later** – the most useful first layout after the outline for thoughts that grew; close to export as Markdown. |
| 37 | Boards (children as columns, grandchildren as cards) | WF (boards), Tana | Kanban-like layout of a branch. | P6; risk of status columns (MLO trap) | UI only (layout id, ADR-062) | D-41 (parked) | **Consider later** – already parked; useful for clustering ideas, not for task status. |
| 38 | Mind-map view | DL | Branch shown radially around its root. | P6, creative thinking | UI only (layout id, ADR-062) | Layouts later (D-39) | **Consider later** – touch-heavy to build well; after article view. |
| 39 | Review of dormant thoughts | MLO (Review view, adapted) | A view listing items not changed for a long time, to look at calmly when the user chooses. | Serves the GTD part of the vision; must stay P5 (no reminders, no counts) | new events or ADR (new query term "not changed since", ADR-062) | Partly D-21 ("changed recently") | **Consider later** – the calm, thought-oriented part of MLO's review, without dates or nagging. |
| 40 | Whiteboard / free canvas | LS (whiteboards) | Items placed freely on a 2D canvas. | Far from the outline core; P8 | new events or ADR (positions per item) | no | **Leave**. |

### Sync, export & sharing

| # | Idea | Found in | What it is | Serves / violates | Architecture impact | Already covered? | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 41 | Automatic backup copy to a chosen folder | DL (daily backups to Dropbox / Google Drive), LS (Markdown Mirror kept up to date) | The app regularly writes a readable or encrypted copy to a folder the user picked. | Serves P7 and quality goal 1 (reliability); P9 if it shows when it last ran | UI only (File System Access on Chromium; store app elsewhere) | Readable export ADR-019; export reminder (D-50) | **Take** – where the platform allows, turn the export reminder into a quiet automatic copy; elsewhere keep the reminder. |
| 42 | Link to a specific item, usable from other apps | OO (Omni Links), LS | "Copy link" to an item that opens the app at that item. | Serves P4, P3 | UI only (start parameter exists) | D-40 (address with item plus view) | **Take** – expose D-40 as "Copy link to item"; the link must reveal nothing without the tree's key. |
| 43 | Item history / version history | DL, OO (iCloud conflict handling) | Earlier states of an item can be seen and restored. | Serves P7, P9 | none | D-25 to D-29, D-22, D-23 | **Take** (done). |
| 44 | Public sharing by link | DL, WF (view-only shared lists), LS (publish) | A web page anyone with the link can read. | Conflicts with end-to-end encryption and no server accounts | server (and new ADR) | ADR-011, ADR-020 | **Leave** – export as readable Markdown/HTML covers sharing a thought. |
| 45 | Calendar sync and daily email digest | WF (Google Calendar sync, Today Digest), DL | Dates in items sync to an external calendar; a morning email lists them. | Against P5 and privacy; MLO trap | server | no | **Leave**. |
| 46 | Automation API, plug-ins, AI on content | DL (API), LS (CLI, plug-ins), OO (Omni Automation, on-device models), WF (AI nodes) | Scripts or AI read and change the outline. | Against privacy goal unless strictly on device; P9 | server / new ADR (ADR-045, ADR-052) | no | **Leave** for now – revisit only for on-device features. |

### Media

| # | Idea | Found in | What it is | Serves / violates | Architecture impact | Already covered? | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 47 | Images in an item (photo, camera, files), resizable | OO (attachments from Photos, camera, Files), DL, WF | Pictures attached to the note and shown inline. | Serves P1 (capture a whiteboard photo in a meeting) | binary data (ADR-022) | Terms: "later also pictures and files" | **Consider later** – planned; follow ADR-022 open points (blob ids, padding). |
| 48 | Audio recording in an item | OO | Record speech directly into a document. | Serves P1 for talks, but large data | binary data (ADR-022) | no | **Consider later** – after images; dictation to text covers most needs first. |
| 49 | PDF viewer and annotation | LS, WF (PDF viewer) | Read and highlight PDFs, highlights become items. | Far from thought capture; P8 | binary data | no | **Leave**. |

### Task-management (MLO trap)

| # | Idea | Found in | What it is | Serves / violates | Architecture impact | Already covered? | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 50 | Checkboxes, completing items, hide completed | OO (status checkboxes), WF, DL (checklists), LS, CV | Each item can be ticked off and hidden when done. | **MLO trap**; P5 | new events or ADR | Example content rule D-67 (thoughts, not tasks) | **Leave** – if "settled" thoughts need marking, a tag (`@@done`) and a saved search do it without a task model. |
| 51 | Parent checkbox showing mixed state / progress summary | OO (checked / unchecked / mixed) | Parent reflects how many children are ticked. | **MLO trap**; P5 (progress pressure) | new events or ADR | no | **Leave**. |
| 52 | Typed columns with sums (number, date, duration, pop-up) | OO (columns, Total summary), Tana (fields in tables) | Spreadsheet-like columns next to the outline with totals. | **MLO trap** and spreadsheet drift; against P8 | new events or ADR | no | **Leave**. |
| 53 | Start / due dates and recurring items | MLO, DL, LS, CV, WF (dates, Mirror To Date) | Items carry dates and come back on schedule. | **MLO trap**; P5 | new events or ADR | Vision: "appointments" not decided | **Leave** – if appointments with oneself are wanted, hand them to the calendar app (export one event), not into the tree. |
| 54 | Importance, urgency, computed priority to-do list | MLO | Scores per item produce a ranked action list. | **MLO trap** | new events or ADR | no | **Leave**. |
| 55 | Contexts as task filters (with open hours) | MLO | Places or tools where an action can be done, used to filter tasks. | **MLO trap** (contexts-as-tasks) | new events or ADR | Tags D-14 can hold a context as a plain label | **Leave** – tags already allow "context" as meaning without task logic. |

**Count:** 55 rows, of which 9 are already covered and marked "Take (done)". New ideas: 46.

## 3. Top 8 to discuss first

1. **Structure sidebar on tablet and desktop (16)** – makes Focus one tap away and shows branch themes at a glance on the devices the owner uses for sorting.
2. **Paste indented text as several items (7)** – notes from a meeting or another app land as a structure in one step, true to "put them in context now quickly".
3. **Multi-select with bulk actions (8)** – sorting a full inbox on touch is the slowest part of journey B today.
4. **Inline item links typed with `[[` (30)** – linking becomes as fast as `@@` tagging, the text-shortcut pattern of principle 2; needs one ADR on links in text vs. relations.
5. **Notes shown fully in the outline as a toggle (23)** – lets the user read a grown idea without opening each item, while writing stays in the detail view.
6. **Automatic backup copy to a chosen folder (41)** – turns the export reminder into quiet reliability (quality goal 1) where the browser allows it.
7. **Styles per nesting level as a theme property (22)** – strengthens the owner's core wish (themes per branch) with a calm visual hierarchy, without a style editor.
8. **Article view of a branch (36)** – the first layout beyond the outline that serves thoughts, not tasks: read a project as a text, and a natural bridge to Markdown export.
