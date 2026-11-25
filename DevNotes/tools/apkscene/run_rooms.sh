#!/bin/bash
# Converts the other built-in rooms (priority order). usage: bash run_rooms.sh <work>
set -u; D="$(cd "$(dirname "$0")" && pwd)"; W="$1"; R="$D/../../refdata"
while read lv sc; do [ -z "$lv" ] && continue; bash "$D/run.sh" convert "$W" "$R/apk" $lv $sc 2>&1 | tail -1 | cut -c1-160; done <<'LIST'
level7 RecCenter_Builtin
level11 Dodgeball
level13 Paddleball
level8 Charades
level105 Charades_InkSpace
level76 LaserTag_Hangar
level77 LaserTag_CyberjunkCity
level72 Soccer
level9 DiscGolf
level10 DiscGolf_Propulsion
level91 Bowling
level6 Dorm_Builtin
level19 GoldenTrophy_Foyer
level20 GoldenTrophy_Armory
level21 GoldenTrophy_Hallway1
level22 GoldenTrophy_Hallway2
level23 GoldenTrophy_Hallway3
level24 GoldenTrophy_DeadIsland
level25 GoldenTrophy_Hallway4
level26 GoldenTrophy_Library
level27 GoldenTrophy_Hallway5
level28 GoldenTrophy_Battlefield
level29 GoldenTrophy_Cafeteria
level62 Isle_Foyer
level63 Isle_Hallway1
level64 Isle_Hallway2
level65 Isle_ShipDeck
level66 Isle_Beach1
level67 Isle_Beach2
level68 Isle_Caves1
level69 Isle_SunkenShip
level70 Isle_BossArena
level107 RecRally_Chaparral
level92 StuntRunner_Lobby
level93 StuntRunner_Course1
level94 StuntRunner_Course2
level95 StuntRunner_Course3
level96 StuntRunner_Course4
level97 StuntRunner_Course5
level98 StuntRunner_Course6
level100 StuntRunner_Baseroom
LIST
