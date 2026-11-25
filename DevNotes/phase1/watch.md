# watch - VR watch on the avatar's drawn wrist (phase 1, 2026-09-24)

Owner: watch task. Owns CODE/Watch/*, SP/harness/scenarios/watch_wrist*.json.

## Status
- [x] read hands_frames.md / hands.md / RrAvatarHand API + test hooks
- [x] read CODE/Watch/* current placement
- [x] design the wrist frame (below)
- [ ] implement RrWatchModel (+ RrWatchUi order, RrWatch reset) + RrWatchWristTest.cs + watch_wrist.json
- [ ] compile
- [ ] harness watch_wrist.json (run 1: measure ring + RPM wrist, provisional constants)
- [ ] final constants, harness run 2
- [ ] harness watch_menu.json regression

## Findings (before code)
- First build: RrWatchModel placed the watch at grip.TransformPoint((-0.015,0,-0.1)) with grip.rotation * RR's
  WatchAnchor rotation. Two errors: (1) RR's anchor rotation is in RR's hand frame (= grip * GripToAim axes), applied
  to the raw grip it is 90 deg off about X: the strap (watch +X) ran ALONG the fingers and 3 o'clock pointed up the
  handle axis. Face normal happened to be right (X is shared). (2) (-0.015,0,-0.1) in grip space = 10 cm down the
  handle axis (grip -Z = little-finger side). The client's default IK draws the left wrist at (-0.033,0.068,-0.036):
  hand calc says the old origin is ~9 cm off the forearm axis and ~3 cm on the finger side of the wrist. => "floating off".
- Pitch/roll limits were therefore swapped too (watch X/Y were rolled 90 deg).
- TIMING BUG: RrWatchModel.LateUpdate had default order 0, same as the client's IKComponentsOrder.LateUpdate (order 0,
  undefined relative order). Reading bones there may see the PRE-IK animator pose. => [DefaultExecutionOrder(10010)]
  (after RrPlayerDriver 10000, before RrGrab 10050); menu RrWatchUi 10020.
- RR data: WatchAnchor (-0.02,0,-0.119) r(0.5,-0.5,-0.5,0.5) in PlayerHand; Hand.Root (wrist) (-0.02,-0.001,-0.105)
  => watch origin 1.4 cm up the forearm from the wrist joint, ON the joint axis. Full-body RR: 1.8 cm.
  WatchFaceTransform p (0,0,0.041) r (0.704,0.704,0.062,0.062): a camera-like frame (X = 3 o'clock, Y = 12 o'clock,
  +Z INTO the face), tilted 10 deg: the ideal eye is 10 deg off the face normal toward 6 o'clock (the thumb side).
  AvatarSkeletonWatchUIAnchor (-0.03,0.01,-0.13). WatchMenuOrigin (-0.15,0,-0.004) = 15 cm out of the back of the hand.
- 12 o'clock = watch +X = little-finger side on BOTH wrists; 3 o'clock toward the fingers (left) / the elbow (right).
- WatchInWrist (our wrist frame: +Z forearm, +Y back of hand): left (0,-0.7071,-0.7071,0), right (-0.7071,0,0,0.7071)
  = Inverse(Euler(0,0,+-90)) * RR anchor rotation.
- RPM Men / RPM Women FBX (single mesh Wolf3D_Avatar, isReadable 1) exist in the harness copy: use for photos and to
  MEASURE the forearm cross-section at the watch (sleeve clearance).

## Log
- started; progress file created
- ~13:xx RrWatchModel.cs rewritten (drawn-wrist pose via RrAvatarHand.TryGetWrist, order 10010, RrWatchSource enum,
  static PoseOnWrist/GazeAngles/TapRegionContains/TryPokePoints/FirstBuildPose, FaceView 10 deg bias, tap uses the
  other hand's drawn fingertip (RR Fingertip (0,0.031,0.10))). RrWatchUi: order 10020 + doc. RrWatch: doc + test
  switch resets. Compile: 0 errors x3, 113 proven. Provisional MeshScale 0.8, Lift 0, BackDistance 0.018.
  rrtool: Wrist_Watch mesh m_IsReadable TRUE (c513202a #-7554622292230741548) -> can measure the ring in the harness.
- next: RrWatchWristTest.cs (Run: numeric checks + ring/RPM fit measurement; 3 photo stations; menu gaze/follow/tap)
- ~13:xx RrWatchWristTest.cs (+meta 2a1f22fadc3647b5b06cd2e0905e2ef9; metas.sh also wrote Interact/RrScreenHold.cs.meta
  09ce762d44e74c03a25a80f0fcaad02c for another agent's new file) and SP/harness/scenarios/watch_wrist.json written.
  Compile 0 errors x3 / 113 proven. Harness run 1 starting (measure ring + RPM, provisional constants).
- RUN 1 (first try died on another agent's RrGrab.cs compile error; retry OK): 50/50 steps OK, 25 PASS / 0 FAIL.
  MEASURED:
  - first build's watch on the default-IK arm: 102.5 mm from the new one, 89.8 mm off the forearm axis, 31.3 mm past the
    wrist joint INTO the hand; strap turned 65.7 deg, 3 o'clock 66 deg off the fingers. (= the user's "floating off".)
  - Wrist_Watch ring (mesh units, radius from its origin by angle from the face toward 12 o'clock): inner/outer
    0:30/40 30:35/46 60:50/56 90:49/55 120-165: OPEN 180:28/33 210:36/41 240:49/54 270:49/54 300:41/51 330:34/43 (mm).
    => inner opening ~98 x 58 mm oval, open on the 12-o'clock/palm quadrant.
  - RPM Men (forearm 0.252): skin under the strap = BAGGY SWEATER: back 46 mm above the joint axis, palm 73 below,
    width 126, thickness 119 mm. Nothing in scale 0.4..1.2 clears it. Photos: watch fully hidden inside the sleeve.
  - RPM Women (forearm 0.257): back 27.8, palm 24.1, width 66, thickness 52 mm. Snuggest fit: mesh scale 0.97
    (MeshScale 0.942), lift 0.5 mm: min gap 2.1 mm, under the case 4.3, under the wrist 2.1, sides 11.5.
  - gaze e2e: head (0,1.59,-3); menu opened by looking (view 32, pitch 2, roll 10) in 0.4 s, on the wrist, follows,
    closes when turned away; tap with the right hand's drawn fingertip opens it.
  PNGs: synth arm: watch on the wrist next to the hand, face on the back, strap round the forearm (good). RPM Men:
  watch invisible (inside the sweater). Menu above the wrist facing the eyes.
- DECISION: MeshScale 0.94 (RPM Women fit), Lift default 1 mm; NEW RrWatchFit.cs: per-avatar sleeve fit. Samples the
  local avatar's readable skinned meshes (any SMR with a bone at the drawn wrist or elbow; LBS from bindposes, chunked
  over frames), solves the smallest lift that keeps 4 mm under the case (can be negative for thin wrists, capped
  -12..+35 mm x size). Unreadable meshes -> default lift (logged). Test cross-checks it against BakeMesh.
- ~15:xx RrWatchFit.cs written (+meta 4733df2a68c04fc98deb2fbeba804f9b); RrWatchModel: MeshScale 0.94, Lift 0.001, fit
  state (MaybeFit/Fit coroutine, RefitNow, LiftUsed/Fitted/FitDone/FitReport/Contour); test: contour check, fit vs
  BakeMesh checks (T-pose both RPMs + posed stations), RPM Women station, twist-minimising RPM posing, back-of-wrist
  profile along the forearm. Compile 0 errors x3 / 113 proven. Harness run 3 (2nd real) starting.
- RUN 2 (3rd harness call): 60 steps, 29 PASS / 4 FAIL. Women T-pose fit: lift 0.6 mm, BakeMesh gap 4.4/4.2 mm (want
  4.0) PASS both wrists; sampler 0.24-0.28 ms (8429 verts), Men 0.36-3.99 ms (10712 verts; first call pays the copy).
  FAILS: (1) posed stations: the forearm lies across the chest and the RPM mesh is ONE mesh -> torso vertices counted as
  "sleeve" -> lift capped 35 mm. (2) Men T-pose: low-poly sweater, only 2-3 VERTICES under the case (rings of vertices
  ~4-5 cm apart; profile: back of wrist 25 mm at +1 cm, 29 at 0, 41 at -1, none at -2) -> MinTopPoints never met ->
  cap. Posing needs a -115 deg forearm twist whatever the upper arm (RPM has no twist bones).
  FIX: only vertices whose main bone is the lower-arm bone (bone at the drawn elbow) or below it; add triangle edge
  midpoints + centroids of all-arm triangles (denser surface). Test BakeMesh check uses the same selection/sampling
  (RrWatchFit.ArmBones / AddSurface) with Unity's skinning for the positions.
- ~15:xx fix done (ArmBones mask + AddSurface triangle midpoints + Window; RrWatchFit.Shutdown from RrWatch.Shutdown).
  Compile 0 errors x3 / 113 proven. Harness run 3 (4th call) starting.
- RUN 3 (4th call): 60/60 steps OK, 33 PASS / 0 FAIL. Fit: RPM Men lift 26.4 / 26.9 mm (sweater), BakeMesh gap 4.1 /
  4.4 mm; RPM Women lift 2.1 / 2.6 mm, gap 4.1 / 4.4 (under the wrist -0.7 / -1.1 mm = strap grazes the skin under the
  wrist, sides 10 mm). Posed stations same numbers. Sampler 0.45-0.73 ms per 8.4-10.7k-vertex avatar (first call in
  the editor 7.06 ms: JIT + list growth). PNGs: RPM Men: watch now ON TOP of the sweater (side + wide), was invisible.
  RPM Women (yellow hoodie, sleeve pushed up): watch on the bare forearm just past the cuff, strap round it (side, under).
  RPM "face" camera was inside the chest -> moved above the right shoulder (rpm_eye / women_eye). Run 4 (5th call) next,
  then watch_menu (6th).
- RUN 4 (5th call): 60/60 steps, 33 PASS, WatchTest: ALL PASS. rpm_eye / women_eye PNGs: face readable, Men's watch
  on top of the sweater, Women's on the bare wrist at the cuff. Next: watch_menu regression (6th, last run).
- RUN 5 (6th, last call): watch_menu regression: 25/25 steps OK; the same 2 URP shader-import errors as the baseline
  (harness, not ours). PNGs match the baseline except the Play page: new registries (other agents) name covers
  "GiftBox_Paintball_Tex|8,276,380,268" (RrCoverArt spec) and RrWatchArt.For passed the whole spec as the asset name
  -> 4 "picture not found" warnings, blank Paintball/Dodgeball/Paddleball/Charades cards. FIXED in RrWatchArt.For
  (parse "|x,y,w,h" into Asset + Crop). Compile 0 errors x3 / 113 proven. NOT harness-verified (run budget used).

## Status: DONE (result returned)
Files: Watch/RrWatchModel.cs (rewritten placement/gesture/tap/fit wiring), Watch/RrWatchFit.cs (new, sleeve fit),
Watch/RrWatchWristTest.cs (new, harness hooks), Watch/RrWatchUi.cs (order 10020 + doc), Watch/RrWatch.cs (doc, resets,
RrWatchFit.Shutdown), Watch/RrWatchArt.cs (cover spec crop parse), SP/harness/scenarios/watch_wrist.json (new).
Final constants: MeshScale 0.94, BackDistance 0.018, Lift 0.001 (default; fitted per avatar), MinSize 0.7 / MaxSize
1.4, fit: 4 mm under the case, lift -12..+35 mm x size.
