#!/usr/bin/env bash
# Publishes the spike app twice (interpreted and AOT), both trimmed with Brotli and gzip precompression (plan Q2),
# and records the publish wall time of each in publish/build-times.json.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p publish
echo "{" > publish/build-times.json
first=1
for mode in interpreted aot; do
  rm -rf "publish/$mode"
  start=$(date +%s%N)
  dotnet publish SpikeApp/SpikeApp.csproj -c Release -p:SpikeBuild=$mode -o "publish/$mode" -nologo -v q
  end=$(date +%s%N)
  secs=$(awk -v a="$start" -v b="$end" 'BEGIN { printf "%.3f", (b - a) / 1e9 }')
  [ $first -eq 1 ] || echo "," >> publish/build-times.json
  first=0
  printf '  "%s": %s' "$mode" "$secs" >> publish/build-times.json
done
echo "" >> publish/build-times.json
echo "}" >> publish/build-times.json
cat publish/build-times.json
