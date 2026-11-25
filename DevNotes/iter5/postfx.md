# iter5 postfx - Rec Room post-processing per room

Status: started 2026-09-24. Owner key: postfx.

## Milestones
- [ ] 1 client post setup (CLIENT + ETS approach)
- [ ] 2 Rec Room profiles (REF2 16 assets, cache 1AF, install Volume prefabs, default)
- [ ] 3 RrPostFxProfiles.cs + RrPostFx.cs
- [ ] 4 hooks in RrRoom/RrTravel/GoldenTrophy
- [ ] 5 compile
- [ ] 6 harness postfx_tour

## Findings
### Client (E:/Somnium Space/Main Build, read only) - milestone 1 DONE
- URP global settings default profile = `_SomniumSpace/Scenes/PostProcessing Default Profile.asset` (guid 8d6e0fde):
  every component overridden to neutral: Tonemapping mode 0 (None), Bloom intensity 0 (thr 0.9, scatter 0.7),
  ColorAdjustments 0, HBAO intensity 0 etc. So in a world there is no tonemapping and no bloom unless the world adds a Volume.
- GraphicsControl (`_SomniumSpace/Graphics/Scripts/GraphicsControl.cs`): in a world `_globalPostProcessVolume` is empty
  ("Player is in a World, ignoring Bloom setting", l.785). AO settings (LoadSetting after session connect, and on every
  settings change) go to GetGlobalVolumes(): registered ones, else **FindObjectsByType<Volume> where isGlobal** (l.1113-1138)
  and call `volume.profile` (instantiates a copy of sharedProfile!) + `profile.Add<HBAO>(true)` if missing.
  => our Volume must set `Volume.profile = ourProfile` (not only sharedProfile) so the client's HBAO lands in OUR
  profile instance and our later edits still reach it; we must not strip HBAO the client added; on clear we keep
  the HBAO component values untouched (never add/remove HBAO ourselves).
- Cameras: post + HDR on, volume mask Default (layer 0). Our Volume object on layer 0.
- World scene `Assets/Rec Room Scene.unity` has NO Volume. Project DefaultVolumeProfile = neutral (like client).
- ETS: `Ets/EtsPostFx.cs` - runtime VolumeProfile (CreateInstance), Add<Tonemapping/ColorAdjustments/Bloom>(false),
  Volume isGlobal priority 100 sharedProfile, layer 0, Destroy(profile) in OnDestroy. Proven in the client.

### Rec Room profiles - milestone 2 DONE (raw dumps in session scratchpad postfx/ref2_profiles.txt)
- All REF2 scene Volumes: global, priority 0, weight 1. URP stock components (GUIDs = URP's own).
- HOUSE profile (14 of 16 REF2 files, incl GoldenLobby and blank "Testing"): Bloom thr 1.04 int 0.15 scatter 0.6 HQ off;
  Tonemapping Neutral; WhiteBalance temp 5; ColorAdjustments exp 0.1 contrast 10; LiftGammaGain lift (0.9216,0.9419,1,0)
  gamma (1,0.9825,0.9598,0) gain (1,0.9898,0.9558,0.0339); ChannelMixer all identity (nothing overridden).
- CrescendoLobby: exp 0.2, hueShift 3, lift (0.8648,0.9299,1,0). IsleLobby lift (0.9593,0.9656,1,0). BurningBurg sat 9 (no exp/contrast),
  lift (0.9379,0.9654,1,0). ForestRoad exp 0.3 (no gamma). RecRoyale: bloom 1.91/0.22/0.32, exp 0.38 contrast 13.44 hue -5 sat 1.8,
  WB 5.5 tint -13.3, LGG inactive. RunTheBlock: bloom 9.2/0.01/0.853, sat 10, WB -6.
- GTact1..6, MakeItToMidnight, RecRally Volumes point at profiles NOT in REF2 -> acts use the house profile (= GoldenLobby).
- Cache 1AF (Rec Center 071825) `RecCenter-RecCenter_071825-PostProcessingProfile`: Bloom 1.1/0.25/0.617; Neutral; WB 5;
  CA exp 0.2 contrast 5 sat 8; LGG lift (0.88991,0.950062,1,0) gamma (1,0.924874,0.867988,0) gain (1,0.988927,0.940286,0.0338983).
- Cache 35C `MakeItToMidnight-Home-PostProcessingProfile`: Bloom 0.98/1.4/0.64; Neutral; CA exp 0.1 contrast 5 sat -2;
  LGG lift (0.930508,0.942829,1,0) gamma (1,0.995392,0.944616,0) gain (1,0.916038,0.883374,0.105932).
- Install: only 2 VolumeProfiles. `CameraRigProfile` (bundle 063b6b66, [RR CameraRig] GlobalSSFX volume prio 0) = only
  Rec Room's custom ComfortEffects (VR vignette/fade, custom type - not ours). The rig's `PostProcessVolume` (prio 1) has
  sharedProfile null: the room's profile is plugged in at runtime. `StuntRunner_Default_SSFX_Profile` (bundle 1a69c270,
  used by Night_StuntRunner_Outdoor_Mood): Bloom 1.1/0.5/0.617; Neutral; WB 2.8; CA exp 0.21 contrast 2.1;
  LGG lift (0.934,0.967,1,-0.005) gamma (1,0.981,0.976,-0.039) gain (1,0.976,0.952,0.046).
- Maker Scene Moods (bundle 1a69c270, RecRoom.Core.Creation.SceneMoods.MoodSetting, 8 moods): all the same post:
  Bloom thr 1.04 int 0.3 diffusion 6; grading temp 5 tint 0 exp 0.1 contrast 10 sat 0. = Rec Room's default room look.
- The Charades Ink Space hull: no Volume in objects.tsv (only 3 objects carry Volume: the 2 camera-rig ones + 4056f2fc PostProcessVolume).
- Scene -> quest (rr_reference_coverage.md): CBM (Crescendo) act1 BurningBurg, act2 ForestRoad, act3 CastleGroundsDay (own profile
  missing; CastleGrounds' = house), GrandHall acts 4/7/10/12 (only GrandHall4's exported), act13 ConcertHall; ILS (Isle) act2
  PiratesBanquet, act4 PortPlunder, act5 GibbetJungle.
- Client + harness pipelines: HDR on, LDR grading LUT 16, volume update EveryFrame.

## Design (milestone 3, in progress)
- RrPostFxProfiles.cs: nested Profile (Name, Source, list of Component.parameter = Vector4, exactly the YAML overrides of
  ACTIVE components) + table (activity, area) -> profile, fallback (activity,0) -> Default (= REF2 Testing, the blank Studio scene).
- RrPostFx.cs: one "Rr Post" GameObject under RrMain (layer 0), Volume global priority 100, runtime VolumeProfile with
  Bloom/Tonemapping/WhiteBalance/ColorAdjustments/LiftGammaGain; `Volume.profile = sharedProfile = ours` (client HBAO lands in ours).
  Enter(activity) weight 0->1 0.35 s; Leave() ->0 0.25 s; SetArea(int) value crossfade 1 s (enum/bool snap like URP).
  VR: bloom downscale Quarter + maxIterations 5 (same widest mip as half x6). No DoF/CA/grain/motion blur/lens (none in RR data).
  Hooks: TestOff/TestOn/TestReport/TestBundleProfile (loads install StuntRunner_Default_SSFX_Profile, compares to data).
- Hooks: RrTravel.Trip (leave after OnLocalExit, enter before OnLocalEnter, arrival-room leave), Recover, Shutdown;
  GT QuestStep: RrPostFx.SetArea(act + 1).
- [x] 3+4 written: Core/RrPostFx.cs, Core/RrPostFxProfiles.cs (+metas); RrTravel.cs (Leave after OnLocalExit, Enter before
  OnLocalEnter, arrival-room Leave, Recover Leave, Shutdown -> RrPostFx.Shutdown); GT QuestStep `RrPostFx.SetArea(act + 1)`.
  RrRoom.cs, RrTravel.Origin.cs, RrSpawn.cs unchanged (Volumes in prefabs: none worth keeping; RrSpawn strips them already).
- [x] 5 compile: 0 errors x3, 113 members proven (2026-09-24).
- [ ] 6 harness run 1: SP/harness/scenarios/postfx_tour.json (copy in DN/tools/harness/scenarios).
- [x] 6 harness run 1 (postfx_tour): 50/50 steps ok. Resolved stack = our values (Neutral, bloom 0.15/1.04 house,
  0.25/1.1 Rec Center). Bundle test: StuntRunner_Default_SSFX_Profile LOADS in 6000.3 with stock URP types bound
  (Bloom Tonemapping WhiteBalance ColorAdjustments ChannelMixer LiftGammaGain), 11/11 params = data -> PASS.
  Luma before->after: dorm 184->180, rc 140->141, gt lobby 112->113, act1 121->123, dodgeball 156->154, paintball 145->145;
  SD drops 1-6 (Neutral shoulder: no clipped whites). Look: warmer (RC floor honey), cool shadows (black ceilings -> navy
  via lift), whites softened (LDR windows/lanterns ~1.0 go beige: no bloom because they are below threshold 1.04).
- run 2 planned: better dorm/act1 cameras, act 4 lava before/after + VR chain + bloom probe (hooks TestVrChain,
  TestDesktopChain, TestBloomProbe added).
- [x] 6 harness run 2 (postfx_tour, final): 66/66 steps ok, 2 of the 4 allowed runs used.
  Luma before->after: dorm 147->149, rc 140->141, rc stage 135->137, gt lobby 112->113, act1 122->124, act4 131->131,
  dodgeball 156->154, paintball 145->145 (SD down 1-5). VR chain resolved "Quarter x5", same luma. Bloom probe (x8)
  diff: glow around the act-4 lava and onto the planks -> bloom works; at Rec Room's 0.15 it is subtle by design.
  Bundle profile test PASS again.
- Findings for room owners (not our files): our own "glow" materials are LDR-ish (RrLook.Unlit whites, Emissive 1.1-1.6):
  under Rec Room's Neutral tonemapping they soften to beige and barely pass bloom threshold 1.04; the quest-kit lava
  (lit base (1,0.3,0.04) + emission 1.1 + 2 orange point lights) shifts from orange to yellow-orange. Rec Room's own
  emissives are HDR (Light_Fluorescent_mat EmissivePower 3.51).

## DONE 2026-09-24. PNG pairs in SP/harness/out/postfx_tour/pfx_*_before|after.png
