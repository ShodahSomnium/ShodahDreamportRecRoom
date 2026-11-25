# Somnium networking guide for Dreamport: Rec Room

Research deliverable (networking). It is written for the implementation agents, who will rely on this file alone.
Every platform fact is measured and cited as `file:line`. Anything that is a guess, or a design proposal rather than
a measurement, is marked **GUESS** or **PROPOSAL**.

Path abbreviations used throughout:

| Tag | Path |
|---|---|
| `ETS` | `D:\Dreamport\Euro Truck Simulator\Euro Truck Sim\Assets\#User\SomniumEuroTruck` |
| `ETSP` | `D:\Dreamport\Euro Truck Simulator\Euro Truck Sim` (the ETS Unity project root) |
| `CM` | `D:\Dreamport\Euro Truck Simulator\Euro Truck Sim\Assets\#User\Community Modules\Plugins\SomniumSpace\Networking` (the world-side copy of the Community Modules networking plugin) |
| `Q` | `D:\Somnium Quake\Assets\#User\SomniumQuake` |
| `AOM` | `D:\Dreamport\Age Of Mythology\Assets\#User\SOMAOM` |
| `CL` | `E:\Somnium Space\Main Build\Assets` (Somnium client source, Unity 6000.3.21f1) |
| `ETSS` | `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Euro-Truck-Simulator-Euro-Truck-Sim\5f8816c6-ecf7-4f37-9aba-070a237e5bf6\scratchpad` (the ETS sessions' research: design docs, probes, the server-API guard) |
| `RRP` | `D:\Dreamport\Rec Room\Rec Room` (our Unity project; currently open in the user's editor; research did not touch it) |
| `SP` | `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad` |

---

## 0. Decisions in one screen

1. **Transport = Photon Fusion 2.0.12, Shared mode**, one Fusion session per Somnium world instance, which every player
   in the instance is already in. The world does not start or own it; it borrows the client's runner.
2. **The world cannot define its own networked behaviours.** World C# is compiled on Somnium's server and never woven
   by Fusion. All networking goes through pre-woven client types placed in the scene:
   `NetworkBridgeEvents` (4 byte[] RPCs) and `NetworkBridgeData{1,2,4,6,8,12,16,32,64}` (a networked `int[]` + the same
   RPCs). No `[Networked]`, no `[Rpc]`, no `NetworkBehaviour` subclass in our code.
3. **State lives in networked ints; events go by RPC.** Ints are replicated only when they change (16 Hz send), are
   held by the Photon server, and are what a late joiner reads. RPCs are reliable and ordered but are **not** replayed
   to late joiners and carry at most **512 bytes** of payload (measured).
4. **Fake the master per activity instance.** The Fusion "shared-mode master" is only the room's orphan collector.
   Each activity instance elects its own authority from the players' presence slots: **the present member with the
   lowest Fusion PlayerId** (ids grow with join order, so this is the longest-present member). It takes Fusion state
   authority over the instance's objects with `RequestStateAuthority()` (always granted: every object carries
   `AllowStateAuthorityOverride`). When it leaves, the next-lowest member takes over and continues from the replicated
   ints. This is the pattern ETS (`EtsSlotRules.ActingMaster`, `EtsTrafficAuthority`), AoM (`HostGone`) and Quake
   (`IsBotAuthority`) already use.
5. **Isolation by distance ("teleport them far away").** Each instance is built at its own origin, 2 km apart; each
   player's private dorm at its own spot 250 m apart. The client stops drawing remote avatars beyond 120 m (when its
   culling applies), voice falls silent beyond 25 m (100 m boosted), and nobody builds an instance they are not in.
   The Somnium avatar is never modified.
6. **Scene network objects (PROPOSAL, all pre-placed): 1 events object, 50 player slots (NBD64), 24 instance-state
   objects (NBD64), 12 actor blocks (NBD64), 48 physics props (NBD12) = 135 NetworkObjects.** The largest proven
   Dreamport scene holds 67 (AoM). See risks.
7. **Physics props are owner-simulated.** Grab = optimistic local hold + `RequestStateAuthority()` on the prop; the holder
   writes its pose; everyone else draws a kinematic puppet on ETS's `EtsNetTimeline` (writer-clock stamps, adaptive
   delay ~135 ms, Hermite). Held props cost nothing (attached to the holder's drawn hand by an offset in the holder's slot).
8. **Hits are decided by the shooter/thrower** ("favour the shooter", as Quake does); damage/death by the victim;
   scores by the instance authority.
9. **Budget:** worst plausible download about 25 KB/s (200 kbit/s) per client with 50 players in 5 busy games; upload
   under 1 KB/s per player (+2-3 KB/s for an instance authority running AI).
10. **Use only server-proven API members** (list in section 10). The server compiles worlds against an older Fusion:
    `NetworkRunner.GetMasterClient()` broke an ETS upload.

---

## 1. Sources studied

| What | Where |
|---|---|
| ETS networking (most mature pattern) | `ETS\SceneNetworking.cs` (591 lines), `ETS\EtsNet.cs` (2758), `ETS\EtsNetWire.cs` (493, includes `EtsNetTimeline`), `ETS\EtsPlayerWire.cs` (1784, includes `EtsSlotRules`, `EtsSlotClaim`), `ETS\EtsSessionWire.cs` (1437), `ETS\EtsTrafficWire.cs` (472, includes `EtsTrafficAuthority`), `ETS\EtsNetVehicle.cs` (575), `ETS\EtsSession.cs` (795), `ETS\EtsTraffic.cs`, `ETS\EtsRiders.cs`, `ETS\README.md` sections "Start area ... networking" (1482-1566), "Traffic" (1568-1644), "Phase 1" (1646-1826), "The upload that failed on the master" (1828-1846), "Phase 2" (1847-1968), `ETS\Editor\EtsNetSceneSetup.cs`, `ETS\Editor\EtsTrafficSceneSetup.cs` |
| ETS design + probes | `ETSS\design\session-net\DESIGN.md` (791 lines: facts F1-F16, bandwidth, risks), `ETSS\fusionapi\check.sh` + `known_good.txt` (server-API guard) |
| Community Modules plugin (world copy) | `CM\NetworkBridge.cs`, `CM\NetworkBridgeData{1,2,4,6,8,12,16,32,64}.cs`, `CM\Virtual\NetworkBridgeData.cs`, `CM\NetworkBridgeEvents.cs`, `CM\SomniumSpace.Network.asmdef` |
| Quake | `Q\Q3Link.cs` (1254: message ids, presence, heartbeat), `Q\Q3Combat.cs` (authority, `SharesGame`), `Q\Q3Remote.cs` (remote weapons, `SetVisible`), `Q\Networking\*` (Community Modules samples: `NetworkGrabbable`, `NetworkOwnershipEvent`, `TeleportNetworkRigidbody3D`, `Networked Components\NetworkEvent*.cs`, `NetworkSpawn.cs`, `Utils\NetValue.cs`, `Utils\BytesWriter.cs`, `Utils\BytesReader.cs`) |
| AoM | `AOM\Aom\AomNet.cs` (2286: host + mirrors, lobby, census over targeted RPCs, host migration), `AOM\Aom\AomWire.cs` (155) |
| Client (platform side) | `CL\_SomniumSpace\Network\Scripts\Bridge\*` (the woven originals), `CL\_SomniumSpace\Network\Scripts\Networking.cs`, `NetworkHelper.cs`, `NetworkRunnerHandler.cs`, `CL\3rdParty\Photon\Fusion\Resources\NetworkProjectConfig.fusion`, `CL\3rdParty\Photon\Fusion\build_info.txt`, `CL\3rdParty\Photon\Fusion\Assemblies\Fusion.Runtime.dll` (+ `.xml`), `CL\_SomniumSpace\Network\Prefabs\Network.Runner.prefab`, `CL\_SomniumSpace\Bridge\Scripts\Components\Player\*`, `CL\_SomniumSpace\Player\...` (culling, voice, respawn, cameras) |
| Bridge SDK (world side) | `RRP\Assets\Plugins\SomniumSpace\Bridge\SomniumSpace.Bridge.dll` + `SomniumSpace.Bridge.xml` |
| Rec Room data (for network-relevant tool settings) | rrtool over `E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64` |
| Probes written for this doc | `SP\netprobe\` (metadata constant dumper), `SP\tickprobe\` (resolves Fusion's tick/send rate from the client config) |

---

## 2. Platform facts (measured)

### 2.1 Summary table

| Question | Answer | Evidence |
|---|---|---|
| Transport | **Photon Fusion 2.0.12 Stable build 1861** (2026-03-19), **Shared mode** (`GameMode = (GameMode)ConnectionMode`, default `ConnectionMode.Shared`). Not PUN. Photon Voice (Fusion flavour) carries voice separately. | `CL\3rdParty\Photon\Fusion\build_info.txt`; `CL\_SomniumSpace\Network\Scripts\Networking.cs:105` (`ConnectionMode ... ?? ConnectionMode.Shared`), `:510-513` (`GetSessionArgs`, `gameArgs.GameMode = (GameMode)ConnectionMode`) |
| Session scope | One Fusion session per Somnium world instance; session name/properties from the instance (`SessionPropertyType.Instance/World/Session`). | `CL\_SomniumSpace\Network\Scripts\NetworkHelper.cs:84-113` |
| Player cap | Fusion `PlayerCount = connection.MaxUsers`; `MAX_PLAYERS_FOR_CUSTOM_WORLD = 50` (read by reflection by ETS probe 1671). Project config allows 100. | `NetworkHelper.cs:97`; `ETSS\design\session-net\DESIGN.md` F3; `NetworkProjectConfig.fusion:22` |
| Tick / send rate | Client config `TickRateSelection {Client 32, ServerIndex 0, ClientSendIndex 1, ServerSendIndex 1}` resolves to **tick 32 Hz, client send 16 Hz, server send 16 Hz** (ClientSendDelta 0.0625 s). Writing an int faster than 16 Hz only keeps the latest value per send. | `NetworkProjectConfig.fusion:23-28`; measured with `SP\tickprobe` calling `Fusion.TickRate.Resolve` on the client's `Fusion.Runtime.dll` |
| Replication | `ReplicationFeatures: 1` = *Scheduling*: when an object's changes do not fit the per-tick data budget, the server raises its priority for the next tick (so over budget = delayed, not lost). | `NetworkProjectConfig.fusion:19`; `Fusion.Runtime.xml:15177-15181` |
| Networked heap | `PageShift 15, PageCount 256` = 8 MB. 50 NBD64 slots are 12.5 KB of it. | `NetworkProjectConfig.fusion:61-62`; DESIGN.md F2 |
| RPC payload limit | **`Fusion.RpcAttribute.MaxPayloadSize = 512` bytes** (a byte[] argument also carries its length). Existing worlds cap messages at 400-500 B (ETS news 400, Quake text 480, AoM census chunks 500). | measured with `SP\netprobe` (metadata of `CL\3rdParty\Photon\Fusion\Assemblies\Fusion.Runtime.dll`); `AOM\Aom\AomNet.cs:33`, `:1110`; `ETS\EtsNet.cs:350`; `Q\Q3Link.cs` (text `> 480` checks) |
| RPC reliability / ordering | The Bridge RPCs use the attribute defaults: `Channel = Reliable` (order preserved, delivery verified, resent), `InvokeLocal = true`, `TickAligned = true` ("Even if set to false, the order of Rpcs is always preserved"). RPC header = 8 bytes (`RpcHeader.SIZE`). | `CM\NetworkBridgeEvents.cs:32,45,52,53`; `Fusion.Runtime.xml:12790-12830`; `SP\netprobe`: `RpcChannel.Reliable = 0`, `RpcHeader.SIZE = 8` |
| RPC targets | `RPC_SendMessageToAll` (sources All, targets All: every client **including the sender, invoked locally at once**); `RPC_SendMessageToProxies` (everyone except the object's state authority); `RPC_SendMessageToController` (the object's state authority only); `RPC_SendMessageToPlayer([RpcTarget] PlayerRef, byte[])` (one player). | `CM\NetworkBridgeEvents.cs:31-57`; the "comes back to the sender" behaviour is relied on by `ETS\EtsNet.cs:348-349` and `Q\Q3Link.cs:1214-1219` |
| RPC sender identity | **Not delivered.** The Bridge events are `Action<byte[]>`; the `RpcInfo` is dropped. Every payload must carry its sender's PlayerId (ETS news does: `EtsNet.cs:350-357`). | `CM\NetworkBridgeEvents.cs:15-18,33-57` |
| Persistent world state for late joiners | Yes: networked ints/bytes of scene objects are part of Fusion's shared state (held by the Photon server); a late joiner receives current values in its join snapshot. RPCs are not stored. Snapshot may arrive after `IsValid`: ETS waits 1.5 s before trusting zeros. | ETS F11 (a late joiner read the truck straight out of the array); `ETS\EtsNetWire.cs:12-15`; DESIGN.md risk 8 |
| Player identity | Fusion `PlayerRef.PlayerId` (int; **session-scoped, grows with every (re)join**: the same user rejoined as Player 3 after Player 2). Bridge `ISomniumPlayerProperties.PhotonId == PlayerId` (`_network.Creator.PlayerId`), `Id` = stable account UserId (string), `NickName`, `IsLocal`. | `CL\_SomniumSpace\Bridge\Scripts\Components\Player\BridgePlayerProperties.cs:9-12`; DESIGN.md F10, F11 |
| Join/leave callbacks | World `SceneNetworking` (a world script implementing `INetworkRunnerCallbacks`): `OnNetworkRunnerCreated`, `OnLocalPlayerJoined`, `OnBecomeWorldMaster`, `OnOtherPlayerJoined(PlayerRef)`, `OnOtherPlayerLeft(PlayerRef)`, `OnReliableMessageReceived(ReliableKey, byte[])`; statics `IsNetworkReady`, `IsConnected`, `IsDebugMode`, `IsMasterClient`, `NetworkRunnerRef`. Bridge: `PlayersContainer.OnPlayerAdded` (proven), `OnPlayerRemoved`, `OnLocalPlayerAdded`. | `ETS\SceneNetworking.cs:18-23,49-64,267-314` |
| "Master" concept | Fusion shared-mode master client (`runner.IsSharedModeMasterClient`); first player in; moves by itself when that player leaves. Our `SceneNetworking` makes it request authority of every object whose `StateAuthority.IsNone`, every 1 s and on every leave. That is its only job. `GetMasterClient()` exists locally but **not on the server** (upload failed with CS1061). | `ETS\SceneNetworking.cs:55-64,225-247,298-314`; `ETS\README.md:1828-1846`; `Q\Q3Combat.cs:2929-2937` |
| Authority semantics | Per NetworkObject. Scene objects use `Flags 524289 = V1 (1) | AllowStateAuthorityOverride (524288)`: any client's `RequestStateAuthority()` is granted, the **last request wins**; writes by a non-authority are dropped (overwritten). A leaver's objects become `StateAuthority.IsNone` (they are not destroyed: `DestroyWhenStateAuthorityLeaves` is not set). `IStateAuthorityChanged` raises `OnStateAuthorityChanged(bool)` on gain and loss. | `ETS\SceneNetworking.cs:26`; `SP\netprobe` flag values; `ETS\EtsPlayerWire.cs:1240-1243` (probe 2200's model); `CM\NetworkBridgeEvents.cs:60-63` |
| Interest | Every Somnium and Dreamport NetworkObject is `ObjectInterest: 1` = Global (`ObjectInterestModes.AreaOfInterest=0, Global=1, Explicit=2`). All clients receive all objects. | `SP\netprobe`; e.g. `CL\_SomniumSpace\Player\Prefabs\NEW\Player.XR.Part.Network.prefab:861`; `ETSP\Assets\Euro Truck Scene.unity` (slot objects) |
| Physics addon on the client runner | `RunnerSimulatePhysics3D` with `_physicsAuthority: 1` (Unity auto-simulates), `_physicsTiming: 0` (FixedUpdate), `ClientPhysicsSimulation: 0` (Disabled: proxies are interpolated, non-physical). | `CL\_SomniumSpace\Network\Prefabs\Network.Runner.prefab:76-89` |
| Runtime spawning | Possible through `SceneNetworking._networkPrefabs` + `runner.Spawn(prefabId, pos)` (Community Modules `NetworkSpawn`), but it writes into the client's global prefab table ("DANGER ... COULD BREAKE SOMNIUM") and no Dreamport world has run it. **Do not use.** | `ETS\SceneNetworking.cs:111-160`; `Q\Networking\Networked Components\NetworkSpawn.cs:1-27`; DESIGN.md F6 |
| Join timing | Runner ready 5.78 s and local join 6.49 s after the world scene loaded (first player became master). Slot claim then takes about 1 s. | DESIGN.md F11 (Player.log:879-947); `ETS\EtsNet.cs:554` |
| Observed remote delay | ETS's adaptive timeline drew remote trucks 117-228 ms behind, typically ~135 ms. | DESIGN.md F11 |
| Avatar culling | Remote avatars are sorted into visibility layers by camera distance: `{15 m/10 players, 30/10, 60/10, 120/50, 200/100}`; the **last layer means culled**, so beyond 120 m (or out of frustum) an avatar is culled, **when** the avatar culling path applies (see `AvatarFeature.ActivateCulling`, which returns early for avatars without LODs when the `AVATARS_LODS_ENABLE` setting is on). Player camera far clip = 10,000 m. | `CL\_SomniumSpace\Player\Prefabs\NEW\Player.Logic.prefab:78-88`; `CL\_SomniumSpace\Player\Scripts\Player\Features\Culling\FrustumCullingManager.cs:156-181`; `CL\_SomniumSpace\Player\Scripts\Components\Features\Common\AvatarFeature.cs:916-928`; `CL\_SomniumSpace\Player\Prefabs\NEW\XR\Player.XR.Body.prefab:2088`, `...\NonXR\Body\CameraSystem.prefab:830` |
| Voice range | Remote voice `AudioSource.maxDistance` = 25 m normal, 100 m boosted. | `CL\_SomniumSpace\Network\Scripts\Voice\VoiceSpeaker.cs:24-25,113` |
| Fall respawn | The client teleports a player whose head goes below the world's respawn height back to the respawn point (after 3 tries: free-fly). Keep every floor above it. | `CL\_SomniumSpace\Player\Scripts\Components\Features\Common\BodyFeature.cs:172-179,219-241` |
| Teleport | `ISomniumPlayerMotion.DoTeleportToPoint(Vector3 position, Vector3 rotation /*Euler, pass (0,yaw,0)*/, Action done)`; the Bridge moves the player at end of frame and calls back then (NonXR) or a frame later (XR). | `CL\_SomniumSpace\Bridge\Scripts\Components\Player\BridgePlayerMotion.cs:19-22`; `ETS\EtsSessionWire.cs:911-915`; `ETS\EtsVr.cs:239`; `ETS\EtsSession.cs:627` |
| Remote hands | `ISomniumPlayerBody.Root/Head/Neck/LeftHand/RightHand` are the player's tracked body transforms (`PlayerBodyHolder`), the same transforms the client networks (`Body/Root`, `Body/Head`, `Body/LeftHand`, `Body/RightHand` each carry a Fusion behaviour with the player's state authority). | `CL\_SomniumSpace\Bridge\Scripts\Components\Player\BridgePlayerBody.cs:9-13`; `CL\_SomniumSpace\Player\Scripts\Components\Features\Common\BodyFeature.cs:73-108,125`; `ETS\EtsRiders.cs:1034-1065` |
| Bridge network API | `SomniumBridge.Network` has only status, `IsConnected`, `IsHost` and 4 connection events; **no messaging**. The Community Modules objects are the only data channel. | `RRP\Assets\Plugins\SomniumSpace\Bridge\SomniumSpace.Bridge.xml` (`T:SomniumSpace.Bridge.Network.ISomniumNetwork` members) |
| Headless host client | The client has a `HOST_CLIENT` path (a server bot) only for `WorldType.Main`; custom worlds have none. Still, never assume the Fusion master has Rec Room installed. | `CL\_SomniumSpace\Network\Scripts\Networking.cs:1-2`; `NetworkHelper.cs:199-209` |

### 2.2 How the world's C# is compiled (the most important constraint)

* The world assembly is uploaded as source and compiled **on Somnium's server against the server's own DLLs**, which
  include an **older Fusion** than 2.0.12 build 1861. A clean local build proves nothing: ETS's upload failed with
  `CS1061: 'NetworkRunner' does not contain a definition for 'GetMasterClient'` (`ETS\README.md:1828-1846`).
* The world assembly is **never woven** by Fusion, so a world class cannot be a `NetworkBehaviour` with `[Networked]`
  properties or `[Rpc]` methods (`ETS\EtsNet.cs:20-22`; `AOM\Aom\AomNet.cs:24-29`). The client weaves
  `SomniumSpace.Network` (`CL\3rdParty\Photon\Fusion\Resources\NetworkProjectConfig.fusion:65-69`), so the
  `NetworkBridge*` types placed in our scene run as the client's woven copies. Their **script GUIDs must match the
  client's** (they do when copied from ETS/AoM; section 3.4).
* Guard: `ETSS\fusionapi\check.sh` dumps every `Fusion.*`/`SomniumSpace.*` member a built DLL references (with
  `ETSS\fusionapi\RefDump.dll`, run by Unity's bundled `dotnet.exe`) and prints any not in `ETSS\fusionapi\known_good.txt`
  (members used by worlds that passed the server compile). The full list is reproduced in section 10. Anything outside it
  is **unproven** and must be avoided or proven by a test upload.

### 2.3 Measured Fusion constants (from `Fusion.Runtime.dll` metadata, `SP\netprobe`)

```
Fusion.NetworkObjectFlags: None 0, V1 1, Ignore 65536, MasterClientObject 131072,
                           DestroyWhenStateAuthorityLeaves 262144, AllowStateAuthorityOverride 524288
Fusion.RpcAttribute.MaxPayloadSize = 512
Fusion.RpcChannel: Reliable 0, Unreliable 1
Fusion.RpcHeader.SIZE = 8
Fusion.RpcSources/RpcTargets: StateAuthority 1, InputAuthority 2, Proxies 4, All 7
Fusion.NetworkObject+ObjectInterestModes: AreaOfInterest 0, Global 1, Explicit 2
Fusion.TickRate.Resolve({Client 32, ServerIndex 0, ClientSendIndex 1, ServerSendIndex 1})
   -> Client 32, ClientSend 16, Server 32, ServerSend 16 (ClientSendDelta 0.0625 s)
```

Re-run: `dotnet SP\netprobe\out\netprobe.dll "<dll>" <Type.FullName ...>`; `dotnet SP\tickprobe\out\tickprobe.dll
"CL\3rdParty\Photon\Fusion\Assemblies" "C:\Program Files\Unity\Hub\Editor\6000.3.5f2\Editor\Data\Managed\UnityEngine"
"C:\Program Files\Unity\Hub\Editor\6000.3.5f2\Editor\Data\Managed"`.

---

## 3. The building blocks (API reference)

### 3.1 `NetworkBridgeEvents` (the RPC channel), exact source

```csharp
// CM\NetworkBridgeEvents.cs:8-76 (identical to the client's CL\_SomniumSpace\Network\Scripts\Bridge\NetworkBridgeEvents.cs except one comment)
public class NetworkBridgeEvents : NetworkBehaviour, IStateAuthorityChanged
{
    public event Action OnSpawned;  // Called when the object is spawned in the network
    public event Action<NetworkRunner, bool> OnDespawned;
    public event Action OnFixedUpdateNetwork;  // once per simulation step (State Authority)
    public event Action OnRender;
    public event Action<bool> OnStateAuthorityChanged; // gain or loss
    public event Action<byte[]> OnMessageToAll;        // All clients including the state authority owner
    public event Action<byte[]> OnMessageToProxies;    // all clients except the state authority owner
    public event Action<byte[]> OnMessageToController; // the state authority owner
    public event Action<byte[]> OnMessageToPlayer;     // a specific player

    public override void Spawned() { OnSpawned?.Invoke(); }
    public override void Despawned(NetworkRunner runner, bool hasState) { OnDespawned?.Invoke(runner, hasState); }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendMessageToAll(byte[] message, RpcInfo info = default) { OnMessageToAll?.Invoke(message); }

    [Rpc(RpcSources.All, RpcTargets.Proxies)]
    public void RPC_SendMessageToProxies(byte[] message, RpcInfo info = default) { OnMessageToProxies?.Invoke(message); }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SendMessageToController(byte[] message, RpcInfo info = default) { OnMessageToController?.Invoke(message); }

    [Rpc(sources: RpcSources.All, targets: RpcTargets.All)]
    public void RPC_SendMessageToPlayer([RpcTarget] PlayerRef player, byte[] message) { OnMessageToPlayer?.Invoke(message); }

    public void StateAuthorityChanged() { OnStateAuthorityChanged?.Invoke(Object.HasStateAuthority); }
    public override void FixedUpdateNetwork() { OnFixedUpdateNetwork?.Invoke(); }
    public override void Render() { OnRender?.Invoke(); }
}
```

Usage rules (all learned in the earlier worlds):

* Guard every call: `behaviour != null && behaviour.Object != null && behaviour.Object.IsValid` (`ETS\EtsNet.cs:185-188`).
  Objects exist from scene load but are only valid once the room is joined (`Q\Q3Link.cs:163-168`).
* Never send `null` or an empty array (Community Modules samples pad to `{0}`: `Q\Networking\Networked Components\NetworkEvent.cs:40-41`; `NetworkEventInt.cs:95-99` logs an error on empty).
* Put the **sender's PlayerId** and a **message type byte** in every payload (the handlers get no `RpcInfo`).
* Proven events: `OnMessageToAll/Proxies/Controller/Player`, `OnSpawned`, `OnStateAuthorityChanged`. `OnRender`,
  `OnFixedUpdateNetwork`, `OnDespawned` are **not** in the proven list: poll from `Update` instead (all earlier worlds do).
* `RPC_SendMessageToController` goes to whoever holds the object's state authority; with none, it is lost. Use it only
  on an object whose authority you know is live (for example our instance object, whose holder is the instance authority).
* `RPC_SendMessageToPlayer` needs a `PlayerRef`. The proven way to get one is to scan `runner.ActivePlayers` for the
  PlayerId (`AOM\Aom\AomNet.cs:1660-1675`) or to read `someObject.Object.StateAuthority`.

### 3.2 `NetworkBridgeData*` (networked ints)

```csharp
// CM\Virtual\NetworkBridgeData.cs:7-11
public class NetworkBridgeData : NetworkBridgeEvents
{
    public virtual NetworkArray<int> IntArray { get; }
    public virtual int IntCount { get; }
}
// CM\NetworkBridgeData64.cs:7-13  (the 1/2/4/6/8/12/16/32 variants differ only in Capacity and IntCount)
public class NetworkBridgeData64 : NetworkBridgeData
{
    // Total 256 Bytes or memory
    [Networked, Capacity(64)] private NetworkArray<int> SyncIntArray => default;
    public override NetworkArray<int> IntArray => SyncIntArray;
    public override int IntCount => 64;
}
```

* Read: `data.IntArray.Get(i)`; write: `data.IntArray.Set(i, v)` (only with `data.Object.HasStateAuthority`); length:
  `data.IntArray.Length`. All three are proven.
* A data object **is also** an events object (it inherits the four RPCs), so a per-instance object can carry both the
  instance's state and its "to the authority" channel.
* Only changed ints are sent. Write only ints that differ from what the array holds (ETS `Push`, section 4.2).
* Legacy `NetworkBridge` (`CM\NetworkBridge.cs:9-69`: `SyncByteArray[512]`, `SyncIntArray[8]`, ... and RPCs with a
  `byte messageID` argument) is marked "LEGACY - DO NOT USE ANYMORE". Quake and AoM used it; **we do not**.

### 3.3 `SceneNetworking` (world script, copy the ETS version)

Copy `ETS\SceneNetworking.cs` (it contains two fixes the Quake/AoM copies lack), change only the namespace to
`SomniumSpace.Worlds.SomniumRecRoom`. Key parts:

```csharp
// ETS\SceneNetworking.cs:18-26
public static event Action OnNetworkRunnerCreated;
public static event Action OnLocalPlayerJoined;          // local player has joined
public static event Action OnBecomeWorldMaster;          // local player became shared-mode master
public static event Action<PlayerRef> OnOtherPlayerJoined;
public static event Action<PlayerRef> OnOtherPlayerLeft;
public static event Action<ReliableKey, byte[]> OnReliableMessageReceived;
public const NetworkObjectFlags NETWORK_OBJECT_DEFAULT_FLAGS = NetworkObjectFlags.V1 | NetworkObjectFlags.AllowStateAuthorityOverride;

// ETS\SceneNetworking.cs:49-64
public static SceneNetworking Instance { get; private set; }
public static NetworkRunner NetworkRunnerRef { get; private set; }
public static bool IsNetworkReady { get; private set; } = false; // local player has joined
public static bool IsConnected => NetworkRunnerRef != null && NetworkRunnerRef.IsRunning; // ETS fix: was "!= null ||"
public static bool IsDebugMode => !IsConnected;
public static bool IsMasterClient => NetworkRunnerRef != null && NetworkRunnerRef.IsSharedModeMasterClient;

// ETS\SceneNetworking.cs:162-175: polls every 50 ms for NetworkRunner.Instances[0].IsRunning, then AddCallbacks(this).
// ETS\SceneNetworking.cs:189-200 (ETS fix): if the local join happened before the callbacks were added, catch it up.
// ETS\SceneNetworking.cs:212-223: collects every NetworkObject in the scene in Awake (sorted by SortKey) and calls
//   runner.RegisterSceneObjects(SceneRef.FromPath(scene.path), objects) when the local player joins.

// ETS\SceneNetworking.cs:225-247  (the "master" does exactly this and nothing else)
private void ReassignNullObjectsAuthority()
{
    foreach (NetworkObject obj in _sceneNetworkObjects)
        if (obj != null && obj.StateAuthority.IsNone)
            obj.RequestStateAuthority();
}
private void SlowLoop()            // InvokeRepeating 1 s after the local join
{
    if (IsMasterClient) ReassignNullObjectsAuthority();
    UpdateMasterClientState();     // raises OnBecomeWorldMaster on change
}
// ETS\SceneNetworking.cs:298-314: OnPlayerLeft -> (if _autoTransferObjectsAuthority && IsMasterClient) ReassignNullObjectsAuthority(); OnOtherPlayerLeft(player)
```

* `OnDestroy` (`:82-94`) nulls every static event: subscribers must subscribe again on each world visit (statics outlive
  a visit: the world assembly is loaded once per client session, `ETS\EtsNet.cs:160-172`).
* Scene field `_autoTransferObjectsAuthority` is 1 in ETS. For Rec Room set it **0** (PROPOSAL): our own rules adopt
  orphans (section 5.4), and the master's instant grab on every leave only races the elected successor. The 1 Hz
  `SlowLoop` orphan grab stays (harmless: it only touches objects nobody holds).
* Do not touch `RegisterNetworkPrefabs`/`RestorePrefabTable` (`:111-160`); leave `_networkPrefabs` empty.

### 3.4 Scene object recipe (exactly what ETS/AoM put in their scenes)

Each network object is a GameObject with two components:

| Component | Script reference (must be exactly this) | Serialized values |
|---|---|---|
| `Fusion.NetworkObject` | `{fileID: -1552182283, guid: e725a070cec140c4caffb81624c8c787, type: 3}` (inside `Fusion.Runtime.dll`, same GUID in client and ETS) | `SortKey` (baked by Fusion's editor on scene save), `ObjectInterest: 1`, `Flags: 524289`, `NestedObjects: []`, `NetworkedBehaviours: [ {fileID of the data component} ]`, `ForceRemoteRenderTimeframe: 0` |
| `NetworkBridgeData64` | guid `0b8147e129a49fd49ba7bdc9dc75daaa` | none |
| `NetworkBridgeData12` | guid `b9da383ae417d8d49822395dc0037d77` | none |
| `NetworkBridgeData8` | guid `763ab0b6abbf99842b746e43535cc896` | none |
| `NetworkBridgeData16` / `32` | `d16fbf991cc3bbc45ba471b20fc1642c` / `4186952287220d44cad3d65df203df41` | none |
| `NetworkBridgeData1/2/4/6` | `b732b36a0f3fb5b419a191381c61f517` / `5392ff6614ff6a249b574b006eab71eb` / `600a00e8478d7ff49b449c5206e1f16a` / `e67a5587f84aae342a807972a8e0f4d6` | none |
| `NetworkBridgeEvents` | guid `6ba26196d4959fb45a90fa1113c622e9` | none |
| (legacy `NetworkBridge`, do not use) | guid `28732b03e6f75884696d521673a4d2b4` | |

Example as saved by ETS (`ETSP\Assets\Euro Truck Scene.unity:2005-2064`, "Ets Net Player 0"):

```yaml
--- !u!114 &2900000202
MonoBehaviour:
  m_GameObject: {fileID: 2900000200}
  m_Script: {fileID: -1552182283, guid: e725a070cec140c4caffb81624c8c787, type: 3}
  m_EditorClassIdentifier: Fusion.Runtime.dll::Fusion.NetworkObject
  SortKey: 879129613
  ObjectInterest: 1
  Flags: 524289
  NestedObjects: []
  NetworkedBehaviours:
  - {fileID: 2900000203}
  ForceRemoteRenderTimeframe: 0
--- !u!114 &2900000203
MonoBehaviour:
  m_GameObject: {fileID: 2900000200}
  m_Script: {fileID: 11500000, guid: 0b8147e129a49fd49ba7bdc9dc75daaa, type: 3}
  m_EditorClassIdentifier: SomniumSpace.Network::SomniumSpace.Network.Bridge.NetworkBridgeData64
```

Make them with an idempotent editor menu, like `ETS\Editor\EtsTrafficSceneSetup.cs:26-108`: create the GameObject,
`AddComponent<NetworkObject>()`, `networkObject.Flags = SceneNetworking.NETWORK_OBJECT_DEFAULT_FLAGS`, set
`ObjectInterest` to 1 through `new SerializedObject(networkObject).FindProperty("ObjectInterest").intValue = 1`
(the field is internal), `AddComponent<NetworkBridgeData64>()`, then `EditorSceneManager.SaveScene` (Fusion bakes
`SortKey` and `NetworkedBehaviours` on save, `NetworkObjectPostprocessor.OnSceneSaving`). Objects are found at run time
**by name** (section 4.1), so names are the contract.

### 3.5 What the Rec Room project needs before any of this compiles (PROPOSAL, from ETS README:1510-1512)

`RRP` currently has **no** Photon/Fusion and no Community Modules networking (checked: no `NetworkBridge*.cs`, no
`build_info.txt` under `RRP\Assets`). ETS copied them from AoM "with their GUIDs". Copy from `ETSP\Assets`, keeping
every `.meta`:

1. `Assets\Photon\Fusion\**` (Fusion 2.0.12 build 1861; `Fusion.Runtime.dll.meta` guid `e725a070cec140c4caffb81624c8c787`; `Runtime\Fusion.Unity.asmdef.meta` guid `1baf1ffff1031e948b3b228ab3453251`).
2. `Assets\Photon\PhotonLibs\**`.
3. `Assets\#User\Community Modules\Plugins\SomniumSpace\Networking\**` (asmdef `SomniumSpace.Network`, guid `58de1d31e4aad8848b2afdda4b94c61b`).
4. `ETS\SceneNetworking.cs` into `Assets\#User\SomniumRecRoom\` with namespace `SomniumSpace.Worlds.SomniumRecRoom` (new GUID is fine: it is a world script).
5. The world's own code references `SomniumSpace.Network` and `Fusion.Unity` (ETS asmdef: `ETS\SomniumEuroTruck.asmdef`
   references `Unity.TextMeshPro, UnityEngine.UI, Unity.XR.Interaction.Toolkit, SomniumSpace.Network, Fusion.Unity,
   URP Runtime, RP Core Runtime`). `RRP\Assets\#User\SomniumRecRoom\Rr\*.cs` currently has no asmdef (namespace
   `SomniumSpace.Worlds.SomniumRecRoom.Rr`); if one is added, name it after the world as the others do.
6. `Fusion.Addons.Physics` is **not** needed (we do not use `NetworkRigidbody3D`; section 5.5.1).
7. The project's own `NetworkProjectConfig.fusion` only matters in the editor (ETS's has `AssembliesToWeave` including
   `SomniumSpace.Network` and a 64 Hz tick); at run time the client's config (section 2.1) applies.

### 3.6 Bridge calls used by networking (all proven unless marked)

| Call | Use |
|---|---|
| `SomniumBridge.PlayersContainer.LocalPlayer`, `.AllPlayers` | local player; all players. `AllPlayers` **copies a List on every call** (`CL\_SomniumSpace\Bridge\Scripts\Components\Player\BridgePlayersContainer.cs:19-28`): cache at 1 Hz and on join/leave. |
| `PlayersContainer.OnPlayerAdded` (+`remove_`) | a Bridge player appeared (it may appear later than the Fusion join). |
| `player.Properties.PhotonId / Id / NickName / IsLocal` | Fusion PlayerId / stable UserId / display name. |
| `player.References.Body.Root/Head/Neck/LeftHand/RightHand` | anchor points for held items, hit volumes, nameplates. |
| `player.Features.Motion.DoTeleportToPoint(pos, new Vector3(0, yaw, 0), done)` | moving the local player between dorm, hub and instances. |
| unproven: `PlayersContainer.GetPlayerByPhotonID(int)`, `OnPlayerRemoved`, `ISomniumPlayerInfo.SetPlayerTagDisableState(bool)`, `ISomniumPlayerVoice.SetVoiceLevel(float)`, `EnableAmplify(float)` | exist in the SDK (`SomniumSpace.Bridge.xml`) but no world that passed the server compile used them. Prefer the proven equivalents. |

### 3.7 Community Modules helpers: what to reuse and what not

| Helper | Verdict |
|---|---|
| `NetValue` (`Q\Networking\Utils\NetValue.cs`) | `GetInt/SetInt/GetFloat/SetFloat` are fine. **`SetByte` and `SetShort` are broken**: they cast the merged int to `short` (`:65`, `:86`), destroying the upper bits. Do not use them; use ETS-style packing (section 4.9). |
| `BytesWriter/BytesReader` (`Q\Networking\Utils\`) | Fixed-size buffers (`BytesWriter(int bytesSize)`; `Validate` refuses to grow, `:301-310`). Usable if sized exactly. |
| `NetworkEvent` (`Q\Networking\Networked Components\NetworkEvent.cs:38-48`) | **Broken for payloads longer than 1 byte**: it allocates `new BytesWriter(2)` then `AddByteArray(data)`, which fails validation and sends `[id, 0]`. Do not use. |
| `NetworkEventInt` (`...\NetworkEventInt.cs:54-116`) | Useful pattern (RPC to all + the authority mirrors the value into an int for late joiners, initial value written on spawn by the authority). Re-implement in our code. |
| `NetworkGrabbable` (`Q\Networking\NetworkGrabbable.cs:69-130`) | The grab-transfers-authority pattern (XRI `firstSelectEntered` -> `RequestStateAuthority`, 2 s timeout). Pattern reused in 5.5; the component itself needs `NetworkRigidbody3D`, which we avoid. |
| `NetworkSpawn` | Do not use (runtime spawning, 2.1). |

---

## 4. Patterns from the existing projects (copy these)

### 4.1 Find objects by name, once a second, until all are found

```csharp
// ETS\EtsNet.cs:592-647 (abridged)
foreach (NetworkBridgeData data in FindObjectsByType<NetworkBridgeData>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    string name = data.name;
    if (!name.StartsWith(EtsPlayerWire.ObjectPrefix, StringComparison.Ordinal) ||
        !int.TryParse(name.Substring(EtsPlayerWire.ObjectPrefix.Length), out int k) || k < 0 || k >= MaxSlots)
        continue;
    if (data.IntCount < EtsPlayerWire.Used) { Debug.LogWarning(...); continue; }   // wrong NBD size in the scene
    _slotObjects[k] = data;
}
// events object: must exclude NetworkBridgeData (it derives from NetworkBridgeEvents)
// ETS\EtsNet.cs:411-416
foreach (NetworkBridgeEvents events in FindObjectsByType<NetworkBridgeEvents>(FindObjectsInactive.Include, FindObjectsSortMode.None))
    if (events.name == EventsObjectName && !(events is NetworkBridgeData))
        _events = events;
```

AoM does the same for "Aom Net Slots n" and sorts by the number, not the text (`AOM\Aom\AomNet.cs:367-432`).

### 4.2 Write only changed ints, only with authority

```csharp
// ETS\EtsNet.cs:1398-1416
private void Push(NetworkBridgeData data, int[] record)
{
    if (!Valid(data) || !data.Object.HasStateAuthority)
        return;
    NetworkArray<int> ints = data.IntArray;
    int n = Math.Min(ints.Length, record.Length);
    for (int i = 0; i < n; i++)
        if (ints.Get(i) != record[i])
        {
            ints.Set(i, record[i]);
            _wroteInts++;
        }
}
```

Write rate cap: `WriteEvery = 1f / 30f` (`ETS\EtsNet.cs:50`), knowing Fusion sends at 16 Hz. A record carries
`seq` (bumped per motion sample) and `stamp` (the writer's `Time.fixedTimeAsDouble` in ms) so readers can dedup and
place samples on the writer's clock (`ETS\EtsNetWire.cs:18-20`).

### 4.3 Read: magic, then changed ints into a local mirror

```csharp
// ETS\EtsNet.cs:717-742 (abridged)
NetworkArray<int> ints = data.IntArray;
int magic = ints.Get(EtsPlayerWire.AtMagic);
if (magic != EtsPlayerWire.Magic) { /* unwritten or another build's layout: treat as empty */ continue; }
for (int i = 0; i < n; i++) { int v = ints.Get(i); if (v != slot[i]) { slot[i] = v; _readInts++; } }
```

Every layout starts with a **magic** int (e.g. `'ETP1' = 0x45545031`) and, where versions may coexist during an upload,
a version field (`ETS\EtsSessionWire.cs:120-124`: magic and version must match, else "unwritten").

### 4.4 Present players, cheaply

```csharp
// ETS\EtsNet.cs:650-676
if (Time.unscaledTime < _presentAt) return;
_presentAt = Time.unscaledTime + PresentEvery;          // 1 s, and reset to 0 by OnOtherPlayerJoined/Left
_present.Clear();
foreach (PlayerRef player in Runner.ActivePlayers)      // an iterator: allocates on every walk
    _present.Add(player.PlayerId);
```

Subscriptions: `SceneNetworking.OnOtherPlayerJoined += OnRoomPlayersChanged; SceneNetworking.OnOtherPlayerLeft += OnRoomPlayersChanged;` (`ETS\EtsNet.cs:136-139`).

### 4.5 Per-player slot claim (the presence mechanism to reuse)

`ETS\EtsPlayerWire.cs:1254-1560`, `EtsSlotClaim`, pure and tested (probe 2200: three simulated clients, "last authority
request wins, writes without authority dropped, a delay on everything"):

```
Idle -> Asking      first free slot from LocalId % 50 on (FreeFor: unwritten, written free, owner not present, or already mine)
Asking -> Confirming  authority arrived within AuthorityWait (3 s): write the claim record (generation + 1)
Confirming -> Owned   ConfirmAfter (1 s) later the slot still names me and I still hold authority
  another present player named there -> "lost", try next slot
  authority lost while the record names me -> ask again after LostPatience (1.5 s)
Owned: authority lost -> re-ask once a second after 1.5 s; another present owner named -> start again
Leaving sends nothing: a slot whose owner is not in ActivePlayers is free (FreeFor, :1010-1019); whoever holds its
authority may write it free (ToFree, :1059-1070).
Constants: AuthorityWait 3.0, ConfirmAfter 1.0, AskEvery 1.0, LostPatience 1.5, RoundPause 5.0 (:1256-1261)
```

The owner id is packed as `PlayerId + 1` in **16 bits** (`ETS\EtsPlayerWire.cs:243,271-274`). PlayerIds grow with
churn: never pack them in a byte.

### 4.6 Authority election and migration (three proven variants)

**Quake (simplest):** the Fusion master runs the bots.

```csharp
// Q\Q3Combat.cs:2936-2937
public bool IsBotAuthority => !SceneNetworking.IsNetworkReady || SceneNetworking.IsMasterClient;
```
and on gaining it, wake every remote-driven bot so it thinks for itself again (`Q\Q3Combat.cs:3248-3290`).
Weakness: the master may be in another activity or have no game; not usable per instance.

**ETS acting master (deterministic from slots):** `EtsSlotRules.ActingMaster` (`ETS\EtsPlayerWire.cs:1134-1188`): if
the room master has no game, "the present player with the game whose slot is claimed and whose PlayerId is lowest acts
for them: Fusion's ids grow with the join order, so that is the longest-present one, and every client reaches the same
answer from the same slots." The acting master then `RequestStateAuthority()` on the session object.

**ETS traffic runner (liveness by heartbeat, takeover queue):** `ETS\EtsTrafficWire.cs:231-472`
(`EtsTrafficAuthority`), driven by `ETS\EtsTraffic.cs:655-692`:

```
HeartbeatSeconds 1   the runner rewrites every block at least once a second (seq + stamp)
StaleSeconds     4   a block unchanged this long has lost its runner: watchers drop its cars
TakeoverSeconds  3   no block changed this long: first client in line asks for all blocks
TakeoverStep   1.5   + 1.5 s per present player with a lower PlayerId that holds no block
ContestSeconds   2   a lower PlayerId that took a block this recently: a higher one stands aside
Nobody takes blocks from a runner that is writing them; a runner that finds another writing its blocks stands down.
```
The holder's id comes from the object: `PlayerRef holder = block.Object.StateAuthority;` (`ETS\EtsTraffic.cs:747`).

**AoM host migration (explicit successor):**

```csharp
// AOM\Aom\AomNet.cs:875-908 (abridged): the lowest remaining seat takes over
if (Stage != Phase.Playing || Playing != Role.Mirror || leaver != PeekInt(AtHostId) || MySeat <= 0) return;
for (int i = 0; i < Seats; i++)
    if (_seats[i].Taken && _seats[i].PlayerId != leaver && _seats[i].PlayerId != LocalId && i + 1 < MySeat)
    { Say("The host left; " + _seats[i].Name + " takes over."); return; }
_takingOver = true;
_lobby.Object.RequestStateAuthority(); _events.Object.RequestStateAuthority();
foreach (NetworkBridgeData slot in _slots) slot.Object.RequestStateAuthority();
// AOM\Aom\AomNet.cs:937-943: OnStateAuthorityChanged(true) && _takingOver -> TakeOver()
```

### 4.7 Messages: id byte, sender id, heartbeat, targeted bulk

```csharp
// ETS\EtsNet.cs:337-359 (Announce) -- type byte + 4-byte sender id + body, to all
byte[] body = Encoding.UTF8.GetBytes(text);
int length = Math.Min(body.Length, 400);
var message = new byte[5 + length];
int from = LocalId;
message[0] = NewsText;
message[1] = (byte)from; message[2] = (byte)(from >> 8); message[3] = (byte)(from >> 16); message[4] = (byte)(from >> 24);
Array.Copy(body, 0, message, 5, length);
_events.RPC_SendMessageToAll(message);
// ETS\EtsNet.cs:361-376 (OnNews): check length and type, decode from, ignore our own (it comes back to us)
```

Quake's catalogue (`Q\Q3Link.cs:34-80`) shows the useful split: continuous **state** messages rate-limited by change and
a heartbeat (`_activeRate` 10/s while acting, `_heartbeat` 2 s when idle, immediate when a new player is seen:
`:200-262`), rare **events** (hit, death, frag, item taken), and **presence** ("which match I am in", sent even when
solo so others can hide you: `:743-785`). AoM sends bulk state to one late joiner as a burst of `RPC_SendMessageToPlayer`
chunks of at most 500 bytes (`AOM\Aom\AomNet.cs:1099-1165`, the 500-byte check at `:1110`).

### 4.8 Interpolation: `EtsNetTimeline` (reuse as is)

`ETS\EtsNetWire.cs:221-481`, pure C#. `Add(state, now)` (`:304-354`) places samples on the writer's stamp, dedups by
`Seq`, resets on a new `Epoch` or a jump > 50 m (snap, not slide). `Sample(now, out state)` (`:356-463`) draws at a
render lag steered toward "best recent arrival + spread + 1.1 send intervals + 5 ms", clamped to
`MinDelay 0.03 .. MaxDelay 0.4` s, changing at most 0.05 s per s; cubic Hermite through positions and velocities
(`:465-474`), Nlerp for rotations, extrapolates up to `MaxExtrapolation 0.25` s past the newest sample, `LeadIn` option
so a newly appearing moving object arrives already moving. Measured in two-player runs: 117-228 ms, ~135 ms typical.
Its sample type is `EtsNetWire.State`; for Rec Room make a copy with our own state struct (position, rotation, velocity,
angular velocity, epoch, seq, stamp).

### 4.9 Packing helpers (reuse)

```csharp
// ETS\EtsPlayerWire.cs:772-797
public static int Angle16(float degrees) { double t = degrees / 360.0; t -= Math.Floor(t); return (int)Math.Round(t * 65536.0) & 0xFFFF; }
public static float FromAngle16(int value) { double d = (value & 0xFFFF) * (360.0 / 65536.0); return (float)(d > 180.0 ? d - 360.0 : d); }
public static int S16(double value) { double r = Math.Round(value); int v = r < -32768.0 ? -32768 : r > 32767.0 ? 32767 : (int)r; return v & 0xFFFF; }
public static int Pair(int lo, int hi) { return (lo & 0xFFFF) | (hi << 16); }
// ETS\EtsNetWire.cs:141-149
public static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
public static float Float(int bits) => BitConverter.Int32BitsToSingle(bits);
```
ETS keeps wire code free of Unity native calls (no `Quaternion.Euler`, `Slerp`, ...) so it runs in offline tests
(`ETS\EtsPlayerWire.cs:51-54`). Do the same.

### 4.10 Remote held items and hiding (Quake)

* Quake draws each remote player's weapon at that player's Bridge hand, turned to the **aim they send** (two Angle-like
  shorts), because avatar hand bones point differently per rig (`Q\Q3Link.cs:11-29`, `Pack` at `:945-1000`).
* Quake hides everything it drew for a player who is not in the same match and, as a last resort, **disables that
  player's avatar renderers locally** (`Q\Q3Remote.cs:286-335`: walks `remote.Player.References.Body.Root`, disables
  renderers, remembers them, re-enables later; voice deliberately left alone). This touches the avatar's renderers, so
  for Rec Room it is only an **opt-in fallback** (section 5.10).
* Quake's receive gate: presence is read always; game messages only when `SessionMode == Multiplayer`; state from a
  player who does not `SharesGame(id)` hides them (`Q\Q3Link.cs:1021-1060, 1216-1240`; `Q\Q3Combat.cs:2977-2992`).

### 4.11 Names

`ETS\EtsNet.cs:200-238` (`NameOf`): cached slot names first, then a scan of `SomniumBridge.PlayersContainer.AllPlayers`
matching `Properties.PhotonId == playerId`, guarded by try/catch (no client in the editor), fallback "Player N".

---

## 5. Design for Dreamport: Rec Room (PROPOSAL)

### 5.1 Concepts

| Term | Meaning |
|---|---|
| **Activity** | Dorm, Rec Center, or one of the 21 official games (id table below). |
| **Instance** | One running copy of an activity: key = (activity id, instance number). Built at its own origin. |
| **Instance object** | "Rr Net Instance k" (NBD64, k = 0..23): the instance's state, bound to an instance key by its authority. k fixes the origin. |
| **Presence slot** | "Rr Net Player s" (NBD64, s = 0..49): one per player, claimed with `EtsSlotClaim`; says where the player is and carries their per-player game state. Only its owner writes it. |
| **Instance authority** | The member elected by rule (5.4) who holds Fusion state authority over the instance object and the actor blocks/props leased to it, simulates AI, runs timers and scores. |
| **Prop** | A synced physics object (ball, disc, frisbee, paddle, bottle...), one "Rr Net Prop p" (NBD12) each, leased to an instance. |
| **Actor block** | "Rr Net Actors a" (NBD64): 15 AI actors (quest enemies) or bitsets (loot taken), leased to an instance. |

Activity ids and per-instance caps (caps drive sizing only):

| id | Activity | Networked? | Max players | Source |
|---|---|---|---|---|
| 0 | none / arrival | no | - | |
| 1 | Dorm (private, per player) | **no** (local) | 1 | Rec Room design |
| 2 | The Rec Center (hub) | yes | 50 (whole world; one instance normally) | GUESS |
| 3 | 3D Charades | yes | 8 | GUESS |
| 4 | Bowling | yes | 6 (+ spectators) | Rec Room wiki (Bowling) |
| 5 | Crescendo of the Blood Moon | yes | 4 | GUESS (quests are 1-4) |
| 6 | Disc Golf | yes | 4 | wiki (Disc Golf: 1-4) |
| 7 | Dodgeball | yes | 6 | wiki (Dodgeball: 2-6) |
| 8 | Isle of Lost Skulls | yes | 4 | GUESS |
| 9 | Laser Tag | yes | 8 (2 teams x 4) | wiki (Laser Tag) |
| 10 | Make It To Midnight | yes | 4 | GUESS |
| 11 | My Little Monsters | yes | 4 | GUESS |
| 12 | Paddleball | yes | 2 | wiki (Paddleball) |
| 13 | Paintball | yes | 10 | GUESS (4v4 or 5v5) |
| 14 | Punch Up | yes | 4 | GUESS |
| 15 | Quest for the Golden Trophy | yes | 4 | wiki (1-4) |
| 16 | Rec Rally | yes | 8 | GUESS |
| 17 | Rec Royale | yes | 16 solo (18 in 6x3 squads) | wiki (14-16 solos, 6 teams of 3) |
| 18 | Rec Room Showdown | yes | 4 | GUESS |
| 19 | Soccer | yes | 6 | wiki (2-6) |
| 20 | Stunt Runner | yes | 4 | wiki (up to 4) |
| 21 | The Curse of the Crimson Cauldron | yes | 4 | GUESS |
| 22 | The Rise of Jumbotron | yes | 4 | GUESS |

Sources: Rec Room fandom wiki pages (Laser Tag, Dodgeball, Disc Golf, Bowling, Paddleball, Soccer, Rec Royale, Stunt
Runner, Quest for the Golden Trophy), via search summaries.

### 5.2 Scene network objects

All children of one "Rr Net" GameObject (with our bootstrap component, `[DefaultExecutionOrder(-100)]` as `ETS\EtsNet.cs:41`):

| Name | Count | Component | Written by | Purpose |
|---|---|---|---|---|
| Scene Networking | 1 | `SceneNetworking` (our copy) | - | runner hook, join/leave events |
| Rr Net Events | 1 | NetworkObject + `NetworkBridgeEvents` | anyone (RPC) | broadcast events (5.7) |
| Rr Net Player 0..49 | 50 | NetworkObject + `NetworkBridgeData64` | slot owner | presence + per-player state (5.3) |
| Rr Net Instance 0..23 | 24 | NetworkObject + `NetworkBridgeData64` | instance authority | instance state (5.6); "to authority" RPCs |
| Rr Net Actors 0..11 | 12 | NetworkObject + `NetworkBridgeData64` | instance authority of the lessee | AI actors / bitsets (5.8) |
| Rr Net Prop 0..47 | 48 | NetworkObject + `NetworkBridgeData12` | current holder / thrower, else instance authority | physics props (5.5) |
| **Total NetworkObjects** | **135** | | | |

Heap: 87 x 256 B + 48 x 48 B = 24.6 KB of 8 MB. Wire: idle objects cost nothing.

### 5.3 Coordinates: "teleport them far away"

* **Arrival** (Somnium spawn point): a small neutral room at the world origin. Everyone lands here, is shown loading,
  claims a presence slot (~1 s after joining), then is teleported to their dorm.
* **Dorm of presence slot s** (local-only build): `x = -1125 + (s % 10) * 250`, `z = -2000 - (s / 10) * 250`, `y = 0`.
  250 m apart > 120 m avatar cull distance > 100 m boosted voice. Private: nobody else builds it.
* **Instance object k** (k = 0..23): origin `x = -5000 + (k % 6) * 2000`, `z = 2000 + (k / 6) * 2000`, `y = 0`.
  Farthest point ~9.4 km: float32 step there is ~1 mm (acceptable; ETS noted ~4 mm at 45 km would jitter in VR).
  Geometry of an instance is built only by its members. GUESS to check: the largest rebuilt map (Rec Royale's island)
  must fit in 2 km x 2 km; if not, reserve k = 20..23 with a larger stride.
* All floors must be above the world's respawn height (world descriptor), or the client's anti-fall teleport fires
  (`BodyFeature.cs:219-241`).
* Wire positions are **absolute world millimetres (int32)**: every client builds instance k at the same origin, so no
  origin translation is needed (ETS needed origin-independent coordinates only because its map was centred per
  client). Range +-2147 km.

### 5.4 Presence slot "Rr Net Player s" (64 ints) and instance election

**Layout (PROPOSAL):**

| int | Content |
|---|---|
| 0 | magic `'RRP1'` = `0x52525031` (0 = never written) |
| 1 | seq (bumped on header change or claim) |
| 2 | stamp ms (writer `Time.fixedTimeAsDouble * 1000`) |
| 3 | owner PlayerId+1 (0-15) \| claim generation (16-23) \| flags (24: VR, 25: NoGame, 26: in menu/watch open, 27: AFK) |
| 4 | activity id (0-7) \| instance number (8-15) \| status (16-19: 0 None, 1 Loading, 2 Lobby, 3 Playing, 4 Spectating, 5 Down/respawning) \| team (20-23) \| instance object k+1 (24-31, 0 = unbound) |
| 5 | instance generation seen (0-7) \| spawn index (8-15) \| role/loadout (16-23) \| presence epoch (24-31, bumped on every teleport between activities) |
| 6 | UserId hash (FNV-1a 32 of `Properties.Id`): stable identity across rejoin (scoreboards, party) |
| 7 | party leader PlayerId+1 (0-15) \| spare |
| 8 | right hand: item id (0-11) \| item state (12-23: charge, mode, ammo bucket) \| held prop p+1 (24-31) |
| 9 | left hand: same |
| 10-12 | right-hand grip offset: pos x\|y (S16 mm), pos z (S16 mm)\|spare, rotation smallest-three 32 |
| 13-15 | left-hand grip offset: same |
| 16 | right aim yaw\|pitch (Angle16) (NonXR and fallback only) |
| 17 | left aim yaw\|pitch (Angle16) |
| 18 | health/shield (0-7) \| ammo (8-15) \| kills (16-23) \| deaths (24-31) |
| 19 | activity-specific per-player (e.g. bowling: current frame/ball; disc golf: strokes) |
| 20-31 | vehicle/mount state (Rec Rally kart, Stunt Runner): seq, stamp, pos mm x3, rot32, vel S16 cm/s x3 (2 ints), flags (steer, boost, drift) |
| 32-47 | activity-specific (e.g. Rec Royale inventory: 4 slots x 8 bits, armour, consumables) |
| 48-63 | spare |

Writing: header/status at 4 Hz or on change; aims and vehicle at up to 30 Hz with change detection (Fusion sends 16 Hz);
everything via `Push` (4.2). A player alone, in the editor, or before the room joins writes nothing (`IsDebugMode`).

**Membership:** player P is a member of instance (a, n) iff P's slot is written (`magic`), P's PlayerId is in
`runner.ActivePlayers`, `activity == a`, `instance number == n`, and status in {Lobby, Playing, Spectating, Down}.
NoGame players (bit 25) never join an instance (they have nothing to build).

**Election rule:** the instance authority is **the member with the lowest PlayerId** among members whose status is not
Loading (fall back to Loading members if none). Every client computes it at 4 Hz and immediately on
`OnOtherPlayerJoined/Left`, from the same slots, so all agree (ETS `ActingMaster` idea). PlayerIds only grow, so a
newcomer never displaces a live authority; the result changes only when the authority leaves or changes activity.

**Liveness (from `EtsTrafficAuthority`):** the authority rewrites the instance heartbeat int (timer, 5.6) at least every
1 s. If the instance object has not changed for 3 s while the elected authority is still listed, the next candidate by
PlayerId takes over after `3 s + 1.5 s x (candidates below it)`; an old authority that comes back and sees another
writer named in int 3 of the instance object stands down.

**Taking and holding authority:** the elected client calls `RequestStateAuthority()` on the instance object and on every
actor block and orphaned prop leased to it; it writes only while `HasStateAuthority` and while it is elected. Any other
holder (the Fusion master collecting orphans, a lost race) never writes. If the elected client lacks authority for 0.5 s
it asks again, once a second (EtsSlotClaim's `LostPatience/AskEvery` behaviour).

**Migration sequence:**
1. Authority A leaves: every client gets `SceneNetworking.OnOtherPlayerLeft(A)`; A's objects become `StateAuthority.IsNone`.
2. Every client re-elects: B (next-lowest id) is elected. B requests authority over the instance object, its actor
   blocks, and props with no holder.
3. On `OnStateAuthorityChanged(true)` (or `HasStateAuthority` polled), B takes the ints as they are (they are the last
   replicated state), writes int 3 = B, and continues: timers from the remaining time, AI actors from their last poses
   (as EtsTraffic "adopts" cars), scores unchanged. Expected gap ~0.2-1 s (one authority round trip plus detection).
4. The instance's clients never stop simulating locally in between (timers count down, puppets extrapolate then hold).

**Instance binding (claiming an instance object):**
* Joining an activity: the player picks an instance number: among instances of that activity whose object says phase
  joinable and whose member count < cap, the one with the most members (Rec Room fills rooms); else the lowest unused
  instance number. Party members follow their leader's (a, n).
* The elected authority of an unbound instance claims a **free** instance object (unwritten, or written for an instance
  that has no present members, or generation stale) with the same state machine as `EtsSlotClaim` (search start
  `hash(a, n) % 24`), then writes the header (5.6). Members find their object by scanning headers for (a, n) and write
  k+1 into their slot int 4 bits 24-31. Origin is then fixed by k.
* No free object: the player plays that activity **solo, unnetworked** (as ETS role Local), with a notice. With 24
  objects this only happens when more than 24 separate instances run at once.

### 5.5 Synced physics props (grab/throw ownership)

#### 5.5.1 Why owner-simulated ints, not `NetworkRigidbody3D`

`NetworkRigidbody3D` is proven only as a type (Quake's `NetworkGrabbable` compiles against it) but no Dreamport scene
uses it; with the client's runner (`ClientPhysicsSimulation: Disabled`, Unity physics authority) proxies are
non-physical interpolated bodies and Quake needed a "sketchy" kinematic fix (`Q\Networking\NetworkGrabbable.cs:110-116`).
Our own ints give the same owner-simulated model with full control over rates, grip offsets, bounce samples and late
join, using only proven members.

#### 5.5.2 Prop record "Rr Net Prop p" (NBD12, PROPOSAL)

| int | Content |
|---|---|
| 0 | lease: tag `0xA5` (0-7) \| instance object k+1 (8-15) \| instance generation (16-23) \| local prop id (24-31) |
| 1 | state (0-3: 0 Free, 1 Resting, 2 Held, 3 Flying, 4 Hidden/Despawned) \| hand (4: 0 right, 1 left) \| epoch (8-15: bump on reset, teleport, throw; receivers snap) \| holder/writer PlayerId+1 (16-31) |
| 2 | seq (bump per motion sample) |
| 3 | stamp ms (writer `Time.fixedTimeAsDouble`, unwrapped like `EtsNetWire`) |
| 4-6 | position, world mm (int32 each) |
| 7 | rotation, smallest-three 32 bit (2-bit index, 3 x 10-bit components in +-0.7072; ~0.1 deg) |
| 8 | velocity x \| y (S16 cm/s, +-327 m/s) |
| 9 | velocity z (S16 cm/s) \| throw id (16-31, incremented per throw; used by hit/catch arbitration) |
| 10 | angular velocity x,y,z (3 x signed 10 bits, 0.1 rad/s, +-51 rad/s) |
| 11 | kind-specific (dodgeball: live 0/1, thrower team; disc: flight model flags; bottle: fill level) |

Lease: the instance authority leases props for its instance (claim like 5.4 binding: free = unwritten, or lease names
an instance object whose header no longer matches). Local prop id maps to the activity's prop list (e.g. dodgeball balls
0-5). Rec Room values to honour (read from the bundles with rrtool; `(DodgeballBall)` in bundle
`43ea1176ca25c0333547813ec3298808`, pathId `-1619130950701524752`): `Tool.interactionSettings.MaxThrowSpeed 15`,
`ThrowSpeedMultiplier 1`, `RigidbodyPickup.interpolateOnPickup 1`, `ToolCleanup.defaultDelayUntilCleanup 15 s`,
`defaultMinPlayerDistanceBeforeCleanup 3 m`, `MinDisplacementBeforeCleanup 0.1 m`, `CollisionAudio` bounce mapping
with `InstanceLimitDuration 0.15 s`.

#### 5.5.3 Grab, hold, throw

1. **Grab input** (VR grip over the prop, or NonXR click): allowed if the prop is leased to my instance and its state is
   not Held by another present player (games may allow stealing; Rec Room's dodgeball does not).
2. **Optimistic hold**: attach locally to my hand at once, play the pickup sound, start a 2 s timeout
   (`NetworkGrabbable` uses `TIMEOUT = 2.0f`), call `prop.Object.RequestStateAuthority()`.
3. **Authority arrives** (`HasStateAuthority`): write state Held, holder = me, hand, epoch unchanged; write my slot's
   hand record (held prop p+1 and grip offset: my hand-relative pose of the prop). **While held nothing else is sent.**
4. **Lost** (authority went to a later requester, or another present player is written as holder): drop locally and
   snap to the network pose.
5. **Remote clients** attach a Held prop to the holder's `References.Body.RightHand/LeftHand` with the offset from the
   holder's slot. Delay then equals the avatar's own delay, so the ball stays in the drawn hand (ETS measured that
   mixing two different delays puts things 0.5-4.75 m off a moving avatar, DESIGN.md F11). VERIFY in client: whether
   Bridge hand transforms of a NonXR player are meaningful; if not, use the aim ints and a fixed hand pose.
6. **Throw**: release velocity from the local hand's velocity history, times `ThrowSpeedMultiplier`, clamped to
   `MaxThrowSpeed`. Make the local Rigidbody dynamic, set state Flying, bump epoch and throw id, write pose+velocity at
   once (`_forceWrite`), then from `FixedUpdate` at most 30 writes/s with change detection until the body sleeps;
   **force a write at every collision** so a sample lies exactly on the bounce. Then state Resting with the final pose,
   and stop writing.
7. **Remote Flying**: `EtsNetTimeline` with `LeadIn = true` (a newly thrown ball arrives moving). On the throw frame,
   blend 150 ms from the prop's last drawn in-hand pose into the timeline (hides the hand-vs-ball delay mismatch).
   Remote props are **kinematic**; their colliders are triggers except for layers that may interact (paddles, feet).
8. **Touch-steal for rackets and kicks** (Paddleball, Soccer): when my paddle/foot/body hits a prop owned by someone
   else, compute the outgoing velocity locally, simulate it immediately (optimistic) and `RequestStateAuthority()`.
   Rule: last toucher owns, with a 0.25 s cooldown per prop so two players do not ping-pong authority.
9. **Holder leaves**: the prop's authority becomes None. The instance authority requests orphaned props of its instance,
   sets them Resting where they are (or returns them to spawn per `ToolCleanup` after 15 s), bumps epoch.
10. **Cleanup/reset** is written by the instance authority (it requests the prop's authority first).

#### 5.5.4 Sounds and effects of props

Remote puppets are kinematic, so they produce no collisions of their own. The owner sends an **Impact** event
(5.7, type 4) per audible collision, throttled per prop by `InstanceLimitDuration` (0.15 s), with intensity from the
relative speed; receivers play the same clip mapping and volume curve. Pickup/throw sounds are played by each client
when it sees the state change (Held, Flying) in the ints, not by event.

#### 5.5.5 Bowling pins (not props)

Pins are simulated only by the bowler's client. During a roll the bowler streams pin poses with a **PinStream** event
(10 pins x 10 bytes, at 10 Hz for ~3 s: ~1 KB/s briefly); at settle it writes the lane result (standing mask, frame
scores) into the instance object (5.6 activity area). Late joiners see the lane's standing mask from the ints.

#### 5.5.6 Local-only props

Dorm props (basketball, drink bottle...) are local physics with no network object (the dorm is private). Visual-only
debris (paint splats, broken glass) is local, triggered by events.

### 5.6 Game-state replication: "Rr Net Instance k" (NBD64, PROPOSAL)

| int | Content |
|---|---|
| 0 | magic `'RRI1'` = `0x52524931` |
| 1 | seq (bumped per write) |
| 2 | activity (0-7) \| instance number (8-15) \| generation (16-23, +1 on every binding) \| wire version (24-31) |
| 3 | authority PlayerId+1 (0-15) \| phase (16-23: 0 Idle/Lobby, 1 Countdown, 2 Playing, 3 RoundOver, 4 MatchOver) \| round (24-31) |
| 4 | **heartbeat + timer**: remaining ms of the current phase at the moment of writing (0 = no timer); rewritten at least every 1 s |
| 5 | map/variant (0-15) \| mode (16-23) \| options (24-31: friendly fire, locked, private, spectators allowed) |
| 6 | RNG seed (spawns, loot, AI) |
| 7 | score team 0 (0-15) \| team 1 (16-31) |
| 8 | score team 2 \| team 3 (Rec Royale squads use 32-39 instead) |
| 9-10 | objective state (CTF flag holder PlayerId+1 per team (16 bits each); flag at base/dropped bits) |
| 11-26 | roster: 16 entries, each PlayerId+1 (0-15) \| team (16-19) \| ready (20) \| spectator (21) \| per-player score (22-31) |
| 27-58 | activity-specific (bowling: per-lane frame and standing mask; quests: act/stage, checkpoint, boss HP; Rec Rally: lap/checkpoint per roster entry; Rec Royale: storm circle centre (2 ints) and radius, stage) |
| 59 | actor blocks leased (bitmask over 12 blocks) |
| 60 | props leased count \| spare |
| 61-63 | spare |

Rules:
* **Only the instance authority writes it.** Team assignment, phase changes, round results and team scores are decided
  there; players ask by event (type 6 "Request" to `instanceObject.RPC_SendMessageToController`).
* **Timers without a shared clock.** Fusion's simulation time is not a proven API. The authority writes the remaining
  milliseconds (int 4) at least once a second and on every phase change; a receiver that sees int 4 change sets
  `localEnd = Time.unscaledTime + remaining/1000` (error = one-way latency, ~50-100 ms; GUESS) and counts down locally.
  The same int is the authority's heartbeat (liveness, 5.4).
* **Late join:** a joiner reads the instance object (phase, timer, roster, scores, map, seed), the leased props (poses
  and holders), the actor blocks, and every member's presence slot (teams, health, held items). Anything bigger than the
  ints (e.g. a Rec Royale loot table beyond the bitsets, a charades word list) is fetched with a **Hello** event to the
  authority, answered by bursts of `RPC_SendMessageToPlayer` chunks of <= 480 bytes (AoM census pattern, 4.7).
* **Snapshot race:** a client that sees an instance object valid but all zeros waits 1.5 s before treating it as
  unwritten (DESIGN.md risk 8).
* **Mid-round joiners** enter as spectators or join the smaller team per the activity's rule, decided by the authority.

### 5.7 Events and RPCs

**Channel choice:**
* Broadcast events (shots, impacts, deaths, effects): `Rr Net Events`.`RPC_SendMessageToAll`. Receivers drop events
  whose instance key is not theirs. With Global interest every client receives them anyway; one broadcast costs the
  sender one message.
* To the instance authority (requests, hits on AI, catch/hit claims, Hello): `instanceObject.RPC_SendMessageToController`
  (reaches whoever holds the instance object; during a migration gap the message may be lost, so requests are re-sent
  until their effect shows in the ints).
* To one player (census replies, private notices): `Rr Net Events`.`RPC_SendMessageToPlayer(playerRef, bytes)` with the
  `PlayerRef` from `runner.ActivePlayers`.
* Do not use `RPC_SendMessageToProxies` on the events object (its "authority" is an arbitrary player, usually the Fusion master).

**Frame format (PROPOSAL):** one RPC carries a batch of sub-messages, flushed every 50 ms or when 480 bytes are reached:

```
batch:   [0] 0x52 ('R')  [1] wire version  [2..3] sender PlayerId (u16 LE)
         then sub-messages: [type u8][len u8][body len bytes] ...
body common prefix: [inst u8 = instance object k+1, 0 = global][instGen u8]
```

**Message catalogue (PROPOSAL):**

| type | Name | Sender -> target | Body after prefix | Size |
|---|---|---|---|---|
| 1 | Fire | shooter -> all | tool id u8, shot seq u8, origin (3 x S16 cm relative to instance origin), dir yaw+pitch (2 x Angle16), speed u16 cm/s, seed u8 | 17 B |
| 2 | Hit | shooter -> all | victim PlayerId u16, damage u8, body part u8, shot seq u8, hit point (3 x S16 cm) | 13 B |
| 3 | Death/Out | victim -> all | killer PlayerId u16, tool id u8, point (3 x S16 cm) | 11 B |
| 4 | Impact | prop owner -> all | prop p u8, surface/clip u8, intensity u8, point (3 x S16 cm) | 11 B |
| 5 | Catch/HitClaim | player -> authority | prop p u8, throw id u16, kind u8 (catch / hit), victim u16 | 8 B |
| 6 | Request | player -> authority | request u8 (join team, ready, start, vote map, leave), arg u8 | 4 B |
| 7 | ActorHit | player -> authority | block u8, actor u8, damage u8, tool id u8, point (3 x S16 cm) | 12 B |
| 8 | Effect | anyone -> all | effect id u16, point (3 x S16 cm), yaw Angle16, param u8 | 11 B |
| 9 | Toast | authority -> all | text id u16, args (<= 8 B) | <= 12 B |
| 10 | PinStream | bowler -> all | lane u8, 10 x (pos 3 x S16 mm-lane-relative, rot 32) | ~103 B |
| 11 | Hello | joiner -> authority | what u8 | 3 B |
| 12 | Census | authority -> joiner | chunk index u8, data | <= 470 B |
| 13 | Emote/Sound | anyone -> all | clip id u16, point | 8 B |

**Who decides what:**
* **Hits on players**: the shooter/thrower's client, against the victim as drawn on the shooter's screen, using our own
  hit volumes that follow the victim's Bridge body transforms (separate GameObjects we own, never components added to
  the avatar). The victim's client applies damage and owns its health (slot int 18) and emits Death. The authority
  counts Death events into scores. This is Quake's split (`Q\Q3Link.cs:275-340` ReportHit, `:525-560` ReportDeath).
* **Hits on AI actors**: players report ActorHit to the authority, which applies damage in the actor block (Quake's
  MessageBotHit, `Q\Q3Link.cs:603-640`).
* **Dodgeball catch vs hit**: the thrower reports HitClaim, the catcher reports CatchClaim, both with the throw id
  (prop int 9); the authority takes the **first report it receives** for that throw id and ignores the other.
* **Projectiles** (paintballs, laser bolts, arrows) are not network objects: every client simulates the visual from
  Fire's origin/dir/speed/seed; only the shooter decides hits.

**Limits:** payload <= 480 B (512 hard, 2.1). Rate: batching keeps each client at <= 20 RPCs/s (GUESS: no Fusion or
Photon per-client RPC limit was found locally; AoM sent bursts of dozens of 500-byte RPCs during a census without
reported trouble). No ordering is guaranteed between an RPC and an int change: carry seq/throw ids and tolerate either
order.

### 5.8 AI actors and bitsets: "Rr Net Actors a" (NBD64)

Same shape as ETS traffic blocks (`ETS\EtsTrafficWire.cs:6-29`) and AoM unit slots (`AOM\Aom\AomWire.cs:5-20`):

| int | Content |
|---|---|
| 0 | magic `'RRA1'` \| (the lease: instance object k+1 and generation are in int 3) |
| 1 | seq (bumped per write, and at least once a second: heartbeat) |
| 2 | stamp ms (writer clock) |
| 3 | writer PlayerId+1 (0-15) \| instance k+1 (16-23) \| instance generation (24-31) |
| 4..63 | 15 actors x 4 ints: `+0` x mm-relative-to-instance-origin (int32) ; `+1` z (int32) ; `+2` y S16 cm \| yaw Angle16 ; `+3` actor type (0-7) \| anim state (8-15) \| health % (16-22) \| flags (23-28: alive, attacking, hurt, stunned) \| serial (29-31, new actor in slot = snap) |

Written at 10 Hz by the instance authority, only changed ints (ETS measured 704 ints/s for a full 60-car load, 2.8 KB/s).
Watchers draw actors on `EtsNetTimeline` with `LeadIn` (velocity from successive samples). On migration the new
authority adopts each actor from its last pose. The same block can instead hold a bitset (60 ints = 1,920 bits) for
"loot item taken" or "breakable destroyed" (Rec Royale, quests).

### 5.9 Vehicles (Rec Rally karts, Stunt Runner movers)

A kart is owned by its driver and travels in the driver's presence slot (ints 20-31), exactly like ETS trucks in their
owner's slot (`ETS\EtsPlayerWire.cs:62-96`, measured 535-663 B/s per driving truck at 16 Hz). Draw remote karts on
`EtsNetTimeline`; carry the driver's avatar by teleport-free local placement is **not** possible (the avatar is the
client's), so seat the local player with the Bridge pose API as ETS does and accept that remote avatars follow their
own sync (ETS README 1482-1500; DESIGN.md F11/F14).

### 5.10 Visibility and isolation rules

1. Build only the local player's current activity (plus the arrival room). Nobody else's instance geometry exists here.
2. Remote players in other activities are far away (5.3): culled by the client beyond 120 m when its culling applies,
   inaudible beyond 25 m (100 m boosted), and at 2 km a 1.8 m avatar is about 1 pixel even if drawn.
3. Everything **we** draw for a player (held tools, hit volumes, name/score tags, team colours) exists only while
   `SameInstance(player)`.
4. Receive gate (Quake's): presence is read always (it drives the watch menu's "who is where"); game events are ignored
   unless their instance key is ours.
5. Fallbacks, only if a client test shows distant avatars or name tags still visible: (a) the world-level
   `SomniumPlayerTagRules` areas or `ISomniumPlayerInfo.SetPlayerTagDisableState` (unproven API) for tags; (b) Quake's
   local renderer hide (`Q\Q3Remote.cs:286-335`). (b) touches the avatar's renderers locally and is reversible, but the
   user asked not to touch the Somnium avatar: **ask the user before enabling it**.
6. Voice stays Somnium's (spatial). Team voice over distance (Rec Royale squads) would need
   `ISomniumPlayerVoice.EnableAmplify` (unproven): optional, ask first.

### 5.11 Offline, editor and no-game behaviour

* `SceneNetworking.IsDebugMode` (no runner: editor, or before joining): everything runs locally as one player, every
  write is skipped, the local player is authority of whatever it plays (ETS "role Local").
* Before `IsNetworkReady` the player waits in the arrival room; after 20 s without a room, go Offline (ETS Phase 2 rule).
* A visitor without Rec Room installed claims a slot with NoGame set (so others see them in lists), is never elected and
  stays in the arrival room with a message.

---

## 6. Bandwidth budget

Payload only (ints x 4 B x changes per second at the 16 Hz send; RPC payload + 8-byte RPC header). Fusion/Photon packet
overhead is not measurable offline (as in DESIGN.md 2.10).

| Source | Per unit | Basis |
|---|---|---|
| Presence slot, player idle or in VR holding still | ~0 B/s | only changed ints are sent |
| Presence slot, NonXR player aiming (2 aim ints) + health/ammo | ~150 B/s | 2 ints x 4 B x 16 Hz + ~1 int/s |
| Held prop | 0 B/s | offset sent once in the holder's slot |
| Flying prop | ~450-512 B/s | ~7-8 changed ints x 4 B x 16 Hz |
| Actor block, 15 moving actors at 10 Hz | ~2.4 KB/s | 15 x 4 ints x 4 B x 10 Hz (ETS: 704 ints/s = 2.8 KB/s for 60 cars) |
| Kart (driver's slot) | ~550-650 B/s | ETS truck measurement (535-663 B/s) |
| Fire event | ~20-30 B per shot | 17 B body + batch/RPC headers amortised |
| Instance object heartbeat | ~4-8 B/s | int 1 and int 4 once a second |

Worst plausible download per client, 50 players (15 in Paintball/Laser Tag, 16 in Rec Royale, 8 in Rec Rally, 4 in a
quest, 7 elsewhere):
* slots of 40 active players: 40 x 150 B/s = 6 KB/s
* 10 props in flight at once: 5 KB/s
* 1 quest with 2 busy actor blocks: 4.8 KB/s
* 8 karts: 5 KB/s
* shots: 31 shooters x 3 shots/s x 25 B = 2.3 KB/s
* **total about 23-25 KB/s (about 200 kbit/s)**, below ETS's accepted 17-32 KB/s range (DESIGN.md 2.10).

Upload per client: own slot 0.15 + own flying prop 0.5 + events 0.1-0.2 = **under 1 KB/s**; an instance authority adds
its actor blocks (2-5 KB/s for a quest) and heartbeat. Everything is Global interest, so these download numbers are the
same for every client whatever activity it is in.

---

## 7. Implementation checklist and testing

1. Copy the Fusion/Community Modules packages with GUIDs (3.5). Add the 135 objects with an idempotent editor menu (3.4).
2. Keep all wire layouts, election, claim and timeline code **pure** (no Fusion, no Unity native calls), as ETS does, so a
   simulated room can test them offline. Model Fusion as probe 2200 did: "last authority request wins, writes without
   authority dropped, a delay on everything" (`ETS\EtsPlayerWire.cs:1240-1243`). Test: joins, leaves, simultaneous
   claims, authority leaving mid-round, late join mid-round, snapshot arriving late, dodgeball catch/hit race.
3. Every build: run the server-API guard (`ETSS\fusionapi\check.sh <built dll>`), with `known_good.txt` extended only by
   members proven by an upload.
4. Log prefix `[SomniumRecRoom] Network:`; log claims, elections, authority changes, bindings, and every 10 s
   ints/s read and written (ETS logs "wrote/read ints/s" for the budget check).
5. Reset statics in `OnDestroy`; subscribe to `SceneNetworking` events in `Awake`/`OnEnable` of each visit.
6. Use Unity's null test (`== null`), never `??`, on components (fake-null in the editor: `ETS\EtsNet.cs:73-76`).
7. Fields on scene objects that code sets should be `[NonSerialized]` (a scene value overrides the code default:
   `ETS\EtsNet.cs:462-466`).

---

## 8. Risks and open questions

| # | Risk | Mitigation |
|---|---|---|
| R1 | **135 scene NetworkObjects** is twice the largest proven Dreamport scene (AoM: 67 = 64 slots + lobby + events + bank; ETS: 56). Join snapshot size and registration time are unmeasured at this count. | First client test with the full scene. Fallbacks: props 48 -> 32, instances 24 -> 16, actor blocks 12 -> 8 (= 108). |
| R2 | Server Fusion is older than local; only the members in section 10 are proven. `GetMasterClient`, `SimulationTime`, `SendReliableDataToPlayer`, `OnRender`, `OnFixedUpdateNetwork`, `GetPlayerByPhotonID` are not. | Guard script; avoid unproven members. |
| R3 | Client avatar culling beyond 120 m is conditional (`AvatarFeature.cs:916-928`); name tags' range unknown. | 2 km spacing; world-drawn tags only; fallbacks in 5.10 need the user's consent. |
| R4 | RPC rate limits of Fusion shared mode / Photon Cloud unknown. | Batch per 50 ms; <= 20 RPC/s per client (GUESS). |
| R5 | Bridge hand transforms of NonXR players may not follow the visible avatar hand. | Test; fall back to aim ints and a fixed hand pose. |
| R6 | Timer accuracy without a shared clock (~one-way latency). | Acceptable for game timers; countdown sounds may be ~0.1 s late on non-authority clients. |
| R7 | Authority churn at migration: the Fusion master's 1 Hz orphan grab can briefly hold an instance object. | `_autoTransferObjectsAuthority = 0`; elected writer re-asks after 0.5 s; non-elected holders never write. |
| R8 | Remote kinematic props can push the local player's controller. | Remote prop colliders as triggers except whitelisted interaction layers. |
| R9 | Instance footprint > 2 km (Rec Royale island) would overlap a neighbour. | Measure the rebuilt map; reserve larger-stride instance objects. |
| R10 | Hit volumes following remote Bridge body transforms lag the avatar the shooter sees by different amounts. | Favour the shooter (Rec Room does the same); hit volumes sized generously. |

Open questions for the user:
1. May we hide name tags or, as a last resort, locally hide avatar renderers of players in other activities if distance
   alone is not enough (5.10)?
2. Dorm visiting (inviting friends into your dorm) in scope? The proposal keeps dorms private and local.
3. Team voice over distance in Rec Royale (voice amplify API, unproven)?

---

## 9. Quick reference: code shapes for the implementation (PROPOSAL)

```csharp
// Elect the instance authority (pure; run at 4 Hz and on join/leave)
public static int ElectAuthority(int[][] slots, int count, IReadOnlyList<int> present, int activity, int number)
{
    int best = int.MaxValue, bestLoading = int.MaxValue;
    for (int s = 0; s < count; s++)
    {
        int[] r = slots[s];
        if (r[0] != RrPlayerWire.Magic) continue;
        int owner = (r[3] & 0xFFFF) - 1;
        if (owner < 0 || !Contains(present, owner) || (r[3] & (1 << 25)) != 0) continue; // not present or NoGame
        if ((r[4] & 0xFF) != activity || ((r[4] >> 8) & 0xFF) != number) continue;
        int status = (r[4] >> 16) & 0xF;
        if (status == 0) continue;
        if (status == 1) { if (owner < bestLoading) bestLoading = owner; }
        else if (owner < best) best = owner;
    }
    return best != int.MaxValue ? best : bestLoading != int.MaxValue ? bestLoading : -1;
}

// Smallest-three quaternion, 32 bits (2-bit index, 3 x 10 bits)
public static int PackRot(Quaternion q)
{
    float[] c = { q.x, q.y, q.z, q.w };
    int big = 0; for (int i = 1; i < 4; i++) if (Math.Abs(c[i]) > Math.Abs(c[big])) big = i;
    float sign = c[big] < 0 ? -1f : 1f; int packed = big; int shift = 2;
    for (int i = 0; i < 4; i++) if (i != big)
    {
        float v = c[i] * sign / 0.70710678f;                       // -1..1
        int u = (int)Math.Round((v * 0.5f + 0.5f) * 1023f);
        packed |= (u < 0 ? 0 : u > 1023 ? 1023 : u) << shift; shift += 10;
    }
    return packed;
}
public static Quaternion UnpackRot(int packed)
{
    int big = packed & 3; float[] c = new float[4]; float sum = 0f; int shift = 2;
    for (int i = 0; i < 4; i++) if (i != big)
    {
        float v = ((packed >> shift) & 1023) / 1023f * 2f - 1f; c[i] = v * 0.70710678f; sum += c[i] * c[i]; shift += 10;
    }
    c[big] = (float)Math.Sqrt(Math.Max(0f, 1f - sum));
    return new Quaternion(c[0], c[1], c[2], c[3]);
}

// Send an event batch (sender id in the payload: handlers do not get RpcInfo)
void Flush()
{
    if (_batchLen <= 4 || !Valid(_events)) return;
    var bytes = new byte[_batchLen]; Array.Copy(_batch, bytes, _batchLen);
    _events.RPC_SendMessageToAll(bytes);           // comes back to us too: skip our own sender id on receipt
    _batchLen = 4;                                  // keep header: 'R', version, sender u16
}
```

---

## 10. Appendix: server-proven API members (from `ETSS\fusionapi\known_good.txt`)

Members used by worlds that passed Somnium's server compile (AoM `SOMAOM.dll`, Quake `SomniumQuake.dll`, Blades Edge,
ETS uploads). Stay inside this list.

```
Fusion.Addons.Physics.NetworkRigidbody3D (type)
Fusion.GameMode, HostMigrationToken, INetworkPrefabSource, INetworkRunnerCallbacks, IPublicFacingInterface (types)
Fusion.NetworkArray`1: Get, Set, get_Length
Fusion.NetworkBehaviour: ResetState, get_HasStateAuthority
Fusion.NetworkInput (type)
Fusion.NetworkMecanimAnimator: Animator, SetTrigger
Fusion.NetworkObject: Flags, RequestStateAuthority, SortKey, get_HasStateAuthority, get_IsValid, get_StateAuthority
Fusion.NetworkObjectFlags, NetworkObjectGuid (op_Implicit), NetworkPrefabId
Fusion.NetworkPrefabTable: AddSource, Clear, GetEntries, TryAddSource
Fusion.NetworkProjectConfig: PrefabTable, get_Global
Fusion.NetworkRunner: AddCallbacks, RegisterSceneObjects, Spawn, StartGame, get_ActivePlayers, get_Instances,
                      get_IsRunning, get_IsSharedModeMasterClient, get_LocalPlayer; NetworkRunner+OnBeforeSpawned
Fusion.NetworkRunnerCallbackArgs(+ConnectRequest), NetworkSceneLoadId, NetworkSpawnFlags (types)
Fusion.PlayerRef: get_IsNone, get_IsRealPlayer, get_None, get_PlayerId, op_Equality, op_Inequality
Fusion.RpcInfo, SceneRef (FromPath, get_IsValid), SessionInfo, ShutdownReason, SimulationMessagePtr (types)
Fusion.SimulationBehaviour: get_Object
Fusion.StartGameArgs: GameMode, SessionName; StartGameResult
Fusion.Sockets: NetAddress, NetConnectFailedReason, NetDisconnectReason, ReliableKey (types)
Fusion.Unity: NetworkAssetSourceStatic`1.Object, NetworkPrefabSourceStatic (.ctor, AssetGuid)
SomniumSpace.Bridge.Components.SomniumTriggerActionArgs: .ctor, get_IsLocal, get_IsPlayer
SomniumSpace.Bridge.Environment.ISomniumEnvironment: SetSkybox, SetSunSource, UpdateEnvironment
SomniumSpace.Bridge.LocalStorage.ISomniumWorldsLocalStorage: ReadText, TextExists, WriteText
SomniumSpace.Bridge.Player.Components.ISomniumPlayerAnimation: SetFallingAnimationDisabled
SomniumSpace.Bridge.Player.Components.ISomniumPlayerAvatar: SetVRIKLegMovementDisabled
SomniumSpace.Bridge.Player.Components.ISomniumPlayerBody: get_Head, get_LeftHand, get_Neck, get_RightHand, get_Root
SomniumSpace.Bridge.Player.Components.ISomniumPlayerFeatures: get_Animation, get_Avatar, get_Motion, get_Poses
SomniumSpace.Bridge.Player.Components.ISomniumPlayerMotion: DoTeleportToPoint, SetCollisionDisableState,
      SetFlyModeDisableState, SetGravity, SetJump, SetMovementSpeed, SetTeleportationDisableState
SomniumSpace.Bridge.Player.Components.ISomniumPlayerPoses: SetPose; PoseType (type)
SomniumSpace.Bridge.Player.Components.ISomniumPlayerProperties: get_Id, get_IsLocal, get_NickName, get_PhotonId
SomniumSpace.Bridge.Player.Components.ISomniumPlayerReferences: get_Body
SomniumSpace.Bridge.Player.ISomniumPlayer: get_Features, get_Properties, get_References
SomniumSpace.Bridge.Player.ISomniumPlayersContainer: add_OnPlayerAdded, get_AllPlayers, get_LocalPlayer, remove_OnPlayerAdded
SomniumSpace.Bridge.SomniumBridge: get_Environment, get_LocalStorage, get_PlayersContainer
SomniumSpace.Network.Bridge.NetworkBridge: RPC_SendMessageToAll, add/remove_OnMessageToAll, get_SyncByteArray
SomniumSpace.Network.Bridge.NetworkBridgeData: get_IntArray, get_IntCount
SomniumSpace.Network.Bridge.NetworkBridgeEvents: RPC_SendMessageToAll, RPC_SendMessageToController,
      RPC_SendMessageToPlayer, RPC_SendMessageToProxies, add/remove_OnMessageToAll, add/remove_OnMessageToController,
      add/remove_OnMessageToPlayer, add_OnMessageToProxies, add/remove_OnSpawned, add/remove_OnStateAuthorityChanged
```

(`ISomniumPlayerFeatures.get_Voice`, `get_Info`, `get_Scale`, `get_Wearable`, `ISomniumPlayersContainer.GetPlayerByPhotonID`,
`remove_OnMessageToProxies`, `NetworkBridgeEvents.add_OnRender/OnFixedUpdateNetwork/OnDespawned` are **not** on the list.)
