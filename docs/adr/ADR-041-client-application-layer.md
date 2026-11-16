# ADR-041: Client application layer, thin components

id: ADR-041 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** put use cases, UI state and UI decisions into ThoughtTree.Client.App, plain C# without Razor or browser dependencies
- **MUST** keep Razor components to showing state and forwarding user input
- **MUST** keep navigation state (focus, selection, scroll position) per view, so several views can exist at once (structure sidebar, side-by-side panes)
- **MUST** enforce privacy-critical confirmations (sharing, unencrypted export, importing a tree into another) in the application layer, so no component can bypass them
- **MUST NOT** use HTML snapshot tests; assert meaning instead

verify:

```bash
dotnet test --filter "ADR=ADR-041"
```

why: UI behaviour becomes fast, deterministic unit tests and runs in the simulation; thin components are hard to get wrong.
