#!/bin/bash
# Writes a Unity .meta (fresh GUID) next to every .cs/.shader/.asmdef/folder under the given root that has none.
# Usage: bash metas.sh ["D:/Dreamport/Rec Room/Rec Room/Assets/#User/SomniumRecRoom"]
ROOT="${1:-D:/Dreamport/Rec Room/Rec Room/Assets/#User/SomniumRecRoom}"
newguid() { powershell -NoProfile -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r\n'; }
n=0
while IFS= read -r -d '' f; do
  [ -f "$f.meta" ] && continue
  g=$(newguid)
  if [ -d "$f" ]; then
    printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$g" > "$f.meta"
  else
    case "$f" in
      *.asmdef) printf 'fileFormatVersion: 2\nguid: %s\nAssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$g" > "$f.meta" ;;
      *.cs|*.shader|*.hlsl|*.md) printf 'fileFormatVersion: 2\nguid: %s\n' "$g" > "$f.meta" ;;
      *) continue ;;
    esac
  fi
  n=$((n+1)); echo "meta: $f -> $g"
done < <(find "$ROOT" -mindepth 1 \( -type d -o -name "*.cs" -o -name "*.shader" -o -name "*.hlsl" -o -name "*.asmdef" -o -name "*.md" \) -print0)
echo "$n meta file(s) written"
