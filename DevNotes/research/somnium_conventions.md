# Dreamport conventions and tooling guide (for "Dreamport: Rec Room")

Research only, 2026-09-23. Everything below was read from the three sibling projects, the Somnium client source,
the SDK DLLs, the user's Claude memory files and the new Rec Room project (read-only). Items marked **GUESS** or
**UNVERIFIED** are not measured. Line numbers refer to the files as they were on 2026-09-23.

Sources read (so later agents know what is already covered):

| Source | Path |
| --- | --- |
| Quake III world | `D:\Somnium Quake\Assets\#User\SomniumQuake\` (no README exists) |
| Age of Mythology world | `D:\Dreamport\Age Of Mythology\Assets\#User\SOMAOM\` (no README exists), `Scripting\logs.txt` |
| Euro Truck Sim world | `D:\Dreamport\Euro Truck Simulator\Euro Truck Sim\Assets\#User\SomniumEuroTruck\` incl. `README.md` (2,561 lines), `Scripting\logs.txt` |
| User memory (AoM) | `C:\Users\derii\.claude\projects\D--Dreamport-Age-Of-Mythology\memory\*.md` |
| User memory (ETS) | `C:\Users\derii\.claude\projects\D--Dreamport-Euro-Truck-Simulator-Euro-Truck-Sim\memory\*.md` |
| ETS offline tooling | `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Euro-Truck-Simulator-Euro-Truck-Sim\5f8816c6-...\scratchpad\` (`build\*.rsp`, `fusionapi\`, `look\trees\shader\dxc.sh`) |
| Somnium client | `E:\Somnium Space\Main Build` (Unity 6000.3.21f1, Mono) |
| New project | `D:\Dreamport\Rec Room\Rec Room` (Unity 6000.3.5f2) |

Note on "the three READMEs": only the ETS world has a README. Quake's and AoM's rules live in code comments, in
`Scripting\logs.txt` and in the memory files. All of them are merged into section 24 (Gotchas).

---

## 0. TL;DR - what a new Dreamport world must do

1. Code in `Assets/#User/SomniumRecRoom/`. There must be one runtime asmdef, `SomniumRecRoom`, with
   `rootNamespace` `SomniumSpace.Worlds.SomniumRecRoom`. Editor code goes in `Editor/` with its own
   `SomniumRecRoom.Editor` asmdef (Editor platform only).
2. The runtime asmdef may reference only the server whitelist: `Unity.TextMeshPro`, `UnityEngine.UI`,
   `Unity.XR.Interaction.Toolkit`, `Unity.RenderPipelines.Universal.Runtime`, `Unity.RenderPipelines.Core.Runtime`,
   `SomniumSpace.Network`, and `Fusion.Unity`. The Bridge, Fusion DLLs, Newtonsoft, Photon3Unity3D and UnityEngine
   modules are auto-referenced plugins. Never reference `CommunityModules`, `Unity.InputSystem` or
   `Unity.XR.CoreUtils` (section 5).
3. Write C# 9. Never call Fusion members that no earlier world used (the server's Fusion is older), and run the
   guard (section 21).
4. The scene holds an `Uploader` object (`WorldSDKConfig` + `WorldConfig`) with `EnableScripting: 1`,
   `ScriptingAssembly: SomniumRecRoom` and `AsmdefReference` set to the asmdef. **Choose the `WorldIdForUpload` with
   the user**, because siblings overwrite worlds 12308, 12410 and 12510. The scene also needs a spawn platform, a
   status label (TMP, LiberationSans SDF), a light, and the network objects.
5. Nothing from the game ships. The game install is found at runtime (`RrInstall.Locate`). Custom shaders reach the
   world only through a scene-referenced field. Stock URP shaders can be `Shader.Find`-ed.
6. Every opaque custom shader needs `UniversalForward`, `ShadowCaster`, `DepthOnly` and `DepthNormals` passes and
   single-pass-instanced stereo macros. Without them it is invisible in the client, or in the right eye.
7. Log every line with the prefix `[SomniumRecRoom] <Area>: ...`. Keep a status label and a 10 s heartbeat, and
   print the values that matter at runtime.
8. The user uploads and tests in the client. End every code-changing turn with **READY TO UPLOAD** or **NOT READY
   TO UPLOAD YET**. If a build lacks requested features, say so in the first line.

---

## 1. The sibling worlds at a glance

| | Quake III | Age of Mythology | Euro Truck Sim 2 |
| --- | --- | --- | --- |
| Project | `D:\Somnium Quake` | `D:\Dreamport\Age Of Mythology` | `D:\Dreamport\Euro Truck Simulator\Euro Truck Sim` |
| Code folder | `Assets/#User/SomniumQuake/` | `Assets/#User/SOMAOM/` | `Assets/#User/SomniumEuroTruck/` |
| Runtime asmdef (guid) | `SomniumQuake` (a2814cd73dbbfdf419bf5f85beb6c596) | `SOMAOM` (767caf498324d6d4bb189ae479ab88f0) | `SomniumEuroTruck` (9c83a30e9f57441db3ef46ca578eb7bc) |
| Editor asmdef | none (editor code in runtime asmdef under `#if`) | `SOMAOM.Editor` (3524287537df0a24dbe7e637e6baabd9), `SOMAOM.Tests` | `SomniumEuroTruck.Editor` (567eed989a404c2fa6f0eabc49328d14) |
| Root namespace | `SomniumSpace.Worlds.SomniumQuake` | `SomniumSpace.Worlds.SomniumMythology` | `SomniumSpace.Worlds.SomniumEuroTruck` |
| Format sub-namespace/folder | `.Q3` / `Q3/` | `.Aom` / `Aom/` | `.Ets` / `Ets/` |
| Class prefix | `Q3` | `Aom` | `Ets` |
| Log prefix | `[SomniumQuake]` | `[SomniumMythology]` | `[SomniumEuroTruck]` |
| Scene | `Assets/SomniumSpaceTemplates/WorldTemplate/Somnium_WorldLounge.unity` (the SDK template, edited) | `Assets/Main Scene.unity` | `Assets/Euro Truck Scene.unity` |
| Uploader AssetName / world id | "Transit Map" / 12308 | "Trains Are Cool" / 12410 | "Bigger transit" / 12510 |
| Spawn (WorldConfig) | (0, 12.26, -35.52), respawn Y -500 | (0, 0.897, 0), respawn Y -100 | (0, 0.9, -9), respawn Y -1000 |
| Install finder | `Q3/Q3Install.cs` (469 lines) | `Aom/AomInstall.cs` (275) | `Ets/EtsInstall.cs` (366) |
| Bridge DLL md5 | e729e27b... (old SDK) | e729e27b... (old SDK) | 29e3b224... (**same as Rec Room project**) |
| Fusion | 2.0.12 Stable 1861 | 2.0.12 | 2.0.12 (copied from AoM with GUIDs) |

All three follow the same idea. The game's own files are read from the player's install at runtime and rebuilt in
Unity, and nothing from the game ships. Quake, AoM and ETS also carry an optional "embedded game" side tool
(`*EmbeddedGame`, `*LaunchSpike`, `*WindowCapture`) that launches the real exe. The user rejected that approach
("it needs to be INSIDE Somnium"), so do not propose it for Rec Room.

---

## 2. Folder layout

The ETS layout is the most mature, so copy it:

```
Assets/
  Euro Truck Scene.unity                     <- the world scene, in Assets root (AoM: Main Scene.unity)
  #User/
    Community Modules/Plugins/SomniumSpace/Networking/   <- SomniumSpace.Network asmdef + NetworkBridge*.cs (copied WITH GUIDs)
    SomniumEuroTruck/
      SomniumEuroTruck.asmdef
      README.md                              <- the living design/status document
      Ets/        *.cs                       <- pure format readers/builders, namespace ...SomniumEuroTruck.Ets
      Ets/GDeflate/                          <- vendored third-party code (MIT, LICENSE.md kept)
      Editor/     SomniumEuroTruck.Editor.asmdef, EtsSceneSetup.cs, EtsNetSceneSetup.cs, EtsShaderReferences.cs, ...
      Shaders/    EtsDecal.shader, EtsMulti.shader, EtsLeaves.shader, EtsSky.shader, EtsHeightToNormal.shader
      *.cs                                   <- MonoBehaviours / world logic, namespace ...SomniumEuroTruck
      SceneNetworking.cs                     <- the world's own copy (own namespace; see section 19)
  Photon/Fusion, Photon/PhotonLibs           <- copied from AoM WITH GUIDs (networking)
  Plugins/SomniumSpace/...                   <- SDK (never edit)
```

AoM differs in a few ways. Shaders sit next to the code (`Aom/AomGround.shader`, `AomBlendLit.shader`,
`AomTinted.shader`). Their carrier materials are in `SOMAOM/Resources/*.mat`, which are referenced from scene
fields (the folder name does not matter, see section 13). Networking copies are in
`SOMAOM/Networking/{SceneNetworking.cs, Utils/BytesReader.cs, BytesWriter.cs, NetValue.cs}`. Tests are in
`SOMAOM/Tests/Editor/`.

**Recommended for Rec Room** (matches what already started in the project):

```
Assets/Rec Room Scene.unity                 (GUESS at the name; siblings put the scene in Assets root)
Assets/#User/SomniumRecRoom/
  SomniumRecRoom.asmdef                     (MISSING today - see section 20)
  README.md
  Rr/        RrInstall, RrUnityFs, RrSerializedFile, RrLz4, RrReader, RrCatalog, RrConfig, RrPrefabData, RrReach, RrWorker, RrCommonStrings (exist, namespace ...SomniumRecRoom.Rr)
  Editor/    SomniumRecRoom.Editor.asmdef, RrSceneSetup.cs, RrNetSceneSetup.cs, RrShaderReferences.cs
  Shaders/   Rr*.shader
  *.cs       world MonoBehaviours (namespace SomniumSpace.Worlds.SomniumRecRoom)
```

---

## 3. Assembly definitions (exact)

### 3.1 ETS runtime (`SomniumEuroTruck.asmdef`), the template to copy

```json
{
    "name": "SomniumEuroTruck",
    "rootNamespace": "SomniumSpace.Worlds.SomniumEuroTruck",
    "references": [
        "Unity.TextMeshPro",
        "UnityEngine.UI",
        "Unity.XR.Interaction.Toolkit",
        "SomniumSpace.Network",
        "Fusion.Unity",
        "Unity.RenderPipelines.Universal.Runtime",
        "Unity.RenderPipelines.Core.Runtime"
    ],
    "includePlatforms": [], "excludePlatforms": [], "allowUnsafeCode": false, "overrideReferences": false,
    "precompiledReferences": [], "autoReferenced": true, "defineConstraints": [], "versionDefines": [],
    "noEngineReferences": false
}
```

AoM `SOMAOM.asmdef` has the same 7 references in another order. `allowUnsafeCode` is false in all three worlds.
Unknown whether the server allows `unsafe`, so avoid it.

### 3.2 ETS editor (`Editor/SomniumEuroTruck.Editor.asmdef`)

```json
{ "name": "SomniumEuroTruck.Editor", "rootNamespace": "SomniumSpace.Worlds.SomniumEuroTruck.EditorTools",
  "references": ["SomniumEuroTruck","Unity.TextMeshPro","UnityEngine.UI","SomniumSpace.Network","Fusion.Unity"],
  "includePlatforms": ["Editor"], "autoReferenced": true, ... }
```

### 3.3 AoM tests (`Tests/Editor/SOMAOM.Tests.asmdef`)

References `SOMAOM`, `UnityEngine.TestRunner` and `UnityEditor.TestRunner`. It sets `overrideReferences: true`,
`precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false`, `defineConstraints:
["UNITY_INCLUDE_TESTS"]` and targets the Editor only.

### 3.4 Quake (`SomniumQuake.asmdef`)

Quake references by GUID. The GUIDs resolve as follows:

| GUID | Assembly |
| --- | --- |
| 6055be8ebefd69e48b49212b09b47b2f | Unity.TextMeshPro |
| 2bafac87e7f4b9b418d9448d219b01ab | UnityEngine.UI |
| 58de1d31e4aad8848b2afdda4b94c61b | SomniumSpace.Network |
| 1baf1ffff1031e948b3b228ab3453251 | Fusion.Unity |
| ee448cb1442054024b3c982e54100a6a | Fusion.Addons.Physics |
| fe685ec1767f73d42b749ea8045bfe43 | Unity.XR.Interaction.Toolkit |
| 75469ad4d38634e559750d17036d5f7c | Unity.InputSystem |

Quake's code does use `UnityEngine.InputSystem` (`Q3Gamepad.cs:2`) and `Fusion.Addons.Physics`
(`Networking/NetworkGrabbable.cs`, `TeleportNetworkRigidbody3D.cs`). **UNVERIFIED** whether the upload server
resolves those two. No Quake server log exists. ETS and AoM avoid both, and the offline check in section 21 treats
InputSystem as not available. Do not use them.

### 3.5 Recommended `SomniumRecRoom.asmdef`

Copy 3.1 with `name` `SomniumRecRoom` and `rootNamespace` `SomniumSpace.Worlds.SomniumRecRoom`.
`SomniumSpace.Network` and `Fusion.Unity` exist only once Photon and the network scripts are imported (section 19).
Until then, a name reference to a missing asmdef only gives an editor warning **GUESS**. An asmdef `.meta` looks
like this:

```
fileFormatVersion: 2
guid: <32 hex>
AssemblyDefinitionImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

Script `.meta` files written by hand are just two lines (ETS does this for every new script, so the scene YAML can
name the GUID before Unity imports the file):

```
fileFormatVersion: 2
guid: d0a56e14f4d54a96b68f3d017052089e
```

---

## 4. (reserved: the numbering follows the TL;DR)

## 5. The upload server's compile (the fixed reference list)

When uploading with scripting on, the SDK sends the asmdef's sources. The server generates a "sanitized project"
(`~<worldId>\ServerBuildWork\<Asm>.csproj`) and compiles it against `~ServerReferences\...`. The SDK writes the
server's log to `<project>\Scripting\logs.txt`.

**Resolved.** AoM's log (2026-09-02 14:21 UTC) and ETS's log (2026-09-23 00:39 UTC) agree:
- ScriptAssemblies: Fusion.Common, Fusion.Log, Fusion.Realtime, Fusion.Runtime, Fusion.Sockets, Fusion.Unity,
  NanoSockets, Newtonsoft.Json, Photon3Unity3D, SomniumSpace.Bridge, SomniumSpace.Network,
  Unity.RenderPipelines.Core.Runtime, Unity.RenderPipelines.Universal.Runtime, Unity.TextMeshPro,
  Unity.XR.Interaction.Toolkit, UnityEngine.UI and websocket-sharp.
- The UnityEngine modules: `UnityEngine.dll` plus 80 modules, among them AssetBundleModule, AudioModule,
  AnimationModule, ParticleSystemModule, PhysicsModule, InputLegacyModule, XRModule, VRModule, UIModule,
  TextCoreFontEngine/TextEngine, UnityWebRequest (+Audio/AssetBundle/Texture/WWW), VehiclesModule,
  ImageConversionModule and SubsystemsModule. The full list is in `D:\Dreamport\Age Of Mythology\Scripting\logs.txt`.

**Skipped** (present in the local csproj but not on the server): AsyncIO, CommunityModules,
Meshia.MeshSimplification.Runtime, NetMQ, SomniumSpace.Bundles, SomniumSpace.Common, SomniumSpace.Helpers,
SomniumSpace.SDK, SomniumSpace.SDK.Tools and SomniumSpace.User.

**Not listed at all:** Unity.InputSystem, Unity.XR.CoreUtils and Fusion.Addons.Physics. The server log lists only
what a world's csproj referenced, and neither AoM nor ETS referenced these, so treat them as unavailable.

**Failures seen:**
| Date | World | Error | Cause | Fix |
| --- | --- | --- | --- | --- |
| 2026-09-02 | AoM (12410) | CS0246 `CommunityModules` could not be found | asmdef referenced CommunityModules, which the server skips | Copy `SceneNetworking.cs` + `Utils/*` into the world's own folder/namespace (same GUIDs), drop the reference |
| 2026-09-23 | ETS (12510) | CS1061 `NetworkRunner.GetMasterClient` does not exist | the server's Fusion is OLDER than local 2.0.12 | Use only `SceneNetworking.IsMasterClient` → `runner.IsSharedModeMasterClient`; run the API guard (section 21) |

**The client side of scripting.** The client downloads the compiled assembly as an encrypted zip and loads it with
`Assembly.Load(bytes)`. The code is in `E:\Somnium Space\Main Build\Assets\_SomniumSpace\Worlds\Scripts\Scripting\WorldScriptLoader.cs:210-230`,
and `ValidateAssembly` is only a `//TODO`. So P/Invoke, `System.Diagnostics.Process`, threads and `System.IO` all
work in uploaded worlds. The client is **Mono** and keeps the world assembly loaded across visits, so statics
survive a re-entry (section 16).

---

## 6. Namespaces, naming, code style

- **Namespaces.** Use `SomniumSpace.Worlds.<WorldName>` for MonoBehaviours and world logic. Use
  `SomniumSpace.Worlds.<WorldName>.<Prefix>` for Unity-free format readers, builders and pure logic (`...Ets`,
  `...Aom`, `...Q3`). Use `...<WorldName>.EditorTools` for Editor code and `...<WorldName>.Tests` for tests. Rec Room
  already uses `SomniumSpace.Worlds.SomniumRecRoom.Rr`.
- **Class names** always carry the prefix (`EtsTruckController`, `AomUnit`, `Q3Player`, and for Rec Room `Rr...`).
  One public type per file, and the file name equals the type.
- **Scene object names** also carry the prefix. Examples: "Ets Map Viewer", "Ets Net", "Ets Net Player 7",
  "Aom Net Slots 12", "Aom Battleground". Runtime-created objects get readable names too ("Ets details", "Ets screen
  /ui/loading.sii", "ETS.Tablet.*").
- **Comments.** Every class has an XML `<summary>` that explains *why*: what was measured and which mistake it
  prevents. Comments cite evidence ("measured 2026-09-21 from the client source", "probe 1961", file:line into the
  client). Keep this habit, because later agents read these comments as specification.
- **C# 9.** Unity's default, and the language every sibling world passed the server with. No struct field
  initialisers, no parameterless struct constructors and no file-scoped namespaces. Vendored C# 10 code must be ported
  (ETS memory "ets2-161-port").
- **The Unity null test, never `??` or `?.` on UnityEngine.Object.** The editor returns fake-null objects from
  `GetComponent` that only `== null` catches. ETS comments repeat this rule (`EtsDetailDriver.cs:66`,
  `EtsNetSceneSetup.cs` "Unity's null test, not ??"). `?.` on Bridge interfaces (plain C# objects) is fine.
- **Tunables.** Tunables are `[SerializeField] private` with `[Tooltip]` (Q3Player is the model). Remember that the
  scene overrides code defaults (section 24).
- **Feature flags.** A static class (`EtsFeatures.NewFlow`) may exist, but the user must never discover a feature
  that is switched off (memory "no-hidden-features-uploads").

---

## 7. Logging conventions

- Every line starts with the world tag: `Debug.Log("[SomniumEuroTruck] <Area>: <message>")`. Areas in use include
  `Network:`, `Network: session:`, `Tablet:`, `Storage:`, `Master:`, `Profile:`, `Seat:`, `Traffic:`, `Details:`,
  `Terrain:`, `Look:`, `Fog:`, `Render settings:`, `Sky:`, `Stream:`, `Riders:` and `Flow:`. Use
  `[SomniumRecRoom]`. Counts in the siblings: ETS has 282 Log and 100 LogWarning lines, AoM 72 and 36, Quake 50
  and 27.
- Use warnings for "carrying on but something is missing". Use `LogError` almost never. SceneNetworking's own lines
  use `[SceneNetworking]` and `[World]`.
- **Log once per kind** for anything per-frame or per-item: a `HashSet<string>` capped at 200. See
  `EtsSemaphores.Warn(what, e)` and `Note(text)` at `Ets/EtsSemaphores.cs:104-121`, which print "... failed:
  <Type>: <msg> (logged once; carrying on)".
- **User-facing notes** use `EtsGameUi.Note(what)`, which prints `[SomniumEuroTruck][You] ...` (`EtsGameUi.cs:43`).
- **Heartbeat.** Every 10 s, one line saying which screen is up, where the player/vehicle is, and what state the
  game is in (`EtsFrontEnd.Heartbeat`, `EtsFrontEnd.cs:212`). The console then reads as a diary of the run.
- **Print runtime values, not code defaults.** Scene-serialized fields override the code, so log the value
  actually used.
- **Diagnostics keys** in the editor or client: F9, F10 and F11 in ETS. F10 prints `EtsProbe.UnderCursor()`, which
  names the object, mesh, material and textures under the mouse. Avoid keys the client binds (section 11).
- **Privacy.** Log only the first 4 characters of an account id (`EtsClientProbe`), because Player.log gets shared.
- **Where logs are:**
  - client log: `C:\Users\derii\AppData\LocalLow\Somnium Space Ltd\Somnium Space VR\Player.log`
  - editor log: `C:\Users\derii\AppData\Local\Unity\Editor\Editor.log`, which also shows shader variant stripping
    ("After built-in stripping: N")
  - server compile log: `<project>\Scripting\logs.txt`
- **Every README section ends with "Log lines to look for"** (section 22), so the user's next Player.log can be
  checked against it.

---

## 8. Scene setup

### 8.1 What the sibling scenes contain

| Object | ETS | AoM | Purpose |
| --- | --- | --- | --- |
| `Uploader` | yes | yes | `WorldSDKConfig` + `WorldConfig` (below) |
| `Directional Light` | yes (+ `UniversalAdditionalLightData`) | yes | sun; RenderSettings `m_Sun` points at it |
| `Spawn Platform` | yes: built-in Plane mesh (`{fileID: 10209, guid: 0000000000000000e000000000000000}`), scale (3,1,3) = 30 m, y -0.6, MeshCollider | yes | holds the player until the world streams in |
| `Status Canvas` / `Status` | yes | yes | ScreenSpaceOverlay canvas, sortingOrder 100, CanvasScaler 1920x1080, TMP 20 pt white, top-left, 32 px margin; **its LiberationSans SDF font (guid 8f586378b4e144a9851e7b34d9b748ee) is how the font ships** |
| main component | `Ets Map Viewer`, `Ets Model Viewer` | `Aom Battleground` | the world; has the serialized shader/material fields |
| `Global Volume` | yes (URP Volume) | - | post (ETS builds its own "Ets Post" volume at runtime) |
| network root | `Ets Net` (EtsNet) + `Scene Networking` + `Ets Net Session`, `Ets Net Events`, `Ets Net Traffic 0..3`, `Ets Net Player 0..49` | `Aom Net` + `Scene Networking`, `Aom Net Lobby`, `Aom Net Events`, `Aom Net Bank`, `Aom Net Slots 0..63` | section 19 |

There is no camera in the scene. In the editor, components make a fly camera only if **no camera at all** draws to
the screen (see "The stray camera", ETS README l.1112). In the client, the client owns the camera.

### 8.2 The Uploader (exact serialized fields)

GameObject `Uploader` has two MonoBehaviours.

**`WorldSDKConfig`**: script `{fileID: -553830836, guid: 5a46fca9cfe00e84b83457af8d644413}`, class
`SomniumSpace.SDK.dll::SomniumSpace.SDK.Configs.WorldSDKConfig`. Relevant fields, with ETS values:

```yaml
  Config: {fileID: 100003}            # the WorldConfig below
  DoCleanBuild: 1
  AssetName: Bigger transit
  AssetDescription: '...'
  PreviewTexture: {fileID: 2800000, guid: ..., type: 3}
  WorldIdForUpload: 12510             # !!! pick with the user; siblings: 12308 Quake, 12410 AoM, 12510 ETS
  DoReplaceExistingWorld: 1           # replace that world
  OpenScriptingSettings: 1
  EnableScripting: 1
  AssemblyIndex: 0
  ScriptingAssembly: SomniumEuroTruck # asmdef name
  AssemblyAvailabilityCorrect: 1
  AsmdefReference: {fileID: 5897886265953266890, guid: 9c83a30e9f57441db3ef46ca578eb7bc, type: 3}  # the asmdef asset (fileID constant for asmdefs)
  CheckedScriptingAssembly: SomniumEuroTruck
  UploadedWorlds: [...]               # filled by the SDK from the account; do not hand-edit
```

Every public field of `WorldSDKConfig` (dumped from `SomniumSpace.SDK.dll`): IsOpen360Preview, IsEditSpawnPos,
ShowRespawnHeight, ValidationData, InfoIsOpen, Preview360PicturePath, Preview360Texture, Preview360Location,
blockchainType, WorldSizeType, WorldSizeMB, WorldIdForUpload, DoReplaceExistingWorld, OpenScriptingSettings,
EnableScripting, AssemblyIndex, ScriptingAssembly, AssemblyAvailabilityCorrect, AssemblyVisualise, AsmdefReference,
SelectedIndexForSize, WorldLimitations, OwnedTokens, OwnedWorldSizes, SelectedIndexForWorldToReplace, UploadedWorlds,
Preview360ImageData, CheckedScriptingAssembly, TipJarUsed, TIpJarServerError and TipJarUsageInfo. It also has
AssetName, AssetDescription, DoCleanBuild, PreviewTexture and LastBuildSizeBytes (these were read from the scene).

**`WorldConfig`**: script `{fileID: 252206447, guid: ca5d94d337ec5644a94dfc0f52bb6771}`, class
`SomniumSpace.Bundles.dll::SomniumSpace.Bundles.Configs.WorldConfig`:

```yaml
  IsCanMint: 1
  Framework: {SDKVersion: {Major: 3, Minor: 1, Patch: 8}, UnityVersion: 6000.3.5f2, PakagesInfo: [{Type: 0, Version: 17.3.0}, {Type: 1, Version: 1.16.1}]}
  SpawnPointPos: {x: 0, y: 0.9, z: -9}
  SpawnPointDirection: 0              # degrees yaw (GUESS: float yaw)
  RespawnHeightY: -1000
  RespawningBehavior: 0               # enum RespawningBehaviorType: 0 PlayerSpawnPosition, 1 LastPlayerPosition, 2 Disabled
  Subcategory: 1
```

The Rec Room project's SDK DLLs (SDK, Bundles, SDK.Editor, Bridge) are byte-identical to ETS's.

### 8.3 The template scenes in the Rec Room project

- `Assets/SomniumSpaceTemplates/WorldTemplate/Somnium_WorldLounge.unity`: Uploader at l.6862. AssetName "Somnium
  Space World Template", world id 0, scripting off, spawn (0, 12.26, -65.47), respawn -500. Quake started from this.
- `Assets/SomniumSpaceTemplates/SimpleWorldTemplate/SimpleWorldScene.unity`: AssetName "DemoWorld", scripting off,
  `ScriptingAssembly: SomniumSpace.WorldInteractions.Examples`, spawn (0, 5, 0), respawn -100.

### 8.4 How scenes were made

- **The ETS scene is hand-written YAML** with round fileIDs. Uploader objects use `100000`-`100003`, the light
  `200000`, the platform `300000`, and the viewer `400000`. The status canvas is `500000`/`600000` and the map viewer
  `700000`. Network objects use `2900000001` onwards. Script GUIDs are chosen in hand-written `.meta` files first.
  This works while Unity is closed, or with a reload when it is open.
  **In this task the user's editor is open, so don't touch the project; an implementer writing YAML later must tell
  the user to reload the scene.**
- **Editor menu tools** create objects idempotently and wire fields through `SerializedObject`. Menu roots are
  "Somnium Euro Truck/..." and "Somnium Mythology/...". Examples:
  - `EtsSceneSetup.cs`: "Create capability probe", "Create model viewer", "Create map viewer", "Create embedded
    game". `CreateHost(name, out TMP_Text label)` builds the host plus a status canvas.
  - `EtsNetSceneSetup.cs`: "Add network objects (truck sync)" and "(session + players)". It adds the NetworkObject
    and data component when missing, fixes the flags and interest, and saves the scene so Fusion bakes it.
  - AoM `AomSceneSetup.cs`: probe, texture browser, unit browser, map viewer, battleground, model viewer,
    embedded game. `AomVrBoardBuilder.cs` has "Build VR Menu Prefab".
- **Pattern to copy** (from `EtsSceneSetup.cs`):
  ```csharp
  [MenuItem("Somnium Rec Room/Create ...", false, 20)]
  private static void Create()
  {
      GameObject host = CreateHost("Rr ...", out TMP_Text label);   // host + Status Canvas
      var c = host.AddComponent<RrSomething>();
      var so = new SerializedObject(c);
      so.FindProperty("_status").objectReferenceValue = label;
      so.ApplyModifiedPropertiesWithoutUndo();
      Undo.RegisterCreatedObjectUndo(host, "Create ...");
      Selection.activeGameObject = host;
  }
  ```

---

## 9. Spawn, teleport, respawn

- **Spawn point.** Set by `WorldConfig.SpawnPointPos` and `SpawnPointDirection`. Put a `Spawn Platform` (collider)
  under it, because the world is built after the player arrives. For Rec Room: spawn inside the Dorm at its origin,
  and make sure the dorm floor collider exists **before** the player lands. The platform can be the dorm floor
  itself, placed statically in the scene.
- **Teleport.** Use the Bridge only:
  `SomniumBridge.PlayersContainer?.LocalPlayer?.Features?.Motion?.DoTeleportToPoint(Vector3 pos, Vector3 eulerDeg, Action done)`,
  with rotation `(0, yaw, 0)` (`EtsVr.Teleport`, `EtsVr.cs:230`). Setting the body transform directly does not
  work, because the character controller drags the player back (AomBridge note). ETS waits for the teleport's
  callback before hiding the old place.
- **Respawn.** In `BodyFeature.cs:172-238` (client), the head position below `RespawnHeightY` triggers a teleport
  back. The target is the spawn point (behaviour 0) or the last grounded position (1). After 3 attempts
  (`FALLING_ATTEMPTS = 3`) the client switches on **free-fly mode** and shows "Anti falling system". **Every playable
  area of a Rec Room room must be above `RespawnHeightY`.** To place rooms "far away", separate them horizontally,
  not downward.
- The Bridge also has `SetMovementSpeed`, `SetGravity`, `SetJump`, `SetTeleportation(...)`,
  `SetTeleportationDisableState`, `SetCollisionDisableState`, `SetFlyModeDisableState`, `SetGlideDisableState`
  (`ISomniumPlayerMotion`) and scene components `SomniumPlayerTeleportationRules` / `SomniumPlayerCollisionRules`
  (areas).

---

## 10. Runtime-built UI

### 10.1 Screen canvas: `EtsGameUi.CreateCanvas` (`EtsGameUi.cs:288-302`)

```csharp
public static Canvas CreateCanvas(string name, int sortingOrder, bool interactive = true)
{
    var go = new GameObject(name);
    var canvas = go.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = sortingOrder;
    var scaler = go.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1440, 900);          // the game's frame (ETS /def/ui.sii)
    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
    scaler.matchWidthOrHeight = 0.5f;
    go.AddComponent<GraphicRaycaster>();
    EtsVrPanel.Adopt(canvas, interactive);                        // becomes a world-space panel in a headset
    return canvas;
}
```

`Place(parent, name, left, right, bottom, top)` (l.315) puts a RectTransform with anchor and pivot (0,0) at pixel
coordinates of the frame. **It copies the parent's layer onto every child at birth.** The XR ray only hits the UI
layer, and a child created on layer 0 is dead to the ray. `Label(rect, text, size, align, colour)` (l.412) adds a
TextMeshProUGUI with `raycastTarget = false` and wrapping on. `MakeButton(rect, label, onClick)` is at l.1030.

Sorting orders in use: loading screen 500 over desktop 400, status label 100, game screen -100.

### 10.2 VR conversion: `EtsVrPanel` (`EtsVrPanel.cs`)

Screen-space overlays are not drawn into a headset (only the desktop mirror). So when `EtsVr.Present`
(`XRSettings.isDeviceActive && loadedDeviceName != ""`, `EtsVr.cs:45`) is true, `Adopt` (l.54/59) converts every
canvas:
- `renderMode = WorldSpace`, CanvasScaler **disabled**, size = frame (1440x900), `localScale = 0.95 m / 1440`;
- add a `TrackedDeviceGraphicRaycaster` next to the GraphicRaycaster. This is what answers the client's XR ray
  (AoM: "the interface became pressable the moment it was given one");
- the whole tree goes on **layer 5 (UI)**, re-applied in `Start` for late children;
- `worldCamera = EtsVr.Head()`, re-asked every 0.5 s because the client camera arrives late and can change;
- `Face()` (l.132) hangs it upright 0.8 m in front of the eyes, yaw only. It comes round again if the head moves more
  than 2.5 m away. Panels drawn over others sit 0.0001 m x sortingOrder nearer;
- full-screen stretched children (offsets beyond ±500) are cut to the frame;
- `AttachTo(anchor, localPoint, lookFromLocal, focus, frameWidth)` (l.173) parents it to a moving thing (the
  dashboard). **This is the primitive for a wrist watch.**

`EtsVr.Head()` (l.248) returns `Camera.main` if active. Otherwise it returns any enabled camera with no target texture
and a stereo eye mask that is not None. Ask again every time; never cache the camera.

### 10.3 World-space panel driven by the client pointer: `AomUiPanel` (AoM)

A plain Canvas with a `GraphicRaycaster` and a camera assigned, driven by the client's own pointer (desktop and
VR). `UiLayer = 5` (`AomUiPanel.cs:52`). **Earlier attempts removed the raycaster and computed the ray by hand; that
never worked.** `OverEverything` (l.85) draws the panel with ZTest Always in the overlay queue, for panels strapped
to the player. The AoM pointer line uses `renderQueue = Overlay + 100` and ZTest Always while aimed at UI.

### 10.4 Wrist panel precedent (AoM, relevant to the Rec Room watch)

AoM shows the command panel on the left forearm when the player looks at the wrist (`AomVr.cs:1620-1660`):
- the hand is `AomBridge.HandOf(false)` = `SomniumBridge.PlayersContainer?.LocalPlayer?.References?.Body?.LeftHand`
  (`AomBridge.cs:52-62`). This is the avatar's IK hand, which is better than the raw controller pose (the "Arm Bar"
  is at `AomVr.cs:574-660`);
- facing = controller `deviceRotation` × a grip-to-aim twist. Up = facing·up. Visible when
  `Dot(up, (head - wrist).normalized) > 0.45` (the "look at your watch" gesture);
- placement: `position = wrist + up*0.02`, `rotation = LookRotation(-up, facing*forward)`. The arm bar variant uses
  `hand.position + hand.up*0.03`, `LookRotation(-hand.up, hand.forward)`;
- AoM does **not** read the thumbsticks. The client's quick-action menu is bound to both sticks.

Timing tip (ETS): objects that must follow the client's IK or tablet exactly are placed in
`RenderPipelineManager.beginCameraRendering`. That runs after every LateUpdate whatever the execution order (ETS
mirrors and tablet snap). Quake uses `[DefaultExecutionOrder(-500)]` for code that must run **before** the client
reads the avatar (`Q3Player`).

### 10.5 Hosting screens inside another frame: `EtsUiHost`

`using (EtsUiHost.Into(frameRect)) { EtsGameUi.BuildScreen(...); }` builds a screen inside a given RectTransform
instead of its own canvas (the ETS tablet app). `EtsUiOrder.Order` sorts siblings when there is no Canvas sorting.

### 10.6 Menus

`EtsMenu` is a runtime UGUI + TMP menu built in code: title, job board, settings. Escape toggles it (only when no
tablet is linked; Escape is the client's "Exit" binding, see 11.1). While it is open, gameplay keys are ignored and
the cursor is free. If no `EventSystem` exists (the editor), it creates "Ets Event System" with
`StandaloneInputModule` (`EtsMenu.cs:~495`). In the client, the client's XRUIInputModule already exists.

ETS phase 2 moved all menus into the **Somnium tablet** as an injected app (`EtsTabletLink.cs`, 1,514 lines; the
design notes are at l.30-70). The findings:
- the tablet lives in `MasterScene`;
- clone one client button under an inactive holder and strip it to UI/TMP;
- never use `DontDestroyOnLoad`;
- sweep leftovers named `ETS.Tablet.*`;
- the tablet canvas is on layer 9.

Rec Room's menu is a **wrist watch**, so this is only a fallback idea.

### 10.7 Fonts

- The world ships **LiberationSans SDF** (TMP Essentials, guid 8f586378b4e144a9851e7b34d9b748ee, present in the Rec
  Room project under `Assets/TextMesh Pro/Resources/Fonts & Materials/`). It ships because the scene's status label
  references it. `Resources/` alone does not pack anything.
- Fallback chain in ETS (`EtsNameplate.FallbackFont`, `EtsMenu.Build`): the font of the scene's "Status Canvas"
  label, else `TMP_Settings.defaultFontAsset`, else any `TMP_Text` in the scene.
- ETS builds TMP fonts from the game's bitmap fonts at runtime (`EtsFont.cs`, `EtsFont.Build`). Rec Room's bundles
  already hold fonts. The `Font` assets (TTF data) include legacy_FUTURA-T-OT-BOLD-CONDENSED, legacy_FuturaT_Bold,
  Jost-500-Medium, Roboto-Regular and NotoSans*. The TMP_FontAssets include "legacy_FUTURA-T-OT-BOLDSDF",
  "Jost-500-MediumSDF", "Roboto-RegularSDF" and "legacy_RecRoyaleFont-Regular SDF", mostly in bundle
  `f124b399caea956fb2ae85b8413c07d2` (13 MB, a dependency of many bundles). There are two options:
  - `TMP_FontAsset.CreateFontAsset(font)` from the Font (the TextCore modules are on the server list);
  - load the TMP_FontAsset directly. Its MonoScript is `TMPro.TMP_FontAsset` in `Unity.TextMeshPro`, which the
    client also has.

  **UNVERIFIED**: whether a 6000.0 TMP_FontAsset deserialises in the 6000.3 client. Test with a probe first.

---

## 11. Input

### 11.1 Keys and buttons the client owns

Desktop keys, from `E:\Somnium Space\Main Build\Assets\_SomniumSpace\Inputs\2D\2D Input Actions.inputactions`:

| Key | Client action |
| --- | --- |
| W A S D, arrows | Move / Navigate |
| Q, E | Rotate |
| Space | Jump |
| Shift | Sprint |
| F | Interact |
| I | Inspector |
| M | Microphone |
| R | PopUpMenu |
| Tab | Tablet |
| X | HideObject / MuteObject |
| Escape | Exit |
| Mouse left | Click |
| Mouse right | RightClick / Second Camera Action |
| Mouse middle | First Camera Action / MiddleClick |
| Scroll | Zoom |
| Enter | ends a chat message (ETS README) |

Free for a world, as used by siblings: digits, F1-F12 (ETS uses F8-F11), `[`, `]`, C, T, H, P, G and so on. The
cursor is **never locked** in desktop mode. Quake pauses its movement while Tab (tablet) is open.

VR, from the client's `XRI Input Actions.inputactions` (ETS findings):
- the menu buttons open the client menu;
- right B is InspectorMode;
- left X is Inspector (hold) and Mute/Hide (multi-tap);
- a short tap of B or Y opens the tablet;
- both thumbsticks drive the client's quick-action menu (AoM);
- triggers and grips are free for a world. ETS: right trigger accelerate, left trigger brake, grip = hold wheel,
  right A parking brake, left X/Y own menu/get out until the tablet app took over.

### 11.2 Reading input

- **Keyboard/mouse.** Use legacy `UnityEngine.Input` (InputLegacyModule is on the server list; the client runs
  `activeInputHandler: 2` = Both). Never use the Input System package.
- **VR controllers.** Use `UnityEngine.XR.InputDevices` and `CommonUsages` (`EtsVr.Poll`, `EtsVr.cs`):
  - look up the devices again every 1 s until valid;
  - use a dead zone of 0.15;
  - trigger > 0.05;
  - read the grip analog first, with the `gripButton` value as a floor (runtimes differ);
  - detect edges with held/pressed pairs;
  - haptics via `device.SendHapticImpulse(0, amp, secs)` when `TryGetHapticCapabilities(...).supportsImpulse`.

### 11.3 Clicking and pointing at world objects (measured in the client source; ETS `EtsTruckDoor`)

- **Desktop.** There is no XR interactor. Clicks come as UGUI pointer events through `XRUIInputModule` plus a
  `PhysicsRaycaster` on the desktop camera, whose effective mask is layers **0, 1, 4, 5, 14, 16**.
- **VR.** The pointer is XRI `NearFarInteractor`: far mask **0, 5, 6, 10, 14, 16, 23**, 5 m reach, triggers ignored,
  interaction layers 144 (bits 4 and 7), hovered-activate on.
- **Recipe for a clickable handle** (ETS): a non-trigger collider on **layer 16** with `excludeLayers = ~0` (so it
  pushes nothing), an `XRSimpleInteractable` (`interactionLayers` bit 4, listen to `activated` and `selectEntered`),
  and an `IPointerClickHandler`. ETS adds a legacy-Input raycast fallback on left mouse down. XRSimpleInteractable
  compiles without CoreUtils or InputSystem (checked in section 21).
- In VR, the trigger that clicked is often also a gameplay trigger. Wait for its release before treating it as
  input (ETS pedals).

---

## 12. Layers and physics

The layer collision matrix is **byte-identical** in the client (`E:\Somnium Space\Main Build\ProjectSettings\DynamicsManager.asset`),
in ETS and in the Rec Room project. AoM's differs. Decoded (row = layer, list = layers it collides with):

| # | Name | Collides with |
| --- | --- | --- |
| 0 | Default | 0,2,3,6,7,14,15,16,17,23,24,26,28,31 |
| 1 | TransparentFX | 1,2,3,4,5,10-14,16,21-24,29,31 |
| 2 | Ignore Raycast (**avatar CharacterController**) | 0,1,2,3,7,11,12,13,14,16,21,22,23,24,28,29,31 |
| 3 | (unnamed) | all |
| 4 | Water | 1,3,4,15,31 |
| 5 | UI | 1,3,5,7,10-14,16,21-24,29,31 |
| 6 | XRInteractable | 0,3,6,7,9-14,16,18,19,23,27,29,31 |
| 7 | PhysicalObject (**VR hands: two dynamic 1 kg boxes**) | 0,2,3,5,6,7,10-19,23,24,26-29,31 |
| 8 | NotRendererByMainCamera | 3,10-14,16,21-23,29,31 |
| 9 | Overlay (tablet canvas) | 3,6,9-14,16,21-23,29,31 |
| 10 | GrababbleObject | 1,3,5-16,21-24,26,29,30,31 (**not 0, not 2**) |
| 11 | ReflectionProbe | 1,2,3,5-14,16,21-24,26,28,29,31 |
| 12 | PostProcess | 1,2,3,5-14,16,21-24,29,31 |
| 13 | GrabHand | 1,2,3,5-14,16,21-23,29,31 |
| 14 | Terrain | 0,1,2,3,5-14,16,21-23,26,28-31 |
| 15 | Triggers | 0,3,4,7,10,15,16,17,19-23,28,29,31 |
| 16 | BuildingItem | 0,1,2,3,5-16,21-24,26,28-31 |
| 17 | Parcel | 0,3,7,15,18,21,22,23,29,31 |
| 18-20 | Data/Bundle/ActionTracker | (trackers) |
| 21 | NotRendererByTabletCamera | 1,2,3,5,8-24,29,31 |
| 22 | Media_360 | same as 21 |
| 23 | Environment | 0,1,2,3,5-23,26,28-31 |
| 24 | Player | 0,1,2,3,5,7,10,11,12,16,21,22,24,25,26,28,31 |
| 25 | Underground | 3,24,31 |
| 26 | Bullet | 0,3,7,10,11,14,16,23,24,27,28,29,31 (**not 2**) |
| 27 | ShootingPlayer | 3,6,7,26,31 |
| 28 | Vehicle | 0,2,3,7,11,14,15,16,23,24,26,28-31 |
| 29 | (unnamed) | 1,2,3,5-23,26,28,31 |
| 30 | IkContact | 3,10,14,16,23,28,31 |
| 31 | (unnamed) | all |

Avatar facts (client source, ETS memory "somnium-client-physics"):
- The avatar's CharacterController is on layer 2, on `XR.Body` / `NonXR.Body`. It has a **kinematic Rigidbody**, so it
  shoves dynamic bodies at up to 10 m/s depenetration.
- The VR ground check (`_groundLayers`) is 0, 2, 7, 14, 16, 23 and 28.
- `SetCollisionDisableState(true)` only ORs layer 2 into the controller's `excludeLayers` (players stop bumping each
  other). It does NOT make the avatar pass through the world.
- Never park an avatar inside a dynamic body. The ETS truck was launched 1.6 km this way.

ETS usage (`Ets/EtsLayers.cs`):
- Default 0 is the drive surface.
- Environment 23 holds buildings.
- Vehicle 28 holds trucks.
- UI 5 holds panels.
- 16 holds click handles.
- Ground rays use `(1<<0)|(1<<14)`.
- `SetRecursively(Transform, int)`.

**Proposal for Rec Room (GUESS, not tested):**
- Room shells and floors go on 0 or 23. Both are walkable in VR and desktop. Only 0 is in the desktop pointer mask.
- Throwables go on 10 (VR far ray can point at them; they do not collide with floors on 0, so use 23 floors) or on
  16 (desktop and VR pointer both hit 16; it collides with 0 and 23, but also with the avatar capsule 2).
- **26 Bullet / 27 ShootingPlayer are a pair made for shooter worlds.** Pellets on 26 hit hit-boxes on 27 (27 collides
  only with 3, 6, 7, 26 and 31), never the avatar capsule. That is a clean fit for paintball, laser tag and
  dodgeball hit-boxes that follow each player's Bridge `Body.Head`/`Root`.
- Decide per prop with this table open.

---

## 13. Rendering, shaders and shipping them

- **Client renderer** (`E:\Somnium Space\Main Build\Assets\URP\URP_Renderer.asset`):
  - Forward (`m_RenderingMode: 0`), `m_DepthPrimingMode: 2` (Forced);
  - HBAO asks for depth and normals, so the prepass is **DepthNormals**;
  - Decal renderer feature, HDR on, MSAA 1, depth and opaque textures on;
  - Stereo is **SinglePassInstanced** (D3D11).
  - The Rec Room project's `Assets/Urp/UniversalRender_Renderer.asset` matches: forward, priming 2. Pipeline asset
    `UniversalRender.asset` (guid dd6a2a153b6721d4990239e525314207), a Legacy variant, current quality index 5.
- **Opaque custom shaders need four passes**: `UniversalForward`, `ShadowCaster`, `DepthOnly` and `DepthNormals`.
  With depth priming, opaques draw with ZTest Equal against the prepass, so a shader without DepthNormals draws
  **nothing** in the client and still looks fine in the editor (AoM ground vanished for two uploads). Keep vertex
  positions identical across passes. Reference: `EtsMulti.shader` passes at l.71, 263, 348 and 428.
- **Stereo.** Every pass needs `#pragma multi_compile_instancing`, `UNITY_VERTEX_INPUT_INSTANCE_ID`,
  `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID` and `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`. Without them
  the shader renders only into the left eye, which shows as "holes" (ETS 2026-09-22). The built-in
  `Sprites/Default` has no stereo instancing, so it was invisible in VR (AoM). Prefer
  `Universal Render Pipeline/Particles/Unlit`, `.../Unlit` or `.../Lit`.
- **How custom shaders ship.** A world is uploaded as an **asset bundle of the scene and its dependencies**. So:
  - `Shader.Find("MyWorld/X")` returns **null** in the client unless something in the scene references the shader;
  - "Always Included Shaders" is a client build setting and does nothing for a world;
  - `Resources/` is not packed (`Resources.Load` returned null in the client, AoM 2026-09-01);
  - stock URP shaders are found by `Shader.Find`, because the client ships them.
  - Two proven patterns:
    1. **ETS:** a `[SerializeField] Shader _decalShader` (and `_multiShader`, `_skyShader`, `_leavesShader`) on the
       scene's `EtsMapViewer`. In YAML this is `_decalShader: {fileID: 4800000, guid: <shader .meta guid>, type: 3}`.
       The field is copied into a static (`EtsMaterialCache.DecalShader`) with a `Shader.Find` fallback.
       `Editor/EtsShaderReferences.cs` is an `[InitializeOnLoad]` healer. It refills any empty shader field by GUID on
       scene open, after script reload (`delayCall`) and on `sceneSaving`. The reason: once, Unity compiled
       half-merged code, dropped the field and saved `{fileID: 0}`, and the sky shader silently vanished from an
       upload.
    2. **AoM:** `[SerializeField] Material _groundMaterial, _blendMaterial, _tintedMaterial` on `AomBattleground`,
       with the assets in `SOMAOM/Resources/*.mat`. `AomShaders.Carry(...)` stores them and `AomShaders.Bundled(name)`
       reads `.shader` off them. Quake likewise has `Q3MapLoader._materialTemplate` (a `SomniumQuake/Q3Lightmapped`
       material).
  - Log which shader was actually found ("Decal shader: ...", "Leaves shader MISSING ...").
- **Shader names used:** "SomniumEuroTruck/Multi|Decal|Leaves|Sky|HeightToNormal", "Somnium
  Mythology/Blend|Ground|Tinted" and "SomniumQuake/Q3Blend|Q3Lightmapped". Use "SomniumRecRoom/<Name>".
- **Fog variant stripping.** Graphics > Fog Modes is Automatic (`m_FogStripping: 0`), which the SDK expects. The
  bundle keeps only the fog variants of the fog mode **the scene is saved with**. ETS was saved with `m_Fog: 0`, so no
  fog ever drew (Editor.log: "After built-in stripping" 16 → 4). If the world wants fog, save the scene with Fog on
  in the mode that will be used.
- **RenderSettings belong to the active scene**, and in the client the active scene may not be the world's. ETS
  subscribes to `SceneManager.activeSceneChanged` and re-applies (`EtsMapViewer.cs:407`, `ApplyClimate` ~l.650).
  The Bridge also offers `SomniumBridge.Environment.SetSkybox(Material)`, `SetSunSource(Light)`,
  `SetAmbientModeSkybox(float)`, `SetAmbientModeGradient(sky, equator, ground)`, `SetAmbientModeColor(Color)` and
  `UpdateEnvironment()`.
- **Cameras.** The rendering camera is the client's, and it is often **not** `Camera.main` in the editor. Use
  `EtsVr.Head()` or AoM `AomView` (applies `WantsView` / `WantedFov` each LateUpdate to any enabled camera). Never
  write the camera in a headset. Move the rig or avatar instead (ETS seat, Quake PlaceHead). Unity's default far clip
  of 1000 m cut ETS's ground. Raise it if rooms are big.
- **Post.** The client cameras have post and HDR on with the Default volume mask. The tablet layer 9 is drawn after
  post.
- **Offline shader check** (ETS): `...\look\trees\shader\dxc.sh <file.shader>` compiles every HLSLPROGRAM with the
  Windows SDK dxc (`C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\dxc.exe`, SM 6.0) against URP 17.3
  includes copied to `...\phase1\F\shaders\inc`. It covers variants BASE, INSTANCING_ON, STEREO_INSTANCING_ON and a
  few shadow/fog combinations. It does not check ShaderLab syntax, so Unity must still import the shader.

---

## 14. Audio

- `OnAudioFilterRead` is **never called for world code** in the client (ETS had no sound at all, 2026-09-23). Build
  clips with `AudioClip.Create(name, samples, channels, rate, false)` + `SetData` and play them through ordinary
  `AudioSource`s. Quake and AoM do this.
- Streaming: `UnityWebRequestMultimedia.GetAudioClip(url, AudioType)` with `streamAudio` (AoM music, ETS radio).
  The UnityWebRequestAudio module is on the server list.
- Rec Room's 1,178 AudioClips are ordinary Unity AudioClips inside the plain bundles. Loaded with the AssetBundle API
  they play directly. There is no FMOD problem like ETS had (ETS sounds were FMOD banks and had to be synthesised).

---

## 15. Finding the game install

All three finders are static classes that never throw. Each tries a hint, walks Steam's own library list, and
reports in player words. None reads the registry: "Microsoft.Win32.Registry is not available in every Unity player
profile" (`EtsInstall.cs:248` comment), so Steam's root is taken from Program Files.

| Step | EtsInstall (`Ets/EtsInstall.cs`) | Q3Install (`Q3/Q3Install.cs`) | RrInstall (already in RR project) |
| --- | --- | --- | --- |
| hint | `Locate(string hint)` l.135: inspector path first | configured list on the loader | `Locate(string hint)` |
| Steam root | `SteamRoot()` l.248: `%ProgramFiles(x86)%\Steam`, `%ProgramFiles%\Steam` if `steamapps` exists | drive roots × RelativeRoots incl. Proton/Deck paths | same as ETS (`SteamRoots()`) |
| libraries | `SteamLibraries()` l.168: root + every `"path"` line of `steamapps\libraryfolders.vdf` (`VdfPath` l.217, unescapes `\\`) | also `config/libraryfolders.vdf`; `DriveInfo.GetDrives()`; budgeted folder hunt (depth 4, 4000 dirs) | + `SteamLibrary`, `Steam`, `Games\Steam`, Program Files on every fixed drive |
| folders | "Euro Truck Simulator 2", "... Demo" (full game first) | "Quake 3 Arena", "Quake III Arena", "QuakeIII", "quake3" | "RecRoom", "Rec Room"; Oculus: `Software\against-gravity-rec-room` under Program Files\Oculus\Software, "Oculus Apps", "Meta Apps" |
| validity | `IsInstall(root, out missing)`: exe + base.scs + def.scs | a `baseq3` holding `*.pk3` | `RecRoom_Data\StreamingAssets\aa\StandaloneWindows64\*.bundle` exists |
| report | `Describe(root, home)` l.322: "not found ... runs your own copy ... install it" + sizes | `Result.Report`: every path tried | `Describe(root)` |

Measured on this PC for Rec Room:
- Steam app **471710**, `E:\SteamLibrary\steamapps\appmanifest_471710.acf`.
- Install `E:\SteamLibrary\steamapps\common\RecRoom` holds RecRoom.exe, Recroom_Release.exe, GameAssembly.dll,
  UnityPlayer.dll, Referee*.
- `RecRoom_Data\StreamingAssets\aa\` holds `catalog.bin`, `catalog.hash`, `settings.json`, `AddressablesLink/` and
  `StandaloneWindows64/` (2,787 bundles).
- `C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf` lists `C:\Program Files (x86)\Steam`,
  `E:\SteamLibrary` and `D:\SteamLibrary`.

**Visitors without the game** must get a clear message on the status label and in the log (ETS: "A visitor without
the ETS2 Demo installed stays in the empty void"). They should still see other players.

---

## 16. Threading, frame budgets, caching and lifetime

- **`EtsWorker`** (`Ets/EtsWorker.cs`, 198 lines) is one background thread (`IsBackground`,
  `ThreadPriority.BelowNormal`) for **pure** work: file reads, decompression, parsing. Its API is
  `new EtsWorker(name)`, `Post(Func<object> work, Action<object, Exception> done)`, `int Drain(Func<bool> more)` (main
  thread; runs callbacks while `more()` is true), `Waiting`, `Threaded`, `Failure` and `Dispose()`. If the thread
  cannot start, jobs run inline, one per Drain. Rules:
  - no UnityEngine object on the worker;
  - no shared cache the main thread writes (ETS locks `ReadSii`'s dictionary after it lost entries under concurrent
    reads);
  - stream reads under a lock.

  The Rec Room project already has `Rr/RrWorker.cs`.
- **Frame budgets.** VR at 90 Hz has about 11 ms for everything. Budgets used in ETS:
  - details 1.5 ms (`EtsDetailDriver.FrameBudgetMs`, a queue of `Action`s drained under a `Stopwatch`, at least
    one job per frame);
  - streaming 2-2.5 ms, loading ≤ 12 ms under a loading screen;
  - truck builds in 1.5 ms slices;
  - `Step(Stopwatch, budgetMs)` / `IndexStep(budgetMs)` methods that return true when done.

  Log the worst frame ("worst build frame X ms (budget 2.5 ms)").
- **Caching.**
  - Load each texture and material once (`EtsTextureCache`, `EtsMaterialCache`).
  - `EtsModelBuilder.MeshCache` shares meshes. **Never destroy a shared mesh on unload** (`EtsModelBuilder.IsShared`
    guard; a bug that deleted every tree of a species).
  - Use `material.enableInstancing` and static batching per built chunk (`CombineStatic`).
  - Cache expensive derived data in Bridge LocalStorage keyed by a signature (`ets_hq_catalog_<signature>`).
    RrInstall already exposes `catalog.hash` for that.
  - Don't retry failures forever: keep an `_unbuildable` set. ETS rebuilt 190 failed items every 0.5 s until it did.
- **Lifetime.**
  - Never `DontDestroyOnLoad`; everything must go when the player leaves.
  - **Statics survive a re-entry** (the client keeps the assembly loaded). Clear statics in `OnDestroy` and
    re-read per visit.
  - Unsubscribe static events (`SceneManager.activeSceneChanged`) in `OnDestroy`.
- **AssetBundles** (reasoned from Unity behaviour, **UNVERIFIED in the client**). Loaded bundles are not scene
  objects and survive the world being unloaded. Unload them in `OnDestroy` (`bundle.Unload(true)` when leaving).
  On start, reuse any a previous visit left loaded via `AssetBundle.GetAllLoadedAssetBundles()`. Otherwise the second
  visit fails with "another AssetBundle with the same files is already loaded".
- **Bridge in the editor.** `SomniumBridge.PlayersContainer`, `Network`, `Environment` and `LocalStorage` all
  return **null** when there is no client (`HasInstance` is `protected static`, so calling it gives CS0122). Always
  `?.`. In the editor there is no avatar, so collision bugs with the avatar never show there.

---

## 17. Error handling and diagnostics

- **Never throw out of a search or a build step.**
  - Swallow per candidate (an unreadable drive costs one library, not the search).
  - Wrap each job in try/catch.
  - Count failures (`JobFailures`) and warn once per kind.
- **A status label** in the scene reports every failure a player could hit. A scene without one "fails silently as
  far as anyone standing in the world is concerned" (`EtsSceneSetup` summary).
- **Capability probe first** (AoM/ETS `*LaunchSpike`, ETS `EtsClientProbe`). A throwaway component measures what the
  plan depends on in the real client, logs each fact once and removes itself. For Rec Room the first probe should
  check that:
  - `AssetBundle.LoadFromFile` works on a Rec Room bundle in the client;
  - the load time;
  - that dependencies resolve;
  - which components come back as missing scripts;
  - that a TMP_FontAsset and an AudioClip from the bundles work.
- **Before theorising, measure.** F10-style "what is under the cursor" and census lines. The user's rule: "Never
  guess, check the files, state which files were checked" (memory "never-guess-check-the-files"). Change only what
  was reported broken.
- **Offline replicas and unit tests** of pure logic: AoM `Tests/Editor/AomWireTests.cs` (NUnit), run offline with an
  assert shim via `dotnet run`; ETS "probe NNNN" harness programs in its scratchpad.

---

## 18. Persistence (Bridge LocalStorage)

Decompiled `SomniumSpace.Bridge.LocalStorage.ISomniumWorldsLocalStorage` (same Bridge DLL as the RR project):

```csharp
string ReadText(string fileName, bool usePlainText = false);
bool   WriteText(string fileName, string text, bool usePlainText = false);
bool   DeleteText(string fileName);
bool   TextExists(string fileName);
string[] GetAllTextNames();
T      ReadJson<T>(string fileName, bool usePlainText = false) where T : new();
bool   WriteJson(string fileName, object data, bool usePlainText = false);
bool   DeleteJson(string fileName); bool JsonExists(string fileName); string[] GetAllJsonNames();
long   GetUsedSpaceBytes(); void ClearAllData();
```

- Get it with `SomniumBridge.LocalStorage`. Read it **once**, because its getter logs "[Bridge] LocalStorage was not
  found" on every call when missing.
- Files live in `%LOCALAPPDATA%\Temp\Somnium Space Ltd\Somnium Space VR\WorldsLocalStorage\<worldId>\`, encrypted by
  the client unless `usePlainText`. That folder is under %TEMP%, and Windows cleaning can wipe it.
- ETS keeps two check-summed copies (`ets_profile_<id>`, `ets_profile_<id>_2`), keyed by
  `LocalPlayer.Properties.Id`, and keeps unreadable files as `_bad`. A save costs 7.7 ms median.
- In the SDK editor it is null, so the data lasts one Play session.

---

## 19. Networking (summary; the pattern the siblings use)

- **Session.** One Photon Fusion 2 **shared-mode** session per world instance. Everyone in the instance is already in
  it, and the first player in is master. The world does **not** start a runner. Its `SceneNetworking` picks up the
  client's (`OnNetworkRunnerCreated`, `IsNetworkReady`, `IsConnected`, `NetworkRunnerRef`).
  - Master is `SceneNetworking.IsMasterClient` → `runner.IsSharedModeMasterClient` (ETS `SceneNetworking.cs:55`).
  - Events: `OnLocalPlayerJoined`, `OnBecomeWorldMaster`, `OnOtherPlayerJoined(PlayerRef)`,
    `OnOtherPlayerLeft(PlayerRef)` and `OnReliableMessageReceived`.
  - Player number: `PlayerRef.PlayerId` (1, 2, ...). Nickname:
    `SomniumBridge.PlayersContainer.LocalPlayer.Properties.NickName`.
- **Constraints.**
  - No runtime-created network objects and no custom NetworkBehaviours. Use only the hand-placed scene objects with
    `NetworkBridgeEvents`, `NetworkBridgeData1/2/4/6/8/12/16/32/64` (an int `NetworkArray` each) or the older
    `NetworkBridge` (512 bytes; the user calls it "discouraged, heavy").
  - RPCs carry at most `RpcAttribute.MaxPayloadSize = 512` bytes and are for events only. Unchanged networked ints
    cost no bandwidth.
  - The user's guidance allows up to about 1,000 network components.
- **Scripts and GUIDs** (copy WITH GUIDs so scene references survive):

| Script | GUID |
| --- | --- |
| SceneNetworking (AoM/Quake copy) | c08a1242500f41e4fad1f1551d642746 (ETS's own copy: 1d432886c69945999ab0dea302713af7) |
| NetworkBridge | 28732b03e6f75884696d521673a4d2b4 |
| NetworkBridgeEvents | 6ba26196d4959fb45a90fa1113c622e9 |
| NetworkBridgeData1 / 2 / 4 / 6 / 8 | b732b36a0f3fb5b419a191381c61f517 / 5392ff6614ff6a249b574b006eab71eb / 600a00e8478d7ff49b449c5206e1f16a / e67a5587f84aae342a807972a8e0f4d6 / 763ab0b6abbf99842b746e43535cc896 |
| NetworkBridgeData12 / 16 / 32 / 64 | b9da383ae417d8d49822395dc0037d77 / d16fbf991cc3bbc45ba471b20fc1642c / 4186952287220d44cad3d65df203df41 / 0b8147e129a49fd49ba7bdc9dc75daaa |
| NetworkBridgeData (base, `Virtual/`) | f25c252a6d007714bb7343d6e040f368 |
| Fusion NetworkObject | `{fileID: -1552182283, guid: e725a070cec140c4caffb81624c8c787}` (Fusion.Runtime.dll) |

- **NetworkObject YAML** (ETS "Ets Net Session"): `SortKey: 3877532583`, `ObjectInterest: 1` (Global),
  `Flags: 524289` (= `NetworkObjectFlags.V1 | AllowStateAuthorityOverride` = `SceneNetworking.NETWORK_OBJECT_DEFAULT_FLAGS`),
  `NestedObjects: []`, `NetworkedBehaviours: [ {fileID: <the data component>} ]`,
  `ForceRemoteRenderTimeframe: 0`. Fusion's editor bakes SortKey and NetworkedBehaviours when the scene is saved, so
  prefer the editor tool (`EtsNetSceneSetup`) over hand YAML. SceneNetworking fields: `_autoTransferObjectsAuthority: 1`,
  `_editorConnectPhotonNetwork: 0`, `_networkPrefabs: []`, `_networkPrefabsGuid: []`.
- **Code.** Hold components as the **base type `NetworkBridgeData`** and check `IntCount`, as AoM and ETS do. The
  server-API guard has no proof for `NetworkBridgeData64` as a type in code (section 21). Proven members include
  `NetworkBridgeData.IntArray/IntCount`, `NetworkBridgeEvents.RPC_SendMessageToAll/Proxies/Controller/Player`,
  `add_OnMessageTo*`, `add_OnSpawned`, `add_OnStateAuthorityChanged`, `Object.HasStateAuthority` and
  `NetworkArray<int>.Set`. Authority comes from `Object.RequestStateAuthority()` on the objects a client writes.
- **Architectures that worked.**
  - AoM: the host runs the sim; mirrors puppet it via 64 `Aom Net Slots` of 4 ints per unit; orders go by RPC to
    the controller.
  - ETS: a session object written by the master; one 64-int slot per player ("Ets Net Player 0..49", claimed by
    authority); positions in centimetres; puppets on a stamped Hermite timeline.
- **The Rec Room project has NO networking yet.** To add it, copy exactly what ETS copied from AoM, with GUIDs:
  - `Assets/Photon/Fusion` (9.2 MB) and `Assets/Photon/PhotonLibs` (1.1 MB);
  - `Assets/#User/Community Modules/Plugins/SomniumSpace/Networking/` (the SomniumSpace.Network asmdef +
    NetworkBridge*.cs);
  - a world-owned `SceneNetworking.cs` in the world namespace;
  - add `"com.unity.nuget.mono-cecil": "1.10.2"` to `Packages/manifest.json`. All three siblings have it; the RR
    project does not. Fusion's weaver asmdef `Unity.Fusion.CodeGen` needs Mono.Cecil;
  - the sibling projects' Standalone scripting defines include
    `FUSION_LOGLEVEL_INFO;FUSION_WEAVER;FUSION2;FUSION_2;FUSION_2_0;FUSION_2_0_12;FUSION_2_OR_NEWER;FUSION_2_0_OR_NEWER`.
    **GUESS**: Fusion's installer adds them on import. Check after import.

---

## 20. The new project `D:\Dreamport\Rec Room\Rec Room` (read-only inspection, 2026-09-23 ~23:20)

- **Unity** 6000.3.5f2 (`ProjectSettings/ProjectVersion.txt`). Product "SomniumSpaceSDK". Linear colour space.
  `m_StereoRenderingPath: 1`. `activeInputHandler: 2` (Both). Standalone API compatibility 3 (.NET Framework 4.x).
  `allowUnsafeCode: 0`. Standalone defines are only `USE_INPUT_SYSTEM_POSE_CONTROL;USE_STICK_CONTROL_THUMBSTICKS`.
  `EditorBuildSettings` scenes are empty.
- **Assets:**
  - `#User/SomniumRecRoom/Rr/`: 11 scripts written by other agents tonight (RrCatalog, RrCommonStrings, RrConfig,
    RrInstall, RrLz4, RrPrefabData, RrReach, RrReader, RrSerializedFile, RrUnityFs, RrWorker). Namespace
    `SomniumSpace.Worlds.SomniumRecRoom.Rr`. **No asmdef and no .meta files yet.** Without an asmdef they compile
    into Assembly-CSharp, which the Uploader cannot use as the scripting assembly.
  - `Plugins/SomniumSpace/`: Bridge (+ Editor, xml), Bundles, Common, Helpers, SDK, SDK.Editor, SDK.Tools and User
    DLLs (same md5 as ETS).
  - `Plugins/Meshia/Meshia.MeshSimplification.Runtime.dll` (skipped by the server).
  - `SomniumSpace/Prefabs/WorldTestingManager.prefab`; `SomniumSpace/Resources/` (Fonts/Exo,
    Shader/CubeMapToLongLat, UIToolkit, WorldTestingInput.inputactions).
  - `SomniumSpaceTemplates/`: ItemsTemplate, SimpleWorldTemplate/SimpleWorldScene.unity,
    WorldTemplate/Somnium_WorldLounge.unity (+ `ScriptingExamples/Bridge/Scripts/SomniumSpace.Bridge.Examples.asmdef`,
    which references TMP by GUID only).
  - `TextMesh Pro/` (Essentials: LiberationSans SDF, TMP Settings guid 3f5b5dff67a942289a9defa416b206f3).
  - `Urp/` (UniversalRender.asset dd6a2a15..., UniversalRender_Legacy.asset e63e9c1b..., UniversalRender_Renderer.asset
    c42ea1a7..., GlobalSettings aef57a8e...).
  - `XR`, `XRI`, `XRI_Examples`, `Animation/Spinner`, `DefaultVolumeProfile.asset`, `Editor Default Resources`.
  - **No Photon, no Community Modules, no world scene.**
- **Packages/manifest.json:**
  - URP 17.3.0, XRI 3.3.1, xr.management 4.5.3, OpenXR 1.16.1, ugui 2.0.0, newtonsoft 3.2.2, test-framework 1.6.0,
    timeline 1.8.10, editorcoroutines, fbx 5.1.5, 2d.sprite, ide.rider/visualstudio, and every built-in module;
  - identical to the siblings' except that it **lacks `com.unity.nuget.mono-cecil` 1.10.2**.
- **ProjectSettings:**
  - TagManager layers as in section 12 (tag `AvatarPreviewImage`);
  - DynamicsManager matrix identical to the client (gravity -9.81, default contact offset 0.01, solver iterations 6,
    max depenetration 10, `m_QueriesHitTriggers: 1`, `m_AutoSyncTransforms: 0`);
  - Graphics: pipeline dd6a2a15..., fog stripping Automatic (keep linear/exp/exp2 flags 1), lights linear intensity;
  - Quality: current level 5 "Ultra" → UniversalRender.asset.
- **Generated csproj files** (22:33, before the Rr files existed): `Assembly-CSharp.csproj`,
  `Assembly-CSharp-Editor.csproj` and `SomniumSpace.Bridge.Examples.csproj`. They use LangVersion 9.0,
  TargetFrameworkVersion v4.7.1, references from `...\6000.3.5f2\Editor\Data\UnityReferenceAssemblies\unity-4.8-api\`
  (mscorlib, System*, Facades/netstandard ...) plus all `Managed\UnityEngine\UnityEngine*.dll`, UnityEditor, plugin
  DLLs and PackageCache DLLs, and defines including `UNITY_EDITOR`. They are editor builds, so they are not the
  server's list; section 21 derives the server list instead.
- `Library/ScriptAssemblies` has UnityEngine.UI, Unity.TextMeshPro, Unity.XR.Interaction.Toolkit,
  Unity.RenderPipelines.Core/Universal.Runtime, Unity.InputSystem, XR.CoreUtils and so on. It has **no Fusion and no
  SomniumSpace.Network**, so the compile check borrows those from AoM.

---

## 21. Compile-checking outside Unity (built and tested tonight)

Folder: `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad\compilecheck\`

| File | What |
| --- | --- |
| `build.sh` | The server-like build plus the Fusion/Somnium API guard, optionally the editor builds |
| `make_csproj.sh` | Turns `out/server.rsp` into an SDK-style csproj for `dotnet build` |
| `sample/RrCompileSample.cs` | Touches every API family the world will use (AssetBundle, URP, AudioClip.Create, UnityWebRequestMultimedia, TMP, UGUI, TrackedDeviceGraphicRaycaster, XRSimpleInteractable, UnityEngine.XR, Bridge, Fusion, NetworkBridgeData, threads) |
| `sample/Editor/RrSampleSceneSetup.cs` | Editor menu sample |
| `bad_inputsystem/`, `bad_fusionapi/` | Negative tests |
| `guard/` | `RefDump.dll`, `RefDump.cs`, `known_good.txt` copied from ETS `scratchpad/fusionapi/` |
| `tools/enumdump/` | Tiny metadata dumper (`dotnet run -- <dll> <TypeSuffix>`) used to read SDK enums and fields |

**Usage** (Git Bash):

```bash
C=/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad/compilecheck
bash $C/build.sh                     # sample/                   -> out/server_like.dll + guard
bash $C/build.sh project             # Assets/#User/SomniumRecRoom (excl. Editor/, Tests/) - read-only on the project
bash $C/build.sh project --editor    # + editor_like.dll (UNITY_EDITOR) + editor_tools.dll (Editor/ folder)
bash $C/build.sh dir "<windows path>"
bash $C/make_csproj.sh && dotnet build $C/csproj/SomniumRecRoom.CompileCheck.csproj -nologo -v q
```

**How it works.**
- The compiler is Unity's own Roslyn: `"C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe" ".../DotNetSdkRoslyn/csc.dll" -noconfig @out/server.rsp`
  (Roslyn 4.3.1). Settings: `-langversion:9 -nostdlib+ -target:library -warn:4`, and defines
  `UNITY_STANDALONE_WIN;UNITY_STANDALONE;UNITY_64;UNITY_6000_0_OR_NEWER;UNITY_6000_3_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2023_1_OR_NEWER;ENABLE_LEGACY_INPUT_MANAGER;ENABLE_VR;ENABLE_PHYSICS;ENABLE_AUDIO;CSHARP_7_3_OR_NEWER`.
  There is **no UNITY_EDITOR**. **GUESS**: the server's exact define set is unknown.
- References:
  - `unity-4.8-api\{mscorlib, System, System.Core, System.Xml, System.Xml.Linq, System.Numerics, System.Runtime.Serialization, System.IO.Compression, Facades\netstandard, Facades\System.Runtime}.dll`;
  - every `~ServerReferences\UnityEngine\*.dll` line of AoM's `Scripting\logs.txt`, mapped to `...\Editor\Data\Managed\UnityEngine\*.dll`;
  - from the RR project: `Library/ScriptAssemblies/{UnityEngine.UI, Unity.TextMeshPro, Unity.XR.Interaction.Toolkit, Unity.RenderPipelines.Core.Runtime, Unity.RenderPipelines.Universal.Runtime}.dll`,
    `Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll` and
    `Assets/Plugins/SomniumSpace/Bridge/SomniumSpace.Bridge.dll`;
  - from AoM, until the RR project imports Photon: `Assets/Photon/Fusion/Assemblies/Fusion.{Common,Log,Realtime,Runtime,Sockets}.dll`,
    `Library/ScriptAssemblies/{Fusion.Unity, SomniumSpace.Network}.dll`,
    `Assets/Photon/PhotonLibs/netstandard2.0/Photon3Unity3D.dll`, `PhotonLibs/WebSocket/websocket-sharp.dll` and
    `Fusion/Plugins/NanoSockets/NanoSockets/Dynamic/NanoSockets.dll`.
- Paths are passed as Windows paths (`pwd -W`). csc cannot see Git Bash's `/tmp` mapping, which was the first
  failure.
- **Guard.** `RefDump.dll` (run with Unity's dotnet) lists every `Fusion*`/`SomniumSpace*` member and type the DLL
  references. Anything not in `known_good.txt` (members used by AoM, Quake, Blades Edge and ETS builds that passed
  the server) is printed as UNPROVEN and the script exits 1.
- Editor builds, as ETS keeps three lists (`runtime.rsp`, `runtime_server.rsp`, `editortool.rsp`): the same runtime
  sources with `UNITY_EDITOR` + `UnityEditor.dll`/`UnityEditor.CoreModule.dll` → `editor_like.dll`, then `Editor/**`
  against it → `editor_tools.dll`.

**Results tonight:**

| Run | Result |
| --- | --- |
| `build.sh` (sample) | 0 errors, 0 warnings; guard: "all 29 Fusion/Somnium members are proven"; exit 0; ~2-6 s |
| `build.sh sample --editor` | server-like 0, editor-like runtime 0, editor tools 0 errors |
| `build.sh dir bad_inputsystem` | `error CS0234: The type or namespace name 'InputSystem' does not exist in the namespace 'UnityEngine'`, which is the intended failure |
| `build.sh dir bad_fusionapi` | compiles; guard prints `Fusion.Runtime \| Fusion.NetworkRunner::GetMasterClient` (the exact member that broke the ETS upload), exit 1 |
| `build.sh project` | the 11 Rr files: 0 errors, 0 warnings, 0 Fusion/Somnium members used |
| `dotnet build` of the generated SDK csproj | Build succeeded, 0 warnings, 0 errors, 2.4 s (`netstandard2.1`, `NoStdLib`, `DisableImplicitFrameworkReferences`, same 109 references and defines) |

Findings the sample surfaced:
- `SomniumBridge.HasInstance` is **not accessible** (CS0122; it is `protected static`, although the XML docs list
  it). Use the null-returning getters.
- The first sample held `NetworkBridgeData64` as a field type, and the guard flagged `SomniumSpace.Network |
  NetworkBridgeData64 (type)` as unproven. Hold `NetworkBridgeData` instead.

**What it cannot catch:**
- the server's real define set;
- shaders (use `dxc.sh`, section 13);
- serialization and scene problems;
- anything behavioural in the client.

A clean offline build is necessary, not sufficient. The ETS rule is to run it before every READY.

---

## 22. README style (the user's preferred format)

Model: `D:\Dreamport\Euro Truck Simulator\Euro Truck Sim\Assets\#User\SomniumEuroTruck\README.md`. It sits in the
code folder (`Assets/#User/<World>/README.md`) and is written in plain British English prose.

1. `# Somnium <Game>` followed by one paragraph: the game inside a Somnium world, read from the player's install at
   runtime, nothing shipped, non-XR first then VR.
2. `## Where it stands`: a table `| Layer | State |`. Each state starts with a bold word (Done, Built, Runs,
   Synthesised, Not built) followed by measured numbers ("All 8 demo archives mount in ~0.3 s", "0 bad indices") and
   what is untested ("Built, untested in Play as of 2026-09-09 02:00").
3. `## Files`: a table `| File | Role |`, one line each.
4. Format facts (measured), `## Workflow` (numbered: open the scene, press Play; menu items; upload with scripting
   on and the assembly name), and `## Next` (numbered).
5. Then **dated sections**, newest last: `## <What happened, in words> (YYYY-MM-DD[, time of day])`, for example "The
   truck fell through the world in the first client run, and getting in by clicking (2026-09-21, night)". Inside:
   - **What the log said.** Quote the Player.log lines.
   - **Why, from the client source.** Give file and line.
   - **What changed.** Bullets naming classes, and GUIDs of new scripts.
   - **Adversarial review, fixed / Rejected by its verifier.**
   - **Log lines to look for.** Exact strings.
   - **Still open / Not verified / Risks.**
   - **Integration.** Which rsp lists gained which files; "All three builds compile with 0 errors and 0 warnings";
     "The server API check reports all N members proven".
6. Corrections are written into the text ("**Correction (same evening):** this section first said ... It does
   not"). Dead ends are recorded as "DEAD END, do not retry: ...".

---

## 23. Working agreements with the user (from memory files)

- **Order.** The user's standing order is non-XR first, then VR. For Rec Room they explicitly asked for both.
- **Never ship game assets.** Read the player's install.
- **Upload status.** End every code-changing turn with a separate line, **READY TO UPLOAD** (compiled offline against
  the server list, guard passed, contents listed) or **NOT READY TO UPLOAD YET** (what is missing). Say so when
  shader edits could not be compiled offline.
- **Intermediate uploads.** The first line must say "This build does NOT yet have X and Y". Never hide requested
  features behind flags.
- **Judge by the real game.** The user compares everything to the real game and sends screenshots. "Close enough"
  custom UI is rejected; replicate the real one (ETS: three failed launchpad rebuilds).
- **Evidence.** Never guess; read the file; say which files were checked; change only what was reported broken.
- **Files outside the project** (the user's own data) cannot be edited by the agent (a safety block). Give the user a
  one-line command instead.
- The user tests in the client and uploads through the SDK. Their next Player.log is the evidence.

---

## 24. Gotchas master list (every one recorded in the siblings; a new world must avoid them)

**Upload and compile**
1. An asmdef reference the server skips (CommunityModules, AsyncIO, NetMQ, SomniumSpace.SDK/Bundles/Common/Helpers/User,
   Meshia) gives **CS0246** at upload (AoM 2026-09-02). Copy the needed scripts into the world folder with their
   GUIDs instead.
2. Fusion members newer than the server's Fusion give **CS1061** (`GetMasterClient`, ETS 2026-09-23). Use only what
   the guard proves. Master = `IsSharedModeMasterClient`. ETS also removed `GetAllBehaviours<NetworkTransform>`.
3. `Unity.InputSystem` and `Unity.XR.CoreUtils` are not proven on the server. Use `UnityEngine.Input` and
   `UnityEngine.XR`.
4. The world compiles as **C# 9**. A `new SomeStruct()` of C# 10 code compiles but leaves arrays null.
5. `SomniumBridge.HasInstance` gives CS0122 (protected).
6. **The Uploader replaces an existing world** (`DoReplaceExistingWorld: 1`, `WorldIdForUpload`). Confirm the slot
   with the user. Quake=12308, AoM=12410, ETS=12510.
7. **Missing scripts break the scripting assembly selection.** Without an asmdef, scripts go to Assembly-CSharp and
   cannot be the scripting assembly.
8. **Half-merged code wipes scene fields.** Unity compiling half-merged code can re-serialize the scene without a
   field (ETS sky shader became `{fileID: 0}`). Heal fields in an `[InitializeOnLoad]` editor script, save the scene
   before uploading, and check the log line that proves the shader is present.

**Packing and shaders**
9. `Shader.Find` of a custom shader returns null in the client unless a scene-referenced field or material carries
   it. `Resources/` and "Always Included Shaders" do nothing for a world.
10. Opaque custom shaders without a **DepthNormals** pass are invisible in the client (forced depth priming + HBAO).
11. Shaders without stereo-instancing macros draw into the left eye only ("holes" in VR). `Sprites/Default` is not
    stereo-instanced.
12. **Fog is stripped unless the scene is saved with it.** Fog variants are stripped to the mode the scene is saved
    with. A scene saved with Fog off means no fog can ever draw.
13. Material colours must be passed as linear values (the project is linear). ETS "Material colours are passed as
    linear values".

**Rendering, cameras, VR**
14. The render camera is not reliably `Camera.main`. A disabled camera is invisible to `Camera.main`. Test "any
    camera drawing to the screen" before creating an editor camera (ETS stray camera).
15. Never set the camera pose or FOV in a headset. Move the rig or avatar through the Bridge. A panel welded to the
    head cannot be read; place it once in front of the player and re-place it when they move away.
16. Screen-space overlay canvases do not appear in a headset. Convert them to world space (`EtsVrPanel`).
17. World-space UI needs a GraphicRaycaster + **TrackedDeviceGraphicRaycaster**, layer 5 on the **whole** tree
    (including late children), and `worldCamera` set and refreshed.
18. **RenderSettings are per active scene.** Re-apply them when the world's scene becomes active.
19. Unity's default far clip (1000 m) cut the ETS world, and the skybox then showed as holes.
20. Reflections: don't turn "wet" or specular data into metal. A realtime reflection probe rendering a whole world
    every second caused the stutter (ETS). Keep probes small, infrequent and culled.
21. A rolling or tilting view can cause motion sickness. Offer a toggle (ETS cab tilt).

**Input and UI**
22. Don't bind keys the client owns (table in 11.1). Escape is the client's Exit, Tab its tablet, F Interact, R
    PopUpMenu, M mic, I Inspector, X Hide/Mute, Q/E rotate, Enter chat.
23. In VR the menu buttons, right B, left X and a tap of B or Y belong to the client. Both thumbsticks belong to its
    quick-action menu.
24. OnGUI runs several passes a frame, so consume a click once per rect (AoM `AomPointer.Clicked`).
25. Desktop world clicks only reach layers 0, 1, 4, 5, 14 and 16 (PhysicsRaycaster). The VR far ray reaches 0, 5, 6,
    10, 14, 16 and 23. Triggers are ignored.
26. The trigger that clicked a thing is often a gameplay input too. Wait for its release.
27. Pause the world's movement while the Somnium tablet is open (Quake, Tab). In VR, hold vehicles or props while the
    tablet is open (ETS).

**Physics, layers, avatar**
28. The avatar capsule is layer 2 with a kinematic Rigidbody. `SetCollisionDisableState` does not make it pass
    through the world. Never write the avatar into a dynamic body. Use `excludeLayers |= 2|7|24` on bodies the player
    sits in, or make them kinematic.
29. Layer 2 (Ignore Raycast) collides with almost nothing solid except the listed layers (0, 14, 16, 23, 28 ...). The
    ETS truck on layer 2 drove through buildings.
30. Unity computes mass properties lazily. Don't read and restore `inertiaTensor` in the frame colliders were added.
31. **Ray origins.** Ray origins inside geometry find nothing. Too-high origins find a second surface (a roof) above
    the real ground. ETS arrivals landed on garage roofs 7.4 m up.
32. PhysX "cleaning the mesh failed": don't give MeshColliders to slivers or degenerate meshes, and dedupe points.
33. The VR hands are two dynamic 1 kg boxes on layer 7. They push things on layers that collide with 7.
34. The client's anti-fall teleports the head back when it goes below `RespawnHeightY`, and after 3 falls enables
    free-fly. Keep all areas above it.

**Scene and serialization**
35. **The scene overrides code defaults.** `[SerializeField]` values in the scene beat C# defaults. Unity
    re-serializes the scene when the user plays, so re-grep the scene after every client run, and print runtime
    values (ETS `_streamRadius: 500` shrank the world).
36. Hand-written scene YAML needs the script GUIDs decided up front (write the `.meta` files). Fusion bakes SortKey
    on save; prefer the editor tools.
37. `Place` children must copy the parent's layer, or the XR ray misses rows added later (up to 250 ms of dead ray in
    ETS).

**Audio**
38. `OnAudioFilterRead` is not called for world code. Use `AudioClip.Create` + `SetData`.

**Lifetime and editor vs client**
39. **Assembly lifetime.** The client keeps the world assembly loaded, so statics survive a re-entry. Never
    `DontDestroyOnLoad`. Clear statics and unsubscribe static events in `OnDestroy`.
40. The Bridge is null in the editor (no player, no storage, no network). The editor has no avatar, so avatar
    collision bugs only show in the client.
41. `SomniumBridge.LocalStorage` logs an error on every getter call when missing. Read it once. Its folder is under
    %TEMP%.
42. Never cache the camera or the XR devices. Re-query (1 s for devices, 0.5 s for canvas cameras).
43. **Shared meshes.** Never destroy meshes or materials that are shared through a cache when unloading a chunk.
44. **Frame cost.** 90 Hz VR leaves about 11 ms. A single 100-1000 ms main-thread build item was the ETS stutter.
    Budget per frame and move pure work to a worker.
45. Worker threads must not touch UnityEngine objects or unlocked shared dictionaries.
46. Don't retry failed builds forever. Remember failures.

**Networking**
47. There are no runtime-spawned network objects and no custom NetworkBehaviours. Use hand-placed scene objects
    only.
48. RPCs carry ≤ 512 B and are for events. State goes in networked ints, and unchanged ints are free.
49. **Stale-world reads.** A joiner can read network slots before its own world is built. AoM `AomArrival`: count
    world generations and ignore reads until the match's own world exists.
50. Two installs rarely build identical content. Never number locally-generated objects and expect peers to agree.
    The host announces, mirrors follow (AoM census).
51. A master who chooses and leaves at once must not split the room. ETS keeps a "Chosen" record in the session
    object and never rewrites it just to rename the master.

**Process**
52. Every serious regression came from acting on a theory. Read the file that decides the behaviour and quote it.
53. Don't claim client behaviour that was only compiled offline. Say "not yet run in the client".

---

## 25. Open questions for the implementers (not answered by this research)

- The server's exact C# defines and language version (C# 9 assumed; every sibling passed with it).
- Whether `Unity.InputSystem` and `Fusion.Addons.Physics` resolve on the server. Quake uses both, but there is no
  log; avoid them.
- Whether Rec Room's 6000.0.27 bundles load unchanged in the 6000.3.21 client, whether their shaders work there (the
  plan is to replace them with ours anyway), and whether TMP_FontAsset MonoBehaviours in them deserialise. **Do a
  client probe first** (section 17).
- Whether the client culls or hides remote avatars by distance, and how far voice carries. This matters for the
  "rooms far apart" plan; not measured.
- Which Somnium world id Rec Room should upload to (ask the user; do not reuse 12308, 12410 or 12510 without
  permission).
