# ADR-041: Client application layer, thin components

id: ADR-041 · status: Accepted

applies_to: `src/ThoughtTree.Client*/**`

- **MUST** put use cases, UI state and UI decisions into ThoughtTree.Client.App, plain C# without Razor or browser dependencies
- **MUST** keep Razor components to showing state and forwarding user input
- **MUST** enforce privacy-critical confirmations (sharing, unencrypted export, merging trees) in the application layer, so no component can bypass them
- **MUST NOT** use HTML snapshot tests; assert meaning instead

verify:

```bash
dotnet test --filter "ADR=ADR-041"
```

why: UI behaviour becomes fast, deterministic unit tests and runs in the simulation; thin components are hard to get wrong.
