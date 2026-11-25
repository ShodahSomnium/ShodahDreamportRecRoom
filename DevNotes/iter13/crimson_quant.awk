# crimson_quant.awk - the third size pass for the Crimson Cauldron's levels (iteration 13, crimson agent), run after
# crimson_thin.awk / crimson_thin2.awk and before rrsk2bytes.sh --quant 3:
#   - wall-like collider boxes (thin axis more than 60 degrees from up) whose face is under CW m2 go (rock and trunk
#     facets; every walkable box stays, whatever its size);
#   - collider boxes are written to 1 cm (positions), 3-decimal rotations and sizes rounded UP to 1 cm (no new gaps);
#   - foliage plates (FQ: trees, vines, plants, canopies) the same: 1 cm centres, 3-decimal rotations, half-extents
#     rounded up to 1 cm (they overlap by a few mm instead of opening cracks).
# Architecture plates keep rrsk2bytes' 1 mm. Measured numbers only, nothing moved beyond the rounding.
#   awk -v CW=0.1 -v FQ="Tree_|Vines|Plant_|Canopy" -f crimson_quant.awk in.rrsk > out.rrsk
function r(v, d,   s) { s = sprintf("%." d "f", v); sub(/0+$/, "", s); sub(/\.$/, "", s); if (s == "-0" || s == "") s = "0"; return s }
BEGIN { if (CW == "") CW = 0.1; if (FQ == "") FQ = "Tree_|Vines|Plant_|Canopy" }
{
  if ($1 == "C" && $2 == "box" && NF >= 12) {
    x = $6; y = $7; z = $8; w = $9; sx = $10; sy = $11; sz = $12
    if (sx <= sy && sx <= sz) { up = 2 * (x * y + w * z); f = sy * sz }
    else if (sy <= sx && sy <= sz) { up = 1 - 2 * (x * x + z * z); f = sx * sz }
    else { up = 2 * (y * z - w * x); f = sx * sy }
    if (up < 0) up = -up
    if (up < 0.5 && f < CW) { cut++; next }
    for (i = 3; i <= 5; i++) $i = r($i, 2)
    for (i = 6; i <= 9; i++) $i = r($i, 3)
    for (i = 10; i <= 12; i++) $i = r($i + 0.004, 2)
    q++
  } else if ($1 == "G" && $2 == "plate" && $3 ~ FQ) {
    for (i = 4; i <= 6; i++) $i = r($i, 2)
    for (i = 7; i <= 10; i++) $i = r($i, 3)
    for (i = 11; i <= NF; i++) if (substr($i, 1, 2) == "e=") { split(substr($i, 3), e, ","); $i = "e=" r(e[1] + 0.004, 2) "," r(e[2] + 0.004, 2) }
    q++
  }
  print
}
END { printf "# crimson_quant: %d wall-like collider boxes < %s m2 left out, %d records to 1 cm\n", cut, CW, q > "/dev/stderr" }
