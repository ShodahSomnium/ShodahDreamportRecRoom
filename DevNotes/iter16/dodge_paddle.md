# iter16 - Dodgeball + Paddleball rooms from the Android levels

Agent notes (append-only log). Lead file is rooms_same.md (do not edit).

## Plan
1. Data: gtscene prep jt=1 level11 Dodgeball / level13 Paddleball -> DN/iter16/work/*_jt.rrsk; thin/clean if big;
   rrsk2bytes --name Dodgeball / Paddleball -> CODE/Data/*.bytes (<1 MB). No bankscene (lead does it).
2. Measure from jt rrsk: spawns/team, scoreboard, exit door, courts, nets, ball spawns, zones, floor heights.
3. Code: RrDodgeballRoom.cs / RrPaddleballRoom.cs -> RrJumbotronArea build like RrSoccerRoom; markers at runtime,
   measured constants as fallback; old hand-built geometry only as fallback when RrSceneBank.Text null.
4. Compile after each step (compilecheck build.sh project --editor).

## Log
- 2026-11-10 15:41 notes created
- 2026-11-10 15:41 gtscene prep jt=1: Dodgeball_jt.rrsk (level11), Paddleball_jt.rrsk (level13) in work/
- 15:45 raw jt sizes: Dodgeball_jt 1471 KB text (14175 plates, 4695 C, 56 R, 3 P (DodgeballBall)), Paddleball_jt 1316 KB
  (14562 plates, 64 C, 54 R, 1 P (Paddleball_Paddle)). Both levels have 8 ActivityDoor double doors (frames at x +-3.4/+-5.7, z +-17.9).
- 15:50 DATA done. Clean: awk -v door=X,Z -f DN/iter16/dp_tools/dp_clean.awk in in > *_clean.rrsk (drops C spheres = the
  level's balls/coconuts, ?Levitation A, the ONE exit door's R+C records, the small circuit-board C box). Doors used:
  Dodgeball frame (-5.669,-17.908), Paddleball frame (-3.4197,-17.9026) (nearest the pre-game / spectator spawns).
  rrsk2bytes -> CODE/Data/Dodgeball.bytes 134353 B (guid 67a5b004...), Paddleball.bytes 121020 B (guid 91656f43...). No thinning.
  Runtime Skip (code): Dodgeball (DodgeballBall), Coconut, PracticeMale_Cutout_LOD0, CircuitNode_Board;
  Paddleball (Paddleball_Paddle), DodgeBall (the static ball mesh), CircuitNode_Board.
- 16:00 re-clean Dodgeball with -v dropc (the two 9x0.1x9 C boxes at (0,0.436,+-4.5), top 0.486 = 5 cm over the drawn
  court; the level's balls sit at y 0.55 = drawn floor 0.436 + r) -> Dodgeball.bytes 134339 B.

## Measured (scene coords = room-local, anchor 0)
Dodgeball (level11): gym x +-13.1, z +-18.1, ceiling C 12.52; gym floor y 0.356 (C tops 0.342/0.356), court drawn floor
y 0.436 (court C (0,0.398,0) 9.11x18.26, top 0.436). Lines (Paint_LightGrey plates at y 0.436): side x +-4.511 (outer
edge 4.557), back z +-9.074 (outer 9.129), attack z +-3.04, centre z 0 (0.176 wide), red bands z +-0.087..0.611.
Kill zones (G kill_zone boxes 10x15x20 / 30x15x10): x>4.6, x<-4.6, z>9.2, z<-9.2 (Whole Court 9.1 x 18.27).
Teams: Team1 = blue at -z: court spawns (-2.5|0|2.5, 0.486, -6) yaw 0; sideline (out) spawns (5.5, 0.356, -2|-3.25|-4.5)
yaw -90. Team2 = red at +z: court (-2.5|0|2.5, 0.486, 6) yaw 180; sideline (-5.5, 0.356, 2|3.25|4.5) yaw 90.
Pre-game: SpawnPoint1-8 team=pregame at x -6.4..-12, z -12.9..-17.1, y 0.356 (face the projector).
ScoreboardProjector (2.79, 0.356, -11.81) yaw 242.5 (-117.5). Leaderboard (9.97,0.36,-15.37).
Balls: P (DodgeballBall) (-2.5|0|2.5, 0.55, 0). Coconut bombs (Dodgebombs mod) at the same spots (skipped).
Barriers (2026 map change, CrashPad_Scaffolding): C boxes at (+-2.5, 0.733, +-9.875): x 0.65..4.35 each side of a
1.3 m gap, y 0.35..1.116, z 9.58..10.17. Practice standees: [PracticeMale_Cutout] (2.49,0.486,6.74) yaw 180,
(Standee_Female01) (-2.435,0.486,3.04) yaw 180, (Standee_Female02) lying at (0.08,0.92). Mods board (-6.7,1.79,-10.16).
8 ActivityDoor double doors: frames x -5.669/-3.417/3.309/5.579 at z -17.908 (yaw 0) and x -5.712/-3.411/3.26/5.62
at z 17.915 (yaw 180); signs over each pair. EXIT = (-5.669, 0.342, -17.908) yaw 0 (nearest the pre-game spawns).
End walls: window frames x -2.15..2.15 y 4.26..11.46.
Paddleball (level13): same gym shell 0.36 m lower (floor y 0.0, ceiling 12.16, walls x +-13.1 z +-18.1).
Court tube along z: drawn floor y 0.076 (C top 0.076), glass side walls x +-1.352 (C inner faces +-1.304), glass roof
y 2.25 (C ceiling bottom 2.375), end walls C inner face z +-6.95 (red goal paint x +-1.15, y 0.15..1.9 at z +-7.058);
Team1Goal (blue's, +z) / Team2Goal (red's, -z) trigger 2.7 x 2.05 x 0.39 at z +-6.96. Teams: Team1SpawnPoint blue
(0, 0.001, 5.6) yaw 180; Team2SpawnPoint red (0, 0.001, -5.77) yaw 0 -> BLUE IS +z here (the old room had blue at -z).
Balls: static DodgeBall meshes (0, 1.028, 0) and (0.591, 0.873, -4.29) (red's serve spot); ball spawners (dress) at
(-1.748,0.998,5.089) and (1.754,0.998,-5.034); paddle P (Paddleball_Paddle) (-0.125,0.204,-4.598); locker table (-1.81,-0.1,-4.23).
Spectators: SceneSpawnPoint x8 at x -0.7..-6.2, z -13.3..-15.3, y 0.001, facing +z. ScoreboardProjector (-4.17, 0, -0.09)
yaw 109.6. Teleport portal window in the west glass at z -5.77. Big C box over the court (0,4.92,0) 3.2x5.2x15.2 (blocker).
Doors: frames x -5.7277/-3.4197/3.3003/5.5913 at z -17.9026 (yaw 0), x -5.7037/-3.3597/3.2503/5.5853 at z 17.887 (yaw 180).
EXIT = (-3.4197, 0.0, -17.9026) yaw 0 (nearest the spectator spawns).
- 16:20 PADDLEBALL code: RrPaddleballRoom.cs builds level13 via RrJumbotronArea (Skip (Paddleball_Paddle), DodgeBall,
  CircuitNode_Board), stand-in hall/tube only when RrSceneBank.Text is null / parse fails. Court numbers are now instance
  properties (HalfWidth 1.30, Floor 0.076, Top 2.25, HalfLength 6.95; stand-in 1.5/0/3/6) used by RrPaddleballBall walls/
  goals; BlueEnd +1 in the level (Team1 blue at +z), -1 stand-in; EndZ(team)/TeamAtEnd() replace the old -z/+z signs
  (serve points, pads, paddle rests, auto-serve, hit direction, goal claim, goal fx). Markers: spawn team=blue/red -> pads,
  others -> lobby; scoreboard -> console. Portal (-3.42,0,-17.83) yaw 0. Boards: ends z +-7.75 y 3.8 (3.2 m) + far wall
  (0,5.6,17.85) 6 m. Goal frames (SandboxToolGoalLarge) stand-in only. Test serve aims at red's end. Compile: 0 errors.
- 16:45 DODGEBALL code: RrDodgeballRoom.cs builds level11 via RrJumbotronArea (Skip (DodgeballBall), Coconut,
  PracticeMale_Cutout_LOD0, CircuitNode_Board); stand-in shell/court/decor/barriers/cones only without the data. Rules
  numbers now instance fields from UseLayout(): court 4.557 x 9.129 half, floor 0.436, out past x 5.0 / z 9.6 (kill zones
  4.6/9.2 + 0.4, never before the pads' inner face 9.58), hall 13.1 x 18.1, y 0.356..12.5, barriers z 9.875 / x 4.35,
  balls x -2.5/0/2.5 at floor + r. ReadMarkers: spawn team=pregame -> lobby, team blue/red + "Sideline" -> bench, else
  court spawns (measured arrays as fallback). Console on (ScoreboardProjector) marker; portal (-5.669,0.342,-17.84) yaw 0;
  boards on end walls (0,5.8,+-17.95) 5 m; practice targets on the level's upright daytime standees. Compile: 0 errors.
- 17:00 boards moved onto the end walls' solid band (windows above y 4.26 / 3.9): Dodgeball (0,3.25,+-17.78) 4 m;
  Paddleball far wall (0,2.9,17.78) 4 m. Lobby return uses all lobby spawns. Dodgeball test hooks (throw/bounce) measure
  from room.CourtFloor; Paddleball test serve aims at red's end. Final compile: 0 errors (2 old warnings, not ours). DONE.

## Result
Files: Games/Dodgeball/RrDodgeballRoom.cs, RrDodgeballTest.cs; Games/Paddleball/RrPaddleballRoom.cs, RrPaddleballBall.cs,
RrPaddleballTest.cs; CODE/Data/Dodgeball.bytes (134339 B) + .meta, Paddleball.bytes (121020 B) + .meta;
DN/iter16/dp_tools/dp_clean.awk; work/ *_jt.rrsk, *_clean.rrsk, renders.
Rebuild the data: gtscene prep (above) -> awk -v door=-5.669,-17.908 -v 'dropc=C box 0 0.436 -4.5 0 0 0 1 9 0.1 9|C box 0 0.436 4.5 0 0 0 1 9 0.1 9'
-f dp_clean.awk Dodgeball_jt.rrsk Dodgeball_jt.rrsk > Dodgeball_clean.rrsk ; awk -v door=-3.4197,-17.9026 -f dp_clean.awk
Paddleball_jt.rrsk Paddleball_jt.rrsk > Paddleball_clean.rrsk ; rrsk2bytes.sh <clean> --name Dodgeball / Paddleball.
Integrator: run DN/tools/rrscene/bankscene.sh once (until then both rooms log "no scene data" and build the stand-ins).

## Open issues / GUESS
- One REC CENTER portal per room (the door nearest the lobby spawns); the level's other 7 doors are scenery and do nothing.
- Dodgeball out rule: feet past x +-5.0 / z +-9.6 (kill zones 4.6/9.2 + 0.4 m lean margin) - GUESS margin.
- Dodgeball mods board (PaintballModsBoard dress) and the leaderboard projector are scenery; no mods (LowGrav, Disco, Dodgebombs).
- Paddleball: blue now at +z (the level's Team1); paddle stands are drawn-only boxes inside the court ends (x +-0.9, z +-6.6);
  ball box x +-1.30, y 0.076..2.25, z +-6.95 (C ceiling is 2.375, the glass roof plate 2.25 - used the glass).
- GymGlass / court look depends on the kit's material picks; untested in the world.
- Scoreboard spots are ours (the levels have none besides the projector).
