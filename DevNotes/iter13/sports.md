# iter13 - sports (Soccer + Bowling, basic form)

## Plan
1. Read Dodgeball/Paddleball rooms, RrGameRoom, registry, RrSceneBank, research rules.
2. Soccer: Games/Soccer/* - RegisterSoccer, room from Soccer.rrsk (data file), teams, networked ball, goals, timer, kickoff.
3. Bowling: Games/Bowling/* - RegisterBowling, room from BowlingAlley.rrsk (data file), lanes, pins, balls, 10-frame scoring, pinsetter.
4. Compile check (0 errors), notes + shared requests.

## Log
- 14:18 read framework (RrGameRoom, Paddleball, Dodgeball ball, console, net body); ran gtscene jt=1 on level72/level91 -> NSP/sports/{Soccer,Bowling}_jt.rrsk (454 KB / 237 KB gz)
- 16:05 RESUMED (context lost). Re-read framework; measured markers in the jt rrsk (below). Design fixed:

## Measured (scene coords = room-local, anchor 0)
Soccer (level72): field glass x +-22, goal mouths z +-47.06; trigger_zone Team1Goal (0,2.238,-47.81) 20x4.8x4.5 (blue's
goal, -z), Team2Goal (0,2.11,47.83); goalbox movement blockers (0,-0.15,+-40.9) 21.46x10x6.79; ball P (SoccerBall)
(0,5.18,0) s0.65; team spawns Team1 (blue) (-0.01,-27.85) (2.15,-4.32) (-1.63,-5.41), Team2 (red) (0.05,28.03) (-2.15,4.45)
(1.6,5.36); pregame lobby x -24..-34 z -4..1.4 (SpawnPoint1-8); ScoreboardProjector (-24.93,-0.05,2.97); exit door
frame (-38.67,-0.04,0.35) yaw 90; big end panels (0.2,14.68,+-52.2) 12.3x9.35.
Bowling (level91): lanes x -2.9 / 0 (scoring) / 2.93, glossy lane plates z 7.85..-19.25 (1.776 m wide, top y 0.127);
foul line z 7.85 (approach to z 11.85), pins sandbox x1.5 at y 0.13, head pin z -16.501, rows -16.89/-17.28/-17.67,
0.225 lateral; back wall z -18.8; 14 balls at z 17.874 y 0.751 x -7.2..6.96 (rack); projector (-5.83,0.13,11.69);
exit door (0,0.37,35.39) yaw 180; overhead monitors x +-3.84 y 3.8-6.4 z 7.9-9.3; pin C boxes+capsules stripped at convert.

## Design
Files: Games/Soccer/{RrRegistry.Soccer.cs, RrSoccerRoom.cs, RrSoccerBall.cs}; Games/Bowling/{RrRegistry.Bowling.cs,
RrBowlingRoom.cs, RrBowlingLane.cs, RrBowlingBall.cs, RrBowlingScore.cs}. Level via RrJumbotronArea (public, reused as LaserTag).
Data: CODE/Data/Soccer.bytes (Soccer_jt as is, kit Skip (SoccerBall), ActivityDoor*), Bowling.bytes (pin colliders cut by
DN/iter13/sports_tools/bowl_clean.awk; kit Skip BowlingAlley_Pin).
Soccer: 2 teams, 180 s, most goals (draw ok), ball net prop 0 (no pickup), kicks = local body/teleport/hand contact
(LocalTouch + velocity), goals claimed by the simulating client (Paddleball pattern), 3 s then re-drop at (0,5.18,0).
Events 90-92. Bowling: FFA 1-6, scoring lane x0 networked (5 balls net props 0-4, pins local bodies: roller simulates +
streams EvPins, result mask to authority, sheet in custom ints), practice lanes local only. Events 93-97.
- 16:12 DATA done: CODE/Data/Soccer.bytes 453 KB (NSP/sports/Soccer_jt.rrsk as is), CODE/Data/Bowling.bytes 235 KB
  (awk -f DN/iter13/sports_tools/bowl_clean.awk Bowling_jt.rrsk Bowling_jt.rrsk > Bowling_clean.rrsk: 96 pin colliders of 33 pins cut).
  jt source: bash DN/tools/gtscene/run.sh prep NSP/apkwork level72 Soccer <out> jt=1 (level91 Bowling likewise). Next: code.
- 16:35 SOCCER code written: Games/Soccer/RrRegistry.Soccer.cs, RrSoccerRoom.cs, RrSoccerBall.cs; compile: 0 errors in
  Soccer (6 errors only in the lasertag agent's in-progress files). Goal mouth measured z +-44.25 (goal box 20 x 4.7 x 5.6 deep).
  Next: Bowling.
- 17:05 BOWLING code written: Games/Bowling/RrRegistry.Bowling.cs, RrBowlingRoom.cs, RrBowlingLane.cs, RrBowlingBall.cs,
  RrBowlingScore.cs. Compile 0 errors (whole project). Next: review pass (scoring unit check offline, .meta files?, notes).
- 17:20 scoring rules checked offline (NSP/sports/scoretest: perfect 300, all 5-spares 150, gutter 0, nines 90, classic 167,
  10th-frame X72 / X7/ / 7/X, Position() consistent) - all OK. Added harness hooks RrSoccerTest / RrBowlingTest.
  Portal heights from the level's door frames (Soccer y -0.04, Bowling y 0.373 on the raised lounge). Compile 0 errors.

## Result (basic form)
SOCCER (Games/Soccer): RrRegistry.Soccer.cs (Sports, 1-6, SportFunk GUESS, GiftBox_Soccer_Tex), RrSoccerRoom.cs,
RrSoccerBall.cs, RrSoccerTest.cs. Level72 via RrJumbotronArea (Data/Soccer.bytes). Lobby = pre-game hall (SpawnPoint1-8),
hologram at the level's projector, REC CENTER portal at the level's door. START (alone = practice) -> countdown 5 s on
team spawns -> ball (SoccerBall, net prop 0) dropped from (0,5.18,0) -> 180 s, goals in the level's 20 m goal zones
(claimed by the simulating client, event 91 -> authority 92), 3 s, re-drop; most goals wins / Tiegame; goal boxes closed
to players while playing; boards on the end panels. Kicks: body (8-20 m/s by approach), teleport-in (20), VR hand
(x1.5), screen click (16). Sounds: Soccer_HitBallHeavy/Light, Soccer_BallHitsFloor/Environment, Soccer_Goal01, Goal +
team VO, SR_Buzzer_Long01, [VFX_Default_Goal].
BOWLING (Games/Bowling): RrRegistry.Bowling.cs (Sports, 1-6, Bowling_MusicGameplay, SpawnableToolTex_BOWLING_PIN),
RrBowlingRoom.cs, RrBowlingLane.cs, RrBowlingBall.cs, RrBowlingScore.cs, RrBowlingTest.cs. Level91 via RrJumbotronArea
(Data/Bowling.bytes, kit skips BowlingAlley_Pin + ActivityDoor*, drops the 3 pin-blocker stand-ins). Scoring lane x 0
(4 BowlingBall net props 0-3 from the level's shelf; rolled balls come back onto the ball return at x -1.44), practice
lanes x -2.9 / 2.93 (4 local balls each). 30 SandboxBowlingPin on the level's own pin markers. Roll: ball crossing the
foul line (z 7.85) on its simulating client -> EvRollStart 93 -> authority (only the current bowler in a match) ->
roller simulates pins + streams EvPins 95 at 10 Hz -> settle -> EvRollResult 94 -> authority scores (10 frames, strikes,
spares, 10th-frame bonus), next rack, 3.2 s machine cycle (sweeper bar, Bowling_Sweeper01) -> next roll / bowler. Only
the current bowler can pick up scoring balls. Sheet on the overhead monitors (TMP mspace), console status line.
Sounds: Bowling_BallRoll02 rumble (speed^2), Gutter01, BackboardThud01, Thud01/MegaThud01, MegaStrike01, CrowdCheer01,
SR_Crowd_Aww_01, SFX_Collision_Wood1_Mono pin clatter (GUESS), AttentionWhistle on your turn, AmyVO_Generic_YouWin.

## Requests for the integrator
1. Run DN/tools/rrscene/bankscene.sh once so RrMain's scene data carries Data/Soccer.bytes and Data/Bowling.bytes
   (without them both rooms build a plain stand-in court/alley and log "no scene data").
2. No shared-file edits needed (registry hooks are partial methods; RrActivity already has both).

## Open issues / GUESS
- Soccer speed-boost orbs (yellow, final rules) not built (no markers in level72; research GUESS numbers: 4 orbs, 5 s x1.6).
- Kick speeds are GUESS (bundle min/maxLocomotionAttackForce 20 read as 20 m/s top speed).
- Bowling: ball shelf poses are the level's (kinematic until picked up); pin clatter clip GUESS; practice lanes are
  per-client (not shared); screen bowling = release rolls flat along the view at >= 8 m/s (no charge).
- Untested in play mode (per instructions): first tour should watch the logs "Soccer: field built", "Bowling: alley
  built", "Bowling: pin spots from the level: 10 / 10 / 10", "pin (SandboxBowlingPin): ... m" (expect ~0.55 m tall).
- 17:30 fix: a stale non-bowler roll during a match keeps the rack and the turn. Final compile 0 errors. DONE (basic form).

## Harness steps for the tour (integrator)
{"goto": "Soccer", "timeout": 180}
{"expect_log": "Soccer: field built", "timeout": 10}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Soccer.RrSoccerTest.Start"}
{"expect_log": "SoccerTest: start: phase Playing", "timeout": 15}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Soccer.RrSoccerTest.Shoot"}
{"expect_log": "Soccer: goal for Blue", "timeout": 8}
{"shot": "soccer_field", "pos": [0, 6, -40], "look": [0, 2, 0], "frame": "room", "fov": 80, "w": 1280, "h": 720}
{"goto": "Bowling", "timeout": 180}
{"expect_log": "Bowling: alley built", "timeout": 10}
{"shot": "bowling_lanes", "pos": [0, 1.7, 12.5], "look": [0, 0.5, -16.5], "frame": "room", "fov": 70, "w": 1280, "h": 720}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Bowling.RrBowlingTest.Start"}
{"expect_log": "BowlingTest: start: phase Playing", "timeout": 15}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Bowling.RrBowlingTest.Roll"}
{"expect_log": "pins settled", "timeout": 25}
{"expect_log": "BowlingTest: roll", "timeout": 25}
