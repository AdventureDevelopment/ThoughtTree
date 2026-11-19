# Information architecture: check for release 1

Status: check of topic 7 of [ux-concept.md](../ux-concept.md) on 2026-11-18, against D-1 to D-165, ADR-060 to ADR-072, ADR-074, ADR-077 to ADR-081, [zettelkasten-claim.md](../zettelkasten-claim.md) and the open decisions in section 9 of [architecture.md](../architecture.md). This file decides nothing; it lists where every place lives and what is missing. Decisions are recorded in the log of ux-concept.md.

**Verdict: not done.** The phone core (outline, Focus, capture, inbox, search in one tree, trees, history, conflicts, settings, feedback, tutorial) is decided and consistent. Missing are the places of the release 1 features added since 2026-11-16 (connection map, relation notes, readable import and export, all-trees search, tree-level settings, and since 2026-11-19 the Tags and Relation types branches and the beta format-change prompt), the entry to pairing, and how the item menu holds its new entries. Progress since the check: see D-166 to D-176.

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
| All-trees search | Not in release 1; idea for later | None | D-186 | Later (out of release 1) |
| Trees: switch, new | List under the tree's name at the top | Tap "My thoughts ▾"; wide: navigation column | D-115 to D-118, D-164 | Decided |
| Trees: manage | More › Trees, ⋯ per tree: Rename, Theme, Remove from this device | More › Trees | D-119, D-163 | Decided |
| Tree settings (tree features: headings ADR-079, tags ADR-082, relations ADR-084) | More › This tree… (replaces "Theme of this tree") | More › This tree… | D-183, D-184 | Decided |
| Themes | Theme picker with live preview | Item ⋯ › Theme; More › Theme of this tree; More › Trees › ⋯ › Theme | D-34, D-35, D-65, D-92, D-163 | Decided (one double path, C2) |
| History of a thought | Sheet | Item ⋯ › History; 🕘 in the Focus line; wide: details column | D-85, D-151 | Decided |
| History of a tree (exports, ADR-078) | On More › This tree… | More › This tree… | D-183, D-184 | Decided |
| Links and Linked from | One sheet, two tabs | ↗ / ↙ in the Focus line; wide: details column | D-85, D-114, D-121, D-151 | Partly (G9, C3) |
| Link to… (new relation) | Target picker | Item ⋯ › Link to… | D-9, D-13 | Decided |
| Relation note | Under the relation in the Links sheet, map card and side column; relation sheet | ＋ or ⋯ on a relation; tap a line on the map | ADR-077, D-175, D-176 | Decided |
| Relation type of one relation | Type chip in the relation sheet (`relation.type-changed`, ADR-083) | Relation ⋯ › type chip | D-13, D-175, ADR-083 | Decided |
| Relation types (manage) | Items in the marked branch "Relation types"; a new tree has only `related` (ADR-084) | More › Relation types; wide: navigation column; a type's page sets reverse name and symmetry | ADR-084, D-177, D-178 | Decided |
| Connection map | Own place; wide: map in the middle, selected thought at the side | Item ⋯ › Map; ← Back | D-167 to D-174; row "Connection map (follow-ups)" | Decided |
| Tags: add, remove, rename | Chips in Focus, tag picker, tag popover | `@@`; chip ＋; tap a chip | D-14, D-64, D-90 | Decided |
| Tags (manage: list, describe, nest, delete, join) | Items in the marked branch "Tags" (ADR-082); renaming a tag is a title change | More › Tags; tag chip › Open the tag; wide: navigation column | ADR-082, D-177, D-178 | Decided |
| Conflicts | Mark on the thought; More › Conflicts while any exist; top of the history | Exchange summary "Show both versions"; More › Conflicts | D-22, D-23, D-127, D-165, D-163 | Decided |
| Exchange by file (round trip) | Flow from More | More › Exchange with another device | D-19 to D-21, D-126 | Partly (no sketch) |
| Pairing a device | More › This tree… › Pair another device…; also prompted by a file from another tree | More › This tree… | D-81, D-84, D-163, D-187 | Partly (pairing screens need a sketch) |
| Import a tree into another (may slip) | Step after pairing | Not placed | D-84 | Partly |
| Readable export of a branch | Item menu and Focus | Item ⋯ › Export…; in Focus the ⋯ of D-166 | ADR-080, D-180 | Decided |
| Readable import into a thought | Item menu and Focus | Item ⋯ › Import into this thought… | ADR-080, ADR-078, D-180 | Decided |
| Readable export of the tree, import as a new tree (vault) | More › Trees › ⋯ › Export… (encrypted copy first, D-182); tree list › ＋ Import a tree… | More › Trees; the tree's name at the top | D-179, ADR-019, ADR-078, D-180 | Decided |
| Import report | An ordinary thought at the end of the imported branch | Outline | Zettelkasten decision 4d | Decided |
| Settings (device) | More › Settings: hand, language (also Easy), light or dark, export reminder, offer feedback, device label and name | More › Settings; wide: navigation column | D-122, D-126, D-146, ADR-070, architecture section 6 | Partly (G10) |
| App update consent, performance report | Not placed | Not decided | ADR-036, ADR-051 | Missing (G10) |
| Beta: format-change prompt, export of every tree, import after the update | Banner, then "Before you update" sheet; after the update the tree list | Banner › Before you update; tree list › ＋ Import your exported trees… | ADR-085, D-181 | Decided |
| Reset or remove all data from this device | Not placed (the prototype had one, D-80) | Not decided | D-80 | Missing (G11) |
| Feedback | Line at the end of menus and sheets, button at the top, More › Send feedback | As described | D-123, D-124, D-159, ADR-070 | Decided |
| Tutorial | A tree, opened first; back by More › Trees | First start; More › Trees › "Bring the tutorial back" | D-69, D-134 to D-147, D-163 | Decided (C4) |
| Templates (may slip) | "Templates · n" row in a branch | `::`; hold ＋ › From template… | D-108 to D-113 | Partly (link slots, D-111) |
| Sort mode, copy and paste | Band and action bar | Item ⋯ › Sort; More › Sort; ⋯ › Copy, Paste below; More › Paste at the top level | D-88, D-120 | Decided |
| Diagnostics view (may slip) | Not placed | Not decided | Release 1 scope 3 | Missing (low) |
| Back | Device back, ← Back, iPhone edge swipe | Everywhere | D-131, D-132 | Decided |

## Gaps (G) and conflicts (C)

- **G1 Connection map (closed by D-168 to D-174).**
- **G2 Relation note and relation type (closed by D-175):** a relation's type is changed with the type chip (ADR-083); managing the types themselves is G12.
- **G3 Readable import and export (closed by D-179, D-180).**
- **G4 Tree-level settings (closed by D-183 to D-185).**
- **G5 The focused thought's ⋯ (closed by D-166):** D-151 removed the strip with its ⋯ (D-65, D-66); Focus now has no item menu of its own, but ADR-080 needs one there, and Move to…, Theme and Delete of the focused thought have no path except ← Back first.
- **G6 Length of the item menu (deferred by D-167; simplest form built first, ideas in open question 20):** D-59 had 9 entries; with Copy, Paste below, Sort, Export…, Import into this thought… and the map it grows to about 14. D-80 allows one level only, so it needs grouping or a decision on what stays.
- **G7 All-trees search (closed by D-186):** not in release 1; search stays per tree.
- **G8 Pairing (placed by D-187):** the screens for showing and scanning the code still need a sketch.
- **G9 Linked thought in the Links sheet:** where tapping leads is an [ASSUMPTION] in D-85; D-121 answers it only for links in text.
- **G10 Settings list:** D-122 does not list update consent (ADR-036), the performance report (ADR-051), device label and name, or "Offer feedback" (ADR-070, named in D-123).
- **G11 Reset:** whether release 1 offers "remove all data from this device" (beyond removing one tree) is not decided.
- **G12 Tags and relation types branches (closed by D-177, D-178).**
- **G13 Beta format change (closed by D-181).** The ordinary update banner ("A new version is ready · Update") is named there too; the rest of update consent (G10) stays open.
- **C1 Export in More (resolved by D-179):** no Export entry in More; whole-tree export is in More › Trees.
- **C2 Tree theme in two places (settled by D-183):** the open tree's theme on More › This tree…, any tree's in More › Trees › ⋯; intended.
- **C3 Name of backlinks from text:** "Mentioned in" (D-85, D-111) or "Linked from" (architecture section 8); open in the architecture row "Predefined relation types in new trees".
- **C4 Tutorial wording (resolved):** architecture.md now uses "Bring the tutorial back" (D-163).
- **C5 Wide screens:** the navigation column (D-150) repeats parts of More (trees, views, settings); no rule says which More entries it shows, or where the map and conflicts appear there.
- **Depth:** every path above is at most two taps from the outline (More › X, ⋯ › X); D-80 holds except where G6 forces a submenu.
- **New Zettelkasten features against the principles:** the map and the relation note fit principle 6 (several dimensions); the item-menu entries for export and import fit "actions where their subject is" (D-53). Readable export must keep the ADR-019 warning in every path, including the quick branch export.

## Questions for the owner, in proposed order

1. G6: deferred by D-167 until tried (G5 closed by D-166).
2. G1 and G2: closed (D-168 to D-176).
3. G3 and C1: closed (D-179, D-180).
4. G4: closed (D-183 to D-185).
5. G8, G10, G11: pairing entry, settings list, reset.
6. G12 and G13 closed (D-177, D-178, D-181).
