#!/bin/bash
# Builds rcref_cache (dotnet SDK 8). Compiles in by path, unchanged: ../rrtool_cache/*.cs (UnityFS / serialized-file
# reader), ../apkscene/Geo.cs (V3/Q). CRc.cs is a snapshot copy of ../rcscene/Rc.cs's reader classes.
set -u
C="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$C/out"
cd "$C" && dotnet build -c Release -o "$C/out/bin" -v q -nologo 2>&1 | grep -E "error|Warn|warn" | grep -v "warning CS" | head -30
ls -la "$C/out/bin/rcref_cache.dll" | awk '{print $5, $9}'
