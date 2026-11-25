# Rec Room party games: 3D Charades, Stunt Runner, Rec Rally, Make It To Midnight, My Little Monsters, Punch Up

Status: COMPLETE (2026-09-24, run 2). Sections: 0 summary, 1-6 games (web facts, bundle map, rebuild plan), 7 shared systems + networking, 8 order/effort, 9 open questions.

## 0. Summary and conventions

- `RR` = `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad\rrtool\bin\out\rrtool.exe`.
  Bundles live in `E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64\<bundle>.bundle`.
- **BIG** = bundle `43ea1176ca25c0333547813ec3298808` (302 MB, 38,150 objects, 793 prefab roots, 642 container keys).
  It is Rec Room's "sandbox/kit/activity" bundle and holds most of what this document needs (Charades venue, pen,
  manager; Stunt Runner kit + manager; rally buggy and cars; punching bag).
- "container `xyz`" = Addressables key: `bundle.LoadAsset<GameObject>("xyz")` works (see `rr_runtime_facts.md`). A
  prefab WITHOUT a container key can only be obtained by loading a container asset that references it and then
  `Resources.FindObjectsOfTypeAll<GameObject>()` by name (implicit assets become resident).
- Obscured (ACTk) values were decoded with `scratchpad\party\deobs.pl` (F: float, I: int, B: bool).
- Helper scripts (Git Bash): `scratchpad\party\d.sh <bundle> <pathId>` (decoded MB dump), `party\ft.sh` (tree + all MB
  fields), `party\geo.sh` (tree with mesh/material/box/audio per node), `research\mesh.sh <bundle> <meshIds..>` (mesh
  AABB), `research\partyq\clipinfo.sh` (clip length). Raw dumps saved in `scratchpad\party\*.txt` and
  `research\partyq\*`.
- Web sources: Rec Room fandom wiki via MediaWiki API (saved wikitext: `research\party_wiki\*.txt`), wiki News
  Archive 2020, pocketgamer. recroom.com now only shows an "under construction" page (a search snippet says Rec Room
  shut down 2026-06-01; unverified).

| Game | What it really is | Bundle support | Rebuild difficulty |
| --- | --- | --- | --- |
| 3D Charades | 2-8 players; one "performer" draws the word on the card in 3D with the Charades Pen on a stage, others guess before the timer ends. Co-op Classic / Team Lightning. | **Excellent**: whole Ink Space venue as one sandbox prefab (mesh + collision + lights + posters), stage, pen, card, manager config (timings), sounds, 2 music tracks. | Low-medium (3D ink drawing is the only real system) |
| Stunt Runner | 1-4 player obstacle-course race (parkour game show), 6 courses, 3 min each | Kit pieces (walls, floor, ceiling, checkpoint arch, boost can, end button, jumbotron), manager with full announcer/crowd audio config, boost manager | Medium-high (courses must be authored; parkour locomotion) |
| Rec Rally | 6-player off-road buggy race, 3 laps, nitro cans, drift lever | Rally buggy vehicle prefab (+50s car, truck, wolf), rally boost pickup, race HUD | High (driving physics + a whole off-road track) |
| Make It To Midnight | 4v1 asymmetric horror at a night-time fairground (Dead-by-Daylight-like), 2023 | Bonky's squeaky hammer (= Paintball SplatHammer, mesh `Weapon_BonkyHammer`) with inflate/deflate/swing/hit sounds, clown costume dummy, fair food, standees, heartbeat/spooky ambience; no map, generators or cages | High, map and rules procedural/own |
| My Little Monsters | 2024 creature-care roleplay (Cryptid Creek, Sasquatch/Mothman/Chupacabra), made in Rooms 2.0 | Only MLM clothing/spray-paint skins and dango food; no monsters, sanctuary or town | Very high; recommend a reduced "pet" mini-game |
| Punch Up | Never-released 2016 boxing Original (easter egg only) | Wobbly punching-bag tool (physics joints), punch sounds, boxing bell `DingDing`; no glove mesh | Low if designed as our own boxing mini-game |

## 1. 3D Charades

### 1.1 What it is (web)
- Rec Room Original since 2016-07-06, 2-8 players, all platforms. The performer draws with the **Charades Pen** (a
  Maker Pen variant) either the word on a drawn **card** or anything they like; the rest shout guesses (voice chat)
  before time runs out. Since 2020-09 the venue is **The Ink Space**, a "hip underground cafe" (brick, stage, couches,
  cafe tables, posters, open-mic sign-up), also a clonable base room. Older venue: a theatre ("Piano Lounge" music).
- Modes (config values from the game-rule wrappers, BIG):

| Mode | prefab (container) | fields |
| --- | --- | --- |
| Co-op Classic (everyone one team, count rounds solved) | `(CharadesCoOpClassic_GameRuleWrapperCircuit)Variant` #-5917197373697256347 (container `7ab`), MB `CharadesCoOpClassicGameRuleWrapper` #-4456258768987909019 | `defaultGameDuration` **480 s**, `defaultRoundPerformerDuration` **60 s** |
| Team Lightning (red vs blue, alternating team rounds) | `(CharadesTeamLightning_GameRuleWrapperCircuit)Variant` #-8838409188716673770 (container `977`), MB #-7992648687159442154 | `defaultTeamRoundDuration` **90 s**, `defaultMaxRoundCount` **4**, `defaultPerformerDuration` **45 s** |

- Both point `gameRulePrefab` -> `CharadesManager` on `[CharadesManager]` #553067014653342377 (no container key;
  resident once `7ab`/`977` is loaded).
- 2020 rule changes (news archive): longer drawing time (1 min co-op, 45 s Team Lightning); guessers can no longer grab
  the drawing when it is not their turn; screen players got "Draw Mode" (finger painting with a cursor), a shortcut
  menu of shapes/colours, trigger-sensitive tube size (controller), speed-dependent tube size (mouse/touch).
- What makes it fun: the absurd 3D doodles, the time pressure (ticking loop), the social shouting; drawing in 3D lets
  the performer walk around the sculpture. The card is private to the performer ("No Peeking!").

### 1.2 Flow we implement (from wiki + manager fields; details marked GUESS)
1. Lobby: players sit/stand in the audience area. Press the start button (Charades push button) -> pick mode.
2. Co-op Classic: performers rotate (everyone gets a turn - 2016 change). Performer walks on stage, gets the Charades
   Pen and a card (random word; hidden text "No Peeking!" for others), `timerTickLoopClip` plays (60 s clip exactly
   matches the 60 s performer time). Guessers shout; when someone guesses, the performer presses the **success
   button** (GUESS: the performer judges, as in the original) -> `successClip` Charades_TeamScoreUp + `[VFX_Charades_Goal]`
   particles; score "Round" +1 (`scoreName` = "Round"); next performer. Timeout -> `timerExpiredClip`
   SFX_Failure_Stereo, next performer. Game ends at 480 s.
3. Team Lightning: players join red/blue (VO "Red Team"/"Blue Team" on join). Each team round lasts 90 s; performers
   of the active team draw 45 s each, as many words as possible; 4 rounds total (2 per team, GUESS); most words wins.
4. Drawings are cleared at the end of each performer turn (GUESS: the original removes the ink on turn end).
- Locomotion: `CharadesManager.customInGameRole` overrides nothing (all override flags 0) -> normal movement.

### 1.3 Bundle content (all BIG unless noted)
Venue and set pieces:

| Asset | pathId | container | Notes |
| --- | --- | --- | --- |
| `(Charades_InkSpace_MainHull_Sandbox)Variant` | -3838970831184783737 | `84` | **The whole Ink Space room**: `Charades_Full_fbx` (mesh `Charades_LoungeVenue` #-8454148288694563682, 4,446 verts, 9 submeshes, AABB centre (0,-0.09,0) extents (10.41,3.54,11.50) -> 20.8 x 7.1 x 23.0 m; materials Wood_Floor_Matte, Brick_Red, Paint_Dusk, Charades_Carpet_Taupe, Wood_Floor_Faded_Matte, Paint_Warm_Grey x2, Paint_White, Plaster_Black), `Collision` (40 BoxColliders), `Lighting` (WarmGlow/Ceiling/Bounce/Spot lights - Light components come through), `Props` (fluorescent lights, pipes, I-beams, 3 `[StageLight_Can]`, cafe shelves, pendant lights, bulletin boards, 3 `[Charades_Rug]`, neon root-beer sign, ~30 Charades posters, `[Exit_Sign]`, `[Charades_Ink_Space_Sign]`), 4 `[Poster_OpenMic_Signup]`, `[ActivityDoor]`, 4 `[Chair_Cafe_Round]`. Full node list with transforms: `scratchpad\party\inkspace_geo.txt` |
| `(Charades_Stage)` | 3323210107304673527 | `23f` | `Charades_Floor_fbx` mesh `Charades_Stage` #-3701533869647177527 (390 verts, 2 submeshes, extents (1.85,0.196,5.01) -> 3.7 x 0.39 x 10.0 m), 4 BoxColliders (main 9.99 x 0.39 x 2.55 rotated -90 deg yaw + curved apron pieces), `Spawn Point` (0,0.43,0) |
| `[Charades_Ink_Space_Sign]` / mesh `Charades_Ink_Space_Sign` | 3581481857921783431 / -8999161673394944130 | (inside hull) | 4.7 x 2.0 m sign; tex `Charades_Ink_Space_Sign_Tex` #6113426056027828488, normal #295480876459229630, mat #8339487763803142468 |
| Posters `Charades_Poster_01..06` (Charades, Jazz, DJ, Comedy, Punk, RootBeer) | meshes -201365395995154286, 7402262529267607698, 1247014125657204882, -4956330108758710126, -8671992479366043502, -5928661061495150446 | (inside hull) | tiny quads (0.02 x 0.026 m) scaled x27-65 in the hull; atlas `Charades_Posters_Tex` #-5487349647553081824, mat `Charades_Posters_Mat` #-1009955095550796608 |
| Push button mesh `Charades_Button` / `Charades_Button_Base` | 4413723384518494692 / -7875137590744036892 | used by `(Sandbox_Room_Boost_Button)` container `11` and other creator buttons | 0.30 m cap (72 verts) on a 0.35 m base; `PhysicsButtonVisual` + Rigidbody; material `Button_Charades_Mat` #5687506969800493664 (tex `Button_Charades_tex` #-5913843984674215641). Press sound `Charades_ButtonPress` |
| `[CharadesCard]` | -3295324125890308427 | none (GUESS: not reachable at runtime - rebuild) | card mesh `Card` #-5114949774247464399 (0.197 x 0.147 x 0.008 m, `Paint_GoldenOrange_Mat`), white quad, TMP text `Text` (Futura Bold Oblique SDF orange), `Card` MB: `hiddenWordString` "No Peeking!", April Fools text. Pickup: one hand, no lock |
| `[VFX_Charades_Goal]` | 8869808536301172773 | none | success burst ParticleSystem + child `Fragments`; swap Sprites/Default materials (runtime facts) |
| `[CharadesManager]` | 553067014653342377 | none (resident via `7ab`/`977`) | MB `CharadesManager` #-4657640698747555159 |

Charades Pen:

| Asset | pathId | Notes |
| --- | --- | --- |
| `(CharadesPen)` (container `cb9`) | -5340755430698854321 | layer 10; `MakerPen_fbx` (Animator, SkinnedMeshRenderer `MakerPen`, joints `Extruder_Jnt`, `Trigger_Jnt`), skins inactive, `Colliders/Body`, `Handle` boxes, `ExtrusionPoint` at local (0,0.036,0.152) (ink comes out here), `MenuAnchor` (0,0.078,0.028) + `ToolMenu` (projected watch-style menu), `LaserPointer` LineRenderer. `Tool.interactionSettings`: LockOnPickup 1, grip pos (0,0.0204,0.0393) rot (0.272,0,0,0.962). Material `CharadesPen_mat` #554421989246855844, tex `CharadesPen_Tex` #1269782042441153796 |
| `CharadesPen` MB | 415520019412801615 | `defaultColor` 0, nonImposterObjects list |
| `MakerPenVisuals` | 5512128799453993039 | `laserMaxDistanceDefault` 50, `laserVisualMaxDistance` 15 |
| `MakerPenAudio` -> `MakerPenAudioContent` | bundle `1df8e0efac4f496ebc58d7e6f86480c3` (86 MB) #2089211229727463353 | tube draw loop `sfx_ui_maker_pen_draw_tube_ribbon_loop_01` #-8388756256425894136 (volume by draw speed, curve 0.005->0.22 over 0..1), shape loops `..._draw_primitive_loop_slow_01` #1211891414961548223 / `_medium_01` #53076536822396677 / `_idle_01` #1335313407463815255, create `sfx_ui_prop_spawn_01..04` (#3590727226276041322, #622733502832470028, #-865684849236943307, #-4203138781948389954; vol 0.275), delete `sfx_ui_maker_pen_created_object_delete_01..05` (vol 0.06), clone `sfx_ui_object_clone_01..05`, fade in 0.2 s / out 0.08 s |
| `CharadesPenScreenToolBehaviorConfig` | -2713611485604520906 | screen-mode extrusion size vs cursor speed curve: 1.0 at 0 -> 0.15 at 5 viewport/s; Apple-pencil pressure 0.3->1.0 |
| Pen menu `(CharadesPenMenuMainScreen)` | bundle `31e5fcc2b247152af23999e35e3c10eb` #-5974488891697480681 (container `d`) | shape buttons **Tube, Cube, Sphere, Cylinder, Pyramid** (sprites `MakerPen_Sprites_Tube/Cube/Sphere/Cylinder/Pyramid` in bundle f124b399...), **Undo, Redo, Scale**, material buttons **Cardboard, Neon, Shiny Metal**, 12 colour swatches (colours set at runtime - use our own 12-colour palette, GUESS), button colour rgba(0.396,0.447,0.588) |
| Ink material `AG-MakerPenShape` | -224598426847956790 (shader `AG/MakerPenShape` in bundle b1a0b840467d50b0fbe1fbc1413be7b7 #4012107730349444667; batched variant #8741355202197176085) | _Smoothness 1, _Metallic 0, _BumpScale 0.4; no DepthNormals pass -> set renderQueue 2501 (runtime facts). No container found referencing it -> GUESS use our own URP Lit ink material if it is not resident |

Sounds / music:

| Clip | bundle | pathId | length | used for |
| --- | --- | --- | --- | --- |
| Charades_TeamScoreUp | BIG | -111350679483700582 | 2.55 s | `successClip` |
| Charades_IndividualTimerLoop | BIG | -4051645749183987204 | 60.00 s | `timerTickLoopClip` (performer timer) |
| Charades_ButtonPress | BIG | -5550952510476749650 | 0.27 s | push button |
| Blue Team / Red Team | BIG | -1273163103982826951 / -8532761083206627114 | 1.5 s each | team join VO |
| SFX_Failure_Stereo | 70f410f60ab29de333e1275fd09f8d94 | 5094249946107902258 | 2.37 s | `timerExpiredClip` |
| MUS_BGM_3DCharades2_Music ("Charades (Piano Lounge)") | d386dc983204960ad15e2fc743982995 (container `6`) | 2769843072205654133 | 202 s | lobby music (legacy venue) |
| MUS_BGM_3DCharades2b_Music ("Charades (Ink Space)") | 7b3ac31f46677fb36cd0869cf05a4cb0 (container `a`) | 1945390999528493133 | 146 s | Ink Space music |

SFXTrack names come from BIG MBs #7327717298605630684 / #7330000565235234929.

### 1.4 Ink Space layout (hull-local coordinates, computed from the collision boxes)
- Entrance: `[ActivityDoor]` at (-3.66,-1.50,-11.02) at the top of a stair (collision steps at x=-3.3, z=-10.4 ->
  -6.6, tops y -1.54 -> -2.41) that descends into the **main hall**: floor box 21.2 x 15.2 m, top y = -3.24, spanning
  x -10..+11, z -3.6..+11.6; walls at x=+-10.2 and z=+11.3, ceiling underside y = +2.96. Six posts on the long walls
  (z=-2.9 and z=10.8), two pillars at x=-1.6 (z=1.8, 6.1).
- Raised platform on the **-x side**: box 11.0 x 15.2 m, top y -2.59 (x -10.1..0.9), with low benches/step boxes
  (Cube 6-9, tops -2.47) and the three `[Charades_Rug]`s (x -8.8, z 0.9/4.0/7.0) -> the lounge/audience area.
- Raised strip at x 0.4..3.6 along z -3.7..11.6 (five 3.2 x 0.39 x 3.65 boxes, tops -2.62, end pieces tilted),
  with three `[StageLight_Can]` hanging above at x=1.3 (z 0, 4.3, 8.1) -> **stage** (GUESS: the performer stands here
  facing -x toward the lounge). The separate `(Charades_Stage)` piece (10 m long along its local z after its -90 deg
  collision yaw) fits this strip; place its root at hull-local (2.0, -3.0, 4.0) with yaw 0 so its 0.39 m top lands at
  y -2.62 like the strip collision (GUESS - verify visually; the hull strip collision may already be the stage's).
- +x side (x 3.6..10) = cafe: `[Chair_Cafe_Round]`s at (8.4..9.0, -3.23/-2.21, 9.6..10.4), bulletin boards and open-mic
  posters on the back wall z=11.1, neon root-beer sign and cafe shelves on the -x wall (x=-10.1).
- Sign "The Ink Space" above the entrance wall at (-3.64,1.78,-3.50).

### 1.5 Rebuild plan (3D Charades)
1. Instance: load BIG, `LoadAsset("84")` (hull) and `LoadAsset("23f")` (stage); keep original materials, apply the
   depth-priming fix; convert Light components to our lighting budget (or keep and bake - hull has ~25 lights; GUESS
   keep only the 3 stage cans + warm glows realtime). Hull collision boxes -> our walkable layer.
2. Game controller (our code) with state networked per `somnium_networking.md` section 5: `mode`, `phase`
   (lobby/performing/intermission/end), `performerId`, `wordIndex`, `phaseEndTime`, `score[2]`, `round`. Authority =
   elected instance owner. Timings from the wrapper fields above (480/60 co-op, 90/45/4 lightning).
3. Word cards: our own word list (Rec Room's list is server-side / not in bundles). Card = rebuilt from `Card` mesh or a
   procedural 0.197 x 0.147 slab + TMP text; only the performer's client shows the word, others see "No Peeking!".
4. Charades Pen: instantiate `(CharadesPen)` (container `cb9`) for its visuals (strip missing scripts), attach our
   `InkPen` component: while trigger held, sample `ExtrusionPoint` every ~1 cm, build a tube mesh (8-sided, radius
   0.5-2.5 cm from trigger pressure / screen cursor-speed curve above), colour from a 12-colour palette, shapes
   (cube/sphere/cylinder/pyramid primitives) placed with one click; undo/redo stack; clear on turn end. Audio: tube
   ribbon loop with volume = draw speed, prop_spawn clip for shapes, delete clips for undo.
   Networking: stroke = list of points quantised to 1 mm in stage space + colour + radius, broadcast as "Ink" event
   batches (section 7.3), receivers rebuild meshes; late joiners get the turn's strokes via Hello/Census. Only the
   performer may draw (2020 rule: guessers cannot grab the drawing).
5. Buttons: `Charades_Button` mesh pair as physical push buttons (start game, "Got it!", "Skip") on a podium at the
   stage edge; press -> `Charades_ButtonPress`. "Got it!" -> success clip + `[VFX_Charades_Goal]` + score.
6. Timer display: our own TMP world-space timer + a large wall scoreboard (Rec Room's scoreboard projector is a
   missing-script UI, rebuild). Ticking loop clip 60 s starts at performer start (co-op); for 45 s turns start it at
   offset 15 s so it ends with the timer.
7. Music: Ink Space track (`7b3ac31f...` key `a`) looped low in lobby, ducked during performance.
8. Controls: VR - grab pen with grip, draw with trigger (analog = tube size), menu button on pen for shape/colour;
   Non-XR - equip pen, hold left mouse to draw at a depth plane in front of the camera (screen "finger painting":
   ray from cursor to a virtual canvas plane 1.5 m in front of the performer, scroll wheel = depth), number keys 1-5
   shapes, `[` / `]` colour cycle, Z undo, C clear (all free keys, `somnium_conventions.md` 11.1; F/E/R/Q/Tab are
   client-owned).

## 2. Stunt Runner

### 2.1 What it is (web)
- Rec Room Original, 2019-09-05, 1-4 players, "obstacle-based extreme sports game show": contestants sprint, climb,
  wall-run, mantle, slide and dodge hazards; lowest time wins. Six courses of rising difficulty: **1 Anticipation,
  2 Daring, 3 Patience, 4 Caution, 5 Trust, 6 Perseverance**. Everyone starts together; max **3 min** per course, then
  everyone is moved to the next. Rank from the total of the six times: S+ (< 4:45 total), S, A, B, C (thresholds other
  than S+ unknown - GUESS: S < 5:30, A < 6:30, B < 8:00, else C).
- Features: rope-grid climbing (VR: pull yourself hand over hand; screen: touch the wall and move on it), wall running
  (jump onto a wall while moving; jump off at ~20 deg), sliding (crouch while sprinting; builds speed on slopes),
  KO Cola boost cans (stackable), moving platforms that carry you, bounce pads, red "caution" blocks that KO you back
  to the last checkpoint (course 4). Sprint does nothing in Stunt Runner (only boosts/slides add speed).
- Community tricks: "Mega jump" (jump onto the end of a wall and immediately jump off -> boosted jump), long jump off
  the end of a wall, high jump by pulling back while on a wall. Course 3 has a moving-platform maze; course 2 split
  paths + two sets of bounce pads; course 1 barriers with boosts and a wall-run section.
- Setting (bundle names): a TV **sound stage** (Env_SoundStage pillars/beams, crash pads, shiny scaffolding, marquee
  light strips, chevrons, commentator booth, jumbotron, player-following spotlights), announcer + live crowd audio.
  Fun: speed-running flow, the announcer/crowd reacting to you, beating friends' times.

### 2.2 Game config (BIG)

| Object | pathId | container | key fields |
| --- | --- | --- | --- |
| `(StuntRunner_GameRuleWrapperCircuit)Variant` / MB `StuntRunnerGameRuleWrapper` | 991735120586144402 / 7989349537586870930 | `528` | `defaultMaxCourseTime` **180**, music/announcer/ambience/spotlights enabled; `gameRulePrefab` -> `[StuntRunner_Manager]`; `autoApplyConfigurationOnConnect` -> `StuntRunner_Config` |
| `StuntRunner_Config` (GameConfigurationAsset) | -3926239554402303909 | - | min 1 player, **4 teams of 1** (every runner is his own team), `GameStartDelay` **10 s**, `EndGameResultsDuration` **15 s**, respawn on game end, no join-in-progress, stats Score, Finished Course, Course Time, Total Time, CompletedCourseTime1..6 |
| `[StuntRunner_Manager]` / MB `StuntRunnerManager` | -4814964379236605330 / -1719512394218773906 | (resident via `528`) | `courseMaxBaseScore` 180000, `winGameOverDuration` 5, `loseGameOverDuration` 6, `timeBetweenRaceStartAndGameplayMusic` 1.5 s, all audio of 2.4. Decoded dump: `research\partyq\sr_manager_dec.txt` |
| `[StuntRunner_BoostManager]` / `StuntRunnerBoostManager` | 6890113733487264461 / 8051364640295977677 | (resident via `da`) | `boostedSprintSpeedMultiplier` **1.45**, `boostedCruisingTime` **2 s** (per can, stacks), accel rate 1/s, decel rate 0.5/s, no air impulse (would be 10 m/s, 20 % up), `boostFireSfx` Locomotion_KOCola_Initiate01 vol 0.7 |
| `[StuntRunnerRoomBase]` / `StuntRunnerGameplayRoom` | 2013680972158487495 / 3366734675259247559 | - | scoreboard/leaderboard slots empty (room-authored) |

Locomotion constants (Rec Room's cross-platform locomotion, `CrossPlatformPlayerLocomotionContent`, bundle
`c513202af4f2b6439095734ab5caead0` #-5920908264689802500; decoded JSON `scratchpad\party\loco_content.json`). We
implement these ourselves in a Stunt Runner movement add-on (the Somnium avatar is not modified; see 2.5):

| Group | values |
| --- | --- |
| walk / sprint | walk max 3 m/s, accel 0.15 s; sprint x1.5, accel 0.225 s; step 0.25 m; terminal fall 20 m/s; air control 50 %; air drag 2, ground drag 10 |
| crouch / slide | crouch height 0.725, speed x0.75; slide height 0.55, min duration 1.5 s, max speed = 2 x sprint, air control 20 %, steering 0.8, min slope 8 deg for gravity sliding, gravity lateral/forward accel 1.6 s / 3 s, abrupt stop against walls > 45 deg from normal |
| wall run | duration 10 s, drop-rate parameter 0.6, max angle from wall tangent 60 deg, max fall speed 12 m/s, start vertical speed <= 0.85 x jump speed, start boost 0.4 x jump speed, wall-jump height 0.85 m, direction parameter 0.35, air control x0.2 |
| wall climb (rope grid) | max speed 0.66 m/s (screen), accel 0.0825 s, jump along wall 1.75 m in 0.5 s, jump off wall 5 m/s forward + 0.33 m up, VR manual climb jump: hand speed 1.5..8 m/s -> 2..4.75 m/s |
| clamber / mantle | min speed 3 m/s, ledge probe heights 0.66/1.33/2.0/2.66 m, min ledge 0.25 m, restitution short/med/tall clamber 0.75/0.5/0.25, mantle 0.6/0.4/0.2 |
| jump | jump cutoff height x0.5 (release early = lower jump), impulse air control 10 % |

Locomotion feedback (`CrossPlatformPlayerLocomotionFeedbackContent`, bundle `1df8e0efac4f496ebc58d7e6f86480c3`
#-1508725186499743944; `scratchpad\party\loco_feedback.json`): slide start = Locomotion_WallRun_Attach01 vol 0.35,
slide loop Locomotion_Sliding_SlowLoop01 vol 0.65 (fade 0.4 s), wall-run attach/detach/loop vol 0.2/0.22/0.65, climb
grab vol 0.2, sprint wind (RecRoyale_Glider_WindGust01) + SR_SonicBoom_01, slide sparks `(Sliding_Sparks_VFX)`
#-1158909881532379430, climb VFX `(Climbing_VFX)` (1df8e0ef key `a48`), first-person wind lines
`(Wind_FirstPerson_VFX)` #8014671684417917966 (emission x8 sprint, x20 slide), `(Wind_FirstPerson_SlideStart_VFX)`
#6758693801866178961, `(Wind_FirstPerson_Boost_VFX)` #-2837915202752460965 (all in 1df8e0ef).
Locomotion clips with keys (bundle 1df8e0ef): WallRun_Attach01 #4977359329881650060 `cc4`, WallRun_Detach01
#696440810051722626 `a91`, WallRun_Running01 #-8036678031433869105 `c3`, Sliding_SlowLoop01 #-8522786083536121570
`f56d`, Climbing_GrabWall01 #-2280638467403087580 `ef8`, SR_SonicBoom_01 #1150801257012532740 `b8a`;
Locomotion_Clamber_03 (c513202a #5720396251687053548); footsteps Locomotion_Footstep_Tile01/02/03 (5aeb4370 `4`,
3b004927 `c`, 1fe270da `6`); boost Locomotion_Boost_Initiate02/Looping01 (8 s)/Misfire01/Spent01 and
Locomotion_KOCola_Initiate01 (BIG #-6491063285510159844, #-4379981765045079288, #6764848017567955167,
#2953076619741688355, #-5645152267747482837).

### 2.3 Kit prefabs (BIG; all have container keys)

| Prefab | pathId | key | size / content |
| --- | --- | --- | --- |
| `(StuntRunner_Floor)` | 4539472656680002431 | `2d4` | sound-stage floor 98.4 x 1 x 114.5 m (`Plastic_VerColor_CrashPads_Mat`), root authored at (-0.545,-0.3,3.664) |
| `(StuntRunner_Ceiling)` | 3688575249911266730 | `b6` | ceiling 98.4 x 114.5 m at y 35.11 + 8 wooden beams, "Key Fill" light |
| `(StuntRunner_Wall_Long)` | -4722935107846299007 | `f76` | wall 112.5 x 34.4 x 1.5 m with 6 pillars + pipes |
| `(StuntRunner_Wall_Short)` | -1019735043916253826 | `12` | wall 98.4 x 34.4 x 1.5 m with pillars |
| `(StartingGate_Sandbox_Invention)Variant` / `StuntRunnerStartingGate` | -6018589947144721808 / -9207691963189712272 | `9c9` | 1,639 nodes: scaffolding tower, marquee lights, chevrons, crash-pad scaffolds, **4 start gates** (4 Animators), 2 `[Light_RaceStart]` (5-stop countdown light: 0.1/0.3/0.5/0.7/0.9 with 0.8 s delays), 6 start VFX, `Player1..4SpawnPoint` at local (3.24/1.19/-0.81/-2.84, -0.21, 2.9) - lanes 2.03 m apart |
| `(EndingGate_Sandbox_Invention)Variant` / `StuntRunnerEndingGate` | -2742104978754823211 / -866986578510930987 | `53` | 1,817 nodes: finish arch, `Button_Podium` 3.1 x 0.14 x 3.25 m at (6.6,4.38,-0.13), `[Button_StuntRunnerEnd]`, `[LeaderboardProjector]`, 11 end VFX, `SpawnPoint` |
| `(Checkpoint_Scaffolding_Sandbox_Invention)Variant` | -3137407743591503543 | `0b4` | checkpoint arch ~13 m wide (scaffold towers at x -6.9/+6.1) with marquee lights across the top (y 3.4), TMP label, SpawnPoint |
| `(Checkpoint_Sandbox)` / `SandboxCheckpoint` | 57576463870705273 / 418793746009669241 | `17` | 1 m trigger cube (`CheckpointOverlapVolume` localSize 1) linked to a respawn point |
| `(Button_StuntRunnerEnd_Sandbox)` / `StuntRunnerButton` | 3625077860156271739 / -8848032797446804357 | `026` | finish button: `Button_Stuntrunner_LOD0` #-6089926443751241188 (0.47 m dia) + base; press sound Charades_ButtonPress; `instanceLimitDuration` 0.5 |
| `(Stuntrunner_Pickup_Sandbox)` / `BoostCollectible` | -7327621520255117006 / 4390597719606335794 | `da` | **KO Cola**: can ~0.2 m dia x 0.47 m (visual root scale 0.4) with outline + ring, spins 50 + 150 deg/s; `respawnTime` 5 s, scale-up 0.25 s, pickup SFX SR_KOCola_Collect01..03 vol 0.6, VFX `[StuntRunner_Pickup_VFX]` #5491310025194905856, refs boost manager |
| `(Bounce_Pad_Sandbox)` / `SandboxBouncePad` + `PlayerLauncher` | 7938829513301719828 / -601850994485396716, 2384593175775181588 | `81d` | velocity x1 + 5 m/s additive, max 8 (launcher max 10), air control 25 %, launch only from the top face, direction +Y |
| `(Impulse_Volume_Sandbox)` | 2201342964844761542 | `427` | same parameters, any face |
| `(SpringBoard_2x2_Sandbox)` | -1767378013996874261 | `dea` | 2 x 2 m springboard (base + springs meshes); sound SR_SpringBoard_Bounce01 (551144ad..., key `0`) |
| `(Rope_Net_Sandbox)` | 9179670166180098550 | `ce3` | 1 x 1 m rope-net tile (`Rope_Net_LOD0` #-6024061567578700654, 468 verts) = climbable surface |
| Crash pads | 6512868755316705115 / 2682908206513992767 / 8231482341944254460 / 7207626475150278643 | `2cb` / `aa8` / `aa7` / `199` | 1x2 thick (2.0 x 0.44 x 1.0 m), 1x2 thin (0.2 m), 1x1.5 extra thick (0.64 m), scaffolding tower 1.18 x 3.0 x 1.18 |
| Scaffolding | -128401667918523939 / 4692617179430033430 | `aae` / `af6` | shiny truss 0.49 x 0.49 x 1 m; block 1.09 x 0.22 x 0.21 m |
| Lights | 8436873244865663797 / -2930573430630659911 / 7222686395682522939 | `c83` / `c2` / `53e` | chevron arrow (5.1 x 3.5 m mesh), marquee 1 m bulb strip, light strip 0.5 m |
| `(StuntRunnerJumbotron_Makerpen)` | 5375484431712432478 | `1` | 14.2 x 12.3 x 6.3 m jumbotron (`StuntrunnerJumbotron` #1307961088125904443) on scaffolding, 3 spotlights |
| `(CommentatorBooth_Sandbox_Invention)Variant` | 5949274184905286456 | `9b9` | commentator booth (10 x 4.6 m front) with stage lights |
| `[StageLight_PlayerFollowing]` | -5229972498005014349 | - (via manager) | spotlight that follows a player |
| Loot chests | bundle a816ab167d7fb1b28066a841475b81e9 #-3061100035516136514, #3721382388939383954 | - | `(LootChest_Stuntrunner_..._AddressablePrefabSpawner)` (reward chest; optional) |

Full per-node dumps: `research\partyq\geo_<pathId>.txt`.

### 2.4 Audio (length; bundle8; pathId; container key if any)
- Announcer countdown: `SR_Announcer_StartThree01` 2.67 s (23a0d18f #4778510324469871753 `1`), `StartTwo01` 2.72 s
  (73662ddc #-1954118503817730974 `d`), `StartOne01` 2.70 s (d50327e2 #837662083403697195 `f`), `GetReady01` 7.09 s
  (158e7753 #-3518353551257379666 `f`), `Go01` 3.27 s (52cac00a #-2035893172335327474 `4`).
- Time warnings (BIG): OneMinuteRemaining01 #-9113955684142618141, ThirtySecondsRemaining01 #-1659547524144203664,
  TenSecondsRemaining01 #5981280078990013162, FinishFive01 #7880682583335908424, FinishFour01 #3231088646983369899,
  FinishThree01 #46558714461823471, FinishTwo01 #4127904208282200156, FinishOne01 #-5013458752234807298.
- Finish VO by percentile of the max course time: <= 25 %: Amazing01 (eea4b81f #-5494293293969867525 `4`) /
  Incredible01 #5333507040288918788 / Outstanding01 #-8148515101593399114; <= 66.7 %: Adequate01 (7468b20c
  #1549312777495707930 `3`) / NiceGoing01 #-1667797987420325372; else NiceTry01 #-7992585431669211122 / SoClose01
  #-6515509941883882001. Taunts on falls: AlmostHadIt01 #7138985541017383776, FeelThatInTheMorning01
  #-8070413214347885895, Ooh01 #4243859324708247206, ThatsGottaHurt01/02 #-2152732628715877657/#-5778285684980189744,
  WatchOut01 #-4685189970749217592.
- Crowd: SR_Audience_Cheer02/03/04 (BIG #2157570899536906946; 6cfb34be #3175557120754433836 `f`; BIG
  #-8244167036036898340; vol 0.65) for top 66.7 %, else laughs SR_CrowdReactions_Laugh02 (5342d97d `0`) / Laugh03;
  reactions Ahh01/02, Aww_01 (b4e61aca `d`), Laughter_01/02, NewBestOoo01 ("NEW PERSONAL BEST!" 3 s).
- Stingers/SFX: SR_Flyover01 8.5 s (pre-race), SR_GateDown_02 3.7 s (race start, vol 0.8), SR_ButtonPressed_01
  (finish), SR_Buzzer_Long01 (time out), SR_Buzzer_Short01 (412631213... `5`, player out), SR_Fail01 (local time out),
  SR_CourseClear01 4.7 s ("Course Complete! / Advancing to next course..." 4 s), SR_CountdownTick 0.92 s (64442b9f
  `4`), SR_CountdownGO (a82aea4e `b`), SR_Collectible_Collect01 (118c2a5e `d`), SR_Water_BigSplash03 (ffc46ab6 `3`).
- Music: loading `SR_Fanfare01` 11.9 s (BIG #7109352028673205774), gameplay `SR_GameOn01` 78 s (330ba010 `6`),
  `SR_FinalLap01_NoSilence` 77 s (26a5f0a8 `6`), `SR_DadRunner` 96 s (eb3e5db6 `1`), post-run `SR_PostRun01` 48 s
  (a7e68bf6 `9`). Notifications: "FINISHED! Time: {0}" 6 s, "You got an S+/S/A/B/C rank!" 5 s, "Run Complete!",
  "Collect your prize!", lose text "You'll get 'em next time!".
- Full list with lengths: `research\partyq\sr_clips.tsv`. BIG clips without a key become resident after loading
  `528` (manager) or `da` (pickup).

### 2.5 Rebuild plan (Stunt Runner)
1. Arena per instance: sound-stage shell from `2d4`/`b6`/`f76`/`12` (98 x 114 x 35 m). Start gate (`9c9`), courses
   inside the shell, checkpoint arch (`0b4`) + our trigger volume per section, finish gate (`53`) with the finish
   button, jumbotron (`1`) showing times, commentator booth (`9b9`) as decoration.
2. Six courses authored by us (official geometry is in the encrypted levels): kit crash pads as platforms,
   scaffolding, rope nets as climb walls, springboards/bounce pads, KO Cola cans, chevrons for direction, red blocks
   for course 4 "Caution", animated moving platforms for course 3 "Patience", plus procedural boxes/ramps using
   `Plastic_VerColor_CrashPads_Mat`. Target 30-50 s per good run so the S+ total (< 4:45) is reachable. Store each
   course as a JSON list of kit placements (key, position, rotation, scale) so later agents can author without scenes.
3. Movement: the Bridge has no "add velocity" call (`somnium_player.md` 6.1), so follow `somnium_player.md` 6.4:
   while on a course use the **Quake takeover** (freeze per 6.2, disable the client's CharacterController, drive
   `Body.Root` with our own capsule controller; restore on leave/OnDisable). Our controller implements the 2.2
   constants: walk 3 m/s (sprint key ignored - Stunt Runner has no sprint), wall-run (side sphere-casts, 10 s max, drop
   rate 0.6, wall jump 0.85 m), slide (Non-XR: C while moving - C is free; VR: click right stick; >= 1.5 s, up to x2 speed,
   gravity on slopes > 8 deg), clamber (ledge probes 0.66/1.33/2.0/2.66 m, min speed 3 m/s), boost x1.45 for 2 s per
   can, bounce pads (+5 m/s, max 8-10), moving platforms (add platform delta). Climbing: VR = grip on rope-net colliders
   (hand delta -> body delta, or the Bridge `SomniumClimbableSurface`, `somnium_player.md` 6.4); Non-XR = touching a
   net sticks you to it and WASD moves along it at 0.66 m/s. The avatar itself is never modified.
4. Race flow (authority = elected instance owner): lobby at the start gate -> 10 s start delay -> SR_Flyover01 ->
   3-2-1 announcer + race lights -> gates drop (SR_GateDown_02) -> music after 1.5 s -> checkpoints (respawn at last on
   fall/KO; kill plane -> SR_Buzzer_Short01 + taunt VO) -> finish button (time, percentile VO + crowd) -> all finished
   or 180 s -> "Course Complete!" -> teleport everyone to the next course -> after course 6: total time, rank
   notification, leaderboard (our networked store, best total per player). Networked ints: phase, course index,
   course start time, per-player checkpoint index and finish time (ms).
5. `[StageLight_PlayerFollowing]` spotlights on the leader (optional).

## 3. Rec Rally

### 3.1 What it is (web)
- Rec Room Original, 2021-09-29: **six-player off-road rally race** ("boost, bump and powerslide your way to the
  finish") on one course in **Pioneer Peak**, Rec Room's "dusty southwestern countryside". Not a kart game with
  weapons: the only items are **nitro gas cans** (collect 3 -> one boost). The Rally Buggy (3 seats: driver,
  passenger, standing back seat) was released as a Maker Pen palette vehicle at the same time; other drivable
  vehicles: Trusty Pickup Truck, Classic Coupe (50s car), Wolf Ride.
- Rules: 3 laps (~2-3 min per lap); a lap counts when crossing the start/finish line; the race auto-ends after a
  time limit. Players are locked in their seats during the start countdown; you cannot grab other players' buggies
  during a race. Top 3 are placed on a podium after the race, others spawn in the spawn area. Rewards: Settler set
  (random), red/blue racing sets at the merch booth.
- Driving: enter by grabbing a door or the trunk. VR: grab the wheel and steer like a real wheel ("motion") or with
  the stick ("joystick" mode); drift = grab the lever right of the driver seat (handbrake); boost = hit the Boost
  button on the dash (or click the left stick); exit = A/X. Screen (PC): **W/A/S/D steer + throttle/brake, Left Shift
  drift, Space boost, Esc exit**; gamepad: left stick, X/A drift, L3 boost, Circle/B exit. The vehicle can be picked up
  to unstick it (reset handles).
- Course (wiki walk-through): dirt road -> two turns -> hill up to a big **red barn** (hay, cut-out chickens and horse,
  boosts, parked vehicles; you can drive through the barn) -> small jump -> right to a **very steep hill** (slow without
  boost) -> huge descent with a turn (many boosts) -> short rocky climbs -> small **tunnel** -> view of a **bridge**,
  cross it -> narrow **drift section** marked with big red/white arrows -> small hill -> **railway**: turn right and
  drive along the tracks; two exits: an early one through a **cave full of crates**, or a later turn to a **big jump**;
  after the jump either go straight + curve to the finish, or follow the **river** to a hidden jump that lands next to
  the finish.
- Fun: physics bumping between six buggies, drift + boost timing, shortcuts, passengers riding along.

### 3.2 Bundle content (BIG unless noted)
Vehicle `(Vehicle_RallyBuggy)` #4118328164326650835, container **`20d`** (full node dump `scratchpad\party\buggy_geo.txt`,
MB fields `scratchpad\party\buggy_mb.txt`):

| Part | values |
| --- | --- |
| Rigidbody | mass **1000**, drag 0.1, angular drag 0.8, interpolate, continuous collision |
| Body | skinned `GoKart_Med_Skinned_LOD0..3` (#-2304406829568148885 LOD0: 11,140 verts, 3 submeshes, 2.6 x 3.7 x 4.0 m incl. flag pole); joints `Kart_Body_Jnt`, `Kart_Boost_Jnt`, `Kart_Brake_Jnt` (e-brake lever `GoKart_EBrake`), `Kart_FlagBase_Jnt` (2.36 m flag), `Kart_SteeringWheel_Jnt`, 4 `Kart_Tire_*_Jnt` |
| Seats | `[Seat_NoUGC] Driver` (-0.364,0.771,-0.156), `Passenger` (0.302,0.771,-0.109), `Back` standing (0,0.976,-1.164) (chassis space); each with `CPSpawn` (0,0.54,0) and door highlight `GoKart_Door_Highlight` |
| Collision | `[BuggyBodyCollision]` ~30 boxes (floorboard, doors, seats, truck bed, roll cage bars, wheel guards), `SkidPlate` 1.25 x 0.25 x 3.1 box + capsules, `MakerPenBounds` 2.6 x 2.05 x 4.03 |
| Controls | `[SteeringWheelRoot]` at (-0.374,1.17,0.469) with `GoKart_SteeringWheel` mesh + 0.38 m collider; handbrake lever default/engaged transforms at (-0.089,0.676,-0.015); boost button collider at `Kart_Boost_Jnt` (0.1 m) with default/engaged (pushed 3.1 cm); gear indicators D/N/R; speedometer + boost gauges (`GoKart_NeedleMesh`) |
| `GroundVehicle` #6996602249152246739 | baseVehicleDrag 0.1, floor detection 5 m @ 20 Hz, grounded angular drag 0.8, airborne 2, angular-drag multiplier curve (x1 up to 90 deg/s, x7.1 at 123, x10 at 280), damage force x100 |
| `VehicleEngine` #-6053207765397720109 | maxTorque **3000** (curve 1.0 -> 0.525 at max RPM), maxRPM **700**, reverse torque 4500, max reverse RPM 200 |
| `WheelDifferential` #-1051001572436134957 | maxSteeringAngle **35 deg**, front axle steers, both axles drive |
| `Wheel` x4 | radius **0.46 m**, all drive + handbrake, front steer; tire friction 1.7, long./lat. friction 2, traction over slip curve (0:0, 0.1:1, 1:0.7), handbrake long. x0.9 / lat. x0.306, braking torque 1500, cornering stiffness 0.2 |
| `WheelSuspension` x4 | at (+-1, 0.524, 0) on axles at z +1.246/-1.274 (wheelbase 2.52 m, track 2.0 m), max extension **0.6 m**, rest compression 40 %, damping ratio 0.8, max damping force 3000 |
| `VehicleBoost` #-7616250954263743533 | capacity 3, cost 3 (3 cans = 1 boost), throttle x1.25, intro 0.25 s, active **3 s**, outro 0.25 s, torque +35 %, stacks |
| `VehicleSteeringAssist` #6767212749790131155 | drift assist above 5 m/s with throttle and steer >= 0.9 (friction x1 -> x0.3 over 1 s), drift boost: sideslip >= 10 deg, torque x3.2 after the drift |
| Audio | engine start `sfx_rec_rally_utv_enginestart_rev_01` #-644347506254163657, idle loop #-8175687931211277736, stop #1048757527632956039, acceleration loop `_acceleration_loop_09` #3743412075217253155 (throttle cap 14 m/s); tires rolling dirt #-8072095034557070396 / fast #-8594367905331256023 (to 24 m/s), sliding #-7851349196959404910 (3..11 m/s); suspension light 05/06, medium 01-03; body impacts chassis 01-03 / dynamic 01-02; e-brake engaged/disengaged #-1277838059013687811/#-6328733666754585907, tire squeal #8402463317589644940; boost Locomotion_Boost_Initiate02/Looping01/Spent01/Misfire01 (all BIG, resident with `20d`) |
| VFX | `[Vehicle_SlippingDust_VFX]`, `[Vehicle_TireDirt_VFX]` per rear wheel, `[Vehicle_CollisionSparks_VFX]`, `[Vehicle_CollisionDust_VFX]` roots in BIG |

Other drivable vehicles (same component set): `(Vehicle_50sCar)` #-3993029803350148515 key `4ec` (Car_50s 2.4 x 1.6 x 4.9 m),
`(Vehicle_Truck)` #1041960009261110536 key `8f` (Car_Truck 2.5 x 1.6 x 4.3 m), `(Vehicle_Wolf)` #-481421869805313682 key `57d`.
Static props: `(Car_50s_Sandbox)Variant` `ba`, `(Car_80sSportsCar_Sandbox)Variant` `c19` (2.2 x 1.3 x 4.1 m), `(Tire_Car_Sandbox)Variant` `14a`.

Race items / HUD / audio:

| Asset | where | notes |
| --- | --- | --- |
| `(UGC_RecRally_Pickup_Sandbox)` | BIG #1041892948749590295, key `edd` | nitro can (`RecRally_NitroBoost_Pickup_fbx`, mat #-1341535144137082110), `TriggerCollectible` type 7, value 1, respawn **5 s**, scale-in 0.5 s, spins, SFX `sfx_rec_rally_boostpickup_collect_04` (1.37 s, vol 0.8, pitch var 0.08), VFX `[StuntRunner_Pickup_VFX]` |
| `(LocalPlayerRaceHUD)` | e2ade4f20354802a95a7a321df723e29 #7602363803801519366, key `0` | screen HUD: placement sprite `RacePlacement_1st..8th` (#6582927287282699536, #7143214893917741097, #1856611940223764231, #5720102601541541071, #-8926427479965386615, #-5052414871918794779, #5073115794614427273, #4026333384606058048), lap text, time-left text |
| Music `RecRally_MainGameplayLoop` | 0976d161a414d8d74fb22d37753aed65 #-4751882525490226119, key `5` | 127 s loop |
| Ambience | keys: cows 450df823 `1`, goats 59e7fbf1 `7`, henhouse 49530a0f `8`, horse 31bdc950 `1`, rattlesnake fbb3af9f `a`, train signal 6a4fcb9f `d` | 2-6 s one-shots for the barn / railway |
| Start lights | `[Light_RaceStart]` (Stunt Runner, inside `9c9`) | reuse for the rally start |
| Southwestern kit (BIG) | rocks `(Rock_Cliff)` `1ed` (12.2 x 6.6 x 5.4 m), `(Rock_Plateau)` `b25` (8 x 4.7 x 6.7), `(Rock_Wall)` `dc7` (4.9 x 8.2 x 7.7), `(Rock_Tall)` `b8` (5 x 6.4 x 3.6), `(Rock_Wedge)` `22e`, `(Rock_Cone)` `423`, `(Rock_01)` `761`, `(Rock_04)` `73`, `(Rock_Stump)` `b8fd`; `(Hay_Bale_Sandbox)` `7c` (1.3 x 0.8 x 0.8), fences `cd` / `93d` / `(Fence_Wood_Frame_5m)` `e1d` (5 x 2.6 m), `(Railing_Guardrail_Auto)` `81e` (1 m), `(Concrete_Barrier)` `418`, `(Tractor_Tire)` `704` (2.25 m), `(MetalBarrel)` `79`, `(Wood_Pallet)` `ce4a`, `(LogPile)` `523`, `(PineTree_Dead)` `f7` (24 m tall), `(Tree_Stump)` `275`, `(GasPump)` `4d`, signs `(Sign_FrankiesFuel)` `31`, `(Sign_Diner)` `61`, `(Sign_DriveIn)` `d8cd`, `(Sandbox_Cone)` `34b`, `(BroadcastCameraStand)` `5c8`, `(Light_Chevron_Sandbox)` `c83` (drift arrows) | (all keys in BIG) |

Not found in bundles: the Pioneer Peak terrain, barn, bridge, tunnel, railway, cut-outs, podium, merch booth.

### 3.3 Rebuild plan (Rec Rally)
1. Track: procedural terrain (heightmap-free mesh strips) for a ~1.5-2 km loop following the wiki walk-through
   (sections in 3.1), 12-16 m wide dirt road (vertex-coloured dirt material; GUESS reuse a Rec Room ground material if
   one is found - else URP Lit), banks from the Rec Royale rock kit, fences/guard rails/hay bales/tires along the edges,
   chevrons at the drift section, our own barn (box + gable roof, red paint material), bridge (plank deck + guard rails),
   tunnel/cave (rock pieces), railway (rails as thin boxes + sleepers), river (flat water plane), 2 big ramps (jumps).
   Store the spline + prop list as data. Invisible kill/reset volumes under the map; reset to the last checkpoint.
2. Vehicles: instantiate `(Vehicle_RallyBuggy)` (key `20d`) for visuals/colliders/seat transforms; strip missing
   scripts; our own `RallyBuggy` raycast-wheel controller reproducing 3.2 numbers (mass 1000, 4 raycast suspensions
   0.6 m / 40 % rest / damping 0.8, wheel radius 0.46, torque 3000 falling to 52 % at max speed ~ 700 RPM x 2 pi x 0.46 /60
   = 33.7 m/s top speed (GUESS for max speed), steer 35 deg, handbrake lateral friction x0.306 for drift, drift boost
   torque x3.2 after a 10 deg+ sideslip, nitro: 3 cans -> 3 s of +35 % torque + throttle x1.25). Animate the skinned
   joints (wheels spin/steer, steering wheel, e-brake lever, boost button, gauges).
3. Seating: use the ETS seat pattern of `somnium_player.md` 6.3 (freeze 6.2, `Poses.SetPose(SittingStraight)` every
   frame, write `Body.Root` to the seat each LateUpdate, VR `PlaceHead`, get out = level rig + restore + teleport beside
   the buggy). Seat transforms from 3.2 (driver, passenger, standing back seat). VR: grab the steering-wheel collider
   (motion mode = wheel angle from hand positions; joystick mode = left stick), triggers = throttle/brake, grab the
   e-brake lever (drift), push the boost button. Non-XR (client walk/jump/sprint zeroed while seated, so the keys are
   free to read with `Input.GetKey`): W/S throttle/brake-reverse, A/D steer, Left Shift drift, Space boost, **G** exit
   (Esc/F/E are client-owned - `somnium_conventions.md` 11.1). Rec Room's own screen tuning:
   `SteeringWheelScreenToolBehaviorConfig_Kart` (BIG #370115238501748491: gas axis 0.15..0.97, steering dead zone 0.4
   (0.2 with button throttle), steer return-to-rest and input smoothing 0.25 s - use for digital A/D steering) and
   `VehicleScreenPlayerModeConfig` (063b6b66... #-8283927040496672148).
4. Race logic (authority = instance owner): 6 grid slots, 3-2-1 with `[Light_RaceStart]` + SR countdown clips,
   seat lock during countdown, ordered checkpoint gates (anti-shortcut: must pass all), lap counter, placement by
   (lap, checkpoint, distance to next), race time limit (GUESS 10 min), results -> podium (our own 3-step podium) +
   HUD placement sprite. Networking: each driver simulates his own buggy and streams pose/velocity at 10-20 Hz
   (networked ints: quantised position/yaw per slot, lap/checkpoint/finish time); remote buggies interpolated;
   buggy-buggy bumps are local-only kinematic pushes (GUESS: enough for fun).
5. Audio: engine idle/accel loops pitched by RPM, tire rolling/slide loops, suspension/impact one-shots, nitro sounds,
   `RecRally_MainGameplayLoop` music, farm/rattlesnake/train ambience at the barn and railway.

## 4. Make It To Midnight (MITM)

### 4.1 What it is (web: fandom wiki MITM page saved in `research\party_wiki\Make_It_To_Midnight.txt`, pocketgamer, levelboi)
- Rec Room Original released 2023-08-17, built with Rec Room Studio. **4 v 1 asymmetric horror** (inspired by Dead by
  Daylight), needs exactly 5 players to start. Setting: the abandoned **Rec County Fairground** at night, the
  "Merry Midway" (Ferris wheel, a slide, alleys, a red circus tent with the performers' acts, concession stands with
  signs (pretzels, cotton candy, root beer, VR Cola at .87), bathrooms with stalls, bushes, photo booths, north and
  south exit gates). Posters of the performers: Bonky the Clown, Wanda the Dancer, Helga the Seer, Lionel the Tamer.
- **Survivors (4)**: spawn together in a photo booth. Goal: repair **3 generators** (colour-coded; sit on an empty seat
  at a generator; progress fills while seated; periodic skill check: a spinning dial - press when it is in the green:
  +25 XP, light green = critical +50 XP and extra progress; missing/leaving/too late = **fail: -10 % progress and
  Bonky is alerted**). Several survivors can work one generator (faster). Big light above a generator flickers while in
  progress, steady when done. After the 3rd generator: alarm + announcer ("head for the exits"), **north and south
  exit gates** open by pulling a lever (progress is kept if you let go); escaping = +550 XP (gate +150 XP).
  - Stamina 100: sprint 1/s(GUESS unit), jump 5, slide 5, climb 40; regenerates when idle; exhausted = slower recovery.
    Getting hit grants a short speed burst.
  - Hit once = **injured** (bandage icon, loud breathing, red aura); heal = another survivor holds a button ~**10 s**.
    Hit when injured = **caged** for **1:30** then eliminated; teammates free you by grabbing the cage door (hold; timer
    pauses while unlocking). A freed survivor cannot be caged again (next cage = out).
  - Hiding: bushes, behind objects, bathroom stalls (a stall door opened into someone honks and stuns them briefly).
  - Sounds of healing/uncaging are heard map-wide.
- **Bonky the Clown (1)**: spawns in a far photo booth; giant **squeaky hammer**: a hit injures, a hit on an injured
  survivor cages; after each swing Bonky is slowed and the hammer must **re-inflate (~3 s)**. **Fear aura** (red glow,
  20 m: survivors see a warning; injured survivors inside breathe loudly). Abilities (jump button, survivors are notified):
  Clown Radar (reveal survivors in the aura for 25 s, 75 s cooldown), Silly Sneak (silent, no aura for 10 s, 45 s
  cooldown), Bonky Bounce (long forward leap, 75 s cooldown). Can bonk generators (with a perk) to scramble tests.
- **Clock**: at 10 min ("approaching midnight") Bonky speeds up progressively; at 15 min the clock strikes **midnight**
  and all survivors are revealed to Bonky. Match ends when all survivors escaped/eliminated.
- Meta: levels to 100, perks (12 survivor, 13 Bonky, up to 3 equipped), tickets for circus outfits/food. VR players are
  forced into seated-mode comfort; screen players have improved melee.
- Fun: tension of hiding + the funny/creepy squeaky clown, cooperative rescues, the skill-check mini-game.

### 4.2 Bundle content
Nothing of the MITM map/generators/cages/photo booth was found (it was a Rec Room Studio room). Usable pieces:

| Asset | where | use |
| --- | --- | --- |
| `(Paintball_SplatHammer)` - its visual mesh is **`Weapon_BonkyHammer_LOD0..2`** | BIG #6323783143975840158, key **`51d`**; mesh in 1df8e0ef #-1502196845052034003 (0.66 x 1.87 x 0.38 m) | Bonky's squeaky hammer (skins `Weapon_BonkyHammer_Skin_BonkyFace/Swirl_*` materials in 5c91ea11...). Sounds (BIG): `sfx_pm_splathammer_swing_01..03` (#-3179289568074440744, #-7897245257203588809, #8971259812870537797, vol 0.4), `_playerhit_01` #-3390020069545992361 (1.31 s), `_deflate_01` #6862171501018066204 (1.03 s), `_inflate_01` #-8161971383599738263 (1.84 s), `_ready_01` #3124771178365105948, `_physical_hit_empty/full_*`, `_splat_01..03`. MeleeWeapon damage 100; `ExplosiveWeaponAddon.cooldownSecondsOptional` 6 s (paint burst radius 7 - not used) |
| `(StNMonsterCostumeDummy)` | BIG #-6747469095946872866, key **`56`** | mannequin with avatar rig (`Avatar_Rig_fbx` Animator) wearing `Enemy_Clown_fbx` (`Enemies_Clown_LOD0` #6314532174606668081 body ~1.08 x 1.48 m, head `Enemies_Clown_Head_LOD0` #-1636406042487385807, mats `Enemy_Clown_Mat`/`_Head_Mat` in 674f49fa...). Use as Bonky posters/statues/decor, or as a costume shell that follows the Bonky player (GUESS; avatar itself untouched) |
| Fair food | bundle 85515de1... keys: `(FoodConsumable_Popcorn_Butter)` `7a`, `(Consumables_Popcorn_Box)` `ca`, `(Consumables_CandyApple_CandyAppleBasket)` `e4`, `(FoodConsumable_CandyApple)` `e6`, Monster Munchies popcorn `d67`/`c0`/`c57` | concession stands |
| Standees | BIG `(Standee_Skeleton01)` `1ba`, `(Standee_Skeleton02)` `87f`, `(Standee_Skeleton_Drummer)` `69f`, `_Fiddle` `647`, `_Squeezebox` `04`, `(Standee_Bear01)` `81`, `(Standee_Male01)` `5e7`, `(Standee_Female01/02/03)` `01b`/`bd`/`fc` | spooky cut-outs around the midway |
| Misc props (BIG) | `(Sandbox_Stool)` `0ca`, `(Stool_Wood_Round_Sandbox)` `58` (generator seats), `(TrashBin_Sandbox)` `0c`, `(TrashCan_Metal_Sandbox)` `d4ae`, `(Booth_Diner_Sandbox)` `d57`, `(Sandbox_LogBench)` `f5a`, fences `cd`/`93d`/`e1d`, `(MetalBarrel_Sandbox)` `79`, `(Wood_Pallet_Sandbox)` `ce4a`, `(Hay_Bale_Sandbox)` `7c`, `(PineTree_Dead_Sandbox)` `f7`, marquee/chevron lights `c2`/`c83` (fair lights), Ink Space chairs | midway dressing |
| Audio | heartbeat `RecRoyale_Heartbeat_Up01` (dad8e9c1... key `b`) / `_Down01` (4c4bab42... key `d`); ambience `Quest_Pirate1_Caves_GhastlyAmbiance01` 60 s (f515f334... key `8`), `Quest_Goblin2_Ambiance_Mines01` 32 s (0e82ce18... key `9`); countdown/buzzers from Stunt Runner (2.4); `SFX_Failure_Stereo` (skill-check fail) | fear aura, generator fail, alarms |
| Locomotion feedback | `CrossPlatformPlayerLocomotionFeedbackContent` heartbeat curves (5 s curve, start delay 2 s, wind-down 1.5 s) | survivor heartbeat near Bonky |

### 4.3 Rebuild plan (MITM)
1. Map (our own, procedural + kit): ~80 x 80 m night fairground inside a fence ring with 2 exit gates (north/south,
   lever + gate animation), a Ferris wheel (procedural ring of spokes + gondolas from primitive meshes, slow rotation),
   a red/white striped circus tent (procedural cone/cylinder, stripe texture generated at runtime), a slide, 4-6
   concession stands (box booths + popcorn/candy-apple props), bathrooms block with 4 stalls (hinged doors), bushes
   (procedural blobs) and hay/barrel/fence clutter for line-of-sight breaks, 2 photo booths (spawn points), string
   lights (emissive spheres on catenary lines), dark ambience + fog. 5 generators placed around (need 3), colour-coded
   lights above each (flicker in progress, steady when done).
2. Roles & flow (authority = elected owner; exactly 5 players; if < 5 = "Not enough players to start"): random Bonky;
   10 s intro card per role; match clock 0..15 min (Bonky speed multiplier ramps from 10 min, midnight reveal at 15);
   end on all survivors out/escaped. State in networked ints: generator progress x5 (0-1000), per-survivor state
   (healthy/injured/caged/out/escaped + cage timer), gates open progress, Bonky ability cooldowns, clock.
3. Survivor mechanics: generator seat interaction (sit = attach player to seat transform), skill-check UI (world-space
   dial on the generator; press interact key / VR trigger in the green window: green 15 % of the dial, light green 5 %,
   GUESS), heal (hold near injured teammate 10 s), cage rescue (hold on cage door; cage = procedural bars around the
   player, player locked), stamina bar for sprint/jump/slide (stamina costs above).
4. Bonky: gets the hammer (key `51d` visuals, strip MeleeWeapon), swing detection (VR: hammer head velocity > 3 m/s
   hitting a survivor capsule; screen: click = scripted swing animation + sphere cast 1.5 m), after swing: deflate sound
   + 3 s re-inflate (`_inflate_01`), Bonky move speed x0.5 during re-inflate (GUESS). Fear aura = red point light + 20 m
   radius heartbeat for survivors. Abilities on the jump key (client jump stays; use a key the client does not own if
   jump is owned - see conventions s11).
5. Sounds: hammer set above, heartbeat, ghastly ambience, alarm (SR_Buzzer_Long01 / announcer-less), gate lever.
6. Avatar rule: never modify the Somnium avatar - Bonky identity via the hammer, a red aura and an optional clown-head
   prop following the head bone position (not parented into the avatar hierarchy - GUESS acceptable).

## 5. My Little Monsters (MLM)

### 5.1 What it is (web: fandom wiki `research\party_wiki\My_Little_Monsters.txt`)
- Rec Room Original (made entirely in Rooms 2.0), released 2024-06-13, 1-8 players, roleplay/creature-care in the
  town of **Cryptid Creek**. You are a new monster caretaker: hatch an egg and raise a **Sasquatch, Mothman or
  Chupacabra** (colours blue, green, orange, pink, red; black variants via a side quest).
- Town NPCs: Clarence (Monster HQ, missions that raise the sanctuary star level = sanctuary size), Penny
  (EGGceptional Cryptids: eggs 600 prismatic essence, renaming, storage), Wilson (General Store: craft decorations),
  Benedict (Cryptid Chic clothing), Iris (prismatic essence shop + 5-day visit rewards), DJ (side quest), the Sunstone
  (refills and upgrades the monster's light), Cornelius Cryptid (founder, statue by the lake, cabin on the hill).
- **Sanctuary**: your private area where the monster lives; decorate with crafted items. Interactions (only there):
  Pet, Sit (logs/benches/chairs), Feed (fruit tree), Play (toy ball dispenser), Sleep (cardboard box bed/house), Work
  Out (hamster wheel, swing set), Music (DJ set, microphone, taiko drum, gong), Cuddle (doll dispensers). Each gives
  20 XP (Play 1), or 100 XP when it is what the monster currently wants (the monster refuses other activities until
  satisfied). Growth stages: Hatchling (300 XP) -> Toddler (400) -> Child (600) -> Teen (800) -> Adult (1000) -> Elder.
- **The Evergrown** (adventure zone): gather resources with axe/pickaxe (stone/metal/hardened/everwood tiers; trees,
  saplings, boulders, crystals, mineral deposits, dig sites) for crafting; you survive there only inside your monster's
  light: 3 stars (1 min each, up to 10 by upgrades); 7 s outside the light = removed.
- Fun: attachment to a growing pet, collecting/crafting, cozy decorating, co-op exploring.

### 5.2 Bundle content
No monster, egg, sanctuary, Evergrown or Cryptid Creek assets are in the plain bundles (Rooms 2.0 content). Found only:
MLM clothing materials (`Baseball_Hat_MLMMothman/Sasquatch`, `UntuckedTee_Shirt_MLM_*`, `Hoodie_Shirt_MLM_Mat`),
spray-paint skins `PaintSpray_Skin_Sasquatch/Mothman/Chupacabra_{Common,Uncommon,Rare,Epic}` (5c91ea11...), share-camera
skin `ShareCamera_Skin_Cryptid_Mat`, and food `(FoodConsumable_MLMDango)` key `a6`, `(Consumables_MLMDango_Tray)` `06`,
`(Consumables_MLMDango_IceCream)` `6ec`, `(MLMDangoConsumableTool)` `cdb` (bundle 85515de1...). Possible stand-ins
(GUESS): `(Quest_Dracula1_Bat_Plushy)` BIG #4436530697076807301 (baby Mothman), `(Standee_Bear01)` / Frontier bear
standee (Sasquatch reference), Dracula wolf enemy (Chupacabra); `HeightmapGenerators` (bundle 18c123d6...
#-2865225905434159388, Rooms 2.0 terrain layer presets GentleSlopes/RuggedSlopes/Flat/Cliffs/MediumSlopes/RiverBed)
and shader `AG/TerrainWater` (18c123d6 #-3756027771748033771) for the creek; `(Sandbox_LogBench)` `f5a`,
`(Tree_Stump_Sandbox)` `275`, rocks (3.2) for the Evergrown.

### 5.3 Rebuild plan (MLM) - reduced scope recommended
1. "Cryptid Creek Sanctuary" instance: a small valley (procedural terrain using the heightmap-layer ideas above), a
   creek, a hatchery booth, a general-store bench, and one sanctuary pad per player (up to 8, 12 x 12 m each).
2. Monsters: our own stylised procedural creatures (rigged primitives: body capsule + head sphere + ears/wings/spines
   per species; 5 colour palettes; scale per growth stage 0.4 -> 1.2 m) with simple animation (bob, blink, hop, wing
   flap) and a follow/wander AI on the NavMesh. Species: Sasquatch (furry brown biped - use a fur-like noise texture),
   Mothman (dark body, big red eyes, wings), Chupacabra (spiky quadruped).
3. Care loop: needs/wants (Feed, Play, Sleep, Work Out, Music, Cuddle, Pet), interaction props built from kit pieces
   (fruit tree -> tree stump + spheres; ball dispenser -> our ball; box bed -> cardboard box `(Cardboard_box_Sandbox)`
   BIG; hamster wheel procedural), XP numbers above (20 / 100 when wanted), stages Hatchling..Elder with the XP table.
4. Persistence: per-player monster (species, colour, name, XP, stage) must survive sessions -> store per player in our
   persistent store (see `somnium_networking.md` for what persistence exists; GUESS: Somnium world storage or local
   player prefs); others see it via networked ints while in the instance.
5. Skip for v1: the Evergrown gathering/crafting economy, NPC quest chains, shops.

## 6. Punch Up

### 6.1 What it is (web: fandom wiki `research\party_wiki\Punch_up.txt`)
- A Rec Room Original announced in 2016 ("coming soon" posters in the 2016 locker room / 2017 Rec Center) that never
  shipped. Early 2016 builds had it under the file name "boxing": an **orange gym** (the dodgeball gym layout) with 2
  white balls on each side where the dodgeballs usually are; one side said "Dodge Ball", the other "Rec Room". The
  devs built a simple prototype and moved on. Easter egg: a **boxing glove** in a trash bin in the Laser Tag map
  CyberJunk City. No rules are documented - we design our own.

### 6.2 Bundle content

| Asset | where | notes |
| --- | --- | --- |
| `(PunchingBag)_SpawnableTool` | BIG #-938117583273895998, key **`88`** | wobbly training dummy: `PunchingBag_Base_LOD0` (0.75 m dia x 0.84 m weighted base, 1df8e0ef #-2282911919597587554) + skinned `PunchingBag_Dummy` torso/head (0.4 x 0.25 x 0.92 m) on physics proxies Root/Chest/Neck/Head with Rigidbodies + ConfigurableJoints + `JointMaxStretch` (springy wobble works with built-in physics), CollisionAudio -> DummyPunched1/2 |
| `PunchingBag_fbx` | 1df8e0ef #-1186003750933400674 | raw model (Animator, LODGroup) |
| `PunchingBag_PlayerTransformation` | d10447fc7fb3dd6312c640a4b3dd438b #7253829310613466196 (container path key `Assets/Activities/Quest/Shared/Content/Enemies/Transformations/PunchingBag_PlayerTransformation.prefab`) | quest "turn player into a punching bag" transformation (not needed) |
| `DummyPunched1` / `DummyPunched2` | 04b4c836... key `f` (0.19 s) / c66c4c4a... key `d` (0.38 s) | punch impacts |
| `DingDing` | cc5b30c7397eaaaa29b44e823663a6f4 #-463502042293685994 key `9` (3.0 s) | boxing bell |
| Crowd | SR_Audience_Cheer*, SR_Crowd_Aww_01, SR_CrowdReactions_* (2.4) | crowd |

No boxing glove mesh exists in the plain bundles.

### 6.3 Rebuild plan (Punch Up) - our own design
1. Venue: the orange gym (reuse the dodgeball gym of `rr_sports.md` sections 2.3 and 9.1, orange-tinted; else a procedural
   20 x 12 m gym with orange walls) with a central **boxing ring** (procedural: 6 x 6 m raised platform 1 m, 4 corner
   posts, 3 rope rings as thin cylinders, corner pads red/blue), punching-bag practice corner with `88` instances.
2. Gloves: procedural boxing gloves (rounded box + cuff, red/blue team colour, URP Lit) that follow the player's hands
   (VR: tracked hands; Non-XR: an animated jab/hook on click) - held props, not avatar modifications.
3. Rules (GUESS, simple and fun): 1v1 (or 2v2 tag) best of 3 rounds x 60 s; a hit = glove sphere (r 0.12 m) touching
   the opponent's head/torso capsule with glove speed > 2.5 m/s (VR) -> +1 point; head hit +2; blocking = gloves
   within 0.25 m of own head cancels head hits. Knock-back = small camera shake / push through the client's allowed
   movement API. Bell `DingDing` at round start/end, `DummyPunched1/2` per hit (pitch random), crowd cheer on KO
   (5 points ahead) or round win.
4. Networking: authority computes hits from both players' streamed hand poses (hand positions/velocities already in
   the avatar stream? - GUESS; otherwise each client reports its own landed hits and authority validates distance).

## 7. Shared systems and networking mapping

### 7.1 Things several games share

| System | Source | Used by |
| --- | --- | --- |
| Push button (cap 0.30 m on base 0.35 m) + press sound `Charades_ButtonPress` | meshes `Charades_Button` #4413723384518494692 / `Charades_Button_Base` #-7875137590744036892 (BIG); loadable via `(Sandbox_Room_Boost_Button)` key `11` (whose child `Charades_Button` #4124224791932114400 carries the mesh + `PhysicsButtonVisual`) | Charades start/"Got it", Stunt Runner lobby, Punch Up start |
| Race start light `[Light_RaceStart]` (5 stops, 0.8 s apart) + 4 animated gates | inside `(StartingGate_Sandbox_Invention)Variant` key `9c9` | Stunt Runner, Rec Rally |
| Countdown VO / tick / GO, buzzers, crowd cheers, "Course Complete" | Stunt Runner clips (2.4) | all races, Punch Up, Charades end |
| Pickup spin + respawn pattern (`BoostCollectible`/`TriggerCollectible`: respawn 5 s, scale-in 0.25/0.5 s, rotate 50 deg/s) | 2.3, 3.2 | KO Cola, rally nitro |
| Pickup VFX `[StuntRunner_Pickup_VFX]` #5491310025194905856 | BIG | KO Cola, rally nitro |
| Player spotlight `[StageLight_PlayerFollowing]` #-5229972498005014349 | BIG | Stunt Runner leader, Charades performer, Punch Up ring |
| Scoreboard / leaderboard projectors `(ScoreboardProjector)` `c3e`, `(LeaderboardProjector)` `74c`, `(LeaderboardProjector_V2)` `f31` | BIG | all (UI is missing-script: rebuild the text with TMP, keep the projector mesh) |
| Locomotion content (walk/sprint/slide/wall-run/climb constants) | 2.2 | Stunt Runner (all), MITM (slide on slopes, stamina), Punch Up (none) |
| Southwestern/outdoor kit (rocks, fences, hay, barrels, dead pine) | 3.2 | Rec Rally, MITM, MLM |
| Fair food props (popcorn, candy apple, dango, root beer) | bundle 85515de1... | MITM concessions, MLM feeding, Charades cafe |

### 7.2 Instance sizes (corrections to `somnium_networking.md` 5.1 caps)

| id | Activity | cap in networking doc | real game | recommendation |
| --- | --- | --- | --- | --- |
| 3 | 3D Charades | 8 | 2-8 | keep 8 |
| 10 | Make It To Midnight | 4 | **exactly 5 (4 survivors + Bonky)** | set 5 |
| 11 | My Little Monsters | 4 | 1-8 | 8 (sanctuaries per player) or 4 for v1 |
| 14 | Punch Up | 4 | never released | 4 (2 in the ring + 2 waiting) |
| 16 | Rec Rally | 8 | **6** | 6 |
| 20 | Stunt Runner | 4 | 1-4 | keep 4 |

### 7.3 Per-game use of the instance object (NBD64, `somnium_networking.md` 5.6) and presence slot (5.4)
- **3D Charades**: int 3 phase (Lobby/Countdown/Playing=performing/RoundOver=between performers/MatchOver), int 4
  remaining ms of the performer turn, int 5 mode (0 co-op, 1 lightning), int 7 team scores (co-op: team 0 = rounds
  solved), roster 11-26 (team 0/1 for lightning), 27 performer PlayerId+1 | word index (16 bits), 28 turn counter |
  game remaining s, 29 ink stroke count of the current turn. Ink: the performer broadcasts strokes as **type 14 "Ink"**
  sub-messages (new; body: stroke id u16, colour u8, radius u8 (mm), n points u8, points as 3 x S16 mm relative to the
  stage origin, delta-coded) in <= 480 B batches every 50 ms; late joiners request the current drawing with Hello ->
  Census chunks. Word secrecy: the performer's client picks the word from the seed + index (every client has the same
  list), so the word index can be public; only the performer's UI shows it (cheating possible but harmless).
- **Stunt Runner**: int 3 phase, int 4 course remaining ms, int 5 course index 0-5, 27-30 per-roster finish time of the
  current course (ms, 0 = not finished), 31-34 total time; presence slot 19 = checkpoint index; slot 20-31 = our runner
  pose when using the Quake takeover (remote players are otherwise seen through Somnium's own avatar sync - GUESS: the
  client already syncs the avatar root we move, so slot 20-31 may be unnecessary).
- **Rec Rally**: slot ints 20-31 = buggy pose/velocity/flags at 10-20 Hz (already reserved "vehicle/mount state");
  instance 27-32 lap | checkpoint per roster entry, 33-38 finish ms; nitro can taken bitset (int 39, 1 bit per can,
  respawn 5 s by authority).
- **MITM**: 27-31 generator progress x5 (0-1000 each) | sabotage flags, 32 gates progress (2 x 16 bits), 33 Bonky
  PlayerId+1 | ability id | cooldown end s, 34-37 per-survivor state (healthy/injured/caged/out/escaped) | cage
  remaining s; skill-check fail = Effect event (alert Bonky). Hammer hit = Hit event (type 2) from Bonky's client.
- **Punch Up**: 27 fighter A | fighter B PlayerId+1, 28 round | points A | points B; punch hit = Hit event.
- **MLM**: per-player monster = presence slot 19 (species 2 bits | colour 3 bits | stage 3 bits | want 4 bits | XP 16
  bits); monster pose = local AI on each client seeded by owner position (GUESS: cosmetic divergence acceptable).

## 8. Recommended order and effort
1. **3D Charades** (highest value / lowest cost): venue + stage + pen + card + buttons all load from BIG; the only new
   system is ink drawing + its network stream. 3-5 days.
2. **Stunt Runner**: kit + audio all present; needs our course data (6 courses) and the takeover movement controller
   (shared with MITM stamina/slide). 1.5-3 weeks (courses dominate).
3. **Rec Rally**: buggy model/colliders/sounds present; needs a raycast vehicle controller, the seat pattern (ETS), a
   ~2 km procedural track and race logic. 2-4 weeks.
4. **Punch Up**: our own design around the punching bag + procedural ring/gloves. 3-5 days.
5. **Make It To Midnight**: all-procedural map + asymmetric rules + 5-player requirement. 3-5 weeks.
6. **My Little Monsters**: procedural creatures + care loop; persistence needed. 3+ weeks; ship a reduced v1.

## 9. Open questions / GUESS list
- Ink Space: the exact placement of `(Charades_Stage)` inside the hull (1.4 GUESS: root at hull-local (2.0,-3.0,4.0), stage
  along z at x 0.4..3.6 facing -x) - verify by instantiating both and looking; also whether the audience couches are
  part of the venue mesh (the wiki shows wooden couches; no couch prefab is inside the hull: place `(Sandbox_Couch)`
  key `c5`, `(Sandbox_CouchChair)` `f0` or `(RecCenter_Couch_Sandbox)Variant` `ed9` (BIG; dump
  `research/dumps/couch.txt`) on the rugs at x -8.8).
- Charades: who confirms a correct guess in Rec Room (performer button vs guesser buttons)? `CharadesManager.guessButton`
  is null in the prefab (room-assigned). We use performer-confirms. Word list: not in bundles - write our own
  (family-friendly, ~500 words).
- `AG-MakerPenShape` material (BIG #-224598426847956790) has no container; check at runtime whether loading `cb9`
  (pen) makes it resident; else use URP Lit.
- `[CharadesCard]` (no container) - check if resident after `7ab`/`977`/`cb9`; else rebuild (sizes in 1.3).
- Stunt Runner rank thresholds except S+ (< 4:45) unknown; course geometry unknown (encrypted level) - courses are
  our own designs.
- Rec Rally: race time limit, max speed (derived GUESS 33.7 m/s), track length; Pioneer Peak terrain not in bundles.
- MITM: number of generators on the map (we use 5, need 3), skill-check window sizes, stamina units, Bonky speeds;
  map image `MITM Map.png` on the wiki was not downloaded (fetching files needs the user's permission).
- MLM: no creature assets; persistence mechanism for per-player pets must be confirmed with `somnium_networking.md` /
  Somnium world storage.
- Punch Up: no official rules exist; everything in 6.3 is our design.
