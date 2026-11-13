# Prototype review: ThoughtTree phone prototype compared with the UX concept

Source: `artifact-c7fcad9b-1794579761-1400.html` (826 lines, read in full). Compared with `docs/ux-concept.md` (D-1 … D-84, current versions only: D-52 not D-17, D-18 over D-10, D-84 not D-81, D-45 not D-44, D-39 not D-38) and, for context, ADR-059, ADR-066, ADR-067.
Line numbers ("L…") refer to the prototype file. Topic numbers refer to the concept's topic table (1 Principles, 2 Undo, 3 Components, 4 Tokens, 5 Layout, 6 Touch, 7 Information architecture, 8 States, 9 Onboarding, 10 Microcopy, 11 Agents).

---

## 1. New in the prototype (not covered by any decision yet)

### Outline: selection and editing

| # | Behaviour | Where | Topic |
| --- | --- | --- | --- |
| N1 | **Selection model "tap once selects, tap again edits".** Tapping a row's text selects it (selection colour, ⋯ appears); tapping the selected row again starts editing its title. On a keyboard, Enter on a focused row starts editing directly (no selection step). | `row()` L409–410, `select()` L502 | 6, 5 |
| N2 | **Focused title is edited with one tap** (no select step), unlike rows. | `renderTop()` L359–360 | 6, 7 |
| N3 | **Enter on an existing item** saves it and starts a new empty sibling directly after it (the same chaining as for new items). Exception: the focused item's title in the header (no chain, as D-77). | `editor()` L438, `commitDraft()` L456–457 | 6 |
| N4 | **Escape cancels the edit** and discards the draft (no save). | L439, `cancelDraft()` L448 | 6 |
| N5 | **Leaving the field saves**: on blur the draft is committed after 150 ms, unless focus moved into the editing toolbar or another title field. | L443 | 6, 1 (principle 7) |
| N6 | **An empty new line is never saved**: a new draft with no title, note or tag is discarded on commit. Clearing an existing item's title, however, saves an empty title, shown as "Untitled" (italic, muted); it does not delete the item. | `commitDraft()` L454–456, L405, CSS `.title.empty` L76 | 6, 10 |
| N7 | **While a title is edited, its tag chips and note preview are hidden** (the editor replaces the whole row body); tags are not shown as `@@name` text in the field either. | `row()` L404 | 6, 7 |
| N8 | **＋ Add here placement rules**: nothing selected → new last child of the current view (focused item or top level, before the inbox); an item selected → new sibling directly after it; the inbox selected → new last child inside the inbox (the inbox is expanded). | L812–813 | 5, 6 |
| N9 | **Indent nests at the end** of the item above (last child) and expands that item; "Nothing above to nest under" toast when there is no item above. Outdent at the top level, inside the inbox or at the focused item's level does nothing, silently. | `indentDraft()`/`outdentDraft()` L461–468, `moveRelative()` L472–481 | 6 |
| N10 | **Indent/outdent/move up/move down of an existing item while editing write the move at once** (as separate events, separate quick-undo steps from the text change); for a new, unsaved line they only move the draft position. | L472–484 | 6, 2 |
| N11 | **Typing undo/redo in the toolbar** go back character by character (each input event is one step of the draft history). | `editor()` L431–436, tools L494–495 | 2, 6 |
| N12 | **Invisible marker in empty lines** (zero-width space) so Backspace on an empty line is detected on every keyboard; the marker is removed from saved text. | L323, L430–442 | 6 |
| N13 | **Tag names typed with `@@`** may contain letters, digits, `_` and `-`; an existing tag is reused when names match ignoring case and accents, with German folding (ä→a, ö→o, ü→u, ß→ss; also "ae"→"a" etc.); otherwise a new tag is created. | `parseTitle()` L330–333, `ensureTag()` L328, `norm()` L321 | 7 (tags, open questions 1–2) |
| N14 | **Bottom bar hides while editing** (the toolbar takes its place), the floating 📥 stays. | `showTools()` L487 | 5 |

### Outline: display and navigation

| # | Behaviour | Where | Topic |
| --- | --- | --- | --- |
| N15 | **Highlight flash**: when the app brings an item into view (Go there, undo, back, up), it scrolls it to the centre and draws a short accent ring that fades in 1.4 s. | `flash()` L523, CSS L67–68 | 8, 2 |
| N16 | **Back without history goes one level up** (to the parent of the focused item) and flashes the item left. | `goBack()`/`goUp()` L504–509 | 5, 7 |
| N17 | **Breadcrumb at the top level** shows only "My thoughts"; when focused, ancestors are buttons separated by "›", scrolled so the end is visible; the last crumb is in bold. | `renderTop()` L353–365 | 7 |
| N18 | **"Go there" leaves Focus**: `goTo()` always returns to the whole tree, expands the item's ancestors, selects and flashes it. Used by Undo, Move's "Go there", exchange's "Show" and `item:` links. | `goTo()` L524–525 | 7, 2 |
| N19 | **Back-to-selection label is not shown when the inbox is selected.** | `updatePill()` L512 | 5 |
| N20 | **Empty states**: "Nothing matches." (search), "The inbox is empty. Tap 📥 to capture a thought." (focused on the inbox), "Nothing here yet. Tap ＋ Add here." (empty view). | L394 | 10, 8 |
| N21 | **Screen-reader announcements** through a polite live region: every toast text, "Captured", "Focused on …", "Showing the whole tree". | `announce()` L320, L503, L667 | 1 (accessible), 8 |
| N22 | **Escape closes any sheet or popover** (and cancels a theme preview); **Ctrl/Cmd+Z** outside text fields is quick undo. | L817–818 | 6 |
| N23 | **Swipe details**: only touch and pen (not mouse); the gesture is decided after 10 px; vertical or rightward movement cancels it; the row follows the finger; the "Open ›" hint sits at the row's end side; release beyond 70 px opens the detail page, otherwise the row snaps back (0.18 s); no swipe on a row being edited. | `addSwipe()` L416–425 | 6 |
| N24 | **Disabled menu entries explain themselves in the label** for the inbox: "Move to… (the inbox stays last)", "Delete (the inbox cannot be deleted)". The inbox title can be edited (renamed) and the inbox tagged and themed. Moving it with the toolbar gives "The inbox always stays last at the top level." | `itemMenu()` L543–547, L473 | 7, 10 |

### Item menu, move, tags, themes

| # | Behaviour | Where | Topic |
| --- | --- | --- | --- |
| N25 | **Delete has no confirmation**; it deletes at once and shows "Deleted "…"" with Undo. | L547–548 | 7, 2 |
| N26 | **Popover keyboard focus** goes to the first entry; the popover opens below the ⋯, or above it when there is no room. | `openPop()` L532–537 | 5 |
| N27 | **Move picker search** matches titles only (not notes), shows at most 12 matches with their path; "No item matches." otherwise. Targets that are the item itself or inside it are disabled. A chosen target is expanded afterwards. 8 recent targets are remembered, 5 shown; the inbox is never a recent target. | `movePicker()`/`doMove()` L552–572 | 7 |
| N28 | **Tag picker toggles at once**: tapping ☐/☑ writes the tag change immediately (no Apply); "＋ Create "…"" appears when no tag matches exactly; a hint points to `@@`. Tags sorted by number of items. | `tagPicker()` L575–582 | 7 |
| N29 | **Theme list**: Paper, Inbox ("Cool tray for what waits (draft)"), Sage ("Quiet green"), Dusk ("Evening blue"); every theme, including Inbox, can be chosen for any item. Tapping the dimmed background also cancels the preview. Hint: "A theme applies to this item's whole branch. Tap a theme to preview it." | `THEMES`/`themePicker()` L585–594, L529 | 4 |
| N30 | **Theme for the whole tree** exists in the data (`theme.set` with target "tree") but has no UI. | `project()` L255–259 | 4 |

### History and undo

| # | Behaviour | Where | Topic |
| --- | --- | --- | --- |
| N31 | **History scope "This branch / Only this item"**: for an item with children the history sheet shows two chips, default "This branch" (all events of items inside, including deleted ones, each entry labelled with the item's title and "· deleted"); "Only this item" shows its own events. Items without children have no chips. | `historySheet()` L608–618 | 2, 7 |
| N32 | **History entry contents**: a plain description ("Created "…" in the inbox", "Title → "…"", "Note changed", "Moved to …", "Deleted", "Restored", "Tagged @@…", "Untagged @@…", "Theme → …", suffix "(undo of an earlier change)"), date and time, device ("this device (Phone)" or the other device's label), "· reverted" if reverted, and a button "Revert this change" or, for an undo, "Revert this undo". Reverted entries are struck through and have no button. Empty: "No changes yet." | `describe()` L597–603, L613–618 | 2, 10 |
| N33 | **Quick undo jumps to the item**: after undo the item is shown (Go there, see N18, leaving Focus) and a toast "Undone: …" with Go there appears. Nothing left: "Nothing of yours to undo on this device." Undo first saves an open edit. | `quickUndo()` L636–644, L815 | 2 |
| N34 | **Tutorial content cannot be quick-undone** (seed events belong to device "Tutorial", not this device); history shows "Tutorial" as their device. | `seed()` L310, `quickUndo()` L637 | 2, 9 |
| N35 | **Undo of a move puts the item back directly after its former previous sibling**. | `inverse()` L627 | 2 |

### Capture sheet

| # | Behaviour | Where | Topic |
| --- | --- | --- | --- |
| N36 | **Context in the sheet**: above the input line, the last 3 existing inbox items are shown muted; each thought captured in this session is listed with ✓ and indented by its depth. | `capture()` L659–660 | 5, 7 |
| N37 | **Target line under the input**: "Next thought goes into the inbox" or "Next thought goes under "…"". | L662 | 10, 5 |
| N38 | **Nesting in the sheet can start under an existing inbox item**: before anything is captured, "above" is the last item already in the inbox, so a leading space or ⇥ nests the first new thought under it. | L647–649 | 6 |
| N39 | **Enter on an empty line does nothing**; ✓ saves a typed line and closes the sheet. **Close, tapping the dimmed background or Escape close the sheet without saving the text still in the input line.** | L657, L666, `openSheet()` L530, L529, L818 | 6, 1 (principle 7) |
| N40 | **"Details" button** in the sheet header opens the detail page of the last captured thought; before anything is captured it shows "Capture a thought first." | L671 | 7 |
| N41 | **The sheet appears in the inbox's theme** and has its own toolbar (⇤ ⇥ ## @@ ✓, no undo/redo/up/down); ⇤ is disabled at inbox level. | L654–657, L672 | 4, 6 |

### Detail page

| # | Behaviour | Where | Topic |
| --- | --- | --- | --- |
| N42 | **Empty note opens the editor at once** (keyboard up); a note with text is shown formatted and becomes editable on tap. | `openDetail()` L694–706 | 7 |
| N43 | **Note saving**: 1.2 s after the last keystroke, and on leaving the field, Back, History or ⋯. | L686, L696–703 | 7, 1 (principle 7) |
| N44 | **Title on the detail page is always an input** showing the raw Markdown; `##` and `@@` work there too (applied on change). The path above it ("My thoughts › …") is plain text, not tappable. | L679–680, L700 | 7 |
| N45 | **Note Markdown subset**: `#`–`###` headings, `-`/`*` and numbered lists, paragraphs with line breaks, `**bold**`, `*italic*`/`_italic_`, `` `code` ``, links. Links: only https, http, mailto (open in a new tab) and `item:` links (close the detail page and go to the item; label falls back to the item's title or "not found"). Formatting bar: B, I, H (inserts "## "), • (inserts "- "), 🔗 (inserts `[text](https://)`), ✓. | `mdInline()`/`mdBlocks()` L709–728, L691 | 7 |
| N46 | **Strip** shows "🕘 n" with the number of the item's events, and ⋯ at the end. | L702–703 | 7 |
| N47 | **Tag chip popover** on the detail page: "Remove from this item", "Show all with "…"", "Rename "…" everywhere…" → sheet "Rename tag" / "Changes the name on every item" / Rename. "Show all with" puts `@@name` into search; tag matching there is by substring of the tag name. | `tagMenu()`/`renameTag()` L734–738, `matches()` L367 | 7 |

### Search

| # | Behaviour | Where | Topic |
| --- | --- | --- | --- |
| N48 | **Search replaces the top bar**: input (focused at once, "Search titles and notes"), chip "Changed recently", ✕ to close. 🔍 in the bar toggles search on and off. | `renderTop()` L346–352, L814 | 7 |
| N49 | **Matching**: title, note and tag names; ignoring case and accents with German folding (see N13); `@@` before a word searches tags. While filtering, all branches are shown expanded and Markdown in titles is shown stripped. | `matches()` L367–368, L378–389, L405 | 7 |
| N50 | **Search scope is the current view**: when focused on an item, only matches inside its branch are shown. | `renderOutline()` L376–393 | 7 |
| N51 | **"Changed recently" = changed in the last 48 hours** (any change: title, note, move, tag, theme). Can be used with an empty query. | L368, L255 | 7 |
| N52 | **Highlight**: the first match in the title and in the note preview is marked; when the match lies further down in the note than its first line, nothing on the row shows it. | `highlight()` L369–373, L408 | 7 |

### More menu, exchange, toasts, keyboard

| # | Behaviour | Where | Topic |
| --- | --- | --- | --- |
| N53 | **More menu contents** (in order): "▸ Collapse all" / "▾ Expand all" side by side; "📥 Inbox" (opens the inbox's item menu anchored at More); "Exchange with another device"; "Hand: right/left" (toggles on tap); "Event log"; "About this prototype"; "Reset prototype…". | `moreSheet()` L741–752 | 7, 5 |
| N54 | **Exchange sheet** in two numbered steps "1 · Send" (copy to clipboard; fallback selects the text for manual copying) and "2 · Receive" (paste or choose a file). Result line names new, already known and rejected events. | `exchangeSheet()` L764–798 | 7, 8 |
| N55 | **Toasts**: one at a time, 5.2 s, actions as underlined text buttons; placed above the bottom bar, at the top while a sheet or the detail page is open. | `toast()` L520–522, CSS L110–113 | 8 |
| N56 | **Keyboard handling**: toolbars and sheets are lifted above the on-screen keyboard using the visual viewport (`--kb`). The floating 📥 and the toast are not lifted (fixed distance from the bottom). | `placeTools()` L488–489, CSS L93, L104, L110, L117 | 6, 8 |
| N57 | **Last place is restored on start** (focused item, collapsed state, hand, recent targets), but not the selection or scroll position. | `ui` L218–219, L821 | 5 (D-4) |
| N58 | **Device label is guessed** from the browser (Phone / Tablet / Desktop) and used in history and exchange texts. | L216 | 7, 10 |

### Tutorial (onboarding content and flow)

| # | Behaviour | Where | Topic |
| --- | --- | --- | --- |
| N59 | **Tutorial tree content**, all in the user's one tree "My thoughts", in this order: "👋 Welcome to ThoughtTree" (note: work through it, delete it whenever you like) with "Tap a line once to select it, tap again to edit it", "Swipe left on a line to open its note" (note demonstrating Markdown and an `item:` link back), "The arrow ▸ means there is more inside" (collapsed, 2 children), "⋯ on a selected line opens its actions"; "📥 Capture and sort" (4 items: 📥, Inbox at the bottom, ⋯ › Move to…, ↩ Undo); "✍️ Writing faster" (Enter, space, Backspace, `##`, `@@`); "🎨 Every branch can have its own mood" (themed Sage; Theme, Focus); "🔎 Finding things again" (search, tag on the detail page, Changed recently); "🔁 More than one device" (Exchange, History with "Every change is an event"). Inbox (note explains itself) with 2 items: a sorting exercise and a thought. Tag "tip" on 4 items. | `seed()` L270–312 | 9, 10 |
| N60 | **The tutorial teaches by doing**: lines are instructions to try on themselves; no separate screens, no end state or completion; it is removed by deleting its items. | `seed()` | 9 |

---

## 2. Differs from a decision

| Decision | Decided | Prototype |
| --- | --- | --- |
| **D-5 / D-18** | Capture view shows the target tree and one tap switches to another tree's inbox. | Sub-title "Into the inbox of "My thoughts"" (fixed text); no switching (one tree only). |
| **D-7** (second part) | Next item: one-tap choice "keep in inbox" or "place right below the last placed item". | `ui.lastPlaced` is stored (L571) but never used; no such choice exists. |
| **D-6 / D-9** | After choosing a target, its children are shown so the item can be placed at a chosen position. | Item always goes to the end of the target (L567); no placing step. |
| **D-9** | Browse the whole tree "as a compact, collapsed view". | Browse lists the tree fully expanded, but only 4 levels deep (L564); deeper items are reachable only by search. Search matches titles only. |
| **D-12** | A match in an item's note is marked on its node. | Only matches in the note's first line are visible (preview highlight); deeper matches are not marked (N52). |
| **D-14** | Typing `@@` suggests existing tags; tapping a chip filters the tree. | No suggestions while typing. Chips in the outline are not tappable (tap selects the row); filtering by tag only via the detail page's chip popover. |
| **D-21** | "Show" filters the tree to the arrived items and their ancestors. | "Show" jumps to the first changed item (`goTo`, L774); no filter. |
| **D-20** | Offer "Send back to Phone". | Toast action "Send back", which reopens the exchange sheet. |
| **D-22 / D-23** | Conflicts are named in the confirmation and marked on the item; resolve view. | Not implemented: concurrent changes are resolved silently by event order. |
| **D-27** | History shows "reverted by …". | Shows "· reverted" without naming the reverting change. |
| **D-28** | Undo stops, names the later change with device **and value**, offers history. Example wording: "This item has been changed since on Tablet ('C'). Open history?" | Names item and device only: ""…" was changed on Tablet since. Undo stops here." with action "History". |
| **ADR-067 (from D-83, open question 14)** | One action = one quick-undo step. | Actions writing several events are several undo steps, e.g. creating an item with `@@newtag` (create + tag), editing a title with `##` (title + note), indent while editing (move + text). |
| **D-25 / principle 3** | Undo shows what it reverses (brings the item into view). Not decided whether this may leave Focus. | Undo, Go there and `item:` links always leave Focus and show the whole tree (N18). |
| **D-34** | Theme can be chosen per tree. | No UI for the tree's theme (N30). |
| **D-43** | Sizes in relative units, scaling with the device text size. | All sizes in px. |
| **D-52 / D-54** (hand setting mirrors toolbars) | The hand setting mirrors the editing toolbars. | Outline and capture toolbars and the detail strip are mirrored; the note formatting bar (`.notebar`) is not (no `.hand-left` rule, L160–162). |
| **D-54** | 📥 while typing: saves the text, opens the capture sheet, closing returns to the text with the cursor where it was, with a short confirmation. Only the edited line gets extra space beside the button. 📥 stays just above the toolbar. | 📥 saves the draft and ends editing (L647); closing the sheet does not return to the text; no confirmation. No extra space for the edited line. 📥 keeps a fixed distance from the bottom and is not lifted with the keyboard (N56). |
| **D-63** | Strip shows links, backlinks and history with counts and the last change (🔗 2 · ↩ 3 · 🕘 06:12). | Only "🕘 n" (event count, no time) and ⋯; links and backlinks not built. |
| **D-64** | Rename entry: "Rename … (n items)…". | "Rename "…" everywhere…" without count. Chips on the detail page are not refreshed after toggling tags in the picker until the page is reopened (L579, L530). |
| **D-65** | "Same as above" names the theme it follows **and where from**; the preview is live **on the page**. | Shows "Follows Sage" without where from. Opened from the detail page, the preview recolours the app behind, not the detail page itself (the detail page keeps its own theme class, L678, L593). |
| **D-69** | Tutorial is its own tree, opened first, next to the user's own tree "My thoughts"; brought back with "Show the tutorial again". | Tutorial content lives inside the single tree "My thoughts" together with the user's items and inbox; brought back only by "Reset prototype…", which deletes everything. |
| **D-78** | After `Title ## text`, a confirmation says where the text went. | Only when editing an existing item; creating a new item (outline or capture sheet) with `##` gives no confirmation. |
| **D-80** | Messages are never hidden by sheets **or the keyboard**. | Toasts move to the top for sheets and the detail page, but during outline editing they keep a fixed bottom position and can sit behind the keyboard (N56). |
| **D-82** | Inbox theme is Deep stone (warm grey, charcoal accent, values given). | Inbox theme is the older "Cool tray" draft (blue-grey, accent `#4d6f86`), labelled "(draft)". |
| **D-84** | Different tree: pairing offers only "Add as a further tree" or cancel; import into an own tree is a separate step "Import 'Ideas' into 'My thoughts' as a new branch". | Offers "Delete this device's tree and use the one from …" (with a second confirmation if there are own changes) or "Merge both trees" ("items of both trees appear side by side, and one of the two inboxes becomes an ordinary item"). |
| **D-53 / D-58 / D-59** | Item menu: Open, Focus, Move to…, Link to…, Tags, Theme, Views, History, Delete. More: Switch tree, All views, Exchange, Export, Settings, Feedback. | Item menu lacks Link to… and Views (not built; order otherwise as decided). More lacks Switch tree, All views, Export, Settings, Feedback; holds the hand setting directly, Collapse/Expand all, Inbox and prototype entries. |
| **D-6** | Drag and drop for nearby moves. | Not built (stated in About). |
| **D-61 / D-62** | Formatting as you type, paste conversion. | Raw textarea while writing (as D-74 allows for now); no paste handling. |
| **D-71** | — (confirmed) | Note only: `.count` style for a calm inbox number (D-8) exists but is unused; the inbox shows "+n" only when collapsed. |

---

## 3. Confirms a decision

- D-1 – captured thoughts go into the inbox
- D-2 – Enter chains a sibling; leading space nests; Tab
- D-3 – Backspace on an empty line moves out; removes the line at top/inbox/focus level; Shift+Tab
- D-4 – opens at the last focused place (selection not restored, N57)
- D-8 – no badge, no warning colours on the inbox
- D-10 / D-18 – new items typed in place; 📥 opens a light sheet over the dimmed view, lines behave like the outline, Details button
- D-11 – `##` splits title and note, `\##` escape, appended to an existing note
- D-12 – search filters in place with ancestors, highlights, closing returns to the view
- D-14 – `@@name` tags a title, shown as chip; tag picker on the detail page
- D-21 – "Changed recently" filter in search; confirmation names item count
- D-25 – undo writes new events; any change revertible from the history
- D-26 – draft undo/redo while editing; no redo for quick undo, an undo is reverted from history
- D-27 – undo derived from the log with `reverts`; survives reload
- D-29 – no limit for quick undo
- D-33 – Paper tokens match the draft values
- D-34 / D-35 – theme per item applies to its branch; tinted in the overview; full screen when focused
- D-36 – theme stored as an event (`theme.set`)
- D-42 – comfortable density (rows ≥ 44 px, 16 px), note preview under the title
- D-52 – 📥 floating at the thumb's corner; bar ＋ Search Undo More by reach; back-to-selection label; hand setting mirrors
- D-53 – ⋯ on the selected row; no press and hold
- D-55 – bar and 📥 do not hide on scroll
- D-56 / D-57 – "Focus" and "Open" wording
- D-58 / D-59 – popover at the ⋯, hand side, same order up or down (minus Link to…, Views)
- D-60 – detail page note-first
- D-64 – tag picker (focused search, counts, most used first, several ticked, Create), chip popover order
- D-65 – detail page in the item's theme; "Same as above"; live preview with Apply/Cancel; ⋯ at the strip's thumb end
- D-66 – item menu from the detail page without "Open", opens upwards; menus cover 📥
- D-68 – inbox is a marked item, last at the top level, expandable, focusable, cannot be moved or deleted, item menu in More › Inbox, own theme
- D-70 – inline Markdown in titles, Markdown in notes, tags plain text, emojis
- D-71 – ▸/▾ arrow buttons in accent, ● for leaves, "+n" badge for direct children
- D-72 – swipe towards the start opens the detail page with "Open ›" hint
- D-73 – device back closes the detail page; visible back button styled as a button
- D-74 – note shown formatted when reading; formatting bar B, I, H, list, link, ✓
- D-75 – toolbars show ✓ only; capture sheet uses the same toolbar, no buttons in the line
- D-76 – ⇥/⇤ always visible; Backspace places the line after its former parent
- D-77 – Focus: ← back to previous view and position with the item selected; device back; breadcrumb; full title in normal weight, editable in place; note line opens detail
- D-79 – Collapse all / Expand all for the current view in More
- D-80 – toasts at the top while a sheet is open; Reset reachable at the first level of More (keyboard case differs, see section 2)
- ADR-066 – "Top level, last" places before the inbox

---

## 4. Prototype-only (should not become product behaviour)

- Storage in the browser's localStorage (`tt-proto-*` keys); "clearing site data deletes it"
- "Prototype" label in the top bar at the top level
- More › Event log (raw event list with event types and clocks, newest 200) and its Reset button
- More › Reset prototype… / "Delete everything and reset" / "Reset to the tutorial"
- More › About this prototype (including "This device: Phone · <id>")
- Technical lines in history entries: event type (`item.moved`) and "clock t.c"
- Exchange by clipboard/plain-text JSON ("Copy this device's events", "Import pasted text", format `thoughttree-prototype-export`), the sub-label "Sync by file, as in release 1" and the warning about plain text
- The different-tree dialog "Delete this device's tree …" / "Merge both trees" (a workaround for every device starting its own tutorial; D-84 decides otherwise)
- Tutorial events marked as device "Tutorial"
- Theme label "(draft)" and the "Cool tray" inbox palette
- Device label guessed from the user agent
- Technical wording in the tutorial ("Every change is an event") and in More ("N events in this browser")

---

## 5. User-visible texts (input for microcopy, topic 10)

| Where | Text |
| --- | --- |
| Bottom bar | More · Undo · Search · Add here (icons ⋯ ↩ 🔍 ＋) |
| 📥 accessible name | Capture a thought into the inbox |
| Top bar | My thoughts · Prototype · Untitled |
| Focus header | ← (Back to the previous view) · Title, tap to edit |
| Back-to-selection label | ↑ Back to "…" |
| Row accessible names | Expand, n items inside · Collapse · No items inside · Actions for … |
| Badge tooltip | n items inside |
| Swipe hint | Open › |
| Editing toolbar (names) | Undo typing · Redo typing · Outdent · Indent · Move up · Move down · Insert note separator (##) · Insert tag marker (@@) · Done (✓) |
| Capture toolbar (names) | Outdent: move this line out · Indent: nest this line under the one above · Insert note separator · Insert tag marker · Done |
| Empty states | Nothing matches. · The inbox is empty. Tap 📥 to capture a thought. · Nothing here yet. Tap ＋ Add here. |
| Search | Search titles and notes · Changed recently · ✕ (Close search) |
| Item menu | Open · Focus · Move to… · Move to… (the inbox stays last) · Tags · Theme · History · Delete · Delete (the inbox cannot be deleted) |
| Move sheet | Move "…" to… · Find the target by title · Matches · No item matches. · Fixed targets · 📥 Inbox · Top level, first · Top level, last · Recent targets · Browse the tree |
| Tag sheet | Tags · Find or create a tag · ＋ Create "…" · Tip: type @@name while writing a title to tag it without opening this. |
| Tag chip popover | Remove from this item · Show all with "…" · Rename "…" everywhere… |
| Rename sheet | Rename tag · Changes the name on every item · Rename |
| Theme sheet | Theme · Same as above · Follows … · Paper "Warm paper, rust ochre" · Inbox "Cool tray for what waits (draft)" · Sage "Quiet green" · Dusk "Evening blue" · Apply · Cancel · A theme applies to this item's whole branch. Tap a theme to preview it. |
| History sheet | History · This branch · Only this item · Created "…" (in the inbox) · Title → "…" · Note changed · Moved to … / Inbox / top level · Deleted · Restored · Tagged @@… · Untagged @@… · Theme → … / same as above · (undo of an earlier change) · this device (Phone) · · deleted · · reverted · Revert this change · Revert this undo · No changes yet. |
| Capture sheet | Capture · Into the inbox of "My thoughts" · Details · New thought (input name) · Next thought goes into the inbox · Next thought goes under "…" · Enter starts the next thought. A space at the start of an empty line, or ⇥ above the keyboard, nests it under the line above; Backspace on an empty line, or ⇤, moves it back out. Use ## to add a note and @@ for tags. |
| Detail page | Write the note… · ＋ Tag · 🕘 n (History, n changes) · ⋯ (Item actions) · B I H • 🔗 ✓ (Bold, Italic, Heading, List, Link, Done) · not found (broken item link) |
| More sheet | More · ▸ Collapse all · ▾ Expand all · 📥 Inbox "Open, focus, tags, theme, history" · Exchange with another device "Sync by file, as in release 1" · Hand: right / left "Mirrors the capture button and the bar" · Event log "n events in this browser" · About this prototype · Reset prototype… "Start again with the tutorial tree" |
| Sheet header | Close |
| Exchange sheet | Exchange with another device · Both devices end up with all events · 1 · Send · Copy this device's events · 2 · Receive · Paste the text exported on the other device · Import pasted text · Events with an id you already have are skipped. Afterwards, send your own events back so both devices are equal. In the real app this is an encrypted file; here it is plain text, so do not use private notes. |
| Exchange status | Copied n events. Paste them on the other device, for example through a note or a message to yourself. · Copying was refused here. The text is selected below: copy it by hand. · n new events from X (m items new or changed). k already known. · j rejected: same id, different content. · That is not a ThoughtTree export. Was the whole text pasted? · That is not a ThoughtTree export. · This is a different tree than the one on this device (each device started its own tutorial). What should happen? · Delete this device's tree and use the one from X · Merge both trees · (and their explanations) |
| Toasts | Moved to "…" [Undo] [Go there] · Deleted "…" [Undo] · Undone: … [Go there] · Nothing of yours to undo on this device. · "…" was changed on X since. Undo stops here. [History] · This change cannot be reverted here. · Moved to the note [Open] · Added to the end of the note [Open] · Nothing above to nest under · The inbox always stays last at the top level. · Capture a thought first. · Everything collapsed · Everything expanded · n new or changed items from X [Show] [Send back] · Nothing new from X: both devices already have the same events. · The old tree is deleted. This device now has the tree from X; exchanges keep both in step from now on. · Reset to the tutorial |
| Reset | Reset prototype · Deletes everything in this browser · All n events on this device are deleted, including everything you entered, and the tutorial tree is created again. Export first if you want to keep anything. · Delete everything and reset · Delete all events in this browser? · Delete everything |
| Screen reader only | Captured · Focused on … · Showing the whole tree |
