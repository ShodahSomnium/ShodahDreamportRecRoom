# convert: Android level files -> RRSK placement data (started 2026-09-24)

Owner: agent "convert". Owns DevNotes/tools/apkscene/, DevNotes/refdata/apk/, DevNotes/apk/*. No project code edits.
Source: the user's Android XAPK (2026-03-23, APKPure), extracted packs in the session scratchpad (`.../scratchpad/apk`).
DATA ONLY: nothing is installed, executed, loaded or copied into the project; only derived names/numbers are written.

## Status
- [x] 1 reader: tools/apkscene (README there)
- [x] 2 all 114 levels identified by the BuildSettings scene list -> refdata/apk/INDEX.md
- [x] 3 Paintball lobby + 7 maps -> refdata/apk/Paintball_<Map>.rrsk, validated
- [x] 4 41 more rooms converted (Rec Center, Dorm, Dodgeball, Paddleball, Charades x2, Laser Tag x2, Soccer, Disc Golf x2, Bowling, Golden Trophy x11, Isle x9, Rec Rally, Stunt Runner x8)

## Log
- 21:18 reader works: bank = 2,838 type trees from 2,787 PC bundles (10 s); every built-in class in the Paintball levels
  parses by exact type hash; Terrain(218)/TerrainCollider(154)/TerrainData(156)/RenderSettings(104) have no PC tree and
  are read by PPtr scan + fixed layout. Base APK (com.AgainstGravity.RecRoom.apk from the same XAPK) gives
  globalgamemanagers (BuildSettings scene list = level index -> scene path) and globalgamemanagers.assets (MonoScripts).
- 21:18 all 8 Paintball files written (refdata/apk/Paintball_*.rrsk + .md); flag bases match the room exports to
  <= 0.001 m on all 7 maps with a ref; no static batching in the Android build (0 combined meshes).
- 21:50 terrain decode confirmed: heights[z*res+x]; spawn points sit on the terrain (median 0.000 m).
  Meshes not in the install are measured, never copied: ground-like -> H grid; colliders -> voxel-shell boxes;
  unique big structures -> voxel X sets.
- 22:05 other rooms converted (run_rooms.sh), INDEX.md generated (index.sh), H record documented in
  tools/rrscene/README.md. All files pass a field-count check (0 bad lines; 41 H grids with the correct row shapes).

## Paintball results
| file | level | renderers | install-mesh match | colliders (C) | terrain | flag bases vs room export | weapons within 0.5 m |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Paintball_River | 14 | 921 | 57.0% | 2,917 | yes | 0.000 m | 27/27 |
| Paintball_Homestead | 15 | 1,168 | 72.4% | 3,323 | yes | no ref file | - |
| Paintball_Quarry | 16 | 982 | 49.2% | 5,320 | yes | 0.000 m | 28/30 |
| Paintball_Clearcut | 17 | 954 | 67.7% | 3,355 | yes (513 heightmap; first 257x257 array was not it) | 0.000 m | 29/29 |
| Paintball_Spillway | 18 | 1,716 | 44.8% | 7,558 | no (terrain inactive; mesh ground) | 0.000 m | 44/46 |
| Paintball_DriveIn | 103 | 2,567 | 94.4% | 5,215 | yes | 0.001 m | 24/24 |
| Paintball_MerryMidway | 104 | 771 | 41.5% | 3,396 | yes | 0.001 m | 25/25 |
| Paintball_Lobby | 113 | 822 | 56.7% | 2,431 | yes | no flag bases | 1/1 |

## Open points for the kit / game agents
- The kit does not build H yet. The format is in tools/rrscene/README.md (H grid / H row).
- Gameplay prefabs are G-only (`prefab=`); the weapon sets per mode carry `set=` and `active=0`.
- Terrain materials are not in the install: `mat=Goblin_Ground_Green_Mat mat.guess=1` + `layers=` texture names.
