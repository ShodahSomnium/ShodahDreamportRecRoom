# Rec Room reference coverage: Dorm, Rec Center and the activities

Status: COMPLETE (2026-09-24, iteration 4, task "coverage"). Section 5 merges the parser rates from
`DN/refdata/INDEX.md`.
Reference only: nothing in REF1/REF2 is copied into the project or shipped. This doc lists what exists, how
trustworthy it is, and what it is good for. Numbers come from file names, Unity YAML and glb headers. No asset
was imported.

- **REF1** = `D:\Dreamport\EXTERNAL FILES FOR REFERNCE\Rec\Rooms`: Rec Room's own room-export output for 14 rooms.
  Decoded in `rr_room_exports.md`.
- **REF2** = `D:\Dreamport\EXTERNAL FILES FOR REFERNCE\Downloads\RecRoomAssets`: 36,024 files, 8.3 GB,
  AssetRipper-style. Its `Rooms\` folder holds the same zips as REF1.
- **Install** = the user's Rec Room Addressables bundles. Index:
  `...\00e4b7e5-...\scratchpad\rrtool\idx\objects.tsv`, with 3,142 Mesh, 2,269 Material, 4,133 Texture2D and 1,174
  AudioClip names, and 3,936 prefab roots. AudioClip list: `...\00e4b7e5-...\scratchpad\research\audioclips.tsv`.
  "SP/rrtool/idx" in the brief means this older scratchpad.
- **Cache** = the user's `HTTPCache`. It was used for the Rec Center in iteration 3 (`rr_reccenter_cache.md`).

Words used below:
- **install**: a mesh whose name is an install Mesh name, so it can be loaded at runtime through RrAssets.
- **REF2-only**: the geometry exists only in REF2. It cannot ship, so the world needs a stand-in (RRSK `X` box/cyl
  with the average colour) or a procedural rebuild measured from it.
- **builtin**: Unity primitives (Cube, Cylinder, Plane...). These can be rebuilt exactly.
- **missing**: the scene points to a mesh that REF2 does not contain.

---------------------------------------------------------------------------------------------------------------------

## 0. Verdict (read this first)

**Correction to `rr_room_exports.md`: the archive rooms are not "on a blank Maker scene".**
- Every archive subroom (APPPLESARCHIVE, TheRecRoomArchiveVolume1) has a `CurrentSave.UnityAssetId` in
  `Subroom.json`. This is a **Rec Room Studio (RRS) scene bundle**. The Description reads "Cloned from
  ^RROArchive-rrs" or "^TheRecRoomArchive-Volume1".
- `UnitySceneId a75f7547` is only the generic Studio base room. The real geometry is the RRS scene.
- The 43 `.unity` files in REF2 are these RRS scenes for APPPLESARCHIVE (named `APPPLESARCHIVE-<subroom>`), plus
  `RecRoyale__PVP` and `RRLazerTagBeta`.
- The Volume-1 scenes (DormRoom-2018 `0c05d92b`, RecCenter2018 `e5209121`, RecCenter-2020 `6bb10f58`...) are
  **not** in REF2.
- So the REF1 persisted data is only the prop layer on top of each scene. That is why the DormRoom-2018 props fit
  our Dorm kit frame exactly: its RRS scene was most probably the official `dormroom2` geometry, ported (GUESS).

**The REF2 scenes are ports of OFFICIAL levels, not hand builds.**
- The scene roots are Rec Room's own level roots: `Quest_Hallway1_root` ... `Quest_Library_root`,
  `Quest_DeadIsland_root`, `Quest_Foyer_root`, `Quest_Dracula1_<Level>_root`, `Quest_Pirate1_<Level>_root`,
  `Orientation_Scene1_root`, Rec Rally's `Systems/Terrain/DynamicObjects/StaticObjects`, and Rec Royale's
  `Locales`, `[RecRoyale_GameplayZone]` and `[RecRoyale_Frontier_CableCar]`.
- The RRS registries (MaterialMap, PrefabRegistry, AudioClipRegistry, ...) sit next to those roots.
- The official gameplay markers are there by name: `EnemySpawnPoint`, `Player7SpawnPoint`, `Spawn Regions`,
  `[Quest_Boulder]`, `LavaPathBlocker`, `CPSpawn`, `[Checkpoint]`, `[RecRally_Pickup]`, 455 `Chest` and 66
  `SpawnPoint` in Rec Royale, and `HolsteringTransform`.
- The MonoBehaviours have **no fields** (stripped), so markers carry a name and a transform but no settings.
- The RRS registry names in `audio\` and `.unity files\` show that REF2 was ripped from about 90 Studio rooms, for
  example `RROSandBoxRRS-GTact1..6`, `AllIsleLevelsRRS-*`, `CrescendoIsleFly-*`, `MerryMidwayPVP-Home`,
  `9hjs_BonkysInferno_1807-Home`, `RRJumboTronBossArena-LaserTagBeta`, `2021RRS-RecCenter` and
  `2019_Rec_Center-RecCenter`. Only 43 of them came with a `.unity` file.

**Geometry is the weak part.**
- Mesh references resolve through `.assets\*.asset.meta` (6,426 Mesh assets). For the rest they fall back to
  GameObject and glb names.
- The parser's rates (`DN/refdata/INDEX.md`, section 5) count active LOD0 renderers whose mesh is an install mesh or
  is covered by an install prefab:
  - **Golden Trophy 37-64 %**, GT lobby 60 %;
  - **Crescendo 11-74 %** (Gallery 74, Grand Halls 57-68, Lower Sanctum 11, Concert Hall 25);
  - **Isle 10-61 %** (Pirates Banquet 61, High Seas 10, Sunken Ship 14);
  - Rec Rally 31 %, Rec Royale 32 %, MITM 62 %, Laser Tag beta 23 %.
- Everything else becomes an `X` stand-in (box or cylinder with the average colour).
- Material matches are 20-92 %.
- My own MeshFilter count (section 3.0, all LODs, exact names) is stricter and gives lower install shares.

**Summary of what can be rebuilt "from reference"**
- **Rebuild from reference:**
  - Quest for the Golden Trophy: acts 1-6 and the lobby (acts 7-9 are missing);
  - Rec Rally (Chaparral);
  - Rec Royale (Frontier Island; its terrain heightfield is missing);
  - Make It To Midnight: from the user's cache `35C.data` (the official Aug 2025 scene), if the user approves using
    the cache for it as for the Rec Center;
  - the Dorm (install kit plus REF1 prop layout);
  - the Rec Center (cache, done).
- **Reference + research:**
  - Isle of Lost Skulls and Crescendo of the Blood Moon: every act present, but a large share of the geometry is
    REF2-only or missing;
  - Paintball: REF1 gameplay layer on 6 maps, map geometry only as fragments;
  - Laser Tag: roof and prop chunks of Hangar and Cyber Junk City; the only full scene is the older 2018 beta;
  - Paddleball and Dodgeball: built; REF1 and REF2 add positions and physics numbers;
  - Bowling: REF2 has the alley kit, but no layout.
- **Research only:**
  3D Charades (built), Disc Golf, Soccer, Stunt Runner, Showdown, My Little Monsters, Punch Up, The Curse of the
  Crimson Cauldron and The Rise of Jumbotron. These have only kit pieces, audio and one activity picture each.
  Crimson Cauldron has 2 world-space chunks.

---------------------------------------------------------------------------------------------------------------------

## 1. Coverage table (short)

`R2 scenes`: non-empty REF2 `.unity` files (empty ones in brackets). `geom` = the parser's install mesh % per scene
(`DN/refdata/INDEX.md`, active LOD0 renderers, install mesh or prefab). The rest are `X` stand-ins. Section 3.0 has
my stricter split: install / REF2-only / builtin / missing.

| place / activity | built? | R2 scenes | REF1 | install kit | version of the reference | geom | verdict |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Dorm | yes | 0 | DormRoom-2018 (+2019, 2021, UnknownDate, DormRoom 1717616) | full Maker Pen `Dorm_*` kit (40 meshes, 56 prefabs) | kit = final `dormroom2` (2018 Spring Cleaning); REF1 = archivist's 2018 prop layout | kit complete | rebuild from reference (done; apply `rr_latest_places` 2.3 deltas) |
| Rec Center | yes | 0 (only Orientation 1/2/End, a different room) | 4 archivist versions (2018-2021) | a few props | cache `1AF` = final July 2025 | cache complete | rebuild from reference (done from the cache) |
| 3D Charades | yes | 0 | - | pen, buttons | - | - | research only |
| Bowling | no | 0 | - | balls and pins (12 prefabs) | REF2 alley kit has 2016/2017/2020 frames = late | kit only (~33 REF2-only alley meshes) | reference + research |
| Crescendo of the Blood Moon | no | 13 acts + lobby, 17 files (2 empty) | prop dumps (whips, shovels) | 44 prefabs (weapons, enemies) | official port, >= 2019-11 (GUESS) | 11-74 % (parser: Gallery "rebuildable from the install", 11 "partly", 3 "mostly stand-ins") | reference + research |
| Disc Golf | no | 0 | - | discs (7 prefabs) | - | - | research only |
| Dodgeball | yes | 0 | - | ball, court prefabs | - | - | research only (+ REF2 physics) |
| Isle of Lost Skulls | no | 8 acts + lobby (9) | prop dumps (bottles, flintlocks) | 43 prefabs | official port, >= 2018-12 "Rebooty" (holsters) | 10-61 % + REF2 terrains and world chunks | reference + research |
| Laser Tag | no | 1 (2018 BETA: Mainframe Assault / Fuel Run, cut) | LAZERTAGCOPYd weapon pickups | 24 Arena_ prefabs (guns, grenade) | beta = OLD; Hangar/Cyber Junk chunks = final-era | beta 23 % (1,599 X); final maps: fragments only | reference + research |
| Make It To Midnight | no | 1 (official 2023 layout, prefabs missing) | 2 player remixes | 36 prefabs (hammer, perks) | final (2023; cache Aug 2025) | REF2 62 % of the loose renderers, but 1,603 prefab instances unresolved; cache complete | rebuild from reference (cache 35C, approval needed) |
| My Little Monsters | no | 0 | - | 11 prefabs | - | - | research only |
| Paddleball | yes | 0 | vol.1 Paddleball = official gameplay layer | paddle, ball | final scene layer | - | research + REF1 (built) |
| Paintball | 2 of 7 maps | 0 | paintballtroll1: lobby + River, Drive-in, Quarry, Clearcut, Spillway, Merry Midway (no Homestead) | 32 prefabs (weapons, flags) | REF1 on final scenes (troll edits) | Spillway terrain + Dam kit, Quarry walls, crane; rest missing | reference + research |
| Punch Up | no | 0 | - | punching bag props | never released | - | research only |
| Quest for the Golden Trophy | yes | acts 1-6 + lobby (7) | GoldenLobby props; GTact1-6 empty | 24 prefabs + kit (Lockers, Scaffolding, WineBarrel...) | official port; lobby has the 2020 merch Store; act 6 = Library -> final-compatible | acts 37-64 %, lobby 60 % | rebuild from reference (acts 1-6, lobby); acts 7-9 research |
| Rec Rally | no | 1 (Chaparral) | 28 UGC + 6 buggies | 2 prefabs | official 2021 track | 31 % (806 X), TerrainData missing | rebuild from reference |
| Rec Royale | no | 1 (Frontier Island) | 3 player layers | 22 prefabs, 86 meshes | official, content final since 2020-12 | 32 % (3,577 X); TerrainData missing | rebuild from reference (terrain rebuilt) |
| Rec Room Showdown | no | 0 | - | none found | - | - | research only |
| Soccer | no | 0 | - | ball (4 prefabs) | - | - | research only (+ REF2 physics) |
| Stunt Runner | no | 0 (StuntLobby empty) | - | 14 prefabs, springboards | - | course chunks are empty placeholders | research only |
| Curse of the Crimson Cauldron | no | 0 | - | 26 prefabs (enemies, weapons) | - | 2 world chunks (Bog, Mines1) | research only (+ fragments) |
| Rise of Jumbotron | no | 0 (JumbotronBoss empty) | - | 32 prefabs, 39 meshes | - | Cargo1 chunk = empty placeholder | research only (+ kit) |

The brief names 20 activities (it says "21"). Rec Royale Solos and Squads share one map and count as one row here.

---------------------------------------------------------------------------------------------------------------------

## 2. REF2 inventory

### 2.1 Folders

| folder | files | size | what it is |
| --- | --- | --- | --- |
| `.unity files\` | 257 | 493 MB | 43 Unity YAML scenes (38 non-empty, section 3.0), each with RRS registries (`-AudioClips/-ParticleVfxs/-Projectiles/-PostProcessingProfile.asset`, `.mixer`, `.lighting`). `RunTheBlock\` has lighting, reflection probes and occlusion data but **no .unity** |
| `.assets\` | 12,854 | 597 MB | **6,426 Mesh assets + .meta** (all class Mesh; the GUIDs in the metas are what the scenes reference). Names: 2,285 ProBuilder (`CachedProBuilderMesh`, `pb_Mesh`), ~2,460 hex-GUID names (Studio/UGC meshes), 38 `Combined Mesh (root_ scene)` static batches, the rest named official meshes |
| `Mesh\` | 11,933 | 343 MB | glb exports by mesh name: 7,567 ProBuilder, 5,984 distinct names. Includes **world-space static-batch chunks of official levels** that have no scene here (2.3) and terrain meshes. 12 `.json` files are empty static-batch placeholders |
| `2D Textures\` | 6,387 | 2.4 GB | 4,507 png, 28 exr, 126 .mat. 4,589 distinct names; `Cubmaps\` (261: skyboxes and reflection probes such as `Dormroom2_Baked_Skybox`, `River_ReflectionProbe_*`, `ClearCutReflectionHDR`, `Moods_*`) and `Materials\` (122) |
| `audio\` | 2,505 | 1.9 GB | 1,197 wav, 615 ogg, 61 mp3, 67 mixers, 84 registry assets. 1,934 distinct clip names; 60 mp3 = the **Rec Room OST Vol 1-3** albums |
| `Fbx,Prefabs,Objs\` | 101 | 215 MB | loose exports: weapons (PaintBall_*, Crossbow, Gold pistol), `Goldenlobby.fbx` (52.8 MB, the Golden Trophy lobby as one model), `GlassCasePivot_SRank`, **`Static.prefab`** (966 GOs: MITM fairground: tents, stalls, Ferris wheel, `Banner_BonkyAndHelga`, `BonkyPerksBoard`), **`Static Variant.prefab`** (13,470 GOs: carousel, vendor stalls, 1,276 light cubes, hay bales; GUESS Merry Midway / MITM variant), `Instanced.prefab` (714 prefab instances: VendorStalls, TicketBooths, Carousel, Cages...), `[GiantSlideNoCanopy2]`, `[Quest_Dracula1_Carriage]`, `[SoccerShield]`, `Roomie Test Avatar` |
| `PhysicMats\` | 91 | 1 MB | **29 physics materials with values** (2.4); not in the install index |
| `Sprites\` | 1,327 | 12 MB | UI sprite json (watch/menu icons, mouths/eyes, KO skins) |
| `Pics\` | 16 | 4 MB | NUX control-menu images per headset, a penguin drawing |
| `Fonts\` | 23 | 6 MB | legacy fonts (Futura, Source Sans Pro, Oswald, RecRoyaleFont, Diggity) |
| `Clothes\` | 101 | 594 MB | the archivist's (account 1257174) avatar outfit exports. Not used: we never touch the avatar |
| `Rooms\` | 429 | 2.0 GB | = REF1 |

### 2.2 Activity pictures (1024x512 in-game renders, one per activity; look references)
In `2D Textures\`, not in the install. They show real map views, e.g. the Bowling lanes with neon pins and green
diamonds, and the Homestead barn with its windmill.
- `Activity_Image_`: Bowling, Charades, Cyberjunk, Discgolf_Lake, Discgolf_Propulsion, Dodgeball, Dorm,
  Lasertag_Arena, Lounge, Maker room, Paddleball, Paintball_Clearcut/Homestead/River/Spillway,
  Pantball_DriveIn/Quarry, Park, performance hall, Quest_CBM/CCC/Golden_Trophy/ILS/Jumbotron, RecRoyale, Rec_Center,
  Soccer, StuntRunner, AR.
- `Cohort_Nux_Image_Dodgeball`, `Cohort_Nux_Image_Paintball`.
- None for Showdown, Merry Midway, Rec Rally, MITM, MLM, Punch Up or Paddleball maps other than the court.

### 2.3 Official-level fragments in `Mesh\` (world-space static-batch chunks and terrains; x negated in glb)

| level (activity) | chunks with geometry | bounds (glb frame, m) | note |
| --- | --- | --- | --- |
| `Arena_Hangar3` (Laser Tag Hangar) | StaticObjects-6..11 (4 x 50-65k verts + 2 small) | x -40..40, y 5.5-11.7, z -60..70 | only the upper part (roof, trusses). -28 and -62 are empty .json |
| `Arena_Cyberjunk_City` (Laser Tag) | StaticObjects-8, .Props-1/2/52 | x -23..45, y -12..20, z -99..36 | part of the city |
| `Paintball_Dam_Terrain` (Spillway) | full terrain mesh, 62,500 verts | 256 x 256 m, height 0-73 m | local terrain frame (position unknown) |
| `DriveIn.DriveIn.BG-1`, `paintball2_open ... CloudPlane` | background quads | km-scale | skybox cards only |
| `Quest_Pirate1_*` (Isle) | Beach1 x3, Beach2 x3, Caves1 x6, Foyer, Hallway1, Hallway2 x3, SunkenShip, + terrains Beach1, Beach2, BossArena, ShipDeck, SunkenShip, Sand | e.g. Caves x -363..-197 | matches the Isle scenes (fills part of their "missing") |
| `Quest_Dracula1_*` (Crescendo) | Township-56/57, CarriageRide-16/18, CastleCourtyard-29, Library-15/20, Graveyard-42 | e.g. Library x -63..1, z -135..-53 | a few chunks per level |
| `Quest_Goblin2_*` (Crimson Cauldron) | Bog-24 (49k verts), Mines1-16 (55k verts) | Bog x -25..20, z -73..-25 | Forest2 and Bog-18 are empty .json |
| `RecRoyale_Frontier.Locales.Rocks-0/2/4` | 3 | ~2 km | rocks of the island |
| `Terrain_x0_y0 .. x1_y1`, `Terrain_PlayArea` | 4 x 128 m tiles, height up to 8 m | 256 x 256 m | GUESS: Rec Rally's terrain (its TerrainData `11341c6b` is not in REF2; its Terrain object sits at (-156, -9.9, -145.8)) |
| `StuntRunner_Course3/4/5`, `Quest_Scifi_Cargo1`, `Quest_Hallway1/3`, `Quest_Battlefield` | none | - | empty .json placeholders: no geometry |

### 2.4 Physics materials (REF2 `PhysicMats\`; dynamic friction / static friction / bounciness, combine modes)
- Paddleball: Ball 0 / 0 / 0.6 (friction and bounce combine = Minimum); Paddle 0.6 / 0.6 / 0 (bounce Minimum);
  Wall 0 / 0 / 0.6.
- Dodgeball: Ball 0.6 / 0.6 / 0.6; Court 0.6 / 0.6 / 0.6.
- Soccer: Ball 0.3 / 0.2 / 0.6; SoccerEnvironment 0.3 / 0.2 / 0.6.
- Bowling: Ball 0.14 / 0 / 0.25; Pin 0.6 / 0.6 / 0.6 (bounce combine 3 = Maximum).
- Pool: Ball 0.03 / 0.03 / 0.9; Table 0.3 / 0.5 / 0.2.
- Ping-pong: Ball 0.1 / 0.1 / 0.8; Paddle 0.6 / 0.6 / 0.2.
- Basketball 0.8 / 0.8 / 1.0 (Park_Basketball 0.9); BasketballBackboard 0.45; RimInner 0.05.
- Frisbee 0.1 / 0.1 / 0.15; DiscGolf_Terrain 0.2 / 0.6 / 0.
- Others: Tennisball 0.95 bounce, Grenade 0.4, Die 1.0, LockerRoomFloor 0.58, Player_phys 1 / 1 / 0, Gun 1 / 1 / 0.

Combine enum: 0 Average, 1 Minimum, 2 Multiply, 3 Maximum. These are plain numbers, so the builders can type them
in (they are facts, not files).

### 2.5 Audio
- **824 of the 1,934 REF2 clip names are also in the install**; 1,110 are REF2-only (per activity: section 4).
- The 60 OST mp3 files are the commercial albums. They are reference for the look and feel only: never ship them.
- Runtime audio must come from the install. Where only REF2 has a clip, the world goes silent or uses an install
  substitute.

---------------------------------------------------------------------------------------------------------------------

## 3. The REF2 scenes

### 3.0 Scene table (MeshFilter share: install / REF2-only / builtin / missing; MF = MeshFilters)
My strict count: all MeshFilters (every LOD, active or not) and exact mesh names. The parser rates in section 5
(`DN/refdata/INDEX.md`, active LOD0 renderers) are the ones to plan with.

| scene | root (official level) | activity, act | MF | inst | REF2 | builtin | miss |
| --- | --- | --- | --- | --- | --- | --- | --- |
| GoldenLobby | Quest_Foyer_root | Golden Trophy lobby (theatre stage, Store/Storefront = 2020 merch booth, 104 CPSpawn) | 2540 | 8 | 16 | 63 | 12 |
| GTact1 | Quest_Hallway1_root | GT act 1 (14 EnemySpawnPoint) | 644 | 23 | 41 | 31 | 4 |
| GTact2 | Quest_Hallway2_root | GT act 2 (27 EnemySpawnPoint, 24 fat pots, teleport targets) | 1117 | 9 | 49 | 36 | 4 |
| GTact3 | Quest_Hallway3_root | GT act 3 (`[Quest_Boulder]`) | 1043 | 16 | 45 | 30 | 6 |
| GTact4 | Quest_DeadIsland_root | GT act 4 (lava: `Lava Ambient` x11, 3 `LavaPathBlocker`, spawn regions) | 626 | 5 | 79 | 6 | 9 |
| GTact5 | Quest_Hallway4_root | GT act 5 | 1724 | 9 | 44 | 36 | 9 |
| GTact6 | Quest_Library_root | GT act 6 = **library** (65 bookshelves, 1,398 books) | 4173 | 41 | 39 | 13 | 5 |
| CrescendoLobby = Foyer (same file twice) | Quest_Dracula1_Foyer_root | Crescendo lobby (`GlassCasePivot_SRank`) | 2497 | 12 | 21 | 56 | 8 |
| BurningBurg | Quest_Dracula1_Township_root | CBM act 1 | 2945 | 0 | 44 | 29 | 24 |
| ForestRoad | Quest_Dracula1_CarriageRide_root | CBM act 2 | 1310 | 27 | 37 | 26 | 8 |
| CastleGroundsDay | Quest_Dracula1_CastleEntry_root | CBM act 3 (CastleGrounds = empty) | 2137 | 9 | 52 | 34 | 2 |
| GrandHall1, 2, 3, GrandHall1-4 | Quest_Dracula1_CastleMainFoyer_root | CBM acts 4/7/10/12 (GrandHall4 = empty) | ~1576 | 1-3 | 32-39 | 20-23 | 38-40 |
| Gallery | Quest_Dracula1_Gallery_root | CBM act 5 | 2721 | 5 | 42 | 13 | 37 |
| ChamberOfKnowledge | Quest_Dracula1_Library_root | CBM act 6 | 3378 | 3 | 40 | 25 | 31 |
| GardenOfSarrows | Quest_Dracula1_Park_root | CBM act 8 | 1663 | 2 | 32 | 26 | 38 |
| InvertedBallroom | Quest_Dracula1_Ballroom_root | CBM act 9 | 2005 | 2 | 39 | 33 | 24 |
| LowerSanctum | Quest_Dracula1_Dungeon_root | CBM act 11 (planks on scaffolding: >= 2019-11 GUESS) | 2307 | 0 | 47 | 29 | 22 |
| ConcertHall | Quest_Dracula1_BossRoom_root | CBM act 13 (Dracula) | 1069 | 4 | 18 | 20 | 56 |
| IsleLobby | Quest_Pirate1_Foyer_root | Isle lobby | 1823 | 6 | 23 | 54 | 14 |
| GalIery (capital I) | Quest_Pirate1_Hallway1_root | ILS act 1 Galley jail | 3429 | 0 | 56 | 16 | 27 |
| PiratesBanquet | Quest_Pirate1_Hallway2_root | ILS act 2 | 5209 | 0 | 51 | 27 | 21 |
| HighSeas | Quest_Pirate1_ShipDeck_root | ILS act 3 | 1277 | 0 | 51 | 13 | 34 |
| PortPlunder | Quest_Pirate1_Beach1_root | ILS act 4 (HolsteringTransform) | 3723 | 2 | 43 | 33 | 20 |
| GibbetJungle | Quest_Pirate1_Beach2_root | ILS act 5 | 2759 | 9 | 42 | 10 | 36 |
| Caves | Quest_Pirate1_Caves1_root | ILS act 6 | 3673 | 0 | 42 | 21 | 35 |
| SunkenShip | Quest_Pirate1_SunkenShip_root | ILS act 7 | 2982 | 2 | 32 | 25 | 39 |
| GhostBeardGauntlet | Quest_Pirate1_BossArena_root | ILS act 8 (boss) | 1083 | 5 | 47 | 17 | 29 |
| RecRally | Systems / Terrain / StaticObjects | Rec Rally Chaparral (75 `[RecRally_Pickup]`, 5 `[Checkpoint]`, 50 CPSpawn, 6 Gate) | 8798 | 16 | 64 | 11 | 8 |
| RecRoyale__PVP-RecRoyale | Locales, GameplayZone, CableCar, Gaia Environment | Rec Royale Frontier Island (455 Chest, 66 SpawnPoint) | 18559 | 4 | 60 | 29 | 5 |
| MakeItToMidnight | Gameplay / Static / Terrain / Collision | MITM (SurvivorPerksBoard, BonkyPerksBoard, PhotoHut, Cage, Ferris wheel, carousel, exit lights); 1,605 prefab instances whose prefabs are not in REF2 | 1089 | 0 | 67 | 0 | 32 |
| RRLazerTagBeta-LaserTagBeta | RedSide/BlueSide/FlippedGeo, MainframeAssaultEncounters, FuelCellRunEncounters | pre-release **Laser Tag beta** (weapon vending machines, credits, fuel cells, drop ship) | 6513 | 8 | 26 | 65 | 0 |
| OrientationStage1 / 2 / End | Orientation_Scene1/2_root, Orientation_Rewards_root | 2021 Orientation (not an activity) | 804-1058 | 2-14 | 76-91 | 5-11 | 0-1 |

Empty (16 KB, registries only): CastleGrounds, GrandHall4, JumbotronBoss, StuntLobby, Testing.
These are exactly the subrooms with "no save" in REF1.

### 3.1 How far to trust the scenes
- **Official, not player-built:**
  - the roots and the internal names (`Hallway3StaticBatch_Wallpaper_RockWall_Mat`, `[Quest_Door_Inactive]`,
    `Quest_Dracula1_*`);
  - the markers named after Rec Room's quest framework (EnemySpawnPoint, PlayerNSpawnPoint, Spawn Regions);
  - the install match: in GTact3, 107 of 249 distinct object names are install object names.
- **Which build:** the scenes are archivist ports (RRS rooms created 2026-03). The ported build's date is not
  stored. The evidence dates them late:
  - the GT lobby has the 2020 Store;
  - the Isle has back holsters (2018-12) and checkpoint spawns;
  - the CBM dungeon has scaffolding planks (the 2019-11 note).
  Only CBM's 2024-11 mobile port changes (lighting, occlusion) might differ, and none of those changes is visible
  in layout. Treat all of them as **final layout**.
- **Not final:** RRLazerTagBeta. Mainframe Assault and Fuel Run were cut before Laser Tag's release
  (`rr_latest_rgames_action` 2.1).

---------------------------------------------------------------------------------------------------------------------

## 4. Per place / activity

Each block: sources, version, geometry, what only REF2 has (vs the install), verdict, and the references that are
still needed.

### 4.1 Dorm (built)
- **Sources:**
  - REF1 `DormRoom-2018`: 67 props; the final 2018 layout, in our kit frame (`rr_room_exports` 3.1).
    `DormRoom2021` = the same save with swaps; `DormRoom2019` is sparse; `DormRoom-UnknownDate` = pre-2018.
  - Install: the full `Dorm_*` Maker Pen kit = the final `dormroom2` shell (`rr_dorm_core`).
  - Research: `rr_latest_places` 2.
- **REF2-only:**
  - mesh `DormRoom2_Hull` (1,411 verts, y 2.5-7.9 m, x -4.9..4.0, z -1.0..3.1): an upper shell piece of
    `dormroom2`, consistent with the kit frame (GUESS);
  - textures `DormRoom_BunkBedDresser_*`, `DormRoom_BulletinBoard_*`, `DormRoom_Map_*`, `DormRoom_Rugs`,
    `Dormroom_Desklamp_*`, `DormRoom_LUT`, `Cubmaps/Dormroom2_Baked_Skybox`;
  - `Activity_Image_Dorm`;
  - `Pics\Nux_Menu_*` (the NUX control posters; `rr_latest_places` says the final social posters were
    notebook-style NUX posters).
- **Version:** final (the kit). The 2018 prop layout is the archivist's, on an RRS scene we do not have.
- **Verdict:** rebuild from reference (done). Apply the 2.3 deltas: NUX posters, help buttons, one armchair, no
  dresser, whiteboard and markers on the loft, mirror labels, the Roomiverse/RecCenter door option.
- **Needed:** nothing essential. Optionally the DormRoom-2018 RRS scene (`0c05d92b`), if it is in the user's cache.

### 4.2 Rec Center (built)
- **Sources:**
  - Cache `1AF.data` = `RecCenter-RecCenter_071825.unity` (final, measured: `rr_reccenter_cache.md`).
  - REF1: 4 archivist versions (2018/2019/2020/2021) = older rooms.
  - REF2: no Rec Center scene; only the registries `2019_Rec_Center-RecCenter` and `2021RRS-RecCenter`, so their
    scenes are absent.
- **REF2-only:**
  - meshes `RecCenter_Cafe_ChalkBoard/DisplayCase/Table`, `RecCenter_Chair`, `RecCenter_Percolator`,
    `RecCenter_Backdrop`, `reccenter_fence`, `LoungeDoor_*`;
  - textures `RecCenter_Marquee`, `RecCenter_ProjectionScreen`, `RecCenter_SubwayBrick`,
    `RecCenter_Carpet_theater`, `Activity_Image_Rec_Center`, `Activity_Image_Lounge`;
  - audio `RecCenter_Ambiance_CafeCounter`, `Rec Room Theme Loop`.
- **Version:** cache = final. REF1 = old.
- **Verdict:** rebuild from reference (done). REF2 adds nothing structural. The Orientation scenes are a separate
  room.
- **Needed:** the room data save `182.data` decode (door lists), as already noted in `rr_reccenter_cache` 7.

### 4.3 3D Charades (built: Ink Space)
- **Sources:** research `rr_party`, `rr_latest_rgames_story` 1. No REF1 or REF2 scene.
- **REF2-only:** `Activity_Image_Charades` (the old theatre); audio `MUS_BGM_3DCharades`,
  `MUS_BGM_Countdown_3DCharades`, `CharadesBuzzer`; OST "16 - 3D Charades" and "17 - The Ink Space".
  The install has 5 charades clips, 10 prefabs and 11 meshes.
- **Verdict:** research only. Final-rule gaps are in `rr_latest_rgames_story` 1.3: pen shapes, materials, redo,
  scale, grabbable drawings, minimum players.
- **Needed:** pictures or video of the Ink Space, if the look is to be matched.

### 4.4 Bowling (not built)
- **Sources:** research `rr_sports`, `rr_latest_rgames_action` 9 (Lone Shoe Lanes, unchanged since 2018, pool table
  playable since 2020-02). No scene anywhere.
- **REF2-only:**
  - kit meshes `BowlingAlley_*`: Bar, BackCounter, Counter, BallRack, BallReturn, Bumpers, LaneArrow, LaneDot,
    LaneLight, LaneLine, LaneNumbers, PinBlocker, PinGrabber, Lockers, LockerDoor, DanceMachine, BasketballGame,
    ArcadeDoor, PoolLight, PoolStep, PoolCueRack, NeonPin, Sign_Eat, Sign_Play, PictureFrame2016/2017, Shoe, Table,
    WallDiamonds, AnimatingSign, OutOfOrder;
  - textures LaneWood, PinMachine, NeonSigns, FramedShirts and FramedShirts2020 (the 2020 frames show the kit is late);
  - `Activity_Image_Bowling` (lane view);
  - audio `Bowling_Ambience`, `Bowling_Pins`, "Lone Shoe Blues";
  - physics for the ball, pins, pool ball and pool table.
  - The install has balls and pins (12 prefabs, 15 meshes).
- **Verdict:** reference + research. Measure the kit pieces from REF2 (sizes), build the room procedurally, and lay it
  out from the picture and research.
- **Needed:** a top-down or walkthrough video or screenshots of Lone Shoe Lanes (lane count, room size, arcade,
  pool area).

### 4.5 Crescendo of the Blood Moon (not built)
- **Sources:**
  - REF2: all 13 acts plus the lobby (section 3.0; 4 Grand Hall visits = GrandHall1/2/3/1-4).
  - REF2 `Mesh\` chunks (2.3).
  - REF1 prop dumps (whips, shovels, pitchforks, blunderbusses, standees; played-state snapshots).
  - Install: 44 prefabs (enemies, weapons, mirrors).
  - Research `rr_quests`, `rr_latest_rgames_story` 7 (13 acts, 5 mini-bosses, 3 light mirrors).
- **REF2-only:**
  - ~70 meshes: Dracula boss and cape, Dracula_Wolf, Dracula_Carriage, coffins, headstones, `Dracula1_FatPot_*`,
    hanging lanterns, skeleton standees;
  - 165 textures;
  - 128 audio clips: `Quest_Dracula1_*` ambiences, cinematics, VO, `CBM_*` music stems; OST tracks 17-24.
- **Version:** official port; final layout (3.1).
- **Geometry:** parser 11-74 % install meshes. Gallery 74 %; Grand Halls 57-68 %; Chamber 45 %; Forest Road and
  lobby 41-43 %; Burning Burg 35 %; Garden 37 %; Ballroom 32 %; Castle Grounds 27 %, Concert Hall 25 % and Lower
  Sanctum 11 % are "mostly stand-ins". My strict count: 2-56 % missing, the Concert Hall worst.
- **Verdict:** reference + research. The layout, markers, doors and lights come from REF2. The walls need stand-ins
  or procedural shells where meshes are REF2-only or missing.
- **Needed:** better geometry for the Grand Hall and the Concert Hall, e.g. the `CrescendoIsleSandbox-*`,
  `Crescen-Do-*`, `DraculaHalls` or `Grand-Hall-KNP` Studio rooms, whose registries are in REF2 but whose scenes
  are not.

### 4.6 Disc Golf (not built)
- **Sources:** research `rr_sports`, `rr_latest_rgames_action` 8 (Lake and Propulsion, unchanged since 2017; 2019:
  water no longer KOs).
- **REF2-only:**
  - `Activity_Image_Discgolf_Lake` and `_Propulsion`;
  - `DiscGolf2_Sign`, `DiscGolf2_WelcomeSign2`;
  - disc and power-up textures, "Propulsion" fan-blade SFX;
  - physics `DiscGolf_Terrain` 0.2 / 0.6 / 0 and Frisbee.
  - The `Terrain_x*_y*` tiles are unidentified (GUESS: Rec Rally, see 2.3).
- **Verdict:** research only (+ physics numbers).
- **Needed:** course maps or videos for both courses (hole positions, baskets, fan pads).

### 4.7 Dodgeball (built)
- **Sources:** research `rr_sports` 2, `rr_latest_rgames_action` 5. REF1 LockerRoom-2017 has 12 dodgeballs but is an
  old room.
- **REF2-only:**
  - `DodgeballCourt` (only an 8-vertex decal quad, 9 x 1.2 m);
  - `Bleacher*`, `DodgeballThrow_Cutout`;
  - physics Ball and Court (0.6 / 0.6 / 0.6);
  - OST "Sport Funk 2 aka Dodgeball", `Dodge Baller`;
  - `Activity_Image_Dodgeball`, `Cohort_Nux_Image_Dodgeball`.
- **Verdict:** research only (keep the built gym). **Final-rule update needed:**
  - `CatchOutsThrower = false` (2025-01: no catch-outs), and drop the teammate return;
  - constant 14 m/s screen throw;
  - barriers behind each side;
  - mods (Low Gravity, Dodge-Bombs, Disco Dodgeball).
- **Needed:** nothing for the rules. The gym layout stays research-based.

### 4.8 Isle of Lost Skulls (not built)
- **Sources:**
  - REF2: all 8 acts plus the lobby (section 3.0), plus the most `Mesh\` chunks and 5 terrain meshes of any quest.
  - REF1 prop dumps (bottles, jugs, flintlocks, standees).
  - Install: 43 prefabs.
  - Research `rr_quests`, `rr_latest_rgames_story` 8.
- **REF2-only:**
  - ~147 meshes: Ghost Beard, skeleton pirates, gibbets, corals, crab traps, pirate puzzle, captain costume;
  - 113 textures;
  - 88 audio clips: `Quest_Pirate1_*` ambiences (port waves, ship creak, sunken ship drips), cutlass, flintlock;
    OST Vol 1 tracks 1 and 10-16.
- **Version:** official port after the 2018-12-05 "Rebooty" (HolsteringTransform, CPSpawn) = final.
- **Geometry:** parser 10-61 % install meshes. Pirates Banquet 61 %, Galley 51 %, lobby 49 %, Caves 43 %, Port
  Plunder 32 %, Ghost Beard 31 %, Gibbet Jungle 28 %, Sunken Ship 14 %, High Seas 10 % (the last 3 "mostly
  stand-ins"). The Mesh chunks and terrains cover part of the gap as measurements.
- **Verdict:** reference + research. It is the most complete quest after the Golden Trophy.
- **Needed:** nothing blocking. The `AllIsleLevelsRRS` and `CrescendoIsleFly` Studio scenes would fill the gaps.

### 4.9 Laser Tag (not built)
- **Sources:**
  - REF2 `RRLazerTagBeta` = the 2018 pre-release beta (Mainframe Assault / Fuel Run encounters, weapon vending
    machines, credits) = **old**, but a full arena built largely from primitives (635 of 2,150 renderers; 4,236 builtin MeshFilters), with red and
    blue sides. GUESS: an early Hangar.
  - REF2 `Mesh\` Hangar3 roof chunks and Cyber Junk City chunks (2.3), both final-era maps.
  - REF1 `LAZERTAGCOPYd`: weapon pickups on the official scene `239e676c` (a player copy). REF1 vol.1 LaserTag-Beta.
  - Research `rr_shooters` 10.4, `rr_latest_rgames_action` 2.
- **REF2-only:**
  - ~54 meshes: Arena_Cyber billboards, graffiti, neon signs, faction posters, marquees `Arena_Laser_Tag_Marquee`,
    `_Lounge_`, `_Cyberjunk_`; the LaserTag costume;
  - 37 textures;
  - 38 audio clips: AmyVO Laser Tag lines (team leads, near victory, power weapon online), Arena_Cyber
    ambiences, `Arena_CyberJunk_Fanfare`; OST "Laser Tag High/Low", "Welcome to Cyber Junk City".
  - The install has 24 `Arena_*` prefabs (guns, grenade, EMP, fuel cell).
- **Verdict:** reference + research.
  - Hangar: take the envelope and roof height from the chunks. They span 80 x 130 m with tops at 9.4-11.7 m, so
    they probably include the building around the 56 x 32 m arena of `rr_shooters` 10.4. The floor layout comes
    from research, with the beta arena as a hint.
  - Cyber Junk City: partial.
- **Needed:** top-down pictures or video of both final maps. The official scene `239e676c` is not reachable.

### 4.10 Make It To Midnight (not built)
- **Sources:**
  - Cache `35C.data` = `MakeItToMidnight-Home.unity` (7 Aug 2025, the official Studio scene; indexed in
    `SP/cacheidx`: 1,365 meshes, 291 textures, 171 AudioClips, prefabs `[ExitGate]`, `[NewGeneratorWithArt]`,
    `[Cage]`, `[PhotoBooth]`, `BonkyPerkBoard`, `SurvivorPerkBoard`...).
  - REF2 `APPPLESARCHIVE-MakeItToMidnight` (the same layout names; prefabs missing) and `Fbx,Prefabs,Objs\Static.prefab`.
  - REF1 vol.1 `MakeitToMidnight` and `alternate` = player remixes (paintball guns); the APPPLES one is empty.
  - Research `rr_party`, `rr_latest_rgames_story` 2.
- **REF2-only:**
  - 160 audio clips: `sfx_stn_*` survivor foley, perks, generator minigame, cage, carousel ambience; Bonky VO
    `Bonky_KO_*`/`Bonky_RoundStart_*`; `Popcorn2_*` timers and combat (the Bonky's Inferno registry);
  - popcorn and zombie meshes;
  - `Static.prefab`.
- **Version:** final (2023 game; the cache is from Aug 2025).
- **Verdict:** rebuild from reference **if the user allows the cache for MITM**, as for the Rec Center. Without it:
  REF2 layout + stand-ins (0 % install meshes).
- **Needed:** the user's OK to measure `35C.data`.

### 4.11 My Little Monsters (not built)
- **Sources:** research `rr_latest_rgames_story` 3 only. REF2 has one clip (`sfx_mlm_harvesting_pickup_01`). The
  install has 11 prefabs.
- **Verdict:** research only.
- **Needed:** pictures or video of the room, or its Studio scene (a Rooms 2.0 game from 2024, so it is a Studio
  bundle; it could be in the cache if the user played it).

### 4.12 Paddleball (built)
- **Sources:**
  - REF1 vol.1 `Paddleball` = the official scene `d89f74fa` gameplay layer: GAME_CONFIGURATION, rule circuit,
    paddle, scoreboard and leaderboard positions (`rr_room_exports` 2/6).
  - Research `rr_sports`, `rr_latest_rgames_action` 6.
- **REF2-only:**
  - `PaddleballCourt` (decal quad), `Paddleball_GlassSupports`, `CourtGlass(.001)`, `CourtGlassTop`,
    `Paddleball_Practice_cutout`;
  - physics Ball 0 / 0 / 0.6, Paddle 0.6 / 0.6 / 0, Wall 0 / 0 / 0.6;
  - textures (target, ball decal, poster), `Activity_Image_Paddleball`.
- **Verdict:** research + REF1 (built). Final: strictly 1 v 1, VR only; the build's 2 v 2 and screen mode are
  Somnium extras (a decision to keep or restrict).
- **Needed:** nothing.

### 4.13 Paintball (built: lobby, River, Drive-In)
- **Sources:**
  - REF1 `paintballtroll1`: lobby + River, Drive-in, Quarry, Clearcut, Spillway and Merry Midway, on the official
    scenes (flags, weapon pickups, CTF / Team Battle configs; troll additions to filter). Homestead has no save.
  - Research `rr_shooters`, `rr_latest_rgames_action` 1 (7 maps, 2025 lobby, Splat Hammer, mods).
  - Install: 32 prefabs (weapons, flags).
- **REF2-only:**
  - geometry fragments: `Paintball_Dam_Terrain` (Spillway 256 m terrain, 0-73 m), the `Dam_*` Spillway kit
    (stairs, turbine, red windows, lamps), `QuarryWall_V1/2/6/9`, `PaintballCrane_*` (Quarry), `Paintball_Stump`,
    DriveIn background, a cloud plane;
  - reflection probes River and ClearCut;
  - activity pictures for 6 maps (not Merry Midway);
  - audio: map music (OST Vol 3 tracks 9-14), 10-second map stingers (`PtblClearcut/Homestead/Quarry_10Sec`),
    ambiences (Dam waterfall and crickets, Quarry, Drive-In workshop), `River Predator`;
  - the `MerryMidwayPVP-Home` registry (a community Merry Midway copy; its scene is absent). `Static Variant.prefab`
    may be from it (GUESS).
- **Verdict:** reference + research. The gameplay layer comes from REF1. Spillway's terrain comes from REF2. The rest
  of the geometry is procedural from research and the pictures.
- **Final-rule updates for the built game:**
  - 5 more maps, and only the selected map exists;
  - Drive-In paint thrower and mines;
  - grenade splash hits teammates;
  - head + torso hit zones;
  - mods, the match summary page, the 2025 shared lobby;
  - Merry Midway + Splat Hammer.
- **Needed:** map layouts for Homestead (no REF1), Quarry, Clearcut, Spillway, Merry Midway (videos or top-downs),
  or the Studio copies (e.g. `MerryMidwayPVP`).

### 4.14 Punch Up (not built)
- **Sources:** `rr_latest_rgames_story` 4: never released (an Easter egg). REF2 has `PunchingBag*` and `BoxingGlove`;
  the install has 10 punching-bag meshes.
- **Verdict:** research only (our own design).
- **Needed:** nothing can exist.

### 4.15 Quest for the Golden Trophy (built)
- **Sources:**
  - REF2 GTact1-6 + GoldenLobby (section 3.0) and `Goldenlobby.fbx`.
  - REF1: GoldenLobby props (bows, swords, standees, scoreboard); GTact1-6 are empty.
  - Install: 24 prefabs + kit (Lockers, Scaffolding_2M/6M, Curtain, WineBarrel, WallTorch, Crenellations,
    Bookshelves, FatPot).
  - Research `rr_quests`, `rr_latest_rgames_story` 5.
- **REF2-only:**
  - ~85 meshes: `Goblin_Boss_*` (Goblin King parts), `Goblin_Flying_*`, `Goblin_GroundCannon_*`, `Goblin_Ground_*`,
    `Goblin_Knight_Mesh`, knight costume;
  - audio: "Golden Trophy - Explore / Combat / Clear Fanfare / Welcome Fanfare / The Goblin King",
    `Quest_Goblin_Knight_Charge/Prepare/SpinAttack` (the elite spin), OST tracks 2-4 and 7.
- **Version:** official port, consistent with the final (library act 6, 2020 Store, lava act 4, boulder act 3).
- **Geometry:** parser 37-64 % install meshes (GTact6 64 %, mat 92 %; GTact3 54 %; GTact5 51 %; GTact2 44 %;
  GTact4 41 %; GTact1 37 %) and GoldenLobby 60 % (105 primitives). GTact3 has 29 install prefabs (P); GTact6 has
  562 install renderers. The bounds of GTact3 and PortPlunder include far sky or terrain cards (INDEX note).
- **Missing:** acts 7 (maze), 8 (half lava / half wood) and 9 (Goblin King arena), and the dressing room. GUESS: the dressing room is part of the Foyer scene, which has a `Weapons` group and 6
  `TeleportVolume` / `TeleportDestination` pairs next to the theatre stage (`Quest_Theater_Dept_Marquee`,
  `[StagePieces]`).
- **Verdict:** rebuild from reference for acts 1-6 and the lobby. Acts 7-9 stay research.
- **Final-rule updates for the built game:**
  - **one-hit downs** (no 100 HP);
  - wiki points and the S/A/B/C gold thresholds;
  - the elite barrel knight spins;
  - dressing room with 4 swords, 2 shields and 2 bows;
  - theatre-stage lobby with "Game On!";
  - RRO mods.
- **Needed:** acts 7-9. The Studio room `RROSandBoxRRS` (the registries of GTact1-6 are there) may have more acts.

### 4.16 Rec Rally (not built)
- **Sources:**
  - REF2 `APPPLESARCHIVE-RecRally` = the official Chaparral track (`RecRally_Chaparral.StaticObjects` batches, 166
    rail tracks, 75 nitro pickups, 5 checkpoints, 50 checkpoint spawns, 6 gates).
  - REF1: 28 UGC + 6 `RALLY_BUGGY` (the player layer).
  - Research `rr_latest_rgames_story` 10.
- **REF2-only:**
  - GoKart meshes, `RecRally_StartingGate`;
  - audio: music (FinalLap, FinalLapStinger, Flyover, GameStartFanfare, MainGameplayLoop, YouWin, YouLose),
    henhouse SFX.
  - The install has `(Vehicle_RallyBuggy)`, `(UGC_RecRally_Pickup_Sandbox)` and the `GoKart_*` buggy meshes.
- **Version:** the 2021 track = final (2025 changes are rules: 4 players). The internal name is "Chaparral"; the
  research calls the course "Pioneer Peak" (the same single course, GUESS).
- **Geometry:** parser 31 % install meshes (438 R, 806 X, 228 unknown-size), 401 G markers. The TerrainData is not
  in REF2; GUESS it is the `Terrain_x*_y*` tiles (2.3).
- **Verdict:** rebuild from reference (many stand-ins; the track surface needs the terrain).
- **Needed:** confirm the terrain tiles, or pictures of the track.

### 4.17 Rec Royale (not built)
- **Sources:**
  - REF2 `RecRoyale__PVP-RecRoyale` = the official Frontier Island (Locales, 455 chests, 66 spawn points, gameplay
    zone, cable-car path, Gaia settings; 18,559 MeshFilters, ~16.7k box and 9.2k mesh colliders).
  - REF1: 3 small player layers.
  - Research `rr_shooters`, `rr_latest_rgames_action` 3.
- **REF2-only:**
  - ~68 Frontier meshes (camper, gondola, hang glider, loot chest, outhouse, shed, stables, spike fence);
  - rock chunks;
  - 145 textures;
  - 32 audio clips (glider, gondola, chests, revive, ambiance, OST "Frontier Island").
  - The install has 22 prefabs and 86 meshes (the most install matches of any game).
- **Version:** final (content final since 2020-12).
- **Geometry:** parser 32 % install meshes (1,773 R, 157 P, 3,577 X), 1,346 G markers, 16,876 colliders, a
  1.8 MB RRSK. **The Gaia TerrainData `515f7354` is not in REF2.** Rebuild the ground from marker heights (chests and spawns lie on the ground) plus the rock chunks.
- **Verdict:** rebuild from reference; a large performance job.
- **Also needed:** a player-count decision (the final game needs 14 players for solos, 18 for squads).
- **Needed:** the island terrain (heightmap or top-down), if the reconstruction is not good enough.

### 4.18 Rec Room Showdown (not built)
- **Sources:** research `rr_latest_rgames_action` 4 only. REF2 has only `Standee_WesternBuilding_Large/Medium/Small`
  (standee cut-outs, maybe Showdown decor) and a cowboy costume. The install has nothing named Showdown.
- **Verdict:** research only.
- **Needed:** pictures or video of the Showdown town.

### 4.19 Soccer (not built)
- **Sources:** research `rr_sports`, `rr_latest_rgames_action` 7 (3 min, speed orbs, no shields).
- **REF2-only:** `Soccer_Court_Support`, `Soccer_Glass_corner_Suport`; physics Ball / Environment (0.3 / 0.2 / 0.6);
  `Soccer_Ambiance`, goal and ball-hit SFX; `Activity_Image_Soccer`. `[SoccerShield].fbx` exists, but shields are
  not final.
- **Verdict:** research only (+ physics).
- **Needed:** field dimensions (pictures).

### 4.20 Stunt Runner (not built)
- **Sources:** research `rr_latest_rgames_story` 11. REF2 `StuntLobby` is empty, and the `StuntRunner_Course3/4/5`
  chunks are empty placeholders. The install has 14 prefabs, 9 meshes and 59 `SR_` clips.
- **REF2-only:** `StuntrunnerJumbotron`, `SpringBoard_2x4_*`, `Spinner`; billboard textures (RECtv, Crime Show);
  18 `SR_` announcer and audience clips; OST "Stunt Runner", "NEW JACK RUNNER", "Post Run".
- **Verdict:** research only (+ kit sizes).
- **Needed:** course videos for the 6 courses.

### 4.21 The Curse of the Crimson Cauldron (not built)
- **Sources:** research `rr_latest_rgames_story` 6 (9 acts). No REF2 scene. The install has 26 prefabs.
- **REF2-only:**
  - world chunks `Quest_Goblin2_Bog-24` and `Mines1-16` (2.3);
  - kit `Goblin2_DrawBridge*`, `Quest_Goblin2_Boiler`, `GlowWindow`, `LifeRing` (the bog floats), `WitchBody`,
    `WitchBroom`, `WitchLantern`, `Quest_Caldron_Marquee`, BogMonster;
  - 75 textures; 52 audio clips (potions, bog monster, witch).
- **Verdict:** research only (+ 2 fragments).
- **Needed:** the act scenes (a Studio port like the GT ones), or videos.

### 4.22 The Rise of Jumbotron (not built)
- **Sources:** research `rr_quests` 4.4, `rr_latest_rgames_story` 9. `JumbotronBoss` is empty, and the `Cargo1`
  chunk is an empty placeholder. The install has 32 prefabs and 39 meshes.
- **REF2-only:**
  - kit (~90 meshes): SciFi enemies (Ground, Heavy, Roomba, flying), cover walls, terminals, garage floor decals,
    pillar, reception desk, treasure chest, `JumboTron_Base/Screen/Wires`, `Quest_Jumbotron_Marquee`;
  - 132 textures; 72 audio clips; OST "Jumbotron Explore / Engage", "The Fall of Jumbotron".
  - The Laser Tag beta scene uses the same sci-fi kit (cover walls, `[Quest_Scifi_Foyer_Banner]`), and a Studio room
    `RRJumboTronBossArena` reused it.
- **Verdict:** research only (+ kit sizes).
- **Needed:** the act scenes or videos.

---------------------------------------------------------------------------------------------------------------------

## 5. Parser match rates (from `DN/refdata/INDEX.md`)

The parser (`DN/tools/rrscene`, other iter4 task) wrote one `DN/refdata/<scene>.rrsk` + `.md` per scene.
- `mesh %` = active LOD0 renderers whose mesh is an install Mesh (R) or covered by an install prefab (P), over the
  non-primitive renderers. `X` = stand-ins, `G` = gameplay markers.
- The verdict is the parser's.
- Materials are mostly inferred by vote (the .mat files have no metas).

| activity | scene: mesh % / mat % / X / G / verdict |
| --- | --- |
| Golden Trophy | GoldenLobby 60/54/174/27 partly; GTact1 37/62/68/36; GTact2 44/47/66/94; GTact3 54/67/68/30; GTact4 41/43/47/47; GTact5 51/59/82/34; GTact6 64/92/329/26 (all "partly") |
| Crescendo | CrescendoLobby=Foyer 41/38/82/34; BurningBurg 35/45/165/76; ForestRoad 43/56/81/34; CastleGroundsDay 27/33/121/201 **stand-ins**; GrandHall1/2/3 57/~44/~104/~100; GrandHall1-4 68/39/117/104; Gallery 74/44/122/109 **rebuildable from the install**; ChamberOfKnowledge 45/32/261/281; GardenOfSarrows 37/50/78/110; InvertedBallroom 32/35/98/156; LowerSanctum 11/36/300/212 **stand-ins**; ConcertHall 25/46/154/148 **stand-ins** |
| Isle | IsleLobby 49/52/126/21; GalIery 51/75/374/134; PiratesBanquet 61/78/366/170; HighSeas 10/34/261/113 **stand-ins**; PortPlunder 32/42/431/128; GibbetJungle 28/34/395/135 **stand-ins**; Caves 43/64/470/232; SunkenShip 14/54/434/175 **stand-ins**; GhostBeardGauntlet 31/48/104/61 |
| Rec Rally | RecRally 31/31/806/401 partly |
| Rec Royale | RecRoyale 32/57/3577/1346 partly (1.8 MB RRSK) |
| Make It To Midnight | MakeItToMidnight 62/84/210/1603 partly (1,603 G = unresolved prefab instances) |
| Laser Tag (beta) | LaserTagBeta 23/23/1599/182 **stand-ins** (635 primitives) |
| (Orientation) | OrientationStage1 47/53/92/9; Stage2 33/35/138/20; StageEnd 9/20/217/12 stand-ins |
| empty | CastleGrounds, GrandHall4, JumbotronBoss, StuntLobby, Testing |

Paintball gameplay layers from REF1 (`DN/refdata/Paintball_<Map>_gameplay.rrsk`; official-looking G set / weapon
pickups; troll extras are marked `G extra`):
- Clearcut 74 / 29
- Drive-in 51 / 24
- Merry Midway 45 / 25
- Quarry 68 / 30
- River 63 / 27
- Spillway 82 / 46
- Lobby 4 / 1

Each map has 2 flag bases and 2 game configs.

How the two measures relate:
- My section 3.0 split counts every MeshFilter, including LOD1/2 and inactive ones, and needs an exact mesh-name
  match. It is therefore lower on "install".
- Use the parser's numbers for planning.
- Use 3.0 to see where the gap is: REF2-only versus missing.

---------------------------------------------------------------------------------------------------------------------

## 6. Next iterations (each one shippable and testable in the world)

Order: the most complete references and the most-loved games first. Each one reuses what exists: the quest framework
from Golden Trophy and the shooter framework from Paintball.

| iter | content (1-3 places or games) | reference used | test in the world |
| --- | --- | --- | --- |
| 4 | **Golden Trophy from reference**: lobby + acts 1-6 from the REF2 RRSK (acts 7-9 keep the research build) + the final quest rules (one-hit downs, points, ranks, spinning elite knight, dressing room) + **Dodgeball final rules** (no catch-outs, 14 m/s screen throw, barriers) | REF2 GT scenes (best install share), research | run all 9 acts with 1-4 players; compare the lobby with `Activity_Image_Quest_Golden_Trophy` |
| 5 | **Paintball final**: the REF1 gameplay layer on River and Drive-In (flags, pickups), the final rules (grenade splash, hit zones, selected map only), + **Spillway** (REF2 terrain + Dam kit + REF1 layer) | REF1 paintballtroll1, REF2 terrain, research | CTF and Team Battle on 3 maps |
| 6 | **Isle of Lost Skulls** (lobby + 8 acts) on the quest framework | REF2 Isle scenes + chunks + terrains | a full run, checkpoints |
| 7 | **Rec Rally** (Chaparral) + **Laser Tag Hangar** (chunk envelope + research, TDM with bots) | REF2 RecRally, Mesh chunks | a 4-player race; 4 v 4 with bots |
| 8 | **Crescendo of the Blood Moon** (lobby + 13 acts) | REF2 CBM scenes | a full run, mirrors vs Dracula |
| 9 | **Make It To Midnight** (cache 35C if approved) + **Rec Royale** (Frontier Island, rebuilt terrain, a lower start threshold) | cache / REF2 | a 5-player MITM round; a Rec Royale match with a small threshold (performance check VR) |
| 10 | Paintball maps 4-7 (Quarry, Clearcut, Homestead, Merry Midway) + Cyber Junk City | REF1 layers + research + pictures | map rotation |
| 11 | Sports: Soccer, Disc Golf (Lake + Propulsion), Bowling (with the pool table) | research + REF2 kit sizes and physics + pictures | matches / rounds |
| 12 | The rest: Stunt Runner, Showdown, Crimson Cauldron, Jumbotron, My Little Monsters, Punch Up | research (+ fragments) | per game |

Built games that need final-rule updates (from the rr_latest_* docs):
- **Dodgeball**: catch-outs removed (2025-01), constant screen throw, barriers, mods.
- **Golden Trophy** (and every future quest): one-hit downs, high-five revive, fixed scoring and ranks.
- **Paintball**: maps, modes, mods, splash and hit zones, the 2025 lobby, Merry Midway and the Splat Hammer.
- **Paddleball**: 1 v 1 VR-only is the original (the build's extras are a decision for the user).
- **3D Charades**: the pen features and the minimum player count.
- **Dorm**: the small deltas in `rr_latest_places` 2.3.

---------------------------------------------------------------------------------------------------------------------

## 7. User-provided references that would still help (in order of value)
1. **OK to measure `HTTPCache/35C.data` for Make It To Midnight** (the official final scene is already on disk).
2. **The missing Studio scenes whose registries are in REF2**:
   - `RROSandBoxRRS` (GT; maybe acts 7-9);
   - `AllIsleLevelsRRS`, `CrescendoIsleFly`, `CrescendoIsleSandbox`, `Crescen-Do`, `DraculaHalls`,
     `Grand-Hall-KNP`, `ProjectGhostDrac` (fill the Isle and CBM gaps);
   - `MerryMidwayPVP` (Merry Midway);
   - `2019_Rec_Center` and `2021RRS-RecCenter`;
   - `RRJumboTronBossArena`.
   They would come from the same ripping source as REF2.
3. **Paintball map layouts**: Homestead (no REF1 save), Quarry, Clearcut, Spillway, Merry Midway; top-down
   screenshots or walkthrough videos.
4. **Laser Tag Hangar and Cyber Junk City** layouts (videos).
5. **Terrain** for Rec Royale (Frontier Island) and confirmation of Rec Rally's.
6. Videos or pictures of: Showdown, Stunt Runner courses, My Little Monsters, the Crimson Cauldron and Jumbotron acts,
   Bowling (Lone Shoe Lanes), Soccer field, Disc Golf courses.

---------------------------------------------------------------------------------------------------------------------

## 8. Method and work files (scratchpad, not shipped)
Work dir `...\1c0947a6-...\scratchpad\cov\`:
- `ref2_files.tsv`: size, date and path of every REF2 file.
- `ag.tsv`: `.assets` meta GUID -> mesh name.
- `scene_meshrefs.tsv`, `scene_mf.tsv` (scene, GameObject, mesh GUID) and `scene_mf_summary.txt` / `scene_pct.tsv`
  (section 3.0).
- `roots.awk` (root objects), `mf.awk` (MeshFilters), `glbb.pl` (glb POSITION bounds).
- `groups.txt` / `agroups.txt` (keyword groups).
- `ref2_only_*.tsv` (REF2-only names per group).

Name matching is exact (after stripping ` (n)` and GUID suffixes). The keyword grouping is approximate: counts in
section 4 are indicative.
