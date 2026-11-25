# crimson_thin2.awk - the second thinning pass for the Crimson Cauldron's levels (iteration 13, crimson agent): after
# crimson_thin.awk the eleven rooms were still 4.7 MB of gzip data (budget ~3 MB). Rule (measured areas only; nothing is
# moved or re-shaped): a plate under SMALL m2 goes when its material's small plates together are under SHARE of that
# material's area (they are the bevels and facets of big surfaces: rocks, walls, trunks), or when the material is foliage
# (FOL, as crimson_thin.awk). Materials whose area is mostly small plates (modular stones, ladders, fences, dark metal
# trim) keep them. Two passes over the same file:
#   awk -v SMALL=0.1 -v SHARE=0.12 -f crimson_thin2.awk in.rrsk in.rrsk > out.rrsk
BEGIN {
  if (SMALL == "") SMALL = 0.1; if (SHARE == "") SHARE = 0.12
  FOL = "Tree_|Vines|Plant_|Mushroom|Crystal_Cluster|Grass|Bush|Leaf|Leaves|Flower|Fern|Moss|Ivy|Hay|Straw|Reed|Lily|Candle|Cobweb|Spiderweb"
  KEEP = "Ladder"
}
function area(   i, e) {
  for (i = 4; i <= NF; i++) if (substr($i, 1, 2) == "e=") { split(substr($i, 3), e, ","); return 4 * e[1] * e[2] }
  return -1
}
FNR == NR { if ($1 == "G" && $2 == "plate") { a = area(); if (a >= 0) { tot[$3] += a; if (a < SMALL) sm[$3] += a } } next }
{
  if ($1 == "G" && $2 == "plate" && $3 !~ KEEP) {
    a = area()
    if (a >= 0 && a < SMALL && ($3 ~ FOL || sm[$3] < SHARE * tot[$3])) { cut++; next }
  }
  print
}
END { printf "# crimson_thin2: %d plates < %s m2 left out (bevels/facets of big surfaces, foliage)\n", cut, SMALL > "/dev/stderr" }
