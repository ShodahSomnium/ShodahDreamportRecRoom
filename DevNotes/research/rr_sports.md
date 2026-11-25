# Rec Room sports activities: rules, layouts, bundle content, rebuild plan

Status: COMPLETE (2026-09-24). Bundle facts are measured; rules come from the Rec Room wiki / patch notes; arena dimensions marked GUESS.

Conventions used below
- `SP` = `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad`
- `RR` = `SP\rrtool\bin\out\rrtool.exe`. Bundle names are file names without `.bundle` in
  `E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64\`.
- `TOOLS` = bundle **`43ea1176ca25c0333547813ec3298808`** (302 MB, 642 Addressables container entries). Almost every
  sports prefab, manager and many clips live here. Opening it costs ~0.4 ms (header only, LZ4 chunked).
- `ART` = bundle `1df8e0efac4f496ebc58d7e6f86480c3` (87 MB: shared meshes/materials e.g. DodgeBall mesh, Frisbee meshes).
- `SKINS` = bundle `5c91ea11b5d1563aae878dd8be401e34` (324 MB: equipment skins, ball/disc skins).
- `FX` = bundle `f124b399caea956fb2ae85b8413c07d2` (13 MB: GlowGoal mesh, gradients, plastic textures).
- "key" = Addressables container key: load with `bundle.LoadAsset<GameObject>(key)` (see `rr_runtime_facts.md`).
  Objects with no key are implicit: they become resident when a keyed prefab that references them is loaded
  (then find them with `Resources.FindObjectsOfTypeAll<T>()` by name).
- Unity PhysicMaterial combine enum: 0 Average, 1 Multiply, 2 Minimum, 3 Maximum.
- Rigidbody `cd` = collisionDetectionMode: 0 Discrete, 1 Continuous, 2 ContinuousDynamic, 3 ContinuousSpeculative.
- Raw dumps from the research runs: `SP\sports\*.txt` (full `RR tree --fields --all`), `*.sum` (condensed),
  `*mgr.json` / `*mgr.dec.json` (manager MonoBehaviours, ACTk-obscured values decoded by `SP\sports\deob.pl`).

---------------------------------------------------------------------------------------------------------------------

## 0. Summary / how to use this document

- The official sports ROOMS (Dodgeball gym, Paddleball court, Soccer arena, Disc Golf Lake / Propulsion course,
  Bowling alley) live in the encrypted `level*` files. **No arena geometry is in the plain bundles.** What IS in the
  bundles: every playable object (balls, paddles, discs, pins, goal baskets, hoop, shield), the per-sport game
  manager prefabs with their tuning + sound references, Maker-Pen "game rule" wrapper circuits (default win
  conditions), sandbox goals (tool goal / player goal frames), scoreboard UI prefabs, jukebox/stool/pool props from the
  bowling alley, the full set of sport SFX/VO/music, and VFX prefabs (goal burst, disc goal rays, hit particles).
- So each arena = procedural shell (floor, walls, lines, glass, lights, per the layouts in sections 2-7) + Rec Room
  props instantiated from TOOLS + our own game logic (C#) driven by the numeric settings read from the Rec Room
  manager/prop MonoBehaviours with our parser (they are inert missing scripts at runtime).
- All sports prefabs keep original `AG/*` materials (they render correctly in URP, see `rr_runtime_facts.md`); apply
  the depth-priming fix (renderQueue 2501 when the shader has no `DepthNormals` pass) and the particle-material swap.

---------------------------------------------------------------------------------------------------------------------

## 1. Shared building blocks

### 1.1 Game managers (TOOLS, all prefab roots, layer 0, component = manager MonoBehaviour + RRNetworkView)

| Manager prefab | root pathId | MB pathId | scoreName | key tuning (decoded) |
| --- | --- | --- | --- | --- |
| `[DodgeballManager]` | 24173877570888643 | 34575037888914371 | "Round" | in-game role: maxTeleportDistance 12, teleportDelay 1.5 s |
| `[PaddleballManager]` | 1086116914607114145 | 19846918992316321 | "Goal" | ballScoredStateDuration 2 s; role: maxTeleportDistance 12, walk speed 3 (VR+screens) |
| `[SoccerManager]` | 2020373155634707127 | 7097448528076044983 | "Goal" | role: maxTeleportDistance 8, teleportDelay 0.5, walk speed 6; soccerBallSpawnTransform null (set by room) |
| `[DiscGolfManager]` | 4050606750367316508 | 3924058938226539036 | "Stroke" | firstHole 0, lastHole 8 (=9 holes), mercyRuleHoleScore 7, strokeIncrementThreshold 2.5, gravityPickupDistanceOverride 1.5, catchNotificationDistanceBeforeScaling 10, catchNotificationMetersPerScaleFactor 12 |
| `[BowlingManager]` | 5756181348982857369 | 7103344968492981913 | "" | (no tuning fields serialized; logic is code) |

None of these has a container key; they become resident when the matching `(X_GameRuleWrapperCircuit)Variant`
(below) is loaded, or read them with our parser. The managers have NO children (no arena, no scoreboard).

Disc golf score labels (DiscGolfManager, exact strings): scoreHoleInOne "Hole In One!", scoreLessThanNeg2
"Albatross -3", scoreNeg2 "Eagle -2", scoreNeg1 "Birdie -1", scoreZero "Par", scorePos1 "Bogey +1", scorePos2
"Double Bogey +2", scorePos3 "Triple Bogey +3".

Manager sound / VFX references (clip -> bundle table in section 8):

| Manager | field | asset |
| --- | --- | --- |
| Dodgeball | roundStartSFX | `Whistle01` (ec475596...) |
| Dodgeball | roundOverSFX | `Horn01` (d9e43916..., 2.45 s) |
| Dodgeball | playerImpactSFX | `dodgeball_PlayerHit_01` (cccf414c...) |
| Dodgeball | playerImpactVFXPrefab | `[VFX_Default_HitParticles]` PooledParticle #-7923393916391551131 (TOOLS) |
| Dodgeball | playerOutSFX / playerOutVO | `OutSFX` (TOOLS) / `Out` VO (TOOLS) |
| Dodgeball | playerScoreSFX / playerScoreVO | `HitSound01` (TOOLS) / `Hit` VO (0933d7bb...) |
| Paddleball | goalSFX / goalVO / goalVFXPrefab | `CrowdCheer01` (TOOLS) / `Goal` VO (f31fc5a1...) / `[VFX_Default_Goal]` #-2653267271883095523 |
| Soccer | goalSFX / goalVO | `Soccer_Goal01` (TOOLS, 7.8 s) / `Goal` VO |
| Soccer | blueTeamScoreSFX / redTeamScoreSFX | `Blueteamscores` (3ac9d648...) / `Redteamscores` (2467f3cb...) |
| Soccer | powerupPickupSFX | `Soccer_PickupCollected02` (TOOLS) |
| DiscGolf | catchSFX / catchVFXPrefab | `DiscGolf_Catch_01` (f8909f40...) / `[VFX_Charades_Goal]` #-3007701312853215195 |
| DiscGolf | goalSFX / goalVFXPrefab | `CrowdCheer01` / `[VFX_DiscGoalRays]` #7968694324638980927 |
| DiscGolf | playerOutSFX | `OutSFX` |
| DiscGolf | powerupHitSFX | `DiscGolf Propulsion SFX - Powerup 1` (TOOLS, 4.2 s) |
| DiscGolf | discSkinOverride | EquipmentSkin @SKINS #-8931735553398038613 |

### 1.2 Game-rule wrapper circuits (TOOLS; Maker Pen "game rule" chips; give the DEFAULT win conditions)

| Prefab (key) | root pathId | wrapper MB | default |
| --- | --- | --- | --- |
| `(Dodgeball_GameRuleWrapperCircuit)Variant` (key `07`) | -6529224550004290859 | DodgeballGameRuleWrapper #-5479120243638007083 | **defaultScoreToWin 3** (rounds) |
| `(Paddleball_GameRuleWrapperCircuit)Variant` (key `27b`) | -3783089869346524771 | PaddleballGameRuleWrapper #-294364925979479651 | **defaultGoalsToWin 5** |
| `(Soccer_GameRuleWrapperCircuit)Variant` (key `f33`) | -7226000902782972202 | SoccerGameRuleWrapper #-6734295481157218602 | **defaultGameDuration 180 s** |
| `(DiscGolf_GameRuleWrapperCircuit)Variant` (key `2da`) | 1745702773725714575 | DiscGolfGameRuleWrapper #-4788740449420453745 | (none: 9 holes from manager) |
| `(DiscGolfCoopCatch_GameRuleWrapperCircuit)Variant` (key `20e`) | -5904131097940055450 | DiscGolfCoOpCatchGameRuleWrapper #-2778881286248650138 | co-op catch mode, same manager |
| `(Bowling_GameRuleWrapperCircuit)Variant` (key `c1`) | 5342424014909944702 | BowlingGameRuleWrapper #-6360394629792517250 | (none) |

Each wrapper's `gameRulePrefab` points at the manager above. Loading a wrapper by key makes its manager (and the
manager's clips/VFX) resident.

### 1.3 Scoreboards, team visuals, generic VFX (TOOLS unless noted)

| Asset | pathId | notes |
| --- | --- | --- |
| `[GameScoreboardView]` | 7808896619519225561 (no key) | Rec Room's in-world team scoreboard view (dump: `SP\research\shoot\dumps\_GameScoreboardView_.txt`) |
| `(ScoreboardProjector)` (key `c3e`) | 2378492940477835731 | Maker Pen scoreboard projector prop |
| `(GameScoreboardTeamRow)` (ART key `6d`) / `(GameScoreboardPlayerRow)` (ART key `dc89`) / `(GameScoreboardPlayerStatColumn)` | ART -7116564267200832181 / 8098159630668516334 / 8329486366838795837 | UI row prefabs (UGUI/TMP bind; theming scripts missing -> use for fonts/sprites only) |
| `RedTeamIcon` / `BlueTeamIcon` | bundle `85515de19d04f3c4dfe704e6f02046e3` -393923677667482086 / -1837883143400161671 | team icons |
| `[VFX_Default_Goal]` | root -2607123032379620835 (PooledParticle #-2653267271883095523) | paddleball goal burst |
| `[VFX_DiscGoalRays]` | root -8509971318059427009 (PooledParticle #7968694324638980927) | disc golf basket hit |
| `[VFX_Charades_Goal]` | root 8869808536301172773 (PooledParticle #-3007701312853215195) | disc golf catch |
| `[VFX_Basketball_PassedRim]` | root 9003561391953464342 (PooledParticle #6213644211515935766) | basketball swish |
| `[Sandbox_BasketballRim_VFX]` | -5887031207943934196 | sandbox hoop VFX |
| `[SandboxGoalHitVfx]` | 7375694401223502914 | sandbox tool/player goal hit |
| `[VFX_Default_HitParticles]` | (PooledParticle #-7923393916391551131) | dodgeball player hit |
| `[Arena_Points_WorldSpaceNotification_Team1]` / `Team2 Variant` | 7500639247250683869 / 983594565515664114 | floating "+points" world notifications (from Laser Tag arena) |

### 1.4 Sandbox goals (TOOLS) - usable as soccer/paddleball goal frames and triggers

| Prefab (key) | root pathId | geometry |
| --- | --- | --- |
| `(SandboxToolGoalLarge)` (key `e`) | -9074891411949413797 | frame `Gizmo_Modular_fbx` scale 0.3: trigger face BoxCollider 7x7x0.3 -> **2.1 x 2.1 m** opening, 4 frame bars 0.35 thick; layer 23. `SandboxToolGoal`: hitAudio `Goal` VO, hitFlash 0.3 s. Detects TOOLS (balls) |
| `(SandboxToolGoalMedium)` (key `b9`) / `(SandboxToolGoalSmall)` (key `2ee`) | -4614816247404810270 / 4505979316682455644 | smaller versions |
| `(SandboxPlayerGoalLarge)` (key `4b`) | 8884319059487554873 | same frame; `SandboxPlayerGoal` detects PLAYERS; faces use `SandboxPlayerGoalFace_mat` |

Both goals are "Gizmo" style (glowing Maker Pen frame look), not a real football net. Faces are SkinnedMeshRenderers
(`Gizmo_Face` mesh #8009120866254515393) whose corners are driven by 4 bones `BottomLeft/BottomRight/TopLeft/TopRight`
at +-3.5 (x0.3 = +-1.05 m) - moving those bones resizes the goal.

### 1.5 Screen-mode (Non-XR) throw configs = Rec Room's own desktop tuning (use for our Non-XR players)

ScriptableObject-like MonoBehaviours (no GameObject) referenced by each tool's `Tool.screenSettings`; values read with
`RR dump`:

| Config (bundle, pathId) | Key values |
| --- | --- |
| `ThrowDodgeballToolBehaviorConfig` (TOOLS -8825024476658176348) | **throwSpeed 14 m/s**, minChargeDuration 0.1 s, maxChargeDuration 0.5 s, chargeCurve ease-out 0->1, throwGravityCompensationAngle 7 deg, targetMode 2 (aim assist toward a target) with **targetDistanceThreshold 9 m**, maxThrowCurveAngle 30 deg, throwAngularSpeedNormalizedCurve 0.3->1 |
| `ThrowDiscGolfDiscToolBehaviorConfig` (ART 3372446327481663969) | **throwSpeedCurve: charge 0 -> 5 m/s, charge 1 -> 19.96 m/s**, maxChargeDuration 3 s, minHeavyThrowCharge 0.5 (light vs heavy throw animation), maxThrowCurveAngle 30 deg (curve from mouse sideways motion / aim), throwAngularSpeedNormalizedCurve 0.3->1 |
| `ThrowBowlingBallToolBehaviorConfig` (TOOLS -2239386326294135621) | **minThrowSpeed 4.5, maxThrowSpeed 9 m/s**, minChargeTimeForValidThrow 1 s, maxWalkSpeedThrowSpeedMultiplier 1.5 when walking at >= maxThrowWalkSpeed 2.5 m/s (run-up adds speed), release window on releaseSpeedCurve 0.275-0.345 (timing skill), crosshair distance curves |
| `BasketballThrowToolBehaviorConfig` (TOOLS -3833266418078692338) | throwSpeed 12, throwAngularSpeed 180, throw toward gaze target, normalizedVelocityByGazePitch (-90: 0.30, 0: 0.59, 45: 0.99, 90: 0.79), pitchByGazePitch (-90:-90, 0:70, 90:90) = looking level throws a 70 deg lob |
| `PaddleToolBehaviorConfig` (TOOLS -2562427756441507569) | primarySwing animation only (PlaySpeed 4): desktop paddle = click to swing |

Our Non-XR mapping (the client owns W A S D, Q, E, Space, Shift, F, I, M, R, Tab, X, Esc, mouse wheel;
`somnium_conventions.md` 11): **left mouse** = pick up the aimed prop within 2.5 m / hold to charge / release to throw
(siblings read `Input.GetMouseButton*` directly, e.g. `D:\Somnium Quake\Assets\#User\SomniumQuake\Q3Menu.cs:361`,
`D:\Dreamport\Age Of Mythology\Assets\#User\SOMAOM\Aom\AomPointer.cs:49-65`), **right mouse** = drop / cancel,
**G** = hazard / reset my disc, **digits** unused. VR: grip = grab (our own code, never XRI Grabbable;
`somnium_player.md` 3.3), release = throw with hand velocity x `ThrowSpeedMultiplier`, clamped to `MaxThrowSpeed`.

### 1.6 Arena-building pieces available in bundles (keyed, TOOLS unless noted)

| Use | Prefab / asset (key) | Notes |
| --- | --- | --- |
| goal net / divider net | `(Rope_Net_Sandbox)` (`ce3`, root 9179670166180098550) | `Rope_Net_LOD0/1/2` meshes are 1 x 1 m tiles (extent 0.5) - scale/tile for nets |
| lake course trees | `(Sandbox_FlatTreeLarge)` (`63e`), `(Sandbox_FlatTreeMedium)` (`4ae`), `(Sandbox_FlatTreeSmall)` (`e9d`), `(PineTree_Dead_Sandbox)Variant` (`f7`, LODGroup, 4 capsule colliders) | stylised Rec Room trees |
| rocks | `(Rock_01)Variant_Sandbox)Variant` (`761`), `Rock_04` (`73`), `Rock_Cliff` (`1ed`), `Rock_Cone` (`423`), `Rock_Wedge` (`22e`), `Rock_Wall` (`dc7`), `Rock_Plateau` (`b25`), `Rock_Tall` (`b8`), `Rock_Stump` (`b8fd`), `(Tree_Stump_Sandbox)Variant` (`275`) | |
| park furniture | `(Sandbox_LogBench)` (`f5a`), `(Sandbox_SittingLog)` (`abf`), `(Fence_Sandbox)Variant` (`cd`), `(Fence_Wood_Frame_5m_Sandbox)Variant` (`e1d`), `(Fence_Wood_Board_Wide_Sandbox)Variant` (`93d`), `(Sandbox_Cone)` (`34b`) | |
| water | `Water_Lake_Mat` (bundle `18c123d6ac91200e89ad964373be8ed2`, key `6a`); also Water_River/Ocean/Shoreline mats in the same bundle | lake surface for Disc Golf Lake |
| bowling carpet | `Scifi_Carpet_Mat` (bundle `076273bc8cadb5c5abe8603908235619`, key `5`) | classic bowling-alley "space" carpet |
| propulsion gizmos | `(PistonGizmo_CV2)` (`fa`), `(RotatorGizmo_CV2)` (`259`), `(Bounce_Pad_Sandbox)` (`81d`; PlayerLauncher velocityAdditive 5, maxVelocity 8-10), `[Sandbox_Wind_Wavy_VFX]` (no key; TOOLS 8024983982320971218) | moving walls / fans for the Propulsion course |
| boost pickup look | `(Stuntrunner_Pickup_Sandbox)` (`da`): `RecRoom.BoostCollectible` respawn 1, **respawnTime 5 s**, scale-up 0.25 s, pickup VFX `[StuntRunner_Pickup_VFX]` | a ready-made "collect and respawn" pickup (Ko-Cola can visual); reuse the ring child `BucketCollectableRing` for soccer boost orbs |
| surfaces without keys | `Wood_Floor_Matte_mat`, `Wood_Floor_Faded_Matte_Mat`, `Glass_Clear_mat`, `GlassPane_Clear_LessReflective_Mat`, `Rubber_VertColor_FloorMat_Mat`, `Metal_FloorTread_Shiny_Mat`, `Carpet_LowPile_Striped_Blue_Mat` (all TOOLS) | implicit: resident only if a loaded prefab uses them. Safer: clone a resident AG material (e.g. `Paint_LightGrey_Mat`, resident with the disc goal) with `new Material(src)` and set `_BaseColor` / `_BaseMap` - keeps Rec Room's shading and has a DepthNormals pass (`AG/Standard-Opaque`) |

---------------------------------------------------------------------------------------------------------------------

## 2. Dodgeball

### 2.1 Bundle content

`(DodgeballBall)` - TOOLS key **`1bf`**, root -1619130950701524752, layer 10.

| Component | values |
| --- | --- |
| SphereCollider | radius **0.11**, material `DodgeballBall_phys` (ART #-6279935958218146749): dyn/stat friction 0.6/0.6, bounciness **0.6**, frictionCombine Maximum, bounceCombine Minimum |
| Rigidbody | mass **1**, drag 0, angularDrag 2, gravity, ContinuousDynamic |
| Tool | pickup yes, handedness 2, throwSpeedMultiplier 1, **MaxThrowSpeed 15 m/s** (HasMaxThrowSpeed 1), ThrowAudio `Throw01` vol 0.5, screen config `DodgeballScreenToolBehaviorConfig` #-8825024476658176348 |
| CollisionAudio | "Bounce": `Bounce03` (a1ca25a9..., key `4`), layers mask 3072, min impact 0.15, pitchVar 0.15 |
| Dodgeball MB | fireTrailParticles -> `[DodgeballFireTrail]` ParticleSystem #-3657161367477200656 (inactive child, dur 3 s loop) |
| Mesh child | `DodgeBall` mesh (ART #4365031544678992029, AABB extent 0.110 -> 0.22 m diameter), material `Dodgeball_mat` (TOOLS #2020522095225122594, shader `AG/Standard-Opaque-Decal`, `_DecalTex` Dodgeball_Ball_Tex) |
| Trail child | TrailRenderer time 2 s, material `VFX_Spark_mat` (ART, shader Mobile/Particles/Additive -> swap to URP particle unlit additive); ToolTrail: visible after attack/upon release, hidden while held |
| Skins (inactive) | Spikes (Cactus), Coconut; more dodgeball skin materials in SKINS (Baseball, Billiards, Comic, Disco, GlowInTheDark, Goblin, Gold, Pumpkin, Recasso) and ART (Beach, Cactus, Coconut) |

Other: `DodgeballThrow_Cutout_LOD0/1` meshes (TOOLS -7529138995897237782 / 4490280236002311914): a cardboard
standee of a player throwing (tutorial prop). Hand poses `Hand_Pose_DodgeBallGrip.L/.R` (ART AnimationClips).
Music: `DodgeBaller` (bundle `3be527316db9cfb62f25d7d16854a03e`, key `f`, 150 s stereo, streaming).

### 2.2 Rules and round flow (wiki + bundle defaults)

- Room `^Dodgeball` (pickup room: VR walk/teleport + screen mode together) and `^DodgeballVR` (teleport-VR only).
  One of the launch games (2016). **2-6 players, two teams (auto-sorted), i.e. up to 3v3.**
- Teams are placed on their own half of a gym court. **Three balls spawn at even spacing along the centre line** when
  a round begins; a ball that leaves the play area respawns at its start spot.
- Players may move with a short cooldown (bundle: teleportDelay **1.5 s**, maxTeleportDistance **12 m**) and can hold
  **a ball in each hand**.
- **Hit by a thrown ball = out**: the player goes to the sideline until the next round (bundle: playerImpactSFX,
  `[VFX_Default_HitParticles]`, "Out" VO + OutSFX; thrower hears HitSound01 + "Hit" VO).
- **Catching a thrown ball nullifies the throw: neither player is out** (no "catch = thrower out" rule in Rec Room).
- Leaving your side / the court gives a penalty (patch notes 2016: "added a penalty for leaving the court"; 2018:
  fixed getting onto the wrong side). GUESS: stepping over the centre line or out of bounds = out.
- The round ends when every player of one team is out (round over: `Horn01` 2.45 s). **First team to 3 round wins
  takes the match** (wiki; `defaultScoreToWin 3`). Round start: `Whistle01`.
- "On fire" balls: a thrown ball ignites (the `[DodgeballFireTrail]` particle child, `Dodgeball.fireTrailParticles`)
  and stays live until stopped; only a live ball knocks players out; players reported that a still-burning ball
  could not be picked up until stopped by hand (Steam discussion). Implement: live = thrown by an opponent and not yet
  touched floor/wall/ceiling/another ball; fire trail on while live; hit while live = out; catch while live = dead
  ball, no out. GUESS: a live ball hitting a ball held by the target is blocked (dead, no out).
- Friendly fire: GUESS no (a teammate's ball never counts).
- Music: `DodgeBaller` (150 s). VO set: Ready / Start / Red Team / Blue Team / Redteamwins2 / Blueteamwins2.

### 2.3 Arena layout (GUESS where marked; the level is encrypted)

Known: indoor gymnasium, wooden court split into two team halves by a centre line, sidelines where out players wait,
3 balls on the centre line. Everything numeric below is a GUESS sized for 3v3 VR play:

| Element | Spec (instance-local metres, +Z = red half) |
| --- | --- |
| court | **9 m wide (X) x 16 m long (Z)**, centre line at Z=0 (volleyball-court proportions); team halves 9 x 8 m |
| floor | wood (clone of an AG material with `Wood_Floor_Matte_mat` look or procedural planks), painted lines 0.08 m: boundary, centre line, 3 m "attack" lines; halves tinted red/blue near the back |
| ball spawns | (X=-3, 0, +3; Y=0.11; Z=0) - "even increments across the centre line" |
| team spawns | blue at Z=-6 (X=-2.5, 0, 2.5) facing +Z; red mirrored at Z=+6 |
| out benches | along both long sides, 1.5 m outside the sideline, on a 0.4 m riser; out players are teleported to their own team's bench |
| walls | gym walls 3 m beyond the court on all sides, 7 m tall; padded 2 m dado; ceiling 9 m with light panels; a mid-court invisible barrier (trigger) so nobody crosses the centre line |
| scoreboard | one on each end wall above 4 m (team round wins + players left) |
| play-area bounds | box 15 x 22 m around the court; a ball leaving it or resting there > 3 s respawns at its centre-line spot |

---------------------------------------------------------------------------------------------------------------------

## 3. Paddleball

### 3.1 Bundle content

`(Paddleball_Paddle)` - TOOLS key **`2d`**, root 7792956688979234920.

| Component | values |
| --- | --- |
| Tool | pickup, **LockOnPickup 1** (stays in hand), handedness 0, R-hand grip pos (0.0003,0.0275,0.0455) rot (-0.291,0.0015,-0.005,0.957); screen config `PaddleScreenToolBehaviorConfig` #-2562427756441507569 |
| Rigidbody | mass **10**, ContinuousSpeculative |
| Paddle MB | springSettings: **SpringForce 50000, Damper 1, MassMultiplier 1** (physics paddle follows hand through a spring; Rec Room uses proxy physics) |
| CollisionAudio | "Hit": `RacketHit01` (TOOLS), layers 1024, pitchVar 0.2, volume curve (0->0.33, 1->1) |
| Visuals | Pivot (scale 1.25): `Racket` handle mesh (TOOLS #4859623290144747524, extent 0.016x0.130x0.012) mat `PropOutline_Orange_Mat`; `RacketHead` (#6560377849420181508, extent 0.249x0.324x0.018, local scale 0.6) mat `PropOutline_Yellow_Mat`; `RacketMesh` net (#-2414519376486469628) mat `Racket_Net` (AG/Standard-Transparent) |
| Colliders | 6 BoxColliders on "Colliders" (scale 1.25): frame 0.18x0.041x0.02 @y0.471, sides 0.03x0.162x0.02 @x+-0.135 y0.31, bottom 0.17x0.055 @y0.143, **face 0.24x0.28x0.02 @y0.31**, handle 0.032x0.261x0.024; material `PaddleballPaddle` (TOOLS #-8656661757870204304): friction 0.6/0.6, **bounciness 0**, bounceCombine Minimum |
| VFX | `[VFX_Impact_Particles]` child of net (inactive, Wave 0.1 s + Spark 0.3 s) played on hit |

The paddleball BALL is not a separate prefab in the bundles (the room's ball lives in the encrypted level).
Candidates: `(TennisBall)` (key `787`, radius 0.07, mass 0.25, drag 0.8, angDrag 0.5, MaxThrow 18,
`Tennisball_phys` bounciness **0.95** friction 0.1, Minimum/Maximum combine; impact clips `TennisBall_Impact01a/b`) or
`(PingpongBall)` (key `568`, radius 0.02). GUESS: use the TennisBall prefab (with gravity off, see 3.2) or a
procedural glowing sphere of ~0.1 m radius.

Also present: `Paddleball_Practice_cutout_LOD0/1` meshes - these are the LOD meshes of `(Standee_Female03)`
(TOOLS root 856613040094012036; mesh #-7079709416345443572, extent 0.84x0.93x0.22 -> 1.68 x 1.86 m standee).

Ping pong (bonus, same family): `(Sandbox_Table_PingPong)` key `7cd` root -1362248597509205463, table mesh
`PingPongTable` (#-1134645092560256000, extent 0.93x1.37x0.46 -> 1.85 x 2.74 m top, placed at y 0.76);
`(Pingpong_Paddle)` key `64` (mass 2, Paddle spring 10000, collider 0.147x0.182x0.025 `PingpongPaddle_phys`
bounciness 0.2); `(PingpongBall)` key `568` (r 0.02, mass 0.25, drag 0.5, `PingpongBall_phys` bounciness 0.8;
clips pingpongballhittable/hitpaddle/hitfloor).

### 3.2 Rules and flow

- Room `^Paddleball`, launch game (2016), **exactly 2 players (1v1)**, VR only in Rec Room (no screen mode there; we
  also offer Non-XR with click-to-swing, `PaddleToolBehaviorConfig`). Leaderboards existed (2016).
- "3D Pong": a **zero-gravity ball** floats in a transparent ("plexiglass") tube. One player **serves the floating
  ball**; the other must stop it reaching the back of their own play area and hit it back. **A point is scored when
  the ball reaches the back of the opponent's area. First to 5 points wins** (wiki; `defaultGoalsToWin 5`).
- After a point: goal VO "Goal", `CrowdCheer01`, `[VFX_Default_Goal]` at the goal, then the ball stays in the scored
  state for **2 s** (`ballScoredStateDuration`) before the next serve.
- In-game role: walk speed 3 (VR and screens), teleport up to 12 m (players mostly stand at their end).
- GUESS details (not documented): the player who conceded serves next; the serve ball appears floating ~0.8 m in front
  of the server at chest height (1.3 m) and stays put until touched (a 5 s serve timer auto-launches it slowly toward
  the receiver); ball speed gains ~5 % per paddle hit up to a cap (~14 m/s), walls/floor/ceiling reflect it with
  restitution 1 and no friction; only the paddle (not hands/body) returns the ball.

### 3.3 Arena layout (GUESS; the level is encrypted)

| Element | Spec (instance-local, +Z toward player 2) |
| --- | --- |
| tube | rectangular glass tube **3.0 m wide x 3.0 m tall x 12 m long** (inner), floor at Y=0; walls = `Glass_Clear_mat` look with thin glowing edge frames; perfectly reflective colliders (PhysicMaterial bounciness 1, friction 0, combine Maximum/Minimum) |
| player zones | 1.5 m deep standing pad at each end (Z = -6..-4.5 and +4.5..+6), coloured per player; a 0.05 m line at Z = +-4.5 |
| goals | the whole back wall of each end (3 x 3 m) is the goal plane: trigger volume 0.3 m thick at Z = -6 / +6; glowing grid face (reuse `SandboxToolGoalLarge` face material, frame scaled to 3 x 3 m via its 4 corner bones) |
| serve points | (0, 1.3, -3.7) for player 1, (0, 1.3, +3.7) for player 2 |
| ball | zero-gravity sphere r ~0.1 m (TennisBall prefab with `useGravity=false`, drag 0, angDrag 0.5, or procedural glowing sphere) with a TrailRenderer |
| spectator area | outside the tube along one side, raised 0.5 m, glass between |
| scoreboard | above each goal (own score large, opponent small) |

---------------------------------------------------------------------------------------------------------------------

## 4. Soccer

### 4.1 Bundle content

`(SoccerBall)` - TOOLS key **`00`**, root -3401908588561202286, **root scale 0.65**.

| Component | values |
| --- | --- |
| SphereCollider | radius 1.1 x 0.65 = **0.715 m radius (1.43 m diameter: a giant ball)**, material `SoccerBall` (TOOLS #-5561293403297317616): friction dyn 0.3 / stat 0.2, **bounciness 0.6**, Average/Average |
| Rigidbody | mass **0.75**, drag 0, angularDrag 0.15, Continuous |
| Tool | **CanBePickedUp 0** (cannot be grabbed: hit/kick it), MaxThrowSpeed 35; Local/RemoteAttackAppliedAudio `Soccer_HitBallHeavy_01` (245220f5..., vol 0.3) |
| SoccerBall MB | **minLocomotionAttackForce 20, maxLocomotionAttackForce 20** (impulse when a player body/hand "attacks" the ball) |
| CollisionAudio | Bounce: `Soccer_BallHitsFloor_01` (0a532444...) layers 18432, min 0.5, pitchVar 0.131; Impact: `Impact01` (TOOLS) layers 512 |
| Mesh child | scale 10: reuses `DodgeBall` mesh (ART) with `Soccerball_mat` (TOOLS #2601064451235989859, AG/Standard-Opaque-Decal, SoccerBall_Tex + Soccer_Ball_Decal) -> 0.110 x 10 x 0.65 = 0.715 m radius |
| Trail | TrailRenderer 2 s `VFX_Spark_mat`, visible after attack applied |
| SoccerFireTrail | inactive ParticleSystem + Smoke (power shot fire trail) |
| CameraFocusTarget | inactive helper cube for spectator camera |

`(SoccerShield)` - TOOLS key **`28`**, root 5308522682032217984: a hand-held round shield players carry to bat the
ball. Rigidbody mass 10; Tool LockOnPickup 1, handedness 1, MaxThrow 10; MeleeWeapon (canBlock...);
TeamColorRenderer (`_BaseColor` per team); mesh `Shield_LOD0` (ART #-5393678660752940095, extent 0.370x0.049x0.371
-> **0.74 m diameter disc**) mat `SoccerShield_Mat 3` (AG/Standard-Transparent, base colour (0.24,0.79,1)); convex
MeshCollider (Shield_LOD1) material `SoccerEnvironment` (TOOLS #-1306352466029638245: friction 0.3/0.2, bounciness 0.6,
frictionCombine Maximum, bounceCombine Minimum).

No soccer goal / arena / power-up prefab exists in the bundles (the Soccer_PickupCollected02 clip and the
`powerupPickupSFX` field prove power-ups exist in the room).

### 4.2 Rules and flow

- Room `^Soccer` (pickup room), added Aug 2016 as **"Shield Soccer"** ("Experiments Edition"); **2-6 players, two
  teams (3v3)**. Also used for impromptu Ultimate Frisbee. Screen mode supported (2018 fix).
- Players **kick an oversized ball** (1.43 m diameter) across a large indoor field into the opponents' goal. The ball
  cannot be picked up (`CanBePickedUp 0`). Ways to hit it: **teleport into it** (the teleport landing "kicks" it; where
  you land relative to the ball sets the direction), walk/run into it with the body, hit it with hands, and (Shield
  Soccer era) bat it with the shield. Bundle: `SoccerBall.min/maxLocomotionAttackForce 20` = the impulse of a
  body/teleport kick. Tip from the wiki: bank shots off the walls; on kick-off, kick the ball before it lands.
- **Match = 3 minutes** (`defaultGameDuration 180`); most goals wins; a draw is possible (VO `Tiegame`). Goal:
  `Soccer_Goal01` + `Redteamscores`/`Blueteamscores` + "Goal" VO. Countdown VO "One minute remaining", "10 seconds
  remaining".
- Kick-off: GUESS the ball is dropped from ~4 m above the centre spot after a 3-2-1 countdown (hence "kick it before
  it hits the ground"); after each goal the ball re-drops at the centre after ~3 s; players are not repositioned.
- In-game role: **walk speed 6 m/s** (fast), **teleport max 8 m, teleport delay 0.5 s**.
- **Speed boosts (power-ups): yellow orbs on the field.** Walking players run faster; teleporting players get a
  longer teleport and (wiki wording) a changed cooldown. In the Shield Soccer era the orbs charged the shield so that
  the next hit on the ball was more powerful (`SoccerFireTrail` child on the ball = the powered shot). Bundle sounds:
  `Soccer_PickupCollected02` (pickup), `Locomotion_Boost_Initiate02/Looping01/Spent01` (boost). GUESS numbers: boost
  lasts 5 s, walk speed x1.6, teleport max 8 -> 12 m and delay 0.5 -> 0.25 s; orb respawn 10 s; 4 orbs.

### 4.3 Arena layout (GUESS; the level is encrypted)

| Element | Spec (instance-local, +Z = red goal) |
| --- | --- |
| field | **24 m wide (X) x 40 m long (Z)**, artificial turf colour, centre line + 4 m radius centre circle + goal areas 10 x 4 m |
| walls | solid rebound walls all round, 3 m tall, rounded (bevelled) corners so the ball never sticks; PhysicMaterial like `SoccerEnvironment` (bounciness 0.6, friction 0.3/0.2, frictionCombine Maximum, bounceCombine Minimum); glass/net above the walls up to 8 m; roofed indoor hall 12 m high with flood lights |
| goals | recessed in each end wall: **opening 7 m wide x 3.5 m tall, 2.5 m deep**, back and roof netted (`Rope_Net_Sandbox` tiles); goal trigger box fills the recess behind the goal line; team-coloured glowing frame |
| kick-off | ball spawn (0, 4, 0); teams spawn at Z = -10 (blue) and Z = +10 (red), 3 spots each at X = -5, 0, +5 |
| boost orbs | 4 orbs at (+-8, 1, +-8) (yellow, emissive, rotating; pickup radius 1 m) |
| scoreboard | two big boards above the goals: team scores + clock (m:ss) |

The ball: mass 0.75 kg with a 0.715 m radius is very light for its size - it floats/bounces a lot (bounciness 0.6,
angular drag 0.15). Keep these values; add a max speed clamp of 35 m/s (`MaxThrowSpeed 35`).

---------------------------------------------------------------------------------------------------------------------

## 5. Disc Golf (Lake + Propulsion)

### 5.1 Bundle content

`(DiscGolfDisc)` - TOOLS key **`9d`**, root 6557311508961764802.

| Component | values |
| --- | --- |
| Rigidbody | mass **0.1**, drag 0.1, angularDrag 0.01, Continuous |
| DiscGolfDisc MB (#-1850390638078384702) flight model | airLiftModifier **0.8**; minAngularVelocityForLift **6** rad/s; linearToAngularVelocity: curve linear 0->1, minValue 0, **maxValue 45**, duration 20; angularStabilityModifier 0.9, angularStabilityPercent 0.3, angularStabilitySpeed 2; **curveScaleFactor 0.65**; ThrowAssistCurve (t0 -> 1.0, t1.0 -> 0.75), ThrowAssistMultiplier 1; beamRenderer (GlowDisc child), team -1 |
| Colliders (child "Mesh", rotated -90 X) | convex MeshCollider `Frisbee_LOD0` material `Frisbee` (TOOLS #2578015535726861189: friction 0.1/0.1, bounciness 0.15, Average) + 2 CapsuleColliders r 0.015 h 0.3048 (crossed, x and y) |
| Mesh | `Frisbee_LOD0/1/2` (ART #-4387283839739004729 / -5586055341698156345 / 6066319044779079879; extent 0.151 x 0.151 x 0.014 -> **0.30 m diameter, 2.8 cm thick**), mat `SportDisc_Mat` (TOOLS #-6223015986723410147) recoloured per team via TeamColorRenderer (`_BaseColor`) |
| Tool | pickup, grip R pos (-0.152,-0.006,0.034) (disc held by rim); PickupAudio `Catch01`; ThrowAudio `Throw01` vol 0.5; screen config `DiscScreenToolBehaviorConfig` (ART #3372446327481663969) |
| CollisionAudio | "Static": `DiscImpact` (c880b21c..., key `8`), layers 3073, pitchVar 0.2 |
| DiscNetworkTransform | extrapolatesCurve/Drag/Lift 1 (network prediction uses the flight model) |
| Trails | `Trail` (inactive, `DiscGolfDisc_Trail_Mat`) and `LaserTrail` (active, `DiscGolfDisc_Laser_Trail_Mat`, cyan), lifetime 2 s, widths 0.01->0.3 |
| GlowDisc | inactive beam: `GlowGoal` mesh (FX #2536098510781299611) scale (0.5,0.5,82.63) = a ~50 m tall red light column (GUESS: marks where the disc lies; `DiscGolfDisc.beamRenderer`) (`DiscGolfGlow_Mat`, AG/Unlit-Additive-Soft) |

`(Sandbox_DiscGolfGoal)` - TOOLS key **`c`**, root 3633526089731069111 (root rotated -90 X: local Z = up).

| Part | values |
| --- | --- |
| DiscGolfGoal MB | **hole 0, par 3**; beamRenderer -> GlowGoal; OnGoalHitEvent |
| FrisbeeGoal mesh | TOOLS #7878212149386823761, extent 0.298 x 0.298 x 1.216 -> **0.60 m wide, 2.43 m tall** basket post; 3 materials Paint_Orange / Paint_LightGrey / Metal_LightGrey_Dull |
| Colliders | pole 0.05x0.05x2.43; basket tray 0.66 x 0.60 x 0.26 centred 0.66 m up; top plate 0.52 x 0.53 x 0.06 at 1.31 m |
| Chains / Flag | `FrisbeeGoal_Chains` + `FrisbeeGoal_Flag` SkinnedMeshRenderers with Unity **Cloth** (class183) - cloth works at runtime; flag at ~2.26 m |
| GlowGoal | inactive; scale (1,1,110) of the 0.61 m GlowGoal mesh = ~67 m tall yellow beam (`DiscGolfGoalGlow_Mat`) to show the hole from afar |
| Rigidbody | mass 1e-7 (kinematic pickup), SimpleVolume 0.6x0.6x2.5 |

Real Rec Room disc golf basket = this mesh (the room used the same asset). Hand poses `Hand_Pose_FrisbeeGrip.L/.R`
(ART). Disc skins: `DiscGolfDisc_Pie` (bundle 6d3223da354de646ab79d5660dcac9d2), `DiscGolfDisc_Skin_ArcheryTarget`
(bundle 30d07e20c6ea09f051ef337956758489), Laser / Sewer_Lid textures (SKINS).
Nature props for a lake course: `(PineTree_Dead_Sandbox)Variant` (TOOLS -4490394049085640736; dump `SP\sports\n.txt`)
and other Sandbox trees/rocks (section 1.6).

### 5.2 Rules and flow

- Rooms `^DiscGolfLake` and `^DiscGolfPropulsion` (pickup rooms). **1-4 players**, **9-hole course**
  (`firstHole 0 .. lastHole 8`); throw the disc from each tee to the hole's basket in as few throws as possible.
  History: launch 2016; full 9 holes + disc "curve" physics Sep 2016; **scoreboards at each tee** and the **mercy
  rule** Oct 2016; Propulsion map ("Matter Propulsion Test Range, Rec Room Science Dept.") Mar 2017; **Co-op Catch**
  mode May 2017. Completing a round could award cosmetics (Lake: Golfer Set; Propulsion: Mad Scientist Set) - skip.
- Turn flow (reconstructed; GUESS where noted): everyone starts at the tee of hole 1 and throws (no strict turn order,
  GUESS). You then teleport/walk to your disc, which marks its resting place with a tall red light beam (`GlowDisc`,
  GUESS: shown while the disc rests) and throw again from there. You can grab your disc from 1.5 m away
  (`gravityPickupDistanceOverride`). **A throw counts as a stroke only if the disc travels more than 2.5 m**
  (`strokeIncrementThreshold` - so fumbles don't count; GUESS on exact meaning). The hole ends for you when the disc
  lands in the basket (`DiscGolfGoal` hit -> `[VFX_DiscGoalRays]` + `CrowdCheer01`; score label from the table in 1.1
  relative to par). **Mercy rule: at 7 strokes on a hole (`mercyRuleHoleScore 7`) you are moved on to the next tee.**
  When all players have holed out, everyone moves to the next tee (teleport). Lowest total after 9 holes wins.
- **Hazards:** water (Lake) and hazard panels / out-of-bounds (Propulsion) "hazard" the disc (`playerOutSFX` =
  OutSFX): GUESS: the disc returns to where it was thrown from and +1 penalty stroke. A watch "Hazard" button let a
  player hazard a stuck disc manually (2017 patch notes) - map to key **G** / a watch button.
- **Minus-stroke bonus panels / power-ups:** Lake has "a few minus-stroke bonus panels"; Propulsion has power-ups
  (`powerupHitSFX` = "DiscGolf Propulsion SFX - Powerup 1"; a 2017 patch fixed "powerups wouldn't reset after a
  private match", so they are consumed per match). Implement: a floating ring/panel; the disc passing through it gives
  -1 stroke on that hole, once per player per match. Scores below par like "-17 total" appear in videos, so bonuses
  can stack.
- **Propulsion obstacles:** fan panels (blow the disc: apply force along the fan axis inside its volume), hazard
  panels (touch = hazard), moving wall panels (kinematic pistons), climbable platforms (player can climb to throw from
  height).
- **Co-op Catch mode** (`DiscGolfCoopCatch` wrapper, same manager): up to 4 players throw and catch the disc as many
  times in a row as possible without it touching the ground; each catch plays `DiscGolf_Catch_01` +
  `[VFX_Charades_Goal]` and shows a notification whose size grows with catch distance beyond 10 m (+1 scale per 12 m:
  `catchNotificationDistanceBeforeScaling 10`, `catchNotificationMetersPerScaleFactor 12`).

### 5.3 Flight model (from `DiscGolfDisc`, re-implement in our FixedUpdate)

The component is a missing script at runtime, so we implement lift/curve ourselves with its parameters (GUESS on the
exact formulas; the names are Rec Room's):
- spin: on release set angular velocity about the disc's up axis to `linearToAngularVelocity(speed)` = speed
  mapped linearly 0 -> 0 ... 20 m/s -> 45 rad/s (curve 0..1 over `duration 20`, `maxValue 45`).
- lift: while spin > `minAngularVelocityForLift 6` rad/s, add `F = airLiftModifier(0.8) * 0.5 * rho * v^2 * A * CL(alpha)`
  along the disc normal (A = pi * 0.15^2 = 0.0707 m^2, rho 1.2); simple CL = 0.15 + 2.5 * alpha (rad). Rigidbody drag
  0.1 handles drag.
- stability: each step slerp the disc's up vector toward world up by `angularStabilitySpeed 2` x dt x
  `angularStabilityPercent 0.3`, and multiply off-axis angular velocity by `angularStabilityModifier 0.9`.
- curve (fade): add sideways acceleration proportional to bank angle x `curveScaleFactor 0.65` (an anhyzer/hyzer
  release curves; Sep 2016 added "curve").
- throw assist: multiply release speed by `ThrowAssistCurve(t)` (1.0 at t=0 -> 0.75 at t=1 s of hold? GUESS) x
  `ThrowAssistMultiplier 1`.
- network: `DiscNetworkTransform` extrapolates curve, drag and lift - our remote puppets should run the same model
  between samples (networking doc 5.5.3, prop int 11 "flight model flags").

### 5.4 Course layouts (GUESS; the level is encrypted)

**Lake (beginner).** Outdoor lakeside park, sunny. Suggested 180 x 180 m terrain around a 70 x 50 m lake, low hills,
trees (`Sandbox_FlatTree*`, pines), rocks, log benches, wooden fences, a small bridge/pier (procedural planks). Holes
(tee -> basket distance, par): 1: 25 m par 3 (straight, open) - 2: 35 m par 3 (dogleg left around trees) - 3: 30 m
par 3 (over a lake inlet: water hazard) - 4: 45 m par 4 (along the shore) - 5: 20 m par 3 (island green with a bonus
panel on the way) - 6: 40 m par 3 (uphill) - 7: 30 m par 3 (between rocks) - 8: 50 m par 4 (across the lake, bonus
panel over the water) - 9: 35 m par 3 (back to the start). Total par 29. Tee = 3 x 2 m concrete pad with a vertical
scoreboard (player rows x 9 holes) and a hole sign; basket = `(Sandbox_DiscGolfGoal)` with `hole` = index, `par`.
**Propulsion (advanced).** Sci-fi test range (white/orange lab panels, glass, hazard stripes). Nine holes built from
walls, ramps and platforms (procedural boxes), with fan panels (2 x 2 m emissive grate + `[Sandbox_Wind_Wavy_VFX]`,
force 15 m/s^2 along the normal inside a 2 x 2 x 6 m volume), red hazard panels, moving walls (`PistonGizmo`-style
kinematic boxes, 2 m travel, 3 s period), climbable platforms (ladder + 3 m ledge), and blue -1 power-up rings.

---------------------------------------------------------------------------------------------------------------------

## 6. Bowling

### 6.1 Bundle content

`(BowlingBall)` - TOOLS key **`a5a`**, root 8279500870475957079.

| Component | values |
| --- | --- |
| Rigidbody | mass **8**, drag 0, angularDrag 0, ContinuousDynamic |
| BowlingBall MB (#9198869001968010071) | AngularVelocityThrowMultiplier **1.5**, InertiaMultiplier **2**, LerpFrictionSpeed 0.75, MaxDepenetrationVelocity 1, YUpVelocityScalar **0.5** (halves upward release velocity), rumble clip `Bowling_BallRoll02` (TOOLS, 3.9 s loop) volume by speed^2 curve 0 -> 1 at speed^2 = 6 |
| SphereCollider | radius **0.125** (0.25 m ball), material `BowlingBall` (TOOLS #-4770988851412492563): dyn friction 0.14, static 0, bounciness 0.25, Minimum/Minimum |
| Tool | throwSpeedMultiplier **1.1**, MaxThrowSpeed **15**; PickupAudio `Bowling_Pickup01`; screen config `BowlingBallScreenToolBehaviorConfig` #-2239386326294135621 |
| CollisionAudio #1 | Floor `Bowling_Thud01` (d173e9c6..., key `c`) layers 2048; **tag `CollisionSfx/Bowling/Backboard`** -> `Bowling_BackboardThud01`; **tag `CollisionSfx/Bowling/Gutter`** -> `Bowling_Gutter01` |
| CollisionAudio #2 | **tag `CollisionSfx/Bowling/Lane`** -> `Bowling_MegaThud01` (hard lane impact / dropped ball) |
| Mesh | `BowlingBall` (TOOLS #4491958945375340421, extent 0.124 -> 0.248 m), mat `BowlingBall_Mat` (base colour cyan (0,0.77,1)); skins Gold / SciFiCarpet (SKINS) |

`ThrowBowlingBallToolBehaviorConfig` (screen-mode bowling, dump `SP\sports\bowlscreen.json`): wind-up + release
player animations, **minChargeTimeForValidThrow 1 s**, releaseSpeedCurve peaks between 0.275 and 0.345 of the swing.

`(SandboxBowlingPin)` - TOOLS key **`a2a`**, root -6290344451893559088, **root scale 1.5**.

| Component | values |
| --- | --- |
| Rigidbody | mass **1**, angularDrag 1, ContinuousDynamic |
| Colliders (x1.5) | base box 0.055x0.1x0.055 @y0.05 (material `BowlingPin`: friction 0.6/0.6, bounciness 0.6, bounceCombine Maximum); capsule r0.04 h0.38 @y0.19 (neck); capsule r0.076 h0.2 @y0.12 (belly) |
| BowlingPin MB | lane null, tool; BowlingPinCircuitOutput: outputs "is upright" + "angle from upright" |
| Mesh | `BowlingAlley_Pin` (#6740480237363982306, extent 0.062 x 0.183 -> 0.124 x 0.366 m; x1.5 = **0.185 x 0.55 m**), LOD1; mat `Bowling_Pin_Mat` |

NOTE: at 1.5 scale the pin is 0.55 m tall vs a real 0.38 m pin; the unscaled mesh (0.366 m) matches a real pin.
GUESS: the official alley used scale 1.0 pins with the 0.25 m ball (real 0.218 m); choose 1.0 for a regulation look,
1.5 for the sandbox look.

Bowling alley decor (TOOLS): `(BowlingAlley_Jukebox_Sandbox)Variant` key `48` (root 9197836360092917472; RecRoom.Radio
with tracks list, AudioSource vol 0.1 range 1-100, `pauseDuringGameInProgress` 1; scene position in its source room
(-1.863, 0.387, 32.997) rot Y -91.6 deg); `(BowlingAlley_Stool)SandboxVariant` key `8a` (7391367745982158483, seat
0.46 m); pool table set meshes `BowlingAlley_PoolTable_fbx`, `_PoolBall_fbx`, `_PoolCue_fbx`, `_PoolRack_fbx`,
`BowlingAlley_PoolCueRack` (TOOLS; atlas `BowlingAlley_Assets_Mat` with `BowlingAlley_Assets_tex/Norm/Spec`);
`(Sandbox_Lockers)` key `d5c`. Bowling shirts are avatar clothing (ignore: avatar is never modified).

**Not in bundles:** lanes, gutters, pin deck, pinsetter/sweeper, ball return, score monitors. Procedural (section 9.5).

### 6.2 Rules and flow

- Room `^Bowling` = **"Lone Shoe Lanes"**, released 11 Oct 2018 (pickup room). **One scoring lane (the middle one)
  for up to 6 bowlers**, plus **two practice lanes**, room for a few spectators, a **pool table** and (bundle) a
  **jukebox** and bar stools. **Fully automated scoring.**
- Standard ten-pin: **10 frames**, up to 2 rolls per frame; strike = all 10 on the first roll (bonus: next 2 rolls);
  spare = all 10 with 2 rolls (bonus: next roll); 10th frame: strike -> 2 more rolls, spare -> 1 more roll (bonus
  only). Max 300. Players bowl in turn order, one frame each.
- Pick up a ball with the trigger/grip and release to throw (VR); screen players use the charge/timing throw
  (`ThrowBowlingBallToolBehaviorConfig`, section 1.5). **Other players cannot grab your ball right after you throw
  it** (2018 patch). You get a reminder to bowl again after a strike/spare in the 10th frame (2018 patch).
- Pin fall detection (Rec Room exposes it on the sandbox pin: "is upright" + "angle from upright"): GUESS a pin is
  down when tilted more than 30 deg from upright or displaced > 0.15 m from its spot, evaluated once the pins have
  settled (all pin speeds < 0.05 m/s or 6 s after the ball passed the head pin).
- Sounds: roll rumble (`Bowling_BallRoll02` looped, volume by speed^2), gutter (`Bowling_Gutter01`, collider tag
  `CollisionSfx/Bowling/Gutter`), backboard/pit (`Bowling_BackboardThud01`), dropped ball on the approach
  (`Bowling_MegaThud01`, tag `CollisionSfx/Bowling/Lane`), strike stinger `Bowling_MegaStrike01`, sweeper
  `Bowling_Sweeper01`, pin clatter `SFX_Collision_Wood1-3_Mono` (GUESS), crowd `SR_Crowd_Aww_01` for gutter balls,
  music `Bowling_MusicGameplay` / jukebox tracks.

### 6.3 Pin deck and machine cycle (implementation spec)

- **Pin spots** (regulation, 12 in = 0.3048 m between adjacent spots, triangle pointing at the bowler; lane-local
  X across, Z along the lane from the head pin): pin 1 (0, 0); pins 2,3 (-+0.1524, 0.264); pins 4,5,6 (-0.3048, 0,
  +0.3048 at Z 0.528); pins 7-10 (-0.4572, -0.1524, +0.1524, +0.4572 at Z 0.792).
  (row spacing = 0.3048 x sin 60 = 0.264 m). If the sandbox pin scale 1.5 is used, scale the spots by 1.5 too.
- **Cycle** after each roll: (1) wait for settle (above) -> (2) sweeper bar drops in front of the pins (0.5 s,
  `Bowling_Sweeper01`) -> (3) the rake lifts standing pins (store their spots), sweeps fallen pins into the pit (make
  them kinematic and animate/despawn) -> (4) second roll: lower the standing pins back on their spots; frame over or
  strike: lower a full new rack -> (5) sweeper lifts (total cycle ~4-5 s).
- **Ball return:** the ball falls into the pit behind the deck, is despawned and reappears ~3 s later rolling onto the
  ball rack at the approach (animate along a return track from the pit under the lane to the rack; rumble sound).
- Pins are simulated only on the bowler's client and streamed (networking doc 5.5.5, PinStream event); the lane result
  (standing mask, frame scores) goes into the instance ints.

### 6.4 Alley layout (partly evidenced, rest GUESS)

Evidence: the sandbox variants keep their source-scene transforms: jukebox at (-1.863, 0.387, 32.997) rotated
-91.6 deg about Y, a bar stool at (-6.57, -0.044, -0.778). So the room spans at least ~34 m along Z, consistent with
**regulation-length lanes** (18.29 m foul line to head pin) plus approach, seating and machine area. GUESS layout:

| Element | Spec (room-local, Z along the lanes, foul line at Z = 0, bowlers at Z < 0) |
| --- | --- |
| lanes | 3 lanes side by side: practice (X = -3.2), **scoring lane (X = 0)**, practice (X = +3.2); each **1.066 m** wide wood (maple look), **gutters 0.235 m** each side (half-pipe, 0.1 m deep, tagged Gutter), 0.3 m capping between lanes; head pin at Z = 18.29; pin deck to Z = 19.2; pit Z = 19.2..20.2 (0.4 m drop, backboard curtain at Z = 20.2 tagged Backboard) |
| approach | Z = -4.6 .. 0, same wood, foul line 0.01 m dark stripe; lane surface tagged Lane |
| ball rack / return | one per lane at X = lane +- 0.9, Z = -3.5: hood + rack holding 5 balls (`(BowlingBall)` recoloured via `_BaseColor`: blue, red, green, purple, orange) |
| settee / bowlers' area | Z = -9 .. -5: curved bench for 6 + score console; overhead score monitors above the approach (Z = -1, Y = 3.2) showing the 10-frame sheet for all bowlers |
| pinsetter hood | Z = 18.0 .. 20.5, 1.6 m high masking unit across all lanes with the "Lone Shoe Lanes" style sign (our own text) |
| lounge | behind the settee (Z = -15 .. -9): pool table `(PoolTable)` key `ee` + cues `(PoolCue)` key `4f` + rack `(PoolTriangle)` key `ad`, jukebox `48` against the side wall, bar stools `8a`, space carpet `Scifi_Carpet_Mat` |
| walls/ceiling | room 16 m wide x 38 m long x 5 m high; dark walls with neon trim; ceiling lights over the approach; darker over the lanes with lights on the pin decks |

---------------------------------------------------------------------------------------------------------------------

## 7. Basketball (toy)

`(Basketball)` - TOOLS key **`d2`**, root 5755647178341420997.
- Rigidbody mass **2**, drag 0.1, angDrag 0.1, Continuous. SphereCollider r **0.124** material `Basketball`
  (TOOLS #-5530415985010047082: friction 0.8/0.8, **bounciness 0.8**, Average).
- Tool: PickupAudio `basketballPickUp` (0.5), ThrowAudio `Throw01`, screen config `ThrowScreenToolBehaviorConfig`.
- CollisionAudio: `BasketballBounce01` (layers 3072), tag `CollisionSfx_Table` -> `BasketballHitRim01`.
- Basketball MB: PassedRimAudio `DingDing` (cc5b30c7..., key `9`, vol 0.7), PassedRimParticle
  `[VFX_Basketball_PassedRim]`, FireTrail particle.
- Meshes `Basketball_LOD0/1/2` (#7446308437720859531, extent 0.124 -> 0.248 m), mat `Basketball_Mat` (ART); skins
  NBA Logoman / NBA 75 / CuteCat / BeachBall (inactive children).

`(Basketballhoop)` - TOOLS key **`78`**, root 6058454435073796307 (kinematic).
- `Goal_LOD0/1` backboard+rim mesh (#-3297046497160156249, extent 0.914 x 0.308 x 0.610 -> **1.83 x 1.22 m
  backboard** (regulation 6 x 4 ft)); mat `BasketBall_Goal_Recolor_Mat` (**AG/Standard-Opaque-Instanced: needs the
  renderQueue 2501 depth-priming fix**).
- Backboard BoxCollider 1.8288 x 0.0254 x 1.2192 material `BasketballBackboard` (bounciness 0.45, Minimum).
- Rim: `Rim.ProxyOuter` (scale 1.03) + `Rim.ProxyInner` MeshColliders (`Rim.Proxy` mesh extent 0.249 -> ~0.50 m
  ring (regulation 0.457 m)), inner material `BasketballRimInner` (bounciness 0.05); `BrickCollision` box.
- Layer 11 on the rim group. The hoop has no pole; mount it on a procedural pole/wall at rim height 3.05 m.
- Hoop "score" detection is done by the ball (PassedRim): implement a trigger below the rim.
- Basketball in Rec Room is a toy (Rec Center and dorms have balls and hoops; there is no official basketball game;
  community rooms such as `^SaintsBasketBallArena` exist). The `BasketballThrowToolBehaviorConfig` (1.5) shows how
  screen players shoot: throw toward the gaze target with a pitch lift (looking level = 70 deg lob, 12 m/s).
- Toy rules for us: a hoop mounted at rim height **3.05 m** on a wall or pole; a swish/make = ball centre passes
  downward through a trigger disc (r 0.2 m) 0.15 m below the rim plane -> `DingDing` (vol 0.7) +
  `[VFX_Basketball_PassedRim]`. Optional local "shots made" counter. Balls respawn to a rack after 15 s idle
  (`ToolCleanup` defaults: 15 s delay, 3 m player distance, 0.1 m displacement).

---------------------------------------------------------------------------------------------------------------------

## 8. Sounds / VFX / music master table

### 8.1 Audio clips

Columns: clip | bundle | clip pathId | container key (`-` = no key: implicit, becomes resident when a prefab/manager that
references it is loaded - e.g. load the wrapper circuit or the ball prefab first, then find it with
`Resources.FindObjectsOfTypeAll<AudioClip>()` by name) | length s | channels | loadType (0 DecompressOnLoad,
1 CompressedInMemory, 2 Streaming) | use.
Machine-readable copies: `SP\research\sports_data\clipfull.tsv` (name, bundle, pathId, key), `SP\research\sports_data\clipinfo.tsv` (lengths).

| Clip | Bundle | pathId | key | s | ch | load | Use |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 10 seconds remaining | `063b6b6653aa642616fd21d3d4701899` | 8067635986747856111 | - | 2.00 | 2 | 1 | announcer VO (timed games: soccer) |
| ActivityTitleJingle | `063b6b6653aa642616fd21d3d4701899` | -169042326832854528 | - | 3.09 | 2 | 2 | activity intro jingle |
| AmyVO_Generic_YouWin | `3f59859587ec9cf3ef9a55a503310ee9` | 1085161917782553087 | `b` | 0.94 | 2 | 1 | VO "You win" (paddleball 1v1 / bowling winner) |
| AttentionWhistle | `155d17805c3b91085a1f9f4e18e28f4c` | 2441349739855970044 | `1` | 0.50 | 1 | 0 | short whistle (kick-off, foul) |
| BasketballBounce01 | `43ea1176ca25c0333547813ec3298808` | 4467403410159767833 | - | 0.29 | 2 | 0 | Basketball CollisionAudio floor |
| BasketballHitRim01 | `43ea1176ca25c0333547813ec3298808` | -8057379618765553001 | - | 0.75 | 2 | 1 | Basketball rim (tag CollisionSfx_Table) |
| Blue Team | `43ea1176ca25c0333547813ec3298808` | -1273163103982826951 | - | 1.50 | 2 | 1 | VO team announce |
| Blueteamscores | `3ac9d6486250045ffd4aca7344fc91d4` | 5666518060238787045 | `a` | 2.00 | 1 | 1 | Soccer blueTeamScoreSFX |
| Blueteamwins2 | `917f6f76fe90601bcf46d25445d9d4d1` | 1534306606037199495 | `5` | 2.00 | 1 | 1 | VO end of match |
| Bounce03 | `a1ca25a9d146b0378081382720c774c4` | -4218819489703360599 | `4` | 0.64 | 1 | 1 | DodgeballBall CollisionAudio |
| Bowling_BackboardThud01 | `43ea1176ca25c0333547813ec3298808` | -97151630081898089 | - | 1.00 | 1 | 1 | BowlingBall tag CollisionSfx/Bowling/Backboard |
| Bowling_BallRoll02 | `43ea1176ca25c0333547813ec3298808` | 949109324821287978 | - | 3.94 | 1 | 2 | BowlingBall rumble loop (volume by speed^2) |
| Bowling_Gutter01 | `43ea1176ca25c0333547813ec3298808` | -6980963308601379002 | - | 1.52 | 1 | 1 | BowlingBall tag CollisionSfx/Bowling/Gutter |
| Bowling_MegaStrike01 | `d8f39f5f61b670905163fd15a494b936` | -5622660627512024886 | `c` | 2.01 | 2 | 2 | strike stinger |
| Bowling_MegaThud01 | `43ea1176ca25c0333547813ec3298808` | -8519458992720055254 | - | 1.03 | 2 | 1 | BowlingBall tag CollisionSfx/Bowling/Lane (hard drop) |
| Bowling_MusicGameplay | `524b224f6b39a4d1aa21a94a9fbc2337` | -6912712655756359781 | `6` | 104.84 | 2 | 2 | bowling background music |
| Bowling_Pickup01 | `43ea1176ca25c0333547813ec3298808` | 1463225188533934318 | - | 0.75 | 1 | 1 | BowlingBall PickupAudio |
| Bowling_Sweeper01 | `f9ec7076558375aae47ded317a48f738` | -6701904002155871659 | `6` | 2.13 | 1 | 2 | pinsetter sweeper cycle |
| Bowling_Thud01 | `d173e9c68bec88f3c2bfced12b250f01` | -6575676354538734675 | `c` | 0.90 | 1 | 1 | BowlingBall floor thud |
| Catch01 | `063b6b6653aa642616fd21d3d4701899` | -3131456990773043 | - | 1.61 | 2 | 1 | DiscGolfDisc/BowlingPin PickupAudio, dodgeball catch |
| Catch01 | `43ea1176ca25c0333547813ec3298808` | -3131456990773043 | - | 1.61 | 2 | 1 | DiscGolfDisc/BowlingPin PickupAudio, dodgeball catch |
| Catch01 | `85515de19d04f3c4dfe704e6f02046e3` | -3131456990773043 | - | 1.61 | 2 | 1 | DiscGolfDisc/BowlingPin PickupAudio, dodgeball catch |
| Charades_TeamScoreUp | `43ea1176ca25c0333547813ec3298808` | -111350679483700582 | - | 2.55 | 2 | 2 | generic team score up |
| CrowdCheer01 | `43ea1176ca25c0333547813ec3298808` | -1865890325946663720 | - | 6.31 | 2 | 2 | Paddleball goalSFX, DiscGolf goalSFX |
| DingDing | `cc5b30c7397eaaaa29b44e823663a6f4` | -463502042293685994 | `9` | 3.00 | 1 | 2 | Basketball PassedRimAudio |
| DiscGolf Propulsion SFX - Powerup 1 | `43ea1176ca25c0333547813ec3298808` | -2997314221359343725 | - | 4.21 | 2 | 2 | DiscGolf powerupHitSFX |
| DiscGolf_Catch_01 | `f8909f400d5b748d20c2222b985c47da` | 8870581045555355433 | `2` | 0.87 | 2 | 1 | DiscGolf catchSFX (co-op catch) |
| DiscImpact | `c880b21ca7f7b8485d5be66a9bcbb19a` | 5551713251715769855 | `8` | 0.68 | 1 | 1 | DiscGolfDisc CollisionAudio |
| DodgeBaller | `3be527316db9cfb62f25d7d16854a03e` | 5707552414495990794 | `f` | 150.31 | 2 | 2 | Dodgeball music |
| Goal | `f31fc5a1f262ad0186a9ec29c122cdbb` | 6157485182715445792 | `7` | 2.00 | 2 | 1 | VO "Goal" (paddleball, soccer, SandboxToolGoal) |
| GolfClap | `cf87e10edb48ff524b56e0701ca07178` | 9097481727845543271 | `9` | 5.47 | 1 | 2 | disc golf polite applause (par / hole done) |
| Hit | `0933d7bb1b2ab92a3f410ed9ad4b3f78` | -1202248016175986536 | `0` | 0.62 | 2 | 1 | Dodgeball playerScoreVO |
| HitSound01 | `43ea1176ca25c0333547813ec3298808` | 6451015365694038197 | - | 4.89 | 1 | 2 | Dodgeball playerScoreSFX |
| Horn01 | `b78dc502268a64a8a0693a27bf6b3bcc` | -316736602211080581 | `1` | 0.25 | 1 | 0 | Dodgeball roundOverSFX (d9e43916 2.45 s); short horn (b78dc502) |
| Horn01 | `d9e439167b73be7c547ad553844715e9` | -7173929055174883294 | `0` | 2.45 | 2 | 2 | Dodgeball roundOverSFX (d9e43916 2.45 s); short horn (b78dc502) |
| Impact01 | `43ea1176ca25c0333547813ec3298808` | 7265352653502151123 | - | 1.50 | 2 | 1 | SoccerBall Impact (layer 9 mask 512) |
| Kiosk_SelfieCountdownBeep | `16079ee01a0c29220760d8ebfcde3a67` | 3644824455668649059 | - | 0.21 | 2 | 0 | countdown beep |
| Locomotion_Boost_Initiate02 | `43ea1176ca25c0333547813ec3298808` | -6491063285510159844 | - | 1.99 | 1 | 1 | speed boost start (soccer boost orb) |
| Locomotion_Boost_Looping01 | `43ea1176ca25c0333547813ec3298808` | -4379981765045079288 | - | 8.00 | 1 | 2 | speed boost loop |
| Locomotion_Boost_Misfire01 | `43ea1176ca25c0333547813ec3298808` | 6764848017567955167 | - | 0.63 | 1 | 1 | boost misfire |
| Locomotion_Boost_Spent01 | `43ea1176ca25c0333547813ec3298808` | 2953076619741688355 | - | 2.04 | 1 | 2 | boost end |
| MatchOver01 | `34d38287683c32697ba1ca1e0e0c51f8` | -7587647799770657140 | `4` | 2.99 | 2 | 2 | match over stinger |
| One minute remaining | `063b6b6653aa642616fd21d3d4701899` | 7283794961762533954 | - | 2.00 | 2 | 1 | announcer VO (soccer) |
| Out | `43ea1176ca25c0333547813ec3298808` | -4456959885725097216 | - | 2.00 | 2 | 1 | Dodgeball playerOutVO |
| OutSFX | `43ea1176ca25c0333547813ec3298808` | 7834946154998007773 | - | 1.38 | 1 | 1 | Dodgeball playerOutSFX, DiscGolf playerOutSFX (hazard) |
| Pool_BallEntersPocket_01 | `0be89c4ffcfb4b27a667c8f1390cbf9e` | -1408865243768810731 | `6` | 0.77 | 1 | 1 | pool (bowling alley pool table) |
| Pool_BallHitsBallLoud_01 | `a7cc02c47c457ad14564802a1953ed38` | 4929652228747482188 | `5` | 0.55 | 1 | 1 | pool |
| Pool_BallHitsBallQuiet_01 | `43ea1176ca25c0333547813ec3298808` | 4869652709913115368 | - | 0.57 | 1 | 1 | pool |
| Pool_BallHitsCushion_01 | `51a2e9dcf113da54302d8c7be6208932` | 3550569257351873272 | `4` | 0.30 | 1 | 0 | pool |
| RacketHit01 | `43ea1176ca25c0333547813ec3298808` | -311142949529028149 | - | 0.79 | 1 | 1 | Paddleball_Paddle CollisionAudio |
| Ready | `85515de19d04f3c4dfe704e6f02046e3` | 1767122124434549023 | - | 1.13 | 1 | 1 | VO countdown |
| RecRoomTheme2 | `b203e635dad693c4bb407e11f8d8add6` | -4000194044881505769 | `9` | 60.55 | 2 | 2 | lobby/menu music |
| Red Team | `43ea1176ca25c0333547813ec3298808` | -8532761083206627114 | - | 1.50 | 1 | 1 | VO team announce |
| Redteamscores | `2467f3cb45a1985a4ee8b447f4e7f1db` | -7302563003478606809 | `4` | 2.00 | 1 | 1 | Soccer redTeamScoreSFX |
| Redteamwins2 | `07668113e65085e893d9e031895118ba` | -214604016597554931 | `a` | 2.00 | 1 | 1 | VO end of match |
| SFX_Collision_Wood1_Mono | `43ea1176ca25c0333547813ec3298808` | -570680566232350095 | - | 0.30 | 1 | 0 | wood impacts (pins on deck GUESS) |
| SFX_Collision_Wood2_Mono | `43ea1176ca25c0333547813ec3298808` | 8791055592314624955 | - | 0.35 | 1 | 0 | wood impacts |
| SFX_Collision_Wood3_Mono | `43ea1176ca25c0333547813ec3298808` | 4607502404004397211 | - | 0.32 | 1 | 0 | wood impacts |
| SFX_ScoreboardLoop_Mono | `43ea1176ca25c0333547813ec3298808` | 7793791049907991102 | - | 8.00 | 1 | 2 | scoreboard loop |
| SFX_ScoreboardPopDown_Mono | `43ea1176ca25c0333547813ec3298808` | 4780644390827965513 | - | 0.64 | 1 | 1 | scoreboard hide |
| SFX_ScoreboardPopUp_Mono | `43ea1176ca25c0333547813ec3298808` | -3809507613895287341 | - | 0.92 | 1 | 1 | scoreboard show |
| SR_Announcer_OneMinuteRemaining01 | `43ea1176ca25c0333547813ec3298808` | -9113955684142618141 | - | 4.36 | 2 | 2 | announcer (Stunt Runner, reusable) |
| SR_Announcer_TenSecondsRemaining01 | `43ea1176ca25c0333547813ec3298808` | 5981280078990013162 | - | 4.36 | 2 | 2 | announcer |
| SR_Announcer_ThirtySecondsRemaining01 | `43ea1176ca25c0333547813ec3298808` | -1659547524144203664 | - | 4.36 | 2 | 2 | announcer |
| SR_Audience_Cheer02 | `43ea1176ca25c0333547813ec3298808` | 2157570899536906946 | - | 6.04 | 2 | 2 | crowd cheer |
| SR_Audience_Cheer03 | `6cfb34be51bf92f4e270d20eda1f61c5` | 3175557120754433836 | `f` | 10.67 | 2 | 2 | crowd cheer |
| SR_Audience_Cheer04 | `43ea1176ca25c0333547813ec3298808` | -8244167036036898340 | - | 7.77 | 2 | 2 | crowd cheer |
| SR_Buzzer_Long01 | `43ea1176ca25c0333547813ec3298808` | 8171604967208281296 | - | 1.92 | 1 | 1 | end-of-period buzzer |
| SR_Buzzer_Short01 | `412631213c9148cf7c6b9b39ca7437db` | 7689474870081883382 | `5` | 1.85 | 1 | 1 | short buzzer |
| SR_CountdownGO | `a82aea4e93c7dae70ae8e89c49f48c64` | 984585080737202074 | `b` | 1.69 | 2 | 1 | countdown GO |
| SR_CountdownTick | `64442b9f70ad081f550f2859ff055a69` | -154115121462272171 | `4` | 0.92 | 2 | 1 | countdown tick |
| SR_Crowd_Aww_01 | `b4e61aca4c54e0e28d4cfd897a12a2dc` | 411954954512058323 | `d` | 3.14 | 2 | 2 | crowd aww (gutter ball, miss) |
| SR_Fanfare01 | `43ea1176ca25c0333547813ec3298808` | 7109352028673205774 | - | 11.91 | 2 | 2 | fanfare (winner) |
| ScoreSFX | `a8a48c1d41803ec702618201fa374ba1` | 7228764702924650658 | `2` | 4.19 | 1 | 2 | score jingle |
| Soccer_BallHitsEnvironment_01 | `43ea1176ca25c0333547813ec3298808` | 3651938350198410537 | - | 0.17 | 1 | 0 | soccer ball vs wall |
| Soccer_BallHitsFloor_01 | `0a532444323bfa956df8c4a192e9a123` | 3756535714275733104 | `d` | 1.40 | 1 | 1 | SoccerBall CollisionAudio bounce |
| Soccer_Goal01 | `43ea1176ca25c0333547813ec3298808` | 4857468249237202479 | - | 7.78 | 1 | 2 | Soccer goalSFX (7.8 s horn+crowd) |
| Soccer_HitBallHeavy_01 | `245220f51e63511ec253ca3fc9e0ae5d` | 2650611460199181457 | `9` | 2.69 | 1 | 2 | SoccerBall Local/RemoteAttackAppliedAudio |
| Soccer_HitBallLight_01 | `4893bc8208a08873b3a18fc5458724e5` | 3267359303711825252 | `7` | 0.18 | 1 | 0 | soccer light touch |
| Soccer_OtherPlayersLightHit_02 | `eff0fc05b14da57b9c09f770efe665ec` | 5242984011875655852 | `b` | 0.37 | 1 | 0 | soccer remote light touch |
| Soccer_PickupCollected02 | `43ea1176ca25c0333547813ec3298808` | -5235423671436938057 | - | 0.58 | 1 | 1 | Soccer powerupPickupSFX |
| SportFunk | `db607c4f818d0dff25b3d94361e97a90` | -8860659005492720430 | `d` | 83.92 | 1 | 2 | sports music (Sport Funk) |
| SportFunkO2c (slow dance) | `ee61d0666426349289ae3758fd511a22` | 2598618057050425148 | `8` | 136.61 | 2 | 2 | sports music variant |
| SportFunkO2c (slow dance) - 10s fanfare | `63f5edf55ba2bbdbe50f7734c12a6670` | 2182454630033063547 | `4` | 10.45 | 2 | 2 | 10 s fanfare |
| SportFunkO2c (slow dance) - radio | `644d5b01259b0ca5686e0d4faa6b4cb8` | 3240965949479437590 | `a` | 136.61 | 2 | 2 | radio variant |
| Start | `85515de19d04f3c4dfe704e6f02046e3` | 946889272837270355 | - | 1.00 | 1 | 1 | VO countdown |
| Team Battle | `43ea1176ca25c0333547813ec3298808` | 720655657548790325 | - | 1.18 | 1 | 1 | VO mode announce |
| TennisBall_Impact01a | `abebfc15f7f6fa295bb9dfc835d5b4ae` | -6624284809918417228 | `3` | 0.50 | 2 | 0 | TennisBall CollisionAudio |
| TennisBall_Impact01b | `43ea1176ca25c0333547813ec3298808` | -8348869883742077951 | - | 0.50 | 2 | 0 | TennisBall CollisionAudio |
| The Winner Is | `85515de19d04f3c4dfe704e6f02046e3` | -5226112581089106014 | - | 2.50 | 1 | 2 | VO winner |
| Throw01 | `d5ff127b953bd3929f3339985e37a0af` | -3367295008890999457 | `d` | 1.49 | 1 | 1 | ThrowAudio of balls/discs (vol 0.5) |
| Tiegame | `4a14373c13bfcdcc8b456e30a2d967d7` | 953361724431557829 | `0` | 2.00 | 1 | 1 | VO tie (soccer draw) |
| Whistle01 | `ec475596729ee618da02b51597ad440c` | -8433016277149723008 | `e` | 4.25 | 1 | 2 | Dodgeball roundStartSFX |
| basketballPickUp | `43ea1176ca25c0333547813ec3298808` | -1934144089526804261 | - | 0.35 | 2 | 0 | Basketball PickupAudio |
| dodgeball_PlayerHit_01 | `cccf414cfc1c658c89d4868e63b814c5` | -864357603377735205 | `d` | 2.34 | 2 | 2 | Dodgeball playerImpactSFX |
| pingpongballhitfloor | `43ea1176ca25c0333547813ec3298808` | -5045961717320881537 | - | 0.45 | 2 | 0 | PingpongBall |
| pingpongballhitpaddle | `43ea1176ca25c0333547813ec3298808` | -6448852389865798892 | - | 0.50 | 2 | 0 | PingpongBall (tag CollisionSfx_Paddle) |
| pingpongballhittable | `43ea1176ca25c0333547813ec3298808` | 1449634175587052288 | - | 0.29 | 2 | 0 | PingpongBall (tag CollisionSfx_Table) |

Notes: `Catch01` exists as three identical copies (bundles 063b6b66..., TOOLS, 85515de1...) - any resident one works.
`Horn01` has two different clips: d9e43916... (key `0`, 2.45 s, the dodgeball round-over horn) and b78dc502... (key
`1`, 0.25 s). Stereo clips for music/VO; mono for spatial SFX. Play spatial SFX with 3D AudioSources (min 1, max
30 m); the jukebox uses vol 0.1, range 1-100, output channel 2 (music).

### 8.2 VFX prefabs (TOOLS, no container keys: resident via the manager/ball that references them)

| Prefab | root pathId | children / materials (shader) | used by |
| --- | --- | --- | --- |
| `[VFX_Default_Goal]` | -2607123032379620835 | root `Burst_01`, `Fragments` (`Flagmentt_01`), `Sparkles` (`Sparkle_01`) - all `AG/Unlit-Additive-Soft` | Paddleball goal (also good for soccer goals) |
| `[VFX_DiscGoalRays]` | -8509971318059427009 | `Water_VFX`; `VFX_DiscGoalSwirl` (`Default-Particle` @776a1791 + `WindWave_mat` @ART), `VFX_DiscGoalBeam` (`DiscGolfGoalGlow_Mat`) | disc in basket |
| `[VFX_Charades_Goal]` | 8869808536301172773 | `Burst_01`, `Fragments` | disc golf co-op catch |
| `[VFX_Basketball_PassedRim]` | 9003561391953464342 | `Burst_01`, `VFX_WeaponImpactCenter_Mat`, `Sparkle_01` | basketball make |
| `[VFX_Default_HitParticles]` | 6721327806489923429 | `VFX_WeaponImpactCenter` child (layer 11): `VFX_WeaponImpact_Mat`, `VFX_WeaponImpactCenter_Laser_Crit_Mat` (@ART) | dodgeball player hit |
| `[DodgeballFireTrail]` (child of the ball) | ball child 6532272740782921968 | `Fire_01` (@ART, AG/Unlit-Additive-Soft), loop, dur 3 | live dodgeball |
| `SoccerFireTrail` (child of the ball) | -1881405825796177006 | `Fire_01` + `Smoke` (`Smoke_01`, **Legacy Shaders/Particles/Alpha Blended**) | powered soccer shot |
| `[VFX_Impact_Particles]` (child of the paddle net) | 6807296034890748008 | Wave 0.1 s + Spark 0.3 s | paddle hit |
| `[SandboxGoalHitVfx]` | 7375694401223502914 | - | sandbox goal hit |
| `[Sandbox_BasketballRim_VFX]` | -5887031207943934196 | - | sandbox hoop |
| `[Sandbox_Wind_Wavy_VFX]` | 8024983982320971218 | - | Propulsion fans |
| Trails | - | ball `Trail` children use `VFX_Spark_mat` (@ART, **Mobile/Particles/Additive**); disc trails `DiscGolfDisc_Trail_Mat` / `DiscGolfDisc_Laser_Trail_Mat` (AG/Unlit-Additive-Soft) | thrown balls, discs |

Material fixes (from `rr_runtime_facts.md`): swap `Mobile/Particles/Additive`, `Legacy Shaders/Particles/Alpha
Blended` and the built-in `Default-Particle` to `Universal Render Pipeline/Particles/Unlit` (additive / alpha) so they
render in both VR eyes; for opaque materials whose shader has no `DepthNormals` pass (`AG/Standard-Opaque-Instanced`:
the hoop; check `AG/Standard-Opaque-Decal`: dodgeball and soccer ball) set `renderQueue = 2501`.

### 8.3 Music

| Track | bundle / key | length | where |
| --- | --- | --- | --- |
| `DodgeBaller` | `3be527316db9cfb62f25d7d16854a03e` / `f` | 150 s | Dodgeball |
| `Bowling_MusicGameplay` | `524b224f6b39a4d1aa21a94a9fbc2337` / `6` | 105 s | Bowling (plus jukebox tracks: `SP\research\dumps\tracks.tsv`, e.g. `SportFunk` db607c4f.../`d`) |
| `SportFunk` | `db607c4f818d0dff25b3d94361e97a90` / `d` | 84 s | Soccer / Paddleball GUESS (Paddleball had music: 2018 fix "Paddleball music wasn't playing") |
| `SportFunkO2c (slow dance)` / `- radio` / `- 10s fanfare` | `ee61d066...` `8` / `644d5b01...` `a` / `63f5edf5...` `4` | 137 / 137 / 10 s | variants; fanfare for match end |
| `RecRoomTheme2` | `b203e635dad693c4bb407e11f8d8add6` / `9` | 61 s | Disc Golf Lake ambience GUESS |

---------------------------------------------------------------------------------------------------------------------

## 9. Rebuild plans per arena

### 9.0 Common recipe (all sports)

1. **Instance placement:** each activity instance is built at its own origin 2 km apart (networking doc section 0,
   item 5). Everything below is instance-local.
2. **Bundles:** open TOOLS (`43ea1176...`), ART (`1df8e0ef...`), FX (`f124b399...`) and the clip bundles listed in 8.1
   with `AssetBundle.LoadFromFile` (+ `RrReach.Bundles(bundle, pathId)` for each prefab's Unity-component deps). Load
   the game-rule wrapper of the sport by key (e.g. `07` for Dodgeball) so the manager and its clips/VFX become
   resident; read their numbers with our parser (or use the values in this document).
3. **Instantiate** each prop under an inactive parent; delete Rec Room scaffolding children: `PropContainer`,
   `CircuitOutputs`/`Circuit Outputs`/`*_OSlot`/`*_ISlot`/`ArrowConnectionMesh` (layers 23/27 circuit pins),
   `RezBounds`, `AnimateInOutBounds`, `CameraFocusTarget`, `[*]_Physics` proxy objects only if our hold code does not
   need them; strip Volume/EventSystem/EventTrigger/Button; activate.
4. **Layers** (`somnium_player.md` 3.3, conventions 12): arena floors/walls on 23 (Environment); balls/discs/pins on
   26 (Bullet: hits floors on 0/23 and player hit-boxes on 27, not the avatar capsule) or 10; held props exclude
   layer 7 (client hand boxes).
5. **Physics:** keep the Rec Room Rigidbody and PhysicMaterial values (tables above). Remote copies are kinematic
   puppets (networking doc 5.5.3).
6. **Materials:** keep originals, apply the depth-priming and particle fixes (8.2).
7. **Audio:** 3D AudioSources on props; collision sounds via our own `CollisionAudio` re-implementation using the
   bundle mapping (layer mask / tag / min impact / pitch variation / volume curve), per-prop rate limit 0.15 s.
8. **Networking:** props = "Rr Net Prop" NBD12 objects leased per instance (networking 5.5); game state in "Rr Net
   Instance k" ints (5.6): phase/timer/scores/roster in ints 0-26; sport-specific ints 27-58 below; per-player sport
   state in the presence slot int 19 / ints 32-47.

### 9.1 Dodgeball

- Geometry (procedural, 2.3): floor box 15 x 0.2 x 22 m (court drawn on a 9 x 16 m texture/decals), 4 walls, ceiling,
  2 benches (boxes 0.45 m high x 0.4 deep x 7 m), centre barrier trigger (9 x 3 x 0.2 m), out-of-bounds triggers.
- Props: 3 x `(DodgeballBall)` key `1bf` (prop ids 0-2; allow up to 6 for 6 players GUESS), spawns at (+-3/0, 0.11, 0).
- Logic (instance authority): Lobby -> Countdown 3 s (`Ready`/`Start` VO, `SR_CountdownTick`/`GO`) -> Playing (teams
  on halves, balls spawned, `Whistle01`) -> RoundOver when a team has 0 players in (`Horn01`, team round score +1 in
  int 7) -> next round after 4 s, or MatchOver at 3 wins (`Redteamwins2`/`Blueteamwins2`, `DodgeBaller` stops).
- Hits: thrower-authoritative claim (networking doc: HitClaim / CatchClaim with the prop's throw id, int 9 bits
  16-31); the authority rejects a hit if a CatchClaim for the same throw id arrives first or within 150 ms. Live flag
  + thrower team in prop int 11. Player hit-box: capsule r 0.3 m, h 1.8 m around the Bridge head/body on layer 27.
- Instance ints: 27 = "out" bitmask over roster entries (16 bits) | balls live mask (16-18); 28 = round number |
  last round winner.
- Non-XR: click to pick up (2.5 m), hold to charge 0.1-0.5 s, release throws at 14 m/s with 7 deg gravity
  compensation and soft aim toward the nearest opponent within 9 m and 30 deg (1.5).

### 9.2 Paddleball

- Geometry: glass tube 3 x 3 x 12 m (six quads with glass material + edge frames), end goal planes (triggers), two
  standing pads, spectator gallery.
- Props: 2 x `(Paddleball_Paddle)` key `2d` (LockOnPickup: stays in hand until the match ends; one per player,
  spawned on a stand beside each pad), 1 ball (TennisBall `787` with gravity off, or procedural).
- Paddle hit (local, by the paddle owner = touch-steal owner of the ball): `v_out = reflect(v_in - v_paddle, n) +
  v_paddle` then scale by 1.05 (GUESS speed-up), clamp 14 m/s; play `RacketHit01` (pitch var 0.2) and the paddle's
  `[VFX_Impact_Particles]`; take ball authority (0.25 s ownership cooldown).
- Walls: perfectly elastic reflection (no gravity, no drag); play `pingpongballhittable`/tennis impact softly.
- Goal: the ball owner detects the goal-plane trigger and sends GoalClaim; authority adds the point (int 7), plays
  `Goal` VO + `CrowdCheer01` + `[VFX_Default_Goal]`, holds 2 s, resets the ball to the conceder's serve point.
  First to 5 -> MatchOver (`AmyVO_Generic_YouWin` for the winner locally).
- Instance ints: 27 = server (0/1) | rally hit count | ball state (0 waiting serve, 1 live, 2 scored).

### 9.3 Soccer

- Geometry: field floor 24 x 40 m (turf material, lines), perimeter walls 3 m with bevelled corners (45 deg 2 m
  chamfers), upper glass to 8 m, roof; 2 goal recesses 7 x 3.5 x 2.5 m with nets (`Rope_Net_Sandbox` tiles) and goal
  triggers; stands optional.
- Props: 1 x `(SoccerBall)` key `00` (keep root scale 0.65; do not allow pickup). Optional: `(SoccerShield)` key `28`
  for a "Shield Soccer" classic mode.
- Kicks: (a) body contact: our player capsule proxy (kinematic, layer 27) moving into the ball -> impulse 20 N s along
  (ball - player) flattened + 0.3 up (`maxLocomotionAttackForce 20`); (b) teleport landing within 1.2 m of the ball =
  kick with the same impulse in the teleport direction; (c) VR hand hits: hand velocity x 0.75 kg transfer. Sounds:
  `Soccer_HitBallHeavy_01` (vol 0.3) on kicks, `Soccer_HitBallLight_01` light touches, `Soccer_BallHitsFloor_01`,
  `Soccer_BallHitsEnvironment_01` walls. Show the ball trail after a kick (ToolTrail: visibleAfterAttackApplied).
- Boost orbs: 4 pickups (visual: emissive yellow sphere r 0.35 m + `BucketCollectableRing` from
  `(Stuntrunner_Pickup_Sandbox)` key `da`), 10 s respawn (GUESS), boost 5 s: walk speed 6 -> 9.6, teleport 8 -> 12 m,
  delay 0.5 -> 0.25 s (GUESS). Our locomotion changes go through the client's movement API (see `somnium_player.md`
  6) - if speed cannot be raised, apply a forward push on the player instead (GUESS).
- Logic: Lobby -> Countdown -> Playing 180 s (timer in int 4) -> after a goal: `Soccer_Goal01`, team VO, 3 s pause, ball
  re-drop at (0, 4, 0) -> at 0 s: MatchOver (`SR_Buzzer_Long01`, winner VO or `Tiegame`).
- Instance ints: 27 = orb availability mask (4 bits) | ball state; 28-31 = orb respawn remaining ms.
  Player slot int 19 = boost remaining ms.

### 9.4 Disc Golf (Lake, Propulsion)

- Geometry: Lake = terrain mesh (procedural heightmap 180 x 180 m, grass colour), lake plane with `Water_Lake_Mat`
  (key `6a`) and a water trigger volume (hazard), paths, 9 tee pads + 9 baskets, trees/rocks/benches/fences from 1.6,
  bonus rings. Propulsion = procedural sci-fi blocks + fans/hazard panels/moving walls/rings (5.4).
- Props: one `(DiscGolfDisc)` key `9d` per player (team colour via `_BaseColor` on `SportDisc_Mat`), baskets
  `(Sandbox_DiscGolfGoal)` key `c` (9 instances; set `hole`/`par` in our data; enable `GlowGoal` beam on the current
  hole's basket).
- Scoring is self-reported by each player (co-operative feel; the authority validates holes finished): player slot
  int 19 = current hole (0-3) | strokes this hole (4-11) | bonus used this hole (12-15); ints 32-33 = per-hole results
  9 x 6 bits signed (-31..+31) vs par; int 34 = power-up used mask (up to 32 panels).
  Instance ints: 27 = tee the group is on (all move when everyone holed out or mercied), 28 = mode (0 stroke play,
  1 co-op catch) | co-op current streak (8-19) | best streak (20-31).
- Throw counting: stroke++ when the disc is released and later comes to rest > 2.5 m from the release point
  (`strokeIncrementThreshold`); mercy at 7; hazard (water trigger / hazard panel / G key) -> OutSFX, disc teleports to
  the last lie, +1.
- Disc flight: implement 5.3 in `FixedUpdate` on the owner; remote puppets extrapolate with the same model.

### 9.5 Bowling (Lone Shoe Lanes)

- Geometry (6.4): 3 lanes (wood boxes 1.066 m wide, gutters as half-cylinder meshes, capping), approach, foul line,
  pin decks, pits with backboard, masking hood, ball returns (hood + rack), settee, overhead monitors (world-space
  canvas with TMP fonts from the Rec Room UI bundles), lounge with pool table/jukebox/stools, carpet.
  Collider tags: lane surface `CollisionSfx/Bowling/Lane`, gutters `.../Gutter`, backboard `.../Backboard` (our
  CollisionAudio re-implementation maps them to the clips in 6.1). Lane PhysicMaterial: friction 0.04 (oiled) on the
  first 12 m, 0.2 after (GUESS, gives hook), bounciness 0.
- Props: 5 balls per lane (`(BowlingBall)` `a5a`, recoloured), 10 pins per lane (`(SandboxBowlingPin)` `a2a`; at
  scale 1.0 for regulation size - then the colliders shrink too; GUESS use 1.0).
- Ball: mass 8, angular drag 0, `AngularVelocityThrowMultiplier 1.5` (spin from wrist), `YUpVelocityScalar 0.5`
  (throws are flattened), `InertiaMultiplier 2`, max 15 m/s, throw x1.1.
- Scoring lane logic (instance authority + bowler's client): roster order (up to 6), current bowler may grab a ball;
  on release -> roll; bowler's client simulates pins, streams PinStream, detects settle, computes knocked pins,
  writes result; authority updates the score sheet, runs the machine cycle animation for everyone, advances
  ball/frame/bowler. 10th-frame bonus rolls per standard rules. Practice lanes: local physics only, no scoring.
- Instance ints: 27-44 = score sheets (6 bowlers x 3 ints: 21 rolls x 4 bits = 84 bits, value 15 = not rolled),
  45 = current bowler (0-3) | frame (4-7) | roll (8-9) | standing mask (10-19) | machine state (20-23).

### 9.6 Basketball (toy, Rec Center / dorm)

- `(Basketballhoop)` key `78` mounted at rim 3.05 m on a procedural pole/backboard support; 2-3 `(Basketball)` key
  `d2`; make detection trigger (7). Local physics in the dorm; networked props in the Rec Center.

---------------------------------------------------------------------------------------------------------------------

## 10. Open questions / GUESS list

1. All arena dimensions (2.3, 3.3, 4.3, 5.4, 6.4) are GUESSES except: bowling lane regulation sizes (real-world), the
   ~34 m bowling room extent (from jukebox/stool source transforms), goal basket/hoop/ball sizes (meshes). Verify with
   gameplay videos (e.g. YouTube "How To Rec Room: Paddleball/Dodgeball/Disc Golf", links in the wiki pages) if more
   fidelity is needed.
2. Paddleball ball: size, speed-up per hit and serve rules are not in the bundles (level-only). TennisBall used as a
   stand-in.
3. Soccer boost numbers (duration, multipliers, respawn) and kick-off procedure are GUESSES; the in-game role values
   (walk 6, teleport 8 / 0.5 s) are measured.
4. Disc golf: exact lift/curve formulas (only parameter names/values are known); hazard penalty (+1 assumed); whether
   bonus panels are per player or per group; hole pars of the real courses (the sandbox basket default is par 3).
5. `strokeIncrementThreshold 2.5` meaning (distance vs. time) - assumed metres travelled.
6. Dodgeball: blocking with a held ball, friendly fire, penalty for crossing the centre line - GUESSES.
7. Bowling pin scale (sandbox 1.5 vs regulation 1.0) and pin-down threshold.
8. `AG/Standard-Opaque-Decal` pass list not yet measured (depth-priming fix may be needed for the dodgeball/soccer
   ball) - check `material.FindPass("DepthNormals")` at runtime.
9. Whether the client exposes a way to change walk speed / teleport distance for boosts (see `somnium_player.md` 6).
10. Per a web search result (unverified), Rec Room shut down on 1 June 2026; irrelevant for runtime (we read the local
    install) but the official rooms can no longer be visited for reference.

---------------------------------------------------------------------------------------------------------------------

## 11. Sources

- Bundles (measured with rrtool, 2026-09-24): TOOLS `43ea1176ca25c0333547813ec3298808` etc.; dumps in `SP\sports\`.
- Rec Room wiki via the MediaWiki API (saved in `SP\research\sports_data\wiki\*.json`): Dodgeball, Paddleball, Soccer, Disc_Golf,
  Bowling, Ultimate_Frisbee, Rec_Center, Sports_Games, News_Archive_2018 (`SP\research\sports_data\wiki\News_Archive_2018.txt`);
  older archives in `SP\research\shoot\wiki\News_Archive_2016.txt`, `News_Archive_2017.txt`.
  https://rec-room.fandom.com/wiki/Dodgeball , /wiki/Paddleball , /wiki/Soccer , /wiki/Disc_Golf , /wiki/Bowling
- Steam news "Rec Room UPDATE - the Bowling edition" (11 Oct 2018) and the 12 Oct 2018 patch (quoted in the 2018
  archive): Lone Shoe Lanes, 6 bowlers, practice lanes, automated scoring, ball-grab protection.
- Steam discussion "Dodgeball Completely Broken Today" (app 471710) - on-fire ball behaviour.
- Speedrun.com / YouTube listings for Disc Golf Propulsion (scores like -17 exist).
