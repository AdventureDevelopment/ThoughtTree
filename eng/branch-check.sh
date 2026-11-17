#!/usr/bin/env bash
# Runs the steps of the branch check (.github/workflows/checks.yml) locally, in the same order.
# Keep both in step when either changes.
set -euo pipefail
cd "$(dirname "$0")/.."

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

dotnet restore ThoughtTree.slnx --locked-mode
dotnet build ThoughtTree.slnx --no-restore --configuration Release -warnaserror
dotnet format ThoughtTree.slnx --verify-no-changes --no-restore
for intent in Specify Secure Confirm; do
  echo "== Tests: $intent"
  dotnet test ThoughtTree.slnx --no-build --configuration Release --filter "Intent=$intent"
done
echo "== Verify report: tests/secure/ThoughtTree.Architecture.Rules/bin/Release/net10.0/verify-report.md"
