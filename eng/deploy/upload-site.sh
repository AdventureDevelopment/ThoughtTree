#!/usr/bin/env bash
# Uploads a published site to one environment of a Static Web App. The deployment token is read from Azure
# for this run only, masked in the log and handed to the deploy tool through its environment; it is never
# stored (ADR-046). The Static Web Apps CLI is MIT licensed and pinned; it downloads Microsoft's deploy
# client and checks its checksum.
# Usage: eng/deploy/upload-site.sh <resource-group> <site> <folder> <environment>
#   <environment> is "production" for the site itself, or a preview name such as pr12.
set -euo pipefail

group="${1:?resource group}"
site="${2:?site name}"
folder="${3:?published folder}"
environment="${4:?environment}"

token=$(az staticwebapp secrets list --name "$site" --resource-group "$group" --query properties.apiKey --output tsv)
echo "::add-mask::${token}"

SWA_CLI_DEPLOYMENT_TOKEN="$token" npx --yes @azure/static-web-apps-cli@2.1.0 deploy "$folder" \
  --env "$environment" --no-use-keychain

if [ "$environment" = production ]; then
  host=$(az staticwebapp show --name "$site" --resource-group "$group" --query defaultHostname --output tsv)
else
  host=$(az staticwebapp environment list --name "$site" --resource-group "$group" \
    --query "[?name=='${environment}'].hostname | [0]" --output tsv)
fi
echo "Deployed to https://${host}"

# The deployed site must send the policy of ADR-045 with the hash filled in (static check after deployment).
headers=$(curl --silent --show-error --fail --head "https://${host}/")
grep -qi "^content-security-policy: default-src 'self'" <<<"$headers" || { echo "Content-Security-Policy header missing" >&2; exit 1; }
grep -qi "^content-security-policy:.*'sha256-" <<<"$headers" || { echo "import map hash missing in the policy" >&2; exit 1; }
if grep -qi "importmap-hash\|unsafe-inline\|'unsafe-eval'" <<<"$headers"; then echo "policy not filled in or weakened" >&2; exit 1; fi
grep -qi "^referrer-policy: no-referrer" <<<"$headers" || { echo "Referrer-Policy header missing" >&2; exit 1; }

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  echo "Deployed to https://${host}" >> "$GITHUB_STEP_SUMMARY"
fi
