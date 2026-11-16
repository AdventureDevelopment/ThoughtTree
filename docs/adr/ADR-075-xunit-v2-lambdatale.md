# ADR-075: xUnit v2 and LambdaTale 2.9.x for all test projects

id: ADR-075 · status: Accepted · kind: bet ([bet card](bets.md#adr-075))

applies_to: `tests/**`, `Directory.Packages.props`, `**/*.csproj`

- **MUST** use xUnit v2 (`xunit` 2.9.x) in every test project, because `bbv.LambdaTale` 2.9.x depends on `xunit.core` 2.9.x
- **MUST** write specs with `bbv.LambdaTale` 2.9.x (ADR-054)
- **MUST NOT** reference xUnit v3 packages (`xunit.v3*`, `xunit` 3.x) or add another specification or BDD library
- **MUST** keep these versions in `Directory.Packages.props` only; changing them is a human decision made through the bet card

verify:

```bash
dotnet test --filter "ADR=ADR-075"
! grep -rEn 'xunit\.v3|Include="xunit" Version="3' --include='*.csproj' --include='Directory.Packages.props' .   # no match
```

why: Specs are the specification agents work towards; the spec library and its test framework must not drift or be swapped while LambdaTale has no xUnit v3 support.
