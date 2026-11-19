# infra: hosting on Azure Static Web Apps (release 1)

The test environment, the production environment and the pull request previews come only from the templates here (ADR-046). GitHub Actions signs in through OpenID Connect; no secret, password or deployment token is stored anywhere.

| File | What it does | Who runs it |
| --- | --- | --- |
| `main.bicep` | One Static Web App (Free plan) for one environment | The deploy workflows, on every deployment |
| `test.bicepparam`, `production.bicepparam` | The parameters of the two environments; they differ only in their values | The deploy workflows |
| `bootstrap/bootstrap.bicep`, `bootstrap/deploy-identity.bicep` | The resource groups `rg-thoughttree-test` and `rg-thoughttree-prod`, and the three sign-in identities | The owner, once (below) |

## Identities and what each may do

| Identity | GitHub may use it for | Azure access |
| --- | --- | --- |
| `id-thoughttree-test` | Jobs in the GitHub environment `test` (only `main`) | Contributor on `rg-thoughttree-test` |
| `id-thoughttree-production` | Jobs in the GitHub environment `production` (only after the owner's approval) | Contributor on `rg-thoughttree-prod` |
| `id-thoughttree-preview` | `pull_request` runs of this repository (not forks) | Custom role "ThoughtTree preview deployer" on `rg-thoughttree-test`: read the site, read its deployment token, delete its staging environments. Because it reads the token, any pull request run of a branch in this repository (agents' branches included) can upload content to the test site, its main slot included; it cannot change any other resource |

## Workflows

| Workflow | When | What |
| --- | --- | --- |
| `deploy-test.yml` | Every push to `main` | Branch checks, then apply `main.bicep` with `test.bicepparam`, publish, upload, check the headers of ADR-045 |
| `preview.yml` | Pull request opened or updated; closed | Upload a preview named `pr<number>` to the test site (link in the run summary); remove it when the pull request closes |
| `deploy-production.yml` | Started by hand on `main` (Actions › deploy-production › Run workflow) | Waits for the owner's approval, then the same steps with `production.bicepparam` |

The scripts are in `eng/deploy/`. The Free plan allows only a few staging environments at a time (three at the time of writing); a preview fails when they are used up, until a pull request closes.

## One-time setup (owner, about 20 minutes, works from a tablet browser)

Needs the role Owner (or Contributor plus User Access Administrator) on the Azure subscription, because the bootstrap creates role assignments and a custom role.

1. **Run the bootstrap in Azure Cloud Shell.** Azure portal, Cloud Shell icon in the top bar, choose Bash.
   - Get the files, either with the GitHub CLI in Cloud Shell:

     ```bash
     gh auth login   # choose GitHub.com, HTTPS, "Login with a web browser", enter the code on github.com/login/device
     gh repo clone AdventureDevelopment/ThoughtTree -- --depth 1 --branch main
     cd ThoughtTree
     ```

     or by uploading `infra/bootstrap/bootstrap.bicep` and `infra/bootstrap/deploy-identity.bicep` (Cloud Shell, "Manage files", Upload) into one folder and using that folder below instead of `infra/bootstrap/`.
   - Run it (the repository name must be written exactly as GitHub shows it):

     ```bash
     az deployment sub create --name thoughttree-bootstrap --location westeurope \
       --template-file infra/bootstrap/bootstrap.bicep \
       --parameters repository=AdventureDevelopment/ThoughtTree \
       --query properties.outputs
     ```

   - The output lists five values: `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_CLIENT_ID_TEST`, `AZURE_CLIENT_ID_PRODUCTION`, `AZURE_CLIENT_ID_PREVIEW`. They are identifiers, not secrets. Running the bootstrap again changes nothing that already exists.
2. **Create the GitHub environments.** Repository, Settings, Environments, New environment:
   - `test`: Deployment branches and tags, Selected branches and tags, add `main`.
   - `production`: Required reviewers, add yourself; Deployment branches and tags, Selected branches and tags, add `main`.
3. **Add the five repository variables.** Settings, Secrets and variables, Actions, tab Variables (not Secrets), New repository variable, once for each value from step 1, with the same names.
4. **First deployment.** The next merge to `main` creates the test site and prints its address in the run summary of `deploy-test`. Pull request previews work from then on.

## Removing

Delete the resource groups `rg-thoughttree-test` and `rg-thoughttree-prod` (sites and identities go with them), then the custom role "ThoughtTree preview deployer" (Subscription, Access control, Roles), the two GitHub environments and the five variables.
