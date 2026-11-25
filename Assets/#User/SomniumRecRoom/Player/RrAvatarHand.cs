using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>Where an <see cref="RrAvatarHand"/> pose came from.</summary>
    public enum RrAvatarHandSource
    {
        /// <summary>Nothing known: no avatar bones, no IK target, no tracked hand (the editor without the test arm, the desktop left hand).</summary>
        None,

        /// <summary>The local avatar's humanoid Animator bones (<c>Animator.GetBoneTransform</c>).</summary>
        HumanoidBones,

        /// <summary>Bones found by name under the local avatar (non-humanoid rigs, or humanoid Avatars without finger mappings).</summary>
        NamedBones,

        /// <summary>The client's VRIK hand target (<c>Left/RightHandVRIKPoint</c>) plus the client's own default offsets: no avatar bones found.</summary>
        IkTarget,

        /// <summary><see cref="RrHand.Grip"/> plus the client's default IK offsets (VR), or the screen-mode virtual hand: neither bones nor IK target.</summary>
        ControllerGuess
    }

    /// <summary>
    /// A read-only model of the local avatar's DRAWN hands: the wrist, the centre of the closed fist and Rec Room's hand
    /// frame on it, so held props (grab task) and the wrist watch (watch task) can sit on the hand the player sees rather
    /// than on the controller. Nothing is ever added to, parented under or changed on the Somnium avatar: its transforms
    /// are only read, cached, and re-found when they are destroyed.
    ///
    /// <b>Why it is needed.</b> <see cref="RrHand.Grip"/> is the controller's OpenXR grip pose (measured in the headset:
    /// the client's <c>LeftHandAnchor</c> IS the grip pose, offset 0 / 0 deg). The client does not draw the avatar's hand
    /// there: VRIK solves the arm toward <c>LeftHandVRIKPoint</c>, a child of <c>Stabilized</c> (an XRI
    /// XRTransformStabilizer that follows the anchor in Update with 5 deg / 5 cm smoothing), at the local pose
    /// <c>PlayerBodyHolder._ikPoints._leftHandPos</c> (-0.033, 0.068, -0.036) and a rotation built from
    /// <c>_leftHandRot</c>, <c>_leftHandDefaultDelta</c> and the avatar's bind pose (VRIKHandler.cs:125-148, 183-227). The
    /// VRIK arm then sets the hand bone to the target's position and rotation (IKSolverVRArm.cs:258-262, 458). Worked
    /// through (hands_frames.md section 2), the drawn wrist sits 8.4 cm behind the grip centre and the drawn hand is
    /// turned 26.7 deg from the grip frame: its fingers point 24 deg higher and 12 deg further out than the grip's
    /// "front of the fist".
    ///
    /// <b>Where the avatar is.</b> The client instantiates it at the root of MasterScene (AvatarSpawner.cs:31), not
    /// under the Bridge body. The model is renamed "Root" and sits under a MainRoot that the local player keeps on layer
    /// 21, with the head bone scaled to 1e-10 so it is not drawn in first person (MainRoot.cs:62-112). Its arms and hands
    /// ARE drawn in first person. The local player also has a MirrorRoot copy (layer 8, bones copied after the IK) and a
    /// TeleportRoot copy (layer 6, usually inactive). The humanoid Animator of the model whose head bone is within 1 m of
    /// the Bridge head anchor is used: layer 21 first, then 8, then the rest, nearest first (remote players' avatars are
    /// on layer 6). An avatar or outfit change destroys the model and instantiates a new one (BaseAvatarRoot.cs:130-158).
    /// The cached bones then read null and the search runs again. While nothing is found, it runs twice a second for
    /// the first 6 scans, then every 3 s.
    ///
    /// <b>Rig-independent frame.</b> Bone axis conventions differ per rig, so orientations come from bone POSITIONS:
    /// fingers = hand bone to middle proximal (else the mean of the other proximals, else the forearm); thumb side =
    /// index proximal minus little (or ring) proximal, projected square to the fingers (else the thumb proximal). That
    /// gives Rec Room's hand frame (<see cref="TryGetRecRoomHand"/>): +Z fingers, +Y thumb side, +X = Y x Z = out of the
    /// left palm and out of the back of the right hand. That is the frame Rec Room's own hand rig uses (the (Player)
    /// prefab's LeftHand: wrist at z -0.105, finger tip at z +0.106, thumb toward +y, fingers curling toward +x).
    /// Fallback chain: humanoid bones, then bones by name (mixamorig:LeftHand, LeftHand, hand_l, Left_Hand, L_Hand,
    /// Bip01 L Hand, J_Bip_L_Hand...), then the IK target with the client's constants, then the grip with the same
    /// constants.
    ///
    /// <b>When to read.</b> Every getter reads the CURRENT transforms; nothing is cached across calls. The client solves
    /// the IK in <c>IKComponentsOrder.LateUpdate</c> (default order 0, IKComponentsOrder.cs:90-119; VRIK itself is
    /// disabled, VRIKHandler.cs:96), and the mirror copies and finger poses also run at order 0. No avatar code has a
    /// later execution order, and none re-solves in <c>Application.onBeforeRender</c> or
    /// <c>RenderPipelineManager.beginCameraRendering</c> (grep of CLIENT/_SomniumSpace). So from our LateUpdate at 10000
    /// (<see cref="RrPlayerDriver"/>) until the frame is drawn, the bones are final and match what is on screen. Read
    /// there or in beginCameraRendering; both give the same pose. Only the anchors, and anything parented under them
    /// such as <see cref="RrHand.Grip"/>, are re-posed at BeforeRender. So a prop on the grip can lead the drawn hand
    /// by that last pose update plus the stabilizer's lag. A prop placed from these poses moves exactly with the hand.
    ///
    /// <b>Cost.</b> A query is about a dozen transform reads and a few normalisations, a few microseconds. The avatar
    /// search (one FindObjectsByType of Animator) runs only after <see cref="Refresh"/>, when the cached bones are
    /// destroyed, or (throttled) while none are found. Statics are cleared by <see cref="Reset"/>
    /// (RrPlayer.Shutdown).
    /// </summary>
    public static class RrAvatarHand
    {
        // ---- the client's constants (CLIENT/_SomniumSpace/Player/Prefabs/NEW/XR/Player.XR.Body.prefab:1812-1829) ----

        /// <summary><c>PlayerBodyHolder._ikPoints._leftHandPos</c>: the left VRIK target under <c>Stabilized</c> (IKPoints.SetToDefault).</summary>
        public static readonly Vector3 ClientLeftIkPosition = new Vector3(-0.033f, 0.068f, -0.036f);

        /// <summary><c>_rightHandPos</c>.</summary>
        public static readonly Vector3 ClientRightIkPosition = new Vector3(0.033f, 0.068f, -0.036f);

        private static readonly Quaternion ClientLeftHandRot = new Quaternion(0.04427278f, -0.106851f, -0.74875784f, -0.65267485f);
        private static readonly Quaternion ClientRightHandRot = new Quaternion(0.6526879f, -0.74874645f, 0.10685031f, 0.044274643f);
        private static readonly Quaternion ClientLeftHandDefaultDelta = new Quaternion(-0.032612942f, 0.1006975f, -0.17173481f, -0.97944045f);
        private static readonly Quaternion ClientRightHandDefaultDelta = new Quaternion(-0.97944033f, 0.17173517f, 0.10069749f, 0.032613374f);

        // ---- palm model --------------------------------------------------------------------------------------------

        /// <summary>
        /// Wrist to middle-knuckle length (m) that the palm coefficients are scaled to. With it, the client's default
        /// IK puts the palm exactly on the grip centre. The grip centre is 6.9 cm along the fingers from the drawn wrist,
        /// 4.7 cm toward the palm and 0.5 cm toward the thumb: 0.75 / 0.512 / 0.0576 of 0.092 m.
        /// </summary>
        public const float ReferenceHandLength = 0.092f;

        public const float PalmAlong = 0.75f;
        public const float PalmInward = 0.512f;
        public const float PalmThumbward = 0.0576f;

        /// <summary>Elbow to wrist (m) assumed without an elbow bone, times the rig scale.</summary>
        public const float DefaultForearmLength = 0.25f;

        /// <summary>Hand length / forearm length used when one of them is unknown (0.092 / 0.25).</summary>
        public const float HandToForearm = 0.368f;

        private const float MaxAvatarDistance = 1f;
        private const float ScanInterval = 0.5f;
        private const float SlowScanInterval = 3f;
        private const int FastScans = 6;
        private const float ValidateInterval = 1f;
        private const float IkRetryInterval = 2f;
        private const float CompareDelay = 2f;
        private const int MaxSourceLogs = 12;
        private const int LocalMainLayer = 21;
        private const int LocalMirrorLayer = 8;
        private const string MasterSceneName = "MasterScene";

        /// <summary>The drawn hand's Rec Room frame in the IK target's parent space (= the grip) at the default IK: _handRot * Inverse(_handDefaultDelta) * T-pose anatomy.</summary>
        private static readonly Quaternion LeftRecRoomInIk = RecRoomInIk(ClientLeftHandRot, ClientLeftHandDefaultDelta, true);
        private static readonly Quaternion RightRecRoomInIk = RecRoomInIk(ClientRightHandRot, ClientRightHandDefaultDelta, false);
        private static readonly Pose LeftPalmDefault = PalmDefault(true);
        private static readonly Pose RightPalmDefault = PalmDefault(false);

        private sealed class HandState
        {
            public readonly RrHandSide Side;
            public Transform Hand, LowerArm, Index, Middle, Ring, Little, Thumb;
            public bool Found;
            public bool Named;
            public Transform IkTarget;
            public Transform IkFor;
            public float IkRetry;
            public bool LoggedIk;
            public bool Compared;
            public RrAvatarHandSource Source;
            public int Logs;

            public HandState(RrHandSide side)
            {
                Side = side;
            }

            public string Name
            {
                get { return Side == RrHandSide.Left ? "left" : "right"; }
            }

            public void ClearBones()
            {
                Hand = LowerArm = Index = Middle = Ring = Little = Thumb = null;
                Found = false;
                Named = false;
            }

            public void ClearAll()
            {
                ClearBones();
                IkTarget = null;
                IkFor = null;
                IkRetry = 0f;
                LoggedIk = false;
                Compared = false;
                Source = RrAvatarHandSource.None;
                Logs = 0;
            }
        }

        private struct Frame
        {
            public Vector3 Wrist;
            public Quaternion WristRotation;
            public float Forearm;
            public Vector3 Palm;
            public Quaternion RecRoom;
        }

        private static readonly HandState LeftState = new HandState(RrHandSide.Left);
        private static readonly HandState RightState = new HandState(RrHandSide.Right);
        private static readonly List<Transform> Scratch = new List<Transform>(256);
        private static readonly Queue<Transform> Bfs = new Queue<Transform>(64);

        private static Transform _avatarRoot;
        private static Animator _animator;
        private static bool _humanoid;
        private static bool _hadAvatar;
        private static bool _dirty = true;
        private static float _nextScan;
        private static float _nextValidate;
        private static float _adoptedAt;
        private static Transform _loggedAvatar;
        private static int _avatarLogs;
        private static int _scans;
        private static int _failedScans;

        // ---- test override (RrAvatarHandTest only) ----
        internal static bool TestActive;
        internal static Transform TestRoot;
        internal static Transform TestLeftIk;
        internal static Transform TestRightIk;
        internal static bool TestNoControllerGuess;

        // ---- public API --------------------------------------------------------------------------------------------

        /// <summary>
        /// The drawn wrist joint (the hand bone's pivot). Rotation: +Z along the forearm (elbow to wrist, i.e. toward
        /// the fingers), +Y out of the back of the hand (made square to the forearm), +X = Y x Z: the thumb side on the
        /// left hand, the little-finger side on the right. <paramref name="forearmLength"/> is elbow to wrist in world
        /// metres, so it carries avatar and rig scale. Without an elbow bone it is 0.25 m times the rig scale.
        /// </summary>
        public static bool TryGetWrist(RrHandSide side, out Pose wrist, out float forearmLength)
        {
            Frame f;
            if (Solve(side, out f))
            {
                wrist = new Pose(f.Wrist, f.WristRotation);
                forearmLength = f.Forearm;
                return true;
            }

            wrist = Pose.identity;
            forearmLength = 0f;
            return false;
        }

        /// <summary>
        /// Centre of the drawn closed fist, with axes in <see cref="RrHand.Grip"/>'s convention (OpenXR grip). +Z runs
        /// through the fist from little finger to thumb, square to the fingers. -Y comes out of the front of the fist,
        /// along the fingers from wrist to middle knuckle. +X points right: out of the left palm, out of the back of the
        /// right hand. At the client's default IK this is <see cref="GripToPalmDefault"/> in grip space: the same
        /// centre, turned 26.7 deg because the hand holds the controller at an angle.
        /// <c>palm.rotation * RrHand.GripToAim</c> is Rec Room's hand frame (<see cref="TryGetRecRoomHand"/>).
        /// </summary>
        public static bool TryGetPalm(RrHandSide side, out Pose palm)
        {
            Frame f;
            if (Solve(side, out f))
            {
                palm = new Pose(f.Palm, f.RecRoom * Quaternion.Inverse(RrHand.GripToAim));
                return true;
            }

            palm = Pose.identity;
            return false;
        }

        /// <summary>
        /// Rec Room's hand frame on the drawn hand: at <see cref="TryGetPalm"/>'s position, rotation
        /// <c>palm.rotation * RrHand.GripToAim</c>: +Z fingers, +Y thumb side, +X out of the left palm / back of the
        /// right hand. Rec Room's <c>Tool.interactionSettings</c> poses (<c>FullBody{Left,Right}HandGripTransform</c>,
        /// <c>{Left,Right}HandPickupTransform</c>) are the TOOL's local pose in this frame, so a tool goes at
        /// <c>hand.position + hand.rotation * (scale * gp)</c> with rotation <c>hand.rotation * gr</c>
        /// (hands_frames.md section 4).
        /// </summary>
        public static bool TryGetRecRoomHand(RrHandSide side, out Pose hand)
        {
            Frame f;
            if (Solve(side, out f))
            {
                hand = new Pose(f.Palm, f.RecRoom);
                return true;
            }

            hand = Pose.identity;
            return false;
        }

        /// <summary>The live drawn palm (<see cref="TryGetPalm"/>) in <see cref="RrHand.Grip"/>'s local space (position via InverseTransformPoint).</summary>
        public static bool TryGetGripToPalm(RrHandSide side, out Pose gripToPalm)
        {
            RrHand hand = RrHand.Get(side);
            Transform grip = hand != null ? hand.Grip : null;
            Pose palm;
            if (grip == null || !TryGetPalm(side, out palm))
            {
                gripToPalm = Pose.identity;
                return false;
            }

            gripToPalm = new Pose(grip.InverseTransformPoint(palm.position), Quaternion.Inverse(grip.rotation) * palm.rotation);
            return true;
        }

        /// <summary>
        /// Where the client's DEFAULT IK draws the palm (<see cref="TryGetPalm"/> convention), in the grip's space at rig
        /// scale 1. Multiply the position by the anchor's lossy scale if needed; it is (0, 0, 0) to 0.1 mm anyway.
        /// Left rotation (-0.2098, -0.0142, -0.0957, 0.9729) = Euler(-24.3, 0.8, -11.4); the right one is mirrored.
        /// </summary>
        public static Pose GripToPalmDefault(RrHandSide side)
        {
            return side == RrHandSide.Left ? LeftPalmDefault : RightPalmDefault;
        }

        /// <summary>
        /// Rec Room's hand frame on the default-IK drawn hand, in the grip's space: <see cref="GripToPalmDefault"/>
        /// turned by <see cref="RrHand.GripToAim"/>. Left (0.5396, -0.0777, -0.0576, 0.8363) = GripToAim * Euler(-23.9,
        /// -12.1, 4.3); right (0.5396, 0.0777, 0.0577, 0.8363) = GripToAim * Euler(-23.9, 12.1, -4.3). For a prop parented
        /// under the grip: localRotation = q * gr, localPosition = p + q * (scale * gp).
        /// </summary>
        public static Pose GripToRecRoomDefault(RrHandSide side)
        {
            Pose p = GripToPalmDefault(side);
            return new Pose(p.position, p.rotation * RrHand.GripToAim);
        }

        /// <summary>What the last pose for this hand came from (evaluates the chain now).</summary>
        public static RrAvatarHandSource Source(RrHandSide side)
        {
            Frame f;
            Solve(side, out f);
            return State(side).Source;
        }

        /// <summary>Forgets the cached avatar bones and IK targets; the next query searches again. Cheap: nothing is searched here.</summary>
        public static void Refresh()
        {
            _dirty = true;
            LeftState.IkFor = null;
            RightState.IkFor = null;
        }

        /// <summary>Clears every static (world shutdown). The assembly outlives the world, so nothing may point at old objects.</summary>
        public static void Reset()
        {
            LeftState.ClearAll();
            RightState.ClearAll();
            Scratch.Clear();
            Bfs.Clear();
            _avatarRoot = null;
            _animator = null;
            _humanoid = false;
            _hadAvatar = false;
            _dirty = true;
            _nextScan = 0f;
            _nextValidate = 0f;
            _loggedAvatar = null;
            _avatarLogs = 0;
            _scans = 0;
            _failedScans = 0;
            TestActive = false;
            TestRoot = null;
            TestLeftIk = null;
            TestRightIk = null;
            TestNoControllerGuess = false;
        }

        /// <summary>One line for diagnostics.</summary>
        public static string Describe()
        {
            return "avatar " + (_avatarRoot != null ? "'" + _avatarRoot.name + "' (" + (_humanoid ? "humanoid" : "named bones") + ", layer " + _avatarRoot.gameObject.layer + ")" : "none") +
                   ", left " + Source(RrHandSide.Left) + ", right " + Source(RrHandSide.Right) + ", scans " + _scans + (TestActive ? ", TEST OVERRIDE" : "");
        }

        // ---- test override (internal: RrAvatarHandTest) ----------------------------------------------------------------

        /// <summary>Feeds a test avatar and/or IK targets instead of the scene search and the Bridge (the harness has no client).</summary>
        internal static void SetTestAvatar(Transform root, Transform leftIk, Transform rightIk, bool allowControllerGuess)
        {
            TestActive = true;
            TestRoot = root;
            TestLeftIk = leftIk;
            TestRightIk = rightIk;
            TestNoControllerGuess = !allowControllerGuess;
            Refresh();
        }

        internal static void ClearTestAvatar()
        {
            TestActive = false;
            TestRoot = null;
            TestLeftIk = null;
            TestRightIk = null;
            TestNoControllerGuess = false;
            Refresh();
        }

        /// <summary>The drawn Rec Room frame in the IK target parent's (= grip) space at the default IK, for tests.</summary>
        internal static Quaternion RecRoomInIkDefault(RrHandSide side)
        {
            return side == RrHandSide.Left ? LeftRecRoomInIk : RightRecRoomInIk;
        }

        internal static Vector3 ClientIkPosition(RrHandSide side)
        {
            return side == RrHandSide.Left ? ClientLeftIkPosition : ClientRightIkPosition;
        }

        /// <summary><c>_leftHandRot</c> / <c>_rightHandRot</c> (tests: re-creating VRIK's target rotation on a real rig).</summary>
        internal static Quaternion ClientHandRotation(RrHandSide side)
        {
            return Quaternion.Normalize(side == RrHandSide.Left ? ClientLeftHandRot : ClientRightHandRot);
        }

        /// <summary><c>_leftHandDefaultDelta</c> / <c>_rightHandDefaultDelta</c>.</summary>
        internal static Quaternion ClientHandDefaultDelta(RrHandSide side)
        {
            return Quaternion.Normalize(side == RrHandSide.Left ? ClientLeftHandDefaultDelta : ClientRightHandDefaultDelta);
        }

        // ---- solving -----------------------------------------------------------------------------------------------

        private static HandState State(RrHandSide side)
        {
            return side == RrHandSide.Left ? LeftState : RightState;
        }

        private static bool Solve(RrHandSide side, out Frame f)
        {
            HandState s = State(side);
            f = default(Frame);
            RrAvatarHandSource source = RrAvatarHandSource.None;
            try
            {
                EnsureAvatar();
                if (s.Found && s.Hand != null && FromBones(s, out f))
                {
                    source = s.Named ? RrAvatarHandSource.NamedBones : RrAvatarHandSource.HumanoidBones;
                    if (!s.Compared && Time.unscaledTime - _adoptedAt >= CompareDelay && (TestActive || RrPlayer.InVr))
                    {
                        CompareWithIk(s, f);
                    }
                }
                else if (FindIkTarget(s) && FromIkTarget(s, out f))
                {
                    source = RrAvatarHandSource.IkTarget;
                }
                else if (FromController(s, out f))
                {
                    source = RrAvatarHandSource.ControllerGuess;
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Hands", "solve-" + s.Name, s.Name + " drawn hand: reading the pose threw " + e.GetType().Name + ": " + e.Message + " (logged once)");
                source = RrAvatarHandSource.None;
            }

            SetSource(s, source);
            return source != RrAvatarHandSource.None;
        }

        /// <summary>
        /// Once per avatar and hand, in VR (or under the test override): how far the drawn bones are from what the
        /// client's constants predict (the IK target's position; the Rec Room frame from <c>Stabilized</c> times the
        /// constant), and where the drawn palm sits in the grip. One log line, so the analysis in hands_frames.md is
        /// checked against the real avatar in the headset.
        /// </summary>
        private static void CompareWithIk(HandState s, Frame bones)
        {
            if (!FindIkTarget(s))
            {
                return;
            }

            s.Compared = true;
            Frame ik;
            if (!FromIkTarget(s, out ik))
            {
                return;
            }

            string grip = "";
            RrHand hand = RrHand.Get(s.Side);
            if (hand != null && hand.Grip != null && hand.Tracked && !hand.IsScreenHand)
            {
                Transform g = hand.Grip;
                Vector3 local = g.InverseTransformPoint(bones.Palm);
                Quaternion rot = Quaternion.Inverse(g.rotation) * bones.RecRoom * Quaternion.Inverse(RrHand.GripToAim);
                Pose def = GripToPalmDefault(s.Side);
                grip = "; drawn palm in the grip " + RrHand.Fmt(local) + ", " + Quaternion.Angle(rot, def.rotation).ToString("0.0") +
                       " deg from GripToPalmDefault, " + Quaternion.Angle(rot, Quaternion.identity).ToString("0.0") + " deg from the grip";
            }

            RrLog.Info("Hands", s.Name + " drawn hand vs the client's IK constants: wrist " + ((bones.Wrist - ik.Wrist).magnitude * 100f).ToString("0.0") +
                                " cm from '" + s.IkTarget.name + "', Rec Room frame " + Quaternion.Angle(bones.RecRoom, ik.RecRoom).ToString("0.0") +
                                " deg from Stabilized * constant, forearm " + bones.Forearm.ToString("0.000") + " m" + grip);
        }

        private static bool FromBones(HandState s, out Frame f)
        {
            f = default(Frame);
            Vector3 h = s.Hand.position;
            bool elbow = s.LowerArm != null;
            Vector3 e = elbow ? s.LowerArm.position : h;
            float forearm = elbow ? Vector3.Distance(h, e) : 0f;

            Vector3 knuckle;
            bool haveKnuckle = true;
            if (s.Middle != null)
            {
                knuckle = s.Middle.position;
            }
            else
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                Accumulate(s.Index, ref sum, ref n);
                Accumulate(s.Ring, ref sum, ref n);
                Accumulate(s.Little, ref sum, ref n);
                haveKnuckle = n > 0;
                knuckle = n > 0 ? sum / n : h;
            }

            Vector3 toKnuckle = knuckle - h;
            float handLength = toKnuckle.magnitude;
            Vector3 fingers;
            if (haveKnuckle && handLength > 0.005f)
            {
                fingers = toKnuckle / handLength;
            }
            else if (forearm > 0.01f)
            {
                fingers = (h - e) / forearm;
                handLength = 0f;
            }
            else
            {
                return false;
            }

            Vector3 radial = Vector3.zero;
            Transform ulnar = s.Little != null ? s.Little : s.Ring;
            if (s.Index != null && ulnar != null)
            {
                radial = s.Index.position - ulnar.position;
            }
            else if (s.Thumb != null)
            {
                radial = s.Thumb.position - h;
            }

            radial = Vector3.ProjectOnPlane(radial, fingers);
            Quaternion rr;
            if (radial.sqrMagnitude > 1e-8f)
            {
                rr = Quaternion.LookRotation(fingers, radial);
            }
            else if (!FallbackOrientation(s, fingers, out rr))
            {
                return false;
            }

            if (forearm <= 0.01f)
            {
                forearm = handLength > 0.005f ? handLength / HandToForearm : DefaultForearmLength * RigScale(s.Side);
            }

            if (handLength <= 0.005f)
            {
                handLength = forearm * HandToForearm;
            }

            Build(s.Side, h, elbow ? h - e : fingers, rr, handLength, forearm, out f);
            return true;
        }

        /// <summary>No finger bones to orient the hand: the client's own IK frame (VR) or the grip's, keeping the bone's finger direction.</summary>
        private static bool FallbackOrientation(HandState s, Vector3 fingers, out Quaternion rr)
        {
            Quaternion frame;
            if (FindIkTarget(s) && s.IkTarget.parent != null)
            {
                frame = s.IkTarget.parent.rotation * RecRoomInIkDefault(s.Side);
            }
            else
            {
                RrHand hand = RrHand.Get(s.Side);
                if (hand == null || !hand.Tracked || hand.Grip == null)
                {
                    rr = Quaternion.identity;
                    return false;
                }

                frame = hand.Grip.rotation * RecRoomInIkDefault(s.Side);
            }

            Vector3 up = Vector3.ProjectOnPlane(frame * Vector3.up, fingers);
            rr = up.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(fingers, up) : frame;
            return true;
        }

        private static bool FromIkTarget(HandState s, out Frame f)
        {
            f = default(Frame);
            Transform t = s.IkTarget;
            Transform parent = t.parent;
            if (parent == null)
            {
                return false;
            }

            Quaternion rr = parent.rotation * RecRoomInIkDefault(s.Side);
            float scale = Mathf.Max(0.05f, parent.lossyScale.x);
            Build(s.Side, t.position, rr * Vector3.forward, rr, ReferenceHandLength * scale, DefaultForearmLength * scale, out f);
            return true;
        }

        private static bool FromController(HandState s, out Frame f)
        {
            f = default(Frame);
            if (TestActive && TestNoControllerGuess)
            {
                return false;
            }

            RrHand hand = RrHand.Get(s.Side);
            if (hand == null || !hand.Tracked || hand.Grip == null)
            {
                return false;
            }

            Transform grip = hand.Grip;
            if (hand.IsScreenHand)
            {
                // Our own virtual hand, not a drawn one: Rec Room's frame is the view (aim), the palm is the grip centre.
                Quaternion view = grip.rotation * RrHand.GripToAim;
                BuildFromPalm(s.Side, grip.position, view, ReferenceHandLength, DefaultForearmLength, out f);
                return true;
            }

            float scale = hand.Anchor != null ? Mathf.Max(0.05f, hand.Anchor.lossyScale.x) : 1f;
            Quaternion rr = grip.rotation * RecRoomInIkDefault(s.Side);
            Vector3 wrist = grip.position + grip.rotation * (ClientIkPosition(s.Side) * scale);
            Build(s.Side, wrist, rr * Vector3.forward, rr, ReferenceHandLength * scale, DefaultForearmLength * scale, out f);
            return true;
        }

        /// <summary>Wrist, forearm direction and Rec Room frame to the full frame (wrist rotation and palm centre).</summary>
        private static void Build(RrHandSide side, Vector3 wrist, Vector3 forearmDir, Quaternion rr, float handLength, float forearm, out Frame f)
        {
            Vector3 x = rr * Vector3.right;
            Vector3 thumb = rr * Vector3.up;
            Vector3 fingers = rr * Vector3.forward;
            Vector3 palmOut = side == RrHandSide.Left ? x : -x;
            Vector3 z = forearmDir.sqrMagnitude > 1e-8f ? forearmDir.normalized : fingers;
            if (Vector3.Cross(z, palmOut).sqrMagnitude < 1e-4f)
            {
                z = fingers;
            }

            f.Wrist = wrist;
            f.WristRotation = Quaternion.LookRotation(z, -palmOut);
            f.Forearm = forearm;
            f.Palm = wrist + fingers * (PalmAlong * handLength) + palmOut * (PalmInward * handLength) + thumb * (PalmThumbward * handLength);
            f.RecRoom = rr;
        }

        private static void BuildFromPalm(RrHandSide side, Vector3 palm, Quaternion rr, float handLength, float forearm, out Frame f)
        {
            Vector3 x = rr * Vector3.right;
            Vector3 palmOut = side == RrHandSide.Left ? x : -x;
            Vector3 wrist = palm - (rr * Vector3.forward) * (PalmAlong * handLength) - palmOut * (PalmInward * handLength) - (rr * Vector3.up) * (PalmThumbward * handLength);
            Build(side, wrist, rr * Vector3.forward, rr, handLength, forearm, out f);
        }

        private static void Accumulate(Transform t, ref Vector3 sum, ref int n)
        {
            if (t != null)
            {
                sum += t.position;
                n++;
            }
        }

        private static float RigScale(RrHandSide side)
        {
            RrHand hand = RrHand.Get(side);
            if (hand != null && hand.Anchor != null)
            {
                return Mathf.Max(0.05f, hand.Anchor.lossyScale.x);
            }

            return 1f;
        }

        private static void SetSource(HandState s, RrAvatarHandSource source)
        {
            if (source == s.Source)
            {
                return;
            }

            s.Source = source;
            if (s.Logs >= MaxSourceLogs)
            {
                RrLog.WarnOnce("Hands", "flap-" + s.Name, s.Name + " drawn hand: the source keeps changing (" + MaxSourceLogs + " changes logged); now " + source + " (logged once)");
                return;
            }

            s.Logs++;
            RrLog.Info("Hands", s.Name + " drawn hand now from " + DescribeSource(s));
        }

        private static string DescribeSource(HandState s)
        {
            switch (s.Source)
            {
                case RrAvatarHandSource.HumanoidBones:
                case RrAvatarHandSource.NamedBones:
                {
                    float forearm = s.LowerArm != null ? Vector3.Distance(s.Hand.position, s.LowerArm.position) : 0f;
                    float hand = s.Middle != null ? Vector3.Distance(s.Hand.position, s.Middle.position) : 0f;
                    return (s.Source == RrAvatarHandSource.HumanoidBones ? "humanoid bones" : "bones found by name") + " of '" + (_avatarRoot != null ? _avatarRoot.name : "?") +
                           "': hand '" + s.Hand.name + "', forearm " + (s.LowerArm != null ? forearm.ToString("0.000") + " m" : "none") +
                           ", wrist->middle knuckle " + (s.Middle != null ? hand.ToString("0.000") + " m" : "none") +
                           ", proximals " + Flag(s.Index, "i") + Flag(s.Middle, "m") + Flag(s.Ring, "r") + Flag(s.Little, "l") + Flag(s.Thumb, "t");
                }

                case RrAvatarHandSource.IkTarget:
                    return "the client's IK target '" + (s.IkTarget != null ? s.IkTarget.name : "?") + "' under '" +
                           (s.IkTarget != null && s.IkTarget.parent != null ? s.IkTarget.parent.name : "?") + "' with the client's default offsets (no avatar bones)";
                case RrAvatarHandSource.ControllerGuess:
                    RrHand h = RrHand.Get(s.Side);
                    return h != null && h.IsScreenHand
                        ? "the screen-mode virtual hand (no drawn avatar hand)"
                        : "the controller grip plus the client's default IK offsets (wrist " + RrHand.Fmt(ClientIkPosition(s.Side)) + " in the grip; no avatar bones, no IK target)";
                default:
                    return "nothing (no avatar bones, no IK target, hand not tracked)";
            }
        }

        private static string Flag(Transform t, string letter)
        {
            return t != null ? letter : "-";
        }

        // ---- finding the local avatar ------------------------------------------------------------------------------

        private static void EnsureAvatar()
        {
            float now = Time.unscaledTime;
            bool need = _dirty;
            if (!need)
            {
                if (_avatarRoot == null)
                {
                    // Destroyed (avatar change): look again at once, then at the throttled rate.
                    need = _hadAvatar || now >= _nextScan;
                }
                else if (!_avatarRoot.gameObject.activeInHierarchy || (LeftState.Found && LeftState.Hand == null) || (RightState.Found && RightState.Hand == null))
                {
                    need = true;
                }
                else if (now >= _nextValidate)
                {
                    _nextValidate = now + ValidateInterval;
                    need = !StillLocal();
                }
            }

            if (need)
            {
                Scan(now);
            }
        }

        private static bool StillLocal()
        {
            if (TestActive)
            {
                return _avatarRoot == TestRoot;
            }

            Transform head = RrPlayer.HasClient ? RrPlayer.HeadAnchor : null;
            if (head == null)
            {
                return false;
            }

            Transform bone = _humanoid && _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (bone == null)
            {
                bone = LeftState.Hand != null ? LeftState.Hand : RightState.Hand;
            }

            return bone != null && (bone.position - head.position).magnitude <= MaxDistance(head) * 1.5f;
        }

        private static float MaxDistance(Transform head)
        {
            Transform root = RrPlayer.Root;
            float scale = root != null ? Mathf.Max(1f, root.lossyScale.y) : 1f;
            return MaxAvatarDistance * scale;
        }

        private static void Scan(float now)
        {
            _dirty = false;
            _scans++;
            // While nothing is found (avatar loading, blocked or hidden), scan twice a second at first, then every 3 s.
            _nextScan = now + (_failedScans < FastScans ? ScanInterval : SlowScanInterval);
            _nextValidate = now + ValidateInterval;
            _avatarRoot = null;
            _animator = null;
            _humanoid = false;
            LeftState.ClearBones();
            RightState.ClearBones();
            LeftState.Compared = RightState.Compared = false;
            _hadAvatar = false;
            if (TestActive)
            {
                if (TestRoot != null)
                {
                    Adopt(TestRoot, TestRoot.GetComponentInChildren<Animator>(true), "test override");
                }

                return;
            }

            if (!RrPlayer.HasClient)
            {
                return;
            }

            Transform head = RrPlayer.HeadAnchor;
            if (head == null)
            {
                return;
            }

            float max = MaxDistance(head);
            Animator[] all = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Animator best = null;
            float bestScore = float.MinValue;
            float bestDistance = 0f;
            int candidates = 0;
            foreach (Animator a in all)
            {
                if (a == null || !a.gameObject.activeInHierarchy)
                {
                    continue;
                }

                int layer = a.gameObject.layer;
                Transform headBone = null;
                if (IsHumanoid(a))
                {
                    headBone = a.GetBoneTransform(HumanBodyBones.Head);
                }
                else if (layer == LocalMainLayer || layer == LocalMirrorLayer)
                {
                    headBone = FindNamedHead(a.transform);
                }

                if (headBone == null)
                {
                    continue;
                }

                float d = (headBone.position - head.position).magnitude;
                if (d > max)
                {
                    continue;
                }

                candidates++;
                float score = (layer == LocalMainLayer ? 3f : layer == LocalMirrorLayer ? 2f : 0f) +
                              (a.gameObject.scene.name == MasterSceneName ? 1f : 0f) - d;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = a;
                    bestDistance = d;
                }
            }

            if (best != null)
            {
                Adopt(best.transform, best, candidates + " candidate(s) within " + max.ToString("0.0") + " m of the head anchor; picked layer " + best.gameObject.layer +
                                            ", scene " + best.gameObject.scene.name + ", head bone " + bestDistance.ToString("0.00") + " m from the head anchor");
                _failedScans = 0;
            }
            else
            {
                _failedScans++;
                if (_failedScans == FastScans)
                {
                    RrLog.Info("Hands", "no local avatar model found in " + FastScans + " scans (" + all.Length + " Animators in the scene, none humanoid within " +
                                        max.ToString("0.0") + " m of the head anchor): drawn hands fall back to the IK target / controller; scanning every " +
                                        SlowScanInterval.ToString("0") + " s");
                }
            }
        }

        private static bool IsHumanoid(Animator a)
        {
            Avatar av = a != null ? a.avatar : null;
            return av != null && av.isValid && av.isHuman;
        }

        private static void Adopt(Transform root, Animator animator, string how)
        {
            _avatarRoot = root;
            _animator = animator;
            _humanoid = IsHumanoid(animator);
            _adoptedAt = Time.unscaledTime;
            _hadAvatar = true;
            FillBones(LeftState);
            FillBones(RightState);
            if (root != _loggedAvatar && _avatarLogs < 8)
            {
                _loggedAvatar = root;
                _avatarLogs++;
                RrLog.Info("Hands", "local avatar '" + root.name + "'" + (root.parent != null ? " under '" + root.parent.name + "'" : "") + ": " +
                                    (_humanoid ? "humanoid Animator" : animator != null ? "generic Animator (bones by name)" : "no Animator (bones by name)") +
                                    ", left hand " + (LeftState.Found ? "'" + LeftState.Hand.name + "'" : "not found") +
                                    ", right hand " + (RightState.Found ? "'" + RightState.Hand.name + "'" : "not found") + " (" + how + ")");
            }
        }

        private static void FillBones(HandState s)
        {
            s.ClearBones();
            bool left = s.Side == RrHandSide.Left;
            if (_humanoid)
            {
                Animator a = _animator;
                s.Hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                s.LowerArm = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                s.Index = a.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
                s.Middle = a.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                s.Ring = a.GetBoneTransform(left ? HumanBodyBones.LeftRingProximal : HumanBodyBones.RightRingProximal);
                s.Little = a.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
                s.Thumb = a.GetBoneTransform(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
                if (s.Hand != null && s.Index == null && s.Middle == null && s.Little == null)
                {
                    // The Avatar maps no fingers: the bones may still be there under their own names.
                    FindFingers(s);
                }
            }

            if (s.Hand == null && _avatarRoot != null)
            {
                s.Hand = FindNamedHand(_avatarRoot, left);
                if (s.Hand != null)
                {
                    s.Named = true;
                    s.LowerArm = FindLowerArm(s.Hand);
                    FindFingers(s);
                }
            }

            s.Found = s.Hand != null;
        }

        // ---- name search (non-humanoid rigs) ------------------------------------------------------------------------

        /// <summary>
        /// Lower-cased, rig prefixes (mixamorig:, Bip01, J_Bip_, DEF-, CC_Base_) and separators removed:
        /// "mixamorig:LeftHand" -> "lefthand", "hand_l" -> "handl", "Bip01 L Hand" -> "lhand", "J_Bip_L_Hand" -> "lhand".
        /// </summary>
        internal static string Canon(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            string n = name.ToLowerInvariant();
            int colon = n.LastIndexOf(':');
            if (colon >= 0 && colon < n.Length - 1)
            {
                n = n.Substring(colon + 1);
            }

            var sb = new System.Text.StringBuilder(n.Length);
            foreach (char c in n)
            {
                if (c != ' ' && c != '_' && c != '.' && c != '-' && c != '|')
                {
                    sb.Append(c);
                }
            }

            n = sb.ToString();
            n = StripPrefix(n, "mixamorig");
            n = StripPrefix(n, "bip001");
            n = StripPrefix(n, "bip01");
            n = StripPrefix(n, "jbip");
            n = StripPrefix(n, "ccbase");
            n = StripPrefix(n, "def");
            return n;
        }

        private static string StripPrefix(string n, string prefix)
        {
            return n.Length > prefix.Length && n.StartsWith(prefix, StringComparison.Ordinal) ? n.Substring(prefix.Length) : n;
        }

        /// <summary>The part of a canonical name once its side is removed, or null when it names the other side / no side.</summary>
        internal static string PartFor(string canon, bool left)
        {
            string word = left ? "left" : "right";
            string other = left ? "right" : "left";
            if (canon.Contains(other))
            {
                return null;
            }

            int at = canon.IndexOf(word, StringComparison.Ordinal);
            if (at >= 0)
            {
                return canon.Remove(at, word.Length);
            }

            char letter = left ? 'l' : 'r';
            if (canon.Length > 1 && canon[0] == letter)
            {
                string rest = canon.Substring(1);
                if (IsArmPart(rest))
                {
                    return rest;
                }
            }

            if (canon.Length > 1 && canon[canon.Length - 1] == letter)
            {
                string rest = canon.Substring(0, canon.Length - 1);
                if (IsArmPart(rest))
                {
                    return rest;
                }
            }

            return null;
        }

        private static bool IsArmPart(string part)
        {
            return part == "hand" || part == "wrist" || part == "forearm" || part == "lowerarm" || part == "elbow";
        }

        private static Transform FindNamedHand(Transform root, bool left)
        {
            Scratch.Clear();
            root.GetComponentsInChildren(true, Scratch);
            Transform wrist = null;
            foreach (Transform t in Scratch)
            {
                string part = PartFor(Canon(t.name), left);
                if (part == "hand")
                {
                    Scratch.Clear();
                    return t;
                }

                if (part == "wrist" && wrist == null)
                {
                    wrist = t;
                }
            }

            Scratch.Clear();
            return wrist;
        }

        private static Transform FindNamedHead(Transform root)
        {
            Scratch.Clear();
            root.GetComponentsInChildren(true, Scratch);
            Transform found = null;
            foreach (Transform t in Scratch)
            {
                if (Canon(t.name) == "head")
                {
                    found = t;
                    break;
                }
            }

            Scratch.Clear();
            return found;
        }

        /// <summary>The elbow: up the hand's parents, skipping twist/roll bones that sit next to the wrist.</summary>
        private static Transform FindLowerArm(Transform hand)
        {
            Transform p = hand.parent;
            Transform firstFar = null;
            for (int i = 0; i < 4 && p != null; i++, p = p.parent)
            {
                string c = Canon(p.name);
                bool twist = c.Contains("twist") || c.Contains("roll");
                if (!twist && (c.Contains("forearm") || c.Contains("lowerarm") || c.Contains("elbow")))
                {
                    return p;
                }

                if (!twist && firstFar == null && (p.position - hand.position).magnitude > 0.1f)
                {
                    firstFar = p;
                }
            }

            return firstFar != null ? firstFar : hand.parent;
        }

        /// <summary>Finger proximals: breadth-first under the hand (3 levels), first bone per finger word, metacarpals skipped.</summary>
        private static void FindFingers(HandState s)
        {
            Bfs.Clear();
            Bfs.Enqueue(s.Hand);
            int guard = 0;
            while (Bfs.Count > 0 && guard++ < 200)
            {
                Transform t = Bfs.Dequeue();
                if (t != s.Hand)
                {
                    string c = Canon(t.name);
                    if (!c.Contains("meta") && !c.Contains("carpal"))
                    {
                        if (s.Thumb == null && (c.Contains("thumb") || c.EndsWith("finger0", StringComparison.Ordinal)))
                        {
                            s.Thumb = t;
                        }
                        else if (s.Index == null && (c.Contains("index") || c.EndsWith("finger1", StringComparison.Ordinal)))
                        {
                            s.Index = t;
                        }
                        else if (s.Middle == null && (c.Contains("middle") || c.EndsWith("finger2", StringComparison.Ordinal)))
                        {
                            s.Middle = t;
                        }
                        else if (s.Ring == null && (c.Contains("ring") || c.EndsWith("finger3", StringComparison.Ordinal)))
                        {
                            s.Ring = t;
                        }
                        else if (s.Little == null && (c.Contains("pinky") || c.Contains("little") || c.EndsWith("finger4", StringComparison.Ordinal)))
                        {
                            s.Little = t;
                        }
                    }
                }

                if (Depth(t, s.Hand) < 3)
                {
                    for (int i = 0; i < t.childCount; i++)
                    {
                        Bfs.Enqueue(t.GetChild(i));
                    }
                }
            }

            Bfs.Clear();
        }

        private static int Depth(Transform t, Transform root)
        {
            int d = 0;
            while (t != null && t != root && d < 8)
            {
                t = t.parent;
                d++;
            }

            return d;
        }

        // ---- the client's IK target ----------------------------------------------------------------------------------

        private static bool FindIkTarget(HandState s)
        {
            if (TestActive)
            {
                s.IkTarget = s.Side == RrHandSide.Left ? TestLeftIk : TestRightIk;
                return s.IkTarget != null;
            }

            Transform anchor = RrPlayer.BridgeHand(s.Side);
            if (anchor == null)
            {
                s.IkTarget = null;
                s.IkFor = null;
                return false;
            }

            if (s.IkFor == anchor)
            {
                if (s.IkTarget != null)
                {
                    return true;
                }

                if (Time.unscaledTime < s.IkRetry)
                {
                    return false;
                }
            }

            string name = s.Side == RrHandSide.Left ? "LeftHandVRIKPoint" : "RightHandVRIKPoint";
            Transform found = FindChild(anchor.parent, name, 3);
            if (found == null)
            {
                found = FindChild(anchor, name, 2);
            }

            s.IkFor = anchor;
            s.IkTarget = found;
            s.IkRetry = Time.unscaledTime + IkRetryInterval;
            if (found != null && !s.LoggedIk)
            {
                s.LoggedIk = true;
                RrLog.Info("Hands", s.Name + " IK target '" + found.name + "' under '" + (found.parent != null ? found.parent.name : "?") + "': local " +
                                    RrHand.Fmt(found.localPosition) + " (client default " + RrHand.Fmt(ClientIkPosition(s.Side)) + ")");
            }

            return found != null;
        }

        private static Transform FindChild(Transform root, string name, int depth)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.name == name)
                {
                    return c;
                }
            }

            if (depth <= 1)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name, depth - 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ---- constants -----------------------------------------------------------------------------------------------

        /// <summary>
        /// The drawn hand's Rec Room frame under the IK target's parent. VRIK sets the hand bone to the target rotation
        /// _handRot * Inverse(_handDefaultDelta) * B, where B is the hand's bind rotation (VRIKHandler.CalculateTargetDelta).
        /// So the T-pose anatomy (left: fingers -X, thumb +Z, palm out -Y; right: fingers +X) is turned by
        /// _handRot * Inverse(_handDefaultDelta) whatever the rig's bone axes are.
        /// </summary>
        private static Quaternion RecRoomInIk(Quaternion handRot, Quaternion defaultDelta, bool left)
        {
            Quaternion anatomy = Quaternion.LookRotation(left ? Vector3.left : Vector3.right, Vector3.forward);
            return Quaternion.Normalize(Quaternion.Normalize(handRot) * Quaternion.Inverse(Quaternion.Normalize(defaultDelta)) * anatomy);
        }

        private static Pose PalmDefault(bool left)
        {
            Quaternion rr = RecRoomInIk(left ? ClientLeftHandRot : ClientRightHandRot, left ? ClientLeftHandDefaultDelta : ClientRightHandDefaultDelta, left);
            Vector3 x = rr * Vector3.right;
            Vector3 palmOut = left ? x : -x;
            Vector3 wrist = left ? ClientLeftIkPosition : ClientRightIkPosition;
            Vector3 palm = wrist + (rr * Vector3.forward) * (PalmAlong * ReferenceHandLength) + palmOut * (PalmInward * ReferenceHandLength) +
                           (rr * Vector3.up) * (PalmThumbward * ReferenceHandLength);
            return new Pose(palm, rr * Quaternion.Inverse(Quaternion.Euler(90f, 0f, 0f)));
        }
    }
}
