# iter6 - load Rec Room's own cached Rec Center at runtime (durability notes)

Started 2026-09-24. Task: find the player's HTTPCache Rec Center scene bundle (1AF + 44 deps), load it additively,
sanitise, align into the room frame, hide our hand-built shell; fall back to the rebuild when missing.

## Status
- [x] step 1 feasibility: YES (run 1, reccenter_cache_probe)
- [x] step 2 runtime loader RrRecCenterCache.cs + RrRecCenterFit.cs, integrated in RrRecCenterRoom (runs 2-4, reccenter_cache 64/64)
- [x] rebuild regression: reccenter_tour now forces the fallback, 41/41 (run 5)

## Log

### 20:50 - offline findings (before any Unity run)
- Scanner `Rr/RrHttpCache.cs` (new, pure) + `RrUnityFs.ReadNodeRange` (additive). Offline test (dotnet console in
  session scratchpad `cscan/`): finds 1AF = `Assets/s/Assets/Rooms/RecCenter/RecCenter-RecCenter_071825.unity`,
  44 deps (183..1AE), 75.9 MB total, 1 missing CAB `cab-634aa40a8f2cc92082b491f41a54aec8`. First scan 849 ms
  (1,011 files, 344 UnityFS, 3 readable scene bundles; 35C is one LZMA block and is skipped), cached re-scan 93 ms.
- **The missing CAB is the Rec Room Studio shader bundle**: 116 of the 123 materials point into it. Its pathIds
  match the install's stock shaders: -4055751637662985151 = Universal Render Pipeline/Lit (92 mats),
  -261421426886131751 = URP/Unlit (3), -3885793140065008626 = URP/Terrain/Lit (1), 683148379888368288 =
  TextMeshPro/Distance Field (8), 7030032868776217633 = TMP/Mobile/Distance Field (1); 8918850207803687531 (9 mats,
  URP particle keywords: _SOFTPARTICLES_ON/_FADING_ON/_COLORADDSUBDIFF_ON) and -6364362845305036051 (4 tree mats,
  _TreeInstanceColor/_Cutoff) are not in the install. Rec Room's runtime swapped them ("ShaderPropertyTranslator ...
  replacing its shader regardless" in Player-prev.log). So every material must get a shader at load.
- **524 of 2,571 renderers are static-batched** ("Combined Mesh (root: scene) N", vertices in scene world space,
  m_IsReadable false). Moving their transforms does not move them. Fix: Renderer.staticBatchRootTransform (internal,
  present in the client's stripped UnityEngine.CoreModule) set by reflection to the frame container.
- Scene content: no cameras, no AudioListeners, no EventSystems, no Canvases (TMP door signs use CanvasRenderer only),
  1 URP Volume (`VisualPostProcessing/Post Processing Volume`, live because URP's scripts exist here), 2 reflection
  probes (inactive), 42 lights (1 directional KeyLight with soft shadows), 2 AudioSources (the real ambience clips),
  Terrain + TerrainCollider, 6 light-shaft SkinnedMeshRenderers with Animators, LightmapSettings with 0 lightmaps
  (all realtime), OcclusionCullingData present (class 363). Layers used: 0 (2283), 11 (234), 14 (24), 10, 25, 4.
- **Rec Room's own post profile for the final Rec Center** (`RecCenter-RecCenter_071825-PostProcessingProfile`,
  read offline from 1AF sharedAssets; for the post team):
  Bloom threshold 1.1, intensity 0.25, scatter 0.617, high-quality filtering off (clamp/tint/dirt not overridden);
  Tonemapping mode 1 (Neutral); WhiteBalance temperature +5 (tint not overridden); ColorAdjustments postExposure
  0.2, contrast 5, saturation 8 (colorFilter/hue not overridden); ChannelMixer nothing overridden; LiftGammaGain
  lift (0.890, 0.950, 1.000, 0), gamma (1.000, 0.925, 0.868, 0), gain (1.000, 0.989, 0.940, 0.034).

### 21:10 - STEP 1 FEASIBILITY: YES (harness run 1, reccenter_cache_probe)
- Harness editor 6000.3.5f2 loads all 45 cache bundles (0.12 s) and the scene additively (0.40 s); first scan 1.8 s on
  the worker (cold), total 2.7 s. The client (6000.3.21) runs the same loader API; expected to work the same.
- As loaded: 75 renderer materials, 73 on Hidden/InternalErrorShader, 1 Standard, 1 legacy particle. After mapping:
  64 Lit -> "Rec Room Studio Target/Lit" (install bundle 43ea1176...), 4 particles -> URP Particles/Unlit, 4 trees ->
  Target/Lit alpha-clipped, 1 TMP -> TMP/Mobile/Distance Field, 1 Unlit, 1 Terrain/Lit. 0 unsupported afterwards.
- **Target/Lit shows the vertex colours** (orange REC ROOM logo, blue kiosk back and wall bands, orange gym): the
  install's URP Lit and the client's URP Lit render the walls and the logo WHITE (shots cc_entrance_install/_client).
  So Target/Lit is the default. RrMaterials then moves those materials to queue 2501 (no DepthNormals pass).
- Static-batch re-rooting works: combined meshes and loose objects line up (floor raycasts: hall 0.00, kiosk 0.25,
  theatre -0.78, gym 0.25, balcony collider top 3.32 - the real balcony floor is ~3.3, not 3.10).
- 27 roots, 839 renderers (524 static-batched, 7 skinned, 238 LOD groups), 42 lights (1 directional KeyLight, off),
  1,177 colliders (29 mesh, 10 triggers -> off), before: layer 0 x1023, 11 x116, 14 x24, 25 x10, 10 x4; now all 23.
  0 cameras, 0 listeners, 0 canvases, 1 Volume (destroyed after logging), 2 audio sources, 2 reflection probes
  (inactive), 1 terrain, 6 animators. Lightmaps 0 -> 0 (the room is realtime-lit).
- The key light (on, no shadows) makes almost no visible difference: kept off.
- The cached scene's RenderSettings (not applied; additive scenes' are ignored): fog ON linear 5..400 m colour
  (1, 0.898, 0.639); ambient Trilight sky (0.486, 0.651, 0.863) equator (0.639, 0.557, 0.388) ground (0.510,
  0.286, 0.173) intensity 1; custom reflection cubemap; skybox SkyboxLockerRoom_Baked. For the post team.
- Duplicates seen (our props over theirs): cafe chairs/stools/pendants, couches, lockers, punching bag, vending machine
  in the gym, the Friend-O-Tron over their static vending machine by the curved wall, board. Hoops poke through the
  real gym walls; the mirror stands 0.5 m off the real hallway wall -> step 2 nudges them onto the real walls.

### 21:40 - STEP 2 loader integrated (harness run 2, reccenter_cache: 62/62 steps ok)
- RrRecCenterRoom.Build: RrRecCenterCache.Load first; with the cache: no shell/outdoors/fx, 54 of 107 props left out
  (furniture copies), doors and props fitted onto the real colliders (RrRecCenterFit), our vending machine and
  Friend-O-Tron take the places of the building's two [VendingMachine_Static] (hidden), the building's feed board
  hidden under ours; ambience = the building's own 2 AudioSources. Without it: the rebuild, unchanged.
- Leave -> "cached Rec Center released: scene unloaded, 45 bundles unloaded in 0.09 s"; census at Dorm / RecCenter
  visits 1-3 "1 room, nothing outside it"; forced fallback visit builds the rebuild in 0.7 s; re-entry reloads the cache.
- Found in the shots and fixed for run 3: our door signs doubled under theirs (door looks load async -> hide after
  props, wait for Built); MakerWorld door vanished INTO the wall (collider 0.3 m behind the painted wall -> doors never
  behind their real sign's plane); Magic Door stood in the hall->cafe opening (-> entrance wall right of the hallway,
  (2.6, 0, -15.44), GUESS); ping-pong paddles/balls and stacked plates fell through our props (-> loose props fitted
  last, onto the building or our props, in layout order); mirror 0.85 m and hoop 2 0.66 m off the real walls (-> 1 m
  tolerance; hoop 2 moved to z 22 clear of the building's punching bag); cakes into the building's pastry cases
  (-> x -16.8 / -15.3).

### 22:20 - DONE (harness runs: 1 probe, 3 x reccenter_cache, 1 x reccenter_tour; 5 of 6 used)
- Run 4 (final): 64/64 steps. Visit 1 cached (load 2.6 s: scan 90 ms cached, bundles 0.0 s, scene 0.4 s, room built
  in 4.0 s), visit 2 forced fallback (rebuild 0.7 s), visit 3 re-entry cached (0.5 s). Census "1 room, nothing outside
  it" on every visit; release "45 bundles unloaded in 0.10 s". Both hoops score, bell, radios, vending work.
- Fit (identical on both cached visits): doors SPORTS & PVP (0, 0.25 floor, -0.31 onto the gym back wall), QUESTS
  -0.11, MAKERWORLD +0.22 by its sign (+0.22 floor), FEATURED ROOMS +0.20, CAMPUS JAMS +0.22 by its sign, Dorm -0.04
  by its sign; Magic Door moved to the entrance wall (2.6, 0, -15.44); bell and kiosk radio +0.14 (counter top 1.19),
  cafe radio and cafe props -0.16 (counter 0.77), gym props/vending +0.25, hoop 1 +0.46 and hoop 2 -0.66 onto the
  real gym walls (hoop 2 at z 22), mirror -0.85 onto the real hallway wall; 6 of our door signs hidden under theirs.
- Best shots copied to DevNotes/iter6/cachecenter_shots/ (arrival, kiosk, cafe, windows, gym_hoop, door_quests,
  fallback_entrance + exterior, doors, hallway, re-entry).

### What differs from the user's video (final state, 9 h before the shutdown) - the cache holds only 071825
- Same as the video: timber beams and 2 skylights, the kiosk with the orange REC ROOM logo, the balcony with the
  white-and-wood front and back stairs, the navy carpets upstairs, the teal cafe with pastry cases and round teal
  tables and a curved window onto the garden, the theatre lounge with red curtains, brick and the QUESTS door with the
  quest posters, the gym, the outdoor garden with trees behind a brick fence.
- Not in the July 2025 scene: the curved upper balcony ring on the right with lights under it, the raised curved
  platform at the curved window wall (July: a flat floor and a lounge), the lounge with the big blue-and-orange mural
  and FEATURED ROOMS double wooden doors (July: a "Featured Rooms" sign over a single door spot upstairs in the wing,
  no mural), the orange-walled upstairs corridor with a "BACKPACK TOYS" sign, the dartboard panel by the stairs. They
  were most likely added in the August 2025 version (the research names "Home_082525"), which this cache does not have:
  the only Rec Center scene bundle cached is RecCenter_071825 (downloaded 26 Jul 2025); the other scene bundles are
  MakerWorld-Home, CalmingView-Home and 35C (MakeItToMidnight, LZMA).
- The doors, hoop(s), ping-pong, Friend-O-Tron, radios and mirror were room data in Rec Room: ours stand in.

### Untested / risks
- The Somnium client itself (6000.3.21 Mono): the 2021.3 bundle load, the reflection on Renderer.staticBatchRootTransform
  (the member is in the client's stripped UnityEngine.CoreModule), Rec Room Studio Target/Lit under the client's
  depth priming (RrMaterials puts it at queue 2501) and in single-pass-instanced VR.
- Walking in VR/desktop on the adopted colliders (harness: raycasts only); frame hitch of the scene activation (behind
  the loading card) and ~5,800 "referenced script is missing" warnings per load.
- Mixed instances: a player with the cache and one without see our shared toys at the same world spots, but the
  floors differ by up to 0.25 m (gym) between the two buildings.
- Other PCs: LZMA-compressed cache bundles are skipped (fallback); a different Rec Center version in a cache would use
  the 071825 frame transform (the fit logs the offsets).
- The building's Doorframe and Sign materials (Campus Jams) look silvery: specular white + smoothness 1 in the
  Studio material; left as loaded.
