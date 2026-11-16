#!/usr/bin/env bash
# Read-only smoke test of a deployed site (docs/architecture.md section 8, deployment tests):
# the start page loads over HTTPS and carries the security headers of ADR-045.
set -euo pipefail
url="${1:?usage: eng/smoke-headers.sh https://<host>}"

headers=$(curl --silent --show-error --fail --max-time 30 --dump-header - --output /dev/null "$url/")
echo "$headers" | head -1

check() {
  if echo "$headers" | grep -qiE "^$1:.*$2"; then echo "ok: $1"; else echo "missing or wrong: $1 (expected $2)"; exit 1; fi
}
check "content-security-policy" "default-src 'none'"
check "content-security-policy" "script-src 'self' 'wasm-unsafe-eval'"
check "referrer-policy" "no-referrer"
check "strict-transport-security" "max-age="
check "x-content-type-options" "nosniff"
