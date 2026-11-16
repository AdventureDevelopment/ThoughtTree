# infra

Bicep templates for the hosting of release 1 (ADR-046, ADR-047): one Azure Static Web App per
environment, created only from `main.bicep` with `test.bicepparam` or `production.bicepparam`.
Pull request previews are staging environments of the test site.

| Environment | Resource group | Site | GitHub identity (federated subjects) |
| --- | --- | --- | --- |
| test and previews | `rg-thoughttree-test` | `swa-thoughttree-test` | `thoughttree-github-test` (`pull_request`, `environment:test`) |
| production | `rg-thoughttree-prod` | `swa-thoughttree-production` | `thoughttree-github-prod` (`environment:production`) |

Each identity has the Contributor role on its own resource group only, so a pull request (which can
change its own workflow) can reach the test resource group but never production.

## What the owner sets up once

1. Both resource groups and both user-assigned identities with their federated credentials
   (issuer `https://token.actions.githubusercontent.com`, audience `api://AzureADTokenExchange`,
   subject `repo:AdventureDevelopment/ThoughtTree:<subject>`).
2. GitHub environments: `test`; `production` with the owner as required reviewer and deployment
   branches limited to `main` (the manual approval of ADR-046).
3. Repository variables (Settings, Secrets and variables, Actions, Variables; not secrets):
   `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_CLIENT_ID_TEST`, `AZURE_CLIENT_ID_PRODUCTION`.

No secret is stored anywhere: the workflows sign in with OpenID Connect and fetch the site's
deployment token at run time.

## Workflows

- `.github/workflows/preview.yml`: pull requests from this repository; applies the test template,
  uploads the preview, removes it when the pull request closes.
- `.github/workflows/deploy.yml`: `main`; publishes once, deploys to test, then to production after
  approval; each deployment ends with `eng/smoke-headers.sh` (ADR-045 headers).

## Checks without Azure

`tests/secure/ThoughtTree.Infrastructure.Rules` compiles the templates with the Bicep CLI from NuGet
(Linux build) and checks the ADR-046 template and workflow rules and the ADR-045 headers:
`dotnet test tests/secure/ThoughtTree.Infrastructure.Rules`.
