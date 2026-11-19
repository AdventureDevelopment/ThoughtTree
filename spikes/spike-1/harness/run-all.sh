#!/usr/bin/env bash
# Runs all agent-side series of spike 1 one after another (timing series first, then durability), so they do not
# disturb each other. Takes several hours. Log: out/run-all.log
set -uo pipefail
cd "$(dirname "$0")"
T="--chunk 10000 --batch 1000"
node run-series.mjs --plan main --browsers chromium --builds interpreted,aot --throttles 1,4,6 $T
node run-series.mjs --plan main --browsers firefox --builds interpreted,aot --throttles 1 $T
node run-series.mjs --plan search --browsers chromium --builds interpreted,aot --throttles 1,4,6 $T
node run-series.mjs --plan search --browsers firefox --builds interpreted,aot --throttles 1 $T
node run-series.mjs --plan longnotes --browsers chromium --builds interpreted,aot --throttles 1 $T
node kill.mjs --browser chromium --variant mixed-relaxed --durability relaxed --kills 200
node kill.mjs --browser chromium --variant mixed-strict --durability strict --kills 200
node kill.mjs --browser firefox --variant mixed-default --durability default --kills 200
node kill.mjs --browser chromium --variant snapshot-relaxed --durability relaxed --workload snapshot --kills 50
node kill.mjs --browser firefox --variant snapshot-default --durability default --workload snapshot --kills 50
node kill.mjs --browser chromium --variant draft-relaxed --durability relaxed --workload draft --kills 50
node kill.mjs --browser firefox --variant draft-default --durability default --workload draft --kills 50
node kill.mjs --browser chromium --variant open-relaxed --durability relaxed --mode open --kills 50
node kill.mjs --browser firefox --variant open-default --durability default --mode open --kills 50
node kill.mjs --browser chromium --variant close-relaxed --durability relaxed --mode close --kills 20
node kill.mjs --browser firefox --variant close-default --durability default --mode close --kills 20
echo ALL DONE
