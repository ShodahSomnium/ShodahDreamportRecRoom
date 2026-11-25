# topdown.awk - an SVG plan (top view) of one RRSK scene, for reading layouts. Research aid only.
# usage: awk -v X0=-100 -v Z0=30 -v X1=-25 -v Z1=110 -v PX=10 -f topdown.awk inst_meshes.tsv scene.rrsk > plan.svg
#   X0..X1, Z0..Z1: the window in scene metres; PX pixels per metre. +x right, +z up.
#   Draws C boxes (grey by top height), X stand-ins (their colour, named when the rrsk came from APKSCENE_DEBUG=1),
#   R renderers (install mesh bounds from inst_meshes.tsv, blue outline, mesh name), G markers (red dots, kind:name).
BEGIN { FS = "\t"; W = (X1 - X0) * PX; H = (Z1 - Z0) * PX }
FNR == NR { if (NF >= 6) { split($6, a, " "); if (length(a) >= 6) { mc[$3] = a[1] " " a[2] " " a[3]; me[$3] = a[4] " " a[5] " " a[6] } } next }
FNR == 1 { FS = " "; $0 = $0; printf "<svg xmlns='http://www.w3.org/2000/svg' width='%d' height='%d' style='background:#dfe8d0;font-family:Arial'>\n", W, H }
function sx(x) { return (x - X0) * PX }
function sz(z) { return (Z1 - z) * PX }
function rot(qx, qy, qz, qw, vx, vy, vz,   tx, ty, tz) { # rotate v by q into RX RY RZ
  tx = 2 * (qy * vz - qz * vy); ty = 2 * (qz * vx - qx * vz); tz = 2 * (qx * vy - qy * vx)
  RX = vx + qw * tx + (qy * tz - qz * ty); RY = vy + qw * ty + (qz * tx - qx * tz); RZ = vz + qw * tz + (qx * ty - qy * tx) }
function poly(cx, cy, cz, ex, ey, ez, qx, qy, qz, qw, style,   i, sgx, sgz, s, px, pz, lvl, mnx, mxx, mnz, mxz) { # footprint of an oriented box
  lvl = (qx < 0.02 && qx > -0.02 && qz < 0.02 && qz > -0.02); s = ""
  if (!lvl) { mnx = 1e9; mxx = -1e9; mnz = 1e9; mxz = -1e9; for (i = 0; i < 8; i++) { rot(qx, qy, qz, qw, (i % 2 ? 1 : -1) * ex, (int(i / 2) % 2 ? 1 : -1) * ey, (i >= 4 ? 1 : -1) * ez); if (RX < mnx) mnx = RX; if (RX > mxx) mxx = RX; if (RZ < mnz) mnz = RZ; if (RZ > mxz) mxz = RZ }
    s = sprintf("%.1f,%.1f %.1f,%.1f %.1f,%.1f %.1f,%.1f", sx(cx + mnx), sz(cz + mnz), sx(cx + mxx), sz(cz + mnz), sx(cx + mxx), sz(cz + mxz), sx(cx + mnx), sz(cz + mxz)) }
  else for (i = 0; i < 4; i++) { sgx = (i == 0 || i == 3) ? -1 : 1; sgz = (i < 2) ? -1 : 1; rot(qx, qy, qz, qw, sgx * ex, 0, sgz * ez); s = s sprintf("%.1f,%.1f ", sx(cx + RX), sz(cz + RZ)) }
  printf "<polygon points='%s' style='%s'/>\n", s, style }
function inwin(x, z, m) { return x > X0 - m && x < X1 + m && z > Z0 - m && z < Z1 + m }
function dec(s) { gsub(/%20/, " ", s); gsub(/%25/, "%", s); return s }
$1 == "M" { mesh[$2] = $3; next }
$1 == "C" && $2 == "box" { if (!inwin($3, $5, 20)) next; top = $4 + $11 / 2; g = int(200 - (top - GY) * 12); if (g < 40) g = 40; if (g > 230) g = 230
  poly($3, $4, $5, $10 / 2, $11 / 2, $12 / 2, $6, $7, $8, $9, sprintf("fill:rgb(%d,%d,%d);fill-opacity:0.55;stroke:#555;stroke-width:0.5", g, g, g)); next }
$1 == "X" { if (!inwin($3, $5, 30)) next; nm = ""; if ($NF ~ /^#/) nm = dec(substr($NF, 2)); if (VOX != "") nm = VOX
  poly($3, $4, $5, $6, $7, $8, $9, $10, $11, $12, sprintf("fill:rgb(%d,%d,%d);fill-opacity:0.7;stroke:#a0a;stroke-width:0.7", $13 * 255, $14 * 255, $15 * 255))
  if (nm != "" && ($6 > 0.6 || $8 > 0.6) && !(nm in seenx)) { seenx[nm] = 1; printf "<text x='%.1f' y='%.1f' font-size='9' fill='#707'>%s</text>\n", sx($3), sz($5), nm }
  next }
/^# X from a/ { VOX = $0; sub(/.*shell of \?/, "", VOX); sub(/ \(.*/, "", VOX); VOX = dec(VOX); next }
/^#/ { next }
$1 == "R" { VOX = ""; n = mesh[$2]; if (!inwin($4, $6, 5) || !(n in mc)) next; split(mc[n], c, " "); split(me[n], e, " ")
  s1 = $12; s2 = $13; s3 = $14; rot($8, $9, $10, $11, c[1] * s1, c[2] * s2, c[3] * s3)
  poly($4 + RX, $5 + RY, $6 + RZ, e[1] * (s1 < 0 ? -s1 : s1), e[2] * (s2 < 0 ? -s2 : s2), e[3] * (s3 < 0 ? -s3 : s3), $8, $9, $10, $11, "fill:#9cf;fill-opacity:0.35;stroke:#26a;stroke-width:0.6")
  if (e[1] * s1 > 0.4 || e[3] * s3 > 0.4) { k = n; sub(/_LOD[0-9]$/, "", k); if (!(k in lbl) || LALL) { lbl[k]++; printf "<text x='%.1f' y='%.1f' font-size='8' fill='#135'>%s</text>\n", sx($4), sz($6), k } }
  next }
$1 == "G" { VOX = ""; if (!inwin($4, $6, 0)) next; if ($2 ~ /store|teleport_block/ || $3 ~ /Lockers_Individual|_LOD[0-9]|Cutout|_fbx$/) next; k = $2 ":" dec($3); if (length(k) > 34) k = substr(k, 1, 34)
  printf "<circle cx='%.1f' cy='%.1f' r='3' fill='red'/><text x='%.1f' y='%.1f' font-size='9' fill='#b00'>%s</text>\n", sx($4), sz($6), sx($4) + 4, sz($6) - 2, k; next }
{ VOX = "" }
END { for (x = int(X0 / 10) * 10; x <= X1; x += 10) printf "<line x1='%.1f' y1='0' x2='%.1f' y2='%d' stroke='#999' stroke-width='0.3'/><text x='%.1f' y='10' font-size='9'>%d</text>\n", sx(x), sx(x), H, sx(x) + 1, x
  for (z = int(Z0 / 10) * 10; z <= Z1; z += 10) printf "<line x1='0' y1='%.1f' x2='%d' y2='%.1f' stroke='#999' stroke-width='0.3'/><text x='1' y='%.1f' font-size='9'>%d</text>\n", sz(z), W, sz(z), sz(z) - 1, z
  print "</svg>" }
