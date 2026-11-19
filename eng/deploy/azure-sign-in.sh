#!/usr/bin/env bash
# Signs in to Azure as one deployment identity through OpenID Connect federation (ADR-046): GitHub issues a
# short-lived token for this job and Azure exchanges it; no secret exists or is stored. The job needs
# `permissions: id-token: write`; AZURE_CLIENT_ID, AZURE_TENANT_ID and AZURE_SUBSCRIPTION_ID come from
# repository variables (identifiers, not secrets; infra/README.md).
set -euo pipefail

: "${ACTIONS_ID_TOKEN_REQUEST_URL:?the job needs permissions: id-token: write}"
: "${ACTIONS_ID_TOKEN_REQUEST_TOKEN:?the job needs permissions: id-token: write}"
: "${AZURE_CLIENT_ID:?repository variable missing, see infra/README.md}"
: "${AZURE_TENANT_ID:?repository variable missing, see infra/README.md}"
: "${AZURE_SUBSCRIPTION_ID:?repository variable missing, see infra/README.md}"

# Neither the request token nor the OpenID Connect token goes on a command line, where every process of the job
# could read it in /proc: both go through files only this user can read; curl reads its header with @file and
# the Azure CLI loads an argument value from a file with @file. Shell builtins (printf, echo) start no process.
umask 077
secrets_dir=$(mktemp -d)
trap 'rm -rf "$secrets_dir"' EXIT
printf 'Authorization: bearer %s\n' "$ACTIONS_ID_TOKEN_REQUEST_TOKEN" > "$secrets_dir/header"
curl --silent --show-error --fail --header "@$secrets_dir/header" \
  "${ACTIONS_ID_TOKEN_REQUEST_URL}&audience=api://AzureADTokenExchange" | jq --raw-output --exit-status '.value' > "$secrets_dir/token"
echo "::add-mask::$(<"$secrets_dir/token")"

az login --service-principal --username "$AZURE_CLIENT_ID" --tenant "$AZURE_TENANT_ID" \
  --federated-token "@$secrets_dir/token" --allow-no-subscriptions --output none
rm -rf "$secrets_dir"
az account set --subscription "$AZURE_SUBSCRIPTION_ID"
echo "Signed in to Azure as deployment identity ${AZURE_CLIENT_ID}"
