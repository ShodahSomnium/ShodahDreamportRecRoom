#!/bin/bash
# rrsk2bytes.sh - one RRSK 1 placement file -> Assets/#User/SomniumRecRoom/Data/<Name>.bytes (gzip of the compacted text)
# plus its .meta (TextScriptImporter, a GUID derived from the name), for Core/RrSceneBank.cs. The same lossless compaction
# as rrsk2cs.sh (it is used for it); nothing of the .bytes counts against the assembly's string heap.
#
#   bash rrsk2bytes.sh <in.rrsk> [--name Name] [--quant N]
#
# Then run bankscene.sh once so the scene's RrMain carries every Data/*.bytes. Prints: name, bytes, guid.
set -u
IN="${1:-}"
[ -f "$IN" ] || { sed -n '2,9p' "$0"; exit 2; }
shift
NAME=""; EXTRA=()
while [ $# -gt 0 ]; do
  case "$1" in
    --name) NAME="$2"; shift ;;
    --quant) EXTRA+=(--quant "$2"); shift ;;
    *) echo "unknown option $1"; exit 2 ;;
  esac
  shift
done
HERE="$(cd "$(dirname "$0")" && pwd)"
DATA="D:/Dreamport/Rec Room/Rec Room/Assets/#User/SomniumRecRoom/Data"
mkdir -p "$DATA"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
[ -n "$NAME" ] || NAME="$(tr -d '\r' < "$IN" | awk 'NR<=5 && /^#[ ]*RRSK[ ]/ { sub(/^#[ ]*RRSK[ ]+[0-9]+[ ]*/, ""); print; exit }')"
[ -n "$NAME" ] || NAME="$(basename "$IN" .rrsk)"
bash "$HERE/rrsk2cs.sh" "$IN" "$TMP/x.cs" --mode z "${EXTRA[@]}" > /dev/null || { echo "rrsk2cs failed"; exit 3; }
# the z chunks: private const string Cn = "....";  joined, prefix dropped, base64-decoded = the gzip bytes
sed -n 's/^ *private const string C[0-9]* = "\(.*\)";$/\1/p' "$TMP/x.cs" | tr -d '\n' | sed 's/^RRSZ1://' | base64 -d > "$DATA/$NAME.bytes" || { echo "decode failed"; exit 4; }
GUID="$(printf 'SomniumRecRoom/Data/%s' "$NAME" | md5sum | cut -c1-32)"
if [ ! -f "$DATA/$NAME.bytes.meta" ]; then
  printf 'fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$GUID" > "$DATA/$NAME.bytes.meta"
fi
gzip -t "$DATA/$NAME.bytes" 2>/dev/null || { echo "bad gzip for $NAME"; exit 5; }
echo "$NAME $(stat -c %s "$DATA/$NAME.bytes") $(sed -n 's/^guid: //p' "$DATA/$NAME.bytes.meta")"
