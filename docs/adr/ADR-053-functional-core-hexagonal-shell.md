# ADR-053: Functional core, hexagonal shell

id: ADR-053 · status: Accepted

applies_to: `src/**`

- **MUST** keep Domain pure: commands plus state give events, events plus state give the new state; no ports, no I/O
- **MUST** put use cases and UI state into the application layer, which defines the ports it needs
- **MUST** implement ports only in adapters, each with a fake for tests
- **MUST** let dependencies point only inwards: UI → Application → Domain; adapters → Application; Domain depends on nothing (the UI references Application only)
- **MUST** let the UI bind only to view models of the application layer; **MUST NOT** reference Domain from the UI
- **MUST NOT** introduce per-use-case ceremony (interactor, input and output ports, request and response models, presenters) or entities with behaviour as the centre
- **MUST** structure the server the same way: Server.Core with ports, store adapters, ASP.NET Core as the outer shell

verify:

```bash
dotnet test --filter "ADR=ADR-053"   # architecture tests (ArchUnitNET)
```

why: Every kind of code has exactly one place; the rule is mechanically checkable; the core is testable without anything around it.
