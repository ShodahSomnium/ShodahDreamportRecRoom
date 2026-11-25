# bowl_clean.awk - Bowling_jt.rrsk (gtscene jt=1 of level91) -> the same with the bowling pins' own colliders removed:
# the level stores every pin's box + capsule colliders as static C records at the pin spots, and our pins are real
# Rec Room (SandboxBowlingPin) props placed on those spots. Two passes over the same file:
#   awk -f bowl_clean.awk Bowling_jt.rrsk Bowling_jt.rrsk > Bowling_clean.rrsk
# A C record is dropped when its centre is within 0.07 m (x/z) of a pin marker (weapon_pickup BowlingPin / (SandboxBowlingPin))
# and 0..0.7 m above the pin's base. Prints the count to stderr.
FNR == NR {
  if ($1 == "G" && $2 == "weapon_pickup" && ($3 ~ /BowlingPin/ || $0 ~ /prefab=\(SandboxBowlingPin\)/)) { n++; px[n] = $4; py[n] = $5; pz[n] = $6 }
  next
}
$1 == "C" {
  for (i = 1; i <= n; i++) {
    dx = $3 - px[i]; dz = $5 - pz[i]; dy = $4 - py[i]
    if (dx * dx + dz * dz < 0.0049 && dy >= -0.01 && dy <= 0.7) { dropped++; next }
  }
}
{ print }
END { printf "pins %d, pin colliders dropped %d\n", n, dropped > "/dev/stderr" }
