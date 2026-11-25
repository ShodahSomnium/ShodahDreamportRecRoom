#!/bin/bash
# Offline check of the scene kit's parser (Core/RrSceneData.cs + Core/RrSceneKitData.cs) under Unity's bundled .NET 6,
# with UnityEngine.CoreModule referenced for the plain structs (Vector3, Quaternion, Color, Bounds - no engine calls).
#
#   # 1. generate classes named RrScene_<file base><one suffix char> from the .rrsk files:
#   for f in refdata/*.rrsk; do n=$(basename "$f" .rrsk | sed 's/[^A-Za-z0-9_]/_/g'); \
#     bash ../rrsk2cs.sh "$f" gen/RrScene_${n}Z.cs --mode z --class RrScene_${n}Z; done
#   #    (a file whose name has other characters than [A-Za-z0-9_] needs a copy named with '_' for the lookup)
#   # 2. build and run (Windows paths):
#   bash build.sh testZ "$(cd gen && pwd -W)"
#   "C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe" out/testZ.dll "<refdata dir>" [verbose]
#
# It parses every source .rrsk and the generated Text(), compares all records (counts, names, poses), prints parse
# time and allocation, and runs a few edge cases. CORE= points at the Core folder (default: the iter4 stage copy).
set -u
C="$(cd "$(dirname "$0")" && pwd -W)"
U="C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data"
DN="$U/NetCoreRuntime/dotnet.exe"; CSC="$U/DotNetSdkRoslyn/csc.dll"; NC="$U/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
CORE="${CORE:-C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad/stage/SomniumRecRoom/Core}"
NAME="$1"; GEN="$2"; mkdir -p "$C/out"
{ echo "-nologo"; echo "-nostdlib+"; echo "-target:exe"; echo "-langversion:9"; echo "-optimize+"; echo "-nowarn:0168,0219,8632,1701,1702"
  for f in System.Runtime System.Private.CoreLib System.Collections System.Linq System.Console System.Runtime.Extensions System.IO.Compression System.Text.Encoding.Extensions mscorlib netstandard; do echo "-r:\"$NC/$f.dll\""; done
  echo "-r:\"$U/Managed/UnityEngine/UnityEngine.CoreModule.dll\""
  echo "-out:\"$C/out/$NAME.dll\""; echo "\"$C/Program.cs\""; echo "\"$CORE/RrSceneData.cs\""; echo "\"$CORE/RrSceneKitData.cs\""
  find "$GEN" -name "*.cs" | sed 's#.*#"&"#'; } > "$C/out/$NAME.rsp"
"$DN" "$CSC" -noconfig @"$C/out/$NAME.rsp" > "$C/out/$NAME.log" 2>&1
echo "build $NAME: $(grep -c 'error' "$C/out/$NAME.log") error line(s)"; grep "error" "$C/out/$NAME.log" | head -5 | cut -c1-300
cp "$U/Managed/UnityEngine/UnityEngine.CoreModule.dll" "$C/out/" 2>/dev/null
printf '{ "runtimeOptions": { "tfm": "net6.0", "framework": { "name": "Microsoft.NETCore.App", "version": "6.0.21" } } }' > "$C/out/$NAME.runtimeconfig.json"
