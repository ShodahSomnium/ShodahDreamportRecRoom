using System.Collections;
using System.Globalization;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's own post-processing on the room you are in: one global URP Volume that this world owns ("Rr Post",
    /// under the Rec Room object), with a runtime profile built from <see cref="RrPostFxProfiles"/>.
    ///
    /// Why a Volume of our own. In a world the Somnium client applies only its URP default profile
    /// (<c>_SomniumSpace/Scenes/PostProcessing Default Profile.asset</c>). That profile overrides every component to a
    /// neutral value: Tonemapping None, Bloom intensity 0, no colour grading, HBAO intensity 0. Its graphics settings do not
    /// reach a world's volumes for bloom ("Player is in a World, ignoring Bloom setting"), and the world scene has no
    /// Volume. Without this, Rec Room's materials show untonemapped and without their glow. The approach is the
    /// one Somnium Euro Truck proved in the client (EtsPostFx, "Ets Post"): a new GameObject on layer 0 (the client
    /// cameras' volume mask is Default only), a global Volume at priority 100, and a
    /// <c>ScriptableObject.CreateInstance&lt;VolumeProfile&gt;()</c> with stock URP components. Only the parameters Rec
    /// Room overrode are overridden, so everything else stays at the client's default.
    ///
    /// Two client details shape this class:
    /// - The client's ambient-occlusion setting writes HBAO into every global Volume it finds in a world
    ///   (<c>GraphicsControl.GetGlobalVolumes</c>: <c>FindObjectsByType&lt;Volume&gt;</c>, then
    ///   <c>volume.profile.Add&lt;HBAO&gt;</c>). <c>Volume.profile</c> silently clones the shared profile, so ours is set as
    ///   both the shared and the instance profile. The client's HBAO then lands in our profile, and our later writes
    ///   still reach the Volume. We never add, remove or change HBAO; on shutdown only our own components are
    ///   destroyed.
    /// - Nothing of the client is modified: no camera, no renderer feature, no volume of its own. Destroying "Rr Post"
    ///   returns the client to exactly its own state.
    ///
    /// The flow. Travel calls <see cref="Enter"/> once the new room is built (under the loading card): the room's
    /// profile is written and the weight rises 0 -> 1 in 0.35 s. <see cref="Leave"/> takes it back to 0 in 0.25 s when
    /// the room is left. <see cref="SetArea"/> (quest acts) crossfades the values in 1 s. Floats, colours and vectors
    /// are lerped; enums and switches change at the start, the way URP itself blends them.
    ///
    /// VR and comfort. Rec Room's 19 measured profiles contain no depth of field, motion blur, lens distortion,
    /// chromatic aberration, film grain or vignette, so there is none here, on desktop or in VR. Rec Room's VR
    /// comfort vignette (its own ComfortEffects type) is not reproduced. Bloom and colour grading are kept in both
    /// modes. The grading (white balance, lift/gamma/gain, exposure, contrast, Neutral tonemapping) is baked into URP's
    /// LUT, so it is free. Bloom is the only real cost. In a headset it runs from quarter resolution with 5 iterations
    /// instead of Rec Room's half resolution with 6: the widest mip is the same (1/64), so the glow keeps its spread
    /// at about half the fill cost.
    /// </summary>
    public sealed class RrPostFx : MonoBehaviour
    {
        public const float Priority = 100f;
        public const float EnterSeconds = 0.35f;
        public const float LeaveSeconds = 0.25f;
        public const float AreaSeconds = 1f;

        public static RrPostFx Current { get; private set; }

        /// <summary>The room whose profile is applied (None: none, the arrival room).</summary>
        public static RrActivity Activity { get; private set; }

        /// <summary>The area of that room (0: the room itself or a quest's lobby; n: act n).</summary>
        public static int Area { get; private set; }

        /// <summary>The profile being applied (or last applied while fading out).</summary>
        public static RrPostFxProfiles.Profile Profile { get; private set; }

        /// <summary>Harness before/after switch: the Volume's weight is held at 0 while true.</summary>
        public static bool Suppressed { get; private set; }

        private static int _unsupportedLogs;

        private enum Kind : byte
        {
            Float,
            Int,
            Bool,
            Color,
            Vector,
            Tone,
            Downscale
        }

        private struct SlotDef
        {
            public readonly string Component;
            public readonly string Parameter;
            public readonly Kind Kind;

            /// <summary>The client's default (= URP's constructor value) for a parameter Rec Room did not set.</summary>
            public readonly Vector4 Neutral;

            public SlotDef(string component, string parameter, Kind kind, Vector4 neutral)
            {
                Component = component;
                Parameter = parameter;
                Kind = kind;
                Neutral = neutral;
            }
        }

        private static readonly Vector4 One = new Vector4(1f, 1f, 1f, 1f);
        private static readonly Vector4 Lgg = new Vector4(1f, 1f, 1f, 0f);

        // Every parameter Rec Room's profiles override, plus the two bloom chain settings the headset changes.
        private static readonly SlotDef[] Slots =
        {
            new SlotDef("Bloom", "threshold", Kind.Float, new Vector4(0.9f, 0f, 0f, 0f)),
            new SlotDef("Bloom", "intensity", Kind.Float, Vector4.zero),
            new SlotDef("Bloom", "scatter", Kind.Float, new Vector4(0.7f, 0f, 0f, 0f)),
            new SlotDef("Bloom", "clamp", Kind.Float, new Vector4(65472f, 0f, 0f, 0f)),
            new SlotDef("Bloom", "tint", Kind.Color, One),
            new SlotDef("Bloom", "highQualityFiltering", Kind.Bool, Vector4.zero),
            new SlotDef("Bloom", "downscale", Kind.Downscale, Vector4.zero),
            new SlotDef("Bloom", "maxIterations", Kind.Int, new Vector4(6f, 0f, 0f, 0f)),
            new SlotDef("Tonemapping", "mode", Kind.Tone, Vector4.zero),
            new SlotDef("WhiteBalance", "temperature", Kind.Float, Vector4.zero),
            new SlotDef("WhiteBalance", "tint", Kind.Float, Vector4.zero),
            new SlotDef("ColorAdjustments", "postExposure", Kind.Float, Vector4.zero),
            new SlotDef("ColorAdjustments", "contrast", Kind.Float, Vector4.zero),
            new SlotDef("ColorAdjustments", "colorFilter", Kind.Color, One),
            new SlotDef("ColorAdjustments", "hueShift", Kind.Float, Vector4.zero),
            new SlotDef("ColorAdjustments", "saturation", Kind.Float, Vector4.zero),
            new SlotDef("LiftGammaGain", "lift", Kind.Vector, Lgg),
            new SlotDef("LiftGammaGain", "gamma", Kind.Vector, Lgg),
            new SlotDef("LiftGammaGain", "gain", Kind.Vector, Lgg)
        };

        private const int SlotDownscale = 6;
        private const int SlotIterations = 7;
        private const int SlotBloomIntensity = 1;
        private const int SlotTemperature = 9;
        private const int SlotExposure = 11;

        /// <summary>
        /// Every profile, darker by this many EV. Iteration 6 set -0.35 ("too bright") while the rooms were lit by the
        /// world scene's own sun (1.3) and pale-blue ambient with no shadows. Iteration 8 lights every room with Rec
        /// Room's own values (<see cref="RrEnvironment"/>: its key light, its darker trilight ambient, fog and shadows),
        /// which alone took the harness rooms 0.3-0.5 EV darker (mean luma Dorm 135 -> 105, Rec Center 107 -> 81 at the
        /// same camera), so the extra darkening would count twice: back to 0, Rec Room's own profile exposure only. The
        /// watch's Brightness slider still moves it.
        /// </summary>
        public const float BaseExposure = 0f;

        /// <summary>Every profile, warmer by this much white-balance temperature (user feedback: "it needs to be warmer").</summary>
        public const float BaseWarmth = 12f;

        /// <summary>Rec Room's bloom intensity is scaled by this (part of "too bright").</summary>
        public const float BloomScale = 0.7f;

        private static float _userExposure;
        private static float _userWarmth;

        /// <summary>The watch's Brightness slider, in EV on top of <see cref="BaseExposure"/> (-1..+1).</summary>
        public static float UserExposure
        {
            get { return _userExposure; }
            set
            {
                _userExposure = Mathf.Clamp(value, -1f, 1f);
                Retarget();
            }
        }

        /// <summary>The watch's Warmth slider, in white-balance temperature on top of <see cref="BaseWarmth"/> (-30..+30).</summary>
        public static float UserWarmth
        {
            get { return _userWarmth; }
            set
            {
                _userWarmth = Mathf.Clamp(value, -30f, 30f);
                Retarget();
            }
        }

        private static float _retargetLogAt;

        /// <summary>Re-applies the current profile with the user's offsets (a quick 0.2 s blend).</summary>
        private static void Retarget()
        {
            var fx = Current;
            if (fx == null || Profile == null)
            {
                return;
            }

            fx.Target(Profile, Player.RrPlayer.InVr, 0.2f);
            if (Time.unscaledTime >= _retargetLogAt)
            {
                // One line per half second while a slider moves: the numbers to send back after a test.
                _retargetLogAt = Time.unscaledTime + 0.5f;
                RrLog.Info("PostFx", "look from the watch: brightness " + (BaseExposure + _userExposure).ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " EV total ("
                    + _userExposure.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " from the slider), warmth +" + (BaseWarmth + _userWarmth).ToString("0", CultureInfo.InvariantCulture)
                    + " total (" + _userWarmth.ToString("+0;-0", CultureInfo.InvariantCulture) + " from the slider)");
            }
        }

        private Volume _volume;
        private VolumeProfile _profile;
        private VolumeComponent[] _own;
        private VolumeParameter[] _params;

        private readonly Vector4[] _from = new Vector4[Slots.Length];
        private readonly Vector4[] _to = new Vector4[Slots.Length];
        private readonly bool[] _fromOn = new bool[Slots.Length];
        private readonly bool[] _toOn = new bool[Slots.Length];
        private float _blend = 1f;
        private float _blendSeconds = 1f;

        private float _weight;
        private float _weightTarget;
        private float _weightSeconds = 0.3f;

        // ---- the API travel and the rooms call ----

        /// <summary>The local player has arrived in <paramref name="activity"/>: its profile (area 0) fades in.</summary>
        public static void Enter(RrActivity activity)
        {
            var fx = Ensure();
            if (fx == null)
            {
                return;
            }

            Activity = activity;
            Area = 0;
            var p = RrPostFxProfiles.For(activity, 0);
            bool vr = Player.RrPlayer.InVr;
            fx.Target(p, vr, fx._weight > 0.001f ? EnterSeconds : 0f);
            fx._weightTarget = 1f;
            fx._weightSeconds = EnterSeconds;
            fx.ApplyWeight();
            fx.DriveAo(true);
            RrLog.Info("PostFx", activity + ": " + p.Name + " (" + p.Source + "): " + p.Describe() + "; bloom chain " + (vr ? "quarter resolution x5 (VR)" : "Rec Room's half resolution x6")
                + "; global Volume priority " + Priority.ToString("0", CultureInfo.InvariantCulture) + ", layer 0, fading in over " + EnterSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s");
        }

        /// <summary>
        /// A quest's act changed (0: the lobby, n: act n): that area's profile crossfades in. Cheap when nothing
        /// changes, so a room may call it every frame. Before <see cref="Enter"/> (while a room builds) it does nothing.
        /// </summary>
        public static void SetArea(int area)
        {
            if (area == Area)
            {
                return;
            }

            Area = area;
            var fx = Current;
            if (fx == null || Activity == RrActivity.None)
            {
                return;
            }

            var p = RrPostFxProfiles.For(Activity, area);
            if (p == Profile)
            {
                RrLog.Info("PostFx", Activity + " area " + area + ": " + p.Name + " again (no change)");
                return;
            }

            fx.Target(p, Player.RrPlayer.InVr, AreaSeconds);
            RrLog.Info("PostFx", Activity + " area " + area + ": crossfading to " + p.Name + " (" + p.Source + ") over " + AreaSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s: " + p.Describe());
        }

        /// <summary>The room is being left: the Volume fades to weight 0 (the values stay, so the next room can crossfade).</summary>
        public static void Leave()
        {
            var fx = Current;
            var was = Activity;
            Activity = RrActivity.None;
            Area = 0;
            if (fx == null)
            {
                return;
            }

            fx._weightTarget = 0f;
            fx._weightSeconds = LeaveSeconds;
            fx.DriveAo(false);
            if (was != RrActivity.None)
            {
                RrLog.Info("PostFx", "left " + was + ": back to the client's own post-processing in " + LeaveSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s");
            }
        }

        /// <summary>World shutdown (RrTravel.Shutdown): destroys the Volume and its profile and clears every static.</summary>
        public static void Shutdown()
        {
            var fx = Current;
            Current = null;
            if (fx != null)
            {
                fx.DriveAo(false);
                fx.DestroyOwn();
                Destroy(fx.gameObject);
            }

            Activity = RrActivity.None;
            Area = 0;
            Profile = null;
            Suppressed = false;
            _userExposure = 0f;
            _userWarmth = 0f;
            _retargetLogAt = 0f;
            _unsupportedLogs = 0;
        }

        // ---- harness hooks ----

        /// <summary>Harness: holds the weight at 0 at once (the "before" picture: the client's default post only).</summary>
        public static void TestOff()
        {
            Suppressed = true;
            if (Current != null)
            {
                Current.ApplyWeight();
            }

            RrLog.Info("PostFx", "test: off (weight 0; " + Describe() + ")");
        }

        /// <summary>Harness: releases <see cref="TestOff"/> and finishes every blend at once (the "after" picture).</summary>
        public static void TestOn()
        {
            Suppressed = false;
            var fx = Current;
            if (fx != null)
            {
                fx._blend = 1f;
                fx.WriteBlend(1f);
                fx._weight = fx._weightTarget;
                fx.ApplyWeight();
            }

            RrLog.Info("PostFx", "test: on (" + Describe() + ")");
        }

        /// <summary>
        /// Harness: bloom intensity x8 (at least 1) for one picture, to show which surfaces are bright enough
        /// (above Rec Room's threshold) to glow at all. <see cref="TestOn"/> puts Rec Room's value back.
        /// </summary>
        public static void TestBloomProbe()
        {
            var fx = Current;
            if (fx == null || fx._params == null)
            {
                return;
            }

            float probe = Mathf.Max(1f, fx._to[1].x * 8f);
            Write(fx._params[1], Kind.Float, new Vector4(probe, 0f, 0f, 0f), true);
            RrLog.Info("PostFx", "test: bloom probe, intensity " + F(probe) + " instead of " + F(fx._to[1].x) + " (threshold " + F(fx._to[0].x) + ")");
        }

        /// <summary>Harness: the headset's bloom chain (quarter resolution x5) on the current profile, at once.</summary>
        public static void TestVrChain()
        {
            Retarget(true);
        }

        /// <summary>Harness: back to the desktop bloom chain (Rec Room's half resolution x6), at once.</summary>
        public static void TestDesktopChain()
        {
            Retarget(false);
        }

        private static void Retarget(bool vr)
        {
            var fx = Current;
            if (fx == null || Profile == null)
            {
                return;
            }

            fx.Target(Profile, vr, 0f);
            RrLog.Info("PostFx", "test: " + (vr ? "VR" : "desktop") + " bloom chain on " + Profile.Name);
        }

        /// <summary>Harness: the state and what URP resolved for the last camera it updated.</summary>
        public static void TestReport()
        {
            var sb = new StringBuilder(Describe());
            var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
            if (stack != null)
            {
                var tone = stack.GetComponent<Tonemapping>();
                var bloom = stack.GetComponent<Bloom>();
                var adjust = stack.GetComponent<ColorAdjustments>();
                var white = stack.GetComponent<WhiteBalance>();
                var lgg = stack.GetComponent<LiftGammaGain>();
                sb.Append("; resolved: tonemapping ").Append(tone != null ? tone.mode.value.ToString() : "?")
                    .Append(", bloom ").Append(bloom != null ? F(bloom.intensity.value) + " from " + F(bloom.threshold.value) + " scatter " + F(bloom.scatter.value) + " " + bloom.downscale.value + " x" + bloom.maxIterations.value : "?")
                    .Append(", exposure ").Append(adjust != null ? F(adjust.postExposure.value) + " contrast " + F(adjust.contrast.value) + " saturation " + F(adjust.saturation.value) : "?")
                    .Append(", temperature ").Append(white != null ? F(white.temperature.value) : "?")
                    .Append(", gamma ").Append(lgg != null ? lgg.gamma.value.ToString("F3") : "?");
            }

            RrLog.Info("PostFx", "test report: " + sb);
        }

        /// <summary>
        /// Harness: loads the install's own VolumeProfile (StuntRunner_Default_SSFX_Profile) through Unity's loader and
        /// compares every overridden parameter with <see cref="RrPostFxProfiles.StuntRunnerSsfx"/>. Two questions are
        /// answered at once: does a Rec Room VolumeProfile bind to the stock URP types in 6000.3, and was the data read right.
        /// </summary>
        public static IEnumerator TestBundleProfile()
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                RrLog.Warn("PostFx", "bundle profile test: no RrAssets");
                yield break;
            }

            VolumeProfile loaded = null;
            const string name = "StuntRunner_Default_SSFX_Profile";
            yield return assets.LoadRoutine<VolumeProfile>(name, 114, p => loaded = p);
            if (loaded == null)
            {
                RrLog.Warn("PostFx", "bundle profile test: " + name + " did not load as a VolumeProfile");
                yield break;
            }

            var sb = new StringBuilder();
            int missing = 0;
            foreach (var c in loaded.components)
            {
                if (c == null)
                {
                    missing++;
                    continue;
                }

                sb.Append(c.GetType().Name).Append(c.active ? "" : " (inactive)").Append(' ');
            }

            var want = RrPostFxProfiles.StuntRunnerSsfx;
            int same = 0;
            int diff = 0;
            var diffs = new StringBuilder();
            for (int i = 0; i < Slots.Length; i++)
            {
                var param = ParamOf(loaded, i);
                Vector4 v;
                bool inData = want.TryGet(Slots[i].Component, Slots[i].Parameter, out v);
                bool inBundle = param != null && param.overrideState;
                if (!inData && !inBundle)
                {
                    continue;
                }

                Vector4 got = inBundle ? Read(param, Slots[i].Kind) : Vector4.zero;
                if (inData == inBundle && (got - v).sqrMagnitude < 1e-8f)
                {
                    same++;
                }
                else
                {
                    diff++;
                    diffs.Append(' ').Append(Slots[i].Component).Append('.').Append(Slots[i].Parameter).Append(" data ").Append(inData ? v.ToString("F4") : "-").Append(" bundle ").Append(inBundle ? got.ToString("F4") : "-");
                }
            }

            RrLog.Info("PostFx", "bundle profile test: " + name + " loaded, " + loaded.components.Count + " components (" + sb.ToString().Trim() + (missing > 0 ? ", " + missing + " unbound" : "") + "); "
                + same + " overridden parameters match the data, " + diff + " differ" + diffs + (diff == 0 && missing == 0 ? " - PASS" : " - FAIL"));
        }

        // ---- building and blending ----

        private static RrPostFx Ensure()
        {
            if (Current != null)
            {
                return Current;
            }

            var go = new GameObject("Rr Post") { layer = 0 };
            var main = RrMain.Instance;
            if (main != null)
            {
                go.transform.SetParent(main.transform, false);
            }

            var fx = go.AddComponent<RrPostFx>();
            fx.Build();
            RrLog.Info("PostFx", "built the \"Rr Post\" Volume (global, priority " + Priority.ToString("0", CultureInfo.InvariantCulture) + ", layer 0, its own runtime profile; "
                + "the client's default profile has no tonemapping and no bloom, so Rec Room's grading is ours to add)");
            return fx;
        }

        private void Build()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Rr Post (Rec Room)";
            var bloom = _profile.Add<Bloom>(false);
            var tone = _profile.Add<Tonemapping>(false);
            var white = _profile.Add<WhiteBalance>(false);
            var adjust = _profile.Add<ColorAdjustments>(false);
            var lgg = _profile.Add<LiftGammaGain>(false);
            _own = new VolumeComponent[] { bloom, tone, white, adjust, lgg };
            _params = new VolumeParameter[Slots.Length];
            for (int i = 0; i < Slots.Length; i++)
            {
                _params[i] = ParamOf(_profile, i);
                _from[i] = _to[i] = Slots[i].Neutral;
            }

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = Priority;
            _volume.weight = 0f;
            _volume.sharedProfile = _profile;
            _volume.profile = _profile;
            Current = this;
        }

        /// <summary>Starts a value blend from what is shown now to <paramref name="p"/> (0 s: at once).</summary>
        private void Target(RrPostFxProfiles.Profile p, bool vr, float seconds)
        {
            // Where the running blend is now becomes the start.
            float k = Smooth(_blend);
            for (int i = 0; i < Slots.Length; i++)
            {
                _from[i] = Mix(i, _from[i], _to[i], k, _fromOn[i], _toOn[i]);
                _fromOn[i] = k >= 1f ? _toOn[i] : _fromOn[i] || _toOn[i];
                _to[i] = Slots[i].Neutral;
                _toOn[i] = false;
            }

            foreach (var v in p.Values)
            {
                int slot = SlotOf(v.Component, v.Parameter);
                if (slot < 0)
                {
                    if (_unsupportedLogs++ < 8)
                    {
                        RrLog.Info("PostFx", p.Name + ": " + v.Component + "." + v.Parameter + " is not applied (not one of the parameters this world sets)");
                    }

                    continue;
                }

                _to[slot] = v.V;
                _toOn[slot] = true;
            }

            // The user's correction after trying iteration 5 in the client ("too bright, it needs to be warmer"): our
            // rebuilt rooms have no baked lighting, so Rec Room's own values read brighter and cooler than Rec Room did.
            // A base offset for every profile, plus the live Brightness/Warmth sliders on the watch's Settings page.
            _to[SlotExposure].x += BaseExposure + UserExposure;
            _toOn[SlotExposure] = true;
            _to[SlotTemperature].x = Mathf.Clamp(_to[SlotTemperature].x + BaseWarmth + UserWarmth, -100f, 100f);
            _toOn[SlotTemperature] = true;
            _to[SlotBloomIntensity].x *= BloomScale;

            if (vr)
            {
                _to[SlotDownscale] = new Vector4((float)BloomDownscaleMode.Quarter, 0f, 0f, 0f);
                _toOn[SlotDownscale] = true;
                _to[SlotIterations] = new Vector4(5f, 0f, 0f, 0f);
                _toOn[SlotIterations] = true;
            }

            Profile = p;
            _blendSeconds = Mathf.Max(0.0001f, seconds);
            _blend = seconds > 0f ? 0f : 1f;
            WriteBlend(Smooth(_blend));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_blend < 1f)
            {
                _blend = Mathf.Min(1f, _blend + dt / _blendSeconds);
                WriteBlend(Smooth(_blend));
            }

            if (_weight != _weightTarget)
            {
                _weight = Mathf.MoveTowards(_weight, _weightTarget, dt / Mathf.Max(0.0001f, _weightSeconds));
                ApplyWeight();
            }
        }

        private void ApplyWeight()
        {
            if (_volume != null)
            {
                _volume.weight = Suppressed ? 0f : Smooth(_weight);
            }
        }

        private void WriteBlend(float k)
        {
            if (_params == null)
            {
                return;
            }

            for (int i = 0; i < Slots.Length; i++)
            {
                var param = _params[i];
                if (param == null)
                {
                    continue;
                }

                bool on = k >= 1f ? _toOn[i] : _fromOn[i] || _toOn[i];
                Write(param, Slots[i].Kind, Mix(i, _from[i], _to[i], k, _fromOn[i], _toOn[i]), on);
            }
        }

        /// <summary>The value at blend k: floats, colours and vectors lerp; enums, integers and switches take the target at once.</summary>
        private static Vector4 Mix(int slot, Vector4 from, Vector4 to, float k, bool fromOn, bool toOn)
        {
            if (!fromOn)
            {
                from = Slots[slot].Neutral;
            }

            if (!toOn)
            {
                to = Slots[slot].Neutral;
            }

            switch (Slots[slot].Kind)
            {
                case Kind.Float:
                case Kind.Color:
                case Kind.Vector:
                    return Vector4.Lerp(from, to, k);
                default:
                    return k > 0f ? to : from;
            }
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static int SlotOf(string component, string parameter)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Component == component && Slots[i].Parameter == parameter)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The URP parameter of slot i in a profile (ours or one loaded from the install), or null.</summary>
        private static VolumeParameter ParamOf(VolumeProfile p, int i)
        {
            if (p == null)
            {
                return null;
            }

            Bloom b;
            Tonemapping t;
            WhiteBalance w;
            ColorAdjustments c;
            LiftGammaGain l;
            switch (i)
            {
                case 0: return p.TryGet(out b) ? b.threshold : null;
                case 1: return p.TryGet(out b) ? b.intensity : null;
                case 2: return p.TryGet(out b) ? b.scatter : null;
                case 3: return p.TryGet(out b) ? b.clamp : null;
                case 4: return p.TryGet(out b) ? b.tint : null;
                case 5: return p.TryGet(out b) ? b.highQualityFiltering : null;
                case 6: return p.TryGet(out b) ? b.downscale : null;
                case 7: return p.TryGet(out b) ? b.maxIterations : null;
                case 8: return p.TryGet(out t) ? t.mode : null;
                case 9: return p.TryGet(out w) ? w.temperature : null;
                case 10: return p.TryGet(out w) ? w.tint : null;
                case 11: return p.TryGet(out c) ? c.postExposure : null;
                case 12: return p.TryGet(out c) ? c.contrast : null;
                case 13: return p.TryGet(out c) ? c.colorFilter : null;
                case 14: return p.TryGet(out c) ? c.hueShift : null;
                case 15: return p.TryGet(out c) ? c.saturation : null;
                case 16: return p.TryGet(out l) ? l.lift : null;
                case 17: return p.TryGet(out l) ? l.gamma : null;
                case 18: return p.TryGet(out l) ? l.gain : null;
                default: return null;
            }
        }

        private static void Write(VolumeParameter param, Kind kind, Vector4 v, bool on)
        {
            switch (kind)
            {
                case Kind.Float:
                    ((VolumeParameter<float>)param).value = v.x;
                    break;
                case Kind.Int:
                    ((VolumeParameter<int>)param).value = Mathf.RoundToInt(v.x);
                    break;
                case Kind.Bool:
                    ((VolumeParameter<bool>)param).value = v.x > 0.5f;
                    break;
                case Kind.Color:
                    ((VolumeParameter<Color>)param).value = new Color(v.x, v.y, v.z, v.w);
                    break;
                case Kind.Vector:
                    ((VolumeParameter<Vector4>)param).value = v;
                    break;
                case Kind.Tone:
                    ((VolumeParameter<TonemappingMode>)param).value = (TonemappingMode)Mathf.RoundToInt(v.x);
                    break;
                case Kind.Downscale:
                    ((VolumeParameter<BloomDownscaleMode>)param).value = (BloomDownscaleMode)Mathf.RoundToInt(v.x);
                    break;
            }

            param.overrideState = on;
        }

        private static Vector4 Read(VolumeParameter param, Kind kind)
        {
            switch (kind)
            {
                case Kind.Float:
                    return new Vector4(((VolumeParameter<float>)param).value, 0f, 0f, 0f);
                case Kind.Int:
                    return new Vector4(((VolumeParameter<int>)param).value, 0f, 0f, 0f);
                case Kind.Bool:
                    return new Vector4(((VolumeParameter<bool>)param).value ? 1f : 0f, 0f, 0f, 0f);
                case Kind.Color:
                    var c = ((VolumeParameter<Color>)param).value;
                    return new Vector4(c.r, c.g, c.b, c.a);
                case Kind.Vector:
                    return ((VolumeParameter<Vector4>)param).value;
                case Kind.Tone:
                    return new Vector4((int)((VolumeParameter<TonemappingMode>)param).value, 0f, 0f, 0f);
                case Kind.Downscale:
                    return new Vector4((int)((VolumeParameter<BloomDownscaleMode>)param).value, 0f, 0f, 0f);
                default:
                    return Vector4.zero;
            }
        }

        private static string Describe()
        {
            var fx = Current;
            if (fx == null)
            {
                return "no Volume";
            }

            int foreign = 0;
            if (fx._profile != null)
            {
                foreach (var c in fx._profile.components)
                {
                    if (c != null && System.Array.IndexOf(fx._own, c) < 0)
                    {
                        foreign++;
                    }
                }
            }

            return Activity + " area " + Area + ", " + (Profile != null ? Profile.Name : "no profile") + ", weight " + F(fx._volume != null ? fx._volume.weight : 0f)
                + (Suppressed ? " (suppressed)" : "") + ", blend " + F(fx._blend) + (foreign > 0 ? ", " + foreign + " component(s) added by the client (HBAO)" : "");
        }

        private static string F(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // ---- ambient occlusion: the client's HBAO in our profile (iteration 9) ----

        /// <summary>
        /// Rec Room's shaders were built for URP's screen-space ambient occlusion (every AG/Standard variant has
        /// _SCREEN_SPACE_OCCLUSION): soft contact shadows in corners, under furniture and where walls meet floors. The
        /// client's own AO is HBAO (HorizonBasedAmbientOcclusion.Universal.HBAO), which GraphicsControl writes into every
        /// global Volume when it applies its settings (after the world scene loads and on every settings change), adding
        /// the component where it is missing. Its renderer feature is off unless the player turns ambient occlusion on
        /// (every quality preset has it off), so this can only shape AO the player asked for.
        ///
        /// While a room is shown: radius <see cref="AoRadius"/> m and intensity at least <see cref="AoIntensity"/>, through
        /// the component's public parameter list (the HBAO type is the client's, so it is found by its type name and its
        /// two parameters by their fixed ranges: radius 0.25-5, intensity 0-4, in declaration order). The client's own
        /// values come back when the room is left; a player who has intensity 0 keeps no AO; the next change of the AO
        /// setting in the client wins until the next room.
        /// </summary>
        public const float AoRadius = 0.6f;

        public const float AoIntensity = 1f;

        private VolumeComponent _ao;
        private ClampedFloatParameter _aoRadius, _aoIntensity;
        private bool _aoSaved, _aoRadiusOn, _aoIntensityOn, _aoMissingLogged;
        private float _aoRadiusWas, _aoIntensityWas;

        /// <summary>
        /// World start (RrMain.Awake): the Volume exists before the client applies its graphics settings to the world, so
        /// the client's HBAO lands in our profile (it looks for global Volumes only then and on later settings changes).
        /// Weight 0, nothing of Rec Room in it until <see cref="Enter"/>.
        /// </summary>
        public static void Prepare()
        {
            if (Current == null)
            {
                Ensure();
            }
        }

        private VolumeComponent FindAo()
        {
            if (_profile == null)
            {
                return null;
            }

            foreach (var c in _profile.components)
            {
                if (c != null && c.GetType().Name == "HBAO")
                {
                    return c;
                }
            }

            return null;
        }

        private void DriveAo(bool on)
        {
            var ao = FindAo();
            if (ao == null)
            {
                if (on && !_aoMissingLogged)
                {
                    _aoMissingLogged = true;
                    RrLog.Info("PostFx", "AO: the client's HBAO is not in our profile (editor/harness, or the client has not applied its graphics settings since the Volume was made): nothing to shape");
                }

                return;
            }

            if (!ReferenceEquals(ao, _ao))
            {
                _ao = ao;
                _aoRadius = null;
                _aoIntensity = null;
                _aoSaved = false;
                foreach (var p in ao.parameters)
                {
                    var c = p as ClampedFloatParameter;
                    if (c == null)
                    {
                        continue;
                    }

                    if (_aoRadius == null && Mathf.Approximately(c.min, 0.25f) && Mathf.Approximately(c.max, 5f))
                    {
                        _aoRadius = c;
                    }
                    else if (_aoRadius != null && _aoIntensity == null && Mathf.Approximately(c.min, 0f) && Mathf.Approximately(c.max, 4f))
                    {
                        _aoIntensity = c;
                    }
                }

                if (_aoRadius == null || _aoIntensity == null)
                {
                    RrLog.Warn("PostFx", "AO: the client's HBAO has " + ao.parameters.Count + " parameters but not the radius (0.25-5) and intensity (0-4) this world knows: left alone");
                    return;
                }
            }

            if (_aoRadius == null || _aoIntensity == null)
            {
                return;
            }

            if (on)
            {
                if (!_aoSaved)
                {
                    _aoRadiusWas = _aoRadius.value;
                    _aoIntensityWas = _aoIntensity.value;
                    _aoRadiusOn = _aoRadius.overrideState;
                    _aoIntensityOn = _aoIntensity.overrideState;
                    _aoSaved = true;
                }

                if (_aoIntensityWas <= 0f)
                {
                    RrLog.Info("PostFx", "AO: the client's HBAO intensity is 0 (the player's choice): left off");
                    return;
                }

                _aoRadius.value = AoRadius;
                _aoRadius.overrideState = true;
                _aoIntensity.value = Mathf.Max(AoIntensity, _aoIntensityWas);
                _aoIntensity.overrideState = true;
                RrLog.Info("PostFx", "AO: the client's HBAO shaped like Rec Room's contact shadows: radius " + F(_aoRadiusWas) + " -> " + F(AoRadius) + " m, intensity "
                    + F(_aoIntensityWas) + " -> " + F(_aoIntensity.value) + " (drawn only when ambient occlusion is on in the client's graphics settings)");
            }
            else if (_aoSaved)
            {
                _aoRadius.value = _aoRadiusWas;
                _aoRadius.overrideState = _aoRadiusOn;
                _aoIntensity.value = _aoIntensityWas;
                _aoIntensity.overrideState = _aoIntensityOn;
                _aoSaved = false;
            }
        }

        private void DestroyOwn()
        {
            // Only what this class made: a component the client added to our profile (HBAO) is the client's.
            if (_own != null)
            {
                foreach (var c in _own)
                {
                    if (c != null)
                    {
                        Destroy(c);
                    }
                }

                _own = null;
            }

            if (_profile != null)
            {
                Destroy(_profile);
                _profile = null;
            }

            _params = null;
        }

        private void OnDestroy()
        {
            DestroyOwn();
            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
