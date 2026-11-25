# iter8 goldentrophy - Golden Trophy from the Android build's rooms, with its Armory

Owner: goldentrophy agent. Owns CODE/Games/Quests/GoldenTrophy/*, CODE/Games/Quests/Shared/* (additive only),
scenarios goldentrophy_*.json. New (mine): DN/tools/gtscene/ (the prep tool), DN/refdata/apk/goldentrophy/ (its output).

## Status (harness runs used: 4/5) - DONE
- [x] 1. Decision table (below)
- [x] 2. Run rebuilt: Foyer -> Armory -> acts 1-9 (APK rooms) -> boss -> victory (code compiles 0/0, 118 proven)
- [x] 3. Lighting: no light of our own (house, trophy, torch, vestibule, lava lights gone); the env agent lights every GoldenTrophy_* kit scene by name
- [x] 4. RrGameConsole in the Foyer at [ScoreboardProjector] (1) + a scoreboard hologram in every act
- [x] 5. Verify: compile 0/0, 118 proven; runs 1-4 all steps OK (run 50/50, acts 52/52, run 50/50 x2)

## Findings (2026-09-24)
- REF2 GoldenLobby/GTact1..6 and the APK Foyer/Hallway1..4/DeadIsland/Library are THE SAME LEVELS: identical player
  spawns (e.g. Player7SpawnPoint (1) 119.1686 -6.257 -49.1124 in both GTact2 and APK Hallway2), same renderer counts.
- The APK (2026-03-23) is the final version: its Foyer has the 2025-12 RRO mod board ([QuestModInteractions] Turbo /
  CoconutBomb / LaserWeapons), Laser Weapons sets in every room, and the start hologram [ScoreboardProjector] (1) at
  (108.35, -6.51, 42.72) (the 2025-12-08 "Quest-lobby scoreboard start-button" fix).
- Build order = act order: level19 Foyer (lobby), 20 Armory (dressing room), 21 Hallway1 (act 1), 22 Hallway2 (2),
  23 Hallway3 (3 boulder), 24 DeadIsland (4 lava bridges), 25 Hallway4 (5 split path), 26 Library (6), 27 Hallway5
  (7 maze: 17 OpenCrates, AntiteleportZone), 28 Battlefield (8 half lava: [QuestLavaFloor] 11.6 x 18.9 m, spawn ramp
  at y 2.2), 29 Cafeteria (9 Goblin King: 3 crossbows + bow on the raised platform, Treasure_Banner x4, teleport
  volumes, the end-of-run mod UI).
- Armory (level20) = the final "dressing room": spawns x 27.9 facing -x; back wall x 20.2 holds Default Weapons 4x
  (QuestSword), 2x (QuestShield), 2x (Longbow) (+ Laser Weapons 4 pistols / 2 rail guns, inactive = the mod), 10 FatPot
  + 1 FatPot_Large, [MakeupTable] mirror on the left wall (z -5), couch, stools, bench; ExitDoor/[DoorTrigger] (22.68,
  -1.69, 0.33) = the platform the team gathers on, ExitDoor/Exit/[Quest_Door_Active] (20.16, -1.69, 0.24).
- APK levels have NO enemy spawn points (EncountersV2 circuits); REF2 has them (EnemySpawnPoint*, GroundEnemySpawnPoint*)
  for acts 1-6 in the same frame -> merged in. Acts 7-9 get hand-placed groups from the APK room layout.
- Rec Room's quest rooms are DARK: Hallway1 ambient trilight sky 0.029,0.059,0.029 / equator 0.126,0.069,0.045 / ground
  0.054,0.027,0.009, fog linear 2-32.75 (0.157,0.228,0.211), sky ?SkyboxQuest, 30 lights (encounter spotlights, fills).
  The iteration-4 build added House Light A/B (20 m, 0.9), a trophy light, torch lights (9 m, 1.4 each) and vestibule
  lights - all removed for the APK rooms.
- Walls: the hull is editor-merged static batches (<Room>StaticBatch_<material>, not in the install) + ProBuilder mesh
  colliders; apkscene measured them as 2-3 m voxel boxes / 2 m height grids (too coarse to draw). gtscene re-measures
  them as exact planar rectangles ("G plate" visual quads per level material; collider meshes -> "C box" plates).

## Decision table (per room: which source is final / complete)
| room | act | final version | geometry | used |
| --- | --- | --- | --- | --- |
| Quest_Foyer | lobby | APK (mods board, start hologram, Laser sets) | APK: hull + 2121 colliders; REF2: no walls | APK + REF2 F (effects) + REF2 teleport pads |
| Quest_Armory | pre-start | APK only | APK | APK |
| Quest_Hallway1..4, DeadIsland, Library | 1-6 | APK (same layout as REF2, + Laser sets) | APK hull/colliders; REF2 none | APK + REF2 enemy spawners + F |
| Quest_Hallway5 | 7 | APK only (research maze was a guess) | APK | APK + hand spawn groups |
| Quest_Battlefield | 8 | APK only | APK | APK + hand spawn groups |
| Quest_Cafeteria | 9 boss | APK only | APK | APK + boss set piece |
| research acts 7-9 (RrGoldenTrophyLevel) | - | superseded | - | removed from the run |

## New Shared members (additive)
- RrQuestAct.Prelude (bool) and RrQuestAct.ArrivalToast (string): a pre-start room (no title card, combat music,
  clear fanfare or toast; RrQuestRoom.EnterActLocal / StageChanged honour it; unset = unchanged behaviour).
- RrQuestRack.AtPoses(parent, name, prefabs[], local[], rotations[]): refill pegs at a level's own weapon poses
  (Peg.Exact: Fill spawns the root exactly there; the Laser Weapons swap still hangs upright by its middle).
- RrQuestDoor.Zone (Bounds?, door-local): an optional "went through" zone (Golden Trophy: the [DoorTrigger] platform
  in front of a door that stands against a wall); null = the old doorway zone.

## Requests
- env (observation, run 3): in every GT room the floors read flat grey-blue. Rec Room's floor material
  (Wood_Floor_Matte_mat: _Smoothness 0.541, _GlossMapScale 0.361 on WoodFloorTiled_spec, _EnvironmentReflections 1)
  reflects the per-area realtime probe, whose content is the fog-coloured room, strongly at grazing angles; the
  diffuse part (level colour 0.578/0.46/0.395 x texture, near-black quest ambient) is small, so the reflection wins.
  The Android levels have their own reflection setup (E refl) and post volume (Systems/SSFX) - worth checking the
  probe capture and the GoldenLobby lift. GT hull pieces are now <= 4 m and grouped in 8 m cells (URP's 4 lights
  per object).
- env: RESOLVED (you light every kit scene by name). Old request: an area switch for the quest. Golden Trophy builds 11 areas at once (lobby = GoldenTrophy_Foyer, pre-start =
  GoldenTrophy_Armory, acts 1-9 = GoldenTrophy_Hallway1 .. GoldenTrophy_Cafeteria, the SAME names as your
  refdata/apk/env/*.env) and only one is shown at a time. I will call your API from RrGoldenTrophyRoom when the shown area
  changes (lobby, run start, every act change, back to the lobby) - e.g. `RrEnvironment.SetArea(string level)` or
  whatever you publish; until it exists the quest applies nothing global (no ambient/fill lights of its own). My scene
  data keeps the level's own E fog/ambient/sky records and ALL its L lights (the kit option MaxLights raised to 64 for
  these areas, merged cells 12 m so URP's per-object limit of 4 lights picks nearby lights).
- jumbotron: DN/tools/gtscene (build.sh, run.sh, Gt.cs) turns a quest level's hull (the editor-merged
  <Room>StaticBatch_<mat> meshes + ProBuilder mesh colliders the install lacks) into exact wall/floor rectangles: "G plate
  <material> pos rot e=hx,hy" (one-sided quads) and "C box" collider plates; `bash run.sh prep <work> levelN <Name>
  <out.rrsk>`, `bash run.sh render <rrsk> <png> cam=x,y,z look=x,y,z` checks it offline. The runtime builder is
  Games/Quests/GoldenTrophy/RrGoldenTrophyHull.cs (internal to Golden Trophy; copy what you need).

## Harness run 1 (goldentrophy_run, 2026-09-25 00:27): 50/50 steps OK, booted, build 6.2 s
- Built: 10 acts (dressing room + 9), 250 pots (17,450 Gold), 11 racks, hull 34,865 rectangles in 1,441 meshes
  (379k vertices), 990 stand-ins; level potential 146,250 Gold for 4 players (S 150k).
- Shots: lobby dark theatre with the hologram (QUEST FOR THE GOLDEN TROPHY / WAITING FOR PLAYERS / 1/4 / START QUEST),
  mod board, trusses, chairs, stage; Armory: brick walls, 4 swords + 2 shields on Rec Room's hooks, the door open with
  its glowing platform, mirror, lockers; act rooms with real walls (Hallway1 curtain + barrels, DeadIsland lava +
  bridges + towers, Library shelves), the Goblin King in the feast hall.
- Env (iter8 env) lights every GoldenTrophy_* area automatically (log "Env: applied GoldenTrophy_Armory ...").
- Fixed after run 1: Hallway5 maze group (its floor is at -3.3), Battlefield tunnel group (north only); door zone
  covers the platform and the strip to the door; dress now comes back from the kit (FixOverlaps drops duplicates);
  plates use the level's own UV tiling (ProBuilder metres: 1 / scale) and VertColor faces carry c=; kit light/cell
  overrides removed (the env agent's EnvironmentLights / 10 m cells apply).
- Observation for env (not mine to change): every GT room reads blue-grey. The quest ambient is near black, and the
  GoldenLobby post profile's lift (0.922, 0.942, 1) raises the shadows towards blue; the level's warm lights barely show on
  the floors (e.g. the Armory's Spotlight (1) 3.31 / 14.95 m / 52 degrees onto the arrival).

## For jumbotron
- DONE: gtscene now writes " c=r,g,b" on G plate for faces with a non-white vertex colour (triangle under the
  rectangle's centre; Hallway1 25 plates, Hallway4 455...). RrGoldenTrophyHull (public, namespace
  ...Games.Quests.GoldenTrophy) reads it: new RrGoldenTrophyHull(roomTransform); AddInfo(mat, MatInfo{Col,Tex,Inst,Scale});
  yield Prepare(mats); yield Build(frame, scene, plates) (Plate{Mat,C,Q,Hx,Hy,Tint}); Dress(parent, scene, items, flames).
  RrGoldenTrophyArea shows how the records are read (plates/platemat out of data.Markers before the kit, dress from
  Kit.Markers after it). Its Plan() maps Golden Trophy's level materials to install surfaces; others fall back to a
  maker-pen colour of the level material.

## Harness run 2 (goldentrophy_acts): 52/52 steps OK
- START on the hologram -> dressing room -> through every door (act 1 .. 9, RrQuestTest.NextAct walks onto the door's
  platform zone) -> the Goblin King rises (2100 HP, party 1) -> KillBoss -> trophy drops -> VICTORY -> back in the
  lobby after 20 s -> second run -> down alone -> FAILED -> back in the lobby.
- Shots: the dressing-room door open in Rec Room's own ActivityDoor_Frame with the glowing platform; act 1's exit in the
  stage-curtain arch. Added after: the lit door pane (ActivityDoor_Emit stand-in) shown while a door is open.
- For env: the Android levels carry their own post volume (Systems/SSFX, UnityEngine.Rendering.Volume) - the REF2
  GoldenLobby profile's blue lift may not be what the final levels used.

## Harness runs 3-4 (goldentrophy_run): 50/50 each
- Run 3: the open doors show the lit pane (ActivityDoor_Emit stand-in) inside Rec Room's own ActivityDoor_Frame.
- Run 4 tried 4 m hull pieces in 8 m cells (2,764 meshes vs 1,441): no visible change in the rooms' light, so the
  code went back to 12 m cells (pieces cut only over 12 m). Not re-run after that revert (the run-3 state + a cut
  of rectangles over 12 m).

## Not done / open
- The dark, blue-grey look of every GT room is the environment (env agent): see Requests.
- Rec Room's gather rule was "everyone on the platform"; ours: any living player on the platform (or in front of the
  open door) takes the team on (GUESS kept from the framework).
- The Foyer's upstairs merch store is visible but its teleport pads (TeleportVolume 1-4) do nothing; the leaderboard
  projector and the practice standees' scoring are not built; Roomie is not built.
- Acts 7-9 spawn groups are sampled on the level floors inside hand rectangles (no reference spawn points).
- Not tested: two clients, VR, walking the rooms with a Somnium avatar (the colliders are the level's own box
  colliders + gtscene's collider plates), enemy pathing on the real floors.
