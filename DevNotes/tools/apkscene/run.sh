#!/bin/bash
# usage: bash run.sh <cmd> <work> ...   (work = a scratch dir; bank.bin / inst_*.tsv live there)
exec "C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe" "$(cd "$(dirname "$0")" && pwd -W)/out/apkscene.dll" "$@"
