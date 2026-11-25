# iter15 Stunt Runner + Rec Rally (basic form)

Started 2026-10-17. Status: reading existing games.

## Data (done 20:30)
- Stunt Runner: rrsk2bytes --quant 3 of DN/refdata/apk/StuntRunner_{Lobby,Course1..6}.rrsk (plain apkscene RRSK, no jt=1)
  -> Data/StuntRunner_Lobby 17 KB, Course1 48, Course2 48, Course3 63, Course4 52, Course5 64, Course6 87 KB (total ~380 KB).
- Rec Rally: level107 gtscene jt=1 BUT the stock apkscene only gives the terrain a collider within the play area +30 m
  (spawn/pregame markers) -> near grid 190x184 m only; the track runs to x 344 z 354. Private tool copy in scratch
  (sr/tools, Emit.cs line 231: env RR_PLAY_PAD, default 30) run with RR_PLAY_PAD=400 -> terrain 251x251 @2 m collider=1.
  Then: drop vegetation plates (awk: G plate mats ~ Tree_|Bush|Plant_|TallTree; 1.0M of 1.22M plates), isle_thin.pl
  plates=20000 boxes=5000 (-> 39.4k plates, 4.5k collider plates), rrsk2bytes --quant 3 -> Data/RecRally_Chaparral.bytes 1.32 MB.
  Kept: DN/refdata/apk/rally/RecRally_jt1_wide.rrsk (raw jt=1), RecRally_wide_noveg.rrsk, RecRally_wide_thin.rrsk.
- integrator: run bankscene.sh to add the 8 new Data files to RrMain (not run by me, as instructed).
- 20:32 Rec Rally data redone: also drops vegetation dress boxes and every record within 2.3 m (xz, y 0.1..3.5) of the 6
  Team spawn points (the level's 6 parked grid karts: 162 records) -> RecRally_Chaparral.bytes 1.30 MB.
  Lap data: LapTriggerCollider1..12 (3a/3b, 4a/4b, 12a/12b alternates) + LapFinishTriggerCollider (-118,22,63 size 244x143x34).

## Stunt Runner code (done 21:0x, compiles; the only project error is another agent's RrAvatarHand.cs)
Games/StuntRunner: RrRegistry.StuntRunner.cs, RrStuntRunnerCourses.cs (scenes, anchors: courses in a row 450 m apart
700 m north of the lobby), RrStuntRunnerLevel.cs (RrJumbotronArea build + markers: lanes, checkpoints, finish button,
kill boxes, KO Cola spots, springboards, rope-net tiles), RrStuntRunnerRunner.cs (local runner: checkpoints, kill zones,
pickups x1.45/2 s, pads jump x2.4, wall-run gravity x0.15 <=2.5 s, slide C / VR stick click 1.5 s x1.6, net climb =
teleport to the ledge on Space / grip), RrStuntRunnerRoom.cs (RrGameRoom, rounds = courses, 180 s, authority ends a
course when every runner finished; personal bests in persistentDataPath/SomniumRecRoom/stuntrunner_best.txt).
Lobby + course 1 built in Build(); courses 2-6 built one by one in the background after arrival.
Next: Rec Rally.

## Rec Rally code (done ~21:40; full project compile 0 errors, API guard all proven)
Games/RecRally: RrRegistry.RecRally.cs, RrRecRallyTrack.cs (markers: Team1..6 grid, PreGame lobby, PostGame podium,
ScoreboardProjector console, LapTriggerCollider1..12 a/b + LapFinishTriggerCollider, 75 VisualRoot nitro cans, KillZone),
RrRecRallyKart.cs (install (Vehicle_RallyBuggy) visual, own raycast-wheel physics, vehicle ints 20-28 publish/follow),
RrRecRallySeat.cs (ETS seat pattern: freeze, SittingStraight on desktop, head on the eye, VR rig to eye; capsule ignores
buggy), RrRecRallyRoom.cs (RrGameRoom: START (4 race, fewer = practice), 10 s countdown seat-locked, 3 laps, gates in
order with one-gate tolerance, positions, nitro, auto/manual reset, finish, podium top 3, race end all-done / +60 s / 600 s).

## Tool note (scratch gets wiped)
The rally terrain collider needed apkscene Emit.cs line 231 changed to read the play-area pad from env RR_PLAY_PAD
(default 30): `{ double pad = double.TryParse(Environment.GetEnvironmentVariable("RR_PLAY_PAD"), out var pp) ? pp : 30;
pmn = pmn - new V3(pad, pad, pad); pmx = pmx + new V3(pad, pad, pad); }` - built as a private gtscene copy in the
session scratch (not in DN/tools, which other agents use). Re-run: copy apkscene+gtscene+rrtool, patch, gtscene build.sh,
RR_PLAY_PAD=400 run.sh prep ... level107 ... jt=1, then the awk filter (veg plates/dress + 2.3 m around the 6 Team spawns),
isle_thin.pl plates=20000 boxes=5000, rrsk2bytes --quant 3. Kept: refdata/apk/rally/RecRally_wide_noveg.rrsk (thin input),
RecRally_wide_thin.rrsk (bytes input); the 122 MB raw jt=1 file was deleted.

## Result / open issues (handover 2026-10-17)
- Integrator: run bankscene.sh (8 new Data files: StuntRunner_Lobby, _Course1..6, RecRally_Chaparral), bump RrVersion.
- Nothing tested in the client or editor (no harness, as instructed). Untested GUESSes: SR checkpoint box 14x5x5 m on the
  arch spawn point; finish by reach (1 m) or pressing the button; wall-run = gravity x0.15 (client applies it only while
  falling); slide cannot lower the capsule; net climb is a teleport to the ledge; pads only raise the jump (no auto
  launch); Rally gate order = the level's LapTriggerCollider numbers (one-gate skip tolerated); kart tuning (600 kg,
  24 m/s, grip) and the seat (desktop SittingStraight + head-on-eye; VR rig-to-eye) need a drive in the client;
  desktop driving uses the client's own camera (no chase camera of ours); remote buggies are dead-reckoned at ~10 Hz.
- Not done: SR jumbotron/spotlights/commentator VO beyond start/finish/falls, moving platforms (course 3), red caution
  blocks (course 4) behave as plain geometry, ziplines; Rally podium ceremony is a teleport, no merch/rewards, no team
  radio, no steering-wheel grab in VR (stick only), vegetation (trees/bushes) left out of the data.
