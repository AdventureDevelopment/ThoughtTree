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
  az staticwebapp environment list --name "$site" --resource-group "$group" --query "[].name" --output tsv \
    | grep -E '^pr[0-9]+$' || true
}

prune() {
  for preview in $(previews); do
    state=$(python3 -I eng/deploy/site_artifact.py pull-request-state "${preview#pr}")
    if [ "$state" != open ]; then
      echo "Removing preview $preview (pull request ${preview#pr} is $state)"
      az staticwebapp environment delete --name "$site" --resource-group "$group" \
        --environment-name "$preview" --yes --output none
    fi
  done
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
