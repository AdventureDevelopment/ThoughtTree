#!/usr/bin/env bash
# Pull request previews on their own Static Web App, stapp-thoughttree-preview in rg-thoughttree-preview, which
# shares nothing with the test or production site (its deployment token reaches only previews). Runs from main
# in the workflow preview-deploy.yml, never with pull request code; the site to upload is an artifact that
# eng/deploy/site_artifact.py has fetched and checked as data.
#
#   preview.sh prune                       remove the previews of closed pull requests
#   preview.sh deploy <number> <folder>    prune, then upload <folder> as preview pr<number>
#
# The Free plan allows 3 staging environments per site (learn.microsoft.com/azure/static-web-apps/plans).
# When all are taken by open pull requests, deploy fails with a message that names them; it never waits for a
# free slot. Prune waits (bounded) only to see deleted previews leave the list.
set -euo pipefail
cd "$(dirname "$0")/../.."

group=rg-thoughttree-preview
site=stapp-thoughttree-preview
limit=3

previews() {
  local names
  names=$(az staticwebapp environment list --name "$site" --resource-group "$group" --query "[].name" --output tsv)
  grep -E '^pr[0-9]+$' <<<"$names" || true
}

# The one error a delete may end with (observed 2026-11-24 for pr13, preview-deploy run 45420685246): Azure
# accepts the DELETE (staticSites/builds/delete, in the role), then Azure CLI polls the operation status at
# /subscriptions/<id>/providers/Microsoft.Web/locations/<region>/staticSitesOperationStatuses/<id>, a
# subscription-scope resource the "ThoughtTree site deployer" role (resource group only) cannot read, and fails
# with AuthorizationFailed. Only that exact action, on a scope of that shape and with no other action named, is
# tolerated; whether the environment is really gone is then checked on the environment list. Any other error,
# including AuthorizationFailed for another action (such as builds/delete), stops the script with its message.
known_polling_error() {
  local error="$1" actions
  grep -qE '\(AuthorizationFailed\)|Code: AuthorizationFailed' <<<"$error" || return 1
  actions=$(grep -oE "perform action '[^']*'" <<<"$error" | sort -u)
  [ "$actions" = "perform action 'Microsoft.Web/locations/staticSitesOperationStatuses/read'" ] || return 1
  grep -qE "over scope '/subscriptions/[0-9a-f-]{36}/providers/Microsoft\.Web/locations/[a-z0-9]+/staticSitesOperationStatuses/[0-9a-zA-Z-]+'" <<<"$error"
}

delete_preview() {
  local error
  if error=$(az staticwebapp environment delete --name "$site" --resource-group "$group" \
      --environment-name "$1" --yes --output none 2>&1); then
    return 0
  fi
  if known_polling_error "$error"; then
    echo "::notice::$1: Azure accepted the delete; Azure CLI could not read the operation status at subscription scope (expected, see infra/README.md); checking the environment list instead"
    return 0
  fi
  printf '%s\n' "$error" >&2
  echo "::error::Deleting preview $1 failed" >&2
  exit 1
}

# How long prune waits for deleted previews to leave the list: 18 checks 10 seconds apart (3 minutes). Only the
# tests in tests/secure/.../Deployment/preview_prune_tests.py shorten it; a shorter wait can only fail sooner,
# and values outside 1..18 checks and 0..10 seconds are refused.
wait_checks="${PREVIEW_PRUNE_WAIT_CHECKS:-18}"
wait_seconds="${PREVIEW_PRUNE_WAIT_SECONDS:-10}"
[[ "$wait_checks" =~ ^([1-9]|1[0-8])$ && "$wait_seconds" =~ ^([0-9]|10)$ ]] \
  || { echo "::error::PREVIEW_PRUNE_WAIT_CHECKS must be 1..18 and PREVIEW_PRUNE_WAIT_SECONDS 0..10" >&2; exit 2; }

prune() {
  local removed=() remaining=""
  for preview in $(previews); do
    state=$(python3 -I eng/deploy/site_artifact.py pull-request-state "${preview#pr}")
    if [ "$state" != open ]; then
      echo "Removing preview $preview (pull request ${preview#pr} is $state)"
      delete_preview "$preview"
      removed+=("$preview")
    fi
  done
  [ "${#removed[@]}" -eq 0 ] && return 0
  for _ in $(seq 1 "$wait_checks"); do
    remaining=$(previews | grep -Fx -f <(printf '%s\n' "${removed[@]}") || true)
    [ -z "$remaining" ] && return 0
    sleep "$wait_seconds"
  done
  echo "::error::Previews still listed after the wait: $(tr '\n' ' ' <<<"$remaining"). The delete may not have gone through; check stapp-thoughttree-preview > Environments and re-run." >&2
  exit 1
}

case "${1:-}" in
  prune)
    prune
    ;;
  deploy)
    number="${2:?pull request number}"
    folder="${3:?published folder}"
    [[ "$number" =~ ^[1-9][0-9]{0,9}$ ]] || { echo "not a pull request number: $number" >&2; exit 2; }
    prune
    existing=$(previews)
    # Runs for different pull requests may overlap (one concurrency group per branch); if two pass this check
    # together, the fourth preview fails in Azure instead of here.
    if ! grep -qx "pr$number" <<<"$existing"; then
      count=$(grep -c . <<<"$existing" || true)
      if [ "$count" -ge "$limit" ]; then
        echo "::error::The preview site holds $count previews ($(tr '\n' ' ' <<<"$existing")), the limit of the Free plan. Close or merge one of these pull requests, or delete a preview by hand (infra/README.md); then re-run this workflow."
        exit 1
      fi
    fi
    eng/deploy/upload-site.sh "$group" "$site" "$folder" "pr$number"
    ;;
  *)
    echo "usage: preview.sh prune | deploy <number> <folder>" >&2
    exit 2
    ;;
esac
