# apkscene: the Android build's level files -> RRSK placement data

Reads the user's Android build of Rec Room (2026-03-23 XAPK from APKPure) as **data only**. Nothing is installed,
executed, loaded into Unity, or copied into the project. The tool writes derived names and numbers (RRSK 1 text) to
`DevNotes/refdata/apk/`. Meshes and materials are named so the scene kit can load them from the player's **PC install**.

## Inputs (all read-only)
| what | where |
| --- | --- |
| `UnityDataAssetPack.apk` (zip, entries stored) | `Paths.Apk` (default: session scratchpad `.../scratchpad/apk`, env `APK_DIR`). Read in place, not extracted: `assets/bin/Data/levelN[.splitK]`, `sharedassetsN.assets[.splitK]`, GUID-named single-asset files, `.resource` streams |
| base APK Data files | `<APK_DIR>/base/`: `globalgamemanagers` (BuildSettings scene list), `globalgamemanagers.assets` (MonoScripts), `unity default resources`, `unity_builtin_extra`, taken from `com.AgainstGravity.RecRoom.apk` in the same XAPK with `unzip -j` |
| PC install bundles | `E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64` (type trees, mesh/material names) and `UnityPlayer.dll` (common strings) |
| rrtool index | `objects.tsv` (prefab roots, AudioClips), `mbscripts.tsv` (script names); env `RRTOOL_IDX` |

## Build and run
`bash build.sh` (Unity's bundled Roslyn + .NET 6.0.21; compiles `../rrtool/Core.cs` in by path). `bash run.sh <cmd> <work> ...`,
where `<work>` is a scratch folder for `bank.bin`, `inst_meshes.tsv`, `inst_materials.tsv`.

| command | what | time |
| --- | --- | --- |
| `bank <work>` | type-tree bank from every PC bundle, keyed by (class id, old type hash) and by MonoBehaviour script id, plus script id -> name; install mesh index (name, vertex count, submesh index/vertex counts, AABB) and material names | 10 s |
| `info <work> <file>` | header, externals (present or not), per type: class, script, where its tree came from (`hash`, `script`, `class`, `NONE`), read errors | <1 s |
| `stats <work> levelN` / `find <work> levelN <scriptRe> [pathRe]` / `dump <work> <file> <pathId>` | debugging | <1 s |
| `scan <work> <outDir>` | all 114 levels -> `_levels.tsv` (scene path, GOs, renderers, install-mesh match, colliders, terrain, roots, sign texts, gameplay scripts) | 40 s |
| `convert <work> <outDir> levelN <Scene> [ref=<gameplay.rrsk>] [scene=<path>]` | `<Scene>.rrsk` + `<Scene>.md`, row in `_converted.tsv`; with `ref=` a frame check of flag bases and weapon pickups | 1-3 s |
| `hcheck <work> levelN` | terrain decode check: spawn point heights against the heightmap in both index orders | <1 s |
| `bash run_paintball.sh <work>`, `bash run_rooms.sh <work>`, `bash index.sh` | the Paintball lobby + 7 maps, the other rooms, `INDEX.md` | |

## How the files are read
- **Serialized files.** Format 22, Unity 6000.0.27f1, platform 13, **no type trees**. Every type entry still carries its
  16-byte old type hash (and a script id for MonoBehaviours). The same hash in a PC bundle means the same layout, so the
  bank's tree reads the Android object exactly. Every object read must consume exactly its byte size.
  - All built-in classes in the Paintball levels read this way: GameObject, Transform, RectTransform, MeshFilter,
    MeshRenderer, SkinnedMeshRenderer, the colliders, Light, LODGroup, AudioSource, Mesh, Material, Texture2D, and others.
  - Scene-only MonoBehaviour scripts that are not in any PC bundle have no tree. They are named through `m_Script` ->
    MonoScript in `globalgamemanagers.assets`.
- **No PC tree:** Terrain (218), TerrainCollider (154), TerrainData (156), RenderSettings (104).
  - Terrain and TerrainCollider: the TerrainData and material are found by scanning for PPtrs that resolve to class 156
    or 21.
  - TerrainData: `m_Heights` is found as an int16 array of R*R with R = 2^k+1. The fields after it are checked (holes,
    LOD, error arrays, `m_Resolution == R`, `m_Levels`, `m_Scale`). The terrain layers are the PPtr list after the name.
  - RenderSettings: its leading fields (fog, ambient, skybox) have a stable layout.
- **Heights** are `heights[z * res + x] / 32766 * scale.y + terrain.y`. `hcheck` confirms this order: the median spawn
  point sits 0.000 m off the terrain on River, Homestead, Drive-In and Merry Midway, 0.064 m on Quarry and 0.139 m on Clearcut (some of its spawns stand on structures). The other
  index order is off by 0.8 to 1.2 m.
- **Externals** are resolved by file name: `sharedassetsN.assets`, the GUID-named single-asset files,
  `globalgamemanagers.assets`, and `Library/unity default resources`. Split files are joined in `.split0..k` order.
- **Level names**: level N is BuildSettings scene N (114 scenes, level0 = `main_root.unity`).
- **Static batching**: this build has none. No renderer points into a `Combined Mesh`, and every renderer keeps its
  own mesh. Some Rec Room scenes contain editor-merged meshes instead, such as `DriveIn.DriveIn.Locales-24` and
  `...StaticBatch_<material>`. Those are not in the install and are handled as unmatched meshes.

## What is written (RRSK 1, see `../rrscene/README.md`; H record documented there)
- **M / T / R**: an active, enabled, LOD0 renderer becomes an R record when its mesh name is an install Mesh name
  (exact, else by letter case). A material name that is not an install Material is written `?name`.
- **X stand-ins** for meshes that are not in the install:
  - A thin, flat, visual-only mesh (water planes) becomes one box.
  - A ground-like mesh becomes an H grid. Ground-like means at least 24 x 24 m and at least 60% of its triangle area
    facing up.
  - A unique big structure (over 6 m, used at most twice in the scene) becomes a set of boxes from a coarse voxel shell
    (at most 60 boxes).
  - Everything else becomes the box of its mesh bounds, in the material's colour.
- **P**: a GameObject named `(X)` or `[X]` (with ` (n)`, ` Variant` and `(Clone)` stripped) that is an install prefab
  root, with renderers in its scene subtree. Its subtree is not expanded.
  - Gameplay prefabs are **G only**, not P: flag bases, flags, weapons, standees, scoreboards, leaderboards, game
    configs and rules, and stores. The game spawns those itself. Their G carries `prefab=(X)`.
- **C** colliders exactly as the level has them: box, sphere, capsule, and mesh when the mesh is in the install.
  A MeshCollider whose mesh is **not** in the install is measured:
  - ground-like: an H grid with `collider=1`;
  - otherwise: boxes from a voxel shell (0.5 to 2 m voxels, at most 120 boxes).

  A `# C from a ... voxel shell` comment precedes each such set. Only the resulting numbers are written, never
  vertices or indices. Trigger colliders become G records with `shape=`.
- **G** markers:
  - kinds from scripts and prefab names: `spawn` (GamePlayerSpawnPoint; `team=blue|red|pregame` from the path),
    `flag_base`, `flag`, `weapon_pickup` (`set=` the weapon group, e.g. `Weapons`, `ShotgunsOnlyWeapons`,
    `FiestaWeapons`; `active=0` for the sets that start disabled), `standee`, `scoreboard`, `leaderboard`,
    `game_config`, `game_rule`;
  - volumes (`shape=box size=` from the object's own collider): `bounds`, `team_zone`, `spawn_area` (OverlapVolume),
    `kill_zone`, `teleport_block`;
  - others: `spectator`, `store`, `mod_portal` (the Paintball mod portals), `trigger_zone`.

  Every G has `path=` (the scene path, last 120 characters).
- **H** terrain and ground grids. Inside the play area (spawns, flag bases, weapons and bounds, plus 30 m) cells are
  at most 2 m, with `collider=1` where the level has a collider. A large grid also gets a visual-only `~far` grid
  (`far=1`, up to 64 cells a side).
- **L, A, E** as rrscene does: lights, audio (install clip names or `?name`), fog, ambient and sky.

## Results (2026-09-24)
See `../../refdata/apk/INDEX.md`. For the Paintball maps, the flag bases match the room exports
(`refdata/Paintball_<Map>_gameplay.rrsk`) to within 0.000-0.001 m on all 7 maps that have a reference. The weapon
pickups match to within 0.5 m: 27/27, 28/30, 29/29, 44/46, 24/24 and 25/25.

## Lighting environment (iteration 8, 2026-09-25)

| command | what | time |
| --- | --- | --- |
| `env <work> <outDir> levelN=Scene ...` | per level `<outDir>/<Scene>.env` + `_env.tsv`: RenderSettings in full, the sky material (shader + every property), every enabled light with shadow type/strength and bake mode, lightmap use, reflection probes, light probe groups | 16 s for 61 levels |
| `envcs <work> <envDir> <out.cs> [extraDir...]` | `Assets/#User/SomniumRecRoom/Core/RrEnvironmentTable.cs`: one line per scene for RrEnvironment (format in RrEnvironmentSetting.cs); extra `.env` files (hand-made from REF2 E records and the cache research) in `refdata/env_extra` | 1 s |
| `cubeavg <work> levelN...` | the sky cubemap's per-face mean colour (below) | <1 s |
| `envprobe`, `envwords`, `envtex` | debugging: first light/renderer/probe dicts, RenderSettings raw words, the sky material's textures | |

Run: `bash run.sh env <work> ../../refdata/apk/env $(awk -F'\t' '{print $1"="$2}' ../../refdata/apk/_converted.tsv | sort -u)`, then
`bash run.sh envcs <work> "D:/Dreamport/Rec Room/Rec Room/DevNotes/refdata/apk/env" "D:/Dreamport/Rec Room/Rec Room/Assets/#User/SomniumRecRoom/Core/RrEnvironmentTable.cs" "D:/Dreamport/Rec Room/Rec Room/DevNotes/refdata/env_extra"`.

**RenderSettings (class 104) in the player format** is read raw (no PC tree). Measured on level113, 333 bytes, every
byte consumed: fog (bool, colour, mode, density, start, end), ambient sky/equator/ground colours, intensity, mode,
subtractive shadow colour, skybox PPtr, halo strength, flare strength, flare fade, halo texture PPtr, spot cookie PPtr,
default reflection mode (1 = custom), resolution, bounces, reflection intensity, custom reflection PPtr, then the
player-only part: the ambient probe (27 floats, SH L2 channel-major), the generated skybox reflection PPtr, the sun PPtr,
the indirect specular colour and one bool.

**Findings.** None of the 61 converted levels has a lightmap (every renderer's m_LightmapIndex is 65535) and every
light is realtime (m_Lightmapping 4). Skies: Skybox/Cubemap with a baked cubemap (Paintball, Dorm, gyms, Rec Center,
Disc Golf, Soccer), Skybox/Procedural (quests, Charades, Bowling, Laser Tag), AG/Skybox/Simple Gradient (Stunt Runner),
Custom/SkyBox (Drive-In, Merry Midway, Rec Rally).

**Cubemap colours without an ASTC decoder.** The Android cubemaps are ASTC 8x8 (format 51) or 6x6 (50), RGBM
(texture usage 5), faces stored one after another with their whole mip chains (+X -X +Y -Y +Z -Z). The last mip of each
face is 1x1, one 16-byte block, and the encoder writes a single texel as a void-extent block (bits 0-8 = 0x1FC) whose
constant colour is four UNORM16 values at bytes 8-15. RGBM x 5 of that colour is the face's mean. Check: the Dorm's
SkyboxDormRoom_Baked decodes to face means within 0.011 (RMS) of REF2's `Dormroom2_Baked_Skybox.png`.

### E records (iteration 8)
`convert` now also writes (older kits skip unknown E kinds and ignore keys after an L record's numbers):
```
E refl intensity=<reflection intensity> mode=<skybox|custom>
E sun px py pz qx qy qz qw r g b intensity shadows=<none|hard|soft> strength=<s>   (RenderSettings.sun, else the brightest enabled directional light)
E sun none
L ... <13 numbers> shadows=<hard|soft> strength=<s>                               (only on lights that cast shadows)
```
The `.env` files add `E skymat <material> shader=<shader> <prop>=<value>...`, `E light ...` (every light, with
`shadows= strength= bake= bounce= name=`), `E probe ...`, `E bake lightmapped=/ maps= lights=rt,mixed,baked ...` and
`E shprobe <27 floats>`.

## Rec Room's lights and torches (iteration 9, 2026-09-25)

| command | what | time |
| --- | --- | --- |
| `tree <work> levelN <nameRe> [max]` | the subtrees of the GameObjects whose name matches: world pose, active flag, and per component Light (type, colour, intensity, range, spot/inner, shadows, lightmapping), renderers (mesh, materials, cast shadows), particle systems, scripts with their scalar fields (URPLight's specularContribution / isHemisphereLight / hemisphere angles) | 1 s |
| `mat <work> levelN <matNameRe>` | every material of that name the level's renderers use: shader, keywords, colours (emission), floats, texture slots | 1 s |
| `scripts <work> <scriptRe> [fileRe]` | every MonoBehaviour in the Android Data files whose script name matches (PC type trees only; URP assets have none) | 1-30 s |

- `E light` lines (env) and `L` records (convert) now end with Rec Room's URPLight values when the light has one:
  `spec=<specularContribution>`, `hemi=1 hin=<inner> hout=<outer>` for hemisphere lights, and `ispot=<inner spot angle>`
  for spots. envcs writes them per scene as `urp=x,y,z,spec,hemi,hin,hout,ispot;...` (lights that are not plain, and
  every spot) into RrEnvironmentTable. 2,784 of the 2,810 lights in the 61 levels carry URPLight: 2,560 with specular
  contribution 0, 967 hemisphere lights.
- Example: `bash run.sh tree <work> level19 "lantern|torch" 50` shows the Golden Trophy lobby's torches: `[WallTorch]`
  (WallTorch_LOD0 + two FlamePlane cards PaperFlame_Mat + a `[VFX_Flame]` square-particle system), their lights in
  `Systems/FoyerLighting` ("Torch Hot Spot" 1 / 1.41 m and "Torch Light" 0.85 / 5.8 m, orange, realtime, spec 0) and
  `[Lantern_Post_Wooden]` with its "EmitCubeCandle" hemisphere light (34/133 degrees, facing down).
- The install shaders' own light path (why Rec Room's surfaces get no point light in the client) was read with the
  scratchpad tool `DevNotes/tools/shdis` (DXBC disassembly of the install's compiled shaders): DevNotes/iter9/light9.md.

## Material descriptions (matdesc, 2026-11-22)

| command | what | time |
| --- | --- | --- |
| `matdesc <work> levelN <layout.rrsk or .bytes> [out=<file>] [tsv=<rows.tsv>] [scene=<name>]` | adds `K <t> ...` records (one per '?name' T entry, right after the last T line), ` gcol=r,g,b` on H grid lines whose mat is a guess or '?name', and ` avg=r,g,b itex=0\|1` on `G platemat` lines with inst=0 or a non-install tex. Every other line stays byte-identical; a re-run first removes what an earlier run added. Without `out=` it only prints. Reads gzip or plain text, writes plain text (gzip it with `gzip -9 -n`). | 1-3 s |
| `matcheck <work> <original> <rewritten>` | the rewritten file = the original + K lines + those keys only (exit 1 otherwise) | <1 s |
| `matreport <work> <rows.tsv> <out.md>` | the sanity-check tables | <1 s |
| `shaderdbg` / `glsl <work> levelN <matRe> [passRe]` / `vcdbg` / `vchist` / `layerdbg` | debugging: shader parsed form, the GLES3 program's colour/vertex-colour use, a mesh's vertex channels, terrain layers | |

Key definitions, the decisions (GLES3 GLSL as evidence for colour property, main texture, alpha test, vertex colours,
emission, hidden shaders) and the results: `../../refdata/matdesc_2026-11-22.md`. Texture means are measured from the
top mip (ASTC via `Astc.cs`, ETC1/2 and uncompressed via `MatTex.cs`) in memory only.
