# ThoughtTree – Architecture Documentation (arc42)

## 1. Introduction and Goals

ThoughtTree is a hierarchical note-taking Progressive Web App for structuring thoughts as an outline tree. It is inspired by the UI of MyLifeOrganized (MLO), but focused on notes and thinking rather than task management.

**Key requirements**

- Hierarchical (tree-based) notes with unlimited nesting, collapse/expand, drag-and-drop, focus on an item
- Runs on every device as an installable PWA
- Standalone first: a device works fully on its own, with no internet, no account and no server; pairing with other devices and server sync are optional and can be added later
- Synchronisation of items across devices over the internet
- Touch-first outliner UX for phone and tablet; keyboard shortcuts on desktop as a nice add-on
- End-to-end encrypted: notes are encrypted on the device before sync; the server never sees plaintext
- Fast full-text search across all notes, performed on the device (the server never sees plaintext)
- Direct device-to-device sync: two paired devices can exchange events with each other without going through the sync server
- Multiple trees per device: a device can hold several independent trees side by side, each with its own key and event log (for example a private and a work tree)
- See where changes come from: every device has a label (for example "Tablet"), and an item shows which device last changed it and when
- Explicit sharing: pairing another device of one's own and sharing a tree with another user are two separate actions; every tree shows whether it is private or shared, and with whom
- Usable where VPNs are blocked: file-only sync stays a full alternative, the user controls app updates, and the sync server can be self-hosted
- Readable export and import: a tree can be exported unencrypted as Markdown, OPML or JSON, and such files can be imported as a new tree or into an existing item
- Importing a tree into another: another tree can be imported into a tree as a new branch, keeping its history (union of their event logs, re-encrypted with this tree's key); the receiving tree keeps its name, theme and inbox; everyone who holds the receiving tree can then read the imported tree including its history
- Honest about protection: the app states plainly what it protects (content) and what it does not hide (that and when ThoughtTree is used)

**Quality goals**

| Priority | Quality goal | Motivation |
| --- | --- | --- |
| 1 | Reliability | No data loss, offline or during sync, merge, import, update or conflict; all devices with the same events and the same app version show the same tree; notes outlive app versions, frameworks and cloud services, so old data stays readable for good |
| 2 | Privacy & Security | Personal thoughts stay private: end-to-end encryption on the device, the server only relays ciphertext; minimal metadata, in particular nobody operating or observing the server learns where the user is; integrity of events |
| 3 | Usability | Outliner UX like MLO on touch devices: quick capture, drag-and-drop, gestures; keyboard on desktop; accessible (screen readers, keyboard-only use, contrast, scalable text); English and German; honest about what is protected |
| 4 | Portability | Runs on phone, tablet and desktop in all major browsers (Chromium-based, Safari, Firefox); installable as PWA, later as store app or package; server self-hostable without Azure; data never locked in (readable export and import) |
| 5 | Maintainability | Testable (replaceable time and randomness, device simulation); changeable without breaking stored data (downward-compatible events, format versions); manageable by one developer; one language (C#) across client and server |
| 6 | Performance | Trees with thousands of items stay responsive (targets in section 10) |

**Stakeholders**

| Role | Expectation |
| --- | --- |
| Owner / developer (Sourceress) | A tool for personal thinking that is fun to build and maintain in C# |
| End users (initially the owner) | Reliable notes on phone, tablet and desktop |

## 2. Constraints

- Technology: C# on .NET 10 (the current long-term support release, pinned in `global.json`), Blazor WebAssembly as PWA, ASP.NET Core with SignalR on the server
- Must work offline; network is optional and intermittent
- Source control: GitHub repository `AdventureDevelopment/ThoughtTree`, private until the first release, public from then on
- Licence: Apache 2.0 for the whole repository; the name "ThoughtTree" is not licensed
- Dependencies may only use licences from an approved list (MIT, Apache 2.0, BSD and similar permissive licences), checked in the push gate; MPL-2.0 is also allowed for test-only dependencies that are not distributed (for example the accessibility checker axe); fonts may use the SIL Open Font License 1.1
- Single developer, incremental delivery
- Hosting: Microsoft Azure (developer familiarity), free tiers wherever possible; release 1 has no sync server and serves only the static app from Azure Static Web Apps (free tier), with a preview address for every pull request; once a sync server exists, the container image runs on Azure Container Apps, scaling to zero; target cost near zero for a single user
- The sync service (and the static app) must also be deployable by others on their own server, without Azure
- Server storage stays minimal: an append-only encrypted event log per account (one account per tree); concrete store (Azure Table Storage, Blob Storage or SQLite) is an open decision
- Development process: trunk-based development with short-lived branches: every change, by a person or an agent, reaches `main` only through a pull request; release flags (Microsoft.FeatureManagement) instead of long-lived feature branches
- Branch protection on `main`: no direct pushes; the push gate must pass; changes to protected paths (`tests/specify/**`, `tests/data/**`, the reference model, `*.verified.*`, `docs/**` including the ADRs and the invariant catalogue, `.github/CODEOWNERS`, the reviewer agent's workflow and prompt, the branch protection settings) need the owner's review through CODEOWNERS, and an approval counts only for the latest push; `AGENTS.md`, `CLAUDE.md` and the rest of `.github/**` agents may change on the approval of an independent reviewer agent (required check, runs from `main`, sees only the diff and the rules; the owner reviews them until it exists; a weekly digest issue lists every such change); other pull requests may merge automatically once the gate is green
- Agentic development: the system is developed largely by AI coding agents; the test harness is what keeps it in a working state and must be of very high quality
- CI/CD via GitHub Actions: every push to a branch runs the branch check, every pull request the push gate; a successful build on `main` deploys automatically to a test environment on Azure; promotion to production is a manual approval step

## 3. Context and Scope

**Business context**

- User interacts with the ThoughtTree PWA on one or more devices; every device works on its own
- Devices can exchange a tree's events directly as an encrypted file, without internet
- Devices can optionally synchronise a tree through a central sync service over HTTPS/WebSockets
- Other users can receive a tree that is shared with them and then work on it like the owner

**Technical context**

| Partner | Interface | Purpose |
| --- | --- | --- |
| Browser / OS | Service Worker, local store (IndexedDB; SQLite on the Origin Private File System only as a revisit option, see open decisions), Web App Manifest, Web Crypto API | Installability, offline cache, local persistence, client-side encryption |
| Sync Service (ASP.NET Core on Azure) | REST + SignalR | Push/pull of encrypted events, real-time notifications |
| Authentication (device tokens) | Bearer token | Identifies which account's event log a device may access; one token per device and tree, no user accounts |
| Other devices (own or of other users) | Pairing or sharing flow (QR code or typed code) | Hands over a tree's key and account identity; no server involvement in key material |
| File transfer of the OS (share sheet, AirDrop, Nearby Share, USB) | File download / file picker, Web Share API where available | Carries the encrypted export file between devices |

## 4. Solution Strategy

- **Blazor WebAssembly PWA** for a single C# codebase on every device
- **Event sourcing** as the core data model: every change to the tree is an immutable event; the current tree is a projection of the event log
- **Offline-first**: events are appended to the local store first, then synchronised
- **Sync by exchanging event logs** instead of whole documents, which keeps conflicts small and mergeable
- **ASP.NET Core backend** as a thin event store and relay (REST for push/pull, SignalR for live updates)
- **Domain project for the client, contracts project shared with the server**: the server cannot decrypt events, so it needs only the envelope, the export file format and the API data; all domain logic (events, tree, projection, conflicts, clock) lives in a client-side domain project
- **End-to-end encryption**: event payloads are encrypted on the device with the tree's key, handed over only by pairing or sharing; the server stores and relays ciphertext plus minimal routing metadata (account id, event id, sequence number)
- **Several independent trees per device**: each tree has its own key, account identity and event log; nothing is shared between trees
- **Deterministic replay**: every device orders a tree's events by hybrid logical clock and device id, so all devices compute the same tree from the same events
- **Touch-first UI**: the outliner is designed for phone and tablet gestures first; desktop keyboard shortcuts come as an add-on
- **Pluggable sync transport**: every exchange between two logs follows the same steps, independent of how the bytes travel; the Sync Engine talks to an `ISyncTransport` abstraction, with the server and the file as the first transports
- **No gaps per device**: every transport delivers the events of a device without gaps: a device never holds a later event of device D without all earlier ones; conflict detection relies on this
- **Device-to-device sync by encrypted file**: works without internet and is triggered manually; one device exports its event log as an encrypted file, the user hands it over (AirDrop, Nearby Share, USB), the other device imports and merges it. A native wrapper with automatic local discovery is a possible later upgrade
- **User controls when to sync**: server sync runs only when the user wants it: live (continuous, with real-time updates), manual (on request), or scheduled at times the user plans; the mode is chosen per tree and per device
- **Later distribution as a store app or downloadable package**: adds reliable background sync at planned times and control over updates, which a PWA in the browser cannot fully provide
- **Self-hostable sync server, one installation per server**: the sync service and the app are offered as one package that anyone can run on a server they trust; an app only syncs with the server it was loaded from, so its Content Security Policy can stay strict; using the hosted server and a self-hosted one on the same device means two installations, each with its own trees; each tree records its server as an event, so all its devices agree on it
- **User controls app updates**: the app checks for and applies updates only when the user allows it
- **Later option: sync through a cloud folder** the user already uses; the app writes the encrypted export file there, which looks like ordinary file sync on the network; needs the store app or downloadable package

## 5. Building Block View

**Architectural style: functional core, hexagonal shell**

- Domain (core): pure functions; commands plus current state give events, events plus state give the new state; no interfaces to the outside world
- Application: use cases and UI state; defines the ports it needs (event store, transport, crypto, clock, files and sharing), calls Domain and stores the resulting events
- Adapters: implement the ports (browser storage, Web Crypto, files, server and file transport) and their fakes for tests
- UI: Razor components that read state from the application layer and forward user input to use cases
- Server in the same style: Server.Core with its ports (store, clock), store adapters, ASP.NET Core as the outermost shell
- Dependency rule: dependencies only point inwards (UI → Application → Domain; adapters → Application); Domain depends on nothing; checked by architecture tests
- Navigation state (focus, selection, scroll position) belongs to a view, and the application layer allows several views at once, so a structure sidebar and later side-by-side panes on tablet and desktop need no rework
- The application layer offers view models shaped for display (for example: item with title, collapsed, has conflict, number of backlinks); the UI binds only to these and never references Domain types
- Taken from Clean Architecture: the dependency rule, view models instead of domain types in the UI, a structure that shows what the system does; deliberately not taken: interactor, input and output port, request and response model and presenter per use case, and entities with behaviour as the centre


**Level 1 – System**

| Building block | Responsibility |
| --- | --- |
| ThoughtTree.Domain (class library) | Pure core: events, tree, projection, conflict detection, hybrid logical clock, command logic, validation; used by the client, the simulation and a later native app |
| ThoughtTree.Contracts (class library) | Event envelope, export formats, pairing code, API data; shared by client and server |
| ThoughtTree.Client.App (class library) | Application layer: use cases, UI state, view models, ports; no browser APIs |
| ThoughtTree.Client (Blazor WASM) | Razor components and browser adapters (separated by namespace), resources, composition root |
| ThoughtTree.Server.Core (class library) | Server logic and its ports, independent of ASP.NET Core |
| ThoughtTree.Server (ASP.NET Core) | Shell around the core: sync API, SignalR hub, stores (in-memory, SQLite, Azure), serves the client app |

**Code organisation**

- A new project only for a real boundary: deployment (browser, container), sharing (client and server), or reuse (simulation, later native app); everything inside a project is separated by namespace and checked by architecture tests
- Inside each project, folders by feature, named after the glossary and the same in every project: `Outline`, `Relations`, `DisplayNames`, `Conflicts`, `Trees`, `Sync`, `Sharing`, `Search`, `Export`, `Feedback`, `Settings`; cross-cutting parts (`Clock`, `Text`, `Versioning`, `Crypto`) in their own folders
- Shared build settings in the repository root: `Directory.Build.props` (nullable, warnings as errors, analyzers, banned API files), `Directory.Packages.props` (all package versions, checked for licences), `global.json` (fixed SDK), `.editorconfig`
- Tests are grouped by intent, visible in folders, project names (by kind: `Specs`, `Properties`, `Model`, `Compatibility`, `Simulation`, `Rules`, `Contract`, `Security`, `Budgets`, `EndToEnd`, `Smoke`) and a trait (`Intent = Specify | Secure | Confirm`), and the CI summary is grouped by intent:

```text
src/
  ThoughtTree.Domain  ThoughtTree.Contracts  ThoughtTree.Client.App  ThoughtTree.Client
  ThoughtTree.Server.Core  ThoughtTree.Server
tests/
  specify/   ThoughtTree.Domain.Specs  ThoughtTree.Client.App.Specs  ThoughtTree.Client.Specs  ThoughtTree.Server.Specs
  secure/    ThoughtTree.Domain.Properties  ThoughtTree.Domain.Model  ThoughtTree.Formats.Compatibility
             ThoughtTree.Simulation  ThoughtTree.Client.Rules  ThoughtTree.Client.App.Rules  ThoughtTree.Server.Contract
             ThoughtTree.Server.Security  ThoughtTree.Architecture.Rules  ThoughtTree.Performance.Budgets
  confirm/   ThoughtTree.EndToEnd  ThoughtTree.Deployment.Smoke
  support/   ThoughtTree.Testing  ThoughtTree.Client.SpecHost
  data/      formats/  regression-seeds/  synthetic-datasets/
infra/       Bicep templates
deploy/      Docker Compose example for self-hosting
docs/        architecture, ADRs
```

- Where tests of each kind live: Domain specs in `Domain.Specs`; invariants and properties in `Domain.Properties`; use cases in `Client.App.Specs` (Specify); privacy confirmations and language resources as rules in `Client.App.Rules` (Secure); generic component rules (test ids, accessible names, pseudo-localisation) in `Client.Rules`; UI and accessibility specifications in `Client.Specs`; store contract tests in `Server.Contract`
- `tests/specify/` is protected as a whole: specs may be added, changing or deleting one needs human approval; every runtime scenario of section 6 is covered by at least one spec that names it
- Specs are written with LambdaTale in domain language at every level (Domain, application layer, UI, server); end-to-end tests use LambdaTale too but confirm rather than specify

**Level 2 – Client**

| Component | Project | Responsibility |
| --- | --- | --- |
| Client Application Layer (ThoughtTree.Client.App) | Client.App | Plain C#: use cases (capture, move, resolve conflicts, pair, share, export, …), UI state (selection, focus, open dialogs) and UI decisions (for example: sharing requires a confirmed warning); used by the Razor components and by the simulation |
| Browser Adapters | Client (adapters) | Every browser API (local store, Web Crypto, file and share APIs, QR scanner, clipboard, service worker) behind a C# interface, with thin JavaScript interop implementations and fakes for tests |
| Outline UI | Client (UI) | Razor components that only show state and forward user input: tree rendering, touch editing, gestures, drag-and-drop, focus; keyboard shortcuts on desktop |
| Tree Manager | Client.App | Lists, creates, switches and deletes trees (the name comes from the projection); the only component that stores a tree's secrets and settings: key and `KeyId`, account id and secret, device token, whether this device uses the tree's server, sync mode; removes the key when a tree is deleted |
| Command Handler | Client.App (calls command logic in Domain) | Wires user actions to the command logic in ThoughtTree.Domain, which decides which events to write, with clock, `replaces` and `seen`; stores the result |
| Local Event Store | Port in Client.App, implemented in Client (adapters) | Append-only log in the local store; tracks sync progress per destination (server, devices synced by file) |
| Projection | Domain (pure); snapshots stored through a Client.App port | Builds the in-memory tree from events; incremental updates; snapshots; derives conflicts |
| Search | Domain (matching, index logic), Client.App (use case) | Client-side full-text search over the projection; optional local index |
| Crypto | Port in Client.App, implemented in Client (adapters) | Stateless operations with the Web Crypto API: creates keys, pads, encrypts and decrypts envelopes; stores nothing, the non-extractable key handle is handed in for each operation (ADR-073) |
| Sync Engine | Client.App | Exchanges events through an `ISyncTransport`: push unsynced encrypted events, pull remote events, decrypt, merge and apply |
| Server Transport | Client (adapters) | `ISyncTransport` over the Sync API and Sync Hub |
| File Transport | Client (adapters); file format in Contracts | `ISyncTransport` that writes the encrypted export file and reads an imported one |
| Sync Scheduler | Client.App | Decides when to sync: live, manual or scheduled mode; scheduled sync at a random moment in a UTC window; batching of pushes |
| Pairing & Sharing | Client.App; QR scanner in Client (adapters); code format in Contracts | Creates and scans pairing and sharing codes; adds a received tree as a further tree, or imports a tree into another as a new branch (re-encrypting its envelopes, see Importing a tree into another); writes `device.joined`; keeps pairing and sharing apart |
| Conflict View | Client (UI) with use case in Client.App | Lists the conflicts the projection derives from the log and lets the user resolve them by writing the resolving event |
| Update Control | Client.App; service worker in Client | Checks for and applies app updates only with the user's consent; triggers rebuilding snapshots and search index after an update |
| Storage Guard | Client.App; browser checks in Client (adapters) | Requests persistent storage, warns when the app runs in a plain browser tab instead of being installed, reminds the user to export |
| Readable Export/Import | Client.App; formats in Contracts | Exports the current tree, or one branch from the item's actions (ADR-080, Proposed), unencrypted as Markdown, OPML or JSON; imports such files as a new tree or into an item |
| Auth Client | Client (adapters) | Attaches the tree's device token, held by the Tree Manager, to sync requests |
| Service Worker | Client (static files) | Caches app shell and assets for offline start; applies app updates only when the user allows it |

**Level 2 – Server**

| Component | Project | Responsibility |
| --- | --- | --- |
| Sync API (REST) | Server | `POST /v1/events` (push ciphertext), `GET /v1/events?since=` (pull), plus registration, tokens, capabilities, inbox and account deletion; versioned (ADR-037); the full contract is defined in Contracts before the first release with a server (open decision) |
| Sync Hub (SignalR) | Server | Notifies connected devices about new events |
| Event Store | Port in Server.Core, implementations in Server | Persistent per-account append-only log of encrypted envelopes; gap-free sequence numbers, readable only in commit order; a pushed batch is stored all or nothing (see Event exchange) |
| Registration | Server.Core | Registers an account on first contact; issues device tokens to devices that present the account secret |
| Account Capabilities | Server.Core | Stores permission and experiment flags per account (for example the private feedback inbox) and reports them to the device during sync |
| Feedback Inbox | Server.Core | Stores feedback encrypted to the operator's public key until the operator collects it; can be switched off by an operations flag |
| Auth | Server.Core (validation), Server (middleware) | Validates device tokens, scopes requests to one account |

## 6. Runtime View

**Scenario: Editing an item offline**

1. User edits an item's title in the Outline UI
2. Command Handler creates an `item.title-changed` event with the next hybrid logical clock value
3. Local Event Store appends the event; it is not yet known to any sync destination
4. Projection applies the event; UI updates immediately

**Scenario: Coming back online (live sync mode)**

1. Sync Engine detects connectivity
2. Pushes all events the server does not have yet to `POST /v1/events`, batch by batch, strictly one after another
3. Pulls events since last known server sequence number
4. Applies remote events to the projection, which derives any conflicts; the user resolves them in the Conflict View
5. Server broadcasts new events to other devices via SignalR

**Scenario: Real-time update on a second device (live sync mode only)**

1. SignalR hub signals "new events available"
2. Device pulls events since its last sequence number
3. Projection updates the tree

**Scenario: Concurrent edits on two devices**

1. Device A and device B are offline; both last saw the change of item N's title made by event E1
2. On Monday A renames N to "Ideas"; the event gets A's clock (Monday) and names E1 as the change it replaces
3. On Friday B renames N to "Thoughts"; the event gets B's clock (Friday) and also names E1
4. After the next sync both devices hold both events and replay them in the agreed order; "Thoughts" comes later and is shown on both devices
5. Because both renames replace the same change E1, both devices mark N as having a conflict and offer both titles
6. The user picks a title on either device; this writes a new title change that names both renames as replaced, and the conflict disappears on all devices once they have synced

**Scenario: Deleting an item that another device changed**

1. Device A and device B are offline
2. On A the user deletes item N with its subtree; the deletion records what A had seen from each device
3. On B the user adds a child to N and renames one of N's children
4. After the next sync, N is deleted on both devices, but marked as a conflict, because the deletion had not seen B's changes
5. The user sees the conflict, for example in a list of conflicts of the tree, and chooses: keep N deleted, or restore N with B's changes

**Scenario: Readable export and import**

1. The user chooses "Export readable" for one tree and picks Markdown, OPML or JSON
2. The app warns that the file is not encrypted and anyone who gets it can read the whole tree; the user confirms
3. The app writes the current tree, without history, deleted items or device information, into a file whose name marks it as unencrypted
4. On any device, also one without the tree's key, the user chooses "Import readable" and picks such a file, for example an OPML export from another outliner
5. As a new tree: the app creates a tree with its own key, writes the same starting events as for any new tree, then the imported content; into an existing item: the content is written under that item
6. Every imported item gets a new id (except in an update import, step 8); `item:` links within the imported content are rewritten to the new ids; links to items outside the file are shown as not found
7. A JSON import also restores relations and their notes, tags and relation types (`relation.added`, `relation.note-changed`, `item.tagged`, `relation-type.set`; tags and types are items in the marked branches); the target keeps its own default type, and an imported `related` arrives as an ordinary type (ADR-082, ADR-084, Proposed); the imported tag and type items move into the target's Tags and Relation types branches without matching, and equal tag titles get the offer to join (owner 2026-11-19)
8. Update instead of copy (ADR-078, Proposed): the export writes `item.exported` or `tree.exported` with the export point into the log, and the files carry each item's id and that event's id, no device information; imported into the tree they came from, the app offers "Update from these files", writes ordinary change events for known items (a change made in the app since the export becomes a visible conflict), creates new items only for unknown content, lists items whose files are missing and deletes them only after the user confirms, all as one action

**Scenario: Device with a clock far ahead**

1. Device A's clock is three days ahead; it writes events with clocks three days in the future
2. Device B receives them, accepts them and raises its own clock to theirs, so its later edits still come after A's events
3. B warns that A's clock seems to be wrong
4. Until real time catches up, B's clock counts on from A's time; the order stays the same on every device, but edits are no longer ordered by real time

**Scenario: Event from a newer app version**

1. Device A runs a newer app version and writes an event type or event version that device B does not know
2. Device B receives the event by sync or file, stores it in its Local Event Store and keeps it for further sync
3. The projection on B skips the event and the app suggests updating
4. After the update B replays the log again and the event takes effect

**Scenario: First device setup**

1. User installs ThoughtTree and opens it for the first time
2. App generates a user id, with an empty user name; this is the user identity; nothing is asked, in line with D-134 (first start goes straight into the tutorial)
3. App generates a device id and a device label from the platform (such as "Phone" or "Tablet"); name and label are asked for later, at the first pairing and in More › Settings, and can be changed at any time (owner decision 2026-11-16)
4. App creates two trees locally, each with its own key and account identity (account id and account secret) generated with the Web Crypto API and stored in the local store; nothing is sent anywhere:
   - the tutorial tree, written in the device's language from the app's resource files, which explains ThoughtTree by its own items; it is opened first
   - the user's own tree, named "My thoughts" by default (changeable at any time), with its inbox and the name item (step 5); no decision about a name is needed before the user has seen anything
5. Each tree starts with these events: `device.joined` for this device and user, the branch "Tags" with `tree.tags-set`, the branch "Relation types" with `tree.relation-types-set` and inside it the type `related` (`relation-type.set`, marked as the default by `tree.default-relation-type-set`, ADR-084, Proposed), the inbox item with `tree.inbox-set` and the predefined theme "Inbox" (`theme.set`), and a first top-level item whose title is the tree name; the tutorial tree then contains its tutorial items
6. User works through the tutorial or starts taking notes; events are stored locally
7. No internet, login or server is needed at any point

**Scenario: Creating another tree on a device**

1. User chooses "New tree" and gives it a name
2. App generates a new key and a new account identity for this tree, locally; the first event of the tree is a `device.joined` for this device and user
3. The tree starts with the same events as the first tree: `device.joined`, the Tags and Relation types branches with the type `related`, the inbox item with `tree.inbox-set` and the predefined theme "Inbox" (`theme.set`, ADR-066), and a first top-level item whose title is the name; it is independent of all other trees on the device
4. User moves between trees with the tree switcher

**Scenario: Pairing another device of the same user**

1. Pairing is for the user's own devices; it is optional, works without internet, and always concerns one tree
2. On a device that holds the tree the user opens "Pair my other device"
3. App renders a QR code containing the tree access (see Tree access) plus the user identity, generated locally (also shown as a typed code for devices without a camera)
4. The new device scans the QR code (or the user types the code)
5. The new device asks the user what to do: add the tree next to the trees already on this device, or cancel
6. Add: the tree is stored as a further tree with its own key; nothing else on the device changes
7. Once the tree's events have arrived (by file or through the server), the user can import it into one of the device's own trees as a new branch ("Import 'Ideas' into 'My thoughts' as a new branch", see Importing a tree into another)
8. The new device takes over the user identity from the QR code and writes a `device.joined` event, so the tree knows this device belongs to the same user; if the device already had a different user identity, the app asks whether both are the same person, and if so the device writes a new `device.joined` with the adopted identity in each of its other trees
9. The devices now exchange the tree's events by file, or through the server if server sync is switched on for that tree

**Scenario: Sharing a tree with another user**

1. Sharing is a separate action from pairing; it works without internet and always concerns one tree
2. On a device that holds the tree the user opens "Share with another user" and confirms a warning: the other user will be able to read and change the whole tree, and this cannot be taken back
3. App renders a QR code containing the tree access, marked as a share; it does not contain the user identity, so the other user keeps their own
4. The other user's device scans the QR code and stores the tree as a further tree next to its own; its own trees and its own user identity stay untouched
5. The other user's device writes a `device.joined` event with its own user identity
6. From then on the tree is shown as shared on all devices that hold it, with the list of users and their devices
7. Events are exchanged by file, or through the server if server sync is switched on for that tree

**Scenario: Switching on server sync (optional)**

1. On any device the user switches on server sync for the tree; the server is always the one this installation was loaded from (the hosted service, or a self-hosted server whose app the user opened); the device writes a `tree.server-changed` event with that address, so all devices of the tree learn it
2. The device contacts the Sync Service with the account identity; the first device to do so registers the account, later devices prove membership with the account secret
3. Sync Service issues a device token for this device and this tree; a device that syncs several trees holds one token per tree
4. The user chooses the sync mode for this tree on this device: live, manual or scheduled; the dialog explains the trade-offs and preselects live
5. The device pushes its event log and pulls events it does not have yet
6. Other devices of the tree learn the address from the event (or from the pairing code); a device whose installation belongs to that server joins the same way whenever the user switches server sync on there; a device whose installation belongs to another server can only sync that tree by file, and the app says which installation to use; each device decides for itself whether it uses the server
7. Moving the tree to another server: the user brings the tree into the other server's installation by pairing (a typed code works on the same device too) and switches server sync on there; this writes a new `tree.server-changed` event, and every device then pushes its whole log to the new server, duplicates are skipped

**Scenario: Device-to-device sync without internet**

1. On device A the user chooses "Export for another device"
2. Device A writes its event log, already encrypted with the key, into a single file
3. The user hands the file to device B (AirDrop, Nearby Share, USB or any other file transfer)
4. On device B the user chooses "Import from another device" and picks the file
5. Device B merges the events into its Local Event Store; events it already has are recognised by their `EventId` and skipped
6. Projection applies the new events and derives conflicts with the same rules as server sync
7. For a two-way sync the user repeats the steps from B to A
8. When either device is online again, it pushes the merged events to the server as usual

## 7. Deployment View

| Node | Content |
| --- | --- |
| User device (browser / installed PWA) | Blazor WASM app, Service Worker, local storage (events, snapshots, tree access) |
| Azure Static Web Apps (release 1: test, production, previews) | The static client app only (no sync server in release 1); the strict security headers of ADR-045 come from its configuration file; every pull request gets its own preview address, so changes can be tried on the owner's devices before merging |
| ThoughtTree container image (once a sync server exists) | One image: ASP.NET Core server serving the sync API, the SignalR hub and the static client app from one origin; store chosen by configuration |
| Azure Container Apps (test and production, once a sync server exists) | The container image, scaling to zero; store: the chosen Azure store (not SQLite, because file locking on Azure file shares is unreliable) |
| Event log storage on Azure | Append-only encrypted event log per account; Table Storage or Blob Storage is an open decision |
| Self-hosted server (optional) | The same container image on any server, with SQLite on a volume, behind a reverse proxy that provides HTTPS (example: Docker Compose with Caddy); backups of the volume contain only encrypted data |
| Container registry | GitHub Container Registry; public image, amd64 and arm64, version tags such as `1.4.2` and `1.4` |

**Going public (checklist for the first release)**

- Check the whole git history for secrets
- `LICENSE` (Apache 2.0) and `NOTICE` ("Copyright 2026 The ThoughtTree authors", note on the name, third-party notices generated by the release pipeline) present; add a short `CONTRIBUTING` guide
- Make the repository public, then (once a sync server exists, ADR-047) publish the first public image

**Environments**

- Two environments, test and production, plus a preview environment per pull request; in release 1 each is a static site on Azure Static Web Apps; once a sync server exists, test and production each run their own instance of the container image (serving the app and the sync API from one origin) with their own storage
- Data is never shared between test and production
- The test environment uses registration mode "open" with tight limits, and its data is deleted regularly; production uses "join code only"; no environment needs stored credentials for its checks

**Delivery pipeline**

- GitHub Actions builds and tests every push to `main`
- Every pull request is deployed to its own preview address (Azure Static Web Apps), removed when the pull request closes
- A successful build on `main` deploys automatically to test
- Test, production and the pull request previews are created only from Bicep templates in the repository (ADR-046)
- GitHub Actions signs in to Azure through OpenID Connect federation; no deployment secrets are stored, and agents never see deployment credentials
- Promotion to production is a manual approval step
- Every release is reproducible from its git tag, and its build is archived as a release artifact
- Unfinished features stay switched off in production through release flags

## 8. Cross-cutting Concepts

**Domain model**

- `Item`: Id (GUID), ParentId, Title, Note, CreatedAt, UpdatedAt, Deleted; its position among its siblings is not stored on the item but follows from replaying the events
- `Tree`: one independent outline, the projection over one event log; it has its own key and its own account identity (account id and account secret)
- A device can hold several trees; they share nothing and are synced, searched and exported separately
- Top level: an item without a parent is a top-level item; a tree can have one or several; there is no special root item, every item is created by `item.created`
- Text: titles are a single line of Markdown limited to inline formatting (emphasis, links, inline code); notes are Markdown without headings (a heading is a thought: typing one in a note creates an item inside, ADR-072); a tree can switch this off with the tree feature "Headings become thoughts", and its notes then keep their headings (ADR-079, Proposed); display names (tags, relation types and other structural elements) and view names are plain text without Markdown; all text is Unicode and may contain emojis; search and sorting work on the text without Markdown markers
- Every item has exactly one parent (or none at top level), for good; an item cannot appear in several places; where something is needed in two places, a link points to it
- Links inside text: a title or note links to an item with a standard Markdown link using the `item:` scheme, for example `[project ideas](item:a71c4e0b-…)`; item ids never change, so links survive moves and renames
- Link text: the link picker inserts a live link with empty text (`[](item:<id>)`), shown with the target's current title as plain text; if the user types their own text, it stays fixed; renaming an item writes no event for the links pointing to it; readable export fills in the target's title for live links
- Links whose target is gone: a deleted target, or one in a deleted subtree, is shown with its last title struck through and marked deleted, and clicking offers to restore it or show it read-only; an id not in the tree is shown as "not found, possibly not synced yet", because a linking note can arrive before the item it links to from another device; how a link is shown is computed from the current tree, no events are written
- Before deleting an item that other notes link to, the app lists those links
- Backlinks: the projection reads the `item:` links in all titles and notes and keeps an index of which item is linked from where, updated with each event and rebuilt after an update, with no events of its own; each item shows a "Linked from" section with inline links and incoming relations (with the reverse name of the type), clearly distinguished; links from deleted items are not counted or shown separately
- An inline link is never turned into a relation automatically: inline links stay text, relations stay deliberate connections
- The connection map draws inline links too, as untyped connections read from the backlink index, next to the typed relations; this writes no events and changes no stored format (owner decision 2026-11-17, Zettelkasten decision 2; turning links into relations, UX open question 19, stays out)
- External links are clickable and open in a new tab, or in the system browser for the installed app; the target address is shown before opening (hover or long press); the app sends no referrer (`Referrer-Policy: no-referrer`), so a self-hosted server's address is not revealed; no automatic link previews
- Later, with binary data: a preview picture of a linked page, created only when the user asks for it, on the device, and stored as an encrypted blob in the tree; never created automatically, never through our server or a third-party service, because the address itself is content
- Remote pictures (`![](https://…)`) are not loaded; the app shows a placeholder with the address, and clicking opens the picture in the browser; later, with binary data, the user can store such a picture once as an encrypted blob in the tree
- Nothing connects trees: links and relations only point to items of the same tree; the link picker offers only items of the current tree, and an `item:` link whose id is not in the tree is shown as not found; content moves between trees only as copies (copy and paste, readable export and import), with one sanctioned exception that keeps the history: importing a whole tree into another as a new branch (see Importing a tree into another)
- Fonts: the base font is the device's system font; fonts of themes are shipped from the app's own origin and downloaded in the background only when a theme in use needs them (ADR-061)
- Views: a tree and any item can have an ordered list of views (saved searches now, layouts such as board or columns later), defined by events and shared like content; the query is structured data with its own format version, results are computed on the device; at the tree level a view lists all views; always available (ADR-062)
- Themes: a tree and any item can have a theme (`theme.set`), which applies to the whole branch unless a child sets its own; themes are content shared by all devices and users of the tree, referenced by fixed ids shipped with the app (initially "Paper" and "Inbox"), with the default theme "Paper" as fallback; always available (ADR-060)
- Tutorial tree: on first start the app creates a tutorial tree next to the user's own tree and opens it first; it is an ordinary tree that explains ThoughtTree by its own items; its texts come from the app's resource files and are written in the device's language when the tree is created (not translated afterwards); it can be deleted, and "Show the tutorial again" creates a fresh one in the current version; it is not paired or shared unless the user chooses to
- Inbox: every tree has an inbox, an ordinary item marked as the tree's inbox by `tree.inbox-set` when the tree is created; captured thoughts are created inside it; it starts with the predefined theme "Inbox", set by an ordinary `theme.set` and changeable like any theme; it is always shown last at the top level, whatever its stored position, expands like any branch and can be focused, themed and tagged; it cannot be moved or deleted; the tree name skips it; the latest `tree.inbox-set` in the agreed order wins, so after importing a tree the importing device confirms its own inbox and the other one becomes an ordinary item (ADR-066)
- Copy and paste: items with their branches can be copied into another tree or place; the paste creates new items as one action, carries titles, notes, structure, tags (matched by name), themes and template markings, rewrites links and relations within the copy, drops relations to outside items, and starts without history; pasting into a shared tree shows a calm hint the first time (ADR-069)
- Templates: a template is an ordinary item marked by `item.template-set`, valid in its parent's branch; creating from it (`::name` at the start of an empty line, or "From template…") copies its branch as one action without a reference back; new trees get no templates, the tutorial tree offers a set to copy (ADR-068)
- Focus shows a thought as a page: title, tags, a line with history and links, the note, then the branch, whose items are formatted like headings with their notes; there is no separate detail page (ADR-072)
- Tags: items can carry tags, structural elements of the tree named by display names (ADR-058); a tree feature, switched on by default
- Proposed change (ADR-082, owner decision 2026-11-19): a tag is an item inside the tree's marked branch "Tags" (`tree.tags-set`), by its place; its title is its name, its note its description, its children its nested tags, and a parent tag includes its nested tags when filtering; joining duplicates moves children, appends the note and deletes the joined tag as one action; the tags switch off hides the branch too; applies instead of the bullet above once accepted
- Relations: besides the tree structure (parent, child, sibling order), items can be connected by typed, directed relations (for example `depends_on`, `references`), in any number and across the tree; relations never change where an item sits
- A relation is identified by its three parts (from item, type, to item), so adding the same relation twice yields one relation (by its id, with equal relations shown as one, once ADR-083, Proposed, is accepted); concurrent adding and removing is decided by the agreed event order, without conflict marking
- Relations of a deleted item are hidden but kept, and come back when the item is restored
- Proposed change (ADR-083, owner decision 2026-11-19): a relation has its own id; its type can be changed (`relation.type-changed`, concurrent changes are a visible conflict) and it is removed and restored by id; relations with equal three parts are shown as one; applies instead of the identity by three parts once accepted
- A relation can carry a note saying why the two items are connected (`relation.note-changed`, keyed by the relation's three parts, by its id once ADR-083 is accepted); it is hidden while the relation is hidden and comes back with it; concurrent edits are a visible conflict like item notes; it is searched, encrypted and exported like item notes (ADR-077, Proposed)
- Removing a relation type (`relation-type.removed`) hides its relations and display names but keeps them; they come back if the type is defined again (with ADR-084, Proposed: deleting the type item or moving it out of its branch does this)
- An item restored while one of its ancestors is still deleted stays hidden as "in a deleted part" until that ancestor is restored; the UI offers to restore the ancestor
- `display-name.removed` and a concurrent `display-name.set` for the same element, field and language are decided by the agreed event order (dropped by ADR-084, Proposed)
- Relation types: a relation refers to a type id, never to a name, so renaming a type changes no relation; a type has a name, a name for the reverse direction (for example "supports" / "supported by") and whether it is symmetric; for symmetric types the two item ids are stored in a fixed order, so A–B and B–A are the same relation
- Types are defined per tree by `relation-type.defined` events, their names by `display-name.set`; there are no built-in types: when a tree is created the app writes the predefined types (initially `related`, `supports`, `contradicts`, `example_of`, `cites`, `depends_on`) as ordinary events, after which they can be renamed, removed or extended like any other type
- Predefined types have fixed ids that are the same in every tree, so merging two trees does not duplicate them
- Proposed change (ADR-084, owner decision 2026-11-19): relation types are items inside the tree's marked branch "Relation types" (`tree.relation-types-set`), by their place; the title is the name, the note says what the type means, `relation-type.set` keeps `symmetric` and the reverse name; a new tree gets only `related`, marked as the default type by `tree.default-relation-type-set`; the tutorial offers further types to copy; display names are dropped; applies instead of the bullets above on `relation-type.removed`, `display-name.removed`, relation types, their definition and fixed ids once accepted
- A new relation gets the predefined type `related` until the user changes it (D-13; confirmed by the owner 2026-11-17); "mentions" is not a relation type: it is only the interface name for the untyped link connections on the map (no events)

**Internationalisation**

- All app texts are kept in resource files (`.resx` with `IStringLocalizer`) from the start; no text is written directly into a component; the same files hold the names of predefined elements
- Source language English, first translation German
- Easy language: the tutorial (and later other texts) also exists in Easy language, as its own set of resource files per variant (for example `de-x-easy`, `en-x-easy`), chosen in the settings like a language
- The app language follows the device's language and can be changed in the settings; this setting belongs to the device and is not synced; changing it reloads the app
- Dates, times and numbers are shown in the format of the chosen language; they are stored as before (ISO 8601 with UTC offset)
- All languages are available offline: the service worker caches all resource files
- The culture data for all languages is loaded from the start, so dates, numbers and sorting work in every language and adding a language later only needs its resource file, without further downloads; the price is a larger initial download
- Structural elements that users can define (relation types now; tags, note kinds and similar later) carry no names themselves; their names are display names, one per language, set by `display-name.set` events, like a resource file inside the tree (dropped by ADR-084, Proposed: tags and relation types become items named by their titles)
- Languages are identified by standard language tags such as `de`, `de-CH`, `en`
- A device shows the display name in this order: display name in the tree for the device's language (`de-CH`), display name in the tree for its base language (`de`), for predefined elements the app's own resource files, any display name in the tree
- Predefined elements need no special case: the app ships translations for their fixed ids in its resource files, as it does for its own texts; a tree can override or add languages with `display-name.set`
- Proposed change (ADR-081, owner decision 2026-11-18): no translation inside the tree; a structural element has one name per field, the one the user typed; predefined elements show the app's resource name in the device's language until renamed; the per-language bullets above apply until ADR-081 is accepted; ADR-084 (Proposed, 2026-11-19) goes further: tags and relation types become items named by their titles, so no display names are left, and the titles written at tree creation use the app language chosen on the creating device
- All structural elements later use the same display-name mechanism; exporting a tree's display names as a resource file for translation elsewhere, and importing it again, is a possible later feature (dropped by ADR-084, Proposed)
- Content (titles and notes) is stored as written and never translated in the log
- All text is normalised before it goes into an event (titles, notes, display names, names), by the Command Handler and by readable import: Unicode normalisation form NFC; line breaks in notes stored as `\n` only; line breaks in titles replaced by spaces; emoji sequences stay unchanged
- Search and comparisons normalise as well, as a safeguard against events from a faulty or very old app
- Right-to-left text: every title and note paragraph is shown with automatic direction detection (`dir="auto"`), so content in Arabic, Hebrew or mixed languages displays correctly in any interface language
- No right-to-left interface for now, but the UI is built direction-neutral from the start: CSS logical properties (`margin-inline-start` instead of `margin-left`), and gestures defined relative to the reading direction ("towards the end of the line" instead of "to the right"), so a mirrored interface later needs no rework
- Content is never translated, neither stored nor on the fly: translation would need an AI model reading the user's thoughts, which ADR-071 rules out
- Binary data (pictures, files) is planned for later: it is stored as separate encrypted blobs and referenced from events by id, never placed inside events
- Tree name: the title of the first visible top-level item other than the inbox (and the Tags and Relation types branches, ADR-082, ADR-084, Proposed); renaming that item or placing another item first renames the tree, with no event of its own; a tree without visible top-level items is shown as "Untitled" (not stored)
- `Device`: Id (GUID), Label; the label travels as a `device.label-changed` event in the log of every tree the device holds, so all other devices of that tree learn it, and it is encrypted like every other event
- `User`: Id (GUID), Name; created on first setup and carried to the user's other devices by pairing; it is self-declared and not verified by anyone
- Authority rules in the projection: `device.label-changed` always renames the device that wrote it; `user.name-changed` is applied only if the writing device has joined with that `userId`; otherwise it is ignored
- A tree is private while all devices that have joined it belong to one user, and shared as soon as a device of another user has joined; this is derived from the `device.joined` events, not stored as a flag; if a device has several `device.joined` events, the latest in the agreed order counts

**Event types (initial set)**

| Type name | Data |
| --- | --- |
| `item.created` | `itemId`, `parentId` (empty for a top-level item), `afterItemId`, `title`, `note` (optional), `format` |
| `item.title-changed` | `itemId`, `title`, `format`, `replaces` |
| `item.note-changed` | `itemId`, `note`, `format`, `replaces` |
| `item.moved` | `itemId`, `parentId` (empty to move to top level), `afterItemId`, `replaces` |
| `item.deleted` | `itemId`, `seen` |
| `item.restored` | `itemId` |
| `device.joined` | `deviceId`, `deviceLabel`, `userId`, `userName` |
| `device.label-changed` | `label`; a device can only rename itself, so the device is the one that wrote the event |
| `user.name-changed` | `userId`, `userName` |
| `relation.added` | `fromItemId`, `typeId`, `toItemId`; ADR-083 (Proposed) adds `relationId` |
| `relation.removed` | `fromItemId`, `typeId`, `toItemId`; ADR-083 (Proposed): `relationId` only |
| `relation.type-changed` | `relationId`, `typeId`, `replaces` (ADR-083, Proposed) |
| `relation.restored` | `relationId` (ADR-083, Proposed) |
| `relation.note-changed` | `fromItemId`, `typeId`, `toItemId`, `note`, `format`, `replaces`; the reason for the relation, Markdown without headings; empty removes it (ADR-077, Proposed); ADR-083 (Proposed) keys it by `relationId` instead of the three parts |
| `relation-type.defined` | `typeId`, `symmetric`, `replaces`; names are display names (see below); dropped by ADR-084 (Proposed) |
| `relation-type.removed` | `typeId`; dropped by ADR-084 (Proposed): deleting or moving the type item does this |
| `relation-type.set` | `itemId`, `symmetric`, `reverseName`, `replaces`; the properties of a type item (ADR-084, Proposed) |
| `tree.relation-types-set` | `itemId`; marks the branch "Relation types", written once when the tree is created (ADR-084, Proposed) |
| `tree.default-relation-type-set` | `itemId`; marks the type item that new relations get (D-13), written when the tree is created; the latest marking wins (ADR-084, Proposed) |
| `tree.tags-set` | `itemId`; marks the branch "Tags", written once when the tree is created (ADR-082, Proposed) |
| `item.template-set` | `itemId`; marks an item as a template (ADR-068) |
| `item.template-removed` | `itemId` |
| `tree.inbox-set` | `itemId`; marks the tree's inbox, written once when the tree is created |
| `theme.set` | `target` (the tree or an `itemId`), `themeId`; concurrent changes are decided silently by the agreed order, no `replaces` (ADR-060) |
| `view.set` | `owner` (the tree or an `itemId`), `viewId`, `name`, `query` (structured, with its own format version), `layoutId`, `replaces` |
| `view.removed` | `viewId` |
| `tag.defined` | `tagId`; names are display names; dropped by ADR-082 (Proposed): a tag is an item in the Tags branch |
| `tag.joined` | `fromTagId`, `intoTagId`; assignments of the joined tag count for the target tag; with ADR-082 (Proposed) both are tag items |
| `item.tagged` | `itemId`, `tagId`; with ADR-082 (Proposed) `tagId` is the tag item's id |
| `item.untagged` | `itemId`, `tagId` |
| `display-name.set` | `elementId`, `field` (for example `name`, `reverseName`), `language`, `text`, `replaces`; ADR-081 (Proposed) drops `language`: one name per element and field; ADR-084 (Proposed) drops the event |
| `display-name.removed` | `elementId`, `field`, `language`; ADR-081 (Proposed) drops `language`; ADR-084 (Proposed) drops the event |
| `tree.feature-changed` | `feature`, `enabled`, `replaces` |
| `tree.server-changed` | `address` (empty for no server), `joinCode` (optional), `replaces` |
| `item.exported` | `itemId`, `form`, `format`, `seen`; a readable export of the item's branch; the files carry only this event's id (ADR-078, Proposed) |
| `tree.exported` | `form`, `format`, `seen`; a readable export of the whole tree (ADR-078, Proposed) |

- `afterItemId` names the sibling the item is placed directly after; empty means first child of the parent, or first top-level item when the parent is empty
- `reverts` (optional, on events written by undo) names the event this event reverts, like git's "This reverts commit …"; the history shows it, and quick undo skips reverted events (ADR-059)
- `replaces` lists the event ids of the changes this change replaces; `seen` holds, for every device of the tree, the highest clock received from it (see Conflict resolution)
- `format` names the text format of title and note; initially always `markdown`; it keeps a later change of format possible without guessing what older texts meant
- Title and note changes carry the full text; one event is written per editing session (when the user leaves the field or after a short pause), not per keystroke, so the log does not grow with every edit

Every event has, besides its data: type name, schema version, `DeviceId`, `Clock` (hybrid logical clock), `actionId` (shared by all events of one user action, ADR-067) and `Timestamp` (for display only; ISO 8601 with the device's UTC offset at the time of writing, for example `2026-11-11T15:30:00+01:00`). `EventId` sits in the envelope (see Event envelope and encryption). Bookkeeping that only concerns one device, such as the order in which events were stored locally, is kept by the Local Event Store and is not part of the event.

Example of an event before encryption:

```json
{
  "type": "item.title-changed",
  "schema": 1,
  "deviceId": "3f2a…",
  "actionId": "5b19…",
  "clock": { "time": 1794407400123, "counter": 0 },
  "timestamp": "2026-11-11T15:30:00.123+01:00",
  "data": { "itemId": "a71c…", "title": "Ideas", "replaces": ["9e04…"] }
}
```

**Ordering of siblings**

- Position by reference: `item.created` and `item.moved` place an item directly after a named sibling (`afterItemId`), or first under its parent when empty
- The projection replays all events of a tree in one agreed order, sorted by `Clock`, then `DeviceId`; every device computes the same order, independent of the order in which events arrived
- `Clock` is a hybrid logical clock: a UTC time in milliseconds (64-bit) plus a counter (32-bit). A new event gets the device's current UTC time, or, if the highest clock in the tree's local log is equal or later, that clock with the counter raised by one
- The clock is not stored separately: it is always derived from the highest clock in the tree's local log, so events received by sync or file raise it automatically
- Events from a device whose clock is far ahead are accepted like all others, so every device keeps the same order; the receiving device warns that the other device's clock seems wrong
- `DeviceId` is compared as the lowercase text form of the GUID, character by character (ordinal), never by the binary GUID order of .NET
- The local and server sequence numbers only track sync progress; they never determine the tree
- Two items placed after the same sibling at the same time are ordered by that agreed event order
- If the referenced sibling is deleted, it stays in the projection as a tombstone, so the reference still resolves
- If the referenced sibling is under a different parent at the time of replay, the item is placed first under its parent

**Event exchange**

- Every exchange between two logs, whatever the transport, follows the same steps:
  1. Find out what the other side already has
  2. Send what it is missing
  3. Receive what this side is missing
  4. Recognise duplicates by `EventId` and skip them, so sending too much is always harmless
- Only step 1 differs per transport:
  - Server: by the server's sequence number; the device remembers up to which `ServerSequence` it has pulled, and which of its events the server has confirmed; the server cannot read device or clock, so its own numbering is the only option
  - File: by default unknown, so the whole log is exported, which is always correct thanks to duplicate detection; later option: remember what a device had at the last import from it and export only what is newer
- What a device has can be described like `seen`: for each device of the tree, the highest clock received from it; this works because every transport delivers a device's events without gaps
- Sync progress is tracked per destination (the server, each device synced by file), not by one synced flag per event
- The server stores each `EventId` only once, so pushing the same event again (for example after it reached the server through another device) is harmless
- Same `EventId`, same content: an envelope whose `EventId` is already known but whose content differs is rejected and reported as a defect, on the server and on every device; it never replaces the stored one; the simulation injects such envelopes
- Server requirements for the no-gap rule (the server cannot check it itself, because it cannot read `DeviceId`):
  - `ServerSequence` numbers are gap-free per account and become readable only in commit order, so a pull "since X" can never skip an event that commits later with a lower number
  - A pushed batch is stored all or nothing, in the order the device sent it
  - A device sends its batches strictly one after another and sends the next one only after the previous one is confirmed
  - These requirements are part of the store contract tests and a criterion of the open decision on the server store; the simulation injects concurrent pushes and failing batches
- The exact `ISyncTransport` interface is left to the implementation

**Importing a tree into another**

- From the user's view: "Import 'Ideas' into 'My thoughts' as a new branch"; offered for two trees on the same device, with the events of both present; the user's tree keeps its name, its theme and its inbox, and the imported tree arrives as one branch
- This is the one sanctioned way content moves between trees with its history: the imported tree's events are added to the target tree's log (union, re-encrypted with the target tree's key), so ids, history, devices and links are kept
- Right after, the importing device writes ordinary follow-up events as one action (one `actionId`, ADR-067): a new top-level item titled with the imported tree's name; `item.moved` of each of the imported tree's top-level items under it (its former inbox included, which loses its marking); `theme.set` for the target tree's own theme; `tree.inbox-set` for the target tree's own inbox (the latest marking wins, ADR-066); with ADR-082 and ADR-084 (Proposed) also `tree.tags-set` and `tree.relation-types-set` for the target's own branches and `item.moved` of the imported tag and type items into them, the imported tree's former branches becoming ordinary items; being newer, they win over the imported tree's name, theme and inbox
- The app then offers to move the former inbox's contents into the target's inbox, and to delete the imported tree from this device
- Predefined relation types and display names coincide (same fixed ids, same values), so they cause no conflicts (with ADR-084, Proposed: the importing device writes `tree.default-relation-type-set` for the target's default type, an imported `related` stays as a duplicate type; the imported tag and type items move into the target's branches in the same action, equal tag titles get the offer to join, owner 2026-11-19); own types and display names are added; a differing `tree.server-changed` becomes an ordinary conflict
- Devices or users that still hold the imported tree separately keep it; to work on the result there, they pair with the target tree (see open decision Membership)

**Conflict resolution**

- Events are commutative where possible; otherwise last-writer-wins per field, decided by the agreed event order (`Clock`, then `DeviceId`); with the hybrid clock this is usually the latest edit in real time
- `Timestamp` is never used to decide anything; only `Clock` is, which stays consistent even when device clocks drift
- Events that carry `replaces` (a list of event ids of the changes they replace; a missing value means the event replaces nothing known): `item.title-changed`, `item.note-changed`, `item.moved`, `relation-type.defined`, `display-name.set`, `tree.feature-changed`, `tree.server-changed`, `view.set`; once ADR-077 is accepted also `relation.note-changed`; once ADR-083 and ADR-084 are accepted also `relation.type-changed` and `relation-type.set`, while `relation-type.defined` and `display-name.set` are dropped
- Conflict: two or more changes of the same field of the same element (item, relation type, display name, tree feature, server address, view; once ADR-083 and ADR-084 are accepted also a relation's type, relation types are items and display names are dropped) that no later change replaces and whose values differ; the projection shows the winner by agreed order and marks the element, offering all competing values
- Concurrent changes with the same value are not a conflict (for example the predefined relation types written by two trees that are later merged; with ADR-084, Proposed, see the open decision on the `related` item in section 9)
- The Conflict View lists all conflicts of a tree, grouped by kind: items, relation types, display names, tree features, server address, views; once ADR-077 is accepted also relation notes; once ADR-083 and ADR-084 are accepted also relations (their type), without display names
- The user resolves a conflict by picking a value; this writes a new change that names all competing changes as replaced, so the conflict disappears deterministically on every device
- Delete conflict: a deletion conflicts with every change to the deleted item or anywhere in its subtree (including items created or moved into it) that the deleting device had not seen when it deleted
- To decide this, `item.deleted` carries what the deleting device had seen: for every device of the tree the highest clock it had received from that device (`seen`); a change in the subtree from device D with a clock above `seen[D]` was not seen
- The item stays deleted in the projection, but is marked with a conflict listing the unseen changes; the user either keeps it deleted (a new deletion that has seen them) or restores it (`item.restored`) together with the changes
- A conflict marker is derived from the log by the projection, not stored
- A move that would create a cycle is applied with the moved item placed at top level instead of under the requested parent
- Deleted items are tombstoned, never physically removed, to keep merges consistent
- Merging always means taking the union of two event logs, with events identified by `EventId`; trees are never merged directly, the projection rebuilds the tree from the combined log
- Two devices that were used on their own before pairing keep separate trees; pairing only adds a further tree, and content moves between them by importing a tree into another under a new item (see Importing a tree into another, D-84), after which the ordinary merge rule applies

**Event envelope and encryption**

- An event is stored and transmitted as an envelope: a few plaintext fields plus one encrypted payload
- Plaintext: `EventId`, envelope format version, `KeyId` (a short random id assigned when the key is created, naming which key of the tree encrypted the payload), the nonce and, once synced, `ServerSequence`; the account id comes from the device token and is not repeated in each event
- Encrypted: a payload wrapper that holds the event, that is everything else, including event type, `DeviceId`, `Clock`, `Timestamp` and the event data, so the server learns neither content nor structure nor which device wrote what
- Encryption with AES-GCM (256-bit key, fresh random nonce per event) via the Web Crypto API; every plaintext header field except the nonce (the encryption's own input) and `ServerSequence`, plus the account id and a fixed context label, are bound to the ciphertext as additional authenticated data, so the server cannot move, swap or relabel events unnoticed
- Before encryption the payload is padded to a fixed size bucket (the bucket list is fixed before the first release, see open decision Size limits), so the size of an envelope does not reveal the length of titles and notes
- The same envelopes are used in the local store, on the server and in the export file
- The export file has its own format version and a header with only the format version and `KeyId` (ADR-073); optional parts live inside the encrypted part, so later additions do not break older apps; a device without the key cannot name the tree a file belongs to
- Readable export files are long-lived formats too: the JSON format carries its own format version, Markdown and OPML a version marker; every later version can import readable files of every earlier release
- Hardening before release 1 (ADR-073, see Release 1 scope in section 9): all plaintext header fields bound as additional authenticated data, a payload wrapper with room for optional parts, a non-extractable key handle with the raw key stored only wrapped, local snapshots, search index and drafts encrypted at rest, an export file header with only format version and `KeyId`
- An envelope may be re-encrypted, for example when a tree is imported into another and its events are re-encrypted with the target tree's key; this does not count as rewriting the log, because the event inside stays unchanged

**Event versioning**

- Events are never changed or rewritten once written (only their envelope may be re-encrypted)
- Every event carries a type name and a schema version; type names are fixed strings such as `item.created`, never .NET class names, so code can be renamed without breaking stored logs
- Events are serialized as JSON inside the encrypted payload
- Event versioning must be downward compatible: an older app must never miss anything it already knows about. Rules:
  1. Existing events only grow: fields may be added, never removed or changed in meaning; an older app ignores fields it does not know
  2. The structure of the tree comes only from the basic events (`item.created`, `item.moved`, `item.deleted`, `item.restored`, `item.title-changed`, `item.note-changed`); a new feature that changes structure writes these events instead of an event of its own, so an older app always sees every item with its title and note in the right place
  3. A new kind of information gets a new event type instead of being added to the meaning of an existing one; older apps skip it without anything else being affected
  4. A missing field always means "unchanged", never "reset to a default", so an older app that writes an event without a newer field does not overwrite the information in that field
- Because of these rules the schema version normally stays at 1; it is a safety net for a breaking change that cannot be avoided: newer apps convert such events to the current shape when reading them (upcasting), older apps treat them as unknown
- Newer apps always read all older events
- Unknown event types or newer versions from a newer app on another device are stored and synced unchanged, but skipped by the projection until the app is updated
- The tree stays fully editable meanwhile; the app shows a warning that the tree contains changes from a newer app version and that some of them are not visible until the update
- Three versions, each with its own purpose:
  - Envelope format version: how an event is stored and encrypted; read by every device and the server
  - Schema version: the shape of one event type; read by the projection
  - App version: the installed release; recorded in snapshots and search index (see Persistence)

**Persistence**

- Client: the local store (IndexedDB, behind a port that keeps SQLite possible; see open decisions), behind a port of the application layer, with these logical stores per tree: events (envelopes), sync progress per destination, snapshots, search and backlink index, tree access (key, `KeyId`, account), device token, device settings and UI state, drafts, local usage counters; plus the layout version for migrations
- Drafts: text being edited is saved locally as a draft on every change and becomes an event at the end of the editing session; after a crash or a killed tab, the draft is recovered and turned into an event, so R1 covers text being typed as well
- Snapshots speed up startup; the projection replays only events after the latest snapshot
- Snapshots and the search index record the app version whose projection built them; after an update they are discarded and rebuilt from the full log, so events skipped by the older version take effect
- All local data is partitioned by tree: events, snapshots, key, account identity and search index are stored per tree id
- The app asks the browser for persistent storage (`navigator.storage.persist()`) so local data is not evicted under storage pressure
- The export file doubles as a backup; the app reminds the user to export when a tree has never been synced or exported for a while, at an interval the user can set
- Limitation: the export file is encrypted with the tree's key, so it only helps while a device with the key still exists (see open decision Key backup); the readable export is a backup that works without the key

**Search**

- All search runs on the client over the decrypted in-memory projection; no server-side search is possible by design
- Initial implementation: linear scan with case-insensitive and fuzzy matching, highlighting the path to each hit
- Matching rules: case is always ignored; accents are ignored by default ("Bar" finds "Bär", "cafe" finds "café"), with a toggle for exact accents; German equivalents always apply (ä = ae, ö = oe, ü = ue, ß = ss), since content carries no language; Markdown markers are ignored; emojis are searchable like any other character
- Search behaviour is not a foundation decision: the index is rebuilt from the log and can change with any app version
- If trees grow large: an incremental local index (inverted index of titles and notes) persisted in the local store and updated as events are applied
- Search scope can be limited to the currently focused branch
- Each tree has its own search index; one search can cover the open tree or all trees on the device, and every hit shows which tree it belongs to

**Security**

- End-to-end encryption: event payloads are encrypted on the device with the key (Web Crypto API); the server stores and relays only ciphertext
- The key never leaves the user's devices except through pairing, sharing (QR code or typed code) or an explicit key backup; the server holds no key material
- Readable export is unencrypted by design: it is never the default, never automatic, always for one tree or one of its branches only (ADR-080, Proposed), confirmed after a warning, and the file name marks it as unencrypted
- No AI on the user's thoughts: no AI model, on the device, on a server or at a third party, reads, summarises, classifies, places, links, completes or writes titles, notes, tags, views or structure; only deterministic helpers with visible rules (search, suggestions of known tags, templates, link suggestions by title match) (ADR-071)
- No code plugins: the app runs only its own code, because code inside the app sees decrypted content and the key handle, also of other people in a shared tree; it is extended by data (templates, views, relation types, tags, import profiles), by working on the readable export with other tools, or by a fork that is self-hosted on its own origin with its own policy (ADR-076, Proposed)
- Authorship is not proven: `DeviceId` and `userId` are not signed, so anyone holding a tree's key can write events that appear to come from another device or user; the server, or someone with a device token, can withhold events or send old ones again; the app's explanation of what it protects says so
- Each tree's key is random; daily use goes through a non-extractable key handle and the raw key is stored only wrapped by a non-extractable device key, unwrapped only for pairing, sharing or key backup, because those put it into a code (ADR-073); a script running in the page can still use the handle, which the strict CSP (ADR-045) guards against
- Authentication only by device token; all sync endpoints require a valid token; there are no user accounts or logins on the server, because they would link all trees of a user to one identity
- Server registration uses an account secret that is created on the device and shared only through pairing, sharing or an explicit key backup
- Events are scoped per account; no cross-account access
- HTTPS everywhere
- Content is untrusted input: titles, notes, display names, imported files and feedback texts may come from other people (shared trees); Markdown is rendered without raw HTML; only the link schemes `item:`, `https:`, `http:` and `mailto:` are allowed, everything else is shown as plain text; a collection of known attack strings must render harmlessly (gate); the Content Security Policy is the second line of defence, not the only one
- The server stores account secrets and device tokens only as hashes, so stolen server data cannot be used to join an account or push events
- Server counters for operations and bet signals: rejected registrations, rate-limit hits, number of accounts, storage used; counts only, no IP addresses or account ids, nothing per user; kept in memory or stored as daily totals at most; visible only to the operator
- Abuse protection on the server: maximum request size; maximum events and storage per account; rate limiting of registrations and requests in memory only (the IP address is used for the moment, never stored); registration mode per server: open, or only with a server join code that the operator hands out; the owner's default server uses "join code only"
- Join codes are stored on the server only as hashes (ADR-043); every member of a tree learns its server's join code through `tree.server-changed`; changing a join code is a server setting and does not affect accounts already registered
- Device tokens are issued per device and tree, so the server gets no ready-made link between the trees of one device

**Metadata protection**

- Threat model: (A) someone who obtains the stored server data, and (B) someone who can observe the running server, including the cloud provider and the operator
- Store as little as possible: the server keeps no receive times and no IP addresses; the server writes plain logs only, which record no IP addresses and are kept only briefly; there is no telemetry on the server (no Application Insights or similar service), as there is none in the client
- Envelopes are padded to size buckets (see Event envelope and encryption)
- Events are pushed in batches after a short pause in editing rather than one by one, so the server does not see the rhythm of typing; the price is that other devices see changes a few seconds later
- Priority: an observer of the running server (B) must not learn where the user is
- Remaining leak accepted for B: when devices are online and roughly when trees are edited, the IP address of each request, and over time the user's approximate time zone and changes of it when travelling
- The IP address is mitigated outside the app: the user can use a VPN, or not switch on server sync at all and sync by file only; the app explains this where server sync is switched on
- Sync requests send a neutral `Accept-Language` header, so they do not reveal the user's language and region
- Even without server sync, the app contacts the server it was loaded from (the hosted container or a self-hosted server) when it checks for updates and so reveals the IP address
- Requirement: the user controls updates, for example "check for updates only when I ask"; the app applies an update only when the user allows it
- Limitation in the browser: when an installed PWA is opened while online, the browser itself checks the server the app was loaded from for a new service worker (at most about once a day) and the app cannot switch this off; full control over update checks needs the self-hosted package (the browser then contacts that server) or the later store app or downloadable package
- Where VPNs are blocked or risky, the user can stay without any server contact apart from installing and updating: file-only sync, updates only on request; or use a self-hosted server they trust
- A network observer (for example an internet provider) can still see that a device connects to the sync server or the server the app was loaded from; the app says so in plain words and does not promise to hide that ThoughtTree is used
- Manual and scheduled sync reduce what B sees: without a live connection, the server sees only the sync times, not when the user is online or writing
- Default sync mode: when server sync is switched on, the user chooses the mode; live is preselected, because reliability comes first; in manual and scheduled mode the app shows how many changes are not yet synced and reminds the user when changes have waited longer than a time the user sets
- Scheduled sync runs at a random moment within a time window the user sets; the app converts the window to UTC once and keeps it fixed, so sync times show neither round local times nor the switch to and from daylight saving time nor travel; the price is that in summer the window shifts by an hour in local time
- Consequence accepted: losing the key on all devices means the notes cannot be recovered
- Private and shared are recorded in the event log and shown in the UI, but not enforced by cryptography: every holder of the key can read and change the whole tree, there is no read-only access, and a key passed on outside the app leaves no trace

**Tree access**

- The event log restores a tree's content; the tree access restores the right to read and sync it; it is kept outside the log by the Tree Manager
- Tree access consists of: key, `KeyId`, account id and account secret
- Not part of it: device tokens (per device, can be requested again with the account secret), whether a device uses the server and its sync mode (settings of each device), tree name and server address (come from the events)
- Pairing, sharing and a future key backup all hand over exactly the tree access; pairing adds the user identity, sharing does not
- The pairing and sharing code may additionally contain the server address and its join code, if the tree uses server sync, so the new device can fetch the log without the user typing the address
- The code has its own format version, so later additions do not break pairing between old and new apps

**Feature flags and tree features**

- Switches are kept apart by kind:

| Kind | Purpose | Source | Lifetime |
| --- | --- | --- | --- |
| Release flag | Hide unfinished code | Configuration shipped with the app, per environment | Temporary: removed once the feature is complete |
| Experiment flag | Give a feature to part of the users and learn whether it works | Assignment per account on the server (a random share, adjustable), or drawn by the device from a share in the shipped configuration (a locally drawn flag is device state, never synced, ADR-031) | Until the review of its bet card decides: raise to everyone or remove |
| Operations flag (kill switch) | Switch something off quickly if it causes trouble | Server configuration | Long-lived |
| Permission flag | What an account may do (for example use the private feedback inbox) | Stored per account on the server, reported to the device during sync | Long-lived |
| Tree feature | Features the user chooses for a tree (for example relations, tags, "Headings become thoughts" (ADR-079, Proposed); tags and headings becoming thoughts are on by default; relations on by default with ADR-084, Proposed) | `tree.feature-changed` events, so all devices of a tree agree; older apps ignore unknown names | As long as the user wants |
| Device setting | Language, sync mode, use of the server, showing experimental features, whether feedback is offered (ADR-070), the hand side (ADR-013) | Stored on the device, never synced | As long as the user wants |

- Flags are never events; release flags come only from shipped configuration, so they need no server contact and work for file-only users; a self-hosted package brings its own configuration
- Experiments respect privacy: there is no third-party flag service, no tracking and no telemetry; the server stores only the flag per account; users who get a feature still decide themselves whether to use it; results come only from the privacy-compatible signals of the experiment's bet card (feedback, server counters, voluntary signal answers)
- No switch ever makes trees differ: switches only hide commands and UI; the projection always applies every event its app version knows, so switching a tree feature off hides for example relations but does not remove them

**Testing**

Test goals, derived from the quality goals; a gate has zero tolerance (a single failure stops delivery), a watch is measured and tracked over time:

| Quality goal | Test goal | Kind |
| --- | --- | --- |
| Reliability | No data loss: every written event survives any sequence of offline work, sync, merge, import, update, conflict resolution, the app closing while writing, and full storage | Gate |
| Reliability | Convergence: the same events give the same tree, conflicts, display names (dropped by ADR-084, Proposed), tree name and private/shared status on every device | Gate |
| Reliability | Longevity: all stored data of every released version stays readable; older apps see every item and text of newer data | Gate |
| Privacy & Security | Confidentiality: nothing but the allowed plaintext fields leaves the device (server store, export files, network requests, logs) | Gate |
| Privacy & Security | Metadata: padding, batching, no stored IP addresses or times, neutral headers and UTC scheduling work as decided | Gate |
| Privacy & Security | Integrity: tampered, moved or swapped envelopes and wrong keys are detected and rejected | Gate |
| Privacy & Security | Access: the server never gives one account's events to another account's token | Gate |
| Usability | Core flows on touch (capture, edit, move, indent, Focus, search, resolve conflicts, pair, file round trip, move along connections on the map; share once the sharing UI exists) work on phone and tablet screen sizes | Gate (the UX foundation decisions are made; release 1 scope, decision 5) |
| Usability | Accessibility: no violations in automated checks; screen reader experience through user feedback | Gate (automated), watch (feedback) |
| Usability | Languages: every text exists in English and German, a missing text fails the build; layouts hold with longer German texts | Gate |
| Usability | Feel: daily use by the owner and user feedback | Watch (judgement) |
| Portability | Browsers: core flows work in Chromium, WebKit and Firefox, including starting the installed PWA offline | Gate |
| Portability | Self-hosting: the package runs without Azure, with its own store | Gate |
| Portability | Data portability: export followed by import gives back the same structure, titles, notes and relations (JSON), within each format's limits | Gate |
| Portability | Obsidian round trip: the guarantees G1 to G7 of [zettelkasten-claim.md](zettelkasten-claim.md) (round trips from either side, no events for unchanged files, exact updates with visible conflicts, links resolve in Obsidian, nothing silently lost); the manual check in Obsidian on desktop and iPad before the claim is made is a Confirm step (owner decision 2026-11-18) | Gate |
| Maintainability | Fast feedback: the branch check runs on every push within a few minutes and the push gate on every pull request within a set time budget (for example 10 minutes) | Watch |
| Maintainability | Reproducibility: every failure can be repeated from its seed; unreliable tests are fixed, never ignored | Gate |
| Maintainability | Architecture rules: automated checks that Domain does not depend on the browser, the server does not reference Domain, only Contracts are shared | Gate |
| Maintainability | Depth of Domain tests measured by mutation testing, not coverage alone; changed Domain code must reach a mutation score of 80 % (release 1 scope, decision 5) | Gate in the push gate for changed code; watch nightly for the whole Domain |
| Performance | Download size budget and complexity checks on reference data (1,000, 10,000, 100,000 events); absolute timings are design targets, not tests | Gate |

Harness for agentic development (the test harness is the specification the agents work towards):

1. Tests express intent, not just examples: invariants, property-based tests, simulations and fixed stored formats carry the main load; example tests mainly document behaviour
2. The harness is protected: stored test data from released versions is append-only; deleting a gate test or weakening an assertion needs human approval; every change reaches `main` through a pull request, protected paths need the owner's review (CODEOWNERS with branch protection), and CI compares each pull request with its base to report removed tests, changed expected values and changes to protected files; agents may add tests freely
3. Failures say what to do: name the broken invariant, show a minimal counterexample, give the seed to reproduce
4. Zero flakiness: every test is deterministic; an unreliable test is a defect with top priority
5. The whole gate suite runs in an agent's sandbox, without Azure accounts, secrets or network (SQLite, Azure Storage emulator, local browsers); tests against real Azure belong only to the deployment tests
6. The quality of the tests is measured: mutation testing checks that tests notice deliberately broken code; agent-added tests must meet the agreed score
7. Architecture rules are checked automatically on every push
8. Documentation is part of the harness: this document, its ADRs and a short agent guide in the repository (rules, commands, what may and may not be changed) give agents the intent; changing an ADR is a human decision
9. UI tests use stable test identifiers on elements, not texts or layout
10. Feedback is fast enough for small steps: besides the full gate within the time budget, a fast subset for the area being changed answers within seconds

Kinds of tests and loops: tests are not a numbered stack; they are grouped by role, and run in loops by cost.

| Role | Kinds |
| --- | --- |
| Specify: what the system should do, as acceptance criteria, written first | Domain specifications (scenario tests), UI specifications |
| Secure: correctness and rules | Compiler and analyzers, Domain properties and invariants, formats, simulation, application layer rules (privacy confirmations, language resources), component rules, server, performance (size budget, complexity) |
| Confirm: the whole system in real environments | End-to-end, deployment, manual |

| Loop | Time | Contains |
| --- | --- | --- |
| Inner loop (the agent while working) | Seconds | Compiler; specifications, properties and component rules of the area being changed; UI specifications in Chromium |
| Branch check | Within a few minutes; runs in CI on every push to a branch (owner decision 2026-11-15, saves CI minutes) | Build with warnings as errors, analyzers, banned APIs and architecture tests, all specifications and securing tests that need no browser (including regression seeds and format data), UI specifications in headless Chromium, licence check, every `verify` line that needs no browser, container, Azure account or network |
| Push gate (runs on pull requests only; required for merge) | Within the time budget (for example 10 minutes) | Everything in the branch check, plus: all specifications and all securing tests (including size budget and complexity checks), regression seeds, mutation testing of changed Domain code, UI specifications in all three browsers, end-to-end smoke set in Chromium, self-hosted container test (CI with Docker; from the first server release, decision 5) |
| Nightly | Hours | Long simulations, full end-to-end in three browsers, mutation testing of the whole Domain |
| After deployment | Minutes | Deployment smoke tests |
| Before release | Minutes | Short guided release check: release 1 on phone, tablet and desktop (release 1 scope, decision 5); later releases on the owner's phone and on every device type whose code changed |

Domain tests:

- Covers everything that decides what the tree is: projection, sibling order, cycles, hybrid clock, conflict detection, relations, types, display names (dropped by ADR-084, Proposed), tree features, derived facts (private or shared, tree name), text normalisation, versioning, and the command logic that decides which events a user action writes
- Domain is pure logic: no storage, network, browser or asynchronous I/O; time and ids come from outside
- Invariant catalogue: the core rules, each with an id; every test that checks an invariant names its id, so a failure says which rule broke

| Id | Invariant |
| --- | --- |
| INV-1 | Order independence: any order of the same events gives the same tree |
| INV-2 | Duplicates harmless: applying an event twice changes nothing |
| INV-3 | Merge is union: merging logs in any grouping gives the same tree |
| INV-4 | Tree shape: every item has at most one parent, there are no cycles, every visible item is reachable from top level |
| INV-5 | Nothing lost: every item ever created is in the projection (visible or deleted), every text ever written is in its history |
| INV-6 | Snapshot equivalence: snapshot plus the remaining events gives the same tree as a full replay |
| INV-7 | Incremental equals full: applying events one at a time gives the same tree as rebuilding |
| INV-8 | Clock moves forward: a new event's clock is above every clock in the local log |
| INV-9 | Conflicts agree: all devices with the same events see the same conflicts; a resolution removes them everywhere |
| INV-10 | Older apps miss nothing: skipping unknown events still shows every item and text of the known events |
| INV-11 | Normalised text: every text in every event is NFC; normalising twice changes nothing |
| INV-12 | Symmetric relations are one: A–B and B–A of a symmetric type are the same relation |
| INV-13 | One id, one content: envelopes with the same `EventId` are identical; a known id with different content is rejected and reported, never stored |
| INV-14 | Undo returns: reverting an action (all events with its `actionId`) and then reverting the undo gives back the same tree, as long as no other change touched its items in between |

- Reference model: a deliberately naive projection (sort all events, replay one by one) is the referee; property tests check that the real, optimised projection always gives exactly what the model gives; the model is protected like the stored test data and changes only with human approval
- Generators create random but valid histories of several devices working concurrently, biased towards hard cases (same `afterItemId`, moves into deleted subtrees, cycles, equal clocks, clocks far ahead, unknown event types); they are reused by the simulation
- Scenario tests in readable form (given events, when an action, then the tree) cover the runtime scenarios of section 6
- Runs in under about 30 seconds on every push, with fixed seeds plus one new random seed; nightly runs use many more cases
- Tooling: xUnit v2 (2.9.x, because LambdaTale 2.9.x requires it; no LambdaTale 3.x exists on nuget.org, checked 2026-11-15; revisit when LambdaTale supports xUnit v3, ADR-075); CsCheck for property-based and model-based tests; Shouldly for assertions; LambdaTale 2.9.x (bbv's port of xBehave.net) for scenario tests; Verify for expected trees as text outlines; `FakeTimeProvider` for time; Stryker.NET for mutation testing (changed code via `--since`); Microsoft code coverage for information only; ArchUnitNET for architecture rules

Format tests:

- Stored test data per release: a generator in the harness writes, with a fixed seed and a test key, encrypted event logs (with every event type, including tags, themes and views), encrypted export files, readable export files (JSON, Markdown, OPML), view queries, pairing and sharing codes, recorded API requests and responses, and the expected tree as a text outline into `tests/data/formats/<version>/`; the folder is append-only; snapshots are not included (local caches, rebuilt after updates)
- Sample Obsidian vaults under `tests/data/obsidian/`: one per style (folders, `up` property, Luhmann ids, Dataview inline fields) and a mixed one with the edge cases; each import and export result secured by Verify snapshots the owner approves (Zettelkasten guarantee G5)
- New reads old: the current version reads every stored version and gets exactly the expected tree
- Old reads new: CI builds the previous release from its git tag and lets its Domain read data written by the current version; every item and every text must be there (INV-10)
- Round trip: the current version writes and reads back without loss
- Encryption: fixed test data (key, nonce, authenticated data) gives exactly the expected bytes; tampered envelopes (flipped byte, swapped `EventId`, other account id, other envelope version or context label, wrong `KeyId`) are rejected; every encrypted size is one of the padding buckets; an unknown future envelope version leads to a clear request to update, not a crash
- Local storage is a format too: a new app version opens the local store of the previous one and migrates it without loss; tested in real browsers in the end-to-end tests
- The API is a format too: it carries a version in its address (for example `/v1/events`); the new server handles requests of old clients, the new client works with an older server or says clearly that the server needs updating
- Pairing and sharing codes: user name and server address have a maximum length; a test checks that the largest possible code fits into a QR code that phones read reliably

Simulation tests:

- Deterministic simulation: the whole system minus browser and real network runs in one process; a seed controls everything, so the same seed gives exactly the same run
- Simulated: devices of several users with the real client logic on in-memory storage; the real server core with an in-memory store; network (delays, lost, duplicated and reordered messages, unreachable or restarting server); files (export and import, the same file twice, old files late); clocks (offsets, jumps, days ahead); users (random actions from the Domain test generators, including pairing, sharing, importing a tree into another and changing the server); failures (app closed while writing, full storage, rejected token, app update in mid-run)
- Checked continuously: INV-4, INV-8, the no-gap rule, the server holds only envelopes; checked after a final full sync: INV-1, INV-5, INV-9
- A failure reports the seed with a command to repeat it, a shortened run with only the steps that still fail, and a readable timeline per device naming the broken invariant
- Every push runs the regression seeds plus a few new random ones within about 1 to 2 minutes; nightly runs use thousands of seeds and long runs; a seed that failed once is added to the regression list (`tests/data/regression-seeds`), which is append-only
- Tooling: own harness built on CsCheck, so its generators and shrinking work for whole runs

Application layer and component rule tests:

- Use cases, UI state and UI decisions of the client application layer are specified in `Client.App.Specs` (Specify), with xUnit, fast and without a browser; privacy-critical confirmations (sharing, unencrypted export, importing a tree into another) and language resources (every key in English and German, matching placeholders, no unused keys) are rules in `Client.App.Rules` (Secure) and gates
- bUnit is used only for generic rule checks: every component is rendered in a catalogue of sample states and checked for: test ids from the catalogue on every element that shows a domain concept, an accessible name on every interactive element, `dir="auto"` on user content, and no hard-coded text (rendered with pseudo-localisation, every visible text must be changed)
- The test id catalogue is a set of C# constants named after the glossary concepts, shared by components and tests; UI specification and end-to-end tests are written in C# with Playwright for .NET

UI specification tests:

- Specify and check how the UI behaves while it is being built; usable in the agent's working loop; not end-to-end tests
- The real client in a real browser with everything behind it faked (storage, server, time); seconds per test; Chromium in the fast loop, WebKit and Firefox in the push gate
- Written as specifications with LambdaTale on Playwright for .NET, in the domain language (given a state, when the user acts with real touch or keyboard input, then the visible result); they are the acceptance criteria of a UI feature and are protected like other gate tests
- A spec host, a test build of the client in a separate test-only project, is loaded once per test worker and offers hooks to load a state, reset it and control time; test hooks never reach the production build
- Tests wait for meaningful states (the app signals idle after an action) and Playwright's automatic waiting, never for fixed times; animations are off in the spec host
- Selectors: domain concepts (item, title, conflict marker, …) by test ids from the catalogue; controls by role and accessible name (from the resource files), so a control without an accessible name cannot be found; phone, tablet and desktop sizes; runs headless without network or secrets

Accessibility tests (target: WCAG 2.2, level AA; automated tools cover only part of the criteria, the rest relies on feedback, see risks):

- An axe scan (Deque.AxeCore.Playwright) after every step of every UI specification; any finding fails the test
- Core specifications run a second time with the keyboard as the only input: every flow completable, focus always visible, no keyboard traps, sensible focus order, focus in the right place after actions
- Changes that are not immediately visible are announced through a live region; specifications check the announcement
- Touch targets at least 24×24 CSS pixels; usable at 320 pixels width without horizontal scrolling; nothing cut off with enlarged text and text spacing; no animations with reduced motion; axe scan again in forced-colours mode
- A unit test checks the contrast of every text and background pair of the design tokens in light and dark mode
- Component rules (accessible names, alternatives to gestures) as described for the component rule checks
- Screen reader experience (VoiceOver, TalkBack, NVDA) is not tested manually; it relies on user feedback, with accessibility as a feedback category
- End-to-end tests are reduced to what needs the whole system: sync through the real server, offline start and installation, update control, migration of local storage, file export and import between browser contexts, browser differences

Server tests:

- Store contract tests: one suite runs against every store implementation (in-memory for the simulation, SQLite for self-hosting, the chosen Azure store via the Azurite emulator), including duplicate detection and ordering by `ServerSequence`
- API tests over HTTP with the ASP.NET Core test host: versioned endpoints and the recorded requests of every release; the SignalR hub only says that new events are available, never content
- Security tests (gate): requests without a token, with an invalid token or with a token of another account are rejected; registration with a wrong account secret or join code fails; malformed envelopes and oversized requests are rejected cleanly; store and captured logs contain no IP addresses and no receive times
- The self-hosted package is started as a container and tested in CI (push gate) and nightly, not in the inner loop, because the agent's sandbox may not have Docker

End-to-end tests:

- Scenarios: (1) two devices sync through the real server, live mode with a SignalR notification and manual mode; (2) offline start from the service worker cache, edits survive a reload; (3) installability: manifest, service worker, installation criteria; (4) update control between two real builds, rebuild of snapshots and search index after consent; (5) migration of local storage from the previous release; (6) file export in one browser context, import in another; (7) pairing by typed code, and in Chromium by QR code with a video file as fake camera; (8) app closed while writing, no event lost; (9) scenario 1 against the self-hosted container
- The fixture starts the whole system locally (published client as static files, server with SQLite), without Azure or secrets; every test gets fresh browser contexts and a fresh server store; no waiting for fixed times; the server can run with fake time
- On failure Playwright keeps a trace, stored by CI as an artifact for diagnosis; no traces for passing runs; no automatic retries
- Push gate: scenarios 1, 2, 6 and 8 in Chromium (release 1, without a server: 2, 6, 7 and 8, owner decision 2026-11-15); nightly: all scenarios in Chromium, WebKit and Firefox, scenario 9 in CI with Docker
- Every release is reproducible from its git tag and its build is archived (for example attached to a GitHub release), from the first release on; update, migration and "old reads new" tests depend on it

Deployment tests:

- After each deployment to test; in release 1 without a sync server the sync checks are left out (once a server exists, each run creates a new tree and account, so no credentials are stored): the app loads and its service worker registers; a version endpoint shows the deployed commit; the service worker file is served without caching; an account registers with the join code, an event is pushed and pulled, SignalR connects; HTTPS with HSTS and security headers are present, the page makes no requests to third parties, logging and telemetry record no IP addresses
- After promotion to production: read-only checks only (health, version, headers, service worker); sync is proven in the test environment with the identical image (ADR-047); if a check fails, the previous archived release is deployed again automatically
- The self-hosted container image is built, published and checked with the same smoke tests as the test environment

Performance tests:

- Reference datasets, generated with fixed seeds: 10,000 items with 100,000 events; a deep tree (50 levels); a wide tree (2,000 children under one item); long notes; many relations; many conflicts
- The app sets timing marks (`performance.mark`) for app start until the tree is visible, input until display, search until first hits, and app start until the first sync completes; tests read them, and they help diagnosis on real devices
- Gate: a download size budget for the first load, measured exactly, with a fixed upper limit
- Gate: complexity checks: replay, search and conflict detection are measured with 1,000, 10,000 and 100,000 events and the ratio is checked, so a quadratic algorithm fails regardless of the machine's speed
- Absolute timings are not used as tests, because they are too unreliable on shared machines; the targets of section 10 are design targets, secured by the size budget and the complexity checks

Feedback, reports and signals:

- Private feedback inbox (optional): instead of a GitHub issue, feedback can be sent to the server's inbox, end-to-end encrypted on the device with the operator's public key; the server stores only ciphertext, the operator collects and decrypts it on their own device, and it is deleted from the server after collection; available to accounts with the permission flag, assigned at first as an experiment to a share of accounts (an account is a tree, so the app offers the inbox if any tree on the device has the permission); the operator's public key is reported by the server with the capabilities; an operations flag can switch the inbox off; only on the user's initiative, with the same preview; the IP address is not stored; what arrives is untrusted input, and the operator decides what becomes a GitHub issue
- Diagnostics view: the app shows its timing marks and the shape of the tree on demand, only on the device
- Performance report: if a user finds the app too slow, they can send a report containing only numbers: timings, app version, build and environment, storage engine, AOT or interpreted, browser and operating system family, screen size class, the active switches (release, experiment and permission flags, the tree features of the affected tree, whether experimental features are on for this device), and the shape of the tree (number of items and events, depth, maximum width, distribution of note lengths, number of relations and conflicts); never titles, notes, names, device labels, display names, ids or timestamps; the user sees the report before sending and can edit or cancel it
- The report is sent as a pre-filled GitHub issue that the user submits themselves, or copied or saved as a file, or optionally sent to the private feedback inbox end-to-end encrypted to the operator (ADR-055); never unencrypted through a server; the app says that a GitHub issue is linked to the user's GitHub account and public
- Reports are untrusted input: the report is a JSON block with a fixed schema; the pipeline reads only that block, validated against the schema, and ignores all free text; agents treat it as data, never as instructions; a human label decides whether a report is worked on
- From the tree shape in a report the harness generates a synthetic dataset of the same shape to reproduce the slowdown; once the cause is found, the dataset joins the reference datasets and its complexity check becomes a gate
- Signals for bets (ADRs of kind `bet`) through the feedback form: each bet can define a few optional signal questions with fixed answer choices, shown in the feedback form; users can also tick "include usage summary": counts and shares computed on the device from one tree the user chooses (for example sync mode in use, conflicts resolved in the last 30 days, uses of predefined versus own relation types without names, share of titles with Markdown, median taps to capture); never content, names or timestamps; never automatic or by default; shared trees are excluded, because they contain other people's activity (only the number of trees and of shared trees on the device may be counted); shown in the preview like everything else
- Local usage counters: the counts needed for usage summaries are kept on the device only, per tree, as daily totals for at most 30 days; never synced, never sent except in a usage summary the user ticks
- Every signal is defined in its bet card with exactly the numbers it collects; a new signal needs human approval
- A regular agent review (for example monthly, as an issue) counts signal answers per bet and, when a review trigger has occurred, proposes a review of the ADR with the numbers; the decision stays with the owner
- The report format is designed so that error reports (what went wrong and where in the code, without content) can use it later

Manual tests and feedback:

- The owner's daily use on phone, tablet and desktop is the main manual test
- In-app feedback: users (and the owner) send findings (bug, usability, accessibility, performance, other) from within the app, using the same mechanism as the performance report: a pre-filled GitHub issue the user submits, copy or file, or the private feedback inbox (ADR-055); the user's own description, plus optionally the diagnostics block with numbers only; previewed and editable; never unencrypted through a server; free text is untrusted input (ADR-051); a human label decides whether agents work on it; the feedback form itself is accessible
- Release check: before promotion to production, the owner runs a guided check of about five minutes on each device (release 1: phone, tablet and desktop; later releases: the phone and every device type whose code changed), shown in the test build of the app: the update arrives and is applied with consent; the tree is complete; sync works; on the iPhone started from the home screen and offline (on a borrowed iPhone, see the Safari decision); the result is sent with the same mechanism as an issue linked to the release, and is part of the manual approval
- Exploratory testing by an agent: nightly or before a release, an agent uses the app with tasks (for example "collect ideas for a trip and sort them", "share a tree") at phone size and with keyboard only, and reports what was confusing, slow or broken; findings become issues that need a human label
- No paid device clouds; free programmes for open-source projects can be checked once the repository is public

Compiler and analyzers (the first gate):

- Nullable reference types on, warnings treated as errors, .NET analyzers at a strict level, `dotnet format` checked in CI
- Banned API analyzer (Microsoft.CodeAnalysis.BannedApiAnalyzers): forbidden calls (for example `DateTime.Now`, `Guid.NewGuid`, `new Random`, browser and I/O APIs in Domain) are compile errors with a message saying why
- Snapshot files of Verify (`*.verified.*`) are protected like the stored test data: CI never accepts new snapshots automatically, and changes need human approval

Principles that shape the code from the start:

- Everything random or time-based is replaceable: time comes only through .NET's `TimeProvider`, ids and random values (item ids, event ids, nonces, the random sync moment) through a replaceable generator, so every test can be reproduced exactly
- Several devices can be simulated in one process without a browser: the local store sits behind an interface with an in-memory implementation, and transports, scheduler and clock are replaceable; simulations run many devices with random online and offline phases, clock offsets, duplicate imports and merges, and check that all end with the same tree and the same conflicts
- Stored formats are tested for compatibility: a collection of real stored data (events, envelopes, export files, pairing codes) from every released version is kept in the repository and read by every new version; the previously released Domain version reads events of the new version and must still see every item and every text (rules 1 to 4 of event versioning); fixed test data for encryption makes sure a library update cannot silently change the format

Testing rules:

- The Domain project carries most of the logic and is covered by unit tests without browser or server
- Property-based tests check convergence: the same set of events, in any arrival order and with duplicates, always gives the same tree; further ones cover cycles, references to deleted or moved siblings, conflicts and replay after snapshots
- Privacy is tested automatically: padding sizes, no IP addresses or receive times stored, neutral `Accept-Language`
- Browser tests also run on WebKit (Safari's engine), where most platform limitations come from
- The self-hosted package is tested in CI with its own store
- Test data never contains real notes

**UI / UX**

- The UX concept is a separate document: [ux-concept.md](ux-concept.md)
- Accessible from the start: every component works with screen readers and keyboard only; every drag or gesture has an alternative (menu or keyboard command); sufficient contrast and scalable text
- Touch-first for phone and tablet: tap to select and tap again to edit (D-91), drag-and-drop to move (D-89), indent and outdent from the editing toolbar and typing shortcuts (D-54, D-75, D-76); details in the UX concept
- Focus on any item as if it were its own document
- Breadcrumb for the current focus path
- Desktop add-on: keyboard shortcuts such as Enter (new sibling), Tab / Shift+Tab (indent/outdent), arrows (navigate), Ctrl+Up/Down (move)
- Tree switcher to move between the trees on a device
- An item shows which device last changed it and when, using the device label; the full change history per item is shown in the item's history (ADR-059), since all events are kept
- Times are shown in the viewing device's local time; the original local time of the change (from the offset in the timestamp) can be shown as well, for example when a change was made while travelling
- Every tree is marked private or shared; a shared tree lists its users and their devices, and changes show the user as well as the device
- A short, plain explanation of what ThoughtTree protects and what it does not, shown when server sync is switched on and available at any time
- "Pair my other device" and "Share with another user" are two separate actions with separate wording and a warning before sharing
- UI state (collapsed items, current focus, open tree) belongs to one device: it is stored locally per tree and never synced; only domain events go into the event log

## 9. Architecture Decisions

Each decision is a separate, agent-optimised record in [`docs/adr`](adr/README.md): scope (`applies_to`), rules (MUST / MUST NOT / SHOULD / MAY), a `verify` command and a one-line reason.

| ID | Decision | Applies to |
| --- | --- | --- |
| [ADR-001](adr/ADR-001-blazor-webassembly.md) | Client in Blazor WebAssembly | `src/ThoughtTree.Client*/**` |
| [ADR-002](adr/ADR-002-event-sourcing.md) | Event sourcing as data model | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-003](adr/ADR-003-own-sync-service.md) | Own ASP.NET Core sync service | `src/ThoughtTree.Server*/**` |
| [ADR-004](adr/ADR-004-sibling-order-by-reference.md) | Sibling order by reference | `src/ThoughtTree.Domain/**` |
| [ADR-005](adr/ADR-005-tombstones.md) | Tombstones instead of hard deletes | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**` |
| [ADR-006](adr/ADR-006-event-exchange.md) | Event exchange behind ISyncTransport | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**` |
| [ADR-007](adr/ADR-007-file-sync.md) | Device-to-device sync by encrypted file | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Contracts/**` |
| [ADR-008](adr/ADR-008-standalone-first.md) | Standalone first | `src/ThoughtTree.Client*/**` |
| [ADR-009](adr/ADR-009-merge-is-union.md) | Merging is the union of logs; no special root | `src/ThoughtTree.Domain/**` |
| [ADR-010](adr/ADR-010-multiple-trees.md) | Multiple independent trees per device | `src/ThoughtTree.Client*/**` |
| [ADR-011](adr/ADR-011-pairing-and-sharing.md) | Pairing and sharing are separate | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-012](adr/ADR-012-one-key-per-tree.md) | One key per tree, equal access | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Domain/**` |
| [ADR-013](adr/ADR-013-device-local-state.md) | Device state is never synced | `src/ThoughtTree.Client*/**` |
| [ADR-014](adr/ADR-014-hybrid-logical-clock.md) | Event order by hybrid logical clock | `src/ThoughtTree.Domain/**` |
| [ADR-015](adr/ADR-015-envelope-plaintext.md) | Minimal plaintext in the envelope | `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**` |
| [ADR-016](adr/ADR-016-downward-compatible-events.md) | Downward-compatible event versioning | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**` |
| [ADR-017](adr/ADR-017-json-fixed-type-names.md) | JSON with fixed type names | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**` |
| [ADR-018](adr/ADR-018-visible-conflicts.md) | Conflicts are visible and resolved by the user | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-019](adr/ADR-019-readable-export.md) | Unencrypted export only as readable tree | `src/ThoughtTree.Client*/**` |
| [ADR-020](adr/ADR-020-no-server-accounts.md) | No user accounts on the server | `src/ThoughtTree.Server*/**`, `src/ThoughtTree.Client*/**` |
| [ADR-021](adr/ADR-021-server-address-event.md) | Server address is a property of the tree | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-022](adr/ADR-022-binary-blobs.md) | Binary data as separate blobs | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**` |
| [ADR-023](adr/ADR-023-one-parent.md) | One parent per item | `src/ThoughtTree.Domain/**` |
| [ADR-024](adr/ADR-024-relations.md) | Relations separate from the tree | `src/ThoughtTree.Domain/**` |
| [ADR-025](adr/ADR-025-predefined-relation-types.md) | Relation types defined per tree, predefined set | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-026](adr/ADR-026-translatable-display-names.md) | Translatable display names for structural elements | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-027](adr/ADR-027-project-dependencies.md) | Domain and Contracts projects | `src/**` |
| [ADR-028](adr/ADR-028-tree-access.md) | Tree access | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Contracts/**` |
| [ADR-029](adr/ADR-029-text-normalisation.md) | Text normalisation before writing | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-030](adr/ADR-030-metadata-protection.md) | Metadata protection | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**` |
| [ADR-031](adr/ADR-031-switches.md) | Kinds of switches | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**`, `src/ThoughtTree.Domain/**` |
| [ADR-032](adr/ADR-032-resource-texts.md) | App texts in resource files | `src/ThoughtTree.Client*/**` |
| [ADR-033](adr/ADR-033-accessible-ui.md) | Accessible, direction-neutral UI | `src/ThoughtTree.Client*/**` |
| [ADR-034](adr/ADR-034-replaceable-time-random.md) | Replaceable time and randomness | `src/**` |
| [ADR-035](adr/ADR-035-protected-harness.md) | Protected test harness | `tests/**`, `.github/**` |
| [ADR-036](adr/ADR-036-user-controlled-updates.md) | User-controlled app updates | `src/ThoughtTree.Client*/**` |
| [ADR-037](adr/ADR-037-api-versioning.md) | Versioned, compatible sync API | `src/ThoughtTree.Server*/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**` |
| [ADR-038](adr/ADR-038-local-storage-migration.md) | Local storage migrations without loss | `src/ThoughtTree.Client*/**` |
| [ADR-039](adr/ADR-039-controlled-concurrency.md) | No uncontrolled concurrency or timers | `src/**` |
| [ADR-040](adr/ADR-040-server-core-library.md) | Server logic as a library | `src/ThoughtTree.Server.Core/**`, `src/ThoughtTree.Server/**` |
| [ADR-041](adr/ADR-041-client-application-layer.md) | Client application layer, thin components | `src/ThoughtTree.Client*/**` |
| [ADR-042](adr/ADR-042-browser-adapters.md) | Browser APIs behind interfaces | `src/ThoughtTree.Client*/**` |
| [ADR-043](adr/ADR-043-hashed-server-secrets.md) | Server secrets only as hashes | `src/ThoughtTree.Server*/**` |
| [ADR-044](adr/ADR-044-abuse-protection.md) | Abuse protection and registration modes | `src/ThoughtTree.Server*/**` |
| [ADR-045](adr/ADR-045-strict-csp.md) | Strict Content Security Policy | `src/**`, `infra/**` |
| [ADR-046](adr/ADR-046-infrastructure-as-code.md) | Infrastructure as code, deployment without stored secrets | `infra/**`, `.github/**` |
| [ADR-047](adr/ADR-047-one-container-image.md) | One container image everywhere | `src/ThoughtTree.Server*/**`, `infra/**`, `.github/**` |
| [ADR-048](adr/ADR-048-image-supply-chain.md) | Hardened image and supply chain | `src/ThoughtTree.Server*/**`, `.github/**` |
| [ADR-049](adr/ADR-049-apache-licence.md) | Apache 2.0, public from the first release | `**` |
| [ADR-050](adr/ADR-050-dependency-licences.md) | Approved licences for dependencies | `**/*.csproj`, `**/package.json`, `Directory.Packages.props` |
| [ADR-051](adr/ADR-051-user-reports.md) | User reports and feedback: sent by the user, untrusted input | `src/ThoughtTree.Client*/**`, `.github/**` |
| [ADR-052](adr/ADR-052-untrusted-content.md) | Content is untrusted input | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Domain/**` |
| [ADR-053](adr/ADR-053-functional-core-hexagonal-shell.md) | Functional core, hexagonal shell | `src/**` |
| [ADR-054](adr/ADR-054-code-organisation.md) | Code organisation: boundaries, feature folders, test intents | `src/**`, `tests/**` |
| [ADR-055](adr/ADR-055-private-feedback-inbox.md) | Private feedback inbox | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Server*/**` |
| [ADR-056](adr/ADR-056-server-store-migrations.md) | Server store migrations | `src/ThoughtTree.Server*/**`, `infra/**` |
| [ADR-057](adr/ADR-057-sync-modes.md) | Sync modes, live preselected | `src/ThoughtTree.Client*/**` |
| [ADR-058](adr/ADR-058-tags.md) | Tags as tree elements, typed with `@@` | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-059](adr/ADR-059-undo.md) | Undo writes new events; quick undo covers the user's own actions on this device | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-060](adr/ADR-060-shared-themes.md) | Themes of trees and branches are shared through events | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-061](adr/ADR-061-shipped-fonts.md) | System font first, shipped fonts under the SIL Open Font License | `src/ThoughtTree.Client*/**`, `**/*.csproj`, `**/package.json` |
| [ADR-062](adr/ADR-062-views-on-items.md) | Views belong to items | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-063](adr/ADR-063-terms.md) | Terms: item, note, branch, node | `docs/**`, `src/**`, `tests/**` |
| [ADR-064](adr/ADR-064-ui-follows-ux-concept.md) | UI work follows the UX concept | `src/ThoughtTree.Client*/**`, `tests/specify/**` |
| [ADR-065](adr/ADR-065-focus.md) | "Focus" instead of "Zoom" | `docs/**`, `src/ThoughtTree.Client*/**`, `tests/**` |
| [ADR-066](adr/ADR-066-inbox.md) | The inbox is a marked item, always last | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-067](adr/ADR-067-one-action-one-undo-step.md) | One action, one undo step | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Contracts/**` |
| [ADR-068](adr/ADR-068-templates.md) | Templates are marked items, valid below their parent | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-069](adr/ADR-069-copy-paste-between-trees.md) | Copy and paste between trees | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-070](adr/ADR-070-feedback-where-annoyed.md) | Feedback offered where the user is, with the place it came from | `src/ThoughtTree.Client*/**` |
| [ADR-071](adr/ADR-071-no-ai-on-content.md) | No AI on the user's thoughts | `src/**` |
| [ADR-072](adr/ADR-072-headings-are-thoughts.md) | A heading is a thought; Focus shows a thought as a page | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-073](adr/ADR-073-encryption-hardening.md) | Encryption hardening before release 1 | `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**` |
| [ADR-074](adr/ADR-074-release-1-scope.md) | Release 1 scope: standalone, file exchange, final formats | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**` |
| [ADR-075](adr/ADR-075-xunit-v2-lambdatale.md) | xUnit v2 and LambdaTale 2.9.x for all test projects | `tests/**`, `Directory.Packages.props`, `**/*.csproj` |
| [ADR-076](adr/ADR-076-no-code-plugins.md) | No code plugins; extend by data, readable files or a fork (Proposed) | `src/**` |
| [ADR-077](adr/ADR-077-relation-notes.md) | A note on a relation (Proposed; amends ADR-018) | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**` |
| [ADR-078](adr/ADR-078-update-import.md) | Re-importing a readable export updates the tree it came from (Proposed; amends ADR-019) | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**` |
| [ADR-079](adr/ADR-079-keep-headings-tree-feature.md) | Headings become thoughts is a tree feature (Proposed; amends ADR-072, ADR-029) | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**` |
| [ADR-080](adr/ADR-080-branch-export.md) | Readable export and import of a branch, where the user is (Proposed; amends ADR-019) | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Contracts/**` |
| [ADR-081](adr/ADR-081-one-name-no-translation.md) | One name per structural element, no translation inside the tree (Proposed; amends ADR-026) | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**` |
| [ADR-082](adr/ADR-082-tags-are-items.md) | Tags are items in the tree's Tags branch (Proposed; supersedes ADR-058; amends ADR-019, ADR-066, ADR-069) | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**` |
| [ADR-083](adr/ADR-083-relation-ids.md) | Relations have an id (Proposed; amends ADR-018, ADR-024, ADR-077) | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**` |
| [ADR-084](adr/ADR-084-relation-types-are-items.md) | Relation types are items; a new tree has only `related` (Proposed; supersedes ADR-025, ADR-026, ADR-081; amends ADR-018, ADR-019, ADR-066, ADR-069) | `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**` |
| [ADR-085](adr/ADR-085-beta-data-before-release-1.md) | Beta data before release 1: formats free until v1, bridged by the Obsidian export (Proposed; amends ADR-016, ADR-038) | `src/ThoughtTree.Client*/**`, `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**` |

**Open decisions**

Ordered by when they must be settled (release 1 scope, decision 4).

| Topic | Options | Settle |
| --- | --- | --- |
| Independent reviewer agent | **Decided by the owner, 2026-11-16:** agents may change `AGENTS.md`, `CLAUDE.md` and the rest of `.github/**` on the approval of an independent reviewer agent (a required check that runs from the workflow on `main`, so a pull request cannot change its own judge); ADRs and the owner-only list stay with the owner; a weekly digest issue lists agent-approved rule changes; until the reviewer exists the owner reviews these files. Rules to hold: the reviewer reads the diff as data only, never checks out or runs pull request code with the credential, and the credential is never available to pull requests from forks (the repository is public; `pull_request_target` is dangerous if misused). Open: the AI credential the reviewer needs as a CI secret (ADR-046 forbids deployment secrets only; its wording should say so), model and prompt, and what the digest contains | Before project setup |
| Agent identity for pull requests | **Decided by the owner, 2026-11-17 (option E):** agents open pull requests as `AdventureDevelopment` (the repository owner account, admin) with a fine-grained token limited to this repository (Contents and Pull requests write, no Administration permission); the owner reviews and approves as the separate account `ProductOwnerRole` (the code owner in `.github/CODEOWNERS`, write access), so the code-owner review of the owner-only paths is independent (a pull request author cannot approve their own pull request). Branch protection also applies to administrators (see the settings checklist). Until now pull requests 1 to 3 were opened and reviewed under `AdventureDevelopment`, so those reviews were not independent. Changeable later without touching a rule or a stored format; a GitHub App with installation tokens (bot-authored pull requests) stays a possible upgrade. Not verified in the cloud session: which token and scopes the session holds today (owner checks by hand). Still open, in other rows: a required CI check that fails on protected-path changes without approval (belt and braces); the independent reviewer agent (row above; its approval is a status check, so it needs no identity) | Decided; owner sets up the token and the review account by hand |
| GitHub settings for `main` | **Decided by the owner, 2026-11-17 (option A, full set); the owner sets them by hand on GitHub (agents cannot):** branch protection or ruleset on `main` with: require a pull request; require review from Code Owners; require approval of the most recent push; required checks `Everything in the branch check / Build, analyzers, architecture rules and tests` and `Protected harness data (ADR-016, ADR-035)` (names from `.github/workflows/push-gate.yml` and `checks.yml`; the job `Push gate parts that need later stages` is a placeholder and not required), branch up to date before merging; no bypass for administrators; block force pushes and deletion. Not verified: whether any of this is set today (an agent cannot read the settings). Also decided 2026-11-17: `eng/approved-suppressions.txt` gets an owner entry in `.github/CODEOWNERS` (ADR-042 allows suppressions only in files on that list; done in this change) | Decided; owner sets up by hand |
| Azure Static Web Apps deployment workflow | **Decided by the owner, 2026-11-17 (option A):** the workflow file Azure generated (`azure-static-web-apps-victorious-pond-00fef8010.yml`, added to `main` by the portal) is removed. It conflicted with ADR-046: stored deployment token instead of OpenID Connect, no manual approval before production, no `permissions` block, actions pinned by tag only; it ran on `pull_request` so a pull request could read the secret; its paths `Client` and `Api` do not exist; no `infra/` Bicep exists yet. Replacement comes with the Azure side: Bicep templates in `infra/`, OIDC federation, commit-pinned actions, minimal permissions, manual approval for production, preview per pull request (D-50, D-51; foundation step 1). Owner by hand: revoke or delete the stored `AZURE_STATIC_WEB_APPS_API_TOKEN_…` secret and the generated deployment token in the portal if still active; not verified from here (Azure and repository secrets are not readable by an agent) | Before release 1 (deployment); replacement workflow when the Azure side is ready |
| Template link slots | Whether template lines can be link slots that create typed relations (`item.slot-set`), or stay text with inline `item:` links, or carry a relation type for links typed into them (UX decision D-111 left it open; the slot rule was removed from ADR-068 on 2026-11-16; templates may slip to 1.x, release 1 scope decision 3) | With the templates feature |
| Local storage engine | **Decided by the owner, 2026-11-15: IndexedDB for release 1**, behind the engine-neutral storage interface with contract tests every engine must pass. Reasons: mature on Safari; no extra download or worker; the tree is projected in memory from an append-only log; with an encrypted search index (ADR-073) SQLite's full-text index brings no advantage. SQLite compiled to WebAssembly on the Origin Private File System stays a revisit option; review when: reading 100,000 events at start misses the measured target on a mid-range phone, or IndexedDB loses data in the durability tests (app closed while writing), or the size or speed budget cannot be kept. Open: Safari (decided, see the Safari row) | Decided; measurements in the spike |
| Blazor AOT compilation and spike | Interpreted (smaller download) or AOT-compiled (much faster for heavy computation such as replaying 100,000 events, larger download). **Spike agreed by the owner, 2026-11-15** (one task, answers this and sets the budgets): (1) start and replay time of the 100,000-event reference dataset with IndexedDB on a mid-range Android phone and on desktop, including batched Web Crypto decryption and snapshot loading; (2) the same for AOT and interpreted builds, with download size of each, which sets the size and load budgets; (3) durability in Chromium and Firefox: no data loss when the app is closed or killed while writing, repeated kills; (4) search: encrypted index against building the index in memory at start (also numbers for the optional unencrypted index); (5) a proposal for the stored size limit numbers; (6) output: a short report with a recommendation and measurements stored as reproducible data. Done when all are answered and the owner decides AOT from the report. Safari is not part of the spike (owner decision: no cloud accounts). Plan: [spike-1-storage-and-aot.md](spikes/spike-1-storage-and-aot.md) | Before release 1 (measured in the spike; can be changed later) |
| Safari (iPhone and iPad) support | Release 1 scope and the quality bar assume iPhone and iPad (D-50, D-51, offline start from the home screen), but no iOS device or cloud account is available for testing. Options discussed: borrow a device; a one-month cloud real-device plan (about $25 to $40); call release 1 a beta without Safari support and make Safari a gate for 1.0 (formats stay final, an engine change after the beta would need a migration, ADR-038). **Decided by the owner, 2026-11-16: borrow an iPad and an iPhone** for the release check and the first iPad trial; the cloud plan is not taken now (it stays a fallback if no device can be borrowed; free device-cloud programmes for open-source projects can be checked now that the repository is public); the storage spike stays Chromium and Firefox (spike plan) and Safari is tested on the borrowed devices as soon as they are available. Added 2026-11-18 (Zettelkasten session): the owner and a person with an iPad, who wants to keep a Zettelkasten in ThoughtTree, test every merged pull request in the test environment; this covers the Obsidian round trip on iPad (PF7, guarantee G6 of [zettelkasten-claim.md](zettelkasten-claim.md)) | Before release 1 |
| Size limits | Stored limits (before release 1, they become part of stored formats): padding bucket list and behaviour above the largest bucket; maximum title and note length; maximum user name and server address length; limits of a readable import (unpacked size, number of files, added 2026-11-17 with the Obsidian import); size budget of the first load; budget for fonts downloaded in the background (ADR-061). Server limits (before the first server release): maximum request size; maximum events and storage per account | Before release 1 (stored limits); before the first server release (server limits). Owner decision 2026-11-15: rules fixed now (buckets; above the largest bucket the payload is padded to a multiple of it; hard maximums for title and note enforced when an event is written; name and address maximums so the largest pairing code fits one QR code, ADR-028); the numbers are proposed to the owner after the storage and AOT spike and recorded as an ADR; load and font budgets follow the spike results. Owner decision 2026-11-17: no number is fixed before the spike, including the maximum title and note length (a proposal of 1,000 and 100,000 characters was discussed and not taken; maximums checked only on writing can be raised later without breaking stored data, lowering them cannot) |
| Serialization format | JSON (ADR-017) is confirmed as the format of envelope version 1 (owner decision 2026-11-15); a compact binary format such as MessagePack can come later as a new envelope version if logs grow too large (signal: size of the 100,000-event reference dataset) | Decided |
| Server event store | Azure Table Storage, Blob Storage or SQLite (parked by the owner 2026-11-16 until the server is planned; review finding 2.11: SQLite is listed here but excluded on Azure by section 7 and ADR-047, which keeps it for self-hosting; idea to consider: PostgreSQL as a second container of the Container App, one store for Azure and self-hosting, but it needs a persistent volume (Azure Files, the same reliability question as SQLite), runs with one replica only, and the monthly cost against the near-zero goal is unchecked; it would need a spike). To consider: Table Storage records a time on every row that cannot be switched off, and Blob Storage records creation and change times, which conflicts with storing no times of use unless events are packed into larger chunks; SQLite stores no such times; the self-hosted package needs a store that runs without Azure; later binary data needs blob storage | Before the first server release |
| End-to-end smoke set after the first server release | Which end-to-end scenarios the push gate runs once the server exists. Today: release 1 runs 2, 6, 7 and 8; the loop table says 1, 2, 6 and 8 with scenario 9 nightly; release 1 scope decision 5 says 1 and 9 switch on with the server and 7 stays; the container test is in the push gate from the first server release. Proposed: 1, 2, 6, 7, 8 and 9, revisited if the gate exceeds its time budget. Parked by the owner 2026-11-16 | Before the first server release |
| Gate suite in an agent's sandbox | Harness principle 5 (section 8) says the whole gate suite runs in an agent's sandbox, but the container test needs Docker and ADR-046's verify needs the Bicep CLI. Proposed: principle 5 says "needs no Azure account, secrets or network" and names Docker and the Bicep CLI as tools CI provides. Parked by the owner 2026-11-16 until the server is planned | Before the first server release |
| Sync API contract | Endpoints, request and response shapes, paging and limits of pulls, error and status codes, authentication per endpoint, the "server needs updating" signal, the hub messages, account deletion; defined in Contracts before the first release with a server, because `v1` is frozen from then on (ADR-037) | Before the first server release |
| Replicas and live sync | Whether the server runs with one replica (in-memory rate limits and counters stay valid, no SignalR backplane) or several (needs a backplane and shared counters); whether live mode keeps a WebSocket open or uses notification on demand, because open connections prevent scaling to zero | Before the first server release |
| Account deletion and retention | Deleting an account's log on the server with the account secret (after deleting a tree, after importing a tree into another, after moving to another server, on request); how long a server keeps data of accounts that are no longer used; data-protection duties of an operator | Before the first server release |
| Optional unencrypted search index | **Choosing it needs an ADR-073 change** (ADR-073 says MUST encrypt the search index; owner decision 2026-11-16: keep ADR-073 as is until the spike has measured the cost). Whether a user may knowingly switch the local search index to unencrypted storage on one device, for faster search (default stays encrypted, ADR-073); a device setting, not an event (ADR-013); the privacy text would have to say so | After the storage spike measured the cost of an encrypted index |
| Key backup | How a tree survives the loss of all devices; direction decided for release 1 scope: a random recovery code rather than a passphrase (Web Crypto offers only PBKDF2, weak against guessing of weak passphrases); a recovery-code-wrapped key lives in a separate backup file, not in the exchange file header (ADR-073, owner decision 2026-11-16); the key stays random and is only wrapped for backup, so this does not affect the rest of the architecture | Later |
| Log growth | Whether and how old events can ever be compacted without breaking merges with devices that were offline | Later |
| Binary data (pictures, files) | Rule decided: separate encrypted blobs referenced from events (ADR-022). Open: random blob ids (not content hashes), padding or fixed-size chunks to hide file size, server blob storage next to the event log (affects the choice of store and the free tiers), fetching blobs only when needed, including files in exports, when blobs of deleted items may be removed | Later |
| Manual screen reader testing | Whether VoiceOver, TalkBack and NVDA are tested by hand before a release, in addition to the automated checks of names, roles and announcements (owner decision 2026-11-16: later, not for release 1; today the screen reader experience relies on user feedback, section 8) | Later |
| Obsidian import and export (Zettelkasten claim) | Potential users want to keep an Obsidian-style Zettelkasten in ThoughtTree with a full import and export cycle. **Decided by the owner, 2026-11-17:** the claim and the feature mapping in [zettelkasten-claim.md](zettelkasten-claim.md) (notes, structure, links, tags and typed links round-trip; attachments, embeds, block links and plugin views do not, yet). Decided 2026-11-17 and 2026-11-18: links versus relations, the note on a relation (ADR-077), import (ADR-078, ADR-079, ADR-080), export and the round-trip guarantee, the acceptance scenarios, and release 1 scope (all of it in release 1, core). Still open here: the follow-up rows "Tags and relation types as items", "Predefined relation types in new trees", "Relation note (UX)", "Headings in notes (UX)" and "Readable import and export flow (UX)". Anything that touches stored events or the readable formats must be settled before release 1 (ADR-073, ADR-074) | Before release 1 |
| Predefined relation types in new trees | The fixed ids of the predefined types become stored data in release 1, so the set and its ids must be final before then; display names can change at any time. Decided 2026-11-17: `related` stays the default type (D-13), "mentions" is only the interface name for link connections. **Decided by the owner, 2026-11-19 (session "Tags and relation types as items", decision 4, option b):** a new tree gets only the predefined type `related`, the default of D-13 (owner 2026-11-19: without a fixed id, marked as the default by `tree.default-relation-type-set`, see the row "The `related` item in merged trees"); the other types move to the tutorial, which offers them to copy into other trees, like the templates (ADR-068, ADR-069); untyped relations were rejected (an empty type would be a built-in type in disguise, ADR-025). The tutorial's types are ordinary types without fixed ids (option a): copying them gives new ids, matched by name like tags (ADR-069); the tutorial offers the types common in Zettelkasten practice, the list fixed with the tutorial content (lesson 5, D-143); duplicate types after an import are accepted, the user can retype their relations and remove one; the Obsidian export writes every type by its name, `related` included (owner 2026-11-19: what the user sees is what is written; `zitiert` and `cites` stay different properties; on re-import the name matches the type item, and `related` is found by its default marking). Proposed as ADR-084, which retires the ADR-025 bet card on acceptance. Still open: how the interface names backlinks from text ("Linked from" in section 8, "Mentioned in" in D-111), for the UX session | Decided 2026-11-19 (ADR-084 Proposed); backlink name with the UX session |
| Beta users' data before release 1 | Owner, 2026-11-19: interested beta users use the test environment through continuous delivery; their data should not be lost on the way to v1 if possible, but it is not guaranteed. **Decided by the owner, 2026-11-19 (option a):** stored formats stay free until v1; before an update that changes the stored-format version, the app offers an export of every tree in the Obsidian form, which carries everything but history, conflicts and devices ("everything is a zettel", zettelkasten-claim.md), and after the update offers to import it as new trees; old data stays untouched (ADR-085, Proposed). **Needs sketch, owner to brief the UX session.** Question: how does the app tell a tester that the next update changes the stored format, offer the Obsidian export of every tree before the update, and offer the import as new trees after it, including old trees shown as not readable? | Decided (ADR-085 Proposed); sketch before the first beta format change |
| The `related` item in merged trees | Found by the independent review, 2026-11-19: with ADR-084 (Proposed) `related` is an item with the same fixed id in every tree, but each tree creates it under its own Relation types branch and with a title in its own creating language; importing one tree into another (union of logs) then holds two creations of one item id with different parents and possibly different titles, which "same value, no conflict" (section 8) does not cover. **Decided by the owner, 2026-11-19 (option b):** `related` has no fixed id; it is an ordinary type item with a new id per tree, marked as the tree's default relation type by `tree.default-relation-type-set` (`itemId`), written when the tree is created; the latest marking in the agreed order wins, so after importing a tree the importing device writes it for the target's default and the imported `related` stays as a duplicate type (accepted, decision 4); the user can make another type the default; no item has a fixed id | Decided (ADR-084 Proposed) |
| Tags and relation types branches (UX) | Tags and relation types are items in marked branches (ADR-082, ADR-084, Proposed, owner 2026-11-19). **Needs sketch, owner to brief the UX session.** Question: where are the branches "Tags" and "Relation types" shown (in the outline next to the inbox, and in which order; in the navigation column of D-150 as the tag list), how does a tag or type item look different from an ordinary thought, how does the user set a type's reverse name and symmetry, how are joining tags and copying types from the tutorial offered, and how do D-14 (chips, `@@`) and D-64 (tags on the page, rename everywhere, now a title change) read with tags as items? In release 1 as core | Before release 1 |
| Relation note (UX) | The note on a relation is decided (ADR-077, Proposed, owner 2026-11-17); its interface is not. Interface decided by the owner, 2026-11-19, in UX decisions D-175 and D-176 ([ux-concept.md](ux-concept.md), sketch `docs/ux/sketches/relation-note.html`): the reason shows under the relation wherever relations are listed (Links and Linked-from sheet, the map's card and side column), "＋ Add a reason" on its own small line when there is none; Link to… links at once and never asks; a relation sheet holds from, type chip (`relation.type-changed`, ADR-083), to, the reason and remove; on the map a line can be selected; screen readers hear the reason after the relation. In release 1 as core (Zettelkasten decision 7). Nothing open in the interface | Decided (UX) |
| Tags and relation types as items | Owner's idea, 2026-11-17, widened 2026-11-18 (Zettelkasten session): relation types are included (a type as an item with a note saying what it means; reverse name and symmetry to be placed); relations themselves as items are rejected (owner 2026-11-18: no place in the one-parent tree, duplicates offline, ADR-024 and ADR-077 cover the need); names are untranslated (ADR-081, Proposed). **Decided by the owner, 2026-11-19 (session "Tags and relation types as items"):** (1) two models were weighed, an element with a description and a marked item; an item tagged by a relation `tagged` and an element pointing to a thought were dropped; (2) tags are items: a tag is an item inside the tree's marked branch "Tags" (`tree.tags-set`, written when the tree is created, like the inbox); being a tag follows from the place, with no mark event; its title is its name, its note its description, its child items its nested tags; folder names can become nested tags on import; the branch is the tag list for the navigation column (D-150); an item moved out of the branch stops being a tag and its assignments are hidden until it comes back (as relations of deleted items, ADR-024); `tag.defined` and display names for tags are dropped, `item.tagged` / `item.untagged` name the tag item, `tag.joined` stays for duplicates made offline. Drivers (owner): folders as tags, navigable tags, a tag with a description, nested tags. (3a) relations get their own id (owner 2026-11-19, option c): the type of a relation can be reassigned while the relation keeps its note and its history; concurrent changes of the type are a visible conflict; relations with equal three parts (from, type, to) are shown as one, and if more than one carries a note the notes are a visible conflict; amends ADR-024 (identity by three parts) and ADR-077 (note keyed by the relation's id); relations are still not items (no place in the tree); chosen now because an id cannot be added cleanly after release 1. (3) relation types are items too (owner 2026-11-19, option b): a type is an item inside the tree's marked branch "Relation types" (`tree.relation-types-set`, written when the tree is created); being a type follows from the place, as for tags; its title is its name, its note says what it means; `symmetric` and the reverse name are kept per type by an event with `replaces`; `related` is an item in every new tree, marked as the default type (no fixed id, see the row "The `related` item in merged trees"); types are taken from the tutorial by ordinary copy and paste; moving a type out of the branch or deleting it hides its relations until it comes back. Owner's direction for the UX session: the branch may be hidden in the outline and reached from the global menu (More › Relation types), needs a sketch. (5) the stored format (owner 2026-11-19, option a): new `tree.tags-set`, `tree.relation-types-set`, `relation-type.set` (`itemId`, `symmetric`, `reverseName`, `replaces`), `relation.type-changed`, `relation.restored`; `relation.added` gains `relationId`; `relation.removed` and `relation.note-changed` are keyed by `relationId`; `item.tagged`, `item.untagged` and `tag.joined` name tag items; `tag.defined`, `relation-type.defined`, `relation-type.removed` and `display-name.set` / `display-name.removed` are dropped, since no named structural element is left (supersedes ADR-026 and ADR-081); no migration, since no feature code exists yet; before v1 formats stay free to change (ADR-085, Proposed). Details of (5), owner 2026-11-19, all option a: titles written at tree creation ("Tags", "Relation types", "related", the inbox) use the app language chosen on the creating device and are content afterwards; joining duplicate tags moves the joined tag's children under the target, appends its note below a separator line and deletes it, as one action; a parent tag includes its nested tags when filtering, searching and counting; switching tags or relations off hides chips, `@@`, the picker, relations on the map and the matching branch, and keeps everything (the earlier direction to hide the Relation types branch behind More is dropped). Proposed as ADR-082 (tags), ADR-083 (relation ids), ADR-084 (relation types, only `related`, no display names). Import of the marked branches (owner 2026-11-19, option c): the tag and type items of an imported tree (JSON import, importing a tree into another, Obsidian import) move into the target's Tags and Relation types branches in the same action as the imported content, without matching; equal tag titles then get the usual offer to join (ADR-082), equal type titles are accepted as duplicates, `related` included; the target's default marking wins. The two marked branches cannot be moved or deleted, like the inbox (owner 2026-11-19, option a). Release 1 scope (owner 2026-11-19): unchanged in content; display names, `tag.defined` and five predefined types leave, the marked branches, identity by place, relation ids and joining come in; riskiest part: identity by place with nested-tag filtering within PF1 and PF2; may slip to 1.x as UI only: the join offer, editing a type's reverse name and symmetry, copying types from the tutorial. Background as first recorded: a tag could be an item instead of a structural element (ADR-058), so it can have a note, relations, a place on the map and nested tags as items inside items. Variants: (1) an item marked as a tag, like a template; (2) any item can be a tag, assigned by a relation of a predefined type such as `tagged`, with no tag events. Problems to solve: duplicates made offline (today `tag.joined`; items cannot be merged), where tags live in the tree (a marked "Tags" branch like the inbox?), hubs on the connection map, the tree feature switch. Would supersede ADR-058 and change ADR-025, ADR-026 and the UX decisions on tags (D-14, D-15, D-64). Parked with it: a Tags section in the navigation column on wide screens (D-150; tags open question 3) and keeping Obsidian folders as tags on import. To be settled in a session of its own | Decided 2026-11-19 (ADR-082, ADR-083, ADR-084 Proposed) |
| Headings in notes (UX) | ADR-079 (Proposed, owner 2026-11-17) makes "Headings become thoughts" a tree feature. **Needs sketch, owner to brief the UX session.** Question: with the feature off, how does Focus (D-155) tell a heading inside a note from a thought shown as a heading, where is the feature switched (tree settings, the import question), and what does the Markdown export of one document do with headings in notes (decision 5 of [zettelkasten-claim.md](zettelkasten-claim.md))? | Before release 1 (readable formats) |
| Readable import and export flow (UX) | Decided for the Obsidian import (owner 2026-11-17, [zettelkasten-claim.md](zettelkasten-claim.md)): a zip of the vault or branch, a single `.md` file for a single thought, a folder where the browser allows; the proposed hierarchy (folders, property or Luhmann ids); the report stored as a thought "Import report". **Needs sketch, owner to brief the UX session.** Question: how do choosing the files, the proposed hierarchy, the headings question, progress and cancel, the report, and the ⋯ menu entries "Export…" and "Import into this thought…" with the ADR-019 warning look on phone and tablet, and how short may the warning be for a quick branch export? | Before release 1 |
| Obsidian vault bridge | **Decided by the owner, 2026-11-17:** ThoughtTree is not a live interface on an Obsidian vault: a PWA cannot open a folder on iPad (Safari and Firefox do not offer `showDirectoryPicker`), the vault would replace the event log as the truth (ADR-002: no history, undo or visible conflicts), its files are plaintext at rest (ADR-073), and Obsidian, its plugins and file sync would write the same files outside the conflict model. Later idea: a linked vault that re-imports and re-exports on request, with ThoughtTree's encrypted log staying the truth and file changes becoming ordinary events (conflicts visible as usual); it builds on the identity rules of the Obsidian import and export (see [zettelkasten-claim.md](zettelkasten-claim.md)) and needs folder access, so the store app or desktop Chromium. Not in release 1 | Later |
| Zotero connection | Direction (owner, 2026-11-16): first a file import (CSL JSON or BibTeX) with items linked by a relation type `cites` (from the tutorial, ADR-084, Proposed); a live fetch only if users ask; a live connection would need an ADR-030 change (no third-party requests from the client). Not in release 1 | Later |
| All-trees search (UX) | All-trees search is in release 1 (owner, 2026-11-16). Open: the sketch (ADR-064) and the UX decision D-n that extends D-97 (where the scope is chosen, how hits show their tree, how a hit opens another tree); whether searching several trees needs every tree's key unwrapped at the same time (each tree has its own index and key, ADR-073) and the privacy text; the budget for searching several trees on a phone . **Parked by the owner, 2026-11-17 (not needed for the setup); needs sketch, owner to brief the UX session.** Questions for the sketch and the UX decision D-n: (1) where the user chooses the scope (this tree, this branch, all trees); (2) how a hit shows its tree; (3) what opening a hit in another tree does and how the user goes back; (4) what shows while other trees' indexes load or when search is slow on a phone; (5) the privacy sentence. Engineering facts checked 2026-11-17 (not an owner decision): keys are used only as non-extractable handles (ADR-073), so several trees at once expose no raw key; each tree has its own encrypted index (ADR-010, ADR-073); whether the spike measures search across several trees is not confirmed, the phone budget needs numbers | Before release 1 |
| Connection map (follow-ups) | The map is in the first trial version (owner, 2026-11-16). Decided: core, not behind a release flag; the alternative for screen readers and keyboard is both the links and backlinks strip (D-63) for the direct connections of an item and a keyboard-operable map (arrow keys move between connected items, with announcements; owner decision 2026-11-16). Open: the performance budget: decided in direction, the map shows only the neighbourhood of the selected item (proposed: 2 steps and at most 200 items drawn, to be confirmed by the first measurement on the iPad), independent of tree size; a later feature (owner idea, 2026-11-16): magnifying and shrinking the map with loading of more items, perhaps with a warning if too many would load; a sketch first (ADR-064) and a UX decision D-n: decided, the order is an HTML sketch in `docs/ux/sketches/` shown to the owner (neighbourhood, highlight, tap to move, keyboard path, the strip), then the UX decision is appended; this is the first step of the map's slice, before any code . **Parked by the owner, 2026-11-17 (not needed for the setup; the map comes after relations, release 1 scope decision 7); needs sketch, owner to brief the UX session.** The decisions above stand; the sketch must show the neighbourhood, highlight, tap to move, the keyboard path and the strip, and the question to settle with it is whether 2 steps and 200 items is the right budget (to be measured on the iPad); added 2026-11-17 (Zettelkasten decision 2): the map also draws inline links as untyped connections, so the sketch must answer "How does the map show untyped inline links next to typed relations, and can the user hide either kind?", and the budget counts both. **Sketch done, decided by the owner 2026-11-19 in UX decisions D-168 to D-174** ([ux-concept.md](ux-concept.md), sketch `docs/ux/sketches/connection-map.html`): the map is a place of its own (⋯ › Map); on wide screens it takes the middle column with the selected thought shown at the side as in Focus (D-168), on the phone a card (D-169); no visible list, screen readers get the map as a structured list (D-170); visual zoom by pinch, drag and ＋ −, thoughts drawn as dots where names do not fit, the middle and the selected thought keep their names (D-171); keyboard path, provisional until UI testing (D-172); rings that never move on their own, no tree lines, the budget cut named in the header, no lines to deleted thoughts (D-173); ＋ Add here disabled on the map (D-174); both kinds of line can be hidden by switches, lines can be selected (D-175). Still open: the budget number (2 steps and 200 thoughts, to be measured on the iPad) and the later zoom with loading of more thoughts | Before release 1 (budget measurement) |
| Import and `device.joined` | Importing a tree adds its events, including its `device.joined` events (ADR-010); ADR-011 derives private or shared from `device.joined`, so importing a shared tree could turn the target tree shared. Options: drop the imported `device.joined` events, or write fresh ones after the import. Needs an ADR-010 or ADR-011 change **Parked by the owner, 2026-11-17:** direction is to drop the imported `device.joined` events (no format field needed, so it does not have to be decided before release 1; the option that keeps them needs a marker on every imported event and was not favoured). Decide and amend ADR-010 when the import is built; do not build the import before that (importing a tree is a feature that may slip to 1.x, release 1 scope decision 3) | With the import feature (before it ships) |
| Tutorial privacy sentence about the feedback inbox | D-147 (4) says the private feedback inbox is encrypted to the operator, but the inbox is out of release 1 (ADR-074) and scope decision 6 requires every privacy sentence to be true of the shipped code. Proposed: hide that sentence behind the same release flag as the inbox . **Parked by the owner, 2026-11-17 (not needed for the setup).** Proposal stands, not decided: hide the sentence behind the inbox's release flag; settle when the tutorial is written | Before release 1 |
| "Sage" theme of the tutorial | D-144 lesson 7 uses a "Sage" theme; ADR-060 ships at least Paper and Inbox. Options: ship Sage as a third theme, or change the lesson to use shipped themes . **Parked by the owner, 2026-11-17 (not needed for the setup).** Question for when the tutorial or the themes are built: ship Sage as a third theme, or change lesson 7 to the shipped themes | Before release 1 |
| Tutorial without slipped features | D-143 lesson 6 (templates) and D-113 (copy and paste) may slip to 1.x, and ADR-074 hides unfinished features in the tutorial too; the lesson plan has no fallback without them . **Parked by the owner, 2026-11-17 (not needed for the setup).** Question for when the tutorial is written: what the lesson plan shows if templates (lesson 6) and copy and paste (D-113) slip to 1.x | Before release 1 |
| Which copies reset the export reminder | D-126 counts "last copied to another device" by exchange or export; the encrypted export file only helps while a device with the key exists (section 8). Decide which copies reset the reminder. Input 2026-11-18: readable exports are now recorded as `item.exported` and `tree.exported` events (ADR-078, Proposed) | With the export feature |
| Left and right | ADR-033 bans `left` and `right` for layout direction, while D-52 has a physical "Hand: right / left" setting. Proposed: logical properties for reading direction and a separate per-hand rule | With the UI component base |
| Right-to-left interface | Section 8 says no right-to-left interface for now, while D-133 specifies a mirrored one; consistent only if D-133 means later. Proposed: record D-133 as later . **Parked by the owner, 2026-11-17 (not needed for the setup).** Proposal stands, not decided: record D-133 as later | Before release 1 (wording) |
| Marker events and older apps | ADR-016 says structure is expressed only through the basic events, but inbox and template markings change what the structure looks like; an older app would show a different structure. Options: accept it (older apps ignore the markings), or version gate **Parked by the owner, 2026-11-17, not decided.** Recommendation of the session, not an owner decision: accept (older apps ignore the markings with a warning; no format field), with a one-line clarification of ADR-016 (structure means which items exist, their order, titles and notes) and a spec that new apps cope with a deleted or moved inbox. Why it still belongs before release 1: a version gate would need a field that release 1 apps already know, so it cannot be added later for apps already out | Before release 1 (stored formats) |
| Sketches named but not stored | D-82 (inbox theme), D-88 (starting a drag, sort mode) and D-89 (landing place, variant A) name sketches that are not in `docs/ux/sketches/`, although D-47 requires stored sketches. Options: recreate them, or note the decisions as made without a stored sketch | With the UX concept update |
| Token names across themes | D-82 uses names such as "list", "tinted rows" and "on accent"; the Paper token table uses `surface-content`, `accent-text` and `on-accent` and has no tint token, which D-35 and D-92 need. Align the names and add a tint token before the design tokens are built (ADR-064) | Before the design tokens (foundation step 4) |
| Key rotation | `KeyId` leaves room for changing a tree's key (for example after a leak); whether and how rotation is offered | Later |
| Membership | Removing a device or user from a tree (`device.left`), lifetime and revocation of device tokens by the account secret, what a member list shows after a tree was deleted on a device; and after importing a tree into another: whether the imported tree's devices and users become members of the target tree, and what happens to the imported tree's account and devices | Later |

Closed: touch gestures for indent, outdent and move are decided in the UX concept (D-54, D-72, D-75, D-76, D-89). Release 1 scope is decided (below).

**Release 1 scope** (decided by the owner, 2026-11-15; rules for agents in [ADR-073](adr/ADR-073-encryption-hardening.md) and [ADR-074](adr/ADR-074-release-1-scope.md), both Accepted)

Release 1 is the first public release. It gets the foundation right: everything it stores is final, so server sync, sharing, replicas and binary data can follow later without migrating data. Given by the owner: phone, tablet and desktop; no sync server, every device standalone, sync only by encrypted file with the guided round trip (D-19, D-20); themes per tree and per item; the tutorial tree as the first tree; static hosting on Azure Static Web Apps with a preview per pull request (D-50, D-51).

| # | Decision | Result |
| --- | --- | --- |
| 1 | Stored formats | Final from release 1: release 1 writes the server-ready envelope (encrypted payload wrapper, plaintext `EventId`, envelope version, `KeyId` and nonce, padding; the account id is authenticated data and not stored in the envelope) in the local store and the exchange file; later releases only add event types and fields (ADR-016); format data starts with `tests/data/formats/1.0`. Encryption hardened before release 1 (ADR-073): every plaintext header field (envelope version, `KeyId`, `EventId`) plus the account id and a fixed context label bound as additional authenticated data, `ServerSequence` excluded; the encrypted payload is a wrapper with room for optional parts (later device signatures without a new envelope version); daily use only through a non-extractable key handle, the raw key stored only wrapped by a non-extractable device key and unwrapped for pairing, sharing or backup (door for a later app lock); production keys and nonces only from the browser's cryptographic random source, checked by a test; snapshots, search index and drafts encrypted at rest with the tree key; the exchange file header holds only format version and `KeyId`, bound to the contents (the account id sits inside the encrypted part). Implementation notes: encryption runs through Web Crypto (AES-GCM is not available in .NET in the browser), calls are batched for replay; tests and simulation use .NET `AesGcm` against the same fixed test data. Considered and not taken: per-user keys or MLS (forward secrecy is meaningless for a readable history; revocation later by key rotation), XChaCha20-Poly1305 (not in Web Crypto), per-event derived keys (double cost; nonce safety secured by the random source test instead) |
| 2 | Pairing and sharing | Pairing own devices is in (the second device needs the key to read the exchange file). Sharing with other users is out; its formats and Domain rules are final and tested: `device.joined` with user identity, `user.name-changed`, `device.label-changed`, the code format with its own version and a kind (pairing or sharing), the derivation of private or shared |
| 3 | Features | **In (core):** outline (items, nesting, collapse, move, Focus, inbox ADR-066, touch gestures and keyboard); several trees; device labels and who changed what; pairing, file round trip, visible conflicts (ADR-018, D-127); undo with `actionId` on every event and item history (ADR-059, ADR-067); search on the device, across the open tree, the focused branch or all trees on the device (owner decision 2026-11-16, extends D-97; needs a sketch and a UX decision first, see the open decisions); tags (ADR-058; ADR-082 Proposed); relations (ADR-024, ADR-025; ADR-083, ADR-084 Proposed); the connection map (a plain map of the relations between items: tap an item to highlight all its connections, tap a connected item to move there; not the atlas of D-149; core: release 1 does not ship without it, owner decision 2026-11-16); themes (ADR-060); the tutorial, also in Easy language (ADR-032, D-146; owner decision 2026-11-16: in, as core); English and German; accessibility; export reminder (D-126); readable export (Markdown, OPML, JSON) and readable import; the Zettelkasten claim (owner decision 2026-11-18, [zettelkasten-claim.md](zettelkasten-claim.md)): Obsidian import and export with the round-trip guarantee G1 to G7, the update import (ADR-078), branch export and import from the thought's actions (ADR-080), relation notes (ADR-077), the tree feature "Headings become thoughts" (ADR-079), one untranslated name per structural element (ADR-081), the map drawing links as untyped connections; the claim is made at release 1 when its acceptance scenarios pass; user-controlled updates (ADR-036); feedback as GitHub issue, copy or file (ADR-051, ADR-070); performance report (ADR-051). **May slip to 1.x without rework** (new event types or UI only), hidden by release flags until complete: views and saved searches (ADR-062); templates (ADR-068) with copy and paste between trees (ADR-069); importing a tree into another; diagnostics view. **Out:** server sync, sync modes (ADR-057), server address event (ADR-021), sharing UI, private feedback inbox (ADR-055), experiment and permission flags, binary data (ADR-022), store app, links as relations (UX open question 19), key rotation, removing members, dream features |
| 4 | Open decisions | Before release 1: AOT and the load budgets (the spike), the stored size limit numbers; from the Zettelkasten session (2026-11-18): the sketches for the connection map, relation notes, headings in notes and the readable import and export flow; from the session of 2026-11-19: the sketches for the Tags and Relation types branches and the beta format-change prompt, and the `related` item in merged trees. Decided 2026-11-19: tags and relation types as items, relation ids, predefined relation types in new trees, beta data (ADR-082 to ADR-085, Proposed). After the storage spike: the optional unencrypted search index. Decided: storage engine (IndexedDB), JSON for envelope version 1, the encryption changes of decision 1 (ADR-073). Before the first server release: server event store, sync API contract, replicas and live sync, account deletion and retention, server size limits. Later: key backup (direction: recovery code), log growth, binary data, key rotation, membership. See the open decisions table |
| 5 | Quality bar | Push gate fully green, without the server parts (server tests, container test, end-to-end scenarios 1 and 9, sync deployment checks), which switch on with the first server release; end-to-end scenario 7 (pairing) stays. The usability core flows are a gate, no longer pending: capture, edit, move, indent, Focus, search, resolve conflicts, pair, file round trip, move along connections on the map. Mutation score 80 % for changed Domain code in the push gate; whole Domain watched nightly, a gate once it reaches 80 %. Nightly green on the release commit and no unreliable test open in the last 7 nights. Download size budget set after the spike and green; complexity checks green up to 100,000 events. Before release: the guided release check on phone, tablet and desktop (including a file round trip between two of them and, offline start from the iPhone home screen on a borrowed device) and an exploratory agent run at phone size and keyboard only, with no open finding the owner labels as blocking |
| 6 | Done | Core features complete, unfinished slip features hidden by release flags (not in UI or tutorial); formats final and written as format data 1.0 (envelope version 1, payload wrapper, pairing and sharing code, export file, readable formats, local store layout); the decisions due before release 1 recorded here and in the ADRs; quality bar met; every sentence of the tutorial's privacy branch (D-135) and of the protection explanation checked against the shipped code (it says plainly that there is no server and that losing all devices without a readable export loses the tree); production deployment on Azure Static Web Apps through the manual approval, git tag and archived build; repository public under Apache 2.0 with README and licence (ADR-049); the command table in AGENTS.md filled; no bet card has the trigger "the repository goes public" (the repository is public already), so no such review is due |
| 7 | Order of foundation work | (1) Project setup and harness skeleton: `.github/CODEOWNERS` and branch protection with code-owner review, the independent reviewer agent as a required check and the weekly digest, six projects, `global.json`, analyzers and banned APIs, architecture tests, CI branch check and push gate, Static Web Apps preview per pull request, AGENTS.md commands. (2a) Domain core, specification first: events with the payload wrapper, hybrid clock, projection, reference model, generators, INV-1 to INV-14; in parallel (2b) storage and AOT spike in real browsers (Chromium and Firefox; Safari later on a borrowed device, see the open decisions). (3) Formats and encryption: envelope version 1, padding, fixed test data, export file, pairing code, stored size limits. (4) Client shell: application layer, Tree Manager with non-extractable key, local store adapter, encrypted local data, design tokens and component base, PWA and update control. (5) Core features in thin vertical slices (order refined later; the connection map comes after relations). (6) Release hardening: format data 1.0, privacy text check, bet review, release check on three devices |

## 10. Quality Requirements

Scenarios grouped by quality goal; "secured by" names the invariant, test kind or ADR that checks them.

**Reliability**

| # | Scenario | Expected | Secured by |
| --- | --- | --- | --- |
| R1 | App closed while writing | No event lost after reopening | End-to-end scenario 8, simulation |
| R2 | Device offline for weeks, then syncs | Nothing lost; all devices show the same tree | Simulation, INV-5 |
| R3 | Same events in a different order, with duplicates | Identical tree and conflicts on every device | INV-1, INV-2, INV-9 |
| R4 | Two devices rename the same item offline | Both values kept, conflict shown, resolved once for all devices | INV-9, ADR-018 |
| R5 | A device's clock is three days ahead | Same order on every device, warning shown | ADR-014, simulation |
| R6 | Older app receives newer events | Sees every item and text, stays editable, shows a warning | INV-10, ADR-016 |
| R7 | Data written by release 1 opened by any later release | Complete | Format tests |
| R8 | Update migrates local storage | Nothing missing | ADR-038, end-to-end scenario 5 |

**Privacy & Security**

| # | Scenario | Expected | Secured by |
| --- | --- | --- | --- |
| P1 | Someone reads the whole server store | No content, structure, device labels, note lengths, IP addresses or times of use; this constrains the open decision on the server store | Server security tests |
| P2 | Someone observes the running server | At most online times, approximate time zone and IP address (hidden by a VPN); never content | ADR-030 |
| P3 | The server alters, moves or swaps envelopes | Detected and rejected | Format tests, ADR-015 |
| P4 | Another user of a shared tree writes malicious Markdown | Renders harmlessly; no access to keys | ADR-052 |
| P5 | Server data is stolen | Cannot be used to join accounts or push events | ADR-043 |
| P6 | Code tries to contact a third party | The browser blocks it | ADR-045, deployment tests |

**Usability**

| # | Scenario | Expected | Secured by |
| --- | --- | --- | --- |
| U1 | Capture a thought on the phone | New item within two taps of opening the app (design target) | UI specifications |
| U2 | Use without touch or without sight | Every flow completable with keyboard and screen reader; WCAG 2.2 AA as the target, verified automatically for the automatable criteria and through feedback for the rest | Accessibility tests, ADR-033 |
| U3 | Sharing, unencrypted export, importing a tree into another | Never without a confirmed warning | Application layer tests, ADR-041 |
| U4 | German interface with longer texts | Layout holds | Pseudo-localisation, UI specifications |

**Portability**

| # | Scenario | Expected | Secured by |
| --- | --- | --- | --- |
| PO1 | Core flows in Chromium, WebKit and Firefox | Work in all three | UI specifications (push gate), end-to-end tests (nightly) |
| PO2 | Self-hosting on a Raspberry Pi | Runs without Azure, with SQLite | Container tests, ADR-047, ADR-048 |
| PO3 | A user leaves ThoughtTree | Readable export complete; JSON imports back completely | Format tests, ADR-019 |

**Maintainability**

| # | Scenario | Expected | Secured by |
| --- | --- | --- | --- |
| M1 | An agent changes Domain code | Gate result within 10 minutes; a failure names the broken invariant and the seed | Harness principles |
| M2 | An agent weakens a test, stored test data or a baseline | Blocked; needs human approval | ADR-035 |
| M3 | An agent adds a package with an incompatible licence | Push gate fails | ADR-050 |

**Performance** (design targets, not tested as absolute numbers)

| # | Scenario | Target | Secured by |
| --- | --- | --- | --- |
| PF1 | Start offline with 10,000 items | Tree visible within 1 s | Complexity checks, AOT decision |
| PF2 | Edit an item | Display updated within 50 ms | Design target |
| PF3 | Search with 10,000 items | First hits within 200 ms | Complexity checks |
| PF4 | Import 10,000 events | Visible within 5 s | Complexity checks |
| PF7 | Readable import of an Obsidian vault with 5,000 notes on an iPad | Within 60 s, with progress and cancel; all or nothing (proposed 2026-11-17, to be confirmed by measuring on the iPad) | Complexity checks, measurement |
| PF5 | First load | Within the download size budget (value to be set after the storage and AOT decisions) | Size budget gate |
| PF6 | Ten times more data | About ten times the time, never more | Complexity checks |

## 11. Risks and Technical Debt

- Blazor WASM startup size and initial load time, increased by loading the culture data for all languages
- Local store access from Blazor requires JS interop (IndexedDB; a Web Worker only if SQLite is ever adopted); performance for large logs needs measurement
- Event log growth: needs snapshotting and possibly compaction strategy
- Conflict rules for moves are non-trivial; need thorough tests
- Single developer: scope creep towards task management (the MLO trap)
- A shared key cannot be taken back: anyone who has received a tree's key keeps access to that tree
- Background sync in a PWA is limited: browsers decide themselves when periodic background sync runs, and some (Safari, iOS) do not support it; scheduled sync may only run while the app is open until a store app or package exists
- Multiple trees multiply the work: sync, search, snapshots and export all run per tree
- The browser may delete local data: Safari removes storage of sites not used for a while unless the app is installed to the home screen; mitigated by persistent storage, installing the PWA and regular export
- Losing every device that holds a tree loses the tree, because the server only holds ciphertext
- Hybrid clocks and replay order are easy to get subtly wrong; covered by property-based convergence tests
- Old app versions on rarely used devices skip newer events and may show an outdated tree until updated
- Authorship is not proven: anyone with a tree's key can write events that appear to come from another device or user; the server can withhold or replay events
- Membership only grows: devices and users cannot yet be removed from a tree, and device tokens do not expire
- A device with a clock far ahead, by mistake or on purpose in a shared tree, moves the clock of every device forward; last-writer-wins then stops following real time until real time catches up
- Live mode keeps connections open, which can prevent scaling to zero and raise cost; with several server replicas, in-memory rate limits and counters are per replica and reset on every cold start
- WCAG 2.2 AA is only partly verifiable automatically; screen reader experience relies on feedback
- Server data of accounts that are no longer used stays until account deletion and retention are decided
- LambdaTale is a niche dependency that every specification relies on (ADR-075, bet card with review triggers)

## 12. Glossary

| Term | Meaning |
| --- | --- |
| Outliner | Editor whose primary structure is a hierarchy of items rather than pages |
| Tree | One independent outline with its own key and event log; a device can hold several |
| Item | The thing in the tree: a title, an optional note, tags, and optionally a theme and views (later pictures and files); what it is (thought, idea, project, topic, vision) is up to the user, and an item can grow |
| Branch | An item together with everything below it |
| Node | Technical term only: how an item appears in the tree, its line and place; used in rendering and specifications |
| Title | The short text on an item's line |
| Thought | The user-facing word for an item in the texts the user reads (D-140); documents and code say item |
| Note | The optional longer body of an item |
| Top-level Item | An item without a parent; a tree can have several |
| Parent / Children | The item directly above an item / the items directly below it |
| Sibling Order | The order of the children under one parent; follows from replaying the events, each placing an item after a named sibling |
| Hybrid Logical Clock | The clock carried by every event: UTC time plus a counter; it follows real time where possible and never goes back behind any event the device has seen, so events can be ordered the same way on every device |
| Conflict | Two or more changes with different values of the same field of an element (item, relation type, display name, tree feature, server address, view; with ADR-083 and ADR-084, Proposed, also a relation's type and no display names) made without seeing each other; shown to the user, who picks the value |
| Agreed Event Order | The order every device replays a tree's events in: by hybrid logical clock, then device id |
| Collapse | Hide an item's children in the view; UI state of one device, not a domain event |
| Focus | Make an item the temporary top of the view, showing its branch and the item as a page (title, tags, note; D-151, ADR-072); "zoom" is used only for magnifying (pinch or browser zoom) |
| Event | Immutable record of one change to the tree |
| Event Log | The sequence of all events of one tree |
| Projection | The tree rebuilt by replaying events |
| Snapshot | A saved projection, used to speed up replay |
| Tombstone | Marker that an item was deleted without removing its data |
| User | A person who uses ThoughtTree, with a self-chosen name; a user can have several devices |
| Device | One installation of ThoughtTree, on a phone, tablet or desktop, belonging to one user |
| Device Label | The name of a device, such as "Tablet", shown next to the changes it made |
| Private Tree | A tree held only by devices of one user |
| Shared Tree | A tree held by devices of more than one user |
| Pairing | Bringing a tree onto another device of the same user, via QR code or typed code |
| Sharing | Giving a tree to another user by handing over its key and account identity; every user of a shared tree can read and change all of it |
| Key | The end-to-end encryption key of one tree; exists only on devices that hold that tree |
| Account | The identity of one tree towards the sync server (account id and account secret); one account per tree; not a user account: there are no user accounts or logins on the server |
| Account Secret | Secret created on the device with a tree; proves to the sync server that a device may join that tree's account |
| Device Token | Credential that lets a device access a tree's event log on the sync server |
| Event Envelope | The stored and transmitted form of an event: a few plaintext fields plus the encrypted payload |
| Upcasting | Converting an event written in an older schema version to the current shape when it is read |
| Sync Transport | The way events travel between devices (`ISyncTransport`): through the server or as an export file |
| Export File | An encrypted file containing a tree's events, used for device-to-device sync and as a backup |
| Readable Export | An unencrypted Markdown, OPML or JSON file with the current tree, without history |
| Release Flag | A temporary switch, shipped with the app, that keeps an unfinished feature off in an environment |
| Experiment Flag | A switch that gives a feature to a share of accounts, to learn whether it works; ends with the review of its bet card |
| Operations Flag | A long-lived server switch that turns something off quickly if it causes trouble (kill switch) |
| Permission Flag | A flag per account on the server that says what the account may do |
| Bet | An architecture decision that rests on an assumption; its bet card says what should improve, how we notice it and when we review it |
| Tree Feature | A feature the user switches on or off for a tree, recorded as an event so all devices of the tree agree |
| View | A saved perspective of an item's branch or the whole tree: a name, a query and a layout |
| Inbox | The item of a tree where captured thoughts wait to be placed; always shown last at the top level |
| Template | An item marked as a template; creating from it copies its branch, valid in its parent's branch |
| Tag | A structural element of a tree, named by display names, that items can carry in any number; typed with `@@` or chosen in the tag picker; with ADR-082 (Proposed) an item inside the tree's Tags branch, named by its title |
| PWA | Progressive Web App: installable, offline-capable web application |
| Installation | The app as loaded from one server (the hosted service or a self-hosted one), with its own local store and trees; it syncs only with that server |
| Tree Access | What a device needs outside the event log to read and sync a tree: key, `KeyId`, account id, account secret |
| User Identity | User id and user name of a person; carried to their other devices by pairing |
| Tree Name | The title of the first visible top-level item other than the inbox (and the Tags and Relation types branches, ADR-082, ADR-084, Proposed) |
| `EventId` | Globally unique id of an event; readable in the envelope; the same id must always carry the same content (INV-13) |
| `KeyId` | Short random id of a tree's key, readable in the envelope |
| `ServerSequence` | Gap-free number the server gives each envelope of an account, in commit order; used only to track sync progress |
| `reverts` | Event id of the change an undo event reverts |
| `actionId` | Id shared by all events written for one user action; quick undo reverts an action as one step |
| `replaces` | List of event ids of the changes an event replaces; used to detect conflicts |
| `seen` | For each device of a tree, the highest clock a device had received from it; carried by `item.deleted` |
| Envelope / Schema / App Version | Version of the stored envelope format / of one event type's shape / of the installed release |
| Padding Bucket | One of the fixed sizes a payload is padded to before encryption |
| Join Code | Code an operator hands out so that accounts can register on a server in "join code only" mode |
| Sync Mode | Live, manual or scheduled; chosen per tree and device |
| Relation / Relation Type | A typed, directed connection between two items of a tree / its type, defined per tree |
| `item:` Link | A Markdown link to an item of the same tree by its id |
| Backlink | A link or incoming relation shown on the item it points to |
| Display Name | The name of a structural element (for example a relation type) in one language (one name for all languages once ADR-081, Proposed, is accepted; dropped by ADR-084, Proposed) |
| Draft | Text being edited, saved locally until it becomes an event at the end of the editing session |
| Gate / Watch | A test result that stops delivery on a single failure / a measure that is tracked over time |
| Invariant | A core rule of the domain with an id (INV-n), checked by property tests |
| Reference Model | A deliberately naive projection that serves as the referee for the real one |
| Regression Seed | A simulation seed that failed once and is replayed on every push |
| Specify / Secure / Confirm | The three intents of tests: acceptance criteria / correctness and rules / the whole system in real environments |
| Inner Loop / Branch Check / Push Gate | Tests an agent runs while working / the smaller set CI runs on every push to a branch / the full set that must pass before a change can merge (runs on pull requests) |
| Spec Host | A test build of the client with hooks to load state and control time, never shipped |
| Test Id Catalogue | Shared C# constants naming the `data-testid` of every element that shows a domain concept |
| Bet Card | The entry for a bet in `docs/adr/bets.md`: TL;DR, outcome, assumption, signal, review trigger |
| Diagnostics Block / Usage Summary | Numbers and switch states added to feedback / counts computed on the device from one tree the user chooses |
| Feedback Inbox / Operator | The server's optional inbox for feedback encrypted to the operator / the person who runs a server |
| MLO | MyLifeOrganized, the task outliner whose UI inspired ThoughtTree |
