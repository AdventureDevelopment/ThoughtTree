# ThoughtTree – Architecture Documentation (arc42)

## 1. Introduction and Goals

ThoughtTree is a hierarchical note-taking Progressive Web App for structuring thoughts as an outline tree. It is inspired by the UI of MyLifeOrganized (MLO), but focused on notes and thinking rather than task management.

**Key requirements**

- Hierarchical (tree-based) notes with unlimited nesting, collapse/expand, drag-and-drop, zoom into a node
- Runs on every device as an installable PWA
- Standalone first: a device works fully on its own, with no internet, no account and no server; pairing with other devices and server sync are optional and can be added later
- Synchronisation of nodes across devices over the internet
- Touch-first outliner UX for phone and tablet; keyboard shortcuts on desktop as a nice add-on
- End-to-end encrypted: notes are encrypted on the device before sync; the server never sees plaintext
- Fast full-text search across all notes, performed on the device (the server never sees plaintext)
- Direct device-to-device sync: two paired devices can exchange events with each other without going through the sync server
- Multiple trees per device: a device can hold several independent trees side by side, each with its own key and event log (for example a private and a work tree)
- See where changes come from: every device has a label (for example "Tablet"), and a node shows which device last changed it and when
- Explicit sharing: pairing another device of one's own and sharing a tree with another user are two separate actions; every tree shows whether it is private or shared, and with whom

**Quality goals**

| Priority | Quality goal | Motivation |
| --- | --- | --- |
| 1 | Usability | Outliner UX like MLO on touch devices: quick capture, drag-and-drop, gestures; keyboard navigation secondary |
| 2 | Reliability | No data loss offline; deterministic sync and conflict handling |
| 3 | Privacy & Security | Personal thoughts stay private: end-to-end encryption on the device, the sync server only relays ciphertext; strict per-user scoping |
| 4 | Maintainability | Single language (C#) across client and server |
| 5 | Performance | Trees with thousands of nodes stay responsive |

**Stakeholders**

| Role | Expectation |
| --- | --- |
| Owner / developer (Sourceress) | A tool for personal thinking that is fun to build and maintain in C# |
| End users (initially the owner) | Reliable notes on phone, tablet and desktop |

## 2. Constraints

- Technology: C# / .NET, Blazor WebAssembly as PWA
- Must work offline; network is optional and intermittent
- Source control: private GitHub repository `AdventureDevelopment/ThoughtTree`
- Single developer, incremental delivery
- Hosting: Microsoft Azure (developer familiarity), free tiers wherever possible (Static Web Apps for the PWA, Container Apps or Functions for the sync service); target cost near zero for a single user
- Server storage stays minimal: an append-only encrypted event log per account (one account per tree); concrete store (Azure Table Storage, Blob Storage or SQLite) is an open decision
- Development process: trunk-based development on `main` with feature flags (Microsoft.FeatureManagement) instead of long-lived feature branches
- CI/CD via GitHub Actions: every push builds and tests, deploys automatically to a test environment on Azure; promotion to production is a manual approval step

## 3. Context and Scope

**Business context**

- User interacts with the ThoughtTree PWA on one or more devices; every device works on its own
- Devices can exchange a tree's events directly as an encrypted file, without internet
- Devices can optionally synchronise a tree through a central sync service over HTTPS/WebSockets
- Other users can receive a tree that is shared with them and then work on it like the owner

**Technical context**

| Partner | Interface | Purpose |
| --- | --- | --- |
| Browser / OS | Service Worker, IndexedDB, Web App Manifest, Web Crypto API | Installability, offline cache, local persistence, client-side encryption |
| Sync Service (ASP.NET Core on Azure) | REST + SignalR | Push/pull of encrypted events, real-time notifications |
| Authentication (initially device tokens, later optional OpenID Connect) | Bearer token | Identifies which account's event log a device may access; kept behind an `IAuthProvider` abstraction so it can be swapped |
| Other devices (own or of other users) | Pairing or sharing flow (QR code or typed code) | Hands over a tree's key and account identity; no server involvement in key material |
| File transfer of the OS (share sheet, AirDrop, Nearby Share, USB) | File download / file picker, Web Share API where available | Carries the encrypted export file between devices |

## 4. Solution Strategy

- **Blazor WebAssembly PWA** for a single C# codebase on every device
- **Event sourcing** as the core data model: every change to the tree is an immutable event; the current tree is a projection of the event log
- **Offline-first**: events are appended locally in IndexedDB first, then synchronised
- **Sync by exchanging event logs** instead of whole documents, which keeps conflicts small and mergeable
- **ASP.NET Core backend** as a thin event store and relay (REST for push/pull, SignalR for live updates)
- **Shared domain project** (.NET class library) containing events, aggregates and projections, used by client and server
- **End-to-end encryption**: event payloads are encrypted on the device with the tree's key, handed over only by pairing or sharing; the server stores and relays ciphertext plus minimal routing metadata (account id, event id, sequence number)
- **Several independent trees per device**: each tree has its own key, account identity and event log; nothing is shared between trees
- **Deterministic replay**: every device orders a tree's events by Lamport clock and device id, so all devices compute the same tree from the same events
- **Touch-first UI**: the outliner is designed for phone and tablet gestures first; desktop keyboard shortcuts come as an add-on
- **Pluggable sync transport**: the sync protocol ("send me your events since X") is independent of how the bytes travel; the Sync Engine talks to an `ISyncTransport` abstraction, with the server as the first transport
- **Device-to-device sync by encrypted file**: works without internet and is triggered manually; one device exports its event log as an encrypted file, the user hands it over (AirDrop, Nearby Share, USB), the other device imports and merges it. A native wrapper with automatic local discovery is a possible later upgrade

## 5. Building Block View

**Level 1 – System**

| Building block | Responsibility |
| --- | --- |
| ThoughtTree.Client (Blazor WASM) | UI, local event store, projections, sync client |
| ThoughtTree.Domain (class library) | Event definitions, tree aggregate, projection logic, validation |
| ThoughtTree.Server (ASP.NET Core) | Authentication, event store per user, sync API, SignalR hub |

**Level 2 – Client**

| Component | Responsibility |
| --- | --- |
| Outline UI | Tree rendering, touch editing, gestures, drag-and-drop, zoom; keyboard shortcuts on desktop |
| Tree Manager | Lists, creates and switches trees; holds each tree's key, account identity and private/shared state |
| Command Handler | Turns user actions into domain events; stamps them with the next Lamport clock value |
| Local Event Store | Append-only log in IndexedDB; tracks synced/unsynced events |
| Projection | Builds the in-memory tree from events; incremental updates; snapshots |
| Search | Client-side full-text search over the projection; optional local index |
| Crypto | Key generation and storage, encryption/decryption of event payloads (Web Crypto API), device pairing payloads |
| Sync Engine | Exchanges events through an `ISyncTransport`: push unsynced encrypted events, pull remote events, decrypt, merge and apply |
| Server Transport | `ISyncTransport` over the Sync API and Sync Hub |
| File Transport | `ISyncTransport` that writes the encrypted export file and reads an imported one |
| Auth Client | Device token handling behind `IAuthProvider`; swappable for OIDC later |
| Service Worker | Caches app shell and assets for offline start |

**Level 2 – Server**

| Component | Responsibility |
| --- | --- |
| Sync API (REST) | `POST /events` (push ciphertext), `GET /events?since=` (pull) |
| Sync Hub (SignalR) | Notifies connected devices about new events |
| Event Store | Persistent per-account append-only log of encrypted events (store to be decided: Table Storage, Blob Storage or SQLite) |
| Registration | Registers an account on first contact; issues device tokens to devices that present the account secret |
| Auth | Validates device tokens, scopes requests to one account |

## 6. Runtime View

**Scenario: Editing a node offline**

1. User edits a node's title in the Outline UI
2. Command Handler creates a `NodeTitleChanged` event with the next Lamport clock value
3. Local Event Store appends the event (marked unsynced)
4. Projection applies the event; UI updates immediately

**Scenario: Coming back online**

1. Sync Engine detects connectivity
2. Pushes all unsynced events to `POST /events`
3. Pulls events since last known server sequence number
4. Applies remote events to the projection; resolves conflicts
5. Server broadcasts new events to other devices via SignalR

**Scenario: Real-time update on a second device**

1. SignalR hub signals "new events available"
2. Device pulls events since its last sequence number
3. Projection updates the tree

**Scenario: Concurrent edits on two devices**

1. Device A and device B both hold clock 41 and are offline
2. On A the user renames node N to "Ideas"; the event gets clock 42 and A's device id
3. On B the user renames the same node to "Thoughts"; the event also gets clock 42, with B's device id
4. After the next sync both devices hold both events and replay them in the agreed order: clock 42, then device id
5. Both devices show the title from the event that comes last in that order; the other title stays in the log and can be shown in the node's history
6. Both devices raise their clock to at least 42, so their next events come after both renames

**Scenario: Event from a newer app version**

1. Device A runs a newer app version and writes an event type or event version that device B does not know
2. Device B receives the event by sync or file, stores it in its Local Event Store and keeps it for further sync
3. The projection on B skips the event and the app suggests updating
4. After the update B replays the log again and the event takes effect

**Scenario: First device setup**

1. User installs ThoughtTree and opens it for the first time
2. App asks for a user name and generates a user id; this is the user identity
3. App generates a device id and asks for a device label, with a suggestion such as "Phone" or "Tablet"; name and label can be changed later
4. App generates the key and an account identity (account id and account secret) for the first tree locally with the Web Crypto API and stores them in IndexedDB; nothing is sent anywhere
5. User starts taking notes; events are stored locally
6. No internet, login or server is needed at any point

**Scenario: Creating another tree on a device**

1. User chooses "New tree" and gives it a name
2. App generates a new key and a new account identity for this tree, locally
3. The new tree starts empty and is independent of all other trees on the device
4. User moves between trees with the tree switcher

**Scenario: Pairing another device of the same user**

1. Pairing is for the user's own devices; it is optional, works without internet, and always concerns one tree
2. On a device that holds the tree the user opens "Pair my other device"
3. App renders a QR code containing the tree's key and account identity plus the user identity, generated locally (also shown as a typed code for devices without a camera)
4. The new device scans the QR code (or the user types the code)
5. The new device asks the user what to do: add the tree next to the trees already on this device, merge one of this device's trees into it, or cancel
6. Add: the tree is stored as a further tree with its own key; nothing else on the device changes
7. Merge: the chosen local tree gives up its own key and account identity and adopts the ones from the QR code; its events are kept, their envelopes are re-encrypted with the adopted key, and they are merged with the other device's events on the next sync
8. The new device takes over the user identity from the QR code and writes a `DeviceJoined` event, so the tree knows this device belongs to the same user
9. The devices now exchange the tree's events by file, or through the server if server sync is switched on for that tree

**Scenario: Sharing a tree with another user**

1. Sharing is a separate action from pairing; it works without internet and always concerns one tree
2. On a device that holds the tree the user opens "Share with another user" and confirms a warning: the other user will be able to read and change the whole tree, and this cannot be taken back
3. App renders a QR code containing the tree's key and account identity, marked as a share; it does not contain the user identity
4. The other user's device scans the QR code and stores the tree as a further tree next to its own; its own trees and its own user identity stay untouched
5. The other user's device writes a `DeviceJoined` event with its own user identity
6. From then on the tree is shown as shared on all devices that hold it, with the list of users and their devices
7. Events are exchanged by file, or through the server if server sync is switched on for that tree

**Scenario: Switching on server sync (optional)**

1. On any device the user switches on server sync
2. The device contacts the Sync Service with the account identity; the first device to do so registers the account, later devices prove membership with the account secret
3. Sync Service issues a device token for this device
4. The device pushes its event log and pulls events it does not have yet
5. Other paired devices join the same way whenever the user switches server sync on there

**Scenario: Device-to-device sync without internet**

1. On device A the user chooses "Export for another device"
2. Device A writes its event log, already encrypted with the key, into a single file
3. The user hands the file to device B (AirDrop, Nearby Share, USB or any other file transfer)
4. On device B the user chooses "Import from another device" and picks the file
5. Device B merges the events into its Local Event Store; events it already has are recognised by their `EventId` and skipped
6. Projection applies the new events and resolves conflicts with the same rules as server sync
7. For a two-way sync the user repeats the steps from B to A
8. When either device is online again, it pushes the merged events to the server as usual

## 7. Deployment View

| Node | Content |
| --- | --- |
| User device (browser / installed PWA) | Blazor WASM app, Service Worker, IndexedDB (events, snapshots, key) |
| Azure Static Web Apps (free tier) | Blazor WASM static files |
| Sync service on Azure | ASP.NET Core Sync Service; hosting on Container Apps or Functions is an open decision |
| Event log storage on Azure | Append-only encrypted event log per account; Table Storage, Blob Storage or SQLite is an open decision |

**Environments**

- Two environments, test and production, each with its own static site, sync service and storage
- Data is never shared between test and production

**Delivery pipeline**

- GitHub Actions builds and tests every push to `main`
- A successful build deploys automatically to test
- Promotion to production is a manual approval step
- Unfinished features stay switched off in production through feature flags

## 8. Cross-cutting Concepts

**Domain model**

- `Node`: Id (GUID), ParentId, Title, Note, CreatedAt, UpdatedAt, Deleted; its position among its siblings is not stored on the node but follows from replaying the events
- `Tree`: one independent outline, the projection over one event log; it has its own key and its own account identity (account id and account secret)
- A device can hold several trees; they share nothing and are synced, searched and exported separately
- `Root`: implicit, with a fixed well-known id that is the same in every tree and on every device; it is never created by an event, so two event logs that are merged always refer to the same root
- `Device`: Id (GUID), Label; the label travels as a `DeviceLabelChanged` event in the log of every tree the device holds, so all other devices of that tree learn it, and it is encrypted like every other event
- `User`: Id (GUID), Name; created on first setup and carried to the user's other devices by pairing; it is self-declared and not verified by anyone
- A tree is private while all devices that have joined it belong to one user, and shared as soon as a device of another user has joined; this is derived from the `DeviceJoined` events, not stored as a flag

**Event types (initial set)**

- `NodeCreated(nodeId, parentId, afterNodeId, title)`
- `NodeTitleChanged(nodeId, title)`
- `NodeNoteChanged(nodeId, note)`
- `NodeMoved(nodeId, newParentId, afterNodeId)`
- `NodeDeleted(nodeId)`
- `NodeRestored(nodeId)`
- `DeviceJoined(deviceId, deviceLabel, userId, userName)`
- `DeviceLabelChanged(deviceId, label)`
- `UserNameChanged(userId, userName)`

`afterNodeId` names the sibling the node is placed directly after; empty means first child of the parent.

Each event carries: `EventId` (GUID), `DeviceId`, `Clock` (Lamport clock), `Timestamp` (for display only; ISO 8601 with the device's UTC offset at the time of writing, for example `2026-11-11T15:30:00+01:00`), `LocalSequence`, and after sync a `ServerSequence`.

**Ordering of siblings**

- Position by reference: `NodeCreated` and `NodeMoved` place a node directly after a named sibling (`afterNodeId`), or first under its parent when empty
- The projection replays all events of a tree in one agreed order, sorted by `Clock`, then `DeviceId`; every device computes the same order, independent of the order in which events arrived
- Each device keeps one Lamport clock per tree: a new event gets the highest clock seen in that tree plus one; events received by sync or file raise the clock to their highest value
- The local and server sequence numbers only track sync progress; they never determine the tree
- Two nodes placed after the same sibling at the same time are ordered by that agreed event order
- If the referenced sibling is deleted, it stays in the projection as a tombstone, so the reference still resolves
- If the referenced sibling is under a different parent at the time of replay, the node is placed first under its parent

**Conflict resolution**

- Events are commutative where possible; otherwise last-writer-wins per field, decided by the agreed event order (`Clock`, then `DeviceId`)
- Wall-clock timestamps are never used to decide conflicts, because device clocks drift
- Moves that would create a cycle are rejected on apply and re-parented to the root
- Deleted nodes are tombstoned, never physically removed, to keep merges consistent
- Merging always means taking the union of two event logs, with events identified by `EventId`; trees are never merged directly, the projection rebuilds the tree from the combined log
- This also covers two devices that were used on their own before pairing: their top-level nodes become siblings under the shared root

**Event envelope and encryption**

- An event is stored and transmitted as an envelope: a few plaintext fields plus one encrypted payload
- Plaintext: `EventId`, envelope format version, `KeyId` (which key of the tree encrypted the payload) and, once synced, `ServerSequence`; the account id comes from the device token and is not repeated in each event
- Encrypted: everything else, including event type, `DeviceId`, `Clock`, `Timestamp` and the event data, so the server learns neither content nor structure nor which device wrote what
- Encryption with AES-GCM (256-bit key, fresh random nonce per event) via the Web Crypto API; `EventId` and account id are bound to the ciphertext as additional authenticated data, so the server cannot move or swap events unnoticed
- The same envelopes are used in IndexedDB, on the server and in the export file
- An envelope may be re-encrypted, for example when a local tree is merged into a received one and adopts its key; this does not count as rewriting the log, because the event inside stays unchanged

**Event versioning**

- Events are never changed or rewritten once written (only their envelope may be re-encrypted); every event carries its event type and a schema version
- Older versions are converted to the current shape when read (upcasting), so the projection only handles the current shape
- Unknown event types or newer versions from a newer app on another device are stored and synced unchanged, but skipped by the projection until the app is updated
- New fields are added as optional fields with a defined default, so older apps can still read the events they know

**Persistence**

- Client: IndexedDB (via a JS interop wrapper) with two stores: `events` and `snapshots`
- Snapshots speed up startup; the projection replays only events after the latest snapshot
- All local data is partitioned by tree: events, snapshots, key, account identity and search index are stored per tree id
- The app asks the browser for persistent storage (`navigator.storage.persist()`) so local data is not evicted under storage pressure
- The export file doubles as a backup; the app reminds the user to export when a tree has never been synced or exported for a while

**Search**

- All search runs on the client over the decrypted in-memory projection; no server-side search is possible by design
- Initial implementation: linear scan with case-insensitive and fuzzy matching, highlighting the path to each hit
- If trees grow large: an incremental local index (inverted index of titles and notes) persisted in IndexedDB and updated as events are applied
- Search scope can be limited to the currently zoomed subtree
- Limitation accepted: search only works on paired devices that have synced
- Each tree has its own search index; one search can cover the open tree or all trees on the device, and every hit shows which tree it belongs to

**Security**

- End-to-end encryption: event payloads are encrypted on the device with the key (Web Crypto API); the server stores and relays only ciphertext
- The key never leaves the user's devices except through pairing (QR code or typed code); the server holds no key material
- Authentication by device token behind `IAuthProvider`; all sync endpoints require a valid token; OpenID Connect is an optional later upgrade
- Server registration uses an account secret that is created on the device and shared only through pairing
- Events are scoped per account; no cross-account access
- HTTPS everywhere
- Consequence accepted: losing the key on all devices means the notes cannot be recovered
- Private and shared are recorded in the event log and shown in the UI, but not enforced by cryptography: every holder of the key can read and change the whole tree, there is no read-only access, and a key passed on outside the app leaves no trace

**Feature flags**

- Unfinished features are switched off by feature flags (Microsoft.FeatureManagement), in the client and on the server
- Flags are set per environment through configuration; production only switches a feature on once it is complete
- Flags never change the event model: events of a feature that is switched off are still stored, synced and replayed

**Testing**

- The Domain project carries most of the logic and is covered by unit tests without browser or server
- Property-based tests check convergence: the same set of events, in any arrival order and with duplicates, always gives the same tree
- Further property-based tests cover moves that would create cycles, references to deleted or moved siblings, and replay after snapshots
- The Sync Service is tested with integration tests against the chosen store; the client with component tests and a few end-to-end tests in a browser

**UI / UX**

- Touch-first for phone and tablet: tap to edit, drag-and-drop to move, gestures for indent and outdent (details to be designed)
- Zoom into any node as if it were its own document
- Breadcrumb for the current zoom path
- Desktop add-on: keyboard shortcuts such as Enter (new sibling), Tab / Shift+Tab (indent/outdent), arrows (navigate), Ctrl+Up/Down (move)
- Tree switcher to move between the trees on a device
- A node shows which device last changed it and when, using the device label; a full change history per node is possible later, since all events are kept
- Times are shown in the viewing device's local time; the original local time of the change (from the offset in the timestamp) can be shown as well, for example when a change was made while travelling
- Every tree is marked private or shared; a shared tree lists its users and their devices, and changes show the user as well as the device
- "Pair my other device" and "Share with another user" are two separate actions with separate wording and a warning before sharing
- UI state (collapsed nodes, current zoom, open tree) belongs to one device: it is stored locally per tree and never synced; only domain events go into the event log

## 9. Architecture Decisions

| ID | Decision | Rationale |
| --- | --- | --- |
| ADR-1 | Blazor WebAssembly instead of JS framework | Developer is a C# developer; single language across stack |
| ADR-2 | Event sourcing as data model | Enables offline-first and log-based sync with small, mergeable conflicts |
| ADR-3 | Own ASP.NET Core sync service instead of BaaS | Full control over event model and merge rules; stays in .NET |
| ADR-4 | Sibling order by reference (`afterNodeId`) in the events, not by order keys on nodes | Fits event sourcing: the order follows from replaying the log; no growing keys |
| ADR-5 | Tombstones instead of hard deletes | Deterministic merges, undo/restore possible |
| ADR-6 | Sync transport behind `ISyncTransport` | The event exchange is independent of how bytes travel; server, file and later transports plug in without touching the event model |
| ADR-7 | Device-to-device sync by encrypted file export/import | Must work without internet; manual sync is acceptable; a browser cannot discover devices on the local network; doubles as a backup |
| ADR-8 | Standalone first: key and account identity are created on the device, server sync is optional per tree | A device must work with no internet, account or server |
| ADR-9 | Merging means the union of event logs, never a merge of trees; the root is implicit with a fixed id | One merge rule for server sync, file sync and pairing of devices that started on their own |
| ADR-10 | Multiple trees per device, each with its own key, account identity and event log | Follows from keys being independent of devices; allows separate trees and sharing one tree without exposing the others |
| ADR-11 | Pairing (own devices) and sharing (other users) are separate actions; users and devices are recorded by `DeviceJoined` events | Makes visible where changes come from and whether a tree is private or shared |
| ADR-12 | One key per tree with equal access for every holder; no read-only access and no revocation for now | Keeps the encryption design simple; per-user keys are a much larger design and are deferred |
| ADR-13 | UI state (collapse, zoom, open tree) is device-local and never synced; only domain events enter the event log | Keeps the log a record of content changes; each device keeps its own view |
| ADR-14 | Events are ordered by a Lamport clock plus device id, not by wall-clock time or arrival order | Every device must compute the same tree from the same events; device clocks drift and arrival order differs per device |
| ADR-15 | Only `EventId`, envelope version, `KeyId` and server sequence stay in plaintext; event type, device, clock and data are encrypted | The server only needs to store, deduplicate and relay; everything else would reveal structure and activity. Accepted consequence: the server can never filter or compact events by type. `KeyId` keeps later key rotation possible |
| ADR-16 | Events are versioned and upcast on read; unknown events are kept and synced but skipped | Devices run different app versions; the log must never be rewritten and no device may drop another device's events |

**Open decisions**

| Topic | Options |
| --- | --- |
| Server event store | Azure Table Storage, Blob Storage or SQLite |
| Sync service hosting | Azure Container Apps or Azure Functions |
| Touch gestures for indent, outdent and move | To be designed and tested on phone and tablet |
| Log growth | Whether and how old events can ever be compacted without breaking merges with devices that were offline |
| Key backup | Whether to offer a recovery code so a tree survives the loss of all devices |

## 10. Quality Requirements

| Scenario | Target |
| --- | --- |
| Start the app offline | Full tree visible within 1 s for up to 10,000 nodes |
| Edit a node | UI reflects change in under 50 ms |
| Reconnect after 1 day offline | All local changes synced without data loss |
| Concurrent edit on two devices | Both edits preserved or deterministically resolved |
| Same events arrive in a different order on two devices | Both devices show exactly the same tree |
| Search in a tree with 10,000 nodes | First results within 200 ms |
| Import an export file with 10,000 events | Merged and visible within 5 s |
| Attacker reads the whole server store | Learns no note content, no tree structure and no device labels |
| Device used with no internet for weeks | Works fully; nothing is lost when it syncs again |

## 11. Risks and Technical Debt

- Blazor WASM startup size and initial load time
- IndexedDB access from Blazor requires JS interop; performance for large logs needs measurement
- Event log growth: needs snapshotting and possibly compaction strategy
- Conflict rules for moves are non-trivial; need thorough tests
- Single developer: scope creep towards task management (the MLO trap)
- A shared key cannot be taken back: anyone who has received a tree's key keeps access to that tree
- Multiple trees multiply the work: sync, search, snapshots and export all run per tree
- The browser may delete local data: Safari removes storage of sites not used for a while unless the app is installed to the home screen; mitigated by persistent storage, installing the PWA and regular export
- Losing every device that holds a tree loses the tree, because the server only holds ciphertext
- Lamport clocks and replay order are easy to get subtly wrong; covered by property-based convergence tests
- Old app versions on rarely used devices skip newer events and may show an outdated tree until updated

## 12. Glossary

| Term | Meaning |
| --- | --- |
| Outliner | Editor whose primary structure is a hierarchy of nodes rather than pages |
| Tree | One independent outline with its own key and event log; a device can hold several |
| Node | A single item in the tree, with a title and an optional note |
| Title | The short text on a node's line |
| Note | The optional longer body of a node |
| Root | The invisible top node of the tree |
| Parent / Children | The node directly above a node / the nodes directly below it |
| Sibling Order | The order of the children under one parent; follows from replaying the events, each placing a node after a named sibling |
| Lamport Clock | A logical counter carried by every event; a device sets it one higher than the highest clock it has seen, so events can be ordered the same way on every device |
| Agreed Event Order | The order every device replays a tree's events in: by Lamport clock, then device id |
| Collapse | Hide a node's children in the view; UI state of one device, not a domain event |
| Zoom | Make a node the temporary root of the view |
| Event | Immutable record of one change to the tree |
| Event Log | The sequence of all events of one tree |
| Projection | The tree rebuilt by replaying events |
| Snapshot | A saved projection, used to speed up replay |
| Tombstone | Marker that a node was deleted without removing its data |
| User | A person who uses ThoughtTree, with a self-chosen name; a user can have several devices |
| Device | One installation of ThoughtTree, on a phone, tablet or desktop, belonging to one user |
| Device Label | The name of a device, such as "Tablet", shown next to the changes it made |
| Private Tree | A tree held only by devices of one user |
| Shared Tree | A tree held by devices of more than one user |
| Pairing | Bringing a tree onto another device of the same user, via QR code or typed code |
| Sharing | Giving a tree to another user by handing over its key and account identity; every user of a shared tree can read and change all of it |
| Key | The end-to-end encryption key of one tree; exists only on devices that hold that tree |
| Account | The identity of one tree towards the sync server (account id and account secret); one account per tree |
| Account Secret | Secret created on the device with a tree; proves to the sync server that a device may join that tree's account |
| Device Token | Credential that lets a device access a tree's event log on the sync server |
| Event Envelope | The stored and transmitted form of an event: a few plaintext fields plus the encrypted payload |
| Upcasting | Converting an event written in an older schema version to the current shape when it is read |
| Sync Transport | The way events travel between devices (`ISyncTransport`): through the server or as an export file |
| Export File | An encrypted file containing a tree's events, used for device-to-device sync and as a backup |
| Feature Flag | A switch that keeps an unfinished feature off in an environment |
| PWA | Progressive Web App: installable, offline-capable web application |
