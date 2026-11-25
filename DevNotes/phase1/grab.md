# grab - held items follow the hand's orientation

Status: DONE (2026-09-24). Compile 0 errors x3, 113/113 proven. 7 real harness runs (+2 aborted in <6 s by the watch task's mid-edit compile errors).

## Files touched
- CODE/Interact/RrScreenHold.cs (NEW) - Rec Room screen-mode hand per tool (IdleAnimation), table + runtime read
- CODE/Interact/RrGrab.cs - hold pose = grip * HoldFrame * (gp,gr); IsScreen/HoldFrame/WorldHoldPose; VrMode; trigger ray = AimRay; wire pose; drawn-palm log
- CODE/Interact/RrGrabbable.cs - docs (tool-in-hand semantics), GripSource, HasScreenHand/ScreenHandPosition/Rotation
- CODE/Interact/RrTools.cs - ApplyHold: legacy first, FullBody (RootOverride corrected) second, proper mirror; screen hand
- CODE/Player/RrHand.cs - VrMode, RecRoomFrame, test mode (SetTestVr fake anchors, TestView), docs
- CODE/Net/RrNetBody.cs - WireHeldPose / PlaceFromWire (anchor-relative metres; desktop head-yaw frame), receiver uses it
- CODE/Interact/RrGrabTest.cs (NEW, meta fa71aee947a64151b167739cdb518433) - harness hooks Vr/VrRound2/VrEnd/Screen/ScreenPistol/ScreenEnd/Clear
- CODE/Interact/RrScreenHold.cs.meta (guid 09ce762d44e74c03a25a80f0fcaad02c, written by someone's metas.sh run)
- CODE/Games/Shared/RrWeapon.cs - Setup: RrTools.ApplyHold first (legacy-first + screen hand), spec copy only as fallback (3 lines)
- CODE/Games/Shared/RrMelee.cs - Attach: RrTools.ApplyHold; MeasureBlade: hand point in the sword frame = Inverse(gr) * -gp (2 lines)
- CODE/Games/Paintball/RrPaintballFlags.cs - VR carry (local + remote) = Rec Room's FlagTool LeftHandPickupTransform on the drawn-hand frame (was grip*Euler(-70,0,0): upside down)
- SP/harness/scenarios/grab_pose.json (NEW)

## Milestones
- [x] read hands_frames.md, hands.md, ARCHITECTURE hard rules, RrGrab, RrHand, RrGrabbable, RrTools, RrNetBody (held parts)
- [x] fix hold pose (RrGrab + RrTools legacy-first + mirrored rotations) - compiles 0 errors x3, 113 proven (12:xx)
- [x] audit all hold-frame paths (see Decisions / Audit)
- [x] test path + grab_pose.json scenario (written, compiles; harness run 1 next)
- [x] regressions (interact, dorm_room, paintball_all, paddleball_court, charades_play) - no regressions

## Findings
- ROOT CAUSE (from hands_frames.md 3): RrGrab.cs:207-208 applied Rec Room's hold pose as the HAND's pose in the tool
  (RestRotation = Inverse(gr), RestPosition = -(Inverse(gr)*gp)) and directly in RrHand.Grip's frame. Rec Room's
  {Left,Right}HandPickupTransform / FullBody* are the TOOL's local pose in Rec Room's hand frame (+Z fingers, +Y thumb),
  and that frame is Grip * G (G = RrAvatarHand.GripToRecRoomDefault = GripToAim * Euler(-23.9,-+12.1,+-4.3)); the grip
  frame's own +Z is the thumb axis, so every tool was ~90 deg off (bottle along the forearm, pistol at the floor).
- RrTools preferred FullBody (relative to HandVisualPickupInfo.RootOverride when set - pistol points backward); must be
  legacy first. Its one-side fallback reused the rotation unmirrored (RR mirror: p.x -> -p.x, (x,y,z,w) -> (x,-y,-z,w)).
- Screen mode: the screen grip is view * Inverse(GripToAim); "no pose" objects got RestRotation identity = lying with
  their up axis toward the camera (comment said upright). Fix: RestRotation = GripToAim (upright to the view).
- Rec Room screen mode data: Tool.screenSettings.IdleAnimation = PlayerAnimationAsset (frames with RightHand/Head poses
  in the body frame + right/left hand position/rotation offsets). PaintballGun: Screens_FPS_PBPistol_AttackReady
  (43ea1176 #432506639366587634): right hand (0.1265,1.3602,0.4734)+(0,-0.06,0.05), rot (-0.2088,-0.0148,0.0008,0.9778)
  ~ Euler(-24,-1.7,0) (fingers 24 deg UP); head (0.014,1.503,0.117); camera offset (0,-0.05,0).
- Remote display: receiver places the prop at remote Bridge hand.TransformPoint(sent) (scale-dependent), sender sent the
  pose relative to our MEASURED grip (== anchor in the user's log, but not in general). Desktop players have null Bridge
  hands (somnium_player.md 8.2), so a desktop holder's prop froze remotely at its last timeline sample.
- Bridge hand anchors: parenting our Grip under Body.LeftHand/RightHand is the first build's design and the Bridge's
  own example (BridgePlayerAttach instantiates under Body.RightHand). Not the avatar model. Kept.

## Decisions

## Verification

## Screen-mode data (measured 2026-09-24, SCRATCH = 1c09.../scratchpad/grab/, calc grab/scalc)
Rec Room screen hand = Tool.screenSettings.IdleAnimation (PlayerAnimationAsset): frame 0 RightHand pose + rightHandPositionOffset
(body frame), rotation * Euler(rightHandRotationOffset); RR screen camera = root + (0,1.55,0.01) + cameraPositionOffset
(FirstPersonScreenPlayerCameraSettings pivot (0,1.35,-0.08) + (0,0.2,0.09)). Check of the convention: tool = hand * (gp,gr)
gives PB pistol barrel 4.1 deg from view forward, rifle 1.0, crossbow 1.6, pen tip 4.3, cup up 3.4 deg from view up,
bottle 15.6 deg back (drinking tilt). View-space hand poses (pos, rot):
- PBPistol Screens_FPS_PBPistol_AttackReady 43ea #432506639366587634 (0.1265,-0.1998,0.5134) (-0.2088,-0.0037,0.0012,0.9779)
- Rifle Screens_FPS_BurstGun_AttackReady 43ea #-1548489987572327908 (0.1634,-0.2377,0.2186) (-0.1793,-0.0063,-0.0008,0.9838)
- Crossbow Screens_FPS_Crossbow_Idle 43ea #6182427797016904464 (0.1474,-0.2509,0.4917) (0.0416,0.0131,-0.0090,0.9990)
- Longbow Screens_FPS_Longbow_Idle 43ea #6174749835601728300 (-0.0906,-0.1235,0.4177) (-0.4098,-0.1174,0.4514,0.7839)
- RootBeer Screens_RecRoyalePotionIdle_01 1df8 #-5624923231098364159 (0.1682,-0.1196,0.4049) (-0.1899,0.0498,-0.0155,0.9804)
- SoloCup Screens_GenericHoldIdle_02 1df8 #-1620251211297073511 (0.1982,-0.2496,0.4849) (-0.0071,-0.0566,-0.0292,0.9979)
- Pen Screens_CharadesPen_Idle 43ea #4105407807365469649 (0.1785,-0.1698,0.3811) (-0.2952,-0.0309,0.0027,0.9549)
- Sword Screens_FPS_QuestSwordAndShield_Idle_01 1df8 #-4179572484253039610 (0.2030,-0.2751,0.4701) (-0.1167,0.0159,-0.0283,0.9926)
- Paddle Screens_PaddleballPaddle_Idle 43ea #-2452710493553015242 (0.3652,-0.4167,0.4132) (-0.1965,0.3422,0.2113,0.8942)
- PingPaddle Screens_PaddleballPaddle_ActionReady 43ea #8873110154974575101 (0.4487,-0.0144,0.4522) (-0.5603,0.5192,0.4385,0.4735)
- Basketball Screens_Basketball_Idle 43ea #-6281822192477224967 (0.1954,-0.0498,0.3639) (-0.2253,0.0334,-0.4976,0.8369)
- Dodgeball Screens_Dodgeball_Idle 43ea #3713113946385890874 (0.2210,-0.0920,0.3375) (-0.3749,0.2509,-0.4184,0.7883)
- DEFAULT Screens_Default_Idle_HoldingTool_02 1df8 #2259509279752159487 (0.2165,-0.2337,0.3717) (-0.0325,0.1052,-0.0086,0.9939)
- (FlagTool uses Screens_Generic_Offhand; PoolCue has no IdleAnimation.)
VR expectations for an upright fist pointing forward (grip = Euler(-90,0,0)), G from hands_frames: pistol barrel 11.4 deg from
forward (old: 110), bottle up 17.9 deg from world up (old: 96.5), paddle head 17.6 from up, ball centre 12.6 cm on the palm side.
Sword: the hand in the tool frame is (0,-0.011,0.101) (RrMelee used GripPosition (-0.011,-0.071,-0.071) as that point).

## Harness run log (max 8)
1. grab_pose (run 1): VR 15/15 PASS (bottle 17.9 deg from up [old 96.5], pistol barrel 11.4 from aim [old 110.0], ball 12.6 cm
   palm side [old -12.3], paddle head 17.7 from up; exact path 0.00 mm/0.000 deg; wire rebuilt 0.41-0.59 mm / 0.00 deg vs the
   first build's 30.2 mm / 8.7 deg on the scaled+offset anchor). Screen: bottle 15.6 deg lean, 18 deg right / 28 below; pistol
   barrel 4.1 deg from view fwd, 11.5 right / 16.7 below; screen wire 0.00 mm. ONE FAIL: screen-hand-data 1.14 deg = the table
   quaternions' 4-decimal rounding (|q| 0.99996 reads as ~1 deg in Quaternion.Angle) -> Entry now normalises. PNGs looked at:
   grab_vr_1_pov/side (bottle upright in the left fist, pistol level pointing forward), grab_vr_2_pov (ball on the left palm
   side, paddle head up), grab_screen_bottle (bottle lower right, neck up, cut by the bottom edge), grab_screen_pistol (FPS-style).
2. interact: 6/6 passed (drink: bottle tipped at the mouth, pouring; eat, break, ball, button OK). First screen pickup log:
   "root beer in the tool's screen hand; up 4 deg from world up".
3. dorm_room: 18/19 - the ONE failure "DormTest: basket: SCORED" is PRE-EXISTING (baseline failed the same step: ball crossed
   the rim plane 0.017 m from centre, now 0.019 m, 0 scores). Not a hold-pose path (the test launches the ball).
4. paintball_all: 29/29 steps OK (baseline 29/29). Screen pickup: pistol legacy pose in its screen hand, forward 4 deg from aim.
   First shot: barrel forward (-0.01, 0.07, 1.00).
5. paddleball_court: ABORTED in 6 s - compile error in Watch/RrWatchModel.cs (watch task mid-edit). Retry after it settles.
6. paddleball_court (retry): 18/18 OK (baseline 18/18). Screen paddle from its IdleAnimation (Rec Room's paddle idle: 65 deg from up).
7. charades_play: first try ABORTED in 5 s (Watch/RrWatchWristTest.cs mid-edit); retry 23/23 OK, 3 strokes drawn, pen held
   (screen: legacy pose in its screen hand, 4 deg from the aim).
- Added no-pose checks (vr-nopose-where, vr-nopose-given, screen-nopose) to RrGrabTest + scenario; final grab_pose run next.
8. grab_pose (final): 40/40 steps OK; VR 17/17 (incl. vr-nopose-where 0.00 mm, vr-nopose-given 0.00 mm), screen 7/7
   (screen-hand-data 0.08 mm / 0.00 deg after normalising; screen-nopose centred 0.00 mm, 0.00 deg from upright to the view).

## Decisions
- VR frame = RrAvatarHand.GripToRecRoomDefault (constant), props stay parented to the grip. Reason: the client's default IK
  draws the palm ON the grip centre (0.1 mm; RPM rig 1.6 cm), so "place in the drawn palm" is not needed; a one-shot log per
  hand ("Grab: <side> hand: the drawn palm (<source>) sits X cm and Y deg from the frame held objects use") gives the headset's
  real numbers. Pitch-only variant (GripToAim * Euler(-23.9,0,0)) stays a one-line change in RrHand.RecRoomFrame if the
  pistol's 10.7 deg outward yaw feels wrong.
- Screen: tool = view * RrScreenHold hand (IdleAnimation) * (gp,gr); no-pose objects centred at ScreenOffset upright to the view.
- Throw velocity unchanged (controller / grip history).
- Kept the Grip under the Bridge hand anchor (first-build design; Bridge's own BridgePlayerAttach example does it; not the avatar).

## Audit (every path that depends on the hold frame)
- pickup + slide-in: From = world pose at grab, Rest = new pose; unchanged mechanics.
- drink/eat: PoseAtMouth uses Grip*Rest (consistent); VR Tipped()/AtMouth use the bottle's own axis -> bottle now upright
  when held upright (first build: ~90 deg, i.e. tipped).
- two-handed: no two-hand hold exists (TwoHanded is set, unused); hand-to-hand re-applies the other side's mirrored pose.
- LockOnPickup / release / throw: unchanged; v + w x (centre - grip) still physical.
- screen hand: RrHand grip unchanged (view turned back by GripToAim); HoldFrame carries the tool's view-space hand into it.
- remote display: WireHeldPose/PlaceFromWire (anchor-relative metres, no scale; desktop = head + body yaw). Harness: 0.4-0.6 mm
  vs the first build's 27-30 mm / 8.7 deg on a 0.889-scaled anchor with a calibration offset.
- weapons: RrWeapon reads the hold via RrTools.ApplyHold (legacy first + screen hand); shots from Barrel.position along the
  barrel (VR) / to the crosshair (screen) - unchanged, now the barrel points where the gun is drawn. RrProjectile: nothing.
- paddle face box, charades ExtrusionPoint/Tip, pool cue ends, ping-pong paddle velocity: all read the tool's own transforms.
- swords: RrMelee reads ApplyHold; blade-tip heuristic now uses the hand's point in the sword frame (Inverse(gr) * -gp).
- bows/crossbows: RrWeapon path (legacy poses: longbow Euler(40,0,0) (0,0.038,0.015), crossbow (0,-0.024,0.229)).
- paintball flags: VR carry (local + remote) now Rec Room's FlagTool LeftHandPickupTransform on the drawn-hand frame.
- dodgeballs: FullBody pose (legacy zero) -> ball on the palm side, like the basketball.
- VR trigger ray for buttons (RrGrab.VrHand) was grip.forward = the THUMB axis (up); now hand.AimRay (front of the fist).
