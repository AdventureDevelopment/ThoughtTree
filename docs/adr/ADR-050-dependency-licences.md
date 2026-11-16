# ADR-050: Approved licences for dependencies

id: ADR-050 · status: Accepted · kind: constraint

applies_to: `**/*.csproj`, `**/package.json`, `Directory.Packages.props`

- **MUST** use only dependencies whose licence is on the approved list: MIT, Apache 2.0, BSD-2-Clause, BSD-3-Clause, ISC, and similar permissive licences approved by a human
- **MUST** check the licences of all direct and transitive dependencies in the push gate, including test projects and the components of the container base image
- **MAY** ship fonts under the SIL Open Font License 1.1 (ADR-061)
- **MAY** use MPL-2.0 dependencies in test projects that are not distributed (approved for the accessibility checker axe)
- **MUST NOT** add a dependency with a paid, copyleft or unknown licence without human approval

verify:

```bash
dotnet test --filter "ADR=ADR-050"   # licence check over the resolved dependency graph
```

why: Agents add packages freely; one incompatible licence can make the whole project unusable for others.
