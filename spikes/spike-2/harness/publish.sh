#!/usr/bin/env bash
# Publishes the spike 2 app (AOT, as chosen for release 1; `interpreted` as an optional argument), trimmed, with
# Brotli and gzip precompression, into publish/<build>. Needs the WebAssembly workload: dotnet workload install wasm-tools.
# Usage: harness/publish.sh [aot|interpreted]
set -euo pipefail
cd "$(dirname "$0")/.."
build="${1:-aot}"
rm -rf "publish/$build"
dotnet publish Spike2App/Spike2App.csproj -c Release -p:SpikeBuild="$build" -o "publish/$build" -nologo -v q
echo "published publish/$build"
