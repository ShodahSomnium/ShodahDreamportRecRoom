# Hands: the drawn avatar hand, and the hand-frame analysis (task "hands", 2026-09-24)

Status (2026-09-24): API fixed (section 0), implemented in `CODE/Player/RrAvatarHand.cs`, harness-verified (section 6).
Not run in the Somnium client yet. The one-shot log line "drawn hand vs the client's IK constants" in the next VR session
will give the real residuals.

## 0. API (fixed - the grab and watch tasks can rely on it)

```csharp
namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    public enum RrAvatarHandSource { None, HumanoidBones, NamedBones, IkTarget, ControllerGuess }

    public static class RrAvatarHand
    {
        // The drawn wrist joint (the hand bone's pivot). Rotation: +Z along the FOREARM (elbow -> wrist, i.e. toward
        // the fingers), +Y out of the BACK of the hand (projected square to the forearm), +X = Y x Z (Unity
        // left-handed): the thumb side on the LEFT hand, the little-finger side on the RIGHT hand.
        // forearmLength = elbow -> wrist in world metres (includes avatar and rig scale); 0.25 * rig scale when no
        // elbow bone is known (IkTarget / ControllerGuess).
        public static bool TryGetWrist(RrHandSide side, out Pose wrist, out float forearmLength);

        // Centre of the drawn closed fist, axes in RrHand.Grip's convention (OpenXR grip): +Z runs through the fist
        // little finger -> thumb (the thumb side, square to the fingers), -Y comes out of the front of the fist
        // (= the finger direction, wrist -> middle knuckle), +X = right (out of the LEFT palm, out of the BACK of
        // the RIGHT hand). So palm.rotation * RrHand.GripToAim = Rec Room's hand frame on the drawn hand.
        public static bool TryGetPalm(RrHandSide side, out Pose palm);

        // Rec Room's own hand frame (the frame Tool.interactionSettings poses are written in) on the drawn hand:
        // position = TryGetPalm, rotation = palm.rotation * RrHand.GripToAim: +Z = fingers, +Y = thumb side,
        // +X = out of the left palm / back of the right hand.
        public static bool TryGetRecRoomHand(RrHandSide side, out Pose hand);

        // The live drawn palm expressed in RrHand.Grip's local space (for parenting under the grip).
        public static bool TryGetGripToPalm(RrHandSide side, out Pose gripToPalm);

        // Constants: where the client's DEFAULT IK draws the palm / Rec Room's hand frame, in RrHand.Grip's local
        // space (VR, grip == the client's hand anchor, measured). Use when no bones are known or to avoid IK noise.
        public static Pose GripToPalmDefault(RrHandSide side);
        public static Pose GripToRecRoomDefault(RrHandSide side);

        public static RrAvatarHandSource Source(RrHandSide side);
        public static void Refresh();   // forget the cached bones; the next query re-finds them (throttled scan)
        public static void Reset();     // statics cleared (RrPlayer.Shutdown hook requested)
        public static string Describe();
    }
}
```

Timing contract: every getter reads the CURRENT transforms (no cross-phase cache). Call it where the bones are final
for the frame: in `LateUpdate` at execution order >= 10000 (RrPlayerDriver's slot) or in
`RenderPipelineManager.beginCameraRendering`. Both see the same bone pose (section 1.4). Reading in Update gives
last frame's pose.

Test hooks (for the watch task), class `SomniumSpace.Worlds.SomniumRecRoom.Player.RrAvatarHandTest`:
`TestArm BuildTestArm(RrHandSide side, Pose wrist, float scale = 1f)` builds a synthetic arm whose DRAWN wrist is
exactly `wrist` (API convention) with Mixamo (left) / Unreal (right) bone names, random bone rotations, and the fake
client rig for it (`LeftHandOffset` > `LeftHandAnchor` + `Stabilized` > `LeftHandVRIKPoint`; `TestArm.Grip` = the
grip pose the default IK needs), and feeds both arms to RrAvatarHand (internal `SetTestAvatar`);
`IEnumerator BuildDefaultArms()` = zero-argument variant (both arms at `DefaultWrist(side)`: thumbs up, forearms
forward, wrists at (+-0.2, 1.35, -2.2), with stick visuals and frame axes); `ClearTestArms()` removes everything and
the override. Scenario coroutines: `.Run` (numeric checks, "HandsTest: PASS/FAIL name (...)", "HandsTest: ALL PASS")
and `.Scene` (the four-station picture, section 6). In the harness RrHand.Left is not tracked (desktop), so a watch
test should read `RrAvatarHand.TryGetWrist` rather than RrHand.

## 1. The client: where the drawn hand comes from (CL = E:/Somnium Space/Main Build/Assets/_SomniumSpace)

### 1.1 Where the local avatar model lives
- `AvatarSpawner.SpawnAvatar` instantiates the avatar prefab at the ROOT of MasterScene for the local player
  (`CL/AvatarSystem/Scripts/Spawner/AvatarSpawner.cs:31`, `WorldsHelper.SpawnParameters` = scene "MasterScene",
  `CL/Utilities/Scripts/WorldsHelper.cs:16,34`); remote avatars go to the active scene (`:33`). It is NOT under the
  Bridge's `Body.Root` (`XR.Body`) and not under the hand anchors. The Bridge exposes no avatar transform
  (`ISomniumPlayerAvatar` = `OnAvatarChanged` + `SetVRIKLegMovementDisabled` only, bridge_decomp).
- Hierarchy: `AvatarMainPrefab(Clone)` > holders (made by `AvatarGenerator`, `Generator/AvatarGenerator.cs:222-285`)
  > `MainRoot` / `MirrorRoot` / `TeleportRoot` / `WearablesRoot` (`Holders/XR/XRLocalRootsHolder.cs:10-36`) > the
  loaded model, instantiated and renamed **"Root"** (`Roots/Base/BaseAvatarRoot.cs:137-158`).
- Layers: local MainRoot = **21** (drawn by the main camera, not the tablet camera; `Roots/MainRoot.cs:62-66`,
  constants `BaseAvatarRoot.cs:123-126`), 22 when the dummy holder is shown (`MainRoot.cs:114-118`); MirrorRoot = 8
  (6 while the main is hidden, `Roots/MirrorRoot.cs:14,26`); TeleportRoot = 6 and inactive (`Roots/TeleportRoot.cs:15-19`);
  remote players' MainRoot = 6 (`MainRoot.cs:65`). The mirror/wearables copies get the main root's bone transforms
  after the IK and again in LateUpdate (`RootHandlers/Common/RootsMirroringHandler.cs:33-106`), so all copies show the
  same pose. **RrAvatarHand picks the humanoid Animator whose head bone is within 1 m of the Bridge head anchor,
  preferring layer 21, then 8, then MasterScene, then nearest.**

### 1.2 Animator and bones
- The model's own `Animator` sits on "Root" (`BaseAvatarRoot.cs:142`, controller cleared at `:143-144`); everything in
  the avatar system uses `Animator.GetBoneTransform(HumanBodyBones.*)` (`MainRoot.cs:78,104`,
  `RootsMirroringHandler.cs:91-106`), so Somnium avatars are **humanoid**. Bind pose = the Avatar's
  `humanDescription.skeleton` (`RootHandlers/Common/AnimatorHandler.cs:168-195`). Bone names are the rig's own
  (RPM Men in SomniumSpaceTemplates: `LeftHand`, `LeftForeArm`, `LeftHandIndex1`..., Mixamo-style without prefix);
  RrAvatarHand does not depend on names on the humanoid path.
- Measured on RPM Men (the Ready Player Me template avatar): forearm 0.252 m, wrist -> middle knuckle 0.101 m. Its
  FBX rests in an A-pose (fingers 60 deg below the T-pose line); its Avatar skeleton is the T-pose (section 6).

### 1.3 Which IK, and the exact hand targets
- **FinalIK VRIK** on the model root (`RootHandlers/Common/VRIKHandler.cs:82-96`: `AddComponentToRoot<VRIK>()`,
  registered with `IKComponentsOrder`, `_vrikRef.enabled = false`). Arm targets = `IKPoints._leftHand/_rightHand`
  (`VRIKHandler.cs:71-73,146-148`) = `LeftHandVRIKPoint` / `RightHandVRIKPoint` (layer 30, local scale 0.08647).
- **Parent of the targets = `Stabilized`**, not the anchor: `CL/Player/Prefabs/NEW/XR/Player.XR.Body.prefab:2462-2476`
  (father 7694723122299348976 = `Stabilized`, `:2578-2592`), a sibling of `LeftHandAnchor` under `LeftHandOffset`.
  `Stabilized` carries XRI `XRTransformStabilizer` with target = `LeftHandAnchor`, angle 5, position 0.05, world space
  (`:2593-2609`); it runs in **Update only**, at order -29985 (after the controllers; `XRTransformStabilizer.cs:20,132-146`,
  `XRInteractionUpdateOrder.cs:73`). It lags the anchor for small motions (< 5 deg / 5 cm) and follows big ones.
  (somnium_player.md 1.2 lists the VRIK point under the anchor; the prefab says Stabilized.)
- Target local pose: `IKPoints.SetToDefault` (`Player/Scripts/Components/BodyParts/Additional/IKPoints.cs:56-75`,
  called at `VRIKHandler.cs:125` on first init) sets `_leftHandPos` / `_leftHandRot` from `PlayerBodyHolder._ikPoints`
  (prefab `:1812-1829`): **left pos (-0.033, 0.068, -0.036)**, rot (0.04427, -0.10685, -0.74876, -0.65267); right pos
  (0.033, 0.068, -0.036), rot (0.65269, -0.74875, 0.10685, 0.04427). (The prefab's own serialised left VRIK point
  pose, (-0.036, 0.0596, -0.0485), is overwritten.) Then `target.rotation *= Inverse(_leftHandDefaultDelta) * B`
  where B = the hand bone's global bind rotation below the VRIK root (`VRIKHandler.cs:133-138,183-227`);
  `_leftHandDefaultDelta` = (-0.03261, 0.10070, -0.17173, -0.97944), right (-0.97944, 0.17174, 0.10070, 0.03261).
- VRIK then puts the hand bone at the target position and **rotation** (`Assets[3rdParty]/RootMotion/FinalIK/IK
  Solvers/IKSolverVRArm.cs:258` `IKRotation = target.rotation`, `:262`, `:458` `hand.solverRotation = rotation`,
  rotationWeight 1). So hand bone world rotation = S * _handRot * Inverse(_handDefaultDelta) * B (S = Stabilized):
  the T-pose anatomy is turned by W = _handRot * Inverse(_handDefaultDelta) **whatever the rig's bone axes are**.

### 1.4 When things move in a frame
| when | what | evidence |
| --- | --- | --- |
| Update (order -30000 region) | anchors from TrackedPoseDriver / XRI controller | prefab `m_UpdateType: 0` = Update+BeforeRender (`:2131,3324,3991`) |
| Update, order -29985 | `Stabilized` eases toward the anchor | XRTransformStabilizer |
| Animator update | model bones (walk/idle layers) | Unity |
| LateUpdate, order 0 | `IKComponentsOrder.LateUpdate`: pre-IK, VRIK + grounder solve, post-IK (mirror copies, finger poses) | `RootHandlers/Additional/IKComponentsOrder.cs:90-119` |
| LateUpdate, order 10000 | **RrPlayerDriver: RrHand poses; read the drawn hand here** | `Player/RrPlayerDriver.cs` |
| Application.onBeforeRender | anchors (and children: RrHand.Grip) re-posed with the late pose | TrackedPoseDriver |
| beginCameraRendering | nothing in the avatar system | grep of CL: only SomniumUpscaler / NetworkDriver / OpenVRDevicesManager |
No avatar script has a `DefaultExecutionOrder` (grep). The bones are final from LateUpdate@0 to the end of the frame;
reading at LateUpdate@10000 or in beginCameraRendering gives the same drawn pose. The drawn hand does NOT get the
BeforeRender re-pose (it was solved from the Update-time, stabilised anchor), so an object parented under the grip
can run ahead of the drawn hand by the last-ms pose update plus the stabiliser lag.

### 1.5 Drawn in first person?
Yes, except the head: the local MainRoot renders on layer 21 with the head bone scaled to 1e-10
(`MainRoot.cs:74-82,99-112`), shadows off (`:70-71`), `updateWhenOffscreen` on (`:120-130`). Hands and arms are visible
in VR.

### 1.6 Scale
- The avatar holder is uniformly scaled for the avatar height (`RootHandlers/Common/AvatarScaleHandler.cs:12-30`,
  `Holder.SetLocalScale(one * scale)`).
- The XR rig is scaled too: the user's log shows the anchors' lossy scale 0.889 (then 1 after a rebuild), so the IK
  offsets above are in rig-scaled metres (they live under LeftHandOffset). RrAvatarHand works from world bone
  positions (scale-free); the IK/controller fallbacks multiply by the parent's lossy scale.

### 1.7 Avatar / outfit change
`SwitchAvatar` loads a new model; `BaseAvatarRoot.Initialize` instantiates it (`:137-158`) and the old roots are
destroyed (`Holders/Common/RootsHolder.cs:16-27` -> `BaseAvatarRoot.Destroy` `:250-253`). Bones are destroyed and
recreated (cached Transforms read null). The Bridge anchors were also rebuilt once in the user's session ("grip frame
was destroyed with the client's hand anchor"). RrAvatarHand re-finds on null, on Refresh(), and re-validates the
avatar's distance to the head every second.

## 2. The frames, with numbers (Unity left-handed; quaternions (x, y, z, w))

### 2.1 Anchor = grip (MEASURED)
The user's VR session log (LocalLow/Somnium Space Ltd/Somnium Space VR/Player.log, Oculus Touch via OpenXR):
"left/right grip frame calibrated against the OpenXR grip pose under 'LeftHandAnchor': offset (0,0,0), rotation
(0,0,0); spread 0.0 cm / 0.0 deg -> the anchor IS the grip pose". So `Body.LeftHand` = LeftHandAnchor = the OpenXR
**grip** pose, and `RrHand.Grip` = the anchor with identity offset (RrHand.cs calibration). Same log: anchor lossy
scale 0.889 (rig scaled for avatar height).

### 2.2 RrHand.Grip and RrHand.AimRotation
- Grip (Unity-converted OpenXR grip): **+Z through the fist from little finger to thumb** (the controller's handle
  axis), **+X right** for both hands (out of the left palm, into the right palm = out of the back of the right hand),
  **-Y out of the front of the fist** (knuckle side, forward), +Y toward the user. Origin: centre of the handle at
  the palm (OpenXR: "palm centroid when closing the fist, at the surface of the palm").
- AimRotation = Grip * Euler(90,0,0): forward = Grip -Y, up = Grip +Z, right = Grip +X (Quake/AoM "GripToAim").

### 2.3 The drawn hand at the client's default IK, in the grip frame (derived; code: RrAvatarHand constants)
W = _handRot * Inverse(_handDefaultDelta) turns T-pose anatomy (left: fingers -X, thumb +Z, palm-out -Y; right:
fingers +X) into the IK parent's (= Stabilized ~ anchor = grip) space:

| | left | right |
| --- | --- | --- |
| wrist joint (hand bone pivot) | (-0.033, 0.068, -0.036) | (0.033, 0.068, -0.036) |
| fingers (wrist -> middle knuckle) | (-0.192, -0.894, 0.406) | (0.192, -0.894, 0.406) |
| thumb side (square to fingers) | (0.013, 0.411, 0.912) | (-0.013, 0.411, 0.912) |
| out of the palm | (0.981, -0.180, 0.068) | (-0.981, -0.180, 0.068) |
| grip centre seen from the wrist | 6.9 cm along the fingers, 4.7 cm toward the palm, 0.5 cm toward the thumb (8.4 cm) | same |
| drawn Rec Room-convention frame (+Z fingers, +Y thumb, +X = Y x Z) | **(0.5396, -0.0777, -0.0576, 0.8363)** = GripToAim * Euler(-23.9, -12.1, 4.3) | **(0.5396, 0.0777, 0.0577, 0.8363)** = GripToAim * Euler(-23.9, 12.1, -4.3) |
| drawn palm, grip convention (= above * Inverse(GripToAim)) | (-0.2098, -0.0142, -0.0957, 0.9729), 26.7 deg from the grip | (-0.2098, 0.0142, 0.0957, 0.9729) |

Reading: the avatar's fist encloses the controller handle (the grip centre is the drawn fist centre), but the drawn
fingers point 24 deg above the grip's "front of the fist" and 12 deg outward, rolled 4 deg. That is the anatomical
obliquity of a power grip (the handle runs diagonally across the palm), as tuned by Somnium. The wrist frame of the
API (+Z forearm, +Y back of hand) is the Rec Room-convention frame rolled by Euler(0,0,+90) (left) / (0,0,-90)
(right): `wrist = rr * Euler(0, 0, left ? 90 : -90)`.
**Checked on a real rig** (harness, RPM Men put into its Avatar skeleton pose, hand bone posed exactly as VRIK does):
the bone-derived frame is 9.4 deg from Stabilized * constant, and the drawn palm lands 1.6 cm from the grip centre
(its hand is 0.101 m against the 0.092 m reference). The residual is that rig's own rest pose: its fingers are
7.3 deg off an ideal T-pose.

### 2.4 Rec Room's hand frame (bundle data)
Rec Room's `(Player)` prefab (c513202a #1873379750507966147, dump SP/research/_player.txt:223-285), LeftHand = the
tracked `PlayerHand` all tools and the watch hang off:
- the hand mesh rig (Mesh p (-0.02,0,0) r (-0.5,0.5,-0.5,0.5) s (1,1,-1)) gives, in the LeftHand frame: wrist
  `Hand.Root` (-0.020, -0.001, -0.105); finger base (-0.030, 0, 0.004) -> tip (-0.002, 0, 0.106) along **+Z**,
  curling toward **+X**; thumb base (-0.020, 0, -0.077) -> tip (-0.010, 0.098, 0.011) toward **+Y**;
- `WatchAnchor` p (-0.02, 0, -0.119) r (0.5,-0.5,-0.5,0.5): face +Z -> hand -X = out of the back of the left hand;
- `Fingertip` / `MenuLaserOrigin` (0, 0.031, 0.10); `HandCenter` (-0.01, 0.011, -0.004).

=> **Rec Room hand frame: +Z = fingers (forward), +Y = thumb side (up), +X = out of the left palm / out of the back
of the right hand (right), origin = palm centre at the knuckle line.** Its axes are those of AimRotation
(+Y through the fist on the thumb side, +Z out of the front of the fist). The hypothesis holds for the AXES.
Rec Room's per-controller input configs (bundle 056ea358476dda1486b5728c1ae0c35a) turn each raw controller pose
into this anatomical frame: Oculus native 0 / 0; SteamVR Touch (0, 0.011, -0.052) + Euler(37.89, 0, 0); Vive
(0, -0.021, -0.065) + (30, 0, 0); WMR (0, 0.011, -0.052) + (30, 0, 0); Index (+-0.013, -0.005, -0.07) + (28, 0, -5).
So the frame is "where the hand is", not "where the controller is". Its Somnium equivalent is therefore the DRAWN
hand's anatomical frame (2.3), not AimRotation. The two differ by 26.7 deg.

## 3. What Rec Room's hold poses mean (bundle data; SP/rrtool, bundle 43ea1176 unless noted)
`Tool.interactionSettings.{Left,Right}HandPickupTransform` (legacy) and `FullBody{Left,Right}HandGripTransform`
are **the TOOL root's local pose in the hand frame** (tool = hand * (p, r)). They are NOT the hand's pose in the
tool, which is what RrGrab.cs:207-208 assumes (RestRotation = Inverse(r), RestPosition = -(Inverse(r) * p)). For
each tool: where its gripped part lands under both readings (B = tool in hand, A = hand in tool, the first build):

| tool (pathId) | field | B: gripped part in the hand frame | A: same |
| --- | --- | --- | --- |
| PaintballGun (-5634856804522456178) | legacy L/R (0, 0.0723, 0.0120), Euler(20,0,0) | handle centre 1.1 cm from the origin; barrel above the fist, 20 deg below +Z | handle 14.5 cm away, barrel under the fist |
| Basketball (5755647178341420997) | FB L (0.1257, 0.0069, 0.0071), identity | ball centre 12.6 cm on the PALM side (radius 12.4 cm): the palm touches the ball | ball on the back of the hand |
| SoloCup (3935866521521447249) | legacy L (0.04, -0.087, 0), identity | cup axis 4 cm on the palm side, bottom 8.7 cm below: held mid-height | cup above the hand, on its back |
| Consumables_RootBeer_Bottle (85515de1 #7741124006095656599) | FB L (0.0268, -0.0355, -0.0250), Euler(6.5,0,0) | bottle axis 3.4 cm on the palm side, upright (6.5 deg) | axis on the back of the hand |
| CharadesPen (-5340755430698854321) | legacy (0, 0.0204, 0.0393), Euler(31.6,0,0) | handle through the fist, tip forward-down | handle 15 cm away |
| QuestSword (-7308406628275338915) | legacy (-0.011, -0.071, -0.071), rot (-0.2706,0.2706,0.6533,0.6533) | blade axis passes 1.1 cm from the origin, blade out of the thumb side, 45 deg forward | blade sideways out of the back of the hand |
| Paddleball_Paddle (7792956688979234920) | legacy L (0.0003, 0.0151, 0.0512), Euler(-18.8,..) | handle through the fist, head up-forward | head along the forearm |

The first build's placement reproduces the user's report ("they need to be rotated up 90 degrees"): see
`hands_old_pov.png`, where the bottle lies along the forearm pointing back and the pistol points at the floor.

**Legacy first, FullBody second.** The legacy Pickup transform is in the PlayerHand frame, and every tool above is
consistent in it. The FullBody grip of the PaintballGun, (-0.030, 0.093, 0.012) with rot (0.00017, 0.9914,
-0.0013, 0.1312) (165 deg about Y), points the gun BACKWARD in any hand frame (`hands_old_pov.png`). Its
`HandVisualPickupInfo.RootOverride` is the FBX "Root" node, which sits under `PaintBallPistol_fbx` rotated 180 deg
about Y. So FullBody poses are relative to the override node when one is set (the rifle and sword have one too,
the pen, cup, paddle, ball and bottle do not). RrTools currently prefers FullBody. Use FullBody only when the legacy
Position is zero (bottle, basketball; there the two readings agree with the data). Rec Room stores both hands
mirrored: x -> -x for positions, (x, y, z, w) -> (x, -y, -z, w) for rotations. RrTools' fallback that reuses one
side's rotation for the other is exact only for pure X rotations.

## 4. The conversion for the grab task (full formula)

Inputs: Rec Room hold pose (gp, gr) for this hand (legacy first, else FullBody; mirrored as above if only one side
is set). k = the tool root's uniform scale relative to its prefab (1 for Rec Room props; RrGrab's
`Vector3.Scale(t.localScale, gp)` for uniform scale).

**(a) On the drawn hand (exact to what the player sees; place each LateUpdate at order >= 10000):**
```
RrAvatarHand.TryGetRecRoomHand(side, out Pose H);      // palm centre, rotation palm * GripToAim
tool.position = H.position + H.rotation * (k * gp);
tool.rotation = H.rotation * gr;
```
**(b) Parented under RrHand.Grip (the way RrGrab holds props now; BeforeRender-fresh, may lead the drawn hand by
the stabiliser lag):**
```
Pose G = RrAvatarHand.GripToRecRoomDefault(side);      // Rec Room hand frame in grip space at the default IK
// left  G = ((0,0,0), (0.5396, -0.0777, -0.0576, 0.8363)) = GripToAim * Euler(-23.9, -12.1,  4.3)
// right G = ((0,0,0), (0.5396,  0.0777,  0.0577, 0.8363)) = GripToAim * Euler(-23.9,  12.1, -4.3)
tool.localRotation = G.rotation * gr;
tool.localPosition = G.position + G.rotation * (k * gp);
```
instead of today's `localRotation = Inverse(gr); localPosition = -(Inverse(gr) * (k * gp))`. To follow a custom
avatar's hand, measure once per grab: `RrAvatarHand.TryGetGripToPalm(side, out P)` then
`G = (P.position, P.rotation * RrHand.GripToAim)`. It is noisy frame to frame (stabiliser, IK), so average a few
frames or use it only when `Source` is HumanoidBones/NamedBones.

**(c) Screen mode:** `TryGetRecRoomHand` returns the virtual hand with H.rotation = RrHand.AimRotation and
H.position = the grip, which is today's screen convention.

Grip -> drawn palm (for "sit in the avatar's drawn hand"): `GripToPalmDefault(side)` = ((0,0,0) to 0.1 mm,
left (-0.2098, -0.0142, -0.0957, 0.9729), right (-0.2098, 0.0142, 0.0957, 0.9729)). Live: `TryGetGripToPalm`.
The drawn wrist in grip space: position `RrAvatarHand.ClientLeftIkPosition` (-0.033, 0.068, -0.036) (x the anchor's
lossy scale), rotation = G.rotation * Euler(0, 0, left ? 90 : -90).

Why not plain AimRotation (Grip * Euler(90,0,0)): it has the right axes, but it is 26.7 deg away from the hand the
client draws. With it, a cup would lean 24 deg forward of the drawn fingers, and a pistol would point 20 deg below
Quake's proven aim. With G, the Rec Room pistol's barrel (legacy pose) points 4 deg above Quake's aim (Grip -Y) and
10.7 deg outward, following the drawn fingers. If aiming feels off in the headset, the pitch-only variant
`GripToAim * Euler(-23.9, 0, 0)` drops the yaw/roll (a one-line change for the grab task).
Also considered, not chosen: aligning Rec Room's gripped handles with the physical controller handle. The pistol,
sword, paddle and pen handles sit 48 / 45 / 56 / 32 deg from Rec Room's +Y toward +Z, which would mean
Grip * Euler(~45,0,0). But cups and bottles would then lean 21 deg back from the drawn hand. Rec Room re-poses its
own hand visual per tool (`HandVisualPickupInfo`, `*HandVisualPickupTransform` nodes), so handle alignment is not a
fixed frame.

## 5. For the watch task
- Drawn wrist: `RrAvatarHand.TryGetWrist(RrHandSide.Left, out Pose w, out float forearm)`: w.position = the wrist
  joint, +Z along the forearm, +Y = back of the hand. Read in beginCameraRendering or LateUpdate@10000 (1.4).
- Rec Room's watch relative to its own wrist joint (Hand.Root): (0, 0.001, -0.014) in the hand frame, i.e. **1.4 cm up
  the forearm from the wrist joint, on the joint axis**. The full-body avatar puts it 1.8 cm before the wrist
  (rr_watch_ui.md 2.1). Rotation in OUR wrist frame: left **(0, -0.7071, -0.7071, 0)** (face +Z -> wrist +Y = back
  of the hand, 3 o'clock +Y -> wrist +Z toward the fingers, strap +X -> wrist -X); right **(-0.7071, 0, 0, 0.7071)**
  (face -> +Y, 3 o'clock -> -Z, strap -> +X).
- Suggested pose: `watch.position = w.position + w.rotation * (0, 0, -0.014 * s)`, `watch.rotation = w.rotation *
  WatchInWrist(side)`, with s = forearm / 0.25 (a size hint; the Rec Room watch mesh is 108 x 45 x 72 mm with the
  strap ring round its origin).
- Fallbacks keep working without bones: Source IkTarget (VR, no avatar) and ControllerGuess use the client's
  constants (straight wrist).

## 6. Verification (harness, SP/harness/scenarios/hands_frames.json; out/hands_frames)
Compile: `build.sh project --editor`: 0 errors in all three builds, all 113 Fusion/Somnium members proven (no new
Bridge members used). Harness run 2026-09-24 ~11:30: 35/35 steps OK, "HandsTest: summary 16 passed, 0 failed",
"HandsTest: ALL PASS":

| check | result |
| --- | --- |
| constants-left/right | GripToPalmDefault position (0,0,0); drawn Rec Room frame 26.72 deg from GripToAim, 0.000 deg from the worked value; fingers (-+0.192, -0.894, 0.406) |
| names | mixamorig:LeftHand, hand_l, Bip01 L Hand, J_Bip_R_Hand, Hand.R recognised; Left_Hand not taken as right; LeftHandIndex1 / LeftHandle not a hand |
| named-left/right (Mixamo left with a roll bone, Unreal right with a metacarpal + twist bone; random bone rotations) | wrist 0.00 mm / 0.00 deg, forearm 0.2500 m, palm 0.00 mm / 0.00 deg, frame 0.00 deg, X/Y senses 1.000 |
| cost | 0.90 us per query in the editor (4 queries a frame = 0.004 ms) |
| one-shot IK comparison log | "left drawn hand vs the client's IK constants: wrist 0.0 cm, Rec Room frame 0.0 deg" (synthetic, self-consistent) |
| scaled-left/right (1.3, 0.8) | exact (0.00 mm / 0.00 deg), forearm 0.325 / 0.200 m |
| ik-left/right (avatar destroyed -> automatic fallback) | Source IkTarget, wrist 0.00 mm / 0.00 deg, palm = grip * GripToPalmDefault exactly |
| controller (harness screen hand) | right Source ControllerGuess, palm = grip, frame = AimRotation (0.00 deg); left None |
| humanoid-detect / humanoid-left/right (RPM Men in its Avatar skeleton pose) | HumanoidBones; forearm 0.252 m, wrist->middle knuckle 0.101 m; frame vs ideal T-pose: fingers 7.3, thumb 8.6, X 6.9 deg |
| vrik-left/right (RPM hand posed exactly as VRIK would) | bone-derived frame 9.4 deg from Stabilized * constant; palm (+-0.008, 0.003, 0.014) m from the grip centre |

PNGs looked at: `hands_old_pov.png` (the first build: bottle lying along the forearm pointing back, pistol barrel at
the floor, as the user reported), `hands_new_pov.png` (section 4: bottle upright in the left fist, pistol forward
above the right fist), `hands_cup_sword_pov.png` (cup upright on the left palm side, sword handle through the right
fist with the blade up and forward), `hands_pen_paddle_pov.png` (pen body forward over the fist, paddle head up),
`hands_overview.png`, `hands_rpm.png` / `hands_rpm_hand.png` (frame axes on the RPM hands: +Z down the fingers,
+X out of the left palm toward the thigh).

**Not tested:** anything in the real Somnium client. That covers the scene search for the local avatar (layer 21,
MasterScene, 1 m from the head anchor), the real `LeftHandVRIKPoint` lookup under the Bridge body, the one-shot "drawn
hand vs the client's IK constants" line in VR (it will print the real residuals: look for it in the next headset
log), avatar/outfit changes, desktop avatars, and IL2CPP cost. The section-4 conversion has not been seen in a
headset; the tools were only placed on synthetic arms.
