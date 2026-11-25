#!/bin/bash
# Builds apkscene.dll with Unity's bundled Roslyn against Unity's bundled .NET 6.0.21 (no SDK / nuget needed).
# Compiles rrtool's bundle reader (../rrtool/Core.cs: BR, TNode, Common, Bundle, Lz4Dec, ClassNames) in by path.
set -u
C="$(cd "$(dirname "$0")" && pwd -W)"
U="C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data"
DN="$U/NetCoreRuntime/dotnet.exe"; CSC="$U/DotNetSdkRoslyn/csc.dll"
NC="$U/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
mkdir -p "$C/out"
{ echo "-nologo"; echo "-nostdlib+"; echo "-target:exe"; echo "-langversion:latest"; echo "-optimize+"; echo "-nowarn:0168,0219,8632"
  for f in System.Runtime System.Private.CoreLib System.Collections System.Linq System.Console System.Runtime.Extensions System.Text.Encoding.Extensions System.Text.RegularExpressions System.Memory System.IO.Compression netstandard; do echo "-r:\"$NC/$f.dll\""; done
  echo "-out:\"$C/out/apkscene.dll\""
  for f in Zip Af Geo Ld Inst Scene Shape Emit Conv Env EnvCs Astc Tree MatDesc MatTex MatRun MatDbg Main; do echo "\"$C/$f.cs\""; done
  echo "\"$C/../rrtool/Core.cs\""; } > "$C/out/build.rsp"
"$DN" "$CSC" -noconfig @"$C/out/build.rsp" 2>&1 | grep -E "error|warning" | head -30
cat > "$C/out/apkscene.runtimeconfig.json" <<'JSON'
{ "runtimeOptions": { "tfm": "net6.0", "framework": { "name": "Microsoft.NETCore.App", "version": "6.0.21" } } }
JSON
ls -la "$C/out/apkscene.dll" | awk '{print $5, $9}'
