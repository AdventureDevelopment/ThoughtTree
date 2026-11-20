#!/usr/bin/env bash
# Publishes the client app for Azure Static Web Apps and writes the SHA-256 of its inline import map into the
# Content-Security-Policy of staticwebapp.config.json (ADR-045). The import map changes with every build
# (fingerprinted file names), so its hash cannot be committed; the committed policy holds the placeholder
# {importmap-hash} instead. Fails if the placeholder or the import map is missing or appears twice, or if the
# page has any other inline script. Usage: eng/deploy/publish-client.sh <output-folder>
set -euo pipefail
cd "$(dirname "$0")/../.."

out="${1:?usage: publish-client.sh <output-folder>}"
dotnet publish src/ThoughtTree.Client/ThoughtTree.Client.csproj --configuration Release --output "$out" -warnaserror

python3 - "$out/wwwroot" <<'PY'
import base64, hashlib, pathlib, re, sys

site = pathlib.Path(sys.argv[1])
page = (site / "index.html").read_text(encoding="utf-8")
maps = re.findall(r'<script type="importmap">(.*?)</script>', page, re.S)
inline = [m for m in re.findall(r"<script(?![^>]*\ssrc=)[^>]*>", page) if 'type="importmap"' not in m]
if len(maps) != 1 or inline:
    sys.exit(f"index.html: expected exactly one inline import map and no other inline script, found {len(maps)} and {len(inline)}")
digest = "sha256-" + base64.b64encode(hashlib.sha256(maps[0].encode("utf-8")).digest()).decode("ascii")

config_path = site / "staticwebapp.config.json"
config = config_path.read_text(encoding="utf-8")
if config.count("{importmap-hash}") != 1:
    sys.exit("staticwebapp.config.json: expected the placeholder {importmap-hash} exactly once")
config_path.write_text(config.replace("{importmap-hash}", digest), encoding="utf-8")
print(f"Content-Security-Policy: import map hash {digest}")
PY
