# Security review: Azure deployment pipeline (branch `claude/azure-pipeline`), 2026-11-19

Independent review of `git diff origin/main...HEAD` on `claude/azure-pipeline` (head `0dc3f8f`, 20 files), against AGENTS.md, ADR-030, ADR-035, ADR-045, ADR-046, ADR-050 and `docs/architecture.md` section 7. The reviewer did not write the code and did not read the author's notes. No file under review was changed.

Severity: critical, high, medium, low, info. Confidence: confirmed (by reading the code, or checked against an upstream source), probable (follows from documented platform behaviour not tested here), speculative.

Summary: 0 critical, 3 high, 3 medium, 3 low, 6 info.

## Findings

### H1 (high, confirmed): every pull request run of a branch in this repository can read the test site's deployment token and overwrite the test site

- Location: `.github/workflows/preview.yml:28-63` (job `deploy`) and `:65-89` (job `remove`); `infra/bootstrap/bootstrap.bicep:38-58` (custom role with `Microsoft.Web/staticSites/listSecrets/action`) and `:83-92` (subject `repo:<repo>:pull_request`); `eng/deploy/upload-site.sh:15-19`.
- What happens: the `deploy` job has `id-token: write`, runs on `pull_request`, and executes code from the pull request's merge ref before and after signing in: `dotnet publish` of the pull request's projects (MSBuild targets, `Directory.Build.props`, source generators and analyzers run arbitrary code), and the pull request's own versions of `eng/deploy/*.sh` and of `preview.yml` itself (for `pull_request` events the workflow file is taken from the merge ref). Doing the build before the sign-in step does not help: the pull request controls the scripts, and a process started during the build can outlive its step and wait for `~/.azure` or for `ACTIONS_ID_TOKEN_REQUEST_*`, which are in the job environment from the start.
- Attack path: an agent (prompt-injected through a feedback report or user content, or with a stolen agent token; agents push with Contents write as the admin account `AdventureDevelopment`) opens a pull request that changes `eng/deploy/upload-site.sh` to deploy with `--env production`, or prints the token base64-encoded (`::add-mask::` hides only the literal value). Result: (a) arbitrary content and an arbitrary `staticwebapp.config.json` (CSP removed) on the main slot of the test site, which the owner opens on their devices and where test data lives in the origin's IndexedDB; (b) the exfiltrated deployment token stays valid until it is reset, so the attacker keeps write access after the pull request is closed. No review is needed: the run starts on `opened`/`synchronize`.
- Rules broken: `docs/architecture.md` line 347 ("agents never see deployment credentials"), the `why` of ADR-046 ("no credentials an agent could see or leak"), and the owner's 2026-11-16 rule recorded for the reviewer agent ("never checks out or runs pull request code with the credential", architecture.md line 975). `infra/README.md:17` and `bootstrap.bicep:8-9` document the token access as known, but the architecture.md row changed by this branch (line 978) says only "the deployment token is read from Azure per run and never stored", so the docs now contradict each other and the accepted risk is not an owner decision on record.
- Minimal fix: split build and deployment. (1) `pull_request` job without `id-token`: build, upload the `wwwroot` folder as an artifact. (2) A `workflow_run` workflow (runs from `main`, never checks out pull request code) downloads the artifact as data, validates the pull request number taken from the event (not from the artifact), signs in and uploads. (3) Deploy previews to a separate Static Web App (`stapp-thoughttree-preview`, its own resource group or at least its own token), so the preview token cannot write the test site's main slot; the deployment token is per site, not per environment, so the custom role cannot narrow it. (4) Reset the preview site's token periodically (`az staticwebapp secrets reset-api-key`, needs a role the preview identity does not get). (5) Add an architecture rule: no job that runs on `pull_request` has `id-token: write`. Until then, do not set `AZURE_CLIENT_ID_PREVIEW`.

### H2 (high, confirmed by reading; Azure behaviour probable): the test and production identities can give themselves new federated credentials and bypass the production approval

- Location: `infra/bootstrap/bootstrap.bicep:18, 61-81` (built-in Contributor at resource-group scope) and `infra/bootstrap/deploy-identity.bicep:16-43` (the managed identity and its federated credential live in the same resource group as the site).
- What happens: Contributor allows `Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials/write` (its `notActions` exclude only `Microsoft.Authorization/*/Write` and `/Delete` and a few others). `id-thoughttree-production` is Contributor on `rg-thoughttree-prod`, where it lives itself; `id-thoughttree-test` is Contributor on `rg-thoughttree-test`, where `id-thoughttree-preview` and itself live.
- Attack path: a change to `infra/main.bicep` adds a `federatedIdentityCredentials` child to the existing `id-thoughttree-production` with subject `repo:<repo>:pull_request`, `repo:<repo>:ref:refs/heads/<any>` or `repo:<attacker>/<repo>:...` (any repository on github.com). Once one approved production run applies it, every later run with that subject is Contributor on production without approval, and with an outside repository the access survives deleting the branch. The approver sees only "deploy-production" and a commit id. For the test site the same happens with no approval at all on the next merge to `main`. `infra/**` and `eng/deploy/**` are not in `.github/CODEOWNERS` (only `/.github/` is, as an interim entry to be removed when the reviewer agent exists), so these changes do not need the code owner's review.
- Minimal fix: move the three identities to their own resource group (for example `rg-thoughttree-identity`) in which no deployment identity has any role; replace Contributor with a custom role limited to `Microsoft.Web/staticSites/*` and `Microsoft.Resources/deployments/*` on the site's group. Add `/infra/` and `/eng/deploy/` to `.github/CODEOWNERS` permanently (protected-path list in ADR-035 is the owner's change).

### H3 (high, probable): the production job runs an unpinned npm dependency tree and an unpinned binary while signed in to Azure

- Location: `eng/deploy/upload-site.sh:15-19`, called after `eng/deploy/azure-sign-in.sh` in `.github/workflows/deploy-production.yml:55-62` and `deploy-test.yml:55-62`.
- What happens: `npx --yes @azure/static-web-apps-cli@2.1.0` pins only the top package. Checked with `npm view`: it has 37 direct dependencies, all with caret ranges (`keytar`, `devcert`, `@azure/identity`, `openid-client`, `update-notifier`, ...), so a few hundred transitive packages are resolved fresh on every run, with install scripts enabled (`keytar` builds or downloads a native module). The CLI then downloads `StaticSitesClient` at tag `stable` from `https://aka.ms/swalocaldeploy` and compares it with a checksum from the same metadata response (`dist/core/deploy-client.js:23-40, 85-86` in 2.1.0): this detects corruption, not a malicious release, and the version floats. All of this runs with the Azure CLI signed in as Contributor on `rg-thoughttree-prod` (`~/.azure`), with the deployment token in `SWA_CLI_DEPLOYMENT_TOKEN`, and with write access to the already published `wwwroot`.
- Attack path: one compromised version of any transitive npm package (2025 saw several self-spreading npm compromises) is pulled in on the next production run after approval, changes the app before upload or uses the Azure session (and, with H2, adds a federated credential for persistence).
- Minimal fix: commit `eng/deploy/package.json` and `package-lock.json` (integrity hashes) and install with `npm ci --ignore-scripts`; read the token, then `az logout` before starting the CLI; pin `StaticSitesClient` with `DEPLOY_BINARY_VERSION` (supported by 2.1.0, `constants.js`) and an independently recorded SHA-256. The lock file also brings the tool under the ADR-050 licence check (`**/package.json`).

### M1 (medium, confirmed): production does not deploy exactly what was tested, and there is no rollback path

- Location: `.github/workflows/deploy-production.yml:6-7, 22-62`.
- What happens: production rebuilds from whatever `main` points to when the run is started. Nothing checks that `deploy-test` succeeded for that commit, nor that the bits equal the tested ones (they are rebuilt, and the deploy tooling of H3 is resolved anew). A merge landing between the test deployment and the dispatch goes to production untested on the test site. The header check in `upload-site.sh:29-34` runs after the content is live, so a failing check leaves the bad version in production. Rolling back needs a revert pull request through the full gate.
- Minimal fix: `deploy-test.yml` uploads the published `wwwroot` as an artifact; `deploy-production.yml` takes a `run_id` (or commit id) input, verifies with the GitHub API that this `deploy-test` run succeeded on `main`, downloads that artifact and uploads it. Rollback is the same workflow with an older run id. Show the commit id in the run name so the approver sees it.

### M2 (medium, confirmed): the new architecture rules can pass while the rules they secure are broken

- Location: `tests/secure/ThoughtTree.Architecture.Rules/Deployment/InfrastructureRules.cs` and `SecurityHeaderRules.cs`.
  - `InfrastructureRules.cs:15`: only `*.yml`; a workflow named `*.yaml` escapes every rule (no `pull_request_target` check, no pinning, no secret check).
  - `:66-80` and `:193`: `AutomaticTrigger` matches only block style with two spaces; `on: push`, `on: [push, workflow_dispatch]`, and the triggers `workflow_call`, `repository_dispatch`, `workflow_run` in flow style, `deployment`, `check_suite` pass. `ShouldContain("workflow_dispatch:")` and `ShouldContain("environment: production")` are satisfied by a comment.
  - `:169`: `secrets['NAME']` and `toJSON(secrets)` are not found.
  - `:175-179`: `permissions: write-all` (top level or job level) is not found; no rule stops `id-token: write` on a job that runs on `pull_request` (the cause of H1).
  - `:91`: `environmentName ==` without the space, or `== environmentName`, passes.
  - `:199-203`: other imperative creations (`az resource create`, `az rest --method put`, `az deployment ... --template-file <other>`) pass.
  - `SecurityHeaderRules.cs:271-276`: only `globalHeaders` is read. Static Web Apps lets `routes[].headers` set or override headers per route, so a route such as `{"route": "/*", "headers": {"Content-Security-Policy": "default-src *"}}` passes all four tests.
  - No rule checks the ADR-046 requirement "logging records no IP addresses" although its verify line says "IP logging off" (see L1).
- Minimal fix: parse the workflows as YAML (a YAML parser in the test project, licence check under ADR-050) and assert on the parsed `on`, `permissions`, `jobs.*.environment` and `uses` values; scan `*.yml` and `*.yaml`; fail on any `routes`, `responseOverrides` or second spelling of a security header in `staticwebapp.config.json` unless it repeats the global value.

### M3 (medium, probable): the approval for production rests on GitHub settings that the agents' own admin account can bypass

- Location: `infra/README.md:53-56`; `deploy-production.yml:33`; owner decision "Agent identity for pull requests" (architecture.md line 976: agents act as `AdventureDevelopment`, admin).
- What happens: README step 2 says "Required reviewers, add yourself". If the owner sets this up from `AdventureDevelopment`, the reviewer is the account agents act as. GitHub environments by default let administrators bypass protection rules ("Allow administrators to bypass configured protection rules"), and "Prevent self-review" is off by default. The fine-grained agent token as described (Contents and Pull requests write) should not be able to dispatch workflows or approve deployments (needs Actions and Deployments write), but nothing in the repository checks that.
- Minimal fix: in README step 2 name `ProductOwnerRole` as the required reviewer, turn on "Prevent self-review", turn off administrator bypass, restrict deployment branches to `main` for both environments, and move `AZURE_CLIENT_ID_TEST` and `AZURE_CLIENT_ID_PRODUCTION` from repository variables to environment variables.

### L1 (low, confirmed): ADR-046 "logging records no IP addresses" is neither configured nor verified

- Location: `infra/main.bicep:20-35`; ADR-046 rule 6 and verify line.
- The Free plan deploys no customer-visible logs and the template adds no diagnostics, which fits the rule today, but the rule asks the template to configure it and the verify line claims a check that does not exist. Fix: state in `main.bicep` why nothing is configured (no log sink on the Free plan), and add a test that no `diagnosticSettings`, Application Insights or log analytics resource or `logging` property appears (partly there in `No_telemetry_or_logging_service_is_deployed`, which only looks at resource type names).

### L2 (low, probable): the built-in Static Web Apps sign-in endpoints stay reachable

- Location: `src/ThoughtTree.Client/wwwroot/staticwebapp.config.json` (no `routes`).
- Static Web Apps enables pre-configured GitHub and Microsoft Entra ID providers under `/.auth/login/*` on every site. The app does not use them; a crafted link to `/.auth/login/github` sends users to a third-party sign-in on the app's own domain. Fix: routes `/.auth/*` with `statusCode: 404` (documented way to disable the providers), and a rule test that keeps them.

### L3 (low, confirmed): the OpenID Connect token is passed on the command line

- Location: `eng/deploy/azure-sign-in.sh:19-20`. `--federated-token "$token"` is visible in `/proc/*/cmdline` to every process of the job (in the preview job these are pull request processes, see H1). Short-lived (minutes) and single-audience, so low. Fix: run the sign-in in a job step that no pull request code precedes (follows from H1's fix).

### Info

- I1 (confirmed): all action pins match their release tags, checked with `git ls-remote` against GitHub: `actions/checkout` v7.0.1 = `3d3c42e5…`, `actions/setup-dotnet` v6.0.0 = `a98b5685…`, `actions/upload-artifact` v7.0.2 = `cf430e03…`. Bicep tag `v0.49.2` exists (`b34271ac…`); `az bicep install` downloads it without a hash pin, in a job without credentials.
- I2 (confirmed): no script injection: no `${{ … }}` with branch names, titles or inputs inside `run:`; `PREVIEW_NAME` uses the pull request number (an integer). No `pull_request_target`, no `workflow_run`. All workflows declare `permissions: contents: read` at top level; `id-token: write` only per job; `persist-credentials: false` everywhere.
- I3 (confirmed): forks: the preview jobs skip pull requests whose head repository is not this one; a fork could edit the `if`, but GitHub issues no OIDC token to `pull_request` runs from forks of a public repository, and a fork's `environment:` jobs fail the branch policy. No cache crosses a trust boundary: only `checks.yml` uses the setup-dotnet cache, and pull request caches are not readable from `main`; deploy jobs use none. Restore in the deploy jobs runs in locked mode (`Directory.Build.props:23`, `CI=true` on GitHub runners).
- I4 (confirmed): CSP in `staticwebapp.config.json` meets ADR-045: own origin only, `object-src` and `frame-ancestors` `'none'`, no `unsafe-inline`/`unsafe-eval`, only `'wasm-unsafe-eval'`. The placeholder fails closed (an unfilled policy blocks the import map). `publish-client.sh` rejects any other inline `<script>` in `index.html`. Not present and not required by ADR-045: `Strict-Transport-Security` (the platform sends it, not verified), `Permissions-Policy`, `Cross-Origin-Opener-Policy`, `require-trusted-types-for 'script'`; worth considering. No service worker exists yet, so its caching and update behaviour could not be reviewed; when it comes, `index.html` and the worker script need `Cache-Control: no-cache`.
- I5 (confirmed): licences: `@azure/static-web-apps-cli` 2.1.0 is MIT (npm metadata) and contains no telemetry code (searched the 2.1.0 package; it contacts the npm registry for update notices). `StaticSitesClient`, which it downloads, is a closed-source Microsoft binary under Microsoft terms, not on the ADR-050 list; ADR-050's `applies_to` does not cover tools run through `npx`, so this needs an explicit owner approval or an ADR note. Its own telemetry was not checked.
- I6 (confirmed): `azurestaticapps.net` and its regional subdomains are on the Public Suffix List, so preview hosts are separate sites from the test site. The Free plan's few staging slots can be used up by agents' open pull requests (availability only). Preview sites are public to anyone with the address.

## Trust boundaries

| Who | Triggers | Code that runs | Credential in the job | Can reach |
| --- | --- | --- | --- | --- |
| Anyone (fork) | `pull_request` (push-gate, infra-check; preview skipped) | Fork's code and workflow files | Read-only `GITHUB_TOKEN`, no OIDC | Nothing in Azure |
| Branch in this repository (agents, `AdventureDevelopment` token) | `push` (branch-check, infra-check) | Branch code | Read-only `GITHUB_TOKEN` | Nothing in Azure, unless an environment's branch policy is missing |
| Branch in this repository, pull request open | `pull_request` (push-gate, infra-check, preview) | Pull request code and its own `preview.yml` and `eng/deploy/*` | OIDC as `id-thoughttree-preview` | Test site deployment token: main slot and all previews of the test site (H1) |
| Merge to `main` | `push` to `main` (deploy-test) | `main` | OIDC as `id-thoughttree-test`, environment `test` | Contributor on `rg-thoughttree-test`, incl. the identities there (H2) |
| Owner (or anyone with Actions write) dispatches, required reviewer approves | `workflow_dispatch` on `main` (deploy-production) | `main` plus npm packages resolved at run time (H3) | OIDC as `id-thoughttree-production`, environment `production` | Contributor on `rg-thoughttree-prod`, incl. its own identity (H2) |
| Owner in Cloud Shell | bootstrap, once | `infra/bootstrap/*` | Owner's Azure session (Owner or User Access Administrator) | Subscription: role definition, role assignments, identities |

## Settings the owner must verify in GitHub and Azure (not visible to an agent)

1. GitHub environment `test`: deployment branches limited to `main`; no other branch or tag pattern.
2. GitHub environment `production`: required reviewer is `ProductOwnerRole` (not `AdventureDevelopment`); "Prevent self-review" on; "Allow administrators to bypass configured protection rules" off; deployment branches limited to `main`.
3. Agent token (`AdventureDevelopment` fine-grained token): no Actions write (cannot dispatch `deploy-production` or re-run jobs), no Deployments write (cannot approve), no Administration, no Variables or Secrets write, no Environments write.
4. Actions settings: "Require approval for all outside collaborators" for fork pull requests; workflow permissions default "Read repository contents"; "Allow GitHub Actions to create and approve pull requests" off.
5. OIDC subject claim template of the repository and organisation is the default (`GET /repos/{owner}/{repo}/actions/oidc/customization/sub`): a customised template changes what the subjects in `bootstrap.bicep` match.
6. Repository name casing in the bootstrap parameter matches GitHub's exactly (the `sub` comparison is case-sensitive).
7. Branch protection on `main` as decided on 2026-11-17, including "no bypass for administrators" (the agent account is admin).
8. Azure: after bootstrap, `az identity federated-credential list` for each of the three identities shows exactly one credential with the expected subject; repeat after every production deployment until H2 is fixed.
9. Azure: the old Static Web App and its token are gone (recorded as done on 2026-11-19) and no repository or environment secret named `AZURE_STATIC_WEB_APPS_API_TOKEN_*` remains.
10. Azure: Static Web Apps platform logs for the Free plan do not store visitor IP addresses in any resource the owner controls (ADR-030, ADR-046; L1).

## Not covered

- No workflow was run, and nothing was deployed; Azure behaviour (Contributor covering federated credential writes, per-site scope of the deployment token, `/.auth` providers enabled by default, HSTS sent by the platform) is taken from Azure documentation, not tested.
- GitHub and Azure settings listed above.
- The `StaticSitesClient` binary itself (contents, telemetry, licence text).
- The Bicep files were not compiled (no Bicep CLI in this session); the architecture tests were read, not run.
- Files outside the diff, except where needed to judge it (`.github/CODEOWNERS`, `Directory.Build.props`, `tests/secure/ThoughtTree.Architecture.Rules/Support/Repository.cs`, the ADRs and `docs/architecture.md`).
