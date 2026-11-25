# iter13 / crimson - Curse of the Crimson Cauldron (basic form)

## Plan
1. Read existing quest code (Jumbotron / GoldenTrophy / Quests/Shared), RrSceneBank, rrsk2bytes, apkscene, gtscene jt=1, jumbotron_convert.sh.
2. Convert APK levels 50-60 (Quest_Goblin2_*) -> RRSK (apkscene + gtscene jt=1 + markers) -> CODE/Data/CC_*.bytes (rrsk2bytes.sh).
3. Code in Games/Quests/CrimsonCauldron/: Registry, Room (lobby+console), Acts (from markers), Boss (simplified), Hull/look reuse.
4. Compile check (bash SP/compilecheck/build.sh project --editor) -> 0 errors.
5. Notes: harness steps + shared requests.

## Log
- (resume 2) Found from attempt 1: tools crimson_convert.sh / crimson_markers.pl / crimson_thin.awk (DN/iter13); NSP/cc13/refrrsk = 11 full RRSK
  (59 MB, markers appended), NSP/cc13/thin = thinned (28 MB), NSP/apkwork/ccdump+ccstats. No Data/*.bytes, no code yet.
- (step A) crimson_markers.pl FIX: /Rig/i exclusion ate every Triggers/Overlap Volume -> enc_trigger now checked first (backup NSP/cc13/crimson_markers.pl.before). New crimson_thin2.awk (plates < 0.1 m2 of big-surface materials / foliage). NSP/cc13/thin3 = thin + thin2 + fixed markers.
- (step B) size: thin (4.73 MB gz) -> + thin2 (3.65) -> + crimson_quant.awk (wall-like C < 0.1 m2 out; C / foliage / rock plates to
  1 cm, sizes rounded up) = 3.04 MB for 11 rooms. crimson_convert.sh rewritten = the whole chain (prep, markers, 3 passes,
  rrsk2bytes); run with --no-prep from NSP/apkwork/ccraw (= attempt 1's refrrsk) -> DN/refdata/apk/Crimson_*.rrsk + Data/Crimson_*.bytes.
- (step C) DONE Data/Crimson_{Foyer,Armory,CastleCourtyard,Forest1,GoblinCamp,Forest2,Bog,Mines1,BoilerRoom,BellTowerStairs,BellTowerArena}.bytes (+meta) = 3,041,629 B; refdata/apk/Crimson_*.rrsk written. NEXT: code.
- (step D) code plan (Games/Quests/CrimsonCauldron/): Registry; Room (RrQuestRoom: def, 11 areas on a 320 m grid, lobby console,
  ShowAreas, lighting SetArea(Crimson_X), fall check for the Arena's PitKillzone, hooks); Acts (Foyer, Armory prelude, acts 1-9,
  reusing RrJumbotronArea (public, generic kit+hull+haze builder) - pots/weapons/doors/zones done here, GT-style medieval exit
  door on [Quest_Door_Active] + [DoorTrigger] pad); Kit (zones with rotation, racks at Rec Room's poses, pots 50/100, lava =
  bog water / boiler pit); Ladder (TeleportVolume -> TeleportDestination); Steam (Quest_SteamTrap cycles, local down);
  Witch (boss: red imp x2.2 + hat, 3 phases by HP, adds from the level's East/North/West points, orb volleys). Bog monster =
  green barrel knight x1.6. Explosive barrels: skipped (no framework support; open issue).
- (step E) written: Registry, Kit, Ladder, Steam, Witch. NEXT: Room + Acts, then compile.
- (step F) Room + Acts written, metas made. NEXT: compile check.
- (step G) compile: full project has 5 errors in LostSkulls (another agent, in progress) + Crescendo registry; a copy without those two folders = 0 errors, 1 warning (not ours), API guard 118/118.
- (step H) harness scenario written (not run): SP/harness/scenarios/crimson_run.json; offline check NSP/cc13/checkacts.pl: every group and zone of the Acts resolves in the data.
- (step I) lighting env: apkscene env for levels 50-60 -> DN/refdata/apk/env/Crimson_*.env (+ 11 rows appended to _env.tsv). RrEnvironmentTable.cs (Core) regeneration = shared request (envcs).
- (step J) added: ceiling-hatch exit (BellTowerStairs door is flat: leaves dropped, the pad takes the team on); Finale reward chests (RrQuestKit.Chest on the 4 GiftBoxSpawnPoints, open on the win). Compile copy: 0 errors.
- (step K) ground spawn groups skip points in the water (Bog flyer regions lie on the swamp); the bog monster's pool ([QuestLavaFloor] (2)) is not a down; compile copy 0 errors.

## Result (iteration 13) - The Curse of the Crimson Cauldron, basic form

Code: Assets/#User/SomniumRecRoom/Games/Quests/CrimsonCauldron/ (namespace ...Games.Quests.CrimsonCauldron, 7 files + metas)
- RrCrimsonCauldronRegistry.cs - RegisterCrimsonCauldron (Quests, 1-4 players, witch polaroid cover, lobby song).
- RrCrimsonCauldronRoom.cs - RrQuestRoom: def (Gold, -2000 per down, 11 roster types incl. Imp Witch = GoblinVolley,
  bog monster = GoblinKnightCharge x1.6, witch = GoblinEliteFlyingRanged x2.2; kill gold GUESS ~2x Golden Trophy; boss
  15,000; music Ballad of Vota Vader (lobby/boss), Exploring the Mines (explore), GoldenTrophyCombat2 + clear fanfare
  (GUESS reuse), stingers "Crimson Cauldron - Our Heroes Have Triumphed/Failed"), 320 m act grid, one room active at a
  time, lighting SetArea(Crimson_X) + RrPostFx area, gather pads, Arena kill floor, reward chests, harness hooks.
- RrCrimsonCauldronActs.cs - Foyer (console on [ScoreboardProjector] (1), Rec Center portal in the activity door,
  practice rack), Armory (prelude, weapon rack at Rec Room's poses), acts 1-9 with the walkthrough's waves on each level's
  encounter points and Overlap Volume zones (all 104 groups/zones resolve: NSP/cc13/checkacts.pl).
- RrCrimsonCauldronKit.cs - markers -> pots (50/100), racks, GT-style medieval exit door (+ ceiling hatch case), zones
  (turned volumes), lava (bog water / boiler pit), ladders, steam, kill floor.
- RrCrimsonLadder.cs (touch the rungs -> TeleportDestination), RrCrimsonSteam.cs (2 s every 6 s on RrNet.Now, down),
  RrCrimsonWitch.cs (3 HP phases, adds from East/North/West, purple orb volley every 5 s).
Reused (read-only): RrJumbotronArea / Hull / Look / Haze (generic level builder), RrQuestRack, RrQuestDoor, RrQuestKit.

Data: Data/Crimson_{Foyer,Armory,CastleCourtyard,Forest1,GoblinCamp,Forest2,Bog,Mines1,BoilerRoom,BellTowerStairs,
BellTowerArena}.bytes (+meta) = 3,041,629 B. Source RRSK: DN/refdata/apk/Crimson_*.rrsk; env: DN/refdata/apk/env/Crimson_*.env.
Tools: DN/iter13/crimson_convert.sh (whole chain), crimson_markers.pl (fixed), crimson_thin.awk, crimson_thin2.awk,
crimson_quant.awk.

Compile: the full project currently has errors only in other agents' in-progress folders (LostSkulls, Crescendo); a copy
without those two folders compiles with 0 errors (API guard 118/118). Harness NOT run (builder rule); scenario ready:
SP/harness/scenarios/crimson_run.json.

Shared requests (integrator):
1. bash DN/tools/rrscene/bankscene.sh once (adds Data/Crimson_*.bytes to RrMain's scene data list). Without it every
   act builds a plain 40 m stand-in floor and a stand-in exit (logged "no level data in the scene").
2. Lighting rows: bash DN/tools/apkscene/run.sh envcs <work> "DN/refdata/apk/env" "Core/RrEnvironmentTable.cs"
   "DN/refdata/env_extra" (adds Crimson_* rows, +27 KB of strings; without them the scenes light from their own records).
3. Optional (polish): RrQuestRoom.LocalLava's toast is "Burned in the lava!" also in the Bog's water. Add to RrQuestRoom
   `protected virtual string LavaToast { get { return "Burned in the lava!"; } }`, use `Hud.Toast(LavaToast, 2f);`, and
   then the room can override it with `ActIndex == BogAct ? "Swallowed by the bog!" : "Burned in the lava!"`.
4. README.md: iteration-13 row / checklist line for the Crimson Cauldron (lobby -> Armory -> 9 acts -> Crimson Witch).

Open issues / polish later: explosive barrels are not interactive (no framework support); the witch is simplified (weapon
hits + HP phases instead of batting her orb back); Imp Witch / bog monster are stand-ins; steam timing GUESS; no
`gtscene float` pass for these levels (some props may float); the size passes removed plates < 0.1 m2 of big surfaces and
foliage (small holes in trees/rocks possible); RrJumbotronArea logs under the "Jumbotron" tag.
- (step L) DN/refdata/apk/INDEX.md rows level50-60 marked converted. DONE (final compile of the copy: 0 errors).
