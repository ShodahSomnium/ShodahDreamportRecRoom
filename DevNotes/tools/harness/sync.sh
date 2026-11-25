#!/bin/bash
# Mirrors the real Rec Room project's code into the harness copy (read-only on the real project).
#
#   bash sync.sh
#
# robocopy /MIR (so deletions follow, .meta files included, only changed files copied) for:
#   Assets/#User/SomniumRecRoom, Assets/#User/Community Modules, Assets/Photon   (+ each folder's own .meta)
# plain copies (only when different) of:
#   Packages/manifest.json, Assets/#User.meta, every top-level Assets/*.unity scene (+ .meta)
# The harness's own folders (Assets/RrHarnessTests, Assets/Editor, Assets/RRProbe, ...) are never touched.
# Exit 0 on success, 1 if any copy failed (robocopy codes >= 8 are failures, 0-7 are success).
set -u
export MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*'   # keep robocopy's /MIR etc. away from Git Bash path conversion
SRC="D:/Dreamport/Rec Room/Rec Room"
DST="D:/Dreamport/Rec Room/RRHarness"
FAIL=0
TMPLOG="$(mktemp)"

win() { cygpath -w "$1"; }

copyfile() { # $1 = file relative to the project root; copied only when the bytes differ
  local s="$SRC/$1" d="$DST/$1"
  [ -f "$s" ] || return 0
  if ! cmp -s "$s" "$d" 2>/dev/null; then
    mkdir -p "$(dirname "$d")"
    if cp -f "$s" "$d"; then echo "sync: file $1"; else echo "sync: FAILED file $1"; FAIL=1; fi
  fi
}

mirror() {   # $1 = folder relative to the project root
  if [ ! -d "$SRC/$1" ]; then echo "sync: (absent in the project) $1"; return 0; fi
  robocopy "$(win "$SRC/$1")" "$(win "$DST/$1")" /MIR /R:2 /W:1 /NP /NJH /NJS /NDL /XF '*.tmp' > "$TMPLOG" 2>&1
  local rc=$?
  if [ $rc -ge 8 ]; then
    echo "sync: FAILED $1 (robocopy $rc)"; cat "$TMPLOG"; FAIL=1
  else
    local n; n=$(grep -c '[^[:space:]]' "$TMPLOG")
    echo "sync: $1 - $n change(s) (robocopy $rc)"
    [ "$n" -gt 0 ] && [ "$n" -le 40 ] && sed 's/^[[:space:]]*/    /' "$TMPLOG" | grep '[^[:space:]]'
  fi
  copyfile "$1.meta"
}

mkdir -p "$DST/Assets/#User"
copyfile "Assets/#User.meta"
mirror "Assets/#User/SomniumRecRoom"
mirror "Assets/#User/Community Modules"
mirror "Assets/Photon"
copyfile "Packages/manifest.json"
for f in "$SRC"/Assets/*.unity; do
  [ -f "$f" ] || continue
  n="$(basename "$f")"
  copyfile "Assets/$n"
  copyfile "Assets/$n.meta"
done
rm -f "$TMPLOG"
exit $FAIL
