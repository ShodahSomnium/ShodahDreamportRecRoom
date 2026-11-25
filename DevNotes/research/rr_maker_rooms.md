# Rec Room maker rooms (player-made rooms): research notes

Research for "Dreamport: Rec Room": how the final Rec Room (2024-2026, before the June 2026 shutdown) let a player
create a room from a base map, edit it with the Maker Pen, save it and let others join; plus what local data we have
for the base maps. Started 2026-10-14. Sources are given per fact; GUESS marks anything not confirmed.

## 0. Summary

- **Flow**: Watch > Create > "create room" > pick a base/template card (server-fed list in the final UI, see 4.2) >
  name it (^Name) > you are put in a private instance with the Maker Pen. Save / Restore / Settings / Permissions live
  on the watch's This Room page. Others find rooms in Play (Highlight, Search by name or #tag, Library > My Rooms),
  via profile/rec.net, Room Doors, Link Buttons, invites, party follow and Meetup codes.
- **Final template choice (11 Aug 2025 - shutdown)**: ^DefaultMakerRoom (R1, now with unlimited ink) or
  ^AdvancedMakerRoom (R2), plus template rooms (GUESS which ones were listed; the archive has Birthday, Catwalk, Cozy
  Store, Modern Store, Race Track, Wedding, Costumes, Rec Western, Ski Lodge, YouTube, Stunt, Platformer, Obby,
  MakerWorld). The picker is a server-fed list, so the final tile list is in no local file. The original base-room
  set (RoomIds 170144-170159): Lounge, Performance Hall, Maker Room, Park, 5 Paintball maps, Lake, Gym, Stadium,
  Hangar, CyberJunk City; later Bowling Alley, Stunt Runner base, The Ink Space. Most player rooms (75% in 2019; 89 of
  103 exported subrooms) were built on the blank Maker Room scene a75f7547.
- **Maker Room** = a 15 x 15 x 8.2 m box of 6 deletable install prefabs (`(Sandbox_Floor/Ceiling/Wall/Wall_Window)`),
  Android `Basement.unity` (level78) by strong GUESS.
- **Roles**: Creator (255), Co-Owner (30, GUESS), Contributor (invite-only since 2024; 20 GUESS), Host (10 GUESS),
  Everyone; Maker Pen by default
  for creator + co-owners; 2024 room-level rights Save / Publish / Clone / Restore Room, subroom management.
- **Saves**: per subroom, a history of saves (DataBlob = protobuf `PersistedRoomData`: one view per object with
  prefab GUID + transform + components; shape containers with ShapeData; circuits; roles; mood). Auto-backup slider
  (1-30 min) and Restore dropdown ([Latest]/[Backup]).
- **Maker Pen**: Object vs Edit mode; tools Create, Select (+Merge/Split/Copy...), Move, Rotate, Scale, Manipulate,
  Connect, Configure (physics mode Decoration/Environment/Physical/Sticky, respawn, tags, grabbable), Clone, Recolor,
  Freeze, Delete, Make Invention, Undo/Redo (10), Done Editing; Align/Snap/World-space toggles. Shapes Tube, Ribbon,
  Cube, Sphere, Cylinder, Wedge, Pyramid (+Cone, Hemisphere, Donut, Pipe, HalfPipe 2020; +21 in 2023); 60 colours,
  25 materials; ~256 props in 8 categories; ink limit until unlimited ink (R1, Aug 2025).
- **Local data**: Android levels for Park (75), Performance Hall (73), Basement/Maker Room (78), ugcr2 (108), UGC
  dorms (109/110), sandboxes (111/112), eventroom (12, Lounge?) are NOT converted, and the Android data pack is no
  longer on disk. Converted and usable: Dorm, Rec Center, Ink Space (Charades), Lake, Gym, Stadium, Bowling, Laser
  Tag, Paintball. The install has every Maker Pen asset (shapes, shader, menus, sprites, SFX) and 646 spawnable props.
- **Recommendation** (section 5): first Maker Room + Dorm + Ink Space; tools = shapes (5 primitives + tube), select,
  move/rotate/scale, clone, recolor, freeze, delete, undo, a curated prop page; save JSON in LocalStorage modelled on
  PersistedRoomData; owner-hosted instances stream the room to joiners.

## 1. "Create a room" flow (final Rec Room)

### 1.1 Entry point

- **Watch > Create** (5th main watch tab, icon `icon_Nav_Create`, see `rr_watch_ui.md`). The Create menu "shows your
  already-created rooms and a button to create a new room. The create a new room screen showed a list of room
  templates that you could select from, once selected you could name the room and start creating." [W-Watch]
- Since 2020 "The Maker Pen is now automatically equipped after creating a new room" [W-News2020].
- The new room is a **private instance** you are put into at once; it is a working copy until saved/published (see 1.5).
- Junior accounts cannot create custom rooms; juniors can use the Maker Pen only in their own Dorm [W-Custom, W-Pen].
- Other ways to get a room: **Copy** a room from its details page (Play > room > blue "i" > "Copy"), only if the
  creator allowed it (tag `#copyable`, `CloningAllowed` in RoomDetails). Copies of some RROs (Paintball maps) come with a
  Game Rules chip pre-configured like the original [W-Custom]. Rec Room also published copyable **template rooms**
  (section 1.2 c).
- 2024 experiment: **"My Dorms"** in the Create tab: several dorm rooms per player, swap them per season/event
  [W-News2024, ship notes 2024-08].

### 1.2 Base maps / templates offered

Three generations, newest last. The final list (Aug 2025 - shutdown) is (d).

a) **Built-in base rooms (Rooms 1, 2017-2024).** A "base room" = an official Unity scene with no built-in game rules
   that a custom room is built on top of ("the base rooms corresponding to #RecRoomOriginal rooms have no built-in game
   rules" [W-Custom]). Known ones:
   - **Maker Room**: the "blank slate"; "a simple, rectangular room"; its walls, ceiling and floor are deletable with
     the Maker Pen's Delete mode and cannot be brought back, so freeze the ones you keep [W-MakerRoom]. "To start with a
     completely empty room, create a new room based on the Maker Room and then delete everything" [W-Custom].
     "almost everyone uses the Maker Room as their base anyway" (Rec Room staff, 2019 Quest launch post) and rooms on
     the Maker Room + Rec Center + Dorm + Paintball + Charades + Dodgeball + Paddleball = 75% of all player rooms
     [W-News2019, reddit 2019-05-15].
   - **Performance Hall**: "a large, empty room with a stage" [W-PerfHall]. Only reachable as a custom-room base.
   - **The Ink Space**: the 2020 Charades cafe venue, "added ^TheInkSpace as a new base room you can clone and
     customize ... Find ^TheInkSpace on the Create page of your Watch Menu!" (ship notes 2020-09-08) [W-InkSpace].
   - **Park**: outdoor terrace, fountain, playfield, gazebo, basketball court, pond + waterfall + cave, campfire,
     open-air stage; "Good for sandboxes and new players to experiment with the maker pen" [W-Park].
   - **Lounge** (ping pong, darts, Charades stage, card table) [W-Lounge].
   - **The official base-room set** (fan archive room.json, creator 1, consecutive RoomIds 170144-170159 = created
     together): ^Lounge, ^PerformanceHall, **^DefaultMakerRoom** (the Maker Room), ^Park, ^River, ^Homestead, ^Quarry,
     ^Clearcut, ^Spillway, ^Lake (Disc Golf), ^Gym (Dodgeball), ^Stadium (Soccer), ^Hangar, ^CyberJunkCity (Laser Tag);
     later ^BowlingAlley (269686), ^StuntRunnerBaseRoom (2707036), ^TheInkSpace (9199030). Each is one subroom "Home"
     with `IsSandbox: true`, max 8-20 players, and (except Lounge/Park) **no save**: the base is the bare scene.
     Scene ids and Android levels: section 4.3. The Dorm and Rec Center were also used as bases (Dorm = every player's
     own room; Rec Center copies). Paddleball/Paintball copies came with their game-rule layer.
   - 2020: "Creators should be able to bake AI nav mesh properly in all base rooms. This includes rooms like
     Spillway, Cyberjunk City and many more!" (Ink Space patch notes) [W-News2020] -> game-map scenes were base rooms.
b) **Rooms 2.0 bases (Dec 2023 alpha -> 2025 default)**: an R2 room has its own base scene. In the Android/PC build:
   `ugcr2.unity` (activity "UCGR2", the blank R2 maker room, internal room type `MAKER_ROOM_2`), `ugcr2dormroom.unity`
   (R2 dorm), `Orientation_SandboxR2.unity`. The public Rec Center of 2025 loads as `LoadScene(MAKER_ROOM_2)` "ugcr2"
   plus a Studio asset-bundle download (user's Player-prev.log, see `rr_latest_places.md`), i.e. Rec Room's own late
   rooms are R2 blank scenes + Studio bundles.
c) **Template rooms** (copyable #community rooms by Rec Room staff, shown in Create): YouTube "Template Rooms"
   batches 1-4 by Rec Room Inc.: ^PVPTemplate, ^HouseTemplate, ^TreeClubHouse (batch 2), ^StageTemplate,
   ^TalkShowTemplate, ^TheaterTemplate (batch 3), ^RecWestern, ^EscapeRoomKit, ^RecHolidaySpecial (batch 4) [W-Custom];
   ^StuntTemplate (2019, Stunt Runner maps) [W-News2019]; ^PlatformerTemplate (Run The Block mechanics, Maker Pen
   editable) [W-Platformer]; ^1stRooms2_Template (copyable R2 demo with tutorials) [W-Rooms2]; July 2024 "a new
   kit-based Template Room available from the Create menu in-game" with the My Little Monsters assets (ship notes
   2024-07-29 "My Little Template") [W-News2024]. Template rooms present in the fan archive (section 4.3):
   ^BirthdayTemplate, ^CatwalkTemplate, ^CozyStoreTemplate, ^ModernStoreTemplate, ^RaceTrackTemplate,
   ^WeddingTemplate, ^CostumesTemplate, ^RecWestern, ^SkiLodgeTemplate, ^YoutubeTemplate, ^StuntTemplate (R1, Maker
   Room or Stunt Runner scene, by two creator accounts, GUESS Rec Room staff) and ^MakerWorldTemplate, ^ObbyTemplate,
   ^PlatformerTemplate (creator 1, R2/Studio scene 5d4e40d8).
d) **Final flow (11 Aug 2025 -> shutdown):** "After the update tonight, you'll see two options when creating a new
   room: ^DefaultMakerRoom (R1) / ^AdvancedMakerRoom (R2)". R1 became the default again (R2 had been pushed as the
   default during 2025: "Soon it will be the default for new room creation", The Messy Middle, Mar 2025), and **new R1
   rooms got unlimited ink**; existing R1 rooms can toggle unlimited ink in Room Settings (forum post "R1 is the new
   default room type + removing Maker AI", 2025-08-11) [W-News2025]. Maker AI (voice/text AI building, R2 + dorm,
   $1.99 day pass) was removed 2 Sep 2025.
   - So in the final game the template picker was: blank R1 maker room, blank R2 maker room, plus template/base rooms.
     The install's picker `(CreateBaseRooms)` is a server-fed list of up to 32 room cards (section 4.2), so the exact
     final tile list is not in any local file or source found. GUESS: the two maker rooms first, then the template
     rooms of (c) and the base rooms of (a).

### 1.3 Room settings: name, description, privacy, max players, tags, image

From the watch [W-Watch] and the official exporter's `RoomDetails.json` / `Subroom.json` (14 exports, local; field
values below are real):
- **Main tab**: name, description, banner image, **three tags** ("you can set up to 3 room tags" [W-Tags]).
- Names: shown with a `^` prefix (house symbol); `.`, `-`, `_` allowed in names since 2020 [W-News2020]. RoomDetails
  has `Name` and `FriendlyName`.
- **Accessibility** (room and per subroom): ints 0/1/2 in the export. paintballtroll1 room = 0 while its Lobby subroom
  = 2 and map subrooms = 1. GUESS from the API: 0 private, 1 public, 2 unlisted. (Archive rooms Calmjourney,
  2019_Rec_Center, coloredspeedball are 1 = public.)
- **MaxPlayers** per room and per subroom (seen 2..40: 2, 3, 4, 8, 12, 15, 20, 33, 40), `MaxPlayerCalculationMode`.
- **Tags**: `{Tag, Type, IsPrimaryGenre}`; Type 0 = creator tag (`pvp`, `hangout`, `4v4`), 1 = system/feature tag
  (`paintball`, `beta`, `limitsv2`), 2 = automatic (`community`, `screen`, `walkvr`, `teleportvr`, `junior`, `pickup`,
  `dormroom`, `rrstudio`). Automatic tags: #community (every custom room), #new, #featured, #copyable,
  #teleportvr/#walkvr/#screen and #pickup (all three) [W-Tags].
- Platform flags: `SupportsScreens`, `SupportsWalkVR`, `SupportsTeleportVR`, `SupportsVRLow`, `SupportsQuest2`,
  `SupportsMobile`, `SupportsJuniors`; `AgeRating`, `MinLevel`, `WarningMask`/`CustomWarning`.
- Other flags: `CloningAllowed`, `DisableMicAutoMute`, `DisableRoomComments`, `EncryptVoiceChat`, `IsDorm`,
  `SupportsLevelVoting`, `IsRRO`, `IsDeveloperOwned`, `ExcludeFromLists`; images `ImageName`, `PromoImages`,
  `LoadScreens`; `Stats` {CheerCount, FavoriteCount, VisitorCount, VisitCount}.
- **Subroom settings**: name, mood (environment lighting), capacity, "home"/entry flag [W-Watch]; in JSON
  `UnitySceneId` (the base scene), `MaxPlayers`, `Accessibility`, `IsSandbox`, `DefaultMatchmakingPolicy`,
  `ShouldAutoStageSaves`, `StagedSubRoomDataSaveId`, `CurrentSave`.
- **Meetup tab**: create a Meetup code/link to bring people into a private room [W-Watch].

### 1.4 Roles and permissions

- Room **permissions** (formerly "room roles"): **Creator, Co-Owner, Contributor, Host, Everyone, Defaults**
  (non-editable). Friends are assigned per permission with the people button; top-most permission wins [W-Watch].
  The export's `Roles` array uses ints: **255 = creator** (1 per room), **30 = co-owner** (paintballtroll1: "everyone
  can get co if u ask me" -> 39 x 30), **20** (2 in two rooms) and **10** (dorm, Calmjourney). GUESS: 20 = contributor
  (older name moderator), 10 = host, 0 = no role (122 entries, ex-role players). `InvitedRole` holds a pending invite:
  "Room contributors must now be added by invitation" (2024-09-16) [W-News2024].
- Per-permission settings (subroom level) [W-Watch]: Creative = Can View Gadgets?, **Can Use Maker Pen?**, Can Use
  Delete All Button?, Can Save Inventions?; Social = Vote Kick, Can Invite?, Can Talk?, Disable Mic Auto Mute?; Misc =
  Can Use Share Camera?, Can Print Photos?, Can Revive Self?, Auto Assigned Game Roles, Can Start Games?, Can End Games
  Early?, Can Change Game Mode?. By default **creators and co-owners** may use the Maker Pen / Palette [W-Palette].
- Room-level "This Room" permissions (added 2024): Edit Main Room Settings, Edit Room Econ Settings, Edit Room
  Progression, **Save Room, Publish Room, Clone Room, Restore Room**, Reset/Delete Cloud Variables, Edit Subroom
  Settings, Create/Name/Clone/Move/Delete Subrooms [W-News2024].
- Separate from permissions: **game roles** (Everyone, In-Game, Eliminated, Defaults) set by Role chips; they are
  stored in the save as GAME_ROLE_NODE objects (a fresh blank maker room holds exactly 3 of them + a Welcome Mat,
  `rr_room_exports.md` section 2).

### 1.5 Saving: Save room, auto-save, restore saves

- "Custom rooms may be saved with the This Room Menu of the Watch Menu such that they are persistent" [W-Custom].
  The room's state is a **save** (`SubRoomDataSave`: id, `DataBlob` = `*.room` file, hash, `PersistenceVersion` 255,
  `SavedByAccountId`, `SavedOnPlatform`, `SavedOnDeviceClass`, `Description`, `CreatedAt`, moderation state).
  Every subroom has its own save history; `CurrentSave` is the one that loads.
- **Restore Room**: pick an older save and make it current (a room-level permission since 2024) [W-News2024].
  UI: This Room > Restore (rotating arrow) > dropdown of saves labelled [Latest], [Backup] and timestamps > "Go".
  **Auto-backup**: Settings > Advanced > "Backup Frequency" slider, 1-30 min (happyfox KB 144, 2021).
- Auto-save / staging: `ShouldAutoStageSaves: true` + `StagedSubRoomDataSaveId` per subroom and
  `CurrentSnapshotId` / `LatestSnapshotId` / `StagedSnapshotId` / `CompatibleSnapshotId` at room level, plus
  `PublishStateAvailability {CanSaveAsBeta, CanSaveAsUpdate, AvailableUpdateTokenCount 3, NextAvailableUpdateDateTimeUtc}`
  (export JSON). GUESS: saves are staged and then published as a room **update** (limited update tokens) or as a
  **beta**; players in public instances see the published snapshot.
- 2024: "Monitor rooms for corruption and refuse to save them if corruption is detected" [W-News2024].
- Edit-ready (R2, Jan 2025): rooms load "edit-ready" for creators by default; subroom setting controls how others load
  them; "This Room" shows if the instance is edit-ready [W-News2025].

### 1.6 Subrooms

- "Rooms are composed entirely of Subrooms. A newly created Room ... is a single Subroom named 'Home'." One level
  deep; any number can be marked "Entry" (random entry if several). Subrooms were added by creating a second room and
  moving its sole subroom into the first; copying copies the entire room [W-Custom]. Since 2024 there are Create /
  Clone / Move / Delete Subroom permissions [W-News2024].
- Subroom settings (happyfox KB 55 "How to create Subrooms", via Wayback): subroom name (no spaces; full name
  `^Parent.Subroom`, e.g. `^Paintball.River`), Max Player Count, Subroom mood (sky box + lighting presets), Entry room
  (default entry when joining the parent), Move Subroom (to another room you own or co-own); only while inside it:
  Show gadgets to everyone (costs ink), Use directional voice chat. Managed at This Room > Settings > Subrooms (list
  ends with `.Home`; copy subrooms there).
- Each subroom has its own base scene (`UnitySceneId`), save, max players and accessibility (paintballtroll1: 8
  subrooms = Lobby + 7 Paintball maps, each on its official scene; the fan archive's 27- and 46-subroom rooms).
- Moving between subrooms: Room Door / Welcome Mat configured to a subroom; "Force New Instance (subroom only)"
  option on the 2020 Room Door [W-News2020].

### 1.7 Publishing, discovery and joining

- Publishing makes a room listed (tags, image, platform support set on the **Publish Room** page) [W-Tags]. Creators
  get prompted to post an announcement to subscribers on publish (2020) [W-News2020]. `PublishState`, `PublishedAt`.
- Finding rooms: Watch **Play** menu: Highlight (Top Twelve, Trending, Featured, Featured Creator, Rec Room
  Originals), **Search** (name or #tag), **Library** (Continue Playing, Saved for Later, Favorites, **My Rooms**);
  tapping a card opens Room Info (name, creator, capacity, tags, visits/cheers/favorites, description, photos; cheer,
  favorite, save for later, copy, owner options) [W-Watch]. Also rec.net room pages, the creator's profile portfolio,
  Link Buttons and Room Doors placed in other rooms (incl. your dorm) [W-Custom, W-News2020].
- Joining: public instance via Play; private instance via invite / party follow ("Party Auto-follow" default since
  2020) or a **Meetup code/link** [W-Watch, W-News2020]. Instances: public vs private, a room browser of instances
  (2019 "New Room Instance browser!") [W-News2019].
- Deleting a room: rec.net > room > gear > Delete [W-Custom].

## 2. Maker Pen build tools

### 2.1 Pen menu modes

Getting the pen: Watch > Backpack > Tools > Maker Pen "Use" (or favourite + over-the-shoulder grab); auto-equipped
after creating a room (2020). Only players whose permission has **Can Use Maker Pen?** can use it and its Palette
(default: creator + co-owners) [W-Pen, W-Palette]. Dropping it (or "Done Editing") ends editing; it despawns if
dropped over the back or >~3 m from the player [W-Pen].

Two states [W-Pen]: **Object mode** (tools act on whole objects: Maker Pen objects, props, gadgets) and **Edit mode**
(tools act on the shapes of ONE Maker Pen object; drawing a new shape starts Edit mode; "Done Editing" turns the shapes
into an object; only one object in Edit mode at a time; others cannot touch it while edited; "Fade world in edit
mode" greys out the rest).

Menu (VR: look at the top of the pen; screens: Shortcuts menu) [W-Pen]; icons exist in the install as
`MakerPen_Sprites_*` (bundle f124b399): Create, Select, Move, Rotate, Scale, Manipulate, Clone, ReColor, ColorPick,
Configure, Wire (Connect), Freeze, Delete, Edit, Done, Undo, Redo, Snap, Align, LocalSpace/worldaxis, GridSize,
Mirror, Marquee, Ungroup, Settings, Animation, and shape icons Tube, Ribbon, Cube, Sphere, Cylinder, Wedge, Pyramid.

| tool | what it does (wiki) |
| --- | --- |
| Palette (top-left button) | opens/closes the Palette (shapes, swatches, circuits, props, inventions, settings); grab its side handles to move it |
| Align / Snap / World Space (3 checkboxes) | Align: snap bounding-box faces (and optionally edges) to nearby cubes; Snap: position + rotation snapping per Palette settings; World Space: move/rotate/scale on room axes instead of the object's local axes |
| Create | spawn the selected shape / gadget / prop / invention; auto-active when picking something in the Palette; shapes are drawn by holding the trigger and moving (box from press point to release point) |
| Select | pick/unpick shapes or objects (drag = select sphere); double-tap clears; "Options" context menu: Merge Shapes, Split Shapes, Eyedropper, Copy (to clipboard), Center Pivot, Create/Unpack/Merge Circuit Board, Add State, Select Entire Invention |
| Move | drag freely, or tap for 3 axis handles; snap grid from Settings |
| Rotate | free, auto-axis, or tap for 4 rotation rings; angle snap |
| Scale | uniform scale (props too); tap = single axis for shapes |
| Manipulate | 6 face handles to stretch a shape; edit tube/ribbon points |
| Connect (was "Wire") | wire chip pins (CV1), attach objects to gizmo axles/bodies, state machine transitions; hover 2 s = pin tooltip |
| Configure (was "Settings") | opens Configure Object pages: shape density; object Physics Mode (decoration / environment / physical / sticky), Respawn + delay, Edit Tags, Is Grabbable, Is Climbable, Hidden during games, per-prop settings (breakable, beacon...) |
| Clone | duplicate shape/object (or the whole selection); drag handles for aligned clones |
| Recolor | apply the current swatch (colour + material) to a shape (Edit) or a whole object / recolourable prop (Object) |
| Freeze | lock an object: cannot be grabbed, moved, cloned, scaled, deleted (still configurable / connectable); toggle again or double-tap in Select |
| Delete | delete shape or object (props and gadgets included) |
| Make Invention | save the selection as a spawnable invention (name, description, thumbnail); blocked if "Can Save Inventions?" is off |
| Undo / Redo | almost every action (create, move, rotate, scale, clone, recolour, freeze, delete, manipulate); **10 steps**; config edits themselves are not undoable |
| Done Editing | leave Edit mode |
| Palette > Settings > **Delete Everything** | deletes all props and Maker Pen objects in the room (gated by "Can Use Delete All Button?") |

Object modes [W-Pen]: **Decoration** (no collision, no physics, grabbable), **Environment** (default; collides, not
simulated), **Physical** (mass from shape density x volume, falls), **Sticky** (physical + sticks to what it touches;
parent/child compositions). Modifiers: Frozen, Selected, Is Grabbable, Is Climbable, Hidden during games.

Later additions (ship notes): Circuit Boards (2019), self-scaling (2019), Copy/Paste clipboard (2024), Eyedropper in
R2 (Jul 2024), Rooms 2 hierarchy (containers, "Create Empty Parent", Connect Widget, Object Widget nameplates,
Quick Action Menu, Jan 2025), Replicator (R2, 2024) [W-News2024, W-News2025]. A **simplified Maker Pen** ("Expresso"
radial menu) shipped in 2025: the install has `(MakerPenExpressoMenu_Full)` / `_WidgetsOnly` and volume materials
`MakerPenExpresso_Volume_{Create, Select, Transform, CloneFlip, Recolor, Freeze, Delete, Configure, Connect}` (bundle
2c65be5c) = the 9 tools of the simple mode. Also a "MakerPenHUD" toolbar UI for screens (bundle eac2a6a5).

### 2.2 Shapes and materials

- **Shapes** [W-Palette]: Tube, Ribbon (drawn strokes; trigger pressure = width; facets, height ratio, smooth,
  connect-nearby, fixed width settings), Cube, Sphere (ellipsoid), Cylinder, Wedge, Pyramid; "Uniform shapes" setting
  forces cube/sphere/disc/square base. 2020 added **Cone, Hemisphere, Donut, Pipe, Half Pipe** ("most useful in
  decoration mode", inaccurate collision at first) [W-News2020]. March 2023 added **21 new shapes** (beta; "Default
  Shapes" vs "All Shapes" views; out of beta 2024) [W-News2023, W-News2024].
- Install meshes (bundle 1df8e0ef, with LOD1-3 and `_Collision` meshes): `ExtrusionShape_{Cube, Sphere, Cylinder,
  Pyramid, Wedge, Cone, Hemisphere, Donut, Pipe, HalfPipe, Diamond, Mound}` (+ `_CenterPivot` variants for sphere and
  hemisphere, `_Collision_Sharp` for pyramid and wedge). The other 2023 shapes were not found by name in the install
  (GUESS: server-side or named differently). Tubes/ribbons are generated meshes (`CurveShapeData` control points).
- Rendering: one shader `AG/MakerPenShape` (+ `-Batched`, bundle b1a0b840), material `AG-MakerPenShape` (bundle
  43ea1176); colour and material are per-shape data (`SandboxColorable`: colour index + material index; palettes
  are not in the bundles, see `rr_dorm_core.md`).
- **Colours**: 60 named colours with Color IDs (27 added in 2020) [W-Palette, W-News2020]; custom swatches
  (colour + material) can be saved; RGB colour exists in the save (`SandboxColorableData.rgbcolor`).
- **Materials** (25) [W-Palette]: Cardboard, Wood Grain, Painted Wall, Painted Brick, Carpet, Wood Floor, Dirt,
  Linoleum Tiles, Cloth, Shiny Metal, Sand, Glossy Plastic, Neon, Rubber Floor, Metal Floor Tread, Crashpad, Painted
  Wood, Grip Tape, Galvanized Metal, Particle Board, Checkered Linoleum, Sparkly Vinyl, Gravel, Grass, Rock. (Install
  apply-material SFX exist for cardboard, carpet, cloth, dirt, linoleum tiles, painted brick, painted wall, sand,
  shiny metal, wood floor, wood grain, neon, generic.)
- Palette **Settings** tab [W-Palette]: Don't rotate while moving, Enable self-scaling, Create grabbable objects,
  Position snap (cm), Rotation snap (deg), Uniform shapes, Snap to center, Align to edges, Show grid, Tube size, Fixed
  tube width, Connect nearby tubes, Smooth tubes, Tube height ratio, Tube facets, Fade world in edit mode, Drag select
  size, Show Pivot, Hide Gadgets For Me, Play Gizmos, Delete Everything.

### 2.3 Object catalog (palette)

- Palette tabs (wiki, older layout): Shapes, Swatches, Circuits (CV2 chips + "Circuit Components"), CV1 (Legacy)
  (Game/Math/Other chips, Other Components, Gizmos), **Props**, Inventions, Settings. Install carousels
  (`[ItemCarousel] *`, bundle bf65e263) confirm the final categories: **Creative, Decor, Dynamic, Furniture, Kitchen,
  Sports, Toys, Weapons**, plus All, Commonly Used, In This Room, Favorite, Clipboard, Inventions (Mine / Downloaded /
  Published / My Legacy / My Rooms 2), Gizmos, Circuit Components, Containers, chip groups (Logic, Math, Variables,
  Lists, Strings, Time, Events, Player, Physics, Networking, Economy, Control Flow, Conversions, Utilities...).
- Wiki prop list (last full edit before shutdown) per category: Creative 13, **Decor 106**, Dynamic 32, Furniture 21,
  Kitchen 20, Sports 16, Toys 12, Weapons 36 = **~256 props** with ink costs [W-Palette]. Examples: Decor = 50s Car,
  barrels, crashpads, fences, I-beams, pipes, plants, rocks (9), scaffolding, signs, standees, trees, trash cans, walls;
  Furniture = bookshelves, booth seat/table, chairs, couch, gaming tables, lockers, benches, picnic tables, stool,
  tables, umbrella; Kitchen = bottles, cups, glasses, mugs, coffee pot, plates, mini fridge; Sports = basketball,
  bowling ball/pin, CTF flag, dart, disc, dodgeball, paddle, ping pong set, pool table/cue/rack, target; Toys =
  microphones, glowsticks, meeple, radio, RC car, speaker; Weapons = paintball and laser tag guns, bow, crossbows,
  pirate weapons, wand, shields.
- Gadgets / Circuit Components (CV2): Animation Controller, Audio Player, Button, Beacon, Dice Set, Dome Light,
  Emitter, Explosion Emitter, Full Body Costume Dummy, Gun Handle, Interaction Volume, Invisible Collision, Laser
  Pointer, Motion Trail, Piston, **Point Light**, Projectile Launcher, Respawn Point, **Room Door**, Rotator, **Seat**,
  SFX, **Spotlight**, **Sun Direction**, **Text**, Text Screen, Toggle Button, Trigger Handle, Trigger Volume, Vector
  Component, **Welcome Mat** [W-Palette].
- Install (PC bundles, rebuilt rrtool index 2026-10-14): **646 prefab roots with `SpawnableTool`** (640 of them in
  bundle `43ea1176ca25c0333547813ec3298808`), each with a 16-byte `typeId` = the `prefab_id` stored in saves
  (`DevNotes/tools/roomexport/spawnable_typeids_from_bundles.tsv`); **603 palette thumbnails** `SpawnableToolTex_<ENUM>`
  (Sprite + Texture2D); 130 `(Sandbox_*)` roots, 47 `(Dorm_*)` kit pieces, 46 `(Quest_*)` props, 26 quest AI enemies,
  12 trophies, 11 Rec Royale items, 10 standees, 9 rocks, 9 Arena (laser tag) weapons; 349 prefabs are recolourable
  (`SandboxColorable`). The category of each prop is NOT in the bundles (server palette config).
- Base-room shells as palette props: `(Sandbox_Wall)`, `(Sandbox_Wall_Window)`, `(Sandbox_Floor)`, `(Sandbox_Ceiling)`,
  `(Sandbox_VerticalWall)`, `(Sandbox_HorizontalWall)` (bundle 43ea1176), `INK_SPACE_ROOM_HULL` +
  `INKSPACE_STAGE` (`(Charades_InkSpace_MainHull_Sandbox)Variant`), `STUNTRUNNER_BASEROOM_{FLOOR, CEILING, WALL_LONG,
  WALL_SHORT}`, the 47 `DORM_*` pieces. That is how "deletable" base rooms (Maker Room, Ink Space, Stunt Runner base,
  Dorm since 2019) were built: their architecture is ordinary spawnable objects.
- **Maker Room shell sizes** (install meshes): `Sandbox_Floor` AABB 15.0 x 0.4 x 15.0 m; `Sandbox_Wall` 15.0 wide x
  8.2 high x 0.4 m; `Sandbox_Wall_Window` same + windows (depth 1.34 m); `(Sandbox_Ceiling)` = the floor mesh at
  y 8.19 with 6 `[Light_Flourescent]` meshes, a "Room Fill" light and 4 "Ceiling Light" lights. => the blank Maker Room
  is a **15 x 15 x 8.2 m** box. All pieces are `KinematicRigidbodyPickup` + `PersistenceView` spawnables.

### 2.4 Ink limit

- Every object costs "ink"; the room has a limit shown as % on the pen's glue stick and the Palette bar [W-Pen].
  Ink is "our rough ballpark measurement of how much stuff you can put into a Room without the experience degrading
  too badly or crashing", a mix of CPU, GPU, memory and network cost; tuned for the weakest platforms (Quest, phones,
  PS4) ("All About Ink", VotaVader, blog.recroom.com 2020-12-15, via Wayback).
- Costs (wiki, % of the room limit): cube/tube-segment decoration 0.0034, environment 0.0052, physical 0.0147,
  sticky 0.0148; sphere 0.0017/0.0026/0.0074; cylinder, wedge, pyramid 0.017/0.026/0.074; props 0.32-6.3 (pool table
  5.65, marker set 6.33, art canvas 4.29); CV1 chip 0.05; gizmo 0.55. Each Maker Pen object costs much more than a
  shape inside it -> merge shapes; decoration costs about half of environment [W-Pen, W-Palette].
- CV2 circuits cost no ink; they use per-frame **Heat** / Network Heat (2020) [W-News2020, happyfox KB 135].
- `LimitsV2` room tag gave higher ink; on 2025-08-11 new R1 rooms got **unlimited ink** (toggle for old rooms in Room
  Settings) with planned performance warnings [W-News2025].
- AI bots have a separate AI budget (2020) [W-News2020].

### 2.5 What a saved room contains

Source: the official end-of-service exporter output in `D:\Dreamport\EXTERNAL FILES FOR REFERNCE\Rec\Rooms` (decoded in
`rr_room_exports.md`, descriptor strings read again for this note).
- A **room** = metadata (`RoomDetails.json`: name, description, image, tags, roles, accessibility, max players,
  platform flags, stats, snapshot/publish state) + N **subrooms**.
- A **subroom** = `UnitySceneId` (the base scene; never in the data) + its **current save** = one protobuf
  `rec_room.PersistedRoomData`:
  - `persistence_views[]`: one per placed object: `id` (GUID), `transform` (position, quaternion, uniform scale),
    `spawnable_tool_data.prefab_id` (= `SpawnableTool.typeId`), and optional component messages (~120 kinds):
    `sandbox_colorable_data` {color index, material index, rgbcolor, image}, `tagged_tool_data` (tags), physics mode,
    respawn, grabbable flags, text, door/welcome-mat targets, seat, light settings, game configuration, weapon
    settings...
  - Maker Pen objects are views with `shape_container_data` {`shape_collection`, `physics_mode`,
    `can_modify_with_circuits`, `optimize_colliders`}; each **ShapeData** = {`shape_type` (int), `shape_color`,
    `shape_material`, `current_local_position`, `current_local_rotation`, `scale`, `density`, `uv_offset`, `uv_scale`,
    `config_flags`, and either `mesh_shape_data` {legacy_scale, aspect_ratio} or `curve_shape_data`
    {control_points[] (position, normal, radius), flat_shaded, tube_height_scale, tube_ring_points, end cap style}}.
  - room-level: `circuit_data` (CV1), `circuit_v2_data`, `connectable_graph_data` (wires), `game_role_data`,
    `room_role_data`, `tool_tag_settings_data`, `scene_settings_data`, mood (`room_fog_data`, `room_mood_sun_data`,
    `room_mood_background_objects`), `navmesh_data`, `object_model_data` (R2 hierarchy), `last_save_time`,
    `activity_id`, `sub_room_id`, `version`.
- Scale of real saves: the 2018 Dorm reconstruction = 67 views incl. 856 shapes in 4 containers; paintball maps
  74-2162 shapes each; a fresh blank Maker Room save = 3 GAME_ROLE_NODEs + 1 WELCOME_MAT_V2 (`rr_room_exports.md`).
- R2/Studio rooms additionally reference Studio asset bundles (`UnityAssetId`, `ReferencedUnityAssetIds` in the save
  record; e.g. ^MakerWorldTemplate lists ~30 asset ids).
- Saving UI: This Room > Save; Settings > Advanced > **Backup Frequency** slider (auto-backup every 1-30 min); This
  Room > **Restore** (rotating-arrow icon) > dropdown of saves labelled [Latest] / [Backup] / older timestamps > "Go"
  (happyfox KB 144 "I lost progress while building with the Maker Pen", 2021, via Wayback).

## 3. Sources

Wiki pages were read as wikitext through the fandom API
(`https://rec-room.fandom.com/api.php?action=parse&page=<Page>&prop=wikitext&format=json`; the HTML site answers 402
to WebFetch). Copies of the wikitext are in this session's scratchpad (`scratchpad/wiki/*.txt`), not in the project.
- [W-Custom] Custom Rooms (incl. Subrooms section). [W-Rooms2] Rooms 2.0. [W-MakerRoom] Maker Room. [W-Park] Park.
  [W-Lounge] Lounge. [W-PerfHall] Performance Hall. [W-InkSpace] The Ink Space. [W-Platformer] Platformer Template.
  [W-Tags] Room Tags. [W-Watch] Watch Menu (Play, Create, Room Info, Room Settings, Permissions, Roles, Meetup).
  [W-Pen] Maker Pen. [W-Palette] Palette. Also Sandbox Machine, Gizmos, Clubs, Dorm Room, Inventions.
- [W-News2018 ... W-News2025] News Archive 2018-2025 (copies of Steam/ship notes/forum posts), incl. "Oculus Quest
  details" (reddit, 2019-05-15), "Ink Space edition" (2020-09-08), "Shape Up!" (2023-03-06), "My Little Template"
  (2024-07-29), "Upgraded Bean" (2024-09-16, contributors by invitation), the This Room permissions list (2024),
  "Road to Rooms 2" (2025-01-27), "The Messy Middle" (2025-03), "R1 is the new default room type + removing Maker AI"
  (forum.rec.net/t/10999, 2025-08-11), Company News Aug 2025. Latest News page (2026: data export, shutdown posts).
- Rec Room blog "All About Ink" (VotaVader, 2020-12-15), via
  `web.archive.org/web/2024/https://blog.recroom.com/posts/2020/12/14/all-about-ink` (curl; WebFetch cannot use the
  Wayback Machine).
- rec.net creator page "New Contributor Permissions!" (`rec.net/creator/p/contributor-permissions`, Wayback
  2026-04-12): "Contributors can help build out room content, but have more limited access to critical features
  around publishing, permissions, and room economics"; contributors get an invitation they must accept.
- Rec Room help center (HappyFox, Wayback 2025): KB 144 "I lost progress while building with the Maker Pen" (backup
  frequency + restore), KB 55 "How to create Subrooms", KB 135 "Resource Constraints: Heat, Network, and Ink Usage".
  The older Zendesk help center could not be listed (Wayback CDX was "Temporarily Offline" for it).
- YouTube (Rec Room Inc.) tutorials are referenced by the wiki ("How To Rec Room - Create a Room" X0VJklw2aew, "Room
  Setup and Settings" YKvgT6fgDw4, "Template Rooms Batch 1-4", "New Maker Pen Menu!" E_OHDVkBDpg, "Saving Maker Pen
  ink" cKF_XnBgxBY); their descriptions could not be fetched (YouTube page body not returned).
- Local: Rec Room's official exporter output (`EXTERNAL FILES FOR REFERNCE\Rec\Rooms`, 14 rooms: RoomDetails.json,
  Subroom.json, descriptor_set.binpb strings), `DevNotes/refdata/apk/INDEX.md` + `_levels.tsv`, the PC install
  (re-indexed with rrtool), `rr_room_exports.md`, `rr_dorm_core.md`, `rr_watch_ui.md`, `rr_latest_places.md`,
  `iter13/makerpen.md`, `somnium_conventions.md` section 18.
- Fan archive: `https://archive.splootybean.com/Rooms/` listing + 34 room.json files (light use, no bundles).

## 4. Local data

### 4.1 Android build levels (DevNotes/refdata/apk)

From `DevNotes/refdata/apk/INDEX.md` and `_levels.tsv` (Android build 2026-03-23, data only). "match" = share of
renderers whose mesh is an install mesh (the rest becomes stand-ins). Scene-id column: section 4.3.

| level | scene (path) | what it is | GOs | rend. | match | coll. | terrain | RRSK? |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| level78 | `Activities/Basement/Scene/Basement.unity` | **the R1 blank Maker Room** (GUESS, strong: roots Dynamic/TestMaterials/Systems/Static like ugcr2; exactly 6 SpawnableTools = 4 walls + floor + ceiling; 12 renderers = 6 shell meshes + 6 ceiling fluorescents, all install meshes) | 111 | 12 | 100% | 7 | no | **no** |
| level108 | `Activities/UCGR2/Scene/ugcr2.unity` | **R2 blank base** (`MAKER_ROOM_2`; the 2025 public Rec Center loads it + a Studio bundle) | 66 | 0 | - | 1 | no | no (nothing to draw) |
| level109 | `Activities/Dormroom/Scenes/ugcdormroom.unity` | R1 UGC Dorm base: lighting, outdoors, systems; the walls come from the `(Dorm_*)` spawnables in the save | 224 | 12 | 67% | 13 | no | no (but `Dorm_Builtin` covers it) |
| level110 | `Activities/Dormroom/Scenes/ugcr2dormroom.unity` | R2 Dorm base (same, R2) | 184 | 5 | 80% | 1 | no | no |
| level111 / 112 | `Orientation_Sandbox` / `Orientation_SandboxR2` | tutorial sandboxes (R1 / R2), a door + 5-6 meshes | 33 / 71 | 6 / 5 | 100% | 1 / 0 | no | no |
| level75 | `Packages/com.recroom.legacy/.../Park/Scenes/Park.unity` | **Park** base room (terrace, fountain, playfield, gazebo, court, pond, cave, campfire, stage; roots Water, Terrain, Lighting, SpawnPoints, Static Props, Dynamic Props, StreamPoints, TeleportBlockers; 4 SpawnableTools, 1 portal) | 4733 | 453 | **19.4%** | 1043 | **yes** | **no** |
| level73 | `Activities/PerformanceHall/PerformanceHall.unity` | **Performance Hall** (stage, doors, StageBlockers, [CharadesLighting]; 10 portals, 2 SpawnableTools) | 770 | 64 | 59.4% | 147 | no | **no** |
| level12 | `Activities/Eventroom/Scene/eventroom.unity` | "Event room": signs "Rec Center / Cards", 80 SpawnableTools incl. disc golf discs, balls, paddles. GUESS = the **Lounge** base room (ping pong, darts, card table, Charades stage) | 3338 | 283 | 50.2% | 716 | no | **no** |
| level106 | `the_ink_space.unity` | **The Ink Space** base room | 2423 | 335 | 100% | 882 | no | no; but `Charades_InkSpace.rrsk` (level105, 98.7%, 89 KB) is the same venue |
| level6 | `dormroom2.unity` | built-in Dorm (2019 stamp) | 786 | 47 | 80.9% | 66 | no | **yes** `Dorm_Builtin.rrsk` 11 KB |
| level7 | `reccenter.unity` | older built-in Rec Center | 7871 | 468 | 65.4% | 1387 | yes | **yes** `RecCenter_Builtin.rrsk` 198 KB |
| level9 | `discgolf.unity` | **Lake** (Disc Golf) | 6514 | 886 | 42.3% | 4564 | yes | yes `DiscGolf.rrsk` 472 KB |
| level11 | `dodgeball.unity` | **Gym** (Dodgeball) | 2954 | 124 | 46.0% | 88 | no | yes `Dodgeball.rrsk` 34 KB |
| level72 | `soccer.unity` | **Stadium** (Soccer) | 801 | 169 | 40.8% | 163 | no | yes `Soccer.rrsk` 110 KB |
| level76 / 77 | `Arena_Hangar3` / `Arena_Cyberjunk_City` | **Hangar** / **CyberJunk City** (Laser Tag) | 6848 / 12152 | 479 / 691 | 24-48% | 2452 / 5512 | no | yes |
| level91 | `BowlingAlley.unity` | **Bowling Alley** | 3215 | 165 | 57.6% | 652 | no | yes `Bowling.rrsk` 78 KB |
| level100 | `StuntRunner_Baseroom.unity` | Stunt Runner base room: systems only (walls are spawnables) | 539 | 0 | - | 1 | no | yes (empty) |
| level14-18, 103, 104, 113 | Paintball maps + lobby | River, Homestead, Quarry, Clearcut, Spillway, Drive-In, Merry Midway | - | - | 41-94% | - | most | yes (all, + `dressed/`) |
| level13 | `paddleball.unity` | Paddleball | 662 | 197 | 29.4% | 65 | no | yes |

- **Not converted** among the creation bases: Basement (Maker Room), ugcr2, ugcdormroom/ugcr2dormroom, Orientation
  sandboxes, **Park**, **Performance Hall**, eventroom (Lounge?), the_ink_space.
- **The Android data pack is gone**: `scratchpad/apk/UnityDataAssetPack.apk` no longer exists (only `apk/base/` is
  left) and no `.xapk` was found under D:\Downloads or the user folders, so `apkscene convert` cannot run until the user
  provides the XAPK again. Park/Performance Hall conversion needs it; the Maker Room does not (its 6 pieces are install
  prefabs, section 2.3).
- Park would be mostly stand-ins (19% install meshes) + a terrain heightmap; Performance Hall about 60% install art.

### 4.2 PC install index (objects.tsv)

- The index under the old session's scratchpad (`00e4b7e5.../scratchpad/rrtool/idx/`) is **empty now** and its
  `rrtool.dll` is gone. I rebuilt rrtool from `DevNotes/tools/rrtool` (Unity's Roslyn, as `apkscene/build.sh` does)
  into this session's scratchpad (`1c0947a6.../scratchpad/rrtoolbuild/bin/out/rrtool.dll`) and re-indexed the install
  (read-only, a few seconds): `rrtoolbuild/idx/objects.tsv` (28.5 MB) etc. Totals: 203,926 child GameObjects, 3,953
  prefab roots, 3,229 meshes, 2,300 materials, 3,784 sprites, 185 shaders.
- Maker Pen kit found (details in 2.1-2.3): pen `(MakerPen)` (4056f2fc), `(CharadesPen)`; menus `[MakerPenMenu]`,
  `[MakerPenPaletteMenu]` (4056f2fc), `(MakerPenMainScreen)`, `(MakerPenPalette*Screen)` x10 (16079ee0),
  `(MakerPen_Palette*)`, `(Makerpen_Palette_{Shapes, Swatches, Props, Objects, Inventions, Circuits_V1/V2, Gizmos,
  Clipboard, Favorite, Search})`, `(MakerPen_ConfigMenu)`, `(MakerPen_HierarchyMenu)`, `(MakerPen_Tools)` (bf65e263),
  "Expresso" simple menu (2c65be5c), HUD toolbar (eac2a6a5); gizmo visuals `[MakerPenTranslateAxis]`,
  `[MakerPenRotateAxis]`, `[MakerPenScaleAxis]`, `[MakerPenGridPlane]`, `[MakerPenHandleManagerPrefab]`,
  `MakerPenDragSelectVisuals`, `(MakerPenHoverEffectDecal)` (140b5ae8); shapes `ExtrusionShape_*` (1df8e0ef);
  shader `AG/MakerPenShape`, `AG/MakerPenGridLine`, `AG/MakerPenHandleShader`; containers `(StandaloneShapeContainer)`,
  `(OMCreationShapeContainer)` (R2) (43ea1176); 40+ pen skins; SFX (draw tube loop, draw primitive loops, delete x5,
  apply colour x4, apply material per material, place, pickup, magnetic snap).
- Room-creation UI: `(CreateBaseRooms)` and `(CreateBaseRooms)R2IsDefault` (3770c602): a searchable, server-fed room
  list (`RoomListModel` source 20, max 32 results, `CreationBaseRoomCardSpawnerImpl`) = the "pick a base room" page;
  `(CreateRoomDialog)` (0f3106ff): header image of the chosen base, title/body, a name input with "^" prefix text and
  emoji picker, a checkbox with a "Free Game AI Credit / Next refill" counter (2025 Game AI), Apply. Older UI:
  `[BaseRoomButton]` x15 (`AGUI.StackedUI.BaseRoomButton`: roomImage, roomNameText, button, "NotYetSupportedBanner";
  bundle 16079ee0), sprites `MenuIcon_Sprites_Create_New_Room`, `MenuIcon_Sprites_2_NewRoomFilter`. **No base-room
  names or thumbnails are in the bundles** (they come from the server: room images by `ImageName`).
- Other creation UI in 16079ee0: `(ThisRoomScreen)`, `(RoomInstanceBrowserScreen)`, `(ChooseSubroomDialog)`,
  `[RoomRoleRow]`, `[AddRemovePlayerRoomRoleButton]`, `(RoomBanPlayerScreen)`; permission UI
  `(RoomPermissionLevelMakerPenSettings)`, `(Switch) CreatorMakerPenOverride` (1a69c270).
- Props catalog: 646 `SpawnableTool` prefabs, 603 `SpawnableToolTex_*` thumbnails (2.3). Thumbnail list saved in
  `scratchpad/rrtoolbuild/stt.txt`, spawnable list in `scratchpad/rrtoolbuild/spawn.tsv` (scratch, not project files).

### 4.3 Fan archive (archive.splootybean.com) room.json

Read lightly: the /Rooms/ listing (130 folders) and 34 `room.json` files (each < 10 KB), 1 s apart; no bundles, no
images, no saves. Official rooms are creator 1 (Rec Room). RoomIds 170144-170159 are the original base-room set.

| room | RoomId | creator | UnitySceneId (subroom Home) | IsSandbox | max | save | Android level (GUESS where marked) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ^Lounge | 170144 | 1 | a067557f-ca32-43e6-b6e5-daaec60b4f5a | yes | 12 | yes | level12 eventroom (GUESS) |
| ^PerformanceHall | 170145 | 1 | 9932f88f-3929-43a0-a012-a40b5128e346 | yes | 12 | none | level73 |
| **^DefaultMakerRoom** | 170146 | 1 | **a75f7547-79eb-47c6-8986-6767abcb4f92** | yes | 12 | none | level78 Basement (GUESS, strong) |
| ^Park | 170147 | 1 | 0a864c86-5a71-4e18-8041-8124e4dc9d98 | yes | 20 | yes (public RRO hangout) | level75 |
| ^River / ^Homestead / ^Quarry / ^Clearcut / ^Spillway | 170149-170153 | 1 | e122fe98 / (Homestead a785267d) / ff4c6427 / 380d18b5 / 58763055 (export data) | yes | 12 | none | level14-18 |
| ^Lake | 170154 | 1 | f6f7256c-e438-4299-b99e-d20bef8cf7e0 | yes | 12 | none | level9 discgolf |
| ^Gym | 170156 | 1 | 3d474b26-26f7-45e9-9a36-9b02847d5e6f | yes | 12 | none | level11 dodgeball |
| ^Stadium | 170157 | 1 | 6d5eea4b-f069-4ed0-9916-0e2f07df0d03 | yes | 12 | none | level72 soccer |
| ^Hangar | 170158 | 1 | 239e676c-f12f-489f-bf3a-d4c383d692c3 | yes | 12 | none | level76 |
| ^CyberJunkCity | 170159 | 1 | 9d6456ce-6264-48b4-808d-2d96b3d91038 | yes | 12 | none | level77 |
| ^BowlingAlley | 269686 | 1 | ae929543-9a07-41d5-8ee9-dbbee8c36800 | yes | 8 | none | level91 |
| ^StuntRunnerBaseRoom | 2707036 | 1 | 882e9b96-7115-4b03-86f6-c0c9d8e22e00 | yes | 12 | none | level100 |
| ^TheInkSpace | 9199030 | 1 | 1fa06e3c-c307-4c11-a91b-1fabcddb8a96 | yes | 8 | none | level106 |
| ^Orientation | 21001727 | 1 | c79709d8-a31b-48aa-9eb8-cc31ba9505e8 | **no** | 1 | yes | level31-36 |
| ^MakerWorldTemplate / ^ObbyTemplate / ^PlatformerTemplate | 64-bit ids | 1 | **5d4e40d8-f289-4295-a6e1-4f907835007d** (+ Studio `UnityAssetId`) | yes | 4-12 | yes | level108 ugcr2 (GUESS: the R2/Studio base) |

- Base rooms with `CurrentSave: null` are the pure scene: creating a room from them starts from the scene alone. Base
  rooms are `Accessibility` 2 (GUESS unlisted) at room level; Lounge, Park, Orientation are 1 (public RROs).
- `IsSandbox: true` on every editable subroom (Orientation is the only `false`) -> GUESS: IsSandbox = "Maker Pen
  editing allowed" for the subroom.
- Copyable **template rooms** in the archive, all on the blank Maker Room scene a75f7547 with a save: ^BirthdayTemplate,
  ^CatwalkTemplate, ^CozyStoreTemplate, ^ModernStoreTemplate, ^RaceTrackTemplate, ^WeddingTemplate (creator 1586511),
  ^CostumesTemplate, ^RecWestern, ^SkiLodgeTemplate, ^YoutubeTemplate (creator 538365), ^StuntTemplate (538365, on
  the Stunt Runner base scene; the wiki calls it "Meriesa's ^StuntTemplate", so 538365 is GUESS Rec Room creator
  Meriesa). Player rooms: ^BasementPvp (a75f7547), ^TeamPark (11 subrooms, all a75f7547), etc.
- Matches the exporter data: 89 of 103 exported subrooms use a75f7547; JAPENESEASSETSRECROOM uses 5d4e40d8
  (`rr_room_exports.md` section 2).

### 4.4 Existing project work

- `Interact/MakerPen/` (iter13, `DevNotes/iter13/makerpen.md`): a **basic networked Maker Pen**: tube ink strokes,
  16-colour palette (GUESS colours), undo/erase/clear own strokes, per-author ownership, late-join resend, caps
  (240 points/stroke, 200 strokes + 8000 points per author), event ids 20-23, watch Home tile "Maker Pen". Strokes are
  not saved and vanish on leave. This is the natural base for the build tools.
- `Places/Dorm` (Dorm rebuilt from the 47 `(Dorm_*)` Maker Pen kit pieces, `rr_dorm_core.md`), `Places/RecCenter`
  (rebuild + the user's cached Studio Rec Center), Charades/Ink Space, Paintball, Bowling, quests: the travel system
  builds one room at the world origin (`Rooms/RrTravel`, `RrRoom`, `RrRegistry`).
- `Core/RrSceneKit` + RRSK files rebuild reference levels from install meshes (+ stand-ins) at run time.
- Networking (`Net/`): presence slots, per-instance elected authority, instance state, batched events, owner-simulated
  props (`somnium_networking.md`). Persistence: only `SomniumBridge.LocalStorage` (per player, per world, encrypted,
  under %TEMP%; `somnium_conventions.md` section 18). **There is no server-side storage**, so a saved room lives on the
  owner's PC and exists for others only while the owner (or someone holding a copy) is in the instance.

## 5. Recommended "basic form" scope

Constraint first: Somnium gives us **no server storage**, only per-player encrypted LocalStorage. So a maker room is
owned and stored by its creator; others see it while an instance is open with the owner (or a co-owner holding a
copy) present, and the room data travels over our instance network. Everything below fits that.

### 5.1 Templates, in order

1. **Maker Room (blank)** = Rec Room's ^DefaultMakerRoom. Build it from the 6 install prefabs (`(Sandbox_Floor)`,
   `(Sandbox_Ceiling)` with its fluorescents and lights, 3 x `(Sandbox_Wall)`, 1 x `(Sandbox_Wall_Window)`): a
   15 x 15 x 8.2 m box. The pieces are ordinary saved objects, so delete/freeze works on them exactly as in Rec Room.
   No Android data needed. GUESS: which side has the window wall and the exact wall offsets (the Basement level is
   not converted); use floor at y 0, walls at +-7.5 m (wall mesh pivot at its base). A fresh room also gets a Welcome
   Mat (spawn) as in Rec Room's default save.
2. **Dorm** ("My Dorm"): already rebuilt from the 47 `(Dorm_*)` Maker Pen pieces, which is exactly how Rec Room's own
   dorm was a Maker Pen room since Jan 2019. Make the existing dorm pieces saveable objects; every player gets one.
3. **The Ink Space**: `Charades_InkSpace.rrsk` (98.7% install art) exists; Rec Room offered it as a fully deletable
   base (and the hull is a spawnable `INK_SPACE_ROOM_HULL`). Cheap third template.
4. Second wave, from existing RRSK conversions, as "scene bases" (non-deletable scene + saved layer on top, like Rec
   Room's game-map bases): Gym (Dodgeball), Stadium (Soccer), Lake (Disc Golf), Bowling Alley, Hangar / CyberJunk City,
   the Paintball maps.
5. **Park** and **Performance Hall** need the Android XAPK again (data pack no longer on disk) and a conversion; Park
   is only 19% install meshes (mostly stand-ins + terrain). Ask the user for the XAPK before planning them. Lounge =
   eventroom is a GUESS; verify before offering it.
6. Skip for now: R2 features (hierarchies, Replicator, Studio bundles), Maker AI, copy-from-other-player rooms.

### 5.2 Create flow (watch)

Watch **Create** tab: "My Rooms" (from LocalStorage) + "Create Room" -> template cards (name + picture) -> dialog: name
(^ prefix, letters/digits/`.-_`), optional description, max players (default 12, Maker Room in RR was 12), privacy
(Private = invite/party only; Public = listed in Play > player rooms while open). Then build at the world origin
via the existing travel system and auto-give the Maker Pen (Rec Room did since 2020). This Room page: Save, Restore
(dropdown of saves [Latest]/[Backup]), Settings (name, description, 3 tags, max players), Permissions (co-owners).

### 5.3 Build tools (basic form)

Extend `Interact/MakerPen` (already networked) with Rec Room's mode set, but only these:
- **Create shapes**: Cube, Sphere, Cylinder, Wedge, Pyramid (install `ExtrusionShape_*` meshes + `AG/MakerPenShape`
  material, box drawn from trigger-down to trigger-up corner) plus the existing Tube. Later Cone, Hemisphere, Donut,
  Pipe, HalfPipe (meshes are there).
- **Edit / Done Editing** (shapes grouped into one object), **Select** (single + drag), **Move / Rotate** (grab with
  the free hand and pen-ray drag; Snap toggle with grid 0.1/0.25/0.5 m and 15/45 deg), **Scale** (uniform; shapes
  per-axis later), **Clone**, **Recolor** (colour + material swatch), **Freeze**, **Delete**, **Undo/Redo** (10 steps,
  Rec Room's number).
- **Configure** reduced to: physics mode (Decoration / Environment / Physical), Is Grabbable.
- **Props palette**: a curated first page from the 646 install spawnables in Rec Room's categories (Furniture, Decor,
  Sports, Toys, Kitchen, Creative) spawned through the existing Interact layer; thumbnails from `SpawnableToolTex_*`.
  Gadgets: Point Light (light grid aware), Text, Welcome Mat / spawn point; Room Door (to another saved room) later.
- **Limits** instead of ink: e.g. max objects and shapes per room (start around 300 objects / 3000 shapes / 200 props,
  tune in tests) and a bar on the palette; Rec Room's own ink costs (2.4) can weight them (decoration ~0.65x
  environment, physical ~3x).
- Permissions: creator + co-owners (Somnium player ids) edit; everyone else plays. Contributor/host later.

### 5.4 What the save must hold (JSON in Bridge LocalStorage, keep it close to Rec Room's PersistedRoomData)

- Room: `version`, `roomId` (GUID), `name`, `description`, `tags[<=3]`, `template` (maker / dorm / inkspace /
  scene id), `maxPlayers`, `privacy`, `ownerId`, `coOwners[]`, `createdAt`, `savedAt`, `saveNo`, optional mood (sky,
  sun, fog), spawn point.
- Objects (`views[]`, like `persistence_views`): `id` (GUID), `kind` (prop / shapes / gadget / base piece), `prefab`
  (install prefab root name + Rec Room `typeId` GUID from `spawnable_typeids_from_bundles.tsv`, so exported Rec Room
  saves could be imported later), `pos`, `rot` (quaternion), `scale` (uniform), `color` + `material` index,
  `frozen`, `physics`, `grabbable`, `tags`, per-kind data (text string, light colour/range, door target).
- Shape objects: `shapes[]` = {`type`, local `pos`, `rot`, `scale` (xyz), `color`, `material`, tube
  `points[]`+radius} (= Rec Room's ShapeData minus density/uv).
- Base pieces of the Maker Room/Dorm are saved like any object (so deleting a wall persists); scene-base templates
  save only what was added.
- Keep the last N saves (e.g. 10) + an auto-backup every 5 min (Rec Room: 1-30 min slider), two check-summed copies
  as ETS does; Restore picks one.
- Networking: the owner's client is the room's authority; it streams the save (compressed, chunked) to joiners and
  relays edit events (create / transform / delete / recolor / freeze, reusing the Maker Pen event pattern).
