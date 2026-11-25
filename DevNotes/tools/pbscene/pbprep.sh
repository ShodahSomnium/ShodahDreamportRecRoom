#!/bin/bash
# pbprep.sh - the Paintball lobby and 7 maps from the Android build's levels to the world's generated scene files.
#   bash pbprep.sh [work]     (work: scratch dir with the apkscene bank; default: the session scratchpad's apkwork)
# 1. ../apkscene convert with APKSCENE_DEBUG=1 (stand-ins carry their mesh name) into <work>/pbdbg/;
# 2. pbprep.awk (+ rrscene inst_renderers.tsv, apkscene inst_materials.tsv): stand-ins -> "G dress <kind>" markers,
#    built-in cubes -> "G dress cube", tree-crown LOD parts -> "G tint", unknown materials -> the install mesh's own
#    -> DevNotes/refdata/apk/dressed/Paintball_<Map>.rrsk;
# 3. ../rrscene/rrsk2cs.sh (RRSZ1, 1 mm positions) -> Games/Paintball/Scenes/RrScene_Paintball_<Map>.cs.
# Data only: the Android files are read in place by apkscene; the outputs are names and numbers.
set -u
D="$(cd "$(dirname "$0")" && pwd)"; DN="$D/../.."
W="${1:-C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad/apkwork}"
RRS="${RRSCENE_WORK:-C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad/rrscene}"   # rrscene inst: inst_renderers.tsv
OUT="$DN/refdata/apk/dressed"; CS="$DN/../Assets/#User/SomniumRecRoom/Games/Paintball/Scenes"
mkdir -p "$W/pbdbg" "$OUT" "$CS"
P=Packages/com.recroom.legacy/Runtime/Assets/Activities/Paintball/Maps
while read lv sc sp; do
  APKSCENE_DEBUG=1 bash "$D/../apkscene/run.sh" convert "$W" "$W/pbdbg" $lv Paintball_$sc "scene=$P/$sp" > /dev/null 2>&1 || { echo "convert $sc failed"; exit 1; }
  awk -f "$D/pbprep.awk" "$RRS/inst_renderers.tsv" "$W/inst_materials.tsv" "$W/pbdbg/Paintball_$sc.rrsk" > "$OUT/Paintball_$sc.rrsk"
  bash "$DN/tools/rrscene/rrsk2cs.sh" "$OUT/Paintball_$sc.rrsk" "$CS/RrScene_Paintball_$sc.cs" --ns SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball.Scenes --mode z --quant 3
done <<'LIST'
level14 River River/paintball.unity
level15 Homestead Construction/paintball2_open.unity
level16 Quarry Castle/Paintball_Castle.unity
level17 Clearcut ClearCut/Paintball_ClearCut.unity
level18 Spillway Dam/Paintball_Dam.unity
level103 DriveIn DriveIn/DriveIn.unity
level104 MerryMidway MerryMidway/MerryMidway.unity
level113 Lobby Lobby/paintball_lobby.unity
LIST
