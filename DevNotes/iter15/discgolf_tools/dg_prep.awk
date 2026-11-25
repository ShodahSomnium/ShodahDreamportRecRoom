# dg_prep.awk - iteration 15 Disc Golf: cleans a gtscene jt=1 RRSK of a Disc Golf level (level9 Lake, level10
# Propulsion) before isle_thin.pl. Measured numbers in, measured numbers out; only records are left out:
#   - the level's own discs (practice discs, the four GameplayDiscs): R and C mesh records of the install mesh
#     Frisbee_LOD0 and placed (DiscGolfDisc) prefabs (P), their crossed capsule colliders (r 0.015 h 0.3048) and the hull plates drawn in disc materials
#     (DiscGolfDisc_*, SportDisc_*) - the room spawns its own (DiscGolfDisc) tools;
#   - with cut=x0,z0,x1,z1: gtscene's 0.2 m collider plates ("C box" depth 0.2, the hull's mesh colliders) outside that
#     scene-space rectangle (Lake: 642 k of 717 k plates are the background hills nobody can reach).
#   awk -v cut=-90,-120,120,140 -f dg_prep.awk in.rrsk > out.rrsk
BEGIN { if (cut != "") { split(cut, c, ","); x0 = c[1] + 0; z0 = c[2] + 0; x1 = c[3] + 0; z1 = c[4] + 0; useCut = 1 } }
$1 == "M" { mesh[$2] = $3 }
$1 == "R" && mesh[$2] == "Frisbee_LOD0" { dr++; next }
$1 == "C" && $2 == "mesh" && mesh[$3] == "Frisbee_LOD0" { dc++; next }
$1 == "P" && $2 == "(DiscGolfDisc)" { dr++; next }
$1 == "C" && $2 == "capsule" && $10 == 0.015 && $11 == 0.3048 { dc++; next }
$1 == "G" && $2 == "plate" && ($3 ~ /^DiscGolfDisc_/ || $3 ~ /^SportDisc_/) { dp++; next }
useCut && $1 == "C" && $2 == "box" && $12 == 0.2 && ($3 < x0 || $3 > x1 || $5 < z0 || $5 > z1) { dcut++; next }
{ print }
END { printf("dg_prep: left out %d disc renderers, %d disc colliders, %d disc plates, %d collider plates outside the cut\n", dr, dc, dp, dcut) > "/dev/stderr" }
