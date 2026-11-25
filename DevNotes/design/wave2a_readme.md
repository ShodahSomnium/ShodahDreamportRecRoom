## Dorm Room (2026-09-24)

`Places/Dorm/`, namespace `...Places.Dorm`, registered by `RrRegistryDorm.cs` (hook `RegisterDorm`):
RrActivity.Dorm, "Dorm Room", Social, 1 player, Networked = false. Cover `illustration_HomeButton_DormRoom`, icon `Illustration_Menu_Dorm`. No background music, like Rec Room's dorm.

**What was built.** `RrDormRoom` rebuilds the 2018 "Spring Cleaning" dorm from Rec Room's own Maker-Pen kit in bundle 43ea1176.
- **Architecture:** 22 pieces: floor, skirting, 4 walls, loft walls, pillar, stripe, flat and angled ceilings, skylight, large and small windows, loft floor, beam, railing and shelves, plus the gold light shafts. Each root is placed at the inverse of its mesh child's local transform, which snaps the pieces back into the original room frame. Measured deviation from the research table: at most 10 mm.
- **Colours:** the orange `AG-MakerPenShape` surfaces are recoloured with `RrLook.Solid`: walls #A9BFD0, ceiling #C9D6E0, frames and shelves #F2F2F2, skirting #EDEDED, beam #6E4A30. The main floor uses Rec Room's `Wood_Floor_Matte_mat`. Architecture casts shadows on both sides, so the sun only comes in through the windows. Maker-Pen selection meshes are hidden.
- **Furniture:** bunk bed and desk under the big window, dresser under the loft, ladder in the railing gap, mirror facing the spawn, campus map, Code of Conduct, Block/Mute and Shake Hands posters, community board, 2 armchairs, 3 plants, art canvas on the loft.
- **Hoop:** placed on the pillar face at scale 0.94, so its rim lands on Rec Room's saved rim VFX position (3.905, 1.097, -1.090). Measured: 2 mm off.
- **Props (17, all through `RrTools.MakeTool`):** basketball, office bin, 2 root beers, water bottle, pizza slice, apple, desk lamp with a point light, radio, cup, dice, frisbee, bell, 2 books, Oscar trophy, paintbrush. Each is dropped 1 cm above its surface, and its home is set once it has settled.
- **Our own parts:**
  - `RrDormMirror`: planar reflection on the glass, 448 px, within 6.5 m; otherwise a stylised glass.
  - `RrDormBoard`: the Play board, with Rec Room cover-art tiles that travel to each activity.
  - `RrDormRadio`: Rec Room's 41-track playlist.
  - `RrDormLadder`: press, or VR grip, to climb up to or down from the loft.
  - `RrDormOutdoors`: campus, sky dome and clouds outside the windows.
  - MIRROR panel: mirror on/off, put things back, Rec Center.
  - `RrPortal` door to the Rec Center.
  - Lights: 3 point lights plus a warm "sun" spot light through the big window.

**Rec Room values used:**
- Room frame x -4.94..4.77, z -2.96..2.96; loft floor 2.75.
- Spawn (0.30, 0, 0.60) facing the mirror.
- Mirror glass 2.04 x 2.21 m.
- Ladder 2.81 m high.
- Bunk-bed desk top 0.85 m; dresser top 1.81 m.
- Radio: random first track, 1-30 m linear rolloff.
- Ambience `AmbianceSummerOutdoor`.
- ToolCleanup: 0.1 m / 30 degrees / 3 m / 15 s.

**Log lines to look for:**
- `Dorm: floor: Rec Room's Wood_Floor_Matte_mat (AG/Standard-Opaque)`
- `Dorm: hoop rim at room (3.905, 1.095, -1.091) ... off by 2 mm`
- `Dorm: play board: N activities`
- `Dorm: built at (x, z): 60 Rec Room objects, 17 props, max kit snap error 10 mm, 3.0 s` (with `; missing: ...` when a prefab was not found)
- `Dorm: radio: <clip> (<s> s)`
- `Dorm: ladder (press|grip): up to the loft`
- `Dorm: put N prop(s) back`

**Harness:** `scenarios/dorm_room.json`, with hooks `Places.Dorm.RrDormTest.Report / StandAtMirror / ShootBasket / ClimbLadder / RadioNext`. It produces 9 PNGs in `out/dorm_room/`.

**Untested:**
- A basket counted in the dorm. The scripted lob goes through the rim cleanly, but RrBall does not score it; the Interact module is asked to look.
- VR: ladder grip, poke and ray presses, and the mono mirror in stereo.
- Client-side clicks.
- Audio levels by ear.
- Radio auto-advance.
- Sitting in the armchairs (there is no RrSeat yet).

### Places/RecCenter: the Rec Center hub (2026-09-24)

**Built:** Rec Room's social hub, rebuilt from `rr_reccenter.md` Â§3 at the Rec Center instance origin (offline: (-3000, 0, 2000)). It registers through `RegisterRecCenter` and replaces the Rooms placeholder (Social, 1-40 players, cover `illustration_HomeButton_RecCenter`, icon `Illustration_Menu_RecCenter`, no global music).

What is in it:
- **Hall and lobby.** A 40 x 36 x 8 m hall and a 12 x 8 x 4 m lobby. `RrRecCenterShell` builds 319 boxes with metre UVs anchored in room space, so tiled textures continue across pieces. The floor, ceiling and walls are split into pieces of about 10 m, so each piece is lit by the point lights near it.
- **Materials.** Rec Room's own surface materials, made resident by loading containers `84`, `(Sandbox_Wall)`, `(Sandbox_Floor)` and the jukebox, plus `Wood_Horizontal_RedBrown_mat`: wood floors, blue wainscot with an orange stripe, off-white paint, carpet, linoleum, fluorescent panels, clear glass, orange I-beam paint, the exit sign and the Stay Hydrated poster.
- **Doors.** Every activity registered at build time gets `RrPortal.Create` at its Â§3.3 spot. The other spots show a "COMING SOON" placard. There is also the Dorm door and a "DELIGHT ME!" door to a random activity.
- **Props.** 128 Rec Room prefabs follow Â§3.4. They are placed from their renderer bounds, because pivots are often at the mesh centre. Furniture uses `Scenery()`, beams and pendants `Visual()`, and pick-ups `MakeTool`. The 14 shared toys get RrNetBody with LocalPropId 1-14: 4 basketballs, 2 dodgeballs, 2 ping-pong balls, 4 paddles and 2 cues.
- **Things that work:**
  - the reception bell (event 64);
  - the kiosk and cafe radios with Rec Room's 41-track playlist, and the jukebox with its 15-track list (`RrRecCenterRadio`: pressing steps to the next track, then off; the track lives in custom ints 0-2);
  - the pool table (`RrRecCenterPool`, see below);
  - the ping-pong paddle hit (`RrRcPingPongBall`);
  - the basketball hoop (Interact's RrHoop);
  - the vending machine, which drops a drinkable `(WaterBottle)`;
  - the Friend-O-Tron, which lists who is here;
  - 24 poker chips made from Rec Room's `PokerChip_LOD1` mesh;
  - the community board notes;
  - ambience loops.
- **Pool table.** 16 balls simulated on every client. A shot is event 66, carrying every ball's position and the struck ball's velocity. When the balls rest, the authority writes their positions to ints 4-19 with a version in int 3. Racks are request 67, repeated until the rack counter in int 20 moves, then event 68 to everyone. Cues are racked and kinematic until taken. Hold "use" to charge and release to shoot (desktop aims with the view, VR with the cue's tip); a VR cue also hits physically.

**Rec Room values used:** hall 40 x 36 x 8 m, lobby 12 x 8 x 4 m, and door, furniture and light positions from Â§3.3-3.5. Wainscot 0-1.2 m in Paint_Blue (0.218, 0.558, 0.840); stripe 1.2-1.35 m in orange (0.953, 0.477, 0.140). Plank tiling 1:4 (Rec Room's 10 x 40). Ring kiosk outer radius 2.4 m, inner 1.7 m, height 1.05 m, 1.2 m opening. Reception bell `ReceptionBell`, minTimeBetweenRings 0.25 s. Radio and jukebox playlists from `_rc_tracks.tsv`: the kiosk radio starts on Rec Room Theme, the cafe radio on Food Court, the jukebox on Lone Shoe Blues. Pool ball radius 0.0315 m, 8-ball and 9-ball rack orders, `Pool_CueHitsBall_01` and `Pool_ResetTable_01`. `VendingMachine_working` and `VendingMachine_drop`. `Kiosk_ButtonPressedNext` on presses. Ambience: `sfx_onboarding_outdoor_ambiance_local_people02`, `AmbianceSummerOutdoor`, `Kiosk_PowerUpAmbianceLoop`.

**Log lines to look for:**
- `RecCenter: surfaces: 13 Rec Room materials, 0 fallback colour(s), beam paint, exit sign, poster`
- `RecCenter: shell built: 319 pieces, Rec Room surfaces`
- `RecCenter: poker chips: 24 (Rec Room PokerChip_LOD1 mesh)`
- `RecCenter: built at (x, z) in 3.x s: N doors, M reserved, 128 prefabs (0 failed), 33 local tools, 14 shared props, 3 radios, pool 16 balls (...), hoop ready`
- `RecCenter: entered from <activity> (instance n, authority b)`
- `RecCenter: instance ints initialised`
- `RecCenter: <Kiosk Radio|Cafe Radio|Jukebox>: playing <title> (<clip>)`
- `RecCenter: reception bell rung by player n`
- `RecCenter: DELIGHT ME! door picked <activity>`
- Harness hooks (`RrRecCenterTest.Report/RingBell/PressRadios/PoolBreak/CueShot/PoolRack9/ShootHoop/Vend/FriendOTron`) log `RecCenter: test: ...`.

Harness: `scenarios/reccenter_tour.json` passes all 39 steps.

**Untested:**
- Two-client sync: radios, pool shots, rest layout and racks, the bell, the 14 net props, and late joiners.
- VR: the physical cue hit, ping-pong returns with a held paddle, and poke or trigger presses.
- Sitting: RrSeat is missing.
- Pool balls are procedural colours (Rec Room's `[PoolBall]` is not inside the table prefab), and the 8-ball and 9-ball buttons are procedural.
- Fronts still guessed: poker chair and lockers.
- Audio levels and 3D ranges, and the authority's advance to the next track when a song ends.
- The client frame time during the build.

## Paintball: a lobby, River and Sunset Drive-In, Capture the Flag and Team Battle (2026-09-24)

**What was built.** `Games/Paintball/` (namespace `...Games.Paintball`) registers Rec Room's Paintball through `RrRegistry.RegisterPaintball`:
- Category PvP, 2-8 players.
- Cover and icon `GiftBox_Paintball_Tex|8,276,380,268`.
- Arrival music `PaintballLobby_RiverRadio`.

One `RrPaintballRoom : RrGameRoom` builds three areas at the instance origin (offline k = 12, origin (-5000, 0, 6000)):
- **The lobby** at the origin: a plank deck in a forest clearing, with Blue and Red team pads, a console (RIVER / DRIVE-IN, CAPTURE THE FLAG / TEAM BATTLE, START), a scoreboard, lockers, two `(Radio)` props, a practice range with `(Paintball_Target_Sandbox)Variant` targets and pistol, rifle and shotgun practice racks, and the Rec Center door (`RrPortal`).
- **River** at room-local (0, 0, 450), from rr_shooters Â§10.1: an 84 x 48 m clearing split by a shallow river (bed -0.8, water -0.3, 1:3 banks), a 3 x 12 m bridge, stepping rocks, a fallen log, two wooden forts (10 x 7 m, 1.2 m walls with 1.5 m gaps, deck at 2.6 m with parapet slots, back ramp), mirrored mid cover and a flat-tree border.
- **Sunset Drive-In** at (0, 0, -450), from Â§10.2: the screen at x +33 with a 4 m sniper platform, 4 rows of `Car_50s`/`Car_80sSportsCar` with speakers, the snack bar with booths, stools, projector, AC units and the Debbie's Drive-In sign on a reachable roof, Blue at Frankie's Fuel (canopy, pumps, shop as the spawn room, pole sign), Red at the ticket booth (barrier arms, fence line, `Sign_Drivein_Redbase`), and the movie audio looping at the screen.

Build: 316 Rec Room props with 0 missing, 11 racks, 3.1 s in the editor.

Files and GUIDs:

| File | GUID | Role |
| --- | --- | --- |
| `RrPaintballRoom.cs` | 3e089fd5ea184289a384484fce94e883 | Rules, flow, spawns, votes, VO queue |
| `RrPaintballLobby.cs` | 1d85da85668946faa877b6c75adba122 | Lobby builder |
| `RrPaintballRiver.cs` | 0f319d79760749c0b7c3f9a276cf3ff7 | River builder |
| `RrPaintballDriveIn.cs` | d8411bbc57c14441bfeb58d7c89e5e97 | Drive-In builder |
| `RrPaintballMap.cs` | c1a12ffd283f4de7824aa4c13927e3a3 | Map data |
| `RrPaintballBuilder.cs` | 2d47fb5b7298424c86a51dfd3db24e9c | Grounded prop spawns, palette, tint |
| `RrPaintballFlags.cs` | 74d82e89e5b44b33a258c17ec6869206 | Capture the Flag |
| `RrPaintballRack.cs` | dc73d8435a7c415dbf64f1f96266d418 | Weapon racks |
| `RrPaintballMarkers.cs` | 98e23360518848b39926d59e447104a9 | Team gem and KO marker over members |
| `RrPaintballTest.cs` | 2a66eacc03ab495482e4ebd9d6bd7e6e | Harness hooks |
| `RrRegistryPaintball.cs` | 8534f57d00ed4d38afb04ec87473a0ca | Registry hook |

**Measured Rec Room values used.** From the PaintballCTF and PaintballTeamBattle configs and PaintballManager (rr_shooters Â§3.1, Â§4.1, Â§7):
- MaxHealth 1 (one hit is out), respawn 5 s, spawn invincibility 5 s.
- 2 teams x 4, GameStartDelay 10 s (the countdown), EndGameResultsDuration 15 s (round-over 4 s plus match-over 11 s).
- CTF: 3 captures or 600 s. Team Battle: 50 hits or 600 s. Friendly fire off.
- Pistol `(PaintballGun)` at spawn. `(PaintballAssaultRifle)`, `(PaintballShotgun)` and `(PaintballRifleScoped)` on racks that refill after 15 s.
- Flags `(FlagTool)` and `(FlagGoal)`, tinted Blue rgba(0.22,0.56,0.84) and Red rgba(1,0.45,0.25). Flag foot read from `Goal Attach Point` (-0.983). Stand attach at +0.3.
- VO:
  - Countdown: "Capture The Flag" / "Team Battle", "Blue Team" / "Red Team", 5up..1up, the PtblRiver_10Sec sting.
  - Flag events: FlagTaken, "Blue/Red flag taken/dropped/returned", FlagReturn01, ScoreSFX, Blueteamscores / Redteamscores.
  - Results: Blueteamwins2 / Redteamwins2 / Tiegame, MatchOver01.
  - Player out: "Out".
  - Consecutive VO lines are spaced 1.5 s apart (audioFXMinInterval).
- Music: RiverPredator and PtblSunset while playing; lobby radios PaintballLobby_RiverRadio / _SunsetRadio.
- Rack refill puff [VFX_ObjectSpawn]; out marker KO_Skull.
- Respawn at the team spawn farthest from opponents (the 2020 rule).

**Networking.** Phase, map, mode and scores are in the instance ints (authority only). The CTF flags use custom ints 0..11: per flag, state, holder id + 1, and the dropped position in room-local mm. Flag take, return and capture requests are `EvFlag` 64 to the authority, repeated every 0.3 s until the ints show them. Map and mode votes use GameRequest 16/17 and are repeated until seen. Rack pickups are `EvRack` 65. Outs are the shared kit's Death events; the authority counts Team Battle hits and drops a carried flag at the victim's feet, or when the carrier leaves or shows Down.

**Log lines to look for.**
- `Paintball: built the lobby, River and Sunset Drive-In: N Rec Room props (M missing), 11 racks, flags ready`
- `Paintball: entered: River, Capture the Flag, phase ...`
- `Paintball: joined the Blue team in the lobby`
- `Paintball: match starting: ...`
- `Paintball: spawned in the Blue base on River`
- `Paintball: Red flag taken by player N`
- `Paintball: Blue captured the Red flag`
- `Paintball: Red flag dropped by player N at ...`
- `Paintball: took the Shotgun from rack 3 (press|grip)`
- `Paintball: round over: Blue wins (3 - 1)`
- Harness hooks: `PaintballTest: ...`

**Verified.** `build.sh project --editor`: 0 errors, 0 warnings, all 113 members proven. Harness `scenarios/paintball_all.json`: 3 runs, 0 failed steps. It builds the room, takes 15 shots, plays a River CTF (fire, take, capture, rack, out and respawn) and a Drive-In Team Battle (3 hits).

**Not verified / risks.**
- Nothing tested with real remote players or in VR: flag grip, rack grip, left-hand carry, remote carrier display, team gems, KO skull.
- Clip names are not confirmed by ear.
- No out-of-bounds timer (invisible walls instead), no grenades or mines, no scope camera.
- Lobby votes are last-press-wins.
- The Drive-In 'sunset' is only warm point lights and an emissive screen; the world's sun is not changed.

## 3D Charades in The Ink Space (2026-09-24)

**What was built.** Games/Charades (namespace `SomniumSpace.Worlds.SomniumRecRoom.Games.Charades`). It is registered through `RrRegistry.RegisterCharades`: "3D Charades", category Party, 2-8 players, cover `GiftBox_Charades_Tex|8,276,380,268`, music MUS_BGM_3DCharades2b_Music.
- `RrCharadesRoom : RrGameRoom` builds the room.
  - Venue: Rec Room's whole venue `(Charades_InkSpace_MainHull_Sandbox)Variant` (key 84), spawned as scenery and lifted 3.24 m so the main hall floor is y = 0. Its 26 lights are kept with shadows off, and its own activity door is hidden.
  - Added pieces: `(Charades_Stage)` (key 23f) snapped onto the middle strip, three `(Sandbox_Couch)` on the lounge rugs, and an RrPortal to the Rec Center at the hull door's spot (-3.66, 1.74, -11.02).
  - Performer's podium: CORRECT!, SKIP, UNDO, CLEAR, 12 colour swatches, CO-OP CLASSIC / TEAM LIGHTNING start buttons, and a 2.6x word card.
  - Also: an RrScoreboard with a status line, a performer spotlight, and the timer loop.
- `RrCharadesInk`: 8-sided tubes sampled every 8 mm, quantised to 1 mm with the radius in 0.25 mm steps, drawn with the RrLook.Solid maker-pen paint.
- `RrCharadesPen`: drawing with Rec Room's `(CharadesPen)` (key cb9) through RrTools.MakeTool plus RrNetBody (prop 0).
- `RrCharadesCard`: the rebuilt 0.197 x 0.147 x 0.008 m card ("No Peeking!").
- `RrCharadesWords`: our own 275-word list.
- `RrCharadesTest`: harness hooks.

**Measured Rec Room values used.**
- Co-op Classic: 480 s game, 60 s per performer.
- Team Lightning: 4 team rounds of 90 s, 45 s per performer.
- Clips:
  - Charades_IndividualTimerLoop: 60.00 s, started at 60 minus the turn length.
  - Charades_TeamScoreUp (2.55 s): correct guess.
  - SFX_Failure_Stereo: time's up.
  - Charades_ButtonPress: every button.
  - sfx_ui_maker_pen_draw_tube_ribbon_loop_01: pen draw loop, volume 0.005 to 0.22 by draw speed.
  - sfx_ui_maker_pen_created_object_delete_01..05: undo.
- Effect: [VFX_Charades_Goal].
- Pen: ExtrusionPoint (0, 0.036, 0.152), lock on pickup.
- Screen tube size: 1.0 at rest down to 0.15 at 5 viewport/s.
- Announcer: "Blue Team" / "Red Team" on team assignment.

**GUESSES.**
- The co-op intermission lasts 4 s.
- SKIP deals a new word to the same performer, in both modes.
- The performer confirms a correct guess.
- The performer faces -x toward the lounge.
- The stage piece's top sits at 0.62 on the strip.
- The 12-colour palette is our own.

**Network.** State lives in the instance's custom ints 0-5 (performer and word; turn and word counters; the game-remaining ms at which drawing starts and at which the turn ends; the last result; rotation counters).
- Event 67: requests to the authority, repeated every 0.5 s until the state shows them.
- Event 64: ink points.
- Event 65: undo.
- Event 66: clear.
- Event 68: resend request.

**Log lines to look for.**
- `Charades: Ink Space hull spawned: bounds (21.48, 7.07, 23.91) ... 26 lights (shadows off)`
- `Charades: The Ink Space built: stage mark (2.00, 0.64, 4.00), pen ready, 275 words`
- `Charades: player N starts Co-op Classic (480 s, 60 s per performer)` / `... Team Lightning (4 x 90 s, 45 s per performer)`
- `Charades: turn N: performer P (Blue), 45 s, word #W`
- `Charades: you are the performer: "Duck"`
- `Charades: solved "House" (score 1)`
- `Charades: skipped "..."`
- `Charades: ink cleared for drawing K (...)`
- `Charades: asked the performer for drawing K` / `re-sending drawing K ...`
- `Charades: the performer left: next turn`

**Harness.** `bash SP/harness/run.sh SP/harness/scenarios/charades_play.json` passes 23 of 23 steps. `charades_lightning.json` passes 17 of 17. The build takes 2.9 s.

**Integration.** The server-like build has 0 errors and 0 warnings, with all 113 Fusion/Somnium members proven. The editor-like runtime and editor tools builds each have 0 errors.

**Not verified.**
- Two real clients: ink seen remotely, resend, pen sync, and request retries to a remote authority.
- VR drawing, pressure and poking the podium.
- Desktop drawing with the real mouse.
- Sounds by ear.
- The turn timeout, Lightning team-round switch and match-over in a full game.
- The cover art (every GiftBox_* name misses in RrCoverArt).

## Dodgeball and Paddleball (Games/Dodgeball, Games/Paddleball) (2026-09-24)

**Dodgeball.** Built:
- A 16 x 22 x 9 m gym (ours; the level is encrypted): 9 x 16 m wooden court, centre line, circle, attack lines, team zones, padded walls, windows, trusses, light panels and backboards.
- Team benches on risers, two end scoreboards with info strips, the REC CENTER door and a START button.
- Rec Room props: 3 x (DodgeballBall) on the centre line as networked tools, (Sandbox_Cone) at the corners, and (Standee_Male01) / (Standee_Female03) as practice targets.
- RrGameRoom flow: countdown 3 s, untimed rounds, round over 4 s, first to 3 rounds.
- Rules: a live throw hits and puts the player out (to the bench). Centre line or out of court is out. A catch puts the thrower out and brings back the catcher's first teammate out (CatchOutsThrower; Rec Room's own rule, catch = dead ball, is false).
- Offline alone it is practice.
- Net: custom ints 0 out mask, 1 flags | round, 2 count, 4..15 players (id+1 | team<<20). Events 64 hit claim, 65 catch claim, 66 line claim (to the authority, resent every 0.3 s until seen), 67 verdict. The authority holds a hit 150 ms so a catch of the same throw id wins. Hits are found by the thrower's client, sweeping the flying ball against the layer-27 hit volumes. The ball's kind int holds live | team | thrower.

Measured values used: ball radius 0.11, mass 1, bounciness 0.6, MaxThrowSpeed 15. Screen throw 14 m/s, charge 0.1-0.5 s, 7 degrees gravity compensation, aim assist 9 m / 30 degrees. defaultScoreToWin 3. Sounds and effects: Whistle01, Horn01, dodgeball_PlayerHit_01, [VFX_Default_HitParticles], Out + OutSFX, Hit + HitSound01, Catch01, [DodgeballFireTrail] while live, Bounce03 via CollisionAudio. Music DodgeBaller.

**Paddleball.** Built:
- A dark hall with a 3 x 3 x 12 m glass tube (layer 23), glowing edge rings, team pads, (SandboxToolGoalLarge) frames stretched over the ends, the gallery, 3 scoreboards, the door and START.
- 4 x (Paddleball_Paddle) (key 2d) on stands: networked, SolidWhileHeld, LockOnPickup, handed out at the countdown.
- The (TennisBall) scaled to r 0.1 as the zero-gravity ball: RrNetBody, contacts off, perfect wall reflection by our own code, at least 25 % of the speed along the tube.
- Hits are computed from the paddle's face box on the holder's client: v_out = reflect(v_ball - v_face, n) + v_face, then 5 % faster, 5-14 m/s, always toward the other end. The hitter takes the ball (touch-steal).
- Goals are claimed by the ball's controller (event 71, resent until the rally moves on). Point show: Goal VO + CrowdCheer01 + [VFX_Default_Goal], 2 s, then the conceder serves. The serve auto-launches after 5 s, or after 1 s from an empty side. First to 5, AmyVO_Generic_YouWin for the winners.
- Screen mode: a click swings the paddle (0.25 s) and returns a ball within 2.4 m along the crosshair.
- Net: custom ints 0 ball state, 1 server, 2 rally, 3 hits, 4 winner+1, 5 flags, 8..11 seats. Events 70 hit (sound), 71 goal claim, 72 goal.

Measured values used: paddle face 0.24 x 0.28 x 0.02 under a 1.25-scaled node, mass 10, LockOnPickup; ballScoredStateDuration 2 s; goals to win 5; RacketHit01, [VFX_Impact_Particles], TennisBall_Impact01a. Music SportFunk (GUESS).

Log lines to look for:
- `Dodgeball: gym built: court 9 x 16 m, 3 balls`
- `Dodgeball: round N playing: K players`
- `Dodgeball: ball i: first live throw`
- `Dodgeball: ball i hit player P ... claiming`
- `Dodgeball: caught ball`
- `Dodgeball: verdict hit|catch|line|left: out X`
- `Dodgeball: local player out: to the Blue bench`
- `Dodgeball: round N won by Blue: blue 1 - red 0`
- `Paddleball: court built: tube 3 x 3 x 12 m, 4 paddles`
- `Paddleball: paddle 0 given to the local player`
- `Paddleball: match playing: blue 1 v red 0`
- `Paddleball: first paddle hit: paddle 0 at 9.0 m/s`
- `Paddleball: goal for Blue: blue 1 - red 0`
- `Paddleball: automatic serve by Red`
- `Paddleball: ball stalled: serving again`

Harness scenarios: dodgeball_gym (20/20 steps) and paddleball_court (18/18); hooks are RrDodgeballTest.Start/Throw/Opponent and RrPaddleballTest.Start/Serve/WaitFlight/Concede.

Untested:
- anything networked (a second client)
- VR paddle face hits, screen swings and screen charged throws
- real catches, the line / out-of-court check, the bench teleport and the teammate coming back
- match over back to the lobby
- 2v2 seats
- sounds by ear
- no screenshot of the scoreboard info strip in the gym

## The quest framework and Quest for the Golden Trophy (2026-09-24)

**What was built.**
- `Games/Quests/Shared` (namespace `...Games.Quests`) is a reusable quest framework on top of `RrGameRoom`:
  - The lobby is `RrPhase.Lobby`. The start button starts the run; it never starts automatically.
  - The whole run is one `Playing` round, and the end screen is `MatchOver`.
  - The instance authority runs the act machine in the custom ints (`RrQuestInts`): Act, Stage (Intro, Combat or Clear), Wave, team Score, MaxPossible, Outcome, Party, Downs, Kills, Revives, BossHealth, BossPhase, Hazards and HazardTime.
- `Games/Quests/GoldenTrophy` (namespace `...Games.Quests.GoldenTrophy`) is the first quest built on it: a castle-courtyard dressing room and nine acts. It is registered through `RegisterGoldenTrophy`: category Quests, 1-4 players, cover and icon `Consumable_Polaroid_Goblin1_Tex(2048)`, music GoldenTrophyExplore.

| File | Role |
| --- | --- |
| Shared/RrQuestRoom.cs | Abstract quest room. It holds the act machine, gold (`AddPoints`), requests (`Ask`), and the down-penalty and fail checks. Every client moves its own player into each act, handles doors, music and stingers, and checks lava. It also provides the start button and the test hooks. |
| Shared/RrQuestDef.cs, RrQuestAct.cs, RrQuestWave.cs | Data: currency, penalty, music, enemy roster, weapons, and player rules; acts (origin, entry/checkpoint, exit door, area, spawn groups, lava, waves); waves (ActStart, PrevCleared, PrevBelow n, Enter zone, After s), scaled by party size. |
| Shared/RrQuestDirector.cs | The authority's wave runner: triggers, an interleaved spawn queue, and a MaxAlive cap. |
| Shared/RrQuestEnemies.cs | 30 enemy slots in RrNetActors blocks 0-1. The authority publishes; watchers follow with id = 1 + slot x 8 + serial. EnemyAttack and EnemyVolley are replayed so that each victim's client decides. Arrows stun basic goblins and imps, and a watcher's parry becomes a stun request. |
| Shared/RrQuestBreakables.cs | Rec Room pots through RrTools.MakeTool, with a broken bitset in block 2. The authority pays each pot once. |
| Shared/RrQuestRevive.cs | Down, bleed-out (30 s) back to the checkpoint, revive (2.5 m, stay within 1 m, Quest_Revive), and our own downed markers. |
| Shared/RrQuestHud.cs | Gold, HP, title card, toasts, prompt with bar, boss bar and floating numbers. A screen overlay on desktop; a world panel re-placed in VR. |
| Shared/RrQuestRack.cs, RrQuestGear.cs | Weapon racks that refill in 3 s, and remote players' held weapons from presence hand items. |
| Shared/RrQuestDoor.cs, RrQuestKit.cs | The wooden act door with a pass-through zone; kit placement grounded by bounds; walls with merlons, towers, torches, banners, lava pools, chests; the tiling pass with Rec Room stone and wood. |
| Shared/RrQuestEvents.cs, RrQuestInts.cs, RrQuestTest.cs | Event ids 64-69 with the wire helpers, the int layout, and the harness hooks. |
| GoldenTrophy/RrGoldenTrophyRoom.cs, RrGoldenTrophyLevel.cs | The definition, the lobby, the nine acts, the victory set piece, and TestPushBoulder. |
| GoldenTrophy/RrGoblinKing.cs, RrGoldenTrophyBoulder.cs | The boss, and the act-3 rolling boulder, which runs on the synced instance timer. |

**How a new quest uses it.**
1. Subclass `RrQuestRoom`.
2. `CreateDef()`: set the currency, the penalty and the music; add enemies with `AddEnemy("(QuestAIController)...")`; add weapons.
3. `BuildLevel()`: build the lobby. For each act, make a root with `RrBuild.Group`, set `act.Root`, `act.ExitDoor = RrQuestDoor.Create(...)`, add spawn groups with `act.Group(...)`, waves with `act.Wave(When).Add(type, count4, group)`, lava with `act.Lava.Add(RrQuestKit.LavaPool(...))`, and pots with `Pots.Add(...)`. Register racks in `Racks` and put the lobby's `StartButton`. Then call `Def.AddAct(act)`.
4. Optional hooks: `AuthorityActStep` and `AuthorityActDone` for bosses and puzzles; `AuthorityHazard` for levers (sent with `Ask(ReqHazard, code, arg)`); `OnEnemyReady` for boss bodies; `PointsFor` and `PointsKind`; `OnActStage` and `QuestStep` for what every client shows; `OnRunReset`; `BossName`.
5. For a boss: set `QuestInt BossHealth` (permille) and call `Enemies.Volley(...)` for scripted shots.

**Measured Rec Room values used.**
- Rule circuit fe6 [Quest_Goblin1_Manager]: Gold; death penalty 2000; win clip "Crimson Cauldron - Our Heroes Have Triumphed"; lose clip "Golden Trophy - Our Heroes Have Failed"; point effects [VFX_GoldCoins] / [VFX_BossGoldCoins]; coin sounds Quest_Coins_Spawn01 / Quest_BossCoins.
- Music: GoldenTrophyExplore, GoldenTrophyCombat2, GoldenTrophyClearFanfare2 (4.5 s).
- Spawners: GoblinBasicGroundMelee2020 (100 HP, 3 m/s), GoblinBasicFlyingRanged, GoblinEliteGroundMelee (200 HP), GoblinEliteFlyingRanged, and GoblinKnightCharge and GoblinKnightSpin (250 and 333 HP, charging 8 m at 12 m/s).
- Pots: FatPot 50, FatPot_Large 100.
- Revive: range 2.5 m, reviver stays within 1 m.
- Weapons: (QuestSword) 67-100 AI damage over 3-15 m/s; (Longbow) 17-67; (Crossbow).
- Kit: (Sandbox_FlatTreeLarge/Medium/Small), (Sandbox_Campfire), (LogPile_Sandbox)Variant, (Sandbox_StonePillar), (Sandbox_Barrel), (Hay_Bale_Sandbox)Variant, (LavaBoulder), (Golden_Bucket).
- Materials: Stones_Modular_Theater_Mat and Wood_Floor_Matte_mat, tiled.
- GUESS (research 3.4, 4.1): layouts, wave contents, party scaling, and the king's 3000 HP, three orbs every 4 s, and adds every 25 %.

**DEAD END, do not retry.**
- Water_Lava_Mat (AG/TerrainWater) loads but draws nothing on a box. The lava is our own glow with crust plates.
- Wall_Stone_Section_Theater_mat is an atlas whose wooden inserts repeat as brown patches when tiled. The walls use the flagstone material instead.
- "(Rock_01)Variant" is not a prefab name.

**Log lines to look for.**
- `Quest: kit ready: stone ..., floor Stones_Modular_Theater_Mat, wood Wood_Floor_Matte_mat`
- `Quest: textured 1080 boxes with Rec Room stone/wood`
- `Quest: Quest for the Golden Trophy built at (...): 9 acts, 7 enemy types, 46 pots (2900 Gold), 7 racks, N kit props`
- `Quest: run started with N player(s)`
- `Quest: act a/9 "ACT a"`, `Quest: local player into act a at (...)`, `Quest: act a wave w: n enemies (party p)`, `Quest: act a clear (gold g)`
- `Quest: boulder released by player id (16.0 s roll)`
- `Quest: player id is down: -2000 Gold`, `Quest: revived player id`, `Quest: bled out: respawned at the checkpoint`
- `Quest: the Goblin King rises: H HP (party p), 6.0 m tall`, `Quest: Goblin King phase k: n adds`
- `Quest: the Golden Trophy drops on the throne`
- `Quest: Quest for the Golden Trophy: VICTORY|FAILED in act a, g Gold of m (rating X), kills, downs, revives`
- `Quest: back in the lobby`

**Verified.**
- The harness scenarios goldentrophy_run, goldentrophy_acts and goldentrophy_final boot with 0 step failures and cover the lobby, the start, acts 1-4, 7 and 9, pots, the boulder, the boss, victory, failure and the lobby reset.
- All three builds compile with 0 errors and 0 warnings. The server API check reports all 113 members proven.

**Still open / Not verified.**
- Everything networked: actor sync, attack replay, requests, revive, remote gear, hand-over and late joiners.
- The VR HUD and VR revive.
- The last visual tweaks: lava colour, act 3's goblins in the boulder lane, and the rock prefab name.
- Bow draw, pot breaking by projectiles, NavMesh, the king's retargeting and shield stun, potions, and the 4 m / 1 s VR teleport limit.

