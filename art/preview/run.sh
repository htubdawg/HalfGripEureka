#!/bin/bash
# Pictures of the mine without the game: the mod's own geometry code (Mine, Pithead, Route, MeshKit, Textures) is compiled against a
# small stand-in for UnityEngine (Stubs.cs), its meshes written to JSON and drawn with three.js in the pre-installed Chromium.
# Plain colours (each texture's average times its tint), no game lighting: for checking shapes, junctions and placement.
#   art/preview/run.sh <outdir>      needs the .NET 8 SDK and Node (Playwright is pre-installed in a cloud session)
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
OUT=$(mkdir -p "$1" && cd "$1" && pwd)
WORK=${TMPDIR:-/tmp}/eureka-preview
mkdir -p "$WORK"
[ -d "$WORK/node_modules/three" ] || (cd "$WORK" && npm init -y >/dev/null && npm install three@0.160.0 >/dev/null)
if ! dotnet build "$HERE/preview.csproj" -o "$WORK/bin" -v q --nologo > "$WORK/build.log"; then grep -E " error " "$WORK/build.log" | sort -u; exit 1; fi
dotnet "$WORK/bin/preview.dll" "$WORK/scene.json"
cp "$HERE/view.html" "$HERE/shoot.js" "$WORK/"
python3 "$HERE/shots.py" "$WORK"
(cd "$WORK" && node shoot.js "$OUT")
