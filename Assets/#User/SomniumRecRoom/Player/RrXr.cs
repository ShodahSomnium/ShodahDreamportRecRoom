using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// The headset and the two controllers read through UnityEngine.XR (InputDevices + CommonUsages), the way ETS
    /// (<c>EtsVr.Poll</c>) and Quake (<c>Q3Vr</c>) read them: the Input System package is not on the upload server's
    /// reference list (conventions §5, §11.2), and the legacy XR API compiles against the modules it does list.
    ///
    /// Why each rule is here:
    /// - Devices are looked up again once a second while one is missing: the client starts OpenXR before the world
    ///   and controllers connect late or reconnect (conventions §24 item 42: never cache devices for good).
    /// - The grip is read analog first with <c>gripButton</c> as a floor, and the trigger likewise with
    ///   <c>triggerButton</c>: runtimes report one or the other (ETS <c>EtsVr.Grip</c>; AoM lost the trigger by
    ///   reading only the button, somnium_player.md §4.1).
    /// - Poses from InputDevices are in tracking space. The client initialises OpenXR first
    ///   (<c>XRGeneralSettings</c> loader order OpenXR, OpenVR), so <c>devicePosition/deviceRotation</c> is the
    ///   OpenXR <b>grip</b> pose. It is brought into the world with the HMD pair, exactly as Quake, AoM and ETS did
    ///   (somnium_player.md §2.2): <c>toWorld = camera.localToWorldMatrix * TRS(hmdPos, hmdRot, 1).inverse</c>, which
    ///   carries the client's whole rig (XROrigin, floor offset, posture offsets) without knowing its layout.
    /// </summary>
    public static class RrXr
    {
        private const float SearchInterval = 1f;

        private static InputDevice _left;
        private static InputDevice _right;
        private static InputDevice _hmd;
        private static float _nextSearch;
        private static bool _reported;
        private static readonly List<InputDevice> Found = new List<InputDevice>();

        private static int _toWorldFrame = -1;
        private static bool _toWorldOk;
        private static Matrix4x4 _toWorld = Matrix4x4.identity;

        /// <summary>Whether a headset is actually running (the siblings' test: an active device with a name).</summary>
        public static bool Present
        {
            get { return XRSettings.isDeviceActive && !string.IsNullOrEmpty(XRSettings.loadedDeviceName); }
        }

        public static InputDevice Controller(RrHandSide side)
        {
            return side == RrHandSide.Left ? _left : _right;
        }

        public static InputDevice Headset
        {
            get { return _hmd; }
        }

        /// <summary>Once per frame (the input pump): finds missing devices again, at most once a second.</summary>
        internal static void Poll()
        {
            if (!Present)
            {
                return;
            }

            if ((!_left.isValid || !_right.isValid || !_hmd.isValid) && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + SearchInterval;
                if (!_left.isValid)
                {
                    _left = First(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left);
                }

                if (!_right.isValid)
                {
                    _right = First(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right);
                }

                if (!_hmd.isValid)
                {
                    _hmd = First(InputDeviceCharacteristics.HeadMounted);
                }

                if (!_reported && _left.isValid && _right.isValid)
                {
                    _reported = true;
                    RrLog.Info("Player", "VR on '" + XRSettings.loadedDeviceName + "': headset " + (_hmd.isValid ? "'" + _hmd.name + "'" : "(not found)") +
                                         "; left " + Describe(_left) + "; right " + Describe(_right));
                }
            }
        }

        /// <summary>Trigger pull 0..1 (0 below 0.05), the button counting as a full pull.</summary>
        public static float Trigger(RrHandSide side)
        {
            InputDevice d = Controller(side);
            if (!d.isValid)
            {
                return 0f;
            }

            float value = d.TryGetFeatureValue(CommonUsages.trigger, out float pulled) ? pulled : 0f;
            if (d.TryGetFeatureValue(CommonUsages.triggerButton, out bool down) && down)
            {
                value = Mathf.Max(value, 1f);
            }

            return value > 0.05f ? value : 0f;
        }

        /// <summary>Grip squeeze 0..1, the button counting as a full squeeze.</summary>
        public static float Grip(RrHandSide side)
        {
            InputDevice d = Controller(side);
            if (!d.isValid)
            {
                return 0f;
            }

            float value = d.TryGetFeatureValue(CommonUsages.grip, out float squeezed) ? squeezed : 0f;
            if (d.TryGetFeatureValue(CommonUsages.gripButton, out bool down) && down)
            {
                value = Mathf.Max(value, 1f);
            }

            return value;
        }

        /// <summary>A (right) / X (left). Both belong to the client (jump; inspector) - read, never bind.</summary>
        public static bool Primary(RrHandSide side)
        {
            InputDevice d = Controller(side);
            return d.isValid && d.TryGetFeatureValue(CommonUsages.primaryButton, out bool down) && down;
        }

        /// <summary>
        /// Tracking space to world for this frame (cached per frame). False when there is no camera or the headset
        /// gives no pose; the matrix is then the camera parent's (a rough stand-in) or identity.
        /// </summary>
        public static bool TryGetTrackingToWorld(out Matrix4x4 toWorld)
        {
            int frame = Time.frameCount;
            if (_toWorldFrame != frame)
            {
                _toWorldFrame = frame;
                _toWorldOk = ComputeToWorld(out _toWorld);
            }

            toWorld = _toWorld;
            return _toWorldOk;
        }

        private static bool ComputeToWorld(out Matrix4x4 m)
        {
            m = Matrix4x4.identity;
            Camera cam = RrPlayer.Camera;
            if (cam == null)
            {
                return false;
            }

            Transform t = cam.transform;
            if (_hmd.isValid)
            {
                // The camera's TrackedPoseDriver follows the centre eye; devicePosition is Quake's choice and the same
                // pose on OpenXR. Either pair gives the tracking origin when inverted against the camera.
                Vector3 p;
                Quaternion r;
                bool havePos = _hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out p) || _hmd.TryGetFeatureValue(CommonUsages.devicePosition, out p);
                bool haveRot = _hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out r) || _hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out r);
                if (havePos && haveRot && Mathf.Abs(r.w * r.w + r.x * r.x + r.y * r.y + r.z * r.z - 1f) < 0.01f)
                {
                    m = t.localToWorldMatrix * Matrix4x4.TRS(p, r, Vector3.one).inverse;
                    return true;
                }
            }

            Transform parent = t.parent;
            if (parent != null)
            {
                m = parent.localToWorldMatrix;
            }

            return false;
        }

        /// <summary>
        /// The controller's OpenXR grip pose in world space (converted with <see cref="TryGetTrackingToWorld"/>).
        /// False when the controller is missing, untracked, or the headset pose is unknown.
        /// </summary>
        public static bool TryGetGripPose(RrHandSide side, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            InputDevice d = Controller(side);
            if (!d.isValid)
            {
                return false;
            }

            if (d.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && !tracked)
            {
                return false;
            }

            if (!d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p) || !d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion r))
            {
                return false;
            }

            if (!TryGetTrackingToWorld(out Matrix4x4 toWorld))
            {
                return false;
            }

            position = toWorld.MultiplyPoint3x4(p);
            rotation = toWorld.rotation * r;
            return true;
        }

        /// <summary>A short buzz where the runtime offers impulses; harmless where it does not.</summary>
        public static void Pulse(RrHandSide side, float amplitude, float seconds)
        {
            InputDevice d = Controller(side);
            if (d.isValid && d.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                d.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0f, seconds));
            }
        }

        /// <summary>What a controller reports, for the log (which features exist cannot be reasoned out).</summary>
        public static string Describe(InputDevice d)
        {
            if (!d.isValid)
            {
                return "(no controller)";
            }

            bool grip = d.TryGetFeatureValue(CommonUsages.grip, out float _);
            bool gripButton = d.TryGetFeatureValue(CommonUsages.gripButton, out bool _);
            bool trigger = d.TryGetFeatureValue(CommonUsages.trigger, out float _);
            bool triggerButton = d.TryGetFeatureValue(CommonUsages.triggerButton, out bool _);
            bool pose = d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 _);
            bool haptics = d.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse;
            return "'" + d.name + "' grip " + (grip ? "yes" : "NO") + "/button " + (gripButton ? "yes" : "NO") +
                   ", trigger " + (trigger ? "yes" : "NO") + "/button " + (triggerButton ? "yes" : "NO") +
                   ", pose " + (pose ? "yes" : "NO") + ", haptics " + (haptics ? "yes" : "NO");
        }

        private static InputDevice First(InputDeviceCharacteristics wanted)
        {
            Found.Clear();
            InputDevices.GetDevicesWithCharacteristics(wanted, Found);
            InputDevice d = Found.Count > 0 ? Found[0] : default(InputDevice);
            Found.Clear();
            return d;
        }

        /// <summary>Statics survive a world re-entry: cleared by RrPlayer.Shutdown.</summary>
        internal static void Reset()
        {
            _left = default(InputDevice);
            _right = default(InputDevice);
            _hmd = default(InputDevice);
            _nextSearch = 0f;
            _reported = false;
            _toWorldFrame = -1;
            _toWorldOk = false;
            _toWorld = Matrix4x4.identity;
        }
    }
}
