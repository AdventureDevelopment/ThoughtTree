#!/usr/bin/env bash
# Spike preview (.github/workflows/preview-build.yml on main): builds the on-device site of spike 2 into <folder>
# when a pull request changes only this spike (and docs/spikes/). Runs without credentials. Installs the
# WebAssembly workload for the AOT build, publishes it and assembles the site with its own strict policy
# (harness/publish-device.sh). Usage: spikes/spike-2/preview-site.sh <folder>
set -euo pipefail
out=$(realpath -m "${1:?usage: preview-site.sh <folder>}")
cd "$(dirname "$0")"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet workload install wasm-tools
rm -rf publish/aot
harness/publish.sh aot
harness/publish-device.sh "$out"
