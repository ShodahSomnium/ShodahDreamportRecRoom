# iter8 - paintball (Paintball lobby feels strange)

Owner: paintball agent. Owns CODE/Games/Paintball/* (incl. Scenes/), DN/tools/pbscene, scenarios paintball_*.json.

## Status
- [x] read notes (DN/apk/paintball.md, README Iteration 7, footage notes), looked at iteration-7 shots (SP pb/final)
- [x] trees: install has NO 3D tree (only cardboard (Sandbox_FlatTree*) standees + PineTree_Dead) -> our own trees
      reshaped: crowns of 5-8 faceted clumps in two shades, white aspen trunks, 4-tier pines (RrPaintballDress.Tree)
- [x] shadows on for all stand-ins (were ShadowCastingMode.Off)
- [x] tyre stacks = install Tire_LOD0 + Paintball_TractorTire_Mat, 3 per 1.08 m stack at scale 0.68 (were one black drum)
- [x] containers: corrugation ribs + rails
- [x] structures: collider -> smallest containing region (the lobby's roof collider fell into the 7x5x12 m
      LightShaftLocker volume and was drawn see-through = "no roof", pale haze over the spawn); light shafts/fog never
      regions and never drawn; voxel boxes covered < 30 % by drawn colliders are kept
- [x] lobby building faces: inside (roof overhead + wall across, 2 physics rays) Paint_Warm_Grey_Mat, outside
      Wood_Horizontal_RedOrange_Mat; covered floors Paint_DarkGrey_Mat; corrugated roof Metal_Galvanized_Mat
- [x] signs: the level's own TMP texts read from the Android level (private debug copy of apkscene "find" in my
      scratchpad, prints m_text): Door_Sign "Paintball" (exit door), "Customize your look!" x2, "Try 'em out!"
      (test wall); pedestal button text is "Challenge" (Practice Arena duel); orange boards: PRACTICE ARENA (arch),
      PAINTBALL (entrance board), CHOOSE YOUR SKINS / SPRAYS / TARGET PRACTICE / LEADERBOARD (GUESS wording)
- [x] lounge: rug red + orange border + roundel (level's Rug_Quest), armchair purple (level: ?Couch_Plush_Purple_Mat)
- [x] RrGameConsole at the level's 2nd (inactive) [LeaderboardProjector] (-46.25,-5.124,71.15) yaw -90.3 facing the
      locker building's plaza door; Modes/Maps/START wired to VoteMode/VoteMap/PressStart
- [x] rocks: RecRoyale_Rock_{Cliff,Tall,Plateau,Cone,Stump} + Rocks_Frontier_Mat fitted to each cliff bounds (were grey eggs)
- [x] lockers: red/teal per row slot, door vents + handle; Spillway pine leaves = 3 cones
- [x] compile 0/0/0, 113 proven (00:35)
- [x] harness run 1 (50/50 ok; NOTE it ran the OLD scenario: the harness reads SP/harness/scenarios, not DN/tools/harness/scenarios; copied mine there for run 2). Seen: PRACTICE ARENA / PAINTBALL / LEADERBOARD / Try 'em out! signs read right; hologram opens (4.36 x 3.27 m, 1/8, 1 MORE PLAYER NEEDED, MAP: RIVER); clumpy trees; real tyre stacks; ribbed containers; roof back on the locker room (grey interior, dark); Quarry cliffs now rows of RecRoyale rocks. Lobby: 30 install tyres, 21 rocks, building faces 1152 in / 270 out, 20 labels, 1 couch material purple.
- [x] fixes for run 2: lobby MaxLights 16 (14 level lights, 6 were dropped at 8), ceilings Paint_White_Mat, one CHALLENGE label (back-to-back TMP labels garbled), container ribs on cover blocks too
- [x] maps: coverage rule (keep voxel boxes < 30 % covered) now lobby-only: run 1 drew cream envelope slabs over Merry Midway and the Drive-In
- [x] harness run 2: aborted after 6 s, GoldenTrophy compile errors (another agent mid-edit), no shots (counted as used)
- [x] harness run 3: 56/56 ok. Hologram open (projector mount, 1 row, START greyed with 1 player, "1 MORE PLAYER NEEDED",
      MAP: RIVER). Signs, CHALLENGE label, rug, tyres, containers, rocks all read right; env lighting now applied
      (level sun/fog/Trilight ambient, gradient sky): exteriors good, the locker room interior too dark (request to env).
      Drive-In back to its iteration-7 look (coverage rule lobby-only). Gallery Bonky standee was a blank box.
- [x] for run 4: Bonky -> install PracticeMale_Cutout_LOD0 + Standee_Male01_mat; courtyard door shot; lounge angle
- [x] harness run 4 (last; 4 of 4 used incl. the aborted run 2): 56/56 ok. Gallery male cutout in place of the blank Bonky box;
      courtyard view of the exit door: PAINTBALL board, white door surround, teal and red lockers, skins shop. The new
      lounge camera faced a wall (use run 3's lounge shot: red rug, orange border, roundel). Maps unchanged from run 3.
      Final PNGs: SP(1c0947a6)/pb/iter8_final (run 4) and pb/iter8_run3.

## Open
- Locker-room interior dark under the level's realtime lighting (request to env above).
- Sign wording for CHOOSE YOUR SKINS / SPRAYS / TARGET PRACTICE / LEADERBOARD is a GUESS (the sign atlas is not in the
  install); PRACTICE ARENA and PAINTBALL are from the footage; the four door-sign texts are the level's own.
- Merry Midway's tall concrete slabs and Spillway's dam boxes unchanged; map-side signs (Red Base / Blue Base arrows,
  "Paintball Lobby" doors) are in the maps' own pregame rooms, which we do not use.
- The pedestal (Rec Room's "Challenge" duel button) still starts the team match; the 1 v 1 duel is not built.

## Findings
- Paintball_Lobby has no GameScoreboardProjectors; it has two [LeaderboardProjector] Paintball_Lobby_Variant (active at
  (-46.41,-5.67,60.10) = our LAST MATCH scoreboard; inactive at (-46.25,-5.12,71.15) = our game-start hologram).
- Level TMP texts (scene): "Paintball" (-52.49,-2.96,57.15); "Customize your look!" (-56.55,-2.62,58.08) and
  (-56.37,-2.94,73.40); "Try 'em out!" (-66.41,-2.14,71.69); duel button "Challenge" (-44.37,-4.12,64.27); duel board
  "YOU READY?" (-20.91,1.12,70.54); gallery board "GET READY"/"NOW"/"BEST" (-69.56,-2.26,68.24); gallery "START".

## Requests
- env: the lobby's locker room (inside the level113 building, around the pregame spawns, room-local x -4..10, z -21..4) is
  very dark with the level's realtime lights (run 3: walls near black, ceilings lit only by the Trilight ground colour
  #C5AD41, so they read olive). Rec Room baked this interior (lightmaps are not in the install). If you add an
  interior fill (probe / ambient boost / baked-GI stand-in) for kit scenes, the Paintball lobby is a good test case.
  My surfaces there are install materials: walls Paint_Warm_Grey_Mat, ceilings Paint_White_Mat, floor Paint_DarkGrey_Mat.

## Notes for the orchestrator
- One compile poll hit CS8103 (user strings over the limit) during other agents' edits: generated scene text is now ~3.2 M chars
  (Paintball 1.09 M, Golden Trophy, Jumbotron). Watch it if more scenes are added.
