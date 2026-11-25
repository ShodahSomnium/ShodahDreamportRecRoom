#!/bin/bash
# crimson_convert.sh - the Curse of the Crimson Cauldron's Android levels 50-60 (Quest_Goblin2_*) -> DN/refdata/apk/
# Crimson_<Room>.rrsk -> Assets/#User/SomniumRecRoom/Data/Crimson_<Room>.bytes (DN/tools/rrscene/rrsk2bytes.sh, loaded
# with RrSceneBank.Text). Reference data only (names and numbers); the Android build is read in place, never installed
# or run. Iteration 13 (crimson agent). Stages per room:
#   1. gtscene prep jt=1 (hull rectangles, re-measured stand-ins, colliders) -> <work>/ccraw/Crimson_<Room>.rrsk
#   2. the full GameObject dump (apks find) + crimson_markers.pl (encounters, triggers, ladders, barrels, steam, floats,
#      witch points) appended
#   3. size passes (the organic Goblin2 levels are 60 MB of RRSK, 5-6x the Jumbotron's): crimson_thin.awk (duplicates,
#      back faces -> ds=1, foliage < 0.05 m2, slivers, wall-like collider boxes < 0.05 m2), crimson_thin2.awk (plates
#      < 0.1 m2 of big-surface materials and foliage), crimson_quant.awk (wall-like collider boxes < 0.1 m2; colliders,
#      foliage and rock plates to 1 cm) -> <rrsk dir>/Crimson_<Room>.rrsk
#   4. rrsk2bytes.sh --quant 3 (unless --no-bytes). Result (2026-09-26): 11 rooms, 3.04 MB of gzip data.
#
#   bash crimson_convert.sh <work> <tools> [<rrsk dir>] [--no-bytes] [--no-prep]
#     <work>   apkscene work folder with bank.bin (bash DN/tools/apkscene/run.sh bank <work>)
#     <tools>  scratch tools built by DN/iter8/jumbotron_tools/make_tools.sh <tools> (apks: the broad find; gts: gtscene)
#     --no-prep  reuse <work>/ccraw/Crimson_<Room>.rrsk (stage 1) and <work>/ccdump/<level>.txt (stage 2's dump)
set -u
W="$1"; X="$2"; NOB=0; NOP=0; R=""
shift 2
for a in "$@"; do case "$a" in --no-bytes) NOB=1 ;; --no-prep) NOP=1 ;; *) R="$a" ;; esac; done
D="$(cd "$(dirname "$0")" && pwd)"; DN="$D/.."
[ -z "$R" ] && R="$DN/refdata/apk"; T="$DN/tools"
mkdir -p "$W/ccdump" "$W/ccstats" "$W/ccraw" "$R"
WW="$(cd "$W" && pwd -W)"
FQ="Tree_|Vines|Plant_|Canopy|Concrete_PaleBlue_200x|Stones_Modular|Rock"
while read lv room; do
  [ -z "$lv" ] && continue
  RAW="$W/ccraw/Crimson_$room.rrsk"
  if [ "$NOP" = 0 ]; then
    bash "$X/gts/run.sh" prep "$W" "$lv" "Crimson_$room" "$RAW" jt=1 "stats=$WW/ccstats/$room.tsv" 2>&1 | tail -1 | cut -c1-140
    bash "$X/apks/run.sh" find "$W" "$lv" "-" "." > "$W/ccdump/$lv.txt" 2>&1
  else
    # a raw file of an earlier run: its old marker block goes (it is appended again below)
    n=$(grep -n "^# crimson markers" "$RAW" | head -1 | cut -d: -f1)
    [ -n "$n" ] && { head -n $((n - 1)) "$RAW" > "$RAW.tmp" && mv "$RAW.tmp" "$RAW"; }
  fi
  { echo "# crimson markers (DevNotes/iter13/crimson_markers.pl): encounters, ladders, barrels, steam, floats, witch"; perl "$D/crimson_markers.pl" "$W/ccdump/$lv.txt"; } >> "$RAW"
  awk -f "$D/crimson_thin.awk" "$RAW" > "$W/ccraw/$room.t1" 2> "$W/ccstats/${room}_thin.txt"
  awk -v SMALL=0.1 -v SHARE=0.12 -f "$D/crimson_thin2.awk" "$W/ccraw/$room.t1" "$W/ccraw/$room.t1" > "$W/ccraw/$room.t2" 2>> "$W/ccstats/${room}_thin.txt"
  awk -v CW=0.1 -v FQ="$FQ" -f "$D/crimson_quant.awk" "$W/ccraw/$room.t2" > "$R/Crimson_$room.rrsk" 2>> "$W/ccstats/${room}_thin.txt"
  rm -f "$W/ccraw/$room.t1" "$W/ccraw/$room.t2"
  [ "$NOB" = 1 ] || bash "$T/rrscene/rrsk2bytes.sh" "$R/Crimson_$room.rrsk" --name "Crimson_$room" --quant 3
done <<'LIST'
level50 Foyer
level51 Armory
level52 CastleCourtyard
level53 Forest1
level54 GoblinCamp
level55 Forest2
level56 Bog
level57 Mines1
level58 BoilerRoom
level59 BellTowerStairs
level60 BellTowerArena
LIST
