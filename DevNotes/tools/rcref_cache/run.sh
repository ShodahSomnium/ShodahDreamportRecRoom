#!/bin/bash
# usage: bash run.sh <cmd> ...   (see README.md). RC_CACHE overrides the HTTPCache folder.
exec dotnet "$(cd "$(dirname "$0")" && pwd)/out/bin/rcref_cache.dll" "$@"
