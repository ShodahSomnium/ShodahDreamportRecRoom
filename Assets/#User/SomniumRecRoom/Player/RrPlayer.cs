using System;
using SomniumSpace.Bridge;
using SomniumSpace.Bridge.Player;
using SomniumSpace.Bridge.Player.Components;
using UnityEngine;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// Everything the world needs to know about the local player: head, feet, VR or desktop, teleporting, movement
    /// limits, identity (ARCHITECTURE.md §4, somnium_player.md §1, §6, §7, §9.1). The Somnium avatar itself is never
    /// touched; all of this goes through the Bridge or our own objects.
    ///
    /// <b>Objects.</b> <see cref="Init"/> makes "Rr Player" under <c>RrMain.WorldRoot</c> with the input pump
    /// (<see cref="RrInputPump"/>, Update at order -10000: VR buttons, <see cref="RrInput"/>, the screen hand's buttons)
    /// and the driver (<see cref="RrPlayerDriver"/>, LateUpdate at order 10000 - after the client's Cinemachine brain at
    /// 100 - the client's tablet, desktop play mode (<see cref="RrScreenCamera"/>), both <see cref="RrHand"/> poses and
    /// <see cref="RrRemotePlayers"/>). The hands and the crosshair live under it too.
    ///
    /// <b>The client's body.</b> <c>SomniumBridge.PlayersContainer.LocalPlayer.References.Body</c> is re-read once a
    /// frame (it is null in the editor, arrives after the world starts and changes when the player switches between
    /// headset and desktop, because the XR and NonXR body prefabs differ). Root = <c>XR.Body</c> / <c>NonXR.Body</c>
    /// (the CharacterController), Head = <c>HeadAnchor</c>, hands = the tracked controller anchors, null on desktop.
    ///
    /// <b>The camera</b> (<see cref="Camera"/>) is <c>EtsVr.Head</c>: <c>Camera.main</c> when it renders, else the first
    /// enabled camera drawing to the screen; asked every frame (the client's camera arrives late and can change), with
    /// the scene search throttled to twice a second.
    ///
    /// <b>No client</b> (the user's editor, the harness): <c>PlayersContainer</c> stays null. After half a second the
    /// driver makes the <see cref="RrFallbackRig"/> - a first-person capsule with WASD and right-mouse look, its own
    /// camera when none renders - so the world can be walked; <see cref="Root"/>, <see cref="HeadAnchor"/>,
    /// <see cref="Teleport"/> and <see cref="SetMovement"/> then use it. Once a client container has been seen the rig
    /// is never made (and destroyed if it exists), so it cannot appear in the client.
    /// </summary>
    public static class RrPlayer
    {
        /// <summary>How long the driver waits for a client container before it makes the editor rig.</summary>
        public const float FallbackDelay = 0.5f;

        private const float CameraSearchInterval = 0.5f;

        private static GameObject _object;
        private static RrFallbackRig _rig;
        private static bool _initialised;
        private static float _initAt;
        private static bool _sawClient;

        // Once-a-frame cache of the Bridge body and the view camera.
        private static int _frame = -1;
        private static ISomniumPlayersContainer _container;
        private static ISomniumPlayer _local;
        private static Transform _root;
        private static Transform _head;
        private static Transform _left;
        private static Transform _right;
        private static Camera _camera;
        private static Camera _searched;
        private static float _nextCameraSearch;

        // Movement multipliers we set (restored on shutdown so a frozen player is never left frozen).
        private static float _speed = 1f;
        private static bool _jump = true;
        private static bool _movementTouched;
        private static ISomniumPlayerMotion _appliedMotion;

        private static bool _hasPendingPose;
        private static Vector3 _pendingFeet;
        private static float _pendingYaw;

        private static float _fovKick = 1f;

        // Logged-once facts.
        private static bool _loggedNoClient;
        private static bool _loggedClient;
        private static bool _loggedBridgeError;
        private static bool _loggedNoMotion;
        private static bool _wasVr;
        private static bool _vrKnown;
        private static Transform _loggedRoot;
        private static int _rootLogs;
        private static Camera _loggedCamera;
        private static int _cameraLogs;
        private static int _teleportLogs;
        private static int _movementLogs;
        private static int _thirdPersonLogs;

        // ---- mode --------------------------------------------------------------------------------------------

        /// <summary>True in a headset (XRSettings.isDeviceActive with a device name), as the siblings test it.</summary>
        public static bool InVr
        {
            get { return XRSettings.isDeviceActive && !string.IsNullOrEmpty(XRSettings.loadedDeviceName); }
        }

        /// <summary>
        /// Desktop play mode (Rec Room's first-person screen mode) is in effect this frame: the camera and cursor are
        /// ours, aim is the centre of the view, E/Q/right mouse are gameplay keys (<see cref="RrInput"/>).
        /// </summary>
        public static bool InPlayMode
        {
            get { return RrScreenCamera.Active; }
        }

        /// <summary>No Somnium client: the editor rig (<see cref="RrFallbackRig"/>) is the player.</summary>
        public static bool UsingFallbackRig
        {
            get { return _rig != null; }
        }

        /// <summary>The player chose third person (V) - play mode then leaves the client's orbit camera alone.</summary>
        public static bool ThirdPerson { get; set; }

        /// <summary>Screen mode takes the camera while the screen hand holds something (Rec Room does). Default true.</summary>
        public static bool PlayModeWhileHolding { get; set; } = true;

        /// <summary>Field-of-view multiplier for play mode (sprint 1.15, slide 1.3 in Rec Room's screen configs); 0.5..2.</summary>
        public static float FovKick
        {
            get { return _fovKick; }
            set { _fovKick = Mathf.Clamp(value, 0.5f, 2f); }
        }

        /// <summary>A game (by <paramref name="owner"/>, e.g. "paintball") wants desktop play mode on or no longer.</summary>
        public static void RequestPlayMode(string owner, bool on)
        {
            RrScreenCamera.Request(owner, on);
        }

        // ---- where the player is -----------------------------------------------------------------------------

        /// <summary>The camera that renders the player's view (EtsVr.Head logic), re-queried every frame.</summary>
        public static Camera Camera
        {
            get
            {
                Refresh();
                return _camera;
            }
        }

        /// <summary>The object our player-side things hang under ("Rr Player"), null before Init.</summary>
        public static Transform PlayerObject
        {
            get { return _object != null ? _object.transform : null; }
        }

        /// <summary>The body to move: the Bridge <c>Body.Root</c> (XR.Body / NonXR.Body), or the fallback rig.</summary>
        public static Transform Root
        {
            get
            {
                Refresh();
                if (_root != null)
                {
                    return _root;
                }

                return _rig != null ? _rig.transform : null;
            }
        }

        /// <summary>The Bridge <c>Body.Head</c> (HeadAnchor), or the fallback rig's head.</summary>
        public static Transform HeadAnchor
        {
            get
            {
                Refresh();
                if (_head != null)
                {
                    return _head;
                }

                return _rig != null ? _rig.Head : null;
            }
        }

        /// <summary>The client's tracked controller anchor for a hand (VR only; null on desktop and in the editor).</summary>
        public static Transform BridgeHand(RrHandSide side)
        {
            Refresh();
            Transform t = side == RrHandSide.Left ? _left : _right;
            return t != null ? t : null;
        }

        /// <summary>
        /// Where the eyes are: the camera in VR, in play mode and on the rig; on desktop with the client's orbit camera,
        /// the avatar's head anchor (the camera is behind the avatar then).
        /// </summary>
        public static Vector3 HeadPosition
        {
            get
            {
                Camera c = Camera;
                if (!InVr && !InPlayMode && _rig == null && _head != null)
                {
                    return _head.position;
                }

                if (c != null)
                {
                    return c.transform.position;
                }

                Transform h = HeadAnchor;
                return h != null ? h.position : Vector3.zero;
            }
        }

        /// <summary>Where the player looks: the view (camera) rotation.</summary>
        public static Quaternion HeadRotation
        {
            get
            {
                Camera c = Camera;
                if (c != null)
                {
                    return c.transform.rotation;
                }

                Transform h = HeadAnchor;
                return h != null ? h.rotation : Quaternion.identity;
            }
        }

        /// <summary>
        /// The feet: the Bridge <c>Body.Root</c> on desktop; in VR the head projected onto the rig's floor (the XR
        /// origin is the play-area centre, not under the player); the rig's position; else 1.6 m under the head.
        /// </summary>
        public static Vector3 FeetPosition
        {
            get
            {
                Refresh();
                if (_root != null)
                {
                    if (InVr)
                    {
                        Vector3 h = HeadPosition;
                        return new Vector3(h.x, _root.position.y, h.z);
                    }

                    return _root.position;
                }

                if (_rig != null)
                {
                    return _rig.transform.position;
                }

                Vector3 head = HeadPosition;
                return new Vector3(head.x, head.y - 1.6f, head.z);
            }
        }

        /// <summary>The heading the player faces (degrees, 0 = +Z), from the view flattened onto the floor.</summary>
        public static float Yaw
        {
            get
            {
                Vector3 f = HeadRotation * Vector3.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 1e-6f)
                {
                    return HeadRotation.eulerAngles.y;
                }

                return Mathf.Repeat(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 360f);
            }
        }

        // ---- identity ----------------------------------------------------------------------------------------

        /// <summary>Fusion PlayerId of the local player (<c>NetworkRunner.LocalPlayer.PlayerId</c> while running), 0 offline.</summary>
        public static int LocalId
        {
            get
            {
                try
                {
                    var runner = SceneNetworking.NetworkRunnerRef;
                    if (runner != null && runner.IsRunning)
                    {
                        int id = runner.LocalPlayer.PlayerId;
                        if (id >= 0)
                        {
                            return id;
                        }
                    }
                }
                catch (Exception)
                {
                    // A runner shutting down: offline.
                }

                return 0;
            }
        }

        /// <summary>The local player's Somnium nickname, "Player" without a client.</summary>
        public static string Nick
        {
            get
            {
                Refresh();
                try
                {
                    ISomniumPlayerProperties p = _local != null ? _local.Properties : null;
                    string n = p != null ? p.NickName : null;
                    if (!string.IsNullOrEmpty(n))
                    {
                        return n;
                    }
                }
                catch (Exception)
                {
                    // The client's player object is being replaced.
                }

                return "Player";
            }
        }

        /// <summary>The Bridge's local player (null in the editor).</summary>
        public static ISomniumPlayer Local
        {
            get
            {
                Refresh();
                return _local;
            }
        }

        /// <summary>The Bridge's motion feature of the local player (null in the editor).</summary>
        public static ISomniumPlayerMotion Motion
        {
            get
            {
                Refresh();
                try
                {
                    ISomniumPlayerFeatures f = _local != null ? _local.Features : null;
                    return f != null ? f.Motion : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        internal static ISomniumPlayersContainer Container
        {
            get
            {
                Refresh();
                return _container;
            }
        }

        /// <summary>A client container has been seen: this is the Somnium client, not the editor.</summary>
        public static bool HasClient
        {
            get
            {
                Refresh();
                return _sawClient;
            }
        }

        // ---- actions -----------------------------------------------------------------------------------------

        /// <summary>
        /// Moves the local player (feet position, facing yaw). Client: the Bridge's asynchronous
        /// <c>DoTeleportToPoint(feet, (0, yaw, 0), done)</c>. No client: the fallback rig moves at once (or is made
        /// there). <paramref name="done"/> runs after the move; also when the move is impossible, so a caller never waits
        /// forever. Hands forget their velocity and play mode takes the new heading.
        /// </summary>
        public static void Teleport(Vector3 feet, float yaw, Action done = null)
        {
            LastTeleportAt = Time.realtimeSinceStartup;
            LastTeleportTarget = feet;
            Refresh();
            if (_teleportLogs < 10)
            {
                _teleportLogs++;
                RrLog.Info("Player", "teleport to " + RrHand.Fmt(feet) + " yaw " + yaw.ToString("0") +
                                     (_container != null ? " (Bridge)" : _rig != null ? " (fallback rig)" : " (no body yet: rig made there)"));
            }

            if (_container != null)
            {
                ISomniumPlayerMotion m = Motion;
                if (m == null)
                {
                    if (!_loggedNoMotion)
                    {
                        _loggedNoMotion = true;
                        RrLog.Warn("Player", "teleport: the client has no local player motion yet - not moved");
                    }

                    AfterTeleport(yaw);
                    Invoke(done);
                    return;
                }

                try
                {
                    m.DoTeleportToPoint(feet, new Vector3(0f, yaw, 0f), () =>
                    {
                        AfterTeleport(yaw);
                        Invoke(done);
                    });
                }
                catch (Exception e)
                {
                    RrLog.Error("Player", "DoTeleportToPoint failed", e);
                    Invoke(done);
                }

                return;
            }

            if (_rig != null)
            {
                _rig.Teleport(feet, yaw);
            }
            else
            {
                _hasPendingPose = true;
                _pendingFeet = feet;
                _pendingYaw = yaw;
            }

            AfterTeleport(yaw);
            Invoke(done);
        }

        /// <summary>
        /// Realtime (Time.realtimeSinceStartup) of the last <see cref="Teleport"/> call, -100 before any. Travel's
        /// rescue uses it to tell our own moves (spawns, the dorm ladder, quest acts, Paintball's maps) from the
        /// client's respawn, which does not come through here.
        /// </summary>
        public static float LastTeleportAt { get; private set; } = -100f;

        /// <summary>
        /// Harness only: moves the editor rig the way the client moves the body on its own (its respawn, its
        /// anti-fall), without going through <see cref="Teleport"/> - so travel's rescue sees a move that is not
        /// ours. Returns false (and does nothing) with the client or without a rig.
        /// </summary>
        internal static bool SimulateExternalMove(Vector3 feet)
        {
            Refresh();
            if (_container != null || _rig == null)
            {
                return false;
            }

            _rig.Teleport(feet, Yaw);
            return true;
        }

        /// <summary>Realtime at which the last teleport reported done (the Bridge answers within about two frames).</summary>
        public static float LastTeleportDoneAt { get; private set; } = -100f;

        /// <summary>Where the last teleport put the feet.</summary>
        public static Vector3 LastTeleportTarget { get; private set; }

        /// <summary>
        /// Scales walking speed (1 = the client's default, 0 = frozen, clamped 0..10) and allows or forbids jumping.
        /// Client: <c>SetMovementSpeed(s, s, s)</c> and <c>SetJump</c>; at 0 also VR teleport locomotion and fly mode
        /// are switched off (they would move a frozen player). Restore with <c>SetMovement(1)</c>; shutdown restores it.
        /// </summary>
        public static void SetMovement(float speedScale, bool jump = true)
        {
            float s = Mathf.Clamp(speedScale, 0f, 10f);
            _speed = s;
            _jump = jump;
            _movementTouched = s != 1f || !jump;
            if (_movementLogs < 12)
            {
                _movementLogs++;
                RrLog.Info("Player", "movement x" + s.ToString("0.##") + (jump ? "" : ", no jump"));
            }

            if (_rig != null)
            {
                _rig.SpeedScale = s;
                _rig.JumpAllowed = jump;
            }

            ApplyMovement(Motion, s, jump);
        }

        private static void ApplyMovement(ISomniumPlayerMotion m, float s, bool jump)
        {
            if (m == null)
            {
                return;
            }

            try
            {
                _appliedMotion = m; // also on failure: no retry (and error) every frame
                float j = jump ? 1f : 0f;
                m.SetMovementSpeed(s, s, s);
                m.SetJump(j, j);
                m.SetTeleportationDisableState(s <= 0f);
                m.SetFlyModeDisableState(s <= 0f);
            }
            catch (Exception e)
            {
                RrLog.Error("Player", "setting the movement multipliers failed", e);
            }
        }

        private static void AfterTeleport(float yaw)
        {
            LastTeleportDoneAt = Time.realtimeSinceStartup;
            RrScreenCamera.SetYaw(yaw);
            RrHand l = RrHand.Left;
            RrHand r = RrHand.Right;
            if (l != null)
            {
                l.ClearHistory();
            }

            if (r != null)
            {
                r.ClearHistory();
            }
        }

        private static void Invoke(Action done)
        {
            if (done == null)
            {
                return;
            }

            try
            {
                done();
            }
            catch (Exception e)
            {
                RrLog.Error("Player", "teleport callback threw", e);
            }
        }

        // ---- lifetime ----------------------------------------------------------------------------------------

        public static void Init()
        {
            if (_initialised && _object != null)
            {
                return;
            }

            _initialised = true;
            _initAt = Time.unscaledTime;
            Transform parent = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            _object = new GameObject("Rr Player");
            if (parent != null)
            {
                _object.transform.SetParent(parent, false);
            }

            _object.AddComponent<RrInputPump>();
            _object.AddComponent<RrPlayerDriver>();
            RrHand.Left = RrHand.Create(_object.transform, RrHandSide.Left);
            RrHand.Right = RrHand.Create(_object.transform, RrHandSide.Right);
            _frame = -1;
            Refresh();
            RrLog.Info("Player", "up: " + (InVr ? "VR (" + XRSettings.loadedDeviceName + ")" : "desktop") +
                                 ", client " + (_container != null ? "present" : "absent") +
                                 ", camera " + (_camera != null ? "'" + _camera.name + "'" : "none yet") +
                                 (parent == null ? " (no WorldRoot: at the scene root)" : ""));
        }

        public static void Shutdown()
        {
            try
            {
                if (_movementTouched)
                {
                    ApplyMovement(Motion, 1f, true);
                    RrLog.Info("Player", "movement restored to x1 on shutdown");
                }
            }
            catch (Exception e)
            {
                RrLog.Error("Player", "restoring movement on shutdown failed", e);
            }

            RrScreenCamera.Reset();
            RrInput.Reset();
            RrXr.Reset();
            RrClientTablet.Reset();
            RrRemotePlayers.Reset();
            RrAvatarHand.Reset();
            RrHand.Left = null;
            RrHand.Right = null;
            if (_object != null)
            {
                Object.Destroy(_object);
            }

            _object = null;
            _rig = null;
            _initialised = false;
            _sawClient = false;
            _frame = -1;
            _container = null;
            _local = null;
            _root = _head = _left = _right = null;
            _camera = null;
            _searched = null;
            _nextCameraSearch = 0f;
            _speed = 1f;
            _jump = true;
            _movementTouched = false;
            _appliedMotion = null;
            _hasPendingPose = false;
            _fovKick = 1f;
            ThirdPerson = false;
            PlayModeWhileHolding = true;
            _loggedNoClient = _loggedClient = _loggedBridgeError = _loggedNoMotion = false;
            _vrKnown = _wasVr = false;
            _loggedRoot = null;
            _loggedCamera = null;
            _rootLogs = _cameraLogs = _teleportLogs = _movementLogs = _thirdPersonLogs = 0;
            LastTeleportAt = -100f;
            LastTeleportDoneAt = -100f;
            LastTeleportTarget = Vector3.zero;
        }

        /// <summary>The player object went away without Shutdown (scene unloaded): hand back camera, cursor, movement.</summary>
        internal static void OnDriverDestroyed()
        {
            RrScreenCamera.Exit("the player object was destroyed");
            if (_movementTouched)
            {
                ApplyMovement(Motion, 1f, true);
                _movementTouched = false;
            }
        }

        // ---- per frame (input pump and driver) ---------------------------------------------------------------

        /// <summary>V: switch between first-person play mode and the client's third-person camera (desktop).</summary>
        internal static void ToggleThirdPerson()
        {
            if (!ThirdPerson && !RrScreenCamera.Wanted)
            {
                return;
            }

            ThirdPerson = !ThirdPerson;
            if (_thirdPersonLogs < 6)
            {
                _thirdPersonLogs++;
                RrLog.Info("Player", ThirdPerson ? "third person (V)" : "first person (V)");
            }
        }

        /// <summary>
        /// The driver's LateUpdate (order 10000). The remote-player tick runs in a finally block: it drives the
        /// isolation, and a step above that throws every frame must not leave other players hidden or audible.
        /// </summary>
        internal static void LateTick()
        {
            try
            {
                LateTickBody();
            }
            finally
            {
                RrRemotePlayers.Tick();
            }
        }

        private static void LateTickBody()
        {
            Refresh();
            bool vr = InVr;
            if (!_vrKnown || vr != _wasVr)
            {
                if (_vrKnown)
                {
                    RrLog.Info("Player", vr ? "headset on: VR mode" : "headset off: desktop mode");
                }

                _vrKnown = true;
                _wasVr = vr;
            }

            if (_sawClient)
            {
                if (_rig != null)
                {
                    RrLog.Warn("Player", "a Somnium client appeared: the editor fallback rig is removed");
                    Object.Destroy(_rig.gameObject);
                    _rig = null;
                }

                RrClientTablet.Tick(RrMain.Instance != null && RrMain.Instance.WorldRoot != null ? RrMain.Instance.WorldRoot : PlayerObject);
            }
            else if (_rig == null && _object != null && Time.unscaledTime - _initAt >= FallbackDelay)
            {
                MakeRig();
            }

            if (_movementTouched && _container != null)
            {
                // Set before the client's body existed, or the body was replaced (headset on/off): apply again.
                ISomniumPlayerMotion m = Motion;
                if (m != null && !ReferenceEquals(m, _appliedMotion))
                {
                    ApplyMovement(m, _speed, _jump);
                }
            }

            RrScreenCamera.Tick();
            RrHand l = RrHand.Left;
            RrHand r = RrHand.Right;
            if (l != null)
            {
                l.LatePose();
            }

            if (r != null)
            {
                r.LatePose();
            }
        }

        private static void MakeRig()
        {
            if (!_loggedNoClient)
            {
                _loggedNoClient = true;
                RrLog.Info("Player", "no Somnium client after " + FallbackDelay.ToString("0.0") + " s (editor): making the fallback rig");
            }

            Camera existing = _camera;
            _rig = RrFallbackRig.Create(_object.transform, existing, _hasPendingPose, _pendingFeet, _pendingYaw);
            _hasPendingPose = false;
            _rig.SpeedScale = _speed;
            _rig.JumpAllowed = _jump;
            _frame = -1;
            Refresh();
        }

        /// <summary>Re-reads the Bridge body and the view camera, at most once a frame.</summary>
        private static void Refresh()
        {
            int frame = Time.frameCount;
            if (_frame == frame)
            {
                return;
            }

            _frame = frame;
            ReadBridge();
            ReadCamera();
        }

        private static void ReadBridge()
        {
            _local = null;
            _root = _head = _left = _right = null;
            ISomniumPlayersContainer c = null;
            try
            {
                c = SomniumBridge.PlayersContainer;
            }
            catch (Exception e)
            {
                if (!_loggedBridgeError)
                {
                    _loggedBridgeError = true;
                    RrLog.Warn("Player", "SomniumBridge.PlayersContainer threw (treated as no client): " + e.Message);
                }
            }

            _container = c;
            if (c == null)
            {
                return;
            }

            if (!_sawClient)
            {
                _sawClient = true;
            }

            try
            {
                _local = c.LocalPlayer;
                ISomniumPlayerReferences refs = _local != null ? _local.References : null;
                ISomniumPlayerBody body = refs != null ? refs.Body : null;
                if (body != null)
                {
                    _root = body.Root;
                    _head = body.Head;
                    _left = body.LeftHand;
                    _right = body.RightHand;
                }
            }
            catch (Exception e)
            {
                if (!_loggedBridgeError)
                {
                    _loggedBridgeError = true;
                    RrLog.Warn("Player", "reading the local body failed (retried every frame): " + e.Message);
                }
            }

            if (!_loggedClient)
            {
                _loggedClient = true;
                RrLog.Info("Player", "Somnium client present");
            }

            if (_root != null && _root != _loggedRoot && _rootLogs < 6)
            {
                _loggedRoot = _root;
                _rootLogs++;
                RrLog.Info("Player", "local body: root '" + _root.name + "', head " + Name(_head) + ", hands " + Name(_left) + " / " + Name(_right) +
                                     " (hands are null on desktop)");
            }
        }

        private static void ReadCamera()
        {
            Camera cam = null;
            Camera main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
            {
                cam = main;
            }
            else if (_searched != null && _searched.isActiveAndEnabled && _searched.targetTexture == null)
            {
                cam = _searched;
            }
            else if (Time.unscaledTime >= _nextCameraSearch)
            {
                _nextCameraSearch = Time.unscaledTime + CameraSearchInterval;
                _searched = null;
                foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (c.isActiveAndEnabled && c.targetTexture == null && c.stereoTargetEye != StereoTargetEyeMask.None)
                    {
                        _searched = c;
                        break;
                    }
                }

                cam = _searched;
            }

            _camera = cam;
            if (cam != null && cam != _loggedCamera && _cameraLogs < 6)
            {
                _loggedCamera = cam;
                _cameraLogs++;
                RrLog.Info("Player", "view camera '" + cam.name + "'" + (cam == main ? " (Camera.main)" : " (found by search)"));
            }
        }

        private static string Name(Transform t)
        {
            return t != null ? "'" + t.name + "'" : "null";
        }

        // ---- diagnostics -------------------------------------------------------------------------------------

        /// <summary>One line of state for logs and the harness.</summary>
        public static string Describe()
        {
            Camera c = Camera;
            RrHand r = RrHand.Right;
            string hand = r == null ? "no right hand" :
                "right hand " + r.Source + (r.Tracked ? " at " + RrHand.Fmt(r.Position) : " untracked") +
                (c != null && r.Tracked ? " (view-local " + RrHand.Fmt(c.transform.InverseTransformPoint(r.Position)) + ")" : "");
            return (InVr ? "VR" : "desktop") + (HasClient ? ", client" : ", no client") + (UsingFallbackRig ? ", fallback rig" : "") +
                   ", camera " + (c != null ? "'" + c.name + "'" : "none") + ", feet " + RrHand.Fmt(FeetPosition) + ", yaw " + Yaw.ToString("0") +
                   ", play mode " + (InPlayMode ? "on" : "off") + ", id " + LocalId + ", nick '" + Nick + "', " + hand +
                   ", remotes " + RrRemotePlayers.Count;
        }

        /// <summary>
        /// Harness probe (a scenario's <c>call</c> step; never called by the world): teleports, asks for play mode,
        /// logs the screen hand, then puts everything back.
        /// </summary>
        internal static System.Collections.IEnumerator Probe()
        {
            RrLog.Info("Player", "probe start: " + Describe());
            Vector3 feet0 = FeetPosition;
            float yaw0 = Yaw;
            bool done = false;
            Teleport(new Vector3(2f, 0f, 2f), 90f, () => done = true);
            yield return null;
            yield return null;
            RrLog.Info("Player", "probe after teleport (2,0,2) yaw 90: done " + done + ", " + Describe());
            SetMovement(0f, false);
            RequestPlayMode("probe", true);
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            RrLog.Info("Player", "probe play mode requested: " + Describe());
            RequestPlayMode("probe", false);
            SetMovement(1f, true);
            yield return null;
            yield return null;
            Teleport(feet0, yaw0);
            yield return null;
            RrLog.Info("Player", "probe end: " + Describe());
        }
    }
}
