using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// Desktop "play mode": Rec Room's first-person screen mode on top of the Somnium client (somnium_player.md §5.4 A,
    /// §9.4), driven from the player driver's LateUpdate at execution order 10000 - after the client's Cinemachine
    /// brain (order 100) and every other LateUpdate, like Quake's <c>Q3CameraControl</c>.
    ///
    /// While it is on:
    /// - the client's camera is ours: its <c>CinemachineBrain</c> (found by name, so no Cinemachine reference - it is not
    ///   on the server's list) is paused, as ETS's truck camera does; the view sits at the Bridge head
    ///   + 0.12 m forward + 0.02 m up (Quake: in front of the avatar's face, never inside it), with our own mouse look
    ///   (sensitivity 2, pitch ±89) and FOV 60 x <see cref="RrPlayer.FovKick"/> (the client's 40 is a zoomed
    ///   third-person lens; GUESS, §10.4);
    /// - the body's yaw is written every LateUpdate, so WASD walks where the player looks and the client's Q/E rotation
    ///   (applied in its Update) is undone - which frees Q and E for drop and grab;
    /// - the cursor is locked every frame (Quake's fallback: the client's own cursor API is obfuscated) and a crosshair
    ///   shows at the centre.
    ///
    /// It is suspended - camera, FOV and cursor handed back - whenever a menu has the input (<see cref="RrInput.Blocked"/>),
    /// the window loses focus, or a key that opens a client UI is pressed (Escape, Tab, I, R, Enter). It resumes on the
    /// next left click that is not on UI, and that click is swallowed (it is not a shot). It is wanted while a game
    /// asks for it (<see cref="RrPlayer.RequestPlayMode"/>) or while the screen hand holds something
    /// (<see cref="RrPlayer.PlayModeWhileHolding"/>), never in VR (conventions §24 item 15: never set the camera in a
    /// headset), and not while the player chose third person (V). With the editor fallback rig nothing is taken over:
    /// the rig looks and locks by itself when <see cref="Active"/>.
    /// </summary>
    internal static class RrScreenCamera
    {
        public const float Fov = 60f;
        public const float Near = 0.05f;
        public const float Sensitivity = 2f;

        /// <summary>Play mode was suspended; the next left click off UI resumes it (set by the pump, read here).</summary>
        internal static bool WaitingForClick;
        internal static bool ResumeRequested;

        /// <summary>We hold the client's camera right now.</summary>
        internal static bool On { get; private set; }

        /// <summary>Play mode is in effect this frame (camera held, or the fallback rig in mouse-look mode).</summary>
        internal static bool Active { get; private set; }

        internal static float Yaw
        {
            get { return _yaw; }
        }

        private static readonly List<string> Requests = new List<string>();
        private static Camera _cam;
        private static Behaviour _brain;
        private static float _fovWas = -1f;
        private static float _nearWas;
        private static float _yaw;
        private static float _pitch;
        private static bool _axisFailed;
        private static int _enters;
        private static int _exits;

        internal static void Request(string owner, bool on)
        {
            if (string.IsNullOrEmpty(owner))
            {
                owner = "game";
            }

            int i = Requests.IndexOf(owner);
            if (on && i < 0)
            {
                Requests.Add(owner);
            }
            else if (!on && i >= 0)
            {
                Requests.RemoveAt(i);
            }
        }

        internal static bool Wanted
        {
            get
            {
                if (RrPlayer.InVr || RrPlayer.ThirdPerson)
                {
                    return false;
                }

                if (Requests.Count > 0)
                {
                    return true;
                }

                RrHand right = RrHand.Right;
                return RrPlayer.PlayModeWhileHolding && right != null && right.Held != null;
            }
        }

        /// <summary>A teleport turned the player: the view keeps the new heading instead of writing the old one back.</summary>
        internal static void SetYaw(float yaw)
        {
            _yaw = yaw;
            _pitch = 0f;
        }

        internal static void Tick()
        {
            bool want = Wanted;
            bool usable = !RrInput.Blocked && Application.isFocused;
            if (RrPlayer.UsingFallbackRig)
            {
                if (On)
                {
                    Exit("the editor fallback rig drives the view");
                }

                Active = want && usable;
                WaitingForClick = false;
                ResumeRequested = false;
                RrCrosshair.Show(Active);
                return;
            }

            if (!want)
            {
                if (On)
                {
                    Exit(RrPlayer.ThirdPerson ? "third person chosen" : "nothing needs it");
                }

                WaitingForClick = false;
                ResumeRequested = false;
                Active = false;
                RrCrosshair.Show(false);
                return;
            }

            if (On)
            {
                string why = !Application.isFocused ? "window lost focus" : RrInput.Blocked ? "input taken by " + RrInput.BlockedBy : ClientUiKey();
                if (why != null)
                {
                    Exit(why);
                    WaitingForClick = true;
                    ResumeRequested = false;
                }
            }

            if (!On)
            {
                if (!usable || (WaitingForClick && !ResumeRequested) || !Enter())
                {
                    Active = false;
                    RrCrosshair.Show(false);
                    return;
                }

                WaitingForClick = false;
                ResumeRequested = false;
            }

            Drive();
            Active = On;
            RrCrosshair.Show(Active);
        }

        private static string ClientUiKey()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                return "Escape (client menu)";
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                return "Tab (client tablet)";
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                return "I (client inspector)";
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                return "R (client pop-up menu)";
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                return "Enter (client chat)";
            }

            return null;
        }

        private static bool Enter()
        {
            Camera cam = RrPlayer.Camera;
            if (cam == null)
            {
                return false;
            }

            _cam = cam;
            _brain = cam.GetComponent("CinemachineBrain") as Behaviour;
            if (_brain != null && _brain.enabled)
            {
                _brain.enabled = false;
            }
            else
            {
                _brain = null;
            }

            _fovWas = cam.fieldOfView;
            _nearWas = cam.nearClipPlane;
            Vector3 e = cam.transform.rotation.eulerAngles;
            _yaw = e.y;
            _pitch = Mathf.Clamp(e.x > 180f ? e.x - 360f : e.x, -89f, 89f);
            On = true;
            _enters++;
            if (_enters <= 3)
            {
                RrLog.Info("Screen", "play mode on (#" + _enters + "): camera '" + cam.name + "'" + (_brain != null ? " (Cinemachine brain paused)" : " (no brain)") +
                                     ", FOV " + _fovWas.ToString("0") + " -> " + (Fov * RrPlayer.FovKick).ToString("0") + ", near " + _nearWas.ToString("0.###") + " -> " + Near +
                                     ", body yaw written: " + (RrPlayer.Root != null ? "'" + RrPlayer.Root.name + "'" : "no body") + ", cursor locked");
            }

            return true;
        }

        private static void Drive()
        {
            if (_cam == null || !_cam.isActiveAndEnabled || _cam != RrPlayer.Camera)
            {
                Exit("the camera changed");
                if (!Enter())
                {
                    return;
                }
            }

            if (_brain != null && _brain.enabled)
            {
                _brain.enabled = false;
            }

            float dx = 0f;
            float dy = 0f;
            if (!_axisFailed)
            {
                try
                {
                    dx = Input.GetAxisRaw("Mouse X");
                    dy = Input.GetAxisRaw("Mouse Y");
                }
                catch (ArgumentException e)
                {
                    _axisFailed = true;
                    RrLog.Warn("Screen", "no 'Mouse X/Y' axes in this Input Manager - mouse look off: " + e.Message);
                }
            }

            _yaw = Mathf.Repeat(_yaw + dx * Sensitivity, 360f);
            _pitch = Mathf.Clamp(_pitch - dy * Sensitivity, -89f, 89f);
            Transform root = RrPlayer.Root;
            if (root != null)
            {
                root.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }

            Quaternion view = Quaternion.Euler(_pitch, _yaw, 0f);
            Transform head = RrPlayer.HeadAnchor;
            Vector3 eye;
            if (head != null)
            {
                eye = head.position + view * (Vector3.forward * 0.12f) + Vector3.up * 0.02f;
            }
            else if (root != null)
            {
                eye = root.position + Vector3.up * RrFallbackRig.EyeHeight + view * (Vector3.forward * 0.12f);
            }
            else
            {
                eye = _cam.transform.position;
            }

            _cam.transform.SetPositionAndRotation(eye, view);
            _cam.fieldOfView = Fov * RrPlayer.FovKick;
            _cam.nearClipPlane = Near;
            if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>Hands the camera, lens and cursor back exactly as they were.</summary>
        internal static void Exit(string why)
        {
            if (!On)
            {
                return;
            }

            On = false;
            Active = false;
            if (_brain != null && !_brain.enabled)
            {
                _brain.enabled = true;
            }

            _brain = null;
            if (_cam != null && _fovWas > 0f)
            {
                _cam.fieldOfView = _fovWas;
                _cam.nearClipPlane = _nearWas;
            }

            _cam = null;
            _fovWas = -1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RrCrosshair.Show(false);
            _exits++;
            if (_exits <= 3)
            {
                RrLog.Info("Screen", "play mode off (#" + _exits + "): " + why + "; camera, lens and cursor handed back");
            }
        }

        internal static void Reset()
        {
            Exit("world shut down");
            Requests.Clear();
            WaitingForClick = false;
            ResumeRequested = false;
            Active = false;
            _axisFailed = false;
            _enters = 0;
            _exits = 0;
            _yaw = 0f;
            _pitch = 0f;
            RrCrosshair.Reset();
        }
    }
}
