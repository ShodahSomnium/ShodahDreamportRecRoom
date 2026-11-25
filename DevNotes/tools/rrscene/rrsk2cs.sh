#!/bin/bash
# rrsk2cs.sh - turns one RRSK 1 placement file into a generated C# file for the world (see Core/RrSceneKitData.cs).
#
#   bash rrsk2cs.sh <in.rrsk> <out.cs> [--class RrScene_X] [--ns Namespace] [--mode auto|plain|z]
#                   [--no-compact] [--quant N] [--threshold CHARS]
#
#   --mode plain  RRSK text as string constants, one source line per record ("...\n" + ...), chunks <= 16,000 chars.
#   --mode z      "RRSZ1:" + base64(gzip -9 -n (text)), chunks of 16,000 chars (one source line each).
#   --mode auto   (default) plain if the compacted text is <= --threshold characters (default 65536), else z.
#   compaction    (default on, lossless): numbers lose trailing zeros ("0.5000" -> "0.5", "-0" -> "0"); in R, P, F,
#                 C box, C mesh, X and G records an identity rotation ("0 0 0 1") and a unit scale ("1 1 1") are
#                 dropped and a uniform scale is written once; X drops alpha 1. RrSceneData.Parse reads both forms.
#   --quant N     (lossy, off by default) round positions, sizes and scales to N decimals (3 = 1 mm), rotations
#                 keep 4. Comment lines ("# ..." after the header, "#U ...") are dropped when compacting.
#
# Prints one line: source size, compacted size, embedded size, chunk count, mode. Needs bash, awk, gzip, base64
# (Git Bash has all four). The output is placement data only (names and numbers), never a Rec Room file.
set -u
IN="${1:-}"; OUT="${2:-}"
[ -f "$IN" ] && [ -n "$OUT" ] || { sed -n '2,20p' "$0"; exit 2; }
shift 2
CLASS=""; NS="SomniumSpace.Worlds.SomniumRecRoom.Places.Scenes"; MODE="auto"; COMPACT=1; QUANT=-1; THRESHOLD=65536
while [ $# -gt 0 ]; do
  case "$1" in
    --class) CLASS="$2"; shift ;;
    --ns) NS="$2"; shift ;;
    --mode) MODE="$2"; shift ;;
    --no-compact) COMPACT=0 ;;
    --quant) QUANT="$2"; shift ;;
    --threshold) THRESHOLD="$2"; shift ;;
    *) echo "unknown option $1"; exit 2 ;;
  esac
  shift
done

TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
tr -d '\r' < "$IN" > "$TMP/src.txt"
NAME="$(awk 'NR<=5 && /^#[ ]*RRSK[ ]/ { sub(/^#[ ]*RRSK[ ]+[0-9]+[ ]*/, ""); print; exit }' "$TMP/src.txt")"
[ -n "$NAME" ] || NAME="$(basename "$IN" .rrsk)"
[ -n "$CLASS" ] || CLASS="RrScene_$(printf '%s' "$NAME" | sed 's/[^A-Za-z0-9_]/_/g')"

# ---- compaction ------------------------------------------------------------------------------------------------
if [ "$COMPACT" = 1 ]; then
awk -v Q="$QUANT" '
function tz(s) {                       # trim a number: trailing zeros, lone dot, negative zero
  if (s ~ /^[-+]?[0-9]*\.[0-9]+$/) { sub(/0+$/, "", s); sub(/\.$/, "", s) }
  if (s == "-0" || s == "" || s == "+0") s = "0"
  return s
}
function qn(s) { if (Q >= 0 && s ~ /^[-+]?[0-9]*\.?[0-9]+$/) s = sprintf("%." Q "f", s); return tz(s) }
# position (3 at i), rotation (4), scale (3): writes the short form RrSceneData.Trs reads
function trs(i,   p, r, s, out) {
  p = qn($i) " " qn($(i+1)) " " qn($(i+2))
  r = tz($(i+3)) " " tz($(i+4)) " " tz($(i+5)) " " tz($(i+6))
  out = p
  if (r != "0 0 0 1" && r != "0 0 0 -1") out = out " " r
  sx = qn($(i+7)); sy = qn($(i+8)); sz = qn($(i+9))
  if (!(sx == "1" && sy == "1" && sz == "1")) { if (sx == sy && sy == sz) out = out " " sx; else out = out " " sx " " sy " " sz }
  return out
}
function rest(from,   s, k) { s = ""; for (k = from; k <= NF; k++) s = s " " $k; return s }
function nums(from, to,   s, k) { s = ""; for (k = from; k <= to; k++) s = s " " tz($k); return s }
NR == 1 && /^#/ { print; next }
/^#/ || NF == 0 { next }
$1 == "R" && NF == 13 { print "R " $2 " " $3 " " trs(4); next }
$1 == "P" && NF == 12 { print "P " $2 " " trs(3); next }
$1 == "C" && $2 == "box" && NF == 12 { print "C box " trs(3); next }
$1 == "C" && $2 == "mesh" && NF == 13 { print "C mesh " $3 " " trs(4); next }
$1 == "C" && $2 == "sphere" { print "C sphere" nums(3, NF); next }
$1 == "C" && $2 == "capsule" { print "C capsule" nums(3, NF); next }
$1 == "X" && NF == 16 {
  r = tz($9) " " tz($10) " " tz($11) " " tz($12)
  out = "X " $2 " " qn($3) " " qn($4) " " qn($5) " " qn($6) " " qn($7) " " qn($8)
  if (r != "0 0 0 1" && r != "0 0 0 -1") out = out " " r
  out = out " " tz($13) " " tz($14) " " tz($15)
  if (tz($16) != "1") out = out " " tz($16)
  print out; next
}
$1 == "G" && NF >= 10 {
  r = tz($7) " " tz($8) " " tz($9) " " tz($10)
  out = "G " $2 " " $3 " " qn($4) " " qn($5) " " qn($6)
  if (r != "0 0 0 1" && r != "0 0 0 -1") out = out " " r
  print out rest(11); next
}
$1 == "F" && NF >= 10 {
  out = "F " $2 " " qn($3) " " qn($4) " " qn($5)
  r = tz($6) " " tz($7) " " tz($8) " " tz($9); s = qn($10)
  if (r != "0 0 0 1" && r != "0 0 0 -1") out = out " " r
  if (s != "1") out = out " " s
  print out rest(11); next
}
$1 == "L" { print "L " $2 nums(3, NF); next }
$1 == "A" { print "A " $2 nums(3, NF); next }
$1 == "B" { print "B" nums(2, NF); next }
$1 == "E" && $2 == "fog" { print "E fog " $3 nums(4, NF); next }
$1 == "E" && $2 == "ambient" { print "E ambient " tz($3) " " tz($4) " " tz($5) rest(6); next }
{ print }
' "$TMP/src.txt" > "$TMP/text.txt"
else
  cp "$TMP/src.txt" "$TMP/text.txt"
fi

SRC_BYTES=$(wc -c < "$TMP/src.txt"); TEXT_BYTES=$(wc -c < "$TMP/text.txt"); LINES=$(grep -vc '^#' "$TMP/text.txt")
if [ "$MODE" = auto ]; then [ "$TEXT_BYTES" -le "$THRESHOLD" ] && MODE=plain || MODE=z; fi

# ---- the C# file -----------------------------------------------------------------------------------------------
{
  echo "// <auto-generated>"
  echo "// Generated by DevNotes/tools/rrscene/rrsk2cs.sh from $(basename "$IN") ($SRC_BYTES bytes, $LINES records). Do not edit: regenerate."
  echo "// Placement data only (install asset names and numbers derived from a reference scene); no Rec Room file is embedded."
  echo "// </auto-generated>"
  echo "namespace $NS"
  echo "{"
  echo "    /// <summary>RRSK placement \"$NAME\" for RrSceneKit.Build(Text(), ...); form: $MODE (see RrSceneKitData).</summary>"
  echo "    public static class $CLASS"
  echo "    {"
  printf '        public const string Name = "%s";\n' "$(printf '%s' "$NAME" | sed 's/\\/\\\\/g; s/"/\\"/g')"
  echo "        public const int Records = $LINES;"
  [ "$MODE" = z ] && echo "        public const bool Compressed = true;" || echo "        public const bool Compressed = false;"
} > "$TMP/head.cs"

if [ "$MODE" = z ]; then
  { printf 'RRSZ1:'; gzip -9 -n -c "$TMP/text.txt" | base64 -w0; } > "$TMP/payload.txt"
  EMBED=$(wc -c < "$TMP/payload.txt")
  awk -v W=16000 '{ n = int((length($0) + W - 1) / W); for (i = 0; i < n; i++) printf "        private const string C%d = \"%s\";\n", i, substr($0, i * W + 1, W); print n > "/dev/stderr" }' "$TMP/payload.txt" > "$TMP/chunks.cs" 2> "$TMP/count.txt"
else
  EMBED=$TEXT_BYTES
  # one source line per record; a chunk closes before it would pass 16,000 characters of value
  awk -v W=16000 '
  function esc(s) { gsub(/\\/, "\\\\", s); gsub(/"/, "\\\"", s); return s }
  BEGIN { c = -1; used = W + 1 }
  {
    v = $0 "\n"; L = length(v)
    if (used + L > W) { if (c >= 0) print ";"; c++; printf "        private const string C%d =\n            \"%s\\n\"", c, esc($0); used = L; next }
    printf "\n            + \"%s\\n\"", esc($0); used += L
  }
  END { if (c >= 0) print ";"; print c + 1 > "/dev/stderr" }' "$TMP/text.txt" > "$TMP/chunks.cs" 2> "$TMP/count.txt"
fi
N=$(cat "$TMP/count.txt")
{
  cat "$TMP/head.cs"
  printf '\n        public static string Text()\n        {\n            return SomniumSpace.Worlds.SomniumRecRoom.RrSceneKitData.Join('
  for ((i = 0; i < N; i++)); do [ $i -gt 0 ] && printf ', '; printf 'C%d' $i; done
  printf ');\n        }\n\n'
  cat "$TMP/chunks.cs"
  echo "    }"
  echo "}"
} > "$OUT"
OUT_BYTES=$(wc -c < "$OUT")
echo "$(basename "$IN"): source $SRC_BYTES B, text $TEXT_BYTES B, embedded $EMBED chars, $N chunk(s), mode $MODE, $CLASS.cs $OUT_BYTES B"
