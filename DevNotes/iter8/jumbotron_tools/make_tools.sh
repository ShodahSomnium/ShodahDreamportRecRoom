#!/bin/bash
# make_tools.sh - builds the two scratch tools jumbotron_convert.sh needs, from the shared tools without changing them:
#   <dir>/apks : DN/tools/apkscene with a broader `find` (script "-" = any GameObject, no 400-line cap, and the world
#                scale / component count / colliders of each object) - the EncountersV2 dump jumbotron_markers.pl reads.
#   <dir>/gts  : DN/tools/gtscene (the hull prep; jumbotron_convert.sh runs it with jt=1, iteration 11). VtxCol.cs.txt (the average
#                vertex colour of each plate of a *VertColor* material) is only appended to an old gtscene: upstream writes c= itself.
# Both compile DN/tools/apkscene's current sources by path (every .cs but Main.cs for gtscene).
#   bash make_tools.sh <dir>
set -u
D="$(cd "$(dirname "$0")" && pwd)"; T="$D/../../tools"; O="$1"
mkdir -p "$O/apks" "$O/gts"
# ---- apks
cp "$T/apkscene/"*.cs "$O/apks/" && cp "$T/rrtool/Core.cs" "$O/apks/Core.cs"
perl -0pi -e 's/if \(!\(scr\.Any\(s => sre\.IsMatch\(s\)\) && nre\.IsMatch\(m\.PathOf\(g\.Id\)\)\)\) continue;/if (!((scriptRe == "-" || scr.Any(s => sre.IsMatch(s))) && nre.IsMatch(m.PathOf(g.Id)))) continue;/; s/if \(n\+\+ < 400\) Console\.WriteLine\(\$"\{m\.PathOf\(g\.Id\)\} \| \{w\.p\} \| \{w\.r\} \| act=\{m\.Active\(g\.Id\)\} \| \{string\.Join\(",", scr\.Take\(8\)\)\}"\);/if (n++ < 1000000) { var cols = string.Join(";", m.Col.Where(c => c.Go == g.Id).Select(c => \$"c{c.Cls}{(c.Trig ? "t" : "")}:{c.C}:{c.Size}:{c.Rad}")); Console.WriteLine(\$"{m.PathOf(g.Id)} | {w.p} | {w.r} | act={m.Active(g.Id)} | {string.Join(",", scr.Take(8))} | s={w.s} | comps={g.Comps.Count} | {cols}"); }/' "$O/apks/Conv.cs"
grep -q 'scriptRe == "-"' "$O/apks/Conv.cs" && grep -q 'comps=' "$O/apks/Conv.cs" || { echo "apks: the find patch did not apply (Conv.cs changed upstream)"; exit 1; }
sed -e 's#^C="$(cd "$(dirname "$0")" \&\& pwd -W)"#C="$(cd "$(dirname "$0")" \&\& pwd -W)"#' "$T/apkscene/build.sh" | perl -pe 's{\$C/\.\./rrtool/Core\.cs}{\$C/Core.cs}' > "$O/apks/build.sh"
# every apkscene source file present (upstream adds files, e.g. Env.cs)
FILES=$(cd "$O/apks" && ls *.cs | grep -v '^Core.cs$' | sed 's/\.cs$//' | tr '\n' ' ')
perl -pi -e "s{for f in [A-Za-z ]+; do echo \"\\\\\"\\\$C/\\\$f.cs\\\\\"\"; done}{for f in $FILES; do echo \"\\\\\"\\\$C/\\\$f.cs\\\\\"\"; done}" "$O/apks/build.sh"
cp "$T/apkscene/run.sh" "$O/apks/run.sh"
(cd "$O/apks" && bash build.sh | grep -v warning)
# ---- gts
cp "$T/gtscene/Gt.cs" "$O/gts/Gt.cs"
if grep -q "hasCol" "$O/gts/Gt.cs"; then
  echo "gts: upstream gtscene writes the plates' vertex colours itself (c=); VtxCol not applied"
else
cat "$D/VtxCol.cs.txt" >> "$O/gts/Gt.cs"
perl -0pi -e 's/(      var w = m\.GoW\(r\.Go\); var \(lv, subs\) = SubGeo\.Decode\(L, r\.Mesh\); if \(lv == null\) \{ notes\.Add\("hull mesh unreadable: " \+ r\.Mesh\.Name\); continue; \}\n)/$1      var vcol = VtxCol.Colors(L, r.Mesh);\n/; s/        foreach \(var re in Plates\.Of\(wv, subs\[k\]\)\) \{\n/        var vtris = vcol != null && Regex.IsMatch(mat, "VertColor|Vertcolor", RegexOptions.IgnoreCase) ? new VtxCol.Tris(wv, subs[k], vcol) : null;\n        foreach (var re in Plates.Of(wv, subs[k])) {\n/; s/plates\.Add\(\$"G plate \{Y\.Enc\(mat\)\} \{P3\(re\.c\)\} \{qr\} e=\{F3\(re\.hx\)\},\{F3\(re\.hy\)\}"\);/var pc = vtris?.On(re); plates.Add(\$"G plate {Y.Enc(mat)} {P3(re.c)} {qr} e={F3(re.hx)},{F3(re.hy)}" + (pc != null ? \$" c={F3(pc[0])},{F3(pc[1])},{F3(pc[2])}" : ""));/' "$O/gts/Gt.cs"
[ "$(grep -c 'vcol\|vtris\|pc != null' "$O/gts/Gt.cs")" -ge 3 ] || { echo "gts: the vertex colour patch did not apply (Gt.cs changed upstream)"; exit 1; }
fi
A="$(cd "$T/apkscene" && pwd -W)"; R="$(cd "$T/rrtool" && pwd -W)"
GFILES=$(cd "$T/apkscene" && ls *.cs | grep -v '^Main.cs$' | sed 's/\.cs$//' | tr '\n' ' ')
sed -e "s#A=\"\$C/../apkscene\"#A=\"$A\"#" "$T/gtscene/build.sh" | perl -pe "s{\\\$C/\\.\\./rrtool/Core\\.cs}{$R/Core.cs}; s{for f in [A-Za-z ]+; do echo \"\\\\\"\\\$A/\\\$f.cs\\\\\"\"; done}{for f in $GFILES; do echo \"\\\\\"\\\$A/\\\$f.cs\\\\\"\"; done}" > "$O/gts/build.sh"
cp "$T/gtscene/run.sh" "$O/gts/run.sh"
(cd "$O/gts" && bash build.sh | grep -v warning)
