#!/bin/bash
# bankscene.sh - writes RrMain's "_sceneData" list in "Assets/Rec Room Scene.unity" from every
# Assets/#User/SomniumRecRoom/Data/*.bytes(.meta), so the upload carries them (Core/RrSceneBank.cs). Idempotent; keeps a
# backup of the scene next to this script (bankscene.scene.bak). Close the scene in Unity first (or reload it after).
set -u
PRJ="D:/Dreamport/Rec Room/Rec Room"
SCENE="$PRJ/Assets/Rec Room Scene.unity"
DATA="$PRJ/Assets/#User/SomniumRecRoom/Data"
HERE="$(cd "$(dirname "$0")" && pwd)"
cp "$SCENE" "$HERE/bankscene.scene.bak"
LIST="$(mktemp)"; trap 'rm -f "$LIST"' EXIT
for m in "$DATA"/*.bytes.meta; do
  [ -f "$m" ] || continue
  g="$(sed -n 's/^guid: //p' "$m" | tr -d '\r')"
  [ -n "$g" ] && echo "  - {fileID: 4900000, guid: $g, type: 3}" >> "$LIST"
done
N=$(wc -l < "$LIST")
# drop any old _sceneData block (the key line and its "  - {" items), then insert the new one after _shaderCarriers' items
awk -v list="$LIST" -v n="$N" '
  BEGIN { while ((getline l < list) > 0) items[++k] = l }
  /^  _sceneData:/ { skip = 1; next }
  skip && /^  - \{/ { next }
  { skip = 0 }
  /^  _shaderCarriers:/ { carriers = 1; print; next }
  carriers && /^  - \{/ { print; next }
  carriers { carriers = 0; if (n > 0) { print "  _sceneData:"; for (i = 1; i <= k; i++) print items[i] } else print "  _sceneData: []" }
  { print }
' "$SCENE" > "$SCENE.tmp" && mv "$SCENE.tmp" "$SCENE"
echo "scene data list: $N file(s)"
grep -n "_sceneData" "$SCENE" | head -2
