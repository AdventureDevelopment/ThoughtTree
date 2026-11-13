# ADR-046: Infrastructure as code, deployment without stored secrets

id: ADR-046 · status: Accepted · kind: constraint

applies_to: `infra/**`, `.github/**`

- **MUST** create test, production and the pull request previews only from Bicep templates in `infra/` (Azure Static Web Apps in release 1, Azure Container Apps once a sync server exists)
- **MUST** keep test and production identical apart from parameters
- **MUST** sign in from GitHub Actions to Azure through OpenID Connect federation; **MUST NOT** store deployment secrets
- **MUST** keep promotion to production a manual approval step
- **MUST** protect `main`: no direct pushes, the push gate required, CODEOWNERS review for protected paths
- **MUST** configure in the templates that logging and telemetry record no IP addresses

verify:

```bash
az bicep build --file infra/main.bicep   # templates compile; when: push gate (needs the Bicep CLI, no Azure login)
dotnet test --filter "ADR=ADR-046"   # template rules: no stored secrets, IP logging off, identical parameters
```

why: Reviewable, identical environments; no credentials an agent could see or leak.
