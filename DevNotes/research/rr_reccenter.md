# Dreamport: Rec Room - Rec Center + room transitions (research)

STATUS: complete (2026-09-24). Supporting files: `_rc_tracks.tsv` (radio/jukebox tracks), `_rc_matscan.tsv` (materials per kit prefab), `_rc_briefs.txt` + `_rc_briefs2.txt` (prop summaries), `img/rc/*.png` (door textures, wood floor, UI illustration atlas), `dumps/_brief_<pathId>.txt` (full prefab dumps).

Conventions in this file
* `B43` = bundle `43ea1176ca25c0333547813ec3298808` (the Maker Pen / sandbox-tools bundle, ~800 prefab roots). Most
  props below live there. Other bundles are written as their first 8 hex chars; full names are in
  `SP\rrtool\idx\cabs.tsv` / `objects.tsv` (SP = scratchpad folder).
* "key" = Addressables container key: `bundle.LoadAsset<GameObject>(key)` loads the prefab (see
  `rr_runtime_facts.md`). Only container entries are loadable by API; everything else is reached through a container.
* Sizes are mesh `m_LocalAABB` center/extent (extent = half size), metres, in the mesh's local space, before the
  prefab transform scale.
* GUESS = not verified from data or a source.

---

## 0. Summary (read this first)
* **Rec Room is shut down** (servers off 1 Jun 2026): no live room, images or rec.net data; everything below comes
  from the local plain bundles, the wiki (via search snippets), Wikipedia and Steam patch notes.
* **The official activity door exists in the bundles**: `[ActivityDoor]` (B43 #7410808331468609159) inside
  `(Charades_InkSpace_MainHull_Sandbox)Variant` (container key `84`): wooden double doors with a wired-glass window,
  steel-blue frame, 2.05 x 2.14 m, backlit cream sign above with TMP text "Rec Center" (Futura Bold Condensed).
  Hover animation from Rec Room's clips: leaves +21.44 / -22.27 deg, warm additive glow alpha 0 -> 0.834 in 0.25 s.
  Door local +Z faces the player. Single-door variants: `(Sandbox_RoomDoor)` `2ac`, `(Sandbox_MagicDoor)` `2a9`.
  (2.1)
* **Loading key `84` also makes the shell materials resident** (wood floor, off-white/grey/dusk paints, brick,
  striped carpet, fluorescent panels, orange I-beam paint, posters, exit sign) (2.7).
* **Rec Room's loading screen**: fade 0.15 s, "GOING TO" + `^RoomName` + sub-room + "LOADING..." (0.3 s per dot) +
  progress + tips (2-5 s) + room image + avatar, min 5 s (2.2). We rebuild it (4.2).
* **Real Rec Center props with keys**: RecCenter couch `ed9`, cafe coffee pot `99` / mug `e5b` / plate `20b`,
  reception bell `45b`, Friend-O-Tron `bbd`, vending machine `4ea`, pool table `ee` (+cue `4f`, rack `d6`, triangle
  `ad`), ping-pong table `7cd` (+paddle `64`, ball `568`), card/poker table `51` + poker chairs `ebf`, basketball
  hoop `78` + ball `d2`, radio `f3` (41 Rec Room tracks, all loadable), ambience radio `886`, jukebox `48`, lockers
  `d5c`, community board frame `7ca` + UI `2d` (6d3223da), houseplants, cafe tables/chairs, posters (2.3, 2.4).
* **Not available**: arcade machines and a dartboard (no meshes), per-activity room images (server-side),
  `ActivityTitleJingle` and `music_ui_enter_room_guitar_01` (exist but not loadable), the orange door texture (not
  referenced by any material; recolour the grey frame instead).
* **Plan**: hall 40 x 36 x 8 m + lobby 12 x 8 x 4 m with the Dorm door; 20 activity doors on three walls grouped PvP /
  Sports (north), Quests (east), Party / Racing (west); central kiosk with the bell; basketball, ping-pong, poker,
  pool, lounge, cafe, vending; exact coordinates in section 3; door/transition behaviour in section 4.

---

## 1. The real Rec Center (web research)

Sources: Rec Room fandom wiki (page text only reachable as search-engine snippets: fandom returns HTTP 402 to fetches),
Wikipedia "Rec Room (video game)", Steam news API (patch notes 2021, 2025), rrtool dumps. Rec Room's own site
(recroom.com, rec.net) is offline.

### 1.0 Context: Rec Room is shut down
Rec Room Inc. announced the end of service on 30 Mar 2026; the servers went off on **1 Jun 2026, 12:00 PT**, rec.net
closed 9 Jun 2026 (Wikipedia). The official rooms can no longer be visited at all, so there is no live reference;
this document is the reference. (Relevant for our design: no "live" room names/images can be fetched - everything
must come from the local bundles.)

### 1.1 History (verified)
| when | what |
| --- | --- |
| 2016 | Hub was called the **Locker Room** (contents GUESS: lockers - `Sandbox_Lockers` shares a texture with a fountain and a spotlight, i.e. the old hub set - plus doors to the first activities). |
| 2017 | Given "a fresh coat of paint" and renamed **Rec Center**, with an updated layout "to make it easier to find the activities" (wiki News Archive 2017). |
| Feb 2018 | **Merch Booth** added (buy featured cosmetics / consumables with tokens; tokens from daily challenges). |
| 2018-2020 | Wiki layout description: Merch Booth and the **Quests** door on the right-hand side, the **community board** on the left, the door to the Rec Room Original PvP games behind the "shop" in the centre; labelled doors **"SPORTS & PVP"** (Paintball, Rec Royale, Laser Tag, Bowling, Dodgeball, Disc Golf, Soccer, Paddleball), **"Creative Campus"**, **"Featured Rooms"**. |
| Sep 2020 | Redesign: "new layout, fresh paint, nice big windows". |
| Jun 2021 | Rec Center doors switched to the new room-search menu (a door opens a filtered room list); VR fix "doors wouldn't open if your hands were full"; objects that fall through the floor respawn at their spawn point. |
| 7 Sep 2021 | **Friend-O-Tron** vending machine added in the Rec Center (send a friend a free random gift box once a day). |
| Dec 2021 | Winter (Christmas) variant. Other holiday dressings, e.g. the 5th birthday: streamers, flags, lots of buckets. |
| 22 Sep 2025 | Ship notes: the Dorm door defaults to **^RecCenter** (setting to prefer "Roomiverse" instead). |

### 1.2 What is in it / what players do (verified from wiki/Wikipedia snippets)
* Public hub "between your Dorm Room and other rooms". Walking out of the Dorm door puts you at the Rec Center entrance.
* Loose toys: basketballs (+ hoops), frisbees (disc golf discs), dodgeballs, a rolling cart, water bottles, buckets,
  a ping-pong table with paddles, darts, a cafeteria (cafe counter with coffee, mugs, plates).
* Merch Booth, community board (events/news), Friend-O-Tron, radios (the radio playlist includes "Rec Room Theme" by
  Gribbly, track 31).
* Players meet strangers to team up for activities, hang out, throw balls, and walk through the activity doors.

### 1.3 Look and feel (GUESS - from general knowledge of screenshots, not verifiable now)
* Bright, clean indoor recreation hall ("community centre / school gym" feel), low-poly Rec Room style, flat colours,
  soft baked-looking light. Light wood floors, off-white walls with a coloured lower band, teal/blue and orange
  accents, big windows (2020+) letting daylight in. Ceiling ~6-8 m in the main hall.
* The activity doors are the recognisable **wooden double doors with a small wired-glass window, in a coloured frame,
  with a backlit white sign above** carrying the activity name. When you look at / approach one it swings slightly
  ajar and warm light spills out (this is exactly what the bundle animator does, see 2.1.3).
* Music: no constant BGM in the hub (GUESS); radios/jukebox play Rec Room tracks locally; ambience is people chatter.

### 1.4 Room transitions as Rec Room does them (verified from bundle data, 2.2)
1. You use a door (VR: grab/touch the door leaf; screen mode: interact) or pick a room in the watch.
2. The view fades to black in **0.15 s**.
3. The **loading screen** shows: a background image (the destination room's image, or its custom loading-screen
   image), a bottom bar with **"GOING TO"**, the room name (e.g. `^Paintball`), the sub-room name (e.g. the map),
   "LOADING..." with an animated ellipsis (0.3 s per dot), a progress bar with percent, rotating tips (each shown
   2-5 s) and your avatar picture. It stays at least **5 s**.
4. Fade out (0.15 s) into the new room at a spawn point. Activities (Paintball etc.) then show their title card with a
   jingle (`ActivityTitleJingle`, 3.1 s) and ordinary rooms play `music_ui_enter_room_guitar_01` (3.7 s) (GUESS on
   which plays when; both clips exist in the Startup bundle - see 2.6 for why we cannot load them).
* VR: the loading screen is not in the world - it is blitted to a texture array for the compositor (the component
  references `PlayStationVR/TextureArrayBlit`), i.e. a head-locked panel in a black void. There is also a
  `[ComfortModeBackgroundScene]` (a 100x100 ground plane, layer 22) used as a neutral floor.

### 1.5 Dorm -> Rec Center
* The Dorm has one exit door. Using it loads the Rec Center (Rec Room loaded a separate scene with a loading screen:
  "GOING TO ^RecCenter"). You arrive at the Rec Center entrance, facing into the hall.
* Returning: in Rec Room you went back to the Dorm through the watch ("Go to Dorm") - GUESS whether a physical door
  existed; for Dreamport we put a "Dorm" door by the entrance (see 3).

---

## 2. Bundle content (props, doors, sounds)

### 2.1 Doors

#### 2.1.1 Prefabs (all in B43)
| prefab root | pathId | key | TeleportationPortal.mode | notes |
| --- | --- | --- | --- | --- |
| `(Sandbox_RoomDoor)` | -2215421481889553592 | `2ac` | 5 | Maker Pen room door. Single door leaf in a frame + sign. `RoomDoor` script: `roomNameText` = legacy `TextMesh` "Use maker pen to edit" (font `legacy_FUTURA-T-OT-BOLD-CONDENSED`@f124b399#5546545003271953346, size 100, char 0.7, black). TargetName "" (set per room). |
| `(Sandbox_RoomDoorV2)` | -5656950724883909510 | `09fb` | 19 | Same geometry, V2 logic (room-search menu door). |
| `(DEPRECATED_Sandbox_RoomDoor)` | -6035927394087478105 | `762` | 5 | Old version; contains `[ActivityDoor_Single]` (-2632886716450546521). Pick-up-able (hand transforms at -0.0655,-0.478,-1.084). |
| `(Sandbox_MagicDoor)` | 5412136012577350147 | `2a9` | 18 | Random-room door. TargetName **"RecCenter"**, `MagicDoor` script: sign TMP "Delight Me!", countdown label "New Room In:" + timer "00:00" (inactive), `destinationRefreshAudio` = `MagicDoor_Refresh` (B43 #-2651044525266811996, 3.21 s mono). `[MagicDoorManager]` (Startup bundle 063b6b66 #-8927044789429349444) texts: "Magic Door", "New room in ". |
| **`[ActivityDoor]`** inside `(Charades_InkSpace_MainHull_Sandbox)Variant` (key `84`, root -3838970831184783737) | 7410808331468609159 | (not a container; reach via key `84`) | 5 | **The canonical official double activity door**, with sign TMP text **"Rec Center"** (it is the Charades room's exit door). Local pos in the hull (-3.662,-1.503,-11.019). |

`TeleportationPortal` common fields: `JoinRoomSceneInviteMode 2`, `ForceNewInstance 0`, `CanCarryTools 0`,
`randomPointRadius 6`, `randomizeRotation 1`, `highlightRenderers` = [frame renderer, door-leaf renderer],
`_animator` = the door's Animator, `activityMenuRoot` null.

#### 2.1.2 The double activity door (from `[ActivityDoor]` 7410808331468609159) - build ours exactly like this
Root: BoxCollider size (2.04518, 2.14456, 0.196021) center (0, 1.07228, 0.04454) (door blocks you; we add our own
trigger in front). Pivot at floor centre of the doorway. **+Z is the side the player stands on** (derived: the emit
quad is at z 0.041 behind the leaves at z 0.094; the open pose rotates the left leaf (hinge x +0.906) by +21.4 deg
and the right leaf (hinge x -0.905) by -22.3 deg about Y, which moves both free edges to z ~ +0.33, i.e. the doors
swing out towards +Z, and the additive glow volume extends to z +0.39). So when placing a door in a wall, its local
+Z must point into the hall.

| child (path) | local pos | mesh (B43 pathId) | mesh AABB center / extent | material |
| --- | --- | --- | --- | --- |
| VisualRoot (Animator `ActivityDoor_Controller` #-7429917617384146160, LODGroup: LOD0 h>0.296, LOD1 h>0.0046) | 0,0,0 | | | |
| VisualRoot/ActivityDoor_Frame | 0,0,0 | `ActivityDoor_Frame` -5439803466483268628 (166 v) | c(0,1.072,0.045) e(1.023,1.072,0.098) -> **2.05 w x 2.14 h x 0.20 d** | `ActivityDoor_Mat` 2811243918909541752 |
| VisualRoot/ActivityDoor_Left | 0.906,1.008,0.094 | `ActivityDoor_Left` 4436320742719259628 (292 v) | c(-0.450,0.020,0) e(0.455,0.995,0.092) -> leaf 0.91 x 1.99 (hinge at the leaf's pivot = outer edge) | `ActivityDoor_Mat` |
| VisualRoot/ActivityDoor_Right | -0.905,1.028,0.094 | `ActivityDoor_Right` 4687823158366929900 (292 v) | c(0.450,0,0) e(0.455,0.995,0.092) | `ActivityDoor_Mat` |
| VisualRoot/ActivityDoor_Glow | 0,0.007,-0.023 | `ActivityDoor_Glow` -1082125464069504020 (28 v) | c(0.004,1.120,0.192) e(1.113,1.086,0.221) | `DoorGlow_Mat` -4151099522325754398 |
| VisualRoot/ActivityDoor_Emit | 0.002,0.889,0.041 | `ActivityDoor_Emit` -3973347701799025684 (4 v quad) | c(0,0.148,0) e(0.920,1.005,0) | `DoorEmit_Mat` -8584135234493109356 |
| VisualRoot/ActivityDoor_LOD1 | 0,0,0 | `ActivityDoor_LOD1` -4908219501708999700 (536 v) | c(0,1.072,0.069) e(1.022,1.072,0.115) | `ActivityDoor_Mat` |
| [Door_Sign] | 0.011,2.625,-0.008, rot Y -90, scale (0.793,1,0.793) | | | |
| [Door_Sign]/VisualRoot/Door_Sign_fbx/Door_Sign_LOD0 (+LOD1) | VisualRoot at (-0.217,-0.072,-0.059), fbx at (+0.217,+0.072,+0.059) | `Door_Sign_LOD0` 7570966806676982303 (32 v); LOD1 -8736043291076310497 | c(0,0.003,0) e(0.034,0.173,0.677) -> **1.355 w x 0.346 h x 0.069 d** (x0.793 = 1.07 m wide here) | `DoorSign_mat` 5145632641930865609 |
| [Door_Sign]/VisualRoot/Text | 0,0,0.059 rot Y -90, scale 0.024/0.023 | TMP `TextMeshPro` "Rec Center", colour black, font asset `FUTURA-T-OT-BOLD-CONDENSED SDF`, material `FUTURA-T-OT-BOLD-CONDENSED SDF (World Space)` #809285643245100712; fontSize 100, **auto-size 50..100**, horizontal Center, vertical 4096 (Geometry/middle), wrapping Normal. With scale 0.024 the line height is ~0.23 m at 100 and ~0.12 m at 50 (GUESS from TMP's 10 units per 100 pt); the rect is ~44 x 11 units (sign face 1.07 x 0.27 m; RectTransform not readable by rrtool) | | |

Single-door variant (Sandbox_RoomDoor / MagicDoor): frame `ActivityDoorSingle_Frame` 3122016981987988682
(216 v, c(0.450,1.072,0.048) e(0.573,1.072,0.094) -> 1.15 w x 2.14 h), one leaf `ActivityDoor_Left`
3965774427286443210 at (0.906,1.008,0.094), glow `ActivityDoor_Glow` 7323748068642100426 (c(0.488,1.121,0.192)
e(0.503,1.087,0.221)), emit quad `ActivityDoor_Emit` -5402273472432859958 (e(0.471,1.005,0)), `BackSideBlock`
(inactive, built-in cube scaled 0.9x2x0.05, `Paint_DarkGrey_Mat`), sign `Door_Sign` 1276667591726852526 (same size
as Door_Sign_LOD0) at (0.465..0.5, 2.314..2.45, 0.05) rot Y -90. Frame material = `ActivityDoor_Recolorable_Mat`.

#### 2.1.3 Door materials, textures, animation
| material (B43 pathId) | shader | textures | values |
| --- | --- | --- | --- |
| `ActivityDoor_Mat` 2811243918909541752 | `AG/Standard-Opaque` | `_BaseMap` ActivityDoor_tex 8480320123838684748 (1024^2 DXT5), `_BumpMap` Plastic_Norm@1df8e0ef, `_SpecGlossMap` ActivityDoor_Spec 942125678351842555 | BaseColor white, BumpScale 0.12, GlossMapScale 0.87, Smoothness 1 |
| `ActivityDoor_Recolorable_Mat` -189846955485444842 | `AG/Standard-Opaque` | ActivityDoor_Recolorable_tex -8605225492631622709 | **BaseColor rgba(0.094,0.420,0.867)** (blue tint) |
| `DoorEmit_Mat` -8584135234493109356 | `AG/Standard-Opaque` | none | BaseColor = EmissionColor rgba(1,0.985,0.9), **EmissivePower 2** (the lit glass/backlight behind the leaves) |
| `DoorGlow_Mat` -4151099522325754398 | `AG/Unlit-Additive-Soft` (bundle b1a0b840) | GradientHorizontal@f124b399 #468521523296599447 | BaseColor rgba(2,1.875,1.263,0) (HDR warm), TintColor rgba(1,0.972,0.816,0.472), InvFade 1.63 |
| `DoorSign_mat` 5145632641930865609 | `AG/Standard-Opaque` | DoorSign_Tex -6950139614368841531 (cream light-box gradient), Plastic_Norm, Plastic_Spec | BaseColor rgba(0.991,0.897,0.808), **EmissionColor white, EmissivePower 1.05** (backlit sign) |
| `Paint_DarkGrey_Mat` 8175432421411136506 | `AG/Standard-Opaque` | Paint_Tex/Paint_Norm/Paint_Spec | BaseColor 0.33 grey |

Texture look (exported to `SP\research\img\rc\*.png`): `ActivityDoor_tex` = light oak wood leaf with a tall narrow
**wired-glass window** (upper left of the leaf), a dark grey kick plate at the bottom, dark grey hinges/pulls, and a
**steel-blue** frame area (rgb ~ 0.19,0.40,0.55). `ActivityDoor_Orange_tex` (-7546989565223360369) = the same with a
**rust-orange** frame (~0.54,0.30,0.19). No material in the bundles uses the orange texture, so it is **not loadable**
(only container-reachable assets load). `ActivityDoor_Recolorable_tex` is the same atlas with a **light grey (0.76)
frame area** (alpha 255 everywhere, no recolour mask): the recolorable material tints the whole texture, but it is
only put on frame meshes, so a tinted instance of it on the frame renderer gives any frame colour (3.3). Both atlases
share the UV layout, so it also fits the double door's `ActivityDoor_Frame` mesh.

Depth-priming note: all these are `AG/Standard-Opaque` (has a DepthNormals pass, OK). `AG/Unlit-Additive-Soft` is
transparent - fine.

**Animator `ActivityDoor_Controller`** (-7429917617384146160): two states, `ActivityDoor_Closed` (4863844763800600144)
and `ActivityDoor_Open` (7223134250224600966), transitions 0.25 s (exit time 0.869), speed 1. Both clips are constant
poses:
* Closed: Left leaf euler (0,0,0), Right leaf (0,0,0), glow renderer `_TintColor` = (1,0.972,0.816, **0**).
* Open: Left leaf euler (0, **+21.44**, 0), Right leaf (0, **-22.27**, 0), glow `_TintColor` alpha **0.834**.
So "Open" is the hover/approach state: both leaves swing ~22 deg ajar and the warm additive glow fades in over
0.25 s. The Animator's parameter names are hashed (4 ids: 2788345602, 3743790454, 3984510375, 877234570); we will
not drive the Rec Room Animator - **disable it and animate the two leaf transforms + the glow material's `_TintColor`
alpha ourselves** (0.25 s smoothstep).

#### 2.1.4 Runtime recipe: getting 21 double doors
1. Load B43 plus the bundles its Unity components reach (`RrReach`), then `LoadAsset<GameObject>("84")` (the Charades
   hull). Instantiate it under an **inactive** parent (runtime facts), `Find("[ActivityDoor]")` (direct child of the
   hull root), `Instantiate` that child once as our door template (still inactive), then destroy the hull instance.
   The meshes/materials stay resident (our template references them). If the hull's memory cost is too high, the
   fallback is the single door `(Sandbox_RoomDoor)` key `2ac` (frame 1.15 m wide) scaled or doubled.
2. On the template: disable `VisualRoot`'s Animator; keep the LODGroup; the root BoxCollider (2.045 x 2.145 x 0.196,
   centre (0, 1.072, 0.045)) is the solid door (layer 23); strip nothing else (missing scripts are inert). Add our
   `RrDoor` component and a click collider on layer 16 (4.1).
3. Per door clone: set `[Door_Sign]/VisualRoot/Text` TMP text; give `VisualRoot/ActivityDoor_Glow` its own
   `_TintColor` (MaterialPropertyBlock or material instance; alpha 0 when closed); optionally swap the frame
   renderer's material (`VisualRoot/ActivityDoor_Frame`, and `ActivityDoor_LOD1` for distance) to a tinted
   recolorable instance (needs key `2ac` loaded once to make `ActivityDoor_Recolorable_Mat` resident).
4. Leaves to animate: `VisualRoot/ActivityDoor_Left` (hinge at its pivot, local pos (0.906, 1.008, 0.094)) and
   `VisualRoot/ActivityDoor_Right` (pivot (-0.905, 1.028, 0.094)); rotate about local Y.

### 2.2 Loading screen (`[LoadingScreen]`, Startup bundle 063b6b66, pathId -8342395632758303812)
Child of `(Startup)` (-5896860621545679940, key `7`) / UI. Canvas `RenderMode 0` (Screen Space Overlay),
CanvasScaler scale-with-screen. `LoadingScreen` fields:
`fadeInDuration 0.15`, `fadeOutDuration 0.15`, `minTotalDisplayDuration 5`, `minDisplayDurationPerTip 2`,
`maxDisplayDurationPerTip 5`, `displayDurationPerElipse 0.3`, `customRoomScreenTitle "Room Cheers"`,
`customRoomScreenSubtitle "Cheer and Favorite any room in the This Room section of your Watch Menu"`,
tips from a localisation table (not in the plain bundles - write our own tips), `recroomLogoAsset` =
`RecRoomLogo_Sprite_Shadow` (addressable GUID e60f188928b2a8f42b57abacc61a2eaa), `textureArrayBlitShader`
`PlayStationVR/TextureArrayBlit` (VR compositor path).
Hierarchy: Canvas / ImageTemplate / {BackgroundFill (Image), ImageContainer/BackgroundImage (RawImage +
AspectRatioFitter), BottomRow (SurfaceTheme, UIGradient) / {TitleText, MessageText}, NextRoom (VerticalLayout) /
{BgImage/GradientImage, **Goingto** TMP "GOING TO", Room Details / {RoomNameText "^roomname...", SubRoomNameText},
[LabeledProgressBar] / {[ProgressBar] "INK" + PercentProgressText "50%"}, LoadingText "LOADING...", DownloadText
"DOWNLOADING ASSETS 82%\n54 Kb/s" (inactive)}, Logo (inactive, Animator), Avatar/{AvatarShadow, AvatarImage}},
ImageFade (black Image used for the fades). Font sizes 40/36/32/24. Colours used: white text, black fade,
accent rgba(0.992,0.510,0.302) (Rec Room orange), rgba(0.945,0.878,0.847) (warm off-white).
The rounded corners/gradients are Rec Room scripts (missing at runtime) - rebuild the panel ourselves with
these texts/colours (see 4).

### 2.3 Rec Center furniture and props (all B43 unless noted)
Pivots: many Rec Room props have their pivot at the mesh centre, not the floor (couch, stool, coffee table, seat).
**Always ground them from bounds at runtime**: after instantiating (inactive parent), compute the union of all
enabled `Renderer.bounds` and shift so `bounds.min.y == floorY`. The "size" column is the full size (2 x extent) of
the LOD0 mesh; W = x, H = y, D = z in mesh space (then the prefab/child transforms apply).

| prefab root | pathId | key | LOD0 mesh (pathId) | size W x H x D (m) | material(s) (shader) | Rec Room scripts (inert) / notes |
| --- | --- | --- | --- | --- | --- | --- |
| `(RecCenter_Couch_Sandbox)Variant` | 307189170882659907 | `ed9` | RecCenter_Couch_LOD0 5301908925531971550 | 0.91 x 1.08 x 2.62 (long axis = Z) | Couch_Minimalist_Brown_Instanced_Mat (AG/Standard-Opaque-Instanced) + invisible seat-highlight meshes (AG/DoNotRender) | 3 seats (SeatPortal x6, SeatManagement). The real Rec Center couch. Seat highlight mesh `RecCenter_Couch_Seat_Highlight` (0.76 x 0.16 x 0.79) marks each seat. |
| `(Sandbox_Couch)` | -1532349014166191393 | `c5` | Couch_LOD0 7219601855559821854 | 2.50 x 0.88 x 0.92, pivot centre | Couch_Plush_Blue_Mat | 3 seats (highlights L/M/R at x -0.72/0/+0.72, y -0.10) |
| `(Sandbox_CouchChair)` | -409350397979008085 | `f0` | CouchChair_LOD0 4808764673505244189 | 1.13 x 0.90 x 0.92 | Couch_Plush_Blue_Mat | 1 seat |
| `(Chair_Beanbag_Sandbox)Variant` | 5696760868194268639 | `ec1` | Chair_Beanbag_LOD0 -6022869828372955936 | 1.13 x 0.71 x 1.06 | Chair_Beanbag_Instanced_mat (instanced, InstanceColor) | seat |
| `(Chair_Cafe_Round_Sandbox)Variant` | -4416211188283966690 | `43` | Chair_Cafe_Round_LOD0 -4140423911899414472 | 0.65 x 0.94 x 0.68 | Chair_Cafe_Round_Instanced_Mat | seat |
| `(Chair_Moveable)` | 7009576536407601616 | `ed` | chair_stackable_LOD0 -7595137393900023162 | 0.62 x 0.98 x 0.56 | Chair_Stackable_Wood_mat | seat, pick-up-able |
| `(Stool_Wood_Round_Sandbox)Variant` | -7524920951910648326 | `58` | Stool_Wood_Round_LOD0 -3227611541696513892 | 0.46 x 0.98 x 0.46 | Wood_Horizontal_DarkBrown_Polished_Mat | bar stool (cafe counter) |
| `(Sandbox_Stool)` | -4001956623906876910 | `0ca` | Stool_LOD0 2277103374561093798 | 0.61 x 0.86 x 0.61 | | seat |
| `(Table_Cafe_Recolorable_Sandbox)Variant` | 6836263498621831766 | `d9` | Table_Cafe_LOD0 -2664081120676160106 | 1.74 x 0.79 x 1.74 | Table_Cafe_Mat | |
| `(Table_Cafe_Short_Sandbox)Variant` | 7930058020835849771 | `c48` | Table_Cafe_Short_LOD0 -344973239310036775 | 1.20 x 0.83 x 1.20 | Table_Cafe_Short_mat | cafe table |
| `(Table_Cafe_Tall_Sandbox)Variant` | 6804294882141902652 | `735` | Table_Cafe_Tall_LOD0 -3452708569976040738 | 1.20 x 1.27 x 1.20 | Table_Cafe_Tall_mat | standing table |
| `(Table_Coffee_Curvy_Sandbox)Variant` | 7807009429136648898 | `536` | Table_Coffee_Curvy -3139523759522879572 | 2.06 x 0.44 x 1.33 | Table_Cafe_Curvy_Mat | lounge coffee table |
| `(Sandbox_Table_Coffee_Large)` | -6593861199370616417 | `1a` | Table_LOD0 -1119682663172646350 | 2.38 x 0.68 x 1.23, pivot centre | Wood_Horizontal_DarkBrown_5x_Mat | |
| `(Sandbox_Table_Short)` | 1294817617570128736 | `c72` | LockerTable_Short_LOD0 -6138348630411110873 | 1.21 x 0.44 x 0.62 | Wood_Table_Mat | "LockerTable" = Locker Room bench-table |
| `(Sandbox_Table_Tall)` | -695709540252901845 | `21` | LockerTable_Tall_LOD0 -1234067364966995976 | 1.52 x 0.80 x 0.78 | Wood_Table_Mat | |
| `(Sandbox_Table_Meeting)` | -5170338335639713336 | `d` | MeetingTable_LOD0 4668178034577450256 | 5.76 x 0.84 x 2.29 | Wood_Horizontal_DarkOrange_5x_Mat | |
| `(RecCenter_Cafe_CoffeePot)` | -6802294012789304814 | `99` | RecCenter_Cafe_CoffeePot -265236013762936983 (3 submeshes) | 0.32 x 0.34 x 0.38 (root scale 0.886) | RecCenter_CafeAssets_Mat, Glass_Teal_mat (transparent), RecCenter_CoffeePot_Coffee_Mat (AG/Fresnel) | DrinkableBottle (you can drink from it), ShakeAudio; clips RecCenter_CoffeePot_Pickup_01, Goblin2_Potion_Gulp01@1df8e0ef |
| `(RecCenter_Cafe_Mug)` | 7962773710793404200 | `e5b` | RecCenter_Cafe_Mug_LOD0@1df8e0ef 8167679834603284670 | small mug (~0.1 m) | RecCenter_Cafe_Mug_Mat | ToolSkinMapper (mug skins Area87, CautionTape, Chalice, Galaxy, Hammered_Copper, UglySweater in 5c91ea11); clip RecCenter_CoffeeCup_Pickup_01 |
| `(RecCenter_Cafe_Plate)` | 7902869567407032666 | `20b` | RecCenter_Cafe_Plate_LOD0 -4954649621243297409 | 0.30 x 0.03 x 0.30 | RecCenter_CafeAssets_Mat | clip Quest_Pirate1_Plate_Pickup01 |
| `[RecCenter_Cafe_Shelf]` (not a root) | mesh RecCenter_Cafe_Shelf -6550607793947971865 | - | | | RecCenter_CafeAssets_Mat | only inside other objects; use `(Sandbox_Shelves_Short/Tall)` or procedural shelves |
| `(Sandbox_Bell)` (reception bell) | -6493488885355874438 | `45b` | Bell_LOD0 -6648244280105831510 | 0.087 x 0.057 x 0.087 | ReceptionBell_Mat (tex RecCenter_ReceptionBell_tex) | `ReceptionBell {minTimeBetweenRings 0.25}` + AudioSource (clip ReceptionBell f55c11a1 key `9`, 8.55 s mono, volume 0.518, custom rolloff, min 1, max 200). SimpleSphereVolume r 0.5 = the hit zone. Ring on hand touch / click. |
| `(Radio)` | -7125182807003965041 | `f3` | Boombox_LOD0 6128757217277518295 | 0.13 x 0.43 x 0.31 | Boombox_Mat | `Radio {tracks: 41 SFXTracks, randomInitialTrack 1, pauseDuringGameInProgress 0}` - playlist in 2.6 |
| `(AmbienceRadio)` | 2043062669133516778 | `886` | same boombox | | | `Radio {8 ambience tracks, defaultToTrackLoop 1}` |
| `(BowlingAlley_Jukebox_Sandbox)Variant` | 9197836360092917472 | `48` | jukebox 5844772981663359087 | 0.63 x 1.75 x 1.25 (root scale 1.121 -> ~0.70 x 1.96 x 1.40) | JukeBox_Mat (EmissivePower 2.1), Glass_Clear_mat | `Radio {15 tracks, pauseDuringGameInProgress 1}`, 2 SimplePhysicalButtons (prev/next), TMP "Track Text". Root has a baked position (-1.863,0.387,32.997) - reset it. |
| `(VendingMachine)_SpawnableTool` | -6827405341831640064 | `4ea` | VendingMachine -953006395689189755 (2 submeshes) | 1.15 x 2.01 x 1.04 | VendingMachine_Mat, VendingMachine_Glass_Mat | `VendingMachine` + 4 buttons + ToolPool of 5 water bottles; clips VendingMachine_working, VendingMachine_drop, Charades_ButtonPress |
| `(Sandbox_Friendotron)` | 4167279793516974501 | `bbd` | same VendingMachine mesh | 1.15 x 2.01 x 1.04 | VendingMachine_FriendoTron_Mat | `Friendotron` + 4 buttons. The Rec Center's Friend-O-Tron (Sep 2021). UI prefab `(FriendotronMain)` ef9b849f key `f4`. Root has baked pos (-1.88,-0.013,3.613) - reset. |
| `(WaterBottle)` | -6214342394501383043 | see containers.tsv | WaterBottle_LOD0 4671925464522694165 | 0.086 x 0.24 x 0.086 | WaterBottle_Mat | DrinkableBottle |
| `(Sandbox_Lockers)` | 4986313387930100660 | `d5c` | Lockers_LOD0 -7684154969080968724 | 2.10 x 2.00 x 0.72 | LockerFountainStageLightMakerpen_mat (BaseColor rgba(0.30,0.50,0.62) teal-blue) | the old "Locker Room" lockers |
| `(Sandbox_Houseplant_Dracaena)` | -2609047512322496204 | `ddd` | Dracaena_LOD0 1262724994631689701 | 1.09 x 1.04 x 1.11 | HousePlants_mat, HousePlants_Pot_mat | |
| `(Sandbox_Houseplant_Fig)` | 4859817174503967356 | `cb6` | Fig_LOD0 2034646174600628502 | 0.77 x 1.85 x 0.84 | same | |
| `(Sandbox_Houseplant_SnakePlant)` | 5684304667218242089 | `ce6` | SnakePlant_LOD0 8490777362792521009 | 0.39 x 1.39 x 0.41 | same | |
| `(Light_Pendant_Sandbox)Variant` | -7078574993146532614 | `3aa` | Light_Pendant_LOD0 -942080533585295057 | 0.52 x 1.42 x 0.52, pivot at the TOP (hangs down to y -1.42) | Light_Pendant_Orange_mat (AG/Standard-Opaque-TwoEmissionChannels, EmissionColor HDR (2.64,0.77,0.24)) | no Light component - add a point light ourselves |
| `(Light_Strip_Short_Sandbox)` | 7222686395682522939 | `53e` | Light_Strip_Short_LOD0 8468916779002822795 | 0.10 x 0.05 x 0.50 | Light_Strip_Animating_Mat (AG/Standard-Opaque-Emission-Scroll, yellow) | marquee strip |
| `(BulletinBoard_Sandbox)Variant` | -8423269555302806145 | `7ca` | BulletinBoard -8477503375934492625 | 0.10 x 1.81 x 3.05 (board faces +/-X) | DormRoom_BulletinBoard_mat | the community board frame |
| `(CommunityBoard)_RecCenter` (6d3223da) | -7816176551773483040 | `2d` | UI only (BulletinBoardController + panels FeaturedCreator, FeaturedRooms, Instagram, Announcement, Youtube at local z -1.04..+0.85, y -0.71..+0.39, canvas scale 0.002) | fits on the BulletinBoard | UGUI | content came from rec.net (dead): use only the layout; fill panels with our own texts (activity list, welcome). |
| `(Sandbox_CodeOfConduct_Poster)` | -9153174756256126895 | `760` | PaperMesh 8810772129851369118 (quad) | 0.71 x 0.92 | CodeOfConduct_Mat + TMP (FUTURA-T-OT-BOLD SDF) | CodeOfConductPoster |
| `(Sandbox_MuteGesture_Poster)` | -5503512149270850701 | `ef` | Poster_Dorm -3971044515679710320 (authored x100; child scale shrinks it) | ~0.8 x 1.0 (GUESS, measure bounds) | Poster_TalkToTheHand_Mat | |
| `(Sandbox_ShakeHands_Poster)` | 6657100259046836956 | `b8f` | Poster_Dorm | same | Poster_ShakeHands_Mat | |
| `(Sandbox_Showcase_Poster)` | 2332708627528015599 | `72` | BackingPanel -310033596781856914 | 1.34 x 1.93 | Poster_ContentShowcase_Frame_Mat; image = DefaultProfilePic | frame for our own images (activity posters) |
| `(SandboxWelcomeMat)` | 8578281131999620466 | `d4` | WelcomeMat 1180740731972521389 | 0.43 x 0.01 x 0.63 | Sandbox_WelcomeMat_Mat | at the Dorm door |
| `(Sandbox_Mirror)` | -6592890460863086279 | `544` | Frame -4842667125005389059 | 2.12 x 2.29 | Mirror_Dorm_Mat (AG/Mirror) | skip (mirror shader) |
| `(PunchingBag)_SpawnableTool` | -938117583273895998 | `88` | meshes in 1df8e0ef (PunchingBag_Dummy_LOD0 ...) | ~human size | | 4 ConfigurableJoints: punchable dummy (also in the Dorm); nice next to the Punch Up door |
| `(Sandbox_Minifridge)` | -7423277400919981124 | `c6a` | Fridge -384361085709173453 | 0.77 x 1.00 x 0.77 | Minifridge_mat, Glass_Teal | cafe |
| `(Sandbox_MocktailMixer)` | 2691050225637107153 | `98` | MocktailMixer_LOD0 6642420660025410689 | 0.15 x 0.37 x 0.15 | Metal_Grey_Gloss_Mat | + glasses `(Sandbox_Mocktail_Goblet/Margarita/Martini/Milkshake/Stemless/Tall)` keys `3e`,`297`,`e7`,`dd`,`2971`,`638` |
| `(Sandbox_Shelves_Short)` / `(Sandbox_Shelves_Tall)` | -7080352528632537005 / 2777746776106137644 | `fe5` / `57c` | Bookshelves_Tall_LOD0 836765798633813274 | 1.0 x 0.5 x 2.0 (tall; mesh authored Z-up) | Bookshelf_Mat | |
| `(TrashBin_Sandbox)Variant` | 7458994975604054613 | `0c` | SciFi_TrashBin_LOD0 -5364928341134879601 | 2.62 x 1.34 x 4.12 (dumpster size) | Dumpsters_Recolorable_mat | too big - skip |
| `(Speaker_Concert_Sandbox)` | 6180041966138656744 | `5` | Speaker_Concert_LOD0 | 0.45 x 0.66 x 0.32 | Speaker_Concert_mat | stage speaker |
| `(StageLight_Can_Sandbox)` | 8214013988573972516 | `58e` | StageLight_Can_Body 4912756467522965284 | 0.91 x 0.60 x 0.62 | StageLight_Can_Mat + fog cone | has a Unity Light + URP additional light data (strip the URP data per runtime facts) |
| `(Curtain_Sandbox)Variant` | 7240058040526082005 | `5f` | Curtain_LOD0 -7393571527988818726 | 4.93 x 6.0 x 0.61 | | stage curtain |
| `(Charades_Stage)` | 3323210107304673527 | `23f` | Charades_Stage -3701533869647177527 | 3.71 x 0.39 x 10.02 | Metal_StepTrim_Mat, Carpet_Purple_Mat | ready-made low stage; root baked pos (37.117,3.922,-20.779) - reset |
| `(Sandbox_Speaker)` | -5932272720997597246 | `a2f` | Speaker_LOD0 | 0.45 x 0.66 x 0.32 | Speaker_mat | VoiceLineControl |

### 2.4 Games in the hub: pool, ping-pong, poker/game tables, basketball, darts
| prefab root | pathId | key | size | notes |
| --- | --- | --- | --- | --- |
| `(PoolTable)` | -2137806567733975631 | `ee` | TableTop mesh 8485108020454680227: 2.98 x 0.27 x 1.53; overall ~2.98 x 0.74 x 1.53, long axis X. Cloth top y ~0.697, rails y 0.737, 6 pockets | `RecRoom.PoolTable {poolBallPrefab [PoolBall], resetTableAudioClip Pool_ResetTable_01, surfaceCenter, ballSurfaceCenter, ballReturnPoint, cueBallReturnPoint, frontBallResetPoint, cueBallResetPoint, cueBallDropPoint, resetButton8Ball, resetButton9Ball}`; 18 PoolTableCushion, 6 PoolTablePocket(+Vacuum), 7 PoolTableSurface; two ChargePhysicalButtons with TMP "8-ball" / "9-ball". Pool_Table_Mat. From the Bowling Alley (`BowlingAlley_PoolTable_fbx`). Sounds (bundle:key when a container): Pool_BallEntersPocket_01 0be89c4f:6, Pool_BallHitsBallLoud_01 a7cc02c4:5, Pool_BallHitsCushion_01 51a2e9dc:4, Pool_CueHitsBall_01 60768bed:7, Pool_CueHitsFloor_01 b346782b:5; Pool_ResetTable_01, Pool_BallHitsBallQuiet_01, Charging, SandboxMachineRecycleSingle3D are B43 non-containers (resident with key `ee`, GUESS for the quiet hit). |
| `(PoolCue)` | -6383371098871327646 | `4f` | 0.035 x 1.51 x 0.035 (y 0..1.50) | PoolCue, PoolCueAimGrip, PoolCueRigidbodyPickup (two-hand aim); clip Pool_CueHitsBall_01@60768bed |
| `(PoolCueRack)` | 7460407601710766675 | `d6` | 1.30 x 1.18 x 0.11 (wall rack) | BowlingAlley_Assets_Mat |
| `(PoolTriangle)` | -7644449635642145061 | `ad` | 0.33 x 0.04 x 0.38 | rack triangle |
| `[PoolBall]` (inside PoolTable, layer 24) | -4370770082079619811 | via `ee` | radius 0.0315 m | PoolBall_Mat (AG/Standard-Opaque-InstancedST, atlas PoolBalls_Tex) |
| `(Sandbox_Table_PingPong)` | -1362248597509205463 | `7cd` | PingPongTable -1134645092560256000 (mesh authored Z-up; root rotated 90 deg Y): ~2.74 long x ~1.85 wide incl. net posts, top ~0.76 (GUESS from extents 0.93/1.37/0.46 - verify with bounds) | PingpongTable_Mat, PingpongTable_Net_Mat (transparent). No scripts. |
| `(Pingpong_Paddle)` | 4290896271436088691 | `64` | 0.15 x 0.03 x 0.27 | `Paddle` (inert) - own hit logic needed |
| `(PingpongBall)` | -1004661230452129383 | `568` | radius 0.02 | `Ball`, CollisionAudio |
| `(Sandbox_GameTable)` | -7710282719875035375 | `51` | TableBase 1971635664728222593: 2.33 x 0.88 x 2.42 | round poker/card table (DrawingSurface top, Sandbox_GameTable_mat). With 6-8 `(Sandbox_PokerChair)` (-7939893472342937302, key `ebf`, 0.84 x 0.84 x 0.82, PokerChairsSeat_Mat) and `(SandboxDiceSet)`. Sounds: Poker_ChipClack01 cb06a1bb:f; Poker_DiceClack03 / Poker_DicePickup01 are B43 non-containers (GUESS resident with `(SandboxDiceSet)` key `6d`). |
| `(Sandbox_GameTable_Small)` | 8018390395461510705 | `cac` | 0.97 x 0.78 x 1.01 | small card table |
| `(Basketballhoop)` | 6058454435073796307 | `78` | Goal_LOD0 -3297046497160156249: 1.83 w x 0.62 h x 1.22 deep, pivot at the top of the backboard (mesh y -0.62..0); Rim.Proxy 0.50 x 0.51 | BasketBall_Goal_Recolor_Mat (instanced). No scoring script; wall mounted. Rim height: use 2.75 m (GUESS; real 3.05). |
| `(Basketball)` | 5755647178341420997 | `d2` | radius 0.124 | `Basketball` (inert), TrailRenderer; clips basketballPickUp, Throw01@d5ff127b, DingDing@cc5b30c7 (score ding); VFX `[VFX_Basketball_PassedRim]` |
| `(DodgeballBall)` `1bf`, `(DiscGolfDisc)` `9d`, `(SoccerBall)` `00`, `(Bucket)` `55`, `(SoloCup)` `4ba` | | | | loose toys that lay around the real Rec Center (wiki) |
| `(Dart)` | 3920024455142888564 | `fe7` | 0.045 x 0.045 x 0.154 | `Dart` script, Dart_Case_Full_Mat. **No dartboard mesh exists in the plain bundles** - procedural board (0.45 m disc, bullseye 1.73 m high) or skip. |

**Arcade machines: none in the plain bundles** (no cabinet/arcade/pinball/claw meshes).
**Kiosks:** only `Sandbox_PopUpShop_1.._10_Storefront` / `Sandbox_DiscoveryPopupShop_Storefront` (B43, e.g.
-5217890641710351313; heavy server-driven UI with 12 avatar mannequins, 48 animators, 36 particle systems; sounds
Kiosk_ButtonPressedNext, Arena_CashRegister01@56515818, MoveIn). Patch notes (Oct 2019) mention "the kiosk in the
Rec Center" with a counter. Build a simple Merch Booth / kiosk counter procedurally and use the `Kiosk_*` sounds.

### 2.5 Signage and illustrations
* Door signs: backlit `Door_Sign` + TMP text (2.1.2). Font asset `FUTURA-T-OT-BOLD-CONDENSED SDF`, world-space
  material #809285643245100712. Use it for every sign we make (TMP binds at runtime).
* Big section signs ("QUESTS", "SPORTS", ...): no prefab. Procedural: a `Door_Sign` mesh scaled x3 wide, or TMP text
  on a painted panel, Rec Room orange rgba(0.992,0.510,0.302) / white.
* UI illustrations (atlas `RRUI_Illustration_Atlas(1024)`, f124b399): `illustration_HomeButton_RecCenter` (256x128,
  key `bf9`), `illustration_HomeButton_DormRoom` (key `597`), `Illustration_Menu_RecCenter`, `Illustration_Menu_Dorm`
  (256x256), `icon_AppNav_RecCenter`. The Rec Center illustration shows a **tan civic building, orange-red roof band,
  arched entrances, green dome with a flag** (`SP\research\img\rc\illus0.png`): use it on the loading card, and as the
  motif for the exterior/entrance sign.
* No per-activity room images exist in the bundles (rec.net served them). Loading-screen pictures for activities must
  be our own renders (see 4).

### 2.6 Music and ambience
All radio/jukebox tracks are Addressables containers (`bundle:key`; `LoadAsset<AudioClip>(key)`). Full table:
`SP\research\_rc_tracks.tsv` (owner, index, SFXTrack pathId, friendlyName, clipType 1=music 2=ambience, clip name,
clip bundle, clip pathId, bundle:key).

`(Radio)` playlist (41, random start): Sport Funk (db607c4f:d), In The Bassment (f50dad4f:0), Secret Room
(9a0da6ad:4), Goat Attitude (059acbe5:f), Organism (70d520f6:1), River Predator (e84539de:f), Flight Beam
(6ba20137:9), Electar (b2a8ed09:c), JumboTron Explore (410620fe:0), Hangar (9fbf7292:4), Clearcut (c5e8887b:0),
**Food Court (8477d5ee:3)**, Goblin Work Song (09ebc2f0:7), Range Finder (95dc9c09:9), Spillway (954c197d:0),
Golden Trophy Explore (48e65ed9:f), Golden Trophy Combat (0bcf564a:8), JumboTron Combat (d036b095:2), BeeDM
(e03514bb:d), Stunt Runners (330ba010:6), Stunt Runners Final Lap (26a5f0a8:6), Victory Lap (a7e68bf6:9), New Jack
Runner (eb3e5db6:1), Crescendo Ballroom (8536d664:6), Dodge Baller (3be52731:f), **Lone Shoe Blues (101efd74:1)**,
Shanty (53ac5b38:c), Pirate Shopping Channel (228ee7f6:f), Crescendo Gavotte (3592c282:4), Intro (212aedc9:e),
**Rec Room Theme (b203e635:9, 60.6 s)**, Crescendo Boss (1b69480d:0), Crescendo Miniboss (ce2d2a2f:7), Cyberjunk
Combat (853e5df3:4), Cyberjunk Explore (4de47222:4), Bowling (524b224f:6), Crimson Cauldron Mines (d37aa2e8:7), The
Ballad of VotaVader (00b72637:1), Shanty Piano Version (f2b57b6b:1), Pirate Brawl (8acf4c75:5), Homestead Varmint
(764a3f17:8). Bundle prefixes are 8 hex chars; full names in `_rc_tracks.tsv`.

`(AmbienceRadio)` (8 loops): Outdoor Birds = AmbianceSummerOutdoor (170d5064:1, 117.8 s), Outdoor Rain
(18ef1d8f:d), Spooky Cave (0e82ce18:9), Device Fan = Kiosk_PowerUpAmbianceLoop (5a81b24c:9, 4.0 s), Crowd =
Quest_Pirate1_CrowdAmbiance (6f057ebd:e), River (6a40b22f:f), Spooky (f515f334:8), Crickets (4d9cb9fb:e).

`(BowlingAlley_Jukebox_Sandbox)Variant` (15): Charades Ink Space (7b3ac31f:a), Charades Piano Lounge (d386dc98:6),
Lone Shoe Blues, Clearcut, JumboTron Explore, JumboTron Combat, River Predator, Range Finder, Cyberjunk Explore,
Hangar, Goblin Work Song, Shanty Piano Version, Pirate Shopping Channel, Stunt Runners, Victory Lap.

Recommended hub audio: no global BGM (GUESS: the real hub relied on radios and player voices); a low crowd bed
`sfx_onboarding_outdoor_ambiance_local_people02` (f981a603:a, 27.1 s stereo loop, "local people" chatter) at about
-20 dB in the lobby; `AmbianceSummerOutdoor` very low near the windows; a Radio on the cafe counter playing **Food
Court**, **Lone Shoe Blues**, **Rec Room Theme** (3D AudioSource, spatialBlend 1, maxDistance 15 m); the jukebox in the
lounge with its own 15-track list.

Other clips:
| clip | bundle:key or pathId | length | use / loadability |
| --- | --- | --- | --- |
| ReceptionBell | f55c11a1:9 | 8.55 s mono | reception bell |
| Kiosk_ButtonPressedNext | ae19bb5b:7 | 0.40 s | kiosk button |
| Kiosk_StateChange01 | 47870ee7:b | 0.80 s | kiosk state |
| Kiosk_CardDispenseWarmup | fb2d438f:1 | 2.43 s | kiosk |
| Kiosk_PowerUpAmbiance | 94fc0e87:c | 10.0 s | kiosk power-up |
| Kiosk_PowerUpAmbianceLoop | 5a81b24c:9 | 4.0 s loop | kiosk hum |
| Kiosk_SelfieCountdownBeep | 16079ee0 #3644824455668649059 | 0.21 s | not a container (watch UI bundle) |
| MagicDoor_Refresh | B43 #-2651044525266811996 | 3.21 s mono | not a container; resident after loading `(Sandbox_MagicDoor)` key `2a9` |
| GenericTeleport01 | c513202a #7970199534627750861 | 0.30 s | not a container, no referencing prefab found (watch/notification UI bundle). GUESS resident with the watch UI root. |
| music_ui_enter_room_guitar_01 | 063b6b66 #6425627509938406257 | 3.67 s | not a container, not referenced by any bundle object (code-referenced) -> **not loadable** |
| ActivityTitleJingle | 063b6b66 #-169042326832854528 | 3.09 s | same -> **not loadable** |
| RecRoomTheme2 | b203e635:9 | 60.6 s | usable: first ~3.5 s with a 0.5 s fade as the "arrival" sting |
| CrowdCheer01 | B43 #-1865890325946663720 | | crowd cheer (not a container) |
| VendingMachine_working / VendingMachine_drop | B43 (resident with key `4ea`) | | vending |

### 2.7 Surface materials for the procedural shell (where to get them)
Surface materials are not containers; they become resident when a container that uses them is loaded. Measured
(tree of each container, `SP\research\_rc_matscan.tsv`):

| load this container | makes resident (B43 pathIds) |
| --- | --- |
| `(Charades_InkSpace_MainHull_Sandbox)Variant` key `84` (needed anyway for the double door) | **Wood_Floor_Matte_mat** -541357418168945416 (BaseColor (1,0.735,0.491), tiling 10x40), **Wood_Floor_Faded_Matte_Mat** 3519925957840248261 ((0.688,0.577,0.480)), **Paint_White_Mat** 1996823796982839334 (warm off-white (0.941,0.875,0.821), Paint_Tex, tiling 4x4), **Paint_Warm_Grey_Mat** -5086561319790444796 ((0.453,0.409,0.374)), **Paint_Dusk_Mat** 8786149072629186884 ((0.594,0.502,0.502)), **Brick_Red_Mat** -2341577564581049127 (Brick_Grout_tex), **Carpet_LowPile_Striped_Blue_Mat** 833149777438026330 ((0.337,0.527,0.643)), **Light_Fluorescent_mat** -5006483227839729767 (EmissivePower 3.51, emission white), Light_Fluorescent_Dim_mat, **Ibeams_Orange_mat** -8048217554787188942, Light_Pendant_Orange_mat, Light_Pendant_Yellow_mat, Lights_Poker_mat, Metal_Copper/Dull_Dark/LightGray/LightGrey_Dull, Plaster_Black_mat, Vent_Modular_Mat, **ExitSign_mat** (Exit_Tex, emissive), **Poster_StayHydrated_Mat**, **Poster_OpenMic_SignUp_Mat**, Charades_Posters_Mat, Scifi_Rug_Mat, Wood_ParticleBoard_PaintSplats_Mat, RecCenter_CafeAssets_Mat, Chair_Cafe_Round_Instanced_Mat, DormRoom_BulletinBoard_mat, StageLight_Can_Mat, all ActivityDoor/DoorSign/DoorEmit/DoorGlow materials, TMP Futura Bold Condensed. |
| `(Sandbox_Wall)` key `67f` | **Paint_Blue_Mat** -4120628079863786121 ((0.218,0.558,0.840)), Paint_Dusk_Mat, **Paint_LightGrey_Mat** 7039450671234792075 (white) |
| `(Sandbox_Floor)` key `3ea` | **Tile_Linoleum_Brown_Mat** 4887102829130047087 ((0.529,0.448,0.448), tiling 50x50) |
| `(Sandbox_Ceiling)` key `ce4` | Light_Fluorescent_mat, Paint_Dusk_Mat, Wood_Horizontal_RedBrown_mat@1df8e0ef |
| `(BowlingAlley_Jukebox_Sandbox)Variant` key `48` | **Glass_Clear_mat** (AG/Standard-Transparent) - window glass |
| `(Sandbox_RoomDoor)` key `2ac` | **ActivityDoor_Recolorable_Mat** (frame recolour, see 3.3) |
| direct material containers | Concrete_LightGrey_2x_Mat (1df8e0ef:d5c), Wood_Horizontal_RedBrown_mat (1df8e0ef:fb5), Metal_MedGrey_Eggshell_Mat (1df8e0ef:257) |

Find them after loading with `Resources.FindObjectsOfTypeAll<Material>()` by name. For procedural meshes, make
**instances** (`new Material(src)`) and set the tiling for metre UVs: `SetTextureScale("_BaseMap", ...)`. Suggested
texture world sizes (GUESS, tune by eye): `Wood FloorTiled_tex` = 5 plank rows per tile (see
`img\rc\WoodFloorTiled_tex.png`) -> 1 tile per 1.25 m; Paint_Tex -> 1 tile per 2 m; Carpet -> 1 per 1 m; Linoleum ->
1 per 0.5 m; Brick -> 1 per 1.2 m. Any custom colour (e.g. Rec Room orange accents) = an instance of
`Paint_White_Mat` with `_BaseColor` changed; `Paint_Orange_Mat`'s own value is (0.953,0.477,0.140).
All of these use `AG/Standard-Opaque` (has DepthNormals). Materials on `AG/Standard-Opaque-Instanced` (the
RecCenter couch, beanbag, cafe chair, basketball hoop) and `...-Emission`/`-TwoEmissionChannels`/`-Emission-Scroll`
need the runtime-facts fix (renderQueue 2501 when the shader has no DepthNormals pass).

---

## 3. Room plan: the Dreamport Rec Center

### 3.0 Overview (schematic, not to scale; north up)
```
                      PVP (x -16..-4)                SPORTS (x 0..16)
  z=+18   +--[Paintball][LaserTag][RecRoyale][Showdown][Dodge][Paddle][Soccer][DiscGolf][Bowling]--+
          |                                                                                      |
  PARTY   [3D Charades z14]    poker table (-5,10)      pool table (5,10)       [Blood Moon z14]  QUESTS
  (x=-20) [Midnight   z10]     + 6 chairs               cue rack panel (5,13)   [Lost Skulls z10] (x=+20)
          [Monsters   z 6]   ping-pong (-10.5, 4.5)                  LOUNGE     [Jumbotron  z 6]
          [Punch Up   z 2]                       KIOSK (0,1)       couches,rug  [Cauldron   z 2]
  RACING  [StuntRunner z-2]  ping-pong (-10.5,-1)  + bell, radio   jukebox(15.3,1) [GoldenTrophy z-2]
          [Rec Rally  z-6]                        hanging sign                   vending (19.45,-7.2)
          |  BASKETBALL court x-20..-10, z-18..-8                   cafe tables  Friend-O-Tron (19.45,-5.6)
          |  hoop on west wall z-13                                  counter x15..15.7 | CAFE (soffit 3.6 m)
  z=-18   +---windows x-18..-6----[ archway x -4..4 ]----windows x6..11.5---+------+
                                  | LOBBY x-6..6, z-26..-18 |
                                  | board(W)      lockers(E)|
                                  | lockers(W)  spawn(0,-24.2)
  z=-26                           +--------[ DORM door ]----+
```

### 3.1 Frame, rules
* Coordinates are metres **relative to the Rec Center instance origin O** (networking doc 5.3: instance k origin).
  +X = east, +Z = north, +Y = up. Floor top at y = 0 (O.y must be above the world's `RespawnHeightY`).
* The player enters from the Dorm door at the south (lobby) and looks north into the hall.
* Doors are flush on solid walls (nothing behind them). Door root = floor centre of the doorway, placed **0.06 m in
  front of the wall's inner face**, rotated so the door's local +Z points into the room:
  north wall yaw 180, east wall yaw 270, west wall yaw 90, south (lobby) wall yaw 0.
* Prefab roots may carry their own rotation (ping-pong table root yaw 90, trash bin yaw 90, jukebox baked yaw ~-92,
  Friend-O-Tron and Charades stage baked positions). Rule: `instance.localPosition = our position`;
  `instance.localRotation = Quaternion.Euler(0, yaw, 0) * prefabRootRotation` for props whose root rotation is part of
  their design (ping-pong, trash bin), and `Quaternion.Euler(0, yaw, 0)` for props with a baked scene placement
  (jukebox, Friend-O-Tron, Charades stage); keep the prefab's own scale (jukebox 1.121). Then ground-snap (2.3).
* "Faces" below = the direction a user of the prop looks at it from, i.e. the prop's front points that way. Front
  directions marked GUESS were derived from mesh offsets; verify each once in the editor and flip 180 deg if wrong.
* Static shell and furniture: layer 23 (Environment) or 0; throwables per conventions section 12; click handles 16.

### 3.2 Shell (procedural; wall thickness 0.3 m outside the listed inner faces)
| part | extent (inner faces) | height | materials |
| --- | --- | --- | --- |
| Main hall floor | x -20..20, z -18..18 (40 x 36 m) | top y 0 (slab 0.3) | Wood_Floor_Matte_mat (warm light wood), planks along X |
| Main hall walls | x = +-20, z = +-18 | 0..8.0 | 0-1.2 m wainscot Paint_Blue_Mat; 1.2-1.35 m stripe Paint_White instance tinted orange (0.953,0.477,0.140); 1.35-8.0 m Paint_White_Mat |
| Main hall ceiling | flat | y 8.0 | Paint_White_Mat; 30 fluorescent panels (box 2.4 x 0.06 x 0.4, Light_Fluorescent_mat) at x -15,-9,-3,3,9,15 and z -12,-6,0,6,12, y 7.97; roof beams: `(IBeam_10M_Sandbox)Variant` key `369` (10 x 0.65 x 0.42, Ibeams_RustFree_mat; swap to Ibeams_Orange_mat), 4 per line along X at z -15,-9,-3,3,9,15, top at y 8.0 |
| Archway hall <-> lobby | opening in the south wall, x -4..4, y 0..4.0 | | frame trim 0.2 m Paint orange |
| South windows | two openings in the south wall: x -18..-6 and x 6..11.5, y 1.2..6.6 | | Glass_Clear_mat pane at the wall centre plane, mullions 0.12 m Paint_White every 2 m, sill 0.25 m deep |
| Clerestory windows | north, east, west walls, y 5.6..7.4, x/z from -17.5 to 17.5 | | Glass_Clear_mat, mullions every 2.5 m |
| Lobby | x -6..6, z -26..-18 (12 x 8 m) | ceiling y 4.0 | floor Wood_Floor_Faded_Matte_Mat; walls as the hall (wainscot + stripe); ceiling Paint_White + 4 fluorescent panels at (+-3, 3.97, -24) and (+-3, 3.97, -20) |
| Cafe soffit | box under the ceiling over x 12..20, z -18..-9, underside y 3.6, top y 4.0 | | Paint_White; fascia 0.4 m tinted orange on the west (x 12) and north (z -9) edges |
| Cafe floor | x 4..20, z -18..-8, y 0.003 overlay | | Tile_Linoleum_Brown_Mat |
| Lounge rug | 8.0 x 6.0 m centred (11, 0.005, 5) | | Carpet_LowPile_Striped_Blue_Mat |
| Basketball court paint | area x -20..-10, z -18..-8 (y 0.004 overlays) | | key (lane) x -19.9..-14.1, z -14.8..-11.2 in Paint_White tinted orange; 0.05 m white lines: boundary of the area, free-throw line x -14.1, semicircle r 1.8 at (-14.1, -13), 3-point arc r 6.0 centred (-19.58, -13) clipped to the area |
| Exterior (optional) | ground plane 100 x 100 m at y -0.05 around the building, green (Paint_White tinted (0.30,0.50,0.25)); `(Sandbox_FlatTreeLarge)` `63e` (2.75 x 2.98 m), `(Sandbox_FlatTreeMedium)` `4ae`, `(Sandbox_FlatTreeSmall)` `e9d` at 12-25 m outside the south windows | | seen only through windows |

Colliders: floor/lobby/walls/ceiling boxes (walls include the door walls, so doors never open a hole), counter and
kiosk boxes, window panes (so balls bounce). No ceiling holes.

### 3.3 Activity doors (21)
Build each from the double `[ActivityDoor]` (clone of the hull's child, 2.1.2) with its sign text set. Frame colour
by group: instance of `ActivityDoor_Recolorable_Mat` assigned to the frame renderer only (`ActivityDoor_Frame`);
the leaves keep `ActivityDoor_Mat` (wood). The recolorable frame texel is grey 0.76, so `_BaseColor = target / 0.76`
(GUESS, tune): PvP + Sports = keep the stock blue frame (ActivityDoor_Mat, frame ~(0.19,0.40,0.53)); Quests = rust
orange target (0.54,0.30,0.19) -> BaseColor (0.71,0.39,0.25); Party = purple target (0.42,0.26,0.58) -> (0.55,0.34,0.76);
Racing = green target (0.18,0.48,0.28) -> (0.24,0.63,0.37); Dorm = warm grey target (0.45,0.41,0.37) -> (0.59,0.54,0.49).
Sign TMP: keep the prefab settings (auto-size 50..100, centred, wrapping on, black); long names wrap to 2 lines (sign face 1.07 x 0.27 m).

| # | activity (id from networking 5.1) | group | wall | door root position (x, y, z) | yaw | sign text |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Paintball (13) | PvP | north | (-16, 0, 17.94) | 180 | PAINTBALL |
| 2 | Laser Tag (9) | PvP | north | (-12, 0, 17.94) | 180 | LASER TAG |
| 3 | Rec Royale (17) | PvP | north | (-8, 0, 17.94) | 180 | REC ROYALE |
| 4 | Rec Room Showdown (18) | PvP | north | (-4, 0, 17.94) | 180 | SHOWDOWN |
| 5 | Dodgeball (7) | Sports | north | (0, 0, 17.94) | 180 | DODGEBALL |
| 6 | Paddleball (12) | Sports | north | (4, 0, 17.94) | 180 | PADDLEBALL |
| 7 | Soccer (19) | Sports | north | (8, 0, 17.94) | 180 | SOCCER |
| 8 | Disc Golf (6) | Sports | north | (12, 0, 17.94) | 180 | DISC GOLF |
| 9 | Bowling (4) | Sports | north | (16, 0, 17.94) | 180 | BOWLING |
| 10 | Quest for the Golden Trophy (15) | Quests | east | (19.94, 0, -2) | 270 | GOLDEN TROPHY |
| 11 | The Curse of the Crimson Cauldron (21) | Quests | east | (19.94, 0, 2) | 270 | CRIMSON CAULDRON |
| 12 | The Rise of Jumbotron (22) | Quests | east | (19.94, 0, 6) | 270 | RISE OF JUMBOTRON |
| 13 | Isle of Lost Skulls (8) | Quests | east | (19.94, 0, 10) | 270 | ISLE OF LOST SKULLS |
| 14 | Crescendo of the Blood Moon (5) | Quests | east | (19.94, 0, 14) | 270 | CRESCENDO OF THE BLOOD MOON |
| 15 | 3D Charades (3) | Party | west | (-19.94, 0, 14) | 90 | 3D CHARADES |
| 16 | Make It To Midnight (10) | Party | west | (-19.94, 0, 10) | 90 | MAKE IT TO MIDNIGHT |
| 17 | My Little Monsters (11) | Party | west | (-19.94, 0, 6) | 90 | MY LITTLE MONSTERS |
| 18 | Punch Up (14) | Party | west | (-19.94, 0, 2) | 90 | PUNCH UP |
| 19 | Stunt Runner (20) | Racing | west | (-19.94, 0, -2) | 90 | STUNT RUNNER |
| 20 | Rec Rally (16) | Racing | west | (-19.94, 0, -6) | 90 | REC RALLY |
| 21 | Dorm (1, private) | Home | lobby south | (0, 0, -25.94) | 0 | DORM |

Quest order = release order as remembered (Golden Trophy, Crimson Cauldron, Jumbotron, Isle of Lost Skulls,
Blood Moon; years not verified - GUESS). Grouping follows Rec Room's "SPORTS & PVP" door plus a Quests side (wiki: Quests on the right =
east, community board on the left = west... here the board is in the lobby, west wall).
Keep-clear bands (no furniture): west x -20..-16 for z -8..16; north z 14..18; east x 16..20 for z -4..16.
Optional 22nd door: `(Sandbox_MagicDoor)`-style single door "DELIGHT ME!" on the lobby east wall at (5.94, 0, -20.2)
yaw 270 -> random activity (Rec Room's Magic Door went to a random room; its TargetName default was "RecCenter").

Group header signs (TMP, Futura Bold Condensed, white on a panel of Paint_White tinted orange, panel 0.8 m tall at
y 4.2..5.0, 0.03 m off the wall): "PVP" x -17..-3 and "SPORTS" x -1..17 on the north wall; "QUESTS" z -3..15 on the
east wall; "PARTY" z 1..15 and "RACING" z -7..-1 on the west wall. Over the archway on the hall side:
"WELCOME TO THE REC CENTER" (y 4.3..4.9); on the lobby side: "REC CENTER".

### 3.4 Furniture and props (positions are floor contact points after ground-snap unless y is given)
**A. Central info kiosk / merch booth** (the "shop in the centre")
* Procedural ring counter centred (0, 0, 1.0): outer radius 2.4, inner 1.7, height 1.05, with a 1.2 m opening facing
  north (+Z). Body Paint_White, 0.1 m orange top band, top surface Wood_Floor_Faded_Matte_Mat.
* `(Sandbox_Bell)` `45b` on the counter at (0, 1.05, -1.05) (south point, facing arrivals).
* `(Radio)` `f3` at (1.45, 1.05, -0.45), faces SW (yaw 225), playlist per 2.6.
* `(Sandbox_Houseplant_SnakePlant)` `ce6` inside the ring at (0, 0, 1.0).
* Hanging sign: 4 panels 4.0 x 1.0 m forming a square of side 4.0 around (0, 6.0, 1.0), each with TMP "REC CENTER"
  (Rec Room orange text on off-white), 2 thin cables per panel to the ceiling. Optional: `Kiosk_PowerUpAmbianceLoop`
  (5a81b24c:9) as a quiet 3D hum at the kiosk (maxDistance 6 m) and `Kiosk_ButtonPressedNext` on interactions.

**B. Basketball (south-west)**
* `(Basketballhoop)` `78`: root at (-19.95, 3.085, -13.0), yaw 90 (backboard on the west wall, hoop towards +X). In the
  prefab the backboard centre is the pivot, the rim is 0.335 m below and 0.371 m in front of it -> rim centre at
  (-19.58, 2.75, -13.0) (GUESS rim height, real 3.05 m).
* `(Basketball)` `d2` x4 on the floor at (-11.5, -, -16.8), (-11.0, -, -16.8), (-11.5, -, -17.3), (-11.0, -, -17.3).
* `(DodgeballBall)` `1bf` x2 at (-9.5, -, -16.0), (-9.0, -, -16.4); `(Bucket)` `55` at (-10.2, -, -17.3).

**C. Ping-pong (west)**
* `(Sandbox_Table_PingPong)` `7cd` x2 at (-10.5, 0, -1.0) and (-10.5, 0, 4.5), yaw 0 (with the prefab's own root
  rotation the table's long axis is world X; table top 0.76, net top 0.915).
* `(Pingpong_Paddle)` `64` x2 per table on the top at (-11.6, 0.78, z) and (-9.4, 0.78, z); `(PingpongBall)` `568`
  x1 per table at (-10.5, 0.80, z + 0.3).

**D. Card/poker table (north-centre-west)**
* `(Sandbox_GameTable)` `51` at (-5.0, 0, 10.0).
* `(Sandbox_PokerChair)` `ebf` x6 at radius 1.75 m around (-5, 10) at angles 0, 60, ..., 300 deg (angle 0 = +Z), each
  facing the table centre (GUESS front; verify).
* `(SandboxDiceSet)` `6d` on the table at (-5.0, 0.90, 10.0).

**E. Pool (north-centre-east)**
* `(PoolTable)` `ee` at (5.0, 0, 10.0), yaw 0 (long axis X, 2.98 x 1.53).
* Freestanding panel 1.6 x 2.2 x 0.12 m (Paint_Blue) at (5.0, 0, 13.0); `(PoolCueRack)` `d6` on its south face at
  (5.0, 0.35, 12.92) yaw 180; `(PoolCue)` `4f` x2 in the rack; `(PoolTriangle)` `ad` on the table.

**F. Lounge (east)**
* Rug (3.2). `(Table_Coffee_Curvy_Sandbox)Variant` `536` at (11.0, 0, 5.0), long axis X (yaw 0).
* `(RecCenter_Couch_Sandbox)Variant` `ed9` x2: (11.0, 0, 7.3) facing -Z (yaw 270) and (11.0, 0, 2.7) facing +Z
  (yaw 90). Derivation (GUESS): the couch's length is its local Z and its seats face local -X (seat highlight centre
  x -0.007, backrest collision at x +0.68).
* `(Sandbox_CouchChair)` `f0` x2: (7.6, 0, 5.0) facing +X (yaw 90) and (14.4, 0, 5.0) facing -X (yaw 270) (GUESS:
  CouchChair/Couch face local +Z, from the seat highlight at z +0.084).
* `(Chair_Beanbag_Sandbox)Variant` `ec1` x3 at (8.0, 0, 1.2), (9.6, 0, 0.6) and (13.2, 0, 9.8), random yaw.
* `(Sandbox_Houseplant_Dracaena)` `ddd` at (7.2, 0, 8.6); `(Sandbox_Houseplant_Fig)` `cb6` at (15.0, 0, 8.8).
* `(BowlingAlley_Jukebox_Sandbox)Variant` `48` at (15.3, 0, 1.0), rotation Euler(0,0,0) (front = local -X per the
  glass at x -0.19, so it faces west into the lounge), scale 1.121.

**G. Cafe (south-east, under the soffit)**
* Counter (procedural): x 15.0..15.7, z -17.4..-10.0, height 1.05, body Wood_Horizontal_RedBrown_mat, top Paint_White.
  Staff side (x 15.7..20) is walkable.
* `(Sandbox_Shelves_Short)` `fe5` x2 against the east wall at (19.75, 0, -16.0) and (19.75, 0, -13.0) facing -X;
  `(Sandbox_Minifridge)` `c6a` x2 at (19.55, 0, -11.2) and (19.55, 0, -10.2) facing -X.
* On the counter (y 1.05, x 15.35): `(RecCenter_Cafe_CoffeePot)` `99` at z -16.5 and -12.0; `(RecCenter_Cafe_Mug)`
  `e5b` x8 at z -15.8, -15.55, -15.3, -15.05 and -11.6, -11.35, -11.1, -10.85; `(RecCenter_Cafe_Plate)` `20b` x6
  stacked at z -13.8 (y += 0.03 each); `(Sandbox_MocktailMixer)` `98` at z -14.6; `(Radio)` `f3` at z -17.1 playing
  Food Court / Lone Shoe Blues.
* `(Stool_Wood_Round_Sandbox)Variant` `58` x5 at x 14.5, z -16.8, -15.4, -14.0, -12.6, -11.2.
* `(Light_Pendant_Sandbox)Variant` `3aa` x3 hanging from the soffit: pivot (top) at (15.35, 3.6, -16.0), (15.35, 3.6,
  -13.5), (15.35, 3.6, -11.0) (bottom at 2.18 m) + a Unity point light under each at y 2.3 (range 5, intensity ~1.5,
  colour (1.0, 0.75, 0.5), no shadows).
* `(Table_Cafe_Short_Sandbox)Variant` `c48` x3 at (9.5, 0, -15.0), (9.5, 0, -11.5), (6.0, 0, -15.0), each with
  `(Chair_Cafe_Round_Sandbox)Variant` `43` x2 at +-0.95 m in X facing the table.
* `(Table_Cafe_Tall_Sandbox)Variant` `735` at (12.5, 0, -7.0).
* Poster (procedural quad 0.6 x 0.9 with Poster_StayHydrated_Mat) on the south wall at (17.5, 2.2, -17.97) facing +Z.

**H. Vending (east wall, south of the Quest doors)**
* `(VendingMachine)_SpawnableTool` `4ea` at (19.45, 0, -7.2) facing -X (yaw 270; GUESS front = local +Z from mesh
  centre z +0.076).
* `(Sandbox_Friendotron)` `bbd` at (19.45, 0, -5.6) facing -X (yaw 270), rotation reset (baked placement).

**I. Lobby (x -6..6, z -26..-18)**
* Dorm door: row 21 of 3.3. `(SandboxWelcomeMat)` `d4` at (0, 0, -24.9).
* **Arrival spawn from the Dorm / world arrival: (0, 0, -24.2), yaw 0**; extra spawn spots (-1.5, 0, -24.2),
  (1.5, 0, -24.2), (-1.5, 0, -23.0), (1.5, 0, -23.0).
* Community board: `(BulletinBoard_Sandbox)Variant` `7ca` on the west wall, centre (-5.9, 1.75, -21.5), facing +X
  (board mesh spans Z, faces +-X), with the `(CommunityBoard)_RecCenter` (6d3223da `2d`) panels laid on it, filled with
  our own texts (activity list, controls help, credits).
* `(Sandbox_Lockers)` `d5c`: west wall at (-5.64, 0, -24.4) facing +X; east wall at (5.64, 0, -24.4) and (5.64, 0,
  -22.2) facing -X (GUESS front = local +Z) - the nod to the original "Locker Room".
* Posters: `(Sandbox_CodeOfConduct_Poster)` `760` on the lobby south wall at (-3.0, 1.6, -25.97) facing +Z;
  `(Sandbox_ShakeHands_Poster)` `b8f` at (-5.2, 1.6, -17.85) and `(Sandbox_MuteGesture_Poster)` `ef` at (5.2, 1.6,
  -17.85), both on the hall side of the south wall facing +Z.
* `(Sandbox_Houseplant_Fig)` `cb6` at (-4.6, 0, -17.2) and (4.6, 0, -17.2) (hall side of the archway).

**J. Extra plants between door groups:** `(Sandbox_Houseplant_SnakePlant)` `ce6` at (-19.6, 0, 0.0) (between Punch
Up and Stunt Runner), (-2.0, 0, 17.6) (between PvP and Sports), (19.6, 0, -4.2) (south end of the Quests).

### 3.5 Lighting and audio
* 9 Unity point lights at y 6.0 over x in {-12, 0, 12} x z in {-10, 0, 10}: range 14, intensity ~1.0 (URP), colour
  (1.0, 0.97, 0.92), shadows off; 2 in the lobby at (0, 3.5, -24) and (0, 3.5, -20), range 8. Emissive fluorescent
  panels do the look. (Somnium's sun is blocked by the roof; nothing global is changed.) GUESS values, tune in client.
* Each door: when hovered/open, a small point light at door-local (0, 1.2, 0.6), range 3, intensity 0.6, colour
  (1, 0.97, 0.82) (only doors that are open -> few lights).
* Audio (2.6): crowd bed `sfx_onboarding_outdoor_ambiance_local_people02` (f981a603:a) looping 3D at the archway
  (maxDistance 25 m, ~-20 dB); `AmbianceSummerOutdoor` (170d5064:1) looping at each south window group (maxDistance 8,
  very low); radios as placed; jukebox 15-track list.

### 3.6 Networked items in the hub (budget against networking 5.5: 48 props total)
Hub props that need syncing: 4 basketballs, 2 dodgeballs, 2 ping-pong balls, 4 paddles (optional: paddles as
held-only), 2 pool cues (held-only) = 10-14 props. Pool balls: simulate on the instance authority and pack positions
into the Rec Center instance object (16 balls x (x,z) mm) or keep the pool table local-only in v1 (GUESS/decision).
Mugs, plates, pots, bottles: local-only (Rec Room reset them anyway), or pickable with no sync.

---

## 4. Transitions for Dreamport (implementation spec)

### 4.1 Door behaviour (our `RrDoor` component on each door clone; the Rec Room Animator is disabled)
States: **Closed** -> **Open (hover)** -> **Going** ; or **Locked**.
* Hover: the local player's body position (Bridge local player root) is within 3.0 m in front of the door
  (door-local z 0..3.0, |x| < 1.6) OR the desktop/VR pointer hovers the door's click collider. Open anim 0.25 s
  (smoothstep): `ActivityDoor_Left` local euler Y 0 -> +21.44, `ActivityDoor_Right` Y 0 -> -22.27, glow material
  instance `_TintColor.a` 0 -> 0.834; the hover point light fades in. Reverse on exit. (Values from the Rec Room clips.)
* Use (any of):
  * Desktop: left click on the door's click collider (layer 16, `excludeLayers = ~0`, box 2.05 x 2.14 x 0.3 at the
    leaves, `XRSimpleInteractable` + `IPointerClickHandler`, conventions 11.3);
  * VR: trigger while the far ray hovers the same collider, or a hand (XR `InputDevices` position) touching the leaf
    box (door-local |x| < 1.0, 0.2 < y < 2.1, -0.05 < z < 0.35);
  * Both: walking into the doorway: body position in door-local |x| < 0.9, z < 0.35 (polled every frame; a trigger
    collider would not see the avatar, whose CharacterController is on layer 2 and layer 15 does not collide with 2).
* Locked (activity not implemented / instance full and no new instance allowed): no hover-open; use shows a 1 s
  screen message (Rec Room had `lockedScreenSpaceMessage` / `lockedScreenSpaceMessageDuration 1`), e.g.
  "Coming soon" or "This game is full".
* Debounce: ignore uses for 1.5 s after arriving anywhere (you spawn 1.8 m in front of a door).

### 4.2 The "Going to" sequence (local player)
| t (s) | action |
| --- | --- |
| 0 | Door enters Going (stays open). Freeze locomotion (`ISomniumPlayerMotion.SetMovementSpeed(0)` or equivalent; restore later). Write presence: activity id = target, status 1 Loading, bump epoch (networking 5.4). Optional whoosh: `GenericTeleport01` if resident. |
| 0 -> 0.15 | Fade to black (Rec Room `fadeInDuration 0.15`). Desktop: full-screen overlay canvas. VR: a black inside-out sphere r 0.5 m (URP Unlit, cull off, transparent, queue 4000) parented to the camera. |
| 0.15 | Show the loading card (below). Start building/loading the target instance (async bundle loads; progress = done/total). Call `DoTeleportToPoint(targetSpawn, (0, yaw, 0), done)`. |
| on `done` | Hide/tear down the old activity's geometry for this client (ETS pattern: only after the teleport callback). |
| >= max(minCard, build done) | Fade from black 0.15 s. Status 2 Lobby. Unfreeze. Target door (if arriving at a door) plays Open then Closed over 1.0 s. For activities: title card + sting (first ~3.5 s of `RecRoomTheme2` b203e635:9 with 0.5 s fade; the real `ActivityTitleJingle` is not loadable). |

`minCard`: Rec Room used `minTotalDisplayDuration 5` s. Recommendation: 2.5 s (GUESS; a local teleport needs no 5 s,
but a flash of the card feels broken), 1.0 s for Rec Center <-> Dorm.

**Loading card** (rebuild, do not instantiate Rec Room's `[LoadingScreen]`): background = activity image (our own
screenshot of the rebuilt room, or `illustration_HomeButton_RecCenter` (f124b399 `bf9`) / `illustration_HomeButton_DormRoom`
(`597`) for hub/dorm) over black; bottom bar with "GOING TO" (24 px, Rec Room orange (0.992,0.510,0.302)), the room
name as Rec Room wrote it with a caret, e.g. `^Paintball`, `^RecCenter`, `^DormRoom` (40 px, white), sub-room name
(map / mode, 32 px), "LOADING" + animated dots (0.3 s per dot), progress bar, one tip line rotating every 2-5 s, the
Rec Room logo. VR: world-space panel 1.6 x 0.9 m, 2.0 m in front of the head, yaw-locked, on top of the black sphere.

### 4.3 Where you arrive
| from -> to | arrival |
| --- | --- |
| world arrival / Dorm door / watch "Rec Center" -> Rec Center | lobby spawn (0, 0, -24.2) yaw 0 (+ the 4 extra spots) |
| activity X exit (its "Rec Center" door) -> Rec Center | in front of X's door: `pos = doorRoot + doorForward * 1.8`, yaw = door yaw (facing into the hall), lateral stagger +-0.6 m for simultaneous arrivals; the door plays Open->Closed |
| Rec Center Dorm door / watch "Dorm" -> own Dorm | the Dorm's spawn in front of its door (dorm research doc) |
| Rec Center door X -> activity X | activity X's lobby/spawn (per activity doc); every activity instance gets an exit double door with sign "Rec Center" (exactly the Charades `[ActivityDoor]`) |

### 4.4 Instances
* The Rec Center is one networked instance (activity id 2) that everyone shares; players only build it when they are
  in it (or build it once and keep it while in the dorm - choose by memory budget).
* Door use picks the target instance: join the instance of the same activity that is in Lobby state with free slots
  (Rec Room `JoinRoomSceneInviteMode 2`, `ForceNewInstance 0`), else create a new one; party members follow the
  leader (party doc).

### 4.5 Sound summary for transitions
Door hover: none in the data (GUESS: silent; optional soft `Kiosk_StateChange01` 47870ee7:b at -15 dB). Use:
`GenericTeleport01` (0.30 s) if resident, otherwise none. Arrival: `RecRoomTheme2` sting for activities; nothing for
the hub. Loading: silence (Rec Room's loading screen had no music - GUESS).

---

## 5. Open questions / GUESS list
1. Exact look of the real Rec Center (both versions) could not be verified: fandom blocks fetching and rec.net is
   offline; layout in section 3 is our design following the verified wiki facts (Quests + merch right, community
   board left, sports/PvP behind the central shop, big windows, cafe, basketball, ping-pong, lockers heritage).
2. Couch / lockers / vending / poker-chair / poster front directions (3.4) - derived from mesh offsets; verify once.
3. Ping-pong table exact width (1.85 m incl. posts?) - verify with renderer bounds.
4. Rim height 2.75 m (Rec Room's real hoop height unknown).
5. Door frame recolour values; the stock orange door texture `ActivityDoor_Orange_tex` is referenced by no
   material, so it is not loadable - recolour the grey recolorable frame instead.
6. `GenericTeleport01`, `Kiosk_SelfieCountdownBeep`: not containers; residency after loading the watch UI root is a
   GUESS. `ActivityTitleJingle` and `music_ui_enter_room_guitar_01` are not loadable at all.
7. Texture world sizes for the procedural shell (2.7).
8. Whether the Rec Center kept a physical door back to the Dorm (we add one).
9. Loading-card minimum time (2.5 s proposed vs Rec Room's 5 s).
10. Pool table networking (3.6).
11. `(Sandbox_PopUpShop_*_Storefront)` kiosks were not used (heavy, server-driven); a procedural booth replaces them.
