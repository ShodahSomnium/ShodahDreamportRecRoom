#!/bin/bash
# Offline compile check for the Rec Room Dreamport world, outside Unity.
#
#   bash build.sh                 -> compiles sample/*.cs                       -> out/server_like.dll
#   bash build.sh project         -> compiles Assets/#User/SomniumRecRoom/**.cs (no Editor/, no Tests/) + nothing else
#   bash build.sh dir <folder>    -> compiles every .cs under <folder>
#   bash build.sh ... --editor    -> also compiles <root>/Editor/**.cs against server_like.dll + UnityEditor (out/editor_like.dll)
#
# "server-like" = the reference list the Somnium upload server resolved for AoM (Scripting/logs.txt, 2026-09-02) and
# ETS (2026-09-23): Fusion.*, NanoSockets, Newtonsoft.Json, Photon3Unity3D, SomniumSpace.Bridge, SomniumSpace.Network,
# URP Core/Universal Runtime, Unity.TextMeshPro, Unity.XR.Interaction.Toolkit, UnityEngine.UI, websocket-sharp and every
# UnityEngine.*Module listed in the log. NOT on it (so NOT referenced here): Unity.InputSystem, Unity.XR.CoreUtils,
# CommunityModules, AsyncIO, NetMQ, SomniumSpace.SDK/Bundles/Common/Helpers/User, Meshia. No UNITY_EDITOR define.
# C# 9 (Unity's default; the server's sanitized csproj is assumed to use the same - a guess, but every sibling world
# compiled as C# 9 and passed).
#
# After a clean compile the Fusion/Somnium member guard runs (guard/RefDump.dll + guard/known_good.txt, copied from the
# ETS session's scratchpad fusionapi/): the server's Fusion is OLDER than the local 2.0.12, so any member not used by a
# world that already passed the server (AoM, Quake, Blades Edge, ETS) is printed as UNPROVEN.
set -u
C="$(cd "$(dirname "$0")" && pwd -W)"   # -W = Windows path: csc cannot see Git Bash's /tmp mapping
U="C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data"
RR="D:/Dreamport/Rec Room/Rec Room"
AOM="D:/Dreamport/Age Of Mythology"          # Fusion + SomniumSpace.Network until the RR project imports Photon itself
DN="$U/NetCoreRuntime/dotnet.exe"
CSC="$U/DotNetSdkRoslyn/csc.dll"
REF="$U/UnityReferenceAssemblies/unity-4.8-api"
mkdir -p "$C/out"

MODE="${1:-sample}"; EDITOR=0
for a in "$@"; do [ "$a" = "--editor" ] && EDITOR=1; done
case "$MODE" in
  sample)  ROOT="$C/sample" ;;
  project) ROOT="$RR/Assets/#User/SomniumRecRoom" ;;
  dir)     ROOT="$2" ;;
  *)       echo "usage: build.sh [sample|project|dir <folder>] [--editor]"; exit 2 ;;
esac

# ---- references: the server list, mapped to local files -------------------------------------------------------
RSP="$C/out/server.rsp"
{
  echo "-nologo"; echo "-nostdlib+"; echo "-target:library"; echo "-langversion:9"; echo "-warn:4"
  echo "-nowarn:1701,1702,0649,0169,0414"
  echo "-define:UNITY_STANDALONE_WIN;UNITY_STANDALONE;UNITY_64;UNITY_6000_0_OR_NEWER;UNITY_6000_3_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2023_1_OR_NEWER;ENABLE_LEGACY_INPUT_MANAGER;ENABLE_VR;ENABLE_PHYSICS;ENABLE_AUDIO;CSHARP_7_3_OR_NEWER"
  for f in mscorlib System System.Core System.Xml System.Xml.Linq System.Numerics System.Runtime.Serialization System.IO.Compression Facades/netstandard Facades/System.Runtime; do
    echo "-r:\"$REF/$f.dll\""
  done
  # every UnityEngine module the server resolved (read from the AoM log, the fullest list we have)
  tr -d '\r' < "$AOM/Scripting/logs.txt" | sed -n 's#^Resolved reference ~ServerReferences\\UnityEngine\\\(.*\)$#\1#p' | while read -r m; do
    [ -f "$U/Managed/UnityEngine/$m" ] && echo "-r:\"$U/Managed/UnityEngine/$m\"" || echo "# missing locally: $m" >&2
  done
  # ScriptAssemblies the server resolved
  echo "-r:\"$RR/Library/ScriptAssemblies/UnityEngine.UI.dll\""
  echo "-r:\"$RR/Library/ScriptAssemblies/Unity.TextMeshPro.dll\""
  echo "-r:\"$RR/Library/ScriptAssemblies/Unity.XR.Interaction.Toolkit.dll\""
  echo "-r:\"$RR/Library/ScriptAssemblies/Unity.RenderPipelines.Core.Runtime.dll\""
  echo "-r:\"$RR/Library/ScriptAssemblies/Unity.RenderPipelines.Universal.Runtime.dll\""
  echo "-r:\"$RR/Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll\""
  echo "-r:\"$RR/Assets/Plugins/SomniumSpace/Bridge/SomniumSpace.Bridge.dll\""
  # Fusion 2.1.3 (2026-10-20): this project's own Photon, and Fusion.Unity / SomniumSpace.Network / Photon.Realtime as
  # Unity built them in this project (open the project once after a Photon change so Library/ScriptAssemblies is current).
  for f in Fusion.Common Fusion.Log Fusion.Realtime Fusion.Runtime Fusion.Sockets; do echo "-r:\"$RR/Assets/Photon/Fusion/Assemblies/$f.dll\""; done
  echo "-r:\"$RR/Library/ScriptAssemblies/Fusion.Unity.dll\""
  echo "-r:\"$RR/Library/ScriptAssemblies/SomniumSpace.Network.dll\""
  echo "-r:\"$RR/Library/ScriptAssemblies/Photon.Realtime.dll\""
  echo "-r:\"$RR/Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.dll\""
  echo "-r:\"$RR/Assets/Photon/PhotonLibs/WebSocket/websocket-sharp.dll\""
  echo "-r:\"$RR/Assets/Photon/Fusion/Plugins/NanoSockets/NanoSockets/Dynamic/NanoSockets.dll\""
  echo "-out:\"$C/out/server_like.dll\""
  find "$ROOT" -name "*.cs" -not -path "*/Editor/*" -not -path "*/Tests/*" | sed 's#.*#"&"#'
} > "$RSP"

rm -f "$C/out/server_like.dll"
"$DN" "$CSC" -noconfig @"$RSP" > "$C/out/server_out.txt" 2>&1
ERR=$(grep -c "error CS" "$C/out/server_out.txt"); WARN=$(grep -c "warning CS" "$C/out/server_out.txt")
echo "server-like build of $ROOT: $ERR error(s), $WARN warning(s)  ($(grep -c '\.cs"$' "$RSP") files)"
grep "error CS" "$C/out/server_out.txt" | sed 's#^.*[\\/]##' | sort -u | head -20
[ -f "$C/out/server_like.dll" ] || exit 1

# ---- the server-API guard ----------------------------------------------------------------------------------------
"$DN" "$C/guard/RefDump.dll" "$C/out/server_like.dll" | tr -d '\r' | LC_ALL=C sort -u > "$C/out/api_current.txt"
tr -d '\r' < "$C/guard/known_good.txt" | LC_ALL=C sort -u > "$C/out/api_known.txt"
NEW=$(LC_ALL=C comm -13 "$C/out/api_known.txt" "$C/out/api_current.txt")
if [ -n "$NEW" ]; then echo "UNPROVEN server API (no world that passed the server uses it):"; echo "$NEW"; GUARD=1
else echo "Server API guard: all $(wc -l < "$C/out/api_current.txt") Fusion/Somnium members are proven."; GUARD=0; fi

# ---- optional editor builds (ETS keeps three lists: editor_like runtime, server_like runtime, editor tools) ------------
if [ "$EDITOR" = 1 ]; then
  EDEF="-define:UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_STANDALONE_WIN;UNITY_STANDALONE;UNITY_64;UNITY_6000_0_OR_NEWER;UNITY_6000_3_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2023_1_OR_NEWER;ENABLE_LEGACY_INPUT_MANAGER;ENABLE_VR;ENABLE_PHYSICS;ENABLE_AUDIO;CSHARP_7_3_OR_NEWER"
  EREFS="-r:\"$U/Managed/UnityEngine/UnityEditor.dll\""$'\n'"-r:\"$U/Managed/UnityEngine/UnityEditor.CoreModule.dll\""
  # 1. the same runtime sources with UNITY_EDITOR (catches broken #if UNITY_EDITOR blocks)
  { grep -v '^-out:\|^-define:' "$RSP"; echo "$EDEF"; echo "$EREFS"; echo "-out:\"$C/out/editor_like.dll\""; } > "$C/out/editor_runtime.rsp"
  rm -f "$C/out/editor_like.dll"
  "$DN" "$CSC" -noconfig @"$C/out/editor_runtime.rsp" > "$C/out/editor_runtime_out.txt" 2>&1
  echo "editor-like runtime build: $(grep -c 'error CS' "$C/out/editor_runtime_out.txt") error(s)"; grep "error CS" "$C/out/editor_runtime_out.txt" | head -10
  # 2. the Editor folder (the world's *.Editor asmdef) against it
  if [ -d "$ROOT/Editor" ] && [ -f "$C/out/editor_like.dll" ]; then
    { grep -v '^-out:\|\.cs"$\|^-define:' "$RSP"; echo "$EDEF"; echo "$EREFS"
      echo "-r:\"$C/out/editor_like.dll\""; echo "-out:\"$C/out/editor_tools.dll\""
      find "$ROOT/Editor" -name "*.cs" | sed 's#.*#"&"#'; } > "$C/out/editor_tools.rsp"
    "$DN" "$CSC" -noconfig @"$C/out/editor_tools.rsp" > "$C/out/editor_tools_out.txt" 2>&1
    echo "editor tools build: $(grep -c 'error CS' "$C/out/editor_tools_out.txt") error(s)"; grep "error CS" "$C/out/editor_tools_out.txt" | head -10
  fi
fi
exit $GUARD
