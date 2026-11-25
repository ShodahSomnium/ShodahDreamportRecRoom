#!/bin/bash
# Builds gtscene.dll with Unity's bundled Roslyn against Unity's bundled .NET 6.0.21. Compiles ../apkscene's reader
# (every .cs but Main.cs) and ../rrtool/Core.cs in by path, unchanged; Gt.cs has its own Main/Paths.
set -u
C="$(cd "$(dirname "$0")" && pwd -W)"; A="$C/../apkscene"
W="E:/Real Dreamports/Dreamport RecRoom/Assets/#User/RecRoomDreamport"
U="C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data"
DN="$U/NetCoreRuntime/dotnet.exe"; CSC="$U/DotNetSdkRoslyn/csc.dll"
NC="$U/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
mkdir -p "$C/out"
{ echo "-nologo"; echo "-nostdlib+"; echo "-target:exe"; echo "-langversion:latest"; echo "-optimize+"; echo "-nowarn:0168,0219,8632"
  for f in System.Runtime System.Private.CoreLib System.Collections System.Linq System.Console System.Runtime.Extensions System.Text.Encoding.Extensions System.Text.RegularExpressions System.Memory System.IO.Compression netstandard; do echo "-r:\"$NC/$f.dll\""; done
  echo "-out:\"$C/out/gtscene.dll\""
  echo "\"$C/Gt.cs\""; echo "\"$C/GtPoly.cs\""
  # the world's "G poly" maths (record format + triangulator), compiled in by path so the offline render draws what the world draws
  echo "\"$W/Games/Quests/Jumbotron/RrJumbotronPoly.cs\""
  # every apkscene source but its Main.cs (upstream adds files: Env, Tree (UrpKeys), Astc, EnvCs ...)
  for f in $(cd "$A" && ls *.cs | grep -v "^Main.cs$" | sed "s/.cs$//"); do echo "\"$A/$f.cs\""; done
  echo "\"$C/../rrtool/Core.cs\""; } > "$C/out/build.rsp"
"$DN" "$CSC" -noconfig @"$C/out/build.rsp" 2>&1 | grep -E "error|warning" | head -30
cat > "$C/out/gtscene.runtimeconfig.json" <<'JSON'
{ "runtimeOptions": { "tfm": "net6.0", "framework": { "name": "Microsoft.NETCore.App", "version": "6.0.21" } } }
JSON
ls -la "$C/out/gtscene.dll" | awk '{print $5, $9}'
