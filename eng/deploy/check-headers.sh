#!/usr/bin/env bash
# Checks that a deployed site sends the headers of ADR-045 with the import map hash filled in, and that the
# built-in Static Web Apps sign-in providers are switched off (routes in staticwebapp.config.json).
# Usage: eng/deploy/check-headers.sh https://<host>
set -euo pipefail
url="${1:?site address}"

headers=$(curl --silent --show-error --fail --head --proto '=https' "$url/")
grep -qi "^content-security-policy: default-src 'self'" <<<"$headers" || { echo "Content-Security-Policy header missing" >&2; exit 1; }
grep -qi "^content-security-policy:.*'sha256-" <<<"$headers" || { echo "import map hash missing in the policy" >&2; exit 1; }
if grep -qi "importmap-hash\|unsafe-inline\|'unsafe-eval'" <<<"$headers"; then echo "policy not filled in or weakened" >&2; exit 1; fi
grep -qi "^referrer-policy: no-referrer" <<<"$headers" || { echo "Referrer-Policy header missing" >&2; exit 1; }
for provider in github aad; do
  status=$(curl --silent --output /dev/null --write-out '%{http_code}' --proto '=https' "$url/.auth/login/$provider")
  [ "$status" = 404 ] || { echo "/.auth/login/$provider answers $status; the built-in sign-in must be switched off (404)" >&2; exit 1; }
done
echo "Headers of ADR-045 present on $url; built-in sign-in switched off"
