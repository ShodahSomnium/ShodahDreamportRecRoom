# Iteration 10 - Jumbotron lighting / environment / fog / glow (agent: lighting)

Status: DONE 2026-09-25 (findings A-H + recommendations 4.1-4.7). Append-only.
Scratch: NSP/jt10/lighting/

## 0. Context / method
Read only (CODE untouched). Data: DN/refdata/apk/env/Jumbotron_*.env + rrsk L/G records, apkscene dump/mat/scripts on
the APK (data only), install shaders via shdis (DXBC) + rrtool dumps, a scratch copy of apkscene (NSP/jt10/lighting/apkx:
`shblob` = APK shader GLSL text, `texavg` = texture means as numbers). Section map: 1 = finding E, 2 = findings B+G,
3 = findings A+C+D+F+H, 4 = recommendations at the end.

## 1. Lights per scene (APK L records vs table urp= vs kit caps)

## 2. Environment rows (RrEnvironmentTable Jumbotron_*) vs APK RenderSettings

## 3. Area-fog volumes, portal glows, emissive strips, post-processing

## 4. Recommendations per scene

## Log

### 10:xx finding A - post-processing Volume of every Jumbotron level (apkscene dump, data only)
- level38 (Foyer) #14212 and level49 (Additive) #174: UnityEngine.Rendering.Volume, isGlobal 1, priority 0, weight 1,
  sharedProfile = sharedassets38.assets #118 "Scifi_Default_SSFX_Profile" (same profile in both). Components:
  - Bloom: threshold 1.1, intensity 0.5, scatter 0.617, highQualityFiltering 0 (others default: clamp 65472, tint white)
  - Tonemapping: mode 2 (ACES)
  - WhiteBalance: temperature 0 (override on, = neutral)
  - ColorAdjustments: postExposure +0.5, contrast -14 (saturation/hue/filter not overridden)
  - ChannelMixer: no overrides (identity)
  - LiftGammaGain: lift (0.8929, 0.9395, 1, 0), gamma (1, 0.9814, 0.9961, 0), gain (1, 0.9311, 0.9386, 0.1264)
    (lift/gain: slight blue lift in shadows, warm-neutral gain +0.126)
- raw dump: NSP/jt10/lighting/volprofile.txt

### finding B - ambient probe in the levels is stale / identical
- All 11 Jumbotron room levels store the SAME RenderSettings m_AmbientProbe (DC r 0.0224 g 0.0016 b 0.0135, i.e. the
  procedural sci-fi sky's SH), whatever their trilight colours; Jumbotron_Additive (level49) stores ambient mode
  SKYBOX with an all-zero probe. (GT levels likewise share one probe 0.0012/0.0009/0.0004.)
- AG/Standard-Opaque's lit pixel program (install, shdis agstd p4_s1_22F9EE) evaluates standard URP SH ambient
  (UnityPerDraw cb2[25..31] = unity_SHAr..unity_SHC, max(0)) x albedo, the main light from $Globals cb0[5]/cb0[6],
  the reflection cube unity_SpecCube0 at mip (1.7-0.7r)r*6, then the voxel-grid lights. So Rec Room's surfaces DO take
  RenderSettings ambient (through the SH), i.e. whatever the ambient probe holds.

### finding C - area fog / portal / light-shaft / glow shaders (APK materials vs PC install)
APK material -> shader (apkscene `mat` over level38-48; raw NSP/jt10/lighting/mats_glow.txt):
- Scifi_AreaFog_{Cyan,Pink,Purple,Red}_Mat: "Shader Forge/Particles/FogSin" (NOT in the PC install; its own declared
  m_FallbackName = "AG/Unlit-Additive-Soft", which IS in the install). _BaseColor cyan (0,0.4471,0.553), pink
  (0.781,0.1687,0.6216), purple (0.1817,0.0925,0.2206), red (0.766,0.1655,0.1655); _EdgeColor (0,0,0,1); no _BaseMap
  texture (white); cyan: _EdgeDistance 2.23 _EdgePower 1.01 _StartDistance 10 _EndDistance 2 _Amplitude 0.01 _Speed 1;
  pink: _EdgeDistance 5.55 _StartDistance 20 _EndDistance 2 _Amplitude 0.02.
  FogSin program (APK GLES3 text, read as data, scratch apkx/fogsin/p9.txt): Blend One One, ColorMask RGB, ZTest LEqual,
  ZWrite Off, Cull BACK, queue Transparent+50. vertex: pos += _Amplitude*sin(_Time.y*_Speed) (a 1-2 cm bob).
  fragment:  d = eyeDepth - near;  camFade = smoothstep(0,1,(d - _EndDistance)/(_StartDistance - _EndDistance))
             gap = sceneDepth(_CameraDepthTexture, _DEPTH_TEXTURE variant; else far plane) - d
             edge = saturate(gap/_EdgeDistance)^_EdgePower;  col = lerp(_EdgeColor,_BaseColor,saturate(gap/_EdgeColor.w)) * tex
             out.rgb = col * camFade * edge   (ADDITIVE; invisible within 2 m of the eye, full from 10/20 m; soft where
             it meets geometry; Cull Back = seen only from outside the volume, it vanishes when you stand inside it).
- Portal_*_Mat: "Shader Forge/Particles/PortalFog" (not in install) or ".../PortalFogFresnel" (IN the install:
  1df8e0ef #7824694300842317981; install materials Stagelight_Fog_Mat / Stagelight_Sandbox_Fog_Mat use it).
  PortalFogFresnel (install DXBC, scratch shd_pff): One One, ZWrite Off, Cull Off, ZTest LEqual, Transparent+50;
  props _BaseColor _FadeDistance _DistancePower _StartDistance _EndDistance _MobileDistanceEstimate _UValue _VValue
  _VertexColor; out.rgb = _BaseColor.rgb*_BaseColor.a * saturate((uv.x+_UValue)*(uv.y+_VValue))
  * saturate(gap/_FadeDistance)^_DistancePower * smoothstep((d-_EndDistance)/(_StartDistance-_EndDistance)) * N.V
  (gap from the depth texture with _DEPTH_TEXTURE, else min(far, _MobileDistanceEstimate) - d).
  e.g. Portal_Pink_Foyer_Mat (1,0,0.7655,0.878) Fade 28.71 Pow 1 Start 8.92 End 3 U 1 V 0.02;
  Portal_Cyan_Mat (0,0.9172,1,1) Fade 12.2 Pow 1.35 Start 20 End 6 U 1.27 V 0.07.
- LightShaft_*: AG/Unlit-Additive(-Soft) (install; LightShaft_Yellow_Mat is in the install). AG/Unlit-Additive-Soft
  (install b1a0b840 #-8669431370594030110): Blend SrcAlpha One, ZWrite Off, Cull Off; out = vcol*tex*_BaseColor,
  a *= saturate((sceneDepth - d)*_InvFade). AG/Unlit-Additive: same without the soft factor.
- Scifi_FogVent_Mat: "Shader Forge/Particles/AdditiveFadeDistance" (IN the install, 43ea1176 #-8533069272329298177:
  Blend SrcAlpha OneMinusSrcAlpha, Cull Back, ZTest Less); the install's Sandbox_Fog_Mat is the same material
  (_Color (0,0.3785,0.553), VFX_Fog_Tex). Install Scifi_Fog_Computer_Mat = AG/Unlit-Additive-Soft.
- Emissive strips / glow sticks / door glow (install unless marked): SciFi_Emit_Blue/Cyan_Mat AG/Standard-Opaque (APK
  only), Glowstick_Cyan/Purple/Red/Orange_Mat AG/Standard-Opaque (install; Cyan _BaseColor (0.19,0.856,1)
  _EmissionColor (0,1.478,1.478) _EmissivePower 7; Purple (0.637,0.28,1) emis (0.81,0,1.478) x4.94; Glowstick_Pink APK
  only), DoorEmit_Mat AG/Standard-Opaque emis (1,0.985,0.9) x2, DoorGlow_Mat AG/Unlit-Additive-Soft (2,1.875,1.263)
  InvFade 1.63 GradientHorizontal, StageLight_Glow(_Pink/_RedOrange)_Mat AG/Unlit-Additive (APK only),
  LockerFountainStageLight_mat AG/Standard-Opaque, SciFi_Blue_Emit_Cargo1_Mat "Shader Forge/Lighting/EmitBake" (APK),
  Scoreboard_Holographic_Background_Mat Particles/HolographicTextureSpace (install).

### finding D - post profiles per Jumbotron level vs RrPostFx
- Levels 38, 40-49 (Foyer, Battlefield1 ... Stadium, Additive): global Volume -> sharedassets38 #118
  "Scifi_Default_SSFX_Profile" (finding A: Bloom 1.1/0.5/0.617, ACES, exposure +0.5, contrast -14,
  lift (0.8929,0.9395,1,0) gamma (1,0.9814,0.9961,0) gain (1,0.9311,0.9386,0.1264), white balance 0).
- Level39 (Armory): "Scifi_Armory_SSFX_Profile" (sharedassets39 #54): Bloom 1.1/0.7/0.844, Tonemapping Neutral (1),
  temperature 0, exposure 0, contrast +48.3, lift (0.9087,0.97,1,0), gamma (1,0.9814,0.9961,0), gain (1,0.9311,0.9386,0.1264).
- For comparison GT Foyer (level19) "Goblin2_SSFX_Profile": Bloom 1.1/0.345/0.617, Neutral, temp -6, exposure +0.2,
  contrast 5, lift (0.9338,0.954,1) gamma (1,0.9506,0.907) gain (1,0.9803,0.9583,0.0512). (GT in the world uses the
  REF2 house profile instead and was approved.)
- RrPostFxProfiles today: Map(Jumbotron, 0, JumbotronBoss) = Studio house values (bloom 1.04 / 0.15 x BloomScale 0.7,
  Neutral, temp 5, exposure 0.1, contrast 10, lift 0.9216/0.9419); no per-act entries (acts fall back to area 0?).
  => the Jumbotron's own grade is +0.4 EV brighter, ACES instead of Neutral, 3.3x the bloom (0.5 vs 0.15), contrast -14
  instead of +10: a softer, brighter, glowier look. Recommendation in section 4.
- raw: NSP/jt10/lighting/profiles_armory_gt.txt, volprofile.txt

### finding E - lights per scene (APK env/*.env E light lines = rrsk L records; all enabled+active at load)
| scene | lights | point/spot | hemisphere | spec 0 | shadow-flagged | big (r >= 12 m) |
| --- | --- | --- | --- | --- | --- | --- |
| Foyer | 15 | 14/1 | 4 | 8 | 4 | pink key spot (1,0.17,0.98) x2.98 r46 (shadowed), its hemi bounce x3.62 r14, cyan Main Fill (0,0.60,1) x0.58 r26 |
| Armory | 10 | 9/1 | 5 | 10 | 9 | mint spot (0.54,1,0.70) x2.96 r15, Room Fill same x0.3 r15 |
| Battlefield1 | 18 | 15/3 | 9 | 12 | 11 | violet Room Fill (0.72,0.54,1) x1 r25, pink hemi bounce x13.7 r19, cyan hemi bounce x6.5 r20, 3 spots r22-44 |
| Hallway1 | 32 | 29/3 | 16 | 29 | 27 | red encounter spots x4-6.9 r20-49, blue ending spot x9.8 r44, 11 Cyan Bounce (0,0.48,1) x5.9 r4.1 |
| Hallway2 | 34 | 31/3 | 22 | 31 | 34 | magenta encounter spots, blue ending spot x7.6 r44, pink fills x0.2-0.56 r18-25, 11 violet GlowstickFill x0.42 r2.6 |
| Reception | 30 | 28/2 | 15 | 28 | 10 | teal spots x2.1-2.5 r44, teal Room Fill (0.22,0.75,0.85) x0.86 r32 |
| StadiumConcession | 23 | 21/2 | 16 | 21 | 7 | orange spot x7.6 r17, cyan spot x2.6 r24, blue ship hemi bounce x2.3 r27 |
| StadiumEntry | 46 | 43/3 | 14 | 46 | 22 | orange "Sun Light" spot (1,0.45,0.01) x3 r41, 2 violet/blue encounter spots x2.1-2.6 r60 |
| Garage | 33 | 31/2 | 6 | 31 | 9 | cyan encounter spot x11.9 r30, blue ending spot x2.5 r44; 14 Glowstick Bounce (0.15,0.86,1) x0.93 r2.3 |
| Cargo1 | 66 | 63/3 | 20 | 56 | 30 | magenta spots x1.5-3.2 r28-41, cyan ending spot x2.8 r47, violet fills x0.4-1 r18-24; 22 Explosive (0.86,0.38,0.23) x0.17 r2.6 |
| Stadium | 74 | 71/3 | 14 | 71 | 41 | lilac Fill Room (0.92,0.45,1) x0.24 r32, magenta encounter spot x4.6 r29, white loot spot x3.3 r30; 30 Glowstick Bounce x0.36 r2, 18 "Light Hotspot" (0.85,0.95,1) x74.7 r0.5 |
- Every scene has 1-3 "Hotspot" points: intensity 20-90, range 0.34-1 m (the bright pool where a spot hits the floor /
  a fixture's own glow): with Rec Room's 1-d^2/r^2 they are HDR 20-90 x colour within 0.5 m -> bloom sources.
- No light is white except the Stadium's loot spot/hotspot; every room is lit by saturated cyan/blue/magenta/violet neon.
- Caps: kit LightBudget = max(MaxLights 64, min(MaxEnvironmentLights 48, lights)) -> 64 Unity twins; the grid takes EVERY
  kept light (n >= max also go to `sources`), RrLightGrid.MaxLights 160: NO scene loses a light in the grid
  (Stadium 74, Cargo1 66 all in). Only the Unity twins (URP-shaded things: avatars, RrLook.Glass/Emissive) miss
  10 (Stadium) / 2 (Cargo1) lights, the weakest by intensity x range. Additional-light shadows are not reproduced (41 in
  the Stadium are shadow-flagged; Rec Room shadowed them).
- Grid cell overflow (8 lights per cell), simulated with RrLightGrid's box (B box +0.5 m cut to the lights' reach, cone
  ignored = worst case; NSP/jt10/lighting/cellsim.awk/.txt): Foyer 0, Armory 0, Battlefield1 0, Hallway1 0, Hallway2
  5 cells, Reception 24, StadiumConcession 0, StadiumEntry 6 (cells 5.3 x 8.5 x 8.9 m: its B box is 505x110x405 m and
  two r60 spots reach 126x103x213 m), Garage 0, Cargo1 33 (0.5 %), Stadium 35 (0.6 %). Max 8-12 per cell. => the grid
  is NOT losing light anywhere that matters; StadiumEntry's coarse cells are the only quality issue (fix: cut its grid
  box to the kit core / hull bounds, ~35x15x113 m -> 1.5 x 1.25 x 4.7 m cells).

### finding F - how Rec Room's AG/Standard-Opaque makes its glow (install DXBC + UnityPerMaterial offsets)
- AG/Standard-Opaque lit PS (agstd p4_s1_22F9EE; _EmissivePower = UnityPerMaterial byte 112 = cb4[7].x, confirmed from
  the install shader's binding table, NSP/jt10/lighting/agstd_full.json):
    col    = vertexColour * _BaseColor;  t = _BaseMap(uv)
    albedo = lerp(t.rgb, t.rgb*col, t.a)            (the texture's ALPHA is the tint mask)
    emit   = t.rgb * t.a * col * _EmissivePower      (the same alpha mask is the EMISSION mask; _EmissionColor unused)
    out    = lerp(fogColour, (SH ambient + grid lights + reflection) * albedo + emit, fogFactor)
  => a level material glows where its texture alpha is 1, at _BaseColor x _EmissivePower (linear HDR); fog covers the
  glow too.
- Emissive level materials of the Jumbotron hull (APK, all levels; _EmissivePower > 0) with the texture's measured
  means (scratch apkx `texavg`, level textures decoded from ASTC as numbers only; NSP/jt10/lighting/texavg.txt):
  | material | EP | _BaseColor | texture (install?) | tex mean sRGB, mean a | plate-average emission (linear) |
  | --- | --- | --- | --- | --- | --- |
  | SciFi_Emit_Cyan_Mat (Battlefield1 45 m2) | 10 | 0,0.586,1 | none (a=1) | - | (0, 3.02, 10.0) |
  | SciFi_Emit_Blue_Mat (Hallway1 6, Cargo1 2 m2) | 4.45 | 0.189,0.704,1 | none | - | (0.13, 2.02, 4.45) |
  | WallSign_Red_Mat (2-6 m2 per room) | 9 | 1,0.0735,0.0735 | WallSign_Tex (NO) | 0.637, a 0.422 (rgb*a lin 0.101) | (0.91, 0.006, 0.006); letters ~3.4 |
  | WallSign_Blue_Mat (Battlefield1 3 m2) | 1.72 | 0.287,0.616,1 | WallSign_Tex (NO) | same | (0.012, 0.059, 0.174) |
  | Door_SciFi_Cardboard_mat (79-335 m2) | 2.37 | 0,1,1 | install material (inst=1) | exact | exact |
  | SciFi_DoorTrigger_Mat (12 m2) | 1.37 | 1,0,0.269 | install (inst=1) | exact | exact |
  | Terminal_SciFi_Prop_mat (Foyer 28 m2) | 1 | 1,1,1 | Terminal_SciFi_Prop_Tex (NO) | 0.44, a 0.074 | 0.016 (screens only) |
  | Carpet_Blacklight_Swirls_mat (Armory 138 m2) | 1 | 1,1,1 | Carpet_Blacklight_Swirls_Tex (NO; its _Norm is) | (0.098,0.119,0.27) a 0.277 | (0.025, 0.035, 0.112) |
  | Pillar_SciFi_Pink_mat (74-214 m2) / _Red (Hallway1 169) | 0.5 / 0.4 | 1,0.392,0.946 / 1,0.109,0.454 | Pillar_SciFi_tex (NO) | 0.375 grey, a 0.009 | ~0 (thin strips) |
  | Wall_SciFi_Decor_Blue_mat (Concession 66 m2 + dress) | 1.24 | 0.114,0.680,0.961 | (NO) | 0.434 grey, a 0.003 | ~0 |
  | Enemy_Jumbotron_Mat (Stadium 75 m2) | 3 | 1,0.241,0.558 | Jumbotron_Tex (NO) | 0.24, a 0.019 | (0.018, 0.0001, 0.0015) |
  | Glowstick_Cyan/Purple/Red/Orange_Mat | 7/4.94/20/5.88 | | install materials | exact | exact |
- CONSEQUENCE for the look: the pink pillars (and the Decor_Blue walls) are GREY metal (tex mean 0.375 sRGB) with 1 %
  pink strips in Rec Room; tint and glow only where alpha = 1. As flat plates their mean albedo is ~ the grey texture
  (lin 0.12), NOT the pink _BaseColor. Our RrLook.Solid(pink) = whole pink pillars (part of the "pink boxes" look).
  Same rule for every level-only textured material: flat colour = mean(lerp(tex, tex*col, a)); flat emission =
  col_lin * mean(tex_lin*a) * EP. Brick_Tex a = 1 (mean 0.735 sRGB) -> the cinderblock vertex colour tints fully;
  Wallpaper_SciFiPanels_Tex mean 0.381 (our Solid 0.667 is ~2.7x too bright in linear); Linoleum 0.51; Wood 0.72.
- Our current glow: RrLook.Emissive(col, 1.3-1.4) (URP Lit, not grid-lit) for names containing Emit/Glow ->
  SciFi_Emit_Cyan ~(0,0.42,1.4) instead of (0,3,10): 7x too dim, it hardly passes the bloom threshold. WallSign_* hit
  neither rule -> RrLook.Solid, the red exit signs do not glow at all.

### finding G - environment rows (RrEnvironmentTable key=Jumbotron_*) vs the APK RenderSettings, and what they do
- The table rows are byte-for-byte the APK RenderSettings (apkscene env -> envcs): fog linear, trilight ambient,
  sky SkyboxSciFiQuest (Skybox/Procedural: tint 0,1,0.004 ground 0.538,0.236,0.147 atmosphere 1.76 exposure 0.18
  no sun disk), refl 1 (skybox), no sun, no custom reflection. Nothing is mis-parsed. Garage's "intensity 0" only
  matters for skybox ambient (ignored for trilight, as in Unity).
- Stored m_AmbientProbe = the procedural sky's SH, the same in all 11 levels (see B); Rec Room's shaders sample SH
  (unity_SHAr..C), and Unity rebuilds the probe from the trilight colours for a Trilight scene (iteration 8 note +
  RrEnvironment's own runtime observation) -> the trilight colours ARE what Rec Room showed. I found no Rec Room
  component in the levels that overrides RenderSettings (level38/49 script census: no lighting/ambient script).
- What the trilight gives (Unity SH, cosine-weighted: wall ~ 0.576 E + 0.212 (S+G), floor ~ E/3 + 2S/3, linear;
  shown on a 0.5-grey albedo before any light):
  | scene | wall ambient (lin) | 0.5-grey wall sRGB | floor ambient (lin) | fog colour, start-end |
  | --- | --- | --- | --- | --- |
  | Foyer | 0.046, 0.006, 0.692 | (25,4,107) VIOLET-BLUE | 0.027, 0.005, 0.406 | 0,0.404,0.604 teal, 5-45.3 |
  | Armory | 0.000, 0.003, 0.002 | black | ~0 | 0,0.604,0.479 green-teal, 2.9-71.5 |
  | Battlefield1 | 0.039, 0.021, 0.081 | (22,14,36) | 0.024,0.013,0.044 | 0,0.429,0.604, 7.3-64.5 |
  | Hallway1 | 0.111, 0.007, 0.038 | (43,5,22) crimson | 0.066,0.005,0.031 | 0.129,0.259,0.573, 5-45.3 |
  | Hallway2 | 0.001, 0.002, 0.010 | black | ~0 | 0,0.157,0.384 navy, 0-40.4 |
  | Reception | 0.003, 0.077, 0.161 | (2,35,52) teal | 0.006,0.064,0.173 | 0,0.110,0.259, 6.2-34.4 |
  | StadiumConcession | 0.008, 0.034, 0.068 | (5,21,32) | 0.006,0.021,0.045 | 0.243,0.153,0.373 mauve, 5-45.3 |
  | StadiumEntry | 0.014, 0.000, 0.048 | (10,0,26) | 0.009,0,0.027 | 0.004,0.028,0.180, 0-354 |
  | Garage | 0.000, 0.002, 0.007 | black | ~0 | 0,0.396,0.600 teal, 5-45.3 |
  | Cargo1 | 0.004, 0.002, 0.004 | black | ~0 | 0,0.404,0.604 teal, 5-77.4 |
  | Stadium | 0.022, 0.001, 0.056 | (15,1,29) | 0.016,0.001,0.037 | 0,0.031,0.341 blue, 5-60.6 |
  => ONLY the Foyer's ambient is strong: its equator (0.3104, 0.0793, 1.0815 HDR) puts ~0.69 linear blue (15x the red)
  on every vertical surface = the flat violet walls (+ the teal fog on everything past 5 m, full at 45 m: the "teal
  ceiling" is Unlit_Black_mat 3,374 m2 fogged). The 10 other rooms have near-black ambient: their lights, emission,
  portals and mist are all the light they have.
- The violet in acts 1, 2, 5, 8 is NOT theirs: the jt audit (iter10/jt_audit.md "BIG FINDING 2") shows the automatic
  pick falls back to Jumbotron_Foyer (RrEnvironment.KeyFor(Jumbotron)) because each act's arrival point lies 0.4-5 m
  outside the kit's placed-record bounds (+3 m margin) that RrEnvironment.SceneBuilt registers; the hull is built
  after the kit and is not in those bounds. RrJumbotronRoom never calls RrEnvironment.SetArea (Golden Trophy does).
- How RrEnvironment applies per area: SceneBuilt registers each kit scene with its table row (Find(key) else
  FromScene), Pick every 0.25 s takes the smallest registered box containing the head (or the SetArea/ForcedKey
  name), else the activity default; Apply sets RenderSettings ambient mode/colours (the probe follows at once),
  fog (linear, Rec Room shaders only - world-bundle URP shaders have fog stripped, so RrLook.Glass/Emissive and URP
  Lit are NOT fogged), the sky (Skybox/Procedural with proc= values), reflection intensity 1 (the sky gradient's max
  0.41 > 0.06, so ReflectionOf keeps it: metallic smooth materials - Pillar_SciFi 0.85/1.0, Door_SciFi_Cardboard
  0.85/0.855, Paint_DarkGrey smoothness 0.897 - mirror the magenta/violet procedural sky), no sun (world sun off).

### finding H - where the fog volumes / portals / shafts are, and what the world does with them today
Plate areas (gtscene G plate records, NSP/jt10/lighting/plate_areas.tsv; all skipped by RrJumbotronHull.NotASurface):
- Area mist (FogSin): Foyer cyan 190 m2 (1,587 plates <= 3 m2, y -6.9..-0.7 = a low mist layer over the audience/stage
  floor at y -6.5), Battlefield1 cyan 261 + pink 71, Hallway1 cyan 93 + red 24, Reception purple 110 (1,147 plates,
  y -2.5..-0.7, mostly horizontal). Mixed orientations = a lumpy mist mesh cut into rectangles.
- Portal glows (PortalFog / PortalFogFresnel): Foyer ONE vertical plate 34.5 x 14 m at x 20.08 (centre y -2.89,
  facing +x) = a pink haze curtain across the whole hall between audience and stage (Portal_Pink_Foyer_Mat
  (1,0,0.7655) a 0.878, U 1 V 0.02 = fades to 0 at the bottom, camera fade 3 -> 8.92 m, depth fade 28.71 m pow 1);
  Stadium ONE horizontal 31 x 47 m sheet at y -4.43 (Portal_Red_Stadium_Mat (1,0,0.354) fresnel, F 6.98 pow 1.51);
  StadiumConcession floor-level sheets 1,481 m2 blue + 1,362 m2 pink (1,294 + 1,045 plates at y -3..-2.2) + cyan 729
  + purple 108; Garage 3 vertical blue walls 651 m2 (8.6x13.5, 15.4x14.9, 30.7x9.9 m); Hallway1 blue 263 + red 74;
  Hallway2 red 160, purple 106+65+50+50, cyan 35; Reception cyan 112; StadiumEntry purple 336+123, cyan 213;
  Cargo1 blue/cyan/purple 190; Battlefield1 cyan 86 + pink 54. All 19 portal materials' values:
  NSP/jt10/lighting/portal_values.txt (colour, P=_DistancePower, E=_EndDistance, F=_FadeDistance, S=_StartDistance, U, V).
- Light shafts: install mesh LightArchShaft with a level-only material (?LightShaft_*_Mat) as kit R records: Foyer 3,
  Armory 1, Battlefield1 3, Hallway1 3, Hallway2 3, Reception 3 (1 = install LightShaft_Yellow_Mat), Concession 4,
  StadiumEntry 3, Garage 2, Cargo1 5, Stadium 20. BUG: a "?" material is not loaded, RrSceneKit.MaterialOf falls back
  to RrLook.Solid(0.62,0.62,0.64): every shaft is an OPAQUE GREY cone (audit dumps: "Rr Solid 9E9EA3" batches: Foyer 7,
  Armory 5, act1 12 ... Cargo1 34, Stadium 23 - the shafts plus other level-only props).
  Same fallback hits other GLOWING level-only kit materials: ?Quest_SciFi_LaserTrail (Hallway2 x15, StadiumEntry x18:
  AG/Unlit-Additive-Soft _BaseColor (2,0,0,1) HDR red beams), ?PropBox_SciFi_mat (x2-45 per room: AG/Standard-Opaque
  (0.096,0.7875,1) EP 2.32 = cyan-glowing crates), ?Barrel_SciFi_Explosive_Mat (x4-25, EP 1),
  ?SciFi_Blue_Emit_Cargo1_Mat (Concession x1, EmitBake: albedo (0.547,0.539,0.495) emission (0,1.513,3.596)).
  Light-shaft values (APK): Cyan AG/Unlit-Additive-Soft (0.0243,0.3489,0.7608,0.4118) InvFade 3; HotPink AG/Unlit-Additive
  (0.8396,0,0.5832,0.4902); Red (0.9623,0,0.0955,0.3843); Purple (0.6966,0,1,0.3373); Green (0.2599,0.934,0.4846,0.1176);
  Blue_Stadium (0,0.3326,0.7725,0.2588); Quest_Orange -Soft (2,0.2562,0,0.256); Quest_Projector -Soft alpha 0 (off);
  all GradientHorizontal (install texture). AG/Unlit-Additive(-Soft): rgb = vcol*tex*_BaseColor, a = ..*_BaseColor.a
  (*soft depth factor), Blend SrcAlpha One -> added colour = _BaseColor.rgb * a * gradient.

## 4. Recommendations (concrete; for the fix phase - nothing edited here)
### 4.1 Environment (Core/RrEnvironment usage; the table values stay - they are Rec Room's own)
- R-ENV-1 (a bug, and the biggest single colour error in acts 1/2/5/8): in RrJumbotronRoom.QuestStep call
  RrEnvironment.SetArea(key) with act -1 "Jumbotron_Foyer", 0 "Jumbotron_Armory", 1 "Jumbotron_Battlefield1",
  2 "Jumbotron_Hallway1", 3 "Jumbotron_Hallway2", 4 "Jumbotron_Reception", 5 "Jumbotron_StadiumConcession",
  6 "Jumbotron_StadiumEntry", 7 "Jumbotron_Garage", 8 "Jumbotron_Cargo1", 9 "Jumbotron_Stadium". Call SetArea(null) when
  the room is left, as Golden Trophy does. The alternative is to register each scene with the rrsk B box or the hull bounds.
- R-ENV-2: keep every Jumbotron_* row unchanged (fog, trilight, sky, refl 1, no sun). The Foyer's violet equator is
  Rec Room's own. It reads flat only because the walls lost their textures, emission and haze (4.3-4.5). If the user still
  finds the Foyer too violet after 4.3-4.5, the one knob is the Foyer equator (for example x0.6 -> 0.186,0.048,0.649).
  Test before changing it.

### 4.2 Post-processing (Core/RrPostFxProfiles): use the levels' own Android profiles instead of the REF2 house one
- Map(Jumbotron, 0, ScifiDefault) (areas 0 and 2-10 fall back to it) and Map(Jumbotron, 1, ScifiArmory).
- ScifiDefault = "Scifi_Default_SSFX_Profile" (APK sharedassets38 #118):
  - Bloom: threshold 1.1, intensity 0.5 (x BloomScale 0.7 = 0.35), scatter 0.617, highQualityFiltering 0.
  - Tonemapping: mode 2 (ACES).
  - WhiteBalance: temperature 0.
  - ColorAdjustments: postExposure 0.5, contrast -14.
  - LiftGammaGain: lift (0.8929,0.9395,1,0), gamma (1,0.9814,0.9961,0), gain (1,0.9311,0.9386,0.1264).
- ScifiArmory = "Scifi_Armory_SSFX_Profile" (sharedassets39 #54):
  - Bloom: 1.1 / 0.7 (x0.7 = 0.49) / 0.844, HQ 0.
  - Tonemapping: 1 (Neutral).
  - Temperature 0, postExposure 0, contrast 48.3.
  - Lift (0.9087,0.97,1,0); gamma and gain as in ScifiDefault.
- Effect compared with today's house profile (exposure 0.1, contrast +10, bloom 0.105, Neutral): +0.4 EV, softer
  contrast, 3.3x the bloom, ACES.

### 4.3 Portal glows (all 19 Portal_* materials; skipped today): install shader with the level's numbers
- Material:
  - Clone the install material Stagelight_Fog_Mat (bundle 43ea1176ca25c0333547813ec3298808, pathId -973096557463603888,
    shader "Shader Forge/Particles/PortalFogFresnel": One One, ZWrite Off, Cull Off, queue Transparent+50).
  - Set these per level material from NSP/jt10/lighting/portal_values.txt: _BaseColor (rgb, a), _StartDistance,
    _EndDistance, _FadeDistance, _DistancePower, _UValue, _VValue.
  - Also set _MobileDistanceEstimate 10000, keyword _VERTEX_COLOR on, keyword _DEPTH_TEXTURE off (we cannot rely on the
    client's depth texture), renderQueue 3050.
  - The install DXBC shows the _VERTEX_COLOR variant multiplies rgb by the vertex colour (v5.xyz).
- Mesh:
  - Make the plates double-sided. The fresnel term is max(0, N.V), so a back face renders black.
  - UV0 runs 0..1 per plate (u along local x, v from the plate's bottom edge to its top) for the (u+U)(v+V) mask.
    Horizontal plates use mask 1.
- Depth fade (Rec Room on PC reads the depth texture):
  - Bake saturate(gap/_FadeDistance)^_DistancePower per plate into the vertex colour rgb.
  - gap = a physics raycast from the plate centre along -normal and +normal (take the larger) against the room's C
    colliders, capped at 50 m.
  - Measured examples:
    - Foyer curtain (x 20.08): gap about 2.7 m at stage level (the x 17.4 wall) -> 0.09; above the stage about 15 m
      (back wall at x 4.9) -> 0.52. Use 0.5 for this single plate.
    - Stadium red sheet (y -4.43) over the floor at y -5.2: gap 0.77 m -> 0.036. It is a faint glow near the floor,
      strong only at grazing angles; without the bake it would be about 25x too strong.
- Per scene (colour a, S/E/F/P = _StartDistance/_EndDistance/_FadeDistance/_DistancePower):
  - Foyer: Pink_Foyer (1,0,0.7655,0.878) 8.92/3/28.71/1, x0.5.
  - Battlefield1: Cyan (0,0.9172,1) 20/6/12.2/1.35; Pink (1,0,0.7655,0.878) 8.92/3/28.71/1.
  - Hallway1: Blue (0.038,0.2785,1) 20/6/12.2/1.35; Red (1,0,0) 20/6/12.2/1.35.
  - Hallway2: Red_Hallway2 (1,0,0.354,0.616) 10/2/7.88/2; Purple_Hallway2 (0,0.454,1) 20/2/6.3/1.71; Faint
    (0.3824,0,0.8529) 10/1.5/22.8/1.35; Purple_Cargo (fresnel) 10/2/16.8/1.35; StadiumEntry2 (0.356,0,1,0.297)
    20/6/12.2/1.35; Cyan 20/6/12.2/1.35.
  - Reception: Cyan_StadiumEntry (0,0.715,1) 20/6/12.2/1.88.
  - StadiumConcession: Blue_Concession (0.0726,0.1813,0.5809) 11.5/1/18.66/1.05 (1,481 m2); Pink_Concession
    (1,0.3464,0,0.878) 11.5/1/18.66/1.64 (1,362 m2; U 0 V 0 = a uv-product mask); Cyan_Concession (fresnel)
    10/5/19.78/2.24; Purple_Concession (1,0.4345,0) 15.77/6/22.3/1.35.
  - StadiumEntry: Purple_StadiumEntry (0.3448,0.038,1) 15/1/19.26/1.35; StadiumEntry2; Cyan_StadiumEntry.
  - Garage: Blue_Garage (0.038,0.6766,1) 20/6/12.2/1.35, V -0.32 (3 walls, 651 m2).
  - Cargo1: Blue_Cargo1 (E 0.1), Cyan, Cyan_StadiumEntry, Purple_Cargo, Blue.
  - Stadium: Red_Stadium (1,0,0.354) 10/6/6.98/1.51; Cyan_StadiumEntry.
- Better data (a tool change in DN/tools/gtscene): write each portal/mist plate's UV0 corners (uv=u0,v0,u1,v1), so the
  mask is exact instead of assumed.

### 4.4 Area mist (Scifi_AreaFog_*; skipped today)
- Material:
  - Clone the install Sandbox_Fog_Mat (bundle 43ea1176..., pathId -1312024992473109935, shader
    "Shader Forge/Particles/AdditiveFadeDistance"). Its colour is 2*tex*vcol*_TintColor. Its alpha fades in from
    _EndDistance to _StartDistance and by the depth gap/_SoftParticles. Blend SrcAlpha OneMinusSrcAlpha, Cull Back,
    queue Transparent (set 3050).
  - _BaseMap = a white texture (FogSin had none).
  - _StartDistance/_EndDistance = FogSin's (cyan 10/2, pink 20/2).
  - _SoftParticles = FogSin's _EdgeDistance (cyan 2.23, pink 5.55).
  - _TintColor with a = 0.3, so the added colour equals FogSin's additive one while 70 % of the room shows through:

    | mist | _TintColor linear (a = 0.3) | sRGB |
    | --- | --- | --- |
    | cyan | (0, 0.281, 0.444) | (0, 0.566, 0.697) |
    | pink | (0.954, 0.040, 0.574) | (0.979, 0.222, 0.782) |
    | purple | (0.046, 0.015, 0.066) | (0.238, 0.127, 0.286) |
    | red | (0.913, 0.039, 0.039) | (0.961, 0.218, 0.218) |

  - Vertex alpha = the baked depth fade saturate(gap/_EdgeDistance)^_EdgePower (raycast along -normal); vertex rgb 1.
- Alternative closest to Rec Room's own declaration: AG/Unlit-Additive-Soft (FogSin's m_FallbackName; clone the install
  Scifi_Fog_Computer_Mat or DoorGlow_Mat). It is exactly additive, with _BaseColor = the FogSin colour and
  _InvFade = 1/_EdgeDistance. It has no near-camera fade, so the mist is at full strength at the player's feet.

### 4.5 Glow strips, signs, shafts, lasers and glowing props (the bloom sources)
- Build level materials the Golden Trophy way: an install AG/Standard-Opaque template, as in RrGoldenTrophyHull.Plan,
  with the level's _BaseColor AND _EmissivePower. Rec Room's glow is t.rgb*t.a*col*EP (finding F).
  - Where the texture is in the install, the result is exact.
  - No texture:

    | material | _BaseColor | EP | result |
    | --- | --- | --- | --- |
    | SciFi_Emit_Cyan_Mat | (0,0.5862,1) | 10 | HDR (0,3.0,10.0) |
    | SciFi_Emit_Blue_Mat | (0.189,0.7037,1) | 4.45 | |

  - Level-only textures, as flat plates:

    | material | flat colour | EP |
    | --- | --- | --- |
    | WallSign_Red_Mat | col (1,0.0735,0.0735) | 0.9 (the letters' average) |
    | WallSign_Blue | col (0.287,0.616,1) | 0.17 |
    | Pillar_SciFi_Pink/Red | the texture's grey mean, 0.375 sRGB | 0 |
    | Wall_SciFi_Decor_Blue | grey mean 0.434 sRGB | 0 |
    | PropBox_SciFi (tex a 0.041) | grey mean 0.288 sRGB | 0 |
    | Terminal | grey mean 0.435 sRGB | 0 |
    | Enemy_Jumbotron | the texture's grey mean | 0 |
    | Carpet_Blacklight_Swirls | sRGB (0.098,0.119,0.27) | 1 on white -> about (0.028,0.04,0.128) glow (138 m2 Armory floor) |
    | SciFi_CoverWall01-03_mat | col (0.294,0.883,1) | flat 0.25, or an install-texture substitute |

    - Pillar, Decor, PropBox, Terminal and Enemy_Jumbotron keep their colour in 1-7 % strips.
    - SciFi_CoverWall01-03's EP 1 applies only through its texture alpha of about 0.23-0.27, hence the flat 0.25.
  - Wall_SciFi_Cover_Modular_mat is OPAQUE grey (0.442 sRGB, EP 0). Its _BaseColor alpha 0.354 is unused, so it must not
    become RrLook.Glass.
- Drop RrLook.Emissive (URP Lit x1.3-1.4): it is not grid-lit, not fogged, and 5-7x too dim for Rec Room's bloom
  threshold of 1.1.
- Kit "?" materials that glow (the RrSceneKit fallback grey today):
  - ?LightShaft_* (the LightArchShaft mesh is in the install) and ?Quest_SciFi_LaserTrail: clone the install
    LightShaft_Yellow_Mat (bundle 43ea1176..., pathId -4586346334188962876, AG/Unlit-Additive-Soft, GradientHorizontal).
    Set the level's _BaseColor (finding H list) and _InvFade 3. The lasers use _BaseColor (2,0,0,1) and _InvFade 1.
  - ?PropBox_SciFi_mat and ?Barrel_SciFi_Explosive_Mat: use the AG template. PropBox is (0.096,0.7875,1) at EP 2.32 on a
    texture alpha of 0.041 -> flat grey 0.288 + EP 0.1. The barrel is white at EP 1 times its mask.
  - This needs a material-substitution hook in the kit (a RrSceneKitOptions callback for "?" names), supplied by
    RrJumbotronArea.

### 4.6 Lights / grid
- Nothing is lost. Keep MaxLights 64 / CellSize 12: the grid takes all 10-74 lights (the cap is 160). Cell overflow is
  under 0.6 %.
- StadiumEntry only: cut its grid box to the kit core / hull bounds (about 35 x 15 x 113 m) instead of B box ∩ light
  reach (126 x 103 x 213 m, cells 5.3 x 8.5 x 8.9 m). That gives cells of about 1.5 x 1.25 x 4.7 m (for example, pass the
  Filter or the core bounds as LocalVolume).
- Shadow-flagged lights (Rec Room shadowed 4-41 per room) cannot be reproduced on Rec Room shaders (iteration 9). No action.

### 4.7 Expected picture after 4.1-4.5 (to verify in the harness or the headset)
- Foyer:
  - Blue-violet walls with brick texture variation, lit cyan by the fills.
  - A pink haze curtain over the stage.
  - Cyan mist glowing over the audience floor at 5-20 m.
  - The pink key spot's pool and the hotspots blooming; the red exit signs glowing.
  - Grey (not pink) pillars that reflect the magenta sky.
- Acts 1/2/5/8 in their own colours:
  - Battlefield1: violet-cyan.
  - Hallway1: crimson ambient, cyan bounces, red and blue portals.
  - Concession: mauve fog and a huge blue/pink floor glow.
  - Cargo1: near-black, with magenta and cyan spots.
- Brighter overall, from the +0.4 EV ACES grade and 3.3x the bloom.
