# dp_clean.awk - iteration 16: Dodgeball_jt / Paddleball_jt (gtscene jt=1 of level11 / level13) -> the same without
# the static records our gameplay rebuilds itself (the install meshes / prefabs are left out at runtime by the room's
# RrJumbotronArea.Skip; this drops what Skip cannot reach):
#   - C sphere colliders (the level's balls / coconut bombs, rebuilt as our networked balls),
#   - A records whose clip starts with "?Levitation" (the paddleball's hum at its static spots),
#   - every R / C record of the ONE exit door we replace with RrPortal (centre within |dx| < 1.1, |dz| < 0.35 of the
#     door frame given by -v door=x,z and below y 2.6; the other doors stay as scenery),
#   - small C boxes (< 0.6 m) within 0.6 m of the [*_GameConfiguration] / [*_GameRuleWrapperCircuit] circuit boards
#     (the CircuitNode meshes are skipped at runtime; Rec Room hides circuits from players),
#   - optional -v dropc="<record prefix>|<record prefix>": records starting with one of these (Dodgeball: the two
#     9 x 0.1 x 9 m boxes 5 cm above the court's drawn floor; the level's balls rest on the drawn floor at y 0.436).
# Two passes over the same file:  awk -v door=-5.669,-17.908 -f dp_clean.awk in.rrsk in.rrsk > out.rrsk
BEGIN { split(door, d, ","); dx0 = d[1] + 0; dz0 = d[2] + 0; nd = dropc != "" ? split(dropc, dp, "|") : 0 }
FNR == NR {
  if ($1 == "G" && ($2 == "game_config" || $2 == "game_rule")) { n++; cx[n] = $4; cz[n] = $6 }
  next
}
{ for (k = 1; k <= nd; k++) if (index($0, dp[k]) == 1) { extra++; next } }
$1 == "C" && $2 == "sphere" { spheres++; next }
$1 == "A" && $2 ~ /^\?Levitation/ { audio++; next }
($1 == "R" || ($1 == "C" && $2 == "box")) {
  if ($1 == "R") { x = $4; y = $5; z = $6 } else { x = $3; y = $4; z = $5 }
  if (x - dx0 < 1.1 && dx0 - x < 1.1 && z - dz0 < 0.35 && dz0 - z < 0.35 && y < 2.6) { doorRec++; next }
  if ($1 == "C" && $10 < 0.6 && $11 < 0.6 && $12 < 0.6) {
    for (i = 1; i <= n; i++) { ex = x - cx[i]; ez = z - cz[i]; if (ex * ex + ez * ez < 0.36) { boards++; next } }
  }
}
{ print }
END { printf "dropped: spheres %d, audio %d, exit door records %d, circuit board colliders %d, by prefix %d\n", spheres, audio, doorRec, boards, extra > "/dev/stderr" }
