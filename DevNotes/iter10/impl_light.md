# Iteration 10 - implementation notes, agent "light"

Status: DONE 2026-09-25 (compile OK, not run in play mode). Append-only log.

Owned files: Games/Quests/Jumbotron/RrJumbotronRoom.cs, RrJumbotronArea.cs (not the Hull.Build / Haze.Build lines),
RrJumbotronActs.cs, RrJumbotronBoss.cs, Core/RrEnvironmentTable.cs key=Jumbotron_* rows, Core/RrPostFxProfiles.cs
(+ Core/RrPostFx.cs only if needed).

## Plan
1. Environment per room: RrEnvironment.SetArea(key) on Foyer / Armory / each act entry, SetArea(null) on leave/reset
   (as Golden Trophy). Registration covering the hull if simple.
2. Post: ScifiDefault (all acts + Foyer) and ScifiArmory (Armory) profiles in RrPostFxProfiles; RrPostFx.SetArea per room.
3. Ambient: timebox 40 min for APK LightProbes baked SH -> per-room ambient probe (or skip and say so).
4. StadiumEntry grid box cut to the hull/core bounds via kit options (no RrLightGrid.cs edit).
5. Compile check (bash SP/compilecheck/build.sh project --editor).

## Log
### 14:17-14:22 ambient probes (task 3, timebox 40 min; used ~5 min) - NO DATA, skipped
- `apkscene probes` (DN/tools/apkscene, ProbeDbg: scans class 258 for n x 108-byte SH arrays) over level38-49 and
  sharedassets38-49 (APK read in place, data only): every Jumbotron level has exactly ONE LightProbes object, in its
  sharedassetsN.assets (38 #114, 39 #50, 40 #77, 41 #70, 42 #85, 43 #61, 44 #85, 45 #116, 46 #52, 47 #75, 48 #106;
  49 none), and each is 41 BYTES = the empty asset (name + empty arrays; one SH L2 alone is 108 bytes). Same as the GT
  Foyer in iteration 8 (DN/iter8/env.md). The LightProbeGroup components exist (lightprobegroups=1) but the Android
  build carries no baked coefficients. The PC levels are encrypted (never decrypted), so there is nothing to read.
- => ambient stays the table's trilight (Rec Room's own RenderSettings, which is what an empty probe set falls back to:
  renderers sample the ambient SH built from the trilight). No numbers to apply.
### 14:30 post (task 2) - Core/RrPostFxProfiles.cs
- New profiles ScifiDefault ("Scifi_Default_SSFX_Profile", APK sharedassets38 #118) and ScifiArmory
  ("Scifi_Armory_SSFX_Profile", sharedassets39 #54) with jt_lighting finding A/D numbers; both in All.
- Map(Jumbotron, 0, ScifiDefault) (Foyer; areas 2-10 = acts 1-9 fall back to it), Map(Jumbotron, 1, ScifiArmory)
  (RrJumbotronRoom already calls RrPostFx.SetArea(act + 1)). JumbotronBoss (REF2 house copy) stays defined, unmapped.
- VR sanity (ours, not Rec Room's): Bloom clamp 16 in both (ScifiBloomClamp; Rec Room left 65472). The Hotspot lights
  (x20-90 within 0.5 m) make HDR 20-45 pixels that flare at the headset's quarter-res bloom; 16 > the brightest glow
  strip (SciFi_Emit_Cyan 10). RrPostFx's BloomScale 0.7, BaseWarmth +12 and the watch sliders still apply on top
  (intensity 0.35 / Armory 0.49 after the scale). No RrPostFx.cs change needed.
### 14:45 environment per room (task 1) - RrJumbotronRoom.cs, RrJumbotronArea.cs
- RrJumbotronRoom: FoyerScene const + ActScenes[10] (Armory, Battlefield1, Hallway1, Hallway2, Reception,
  StadiumConcession, StadiumEntry, Garage, Cargo1, Stadium) + SceneOf(act). QuestStep -> ShowLighting(act):
  RrEnvironment.SetArea(SceneOf(act)) (lobby/-1 = Jumbotron_Foyer) + the existing RrPostFx.SetArea(act + 1); one
  RrLog.Info "lighting: <scene>" per change. The act teleport (RrQuestRoom.ActWatch -> EnterActLocal) runs in the same
  OnTick before QuestStep, so the lighting switches with the arrival. OnDisable override: SetArea(null) only while
  RrEnvironment.Activity is still Jumbotron (travel's Leave clears the area anyway; never touches another room).
  Reset: OnRunReset -> the next QuestStep(-1) pins the Foyer again.
- RrJumbotronArea.RegisterWholeRoom(root) after BuildLeaves: re-registers the scene (RrEnvironment.SceneBuilt, internal)
  with core (the kit's registered box minus its margin) U every enabled renderer under the area root (kit, floors,
  hull, haze, leaves; <2000 m) U the arrival, so the automatic pick is right too (arrivals were 0.4-5 m outside).
  Hull.Build / Haze.Build lines untouched.
### 14:50 StadiumEntry grid (task 4) - RrJumbotronArea.cs + RrJumbotronActs.cs
- New RrJumbotronArea.GridToCore (Act6 sets it): right after RrSceneKit.Build, CutGridToCore() sets Kit.Grid.LocalVolume
  = the grid box (B box cut to the Filter) intersected with the scene's registered box (RrEnvironment.SceneWorldBounds =
  placed records + 3 m; ~35 x 15 x 113 m) -> cells ~1.5 x 1.25 x 4.7 m. The grid builds only after travel (or 30 s),
  so the cut is in place; logged, with a note if the grid was already built. No RrLightGrid.cs / RrSceneKit.cs change.
### 14:55 compile check - OK
- bash SP/compilecheck/build.sh project --editor: server-like 0 errors 0 warnings (293 files), API guard 118/118,
  editor-like runtime 0 errors, editor tools 0 errors. (output NSP/jt10/light_cc1.txt). Play-mode harness NOT run (per task).

## Result (agent light)
Files changed: Games/Quests/Jumbotron/RrJumbotronRoom.cs, Games/Quests/Jumbotron/RrJumbotronArea.cs,
Games/Quests/Jumbotron/RrJumbotronActs.cs (StadiumEntry GridToCore only), Core/RrPostFxProfiles.cs.
Not changed: RrJumbotronBoss.cs (nothing lighting-related needed), Core/RrEnvironmentTable.cs (the Jumbotron_* rows
are Rec Room's own RenderSettings, and the file is generated by apkscene envcs - "do not edit"), Core/RrPostFx.cs
(the per-area API already existed: RrPostFxProfiles.Map + RrPostFx.SetArea(act + 1)).
Expected in the log: "Jumbotron: lighting: Jumbotron_<room>", "Environment: area Jumbotron_<room>", "PostFx: Jumbotron
area n: crossfading to Scifi_Default_SSFX_Profile / Scifi_Armory_SSFX_Profile", "Jumbotron_StadiumEntry: light grid box
cut to the kit core, A -> B m", "<scene>: lighting area A -> B m".
Open issues:
- Untested in engine (compile only). Next harness/headset run: EnvTest after TestAct1/2/5/8 must report their own keys.
- No baked light-probe SH exists in the APK (empty LightProbes assets), so the acts keep Rec Room's near-black trilight
  ambient; if they read too dark after the hull/haze fixes, the knobs are exposure (profile/watch) or an env_extra
  override row regenerated into the table - not done (the rows are Rec Room's own values).
- Armory profile contrast +48.3 (Rec Room's own) over a ~0 ambient may crush the Armory's darks - watch it.
- Bloom clamp 16 is ours (VR); remove the two .Set("Bloom","clamp",...) lines if the glow looks capped.
- GridToCore's cut applies only if the grid had not started building (it waits for travel, or 30 s after creation);
  the log says when it was already built.
