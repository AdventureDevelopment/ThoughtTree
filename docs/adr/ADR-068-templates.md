# ADR-068: Templates are marked items, valid below their parent

id: ADR-068 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Client*/**`

- **MUST** keep a template as an ordinary item with its branch, marked by events (`item.template-set` / `item.template-removed` with the item id), so templates sync, are shared and keep their history like any content
- **MUST** make a template valid in the branch of its parent; a template at the top level is valid in the whole tree; moving the template item changes where it is valid; no special root item exists for this
- **MUST** create an item from a template by copying the template's branch as one action (ADR-067): new ids, the template marking not copied
- **MUST** rewrite `item:` links and relations between items of the template's branch to the copies; links and relations pointing outside the branch are kept as they are
- **MUST** recognise `::` as the template shortcut only at the start of an empty line; `\::` there keeps two literal colons
- **MUST NOT** store a reference from a created item to its template; a template's tags are copied with its branch, so items made from it can be found by tag (UX decision D-112)
- **MUST NOT** write templates into new trees; the tutorial tree carries a set of templates (Person, Talk or meeting, Source, Decision, Idea) that the user copies into their own trees (ADR-069)
- **MUST** leave templates out of search, "Changed recently", counts and the Markdown and OPML export, unless the user asks for templates; the JSON export (ADR-019) always contains them
- **MUST NOT** store the display-only group row ("Templates · n") that the outline shows for the templates of a branch; it is a rendering of the marked items

verify:

```bash
dotnet test --filter "ADR=ADR-068"
```

why: Templates as items need no second structure next to the tree and are edited and moved with the tools the user already knows; "valid below the parent" gives branch templates and tree-wide templates without a root item (UX decisions D-107, D-108, D-110).
