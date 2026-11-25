# Dreamport: Rec Room — architecture contract (v1, 2026-09-24)

This is the shared contract for every implementation agent. Code lives in
`D:\Dreamport\Rec Room\Rec Room\Assets\#User\SomniumRecRoom\` (asmdef `SomniumRecRoom`, root namespace
`SomniumSpace.Worlds.SomniumRecRoom`). SP = `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad`.

Read before coding: `SP\research\somnium_conventions.md` (sections 0, 6, 7, 10-13, 16, 21, 24 at least),
`SP\research\somnium_networking.md` (0, 2, 5), `SP\research\rr_runtime_facts.md`, and the research doc for your area
(`SP\research\rr_*.md`, `somnium_player.md`).

## 1. Hard rules (from the siblings; violating any of them has already broken a Dreamport)

1. C# 9, no `unsafe`, no `Unity.InputSystem`, no `Unity.XR.CoreUtils`, no `CommunityModules`. Only the asmdef's
   references + UnityEngine modules + SomniumSpace.Bridge + Fusion (proven members only, see networking doc §10).
2. **Compile check before you finish**: `bash SP/compilecheck/build.sh project` must print `0 error(s)` and
   `all N Fusion/Somnium members are proven`. Editor code: `bash SP/compilecheck/build.sh project --editor`.
3. Never modify the Somnium avatar/player objects (no components on them, no renderer changes). Use the Bridge APIs.
4. Nothing from Rec Room ships: everything comes from the player's install through `RrAssets` at runtime.
5. Never `DontDestroyOnLoad`. Clear statics and unsubscribe static events in `OnDestroy` (the assembly survives a
   world re-entry).
6. Bridge getters are null in the editor: always null-check (`SomniumBridge.PlayersContainer?.LocalPlayer?...`).
7. Frame budget: VR 90 Hz. No single main-thread step over ~4 ms outside a loading fade; spread builds with
   coroutines (`yield return null` between chunks). Pure parsing goes to `RrMain.Worker`.
8. Log with `RrLog.Info("Area", "text")` / `RrLog.Warn` / `RrLog.Error` → `[SomniumRecRoom] Area: text`.
9. Keys the client owns must not be used (Escape, Tab, F, E, Q, R, M, I, X, WASD, arrows, Space, Shift, Enter).
   VR: menu buttons, right B, left X, B/Y taps and both thumbsticks belong to the client. Triggers and grips are ours.
10. Shaders: keep Rec Room's original materials (they render correctly). Every material goes through
    `RrMaterials.Fix` (done automatically by `RrSpawn`). Our own procedural geometry uses `RrLook` materials (§6).
11. Physics layers (see conventions §12): room shells/floors **23 (Environment)** (walkable, VR ray hits it);
    clickable handles **16**; our grabbable props **10** (GrababbleObject: not hit by the avatar capsule, hit by the VR
    ray) — constants in `RrLayers`. Projectiles 26 (Bullet), player hit volumes 27 (ShootingPlayer).
12. Every playable floor stays above y = -50 (respawn height is -100).

## 2. Folder / module map

| Folder | Namespace | Owner | Contents |
| --- | --- | --- | --- |
| `Rr/` | `.Rr` | done | pure bundle format: RrUnityFs, RrSerializedFile, RrCatalog, RrPrefabData, RrConfig, RrReach, RrInstall, RrWorker |
| `Core/` | root | core | RrMain, RrLog, RrLayers, RrAssets, RrSpawn, RrInstance, RrMaterials, RrSound, RrMusic, RrFx, RrLook, RrBuild, RrText |
| `Player/` | `.Player` | player agent | RrPlayer, RrInput, RrHand, RrRemotePlayers |
| `Interact/` | `.Interact` | interaction agent | RrGrabbable, RrGrab (system), tool behaviours (drink, eat, break, collision audio, shake, ball, button, seat) |
| `Net/` | `.Net` | net agent | SceneNetworking (copied), RrNet, RrWire, RrPresence, RrInstances, RrProps, RrEvents, RrTimeline |
| `Rooms/` | `.Rooms` | rooms agent | RrActivity, RrRoomInfo, RrRooms, RrRoom (base), RrTravel, RrLoadingScreen |
| `Watch/` | `.Watch` | watch agent | RrWatch, RrWatchUi, RrUiKit |
| `Places/Dorm/`, `Places/RecCenter/` | `.Places` | place agents | the two social rooms |
| `Games/Shared/` | `.Games` | games-core agent | RrMatch, RrTeams, RrScoreboard, RrAnnouncer, RrHealth, RrHitVolumes, RrWeapon, RrProjectile, RrEnemy |
| `Games/<Name>/` | `.Games.<Name>` | one agent per game | each activity |
| `Editor/` | `.EditorTools` | editor agent | scene setup, network objects, shader references |
| `Shaders/` | - | core | only if needed (`SomniumRecRoom/<Name>`, 4 passes + stereo, see conventions §13) |

Class prefix `Rr`. One public class per file, file named after it. Write XML doc comments in the house style (prose
explaining *why*, measured facts), as in `Rr/*.cs`.

## 3. Core API (Core/) — the contract other modules call

```csharp
// RrMain: the scene's "Rec Room" object. Boots everything in order.
public sealed class RrMain : MonoBehaviour {
  public static RrMain Instance;
  public static bool Ready;                       // catalog loaded, systems up
  public static event Action OnReady;             // fires once (immediately if already Ready when subscribing via WhenReady)
  public static void WhenReady(Action a);
  public RrCatalog Catalog; public RrWorker Worker; public string InstallRoot;
  public RrAssets Assets;                         // also RrAssets.Instance
  public Transform WorldRoot;                     // parent for everything we build
  public void SetStatus(string text);             // the scene status label (+ log)
}

// RrAssets: Rec Room bundles through Unity's own loader.
public sealed class RrAssets {
  public static RrAssets Instance;
  // Find in the catalog (exact name, case-insensitive fallback). classId: 1 prefab root, 83 AudioClip, 28 Texture2D,
  // 213 Sprite, 43 Mesh, 21 Material, 128 Font, 114 MonoBehaviour asset (TMP_FontAsset...), 74 AnimationClip, 91 AnimatorController.
  public bool TryFind(string name, int classId, out RrEntry e);
  // Load an object (container or implicit) and call back on the main thread (null on failure; failures are remembered).
  public void Load<T>(RrEntry e, Action<T> done) where T : UnityEngine.Object;
  public void Load<T>(string name, int classId, Action<T> done) where T : UnityEngine.Object;
  public IEnumerator LoadRoutine<T>(string name, int classId, Action<T> done) where T : UnityEngine.Object;
  // Parsed data of a prefab (hierarchy + configs), cached; pure, can be called on the main thread (ms) or worker.
  public RrPrefabData PrefabData(RrEntry prefab);
  // Resolve an asset reference from a config (e.g. cfg.Clip("uncorkAudio").Clip) to a loaded object.
  public void Resolve<T>(RrTarget target, Action<T> done) where T : UnityEngine.Object;
  public T Resident<T>(string name) where T : UnityEngine.Object;   // already-loaded asset by name, or null
}

// RrSpawn: instantiate a Rec Room prefab safely and map its data to the live objects.
public static class RrSpawn {
  // Loads (if needed) and instantiates. The instance's Rec Room scripts are inert; bound Unity scripts that would act on
  // the client (Volume, EventSystem, cameras, lights' additional data, Button, EventTrigger...) are removed; materials
  // fixed; rigidbodies kept (set kinematic by options). Callback on main thread.
  public static void Spawn(string prefabName, Transform parent, Vector3 localPos, Quaternion localRot,
                           Action<RrInstance> done, RrSpawnOptions options = null);
  public static RrInstance SpawnLoaded(GameObject prefabAsset, RrPrefabData data, Transform parent, Vector3 localPos,
                           Quaternion localRot, RrSpawnOptions options = null);
}
public sealed class RrSpawnOptions {
  public int? Layer;                 // re-layer the whole tree (default: keep Rec Room's, but 5->5, 10->RrLayers.Prop, 17->RrLayers.Prop)
  public bool KeepUiScripts;         // keep UnityEngine.UI / TMP components (UI prefabs)
  public bool StripColliders, StripRigidbodies, Kinematic, StripAudioSources, StripLights, StripParticles;
  public bool Static;                // mark for static batching (props that never move)
}
public sealed class RrInstance : MonoBehaviour {  // on the spawned root
  public RrPrefabData Data;
  public Transform NodeOf(RrNodeData node);        // by IndexPath
  public Transform NodeOf(RrTarget target);        // a config reference into the prefab
  public Transform Find(string nodeName);          // first node by GameObject name
  public RrConfig Config(string scriptShortName);  // e.g. Config("DrinkableBottle")
  public Bounds WorldBounds();                     // renderers' bounds
}

public static class RrMaterials { public static void Fix(Material m); public static void FixTree(GameObject root); }

// RrSound: Rec Room's clip struct played like the game.
public static class RrSound {
  // pitch = 2^(pitchShift) * (1 + Random.Range(-pitchVariation, +pitchVariation))  (GUESS until the research doc says otherwise)
  // volume = configureVolumeInDecibel ? 10^(volume/20) : volume
  public static void Play(RrClipInfo info, Vector3 position, float volumeScale = 1f, Transform follow = null);
  public static void Play(string clipName, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 40f);
  public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 40f);
  public static void PlayUi(string clipName, float volume = 0.8f);    // 2D
  public static AudioSource Loop(string clipName, Transform follow, float volume = 1f);  // caller stops/destroys
}
public static class RrMusic { public static void Play(string clipName, float volume = 0.5f, bool loop = true); public static void Stop(float fade = 1f); }
public static class RrFx {
  public static void Spawn(string prefabName, Vector3 pos, Quaternion rot, float scale = 1f);  // particle prefab, auto-destroyed
  public static void Spawn(RrTarget prefabRef, Vector3 pos, Quaternion rot, float scale = 1f);
}

// RrLook: Rec Room-style materials for procedural geometry, made from Rec Room's own maker-pen / standard materials.
public static class RrLook {
  public static Material Solid(Color c);           // flat painted surface (MakerPen look), cached per colour
  public static Material Wood, Carpet, Tile, Metal, Glass, Grass, Concrete, Water;  // from Rec Room materials when found, else URP Lit fallbacks
  public static Material Emissive(Color c, float intensity = 1f);
  public static Material Unlit(Color c);
}
// RrBuild: procedural helpers; every collider it makes is on RrLayers.Environment unless told otherwise.
public static class RrBuild {
  public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, bool collider = true);
  public static GameObject Floor(...); public static GameObject Wall(Transform parent, Vector3 a, Vector3 b, float height, float thickness, Material m, params RrBuild.Hole[] holes);
  public static GameObject Ramp(...); public static GameObject Cylinder(...); public static TextMeshPro Sign(Transform parent, string text, Vector3 pos, Quaternion rot, float size, Color c);
}
public static class RrText { public static TMP_FontAsset Font; public static TMP_FontAsset Title; }  // Rec Room fonts (Futura/Jost) or LiberationSans fallback
public static class RrLayers { public const int Default = 0, Ui = 5, Grabbable = 10, Click = 16, Environment = 23, Bullet = 26, HitVolume = 27, Vehicle = 28; public const int Prop = Grabbable; public static void Set(GameObject go, int layer); }
```

## 4. Player (Player/)

```csharp
public static class RrPlayer {
  public static bool InVr { get; }                          // XRSettings.isDeviceActive && loadedDeviceName != ""
  public static Camera Camera { get; }                      // EtsVr.Head() logic, re-queried
  public static Vector3 HeadPosition { get; } public static Quaternion HeadRotation { get; }
  public static Vector3 FeetPosition { get; }               // Bridge Body.Root (or camera projected down)
  public static float Yaw { get; }
  public static void Teleport(Vector3 feet, float yaw, Action done = null);   // Bridge DoTeleportToPoint
  public static void SetMovement(float speedScale, bool jump = true);         // restore with SetMovement(1)
  public static int LocalId { get; }                         // Fusion PlayerId or 0 offline
  public static string Nick { get; }
}
public enum RrHandSide { Left, Right }
public sealed class RrHand : MonoBehaviour {                 // two instances, created by RrPlayer; our own transforms that follow the controllers/avatar hands
  public static RrHand Left, Right; public static RrHand Get(RrHandSide s);
  public RrHandSide Side; public Transform Grip;             // where held objects attach
  public Vector3 Velocity, AngularVelocity;                  // from pose history (for throws)
  public bool GripHeld, GripDown, GripUp, TriggerHeld, TriggerDown, TriggerUp; public float Trigger, GripValue;
  public bool PrimaryDown /*A/X*/;  public void Haptic(float amp, float secs);
  public RrGrabbable Held;
}
// Desktop (screen mode): RrHand.Right is a virtual hand placed in front of the camera (Rec Room screen mode holds
// objects lower-right of view); RrInput maps free keys/mouse to grip/trigger (see somnium_player.md for the scheme).
public static class RrInput { public static Ray AimRay { get; } /* camera or right hand */ public static bool UseDown, UseHeld, GrabDown, DropDown, ThrowDown, MenuDown; }
public static class RrRemotePlayers { public static IEnumerable<RrRemote> All; public sealed class RrRemote { public int Id; public string Nick; public Transform Head, Root, LeftHand, RightHand; } }
```

## 5. Interaction (Interact/)

`RrGrabbable : MonoBehaviour` on any holdable thing (our own component, added by room/game code to spawned Rec Room
props or procedural objects): grip offsets from Rec Room's `Tool.interactionSettings` when available, throw multiplier
and max throw speed, pickup/throw sounds, `OnGrab/OnRelease/OnUseDown/OnUseUp` events, `Networked` flag (uses
`RrProps` when the room is networked). `RrGrab` (singleton) handles hover/highlight, grab/release/throw for both hands in
VR and the screen-mode hand, using `RrHand`. Behaviours read their numbers from Rec Room configs via
`RrInstance.Config(...)`: `RrDrinkable` (DrinkableBottle), `RrEdible` (food consumables), `RrBreakable`
(BreakableObject), `RrCollisionSound` (CollisionAudio), `RrShakeSound` (ShakeAudio), `RrBall` (bounce/rim sounds,
hoop scoring hook), `RrPressable` (buttons: world click / VR poke / trigger), `RrSeat`. Factory:
`RrTools.MakeTool(RrInstance inst)` wires every behaviour whose Rec Room component is present on the prefab.

## 6. Rooms (Rooms/)

```csharp
public enum RrActivity : byte {   // wire ids (networking doc §5.1)
  None = 0, Dorm = 1, RecCenter = 2, Charades = 3, Bowling = 4, Crescendo = 5, DiscGolf = 6, Dodgeball = 7, LostSkulls = 8,
  LaserTag = 9, Midnight = 10, LittleMonsters = 11, Paddleball = 12, Paintball = 13, PunchUp = 14, GoldenTrophy = 15,
  RecRally = 16, RecRoyale = 17, Showdown = 18, Soccer = 19, StuntRunner = 20, CrimsonCauldron = 21, Jumbotron = 22 }
public sealed class RrRoomInfo { public RrActivity Activity; public string Title, Subtitle, Description; public int MinPlayers, MaxPlayers;
  public string CoverSprite, IconSprite; /* Rec Room asset names */ public string Music; public Func<GameObject, RrRoom> Create; public bool Networked = true; }
public static class RrRooms { public static void Register(RrRoomInfo info); public static RrRoomInfo Get(RrActivity a); public static IEnumerable<RrRoomInfo> All; }
public abstract class RrRoom : MonoBehaviour {
  public RrActivity Activity; public int InstanceNumber; public Vector3 Origin;       // world position of the room's (0,0,0)
  public abstract IEnumerator Build();                 // spread over frames; build everything under transform (positioned at Origin)
  public virtual Pose Spawn(int index);                 // local-space spawn pose; default (0,0,0)
  public virtual void OnLocalEnter(); public virtual void OnLocalExit();
  public virtual void OnMembersChanged();               // presence changed (join/leave/team)
  public bool IsAuthority { get; }                      // RrNet: this client runs the instance ("fake master")
}
public static class RrTravel {
  public static RrActivity Current; public static RrRoom CurrentRoom;
  public static void GoTo(RrActivity a, int instanceNumber = -1);   // fade + loading card + build + teleport + fade in
  public static void GoHome();                                        // dorm
  public static event Action<RrActivity> OnArrived;
}
```

Placement (networking doc §5.3): arrival room at world origin (the scene's spawn: a Rec Room-style loading space);
dorm of presence slot s at `x = -1125 + (s % 10) * 250, z = -2000 - (s / 10) * 250`; instance object k at
`x = -5000 + (k % 6) * 2000, z = 2000 + (k / 6) * 2000`; offline: slot 0 / k = activity-1 order. y = 0 for all
floors. A room must fit in 1.8 km x 1.8 km centred on its origin (Rec Royale: stay within ±900 m).
Isolation: the local client builds only its current room; everything drawn for remote players exists only while they
are in the same instance.

## 7. Net (Net/) — follow `somnium_networking.md` §5 exactly

```csharp
public static class RrNet {
  public static bool Online { get; }                       // SceneNetworking.IsNetworkReady
  public static int LocalId { get; }
  public static IReadOnlyList<int> Present { get; }
  public static event Action OnPlayersChanged;
  // presence
  public static void SetLocation(RrActivity a, int instanceNumber, RrStatus status); public static void SetTeam(int team);
  public static RrPresenceView Presence(int playerId);     // activity, instance, status, team, vr, nick, hands, health...
  public static List<int> MembersOfMyInstance();
  public static bool IsInstanceAuthority { get; }           // elected per instance; true offline
  public static event Action<bool> OnAuthorityChanged;
  // instance state (64 ints; only the authority writes; ints 27-58 activity-specific)
  public static RrInstanceState Instance { get; }           // Phase, Round, TimerRemaining, Map, Mode, Seed, TeamScore(i), Roster, Get(i), Set(i, v)
  public static int InstanceObjectIndex { get; }            // k, fixes the origin
  // events (batched RPC, scoped to my instance unless global)
  public static void Send(byte type, byte[] body, bool toAuthority = false);
  public static void On(byte type, Action<int, byte[]> handler);  public static void Off(byte type, Action<int, byte[]> handler);
  // props
  public static RrNetProp LeaseProp(int localPropId);      // authority leases; everyone looks up by local id
}
```
Offline/editor: everything works locally (the local player is authority, events loop back).

## 8. Watch (Watch/)
VR: a Rec Room watch on the left wrist (Rec Room's watch mesh if found, else ours), glance or press to open the menu
panel above the wrist. Desktop: key **`** (backquote) or **G**? (pick from free keys, see somnium_player.md) opens the
menu as a screen panel. Screens: Home (Rec Center, Dorm, Play), Play (21 activities with Rec Room artwork), People
(players in the world and where they are, "join" button), Settings (volume, snap turn? no — client owns locomotion),
Leave (back to dorm). Built with UnityEngine.UI + TMP in Rec Room style.

## 9. Games (Games/)
Each game is an `RrRoom` subclass registered in a static `Register()` called from `RrRegistry.RegisterAll()` (one line
per game). Shared kit in `Games/Shared`: `RrMatch` (lobby → countdown → playing → round over → match over, timer,
authority-driven, synced via `RrNet.Instance`), `RrTeams` (red/blue colours as Rec Room), `RrScoreboard` (world-space
board), `RrAnnouncer` (Rec Room VO clips: "Blue Team", "Blueteamwins2", "Gameon", "Gameover", "10 seconds remaining",
"One minute remaining"...), `RrHealth` (local player's hit points, respawn, hit flash), `RrHitVolumes` (layer-27 capsules
following remote players' Bridge Head/Root — our own objects), `RrWeapon`/`RrProjectile` (Rec Room RangedWeapon
configs: fire rate, speed, gravity, damage), `RrEnemy` (quest AI with Rec Room enemy prefabs + Animator).

## 10. Definition of done for every module
- compiles in the offline check (0 errors, guard proven);
- works offline in the editor (no network, no Bridge) with a fallback camera;
- logs its key facts once;
- documented in the module's summary returned to the orchestrator (files, classes, public API, what is untested).

## 11. Registering a place or game (added 2026-09-24)
`Rooms/RrRegistry.cs` is a `static partial class` that calls one partial hook per activity
(`RegisterDorm`, `RegisterRecCenter`, `RegisterPaintball`, ... `RegisterLittleMonsters`, and `RegisterInfrastructure`
for the Rooms module). A module implements ITS hook in its own folder and never edits RrRegistry.cs:

```csharp
namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    public static partial class RrRegistry
    {
        static partial void RegisterPaintball()
        {
            RrRooms.Register(new RrRoomInfo { Activity = RrActivity.Paintball, Title = "Paintball", ...,
                Create = root => root.AddComponent<Games.Paintball.RrPaintballRoom>() });
        }
    }
}
```
