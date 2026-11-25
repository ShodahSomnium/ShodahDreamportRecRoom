# hands - progress (task key: hands)
Started 2026-09-24. Owns: CODE/Player/RrAvatarHand.cs, CODE/Player/RrAvatarHandTest.cs, SP/harness/scenarios/hands_*.json,
DN/phase1/hands.md (this), DN/phase1/hands_frames.md.

## Status
- [x] 1 client research (avatar location, animator, IK, timing, first person, scale, outfit change)
- [x] 2 frame analysis (hands_frames.md)
- [x] 3 API block in hands_frames.md
- [x] 4 RrAvatarHand.cs
- [x] 5 RrAvatarHandTest.cs + scenario hands_frames.json
- [x] 6 compile + harness pass

## Log
- 10:5x Client findings so far (evidence to go in hands_frames.md):
  - VRIK targets Left/RightHandVRIKPoint are children of "Stabilized" (XRI XRTransformStabilizer, target = the anchor,
    angle 5 / position 0.05, runs in Update only), NOT of the anchor: Player.XR.Body.prefab:2462-2476 (father
    7694723122299348976 = Stabilized :2578-2609). somnium_player.md 1.2 is slightly wrong on this.
  - IKPoints.SetToDefault (IKPoints.cs:56-75) sets the VRIK point local pose to _leftHandPos/_leftHandRot (prefab :1812-1829).
  - VRIKHandler.cs:125-148: SetToDefault then target.rotation *= Inverse(LeftHandDefaultDelta) * avatar bind global rot of
    the hand bone (CalculateTargetDelta :183-227) => drawn hand frame relative to Stabilized is avatar-independent.
  - IK solved in IKComponentsOrder.LateUpdate (IKComponentsOrder.cs:90-119, VRIK.enabled=false VRIKHandler.cs:96), default
    order 0 => our LateUpdate@10000 reads solved bones. No IK in onBeforeRender/beginCameraRendering (grep).
  - Avatar: AvatarSpawner.cs:31 instantiates at master-scene root (NOT under Body.Root). Model renamed "Root" under a
    MainRoot (BaseAvatarRoot.cs:130-145), humanoid Animator on "Root". Local MainRoot layer 21, head bone scale 1e-10
    (MainRoot.cs:62-112): body/arms drawn in first person, head hidden. MirrorRoot = 2nd copy layer 8 (bones copied
    post-IK, RootsMirroringHandler.cs:33-106), TeleportRoot = 3rd copy layer 6 inactive.
- 11:1x MEASURED in the user's VR log (LocalLow/Somnium Space Ltd/Somnium Space VR/Player.log): "the anchor IS the grip
  pose" (offset 0, rot 0, Oculus Touch OpenXR) and anchor lossy scale 0.889 then 1 (grip frames destroyed with the
  anchor once = avatar/mode change). So Grip == LeftHandAnchor == OpenXR grip pose.
- Drawn hand vs anchor (qcalc, VRIK: hand rot = target rot, IKSolverVRArm.cs:258,262,458): W = _leftHandRot*Inv(_leftHandDefaultDelta):
  fingers (-0.19,-0.89,0.41), palm-out normal (0.98,-0.18,0.07), thumb (0.01,0.41,0.91) in anchor space; wrist at
  _leftHandPos (-0.033,0.068,-0.036). => the drawn hand matches the grip convention (X palm-out for L, Z through fist,
  -Y fingers) tilted 24 deg about X.
- Rec Room hand frame from (Player) LeftHand rig (_player.txt:223-285): +Z wrist->fingers (Hand.Root at z -0.105,
  finger tip z +0.106), +Y thumb (thumb goes to y +0.098), +X out of the left palm (fingers curl to +X; watch face -X
  = back of hand). = Grip*Euler(90,0,0) = AimRotation. HYPOTHESIS CONFIRMED for rotation.
- 11:3x Rec Room tool poses: FullBody*HandGripTransform / *HandPickupTransform = the TOOL's local pose in the hand frame
  (tool.local = (gp, gr)), NOT the hand in the tool (basketball centre +12.6 cm on the palm side; cup bottom 8.7 cm
  below the hand, axis on the palm side; pistol barrel 7 cm ABOVE the fist). RrGrab applies the inverse (A) - wrong.
  RR per-controller offsets (056ea358 input configs): Oculus native 0/0, SteamVR Touch (0,0.011,-0.052)+Euler(37.89,0,0),
  Vive/WMR 30 deg, Index 28/-5 => RR's hand frame is anatomical per controller; our equivalent = the drawn hand.
- 11:4x API block written to hands_frames.md section 0 (FIXED). Next: implement RrAvatarHand.cs.
- 12:0x RrAvatarHand.cs written (+ .meta 82e128c6e3bd4d3182d9918bb8063bd3; metas.sh also wrote RrIsolation.cs.meta for the
  isolation task's new file). Compile: 0 errors all three builds, 113 members proven. Next: RrAvatarHandTest.cs + scenario.
- 12:2x RrAvatarHandTest.cs (+meta 2d74d1d5a18a4f92969461609056e313) and SP/harness/scenarios/hands_frames.json written.
  Compile 0 errors / 113 proven. Running the harness.
- 12:3x Harness run 1: 9 pass / 6 fail. constants: 1 deg = float precision of Quaternion.Angle on an unnormalised
  expected quat (fixed). humanoid/vrik: the RPM Men FBX rests in an A-POSE (fingers 60 deg off T-pose); the client's
  bind pose is the Avatar skeleton (AnimatorHandler.cs:168-195 humanDescription.skeleton) - test now applies it.
  Shot: FullBody pistol grip points BACKWARD (FB pistol is relative to HandVisualPickupInfo.RootOverride = the FBX
  'Root', 180 deg Y). Legacy {L,R}HandPickupTransform is the PlayerHand-frame pose: pistol handle passes 1.1 cm from
  the RR origin (B), sword line 1.1 cm. => grab should prefer legacy, then FullBody.
  Tube-alignment data: pistol handle 48 deg / sword 45 / paddle 56 / pen 32 from RR +Y toward +Z; cup 0, bottle 6.5.
  Scene rebuilt as 4 stations (old placement vs new). Run 2 in progress.
- 12:5x Run 2: 33/33 steps OK, HandsTest 15/15 (RPM in skeleton pose: rest frame 7-9 deg from ideal T-pose; VRIK
  re-creation 9.4 deg / palm 1.6 cm from the constants). Shots looked at: OLD placement reproduces the bug (bottle
  along the forearm, pistol at the floor); NEW (drawn frame + legacy-first) holds bottle upright, pistol forward,
  cup upright on the palm side, sword blade up-forward, pen forward, paddle head up. Doc sections 1-5 written.
- DECISION: RrAvatarHand's Rec Room frame = the drawn hand's anatomical frame (fingers +Z, thumb +Y) - matches RR's own
  rig semantics; GripToRecRoomDefault = (0.5396,-+0.0777,-+0.0576,0.8363) (26.7 deg from AimRotation). Alternatives
  (AimRotation 90, tube-aligned ~45, pitch-only 66) documented in hands_frames.md section 4.
- Requests (not my files): RrPlayer.Shutdown one-line `RrAvatarHand.Reset();`; RrGrab.cs:207-208 + RrTools legacy-first
  (grab task); RrWatchModel wrist pose (watch task); somnium_player.md 1.2 VRIK-point parent correction.
- 12:6x Added cost check; run 3 (final) in progress.
- 12:7x Run 3: 35/35 steps, HandsTest 16/16 ALL PASS, cost 0.90 us/query. Added scan backoff (6 fast scans at 0.5 s,
  then 3 s; one log line when it slows). Compile 0 errors / 113 proven. hands_frames.md sections 0-6 complete.
  Run 4 (latest code incl. backoff) queued behind other agents' harness runs.
- 12:8x Run 4 (latest code): 35/35 steps OK, HandsTest 16/16 ALL PASS. Task complete; result returned.
