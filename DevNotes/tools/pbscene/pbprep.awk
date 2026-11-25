# pbprep.awk - prepares a Paintball RRSK file for the world (Games/Paintball/RrPaintballLevel + RrPaintballDress).
# Placement data only: names, kinds and numbers.
#
#   awk -f pbprep.awk inst_renderers.tsv inst_materials.tsv debug.rrsk > dressed.rrsk
#
# Inputs: rrscene's inst_renderers.tsv (install prefab renderers: mesh -> materials per submesh) and apkscene's
# inst_materials.tsv (install material -> colour); an RRSK file written by ../apkscene with APKSCENE_DEBUG=1, where
# a plain stand-in ends in " #<mesh>@<material>" and a voxel-shell set follows a
# "# X from a ... voxel shell of ?<mesh> (n boxes) @<material>" comment.
#
# What changes (everything else passes through):
# 1. Stand-ins (X) become "dress" markers by kind; the kit skips them and RrPaintballDress builds them:
#      G dress <kind> cx cy cz qx qy qz qw e=ex,ey,ez c=r,g,b[,a] m=<level material> [cyl=1]
#    [vox=1] marks a box of a voxel-shell set. Kinds: tree_aspen tree_bush tree_pine tree_oak tree_small tree_leaves tree_trunk rock container locker wood tire water
#    metal lamp window stair glass struct keep. Dropped: backdrop meshes over 150 m, fog blobs, mountains, cable
#    protectors, the mods board, anything under 4 cm. Scoreboard_V2 -> "G scoreboard_v2 Scoreboard_V2 ...".
# 2. Renderers whose mesh name is generic (the levels use Unity's built-in cube and LOD-named tree parts; the kit
#    would load an unrelated install mesh of the same name):
#      "Cube" -> G dress cube (a unit cube: centre, rotation, half size = |scale|/2, its material and colour);
#      "LOD0".."LOD9" with a tree material -> "G tint <material> x y z" (the crown colour of the tree there);
#      any other LOD-named or CircuitNode_Board renderer is left out.
# 3. A renderer material the install lacks ("?name") becomes the install's own material for that mesh and
#    submesh (the first install prefab renderer using the mesh), so matched meshes do not draw grey.
function kind(n) {
  if (n ~ /^Aspen_Tree/) return "tree_aspen"
  if (n ~ /^Tree_Bush/) return "tree_bush"
  if (n ~ /^(Tree_Pine|PineTree_New|Pine_Tree)/) return "tree_pine"
  if (n ~ /^Oak_/) return "tree_oak"
  if (n ~ /^Tree_DevilsClub/) return "tree_small"
  if (n ~ /^leaves_LOD/) return "tree_leaves"
  if (n ~ /^trunk_LOD/) return "tree_trunk"
  if (n ~ /^(fog_blob|Mountain_|CableProtector|PaintballModsBoard|UI_Cube_Rounded|Cable_V|SciFi_Garage|Magazine)/) return ""
  if (n ~ /^(Cliff_|Rock_|QuarryWall)/) return "rock"
  if (n ~ /^(ShippingContainer|Shipping_Container|Whole_Closed_Inst|SlidingDoor_|Sliding_Door_|FrontDoor_|BackDoor_|Back_Door_)/) return "container"
  if (n ~ /^Locker_Individual/) return "locker"
  if (n ~ /^(FenceSlats|Backstop|WoodSupport|WoodWalkway|WoodPlank|Pallet|Shelf[0-9]|Refreshment_Stand|Quest_Pirate_BoxSquare|SidePost|Trough|Door_Surround|ArrowSign|VendorStall|TicketBooth|MerchBooth|CarnivalGate)/) return "wood"
  if (n ~ /^(TireStack|Tire_|Tire$)/) return "tire"
  if (n ~ /^(PaintballCrane|Dam_Turbine|Windmill|WindMill|Ladder|Dam_LampPost|Car_FoodTruck|VendingMachine|Cooler_|Ride_Wolf|RecCenter_Percolator|Quest_WallSpeaker|Quest_SciFi_Lazer|BarrelCone|Arena_Cyber_FakeDoor|PortaPotty)/) return "metal"
  if (n ~ /^(Dam_HangingLamp|Dam_WallLamp)/) return "lamp"
  if (n ~ /^(WindowClosed|WindowOpen|Dam_RedWindow|RecRoyale_Frontier_.*Window)/) return "window"
  if (n ~ /^Dam_ModStairs/) return "stair"
  if (n ~ /(StaticObjects|Static Objects|Structures|CachedProBuilderMesh|pb_Mesh|PBGeoMerged|StaticBatch|\.Locales-|\.Static-)/) return "struct"
  return "keep"
}
function dec(s) { gsub(/%20/, " ", s); gsub(/%25/, "%", s); return s }
function enc(s) { gsub(/%/, "%25", s); gsub(/ /, "%20", s); return s }
function big(a, b, c) { return a > b ? (a > c ? a : c) : (b > c ? b : c) }
function abs(v) { return v < 0 ? -v : v }
BEGIN { FS = "\t" }
FILENAME == ARGV[1] { sub(/$/, ""); $0 = $0; if (!($5 in imat)) imat[$5] = $6; next }
FILENAME == ARGV[2] { sub(/$/, ""); $0 = $0; mcol[$3] = $4; next }
FNR == 1 { FS = " "; $0 = $0 }
{ line[++N] = $0 }
END {
  for (i = 1; i <= N; i++) {
    $0 = line[i]
    if ($1 == "M") { mesh[$2] = dec($3) }
    if ($1 == "T") { tname[$2] = dec($3); tidx[dec($3)] = $2; if ($2 + 0 > tmax) tmax = $2 + 0; lastT = i }
  }
  for (i = 1; i <= N; i++) {
    $0 = line[i]; out[i] = $0
    if ($1 == "R" && NF == 13) {
      mn = mesh[$2]; n = split($3, ts, ",")
      if (mn ~ /^LOD[0-9]$/) {
        mt = tname[ts[1]]; sub(/^\?/, "", mt)
        if (mt ~ /^(Tree_|RecRoyale_Tree)/ && mt !~ /[Tt]runk/) { out[i] = "G tint " enc(mt) " " $4 " " $5 " " $6; tints++ } else { out[i] = ""; droppedR++ }
        continue
      }
      if (mn == "CircuitNode_Board") { out[i] = ""; droppedR++; continue }
      if (mn == "Cube") {
        mt = tname[ts[1]]; plain = mt; sub(/^\?/, "", plain)
        c = (mt in mcol) ? mcol[mt] : "1,1,1,1"; split(c, cc, ",")
        out[i] = sprintf("G dress cube %s %s %s %s %s %s %s e=%.4f,%.4f,%.4f c=%s,%s,%s m=%s", $4, $5, $6, $7, $8, $9, $10, abs($11) / 2, abs($12) / 2, abs($13) / 2, cc[1], cc[2], cc[3], enc(mt))
        cubes++; continue
      }
      # unknown level materials -> the install's own material for this mesh and slot
      changed = 0; list = ""
      if (mn in imat) { split(imat[mn], im, ",") } else { delete im }
      for (k = 1; k <= n; k++) {
        t = ts[k]
        if (tname[t] ~ /^\?/ && (k in im) && im[k] != "") {
          nm = im[k]
          if (!(nm in tidx)) { tmax++; tidx[nm] = tmax; tname[tmax] = nm; newT[++NT] = nm }
          t = tidx[nm]; changed = 1; fixed++
        }
        list = list (k > 1 ? "," : "") t
      }
      if (changed) { $3 = list; out[i] = $0 }
      continue
    }
    if ($0 ~ /^# X from a /) {
      vox = $0; sub(/.*voxel shell of \?/, "", vox); vmat = ""
      if (vox ~ / @[^ ]*$/) { vmat = vox; sub(/.* @/, "", vmat); sub(/ @[^ ]*$/, "", vox) }
      sub(/ \([0-9]+ boxes\)$/, "", vox); vox = dec(vox); continue
    }
    if ($1 == "X") {
      name = vox; mat = vmat; isvox = (vox != "")
      if ($NF ~ /^#/) { name = substr($NF, 2); mat = ""; if (name ~ /@/) { mat = name; sub(/.*@/, "", mat); sub(/@.*/, "", name) } name = dec(name); NF--; isvox = 0 }
      if (name == "") name = "?"
      ex = $6; ey = $7; ez = $8
      m = big(ex + 0, ey + 0, ez + 0)
      if (m > 150 || m < 0.04) { out[i] = ""; dropped++; continue }
      if (name == "Scoreboard_V2") { out[i] = sprintf("G scoreboard_v2 Scoreboard_V2 %s %s %s %s %s %s %s e=%s,%s,%s", $3, $4, $5, $9, $10, $11, $12, ex, ey, ez); continue }
      k = kind(name)
      if (name == "?" && (ey + 0 < 0.06 || ex + 0 < 0.06 || ez + 0 < 0.06) && $15 + 0 > $13 + 0.2) k = "water"
      if (k == "" || mat ~ /Backdrop_Mountains/) { out[i] = ""; dropped++; continue }
      a = (NF >= 16 && $16 + 0 < 0.99) ? "," $16 : ""
      if (a != "" && k == "keep") k = "glass"
      out[i] = sprintf("G dress %s %s %s %s %s %s %s %s e=%s,%s,%s c=%s,%s,%s%s m=%s%s%s", k, $3, $4, $5, $9, $10, $11, $12, ex, ey, ez, $13, $14, $15, a, (mat == "" ? "?" : mat), ($2 == "cyl" ? " cyl=1" : ""), (isvox ? " vox=1" : ""))
      kinds[k]++; continue
    }
    if ($1 != "X") { vox = ""; vmat = "" }
  }
  for (i = 1; i <= N; i++) {
    if (out[i] != "" || line[i] == "") print out[i]
    if (i == lastT) for (j = 1; j <= NT; j++) print "T " tidx[newT[j]] " " enc(newT[j])
  }
  s = ""; for (k in kinds) s = s " " k "=" kinds[k]
  printf "# pbprep: dress markers%s; cubes %d; tree tints %d; renderer materials from the install %d; dropped stand-ins %d, renderers %d\n", s, cubes, tints, fixed, dropped, droppedR
}
