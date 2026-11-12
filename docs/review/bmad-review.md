# BMAD Review of the ThoughtTree Architecture Documentation

Review date: 2026-11-12 · Branch: `review/bmad` · Reviewed commit: `3463e87`

## Scope

- `docs/architecture.md` (arc42, sections 1–12)
- `docs/adr/` (ADR-001 … ADR-055, `README.md`, `bets.md`)
- `docs/ux-concept.md`
- `LICENSE`

The reviewed documents were not changed. This file is the only change on the branch.

## Method

BMAD Method **v6.12.4** (`bmad-method` on npm, latest at review time). The interactive installer (`npx bmad-method install`) was not allowed to run in the review sandbox. Instead, the published package was downloaded with `npm pack --ignore-scripts` and unpacked into a temporary directory outside the repository. No BMAD files were added to the repository. The workflows were then followed by hand from the package's own skill files:

| BMAD artefact (v6.12.4) | How it was applied |
| --- | --- |
| `bmad-agent-architect` (Winston) + `bmad-architecture` → **Validate** intent, `references/reviewer-gate.md` | Rubric walker over the **good-spine checklist**: real divergence points fixed, every rule enforceable and preventing its divergence, nothing deferred that lets two units diverge, named tech verified-current, every dimension decided / deferred / open (especially the operational envelope), findings tiered critical → high → medium/low. `lint_spine.py` was not run because it expects BMAD's `ARCHITECTURE-SPINE.md` format, not arc42. Its mechanical checks (duplicate IDs, missing rules, unpinned stack versions) were done by hand. |
| `bmad-review` lens **adversarial** | Looked for what is missing as well as what is wrong (at least ten findings) |
| `bmad-review` lens **edge-case-hunter** | Walked every path of the event model, sync, merge, pairing and import, and reported only the paths that have no handling |
| `bmad-review` lens **verification-gap** (QA perspective) | For every rule: if it broke, would the stated `verify` or test goal fail? |
| `bmad-sprint-planning` → `references/readiness-gate.md` (QA/readiness perspective) | "Could a developer implement this without inventing decisions nothing records?" |
| `bmad-qa-generate-e2e-tests/checklist.md` | Checked the E2E strategy (semantic locators, no fixed waits, independent tests) |

Note: v6 no longer ships a separate QA persona in the core. QA work is split between the verification-gap lens, the readiness gate and the QA checklist. The Test Architect module (TEA) is a separate install and was not used.

BMAD lenses do not rank their findings. The severities below follow the tiering of the reviewer gate:

- **Critical**: an agent following the documents will build something wrong or insecure, or a core guarantee does not hold.
- **High**: a contradiction or gap that forces an implementing agent to invent a decision on a stored format, the security model or the harness.
- **Medium**: an inconsistency, missing rationale or verification gap with limited blast radius.
- **Low**: wording, outdated details or structure.

## Summary

| Severity | Count |
| --- | --- |
| Critical | 4 |
| High | 12 |
| Medium | 20 |
| Low | 12 |
| **Total** | **48** |

Gate verdict (reviewer gate, one sentence): **CONCERNS.** The domain core (event model, HLC, versioning, harness intent) is unusually thorough. However, four critical issues affect the sync guarantees, the self-hosting promise and the harness protection the whole agentic process relies on. The UX and API layers are not yet decided well enough for implementation.

---

## Critical

### C1 – Strict CSP makes "choose a self-hosted server" impossible in the hosted app

- **Where:** ADR-045 (rules 1 and 5); architecture.md §6 "Switching on server sync" step 1; §4 "Self-hostable sync server"; §1 "the sync server can be self-hosted"
- **Problem:** ADR-045 allows connections "only to its own origin and the tree's sync server". It also says a self-hosted package must serve its own app with its own policy "instead of widening the policy of the hosted app". A CSP is a static response header, so the hosted app cannot list a server address the user types at runtime. Yet §6 lets the user choose "the default service or a self-hosted one, by its address" on any device. Trees also live in IndexedDB per origin. A device whose app comes from the hosted origin therefore cannot hold one tree on the default server and another on a self-hosted server, and a tree cannot simply "move to another server" (§6 step 7) across origins.
- **Consequence:** An agent will either weaken the CSP (`connect-src *`), which breaks ADR-045 and P6, or build a server-selection feature that the browser blocks. A core requirement in §1 ("usable where VPNs are blocked … self-hosted") is not achievable as written.
- **Recommendation:** Decide explicitly, in a new ADR or as an open decision, between: (a) a self-hosted server is used only from its own origin's app, with each origin as a separate installation and data store, and migration only by export file or readable export; or (b) a controlled `connect-src` allow-list, for example `https:`, with the rationale and the residual risk. Then correct §6 "Switching on server sync" and the server-move step to match.

### C2 – Harness protection cannot work with trunk-based pushes to `main`

- **Where:** ADR-035 verify; ADR-016 verify; §2 "trunk-based development on `main`"; §8 Testing, harness principle 2 ("CODEOWNERS"); §8 "Compiler and analyzers", Verify snapshots
- **Problem:** All protection checks diff against `origin/main` (`git diff --diff-filter=DM origin/main -- tests/data/formats …`). With trunk-based development directly on `main`, a pushed commit is already `origin/main`, so the diff is empty and the check always passes. CODEOWNERS only takes effect through pull-request reviews with branch protection, which the process in §2 does not include. `tests/specify` and "deleting a gate test or weakening an assertion" have no verify at all, and the invariant catalogue lives in `architecture.md` without any protection.
- **Consequence:** The central promise ("the harness is protected; agents cannot weaken it", scenario M2) is not mechanically enforced. An agent can delete stored format data or specs and CI stays green.
- **Recommendation:** Either require pull requests with branch protection and CODEOWNERS for `tests/**`, `docs/adr/**` and `*.verified.*`, or let CI diff against the previous commit (`github.event.before`) or the last release tag. Add verify lines for `tests/specify` and for removed or changed test methods. State in §2 how "trunk-based" and "human approval" fit together.

### C3 – Pulling by server sequence can silently skip events (no-gap rule unprotected)

- **Where:** §8 Event exchange ("the device remembers up to which `ServerSequence` it has pulled"); ADR-006 rule 3; §5 Server "Event Store"; open decision "Server event store"
- **Problem:** The no-gap guarantee, which delete-conflict detection relies on (`seen`), depends on two things the documents never require:
  1. Server sequence numbers become visible in commit order. With concurrent pushes, sequence 11 can become readable before sequence 10. A device that pulls `since=9` gets 11 and moves its cursor past 10 for good.
  2. A push is atomic and in local order. ADR-044's maximum request size forces batching. If an earlier batch fails and a later one succeeds, events of device D reach the server with a gap.

  The server cannot check the no-gap rule because it cannot read `DeviceId`. Azure Table Storage and Blob Storage, two of the three store candidates, have no natural monotonic commit-ordered sequence.
- **Consequence:** Events are lost from the point of view of some devices (they are never pulled again), or delete conflicts are not detected. This breaks the top quality goal (Reliability, INV-5) and is not detectable by the server.
- **Recommendation:** Add explicit store requirements to the open decision and to the store contract tests: sequence numbers are gap-free and readable only in commit order (or the pull uses a safe overlap or high-water mark); a push batch is all-or-nothing and processed in the order the device sent it; batches are sent strictly one after another. Add a simulation fault "concurrent pushes plus partial batch failure".

### C4 – ADR `applies_to` globs miss `ThoughtTree.Client.App`; README layout is outdated

- **Where:** ADR README "Conventions" (last bullet: assumed project layout lists only Domain, Contracts, Client, Server); `applies_to` of ADR-001, 002, 007, 008, 010, 011, 012, 013, 018, 019, 020, 021, 025, 026, 028, 030, 032, 033, 036, 037, 038; ADR-029 (`Domain/**` only)
- **Problem:** The agent rule is "before changing a file, read every ADR whose `applies_to` matches it". `src/ThoughtTree.Client/**` does not match `src/ThoughtTree.Client.App/**`. Yet §5 and ADR-041 put use cases, sync scheduling, sharing/pairing flows, export confirmation and sync-mode handling into Client.App. Only ADR-031, 041, 042, 051, 052 and 055 use `Client*`. ADR-029 (normalisation) applies only to Domain, but readable import in the application layer must also normalise (§8 Internationalisation).
- **Consequence:** An agent working in Client.App will not load ADR-019 (readable-export warning), ADR-011 (pairing vs sharing), ADR-030 (metadata), ADR-013 (device state) and others, which is exactly where those rules are implemented.
- **Recommendation:** Change these globs to `src/ThoughtTree.Client*/**`, or list Client.App explicitly. Update the README layout to the six projects from ADR-054. Add a CI check that every `src/` project is covered by the globs of the ADRs that name its layer.

---

## High

### H1 – Same `EventId` with different content breaks convergence

- **Where:** §8 Event exchange step 4; ADR-006; ADR-009; INV-2; §8 Event envelope (`EventId` in plaintext)
- **Problem:** Duplicates are recognised by `EventId` alone and skipped. If a buggy or malicious device (any key holder in a shared tree) writes two different payloads with the same `EventId`, devices keep whichever version arrived first. Their trees diverge permanently, and INV-1 and INV-3 fail without any warning. AES-GCM AAD binds `EventId` to the ciphertext but does not make it unique.
- **Recommendation:** Define the rule "same `EventId` ⇒ byte-identical envelope payload, otherwise reject and warn". Alternatively derive `EventId` from a hash of the content. Add this case to the generators and the simulation.

### H2 – Authorship, attribution and server honesty are not protected (risks missing)

- **Where:** §1 "See where changes come from"; §8 event table (`device.label-changed`: "a device can only rename itself"; `user.name-changed` with any `userId`); §8 Security; P3; §11
- **Problem:** `DeviceId` and `userId` sit inside the payload without a signature. Any holder of the tree key can write events as another device or user, rename other users, or fake `device.joined`. "A device can only rename itself" cannot be enforced. The server, or a network attacker with a token, can also withhold events or replay old ones (omission and rollback). P3 only covers changing, moving and swapping envelopes. None of this appears in §11 Risks or in the "honest about protection" statements.
- **Recommendation:** Either accept it explicitly, adding a §11 risk and a sentence in the user-facing protection explanation ("changes shown as from device X could have been written by anyone holding the tree"), or plan per-device signing keys. Define the authority rules for `user.name-changed` and `device.label-changed` (for example, ignore events whose writer differs from the subject).

### H3 – Merging trees contradicts "trees never connect" and leaves several cases undefined

- **Where:** §1 "Merging trees"; §8 Domain model "content moves between trees only by readable export and import"; ADR-010 "MUST NOT share … events … between trees"; §6 Pairing step 7; §8 "A tree is private while …"
- **Problem:**
  1. There is a direct contradiction: merging moves a tree's events into another tree, but §8 and ADR-010 forbid moving content between trees except by readable export.
  2. The merged-in tree's `device.joined` events make the target tree list devices and users that never received the key, and can wrongly mark it as "shared".
  3. Both trees wrote the predefined `relation-type.defined` and `label.set` events with the same fixed ids and empty `replaces`. Under the §8 conflict definition, these are competing changes, so every merge produces spurious conflicts unless identical values are exempt.
  4. Both trees have a first top-level node, so the tree name changes silently by agreed order.
  5. Competing `tree.server-changed` events arise.
  6. The abandoned account's log stays on the old server with no way to delete it.
  7. Merging two trees on the same device (§1) has no runtime scenario.
- **Recommendation:** Add a "Merging trees" runtime scenario and an ADR or ADR amendment that covers membership after a merge, equal-value concurrent changes (no conflict), tree name and server address after a merge, and cleanup of the old account. Reword §8 and ADR-010 to name merge as the one sanctioned exception.

### H4 – Conflict model defined only for node fields; other `replaces` events have no semantics

- **Where:** §8 Conflict resolution ("Conflict: … same field of the same node"; `replaces` listed for title, note, position, server address); event table (`replaces` also on `relation-type.defined`, `label.set`, `tree.feature-changed`); ADR-018 rule 1 (lists relation types but not labels or tree features); glossary "Conflict"
- **Problem:** The three sources disagree on which events carry `replaces`. For labels, relation types, tree features and the server address, nothing says whether concurrent changes produce a visible conflict, where it is shown (the Conflict View lists nodes), or how it is resolved. INV-9 ("conflicts agree") is therefore underspecified for these events.
- **Recommendation:** Make one list of events that carry `replaces` (ADR-018) and match §8 and the glossary to it. For each element kind, state "visible conflict" or "silent last-writer-wins". Define how the Conflict View shows conflicts that are not about nodes.

### H5 – UX foundation decisions are missing, but gates and ADRs depend on them

- **Where:** `docs/ux-concept.md` (status "outline"; "worked out in detail now: 1, 2, 3, 4, 11", but none are); §8 UI/UX; ADR-005 ("restore and undo stay possible"); open decision "Touch gestures"; §8 Testing ("Core flows on touch … Gate"); "contrast of every … design token"
- **Problem:** The document defines Undo as a foundation decision that affects Domain and the application layer, but the architecture has no undo model (scope, events, interaction with sync and conflicts). The component library choice affects the CSP, download size, accessibility and ADR-001, and is open. Design tokens do not exist, yet a unit test checks them. Touch flows are a push gate while the gestures themselves are an open decision. UI specifications "refer to this document", which has no content yet. BMAD's readiness gate asks whether a developer could build the UI without inventing decisions nothing records. The answer is no.
- **Recommendation:** Before any UI work, decide and record Undo (as an ADR), the component library (as an ADR, with a licence check), UX principles and a first token set. Until then, mark UI gates as "pending UX decision".

### H6 – Sync API contract is undefined beyond two endpoints, yet frozen from release 1

- **Where:** §5 Server "Sync API" (only `POST /v1/events`, `GET /v1/events?since=`); Registration, device tokens, Account Capabilities, Feedback Inbox, version endpoint (§8 Deployment tests), SignalR hub; ADR-037; §8 Format tests ("recorded API requests and responses … append-only")
- **Problem:** Registration (including the join code), token issuance, capability reporting, inbox upload and collection, paging and limits on pull, error and status codes, the "server needs updating" signal and the hub contract are not described. ADR-037 forbids changing the meaning of any endpoint, field or status code within `v1`, and the recorded requests become append-only test data, so whatever an agent invents first becomes permanent.
- **Recommendation:** Add an API section, or a Contracts ADR, with the endpoint list, request and response shapes, paging, limits, error codes and authentication per endpoint before the first release.

### H7 – Banned API analyzer cannot enforce exemptions by namespace inside one project

- **Where:** ADR-042 verify ("IJSRuntime is a banned API outside the adapters"); ADR-039 ("MAY use them in the thin hosting layers"); ADR-034 ("provider implementations as the only exemption"); §5 Level 1 ("ThoughtTree.Client: Razor components and browser adapters (separated by namespace)")
- **Problem:** `BannedApiAnalyzers` work per compilation (project), not per namespace. UI components and adapters share the `ThoughtTree.Client` project, so `IJSRuntime` cannot be banned for one and allowed for the other by `BannedSymbols.txt`. The same applies to timers in Client and Server, where hosting code and logic share a project. Exemptions would need `#pragma` or `SuppressMessage`, which an agent can add freely.
- **Recommendation:** Either move the adapters into their own project, which is a "real boundary" per ADR-054, or state that ArchUnitNET rules (namespace → type usage) enforce these bans, and add a CI check that forbids the relevant suppressions outside approved files.

### H8 – `verify` commands can pass without testing anything; some cannot run in the gate

- **Where:** ADR README ("`verify` is run by CI; most checks run the tests tagged with the ADR id"); 40+ ADRs with `dotnet test --filter "ADR=ADR-0xx"`; ADR-046 (`az bicep build`); ADR-048 (`gh attestation verify …:<version>`); ADR-035 ("whole gate suite without … network")
- **Problem:**
  - Nothing requires at least one test per ADR, so a filter that matches no tests passes (or behaves differently by runner) and the ADR is "verified" with no test.
  - Several ADRs have rules that their verify does not touch. ADR-046 only compiles Bicep (it does not check OIDC, missing secrets, IP logging or identical environments). ADR-049 does not check for secrets. ADR-001 only greps `package.json`. ADR-033 only greps `margin`/`padding-left/right`, missing `left:`, `float`, `text-align` and `border-left`. ADR-034 omits `Random.Shared`, `Stopwatch`, `Environment.TickCount` and `RandomNumberGenerator`.
  - ADR-046 and ADR-048 need the Azure CLI, a published image and network access, which contradicts the offline gate. No CI job is defined that runs the verify blocks or says when each one runs.
- **Recommendation:** Add a harness rule: every ADR whose verify is `dotnet test --filter` must match at least one test (fail on zero matches). Add a `when:` field to each verify (push gate, after deployment, release). Tighten the grep and banned lists named above.

### H9 – Named test tooling conflicts with the licence policy

- **Where:** §8 Accessibility tests (`Deque.AxeCore.Playwright`); §8 Domain tests tooling ("Microsoft code coverage"); ADR-050; §2 Constraints
- **Problem:** Deque's axe-core packages (axe-core and `Deque.AxeCore.*`) are published under MPL-2.0, a weak copyleft licence. ADR-050 forbids copyleft without human approval and the push gate would reject it. The licence of the Microsoft code coverage package should also be checked, because some Microsoft testing extensions use a non-OSS Microsoft licence. The architecture names both as fixed tooling without noting the exception.
- **Recommendation:** Record the human approval, for example "MPL-2.0 allowed for test-only dependencies that are not distributed", in ADR-050, or choose alternatives. Run the licence check over test projects deliberately and say whether test-only dependencies are in scope.

### H10 – Live sync, SignalR and scale-to-zero: cost and correctness risks are not listed

- **Where:** §2 ("scaling to zero; target cost near zero"); ADR-047 bet; ADR-030 ("live preselected"); ADR-044 ("rate limiting … in memory only"); §8 Security (server counters "kept in memory")
- **Problem:** Every device in live mode holds a WebSocket open. Container Apps count it as activity, so the app rarely scales to zero and the "near zero cost" assumption fails exactly in the preselected mode. With more than one replica, SignalR needs a backplane and in-memory rate limits and counters are per replica. With scale-to-zero, they reset at every cold start, which weakens abuse protection and the ADR-044 bet signals. None of this is in §11 or in the ADR-047 bet card's assumptions.
- **Recommendation:** Add these to §11 and to the ADR-047 and ADR-044 assumptions. Decide `maxReplicas = 1` (or a backplane) explicitly, and consider long polling or notification-on-demand for live mode.

### H11 – No implementation scope or order (BMAD readiness gate)

- **Where:** whole set; §1 "Key requirements"; many "later" items in §4 and §8; open decisions with "before the first release"
- **Problem:** There is no PRD, epics or release-1 cut. The architecture is the only intent artefact and mixes MVP, "later" and "possible later" features in the same lists. An implementing agent cannot tell what the first release must contain (for example: relations, labels in several languages, experiments, inbox, scheduled sync), which spikes block what, or the order of the foundation work. BMAD's readiness gate requires that requirements trace forward into implementable units.
- **Recommendation:** Add a short "Release 1 scope and sequencing" section, or a separate PRD and epics, that maps §1 requirements to releases and names the blocking decisions (store, storage engine, AOT, UX foundation, API contract) as gates.

### H12 – Readable import loses relations and leaves node identity undefined

- **Where:** ADR-019 rule 4 ("import … with ordinary `node.created` events"); §6 "Readable export and import" step 5; §8 Testing ("export followed by import gives back the same structure, titles, notes and relations (JSON)"); PO3 ("JSON imports back completely"); §1 ("into an existing node")
- **Problem:** Relations need `relation.added`, `relation-type.defined` and `label.set`, which ADR-019 does not allow. The gate and PO3 promise them. The documents also do not say whether imported nodes keep their ids or get new ones:
  - If they keep them, importing the same file twice, or into the source tree, creates duplicate `nodeId`s.
  - If they get new ones, `node:` links in notes break unless they are rewritten.

  Import "into an existing node" has no runtime scenario.
- **Recommendation:** Amend ADR-019 to allow the relation and label events on import. Define "new ids plus link rewriting". Add the "import into node" scenario.

---

## Medium

### M1 – "Never through our server" contradicts the private feedback inbox
§8 Performance tests ("never through our server") and §8 Manual tests ("never through our server") contradict ADR-051 and ADR-055 and §5 "Feedback Inbox". These are outdated statements left from before ADR-055. **Fix:** reword to "never unencrypted through a server; optionally end-to-end encrypted to the operator (ADR-055)".

### M2 – "Key leaves the device only through pairing" ignores sharing and key backup
§8 Security, bullets 2 and 6 ("only through pairing"; "account secret … shared only through pairing"), contradict §6 Sharing, ADR-028 (sharing and key backup hand over the tree access) and the open decision "Key backup". An agent could take this literally and block sharing. **Fix:** "only through pairing, sharing or an explicit key backup".

### M3 – "Own static site" per environment contradicts the single container image
§7 Environments ("each with its own static site, sync service and storage") and §3/§8 ("static host") contradict ADR-047 (one image serves the API and the static client from one origin). **Fix:** state that the static app is served by the same container. Clarify which origin is "the static host" in the metadata discussion.

### M4 – Storage engine is open, but IndexedDB is hard-wired throughout
The open decision "Local storage engine (IndexedDB or SQLite/OPFS)" says storage stays engine-neutral. Yet §4, §5 "Local Event Store: Append-only log in IndexedDB", §8 Persistence, ADR-038 ("IndexedDB layout version") and ADR-042 all fix IndexedDB. Persistence also lists "two stores: `events` and `snapshots`" while tree access, settings, sync progress, search index, UI state and the migration version need storage too. **Fix:** say "local store (IndexedDB or SQLite, see open decision)". List all logical stores.

### M5 – `KeyId` is undefined and implies key rotation that is never designed
§8 Envelope ("`KeyId` (which key of the tree encrypted the payload)"), §8 Tree access and ADR-015/028 use `KeyId`. ADR-012 says there is one key per tree. Nothing says how `KeyId` is formed, when it changes, or how a device holds more than one key (for example after a merge or a future rotation). It is not in the glossary. **Fix:** define `KeyId` (for example a hash of the key) and whether rotation is planned. If it is, add it as an open decision.

### M6 – Membership only grows: no device removal, token revocation or tree removal
There is no event or flow for "this device left or was lost". `device.joined` is permanent (ADR-011), device tokens never expire and cannot be revoked, and deleting a tree on one device leaves it listed as a member everywhere. "Shows with whom it is shared" (§1) is then wrong over time. §11 lists only "a shared key cannot be taken back". **Fix:** add a risk entry. Decide on a `device.left` event, a token lifetime and revocation by the account secret.

### M7 – Clocks far ahead are unbounded
§8 Ordering and ADR-014 ("MUST NOT reject or cap"): one device with a clock years ahead permanently moves every device's HLC forward. Last-writer-wins stops following real time "until real time catches up", which can be never, and the counter width is unspecified. A member of a shared tree can do this on purpose. The risk is not in §11. **Fix:** list the risk. Define the counter width. Consider letting the user accept or reject far-future events, or documenting why not.

### M8 – Size limits that become stored formats are not fixed
The padding buckets are "for example 256 B … 16 KB". There is no maximum title or note length, the behaviour above the largest bucket is undefined, and there are no values for the maximum request size, the maximum events or storage per account (ADR-044), or the maximum user-name and server-address length (ADR-028). Format tests freeze "every encrypted size is one of the padding buckets" from release 1. **Fix:** fix the bucket list and the limits in an ADR or in Contracts before release 1.

### M9 – Text being edited is not an event yet and can be lost
§8 ("one event … per editing session (when the user leaves the field or after a short pause)") versus R1 ("app closed while writing: no event lost") and the quality goal "no data loss". Text typed since the last pause is in no store, and mobile browsers kill background tabs without warning. **Fix:** decide whether drafts are persisted locally (device state), and say what R1 guarantees.

### M10 – Test project names violate ADR-054's own naming rule
ADR-054 requires project names by kind (`Specs`, `Properties`, `Compatibility`, `Rules`, `Security`, `Budgets`, `Smoke`). §5 lists `ThoughtTree.Domain.Model`, `ThoughtTree.Simulation`, `ThoughtTree.Server.StoreContract` and `ThoughtTree.EndToEnd`, so the architecture test would fail on the documented layout. The application-layer xUnit tests (§8, "Use cases … tested with xUnit") have no project, and where UI and accessibility specs live is not stated. **Fix:** extend the kind list or rename the projects, and map each test kind in §8 to a project.

### M11 – Gate and loop definitions contradict each other
- The test goal "Browsers: … Gate" and PO1 ("secured by end-to-end tests (nightly)") disagree, and the end-to-end push gate is Chromium only.
- Mutation testing is a gate for changed code, but the loop table runs it nightly, and the score is only "agreed".
- §8 Server tests run the self-hosted container in the push gate, while end-to-end scenario 9 runs nightly.
- M1 promises results "within 10 minutes", but "Fast feedback" is a watch.
- The size budget is a gate with no value (PF5).

**Fix:** one table that gives the loop and threshold for every gate.

### M12 – WCAG 2.2 AA is a MUST, but verification covers only the automatable part
ADR-033 rule 1 and U2 claim full AA conformance. Verification is axe, keyboard-only specs and user feedback, with no manual screen-reader check (§8). Automated tools cover only a part of the success criteria. **Fix:** either word the target as "AA for automatable criteria plus feedback" or add a periodic manual or assisted audit, and list the gap as a risk.

### M13 – Level 2 components are not mapped to projects
§5 Level 2 "Client" mixes Domain, Client.App and Client parts. "Projection" (with snapshots, which are I/O) and "Command Handler" collide with ADR-027 and ADR-053 (Domain is pure, command logic is in Domain). Sync Engine, Scheduler, Tree Manager, Storage Guard and Update Control have no project. **Fix:** add a project column to the Level 2 tables.

### M14 – No server store versioning, migration or rollback rule
ADR-038 covers only local storage. §7 and §8 promise an automatic redeploy of the previous release if production checks fail, but nothing says how the server store schema is versioned, migrated or rolled back. A rollback after a store change can corrupt data. **Fix:** add a server-store migration ADR (forward-only, compatible with N-1, or similar).

### M15 – Stack versions are not pinned (BMAD "verified-current")
The .NET version or target framework, the Blazor hosting model version, SignalR and the test tool versions are not named. `global.json` is "fixed" but its value is not stated. LambdaTale v3 (bbv's port of xBehave.net) is a niche dependency that runs every spec, and its maintenance risk is not listed. **Fix:** name the target framework (for example the current .NET LTS) and the key package major versions. Add the LambdaTale dependency to §11.

### M16 – Glossary gaps
Terms that are used heavily but not in §12: **Tree access**, **User identity**, **`EventId`**, **`KeyId`**, **`ServerSequence`**, **`seen`**, **`replaces`**, **Join code**, **Sync mode (live / manual / scheduled)**, **Readable export**, **Envelope format version / schema version / app version**, **Padding bucket**, **Relation / Relation type / Backlink / `node:` link**, **Label** (structural naming), **Tree name**, **Gate / Watch**, **Invariant / reference model / regression seed**, **Specify / Secure / Confirm**, **Inner loop / push gate**, **Spec host**, **Test id catalogue**, **Bet card / signal / review trigger**, **Usage summary / diagnostics block**, **Feedback inbox / operator**, **MLO**. "Account" (one per tree) also clashes with "no user accounts" (§3, ADR-020), so the glossary should spell out the difference. "Export File" covers only the encrypted kind, although "readable export file" is used too.

### M17 – Mixed-kind ADRs and ADRs whose rules cannot be fully met
ADR-030 is `kind: bet`, but only its sync-mode rule is a bet ("the rest … is a constraint" appears only in `bets.md`), so an agent reading the ADR cannot tell which rules are firm. ADR-036 ("check for … updates only with the user's consent") cannot be met in a browser PWA (§8 Metadata: the browser checks the service worker itself), and the ADR does not mention the exception. **Fix:** split ADR-030 into constraint and bet. Add the browser limitation to ADR-036.

### M18 – The production probe needs a stored credential
§8 Deployment tests: "one synthetic probe tree whose fixed events are pushed again on every check". This needs the probe tree's account secret, join code and token in CI, which contradicts "no deployment secrets are stored" (§7, ADR-046), or at least needs its own rule. **Fix:** state where the probe credential lives and who may see it.

### M19 – Usage-summary signals need local usage counters that are not designed
`bets.md` signals ("file imports in the last 30 days", "reminders in the last 30 days", "toolbar versus typed", "median taps to capture") require the device to record usage events. Where they are stored, how long they are kept, whether they are per tree, and how they relate to ADR-013 (device state) and the "no telemetry" rule are not described. **Fix:** add a short rule for local usage counters (device-only, per tree, bounded retention, never synced).

### M20 – No deletion or retention of data on the server
There is no API or process to delete an account's log, whether after a tree is deleted, after a merge (H3), after a server move (§6 step 7) or on the user's request. Ciphertext and padding sizes stay forever on servers that the user has left. Data-protection duties for the operator once the repository and server are public are not mentioned. **Fix:** decide on account deletion with the account secret and on retention, and list it in §11.

---

## Low

### L1 – Outdated search limitation
§8 Search: "Limitation accepted: search only works on paired devices that have synced" predates standalone-first (ADR-008). Search works on any device that holds the tree. Remove it or reword it.

### L2 – Runtime scenario "Coming back online" is inaccurate
§6: "`POST /events`" lacks `/v1/`. "Resolves conflicts" contradicts ADR-018: conflicts are derived and resolved by the user. "Detects connectivity" applies to live mode only.

### L3 – First tree versus another tree: initial events differ silently
§6 "First device setup" creates no named first node, so the first tree has no name (§8 Tree name). Neither scenario mentions the predefined relation types and labels that ADR-025 writes at creation, and "otherwise the tree starts empty" contradicts them. Readable import "as a new tree" does not say whether it writes `device.joined` and the predefined types. The tree name of a tree whose top-level nodes are all deleted is undefined.

### L4 – Join code handling
ADR-043 hashes join codes, but §8 Security mentions only secrets and tokens. The join code travels in `tree.server-changed`, so every member of a shared tree learns the operator's code permanently, and rotation is not described.

### L5 – Pairing code usability and exposure
A typed code that holds a 256-bit key, an account secret, GUIDs and a server address is long. Its encoding and length are undefined. There is no expiry, confirmation step or warning against screenshots of the QR code (the full key is on screen).

### L6 – Restore and removal edge cases
`node.restored` under a still-deleted parent, the effect of `relation-type.removed` on existing relations and labels, and `label.removed` versus a concurrent `label.set` are not specified.

### L7 – Cycle wording
§8 says moves that would create a cycle "are rejected on apply and the node is placed at top level". "Rejected" and "placed" contradict each other. ADR-023 is clearer. Align the wording, and state which node moves.

### L8 – Flags per account are per tree
Permission and experiment flags are "per account", and an account is per tree, while the inbox is a device or user feature. A user with three trees may get different assignments. The operator's public key for the inbox is also not described (how it is distributed and how a client trusts it).

### L9 – ADR-054 "keep the six source projects" versus §5 "a new project only for a real boundary"
One says exactly six projects, the other allows more (for example the later native app). Clarify.

### L10 – Section structure and terminology in §8 Testing and the bets
The private feedback inbox, performance reports, bet signals and the diagnostics view sit under "Performance tests". ADR-051 and §8 refer to a bet's "outcome block", but `bets.md` cards have separate Outcome and Signal bullets. Several review triggers ("after the repository has been public for a while") are time-based, against the README convention "triggered by events, not dates".

### L11 – ADR index (§9) drifts from the ADR files
ADR-046's title differs ("… with Bicep …" in the index). The `applies_to` values in the index differ for ADR-031 (`Client*` in the ADR file, `Client` in the index) and ADR-040 (two explicit globs versus `Server*`). Quality goal 1 ("all devices with the same events show the same tree") also does not mention that devices on different app versions show different trees by design (R6).

### L12 – LICENSE: correct, with minor gaps
`LICENSE` is the unchanged Apache 2.0 text (SHA-256 `cfc7749b…3d30` matches ADR-049 verify). There is no `NOTICE` file and no copyright line. "The name is not licensed" is stated only in §2 (Apache §6 already excludes trademarks, but a short trademark note in the README or NOTICE would make the stated intent visible to re-hosters). ADR-050 covers NuGet and npm only, not the licences of the components of the chiseled base image that ADR-048 ships.

---

## Strengths (for balance)

- The event model, HLC ordering, downward-compatible versioning (rules 1–4) and the invariant catalogue with a reference model are precise and testable.
- The ADR format (scope, MUST/MUST NOT, verify, why) and the bet cards with event-based review triggers suit agent-driven development well.
- The privacy reasoning (threat model A/B, metadata, padding, UTC scheduling, honest limits) is explicit, and most of its trade-offs are justified.
- The deterministic simulation and the "old reads new" format tests address the riskiest parts of the architecture.
