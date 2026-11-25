#!/bin/bash
# Builds rrscene.dll with Unity's bundled Roslyn against Unity's bundled .NET 6 runtime (no SDK / nuget needed).
# Compiles rrtool's bundle reader (../rrtool/Core.cs) in by path (not copied).
set -u
C="$(cd "$(dirname "$0")" && pwd -W)"
U="C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data"
DN="$U/NetCoreRuntime/dotnet.exe"; CSC="$U/DotNetSdkRoslyn/csc.dll"
NC="$U/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
mkdir -p "$C/out"
{ echo "-nologo"; echo "-nostdlib+"; echo "-target:exe"; echo "-langversion:latest"; echo "-optimize+"; echo "-nowarn:0168,0219,8632"
  for f in System.Runtime System.Private.CoreLib System.Collections System.Linq System.Console System.Runtime.Extensions System.Text.Encoding.Extensions System.Text.RegularExpressions System.Memory netstandard; do echo "-r:\"$NC/$f.dll\""; done
  echo "-out:\"$C/out/rrscene.dll\""; echo "\"$C/Usings.cs\""; echo "\"$C/Inst.cs\""; echo "\"$C/InstFx.cs\""; echo "\"$C/Fx.cs\""; echo "\"$C/Scene.cs\""; echo "\"$C/Emit.cs\""; echo "\"$C/Main.cs\""; echo "\"$C/Paintball.cs\""; echo "\"$C/../rrtool/Core.cs\""; } > "$C/out/build.rsp"
"$DN" "$CSC" -noconfig @"$C/out/build.rsp" 2>&1 | grep -E "error|warning" | head -20
cat > "$C/out/rrscene.runtimeconfig.json" <<'JSON'
{ "runtimeOptions": { "tfm": "net6.0", "framework": { "name": "Microsoft.NETCore.App", "version": "6.0.21" } } }
JSON
ls -la "$C/out/rrscene.dll" | awk '{print $5, $9}'
