# ADR-046: Infrastructure as code, deployment without stored secrets

id: ADR-046 · status: Accepted · kind: constraint

applies_to: `infra/**`, `.github/**`

- **MUST** create test and production only from Bicep templates in `infra/`
- **MUST** keep test and production identical apart from parameters
- **MUST** sign in from GitHub Actions to Azure through OpenID Connect federation; **MUST NOT** store deployment secrets
- **MUST** keep promotion to production a manual approval step
- **MUST** configure in the templates that logging and telemetry record no IP addresses

verify:

```bash
az bicep build --file infra/main.bicep   # templates compile
```

why: Reviewable, identical environments; no credentials an agent could see or leak.
