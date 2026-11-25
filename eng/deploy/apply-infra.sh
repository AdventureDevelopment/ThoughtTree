#!/usr/bin/env bash
# Applies infra/main.bicep with the parameters of one environment (ADR-046) and prints the site name.
# Usage: eng/deploy/apply-infra.sh test|production|preview
set -euo pipefail
cd "$(dirname "$0")/../.."

environment="${1:?usage: apply-infra.sh test|production|preview}"
case "$environment" in
  test) group=rg-thoughttree-test ;;
  production) group=rg-thoughttree-prod ;;
  preview) group=rg-thoughttree-preview ;;
  *) echo "unknown environment: $environment" >&2; exit 2 ;;
esac

az deployment group create \
  --resource-group "$group" \
  --name "thoughttree-${environment}-${GITHUB_RUN_ID:-local}" \
  --template-file infra/main.bicep \
  --parameters "infra/${environment}.bicepparam" \
  --query properties.outputs.siteName.value --output tsv
