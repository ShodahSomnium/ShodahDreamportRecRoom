#!/bin/bash
# Generates "Assets/Rec Room Scene.unity" in the HARNESS copy with the real editor tool, checks it, and (with
# --install) copies it into the real project.
#
#   bash make_scene.sh             -> lock, sync.sh, Unity -batchmode -executeMethod
#                                     SomniumSpace.Worlds.SomniumRecRoom.EditorTools.RrSceneSetup.CreateWorldSceneBatch -quit,
#                                     check_scene.sh on the result
#   bash make_scene.sh --install   -> the same, then copies the scene + .meta into the real project's Assets/
#                                     (only when every check passed). The open editor imports it on focus; if the
#                                     scene is open there, reload it.
# sync.sh first copies any scene already in the real project to the harness, so the tool completes that scene
# (idempotent) instead of starting over. Log: out/scene/unity.log.
set -u
source "D:/Dreamport/Rec Room/Rec Room/DevNotes/tools/harness/lock.sh"
INSTALL=0
for a in "$@"; do [ "$a" = "--install" ] && INSTALL=1; done
OUT="$HARNESS/out/scene"
mkdir -p "$OUT"
take_lock "make_scene" || exit 3
bash "$HARNESS/sync.sh" || { echo "sync failed"; exit 4; }

T0=$(date +%s)
timeout "${RR_TIMEOUT:-7200}" "$UNITY" -batchmode -projectPath "$(cygpath -w "$PROJECT")" \
  -executeMethod SomniumSpace.Worlds.SomniumRecRoom.EditorTools.RrSceneSetup.CreateWorldSceneBatch \
  -logFile "$(cygpath -w "$OUT/unity.log")" -quit
RC=$?
echo "Unity exit $RC after $(( $(date +%s) - T0 )) s"
grep -E "\[SomniumRecRoom\] (SceneSetup|NetSceneSetup)|error CS|executeMethod" "$OUT/unity.log" | sed 's/^/  /' | head -40
[ $RC = 0 ] || { echo "Unity failed; see $(cygpath -w "$OUT/unity.log")"; exit $RC; }

SCENE="$PROJECT/Assets/Rec Room Scene.unity"
bash "$HARNESS/check_scene.sh" "$SCENE" || { echo "checks failed: not installed"; exit 1; }

if [ $INSTALL = 1 ]; then
  cp -f "$SCENE" "$REAL/Assets/Rec Room Scene.unity"
  cp -f "$SCENE.meta" "$REAL/Assets/Rec Room Scene.unity.meta"
  echo "installed: $(cygpath -w "$REAL/Assets/Rec Room Scene.unity") (+ .meta)"
fi
