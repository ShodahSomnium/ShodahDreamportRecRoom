export const meta = {
  name: 'rr-iteration-3',
  description: 'Rec Room Dreamport iteration 3: final Rec Center (measured from the user cache + archived picture), category doors, Dorm updates',
  phases: [
    { title: 'Build', detail: 'measure the cached Rec Center, category doors + Dorm, then the Rec Center rebuild' },
  ],
}

const PROJ = 'D:/Dreamport/Rec Room/Rec Room'
const CODE = PROJ + '/Assets/#User/SomniumRecRoom'
const DN = PROJ + '/DevNotes'
const SP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const SPB = '/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const NSP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad'
const CACHE = 'C:/Users/derii/AppData/LocalLow/Against Gravity/Rec Room/HTTPCache'

const COMMON = `
# Dreamport: Rec Room - iteration 3 (2026-09-24)
Rec Room rebuilt inside a Somnium Space world (Unity 6000.3 URP; desktop screen mode AND VR; Somnium avatars untouched).
Iteration 2 (one origin for every room, isolation, grab/watch fixes, a private whitelist) is uploaded and the user says
it all works. The user works in short iterations and tests in the world, so: build it well, check it compiles, take a
few harness screenshots to make sure it looks right, and stop - no long test suites.

This iteration: the FINAL (pre-shutdown) Rec Center and the Dorm's final small changes.

## Paths
- CODE = ${CODE} (asmdef SomniumRecRoom; README.md there is the handover). DN = ${DN} (design/ARCHITECTURE.md section 1 =
  hard rules; research/*.md - grep headings, read only the sections you need; phase1/*.md = notes from iteration 2).
- SP = ${SP} (bash ${SPB}): compilecheck/, harness/ (scenarios/, out/), rrtool/ (bundle tool: README at
  DN/tools/rrtool/README.md; index in SP/rrtool/idx), research/img/places/ (Rec Room pictures exported from the bundles).
- Key research: DN/research/rr_latest_places.md (sections 2 = Dorm, 3 = Rec Center, 5 = bundle evidence) and
  DN/research/rr_reccenter.md (door prefab details 2.1, loading 2.2, props 2.3).

## Rules
- The Unity project is open in the user's editor: edit files only, never run Unity on it (the harness uses a copy).
- ARCHITECTURE.md section 1 applies. Never modify the Somnium avatar. Nothing from Rec Room ships: it all loads from the
  player's install through RrAssets at runtime.
- Every room is at the world origin now (Rooms/RrPlacement.cs); only the current room exists (RrTravel).
- Edit ONLY the files your task owns. Compile: "bash ${SPB}/compilecheck/build.sh project --editor" -> 0 errors in all three
  builds and "all N Fusion/Somnium members are proven" (an UNPROVEN member may make the upload fail: don't use it).
  Errors in files you don't own = another agent mid-edit: wait 2 min and retry. New files: "bash ${SPB}/tools/metas.sh".
- Harness: "bash ${SPB}/harness/run.sh <scenario>" (Bash timeout 600000; runs are serialised by a lock). Shot steps take
  "frame": "room" (positions relative to the current room). LOOK at the PNGs. Unity's compiler rejects a lambda local
  that shadows an outer local (CS0136).
- House style: XML doc comments that explain why, with measured facts; RrLog.Info("Area", ...) once per key fact.
- Durability: create DN/iter3/<key>.md in your first minutes and update it after each milestone; if it exists at start,
  you were restarted - read it and continue.

## User decisions (final)
- Rec Center doors: the FINAL behaviour - category doors (SPORTS & PVP, QUESTS, CREATIVE CAMPUS, FEATURED ROOMS) plus the
  Magic Door, each opening a list of activities to pick from.
- Remove what the real Rec Center never had: the pool table (with its cues and balls), the poker table and chips, the
  jukebox, the lockers.
- Games follow Rec Room's final rules with player minimums low enough for small Somnium groups (not this iteration).
- The user's Rec Room download cache may be read for MEASUREMENTS ONLY, unencrypted files only; nothing from it is ever
  loaded in the world, copied into the project or shipped.

## Visual references (seen by the orchestrator; describe-only, no files)
1. The FINAL Rec Center, Rec Room's own room picture (rec.net, archived May 2026), looking in from the entrance:
   - A warm, bright hall. Light orange-brown wood plank floor.
   - The ceiling is cream/beige plaster with HEAVY DARK-BROWN TIMBER BEAMS, crossing timber-frame style. A LARGE
     RECTANGULAR SKYLIGHT with a grid of panes sits in the ceiling over the centre.
   - CENTRE: the information kiosk. A long, low BLUE counter with a light wood top stands on a big ROUND BROWN RUG.
     Behind it, two tall dark-wood posts hold a tall BLUE BACK PANEL with a small grid shelf of merch items. The posts
     carry a big 3D ORANGE "REC ROOM" LOGO sign (the logo with the little bucket/cup icon) at about 5-6 m. The kiosk is
     the shop/info booth.
   - LEFT: an UPSTAIRS BALCONY (mezzanine) with a wooden handrail on dark wood posts. Under it is a BLUE lower wall
     with an orange framed notice board (the community board). A STAIRCASE climbs to the balcony at the back left.
     Beyond it there is a corridor with ORANGE/terracotta walls, a doorway, and a big window onto green trees.
   - RIGHT: a CURVED cream wall topped by a blue band, with BIG MULTI-PANE WINDOWS (dark frames) showing greenery, and a
     couple of brown lounge armchairs by the window. Nearer, a RED BRICK wall section with a blue pillar and a
     marquee-style lit sign (bulbs, pink/yellow) high up, a low blue counter/desk with a small sign, and red curtains
     at the far right edge (a stage or theatre door).
   - Overall palette: cream walls, blue panels/bands, orange accents, dark-brown timber, red brick, warm daylight.
2. A Rec Center category door (wiki photo "Rec center quests door", 2024):
   - A SINGLE wooden door, yellow-orange with wood grain, with a small wired-glass window near the top and a vertical
     push handle. It sits in a thick TEAL frame on a RED BRICK wall.
   - Using it opens a floating ROOM LIST panel in front of the door, in the watch's style: a white panel with tabs along
     the top, a row of room cards (cover art + title), and the orange bottom navigation bar of Rec Room's menu.
`

const RESULT = {
  type: 'object',
  properties: {
    files_changed: { type: 'array', items: { type: 'string' } },
    summary: { type: 'string' },
    verification: { type: 'string' },
    pngs: { type: 'array', items: { type: 'string' } },
    untested: { type: 'array', items: { type: 'string' } },
    readme_section: { type: 'string' },
  },
  required: ['files_changed', 'summary', 'verification', 'pngs', 'untested', 'readme_section'],
}

const MEASURE = `
# Your task: "measure" - the real final Rec Center from the user's download cache (measurements only)
Key: measure. Notes: DN/iter3/measure.md. Deliverable: DN/research/rr_reccenter_cache.md (create it early; fill as you go).
Do not edit CODE. Time-box: about 40 minutes; hand over what you have.

The cache is ${CACHE} (2,022 files, 412 MB, written 26 Jul and 7 Aug 2025 when the user visited Rec Room). The 2025
Rec Center was a Rec Room Studio room whose geometry downloaded as an asset bundle: DN/research/rr_latest_places.md
section 3.1.5. Several cache files start with "UnityFS" (plain Unity bundles, the same format as the install's): e.g.
35C.data 84.6 MB (2025-08-07), 1AF.data 60.1 MB, 2F0.data 13.2 MB, 24A.data, 255.data, 186.data, 1E4.data, 9.data. Others
start with other bytes: read only their first bytes to classify them (PNG/JPEG/KTX/JSON/etc.). If a file looks encrypted
or obfuscated, leave it alone.
1. Classify the files (magic bytes, sizes, dates). Find which plain bundle(s) are the Rec Center (names inside the bundle:
   scene or prefab names, "RecCenter", "Home", room ids, Studio object names). The user's 2025 Rec Room log may help:
   C:/Users/derii/AppData/LocalLow/Against Gravity/Rec Room/Player-prev.log (26 Jul 2025) and Player.log (7 Aug 2025) -
   grep for RecCenter, subroom, bundle and cache lines.
2. Read them with rrtool: its install folder is hard-coded (Db.BundleDir in SP/rrtool/Program.cs; sources also in
   DN/tools/rrtool). Add an override (e.g. an environment variable RR_BUNDLE_DIR and accept *.data files), rebuild it into
   a separate output folder, and do not disturb the existing index in SP/rrtool/idx (write a separate index for the
   cache in ${NSP}/cacheidx).
3. Measure the room: the hierarchy with WORLD transforms of every renderer (mesh name, material name, bounds) and light,
   in the room's own frame. Then summarise:
   - the hall's size and shape (floor outline, wall heights, the vault or ceiling height, timber beam positions, the
     skylight);
   - the positions of the kiosk, the REC ROOM sign, the balcony and its height, the stairs, the windows, the brick wall,
     the marquee sign, the curtains, the doors (category doors and Magic Door) with their signs, the Dorm entrance,
     couches, cafe, hoops, ping-pong, Friend-O-Tron, board, mirror, radios;
   - material colours (base colour values from the materials' properties), and which meshes/materials/prefabs have the
     SAME NAME as something in the install's bundles (grep SP/rrtool/idx/objects.tsv) - those we can spawn from the
     install;
   - a list of every placed object with position/yaw/scale in a table the builder can use directly.
   Write the numbers in the room frame with the ENTRANCE (Dorm side) at -z looking +z, floor at y = 0; state the
   transform you applied.
4. If nothing usable is there (no plain Rec Center bundle, or its geometry is not readable), say so clearly early in the
   doc, so the builder falls back to the picture + rr_latest_places.md 3.3.
Measurements only: do not copy meshes/textures/files into the project or anywhere shipped; don't add runtime loaders.
Return: doc path in files_changed, summary (what was found, confidence), verification = how you measured, pngs = [],
untested, readme_section = a short paragraph for the README about what the measurements are based on.
`

const DOORS = `
# Your task: "doors" - Rec Center category doors with a room list, the Magic Door, and the Dorm's final changes
Key: doors. Notes: DN/iter3/doors.md.
You own: new CODE/Places/RecCenter/RrCategoryDoor.cs and new CODE/Places/RecCenter/RrRoomListPanel.cs (plus a test hooks
file if you need one), CODE/Places/Dorm/*, scenarios SP/harness/scenarios/doors_*.json and dorm_room.json. You may add
(additively, without changing behaviour) small public helpers to CODE/Watch/RrUiKit.cs, RrWatchCard.cs or RrWatchArt.cs
to reuse the watch's look. Another agent ("reccenter") rebuilds the Rec Center and will PLACE your doors through your API,
so publish the API in DN/iter3/doors.md within your first 15 minutes, exactly as below (you may add members):

    namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
    public enum RrDoorCategory { SportsPvp, Quests, CreativeCampus, FeaturedRooms }
    public static class RrCategoryDoor {
      // A category door on a wall: door leaf + frame + sign; using it opens the room list in front of it.
      // localPos = floor centre of the doorway, 0.06 m in front of the wall; the door's +Z faces into the room.
      public static GameObject Create(Transform parent, Vector3 localPos, float yaw, RrDoorCategory category);
      // The Magic Door (Sandbox_MagicDoor, key 2a9): picks a random built activity and travels there.
      public static GameObject CreateMagicDoor(Transform parent, Vector3 localPos, float yaw);
      public static IReadOnlyList<Rooms.RrActivity> Activities(RrDoorCategory category);
      public static string Sign(RrDoorCategory category);  // "SPORTS & PVP", "QUESTS", "CREATIVE CAMPUS", "FEATURED ROOMS"
    }

1. The door: Rec Room's single room door (reference 2 above; rr_reccenter.md 2.1 lists (Sandbox_RoomDoor) key 2ac and
   (Sandbox_MagicDoor) key 2a9; check with rrtool which prefab matches a yellow wooden door with a small window and
   push handle), with a teal frame and a sign above in Futura Bold Condensed. Hover/approach feedback like Rec Room's
   (door glow or ajar), Rec Room door sounds from the bundles. Use: VR - poke or trigger-ray the door; desktop - aim and
   click (see how CODE/Rooms/RrPortal.cs and Interact/RrPressable.cs do presses). Magic Door: plays MagicDoor_Refresh when
   it rolls and travels to a random activity that is built (RrRooms registry).
2. The list panel (RrRoomListPanel): a world-space panel that opens in front of the door, in the watch's style (reuse
   Watch/RrUiKit, RrWatchCard, RrWatchArt, RrWatchSounds). It shows a title (the category) and a card per activity with
   its cover art, title, player count ("N here", from RrWatchWorld) and "COMING SOON" (not pickable) for activities not
   built yet. Picking a card starts RrTravel.GoTo(activity). Close button, closes on walking away (> 4 m) or on travel.
   Input: VR ray/poke and desktop aim+click, the same way the watch menu takes input (study CODE/Watch/RrWatchUi.cs).
   Mapping (rr_latest_places.md 3.3.3; the last two are adaptations): SPORTS & PVP = Paintball, Laser Tag, Rec Royale,
   Showdown, Dodgeball, Paddleball, Soccer, Disc Golf, Bowling; QUESTS = Golden Trophy, Crimson Cauldron, Jumbotron, Isle
   of Lost Skulls, Crescendo of the Blood Moon; CREATIVE CAMPUS = 3D Charades, Make It To Midnight, My Little Monsters,
   Punch Up; FEATURED ROOMS = Stunt Runner, Rec Rally, plus the built activities that appear first on the Dorm's Play board.
3. The Dorm's final changes (rr_latest_places.md 2.2-2.3 and 5.1): the notebook-style social posters ("Stop Gesture" /
   "Shake Hands") if the bundles have them, help buttons (Help / Privacy Policy / Terms: decorative, or show a short note),
   one armchair (not two), no dresser, a marker set by the loft canvas, and the Play board as the final "Play" board.
   Keep everything else in the Dorm as built.
4. Verify: compile; one harness scenario doors_test.json that spawns the four category doors and the Magic Door on the
   Arrival floor (use a test hook), opens a list, and takes shots (door close-up, the open panel); and dorm_room.json
   (existing, update shots if needed). At most 3 harness runs.
Return per the schema: files, summary, verification, pngs (paths of the best 3-4 shots), untested, readme_section.
`

const RECCENTER = (m) => `
# Your task: "reccenter" - rebuild the Rec Center as Rec Room's FINAL version
Key: reccenter. Notes: DN/iter3/reccenter.md.
You own: CODE/Places/RecCenter/* EXCEPT RrCategoryDoor.cs and RrRoomListPanel.cs (the "doors" agent owns those; place
doors only through its API - see DN/iter3/doors.md; if the files do not exist yet, write the rest first and come back),
and SP/harness/scenarios/reccenter_tour.json.

Today's Rec Center (README section "Places/RecCenter", code in Places/RecCenter) is our own invented layout: a flat 40 x
36 x 8 m hall with I-beams, a lobby, 21 double doors, pool/poker/jukebox/lockers. Replace it with the final room as seen
in visual reference 1 above, measured where possible from the user's cache. The "measure" agent's result:
${m ? JSON.stringify(m).slice(0, 7000) : '(the measure agent returned nothing - check DN/research/rr_reccenter_cache.md; if it is missing or says nothing usable, build from the picture and rr_latest_places.md 3.3)'}
Read DN/research/rr_reccenter_cache.md (the measurements) and rr_latest_places.md section 3 (3.3 rebuild plan, 5.2 bundle
evidence).

1. The shell (RrRecCenterShell / RrRcGeo / RrRecCenterSurfaces): the hall's shape, the timber-beam ceiling with the
   skylight, the upstairs balcony with its railing and working stairs (walkable colliders on layer 23), the curved window
   wall with big multi-pane windows, the brick wall section, the corridor to the Dorm entrance at -z. Use the measured
   dimensions when available, otherwise sizes consistent with the picture (a Rec Room hall that 12 players share
   comfortably). Rec Room's own materials from the bundles (wood floor, plaster, brick, timber, glass - see
   RrRecCenterSurfaces and rr_reccenter.md 2.7) and RrLook colours matching the palette. Keep metre UVs so tiling is
   continuous. Daylight feel: warm lights, bright windows (the outdoors view can reuse the Dorm's RrDormOutdoors idea).
2. The kiosk: a long low blue counter with a wood top on a round brown rug, the tall blue back panel with the merch grid
   shelf, two dark-wood posts, and the big 3D orange REC ROOM logo sign (if the bundles have the Rec Room logo as a mesh
   or sprite use it; otherwise build extruded letters with TMP 3D-ish layering or boxes). The reception bell stays on the
   counter (it works today).
3. Doors through the doors agent's API: SPORTS & PVP, QUESTS, CREATIVE CAMPUS, FEATURED ROOMS, the Magic Door, and the
   Dorm entrance (keep the existing RrPortal back to the Dorm). Put them where the measurements say, else as in
   rr_latest_places.md 3.3.3 adapted to the new shell.
4. Contents to keep or add: couches and lounge chairs by the big windows, the cafe (counter, coffee pot, mugs, plates,
   donuts and cakes if the bundles have them), 2 basketball hoops with balls, ping-pong tables with paddles and balls, the
   community board, the Friend-O-Tron and vending machine, radios, the mirror, a Puzzles & Art corner, plants, loose toys
   (basketballs, frisbees, dodgeballs, water bottles). Remove: the pool table (delete RrRecCenterPool/RrRcCue/RrRcPoolBall
   and their uses), the poker table and chips, the jukebox, the lockers. Keep networking of the shared props working
   (RrNetBody local prop ids) and the working interactions (bell, radios, ping-pong, hoops, vending, Friend-O-Tron).
5. Spawn: the player arrives from the Dorm entrance looking in, as in the picture. RrRoom.Spawn and RescuePose must be
   inside the new hall.
6. Verify: compile; update reccenter_tour.json (room-frame shots: the entrance view matching the picture, the kiosk and
   sign, the balcony and stairs, the window wall, each door, the cafe; keep the hook calls that still apply, drop the pool
   ones) and run it - at most 4 harness runs. LOOK at the entrance shot next to reference 1 and fix what differs most.
Return per the schema: files, summary, verification, pngs (the best 5-6 shots), untested, readme_section (replacing the
README's Rec Center section; mark GUESSES).
`

phase('Build')
const measureP = agent(COMMON + MEASURE, { label: 'measure', phase: 'Build', schema: RESULT })
const doorsP = agent(COMMON + DOORS, { label: 'doors', phase: 'Build', schema: RESULT })
const reccenterP = measureP.then(m => agent(COMMON + RECCENTER(m), { label: 'reccenter', phase: 'Build', schema: RESULT }))
const [measure, doors, reccenter] = await Promise.all([measureP, doorsP, reccenterP])
return { measure, doors, reccenter }
