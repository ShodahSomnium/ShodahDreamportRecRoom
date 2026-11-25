# crimson_thin.awk - fits the Crimson Cauldron's eleven gtscene jt=1 levels into the ~3 MB data budget (iteration 13).
# The Goblin2 levels are organic (forests, bog, mines): 60 MB of RRSK, 5-6x the Jumbotron's, almost all of it the
# rectangles gtscene fits to curved foliage (one per triangle pair) and the collider plates of rocks. Rules, all on
# measured sizes (nothing is moved or re-shaped):
#   - exact duplicate records go (BoilerRoom: 30 k of its 64 k collider boxes are doubles);
#   - a cull-off material's back face (gtscene writes it as a second plate: same material, centre and size, the
#     opposite normal) becomes "ds=1" on the front plate (RrJumbotronHull draws it from both sides);
#   - foliage/decoration plates (FOL: trees, vines, plants, mushrooms, crystals, lamp posts, fences...) smaller than
#     FOLMIN m2 go (the leaves and twigs; trunks, canopies and the forest walls stay);
#   - other plates smaller than MINA m2 go (slivers);
#   - collider boxes: every walkable one (its thin axis within 60 degrees of up) stays whatever its size; a wall-like one
#     whose face (two larger sides) is under CMIN m2 goes (rock bumps and pipe facets; the floors stay whole).
#   awk -v FOLMIN=0.05 -v MINA=0.0004 -v CMIN=0.05 -f crimson_thin.awk in.rrsk > out.rrsk
BEGIN {
  if (FOLMIN == "") FOLMIN = 0.05; if (MINA == "") MINA = 0.0004; if (CMIN == "") CMIN = 0.05
  FOL = "Tree_|Vines|Plant_|Mushroom|Crystal_Cluster|Grass|Bush|Leaf|Leaves|Flower|Fern|Moss|Ivy|Hay|Straw|Reed|Lily|LanternPost|SpikedFence|LifeRing|Campfire|Bedroll|Sandbag|LaundryHamper|Couch_Plush|Candle|Cobweb|Spiderweb"
  n = 0
}
{
  if ($1 == "C" || ($1 == "G" && $2 == "plate")) { if (seen[$0]++) { dup++; next } }
  if ($1 == "G" && $2 == "plate") {
    a = -1; ek = ""
    for (i = 4; i <= NF; i++) if (substr($i, 1, 2) == "e=") { ek = $i; split(substr($i, 3), e, ","); a = 4 * e[1] * e[2] }
    if (a >= 0) {
      if ($3 ~ FOL && a < FOLMIN) { fol++; next }
      if (a < MINA) { sliv++; next }
    }
    # normal = local +z: (2(xz + wy), 2(yz - wx), 1 - 2(x^2 + y^2))
    qx = $7; qy = $8; qz = $9; qw = $10
    nx = 2 * (qx * qz + qw * qy); ny = 2 * (qy * qz - qw * qx); nz = 1 - 2 * (qx * qx + qy * qy)
    key = $3 "|" $4 "|" $5 "|" $6 "|" ek
    if (key in front) {
      j = front[key]
      if (nx * fnx[j] + ny * fny[j] + nz * fnz[j] < -0.95 && !(j in ds)) { ds[j] = 1; back++; next }
    } else {
      front[key] = n + 1; fnx[n + 1] = nx; fny[n + 1] = ny; fnz[n + 1] = nz
    }
  }
  if ($1 == "C" && $2 == "box" && NF >= 12) {
    x = $6; y = $7; z = $8; w = $9; sx = $10; sy = $11; sz = $12
    # thin axis and its world up component (rotated unit axis . y)
    if (sx <= sy && sx <= sz) { up = 2 * (x * y + w * z); f = sy * sz }
    else if (sy <= sx && sy <= sz) { up = 1 - 2 * (x * x + z * z); f = sx * sz }
    else { up = 2 * (y * z - w * x); f = sx * sy }
    if (up < 0) up = -up
    if (up < 0.5 && f < CMIN) { col++; next }
  }
  line[++n] = $0
}
END {
  for (i = 1; i <= n; i++) print line[i] ((i in ds) ? " ds=1" : "")
  printf "# crimson_thin: %d duplicates, %d back faces -> ds=1, %d foliage plates < %s m2, %d slivers < %s m2, %d wall-like collider boxes < %s m2 left out\n", dup, back, fol, FOLMIN, sliv, MINA, col, CMIN > "/dev/stderr"
}
