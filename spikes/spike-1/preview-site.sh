#!/usr/bin/env bash
# Spike preview (.github/workflows/preview-build.yml on main): builds the on-device site of spike 1 into <folder>
# when the pull request of claude/spike-1 changes only this spike. Runs without credentials. Installs the
# WebAssembly workload for the AOT build, publishes both builds and assembles the site with its own strict policy
# (harness/publish-device.sh). Usage: spikes/spike-1/preview-site.sh <folder>
set -euo pipefail
out=$(realpath -m "${1:?usage: preview-site.sh <folder>}")
cd "$(dirname "$0")"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet workload install wasm-tools
rm -rf publish/interpreted publish/aot
harness/publish.sh
harness/publish-device.sh "$out"
