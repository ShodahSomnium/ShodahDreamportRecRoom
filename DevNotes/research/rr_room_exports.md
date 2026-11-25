# Rec Room room exports: what persisted_room_data holds

Source folder (read-only): `D:\Dreamport\EXTERNAL FILES FOR REFERNCE\Rec\Rooms` (14 zips + 2 extracted folders,
exported with Rec Room's own end-of-service export tool).
Reference/measurement only: nothing from these files is shipped in the world or loaded at runtime.
Status: complete (2026-09-24). "GUESS" marks inferences that the data cannot prove.

## 0. Verdict (short)

**No export holds Rec Room's own default object layout for an official room.** `persisted_room_data` is only the
*player save layer* of a subroom: the spawned props, Maker Pen shapes, circuits, lights and text the room owner
placed. The official base scene (walls, furniture that is part of the scene, official spawn points, doors built into
the scene) is referenced only by `Subroom.json -> UnitySceneId` and is never in the data.

What each group of rooms really is:

| export | base scene | what the data is | useful for |
| --- | --- | --- | --- |
| TheRecRoomArchiveVolume1 (Dorm 2018/2019/2021/UnknownDate, RecCenter 2018/2020/2021, Orientation, LockerRoom-2017, RecRoyale, MakeItToMidnight, LaserTagBeta...; Paddleball is the exception, on its official scene, see section 2) | **`a75f7547...` = the blank Maker room** (89 of 103 subrooms in all exports use it; a fresh copy holds only 3 GAME_ROLE_NODEs + 1 WELCOME_MAT) | the archivist's (account 1257174) **hand-made reconstructions** of old official rooms: Maker Pen shapes + unresolvable UGC objects (the geometry) + standard spawnable props placed to match | **Dorm: yes, as a second opinion on prop positions.** The reconstruction uses the *same room frame* as the Dorm kit in `rr_dorm_core.md` section 3 (loft floor 2.75, loft shelves ~3.5, pillar at the back-right corner, hoop spot), so its positions can be dropped straight into our DormRoot. Rec Center: consistent frame between the archivist's versions but cannot be tied to anything we have. |
| APPPLESARCHIVE (quests: GTact1-6, GoldenLobby, Crescendo halls, Isle rooms...) | blank Maker room `a75f7547` | reconstructions / prop dumps by 1257174. GTact1-6, MakeItToMidnight = **empty** (the 4 default blank-room objects). Crescendo and Isle rooms hold many quest props (pirate bottles, flintlocks, shovels...) in large-coordinate frames, several knocked over (standees at rx 270) = a snapshot of a played state | weak: prop mixes and counts per quest room; no geometry, no official spawn logic |
| paintballtroll1 (account **1944026861**; River saved by 78961143) | **official paintball scenes** (a different UnitySceneId per map) | a player copy of RRO Paintball: official-looking gameplay layer (2 GAME_CONFIGURATIONs "Paintball Capture The Flag"/"Paintball Team Battle", CTF + Team Battle rule circuits, 2 FLAG_GOAL + 2 DEPRECATED_FLAG_RED, weapons with ranged-weapon settings, standees, score/leaderboards, role nodes with the weapon lists) mixed with the troll's own additions (Maker Pen builds, costume dummies, texts, buttons, audio samplers, projectile launchers) | **Paintball: moderately.** Flag positions, weapon pickup positions (Quarry and River are ~50-65 % point-symmetric = official-looking), game config names/stats. No player spawn points (those are in the scene). Homestead has no save. |
| DormRoom (1717616, a real 2019 dorm) | `76d98498...` (the official Dorm scene, GUESS by elimination) | the owner's own "PRINTER" pixel build (212 markers + collisions); **no** default dorm props (radio, basketball, mirror...) are in the save | confirms the frame only (loft wall x -4.3, loft back wall z -2.717) |
| LAZERTAGCOPYd | `239e676c...` (official Laser Tag scene, GUESS) | 16 views: ARENA_RAILGUN x4, ARENA_SHOTGUN x2, ARENA_SMG x2, ARENA_GRENADE, 2 leaderboards, 1 shape container | weapon pickup positions for that copy only |
| 2019_Rec_Center, AppleSausesRecCenter, Gambino, SkyCafe, Graduation, Calmjourney, coloredspeedball, APPLESRECCON, JAPENESEASSETSRECROOM | blank Maker room (Japanese assets: `5d4e40d8`) | player builds / reconstructions | not for the official rooms |

Creators (`RoomDetails.json -> CreatorAccountId`): **every export is account 1257174** except paintballtroll1
(**1944026861**). All archive subrooms were last saved by 1257174 between 2026-02-28 and 2026-04-01.

**Best use for Dreamport:**
1. **Final Dorm** (2018 "Spring Cleaning" layout): take `DormRoom-2018` (section 3.1) as the prop layout. `DormRoom2021`
   is the same save with a few swaps (door, markers, polaroid, fewer lights), so both describe the same 2018+ room.
   Main corrections to our guess (details in 3.5): **exit door on the left wall under the loft (x -4.82, facing +x)**,
   **mirror on the front wall at (0.88, 1.34, 3.01) facing -z**, **bed/desk against the back wall** (desk top
   y 0.85, radio at (1.38, 0.88, -2.53)), spawn/welcome mat under the loft at (-3.97, 0.04, -0.92).
2. **Rec Center**: only relative layout ideas (pool table, ping-pong, signs "Featured Rooms", "Give It Your Rec Room
   Best!", "Magic Door", "PVP", "Quest", door to the Dorm). No link to an official frame.
3. **Paintball**: flag bases and weapon pickups per map from paintballtroll1 (section 5), with care: the troll moved
   or added things (Drive-in flags at y 18, many extra weapons on Spillway).
4. **Quests**: nothing layout-grade; only which props appear in which quest room (section 6).

## 1. File format (verified)

- `persisted_room_data.binpb` = message `rec_room.PersistedRoomData` from the export's own `descriptor_set.binpb`
  (886 messages, 66 enums). All 95 saved subrooms decode with 0 errors; the only unknown field is an empty `#35` in 14 places in two APPLESRECCON subrooms (LiveAMA, 0FashionShow), irrelevant here.
- `PersistedRoomData.persistence_views` (field 2, repeated `PersistenceViewData`) = **one entry per placed object**:
  `id` (bytes, GUID), `transform` (`TransformData`: `position` Vector3, `quaternion_rotation` (w,x,y,z), uniform
  `scale`), `spawnable_tool_data.prefab_id` (16-byte GUID = the object type), plus ~120 optional component
  messages (`shape_container_data` for Maker Pen shapes, `room_door_data`, `welcome_mat_v2_data`, `seat_data`,
  `text_label_data`/`message_data`, `sandbox_colorable_data` colour index/material, `tagged_tool_data` tags,
  `game_configuration_data`, `ranged_weapon_data`...). Other top-level fields: circuits (`circuit_v2_data`), roles,
  scene settings, fog/sun/skydome mood, navmesh.
- Positions are metres, Unity axes (left-handed, y up). Rotations are Unity quaternions; euler angles in the tables
  are Unity's `Quaternion.eulerAngles` convention (checked against the redundant euler copies Rec Room stores in
  `sticky_data` / `tool_cleanup_data`, which agree to 0.001 deg).
- **Prefab GUIDs = the `typeId` of the `SpawnableTool` component in the game's bundles.** 250 of the 251 distinct
  GUIDs in all `PrefabIds.csv` files are among the 645 `SpawnableTool` components in the bundle index, e.g.
  BASKETBALL 60cf49d9... = `(Basketball)`, RADIO 933d5393... = `(Radio)`, SANDBOX_MIRROR 701638be... =
  `(Sandbox_Mirror)`. Full map: `DevNotes\tools\roomexport\spawnable_typeids_from_bundles.tsv` (typeId, prefab root,
  bundle). The Dorm kit pieces have their own typeIds (e.g. `(Dorm_Bunk_Bed)` 8a53c4f4-a0f1-454b-9da9-f3b0375a27e8,
  `(Dorm_FreeMirror)` 5182b200..., `(Dorm_Mirror_Buttons)` 61c3100a...), and **none of them occurs in any export**.
- 328 distinct prefab GUIDs in the exports are in neither `PrefabIds.csv` nor the bundles. They carry
  `shape_container_data` + `control_panel_data` and most have a Unity-asset-style GUID layout (no v4 version
  nibble). GUESS: Rec Room Studio / UGC objects (the archive's Warnings.log lists skipped inventions such as
  "2018 Dorm And GT Banner", "OG Rec Room Lights", "RRS Donuts and Donut Box"). Shown as `UGC xxxxxxxx` below;
  their meshes are not exported (only Maker Pen shapes go to Scene.glb).

## 2. Base scenes seen

| UnitySceneId | subrooms | identity |
| --- | --- | --- |
| a75f7547-79eb-47c6-8986-6767abcb4f92 | 89 (every archive subroom, all player builds) | blank Maker room (fresh content: GAME_ROLE_NODE x3 at (-5.34/-5.14/-5.54, 1.21, 4.49) + WELCOME_MAT_V2) |
| 76d98498-60a1-430c-ab76-b54a29b7a163 | DormRoom (1717616) "Home" | official Dorm scene (GUESS: only real dorm here) |
| 41dcd3f4-4435-4eff-8b4d-c7603ef17c59 | paintballtroll1 Lobby, archive vol.1 "Lobby" (no save) | Paintball lobby |
| e122fe98-e7db-49e8-a1b1-105424b6e1f0 | River | Paintball River |
| 65ddbb48-5a01-4e3e-972d-e5c7419e2bc3 | Drive-in | Paintball Drive-in |
| ff4c6427-7079-4f59-b22a-69b089420827 | Quarry | Paintball Quarry |
| 380d18b5-de9c-49f3-80f7-f4a95c1de161 | Clearcut | Paintball Clearcut |
| 58763055-2dfb-4814-80b8-16fac5c85709 | Spillway | Paintball Spillway |
| a8b02f8f-7c7c-4271-8d13-6151348c4fc1 | paintballtroll1 MerryMidway, archive vol.1 "MerryMidwayPaintballmap" (no save) | Paintball Merry Midway |
| a785267d-c579-42ea-be43-fec1992d1ca7 | Homestead (no save) | Paintball Homestead |
| 239e676c-f12f-489f-bf3a-d4c383d692c3 | LAZERTAGCOPYd | Laser Tag (GUESS) |
| 5d4e40d8-f289-4295-a6e1-4f907835007d | JAPENESEASSETSRECROOM | unknown official scene |
| d89f74fa-d51e-477a-a425-025a891dd499 | archive vol.1 Paddleball | official Paddleball scene (GUESS); 8 views = RRO gameplay layer: GAME_CONFIGURATION, PADDLEBALL_GAME_RULE_CIRCUIT, PADDLEBALL_PADDLE, SCOREBOARD_PROJECTOR, LEADERBOARD_PROJECTOR, 3 roles |

(Archive subrooms with **no save**: MerryMidwayPaintballmap, Lobby (archive vol.1); GrandHall4, StuntLobby,
CastleGrounds, JumbotronBoss, Testing (APPPLESARCHIVE).)

## 3. Dorm versions (archive vol.1, blank-room reconstructions by 1257174)

All four are on the blank Maker room, so they are the archivist's rebuilds, not Rec Room's default save. But three
of them (2018, 2019, 2021) sit in **exactly the Dorm kit's room frame** of `rr_dorm_core.md` 3.1: markers lie on
the loft floor at y 2.768 (kit loft floor top 2.75) over x -3.3..-4.3 (kit loft x -4.94..-2.63), more markers on
the loft shelves at y 3.574 (kit shelves 3.48-4.34) against the left wall, the whiteboard hangs on the loft's left
wall, the door and welcome mat are under the loft, the desk props line the back wall (z -2.0..-2.6), and the ball
lies in front of the pillar face where our data-backed rim is. So these numbers can be used directly in DormRoot.

| version | views | Maker Pen shapes | Scene.glb | notes |
| --- | --- | --- | --- | --- |
| DormRoom-2018 | 67 | 856 (4 containers) | 11.4 MB | fullest; 7 UGC geometry objects, 9 point lights, 4 SFX nodes; **= the final (Spring Cleaning 2018) dorm** |
| DormRoom2021 | 46 | 109 (1 container) | 5.1 MB | a copy of the 2018 save: 14 objects at identical coordinates (radio, mirror, bucket, plants, seat, table, text, mat, ball, eraser, UGC pieces); adds ROOM_DOOR, POLAROID, yellow markers, 4 darts; drops the lights, canvases, clipboard, jack-o-lantern |
| DormRoom2019 | 25 | 0 | none | same frame, sparse: desk props, loft markers + whiteboard, ROOM_DOOR_V2, mirror, plant, bucket; positions 0.1-0.5 m from the 2018 ones |
| DormRoom-UnknownDate | 65 | 0 | none | different, older layout (29 INVISIBLE_COLLISION_V2 + 19 UGC pieces; loft props at y ~3.6-3.7; door to "LockerRoom-2017"; welcome mat (-2.10, 0.04, -0.95)); GUESS the pre-2018 dorm |

### 3.1 DormRoom-2018 (all objects; room frame, metres; rotation = Unity euler x, y, z)
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| ART_CANVAS | 0.337 | 2.034 | 3.016 | 0, 181, 258 | 0.319 | image_name=44qp6fjmy9ufgjydhjr5s6qwr.png |
| ART_CANVAS | 1.315 | 2.058 | 3.019 | 0, 181, 282 | 0.313 | image_name=clmwwovkdnfqu8aijhuit1zyv.png |
| BASKETBALL | 3.409 | 0.136 | -1.142 | 359.888, 270, 0 | 1 |  |
| BUCKET | -2.163 | 0.013 | -2.636 | 0.106, 306.161, 0.035 | 1 |  |
| CLAMP_GIZMO | -1.106 | 1.472 | -1.673 | 0, 1, 0 | 1 |  |
| DART | 0.781 | 0.053 | 2.935 | 38.062, 357.521, 267.608 | 1 |  |
| DART | 1.906 | 0.954 | -2.072 | 63.212, 86.007, 182.029 | 1 |  |
| DART | 2.057 | 1.031 | -2.055 | 63.212, 86.007, 182.029 | 1 |  |
| DRAWABLE_CLIPBOARD | -3.757 | 2.757 | 1.31 | 0, 72.001, 0 | 1 |  |
| DRAWABLE_CLIPBOARD | 2.887 | 0.015 | 1.913 | 0, 193, 0 | 1 |  |
| DYNAMINC_POINT_LIGHT | 1.069 | 1.246 | 0.81 | 359.825, 358.159, 359.881 | 1 | pal30 |
| DYNAMINC_POINT_LIGHT | 1.125 | 1.195 | 0.407 | 359.666, 358.001, 359.882 | 1 | pal30 |
| DYNAMINC_POINT_LIGHT | 1.307 | 1.246 | 0.818 | 359.825, 358.159, 359.881 | 1 | pal30 |
| DYNAMINC_POINT_LIGHT | 1.363 | 1.193 | 0.415 | 359.666, 358.001, 359.882 | 1 | pal30 |
| DYNAMINC_POINT_LIGHT | 1.545 | 1.245 | 0.826 | 359.825, 358.159, 359.881 | 1 | pal30 |
| DYNAMINC_POINT_LIGHT | 1.601 | 1.194 | 0.423 | 359.666, 358.001, 359.882 | 1 | pal30 |
| DYNAMINC_POINT_LIGHT | 1.783 | 1.245 | 0.834 | 359.825, 358.159, 359.881 | 1 | pal30 |
| DYNAMINC_POINT_LIGHT | 1.84 | 1.192 | 0.431 | 359.666, 358.001, 359.882 | 1 | pal30 |
| DYNAMINC_POINT_LIGHT | 2.021 | 1.246 | 0.84 | 359.825, 358.159, 359.881 | 1 | pal30 |
| ERASER | -3.355 | 2.756 | -2.136 | 0, 253.032, 0 | 1 |  |
| GAME_ROLE_NODE | -5.538 | 1.21 | 4.493 | 0, 0, 0 | 1 | role_name=Everyone / role_guid=c48bac02-a743-4797-94c4-e2d6f... |
| GAME_ROLE_NODE | -5.337 | 1.21 | 4.493 | 0, 0, 0 | 1 | role_name=In-Game / role_guid=2c721342-c7ed-49fe-b8f0-93f8d2... |
| GAME_ROLE_NODE | -5.138 | 1.21 | 4.493 | 0, 0, 0 | 1 | role_name=Eliminated / role_guid=0b9ec39e-2cc9-4825-8321-da1... |
| INTERACTION_VOLUME | -4.681 | 1.046 | -0.876 | 0, 90, 0.112 | 0.04 | interaction_string=Go to Room-i-verse |
| JACKOLANTERN | 3.987 | 1.241 | 2.392 | 0.338, 32.018, 359.685 | 1 |  |
| MARKER_BLACK | -4.77 | 3.574 | 1.339 | 270, 165.964, 90 | 1 |  |
| MARKER_BLACK | -4.689 | 3.574 | -0.31 | 270, 313.481, 271.889 | 1 |  |
| MARKER_BLACK | -4.638 | 3.574 | 0.55 | 270, 103.627, 116.565 | 1 |  |
| MARKER_BLACK | -4.305 | 2.768 | -0.661 | 270, 240.255, 270 | 1 |  |
| MARKER_BLACK | -4.196 | 2.768 | -1.534 | 270, 187.125, 102.529 | 1 |  |
| MARKER_BLACK | -3.831 | 2.768 | -0.91 | 89.98, 233.973, 267.589 | 1 |  |
| MARKER_BLACK | -3.747 | 2.768 | -2.013 | 270, 159.444, 104.036 | 1 |  |
| MARKER_BLACK | -3.587 | 2.768 | -1.372 | 270, 216.87, 93.053 | 1 |  |
| MARKER_BLACK | -3.523 | 2.768 | -0.295 | 270, 342.926, 270 | 1 |  |
| MARKER_BLACK | -3.345 | 2.768 | -0.969 | 270, 27.78, 264.766 | 1 |  |
| MARKER_ERASER | -4.119 | 2.768 | 0.499 | 270, 16.699, 234.782 | 1 |  |
| MOVEABLE_WHITEBOARD | -4.347 | 3.696 | -1.239 | 0, 91.351, 0 | 1 | image_name=do02wpeqldvnqya66z43xjoeb.png |
| NAVMESH_BAKE_VOLUME | -0.315 | 1.264 | 0.755 | 0, 0, 0 | 1 |  |
| PING_PONG_BALL | -4.337 | 0.43 | 0.382 | 359.131, 90.513, 55.205 | 1 |  |
| PING_PONG_PADDLE | -4.396 | 0.424 | 0.861 | 0.017, 278.692, 359.889 | 1 |  |
| PLANT_DRACAENA | -2.663 | 0.017 | 2.227 | 0, 146.857, 0 | 1 |  |
| PLANT_FIG | -4.422 | 0.017 | 1.429 | 0.027, 129.177, 0.118 | 1 |  |
| PLANT_SNAKE | -4.511 | 0.001 | -2.427 | 0.275, 89.726, 358.547 | 1 |  |
| RADIO | 1.381 | 0.883 | -2.529 | 0, 308.043, 0 | 1 |  |
| SANDBOX_MIRROR | 0.882 | 1.339 | 3.011 | 359.921, 179.921, 359.921 | 1.023 | pal12 |
| SANDBOX_SEAT | -3.733 | 0.347 | 1.962 | 0, 132.644, 0.104 | 1.519 |  |
| SANDBOX_TEXT_V3 | 3.778 | 0.888 | 1.972 | 359.901, 213.174, 0.053 | 0.818 | message=Weekly Challenge Reward; pal13 |
| SFX_NODE_V2 | -2.921 | 3.394 | 2.755 | 0, 0, 0 | 1 |  |
| SFX_NODE_V2 | -2.825 | 3.394 | 2.755 | 0, 0, 0 | 1 |  |
| SFX_NODE_V2 | -2.731 | 3.394 | 2.755 | 0, 0, 0 | 1 |  |
| SFX_NODE_V2 | -2.636 | 3.394 | 2.755 | 0, 0, 0 | 1 |  |
| SHAPE_CONTAINER | -4.728 | 2.317 | -1.26 | 0.001, 270.999, 0 | 0.939 | 46 shapes |
| SHAPE_CONTAINER | -1.837 | 5.32 | -2.845 | 0.031, 180.997, 354.837 | 1.278 | 397 shapes |
| SHAPE_CONTAINER | -0.21 | 5.371 | -2.858 | 0.031, 180.997, 354.837 | 1.278 | 397 shapes |
| SHAPE_CONTAINER | 3.394 | 0.754 | -1.359 | 0, 0, 0 | 1 | 16 shapes |
| SOLOCUP | 2.157 | 0.853 | -2.25 | 359.897, 182.108, 0.023 | 1 |  |
| SOLOCUP | 2.469 | 0.853 | -2.256 | 359.903, 168.369, 0.02 | 1 |  |
| TABLE_SHORT | -4.39 | 0.052 | 0.585 | 0, 90, 0.112 | 0.815 |  |
| WATERBOTTLE | 1.331 | 0.978 | -2.055 | 1.027, 133.015, 358.067 | 1 |  |
| WELCOME_MAT_V2 | -3.974 | 0.044 | -0.921 | 0.015, 75.028, 0.111 | 1 | welcome_mat_name=Welcome Mat |
| UGC 13ad3ac2 | -4.794 | -0.022 | -0.46 | 0, 90, 0.112 | 1 |  |
| UGC 2ebb29e2 | -0.136 | -0.123 | -2.13 | 15.973, 90, 0 | 1 |  |
| UGC 5d149f59 | -1.607 | -0.646 | 2.284 | 0, 0, 0 | 1 |  |
| UGC 6a052d42 | 4.836 | 1.393 | 0.611 | 0, 0, 0 | 1 |  |
| UGC 7ba50577 | -4.004 | 1.021 | -0.918 | 359.442, 274.736, 51.844 | 0.941 |  |
| UGC 86974492 | -0.687 | -0.071 | 2.55 | 271.49, 325.146, 18.782 | 1 |  |
| UGC c695cd3e | 2.002 | 0.001 | -2.14 | 358.547, 359.793, 0.122 | 1 |  |

### 3.2 DormRoom2021
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| BASKETBALL | 3.409 | 0.136 | -1.142 | 359.888, 270, 0 | 1 |  |
| BUCKET | -2.163 | 0.013 | -2.636 | 0.106, 306.161, 0.035 | 1 |  |
| DART | 0.892 | 0.801 | -1.542 | 346.326, 209.289, 23.961 | 1 |  |
| DART | 1.236 | 0.889 | -1.574 | 355.999, 199.305, 46.948 | 1 |  |
| DART | 2.4 | 0.021 | 2.441 | 5.917, 6.057, 268.529 | 1 |  |
| DART | 3.253 | 0.908 | -2.027 | 25.636, 5.12, 94.088 | 1 |  |
| ERASER | -3.355 | 2.756 | -2.136 | 0, 253.032, 0 | 1 |  |
| GAME_ROLE_NODE | -5.538 | 1.21 | 4.493 | 0, 0, 0 | 1 | role_name=Everyone / role_guid=c48bac02-a743-4797-94c4-e2d6f... |
| GAME_ROLE_NODE | -5.337 | 1.21 | 4.493 | 0, 0, 0 | 1 | role_name=In-Game / role_guid=2c721342-c7ed-49fe-b8f0-93f8d2... |
| GAME_ROLE_NODE | -5.138 | 1.21 | 4.493 | 0, 0, 0 | 1 | role_name=Eliminated / role_guid=0b9ec39e-2cc9-4825-8321-da1... |
| MARKER_ERASER | -4.119 | 2.768 | 0.499 | 270, 345.964, 265.986 | 1 |  |
| MARKER_YELLOW | -4.77 | 3.574 | 1.339 | 270, 156.371, 111.161 | 1 |  |
| MARKER_YELLOW | -4.688 | 3.574 | -0.31 | 270.193, 136.982, 88.444 | 1 |  |
| MARKER_YELLOW | -4.638 | 3.574 | 0.55 | 270, 296.565, 268.398 | 1 |  |
| MARKER_YELLOW | -4.333 | 2.768 | -0.68 | 4.067, 234.961, 270.04 | 1 |  |
| MARKER_YELLOW | -4.196 | 2.768 | -1.534 | 270, 170.538, 131.634 | 1 |  |
| MARKER_YELLOW | -3.844 | 2.768 | -0.897 | 90, 235.176, 261.87 | 1 |  |
| MARKER_YELLOW | -3.747 | 2.768 | -2.013 | 270, 156.371, 103.241 | 1 |  |
| MARKER_YELLOW | -3.587 | 2.768 | -1.372 | 270, 217.476, 90 | 1 |  |
| MARKER_YELLOW | -3.54 | 2.768 | -0.222 | 270, 8.13, 246.801 | 1 |  |
| MARKER_YELLOW | -3.425 | 2.768 | -0.894 | 270, 33.179, 271.79 | 1 |  |
| MOVEABLE_WHITEBOARD | -4.348 | 3.71 | -1.346 | 358.11, 98.662, 359.746 | 1 | image_name=2db83136c21e4c44b91976ff3307b21d |
| PLANT_DRACAENA | -2.663 | 0.017 | 2.227 | 0, 146.857, 0 | 1 |  |
| PLANT_FIG | -4.422 | 0.017 | 1.429 | 0.027, 129.177, 0.118 | 1 |  |
| PLANT_SNAKE | -4.511 | 0.001 | -2.427 | 0.275, 89.726, 358.547 | 1 |  |
| POLAROID | 1.007 | 0.014 | 0.489 | 90, 76.908, 110.854 | 1 | image_name=3mv6tv60a32lg50v2pj2hhhmn.jpg |
| RADIO | 1.381 | 0.883 | -2.529 | 0, 308.043, 0 | 1 |  |
| ROOM_DOOR | -4.821 | -0.01 | -0.39 | 0, 90.552, 0 | 1 | target_name=RecCenter2021 / target_type=RoomScene / invite_m... |
| SANDBOX_MIRROR | 0.882 | 1.339 | 3.011 | 359.921, 179.921, 359.921 | 1.023 | pal12 |
| SANDBOX_SEAT | -3.733 | 0.347 | 1.962 | 0, 132.644, 0.104 | 1.519 |  |
| SANDBOX_TEXT_V3 | 3.778 | 0.888 | 1.972 | 359.901, 213.174, 0.053 | 0.818 | message=Weekly Challenge Reward; pal13 |
| SFX_NODE_V2 | -2.921 | 3.394 | 2.755 | 0, 0, 0 | 1 |  |
| SFX_NODE_V2 | -2.825 | 3.394 | 2.755 | 0, 0, 0 | 1 |  |
| SFX_NODE_V2 | -2.731 | 3.394 | 2.755 | 0, 0, 0 | 1 |  |
| SFX_NODE_V2 | -2.636 | 3.394 | 2.755 | 0, 0, 0 | 1 |  |
| SHAPE_CONTAINER | 4.426 | 1.365 | -1.356 | 0, 0, 180 | 1 | 109 shapes |
| SOLOCUP | -1.716 | 0.066 | 2.228 | 86.49, 118.065, 358.682 | 1 |  |
| TABLE_SHORT | -4.39 | 0.052 | 0.585 | 0, 90, 0.112 | 0.815 |  |
| WATERBOTTLE | -3.804 | 0.041 | -1.347 | 315.021, 176.348, 87.783 | 1 |  |
| WELCOME_MAT_V2 | -3.974 | 0.044 | -0.921 | 0.015, 75.028, 0.111 | 1 | welcome_mat_name=Welcome Mat |
| UGC 2ebb29e2 | -0.136 | -0.123 | -2.13 | 15.973, 90, 0 | 1 |  |
| UGC 5d149f59 | -1.607 | -0.646 | 2.284 | 0, 0, 0 | 1 |  |
| UGC 64d8ca74 | -0.825 | 1.544 | -2.855 | 0.092, 0, 0 | 1 |  |
| UGC 6a052d42 | 4.836 | 1.393 | 0.611 | 0, 0, 0 | 1 |  |
| UGC 86974492 | -0.687 | -0.071 | 2.55 | 271.49, 325.146, 18.782 | 1 |  |
| UGC c695cd3e | 2.002 | 0.001 | -2.14 | 358.547, 359.793, 0.122 | 1 |  |

### 3.3 DormRoom2019
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| BUCKET | -1.737 | 0.012 | -2.473 | 0, 290.201, 0 | 1 |  |
| DART | 2.504 | 0.859 | -2.112 | 5.844, 11.017, 0 | 1 |  |
| DART | 2.578 | 0.859 | -2.093 | 5.855, 347.783, 0 | 1 |  |
| ERASER | -3.619 | 2.756 | -0.278 | 0, 22.437, 0 | 1 |  |
| GAME_ROLE_NODE | -5.538 | 1.21 | 4.493 | 0, 0, 0 | 1 | role_name=Everyone / role_guid=c48bac02-a743-4797-94c4-e2d6f... |
| GAME_ROLE_NODE | -5.338 | 1.21 | 4.493 | 0, 0, 0 | 1 | role_name=In-Game / role_guid=2c721342-c7ed-49fe-b8f0-93f8d2... |
| GAME_ROLE_NODE | -5.138 | 1.21 | 4.493 | 0, 0, 0 | 1 | role_name=Eliminated / role_guid=0b9ec39e-2cc9-4825-8321-da1... |
| MARKER_ERASER | -3.584 | 2.768 | -1.863 | 90, 351.87, 118.072 | 1 |  |
| MARKER_YELLOW | -4.196 | 2.768 | 0.555 | 90, 239.657, 99.866 | 1 |  |
| MARKER_YELLOW | -4.181 | 2.768 | 0.601 | 90, 303.44, 271.762 | 1 |  |
| MARKER_YELLOW | -4.072 | 2.768 | 1.08 | 90, 266.618, 270 | 1 |  |
| MARKER_YELLOW | -4.034 | 2.768 | 0.839 | 90, 238.841, 267.686 | 1 |  |
| MARKER_YELLOW | -3.995 | 2.768 | 1.548 | 90, 317.816, 95.08 | 1 |  |
| MARKER_YELLOW | -3.761 | 2.768 | -1.164 | 90, 346.866, 268.152 | 1 |  |
| MARKER_YELLOW | -3.752 | 2.768 | -0.822 | 90, 28.301, 256.373 | 1 |  |
| MARKER_YELLOW | -3.72 | 2.768 | -0.64 | 90, 231.34, 180 | 1 |  |
| MOVEABLE_WHITEBOARD | -3.87 | 3.729 | -0.801 | 0, 93.29, 0 | 1 |  |
| PLANT_SNAKE | -4.128 | 0.017 | -2.109 | 0, 3.29, 0 | 1 |  |
| RADIO | 1.533 | 0.865 | -2.608 | 0, 273.177, 0 | 1 |  |
| ROOM_DOOR_V2 | -4.775 | 0.022 | -0.425 | 0.003, 89.468, 0.051 | 1 | destination_room_name=earthys-rec-room-archive; pal50 |
| SANDBOX_MIRROR | 0.889 | 1.339 | 3.014 | 0, 180, 0 | 1 | pal12 |
| SOLOCUP | 2.121 | 0.849 | -2.635 | 0.004, 3.338, 0 | 1 |  |
| SOLOCUP | 2.347 | 0.849 | -2.539 | 0.004, 350.994, 0 | 1 |  |
| WATERBOTTLE | 2.175 | 0.971 | -2.271 | 0, 34.049, 359.938 | 1 |  |
| UGC 5af58975 | -0.707 | 0.021 | 2.57 | 270, 29.812, 320.182 | 1 | pal13 |

### 3.4 DormRoom-UnknownDate
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| BASKETBALL | -3.561 | 0.176 | 2.571 | 74.667, 244.024, 341.043 | 1 |  |
| DART | 3.019 | 0.86 | -2.028 | 5.86, 169.424, 270.118 | 1 |  |
| DART | 3.405 | 0.86 | -1.68 | 5.807, 342.609, 359.882 | 1 |  |
| ERASER | -3.501 | 3.728 | -2.033 | 270.04, 51.111, 202.467 | 1 |  |
| GAME_ROLE_NODE | -2.635 | 2.845 | -2.794 | 0, 180, 0 | 1 | role_name=Eliminated / role_guid=0b9ec39e-2cc9-4825-8321-da1... |
| GAME_ROLE_NODE | -2.435 | 2.845 | -2.794 | 0, 180, 0 | 1 | role_name=In-Game / role_guid=2c721342-c7ed-49fe-b8f0-93f8d2... |
| GAME_ROLE_NODE | -2.235 | 2.845 | -2.794 | 0, 180, 0 | 1 | role_name=Everyone / role_guid=c48bac02-a743-4797-94c4-e2d6f... |
| INVISIBLE_COLLISION_V2 | -4.026 | 1.112 | -0.005 | 0, 90, 0.112 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -3.887 | 2.315 | -0.005 | 0, 90, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -3.88 | 4.043 | -0.055 | 0, 90, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -3.88 | 3.492 | -0.055 | 0, 90, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -3.84 | 2.45 | 2.545 | 0, 180, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -3.84 | 2.45 | -2.635 | 0, 180, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -3.03 | 3.492 | -2.633 | 0, 90, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -3.03 | 4.038 | -2.633 | 0, 90, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -2.93 | 2.535 | -0.005 | 0, 90, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -1.9 | 3.05 | -0.545 | 0, 0, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -1.872 | 2.315 | -0.005 | 0, 90, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -1.855 | 2.45 | -2.635 | 0, 180, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -1.855 | 2.45 | 2.545 | 0, 180, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -0.472 | 0.997 | -1.562 | 0, 0, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -0.462 | 0.997 | -2.693 | 0, 0, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -0.462 | 0.947 | -2.117 | 0, 0, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | -0.122 | 0.003 | 0.005 | 0.079, 0.079, 0.079 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 0.38 | 2.305 | -3.071 | 0, 180, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 0.38 | 2.306 | 3.022 | 359.921, 179.921, 0.079 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 0.581 | 0.542 | 2.542 | 359.921, 179.921, 0.079 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 0.633 | 0.947 | -2.695 | 0, 0, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 0.633 | 2.027 | -2.132 | 0, 0, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 1.602 | 0.997 | -2.693 | 0, 0, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 1.602 | 0.997 | -1.562 | 0, 0, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 1.622 | 0.947 | -2.117 | 0, 0, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 2.105 | 0.218 | 2.408 | 0, 175.596, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 2.913 | 0.94 | 2.39 | 0, 180, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 3.415 | 0.808 | -1.675 | 359.888, 270, 0 | 0.005 |  |
| INVISIBLE_COLLISION_V2 | 3.788 | 2.536 | -0.005 | 0, 90, 0.112 | 0.005 |  |
| MARKER_MAGIC | -3.494 | 3.617 | -1.216 | 270, 279.16, 266.201 | 1 | pal19 |
| MESSAGE_NODE | 1.681 | 1.64 | -0.176 | 0, 74.874, 0 | 1 | messages=You, Have Made A New Friend! |
| MOVEABLE_WHITEBOARD | -3.544 | 3.595 | -1.273 | 0, 88.202, 0 | 1 | image_name=23qmckgnedbd0ja9nw6dql20d.png |
| PING_PONG_BALL | -2.33 | 0.074 | 2.14 | 11.982, 1.28, 12.115 | 1 |  |
| PING_PONG_PADDLE | -2.487 | 0.07 | 2.248 | 0.047, 249.552, 359.898 | 1 |  |
| ROOM_DOOR | -4.041 | 0.03 | -0.513 | 0, 90, 0 | 1 | target_name=LockerRoom-2017 / target_type=RoomScene / invite... |
| SANDBOX_BUTTON | 0.854 | -0.616 | 0.007 | 90, 180, 180 | 1 | messages=Warnig DONT PUSH! |
| SANDBOX_MIRROR | 0.577 | 1.403 | 2.659 | 359.921, 179.921, 0.079 | 0.973 | pal8685676 |
| SFX_NODE_V2 | 0.784 | 2.56 | 0.075 | 347.343, 262.256, 0 | 1 |  |
| TRIGGER_VOLUME_CV2 | 0.704 | 2.538 | 0.051 | 12.657, 82.256, 0 | 0.005 | tags=basketball |
| WELCOME_MAT_V2 | -2.101 | 0.039 | -0.947 | 0, 64.443, 0 | 1 | welcome_mat_name=Welcome Mat |
| UGC 0deb057a | 2.042 | 2.188 | 2.711 | -0, 180, 270 | 1 |  |
| UGC 20b8d8bc | 1.4 | 5.518 | 0.14 | 90, 180, 180 | 1 |  |
| UGC 2657eb9d | -2.082 | 2.611 | 0.866 | 270, 303.69, 296.565 | 1 |  |
| UGC 2b45a3d8 | -3.536 | 2.61 | 2.086 | 0, 136.6, 0 | 1 |  |
| UGC 35f95a4c | 3.216 | 0.851 | -1.464 | 359.889, 255.028, 0.015 | 1 |  |
| UGC 472f30f7 | 3.293 | 0.015 | -1.949 | 270, 225, 180 | 1 |  |
| UGC 5ea9c39d | 2.099 | 0.011 | 2.405 | 270.063, 358.151, 358.151 | 1 |  |
| UGC 6dcf9eb3 | 2.06 | 0.433 | 2.462 | 270.112, 89.972, 90.028 | 1 |  |
| UGC 711dd18c | -1.076 | 2.188 | 2.711 | -0, 180, 270 | 1 |  |
| UGC 73411ffc | -3.793 | 4.069 | 2.002 | 270, 0, 0 | 1 |  |
| UGC 9605105d | 3.537 | 0.926 | -1.243 | 0.004, 1.894, 269.888 | 1 |  |
| UGC b831800b | 3.572 | 0.852 | -2.174 | 270, 180, 165.964 | 1 |  |
| UGC c0d3453a | 2.97 | -1.526 | 3.601 | 0, 0, 0 | 1 |  |
| UGC df54e284 | 3.324 | 0.871 | -1.401 | 0.057, 141.908, 180.096 | 1 |  |
| UGC df54e284 | 3.451 | 0.072 | 2.354 | 0.074, 3.57, 180.084 | 1 |  |
| UGC e052d969 | -2.512 | 0.015 | 2.076 | 270, 180, 180 | 1 |  |
| UGC ec1bff34 | 0.536 | 0.049 | -2.106 | 270, 168.041, 14.959 | 1 |  |
| UGC ec7eeded | 2.735 | 0.022 | 2.093 | 0.079, 0.079, 0.079 | 1 |  |

### 3.5 Comparison with our guessed layout (`rr_dorm_core.md` 3.3)

"2018" = DormRoom-2018 (2021 identical unless noted). Only the object *positions* are evidence; the archivist used
modern props (e.g. SANDBOX_MIRROR, the Apr 2021 Mirror prop) in every version, and the furniture that was part of
the official scene (bunk bed, dresser, board, hoop) exists only as unresolvable UGC geometry, so its exact pose is
not known.

| object | our guess (3.3) | archive evidence | verdict |
| --- | --- | --- | --- |
| **exit door** | front wall (2.90, 0, 2.93), 180 | 2021 ROOM_DOOR (-4.821, -0.01, -0.39) ry 90.6; 2019 ROOM_DOOR_V2 (-4.775, 0.02, -0.425) ry 89.5; 2018 has UGC 13ad3ac2 at (-4.794, -0.02, -0.46) ry 90 (GUESS the door frame); UnknownDate ROOM_DOOR (-4.04, 0.03, -0.51) ry 90 | **move the door to the left wall under the loft, centre z ~-0.43, facing +x** (loft underside 2.64 leaves room for the 2.14 m frame) |
| **mirror** | back wall (-0.40, 1.35, -2.92), 0 | SANDBOX_MIRROR (0.882, 1.339, 3.011) ry 180, scale 1.023 in 2018/2021; (0.889, 1.339, 3.014) ry 180 in 2019; UnknownDate (0.577, 1.403, 2.659) ry 180 | **front wall, under the arched window, facing -z, centre x ~0.88, y ~1.34** (all four versions agree) |
| art canvases | on the loft floor (-4.2, 3.52, -1.0) | 2018: ART_CANVAS (0.337, 2.034, 3.016) and (1.315, 2.058, 3.019) ry 181, scale 0.31-0.32 = two small canvases on the front wall above the mirror | use the archive: two small canvases above the mirror |
| **bunk bed + desk** | right wall (4.04, 0, 0.55), +90 | desk props on a surface at **y 0.85-0.88 along the back wall**: RADIO (1.381, 0.883, -2.529) ry 308, SOLOCUP (2.157, 0.853, -2.250) and (2.469, 0.853, -2.256), WATERBOTTLE (1.331, 0.978, -2.055), 2 DARTs stuck at (2.057, 1.031, -2.055) and (1.906, 0.954, -2.072); UGC c695cd3e at (2.002, 0.001, -2.140) ry 0 (GUESS the bed model, pivot on the floor). 2019: radio (1.533, 0.865, -2.608), cups (2.121/2.347, 0.849, -2.635/-2.539), bottle (2.175, 0.971, -2.271) | **bed against the back wall, desk top y 0.85 (= kit BunkBed desk top 0.85), centred x ~2.0, z ~-2.1, long side along x, rotation 0 or 180** (it must stop at the pillar, x 3.14). Our right-wall guess is wrong |
| radio | bed-local (0.55, 1.07, -0.25) | (1.381, 0.883, -2.529) ry 308 | use the archive value |
| **welcome mat / spawn** | (0.30, 0, 0.60) facing -z | WELCOME_MAT_V2 (-3.974, 0.044, -0.921) ry 75 (2018/2021) | spawn under the loft next to the door, facing roughly +x (into the room) |
| armchair | (-3.6, 0, 1.2) | SANDBOX_SEAT (-3.733, 0.347, 1.962) ry 132.6 scale 1.52 (2018/2021) | close; use the archive |
| coffee table | (none) | TABLE_SHORT (-4.39, 0.052, 0.585) ry 90 scale 0.815, with PING_PONG_PADDLE (-4.396, 0.424, 0.861) and PING_PONG_BALL (-4.337, 0.43, 0.382) on it | add under the loft |
| plants | fig (-4.4, 0, 2.3) | PLANT_FIG (-4.422, 0.017, 1.429) ry 129; PLANT_SNAKE (-4.511, 0.001, -2.427) ry 90 (2019: (-4.128, 0.017, -2.109)); PLANT_DRACAENA (-2.663, 0.017, 2.227) ry 147 | three plants, all around the loft end |
| loft art corner | canvas + brush on the loft | MOVEABLE_WHITEBOARD (-4.347, 3.696, -1.239) ry 91 on the loft's left wall; 10 MARKER_BLACK on the loft floor (y 2.768) and shelves (y 3.574); ERASER (-3.355, 2.756, -2.136); MARKER_ERASER (-4.119, 2.768, 0.499); DRAWABLE_CLIPBOARD (-3.757, 2.757, 1.31) | loft = whiteboard + markers corner |
| basketball | (3.2, 0.13, -0.6) | BASKETBALL (3.409, 0.136, -1.142) ry 270 (2018/2021) | consistent with the rim at (3.905, 1.097, -1.090): the ball rests 0.5 m in front of the pillar face |
| hoop / bin | hoop pivot (3.905, 1.41, -1.44) scale 0.94; trash can (3.905, 0, -1.10) | 2018: a 16-shape Maker Pen object at (3.394, 0.754, -1.359); 2021: a 109-shape object at (4.426, 1.365, -1.356) rot (0, 0, 180); UnknownDate UGC pieces at (3.537, 0.926, -1.243) and (3.324, 0.871, -1.401) | agrees with the data-backed rim spot on the pillar face; no official hoop pose in the data |
| bucket | (none; trash can under the rim) | BUCKET (-2.163, 0.013, -2.636) ry 306 (2019: (-1.737, 0.012, -2.473)) | a bucket near the back wall left of centre |
| dresser | (-4.70, 0, -1.40), +90 under the loft | no dresser object; UGC 7ba50577 (-4.004, 1.021, -0.918) and the door occupy that zone | **conflict**: the door is at z ~-0.4 on that wall, so the dresser cannot be at z -1.4 centred there (GUESS: front-right corner, where a jack-o-lantern (3.987, 1.241, 2.392) and a text sign (3.778, 0.888, 1.972) sit on surfaces of height ~1.24 and ~0.89) |
| board / posters | community board front wall (0, 2.25, 2.90) | front wall holds mirror + canvases; UGC 64d8ca74 on the back wall (-0.825, 1.544, -2.855) (2021) and two 397-shape Maker Pen objects on the back wall high up at (-1.837, 5.32, -2.845) and (-0.21, 5.371, -2.858) | GUESS: board on the back wall, not the front |
| game-role nodes | - | 3 GAME_ROLE_NODEs at (-5.34..-5.54, 1.21, 4.49) = blank-room default, outside the room | ignore |

**Which archive version matches the final dorm:** `DormRoom-2018` (and its copy `DormRoom2021`) = the Spring
Cleaning room with loft, mirror, desk-bed. `DormRoom2019` is the same room with fewer props; `DormRoom-UnknownDate`
is a different, older room. None contains the 2023+ changes (Room-i-verse door, Play board): the door objects point
to rooms by name only (2021 ROOM_DOOR has no target name; UnknownDate targets "LockerRoom-2017").

## 4. Rec Center versions (blank-room reconstructions by 1257174)

| version | views | shapes | notes |
| --- | --- | --- | --- |
| RecCenter2018 (vol.1) | 3 | 0 | **empty** (only the 3 default role nodes, moved to (10.3-10.7, 1.26, -0.38)); geometry never saved |
| RecCenter-2020 (vol.1) | 66 | 737 | own layout; pool table, ping-pong table, coffee pots, 3 basketballs, cones, bells, confetti guns, cue rack, 2 welcome mats, "Featured Rooms / Rec Room" sign |
| RecCenter2021 (vol.1) | 115 | 0 | **copy of 2019_Rec_Center** (same coordinates for ~30 objects) + 69 SANDBOX_SEATs (couches/bleachers seats) + a ROOM_DOOR with a "DormRoom" label |
| 2019_Rec_Center (room 6436648938726713495) | 51 | 0 | labels "Dorm Room", "Quest", "PVP", "Magic Door", "Featured Rooms", "Give It You're Rec Room Best" |
| AppleSausesRecCenter "Reccenter" | 192 | 1398 | another rebuild (17 CHAIRs, 10 CAFE_PLATEs, 26 texts, glass); not tabulated |

The 2019/2020/2021 builds share one frame (signs and props within 0.1 m of each other): **Dorm door east at
x ~17.9, z ~-2.7 (sign y 2.56)**, Quest door north around (-2.9, 2.1, 19.4), PVP door west around (-32.8, 3.0,
-9.5), Featured Rooms board south around (4.7, 6.1, -26.8), Magic Door sign (-12.8, 5.9, -15.7), "Give It Your Rec
Room Best!" banner (-1.9, 5.76, 9.0), café counter around (0, 1.2-1.5, -20..-21), mirror (-8.6, 1.0, 15.0) facing +x,
ping-pong table (-11.49, 0.13, -2.26), pool table (-17.82, 1.68, -17.26) with cue rack (-16.17, 1.89, -15.25),
basketballs around (-21..-27, 0.46, -12..-19), welcome mat (12.90, 0.12, -2.70) facing -x (2019/2021) or
(14.53, -0.13, -4.48 / -0.84) (2020). This is the archivist's frame; nothing ties it to the official Rec Center
scene (GUESS it copies the real proportions: a ~50 x 46 m hall with the Dorm door opposite the PVP door). Objects at
(+-4096, -1.9e6, +-4096) are props that fell out of the world; ignore them.

### 4.1 RecCenter-2020
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| BASKETBALL | -26.811 | 0.461 | -13.312 | 0, 91.886, 0 | 1 |  |
| BASKETBALL | -21.407 | 0.461 | -19.161 | 0, 300.224, 0 | 1 |  |
| BASKETBALL | 2.004 | 0.214 | 2.094 | 0, 131.898, 90.003 | 1 |  |
| BELL | -5.859 | 1.299 | -5.024 | 0, 171.206, 0 | 0.721 |  |
| BELL | -4.536 | 1.3 | -1.098 | 0, 209.637, 0 | 0.587 |  |
| BUCKET | -29.896 | 0.337 | -18.899 | 0, 316.51, 0 | 1 |  |
| BUCKET | -1.806 | 0.09 | -15.537 | 0, 294.006, 0 | 1 |  |
| BUCKET | 4.818 | 0.882 | -7.052 | 0, 181.669, 0 | 1 |  |
| CAFE_PLATE | -1.561 | 0.884 | -19.448 | 0, 286.156, 0 | 1 |  |
| COFFEE_POT | -0.336 | 1.182 | -20.124 | 0, 326.629, 0 | 1 |  |
| COFFEE_POT | -0.323 | 1.182 | -19.735 | 0, 223.732, 0 | 1 |  |
| CONE | -7.976 | 0.955 | -2.445 | 0, 99.543, 0 | 1 |  |
| CONE | -7.937 | 0.955 | -2.917 | 0, 82.849, 0 | 1 |  |
| CONE | -7.845 | 0.955 | -3.342 | 0, 65, 0 | 1 |  |
| CONFETTI_GUN | -5.383 | 1.077 | -2.593 | 11.515, 0.654, 0.014 | 1 |  |
| CONFETTI_GUN | -5.366 | 1.077 | -3.013 | 11.514, 355.473, 0.018 | 1 |  |
| DISCGOLF_DISC | 4.707 | 0.896 | -7.888 | 0, 289.754, 0 | 1 |  |
| DISCGOLF_DISC | 4.736 | 0.896 | -7.484 | 0, 278.479, 0 | 1 |  |
| GAME_ROLE_NODE | -5.538 | -0.133 | 4.493 | 0, 0, 0 | 1 | role_name=Everyone / role_guid=c48bac02-a743-4797-94c4-e2d6f... |
| GAME_ROLE_NODE | -5.138 | -0.055 | 4.493 | 0, 0, 0 | 1 | role_name=Eliminated / role_guid=0b9ec39e-2cc9-4825-8321-da1... |
| GAME_ROLE_NODE | -5.033 | 0.506 | -4.67 | 0.414, 336.488, 151.272 | 1 | role_name=In-Game / role_guid=2c721342-c7ed-49fe-b8f0-93f8d2... |
| INTERACTION_VOLUME | -2.896 | 0.37 | 19.289 | 0, 180, 0 | 0.04 | interaction_string=Use |
| INTERACTION_VOLUME | 17.94 | 1.133 | -2.663 | -0, 270, 0 | 0.04 | interaction_string=Use |
| INVISIBLE_COLLISION | -18.85 | 0.926 | -11 | 0, 0, 0 | 0.05 |  |
| INVISIBLE_COLLISION_V2 | -31.317 | 0.596 | -5.425 | 0, 270.881, 90 | 0.001 |  |
| INVISIBLE_COLLISION_V2 | -31.296 | 0.856 | -5.175 | 5, 270.881, 140 | 0.001 |  |
| LAUNDRY_BIN | 3.714 | 0.088 | -9.521 | 0, 323.233, 0 | 1 |  |
| PING_PONG_BALL | -11.912 | 0.908 | -3.133 | 359.719, 189.309, 0 | 1 |  |
| PING_PONG_PADDLE | -11.923 | 0.903 | -1.141 | 0, 320.546, 0 | 1 |  |
| PING_PONG_PADDLE | -11.172 | 0.903 | -3.349 | 0, 133.822, 0 | 1 |  |
| PLAYER_CREATED_SIGN | 4.685 | 6.052 | -26.823 | 0, 270, 0 | 1.52 | messages=Featured Rooms / messages=Rec Room |
| POOL_CUE_RACK | -16.173 | 1.894 | -15.248 | 0, 180, 0 | 1 |  |
| POOL_TABLE | -17.821 | 1.676 | -17.262 | -0, 359.286, 0 | 1 |  |
| POOL_TRIANGLE | -17.212 | 2.378 | -17.239 | 0, 115.347, 0 | 1 |  |
| SANDBOX_COC_POSTER | 10.838 | 1.419 | -4.998 | 0, 0, 0 | 1 |  |
| SANDBOX_MIRROR | -8.577 | 0.905 | 14.824 | 0, 90, 270 | 2.055 | pal11 |
| SANDBOX_SEAT | -7.836 | -0.309 | 12.354 | 0, 93.098, 0 | 1 |  |
| SANDBOX_SEAT | -6.511 | -0.309 | 11.319 | 0, 0.585, 0 | 1 |  |
| SANDBOX_SEAT | 0.869 | -0.321 | 16.009 | 0, 271.095, 0 | 1 |  |
| SANDBOX_TEXT_V3 | -9.284 | 0.715 | 9.384 | 0, 180, 0 | 1 | message=00:00; pal13 |
| SANDBOX_TEXT_V3 | -1.964 | 5.789 | 9.077 | 0, 180, 0 | 5.912 | message=Give It Your Rec Room Best! ; pal19 |
| SFX_NODE_V2 | -31.4 | 1.395 | -5.148 | 0, 355.881, 0 | 1 |  |
| SFX_NODE_V2 | -30.912 | 1.615 | -5.517 | 0, 0.881, 0 | 1 |  |
| SHAPE_CONTAINER | -8.541 | -0.388 | 13.014 | 270, 90, 180 | 1 | 292 shapes |
| SHAPE_CONTAINER | -8.541 | 2.879 | 13.014 | 270, 90, 180 | 1 | 292 shapes |
| SHAPE_CONTAINER | -8.541 | -0.388 | 16.982 | 270, 90, 180 | 1 | 149 shapes |
| SHAPE_CONTAINER | -8.478 | -0.39 | 13.03 | 270, 270, 270 | 0.998 | 4 shapes |
| TABLE_PINGPONG | -11.487 | 0.127 | -2.256 | 0, 180.303, 0 | 1 |  |
| TEXTURED_QUAD | 18.107 | 5.798 | -5.868 | 0, 270, 0 | 3.339 |  |
| TRIGGER_VOLUME | -13.537 | 85.082 | -77.926 | 0, 70.29, 0 | 0.04 |  |
| TRIGGER_VOLUME_CV2 | -30.916 | 1.593 | -5.467 | 0, 180.881, 0 | 0.005 |  |
| TRIGGER_VOLUME_CV2 | -12.717 | 85.18 | -81.83 | 0, 93.534, 0 | 0.04 |  |
| VARIABLE_NODE | -12.027 | 2.738 | 8.928 | 0, 270, 357.663 | 1 |  |
| VECTOR_GADGET | -31.379 | 0.943 | -5.254 | 274.993, 34.69, 51.633 | 1 |  |
| VECTOR_GADGET | 6.953 | 7.441 | -10.484 | 18.052, 318.534, 1.034 | 1 |  |
| WATERBOTTLE | -31.336 | 0.727 | -5.459 | 304.256, 345.756, 91.429 | 1 |  |
| WELCOME_MAT_V2 | 14.527 | -0.125 | -4.479 | -0, 269.329, 0 | 1 | welcome_mat_name=Welcome Mat |
| WELCOME_MAT_V2 | 14.527 | -0.125 | -0.838 | -0, 269.329, 0 | 1 | welcome_mat_name=Welcome Mat |
| UGC 66625ee0 | 4.699 | 3.189 | -26.814 | 0, 0, 0 | 1 |  |
| UGC 7d5cc1ea | -26.29 | 0.324 | -4.911 | 270, 275.102, 119.745 | 1 |  |
| UGC 7e2338df | -32.822 | 0.346 | -9.074 | 0, 90, 0 | 1 |  |
| UGC 7e2338df | -23.435 | 0.088 | 3.56 | 0, 116.087, 0 | 0.993 |  |
| UGC 7e2338df | -13.26 | 3.393 | -15.711 | 0, 0, 0 | 0.953 |  |
| UGC 7e2338df | -2.438 | -0.696 | 19.424 | 0, 180, 0 | 1 |  |
| UGC b802e662 | 17.957 | 0.123 | -2.67 | -0, 270, 0 | 1 |  |
| UGC f6cf1ef9 | -10.06 | 1.098 | 7.255 | 0, 0.311, 0 | 1 |  |

### 4.2 RecCenter2021
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| AUDIO_PLAYER | 7.902 | 1.03 | 2.033 | 4.997, 247.175, 2.008 | 1 |  |
| BASKETBALL | -26.635 | 0.461 | -14.766 | 0, 0, 0 | 1 |  |
| BASKETBALL | -20.702 | 0.461 | -12.136 | 0, 0, 0 | 1 |  |
| BASKETBALL | -4.457 | 0.213 | -4.885 | 316.793, 140.109, 278.208 | 1 |  |
| CANDLE | 7.591 | 3.642 | -21.263 | 0.22, 3.177, 0.896 | 1 |  |
| CANDLE | 7.718 | 3.641 | -21.343 | 0.014, 359.698, 359.95 | 1.265 |  |
| COFFEE_MUG | -0.042 | 1.472 | -21.308 | 0, 182.456, 0 | 1 |  |
| COFFEE_MUG | -0.041 | 1.472 | -20.858 | 0, 182.348, 0 | 1 |  |
| COFFEE_POT | -0.275 | 1.182 | -20.086 | 0.001, 345.448, 0.005 | 1 |  |
| CONE | -4096 | -1989715.375 | -4094.369 | 312.683, 36.069, 226.549 | 1 |  |
| CONE | -7.854 | 0.955 | -3.634 | 0, 75.028, 0 | 1 |  |
| CONE | -7.761 | 0.955 | -2.645 | 0, 128.718, 0 | 1 |  |
| CONE | 4096 | -1981523 | -4096 | 284.115, 273.634, 348.207 | 1 |  |
| DISCGOLF_DISC | -9.405 | 1.29 | -5.011 | 0, 218, 0 | 1 |  |
| DISCGOLF_DISC | -8.919 | 1.29 | -5.015 | 0, 219.007, 0 | 1 |  |
| GAME_ROLE_NODE | 10.345 | 1.255 | -0.384 | 0, 0, 0 | 1 | role_name=Everyone / role_guid=c48bac02-a743-4797-94c4-e2d6f... |
| GAME_ROLE_NODE | 10.546 | 1.255 | -0.384 | 0, 0, 0 | 1 | role_name=In-Game / role_guid=2c721342-c7ed-49fe-b8f0-93f8d2... |
| GAME_ROLE_NODE | 10.745 | 1.255 | -0.384 | 0, 0, 0 | 1 | role_name=Eliminated / role_guid=0b9ec39e-2cc9-4825-8321-da1... |
| INTERACTION_VOLUME | -32.665 | 1.413 | -9.499 | 0, 0, 0 | 0.04 | interaction_string=Use |
| INTERACTION_VOLUME | -2.833 | 0.235 | 19.405 | 359.921, 179.921, 0.079 | 0.04 | interaction_string=Use |
| INTERACTION_VOLUME | 4.751 | 4.251 | -26.877 | 0, 0, 0 | 0.1 | interaction_string=Use |
| INVISIBLE_COLLISION_V2 | -32.425 | 3.83 | -4.761 | 0, 180, 0 | 0.005 |  |
| RADIO | -4096 | -1989715.25 | -4096 | 359.157, 117.642, 359.677 | 1 |  |
| RADIO | -8.306 | 1.292 | -4.724 | 0, 72.848, 0 | 1 |  |
| ROOM_DOOR | 17.973 | -0.029 | -3.184 | 0, 269.774, 0 | 1.154 | target_type=Room / invite_mode=PartyAutoFollow |
| SANDBOX_SEAT | -24.308 | 0.534 | -1.671 | 0, 99.684, 0.112 | 1 |  |
| SANDBOX_SEAT | -24.163 | 0.534 | -0.827 | 0, 99.684, 0.112 | 1 |  |
| SANDBOX_SEAT | -24.04 | 0.534 | -0.104 | 0, 99.684, 0.112 | 1 |  |
| SANDBOX_SEAT | -23.835 | 0.563 | -3.251 | 0.04, 47.875, 0.105 | 1 |  |
| SANDBOX_SEAT | -22.05 | 0.556 | -3.38 | 0.095, 334.686, 0.06 | 1 |  |
| SANDBOX_SEAT | -21.843 | 0.565 | -0.206 | 359.903, 209.882, 0.056 | 1 |  |
| SANDBOX_SEAT | -19.925 | 0.581 | 6.433 | 0.041, 47.356, 0.104 | 1 |  |
| SANDBOX_SEAT | -19.396 | 0.581 | 5.86 | 0.041, 47.356, 0.104 | 1 |  |
| SANDBOX_SEAT | -18.88 | 0.581 | 5.298 | 0.041, 47.356, 0.104 | 1 |  |
| SANDBOX_SEAT | -17.36 | 0.634 | 8.163 | 359.898, 222.215, 0.045 | 1 |  |
| SANDBOX_SEAT | -16.448 | 0.636 | 6.425 | 0.111, 282.759, 0.012 | 1 |  |
| SANDBOX_SEAT | -15.833 | 0.593 | -1.714 | 0, 101.741, 0.111 | 1 |  |
| SANDBOX_SEAT | -14.22 | 0.497 | -2.999 | 0.091, 340.5, 0.065 | 1 |  |
| SANDBOX_SEAT | -13.935 | 0.583 | -0.815 | 359.905, 205.801, 0.06 | 1 |  |
| SANDBOX_SEAT | -10.833 | 0.554 | -19.358 | 359.945, 149.298, 0.097 | 1 |  |
| SANDBOX_SEAT | -10.601 | 0.565 | -21.276 | 0.066, 18.215, 0.091 | 1 |  |
| SANDBOX_SEAT | -9.767 | 0.546 | -22.841 | 0, 112.953, 0.11 | 1 |  |
| SANDBOX_SEAT | -9.726 | 0.538 | -19.448 | 359.901, 213.174, 0.053 | 1 |  |
| SANDBOX_SEAT | -9.493 | 0.558 | -20.897 | 0.108, 302.036, 0.031 | 1 |  |
| SANDBOX_SEAT | -8.647 | 0.546 | -22.226 | 359.912, 192.938, 0.07 | 1 |  |
| SANDBOX_SEAT | -8.569 | 0.532 | -24.249 | 0.093, 338.569, 0.063 | 1 |  |
| SANDBOX_SEAT | -8.196 | 0.546 | -19.536 | 359.891, 61.435, 0.028 | 0.935 |  |
| SANDBOX_SEAT | -7.86 | 0.55 | -23.44 | 359.888, 279.061, 0 | 1 |  |
| SANDBOX_SEAT | -7.784 | 0.542 | -18.185 | 359.946, 147.322, 0.098 | 0.935 |  |
| SANDBOX_SEAT | -7.733 | -0.177 | 12.325 | 0, 90, 0.112 | 1 |  |
| SANDBOX_SEAT | -6.833 | 0.54 | -25.202 | 359.889, 106.561, 0 | 1 |  |
| SANDBOX_SEAT | -6.808 | 0.546 | -20.003 | 359.944, 329.79, 0.097 | 1 |  |
| SANDBOX_SEAT | -6.528 | 0.534 | -21.521 | 0, 112.953, 0.11 | 1 |  |
| SANDBOX_SEAT | -6.472 | -0.194 | 11.368 | 0, 0, 0 | 1 |  |
| SANDBOX_SEAT | -6.405 | 0.534 | -18.495 | 359.892, 239.783, 0.029 | 1 |  |
| SANDBOX_SEAT | -6.112 | 0.53 | -22.89 | 0.056, 29.718, 0.097 | 1 |  |
| SANDBOX_SEAT | -6.056 | 0.532 | -24.345 | 359.938, 157.6, 0.093 | 1 |  |
| SANDBOX_SEAT | -5.917 | 0.782 | -0.485 | 359.94, 154.524, 0.095 | 0.909 |  |
| SANDBOX_SEAT | -5.222 | 0.534 | -21.044 | 359.905, 206.452, 0.059 | 1 |  |
| SANDBOX_SEAT | -4.903 | 0.53 | -25.89 | 0.11, 289.498, 0.019 | 1 |  |
| SANDBOX_SEAT | -4.815 | 0.534 | -24.776 | 359.889, 251.866, 0.018 | 1 |  |
| SANDBOX_SEAT | -4.702 | 0.542 | -22.481 | 0, 295.781, 0.109 | 1 |  |
| SANDBOX_SEAT | -0.845 | 3.653 | -21.022 | 0.005, 84.528, 0.112 | 1 |  |
| SANDBOX_SEAT | -0.772 | 3.653 | -21.784 | 0.005, 84.528, 0.112 | 1 |  |
| SANDBOX_SEAT | -0.698 | 3.653 | -22.575 | 0.005, 84.528, 0.112 | 1 |  |
| SANDBOX_SEAT | 0.011 | 3.726 | -19.054 | 359.946, 147.157, 0.098 | 1 |  |
| SANDBOX_SEAT | 0.53 | 3.618 | -23.95 | 0, 0, 0 | 1 |  |
| SANDBOX_SEAT | 0.737 | -0.21 | 12.562 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 0.761 | -0.204 | 13.323 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 0.78 | -0.214 | 14.134 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 0.825 | -0.212 | 16.06 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 0.831 | -0.196 | 16.864 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 0.833 | -0.202 | 17.604 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 1.319 | 3.618 | -23.946 | 0, 0, 0 | 1 |  |
| SANDBOX_SEAT | 1.651 | 3.714 | -18.796 | 359.915, 189.277, 0.073 | 1 |  |
| SANDBOX_SEAT | 2.065 | 3.604 | -23.929 | 0, 0, 0 | 1 |  |
| SANDBOX_SEAT | 7.667 | 3.661 | -18.294 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 7.677 | 3.661 | -19.22 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 7.683 | 3.661 | -19.938 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 7.712 | 3.661 | -22.841 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 7.72 | 3.661 | -23.667 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 7.726 | 3.661 | -24.403 | 359.888, 269.553, 0 | 1 |  |
| SANDBOX_SEAT | 12.587 | 3.655 | -7.218 | 2.931, 87.845, 0.954 | 0.909 |  |
| SANDBOX_SEAT | 12.587 | 3.655 | -2.573 | 2.931, 87.845, 0.954 | 0.909 |  |
| SANDBOX_SEAT | 12.683 | 3.665 | -3.28 | 2.991, 93.302, 1.207 | 0.909 |  |
| SANDBOX_SEAT | 12.683 | 3.665 | -7.925 | 2.991, 93.302, 1.207 | 0.909 |  |
| SANDBOX_SEAT | 12.687 | 3.679 | -4.069 | 2.991, 93.302, 1.207 | 0.909 |  |
| SANDBOX_SEAT | 12.687 | 3.679 | -8.714 | 2.991, 93.302, 1.207 | 0.909 |  |
| SANDBOX_SEAT | 13.542 | 3.663 | -0.935 | 358.67, 135.55, 1.142 | 0.909 |  |
| SANDBOX_SEAT | 13.589 | 3.692 | -10.638 | 359.75, 42.215, 0 | 0.909 |  |
| SANDBOX_SEAT | 14.146 | 3.663 | -0.401 | 358.67, 135.55, 1.142 | 0.909 |  |
| SANDBOX_SEAT | 14.161 | 3.694 | -11.065 | 0.04, 47.702, 0.104 | 0.909 |  |
| SANDBOX_SEAT | 14.681 | 3.663 | 0.054 | 358.67, 135.55, 1.142 | 0.909 |  |
| SANDBOX_SEAT | 14.728 | 3.694 | -11.614 | 0.04, 47.702, 0.104 | 0.909 |  |
| SANDBOX_TEXT_V3 | 17.908 | 2.563 | -2.717 | 0, 269.632, 0 | 2.145 | message=DormRoom; pal19 |
| SOLOCUP | -4.653 | -0.707 | 8.233 | 274.139, 102.129, 359.2 | 1 |  |
| TRIGGER_VOLUME_CV2 | 7.684 | 0.91 | 2.01 | 0, 270, 0 | 0.005 |  |
| WATER_JUG | -7.553 | 1.277 | -4.78 | 0.03, 326.884, 1.859 | 1 |  |
| WELCOME_MAT_V2 | 12.899 | 0.122 | -2.7 | 0.112, 270, 0 | 1 | welcome_mat_name=Welcome Mat |
| UGC 024c51ad | -7.264 | -2.91 | -19.078 | 0, 34.092, 0 | 1 |  |
| UGC 23e21c74 | 7.895 | 1.046 | 2.21 | 0, 180, 0 | 0.123 |  |
| UGC 2c0f5650 | -9.353 | 0.588 | 7.863 | 7.496, 359.506, 357.432 | 0.861 |  |
| UGC 3487905d | -2.386 | -0.694 | 19.437 | 359.921, 179.921, 0.079 | 1 |  |
| UGC 3846e7e2 | -32.821 | 0.351 | -9.069 | 0, 90, 0.112 | 1 |  |
| UGC 3846e7e2 | -23.558 | 0.067 | 3.424 | 359.949, 116.832, 0.1 | 1 |  |
| UGC 3846e7e2 | -13.263 | 3.417 | -15.745 | 0, 0, 0.112 | 1 |  |
| UGC 3c17658a | -32.765 | 2.432 | -9.502 | 0, 270, 359.888 | 0.004 |  |
| UGC 3c17658a | 4.708 | 5.571 | -26.83 | 359.004, 180, 0 | 0.004 |  |
| UGC 3f007411 | -10.195 | 0.79 | 15.03 | 0.112, 90, 0 | 1.218 |  |
| UGC 735d40bc | -7.81 | -2.888 | -18.144 | 0, 147.281, 0 | 1 |  |
| UGC 86974492 | -26.402 | 0.268 | -4.931 | 270, 0, 0 | 1.088 |  |
| UGC a74d039b | -1.11 | -1.7 | -20.004 | 0, 0, 0 | 1 |  |
| UGC b7534248 | -24.18 | -2.374 | -0.917 | 0, 189.36, 0 | 1 |  |
| UGC c573242c | -13.926 | -2.653 | -0.734 | 0, 294.364, 0 | 1 |  |
| UGC ebbccb18 | 4.728 | 3.2 | -26.849 | 0, 0, 0 | 1 |  |

### 4.3 2019_Rec_Center
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| AUDIO_PLAYER | 7.902 | 1.03 | 2.033 | 4.997, 247.175, 2.008 | 1 |  |
| BASKETBALL | -26.635 | 0.461 | -14.766 | 0, 0, 0 | 1 |  |
| BASKETBALL | -20.702 | 0.461 | -12.136 | 0, 0, 0 | 1 |  |
| BASKETBALL | 2.741 | 0.214 | 3.115 | 341.007, 138.944, 159.878 | 1 |  |
| CANDLE | 7.589 | 3.642 | -21.262 | 0.024, 3.479, 0.343 | 1 |  |
| CANDLE | 7.718 | 3.641 | -21.343 | 0.036, 359.873, 0 | 1.265 |  |
| COFFEE_MUG | -0.042 | 1.472 | -21.308 | 0, 182.456, 0 | 1 |  |
| COFFEE_MUG | -0.041 | 1.472 | -20.858 | 0, 182.348, 0 | 1 |  |
| COFFEE_POT | -0.275 | 1.182 | -20.086 | 0, 345.45, 0.001 | 1 |  |
| CONE | -4096 | -1963783.75 | -4094.369 | 312.683, 36.07, 226.549 | 1 |  |
| CONE | -7.855 | 0.955 | -3.634 | 0, 75.029, 0 | 1 |  |
| CONE | -7.761 | 0.955 | -2.645 | 0, 128.718, 0 | 1 |  |
| CONE | 4096 | -1955591.625 | -4096 | 284.115, 273.634, 348.207 | 1 |  |
| DISCGOLF_DISC | -9.405 | 1.29 | -5.011 | 0, 217.999, 0 | 1 |  |
| DISCGOLF_DISC | -8.919 | 1.29 | -5.015 | 0, 219.007, 0 | 1 |  |
| GAME_ROLE_NODE | 10.345 | 1.255 | -0.384 | 0, 0, 0 | 1 | role_name=Everyone / role_guid=c48bac02-a743-4797-94c4-e2d6f... |
| GAME_ROLE_NODE | 10.546 | 1.255 | -0.384 | 0, 0, 0 | 1 | role_name=In-Game / role_guid=2c721342-c7ed-49fe-b8f0-93f8d2... |
| GAME_ROLE_NODE | 10.745 | 1.255 | -0.384 | 0, 0, 0 | 1 | role_name=Eliminated / role_guid=0b9ec39e-2cc9-4825-8321-da1... |
| LAUNDRY_BIN | 3.042 | 0.088 | -9.287 | 0.001, 221.606, 0 | 1.045 |  |
| RADIO | -4096 | -1963783.625 | -4096 | 359.157, 117.642, 359.677 | 1 |  |
| RADIO | -8.306 | 1.292 | -4.724 | 0, 72.848, 0 | 1 |  |
| ROTATOR_GIZMO | -21.531 | -25.502 | -9.944 | 345, 127.821, 270 | 1 |  |
| SANDBOX_MIRROR | -8.596 | 1.03 | 15.069 | 0, 90, 270 | 3.415 | pal12 |
| SANDBOX_TEXT_2D | -32.79 | 2.961 | -9.51 | 0, 90, -0 | 3.113 | message=PVP; pal19 |
| SANDBOX_TEXT_2D | -12.835 | 5.908 | -15.677 | -0, 0, 0 | 2.592 | message=Magic Door; pal19 |
| SANDBOX_TEXT_2D | -2.866 | 2.085 | 19.436 | 0, 180, 0 | 3.113 | message=Quest; pal19 |
| SANDBOX_TEXT_2D | -1.9 | 5.758 | 9.017 | 0, 180, 0 | 6.049 | message=Give It You're Rec Room Best; pal19 |
| SANDBOX_TEXT_2D | 4.695 | 6.165 | -26.825 | -0, 0, 0 | 2.987 | message=Featured Rooms; pal19 |
| SANDBOX_TEXT_2D | 17.889 | 2.556 | -2.701 | -0, 270, 0 | 2.592 | message=Dorm Room; pal19 |
| SOLOCUP | -4.653 | -0.707 | 8.233 | 274.128, 102.15, 359.18 | 1 |  |
| TRIGGER_VOLUME_CV2 | 7.684 | 0.91 | 2.01 | 0, 270, 0 | 0.005 |  |
| WATER_JUG | -7.553 | 1.277 | -4.78 | 0.023, 326.885, 1.888 | 1 |  |
| WELCOME_MAT_V2 | 12.899 | 0.122 | -2.7 | 0.112, 270, 0 | 1 | welcome_mat_name=Welcome Mat |
| UGC 024c51ad | -7.264 | -2.91 | -19.078 | 0, 34.092, 0 | 1 |  |
| UGC 23e21c74 | 7.895 | 1.046 | 2.21 | 0, 180, 0 | 0.123 |  |
| UGC 3846e7e2 | -32.821 | 0.351 | -9.069 | 0, 90, 0.112 | 1 |  |
| UGC 3846e7e2 | -23.558 | 0.067 | 3.424 | 359.949, 116.832, 0.1 | 1 |  |
| UGC 3846e7e2 | -13.263 | 3.417 | -15.745 | 0, 0, 0.112 | 1 |  |
| UGC 3846e7e2 | -2.37 | -0.758 | 19.511 | 359.888, 180, 0 | 1 |  |
| UGC 3c17658a | -32.765 | 2.432 | -9.502 | 0, 270, 359.888 | 0.004 |  |
| UGC 3c17658a | -31.636 | 2.696 | -11.689 | 0, 270, 359.888 | 0.004 |  |
| UGC 3c17658a | 0.14 | 2.15 | -21.659 | 359.921, 89.921, 0.079 | 0.004 |  |
| UGC 3f007411 | -10.195 | 0.79 | 15.03 | 0.112, 90, 0 | 1.218 |  |
| UGC 735d40bc | -7.81 | -2.888 | -18.144 | 0, 147.281, 0 | 1 |  |
| UGC 86974492 | -26.402 | 0.268 | -4.931 | 270, 0, 0 | 1.088 |  |
| UGC a74d039b | -1.11 | -1.7 | -20.004 | 0, 0, 0 | 1 |  |
| UGC b7534248 | -24.18 | -2.374 | -0.917 | 0, 189.36, 0 | 1 |  |
| UGC c358e1f6 | 7.765 | 6.8 | -21.32 | 270.048, 180, 180 | 1 |  |
| UGC c573242c | -13.926 | -2.653 | -0.734 | 0, 294.364, 0 | 1 |  |
| UGC ebbccb18 | 4.728 | 3.2 | -26.849 | 0, 0, 0 | 1 |  |
| UGC efaf4451 | 17.957 | 0.112 | -2.634 | 359.888, 270, 0 | 1 |  |

### 4.4 RecCenter2018
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| GAME_ROLE_NODE | 12.746 | 1.176 | -0.422 | 0, 0, 0 | 1 | role_name=Everyone / role_guid=c48bac02-a743-4797-94c4-e2d6f... |
| GAME_ROLE_NODE | 13.014 | 1.176 | -0.422 | 0, 0, 0 | 1 | role_name=In-Game / role_guid=2c721342-c7ed-49fe-b8f0-93f8d2... |
| GAME_ROLE_NODE | 13.326 | 1.176 | -0.422 | 0, 0, 0 | 1 | role_name=Eliminated / role_guid=0b9ec39e-2cc9-4825-8321-da1... |

## 5. Paintball (paintballtroll1, creator 1944026861, on the official scenes)

Each map subroom holds the RRO Paintball gameplay layer (same set on every map, so GUESS official): 2
GAME_CONFIGURATIONs ("Paintball Capture The Flag" stats Hits/Outs/Flags, "Paintball Team Battle" stats Hits/Outs),
PAINTBALL_CAPTURE_THE_FLAG_ and PAINTBALL_TEAM_BATTLE_GAME_RULE_CIRCUIT, a GAME_ROLE_NODE named after the map whose
values list the allowed weapons (paintpistol, paintshotgun, paintscoperifle, paintburstgun, paintgrenade...),
10-22 PLAYER_ROLE_MAP_NODEs, SCOREBOARD_PROJECTOR + 2 LEADERBOARD_PROJECTORs ("Paintball Wins/Hits/Capture The Flag
Wins"), 2 FLAG_GOAL bases each with a DEPRECATED_FLAG_RED 1.28 m above it, and weapon pickups (ranged-weapon props
with settings). Player spawn points are not in the data (scene objects). The troll's additions are the Maker Pen
builds (SpongeBob etc., 74-2162 shapes per map), COSTUME_DUMMY_PLUS, SANDBOX_TEXT_V3/BUTTON/AUDIO_SAMPLER,
projectile launchers, holotars, PVP whips, extra doors (e.g. Quarry ROOM_DOORs to "DormRoom"/"Orientation").

Weapon-pickup symmetry test (a same-type twin at the point reflection through the midpoint of the two flag bases,
within 3 m): **Quarry 15 of 30, River 18 of 27** = official-looking layouts; Clearcut 2/29, Spillway 0/46, Drive-in
0/24, Merry Midway 0/25 = either mirror-symmetric maps or moved by the troll (Drive-in flag bases at y 18 look moved).
Homestead: no save. Lobby: 28 views (11 holotar projectors, cups, bottles, 2 mannequins, radio, 1 pistol).

Gameplay objects per map (flags, weapons, configs, standees, boards):

### 5.x Drivein
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| DEPRECATED_FLAG_RED | 13.986 | 19.547 | 28.126 | 0, 0, 0 | 1 |  |
| DEPRECATED_FLAG_RED | 19.906 | 19.288 | -69.706 | 0, 0, 0 | 1 |  |
| FLAG_GOAL | 13.986 | 18.264 | 28.126 | 0, 25.075, 0 | 1 |  |
| FLAG_GOAL | 19.906 | 18.005 | -69.706 | 0, 0, 0 | 1 |  |
| GAME_CONFIGURATION | -25.566 | 18.106 | -27.667 | 359.919, 59.874, 0.169 | 1 | name=Paintball Team Battle / stat_name=Hits / stat_name=Outs |
| GAME_CONFIGURATION | -25.174 | 18.107 | -29.04 | 359.919, 59.874, 0.169 | 1 | name=Paintball Capture The Flag / stat_name=Hits / stat_name... |
| LEADERBOARD_PROJECTOR | -30.1 | 16.502 | -47.45 | 0, 355.5, 0 | 0.5 | stat_full_name=Paintball Wins / stat_short_name=Wins / stat_... |
| PAINTBALL_CAPTURE_THE_FLAG_GAME_RULE_CIRCUIT | -25.004 | 18.12 | -29.695 | 359.838, 94.328, 0.093 | 1 |  |
| PAINTBALL_GRENADE | -13.672 | 17.819 | -31.234 | 0, 12.995, 359.864 | 1 |  |
| PAINTBALL_GRENADE | 15.989 | 19.327 | -39.047 | 0, 10.001, 0.018 | 1 |  |
| PAINTBALL_GRENADE | 16.243 | 18.908 | 17.051 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 18.463 | 17.649 | -49.306 | 0, 1.911, 0.001 | 1 |  |
| PAINTBALL_GRENADE | 24.127 | 18.026 | 4.826 | 0.275, 29.999, 0.452 | 1 |  |
| PAINTBALL_GRENADE | 25.505 | 19.518 | -26.855 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 29.19 | 19.321 | 0.16 | 0.06, 29.999, 0.027 | 1 |  |
| PAINTBALL_GRENADE | 31.543 | 19.294 | 32.403 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 47.79 | 17.35 | -19.127 | 0.026, 0.003, 0.106 | 1 |  |
| PAINTBALL_SHIELD | 23.974 | 19.28 | -59.302 | 270, 190.455, 87.961 | 1 |  |
| PAINTBALL_SHIELD | 27.285 | 19.646 | 40.528 | 282.823, 24.55, 337.145 | 1 |  |
| PAINTBALL_SHOTGUN | -15.146 | 17.79 | -47.634 | 0, 16.933, 269.853 | 1 |  |
| PAINTBALL_SHOTGUN | 5.529 | 18.987 | -54.239 | 356.833, 329.975, 85.251 | 1 |  |
| PAINTBALL_SHOTGUN | 11.538 | 19.247 | -41.705 | 0.001, 306.916, 269.995 | 1 |  |
| PAINTBALL_SHOTGUN | 15.303 | 18.872 | 18.234 | 0.002, 85, 270.028 | 1 |  |
| PAINTBALL_SHOTGUN | 18.31 | 19.561 | 36.37 | 0, 254.259, 269.989 | 1 |  |
| PAINTBALL_SHOTGUN | 30.177 | 19.14 | -62.91 | 0.079, 358.769, 273.344 | 1 |  |
| PAINTBALL_SHOTGUN | 30.62 | 19.246 | 3.195 | 0, 270.204, 89.994 | 1 |  |
| PAINTBALL_SHOTGUN | 47.873 | 17.307 | -9.716 | 0.046, 346.922, 90.08 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 3.483 | 24.842 | -15.852 | 307.139, 3.65, 358.974 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 5.785 | 22.937 | -67.612 | 290.547, 352.252, 102.16 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 21.771 | 23.346 | 32.385 | 282.673, 181.89, 267.784 | 1 |  |
| PAINTBALL_TEAM_BATTLE_GAME_RULE_CIRCUIT | -25.36 | 18.101 | -28.29 | 359.861, 82.208, 0.125 | 1 |  |
| PAINT_THROWER | -18.208 | 19.141 | -1.828 | 359.932, 176.489, 359.933 | 1 |  |
| PAINT_THROWER | 49.259 | 17.482 | -31.356 | 0.075, 283.421, 0.008 | 1 |  |
| SCOREBOARD_PROJECTOR | -27.711 | 16.502 | -37.361 | 0, 255, 0 | 0.5 |  |

### 5.x Quarry
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| DEPRECATED_FLAG_RED | -1.547 | 3.163 | 49.308 | 0, 0, 0 | 1 |  |
| DEPRECATED_FLAG_RED | 4.66 | 3.207 | -48.49 | 0, 0, 0 | 1 |  |
| FLAG_GOAL | -1.547 | 1.88 | 49.308 | 0, 90, 0 | 1 |  |
| FLAG_GOAL | 4.66 | 1.924 | -48.49 | -0, 270, 0 | 1 |  |
| GAME_CONFIGURATION | -36.664 | -0.643 | -21.654 | 0, 0, 0 | 1 | name=Paintball Capture The Flag / stat_name=Hits / stat_name... |
| GAME_CONFIGURATION | -36.009 | -0.643 | -23.887 | 0, 0, 0 | 1 | name=Paintball Team Battle / stat_name=Hits / stat_name=Outs |
| LEADERBOARD_PROJECTOR | -35.86 | -0.686 | -32.78 | -0, 319.108, 0 | 1 | stat_full_name=Paintball Wins / stat_short_name=Wins / stat_... |
| LEADERBOARD_PROJECTOR | 3.355 | -0.568 | -26.465 | 355.496, 231.661, 7.33 | 0.502 | stat_full_name=Minutes / stat_short_name=Minutes / stat_full... |
| PAINTBALL_CAPTURE_THE_FLAG_GAME_RULE_CIRCUIT | -36.487 | -0.349 | -21.378 | 0, 228.764, 0 | 1 |  |
| PAINTBALL_GRENADE | -3.916 | -1.81 | -15.821 | 0, 60, 0 | 1 |  |
| PAINTBALL_GRENADE | -3.746 | -1.81 | -16.115 | 0, 60.001, 0 | 1 |  |
| PAINTBALL_GRENADE | -3.551 | -1.81 | -16.453 | 0, 60.001, 0 | 1 |  |
| PAINTBALL_GRENADE | 6.498 | -1.81 | 16.455 | 0, 65.928, 0 | 1 |  |
| PAINTBALL_GRENADE | 6.674 | -1.81 | 16.061 | 0.001, 65.928, 0 | 1 |  |
| PAINTBALL_GRENADE | 6.945 | -1.81 | 16.435 | 0, 65.929, 0 | 1 |  |
| PAINTBALL_GRENADE | 26.958 | 2.447 | 12.025 | 0.001, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 27.232 | 2.447 | 12.465 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 27.564 | 2.447 | 12.025 | 0, 0, 0 | 1 |  |
| PAINTBALL_PISTOL | -40.259 | 0.197 | -33.171 | 0, 280.724, 89.99 | 1 |  |
| PAINTBALL_PISTOL | -39.835 | 0.197 | -33.058 | 0, 280.734, 89.99 | 1 |  |
| PAINTBALL_PISTOL | -39.435 | 0.197 | -32.954 | 0, 280.721, 89.995 | 1 |  |
| PAINTBALL_PISTOL | -39 | 0.197 | -32.78 | 0, 280.722, 89.99 | 1 |  |
| PAINTBALL_PISTOL | -20.018 | 2.978 | 10.496 | 0.001, 280.676, 89.994 | 1 |  |
| PAINTBALL_PISTOL | -14.622 | 2.113 | 53.458 | 0.001, 295.687, 89.99 | 1 |  |
| PAINTBALL_PISTOL | 24.982 | 2.986 | -12.527 | 0.001, 295.73, 89.989 | 1 |  |
| PAINTBALL_SHIELD | -20.688 | 1.88 | 7.882 | 270, 264.487, 95.513 | 1 |  |
| PAINTBALL_SHIELD | 1.983 | -5.299 | -2.774 | 276.136, 185.828, 173.954 | 1 |  |
| PAINTBALL_SHIELD | 23.55 | 1.88 | -7.755 | 270, 270.359, 89.641 | 1 |  |
| PAINTBALL_SHOTGUN | -24.337 | 1.606 | -11.33 | 0.005, 295.001, 269.795 | 1 |  |
| PAINTBALL_SHOTGUN | -2.328 | -4.164 | 16.15 | 0, 294.883, 269.996 | 1 |  |
| PAINTBALL_SHOTGUN | 5.893 | -4.164 | -17.941 | 0, 295, 269.997 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | -46.233 | -0.612 | -29.422 | 0.005, 182.01, 90.043 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | -22.06 | 6.105 | -20.67 | 7.018, 76.327, 359.801 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | -2.101 | 4.773 | -36.604 | 7.014, 27.791, 359.794 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 3.339 | 4.773 | 36.215 | 7.022, 91.978, 359.818 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 4.235 | 2.227 | 36.484 | 7.012, 194.873, 359.775 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 5.713 | 2.227 | -36.543 | 7.016, 359.823, 359.782 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 25.583 | 6.105 | 20.01 | 7.018, 252.451, 359.809 | 1 |  |
| PAINTBALL_TEAM_BATTLE_GAME_RULE_CIRCUIT | -35.582 | -0.341 | -23.847 | 0, 271.264, 0 | 1 |  |
| PAINT_THROWER | 6.44 | 0.12 | 12.697 | 0.009, 165.496, 179.996 | 1 |  |
| SCOREBOARD_PROJECTOR | -34.53 | -0.667 | -22.23 | 0, 244.687, 0 | 1 |  |
| STANDEE_FEMALE_01 | -41.528 | -0.687 | -32.643 | 0, 304.665, 0.004 | 1 |  |
| STANDEE_FEMALE_01 | -41.498 | -0.687 | -18.683 | 0, 169.62, 0 | 1 |  |
| STANDEE_FEMALE_01 | -20.867 | 5.977 | -20.823 | 0, 1.679, 0 | 1 |  |
| STANDEE_FEMALE_01 | -4.713 | 1.875 | -36.178 | 0, 307.73, 0 | 1 |  |
| STANDEE_FEMALE_01 | 26.09 | 5.977 | 19.373 | 0, 231.536, 0 | 1 |  |
| STANDEE_MALE_01 | -34.502 | -0.672 | -24.783 | 359.714, 269.997, 0.007 | 1 |  |
| STANDEE_MALE_01 | -27.631 | 1.799 | -23.192 | 0.368, 246.151, 0.007 | 1 |  |
| STANDEE_MALE_01 | -22.023 | -0.823 | -27.502 | 2.822, 270.192, 357.974 | 1 |  |

### 5.x MerryMidway
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| DEPRECATED_FLAG_RED | 13.449 | 2.227 | 88.482 | 0, 0, 0 | 1 |  |
| DEPRECATED_FLAG_RED | 97.822 | 2.277 | 24.914 | 0, 0, 0 | 1 |  |
| FLAG_GOAL | 13.449 | 0.944 | 88.482 | 0, 45, 0 | 1 |  |
| FLAG_GOAL | 97.822 | 0.994 | 24.914 | 0, 64.5, 0 | 1 |  |
| GAME_CONFIGURATION | 117.934 | 1.246 | 75.833 | 359.919, 59.874, 0.169 | 1 | name=Paintball Team Battle / stat_name=Hits / stat_name=Outs |
| GAME_CONFIGURATION | 118.326 | 1.247 | 74.46 | 359.919, 59.874, 0.169 | 1 | name=Paintball Capture The Flag / stat_name=Hits / stat_name... |
| LEADERBOARD_PROJECTOR | 122.01 | -0.151 | 74.75 | -0, 181.469, 0 | 0.75 | stat_full_name=Paintball Wins / stat_short_name=Wins / stat_... |
| PAINTBALL_CAPTURE_THE_FLAG_GAME_RULE_CIRCUIT | 118.496 | 1.26 | 73.805 | 359.838, 94.328, 0.093 | 1 |  |
| PAINTBALL_GRENADE | 13.537 | 2.021 | 71.308 | 0.001, 7.736, 0 | 1 |  |
| PAINTBALL_GRENADE | 18.511 | 9.252 | 80.474 | 0, 82.614, 0.006 | 1 |  |
| PAINTBALL_GRENADE | 46.265 | 0.811 | 58.519 | 0, 82.614, 0.006 | 1 |  |
| PAINTBALL_GRENADE | 48.504 | 0.718 | 48.817 | 0.011, 74.372, 0 | 1 |  |
| PAINTBALL_GRENADE | 73.315 | 0.66 | 57.051 | 0, 279.068, 0 | 1 |  |
| PAINTBALL_GRENADE | 76.994 | 2.001 | 17.622 | 0.011, 74.372, 0 | 1 |  |
| PAINTBALL_GRENADE | 82.277 | 2.073 | 47.696 | 358.64, 297.454, 356.212 | 1 |  |
| PAINTBALL_GRENADE | 88.199 | 1.974 | 20.108 | 0.116, 248.776, 0 | 1 |  |
| PAINTBALL_PISTOL | 39.873 | 2.137 | 93.172 | 11.513, 152.173, 0.017 | 1 |  |
| PAINTBALL_PISTOL | 42.534 | 0.668 | 58.056 | 11.509, 56.032, 0.009 | 1 |  |
| PAINTBALL_PISTOL | 61.611 | 1.915 | 19.698 | 11.509, 29.641, 0.009 | 1 |  |
| PAINTBALL_PISTOL | 73.162 | 0.735 | 57.403 | 11.52, 211.574, 0.063 | 1 |  |
| PAINTBALL_PISTOL | 75.336 | 0.663 | 73.53 | 11.509, 261.336, 0.009 | 1 |  |
| PAINTBALL_SHIELD | 35.935 | -0.202 | 18.865 | 336.779, 292.455, 181.87 | 1 |  |
| PAINTBALL_SHIELD | 53.183 | 2.212 | 89.582 | 336.779, 276.451, 181.87 | 1 |  |
| PAINTBALL_SHIELD | 66.916 | 1.198 | 15.348 | 341.565, 104.348, 3.488 | 1 |  |
| PAINTBALL_SHOTGUN | 19.799 | 1.925 | 89.864 | 0.001, 131.056, 270.014 | 1 |  |
| PAINTBALL_SHOTGUN | 32.226 | 0.42 | 41.078 | 0.001, 117.932, 270.014 | 1 |  |
| PAINTBALL_SHOTGUN | 36.646 | 0.497 | 18.423 | 0, 1.19, 269.981 | 1 |  |
| PAINTBALL_SHOTGUN | 53.436 | 1.95 | 90.24 | 0, 64.851, 269.981 | 1 |  |
| PAINTBALL_SHOTGUN | 66.349 | 2.013 | 15.803 | 0.001, 242.486, 270 | 1 |  |
| PAINTBALL_SHOTGUN | 66.567 | 1.89 | 74.914 | 5.852, 229.995, 6.436 | 1 |  |
| PAINTBALL_SHOTGUN | 91.872 | 1.953 | 8.093 | 0, 203.735, 269.498 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 21.394 | 8.555 | 82.926 | 280.657, 79.846, 82.077 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 85.003 | -0.114 | 88.077 | 278.972, 320.113, 244.882 | 1 |  |
| PAINTBALL_TEAM_BATTLE_GAME_RULE_CIRCUIT | 118.14 | 1.241 | 75.21 | 359.861, 82.208, 0.125 | 1 |  |
| SCOREBOARD_PROJECTOR | 115.502 | -0.155 | 70.273 | 0, 107.458, 0 | 0.75 |  |

### 5.x River
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| DEPRECATED_FLAG_RED | -0.041 | 2.67 | -49.732 | -0, 270, 0 | 1 |  |
| DEPRECATED_FLAG_RED | -0.04 | 2.67 | 49.888 | 0, 270, 0 | 1 |  |
| FLAG_GOAL | -0.042 | 0.774 | 49.889 | 0, 90, 0 | 1 |  |
| FLAG_GOAL | -0.041 | 0.774 | -49.732 | -0, 270, 0 | 1 |  |
| GAME_CONFIGURATION | -36.99 | -0.334 | -20.919 | 0, 0, 0 | 1 | name=Paintball Team Battle / stat_name=Hits / stat_name=Outs |
| GAME_CONFIGURATION | -35.606 | -0.337 | -21.269 | 0, 0, 0 | 1 | name=Paintball Capture The Flag / stat_name=Hits / stat_name... |
| LEADERBOARD_PROJECTOR | -41.62 | -0.667 | -32.96 | -0, 325.302, 0 | 1 | stat_full_name=Paintball Wins / stat_short_name=Wins / stat_... |
| LEADERBOARD_PROJECTOR | 12.525 | 0.098 | 36.706 | 0.027, 180, 0 | 1 | stat_full_name=Time / stat_short_name=Minutes / stat_full_na... |
| PAINTBALL_CAPTURE_THE_FLAG_GAME_RULE_CIRCUIT | -34.954 | -0.326 | -21.45 | 0, 34.454, 0 | 1 |  |
| PAINTBALL_GRENADE | -30.09 | 1.127 | -5.349 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | -8.027 | 0.902 | -29.514 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | -7.683 | 0.902 | -29.514 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | -7.294 | 0.902 | -29.514 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | -2.766 | 1.692 | 1.21 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | -2.343 | 1.692 | 1.21 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 6.753 | 0.306 | 29.42 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 6.953 | 0.306 | 29.819 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 7.185 | 0.306 | 29.42 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 29.622 | 0.958 | 3.847 | 0, 0, 0 | 1 |  |
| PAINTBALL_PISTOL | -39.906 | 0.197 | -33.644 | 0, 280.723, 89.992 | 1 |  |
| PAINTBALL_PISTOL | -39.47 | 0.197 | -33.497 | 0, 280.723, 89.992 | 1 |  |
| PAINTBALL_PISTOL | -39.045 | 0.197 | -33.384 | 0, 280.734, 89.995 | 1 |  |
| PAINTBALL_PISTOL | -38.645 | 0.197 | -33.28 | 0, 280.722, 89.999 | 1 |  |
| PAINTBALL_PISTOL | -30.512 | 1.1 | -5.788 | 0.001, 115.753, 89.997 | 1 |  |
| PAINTBALL_PISTOL | -24.036 | 1.643 | -59.319 | 0.002, 92.635, 89.986 | 1 |  |
| PAINTBALL_PISTOL | -13.887 | 3.731 | 15.189 | 0, 295.737, 89.995 | 1 |  |
| PAINTBALL_PISTOL | -10.135 | 1.585 | 52.206 | 0, 295.74, 89.996 | 1 |  |
| PAINTBALL_PISTOL | -1.226 | 3.215 | 1.274 | 0, 0, 269.998 | 1 |  |
| PAINTBALL_PISTOL | 9.852 | 2.335 | -51.911 | 0, 115.708, 89.997 | 1 |  |
| PAINTBALL_PISTOL | 13.392 | 2.019 | -15.826 | 0.002, 130.758, 89.995 | 1 |  |
| PAINTBALL_PISTOL | 26.283 | 1.643 | 62.032 | 0.002, 270.471, 89.991 | 1 |  |
| PAINTBALL_PISTOL | 30.057 | 0.931 | 3.844 | 0.001, 295.733, 89.997 | 1 |  |
| PAINTBALL_SHIELD | -12.47 | -0.611 | -6.238 | 270, 270.339, 89.661 | 1 |  |
| PAINTBALL_SHIELD | 12.5 | -0.611 | 4.322 | 270, 0, 0 | 1 |  |
| PAINTBALL_SHOTGUN | -24.337 | 0.154 | -15.837 | 0, 295, 269.989 | 1 |  |
| PAINTBALL_SHOTGUN | 25.064 | 0.153 | 13.865 | 0, 295, 269.992 | 1 |  |
| PAINTBALL_TEAM_BATTLE_GAME_RULE_CIRCUIT | -36.348 | -0.341 | -21.053 | 0, 22.333, 0 | 1 |  |
| SCOREBOARD_PROJECTOR | -34.53 | -0.667 | -22.23 | 0, 244.687, 0 | 1 |  |
| STANDEE_FEMALE_01 | -41.878 | -0.653 | -17.293 | 0, 168.365, 0 | 1 |  |
| STANDEE_FEMALE_01 | -39.11 | -0.656 | -30.359 | 0, 337.73, 0 | 1 |  |
| STANDEE_FEMALE_01 | -15.857 | 0.151 | -35.894 | 359.648, 308.398, 0.001 | 1 |  |
| STANDEE_FEMALE_01 | -2.959 | 1.633 | -1.129 | 0, 202.73, 0 | 1 |  |
| STANDEE_MALE_01 | -22.028 | 2.059 | -6.533 | 0, 180, 0 | 1 |  |
| STANDEE_MALE_01 | -22.021 | -0.462 | -28.788 | 0.62, 270.168, 352.634 | 1 |  |

### 5.x Clearcut
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| DEPRECATED_FLAG_RED | -8.037 | -2.69 | 31.348 | 0, 270, 0 | 1 |  |
| DEPRECATED_FLAG_RED | 16.1 | -2.73 | -35.2 | -0, 270, 0 | 1 |  |
| FLAG_GOAL | -8.035 | -4.59 | 31.348 | 0, 90, 0 | 1 |  |
| FLAG_GOAL | 16.1 | -4.63 | -35.2 | -0, 270, 0 | 1 |  |
| GAME_CONFIGURATION | -38.003 | -0.238 | 16.19 | 0, 0, 0 | 1 | name=Paintball Capture The Flag / stat_name=Hits / stat_name... |
| GAME_CONFIGURATION | -36.524 | -0.235 | 16.024 | 0, 0, 0 | 1 | name=Paintball Team Battle / stat_name=Hits / stat_name=Outs |
| LEADERBOARD_PROJECTOR | -35.938 | -0.584 | 2.594 | -0, 335.616, 0 | 1 | stat_full_name=Paintball Wins / stat_short_name=Wins / stat_... |
| PAINTBALL_ASSAULT_RIFLE | -25.803 | -0.247 | -35.574 | 0, 150, 90 | 1 |  |
| PAINTBALL_ASSAULT_RIFLE | -23.932 | 0.238 | 21.601 | 0, 144.385, 270 | 1 |  |
| PAINTBALL_ASSAULT_RIFLE | -7.137 | -4.233 | -5.81 | 0, 150, 0 | 1 |  |
| PAINTBALL_ASSAULT_RIFLE | 9.023 | -3.307 | -26.386 | 0, 150, 90 | 1 |  |
| PAINTBALL_ASSAULT_RIFLE | 40.855 | -3.654 | -28.899 | 0, 150, 90 | 1 |  |
| PAINTBALL_CAPTURE_THE_FLAG_GAME_RULE_CIRCUIT | -37.31 | -0.22 | 16.08 | 0, 19.443, -0 | 1 |  |
| PAINTBALL_GRENADE | -29.59 | 0.862 | -3.349 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | -14.696 | 0.458 | -5.004 | 0, 271.468, 0 | 1 |  |
| PAINTBALL_GRENADE | -14.687 | 0.458 | -4.655 | 0, 271.468, 0 | 1 |  |
| PAINTBALL_GRENADE | -14.677 | 0.458 | -4.265 | 0, 271.468, 0 | 1 |  |
| PAINTBALL_GRENADE | -3.021 | -0.717 | 1.21 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | -2.766 | -0.717 | 1.21 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 14.651 | 3.081 | 17.578 | 0, 177.316, 0 | 1 |  |
| PAINTBALL_GRENADE | 27.99 | -1.236 | 24.402 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 28.21 | -1.236 | 24.557 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 29.194 | 0.958 | 3.939 | 0, 0, 0 | 1 |  |
| PAINTBALL_PISTOL | -39.141 | 0.197 | 4.66 | 0, 280.723, 89.992 | 1 |  |
| PAINTBALL_PISTOL | -38.731 | 0.194 | 4.78 | 0.001, 280.734, 89.994 | 1 |  |
| PAINTBALL_PISTOL | -38.335 | 0.197 | 4.9 | 0, 280.722, 89.999 | 1 |  |
| PAINTBALL_PISTOL | -37.893 | 0.197 | 4.631 | 0, 280.722, 89.999 | 1 |  |
| PAINTBALL_PISTOL | -30.01 | 0.832 | -3.788 | 0.001, 115.753, 89.997 | 1 |  |
| PAINTBALL_PISTOL | 29.629 | 0.931 | 3.935 | 0.001, 295.733, 89.997 | 1 |  |
| PAINTBALL_SHIELD | -12.601 | -2.34 | 15.21 | 270, 0, 0 | 1 |  |
| PAINTBALL_SHIELD | -1.47 | 0.305 | 0.015 | 280.832, 359.688, 0.312 | 1 |  |
| PAINTBALL_SHOTGUN | -27.857 | 0.714 | 15.261 | 0.009, 74.999, 269.925 | 1 |  |
| PAINTBALL_SHOTGUN | 14.595 | -3.18 | -41.156 | 0, 0, 0 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | -18.109 | 2.312 | -3.701 | 0, 105, 0 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 6.621 | -0.187 | 46.844 | 0, 85, 270 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 15.735 | -0.783 | -23.225 | 5.961, 359.712, 359.079 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 24.507 | 2.162 | -0.357 | -0, 225, 0 | 1 |  |
| PAINTBALL_TEAM_BATTLE_GAME_RULE_CIRCUIT | -35.829 | -0.248 | 15.946 | 0, 19.631, 0 | 1 |  |
| SCOREBOARD_PROJECTOR | -34.53 | -0.583 | 15.8 | 0, 244.687, 0 | 1 |  |
| STANDEE_FEMALE_01 | -42.554 | -0.578 | 18.04 | 0, 172.826, 0 | 1 |  |
| STANDEE_FEMALE_01 | -34.357 | -0.656 | 8.817 | 0, 277.73, 0 | 1 |  |
| STANDEE_FEMALE_01 | -8.862 | -4.543 | -9.373 | 359.648, 308.398, 0.001 | 1 |  |
| STANDEE_MALE_01 | -40.004 | -0.566 | 2.215 | 0.029, 330.008, 0.005 | 1 |  |
| STANDEE_MALE_01 | -19.828 | 2.226 | -3.793 | 0, 255, 0 | 1 |  |
| STANDEE_MALE_01 | -7.728 | 0.307 | -34.793 | -0, 345.035, 0 | 1 |  |

### 5.x Spillway
| type | x | y | z | rot x,y,z (deg) | scale | notes |
| --- | --- | --- | --- | --- | --- | --- |
| DEPRECATED_FLAG_RED | 1.6 | 14.37 | -73.99 | 0, 0, 0 | 1 |  |
| DEPRECATED_FLAG_RED | 7.36 | 14.843 | 14.77 | 0, 0, 0 | 1 |  |
| FLAG_GOAL | 1.6 | 13.087 | -73.99 | -0, 270, 0 | 1 |  |
| FLAG_GOAL | 7.36 | 13.56 | 14.77 | 0, 90, 0 | 1 |  |
| GAME_CONFIGURATION | -22.45 | 12.439 | -24.56 | 0, 0, 0 | 1 | name=Paintball Capture The Flag / stat_name=Hits / stat_name... |
| GAME_CONFIGURATION | -20.89 | 12.439 | -25.04 | 0, 39.112, 0 | 1 | name=Paintball Team Battle / stat_name=Hits / stat_name=Outs |
| LEADERBOARD_PROJECTOR | -25.241 | 11.686 | -32.149 | 0.018, 180, 0 | 1 | stat_full_name=Time / stat_short_name=Minutes / stat_full_na... |
| LEADERBOARD_PROJECTOR | -21.911 | 11.674 | -36.446 | -0, 305.617, 0 | 1 | stat_full_name=Paintball Wins / stat_short_name=Wins / stat_... |
| PAINTBALL_CAPTURE_THE_FLAG_GAME_RULE_CIRCUIT | -21.723 | 12.34 | -24.668 | 0, 18.347, 0 | 1 |  |
| PAINTBALL_GRENADE | -11.777 | 13.113 | -18.56 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | -8.576 | 13.319 | -6.635 | 0, 356.467, 0 | 1 |  |
| PAINTBALL_GRENADE | -8.187 | 13.319 | -6.611 | 0, 356.467, 0 | 1 |  |
| PAINTBALL_GRENADE | -7.907 | 13.319 | -6.887 | 0.021, 356.463, 359.898 | 1 |  |
| PAINTBALL_GRENADE | 5.484 | 12.841 | -54.141 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 5.651 | 12.841 | -54.477 | 0.001, 229.311, 0 | 1 |  |
| PAINTBALL_GRENADE | 5.883 | 12.841 | -54.232 | 0, 320.13, 0 | 1 |  |
| PAINTBALL_GRENADE | 8.642 | 17.322 | -75.398 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 8.812 | 17.322 | -75.728 | 0, 229.311, 0 | 1 |  |
| PAINTBALL_GRENADE | 11.65 | 13.811 | 9.003 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 11.852 | 13.811 | 9.484 | 0, 2.944, 0 | 1 |  |
| PAINTBALL_GRENADE | 12.125 | 13.811 | 8.979 | 0.001, 348.564, 0 | 1 |  |
| PAINTBALL_GRENADE | 12.31 | 13.384 | -14.528 | 0, 356.468, 0 | 1 |  |
| PAINTBALL_GRENADE | 12.59 | 13.384 | -14.808 | 0, 356.468, 0 | 1 |  |
| PAINTBALL_GRENADE | 15.917 | 17.174 | 15.185 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 16.11 | 17.174 | 15.443 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 16.223 | 17.174 | 15.255 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 19.488 | 17.149 | -17.97 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 26.86 | 17.182 | -41.21 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE | 31.194 | 14.172 | 7.94 | 0, 0, 0 | 1 |  |
| PAINTBALL_GRENADE_LAUNCHER | 0.458 | 17.744 | -14.055 | 2.375, 69.025, 314.998 | 1 |  |
| PAINTBALL_GRENADE_LAUNCHER | 7.277 | 16.738 | -51.932 | 2.376, 15.093, 44.952 | 1 |  |
| PAINTBALL_GRENADE_LAUNCHER | 16.792 | 13.412 | -22.944 | 0, 74.999, 135 | 1 |  |
| PAINTBALL_GRENADE_LAUNCHER | 19.909 | 17.319 | -3.341 | 0, 210, 135 | 1 |  |
| PAINTBALL_GRENADE_LAUNCHER | 29.333 | 13.427 | -42.306 | 2.369, 14.989, 45 | 1 |  |
| PAINTBALL_PISTOL | -26.183 | 12.57 | -37.548 | 0, 283.401, 90.039 | 1 |  |
| PAINTBALL_PISTOL | -25.789 | 12.57 | -37.438 | 0, 287.266, 90.037 | 1 |  |
| PAINTBALL_PISTOL | -25.371 | 12.57 | -37.336 | 0, 287.128, 90.034 | 1 |  |
| PAINTBALL_PISTOL | -24.96 | 12.58 | -37.232 | 0.035, 284.763, 90.007 | 1 |  |
| PAINTBALL_PISTOL | -11.477 | 13.076 | -18.707 | 0.001, 113.764, 89.994 | 1 |  |
| PAINTBALL_PISTOL | 19.572 | 17.122 | -18.41 | 0.001, 115.753, 89.988 | 1 |  |
| PAINTBALL_PISTOL | 26.123 | 17.156 | -12.457 | 0.001, 115.753, 89.988 | 1 |  |
| PAINTBALL_PISTOL | 31.629 | 14.122 | 7.93 | 0.002, 295.733, 89.994 | 1 |  |
| PAINTBALL_SHIELD | -3.243 | 12.208 | -17.61 | 278.847, 269.49, 0.176 | 1 |  |
| PAINTBALL_SHIELD | 1.855 | 15.845 | -84.439 | 270, 271.008, 88.992 | 1 |  |
| PAINTBALL_SHIELD | 8.682 | 17.13 | 2.47 | 270, 265.185, 94.815 | 1 |  |
| PAINTBALL_SHIELD | 29.239 | 16.427 | -45.927 | 270, 272.04, 87.96 | 1 |  |
| PAINTBALL_SHOTGUN | -8.852 | 13.078 | 11.25 | 0, 74.999, 269.99 | 1 |  |
| PAINTBALL_SHOTGUN | 1.207 | 11.001 | -35.863 | 351.588, 173.039, 271.304 | 1 |  |
| PAINTBALL_SHOTGUN | 8.486 | 17.396 | -63.009 | 0, 90, 269.993 | 1 |  |
| PAINTBALL_SHOTGUN | 8.789 | 12.115 | -39.99 | 0, 240, 269.989 | 1 |  |
| PAINTBALL_SHOTGUN | 18.403 | 10.773 | -1.406 | 298.956, 90.826, 274.932 | 1 |  |
| PAINTBALL_SHOTGUN | 21.186 | 16.474 | -18.388 | 0.002, 340.091, 269.996 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | -9.514 | 16.889 | -45.002 | 292.089, 2.497, 266.493 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 10.213 | 16.101 | -54.228 | 298.932, 9.703, 256.149 | 1 |  |
| PAINTBALL_SNIPER_RIFLE | 27.832 | 17.169 | -22.646 | 0, 310.34, 76.027 | 1 |  |
| PAINTBALL_TEAM_BATTLE_GAME_RULE_CIRCUIT | -20.46 | 12.61 | -25.654 | 0, 63.278, 0 | 1 |  |
| SCOREBOARD_PROJECTOR | -21.141 | 11.693 | -27.2 | 0, 244.687, 0 | 1 |  |
| STANDEE_FEMALE_01 | -27.297 | 11.682 | -36.811 | 0, 336.689, 0 | 1 |  |
| STANDEE_FEMALE_01 | -6.541 | 12.482 | 7.393 | 0.012, 208.39, 0 | 1 |  |
| STANDEE_MALE_01 | -20.084 | 11.641 | -29.594 | 356.527, 269.786, 1.914 | 1 |  |
| STANDEE_MALE_01 | -13.229 | 15.801 | -45.045 | 359.787, 17.832, 0 | 1 |  |
| STANDEE_MALE_01 | 4.302 | 16.453 | -22.493 | 359.76, 254.998, 0 | 1 |  |

## 6. Quests and other archive subrooms (one line each)

All on the blank Maker room, saved by 1257174 (APPPLESARCHIVE 2026-03-30, ConcertHall 2026-04-01). "UGC" = the
unresolvable geometry/prop objects (section 1). Types listed by count; full per-object TSVs can be regenerated with
the tool (section 7).

| subroom | views / shapes | content (top types) |
| --- | --- | --- |
| vol.1 OrientationStageEnd | 26 / 39 | SHAPE_CONTAINER x8, GAME_ROLE_NODE x3, AUDIO_PLAYER x2, CLAMP_GIZMO x2, ANIMATIONGIZMO, SANDBOX_TEXT, UGC 7ba50577, WELCOME_MAT_V2, POLAROID, PARTICLE_EMITTER_V2, TRIGGER_HANDLE, ROOM_DOOR, SHOWCASE_POSTER, TRIGGER_VOLUME_CV2, PAR... |
| vol.1 VariousAssets | 270 / 468 | SHAPE_CONTAINER x9, MAKER_PEN_CHARADES x6, INTERACTION_VOLUME_V2 x6, UGC 3719a469 x5, SANDBOX_FLOOR x5, HAY_BALE x5, SANDBOX_TEXT_2D x4, CLAMP_GIZMO x4, ANIMATIONGIZMO_V2 x4, UGC 77eae76d x4, UGC f982219a x3, UGC cbadc592 x3, UGC ... |
| vol.1 OrientationStage2 | 30 / 10 | GAME_ROLE_NODE x3, HOLOTAR_PROJECTOR_CV2 x3, WATERBOTTLE x3, CLAMP_GIZMO x2, UGC 826c0bd2 x2, UGC 41b183e5 x2, UGC 06485663 x2, SHAPE_CONTAINER x2, COUCHCHAIR x2, WELCOME_MAT_V2, INTERACTION_VOLUME, SANDBOX_MIRROR, SANDBOX_TEXT_V3... |
| vol.1 RecRally | 39 / 0 | UGC 9d4d6573 x28, RALLY_BUGGY x6, GAME_ROLE_NODE x3, WELCOME_MAT_V2, NAVMESH_BAKE_VOLUME |
| vol.1 Locker-Hub-not-rro | 317 / 317 | INVISIBLE_COLLISION_V2 x46, SANDBOX_TEXT_2D x23, DYNAMINC_POINT_LIGHT x23, SHAPE_CONTAINER x19, ROCK_04 x15, PING_PONG_BALL x12, SFX_NODE_V2 x10, UGC efaf4451 x10, WATERBOTTLE x8, VECTOR_GADGET x6, TRIGGER_VOLUME_CV2 x6, PLAYER_CR... |
| vol.1 MerryMidwayPaintballmap | no save |  |
| vol.1 LockerRoom-2017 | 258 / 18 | INVISIBLE_COLLISION_V2 x47, DYNAMINC_POINT_LIGHT x23, BUCKET x13, PING_PONG_BALL x12, DODGEBALL x12, WATERBOTTLE x11, UGC efaf4451 x10, SFX_NODE_V2 x10, VECTOR_GADGET x9, TRIGGER_VOLUME_CV2 x6, CLAMP_GIZMO x5, SHAPE_CONTAINER x4, ... |
| vol.1 CrescendoLobby | 60 / 0 | UGC fd3ae206 x15, WATERBOTTLE x6, SANDBOX_TOGGLE_BUTTON_CV2 x4, AUDIO_PLAYER x4, VECTOR_GADGET x4, STANDEE_MALE_01 x3, UGC 95fd4d19 x3, PVP_WHIP x3, GAME_ROLE_NODE x3, STANDEE_FEMALE_01 x2, DRACULA_SHOVEL x2, UGC 51a2e779 x2, INTE... |
| vol.1 Lobby | no save |  |
| vol.1 OriantationStage4 | 76 / 65 | ROTJ_GRENADE x28, BUCKET x5, BACKSTOPSHORT x4, SHAPE_CONTAINER x3, GAME_ROLE_NODE x3, STANDEE_MALE_01 x3, DISCGOLF_DISC x3, DODGEBALL x2, PLAYER_CREATED_SIGN x2, SANDBOX_BUTTON x2, SANDBOX_BUTTON_CV2 x2, AUDIO_PLAYER x2, WINEBARRE... |
| vol.1 Paddleball | 8 / 0 | GAME_ROLE_NODE x3, LEADERBOARD_PROJECTOR, PADDLEBALL_PADDLE, GAME_CONFIGURATION, SCOREBOARD_PROJECTOR, PADDLEBALL_GAME_RULE_CIRCUIT |
| vol.1 MakeitToMidnight | 33 / 24 | DYNAMIC_POINTLIGHT_V2 x7, GAME_ROLE_NODE x3, TABLE_TALL x3, PAINTBALL_GRENADE x2, SHAPE_CONTAINER x2, BACKSTOPSHORT x2, WELCOME_MAT_V2, PAINTBALL_SHIELD, STANDEE_FEMALE_02, PIRATE_FLINTLOCK, PAINTBALL_PISTOL, SPAWNERCIRCUIT, AUDIO... |
| vol.1 NightRecRoyale | 10 / 0 | WATERBOTTLE x3, GAME_ROLE_NODE x3, DISCGOLF_DISC x2, FOG_NODE, WELCOME_MAT_V2 |
| vol.1 RecRoyale-Workingish | 19 / 0 | PIRATE_PLATE x4, WATERBOTTLE x4, GAME_ROLE_NODE x3, SOLOCUP x2, PIRATE_RUM_JUG, WELCOME_MAT_V2, WATER_JUG, SCOREBOARD_PROJECTOR, UGC e8baf3ed, GAME_CONFIGURATION |
| vol.1 RecRoyale-Circa-2019 | 7 / 0 | GAME_ROLE_NODE x3, WATERBOTTLE x3, WELCOME_MAT_V2 |
| vol.1 Makeittomidnightalternate | 112 / 13 | DYNAMIC_POINTLIGHT_V2 x22, PAINTBALL_GRENADE x14, METAL_BARREL x6, TELEPHONE_POLE x6, PAINTBALL_SHIELD x5, PAINTBALL_SHOTGUN x5, PAINTBALL_PISTOL x5, GAME_ROLE_NODE x4, SANDBOX_TEXT_2D x4, VARIABLE_NODE x4, CIRCUIT_STAGE_LIGHT x3,... |
| vol.1 OrientationStage5 | 39 / 0 | DYNAMINC_POINT_LIGHT x11, INVISIBLE_COLLISION_V2 x10, SHOWCASE_POSTER x4, GAME_ROLE_NODE x3, WELCOME_MAT_V2, POLAROID, INVISIBLE_COLLISION, TRIGGER_VOLUME, SANDBOX_BUTTON, INTERACTION_VOLUME, SANDBOX_SEAT, COSTUME_DUMMY_PLUS, HOLO... |
| vol.1 OrientationStage3 | 25 / 0 | INVISIBLE_COLLISION_V2 x4, SFX_NODE_V2 x4, GAME_ROLE_NODE x3, TRIGGER_VOLUME_CV2 x2, WATERBOTTLE x2, UGC 3ffa080f x2, VECTOR_GADGET x2, BASKETBALL, WELCOME_MAT_V2, SANDBOX_SEAT, ROOM_DOOR, INTERACTION_VOLUME, TRIGGER_VOLUME |
| vol.1 LaserTag-Beta | 10 / 0 | GAME_ROLE_NODE x3, PLAYER_RESPAWN_POINT x2, GAME_CONFIGURATION, PAINTBALL_TEAM_BATTLE_GAME_RULE_CIRCUIT, WELCOME_MAT_V2, AUDIO_PLAYER, UGC 516b98ba |
| vol.1 OrientationStage1 | 5 / 0 | GAME_ROLE_NODE x3, INTERACTION_VOLUME_V2, WELCOME_MAT_V2 |
| GrandHall4 | no save |  |
| StuntLobby | no save |  |
| OrientationStage1 | 4 / 0 | GAME_ROLE_NODE x3, WELCOME_MAT_V2 |
| OrientationStage2 | 12 / 1 | GAME_ROLE_NODE x3, COUCHCHAIR x2, CLAMP_GIZMO x2, WELCOME_MAT_V2, GT_SWORD, SANDBOX_TEXT_V3, POLAROID, SHAPE_CONTAINER |
| InvertedBallroom | 101 / 0 | UGC da3a7317 x22, PIRATE_PLATE x21, VECTOR_GADGET x13, SANDBOX_TOGGLE_BUTTON_CV2 x13, AUDIO_PLAYER x13, UGC 95fd4d19 x9, GAME_ROLE_NODE x3, PVP_WHIP x3, WELCOME_MAT_V2, UGC 3ea6bd3c, UGC 808b51c7, INTERACTION_VOLUME |
| SunkenShip | 47 / 0 | PIRATE_FLINTLOCK x23, PIRATE_PLUSH_FISH x17, GAME_ROLE_NODE x3, PIRATE_OAR x2, WELCOME_MAT_V2, PIRATE_BAR_STOOL |
| GrandHall2 | 53 / 0 | UGC da3a7317 x13, AUDIO_PLAYER x9, SANDBOX_TOGGLE_BUTTON_CV2 x6, VECTOR_GADGET x6, UGC 95fd4d19 x3, GAME_ROLE_NODE x3, PVP_WHIP x3, UGC fd3ae206 x2, WELCOME_MAT_V2 x2, UGC 3ea6bd3c x2, DRACULA_BLUNDERBUSS, DRACULA_SHOVEL, DRACULA_... |
| IsleLobby | 43 / 0 | WELCOME_MAT_V2 x8, PIRATE_MUG x4, PIRATE_BEER_BOTTLE x4, PIRATE_RUM_JUG x3, UGC 3417767a x3, GAME_ROLE_NODE x3, STANDEE_PIRATE_01 x3, PIRATE_CUTLASS x2, PIRATE_PLATE x2, UGC 8c922b5c x2, UGC b802e662, SCOREBOARD_PROJECTOR, NAVMESH... |
| GrandHall1-4 | 34 / 2 | AUDIO_PLAYER x8, VECTOR_GADGET x6, SANDBOX_TOGGLE_BUTTON_CV2 x6, GAME_ROLE_NODE x3, PVP_WHIP x3, DYNAMIC_POINTLIGHT_V2 x2, WELCOME_MAT_V2, NAVMESH_BAKE_VOLUME, DRACULA_BLUNDERBUSS, JACKOLANTERN, FOG_NODE, SHAPE_CONTAINER |
| CrescendoLobby | 60 / 0 | UGC fd3ae206 x15, WATERBOTTLE x6, SANDBOX_TOGGLE_BUTTON_CV2 x4, AUDIO_PLAYER x4, VECTOR_GADGET x4, STANDEE_MALE_01 x3, UGC 95fd4d19 x3, PVP_WHIP x3, GAME_ROLE_NODE x3, STANDEE_FEMALE_01 x2, DRACULA_SHOVEL x2, UGC 51a2e779 x2, INTE... |
| RecRoyale | 7 / 0 | GAME_ROLE_NODE x3, WATERBOTTLE x3, WELCOME_MAT_V2 |
| GTact2 | 4 / 0 | GAME_ROLE_NODE x3, WELCOME_MAT_V2 |
| GardenOfSarrows | 37 / 0 | UGC fd3ae206 x26, PVP_WHIP x3, GAME_ROLE_NODE x3, DRACULA_SHOVEL x2, WELCOME_MAT_V2, INTERACTION_VOLUME, UGC 3ea6bd3c |
| ForestRoad | 15 / 0 | PVP_WHIP x4, DRACULA_SHOVEL x4, GAME_ROLE_NODE x3, DRACULA_BLUNDERBUSS x2, WELCOME_MAT_V2, DRACULA_PITCHFORK |
| GTact5 | 4 / 0 | GAME_ROLE_NODE x3, WELCOME_MAT_V2 |
| GTact4 | 4 / 0 | GAME_ROLE_NODE x3, WELCOME_MAT_V2 |
| GTact3 | 4 / 0 | GAME_ROLE_NODE x3, WELCOME_MAT_V2 |
| BurningBurg | 89 / 2 | UGC fd3ae206 x22, DRACULA_SHOVEL x15, UGC 95fd4d19 x12, DRACULA_PITCHFORK x7, AUDIO_PLAYER x4, VARIABLE_NODE x4, COMBINATOR_NODE x4, PVP_WHIP x3, GAME_ROLE_NODE x3, UGC c2501a11 x2, TRIGGER_VOLUME x2, PISTON_GIZMO x2, TRIGGER_VOLU... |
| CastleGrounds | no save |  |
| GibbetJungle | 193 / 0 | PIRATE_BEER_BOTTLE x74, PIRATE_FLINTLOCK x59, PIRATE_RUM_JUG x23, UGC f6caf129 x20, PIRATE_OAR x6, PIRATE_BAR_STOOL x3, GAME_ROLE_NODE x3, WELCOME_MAT_V2, UGC 2b88c635, STANDEE_PIRATE_PHOTO, PIRATE_MUG, NAVMESH_BAKE_VOLUME |
| GalIery | 73 / 0 | PIRATE_BEER_BOTTLE x15, PIRATE_PLUSH_FISH x12, PIRATE_FRYING_PAN x9, PIRATE_MUG x9, PIRATE_PUSH_BROOM x7, PIRATE_BAR_STOOL x6, PIRATE_PLATE x4, GAME_ROLE_NODE x3, WELCOME_MAT_V2 x3, UGC 2b88c635 x2, INTERACTION_VOLUME, PIRATE_RUM_... |
| GhostBeardGauntlet | 48 / 0 | PIRATE_FLINTLOCK x21, PIRATE_BEER_BOTTLE x11, PIRATE_CUTLASS x7, GAME_ROLE_NODE x3, UGC 3ea6bd3c x2, WELCOME_MAT_V2, PIRATE_RUM_JUG, PIRATE_BAR_STOOL, UGC 2f17da13 |
| Gallery | 21 / 0 | GAME_ROLE_NODE x3, PVP_WHIP x3, SANDBOX_WALL x3, SANDBOX_TOGGLE_BUTTON_CV2 x2, VECTOR_GADGET x2, AUDIO_PLAYER x2, SANDBOX_WALL_WINDOW, SANDBOX_FLOOR, CREATOR_FEEDBACK_BUTTONS, SANDBOX_CEILING, WELCOME_MAT_V2, UGC c2501a11 |
| CastleGroundsDay | 13 / 2 | SANDBOX_WALL x3, GAME_ROLE_NODE x3, CREATOR_FEEDBACK_BUTTONS, SANDBOX_FLOOR, WELCOME_MAT_V2, SANDBOX_CEILING, SANDBOX_WALL_WINDOW, FOG_NODE, SHAPE_CONTAINER |
| ConcertHall | 32 / 0 | UGC da3a7317 x4, VECTOR_GADGET x3, SANDBOX_TOGGLE_BUTTON_CV2 x3, PVP_WHIP x3, GAME_ROLE_NODE x3, AUDIO_PLAYER x3, DRACULA_BLUNDERBUSS x2, UGC 0f91367a x2, UGC 7c14958d, DRACULA_SHOVEL, WELCOME_MAT_V2, DRACULA_PITCHFORK, UGC 0d107f... |
| LowerSanctum | 28 / 0 | DRACULA_SHOVEL x5, UGC da3a7317 x4, UGC 95fd4d19 x4, GAME_ROLE_NODE x3, DRACULA_BLUNDERBUSS x3, PVP_WHIP x3, UGC 808b51c7, UGC 3ea6bd3c, UGC 7ec8f3b6, WELCOME_MAT_V2, INTERACTION_VOLUME, SANDBOX_WALL |
| HighSeas | 18 / 0 | PIRATE_PUSH_BROOM x6, UGC 7ec8f3b6 x4, GAME_ROLE_NODE x3, INTERACTION_VOLUME, WELCOME_MAT_V2, UGC 3ea6bd3c, PIRATE_PLUSH_FISH, HAM |
| RecRally | 39 / 0 | UGC 9d4d6573 x28, RALLY_BUGGY x6, GAME_ROLE_NODE x3, WELCOME_MAT_V2, NAVMESH_BAKE_VOLUME |
| ChamberOfKnowledge | 83 / 1 | UGC da3a7317 x16, AUDIO_PLAYER x13, SANDBOX_TOGGLE_BUTTON_CV2 x11, VECTOR_GADGET x11, UGC fd3ae206 x8, UGC 95fd4d19 x5, GAME_ROLE_NODE x3, PVP_WHIP x3, VARIABLE_NODE x2, COMBINATOR_NODE x2, PISTON_GIZMO, UGC 3ea6bd3c, TRIGGER_VOLU... |
| NightRecRoyale | 10 / 0 | WATERBOTTLE x3, GAME_ROLE_NODE x3, DISCGOLF_DISC x2, FOG_NODE, WELCOME_MAT_V2 |
| GrandHall1 | 51 / 0 | UGC da3a7317 x13, AUDIO_PLAYER x8, SANDBOX_TOGGLE_BUTTON_CV2 x6, VECTOR_GADGET x6, UGC 95fd4d19 x3, PVP_WHIP x3, GAME_ROLE_NODE x3, UGC fd3ae206 x2, WELCOME_MAT_V2 x2, DRACULA_BLUNDERBUSS, UGC 3ea6bd3c, DRACULA_SHOVEL, INTERACTION... |
| Foyer | 60 / 0 | UGC fd3ae206 x15, WATERBOTTLE x6, SANDBOX_TOGGLE_BUTTON_CV2 x4, AUDIO_PLAYER x4, VECTOR_GADGET x4, STANDEE_MALE_01 x3, UGC 95fd4d19 x3, PVP_WHIP x3, GAME_ROLE_NODE x3, STANDEE_FEMALE_01 x2, DRACULA_SHOVEL x2, UGC 51a2e779 x2, INTE... |
| GrandHall3 | 54 / 0 | UGC da3a7317 x13, AUDIO_PLAYER x9, VECTOR_GADGET x6, SANDBOX_TOGGLE_BUTTON_CV2 x6, PVP_WHIP x3, UGC 3ea6bd3c x3, UGC 95fd4d19 x3, GAME_ROLE_NODE x3, WELCOME_MAT_V2 x2, UGC fd3ae206 x2, DRACULA_SHOVEL, DRACULA_PITCHFORK, DRACULA_BL... |
| PiratesBanquet | 253 / 0 | PIRATE_BEER_BOTTLE x143, PIRATE_RUM_JUG x28, PIRATE_PLATE x22, PIRATE_MUG x15, PIRATE_PUSH_BROOM x11, PIRATE_BAR_STOOL x10, STANDEE_PIRATE_01 x6, STANDEE_PIRATE_02 x5, GAME_ROLE_NODE x3, PIRATE_PLUSH_FISH x2, PIRATE_CUTLASS x2, ST... |
| PortPlunder | 146 / 0 | PIRATE_FLINTLOCK x72, PIRATE_BEER_BOTTLE x35, PIRATE_RUM_JUG x19, WELCOME_MAT_V2 x4, PIRATE_PUSH_BROOM x4, GAME_ROLE_NODE x3, TRIGGER_VOLUME_CV2, UGC 2b88c635, MOCKTAIL_MILKSHAKE, UGC 3ea6bd3c, MOCKTAIL_MARTINI, INTERACTION_VOLUME... |
| JumbotronBoss | no save |  |
| OriantationStage4 | 75 / 65 | ROTJ_GRENADE x29, BUCKET x5, BACKSTOPSHORT x4, STANDEE_MALE_01 x3, SHAPE_CONTAINER x3, DISCGOLF_DISC x3, GAME_ROLE_NODE x3, WINEBARREL x2, SANDBOX_BUTTON x2, SANDBOX_AUDIO_SAMPLER x2, DODGEBALL x2, AUDIO_PLAYER x2, SANDBOX_BUTTON_... |
| MakeItToMidnight | 4 / 0 | GAME_ROLE_NODE x3, WELCOME_MAT_V2 |
| OrientationStageEnd | 25 / 39 | SHAPE_CONTAINER x8, GAME_ROLE_NODE x3, CLAMP_GIZMO x2, AUDIO_PLAYER x2, PARTICLE_EMITTER_V2, PARTICLE_EMITTER, UGC 7ba50577, SHOWCASE_POSTER, TRIGGER_HANDLE, WELCOME_MAT_V2, TRIGGER_VOLUME_CV2, POLAROID, ROOM_DOOR, ANIMATIONGIZMO |
| GTact1 | 4 / 0 | GAME_ROLE_NODE x3, WELCOME_MAT_V2 |
| OrientationStage3 | 5 / 0 | GAME_ROLE_NODE x3, WELCOME_MAT_V2, BASKETBALL |
| GoldenLobby | 23 / 0 | WATERBOTTLE x6, GAME_ROLE_NODE x3, STANDEE_MALE_01 x2, STANDEE_FEMALE_01 x2, GT_BOW x2, GT_SWORD x2, WELCOME_MAT_V2, INTERACTION_VOLUME, UGC b802e662, WATER_JUG, SCOREBOARD_PROJECTOR, LEADERBOARD_PROJECTOR |
| Testing | no save |  |
| Caves | 45 / 425 | DYNAMINC_POINT_LIGHT x6, SHAPE_CONTAINER x5, SANDBOX_AUDIO_SAMPLER x4, STATE_MACHINE_STATE x4, PARTICLE_EMITTER x4, GAME_ROLE_NODE x3, CLAMP_GIZMO x3, SFX_NODE_V2 x2, TRIGGER_VOLUME x2, RANGEFINDER_GADGET x2, TRIGGER_HANDLE x2, PA... |
| GTact6 | 4 / 0 | GAME_ROLE_NODE x3, WELCOME_MAT_V2 |
| OrientationStage5 | 34 / 0 | DYNAMINC_POINT_LIGHT x11, INVISIBLE_COLLISION_V2 x10, SHOWCASE_POSTER x4, GAME_ROLE_NODE x3, COSTUME_DUMMY_PLUS, HOLOTAR_PROJECTOR_CV2, POLAROID, WELCOME_MAT_V2, SANDBOX_BUTTON, UGC 0f135b7f |

Reading: GTact1-6, MakeItToMidnight (APPPLES), OrientationStage1, RecRoyale(-Circa-2019), NightRecRoyale hold only
the blank-room defaults plus a few bottles: nothing for the Golden Trophy acts. The Crescendo rooms (Foyer,
GrandHall1-4, ConcertHall, LowerSanctum, ChamberOfKnowledge, BurningBurg, GardenOfSarrows, ForestRoad) and the Isle
rooms (IsleLobby, PiratesBanquet, PortPlunder, GibbetJungle, GalIery, HighSeas, SunkenShip, GhostBeardGauntlet) have
many quest props (pirate bottles/jugs/flintlocks, Dracula shovels/pitchforks/blunderbusses, standees) plus repeated
UGC pieces, in large coordinates (e.g. PiratesBanquet props at x 38-131, z 49-68, several standees lying at
rx ~270). GUESS: the archivist placed them from memory or a played snapshot; there is no scene geometry to check
against, so treat them as prop inventories, not layouts. Vol.1 Paddleball (official Paddleball scene d89f74fa) is the
only one with a plausibly official gameplay layer (config + rule circuit + paddle + boards, 8 views).

## 7. Tooling (reusable)

Source: `D:\Dreamport\Rec Room\Rec Room\DevNotes\tools\roomexport\`
- `RoomExport.cs` - self-contained protobuf decoder (FileDescriptorSet parser + wire-format decoder, packed fields,
  maps, `google.protobuf.Any`, GUID bytes shown as `guid:`). Commands:
  - `schema <descriptor_set.binpb> [filter] [full]` - list messages (with fields when a 3rd arg is given)
  - `enum <descriptor_set.binpb> <filter>` - list enums
  - `decode <descriptor_set.binpb> rec_room.PersistedRoomData <persisted_room_data.binpb> <out.json>` - full JSON with
    field names (DormRoom-2018: 57,399 fields, 1.9 MB JSON)
  - `table <descriptor_set.binpb> <SubRoom_dir> <out.tsv> [label]` - one row per persistence view: type (via the
    subroom's PrefabIds.csv), position, Unity euler rotation, scale, tags, component list, all strings (texts, door
    targets, role names), colour, Maker Pen shape count; prints a one-line type summary
  - `guess <descriptor_set.binpb> <file.binpb>` - rank candidate root message types (for super_room_data etc.)
- `build.sh` - builds `out/RoomExport.dll` with Unity's bundled Roslyn (`Editor/Data/DotNetSdkRoslyn/csc.dll`) against
  Unity's bundled .NET 6.0.21 runtime (`Editor/Data/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21`) and writes a
  runtimeconfig; run with `"<Unity>/Editor/Data/NetCoreRuntime/dotnet.exe" out/RoomExport.dll ...`.
- `md.sh <tsv>` - TSV -> the markdown tables in this document.
- `extract.ps1` - pulls only Subroom.json / persisted_room_data.binpb / PrefabIds.csv / CV2NodeTypes.csv /
  descriptor_set.binpb / RoomDetails.json / super_room_data.binpb / Warnings.log out of every zip (skips .glb/.wav and
  Inventions; short folder names because of the 260-char path limit). Edit `$dst` before reuse (it points at this
  session's scratchpad `ex\`; the requested `exports\` folder name was blocked by the sandbox).
- `spawnable_typeids_from_bundles.tsv` - all 645 `SpawnableTool.typeId` GUIDs in the game's bundles with their prefab
  roots (made with a scratch copy of rrtool whose JSON printer shows 16-byte arrays as GUIDs; the DevNotes rrtool
  source was not changed). Join key for `spawnable_tool_data.prefab_id`.

Numbers not verified in Unity: the euler angles are computed, the frame match for the dorm is by geometry agreement
(loft floor, shelves, pillar, rim), not by a shared id.
