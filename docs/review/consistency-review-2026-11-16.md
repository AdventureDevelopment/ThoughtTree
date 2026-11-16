# Consistency review of the documentation, 2026-11-16

TL;DR: 26 real contradictions need an owner decision (class 2; 2.26 groups three small ones), 22 gaps or risks are noted (class 3), and 45 mechanical fixes were made in 6 commits (class 1). 14 more mechanical fixes sit inside ADRs and are left for the owner, because changing an ADR is a human decision.

Reviewed by an agent that did not write these documents. Four parallel reviewers covered: ADRs against ADRs; architecture.md against the ADRs; ux-concept.md against architecture.md and the ADRs; and AGENTS.md, the PR template, the spike plan and LICENSE/NOTICE against everything, plus links and terms. Every finding below was checked against the files on branch `claude/pensive-brown-khe9av` (base `0a5e481`). Findings that could not be confirmed, or that turned out to be correct on a closer look, were dropped.

Line numbers refer to the files after the class 1 commits.

## Checked and clean

- **ADR index:** all 75 ADRs (ADR-001 to ADR-075) are listed exactly once in architecture.md section 9. Title, file name and `applies_to` match every ADR header. Every ADR is `Accepted`, none is superseded, and no ADR or other document references an ADR beyond ADR-075.
- **Bets:** there are 12 bet ADRs (001, 007, 012, 023, 025, 029, 044, 047, 051, 055, 057, 075), and each has a card in bets.md with a matching anchor. No constraint ADR has a card.
- **Identifiers:** D-1 to D-164 are continuous. INV-1 to INV-14 are defined once (architecture.md section 8), and every INV reference resolves.
- **Links:** every relative link in the reviewed Markdown files resolves, including the links into `docs/ux/sketches/` and `docs/ux/prototype/`.
- **Test framework:** no leftover xUnit v3 or LambdaTale v3 wording remains. The only v3 mentions are the intended "until LambdaTale supports xUnit v3".
- **Retired terms:**
  - no "node.*" event names remain;
  - "zoom" appears only in the magnifying sense;
  - "Node" is used only as the technical term of ADR-063 (and as the arc42 column header in the deployment table);
  - "label" appears only as "device label" or a GitHub issue label, apart from one stale use, which is now fixed.
- **Licence:** the LICENSE sha256 matches ADR-049, and NOTICE carries the copyright line ADR-049 names.

## Class 2: contradictions the owner must decide

Most important first.

### 2.1 First start: setup questions or straight into the tutorial

- **Where:**
  - architecture.md section 6 "First device setup", steps 2 and 3: "App asks for a user name …", "asks for a device label, with a suggestion such as "Phone"".
  - ux-concept.md D-134: "First start goes straight into the tutorial, without welcome screens".
- **Disagreement:** the architecture asks two questions before anything is shown; the UX concept shows nothing before the tutorial. Nothing says when, or whether, the name and the label are asked.
- **Options:**
  - (a) generate defaults silently (device label from the platform, user name empty) and ask later, for example at the first pairing or in More › Settings;
  - (b) ask inside the tutorial;
  - (c) keep the questions and revise D-134.
- **Recommendation:** (a). It keeps D-134, and the name is first needed when another user sees it (sharing, not in release 1).

### 2.2 What a new tree starts with, and its name

- **Status:** decided by the owner, option (a): a new tree starts with its inbox and a first top-level item that carries the name; architecture.md section 6 step 4 aligned; commit `56c78a4`. D-118 and D-129 are log entries and stay as written; the new decision D-164 revises them (commit `844373d`).

- **Where:**
  - architecture.md section 6, step 4: the own tree is "empty apart from its inbox";
  - step 5 of the same scenario: "and a first top-level item whose title is the tree name";
  - "Creating another tree", step 3: the same first top-level item;
  - ux-concept.md D-118: "A new tree starts with its inbox only";
  - ux-concept.md D-129: an empty state for "a new tree with only its inbox";
  - the tree name rule: "first visible top-level item other than the inbox" (architecture.md section 8, ADR-066).
- **Disagreement:** if a tree has only its inbox, its name is "Untitled". This contradicts D-118, where a name is required and the list never shows an unnamed tree. If a first item exists, D-118 and D-129 are wrong.
- **Options:**
  - (a) keep the first top-level item that carries the name, and revise D-118 and D-129 so the tree starts with it plus the inbox;
  - (b) a new tree starts with the inbox only, and the name needs another source. That requires an ADR-009 or ADR-066 change, for example a `tree.renamed` event.
- **Recommendation:** (a). It needs no new event and fits ADR-009. Step 4 "empty apart from its inbox" then becomes "with its inbox and its name item".

### 2.3 Exchange file header: room for optional parts, or only two fields

- **Status:** decided by the owner, option (a): header = format version and `KeyId`; commit `5e3531f`. D-81 and D-128 are log entries and stay unchanged; their wording (naming a tree before pairing) is read through this decision.

- **Where:**
  - ADR-007: "a header with room for optional parts";
  - architecture.md section 8, "The export file has … a header with room for optional parts (for example a protected key)";
  - open decision "Key backup": "the export file header already has room";
  - ADR-073: "MUST keep the exchange file header to format version and `KeyId`". ADR-073 amends ADR-012, ADR-015 and ADR-028, but not ADR-007.
  - Related: D-81 ("the app names the tree and asks to pair first") and D-128 ("This file is from 'Tutorial' …"). A device without the key cannot read the tree name, which is now encrypted.
- **Options:**
  - (a) the header keeps format version and `KeyId` only, and optional parts live inside the encrypted part; ADR-007 and the key backup row are aligned;
  - (b) the header may carry further fields, each bound as authenticated data and never identifying, and ADR-073 is relaxed.
- **Recommendation:** (a), with ADR-073 listing ADR-007 as amended. A recovery-code-wrapped key can live in a separate backup file. The D-81 wording should then say the app cannot name a tree it has no key for.

### 2.4 The zero-match rule against release 1 without a server

- **Status:** decided by the owner, option (a): marker `# when: server`; commit `3a9c20c`.

- **Where:**
  - docs/adr/README.md: "A `verify` with `dotnet test --filter` must match at least one test; CI fails on zero matches";
  - ADR-074: "MUST leave server tests out of the release 1 push gate";
  - server-only ADRs with filter verifies, for example ADR-040, ADR-043, ADR-044, ADR-055 and ADR-056 (and ADR-021, ADR-057, which are out of release 1).
- **Disagreement:** in release 1 these verify lines match zero tests. CI then turns red, or needs an exemption that no ADR defines.
- **Options:**
  - (a) a README convention `# when: server` (or "from the first server release"), skipped until then;
  - (b) placeholder tests.
- **Recommendation:** (a). Placeholder tests would be hollow and conflict with the spirit of ADR-035.

### 2.5 Conflicts "marked seen" against conflicts derived from the log

- **Status:** decided by the owner, option (a): "Keep as it is" writes the ordinary resolving event; D-127 stays as written and D-165 revises it; commit `792a201`.

- **Where:**
  - ux-concept.md D-127: ""Keep as it is" marks it seen. When all are seen, the marks and the More entry disappear";
  - ADR-018: "MUST derive conflicts from the log; MUST NOT store them" and "resolve a conflict by an ordinary event that replaces all competing changes".
- **Disagreement:** "seen" is neither an event nor a defined device state.
- **Options:**
  - (a) "Keep as it is" writes the ordinary resolving event with the current winner, which is consistent with ADR-018 and gives one undo step;
  - (b) "seen" is device state (ADR-013), and the conflict stays in the log and in the Conflict View.
- **Recommendation:** (a). With (b), other devices would keep showing the conflict.

### 2.6 ADR-068: link slots are open, yet a MUST rule uses them

- **Status:** decided by the owner, option (a): slot rule removed from ADR-068, now an open decision in architecture.md section 9; commit `f8d055e`.

- **Where:** ADR-068 has the rule "**OPEN** (UX decision D-111): whether template lines can be link slots …", and the next rule reads "MUST … slot markings not copied; each slot becomes a line that offers to link an item by the slot's relation type". The `why` cites D-107, which D-111 left open. "OPEN" is also not a keyword of the ADR format.
- **Options:**
  - (a) remove the slot part of the MUST and move "OPEN" to the open decisions table;
  - (b) decide D-111.
- **Recommendation:** (a). Templates may slip to 1.x anyway (release 1 scope, decision 3).

### 2.7 Templates and the "full JSON export"

- **Status:** decided by the owner: the JSON export always contains templates (Markdown and OPML leave them out unless asked); ADR-019's import list now reads "ordinary events of the current event types" (also fixes the ADR-019 item in the ADR-file table); commit `f8d055e`.

- **Where:**
  - ADR-068: templates are left out of "the readable export of content, unless the user asks …; they are included in the full JSON export (ADR-019)";
  - ADR-019 knows only the readable exports Markdown, OPML and JSON; no "full JSON export" exists.
  - Also: ADR-019's import event list (`item.created`, `relation.added`, `relation-type.defined`, `display-name.set`) is closed and omits tags, themes, views, template markings and `item.note-changed`. ADR-069 relies on "like a readable import".
- **Recommendation:** decide whether the JSON export always contains templates. Then extend ADR-019's list to "ordinary events of the current event types".

### 2.8 Links across trees

- **Status:** decided by the owner, option (a): ADR-024 allows dangling `item:` text links (shown as not found), never relations across trees; commit `b8ae1bd`.

- **Where:**
  - ADR-024: "MUST NOT connect trees: relations and `item:` links only point to items of the same tree";
  - ADR-069: "leave such links as they are (shown as not found in another tree)", and ADR-068 does the same for templates.
- **Options:**
  - (a) ADR-024 allows dangling `item:` text links (shown as not found) but no relations;
  - (b) the paste rewrites outside links to plain text.
- **Recommendation:** (a). It only clarifies ADR-024.

### 2.9 Experiment flags: a local draw against stored per account

- **Status:** decided by the owner, option (a): a locally drawn flag is device state, never synced; commit `6217b36`.

- **Where:**
  - ADR-031: "assign experiment flags only per account on the server or by a local draw on the device";
  - the next rule: "MUST store permission and experiment flags only as flags per account";
  - architecture.md section 8 ("the server stores only the flag per account").
- **Recommendation:** say where a locally drawn flag lives: device state (ADR-013), never synced.

### 2.10 "No telemetry" against server telemetry

- **Status:** decided by the owner, option (b): no telemetry on the server either; plain logs without IP addresses only; commit `b0e1194`. AGENTS.md and the README keep "no telemetry" as written.

- **Where:**
  - AGENTS.md "Never add telemetry";
  - docs/adr/README.md "no telemetry";
  - ADR-046 "logging and telemetry record no IP addresses";
  - ADR-030 ("record IP addresses in logs and telemetry");
  - architecture.md section 8 (Application Insights).
- **Recommendation:** state that the rule is "no product telemetry about users". Operational server logs and metrics without IP addresses are allowed (ADR-030, ADR-046). Reword the AGENTS.md line accordingly.

### 2.11 SQLite as an Azure store

- **Status:** parked by the owner until the server is planned; the open decision row "Server event store" carries the finding and the PostgreSQL idea; commit `67ce0c6`.

- **Where:**
  - architecture.md section 2 and the open decision "Server event store": "Azure Table Storage, Blob Storage or SQLite";
  - section 7: "the chosen Azure store (not SQLite, because file locking on Azure file shares is unreliable)" and "Table Storage or Blob Storage is an open decision";
  - ADR-047: "MUST NOT run SQLite on Azure file shares".
- **Recommendation:** remove SQLite from the Azure options, or say "SQLite only on a local disk, which Container Apps does not keep".

### 2.12 Devices that started on their own

- **Status:** decided by the owner, option (a): the architecture sentence and ADR-009's `why` name the import; commit `8dd08ff`. The other ADR-009 fix ("visible" and the inbox exception) stays in the ADR-file table.

- **Where:**
  - ADR-009 `why`: "pairing of devices that started on their own";
  - architecture.md "Conflict resolution": "two devices that were used on their own before pairing: their top-level items simply sit side by side at top level";
  - against them: pairing now only adds a further tree, and content moves by importing a tree into another under a new item (D-84, architecture.md section 6).
- **Recommendation:** the architecture sentence should name the import (top-level items move under a new item). ADR-009's `why` should be updated by the owner.

### 2.13 Search scope

- **Where:**
  - architecture.md section 8 "Search": "one search can cover the open tree or all trees on the device";
  - ux-concept.md D-97: "search covers the current view: the whole tree, or only the focused item's branch". No all-trees search appears in the UX.
- **Recommendation:** the architecture keeps the capability, and the UX decides whether release 1 offers it. Record which applies.

### 2.14 Easy-language tutorial in release 1

- **Where:**
  - ADR-032: "the tutorial exists in Easy language from the start";
  - D-146;
  - release 1 scope, decision 3 lists only "the tutorial; English and German" and mentions Easy language neither as in, slip nor out.
- **Recommendation:** list it as a slip feature, or as in.

### 2.15 Far-future events: an open decision against ADR-014

- **Status:** decided by the owner, option (b): ADR-014 is the answer (accept and warn, never reject or cap); the open decision row "Far-future events" is removed; commit `af6c01b`.

- **Where:**
  - open decision "Far-future events": "Whether the user can accept or reject events from a clock far ahead";
  - ADR-014: "MUST accept events from clocks far ahead and warn; MUST NOT reject or cap them".
- **Recommendation:** note in the row that choosing "reject" needs an ADR-014 change.

### 2.16 Usability gate list and the release check

- **Where:**
  - architecture.md section 8 quality table, core flows: "… pair, share";
  - release 1 scope, decision 5: "… pair, file round trip", and the sharing UI is out in release 1;
  - loop table "Before release": "Short release check on the owner's phone", and section 8 "a guided check of about five minutes on the phone";
  - decision 5: "the guided release check on phone, tablet and desktop (including a file round trip …)".
- **Recommendation:**
  - the flow list becomes "… pair, file round trip (share once the sharing UI exists)";
  - the release check text says whether three devices apply to release 1 only or always.

### 2.17 Protected paths leave out the agent guide and CI

- **Status:** decided by the owner, revised the same day: `AGENTS.md`, `CLAUDE.md` and the rest of `.github/**` are agent-changeable on an independent reviewer agent's approval (required check, runs from `main`, weekly digest); the owner reviews them until the reviewer exists; ADRs, CODEOWNERS, the reviewer workflow and prompt, secrets and branch protection stay owner-only; commits `b3bdd3e`, `edca458`. CODEOWNERS file: pending the owner's GitHub username. Class 3 item 1 follows this.

- **Where:** ADR-035, AGENTS.md section 4 and architecture.md section 2 protect `tests/specify/**`, `tests/data/**`, the reference model, `*.verified.*` and `docs/**`. They do not protect `AGENTS.md`, `CLAUDE.md` or `.github/**`: the PR template, CI workflows and CODEOWNERS itself. Yet architecture.md section 8, harness principle 8, counts the agent guide as part of the harness.
- **Recommendation:** add `AGENTS.md`, `CLAUDE.md` and `.github/**` to the protected paths (ADR-035 change).

### 2.18 The PR template invites ADR changes

- **Status:** decided by the owner, option A: reworded to "ADR proposal"; commit `9ce21d4`.

- **Where:**
  - .github/pull_request_template.md: "ADRs updated if a decision changed" and the kind "ADR change";
  - AGENTS.md section 2 and docs/adr/README.md: creating or changing an ADR is a human decision, and agents may only propose one with status `Proposed`.
- **Recommendation:** reword the item to "ADR proposed (status Proposed) if a decision would change; existing ADRs changed only by the owner".

### 2.19 "Screen reader paths covered" against no manual screen reader tests

- **Status:** decided by the owner, option (a); manual screen reader testing (option B) parked as an open decision for later; commit `e55e509`.

- **Where:**
  - PR template and ADR-064: "Keyboard and screen reader paths covered";
  - architecture.md section 8: "Screen reader experience (VoiceOver, TalkBack, NVDA) is not tested manually; it relies on user feedback".
- **Recommendation:** define the item as "accessible names, roles and live-region announcements checked by the specs and component rules".

### 2.20 End-to-end smoke set after the first server release

- **Status:** parked by the owner; now an open decision in architecture.md section 9; commit `80c1efc`.

- **Where:**
  - architecture.md section 8 end-to-end: "Push gate: scenarios 1, 2, 6 and 8 … (release 1: 2, 6, 7 and 8)", with scenario 9 nightly;
  - decision 5: "end-to-end scenarios 1 and 9" switch on with the server, and "scenario 7 (pairing) stays".
- **Disagreement:** after the server release, scenario 7 drops out of the 1, 2, 6, 8 list, and scenario 9 is never in the push gate.
- **Recommendation:** state the post-server push gate set explicitly, for example 1, 2, 6, 7 and 8, with 9 also in the push gate (container test).

### 2.21 "The whole gate suite runs in an agent's sandbox"

- **Status:** parked by the owner until the server is planned; open decision in architecture.md section 9; commit `4976479`.

- **Where:**
  - architecture.md section 8, harness principle 5;
  - against it: the container test in the push gate "because the agent's sandbox may not have Docker", and ADR-046's verify, which needs the Bicep CLI.
- **Recommendation:** principle 5 should say "needs no Azure account, secrets or network". Docker and the Bicep CLI are tools CI provides.

### 2.22 Search index encryption: MUST against an open option

- **Status:** decided by the owner, option (a): ADR-073 unchanged; the open decision row says choosing the option needs an ADR-073 change; commit COMMIT.

- **Where:**
  - ADR-073: "MUST encrypt snapshots, search index and drafts at rest";
  - open decision "Optional unencrypted search index" and spike variant C.
- **Recommendation:** the open decision row should say that choosing it needs an ADR-073 change. Or ADR-073 says "by default".

### 2.23 Spike complexity threshold against PF6

- **Status:** decided by the owner, option (b): the spike uses 10 times, as PF6; commit `31041d0`.

- **Where:**
  - spike-1 plan, Q1 acceptance (owner-confirmed 2026-11-16): "at most 12 times (PF6)";
  - architecture.md section 10, PF6: "About ten times the time, never more".
- **Recommendation:** PF6 reads "at most 12 times" (tolerance), or the spike uses 10.

### 2.24 "Thought" as a term

- **Where:**
  - D-140: user-facing texts say "thought(s)", and "item" stays the technical term;
  - neither ADR-063, the glossary nor the UX Terms table records "thought" as the user-facing word;
  - ADR-072's title and architecture.md section 8 ("Focus shows a thought as a page") use it in technical text, while ADR-063 says MUST use "item" in documents.
- **Recommendation:** add "Thought: the user-facing word for an item (D-140)" to the Terms, the glossary and ADR-063.

### 2.25 Application-layer tests: Specify or Secure

- **Where:**
  - architecture.md section 5: "use cases, privacy confirmations and language resources in `Client.App.Specs`" (under `tests/specify`);
  - the role table in section 8 lists "application layer" under Secure, and calls the privacy confirmations and language resources "gates".
- **Recommendation:** use cases become specs under Specify; the language resource and confirmation checks become rules under Secure (for example `Client.App.Rules`).

### 2.26 Smaller disagreements

- **PR description format:**
  - AGENTS.md: "a one-line TL;DR first";
  - the template: "Two lines: what changes and why".
  - Recommendation: the template follows AGENTS.md.
- **Text size setting:**
  - ADR-013: "device settings such as text size and hand side";
  - D-43: "no in-app text-size slider".
  - Recommendation: drop "text size" from ADR-013.
- **A rejected sketch is kept:**
  - D-149 keeps `tablet-layouts.html` ("not chosen as a look");
  - D-47: "rejected variants are not kept".
  - Owner to say whether D-149 is an exception.

## Class 3: gaps and risks

1. **Decided by the owner, commit `e657c6d`: `.github/CODEOWNERS` added with `@ProductOwnerRole` (write access checked on the collaborator list); the interim lines for AGENTS.md, CLAUDE.md and .github/ are removed once the reviewer agent exists.** Original finding: no CODEOWNERS file exists. The PR template, ADR-035 and ADR-046 rely on it, and foundation step (1) in architecture.md does not list it. Without it, the owner review of protected paths is not enforced.
2. **ADR-035's snapshot check misses new snapshots.** Its verify uses `git diff --diff-filter=DM … '*.verified.*'`, which ignores added files, although the rule also forbids accepting new snapshots automatically.
3. **Release 1 relies on a bet trigger that no card has.** Decision 6 says "bets whose review trigger is "the repository goes public" listed by an agent". No card in bets.md has that trigger; it appears only as an example in docs/adr/README.md.
4. **Importing a tree carries its `device.joined` events.** ADR-010 adds the imported tree's events to the target. ADR-011 derives private or shared from `device.joined`, so importing a shared tree would turn a private tree "shared". Nothing addresses this.
5. **The tutorial privacy branch names a feature that is not in release 1.** D-147 (4) says "the private feedback inbox is encrypted to the operator". The feedback inbox is out of release 1 (ADR-074), and decision 6 requires every privacy sentence to be true of the shipped code.
6. **A tutorial lesson needs a theme the architecture does not ship.** D-144 lesson 7 uses a "Sage" theme. architecture.md and ADR-060 ship "Paper" and "Inbox" ("at least").
7. **Tutorial lessons depend on features that may slip.** D-143 lesson 6 covers templates and D-113 covers copy and paste. Both may slip to 1.x, and ADR-074 hides unfinished features in the tutorial too. The lesson plan has no fallback.
8. **The export reminder may count a copy that does not protect.** D-126 counts "Last copied to another device … by exchange or export". Section 8 notes that the encrypted export file only helps while a device with the key exists. Decide which exports reset the reminder.
9. **Left and right.** ADR-033 bans `left`/`right` for layout direction, while D-52 has a physical "Hand: right / left" setting. Nothing says how the two combine; logical properties per hand and reading direction would work.
10. **Right-to-left interface.** architecture.md section 8 says "No right-to-left interface for now", while D-133 specifies a mirrored one. They are consistent only if D-133 means "later".
11. **Focus: branch only, or a page?** ADR-065, the glossary and the UX Terms table say Focus is "showing only its branch". ADR-072 and D-151 add the title, tags and note as a page. The definitions should mention the page.
12. **ADR-058 cites a rule ADR-024 does not have.** ADR-058 says "as for relations, ADR-024" for concurrent add and remove without conflict marking, but ADR-024 has no such rule.
13. **Marker events change what the structure looks like.** ADR-016 says structure is expressed "only through the basic events", while inbox and template markings change what the user sees. An older app would show a different structure.
14. **The spike code may be caught by repo-wide builds.** The spike lives in `spikes/spike-1/` at the root, but AGENTS.md section 5 and the architecture layout do not mention it. Repo-wide `Directory.Build.props`, banned API files and CI builds may apply to it.
15. **Spike results may hold personal data.** They store device models and browser versions of the owner's devices in a repository that goes public (ADR-049: no personal data). This is probably harmless, but the owner should confirm.
16. **NOTICE may read as a licence restriction.** NOTICE says "You may use and redistribute this software under the terms of the licence, but not under the name 'ThoughtTree'". Apache 2.0 §4(d) says NOTICE text does not modify the licence, and §6 already withholds trademark rights. A pointer to §6 would be safer.
17. **Sketches named in decisions but not stored.** D-82 (inbox theme), D-88 (starting a drag, sort mode) and D-89 (landing place, variant A) name sketches that are not in `docs/ux/sketches/`, although D-47 requires stored sketches.
18. **Stale open questions in ux-concept.md** (left unchanged; the owner decides whether to close them):
    - the inbox theme entry says both "decided in D-82" and "to be designed";
    - tag duplicates are still open, although ADR-058 already decides `tag.joined`;
    - onboarding "content and flow to be refined", although D-134 to D-145 decide them;
    - the status line says "all eleven topics are worked out" while "Order" says "Topics 9 and 10 later";
    - "the review of dormant thoughts" is said to be listed under Open questions, but is not.
19. **Prototype version.** The status line says the prototype reflects decisions up to D-114, `docs/ux/prototype/README.md` says D-113, and D-106 says D-105.
20. **Token names differ between themes.** D-82 uses names such as "list", "tinted rows" and "on accent", while the Paper token table uses `surface-content`, `accent-text` and `on-accent`, and has no tint token, which D-35 and D-92 need.
21. **The device settings list is incomplete.** The "Device setting" row in architecture.md section 8 lists language, sync mode, use of the server and experimental features. It leaves out "offer feedback" (ADR-070) and the hand side (ADR-013).
22. **The PR template's kinds do not match the commit prefixes.** It offers Feature, Fix, Docs, Test harness and ADR change, while AGENTS.md also has spec, refactor, build and chore.

## Class 1: mechanical fixes made

45 fixes in 6 commits.

| Commit | File | Fixes |
| --- | --- | --- |
| `53580ee` | architecture.md | "Merging a local tree into a received one" and "merging trees" replaced by "importing a tree into another", as in section 6, ADR-010 and D-84 (6 places: building block, re-encryption note, simulation, privacy confirmations, two open decisions) |
| `ef7877e` | architecture.md | "Creating another tree" starts with the inbox (ADR-066); display name fallback "any display name", not "any label" (ADR-026); glossary "Tree Name" skips the inbox (ADR-066); glossary "Inbox": thoughts wait to be "placed" (D-139) |
| `612c9d9` | architecture.md | Section 2: per-push CI wording matches the branch check and push gate. Section 7: first public image once a server exists (ADR-047); previews from Bicep (ADR-046). Section 8: usability gate no longer "pending" (decision 5); "Focus" capitalised; mutation score 80 %; push gate = branch check plus …; container test from the first server release; touch interactions point to D-91, D-89, D-54, D-75, D-76; item history exists (ADR-059). Section 9: MAY in the rule list; decision 4 lists the optional unencrypted search index; decision 7 names the branch check |
| `ccc0712` | ux-concept.md (no log entries) | Terms "Note", principle 2, Journeys A and B: no detail page, Focus instead (D-151); principle 10: ADR-071 Accepted; "focused", not "zoomed in" (ADR-065); views through Search (D-125); PR template and AGENTS.md exist; release 1 scope decided (ADR-074); seven unlinked sketches added to the sketch table, `detail-page.html` marked replaced (ADR-064, D-47) |
| `f954d07` | AGENTS.md | .NET 10 cites architecture.md section 2; ADR-039 scope "client logic or the server core"; reports exclude device labels and display names (ADR-051); Intent trait required (ADR-054); push gate adds the container test from the first server release; the wrong `// INV-14` removed from the spec example; test ids belong to all UI tests (Client.Specs, Client.Rules, end-to-end); a new project also needs an ADR change (ADR-054) |
| `0ff60fe` | spike-1-storage-and-aot.md | Q1 acceptance marked confirmed 2026-11-16; "release 1 scope decision 5"; open decision named "Safari (iPhone and iPad) support" |

### Class 1 left for the owner (inside ADRs)

These have one clear fix, but each changes an ADR file, which is a human decision (docs/adr/README.md, AGENTS.md section 2). They were not edited.

| ADR | Fix |
| --- | --- |
| ADR-065 | Its verify returns 1, not 0: it matches the glossary entry for Focus, whose sentence ""zoom" is used only for magnifying" the ADR itself requires. Exclude that use, for example `grep -v 'magnifying'`. The one-time rule "MUST update on acceptance …" is done and can be removed, along with "once applied" |
| ADR-009 | "take the tree name from the title of the first top-level item" lacks "visible" and the inbox exception of ADR-066. The `why` mentions pairing devices that started on their own (see 2.12) |
| ADR-010 | "(copy and paste, readable export and import, ADR-069)": readable export and import is ADR-019 |
| ADR-072 | Its line 7 quotes architecture.md as "notes are full Markdown"; section 8 now reads "notes are Markdown without headings" |
| ADR-067 | Its `why` cites D-83 as the decision, but D-83 decides nothing (it stores the comparison); the grouping came from open question 14 |
| ADR-039 | The verify has only `dotnet build -warnaserror`; the architecture tests its rule names need `dotnet test --filter "ADR=ADR-039"` |
| ADR-038, ADR-045, ADR-047 | Verify lines that need browsers, a deployment or a container image carry no `# when:` marker, so by the README they run in the push gate |
| ADR-046 | Marker "`# templates compile; when: push gate`" is not a README form (the push gate is the default) |
| ADR-071 | The verify is a comment ("review gate"), not a command; "review gate" is defined nowhere |
| ADR-054 | Test locations leave out `tests/support` (listed in README and AGENTS.md) |
| ADR-071 | The privacy sentence "No AI reads your thoughts. ThoughtTree has none, on purpose." differs from D-159's example "ThoughtTree has no AI, on purpose"; choose one |
| docs/adr/README.md | The format template shows `verify` as an indented block, while all ADRs use a fenced bash block; no convention for "Amends ADR-0xx" lines (ADR-012, ADR-015, ADR-028, ADR-073 write them differently) |
| bets.md | The ADR-057 card stands before ADR-044 and ADR-047; cards are out of id order |
| ADR-019 | Import event list out of date (see 2.7) |

## Not covered

- The content of `docs/ux/prototype/` and `docs/ux/sketches/` (only their links were checked) and `docs/ux/outliner-comparison.md`.
- The ux-concept.md decision log was read as history. Older entries contradicting newer ones were not reported, and no log entry was changed.
- No check of external facts: package versions, the LambdaTale README, Azure or Web Crypto capabilities, prices.
- Not every one of the 163 D-n citations was checked against its topic; the ones in the ADRs and in architecture.md were.
- Wording and style (sentence length, plain language) were not reviewed.
