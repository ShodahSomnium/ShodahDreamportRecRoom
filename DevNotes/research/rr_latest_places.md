# Rec Room: the FINAL Dorm Room and Rec Center (pre-shutdown, to 1 June 2026)
rplaces research task, 2026-09-24. Research only: nothing in CODE was changed. Paths: SP = the working scratchpad
(`C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad`),
B43 = bundle `43ea1176ca25c0333547813ec3298808`, B85 = `85515de19d04f3c4dfe704e6f02046e3`. Keys are Addressables
container keys (`RrAssets.Load` by name works the same way). **GUESS** marks everything not backed by a source.

---

## 0. Summary (read this first)

**The Dorm Room is already (almost) the final version.**
* The last structural rebuild of the default dorm was the 27 Apr 2018 "Spring Cleaning" remodel. Until the end, the
  default dorm stayed that room. The user's own Rec Room log from 26 Jul 2025 loads the dorm as Unity scene
  `DORM_ROOM` / `dormroom2`. The Maker Pen `Dorm_*` kit we built from is that room: the same loft, skylight, slanted
  window and arched window.
* Later changes were to contents and behaviour only:
  - Jul 2023: the door became "Room-i-verse" and the community board became the "Play" board.
  - 2024: the "My Dorms" / Custom Dorms experiment.
  - 22 Sep 2025: the door defaulted to **^RecCenter** again. A Watch setting could switch it back to Roomiverse.
* Differences worth fixing are small (section 2.2):
  - the two social posters were probably the notebook-style NUX posters, not the 2018 palette posters;
  - add the help buttons;
  - one armchair, not two;
  - the dresser probably no longer stood in the final default dorm;
  - a whiteboard and markers on the loft;
  - the mirror button labels;
  - the Roomiverse door option.

**The Rec Center we built is not any real version. The real final one is a 2025 Rec Room Studio rebuild, and its
geometry is not in anything we are allowed to read.**
* Correction to `rr_reccenter.md`: "New layout, fresh paint, some nice big windows" is the **28 Aug 2018** "^RecCenter
  edition" (Steam gid 2444779293805621016), not Sep 2020. That 2018 room (rec.net RoomId 170126, created
  2018-08-28T15:44Z) was the Rec Center's last full redesign. After that it was only changed:
  - Jan 2019: info kiosk, ping-pong tables, a second hoop, things moved around;
  - 2021: room-search doors, the Magic Door, the Friend-O-Tron;
  - 2022 on: the storefront, then seasonal pop-up shops;
  - Apr 2024: the upstairs slideshow removed.
* **4 Mar 2025: the ^RecCenter "became a Rec Room Studio room"** (rec.net `BecameRRStudioRoomAt`
  2025-03-04T19:41:39Z). New subrooms followed:
  - "RecCenter-0314" (Apr 2025), then "RecCenter" (May 2025);
  - "Home_082525" (25 Aug 2025; a YouTube video that day is titled "YOU Didn't Notice The NEW Rec Room Rec Center
    UPDATE");
  - final public subroom "Home" (Unity scene a75f7547-…, sandbox = Rooms 2).
  The old 2018 scene (cbad71af-…) survived only as the private subroom "Switch_Home". The user's log from 26 Jul 2025
  shows the public Rec Center loading as `MAKER_ROOM_2` / `ugcr2` plus a 5.7 s **Studio asset-bundle download**. So
  the final geometry came from Rec Room's CDN, not from the install.
* The 22-30 Jan 2026 "Class of '87 Reunion" was a **temporary** recreation of the pre-2018 Rec Center (a modified
  ^LockerRoomArchive by @happyvr). It is not the final ^RecCenter.
* What is certain about the final room: its content list (3.1.3). Visible sources agree that it stayed the 2018
  building: a long **barrel-vaulted hall** with clerestory and large windows, lower side wings, an upstairs, and warm
  wood floors. The detailed layout is only partly known. The wiki caption of the 2018-2021 room says, seen from the
  Dorm hallway:
  - right: Merch Booth and the QUESTS door;
  - left: the community board;
  - centre: the "shop", with the PvP door behind it;
  - "everything else to the left".
* Rebuild plan in section 3.3: the same frame rules as `rr_reccenter.md` §3, at the **world origin** (decision 1).
  Specifically:
  - a vaulted hall of about 24 x 32 m plus a west wing, an upstairs balcony and the Dorm hallway;
  - **4 category doors plus the Magic Door, which open a filtered list** (Rec Room's behaviour since Jun 2021), not 21
    single-activity doors;
  - only props that are proven Rec Center props or are named in patch notes;
  - **no pool table** (Rec Room's pool table was in ^Bowling);
  - MaxPlayers 12 (rec.net, 2024-2026).
  Positions, sizes and colours are marked GUESS: confidence is low for geometry and colours, and medium to high for
  contents and history.
* **The single biggest open lever (3.1.5):** `C:\Users\derii\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache`
  (418 MB, 2,022 files, large `.data` files dated 26 Jul and 7 Aug 2025) very probably still holds the final Rec
  Center's Studio bundle, which would contain the real geometry. It is not an install file, and it is outside the
  approved sources, so it was **not opened**. The user must decide (question 1). The archived room pictures (3.1.4)
  would also settle the look.

Exported PNGs (50, section 4): `SP\harness\out\export\*.png`, durable copy in `SP\research\img\places\` (with crops
and contact sheets `sheet_*.png`, `map_reccenter.png`, `map_dorms.png`).

---

## 1. Sources and method

| source | how read | what it gave |
| --- | --- | --- |
| Fandom wiki (MediaWiki API, `action=parse&prop=wikitext`, `list=search`, `prop=revisions`, `prop=imageinfo`) with curl + UA header | text; raw files in `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\1c0947a6-bfa3-41e9-ae24-3394d8a31490\scratchpad\rplaces\w_*.txt` (session scratch) and `SP\research\dorm\wiki\*.json` | Dorm_Room, Rec_Center, Timeline, News Archive 2018-2025, Weekly Rotation (+ Archive 2023-2025), Challenges, Kiosk, Mirror, Map:Campus Map; revision dates; image upload dates |
| Steam news app 471710 (`SP\research\dorm\wiki\steamnews.json`, 564 items 2016-06-30 .. 2026-04-06) | PowerShell keyword extract (`rplaces\steamnews_hits.txt`) | dated patch notes (Steam truncates some long items; the wiki News Archive has the full text) |
| Wayback Machine CDX + `id_` raw captures | text only | rooms.rec.net JSON for RoomId 170126, rec.net/room/RecCenter (16 captures 2021-2026: og:image, SubRooms, tags), rec.net/room/DormRoom, rec.net/creator/p/class-of-87-reunion, recroom.com ship-notes |
| YouTube | watch/search pages as text (title, publishDate, description) | dated video titles; captions were not obtainable (empty timedtext) |
| User's Rec Room logs `C:\Users\derii\AppData\LocalLow\Against Gravity\Rec Room\Player-prev.log` (26 Jul 2025), `Player.log` (7 Aug 2025) | grep for room loads only | which Unity scene each room used in 2025 |
| rrtool index `SP\rrtool\idx\*.tsv` + `rrtool dump/tree` | as in the other docs | bundle evidence (section 5) |
| Harness export `RrHarness.RrExport.ExportArt` (new, section 4) | 1 run, 50/50 written | Rec Room's own pictures of both rooms |

Not done (rules): no image files downloaded from the web. The wiki screenshots and rec.net room pictures were not
viewed; their URLs are listed in 3.1.4 and 2.1.4 so the user can open them. Nothing in the install's
level/sharedassets/resources was read, and neither was the HTTPCache.

---

## 2. The Dorm Room

### 2.1 (a) The final version

#### 2.1.1 Timeline (dated, sourced)
| date | change | source |
| --- | --- | --- |
| 27 Apr 2018 | "Fancy new dorm room ... more spacious layout and a larger mirror" (the "Spring Cleaning" remodel = the Maker Pen `Dorm_*` kit) | wiki News Archive 2018; Timeline "April 27, 2018 - New Dorm Room released" |
| 18 Jan 2019 | Dorm becomes a saveable Maker Pen room ("Social Dorm"); Safe Mode loads the default dorm | Timeline; Steam 2019-10-02 |
| Jul 2019 | HoloHelpers greet new players in the dorm | Steam 2019-07-18 |
| 17 Sep 2019 | clothes move from the dresser drawers to the **mirror** | Steam 2019-09-17 |
| 21 Apr 2021 | dorm mirror repackaged as the Mirror prop | Steam 2021-04-22 |
| May 2021 | Code of Conduct prompt the first time a new player leaves the dorm | wiki News Archive 2021 |
| Dec 2021 - Jul 2022 | "Dorm Skins" button on the mirror, Dorm Skin store | Timeline, Steam |
| **7 Jul 2023** | "Rec Center" door replaced by the **"Room-i-verse" door** (opens the Watch Play menu); community board replaced by the **Play board** (shows the Play menu) | wiki Dorm_Room "Update History" (edit by BenjiWiki 2023-12-22) |
| Oct 2023 | wiki contents list rewritten "since a few things are not accurate to the present dorm room" | Dorm_Room revisions 2023-10-20 (Detective Knuckles) |
| Jun-Aug 2024 | Custom Dorms / "My Dorms" experiment (multiple dorms, walls/floor/door fixed) | Steam 2024-06-12, 2024-08-06 |
| Oct 2024 | wiki screenshots updated ("20210519163637 1.jpg" re-uploaded 2024-10-19; "Dorm room community board.jpg" 2024-05-17) | imageinfo |
| Nov 2024 | experiment: new users see the **categories page on the bulletin board in their dorm** | Steam 2024-11-23 |
| Feb 2025 | mirror button "Custom Shirts" renamed **"Custom Items"** | Steam 2025-02-19 |
| May 2025 | Avatar Studio items can be tried on in the dorm; Maker AI "dorm environment" (RR+) | Steam 2025-05-06 |
| **22 Sep 2025** | "The dorm room door now defaults to **^RecCenter**. If you prefer 'Roomiverse', there's an option for that (Watch > Settings > Experience)." | Steam 2025-09-23 (gid 1811138915541432); wiki Timeline "September 22, 2025 - Re-added Rec Center door" |
| 26 Jul 2025 (observed) | default dorm still loads Unity scene **DORM_ROOM ("dormroom2")** | user's `Player-prev.log` (`LoadScene(DORM_ROOM)`, `Load Scene dormroom2`) |
| 1 Jun 2026 | servers off | Wikipedia, Steam 2026-03-30 |

No structural dorm redesign after April 2018 appears in any source: the Timeline, the 564 Steam items, the wiki News
Archives 2018-2025 or the Dorm_Room history.

#### 2.1.2 What the final default dorm contained
From the wiki Dorm_Room page as rewritten in Oct 2023, Dec 2023 and Oct 2024 ("An unmodified Dorm Room contained"),
cross-checked with the new-player challenges (wiki Challenges, removed Jan 2024):
* a **mirror** with 5 buttons: Customize, Dorm Skins, Rec Room+, Outfits, Custom Clothing (renamed "Custom Items" in
  Feb 2025);
* a **bed with a desk below it** (loft bed), with props such as a **radio** on the desk;
* a **board showing the Play menu** (the Play board);
* the **Code of Conduct**, plus buttons that open the **Privacy Policy and Help Center** (kit `Dorm_Help_Buttons`:
  Help, Privacy Policy, Terms of Service);
* **two posters showing the Block/Mute and Friends features**. The wiki: "Most items could be found in the Palette
  of your Maker Pen, with the exception of the social posters". The 2018 posters `(Sandbox_MuteGesture_Poster)` and
  `(Sandbox_ShakeHands_Poster)` ARE palette items, so the dorm's own posters were something else. The best match is
  the notebook-paper NUX posters with per-platform variants (`Nux_Stop_Gesture2`, `Nux_Shake_hands2`, plus `_PSVR`),
  which are standalone Addressables, not palette items (**GUESS**, medium);
* a **basketball hoop above a dustbin**, and a basketball ("there's one in your dorm room!");
* a **ladder to the second floor** (loft);
* **an armchair** (singular) **and some plants**;
* **art supplies and a canvas** (loft). The new-player challenge says "Pick up a **marker in the loft** of your dorm
  room, then select the **white board** there to draw";
* a **water bottle** ("Find the water bottle in your dorm room");
* the **Campus Map** ("By default your dorm room contained a Campus Map");
* the exit **door**: labelled for the Rec Center (2016 - Jul 2023, and the default again from 22 Sep 2025), or
  "Room-i-verse" (Jul 2023 - Sep 2025, and optional afterwards).
* Not in the final list: the **dresser**. It stopped being the clothes UI in Sep 2019 and is absent from the
  2023/2024 list, so it was probably removed (**GUESS**).
* Tutorial VO still in the Startup bundle (063b6b66):
  - `ExploreNUX_02` (#-6904143099054111611; subtitle "This is your dorm room, a quiet place to prepare for a big day
    in Rec Room.");
  - `ExploreNUX_03` (#6721287937923792997; "Feel free to play around in your dorm room until you're comfortable
    moving and grabbing stuff.");
  - `AmyVO_CohortNux_01` (#-3274596626915155, "... then go through that door.");
  - `CohortNUX_UseTheMirrorToCustomizeYourLook` (#-1202921961373180947).

#### 2.1.3 Look
* Architecture = the kit (`rr_dorm_core.md` §3.2, exact): 9.7 x 5.9 m, loft at 2.75 m, skylight, slanted big window,
  round-arched window, pillar box.
* Colours: the kit's architecture shader is recoloured at runtime from palettes that are not in the bundles. What we
  have:
  - the campus-map dorm icon (`map_dorms.png`): an isometric cut-away with an **orange wood floor**, a **purple
    rug**, **dark (brown-grey) walls**, a teal element (window or board) and white skylight slats;
  - the watch illustrations (`illustration_HomeButton_DormRoom`, `Illustration_Menu_Dorm`): a **brown-framed
    arched window**, brown ladder shelves with books, a camera and a plant, and a bed with an **orange** blanket;
  - the kit's palette thumbnail (`SpawnableToolTex_DORM_BUNK_BED`): a **purple** blanket, white pillow and brown
    wood.
  The wall colour is therefore **unknown** (**GUESS**). Our #A9BFD0 pale blue-grey is not contradicted by anything
  we have seen but is also not supported. The wiki screenshots would settle it (question 3).
* Lighting: Apr 2019 lighting overhaul ("the dorm room is a little darker than you expected", Dev Q&A: Let There Be
  Lights). Jul 2025 "new rendering engine" (Timeline 2025-07-28). Oct 2025: "Reflective surfaces and avatar items can
  react to lights". No per-room values are available.
* Sounds: no music (the radio is opt-in); outdoor ambience (our `AmbianceSummerOutdoor`, GUESS).

#### 2.1.4 Pictures that would settle the look (not downloaded; for the user)
* Wiki: `File:20210519163637 1.jpg` ("View of the Dorm Room", 1857x1010, re-uploaded 2024-10-19);
  `File:Dorm room community board.jpg` ("The Play Board", 2024-05-17); `File:Rec_Room_7_27_2020_2_58_27_PM.png`
  (mirror).
* rec.net ^DormRoom og:image: `https://img.rec.net/9irm0spmie3ete7nomyru9asu.png` (page captured 2026-06-05).

### 2.2 (b) What differs from what was built (CODE/Places/Dorm, README "Dorm Room")
| item | built | final (source / confidence) | action |
| --- | --- | --- | --- |
| architecture | kit, exact (10 mm) | same room (dormroom2 scene, 2025 log) HIGH | none |
| exit door | `RrPortal` "REC CENTER" at (2.75, 0, 2.90) yaw 180 | "Rec Center" by default since 22 Sep 2025, "Room-i-verse" optional HIGH | keep. Optional: a Watch setting "Dorm door: Rec Center / Room-i-verse" (Room-i-verse = the door opens the Watch Play page instead of travelling) |
| board | `RrDormBoard` "PLAY" with cover tiles | Play board = the Watch Play menu on the board (`(PlayCommunityBoard)` UI, 6ef67129 key `3`); new users saw the categories page (`(PlayHighlight_CategorySelect)CommunityBoardVariant` key `0`) HIGH | keep. Optional: a category row (Quests / Sports & PvP / Party / ...) on top, like the Play menu |
| social posters | `(Sandbox_MuteGesture_Poster)` + `(Sandbox_ShakeHands_Poster)` (2018 cartoon posters) | two non-palette posters for Block/Mute and Friends; probably `Nux_Stop_Gesture2` + `Nux_Shake_hands2` (notebook style) MEDIUM-GUESS | swap to the NUX textures on our own quads (2.3) |
| help buttons | none | CoC + Privacy Policy / Help Center buttons HIGH | add a small 3-button panel next to the CoC poster (decorative, or open our help text) |
| mirror buttons | our "MIRROR" panel (mirror on/off, put things back, Rec Center) | Customize / Dorm Skins / Rec Room+ / Outfits / Custom Items HIGH | keep our functions (the avatar is never modified). Optional: show Rec Room's five labels greyed out as decoration, with ours below |
| armchairs | 2 `(Sandbox_CouchChair)` (under the loft + on the loft) | "A armchair" (one) MEDIUM | drop the loft armchair (the loft holds the art corner) |
| dresser | `(Dorm_Dresser)` under the loft, with bottles, bell and book on top | not in the 2023/2024 list MEDIUM-GUESS | remove, or keep as decoration (low priority). If removed, move its props to the desk |
| loft art corner | `(ArtCanvas)` + `(Sandbox_Paintbrush)` | "art supplies and a canvas", "a marker in the loft ... the white board" HIGH | keep the canvas; add `(SandboxMarkerSet)` (B43 key `1e4`) on the loft shelf; the canvas acts as the "white board" |
| water bottle, basketball, hoop over the bin, radio on the desk, ladder, plants, campus map, CoC | built | same HIGH | none |
| props not in the wiki list (pizza, apple, lamp, cup, dice, frisbee, bell, books, Oscar trophy, root beers) | built | not listed (the list is not exhaustive: "such as a radio") | keep, harmless |
| colours | walls #A9BFD0, ceiling #C9D6E0, white frames | unknown (2.1.3) | none until a screenshot exists |

### 2.3 (c) Dorm rebuild plan (only the deltas; everything else stays as `rr_dorm_core.md` §3 and the built code)
Frame: the room frame of `rr_dorm_core.md` §3.1 (x -4.94..4.77, z -2.96..2.96, floor y 0), built at the world
origin. Wall props face +z of their root; back wall (z -2.96) yaw 0; front wall (z +2.96) yaw 180.
1. **Posters**: replace the two Sandbox posters with our own quads (0.62 x 0.62 m, 0.01 m off the wall, textures
   loaded by name):
   - Stop Gesture: `Nux_Stop_Gesture2` (bundle 6a69c4f283fb66b549d698cb46729dd5 key `0`, #-3241244070201691738,
     1024², DXT5), centre (-3.25, 1.45, -2.72), yaw 0.
   - Shake Hands: `Nux_Shake_hands2` (3c03b87a9850a50a249a3c98b0355046 key `a`, #4656361576300778954), centre
     (-4.15, 1.45, -2.72), yaw 0.
   - Material: `RrLook` unlit or lit opaque with the texture (the textures carry alpha for rounded paper holes; use
     cutout at 0.5, GUESS).
   - Optional extra (not in the wiki list): `Nux_Fist_Bump2` (1a733ad6... key `6`) and `Nux_Open_Watch2`
     (a754c58d... key `4`, "look at your wrist") by the door, which fits Dreamport's watch.
2. **Help buttons**: a 0.46 x 0.9 m panel at (-1.35, 1.35, -2.935), yaw 0, the same style as our MIRROR panel. Three
   pressables: "HELP", "PRIVACY POLICY", "TERMS OF SERVICE". Rec Room's `(Dorm_Help_Buttons)` (key `0ed`) is only an
   invisible volume plus an unthemed canvas (rr_dorm_core §2.3), so build our own. The CoC poster stays at
   (-2.05, 1.45, -2.96). GUESS on spacing: move the CoC to (-2.20, 1.45, -2.96) if the panels overlap.
3. **Loft**: remove the loft `(Sandbox_CouchChair)` at (-4.2, 2.75, 1.3). Add `(SandboxMarkerSet)` key `1e4`
   (#-5730908251041691954) on the loft shelf or floor at (-4.3, 2.75, 0.2), yaw 90 (GUESS). Keep `(ArtCanvas)` at
   (-4.35, 2.75, -1.0), yaw 90.
4. **Dresser** (optional, GUESS): drop `(Dorm_Dresser)` at (-4.70, 0, -1.40). Put `(Sandbox_Houseplant_Dracaena)`
   there instead, and move the root beers, bell and book to the desk (bed-local x 0.2..0.9, y 0.85).
5. **Door option** (optional): Watch > Settings "Dorm door: Rec Center | Room-i-verse". With Room-i-verse the door
   sign reads "ROOM-I-VERSE" (Futura Bold Condensed, as the Rec Center sign) and using it opens the Watch Play page.
   Rec Room's own illustration for this is `Illustration_Menu_Play` (orange door with the RR logo, `sheet_icons.png`).
6. **Board**: unchanged position (0, 2.25, 2.90) yaw 180, 3.05 x 1.81 m face. Optional content change: header
   "PLAY" + a category strip ("Rec Room Originals" / Quests / Sports & PvP / Party / Racing) above the tiles.
7. **Audio**: optional one-time "welcome" VO the first time a player enters: `ExploreNUX_02` ("This is your dorm
   room...", Startup 063b6b66). Check that it loads (the Startup bundle has unloadable clips, rr_reccenter §2.6) -
   UNTESTED.
Interactions: unchanged (mirror, ladder, radio, hoop, board, door). Networking: unchanged (single player, private).

### 2.4 (d) Dorm PNGs (all `SP\research\img\places\`, also in `SP\harness\out\export\`)
| file | what it shows |
| --- | --- |
| `illustration_HomeButton_DormRoom.png` (155x117, atlas sactx-1 at 1024,385) | watch Home button: brown half-round window, ladder shelf (books, camera, trailing plant), bed with orange blanket and red pillow |
| `Illustration_Menu_Dorm.png` (202x192, sactx-1 at 618,1232) | the same motif, larger (menu icon) |
| `MenuIcon_Sprites_3_HR__Dorm_Icon.png`, `icon_AppNav_Dorm.png` | white/brown pictograms: loft bed over a desk; a bed |
| `DormRoom_Map_Recolor_Tex_rgb.png` (1024², the campus map) + `map_dorms.png` (3x crop) | the dorm as a cut-away: orange wood floor, purple rug, dark walls, skylight slats. The same map shows every campus building (section 3) |
| `SpawnableToolTex_DORM_*.png` (palette thumbnails, rendered by Rec Room) | bunk bed (purple blanket, white pillow, brown wood, drawer unit under the desk), community board (cork board in a wood frame), mirror/help button volumes, large window frame, skylight, light shafts |
| `DormRoom_BunkBed_Recolor_Tex_rgb.png`, `DormRoom_Rug_Recolor_Tex_rgb.png`, `Dorm_Ladder_Recolor_Tex_rgb.png`, `DormRoom_BulletinBoard_tex_rgb.png`, `DormRoom_Light_Fluorescent_Tex_rgb.png` | recolour masks (white/grey, purple block = bedding) and the cork-board texture |
| `CodeOfConduct_Tex_rgb.png` | the CoC poster (5 rules, signed "Coach") |
| `Shake_hands_poster_rgb.png`, `Stop_Gesture_poster_rgb.png` | the 2018 cartoon posters we use now ("SHAKE HANDS TO ADD FRIENDS!", "TALK TO THE HAND TO MUTE & IGNORE") |
| `Nux_Stop_Gesture2_rgb.png`, `Nux_Shake_hands2_rgb.png`, `Nux_Fist_Bump2_rgb.png`, `Nux_Open_Watch2_rgb.png` | the notebook-paper posters ("STOP GESTURE 1 look at player 2 show palm...", "SHAKE HANDS", "FIST BUMP", "WATCH MENU look at your wrist") = probable final social posters |
| `Nux_Menu_Default_Screen.png` | purple screen with the Rec Room logo (the default of the NUX controls board) |
| `ArtCanvas_Tex_rgb.png` | the canvas prop texture |
| `sheet_dorm.png`, `sheet_posters.png` | contact sheets of the above |

---

## 3. The Rec Center

### 3.1 (a) The final version

#### 3.1.1 Timeline (dated, sourced)
| date | change | source |
| --- | --- | --- |
| 30 Jun 2016 | hub = the **Locker Room** (lockers, ping-pong, darts from 20 Sep 2016, kiosk Nov 2016 - Mar 2017) | wiki Rec_Center, Kiosk, Timeline |
| 8 Jun 2017 | renamed **Rec Center**: "a big room with many doors" | wiki Rec_Center history; Timeline |
| 15 Feb 2018 | **Merch Booth** | Timeline |
| **28 Aug 2018** | "The Rec Center got a major upgrade! **New layout, fresh paint, some nice big windows**, etc." (gif i.imgur.com/I9ztiiE.gif). Doors become "multi-doors" (tag searches). You can buy **donuts**. rec.net room ^RecCenter RoomId **170126** created 2018-08-28T15:44:47Z | Steam gid 2444779293805621016; wiki "New Rec Center released ... to the current version"; rooms.rec.net JSON |
| 14 Sep 2018 | "The **screens upstairs** in the Rec Center now show a feed of images from rec.net" (slideshow): the 2018 room has an **upstairs** | Steam 2018-09-14 |
| Nov 2018 | a community board "in the ^RecCenter for social viewing" | wiki News Archive 2018 |
| **Jan 2019** (Social Dorm edition) | "an **information kiosk in the middle** of the Rec Center, to better break up the larger space"; "full **Ping-Pong Tables**, and **another Basketball Hoop**"; "moved around the **Merch Booth, Puzzles & Art, the Mirror, the Community Board and Dart Board**"; "Cheer & 'Go to Room' buttons added to Rec Center **projector room**" | wiki News Archive 2019 l.1874-1888 |
| Apr 2019 | lighting/rendering overhaul; "somebody closed the Rec Center window" | Steam 2019-04-23 |
| Sep-Oct 2019 | Rec Center moderators; kiosk counter collision fixed; spot behind the community board fixed | Steam 2019-09-10, 2019-10-15 |
| Nov 2019 | "Revised layout of merch booths in Rec Center"; "you should be able to make a basket in the Rec Center now" (screens) | Steam 2019-11-21 |
| Sep 2020 | Red Velvet and Double Chocolate cakes "in the **Rec Center Cafe**" | Steam 2020-09-09 |
| 8 Oct 2020 | room PublishedAt (re-publish) | rooms.rec.net |
| 2 Jun 2021 | "doors in ^RecCenter use our new **room search menu**" (a door opens a filtered room list) | Steam 2021-06-02 |
| 25 Jun 2021 | "Facilities has removed a few things from the Rec Center in order to make room for some changes" | Steam 2021-06-25 |
| 9 Jul 2021 | **Magic Door** "in both the Rec Center and the play menu" (random room) | Steam 2021-07-09 |
| 7 Sep 2021 | **Friend-O-Tron** vending machine (a free random gift once a day) | Timeline; wiki News Archive 2021 |
| Dec 2021 | "tooltips ... navigating the **menu in the cafe** in Rec Center"; winter "snowfall around the Rec Center" | wiki News Archive 2021/2022 |
| 2021-2022 | holiday variants: Spooky, Winter, Lunar New Year, Love, League of Heroes, Arts Festival, NBA 75 (skin stripped Jun 2022: "the actual Rec Center no longer being littered with decor") | wiki Rec_Center gallery; YouTube f1cZEiYYqlo (2022-06-23) |
| Feb 2022 | "See the **RecCenter storefront**" (RR+ Seasons) | Steam 2022-02-02 |
| Jan 2023 | "the sweet sounds of **coffee being made** in the Rec Center" (ambient sound); NFL collab items | Steam 2023-01-31, 2023-01-25 |
| Mar 2023 | "Rec Center Changes" (video chapter; content unknown) | YouTube 0Y5wNMr4ZFU |
| 2 Apr 2024 | "**Removed slideshow** from Rec Center, we're hoping to use that space for something different" | Steam 2024-04-02 |
| Oct 2024 | "This Is GONE From The Rec Center Now..." (content unknown) | YouTube -eudwkmbhFI (2024-10-14) |
| **4 Mar 2025** | **BecameRRStudioRoomAt 2025-03-04T19:41:39Z** (the Rec Center becomes a Rec Room Studio / Rooms-2 room); tags gain #rrstudio #gameai #beta | rooms.rec.net bulk JSON (Wayback 2026-05-27); rec.net/room/RecCenter captures |
| Apr - May 2025 | new subroom "RecCenter-0314" (Unity scene a75f7547-79eb-47c6-8986-6767abcb4f92) next to the old "Home" (scene cbad71af-0831-44d8-b8ef-69edafa841f6, made private by May) | rec.net captures 2025-04-01, 2025-05-31 |
| Apr 2025 | "Sci-Fi Rec Center" dressing + Merch Booth; weekly "**Rec Center Booth & Pop-Up Shops**" | wiki Weekly Rotation Archive 2025 |
| 26 Jul 2025 (observed) | public ^RecCenter = `LoadScene(MAKER_ROOM_2)` "ugcr2" + `AssetBundle_Download` 5.7 s + `STUDIO_ASSET_BUNDLE_PROCESSED`; 12 players | user's Player-prev.log l.2748-2812 |
| Aug 2025 | "Medieval Fest" dressing; subroom **"Home_082525"** (25 Aug 2025); room picture changed to 50xrbhogfdae0mcgzfkomlghu between 16 and 26 Aug; video "YOU Didn't Notice The NEW Rec Room Rec Center UPDATE" (2025-08-25) | wiki; rec.net captures; YouTube TmRdY84jaNw |
| Aug 2025 | gift packages spawn "when entering your dorm and/or the Rec Center" | Steam 2025-08-12 |
| Sep 2025 | Dorm door defaults to ^RecCenter again; Grow and Shrink potions "in the Supplies store or the Rec Center" | Steam 2025-09-23 |
| 22-30 Jan 2026 | Class of '87 Reunion: a **separate, temporary** "recreated Classic Rec Center" (modified ^LockerRoomArchive by @happyvr); one challenge was "Visit the ^RecCenter" (so the normal one still existed) | rec.net/creator/p/class-of-87-reunion (Wayback 20260323165645) |
| May 2026 (final) | subrooms: **"Home"** (public, scene a75f7547-..., IsSandbox true = Rooms 2) and **"Switch_Home"** (private, scene cbad71af-... = the 2018 Unity scene); MaxPlayers **12**; description "A social hub to meet and mingle with friends new and old."; tags hangout hub meetup social (+ beta nomp rrstudio gameai) | rec.net/room/RecCenter (Wayback 20260521043759) |

#### 3.1.2 What the final room was (best-supported reconstruction)
* **Building**: still the 2018 building. Evidence:
  - the same room id since 2018;
  - the campus map (updated late enough to show Run The Block, 2024) draws the Rec Center as a **long hall with a
    barrel-vaulted, orange-brown roof, cream walls, a band of clerestory windows under the eaves, a big arched gable
    end, and lower side wings with orange pitched roofs** (`map_reccenter.png`);
  - the watch pictogram `MenuIcon_Sprites_3(HR)_Rec_Center_Icon` shows the same: a vaulted centre and grid windows
    on both sides;
  - the 2025 Studio rebuild kept the name, the description and the old subroom as a fallback, which points to a
    port rather than a new design.
  The port **may have changed details**, and the Aug 2025 video says "NEW ... UPDATE". This is **unknown** and
  cannot be checked without the pictures or the cached bundle.
* **Layout** (wiki caption of the 2018-2021 photo taken "from the hallway to the Dorm Room"):
  - Merch Booth and the QUESTS door on the **right**;
  - community board on the **left**;
  - PvP door **behind the "shop" in the centre**;
  - "Everything else is to the left".
  Plus an **upstairs** with screens (the slideshow, removed Apr 2024) and a **projector room** with Cheer / Go to Room
  buttons.
* **Doors** (wiki, 2021-2024, after the Jun 2021 room-search change; using one "opens a list of relevant rooms"):
  - **QUESTS**;
  - **SPORTS & PVP**: Paintball, Rec Royale, Laser Tag, Bowling, Dodgeball, Disc Golf, Soccer, Paddleball;
  - **Creative Campus**;
  - **Featured Rooms**;
  - the **Magic Door** (random room, 2021).
* **Contents** (wiki + patch notes; "RecCenter_" asset names prove the furniture):
  - Merch Booth / storefront / pop-up shops;
  - the central info kiosk with a **reception bell** (`RecCenter_ReceptionBell_tex`);
  - the community board (`(CommunityBoard)_RecCenter` UI);
  - the **cafe**: coffee pot, mugs, plates, cafe shelf (`RecCenter_Cafe_*`), with donuts and cakes and a cafe menu;
  - lounge **RecCenter couches** (`(RecCenter_Couch_Sandbox)Variant`);
  - 2 basketball hoops;
  - ping-pong tables with paddles;
  - Puzzles & Art;
  - a **mirror**;
  - a dart board (2016-2019; final unknown);
  - Friend-O-Tron;
  - radios;
  - loose toys: "basketballs, frisbees, dodgeballs, a rolling cart, water bottles, buckets, a ping-pong table with
    paddles, a cafeteria".
* **Not in any source for the Rec Center**: a pool table (it was in ^Bowling, Steam 2020-02-03 "The Pool Table in
  ^bowling is now fully operational"), a poker/card table, a jukebox (the jukebox prefab is the Bowling alley's),
  lockers (2016 Locker Room era), arcade machines.
* **Look**: warm light wood floors, big windows with daylight. `CustomLoadingScreenDefault` (Rec Room's default
  loading picture) shows a warm interior: light wood floor, terracotta lower walls, tall dark-brown-framed multi-pane
  windows over cream sills, trees outside. It fits "nice big windows", but **which room it shows is a GUESS**.
* **Sounds**: radios; coffee-making ambience (2023); crowd chatter from players. No global music (**GUESS**).
* **Capacity**: 12 players per instance (rec.net 2024-2026).

#### 3.1.3 Confidence
| aspect | confidence | why |
| --- | --- | --- |
| history/dates | high | Steam, wiki, rec.net JSON agree |
| that the final room is a 2025 Studio/Rooms-2 rebuild | high | rec.net `BecameRRStudioRoomAt`, subroom scenes, the user's own 2025 log |
| content list | medium-high | wiki + patch notes (up to 2024), bundle names; the 2025 port may have dropped or added things |
| relative layout (right/left/centre/upstairs) | medium for 2018-2021, unknown for 2025 | one wiki caption |
| dimensions, heights, window positions, colours | **low** | only the campus-map exterior, pictograms and one loading picture |

#### 3.1.4 Pictures that would settle it (not downloaded; for the user)
* The final room picture (rec.net og:image since Aug 2025): `https://img.rec.net/50xrbhogfdae0mcgzfkomlghu.png`,
  archived at `http://web.archive.org/web/20260521043800/https://img.rec.net/50xrbhogfdae0mcgzfkomlghu.png`
  (3.4 MB).
* Earlier ones:
  - `bvovcusmx1uy6rw0fmts2og0w` (Mar-Apr 2025, right after the Studio conversion);
  - `8e6mg3hjl1r8ubnj01oq7sqkc` (May 2025);
  - `btz8w4z6x86s8kevi5enat06c` (mid Aug 2025);
  - `3qxr7uxzion3d4lqm234uehlt` (Nov 2024 - Jan 2025);
  - `78aup6c44dgf8whv7ifibzzgi` (2023-2024).
  All are on img.rec.net, archived by the Wayback Machine.
* Wiki:
  - `File:F8f5103dfb6048b2a5931cf07439a0f4.jpg` (2560x1440, the view from the Dorm hallway, uploaded 2021-05-09);
  - `File:Rec center quests door.jpg` (2024-05-17);
  - `File:MerchBooth.jpg` (2020-04-01);
  - `File:Communityboard.png`;
  - `File:ConceptArtRecCenter.jpg` (concept art, 2018-02, i.e. before the 2018 layout);
  - 2025 gallery `File:Sci-FiRecCenter3.jpg`, `File:RecCenterMedieval.jpg`.
* Videos (titles only were read):
  - "New Rec Center! August 28, 2018" (2tO5RqFuOgY);
  - "A tour of the 2018 Rec Center | Rec Room Preservation Project";
  - "YOU Didn't Notice The NEW Rec Room Rec Center UPDATE" (TmRdY84jaNw, 2025-08-25);
  - "The REC CENTER DOOR IS BACK!" (IVVNX0NlPAU, 2025-09-30);
  - "The OG Rec Center is back! How accurate is it?" (GOdngkVaRNU, 2026-01-24, about the reunion copy).

#### 3.1.5 The cached Studio bundle (not opened)
The 26 Jul 2025 log shows the final Rec Center's geometry arriving as a downloaded Studio asset bundle. The user's
`C:\Users\derii\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache` holds 418 MB in 2,022 files. The largest are
`35C.data` 84.6 MB (2025-08-07), `1AF.data` 60.1 MB (2025-07-26), `2BC.data` 22.4 MB (2025-07-26) and more. The Rec
Center visit was on 2025-07-26. Only names and sizes were listed. If the user allows it (question 1), we can check
whether one of these is a plain UnityFS bundle (the first bytes read "UnityFS") and, if so, index it with rrtool like
the install bundles. That would give the real 2025 Rec Center (the Home subroom version of July 2025, one step
before "Home_082525"). Two points for the user:
1. That data exists only on this machine, so it could only serve as a **reference for measurements**. It could not
   be a runtime source for other players (ARCHITECTURE rule 4 loads from each player's own install).
2. Rec Room's asset guidelines apply ("Guidelines - Using Assets Made by Rec Room", recroom.com).

### 3.2 (b) What differs from what was built (CODE/Places/RecCenter, README "Places/RecCenter")
| item | built | final (confidence) |
| --- | --- | --- |
| building | flat-ceiling 40 x 36 x 8 m hall with I-beams + 12 x 8 x 4 m lobby | long barrel-vaulted hall with clerestory windows, big arched gable windows, lower side wing(s), an upstairs balcony, a hallway to the Dorm (medium for the form, low for the numbers) |
| palette | blue wainscot + orange stripe, off-white walls | cream walls, warm wood, terracotta/orange accents, dark-brown window frames (low, GUESS) |
| doors | 21 single-activity doors (PvP/Sports north, Quests east, Party/Racing west), "COMING SOON" placards, group header signs | 4 category doors that open a filtered list: QUESTS, SPORTS & PVP, CREATIVE CAMPUS, FEATURED ROOMS; plus the Magic Door (high for 2021-2024) |
| arrival | lobby with the Dorm door, lockers, board | arrival from the Dorm hallway; community board on the left, Merch Booth on the right, kiosk/"shop" ahead (medium) |
| centre | ring kiosk with the bell and a radio | information kiosk "in the middle" (Jan 2019) with the reception bell (medium-high) |
| merch booth / storefront | none | Merch Booth / storefront / pop-up shops (high) |
| upstairs | none | an upstairs with screens (slideshow until Apr 2024) + a projector room (high that it existed; position GUESS) |
| basketball | one hoop over a painted court | two hoops (Jan 2019); court markings unknown |
| ping-pong | 2 tables | "full Ping-Pong Tables" (plural) - same |
| pool table + cue rack | yes | **not in the Rec Center** (Bowling) - remove |
| poker table + 6 chairs | yes | not in any source - remove (or keep as a GUESS extra) |
| jukebox | yes | not in any source (Bowling's jukebox) - replace with radios |
| lockers | yes | Locker Room era (2016) - remove |
| cafe | SE corner with a soffit, counter and pendants | the Rec Center Cafe with coffee, mugs, plates, donuts, cakes and a menu, plus the coffee-making ambience (high that it existed; position GUESS: to the left) |
| lounge | couches, rug, jukebox | RecCenter couches (high) |
| puzzles & art, mirror, dart board | none | Puzzles & Art and a Mirror (high, Jan 2019), dart board (2016-2019, final unknown) |
| Friend-O-Tron, vending | yes | yes (high); the vending machine's presence in the Rec Center is a GUESS |
| capacity | 1-40 | 12 per instance (high) |
| frame | kilometre instance origin | world origin (decision 1; handled by the origin task) |

### 3.3 (c) Rebuild plan: the final Rec Center (format of `rr_reccenter.md` §3)

#### 3.3.0 Overview (schematic, not to scale; north up; world origin O = (0,0,0) at the hall centre floor)
```
  z=+16  +------------------------------[ SPORTS & PVP (0,0,15.94) ]---------------------------+
         |  west wing (flat 4.5 m)    |  ping-pong (-7,11)          hoop B (8, 2.75, 15.9)       |
         | [CREATIVE CAMPUS (-18,7.94)]|  ping-pong (-7,5.5)     half court x 3..12, z 7..16      |
  z=+8   +---- Puzzles & Art ---------+                           hoop A (11.9, 2.75, 11.5)     |
         | whiteboards, canvases      ||                                          [QUESTS (11.94,0,4)]
         | [MAGIC DOOR (-23.94,0,2)]  ||        KIOSK ring (0,0,2)                 vending (11.45,0,0.4)
         |  LOUNGE couches (-18,-3)   ||   (arcade x=-12,        + bell, radio     Friend-O-Tron (11.45,0,-1.6)
         | [FEATURED (-23.94,0,-8)]   ||    openings z -12..4)                     MERCH BOOTH (10.4,0,-5.5)
         |  CAFE counter z -15.5..-14.5|                                           stair x 2.5..10.5 along z -10..-8.2
  z=-16  +----------------------------+--board(-11.94,1.9,-14)-- balcony z -16..-10 (y 4.0) above -----+
                                                   | hallway x -2.5..2.5 |
  z=-26                                            +---[ DORM (0,0,-25.94) ]---+
```

#### 3.3.1 Frame, rules
* Same as `rr_reccenter.md` §3.1, except that **O is the world origin** (decision 1: every room at the origin; only
  the current room exists). +X east, +Z north, +Y up, floor top y 0. The player arrives from the Dorm hallway in the
  south, facing north.
* Door rule, prefab rotation rule and ground-snap: as `rr_reccenter.md` §3.1 / §2.3 (door root 0.06 m in front of the
  wall's inner face; north wall yaw 180, east 270, west 90, south 0).
* Stairs: visible steps plus an invisible ramp collider (layer 23) with the same slope. The Somnium character
  controller climbs ramps more reliably than 0.2 m steps (**GUESS**; check with the Player module owner).
* Every number in 3.3.2-3.3.4 is **GUESS** unless it cites a source, because no measurement of the real room exists.
  The GUESS numbers follow the evidence: vault, clerestory, wing, upstairs, relative positions from the wiki
  caption, 12 players.

#### 3.3.2 Shell (procedural; wall thickness 0.3 m outside the inner faces)
| part | extent (inner faces) | height | materials (Rec Room, see `rr_reccenter.md` §2.7 for how to make them resident) |
| --- | --- | --- | --- |
| Main hall floor | x -12..12, z -16..16 (24 x 32 m) | top y 0 | `Wood_Floor_Matte_mat` (-541357418168945416, via key `84`), planks along Z |
| Main hall side walls | x = +-12 | 0..7.0 | 0-1.2 m wainscot: `Paint_White_Mat` instance tinted terracotta (0.80, 0.45, 0.30) GUESS; 1.2-7.0 m `Paint_White_Mat` tinted cream (0.93, 0.89, 0.80) GUESS; 0.15 m chair rail at 1.2 m in dark wood (0.30, 0.20, 0.14) |
| Barrel vault | segmental arch across X: springs at y 7.0 on x = +-12, apex y 10.5 at x 0 (radius 22.32 m, centre (0, -11.82) in the XY plane), extruded along Z -16..16; build 12 flat facets per side | 7.0..10.5 | underside `Wood_Horizontal_RedBrown_mat` (1df8e0ef key `fb5`) with UV along the arc; **ribs** every 4 m (z -16, -12 .. 16): 0.3 x 0.45 m arched beams in dark wood (0.30, 0.20, 0.14) |
| Gable walls | z = +-16, up to the vault line | 0..10.5 | as the side walls |
| North gable window ("nice big windows") | semicircle radius 5.0 centred (0, 5.0, 16), plus a mullioned rectangle x -5..5, y 3.2..5.0 | 3.2..10.0 | `Glass_Clear_mat` (via key `48`; or our own glass if the jukebox is dropped: see note), mullions 0.12 m dark wood, 5 radial + 1 horizontal |
| South gable window | semicircle radius 4.0 centred (0, 6.0, -16), above the balcony | 6.0..10.0 | same |
| Clerestory windows | both side walls, y 4.6..6.6, panes 1.8 m wide at a 2.0 m pitch from z -14 to 14 | | glass + dark-wood mullions; sill 0.25 m cream |
| Arcade to the west wing | 4 openings in the x = -12 wall: z -12..-8, -6..-2, 0..4, 6..8 (the last 2 m, to the Puzzles corner) with round-arch heads, y 0..3.6 (arch radius 2.0 from y 1.6); solid piers between; solid wall z -16..-12 (the community board) and z 8..16 | | piers cream, arch trim 0.2 m terracotta |
| West wing | x -24..-12, z -16..8 (12 x 24 m), flat ceiling y 4.5 | 0..4.5 | floor `Wood_Floor_Faded_Matte_Mat` (3519925957840248261); cafe part x -24..-12, z -16..-9 overlay `Tile_Linoleum_Brown_Mat` (via `(Sandbox_Floor)` key `3ea`); ceiling `Paint_White_Mat` + fluorescent panels `Light_Fluorescent_mat` (via `(Sandbox_Ceiling)` key `ce4`) at x -21, -15 and z -12, -6, 0, 6 |
| West wing windows | west wall x = -24: z -14..-10, -6..-2, 3..7 (the doors take z -9..-7 and 1..3); y 0.9..3.6, 4 panes each | | glass + dark-wood mullions ("nice big windows") |
| Upstairs balcony | deck x -12..12, z -16..-10, top y 4.0, 0.3 slab; railing 1.0 m (posts every 1.5 m, top rail 0.08 m, glass infill optional) on the z -10 edge from x -12 to 10.5 (the stair top lands at x 10.5..12) | deck at 4.0 | deck top `Wood_Floor_Matte_mat`; underside cream; railing dark wood |
| Stair | along the balcony front: x 2.5..10.5, z -10..-8.2 (1.8 m wide), rising eastward from y 0 at x 2.5 to y 4.0 at x 10.5 (20 steps of 0.2 x 0.4 m) + invisible ramp collider; landing x 10.5..12, z -10..-8.2 at y 4.0 joins the deck; railing on the open north side (z -8.2). It keeps the east wall free for the Merch Booth, the Friend-O-Tron and QUESTS | | wood treads, cream stringers |
| Dorm hallway | x -2.5..2.5, z -26..-16, ceiling y 3.5 (under the balcony); opening into the hall x -2.5..2.5, y 0..3.5 | 0..3.5 | walls as the hall (wainscot + cream), floor `Wood_Floor_Faded_Matte_Mat`, 2 fluorescent panels (0, 3.47, -23) and (0, 3.47, -19) |
| Exterior | ground 120 x 120 m at y -0.05, flat trees `(Sandbox_FlatTreeLarge)` `63e`, `(Sandbox_FlatTreeMedium)` `4ae`, `(Sandbox_FlatTreeSmall)` `e9d` 10-25 m outside the windows (autumn tint as in the campus map and the loading picture) | | seen only through the windows |
Colliders: floors, walls, vault (simple boxes approximating the facets), balcony deck, railing, stair ramp, counters,
window panes. Glass note: if `(BowlingAlley_Jukebox_Sandbox)Variant` is no longer loaded (the jukebox is dropped),
`Glass_Clear_mat` is not resident. Either keep loading key `48` only for its material, or use `RrLook` glass.

#### 3.3.3 Doors (6)
Build from the double `[ActivityDoor]` (clone from key `84`, `rr_reccenter.md` §2.1.2) with the sign text set. The
frame colour follows `rr_reccenter.md` §3.3 (recolourable frame). **Behaviour (Rec Room since Jun 2021):** using a
category door opens a **list** of the activities in that category (a world panel in front of the door or the Watch
Play page with a filter), and picking one starts the "GOING TO" sequence (`rr_reccenter.md` §4.2). A category with
one activity may travel directly.
| # | sign | wall | door root (x, y, z) | yaw | frame | opens (Dreamport activities; the mapping is an adaptation where marked) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | SPORTS & PVP | north, behind the kiosk (wiki: "PvP ... behind the shop in the center") | (0, 0, 15.94) | 180 | stock blue | Paintball, Laser Tag, Rec Royale, Showdown, Dodgeball, Paddleball, Soccer, Disc Golf, Bowling (the wiki list plus Showdown) |
| 2 | QUESTS | east, right-hand side (wiki) | (11.94, 0, 4) | 270 | rust orange | Golden Trophy, Crimson Cauldron, Jumbotron, Isle of Lost Skulls, Blood Moon |
| 3 | CREATIVE CAMPUS | west wing north wall | (-18, 0, 7.94) | 180 | purple | ADAPTATION: 3D Charades, Make It To Midnight, My Little Monsters, Punch Up (Rec Room's went to ^CreativeCampus, a UGC showcase) |
| 4 | FEATURED ROOMS | west wing west wall | (-23.94, 0, -8) | 90 | green | ADAPTATION: Stunt Runner, Rec Rally + a rotating "featured" pick (Rec Room's listed #featured UGC rooms) |
| 5 | Magic Door `(Sandbox_MagicDoor)` key `2a9` (#5412136012577350147), single door; our "DELIGHT ME!" behaviour | west wing west wall | (-23.94, 0, 2) | 90 | its own | a random activity; sound `MagicDoor_Refresh` (B43 AudioClip) when it rolls |
| 6 | DORM | hallway south end | (0, 0, -25.94) | 0 | warm grey | the Dorm (Dreamport keeps a physical door; Rec Room used the watch) |
Sign font: `FUTURA-T-OT-BOLD-CONDENSED SDF` (as now). No group header signs. Above the kiosk, a hanging double-sided
sign "REC CENTER" (1.6 x 0.4 m at y 5.5, GUESS).

#### 3.3.4 Furniture and props (floor contact points after ground-snap unless y is given; all positions GUESS)
**A. Information kiosk / "shop" in the centre** (Jan 2019)
* Ring counter as built: centre (0, 0, 2), outer r 2.4, inner r 1.7, height 1.05, opening 1.2 m facing north. Colours
  cream body + terracotta top band (was white + orange).
* `(Sandbox_Bell)` `45b` at (0, 1.05, -0.35) (south point, faces arrivals). `(Radio)` `f3` at (1.45, 1.05, 2.55),
  yaw 225.
* `(Sandbox_Houseplant_Fig)` `cb6` inside the ring at (0, 0, 2).

**B. Merch Booth** (right-hand side; storefront since 2022, "Booth & Pop-Up Shops" in 2025)
* Procedural booth against the east wall: back panel 4.0 x 2.6 x 0.1 at x 11.9, z -7.5..-3.5; counter 3.6 x 1.05 x 0.8
  centred (10.4, 0, -5.5), yaw 270 (serves west); header sign "MERCH BOOTH" (1.8 x 0.35 at y 2.9). Terracotta body,
  wood top.
* Display: 3 `(Sandbox_Showcase_Poster)` `72` on the back panel (y 1.9, z -6.7 / -5.5 / -4.3, yaw 270) with activity
  covers (RrCoverArt). Rec Room's own `(Sandbox_PopUpShop_1_Storefront)` key `40ad` (#-5217890641710351313) is too
  heavy (12 mannequins, 48 animators, 36 particle systems, server-driven UI) - do not spawn.
* Function in Dreamport: decorative (nothing is sold; the avatar is never modified). Optional: free consumables on
  the counter (`(Consumables_Donuts_OpenBox)` B85 key `37`).

**C. Community board** (left-hand side)
* `(BulletinBoard_Sandbox)Variant` `7ca` (-8423269555302806145) on the solid hall wall, centre (-11.94, 1.9, -14),
  yaw 90 (face +x); our notes UI as built. The `(CommunityBoard)_RecCenter` UI (6d3223da key `2d`) stays unused
  (dead rec.net feed).

**D. Friend-O-Tron and vending** (east wall between the Merch Booth and QUESTS)
* `(Sandbox_Friendotron)` `bbd` (4167279793516974501) at (11.45, 0, -1.6), yaw 270; reset its baked root position
  (rr_reccenter §2.3).
* `(VendingMachine)_SpawnableTool` `4ea` at (11.45, 0, 0.4), yaw 270 (GUESS that one stood in the final room; water
  bottles were among the loose toys).

**E. Basketball** (two hoops since Jan 2019; NE corner half court)
* `(Basketballhoop)` `78` A on the east wall, rim 2.75 m, at z 11.5 (root (11.9, 3.085, 11.5), yaw 270); B on the
  north wall at x 8 (root (8, 3.085, 15.9), yaw 180). Root height = rim + 0.335 (the rim centre sits at (0, -0.335, +0.371) from the pivot, `rr_dorm_core.md` §4.8);
  verify with bounds.
* Floor paint: none (unknown; the 2026 reunion copy turned "the basketball court" into a lounge, so the older room had
  a court area).
* 3 `(Basketball)` `d2` at (9, 0.13, 9), (9.4, 0.13, 9.3), (8.6, 0.13, 9.4).

**F. Ping-pong** (plural "full Ping-Pong Tables", Jan 2019)
* 2 `(Sandbox_Table_PingPong)` `7cd` at (-7, 0, 11) and (-7, 0, 5.5), long axis along X (root rotation rule), with 4
  `(Pingpong_Paddle)` `64` and 2 `(PingpongBall)` `568` on the tables (the RrRcPingPongBall hit logic as built).

**G. Cafe** (west wing, south part: "Everything else is to the left")
* Counter: 7.5 x 1.05 x 0.8 along the west-wing south wall, x -23.4..-15.9, z -15.6..-14.8 (cream body, wood top);
  back shelves 7.5 x 1.8 x 0.35 on the wall at y 1.1 (procedural, cafe-shelf style, **RecCenter_CafeAssets** colours:
  teal, cream, dark blue, wood - `RecCenter_CafeAssets_tex_rgb.png`).
* On the counter (y 1.05):
  - `(RecCenter_Cafe_CoffeePot)` `99` x2 at (-22.6, 1.05, -15.2) and (-21.8, 1.05, -15.2) (drinkable);
  - `(RecCenter_Cafe_Mug)` `e5b` x6 at x -21.0 .. -19.5 step 0.3, z -15.1;
  - `(RecCenter_Cafe_Plate)` `20b` x4 at x -18.9 .. -18.0, z -15.1;
  - `(Consumables_Donuts_OpenBox)` B85 key `37` (#-6234274183173749506) at (-17.4, 1.05, -15.2);
  - `(FoodConsumable_RedVelvetCake)` B85 key `5` (#-7901413202464288579) at (-16.7, 1.05, -15.2);
  - `(Consumables_Cake_Chocolate_Tray)` B85 key `03` (#86049582684404218) at (-16.2, 1.05, -15.2).
  (The Sep 2020 cafe cakes: "Red Velvet and Double Chocolate"; the Aug 2018 donuts.)
* `(Sandbox_Minifridge)` `c6a` at (-23.5, 0, -13.6), yaw 90. 4 `(Stool_Wood_Round_Sandbox)Variant` `58` in front of
  the counter at z -14.2, x -22, -20.5, -19, -17.5.
* Tables: 3 `(Table_Cafe_Short_Sandbox)Variant` `c48` at (-21, 0, -11), (-17.5, 0, -11), (-14.5, 0, -13), each with
  3 `(Chair_Cafe_Round_Sandbox)Variant` `43` at 0.95 m, facing the table (yaw toward the centre).
* 3 `(Light_Pendant_Sandbox)Variant` `3aa` hung from y 4.5 at x -21.5, -19.5, -17.5, z -14.6 (+ a point light each,
  range 3.5, warm (1, 0.8, 0.55), intensity 1.2).
* Menu board: a 2.0 x 1.0 m panel on the back wall at y 2.6, x -19.6, with the TMP text "REC CENTER CAFE - COFFEE -
  DONUTS - CAKE" (Rec Room's cafe menu UI is not in the bundles as a usable prefab - GUESS).
* Ambience: coffee-making loop `sfx_consumables_coffee_pour_loop_01` (B85 #-2262538946564174360) at the counter,
  volume 0.2, 3D 1..8 m, looped, stand-in for the Jan 2023 "sweet sounds of coffee being made" (GUESS clip).

**H. Lounge** (west wing, middle)
* `(RecCenter_Couch_Sandbox)Variant` `ed9` (0.91 x 1.08 x 2.62, long axis mesh Z; front direction to verify once):
  - couch 1 at (-18, 0, -5.4), long axis along X, facing north;
  - couch 2 at (-20.8, 0, -2.6), long axis along Z, facing east;
  - couch 3 at (-15.2, 0, -2.6), long axis along Z, facing west.
* `(Table_Coffee_Curvy_Sandbox)Variant` `536` at (-18, 0, -2.6).
* Rug 7 x 5 m `Carpet_LowPile_Striped_Blue_Mat` at (-18, 0.005, -2.6).
* 2 `(Chair_Beanbag_Sandbox)Variant` `ec1` at (-16, 0, 0.6) and (-20, 0, 0.6).
* Plants: `(Sandbox_Houseplant_Dracaena)` `ddd` at (-23.4, 0, -5.4) and `(Sandbox_Houseplant_SnakePlant)` `ce6` at
  (-12.6, 0, -5.2).
* `(Radio)` `f3` on the coffee table (radios: wiki).

**I. Puzzles & Art** (west wing, north part)
* 2 `(MoveableWhiteboard)` `fb` (#8372748118076330922) at (-22.6, 0, 5.6), yaw 90, and (-20.0, 0, 7.4), yaw 180.
* 2 `(ArtCanvas)` `d8c` at (-16.4, 0, 7.4) and (-14.6, 0, 7.4), yaw 180.
* `(Sandbox_Table_Short)` `c72` at (-19, 0, 5) with `(SandboxMarkerSet)` `1e4` and 2 `(DrawableClipboard)` `41e` on
  top.
* "Puzzles": no puzzle prefab in the bundles - skip.

**J. Mirror** (Jan 2019)
* Planar mirror 2.04 x 2.21 m (reuse `RrDormMirror`) on the hallway west wall, centre (-2.44, 1.35, -20.5), yaw 90.
  Optional: it costs a reflection render, so switch it off beyond 6.5 m as in the dorm (VR budget).

**K. Upstairs balcony** (the "screens upstairs" / projector room; slideshow removed Apr 2024)
* 2 `(RecCenter_Couch_Sandbox)Variant` `ed9` at (-5, 4.0, -12.2) and (3, 4.0, -12.2), long axis along X, facing
  north over the railing.
* 2 `(Sandbox_CouchChair)` `f0` at (-8.5, 4.0, -12.5) and (6.5, 4.0, -12.5), yaw 0.
* 2 `(Sandbox_Houseplant_Fig)` `cb6` at (-11.4, 4.0, -15.4) and (8.8, 4.0, -15.4).
* On the south gable wall under the fan window: 4 `(Sandbox_Showcase_Poster)` `72` at x -6, -2, 2, 6, y 5.2,
  z -15.94, yaw 0, showing activity covers. This is the "projector room" idea without the dead rec.net feed.

**L. Loose toys** (wiki list; shared, RrNetBody)
* 3 basketballs (E);
* 3 `(DiscGolfDisc)` `9d` on the kiosk counter;
* 3 `(DodgeballBall)` `1bf` at (4, 0.2, 10);
* 2 `(Bucket)` `55` at (-13, 0, 0) and (5, 0, -12);
* 2 `(SoloCup)` `4ba` at the cafe;
* 4 paddles + 2 ping-pong balls (F).
Total shared bodies: 3 + 3 + 3 + 2 + 6 = 17 (networking §5.5 budget 48).
* "A rolling cart": no cart prefab found - skip.

**Removed compared with the build:** pool table + cue rack + triangle, poker table + chairs, jukebox, lockers, the 16
"COMING SOON" placards and group header signs, the lobby.

#### 3.3.5 Lighting and audio
* Sun: directional, warm (1, 0.92, 0.80), intensity 1.1, from the south-west at 45 deg elevation, with shadows, so
  light enters through the fan windows, clerestories and the west-wing windows (the only sources of "big windows"
  light).
* Hall fill: 5 point lights along the vault apex at (0, 9.6, z) for z -12, -6, 0, 6, 12, range 14, intensity 0.8,
  warm white (1, 0.95, 0.88).
* West wing: 4 point lights at y 4.2 (x -21 / -15, z -6 / 3), range 8, intensity 0.7.
* Balcony: 2 at y 7.2 (x -5 / 5, z -13), range 8.
* Cafe pendants (G).
* Ambient: trilight sky (0.62, 0.66, 0.72) / equator (0.60, 0.55, 0.48) / ground (0.35, 0.30, 0.25).
* Keep the visible pixel-light count per object at or below URP's per-object limit (8). The numbers are GUESS; tune
  them against `SP\research\img\places\CustomLoadingScreenDefault_rgb.png`.
* Audio: no global music (GUESS). Radios on the kiosk and in the lounge (Rec Room's 41-track playlist, as built).
  Reception bell. Cafe coffee loop (G). An indoor ambience bed from `(AmbienceRadio)` `886`'s track list
  (`rr_reccenter.md` §2.6) at volume 0.15. Door hover and open sounds as `rr_reccenter.md` §4.5. Magic Door roll
  `MagicDoor_Refresh`.

#### 3.3.6 Interactions and networking
* Category doors open a list (3.3.3). This needs the list UI: a Watch/Rooms module request.
* Magic Door: random activity (existing DELIGHT ME!).
* Friend-O-Tron: as built (lists who is here). Vending: a drinkable water bottle.
* Bell, radios, hoops (RrHoop), ping-pong (RrRcPingPongBall), coffee pot / mugs / food: DrinkableBottle and food
  configs, `rr_dorm_core.md` §4.6-4.7.
* Whiteboards and canvases: decorative unless the Interact module has drawing (UNTESTED / not built).
* Instance size: 12 players (Rec Room's final MaxPlayers). Players elsewhere are hidden and muted (decision 2).

### 3.4 (d) Rec Center PNGs (all `SP\research\img\places\`)
| file | what it shows |
| --- | --- |
| `map_reccenter.png` (3x crop of `DormRoom_Map_Recolor_Tex_rgb.png` at 255,120 215x150) | **the best exterior evidence**: long hall with a **barrel-vaulted orange-brown roof**, cream walls, a clerestory window band under the eaves, an arched gable end, **lower wings with orange pitched roofs and doors**, trees |
| `DormRoom_Map_Recolor_Tex_rgb.png` | the whole campus map (Rec Center, Dorms, Welcome Center, Gymnasium, Paintball, Laser Tag Arena + Cyber Junk City, Theater Dept, Park, Disc Golf, TV Studio + Stunt Runner, Frontier Island, New Construction) |
| `illustration_HomeButton_RecCenter.png` (169x122, sactx-1 at 906,579) | watch Home button: a gabled building, orange roof, RR logo, arched door, two arched windows with flower boxes (a stylised icon) |
| `Illustration_Menu_RecCenter.png` (228x207, sactx-0 at 1401,1703), `Illustration_Menu_RecCenter-Outlined.png` | a civic building with a **glass dome and a flag**, orange-red roof band, RR logo, **4 arched entrances**, tan walls |
| `MenuIcon_Sprites_3_HR__Rec_Center_Icon.png` | white pictogram: a long building with a **vaulted centre roof** and grid windows on both sides |
| `icon_AppNav_RecCenter.png` | pictogram: a gable house with 3 arches |
| `CustomLoadingScreenDefault_rgb.png` (2048x1024, BC7) | 4 avatars in a warm interior: **light wood floor, terracotta walls, tall dark-framed multi-pane windows**, cream sills, autumn trees outside (the room is a GUESS) |
| `VendingMachine_FriendoTron_tex_rgb.png` | the Friend-O-Tron face: blue body, "FRIEND-O-TRON", a "GIFTS FOR FRIENDS" poster, 4 gift buttons, a pink "GIFT" slot |
| `GiftBox_FriendoTron_Tex_rgb.png` | its gift wrap (pink, stars, RR faces) |
| `VendingMachine_tex_rgb.png` | the "COLD DRINKS" machine (dark wood, water-bottle poster) |
| `RecCenter_CafeAssets_tex_rgb.png`, `RecCenter_ReceptionBell_tex_rgb.png` | flat colour atlases of the cafe props (teal, cream, dark blue, wood) and the bell |
| `Poster_Hydration_Tex_rgb.png`, `Poster_OpenMic_Signup_tex_rgb.png`, `Charades_Posters_Tex_rgb.png`, `Poster_rgb.png` | wall posters: "Coach says STAY HYDRATED!", "Open Mic Night! Sign up" + "Inventions for Sale!", 6 event posters (KO Kennedys, Jazz Night, Open Mic Stand-up, All Day Charades, Ice Cold Root Beer, Live DJ), a "Sign Up!" sheet |
| `Poster_ContentShowcase_Frame_Tex_rgb.png` | the showcase poster frame/cork |
| `Banner_Shop.png`, `Banner_RRPlus.png` | store banners (avatars) |
| `Illustration_Menu_Play.png` | an orange door with the RR logo window = the Play / Room-i-verse door |
| `Illustration_Exit.png`, `Illustration_Menu_Play_Alt.png` | a brown door with glass and an exit arrow; map + controller + basketball |
| `LoadingScreen_Tex_rgb.png` | a yellow mask texture (not a picture) |
| `sheet_icons.png`, `sheet_rc.png`, `sheet_loading.png` | contact sheets |

---

## 4. The export tool (reusable)
* `SP\unityharness\RRHarness\Assets\RrHarnessTests\RrExport.cs` (+ .meta, GUID d4b2f7b165fd4e058b139aa193d80ed0),
  static `RrHarness.RrExport.ExportArt()` (a coroutine), in the harness's own project only. What it does:
  - loads each name as a Sprite (class 213) or else as a Texture2D (class 28) through `RrAssets`;
  - blits it into an sRGB ARGB32 RenderTexture and reads it back with `ReadPixels`;
  - crops a Sprite to `textureRect`;
  - writes `name.png`, plus `name_rgb.png` with alpha forced to 1 for textures, to `SP\harness\out\export\`;
  - logs one line per file and "[RrHarness] export art done: N written, M missing/failed".
* Scenario `SP\harness\scenarios\export_art.json`. The list is `RrExport.DefaultNames` (50 names), or
  `SP\harness\scenarios\export_art.txt` if that file exists (one name per line, `#` comments).
* Run 1 (2026-09-24): exit 0, **50 written, 0 missing**, 8.5 s. Log in the session scratch `rplaces\run_export1.txt`.
  The contact sheets and crops in `SP\research\img\places\` were made afterwards with System.Drawing, not by the
  harness.

---

## 5. Bundle evidence: pieces of the final rooms that exist (plain bundles only)
"Proven" = the asset name ties it to the room (`RecCenter_`, `DormRoom_`, `Dorm_`), or a patch note or the wiki
places it there.

### 5.1 Dorm
| piece | bundle / key / pathId | status |
| --- | --- | --- |
| 47 `(Dorm_*)` architecture + furniture kit pieces | B43, see `rr_dorm_core.md` §2.2 | proven (the 2018 dorm = final default) |
| `(Dorm_FreeMirror)` | B43 `ded` #-6428987453455604450 | proven |
| `(Dorm_Mirror_Buttons)` / `(Dorm_Help_Buttons)` (invisible volumes + unthemed canvases) | B43 `27be` #-5360143280130445984 / `0ed` #-6358360931054609893 | proven; rebuild our own panels |
| `(Dorm_Community_Board)` board mesh | B43 `beb` #-5196424665139789200 | proven |
| **`(PlayCommunityBoard)`** (Play board UI, 2023+) | 6ef67129ea8ce0b105a2363c017f3653 `3` #7018542257515072498 (RRUI themed canvas: PlayMenuTopBar, Content) | proven by name; do not spawn (RRUI scripts); copy its layout |
| `(PlayHighlight_CategorySelect)CommunityBoardVariant` (categories page on the dorm board, Nov 2024) | 6ef67129... `0` #-4135738464828043627 | proven by name |
| `(CommunityBoard)_DormRoom` (pre-2023 board UI) | 6d3223da354de646ab79d5660dcac9d2 `eba` #3274340961757224498 | superseded in Jul 2023 |
| `(Dorm_Campus_Map)` + texture `DormRoom_Map_Recolor_Tex` | B43 `04e` #9185056585606946301; tex B43 #-3489455609074429231 | proven |
| social posters (probable final): `Nux_Stop_Gesture2`, `Nux_Shake_hands2` (+ `_PSVR`), `Nux_Fist_Bump2`, `Nux_Open_Watch2` | 6a69c4f283fb66b549d698cb46729dd5 `0` #-3241244070201691738; 3c03b87a9850a50a249a3c98b0355046 `a` #4656361576300778954; 1a733ad6537b27c985933abaa1d49562 `6` #6066368370217309043; a754c58d99395542f95acd90c29f74a0 `4` #42097276752004316 | standalone textures (no prefab); GUESS that they were the dorm posters |
| 2018 posters `(Sandbox_MuteGesture_Poster)` / `(Sandbox_ShakeHands_Poster)` | B43 `ef` / `b8f` | palette items (so NOT the dorm's own, per the wiki) |
| `(Sandbox_CodeOfConduct_Poster)` | B43 `760` #-9153174756256126895 | proven by the wiki list |
| `(SandboxMarkerSet)`, `(Marker)`, `(MoveableWhiteboard)` | B43 `1e4` #-5730908251041691954, `945` #6156072449279112575, `fb` #8372748118076330922 | "marker in the loft" (challenge) |
| `(ArtCanvas)`, `(Sandbox_Paintbrush)` | B43 `d8c`, `26` | proven by the wiki list |
| `(WaterBottle)` | B43 `538` #-6214342394501383043 | proven (challenge) |
| `(Sandbox_RoomDoorV2)` (Rec Room's room door) | B43 `09fb` #-5656950724883909510 | the dorm door prefab family (the sign text lives in the encrypted scene) |
| tutorial VO `ExploreNUX_01/02/03`, `AmyVO_CohortNux_01`, `CohortNUX_UseTheMirrorToCustomizeYourLook` (+ `_subtitles` TextAssets) | Startup 063b6b6653aa642616fd21d3d4701899 #4044404886910248, #-6904143099054111611, #6721287937923792997, #-3274596626915155, #-1202921961373180947 | proven; loadability UNTESTED |

### 5.2 Rec Center
| piece | bundle / key / pathId | status |
| --- | --- | --- |
| `(RecCenter_Couch_Sandbox)Variant` | B43 `ed9` #307189170882659907 | proven (name) |
| `(RecCenter_Cafe_CoffeePot)`, `(RecCenter_Cafe_Mug)`, `(RecCenter_Cafe_Plate)`, `[RecCenter_Cafe_Shelf]` (child object only, #-3945966611536406905) | B43 `99` #-6802294012789304814, `e5b` #7962773710793404200, `20b` #7902869567407032666 | proven (name); cafe sounds `RecCenter_CoffeePot_Pickup_01` #-2672837229593213268, `RecCenter_CoffeeCup_Pickup_01` #6083698800486502382, `RecCenter_CoffeeCup_HitEnvironment_01` #5721234865566839447 (all B43) |
| `(Sandbox_Bell)` with texture `RecCenter_ReceptionBell_tex` | B43 `45b` #-6493488885355874438; tex #-9169443665966815264 | proven (texture name) |
| `(CommunityBoard)_RecCenter` UI | 6d3223da... `2d` #-7816176551773483040 | proven (name); the feed is dead |
| `(BulletinBoard_Sandbox)Variant` board frame | B43 `7ca` #-8423269555302806145 | frame for the board |
| `(Sandbox_Friendotron)` + UI `(FriendotronMain)` + gift box `GiftBox_FriendoTron_Tex` | B43 `bbd` #4167279793516974501; ef9b849f `f4`; tex c16ad2d1 #-2079814009652656467 | proven (Sep 2021) |
| `(Sandbox_MagicDoor)` + `[MagicDoorManager]` + `MagicDoor_Refresh` clip | B43 `2a9` #5412136012577350147; Startup 063b6b66 (GameObject); B43 AudioClip | proven (Jul 2021) |
| `[ActivityDoor]` in `(Charades_InkSpace_MainHull_Sandbox)Variant` | B43 `84` #7410808331468609159 | the official double door (`rr_reccenter.md` §2.1) |
| `(Sandbox_PopUpShop_1..10_Storefront)`, `(Sandbox_DiscoveryPopupShop_Storefront)`, `(Sandbox_PopUpShop_ProgressionEvent_1_Storefront)` | B43 `40ad` #-5217890641710351313 (1), `548` #-4668185558294891347 (Discovery), ... | matches the 2025 "Rec Center Booth & Pop-Up Shops"; too heavy to spawn |
| `(VendingMachine)_SpawnableTool` | B43 `4ea` #-6827405341831640064 | vending (GUESS that it was in the hub) |
| `(Sandbox_Table_PingPong)`, `(Pingpong_Paddle)`, `(PingpongBall)` | B43 `7cd`, `64`, `568` | proven (Jan 2019 "full Ping-Pong Tables") |
| `(Basketballhoop)`, `(Basketball)` | B43 `78`, `d2` | proven (two hoops) |
| `(Sandbox_Mirror)` (AG/Mirror, unusable shader) | B43 `544` #-6592890460863086279 | "the dorm room & rec center mirrors" (Steam 2021-04-22): use our own planar mirror |
| `(Dart)` (no board mesh) | B43 `fe7` #3920024455142888564 | dart board 2016-2019 |
| cafe food: `(Consumables_Donuts_OpenBox)`, `(FoodConsumable_RedVelvetCake)`, `(Consumables_Cake_RedVelvet_Slice)`, `(Consumables_Cake_Chocolate_Tray)` | B85 `37` #-6234274183173749506, `5` #-7901413202464288579, `91` #6406478900910048256, `03` #86049582684404218 | "donuts" (2018), "Red Velvet and Double Chocolate" (2020) |
| coffee ambience stand-in `sfx_consumables_coffee_pour_loop_01` | B85 #-2262538946564174360 | GUESS clip for the 2023 coffee sound |
| loose toys `(DiscGolfDisc)`, `(DodgeballBall)`, `(Bucket)`, `(SoloCup)`, `(SoccerBall)` | B43 `9d` #6557311508961764802, `1bf` #-1619130950701524752, `55` #1791490104528552689, `4ba` #3935866521521447249, `00` #-3401908588561202286 | proven (wiki list; soccer ball not listed) |
| lounge/cafe furniture `(Table_Coffee_Curvy_Sandbox)Variant` `536`, `(Chair_Beanbag_Sandbox)Variant` `ec1`, `(Chair_Cafe_Round_Sandbox)Variant` `43`, `(Table_Cafe_Short_Sandbox)Variant` `c48`, `(Stool_Wood_Round_Sandbox)Variant` `58`, `(Sandbox_Minifridge)` `c6a`, `(Light_Pendant_Sandbox)Variant` `3aa`, houseplants `cb6`/`ddd`/`ce6` | B43 (pathIds in `rr_reccenter.md` §2.3) | generic palette items (plausible, not proven) |
| `(MoveableWhiteboard)` `fb`, `(ArtCanvas)` `d8c`, `(DrawableClipboard)` `41e` #-5469267969632177509, `(SandboxMarkerSet)` `1e4` | B43 | for "Puzzles & Art" (plausible) |
| **not found** | - | a puzzle prefab, a rolling cart, a dart board, arcade machines, the Rec Center's own shell (walls/vault/windows: they are in the encrypted 2018 scene and the 2025 CDN Studio bundle) |

---

## 6. Open questions for the user
1. **May we inspect the HTTPCache?** `C:\Users\derii\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache` probably
   holds the final Rec Center's Rec Room Studio bundle, cached when you visited on 26 Jul 2025. The first step would
   read only the file headers, to find a plain "UnityFS" bundle. It would be used as a measurement reference only,
   never loaded at runtime or shipped. It is not an encrypted install file, but it is outside the sources you have
   approved so far, so it was not touched.
2. **Screenshots.** Any of the following would turn the Rec Center plan from a GUESS into a measured rebuild:
   - your own Rec Room photos of the Rec Center (2024-2026) or the Dorm;
   - the archived room pictures in 3.1.4 (e.g. `http://web.archive.org/web/20260521043800/https://img.rec.net/50xrbhogfdae0mcgzfkomlghu.png`);
   - the wiki "View from the Dorm hallway" photo (`File:F8f5103dfb6048b2a5931cf07439a0f4.jpg`).
   Most useful views: from the Dorm hallway looking in, up at the ceiling/vault, the upstairs, the cafe, and the door
   signs.
3. **Dorm colours.** Do you remember or have a picture of the default dorm's wall colour? The data has no palette
   for the kit's walls. We use pale blue-grey (#A9BFD0).
4. **Doors.** Should the Rec Center follow Rec Room's final behaviour: 4 category doors (QUESTS, SPORTS & PVP,
   CREATIVE CAMPUS, FEATURED ROOMS) plus the Magic Door, each opening a list? Or keep one door per activity, which is
   easier to use but is the pre-2021 behaviour?
5. **Remove the non-Rec-Center extras?** These are the pool table (Rec Room's was in Bowling), the poker table, the
   jukebox and the lockers. They work and are fun, but the final Rec Center did not have them.
6. **Dorm details.** Swap the 2018 cartoon posters for the notebook "Stop Gesture / Shake Hands" posters, drop the
   dresser and the second armchair, add help buttons and a marker set: yes or no? Add the "Room-i-verse" door option?
7. **Seasonal dressing.** The final months had event dressings (Medieval Fest, Winter Wonderland 2025, the Class of
   '87 reunion). Do you want the plain room only (recommended), or one of those?

---

## 7. Notes for the builders (requests; not done here)
* **Places/RecCenter owner.** The rebuild in 3.3 replaces the shell, the doors and several props. Keep the working
  systems: bell, radios, hoop, ping-pong, Friend-O-Tron, vending and net props. The pool code (`RrRecCenterPool`,
  `RrRcCue`) becomes unused if the pool table goes (question 5).
* **Rooms/Watch owner.** Category doors need a "filtered list" UI: a world panel at the door, or the Watch Play page
  with a category filter. That matches Rec Room's Jun 2021 behaviour. `RrActivity` groups already exist (Social / PvP
  / Sports / Quests / Party / Racing); the door-to-group mapping is in 3.3.3.
* **Places/Dorm owner.** Deltas in 2.3 (posters, help panel, loft, optional dresser removal, optional Room-i-verse
  setting).
* **Networking.** The final Rec Center instance size was 12 players.
* **Correction for `rr_reccenter.md` §1.1** (its owner): "Sep 2020 Redesign: new layout, fresh paint, nice big
  windows" should read **28 Aug 2018**. Also add: "4 Mar 2025 converted to a Rec Room Studio room; final subroom Home
  (Rooms 2)".
* Second small correction for `rr_reccenter.md` §2.5: the picture with the "tan civic building ... dome with a flag"
  is `Illustration_Menu_RecCenter` (sactx-0). `illustration_HomeButton_RecCenter` (sactx-1) is a gabled building with
  an orange roof, an arched door and arched windows (both are in `SP\research\img\places\`).

Status: complete for this pass (2026-09-24). Untested:
* No code was changed, so no build of any new room exists.
* All 3.3 numbers are design GUESSES.
* The NUX poster attribution, the vending machine and dart board in the final hub, and the loadability of the Startup
  VO clips are unverified.
