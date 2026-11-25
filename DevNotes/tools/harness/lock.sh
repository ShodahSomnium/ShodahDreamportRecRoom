#!/bin/bash
# Sourced by run.sh and make_scene.sh: one Unity at a time on the harness copy.
#   take_lock "<what>"   -> mkdir harness/lock (atomic), waiting up to 30 min for another holder; released on exit
HARNESS="D:/Dreamport/Rec Room/Rec Room/DevNotes/tools/harness"
PROJECT="D:/Dreamport/Rec Room/RRHarness"
REAL="D:/Dreamport/Rec Room/Rec Room"
UNITY="C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Unity.exe"
LOCK="$HARNESS/lock"

take_lock() {
  local waited=0
  until mkdir "$LOCK" 2>/dev/null; do
    [ $waited -eq 0 ] && echo "harness busy ($(cat "$LOCK/owner" 2>/dev/null)); waiting up to 30 min..."
    if [ $waited -ge 1800 ]; then
      echo "gave up after 30 min: $LOCK is still held by: $(cat "$LOCK/owner" 2>/dev/null)"
      echo "(if no Unity is running on the harness, the holder died: rm -rf \"$LOCK\")"
      return 1
    fi
    sleep 10
    waited=$((waited + 10))
  done
  echo "$$ $(date '+%F %T') $1" > "$LOCK/owner"
  trap 'rm -rf "$LOCK"' EXIT
  trap 'rm -rf "$LOCK"; exit 130' INT TERM
  return 0
}
