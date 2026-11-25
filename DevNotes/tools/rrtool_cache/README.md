# rrtool_cache: rrtool variant that reads the user's Rec Room HTTPCache (measurements only)

A copy of `../rrtool` with these additions (the defaults are unchanged, so it also still reads the install):
* `RR_BUNDLE_DIR` / `RR_IDX_DIR` environment overrides; `*.data` files that start with "UnityFS" are accepted.
* An LZMA block decoder (`Lzma.cs`): some cache bundles (Unity 2021.3.29f1, Rec Room Studio) use LZMA blocks.
* `bundle#k` selects the k-th serialized file of a bundle (scene bundles: `#0` = `.sharedassets`, `#1` = the scene).
* `world <bundle#k>` = every GameObject with WORLD TRS, mesh/material names, world AABB (static-batched: from the
  combined mesh's submesh AABBs), lights and box colliders. `mats <bundle>` = materials with shader, colours, textures.
* `roomframe.pl` = converts `world` output of the Rec Center (1AF) into the room frame of
  `DevNotes/research/rr_reccenter_cache.md` section 1.1.

Build: `dotnet build -c Release -o <outdir>` (a separate folder; keep it out of `../rrtool` so the two don't share
one build). Nothing read with this tool is loaded, copied or shipped: the cache is a measurement reference only.
