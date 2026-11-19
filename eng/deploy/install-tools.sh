#!/usr/bin/env bash
# Installs the deploy tools BEFORE any Azure sign-in (ADR-046, ADR-050):
# - the Static Web Apps CLI exactly as eng/deploy/package-lock.json states (npm ci checks every sha512
#   integrity hash in the lock file), without install scripts, then verifies the registry signatures;
# - StaticSitesClient, the deploy client the CLI runs, pinned by SHA-256 in eng/deploy/deploy-client.json,
#   placed where the CLI looks for it (~/.swa/deploy) so the CLI uses this file instead of downloading one.
# Usage: eng/deploy/install-tools.sh
set -euo pipefail
cd "$(dirname "$0")"

export NO_UPDATE_NOTIFIER=1
npm ci --ignore-scripts --no-audit --no-fund
npm audit signatures

build_id=$(jq -er '.buildId | select(test("^[0-9a-f]{40}$"))' deploy-client.json)
url=$(jq -er '.url | select(startswith("https://"))' deploy-client.json)
sha256=$(jq -er '.sha256 | select(test("^[0-9a-f]{64}$"))' deploy-client.json)

folder="$HOME/.swa/deploy/$build_id"
mkdir -p "$folder"
curl --silent --show-error --fail --location --proto '=https' --output "$folder/StaticSitesClient" "$url"
echo "$sha256  $folder/StaticSitesClient" | sha256sum --check --strict
chmod 0555 "$folder/StaticSitesClient"
echo "StaticSitesClient $build_id checked against the pinned SHA-256"
