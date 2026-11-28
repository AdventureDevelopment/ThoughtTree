# ADR-091: Archive is everyday, erase is final; content lives apart from structure

id: ADR-091 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Amends ADR-005 (archived instead of deleted; erased content is removed), ADR-011 (device labels and user names move into content blobs), ADR-015 (the content blob has its own plaintext fields), ADR-016 (`item.lifecycle-changed` replaces `item.deleted` and `item.restored` among the basic events), ADR-018 (no `seen` on archiving), ADR-019, ADR-024, ADR-062, ADR-080 and ADR-082 ("deleted" reads "archived"), ADR-059 (erasing cannot be undone), ADR-063 (the terms archive and erase), ADR-073 (content blobs are encrypted like events), ADR-074 (tree lifecycle in the first stored format), ADR-078 (missing files archive, not delete), ADR-085 (the update flow archives the old trees), ADR-087 (content blobs are padded), invariant INV-5 (erased text leaves the history; the catalogue changes only with the owner's approval, ADR-035) and UX decisions D-26, D-53, D-58, D-59, D-69, D-99, D-119, D-181; owner decisions 2026-11-27 and 2026-11-28 (open decision "Archive, content and erasure", architecture.md section 9).

The content rules stand once spike 2 (`docs/plan/next-work.md`) meets its decision rule; otherwise they go back to the owner. How a device label or user name is erased (its current or every value) is decided with the identity ADR (open decision "Identity and signed events").

Archive:

- **MUST** give an item and a tree the state `Lifecycle { Live, Archived }`, changed only by `item.lifecycle-changed` (`itemId`, `lifecycle`) and `tree.lifecycle-changed` (`lifecycle`); there is no deleted state, and neither event carries `seen`
- **MUST** archive a branch by one event on its item: descendants keep their own state and are hidden because an ancestor is archived; restoring brings the branch back, and a descendant archived earlier stays archived
- **MUST** keep archived items in the projection, hidden from the outline, the connection map, tag counts and readable export, with their relations, tags and views hidden until restored; search shows them only with a filter "include archived", marked as archived
- **MUST** show a link in text to an archived item like a link to an erased one, offering to restore the archived branch it belongs to
- **MUST** list archived items in a restore view that offers Restore and Erase permanently, and no moving out
- **MUST** apply silently a change that arrives for an archived item or tree, also one written before the archive was seen; no conflict is derived from it
- **MUST** take an archived tree out of the tree list onto More › Trees › Archived trees…, offering only Restore and Erase permanently; opening, searching or exporting needs a restore
- **MUST** archive, in the update flow of ADR-085 (D-181), each exported tree in the old format before the update
- **MUST** keep "Remove from this device" local: it writes no event and is offered for live and archived trees

Content:

- **MUST** keep the text a person typed out of events, in content blobs: item titles and notes, relation notes, reverse names of relation types, view names, device labels and user names; events keep ids, clocks and structure
- **MUST** give each content blob an id derived from its event's id, its own nonce and padding (ADR-087's buckets), encrypted with the tree key (ADR-073); plaintext only the blob id, `KeyId` and nonce
- **MUST NOT** let an event's id, signature or hash cover blob content
- **MUST** treat a blob that has not arrived as missing, never as empty, and ask in each exchange which blobs each side has
- **MUST** keep `deviceId` and `userId` in events; only the label and name text are content

Erase:

- **MUST** offer erasing only for archived items and archived trees, as "Erase permanently…", asking first; the UI names the limits: devices that are offline and files exported earlier keep the text
- **MUST** erase all or nothing by `item.content-erased` (`itemId`) or `tree.content-erased`: every content blob of every event about the item and all its descendants (or every item of the tree), older values included, with the notes of their relations and the names of their views; only the structure stays
- **MUST** delete erased blobs on every device that syncs and keep the erased marker for good, so no device takes the text back
- **MUST NOT** restore an erased item; it stays archived and is shown as erased
- **MUST** reject a text change for erased content, also one written before the erasure was seen: delete its blob and list it, without text, among the events the projection could not apply (D-128)
- **MUST NOT** undo an erasure or revert it from the history (exception to ADR-059)

Terms:

- **MUST** call the everyday action "archive" and the irreversible one "erase" (German "archivieren", "endgültig löschen…"); **MUST NOT** use "delete" for either

verify:

```bash
dotnet test --filter "ADR=ADR-091"
```

why: Archiving keeps the log's promise that nothing is lost; erasing keeps the user's right to have text gone (GDPR Art. 17) without changing a stored event, which only works when text lives apart from structure (the pattern "forgettable payloads", Mathias Verraes).
