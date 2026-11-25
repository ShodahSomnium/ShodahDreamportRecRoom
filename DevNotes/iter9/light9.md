# iter9 light9 - Rec Room's realtime lighting in the client: per-object light limit, AO, URPLight, torches

Owner: light9 agent. Owns Core/RrSceneKit*.cs, RrSceneData.cs, Core/RrEnvironment*.cs, Core/RrPostFx*.cs,
DN/tools/apkscene (URPLight keys), small lighting-only edits in Games/Quests, Games/Paintball, Places.

## Status
- [x] 0 user question: what do the torches do in the build / dumped assets (answer below as found)
- [x] 1 per-object light limit (the real cause: Rec Room shaders take no URP light at all; grid + merging)
- [x] 2 AO (client HBAO in our Volume profile; only when the player turns AO on)
- [x] 3 URPLight emulation (apkscene keys + table + runtime)
- [x] 4 torches: every light in the grid, lantern glow warmer
- [x] 5 verify (compile 0 errors x3, guard proven; harness runs used 6/6)

## Findings
### 0. What the torches are in Rec Room (Android level19 Quest_Foyer, apkscene `tree`/`mat` commands added)
- Every torch/lantern is a mesh + flame cards + particles + REALTIME lights; nothing is baked:
  - `[WallTorch]` / `[Torch_Wall]` (9 + 2 in the Foyer): WallTorch_LOD0 mesh (LanternPost_Mat), two FlamePlane cards
    (PaperFlame_Mat, shader "Shader Forge/Particles/PaperFlame"), a `[VFX_Flame]` particle system
    (VFX_SquareParticle_mat = Legacy Particles/Additive (Soft) on the "Square" texture: Rec Room's flames ARE squares).
  - Each torch's light lives in `Systems/FoyerLighting`: "Torch Hot Spot" point (1, 0.393, 0) intensity 1, range 1.41 m
    + "Torch Light" point same colour, intensity 0.852, range 5.80 m (4.16 m on the upper stage). 11 + 11 lights.
  - `[Lantern_Post_Wooden]` (3): LanternPost_LOD0 post + HangingLantern_LOD0 (LanternPost_Mat), a Flame group
    (2 FlamePlane cards LaternFlame_Mat + 2 CandleFlameMesh Candle_Unlit_Mat = AG/Unlit-Additive, _BaseColor 1.30/0.09/0 HDR)
    and "EmitCubeCandle (1)": point (1, 0.692, 0.278) int 1 range 2.81 with URPLight isHemisphereLight=1 inner 34 outer 133,
    forward = straight down (a downward half-sphere lamp).
  - Every light: m_Lightmapping 4 (Realtime), shadows none, URPLight specularContribution 0; the lamp ones also carry
    UniversalAdditionalLightData with Rec Room's own fields m_RRLightVersion 0, m_SpecularContribution 1 (REF2 Studio
    copy: m_RRLightVersion 1, m_SpecularContribution 0).
- Materials (Android level = PC install, same values): LanternPost_Mat AG/Standard-Opaque _BaseColor (1,0.466,0.118)
  _EmissionColor (0.915,0.360,0) _EmissivePower 0.76, _BaseMap Wall_Torch_Lantern_Post_Tex: the torch cup/lantern glass
  glows through the texture. In the install: LanternPost_Mat, PaperFlame_Mat, LaternFlame_Mat, Candle_Unlit_Mat,
  VFX_SquareParticle_mat, FlamePlane + CandleFlameMesh meshes, Wall_Torch_Lantern_Post_Tex (bundle 43ea1176 / 1df8e0ef).
  NOT in the install: the WallTorch/LanternPost/HangingLantern meshes (our dress stand-ins).
- Proof nothing is baked: AG/Standard-Opaque's keyword list (install shader) = STEREO*, _METALLICSPECGLOSSMAP*,
  _FAST_MODE_SHADING, FOG_*, _ADDITIONAL_LIGHTS(_VERTEX), _RENDER_FEATURESET_MOBILE/FULL, INSTANCING_ON, _NORMALMAP,
  _RECEIVE_SHADOWS_OFF, _ADDITIONAL_LIGHT_SHADOWS, _SCREEN_SPACE_OCCLUSION, _ALPHATEST_ON, _ALPHAPREMULTIPLY_ON,
  DOTS_INSTANCING_ON: no LIGHTMAP_ON / DIRLIGHTMAP / SHADOWS_SHADOWMASK variant exists, so Rec Room's surfaces cannot
  sample a lightmap; plus every renderer's m_LightmapIndex 65535 (iter8 env) and REF2 GoldenLobby Torch Hot Spot
  m_Lightmapping 4. The "WarmGlow (N)" lights in the PC install are the Charades Ink Space hull's (not torches).
- Our build (iter8): the torch stand-ins are orange boxes (dress) + an emissive cream box/flame, the F records place
  Sandbox_Campfire's square-particle flame and the candle card; all 40 Foyer lights are kept by the kit (48 cap).

### Client facts for this pass
- Per-object light limit = the user's "max lights" setting: quality presets 2/4/4/8/8 (GraphicsControl.LightMaxCount ->
  URP asset maxAdditionalLightsCount). Read only; a world must not change it.
- HBAO: HorizonBasedAmbientOcclusion.Universal.HBAO (VolumeComponent). Every client quality preset sets AO tier 0 =
  OFF (the renderer feature is disabled; nothing a world can do). When the user turns AO on, GraphicsControl writes
  tier + intensity (slider, default 1) + radius (default 0.8, presets 0.05 -> clamped 0.25) into EVERY global Volume
  found by FindObjectsByType, adding HBAO if missing, after the world scene loads (WorldTransitionControl
  StartConnectToSession -> GraphicsControl.LoadSettingExt) and on every settings change. Our "Rr Post" volume was
  created at the first room Enter (later), so the client's HBAO never landed in it unless a setting changed in-world.

### THE finding: Rec Room's shaders never receive a point/spot light in the client (install shader disassembly)
Tool: scratchpad shdis (reads the install Shader object, LZ4-decompresses its program blob, D3DDisassemble on every
DXBC program; research only). AG/Standard-Opaque: 1,120 programs (696 ps / 424 vs).
- Rec Room's URP fork does NOT use URP's per-object light lists (no unity_LightIndices anywhere). Every lit Rec Room
  shader (AG/Standard-* x15, AG/MakerPenShape(-Batched) = our RrLook.Solid, AG/Terrain*, AG/Fresnel*, AG/Avatar*,
  AG/StudioWater, Rec Room Studio Target/Lit = the cached Rec Center, even the install's own copy of URP/Lit) reads its
  additional lights from a WORLD-SPACE LIGHT GRID:
    v = worldPos * _WorldSpaceToVoxelCoefficients + _VoxelVolumeWorldOriginPreMultiplied  (cb0[17], cb0[16])
    v = min(uint3(v), (23, 11, 23)); cell = v.x + 24 v.y + 288 v.z     (24 x 12 x 24 grid)
    _Voxels (StructuredBuffer<uint>, 9 per cell): [count, i0..i7]    -> up to 8 lights per cell, per pixel
  then per light i (cbuffer AdditionalLights, 160 slots): _AdditionalLightsPosition/Color/Attenuation/SpotDir with
  Rec Room's OWN semantics:
    atten = saturate(1 - d^2 / Attenuation.y)                       (y = range^2: NO inverse-square falloff)
    spot  = s*s*(3-2s) squared, s = saturate((dot(SpotDir, L) - Attenuation.z) / (Attenuation.w - Attenuation.z))
    diffuse = Color.rgb * atten * spot * N.L;  specular = Blinn-Phong * Color.rgb * atten * Color.a  (a = specularContribution)
    shadows: _AdditionalShadowParams[i] (w < 0 = none)
  Vertex-lit variant: _CompactAdditionalLights* + _VoxelsTexture, smoothstep(1 - d^2/r^2).
- The Somnium client runs stock URP 17: nothing binds _Voxels, so the cell count reads 0 and the whole additional-light
  block is skipped: EVERY Rec Room surface (install art, maker-pen walls, the Rec Center cache) gets only the sun and
  ambient. The torches' lights reached only our few URP-Lit materials and the avatars. Even with _Voxels bound, URP's own
  arrays would be wrong for these shaders (URP writes Attenuation.y = 2.78 for every light, spot z/w as scale/offset,
  point SpotDir (0,0,1)).
- Falloff comparison for a Foyer "Torch Light" (0.852, 5.8 m) on the floor 1.65 m below / 3 m to the side:
  Rec Room 0.78 / 0.55; stock URP inverse-square 0.31 / 0.056. The 39 m fill light at 10 m: Rec Room 0.42, stock 0.0045.

## Implementation (11:40)
- Core/RrLightGrid.cs (new): Rec Room's light grid per scene. 24x12x24 cells over the scene's box, each cell the 8
  strongest lights (brightness x (1 - d^2/r^2) at the cell's nearest point, x cone for spots/hemisphere lights), a
  ComputeBuffer `_Voxels` (9 uints per cell) + `_VoxelVolumeWorldOriginPreMultiplied` / `_WorldSpaceToVoxelCoefficients`
  + Rec Room's arrays (Position; Color = linear colour x intensity, a = specularContribution; Attenuation = (1/r^2, r^2,
  cos outer/2, cos inner/2) with points (-2, -1) = no cone; SpotDir = -forward) + `_AdditionalShadowParams` w = -1, all in
  ONE MaterialPropertyBlock on every renderer of the scope whose material uses a grid shader (AG/Standard-*,
  AG/MakerPenShape*, AG/Terrain*, AG/Fresnel*, AG/Avatar*, AG/StudioWater, Rec Room Studio Target/Lit). A block beats URP's
  globals of the same names, so URP-shaded objects keep URP's lights. Builds once travel is idle and the host is active,
  rescans the scope every 3 s (new props, enemies; re-applies a block someone cleared), merges into someone else's block.
- Kit: every point/spot light (up to 160) -> the grid (host = kit root, scope = kit root's parent: the GT/Jumbotron area
  with its hull and dressing, the Paintball level root); Unity lights (the old budget) stay for URP-shaded things and
  avatars, set up by RrLightGrid.ConfigureUnity: hemisphere lights -> spots (outer capped 179, inner), stock-URP falloff
  factor StockFactor(r) = clamp(0.213 r^2, 0.5, 4) (matches Rec Room's light at half the range). With the grid on the
  small light cells / separate renderers are off (normal CellSize merging): fewer draws, which pays for the renderers
  leaving the SRP Batcher.
- URPLight data: apkscene Env/Emit now write `spec= hemi=1 hin= hout= ispot=` (E light lines, L records); envcs writes
  `urp=x,y,z,spec,hemi,hin,hout,ispot;...` per scene into RrEnvironmentTable (regenerated, 67 of 69 scenes carry it; the
  rest of the table is byte-identical). 2,784 of 2,810 level lights have URPLight: 2,560 spec 0, 967 hemisphere.
  RrSceneData.LightRecord parses the keys (future conversions); the kit falls back to the table by position (0.3 m).
- Dorm rig (RrEnvironment.BuildRig) and the cached Rec Center (Places/RecCenter/RrRecCenterCache.UseLightGrid) get grids too.
- AO: RrPostFx.Prepare() from RrMain.Awake (ONE line in Core/RrMain.cs, not my file: the Volume must exist before the
  client applies its graphics settings, else its HBAO never lands in our profile). In rooms: the client's HBAO radius
  0.6 m, intensity max(1, the player's); restored on Leave/Shutdown; left alone when the player's intensity is 0. Found
  by type name + the parameters' fixed ranges (radius 0.25-5, intensity 0-4) through VolumeComponent.parameters.
- GT dressing: lantern glass (1, 0.56, 0.2) x 2.4 and torch flame boxes (1, 0.48, 0.1) x 2.8 (were a cream 1/0.72/0.35 x 1.4).
- Harness hooks: Core/RrEnvironmentLightTest.cs (RrLightTest: GridOff/On/Wait, Report, ProbeFoyer, Pair* before/after).

## Harness run 1 (light9_tour, 48/48 steps ok, 64 s)
- Grids built: GT Foyer 40 lights (8 hemisphere, 33 spec 0), 221 Rec Room renderers lit, 55 of 6,912 cells full (8);
  Armory 9, Hallway1 30, Hallway2 50 (326 full); Jumbotron Foyer 15; Paintball lobby 13, River 5; Rec Center cache, Dorm rig.
- Mean luma before -> after (post on): GT hall 22.7 -> 43.4, Armory 17.9 -> 44.5, act 1 torch 7.9 -> 39.1, act 1 11.6 -> 31.3,
  Paintball interior 76.8 -> 97.5, lobby 74 -> 95, Rec Center 88 -> 135, Dorm 117 -> 178; 0.00% clipped white pixels in every shot.
  LOOKED: GT lobby = a lit theatre (stage spots on the red curtains, brick walls, the room fill) instead of a fogged dark
  hall; act 1: the torches light the brick wall, the pots and the floor around them; the Armory's arrival spot makes its
  pool on the floor; Rec Center honey-toned and bright like Rec Room's pictures; the Dorm's white walls and carpet.
- Floor light probe in the GT lobby (Rec Room's formula): spawn 1.54/0.23/0.02 (7 lights in the cell), under a torch
  1.62-1.66, 3 m out 0.89, 5 m 0.44, the hologram 0.34.
- Draws: GT Foyer kit 168 separate renderers (iteration 8's shipped "sunless, many lights" rule) -> 30 merged.
- Problems: the spawn camera stood outside the lobby's end wall (x 119 > 118.9: black pair); Jumbotron foyer camera
  behind the hologram in fog (no change visible); Paintball lobby grid over the level's 670 m box = 28 m cells (14 lit);
  the 11 GT grids built in the same frame (45-70 ms together); lantern glow tonemapped to cream.
- Fixed for run 2: grid box = scene box cut to the lights' reach; one grid builds at a time, 2.5 ms slices, the first
  scan sliced (2 ms) too; periodic rescans only re-apply a block that was cleared; cameras moved; glow (1, 0.5, 0.14) x 1.7.

## Harness runs 2-6
- Run 2 (light9_tour): grid boxes cut to the lights' reach (Paintball lobby 28 m -> 1.6 x 3.2 x 1.6 m cells, 14 -> 2,804
  cells lit), the spawn pair from inside the lobby: GT lobby from the spawn luma 21.2 -> 45.0 (the user's view: both
  torches now light the brick pillars, the carpet and the floor; the stage glows red behind the hologram).
- Run 3 (light9_diag): Jumbotron foyer: every grid-shader renderer carries the block (48 maker-pen, 55 AG/Standard-Opaque,
  27 AG/Standard-Opaque-Emission); GPU instancing off changes nothing. Its hull is mostly emissive and its floors URP Lit
  (18 renderers) -> the grid shows little there (luma 35.0 -> 35.8); the grid's own floor probe gives 0.84/0.06/0.93.
- Run 4: Unity twins cast no shadows any more (Rec Room surfaces cannot take URP's additional-light shadows); same pictures.
- Run 5 (light9_perf): Camera.Render CPU (editor) with the per-property arrays: GT lobby 3.2 -> 13.2 ms, Dorm 2.1 -> 11.8,
  Rec Center 6.4 -> 10.8: Unity rebuilt Rec Room's 18 KB AdditionalLights cbuffer on every lit draw. Static renderers no
  light reaches are left batched (Rec Center 40, Dorm 51).
- Run 6 (light9_tour + perf): the block now binds ONE GraphicsBuffer as Rec Room's "AdditionalLights" cbuffer
  (MaterialPropertyBlock.SetConstantBuffer; layout identical in AG/Standard-*, MakerPenShape, Terrain, Avatar, Transparent,
  Studio Target/Lit: 480/640/800/960). Pictures identical to the array mode (luma equal to 0.1 in 5 compared shots).
  Camera.Render: GT lobby off 3.77 / on 6.52 ms (arrays 7.06), Dorm 2.24 / 4.46 (4.91), Rec Center 13.03 / 9.13 (10.38;
  noisy, the "off" pass ran first). So about +2.2 to +2.8 ms of editor CPU per view in the lit rooms: the lit
  renderers leave the SRP Batcher. To watch in the headset.
- Final lumas (post on, before -> after): GT lobby spawn 21.3 -> 44.9, GT hall 22.7 -> 43.4, Armory 17.9 -> 44.5, act 1 torch
  7.9 -> 39.1, act 1 11.6 -> 31.4, Jumbotron foyer 35.0 -> 35.8, Paintball lobby 74 -> 102, Rec Center 88 -> 139, Dorm 117 -> 178;
  0% clipped white. Pictures: DevNotes/iter9/screens/light9_*_{before,after}.png.
- Draws: GT kit scenes (11 areas) iteration 8 shipped ~597 kit renderers (separate when sunless) -> 182 merged (Foyer 168 -> 30,
  Library 564 -> 14, Hallway5 91 -> 28); only one area is shown at a time.

## Open / not done
- Additional-light shadows on Rec Room surfaces (URP's shadow slices follow URP's light order; not reachable): Rec Room
  shadowed 1-3 spots per quest area. The sun's shadows are unchanged.
- Rooms not built by the kit whose own Unity lights never reached Rec Room shaders: Charades, Dodgeball, Paddleball, the
  Rec Center rebuild fallback (only the Dorm rig and the cached Rec Center got grids). A room-level grid from a room's own
  lights is a small follow-up, but their lights were tuned by eye for URP's falloff.
- Jumbotron hull: emissive / URP Lit materials (the jumbotron agent's), so its rooms change little.
- Specular contribution is honoured on Rec Room surfaces only (URP has no per-light switch for the Unity twins).
- AO: works only when the player turns ambient occlusion on in Somnium's graphics settings (every preset has it off);
  not seen in the harness (no client HBAO there).
- The y = range^2 meaning of Rec Room's Attenuation.y is read from the shader (the vertex variant multiplies by the
  compact .w the same way); the fork's C# is encrypted, so it is the one inferred number.
