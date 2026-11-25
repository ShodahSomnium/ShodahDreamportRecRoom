using System;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>Where the wrist watch's pose came from this frame (<see cref="RrWatchModel.Source"/>).</summary>
    public enum RrWatchSource
    {
        /// <summary>Not shown: not in a headset, or the wrist's hand is not tracked.</summary>
        None,

        /// <summary><see cref="RrWatchModel.TestPose"/> (harness photos of the watch alone).</summary>
        TestPose,

        /// <summary>The DRAWN wrist: the local avatar's bones (<see cref="RrAvatarHand"/> HumanoidBones or NamedBones).</summary>
        AvatarBones,

        /// <summary>No avatar bones: the client's VRIK hand target plus the client's default IK offsets.</summary>
        IkTarget,

        /// <summary>Neither: <see cref="RrHand.Grip"/> plus the client's default IK offsets (RrAvatarHand's ControllerGuess).</summary>
        GripClientIk,

        /// <summary>RrAvatarHand gave nothing although the grip is tracked: the grip with the first build's offset.</summary>
        GripFirstBuild
    }

    /// <summary>
    /// The physical watch on the player's wrist (rr_watch_ui.md §2.1, §3.5) and the "look at your watch" gesture.
    ///
    /// <b>Model.</b> Rec Room's <c>Wrist_Watch</c> mesh (1163 vertices, 108 x 45 x 72 mm, the face 4 cm above its
    /// origin, which is the wrist's axis) with its own <c>Watch_Wrist_mat</c>, plus the clock of the prefab: a
    /// <c>ClockFace</c> node at (0, -0.001, 0.04) turned -90 deg about Z holding three thin cubes (hour 3x13x3 mm,
    /// minute 2x18x2 mm, second 1x22x1 mm) in <c>Watch_Wrist_Hand_mat</c>, turning clockwise on local time. When
    /// the mesh is missing a plain dark case and strap stand in. It is our own object, never a child of the avatar.
    ///
    /// <b>Pose: on the avatar's DRAWN wrist.</b> The first build hung the watch on <see cref="RrHand.Grip"/> (the
    /// controller's OpenXR grip pose) at (-0.015, 0, -0.1) with Rec Room's anchor rotation. Both were wrong for the
    /// arm the client draws. The rotation is written in Rec Room's hand frame, whose axes are the grip's turned 90 deg
    /// about X (<see cref="RrHand.GripToAim"/>), so the strap ran along the fingers and 3 o'clock pointed up the
    /// controller handle. The offset is 10 cm down the handle axis, while the client's default IK draws the wrist
    /// joint at (-0.033, 0.068, -0.036) in the grip (hands_frames.md 2.3): measured on the harness's default-IK arm, the
    /// first build's watch sat ~9 cm off the forearm (the user's "floating off the avatar"). Now the pose is built
    /// on <see cref="RrAvatarHand.TryGetWrist"/>, the wrist joint the player sees (+Z along the forearm, +Y out of the
    /// back of the hand), as Rec Room does on its own hand: the watch origin on the forearm axis
    /// <see cref="BackDistance"/> behind the wrist joint (Rec Room: 1.4 cm on its floating hand, 1.8 cm on its
    /// full-body avatar), lifted by <see cref="Lift"/>, the face (+Z) out of the back of the wrist, 3 o'clock (+Y)
    /// toward the fingers on the left wrist and toward the elbow on the right, 12 o'clock (+X, the strap axis) on the
    /// little-finger side of both, so the strap wraps round the forearm (<see cref="LeftInWrist"/>). The mesh is sized
    /// by <see cref="MeshScale"/> times the avatar's forearm length over 0.25 m (clamped to
    /// <see cref="MinSize"/>..<see cref="MaxSize"/>), so it hugs the drawn forearm instead of floating round it.
    /// Sleeves: once per avatar, outfit or wrist change, <see cref="RrWatchFit"/> reads the avatar's readable skinned
    /// meshes round the drawn wrist and lifts (or lowers) the watch so the case sits 4 mm above whatever is there:
    /// skin, a cuff or a sweater. Without readable meshes the default <see cref="Lift"/> stays.
    /// Fallbacks, logged when they change: the client's IK target, then the grip with the client's IK offsets
    /// (both inside RrAvatarHand), then the grip with the first build's offset.
    ///
    /// <b>When.</b> The client solves the arm IK in LateUpdate at execution order 0 (IKComponentsOrder.cs:90-119).
    /// This component runs at 10010 (after <see cref="RrPlayerDriver"/>, before RrGrab), so the gesture reads the
    /// solved wrist; at order 0 it could have read the pre-IK animator pose. The pose is written again in
    /// <c>RenderPipelineManager.beginCameraRendering</c>, where the grip-based fallbacks have their late-latched pose.
    /// Shown in VR only (the screen-mode left hand does not exist).
    ///
    /// <b>Gesture</b> (WatchGazeTracker #1859998750955597507): the menu opens after the face has been within 60 deg
    /// of the view centre, with the wrist pitched at most 45 deg and rolled at most 30 deg away from facing the eyes,
    /// for 0.4 s; a dwell ring fills over the face meanwhile. It closes when the face leaves 60 deg / pitch 50 deg /
    /// roll 50 deg, unless the menu was detached. Pitch = the eye's tilt along the forearm, roll = across the wrist,
    /// both measured in Rec Room's <c>WatchFaceTransform</c> frame (<see cref="FaceView"/>: the ideal eye is 10 deg
    /// off the face normal toward 6 o'clock, the thumb side). Not while the watch hand holds something (Rec Room's
    /// preventsGazeOpenMenuHandStates). With <see cref="RrWatch.TapToOpen"/>, the other hand's drawn fingertip or palm
    /// touching the face region (Rec Room's TapToOpenRegion: 0.08 x 0.06 x 0.05 m centred 5 cm above the watch's
    /// origin, sized with the watch) toggles.
    /// </summary>
    [DefaultExecutionOrder(10010)]
    public sealed class RrWatchModel : MonoBehaviour
    {
        /// <summary>Rec Room's <c>WatchAnchor</c> rotation in its left hand frame (+Z fingers, +Y thumb, +X out of the left palm).</summary>
        public static readonly Quaternion LeftRotation = new Quaternion(0.5f, -0.5f, -0.5f, 0.5f);

        /// <summary>The right-hand anchor, p (0.02, 0, -0.119).</summary>
        public static readonly Quaternion RightRotation = new Quaternion(-0.5f, 0.5f, -0.5f, 0.5f);

        /// <summary>
        /// The watch in <see cref="RrAvatarHand.TryGetWrist"/>'s frame, left wrist: Rec Room's frame is the wrist frame
        /// turned by Euler(0, 0, -90) (hands_frames.md 2.3), so this is Euler(0, 0, -90) * <see cref="LeftRotation"/> =
        /// (0, -0.7071, -0.7071, 0): face (+Z) to wrist +Y (back of the hand), 3 o'clock (+Y) to +Z (fingers), strap
        /// (+X, 12 o'clock) to -X (the little-finger side).
        /// </summary>
        public static readonly Quaternion LeftInWrist = Quaternion.Euler(0f, 0f, -90f) * LeftRotation;

        /// <summary>Right wrist: Euler(0, 0, 90) * <see cref="RightRotation"/> = (-0.7071, 0, 0, 0.7071): face to +Y, 3 o'clock to -Z (the elbow), strap to +X (little finger).</summary>
        public static readonly Quaternion RightInWrist = Quaternion.Euler(0f, 0f, 90f) * RightRotation;

        /// <summary>
        /// Rec Room's <c>WatchFaceTransform</c> under the anchor, p (0, 0, 0.041), r (0.704, 0.704, 0.062, 0.062): a
        /// camera-like frame, +X = 3 o'clock, +Y = 12 o'clock, +Z INTO the face, tilted 10.1 deg so the eye that sees
        /// the face straight on sits 10 deg off the face normal toward 6 o'clock (the thumb side, toward the body).
        /// </summary>
        public static readonly Quaternion FaceView = Quaternion.Normalize(new Quaternion(0.704f, 0.704f, 0.062f, 0.062f));

        /// <summary>First build (kept as the last fallback): the watch origin in the left GRIP frame, a guess 10 cm down the handle axis.</summary>
        public static readonly Vector3 LeftOffset = new Vector3(-0.015f, 0f, -0.1f);

        public static readonly Vector3 RightOffset = new Vector3(0.015f, 0f, -0.1f);

        /// <summary>Forearm length (elbow to wrist, m) at which the sizes below apply: RrAvatarHand's default, RPM Men measures 0.252.</summary>
        public const float ReferenceForearm = 0.25f;

        /// <summary>Clamp of forearm / <see cref="ReferenceForearm"/> (children's and giant avatars, odd rigs).</summary>
        public const float MinSize = 0.7f;

        public const float MaxSize = 1.4f;

        /// <summary>
        /// Scale of Rec Room's mesh on a 0.25 m forearm. Measured (watch_wrist harness run): on RPM Women's 66 x 52 mm
        /// forearm (0.257 m long) the smallest scale that keeps 2 mm between skin and strap all round and 4 mm under the
        /// case is 0.97, i.e. 0.942 per unit size. Rec Room's inner opening is a ~98 x 58 mm oval, so the height is what
        /// limits it; the sides stay ~12 mm loose.
        /// </summary>
        public const float MeshScale = 0.94f;

        /// <summary>Watch origin up the forearm from the drawn wrist joint (m, 0.25 m forearm). Rec Room's full-body avatar: 1.8 cm.</summary>
        public const float BackDistance = 0.018f;

        /// <summary>
        /// Default watch origin off the joint axis toward the back of the hand (m, 0.25 m forearm), used until (or when)
        /// <see cref="RrWatchFit"/> cannot measure the avatar: RPM Women's bare fit needs 0.5 mm. With a readable avatar
        /// mesh the fitted lift replaces it (<see cref="LiftUsed"/>).
        /// </summary>
        public const float Lift = 0.001f;

        /// <summary>Height of the face above the watch's origin in mesh units (ClockFace z; Rec Room's face transform is at 0.041).</summary>
        public const float FaceHeight = 0.04f;

        /// <summary>Rec Room's <c>Fingertip</c> / <c>MenuLaserOrigin</c> in its hand frame: the poking fingertip, 10 cm ahead of the palm centre on the thumb side.</summary>
        public static readonly Vector3 Fingertip = new Vector3(0f, 0.031f, 0.10f);

        public const float OpenAngle = 60f;
        public const float OpenPitch = 45f;
        public const float OpenRoll = 30f;
        public const float DwellTime = 0.4f;
        public const float CloseAngle = 60f;
        public const float ClosePitch = 50f;
        public const float CloseRoll = 50f;

        /// <summary>Rec Room's TapToOpenRegion box in watch mesh units: centre and half size.</summary>
        private static readonly Vector3 TapCentre = new Vector3(0f, 0f, 0.05f);

        private static readonly Vector3 TapHalf = new Vector3(0.04f, 0.03f, 0.025f);

        private const int MaxSourceLogs = 12;

        /// <summary>Test switch: show the watch at this world pose (dwell ring at <see cref="TestDwell"/>) without a tracked hand.</summary>
        public static Pose? TestPose;

        public static float TestDwell = 0.6f;

        /// <summary>
        /// Test switch: put the watch on <see cref="RrAvatarHand"/>'s wrist without a headset or tracked hands (the
        /// harness's synthetic arms and RPM avatar). The gesture only runs with <see cref="TestGaze"/>.
        /// </summary>
        public static bool TestWrist;

        /// <summary>Test switch: with <see cref="TestWrist"/>, run the gaze/tap gesture against the harness camera.</summary>
        public static bool TestGaze;

        /// <summary>The watch is on a tracked wrist this frame (or at <see cref="TestPose"/>).</summary>
        public bool OnWrist { get; private set; }

        /// <summary>What the pose came from this frame.</summary>
        public RrWatchSource Source { get; private set; }

        /// <summary>The watch origin (on the forearm axis; the strap ring is round it).</summary>
        public Vector3 Position { get; private set; }

        public Vector3 FacePosition { get; private set; }

        /// <summary>The watch's rotation: +Z out of the face, +Y 3 o'clock, +X 12 o'clock (the strap axis).</summary>
        public Quaternion Rotation { get; private set; } = Quaternion.identity;

        /// <summary>World scale of Rec Room's mesh this frame.</summary>
        public float Scale { get; private set; } = 1f;

        /// <summary>Forearm length the size came from (m; 0 for the test pose).</summary>
        public float Forearm { get; private set; }

        /// <summary>Lift applied this frame (m): the fitted one (<see cref="Fitted"/>) or <see cref="Lift"/> times the avatar size.</summary>
        public float LiftUsed { get; private set; }

        /// <summary>This frame's lift came from <see cref="RrWatchFit"/> (the avatar's own forearm surface).</summary>
        public bool Fitted { get; private set; }

        /// <summary>The last fit attempt finished (successfully or not) for the current wrist and avatar.</summary>
        public bool FitDone
        {
            get { return _fitDone && !_fitting; }
        }

        /// <summary>What the last fit found.</summary>
        public RrWatchFitReport FitReport
        {
            get { return _fitReport; }
        }

        /// <summary>Rec Room's watch contour (<see cref="RrWatchFit.Contour"/>), null until the mesh loads or when it is not readable.</summary>
        public float[] Contour
        {
            get { return _contour; }
        }

        /// <summary>0..1 while the wrist is held up, before the menu opens.</summary>
        public float Dwell { get; private set; }

        /// <summary>The menu was opened by the wrist (gaze or tap) and follows it; the gesture may close it.</summary>
        public bool OpenedByWrist { get; set; }

        private Transform _body;
        private Transform _clock;
        private Transform _hour;
        private Transform _minute;
        private Transform _second;
        private Image _ring;
        private GameObject _dwellCanvas;
        private bool _meshAsked;
        private bool _usingFallback;
        private float _held;
        private bool _armed = true;
        private bool _tapInside;
        private float _tapCooldown;
        private RrWatchSource _loggedSource = RrWatchSource.None;
        private bool _loggedRightWrist;
        private int _sourceLogs;
        private bool _compared;
        private float[] _contour;
        private readonly System.Collections.Generic.List<Vector3> _surface = new System.Collections.Generic.List<Vector3>(512);
        private readonly RrWatchFitReport _fitReport = new RrWatchFitReport();
        private bool _fitting;
        private bool _fitDone;
        private float _fitLift = float.NaN;
        private RrHandSide _fitSide;
        private float _fitForearm;
        private SkinnedMeshRenderer _fitRenderer;
        private bool _fitHadRenderer;
        private float _nextFit;
        private int _fitFailures;
        private int _fitLogs;

        private void Awake()
        {
            var body = new GameObject("Rr Watch Model");
            _body = body.transform;
            _body.SetParent(transform, false);
            BuildClock();
            BuildDwell();
            body.SetActive(false);
            LoadModel();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            StopAllCoroutines();
            _fitting = false;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            TestPose = null;
            TestWrist = false;
            TestGaze = false;
            TestDwell = 0.6f;
        }

        // ---- building ----

        private void LoadModel()
        {
            if (_meshAsked)
            {
                return;
            }

            _meshAsked = true;
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                UseFallback("no Rec Room assets");
                return;
            }

            assets.Load<Mesh>("Wrist_Watch", 43, mesh =>
            {
                if (_body == null)
                {
                    return;
                }

                if (mesh == null)
                {
                    UseFallback("mesh Wrist_Watch not found");
                    return;
                }

                var go = new GameObject("Wrist_Watch");
                go.transform.SetParent(_body, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                _contour = RrWatchFit.Contour(mesh);
                if (_contour == null)
                {
                    RrLog.Warn("Watch", "wrist watch: mesh Wrist_Watch is not readable; the watch keeps its default lift on every avatar");
                }
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.sharedMaterial = RrLook.Solid(RrUiKit.Hex(0x7C4A1A));
                assets.Load<Material>("Watch_Wrist_mat", 21, mat =>
                {
                    if (mr != null && mat != null)
                    {
                        mr.sharedMaterial = RrMaterials.Fix(mat);
                    }

                    RrLog.Info("Watch", "wrist watch: mesh Wrist_Watch (" + mesh.vertexCount + " verts), material " + (mat != null ? mat.name + " (" + mat.shader.name + ")" : "missing, plain brown"));
                });
            });

            assets.Load<Material>("Watch_Wrist_Hand_mat", 21, mat =>
            {
                if (mat == null || _clock == null)
                {
                    return;
                }

                mat = RrMaterials.Fix(mat);
                foreach (var r in _clock.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterial = mat;
                }
            });
        }

        private void UseFallback(string why)
        {
            if (_usingFallback || _body == null)
            {
                return;
            }

            _usingFallback = true;
            RrLog.Warn("Watch", "wrist watch: " + why + "; drawing a plain watch");
            var dark = RrLook.Solid(RrUiKit.Hex(0x2A2522));
            var strap = RrLook.Solid(RrUiKit.Hex(0x905826));
            Part(PrimitiveType.Cylinder, "Case", new Vector3(0f, 0f, 0.032f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.046f, 0.008f, 0.046f), dark);
            Part(PrimitiveType.Cylinder, "Face", new Vector3(0f, 0f, 0.0385f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.04f, 0.0015f, 0.04f), RrLook.Solid(RrUiKit.Hex(0xF9F3EF)));
            Part(PrimitiveType.Cube, "Strap", new Vector3(0f, 0f, 0.024f), Quaternion.identity, new Vector3(0.1f, 0.026f, 0.008f), strap);
        }

        private Transform Part(PrimitiveType type, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material m, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            var t = go.transform;
            t.SetParent(parent != null ? parent : _body, false);
            t.localPosition = pos;
            t.localRotation = rot;
            t.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }

        private void BuildClock()
        {
            var clock = new GameObject("ClockFace").transform;
            clock.SetParent(_body, false);
            clock.localPosition = new Vector3(0f, -0.001f, FaceHeight);
            clock.localRotation = new Quaternion(0f, 0f, 0.707107f, -0.707107f);
            _clock = clock;
            var hands = RrLook.Unlit(RrUiKit.Hex(0x553308));
            _hour = Hand("HourHand", new Vector3(0.003f, 0.013f, 0.003f), 0.004f, hands);
            _minute = Hand("MinuteHand", new Vector3(0.002f, 0.018f, 0.002f), 0.006f, hands);
            _second = Hand("SecondHand", new Vector3(0.001f, 0.022f, 0.001f), 0.008f, hands);
        }

        private Transform Hand(string name, Vector3 size, float y, Material m)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(_clock, false);
            Part(PrimitiveType.Cube, "Mesh", new Vector3(0f, y, 0f), Quaternion.identity, size, m, pivot);
            return pivot;
        }

        private void BuildDwell()
        {
            _dwellCanvas = new GameObject("DwellVisual", typeof(RectTransform));
            var rt = (RectTransform)_dwellCanvas.transform;
            rt.SetParent(_body, false);

            // Canvas first: a new Canvas starts in screen-overlay mode and drives its rect until made world-space.
            var canvas = _dwellCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            rt.localPosition = new Vector3(0f, 0f, FaceHeight + 0.004f);
            rt.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rt.sizeDelta = new Vector2(40f, 40f); // DwellVisual: 58 mm disc mesh at scale 0.685
            rt.localScale = Vector3.one * 0.001f;
            var ring = RrUiKit.Fill(rt, "Ring");
            _ring = ring.gameObject.AddComponent<Image>();
            _ring.sprite = RrWatchArt.Ring;
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillClockwise = true;
            _ring.fillAmount = 0f;
            _ring.color = RrUiKit.Hex(0xFFDB8B, 0.85f);
            _ring.raycastTarget = false;
            RrLayers.Set(_dwellCanvas, RrLayers.Default);
            _dwellCanvas.SetActive(false);
        }

        // ---- the pose (static so the harness can check it) ----

        /// <summary>Avatar size factor of a forearm: forearm / <see cref="ReferenceForearm"/>, clamped to <see cref="MinSize"/>..<see cref="MaxSize"/>.</summary>
        public static float SizeOf(float forearm)
        {
            if (!(forearm > 0.001f))
            {
                return 1f;
            }

            return Mathf.Clamp(forearm / ReferenceForearm, MinSize, MaxSize);
        }

        /// <summary>
        /// The watch on a drawn wrist (<see cref="RrAvatarHand.TryGetWrist"/> convention: +Z along the forearm, +Y out of
        /// the back of the hand): origin <see cref="BackDistance"/> times the avatar size up the forearm and
        /// <paramref name="lift"/> metres toward the back of the hand; rotation <see cref="LeftInWrist"/> /
        /// <see cref="RightInWrist"/>; mesh scale <see cref="MeshScale"/> times the avatar size.
        /// </summary>
        public static void PoseOnWrist(RrHandSide side, Pose wrist, float forearm, float lift, out Vector3 position, out Quaternion rotation, out float scale)
        {
            float k = SizeOf(forearm);
            position = wrist.position + wrist.rotation * new Vector3(0f, lift, -BackDistance * k);
            rotation = wrist.rotation * (side == RrHandSide.Left ? LeftInWrist : RightInWrist);
            scale = MeshScale * k;
        }

        /// <summary><see cref="PoseOnWrist(RrHandSide, Pose, float, float, out Vector3, out Quaternion, out float)"/> with the default <see cref="Lift"/>.</summary>
        public static void PoseOnWrist(RrHandSide side, Pose wrist, float forearm, out Vector3 position, out Quaternion rotation, out float scale)
        {
            PoseOnWrist(side, wrist, forearm, Lift * SizeOf(forearm), out position, out rotation, out scale);
        }

        /// <summary>Forget the fit and measure the avatar again at the next frame (tests; an outfit change is noticed by itself).</summary>
        public void RefitNow()
        {
            _fitDone = false;
            _fitLift = float.NaN;
            _nextFit = 0f;
            _fitFailures = 0;
        }

        /// <summary>The first build's pose on a grip (grip-local offset, Rec Room's anchor rotation applied to the raw grip). For the one-shot comparison and the tests.</summary>
        public static void FirstBuildPose(RrHandSide side, Pose grip, float gripScale, out Vector3 position, out Quaternion rotation)
        {
            bool left = side == RrHandSide.Left;
            position = grip.position + grip.rotation * ((left ? LeftOffset : RightOffset) * gripScale);
            rotation = grip.rotation * (left ? LeftRotation : RightRotation);
        }

        /// <summary>
        /// Where the face is relative to the eyes: <paramref name="angle"/> = off the view centre; <paramref name="pitch"/>
        /// = the eye's tilt away from the ideal view along the forearm (the 3 o'clock axis), <paramref name="roll"/> =
        /// across the wrist (the 12 o'clock axis), both in <see cref="FaceView"/>'s frame; <paramref name="facing"/> = the
        /// eye is on the face's side.
        /// </summary>
        public static void GazeAngles(Vector3 face, Quaternion watch, Vector3 head, Vector3 headForward, out float angle, out float pitch, out float roll, out bool facing)
        {
            angle = Vector3.Angle(headForward, face - head);
            Vector3 e = Quaternion.Inverse(watch * FaceView) * (head - face).normalized;
            facing = e.z < 0f;
            float depth = Mathf.Max(0.0001f, -e.z);
            pitch = Mathf.Abs(Mathf.Atan2(e.x, depth) * Mathf.Rad2Deg);
            roll = Mathf.Abs(Mathf.Atan2(e.y, depth) * Mathf.Rad2Deg);
        }

        /// <summary>The tap region (Rec Room's TapToOpenRegion, sized with the watch) contains <paramref name="world"/>.</summary>
        public static bool TapRegionContains(Vector3 watchPosition, Quaternion watchRotation, float scale, Vector3 world)
        {
            Vector3 local = Quaternion.Inverse(watchRotation) * (world - watchPosition) / Mathf.Max(0.01f, scale) - TapCentre;
            return Mathf.Abs(local.x) <= TapHalf.x && Mathf.Abs(local.y) <= TapHalf.y && Mathf.Abs(local.z) <= TapHalf.z;
        }

        /// <summary>
        /// The poking hand's drawn fingertip (Rec Room's <see cref="Fingertip"/> on <see cref="RrAvatarHand.TryGetRecRoomHand"/>,
        /// times the avatar size) and palm centre; without a drawn hand, the controller's aim 6 cm ahead of the grip.
        /// </summary>
        public static bool TryPokePoints(RrHandSide side, out Vector3 tip, out Vector3 palm)
        {
            Pose hand;
            if (RrAvatarHand.TryGetRecRoomHand(side, out hand))
            {
                Pose wrist;
                float forearm;
                float k = RrAvatarHand.TryGetWrist(side, out wrist, out forearm) ? SizeOf(forearm) : 1f;
                tip = hand.position + hand.rotation * (Fingertip * k);
                palm = hand.position;
                return true;
            }

            var h = RrHand.Get(side);
            if (h != null && h.Tracked)
            {
                tip = h.Position + h.AimRotation * new Vector3(0f, 0f, 0.06f);
                palm = h.Position;
                return true;
            }

            tip = palm = Vector3.zero;
            return false;
        }

        // ---- per frame ----

        private bool TryWrist(out Vector3 pos, out Quaternion rot, out float scale, out float forearm, out float lift, out bool fitted, out RrWatchSource source)
        {
            scale = 1f;
            forearm = 0f;
            lift = 0f;
            fitted = false;
            if (TestPose.HasValue)
            {
                pos = TestPose.Value.position;
                rot = TestPose.Value.rotation;
                source = RrWatchSource.TestPose;
                return true;
            }

            pos = Vector3.zero;
            rot = Quaternion.identity;
            source = RrWatchSource.None;
            RrHandSide side = RrWatch.RightWrist ? RrHandSide.Right : RrHandSide.Left;
            RrHand hand = RrHand.Get(side);
            if (!TestWrist)
            {
                if (!RrPlayer.InVr || hand == null || !hand.Tracked || hand.IsScreenHand)
                {
                    return false;
                }
            }

            Pose wrist;
            if (RrAvatarHand.TryGetWrist(side, out wrist, out forearm))
            {
                RrAvatarHandSource from = RrAvatarHand.Source(side);
                bool bones = from == RrAvatarHandSource.HumanoidBones || from == RrAvatarHandSource.NamedBones;
                fitted = bones && _fitDone && !float.IsNaN(_fitLift) && _fitSide == side && Mathf.Abs(forearm - _fitForearm) <= 0.005f;
                lift = fitted ? _fitLift : Lift * SizeOf(forearm);
                PoseOnWrist(side, wrist, forearm, lift, out pos, out rot, out scale);
                switch (from)
                {
                    case RrAvatarHandSource.HumanoidBones:
                    case RrAvatarHandSource.NamedBones:
                        source = RrWatchSource.AvatarBones;
                        break;
                    case RrAvatarHandSource.IkTarget:
                        source = RrWatchSource.IkTarget;
                        break;
                    default:
                        source = RrWatchSource.GripClientIk;
                        break;
                }

                return true;
            }

            // Last resort (RrAvatarHand had nothing, e.g. it threw): the first build's grip offset, with the rotation
            // taken through the client's default IK hand frame so the strap at least goes round the wrist.
            if (TestWrist || hand == null || hand.Grip == null)
            {
                return false;
            }

            Transform grip = hand.Grip;
            float gs = hand.Anchor != null ? Mathf.Max(0.05f, hand.Anchor.lossyScale.x) : 1f;
            bool left = side == RrHandSide.Left;
            pos = grip.position + grip.rotation * ((left ? LeftOffset : RightOffset) * gs);
            rot = grip.rotation * RrAvatarHand.GripToRecRoomDefault(side).rotation * (left ? LeftRotation : RightRotation);
            forearm = ReferenceForearm * gs;
            scale = MeshScale * SizeOf(forearm);
            lift = Lift * SizeOf(forearm);
            source = RrWatchSource.GripFirstBuild;
            return true;
        }

        /// <summary>Starts a sleeve fit (<see cref="RrWatchFit"/>) when the drawn wrist is new: first time, other wrist, other avatar size, or the measured mesh was destroyed; failed fits retry after 10 s, then every 60 s.</summary>
        private void MaybeFit()
        {
            if (_fitting || !OnWrist || Source != RrWatchSource.AvatarBones || _contour == null)
            {
                return;
            }

            RrHandSide side = RrWatch.RightWrist ? RrHandSide.Right : RrHandSide.Left;
            bool stale = !_fitDone || _fitSide != side || Mathf.Abs(Forearm - _fitForearm) > 0.005f || (_fitHadRenderer && _fitRenderer == null) ||
                         (float.IsNaN(_fitLift) && Time.unscaledTime >= _nextFit);
            if (!stale || Time.unscaledTime < _nextFit)
            {
                return;
            }

            StartCoroutine(Fit(side, Forearm));
        }

        private System.Collections.IEnumerator Fit(RrHandSide side, float forearm)
        {
            _fitting = true;
            yield return RrWatchFit.SampleSurface(side, _surface, _fitReport);
            _fitting = false;
            _fitDone = true;
            _fitSide = side;
            _fitForearm = forearm;
            _fitRenderer = _fitReport.First;
            _fitHadRenderer = _fitRenderer != null;
            float k = SizeOf(forearm);
            float lift;
            float topGap;
            bool ok = RrWatchFit.SolveLift(_surface, side, MeshScale * k, BackDistance * k, _contour, RrWatchFit.MinLift * k, RrWatchFit.MaxLift * k, out lift, out topGap);
            RrWatchFitReport r = _fitReport;
            string found = r.Matched + " of " + r.Renderers + " skinned meshes at the " + (side == RrHandSide.Left ? "left" : "right") + " wrist (" + r.Unreadable + " not readable), " +
                           r.Points + " surface points from " + r.Vertices + " vertices" + (r.Names.Length > 0 ? " [" + r.Names + "]" : "") + ", " + r.Milliseconds.ToString("0.00") + " ms";
            if (ok)
            {
                _fitLift = lift;
                _fitFailures = 0;
                if (_fitLogs++ < 8)
                {
                    RrLog.Info("Watch", "wrist watch fitted to the avatar's forearm: " + found + "; lift " + (lift * 1000f).ToString("0.0") + " mm (default " + (Lift * k * 1000f).ToString("0.0") +
                                        "), " + (topGap * 1000f).ToString("0.0") + " mm under the case" + (topGap < RrWatchFit.SleeveClearance ? " (capped: the sleeve is bigger than the watch can clear)" : ""));
                }
            }
            else
            {
                _fitLift = float.NaN;
                _fitFailures++;
                _nextFit = Time.unscaledTime + (_fitFailures < 3 ? 10f : 60f);
                if (_fitLogs++ < 8)
                {
                    RrLog.Info("Watch", "wrist watch: no fit to the avatar's forearm (" + found + "); default lift " + (Lift * k * 1000f).ToString("0.0") + " mm, next try in " + (_fitFailures < 3 ? 10 : 60) + " s");
                }
            }
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            PlaceOnWrist();
        }

        private void PlaceOnWrist()
        {
            Vector3 pos;
            Quaternion rot;
            float scale;
            float forearm;
            float lift;
            bool fitted;
            RrWatchSource source;
            bool on = TryWrist(out pos, out rot, out scale, out forearm, out lift, out fitted, out source);
            if (_body == null)
            {
                return;
            }

            if (on != _body.gameObject.activeSelf)
            {
                _body.gameObject.SetActive(on);
            }

            OnWrist = on;
            Source = on ? source : RrWatchSource.None;
            if (!on)
            {
                return;
            }

            _body.SetPositionAndRotation(pos, rot);
            Transform parent = _body.parent;
            float parentScale = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.x) : 1f;
            float local = scale / parentScale;
            if (Mathf.Abs(_body.localScale.x - local) > 1e-5f)
            {
                _body.localScale = new Vector3(local, local, local);
            }

            Position = pos;
            Rotation = rot;
            Scale = scale;
            Forearm = forearm;
            LiftUsed = lift;
            Fitted = fitted;
            FacePosition = pos + rot * new Vector3(0f, 0f, FaceHeight * scale);
            LogSource(source, forearm);
        }

        /// <summary>One line whenever the source changes (at most <see cref="MaxSourceLogs"/>), plus a one-shot comparison with the first build's placement.</summary>
        private void LogSource(RrWatchSource source, float forearm)
        {
            bool right = RrWatch.RightWrist;
            if (source == _loggedSource && right == _loggedRightWrist)
            {
                return;
            }

            _loggedSource = source;
            _loggedRightWrist = right;
            if (_sourceLogs >= MaxSourceLogs)
            {
                return;
            }

            _sourceLogs++;
            string what;
            switch (source)
            {
                case RrWatchSource.TestPose:
                    what = "the test pose";
                    break;
                case RrWatchSource.AvatarBones:
                    what = "the avatar's drawn wrist (" + RrAvatarHand.Source(right ? RrHandSide.Right : RrHandSide.Left) + ")";
                    break;
                case RrWatchSource.IkTarget:
                    what = "the client's VRIK hand target (no avatar bones found)";
                    break;
                case RrWatchSource.GripClientIk:
                    what = "the grip with the client's IK offsets (no avatar bones, no IK target)";
                    break;
                default:
                    what = "the grip with the first build's offset (no drawn hand)";
                    break;
            }

            RrLog.Info("Watch", "wrist watch on the " + (right ? "right" : "left") + " wrist from " + what + ": forearm " + forearm.ToString("0.000") + " m, mesh scale " +
                                Scale.ToString("0.00") + ", origin " + (BackDistance * SizeOf(forearm) * 100f).ToString("0.0") + " cm up the forearm" + (source == RrWatchSource.TestPose ? "" : ", lifted " + (LiftUsed * 1000f).ToString("0.0") + " mm" + (Fitted ? " (fitted)" : "")) +
                                (TestWrist ? " (test)" : ""));
            if (!_compared && source == RrWatchSource.AvatarBones)
            {
                CompareWithFirstBuild(right ? RrHandSide.Right : RrHandSide.Left);
            }
        }

        /// <summary>Once, in a headset: how far the first build's grip-based watch would be from the drawn-wrist watch (the user's "floating off the avatar").</summary>
        private void CompareWithFirstBuild(RrHandSide side)
        {
            var hand = RrHand.Get(side);
            if (hand == null || hand.Grip == null || !hand.Tracked || hand.IsScreenHand)
            {
                return;
            }

            _compared = true;
            Transform g = hand.Grip;
            Vector3 oldPos;
            Quaternion oldRot;
            FirstBuildPose(side, new Pose(g.position, g.rotation), hand.Anchor != null ? hand.Anchor.lossyScale.x : 1f, out oldPos, out oldRot);
            RrLog.Info("Watch", "the first build's grip-based watch would sit " + ((oldPos - Position).magnitude * 100f).ToString("0.0") + " cm from the drawn wrist's, face turned " +
                                Vector3.Angle(oldRot * Vector3.forward, Rotation * Vector3.forward).ToString("0") + " deg, strap turned " +
                                Vector3.Angle(oldRot * Vector3.right, Rotation * Vector3.right).ToString("0") + " deg");
        }

        private void LateUpdate()
        {
            PlaceOnWrist();
            MaybeFit();
            UpdateClock();
            if (TestPose.HasValue)
            {
                ShowDwell(TestDwell);
                return;
            }

            if (!OnWrist || (TestWrist && !TestGaze))
            {
                _held = 0f;
                ShowDwell(0f);
                return;
            }

            if (RrWatch.TapToOpen)
            {
                UpdateTap();
                ShowDwell(0f);
            }
            else
            {
                UpdateGaze();
            }
        }

        private void UpdateClock()
        {
            if (_hour == null || !_body.gameObject.activeSelf)
            {
                return;
            }

            var now = DateTime.Now;
            float seconds = RrWatch.ShowSeconds ? now.Second + now.Millisecond * 0.001f : 0f;
            float minutes = now.Minute + seconds / 60f;
            float hours = (now.Hour % 12) + minutes / 60f;

            // Unity is left-handed: seen from the front (+Z toward the eyes) a positive turn about Z is clockwise.
            // With the ClockFace node turned -90 deg, 12 o'clock is the watch's +X (the strap axis) and 3 o'clock +Y (the hand).
            _hour.localRotation = Quaternion.Euler(0f, 0f, hours * 30f);
            _minute.localRotation = Quaternion.Euler(0f, 0f, minutes * 6f);
            _second.localRotation = Quaternion.Euler(0f, 0f, seconds * 6f);
            _second.gameObject.SetActive(RrWatch.ShowSeconds);
        }

        private void UpdateGaze()
        {
            float angle;
            float pitch;
            float roll;
            bool facing;
            GazeAngles(FacePosition, Rotation, RrPlayer.HeadPosition, RrPlayer.HeadRotation * Vector3.forward, out angle, out pitch, out roll, out facing);
            var ui = RrWatch.Ui;
            bool open = ui != null && ui.IsOpen;

            // A detached menu stays put; raising the wrist again (a full dwell) brings it back onto the wrist.
            bool detached = open && ui.Detached;
            if (open && !detached)
            {
                _held = 0f;
                ShowDwell(0f);
                bool keep = facing && angle <= CloseAngle && pitch <= ClosePitch && roll <= CloseRoll;
                if (!keep && OpenedByWrist && !ui.Detached)
                {
                    OpenedByWrist = false;
                    ui.Close();
                }

                _armed = false;
                return;
            }

            bool raised = facing && angle <= OpenAngle && pitch <= OpenPitch && roll <= OpenRoll && !HandBusy();
            if (!raised)
            {
                _armed = true;
                _held = 0f;
                ShowDwell(0f);
                return;
            }

            // After the menu closed while the wrist stayed up (a button, travel), wait for the wrist to go down first.
            if (!_armed || ui == null || !ui.Built)
            {
                ShowDwell(0f);
                return;
            }

            if (_held <= 0f)
            {
                RrWatchSounds.Play(RrWatchSounds.Cloth);
            }

            _held += Time.deltaTime;
            ShowDwell(Mathf.Clamp01(_held / DwellTime));
            if (_held >= DwellTime)
            {
                _held = 0f;
                _armed = false;
                OpenedByWrist = true;
                if (detached)
                {
                    ui.ToggleDetach();
                }
                else
                {
                    ui.Open();
                }

                RrLog.Info("Watch", "menu opened by looking at the " + (RrWatch.RightWrist ? "right" : "left") + " wrist (view " + angle.ToString("0") + " deg, pitch " + pitch.ToString("0") + ", roll " + roll.ToString("0") + ")");
                var hand = RrHand.Get(RrWatch.RightWrist ? RrHandSide.Right : RrHandSide.Left);
                if (hand != null)
                {
                    hand.Haptic(0.2f, 0.05f);
                }
            }
        }

        private bool HandBusy()
        {
            var hand = RrHand.Get(RrWatch.RightWrist ? RrHandSide.Right : RrHandSide.Left);
            return hand != null && hand.Held != null;
        }

        private void UpdateTap()
        {
            _tapCooldown -= Time.deltaTime;
            RrHandSide otherSide = RrWatch.RightWrist ? RrHandSide.Left : RrHandSide.Right;
            var other = RrHand.Get(otherSide);
            Vector3 tip;
            Vector3 palm;
            if ((!TestWrist && (other == null || !other.Tracked)) || !TryPokePoints(otherSide, out tip, out palm))
            {
                _tapInside = false;
                return;
            }

            bool inside = TapRegionContains(Position, Rotation, Scale, tip) || TapRegionContains(Position, Rotation, Scale, palm);
            if (inside && !_tapInside && _tapCooldown <= 0f)
            {
                _tapCooldown = 0.5f;
                var ui = RrWatch.Ui;
                if (ui != null && ui.Built)
                {
                    if (ui.IsOpen)
                    {
                        ui.Close();
                    }
                    else
                    {
                        OpenedByWrist = true;
                        ui.Open();
                    }

                    RrLog.Info("Watch", "menu " + (ui.IsOpen ? "opened" : "closed") + " by tapping the watch");
                    if (other != null)
                    {
                        other.Haptic(0.3f, 0.05f);
                    }
                }
            }

            _tapInside = inside;
        }

        private void ShowDwell(float fill)
        {
            if (_ring == null)
            {
                return;
            }

            bool show = fill > 0.001f;
            if (_dwellCanvas.activeSelf != show)
            {
                _dwellCanvas.SetActive(show);
            }

            Dwell = fill;
            if (show)
            {
                _ring.fillAmount = fill;
            }
        }
    }
}
