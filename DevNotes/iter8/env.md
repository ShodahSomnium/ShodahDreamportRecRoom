# iter8 env - Rec Room lighting, shadows, sky per room + mesh overlaps

Owner: env agent. Owns CODE/Core/RrEnvironment*.cs, RrSceneKit*.cs, RrSceneData.cs, RrPostFx*.cs (exposure only),
Rooms/RrTravel*.cs, Places/RecCenter/RrRecCenterCache.cs, DN/tools/apkscene, scenarios env_*.json.

## Status
- [x] 1 client lighting facts (below)
- [x] 2 extract environments: apkscene `env` -> DevNotes/refdata/apk/env/*.env + _env.tsv (61 levels)
- [x] 3 RrEnvironment runtime (code written, compiles in isolation; awaiting harness)
- [x] 4 mesh overlaps in kit (code written; counts come from the harness)
- [x] 5 verify (harness runs used: 5/5; last change after run 5 compile-checked only, see below)

## Findings
### Client (E:/Somnium Space/Main Build, read only)
- The Bridge environment (`BridgeEnvironment.cs`) is a thin wrapper: SetSkybox = RenderSettings.skybox, SetSunSource =
  RenderSettings.sun, SetAmbientModeGradient = Trilight colours, UpdateEnvironment = DynamicGI.UpdateEnvironment.
  Nothing in the client scripts writes RenderSettings otherwise, and nothing subscribes to activeSceneChanged.
- LoadingSystem: MAIN_TO_WORLD / NONE_TO_WORLD call SceneManager.SetActiveScene(worldScene) after load, so the world
  scene's own RenderSettings apply from then on; values set while the loader scene is active are lost (ETS EtsSky note).
  -> RrEnvironment re-applies on SceneManager.activeSceneChanged.
- URP asset (URP_RenderPipelineAsset): main light shadows ON, soft shadows ON (quality 2), 4 cascades, shadow distance
  100 (user presets 50/75/100/150/250 via GraphicsControl), main shadowmap 1024 (user setting), additional-light
  shadows ON (atlas 8192), additional lights per pixel, per-object limit 4, Forward renderer, depth priming, HDR on,
  light probe system = probe volumes. Fog stripping manual with all modes stripped (client shaders only).
- URP's main light = RenderSettings.sun when set, else the brightest directional light.

### Rec Room levels (Android build, refdata/apk/env)
- NO lightmaps in any of the 61 levels (every renderer m_LightmapIndex 65535) and EVERY light is realtime
  (m_Lightmapping 4): Rec Room's built-in rooms are realtime-lit - a soft-shadow key light + many point/spot lights +
  trilight ambient + a custom reflection cubemap. So the look is reproducible with realtime lights.
- Skies: Paintball/Dorm/Dodgeball/Paddleball/Soccer/RecCenter/DiscGolf = Skybox/Cubemap with a baked cubemap
  (RGBM, ASTC 8x8; not in the install); quests, Charades, Bowling, Laser Tag = Skybox/Procedural (GT SkyboxQuest:
  exposure 0.01 = black; Jumbotron SkyboxSciFiQuest exposure 0.18 green tint); Stunt Runner = AG/Skybox/Simple
  Gradient (the only sky shader in the install, also used by the install's ComfortModeSkybox material).
- Golden Trophy lobby (Quest_Foyer): no sun, ambient trilight almost black (sky 0.015, equator 0.22/0.11/0.09,
  ground 0.05/0.015/0.008), 40 realtime point/spot lights (1 with shadows), fog linear 2-40 m (0.21,0.20,0.25).
- Paintball lobby: key light soft shadows strength 0.904 colour (0.9985,1,0.888) intensity 1, custom reflection
  intensity 0.348, fog linear 50-350 (0.735,0.797,0.856), trilight sky (0.413,0.545,0.731) eq (0.41,0.41,0.27)
  ground (0.773,0.680,0.253).
- Install shader AG/Standard-Opaque has FOG_LINEAR/EXP/EXP2, _ADDITIONAL_LIGHTS, _ADDITIONAL_LIGHT_SHADOWS,
  _RECEIVE_SHADOWS_OFF, _RENDER_FEATURESET_MOBILE/FULL, _FAST_MODE_SHADING keywords; no _MAIN_LIGHT_SHADOWS* keyword
  (main-light shadow sampling must be compiled in unconditionally or not at all: to verify in the harness).
- Sky cubemaps: REF2 has the same cubemaps as PNG strips (2D Textures/Cubmaps, reference only). The APK's last mip
  of each face is an ASTC void-extent block = the face's average colour (RGBM x5), used to match the REF2 file.

## Notes for the other agents (read me)
- Lighting is automatic: every kit scene (RrSceneKit.Build) registers with Core/RrEnvironment when it finishes, and
  while the player's head is inside its records' bounds (+3 m) its Rec Room lighting applies (sun, ambient, fog,
  sky, reflections), else the room's default (RrEnvironment.KeyFor(activity)). Do NOT set RenderSettings or add
  a directional light in room code. To stop a built scene claiming the player, SetActive(false) its kit root
  (Paintball's SetArea already does). Keys = the RRSK scene name (e.g. GoldenTrophy_Armory, Jumbotron_Foyer,
  Paintball_River) - 69 scenes are in RrEnvironmentTable; an unknown scene is lit from its own E/L records.
- Kit defaults changed: CastShadows = true; EnvironmentLights = true (keeps as many point/spot lights as the level
  ran, up to 48, at least your MaxLights, with Rec Room's shadowed spots soft-shadowed); merge cells 10 m in scenes
  with > 8 point lights; FixOverlaps = true (duplicate R/X/P dropped; X stand-ins and G "dress" markers whose box
  IoU >= 0.5 with an install renderer/prefab the kit built are NOT built/returned; X faces inset 3-6 mm).
  Opt out per build with the options if a room needs the old behaviour.
- New L record keys from apkscene convert: "shadows=soft strength=x"; new E records "E sun ...", "E refl ...".

## Requests
- goldentrophy + jumbotron agents (hull builders RrGoldenTrophyHull / RrJumbotronHull): in the sunless quest areas split
  the merged wall/floor plates into cells of about 6 m (or per plate group). The client's URP lights an object with
  at most 4 point/spot lights, so a hall-sized merged plate mesh gets 4 of the foyer's 40 torches and the walls stay
  dark (harness run 5, GoldenTrophy_Foyer: mean luma 24; the walls are hull plates). The kit now keeps its own
  renderers apart in such rooms (<= 400 renderers) and merges bigger ones in 6 m cells.
- editor / world-scene owner (Editor/RrSceneSetup.cs, not mine): save "Rec Room Scene.unity" with fog ON in Linear mode
  (e.g. start 1000, end 2000: invisible in the arrival room) so Unity's Automatic fog stripping KEEPS the FOG_LINEAR
  variants of the shaders built into the world bundle (URP Lit/Unlit fallbacks, TMP). RrEnvironment turns Rec Room's
  own linear fog on per room (every converted level uses linear fog); Rec Room's install shaders already carry their
  fog variants (AG/Standard-Opaque: FOG_LINEAR/EXP/EXP2), our world-bundle ones are stripped today (no error, they
  just do not fog). The client's own shaders (avatars) strip every fog mode (client GraphicsSettings m_FogStripping 1,
  keep none) and never fog - nothing to do about that from a world.

## Implementation (2026-09-25 00:30)
- Core/RrEnvironment.cs (+Setting, Sky, Table (generated), Test): per room, applied by RrTravel after the build (next to
  RrPostFx.Enter), cleared on Leave, restored on Shutdown. Activity default key (KeyFor) + every kit scene registers
  itself (RrSceneKit.Finish -> RrEnvironment.SceneBuilt); the smallest active kit scene containing the head wins
  (checked 4x/s) -> Paintball maps, quest acts, the Armory, Jumbotron areas switch lighting without room code.
- Sun = the world scene's own Directional Light (turned/coloured/shadowed per room, off for quests); re-applied on
  activeSceneChanged. Sky = runtime copy of the install's ComfortModeSkybox (AG/Skybox/Simple Gradient) with the
  room's colours; Skybox/Procedural (quests) used when Shader.Find has it. Fog ON with Rec Room's values.
  One realtime reflection probe per area (Environment layer, 128 px, rendered once).
- Dorm: RrEnvironment places the level's own 8 lights (RigFor(Dorm): same frame as the Maker-Pen dorm pieces).
- Kit: CastShadows default ON; EnvironmentLights (keep as many lights as Rec Room ran, max 48, their shadowed ones
  soft: <=3 spots, <=1 point); merge cells 10 m when >8 point lights (URP per-object limit 4); FixOverlaps:
  duplicate R/X/P dropped, X and G "dress" markers with IoU>=0.5 vs install renderer/prefab boxes dropped, X faces
  inset 3-6 mm. Phase order now Renderer, Prefab, StandIn.
- Rec Center cache: its KeyLight -> RrEnvironment.UseSunFrom (the sun copies it in the room frame); its own sky
  material SkyboxLockerRoom_Baked used with Shader.Find("Skybox/Cubemap") when present.

## Sky colours from the final cubemaps (00:55)
- apkscene/Astc.cs: an ASTC LDR block decoder (tool only). Checked on the Dorm's cubemap against REF2's
  Dormroom2_Baked_Skybox.png: face means within 0.014 and every elevation band within 0.015 (0 error blocks).
- envcs now derives each Skybox/Cubemap sky's six gradient colours from the decoded cubemap (mip 3, 128 px/face)
  by elevation band (zenith 70-90, mid 20-35, horizon 0-10, ground horizon -3-0, ground mid -30--10, nadir);
  Custom/SkyBox (Drive-In, Merry Midway, Rec Rally) from its material's sky/horizon/ground colours; procedural
  skies from Unity's scattering model (EnvCs.Procedural). Only the 6 colours per room go into the code.
- Harness run 1 (env_tour) started 00:57.

## Harness run 1 (env_tour, 50/50 steps ok)
- Env applied in every room: Dorm_Builtin (+ its 8-light rig), RecCenter_071825 (cache key light -> sun, its own
  SkyboxLockerRoom_Baked through Skybox/Cubemap in the editor), Paintball_Lobby -> Paintball_River (forced), Golden
  Trophy: the quest agent now builds the 11 Android areas as kit scenes; all registered, GoldenTrophy_Foyer applied
  automatically (no sun, 40 lights, 1 shadowed spot, fog 2-40 m).
- Sky probe (AG/Skybox/Simple Gradient): ground-horizon colour AT 0 deg blending to the sky-horizon colour at +20,
  mid +40, top +90; ground mid -30, ground top -90; exposure scales the authored colour; gradient power no effect.
  -> envcs remapped the bands to those elevations.
- Looks: sun shadows on floors/grass (Rec Center skylight pattern on the floor, trees on the Paintball lawns), fog haze
  over the maps, warm trilight. Rooms 0.3-0.5 EV darker than iteration 7 at the same camera -> RrPostFx.BaseExposure
  -0.35 -> 0 (the -0.35 had compensated the old bright world sun).
- _RENDER_FEATURESET_FULL global keyword: no visible difference (dropped).
- Stored ambient probe (RenderSettings m_AmbientProbe) in the levels = the SKYBOX's SH (GT Foyer's black procedural
  sky -> SH ~0.001; Dorm/Paintball SH follow their cubemaps), not the trilight colours; in Trilight mode Unity lights
  from the colours, so the colours are applied (as now). LightProbes objects are empty (GT Foyer: 41 bytes).

## GT API (for the goldentrophy agent)
- `RrEnvironment.SetArea("GoldenTrophy_Armory")` (any RRSK scene name) pins an area's lighting; `SetArea(null)` =
  automatic again; cleared on leaving the room. Not required: with the areas at their own places and hidden ones
  SetActive(false), the automatic pick already follows the player (run 1 log). AreaShown() may call it anyway.

## Harness run 2 (60/60 ok) and changes for run 3
- Exposure 0: Rec Center / Paintball plaza look right (sun patches through the skylight, table shadows on the grass,
  pale-blue gradient sky). Dorm warm (Rec Room's warm trilight + its 8 lights). Golden Trophy foyer dark and foggy
  (Rec Room's values: no sun, near-black sky ambient, fog 2-40 m); its 24 torch flames were NOT placed (kit Fx
  wanted a wall collider with its own renderer; the quest's hull draws plates over Rec Room's collider boxes) ->
  RrSceneKitFx now also accepts a collision face exactly where Rec Room's wall was (off= behind the flame +-12 cm).
- Jumbotron: 11 kit scenes registered, Jumbotron_Foyer applied (its own procedural sci-fi sky).
- Kit: near-duplicate R/X (2 cm, 1.5 deg, 1% size) and exact duplicate dress markers dropped; small merge cells only
  for sunless scenes (quest areas), not for sunlit maps.
- RrEnvironmentTest.Shot* raycast from the scene centre (run 2 shots came from roof height).

## Harness run 3 (66/66 ok) and change for run 4
- Torches: GT Foyer 24/24, Hallway1 17/17, Hallway2 19/19 flames now placed on Rec Room's walls (were 0).
- Overlaps: Spillway 84 duplicate dress markers dropped, Paintball lobby 2 duplicate stand-ins, Jumbotron
  StadiumConcession 1 duplicate renderer; GT Foyer 21 stand-ins inset. Paintball lobby merges in 32 m cells again
  (58 batches, was 124 with 10 m cells).
- Quest floors had a grey-blue sheen: the realtime reflection probe captured the fog. Rec Room's quest areas reflect
  their (black) procedural sky, so the probe now renders only where the level reflected a baked environment cubemap
  (table token reflc=1; 23 of 69 scenes: Paintball, Dorm, gyms, Rec Center...).
- Spillway is Rec Room's night map (moonlight sun (0.28,0.41,0.63), blue fog 0-154 m): dark blue is right.

## Harness runs 4-5 (67/67 ok)
- Run 4: quest floors/chair legs still grey-white without the probe -> the default (sky) reflection was not black.
  Rec Room's quest areas reflected a black procedural sky -> RrEnvironment.ReflectionOf: reflection 0 when the level
  reflected its sky (not a baked cubemap) and that sky's gradient is under 0.06. Run 5: floors dark, torches glowing,
  foyer lit by its torches + stage spot only (luma 24 vs 82 before). No-fog shot shows the brick hall under the fog.
- Final lumas (post on): Dorm 149 -> 117, Rec Center 118 -> 91, Paintball plaza 147 -> 134, River 78 -> 66,
  GT lobby 82 -> 24 (fog 2-40 m + no sun + black sky ambient: Rec Room's own values; the user said "too bright").
- After run 5 (compile-checked, not run): sunless scenes with many lights and <= 400 renderers are not merged
  (each renderer takes its own 4 nearest lights, as Rec Room's unbatched Android levels), LightCellSize 10 -> 6 m.
