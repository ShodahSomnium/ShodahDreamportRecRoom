#!/bin/bash
# Builds shdis.dll (research only) with Unity's bundled Roslyn against .NET 6.0.21; compiles ../rrtool/Core.cs in by path.
set -u
C="$(cd "$(dirname "$0")" && pwd -W)"
U="C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data"
NC="$U/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
mkdir -p "$C/out"
{ echo "-nologo"; echo "-nostdlib+"; echo "-target:exe"; echo "-langversion:latest"; echo "-optimize+"; echo "-nowarn:0168,0219,8632"
  for f in System.Runtime System.Private.CoreLib System.Collections System.Linq System.Console System.Runtime.Extensions System.Text.Encoding.Extensions System.Text.RegularExpressions System.Memory System.Runtime.InteropServices netstandard; do echo "-r:\"$NC/$f.dll\""; done
  echo "-out:\"$C/out/shdis.dll\""; echo "\"$C/Shdis.cs\""; echo "\"$C/Seg.cs\""; echo "\"$C/../rrtool/Core.cs\""; } > "$C/out/build.rsp"
"$U/NetCoreRuntime/dotnet.exe" "$U/DotNetSdkRoslyn/csc.dll" -noconfig @"$C/out/build.rsp" 2>&1 | grep -E "error" | head -20
echo '{ "runtimeOptions": { "tfm": "net6.0", "framework": { "name": "Microsoft.NETCore.App", "version": "6.0.21" } } }' > "$C/out/shdis.runtimeconfig.json"
ls -la "$C/out/shdis.dll" | awk '{print $5, $9}'
