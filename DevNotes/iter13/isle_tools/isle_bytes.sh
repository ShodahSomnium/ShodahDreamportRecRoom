#!/bin/bash
# isle_bytes.sh - the Isle of Lost Skulls rooms (isle_convert.sh --no-bytes output: gtscene jt=1 + isle_markers.pl) thinned to
# the quest's data budget (isle_thin.pl, per-room plate / collider-plate caps below) and written as
# Assets/#User/SomniumRecRoom/Data/Isle_<Room>.bytes (DN/tools/rrscene/rrsk2bytes.sh --quant 3), loaded at runtime with
# RrSceneBank.Text("Isle_<Room>") (Games/Quests/LostSkulls/RrLostSkullsActs.cs).
#
# Also left out here: the mid-level gates' "AreaBlockingGeo" (a 9.2 x 41 x 58 m box collider behind every
# [Quest_Pirate1_MidLevelGate] / [Quest_Pirate1_CurtainGate], switched off by Rec Room's gate script when the gate opens;
# our quest keeps the gates open, RrLostSkullsArea drops their bars at runtime).
#
#   bash isle_bytes.sh <rrsk dir> <thin dir> [--no-thin] [--no-bytes] [--only <Room>]
set -u
R="$1"; O="$2"; shift 2
NOBYTES=0; NOTHIN=0; ONLY=""
while [ $# -gt 0 ]; do case "$1" in --no-bytes) NOBYTES=1 ;; --no-thin) NOTHIN=1 ;; --only) ONLY="$2"; shift ;; esac; shift; done
D="$(cd "$(dirname "$0")" && pwd)"; T="$D/../../tools"
mkdir -p "$O"
while read room plates boxes; do
  [ -z "$room" ] && continue
  [ -n "$ONLY" ] && [ "$ONLY" != "$room" ] && continue
  [ "$NOTHIN" = 1 ] || perl "$D/isle_thin.pl" "$R/Isle_$room.rrsk" "$O/Isle_$room.rrsk" "plates=$plates" "boxes=$boxes" | sed 's/^.*\///'
  awk '!($1 == "C" && $2 == "box" && $10 == 9.2188 && $11 == 41 && $12 == 58.1719)' "$O/Isle_$room.rrsk" > "$O/Isle_$room.cut" && mv "$O/Isle_$room.cut" "$O/Isle_$room.rrsk"
  [ "$NOBYTES" = 1 ] || bash "$T/rrscene/rrsk2bytes.sh" "$O/Isle_$room.rrsk" --name "Isle_$room" --quant 3
done <<'LIST'
Foyer 8000 2500
Hallway1 10500 3000
Hallway2 9000 3000
ShipDeck 12500 3000
Beach1 13000 3000
Beach2 13000 3000
Caves1 12500 3000
SunkenShip 11000 2500
BossArena 10000 2500
LIST
