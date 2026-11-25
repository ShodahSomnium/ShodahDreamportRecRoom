#!/bin/bash
# isle_convert.sh - Isle of Lost Skulls' Android levels 62-70 (Quest_Pirate1_*) -> RRSK (gtscene prep jt=1 + the encounter
# and set-piece markers of isle_markers.pl) -> Assets/#User/SomniumRecRoom/Data/Isle_<Room>.bytes (rrsk2bytes.sh).
# Reference data only (names and numbers); the Android build is read in place, never installed or run.
#
#   bash isle_convert.sh <work> <tools> <rrsk dir> [--no-bytes] [--only <Room>]
#     <work>      apkscene work folder with bank.bin (bash DN/tools/apkscene/run.sh bank <work>)
#     <tools>     scratch tools built by DN/iter8/jumbotron_tools/make_tools.sh <tools> (apks: the broad find; gts: gtscene)
#     <rrsk dir>  where the rrsk go (scratch; the level files are data for the world only through Data/*.bytes)
#
# Size (the world's data budget, ~3 MB for the quest): plates smaller than ISLE_MINPLATE m2 (default 0.004) are left out
# (gtscene jt=1 keeps every rectangle from 1 cm: the level's flour sacks, life rings and boilers are thousands of 1-5 cm
# facets); install renderers, colliders, markers and everything else are kept as gtscene writes them.
set -u
W="$1"; X="$2"; R="$3"; shift 3
NOBYTES=0; ONLY=""
while [ $# -gt 0 ]; do case "$1" in --no-bytes) NOBYTES=1 ;; --only) ONLY="$2"; shift ;; esac; shift; done
D="$(cd "$(dirname "$0")" && pwd)"; DN="$D/../.."; T="$DN/tools"
MIN="${ISLE_MINPLATE:-0.004}"
mkdir -p "$R" "$W/isledump" "$W/islestats"
WW="$(cd "$W" && pwd -W)"
while read lv room; do
  [ -z "$lv" ] && continue
  [ -n "$ONLY" ] && [ "$ONLY" != "$room" ] && continue
  bash "$X/gts/run.sh" prep "$W" "$lv" "Isle_$room" "$R/Isle_$room.full.rrsk" jt=1 "stats=$WW/islestats/$room.tsv" 2>&1 | tail -1 | cut -c1-140
  awk -v T="$MIN" '{ if ($1 == "G" && $2 == "plate") { e = ""; for (i = 11; i <= NF; i++) if (substr($i, 1, 2) == "e=") e = substr($i, 3); split(e, h, ","); if (h[1] * h[2] * 4 < T) next } print }' "$R/Isle_$room.full.rrsk" > "$R/Isle_$room.rrsk"
  bash "$X/apks/run.sh" find "$W" "$lv" "-" "." > "$W/isledump/$lv.txt" 2>&1
  { echo "# isle markers (DevNotes/iter13/isle_tools/isle_markers.pl): encounters, doors, gates, jail locks, cannons, ladders"; perl "$D/isle_markers.pl" "$W/isledump/$lv.txt"; } >> "$R/Isle_$room.rrsk"
  [ "$NOBYTES" = 1 ] || bash "$T/rrscene/rrsk2bytes.sh" "$R/Isle_$room.rrsk" --name "Isle_$room" --quant 3
done <<'LIST'
level62 Foyer
level63 Hallway1
level64 Hallway2
level65 ShipDeck
level66 Beach1
level67 Beach2
level68 Caves1
level69 SunkenShip
level70 BossArena
LIST
