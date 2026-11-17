# Zettelkasten claim

Status: in progress (decisions worked through with the owner, started 2026-11-17). The claim, what is supported, the round-trip guarantee and the acceptance scenarios follow as the decisions are made; until then nothing here is a rule. Rules live in the ADRs, the open points in section 9 of [architecture.md](architecture.md).

## The claim

Decided by the owner, 2026-11-17 (decision 1): "Bring your Obsidian Zettelkasten into ThoughtTree, work on it on any device, and take it back to Obsidian. Notes, structure, links, tags and typed links survive the round trip; attachments, embeds, block links and plugin views do not, yet." The claim may be made only when the acceptance scenarios below pass.

## What is supported

| # | Obsidian | Status | In ThoughtTree |
| --- | --- | --- | --- |
| 1 | Note file | Mapped | Item; title from the file name; a first H1 equal to the file name is dropped |
| 2 | Folders | Mapped | Items; what builds the tree (folders, Folgezettel ids or a `parent`/`up` property) is decision 4 |
| 3 | Headings in a note | Mapped | Items inside the note's item (ADR-072); the import asks once for the whole vault |
| 4 | Body links `[[N]]`, `[[N\|text]]`, `[[N#heading]]`, `[text](N.md)` | Supported | Live `item:` links; display text becomes fixed link text; a heading link points to the item made from that heading |
| 5 | Block links `#^id`, ` ^id` markers | Mapped, lossy | Link to the note; the marker stays as text |
| 6 | Embeds of notes and headings | Mapped, lossy | Plain link, nothing embedded |
| 7 | Attachments (images, PDFs, audio) | Out until binary data (ADR-022) | Text kept, listed in the import report |
| 8 | Typed links: link properties (`source:`, `up:`, `parent:`, Wikilink Types) and inline fields (`supports:: [[X]]`) | Mapped | Relations, the key as relation type (details: decisions 2 and 3) |
| 9 | Tags, inline and in frontmatter, nested `#a/b` | Mapped | Tags (ADR-058), `/` kept in the name (tag nesting parked, D-15); the place of an inline tag in the text is lost |
| 10 | Other properties | Kept | Verbatim, written back on export; where they are kept is decision 4; `id` also identifies the note on re-import |
| 11 | `aliases` | For links | Resolve links on import; kept with the other properties |
| 12 | Unresolved `[[Ghost]]` | Plain text | Kept as written, listed in the import report, exported unchanged |
| 13 | Luhmann or timestamp ids in file names | Kept | Part of the title, or the source of the tree (decision 4) |
| 14 | Daily notes | Plain notes | No daily-note feature; the inbox is the capture point |
| 15 | Unlinked mentions | Not supported | Search covers it by hand |
| 16 | Callouts, highlights, `%%comments%%`, footnotes, math, mermaid | Plain text | Kept verbatim, shown as ordinary Markdown |
| 17 | Canvas, Bases, `.obsidian` settings, plugin caches | Out | Not imported; Canvas and Bases files listed in the report |
| 18 | Backlinks; graph view | Supported | "Linked from"; the connection map draws body links as untyped connections and typed links as relations (decision 2) |

## Links and relations

Decided by the owner, 2026-11-17 (decision 2, UX open question 19 partly): body links stay text (`item:` links, "Linked from"), never relations; typed links of a vault become relations on import (rows 8, decision 4). The connection map draws both: relations with their type, body links as untyped connections read from the backlink index, without events. No stored format changes; turning links into relations stays possible later. A relation made in ThoughtTree gets the default type `related` until the user changes it; "mentions" is only the interface name for the link connections, not a relation type.

## Note on a relation

Decided by the owner, 2026-11-17 (decision 3, ADR-077 Proposed): a relation can carry a note saying why the two thoughts are connected, stored in its own event `relation.note-changed` (built like `item.note-changed`: full text, `replaces`, visible conflicts, undo, history), keyed by the relation's three parts and hidden with the relation. Obsidian has no such field; its export form is decision 5. The interface needs a sketch (open decision "Relation note (UX)").

## Import: what builds the tree

Decided by the owner, 2026-11-17 (decision 4a): the import detects what the vault holds and proposes how the tree is built: from folders (default), from a `parent`/`up` property, or from Luhmann ids in file names, with counts. With a property or Luhmann ids, folders do not become items; a note with several parents goes under the first, the others become relations typed by the property name; a note without a parent goes to the top of the imported branch; cycles are broken where found and reported. A folder note (a note named like its folder) becomes the note of the folder's item. What happens to folder names when folders do not build the tree waits for the open decision "Tags as items" (architecture section 9); the proposal was to keep them as tags.

## Import: identity and re-import

Decided by the owner, 2026-11-17 (decision 4b, ADR-078 Proposed): the export writes each thought's id (`thoughttree` property) and the export point; importing such files into the tree they came from offers "Update from these files". Known thoughts get ordinary change events, a change made in ThoughtTree since the export becomes a visible conflict, unknown files become new thoughts, missing files are listed and deleted only after the user confirms, and the whole update is one undo step. A first import of a foreign vault is a copy; after one export, round trips update.

## Not a live interface on a vault

Decided by the owner, 2026-11-17: ThoughtTree imports and exports a vault; it is not an interface working directly on the vault's files. A PWA cannot open a folder on iPad, the files would replace the event log as the truth (ADR-002), they are plaintext at rest (ADR-073), and Obsidian and file sync would write them outside the conflict model. A later linked vault (re-import and re-export on request, the encrypted log stays the truth) builds on the identity rules decided here; see the open decision "Obsidian vault bridge" in section 9 of [architecture.md](architecture.md).

## Survey: what Obsidian-style Zettelkasten users keep in their files

Checked on 2026-11-17 against the Obsidian help and the plugin pages listed under Sources. Gaps are listed at the end; each becomes a check against a sample vault before the claim is made.

**Obsidian core**

- Links: wikilinks `[[Note]]`, `[[Folder/Note]]`, `[[Note|text]]`, `[[Note#H1#H2]]`, `[[#Heading]]`, `[[Note#^blockid]]`; standard Markdown links `[text](Note%20name.md)` are equivalent, chosen by a setting
- Block ids: ` ^abc-1` written into the text; block links work only inside Obsidian
- Embeds: `!` before a link embeds a note, heading, block, image (`|100x145` size), PDF (`#page=3`), audio or canvas
- Properties: YAML frontmatter, typed per name across the vault (text, list, number, checkbox, date, date and time, tags); links in properties are quoted (`source: "[[Book]]"`); defaults `tags`, `aliases`, `cssclasses`; singular `tag`, `alias`, `cssclass` deprecated since 1.9 but present in older vaults; no nested properties, no Markdown in properties
- Tags: `#a/b`, nested with `/`, case-insensitive, no spaces, not purely numeric; inline or in frontmatter
- Backlinks: linked mentions and unlinked mentions (the note's name appearing as plain text elsewhere)
- Unique note creator: timestamp names (`202611031045`, optionally followed by a title); setups keep the timestamp in an `id` property so renaming does not lose it
- Daily notes: files named `YYYY-MM-DD` in a chosen folder
- Canvas (`.canvas`, JSON Canvas) and Bases (`.base`, database views over properties) are separate file types
- Obsidian-only syntax: callouts `> [!info]`, `==highlight==`, `%%comment%%`, footnotes, `$math$`, mermaid
- Characters not usable in file names for links: `# | ^ : %% [[ ]]`

**Plugins that store something in the files**

| Plugin family | What it writes | Meaning for ThoughtTree |
| --- | --- | --- |
| Breadcrumbs, Labelled Links, Zettelkasten Outliner | Typed links as properties (`up: "[[A]]"`, `parent:`, `child: [...]`) or Dataview inline fields (`parent:: [[A]]`) | Relations with a type; the community's answer to link types, which Obsidian core has never shipped |
| Dataview | Inline fields `key:: value`, `[key:: value]`, `(key:: value)` | Typed links in the body, often on a line that also says why |
| Wikilink Types | `[[A\|… @supports …]]`, synced to frontmatter `supports: ["[[A]]"]` | Typed links again (desktop and mobile) |
| Luhman, Folgezettel, Zettelkasten Navigation, Note ID, Branch Tracker | Luhmann ids as file names (`1114a`, `21/3a1`) or `id` and `parent` properties | A Folgezettel is a tree with one parent per note: the ThoughtTree model |
| Zettelkasten Core and plain setups | Folders `Inbox`, `Literature`, `Permanent`; a `type:` property; `source: "[[…]]"` | Folders often mean the stage of a note, not its topic |
| Templater, QuickAdd, Note Refactor, Obsidian Git | Nothing special in the notes | None |
| AI plugins (Smart Connections, Zettelkasten LLM Tools) | Caches in hidden folders | Ignored; ADR-071 |

**Observations**

- Obsidian has no core way to say why two notes are connected; users write a sentence around the link ("Builds on [[X]] because …") or put it on an inline-field line. A note on a relation adds what Obsidian lacks; its export form must be one Obsidian users already read
- On iPad, Obsidian and most of these plugins run; forum reports describe slowness with very large vaults (20,000 to 40,000 notes), and many users capture on mobile and process on desktop

**Gaps (to check against a sample vault in Obsidian)**

- How Breadcrumbs derives implied reverse edges
- The options of the "New link format" setting (shortest path, relative, absolute)
- Whether links in properties count in Obsidian's backlinks and graph
- How many Zettelkasten users use Folgezettel ids (no reliable numbers)

**Sources**

[Links](https://obsidian.md/help/links) · [Embeds](https://obsidian.md/help/embeds) · [Properties](https://obsidian.md/help/properties) · [Tags](https://obsidian.md/help/tags) · [Bases](https://obsidian.md/help/bases) · [Syntax](https://obsidian.md/help/syntax) · [Advanced syntax](https://obsidian.md/help/advanced-syntax) · [Breadcrumbs](https://publish.obsidian.md/breadcrumbs-docs/Home) · [Breadcrumbs typed links](https://publish.obsidian.md/breadcrumbs-docs/Explicit+Edge+Builders/Typed+Links) · [Dataview metadata](https://blacksmithgu.github.io/obsidian-dataview/annotation/add-metadata/) · [Wikilink Types](https://community.obsidian.md/plugins/wikilink-types) · [Labelled Links](https://community.obsidian.md/plugins/labelled-links) · [Zettelkasten Core](https://community.obsidian.md/plugins/zettelkasten-core) · [Zettelkasten plugins](https://www.obsidianstats.com/tags/zettelkasten) · [Link types request](https://forum.obsidian.md/t/add-support-for-link-types/6994) · [Large vault on iPhone](https://forum.obsidian.md/t/performance-issues-on-iphone-14-pro-with-large-vault-40-000-notes-using-obsidian-sync/98759)
