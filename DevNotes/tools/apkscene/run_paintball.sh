#!/bin/bash
# Converts the Paintball lobby and the 7 maps. usage: bash run_paintball.sh <work>
set -u; D="$(cd "$(dirname "$0")" && pwd)"; W="$1"; R="$D/../../refdata"; P=Packages/com.recroom.legacy/Runtime/Assets/Activities/Paintball/Maps
while read lv sc ref sp; do bash "$D/run.sh" convert "$W" "$R/apk" $lv Paintball_$sc "ref=$R/Paintball_${ref}_gameplay.rrsk" "scene=$P/$sp" 2>&1 | tail -1 | sed 's/Packages[^ ]*//'; done <<'LIST'
level14 River River River/paintball.unity
level15 Homestead Homestead Construction/paintball2_open.unity
level16 Quarry Quarry Castle/Paintball_Castle.unity
level17 Clearcut Clearcut ClearCut/Paintball_ClearCut.unity
level18 Spillway Spillway Dam/Paintball_Dam.unity
level103 DriveIn Drivein DriveIn/DriveIn.unity
level104 MerryMidway MerryMidway MerryMidway/MerryMidway.unity
level113 Lobby Lobby Lobby/paintball_lobby.unity
LIST
