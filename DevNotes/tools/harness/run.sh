#!/bin/bash
# Runs one scenario in the play-mode harness.
#
#   bash run.sh <scenario.json | name in scenarios/> [--no-sync]
#
# 1. takes the harness lock (waits up to 30 min for another run), released on exit;
# 2. sync.sh (the real project's code, network plugins, manifest and scenes -> harness copy);
# 3. Unity batchmode -runTests -testPlatform PlayMode -testFilter RrScenario (with graphics: no -nographics),
#    RR_SCENARIO / RR_OUT set, log and results in out/<scenario>/;
# 4. prints summary.txt and the PNG paths.
# Exit: Unity's (0 passed, 2 test failed = boot failed, 3 run error e.g. compile errors), 124 timeout
# (RR_TIMEOUT seconds, default 7200: the first run imports Photon and recompiles, ~10+ min).
set -u
source "D:/Dreamport/Rec Room/Rec Room/DevNotes/tools/harness/lock.sh"

SCEN="${1:-}"
[ -n "$SCEN" ] || { echo "usage: run.sh <scenario.json> [--no-sync]"; exit 2; }
[ -f "$SCEN" ] || SCEN="$HARNESS/scenarios/$1"
[ -f "$SCEN" ] || SCEN="$HARNESS/scenarios/$1.json"
[ -f "$SCEN" ] || { echo "no scenario: $1"; exit 2; }
NAME="$(basename "$SCEN" .json)"
OUT="$HARNESS/out/$NAME"
SYNC=1
for a in "$@"; do [ "$a" = "--no-sync" ] && SYNC=0; done

take_lock "run $NAME" || exit 3
if [ $SYNC = 1 ]; then bash "$HARNESS/sync.sh" || { echo "sync failed"; exit 4; }; fi

mkdir -p "$OUT"
rm -f "$OUT"/*.png "$OUT"/dump_*.txt "$OUT/summary.txt" "$OUT/results.xml"
[ -f "$OUT/unity.log" ] && mv -f "$OUT/unity.log" "$OUT/unity.prev.log"
export RR_SCENARIO="$(cygpath -w "$SCEN")"
export RR_OUT="$(cygpath -w "$OUT")"
echo "running $NAME: log $(cygpath -w "$OUT/unity.log")"
T0=$(date +%s)
timeout "${RR_TIMEOUT:-7200}" "$UNITY" -batchmode -projectPath "$(cygpath -w "$PROJECT")" \
  -runTests -testPlatform PlayMode -testFilter RrScenario \
  -testResults "$(cygpath -w "$OUT/results.xml")" -logFile "$(cygpath -w "$OUT/unity.log")"
RC=$?
echo "Unity exit $RC after $(( $(date +%s) - T0 )) s  (0 passed, 2 test failed, 3 run error, 124 timeout)"

if [ -f "$OUT/results.xml" ]; then
  grep -o '<test-case [^>]*' "$OUT/results.xml" | sed -n 's/.*fullname="\([^"]*\)".*result="\([^"]*\)".*duration="\([^"]*\)".*/test \1: \2 (\3 s)/p'
fi
echo "-----------------------------------------------------------------------------------------------"
if [ -f "$OUT/summary.txt" ]; then
  cat "$OUT/summary.txt"
else
  echo "no summary.txt - compile errors or the test never started; from the log:"
  grep -E "error CS|Exception|Aborting|Scripts have compiler errors|RrHarness" "$OUT/unity.log" | head -40
fi
echo "-----------------------------------------------------------------------------------------------"
echo "PNG:"
for f in "$OUT"/*.png; do [ -f "$f" ] && echo "  $(cygpath -w "$f")"; done
exit $RC
