# ADR-054: Code organisation: boundaries, feature folders, test intents

id: ADR-054 · status: Accepted · kind: constraint

applies_to: `src/**`, `tests/**`

- **MUST** keep the current six source projects (Domain, Contracts, Client.App, Client, Server.Core, Server); a further project, for example for a later native app, needs a real boundary and an ADR change
- **MUST NOT** add a project without a real boundary (deployment, sharing, reuse); **MUST NOT** add "Shared" or "Common" projects
- **MUST** separate layers inside a project by namespace and check them with architecture tests (for example: UI does not use browser adapters directly, endpoints do not use stores directly)
- **MUST** organise folders by feature, with the same feature names in every project
- **MUST** place tests by intent under `tests/specify`, `tests/secure` or `tests/confirm` (shared helpers in `tests/support`, stored data in `tests/data`), name projects by kind (`Specs`, `Properties`, `Model`, `Compatibility`, `Simulation`, `Rules`, `Contract`, `Security`, `Budgets`, `EndToEnd`, `Smoke`) and set the `Intent` trait
- **MUST** write specs with LambdaTale in domain language; **MUST NOT** change or delete a spec without human approval

verify:

```bash
dotnet test --filter "ADR=ADR-054"   # architecture tests check project references, namespaces and test placement
```

why: Real boundaries where they help (deployment, sharing, reuse, `internal`); everything else stays light and is still checked.
