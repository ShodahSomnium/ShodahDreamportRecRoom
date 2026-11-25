using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    public enum RrHandSide
    {
        Left,
        Right
    }

    /// <summary>
    /// One of the player's two hands as this world sees them: our own transforms following the controller (VR) or
    /// placed in the view (screen mode), with the buttons that matter for grabbing and using things and a velocity
    /// estimate for throws. Held objects attach to <see cref="Grip"/>. Created by <see cref="RrPlayer.Init"/> as
    /// "Rr Hand L" / "Rr Hand R" under the player object; the Somnium avatar is never touched.
    ///
    /// <b>Where the grip frame lives, and why.</b>
    /// - VR with the client's body: <see cref="Grip"/> is a child we add under the Bridge
    ///   <c>Body.LeftHand/RightHand</c>, which are the client's <c>Left/RightHandAnchor</c> - the tracked controller,
    ///   not the avatar's IK hand (somnium_player.md §1.1). The anchors are driven "Update and BeforeRender", so a child
    ///   is re-posed just before rendering with no one-frame swim at 90 Hz; that is why the client hangs its own
    ///   interactors there (§2.3). The anchors are re-read until non-null (they arrive after the world starts).
    /// - The anchor may be the OpenXR <i>aim</i> pose rather than the grip pose (an XRI controller with
    ///   pointerPosition first shares the object with a legacy grip-pose driver, §1.2). So the grip frame's local
    ///   offset is measured, not assumed: each LateUpdate the controller's OpenXR grip pose (converted to world with
    ///   the HMD pair, <see cref="RrXr.TryGetGripPose"/>) is expressed in the anchor's space; once 30 samples agree
    ///   (spread under 1.5 cm and 3 deg) the offset is frozen and logged - the §9.6 probe, done in code for whatever
    ///   runtime and controller the player has. Until then (or if they never agree) the frame follows the live
    ///   measurement. Without a device pose it stays at the anchor (identity).
    /// - VR without a Bridge hand (editor with a headset, client failing): the converted grip pose itself.
    /// - Screen mode (desktop): the right hand is a virtual hand in the view, lower right like Rec Room's screen mode
    ///   (<see cref="ScreenOffset"/> from the camera, somnium_player.md §5.4). With the client's third-person orbit
    ///   camera it stays beside the avatar's head instead (<see cref="ThirdPersonOffset"/>). The left hand is unused
    ///   (<see cref="Tracked"/> false).
    ///
    /// <b>Orientation convention (same for every source).</b> <see cref="Grip"/> is the OpenXR grip pose: +Z runs
    /// through the fist from little finger to thumb, and -Y comes out of the front of the fist. <see cref="AimRotation"/>
    /// = Grip * <see cref="GripToAim"/> (Euler 90,0,0 - Quake's and AoM's constant, proven on this client) points
    /// where the controller points (pointing rays, button presses). The screen hand is built the other way round: its
    /// aim is the view, and its grip is the view turned back by GripToAim.
    ///
    /// <b>Held objects use Rec Room's hand frame, not the grip.</b> Rec Room's hold poses are the tool's pose in its
    /// PlayerHand frame (+Z fingers, +Y thumb side, +X out of the left palm / back of the right hand). In VR that
    /// frame is the hand the client DRAWS: <see cref="RecRoomFrame"/> = <see cref="RrAvatarHand.GripToRecRoomDefault"/>,
    /// AimRotation's axes turned by 26.7 deg (the drawn fingers point 24 deg above the fist's front and 12 deg
    /// outward: the power grip's obliquity, hands_frames.md 2.3). The drawn palm centre is the grip centre (0.1 mm at
    /// the client's default IK, 1.6 cm on the RPM template rig), so held objects stay parented to the grip - with the
    /// BeforeRender pose - and only the rotation changes. In screen mode RrGrab uses the tool's own screen hand
    /// (<see cref="Interact.RrScreenHold"/>).
    ///
    /// <b>Buttons.</b> VR: grip and trigger with hysteresis (on 0.55, off 0.35: <c>EtsSteeringGrab</c>), edges once per
    /// frame, read before any other script's Update (<see cref="RrInputPump"/>). They are raw - they keep reading while
    /// a menu has the input, so a held prop is not dropped when the tablet opens (conventions §24 item 27); check
    /// <see cref="RrInput.Blocked"/> before starting new actions. Screen mode: the right hand mirrors
    /// <see cref="RrInput"/> - trigger = use (left mouse), grip down = grab (E), grip up = drop or toss (Q).
    ///
    /// <b>Velocity.</b> The grip's world pose is sampled every LateUpdate (after the camera and rig have moved) into
    /// an 8-sample ring; <see cref="Velocity"/> and <see cref="AngularVelocity"/> are the change over the last
    /// ~90 ms. The ring is cleared on teleports, when the grip changes parent, and on any jump over 1.5 m in one frame
    /// (a respawn or seat move we did not make), so a throw never inherits a teleport.
    ///
    /// Held objects parented to <see cref="Grip"/> must keep their own (kinematic) Rigidbody: otherwise, in VR, their
    /// colliders would join the client's kinematic 0.1 kg anchor body as a compound collider.
    /// </summary>
    public sealed class RrHand : MonoBehaviour
    {
        /// <summary>What places the grip frame this frame.</summary>
        public enum PoseSource
        {
            None,
            BridgeAnchor,
            TrackedDevice,
            ScreenFirstPerson,
            ScreenThirdPerson
        }

        public static RrHand Left { get; internal set; }
        public static RrHand Right { get; internal set; }

        public const float GripOn = 0.55f;
        public const float GripOff = 0.35f;
        public const float TriggerOn = 0.55f;
        public const float TriggerOff = 0.35f;

        /// <summary>Screen-mode right hand, in the camera's space (lower right of the view). Left mirrors x.</summary>
        public static readonly Vector3 ScreenOffset = new Vector3(0.22f, -0.2f, 0.45f);

        /// <summary>Screen-mode hand with the client's orbit camera: from the avatar's head, in the view's yaw frame.</summary>
        public static readonly Vector3 ThirdPersonOffset = new Vector3(0.25f, -0.35f, 0.35f);

        /// <summary>Grip pose to "where a held pistol points" (Quake <c>_gripToAim</c>, AoM <c>GripToAim</c>).</summary>
        public static readonly Quaternion GripToAim = Quaternion.Euler(90f, 0f, 0f);

        private const int HistorySize = 8;
        private const float HistoryWindow = 0.09f;
        private const float JumpDistance = 1.5f;
        private const int CalibrationSamples = 30;
        private const int CalibrationAttempts = 5;

        public RrHandSide Side;

        /// <summary>Where a held object's grip point goes. May be replaced (see <see cref="GripChanged"/>).</summary>
        public Transform Grip;

        /// <summary>World velocity and angular velocity (rad/s) from the pose history (for throws).</summary>
        public Vector3 Velocity;
        public Vector3 AngularVelocity;

        public float Trigger;
        public float GripValue;
        public bool TriggerHeld, TriggerDown, TriggerUp;
        public bool GripHeld, GripDown, GripUp;

        /// <summary>A (right) or X (left) pressed this frame - note the client owns both (jump; inspector).</summary>
        public bool PrimaryDown;

        /// <summary>The thing this hand holds (set by the grab system). On desktop, holding puts the view in play mode.</summary>
        public Object Held;

        /// <summary>True when this hand is tracked (VR controller present, or the screen-mode hand exists).</summary>
        public bool Tracked;

        /// <summary>Raised when <see cref="Grip"/> is re-parented (new anchor, mode change) or rebuilt after the client destroyed it.</summary>
        public event Action<RrHand> GripChanged;

        public PoseSource Source { get; private set; }

        /// <summary>The Bridge hand transform the grip rides on, or null (desktop, fallback).</summary>
        public Transform Anchor { get; private set; }

        /// <summary>VR: the grip frame's offset under the anchor has been measured and frozen.</summary>
        public bool Calibrated { get; private set; }

        public Vector3 GripOffsetPosition { get; private set; }

        public Quaternion GripOffsetRotation { get; private set; } = Quaternion.identity;

        /// <summary>Times the grip was destroyed with the client's anchor and rebuilt (anything held on it went too).</summary>
        public int GripRebuilds { get; private set; }

        public bool IsScreenHand
        {
            get { return Source == PoseSource.ScreenFirstPerson || Source == PoseSource.ScreenThirdPerson; }
        }

        public Vector3 Position
        {
            get { return Grip != null ? Grip.position : transform.position; }
        }

        public Quaternion Rotation
        {
            get { return Grip != null ? Grip.rotation : transform.rotation; }
        }

        /// <summary>Where a pistol held in this hand points (see the orientation convention above).</summary>
        public Quaternion AimRotation
        {
            get { return Rotation * GripToAim; }
        }

        public Ray AimRay
        {
            get { return new Ray(Position, AimRotation * Vector3.forward); }
        }

        /// <summary>
        /// VR hand logic is in effect: a headset, or the harness's test mode (<see cref="SetTestVr"/>). The grab system
        /// decides VR vs screen with this, so a test can exercise the VR hold path without a headset.
        /// </summary>
        public static bool VrMode
        {
            get { return RrPlayer.InVr || TestVr; }
        }

        /// <summary>
        /// Rec Room's hand frame in <see cref="Grip"/>'s local space (metres; the grip carries no scale): what Rec Room's
        /// Tool.interactionSettings poses are relative to, so a held tool's local pose under the grip is
        /// (p + q * gp, q * gr). VR: the client's default-IK drawn hand, <see cref="RrAvatarHand.GripToRecRoomDefault"/>
        /// (left (0.5396, -0.0777, -0.0576, 0.8363) = GripToAim * Euler(-23.9, -12.1, 4.3); right mirrored; position
        /// 0). Screen hand (and an untracked desktop hand): the view itself, (0, GripToAim) - RrGrab replaces it with
        /// the tool's own screen hand when it has one.
        /// </summary>
        public Pose RecRoomFrame
        {
            get
            {
                if (VrMode && !IsScreenHand)
                {
                    return RrAvatarHand.GripToRecRoomDefault(Side);
                }

                return new Pose(Vector3.zero, GripToAim);
            }
        }

        // ---- test mode (harness only: nothing calls these in a build) ---------------------------------------------

        /// <summary>Test only: VR logic without a headset (see <see cref="SetTestVr"/>).</summary>
        internal static bool TestVr { get; private set; }

        /// <summary>Test only: the screen hand follows this view instead of the camera (a screenshot can match it).</summary>
        internal static Pose? TestView;

        private Transform _testAnchor;
        private static bool _loggedTest;

        /// <summary>
        /// Test only: puts both hands in VR mode on fake client anchors. Each hand's grip goes under its
        /// <paramref name="left"/> / <paramref name="right"/> anchor with the given calibrated offset - the real
        /// PoseVr path (parenting, rig-scale compensation) minus the OpenXR probe - and its grip button reads held (so
        /// a held object is not dropped). Null anchors end test mode: the grips go back under the hands, calibration
        /// is forgotten, and the next LatePose places them as usual. Harness scenario grab_pose.json.
        /// </summary>
        internal static void SetTestVr(Transform left, Pose leftOffset, Transform right, Pose rightOffset)
        {
            bool on = left != null || right != null;
            TestVr = on;
            SetTestAnchor(Left, left, leftOffset);
            SetTestAnchor(Right, right, rightOffset);
            if (on && !_loggedTest)
            {
                _loggedTest = true;
                RrLog.Info("Hand", "TEST MODE: VR hands on fake anchors (harness); held objects use the VR hold path");
            }
        }

        private static void SetTestAnchor(RrHand hand, Transform anchor, Pose offset)
        {
            if (hand == null)
            {
                return;
            }

            hand.EnsureGrip();
            hand._testAnchor = anchor;
            if (anchor != null)
            {
                hand.GripOffsetPosition = offset.position;
                hand.GripOffsetRotation = offset.rotation;
                hand.Calibrated = true;
                hand._calibratedFor = anchor;
                hand.Anchor = null;
                hand.SetButtons(0f, false, 1f, true);
                return;
            }

            hand.GripOffsetPosition = Vector3.zero;
            hand.GripOffsetRotation = Quaternion.identity;
            hand.Calibrated = false;
            hand._calibratedFor = null;
            hand.Anchor = null;
            hand.ParentGrip(hand.transform, Vector3.zero, Quaternion.identity);
            hand.ClearButtons();
        }

        /// <summary>Test mode keeps the grip "held" and the trigger released, whatever the pump reads.</summary>
        private bool HoldTestButtons()
        {
            if (!TestVr || _testAnchor == null)
            {
                return false;
            }

            SetButtons(0f, false, 1f, true);
            PrimaryDown = false;
            return true;
        }

        private readonly Vector3[] _pos = new Vector3[HistorySize];
        private readonly Quaternion[] _rot = new Quaternion[HistorySize];
        private readonly float[] _time = new float[HistorySize];
        private int _count;
        private int _next;

        private bool _gripMade;
        private Transform _calibratedFor;
        private int _samples;
        private int _attempts;
        private bool _gaveUp;
        private readonly Vector3[] _samplePos = new Vector3[CalibrationSamples];
        private readonly Quaternion[] _sampleRot = new Quaternion[CalibrationSamples];
        private bool _loggedAnchor;
        private bool _loggedDevice;
        private bool _loggedScreen;

        public static RrHand Get(RrHandSide side)
        {
            return side == RrHandSide.Left ? Left : Right;
        }

        /// <summary>A controller buzz (VR, where the runtime offers impulses); nothing on desktop.</summary>
        public void Haptic(float amplitude, float seconds)
        {
            if (RrPlayer.InVr)
            {
                RrXr.Pulse(Side, amplitude, seconds);
            }
        }

        /// <summary>Forgets the pose history (velocity reads zero until two new samples exist).</summary>
        public void ClearHistory()
        {
            _count = 0;
            _next = 0;
            Velocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
        }

        internal static RrHand Create(Transform parent, RrHandSide side)
        {
            var go = new GameObject(side == RrHandSide.Left ? "Rr Hand L" : "Rr Hand R");
            go.transform.SetParent(parent, false);
            var hand = go.AddComponent<RrHand>();
            hand.Side = side;
            hand.EnsureGrip();
            return hand;
        }

        // ---- buttons (the input pump, before every other Update) -----------------------------------------

        internal void PollVr()
        {
            if (HoldTestButtons())
            {
                return;
            }

            float trigger = RrXr.Trigger(Side);
            float grip = RrXr.Grip(Side);
            bool triggerHeld = TriggerHeld ? trigger > TriggerOff : trigger > TriggerOn;
            bool gripHeld = GripHeld ? grip > GripOff : grip > GripOn;
            bool primary = RrXr.Primary(Side);
            SetButtons(trigger, triggerHeld, grip, gripHeld);
            PrimaryDown = primary && !_primaryWas;
            _primaryWas = primary;
        }

        private bool _primaryWas;

        /// <summary>Screen mode: the right hand's buttons mirror RrInput (use = trigger, grab = grip down, drop/toss = grip up).</summary>
        internal void MirrorScreen()
        {
            if (HoldTestButtons())
            {
                return;
            }

            bool triggerHeld = RrInput.UseHeld || RrInput.UseDown;
            bool gripHeld = GripHeld;
            if (RrInput.GrabDown)
            {
                gripHeld = true;
            }

            if (RrInput.DropDown || RrInput.ThrowDown)
            {
                gripHeld = false;
            }

            SetButtons(triggerHeld ? 1f : 0f, triggerHeld, gripHeld ? 1f : 0f, gripHeld);

            // Edges follow the keys, not the latch: a second E while "holding" is still a grab attempt (Rec Room's E
            // picks up whatever is aimed at), and Q always lets go, also of something picked up with a click.
            GripDown = RrInput.GrabDown;
            GripUp = RrInput.DropDown || RrInput.ThrowDown;
            PrimaryDown = false;
            _primaryWas = false;
        }

        internal void ClearButtons()
        {
            if (HoldTestButtons())
            {
                return;
            }

            SetButtons(0f, false, 0f, false);
            PrimaryDown = false;
            _primaryWas = false;
        }

        private void SetButtons(float trigger, bool triggerHeld, float grip, bool gripHeld)
        {
            TriggerDown = triggerHeld && !TriggerHeld;
            TriggerUp = !triggerHeld && TriggerHeld;
            TriggerHeld = triggerHeld;
            Trigger = trigger;
            GripDown = gripHeld && !GripHeld;
            GripUp = !gripHeld && GripHeld;
            GripHeld = gripHeld;
            GripValue = grip;
        }

        // ---- pose (the player driver, LateUpdate at execution order 10000) --------------------------------

        internal void LatePose()
        {
            EnsureGrip();
            if (VrMode)
            {
                PoseVr();
            }
            else if (Side == RrHandSide.Right)
            {
                PoseScreen();
            }
            else
            {
                PoseUnusedScreenHand();
            }

            if (Tracked)
            {
                Sample();
            }
            else if (_count > 0)
            {
                ClearHistory();
            }
        }

        private void PoseVr()
        {
            Transform anchor = TestVr ? _testAnchor : RrPlayer.BridgeHand(Side);
            if (anchor != null)
            {
                if (Anchor != anchor || Grip.parent != anchor)
                {
                    Anchor = anchor;
                    if (_calibratedFor != anchor)
                    {
                        Calibrated = false;
                        _samples = 0;
                        _attempts = 0;
                        _gaveUp = false;
                    }

                    ParentGrip(anchor, GripOffsetPosition, GripOffsetRotation);
                    if (!_loggedAnchor)
                    {
                        _loggedAnchor = true;
                        RrLog.Info("Hand", SideName + " grip frame under the client's '" + anchor.name + "' (layer " + anchor.gameObject.layer +
                                           ", lossy scale " + anchor.lossyScale.x.ToString("0.###") + ")");
                    }
                }

                CompensateScale(anchor);
                if (!Calibrated)
                {
                    Calibrate(anchor);
                }

                transform.SetPositionAndRotation(Grip.position, Grip.rotation);
                Source = PoseSource.BridgeAnchor;
                Tracked = true;
                return;
            }

            Anchor = null;
            if (RrXr.TryGetGripPose(Side, out Vector3 p, out Quaternion r))
            {
                ParentGrip(transform, Vector3.zero, Quaternion.identity);
                transform.SetPositionAndRotation(p, r);
                Source = PoseSource.TrackedDevice;
                Tracked = true;
                if (!_loggedDevice)
                {
                    _loggedDevice = true;
                    RrLog.Info("Hand", SideName + ": no Bridge hand - following the controller's converted OpenXR grip pose");
                }

                return;
            }

            Source = PoseSource.None;
            Tracked = false;
        }

        private void PoseScreen()
        {
            Anchor = null;
            Camera cam = RrPlayer.Camera;
            if (cam == null)
            {
                Source = PoseSource.None;
                Tracked = false;
                return;
            }

            ParentGrip(transform, Vector3.zero, Quaternion.identity);
            Transform view = cam.transform;
            Vector3 eye = TestView.HasValue ? TestView.Value.position : view.position;
            Quaternion aim = TestView.HasValue ? TestView.Value.rotation : view.rotation;
            Transform head = RrPlayer.HeadAnchor;
            bool firstPerson = TestView.HasValue || RrPlayer.InPlayMode || RrPlayer.UsingFallbackRig || head == null ||
                               (eye - head.position).sqrMagnitude < 0.6f * 0.6f;
            Vector3 position;
            if (firstPerson)
            {
                position = eye + aim * ScreenOffset;
                Source = PoseSource.ScreenFirstPerson;
            }
            else
            {
                Vector3 flat = Vector3.ProjectOnPlane(view.forward, Vector3.up);
                Quaternion yaw = flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat, Vector3.up) : Quaternion.identity;
                position = head.position + yaw * ThirdPersonOffset;
                Source = PoseSource.ScreenThirdPerson;
            }

            transform.SetPositionAndRotation(position, aim * Quaternion.Inverse(GripToAim));
            Tracked = true;
            if (!_loggedScreen)
            {
                _loggedScreen = true;
                RrLog.Info("Hand", "screen mode: right hand = virtual hand at " + Fmt(ScreenOffset) + " m in the view of camera '" + cam.name +
                                   "' (" + Source + "); left hand unused");
            }
        }

        private void PoseUnusedScreenHand()
        {
            Anchor = null;
            Source = PoseSource.None;
            Tracked = false;
            Camera cam = RrPlayer.Camera;
            if (cam == null)
            {
                return;
            }

            // Kept at the mirrored view position so anything attached by mistake stays in sight, not at the origin.
            ParentGrip(transform, Vector3.zero, Quaternion.identity);
            Transform view = cam.transform;
            var mirrored = new Vector3(-ScreenOffset.x, ScreenOffset.y, ScreenOffset.z);
            transform.SetPositionAndRotation(view.position + view.rotation * mirrored, view.rotation * Quaternion.Inverse(GripToAim));
        }

        /// <summary>Makes the grip transform, or rebuilds it when the client destroyed it together with its anchor.</summary>
        private void EnsureGrip()
        {
            if (Grip != null)
            {
                return;
            }

            bool rebuilt = _gripMade;
            var go = new GameObject(Side == RrHandSide.Left ? "Rr Grip L" : "Rr Grip R");
            go.layer = gameObject.layer;
            Grip = go.transform;
            Grip.SetParent(transform, false);
            _gripMade = true;
            Anchor = null;
            _calibratedFor = null;
            Calibrated = false;
            if (rebuilt)
            {
                GripRebuilds++;
                RrLog.WarnOnce("Hand", "rebuilt-" + Side, SideName + " grip frame was destroyed with the client's hand anchor (avatar or mode change?) - rebuilt; " +
                                                         "anything held on it went with it (logged once)");
                ClearHistory();
                RaiseGripChanged();
            }
        }

        private void ParentGrip(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            if (Grip.parent == parent)
            {
                return;
            }

            Grip.SetParent(parent, false);
            Grip.localPosition = localPosition;
            Grip.localRotation = localRotation;
            Grip.localScale = Vector3.one;
            ClearHistory();
            RaiseGripChanged();
        }

        /// <summary>Held props must keep their size even if the client's rig is scaled (avatar height).</summary>
        private void CompensateScale(Transform anchor)
        {
            Vector3 s = anchor.lossyScale;
            bool unit = Mathf.Abs(s.x - 1f) < 0.001f && Mathf.Abs(s.y - 1f) < 0.001f && Mathf.Abs(s.z - 1f) < 0.001f;
            Vector3 wanted = unit || s.x == 0f || s.y == 0f || s.z == 0f ? Vector3.one : new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            if ((Grip.localScale - wanted).sqrMagnitude > 1e-8f)
            {
                Grip.localScale = wanted;
            }
        }

        /// <summary>
        /// The §9.6 probe in code: the controller's OpenXR grip pose in the anchor's space. While not settled the grip
        /// follows the live value (it is right every frame; only noisy if the HMD conversion is); once 30 samples agree
        /// it is frozen, so later head motion cannot jiggle it.
        /// </summary>
        private void Calibrate(Transform anchor)
        {
            if (!RrXr.TryGetGripPose(Side, out Vector3 gp, out Quaternion gr))
            {
                return;
            }

            Vector3 lp = anchor.InverseTransformPoint(gp);
            Quaternion lr = Quaternion.Inverse(anchor.rotation) * gr;
            Grip.localPosition = lp;
            Grip.localRotation = lr;
            if (_gaveUp)
            {
                return;
            }

            if (_samples > 0 && Quaternion.Dot(_sampleRot[0], lr) < 0f)
            {
                lr = new Quaternion(-lr.x, -lr.y, -lr.z, -lr.w);
            }

            _samplePos[_samples] = lp;
            _sampleRot[_samples] = lr;
            _samples++;
            if (_samples < CalibrationSamples)
            {
                return;
            }

            Vector3 meanPos = Vector3.zero;
            Vector4 sum = Vector4.zero;
            for (int i = 0; i < _samples; i++)
            {
                meanPos += _samplePos[i];
                sum += new Vector4(_sampleRot[i].x, _sampleRot[i].y, _sampleRot[i].z, _sampleRot[i].w);
            }

            meanPos /= _samples;
            sum.Normalize();
            var meanRot = new Quaternion(sum.x, sum.y, sum.z, sum.w);
            float maxPos = 0f;
            float maxAngle = 0f;
            for (int i = 0; i < _samples; i++)
            {
                maxPos = Mathf.Max(maxPos, Vector3.Distance(_samplePos[i], meanPos));
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(_sampleRot[i], meanRot));
            }

            _samples = 0;
            _attempts++;
            string measured = "offset " + Fmt(meanPos) + " (" + meanPos.magnitude.ToString("0.000") + " m), rotation " + Fmt(meanRot.eulerAngles) +
                              " (" + Quaternion.Angle(Quaternion.identity, meanRot).ToString("0.0") + " deg); spread " + (maxPos * 100f).ToString("0.0") +
                              " cm / " + maxAngle.ToString("0.0") + " deg";
            if (maxPos < 0.015f && maxAngle < 3f && meanPos.magnitude < 0.25f)
            {
                GripOffsetPosition = meanPos;
                GripOffsetRotation = meanRot;
                Grip.localPosition = meanPos;
                Grip.localRotation = meanRot;
                Calibrated = true;
                _calibratedFor = anchor;
                float angle = Quaternion.Angle(Quaternion.identity, meanRot);
                RrLog.Info("Hand", SideName + " grip frame calibrated against the OpenXR grip pose under '" + anchor.name + "': " + measured +
                                   (angle < 5f && meanPos.magnitude < 0.02f ? " -> the anchor IS the grip pose" : " -> the anchor is another pose (aim/pointer?)"));
            }
            else if (_attempts >= CalibrationAttempts)
            {
                _gaveUp = true;
                RrLog.Warn("Hand", SideName + " grip calibration did not settle after " + _attempts + " tries (last: " + measured +
                                   ") - the grip frame keeps following the live measurement");
            }
        }

        private void Sample()
        {
            Vector3 p = Grip.position;
            Quaternion q = Grip.rotation;
            float t = Time.time;
            if (_count > 0)
            {
                int last = (_next - 1 + HistorySize) % HistorySize;
                if ((p - _pos[last]).sqrMagnitude > JumpDistance * JumpDistance)
                {
                    ClearHistory();
                }
                else if (t <= _time[last])
                {
                    // Paused or the same frame twice: replace the newest sample instead of dividing by zero later.
                    _next = last;
                    _count--;
                }
            }

            _pos[_next] = p;
            _rot[_next] = q;
            _time[_next] = t;
            _next = (_next + 1) % HistorySize;
            if (_count < HistorySize)
            {
                _count++;
            }

            int newest = (_next - 1 + HistorySize) % HistorySize;
            int oldest = newest;
            for (int k = 1; k < _count; k++)
            {
                int i = (newest - k + HistorySize) % HistorySize;
                if (t - _time[i] > HistoryWindow)
                {
                    break;
                }

                oldest = i;
            }

            float dt = _time[newest] - _time[oldest];
            if (oldest == newest || dt < 1e-4f)
            {
                Velocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
                return;
            }

            Velocity = (_pos[newest] - _pos[oldest]) / dt;
            Quaternion delta = _rot[newest] * Quaternion.Inverse(_rot[oldest]);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            bool finite = !(float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z) || float.IsInfinity(axis.x) || float.IsInfinity(axis.y) || float.IsInfinity(axis.z));
            AngularVelocity = finite && Mathf.Abs(angle) > 1e-4f ? axis * (angle * Mathf.Deg2Rad / dt) : Vector3.zero;
        }

        private void RaiseGripChanged()
        {
            var handler = GripChanged;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(this);
            }
            catch (Exception e)
            {
                RrLog.Error("Hand", "GripChanged handler failed", e);
            }
        }

        private string SideName
        {
            get { return Side == RrHandSide.Left ? "left" : "right"; }
        }

        internal static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.000") + ", " + v.y.ToString("0.000") + ", " + v.z.ToString("0.000") + ")";
        }

        private void OnDestroy()
        {
            if (Left == this)
            {
                Left = null;
            }

            if (Right == this)
            {
                Right = null;
            }

            GripChanged = null;
            if (Left == null && Right == null)
            {
                // Statics survive a world re-entry (ARCHITECTURE rule 5): the test mode never outlives the hands.
                TestVr = false;
                TestView = null;
                _loggedTest = false;
            }

            // The grip may live under the client's anchor: never leave our object on the client's body.
            if (Grip != null && Grip.parent != transform)
            {
                Destroy(Grip.gameObject);
            }
        }
    }
}
