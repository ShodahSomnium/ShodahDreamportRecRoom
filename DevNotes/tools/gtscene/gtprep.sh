#!/bin/bash
# gtprep.sh - Quest for the Golden Trophy's 11 Android levels to the world's generated scene files.
#   bash gtprep.sh [work] [--no-cs]   (work: scratch dir with the apkscene bank; default: the session scratchpad's apkwork)
# 1. gtscene prep (hull -> plates, stand-ins -> dress markers, REF2 enemy spawners + effects merged for the 7 levels the
#    Studio port has) -> DevNotes/refdata/apk/goldentrophy/GoldenTrophy_<Room>.rrsk
# 2. ../rrscene/rrsk2cs.sh (RRSZ1, 1 cm positions) -> Games/Quests/GoldenTrophy/Scenes/RrScene_GoldenTrophy_<Room>.cs
# Data only: the Android files are read in place; the outputs are names and numbers.
set -u
D="$(cd "$(dirname "$0")" && pwd)"; DN="$D/../.."
W="${1:-C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad/apkwork}"
export APK_DIR="${APK_DIR:-C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad/apk}"
OUT="$DN/refdata/apk/goldentrophy"; CS="$DN/../Assets/#User/SomniumRecRoom/Games/Quests/GoldenTrophy/Scenes"
mkdir -p "$OUT"
while read lv room ref; do
  [ -z "$lv" ] && continue
  R=""; [ "$ref" != "-" ] && R="ref=$(cd "$DN/refdata" && pwd -W)/$ref.rrsk"
  bash "$D/run.sh" prep "$W" $lv GoldenTrophy_$room "$OUT/GoldenTrophy_$room.rrsk" "$R" | cut -c1-240 || { echo "prep $room failed"; exit 1; }
  [ "${2:-}" = "--no-cs" ] || bash "$DN/tools/rrscene/rrsk2cs.sh" "$OUT/GoldenTrophy_$room.rrsk" "$CS/RrScene_GoldenTrophy_$room.cs" --ns SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy.Scenes --mode z --quant 2
done <<'LIST'
level19 Foyer GoldenLobby
level20 Armory -
level21 Hallway1 GTact1
level22 Hallway2 GTact2
level23 Hallway3 GTact3
level24 DeadIsland GTact4
level25 Hallway4 GTact5
level26 Library GTact6
level27 Hallway5 -
level28 Battlefield -
level29 Cafeteria -
LIST
