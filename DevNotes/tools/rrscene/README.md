# rrscene: reference scenes to RRSK 1 placement text

This tool reads the AssetRipper-style Unity YAML scenes in the user's reference folder (REF2 =
`D:\Dreamport\EXTERNAL FILES FOR REFERNCE\Downloads\RecRoomAssets`) and the decoded room-export tables (REF1) **read-only**.
It writes derived placement data only: names and numbers, in RRSK 1 text. It never copies a reference file anywhere and
never touches the Unity project. It reads the install through rrtool's reader (`../rrtool/Core.cs`, compiled in by path)
and only opens the plain Addressables bundles, never the encrypted level files.

## Build
`bash build.sh` compiles `out/rrscene.dll` with Unity's bundled Roslyn, targeting Unity's bundled .NET 6.0.21. No SDK is needed.
Run it with `"C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe" out/rrscene.dll <cmd> ...`.

## Run (order matters; `<work>` is a scratch dir for caches)
| step | command | output |
| --- | --- | --- |
| 1 | `inst <work>` (~25 s) | `inst_renderers.tsv` (bundle, rootPid, prefab root, path, mesh, materials per submesh), `inst_materials.tsv` (name, colour, main texture, shader). Uses rrtool `idx/*.tsv` (path in `Inst.cs`). |
| 2 | `scan <work> [fileRegex]` (~30 s) | `guidmap.tsv` (every REF2 `.meta` guid -> file, built on first use), `matvotes.tsv` (scene material GUID -> install material name by vote), `scriptlabels.tsv` (MonoBehaviour script GUID -> count, gameplay label, field names, top GameObject names) |
| 3 | `write <work> <outDir> [fileRegex]` (~30 s) | `<outDir>/<scene>.rrsk`, `<scene>.md`, `INDEX.md`, `_scripts.tsv`; `<work>/stats/*.tsv`. INDEX is rewritten after each scene, so a partial run still leaves a usable index. |
| 1b | `instfx <work>` (~12 s, iteration 5) | `inst_fx.tsv`: every install ParticleSystem (`ps`: bundle, root pathId, root name, root is a catalog prefab root, path, materials, looping, duration, max particles, rate, prewarm, start size, start lifetime) and AudioSource (`as`: clip, volume, min, max, loop, play on awake, spatial blend). 13 bundles, 1,471 systems, 107 sources. `write` needs it for F records. |
| - | `psdump <work> <sceneRegex> <nameRegex>` | debug: the particle-system values the F pass reads for matching objects |
| 4 | `paintball <tsvDir> <exDir> <outDir> [spawnable.tsv]` | `Paintball_<Map>_gameplay.rrsk` from roomexport `table` TSVs (`paintballtro__<id>_<Map>.tsv`), each subroom's `PrefabIds.csv`, and `../roomexport/spawnable_typeids_from_bundles.tsv` |

The scene name is the file stem after the first `-`, for example `APPPLESARCHIVE-GTact3.unity` becomes `GTact3`. Set
`RRSCENE_DEBUG=1` to add ` #meshname` to X lines. Those lines are for debugging only and are not valid RRSK.

## How references are resolved (REF2 facts)
- **Meshes.** A scene's `m_Mesh {fileID: 4300000, guid}` resolves through the `.meta` files to `.assets/<name>.asset`. All 6.4k of these are Mesh YAML. The tool reads `m_Name` and the union of the submesh `localAABB` values from the first 64 KB.
  - About 40% of the mesh GUIDs have no `.meta`. For those, and for static-batched `Combined Mesh (root: scene)` references, the GameObject name stands in for the mesh name (with ` (n)` and `(Clone)` stripped).
  - Bounds then come from `.assets/<name>.asset`, or from `Mesh/<name>.glb` and `<name>_0.glb`, `_1.glb` and so on.
  - The `.glb` files are named by **mesh name**, not by GUID. They are SharpGLTF exports: the node name is the mesh name and the sub-mesh nodes are `SubMesh_i`. The tool reads the POSITION accessor min/max and mirrors X back to Unity.
  - GUID `0000000000000000e000000000000000` is Unity's built-in resources (10202 Cube, 10206 Cylinder, 10207 Sphere, 10208 Capsule, 10209 Plane, 10210 Quad). These become exact X stand-ins.
- **Materials.** `.mat` files have no `.meta`, so only about 5 of 1,271 scene material GUIDs resolve directly. The rest are inferred by vote: when a renderer's mesh matches an install mesh, the install's material at that submesh slot is one vote for that material GUID. A GUID gets a name at a 50% share or higher. Otherwise the tool uses the most common install material for that mesh and slot. Unknown materials are written as `?<guid>`.
- **MonoBehaviours.** They carry no fields: the scripts were stripped. The script GUIDs are not AssetRipper's MD5 of the install script names (tested). Script GUIDs are labelled by voting over their GameObject names (spawn, checkpoint, door, teleport, pickup...). Every GameObject using a labelled script gets a G record (`src=script`).
- **Prefabs.** A GameObject becomes one P record, and its whole subtree is skipped, in these cases:
  - Its normalised name is an install `GameObject*` root in `(X)` or `[X]` form.
  - Its name is a plain root name, and at least half of its subtree's renderers use meshes found in that install prefab.
  - Scene copies of spawnables are named `[X]` where the install root is `(X)`. These map to `P (X)`.
- **PrefabInstance.** These occur only in MakeItToMidnight (1,605) and once in RecRoyale. The source prefabs are not in REF2. The root placement is taken from the modifications. The instance becomes `P` when its name is an install prefab, otherwise `G prefab_instance <name> ... src=prefab:<guid>`.
- **Skipped renderers.** Renderers listed under LOD1+ in a LODGroup, disabled renderers, and renderers on inactive objects are skipped and counted. Trigger colliders become `G <kind|trigger_zone>` records with `shape=` keys.

## RRSK 1 format (one file per scene, UTF-8, one record per line, space separated, floats up to 4 decimals, spaces in names as %20, '%' as %25)
```
# RRSK 1 <scene name>
B minx miny minz maxx maxy maxz                 # bounds of everything (incl. sky domes / far terrain if present)
M <i> <install mesh name>                       # mesh table (install Mesh asset name, exact)
T <i> <install material name>                   # material table (install Material name; '?<guid|name>' = unknown)
R <m> <t[,t...]> px py pz qx qy qz qw sx sy sz   # renderer: mesh index, material per submesh, WORLD TRS (lossy scale)
P <install prefab root name> px py pz qx qy qz qw sx sy sz
X <box|cyl> cx cy cz ex ey ez qx qy qz qw r g b a  # stand-in: world centre, half extents, rotation, colour
                                                # (cyl = cylinder along local Y, radius ex, half height ey)
C box cx cy cz qx qy qz qw sx sy sz | C mesh <m> px py pz qx qy qz qw sx sy sz
C sphere cx cy cz r | C capsule cx cy cz qx qy qz qw r h   # capsule along local Y of the given rotation
L <point|spot|dir|area> px py pz qx qy qz qw r g b intensity range spotAngle
A <install AudioClip name | ?name> px py pz volume minDist maxDist loop [was=<scene clip>] [roll=1|2] [pan=0..1]
F <install effect prefab> px py pz qx qy qz qw scale key=value...   # level effect (iteration 5, below)
G <kind> <name> px py pz qx qy qz qw [key=value...]
E fog <linear|exp|exp2|off> r g b density start end | E ambient r g b [mode= intensity= equator= ground=] | E sky <name>
```
Lines starting with `#` are comments. `#U <name> px py pz qx qy qz qw sx sy sz` marks a renderer whose mesh is
unmatched and whose size is unknown (it has no mesh asset, glb or box collider). Treat it as a hint only. The frame is
the scene's stored Unity world space (Y up). Nothing is re-centred.

G kinds in scenes: spawn, respawn, enemy_spawner, checkpoint, flag_base, weapon_rack, lever, button, teleport, kill_zone,
win_zone, door, trigger_zone, pickup, boss, prefab_instance. G kinds in the paintball files: flag_base, flag,
weapon_pickup, standee, game_config, game_rule, role, role_map, scoreboard, leaderboard, door, and extra (player additions).

## F records: level effects (iteration 5)

One F per **effect root** of a reference scene: a ParticleSystem object together with the ParticleSystem objects
above it (nested systems are one effect). The pose is the effect root's world pose (position, rotation) and the scale
is its lossy world scale (largest axis). Effects are placed from the install's own effect prefabs; nothing new is drawn.

Which effects get an F:
- **Ambient only**: at least one system of the effect loops and plays on awake, and the object is active. Effects that
  gameplay triggers (smash puffs, confetti, fireworks, a waterfall that starts in a cinematic, inactive campfires in
  Rec Royale) are listed in the scene's `.md` ("Triggered by gameplay or inactive"), not placed.
- **Not inside a P prefab**: a prefab the kit spawns brings its own particle systems.

Matching, in order (the `.md` lists every effect with its match or why it was skipped):
1. `match=exact`: the effect root's name is an install prefab root with particle systems (`[X]`, `(X)`, or `[X]` whose root is `(X)`).
2. `match=part`: the same object inside an install prefab (`[VFX_CampfireFlame]` is `(Sandbox_Campfire)` / `VFX/[VFX_CampfireFlame]`); `part=` is the child path. The smallest prefab holding it wins, `(Sandbox_Campfire)` first.
3. `match=kind`: the closest install effect by kind (name + host words): fire (flame, torch, candle, brazier, ember) -> `(Sandbox_Campfire)` part `VFX/[VFX_CampfireFlame]`; lava -> `VFX_Lava_Bubbles01`; fireflies -> `[Sandbox_Fireflies_VFX]`; fog/mist/steam -> `[Sandbox_Fog_VFX]`; wind -> `[Sandbox_Wind_Wavy_VFX]`; leaves -> `[Sandbox_Leaf_VFX]`; a drinking fountain's jet -> `(WaterBottle)` part `Cap/Particle System` (the bottle's pour stream). Waterfalls, smoke, sparks and rain have no install stand-in and are skipped.

Keys:
| key | meaning |
| --- | --- |
| `part=` | child path inside the prefab (the kit clones only that subtree) |
| `kind=`, `match=`, `src=`, `host=` | effect kind; exact / part / kind; the scene effect root's name; its parent's name |
| `loop=1` | the scene effect loops but the install stand-in does not (the kit sets every system to loop) |
| `fit=kind` + `ps.size ps.life ps.speed ps.rate ps.max ps.shape ps.radius ps.box` | a stand-in by kind takes the scene main system's own numbers (sizes, speed, radius and box in world metres; shape = Unity ParticleSystemShapeType); the kit applies size/life/speed/rate as ratios to every system of the stand-in and uses scale 1 |
| `mount=wall back=x,y,z off=` | the effect hangs on a wall torch (host name contains "torch"): the wall is along `back` (the torch's -Z), the flame is `off` metres out from it (0.2876 for Rec Room's `[WallTorch]`). The kit places it only on a wall the room really has. |
| `snd= snd.vol= snd.min= snd.max= snd.guess=1` | a positional loop with the effect: the install effect prefab's own loop (`(Sandbox_Campfire)`: `campfire_02_30_loop` 0.33, 1-28 m), scaled for a stand-in (volume x ratio^2, max x ratio, ratio = scene size / install size); lava gets `Lava Loop Low` 0.35, 2-18 m (GUESS, `snd.guess=1`). Left out when the scene has an ambient loop of its own within 4 m. |

Measured example: every Golden Trophy wall torch's `[VFX_Flame]` is Rec Room's campfire-flame system (circle emitter,
3 s lifetime, no speed) at start size 0.39, 3/s, 5 max, radius 0.057 m, against the campfire's 0.5, 7/s, 15, 0.161 m,
so it becomes `F (Sandbox_Campfire) ... part=VFX/[VFX_CampfireFlame] kind=fire match=kind ... fit=kind ps.size=0.39
ps.life=3 ps.speed=0 ps.rate=3 ps.max=5 ps.shape=10 ps.radius=0.0567 mount=wall back=... off=0.2876
snd=campfire_02_30_loop snd.vol=0.2008 snd.min=1 snd.max=21.84 snd.guess=1`. Act 4's `[VFX_Lava]` is a 13.9 m circle,
2 m particles, 100/s, 1,000 max.

rrsk2cs.sh compacts F like P (identity rotation and unit scale dropped: 3, 4, 7 or 8 numbers).

## Sounds (iteration 5)

- **A clip names**: the exact install clip; else the same name in another letter case or with another take number
  (`..._Local03` -> `..._Local01`); else, for loops only, the install ambience of the same kind (fire ->
  `campfire_02_30_loop`; river/brook/stream/waterfall -> `Quest_Pirate1_Ambiance_BrookLocal01`; crowd ->
  `Quest_Pirate1_CrowdAmbiance`; people -> `sfx_onboarding_outdoor_ambiance_local_people02`; birds/outdoor ambience ->
  `AmbianceSummerOutdoor`; cave/draft -> `Quest_Pirate1_Caves_GhastlyAmbiance01`; mine -> `Quest_Goblin2_Ambiance_Mines01`;
  wind/gust -> `RecRoyale_LookoutTower_GustLocal01`; lava -> `Lava Loop Low`). A substitute keeps the scene's name as
  `was=`. Unmatched clips stay `?name` and the kit skips them.
- `roll=` is the AudioSource's Unity roll-off when not logarithmic (1 linear, 2 custom: the kit plays both as
  linear, which also ends in silence), `pan=` its spatial blend when not 3D (from `panLevelCustomCurve`).
- Each scene's `.md` has an "Effects and sounds" section: the ambient loops (with their substitutes), the one-shot
  sources (the kit turns them into "audio" markers), and the room's clip list from `<scene>-AudioClips.asset` (guid ->
  REF2 `audio/*.meta` -> name). Only BurningBurg (2 clips) and TheBlock (45) have non-empty lists; the Golden Trophy
  scenes have no AudioSources at all.

## H records: height grids (2026-09-24, written by ../apkscene)

These records describe terrain and ground that the kit builds as its own mesh and collider, textured with install
materials. Kits that do not know H skip it (unknown record letter).
```
H grid <id> ox oy oz dx dz nx nz key=value...   # grid header: origin (min x, min height, min z), cell size, cell counts
H row <id> <j> h0 h1 ... h(nx-1)                # row j (z = oz + j*dz); column i at x = ox + i*dx; height = oy + h
```
- `h` is in metres above `oy`, to 2 decimals. `n` means no surface in that cell (a mesh grid with holes).
- There are exactly `nz` rows of `nx` values each, and they follow their header.
- Cells are at most 2 m inside the play area. The play area is the spawns, flag bases, weapons and bounds volumes,
  plus 30 m.
- Keys:

| key | meaning |
| --- | --- |
| `src=terrain` / `src=mesh` | from a Unity Terrain heightmap (bilinear, `res=` samples, `size=x,y,z` metres) / from a ground-like mesh that is not in the install (top surface, rasterised at 1 m) |
| `collider=1` | the level has a collider there (TerrainCollider, or a MeshCollider on that mesh): build a collider from the grid |
| `vis=1` | draw it (the level renders that terrain or mesh) |
| `far=1` | a visual-only coarse grid (up to 64 cells a side) for the part of a large terrain or backdrop outside the play area; no collider |
| `mat=` | install material to texture it with. `mat.guess=1 was=<level material>` when the level's own material is not in the install. `layers=` lists the terrain layer textures (diffuse texture names). |
| `go=` | the GameObject of a mesh grid |

- `<id>` is the terrain or mesh name plus `#n`. `~far` marks the far part of a grid.
- The Unity heightmap order is `heights[z * res + x]`, normalised by 32766 and times the height scale. Checked: spawn
  points sit on it with a median offset of 0.000 m.
