# infra: hosting on Azure Static Web Apps (release 1)

The test environment, the production environment and the pull request previews come only from the templates here (ADR-046). GitHub Actions signs in through OpenID Connect; no secret, password or deployment token is stored anywhere. The design follows the security review of 2026-11-19 (on the branch `claude/azure-pipeline-security-review`, not merged here).

| File | What it does | Who runs it |
| --- | --- | --- |
| `main.bicep` | One Static Web App (Free plan) for one environment | The deploy workflows, on every deployment |
| `test.bicepparam`, `production.bicepparam`, `preview.bicepparam` | The parameters of the three sites; they differ only in their values | The deploy workflows |
| `bootstrap/bootstrap.bicep`, `bootstrap/deploy-identity.bicep`, `bootstrap/site-access.bicep` | The resource groups, the three sign-in identities, their federated credentials and their access | The owner, once (below) |

## Resource groups, identities and what each may do

| Resource group | Holds |
| --- | --- |
| `rg-thoughttree-identity` | The three managed identities and their federated credentials. **No deployment identity has any role here**, so no pipeline run can add a federated credential or a role assignment |
| `rg-thoughttree-test` | `stapp-thoughttree-test` |
| `rg-thoughttree-prod` | `stapp-thoughttree-production` |
| `rg-thoughttree-preview` | `stapp-thoughttree-preview`, which holds only the previews of open pull requests |

| Identity | GitHub may use it for (subject claim) | Azure access |
| --- | --- | --- |
| `id-thoughttree-test` | Jobs in the GitHub environment `test` (`repo:<owner>@<owner id>/<repo>@<repo id>:environment:test`; the environment allows only `main`) | Custom role "ThoughtTree site deployer" on `rg-thoughttree-test` only |
| `id-thoughttree-production` | Jobs in the GitHub environment `production` (`…:environment:production`; only `main`, only after the owner's approval) | The same role on `rg-thoughttree-prod` only |
| `id-thoughttree-preview` | Jobs in the GitHub environment `preview` (`…:environment:preview`; only `main`: `preview-deploy.yml`, which never runs pull request code) | The same role on `rg-thoughttree-preview` only: no access to the test or production site |

The custom role "ThoughtTree site deployer" (defined in `bootstrap.bicep`) allows exactly: `Microsoft.Resources/subscriptions/resourceGroups/read`, `Microsoft.Resources/deployments/*` (apply `main.bicep`), `Microsoft.Web/staticSites/read` and `write` (the site itself), `listSecrets/action` (read the deployment token), `builds/read` and `builds/delete` (list and remove staging environments). Why not a built-in role: there is none for Static Web Apps deployments, and Contributor also allows `Microsoft.ManagedIdentity/…/federatedIdentityCredentials/write`, with which a merged template change could let any branch or repository sign in as production without approval. The role has no `Microsoft.Authorization` and no `Microsoft.ManagedIdentity` action; rule tests keep it so (`DeploymentIdentityRules`).

## Workflows

| Workflow | When | Credential | What |
| --- | --- | --- | --- |
| `deploy-test.yml` | Every push to `main` | Job `build`: none. Job `deploy`: test identity, environment `test` | Branch checks; `build` publishes once without credentials and keeps the site as artifact `site-sha256-<digest>` for 90 days (run id, commit and digest in the run summary); `deploy` builds nothing, fetches that artifact as data and checks its digest, applies `main.bicep` with `test.bicepparam`, uploads and checks the headers of ADR-045 |
| `deploy-production.yml` | By hand on `main` (Actions › deploy-production › Run workflow), with the **run id of a successful deploy-test run** (and optionally its digest) | Production identity, environment `production`, after approval | Builds nothing. Job `verify` (no credential) checks through the GitHub API that the run is a successful push run of `deploy-test.yml` whose commit is on `main` (compared with the head of `main`; refused while a tag named `main` exists), fetches its artifact as data and checks it and its SHA-256, and shows commit and digest in its summary. After the approval, job `deploy` fetches it again, requires the same digest, applies `production.bicepparam` and uploads exactly those files (`swa deploy --env production`) |
| `preview-build.yml` | Pull request to `main` opened or updated (not from forks) | None: `contents: read`, no `id-token` | Runs the pull request's code; publishes and uploads the site as an artifact (kept 3 days). A pull request that changes only one spike (`spikes/<name>/`, plus `docs/spikes/`) and holds an executable `spikes/<name>/preview-site.sh` publishes that script's site instead of the app (spike preview, for trying a spike on a phone or tablet); the deploy checks are the same |
| `preview-deploy.yml` | When `preview-build` completes; nightly; by hand | Preview identity, environment `preview` | Runs from `main` only. Finds the pull request through the GitHub API, fetches the artifact as data (size, paths, links, digest checked; nothing in it is executed), removes previews of closed pull requests, uploads preview `pr<number>`; nightly and by hand it only removes previews of closed pull requests |

Scripts are in `eng/deploy/`. Rollback: run `deploy-production` again with the run id of an earlier `deploy-test` run. Artifacts are kept 90 days (the maximum for a public repository; a lower retention set in the repository settings shortens it), so rollback reaches back 90 days.

Previews: the Free plan allows **3 staging environments per site** ([plans](https://learn.microsoft.com/azure/static-web-apps/plans)). A fourth open pull request gets no preview: `preview-deploy` fails with a message naming the three previews; close or merge one, or delete a preview by hand (portal: `stapp-thoughttree-preview` › Environments), then re-run the failed run. Previews of closed pull requests disappear at the next preview deployment or within a day. They are deleted with `az staticwebapp environment delete` (`eng/deploy/preview.sh`, prune). After Azure has accepted the delete, Azure CLI polls the operation status at subscription scope (`Microsoft.Web/locations/staticSitesOperationStatuses/read`), which the site deployer role, assigned on the resource group only, cannot read; observed for `pr13` on 2026-11-24 (preview-deploy run 45420685246), when the delete went through all the same (its address answered 404 afterwards). Rather than widen the role or bypass the `az rest` ban (owner decision 2026-11-24), prune tolerates exactly that error: AuthorizationFailed naming only that action, on a subscription-scope operation status. Any other error stops the run with its message. Every delete, tolerated or not, is then verified on the environment list (resource group scope) for up to 3 minutes; if the preview is still listed, the run fails naming it. The tolerance relies on Azure CLI's polling failing only after Azure has accepted the DELETE; the list check catches the case where it was not. A preview shows the pull request's own content and `staticwebapp.config.json`: it may lack the policy of ADR-045 (the header check then fails the run), but it is a separate site on its own origin (`azurestaticapps.net` is on the Public Suffix List), so it reaches nothing of the test or production site. Anyone with its address can open it.

Deploy tools: the Static Web Apps CLI comes from `eng/deploy/package-lock.json` (`npm ci --ignore-scripts`, registry signatures verified), its deploy client `StaticSitesClient` is pinned by SHA-256 in `eng/deploy/deploy-client.json`. Both are installed before the sign-in; the upload runs after the Azure CLI has signed out, with only the site's deployment token in its environment (Azure CLI signed out, OpenID Connect request variables and the job's GitHub token removed). When Microsoft stops listing the pinned client (its metadata keeps only the tags `latest`, `stable` and `backup`), deployments stop with a message until the pin is updated in a pull request (steps in `deploy-client.json`). Licences outside the ADR-050 list are decided in `eng/deploy/licences.json`; the owner approved all of them on 2026-11-19: BlueOak-1.0.0 (graceful-fs, jackspeak, minipass, package-json-from-dist, path-scurry), 0BSD (password-prompt, tslib 1 and 2), CC0-1.0 (railroad-diagrams), dual or triple licences whose permissive choice is used (expand-template, node-forge, rc, type-fest 0.21 and 4.41), **no licence field** (spawn-command, valid-url; both ship an MIT licence text), and `StaticSitesClient` (closed-source Microsoft binary, no licence text). The owner sets each `status` to approved or rejected in a pull request; a rule test fails on any new package outside the list that is not recorded there. The owner approved StaticSitesClient on 2026-11-19 under ADR-050 on these conditions: pinned by SHA-256, run with only the deployment token in its environment after `az logout`, and the run fails when the pinned build disappears from Microsoft's metadata; the approval covers the pinned build only (`eng/deploy/licences.json`).

## One-time setup (owner, about 30 minutes, works from a tablet browser)

Needs the role Owner (or Contributor plus User Access Administrator) on the Azure subscription, because the bootstrap creates role assignments and a custom role.

0. **Only if an earlier version of the bootstrap was ever run:** delete the identities `id-thoughttree-test` and `id-thoughttree-preview` in `rg-thoughttree-test` and `id-thoughttree-production` in `rg-thoughttree-prod` (their role assignments go with them; check Access control (IAM) of both groups that no Contributor assignment for a deleted identity remains), then the custom role "ThoughtTree preview deployer" (Subscription › Access control › Roles).
1. **Run the bootstrap in Azure Cloud Shell.** Azure portal, Cloud Shell icon in the top bar, choose Bash.
   - Get the files, either with the GitHub CLI in Cloud Shell:

     ```bash
     gh auth login   # choose GitHub.com, HTTPS, "Login with a web browser", enter the code on github.com/login/device
     gh repo clone AdventureDevelopment/ThoughtTree -- --depth 1 --branch main
     cd ThoughtTree
     ```

     or by uploading the three files of `infra/bootstrap/` (Cloud Shell, "Manage files", Upload) into one folder and using that folder below instead of `infra/bootstrap/`.
   - Run it. The `repository` value must be written exactly as GitHub writes it into the token's subject claim: owner and repository each with their numeric id, `AdventureDevelopment@322102914/ThoughtTree@1452386759` (GitHub's default subject since 2026; seen on 2026-11-24, the first sign-in with `AdventureDevelopment/ThoughtTree` was refused with AADSTS700213 and named the expected subject). The ids never change, so a renamed or re-created repository of the same name cannot sign in. To read them: `gh api repos/AdventureDevelopment/ThoughtTree --jq '"\(.owner.login)@\(.owner.id)/\(.name)@\(.id)"'`. The claim is case-sensitive:

     ```bash
     az deployment sub create --name thoughttree-bootstrap --location northeurope \
       --template-file infra/bootstrap/bootstrap.bicep \
       --parameters repository=AdventureDevelopment@322102914/ThoughtTree@1452386759 location=northeurope \
       --query properties.outputs
     ```

   - Regions (owner decision 2026-11-24): resource groups and identities in North Europe (`northeurope`, the bootstrap's default), the three Static Web Apps in East US 2 (`eastus2`, `infra/*.bicepparam`). West Europe, the owner's first choice, refused both on this subscription (`RequestDisallowedByAzure`, 2026-11-20 for identities, 2026-11-24 for Static Web Apps). Static Web Apps exist only in Central US, East US 2, West US 2, East Asia and West Europe; the region holds the site resource and its settings, while the files are served from Microsoft's worldwide network and the app stores no user data in Azure. To move back to West Europe once Azure allows it (support request at aka.ms/locationineligible): change `location` in the three parameter files; the sites are re-created with new addresses.
   - If Azure answers `RequestDisallowedByAzure` ("The selected region is currently not accepting new customers", seen for West Europe on 2026-11-20), add `location=<region>` to `--parameters` and use the same region for `--location`, for example `northeurope`, `swedencentral`, `germanywestcentral` or `francecentral`. The region of the identities and resource groups does not matter for the app. Resource groups that already exist in another region must be deleted first (a group's region cannot change).
   - **Before every re-run:** the bootstrap runs with your Owner rights whatever `main` holds; read `git log -p infra/bootstrap/` since your last run and run it only if every change there is one you approved.
   - The output lists five values: `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_CLIENT_ID_TEST`, `AZURE_CLIENT_ID_PRODUCTION`, `AZURE_CLIENT_ID_PREVIEW`. They are identifiers, not secrets. Running the bootstrap again changes nothing that already exists.
   - Check: `az identity federated-credential list --resource-group rg-thoughttree-identity --identity-name id-thoughttree-production --query "[].subject"` shows exactly one subject, `repo:AdventureDevelopment@322102914/ThoughtTree@1452386759:environment:production`; the same for `test` and `preview`. Repeat this check now and then; any other entry is an incident.
2. **Create the three GitHub environments.** Repository › Settings › Environments › New environment. For each: Deployment branches and tags › Selected branches and tags › add `main` only (no other branch or tag pattern).
   - `production` in addition: Required reviewers › add **`ProductOwnerRole`** (not the account the agents use, `AdventureDevelopment`); tick **Prevent self-review**; untick **Allow administrators to bypass configured protection rules**.
   - Environment variables (Settings › Environments › the environment › Add environment variable; not secrets): `AZURE_CLIENT_ID` = `AZURE_CLIENT_ID_TEST` in `test`, `AZURE_CLIENT_ID_PRODUCTION` in `production`, `AZURE_CLIENT_ID_PREVIEW` in `preview`.
3. **Add two repository variables.** Settings › Secrets and variables › Actions › Variables › New repository variable: `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID`. Delete any older repository variables `AZURE_CLIENT_ID_TEST`, `AZURE_CLIENT_ID_PRODUCTION` and `AZURE_CLIENT_ID_PREVIEW`.
4. **Check the GitHub settings** an agent cannot see:
   - The agents' token (fine-grained token of `AdventureDevelopment`): no Actions write (cannot start `deploy-production` or re-run jobs), no Deployments write (cannot approve), no Administration, Environments, Variables or Secrets write.
   - Settings › Actions › General: fork pull requests need approval for all outside collaborators; workflow permissions "Read repository contents"; "Allow GitHub Actions to create and approve pull requests" off.
   - The OIDC subject claim template of the repository and the organisation is the default (`GET /repos/AdventureDevelopment/ThoughtTree/actions/oidc/customization/sub` returns `use_default: true`); a custom template changes the subjects above.
   - A tag ruleset (Settings › Rules › Rulesets › New tag ruleset, target all tags): only the owner may create, update or delete tags; no bypass for the agents' account. A run started by a tag reports the tag's name as its branch; `site_artifact.py` refuses promotion when a tag named `main` exists or the run's commit is not on `main`, and the ruleset keeps such tags from being made at all.
   - Branch protection of `main` as decided on 2026-11-17, with no bypass for administrators (the agents' account is an administrator), and "Require review from Code Owners" (CODEOWNERS covers `infra/`, `eng/deploy/` and `.github/workflows/`).
   - No repository or environment secret named `AZURE_STATIC_WEB_APPS_API_TOKEN_…` exists.
5. **First deployment.** The next merge to `main` creates the test site and prints its address and the promotable run id in the run summary of `deploy-test`. The first pull request after that creates the preview site.
6. **Test whether one deployment token reaches every environment of a site** (Microsoft's documentation does not say; the design assumes it does, which is why previews have their own site). After the first preview exists, in the portal: `stapp-thoughttree-preview` › Overview › Manage deployment token: note that there is one token for the site and none per environment. Then, in Cloud Shell, while fewer than three previews exist: `T=$(az staticwebapp secrets list --name stapp-thoughttree-preview --resource-group rg-thoughttree-preview --query properties.apiKey --output tsv)`, make a folder with an `index.html`, run `SWA_CLI_DEPLOYMENT_TOKEN="$T" npx --yes @azure/static-web-apps-cli@2.1.0 deploy <folder> --env pr999999`, and check under Environments that `pr999999` appeared (the token reached an environment it was not issued for); delete it again, `unset T`, and press Reset token on the Manage deployment token page. If a staging token could be limited to one environment, record it as an open decision in `docs/architecture.md`; until then the preview site's token reaches all previews and the preview site's own main slot, never the test or production site.
7. **Check logging** (ADR-030, ADR-046): Static Web Apps on the Free plan offers no log or diagnostic setting; check in the portal (site › Monitoring, Diagnostic settings) that nothing stores visitor IP addresses in a resource you control.

## Before the first production deployment (release 1)

Go through this before `deploy-production` runs for the first time, and again before each release:

- **No token with rights on ThoughtTree outside this project.** A classic personal access token reaches every repository of its account, with admin rights where the account is an administrator. The classic token that another development project uses (noted 2026-11-20) must be replaced there by a fine-grained token limited to that project's repositories, then deleted (Developer settings › Personal access tokens › Tokens (classic)). If that project needs a classic token, it comes from an account without access to ThoughtTree.
- The Claude GitHub App is installed for selected repositories only (Settings › Applications › Installed GitHub Apps › Claude).
- Steps 2 to 7 above are done, and `deploy-test` has run green at least once.

## Removing

Delete the resource groups `rg-thoughttree-test`, `rg-thoughttree-prod`, `rg-thoughttree-preview` and `rg-thoughttree-identity` (sites and identities go with them), then the custom role "ThoughtTree site deployer" (Subscription › Access control › Roles), the three GitHub environments and the two repository variables.
