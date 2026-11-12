# ADR-054: Code organisation: boundaries, feature folders, test intents

id: ADR-054 · status: Accepted

applies_to: `src/**`, `tests/**`

- **MUST** keep the six source projects: Domain, Contracts, Client.App, Client, Server.Core, Server
- **MUST NOT** add a project without a real boundary (deployment, sharing, reuse); **MUST NOT** add "Shared" or "Common" projects
- **MUST** separate layers inside a project by namespace and check them with architecture tests (for example: UI does not use browser adapters directly, endpoints do not use stores directly)
- **MUST** organise folders by feature, with the same feature names in every project
- **MUST** place tests by intent under `tests/specify`, `tests/secure` or `tests/confirm`, name projects by kind (`Specs`, `Properties`, `Compatibility`, `Rules`, `Security`, `Budgets`, `Smoke`) and set the `Intent` trait
- **MUST** write specs with LambdaTale in domain language; **MUST NOT** change or delete a spec without human approval

verify:

```bash
dotnet test --filter "ADR=ADR-054"   # architecture tests check project references, namespaces and test placement
```

why: Real boundaries where they help (deployment, sharing, reuse, `internal`); everything else stays light and is still checked.
