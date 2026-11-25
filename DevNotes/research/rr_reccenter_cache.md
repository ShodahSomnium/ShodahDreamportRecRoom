# The final Rec Center, measured from the user's Rec Room download cache (measurements only)

Written 2026-09-24 (iteration 3, task "measure"). Nothing here is loaded, copied or shipped. The numbers were read
from a plain Unity bundle in the user's HTTPCache so the builder can rebuild the room by hand.

## 0. Verdict (read this first)

* **Usable.** The July 2025 Rec Center (the Rec Room Studio / Rooms 2 version, one step before "Home_082525") is in
  the cache as a **plain, readable UnityFS scene bundle**: `HTTPCache/1AF.data` (60.1 MB, downloaded 26 Jul 2025
  21:13:25 GMT). The addressable key inside is
  `Assets/s/Assets/Rooms/RecCenter/RecCenter-RecCenter_071825.unity`, so this is the scene **saved on 18 Jul 2025**.
  All 2,571 GameObjects, their transforms, the renderers, mesh and material names, material colours, 42 lights and
  7 door-sign texts are readable.
* **It is the 2018 building, ported.** The mesh roots keep the 2018 names (`RecCenter_fbx` with `kiosk`, `Windows/Curved`,
  `Window_Skylight`, `upperlevel`, `Beams`, `RecRoomLogo_fbx`), and the result matches the rec.net room picture:
  a central kiosk under 2 skylights, a left balcony, a curved right wall with big windows, and a marquee with red
  curtains on the right.
* **What is NOT in the scene bundle:** the doors themselves, the hoop and backboard, ping-pong, Friend-O-Tron, radios
  and mirror. In a Studio room these are room-data objects that the game spawns at load. They are in the room's data
  save `HTTPCache/182.data` (671 KB, plain protobuf, not encrypted), which was **not decoded** here (section 7).
  Door positions below come from the **door signs** in the scene: every door has a `[Door_Sign]` with a TextMeshPro
  label, about 2.8 m above its floor.
* **Confidence:** high for positions, sizes, heights and material colours (read directly; about 1 cm).
  Medium for the room *outline* (derived from renderer and collider boxes, not from wall vertices). Wall **paint colours
  are vertex colours** (`Wall_Vertcolor_mat`), which were not read. For those, use the picture (cream, blue, orange).

## 1. Sources and method

| step | what | result |
| --- | --- | --- |
| classify | the first 32 bytes of each of the 1,011 `*.data` files (plus the HTTP header file next to each) | 344 UnityFS (Unity **2021.3.29f1**, LZ4HC + some LZMA blocks), 482 JPEG, 115 PNG, 2 JSON, 68 other (61 of them start `08 ..`: protobuf-like room/invention data; none looked encrypted) |
| find | the 2025 log `Player-prev.log` l.1308-1506: `^RecCenter` RoomId 170126, SubRoom 8470630561060716896, `DownloadAssetBundle` 14:13:25-14:13:30 local = 21:13:25-30 GMT (the Dissonance lines give local = GMT-7) | the 45 UnityFS files with HTTP `date: 26 Jul 2025 21:13:25 GMT` = `183`..`1AF`; `1AF` = the scene, `183`..`1AE` = its 44 dependency bundles (textures/shaders/meshes). `182.data` (21:13:21) = the room data save |
| other bundles | `35C.data` (84.6 MB, 7 Aug) = `MakeItToMidnight-Home.unity`; `1B1`..`1EB` (21:13:35+) = players' avatar/outfit bundles | not the Rec Center |
| read | a **copy** of rrtool in this session's scratchpad (`...\1c0947a6-...\scratchpad\rrtool_cache\`): env `RR_BUNDLE_DIR` / `RR_IDX_DIR`, accepts `*.data`, an LZMA block decoder, `bundle#k` = k-th serialized file (`1AF#1` = the scene, `1AF` = its `.sharedassets`), new commands `world` and `mats`. The original tool and index `SP/rrtool/idx` were not touched | index of the cache in `...\1c0947a6-...\scratchpad\cacheidx\` (objects/containers/deps/scripts/cabs TSVs) |
| measure | `rrtool world 1AF#1` composes full 4x4 TRS down the hierarchy; the world AABB is the mesh AABB transformed by the world matrix. **Static-batched** renderers point at `Combined Mesh (root: scene) N` whose vertices are already in world space, so their AABB is the union of the combined mesh's submesh AABBs `[firstSubMesh, +subMeshCount)` (the original mesh name is lost there; a MeshCollider, if any, still names it) | `cacheidx/world_1AF.tsv` (all objects), `render.tsv`, `placed.tsv`, `lights.tsv`, `mats_1AF.tsv`, `match.tsv`; converter `cacheidx/roomframe.pl` |

### 1.1 The room frame used in this document (the transform applied)

The scene's own frame has the Dorm entrance at **+x**, looking **-x** into the hall. Every number below is in the
**room frame**: entrance (Dorm side) at **-Z** looking **+Z**, **+X = right** as you walk in, **Y = 0 on the main
floor**, origin = the **centre of the kiosk floor**:

```
X   = z_scene + 2.05
Y   = y_scene - 0.09          (main floor top = scene y 0.09: rugs, couches, tables and plants stand there)
Z   = -x_scene - 7.23
yaw = yaw_scene + 90 deg      (yaw = heading of the object's +Z axis, clockwise from +Z seen from above, Unity convention)
```

(The kiosk floor AABB centre is at scene (x -7.23, z -2.05).) Other floors: **balcony / upper floors Y = 3.10**,
**theatre Y = -0.79**, **gym Y about +0.2** (its rug and the free-throw lines are at Y 0.16-0.29), kiosk platform top Y 0.25.

## 2. The building (shell)

```
                         Z = +26   gym back wall: flat windows, SPORTS & PVP door (X -7.45)
          +---- gym ---------------------------+
          | hoop key X -17.7..-11.2, Z 17.7..21.3   punching bag (-2.9, 19.3)
          |           back stairs  ________     \  curved wall (apse) with 2 big curved windows
  X=-25   |  lockers  X -13..-7, Z 8..13         \ X 12 at Z 7 -> X -1.5 at Z 18
  +-------+ CAMPUS JAMS door (X -9.2, Z 9)        \
  | cafe  | community board (X -11.6, Z 5.8)       |  MERCH BOOTH X 9..12, Z -0.5..5.7
  | wing  |===== BALCONY (Y 3.1) X -13.9..-9.4 ===|  bulletin board on right wall
  | window| MakerWorld door upstairs (X -13.7,Z 5.6)| X 11.5 ===  STAGE: red curtains, marquee above;
  |X -25  |          [ KIOSK ] origin               |   Z -8.7..-1.8  -> THEATRE X 11.8..21.6 (Y -0.79)
  | cafe  |    2 skylights Z -9.6..-3.5, 4.4..10.5 |                  QUESTS door X 21.5, Z -4.4
  |counter|  lockers under balcony (X -12..-10, Z -9)|
  +-------+ front stairs X -9.4..-3.9, Z -15.6..-12.8 (up to the balcony)
  Featured Rooms door upstairs (X -24.8, Z -11.9)  |
          +------------- entrance wall Z = -15.6 ---+   (office windows above, X -6..4.4, Y 4.6..7.0)
                      | hallway X -1.8..0.5 |  projector room above it (Y 3.1): screen at Z -25.45
                      +--- DORM door Z -25.1 ---+
```

| element | measured (room frame) | from |
| --- | --- | --- |
| building envelope | walls AABB X -25.43..22.78, Z -27.18..26.18, Y -1.02..12.74; floor mesh X -25.05..21.62, Z -26.84..25.96 (Y -0.78..0.24); roof panels Y 8.92..9.41 over X -25.59..22.82, Z -27.39..26.36 | `walls`, `floor`, `roof` |
| **main hall** | about **25.5 m wide x 33.5 m long**: left wall X ~ -13.9 (balcony back wall), right wall X ~ +11.5 (Z -15.6..7), entrance wall Z ~ -15.6, far end = a **curve** (apse) from (X 11.7, Z 7.3) to (X -1.5, Z 18.0) | beams, window/board/marquee planes, `BackCurveWallCollision` (13 boxes, 8.5 m tall, Y 0..8.5) |
| hall ceiling | timber beam grid underside **Y 8.4**, top Y 9.0; roof/ceiling plane Y 8.9-9.4; `ceiling` mesh spans Y 8.04..11.76 (the left wall line rises to about 12 m: posts at X -13.8 reach Y 12.0-12.2 and carry the 3 clerestory windows; exact roof shape not measured) | `Beams/Beams`, `ceiling`, `roof` |
| **skylights** (2) | `Window_Skylight` X **-3.30..3.48**, Y 9.25..9.57, Z **-9.57..-3.45** and Z **4.42..10.54** (each 6.8 x 6.1 m, glass + navy frame), in front of and behind the kiosk logo. Light shafts (`RecCenter_LightShafts`, animated skinned mesh) hang under both, Y 0.99..8.68 | `Windows/Window_Skylight` |
| clerestory windows | 3 x `Window_Small` on the LEFT wall X -13.95..-13.75, Y 9.53..10.86, at Z -11.35..-7.97, -6.57..-3.19, -1.70..1.68 (3.4 m wide each) | `Windows/Window_Small` |
| **timber beams** (dark brown, 0.5-0.6 m deep) | cross-hall beams at **Z -15.39** (X -14.0..7.7), **Z -2.18** (X -14.1..10.6), **Z 2.41** (X -13.9..12.3); short beams over the balcony at Z -12.17 and Z -7.27 (X -13.95..-9.15); lengthwise beams at **X -9.32** (balcony edge, Z -15.5..10.9) and **X -13.83** (left wall top, Z -16.2..8.5); corner posts at the entrance wall (X -13.7 / 7.7, Z -15.4, Y 2.7..12.2) | `Beams/Beams` (21 objects) |
| **balcony** (left) | floor **Y 3.10**, X **-13.9..-9.4** (4.5 m deep), Z **-15.5..8.7**; navy rug X -12.68..-10.33, Z -14.46..6.56; handrail X -9.43, top **Y 4.15** (1.05 m), Z -11.7..8.7 with posts every 2.3-2.4 m (Z -11.65, -9.26, -6.89, -4.49, -2.09, 0.27, 2.56, 4.81, 7.01), rail and posts `LockerRoom_Metal_Mat` (navy) plus a wood rail `Beams/Banister Beams` | `Collision/Railing`, `Rug_UpperFloor`, `UpperLevelCollision` |
| **front stairs** | at the LEFT of the entrance wall: climb toward -X from X ~ -3.9 (Y 0) to the balcony at X ~ -9.4 (Y 3.1), run Z -15.6..-12.9 (2.7 m wide), railing on the hall side at Z -12.8 (Y up to 4.05) | `Collision/Floor/FrontStairs` |
| **back stairs** | at the far end of the balcony (the "staircase at the back left" in the picture): from the hall floor at X ~ -7, Z ~ 11.7 up to a landing X -13.2..-9.7, Z 8.0..13.0 (top ~Y 1.8), then to the balcony end at Z ~ 8.7; banister X -17.8..-7.0, Y 0.4..4.25, Z 7.6..13.2 | `BackStairs`, `Beams/Railing` |
| **right wall and curve** | right wall plane X ~ 11.4..11.7; curved wall with **2 big curved multi-pane windows**: `Window_Curved_Wide` X 6.25..11.67, Z 10.09..16.23, Y 0.53..5.69 and `Window_Curved_Narrow` X -0.61..4.28, Z 17.04..18.25, Y 0.53..5.70; window sill (dark wood) Y 0.52..0.60 along X -0.5..11.8, Z 10.1..18.4 | `Windows/Curved`, `Beams/WindowSill` |
| **stage / theatre opening** (right) | in the right wall at X ~ 11.8, **Z -8.7..-1.8** (7 m): `[Curtain]` either side (X 11.8, Y 2.8, Z -8.68 and -1.80), valance `[Curtain_Flat]` Y 4.21 (Z -5.18); **marquee sign** `[RecCenter_Marquee]` above it: X 10.87..11.29, **Y 4.60..7.96**, Z -9.33..-1.32 (8.0 x 3.4 m, emissive); 3 steps down X 12.0..12.4 (Y 0 -> -0.79) | `Props/Curtains`, `Props/Misc`, `TheaterStairs` |
| theatre room | X 11.8..21.6, floor **Y -0.79**, Z ~ -10..+1.5; 2 couches (X 15.37 / 18.88, Z -8.1), 2 chairs, a rug (X 16.77, Z -4.46), inner curtains X 14.2..19.9 at Z 1.18, **QUESTS door** on its far wall X 21.5 with **4 quest poster cases** (X 21.52, Y 0.39, Z -8.54 / -6.58 / -2.17 / -0.21), `RecCenter_MeetingLight` X 16.6..17.5, Y 3.8..5.8 | `Props/*`, `Collision/Theater` |
| entrance / Dorm hallway | entrance wall Z ~ -15.6; hallway rug (taupe + gold border) X -1.80..0.54, Z -23.72..-16.44; **Dorm door** at the hallway end Z ~ -25.1 (sign X -0.64, Y 2.47); **office windows** above the hall entrance X -6.04..4.37, Y 4.58..6.95 at Z -15.6 | `Rug_Entryway`, `Window_Office`, `Door_Sign (1)` |
| projector room (over the hallway) | floor Y 3.10, X -13.75..5.25, Z -25.7..-19.0; `[RecCenter_ProjectionScreen]` X -7.59..-0.11, **Y 3.49..7.97**, Z -25.45 (7.5 x 4.5 m, facing +Z); 2 button tables (X -1.99..0.67 and -8.29..-5.64, Z -23.1..-21.0, Y 3.13..3.81); 4 couches (Y 3.12) and 2 glowsticks | `UpperLevelCollision`, `Props/Misc` |
| left wing: **cafe** | behind the balcony's back wall (X < -13.9), **X -25..-14, Z -7.5..5**, floor Y 0; counter `[RecCenter_CafeCounter]` X -24.91..-14.01, Z -5.86..-4.49, top Y 0.77; back counter Z -7.41..-6.73 (top Y 1.06); 2 chalkboards (Y 1.08..2.90) and 3 shelves (Y 1.29 / 1.78 / 2.24) on the back wall Z -7.4; 2 display cases X -21.25..-17.63; 2 coffee percolators X -18.25..-17.51, Y 1.05..1.76; **big cafe window** on the far-left wall `Window_Cafe` X -25.18..-18.32, **Y 0.58..5.93**, Z -3.60..4.72, sill Y 0.5..0.63; 5 cafe tables X -23.3..-17.0, Z -1.65..3.07; 21 white chairs (`CafeGroup`); donuts, cakes, pizza, plates as static props | `Props/Cafe`, `Props/Tables`, `Props/Chairs/CafeGroup` |
| left wing upstairs | floor Y 3.10 over the front-left; curved railing X -24.2..-14.4 around Z -6.8..-5.6; 4 couches, 2 chairs, tables with candles; a door runner rug X -22.80..-15.52, Z -13.17..-10.82 leading to the **Featured Rooms** door (X -24.8) | `UpperLevelCollision`, `Rug_DoorRunner` |
| gym (far left, back) | **X ~ -18..-1.5, Z ~ 13..26**, floor Y ~ 0.2; flat windows on the back wall `Window_Flat_Wide` X -16.88..-8.78 and `Window_Flat_Narrow` X -6.08..-3.07, Y 0.87..5.05, Z 25.8..26.1; high side windows `Window_Single` X -18.0, Y 3.65..5.73 at Z 14.6..17.3 and 21.6..24.3; rug X -9.17..-5.71, Z 21.71..24.75; painted **basketball key** (free-throw lines) X -17.7..-11.2, Z 17.7..21.3, so the **hoop hangs on the X ~ -18 wall at Z ~ 19.5**; punching bag (X -2.90, Z 19.26); **SPORTS & PVP** door on the back wall | `Windows/Flat`, `Props/Basketball`, `Rug_Gym` |
| outside | a terrain, a grass backdrop dome (X -271..305), oaks, pines, aspens and bushes, and a brick fence (`[RecCenter_fence]` x27, 3.2 m tall) around the lot at scene x -38.2, z -37.7 / +20.2 | `Terrain`, `Trees`, `Props/Fence` |

## 3. The kiosk and the REC ROOM sign (centre)

All at the origin. `InfoKiosk` (empty root) is at (0.71, 1.47, -1.64). The kiosk is a **square ring about 4.5 x 4.6 m
with 4 dark-wood posts**, not a long counter. In the picture you see its entrance-facing side.

| part | room frame |
| --- | --- |
| platform `kiosk_floor` (wood + trim) | X -2.34..2.41, Z -2.25..2.25, Y 0.03..**0.25** |
| counter `kiosk_counter` (cafe-atlas material) | X -3.19..2.72, Z -2.97..2.86, Y 0.01..2.16 (includes the shelves) |
| back panel / walls `kiosk_walls` | X -2.37..2.16, Z -2.25..2.39, Y -0.08..**4.54** |
| 4 posts (dark wood, 0.38 m square) | (X -2.35, Z -2.19), (X -2.28, Z 2.38), (X 2.19, Z 2.45), (X 2.18, Z -2.22); Y 0..**8.6** (to the ceiling beams) |
| top frame (4 beams) | Y 4.29..4.58, on the post lines |
| **REC ROOM logo** `RecRoomLogo_fbx` | X -2.12..1.96, **Y 4.56..7.42**, flat at Z -2.26..-2.00 (faces the entrance, -Z); 4.1 m wide, 2.9 m tall |
| grating (back) | X -2.36..2.14, Y 1.14..2.51, Z -2.17..0.52 |
| 2 long shelves | Z ~ -2.0, X -2.0..1.67, Y 0.39..0.47 and 0.85..0.93 |
| stacked merch shelf `RecCenter_Shelf_Stacked` | X -1.83..-0.15, Y 0.76..2.16, Z 0.38..0.92 |
| rug under the kiosk `Rug_Kiosk` (taupe carpet, gold border, scale 1.1; round in the picture) | AABB X -4.28..4.30, Z -4.91..6.10 (8.6 x 11.0 m), Y 0..0.06 |
| posters on the kiosk | `[RecCenter_Posters] (6)` (0.16, 1.70, 2.43), `(7)` (-2.38, 2.46, 1.35), `(9)` off; Code of Conduct poster (off) at Z 0.42 |

## 4. Doors and door signs

The door objects (`[ActivityDoor_RRS]`, `[ActivityDoor_Menu_RRS]`, `[LoungeDoor_RRS]`) live in the room data, not in
the scene. Each has a `[Door_Sign]` (sign mesh 1.355 x 0.346 m, `DoorSign_mat` cream + emissive; text font
FUTURA-T-OT-BOLD-CONDENSED SDF). The sign centre is about 2.8 m above the door's floor, and the door is under it.
The "player reads it looking" column is the direction a player faces to read the sign; the door front faces the
opposite way, toward that player.

| sign text (TMP) | sign centre X, Y, Z | above floor | wall | player reads it looking | notes |
| --- | --- | --- | --- | --- | --- |
| **DORM ROOM** | -0.64, 2.47, **-25.12** | 2.5 | hallway end wall Z -25.1 | -Z (sign faces into the hallway) | the entrance; the player arrives here facing +Z |
| **SPORTS & PVP** | **-7.45, 2.86, 25.60** | 2.7 | gym back wall Z ~ 25.8 | +Z | far end, left of centre |
| **QUESTS** | **21.52, 1.98, -4.36** | 2.8 (theatre floor) | theatre far wall X 21.5 | +X | through the red-curtain stage; 4 quest poster cases beside it |
| **Featured Rooms** | **-24.83, 6.05, -11.93** | 2.9 (upper floor) | left wing, far-left wall X ~ -24.9, **upstairs** | -X | a runner rug leads to it |
| **MakerWorld** | **-13.67, 5.81, 5.60** | 2.7 (balcony) | balcony back wall X -13.9, **upstairs** | -X | the 2025 name of the creative door (the user's decision calls it CREATIVE CAMPUS) |
| **CAMPUS JAMS** | **-9.21, 2.53, 9.06** | 2.5 | under the balcony edge, by the back stairs | -X | = the `LLM Door` group: coral door frame `Doorframe` (0.925, 0.486, 0.414) X -9.28..-8.97, Z 7.91..10.24, Y 0..2.26 (2.33 x 2.26 m opening), teal cloth sign `Sign` (0.290, 0.674, 0.706) Y 3.01..3.93, green carpet X -9.46..-6.10 in front |
| State of the Art | 5.22, 2.55, 16.40 | - | curved wall | yaw 117 | **inactive** (GameObject off) |

* **Magic Door:** no sign or object for it in the July 2025 scene. It is either one of the room-data doors or it was
  not there. The room data (section 7) would settle it.
* The room data names the category menus exactly: **"Quests"**, **"Sports & PVP"**, **"Featured Rooms"** and
  **"Campus Jams"**. Each sends `Open Room Selection Menu` with a `Menu Title` and a `Rooms` list, so a door opens a
  room list, as the user decided. Room names that appear in those lists include Bowling, ChromeMusicLab,
  RecRoyaleSquads, RecRoyaleSolos, IsleOfLostSkulls, CrimsonCauldron, GoldenTrophy, TheRiseofJumbotron,
  Labyrinth_Impossible, LaserTag, Dodgeball, Paintball, DiscGolfLake, MakerWorld, TheBrewBox, DenshaNoJousha,
  Komfy_Kafe, ToSaveTheWorld, Crescendo, MakeItToMidnight, WeCooked, Glide_RRS, ThreadRot, WhatBeatsPaper,
  SketchyShowdown, IO_Interrogator, DormRoom, DormRoomPVP. **Which list belongs to which door was not decoded.**

## 5. Furniture and props (key items; the full list is Appendix A)

| item | room frame (X, Y, Z; yaw) | notes |
| --- | --- | --- |
| **merch booth** `[MerchBooth]` | root (10.43, -0.01, 2.65; 180); counter X 9.05..12.01, Z -0.49..5.70, top Y 0.91 | right, against the right wall just past the stage; 3 buying-spot rugs `[Scifi_Rug]` at (10.34, 6.03), (8.71, 4.31), (8.72, 3.11) |
| bulletin board (2nd) | X 11.43..11.61, Y 0.55..3.27, Z 0.54..4.64 | on the right wall, above/behind the merch booth (`DormRoom_BulletinBoard_mat`) |
| **community board** `Systems/CommunityOutpost/BulletinBoard` | X -12.89..-10.25, Y 0.38..2.19, Z 4.81..6.70; yaw -125 (angled) | LEFT, under the balcony (the picture's "notice board on the blue lower wall") |
| lounge by the curved windows | couches `[RecCenter_Couch]` (1.13, -0.04, 16.95; -81) and (6.82, -0.04, 13.43; -136); chairs (1.89, 14.61; 30), (-1.22, 16.61; -131), (7.62, 10.33; 99), (-1.40, 14.79; 154), (9.42, 11.32; 39); side tables (8.04, 12.40), (0.31, 15.54) | the picture's "brown lounge armchairs by the window" |
| vending machines `[VendingMachine_Static]` | (-3.00, 0.25, 24.03; -90) in the gym, and (4.87, 0.00, 16.06; -153) by the curved wall | static meshes; Friend-O-Tron itself is room data |
| punching bag `[PunchingBag]` | (-2.90, 0.25, 19.26; 127) | gym |
| lockers `[Lockers]` x6 | (-15.84, 0.20, 13.49; 0), (-13.71, 0.20, 13.49; 0), (-11.58, 0.20, 13.49; 0), (-17.18, 0.24, 24.31; 90), (-11.98, -0.04, -8.36; 43), (-10.41, -0.04, -9.80; 43) | **they ARE in the July 2025 scene** (back-left by the back stairs, the gym, and 2 under the balcony near the entrance). The user decided to remove the lockers; this is reported only |
| plants (fig, snake, dracaena) | 11, e.g. (11.21, 0, -1.47) by the stage, (5.61, 0, -15.01) by the entrance, (-8.65, 0, -12.27) at the front stairs | Appendix A |
| posters `[RecCenter_Posters]` | 12, e.g. (4.72, 1.77, -15.48) entrance wall, (-7.68, 1.51, -12.68) front stairs, (-2.48, 1.32, 21.38) gym | |
| fluorescent lights `[Light_Flourescent]` | 3 under the balcony (X -12.9..-10.4, Y 2.9..3.1, Z -6.1..3.0), 2 in the hallway, 1 in the cafe, 2 upstairs (Y 8.1..8.3), 1 in the kiosk | emissive tubes |
| candles | 2 on an upstairs table (-19.2, 3.55, -15.0) | point lights |
| NOT in the scene | hoops, backboard, ping-pong tables, radios, mirror, Friend-O-Tron, doors, the kiosk's bell | room-data objects (`Basketball Register Score`, `[Backboard]` and door names appear in `182.data`) |

## 6. Materials and colours (base colour = `_BaseColor`, linear 0..1 as stored; URP Lit-style shaders from the Studio shader bundle)

| material | used on | `_BaseColor` (r, g, b, a) | notes |
| --- | --- | --- | --- |
| `Wood_Floor_Mat` | main floor, kiosk platform, upper floors | (0.972, 0.633, 0.327) | texture `Wood FloorTiled_tex` tiled 5.2 x 31.95; **light orange-brown planks** |
| `Wood_Horizontal_DarkBrown_3x_Mat` | all timber beams, posts, window sills, stair rails | (0.345, 0.197, 0.130) | `wood_diffuse_horiz_desat` x3 |
| `Wall_Vertcolor_mat` | walls, ceiling, roof, kiosk walls, logo, doorway beams | (1, 1, 1) | **colour is in the mesh vertex colours (not read)**; concrete texture x25 |
| `RecCenter_Brick_Red` | brick wall sections (a submesh of `walls`) | (1, 1, 1) + texture `Brick_Grout_tex` x3 | red brick from the texture |
| `RecCenter_SubwayTile_VertColor_mat` | tiled wall sections | (1, 1, 1) + vertex colour | `Brick_DarkGrout_tex` x30 |
| `LockerRoom_Metal_Mat` | **all window frames**, skylight frames, balcony rail posts | (0.070, 0.171, 0.382) | **navy blue** (reads "dark" in the picture) |
| `Glass_Clear_mat` | window glass | (0, 0.621, 1, a 0.239) | transparent blue tint |
| `LockerRoom_FloorTile_mat` | a floor submesh (flecked linoleum x25) | (1, 1, 1) | |
| `RecCenter_Carpet_Taupe_Mat` | kiosk rug, hallway rug, upper-floor carpet | (0.396, 0.379, 0.341) | Burber carpet x10 |
| `RecCenter_Carpet_mat` | balcony rug, gym rug, door runner | (1, 1, 1) + `Carpet_LowPile_Navy_tex` x5 | **navy** carpet |
| `RecCenter_CarpetBorder_Mat` | all rug borders | (0.858, 0.614, 0.336) | gold |
| `RecCenter_CafeAssets_Mat` | kiosk counter and shelves, cafe counters/shelves/percolators/chalkboards, merch counter | (1, 1, 1) + `RecCenter_CafeAssets_tex` | one atlas; the blue kiosk/counter colour comes from the texture |
| `Curtain_Red_Mat` | stage curtains | (0.509, 0.094, 0.094) | |
| `RecCenter_Marquee_Mat` | marquee | (1, 1, 1) + emission (2.09, 2.09, 2.09) x5.06 | `RecCenter_Marquee_tex` + emission map (the bulbs) |
| `DoorSign_mat` | door signs | (0.991, 0.897, 0.808) + emission ~(1.0, 0.99, 0.98) | |
| `Doorframe` | Campus Jams door frame | (0.925, 0.486, 0.414) | coral painted wood |
| `Sign` | Campus Jams cloth sign | (0.290, 0.674, 0.706) | teal |
| `Orientation_Carpet` | Campus Jams carpet | (0.339, 0.509, 0.465) | green diamond carpet |
| `Couch_Minimalist_Brown_Instanced_Mat` | `[RecCenter_Couch]` | (1, 1, 1) + brown texture | recoloured per instance by `RecRoomStudioInstanceRecolor` |
| `HousePlants_Pot_mat` | plant pots | (0.117, 0.420, 0.838) | blue pots |
| `Wood_Table_Mat` | tables | (0.686, 0.455, 0.231) | |
| `Wood_Horizontal_DarkOrange_5x_Mat` | projector-room button tables | (0.510, 0.345, 0.271) | |
| `Light_Fluorescent_mat` | fluorescent tubes | (0.575, 0.575, 0.575) + emission 1.07 x3.51 | |
| `Quest_Poster_Case_Mat` | quest poster cases | (1, 0.854, 0.517) + emission | |

Lighting: all 42 lights are **realtime** (lightmapping mode 4), no shadows except the key light. The **key light** is a directional light, colour (0.973, 0.942, 0.842), intensity 1, soft shadows, yaw 193, pitch 33: the light travels toward -Z and down, so daylight comes from the far (+Z) end and
lights the faces of things as you walk in. The rest are point and spot lights: "Cool" (0.788, 0.851, 1.0) bounce and point lights along the windows and under the skylights, two white downlights under the skylights (intensity 4.47 and 3.91, range about 7 m, Y 8.1-8.6), and "Warm" (1, 0.95-0.97, 0.53-0.83) lights. A cyan and a magenta bounce light sit behind the marquee. Full table: Appendix B.

## 7. Same names as the install (can be spawned or loaded from the player's install through RrAssets)

The names were checked against `SP/rrtool/idx/objects.tsv` (the install index): an exact name, or a prefab root of the same name.

* **Meshes in the install:** `RecCenter_Couch_LOD0/1/2` (install prefab `(RecCenter_Couch_Sandbox)Variant`),
  `RecCenter_Cafe_Plate_LOD0/1/2` (`(RecCenter_Cafe_Plate)`), `Door_Sign_LOD0/1` (also `Door_Sign`, `Door_Sign_Large`),
  `Fig_LOD0/1/2`, `Candle_Tall`, `FlamePlane`, `Poster`, `Cake_Slice_V1_LOD0/1/2`, `PunchingBag_*` (+
  `(PunchingBag)_SpawnableTool`), `scifi_rug`, `Lockers_LOD0/1/2` (`(Sandbox_Lockers)`), `LockerTable_Short/Tall_LOD*`,
  `VendingMachine*` (`(VendingMachine)_SpawnableTool`), `WaterBottle_LOD*` (`(WaterBottle)`), `Consumable_Sarsaparilla_*`.
* **Materials in the install:** `RecCenter_CafeAssets_Mat` (the whole cafe/kiosk atlas), `DoorSign_mat`,
  `Light_Fluorescent_mat`, `Glass_Clear_mat`, `Glass_Teal_mat`, `Curtain_Red_Mat` (+ prefabs
  `(Curtain_Sandbox)Variant`, `(Curtain_Flat_Sandbox)Variant`), `DormRoom_BulletinBoard_mat`,
  `Couch_Minimalist_Brown_Instanced_Mat`, `HousePlants_mat`, `HousePlants_Pot_mat`, `Wood_Table_Mat`,
  `Wood_Horizontal_DarkOrange_5x_Mat`, `Concrete_Blue_50x_Mat`, `CodeOfConduct_Mat`, `VendingMachine_Mat`,
  `PunchingBag_mat`, `Scifi_Rug_Mat`, `Glowstick_Cyan_Mat`, `LanternPost_Mat`, `WaterBottle_Mat`, all the
  `Consumable_*` food materials, and the TMP font material `FUTURA-T-OT-BOLD-CONDENSED SDF (World Space)`.
  Install prefabs also exist for `(RecCenter_Cafe_CoffeePot)` and `(RecCenter_Cafe_Mug)`.
* **NOT in the install** (the builder must make these procedurally): the building shell and its materials
  (`Wall_Vertcolor_mat`, `Wood_Floor_Mat`, `Wood_Horizontal_DarkBrown_3x_Mat`, `LockerRoom_Metal_Mat`,
  `RecCenter_Brick_Red`, `RecCenter_SubwayTile_VertColor_mat`, `LockerRoom_FloorTile_mat`, the `RecCenter_Carpet*`
  materials), the marquee, the projection screen, the posters, the quest poster cases, the Campus Jams door frame
  and sign, `RecCenter_Chair`, the cafe counters, display cases, percolators and chalkboards (only the atlas material
  is in the install), `Table_Cafe_RecCenter`, the white stackable chairs, the fence, the trees and the light shafts.

## 8. What was not done, and how to get it

1. **The room data save `182.data`** (plain protobuf, 671 KB, last-modified 25 Jul 2025): it holds the doors, their
   room lists, the hoop and backboard, the ping-pong tables, Friend-O-Tron, the radios, the mirror and the kiosk
   objects with their transforms (floats next to the object names). A schema-less protobuf walk would give their
   exact positions and each door's room list. It is readable, but the format is Rec Room's own, so it was left for a
   follow-up: about 1-2 h.
2. **Wall paint colours**: they are vertex colours of the static-batched `walls` combined mesh (the scene's Mesh with
   20 submeshes). Reading the vertex colour channel per submesh would give the exact cream, blue and orange.
3. **Exact floor outline**: the numbers above come from boxes (renderer AABBs and colliders). The `floor` mesh itself
   (non-batched, `col:floor`) could be sliced for an exact polygon.

## Appendix A: every placed prefab instance (top-level `[...]` objects), room frame

Columns: X, Y, Z = the object's pivot; yaw in degrees (see 1.1); scale = lossy world scale; AABB = union of its
renderers where its own renderer is on this GameObject (empty for group roots); install match = the same prefab or
object name exists in the install index. Rows marked **off** are inactive in the scene.

| # | object | group | active | X | Y | Z | yaw | scale | world AABB (room frame) | install name match |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | `[PunchingBag]` | (root) | y | -2.90 | 0.25 | 19.26 | 127 | 1.00,1.00,1.00 | - | name:[PunchingBag] |
| 2 | `[SciFi_Glowstick] (2)` | Summer Party | y | -15.21 | 0.95 | 25.59 | -137 | 1.52,1.52,1.52 | X -15.36..-15.10 Y 0.95..0.98 Z 25.44..25.72 | - |
| 3 | `[SciFi_Glowstick] (3)` | Summer Party | y | -14.31 | 0.24 | 23.90 | 131 | 1.77,1.77,1.77 | X -14.46..-14.13 Y 0.24..0.28 Z 23.74..24.03 | - |
| 4 | `[SciFi_Glowstick] (4)` | Summer Party | y | -14.32 | 0.24 | 25.42 | -105 | 1.77,1.77,1.77 | X -14.55..-14.15 Y 0.24..0.28 Z 25.35..25.49 | - |
| 5 | `[SciFi_Glowstick] (5)` | Summer Party | y | -14.54 | 0.24 | 23.11 | -164 | 1.77,1.77,1.77 | X -14.62..-14.48 Y 0.24..0.28 Z 22.88..23.28 | - |
| 6 | `[SciFi_Glowstick] (1)` | (root) | y | -15.08 | 0.24 | 24.78 | -144 | 1.77,1.77,1.77 | X -15.23..-14.96 Y 0.24..0.28 Z 24.59..24.94 | - |
| 7 | `[SciFi_Glowstick]` | (root) | y | -14.25 | 0.95 | 25.59 | 116 | 1.52,1.52,1.52 | X -14.39..-14.06 Y 0.95..0.98 Z 25.49..25.67 | - |
| 8 | `[Wind_Lines_VFX]` | (root) | y | -14.81 | 2.89 | 26.64 | 160 | 0.86,1.07,0.66 | - | - |
| 9 | `[RecCenter_LightShafts]` | LightShafts | y | 0.11 | 8.68 | -6.46 | 90 | 1.00,1.00,1.00 | X -3.13..3.34 Y 0.99..8.68 Z -9.43..-3.50 | - |
| 10 | `[RecCenter_LightShafts]` | LightShafts | y | 0.16 | 8.68 | 7.57 | 90 | 1.00,1.00,1.00 | X -3.08..3.39 Y 0.99..8.68 Z 4.60..10.53 | - |
| 11 | `[RecCenter_LightShafts]` | LightShafts | y | 2.19 | 2.12 | 17.94 | 90 | 1.00,1.00,1.00 | X -1.05..5.42 Y -0.85..5.09 Z 10.25..17.94 | - |
| 12 | `[RecCenter_LightShafts]` | LightShafts | y | 9.03 | 2.12 | 15.90 | 90 | 1.00,1.00,1.00 | X 5.79..12.26 Y -0.85..5.09 Z 8.21..15.90 | - |
| 13 | `[RecCenter_LightShafts]` | LightShafts | y | -12.24 | 2.12 | 26.09 | 90 | 1.00,1.00,1.00 | X -15.48..-9.01 Y -0.85..5.09 Z 18.40..26.09 | - |
| 14 | `[RecCenter_LightShafts]` | LightShafts | y | -1.38 | 2.02 | 25.83 | 90 | 1.00,1.00,1.00 | X -4.62..1.85 Y -0.95..4.99 Z 18.14..25.83 | - |
| 15 | `[Tree_Oak_Desktop_Yellow] No LOD2` | Trees/Oaks | y | -35.38 | -0.39 | -10.92 | 88 | 1.00,1.00,1.00 | - | - |
| 16 | `[Tree_Oak_Desktop_Yellow] No LOD2` | Trees/Oaks | y | -41.33 | 0.34 | 23.88 | 90 | 1.00,1.00,1.00 | - | - |
| 17 | `[Tree_Oak_Desktop_Yellow] No LOD2` | Trees/Oaks | y | 28.55 | -1.58 | 24.38 | -112 | 1.00,1.00,1.00 | - | - |
| 18 | `[Tree_Oak_Desktop_Yellow] No LOD2` | Trees/Oaks | y | -29.35 | 0.11 | 29.39 | -170 | 1.00,1.00,1.00 | - | - |
| 19 | `[Tree_Oak_Desktop_Yellow] No LOD2` | Trees/Oaks | y | -44.93 | -1.57 | 0.58 | -108 | 1.00,1.00,1.00 | - | - |
| 20 | `[Tree_Oak_Desktop_Yellow] No LOD2` | Trees/Oaks | y | 40.07 | -1.57 | 0.12 | -108 | 1.00,1.00,1.00 | - | - |
| 21 | `[Tree_Oak_Desktop_Yellow] No LOD2` | Trees/Oaks | y | 5.28 | 0.12 | 38.48 | 90 | 1.00,1.00,1.00 | - | - |
| 22 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -26.49 | -0.26 | -2.70 | 90 | 1.00,1.00,1.00 | - | - |
| 23 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -24.29 | -0.47 | 2.01 | 90 | 1.00,1.00,1.00 | - | - |
| 24 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -21.35 | -0.47 | 5.04 | 90 | 1.00,1.00,1.00 | - | - |
| 25 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -19.16 | -0.33 | 6.01 | -22 | 1.00,1.00,1.00 | - | - |
| 26 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -33.31 | -0.84 | 8.75 | -108 | 1.00,1.00,1.00 | - | - |
| 27 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -33.47 | -0.84 | 14.00 | -108 | 1.00,1.00,1.00 | - | - |
| 28 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -31.97 | -0.84 | 26.93 | 52 | 1.00,1.00,1.00 | - | - |
| 29 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -21.49 | -0.73 | 30.22 | -26 | 1.00,1.00,1.00 | - | - |
| 30 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | **off** | -16.70 | -0.73 | 29.06 | 142 | 1.00,1.00,1.00 | - | - |
| 31 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -12.31 | -0.73 | 29.84 | 172 | 1.00,1.00,1.00 | - | - |
| 32 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -9.74 | -0.73 | 29.21 | 172 | 1.00,1.00,1.00 | - | - |
| 33 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -5.27 | -0.73 | 29.09 | -7 | 1.00,1.00,1.00 | - | - |
| 34 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | -0.59 | -0.82 | 29.65 | 117 | 1.00,1.00,1.00 | - | - |
| 35 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 7.12 | -0.82 | 28.74 | 8 | 1.00,1.00,1.00 | - | - |
| 36 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 19.44 | -0.62 | 28.20 | -28 | 1.00,1.00,1.00 | - | - |
| 37 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 19.43 | -0.49 | 9.76 | -83 | 1.00,1.00,1.00 | - | - |
| 38 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 15.76 | -0.49 | 5.79 | -8 | 1.00,1.00,1.00 | - | - |
| 39 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 13.78 | -0.93 | 10.11 | -113 | 1.20,1.20,1.20 | - | - |
| 40 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 12.48 | -0.93 | 12.56 | -113 | 1.20,1.20,1.20 | - | - |
| 41 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 10.53 | -0.93 | 14.91 | -180 | 1.20,1.20,1.20 | - | - |
| 42 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 8.25 | -0.93 | 16.99 | -23 | 1.20,1.20,1.20 | - | - |
| 43 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 5.84 | -0.93 | 18.34 | -23 | 1.20,1.20,1.20 | - | - |
| 44 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 3.26 | -0.93 | 19.32 | 147 | 1.20,1.20,1.20 | - | - |
| 45 | `[Tree_Bush_Mobile] No LOD2` | Trees/Bushes | y | 0.56 | -0.93 | 19.96 | -52 | 1.20,1.20,1.20 | - | - |
| 46 | `[Tree_Aspen_Gold_Mobile] No LODs` | Trees/Trees | y | 12.93 | -0.63 | 32.67 | 142 | 1.00,1.00,1.00 | - | - |
| 47 | `[Tree_Aspen_Gold_Mobile] No LODs` | Trees/Trees | y | -20.15 | -0.58 | 34.18 | 142 | 1.00,1.00,1.00 | - | - |
| 48 | `[Tree_Aspen_Gold_Mobile] No LODs` | Trees/Trees | y | -38.92 | -1.42 | 41.84 | 55 | 1.00,1.00,1.00 | - | - |
| 49 | `[Tree_Aspen_Gold_Mobile] No LODs` | Trees/Trees | y | -38.66 | -1.42 | 16.02 | 55 | 1.00,1.00,1.00 | - | - |
| 50 | `[Tree_Aspen_Gold_Mobile] No LODs` | Trees/Trees | y | -41.12 | -1.42 | 10.48 | 55 | 1.00,1.00,1.00 | - | - |
| 51 | `[Tree_Aspen_Gold_Mobile] No LODs` | Trees/Trees | y | -28.91 | -1.42 | -20.60 | 55 | 1.00,1.00,1.00 | - | - |
| 52 | `[Tree_Aspen_Gold_Mobile] No LODs` | Trees/Trees | y | 23.97 | 0.38 | 15.33 | 107 | 1.00,1.00,1.00 | - | - |
| 53 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -52.06 | -1.00 | 16.55 | 180 | 1.00,1.00,1.00 | - | - |
| 54 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -58.63 | -1.00 | 2.24 | 180 | 1.00,1.00,1.00 | - | - |
| 55 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -49.82 | -2.91 | -12.55 | 180 | 1.00,1.00,1.00 | - | - |
| 56 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -44.50 | -0.71 | -22.06 | -96 | 1.00,1.00,1.00 | - | - |
| 57 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -32.75 | -0.10 | -27.74 | -96 | 1.00,1.00,1.00 | - | - |
| 58 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -59.63 | -3.48 | 32.87 | 180 | 1.00,1.00,1.00 | - | - |
| 59 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -46.92 | -0.20 | 38.82 | -116 | 1.00,1.00,1.00 | - | - |
| 60 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -42.20 | -2.93 | 53.24 | -116 | 1.00,1.00,1.00 | - | - |
| 61 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -28.90 | -1.11 | 48.06 | -116 | 1.00,1.00,1.00 | - | - |
| 62 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -19.35 | -0.92 | 43.71 | -116 | 1.00,1.00,1.00 | - | - |
| 63 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -13.41 | -0.92 | 49.03 | 169 | 1.00,1.00,1.00 | - | - |
| 64 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -1.97 | -0.92 | 49.90 | 95 | 1.00,1.00,1.00 | - | - |
| 65 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | 19.99 | -2.44 | 38.93 | 95 | 1.00,1.00,1.00 | - | - |
| 66 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | 32.53 | -0.96 | 36.06 | 95 | 1.00,1.00,1.00 | - | - |
| 67 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | 41.97 | -0.77 | 27.81 | 95 | 1.00,1.00,1.00 | - | - |
| 68 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | 39.12 | -2.38 | 14.92 | 95 | 1.00,1.00,1.00 | - | - |
| 69 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | 28.75 | -0.87 | 8.72 | 95 | 1.00,1.00,1.00 | - | - |
| 70 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | 28.11 | -0.87 | -0.29 | -167 | 1.00,1.00,1.00 | - | - |
| 71 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -21.33 | -2.93 | 71.37 | 169 | 1.00,1.00,1.00 | - | - |
| 72 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | -2.36 | -0.58 | 71.96 | 169 | 1.00,1.00,1.00 | - | - |
| 73 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | 16.06 | -1.14 | 60.90 | 169 | 1.00,1.00,1.00 | - | - |
| 74 | `[Tree_Pine_Mobile] No LODs` | Trees/Trees | y | 31.04 | -0.20 | 54.89 | 169 | 1.00,1.00,1.00 | - | - |
| 75 | `[Table_Cafe_RecCenter]` | Props/Tables | y | -19.93 | 0.00 | -1.65 | 90 | 1.00,1.00,1.00 | - | - |
| 76 | `[LockerTable_Short] (2)` | Props/Tables | y | -3.67 | 3.11 | -19.84 | 180 | 1.39,1.39,1.39 | - | - |
| 77 | `[LockerTable_Tall] (1)` | Props/Tables | y | -5.49 | -0.01 | -12.02 | 2 | 1.00,1.00,1.00 | - | - |
| 78 | `[LockerTable_Short] (3)` | Props/Tables | y | -19.25 | 3.09 | -14.96 | 180 | 1.05,1.05,1.05 | - | - |
| 79 | `[LockerTable_Short] (1)` | Props/Tables | y | -19.06 | 3.09 | -8.51 | -6 | 1.82,1.17,1.82 | - | - |
| 80 | `[Table_Cafe_RecCenter]` | Props/Tables | y | -17.03 | 0.00 | 0.03 | 124 | 1.00,1.00,1.00 | - | - |
| 81 | `[LockerTable_Short] (5)` | Props/Tables | y | 8.04 | 0.01 | 12.40 | 141 | 1.00,1.00,1.00 | - | - |
| 82 | `[Table_Cafe_RecCenter]` | Props/Tables | y | -21.18 | 0.00 | 1.62 | 72 | 1.00,1.00,1.00 | - | - |
| 83 | `[LockerTable_Short] (4)` | Props/Tables | y | 0.31 | 0.01 | 15.54 | -167 | 1.00,1.00,1.00 | - | - |
| 84 | `[Table_Cafe_RecCenter]` | Props/Tables | y | -18.23 | 0.00 | 3.07 | 97 | 1.00,1.00,1.00 | - | - |
| 85 | `[RecCenter_Cafe_Table] (1)` | Props/Tables | y | -23.30 | 0.00 | -1.45 | 90 | 1.00,1.00,1.00 | - | - |
| 86 | `[LockerTable_Tall] (2)` | Props/Tables | y | 4.57 | 3.32 | -17.46 | 90 | 1.00,1.00,1.00 | - | - |
| 87 | `[RecCenter_Cafe_Plate_Static]` | Props/Cafe/Plates | y | -20.38 | 1.35 | -5.41 | 90 | 1.00,1.00,1.00 | - | - |
| 88 | `[RecCenter_Cafe_Plate_Static]` | Props/Cafe/Plates | y | -20.90 | 1.35 | -5.41 | 90 | 1.00,1.00,1.00 | - | - |
| 89 | `[RecCenter_Cafe_Plate_Static] (12)` | Props/Cafe/Plates | y | -20.81 | 0.55 | -4.99 | 131 | 1.31,1.31,1.31 | - | - |
| 90 | `[RecCenter_Cafe_Plate_Static] (10)` | Props/Cafe/Plates | y | -19.06 | 0.62 | -5.19 | 90 | 1.00,1.00,1.00 | - | - |
| 91 | `[RecCenter_Cafe_Plate_Static] (11)` | Props/Cafe/Plates | y | -19.06 | 0.66 | -5.19 | 90 | 1.00,1.00,1.00 | - | - |
| 92 | `[RecCenter_Cafe_Plate_Static] (9)` | Props/Cafe/Plates | y | -19.06 | 0.59 | -5.19 | 90 | 1.00,1.00,1.00 | - | - |
| 93 | `[RecCenter_Cafe_Plate_Static] (3)` | Props/Cafe/Plates | y | -20.41 | 0.55 | -4.74 | 90 | 1.31,1.31,1.31 | - | - |
| 94 | `[RecCenter_Cafe_Plate_Static] (4)` | Props/Cafe/Plates | y | -19.78 | 1.34 | -5.03 | 90 | 1.00,1.00,1.00 | - | - |
| 95 | `[RecCenter_Cafe_Plate_Static] (5)` | Props/Cafe/Plates | y | -20.16 | 1.34 | -5.03 | 90 | 1.00,1.00,1.00 | - | - |
| 96 | `[RecCenter_Cafe_Plate_Static] (6)` | Props/Cafe/Plates | y | -20.91 | 1.34 | -5.03 | 90 | 1.00,1.00,1.00 | - | - |
| 97 | `[RecCenter_Cafe_Plate_Static] (7)` | Props/Cafe/Plates | y | -20.54 | 1.34 | -5.03 | 90 | 1.00,1.00,1.00 | - | - |
| 98 | `[RecCenter_Cafe_Plate_Static] (8)` | Props/Cafe/Plates | y | -19.06 | 0.56 | -5.19 | 90 | 1.00,1.00,1.00 | - | - |
| 99 | `[RecCenter_Cafe_Plate_Static] (13)` | Props/Cafe/Plates | y | -20.76 | 0.94 | -5.20 | 131 | 1.31,1.31,1.31 | - | - |
| 100 | `[RecCenter_Cafe_Plate_Static] (14)` | Props/Cafe/Plates | y | -20.37 | 0.94 | -4.99 | 131 | 1.31,1.31,1.31 | - | - |
| 101 | `[RecCenter_Cafe_Plate_Static] (15)` | Props/Cafe/Plates | y | -19.88 | 0.94 | -4.97 | 131 | 1.31,1.31,1.31 | - | - |
| 102 | `[RecCenter_Donut_Frosted_Static] (3)` | Props/Cafe/Donuts | y | -18.20 | 1.37 | -5.47 | 90 | 1.00,1.00,1.00 | - | - |
| 103 | `[RecCenter_Donut_Frosted_Static] (5)` | Props/Cafe/Donuts | y | -18.37 | 1.37 | -5.29 | 90 | 1.00,1.00,1.00 | - | - |
| 104 | `[RecCenter_Donut_ChocSprinkles_Static] (4)` | Props/Cafe/Donuts | y | -18.98 | 0.96 | -5.14 | 90 | 1.00,1.00,1.00 | - | - |
| 105 | `[RecCenter_Donut_Frosted_Static] (6)` | Props/Cafe/Donuts | y | -18.37 | 1.37 | -5.11 | 90 | 1.00,1.00,1.00 | - | - |
| 106 | `[RecCenter_Donut_Glazed_Static] (2)` | Props/Cafe/Donuts | y | -17.86 | 1.38 | -5.12 | 90 | 1.00,1.00,1.00 | - | - |
| 107 | `[RecCenter_Donut_Glazed_Static] (4)` | Props/Cafe/Donuts | y | -18.04 | 0.97 | -4.77 | 90 | 1.00,1.00,1.00 | - | - |
| 108 | `[RecCenter_Donut_Glazed_Static]` | Props/Cafe/Donuts | y | -17.86 | 1.38 | -4.91 | 90 | 1.00,1.00,1.00 | - | - |
| 109 | `[RecCenter_Donut_Frosted_Static]` | Props/Cafe/Donuts | y | -18.20 | 1.37 | -4.93 | 90 | 1.00,1.00,1.00 | - | - |
| 110 | `[RecCenter_Donut_Frosted_Static] (4)` | Props/Cafe/Donuts | y | -18.37 | 1.37 | -4.93 | 90 | 1.00,1.00,1.00 | - | - |
| 111 | `[RecCenter_Donut_ChocSprinkles_Static] (1)` | Props/Cafe/Donuts | y | -18.98 | 0.96 | -4.75 | 90 | 1.00,1.00,1.00 | - | - |
| 112 | `[RecCenter_Donut_ChocSprinkles_Static]` | Props/Cafe/Donuts | y | -19.17 | 0.96 | -4.75 | 90 | 1.00,1.00,1.00 | - | - |
| 113 | `[RecCenter_Donut_Glazed_Static] (5)` | Props/Cafe/Donuts | y | -17.87 | 0.97 | -4.77 | 90 | 1.00,1.00,1.00 | - | - |
| 114 | `[RecCenter_Donut_Glazed_Static] (6)` | Props/Cafe/Donuts | y | -18.04 | 1.38 | -4.93 | 90 | 1.00,1.00,1.00 | - | - |
| 115 | `[RecCenter_Donut_ChocSprinkles_Static] (3)` | Props/Cafe/Donuts | y | -19.17 | 0.96 | -4.95 | 90 | 1.00,1.00,1.00 | - | - |
| 116 | `[RecCenter_Donut_Glazed_Static] (3)` | Props/Cafe/Donuts | y | -18.04 | 1.38 | -5.12 | 90 | 1.00,1.00,1.00 | - | - |
| 117 | `[RecCenter_Donut_Glazed_Static] (1)` | Props/Cafe/Donuts | y | -17.92 | 1.40 | -4.97 | 134 | 1.00,1.00,1.00 | - | - |
| 118 | `[RecCenter_Donut_ChocSprinkles_Static] (6)` | Props/Cafe/Donuts | y | -18.98 | 0.96 | -5.36 | 90 | 1.00,1.00,1.00 | - | - |
| 119 | `[RecCenter_Donut_ChocSprinkles_Static] (5)` | Props/Cafe/Donuts | y | -19.17 | 0.96 | -5.14 | 90 | 1.00,1.00,1.00 | - | - |
| 120 | `[RecCenter_Donut_Frosted_Static] (1)` | Props/Cafe/Donuts | y | -18.20 | 1.37 | -5.11 | 90 | 1.00,1.00,1.00 | - | - |
| 121 | `[RecCenter_Donut_ChocSprinkles_Static] (2)` | Props/Cafe/Donuts | y | -18.98 | 0.96 | -4.95 | 90 | 1.00,1.00,1.00 | - | - |
| 122 | `[RecCenter_Donut_PinkSprinkles_Static]` | Props/Cafe/Donuts | y | -18.56 | 1.37 | -5.47 | 90 | 1.00,1.00,1.00 | - | - |
| 123 | `[RecCenter_Donut_PinkSprinkles_Static] (2)` | Props/Cafe/Donuts | y | -18.56 | 1.37 | -5.28 | 90 | 1.00,1.00,1.00 | - | - |
| 124 | `[RecCenter_Donut_PinkSprinkles_Static] (3)` | Props/Cafe/Donuts | y | -18.76 | 1.37 | -5.28 | 90 | 1.00,1.00,1.00 | - | - |
| 125 | `[RecCenter_Donut_PinkSprinkles_Static] (4)` | Props/Cafe/Donuts | y | -18.76 | 1.37 | -5.09 | 90 | 1.00,1.00,1.00 | - | - |
| 126 | `[RecCenter_Donut_PinkSprinkles_Static] (5)` | Props/Cafe/Donuts | y | -18.56 | 1.37 | -5.09 | 90 | 1.00,1.00,1.00 | - | - |
| 127 | `[RecCenter_Donut_PinkSprinkles_Static] (6)` | Props/Cafe/Donuts | y | -18.76 | 1.37 | -4.91 | 90 | 1.00,1.00,1.00 | - | - |
| 128 | `[RecCenter_Donut_PinkSprinkles_Static] (7)` | Props/Cafe/Donuts | y | -18.56 | 1.37 | -4.91 | 90 | 1.00,1.00,1.00 | - | - |
| 129 | `[RecCenter_Donut_BostonCreme_Static]` | Props/Cafe/Donuts | y | -19.18 | 1.37 | -4.91 | 164 | 1.00,1.00,1.00 | - | - |
| 130 | `[RecCenter_Donut_BostonCreme_Static] (1)` | Props/Cafe/Donuts | y | -19.00 | 1.37 | -4.91 | -176 | 1.00,1.00,1.00 | - | - |
| 131 | `[RecCenter_Donut_BostonCreme_Static] (2)` | Props/Cafe/Donuts | y | -19.00 | 1.37 | -5.11 | -156 | 1.00,1.00,1.00 | - | - |
| 132 | `[RecCenter_Donut_BostonCreme_Static] (3)` | Props/Cafe/Donuts | y | -19.18 | 1.37 | -5.11 | -176 | 1.00,1.00,1.00 | - | - |
| 133 | `[RecCenter_Donut_BostonCreme_Static] (4)` | Props/Cafe/Donuts | y | -19.00 | 1.37 | -5.31 | 163 | 1.00,1.00,1.00 | - | - |
| 134 | `[RecCenter_Donut_BostonCreme_Static] (5)` | Props/Cafe/Donuts | y | -19.18 | 1.37 | -5.31 | -156 | 1.00,1.00,1.00 | - | - |
| 135 | `[RecCenter_Donut_BostonCreme_Static] (6)` | Props/Cafe/Donuts | y | -19.00 | 1.37 | -5.49 | -156 | 1.00,1.00,1.00 | - | - |
| 136 | `[RecCenter_Donut_Glazed_Static] (7)` | Props/Cafe/Donuts | y | -18.04 | 0.97 | -4.95 | 90 | 1.00,1.00,1.00 | - | - |
| 137 | `[RecCenter_Donut_Glazed_Static] (8)` | Props/Cafe/Donuts | y | -17.87 | 0.97 | -4.95 | 90 | 1.00,1.00,1.00 | - | - |
| 138 | `[RecCenter_Donut_Glazed_Static] (9)` | Props/Cafe/Donuts | y | -17.87 | 0.97 | -5.14 | 90 | 1.00,1.00,1.00 | - | - |
| 139 | `[RecCenter_Donut_Glazed_Static] (10)` | Props/Cafe/Donuts | y | -18.04 | 0.97 | -5.14 | 90 | 1.00,1.00,1.00 | - | - |
| 140 | `[RecCenter_Donut_Glazed_Static] (11)` | Props/Cafe/Donuts | y | -17.87 | 0.97 | -5.35 | 90 | 1.00,1.00,1.00 | - | - |
| 141 | `[RecCenter_Donut_Chocolate_Static]` | Props/Cafe/Donuts | y | -18.23 | 0.97 | -4.77 | 90 | 1.00,1.00,1.00 | - | - |
| 142 | `[RecCenter_Donut_Chocolate_Static] (1)` | Props/Cafe/Donuts | y | -18.41 | 0.97 | -4.77 | 90 | 1.00,1.00,1.00 | - | - |
| 143 | `[RecCenter_Donut_Chocolate_Static] (2)` | Props/Cafe/Donuts | y | -18.41 | 0.97 | -4.96 | 90 | 1.00,1.00,1.00 | - | - |
| 144 | `[RecCenter_Donut_Chocolate_Static] (3)` | Props/Cafe/Donuts | y | -18.23 | 0.97 | -4.96 | 90 | 1.00,1.00,1.00 | - | - |
| 145 | `[RecCenter_Donut_Chocolate_Static] (4)` | Props/Cafe/Donuts | y | -18.41 | 0.97 | -5.17 | 90 | 1.00,1.00,1.00 | - | - |
| 146 | `[RecCenter_Donut_JamFilled_Static]` | Props/Cafe/Donuts | y | -18.79 | 0.97 | -4.76 | 178 | 1.00,1.00,1.00 | - | - |
| 147 | `[RecCenter_Donut_JamFilled_Static] (1)` | Props/Cafe/Donuts | y | -18.61 | 0.97 | -4.76 | -176 | 1.00,1.00,1.00 | - | - |
| 148 | `[RecCenter_Donut_JamFilled_Static] (2)` | Props/Cafe/Donuts | y | -18.61 | 0.97 | -4.96 | 164 | 1.00,1.00,1.00 | - | - |
| 149 | `[RecCenter_Donut_JamFilled_Static] (3)` | Props/Cafe/Donuts | y | -18.79 | 0.97 | -4.96 | -176 | 1.00,1.00,1.00 | - | - |
| 150 | `[RecCenter_Donut_JamFilled_Static] (4)` | Props/Cafe/Donuts | y | -18.61 | 0.97 | -5.18 | -163 | 1.00,1.00,1.00 | - | - |
| 151 | `[RecCenter_Donut_JamFilled_Static] (5)` | Props/Cafe/Donuts | y | -18.79 | 0.97 | -5.18 | -163 | 1.00,1.00,1.00 | - | - |
| 152 | `[RecCenter_DonutBox_Closed_Static]` | Props/Cafe/Donuts | y | -18.55 | 0.55 | -4.99 | 90 | 0.80,0.80,0.80 | X -18.78..-18.33 Y 0.55..0.61 Z -5.28..-4.69 | - |
| 153 | `[RecCenter_DonutBox_Closed_Static] (1)` | Props/Cafe/Donuts | y | -18.53 | 0.62 | -4.98 | 90 | 0.80,0.80,0.80 | X -18.76..-18.30 Y 0.62..0.69 Z -5.27..-4.68 | - |
| 154 | `[RecCenter_DonutBox_Closed_Static] (2)` | Props/Cafe/Donuts | y | -18.56 | 0.68 | -5.01 | 90 | 0.80,0.80,0.80 | X -18.79..-18.33 Y 0.68..0.75 Z -5.30..-4.71 | - |
| 155 | `[RecCenter_Pizza_Cheese_Static]` | Props/Cafe/Donuts | y | -20.95 | 1.35 | -5.11 | 52 | 1.00,1.00,1.00 | - | - |
| 156 | `[RecCenter_Pizza_Cheese_Static] (1)` | Props/Cafe/Donuts | y | -21.01 | 1.36 | -5.01 | 80 | 1.00,1.00,1.00 | - | - |
| 157 | `[RecCenter_Pizza_Pepperoni_Static]` | Props/Cafe/Donuts | y | -20.57 | 1.35 | -5.12 | 24 | 1.00,1.00,1.00 | - | - |
| 158 | `[RecCenter_Pizza_Supreme_Static]` | Props/Cafe/Donuts | y | -20.15 | 1.35 | -5.13 | 24 | 1.00,1.00,1.00 | - | - |
| 159 | `[RecCenter_Pizza_Pepperoni_Static] (2)` | Props/Cafe/Donuts | y | -20.26 | 1.36 | -5.06 | 58 | 1.00,1.00,1.00 | - | - |
| 160 | `[Cake_RedVelvet_Slice_Static]` | Props/Cafe/Donuts | y | -20.31 | 0.96 | -4.92 | -110 | 0.68,0.68,0.68 | - | - |
| 161 | `[Cake_Chocolate_Slice_Static]` | Props/Cafe/Donuts | y | -19.98 | 0.96 | -4.98 | 90 | 0.69,0.69,0.69 | - | - |
| 162 | `[Cake_Chocolate_Slice_Static] (1)` | Props/Cafe/Donuts | y | -20.85 | 0.97 | -5.30 | 94 | 0.69,0.69,0.69 | - | - |
| 163 | `[RecCenter_Pizza_Supreme_Static] (1)` | Props/Cafe/Donuts | y | -19.73 | 1.35 | -5.12 | -34 | 1.00,1.00,1.00 | - | - |
| 164 | `[RecCenter_Cafe_ChalkBoard] (1)` | Props/Cafe | y | -23.02 | 1.99 | -7.37 | 90 | 1.00,1.00,1.00 | X -24.72..-21.32 Y 1.08..2.90 Z -7.46..-7.28 | - |
| 165 | `[RecCenter_Cafe_Shelf] (1)` | Props/Cafe | y | -19.03 | 1.82 | -7.25 | 90 | 1.00,1.00,1.00 | X -20.45..-17.60 Y 1.78..1.87 Z -7.42..-7.08 | name:[RecCenter_Cafe_Shelf] |
| 166 | `[RecCenter_Cafe_BackCounter]` | Props/Cafe | y | -19.46 | -0.10 | -7.12 | 90 | 1.00,1.00,1.00 | X -24.95..-14.00 Y -0.10..1.06 Z -7.41..-6.73 | - |
| 167 | `[RecCenter_Cafe_ChalkBoard]` | Props/Cafe | y | -15.76 | 1.99 | -7.37 | 90 | 1.00,1.00,1.00 | X -17.46..-14.05 Y 1.08..2.90 Z -7.46..-7.28 | - |
| 168 | `[RecCenter_CafeCounter]` | Props/Cafe | y | -19.46 | -0.28 | -5.39 | 90 | 1.00,1.00,1.00 | X -24.91..-14.01 Y -0.29..0.77 Z -5.86..-4.49 | - |
| 169 | `[RecCenter_Cafe_DisplayCase]` | Props/Cafe | y | -18.54 | 0.53 | -5.04 | -90 | 1.00,1.00,1.00 | X -19.44..-17.63 Y 0.51..1.81 Z -5.65..-4.44 | - |
| 170 | `[RecCenter_Cafe_Shelf] (2)` | Props/Cafe | y | -19.79 | 1.34 | -7.25 | 90 | 1.00,1.00,1.00 | X -21.22..-18.37 Y 1.29..1.38 Z -7.42..-7.08 | name:[RecCenter_Cafe_Shelf] |
| 171 | `[RecCenter_Cafe_DisplayCase] (1)` | Props/Cafe | y | -20.34 | 0.53 | -5.04 | -90 | 1.00,1.00,1.00 | X -21.25..-19.44 Y 0.51..1.81 Z -5.65..-4.44 | - |
| 172 | `[RecCenter_Cafe_Shelf]` | Props/Cafe | y | -19.44 | 2.29 | -7.25 | 90 | 1.00,1.00,1.00 | X -20.86..-18.01 Y 2.24..2.33 Z -7.42..-7.08 | name:[RecCenter_Cafe_Shelf] |
| 173 | `[RecCenter_Cafe_Percolator]` | Props/Cafe | y | -18.07 | 1.07 | -7.04 | 90 | 0.84,1.00,1.00 | X -18.25..-17.88 Y 1.05..1.76 Z -7.33..-6.74 | - |
| 174 | `[RecCenter_Cafe_Percolator] (1)` | Props/Cafe | y | -17.69 | 1.07 | -7.04 | 90 | 0.84,1.00,1.00 | X -17.88..-17.51 Y 1.05..1.76 Z -7.33..-6.74 | - |
| 175 | `[WaterBottle_Static]` | Props/Cafe/PastryCases | y | -17.83 | 0.68 | -4.64 | 135 | 1.00,1.00,1.00 | - | - |
| 176 | `[WaterBottle_Static] (3)` | Props/Cafe/PastryCases | y | -18.13 | 0.68 | -4.64 | 135 | 1.00,1.00,1.00 | - | - |
| 177 | `[WaterBottle_Static] (1)` | Props/Cafe/PastryCases | y | -17.93 | 0.68 | -4.64 | 135 | 1.00,1.00,1.00 | - | - |
| 178 | `[WaterBottle_Static] (2)` | Props/Cafe/PastryCases | y | -18.02 | 0.68 | -4.64 | 135 | 1.00,1.00,1.00 | - | - |
| 179 | `[Ham_Static]` | Props/Cafe/PastryCases | y | -20.77 | 0.67 | -5.00 | -120 | 1.41,1.41,1.41 | - | - |
| 180 | `[WaterBottle_Static] (4)` | Props/Cafe/PastryCases | y | -17.84 | 0.68 | -4.77 | 135 | 1.00,1.00,1.00 | - | - |
| 181 | `[Ham_Static] (1)` | Props/Cafe/PastryCases | y | -20.43 | 0.66 | -4.76 | -158 | 1.41,1.41,1.41 | - | - |
| 182 | `[WaterBottle_Static] (5)` | Props/Cafe/PastryCases | y | -18.04 | 0.68 | -4.77 | 135 | 1.00,1.00,1.00 | - | - |
| 183 | `[WaterBottle_Static] (6)` | Props/Cafe/PastryCases | y | -17.93 | 0.68 | -4.79 | 135 | 1.00,1.00,1.00 | - | - |
| 184 | `[WaterBottle_Static] (7)` | Props/Cafe/PastryCases | y | -18.13 | 0.68 | -4.79 | 135 | 1.00,1.00,1.00 | - | - |
| 185 | `[RecRoyale_Frontier_PizzaBox] (1)` | Props/Cafe/PastryCases | y | -19.93 | 0.60 | -5.06 | 114 | 1.00,1.00,1.00 | X -20.26..-19.60 Y 0.60..0.64 Z -5.39..-4.73 | - |
| 186 | `[RecRoyale_Frontier_PizzaBox] (2)` | Props/Cafe/PastryCases | y | -19.93 | 0.64 | -5.06 | 107 | 1.00,1.00,1.00 | X -20.24..-19.62 Y 0.64..0.68 Z -5.37..-4.75 | - |
| 187 | `[Sarsaparilla_Bottle_Static]` | Props/Cafe/PastryCases | y | -19.22 | 0.54 | -4.73 | 0 | 1.00,1.00,1.00 | - | - |
| 188 | `[Sarsaparilla_Bottle_Static] (9)` | Props/Cafe/PastryCases | y | -19.08 | 0.54 | -4.73 | 0 | 1.00,1.00,1.00 | - | - |
| 189 | `[Sarsaparilla_Bottle_Static] (10)` | Props/Cafe/PastryCases | y | -18.95 | 0.54 | -4.73 | 0 | 1.00,1.00,1.00 | - | - |
| 190 | `[Sarsaparilla_Bottle_Static] (11)` | Props/Cafe/PastryCases | y | -19.20 | 0.54 | -4.65 | 0 | 1.00,1.00,1.00 | - | - |
| 191 | `[Sarsaparilla_Bottle_Static] (12)` | Props/Cafe/PastryCases | y | -18.91 | 0.54 | -4.65 | 0 | 1.00,1.00,1.00 | - | - |
| 192 | `[Sarsaparilla_Bottle_Static] (13)` | Props/Cafe/PastryCases | y | -19.07 | 0.54 | -4.65 | 0 | 1.00,1.00,1.00 | - | - |
| 193 | `[RecRoyale_Frontier_PizzaBox]` | Props/Cafe/PastryCases | y | -19.93 | 0.55 | -5.06 | 124 | 1.00,1.00,1.00 | X -20.28..-19.59 Y 0.55..0.60 Z -5.40..-4.71 | - |
| 194 | `[RecCenter_Couch]` | Props/Couches | y | -19.74 | 3.10 | -6.45 | -96 | 1.00,1.00,1.00 | - | - |
| 195 | `[RecCenter_Couch]` | Props/Couches | y | -21.91 | 3.08 | -8.54 | 179 | 1.00,1.00,1.00 | - | - |
| 196 | `[RecCenter_Couch]` | Props/Couches | y | -1.29 | 3.12 | -19.86 | -90 | 1.00,1.00,1.00 | - | - |
| 197 | `[RecCenter_Couch]` | Props/Couches | y | -6.05 | 3.12 | -19.86 | -90 | 1.00,1.00,1.00 | - | - |
| 198 | `[RecCenter_Couch]` | Props/Couches | y | 1.67 | 3.12 | -21.33 | -42 | 1.00,1.00,1.00 | - | - |
| 199 | `[RecCenter_Couch]` | Props/Couches | y | -9.07 | 3.12 | -21.34 | -138 | 1.00,1.00,1.00 | - | - |
| 200 | `[RecCenter_Couch]` | Props/Couches | y | -17.03 | 3.10 | -14.92 | 90 | 1.00,1.00,1.00 | - | - |
| 201 | `[RecCenter_Couch] (1)` | Props/Couches | y | -21.53 | 3.10 | -14.92 | 90 | 1.00,1.00,1.00 | - | - |
| 202 | `[RecCenter_Couch] (1)` | Props/Couches | y | 1.13 | -0.04 | 16.95 | -81 | 1.00,1.00,1.00 | - | - |
| 203 | `[RecCenter_Couch] (2)` | Props/Couches | y | 6.82 | -0.04 | 13.43 | -136 | 1.00,1.00,1.00 | - | - |
| 204 | `[RecCenter_Couch]` | Props/Couches | y | 15.37 | -0.79 | -8.05 | 90 | 1.00,1.00,1.00 | - | - |
| 205 | `[RecCenter_Couch] (1)` | Props/Couches | y | 18.88 | -0.79 | -8.11 | 90 | 1.00,1.00,1.00 | - | - |
| 206 | `[RecCenter_Chair]` | Props/Chairs | y | -16.66 | 3.08 | -8.91 | 9 | 1.00,1.00,1.00 | - | - |
| 207 | `[RecCenter_Chair]` | Props/Chairs | y | -16.89 | 3.05 | -7.18 | -34 | 1.00,1.00,1.00 | - | - |
| 208 | `[RecCenter_Chair] (1)` | Props/Chairs | y | 13.36 | -0.78 | -0.74 | 180 | 1.00,1.00,1.00 | - | - |
| 209 | `[RecCenter_Chair] (2)` | Props/Chairs | y | 14.38 | -0.78 | 0.58 | -90 | 1.00,1.00,1.00 | - | - |
| 210 | `[RecCenter_Chair]` | Props/Chairs | y | 1.89 | -0.01 | 14.61 | 30 | 1.00,1.00,1.00 | - | - |
| 211 | `[RecCenter_Chair] (1)` | Props/Chairs | y | -1.22 | -0.00 | 16.61 | -131 | 1.00,1.00,1.00 | - | - |
| 212 | `[RecCenter_Chair] (2)` | Props/Chairs | y | 7.62 | 0.01 | 10.33 | 99 | 1.00,1.00,1.00 | - | - |
| 213 | `[RecCenter_Chair] (3)` | Props/Chairs | y | -1.40 | -0.02 | 14.79 | 154 | 1.00,1.00,1.00 | - | - |
| 214 | `[RecCenter_Chair] (5)` | Props/Chairs | y | 9.42 | 0.01 | 11.32 | 39 | 1.00,1.00,1.00 | - | - |
| 215 | `[Chair_White_Stationary]` | Props/Chairs/CafeGroup | y | -20.89 | 0.02 | -1.10 | 120 | 1.00,1.00,1.00 | - | - |
| 216 | `[Chair_White_Stationary] (1)` | Props/Chairs/CafeGroup | y | -22.72 | 0.02 | -2.45 | -17 | 1.00,1.00,1.00 | - | - |
| 217 | `[Chair_White_Stationary] (16)` | Props/Chairs/CafeGroup | y | -17.36 | 0.02 | 2.47 | -56 | 1.00,1.00,1.00 | - | - |
| 218 | `[Chair_White_Stationary] (9)` | Props/Chairs/CafeGroup | y | -16.09 | 0.02 | 0.58 | -123 | 1.00,1.00,1.00 | - | - |
| 219 | `[Chair_White_Stationary] (5)` | Props/Chairs/CafeGroup | y | -23.86 | 0.02 | -2.38 | 19 | 1.00,1.00,1.00 | - | - |
| 220 | `[Chair_White_Stationary] (6)` | Props/Chairs/CafeGroup | y | -19.46 | 0.02 | -0.67 | -157 | 1.00,1.00,1.00 | - | - |
| 221 | `[Chair_White_Stationary] (19)` | Props/Chairs/CafeGroup | y | -18.88 | 0.02 | 2.22 | 33 | 1.00,1.00,1.00 | - | - |
| 222 | `[Chair_White_Stationary] (3)` | Props/Chairs/CafeGroup | y | -23.13 | 0.02 | -0.34 | -163 | 1.00,1.00,1.00 | - | - |
| 223 | `[Chair_White_Stationary] (2)` | Props/Chairs/CafeGroup | y | -20.46 | 0.02 | -2.57 | 26 | 1.00,1.00,1.00 | - | - |
| 224 | `[Chair_White_Stationary] (13)` | Props/Chairs/CafeGroup | y | -20.75 | 0.02 | 2.61 | -158 | 1.00,1.00,1.00 | - | - |
| 225 | `[Chair_White_Stationary] (14)` | Props/Chairs/CafeGroup | y | -22.24 | 0.02 | 1.32 | 71 | 1.00,1.00,1.00 | - | - |
| 226 | `[Chair_White_Stationary] (4)` | Props/Chairs/CafeGroup | y | -18.94 | 0.02 | -2.04 | -63 | 1.00,1.00,1.00 | - | - |
| 227 | `[Chair_White_Stationary] (17)` | Props/Chairs/CafeGroup | y | -19.29 | 0.02 | 3.39 | 109 | 1.00,1.00,1.00 | - | - |
| 228 | `[Chair_White_Stationary] (12)` | Props/Chairs/CafeGroup | y | -20.14 | 0.02 | 1.41 | -81 | 1.00,1.00,1.00 | - | - |
| 229 | `[Chair_White_Stationary] (10)` | Props/Chairs/CafeGroup | y | -17.52 | 0.02 | 1.03 | 154 | 1.00,1.00,1.00 | - | - |
| 230 | `[Chair_White_Stationary] (11)` | Props/Chairs/CafeGroup | y | -18.02 | 0.02 | -0.44 | 60 | 1.00,1.00,1.00 | - | - |
| 231 | `[Chair_White_Stationary] (8)` | Props/Chairs/CafeGroup | y | -16.42 | 0.02 | -0.86 | -29 | 1.00,1.00,1.00 | - | - |
| 232 | `[Chair_White_Stationary] (7)` | Props/Chairs/CafeGroup | y | -22.27 | 0.02 | -1.16 | -110 | 1.00,1.00,1.00 | - | - |
| 233 | `[Chair_White_Stationary] (18)` | Props/Chairs/CafeGroup | y | -17.23 | 0.02 | 3.64 | -120 | 1.00,1.00,1.00 | - | - |
| 234 | `[Chair_White_Stationary] (15)` | Props/Chairs/CafeGroup | y | -21.40 | 0.02 | 0.58 | 8 | 1.00,1.00,1.00 | - | - |
| 235 | `[Chair_White_Stationary] (20)` | Props/Chairs/CafeGroup | y | 1.62 | 0.25 | -1.29 | -114 | 1.00,1.00,1.00 | - | - |
| 236 | `[Curtain] (1)` | Props/Curtains | y | 11.79 | 2.80 | -1.80 | 90 | 0.68,0.93,0.93 | - | - |
| 237 | `[Curtain_Flat]` | Props/Curtains | y | 12.10 | 4.21 | -5.18 | 90 | 0.83,0.34,0.75 | - | - |
| 238 | `[Curtain]` | Props/Curtains | y | 11.78 | 2.79 | -8.68 | -90 | 0.60,0.93,0.93 | - | - |
| 239 | `[Curtain_Flat] (3)` | Props/Curtains | y | 16.97 | 1.52 | -10.07 | 180 | 0.85,0.97,0.75 | - | - |
| 240 | `[Curtain_Flat] (2)` | Props/Curtains | y | 17.02 | 5.73 | 1.32 | 180 | 0.81,0.97,0.75 | - | - |
| 241 | `[Curtain] (2)` | Props/Curtains | y | 14.17 | 2.09 | 1.18 | 0 | 0.68,0.93,0.93 | - | - |
| 242 | `[Curtain] (3)` | Props/Curtains | y | 19.92 | 2.09 | 1.18 | -180 | 0.68,0.93,0.93 | - | - |
| 243 | `[RecCenter_fence] (2)` | Props/Fence | y | 22.21 | 1.10 | 17.93 | 90 | 1.00,1.00,1.00 | X 21.72..22.70 Y -0.52..2.72 Z 15.10..20.76 | - |
| 244 | `[RecCenter_fence] (7)` | Props/Fence | y | 8.10 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X 5.27..10.93 Y -0.52..2.72 Z 30.48..31.46 | - |
| 245 | `[RecCenter_fence] (24)` | Props/Fence | y | -35.63 | 1.10 | -7.59 | -90 | 1.00,1.00,1.00 | X -36.12..-35.14 Y -0.52..2.72 Z -10.42..-4.76 | - |
| 246 | `[RecCenter_fence] (12)` | Props/Fence | y | -17.88 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X -20.71..-15.05 Y -0.52..2.72 Z 30.48..31.46 | - |
| 247 | `[RecCenter_fence] (21)` | Props/Fence | y | -35.63 | 1.10 | 7.73 | -90 | 1.00,1.00,1.00 | X -36.12..-35.14 Y -0.52..2.72 Z 4.90..10.56 | - |
| 248 | `[RecCenter_fence] (1)` | Props/Fence | y | 22.21 | 1.10 | 12.51 | 90 | 1.00,1.00,1.00 | X 21.72..22.70 Y -0.52..2.72 Z 9.68..15.34 | - |
| 249 | `[RecCenter_fence] (18)` | Props/Fence | y | -35.63 | 1.10 | 23.01 | -90 | 1.00,1.00,1.00 | X -36.12..-35.14 Y -0.52..2.72 Z 20.18..25.84 | - |
| 250 | `[RecCenter_fence] (9)` | Props/Fence | y | -2.58 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X -5.41..0.25 Y -0.52..2.72 Z 30.48..31.46 | - |
| 251 | `[RecCenter_fence] (26)` | Props/Fence | y | -27.92 | 1.10 | -9.99 | -180 | 1.00,1.00,1.00 | X -30.75..-25.09 Y -0.52..2.72 Z -10.48..-9.50 | - |
| 252 | `[RecCenter_fence] (19)` | Props/Fence | y | -35.63 | 1.10 | 17.89 | -90 | 1.00,1.00,1.00 | X -36.12..-35.14 Y -0.52..2.72 Z 15.06..20.72 | - |
| 253 | `[RecCenter_fence] (23)` | Props/Fence | y | -35.63 | 1.10 | -2.48 | -90 | 1.00,1.00,1.00 | X -36.12..-35.14 Y -0.52..2.72 Z -5.31..0.35 | - |
| 254 | `[RecCenter_fence] (20)` | Props/Fence | y | -35.63 | 1.10 | 12.88 | -90 | 1.00,1.00,1.00 | X -36.12..-35.14 Y -0.52..2.72 Z 10.05..15.71 | - |
| 255 | `[RecCenter_fence] (16)` | Props/Fence | y | -33.24 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X -36.07..-30.41 Y -0.52..2.72 Z 30.48..31.46 | - |
| 256 | `[RecCenter_fence] (14)` | Props/Fence | y | -28.27 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X -31.10..-25.44 Y -0.52..2.72 Z 30.48..31.46 | - |
| 257 | `[RecCenter_fence] (8)` | Props/Fence | y | 2.65 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X -0.18..5.48 Y -0.52..2.72 Z 30.48..31.46 | - |
| 258 | `[RecCenter_fence] (3)` | Props/Fence | y | 22.21 | 1.10 | 23.25 | 90 | 1.00,1.00,1.00 | X 21.72..22.70 Y -0.52..2.72 Z 20.42..26.08 | - |
| 259 | `[RecCenter_fence] (13)` | Props/Fence | y | -23.03 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X -25.86..-20.20 Y -0.52..2.72 Z 30.48..31.46 | - |
| 260 | `[RecCenter_fence] (17)` | Props/Fence | y | -35.63 | 1.10 | 28.18 | -90 | 1.00,1.00,1.00 | X -36.12..-35.14 Y -0.52..2.72 Z 25.35..31.01 | - |
| 261 | `[RecCenter_fence] (25)` | Props/Fence | y | -32.95 | 1.10 | -9.99 | -180 | 1.00,1.00,1.00 | X -35.78..-30.12 Y -0.52..2.72 Z -10.48..-9.50 | - |
| 262 | `[RecCenter_fence] (15)` | Props/Fence | y | 22.21 | 1.10 | 1.76 | 90 | 1.00,1.00,1.00 | X 21.72..22.70 Y -0.52..2.72 Z -1.07..4.59 | - |
| 263 | `[RecCenter_fence] (10)` | Props/Fence | y | -7.75 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X -10.58..-4.92 Y -0.52..2.72 Z 30.48..31.46 | - |
| 264 | `[RecCenter_fence] (5)` | Props/Fence | y | 19.04 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X 16.21..21.87 Y -0.52..2.72 Z 30.48..31.46 | - |
| 265 | `[RecCenter_fence] (6)` | Props/Fence | y | 13.58 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X 10.75..16.41 Y -0.52..2.72 Z 30.48..31.46 | - |
| 266 | `[RecCenter_fence] (11)` | Props/Fence | y | -12.87 | 1.10 | 30.97 | 0 | 1.00,1.00,1.00 | X -15.70..-10.04 Y -0.52..2.72 Z 30.48..31.46 | - |
| 267 | `[RecCenter_fence] (22)` | Props/Fence | y | -35.63 | 1.10 | 2.49 | -90 | 1.00,1.00,1.00 | X -36.12..-35.14 Y -0.52..2.72 Z -0.34..5.32 | - |
| 268 | `[RecCenter_fence] (4)` | Props/Fence | y | 22.21 | 1.10 | 28.56 | 90 | 1.00,1.00,1.00 | X 21.72..22.70 Y -0.52..2.72 Z 25.73..31.39 | - |
| 269 | `[RecCenter_fence]` | Props/Fence | y | 22.21 | 1.10 | 7.12 | 90 | 1.00,1.00,1.00 | X 21.72..22.70 Y -0.52..2.72 Z 4.29..9.95 | - |
| 270 | `[SciFi_Garage_DottedLine]` | Props/Basketball/FreeThrowDecalGroup (1) | y | -12.96 | 0.26 | 19.51 | 91 | 2.30,0.70,0.70 | X -13.03..-12.88 Y 0.26..0.26 Z 17.82..21.21 | - |
| 271 | `[SciFi_Garage_CurvedLine] (1)` | Props/Basketball/FreeThrowDecalGroup (1) | y | -12.09 | 0.26 | 20.38 | -179 | 0.70,0.70,0.70 | X -12.97..-11.22 Y 0.26..0.26 Z 19.48..21.28 | - |
| 272 | `[SciFi_Garage_CurvedLine] (3)` | Props/Basketball/FreeThrowDecalGroup (1) | y | -12.12 | 0.26 | 18.61 | -89 | 0.70,0.70,0.70 | X -13.02..-11.22 Y 0.26..0.26 Z 17.74..19.48 | - |
| 273 | `[SciFi_Garage_DottedLine] (1)` | Props/Basketball/FreeThrowDecalGroup (1) | y | -15.35 | 0.25 | 17.82 | -179 | 3.19,0.70,0.70 | X -17.71..-13.00 Y 0.25..0.25 Z 17.74..17.90 | - |
| 274 | `[SciFi_Garage_DottedLine] (2)` | Props/Basketball/FreeThrowDecalGroup (1) | y | -15.31 | 0.26 | 21.26 | -179 | 3.19,0.04,0.70 | X -17.66..-12.95 Y 0.26..0.26 Z 21.18..21.34 | - |
| 275 | `[Lockers] (2)` | Props/Lockers | y | -15.84 | 0.20 | 13.49 | 0 | 1.00,1.00,1.00 | - | - |
| 276 | `[Lockers] (5)` | Props/Lockers | y | -17.18 | 0.24 | 24.31 | 90 | 1.00,1.00,1.00 | - | - |
| 277 | `[Lockers] (12)` | Props/Lockers | y | -11.98 | -0.04 | -8.36 | 43 | 1.00,1.00,1.00 | - | - |
| 278 | `[Lockers] (13)` | Props/Lockers | y | -10.41 | -0.04 | -9.80 | 43 | 1.00,1.00,1.00 | - | - |
| 279 | `[Lockers] (9)` | Props/Lockers | y | -13.71 | 0.20 | 13.49 | 0 | 1.00,1.00,1.00 | - | - |
| 280 | `[Lockers] (11)` | Props/Lockers | y | -11.58 | 0.20 | 13.49 | 0 | 1.00,1.00,1.00 | - | - |
| 281 | `[Plant_Dracaena] (1)` | Props/Plants | y | -14.64 | 3.09 | -7.44 | 90 | 1.00,1.00,1.00 | - | - |
| 282 | `[Plant_Fig] (5)` | Props/Plants | y | 4.68 | 3.33 | -16.18 | -90 | 1.00,1.00,1.00 | - | - |
| 283 | `[Plant_Fig] (3)` | Props/Plants | y | 5.61 | 0.00 | -15.01 | 0 | 1.00,1.00,1.00 | - | - |
| 284 | `[Plant_Fig] (1)` | Props/Plants | **off** | 20.89 | -0.77 | 0.67 | 180 | 1.00,1.00,1.00 | - | - |
| 285 | `[Plant_Snake] (1)` | Props/Plants | y | -14.25 | 3.10 | -15.28 | 90 | 1.00,1.00,1.00 | - | - |
| 286 | `[Plant_Fig] (4)` | Props/Plants | y | 11.21 | 0.00 | -1.47 | -155 | 0.81,0.81,0.81 | - | - |
| 287 | `[Plant_Snake]` | Props/Plants | y | -8.65 | 0.01 | -12.27 | 90 | 1.00,1.00,1.00 | - | - |
| 288 | `[Plant_Dracaena]` | Props/Plants | y | -24.38 | 3.09 | -7.77 | 132 | 1.00,1.00,1.00 | - | - |
| 289 | `[Plant_Fig]` | Props/Plants | y | 13.28 | -0.77 | 0.39 | -92 | 1.14,1.14,1.14 | - | - |
| 290 | `[Plant_Fig] (2)` | Props/Plants | y | -24.51 | 3.10 | -15.11 | 90 | 1.00,1.00,1.00 | - | - |
| 291 | `[Plant_Fig] (6)` | Props/Plants | y | 12.96 | -0.77 | -9.67 | 0 | 1.00,1.00,1.00 | - | - |
| 292 | `[Door_Sign] (5)` | Props/DoorSigns | y | -7.45 | 2.85 | 25.60 | 90 | 1.00,1.00,1.00 | - | name:[Door_Sign] |
| 293 | `[Door_Sign] (6)` | Props/DoorSigns | **off** | 5.22 | 2.55 | 16.40 | 117 | 1.00,1.00,1.00 | - | name:[Door_Sign] |
| 294 | `[Door_Sign] (7)` | Props/DoorSigns | y | -24.82 | 6.05 | -11.93 | 0 | 1.42,1.58,1.42 | - | name:[Door_Sign] |
| 295 | `[Door_Sign] (8)` | Props/DoorSigns | y | 21.52 | 1.98 | -4.36 | 180 | 1.00,1.00,1.00 | - | name:[Door_Sign] |
| 296 | `[Door_Sign]` | Props/DoorSigns | y | -13.66 | 5.81 | 5.60 | 0 | 1.00,1.00,1.00 | - | name:[Door_Sign] |
| 297 | `[Door_Sign] (1)` | Props/DoorSigns | y | -0.64 | 2.47 | -25.12 | -90 | 1.00,1.00,1.00 | - | name:[Door_Sign] |
| 298 | `[Door_Sign] (9)` | Props/DoorSigns | y | -9.21 | 2.53 | 9.06 | 0 | 1.00,1.00,1.00 | - | name:[Door_Sign] |
| 299 | `[Poster_CoC]` | Props/Posters | **off** | 0.94 | 1.70 | 0.42 | 180 | 1.54,1.85,1.85 | X 0.33..1.55 Y 0.93..2.46 Z 0.42..0.42 | - |
| 300 | `[Quest_Poster_Case]` | Props/Posters | y | 21.52 | 0.39 | -2.17 | -0 | 1.20,1.20,1.20 | - | - |
| 301 | `[Quest_Poster_Case] (1)` | Props/Posters | y | 21.52 | 0.39 | -0.21 | -0 | 1.20,1.20,1.20 | - | - |
| 302 | `[Quest_Poster_Case] (2)` | Props/Posters | y | 21.51 | 0.42 | -8.54 | -0 | 1.20,1.20,1.20 | - | - |
| 303 | `[Quest_Poster_Case] (3)` | Props/Posters | y | 21.52 | 0.39 | -6.58 | -0 | 1.20,1.20,1.20 | - | - |
| 304 | `[RecCenter_Posters] (1)` | Props/Posters | y | 4.72 | 1.77 | -15.48 | 0 | 0.91,0.91,0.91 | - | - |
| 305 | `[RecCenter_Posters] (2)` | Props/Posters | y | -9.85 | 1.32 | 7.00 | 145 | 0.69,0.69,0.69 | - | - |
| 306 | `[RecCenter_Posters] (3)` | Props/Posters | y | -2.48 | 1.32 | 21.38 | -90 | 0.69,0.69,0.69 | - | - |
| 307 | `[RecCenter_Posters]` | Props/Posters | y | -7.68 | 1.51 | -12.68 | 0 | 1.00,1.00,1.00 | - | - |
| 308 | `[RecCenter_Posters] (4)` | Props/Posters | y | -15.01 | 4.63 | -15.48 | 0 | 0.87,0.87,0.87 | - | - |
| 309 | `[RecCenter_Posters] (5)` | Props/Posters | y | 5.19 | 5.20 | -17.53 | -90 | 1.11,1.11,1.11 | - | - |
| 310 | `[RecCenter_Posters] (6)` | Props/Posters | y | 0.16 | 1.70 | 2.43 | 0 | 0.88,0.88,0.88 | - | - |
| 311 | `[RecCenter_Posters] (7)` | Props/Posters | y | -2.38 | 2.46 | 1.35 | -90 | 0.88,0.88,0.88 | - | - |
| 312 | `[RecCenter_Posters] (8)` | Props/Posters | y | -1.50 | 1.17 | 17.89 | 180 | 0.70,0.70,0.70 | - | - |
| 313 | `[RecCenter_Posters] (9)` | Props/Posters | **off** | -1.04 | 0.56 | -2.50 | 180 | 0.56,0.56,0.56 | - | - |
| 314 | `[RecCenter_Posters] (10)` | Props/Posters | y | -10.13 | 4.42 | 9.28 | -90 | 0.88,0.88,0.88 | - | - |
| 315 | `[RecCenter_Posters] (11)` | Props/Posters | y | -16.15 | 1.61 | 4.20 | 180 | 1.03,1.03,1.03 | - | - |
| 316 | `[SciFi_Glowstick] (2)` | Props/Misc | y | -3.77 | 3.71 | -20.11 | -52 | 1.00,1.00,1.00 | X -3.88..-3.69 Y 3.71..3.73 Z -20.18..-20.02 | - |
| 317 | `[SciFi_Glowstick] (1)` | Props/Misc | y | -3.58 | 3.71 | -20.10 | -177 | 1.00,1.00,1.00 | X -3.60..-3.57 Y 3.71..3.73 Z -20.23..-20.00 | - |
| 318 | `[RecCenter_ProjectionScreen]` | Props/Misc | y | -3.85 | 5.84 | -25.45 | 0 | 1.00,1.00,1.00 | X -7.59..-0.11 Y 3.49..7.97 Z -25.56..-25.34 | - |
| 319 | `[Rug]` | Props/Misc | y | 16.77 | -0.76 | -4.46 | 90 | 1.90,1.90,1.90 | - | - |
| 320 | `[Candles] (2)` | Props/Misc | y | -19.22 | 3.55 | -14.97 | 90 | 1.00,1.00,1.00 | - | name:Candles |
| 321 | `[Candles] (3)` | Props/Misc | y | -19.35 | 3.55 | -15.08 | 90 | 1.00,1.00,1.00 | - | name:Candles |
| 322 | `[RecCenter_Marquee]` | Props/Misc | y | 11.14 | 5.69 | -5.32 | -90 | 1.00,1.00,1.00 | X 10.87..11.29 Y 4.60..7.96 Z -9.33..-1.32 | - |
| 323 | `[Light_Flourescent]` | Light_Meshes | y | -11.63 | 3.10 | -1.44 | 90 | 1.00,1.00,1.00 | X -12.91..-10.35 Y 2.92..3.10 Z -1.71..-1.17 | name:[Light_Flourescent] |
| 324 | `[Light_Flourescent]` | Light_Meshes | y | -11.63 | 3.10 | -5.84 | 90 | 1.00,1.00,1.00 | X -12.91..-10.35 Y 2.92..3.10 Z -6.11..-5.57 | name:[Light_Flourescent] |
| 325 | `[Light_Flourescent]` | Light_Meshes | y | -11.63 | 3.10 | 2.74 | 90 | 1.00,1.00,1.00 | X -12.91..-10.35 Y 2.92..3.10 Z 2.47..3.01 | name:[Light_Flourescent] |
| 326 | `[Light_Flourescent]` | Light_Meshes | y | -0.73 | 3.09 | -18.11 | 90 | 1.00,0.72,1.00 | X -2.01..0.55 Y 2.95..3.09 Z -18.38..-17.84 | name:[Light_Flourescent] |
| 327 | `[Light_Flourescent]` | Light_Meshes | y | -0.73 | 3.09 | -21.86 | 90 | 1.00,0.72,1.00 | X -2.01..0.55 Y 2.96..3.09 Z -22.13..-21.59 | name:[Light_Flourescent] |
| 328 | `[Light_Flourescent]` | Light_Meshes | y | -19.42 | 2.98 | -6.39 | 90 | 1.00,0.71,1.00 | X -20.70..-18.14 Y 2.84..2.98 Z -6.66..-6.12 | name:[Light_Flourescent] |
| 329 | `[Light_Flourescent]` | Light_Meshes | y | -21.28 | 8.27 | -12.06 | 180 | 1.00,1.00,1.00 | X -21.55..-21.01 Y 8.09..8.27 Z -13.34..-10.78 | name:[Light_Flourescent] |
| 330 | `[Light_Flourescent]` | Light_Meshes | y | -17.02 | 8.27 | -12.06 | 180 | 1.00,1.00,1.00 | X -17.29..-16.75 Y 8.09..8.27 Z -13.34..-10.78 | name:[Light_Flourescent] |
| 331 | `[Light_Flourescent]` | Light_Meshes | y | -0.06 | 3.57 | -0.93 | 90 | 1.00,1.00,1.00 | X -1.34..1.22 Y 3.38..3.57 Z -1.20..-0.66 | name:[Light_Flourescent] |
| 332 | `[RecCenter_MeetingLight] (1)` | Light_Meshes | y | -3.61 | 9.34 | -20.15 | 180 | 0.04,0.03,0.04 | X -4.87..-2.36 Y 7.62..9.36 Z -20.59..-19.71 | - |
| 333 | `[RecCenter_MeetingLight]` | Light_Meshes | y | 17.03 | 5.73 | -4.43 | 90 | 0.04,0.04,0.04 | X 16.59..17.47 Y 3.76..5.75 Z -5.68..-3.17 | - |
| 334 | `[CloudPlane_Locker_fbx]` | Systems/Lighting/Baked/Skybox Reflection Probe | **off** | 2.05 | 12.07 | -7.23 | 171 | 1.00,1.00,1.00 | - | - |
| 335 | `[Scifi_Rug]` | Systems/Storefront/MerchBooth/BuyingSpots | y | 10.34 | 0.01 | 6.03 | -90 | 0.25,0.25,0.25 | X 9.94..10.73 Y 0.01..0.02 Z 5.76..6.30 | - |
| 336 | `[Scifi_Rug] (1)` | Systems/Storefront/MerchBooth/BuyingSpots | y | 8.71 | 0.02 | 4.31 | 180 | 0.25,0.25,0.25 | X 8.44..8.98 Y 0.02..0.03 Z 3.91..4.70 | - |
| 337 | `[Scifi_Rug] (2)` | Systems/Storefront/MerchBooth/BuyingSpots | y | 8.72 | 0.02 | 3.11 | 180 | 0.25,0.25,0.25 | X 8.45..8.98 Y 0.02..0.03 Z 2.71..3.50 | - |
| 338 | `[Scifi_Rug] (3)` | Systems/Storefront/MerchBooth/BuyingSpots | y | 8.72 | 0.02 | 1.91 | 180 | 0.25,0.25,0.25 | X 8.45..8.99 Y 0.02..0.03 Z 1.51..2.30 | - |
| 339 | `[Scifi_Rug] (4)` | Systems/Storefront/MerchBooth/BuyingSpots | y | 8.72 | 0.02 | 0.71 | 180 | 0.25,0.25,0.25 | X 8.46..8.99 Y 0.02..0.03 Z 0.31..1.10 | - |
| 340 | `[Scifi_Rug] (5)` | Systems/Storefront/MerchBooth/BuyingSpots | y | 10.36 | 0.01 | -0.90 | 90 | 0.25,0.25,0.25 | X 9.96..10.75 Y 0.01..0.02 Z -1.17..-0.63 | - |
| 341 | `[Scifi_Rug] (1)` | Systems/Storefront/CafeMerchBooth/BuyingSpots | y | -15.32 | 0.02 | -4.29 | -89 | 0.25,0.25,0.25 | X -15.72..-14.92 Y 0.02..0.03 Z -4.57..-4.02 | - |
| 342 | `[Scifi_Rug] (2)` | Systems/Storefront/CafeMerchBooth/BuyingSpots | y | -16.52 | 0.02 | -4.27 | -89 | 0.25,0.25,0.25 | X -16.92..-16.12 Y 0.02..0.03 Z -4.54..-3.99 | - |
| 343 | `[Scifi_Rug] (3)` | Systems/Storefront/CafeMerchBooth/BuyingSpots | y | -22.32 | 0.02 | -4.18 | -89 | 0.25,0.25,0.25 | X -22.72..-21.92 Y 0.02..0.03 Z -4.45..-3.90 | - |
| 344 | `[Scifi_Rug] (4)` | Systems/Storefront/CafeMerchBooth/BuyingSpots | y | -23.52 | 0.02 | -4.15 | -89 | 0.25,0.25,0.25 | X -23.92..-23.12 Y 0.02..0.03 Z -4.43..-3.87 | - |
| 345 | `[MerchBooth]` | Environment | y | 10.43 | -0.01 | 2.65 | 180 | 0.97,1.00,1.01 | - | - |
| 346 | `[VendingMachine_Static]` | (root) | y | -3.00 | 0.25 | 24.03 | -90 | 1.00,1.00,1.00 | - | - |
| 347 | `[VendingMachine_Static] (1)` | (root) | y | 4.87 | 0.00 | 16.06 | -153 | 1.00,1.00,1.00 | - | - |

## Appendix B: lights (room frame)

| light | active | X | Y | Z | yaw | pitch | type (0 spot, 1 dir, 2 point), colour, intensity, range, spot angle, shadows, mode (4 = realtime) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Props/Misc/[Candles] (2)/Candle_Short/Point Light | y | -19.22 | 3.83 | -14.97 | -109 | -90 | `{"type":2,"color":"rgba(1,0.543216,0.270588,1)","intensity":0.18,"range":1.20543,"spot":30,"shadows":0,"mode":4}` |
| Props/Misc/[Candles] (2)/Candle_Tall/Point Light (1) | off | -19.22 | 3.91 | -14.98 | -109 | -90 | `{"type":2,"color":"rgba(1,0.543216,0.270588,1)","intensity":0.35,"range":1.65765,"spot":30,"shadows":0,"mode":4}` |
| Props/Misc/[Candles] (3)/Candle_Short/Point Light | off | -19.35 | 3.83 | -15.08 | -86 | -90 | `{"type":2,"color":"rgba(1,0.543216,0.270588,1)","intensity":0.18,"range":1.20543,"spot":30,"shadows":0,"mode":4}` |
| Props/Misc/[Candles] (3)/Candle_Tall/Point Light (1) | y | -19.35 | 3.91 | -15.09 | -86 | -90 | `{"type":2,"color":"rgba(1,0.543216,0.270588,1)","intensity":0.35,"range":1.65765,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/KeyLight | y | -1.87 | 1.28 | -17.61 | -167 | 33 | `{"type":1,"color":"rgba(0.972549,0.94233,0.842227,1)","intensity":1,"range":10,"spot":30,"shadows":2,"mode":4}` |
| Systems/Lighting/Cool/Bounce Light | y | -23.32 | 3.25 | 1.27 | 124 | -10 | `{"type":0,"color":"rgba(0.788235,0.85098,1,1)","intensity":2.29,"range":13.36,"spot":170,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -13.41 | 10.01 | -9.68 | -90 | -0 | `{"type":2,"color":"rgba(0.788235,0.85098,1,1)","intensity":2.12,"range":5.47,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -13.48 | 10.19 | -4.86 | -90 | -0 | `{"type":2,"color":"rgba(0.788235,0.85098,1,1)","intensity":2.12,"range":5.47,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -13.46 | 10.19 | 0.02 | -90 | -0 | `{"type":2,"color":"rgba(0.788235,0.85098,1,1)","intensity":2.12,"range":5.47,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Bounce Light | y | 9.22 | 2.84 | 13.16 | -133 | -0 | `{"type":0,"color":"rgba(0.788235,0.85098,1,1)","intensity":2.75,"range":8.70379,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Bounce Light | y | 1.79 | 3.19 | 17.59 | -167 | 0 | `{"type":0,"color":"rgba(0.788235,0.85098,1,1)","intensity":2.74,"range":8.70379,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Bounce Light | y | -11.85 | 2.96 | 25.85 | 180 | -0 | `{"type":0,"color":"rgba(0.788235,0.85098,1,1)","intensity":1.38,"range":8.26748,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Bounce Light | y | -4.48 | 3.68 | 25.85 | 180 | -0 | `{"type":0,"color":"rgba(0.788235,0.85098,1,1)","intensity":1.13,"range":7.89,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -17.57 | 4.67 | 15.92 | 90 | -0 | `{"type":0,"color":"rgba(0.788235,0.85098,1,1)","intensity":0.86,"range":5.47,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -11.45 | 1.45 | -1.44 | 90 | -0 | `{"type":2,"color":"rgba(0.788235,0.85098,1,1)","intensity":1.33,"range":5.26,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -3.60 | 7.76 | -20.15 | -90 | -90 | `{"type":2,"color":"rgba(0.783,0.881671,1,1)","intensity":1.32,"range":13.7,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -19.37 | 2.04 | -6.38 | 180 | -0 | `{"type":2,"color":"rgba(0.788235,0.85098,1,1)","intensity":2.31,"range":6.58583,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -21.40 | 5.91 | -12.14 | 90 | -0 | `{"type":2,"color":"rgba(0.788235,0.85098,1,1)","intensity":1.24,"range":6.67005,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -17.50 | 4.67 | 10.61 | 90 | -0 | `{"type":0,"color":"rgba(0.788235,0.85098,1,1)","intensity":1.13,"range":5.27973,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -16.84 | 5.91 | -12.14 | 90 | -0 | `{"type":2,"color":"rgba(0.788235,0.85098,1,1)","intensity":1.24,"range":6.02491,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -11.45 | 1.45 | 2.70 | 90 | -0 | `{"type":2,"color":"rgba(0.788235,0.85098,1,1)","intensity":1.33,"range":5.26,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -11.45 | 1.45 | -5.86 | 90 | -0 | `{"type":2,"color":"rgba(0.788235,0.85098,1,1)","intensity":1.33,"range":5.26,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -5.74 | 4.20 | 0.31 | -179 | -0 | `{"type":2,"color":"rgba(1,1,1,1)","intensity":0.68684,"range":7.5,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | -11.67 | 5.81 | -4.81 | 90 | -0 | `{"type":2,"color":"rgba(1,1,1,1)","intensity":0.68684,"range":7.5,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | 0.09 | 8.56 | -6.49 | 90 | -90 | `{"type":2,"color":"rgba(1,1,1,1)","intensity":4.47,"range":6.88092,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light | y | 0.09 | 8.07 | 7.57 | 90 | -90 | `{"type":2,"color":"rgba(1,1,1,1)","intensity":3.91,"range":7.5,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Bounce Light (1) | y | 11.38 | 6.51 | -5.33 | -89 | -0 | `{"type":0,"color":"rgba(0.134,0.929415,1,1)","intensity":1.72,"range":3.01,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Bounce Light (2) | y | 11.38 | 6.51 | -5.33 | -89 | -0 | `{"type":0,"color":"rgba(1,0.203922,0.972608,1)","intensity":0.61,"range":4.16,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Point Light (1) | y | -17.57 | 4.67 | 22.84 | 90 | -0 | `{"type":0,"color":"rgba(0.788235,0.85098,1,1)","intensity":0.86,"range":5.47,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Cool/Bounce Light (3) | y | -9.14 | 7.52 | -13.79 | 91 | -0 | `{"type":0,"color":"rgba(0.992157,0.968628,0.796079,1)","intensity":1.38,"range":2.76091,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Point Light | y | -1.13 | -0.37 | -19.55 | -4 | -90 | `{"type":0,"color":"rgba(1,0.948118,0.657,1)","intensity":1.06,"range":6.16787,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Point Light | y | -18.91 | 3.48 | -1.37 | 95 | -0 | `{"type":2,"color":"rgba(0.93,0.983667,1,1)","intensity":1.09,"range":11.3925,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Point Light | y | 16.98 | 2.19 | -4.43 | -90 | -90 | `{"type":2,"color":"rgba(1,0.933597,0.561,1)","intensity":2.56,"range":8,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Point Light | y | 0.72 | 2.76 | 12.59 | -76 | -0 | `{"type":2,"color":"rgba(1,0.974545,0.832,1)","intensity":0.88,"range":18.6975,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Point Light | y | 7.51 | 2.76 | 7.97 | -64 | -0 | `{"type":2,"color":"rgba(1,0.974545,0.832,1)","intensity":0.88,"range":18.6975,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Point Light | y | -10.13 | 3.20 | 18.81 | -86 | -0 | `{"type":2,"color":"rgba(1,0.9664,0.776,1)","intensity":1.52,"range":16.9416,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Point Light | y | -2.98 | 3.94 | -6.28 | -179 | -0 | `{"type":2,"color":"rgba(1,0.971412,0.811,1)","intensity":1.44,"range":23.955,"spot":30,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Point Light (1) | y | -1.17 | -0.37 | -21.93 | -4 | -90 | `{"type":2,"color":"rgba(1,0.929412,0.533333,1)","intensity":0.59,"range":7.15829,"spot":90,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Point Light (2) | y | -0.11 | 3.53 | -0.92 | 9 | 90 | `{"type":0,"color":"rgba(1,0.977765,0.853,1)","intensity":1.88,"range":3.22,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Bounce Light | y | -4.91 | -0.05 | 20.99 | 90 | -90 | `{"type":0,"color":"rgba(1,0.929412,0.533333,1)","intensity":2.02,"range":5.53826,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Bounce Light (1) | y | -15.27 | -0.05 | 21.00 | 90 | -90 | `{"type":0,"color":"rgba(1,0.929412,0.533333,1)","intensity":1.55,"range":5.53826,"spot":179,"shadows":0,"mode":4}` |
| Systems/Lighting/Warm/Bounce Light (2) | y | -23.34 | -0.05 | -3.62 | 90 | -90 | `{"type":0,"color":"rgba(1,0.929412,0.533333,1)","intensity":2.37,"range":4.94,"spot":179,"shadows":0,"mode":4}` |

## Appendix C: files (scratchpad of the measuring session; not shipped, not in the project)

* `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\1c0947a6-bfa3-41e9-ae24-3394d8a31490\scratchpad\cacheidx\`: `classify.tsv` (all 1,011 cache files: size, date, magic, content-type), `world_1AF.tsv` (every GameObject, scene frame), `render.tsv` / `placed.tsv` / `lights.tsv` (room frame), `mats_1AF.tsv`, `match.tsv`, `strings_182.txt` (printable strings of the room data), the rrtool index TSVs of the cache, `roomframe.pl` (the transform of 1.1).
* Tool: sources saved in `DevNotes\tools\rrtool_cache\` (README there); built copy in `...\scratchpad\rrtool_cache\` (`Program.cs`, `Core.cs`, `Lzma.cs`, `Scene.cs`; build `dotnet build -c Release -o bin/out`). Use: `RR_BUNDLE_DIR="C:\Users\derii\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache" RR_IDX_DIR=...\cacheidx rrtool world 1AF#1`.
