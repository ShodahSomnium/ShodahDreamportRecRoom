# iter13 - crescendo (Crescendo of the Blood Moon, basic form)

## Plan
1. Read existing quests (Jumbotron, GoldenTrophy) + Quests/Shared + story doc + refdata INDEX.
2. Convert levels 79-89 (Quest_Dracula1_*) with apkscene + gtscene jt=1 + markers -> rrsk -> CODE/Data/*.bytes (rrsk2bytes.sh).
3. Write Games/Quests/Crescendo/*: registry hook (RegisterCrescendo), room, acts, enemies (vampire/bat), Dracula simplified, look.
4. Compile check (bash SP/compilecheck/build.sh project --editor).
5. Notes: shared requests, harness steps.

## Log
- 17:16 RESUMED (2nd attempt; previous one left no log lines). Found from the 1st attempt: NSP/cbm/dump/level79-90.txt (apks find
  dumps for markers), NSP/cbm/rrsk/Crescendo_<Room>.rrsk (gtscene prep jt=1, OLD gtscene 14:15 without the iter13 re-measure/
  float), NSP/cbm/stats/*.tsv, NSP/cbm/texavg.txt, DN/iter13/crescendo_tools/cbm_trim.pl -> NSP/cbm/trim/*.rrsk (0.8-6 MB raw).
  New gtscene with float/re-measure built at NSP/int11/tools/gts (17:06). Work folder with bank.bin: NSP/apkwork.
- 17:25 act map (story doc 7.1 + RrPostFxProfiles Crescendo areas 0..13): lobby=Foyer(79); 1 Township(80) Burning Village;
  2 CarriageRide(81) Forest Road; 3 CastleEntry(82) Castle Grounds; 4/7/10/12 CastleMainFoyer(83) Grand Hall; 5 Gallery(84);
  6 Library(85) Chamber of Knowledge; 8 Park(86) Garden of Sorrows; 9 Ballroom(87) Inverted Ballroom; 11 Dungeon(88) Lower
  Sanctum; 13 BossRoom(89) Concert Hall. Encounters in the dumps: Systems/Encounters/<E#: name>/{Spawn Points,Spawn Regions,
  Triggers/Overlap Volume} (like EncountersV2) -> write crescendo_markers.pl.
- 17:25 DESIGN: reuse RrJumbotronArea (public; builds kit+terrain+hull+haze from an RRSK; Jumbotron reuses RrGoldenTrophyHull
  the same way) - no copy (string heap). Weapons/pots read from Kit.Markers ourselves.
- 17:26 started NSP/cbm/prep_all.sh (NEW gtscene NSP/int11/tools/gts, jt=1) -> NSP/cbm/rrsk2 + stats2, log NSP/cbm/prep_all.log.
- (2) DATA DONE: tools DN/iter13/crescendo_tools/{cbm_convert.sh (driver), cbm_trim.pl (1st attempt: foliage->canopy boxes,
  plates < AMIN out, level-only colours from NSP/cbm/texavg.txt), cbm_thin.awk (dups, small wall colliders), cbm_markers.pl
  (enc_trigger / enemy_spawner / enemy_region / door_trigger (pad Base pose+size) / gate / chest / mirror / portal / trap /
  respawn)}. Run: bash cbm_convert.sh NSP/apkwork NSP/int11/tools NSP/cbm/dump. Output DN/refdata/apk/Crescendo_<Room>.rrsk
  + CODE/Data/Crescendo_<Room>.bytes (11 files, 2,824,117 B total; Dungeon 505 k, CastleEntry 430 k, Township 393 k).
  Marker summary per level: perl NSP/cbm/msum.pl NSP/cbm/mk/levelN.txt.
- (2b) GATES: [Quest_Dracula1_MidLevelGate] = an AreaBlockingGeo box collider (9 x 27 x 11-18 m invisible wall) + an iron door
  (fence_midgate, Candleabras_mat plates). cbm_markers.pl now writes gate_door (pose, scale, hinge) + gate_block (world box);
  new cbm_gates.pl (run by cbm_convert.sh last) takes the 10 blocking colliders + the door plates out of the data; the quest
  rebuilds them (RrCrescendoGate: blocker on until the gate's waves are beaten, bars rise). Data re-run: 2,815,xxx B total.
- (2c) LIGHTING ROWS: apkscene env -> NSP/cbm/env/Crescendo_*.env, copied to DN/refdata/apk/env/ (11 files). envcs into a
  scratch copy: NSP/cbm/envtable.cs (80 scenes; the 11 Crescendo rows = DN/iter13/crescendo_tools/env_rows.txt, ~35 KB of
  string). SHARED REQUEST: regenerate Core/RrEnvironmentTable.cs with envcs (README line 104) - it picks the new .env up.
- (3) CODE PLAN (writing now, Games/Quests/Crescendo/): RrCrescendoRegistry, RrCrescendoRoom (def, build, hub/checkpoint/
  banking, rank screen, lighting, test hooks), RrCrescendoActs (lobby + 11 level builders on RrJumbotronArea; 13 acts, the
  Grand Hall built once and used by acts 3/6/9/11 with its 4 doors), RrCrescendoGate (gate blocker + rising bars),
  RrCrescendoDracula (3 mirrors, bat phase, fireball lines, teleports), RrCrescendoTraps (Dungeon arrow-trap zones).
- (3a) wrote RrCrescendoRegistry.cs, RrCrescendoGate.cs, RrCrescendoRoom.cs (field DraculaFight). NEXT: RrCrescendoActs.cs, RrCrescendoDracula.cs, RrCrescendoTraps.cs, metas, compile.
- (3b) all 6 .cs written + metas (folder meta guid = md5 of path). NEXT: compile check.
- (4) COMPILE: 0 errors (server-like 342 files, 1 warning not ours; API guard 118/118; editor 0).
- (5) DATA FIX + re-run: cbm_trim SKIP no longer drops SupportBeams_Mat ("Beam") / TowerFacade_GlowingWindows_Mat ("Glow");
  level-only BrickFence_Mat / Fence_Brick_Mat / Log_Post_Fence_Mat renamed Crescendo_BrickWall(2)_Mat / Crescendo_LogPost_Mat
  (RrJumbotronLook draws any "fence" level material as glass). Data total 2,885,464 B. Offline renders NSP/cbm/png/*.png ok.
- (6) code: exit fallback (8 m zone at the arrival when a level has no pad), mini-boss double-ready guard, fall rescue.
  Compile 0 errors.
- (7) offline checks: NSP/cbm/check_groups.pl - every spawn group (79) non-empty, every Enter zone found (2 have no trigger:
  CastleEntry Encounter5-aFinaleSetup, Gallery Encounter4-Bandroom -> zone around their points), all 9 exit pads, all 10
  gates (door + blocker), 3 mirrors. Renders NSP/cbm/png/{township_spawn,hall_spawn,carriage,boss}.png (debug colours).

## Result (iteration 13, basic form) - Crescendo of the Blood Moon

Code: Assets/#User/SomniumRecRoom/Games/Quests/Crescendo/ (namespace ...Games.Quests.Crescendo):
- RrCrescendoRegistry.cs - RegisterCrescendo (Quests, 1-3 players, cover Consumable_Polaroid_Dracula_Tex(2048), CBM_Gavotte).
- RrCrescendoRoom.cs - the quest (RrQuestRoom): roster (6 kinds + 5 mini-bosses + Dracula + decoy/real bats), silver per
  kill 10/5/15/10/25/20, mini-bosses 100/400/200/800/1600, Dracula 3200 (GUESS), candles 2/3, pumpkins 3; 13 acts on 10
  levels (Grand Hall built once, acts 4/7/10/12), gates (hazard bits 4+), Grand Hall = bank + checkpoint (a whole-team
  down after it rewinds to the last visit with the banked silver), rank = combat score 4400 - 200/down (S+ 4160, S 3160,
  A <= 8 downs, B <= 12) + rank bonus 15k/10k/6k/3k/2k on the victory screen, lighting SetArea(scene) + RrPostFx act+1,
  fall rescue, harness hooks (TestConsoleStart, TestAct2..TestBoss, TestFightClear, TestToExit).
- RrCrescendoActs.cs - Foyer lobby (console on [ScoreboardProjector] (1), Rec Center portal on [ActivityDoor], practice
  weapons, candles) + the 13 act builders on RrJumbotronArea (reused, public: kit, floors, hull, look, haze) with waves on
  each level's own encounters; weapons at the level's poses + 3 chest weapons per level (flintlock/crossbows/blunderbuss).
- RrCrescendoGate.cs - mid-level gate: invisible blocker (the level's AreaBlockingGeo box) + iron bars that rise.
- RrCrescendoDracula.cs - Dracula (Bellhop body x1.35): organ intro, 5-fireball lines (1/2/3 bursts per stand), moves
  between stage front and balconies, bite zone, 3 light mirrors with fire buttons (auto-aim, line of sight, 6 s recharge),
  bat phase (decoys + the big real bat + minions), 3 hits kill.
- RrCrescendoTraps.cs - Lower Sanctum arrow-trap zones (click, then arrows every 3.2 s: a down in the zone).
Data: Assets/#User/SomniumRecRoom/Data/Crescendo_{Foyer,Township,CarriageRide,CastleEntry,CastleMainFoyer,Gallery,Library,
Park,Ballroom,Dungeon,BossRoom}.bytes (+.meta), 2,885,464 B. RRSK: DN/refdata/apk/Crescendo_*.rrsk. Env: DN/refdata/apk/env/
Crescendo_*.env. Tools: DN/iter13/crescendo_tools/ (cbm_convert.sh = the whole pipeline).

SHARED REQUESTS (integrator):
1. bankscene.sh: add the 11 Data/Crescendo_*.bytes to RrMain's scene data (without it every level is empty: "no scene data").
2. Core/RrEnvironmentTable.cs: regenerate with envcs (apkscene README line 104); the 11 Crescendo .env files are already in
   DN/refdata/apk/env (rows ~35 KB of string; a scratch result is DN/iter13/crescendo_tools/RrEnvironmentTable.withCrescendo.cs.txt).
   Without it the levels use their RRSK E records (fog, ambient, sun) - fine for a first test.
3. Core/RrEnvironment.cs KeyFor: `case RrActivity.Crescendo: return "Crescendo_Foyer";` (optional; the room names its area each frame).
4. README.md: a Crescendo line in "Where it stands" / the iteration table (text in this file's Result).

HARNESS (room frame; level i root = RrCrescendoRoom.LevelSlot(i): 0 Township (-450,0,300), 1 Carriage (-150,0,300),
2 Castle (150,0,300), 3 Grand Hall (450,0,300), 4 Gallery (-450,0,600), 5 Library (-150,0,600), 6 Park (150,0,600),
7 Ballroom (450,0,600), 8 Dungeon (-450,0,900), 9 Concert Hall (-150,0,900); each level's local origin = its arrival).

OPEN (not in the basic form): the carriage does not move; whip grapple/traversal, push-in books and secret shelves,
whip-breakable walls, swinging axes / fire cannons / laser walls, silver chests' random loot, the gift shop; the arrow-trap
x0.1 penalty; mirrors aim themselves; Dracula's organ animation and teleport VFX; decoy bats use the framework's Lunger kind
(DraculaBatLunging, may keep low); "(Whip)" may not be a spawnable install prefab (then WarnOnce, no whip); plates under
0.03-0.06 m2 are left out (small see-through gaps possible); combat music CBM_CombatMiniboss is a GUESS. Not run in play mode.
- (8) DONE 18:06: install check - every prefab/clip/VFX/sprite the code names is in the rrtool index (Whip, Dracula weapons, rum jug, collectables, spawners, CBM_* music, SR_GateDown_02, [Quest_Dracula1_Silver_VFX], cover). Final compile 0 errors.
