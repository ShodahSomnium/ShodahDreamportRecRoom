# Somnium local player, hands, grabbing and input for Rec Room-style interaction

Status: COMPLETE (2026-09-24). GUESS marks untested proposals; section 10 lists them.

Paths: CLIENT = `E:\Somnium Space\Main Build\Assets`, PFB = `CLIENT\_SomniumSpace\Player\Prefabs\NEW`,
ETS = `D:\Dreamport\Euro Truck Simulator\Euro Truck Sim\Assets\#User\SomniumEuroTruck`,
AOM = `D:\Dreamport\Age Of Mythology\Assets\#User\SOMAOM\Aom`, Q3 = `D:\Somnium Quake\Assets\#User\SomniumQuake`,
BRIDGE (decompiled interfaces) = `SP\research\bridge_decomp` (SP = the scratchpad).

## 0. TL;DR

1. **Bridge hands = controller anchors, world space.** `Body.LeftHand/RightHand` are the client's
   `Left/RightHandAnchor` (TrackedPoseDriver + XRI controller, re-posed before render), not the avatar's IK hands.
   They already include the rig (`XR.Body`), offsets and seat/pose adjustments. **On desktop they are null** (local
   AND remote desktop players). `Body.Head` = `HeadAnchor`; `Body.Root` = `XR.Body` / `NonXR.Body` (CharacterController).
2. **Hold frame:** parent a child "Rr Grip L/R" under the Bridge hand (calibrated local offset), attach held props to
   it. Fallback when the hand is null: Quake/AoM's HMD-derived tracking-to-world matrix
   `cam.localToWorldMatrix * TRS(hmdPos, hmdRot).inverse` applied to `devicePosition/deviceRotation` (grip pose),
   `GripToAim = Euler(90,0,0)` for aiming. Settle aim-vs-grip on the anchor with the probe in 9.6.
3. **The client never grabs our props**: its Grab interactor selects only XRI interactables on interaction layer 3
   (Grabbable). Do not add XRGrabInteractable to our props. But its **physical hand boxes** (layer 7, 1 kg, 150 N joint)
   shove anything on layers that collide with 7 - exclude layer 7 on a prop's colliders while it is held.
4. **Layers (GUESS, from the matrix):** grabbable props on **10** (no avatar-capsule shove, not in the desktop pointer
   mask) with room floors/walls on **23**; balls/projectiles on **26**; per-player hit boxes on **27**, as triggers.
5. **VR input:** only **grip and trigger** per hand are free (right A = client jump, X/Y/B/menu/stick clicks are the
   client's). Grip = grab (hysteresis 0.55/0.35), release = drop/throw, trigger = use the held tool. Read with
   `UnityEngine.XR.InputDevices` exactly like `EtsVr.Poll`.
6. **Desktop "play mode"** (inside an activity): own the camera like ETS (disable `CinemachineBrain` by name) with
   Quake's mouse look and cursor lock, write `Body.Root` yaw each LateUpdate (walk direction, and it neutralises the
   client's Q/E rotate), crosshair at centre, aim = camera centre ray, throws/shots toward the gaze target. Keys:
   **E grab, Q tap drop / hold toss, LMB use/fire/throw (charge), RMB aim/secondary, 1-4 holsters, Ctrl slide/crouch,
   V third person, Z reload**; Space/Shift stay the client's jump/sprint; Tab/F/R/I/M/X/Escape stay the client's.
   Suspend (free the cursor, give the camera back) whenever the tablet, a menu, chat or focus loss takes over.
7. **Locomotion:** multipliers only (0..10): `SetMovementSpeed/SetGravity/SetJump`, plus disable switches. Freeze with
   the ETS sequence (6.2) and always restore. Karts = `EtsTruckSeat` pattern (6.3). Stunt Runner moves (slide,
   wall-run) need the Quake takeover: zero multipliers, disable the CharacterController, own mover (6.4).
8. **Remote players:** `Properties.PhotonId` = Fusion `PlayerRef.PlayerId`; `NickName`, `Id` (user id). Remote VR
   bodies have networked Head/hands; remote desktop bodies have Root (+ a fixed Head) only. Build our own hit volumes
   on 27 from Root/Head; the client's `PlayerTrigger` (r 0.18, layer 15, `ISomniumPlayerTrigger.Player`) identifies
   players in zones.
9. Rec Room's own tuning is readable from the bundles: per-tool `Tool.interactionSettings` (pickup transforms,
   LockOnPickup, MaxThrowSpeed, holster slots) and screen-mode configs (throw 8 m/s toward gaze, dodgeball charge
   0.1-0.5 s at 14 m/s, first-person eye 1.55 m, sprint FOV x1.15, slide x1.3, crosshair sprite).
## 1. The local player rig (measured from client prefabs)

### 1.1 What the Bridge body transforms really are

`BridgePlayerBody` (`CLIENT\_SomniumSpace\Bridge\Scripts\Components\Player\BridgePlayerBody.cs:9-13`) just forwards
`IBodyFeature.GetRootTransform/GetHeadTransform/GetNeckTransform/GetLeftHandTransform/GetRightHandTransform`, which
return `PlayerBodyHolder._body` (`BodyInfo {Root, Head, LeftHand, RightHand, Neck}`,
`CLIENT\_SomniumSpace\Player\Scripts\Components\BodyParts\Common\PlayerBodyHolder.cs:9-17`,
`...\Features\Common\BodyFeature.cs:75-113`). The serialized values (dumped from the prefabs with `player_probe/refs.pl`):

| Prefab (who uses it) | Body.Root | Body.Head | Body.LeftHand / RightHand | Body.Neck |
| --- | --- | --- | --- | --- |
| `PFB\XR\Player.XR.Body.prefab` (local, headset) | `XR.Body` (L2; XROrigin, CharacterController, kinematic RB) | `HeadAnchor` (L24, TrackedPoseDriver = HMD) | `LeftHandAnchor` / `RightHandAnchor` (L24, the **controller pose**, not the avatar's IK hand) | `NeckAnchor` |
| `PFB\NonXR\Player.NonXR.Body.prefab` (local, desktop) | `NonXR.Body` (L2) | `HeadAnchor` (L24, child of `Head`) | **null / null** | null |
| `PFB\XR\Player.XR.Body.Simulator.prefab` (REMOTE headset player) | `Root` (L2) | `HeadAnchor` | `LeftHandAnchor` / `RightHandAnchor` (networked) | `NeckAnchor` |
| `PFB\NonXR\Player.NonXR.Body.Simulator.prefab` (REMOTE desktop player) | `NonXR.Body` (L2) | `HeadAnchor` | **null / null** | null |

"Simulator" = remote copy (`NetworkPart.cs:24` names parts "Original" vs "Simulator"). The avatar's IK targets are
separate: `_ikPoints._leftHand = LeftHandVRIKPoint` (L30, child of the anchor, local scale 0.08647), so the
**Bridge hand is the tracked controller anchor, and the drawn avatar hand is solved to follow it** (plus
`_leftHandPos (-0.033,0.068,-0.036)` / `_leftHandRot` offsets inside `PlayerBodyHolder._ikPoints`). AoM's comment
("the avatar's hand, as the client is drawing it", `AOM\AomBridge.cs:43-51`) is loose; ETS states it correctly
(`ETS\EtsSteeringGrab.cs:23-28`).

### 1.2 XR rig hierarchy (local, `Player.XR.Body.prefab`; full dump `SP\research\player_probe\tree_xr_body.txt`)

```
Player.XR.Body                         XRPlayerBody
  XR.Body [L2]                         = Body.Root. XROrigin, CharacterController(c143), Rigidbody(c54),
                                         CameraHolder, PhysicsHolder, PlayerBodyHolder, XRBodyTransformer,
                                         LocomotionMediator, OpenVROffseter, PlayerTransformNetworkDriver
    FloorOffset
      Head > HeadCustomPoseOffset > HeadUserPostureOffset > HeadOffset(OpenVROffsetListener)
        HeadAnchor [L24]               = Body.Head. TrackedPoseDriver, PlayerTransformNetworkDriver
          ... Trigger/PlayerTrigger [L15], NeckAnchor [L12], BlackFadeManager, TabletPoint
        Camera [L24]                   the XR camera (Camera, AudioListener, TrackedPoseDriver) - a SIBLING of
                                         HeadAnchor, both under HeadOffset
      LeftHand > LeftHandCustomPoseOffset > LeftHandUserPostureOffset > LeftHandOffset(OpenVROffsetListener)
        LeftHandAnchor [L24]           = Body.LeftHand. legacy TrackedPoseDriver + XRI ActionBasedController,
                                         kinematic Rigidbody 0.1 kg, PlayerTransformNetworkDriver
          Hand Collider Anchor [L7]    kinematic RB the physical hand box is jointed to
          LeftHandVRIKPoint [L30]      avatar IK target
          LeftTeleportPointer [L21]    XRRayInteractor (teleport)
          LeftHandEnd [L24]
        Stabilized (XRTransformStabilizer)
          Near-Far Interactor Right Left [L6]  NearFarInteractor (UI/pointer)
          Grab Left Variant [L6]               NearFarInteractor (grab)
          Climb Grab Left Variant [L6]         XRDirectInteractor (climbing)
        Hand Collider Left Variant [L7]  dynamic 1 kg box (see 3.3)
      RightHand ... (mirror)
      Teleport, FullBodyTracking, NavigationArrow, AvatarFeedbackVFX
```

Consequences:
- Every VR pose the Bridge gives (Head, LeftHand, RightHand) is already in **world space** and already includes the
  rig (`XR.Body` = XROrigin), the FloorOffset, the client's per-runtime `*Offset` (OpenVROffsetListener), the user
  posture offset and the pose offset. ETS found that entering a Somnium pose (sitting) turns
  `Left/RightHandCustomPoseOffset` by the same angle as the head (`ETS\README.md:1548`, fix in `EtsTruckSeat`).
- The anchors are driven by `m_UpdateType: 0` = **Update and BeforeRender** (`Player.XR.Body.prefab:2131,3324,3991`).
  Anything **parented** under an anchor is re-posed just before rendering (lowest latency); anything we position from
  our own `LateUpdate` uses the Update-time pose (one late-latch behind). See 2.4.
- Position/rotation actions of the ActionBasedController: **pointerPosition first, devicePosition fallback**
  (`CLIENT\_SomniumSpace\Inputs\XRI Input Actions.inputactions`, maps "XRI LeftHand"/"XRI RightHand": Vector3Fallback
  first=`pointerPosition`, second=`devicePosition`; same for rotation). The legacy TrackedPoseDriver on the same
  object (`m_Device 1, m_PoseSource 4/5` = Left/RightPose, i.e. the device/grip pose) also writes it. Which one wins
  per frame is decided by execution order (GUESS: the XRI controller runs after the legacy driver, so the anchor is
  the **aim/pointer** pose when the runtime reports one). Measure it at run time (snippet 9.6).
- Runtime: the client initialises **OpenXR first** (`CLIENT\XR\XRGeneralSettings.asset:48-50` loader order OpenXR,
  OpenVR; `CLIENT\_SomniumSpace\XR\Scripts\XRSystem.cs:108-133` removes OpenVR unless SteamVR is the default runtime
  and starts OpenVR afterwards only for tracker poses). So `UnityEngine.XR` controller poses are OpenXR poses:
  `CommonUsages.devicePosition/deviceRotation` = the **grip** pose.

### 1.3 Desktop rig (`Player.NonXR.Body.prefab`; dump `SP\research\player_probe\tree_nonxr_body.txt`)

```
Player.NonXR.Body [L24]
  NonXR.Body [L2]          = Body.Root. CharacterController, RB, NonXRCameraHolder, PhysicsHolder,
                             PlayerBodyHolder, NonXRAdditionalHolder, NonXRPushProvider, network driver
    Head > HeadCustomPoseOffset > HeadUserPostureOffset > HeadAnchor [L24] = Body.Head
          (AdjustScaler/HeadVRIKPoint, CameraTargetFacePosition, Trigger/PlayerTrigger, Ears = AudioListener)
  CameraSystem             NonXRCameraPresetsHolder, Cinemachine rigs (ThirdPerson / FirstPerson / FreeMode)
    Camera [L24]           THE desktop camera (Camera, MouseHoverRaycaster, ...). NOT under Body.Root.
      TabletPoint, ProfileDisplayPoint, InspectorPoint, Overlay
```

On desktop the camera orbits behind the avatar (third person by default) and is not a child of the body, so
**aim must come from the camera, not from Body.Head** (Quake: "Where shots go. Taken from the camera rather than the
avatar's head", `Q3\Q3Combat.cs:1479-1483`).

## 2. Hands in VR: pose sources

### 2.1 The three candidate sources

| Source | Space | Pros | Cons |
| --- | --- | --- | --- |
| **Bridge `Body.LeftHand/RightHand`** (= `Left/RightHandAnchor`) | world | exactly the transform the client's own interactors, hand boxes, IK and network sync hang off; includes rig, offsets and seat/pose adjustments; re-posed before render; remote players have the same thing (networked) | null on desktop; rotation is (probably) the aim pose, see 1.2 |
| `UnityEngine.XR.InputDevices` `devicePosition/deviceRotation` | **tracking space** (relative to the XROrigin's camera floor offset, NOT world) | always the grip pose; available without the Bridge (editor with a headset) | needs a tracking-to-world transform; lags the before-render pose; misses the client's own offsets |
| Avatar IK hand (`*HandVRIKPoint` or the avatar bone) | world | where the hand is drawn | driven through IK, lags/retargets; not exposed by the Bridge (only by name search) |

### 2.2 How the siblings converted tracking space to world (quote)

All three derive the tracking-to-world matrix from the HMD pair instead of assuming the rig layout:

`Q3\Q3Vr.cs:341-347` (identical in `AOM\AomVr.cs:1286-1300` and the fallback in `ETS\EtsSteeringGrab.cs:413-427`):
```csharp
if (_hmd.isValid &&
    _hmd.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headLocal) &&
    _hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRotation))
{
    toWorld = Head.transform.localToWorldMatrix *
              Matrix4x4.TRS(headLocal, headRotation, Vector3.one).inverse;
}
else
{
    toWorld = Rig != null ? Rig.localToWorldMatrix : Matrix4x4.identity;
}
```
(`Head` = the headset camera: `EtsVr.Head()` / `Camera.main`.) A controller then lands at
`toWorld.MultiplyPoint3x4(devicePosition)` with rotation `toWorld.rotation * deviceRotation`.

Hand placement in Quake (`Q3\Q3Vr.cs:375-387`): position = the Bridge hand when present, else the converted device
position; rotation = the controller's, twisted by `_gripToAim = (90,0,0)` (`Q3Vr.cs:73`; same constant
`AomVr.GripToAim`, `AOM\AomVr.cs:42`):
```csharp
existing.SetPositionAndRotation(
    anchor != null ? anchor.position : toWorld.MultiplyPoint3x4(position),
    toWorld.rotation * rotation * Quaternion.Euler(_gripToAim));
```
Quake hands `_player.RightHand` (= Bridge `Body.RightHand`) as the anchor once it exists
(`Q3\Q3Combat.cs:1439-1465`, retried until the body exists) and mounts the weapon on `Q3Vr.Aim`.

AoM wrist panel (`AOM\AomVr.cs:1627-1665`): wrist = `AomBridge.HandOf(false).position` (Bridge left hand) or the
converted device position; facing = `toWorld.rotation * deviceRotation * Euler(GripToAim)`; shown when
`Dot(facing*up, toHead) > 0.45`; panel at `wrist + up*0.02`, `LookRotation(-up, facing*forward)`.
AoM forearm bar (`AomVr.cs:586-644`): `arm.position = hand.position + hand.up*0.03f;
arm.rotation = LookRotation(-hand.up, hand.forward)` using the Bridge hand's own rotation.

AoM aim ray (`AOM\AomBridge.cs:78-124`): the client's own `XRRayInteractor` nearest the Bridge hand (< 0.5 m,
`best > 0.25f` sqr), ray from `rayOriginTransform` (fallback `transform`) forward. ETS measured the steering-wheel grab
in rig space: `inRig = root.InverseTransformPoint(hand.position)` with `root = Body.Root`
(`ETS\EtsSteeringGrab.cs:400-428`), because the seat moves the rig in LateUpdate.

### 2.3 Recommendation for held objects (Rec Room tools, balls, guns)

1. **Primary pose = Bridge `Body.LeftHand/RightHand`** (world). Cache the Transforms after
   `PlayersContainer.OnLocalPlayerAdded` / retry until non-null (Quake retries every frame).
2. **Hold frame**: a child GameObject we create under the Bridge hand anchor ("RR Grip L/R") with a
   per-controller-family local offset (calibrated once: see 9.6), and parent the held object's visual/root to it
   (kinematic while held). Parenting gives the before-render re-pose for free (no one-frame swim at 90 Hz) - the
   same reason the client hangs its interactors under the anchor. Unparent on release.
3. Throw velocity: sample the hold frame's world position/rotation in `FixedUpdate`/`LateUpdate` into a small ring
   buffer (last ~5 samples, ~60-80 ms) and release with the averaged linear + angular velocity; optionally add the
   device's `CommonUsages.deviceVelocity`/`deviceAngularVelocity` rotated by `toWorld.rotation` (tracking space)
   plus the rig's own velocity (player locomotion). GUESS: the ring buffer alone is enough and immune to rig moves
   made by teleports if samples are cleared on teleport.
4. Fallback when the Bridge hand is null (editor with a headset, or the client failing): Quake's converted device
   pose (2.2) with `GripToAim (90,0,0)` for aiming.
5. Aim rays for guns: the held gun's own muzzle transform forward (Rec Room aims down the gun barrel; there is no
   separate aim pose). For UI pointing, keep the client's own ray (conventions 11.3).

### 2.4 Timing
- Read hands in `LateUpdate` with a late `[DefaultExecutionOrder(10000)]` (Quake's `Q3CameraControl` uses 10000 so it
  runs after Cinemachine/client LateUpdates). If we move the rig (seat, kart), move it first, then read the hands
  (ETS: `EtsTruckSeat.LateUpdate` calls `Wheel.AfterRigPlaced()` at its end, `EtsTruckSeat.cs:861-869`).
- Physics-held objects (joint/velocity tracking instead of parenting) must be driven in `FixedUpdate` from the last
  hand pose; parenting is preferred for anything that does not need to collide while held.

## 3. Somnium's own grabbing of world objects and how to avoid fighting it

### 3.1 The client grabs only XRI interactables, never plain rigidbodies

Per hand there are three interactors (`Player.XR.Body.prefab`, and the ETS summary `ETS\EtsSteeringGrab.cs:14-21`):

| Interactor (prefab) | Type | Interaction layers (XRI) | Near cast | Far cast |
| --- | --- | --- | --- | --- |
| `Grab Right` / `Grab Left Variant` (`PFB\XR\Grab Right.prefab`) | NearFarInteractor, select on grip | `m_Bits: 8` = layer 3 **Grabbable** (`:439-440`) | sphere r=0.075 m on physics layers **0, 7, 14, 23** (`m_PhysicsLayerMask 8405121`, `:665-669`) | ray 1 m on physics 0, 7, 23 (`8388737`, `:687-694`) |
| `Near-Far Interactor Right` / `...Right Left` | NearFarInteractor (UI + pointer) | 4 XRInteractable + 7 XRRay (144) | sphere 0.15 m on physics layer 6 | 5 m on 0,5,6,10,14,16,23 (conventions 11.3) |
| `Climb Grab Right` / `...Left Variant` | XRDirectInteractor | 1 Climbing | trigger capsule | - |

XRI interaction layer names (`CLIENT\XRI\Settings\Resources\InteractionLayerSettings.asset`): 0 Default, 1 Climbing,
3 Grabbable, 4 XRInteractable, 5 GrabbableObject, 6 PointCollider, 7 XRRay, 8 Inspectable, 31 Teleport.

The client adds `XRGrabInteractable` only to its own things (`Games/Interactions/Scripts/Initializers/
InteractableInitializer.cs:52-67` - inventory/built-in items, `GolfBall`, `BowlingBall`, `TabletGrabInteractable`,
`XROffsetGrabbable`). No code scans world objects by physics layer: the only `LayerMask.NameToLayer` uses in the client
are listed in 3.4, none touches `GrababbleObject` (10) or `GrabHand` (13). The Inspector adds an `XRSimpleInteractable`
with Inspectable/Teleport/XRInteractable layers to inspectable targets (`Inspector/Scripts/Helper/ObjectHighlighter.cs:288-356`),
not to arbitrary world props.

**So a world prop with a Rigidbody + Collider and no XRI interactable is never selected by the client's grip.**
Physics layers 10 "GrababbleObject" and 13 "GrabHand" are just names in the collision matrix today.

### 3.2 The physical hand boxes (layer 7) DO touch world rigidbodies

`PFB\XR\Hand Collider Right.prefab` (+ Left Variant), instanced under each `*HandOffset`:
- GameObject on **layer 7** (PhysicalObject), dynamic `Rigidbody` mass **1**, no gravity, not kinematic,
  `excludeLayers = 4` (= layer 2, the avatar capsule) (`:136-154`);
- `BoxCollider` 0.05 x 0.2 x 0.2 m (`:192`), not trigger, excludes layer 2;
- `ConfigurableJoint` to the kinematic `Hand Collider Anchor` (layer 7, child of the hand anchor), linear drives
  spring 2000, damper 10, **max force 150 N**, angular locked;
- `XRHandCollider` (`CLIENT\_SomniumSpace\XR\Scripts\XRHandCollider.cs`): snaps back if > 0.3 m from the anchor
  (`:43-49`); when the Grab interactor selects something the hand's mass drops to 0 instantly and fades back to 1 kg
  over 0.5 s after release (`:52-84`).

Layer 7 collides with 0, 2, 3, 5, 6, 7, 10-19, 23, 24, 26-29, 31 (conventions 12). So **any prop of ours on those
layers gets shoved by the player's hands** (up to 150 N), including the prop we are holding if its collider stays
solid. That is actually Rec Room-like for loose props (you can bat a ball), but it must not act on the held one.

### 3.3 Rules for our grab system (so it never fights the client)

| Rule | Why |
| --- | --- |
| Do NOT add XRGrabInteractable/XRSimpleInteractable with interaction layer 3 (Grabbable) to our props | then the client's Grab interactor would select them on grip, its hand box would drop to 0 kg, `XRHandCollider` and haptics would react, and two systems would move the object |
| Our grab = our own code reading the grip (`EtsVr.GripLeft/Right`, hysteresis 0.55 on / 0.35 off like `EtsSteeringGrab.cs:52-54`) and an overlap test around the Bridge hand | nothing to fight: the client's grip selects nothing on our props (ETS proved this with the wheel) |
| While a prop is held, add layer 7 to every collider's `excludeLayers` (OR in the bit `1 << 7`) or make it kinematic + trigger; restore on release | the 1 kg hand box would otherwise push the held prop / jitter against it |
| Free props: pick a layer by what must hit them (table below) | |
| Held props: make their colliders **triggers** while held (the client's VR far caster ignores triggers, conventions 11.3, so a held prop on 10 does not swallow the UI ray; triggers are not shoved by the hand box). Exception: tools that must physically hit things (paddles, bats, shields): keep them non-trigger, kinematic, moved with `Rigidbody.MovePosition/MoveRotation` in FixedUpdate, with layer 7 excluded | layers 0,5,6,10,14,16,23 are in the VR far mask (5 m); a solid held prop on them sits in front of the ray |

Layer choice for free (not held) Rec Room props (from the conventions 12 matrix):

| Layer | Hit by hand boxes (7) | Hits avatar capsule (2) | Hits floors on 0 / 23 | Client VR far pointer | Desktop pointer (0,1,4,5,14,16) | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| 0 Default | yes | yes | yes / yes | yes | yes | avoid: everything clicks/pushes it |
| 10 GrababbleObject | yes | **no** | **no** / yes | yes | no | good for throwables if floors are on 23 (players walk through props, no capsule shove) |
| 16 BuildingItem | yes | yes | yes / yes | yes | yes | use only for clickable handles (conventions 11.3) |
| 26 Bullet | yes | **no** | yes / yes | no | no | good for balls/projectiles (dodgeballs, paintballs) that must hit floors on 0 and hit-boxes on 27 |
| 29 (unnamed) | yes | yes | no / yes | no | no | possible |
| 17 Parcel | yes | no | yes / yes | no | no | possible, but the name suggests client use; avoid |

GUESS (to test): **props on 10 with room floors/walls on 23**, balls on 26; hit-boxes for players on 27.

### 3.4 Client code that reads layers by name (complete list, `grep NameToLayer CLIENT\_SomniumSpace`)
`ObjectHighlighter.cs:297-298,355` (interaction layers), `GenericItemObjectReference.cs:124` (interaction layer),
`PlayerHapticsManager.cs:37` ("XRButton" physics layer name - does not exist in this project, so never true),
`MotionFeature.cs:185` (COLLISION_PLAYER_LAYER_NAME), `SitInteraction.cs:12` ("UI"), `CameraCapture.cs:38-41`,
`TabletOverlayLayerAssigner.cs:38`.

## 4. Grip / trigger / buttons in VR

### 4.1 Reading (EtsVr.Poll, `ETS\EtsVr.cs`)

Use `UnityEngine.XR.InputDevices` + `CommonUsages` (never the Input System package; conventions 11.2). Exact code to
copy (`EtsVr.cs:94-164, 206-218`):

```csharp
public static bool Present => XRSettings.isDeviceActive && !string.IsNullOrEmpty(XRSettings.loadedDeviceName); // :45
private const float DeadZone = 0.15f;                                                                          // :47

private static void Poll()                       // :94, once per frame (guarded by _polledFrame == Time.frameCount)
{
    ...
    if ((!_left.isValid || !_right.isValid) && Time.unscaledTime >= _nextSearch)
    {
        _nextSearch = Time.unscaledTime + 1f;
        _left = First(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left);
        _right = First(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right);
    }
    if (_left.isValid)
    {
        if (_left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick) && Mathf.Abs(stick.x) > DeadZone)
            _steer = Mathf.Sign(stick.x) * Mathf.InverseLerp(DeadZone, 1f, Mathf.Abs(stick.x));
        _brake = Trigger(_left);
        _gripLeft = Grip(_left);
        Edge(_left, CommonUsages.primaryButton, ref _menuHeld, ref _menuPressed);
        Edge(_left, CommonUsages.secondaryButton, ref _seatHeld, ref _seatPressed);
    }
    ...
}
private static float Trigger(InputDevice device)                                                              // :144
{
    float value = device.TryGetFeatureValue(CommonUsages.trigger, out float pulled) ? pulled : 0f;
    return value > 0.05f ? value : 0f;
}
private static float Grip(InputDevice device)                                                                 // :154
{
    float value = device.TryGetFeatureValue(CommonUsages.grip, out float squeezed) ? squeezed : 0f;
    if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool down) && down) value = Mathf.Max(value, 1f);
    return value;
}
public static void Pulse(bool right, float amplitude, float seconds)                                          // :167
{
    InputDevice device = right ? _right : _left;
    if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
        device.SendHapticImpulse(0u, amplitude, seconds);
}
private static void Edge(InputDevice device, InputFeatureUsage<bool> usage, ref bool held, ref bool pressed)  // :206
{
    bool now = device.TryGetFeatureValue(usage, out bool down) && down;
    pressed = now && !held;
    held = now;
}
```

Also read for throwing: `CommonUsages.deviceVelocity` / `deviceAngularVelocity` (tracking space; rotate by
`toWorld.rotation`, 2.2). Log once what the device reports (`EtsVr.DescribeGrip`, `EtsVr.cs:187-204`;
`Q3Vr.Report`, `Q3\Q3Vr.cs:181-211`) - AoM lost a feature silently by reading only `triggerButton`
(`AOM\AomVr.cs:1361-1371`). Quake fires on `trigger > 0.5f || triggerButton` (`Q3\Q3Vr.cs:411-412`).

### 4.2 What the client already binds in VR (so what is free)

From `CLIENT\_SomniumSpace\Inputs\VR\XRI Player Interactions.inputactions`, `XRI Player Generic Inputs.inputactions`,
`XRI PopUpMenu.inputactions`, `XRI Input Actions.inputactions` (dumped with `player_probe/ia.pl`):

| Control | Client action | Usable by our world? |
| --- | --- | --- |
| Left stick | Locomotion (move), teleport-mode sectors | only if we zero locomotion (seated/karts) |
| Right stick | Snap turn (45 deg) / continuous turn (60 deg/s) / Teleport (up) | turning keeps working even at SetMovementSpeed 0 |
| Left stick click | LeftMuteMic | no |
| Right stick click | ThirdPersonSwitchToCameraMode | no |
| Right A (primaryButton) | **Jump** | keep as jump (Rec Room's A is jump too) |
| Left X (primaryButton) | Inspector (hold), Mute/Hide (multi-tap), Cancel (ends a pose), pop-up Back | no |
| Right B / Left Y (secondaryButton) | Tablet (short tap), PopUp menu, pop-up Quit | no |
| Menu buttons | client menu | no |
| Triggers | LeftHandClickUI/RightHandClickUI (UI press through the Near-Far interactor), Left/RightActivate | **yes** (the client only acts when its ray is on UI/XRI objects) |
| Grips | Left/RightGrab (XRI select - only XRI Grabbable interactables, 3.1) | **yes** |
| Both sticks (pop-up open) | quick action menu selection (AoM finding) | no |

**So a Rec Room world gets grip and trigger per hand, plus gestures.** That matches Rec Room VR itself: grip = grab
(hold), trigger = use the held tool, A = jump (Rec Room help centre and fandom "Basic Controls"; see sources).

### 4.3 Proposed Rec Room VR mapping

| Rec Room action | VR input | Notes |
| --- | --- | --- |
| Grab / hold | grip past 0.55, released below 0.35 (hysteresis from `EtsSteeringGrab.cs:52-54`) | overlap sphere r~0.08 m at the hold frame (2.3) against our grabbable colliders; prefer the nearest; `Tool.interactionSettings.LockOnPickup` 1 = snap to the pickup transform, 0 = keep the grab offset (see 4.4) |
| Drop / throw | grip release | release velocity from the hold-frame history (2.3), times `ThrowSpeedMultiplier`, clamped by `MaxThrowSpeed` when `HasMaxThrowSpeed` |
| Use / fire / drink | trigger (> 0.5 or triggerButton) while holding | when NOT holding, leave the trigger to the client's UI pointer. When the trigger that pressed UI is also a fire trigger, wait for its release first (conventions 11.3) |
| Two-handed tools (shotgun pump, bow) | second hand's grip near the tool's secondary grip point | our code |
| Holster (hip/shoulder) | release the tool near a slot volume following `Body.Root`/`Body.Head` | `HasHipEquipSlot`, `HipEquipSlotPosition`, `ShoulderEquipSlotPosition` per tool (4.4) |
| Watch menu | turn the left wrist up (AoM `Arm` gesture, 2.2) and press with the right trigger/ray | Y/B belong to the client's tablet |
| Jump | client's right A | unchanged |
| Haptics | `EtsVr.Pulse(right, amp, secs)` on grab (0.3, 0.05 s), fire (0.5, 0.03 s), hit | |

### 4.4 Rec Room's own per-tool hold data (read from the bundles, for our grab code)

Every Rec Room tool has `Tool.interactionSettings` (MonoBehaviour `Tool`, read with our parser). Measured examples:

| Prefab (bundle, pathId) | LockOnPickup | Handedness | Right/LeftHandPickupTransform (pos; rot) | Throw | Equip slots |
| --- | --- | --- | --- | --- | --- |
| `(PaintballGun)` (`SP\research\shoot\PaintballGun.txt`, line 9) | 1 | 2 | (0, 0.0722572, 0.0119958); (x 0.173648, w 0.984808) = 20 deg about X | ThrowSpeedMultiplier 1, HasMaxThrowSpeed 0, MaxThrowSpeed 10 | hip (0,0.029,-0.079), shoulder (0.00128578,0.277154,-0.022) |
| `(DodgeballBall)` (`43ea1176ca25c0333547813ec3298808`, `-1619130950701524752`) | 0 | 2 | (0,0,0); identity | multiplier 1, **HasMaxThrowSpeed 1, MaxThrowSpeed 15** | - |

Other fields present: `CanBePickedUp`, `ShowHandWhenHeld`, `FullBodyLeft/RightHandGripTransform`,
`HandVisualPickupInfo`, `CanBeEquipped`, `CanHolderHandClimb`, `RigidbodyPickup.interpolateOnPickup`.
Semantics (GUESS, to calibrate): with LockOnPickup = 1 the tool snaps so that
`tool.world = gripFrame.world * Inverse(TRS(PickupTransform.Position, PickupTransform.Rotation))`, where `gripFrame`
is our hold frame (2.3). LockOnPickup = 0 keeps the offset at the moment of grabbing. Calibrate `gripFrame`'s local
rotation under the Bridge hand once with the paintball pistol: its barrel must lie along the client's pointer ray
(`AomBridge.Pointer`, 2.2). Handedness enum values are unknown (2 on both samples; GUESS 2 = either hand).

## 5. Desktop (non-XR) scheme

### 5.1 What the desktop client does with the mouse (measured)

- Default camera state is third person (`ThirdPersonCameraState.Process`,
  `CLIENT\_SomniumSpace\Player\Scripts\Player\Features\Additional\CameraStates\ThirdPersonCameraState.cs`):
  cursor visible unless middle (First Camera Action) or **right** (Second Camera Action) mouse is held; while held the
  mouse delta rotates the camera, and right-hold also turns the body toward the camera; middle+right = walk forward.
- Zoomed fully in = first person (`FirstPersonCameraState.cs`): cursor hidden (`Cursor.visible = false`, NOT locked),
  mouse always looks, body follows camera.
- The cursor is never locked by the client (`NonXRInteractionControl` locks it but is not on any prefab - guid
  `4c7a9c7450c5c0648ba7c107562ae5b1` has no users). Lock only ever comes from worlds (Quake).
- Left click = UI `Click` (`2D Input Actions.inputactions`, map UI) through the EventSystem (PhysicsRaycaster mask
  0,1,4,5,14,16, conventions 11.3) and `MouseHoverRaycaster` on the camera (`CLIENT\_SomniumSpace\Inspector\Scripts\Core\MouseHoverRaycaster.cs:25-51`):
  raycast from the mouse position, 25 m, layers **5, 6, 7** (`_clickableLayers m_Bits 224`,
  `PFB\NonXR\Body\CameraSystem.prefab:1079-1081`), calls `IMouseHoverHandler` (the client's inspector highlighters;
  their click acts only while Inspector mode (I) is on for ACTIVATION type, `InspectorBase.cs:197-205`).
  **Consequence: a left click for "use/fire" does nothing on the client side** unless it lands on a client UI or an
  inspectable in inspector mode. Keep our props off layers 5, 6, 7 so they are never hover targets.
- Desktop movement: `NonXRMovementProvider` (`CLIENT\...\Controls\NonXR\NonXRMovementProvider.cs:95-122`): Q/E rotate
  the body (`RotationSpeed 110` deg/s) with `transform.Rotate`, then `CharacterController.Move(body.rotation * input)`.
  Settings `CLIENT\_SomniumSpace\Player\Configs\Additional\NonXR.Movement.Settings.asset`: HorizontalSpeed **1.6 m/s**,
  SprintSpeed **6 m/s** (Shift), JumpHeight 2, GravityMultiplier 2. Movement is in the BODY frame, so whoever owns
  `Body.Root`'s yaw owns the walking direction.
- Desktop camera: `CameraSystem/Camera` (tag MainCamera, layer 24, Cinemachine lens FOV **40**, near **0.1**,
  `CameraSystem.prefab:379-382`), driven by a `CinemachineBrain` in LateUpdate.

Full desktop key list the client binds (all `.inputactions` + direct `Input.GetKey` reads in `CLIENT\_SomniumSpace`):
W A S D / arrows, Q E, Space, Shift, F, I, M, R, Tab, X (tap/multi-tap), Escape, mouse left/right/middle/scroll;
vehicles add Alt; `Ctrl+Shift+D` and `Ctrl+F+S` are debug combos (`Network\Scripts\NetworkGUIUtility.cs:56-61`);
G only in XR (`XRAFKModeFeature.cs:40`). Free: C, V, G, H, Z, B, N, T, Y, U, O, P, digits, F1-F12, Ctrl.

### 5.2 How Quake did desktop shooting (quote)

- Fire key `Mouse0` (left click) (`Q3\Q3Combat.cs:33`; scene `Somnium_WorldLounge.unity:1299` `_fireKey: 323`),
  held = auto fire (`Q3Combat.cs:1101-1107`: `bool firing = Input.GetKey(_fireKey) || (inVr && vr.Fire) || ...`).
- Aim = the camera: `Q3Combat.cs:1479-1483` ("Where shots go. Taken from the camera rather than the avatar's head, because
  mouse look is now driven directly onto the camera").
- Cursor LOCKED while playing: `Q3FirstPerson.ApplyUnityFallback` (`Q3\Q3FirstPerson.cs:219-233`) - the reflection
  route into the client's `ICameraStateFuncs` fails because the shipped client is obfuscated, so it sets
  `Cursor.lockState = Locked; Cursor.visible = false` every frame unless paused, and gives the cursor back in
  `OnDisable` (`:213-217`). Quake's menu frees it (`Q3Menu.Pause`, `Q3\Q3Menu.cs:922-943`).
- Own mouse look and first-person camera: `Q3CameraControl.LateUpdate` with `[DefaultExecutionOrder(10000)]`
  (`Q3\Q3CameraControl.cs:31, 246-407`): `_yaw += Input.GetAxisRaw("Mouse X") * 2; _pitch -= Input.GetAxisRaw("Mouse Y") * 2;`
  pitch clamped to +-89; `view.rotation = Euler(pitch, yaw, 0)`; `view.position = head.position + forward*0.12 + up*0.02`
  (scene: `_forceFirstPerson: 1`, `_ownMouseLook: 1`, `Somnium_WorldLounge.unity:1331-1336`). In VR none of this runs.
- Better way to own the desktop camera (ETS): disable the camera's CinemachineBrain found by name, write the pose/FOV
  in LateUpdate, re-enable on release (`ETS\EtsTruckCamera.cs:252-284`):
  `_brain = _camera.GetComponent("CinemachineBrain") as Behaviour; if (_brain.enabled) _brain.enabled = false;`
  (no Cinemachine assembly reference needed; it is not on the server's list).

### 5.3 Rec Room's real screen-mode controls (web + bundle data)

Web (defkey.com Rec Room PC list, Rec Room blog "Updating our Screens Controls" 2022-05-26): WASD move, mouse look,
**E interact/grab, Q drop, left click primary action (use/fire/throw), right click secondary / aim down sights**,
Shift sprint, Ctrl crouch (toggle) or slide while sprinting, Space jump (double = fly where allowed), Tab watch,
1/2/3 backpack slots, 4 backpack menu, V third-person toggle, C emote, T mic, Y room chat, F/G + mouse wave, a
dedicated reload button (key not published; GUESS R).

Bundle data confirms the action set and press types: `RecRoom.Inputs.ScreenControlsMap` (bundle
`063b6b6653aa642616fd21d3d4701899`, pathId `-56959186072725697`, dump `SP\research\dumps\_screencontrolsmap.json`):
`globalControlSettings {maxDoublePressDelay 0.33, minLongPressDelay 0.33}`; controls "Smart Interact Button",
"Smart Drop Button" (keyboard press type 3 = tap), "Smart Toss Button" (type 1 = long press), per hand "Tool Primary
Action", "Tool Secondary Action" (+ Long Press), "Tool Reload", "Drop" (tap) / "Underhand Toss" (long press),
"Equipment Slot 1-4", "Watch Menu", "Jump", "Fly" (type 2 = double press), "Crouch", "Prone" (long press),
"Auto Sprint", "Toggle Third Person" (tap), "Exit Seat" (long press), Vehicle Forward/Reverse/Left/Right/Handbrake/Boost.
(Key codes live in Rewired action ids, not readable.)

Screen tool behaviour configs (ScriptableObjects, `RecRoom.Core.Controllers.ScreenBehaviors.*`; list:
`rrtool script "ScreenToolBehaviorConfig"`), values to reuse:

| Config (bundle, pathId) | Key values |
| --- | --- |
| `ThrowToolBehaviorConfig` (`1df8e0efac4f496ebc58d7e6f86480c3`, `-6877966307972784836`) | throwSpeed **8** m/s, throwAngularSpeed 360 deg/s, **throwTowardsGazeTarget 1**, pitch by gaze pitch = identity (-90..90) |
| `ThrowDodgeballToolBehaviorConfig` (`43ea1176...`, `-8825024476658176348`) | charge **0.1-0.5 s** (chargeCurve 0->1), throwSpeed **14**, throwGravityCompensationAngle 7 deg, targetDistanceThreshold 9 m, maxThrowCurveAngle 30 deg |
| `PaintballPistolScreenToolBehaviorConfig` (`43ea1176...`, `5673056502889006777`) | shootTowardsGazeTarget 1, supportsAimDownSights 1, maximumRateOfFire 20, continuousFireRate 3.5/s (held), recoilAngle (0,-0.5), recoilDuration 0.066 s |
| `FirstPersonScreenPlayerCameraSettings` (`1df8e0ef...`, `-4709700083522651184`) | pivot = root + (0, **1.35**, -0.08); camera = pivot + (0, 0.2, 0.09) (eye 1.55 m); FOV x1 |
| `FPS_Sprint_CameraSettings` / `FPS_Slide_CameraSettings` (`063b6b66...`, `8775679203625615378` / `-3257454483062476569`) | same offsets, pivot y 1.4, **FOV x1.15 sprint, x1.3 slide** |
| `ThirdPersonOverShoulderCameraSettings` (`1df8e0ef...`, `-6389420614606355366`) | pivot root + (0,1.6,0); camera pivot + (0.4875, 0, -1.8); FOV x0.85; min pitch from up 30 deg |
| `DefaultGameplayReticleCursorSettings` (`1df8e0ef...`, `3433996988778886315`) | sprite `Screens_Crosshairs_Center` (`1df8e0ef...`, `-3947755791048088780`), 0.01 of the viewport, pivot centre; also `Screens_Crosshairs_Circle/Mouse/Tick` in the same bundle |

Other configs worth dumping per activity: `ThrowDiscGolfDiscToolBehaviorConfig` (`1df8e0ef...`, `3372446327481663969`),
`ThrowBowlingBallToolBehaviorConfig` (`43ea1176...`, `-2239386326294135621`), `PaddleToolBehaviorConfig`
(`43ea1176...`, `-2562427756441507569`), `SteeringWheelScreenToolBehaviorConfig_Kart` (`43ea1176...`, `370115238501748491`),
`TennisballTossScreenToolBehaviorConfig` (`43ea1176...`, `1439330303247766160`), `QuestSwordScreenToolBehaviorConfig`
(`1df8e0ef...`, `-5707674973637331004`), `LongbowScreenToolBehaviorConfig` (`43ea1176...`, `6530991509869579653`).

### 5.4 Proposed Dreamport screen mode (desktop)

Two layers:

**A. "Play mode" (while the player is inside an activity instance and no menu/tablet is open)** - Rec Room screen
mode, built like Quake + ETS:
- Own the camera: disable `CinemachineBrain` (ETS), place `Camera.main` at the Rec Room first-person eye each LateUpdate
  (`[DefaultExecutionOrder(10000)]`): `eye = Body.Root.TransformPoint(0, 1.55*scale, 0.01)` or, better, the Bridge
  `Body.Head.position` + head-forward 0.12 m (Quake, avoids seeing inside the avatar's head; the avatar is never
  modified). Own mouse look (yaw/pitch from `Mouse X/Y`, sensitivity ~2, pitch +-89). FOV: set vertical FOV ~60 (GUESS;
  the client's 40 is a zoomed third-person lens), sprint x1.15, slide x1.3 (Rec Room). Near clip 0.03 while held
  objects sit close (GUESS). Restore FOV/near/brain on exit.
- Own the body yaw: write `Body.Root.rotation = Euler(0, yaw, 0)` every LateUpdate. This makes WASD walk where the
  player looks AND neutralises the client's Q/E rotation (the provider's `Rotate` in Update is overwritten), which
  frees **Q and E** for Rec Room's drop and interact.
- Lock the cursor (`Locked`, invisible) every frame (Quake fallback), draw our crosshair (Rec Room sprite) at the
  centre. Right mouse (client Second Camera Action) makes the client re-show the cursor on release
  (`SetCursorViewActive`), which the per-frame re-lock undoes.
- Aim ray = `cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))`; throws and shots go **toward the gaze target**
  (Rec Room `throwTowardsGazeTarget`/`shootTowardsGazeTarget`): raycast the aim ray (our layers + floors), then launch
  from the held object's position toward the hit point (or 30 m along the ray).
- Held objects are posed in front of the camera: right-hand tool at `cam.TransformPoint(0.22, -0.2, 0.45)`, left-hand
  at `(-0.22, -0.2, 0.45)`, rotated to `cam.rotation` then by the tool's pickup rotation (GUESS offsets; tune per
  tool family - Rec Room animates the avatar's arms instead). Held objects exclude layer 7 and become kinematic.
- Suspend play mode (free cursor, give the camera back) when: the Somnium tablet is open (Tab, detect with the
  tablet's Animator bool `IsOpen`, `CLIENT\_SomniumSpace\VRTablet\Tablet\Common\TabletTransitionControl.cs:37-51`; ETS
  finds it robustly in `EtsTabletLink.ReadTabletOpen`, `ETS\EtsTabletLink.cs:1315-1329`), our own menu/watch is open,
  Escape/I/R/M/X were pressed (client UIs), a TMP_InputField has focus (chat), or the application lost focus. Resume on
  the next left click that is not over UI (`EventSystem.current.IsPointerOverGameObject()` false).

**B. Outside play mode (lobby, walking around)**: leave the client's camera and cursor alone. Picking up a prop =
left click ON the prop (raycast from `Input.mousePosition` through `Camera.main`), which also enters play mode.

Key map for play mode:

| Rec Room action | Rec Room key | Dreamport key | Clash analysis |
| --- | --- | --- | --- |
| Interact / grab (dominant hand) | E | **E** | client Rotate+ neutralised by our yaw write (play mode only) |
| Drop | Q tap (< 0.33 s) | **Q tap** | client Rotate- neutralised likewise |
| Underhand toss | Q hold (>= 0.33 s) | **Q hold**, toss 3-4 m/s forward-up (GUESS) | |
| Use / fire / throw (charge for dodgeball 0.1-0.5 s) | left click | **left click** | client UI Click: harmless on our props (5.1) |
| Secondary / aim down sights | right click | **right click** | client look/turn-to-camera; cursor re-locked each frame |
| Off-hand grab / use | (dual wield: Smart Dual Wield) | **G** grab to left hand, left click + Alt? (GUESS: skip dual wield on desktop at first) | |
| Reload | dedicated (R?) | **Z** (R is the client's PopUpMenu) | |
| Holster slots | 1 2 3 (4 menu) | **1 2 3 4** | free (ETS uses 1/2 only inside its truck) |
| Watch menu | Tab | **B** or the Somnium tablet app (ETS pattern) - Tab is the client's tablet | decided by the watch-UI research |
| Jump | Space | client Space | same (desktop jump apex ~2 m) |
| Sprint | Shift | client Shift (6 m/s) | same |
| Crouch / slide | Ctrl | **LeftControl** (ours) | free; slide needs own movement (6.4) |
| Third-person toggle | V | **V**: gives the camera back to the client (orbit) but keeps the crosshair logic off | free |
| Client interact | - | F stays the client's | never use F |

## 6. Locomotion control, seats/vehicles, freezing the player

### 6.1 Bridge API (exact signatures, `SP\research\bridge_decomp\SomniumSpace.Bridge.Player.Components\*.cs`)

```csharp
ISomniumPlayer p = SomniumBridge.PlayersContainer?.LocalPlayer;
ISomniumPlayerMotion m = p?.Features?.Motion;
void DoTeleportToPoint(Vector3 position, Vector3 rotation /*Euler degrees, pass (0,yaw,0)*/, Action done = null);
void SetMovementSpeed(float nonXRSpeed, float locomotionSpeed, float nonXRSprintSpeed); // multipliers
void SetGlideSpeed(float speed);
void SetGravity(float nonXRGravity, float XRGravity);
void SetJump(float nonXRJump, float XRJump);
void SetTeleportation(float distance, float cooldown, Gradient rayGradient);
void SetTeleportationDisableState(bool isDisabled);
void SetCollisionDisableState(bool isDisabled);
void SetFlyModeDisableState(bool isDisabled);
void SetGlideDisableState(bool isDisabled);
p.Features.Poses.SetPose(PoseType pose);            // None=-1, SittingStraight=0, SittingCrossedLegs, Meditation,
                                                    // SittingKneesApart, Kneeling, Squat, ProneForward, ProneRelax,
                                                    // LayDownBack, SittingFloor, SittingCoolOnFloor
p.Features.Animation.SetFallingAnimationDisabled(bool isDisabled);
p.Features.Avatar.SetVRIKLegMovementDisabled(bool isDisabled);  // + event Action OnAvatarChanged
p.Features.Scale.SetHeight(float nonXRHeight, float XRHeight);
p.Features.Info.SetPlayerTagDisableState(bool isDisabled);      // hides other players' name tags
```

What the client does with them (`CLIENT\_SomniumSpace\Bridge\Scripts\Components\Player\BridgePlayerMotion.cs:19-67`,
`...\Features\NonXR\NonXRMotionFeature.cs:87-104`, `...\Features\XR\XRMotionFeature.cs:203-265`,
`...\Features\Common\MotionFeature.cs:110-137,180-191`):

| Call | Desktop effect | VR effect |
| --- | --- | --- |
| SetMovementSpeed(a, b, c) | walk 1.6 x a m/s, sprint 6 x c m/s | continuous move 3 x b m/s (`Direct3DMoveProvider m_MoveSpeed 3`, `PFB\XR\Player.XR.Controls.Movement.prefab:520`); **snap/continuous turn unaffected** (45 deg, 60 deg/s, `:335,:383`) |
| SetGravity(a, b) | extra gravity multiplier applied only while falling (base x2) (`NonXRMovementProvider.cs:173-176`) | locomotion gravity multiplier |
| SetJump(a, b) | jump velocity scale (`NonXRMovementProvider.cs:69`) | locomotion jump multiplier (`_jumpHeight 2`, `:555`) |
| SetTeleportationDisableState | no-op (base) | enables/disables the teleport input provider |
| SetCollisionDisableState(true) | ORs layer 2 ("Ignore Raycast") into the CharacterController's `excludeLayers` = players stop bumping each other; the world still collides | same |
| DoTeleportToPoint | **asynchronous**: `DoBeforeTeleport`, wait end of frame, move, wait end of frame, `DoAfterTeleport` (`MotionFeature.cs:125-137`) - use the `done` callback | same (levels the rig) |

All multipliers are clamped to **0..10** (`SomniumPlayerRulesBase.ClampValue`, decompiled `:43-46`). 1 = normal.
There is no "add velocity/impulse to the player" call in the Bridge (`MotionFeature.MoveBodyByVector` exists in the
client but is not exposed).

### 6.2 Freeze / restore (proven sequence, ETS `EtsTruckSeat.cs:395-409` and `:1356-1366`; Quake `Q3Player.cs:275-304, 404-420`)

```csharp
// freeze
m.SetCollisionDisableState(true);  m.SetFlyModeDisableState(true);  m.SetTeleportationDisableState(true);
m.SetMovementSpeed(0f, 0f, 0f);    m.SetGravity(0f, 0f);             m.SetJump(0f, 0f);
p.Features.Animation?.SetFallingAnimationDisabled(true);  // else a gravity-less avatar shows the falling pose
p.Features.Avatar?.SetVRIKLegMovementDisabled(true);      // VR: no IK stepping while carried
// restore (multipliers, so 1 is normal)
m.SetCollisionDisableState(false); m.SetFlyModeDisableState(false); m.SetTeleportationDisableState(false);
m.SetMovementSpeed(1f, 1f, 1f);    m.SetGravity(1f, 1f);             m.SetJump(1f, 1f);
p.Features.Animation?.SetFallingAnimationDisabled(false); p.Features.Avatar?.SetVRIKLegMovementDisabled(false);
```
Restore in `OnDisable` too (leaving the world mid-game). Never leave the multipliers at 0 on an exception path.
The VR right stick still turns the rig while frozen; a seat that writes `Body.Root.rotation` every LateUpdate simply
overrides it.

### 6.3 Seated / vehicle pattern for Rec Rally karts (ETS `EtsTruckSeat`, `ETS\EtsTruckSeat.cs`)

1. Get in: cache `Body.Root/Head/LeftHand/RightHand`, Motion/Animation/Poses/Avatar (`:361-375`), freeze (6.2), make
   the vehicle's colliders ignore the avatar capsule (ETS: the Bridge collision switch does not stop the capsule
   pushing the truck, `:19-22`; passengers add the Vehicle layer to their own capsule's `excludeLayers`, `:1188-1200`).
2. Every `LateUpdate` (`:826-890`): `KeepSitting()` = `Poses.SetPose(PoseType.SittingStraight)` each frame (the client
   drops poses on its cancel action - left X in VR, `PosesFeature.CheckQuitInput`); then
   `_root.position = kart.TransformPoint(seatLocal); _root.rotation = heading(* smoothed tilt)`.
3. VR: `PlaceHead(eye)` (`:1129-1168`) shifts the rig so the camera lands on the driver's eye, keeping the lean inside a
   0.45 m limit, re-taking the rest pose when the head is > 0.7 m from it. Then read hands (steering wheel grab reads
   hands in rig space AFTER the rig moved, `EtsSteeringGrab.cs:23-34, 400-428`).
4. Desktop: the pose lowers `HeadCustomPoseOffset`; `_root.position += eye - _head.position` when the pose has lowered
   the head (`:882-889`); the camera is ours (5.2, `EtsTruckCamera`).
5. Get out (`Release`, `:1321-1394`): `LevelRig()` (upright around the eyes, `:945-984`), `Poses.SetPose(PoseType.None)`,
   restore (6.2), then `DoTeleportToPoint` beside the vehicle.
6. Driver input: VR - triggers for throttle/brake, left stick steer (locomotion is zeroed so the stick is free),
   grip on the wheel rim (EtsSteeringGrab); desktop - WASD read with `Input.GetKey` (the client's walk is zeroed).
   Rec Room kart screen config to read: `SteeringWheelScreenToolBehaviorConfig_Kart`.

### 6.4 Stunt Runner (sprint, slide, wall-run, climb)

| Need | Available | Recommendation |
| --- | --- | --- |
| Sprint | desktop: client Shift sprint 6 m/s (x `nonXRSprintSpeed`); VR: no sprint in the client | desktop: keep Shift, tune with `SetMovementSpeed(1, v, s)`; VR: raise `locomotionSpeed` (e.g. 2 = 6 m/s) while "running" (GUESS: arm-swing detection from hand velocities, Rec Room style) |
| Jump height | `SetJump` multipliers | per course section |
| Slide (Ctrl while sprinting), wall-run, vault, launch pads, moving platforms | nothing in the Bridge | **Quake takeover** while on the course: zero the multipliers (6.2), **disable the CharacterController** (`Q3Player.DisableCharacterController`, `Q3\Q3Player.cs:326-351`: `_characterController = _root.GetComponentInParent<CharacterController>() ?? _root.GetComponentInChildren<CharacterController>(); _characterController.enabled = false;` - the CC ignores transform writes otherwise), run our own kinematic mover (capsule casts against our course colliders), write `Body.Root.position` each frame (VR: move the rig = Root; the camera rides on it), `SetFallingAnimationDisabled(true)` (a disabled CC reports "not grounded" forever). Restore everything on exit and teleport with `DoTeleportToPoint`. |
| Climbing walls (VR) | `SomniumClimbableSurface` (Bridge component): the client adds `ClimbBasedOnTeleportInteractable` with interaction layers Climbing+Teleport (`CLIENT\...\Bridge\Scripts\Components\Runtime\ClimbableSurface\BridgeClimbableSurface.cs:14-22`), grabbed by the "Climb Grab" interactors | only components **present in the scene at world load** are collected (`BridgeHandler.CollectComponents` = `FindObjectsByType<SomniumControlComponent>` at `WorldTransitionControl` load, `BridgeHandler.cs:67-71`); runtime `AddComponent` is never seen. So pre-author scene objects with `SomniumClimbableSurface` + colliders (GUESS: move/resize their existing BoxColliders at run time). Desktop has no climbing. |
| FOV kick | camera is ours in play mode | x1.15 sprint, x1.3 slide (Rec Room values, 5.3) |

A lighter alternative (GUESS, untested): keep the client's CC enabled, zero its speeds, and call
`CharacterController.Move(delta)` on the `Body.Root` CC ourselves each Update - collision against the world stays
the client's. Quake measured that transform writes are overruled by the CC (`Q3Player.cs:315-322`); `Move` calls
should compose, but the client's grounded/falling logic and network driver may fight. Test before relying on it.

## 7. Head pose / camera each frame; VR detection

```csharp
// VR present (EtsVr.cs:45; Q3Vr.cs:40-41)
public static bool Present => XRSettings.isDeviceActive && !string.IsNullOrEmpty(XRSettings.loadedDeviceName);

// The camera being rendered (EtsVr.cs:248-266): ask every time, the client's camera arrives late and can change
public static Camera Head()
{
    Camera main = Camera.main;
    if (main != null && main.isActiveAndEnabled) return main;
    foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        if (c.isActiveAndEnabled && c.targetTexture == null && c.stereoTargetEye != StereoTargetEyeMask.None) return c;
    return null;
}
```

| Question | VR answer | Desktop answer |
| --- | --- | --- |
| Where the eyes are | `EtsVr.Head()` = XR `Camera` (MainCamera, sibling of HeadAnchor under HeadOffset) | `Body.Head` (HeadAnchor on the avatar body) - the camera is an orbit camera behind it unless play mode owns it |
| Where the player looks (aim) | camera forward (head gaze) or the held tool / hand | `Camera.main` centre ray (`ViewportPointToRay(0.5,0.5)`) |
| The rig to move | `Body.Root` = `XR.Body` (XROrigin, CharacterController). Quake/ETS test `head.transform.IsChildOf(_root)` and fall back to `head.transform.root` (`EtsTruckSeat.cs:1138-1148`; `Q3CameraControl.cs:325-330`) | `Body.Root` = `NonXR.Body`; the camera is separate (`CameraSystem`) |
| Tracking-to-world matrix | `head.localToWorldMatrix * TRS(hmdPos, hmdRot, 1).inverse` (2.2) | n/a |
| When | `LateUpdate` at execution order 10000 for anything that follows the camera; the client's Cinemachine brain writes in its LateUpdate (order 100, `EtsTruckCamera.cs:252-259`) | same |

Head pose timing: the XR camera and HeadAnchor are posed by TrackedPoseDrivers (Update and BeforeRender). A
world-space HUD/reticle for VR should be parented to the camera (or placed in `Application.onBeforeRender`) to avoid
a frame of lag; desktop UI uses a screen-space canvas (conventions 10.1).

Scale: `PlayerBodyHolder.CurrentCameraHeight` exists client-side only; from a world use
`Body.Root.InverseTransformPoint(Body.Head.position).y` for the player's eye height (used by Rec Room-style
hit volumes and camera placement).

## 8. Remote players: body transforms, nicknames, ids

### 8.1 Identity (quote)

`CLIENT\_SomniumSpace\Bridge\Scripts\Components\Player\BridgePlayerProperties.cs:9-12`:
```csharp
public string NickName => _playerProperties?.NickName;
public string Id => _playerProperties?.UserId;
public int PhotonId => _network?.Creator.PlayerId ?? -1;   // the Fusion PlayerRef.PlayerId of the creator
public bool IsLocal => _isLocal;
```
`BridgePlayersContainer` (`...\BridgePlayersContainer.cs:41-80`): `LocalPlayer`, `AllPlayers` (a COPY list each call),
`PlayersCount`, `GetPlayerByID(string userId)`, `GetPlayerByPhotonID(int photonId)` (checks the local player first),
`GetPlayerByNickName(string)`; events `OnLocalPlayerAdded`, `OnPlayerAdded`, `OnPlayerRemoved`,
`OnPlayerAvatarChanged`, `OnPlayerOutfitPutOn/TakeOff`. So **PhotonId == the Fusion `PlayerRef.PlayerId` our
networking keys use** (`NetworkObject.StateAuthority.PlayerId`, as `EtsRiders.Bind` compares, `ETS\EtsRiders.cs:1047`).
Name lookup with cache and fallback: `EtsNet.NameOf` (`ETS\EtsNet.cs:200-238`, networking doc 4.11).

### 8.2 Remote body transforms (what exists per remote player)

| Remote player kind | Body.Root | Body.Head | Body.LeftHand / RightHand | Source prefab |
| --- | --- | --- | --- | --- |
| VR | `Root` (L2) | `HeadAnchor` (networked head) | `LeftHandAnchor`/`RightHandAnchor` (networked controller poses) | `Player.XR.Body.Simulator.prefab` |
| Desktop | `NonXR.Body` (L2) | `HeadAnchor` (child of the body at head height; only the root is networked: `Player.NonXR.Part.Network.prefab` has Body/Root only, plus "HeadOffset(Fake one)") | **null** | `Player.NonXR.Body.Simulator.prefab` |

The Fusion transforms behind them: `Player.XR.Part.Network.prefab` `Body/Root, Body/Head, Body/LeftHand,
Body/RightHand` (each `PlayerTransformNetworkObject` + a Fusion NetworkTransform); ETS binds those directly when it
needs Fusion's raw render poses (riding a moving truck), `ETS\EtsRiders.cs:1010-1066` (search
`FindObjectsByType<NetworkBehaviour>` once a second, match `nt.Object.StateAuthority.PlayerId` and parent name "Body").
For Rec Room games (players on static floors) the Bridge body transforms are enough.

Null handling: desktop hands are real C# nulls in builds - use explicit `!= null` checks, never `??` chains on Unity
objects (Quake's `body.RightHand ?? body.Head ?? body.Root`, `Q3\Q3Remote.cs:681`, relies on it; its `Draw` then
skips players whose `RightHand` is null, `:700-708`, so desktop players' weapons never drew).

### 8.3 Hit volumes (paintball, laser tag, dodgeball)

- Build our own hit boxes per remote player (never touch the avatar): a capsule from `Body.Root.position` up to
  `Body.Head.position + 0.12 up` (radius ~0.25) for the body and a sphere r 0.13 at `Body.Head` for the head; VR hands
  optional (spheres at `Left/RightHand`, r 0.08) - Rec Room paintball/laser tag count hits on body and head only
  (GUESS for hands). Put them on layer **27** (ShootingPlayer: collides only with 3, 6, 7, 26, 31) and shoot/throw with
  layer **26** (Bullet) bodies or raycasts masked to 27 (conventions 12). Note layer 7 (VR hand boxes) also hits 27:
  a player's own physical hands would bump their own hit box - make each hit capsule a **trigger** (`isTrigger = true`;
  raycasts then need `QueryTriggerInteraction.Collide`, bullets get `OnTriggerEnter`)
  or give it `excludeLayers = 1 << 7`.
- Update them in `FixedUpdate` (physics queries read collider poses at the physics step) or call
  `Physics.SyncTransforms()` after moving them in LateUpdate before an immediate raycast.
- Hits are decided by the shooter's client against what it sees (networking doc 5.x); the victim's authority accepts.
- The client already gives every player (local and remote) a trigger sphere r **0.18** on layer 15 at the head:
  `Trigger/PlayerTrigger` (`PlayerTrigger.cs:8, 41`: `public class PlayerTrigger : MonoBehaviour, ISomniumPlayerTrigger`,
  `public ISomniumPlayer Player => ManagerRef?.Bridge;`). A world collider on a layer that collides with 15 (0, 3, 4, 7,
  10, 15, 16, 17, 19-23, 28, 29, 31) gets `OnTriggerEnter(Collider c)` and can resolve the player with
  `c.GetComponent<ISomniumPlayerTrigger>()?.Player` - handy for zones (spawn rooms, goal areas, out-of-bounds) and for
  "which player walked in". It is head-sized, so not a body hit box.

## 9. Code snippets for implementers

### 9.1 Local player cache (retry until the body exists)

```csharp
using SomniumSpace.Bridge;
using SomniumSpace.Bridge.Player;
using SomniumSpace.Bridge.Player.Components;

public static class RrPlayer
{
    public static ISomniumPlayer Local => SomniumBridge.PlayersContainer?.LocalPlayer;   // null in the editor
    public static ISomniumPlayerBody Body => Local?.References?.Body;
    public static Transform Root => Body?.Root;
    public static Transform HeadT => Body?.Head;
    public static Transform Hand(bool right)
    {
        ISomniumPlayerBody b = Body;
        if (b == null) return null;
        Transform t = right ? b.RightHand : b.LeftHand;
        return t != null ? t : null;          // null on desktop
    }
    public static ISomniumPlayerMotion Motion => Local?.Features?.Motion;
}
```
Wrap Bridge calls in try/catch in the editor paths (no client). Subscribe to `PlayersContainer.OnLocalPlayerAdded` to
re-cache after an avatar/launch-mode change (the XR/NonXR body prefabs differ).

### 9.2 Hold frame and grab (VR)

```csharp
// once per hand, when the Bridge hand first appears
Transform anchor = RrPlayer.Hand(right);
var grip = new GameObject(right ? "Rr Grip R" : "Rr Grip L").transform;
grip.SetParent(anchor, false);                 // re-posed by the client before render
grip.localPosition = GripOffsetPos;            // calibrated (9.6), start (0,0,0)
grip.localRotation = GripOffsetRot;            // calibrated, start identity

// grab: grip value with hysteresis
bool down = held ? EtsVr.GripRight > 0.35f : EtsVr.GripRight > 0.55f;
if (down && !held)
{
    int n = Physics.OverlapSphereNonAlloc(grip.position, 0.08f, _hits, RrLayers.GrabbableMask, QueryTriggerInteraction.Collide);
    RrProp best = Nearest(_hits, n, grip.position);
    if (best != null) best.AttachTo(grip, best.LockOnPickup ? best.PickupPose : Relative(grip, best.transform));
}
// attach: rb.isKinematic = true; colliders -> isTrigger (or keep solid + excludeLayers |= 1 << 7 for paddles, 3.3);
// transform.SetParent(grip, true) then set
// localPosition/localRotation = inverse(pickup) when locked. Release: SetParent(null), restore, rb.linearVelocity = v.
```

### 9.3 Throw velocity history

```csharp
// in FixedUpdate while held (or LateUpdate with Time.deltaTime), per hand
_pos[_i] = grip.position; _rot[_i] = grip.rotation; _t[_i] = Time.time; _i = (_i + 1) % 6;
// on release: v = (newest.pos - oldest.pos) / (newest.t - oldest.t); w from the rotation delta (ToAngleAxis)
v *= tool.ThrowSpeedMultiplier; if (tool.HasMaxThrowSpeed) v = Vector3.ClampMagnitude(v, tool.MaxThrowSpeed);
```
Clear the buffer on teleports/seat moves (rig jumps).

### 9.4 Desktop play mode skeleton (ETS brain hold + Quake look + lock)

```csharp
[DefaultExecutionOrder(10000)]
public sealed class RrScreenMode : MonoBehaviour
{
    Camera _cam; Behaviour _brain; float _yaw, _pitch, _fovWas, _nearWas; bool _on;
    public float FovKick = 1f;   // 1.15 while sprinting, 1.3 while sliding (Rec Room FPS_Sprint/Slide camera settings)
    public void Enter()
    {
        if (EtsVr.Present) return;
        _cam = EtsVr.Head(); if (_cam == null) return;
        _brain = _cam.GetComponent("CinemachineBrain") as Behaviour;
        if (_brain != null) _brain.enabled = false;
        _fovWas = _cam.fieldOfView; _nearWas = _cam.nearClipPlane;
        Vector3 e = _cam.transform.rotation.eulerAngles; _yaw = e.y; _pitch = e.x > 180f ? e.x - 360f : e.x;
        _on = true;
    }
    public void Exit()
    {
        if (!_on) return; _on = false;
        if (_brain != null) _brain.enabled = true;
        if (_cam != null) { _cam.fieldOfView = _fovWas; _cam.nearClipPlane = _nearWas; }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    void OnDisable() => Exit();
    void LateUpdate()
    {
        if (!_on || _cam == null) return;
        Transform root = RrPlayer.Root, head = RrPlayer.HeadT;
        if (root == null || head == null) return;
        _yaw += Input.GetAxisRaw("Mouse X") * 2f; _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * 2f, -89f, 89f);
        root.rotation = Quaternion.Euler(0f, _yaw, 0f);          // walk direction + cancels client Q/E rotate
        _cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        _cam.transform.position = head.position + _cam.transform.forward * 0.12f + Vector3.up * 0.02f;
        _cam.fieldOfView = 60f * FovKick; _cam.nearClipPlane = 0.03f;
        if (Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
}
```
Pause it (Exit/Enter) around the tablet, our menus, chat focus and focus loss (5.4).

### 9.5 Aim ray and gaze-target launch

```csharp
Camera cam = EtsVr.Head();
Ray aim = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
Vector3 target = Physics.Raycast(aim, out RaycastHit hit, 60f, RrLayers.AimMask, QueryTriggerInteraction.Ignore)
    ? hit.point : aim.GetPoint(30f);
Vector3 dir = (target - heldObject.position).normalized;
rb.linearVelocity = dir * throwSpeed;              // 8 m/s generic, 14 m/s dodgeball (charged), Rec Room configs
rb.angularVelocity = Random.onUnitSphere * Mathf.Deg2Rad * 360f;
```

### 9.6 One-time runtime probe to settle the GUESSes (log, then delete)

```csharp
// VR: which pose drives the Bridge hand? compare with the converted device grip pose
Matrix4x4 toWorld = cam.transform.localToWorldMatrix * Matrix4x4.TRS(hmdPos, hmdRot, Vector3.one).inverse;
Vector3 gripW = toWorld.MultiplyPoint3x4(devPos); Quaternion gripR = toWorld.rotation * devRot;
Debug.Log($"[SomniumRecRoom] hand-vs-grip pos {Vector3.Distance(hand.position, gripW):0.000} m, " +
          $"rot {Quaternion.Angle(hand.rotation, gripR):0.0} deg, local {Quaternion.Inverse(gripR) * hand.rotation}");
// ~0 m / 0 deg: the anchor is the grip pose. A constant 30-60 deg about X: it is the aim (pointer) pose.
// Also list the device's usages once: device.TryGetFeatureUsages(list) -> names (look for "PointerPosition").
```

## 10. Open questions / GUESSes

1. **Aim vs grip pose on the Bridge hand** (1.2): both a legacy TrackedPoseDriver (grip) and an XRI
   ActionBasedController (pointer first) drive `*HandAnchor`. Settle with 9.6 on the user's headset; the hold-frame
   rotation offset depends on it.
2. `Tool.interactionSettings.*PickupTransform` semantics and Handedness enum (4.4) - calibrate with the pistol.
3. Desktop held-object offsets in front of the camera (5.4) - tune visually; Rec Room animates arms instead.
4. Desktop FOV 60 and near 0.03 in play mode (client lens is 40/0.1) - GUESS; check comfort.
5. Whether writing `Body.Root.rotation` each LateUpdate on desktop has side effects on the client's network driver
   (ETS writes it every frame while seated without issues, so likely fine).
6. Tablet-open detection: animator `IsOpen` path is proven in ETS (with a full discovery walk); a lighter search
   (Animators whose controller has a bool "IsOpen" under the local player) is untested.
7. Layer plan (3.3): props on 10 with floors on 23; balls on 26; hit boxes on 27 as triggers. Test that the VR far
   pointer on layer 10 props (in its mask) does not interfere with grabbing (it only affects UI/XRI).
8. Calling `CharacterController.Move` alongside the client (6.4 lighter alternative) is untested.
9. `SomniumClimbableSurface` objects must exist at load; resizing their colliders at run time is untested.
10. Reload key: Rec Room's is not published; `Z` proposed because R is the client's pop-up menu.

## Sources (web)

- Rec Room PC controls list: https://defkey.com/rec-room-pc-shortcuts
- Rec Room blog, "Updating our Screens Controls" (2022-05-26): https://blog.recroom.com/posts/2022/5/26/updating-our-screens-controls
- Rec Room Help Centre, VR Controls and Gestures: https://recroom.zendesk.com/hc/en-us/articles/4424652450071-VR-Controls-and-Gestures
- Rec Room fandom, Basic Controls / Screen Mode: https://rec-room.fandom.com/wiki/Basic_Controls , https://rec-room.fandom.com/wiki/Screen_Mode
