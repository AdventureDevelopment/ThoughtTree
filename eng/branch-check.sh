#!/usr/bin/env bash
# Runs the steps of the branch check (.github/workflows/checks.yml) locally, in the same order.
# Keep both in step when either changes.
set -euo pipefail
cd "$(dirname "$0")/.."

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

dotnet restore ThoughtTree.slnx --locked-mode
dotnet build ThoughtTree.slnx --no-restore --configuration Release -warnaserror
# Bicep templates (ADR-046): with the Azure CLI as in CI, or with a standalone Bicep CLI.
if command -v az >/dev/null; then bicep_cmd=(az bicep); elif command -v bicep >/dev/null; then bicep_cmd=(bicep); else bicep_cmd=(); fi
if [ ${#bicep_cmd[@]} -gt 0 ]; then
  for template in infra/main.bicep infra/bootstrap/bootstrap.bicep; do "${bicep_cmd[@]}" build "$template" --stdout > /dev/null; done
  for parameters in infra/test.bicepparam infra/production.bicepparam; do "${bicep_cmd[@]}" build-params "$parameters" --stdout > /dev/null; done
else
  echo "== Bicep templates not compiled: neither 'az' nor 'bicep' is installed (CI compiles them)"
fi
dotnet format ThoughtTree.slnx --verify-no-changes --no-restore
for intent in Specify Secure Confirm; do
  echo "== Tests: $intent"
  dotnet test ThoughtTree.slnx --no-build --configuration Release --filter "Intent=$intent"
done
echo "== Verify report: tests/secure/ThoughtTree.Architecture.Rules/bin/Release/net10.0/verify-report.md"
