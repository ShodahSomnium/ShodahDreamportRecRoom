# Rec Room watch, menus and UI style (research for Dreamport: Rec Room)

Status: COMPLETE (2026-09-24). Research only; values marked GUESS are proposals, everything else was measured in the bundles or cited.

Abbreviations used everywhere below:
* SP = `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad`
* BUNDLES = `E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64` (file = `<bundle>.bundle`)
* RR = `SP\rrtool\bin\out\rrtool.exe` (see `SP\rrtool\README.md`); RRT = `SP\research\tools\rrt\out\rrt.exe` (same tool, but
  with env `RRT_RT=1` the `tree` command also prints RectTransform anchors/pos/size/pivot); `SP\research\tools\uic.pl`
  compacts a `RRT tree ... --fields` dump into a readable layout spec (used for all `dumps\*.ui.txt` files).
* A bundle written as 8 hex chars (e.g. `f124b399`) is the prefix of the full name; full names are listed in 2.0.
* "container key" = Addressables key usable with `bundle.LoadAsset<T>(key)` (see rr_runtime_facts.md: only container
  entries load by API; other assets become resident when a container asset referencing them is loaded, then
  `Resources.FindObjectsOfTypeAll<T>()` by name).

## 0. TL;DR for implementers

* **What to build**: our own watch menu with UnityEngine.UI + TMP (Rec Room's own UI prefabs load but render as
  grey blocks - their theme/rounded-corner scripts are missing). Copy the RRUI look measured here: a **1920-wide
  cream page (#F9F3EF, radius 20)** above an **orange gradient nav bar (#FF5F2F -> #FF7D31, 150 tall in VR / 130
  on desktop)** with white 100 px icons + Roboto 24 labels in 150-wide tabs; active tab = translucent maroon plate
  (#602121 at 50%, radius 20). Text #393939 on cream, white on orange; badges #10524B pills.
* **Assets** (all from the player's install at runtime): icons atlas `RRUI_Icon_Atlas(512)` (bundle
  f124b399caea956fb2ae85b8413c07d2 key `81`), illustrations atlas `RRUI_Illustration_Atlas(1024)` (same bundle, key
  `e2`), fonts Roboto (`379`, bold `95`) and Jost (`cb7`), 87 UI clips by key (appendix A), watch mesh `Wrist_Watch` +
  `Watch_Wrist_mat` (38066d851f08e64e9ed64b9094f31883 key `0f`).
* **Behaviour**: VR = look at the left wrist (face within 60 deg of view, pitch <= 45, roll <= 30, dwell 0.4 s;
  close beyond 60/50/50) or tap the face; the menu "projects" out with a CRT-style scale animation (0.42 s) and
  paper/stereo "hydrate" sounds; world dims to 80% black behind it. Desktop = key **H** (Tab/Esc belong to the
  Somnium client), panel 0.8 m in front of the camera.
* **Tabs**: Home (Play / Backpack / Settings tiles + "Go to Dorm Room" / "Go to Rec Center"), Play (carousels of
  21 activity cards), People, This Room, Settings (+ look-only Customize/Create/Store/Chat/Notifications).
* **Activity art**: no official cover images exist in the bundles. 17 of 21 get good in-bundle art (gift-box
  prints, quest villain overlays, logos); Bowling, Make It To Midnight, Punch Up and Showdown get stand-ins
  (table 3.3).

```
+--------------------------------------------------------------------------------------------+
|  cream page #F9F3EF (r20)                                                                   |
|  [ Play ] [Backpack] [Settings] |  ( Go to Dorm Room   > )                                 |
|  tiles 190x220, art tile r30    |  ( Go to Rec Center  > )   quick access 380 wide, r30     |
|  label Roboto 32 #393939        |                                                           |
|  ...                                                                                        |
|~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ 48 px shadow ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~|
| [Profile] |      [Home] [Play] [People] [This Room] ...        | [Settings] |  orange bar      |
|           |      icon 100 + label 24 white, 150 wide, sp 20    |            |  #FF5F2F->#FF7D31|
+--------------------------------------------------------------------------------------------+
```

## 1. How the watch works in Rec Room

Sources: Rec Room fandom wiki (Watch Menu, Screen Mode, Showdown, Isle of Lost Skulls, Crimson Cauldron pages via
search summaries; the wiki blocks direct fetches), recroom.com blog "Introducing the Widget Watch" (2025), DefKey
Rec Room PC shortcut list, plus the bundle data quoted below (the bundle data is authoritative where they differ).
Note (reported by a 2026 search summary, not verified here): Rec Room Inc. shut its servers down on 2026-06-01, so
nothing can be fetched from rec.net at runtime; room cover images therefore must come from the bundles (section 3.3).

### 1.1 VR

* The watch is a chunky wrist-watch on the **left wrist** by default (`WatchAnchor` under `(Player)/.../LeftHand`,
  local p=(-0.02, 0, -0.119) = 12 cm behind the hand centre, r=(0.5,-0.5,-0.5,0.5)); a mirrored anchor exists on the
  right hand (p=(0.02,0,-0.119), r=(-0.5,0.5,-0.5,0.5)) for left-handed players. `WatchFaceTransform` child at
  p=(0,0,0.041) is the face centre. `WatchMenuOrigin` (menu spawn point) at p=(-0.15,0,-0.004) left /
  (0.15,0,-0.004) right. (All from `(Player)` c513202af4f2b6439095734ab5caead0 #1873379750507966147, dump
  `SP\research\_player.txt` lines 268-374.)
* **Open by looking at it** (default): `WatchGazeTracker` (c513202a #1859998750955597507) opens the menu when the
  watch face is within **60 deg of the view centre (maxFovAngle), wrist pitch <= 45 deg, roll <= 30 deg** for a
  **dwell of 0.4 s**; it stays open until the face leaves **60 deg / pitch 50 deg / roll 50 deg** (hysteresis). A
  dwell visual (holographic ring, `DwellVisual` mesh with `WatchUI_Holographic_Dwell_Mat`, keyword
  `SWIPE_VALUE_FROM_SCRIPT`) fills over the face during the 0.4 s.
* **Tap to open** (setting "Tap Watch to Open Menu", in the bundle as `TapToOpenWatch [Switch_Compact]` on the
  Settings page): instead of gaze, touch the watch face with the other hand's finger (`TapToOpenRegion`: two
  trigger boxes 0.08x0.06x0.02 at z 0.035 and 0.08x0.06x0.05 at z 0.05 around the face; `TapToOpenVisual.maxSwipeValue 1.35`).
* The menu then **projects** out of the watch as a holographic screen (`(PlayerWatchMenuProjector)`, scanline
  hologram mesh `Screen_Projection` with material `WatchUI_Holographic_Background_Mat`, amber #FFCE5B at 31% alpha)
  and appears above / in front of the wrist, facing the eyes. A dim "fade plane" (black, **max opacity 0.8, fade
  0.1 s**) darkens the world behind it (`WatchMenuFadePlane`).
* **Detach**: pressing the controller menu button while the watch menu is open detaches it and leaves it floating
  in space (the prefab even holds the text "Press the [menu] button on your controller to [detach]":
  `DetachMenuNote`, legacy Futura size 45). The VR nav bar has a `Detach` icon button (icon_Expand / icon_Collapse).
* Other players see a small amber holographic rectangle (`RemoteWatchMenu` cube 1.35x0.9 scaled 0.75,
  `WatchUI_Holographic_Remote_Mat`, amber at 9%) in front of your wrist while you have the menu open.
* Watch face: an analogue clock (`ClockFace`, hour/minute/second hands = thin Unity cubes 3x13x3 mm, 2x18x2 mm,
  1x22x1 mm, additive brown material) showing **local real time**, plus a notification system (4 "notification
  lines" sprites + a projector beam + an icon above the watch when a notification arrives).

### 1.2 Screen mode (PC keyboard/mouse)

* **Tab opens/closes the watch menu** (DefKey list; Esc also backs out of menus in RR - GUESS). Other RR keys: T
  push-to-talk, Y room chat, C emotes (Expresso radial), 1/2/3 backpack slots, 4 backpack menu, V third person, E
  interact, Q drop, F/G wave hands.
* The menu is the **same 1920-wide watch UI**, presented as a full-screen panel in front of the camera (the
  `(NavigationBar)PCConsoleVersion` variant of the nav bar, 130 px tall instead of 150 px in VR); the mouse cursor is
  freed and clicks buttons directly.
* In OUR world, Tab is the Somnium client's Tablet key and Escape is Exit (somnium_conventions.md 11.1), so we
  cannot use them. Proposal: **H** opens/closes the watch (free in the client and unused by Rec Room PC), with
  **Backspace or right mouse** = back. In VR: gaze-to-open as above (no controller button is free: menu buttons,
  B and Y belong to the client; triggers are free, so "touch the watch face with the other index finger" is the
  tap-to-open alternative, no button needed).

### 1.3 Menu structure (current RR watch, 2021-2025, "RRUI")

The watch menu is one big page (`[WatchMenuRRUIRoot]`, 2032x1230 canvas units) with a **cream page area on top**
and an **orange navigation bar at the bottom**. Nav-bar buttons, left to right (PC and VR versions, bundle
4d3a480c5debdc4c839f125fc0c99d42):

| # | button (prefab node) | label | icon sprite (in `RRUI_Icon_Atlas(512)`) | what it opens |
| --- | --- | --- | --- | --- |
| 0 | `[IconButton] My Profile` | "Profile" | RecNet photo in a round mask (r 50) over the `icon_RRLogoBackdrop` tiler; "Jr." badge | profile page |
| - | `[IconButton] Detach` (VR only) | - | `icon_Expand` / `icon_Collapse` | detach / re-attach |
| 1 | `Home` | "Home" | `icon_Home` | Home: big buttons + quick access (Rec Center, Dorm) |
| 2 | `Play` | "Play" | `icon_Nav_Play` (100x100) | Play: room/activity carousels |
| 3 | `Customize` | "Customize" | `icon_customize_look` | avatar customisation |
| 4 | `Create` | "Create" | `icon_Nav_Create` | Maker Pen / create rooms |
| 5 | `Store` | "Store" | `MenuIcon_Sprites_2_ShoppingBag` | store |
| 6 | `People` | "People" | `icon_Players` | friends / players |
| 7 | `Current Room` | "This Room" | `icon_nav_ThisRoom` | this room: players, invite, room settings |
| 8 | `Chat` | "Chat" | `icon_Chat` | chat threads |
| 9 | `Noti` | "Notifications" | `icon_Notification` | notification inbox |
| 10 | `Settings` (bottom panel, right end) | "Settings" | `icon_Gear` | settings tabs: Audio, Comfort, Controls, Experience, Visuals, Notifications, Misc (+ Export, DevOnly) |
| - | `Exit` (inactive) | "Quit" | - | quit game |

Older layouts still in the bundles (useful for a "classic" look): the **Home page big-button row**
(`MainMenu-SizeExperiment-SmallerButtons`, 4d3a480c #4941968766206093556, key `24`): **Play, Create, Store,
Events, Clubs, Challenges, Backpack** (illustrations `Illustration_Menu_Play`, `_Create`, `_Store`, `_Event`,
`_Club`, `_Challenge`, `_Backpack`), a vertical orange separator, and **Quick Access**: **Dorm Room** ("Go to" +
"Dorm Room", `Illustration_Menu_Dorm`), **Rec Center** (`Illustration_Menu_RecCenter`), **Clubhouse**
(`Illustration_Menu_MyClub`) and "Evict All" (Dorm owner only). The 2016-2019 watch had Play / Friends / Backpack /
Settings style tabs with a list of activities by genre and a search bar (wiki).

The 2025 "Widget Watch" (`AppNavCanvas`, f124b399 #6772782859943981025, key `184`) is a slim vertical icon bar
(`icon_AppNav_*`: Play, Players, Chat, Backpack, Dorm, RecCenter, Settings, Escape, ...) with pop-over widget
panels; it was a split test (March 2025, 6 weeks) on mobile/PC/console and never reached VR (blog).
**Recommendation: recreate the RRUI nav-bar watch (table above) with the MainMenu home page.** It is what most
players remember and all its assets are present.

### 1.4 Sounds and animations (summary; details in 2.7 and 3.4)

* Open: the menu scales in like a CRT turning on (X 0.01 -> 1 in 0.056 s, then Y 0.046 -> 1.2 overshoot, settle
  to 1 at 0.42 s) with `sfx_rrui_watch_ui_open_paper_0x` (vol 0.15) + `sfx_rrui_watch_ui_open_stereo_0x` (0.025)
  ("hydrate" pools); the `WatchUI` component also names `comped_sfx_rrui_watch_ui_open_stereo_01` (0.05).
* Close: reverse animation (0.417 s) + `sfx_rrui_watch_ui_close_paper_0x` (0.10) + `..._close_stereo_0x` (0.02).
* Page change: Y squash to 0.5 and back (0.41 s) = `PlayerMenu_Transition`; refresh: Y squash to 0.745.
* Buttons: hover tick, press "select_down", click "select_click"; nav tabs add their own flavour stingers; primary
  (orange CTA) buttons have their own click; buttons pop to 1.03x for 0.05 s on click.

## 2. Bundle content (measured)

### 2.0 Bundles used (full names, size)

| prefix | full bundle name | size | holds |
| --- | --- | --- | --- |
| f124b399 | f124b399caea956fb2ae85b8413c07d2 | 13.3 MB | **RRUI core**: icon + illustration atlases, fonts, most UI sprites, `WatchUI_fbx`, many UI clips, `AppNavCanvas` |
| c513202a | c513202af4f2b6439095734ab5caead0 | 2.0 MB | `(Player)` (key `8`), `[Wrist_Watch] Local`, `[PlayerWatchMenu]`, `(PlayerWatchMenuProjector)`, watch open/close clips |
| 4d3a480c | 4d3a480c5debdc4c839f125fc0c99d42 | 2.3 MB | `(NavigationBar)*` variants, `MainMenu-SizeExperiment-*`, nav flavour audio pools |
| 16079ee0 | 16079ee01a0c29220760d8ebfcde3a67 | 5.6 MB | `(RRUIRootScreen)` (the watch page frame), People/ThisRoom/Report... screens |
| 287abc08 | 287abc08039fc2e1e7ff8d45c5d43651 | 1.7 MB | `Palette_LightTheme`, `AudioPalette_Default`, `ButtonAnimationPalette_Default`, page transition clips |
| 0df4dcda | 0df4dcda61c06138358ef77428370838 | 1.2 MB | UI clips: select_down, hover_general, sidebar_* (people/chat/...), favourite/cheer |
| 38066d85 | 38066d851f08e64e9ed64b9094f31883 | 2.0 MB | `Watch_Wrist_mat` (key `0f`) + its 4 textures, bean-body watch meshes |
| 1df8e0ef | 1df8e0efac4f496ebc58d7e6f86480c3 | 86.9 MB | holographic watch materials, full-body avatar `Watch_Meshes`, `legacy_FuturaExtraBold` |
| 4441eb17 | 4441eb17981f8c7779b14252ac96a0b8 | 185 KB | `(Navigation_Home)` (key `d`), `(Explore)` (key `a`), `(HomeTopHeader)` |
| 3c088378 | 3c0883788b02c781ad5ab32ac400e0c5 | 377 KB | `(Card_Room)` (key `3`), `(Carousel_Rooms_WithTitle)` (key `5`) |
| 6ef67129 | 6ef67129ea8ce0b105a2363c017f3653 | 388 KB | `(PlayDiscoverySections)` (key `5`) |
| 7380adf6 | 7380adf6479d4e5058930a8bd97ce843 | 61 KB | `WatchMenuRoundedCard` (key `39`), `WatchMenuGradientShadow` (key `8`), `RoundedBar` |
| 063b6b66 | 063b6b6653aa642616fd21d3d4701899 | 5.1 MB | watch cloth rustle clips, tutorial VO, room-enter jingle |
| 85515de1 | 85515de19d04f3c4dfe704e6f02046e3 | 146.6 MB | consumables: `Join`/`Leave` clips, **quest villain photo overlays** (section 3.3) |
| 7eb9204a | 7eb9204a118e984b1b77ab153658959d | 149 KB | MonoScripts (every RRUI prefab depends on it) |
| 96a9ca78 | 96a9ca784433befa5280ecb885211558 | 1.3 MB | settings tab pages, watch notification panel |
| edf7f2d6 | edf7f2d64a5698a6cec7ec8a1f821ab8 | 773 KB | full-body avatar skin incl. skinned watch meshes |
| 4056f2fc | 4056f2fc0e2635c36096ef5d5294708e | 7.9 MB | Maker Pen tool menus (use `WatchUI_Rig` too) |

Every bundle's dependency list is in `SP\rrtool\idx\deps.tsv` (c513202a: 35 deps, f124b399: 47, 4d3a480c: 14,
287abc08: 23). Opening a bundle costs ~0.4 ms (header only), so load the closure and only `LoadAsset` what is needed.

### 2.1 The physical watch

**Prefab `[Wrist_Watch] Local`** - c513202a #6221538477733751405 (a child of `(Player)`, not a container entry:
load `(Player)` via key `8` of c513202a with its dependencies to make it resident, then find it with
`Resources.FindObjectsOfTypeAll<GameObject>()` by name; or just use the mesh + material below on our own object).
Remote-player variant `[Wrist_Watch]` #-4812801197627699770. Dump: `SP\research\_watch_local.txt`.

| node | local transform (p / r quaternion / s) | components (useful data) |
| --- | --- | --- |
| `[Wrist_Watch] Local` (root) | 0 / identity / 1 | LODGroup: LOD0 screenRelativeHeight 0.270, LOD1 0.027 (culled below); RR scripts PlayerPartyColorWatchRenderer, TapToOpenVisual, WatchAttractor (missing at runtime) |
| `WatchRenderer` (layer 12, inactive in prefab) | 0 / identity / 1 | MeshFilter `Wrist_Watch` (c513202a #-7554622292230741548, 1163 verts, AABB centre (0,-0.0006,0.0036), **extent (0.0540, 0.0225, 0.0362) -> 108 x 45 x 72 mm**) + `Watch_Wrist_mat` |
| `WatchRenderer_LOD1` | same | `Wrist_Watch_LOD1` (#-6577129380500348460, 544 verts, extent (0.0523,0.0224,0.0355)) |
| `ClockFace` (L10) | p (0,-0.001,0.04) r (0,0,0.707,-0.707) | ClockFace script: axis Z, counter-clockwise flag 0; children `HourHand`/`MinuteHand`/`SecondHand` pivots at 0 with a `Mesh` child = Unity built-in Cube: hour s (0.003,0.013,0.003) p y 0.004; minute s (0.002,0.018,0.002) p y 0.006; second s (0.001,0.022,0.001) p y 0.008; material `Watch_Wrist_Hand_mat` (c513202a #8670758640555084390, `AG/Unlit-Additive`, colour (0.33,0.20,0.03, a 0.744)) |
| `DwellVisual` (L16) | p (0,0,0.01) r (0,0,0.707,-0.707) s (0.685,0.685,2.251) | mesh `Wrist_Watch_Temp` (f124b399 #5168136277838067192, 75 verts, 58x58 mm disc) + `WatchUI_Holographic_Dwell_Mat` (1df8e0ef #-2959662186003812652) |
| `TapToOpenRegion` (L9) | 0 | BoxColliders 0.08x0.06x0.02 c (0,0,0.035) and 0.08x0.06x0.05 c (0,0,0.05) |
| `HandOverlapRegion` (L9) | p (0,0,0.074) | BoxCollider 0.15 cube (hand-near-watch detection) |
| `Wrist Watch UI Anchor` (L12) | p (0.002,0,-0.063) r (0,0,0.707,0.707) | anchor for the room-chat menu and the VR equipment slots menu |
| `NotificationEffectAnchor` / `NotificationProjectorBeam` (L16) | beam p (0,0,0.03) s 0.5; icon p (0.3,0.3,1) s 0.27 | 4 SpriteRenderers `Notification_Tex` (material `Notification_Lines`), LineRenderer beam (`NotificationProjectorBeam` mat), icon `NotificationWatchIcon (HR)` (c513202a #-910836830891869642) |

**Material `Watch_Wrist_mat`** (38066d85 #234898668811482076, **container key `0f`**): shader `AG/Avatar`
(f124b399 #856394433213252575, an RGB-mask recolour shader). Textures (all 1024x1024 DXT5, 38066d85):
`Watch_Wrist_Tex` #8736049625805455317 (R/G/B recolour mask, see `SP\research\img\ui\Watch_Wrist_Tex.png`),
`Watch_Wrist_Norm` #3488357333161610541 (BumpScale 0.28), `Watch_Wrist_Emit` #962660099740890437,
`Watch_Wrist_Spec` #2010745255012260648. Colours: `_Red_Col` #905826 (strap brown), `_Green_Col` #141414,
`_Blue_Col` #8C919E, `_Base_Col` #7C4A1A, `_Decal_Col` #FF7900, `_Emission_Col`/`_EmissionColor` **#FDDC6E**
(warm glow), smoothness 0.642, metallic 0. Keep the original material (rr_runtime_facts.md); apply the
DepthNormals check (renderQueue 2501 if the shader lacks a DepthNormals pass).

Avatar-embedded (skinned) watches also exist - `Watch_Meshes` under `(PlayerAvatarFullBody)` and
`(PlayerAvatarModernBeanBody)` (1df8e0ef), `MB_Avatar_Skin_fbx` (38066d85), `(FB_Avatar_Skin_asset)` (edf7f2d6):
`Wrist_Watch_L_LOD0` (edf7f2d6 #-6114266446682450087, 1151 verts, extent (0.0275,0.0358,0.0275)),
`MB_Wrist_Watch_L_LOD0` (38066d85 #-3769622486244973037, 970 verts). Their anchors (1df8e0ef):
`WatchAnchor.L` #1684635120062418939 = child of `Jnt.ForearmRoll.Tweak.L` (full-body skeleton, chain
Spine.Root -> Mid -> Chest -> ShoulderWidth.Tweak.L -> Shoulder.L -> UpperArm.L (0.125) -> LowerArm.L (0.267) ->
ForearmRoll.Tweak.L (0.118)) at local **p (0, 0.075, 0), r (0,-0.707,0,0.707)** i.e. 7.5 cm further down the forearm
roll bone = on the wrist, rotated -90 deg about Y; `WatchAnchor.R` #-3053871143575236613 same p, r (0.707,0,0.707,0).
Bean body (`(PlayerAvatarModernBeanBody)`): `WatchAnchor.L` #8193672853635580498 under `Jnt.Hand.L` at p (0,-0.012,0.006)
same rotation; `.R` #5700375463597556306. **Useful for us**: on the Somnium humanoid, put the watch on the
LowerArm (forearm) bone at **~0.91 of the elbow->wrist distance** (RR full body: Jnt.Hand.L sits 0.211 m down
Jnt.LowerArm.L; the watch at 0.118 + 0.075 = 0.193 m, i.e. 1.8 cm before the wrist joint), face on the back of the
wrist - GUESS to tune. They are skinned to RR avatar bones, so
**do not use them**; the Somnium avatar is never modified. Use the static `Wrist_Watch` mesh on our own GameObject
that follows the Somnium left hand every frame (not parented under the avatar), see 3.5.

**`WatchUI_fbx`** (f124b399 #-5367901286527339016, **key `37`**): the holographic projection rig used by the watch
and by the Maker Pen tool menus: children `Screen_Projection` (skinned, f124b399 #4278722272748434936, 168 verts,
extent (0.446, 0.427, 0.335) m = the frustum-shaped light beam from the watch to the screen), `Screen_Flat`,
`ButtonLong`/`ButtonRectangle`/`ButtonSquare` (3D button meshes), `WatchUI_Rig` (bones `Root_Jnt` y 0.591 and
`Screen_Jnt` y -0.264: moving Screen_Jnt stretches the beam from the watch to the menu), `Wrist_Watch_Temp`.
Animator controller `PlayerMenuAnimator` (f124b399 #7602770211103541488) clips: `PlayerMenu_Hidden`, `_Visible`,
`_Show` (0.422 s), `_Hide` (0.417 s), `_Transition` (0.411 s), `_Refresh` (0.411 s) - decoded in 3.4.
Hologram materials (shader `Particles/HolographicTextureSpace` f124b399 #6968513869190567035, textures
`SmallScanline_Tex` #-3620716282781208860 / `LargeScanline_Tex` #-5929709619297908686):
`WatchUI_Holographic_Background_Mat` (1df8e0ef #-9209712902741517644: base #FFCE5B a 0.31, large scanline
#806C3F), `..._Remote_Mat` (c513202a #-8624053013506090908: a 0.09), `..._Dwell_Mat` (1df8e0ef
#-2959662186003812652: #B49B62 a 0, large scanline `LargeScanline_Tex_Swipe`, #FFDB8B), `..._Hidden_Mat`,
`..._Pink_Mat` (1df8e0ef), `WatchUI_Holographic_Button_Mat` (f124b399 #300085160763598045),
`..._Button_Purple_Mat` (4056f2fc).

### 2.2 Watch menu prefabs (screens)

| prefab | bundle / pathId | container key | notes |
| --- | --- | --- | --- |
| `[PlayerWatchMenu]` | c513202a #6656225846787751123 | - (child of (Player)) | the watch screen host: `RecRoom.Core.WatchUI` (flow list of ~48 screens, sounds), Canvas 1280x960 at scale 0.0008 under a root of scale 0.5, `Screens` container s 0.85, `Background/Image`, `DetachMenuNote`, `BoxColliderObject`, `HandOverlapRegion` (s 1.2,1,0.85 at z -0.129). Dump `_playerwatchmenu.txt` |
| `(PlayerWatchMenuProjector)` | c513202a #2327931377130008259 | - | gaze tracker, fade plane, projection, keyboard roots (`KeyboardRoot` p (-0.09,-0.75,-0.106) r x -30 deg). Dump `dumps\watchprojector.txt` |
| `(RRUIRootScreen)` | 16079ee0 #1278159438837528033 | full path `Assets/RRUI/Prefabs/WatchMenu/_RRUI 1.0/Shims/(RRUIRootScreen).prefab` | the page frame: 2386.8x1344 world canvas; `[WatchMenuRRUIRoot]` 2032x1230 at s 1.176 holding `Container/Surface` (Router page 1920 x 1080..1230, nav bar group), `Overlay` (dialogs, toasts, callouts), `AppNav Button`, `[VRSideScrollbars]`. Dump `dumps\rruiroot.ui.txt` |
| `(NavigationBar)PCConsoleVersion` | 4d3a480c #2371138885750067752 | `50` | nav bar for screens. Dump `dumps\navbar_pc.ui.txt` |
| `(NavigationBar)ExpandedVRVersion` | 4d3a480c #9212692754824343480 | `b` | nav bar for VR (adds Detach). `dumps\navbar_vr.ui.txt` |
| `(NavigationBar)OldVRVersion` | 4d3a480c #-834650908316812828 | `fd` | older VR bar. `dumps\navbar_oldvr.ui.txt` |
| `(NavigationBar)MobileVersion` | 4d3a480c #4694977110093154933 | `6d` | - |
| `MainMenu-SizeExperiment-SmallerButtons` | 4d3a480c #4941968766206093556 | `24` | **Home page** content. `dumps\mainmenu_small.ui.txt` |
| `MainMenu-SizeExperiment-LargeButtons` | 4d3a480c #-9037983804112507045 | `9` | same with bigger tiles. `_mainmenu_large.txt` |
| `(Navigation_Home)` | 4441eb17 #2314256164230577572 | `d` | newer home page (header + carousels). `_navhome.txt` |
| `(Explore)` | 4441eb17 #-4238980999313088907 | `a` | explore page. `_explore.txt` |
| `(PlayDiscoverySections)` | 6ef67129 #-7123016506058530756 | `5` | **Play page** (sections of carousels). `_playdisc.txt` |
| `(Carousel_Rooms_WithTitle)` | 3c088378 #-8269451064539323077 | `5` | titled horizontal room carousel (height 390) |
| `(Card_Room)` | 3c088378 #-678177868845425690 | `3` | room card 304x290. `dumps\cardroom.ui.txt` |
| `(PeopleScreen)`, `(ThisRoomScreen)`, `(StopScreen)`, `(CheerScreen)`, `(VoteKickScreen)`, `(MessageDialogScreen)`, `(ReportScreen)` | 16079ee0 (see objects.tsv) | - | other watch screens (flows) |
| `(Watch_Settings_Page_*_Tab)` (Audio, Comfort, Controls, Experience, Visuals, Notifications, Misc, Export, DevOnly) | 96a9ca78 | - | settings tabs |
| `AppNavCanvas` | f124b399 #6772782859943981025 | `184` | 2025 Widget Watch. `_appnav.txt` |

Rec Room's own UI prefabs instantiate with UGUI/TMP bound but render as grey blocks (their RoundedCorners, themes,
gradients are missing scripts; rr_runtime_facts.md), so **we rebuild the UI ourselves** from these measurements.

### 2.3 Fonts (measured)

Current RRUI (watch menu 2021+) text = **Roboto Regular** (body, button labels) and **Jost Medium** (headings, text
styles 0-2). The classic 2016-2019 Rec Room look = **Futura T OT Bold** (legacy_*). All in bundle
`f124b399caea956fb2ae85b8413c07d2` (13 MB, the RRUI "core" bundle) unless the table names another bundle, and all are **container entries** (load directly by the key given):

| TMP font asset (MonoBehaviour TMPro.TMP_FontAsset) | pathId | container key | atlas | source Font (key) | notes |
| --- | --- | --- | --- | --- | --- |
| `Roboto-RegularSDFLocalized` | 5750824558662718531 | `379` | `Roboto-Regular SDF Atlas` #6544120537816503363, material `Roboto-Regular Atlas Material` #-1749851550353976253 | `Roboto-Regular` #-3727734941309344658 (key `10`, 182 KB TTF data included) | **the font of every nav/button label** in the watch; Dynamic (AtlasPopulationMode 1), multi-atlas on |
| `Roboto-RegularSDF` | -3166229231410380377 | `46` | #-5865280711253655129, material #-1339708953561366105 | Roboto-Regular | palette text styles 3-8 (body/bold) |
| `Roboto-BoldSDFLocalized` | -4404324859871380382 | `95` | `Roboto-Bold Atlas` #-4965344809119073182, material #7990366769085660258 | (Roboto-Regular source; bold atlas) | bold labels |
| `Jost-500-MediumSDF` | 9124507525632955451 | `cb7` | `Jost-500-Medium Atlas` #-6912151704439024581 (1024), material #8735186211601716283 | `Jost-500-Medium` #-765023178818479648 (key `e2ab`, 102 KB) | palette text styles 0-2 = titles / headers (style 1,2 = ALL CAPS, style 2 bold) |
| `legacy_FUTURA-T-OT-BOLDSDF` | 2045678773915079114 | `a36` | static atlas | - (static; family "Futura URW Extra Bold") | classic Rec Room signage/UI font |
| `legacy_FUTURA-T-OT-BOLDSDFLocalized` | 7300965200844984318 | `05a` | `legacy_FUTURA-T-OT-BOLD SDF Atlas` #-2844331554771857410 | - | |
| `legacy_Futura-T-OT-Bold-Oblique_19061SDF` / `...Localized` | 9129092431596572062 / -7601424879649821243 | `d6` / `424` | | | italic Futura (scoreboards, game titles) |
| `legacy_Futura_Book-Bold_19093 SDF` | 1474912443987902976 (bundle 1df8e0ef) | `8e` | | | |
| `NotoSans-RegularSDF` (+JP/KR/SC/Devanagari) | eb3aed4f #5902983942181601364 | `f` | | | CJK fallbacks (not needed) |

Extra TMP materials (presets), all f124b399 unless noted: `Roboto-BoldSDF_Outlined` (key 3c), `Roboto-BoldSDF_Outlined-Dropshadow`
(ab), `Roboto-BoldSDFLocalized_Outlined` (9a9), `Roboto-RegularSDFLocalizedOutlined` (b3f), `Roboto-BoldSDFLocalized_Overlay(WorldSpace)` (3f7),
`Roboto-RegularSDFLocalizedDropShadow` (3cb93cef.., key d). Legacy Unity `Font` objects also present: `legacy_FuturaT_Bold`
(f124b399 #-8216711628615822395, 48 KB data), `legacy_FuturaExtraBold` (1df8e0ef #3823264970713955787, key 4c).

Measured text parameters used by the watch (from the prefabs): nav-bar label TMP size 24, auto-size 20..24, character
spacing 2, weight 400, hAlign Center (2), vAlign Middle (512), colour white (on dark nav bar) or #393939 (on light cards),
overflow Ellipsis (m_overflowMode 1), no word wrap. Home big-button label TMP size 32 colour #393939. Badge ("99+") size 24 white.
Font face metrics (Roboto): pointSize 50, lineHeight 58.59, ascent 46.39, descent -12.21. Jost: lineHeight 72.25, ascent 53.5.

Runtime note: the Roboto/Jost assets are **dynamic** TMP font assets (their atlas is regenerated at runtime from the
included TTF data); `ClearDynamicDataOnBuild=1`, so the shipped atlas texture may be nearly empty and glyphs are added
on first use - this works in a player because the Font's TTF bytes are in the bundle. GUESS/verify: if TMP in our
Unity 6.3 refuses to add glyphs to a font asset created by 6000.0.27, create our own dynamic TMP_FontAsset at runtime
with `TMP_FontAsset.CreateFontAsset(robotoFont)` from the loaded `Font` (key `10`).

### 2.4 Colour theme (measured: `Palette_LightTheme`)

Asset: MonoBehaviour `RRUI.Theme.Palette` "Palette_LightTheme", bundle `287abc08039fc2e1e7ff8d45c5d43651` pathId
-4933974755402303822 (copy also in eac2a6a5c23d5ef0ac2e6080d24e9f6e). Full decoded dump: `SP\research\_palette_light.txt`
(json: `_palette_light.json`). The theme components on UI objects store only an **index**:
`SurfaceTheme.color` -> backgroundColorPalettes[i]; `LabelTheme.color` / `GraphicTheme.color` -> foregroundColorPalettes[i];
`LabelTheme.textType` -> textStylePalettes[i]; `textSize` -> textSizePalettes[i]; `ButtonTheme` -> buttonPalettes;
`ToggleTheme.toggleType` -> togglePalettes; `DepthTheme.depth` -> depthPalettes (verified by matching the colours
baked into the prefabs' Image.m_Color, e.g. SurfaceTheme 12 = Image colour (0.062,0.321,0.295) = #10524B).

**Surfaces (backgroundColorPalettes)** - "role" is our reading of where the index is used (GUESS where marked):

| idx | colour | gradient (if the surface uses it) | used for |
| --- | --- | --- | --- |
| 0 | **#F9F3EF** (warm cream) | - | **main page background** (Router surface, scrollbars, cards' page) |
| 1 | #FFFFFF | - | cards / list rows (GUESS) |
| 2 | #F9F3EF | - | secondary page bg |
| 3 | #FFFFFF | #FF7F4D -> #E99E4B (t 0.6) | orange promo gradient |
| 4 | #FFFFFF | #D48B4B -> #C56835 | brown/orange gradient |
| 6 / 20 | #F15A25 | - | orange accent |
| 7 | #B2A8A2 | #FFFFFF(0.46) -> #ABABAB | placeholder / profile-pic backdrop (Mask under RecNet image) |
| **8** | #FFFFFF | **#FF5F2F (t 0.30) -> #FF7D31 (t 1.0)**, UIGradient type 4 angle 160 | **the orange nav-bar strip** of the watch |
| 9 | #000000 @ 50% | white->transparent | scroll shadow above nav bar (48 px, alpha 1 -> 0) |
| 10 | #B92B27 | - | red / negative |
| 11 | #F9BF4D | - | yellow |
| **12** | **#10524B** (dark teal) | - | **notification badges** ("99+", "Jr.") |
| 13 | #FFFFFF | #F5D2B8 -> #F9F3EF | peach header fade |
| 14 | #FFFFFF | #F45E07 -> #CC4022 | deep orange gradient |
| 15 | #0AA975 @ 20% | - | green tint |
| 16 | #FD824D | - | light orange |
| 17 | #FFFFFF | #FF5F2F(0.3) -> #FF7D31 | = 8 |
| **18** | #FFFFFF | **#FFFFFF -> #CAC8C6** | **icon tile behind big home illustrations** (radius 30) |
| 19 | #FFFFFF | #FF7F4D -> #E99E4B(0.6) | = 3 |
| 21 | #A89381 @ 50% | - | muted brown |
| 22 | #D1460A | - | burnt orange |
| 23 | #10434D | - | dark teal |
| 24 | #D7E7E3 | - | pale teal |
| 25 | #595959 @ 88% | - | dark overlay (tooltips/dim, GUESS) |
| 26 | #FFFFFF | #738DC0 -> #96BAC8 | blue gradient |
| 27 | #7CE00E | - | lime (online dot, GUESS) |
| 28 | #DDF1B9 | - | pale lime |
| 29 | #D8AAFF | - | lilac |
| 30 | #FFFFFF | #25F4FF -> #5892FF(0.4) -> #CE46FF | RR+ rainbow |
| 31 | #3C1B5F | #3C1B5F -> #190B28 | RR+ purple |
| 32 | #786C9B | - | grey-purple |

**Foreground (text/icon) colours (foregroundColorPalettes):** 0 **#393939** (default dark text), 1 #444444, 2 #1C1831,
3 #39D427 (green), 4 **#FFFFFF** (text on orange/dark), 5 #FFFFFF@80%, 6 #B33600, 7 #393939, 8 #B92B27 (red),
9 #0AA975 (green), 10 #D64100 (orange text).

**Text styles (textStylePalettes)** - font + kerning (TMP characterSpacing) + leading + bold + transformation
(transformation 0 = none, 2 = UPPERCASE; only these two values occur):

| idx | font | kerning | leading | bold | transform |
| --- | --- | --- | --- | --- | --- |
| 0 | Jost-500-MediumSDF | 2 | -20 | no | none |
| 1 | Jost-500-MediumSDF | 2 | -20 | no | UPPER |
| 2 | Jost-500-MediumSDF | 2 | 0 | yes | UPPER |
| 3 | Roboto-RegularSDF | 2 | 0 | no | none (body; used by nav labels) |
| 4 | Roboto-RegularSDF | -3.84 | 0 | yes | none |
| 5 | Roboto-RegularSDF + `Roboto-BoldSDF_Outlined-Dropshadow` | -3.84 | 0 | yes | none |
| 6 | Roboto-RegularSDF + `Roboto-BoldSDF_Outlined` | -3.84 | 0 | yes | none |
| 7/8 | + RRplus outline materials | | | yes | |

**Text sizes (textSizePalettes, canvas units):** -1:16, 0:**24**, 1:**32**, 2:36, 3:**40**, 4:45, 5:55, 8:70, 6:90, 7:120.

**Depth (depthPalettes, canvas units towards the viewer; RR's watch is layered in 3D):** 0:0, 1:25, 2:30, 3:60, 4:75, 5:90.
The nav bar content uses depth 2 (30), side scrollbars/post-router depth 1 (25). In our build apply these as local
Z offsets (negative Z = towards the viewer for a canvas facing -Z) to get Rec Room's subtle parallax. GUESS on sign.

**Button palettes (ButtonTheme; bg colour, tints normal/hover/pressed/disabled multiply the bg; fg = text/icon):**

| type | bg | bg gradient | tint N / H / P / D | fg colour (tints) | text | audio type |
| --- | --- | --- | --- | --- | --- | --- |
| 0 **Primary orange** | - | **#F76B1C -> #F63E16** (hover #F48507 -> #F44807, pressed #FFBF77 -> #FF642A) | white | #FFFFFF | Roboto bold UPPER | 2 Primary |
| 1 orange | #E06C21 | - | W@0.15/0.30/0.45/0 | #D64100 x white | bold UPPER | 0 |
| 2 grey | #555555 | - | W@0/0.30/#F1F1F1@0.30/0 | #393939 | regular | 0 |
| 3 dark | #1C1831 | - | W@0/0.3/0.3/0 | white | regular | 0 |
| 4 negative red | white | - | #C34441 / #E24F4C / #E06865 / #C34441@0.4 | white | bold UPPER | 1 Negative |
| 5 orange flat | white | - | #D45427 / #E06C21 / #F58052 | white | regular | 0 |
| 6 cream | #F9F3EF | - | W@0.70/0.85/0.95 | #B44A25 / #E05C29 / #E57126 | regular | 0 |
| 7 maroon | #602121 | - | W@0/0.3/0.4 | white | regular | 0 |
| 9 / 13 white | white | - | #FFFFFF / #F1F1F1 / #E5E5E5 | #D45427 | bold UPPER | 0 |
| 12 white-orange | white | - | #FFFFFF / #EEEEEE / #E2E2E2 | #E06C21 | bold UPPER | 0 |
| 14/15 teal | white | #77B5BE -> #3EA4BE(0.64) | white | white | bold UPPER | 0 |
| 16 black glass | white | - | #000000@0.85 / 0.85 / 0.90 | hover/pressed #178E64 | bold | 0 |
| 17 dark glass | #000000 | - | W@0.5/0.7/0.9 | white | bold | 0 |
| 21 green | white | #005024 -> #00E200 | | white | | 0 |
| 22 RR+ | white | #25F4FF -> #0257F6 -> #BA00FD | | white | | 0 |
| 23 ghost | white | - | #FFFFFF / #C3AC93@0.4 / @0.6 | #B2A8A2 | regular, border | 0 |

**Toggle palettes (ToggleTheme; inactive -> active):** the nav-bar tabs use **toggleType 24**: bg #602121,
inactive tint white@0.0 (hover @0.3), **active tint white@0.5 (hover @0.6)**, fg white both states -> i.e. the
selected tab gets a translucent dark-maroon rounded (r 20) plate on the orange bar; unselected tabs are just the
white icon+label. Profile tab uses type 23 (same colours, active never highlighted); "Detach" type 10.
Other useful toggles: 0 (white -> orange #D45427 tint, fg orange->white; category chips), 4 (white/#555555 text ->
#DB3906 bg white text; filter chips), 7 (white cream #F9F3EF tint, text #D45427 -> orange #D45427 bg white text).

**Special colours seen in prefabs:** secondary "SwapSides" button teal gradient #77B5BE -> #3EA4BE; page-up/down
orange gradient #F76B1C -> #F63E16; "[ButtonBorder]" selection outline #D64100 (0.84,0.25,0) using sprite
`Border_Weight4_Radius20` (9-slice); scroll track #444444 r 5 width 10; handle white with `Border_Input` border.

### 2.5 Rounded corners, borders, gradients

Rec Room does **not** use 9-sliced rounded sprites for most surfaces: `RRUI.RoundedCorners` (script class
`RRUI.RoundedCorners` in RRUI.Framework.Runtime, 7eb9204a #8374423444666117361) takes a plain Image (sprite null
or `WhiteSprite`) and generates rounded geometry from a **radius in canvas units** plus a per-corner mask
(`customCorners{topLeft,topRight,bottomLeft,bottomRight}`). `RRUI.UIGradient` (type 0 = vertical, 3 = vertical
fade used for shadows, 4 = custom angle; keys with times) tints the vertices. Both scripts are missing at runtime,
so **we must reproduce them**. Radii used by the watch: nav buttons 20, page surface 20, room card 20 (image 16),
big home tiles 40 (outer) / 30 (illustration tile), quick-access 30 / 20, badges 100 (pill), profile mask 50,
scroll track 5, scroll handle 10, carousel background 30.

Recommended implementation (GUESS, standard technique): one runtime-generated white rounded-rect sprite
(e.g. 128x128 texture, radius 64 px anti-aliased, `Sprite.Create(..., border = (64,64,64,64))`, PPU 100) used with
`Image.type = Sliced` and `Image.pixelsPerUnitMultiplier = 64 / radius` (canvas referencePixelsPerUnit 100), so
any radius is exact; per-corner masks = a second sprite with square corners where needed (or a small custom
`Graphic` that emits a rounded quad with per-corner radii and a gradient - cleaner, also does UIGradient).

Sliced sprites that DO exist (all in `RRUI_Icon_Atlas(512)` unless noted; `m_Border` in px L,B,R,T):

| sprite | bundle #pathId | key | size / border / PPU | use |
| --- | --- | --- | --- | --- |
| `Border_Weight4_Radius20` | f124b399 #8063501932131695663 | `57` | 128 / 20 / 128 | **selection / focus outline** of buttons (colour #D64100, or #E15A0C focus) |
| `Border_Weight4_Radius16` | f124b399 #-5177348841714108605 | - | 9-slice | outline r16 |
| `Border_Weight3_Radius8`, `Border_Weight6_Radius16`, `Border_Weight6_Radius20`, `Border_Weight16_Radius20` | f124b399 #4580558283443420772 / #-5806074236384756850 / #-4336032227213987076 / #1006416388976056868 | - | 9-slice | outlines |
| `Border_Card` | f124b399 #-7292106586687314744 | - | 128 / 20 / 128 | card focus outline |
| `Border_Input` | f124b399 #-7614952940411403934 | `0b` | 128 / 10 / 128 | input fields, scroll handle |
| `AppNav_Border_Surface` / `AppNav_Border_Card` | f124b399 #-5618246552773531405 / #74552048704614599 | - | 256/32/128, 128/26/128 | Widget Watch outlines |
| `WatchMenuRoundedCard` | 7380adf6 #6173092594996165100 | `39` | 256 / 50 / 200 | filled rounded card (older watch) |
| `RoundedBar` | 7380adf6 #2205297513791138077 | - | 512x256 / 50 / 800 | pill bar |
| `WatchMenuGradientShadow` | 7380adf6 #-275876285772888591 | `8` | 128 / 0 / 100 | soft drop shadow |
| `InventoryCircle_Filled` / `InventoryCircle` | f124b399 #-3243904107479928569 / #7750160725286166684 | `d8` / - | 128 / 0 / 100 | round badge ("99+") |
| `Circle` | f124b399 #2781217561892571428 | - | 1024 / 0 / 100 | circle |
| `WhiteSprite` | f124b399 #-6481475835838485073 (also #5772214825146149886 key `cf6`) | - | - | plain white |
| `RoomDetail_UI_Gradient_CoverImageBoundary` | 1a69c270 #-7835805752899405336 | - | - | gradient over cover images |

UI materials: `UI-Opaque` (f124b399 #6824148392262841296, shader `UI/Opaque`, keywords UNITY_UI_ALPHACLIP,
UNITY_UI_DEPTHOFFSET) on the page surfaces; `Button_Background_Default_Light_RegularReverse` (#-2322625109044501563)
and `..._LowEmphasis` (#234506295846043305) with shader `UI/Uber Effects` (#1927892937764313254: _GradientColor1..4,
_EnableGradient) on the newer RRUI 2.0 buttons. We can use the default UI material everywhere (GUESS: the RR UI
shaders are not needed for the look, and `UI/Default` is stereo-safe).

### 2.6 Icons and illustrations

Two SpriteAtlases hold nearly all watch art (both in f124b399, both **container entries**):

| atlas | pathId | key | pages | contents |
| --- | --- | --- | --- | --- |
| `RRUI_Icon_Atlas(512)` | #-6348774286154104307 | `81` | 3 x 2048 DXT5 (`sactx-0/1/2-...-3a0c97e7`: #8974098538460935858, #5575389487005497010, #1515406414430402226) | 481 monochrome white icons (`icon_*`, `MenuIcon_Sprites_*`, borders) - tint them with the theme colour |
| `RRUI_Illustration_Atlas(1024)` | #5459815723637717648 | `e2` | 2 x 2048 DXT5 (`sactx-0/1-...-87ec2825`: #4250031914267983864, #-2530325787000881160) | 106 full-colour "sticker" illustrations (dark outline, soft gradients) |

Use `bundle.LoadAsset<SpriteAtlas>("81")` / `("e2")` then `atlas.GetSprite(name)` (returns a clone; cache it).
Many sprites are also individual container entries (keys below) - a key can be shared by several objects in the
same bundle (e.g. `e54` = `MenuIcon_Sprites_2_ShoppingBag` and `MenuIcon_Sprites_2_Gloves`), so load with
`LoadAssetWithSubAssets<Sprite>(key)` and pick by name. Exported atlas pages for reference:
`SP\research\img\ui\sactx-*.png`.

**Illustrations** (f124b399 unless noted; "-" = no own key, take it from atlas `e2`):

| sprite | pathId | key | depicts |
| --- | --- | --- | --- |
| `Illustration_Menu_RecCenter` | -8316592302765324164 | - | the Rec Center building (dome, orange RR sign) |
| `Illustration_Menu_RecCenter-Outlined` | 2357007275092189610 | - | same, outlined |
| `illustration_HomeButton_RecCenter` | -6532222617292706006 | `bf9` | Rec Center, home-button crop |
| `Illustration_Menu_Dorm` | 5780793436761876569 | - | isometric dorm room |
| `illustration_HomeButton_DormRoom` | -76324696636742012 | `597` | dorm room, home-button crop |
| `Illustration_Menu_Play` | -3229762858806621612 | `68` | gamepad + basketball + map |
| `Illustration_Menu_Play_Alt` | 6466962924503721202 | - | door (Play "door icon" variation) |
| `Illustration_Menu_Create` | 8636409163670417477 | `a69` | maker pen |
| `Illustration_Menu_Store` | -6226280644915697107 | `e4` | shopping |
| `Illustration_Menu_Event` | -2143200681699350929 | `a4` | event |
| `Illustration_Menu_Club` / `_MyClub` | 2409310850143031885 / 5597421678659714680 | `b9b7` / - | clubhouse |
| `Illustration_Menu_Challenge` | -5522628227758817658 | `ccc` | checklist |
| `Illustration_Menu_Backpack` | -1684122661537811648 | - | backpack |
| `Illustration_Menu_ThisRoom` | -7319648757183733051 | - | this room |
| `Illustration_Menu_SendInvite` | -2977313386808055310 | `3aa` | invite |
| `illustration_HomeButton_Club` / `_AboutThisRoom` | 8257468932524619500 / 7774160122718716977 | `bd` / `3c6` | |
| `Illustration_Friends` | -1802903823762087366 | `1fc` | friends |
| `Illustration_Party` | 6d3223da #-6556603686601147334 | `b` (6d3223da) | party |
| `Illustration_Trophy` | 6d3223da #7374783289776564760 | `be` (6d3223da) | trophy |
| `Illustration_QuitGame` / `Illustration_Exit` | 5888407421062118820 / -5496247273771113768 | `a7e7` / `8fd` | quit / exit door |
| `Illustration_RecRoomLogo` | 5612397205799235745 | - | RR logo |
| `Illustration_GenericFail` | 2552541325701084733 | `bab` | sad face (placeholder) |

**Nav / general icons** (white, f124b399, atlas `81`): `icon_Home` #4028326832341392647 (key `25`),
`icon_Nav_Play` #-7888984614766343699, `icon_customize_look` #-3459590439138670394, `icon_Nav_Create`
#-2413111018206171468, `MenuIcon_Sprites_2_ShoppingBag` #-5904221652797995960 (`e54`), `icon_Players`
#4901234920836974735 (`9e4`), `icon_nav_ThisRoom` #-4980556076640646314, `icon_Chat` #773670231026290464 (`47`),
`icon_Notification` #4697119013641805538 (`aea`), `icon_Gear` #-5489321053650364783 (`b1`), `icon_Expand`
#6597690002855006339 (`1ee`), `icon_Collapse` #-8096844045244799537 (`2fc`), `icon_backArrow`
#-6344971708135252236 (`77`), `icon_Backpack` #6333063820673705168 (`31c`), `icon_Party` #5432215513472939441
(`30`), `icon_Exit` #5877589638703856240 (`8d`), `icon_Star` (`1b`), `icon_Heart` (`24`), `icon_checkmark`
(`bbe`), `icon_search` (`dac`), `icon_Lock` (`6a`), `WatchMenuChevron` #4940116453458824914 (`41e`),
`WatchMenuCloseIcon` #-5537130705327080986 (`59f`), `arrow` #5770885824170411232 (`12`), `icon_RRLogoBackdrop`
#-5886234580812173607 (`77b`), `MenuIcon_Sprites_2_RECfaceSmall` #1655752065249920072 (tiler pattern),
`MenuIcon_Sprites_3(HR)_Rec_Center_Icon` #-8702098352797491609 and `..._Dorm_Icon` #-5429882211739017625 (key `f26`
= the `MenuIcon_Sprites_3(HR)` sheet).
Widget-watch icons (atlas only): `icon_AppNav_Play` #-691576553380860259, `_RecCenter` #-9198333050756120995,
`_Dorm` #-9087016459182967723, `_Players` #-6953303952981555674, `_Backpack` #-6902508110142306911, `_Settings`
#-2941450501702781801, `_Chat` #8148281873195386862, `_Party` #74848036039860113, `_Escape` #-7796606897978493660,
`_Notification` #-7309306743660056963, `_Store` #-763472464257119137, `_Location`, `_Portal`, `_Invite`, `_GoTo`.
Background tiler patterns (palette `tilerPaletteItems`): `MenuIcon_Sprites_2_RECfaceSmall` tinted white 15%, 6.5
rows, spacing (0.5,0.25) - the faint RR-face wallpaper behind profile pictures and headers.

Activity artwork: see 3.3 (none of the official rooms' cover images are in the bundles).

### 2.7 UI sounds (measured)

All clips are plain AudioClips inside the bundles below (full bundle names; `.bundle` in BUNDLES). Volumes/pitch are
Rec Room's own configured values (`RecRoomAudioClipPoolConfig` / `WatchUI` fields); pick a random clip of a pool each
time; pitchVariation = random +/- range added to pitch 1; pitchShift = constant offset (e.g. -0.65 -> pitch 0.35).

**Watch open/close + physical watch button (from `RecRoom.Core.WatchUI` on `[PlayerWatchMenu]`, c513202a #2594730460354100435)**

| event | clip (bundle, pathId) | volume | pitchVar |
| --- | --- | --- | --- |
| watch menu opened | `comped_sfx_rrui_watch_ui_open_stereo_01` (c513202af4f2b6439095734ab5caead0, 212017420819701930) | 0.05 | 0 |
| watch menu closed | `comped_sfx_rrui_watch_ui_close_stereo_01` (c513202af4f2b6439095734ab5caead0, 3294828249862906175) | 0.04 | 0 |
| button down (3D watch buttons) | `Watch2_button_down` (f4368d66c11d8d988ea177dda8d2118d, 2443297248465651961) | 0.25 | 0.1 |
| button up | `Watch2_button_up` (740d0c93a50d72fff8d36c4445188249, 5989865178240633666) | 0.25 | 0.1 |
| (older) watch open | `Watch2_open` (2a3bec6a25d0c1c9145fe7dad5cb1fe3, -8158630134292052797) | GUESS 0.25 | - |

Alternatives in the same bundle c513202a...: `sfx_rrui_watch_ui_open_stereo_01..03`, `sfx_rrui_watch_ui_close_stereo_01..03`,
`sfx_rrui_watch_ui_open_paper_01..03` / `close_paper_01..03`; cloth rustle (063b6b6653aa642616fd21d3d4701899):
`sfx_rrui_watch_open_cloth_01..03`, `sfx_rrui_watch_close_cloth_01..03` (GUESS: sleeve rustle when raising the wrist).
Page transitions (287abc08039fc2e1e7ff8d45c5d43651): `sfx_rrui_watch_ui_transition_open_01..06`, `..._transition_close_01..06`.

**RRUI button sound palette** (`AudioPalette_Default`, 287abc08039fc2e1e7ff8d45c5d43651 #-8889377385445526091; the
same asset also in eac2a6a5c23d5ef0ac2e6080d24e9f6e). Every RRUI button has an audio type; each type = 4 pools:

| button type (enum) | config asset | pointer down | pointer up | click | hover (enter) |
| --- | --- | --- | --- | --- | --- |
| 0 Default | DefaultButtonAudioPaletteConfig | SelectDown | - | SelectClick | HoverGeneral |
| 1 Negative | NegativeButtonAudioPaletteConfig | SelectDown | - | Negative | HoverGeneral |
| 2 Primary | PrimaryButtonAudioPaletteConfig | PrimaryDown | - | PrimaryClick | PrimaryHover |
| 12 Sidebar | SidebarButtonAudioPaletteConfig | SelectDown | - | SelectClick | SidebarHover |
| 14 Card | CardButtonAudioPaletteConfig | SelectDown | - | SelectClick | CardHover |
| 17 Inactive | InactiveButtonAudioPaletteConfig | InactiveDown | - | - | InactiveHover |
| 18 ToggleOn | ToggleOnButtonAudioPaletteConfig | SelectDown | ToggleOn | - | HoverGeneral |
| 19 ToggleOff | ToggleOffButtonAudioPaletteConfig | SelectDown | ToggleOff | - | HoverGeneral |
| 20 Tabbing | TabbingButtonAudioPaletteConfig | SelectDown | TabClick | - | HoverGeneral |
| 21 Slider | SliderDefaultButtonAudioPaletteConfig | SliderDown | SliderUp | - | - |
| scrolling | DefaultScrollingAudioPaletteConfig | hitting the end of a scroll list: ScrollEnd (min 100 px travel, >= 0.1 s apart) | | | |

Pools (clip names; bundle 8-char prefix; volume; pitchVariation; pitchShift):

| pool | clips | bundle | vol | pVar | pShift |
| --- | --- | --- | --- | --- | --- |
| SelectDown | `sfx_rrui_watch_ui_select_down_01..06` | 0df4dcda61c06138358ef77428370838 | 0.10 | 0 | 0 |
| SelectClick | `sfx_rrui_watch_ui_select_click_01..06` (04 has volume 0 = skip it) | f124b399caea956fb2ae85b8413c07d2 | 0.12 | 0 | 0 |
| HoverGeneral | `sfx_rrui_watch_ui_button_hover_general_01..06` | 0df4dcda... | 0.012 | 0.1 | 0 |
| Negative click | `comped_sfx_rrui_watch_ui_select_negative_01` | f124b399... | 0.2 | 0 | 0 |
| PrimaryDown | `sfx_rrui_watch_ui_select_primary_down_01` | f124b399... | 0.1 | 0 | 0 |
| PrimaryClick | `comped_sfx_rrui_watch_ui_select_primary_01` | f124b399... | 0.2 | 0 | 0 |
| PrimaryHover | `sfx_rrui_watch_ui_button_hover_card_01..06` | f124b399... | 0.01 | 0 | -0.65 |
| SidebarHover | `sfx_rrui_watch_ui_button_hover_sidebar_01..05` | f124b399... | 0.01 | 0.14 | 0 |
| CardHover | `sfx_rrui_watch_ui_button_hover_card_01..06` | f124b399... | 0.008 | 0.1 | 0 |
| InactiveDown | `comped_sfx_rrui_watch_ui_select_input_toggle_inactive_01` | f124b399... | 0.1 | 0 | 0 |
| InactiveHover | `sfx_rrui_watch_ui_button_hover_card_01..06` | f124b399... | 0.01 | 0.1 | +0.4 |
| ToggleOn | `sfx_rrui_watch_ui_toggle_button_on_01..03` | f124b399... | 0.03 | 0 | 0 |
| ToggleOff | `sfx_rrui_watch_ui_toggle_button_off_01..03` | f124b399... | 0.02 | 0 | 0 |
| TabClick | `sfx_rrui_watch_ui_category_tab_01..03`, `sfx_rrui_watch_ui_tab_over_05,06` (tab_over_04 vol 0) | f124b399... | 0.03 | 0.14-0.25 | 0 |
| SliderDown / Up | `sfx_rrui_watch_ui_slider_activate_01` / `..._deactivate_01` | f124b399... | 0.02 / 0.015 | 0.21 | 0 |
| ScrollEnd | `sfx_rrui_watch_ui_scroll_vert_stop_01` | f124b399... | 0.01 | 0.16 | 0 |

Other watch/UI clips worth using (not referenced by the palette; GUESS on exact use from their names):
`sfx_rrui_watch_ui_sidebar_{home,people,chat,profile,settings,thisroom,notification}_01..03` (home in f124b399, others
in 0df4dcda) = per-tab click stingers of the nav bar; `sfx_rrui_watch_ui_card_load_in_01..08` (f124b399) = cards
appearing; `sfx_rrui_watch_ui_favorite_01`, `sfx_rrui_watch_ui_cheer_v2_01`, `sfx_rrui_watch_ui_unfavorite_uncheer_01` (0df4dcda).

Legacy/classic clips named in the brief (all plain AudioClips):

| clip | bundle | pathId | use |
| --- | --- | --- | --- |
| `MenuClick01` | c4851f7659d9c6585ec141ead0bc40e4 (7 KB) | -8298721954535119384 | classic menu click |
| `MenuClick02` | aa02e817edf18528f57d73858c4da490 (7 KB) | 5770086312793729264 | classic menu click (alt) |
| `NotificationBuzz` | f124b399caea956fb2ae85b8413c07d2 | 9097942576596823957 | watch notification buzz |
| `Join` | 85515de19d04f3c4dfe704e6f02046e3 (146 MB bundle, random access) | 1443804865659262507 | player joined room |
| `Leave` | 85515de1... | -9048074685501398382 | player left room |
| `Expresso_Show/Up/Down/Left/Right` | 2c65be5c3a585b0783ff81cca40db13e | 5005850807993254589 / -8322307431717686766 / 3348760041772007572 / 6070621564353732476 / -8992967658772598921 | emote (Expresso) radial menu |
| `GenericTeleport01` | c513202af4f2b6439095734ab5caead0 | 7970199534627750861 | teleport |
| `ObjectiveNotification01` | c513202a... | 1020097329959800517 | objective popup |
| `music_ui_enter_room_guitar_01` | 063b6b6653aa642616fd21d3d4701899 | 6425627509938406257 | GUESS: room-enter jingle |
| `sfx_ui_keys_unlock_01` | 063b6b66... | 2544919301443822602 | unlock |
| `AmyVO_NUX16_ThisIsYourWatchMenu` | 063b6b66... | 2554759873197357277 | tutorial VO (do not need) |

**Nav-bar "flavor" sounds** (played in addition to the Sidebar palette sounds when a nav tab becomes active;
`ToggleTheme.useFlavorSFX=1`; pools live in bundle 4d3a480c5debdc4c839f125fc0c99d42, clips in 0df4dcda/f124b399):

| nav button | pool asset (4d3a480c pathId) | clips | vol | pVar |
| --- | --- | --- | --- | --- |
| Home, Play, Customize, Create, Store | NavBarHomeMagicAudioClipPoolConfig (1885948888087768457) | `sfx_rrui_watch_ui_sidebar_home_01..03` (f124b399) | 0.05 | 0 |
| People | NavBarPeopleMagicAudioClipPoolConfig (7343785870433545639) | `sfx_rrui_watch_ui_sidebar_people_01..03` | 0.08 | 0.11-0.13 |
| This Room, Quit | NavBarThisRoomMagicAudioClipPoolConfig (6253247418867451923) | `sfx_rrui_watch_ui_sidebar_thisroom_01..03` | 0.025 | 0 |
| Chat | NavBarChatMagicAudioClipPoolConfig (5173136184853119915) | `sfx_rrui_watch_ui_sidebar_chat_01..03` | 0.05 | 0.23-0.31 |
| Notifications | NavBarNotificationMagicAudioClipPoolConfig (70198677679744347) | `sfx_rrui_watch_ui_sidebar_notification_01..03` | 0.03 | 0 |
| Settings | NavBarSettingsMagicAudioClipPoolConfig (-2389464889543518121) | `sfx_rrui_watch_ui_sidebar_settings_01..03` | 0.04 | 0 |
| Profile | NavBarProfileMagicAudioClipPoolConfig (-745214864960442732) | `sfx_rrui_watch_ui_sidebar_profile_01..03` | 0.02 | 0 |

Button press "pop" animation (`ButtonAnimationPalette_Default`, 287abc08 #9055926913921059226): click scale **1.03**
over **0.05 s** (scale up then back). Tint fade duration of every button colour transition: **0.1 s**.

Volume note: Rec Room's volumes are very low linear values (0.008-0.25) because its UI mixer bus is boosted; in our
project play them through a 2D AudioSource on the watch with a UI gain multiplier of GUESS x4 (tune by ear so the
click ~ -20 dBFS), keeping the relative ratios above.

### 2.8 Layout sizes (RectTransforms, canvas units)

Measured with `RRT tree` (RRT_RT=1) and compacted by `tools\uic.pl`; full per-node specs in
`SP\research\dumps\*.ui.txt` (rruiroot, navbar_pc, navbar_vr, navbar_oldvr, mainmenu_small, cardroom). Notation:
LE = LayoutElement (min/pref/flex), HLG/VLG pad = left,right,top,bottom, sp = spacing, align = TextAnchor
(0 UpperLeft, 1 UpperCenter, 3 MiddleLeft, 4 MiddleCenter).

**Frame** (`(RRUIRootScreen)` -> `[WatchMenuRRUIRoot]`):

| node | size / layout | look |
| --- | --- | --- |
| `(RRUIRootScreen)` | 2386.8 x 1344, world-space Canvas | transparent |
| `[WatchMenuRRUIRoot]` | 2032 x 1230, scale 1.176, HLG sp 20 align 4 | - |
| `Container` | HLG sp 20 | - |
| `Surface` | LE min 1920 x 1230; VLG align 4 | - |
| `Mask/Content/Router` (the page) | LE min height 1080, pref 1230; width 1920 | **#F9F3EF** (surface 0), radius **20**, material UI-Opaque |
| `NavBarGroup` (bottom of Content) | VLG | - |
| `[ScrollShadow]BottomShadow` | anchored to the nav bar top, height 48, ignore layout | black 50% -> transparent (UIGradient type 3), radius 30 on bottom corners only |
| `PC Background` / `New VR Background` | width 1920 (Old VR 1720), white, radius 20 | - |
| `PC Console Content` / `VR Content` | LE min height **130 (PC)** / **150 (VR)**; radius 20; depth 2 (30) | **orange gradient #FF5F2F (t 0.30) -> #FF7D31 (t 1.0), angle 160 deg** |
| `[VRSideScrollbars]` Left / Right | 170 wide, outside the page (x -25 / +25), VLG sp 10 | cream scrollbar pill (r 20) with PageUp/PageDown buttons 205 x 150 (orange gradient #F76B1C -> #F63E16, r 20, `arrow` icon) and a "SwapSides" button (teal #77B5BE -> #3EA4BE, `Mobile_UI_HandSwap`) |
| `Overlay` (z -2) | same size | dialogs, toasts, callouts, emoji picker, `HighlightFadeout` (z -80) |
| `PostRouterContent` | 1920 x 1080 top-anchored, depth 1 | `[DetachMenuNote]` 1920 x 100 above the page (legacy Futura 45, white 39%) |

**Nav bar** (`(NavigationBar)PCConsoleVersion`, stretched into the orange content):

| node | layout |
| --- | --- |
| root | HLG **pad 10,10,15,10, sp 20**, childControl W/H, no force expand |
| `[PanelVertical] WatchIcon Panel` (left) | HLG sp 10: `My Profile` (LE min width 150, flex H 1), `[SeperationLine]` (3 px, cream, inactive), `Detach` (110 x 90..110, VR) |
| `[PanelVertical] Browse` (middle, flex) | HLG **sp 20, align MiddleCenter**, `Spacer Left` (flex 1) ... `Spacer Right` (flex 1) around the 9 tab buttons |
| `[PanelVertical] Bottom Panel` (right) | HLG sp 20, LE 150: `Settings` (+ inactive `Exit`, min width 138) |
| each tab `[IconButtonWithNotiBadge] X` | **LE min/pref width 150**, VLG (child control W/H, force expand W) |
| . `MultiGraphicButton` | ColorTint target = `Background`; colours normal (1,1,1,0), highlighted (1,1,1,0.30), pressed (1,1,1,0.40), fade 0.1 s |
| . . `Background` | stretch, ignore layout, Image **#602121**, radius **20** (toggle palette 24: alpha 0 idle, 0.3 hover; active tab alpha 0.5, active+hover 0.6) |
| . . `Icon and Text` | VLG **pad 5,5,5,5 sp 5** |
| . . . `Icon - Change Icon Here` | Image white, preserveAspect, sprite 128 px at PPU 128 = **100 x 100** (Play/Customize/Create/Store have LE pref 100 x 100) |
| . . . `[Text] Name` -> TMP | LE min height 30; TMP **24 (auto 20-24), white, Roboto-RegularSDFLocalized, charSpacing 2, centre, ellipsis** |
| . . `[Text] Noti` (badge) | anchored top-right (anchor 1,1, pivot 1,0, pos (15,-40)), height 45, ContentSizeFitter horizontal, HLG pad 10,10; Image **#10524B**, radius **100** (pill); TMP "99+" 24 white, min width 25 |
| `My Profile` button | MultiGraphicButton normal (1,1,1,0.5), hover 0.6, pressed 0.7; VLG pad 42,42,5,0 sp 5; `[RecNetIcon]` Mask r 50 over `#B2A8A2` + tiler; `[Text] JuniorStatus` badge (#10524B, r 100, "Jr.", anchored bottom-right) |

**Home page** (`MainMenu-SizeExperiment-SmallerButtons`, 1920.67 x 500, HLG pad 0,0,20,0 sp 20 align 4):

| node | layout / look |
| --- | --- |
| `[PanelHorizontal] Main Menu` | HLG, LE min/pref height **220**; 7 buttons |
| each big button (`Play`, `Create`, `Store`, `Events`, `Club`, `Challenge`, `Backpack`) | LE pref width **190**; `MultiGraphicButton` Image #E06C21 **radius 40** with ColorTint normal alpha 0 / hover 0.30 / pressed (0.945,0.945,0.945,0.30) (button palette 11 -> invisible at rest, orange glow on hover); VLG **pad 15,15,15,12 sp 10** |
| . `Icon - Change Icon Here` (tile) | LE min height **140**, flex W 1; Image white with **UIGradient angle -45 deg #FFFFFF -> #CAC8C6** (surface 18), **radius 30**; child Image = the illustration (e.g. `Illustration_Menu_Play`, 256 px at PPU 100) preserveAspect |
| . `[Text]` | LE min height 50; TMP **32**, #393939, Roboto-RegularSDFLocalized, charSpacing 2, centred ("Play", "Create", "Store", "Events", "Clubs", "Challenges", "Backpack") |
| . `[Text] Noti` | 65 x 65 circle `InventoryCircle_Filled` #10524B at top-right (-10,0), TMP 24 white |
| . `[ButtonBorder]` (inactive; controller focus) | 9-slice `Border_Weight4_Radius20` #D64100 |
| `Line` | HLG pad 0,0,15,15, LE min width 3: `[SeparationLine]` Image **#F15A25** (surface 6) - a 3 px vertical orange line |
| `Quick Access` -> `[PanelHorizontal] Rooms` | VLG; rows: `Dorm`, `Rec Center`, `Clubhouse`, (`Evict All`, owner only) |
| each quick-access button | LE min/pref width **380**; MultiGraphicButton Image #E06C21 radius **30** (alpha 0 idle / 0.3 hover); VLG pad 0,0,10,10 align 4; `Background` inset 10 px all round (sizeDelta -20,-20) white -> #CAC8C6 gradient radius **20**; `Icon and Text` HLG pad 10,10 sp 10 align MiddleCenter: illustration **100 x 100** (`Illustration_Menu_Dorm` / `Illustration_Menu_RecCenter` / `Illustration_Menu_MyClub`), label TMP **32 Roboto-BoldSDFLocalized #393939** ("Dorm Room", "Rec Center", "Clubhouse") with an optional "Go to" line (TMP 24/28 regular), and a `[Icon]` 60 x 40 `icon_backArrow` #393939 (GUESS: rotated 180 deg to point right) |

**Room card** (`(Card_Room)`, 304 x 290): transparent card (radius 20); `Room Image` inset 16 left/right/top,
AspectRatioFitter width-controls-height **16:9** (272 x 153), radius **16**; `Primary Text` bottom-anchored at y 55,
height 48, TMP **32 bold #AC4306**, NotoSans-RegularSDF, charSpacing -0.25, left-aligned; `Subtitle Area` y 8 height
44 (tags, HLG sp 10); badges (`Badge_PopularNow` 190 x 40 top-left, `Badge_Boosted` 40 x 40 top-right); `Focus
indicator` 9-slice `Border_Weight4_Radius20` #E15A0C. **Carousel** (`(Carousel_Rooms_WithTitle)`, height 390):
header 70 high with title TMP 32 bold #393939 and a "More Rooms" button (300 x 64, r 20); card list = horizontal
ScrollRect (elastic 0.1, inertia) with 96 x 96 prev/next buttons (r 20, `WatchMenuChevron` 72 x 72 #BF4A08).

**World size** (GUESS from the measured chain): `[PlayerWatchMenu]` root scale 0.5 x Canvas 1280 x 960 at 0.0008
= 0.512 x 0.384 m, `Screens` container scale 0.85 -> the watch screen is **~0.44 m wide** when shown on the wrist;
the page is 1920 units, so ~0.00023 m per unit. A detached menu in RR looks about twice that size. Proposal: wrist
menu 0.45 m wide, 0.40 m from the eyes; detached / desktop panel 0.9 m wide at 0.8 m (same as ETS panels).

### 2.9 The classic "Watch v2" look (2018-2020), still in the bundles

The screens under `Assets/Core/Content/Watch_v2/Screens/...` and `.../Watch_v3/Screens/...` (all in 16079ee0,
container keys = those full paths, e.g. `Assets/Core/Content/Watch_v2/Screens/People/(PeopleScreen).prefab`) and
the `[ScreenHeader]` of `(RRUIRootScreen)` keep the older, very recognisable style. Layout specs:
`SP\research\dumps\screen_confirmdialog.ui.txt`, `screen_people.ui.txt`, `screen_playerlist.ui.txt`,
`screen_thisroom.ui.txt`, `screen_instancebrowser.ui.txt`.

| element | measured |
| --- | --- |
| screen | **1280 x 960** (4:3), VLG: `Header` (min height 130) + `Body` (VLG pad 50,50,0,50 sp 50) |
| header row | HLG pad 40,40,15,15 sp 20: Home (150 x 100), title (flex), Bug/Close (45 x 45), party/invite/share buttons (150-220 x 100) |
| **3D button** (`AGUI.StackedUI.Button3D`) | a 2D plane (`2DPlane`, Image **#6900C1 purple**, material `DepthUI_Mat` = shader `UI/Default-Depth`) floating **50 units in front** of a holographic box (`3DObjectContainer` scaled to the button size x 50 deep, mesh `ButtonRectangle` f124b399 #8046151053738374648 - 92 verts, 1.1 x 1 x 1.1 unit box - with `WatchUI_Holographic_Button_Mat` + `UIButtonMask_Mat` + `UICutoutMask_Mat`); in VR the finger **pushes the plane in** from offset -50 to -2.5 (`constraints.UnpressedOffset -50, MaxPressedOffset -2.5`), hair-trigger cancel 0.5; toggle-on colour **#FF6400** orange; tints normal 0.9, highlighted 1.0, pressed 0.588, disabled alpha 0.25, fade 0.1 s |
| body panel | Image **#0096FF at 90%** (0, 0.588, 1, 0.9), 9-slice `SquareUIButton` (f124b399 #-2032820507600567072, key `12` = the `MenuIcon_Sprites(HR)` sheet; rect 145 px, border 25, PPU 100), VLG pad 30,30,20,20 sp 30 |
| text | **Futura** (`legacy_FUTURA-T-OT-BOLDSDFLocalized`, f124b399 key `05a`), white: dialog title 72 (auto 60-72), body 54, button labels 36, small 25 |
| icons | white sprites from the `MenuIcon_Sprites(HR)` sheet (key `12`): `Home` #2065959042400634080, `invite` #1577977548242470112, `share` (#4219771814317967539, key `a0b`), `arrow`, `MenuIcon_Sprites_2_Maximize`, `MenuIcon_Sprites_3(HR)_BUG` |

Use it if the implementers prefer the "old Rec Room" feel (purple push-buttons on cyan panels, Futura); the RRUI
style (cream + orange, Roboto) of 2.4-2.8 is the current one. Both can share the same code with a theme switch.

## 3. Re-creation spec: OUR watch menu (UnityEngine.UI + TMP, built at runtime)

Everything below is a PROPOSAL built on the measurements of section 2. Names of our classes are suggestions
(`RrWatch*`). Units are canvas units of a 1920-wide page (Rec Room's own numbers), so the measured sizes can be
copied 1:1.

### 3.1 Screens

| screen (nav tab) | content in Dreamport | notes |
| --- | --- | --- |
| **Home** (default) | Rec Room "MainMenu" row: big tiles **Play**, **Backpack**, **Settings** (we drop Create/Store/Events/Clubs/Challenges, or show them disabled at 40% alpha with the "Inactive" sounds), orange separator, **Quick Access**: "Go to **Dorm Room**", "Go to **Rec Center**" | tiles jump to Play tab / open backpack / Settings; quick access teleports (networking 5.3) |
| **Play** | vertical scroll of titled carousels ("Rec Room Originals" sections: Hubs, Sports, Shooters, Quests, Racing, Party) of **room cards** for the 21 activities (3.3) | card click opens a **Room details** sub-page: big 16:9 art, title, player count "n / cap", instances list, orange primary button **"Go"** / "Join" |
| **People** | players in the world, grouped "In this room" (same instance) / "Elsewhere" with the activity name; row = name + activity + "Join" button | data from presence slots (networking 5.4) |
| **This Room** | activity name + art header, players here with team colour, "Leave" (back to Rec Center), instance authority badge (GUESS: debug only) | |
| **Settings** | our settings: watch hand (Left/Right), open by (Look / Tap), UI volume, SFX volume, music volume, snap turn is the client's, "Show watch clock seconds" | persistence: Bridge LocalStorage (conventions 18) |
| Customize, Create, Store, Chat, Notifications | show the tab (for the look) but the page shows `Illustration_CannotDoThisAction` / `Illustration_NoAccess` + text "Not available in Dreamport" | or hide these tabs (then keep Home, Play, People, This Room, Settings = 5 tabs, still 150 wide each, centred by the spacers) |
| Profile (left) | the player's Somnium name, no photo (use the `icon_RRLogoBackdrop` tiler on #B2A8A2 with `icon_ProfilePic` white) | |

### 3.2 Layout (build order; all numbers measured unless GUESS)

```
RrWatchMenu (world-space Canvas, layer 5, 2032 x 1230, no CanvasScaler; GraphicRaycaster + TrackedDeviceGraphicRaycaster)
 +- Root (HLG sp 20, align MiddleCenter)
 |   +- Surface (VLG, 1920 x 1230)
 |   |   +- Page (Image #F9F3EF, rounded r 20, LE min 1920 x 1080, pref h 1100 GUESS; RectMask2D for content)
 |   |   |   +- <screen content>  (padding 40 GUESS; Home row is 500 tall at the top)
 |   |   +- NavBarShadow (48 tall, black 50% -> 0 upwards, ignoreLayout, anchored to the nav bar top)
 |   |   +- NavBar (rounded r 20, gradient #FF5F2F->#FF7D31 angle 160, height 150 VR / 130 desktop; local z -30 = depth 2)
 |   |       +- HLG pad 10,10,15,10 sp 20
 |   |       +- Profile (min w 150) | [VR] Detach (110 x 110) | SpacerL(flex) | tabs x N (150 each, sp 20) | SpacerR(flex) | Settings (150)
 |   +- (VR only) SideScrollbars L/R (170 wide, 25 outside the page; PageUp/PageDown 205 x 150)
 +- Overlay (z -2): toasts/dialogs (confirm "Go to Paintball?" etc.)
```

**Tab button** (one prefab-like builder):
* root 150 x (bar height - 25), `RrButton` (our Selectable) whose target is `Plate`;
* `Plate`: rounded r 20, colour #602121; alpha: idle 0, hover 0.30, pressed 0.40, **active tab 0.50** (hover 0.60),
  colour fade 0.1 s;
* `Icon`: 100 x 100, white, preserveAspect, sprite from atlas `81` (table in 1.3);
* `Label`: TMP Roboto-RegularSDFLocalized 24 (auto 20-24), white, characterSpacing 2, centred, ellipsis, 30 tall,
  5 px under the icon (VLG pad 5 sp 5);
* `Badge` (optional): pill #10524B r 100, height 45, TMP 24 white, anchored top-right (15,-40).

**Home tile** (Play/Backpack/Settings...): 190 x 220; plate #E06C21 r 40 (alpha 0 idle / 0.30 hover / 0.30
pressed, i.e. an orange glow appears on hover); VLG pad 15,15,15,12 sp 10; `Tile` 160 x 140 rounded r 30 with a
-45 deg gradient #FFFFFF -> #CAC8C6 holding the illustration (`Illustration_Menu_Play`, `Illustration_Menu_Backpack`,
`illustration`... see 2.6) preserveAspect; `Label` TMP 32 #393939 Roboto-RegularSDFLocalized, charSpacing 2, centred.
Separator: 3 px wide, #F15A25, 15 px top/bottom inset. **Quick access** row: 380 wide x ~130 GUESS; plate #E06C21
r 30 (alpha 0/0.3); inner tile inset 10 px, white -> #CAC8C6 r 20; contents HLG pad 10 sp 10 centred:
illustration 100 x 100 (`Illustration_Menu_Dorm`, `Illustration_Menu_RecCenter`), two lines "Go to" (TMP 24
regular #393939) + "Dorm Room" (TMP 32 Roboto-BoldSDFLocalized #393939), chevron `icon_backArrow` 60 x 40 #393939
rotated 180 deg (GUESS).

**Room card** (Play): 304 x 290; art area inset 16, 16:9 (272 x 153), rounded r 16, drawn as: background gradient
(per activity, 3.3) + art Image (preserveAspect or cropped sprite) + optional 30% black bottom fade
(`RoomDetail_UI_Gradient_CoverImageBoundary`); title TMP 32 **bold** #AC4306 left-aligned at y 55 (NotoSans or
Roboto-BoldSDFLocalized - GUESS: use Roboto, NotoSans is only for CJK), subtitle line TMP 24 #393939 at y 8 (e.g.
"Sports - 2-6 players"); hover: whole card scales 1.03 over 0.05 s + `Border_Weight4_Radius20` outline #E15A0C;
badge top-left "3 playing" pill #10524B r 100 (or `Badge_PopularNow` style). Carousel: title TMP 32 bold #393939,
cards spaced 32 GUESS, horizontal ScrollRect (elasticity 0.1, inertia), prev/next 96 x 96 white r 20 with
`WatchMenuChevron` 72 x 72 #BF4A08.

**Buttons in pages**: primary = palette button 0 (gradient #F76B1C -> #F63E16, hover #F48507 -> #F44807, pressed
#FFBF77 -> #FF642A, white bold UPPERCASE Roboto, audio type Primary); secondary = palette 9/13 (white, text #D45427
bold UPPERCASE, hover #F1F1F1, pressed #E5E5E5); negative = palette 4 (#C34441 / hover #E24F4C / pressed #E06865,
white text, audio Negative); corner radius 20 (GUESS for page buttons; the carousel "More Rooms" button is r 20,
64 tall).

**Fonts**: load `Roboto-RegularSDFLocalized` (f124b399 key `379`), `Roboto-BoldSDFLocalized` (`95`),
`Jost-500-MediumSDF` (`cb7`, headings: page titles TMP 55 Jost, charSpacing 2, GUESS size from textSize 5);
fallback: `TMP_FontAsset.CreateFontAsset(Font "Roboto-Regular" key 10)`; last resort LiberationSans SDF.

**Themes**: hard-code the palette values of 2.4 in a static `RrUiTheme` class (they are 30 numbers; reading
`Palette_LightTheme` at runtime is possible through our MonoBehaviour-data parser but not needed).

### 3.3 The Play list: artwork for the 21 activities

Fact: **no official room cover image exists in the bundles** (Rec Room served them from rec.net; searched all
Sprite/Texture2D names for every activity). Below is the best in-bundle art per activity, found by searching the
index and checking each image by eye (exports in `SP\research\img\act\` and `...\act\crops\`). "crop" = the Rect to
pass to `Sprite.Create(tex, rect, pivot 0.5, 100)` in **texture pixels, origin bottom-left** (Unity convention),
measured by eye +-5 px. "key" = container key in that bundle (load the Texture2D directly); "via" = a container
whose loading makes the texture resident (then `Resources.FindObjectsOfTypeAll<Texture2D>()` by name).
Card background = a gradient we draw behind the art (GUESS colours chosen to match each art).

| id | activity | art | bundle (full) #pathId | key / via | crop (x,y,w,h) | card bg | quality |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2 | **The Rec Center** | `Illustration_Menu_RecCenter` (the RR building sticker) | f124b399caea956fb2ae85b8413c07d2 #-8316592302765324164 | atlas `e2` `GetSprite` (or `illustration_HomeButton_RecCenter` key `bf9`) | whole sprite, centred, 140 tall | #F5D2B8 -> #F9F3EF (surface 13) | good (official UI art) |
| 1 | (Dorm Room, Home only) | `Illustration_Menu_Dorm` | f124b399 #5780793436761876569 | atlas `e2` (or `illustration_HomeButton_DormRoom` key `597`) | whole | #D7E7E3 | good |
| 3 | **3D Charades** | gift-box print: grinning RR face, player, curtain, "REC ROOM" | 56e54a941a1085685fd924215dfa98b5 #8497364866841070682 (`GiftBox_Charades_Tex`, 1024 DXT5) | key `0` | (8, 276, 380, 268) | none (art has its own card-paper bg) | good, official line art |
| 4 | **Bowling** | MISSING. Closest: maker-pen thumbnails `SpawnableToolTex_BOWLING_PIN` + `SpawnableToolTex_BOWLING_BALL` (256 px renders on white) | 61073772489cb694b58c9785c0c38b5b #3495288315271083094 (Sprite) / 478b96c906290001a69aed604d5fcf4a #-5078462379292980962 (Sprite) | keys `8` / `b` | whole sprites, pin left + ball right | lane wood #C68A4B -> #8A5A2B GUESS | fair |
| 5 | **Crescendo of the Blood Moon** | camera-filter overlay: **Dracula** (cut-out render, right half) | 85515de19d04f3c4dfe704e6f02046e3 #6446503188250673887 (`Consumable_Polaroid_Dracula_Tex(2048)`, 2048x1152 DXT5 alpha) | via `(CameraFilterConsumable_Dracula)` key `4d` | whole (16:9, subject at x 1010-2048) | blood-moon night #5B0E1C -> #12060B GUESS + title on the empty left | good |
| 6 | **Disc Golf** | gift-box print: player throwing a disc at a basket | b974181e59512db1d701cd27a7e9ba62 #-1004698853475201603 (`Giftbox_DiscGolf_Tex`) | key `8` | (8, 276, 380, 268) | none | good |
| 7 | **Dodgeball** | gift-box print: two players and a ball | a6675d4780512e8aaf600c698255ed07 #-4732287560964118177 (`GiftBox_DodgeBall_Tex`) | key `9` | (8, 276, 380, 268) | none | good |
| 8 | **Isle of Lost Skulls** | camera-filter overlay: **Ghostbeard** (the quest's boss; left half) | 85515de1 #1068491230219024041 (`Consumable_Polaroid_GhostBeard_Tex(2048)`) | via `(CameraFilterConsumable_GhostBeard)` key `bd` | whole (subject x 0-1130) | sea/night #1E6F8C -> #0B2230 GUESS | good |
| 9 | **Laser Tag** | "LASER TAG - ALPHA TEAM!" t-shirt decal (neon logo) | 1732ad1bc2c9469d57e3219c1205ccbe #-3540715528701913227 (`MaleTShirt_Shirt_LaserTag_Alpha_DECAL`, 1024) | key `2` | logo only: (80, 414, 860, 530) | #1C1831 -> #3C1B5F | good |
| 10 | **Make It To Midnight** | MISSING cover. Closest: **Bonky the Clown** face (MITM's hunter) from the Bonky hammer skin | 5c91ea11b5d1563aae878dd8be401e34 (324 MB) #-9156539268589812605 (`Weapon_BonkyHammer_Skin_BonkyFace_Tex`, DXT1) | via EquipmentSkin `Paintball_SplatHammer_Skin_BonkyFace` key `64c` (verified chain: skin -> `Weapon_BonkyHammer_Skin_BonkyFace_Mat` #7279247270440788617 -> this texture) | (700, 604, 220, 260) | circus red/yellow stripes #E0101A / #F0A030 GUESS | fair; alt: `ClashOfClowns_Ultimate_Icon` f124b399 #3422520949126599709 (zombie clown, has pass badges) |
| 11 | **My Little Monsters** | MLM tee print: cute Mothman monster | 719628ff260b74ead402162674bd8c57 #7154587420942674059 (`UntuckedTee_Shirt_MLM_Mothman_Tex`) | key `7` | (70, 564, 320, 380) | #7B64C8 -> #5A48A0 (matches the print) | good; alt "Cryptid Creek" park poster 396eae396cf2f2a627483978d0d7b1a6 #2733755068750551311 key `a`, crop (105, 594, 225, 325) |
| 12 | **Paddleball** | gift-box print: paddle, player, ball | b0ea06af364a6288a4e0440a74dde9a5 #265319921675186996 (`GiftBox_Paddleball_Tex`) | key `5` | (8, 276, 380, 268) | none | good |
| 13 | **Paintball** | gift-box print: red + blue team players with paintball guns | fcdca0286787152343d13711413ffacf #-902424125500487654 (`GiftBox_Paintball_Tex`) | key `3` | (8, 276, 380, 268) | none | good |
| 14 | **Punch Up** | MISSING (Punch Up was an announced 2016 boxing original that never shipped - wiki). Closest: `MenuIcon_Sprites_2_Gloves` (white glove icon) | f124b399 #1961401339218904136 | key `e54` (sheet; `LoadAssetWithSubAssets<Sprite>`) | whole, 120 px, white | orange gradient #F76B1C -> #F63E16 | weak |
| 15 | **Quest for the Golden Trophy** | camera-filter overlay: **Goblin King** (right half) | 85515de1 #395788247597843364 (`Consumable_Polaroid_Goblin1_Tex(2048)`) | via `(CameraFilterConsumable_GoblinKing)` key `14e` | whole (subject x 1060-2048) | castle gold #B8923E -> #5A4318 GUESS | good |
| 16 | **Rec Rally** | gift-box print: buggy + racer with chequered flags | f9e615a726449a027a304f2427d00aa4 #3259606272287262279 (`GiftBox_RecRally_Tex`) | key `6d` | (8, 276, 380, 268) | none | good; extra icons in `RecRallyIconAtlas(1024)` (e2ade4f20354802a95a7a321df723e29 key `c`) |
| 17 | **Rec Royale** | **"REC ROYALE" badge logo** | 2bd69e86a021f5f428720beb70ff1586 #7445029315490137487 (`Rec_Royale_Badge_Logo`, 1024) | key `7` | whole (diamond badge) | #5B9AA0 -> #3F6E73 (badge sky) | good; alt zombie variant 29c59e4ab412cf37b677ecc4a04a8dfa #2300927463789549143 key `53` |
| 18 | **Rec Room Showdown** | MISSING (western 3v3 in "Sarsaparilla Springs", 2022). Closest: saguaro cactus from `Standee_CactusTumbleweed_Tex` | d10447fc7fb3dd6312c640a4b3dd438b #4107299620806186412 | via container `Assets/Activities/Quest/Shared/Content/Enemies/Transformations/Standee_Cactus_PlayerTransformation.prefab` | saguaro (70, 364, 340, 620); tumbleweed (700, 70, 250, 250) | desert sunset #F2A541 -> #C1502E GUESS | fair |
| 19 | **Soccer** | gift-box print: players and a ball | a3a7abccb87ecd24b6aa3091216297d3 #-3093255798911653092 (`GiftBox_Soccer_Tex`) | key `3` | (8, 276, 380, 268) | none | good |
| 20 | **Stunt Runner** | **"STUNT RUNNER" logo decal** (purple/blue on orange triangle) | 502a254b705e9487ac53c56b51bc6943 #6586765308188880567 (`StuntRunner_Decal_Tex`, 1024) | key `9` | (70, 234, 880, 585) | #2A1B4A -> #120A24 GUESS | excellent (official logo) |
| 21 | **The Curse of the Crimson Cauldron** | camera-filter overlay: **the Witch** (the quest's boss, left half) | 85515de1 #3275326765579235562 (`Consumable_Polaroid_Witch_Tex(2048)`) | via `(CameraFilterConsumable_Witch)` key `d8d` | whole (subject x 0-1060) | purple night #3C1B5F -> #190B28 (surface 31) | good |
| 22 | **The Rise of Jumbotron** | camera-filter overlay: **Jumbotron** (top-right) | 85515de1 #-2238994449571915633 (`Consumable_Polaroid_Jumbotron_Tex(2048)`) | via `(CameraFilterConsumable_Jumbotron)` key `121` | whole (subject x 1360-2048, y 470-1152) | sci-fi magenta #3A0A3C -> #12040F GUESS | good |

Notes on the art:
* The **gift-box prints** (7 activities) share one layout: the 1024 texture holds the same illustration twice, at
  (8, 276, 380, 268) and (8, 4, 380, 268); both work. They are flat two-colour line art (red / green / blue / grey
  per activity) on a card-paper background with a "REC ROOM" logo - consistent as a set.
* The **camera-filter overlays** (5 quests) are 2048x1152 transparent cut-out renders meant to be composited over a
  photo; use the whole texture as the 16:9 card image over the gradient; put the title text on the empty side.
  The store-preview versions (`Consumable_Polaroid_*Store_Tex`, 1024 DXT1, with big "DRACULA"/"GHOSTBEARD"/"WITCH"/
  "JUMBOTRON" titles over a pirate-island photo) exist too but look odd (all have the pirate beach).
* All 16:9 art should be drawn with `preserveAspect` inside the 272 x 153 area; the 380 x 268 gift crops are 1.42:1,
  so fill the card with their paper colour (sample the crop corner) to avoid bars.
* MISSING and weak entries (Bowling, Make It To Midnight, Punch Up, Showdown): if a better image is wanted, render a
  3D snapshot of a signature prop at load time with a hidden camera into a RenderTexture (bowling pin + ball from
  the Bowling kit, Bonky's hammer, a boxing glove prop if one exists, a Showdown revolver/cactus) - GUESS, cheap
  (one frame per card, cached).

Grouping for the Play page carousels (GUESS, mirrors RR's "Rec Room Originals" genres):
**Hangouts**: Rec Center. **Sports**: Bowling, Disc Golf, Dodgeball, Paddleball, Soccer, 3D Charades.
**Shooters**: Paintball, Laser Tag, Rec Royale, Rec Room Showdown. **Quests**: Quest for the Golden Trophy, The
Curse of the Crimson Cauldron, The Rise of Jumbotron, Isle of Lost Skulls, Crescendo of the Blood Moon.
**Racing**: Rec Rally, Stunt Runner. **Party & horror**: Make It To Midnight, My Little Monsters, Punch Up.
Subtitle per card: genre + player range from networking 5.1 (e.g. "Quest - 1-4 players").

### 3.4 Animations and sounds (exact values)

**Menu open** (`PlayerMenu_Show`, scale of the menu root, decoded from the streamed curves; sample rate 90):

| t (s) | scale X | scale Y | scale Z |
| --- | --- | --- | --- |
| 0.000 | 0.01 | 0.01 | 1.0 |
| 0.056 | 1.00 | 0.046 | 1.0 |
| 0.211 | 1.20 | 1.20 | 1.20 |
| 0.422 | 1.00 | 1.00 | 1.00 |

(i.e. a horizontal line snaps out, then the panel opens vertically with a 20% overshoot and settles; use
smooth/cubic interpolation between keys). **Close** (`PlayerMenu_Hide`, 0.417 s): 0 -> 1.0, 0.200 -> 1.2 (all),
0.367 -> (1.0, 0.046, 1.0), 0.417 -> (0.01, 0.01, 1.0). **Page transition** (`PlayerMenu_Transition`, 0.411 s): 0 ->
1; 0.044 -> (1.05, 1.10, 1.05); 0.133 -> (1.0, 0.50, 1.0); hold to 0.267 (swap page content here); 0.367 -> (1.05, 1.10,
1.05); 0.411 -> 1. **Refresh** (`PlayerMenu_Refresh`): same timing, Y dips to 0.745, peaks 1.025-1.028.
Fade plane behind the menu: to 80% black in 0.1 s (VR only; GUESS: skip on desktop). Button click pop: scale 1.03
for 0.05 s. All colour tints: 0.1 s.

**Sounds** (clips and volumes from 2.7; a random clip from each pool; pitch = 1 + pitchShift + random(-pVar, +pVar)):

| event | pool(s) |
| --- | --- |
| menu opens | `sfx_rrui_watch_ui_open_paper_01..03` 0.15 pVar 0.16-0.19 + `sfx_rrui_watch_ui_open_stereo_01..03` 0.025 (c513202a) |
| menu closes | `sfx_rrui_watch_ui_close_paper_01..03` 0.10 + `sfx_rrui_watch_ui_close_stereo_01..03` 0.02 (c513202a) |
| raise wrist / dwell starts (VR) | `sfx_rrui_watch_open_cloth_01..03` (063b6b66) GUESS vol 0.1 |
| page transition | `sfx_rrui_watch_ui_transition_open_01..06` (287abc08) GUESS vol 0.05 |
| nav tab hover / click | SidebarHover (0.01) / SelectDown (0.10) + SelectClick (0.12) + the tab's flavour pool (2.7) |
| card hover / click | CardHover (0.008) / SelectDown + SelectClick; cards appearing: `sfx_rrui_watch_ui_card_load_in_01..08` GUESS 0.02 |
| primary button ("Go") | PrimaryHover (0.01, pitch 0.35) / PrimaryDown 0.1 / `comped_sfx_rrui_watch_ui_select_primary_01` 0.2 |
| negative button | `comped_sfx_rrui_watch_ui_select_negative_01` 0.2 |
| toggles / tabs / sliders | ToggleOn/Off, TabClick, SliderDown/Up pools (2.7) |
| scroll hits the end | `sfx_rrui_watch_ui_scroll_vert_stop_01` 0.01 |
| notification on the watch | `NotificationBuzz` (f124b399 #9097942576596823957) + controller haptic GUESS 0.3 amp 0.1 s |
| a player joins / leaves your instance | `Join` / `Leave` (85515de1 #1443804865659262507 / #-9048074685501398382) |

### 3.5 Placement, input and loading recipe

**The watch object** (VR and desktop both show it on the avatar's wrist; desktop players rarely see it):
* `RrWatchModel` GameObject (NOT a child of the avatar): MeshFilter `Wrist_Watch`, MeshRenderer `Watch_Wrist_mat`,
  LODGroup optional, clock hands = 3 cubes as in 2.1 driven by `DateTime.Now` (hour hand 30 deg/h + 0.5 deg/min,
  minute 6 deg/min, second 6 deg/s, about local Z, the face at local z 0.04).
* Each frame in `RenderPipelineManager.beginCameraRendering` (ETS tip, conventions 10.4): pose =
  `leftHand.TransformPoint(anchorPos)`, rotation `leftHand.rotation * anchorRot`, with the hand =
  `SomniumBridge...LocalPlayer.References.Body.LeftHand` (AoM `AomBridge.HandOf(false)`). The RR anchor offset
  (-0.02, 0, -0.119) is relative to RR's hand pivot; the Somnium IK hand bone pivot differs - GUESS start with the
  watch centre ~0.02-0.05 m up the forearm from the wrist joint (RR's avatar skeleton: 1.8 cm, see 2.1; RR's
  hand-relative anchor puts the face 12 cm behind the hand centre), face on the back of the wrist (AoM used
  `up = facing * up`, `position = wrist + up * 0.02`), then tune in a headset.
* Remote players: show the same model on their left hand (bridge remote bodies) so everyone "has a watch"; show
  the small amber hologram rectangle (`WatchUI_Holographic_Remote_Mat`) in front of their wrist while their menu
  is open (bit in their presence slot).

**Opening**:
* VR, gaze: every frame compute the angle between the head forward and (watchFace - head) (<= 60 deg), the wrist
  pitch/roll relative to "face towards eyes" (<= 45 / 30 deg); when true for 0.4 s continuously (drive the dwell
  ring fill 0 -> 1 meanwhile), open. Close when angle > 60 or pitch > 50 or roll > 50, unless detached. Do not open
  while the hand holds a prop (RR's `preventsGazeOpenMenuHandStates`).
* VR, tap (setting): the right index fingertip (or right controller position) enters a 0.08 x 0.06 x 0.05 m box
  centred 0.05 m above the face -> toggle.
* VR, detach: a "Detach" nav button (icon_Expand) leaves the menu where it is (world-locked, 0.9 m wide) until
  "Collapse" or the menu is closed.
* Desktop: **H** toggles; the panel is placed 0.8 m in front of the camera, 0.9 m wide (ETS `EtsVrPanel.Face`
  pattern), mouse clicks through the client's XRUIInputModule; Backspace = back; H again closes. While open,
  our gameplay keys are ignored (ETS `EtsMenu` rule).
* Pointer: the canvas needs `GraphicRaycaster` + `TrackedDeviceGraphicRaycaster`, whole tree on **layer 5 (UI)**,
  `worldCamera` re-assigned every 0.5 s (conventions 10.2); draw it over the world (ZTest Always / overlay queue,
  AoM `OverEverything`) only when detached is off and it would clip into walls - GUESS.

**Loading recipe** (at world start, after the RR install is found; all names from 2.x):
1. Open bundles (`AssetBundle.LoadFromFile`, cheap): f124b399, 287abc08, 0df4dcda, c513202a, 38066d85, 4d3a480c,
   063b6b66, 1df8e0ef (holo mats) + their deps from `deps.tsv`; art bundles of 3.3 (56e54a94, a6675d47, b0ea06af,
   fcdca028, f9e615a7, a3a7abcc, b974181e, 2bd69e86, 1732ad1b, 502a254b, 719628ff, 61073772, 478b96c9, d10447fc,
   5c91ea11, 85515de1).
2. `LoadAsset<SpriteAtlas>("81")`, `("e2")` from f124b399; fonts by key (`379`, `95`, `cb7`); `Watch_Wrist_mat`
   (`0f` of 38066d85).
3. Audio: **87 of the UI clips are container entries** - load them directly by key (appendix A: e.g.
   `sfx_rrui_watch_ui_select_click_01` = f124b399 key `69f`, `NotificationBuzz` = f124b399 `3f9`, `MenuClick01` =
   c4851f76 `d`). The palettes (`AudioPalette_Default`, `Palette_LightTheme`) are NOT container entries (we hard-code
   their values anyway). Watch mesh + open/close clips: `LoadAsset<GameObject>("8")` of c513202a (`(Player)`) makes
   `Wrist_Watch`, `Watch_Wrist_Hand_mat`, the WatchUI / WatchMenuAudioPalette clips (open/close paper + stereo) and
   the holo materials resident. Then `Resources.FindObjectsOfTypeAll<AudioClip/Mesh/Material>()` by name into
   dictionaries. Do not instantiate `(Player)`. Page-transition clips (287abc08) and Join/Leave (85515de1): the
   referencing container is not identified (GUESS: optional; fall back to `sfx_rrui_watch_ui_category_tab_0x`).
4. Activity art by key (table 3.3) or via the listed container (`LoadAsset<GameObject>(key)` without instantiating),
   then `Sprite.Create` with the crop rect; cache.
5. Build the UI once (hidden), 5 pages, reuse; ~200 Graphics total.
6. Fallbacks: missing sprite -> coloured rounded rect with the activity initial (TMP 90); missing font -> TMP
   default; missing clip -> silent. Log each miss once (conventions 7).

Memory: atlases 2 + 3 pages 2048 DXT5 (~4 MB each ~ 20 MB), five 2048x1152 overlays (~2.4 MB each), gift boxes
0.7 MB each. Unload the art bundles' unused assets after building (`bundle.Unload(false)` keeps loaded assets).

## 4. Open questions, GUESSes and risks

1. **TMP font assets across versions** (6000.0.27 -> 6000.3): UNVERIFIED whether the dynamic Roboto/Jost
   `TMP_FontAsset`s add glyphs in the Somnium client; fallback `TMP_FontAsset.CreateFontAsset(Font)` from the
   included TTF (`Roboto-Regular` key `10`, 182 KB). Test in the client early.
2. **SpriteAtlas loading**: `LoadAsset<SpriteAtlas>("81")` + `GetSprite` should work (atlases are container
   entries) but was not probed; the editor probe only proved GameObject/AudioClip/Texture loading. Fallback: load
   individual sprites by their own keys (2.6) or `Sprite.Create` from the exported atlas page texture using the
   atlas `m_RenderDataMap` rects (`_atlas_icon.json`, `_atlas_illus.json`).
3. **Home tile label colour**: the label carries GraphicTheme 0 (#393939) and sits inside the orange (#E06C21)
   plate, which is invisible at rest (button palette 11 tint alpha 0) - so dark text on cream at rest; the orange
   only shows on hover (30%). If it looks wrong in practice, use white text on a solid #E06C21 plate. GUESS.
4. **World size of the menu** (0.44-0.9 m) and its distance: derived from the prefab scale chain, not measured in
   game (3.5). Tune in a headset.
5. **Watch pose on the Somnium hand**: RR's anchor offsets are relative to RR's hand pivot; the Somnium IK hand
   bone differs. Start from AoM's wrist-panel code (conventions 10.4) and tune.
6. **Depth offsets** (DepthTheme 25/30 units) sign and use: GUESS (towards the viewer).
7. **Gradient direction** of `UIGradient` type 4 angle 160 (nav bar): which end is #FF5F2F is a GUESS (probably the
   top/left, darker orange); the difference is subtle.
8. **Crop rectangles** of section 3.3 are measured by eye on exported PNGs (+-5 px); verify once in Unity and adjust.
9. **Join/Leave, page-transition clips**: their referencing container was not identified (not container entries).
10. **Rec Room shutdown** (2026-06-01, reported by a search summary): rec.net images/APIs are gone; everything
    must come from the install (as planned).
11. **Volumes**: RR's UI volumes (0.008-0.25) are tuned for its own mixer; apply one global UI gain (GUESS x4).
12. **Colour-space**: palette colours were read as stored (sRGB values, Unity Color); Somnium is linear-space URP,
    UGUI converts automatically - use the hex values as sRGB.

Tools created for this research (reusable): `SP\research\tools\rrt\` (rrtool copy whose `tree` prints
RectTransforms when env `RRT_RT=1`; build with `dotnet build -c Release -o out`), `SP\research\tools\uic.pl`
(UI spec compactor), `SP\research\tools\sprkey.sh <names...>` (bundle / pathId / container key of sprites and
textures). Texture exports were made with `SP\work\meshtool\out\meshtool.exe tex <bundle> <outdir> <pathId[:x:y:w:h]>`
(DXT1/DXT5/RGB(A) only; BC7 unsupported).

## Appendix

### A. Container keys of the UI audio clips (load directly with `bundle.LoadAsset<AudioClip>(key)`)

Format `clip = key`, grouped by bundle. Clips NOT listed here are not container entries (watch open/close paper/stereo, page transitions, cloth, Join/Leave, Expresso): they become resident when `(Player)` (c513202a key `8`) or the prefab that uses them is loaded (Join/Leave: referencing container not identified - GUESS find it with a reference scan, or use MenuClick01/02 instead).

* **f124b399caea956fb2ae85b8413c07d2**: NotificationBuzz = `3f9`; comped_sfx_rrui_watch_ui_select_input_toggle_inactive_01 = `2d3`; comped_sfx_rrui_watch_ui_select_negative_01 = `4d`; comped_sfx_rrui_watch_ui_select_primary_01 = `68c`; sfx_rrui_watch_ui_button_hover_card_01 = `798`; sfx_rrui_watch_ui_button_hover_card_02 = `5b8`; sfx_rrui_watch_ui_button_hover_card_03 = `e5`; sfx_rrui_watch_ui_button_hover_card_04 = `c3`; sfx_rrui_watch_ui_button_hover_card_05 = `d37`; sfx_rrui_watch_ui_button_hover_card_06 = `726`; sfx_rrui_watch_ui_button_hover_sidebar_01 = `ba7`; sfx_rrui_watch_ui_button_hover_sidebar_02 = `ce`; sfx_rrui_watch_ui_button_hover_sidebar_03 = `cd`; sfx_rrui_watch_ui_button_hover_sidebar_04 = `efe`; sfx_rrui_watch_ui_button_hover_sidebar_05 = `71`; sfx_rrui_watch_ui_card_load_in_01 = `6`; sfx_rrui_watch_ui_card_load_in_02 = `6b`; sfx_rrui_watch_ui_card_load_in_03 = `1e`; sfx_rrui_watch_ui_card_load_in_04 = `2d`; sfx_rrui_watch_ui_card_load_in_05 = `17a`; sfx_rrui_watch_ui_card_load_in_06 = `44f`; sfx_rrui_watch_ui_card_load_in_07 = `bb`; sfx_rrui_watch_ui_card_load_in_08 = `b6`; sfx_rrui_watch_ui_category_tab_01 = `8ff`; sfx_rrui_watch_ui_category_tab_02 = `c6a`; sfx_rrui_watch_ui_category_tab_03 = `7e4`; sfx_rrui_watch_ui_scroll_vert_stop_01 = `a5e`; sfx_rrui_watch_ui_select_click_01 = `69f`; sfx_rrui_watch_ui_select_click_02 = `fd`; sfx_rrui_watch_ui_select_click_03 = `8fd9`; sfx_rrui_watch_ui_select_click_04 = `726b`; sfx_rrui_watch_ui_select_click_05 = `ba`; sfx_rrui_watch_ui_select_click_06 = `017`; sfx_rrui_watch_ui_select_primary_down_01 = `e8`; sfx_rrui_watch_ui_sidebar_home_01 = `7b`; sfx_rrui_watch_ui_sidebar_home_02 = `35ed`; sfx_rrui_watch_ui_sidebar_home_03 = `aee4`; sfx_rrui_watch_ui_slider_activate_01 = `f6`; sfx_rrui_watch_ui_slider_deactivate_01 = `09`; sfx_rrui_watch_ui_tab_over_04 = `328`; sfx_rrui_watch_ui_tab_over_05 = `7dc`; sfx_rrui_watch_ui_tab_over_06 = `72`; sfx_rrui_watch_ui_toggle_button_off_01 = `6dc`; sfx_rrui_watch_ui_toggle_button_off_02 = `eb1`; sfx_rrui_watch_ui_toggle_button_off_03 = `656`; sfx_rrui_watch_ui_toggle_button_on_01 = `21`; sfx_rrui_watch_ui_toggle_button_on_02 = `01`; sfx_rrui_watch_ui_toggle_button_on_03 = `da`
* **0df4dcda61c06138358ef77428370838**: sfx_rrui_watch_ui_button_hover_general_01 = `bd`; sfx_rrui_watch_ui_button_hover_general_02 = `2a`; sfx_rrui_watch_ui_button_hover_general_03 = `a`; sfx_rrui_watch_ui_button_hover_general_04 = `67`; sfx_rrui_watch_ui_button_hover_general_05 = `82`; sfx_rrui_watch_ui_button_hover_general_06 = `9`; sfx_rrui_watch_ui_cheer_v2_01 = `03`; sfx_rrui_watch_ui_favorite_01 = `65`; sfx_rrui_watch_ui_select_down_01 = `5`; sfx_rrui_watch_ui_select_down_02 = `4`; sfx_rrui_watch_ui_select_down_03 = `2e`; sfx_rrui_watch_ui_select_down_04 = `8`; sfx_rrui_watch_ui_select_down_05 = `0`; sfx_rrui_watch_ui_select_down_06 = `f`; sfx_rrui_watch_ui_sidebar_chat_01 = `b`; sfx_rrui_watch_ui_sidebar_chat_02 = `e`; sfx_rrui_watch_ui_sidebar_chat_03 = `ae`; sfx_rrui_watch_ui_sidebar_notification_01 = `e6`; sfx_rrui_watch_ui_sidebar_notification_02 = `3`; sfx_rrui_watch_ui_sidebar_notification_03 = `fd`; sfx_rrui_watch_ui_sidebar_people_01 = `6`; sfx_rrui_watch_ui_sidebar_people_02 = `2`; sfx_rrui_watch_ui_sidebar_people_03 = `4c`; sfx_rrui_watch_ui_sidebar_profile_01 = `0d`; sfx_rrui_watch_ui_sidebar_profile_02 = `c`; sfx_rrui_watch_ui_sidebar_profile_03 = `25`; sfx_rrui_watch_ui_sidebar_settings_01 = `af`; sfx_rrui_watch_ui_sidebar_settings_02 = `88`; sfx_rrui_watch_ui_sidebar_settings_03 = `c6`; sfx_rrui_watch_ui_sidebar_thisroom_01 = `1`; sfx_rrui_watch_ui_sidebar_thisroom_02 = `87`; sfx_rrui_watch_ui_sidebar_thisroom_03 = `d`; sfx_rrui_watch_ui_unfavorite_uncheer_01 = `cf`
* **c4851f7659d9c6585ec141ead0bc40e4**: MenuClick01 = `d`
* **aa02e817edf18528f57d73858c4da490**: MenuClick02 = `6`
* **f4368d66c11d8d988ea177dda8d2118d**: Watch2_button_down = `5`
* **740d0c93a50d72fff8d36c4445188249**: Watch2_button_up = `5`
* **2a3bec6a25d0c1c9145fe7dad5cb1fe3**: Watch2_open = `7`

