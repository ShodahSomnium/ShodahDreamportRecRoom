#!/bin/bash
# Checks a saved "Rec Room Scene.unity" (YAML) against what Task B requires. Read-only.
#   bash check_scene.sh [scene.unity]     (default: the harness copy's Assets/Rec Room Scene.unity)
# Exit 0 when every check passes, 1 otherwise.
set -u
source "D:/Dreamport/Rec Room/Rec Room/DevNotes/tools/harness/lock.sh"
SCENE="${1:-$PROJECT/Assets/Rec Room Scene.unity}"
[ -f "$SCENE" ] || { echo "no scene at $SCENE"; exit 1; }
Y="$(mktemp)"; tr -d '\r' < "$SCENE" > "$Y"
FAILS=0
guid_of() { sed -n 's/^guid: //p' "$1" | tr -d '\r'; }
ok()   { echo "  ok    $*"; }
bad()  { echo "  FAIL  $*"; FAILS=$((FAILS + 1)); }
expect_count() { # label, pattern, expected
  local n; n=$(grep -c -- "$2" "$Y")
  [ "$n" = "$3" ] && ok "$1: $n" || bad "$1: $n (expected $3)"
}
expect_line() { # label, fixed string
  grep -qF -- "$2" "$Y" && ok "$1 ($2)" || bad "$1: no line '$2'"
}

RRMAIN=$(guid_of "$REAL/Assets/#User/SomniumRecRoom/Core/RrMain.cs.meta")
SN=$(guid_of "$REAL/Assets/#User/SomniumRecRoom/Net/SceneNetworking.cs.meta")
ASM=$(guid_of "$REAL/Assets/#User/SomniumRecRoom/SomniumRecRoom.asmdef.meta")
echo "checking $SCENE"
echo "  (RrMain.cs guid $RRMAIN, SceneNetworking.cs guid $SN, SomniumRecRoom.asmdef guid $ASM)"

echo "objects:"
for n in "Uploader" "Directional Light" "Arrival" "Status Canvas" "Status" "Rec Room" "Rr Net" "Scene Networking" "Rr Net Events"; do
  expect_count "GameObject '$n'" "^  m_Name: $n\$" 1
done
expect_count "Rr Net Player k" '^  m_Name: Rr Net Player [0-9]*$' 50
expect_count "Rr Net Instance k" '^  m_Name: Rr Net Instance [0-9]*$' 24
expect_count "Rr Net Actors k" '^  m_Name: Rr Net Actors [0-9]*$' 12
expect_count "Rr Net Prop k" '^  m_Name: Rr Net Prop [0-9]*$' 48

echo "scripts:"
expect_count "RrMain script (real project guid)" "m_Script: {fileID: 11500000, guid: $RRMAIN, type: 3}" 1
expect_count "SceneNetworking script (real project guid)" "m_Script: {fileID: 11500000, guid: $SN, type: 3}" 1
expect_count "Fusion NetworkObject" "m_Script: {fileID: -1552182283, guid: e725a070cec140c4caffb81624c8c787, type: 3}" 135
expect_count "NetworkBridgeEvents" "guid: 6ba26196d4959fb45a90fa1113c622e9" 1
expect_count "NetworkBridgeData64" "guid: 0b8147e129a49fd49ba7bdc9dc75daaa" 86
expect_count "NetworkBridgeData12" "guid: b9da383ae417d8d49822395dc0037d77" 48
expect_count "WorldSDKConfig" "guid: 5a46fca9cfe00e84b83457af8d644413" 1
expect_count "WorldConfig" "guid: ca5d94d337ec5644a94dfc0f52bb6771" 1
expect_count "UniversalAdditionalLightData" "guid: 474bcb49853aa07438625e644c072ee6" 1

echo "network objects:"
expect_count "Flags 524289" '^  Flags: 524289$' 135
expect_count "ObjectInterest 1 (Global)" '^  ObjectInterest: 1$' 135
SK=$(grep -c '^  SortKey: [1-9]' "$Y"); [ "$SK" = 135 ] && ok "non-zero SortKeys: $SK" || bad "non-zero SortKeys: $SK (expected 135; saved without Fusion's bake?)"
UNIQ=$(grep '^  SortKey: ' "$Y" | sort -u | wc -l); [ "$UNIQ" = 135 ] && ok "distinct SortKeys: $UNIQ" || bad "distinct SortKeys: $UNIQ"
NB=$(awk '/^  NetworkedBehaviours:/{getline; if ($0 ~ /^  - \{fileID: [1-9-]/) n++} END{print n+0}' "$Y")
[ "$NB" = 135 ] && ok "NetworkedBehaviours filled: $NB" || bad "NetworkedBehaviours filled: $NB (expected 135)"
expect_line "SceneNetworking auto transfer off" "_autoTransferObjectsAuthority: 0"
expect_line "SceneNetworking editor connect off" "_editorConnectPhotonNetwork: 0"

echo "uploader:"
expect_line "AssetName" "AssetName: 'Dreamport: Rec Room'"
expect_line "EnableScripting" "EnableScripting: 1"
expect_line "ScriptingAssembly" "ScriptingAssembly: SomniumRecRoom"
grep -q "AsmdefReference: {fileID: [0-9-]*, guid: $ASM" "$Y" || grep -A1 "AsmdefReference:" "$Y" | tr -d '\n' | grep -q "guid: $ASM" && ok "AsmdefReference -> SomniumRecRoom.asmdef" || bad "AsmdefReference does not point at guid $ASM"
expect_line "WorldIdForUpload" "WorldIdForUpload: 0"
expect_line "DoReplaceExistingWorld" "DoReplaceExistingWorld: 0"
expect_line "SpawnPointPos" "SpawnPointPos: {x: 0, y: 0.9, z: 0}"
expect_line "SpawnPointDirection" "SpawnPointDirection: 0"
expect_line "RespawnHeightY" "RespawnHeightY: -100"
expect_line "RespawningBehavior" "RespawningBehavior: 0"

echo "look and wiring:"
expect_line "fog off" "m_Fog: 0"
expect_line "ambient tri-colour (gradient)" "m_AmbientMode: 1"
grep -q "m_Sun: {fileID: [1-9]" "$Y" && ok "RenderSettings.sun set" || bad "RenderSettings.sun not set"
expect_line "LiberationSans SDF on the label" "guid: 8f586378b4e144a9851e7b34d9b748ee"
grep -q "_status: {fileID: [1-9]" "$Y" && ok "RrMain._status wired" || bad "RrMain._status not wired"
ARR=$(awk '/^  m_Name: Arrival$/{print prev} {prev=$0}' "$Y")
[ "$ARR" = "  m_Layer: 23" ] && ok "Arrival on layer 23" || bad "Arrival layer line: '$ARR'"
MISSING=$(grep -c "m_Script: {fileID: 0}" "$Y"); [ "$MISSING" = 0 ] && ok "no missing script references" || bad "$MISSING missing script references"

rm -f "$Y"
echo "$FAILS check(s) failed"
[ $FAILS = 0 ]
