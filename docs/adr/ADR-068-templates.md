# ADR-068: Templates are marked items, valid below their parent

id: ADR-068 · status: Proposed · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** keep a template as an ordinary item with its branch, marked by events (`item.template-set` / `item.template-removed` with the item id), so templates sync, are shared and keep their history like any content
- **MUST** make a template valid in the branch of its parent; a template at the top level is valid in the whole tree; moving the template item changes where it is valid; no special root item exists for this
- **OPEN** (UX decision D-111): whether template lines can be link slots creating typed relations (`item.slot-set`), or stay text with inline `item:` links, or carry a relation type for links typed into them
- **MUST** create an item from a template by copying the template's branch as one action (ADR-067): new ids, the template marking and slot markings not copied; each slot becomes a line that offers to link an item by the slot's relation type or to write text
- **MUST NOT** store a reference from a created item to its template; a template's tags are copied with its branch, so items made from it can be found by tag (UX decision D-112)
- **MUST** offer predefined templates (initially "Person", carrying the tag "person") with fixed ids, written as ordinary events when a tree is created, like the predefined relation types (ADR-025); they can be changed or removed
- **MUST** leave templates out of search, "Changed recently", counts and the readable export of content, unless the user asks for templates; they are included in the full JSON export (ADR-019)
- **MUST NOT** store the display-only group row ("Templates · n") that the outline shows for the templates of a branch; it is a rendering of the marked items

verify:

```bash
dotnet test --filter "ADR=ADR-068"
```

why: Templates as items need no second structure next to the tree and are edited and moved with the tools the user already knows; "valid below the parent" gives branch templates and tree-wide templates without a root item (UX decisions D-107, D-108, D-110).
