#!/bin/bash
# usage: bash run.sh <cmd> ...   (see Rc.cs header). Needs APK_DIR and RRTOOL_IDX like ../apkscene.
exec "C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe" "$(cd "$(dirname "$0")" && pwd -W)/out/rcdress.dll" "$@"
