#!/bin/bash
# Turns out/server.rsp (written by build.sh) into an SDK-style csproj that `dotnet build` (the machine's .NET SDK 8/9/10)
# compiles with exactly the same references, defines and C# version - for IDEs, or when the Unity-bundled csc is not wanted.
#   bash build.sh [sample|project|dir X]   # writes out/server.rsp
#   bash make_csproj.sh                    # writes csproj/SomniumRecRoom.CompileCheck.csproj
#   dotnet build csproj/SomniumRecRoom.CompileCheck.csproj -nologo -v q
set -eu
C="$(cd "$(dirname "$0")" && pwd -W)"
RSP="$C/out/server.rsp"; OUT="$C/csproj"; mkdir -p "$OUT"
DEFINES=$(sed -n 's/^-define://p' "$RSP" | tr -d '\r')
{
  echo '<Project Sdk="Microsoft.NET.Sdk">'
  echo '  <PropertyGroup>'
  echo '    <TargetFramework>netstandard2.1</TargetFramework>'
  echo '    <LangVersion>9.0</LangVersion>'
  echo '    <AssemblyName>server_like_sdk</AssemblyName>'
  echo '    <OutputType>Library</OutputType>'
  echo '    <NoStdLib>true</NoStdLib>'
  echo '    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>'
  echo '    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
  echo '    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>'
  echo '    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>'
  echo '    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>'
  echo '    <Nullable>disable</Nullable>'
  echo '    <ImplicitUsings>disable</ImplicitUsings>'
  echo '    <WarningLevel>4</WarningLevel>'
  echo '    <NoWarn>1701;1702;0649;0169;0414</NoWarn>'
  echo "    <DefineConstants>$DEFINES</DefineConstants>"
  echo '  </PropertyGroup>'
  echo '  <ItemGroup>'
  tr -d '\r' < "$RSP" | sed -n 's/^-r:"\(.*\)"$/\1/p' | while read -r p; do
    n=$(basename "$p" .dll)
    echo "    <Reference Include=\"$n\"><HintPath>$p</HintPath><Private>false</Private></Reference>"
  done
  tr -d '\r' < "$RSP" | grep '\.cs"$' | sed 's/^"\(.*\)"$/\1/' | while read -r f; do
    echo "    <Compile Include=\"$f\" />"
  done
  echo '  </ItemGroup>'
  echo '</Project>'
} > "$OUT/SomniumRecRoom.CompileCheck.csproj"
echo "wrote $OUT/SomniumRecRoom.CompileCheck.csproj ($(grep -c '<Reference' "$OUT/SomniumRecRoom.CompileCheck.csproj") references, $(grep -c '<Compile' "$OUT/SomniumRecRoom.CompileCheck.csproj") files)"
