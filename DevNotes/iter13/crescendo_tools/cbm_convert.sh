#!/bin/bash
# cbm_convert.sh - Crescendo of the Blood Moon's Android levels 79-89 (Quest_Dracula1_*) -> DN/refdata/apk/
# Crescendo_<Room>.rrsk -> Assets/#User/SomniumRecRoom/Data/Crescendo_<Room>.bytes (DN/tools/rrscene/rrsk2bytes.sh,
# loaded with RrSceneBank.Text). Reference data only (names and numbers); the Android build is read in place, never
# installed or run. Iteration 13 (crescendo agent, DN/iter13/crescendo.md).
#
#   bash cbm_convert.sh <work> <tools> <dumps> [--no-bytes]
#     <work>   apkscene work folder with bank.bin (bash DN/tools/apkscene/run.sh bank <work>)
#     <tools>  scratch tools of DN/iter8/jumbotron_tools/make_tools.sh <tools> (gts: gtscene with the iteration-13 re-measure)
#     <dumps>  the broad apkscene find dumps levelN.txt (bash <tools>/apks/run.sh find <work> levelN - . > levelN.txt)
#
# Steps per level: gtscene prep jt=1 (hull as exact rectangles, stand-ins re-measured, install-name clashes re-measured)
# -> cbm_trim.pl (foliage cards as canopy boxes, plates under AMIN m2 and not-surfaces left out, level-only colours from
# the texture means NSP/cbm/texavg.txt) -> cbm_thin.awk (duplicates, small wall colliders) -> cbm_markers.pl (encounters,
# pads, gates, chests, mirrors, traps) -> cbm_gates.pl (the gates' blocking colliders and door plates out) -> rrsk2bytes.sh --quant 3. The eleven files total <= ~3 MB of gzip.
set -u
W="$1"; X="$2"; DU="$3"; NOB=0
[ "${4:-}" = "--no-bytes" ] && NOB=1
D="$(cd "$(dirname "$0")" && pwd)"; DN="$D/../.."
R="$DN/refdata/apk"; T="$DN/tools"; TMP="$W/cbmtmp"
TAV="${CBM_TEXAVG:-C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad/cbm/texavg.txt}"
mkdir -p "$R" "$TMP" "$W/cbmstats"
WW="$(cd "$W" && pwd -W)"
while read lv room amin cmin; do
  [ -z "$lv" ] && continue
  bash "$X/gts/run.sh" prep "$W" "$lv" "Crescendo_$room" "$TMP/$room.prep.rrsk" jt=1 "stats=$WW/cbmstats/$room.tsv" 2>&1 | tail -1 | cut -c1-120
  perl "$D/cbm_trim.pl" "$TMP/$room.prep.rrsk" "$TMP/$room.trim.rrsk" "$TAV" "$amin" 2>&1 | sed 's/.*rrsk: //' | cut -c1-160
  # level-only brick and log fences are opaque: RrJumbotronLook draws any "fence" material as see-through (the sci-fi
  # chain-link fences), so these three are renamed in the plates, dress boxes and platemats (the install has none of them)
  awk -v CMIN="$cmin" -f "$D/cbm_thin.awk" "$TMP/$room.trim.rrsk" \
    | sed -E '/^G (platemat|plate|dress) /{s/BrickFence_Mat/Crescendo_BrickWall_Mat/g; s/Fence_Brick_Mat/Crescendo_BrickWall2_Mat/g; s/Log_Post_Fence_Mat/Crescendo_LogPost_Mat/g}' > "$R/Crescendo_$room.rrsk"
  { echo "# crescendo markers (DevNotes/iter13/crescendo_tools/cbm_markers.pl): encounters, pads, gates, chests, mirrors, traps"; perl "$D/cbm_markers.pl" "$DU/$lv.txt"; } >> "$R/Crescendo_$room.rrsk"
  perl "$D/cbm_gates.pl" "$R/Crescendo_$room.rrsk" > "$TMP/$room.gates.rrsk" && mv "$TMP/$room.gates.rrsk" "$R/Crescendo_$room.rrsk"
  [ "$NOB" = 1 ] || bash "$T/rrscene/rrsk2bytes.sh" "$R/Crescendo_$room.rrsk" --name "Crescendo_$room" --quant 3
done <<'LIST'
level79 Foyer 0.03 0.12
level80 Township 0.04 0.2
level81 CarriageRide 0.04 0.2
level82 CastleEntry 0.05 0.2
level83 CastleMainFoyer 0.03 0.12
level84 Gallery 0.03 0.12
level85 Library 0.04 0.15
level86 Park 0.04 0.2
level87 Ballroom 0.04 0.15
level88 Dungeon 0.06 0.3
level89 BossRoom 0.03 0.12
LIST
