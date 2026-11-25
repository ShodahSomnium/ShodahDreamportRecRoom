# Dreamport: Rec Room - Dorm Room + core interactions (research)

Status: complete (2026-09-24). All sections filled; GUESS marks what is not in the data.

SP = `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad`
RR = `SP\rrtool\bin\out\rrtool.exe`; bundle names are file names in
`E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64\<name>.bundle`.

## 0. Summary / quick reference

- **The Dorm Room can be rebuilt almost exactly.** The 47 `(Dorm_*)` Maker-Pen pieces (bundle
  `43ea1176ca25c0333547813ec3298808`) store their meshes in the **original dorm's room coordinates**; placing each root
  at the inverse of its single child's local transform reassembles the room (table 3.2, exact numbers). Room: 9.7 m
  (x) x 5.9 m (z), 6.3 m high with a sloped ceiling on the +x side, a 2.3 m deep **loft** at the -x end (floor 2.75 m,
  railing, ladder gap), a **skylight**, a **large slanted window** at the +x end, a **round-arched window** high in the
  front wall, a pillar box in the back-right corner, wall stripe and skirting.
- Architecture pieces render with `AG/MakerPenShape` (serialized colour = orange, no textures; Rec Room recolours at
  runtime from palettes that are not in the bundles) -> **we assign colours** (2.1). Furniture pieces keep their own
  textured materials (`AG/Standard-Opaque-Instanced` recolour textures), which render correctly.
- Furniture positions are NOT in the bundles (the level is encrypted) except one data point: the basketball
  rim VFX prefabs were saved at the rim position **(3.905, 1.097, -1.090)** = hoop on the pillar face above a bin.
  The rest of the layout (3.3) is a reasoned GUESS from free wall space, the wiki's object list and the light shafts.
- Real dorm contents (wiki, 2018 remodel): mirror with buttons (Customize / Outfits / Store / Dorm Skins / RR+ /
  Custom Clothing), loft bed with desk + radio, dresser, community/Play board, help buttons, two social posters,
  basketball hoop over a dustbin, ladder to the loft, armchair, plants, easel/canvas on the loft, campus map, exit door
  to the Rec Center.
- Core interaction data (section 4) was tallied over **every instance in all bundles**: Tool defaults
  (ThrowSpeedMultiplier 1, no max throw speed; per-object clamps listed), non-VR throw configs (generic 8 m/s,
  basketball 12 m/s lob curve), CollisionAudio mappings (layers, curves, instance limits), BreakableObject
  (shatter at 2 m/s), DrinkableBottle/Potion/ShakeAudio (root beer: swallow every 0.68 s, 5 swallows), 88 food items
  (bites, intervals, mouth distance, clips), basketball physics (mass 2, bounciness 0.8, rim 0.05, board 0.45, DingDing
  on a basket), AnimateInOut (0.2 s offsets, `[VFX_ObjectSpawn]`), ToolCleanup (0.1 m / 30 deg / 3 m / 15 s).
- Every clip is given with its bundle and pathId; the playback formula is in 4.10.
- Not in the bundles: paper airplane, water balloon, dartboard (a `(Dart)` exists), poker chips prefab (mesh only).

## 1. The real Dorm Room (web research)

Sources: Rec Room fandom wiki "Dorm Room" page (fetched through the MediaWiki API
`https://rec-room.fandom.com/api.php?action=parse&page=Dorm_Room&prop=wikitext&format=json`, saved in
`SP\research\dorm\wiki\Dorm_Room.json`), Steam news for app 471710 (all 564 items, saved in
`SP\research\dorm\wiki\steamnews.json`), web search summaries. Summarised in our own words.

### 1.1 What it is
- The player's private home room: where you spawn on login (unless a club clubhouse is set), where you change your
  avatar at the mirror, and the start of the path out to the Rec Center. Up to 3 invited players can join (since
  Jan 2019 "Social Dorm"). Juniors cannot save it. Since Jan 2019 it is a normal Maker-Pen room: every default object
  can be moved/deleted and the state saved ("This Room > Save"); "Safe Mode" reloads the default dorm.
- Current look = the **"Spring Cleaning" remodel of 27 Apr 2018**: "more spacious layout and a larger mirror". This is
  the room the Maker Pen "Dorm_*" kit (section 2) reproduces: a rectangular room with a **loft (mezzanine) reached by
  a ladder**, angled (sloped) ceiling parts with a **skylight**, **large + small windows** with light shafts, wall
  stripe, floor trims, fluorescent ceiling light, rug.
- History: Jun 2016 split Starter Room + Changing Room; Jul 2016 merged into the Dorm (with a "Zero-G Wardrobe");
  Aug 2016 bulletin board; Oct 2017 computer replaced by a comfy chair; Feb 2018 bulletin board revamp; Apr 2018
  remodel; Nov 2018 the old dorm board replaced by the Community Board (also placed in the Rec Center); 2019 the
  dresser drawers stopped being the clothes UI (Sep 2019 "Access your clothes from the dorm mirror instead of the
  drawers"); Apr 2021 the dorm mirror became the "Mirror" prop; Jul 2023 the Rec Center door became the
  "Room-i-verse" door (opens the Play menu) and the community board became a "Play" board; Sep 2025 the door once
  again defaults to the Rec Center (option in the watch to switch back).

### 1.2 Default contents and what each does

| object | behaviour in Rec Room | our equivalent (kit piece, section 2) |
| --- | --- | --- |
| **Mirror** (large, wall-mounted) | real-time reflection; buttons beside it: *Customize* (skin, face, eyes/mouth, ears, body, hair + colour/dyes, facial hair, shirts, accessories, hats, gloves), *Dorm Skins*, *Rec Room+*, *Outfits* (save/load outfits), *Custom Clothing/Items*. Earlier (2018-2019) also face controls and "Get More Clothes"/"Saved Outfits" | `(Dorm_FreeMirror)` + `(Dorm_Mirror_Buttons)`. In Dreamport the Somnium avatar is NOT modified: the mirror is a reflection only; buttons can open our own UI (e.g. Dreamport settings) or be decorative |
| **Bed with a desk below it** (loft bed) | furniture, sit/lie; radio and other props on the desk | `(Dorm_Bunk_Bed)` (mesh `BunkBed`: mattress top 2.15 m, desk top 0.85 m, drawer cabinet under one end) |
| **Radio on the desk** | tap to cycle music | `(Radio)` prefab (see section 5) |
| **Dresser** | before Sep 2019 its drawers were the clothes browser; now furniture | `(Dorm_Dresser)` (DresserFrame + 12 DresserDrawerN + 2 DresserDoorN meshes: drawers/doors can open) |
| **Board** (Community Board, later Play board) | big screen showing featured rooms/creators/videos; later the Play menu | `(Dorm_Community_Board)` (mesh BulletinBoard) + UI prefab `(CommunityBoard)_DormRoom` in bundle 6d3223da... ; for us: an activity board listing the Dreamport activities (teleport targets) |
| **Code of Conduct + help buttons** | buttons open Privacy Policy / Help Center | `(Dorm_Help_Buttons)` (decorative or open our help page) + `(Sandbox_CodeOfConduct_Poster)` key `760` |
| **Two social posters** | Block/Mute and Friends features explained | `(Sandbox_MuteGesture_Poster)` key `ef` + `(Sandbox_ShakeHands_Poster)` key `b8f` (mesh `Poster_Dorm`, section 5) |
| **Basketball hoop above a dustbin** | shoot the basketball into the hoop; the ball is a normal throwable | `(Basketballhoop)` + `(Basketball)` + a bin such as `(TrashCan_Office)SandboxVariant` |
| **Ladder to the second floor (loft)** | climb up to the loft | `(Dorm_Ladder)` (mesh `LadderShort`), `(Dorm_Loft_Floor)`, `(Dorm_Loft_Railing)`, `(Dorm_Loft_Beam)`, `(Dorm_Loft_Shelves)` |
| **Armchair and plants** | decoration | not in the Dorm kit: `(Sandbox_CouchChair)` key `f0` (armchair with a seat) + houseplants `cb6`/`ce6`/`ddd` (section 5) |
| **Art supplies and a canvas** (loft) | decoration / paint | `(ArtCanvas)` key `d8c` (drawable canvas) + `(Sandbox_Paintbrush)` key `26` (section 5) |
| **Campus Map** | wall map of the Rec Room campus (the wiki has an interactive version listing the rooms: Rec Center, Paintball, Laser Tag, Quests, Disc Golf, Dodgeball, Soccer, Charades, Bowling, Lounge...) | `(Dorm_Campus_Map)` (mesh `CampusMap`) |
| **Exit door** | leads to the Rec Center (2016-2023 and again since Sep 2025) or opens the Play menu (2023-2025) | door prefabs already researched: `SP\research\dumps\roomdoor.txt`, `roomdoorv2.txt`, `magicdoor.txt` (see section 3) |
| **Windows, skylight, light shafts, fluorescent light, rug, stripe, trims** | architecture | the `Dorm_*` kit (section 2) |
| **Drink bottles / basketball / props** | spawnable toys (Jan 2019: bottles and basketballs need "Manipulate" in Maker mode) | section 4 |

- Tutorial VO in the bundles (can be reused as ambience/help, optional): `AmyVO_NUX16_ThisIsYourDormRoom`
  (AudioClip, bundle 063b6b6653aa642616fd21d3d4701899, pathId 5182198265861126878) and
  `BulletinBoard_AtTheCommunityBoard` (same bundle, pathId -7940090562521384743), with `_subtitles` TextAssets.
- "HoloHelpers" (little hint figures) greeted new players in the dorm from Jul 2019.
- Size: no published dimensions. The kit's floor piece is **9.80 m x 6.05 m** (mesh `Dorm_18_FlatFloor`), which we
  treat as the room's footprint (section 3).

## 2. Dorm kit in the bundles (every piece)

All 47 Dorm pieces are prefab roots in **bundle `43ea1176ca25c0333547813ec3298808`** (the big Maker-Pen/sandbox
bundle, same one as `(Basketball)` 'd2'). Load with `bundle.LoadAsset<GameObject>(key)`; key = container key below.
Research dumps: `SP\research\dorm\raw\<piece>.txt` (full `rrtool tree --fields --all`), compact
`SP\research\dorm\pc\<piece>.txt`, summary `SP\research\dorm\summary.txt`. Mesh vertex dumps (our own reader, see
6.3): `SP\research\dorm\verts\<meshPathId>.txt`.

### 2.1 What every piece has (common, all inert for us)
- Root (layer 10): `Tool` (CanBePickedUp 1, CanBeEquipped 1 - irrelevant, kinematic), `CreationObject`,
  `NetworkTransform`, `RigidbodyEx`, **`Rigidbody` mass 1, drag 0, angDrag 0.05, isKinematic TRUE**, `PersistenceView`,
  `SpawnableTool`, `ConnectableTool`, `ConnectionPoint`, `TaggedTool`, `ToolEntity`, `VisibleInCamera`,
  `KinematicRigidbodyPickup` (interpolateOnPickup 1), `ToolCleanup`, `AnimateInOut`, `LocomotionSettings`
  (SupportsClimbing 0, SupportsClambering 1, SupportsWallRunning 1), `EmbodiedEntity`, `SandboxColorable`,
  `RRNetworkView`, `PropContainerProxy`; plus a child `PropContainer` (layer 0) with `Spawn Point`/`Tether Point`
  (Maker Pen control-panel anchors; ignore). Architecture pieces also have `RecRoomAIObjectNameOverride`
  ("Wall", "Floor Trim", "Window Frame", "Fluorescent Fixture").
- **For Dreamport:** keep the Transform tree, MeshFilter/MeshRenderer/LODGroup and colliders; keep the root
  Rigidbody kinematic or remove it (static geometry); reassign layers (Rec Room layers 10/11/27/25/0 -> our static
  environment layer; trigger boxes such as `ExtraPadding` -> Ignore Raycast or remove).
- **Architecture pieces (walls, floor, ceilings, windows, trims, loft) render with `AG-MakerPenShape`**
  (Material pathId -224598426847956790, shader `AG/MakerPenShape` in bundle b1a0b840...). Its serialized
  `_BaseColor` is **orange rgba(1,0.584,0,1)** and it has NO textures: Rec Room recolours it at runtime from
  `SandboxColorable` (a colour index + a "material" index into Maker-Pen palettes that are NOT in the bundles). The
  feasibility render `SP\research\feasibility_png\editor_dorm_wall1_original_back.png` shows exactly that orange.
  => we assign colours ourselves (a material copy per colour with `_BaseColor` set, or URP/Lit).
  `AG/MakerPenShape` has **no DepthNormals pass**: apply the renderQueue 2501 fix from `rr_runtime_facts.md`, or
  simply replace it with URP/Lit (recommended for these untextured surfaces).
- **Walls, floor and ceilings are single-sided planes** (e.g. `Dorm_05` extent z = 0) facing the room interior:
  invisible from outside (`editor_dorm_wall1_original.png` is empty from the front camera). Vertex normals checked: left
  wall +x, back +z, right -x, front -z, floor +y (all point into the room). The box colliders give
  them 0.32-0.37 m thickness on the outside.

| SandboxColorable defaults (UseDefaultColor=1) | colour idx | material idx | our colour (GUESS: palette not in bundles) |
| --- | --- | --- | --- |
| Wall_* (all), Ceiling_Flat/Angled, Wall_Pillar, Loft_Railing | 18 | 9 | pale blue-grey `#A9BFD0` (ceiling a bit lighter `#C9D6E0`) |
| Window_* frames/panes, Skylight_*, Loft_Shelves | 18 | none (UseDefaultMaterial 0) | white `#F2F2F2` |
| Floor, Loft_Floor | 17 | 13 (GUESS: wood planks) | warm wood `#A8744A` |
| Floor_Trim* | 15 | 9 | white `#EDEDED` |
| Loft_Beam | 16 | 12 (GUESS: wood) | dark wood `#6E4A30` |
| Wall_Stripe | none | none | white `#F5F5F5` |
| FreeMirror frame | 12 (material not forced) | - | keep its own painted-wood material |
| Community_Board | 11 (UseDefaultColor 0) | - | keep its textured material |

Optional textures that exist in the same bundle (implicit assets: resident only if a loaded container references
them; otherwise build URP/Lit and skip): `Plaster_tex` #2373076608914493128, `Plaster_norm` #1943999921295244735,
`Plaster_spec` #3816979310244588910 (used by `DormRoom_PlasterWalls_Black_mat` #-7563122068927013931, tiling 15x15);
`Wood FloorTiled_tex` #-1969926788222739412, `Wood FloorTiled_Norm` #-9018188389256569548, `Wood FloorTiled_spec`
#-4649383284996979614 (used by `Wood_Floor_Matte_mat` #-541357418168945416, tiling 10x40).

### 2.2 Piece table
Sizes are the mesh `m_LocalAABB` (size = 2 x extent, c = centre) in the mesh's own space. **For all architecture
pieces the mesh space IS the dorm's room frame** (section 3), so these numbers are room coordinates.
Colliders: count x type ("trig" = trigger). Layers = layers present in the prefab.

| prefab root | pathId | key | mesh(es): name#pathId (verts) size, c(centre) | material [shader] | colliders | layers |
| --- | --- | --- | --- | --- | --- | --- |
| `(Dorm_Bunk_Bed)` | -1361775792136029656 | `bf6` | BunkBed#7721204093867968022 (1547v) size 2.49x2.50x1.42 c(0.00,1.25,0.00);  | DormRoom_BunkBed_Recolor_Mat [AG/Standard-Opaque-Instanced];  | 14xBox  | L0 L10 L11  |
| `(Dorm_Campus_Map)` | 9185056585606946301 | `04e` | CampusMap#3197069287915308566 (104v) size 1.49x0.96x0.05 c(0.00,-0.00,-0.00);  | DormRoom_CampusMap_Recolor_mat [AG/Standard-Opaque-Instanced];  | 1xBox  | L0 L10 L11  |
| `(Dorm_Ceiling_Angled)` | 7485103110733401143 | `846` | Dorm_10#-354718953147034927 (34v) size 3.65x2.10x6.05 c(2.97,5.26,0.03);  | AG-MakerPenShape [AG/MakerPenShape];  | 4xBox  | L0 L10  |
| `(Dorm_Ceiling_Flat)` | 762056056874311417 | `8d` | Dorm_01#5155920357944627921 (96v) size 6.27x0.41x6.01 c(-1.86,6.32,0.01);  | AG-MakerPenShape [AG/MakerPenShape];  | 8xBox  | L0 L10  |
| `(Dorm_Community_Board)` | -5196424665139789200 | `beb` | BulletinBoard#364151924371998230 (120v) size 0.10x1.81x3.05 c(-0.00,0.00,0.00);  | DormRoom_BulletinBoard_Recolor_Mat [AG/Standard-Opaque-Instanced];  | 1xBox 1xBox(trig)  | L0 L10 L25  |
| `(Dorm_Desk_Lamp)` | -3414330160182303774 | `838` | Desk_Lamp_LOD2#-9117980729550830991 (230v) size 0.16x0.48x0.35 c(0.00,0.23,0.05); Desk_Lamp_LOD0#2429946975983239793 (912v) size 0.17x0.48x0.37 c(-0.00,0.23,0.06); Desk_Lamp_LOD1#398240752195732081 (576v) size 0.17x0.48x0.37 c(-0.00,0.23,0.06); Desk_Lamp_Bulb_LOD0#7532778962007163505 (289v) size 0.09x0.16x0.10 c(0.00,0.30,0.15); Desk_Lamp_Bulb_LOD1#7755764230245646961 (137v) size 0.09x0.16x0.10 c(0.00,0.30,0.15);  | DeskLamp_Recolor_mat [AG/Standard-Opaque-Instanced]; DeskLamp_Bulb_Emit_mat [AG/Standard-Opaque-Emission];  | 4xBox  | L0 L10 L11  |
| `(Dorm_Dresser)` | -5078137549051442321 | `15c` | DresserDrawer10#-2257847184135961066 (312v) size 0.66x0.25x0.48 c(0.00,-0.00,0.02); DresserDrawer11#-2408923348584053226 (312v) size 0.66x0.25x0.48 c(0.00,0.00,0.02); DresserDoor2#-2623315243833113066 (256v) size 0.33x1.16x0.05 c(-0.17,-0.00,-0.08); DresserFrame#-2879170611540933098 (200v) size 1.38x1.81x0.46 c(0.00,0.90,-0.03); DresserDrawer7#-2893508467148470762 (312v) size 0.66x0.19x0.48 c(0.00,-0.00,0.02); DresserDrawer9#-5937569000062532074 (312v) size 0.66x0.25x0.48 c(0.00,0.00,0.02); DresserDrawer6#-7018602538192091626 (312v) size 0.66x0.19x0.48 c(0.00,-0.00,0.02); DresserDrawer3#-8467619264981449194 (298v) size 0.33x0.21x0.48 c(0.00,0.00,0.15); DresserDrawer8#-8707512109236828650 (312v) size 0.66x0.19x0.48 c(0.00,-0.00,0.02); DresserDrawer2#15925336381769238 (298v) size 0.33x0.21x0.48 c(0.00,0.00,0.15); DresserDrawer4#2726789981015229974 (298v) size 0.33x0.21x0.48 c(0.00,0.00,0.15); DresserDrawer5#2894865980551775766 (312v) size 0.66x0.19x0.48 c(0.00,-0.00,0.02); DresserDoor1#4096319222238463510 (256v) size 0.33x1.16x0.05 c(0.17,0.00,-0.08); DresserDrawer12#5846549536879068694 (312v) size 0.66x0.25x0.48 c(0.00,0.00,0.02); DresserDrawer1#9106353411804218902 (298v) size 0.33x0.21x0.48 c(0.00,0.00,0.15);  | DormRoom_Ladder_Recolor_Mat [AG/Standard-Opaque-Instanced];  | 1xBox  | L0 L10  |
| `(Dorm_Floor)` | -7474360954332800794 | `62b` | Dorm_18_FlatFloor#-4284638933954137673 (70v) size 9.80x0.00x6.05 c(-0.09,-0.00,0.02);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Floor_Trim)` | -9158584040461197265 | `524` | Dorm_03#7435456235104867025 (180v) size 9.71x0.21x5.92 c(-0.09,0.03,0.00);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xMeshCol  | L0 L10  |
| `(Dorm_Floor_Trim_1)` | -3012212464166505291 | `a47` | Dorm_Trim_01#2405385586213974455 (20v) size 0.03x0.16x4.40 c(4.75,0.06,0.76);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xMeshCol  | L0 L10 L27  |
| `(Dorm_Floor_Trim_2)` | 6338962389890778269 | `deb` | Dorm_Trim_02#-85373521559162441 (32v) size 1.66x0.16x1.55 c(3.94,0.05,-2.18);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xMeshCol  | L0 L10 L27  |
| `(Dorm_Floor_Trim_3)` | 4240025618774585472 | `36` | Dorm_Trim_03#110903292303045047 (20v) size 5.71x0.19x0.03 c(0.28,0.04,-2.95);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xMeshCol  | L0 L10 L27  |
| `(Dorm_Floor_Trim_4)` | 3012543573101144682 | `f9c` | Dorm_Trim_04#6833496798333825463 (28v) size 2.40x0.21x0.26 c(-3.74,0.03,-2.83);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xMeshCol  | L0 L10 L27  |
| `(Dorm_Floor_Trim_5)` | -5694760226513819443 | `5ea` | Dorm_Trim_05#3463899665774211511 (28v) size 0.03x0.21x5.46 c(-4.92,0.03,-0.00);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xMeshCol  | L0 L10 L27  |
| `(Dorm_Floor_Trim_6)` | -7314356806641032990 | `e1b` | Dorm_Trim_06#-5608396595070373449 (28v) size 2.40x0.21x0.26 c(-3.74,0.03,2.83);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xMeshCol  | L0 L10 L27  |
| `(Dorm_Floor_Trim_7)` | -4062224151422903469 | `f4` | Dorm_Trim_07#-2805839093727235657 (24v) size 7.34x0.19x0.03 c(1.10,0.04,2.95);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xMeshCol  | L0 L10 L27  |
| `(Dorm_Fluorescent_Light)` | -6218679449498342124 | `b1` | DormRoom_Flourecent_Light_Base#2341204314138278312 (80v) size 0.31x0.08x1.48 c(0.00,-0.04,-0.00); DormRoom_Flourecent_Light#5889158887510047144 (48v) size 0.31x0.03x1.45 c(0.00,-0.09,0.00);  | Light_Fluorescent_mat [AG/Standard-Opaque]; DormRoom_Light_Fluorescent_mat [AG/Standard-Opaque-Instanced];  | 1xBox 2xMeshCol  | L0 L10  |
| `(Dorm_FreeMirror)` | -6428987453455604450 | `ded` | Frame#-4842667125005389059 (72v) size 2.12x2.29x0.03 c(0.00,-0.00,-0.00); MirrorProp#772694226442482429 (4v) size 2.04x2.21x0.00 c(0.01,-0.00,-0.00);  | Wood_VertColor_Painted_Horizontal_Emit_Mat [AG/Standard-Opaque]; Mirror_Dorm_Mat [AG/Mirror];  | 2xBox 1xBox(trig)  | L0 L10  |
| `(Dorm_Help_Buttons)` | -6358360931054609893 | `0ed` |  | Seat_InvisibleOutline_Mat [AG/DoNotRender]; UGC_MakerPen_Holographic_InteractionVolume_Mat [Particles/HolographicDistanceFadeWorldSpace];  | 4xBox(trig)  | L10 L27 L5  |
| `(Dorm_Ladder)` | -2022675129606423973 | `be` | LadderShort#-8339096806702201322 (240v) size 0.97x2.66x0.48 c(0.45,1.32,-0.00);  | DormRoom_Ladder_Recolor_Mat [AG/Standard-Opaque-Instanced];  | 3xBox  | L0 L10 L11  |
| `(Dorm_Light_Shafts)` | 6898921893816917418 | `583` | DormRoom_Light_Shaft#-4092139463772574716 (40v) size 8.93x6.45x3.66 c(0.25,3.22,1.04); DormRoom_LightShaft_Highlight#-7168587578605802492 (100v) size 8.66x4.92x3.00 c(0.54,5.25,0.78);  | DormRoom_LightShaft_Gold_Mat [AG/Unlit-Additive-Soft]; UGC_MakerPen_Holographic_InteractionVolume_Mat [Particles/HolographicDistanceFadeWorldSpace];  | 1xMeshCol  | L0 L10 L27  |
| `(Dorm_Loft_Beam)` | 8278684845839305457 | `da7` | Dorm_19#8242340523667854033 (98v) size 0.17x0.26x5.54 c(-2.71,2.52,0.02);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Loft_Floor)` | 277741531674452259 | `354` | Dorm_21#4823744652158866129 (99v) size 2.36x0.11x5.53 c(-3.81,2.70,0.01);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Loft_Railing)` | 4334405499154112664 | `463` | Dorm_20#9217110722779714257 (48v) size 0.17x0.77x3.67 c(-2.71,3.13,-0.89);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Loft_Shelves)` | -3021061792140504379 | `7ae` | Dorm_22#8249027726425625297 (146v) size 2.38x0.86x5.57 c(-3.79,3.91,-0.00);  | AG-MakerPenShape [AG/MakerPenShape];  | 4xBox  | L0 L10  |
| `(Dorm_Mirror_Buttons)` | -5360143280130445984 | `27be` |  | Seat_InvisibleOutline_Mat [AG/DoNotRender]; UGC_MakerPen_Holographic_InteractionVolume_Mat [Particles/HolographicDistanceFadeWorldSpace];  | 1xBox(trig)  | L0 L10 L27 L5  |
| `(Dorm_Rug)` | -6345611455275284369 | `d4a` | RugLarge#7875126178138474006 (322v) size 3.76x0.11x2.23 c(0.00,0.00,-0.00);  | DormRoom_Rug_Recolor_mat [AG/Standard-Opaque-Instanced];  | 1xBox  | L0 L10 L11  |
| `(Dorm_Skylight_Frame)` | -6514425902962476994 | `a0` | Dorm_12#2434847682331972305 (88v) size 4.50x0.17x3.47 c(-1.79,6.51,0.77);  | AG-MakerPenShape [AG/MakerPenShape];  | 4xBox  | L0 L10  |
| `(Dorm_Skylight_Window)` | 2576217302369134522 | `e0a` | Dorm_11#-4996771515595189551 (280v) size 4.28x1.42x3.26 c(-1.79,7.17,0.77);  | AG-MakerPenShape [AG/MakerPenShape];  | 11xBox  | L0 L10  |
| `(Dorm_Wall_1)` | 402560699714458626 | `08e` | Dorm_02#4922131485602714321 (49v) size 0.00x6.30x5.56 c(-4.94,3.02,0.00);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_1_Down)` | -5748921536911183506 | `387` | Dorm_Mezzanine_01#2504675019960500825 (35v) size 0.00x2.85x5.56 c(-4.94,1.29,0.00);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_1_Up)` | -5838056398501447013 | `a4` | Dorm_Mezzanine_02#3719651129945768537 (21v) size 0.00x3.46x5.56 c(-4.94,4.44,0.00);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_2)` | -2640346907722580210 | `86c` | Dorm_05#1394361227549053649 (21v) size 5.79x6.41x0.00 c(0.32,3.15,-2.96);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_3)` | -7884117007244086810 | `ae` | Dorm_07#-1303047883665534255 (24v) size 0.00x4.30x4.56 c(4.77,2.13,0.78);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_4)` | -4651524958183908848 | `ef7` | Dorm_16#-3032138809261976879 (54v) size 7.43x6.38x0.00 c(1.09,3.13,2.96);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_Loft_1)` | -4123275525796253202 | `8` | Dorm_04#-5694326513629490479 (58v) size 2.42x6.60x0.27 c(-3.78,3.23,-2.86);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_Loft_1_Down)` | -7731633648264572245 | `d17` | Dorm_Mezzanine_03#5959019891061703257 (35v) size 2.42x2.81x0.27 c(-3.78,1.33,-2.86);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_Loft_1_Up)` | -8653145670191335242 | `a1` | Dorm_Mezzanine_04#2505518085516182105 (30v) size 2.42x3.79x0.27 c(-3.78,4.63,-2.86);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_Loft_2)` | 5193547481809726139 | `24` | Dorm_13#-3372831853024996655 (56v) size 2.41x6.60x0.27 c(-3.77,3.23,2.86);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_Loft_2_Down)` | -8824205714859525179 | `c4e` | Dorm_Mezzanine_05#-3085797616715021735 (35v) size 2.41x2.81x0.27 c(-3.77,1.33,2.86);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_Loft_2_Up)` | 2525196767370635915 | `ea` | Dorm_Mezzanine_06#6358996147171705433 (28v) size 2.41x3.79x0.27 c(-3.77,4.63,2.86);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_Pillar)` | -2285012182502353647 | `d3` | Dorm_06#-2946749362849017135 (56v) size 1.67x5.21x1.55 c(3.97,2.58,-2.22);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xBox  | L0 L10  |
| `(Dorm_Wall_Stripe)` | -6086241792445306834 | `8b` | Dorm_17#1728965476793029329 (58v) size 9.70x0.43x5.91 c(-0.09,1.65,-0.00);  | AG-MakerPenShape [AG/MakerPenShape];  | 6xBox  | L0 L10  |
| `(Dorm_Window_Large_Frame)` | 9019953174353428219 | `92` | Dorm_09#1008361336571990737 (250v) size 3.35x3.67x3.60 c(3.22,4.33,0.78);  | AG-MakerPenShape [AG/MakerPenShape];  | 6xBox  | L0 L10  |
| `(Dorm_Window_Large_Panes)` | 160704314681113999 | `3c8` | Dorm_08#-2652896353366863151 (424v) size 3.14x3.35x3.26 c(3.29,4.37,0.73);  | AG-MakerPenShape [AG/MakerPenShape];  | 11xBox  | L0 L10  |
| `(Dorm_Window_Small_Frame)` | -6727408037762825300 | `2f` | Dorm_14#3711014839404171985 (268v) size 3.38x2.27x0.29 c(-0.02,4.74,2.96);  | AG-MakerPenShape [AG/MakerPenShape];  | 1xMeshCol  | L0 L10  |
| `(Dorm_Window_Small_Panes)` | -5593635074577330071 | `6a` | Dorm_15#2712728749702877905 (444v) size 2.92x1.94x0.71 c(-0.02,4.74,2.74);  | AG-MakerPenShape [AG/MakerPenShape];  | 9xBox  | L0 L10  |

### 2.3 Per-piece behaviour notes
- `(Dorm_Bunk_Bed)`: a **loft bed with a desk and a drawer cabinet under it** (render
  `SP\research\feasibility_png\editor_dorm_bunkbed_original.png`: wooden frame, purple blanket, white pillow). Mesh is
  local-centred (x +-1.24, y 0..2.50, z +-0.71). 14 boxes on child `BunkBed` (layer 11): mattress deck
  2.40 x 0.20 x 1.15 at y 1.95-2.15 (mattress top 2.15), desk top 2.49 x 0.08 x 1.05 at y 0.77-0.85 (z -0.71..0.33),
  cabinet 0.62 x 0.83 x 0.96 at x -1.10..-0.48, four posts 0.15 x 2.50 x 0.15 at x +-1.17 / z +-0.64, end rails at
  y 0.81/1.83/2.33, long rails along z = -0.645 at y 1.82 and 2.34 (the guard-rail side = -z). ToolCollider
  kinematicBounciness 0.6. Material `DormRoom_BunkBed_Recolor_Mat` (`DormRoom_BunkBed_Recolor_Tex`
  #-8227882821412582565, `DormRoom_BunkBedDresser_Norm` #7273569938156420471, `DormRoom_BunkBedDresser_Spec`
  #-3973969132998701453).
- `(Dorm_Dresser)`: frame 1.38 x 1.81 x 0.46 on child `DresserFrame` rotated 180 deg Y (fronts face +z of the
  piece), 2 doors `DresserDoor1/2` (0.33 x 1.16 each, lower part y 0.62-1.78) and 12 drawers `DresserDrawer1..12`
  (1-4: 0.33 x 0.21 x 0.48 small top drawers; 5-12: 0.66 wide x 0.19-0.25 x 0.48), each with an `Interpolator` (movementSpeed 2 m/s, rotationSpeed 90 deg/s, ease-in over
  0.5 m / 80 %): the drawers slide out (pre-2019 clothes browser; names like `DresserDrawer5_hair`). One box
  (child `Collider`, scale 0.25) = 1.38 x 1.81 x 0.49. Material `DormRoom_Ladder_Recolor_Mat` (shared with the
  ladder; `Dorm_Ladder_Recolor_Tex` #-1706890334879588579).
- `(Dorm_FreeMirror)`: child `MirrorProp` (rot 180 deg Y) with mesh `MirrorProp` (quad 2.04 x 2.21, 4 verts) and
  child `Frame` (2.12 x 2.29 x 0.03, `Wood_VertColor_Painted_Horizontal_Emit_Mat`, `AG/Standard-Opaque`). Glass
  material `Mirror_Dorm_Mat` (shader `AG/Mirror`, `_BaseMap` = `Smude_Tex` #-6407946162672663824, a smudge overlay)
  is driven by Rec Room's `Mirror` script (fields: quad, disablePixelLights 1, maxAntiAliasing 1) which renders a
  reflection camera: **not usable** -> implement our own planar reflection on the same quad (mirrored camera into a
  RenderTexture, per eye in VR), or use a Somnium mirror component if the client has one.
  `ConfigurableCollision` with 3 options: `BoxCollider_Environment` (default, physicsMode 0, active),
  `BoxCollider_Decoration` (trigger, mode 1, inactive), `BoxCollider_Physical` (mode 2, inactive); box scale
  2.224 x 0.049 x 2.343 around the glass. Pivot = mirror centre (`Spawn Point` 1.265 above it).
- `(Dorm_Mirror_Buttons)`: invisible interaction volume (`DeformableShape` scale 0.332 x 1.96 x 0.067, holographic
  material, hidden when circuits are hidden) + world canvas `[MirrorUIRoot]` at (-0.835, 0.047, 0) rot 180 deg Y,
  canvas scale 0.002 (UGUI + Rec Room theming: renders grey without their scripts). Buttons (vertical layout,
  spacing 20 px, button min 338 x 200 px): **Customize, Outfits, Store, Dorm Skins, Rec Room +, Custom Clothing,
  Recommended for You, Try On What's Trending, Token Store**, plus a purchase reminder. Rebuild as our own panel.
- `(Dorm_Help_Buttons)`: same pattern, volume scale 0.213 x 0.886, canvas `[HelpButtonsCanvas]` (rot 180 Y, scale
  0.002, `HelpButtonsUGCController`): **Help, Privacy Policy, Terms of Service**.
- `(Dorm_Community_Board)`: child `BulletinBoard` (rot +90 deg Y) mesh 3.05 wide x 1.81 tall x 0.10 (framed board,
  `DormRoom_BulletinBoard_Recolor_Mat`) + child `CommunityBoardLoader` (isInDorm 1, dormRoomCommunityBoardReference =
  Addressables GUID `ebaf09cb713fbe74091d8960b5cda301`, i.e. the UI prefab `(CommunityBoard)_DormRoom`, bundle
  6d3223da354de646ab79d5660dcac9d2, container key `eba`, pathId 3274340961757224498: `BulletinBoardController` with
  panels `RoomsPanel` (world canvas scale 0.002 holding a `[WatchMenuRRUIRoot] CommunityBoard` browser), `WarningPanel`,
  `BannedPanel`; grey without Rec Room theming scripts - build our own board UI on the same 3.05 x 1.81 face) + trigger `ExtraPadding` 3.06 x 1.80 x 0.16
  (layer 25) in front of the face. The board face lies in the piece's XY plane (after the child's +90 deg); the
  screen side is +z (GUESS from the ExtraPadding box centre z +0.128).
- `(Dorm_Campus_Map)`: framed map 1.49 x 0.96 x 0.05 in the XY plane (`DormRoom_Map_Recolor_Tex`
  #-3489455609074429231, `DormRoom_Map_Spec` #7020897200689325696), one box.
- `(Dorm_Desk_Lamp)`: **dynamic** (Rigidbody NOT kinematic, mass 1, drag 0, `RigidbodyPickup`): a pick-up desk lamp
  0.17 x 0.48 x 0.37 (LOD0-2) + emissive bulb (`DeskLamp_Bulb_Emit_mat`, shader `AG/Standard-Opaque-Emission`,
  `_EmissionColor` (2,2,2)); 3 boxes (base + two arm segments). No Light component (add a small point light).
- `(Dorm_Fluorescent_Light)`: ceiling fixture 0.31 x 0.11 x 1.48, pivot at its top face (hangs down to y -0.11):
  base `DormRoom_Light_Fluorescent_mat`, tube `Light_Fluorescent_mat` (`AG/Standard-Opaque`, `_EmissivePower`
  3.51). No Light component: add our own (GUESS: warm-white light).
- `(Dorm_Ladder)`: mesh `LadderShort` (240 v) on a child whose rotation makes the ladder **vertical** in piece space
  (checked on the vertices): rails at x +-0.24, height 0..2.81, 0.06 thick (z). Boxes: `ClimbCollision`
  0.40 x 2.79 x 0.06 plus two `SideCollision` rails. Rec Room climbs by grabbing; for us: a climb volume or a
  "climb to loft" interaction (Somnium has no ladder system known - see 6).
- `(Dorm_Rug)`: mesh `RugLarge` scaled (0.707, 0.064, 0.707) -> 2.66 x 0.007 x 1.58; one flat box.
  `DormRoom_Rug_Recolor_mat` (`DormRoom_Rug_Recolor_Tex` #7094337601934623577, `Carpet_Burber_norm/Spec`).
- `(Dorm_Light_Shafts)`: additive god-rays `DormRoom_Light_Shaft` (40 v; room frame x -4.21..4.72, y 0..6.45,
  z -0.79..2.87), material `DormRoom_LightShaft_Gold_Mat` (shader `AG/Unlit-Additive-Soft`, `_TintColor`
  (0.272,0.155,0.058,0.578), `_InvFade` 1.55, texture `GradientHorizontal`@f124b399) + a selection mesh with a
  MeshCollider (remove it). Transparent, so no depth-priming issue.
- `(Dorm_Wall_Stripe)`: horizontal band 0.43 m high at y 1.43-1.86 (room frame) around all walls (58 v); 6 thin boxes.
- `(Dorm_Floor_Trim)` = the whole skirting board as one mesh (`Dorm_03`, 180 v, 0.21 high); `_1`..`_7` are the same
  skirting split per wall segment (use the whole one OR the parts). Non-convex MeshCollider.
- `*_Down` / `*_Up` wall variants: the same walls split at the loft floor height (y 2.72-2.74). Use the full wall OR
  the Down+Up pair, not both.

## 3. Dorm assembly plan (metres)

### 3.1 Key finding: the architecture pieces snap back into the original room
Every architecture piece is `root -> one child (mesh + usually its collider)`, and **the mesh vertices are stored
in the original dorm's coordinates** ("room frame"); Rec Room only added a child offset to re-centre each piece on
its pivot for the Maker Pen. Placing the root at the **inverse of that child's local transform** restores the
original room exactly (the resulting root poses are round numbers such as (-5,0,0) +90 deg or (0,0,3) 180 deg, which
confirms it). Formula (Unity): `root.localRotation = Quaternion.Inverse(child.localRotation);
root.localPosition = -(root.localRotation * child.localPosition)` (child scale is 1 for all of them).
Computed by `SP\research\dorm\place.pl` from the dumps; output `SP\research\dorm\placements.txt`.
**Implementation tip:** compute the root pose at runtime from the loaded prefab (exact values); the table below comes
from dumps that round rotations to 3 decimals, so it is off by up to ~5 mm (e.g. -4.998 for -5). The intended values
are clearly the round ones (x = +-5 or +-4, z = +-3, y = 6, rotations multiples of 90 deg; `Ceiling_Angled` ~30 deg
roof slope).

**Room frame** (all numbers below, metres, Unity axes, relative to a `DormRoot` transform you place anywhere):
- y = 0 is the floor top. Interior: **x -4.94 .. +4.77 (9.7 m long), z -2.96 .. +2.96 (5.9 m wide)**.
- **-x end = loft end**: mezzanine x -4.94..-2.63 (2.3 m deep), floor top **y 2.75**, underside 2.64; its side walls
  are inset to z +-2.72 (0.27 m thick loft walls). Railing on the loft edge (x -2.72) from z -2.73 to +0.94; the
  **gap z 0.94..2.72 is the ladder opening**. Loft shelves on the loft walls at y 3.48-4.34.
- **Ceiling**: flat at ~y 6.17-6.35 for x < 1.2 (contains the **skylight** opening x -4.04..0.46, z -0.97..2.50);
  for x > 1.2 it **slopes down** to y 4.28 at the +x wall (`Dorm_10`).
- **+x end = window end**: the right wall (x 4.77, z -1.50..3.06, height 4.28) has an opening z -0.945..2.449 above
  y 2.53 holding the **large slanted window** (`Window_Large_*`: glazing from (x 4.86, y 2.70) up to (x 1.72,
  y 6.04), width z -0.90..2.36). Back-right corner: a **pillar/chimney box** x 3.14..4.81, z -2.99..-1.44, 5.18 high.
- **Back wall** (z -2.96, x -2.58..3.21, plain, top 6.35 sloping to 5.2) and **front wall** (z +2.96,
  x -2.62..4.81) with a **round-arched window** x -1.57..1.53, sill y 3.65, arch top y 5.80 (`Window_Small_*`).
  Left wall x -4.94 (full height 6.17), plain.
- Sunlight: the light-shaft mesh shows the sun entering through the large window and the skylight, travelling
  roughly along **(-0.68, -0.73, 0)** (from +x, ~47 deg high); the window shafts hit the floor between x -3.8 and
  2.9, z -0.7..2.9; the skylight shaft lands on the loft floor. Use a directional light with that direction (or a
  baked/fake light) so the god-ray meshes match.
- Nothing in the kit is a door: there is **no door opening** in any wall mesh (walls are solid except the two
  window holes). Rec Room doors are wall-mounted props (see 3.3).

### 3.2 Architecture placements (exact, from the data)
Rotation = quaternion (x, y, z, w). "Use" says which variant set to instantiate (do not double up).

| piece (key) | root position | root rotation | resulting room-frame AABB of the mesh | use |
| --- | --- | --- | --- | --- |
| `(Dorm_Floor)` (62b) | (0, 0, 0) | (0,0,0,1) | x -4.99..4.81, y 0, z -3.01..3.04 | yes |
| `(Dorm_Floor_Trim)` (524) | (-0.080, 0.061, 0) | (0,0,0,1) | skirting all round, y -0.07..0.13 | yes (or `_1`..`_7`) |
| `(Dorm_Floor_Trim_1..7)` (a47, deb, 36, f9c, 5ea, e1b, f4) | (-0.080, 0.061, 0) each | (0,0,0,1) | per-wall skirting segments | alternative |
| `(Dorm_Wall_1)` (08e) left wall | (-4.998, 0, -0.002) | (0, 0.7071, 0, 0.7071) | x -4.94, y -0.13..6.17, z -2.78..2.78 | yes (or `_Down` 387 + `_Up` a4, same pose) |
| `(Dorm_Wall_2)` (86c) back wall | (0, 0, 0) | (0,0,0,1) | x -2.58..3.21, y -0.06..6.35, z -2.96 | yes |
| `(Dorm_Wall_3)` (ae) right wall | (4.998, 0, -0.002) | (0, -0.7071, 0, 0.7071) | x 4.77, y -0.02..4.28, z -1.50..3.06 (window hole) | yes |
| `(Dorm_Wall_4)` (ef7) front wall | (0, 0, 3.000) | (0, -1, 0, 0) (= 180 deg Y) | x -2.62..4.81, y -0.06..6.32, z 2.96 (arch hole) | yes |
| `(Dorm_Wall_Loft_1)` (8) loft back wall | (-4.000, 0, -3.000) | (0,0,0,1) | x -4.99..-2.57, y -0.07..6.53, z -2.99..-2.72 | yes (or `_Down` d17 + `_Up` a1) |
| `(Dorm_Wall_Loft_2)` (24) loft front wall | (-4.000, 0, 3.000) | (0, -1, 0, 0) | x -4.98..-2.57, y -0.07..6.53, z 2.72..3.00 | yes (or `_Down` c4e + `_Up` ea) |
| `(Dorm_Wall_Pillar)` (d3) | (0, 0, 0) | (0,0,0,1) | x 3.14..4.81, y -0.03..5.18, z -2.99..-1.44 | yes |
| `(Dorm_Wall_Stripe)` (8b) | (0, 1.350, 0) | (0,0,0,1) | band y 1.43..1.86 on all walls | yes |
| `(Dorm_Ceiling_Flat)` (8d) | (-2.000, 5.998, -0.002) | (0.7071, 0, 0, 0.7071) | x -5.00..1.27, y 6.11..6.52, z -2.99..3.02 (skylight hole) | yes |
| `(Dorm_Ceiling_Angled)` (846) | (2.991, 5.200, -0.002) | (0.354, -0.612, 0.354, 0.612) | x 1.15..4.79, y 4.21..6.31, z -2.99..3.06 | yes |
| `(Dorm_Skylight_Frame)` (a0) | (-1.750, 6.498, 0.798) | (0.7071, 0, 0, 0.7071) | x -4.04..0.46, y 6.42..6.60, z -0.97..2.50 | yes |
| `(Dorm_Skylight_Window)` (e0a) | (-1.800, 7.000, 0.800) | (0,0,0,1) | x -3.93..0.35, y 6.46..7.88, z -0.86..2.40 | yes |
| `(Dorm_Window_Large_Frame)` (92) | (2.999, 2.500, 0.699) | (0, -0.7071, 0, 0.7071) | x 1.54..4.89, y 2.50..6.17, z -1.02..2.58 | yes |
| `(Dorm_Window_Large_Panes)` (3c8) | (3.530, 4.000, 0.760) | (0,0,0,1) | x 1.72..4.86, y 2.70..6.04, z -0.90..2.36 | yes |
| `(Dorm_Window_Small_Frame)` (2f) | (0, 3.600, 3.000) | (0, -1, 0, 0) | x -1.71..1.66, y 3.60..5.87, z 2.81..3.10 | yes |
| `(Dorm_Window_Small_Panes)` (6a) | (0, 3.800, 3.000) | (0, -1, 0, 0) | x -1.48..1.44, y 3.77..5.71, z 2.38..3.09 | yes |
| `(Dorm_Loft_Floor)` (354) | (-3.805, 2.696, 0.001) | (0, 0.7071, 0, 0.7071) | x -4.99..-2.63, y 2.64..2.75, z -2.76..2.77 | yes |
| `(Dorm_Loft_Beam)` (da7) | (-2.719, 2.500, -0.001) | (0, 0.7071, 0, 0.7071) | x -2.80..-2.63, y 2.39..2.65, z -2.75..2.79 | yes |
| `(Dorm_Loft_Railing)` (463) | (-2.710, 3.083, -0.878) | (0, 0.7071, 0, 0.7071) | x -2.80..-2.63, y 2.74..3.52, z -2.73..0.94 | yes |
| `(Dorm_Loft_Shelves)` (7ae) | (-3.999, 3.500, -0.001) | (0, 0.7071, 0, 0.7071) | x -4.98..-2.60, y 3.48..4.34, z -2.79..2.79 | yes |
| `(Dorm_Light_Shafts)` (583) | (0, 0, 0) | (0,0,0,1) | x -4.21..4.72, y 0..6.45, z -0.79..2.87 | yes (remove its selection MeshCollider) |

Pane meshes: the windows have no glass material of their own (MakerPenShape = the muntins/frames only, GUESS).
Behind the openings put a sky/campus backdrop (the world outside is Somnium's; a simple skybox-coloured quad or the
Somnium sky is fine) - GUESS.

### 3.3 Furniture and props (DESIGN - the real positions are not in the bundles; GUESS, tweakable)
Convention measured on the wall-mounted pieces (canvases rotated 180, board padding at +z): a wall prop's **front
faces +z of its root**. (Confirmed on the mirror: the `MirrorProp` quad normal is -z in mesh space, the child is
rotated 180 deg, so the glass faces +z.) So: on the back wall (normal +z) rotation = identity; on the front wall (normal -z)
rotation = 180 deg Y (0,1,0,0); on the left wall (normal +x) = +90 deg Y (0,0.7071,0,0.7071); on the right wall /
pillar-side (normal -x) = -90 deg Y (0,-0.7071,0,0.7071).

| object (bundle 43ea1176... unless noted, key) | root position | rotation (Y deg) | reasoning |
| --- | --- | --- | --- |
| `(Dorm_FreeMirror)` (ded) | (-0.40, 1.35, -2.92) | 0 | big full-length mirror on the plain back wall facing the spawn (bottom edge y 0.21, top 2.49) |
| `(Dorm_Mirror_Buttons)` (27be) volume / our button panel | (1.05, 1.35, -2.92) | 0 | right of the mirror (our own UI: Dreamport settings, not avatar editing) |
| `(Dorm_Help_Buttons)` (0ed) | (-1.95, 1.35, -2.92) | 0 | left of the mirror |
| `(Dorm_Campus_Map)` (04e) | (2.30, 1.60, -2.93) | 0 | remaining back-wall space before the pillar (x 1.55..3.05) |
| `(Dorm_Community_Board)` (beb) | (0.00, 2.25, 2.90) | 180 | front wall under the arched window (y 1.35..3.16, sill 3.65); our activity board |
| exit door `(Sandbox_RoomDoorV2)` key `09fb` (frame 1.14 x 2.14 m, pivot at one side: mesh x -0.12..1.02) | (2.90, 0, 2.93) | 180 | front wall right of the board (frame then spans x 1.88..3.02); door to the Rec Center instance |
| `(Dorm_Bunk_Bed)` (bf6) | (4.04, 0, 0.55) | +90 (0,0.7071,0,0.7071) - check: guard rail (-z) must face -x | along the right wall under the large window (bed top 2.50 < window bottom 2.53) |
| `(Radio)` (key `f3`, pathId -7125182807003965041, boombox 0.13 x 0.43 x 0.31) on the desk | bed-local (0.55, 1.07, -0.25) (its mesh is centred on the pivot, half height 0.217) | bed +0 (GUESS) | "radio on the desk" |
| `(Dorm_Desk_Lamp)` (838) on the desk | bed-local (0.95, 0.86, 0.10) | bed +180 | desk lamp |
| `(Dorm_Dresser)` (15c) under the loft | (-4.70, 0, -1.40) | +90 | against the left wall in the under-loft nook (1.81 < 2.64 headroom) |
| `(Dorm_Ladder)` (be) | (-2.60, 0, 1.83) | +90 | vertical ladder in the railing gap, top 2.81 at loft floor 2.75 |
| `(Dorm_Rug)` (d4a) | (0.30, 0, 0.10) | 0 | centre of the main floor |
| `(Dorm_Fluorescent_Light)` (b1) x3 | (-3.80, 2.64, 0) under loft; (-1.20, 6.15, -1.95) and (-3.80, 6.15, -1.95) on the flat ceiling | 0 | lighting |
| `(Basketballhoop)` (key `78`, see 4.8) at scale **0.94** | (3.905, 1.41, -1.44) | 0 | **DATA-BACKED**: the prefab roots `[VFX_Basketball_PassedRim]` (pathId 9003561391953464342) and `[Sandbox_BasketballRim_VFX]` (-5887031207943934196) in bundle 43ea1176... were saved at **(3.905, 1.097, -1.090)** = the rim centre in the dorm's room frame: right in front of the pillar face (x 3.14..4.81, z -1.44). The hoop's rim sits (0, -0.335, +0.371) x scale from its pivot, so a backboard flush on the pillar face gives scale 0.94 and pivot (3.905, 1.41, -1.44); rim at y 1.10 = a low "trash-can hoop" |
| trash can `(TrashCan_Office)SandboxVariant` (key `cd8`, pathId -6713108123592849537, 0.46 dia x 0.47 high, dynamic RigidbodyPickup) | (3.905, 0, -1.10) | 0 | "hoop above a dustbin": directly under the rim |
| `(Basketball)` (d2) | (3.2, 0.13, -0.6) resting | - | near the hoop |
| drink bottles: the drinkable `(Consumables_RootBeer_Bottle)` (bundle 85515de19d04f3c4dfe704e6f02046e3, key `3e`, see 4.6) or the 6-pack `(RootBeerConsumableTool)` (key `b2`) | on the dresser top (y 1.81) / desk | - | "drink bottles" |
| armchair `(Sandbox_CouchChair)` (`f0`) + plant `(Sandbox_Houseplant_Fig)` (`cb6`) | under the loft (-3.6, 0, 1.2) and (-4.4, 0, 2.3) | +90 | "armchair and some plants" |
| `(ArtCanvas)` (`d8c`) + `(Sandbox_Paintbrush)` (`26`) | canvas standing on the loft floor at (-4.2, 3.52, -1.0) (centre; half height 0.77) facing +x into the room, brush on the loft shelf; GUESS | +90 | "art supplies and a canvas (second floor)" |
| **spawn point** | (0.30, 0, 0.60) facing -z (towards the mirror) | 180 | newcomers see themselves first |

Collision/locomotion: floors (`Dorm_Floor`, `Dorm_Loft_Floor`) walkable; everything else static colliders. The
loft is only reachable by the ladder (or by jumping from the bed deck at 2.15 m - Rec Room players did that).
Recommended for Dreamport: a ladder climb interaction that lerps the avatar to (-3.2, 2.75, 1.8) (top) or
(-2.2, 0, 1.8) (bottom).

## 4. Core interactions (exact configs)

How these were obtained: every instance of each script in all 2,787 bundles was dumped with our batch reader
(`SP\research\dorm\meshtool\bin\out\meshtool.exe many <depth>` reading `bundle<TAB>pathId` lines; outputs
`SP\research\dorm\many\<Script>.json`, one JSON per line) and tallied with `SP\research\dorm\stat.pl`. Counts below are
instance counts. All these scripts are **missing scripts at runtime** (see `rr_runtime_facts.md`): we read their data
with our parser (or hard-code the values below) and implement the behaviour ourselves.

Component model (how a Rec Room "tool" = grabbable object is built):
`Tool` (interaction/audio/render settings) + `Rigidbody` + `RigidbodyEx` (wrapper) + one pickup flavour
(`RigidbodyPickup` dynamic / `KinematicRigidbodyPickup` static / `StabilizedRigidbodyPickup` guns /
`PlungerRigidbodyPickup` weapon parts) + `ToolCollider` on every collider GameObject + `NetworkTransform` +
`AnimateInOut` (spawn/despawn effect) + `ToolCleanup` (auto-return) + optional behaviours (`CollisionAudio`,
`BreakableObject`, `DrinkableBottle`, `Potion`, `ShakeAudio`, `DisposableItem` = food, `MeleeWeapon`, `WeaponAudio`,
`Basketball`, `ToolTrail`, `ToolSkinMapper`...). Non-VR ("screen") behaviour is data-driven by ScriptableObject configs
referenced from `Tool.screenSettings.ScreenBehaviorConfig` (4.1.3).

### 4.1 Tool

#### 4.1.1 `interactionSettings` (1,058 instances)

| field | meaning (inferred) | values seen (count) |
| --- | --- | --- |
| CanBePickedUp | grabbable at all | 1 (842), 0 (216: furniture, doors, kit pieces) |
| CanBeEquipped | can be stored in the backpack/holster | 1 (921), 0 (137) |
| LockOnPickup | stays in hand when grip released (toggle-hold: guns, maker pen, frying pan) | 0 (960), 1 (98) |
| Handedness | 0 any hand, 1 = one specific (grenades, shields), 2 = two-handed/other (quest melee) | 0 (default) |
| Left/RightHandPickupTransform | fixed grip pose (Position + Rotation) relative to the tool; zero = grab where touched | mostly zero |
| FullBodyLeft/RightHandGripTransform | grip pose for full-body avatars | basketball (+-0.1257, 0.0069, 0.0071); bottle (+-0.0268, -0.0355, -0.0250) rot x 0.0563 |
| CustomVisualPickupOverrides + HandVisualPickupInfo {Animation (hash), AnimationBlend} | hand pose animation while holding | basketball -1572429817 / 0.5; bottle -763341925 / 0.375 |
| **ThrowSpeedMultiplier** | release velocity multiplier | **1 (about 1,040; 1,022 of them also unclamped)**; 0.66 (quest melee), 1.5 (grenades, snowball), 1.1 bowling, 1.25 mine, 0.75 EMP |
| **HasMaxThrowSpeed / MaxThrowSpeed** (m/s) | clamp of the release speed | **0 / 10 default (no clamp)**; clamps: 8 quest melee, 25 grenades & snowball, 3 food boxes (only HasMax 0), 5 game tables, 12 ping-pong ball, 15 dodgeball / bowling / snowball pile, 18 tennis ball / mine, 35 soccer ball |
| CanHolderHandClimb | holding it still lets the hand climb (Pushgun) | 0 |

#### 4.1.2 `collisionSettings`, `audioSettings`, `renderSettings`
- collisionSettings: `IgnoredColliders`/`ForceIncludeColliders` lists (usually empty),
  `DynamicKinematicCollisionsCanBeApplied` 0 (1 on basketball, snowball, RoomieTool), `CanApplyDynamicKinematicCollisions`
  1, `DynamicKinematicCollisionSpin` 180 or 0, `UseExistingToolColliderLayerForDefaultLayer` 0,
  `applyForceToRemoteToolsOnAttack` 0. Semantics GUESS: whether a held (kinematic, hand-driven) tool pushes dynamic
  props, and the random spin added to them.
- audioSettings (clip structs, see 4.10): `PickupAudio`, `ThrowAudio`, `DropAudio`, `ResetAudio`,
  `LocalAttackAppliedAudio`, `RemoteAttackAppliedAudio`. Most common values:
  - Pickup: `sfx_consumables_champagne_bottle_pickup01` (v1, 36x), `Goblin2_Potion_Pickup01` (bundle
    1df8e0efac4f496ebc58d7e6f86480c3, #5621140284017789701, v1, pv 0.119, 32x), `Consumable_Pizza_Pickup01`
    (85515de19d04f3c4dfe704e6f02046e3 #-9213198209589280134, v1), `Catch01` (#-3131456990773043, present in
    063b6b66..., 43ea1176... and 85515de1..., v0.66), `basketballPickUp` (43ea1176... #-1934144089526804261, v0.5).
  - Throw: **`Throw01` (bundle d5ff127b953bd3929f3339985e37a0af, #-3367295008890999457)** at v0.5/0.6/0.66/1 (83x),
    `Goblin2_Potion_Slosh02` (1df8e0ef... #-8698235864558300008, v0.35 pv0.085), `WaterBottle_Throw 1`.
  - Drop: rare (grenade release, coffee cup impact...). Reset: `SFX_ObjectRezIn_Mono` (bundle
    85e7a91fa469acdede4ebd6dc5784a33, #-7962594749062835332) on one tool.
- renderSettings: hover highlight include/exclude lists, `SuppressAnimateInOut`, `ShouldAnimateWhileNotHeld`.

#### 4.1.3 Non-VR (screen) behaviour configs = how Rec Room's desktop/mobile mode throws, eats, drinks
`Tool.screenSettings`: `IdleAnimation` (PlayerAnimationAsset, fade 0.4 s), `ScreenBehaviorConfig` (ScriptableObject):

| config (bundle 8, pathId) | key values |
| --- | --- |
| `ThrowToolBehaviorConfig` (1df8e0ef, -6877966307972784836) generic | **throwSpeed 8 m/s**, throwAngularSpeed 360 deg/s, throwTowardsGazeTarget 1, gravity compensation 0, pitch curve identity (-90->-90, 90->90), velocity factor 1, charge anim fade-in 0.25 s, throw anim speed 2 |
| `BasketballThrowToolBehaviorConfig` (43ea1176, -3833266418078692338) | **throwSpeed 12 m/s**, angular 180; **pitchByGazePitch**: gaze -90 -> -90, **gaze 0 -> launch 70 deg up**, gaze 90 -> 90 (a lob); **normalizedVelocityByGazePitch**: -90 -> 0.299, 0 -> 0.593, 45 -> 0.988, 90 -> 0.791 (x throwSpeed); charge anim fade-in 0.66 s |
| `PingPongBallThrowToolBehaviorConfig` (43ea1176, 2576888539619091847), `RoomieThrowToolBehaviorConfig`, `ThrowTomato...`, `ThrowTennisBall...` | same lob curves as the basketball (tomato/tennis: velocity at 45 deg = 0.712, `transformGazePitchWithCurves` 0) |
| `Breakable*ThrowToolBehaviorConfig` (plate, pumpkin, candelabra, generic) | identity curves, charge anim at speed 0.25 (slow wind-up) |
| `DrinkRootBeerToolBehaviorConfig` (85515de1, -8816235241251471407), `DrinkWaterBottle...`, `DrinkPotion...`, `DrinkLatte...`, `DrinkBubbleTea...`, `SipBubbly...` | drinkAnimations list (fade 0.2-0.5 s), **consumePotionOnDrink 1**, throwSpeed 12 (1 for the empty bubbly bottle) |
| `EatPizza/Cake/Donut/Sushi/Chocolate/Pretzel/Popcorn/CakePop ToolBehaviorConfig` (85515de1) | eatAnimation fade 0.4 s, throwOnLastBite 0 (1 for cake pops), throwSpeed 5 (4 cake pop) |

Dreamport Non-XR mapping (GUESS; client-owned keys per `somnium_conventions.md` 11.1 are W A S D, Q, E, Space, Shift,
F, I, M, R, Tab, X, Esc - do not use them): left click on a prop (our own raycast, like ETS) picks it up (held at a
fixed offset in front of the camera, lower right); hold left mouse = charge, release = throw along the camera forward
using the config's pitch/velocity curves and throwSpeed; `G` = "use" (drink / eat / uncork / next radio track);
`H` = drop.

### 4.2 RigidbodyEx / Rigidbody / pickups
- `RecRoom.RigidbodyEx` (1,083): `unityRigidbody`, `precomputedCenterOfMass`, `customCenterOfMass {overrides, value}`
  (14 props override, e.g. houseplant), `physicsInterpolation` 0, `keepUnityRigidbodyWhileParented` 0,
  `keepParentOnParentDestroy` 0, `hasUnityRigidbodyByDefault` 1 (0 for 145 purely-kinematic helpers). A wrapper that
  lets Rec Room remove/re-add the Unity Rigidbody while parented to a hand. For us: keep the Unity Rigidbody; set
  `isKinematic` while held.
- Unity `Rigidbody` values come through (4.8: basketball mass 2, drag 0.1, angDrag 0.1, **collisionDetection 1 =
  Continuous**; bottle mass 1, drag 0, angDrag 0.05, discrete; kit pieces kinematic).
- `RigidbodyPickup` (659): `interpolateOnPickup` 1 (632) / 0 (9: maker pen, feedback tool, roomie), optional
  `proxyPhysicsVisuals` + `proxyPhysicsCollision` (sword rack swords, paddleball paddle: the held visual is separate
  from a physics proxy that collides - GUESS). **Semantics: grabbing moves the object smoothly into the hand (lerp)
  instead of snapping.**
- `KinematicRigidbodyPickup` (330, same fields): props that stay kinematic (all Dorm pieces, trash bin, signs):
  movable only with the Maker Pen. For us: static.
- `StabilizedRigidbodyPickup` (11, arena guns): + `separationAngle` 60 deg, `returnToDestabilizedSpeed` 360 deg/s
  (two-hand aiming). `PlungerRigidbodyPickup` (22): weapon sub-parts (reloader, stabilizer).

### 4.3 ToolCollider (6,944 instances)
Fields: `kinematicBounciness` **0.6 on 6,943 of 6,944** (0.98 on one), `impactType` 0 always, back-references
`ThisRigidbodyEx`, `Tool`. Meaning: marks a collider as belonging to the tool (so hand/grab raycasts map collider ->
tool) and gives the bounce factor used when the tool is kinematic (held) and hits something (GUESS). For us: a
`collider -> prop` dictionary; ignore bounciness.

### 4.4 CollisionAudio (270 instances)
Structure: `audioMappings[]` of `{Name, Type, Layers (bitmask), Tag, velocityToVolumeCurve (AnimationCurve),
Clips[] (clip structs), InstanceLimitDuration}` + `useCustomRolloff` 0 + `customRolloff {rolloffMode 1 (Linear?),
minDistance 2, maxDistance 100}` + `meleeWeaponColliders` + `proxyPhysicsCollision`.

Semantics (inferred): on `OnCollisionEnter` (`Type` 0 always), take the first mapping whose `Layers` contains the other
collider's layer and whose `Tag` (if not empty) equals the other GameObject's tag; volume = `clip.volume x
curve.Evaluate(x)` with **x = impact speed** (GUESS: relative velocity magnitude in m/s, curve clamped outside its keys:
Pre/PostInfinity 2 = clamp); pick a random clip of the mapping; do not replay that mapping within
`InstanceLimitDuration` seconds. 3D AudioSource: min 2 m, max 100 m (unless custom rolloff).

Layer masks: `3072` = layers 10+11, `3073` = 0+10+11 (Rec Room: 0 Default world, 10 tools/props, 11 kinematic
props/furniture - the kit's bed, ladder, hoop are on 11), `4294967295` = all.

Most common mappings (count):

| Name | Layers | Tag | curve (time>value) | InstanceLimit | typical object |
| --- | --- | --- | --- | --- | --- |
| Static | 3073 | - | -0.00>0.08, 1.00>1.00 | 0.5 | 90 bottles/breakables |
| Bounce | 3072 | - | 0.00>0.63, 1.00>1.00 | 0.05 | 38 balls |
| Static | 3073 | - | 0.00>0.80, 1.00>1.00 | 0.5 | 24 |
| Bounce | 3072 | - | 0.00>0.00, 0.25>1.00 | 0.05 | 18 (basketball) |
| IceJostle | all | - | (none) | 0.25 | 17 |
| Static | 3073 | - | 0>0, 1>1 | 0 | 15 |
| Table / Rim / Paddle / Pool Ball / Bowling lane, gutter, backboard | all or 2048 or 16777216 | `CollisionSfx_Table`, `CollisionSfx_Paddle`, `Pool/Ball`, `CollisionSfx/Bowling/...` | various | 0-0.25 | sport balls |

Rec Room surface tags seen: `CollisionSfx_Table`, `CollisionSfx_Paddle`, `Pool/Ball`, `CollisionSfx/Bowling/Lane`,
`CollisionSfx/Bowling/Gutter`, `CollisionSfx/Bowling/Backboard`. Tags live on GameObjects (`m_Tag` index; the tag
names table is in the encrypted globalgamemanagers) - we reproduce them with our own component or name convention.

Example, root beer bottle: Static, layers 3073, curve (-0.0027 > 0.0818, 1 > 1), clip
`Quest_Pirate1_BottleHitEnvironment01` (1df8e0efac4f496ebc58d7e6f86480c3 #7574836580665977938) v1 pv0.08 ps-0.156,
InstanceLimitDuration 0.5. Basketball: see 4.8.

### 4.5 BreakableObject (92 instances)
Fields: `thisWeapon` (MeleeWeapon), `wholeObject` (GameObject shown intact), `fracturedObject` (inactive GameObject
with the fracture SkinnedMesh + rig of chunk joints, layer 17), `shatterOnAnyImpact`, `shatterSpeed` (m/s),
`brokenLifetime` (s), `OnlyAllowDamageFromSources`/`DisallowDamageFromSources`,
`advancedPhysicsPortionOfVelocityUsedForShattering`, `advancedPhysicsMinimumImpulseForBreakage`, `smashParticlePrefab`
(PooledParticle), `smashSounds[]` (clip structs, random pick).

| pattern (count) | shatterOnAnyImpact | shatterSpeed | brokenLifetime | adv. portion / min impulse | examples |
| --- | --- | --- | --- | --- | --- |
| consumables & potions (56) | 0 | **2 m/s** | 2 s | 0.2 / 0 | root beer bottles, bubbly glasses, lattes, bubble tea, potions, hair dye |
| quest props (34) | 1 | 2 | 5 | 0.2 / 0 | fire potion, rum jug, beer bottle, skeleton bone |
| plate, mug (2) | 0 | 2 | 5 | 0.2 / 0 | Quest_Pirate1_Plate/Mug |

`smashParticlePrefab`: `(Quest_Pirate1_Weapon_Sparks_VFX)` (bundle 1df8e0efac4f496ebc58d7e6f86480c3,
PooledParticle #1433299472472273473) on 31, null on 57, popcorn-specific `[Popcorn_Hit_*_VFX]` on 4.
Semantics: when an impact's relative speed >= shatterSpeed (or any impact if shatterOnAnyImpact), hide `wholeObject`,
enable `fracturedObject` (its chunk joints get rigidbodies: `RagdollPiece.disableColliderWhenNotRagdolling 1`), give
the chunks the pre-impact velocity x portion, play a random smash sound + particle, destroy after `brokenLifetime`.
Root beer bottle smash sounds: `Quest_Pirate1_BottleShatter01` (#-716113269425279608), `...03` (#4721082192337417354,
ps +0.15), `...05` (#7263918511081748761, x3 in the list = weighted), all in bundle 1df8e0ef..., v0.5 pv0.2.

### 4.6 DrinkableBottle, Potion, ShakeAudio (the drink bottle)
Prefab used as the model: **`(Consumables_RootBeer_Bottle)`** (bundle 85515de19d04f3c4dfe704e6f02046e3, pathId
7741124006095656599, **container key `3e`**; it is spawned by `(RootBeerConsumableTool)` key `b2`, pathId -4369251225219183872,
a 6-pack crate: `ToolSet._pieces` = holder + 6 bottles at (+-0.125, 0.021, +-0.063) and (0, 0.021, +-0.077), bottles
rotated 0/180 deg). `rr_runtime_facts.md` loaded key `cf` = the `(Consumables_RootBeer_PixelPop_Bottle)` variant (pathId
-3649557515530645676). The Maker Pen `(Sandbox_RootBeerBottle)` (43ea1176..., key `9a`, pathId 527952798101794375) is
only breakable/throwable (BreakableObject, CollisionAudio, MeleeWeapon, NO DrinkableBottle) - use the consumable bottle
for drinking. `(WaterBottle)` 43ea1176... pathId -6214342394501383043 is drinkable (key `538`). Dumps: `SP\research\dorm\core\rootbeer_bottle.txt`.

Bottle anatomy: root (layer 10) Rigidbody mass 1 dynamic; `VisualRoot/Whole` (mesh `Consumable_Sarsaparilla_Bottle`,
scale 0.5, boxes 0.186 x 0.444 x 0.173 body + 0.1 x 0.227 x 0.1 neck + a 0.22 x 0.76 x 0.22 trigger),
`Whole/MouthTrigger` (layer 2, SphereCollider r 0.1 trigger at the neck), `Whole/MeleeCollider`
(MeleeWeaponCollider radius 0.15), `Cap/PotionParticle` + `Particle System` (the pour stream; material
`DrinkingWater_Mat`@1df8e0ef; ParticleEventListener), `Fractured` (layer 17, inactive, 5 chunk joints with boxes).

`DrinkableBottle` fields (67 instances; root beer values, then variants):

| field | root beer | meaning |
| --- | --- | --- |
| cork / capTransform | `Cork` GO / `Cap` | the cap is removed on first use ("uncork") |
| uncorkAudio | `Consumable_RootBeer_Uncap01` (85515de1 #-5416584273756112597) v0.75 pv0.2 | |
| lifeSpan | 8 | seconds of liquid when tipped continuously (GUESS: pour budget) |
| onlyPourWhileHeld | 1 | (0 for the water bottle) |
| pourAudio | `Consumable_RootBeer_FizzLoop01` (#-6543888900021017809) v0.1 | loop while pouring |
| emptyPourPitchShift | 0.5 | pitch added when empty |
| pourInterval / drinkInterval | 0.5 / **0.68 s** | cadence of pour particles / swallow ticks |
| drinkAudioClips | `Consumable_RootBeer_Swallow01` (bundle c51d5ab70b574e4df10035eafa888943 #7340386932620817505), `...02` (85515de1 #2533759578933384957), `...03` (#-3510890591318876210), v0.5 pv0.15 | random per swallow |
| drinkSplashPrefab | `(WaterBottleSplashFx)`@1df8e0ef PooledParticle #-4266659521823918626 | splash where the stream lands |
| mouthTrigger, particleDrinkRadius, intersectionDrinkRadius | SphereCollider on the neck, 0.25, 0.25 | drinking = pour stream or bottle mouth within 0.25 m of the player's mouth |
| isRoomieOnlyDrinkable, keepIsEmptyStateOnReset | 0, 1 | |
| shakeAudio | ShakeAudio on the same object | |

Variants: potions / hair dye (33): pourInterval 0.25, drinkInterval 0.5, keepIsEmpty 0; bubbly flutes (21): 0.65 /
1.25; water bottle (6): onlyPourWhileHeld 0, radii 0.15, emptyPourPitchShift 0, drink clip `Goblin2_Potion_Gulp01`
(1df8e0ef #-482192376788914180) v0.33; coffee pot (1): 0.5/0.68, radii 0.15; lattes/hot chocolate: uncork
`sfx_consumables_coffee_stopperpopout_01` v0.75, pour `sfx_consumables_coffee_pour_loop_01` v0.9, sips
`sfx_consumables_coffee_sip_01..03` v0.35 pv0.08.

`Potion` (script `RecRoom.Activities.Quest.Goblin2.Potion`, 60 instances; on every drinkable): `powerupType` -1
(none; 0 = quest fire potion), **`drinksToActivate`** (root beer 5, potions 2, bubbly/latte 5), **`drinkResetTime`**
(2 s; 10,000,000 = never for shareable bubbly/latte), `shareable` (1 for bubbly/latte), `clinkSfx[4]`,
`liquidLevelTransform` + `liquidScaleByDrinks[]` (visible liquid level shrinking per drink). Semantics: counts
swallows; after `drinksToActivate` swallows within `drinkResetTime` the effect fires (root beer: none, i.e. "empty").
`PotionConsumableTool` (29, Ego/hair dye/quest potions): just links `tool` + `potion`.

`ShakeAudio` (79): `onlyPlayAudioWhileHeldByPlayer` 0 (60) / 1 (19), `lookBackStartTime` 0.1, `lookBackEndTime` 0.05,
`shakeDefinitions[] {MinimumLinearAcceleration, MaximumLinearAcceleration, MinimumAngularAcceleration,
MaximumAngularAcceleration, MinimumInterval, AudioClips[]}`:
- potions/root beer: linear 100..10000 m/s2, angular 120..10000 deg/s2 (GUESS unit), MinimumInterval 0.4 s,
  clips `Goblin2_Potion_Slosh01/02/03` (1df8e0ef #-7978863167757867463 / #-8698235864558300008 /
  #-8663540488445145083) at v0.5 (root beer v0.1).
- bubbly: linear 8..50, angular 6..50, interval 0.85, `sfx_consumables_champagne_bottle_slosh02` v0.1.
Semantics: acceleration estimated from the velocity change between 0.1 s and 0.05 s ago; above the minimum, play a
random slosh (volume scaled between min and max, GUESS) no more often than MinimumInterval.

Also on the bottle: `WeaponAudio.onFire` = `Quest_Pirate1_BottleThrow01` (1df8e0ef #3656724217005259585, v0.6 pv0.1),
`onSwingAudioPool` = Slosh02 v0.6; `MeleeWeapon` (damageType 1002, 100 damage to players, 50 to enemies,
`supportsThrowDamage` 1, `minThrowDamageSpeed` 0.45 m/s) - the bottle is a (quest) weapon; ignore in the dorm.
`VisibilityGroup`, `ConsumableSpawnedToolPiece`: bookkeeping.

### 4.7 Food consumables (`DisposableItem`, 88 instances)
Consumables come as a **`FoodConsumable_*` item** (`RecRoom.Consumable.FoodConsumableItem`: friendlyName, category
4 = food, limitCount 1, `consumableTool` = a `*ConsumableTool` spawner) -> the spawner (`SpawnerConsumableTool` +
`ToolSet` with piece list, e.g. pizza box + slices) -> **edible pieces with `DisposableItem`**. All in bundle
85515de19d04f3c4dfe704e6f02046e3. Full table: `SP\research\dorm\core\disposables.tsv`.

`DisposableItem` fields: `minBiteInterval` (s), **`visualsAfterEachBite[]`** (GameObjects enabled one at a time: whole
-> half -> last...; count = number of bites), `isLastPieceNonEdible` (the last visual is a stick/cup/core that is not
eaten), `inedibleCleanupDelay`, `destroyFinalPieceOnCollision`, `biteAudioClips[]`, `audioRolloff {Linear, 0-12 m}`,
`biteParticles[]`, `eatPositionOverride`, **`minDistanceFromHeadSq`** (bite when the item is within sqrt(value) of
the head), `breakableObject`.

| item (piece prefab, pathId) | bites | biteInterval | minDist (m) | last piece | bite clips (vol, pitchVar) |
| --- | --- | --- | --- | --- | --- |
| cheese pizza slice `(Consumables_CheesePizza_Slice)` key `e7` 4480956918179121698 (pepperoni key `21c` -5987117321215825740, hawaiian 5144217861204665669, supreme 6225706476375491530, dessert -2565059966910619280) | 3 (`pizza_cheese_slice_whole/half/last`) | 1.5 | 0.20 | eaten, destroyed on collision | `Consumable_Pizza_Chew01` (#4930413585144798225), `Chew02` (#624840576556265396) v0.33 pv0.1 |
| apple `(Consumables_Apple_Red)` key `a6e` -8646570057168270610 | 4 | 1.5 | 0.20 | eaten | `Consumable_Apple_Chew01..04` (85515de1 #1496363507852514589, #-836904418029204735, #-7695984439385074923; `Chew04` in bundle 33411ac7d2e2169af3b0b3426a06a276 #-9051192780791714895) v1 pv0.1 |
| candy apple 9190773214173195188 / poison apple -160560493556213227 | 4 | 1 / 1.5 | 0.20 | eaten / inedible core | apple chews v1 |
| cake slices `(Consumables_Cake_*_Slice)` (chocolate key `32d` 487917360470858137, cheese, fruit, pastel, red velvet, yellow, 87 flavor, mardi gras) | 4 | 1 | 0.32 | eaten | `sfx_consumables_cake_takebite_01` (bundle 06238e70c14666638ecf26f216f9fa97 #152962117004314022), `_02` (@95e2f5c2 #-6043545693460192272), `_03`, v0.33 pv0.1 |
| cupcakes (ghost/worm) | 4 | 1 | 0.20 | eaten | cake bites v0.5 |
| cake pops (6 kinds) | 4 | 1 | 0.32 | stick inedible | cake bites v1 |
| donuts (9 kinds, e.g. glazed key `673` 6545480484343517428) | 3 | 1.5 | 0.20 | eaten | pizza chews v0.33 |
| chocolates (circle/square, dark/milk/white) | 2 | 1 | 0.32 | eaten | `sfx_consumables_chocolates_takebite_01` (85515de1 #-8142863914327731383), `_02` (#-2747998578073487313), `_03` v0.33 |
| **bubble tea cup** `(Consumables_BubbleTea_Cup)` key `8a` 2237422486405014313 (+matcha, taro, eyeball) | 4 (sips) | 1 | 0.15 | cup inedible, **BreakableObject** | `sfx_consumables_coffee_sip_01..` (85515de1 #-1378878365209421351, #-577687023414712226, #-5424866812987397008) v0.6 |
| nachos (4 chips) | 1 each | 1 | 0.20 | eaten | apple chews |
| sushi (hand roll 2, nigiri 1) | 1-2 | 1 | 0.20 | eaten | `sfx_consumables_sushi_takebite_01..03`, `handroll_takebite_01..03` v0.5 |
| mochi, onigiri, taiyaki, pretzel, popcorn ball, popsicle, turkey leg, ham, dango, chestnut | 1-4 | 1-1.5 | 0.20 | various | chocolate/pizza/popcorn bites |
| tomatoes, tennis balls, swords, snowball (DisposableItem with 0-1 visuals) | - | - | - | inedible | no bite audio (throwables) |

Coffee: the latte (`(Consumables_Latte_Cup)` key `6cb`, pathId -3152192882405348162) / pumpkin spice / lavender latte / hot chocolate are **drinkables** (4.6), the Rec Center cafe
`(RecCenter_Cafe_Mug)` and `(RecCenter_Cafe_CoffeePot)` are in 43ea1176... (earlier dumps
`SP\research\dumps\_brief_7962773710793404200.txt` mug, `_brief_-6802294012789304814.txt` coffee pot:
DrinkableBottle 0.5/0.68 s, radii 0.15, `Goblin2_Potion_Gulp01`).
Eating semantics (inferred): while held, if `(item.position - head.position).sqrMagnitude < minDistanceFromHeadSq`
and `minBiteInterval` elapsed: advance to the next visual, play a random bite clip + bite particle; after the last
visual is eaten, destroy the item (or keep the inedible last piece; `destroyFinalPieceOnCollision` = it disappears when
it next hits something). Non-VR: the "Eat...ToolBehaviorConfig" plays an eat animation and takes one bite per use.
For Dreamport: Somnium gives the head pose (`References.Body.Head` / camera): same test.

### 4.8 Basketball + hoop
**`(Basketball)`**: bundle 43ea1176ca25c0333547813ec3298808, container **`d2`**, pathId 5755647178341420997
(dump `SP\research\dorm\core\basketball.txt`).
- Rigidbody **mass 2, drag 0.1, angularDrag 0.1, gravity, collisionDetection Continuous (1)**, not kinematic.
- Child `basketball_fbx` (rot +90 X): **SphereCollider r 0.124082** (ball diameter 0.248 m) with PhysicMaterial
  **`Basketball`** (class 134, #-5530415985010047082): dynamic friction 0.8, static 0.8, **bounciness 0.8**, friction
  combine Average (0), bounce combine Average (0). LODGroup (size 1.73) with `Basketball_LOD0/1/2` (502 v; bounds
  +-0.124), material `Basketball_Mat`@1df8e0ef #7437356361916885911. Inactive skin children (NBA logo, NBA75, cute
  cat, beach ball) driven by `ToolSkinMapper` (RecNetPrefabId `[Basketball]`) - ignore.
- `Tool`: CanBePickedUp 1, ThrowSpeedMultiplier 1, no max throw speed (10 default unused), FullBody grips
  (+-0.125746, 0.0068615, 0.00713116), hand pose anim -1572429817 blend 0.5, `DynamicKinematicCollisionsCanBeApplied` 1,
  **PickupAudio `basketballPickUp`** (43ea1176 #-1934144089526804261) v0.5, **ThrowAudio `Throw01`** (d5ff127b953bd3929f3339985e37a0af
  #-3367295008890999457) v0.5; screen: idle anim `Screens_Basketball_Idle`, `BasketballThrowToolBehaviorConfig` (4.1.3).
- `CollisionAudio`: mapping **"Bounce"**: layers 3072 (10+11), no tag, curve (0 > 0, 0.247629 > 0.999875), clip
  **`BasketballBounce01`** (43ea1176 #4467403410159767833) **v0.793408 pv0.05**, InstanceLimitDuration **0.05**;
  mapping **"Rim"**: all layers, tag `CollisionSfx_Table`, same curve, clip **`BasketballHitRim01`** (43ea1176
  #-8057379618765553001) **v0.15 pv0.05**, limit 0.05. Rolloff 2-100 m.
- `Basketball` script: `FireTrail` = ParticleSystem on inactive child `[DodgeballFireTrail]` (material `Fire_01`@1df8e0ef
  #-4807334223617823362, 3 s loop) - a "hot streak" effect (GUESS: after consecutive baskets); `PassedRimParticle` =
  **`[VFX_Basketball_PassedRim]`** (43ea1176 pathId 9003561391953464342, PooledParticle #6213644211515935766, 1 s
  burst, material `Burst_01` #2268368546705156181 + child `Fragments` `VFX_WeaponImpactCenter_Mat`; no container key =
  implicit, resident once `(Basketball)` is loaded); **`PassedRimAudio` = `DingDing`** (bundle
  cc5b30c7397eaaaa29b44e823663a6f4 #-463502042293685994) **v0.7**.
- `ToolTrail` on inactive child `Trail` (TrailRenderer, material assigned at runtime): visibleWhileHeld 0,
  visibleUponRelease 1, releaseTime 2 s, fadeOutUponRelease 1, releaseFadeOutDuration 1 s, visibleAfterAttackApplied 1.
- `RezBounds` child (SimpleVolume, scale 0.174 x 0.174 x 0.196) = AnimateInOut bounding volume. ToolCleanup defaults.

**Hoop `(Basketballhoop)`**: container **`78`**, pathId 6058454435073796307 (`SP\research\dorm\core\hoop.txt`).
Kinematic. Child `BasketRimGroup` (layer 11, rot -90 X) with LOD `Goal_LOD0/1` (590 v; material
`BasketBall_Goal_Recolor_Mat`, `_InstanceColor` orange (1,0.282,0)). In the root frame: backboard 1.83 wide x 1.22 tall
x 0.025 thick (box on the Goal LODs, PhysicMaterial **`BasketballBackboard`** bounciness **0.45**, friction 0.8,
combine Multiply (2)), whole assembly depth 0.615 m (board at z 0..0.025, rim out to z 0.615); rim = two non-convex
MeshColliders `Rim.ProxyOuter` (scale 1.03) and `Rim.ProxyInner` (mesh `Rim.Proxy`, ring 0.50 m outer diameter,
PhysicMaterial **`BasketballRimInner`** bounciness **0.05**, combine Multiply) centred at **(0, -0.335, +0.371)** from
the pivot; `BrickCollision` box (the bracket) with the same rim material. Unity combine rule: Multiply wins over
Average, so ball-on-board bounce = 0.8 x 0.45 = 0.36 and ball-on-inner-rim = 0.04 (a dead rim, the ball drops in);
`Rim.ProxyOuter` has NO PhysicMaterial (Unity default: bounciness 0, Average) so ball-on-outer-rim = (0.8 + 0) / 2 = 0.4.
The rim colliders presumably carry the tag used by the ball's "Rim" sound mapping (`CollisionSfx_Table`, GUESS).
There is **no "score" trigger** in the hoop: the Basketball script detects passing the rim itself. Our implementation:
a trigger disc (r 0.2, 0.05 thick) at the rim centre; count a basket when the ball enters it moving downward
(vy < 0) and exits below; then play `DingDing` v0.7 and spawn `[VFX_Basketball_PassedRim]` at the rim.
In the dorm: rim centre at room (3.905, 1.097, -1.090) (section 3.3).

### 4.9 AnimateInOut (993) and ToolCleanup (1,023)
`RecRoom.AnimateInOut`: `effectPrefab` (null on 988 of 993), `boundsRendererRoot`, `ignoredRenderers[]`,
`boundingVolume` (SimpleVolume child, e.g. basketball `RezBounds`), **`animInStartOffset` 0.2 s, `animOutStartOffset`
0.2 s** (842), `SupportsAnimateIn/Out` 1, `SuppressAnimation` 0 (45 suppress). The only effect prefab referenced:
**`[VFX_ObjectSpawn]`** (43ea1176 pathId -1545908865104360033, PooledParticle #-2931215021451260513, 0.35 s one-shot,
material `VFX_Avatar_Spawn_Mat`@1df8e0ef #4606990561825027975; implicit asset, resident when `(Whip)` /
`[RecRoyale_WinnerDoor]` are loaded); variants `(VFX_ObjectSpawn)LimitedTimeBundleVariant` and
`[VFX_ObjectSpawn] Yeti Despawn` in bundle 6d3223da354de646ab79d5660dcac9d2. The standard "rez" look (object
materialising) is shader/code driven, not data (GUESS). Sounds: `SFX_ObjectRezIn_Mono` (85e7a91fa469acdede4ebd6dc5784a33
#-7962594749062835332, 2.5 s) and `sfx_ui_prop_spawn_01..04` (1df8e0efac4f496ebc58d7e6f86480c3 #3590727226276041322,
#622733502832470028, #-865684849236943307, #-4203138781948389954, 0.32 s). **Our implementation:** after a 0.2 s
delay scale the renderers' root from 0 to 1 over ~0.35 s (ease-out back) + optional `[VFX_ObjectSpawn]` particle +
`sfx_ui_prop_spawn_0x`; reverse for despawn.

`ToolCleanup` (auto-reset of props moved and left): defaults on 840 of 1,023: `excludeFromCleanup` 0,
`allowUserOverrides` 1, **`MinDisplacementBeforeCleanup` 0.1 m, `MinRotationAngleBeforeCleanup` 30 deg,
`defaultMinPlayerDistanceBeforeCleanup` 3 m, `defaultDelayUntilCleanup` 15 s**, `defaultUseVisualEffects` 1,
`OnlyCleanupWhenSleeping` 0. Semantics: if a prop is displaced > 0.1 m or rotated > 30 deg from its spawn pose, is not
held, and no player is within 3 m, then after 15 s it despawns (AnimateOut) and respawns at its spawn pose (AnimateIn).
Variants: 110 excluded (managers, AI); consumable empties 999999 (never); standees 1 m / 0 m / 3 s / only when
sleeping; grenades player distance 1 m; bowling ball 3 m / 5 m / rotation ignored; disc golf disc only when sleeping;
whip 1.2 s; flag 30 s.

### 4.10 The clip struct and how to play it (GUESS where marked)
Struct `{audioClip, configureVolumeInDecibel, volume, pitchVariation, pitchShift, subtitles}`. Survey of ~7,500 clip
structs: `configureVolumeInDecibel` is **0 everywhere** in the scripts we dumped (so `volume` is linear 0..1: most
common 1, 0.5, 0.8, 0.35, 0.25); `pitchVariation` 0..0.2 typical (0.621 once); `pitchShift` 0 (7,368), 0.5 (73: smash
sounds, empty pour), -0.2 (22), 0.15, -0.156, -0.1, -0.907 (4).
Playback (GUESS, consistent with the values): `source.volume = volume` (or `10^(volume/20)` if
configureVolumeInDecibel = 1) x mapping factor (e.g. CollisionAudio curve); `source.pitch = 1 + pitchShift +
Random.Range(-pitchVariation, +pitchVariation)` (clamp >= 0.1); one-shot 3D AudioSource at the object (spatialBlend
1, rolloff Linear/Logarithmic 2..100 m for CollisionAudio, 0..12 m for food/consumables `audioRolloff`); `subtitles`
= localisation (ignore). Random clip choice from lists; a clip listed several times is more likely.

## 5. Other dorm-worthy objects

All in bundle **43ea1176ca25c0333547813ec3298808** unless noted. Size = mesh LOD0 full extents (2 x `m_Extent`)
in metres. Briefs: `SP\research\dorm\props\<pathId>.txt` (meshes + bounds, materials, clips; run
`SP\research\dorm\props\summ.sh` for a one-line table). "dyn" = dynamic `RigidbodyPickup`, "kin" =
`KinematicRigidbodyPickup` (static furniture).

### 5.1 Directly from the real dorm's list

| object | pathId | key | size (m) | what it does / notable data |
| --- | --- | --- | --- | --- |
| `(Radio)` (boombox) | -7125182807003965041 | `f3` | 0.13 x 0.43 x 0.31 | dyn, LockOnPickup 1 (stays in hand), `RecRoom.Radio`: 41 music tracks (`SFXTrack` ScriptableObjects), `randomInitialTrack` 1, AudioSource volume 0.1, 1-30 m linear rolloff, ToolMenuProjector (a small menu to change track), skinned boombox with an Animator (speaker pump). **Track list with clip bundles/pathIds: `SP\research\dorm\core\radio_tracks_full.tsv`** (Sport Funk, In The Bassment, Secret Room, Goat Attitude, Organism, River Predator, Flight Beam, Electar, JumboTron Explore, Hangar, Clearcut, Food Court, Goblin Work Song, Range Finder, Spillway, Golden Trophy Explore/Combat, JumboTron Combat, BeeDM, Stunt Runners (+Final Lap), Victory Lap, New Jack Runner, Crescendo Ballroom/Gavotte/Boss/Miniboss, Dodge Baller, Lone Shoe Blues, Shanty (+Piano), Pirate Shopping Channel, Intro, **Rec Room Theme**, Cyberjunk Combat/Explore, Bowling, Crimson Cauldron Mines, Ballad of VotaVader, Pirate Brawl, Homestead Varmint). Our radio: click = next track |
| `(AmbienceRadio)` | 2043062669133516778 | `886` | same boombox | same script, its own track list (GUESS: ambience loops), randomInitialTrack 0 |
| `(Sandbox_CouchChair)` (armchair) | -409350397979008085 | `f0` | 1.13 x 0.90 x 0.92 | dyn, 1 `SeatPortal` (sit: box collider on `[Seat]`, `anchorPoint` (0,-0.8,0) below the seat marker, `standingAnchorPoint` (0,0,0)) |
| `(Sandbox_Couch)` | -1532349014166191393 | `c5` | 2.50 x 0.88 x 0.92 | dyn, 3 seats |
| `(Chair_Beanbag_Sandbox)Variant` | 5696760868194268639 | `ec1` | 1.13 x 0.71 x 1.06 | kin, seat |
| `(Sandbox_Houseplant_Fig)` / `_SnakePlant` / `_Dracaena` | 4859817174503967356 / 5684304667218242089 / -2609047512322496204 | `cb6` / `ce6` / `ddd` | 0.77 x 1.85 x 0.84 / 0.39 x 1.39 x 0.41 / 1.09 x 1.04 x 1.11 | dyn plants ("some plants"); snake plant has a custom centre of mass |
| `(ArtCanvas)` / `(ArtCanvas_Square)` | -7928479234811092907 / -284779296372546362 | `d8c` / `c3` | 1.11 x 1.54 / 1.11 x 1.11 (frame, 0.03 thick) | kin, `DrawingSurface` + `Drawing.ArtCanvas` ("art supplies and a canvas" on the loft) |
| `(Sandbox_Paintbrush)` | 8549479499466616881 | `26` | - | a `WhiteboardPen`-type brush for the canvas |
| `(Sandbox_MuteGesture_Poster)` / `(Sandbox_ShakeHands_Poster)` | -5503512149270850701 / 6657100259046836956 | `ef` / `b8f` | mesh `Poster_Dorm` (authored in cm: 84 x 103 x 1.1 before the prefab scale) | kin, TextMeshPro text: **the two social posters (Block/Mute and Friends)** |
| `(Sandbox_CodeOfConduct_Poster)` | -9153174756256126895 | `760` | 0.71 x 0.92 paper | kin, `CodeOfConductPoster` + TMP ("Code of Conduct") |
| `(Sandbox_Mirror)` | -6592890460863086279 | `544` | same meshes as `Dorm_FreeMirror` (2.04 x 2.21 glass) | the 2021 "Mirror" prop = the dorm mirror repackaged |
| `(TrashCan_Office)SandboxVariant` / `(TrashCan_Metal_Sandbox)Variant` / `(TrashBin_Sandbox)Variant` | -6713108123592849537 / -1719041819296943189 / 7458994975604054613 | `cd8` / `d4ae` / `0c` | office bin 0.46 dia x 0.47 | the dustbin under the hoop (office bin dyn, TrashBin kin) |
| `(Basketball)`, `(Basketballhoop)` | 5755647178341420997, 6058454435073796307 | `d2`, `78` | ball 0.248 dia; board 1.83 x 1.22 | section 4.8 |
| drinks & food | see 4.6 / 4.7 | `3e` (root beer bottle), `b2` (6-pack), `e7` (pizza slice), `a6e` (apple)... | | section 4.6-4.7 |

### 5.2 Other dorm-worthy toys and furniture

| object | pathId | key | size (m) | notes |
| --- | --- | --- | --- | --- |
| `(Dart)` | 3920024455142888564 | `fe7` | 0.045 x 0.045 x 0.154 | dyn. `Dart` script: **sticks where it lands**: `attachOnRelease` 1, `persistAttachment` 1, `disableCollidersWhenAttached` 1, `minSpeedToStick` 1 m/s, `maxAngleToVelocityToStick` 45 deg, `maxAngleToSurfaceToStick` 60 deg, `tailDrag` 0.02, `dragByAngle` curve (0 deg -> 0, 90 -> 1, 180 -> 1: aligns the dart with its flight), `scaleVelocityAtRelease` 1.25. Throw clip `Throw01`. **No dartboard prefab exists** - use the campus map / a poster / a Maker-Pen-style disc as target |
| `(DiscGolfDisc)` (frisbee) | 6557311508961764802 | `9d` | mesh `Frisbee_LOD0`@1df8e0efac4f496ebc58d7e6f86480c3 | dyn, `DiscGolfDisc` flight model + `DiscGolfDiscTrail` (additive trail `DiscGolfDisc_Trail_Mat`), catch `Catch01`, throw `Throw01`; flight details belong to the sports research (disc golf) |
| `(SoccerBall)` | -3401908588561202286 | `00` | dodgeball mesh | dyn, `SoccerBall`, hit clip `Soccer_HitBallHeavy_01`; MaxThrowSpeed 35 |
| `(DodgeballBall)` | -1619130950701524752 | `1bf` | - | dyn, `Dodgeball`, trail; MaxThrowSpeed 15 |
| `(TennisBall)` | -7724947534371109278 | `787` | (uses the basketball LOD mesh, scaled) | dyn, `Ball`; MaxThrowSpeed 18 |
| `(PingpongBall)` / `(Pingpong_Paddle)` / `(Sandbox_Table_PingPong)` | -1004661230452129383 / 4290896271436088691 / -1362248597509205463 | `568` / `64` / `7cd` | ball 0.04 dia | ping-pong set (Paddleball-style) |
| `(BowlingBall)` | 8279500870475957079 | `a5a` | 0.248 dia | dyn, `BowlingBall`, roll clip `Bowling_BallRoll02`, pickup `Bowling_Pickup01` |
| `(Sandbox_Snowball)` | 4897558500545540924 | `5ec` | - | dyn, `Snowball` (splat `Impact_Snowball`), ThrowSpeedMultiplier 1.5, MaxThrowSpeed 25 - the closest thing to a **water balloon** (none exists) |
| `(Consumables_BubbleWandTool)` | -7081731020171847785 (bundle 85515de1...) | `1e` | - | bubble wand consumable |
| `(Marker)` / `(EraserMarker)` / `(MagicMarker)` / `(Marker_Recolorable)Variant` | 6156072449279112575 / -1839925672122964462 / -7292178283541048878 / -380321452371682318 | `945` / `9cf` / `3cb` / - | 0.057 dia x 0.50 | `WhiteboardPen`: brush tip, `maxDrawingOffset` 0.01, `minControlPointDistance` 0.005 m, `_drawingMaterial` `PenDrawing_Mat` #2782482085788419907 (eraser `EraserMaterial`), vibration curve, `MarkerDown` clip; the MagicMarker (charades) has a physical button |
| `(MoveableWhiteboard)` | 8372748118076330922 | `fb` | frame `Whiteboard_Frame_LOD0`@1df8e0ef | `DrawingSurface` (resolution 1024, allowSharing 1) + `MoveableWhiteboard` (rolling) |
| `(StickyPoster)` / `(StickyNote)` | 4652402129116402587 / -4198771504764707893 | `23` / `417a` | 0.10 x 0.10 note | `DrawingSurface` 256 px + `SimpleSticky` (sticks to walls), pickup clip `SFX_Pickup_Card1_Stereo` |
| `(Sandbox_Bell)` | -6493488885355874438 | `45b` | 0.087 dia | reception bell (`ReceptionBell` clip, SimpleSticky) |
| `(Sandbox_Speaker)` | -5932272720997597246 | `a2f` | 0.45 x 0.66 x 0.32 | "voice line" speaker (`VoiceLineControl` x4) |
| `(SandboxDiceSet)` / `(SandboxDiceSet_V2)` | -399789956074880494 / 5120486484033775800 | `6d` / `25a` | dice box 0.11 x 0.15 x 0.11 | dice (throw clip `SandboxDice_Throw`) |
| `(SoloCup)` | 3935866521521447249 | `4ba` | 0.13 x 0.13 x 0.18 | plastic cup (skins) |
| `(Sandbox_Book_Green)` / `(Sandbox_Book_Yellow)` | 6641017581094378083 / -7367355296382360499 | `e82` / - | 0.12 x 0.30 x 0.36 | books for the loft shelves |
| `(Trophy_MakerPen)` (+Bronze `01`/Silver), `(Trophy_Oscar)`, `(Trophy_Moderator)`, `(Trophy_Clapperboard)`, `(Trophy_CostumeDummy)`, `(Trophy_DevMod)` | -5738573795837604323, -2711667834383635732, ... | `cdb`, `09d`, ... | Oscar 0.36 x 0.53 x 0.24 | dyn trophies with `PlayerEditableText` (TMP engraving) - shelf decoration |
| `(Sandbox_Table_Coffee_Large)` / `(Sandbox_Table_Short)` | -6593861199370616417 / 1294817617570128736 | `1a` / `c72` | 2.38 x 0.68 x 1.23 / 1.21 x 0.44 x 0.62 | kin tables |
| `(RecCenter_Couch_Sandbox)Variant` | 307189170882659907 | `ed9` | 0.91 x 1.08 x 2.62 | kin, 6 seats (Rec Center couch) |
| `(Chair_Moveable)` | 7009576536407601616 | `ed` | 0.62 x 0.98 x 0.56 | dyn stackable chair with a seat |
| pool: `(PoolTable)`, `(PoolCue)`, `(PoolCueRack)`, `(PoolTriangle)`, `[PoolBall]` | -2137806567733975631, -6383371098871327646, 7460407601710766675, -7644449635642145061, -4370770082079619811 | `ee`, `4f`, `d6`, `ad`, none | table base 1.47 x 0.58 x 0.98 (LOD mesh `base`) | full pool game (`PoolTable`, 18 cushions, 6 pockets with vacuum, `ChargePhysicalButton`); earlier dumps `SP\research\dumps\_brief_-2137806567733975631.txt` etc. - party/lounge research |
| poker: `(Sandbox_PokerChair)` | -7939893472342937302 | `ebf` | 0.88 x 0.84 x 0.92 | seat only; a `PokerChip_LOD1` mesh (#-748064070789031701) and `Poker_DiceClack03` clip exist but **no chip/card prefab** |
| `(MakerPen)` | 3543397827117124696 (bundle 4056f2fc0e2635c36096ef5d5294708e) | `5` | - | the Maker Pen tool itself (building UI is far beyond scope; could be a toy that shoots `[VFX_ObjectSpawn]`) |

Not found in any bundle (searched names): **paper airplane, water balloon, dartboard, guitar, pinata**.

## 6. Open questions, guesses, tooling

### 6.1 GUESSES to verify in Unity (quick checks)
1. Furniture layout (3.3) except the hoop/bin: positions are design choices, not data. Check against a screenshot of
   the real dorm if one is available to the implementer (the wiki page `rec-room.fandom.com/wiki/Dorm_Room` has
   "View of the Dorm Room" and concept art; the YouTube "A tour of the 2018 Dorm Room | Rec Room Preservation
   Project" video shows it).
2. Wall-prop facing (+z = front) for mirror, board, map, button panels: verify visually once instantiated.
3. Bunk bed orientation (guard rail on -z = room side): check the render.
4. Colours for the MakerPenShape surfaces (2.1).
5. CollisionAudio curve x axis = impact speed in m/s (4.4). Alternative: normalised speed. Tune by ear.
6. Clip pitch formula `1 + pitchShift + rand(+-pitchVariation)` (4.10). Alternative: semitones.
7. ShakeAudio acceleration units (m/s2 and deg/s2) and the volume mapping between min and max.
8. `Potion.drinkResetTime` semantics (window for counting swallows).
9. The "rez" spawn effect look (4.9) - Rec Room's is code/shader driven; ours is a scale tween.
10. Whether the pane meshes include glass (they use MakerPenShape; probably only muntins) - add a transparent glass
    quad if the openings look empty.

### 6.2 Things deliberately not reproduced / Dreamport adaptations
- Avatar customisation at the mirror: **no** (the Somnium avatar is never modified). The mirror is a mirror; its button
  panel becomes Dreamport's own menu (settings, help, activity list).
- Dorm Skins, RR+, store, tokens: no.
- Community Board: becomes an activity board (teleport to the Rec Center / activities, see
  `somnium_networking.md` 5.3 "teleport them far away").
- Door: `(Sandbox_RoomDoorV2)` visual + our own trigger that teleports to the Rec Center instance.
- Networking: dorm props are local-only physics (`somnium_networking.md` 5.5.6: the dorm is private, no network
  object). If several players share a dorm later, use the prop record of 5.5.2.
- Layers: floors/walls on 0 or 23, props on 10 or 16 (`somnium_conventions.md` 12). Rec Room layers (10, 11, 17,
  25, 27, 0, 2) must be remapped on instantiate.

### 6.3 Research tooling added in this task (reusable, all under `SP\research\dorm\`)
- `meshtool\` = a copy of rrtool with two extra commands (built with `dotnet build -c Release -o bin/out`):
  - `meshtool.exe verts <bundle> <meshPathId>`: prints the mesh's vertex positions (`v i x y z`) and triangles
    (`t a,b,c ...`) from the inline `m_VertexData` (stream 0, float3 positions) and 16/32-bit index buffer.
  - `meshtool.exe many <depth>`: reads `bundle<TAB>pathId` lines on stdin, prints `bundle<TAB>pathId<TAB>json` for
    each (one process for thousands of objects: all 6,944 ToolColliders in ~20 s).
- `piece.pl` (compact prefab summary from `rrtool tree --fields --all`), `waabb.pl` (root-space AABBs of meshes and
  boxes, `VERTS=n` prints transformed vertices), `place.pl` (room placement = inverse child transform),
  `localbox.pl`, `tris.pl` (triangle centroids, used to find wall openings), `stat.pl` (value distributions over
  `many\*.json`), `mbd.sh` (deep dump of one MonoBehaviour of a prefab), `allpieces.sh` (dumps all dorm pieces).
- Data: `raw\`, `pc\` (47 pieces), `verts\`, `many\` (all instances of Tool, ToolCleanup, AnimateInOut,
  BreakableObject, DrinkableBottle, Potion, ShakeAudio, CollisionAudio, RigidbodyPickup(+variants), RigidbodyEx,
  ToolCollider; `roots_pos.tsv` = every prefab root saved at a non-zero position), `core\` (basketball, hoop, bottle,
  pizza, `disposables.tsv`), `props\` (section 5 briefs), `wiki\` (wiki JSON + all Steam news).
