#!/usr/bin/env bash
# Uploads a published site to one environment of a Static Web App (ADR-046) and checks its headers (ADR-045).
# Needs eng/deploy/install-tools.sh to have run (before the sign-in) and an Azure sign-in as the site's identity.
#
# The deployment token is read from Azure for this run only and masked. Then the Azure CLI signs out and its
# cache is removed, so the deploy tools run with ONLY the deployment token, which they get through the
# environment variable SWA_CLI_DEPLOYMENT_TOKEN (never on a command line), with the job's OpenID Connect request
# variables removed. The CLI runs in an empty folder, so no swa-cli.config.json is picked up.
#
# StaticSitesClient is pinned (eng/deploy/deploy-client.json). The CLI still compares it with Microsoft's
# metadata and downloads another build if no tag (stable, latest, backup) names the pinned one; so this script
# picks the tag that names the pinned build with the pinned SHA-256, fails if none does, and checks afterwards
# that no other client was downloaded.
#
# Usage: eng/deploy/upload-site.sh <resource-group> <site> <folder> <environment>
#   <environment> is "production" for the site itself (swa deploy --env production), or a preview name such as
#   pr12 (a staging environment).
set -euo pipefail
deploy="$(cd "$(dirname "$0")" && pwd)"

group="${1:?resource group}"
site="${2:?site name}"
folder="$(cd "${3:?published folder}" && pwd)"
environment="${4:?environment}"
[[ "$environment" =~ ^(production|pr[1-9][0-9]{0,9})$ ]] || { echo "unknown environment: $environment" >&2; exit 2; }

build_id=$(jq -er .buildId "$deploy/deploy-client.json")
sha256=$(jq -er .sha256 "$deploy/deploy-client.json")
client="$HOME/.swa/deploy/$build_id/StaticSitesClient"
echo "$sha256  $client" | sha256sum --check --strict --quiet

metadata=$(curl --silent --show-error --fail --location --proto '=https' https://aka.ms/swalocaldeploy)
tag=$(jq -r --arg id "$build_id" --arg sha "$sha256" \
  '[.[] | select(.buildId == $id and (.files["linux-x64"].sha | ascii_downcase) == $sha) | .version] | first // empty' <<<"$metadata")
if [ -z "$tag" ]; then
  echo "::error::Microsoft's deploy client metadata no longer names the pinned StaticSitesClient $build_id; update eng/deploy/deploy-client.json (steps in its comment) in a pull request." >&2
  exit 1
fi
jq --arg t "$tag" --arg sha "$sha256" --arg bin "$client" \
  '{metadata: (.[] | select(.version == $t)), binary: $bin, checksum: $sha}' <<<"$metadata" > "$HOME/.swa/deploy/StaticSitesClient.json"

if [ "$environment" = production ]; then
  host=$(az staticwebapp show --name "$site" --resource-group "$group" --query defaultHostname --output tsv)
fi
token=$(az staticwebapp secrets list --name "$site" --resource-group "$group" --query properties.apiKey --output tsv)
echo "::add-mask::${token}"

az logout --output none 2>/dev/null || true
az account clear --output none 2>/dev/null || true
rm -rf "$HOME/.azure"

workdir=$(mktemp -d)
(
  cd "$workdir"
  unset ACTIONS_ID_TOKEN_REQUEST_URL ACTIONS_ID_TOKEN_REQUEST_TOKEN
  export SWA_CLI_DEPLOYMENT_TOKEN="$token" SWA_CLI_DEPLOY_BINARY_VERSION="$tag" NO_UPDATE_NOTIFIER=1
  "$deploy/node_modules/.bin/swa" deploy "$folder" --env "$environment" --no-use-keychain 2>&1 | tee deploy.log
)
unset token

echo "$sha256  $client" | sha256sum --check --strict --quiet
others=$(find "$HOME/.swa/deploy" -mindepth 1 -maxdepth 1 -type d ! -name "$build_id")
[ -z "$others" ] || { echo "::error::the CLI downloaded another deploy client: $others" >&2; exit 1; }

# The address of a staging environment is known only after the upload; take it from the CLI's output.
if [ "$environment" != production ]; then
  host=$(grep -oE "https://[a-z0-9.-]+\.azurestaticapps\.net" "$workdir/deploy.log" | tail -n 1 | sed 's|^https://||' || true)
fi
if [ -z "${host:-}" ]; then
  echo "::error::the address of $environment is not known, so its headers cannot be checked" >&2
  exit 1
fi
echo "Deployed to https://${host}"
"$deploy/check-headers.sh" "https://${host}"

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  echo "Deployed to https://${host}" >> "$GITHUB_STEP_SUMMARY"
fi
