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

token=$(curl --silent --show-error --fail \
  --header "Authorization: bearer ${ACTIONS_ID_TOKEN_REQUEST_TOKEN}" \
  "${ACTIONS_ID_TOKEN_REQUEST_URL}&audience=api://AzureADTokenExchange" | jq --raw-output '.value')
echo "::add-mask::${token}"

az login --service-principal --username "$AZURE_CLIENT_ID" --tenant "$AZURE_TENANT_ID" \
  --federated-token "$token" --allow-no-subscriptions --output none
az account set --subscription "$AZURE_SUBSCRIPTION_ID"
echo "Signed in to Azure as deployment identity ${AZURE_CLIENT_ID}"
