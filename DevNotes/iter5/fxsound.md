# iter5 fxsound - Rec Room level effects and sounds
Status: DONE 2026-09-24 (4 harness runs used). Compile 0 errors x3, 113 members proven.
SCR = C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad
rrscene work dir = SCR/rrscene (guidmap, matvotes, inst_*.tsv). New: `rrscene instfx <work>` -> inst_fx.tsv
(13 bundles, 1471 particle systems, 107 audio sources; ~12 s).

## Facts found
- REF2 AudioClips.asset: only BurningBurg (2 clips) and TheBlock (45) are non-empty; ParticleVfxs.asset all empty.
- GT scenes (GoldenLobby, GTact1-6) have 0 AudioSources; their particle systems: [VFX_Flame] under every
  [WallTorch]/[Torch_Wall] (8-15 per scene), 3 "Particle System" under each [DrinkingFountain], one [VFX_Lava] (act 4).
- Install has no WallTorch / VFX_Flame / DrinkingFountain / Wind_Lines. Fire in the install: (Sandbox_Campfire) =
  [VFX_CampfireFlame] (loop, VFX_SquareParticle_mat) + Audio campfire_02_30_loop 0.33 1-28 m loop; the same flame is in
  (Quest_Dracula1_MiniBossSkeletonBone) with campfire_02_30_loop 0.25 1-28 m. Lava: VFX_Lava_Bubbles01 (one-shot),
  clips "Lava Loop Low/High". Fog [Sandbox_Fog_VFX], fireflies [Sandbox_Fireflies_VFX], wind [Sandbox_Wind_Wavy_VFX].
- Rec Center cache 1AF#1: particles only [Wind_Lines_VFX] (room (-14.81,2.89,26.64) yaw 160, scale .86/1.07/.66);
  6x [RecCenter_LightShafts] (skinned Skylight_LightShafts, LightShaftLocker_Mat: URP particles, GradientHorizontal,
  tint (1,0.945,0.601,0.039)); audio Systems/Audio/Local: CafeCounter = RecCenter_Ambiance_CafeCounter 0.65 loop
  custom rolloff 9.55-13.08 m at scene (-5.409,-0.515,-21.793) scale 3.24; AmbianceLightCrowd 0.25 loop 2D (pan 0).
  Neither clip is in the install.
- Area code (RrGoldenTrophyArea, not mine) drops 72/80 torch stand-ins (no rebuilt wall within 1.2 m) and hangs 8 of
  RrQuestKit.Torch (procedural). So F flames must be placed after the room is built, only where a wall is behind.

## Milestones
- [x] 1 parser F records (Fx.cs, InstFx.cs, Emit.cs Effects(), psdump); refdata regenerated (backup SCR/rrscene/refdata_before_iter5); GT rrsk identical except F lines
- [x] 2 parser sounds: A was=/roll=/pan=, per-scene md "Effects and sounds" (loops, one-shots, AudioClips.asset list). Kit: RrSceneData F + A keys, RrSceneKitFx (deferred placement, wall raycast, nearest 40 fx / 8 sounds), EffectPhase, options/result. Compile 0 errors. GT Scenes regenerated (--mode z), offline parse same=True all 7 (SCR/fx/ptest).
- [x] 3 Rec Center: Places/RecCenter/RrRecCenterFx.cs (6 shafts rebuilt from the cache rig corners, mesh Skylight_LightShafts measured via SCR/fx/mesh/mc.dll; URP Particles/Unlit remade with LightShaftLocker_Mat values (base (2,1.76,0.64,0.078), colour mode add) on GradientHorizontal; wind lines via inline RRSK F), call in RrRecCenterRoom.Build; ambience = Rec Room's 2 sources (crowd 2D 0.135 people02, cafe counter linear 9.55-13.08 at (-19.74,1,-1.82) coffee pour 0.2), birds+kiosk hum removed. Dorm: nothing added (no particle systems in the Dorm kit; shafts+ambience already there)
- [x] 4 interaction sounds audit (table below)
- [x] 5 compile OK (237 files); harness fxsound_tour.json (SP/harness/scenarios + DN/tools/harness/scenarios), 4 runs

## Harness
- run 1 (18:47): 38/38 steps ok. All F placed, but wall mounts hit Rec Room's own (invisible) collision boxes
  -> fixed: only a collider with its own MeshRenderer (the rebuilt shell) counts. Flames and RC shafts not visible in
  shots -> fixes: legacy additive particle materials made additive in templates (RrSceneKit.Additive); RC shafts use
  URP Particles/Unlit with LightShaftLocker_Mat values. Added RrSceneKitTest.Effects diagnostics + wall shot poses.
- run 2 (42/42): 10 of 80 torches on a drawn wall (lobby 0, acts 1/2/4/5 0, act 3 6, act 6 4); RC shafts visible after the URP Particles/Unlit remake.
- run 3 (40/40): lava bubbles visible after lift + shape on all systems; torch embers render as tiny orange squares (Rec Room's VFX_Flame is embers; the flame was the WallTorch mesh) -> added flame cards F (Sandbox_Candle) Candle_Tall/FirePlane_fbx LaternFlame_Mat (kit option FlameCards); fountains floated -> mount=wall for fountains too.
- run 4 (final, 40/40): torches with Rec Room's lantern flame card + embers + crackle on 10 of 80 torches (act 3: 6, act 6: 4; the rest have no drawn wall), act 4 lava bubbles + Lava Loop Low, fountains dropped (no wall), RC 6 shafts + wind + ambience. The flame card is flat: edge-on when faced squarely (fx_gt_act3_torch_close); a crossed pair is the next step.

## Interaction sounds audit (task 4)
| thing | Rec Room | ours | status |
| --- | --- | --- | --- |
| category/activity doors: hover | RoomDoor config has no audio fields (install dump); ActivityDoor Animator only | Kiosk_StateChange01 at low volume (RrPortal/RrCategoryDoor) | kept (GUESS clip from iter 3) |
| doors: open/use | no clip in RoomDoor; travel = loading card | GenericTeleport01 via RrTravel | ok |
| Magic Door | MagicDoor.destinationRefreshAudio = MagicDoor_Refresh | MagicDoor_Refresh | ok |
| Rec Center list panels | watch UI sounds | RrUiButton hover/click + PlayerMenu open/close (RrWatchSounds) | ok; our aim/poke path fires click only (no "down") - minor, later |
| pots breaking | BreakableObject smashSounds | Quest_PotterySmash01-03 (RrQuestBreakables) | ok |
| chests | LootTool (Quest_Dracula1_ChestCoins_*) | RrQuestKit.OpenChest: lid only, no sound | GAP (Games/Quests, not mine): later |
| weapon racks refilling | ? | RrQuestRack refills via MakeTool (AnimateInOut pop-in sound if the prop has one) | check later |
| boulder rolling | ? | impact sounds only (PotterySmash02, Sword_ImpactHeavy01, MeleeImpact_01) | GAP: no rolling loop; install has Bowling_BallRoll02 (GoldenTrophy, not mine) |
| lava sizzle | Floor-is-lava clips Lava Touch / Lava Sink / Lava Loop | burn = VFX_Lava_Bubbles_Death01 only, no sound (RrQuestRoom.LocalLava) | GAP (not mine); ambient Lava Loop Low now plays at act 4's lava (F snd, GUESS) |
| footsteps on stairs | Locomotion_Footstep_Tile01-03 (Rec Room's player locomotion) | none (the Somnium client owns locomotion) | not added |
