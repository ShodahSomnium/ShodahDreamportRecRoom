#!/bin/bash
# jumbotron_convert.sh - The Rise of Jumbotron's Android levels 38-49 -> DN/refdata/apk/Jumbotron_<Room>.rrsk (apkscene
# convert + the encounter markers of jumbotron_markers.pl) -> Games/Quests/Jumbotron/Scenes/RrScene_Jumbotron_<Room>.cs
# (rrsk2cs.sh). Reference data only (names and numbers); the Android build is read in place, never installed or run.
#
#   bash jumbotron_convert.sh <work> <tools> [<rrsk dir>] [--no-cs]
#     <work>      apkscene work folder with bank.bin (bash DN/tools/apkscene/run.sh bank <work>)
#     <tools>     scratch tools built by jumbotron_tools/make_tools.sh <tools> (apks: the broad find; gts: gtscene)
#     <rrsk dir>  where the rrsk go (default DN/refdata/apk); --no-cs: rrsk only (a scratch trial, no scene files)
#
# Iteration 11: gtscene prep runs with jt=1 (DN/tools/gtscene/Gt.cs, Plates.OfJt): the hull as rectangles from 1 cm in
# every material on each face's own axes (no dropped trims, no 5 cm staircases on chamfers), mirrored transforms turned,
# and the level-only stand-ins re-measured as plates / a voxel shell / left out instead of bounds boxes (DN/iter11/
# geometry.md). Per-mesh measurements go to <work>/jtstats/<Room>.tsv. The Golden Trophy (gtprep.sh) keeps jt off.
# Iteration 13: the install-mesh records whose install mesh is not the level's (a name clash, jt=1 prep) are re-measured, and
# the support test (gtscene float) fixes every floating group of pieces before the markers go on (DN/iter13/jumbotron.md).
set -u
W="$1"; X="$2"; NOCS=0; R=""
shift 2
for a in "$@"; do case "$a" in --no-cs) NOCS=1 ;; *) R="$a" ;; esac; done
D="$(cd "$(dirname "$0")" && pwd)"; DN="$D/.."
[ -z "$R" ] && R="$DN/refdata/apk"; T="$DN/tools"
OUT="$DN/../Assets/#User/SomniumRecRoom/Games/Quests/Jumbotron/Scenes"
mkdir -p "$OUT" "$W/jtdump" "$W/jtstats" "$R"
WW="$(cd "$W" && pwd -W)"
while read lv room; do
  [ -z "$lv" ] && continue
  # the hull as exact plates (DN/tools/gtscene prep jt=1: everything apkscene convert writes, the editor-merged hull
  # re-measured as "G plate" quads + "C box" collider plates, the stand-ins re-measured as plates where they are flat)
  bash "$X/gts/run.sh" prep "$W" "$lv" "Jumbotron_$room" "$R/Jumbotron_$room.rrsk" jt=1 "stats=$WW/jtstats/$room.tsv" 2>&1 | tail -1 | cut -c1-160
  # Space (StadiumEntry) spans 640 m: its 1.16 M backdrop plates are cut to the play strip (x -160..0, z -200..20; 5.2 k kept
  # in iteration 8) and to the runtime's own Filter (RrJumbotronActs.Act6: centre -82,0,-88 size 160,90,180), which drops
  # the rest when the room builds anyway
  if [ "$room" = StadiumEntry ]; then
    awk '!($1 == "G" && ($2 == "plate" || $2 == "dress") && ($4 < -160 || $4 > -2 || $5 < -45 || $5 > 45 || $6 < -178 || $6 > 2))' "$R/Jumbotron_$room.rrsk" > "$W/cut.rrsk" && mv "$W/cut.rrsk" "$R/Jumbotron_$room.rrsk"
  fi
  # The boss (Stadium): the runtime drops the small (max half extent < 4 m) plates and stand-ins inside the Jumbotron's
  # block (RrJumbotronActs.Act9 DropBoxes: centre 89.9,11,-96.4 size 14,6,14.5) and rebuilds it; they are left out here
  if [ "$room" = Stadium ]; then
    awk '{ keep = 1; if ($1 == "G" && ($2 == "plate" || $2 == "dress") && $4 >= 82.9 && $4 <= 96.9 && $5 >= 8 && $5 <= 14 && $6 >= -103.65 && $6 <= -89.15) { big = 0; for (i = 7; i <= NF; i++) if (substr($i, 1, 2) == "e=") { n = split(substr($i, 3), e, ","); for (j = 1; j <= n; j++) if (e[j] + 0 > big) big = e[j] + 0 } if (big < 4) keep = 0 } if (keep) print }' "$R/Jumbotron_$room.rrsk" > "$W/cut.rrsk" && mv "$W/cut.rrsk" "$R/Jumbotron_$room.rrsk"
  fi
  # Iteration 13 ("still some random things floating"): the support test, gtscene float (DN/iter13/jumbotron.md). Every
  # group of drawn pieces that touches nothing of the room is kept (keep=: the Departure's platforms and screen curtains, Space's
  # boulder), lowered or snapped onto its support, hung by a string (glow sticks under a ceiling) or left out; the Foyer's
  # activity doors are not drawn (RrJumbotronActs.Foyer); the Jumbotron's rebuilt block (RrJumbotronBoss: body, cap and rim)
  # supports, his own cable loops under the old block go. Report: <work>/jtstats/<Room>_float.tsv.
  FO=""
  [ "$room" = Foyer ] && FO="skip=ActivityDoor_Left,ActivityDoor_Right,ActivityDoor_Emit,ActivityDoor_Glow,ActivityDoor_Frame"
  [ "$room" = StadiumConcession ] && FO="keep=StageTop_mat|Curtain_LOD0"
  [ "$room" = StadiumEntry ] && FO="keep=Rock_Boulder_Mat"
  [ "$room" = Stadium ] && FO="drop=Enemy_Jumbotron_Mat support=89.9,11,-96.4,8.8,5.4,8.8"
  bash "$X/gts/run.sh" float "$R/Jumbotron_$room.rrsk" "$W/float.rrsk" "$WW/inst_meshes.tsv" $FO "report=$WW/jtstats/${room}_float.tsv" | cut -c1-200 && mv "$W/float.rrsk" "$R/Jumbotron_$room.rrsk"
  bash "$X/apks/run.sh" find "$W" "$lv" "-" "." > "$W/jtdump/$lv.txt" 2>&1
  { echo "# jumbotron markers (DevNotes/iter8/jumbotron_markers.pl): encounters, doors, barrels, terminals, portals"; perl "$D/jumbotron_markers.pl" "$W/jtdump/$lv.txt"; } >> "$R/Jumbotron_$room.rrsk"
  [ "$NOCS" = 1 ] || bash "$T/rrscene/rrsk2cs.sh" "$R/Jumbotron_$room.rrsk" "$OUT/RrScene_Jumbotron_$room.cs" --ns SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron.Scenes --quant 3
done <<'LIST'
level38 Foyer
level39 Armory
level40 Battlefield1
level41 Hallway1
level42 Hallway2
level43 Reception
level44 StadiumConcession
level45 StadiumEntry
level46 Garage
level47 Cargo1
level48 Stadium
LIST
