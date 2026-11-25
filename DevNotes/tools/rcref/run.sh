#!/bin/bash
# usage: bash run.sh <cmd> ...   (see RcRef.cs header / README.md)
export APK_DIR="${APK_DIR:-D:/Dreamport/Rec Room/Rec Room/DevNotes/apkwork/apk}"
export RRTOOL_IDX="${RRTOOL_IDX:-D:/Dreamport/Rec Room/Rec Room/DevNotes/rrtoolidx}"
exec "C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe" "$(cd "$(dirname "$0")" && pwd -W)/out/rcref.dll" "$@"
