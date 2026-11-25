#!/bin/bash
# usage: bash run.sh <cmd> ...   (see Gt.cs header)
exec "C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe" "$(cd "$(dirname "$0")" && pwd -W)/out/gtscene.dll" "$@"
