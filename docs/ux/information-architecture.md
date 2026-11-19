# Information architecture: check for release 1

Status: check of topic 7 of [ux-concept.md](../ux-concept.md) on 2026-11-18, against D-1 to D-165, ADR-060 to ADR-072, ADR-074, ADR-077 to ADR-081, [zettelkasten-claim.md](../zettelkasten-claim.md) and the open decisions in section 9 of [architecture.md](../architecture.md). This file decides nothing; it lists where every place lives and what is missing. Decisions are recorded in the log of ux-concept.md.

**Verdict: not done.** The phone core (outline, Focus, capture, inbox, search in one tree, trees, history, conflicts, settings, feedback, tutorial) is decided and consistent. Missing are the places of the release 1 features added since 2026-11-16 (connection map, relation notes, readable import and export, all-trees search, tree-level settings), the entry to pairing, and how the item menu holds its new entries.

Status: **Decided** = place and path decided; **Partly** = place decided, flow or detail open; **Missing** = no place decided. Phone paths; tablet and desktop use the same paths unless noted (D-157: same bar and 📥 everywhere; D-150 and D-156: three columns on wide screens).

## Places and paths

| Place | Where it lives | How the user gets there | Decided by | Status |
| --- | --- | --- | --- | --- |
| Outline of a tree | Main screen; on wide screens the middle column | Start: place last worked on (D-4, D-116) | D-4, D-52, D-71, D-91, D-95, D-150 | Decided |
| Focus (the thought as a page) | Replaces the outline; wide: middle column, details column is its preview | Item ⋯ › Focus, swipe on a row, link preview "Focus", details column "Focus"; ← Back, breadcrumb | D-77, D-96, D-151, D-155, D-163 | Decided |
| Item menu ⋯ | Popover at the selected row's ⋯ (hand side); in Focus at the end of the line under the heading | Select a row, tap ⋯; in Focus tap ⋯ in the 🕘 ↗ ↙ line | D-53, D-58, D-59, D-151, D-166, D-167 (provisional) | Partly (G6 deferred) |
| Capture sheet 📥 | Sheet over every screen, also in Focus and while typing | Floating 📥; wide: corner of the middle column | D-18, D-52, D-54, D-87, D-102 | Decided |
| Inbox | Last item at the top level of every tree | Expand in the outline; More › Inbox (full item menu) | D-68, ADR-066, D-82 | Decided |
| Search in the tree or branch | Field over the outline, filters in place | 🔍 in the bar; Ctrl/Cmd+F assumed (D-157) | D-12, D-97, D-98, D-101, D-103 to D-105 | Decided |
| Tag filter | Filter row like search | Tap a tag chip; tag popover "Show all with …" | D-14, D-64 | Decided |
| Views (may slip) | Line under the search options | 🔍 › "View: Outline ▾"; tree level "All views"; wide: navigation column | D-39, D-125, D-150 | Decided |
| All-trees search | Not placed | Not decided | Architecture row "All-trees search (UX)" | Missing (G7) |
| Trees: switch, new | List under the tree's name at the top | Tap "My thoughts ▾"; wide: navigation column | D-115 to D-118, D-164 | Decided |
| Trees: manage | More › Trees, ⋯ per tree: Rename, Theme, Remove from this device | More › Trees | D-119, D-163 | Decided |
| Tree settings (tree features, ADR-079) | Not placed | Not decided | ADR-079, architecture row "Headings in notes (UX)" | Missing (G4) |
| Themes | Theme picker with live preview | Item ⋯ › Theme; More › Theme of this tree; More › Trees › ⋯ › Theme | D-34, D-35, D-65, D-92, D-163 | Decided (one double path, C2) |
| History of a thought | Sheet | Item ⋯ › History; 🕘 in the Focus line; wide: details column | D-85, D-151 | Decided |
| History of a tree (exports, ADR-078) | Not placed | Not decided | ADR-078 | Missing (G4) |
| Links and Linked from | One sheet, two tabs | ↗ / ↙ in the Focus line; wide: details column | D-85, D-114, D-121, D-151 | Partly (G9, C3) |
| Link to… (new relation) | Target picker | Item ⋯ › Link to… | D-9, D-13 | Decided |
| Relation note | Not placed | Not decided | ADR-077, row "Relation note (UX)" | Missing (G2) |
| Relation type: change, manage | Not placed ("can be changed afterwards", D-13) | Not decided | D-13; row "Tags and relation types as items" | Missing (G2, parked part) |
| Connection map | Own place; wide: map in the middle, selected thought at the side | Item ⋯ › Map; ← Back | D-167 to D-170; row "Connection map (follow-ups)" | Partly (keyboard, layout, zoom open) |
| Tags: add, remove, rename | Chips in Focus, tag picker, tag popover | `@@`; chip ＋; tap a chip | D-14, D-64, D-90 | Decided |
| Tags: list of all, delete everywhere | Parked | Parked | Tags open question 3; row "Tags and relation types as items" | Parked |
| Conflicts | Mark on the thought; More › Conflicts while any exist; top of the history | Exchange summary "Show both versions"; More › Conflicts | D-22, D-23, D-127, D-165, D-163 | Decided |
| Exchange by file (round trip) | Flow from More | More › Exchange with another device | D-19 to D-21, D-126 | Partly (no sketch) |
| Pairing a device | Only the "file from another tree" case | Inside the exchange flow; start of pairing not placed | D-81, D-84, D-163; architecture section 6 | Missing (G8) |
| Import a tree into another (may slip) | Step after pairing | Not placed | D-84 | Partly |
| Readable export of a branch | Item menu and Focus | Item ⋯ › Export…; in Focus the ⋯ of D-166 | ADR-080 | Partly (G3) |
| Readable import into a thought | Item menu and Focus | Item ⋯ › Import into this thought… | ADR-080, ADR-078 | Partly (G3) |
| Readable export of the tree, import as a new tree (vault) | D-53 put Export in More; D-122's More list drops it | Not decided | D-53, D-122, ADR-019 | Missing (G3, C1) |
| Import report | An ordinary thought at the end of the imported branch | Outline | Zettelkasten decision 4d | Decided |
| Settings (device) | More › Settings: hand, language (also Easy), light or dark, export reminder, offer feedback, device label and name | More › Settings; wide: navigation column | D-122, D-126, D-146, ADR-070, architecture section 6 | Partly (G10) |
| App update consent, performance report | Not placed | Not decided | ADR-036, ADR-051 | Missing (G10) |
| Reset or remove all data from this device | Not placed (the prototype had one, D-80) | Not decided | D-80 | Missing (G11) |
| Feedback | Line at the end of menus and sheets, button at the top, More › Send feedback | As described | D-123, D-124, D-159, ADR-070 | Decided |
| Tutorial | A tree, opened first; back by More › Trees | First start; More › Trees › "Bring the tutorial back" | D-69, D-134 to D-147, D-163 | Decided (C4) |
| Templates (may slip) | "Templates · n" row in a branch | `::`; hold ＋ › From template… | D-108 to D-113 | Partly (link slots, D-111) |
| Sort mode, copy and paste | Band and action bar | Item ⋯ › Sort; More › Sort; ⋯ › Copy, Paste below; More › Paste at the top level | D-88, D-120 | Decided |
| Diagnostics view (may slip) | Not placed | Not decided | Release 1 scope 3 | Missing (low) |
| Back | Device back, ← Back, iPhone edge swipe | Everywhere | D-131, D-132 | Decided |

## Gaps (G) and conflicts (C)

- **G1 Connection map (closed by D-168 to D-170):** the keyboard path, zooming and the layout assumptions of the sketch are still open.
- **G2 Relation note and relation type:** no place to read or write the reason; no place to change a relation's type (D-13). Sketch in Task 2.
- **G3 Readable import and export:** the branch entries exist (ADR-080), but the whole-tree export and the import of a vault as a new tree have no entry, and the flow (formats, Obsidian form, warning, progress, report) is not sketched.
- **G4 Tree-level settings:** "Headings become thoughts" (ADR-079) and the tree's history (export events, ADR-078) have no place; More › Trees › ⋯ holds only Rename, Theme, Remove.
- **G5 The focused thought's ⋯ (closed by D-166):** D-151 removed the strip with its ⋯ (D-65, D-66); Focus now has no item menu of its own, but ADR-080 needs one there, and Move to…, Theme and Delete of the focused thought have no path except ← Back first.
- **G6 Length of the item menu (deferred by D-167; simplest form built first, ideas in open question 20):** D-59 had 9 entries; with Copy, Paste below, Sort, Export…, Import into this thought… and the map it grows to about 14. D-80 allows one level only, so it needs grouping or a decision on what stays.
- **G7 All-trees search:** scope choice, hits from other trees, going back. Sketch in Task 2.
- **G8 Pairing:** nothing says where pairing starts (show or scan a code) or where the device label and name are asked (architecture: at the first pairing).
- **G9 Linked thought in the Links sheet:** where tapping leads is an [ASSUMPTION] in D-85; D-121 answers it only for links in text.
- **G10 Settings list:** D-122 does not list update consent (ADR-036), the performance report (ADR-051), device label and name, or "Offer feedback" (ADR-070, named in D-123).
- **G11 Reset:** whether release 1 offers "remove all data from this device" (beyond removing one tree) is not decided.
- **C1 Export in More:** D-53 lists Export in More; D-122, which revises More, does not. One of them must be confirmed.
- **C2 Tree theme in two places:** More › Theme of this tree (D-122) and More › Trees › ⋯ › Theme (D-119). Probably intended (open tree versus any tree), but not stated.
- **C3 Name of backlinks from text:** "Mentioned in" (D-85, D-111) or "Linked from" (architecture section 8); open in the architecture row "Predefined relation types in new trees".
- **C4 Tutorial wording:** the architecture says "Show the tutorial again"; D-163 chose "Bring the tutorial back". Non-log text in the architecture should follow D-163.
- **C5 Wide screens:** the navigation column (D-150) repeats parts of More (trees, views, settings); no rule says which More entries it shows, or where the map and conflicts appear there.
- **Depth:** every path above is at most two taps from the outline (More › X, ⋯ › X); D-80 holds except where G6 forces a submenu.
- **New Zettelkasten features against the principles:** the map and the relation note fit principle 6 (several dimensions); the item-menu entries for export and import fit "actions where their subject is" (D-53). Readable export must keep the ADR-019 warning in every path, including the quick branch export.

## Questions for the owner, in proposed order

1. G6: deferred by D-167 until tried (G5 closed by D-166).
2. G1: the rest of the map sketch (zoom, keyboard, layout).
3. G3 and C1: where whole-tree export and the vault import as a new tree live.
4. G4: one place for tree settings (features, history, theme, rename, remove).
5. G8, G10, G11: pairing entry, settings list, reset.
