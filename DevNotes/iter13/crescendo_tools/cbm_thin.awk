# cbm_thin.awk - the collider half of Crescendo of the Blood Moon's data budget (iteration 13, crescendo agent; the
# plates are cbm_trim.pl's). The castle levels carry up to 29 k "C box" colliders (the Dungeon's cobblestone walls:
# one thin plate behind every rectangle of every stone), most of them wall facets a player never needs one by one.
# Rules, on measured sizes only (nothing is moved or re-shaped):
#   - exact duplicate records go;
#   - a walkable box (its thinnest axis within 60 degrees of up: floors, stairs, ramps, table tops) stays whatever
#     its size;
#   - a wall-like box whose face (its two larger sides) is under CMIN m2 goes (stone bumps, pipe and chain facets);
#     the larger wall plates around them stay, so the walls still stop players and enemies.
#   awk -v CMIN=0.12 -f cbm_thin.awk in.rrsk > out.rrsk
BEGIN { if (CMIN == "") CMIN = 0.12 }
{
  if ($1 == "C" || ($1 == "G" && $2 == "plate")) { if (seen[$0]++) { dup++; next } }
  if ($1 == "C" && $2 == "box" && NF >= 12) {
    qx = $6; qy = $7; qz = $8; qw = $9; sx = $10; sy = $11; sz = $12
    # world up-component of each local axis (the rotation matrix's second row)
    ux = 2 * (qx * qy + qw * qz); uy = 1 - 2 * (qx * qx + qz * qz); uz = 2 * (qy * qz - qw * qx)
    thin = sx; tu = ux; f = sy * sz
    if (sy < thin) { thin = sy; tu = uy; f = sx * sz }
    if (sz < thin) { thin = sz; tu = uz; f = sx * sy }
    if (tu < 0) tu = -tu
    if (tu < 0.5 && f < CMIN) { walls++; next }
  }
  print
}
END { printf "cbm_thin: %d duplicates, %d small wall colliders (< %s m2) left out\n", dup, walls, CMIN > "/dev/stderr" }
