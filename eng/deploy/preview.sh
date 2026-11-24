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
# When all are taken by open pull requests, deploy fails with a message that names them; nothing waits or loops.
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

prune() {
  # A staging environment is deleted with a plain REST DELETE on its resource, which returns after Azure's 202.
  # "az staticwebapp environment delete" (and "az resource delete") then poll the operation status at
  # /subscriptions/<id>/providers/Microsoft.Web/locations/<region>/staticSitesOperationStatuses/<id>: a
  # subscription-scope resource the "ThoughtTree site deployer" role, assigned on rg-thoughttree-preview only,
  # cannot read (preview-deploy run 45420685246, 2026-11-24). Instead of widening the role, this waits until the
  # environment list, readable on the resource group, no longer shows the deleted previews: bounded, and it fails
  # with a message naming what is left.
  local removed=() site_id remaining
  for preview in $(previews); do
    state=$(python3 -I eng/deploy/site_artifact.py pull-request-state "${preview#pr}")
    if [ "$state" != open ]; then
      if [ -z "${site_id:-}" ]; then
        site_id=$(az staticwebapp show --name "$site" --resource-group "$group" --query id --output tsv)
        [[ "$site_id" =~ ^/subscriptions/[0-9a-f-]{36}/resource[Gg]roups/$group/providers/Microsoft\.Web/static[Ss]ites/$site$ ]] \
          || { echo "::error::unexpected id of the preview site: $site_id" >&2; exit 1; }
      fi
      echo "Removing preview $preview (pull request ${preview#pr} is $state)"
      az rest --method delete --url "$site_id/builds/$preview?api-version=2024-04-01" --output none
      removed+=("$preview")
    fi
  done
  [ "${#removed[@]}" -eq 0 ] && return 0
  for _ in $(seq 1 18); do
    remaining=$(previews | grep -Fx -f <(printf '%s\n' "${removed[@]}") || true)
    [ -z "$remaining" ] && return 0
    sleep 10
  done
  echo "::error::Previews still being deleted after 3 minutes: $(tr '\n' ' ' <<<"$remaining"). Re-run this workflow later." >&2
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
