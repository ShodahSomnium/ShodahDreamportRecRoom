# Dreamport: Rec Room - play-mode harness and world scene

The harness runs the world's real code in Unity play mode, headless but with a graphics device, on a throwaway copy of
the project (`scratchpad\unityharness\RRHarness`). It never opens the user's project. `sync.sh` copies the code in, and
one PlayMode test boots RrMain against the player's Rec Room install. The test then carries out a JSON scenario (spawn,
travel, screenshots, calls, log checks) and writes `summary.txt` and PNGs. The same copy produced the world scene, using
the real project's own editor tool, and the scene was then copied into the real project.

SP = `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad`

## Where it stands (2026-09-24, ~02:35)

| Layer | State |
| --- | --- |
| Harness (sync, lock, PlayMode test, scenarios) | **Runs.** The first run took 97 s and imported Photon plus mono-cecil into the copy. Later runs take 15-18 s from start to finish. `smoke`, `world_scene`, `diag_passes` and `dorm` all pass. |
| Smoke (3 props on the Arrival floor, 2 shots) | **Runs.** All 9 steps are OK. The props render with Rec Room's own materials and look right (see below). |
| World scene `Assets/Rec Room Scene.unity` | **Built**, generated in batch mode by `RrSceneSetup`, and all 46 YAML checks pass. It also boots in play mode through the `world_scene` scenario. It has not yet been opened in the user's editor or uploaded. |
| Editor tooling (`SomniumRecRoom.Editor`) | **Built.** It compiles in Unity, which is proven because the scene was made with it, and in the offline editor build with 0 errors. |
| Core fixes | Two, both in Core (see "Core bugs found"). The compile check passes: server-like 0 errors and 0 warnings, all 59 Fusion/Somnium members proven, editor builds 0 errors. |

## Files

Harness (SP\harness):

| File | Role |
| --- | --- |
| `sync.sh` | Mirrors real to harness with robocopy /MIR, .meta files included: `Assets/#User/SomniumRecRoom`, `Assets/#User/Community Modules` and `Assets/Photon`. It also copies each folder's .meta, `Assets/#User.meta`, `Packages/manifest.json` and every top-level `Assets/*.unity` (+ .meta). It never touches the harness's own folders. |
| `lock.sh` | `take_lock`: `mkdir SP\harness\lock`. It waits up to 30 min for another holder and releases on exit or Ctrl-C. Sourced by the scripts. |
| `run.sh` | `run.sh <scenario>`: lock, sync, then Unity `-batchmode -runTests -testPlatform PlayMode -testFilter RrScenario` (without -nographics) with RR_SCENARIO/RR_OUT set. Prints the test result, summary.txt and the PNG paths. |
| `make_scene.sh` | Lock, sync, `-executeMethod ...RrSceneSetup.CreateWorldSceneBatch -quit`, then `check_scene.sh`. With `--install` it also copies the scene and .meta into the real project, but only if every check passed. |
| `check_scene.sh` | Read-only YAML checks of a saved world scene (objects, script GUIDs, 135 NetworkObjects, SortKeys, uploader, look, wiring). |
| `scenarios/smoke.json` | Boot, spawn (Basketball), (Consumables_RootBeer_PixelPop_Bottle) and (Dorm_Bunk_Bed) side by side, take 2 shots, dump. |
| `scenarios/world_scene.json` | Boots the saved world scene itself (`"scene"`), spawns 2 props and takes 2 shots. |
| `scenarios/dorm.json` | `goto Dorm` and shots at the slot-0 dorm origin. It is waiting for the Rooms module. |
| `scenarios/diag_passes.json` | Lists the passes and tags of Rec Room shaders (it found the RrMaterials bug). |
| `out/<scenario>/` | `summary.txt`, `*.png`, `dump_*.txt`, `results.xml`, `unity.log` (+ `unity.prev.log`). |

Harness project (SP\unityharness\RRHarness\Assets\RrHarnessTests, never synced over):

| File | Role |
| --- | --- |
| `RrHarnessTests.asmdef` | PlayMode test assembly. It references SomniumRecRoom, Unity.TextMeshPro, UnityEngine.UI, URP Runtime, UnityEngine.TestRunner, UnityEditor.TestRunner and nunit, and is limited to `UNITY_INCLUDE_TESTS`. |
| `RrHarnessTests.cs` | The one test `RrScenario` (timeout 1 h, `LogAssert.ignoreFailingMessages`). It fails only when boot fails. |
| `RrScenarioRun.cs` | Boot, step runner, shot renderer and summary writer. The step reference is in its header. |
| `RrJson.cs` | A small JSON reader with no package dependency. It accepts `//` comments. |
| `RrDiag.cs` | Diagnostics for `call` steps (`RrHarness.RrDiag.ShaderPasses`). |

Real project (new):

| File | GUID | Role |
| --- | --- | --- |
| `Assets/#User/SomniumRecRoom/Editor/` (folder) | 1b030a9d9a9545358de689d18eb25d01 | |
| `Editor/SomniumRecRoom.Editor.asmdef` | c324ca126c70414d8d3df118449ce9f2 | Editor only. It references SomniumRecRoom, Unity.TextMeshPro, UnityEngine.UI, SomniumSpace.Network, Fusion.Unity and Unity.RenderPipelines.Universal.Runtime. URP is the one addition to the ETS shape: it is needed for `UniversalAdditionalLightData`. |
| `Editor/RrSceneSetup.cs` | 903802783dd94488a1a4640114de0f87 | Menu **Somnium Rec Room/Create world scene**, plus the batch entry `CreateWorldSceneBatch` and the runtime-safe builders the harness reuses (`BuildEnvironment`). |
| `Editor/RrNetSceneSetup.cs` | 591525fcca55433bbeaa9ac5bd686fc1 | Menu **Somnium Rec Room/Add network objects** (it is also called by Create). |
| `Assets/Rec Room Scene.unity` | 5cda4cd6c723fd448be232356d4c9781 | The world scene. |

Real project (changed): `Core/RrMaterials.cs` and `Core/RrMain.cs` (see "Core bugs found").

## How to run

From Git Bash (any directory):

```bash
H=/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad/harness
bash $H/run.sh smoke                    # or a path: bash $H/run.sh /c/.../my.json ; add --no-sync to skip the copy
bash $H/run.sh world_scene              # boots Assets/Rec Room Scene.unity itself
bash $H/make_scene.sh                   # regenerate the world scene in the harness and check it
bash $H/make_scene.sh --install         # ... and copy it into the real project (only when all checks pass)
bash $H/check_scene.sh "/d/Dreamport/Rec Room/Rec Room/Assets/Rec Room Scene.unity"   # check the real one
bash $H/sync.sh                         # just the copy
```

Exit codes of run.sh are Unity's. 0 means the test passed, which means the world booted even if some steps failed.
2 means boot failed, 3 means a run error such as a compile error, and 124 means a timeout (`RR_TIMEOUT`, default
7200 s). Other agents should run it in the background and poll `out/<scenario>/unity.log`, because the lock serialises
runs. If a run dies without releasing the lock and no Unity is open on the harness, delete `SP\harness\lock`.

## Scenario steps

A scenario is `{"name": ..., "scene": "Assets/Rec Room Scene.unity" (optional), "boot_timeout": 240, "camera": [x,y,z],
"steps": [...]}` or a bare array of steps. The first action key of a step decides its kind:

| Step | What it does |
| --- | --- |
| `{"log": "text"}` | Adds a marker to the log and the summary. |
| `{"wait": s}` / `{"wait_frames": n}` | Waits in real time or in frames. |
| `{"spawn": "(Prefab)", "pos": [x,y,z], "yaw": 0, "physics": false, "ground": true, "scale": 1, "options": "prop\|scenery\|visual", "timeout": 180}` | `RrSpawn.Spawn` under `RrMain.WorldRoot`. `ground` puts the renderers' lowest point at pos.y. `physics: false` makes it kinematic. It records the time, bounds, renderers, colliders and shader/queue per material. |
| `{"goto": "Dorm", "instance": -1, "timeout": 120}` | Calls `Rooms.RrTravel.GoTo` by reflection, then waits until `RrTravel.Current` is the target and `Busy` is false. It is marked skip if GoTo does not exist. |
| `{"shot": "name", "pos": [..], "look": [..], "fov": 60, "w": 1280, "h": 720, "msaa": 4, "post": false, "bg": [r,g,b]}` | Renders its own camera to a RenderTexture, then `out/name.png`. It records the render time and mean/SD luma, and flags a blank image. |
| `{"call": "Namespace.Type.StaticMethod"}` | No arguments. If it returns an IEnumerator, that is run to the end. |
| `{"expect_log": "substring", "timeout": 0}` | Passes if any log line so far, or within the timeout, contains the text. |
| `{"dump": "name", "root": "GameObject"}` | Writes hierarchy, layers, components and materials to `out/dump_name.txt` (default root: RrMain.WorldRoot). |
| `{"clear": true}` | Destroys what the harness spawned. |

In create mode, without `"scene"`, the test makes the world's environment by calling
`RrSceneSetup.BuildEnvironment()` through reflection, so it gets the same sun, Arrival floor and render settings as
the scene. It then adds a TMP status label and the "Rec Room" object (RrMain, `_status` wired before Awake), plus a
"Harness Camera" at (0, 1.6, -3). That camera is tagged MainCamera, is the only AudioListener, and has volume 0. There is
no Somnium client, so Bridge getters are null. Shader compilation is synchronous (`ShaderUtil.allowAsyncCompilation =
false`), so shots never show the cyan placeholder.

`summary.txt` sections are: Result, Timings (RrMain.Ready and the first line of each `[SomniumRecRoom]` area, which
gives the boot phases, plus the longest frame), Steps (ms, frames, max frame, message), PNG, Systems
(`RrAssets.Describe`, `RrMaterials.Describe`), Errors and exceptions (grouped, with stacks), Other warnings (Rec Room's
"referenced script is missing" warnings are counted, not listed), and every `[SomniumRecRoom]` and `[RrHarness]` line
with its time.

## Smoke results (2026-09-24 02:25-02:34)

- **Timings.** On the first-ever run, RrMain.Ready came at +4.84 s: the catalog of 2,787 bundles and 24,564 entries was
  built in 4,170 ms and then cached. On every later run Ready comes at +0.75-0.81 s, with the catalog read from cache in
  69-75 ms. Most of the remaining time is the maker-pen material and the Futura/Jost fonts (~0.5 s). Spawn to callback:
  (Basketball) 161-177 ms, root beer 110-134 ms, (Dorm_Bunk_Bed) 42-64 ms. The first shot of a run takes 370-1,460 ms
  because shader variants compile synchronously in the editor. Later shots render in 2-3 ms. The test takes 2.6-2.8 s of
  play mode, and Unity takes 15-17 s from start to finish.
- **What the PNGs show** (`out/smoke/smoke_overview.png`, `smoke_props_close.png`): three props on the light-grey
  20 m Arrival floor under Unity's default sky, with soft shadows from the warm sun.
  - The bunk bed is Rec Room's: wood-grain frame, desk with drawers, white pillows and a purple mattress (the recolour
    mask works).
  - The basketball is orange with black seams.
  - The PixelPop root beer has its pixel-flame label, dark red base and orange cork.
  - Nothing is magenta, cyan or white-washed. Every prop sits on the floor (ground snap) and has the right size: ball
    0.25 m, bottle 0.35 m, bed 2.49 x 2.50 x 1.42 m.
- **world_scene** (`out/world_scene/*.png`): the saved scene boots in play mode (Ready +0.93 s). It shows the 20 m
  Arrival slab with the two props at the spawn, seen from the spawn eye height and from above. It logs no errors besides
  the two editor-only ones listed below. SceneNetworking and the 135 network objects sit idle offline.
- **dorm**: the `goto` path works (GoTo found and invoked). With today's skeleton RrTravel it reports "not arrived after
  5 s (RrTravel.Current = None)", as expected, and the test still passes.

## World scene contents (`Assets/Rec Room Scene.unity`, made by "Somnium Rec Room/Create world scene")

| Object | Contents |
| --- | --- |
| Uploader | WorldSDKConfig and WorldConfig, found by type name because they are not references of our asmdef. AssetName 'Dreamport: Rec Room', a description, DoCleanBuild 1, **WorldIdForUpload 0, DoReplaceExistingWorld 0** (the user picks the slot), OpenScriptingSettings 1, EnableScripting 1, ScriptingAssembly SomniumRecRoom, AsmdefReference = SomniumRecRoom.asmdef (fileID 5897886265953266890, guid dfdce4c5...). Config points to the WorldConfig, which has SpawnPointPos (0, 0.9, 0), SpawnPointDirection 0, RespawnHeightY -100 and RespawningBehavior 0. |
| Directional Light | Warm white (1, 0.957, 0.894), intensity 1.3, Euler (50, -30, 0), soft shadows, strength 0.7, UniversalAdditionalLightData. It is RenderSettings.sun. |
| Arrival | Cube, 20 x 0.5 x 20 m, top face at y = 0, BoxCollider, layer 23, URP default Lit material. |
| Status Canvas / Status | Overlay canvas with sorting 100 and a 1920x1080 scaler. TMP label 20 pt, white, top-left, 32 px margin, LiberationSans SDF (guid 8f586378...). Initial text "Dreamport: Rec Room / Starting...". |
| Rec Room | RrMain (script guid 3c565370..., the real project's), with `_status` set to the label. |
| Rr Net | Children: Scene Networking (SceneNetworking guid dc33e9ee..., `_autoTransferObjectsAuthority 0`, `_editorConnectPhotonNetwork 0`), Rr Net Events (NetworkBridgeEvents), Rr Net Player 0..49, Rr Net Instance 0..23 and Rr Net Actors 0..11 (NetworkBridgeData64), and Rr Net Prop 0..47 (NetworkBridgeData12). That is 135 NetworkObjects, each with Flags 524289 and ObjectInterest 1. All have non-zero, distinct SortKeys and filled NetworkedBehaviours, baked by Fusion. |
| RenderSettings | **Fog off.** Trilight ambient: sky (0.78, 0.86, 1), equator (0.80, 0.80, 0.78), ground (0.52, 0.48, 0.44), intensity 1. Unity's default procedural skybox. |

**Fog choice (conventions §13).** The scene is saved with `m_Fog: 0`. Graphics fog stripping is Automatic, so the fog
variants of any shader that ships inside the world bundle are stripped, and runtime `RenderSettings.fog = true` would
not draw on them. Rec Room's own shaders come from the player's bundles with Rec Room's variants and are not affected.
If a room wants fog later, for example a Midnight or quest atmosphere, the scene must be re-saved with fog on in the
mode that will be used.

Fusion bakes on scene save through a hook registered in the static constructor of `NetworkObjectPostprocessor`, which a
fresh batch-mode editor may never have run. `RrNetSceneSetup` therefore also calls
`Fusion.Editor.NetworkObjectPostprocessor.BakeScene(scene)` by reflection before saving. The check shows 135/135
SortKeys. Both menu items are idempotent. Existing objects are kept, the uploader's name and slot are never reset, the
scripting fields are corrected if they drift, and render settings are re-applied. The scene is saved only when the
network part changed.

## Core bugs found

1. **`RrMaterials.HasDepthNormals` looked at the wrong subshader, so every opaque Rec Room material went to the
   transparent queue.**
   - **What the harness showed.** Every `AG/Standard-Opaque` material came out at queue 2501 ("queue fixes 10" of 14).
     The research says that shader has a DepthNormals pass.
   - **Why.** `diag_passes` found that `Shader.passCount` and `FindPassTagValue(pass, ...)` describe the *active*
     subshader. Unity picks the active subshader using `Shader.globalRenderPipeline`, which URP sets only when it first
     renders. That value was `''` at frame 1554 of batch play mode, so the active subshader was Rec Room's untagged
     fallback: one unnamed pass. Subshader 0 is tagged `UniversalPipeline` and has UniversalForward, SHADOWCASTER,
     DepthOnly and DepthNormals.
   - **Changed.** It now walks the subshaders with `FindSubshaderTagValue(ss, "RenderPipeline")` and uses the first one
     tagged for URP (or untagged), with `GetPassCountInSubshader` and `FindPassTagValue(ss, p, "LightMode")`.
   - **Result.** AG/Standard-Opaque stays at queue 2000. AG/Standard-Opaque-Instanced (the bunk bed, no DepthNormals)
     and AG/Unlit-Additive-Soft still move to 2501, as intended.
   - In the client URP is always rendering, so the old code was probably right there. The new code does not depend on
     timing.
2. **`RrMaterials.Shutdown()` was never called.**
   - **The problem.** RrMain.OnDestroy shut down Look, Text, Sound and Music but not Materials. Its static `Done` and
     `Replaced` sets and the replacement materials outlived a world visit, which breaks hard rule 5.
   - **Changed.** One line in `RrMain.OnDestroy`: `RrMaterials.Shutdown();` after `RrLook.Shutdown()`.
   - **Result.** The harness teardown runs it with no errors.

Nothing else in Core/ or Rr/ was changed. RrAssets, RrSpawn, RrText, RrLook and the catalog all behaved as documented.

## Open issues

1. **Missing-script warnings.** Loading or spawning a Rec Room prefab logs about 190-290 "The referenced script ... is
   missing!" warnings (573 for the three smoke props). In the client they go to Player.log, and they are the likely
   cause of spawn hitches. They cannot be removed in a player, because
   `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` is editor only. This needs measuring in the client.
2. **Frame budget.** It cannot be judged here. Batch play mode shows ~20-27 ms frame spikes even during a plain `wait`,
   so "max frame" in the summary is editor noise, not a VR measurement. Profile spawns in a player.
3. **Rigidbodies on furniture.** Rec Room props, the bunk bed included, carry a non-kinematic Rigidbody and all land on
   layer 10 with `RrSpawnOptions.Prop()`. Room builders must spawn furniture with `RrSpawnOptions.Scenery()`. The harness
   defaults to kinematic.
4. **Uploader fields left at component defaults.** Framework (SDK/Unity/package versions), blockchainType (0),
   IsCanMint (0) and Subcategory (0) are unset. ETS has 3.1.8 / 6000.3.5f2, 3, 1 and 1, and the SDK fills Framework
   when it builds. Check the uploader panel before the first upload, and pick the world slot there.
5. **RenderSettings in the client.** The client may make another scene active, so the scene's ambient and sun may not
   apply. The Rooms/Core owners should re-apply them through `SomniumBridge.Environment`
   (`SetAmbientModeGradient`, `SetSunSource`) as ETS does on `activeSceneChanged`.
6. **Arrival floor.** It is a plain light-grey URP Lit slab. The Rooms module's arrival room should dress it in the Rec
   Room look.
7. **Editor-only noise.** Two errors appear per run at the first render ("Host type is not matching any asset type at
   Path ...AutodeskInteractive.shadergraph" and "...TraceVirtualOffset.urtshader"). They come from the URP package in
   the harness copy, not from our code.
8. **Harness freshness.** The harness compiles whatever other agents have half-written. If a file is mid-edit, run.sh
   exits 3 and prints the `error CS` lines. Rerun when their compile check passes.
9. **The user's editor.** The scene and the new Editor folder import on focus in the user's editor. If
   `Rec Room Scene.unity` is already open there, reload it. To regenerate the scene from the editor, use
   **Somnium Rec Room/Create world scene**. Running it again changes nothing that is already complete.
