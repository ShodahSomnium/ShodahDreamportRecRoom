using System;
using System.Collections.Generic;
using System.Globalization;
using SomniumSpace.Bridge;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's own lighting in the room you are in: its key light as the sun (with its soft shadows), its trilight
    /// ambient, its fog, its reflection strength and its sky, applied when a room is entered and put back exactly when
    /// the world goes. One "Rr Environment" object under the Rec Room object owns it.
    ///
    /// <b>What Rec Room had</b> (DevNotes/iter8/env.md, measured on the 61 Android levels with apkscene env): no
    /// lightmaps anywhere and every light realtime. A room is its key light (soft shadows, strength 0.84-1.0, the only
    /// shadow caster outdoors), 5-145 point and spot lights of which 1-3 big spots also cast shadows, a trilight
    /// ambient, linear fog to the horizon colour, and a baked sky cubemap or Unity's procedural sky. The quest areas
    /// have no sun at all: an almost black ambient (Golden Trophy's lobby 0.015 sky, 0.22/0.11/0.09 equator) lit by
    /// torches. So these numbers reproduce the look without anything baked, which is why iteration 7's rooms, lit by
    /// the world scene's own bright sun and pale-blue ambient with every shadow off, looked flat and too bright.
    ///
    /// <b>Where the numbers come from.</b> <see cref="RrEnvironmentTable"/> (69 scenes, generated) holds each level's
    /// values by scene name. A room's default is looked up by its activity (<see cref="KeyFor"/>); every scene the
    /// kit builds registers itself (<see cref="SceneBuilt"/>), and the scene the player's head is in wins over the
    /// default, so a Paintball map, a quest act or the Armory switch the lighting without the room code doing
    /// anything. A scene the table does not know is lit from its own E and L records (<see cref="RrEnvironmentSetting.FromScene"/>).
    ///
    /// <b>How the client lights a world</b> (the client's source, read only): the Bridge environment is a thin wrapper
    /// over RenderSettings (SetSkybox, SetSunSource = RenderSettings.sun, UpdateEnvironment = DynamicGI), nothing else
    /// in the client writes RenderSettings, and the loader makes the world scene active only after the world's code has
    /// started (MAIN_TO_WORLD: SetActiveScene after the load), so what is set before that lands in the loader scene
    /// (EtsSky's measured note). This class therefore sets RenderSettings and re-applies on activeSceneChanged. URP's
    /// main light is RenderSettings.sun; the client's URP asset has soft main-light shadows, 4 cascades and a user
    /// shadow distance of 50-250 m (default 100), which this class leaves alone. Only proven Bridge members are called
    /// (SetSkybox, SetSunSource, UpdateEnvironment).
    ///
    /// <b>The sun.</b> The world scene's own directional light (RrSceneSetup's, RenderSettings.sun of our scene) is
    /// turned, coloured and given Rec Room's shadow strength, or switched off for a room without a key light; if the
    /// scene has none, "Rr Sun" is made under this object. Its saved state comes back on <see cref="Shutdown"/>.
    ///
    /// <b>The sky.</b> Rec Room's own sky shader is in the install (AG/Skybox/Simple Gradient, carried by the
    /// ComfortModeSkybox material); a runtime copy gets the room's six colours. For the quests' Unity procedural sky the
    /// client's Skybox/Procedural is used with the level's own parameters when the client has that shader, else the
    /// gradient computed from the same parameters. The baked cubemaps themselves are not in the PC install.
    ///
    /// <b>Fog.</b> The world scene is saved with fog off, so the fog variants of shaders built into the world bundle
    /// are stripped; Rec Room's shaders come from the install with their own FOG_LINEAR/EXP/EXP2 variants (keywords
    /// of AG/Standard-Opaque) and do fog. So fog is applied (<see cref="UseFog"/>): Rec Room's materials fog exactly,
    /// our few URP fallback materials stay unfogged.
    /// </summary>
    public sealed class RrEnvironment : MonoBehaviour
    {
        public const string Area = "Env";

        /// <summary>How often the player's position is checked against the registered scenes (seconds).</summary>
        public const float PickInterval = 0.25f;

        /// <summary>A scene's area is its placed records' bounds grown by this (metres).</summary>
        public const float AreaMargin = 3f;

        public static RrEnvironment Instance { get; private set; }

        /// <summary>The setting applied now (null: the world scene's own, the arrival room).</summary>
        public static RrEnvironmentSetting Active { get; private set; }

        /// <summary>The room entered (None in the arrival room).</summary>
        public static RrActivity Activity { get; private set; }

        /// <summary>Apply Rec Room's fog (see the class notes).</summary>
        public static bool UseFog = true;

        /// <summary>Render one realtime reflection probe per area once it is applied (Rec Room's baked "Env" probe).</summary>
        public static bool UseReflectionProbe = true;

        /// <summary>Harness "before": the world scene's own settings, as iteration 7 had them.</summary>
        public static bool Suppressed { get; private set; }

        /// <summary>Harness: a key that wins over the position pick (null = automatic).</summary>
        public static string ForcedKey;

        private sealed class SceneEntry
        {
            public RrSceneKitRoot Root;
            public string Name;
            public RrEnvironmentSetting Setting;
            public Bounds Local;       // in the kit root's space
        }

        private sealed class Saved
        {
            public Material Sky;
            public AmbientMode Mode;
            public Color AmbientLight, Sky3, Equator, Ground;
            public float AmbientIntensity;
            public bool Fog;
            public FogMode FogMode;
            public Color FogColor;
            public float FogDensity, FogStart, FogEnd;
            public DefaultReflectionMode ReflectionMode;
            public float Reflection;
            public Light SunRef;
            public bool SunEnabled;
            public Quaternion SunRotation;
            public Color SunColor;
            public float SunIntensity;
            public LightShadows SunShadows;
            public float SunStrength;
        }

        private static readonly List<SceneEntry> Scenes = new List<SceneEntry>();
        private static Dictionary<string, RrEnvironmentSetting> _table;
        private static readonly HashSet<string> Logged = new HashSet<string>();

        private Saved _saved;
        private Light _sun;
        private bool _sunOwned;
        private Material _gradient;
        private Material _procedural;
        private bool _proceduralLooked;
        private bool _gradientRequested;
        private Material _roomSky;                 // a room's own sky material (the cached Rec Center's)
        private Light _roomSunSource;              // a room's own key light to copy (the cached Rec Center's)
        private RrEnvironmentSetting _default;
        private SceneEntry _activeEntry;
        private float _nextPick;
        private ReflectionProbe _probe;
        private GameObject _rig;
        private RrEnvironmentSetting _rigFor;
        private string _probeKey;
        private int _applies;

        // ---- the API travel, the kit and the rooms call ----

        /// <summary>Travel: the room of <paramref name="activity"/> is built and entered (under the loading card).</summary>
        public static void Enter(RrActivity activity)
        {
            var env = Ensure();
            if (env == null)
            {
                return;
            }

            Activity = activity;
            string key = KeyFor(activity);
            env._default = key != null ? Find(key) : null;
            env._nextPick = 0f;
            env.Pick(true);
            RrLog.Info(Area, activity + ": default " + (env._default != null ? env._default.Key : "none (the world scene's own)") + ", " + Scenes.Count + " kit scene(s) registered; applied " + (Active != null ? Active.Describe() : "the world scene's own"));
        }

        /// <summary>Travel: the room is being left: the world scene's own settings come back (the arrival room).</summary>
        public static void Leave()
        {
            var env = Instance;
            var was = Activity;
            Activity = RrActivity.None;
            _area = null;
            if (env == null)
            {
                return;
            }

            env._default = null;
            env._roomSky = null;
            env._roomSunSource = null;
            env._activeEntry = null;
            if (env._rig != null)
            {
                Destroy(env._rig);
            }

            env._rig = null;
            env._rigFor = null;
            env.RestoreSaved();
            Active = null;
            env.DropProbe();
            if (was != RrActivity.None)
            {
                RrLog.Info(Area, "left " + was + ": the world scene's own lighting is back");
            }
        }

        /// <summary>World shutdown (RrTravel.Shutdown): everything back as saved, own objects destroyed, statics cleared.</summary>
        public static void Shutdown()
        {
            var env = Instance;
            if (env != null)
            {
                env.RestoreSaved();
                env.DropProbe();
                if (env._sunOwned && env._sun != null)
                {
                    Destroy(env._sun.gameObject);
                }

                if (env._gradient != null)
                {
                    Destroy(env._gradient);
                }

                if (env._procedural != null)
                {
                    Destroy(env._procedural);
                }

                Destroy(env.gameObject);
            }

            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Instance = null;
            Active = null;
            Activity = RrActivity.None;
            Suppressed = false;
            ForcedKey = null;
            _area = null;
            UseFog = true;
            UseReflectionProbe = true;
            Scenes.Clear();
            Logged.Clear();
            _table = null;
        }

        /// <summary>
        /// A room that shows one area at a time (Golden Trophy: lobby, Armory, acts) names it: that scene's lighting
        /// applies whatever the head position says, until <c>SetArea(null)</c> (back to the automatic pick) or the room
        /// is left. The key is the RRSK scene name (GoldenTrophy_Foyer, GoldenTrophy_Armory, GoldenTrophy_Hallway1...);
        /// a registered kit scene of that name supplies the frame (its root's rotation turns the key light). Cheap when
        /// nothing changes, so it may be called every frame.
        /// </summary>
        public static void SetArea(string key)
        {
            if (string.Equals(_area, key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _area = string.IsNullOrEmpty(key) ? null : key;
            var env = Instance;
            RrLog.Info(Area, "area " + (_area ?? "automatic (the scene the player is in)") + (Find(_area) == null && _area != null ? " (not in the table: its scene records, when built)" : ""));
            if (env != null && Activity != RrActivity.None)
            {
                env._nextPick = 0f;
                env.Pick(false);
            }
        }

        private static string _area;

        /// <summary>The table's line for a scene name (case-insensitive), or null.</summary>
        public static RrEnvironmentSetting Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (_table == null)
            {
                _table = new Dictionary<string, RrEnvironmentSetting>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in RrEnvironmentTable.Lines)
                {
                    var s = RrEnvironmentSetting.Parse(line);
                    if (s.Key.Length > 0)
                    {
                        _table[s.Key] = s;
                    }
                }

                RrLog.Info(Area, "Rec Room lighting table: " + _table.Count + " scenes");
            }

            RrEnvironmentSetting found;
            return _table.TryGetValue(key, out found) ? found : null;
        }

        /// <summary>The scene whose lighting a room uses before any kit scene claims the player (null: the world's own).</summary>
        public static string KeyFor(RrActivity a)
        {
            switch (a)
            {
                case RrActivity.Dorm: return "Dorm_Builtin";
                case RrActivity.RecCenter: return "RecCenter_071825";
                case RrActivity.Paintball: return "Paintball_Lobby";
                case RrActivity.GoldenTrophy: return "GoldenLobby";
                case RrActivity.Dodgeball: return "Dodgeball";
                case RrActivity.Paddleball: return "Paddleball";
                case RrActivity.Charades: return "Charades";
                case RrActivity.Soccer: return "Soccer";
                case RrActivity.Bowling: return "Bowling";
                case RrActivity.DiscGolf: return "DiscGolf";
                case RrActivity.LaserTag: return "LaserTag_Hangar";
                case RrActivity.StuntRunner: return "StuntRunner_Lobby";
                case RrActivity.RecRally: return "RecRally_Chaparral";
                case RrActivity.LostSkulls: return "Isle_Foyer";
                case RrActivity.Jumbotron: return "Jumbotron_Foyer";
                case RrActivity.CrimsonCauldron: return "Crimson_Foyer";
                case RrActivity.Crescendo: return "Crescendo_Foyer";
                default: return null;
            }
        }

        /// <summary>
        /// The kit built a scene (RrSceneKit, end of the build): it takes over the lighting while the player is inside
        /// <paramref name="localBounds"/> (the placed records' bounds, in the kit root's space).
        /// </summary>
        internal static void SceneBuilt(RrSceneKitRoot root, RrSceneData data, Bounds localBounds)
        {
            if (root == null || data == null)
            {
                return;
            }

            var setting = Find(data.Name) ?? RrEnvironmentSetting.FromScene(data);
            Scenes.RemoveAll(e => e.Root == null || e.Root == root);
            localBounds.Expand(AreaMargin * 2f);
            Scenes.Add(new SceneEntry { Root = root, Name = data.Name, Setting = setting, Local = localBounds });
            if (Logged.Add("scene:" + data.Name))
            {
                RrLog.Info(Area, "scene " + data.Name + " lights itself while the player is inside it: " + setting.Describe());
            }

            var env = Instance;
            if (env != null)
            {
                env._nextPick = 0f;
            }
        }

        /// <summary>The kit root is going (RrSceneKitRoot.OnDestroy).</summary>
        internal static void SceneGone(RrSceneKitRoot root)
        {
            Scenes.RemoveAll(e => e.Root == null || e.Root == root);
            var env = Instance;
            if (env != null && env._activeEntry != null && env._activeEntry.Root == root)
            {
                env._activeEntry = null;
                env._nextPick = 0f;
            }
        }

        /// <summary>The setting the kit should light a scene with (its lights, their shadows): table, else derived.</summary>
        public static RrEnvironmentSetting ForScene(RrSceneData data)
        {
            return data == null ? null : Find(data.Name) ?? RrEnvironmentSetting.FromScene(data);
        }

        /// <summary>
        /// A room's own key light (the cached Rec Center's KeyLight): the sun takes its rotation, colour, intensity and
        /// shadow settings while the room's default applies, and the room's light is switched off (one sun).
        /// </summary>
        public static void UseSunFrom(Light source)
        {
            var env = Ensure();
            if (env == null || source == null)
            {
                return;
            }

            env._roomSunSource = source;
            RrLog.Info(Area, "the room's own key light " + source.name + " becomes the sun: colour " + source.color + ", intensity " + source.intensity.ToString("0.##", CultureInfo.InvariantCulture)
                + ", rotation " + source.transform.eulerAngles.ToString("F0") + ", shadows " + source.shadows + " " + source.shadowStrength.ToString("0.##", CultureInfo.InvariantCulture));
            source.enabled = false;
            env._nextPick = 0f;
            if (Active != null)
            {
                env.Apply(Active, env._activeEntry, true);
            }
        }

        /// <summary>A room's own sky material (the cached Rec Center's, when its shader can draw here).</summary>
        public static void UseSky(Material sky)
        {
            var env = Ensure();
            if (env == null || sky == null)
            {
                return;
            }

            env._roomSky = sky;
            if (Active != null)
            {
                env.Apply(Active, env._activeEntry, true);
            }
        }

        /// <summary>Harness before/after: hold the world scene's own settings (true) or Rec Room's (false).</summary>
        public static void Suppress(bool on)
        {
            Suppressed = on;
            var env = Instance;
            if (env == null)
            {
                return;
            }

            if (on)
            {
                env.RestoreSaved();
                env.DropProbe();
            }
            else if (Active != null)
            {
                env.Apply(Active, env._activeEntry, true);
            }
            else
            {
                env._nextPick = 0f;
                env.Pick(true);
            }
        }

        /// <summary>The world's sun (the scene's directional light or ours), for diagnostics.</summary>
        public static Light Sun
        {
            get { return Instance != null ? Instance._sun : null; }
        }

        // ---- the object ----

        private static RrEnvironment Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var main = RrMain.Instance;
            var go = new GameObject("Rr Environment");
            if (main != null)
            {
                go.transform.SetParent(main.transform, false);
            }

            Instance = go.AddComponent<RrEnvironment>();
            Instance.FindSun();
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Instance.RequestGradient();
            return Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
                Instance = null;
            }
        }

        private static void OnActiveSceneChanged(Scene from, Scene to)
        {
            var env = Instance;
            if (env == null)
            {
                return;
            }

            // Unity keeps render settings per scene: the newly active scene brings its own. Ours are set on it again, but
            // only on our own world scene (a client scene made active while the world goes must keep its own).
            var main = RrMain.Instance;
            if (main == null || to != main.gameObject.scene)
            {
                return;
            }

            env._saved = null;
            if (Active != null && !Suppressed)
            {
                env.Capture();
                env.Apply(Active, env._activeEntry, true);
                RrLog.Info(Area, "active scene is now '" + to.name + "': Rec Room's lighting set on it again (" + Active.Key + ")");
            }
        }

        private void Update()
        {
            if (Activity == RrActivity.None || Suppressed)
            {
                return;
            }

            if (Time.unscaledTime >= _nextPick)
            {
                Pick(false);
            }
        }

        /// <summary>Which setting applies at the player's head: a forced key, the smallest scene containing it, the default.</summary>
        private void Pick(bool force)
        {
            _nextPick = Time.unscaledTime + PickInterval;
            RrEnvironmentSetting want = null;
            SceneEntry entry = null;
            string named = !string.IsNullOrEmpty(ForcedKey) ? ForcedKey : _area;
            if (!string.IsNullOrEmpty(named))
            {
                foreach (var e in Scenes)
                {
                    if (e.Root != null && string.Equals(e.Name, named, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = e;
                        want = e.Setting;
                    }
                }

                want = want ?? Find(named);
            }
            else
            {
                Vector3 head;
                try
                {
                    head = Player.RrPlayer.HeadPosition;
                }
                catch (Exception)
                {
                    head = Vector3.zero;
                }

                float best = float.MaxValue;
                for (int i = Scenes.Count - 1; i >= 0; i--)
                {
                    var e = Scenes[i];
                    if (e.Root == null)
                    {
                        Scenes.RemoveAt(i);
                        continue;
                    }

                    // A scene the room has hidden (Paintball shows the lobby or the map, not both) does not claim the player.
                    if (!e.Root.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Vector3 local = e.Root.transform.InverseTransformPoint(head);
                    if (!e.Local.Contains(local))
                    {
                        continue;
                    }

                    float volume = e.Local.size.x * e.Local.size.y * e.Local.size.z;
                    if (volume < best)
                    {
                        best = volume;
                        entry = e;
                        want = e.Setting;
                    }
                }
            }

            if (want == null)
            {
                want = _default;
                entry = null;
            }

            if (!force && want == Active && entry == _activeEntry)
            {
                return;
            }

            _activeEntry = entry;
            if (want == null)
            {
                RestoreSaved();
                Active = null;
                DropProbe();
                return;
            }

            if (Suppressed)
            {
                Active = want;
                return;
            }

            Apply(want, entry, force);
        }

        // ---- applying ----

        private void Apply(RrEnvironmentSetting s, SceneEntry entry, bool force)
        {
            if (s == null)
            {
                return;
            }

            if (_saved == null)
            {
                Capture();
            }

            bool changed = Active != s;
            Active = s;
            _applies++;
            Quaternion frame = entry != null && entry.Root != null ? entry.Root.transform.rotation : Quaternion.identity;

            // Ambient: Rec Room's trilight (the ambient probe follows the colours at once for a gradient source).
            if (s.HasAmbient)
            {
                RenderSettings.ambientMode = s.AmbientMode;
                RenderSettings.ambientSkyColor = s.AmbientSky;
                RenderSettings.ambientEquatorColor = s.AmbientEquator;
                RenderSettings.ambientGroundColor = s.AmbientGround;
                RenderSettings.ambientLight = s.AmbientSky;
                RenderSettings.ambientIntensity = s.AmbientMode == AmbientMode.Skybox ? Mathf.Max(0.01f, s.AmbientIntensity) : 1f;
            }

            // Fog.
            RenderSettings.fog = UseFog && s.Fog;
            if (RenderSettings.fog)
            {
                RenderSettings.fogMode = s.FogMode;
                RenderSettings.fogColor = s.FogColor;
                RenderSettings.fogDensity = s.FogDensity;
                RenderSettings.fogStartDistance = s.FogStart;
                RenderSettings.fogEndDistance = s.FogEnd;
            }

            // Reflections from the sky (Rec Room used a baked environment cubemap at this strength).
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = ReflectionOf(s);

            // The sun.
            ApplySun(s, frame);

            // The sky.
            var sky = SkyFor(s);
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                try
                {
                    SomniumBridge.Environment?.SetSkybox(sky);
                }
                catch (Exception)
                {
                    // editor / no client: RenderSettings already has it
                }
            }

            try
            {
                if (SomniumBridge.Environment != null)
                {
                    SomniumBridge.Environment.UpdateEnvironment();
                }
                else
                {
                    DynamicGI.UpdateEnvironment();
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce(Area, "updateenv", "updating the environment lighting failed: " + e.Message);
            }

            if (changed || force)
            {
                if (Logged.Add("apply:" + s.Key) || force)
                {
                    RrLog.Info(Area, "applied " + s.Describe() + (sky != null ? "; sky material " + sky.shader.name : "; sky unchanged (no sky material yet)"));
                }

                RenderProbe(s, entry);
            }

            if (entry == null && s == _default)
            {
                BuildRig(s);
            }
        }

        private void ApplySun(RrEnvironmentSetting s, Quaternion frame)
        {
            if (_sun == null)
            {
                FindSun();
            }

            if (_sun == null)
            {
                return;
            }

            bool useRoomLight = _roomSunSource != null && IsDefault(s);
            if (useRoomLight)
            {
                _sun.transform.rotation = _roomSunSource.transform.rotation;
                _sun.color = _roomSunSource.color;
                _sun.intensity = _roomSunSource.intensity;
                _sun.shadows = LightShadows.Soft;
                _sun.shadowStrength = s.HasSun ? s.SunShadowStrength : Mathf.Clamp01(_roomSunSource.shadowStrength);
                _sun.enabled = true;
            }
            else if (s.HasSun)
            {
                _sun.transform.rotation = frame * s.SunRotation;
                _sun.color = s.SunColor;
                _sun.intensity = s.SunIntensity;
                _sun.shadows = s.SunShadows;
                _sun.shadowStrength = s.SunShadowStrength;
                _sun.enabled = true;
            }
            else
            {
                // A room without a key light (the quests, Charades): Rec Room lit it with its own lights only.
                _sun.enabled = false;
            }

            RenderSettings.sun = _sun;
            try
            {
                SomniumBridge.Environment?.SetSunSource(_sun);
            }
            catch (Exception)
            {
                // editor / no client
            }
        }

        private bool IsDefault(RrEnvironmentSetting s)
        {
            return s == _default;
        }

        private Material SkyFor(RrEnvironmentSetting s)
        {
            if (_roomSky != null && s == _default)
            {
                return _roomSky;
            }

            if (s.HasProcedural)
            {
                var p = Procedural();
                if (p != null)
                {
                    p.SetColor("_SkyTint", s.ProcTint);
                    p.SetColor("_GroundColor", s.ProcGround);
                    p.SetFloat("_AtmosphereThickness", s.ProcThickness);
                    p.SetFloat("_Exposure", s.ProcExposure);
                    p.SetFloat("_SunSize", s.ProcSunSize);
                    p.SetFloat("_SunSizeConvergence", s.ProcConvergence);
                    p.SetFloat("_SunDisk", s.ProcSunDisk);
                    p.DisableKeyword("_SUNDISK_NONE");
                    p.DisableKeyword("_SUNDISK_SIMPLE");
                    p.DisableKeyword("_SUNDISK_HIGH_QUALITY");
                    p.EnableKeyword(s.ProcSunDisk == 0 ? "_SUNDISK_NONE" : s.ProcSunDisk == 1 ? "_SUNDISK_SIMPLE" : "_SUNDISK_HIGH_QUALITY");
                    return p;
                }
            }

            if (s.HasGradient && _gradient != null)
            {
                RrEnvironmentSky.Set(_gradient, s);
                return _gradient;
            }

            return null;
        }

        /// <summary>
        /// The reflection strength to set. A level that reflected its sky (not a baked capture) and whose sky is black
        /// (the quests' procedural sky at exposure 0.01: every gradient colour under 0.03) reflected nothing, so nothing
        /// is reflected here either; harness run 4 showed why it matters: with the default reflection left bright, the
        /// quest floors and chair legs came out grey-white in a hall lit only by torches.
        /// </summary>
        public static float ReflectionOf(RrEnvironmentSetting s)
        {
            float r = Mathf.Clamp01(s.Reflection);
            if (!s.ReflectionCustom && s.HasGradient)
            {
                float sky = 0f;
                for (int i = 0; i < 3; i++)
                {
                    sky = Mathf.Max(sky, s.Gradient[i].maxColorComponent);
                }

                if (sky < 0.06f)
                {
                    r = 0f;
                }
            }

            return r;
        }

        /// <summary>Unity's procedural sky, when the client has the shader (it is not in the install).</summary>
        private Material Procedural()
        {
            if (_procedural != null || _proceduralLooked)
            {
                return _procedural;
            }

            _proceduralLooked = true;
            var shader = Shader.Find("Skybox/Procedural");
            if (shader != null && shader.isSupported)
            {
                _procedural = new Material(shader) { name = "Rr Sky (procedural)" };
            }

            RrLog.Info(Area, "Skybox/Procedural " + (_procedural != null ? "is available: the quests' procedural skies draw with their own parameters" : "is not in this build: the quests' skies use the gradient computed from their parameters"));
            return _procedural;
        }

        /// <summary>Loads the install's ComfortModeSkybox (AG/Skybox/Simple Gradient) once, as shared (scope 0).</summary>
        private void RequestGradient()
        {
            if (_gradientRequested)
            {
                return;
            }

            var assets = RrAssets.Instance;
            if (assets == null)
            {
                return;
            }

            _gradientRequested = true;
            int scope = assets.Scope;
            assets.Scope = 0;
            try
            {
                assets.Load<Material>(RrEnvironmentSky.InstallMaterial, 21, m =>
                {
                    if (m == null)
                    {
                        RrLog.Warn(Area, "the install's sky material " + RrEnvironmentSky.InstallMaterial + " did not load: skies stay the world's own");
                        return;
                    }

                    if (this == null)
                    {
                        return;
                    }

                    _gradient = new Material(m) { name = "Rr Sky (Rec Room gradient)" };
                    RrLog.Info(Area, "sky material ready: a copy of the install's " + m.name + " (" + m.shader.name + ", supported " + m.shader.isSupported + ")");
                    if (Active != null && !Suppressed)
                    {
                        Apply(Active, _activeEntry, true);
                    }
                });
            }
            finally
            {
                assets.Scope = scope;
            }
        }

        private void FindSun()
        {
            var main = RrMain.Instance;
            Scene ours = main != null ? main.gameObject.scene : SceneManager.GetActiveScene();
            Light found = null;
            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional && RenderSettings.sun.gameObject.scene == ours)
            {
                found = RenderSettings.sun;
            }

            if (found == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (l != null && l.type == LightType.Directional && l.gameObject.scene == ours && (found == null || l.name == "Directional Light"))
                    {
                        found = l;
                    }
                }
            }

            if (found == null)
            {
                var go = new GameObject("Rr Sun");
                go.transform.SetParent(transform, false);
                found = go.AddComponent<Light>();
                found.type = LightType.Directional;
                found.color = new Color(1f, 0.957f, 0.894f);
                found.intensity = 1.3f;
                found.shadows = LightShadows.Soft;
                found.shadowStrength = 0.7f;
                found.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                _sunOwned = true;
            }

            _sun = found;
            RrLog.Info(Area, "the sun is " + (_sunOwned ? "our own \"Rr Sun\" (the world scene has no directional light)" : "the world scene's '" + found.name + "'") + ": colour " + found.color + ", intensity " + found.intensity.ToString("0.##", CultureInfo.InvariantCulture) + ", shadows " + found.shadows);
        }

        private void Capture()
        {
            var s = new Saved
            {
                Sky = RenderSettings.skybox,
                Mode = RenderSettings.ambientMode,
                AmbientLight = RenderSettings.ambientLight,
                Sky3 = RenderSettings.ambientSkyColor,
                Equator = RenderSettings.ambientEquatorColor,
                Ground = RenderSettings.ambientGroundColor,
                AmbientIntensity = RenderSettings.ambientIntensity,
                Fog = RenderSettings.fog,
                FogMode = RenderSettings.fogMode,
                FogColor = RenderSettings.fogColor,
                FogDensity = RenderSettings.fogDensity,
                FogStart = RenderSettings.fogStartDistance,
                FogEnd = RenderSettings.fogEndDistance,
                ReflectionMode = RenderSettings.defaultReflectionMode,
                Reflection = RenderSettings.reflectionIntensity,
                SunRef = RenderSettings.sun
            };
            if (_sun != null)
            {
                s.SunEnabled = _sun.enabled;
                s.SunRotation = _sun.transform.rotation;
                s.SunColor = _sun.color;
                s.SunIntensity = _sun.intensity;
                s.SunShadows = _sun.shadows;
                s.SunStrength = _sun.shadowStrength;
            }

            _saved = s;
        }

        private void RestoreSaved()
        {
            var s = _saved;
            if (s == null)
            {
                return;
            }

            RenderSettings.skybox = s.Sky;
            RenderSettings.ambientMode = s.Mode;
            RenderSettings.ambientLight = s.AmbientLight;
            RenderSettings.ambientSkyColor = s.Sky3;
            RenderSettings.ambientEquatorColor = s.Equator;
            RenderSettings.ambientGroundColor = s.Ground;
            RenderSettings.ambientIntensity = s.AmbientIntensity;
            RenderSettings.fog = s.Fog;
            RenderSettings.fogMode = s.FogMode;
            RenderSettings.fogColor = s.FogColor;
            RenderSettings.fogDensity = s.FogDensity;
            RenderSettings.fogStartDistance = s.FogStart;
            RenderSettings.fogEndDistance = s.FogEnd;
            RenderSettings.defaultReflectionMode = s.ReflectionMode;
            RenderSettings.reflectionIntensity = s.Reflection;
            RenderSettings.sun = s.SunRef;
            if (_sun != null)
            {
                _sun.enabled = s.SunEnabled;
                _sun.transform.rotation = s.SunRotation;
                _sun.color = s.SunColor;
                _sun.intensity = s.SunIntensity;
                _sun.shadows = s.SunShadows;
                _sun.shadowStrength = s.SunStrength;
            }

            try
            {
                DynamicGI.UpdateEnvironment();
            }
            catch (Exception)
            {
                // nothing to update
            }
        }

        // ---- the light rig of a room the kit does not build ----

        /// <summary>Activities whose room stands in the Android level's own frame, so its lights can be placed as they were.</summary>
        public static bool RigFor(RrActivity a)
        {
            // The Dorm: RrDormRoom snaps Rec Room's Maker-Pen dorm pieces back into the original dorm's coordinates (floor
            // top y 0, interior x -4.94..4.77, z -2.96..2.96), and the level's 8 lights sit inside that interior (Room Fill
            // (0.23, 2.33, 0.49), Lamp Bounce (2.88, 0.85, -2.45), the Key Light spot through the +x window). The other
            // non-kit rooms are Dreamport rebuilds in frames of their own.
            return a == RrActivity.Dorm;
        }

        private void BuildRig(RrEnvironmentSetting s)
        {
            if (_rig != null && _rigFor == s)
            {
                return;
            }

            if (_rig != null)
            {
                Destroy(_rig);
                _rig = null;
                _rigFor = null;
            }

            var room = RrTravel.CurrentRoom;
            if (!RigFor(Activity) || s.Rig.Count == 0 || room == null)
            {
                return;
            }

            // Under the room: it goes with the room.
            _rig = new GameObject("Rr Light Rig (" + s.Key + ")");
            _rig.transform.SetParent(room.transform, false);
            _rigFor = s;
            int shadows = 0;
            var sources = new List<RrLightGrid.Source>();
            foreach (var r in s.Rig)
            {
                var go = new GameObject("Rig " + r.Type);
                go.transform.SetParent(_rig.transform, false);
                go.transform.localPosition = r.Position;
                go.transform.localRotation = r.Rotation;
                var l = go.AddComponent<Light>();
                l.type = r.Type;
                l.color = r.Color;
                l.intensity = r.Intensity;
                l.range = Mathf.Max(0.1f, r.Range);
                if (r.Type == LightType.Spot)
                {
                    l.spotAngle = Mathf.Clamp(r.SpotAngle, 1f, 179f);
                }

                // Iteration 9: no shadow maps for the twins: the room's Rec Room surfaces take this light from the grid, which
                // cannot use URP's additional-light shadows (RrLightGrid); the sun keeps its own.
                l.shadows = LightShadows.None;
                if (l.shadows != LightShadows.None)
                {
                    shadows++;
                    l.shadowNearPlane = 0.1f;
                }

                // Iteration 9: the same light in Rec Room's own light grid for the room's Rec Room shaders (the maker-pen
                // dorm pieces), with its URPLight values; the Unity light for avatars and URP-shaded objects.
                var src = new RrLightGrid.Source
                {
                    Name = "Rig " + r.Type,
                    Type = r.Type,
                    Position = r.Position,
                    Rotation = r.Rotation,
                    Color = r.Color,
                    Intensity = r.Intensity,
                    Range = Mathf.Max(0.1f, r.Range),
                    SpotAngle = r.SpotAngle,
                    InnerSpotAngle = -1f,
                    Specular = 1f,
                    HemiOuter = 180f,
                    Unity = l
                };
                RrEnvironmentSetting.UrpLight u;
                if (s.FindUrp(r.Position, out u))
                {
                    src.Specular = u.Specular;
                    src.Hemisphere = u.Hemisphere;
                    src.HemiInner = u.HemiInner;
                    src.HemiOuter = u.HemiOuter > 0f ? u.HemiOuter : 180f;
                    src.InnerSpotAngle = u.InnerSpot;
                }

                RrLightGrid.ConfigureUnity(l, src);
                sources.Add(src);
            }

            var grid = RrLightGrid.Attach(_rig, room.transform, s.Key, default(Bounds), sources);
            RrLog.Info(Area, s.Key + ": the level's own " + s.Rig.Count + " point/spot lights placed in the room (" + shadows + " with soft shadows)"
                + (grid != null ? ", also in Rec Room's light grid for the room's Rec Room shaders" : ""));
        }

        // ---- the reflection probe ----

        /// <summary>
        /// One realtime probe over the area, rendered once (faces spread over frames), Environment layer only: the
        /// room's own surroundings in shiny materials, as Rec Room's baked "Reflection_Probe_Env_Baked" cubemaps gave.
        /// </summary>
        private void RenderProbe(RrEnvironmentSetting s, SceneEntry entry)
        {
            // Only where Rec Room reflected a baked capture of the room (its custom "Env" cubemap): a quest area reflected its
            // black procedural sky, and a probe there would fill the floors with the fog colour it captures.
            if (!UseReflectionProbe || s.Reflection <= 0.01f || !s.ReflectionCustom)
            {
                DropProbe();
                return;
            }

            string key = s.Key + "#" + (entry != null && entry.Root != null ? entry.Root.GetInstanceID() : 0);
            if (_probe != null && _probeKey == key)
            {
                return;
            }

            Vector3 centre;
            Vector3 size;
            if (entry != null && entry.Root != null)
            {
                centre = entry.Root.transform.TransformPoint(entry.Local.center);
                size = Vector3.Min(entry.Local.size, new Vector3(400f, 200f, 400f));
            }
            else
            {
                var room = RrTravel.CurrentRoom;
                centre = room != null ? room.Origin + Vector3.up * 1.6f : Vector3.up * 1.6f;
                size = new Vector3(200f, 80f, 200f);
            }

            Vector3 at;
            try
            {
                at = Player.RrPlayer.HeadPosition;
            }
            catch (Exception)
            {
                at = centre;
            }

            if (_probe == null)
            {
                var go = new GameObject("Rr Reflection Probe");
                go.transform.SetParent(transform, false);
                _probe = go.AddComponent<ReflectionProbe>();
                _probe.mode = ReflectionProbeMode.Realtime;
                _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                _probe.resolution = 128;
                _probe.hdr = true;
                _probe.boxProjection = false;
                _probe.cullingMask = 1 << RrLayers.Environment;
                _probe.clearFlags = ReflectionProbeClearFlags.Skybox;
                _probe.nearClipPlane = 0.3f;
                _probe.farClipPlane = 500f;
                _probe.importance = 1;
            }

            // From the head when it is inside the area (what the player sees), else from the area's centre.
            if (!new Bounds(centre, size).Contains(at))
            {
                at = centre;
            }

            _probe.transform.position = at;
            _probe.center = centre - at;
            _probe.size = size;
            _probe.intensity = Mathf.Clamp01(s.Reflection);
            _probe.RenderProbe();
            _probeKey = key;
            RrLog.Info(Area, s.Key + ": realtime reflection probe rendered once at " + at.ToString("F1") + " over " + size.ToString("F0") + " m (Environment layer, 128 px, faces over frames), intensity " + _probe.intensity.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void DropProbe()
        {
            if (_probe != null)
            {
                Destroy(_probe.gameObject);
            }

            _probe = null;
            _probeKey = null;
        }

        /// <summary>A registered scene's area as a world box (harness shots).</summary>
        /// <summary>The kit root of a registered scene (its local space is the scene's own frame), or null.</summary>
        public static Transform SceneRoot(string key)
        {
            foreach (var e in Scenes)
            {
                if (e.Root != null && string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return e.Root.transform;
                }
            }

            return null;
        }

        public static bool SceneWorldBounds(string key, out Bounds world)
        {
            world = default(Bounds);
            foreach (var e in Scenes)
            {
                if (e.Root == null || !string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var t = e.Root.transform;
                var c = e.Local.center;
                var x = e.Local.extents;
                world = new Bounds(t.TransformPoint(c), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    world.Encapsulate(t.TransformPoint(c + new Vector3((i & 1) == 0 ? -x.x : x.x, (i & 2) == 0 ? -x.y : x.y, (i & 4) == 0 ? -x.z : x.z)));
                }

                return true;
            }

            return false;
        }

        /// <summary>The registered kit scenes, their world centres and whether they are shown.</summary>
        public static string DescribeScenes()
        {
            var sb = new System.Text.StringBuilder("scenes:");
            foreach (var e in Scenes)
            {
                Bounds w;
                if (e.Root != null && SceneWorldBounds(e.Name, out w))
                {
                    sb.Append(' ').Append(e.Name).Append(" at ").Append(w.center.ToString("F1")).Append(" size ").Append(w.size.ToString("F0")).Append(e.Root.gameObject.activeInHierarchy ? "" : " (hidden)").Append(';');
                }
            }

            return sb.ToString();
        }

        /// <summary>One line for the harness: what is applied and what the sun is doing.</summary>
        public static string Report()
        {
            var env = Instance;
            var sun = env != null ? env._sun : null;
            return "env: " + (Active != null ? Active.Key : "world default") + (Suppressed ? " (suppressed)" : "") + ", activity " + Activity + ", scenes " + Scenes.Count
                + ", sun " + (sun != null ? (sun.enabled ? "on " + sun.transform.eulerAngles.ToString("F0") + " x" + sun.intensity.ToString("0.##", CultureInfo.InvariantCulture) + " " + sun.shadows + " " + sun.shadowStrength.ToString("0.##", CultureInfo.InvariantCulture) : "off") : "none")
                + ", RenderSettings.sun " + (RenderSettings.sun != null ? RenderSettings.sun.name : "null") + ", ambient " + RenderSettings.ambientMode + " " + RenderSettings.ambientSkyColor + ", fog " + RenderSettings.fog
                + ", sky " + (RenderSettings.skybox != null ? RenderSettings.skybox.name + " (" + RenderSettings.skybox.shader.name + ")" : "none") + ", reflection " + RenderSettings.reflectionIntensity.ToString("0.###", CultureInfo.InvariantCulture)
                + ", applies " + (env != null ? env._applies : 0);
        }
    }
}
