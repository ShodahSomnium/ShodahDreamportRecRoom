using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// Rec Room's own final Rec Center, loaded at runtime from the player's Rec Room download cache (nothing ships with
    /// the world). Rec Room kept the July 2025 Studio room in %USERPROFILE%\AppData\LocalLow\Against Gravity\Rec Room\
    /// HTTPCache as plain Unity 2021.3 bundles: a scene bundle ("RecCenter-RecCenter_071825", 1AF.data on the user's PC,
    /// 60 MB) and 44 dependency bundles (textures, meshes, materials). <see cref="RrHttpCache"/> finds them by content on
    /// the worker; this class loads them with Unity's own loader, loads the scene additively, and adopts it into the
    /// Rec Center room. Players without the cache get the same building from its measurements shipped with the world
    /// (iteration 16, <see cref="RrRecCenterRoom.BuildFromData"/>: Data/RecCenter_Final.bytes, in this very frame); the
    /// Dreamport rebuild (<see cref="RrRecCenterShell"/>) is left only for a world without that data.
    ///
    /// What the loaded scene needs before it can live in the world:
    /// <list type="number">
    /// <item><b>Shaders.</b> 116 of the scene's 123 materials point into a bundle that is not in the cache: the Rec Room
    /// Studio shader bundle (cab-634aa40a...). Rec Room itself swapped every Studio material's shader at load. The
    /// shader references' path ids are those of stock URP shaders (Lit, Unlit, Terrain/Lit, TextMeshPro), so each
    /// material gets that shader again (<see cref="Lit"/> chooses which Lit), keeping its saved properties.</item>
    /// <item><b>Placement.</b> The scene's own frame has the Dorm entrance at +x. The room frame (rr_reccenter_cache.md
    /// §1.1): X = z + 2.05, Y = y - 0.09, Z = -x - 7.23, yaw + 90. The scene's roots go under a container with that
    /// pose. 524 renderers are static-batched: their vertices are baked in the scene's world space and moving their
    /// transforms does not move them, so each gets the container as its static batch root
    /// (Renderer.staticBatchRootTransform, set by reflection; present in the client's UnityEngine.CoreModule).</item>
    /// <item><b>Sanitising.</b> The scene's URP Volume (Rec Room's post profile) would drive the client's post: its values
    /// are logged and it is destroyed. Cameras, AudioListeners, event systems and screen-space canvases are switched
    /// off (the July 2025 scene has none). The directional key light is switched off and becomes the world's sun
    /// (<see cref="RrEnvironment.UseSunFrom"/>: its rotation, colour, intensity, soft shadows), and the scene's own sky is used
    /// when the client has Skybox/Cubemap; the brightest
    /// <see cref="MaxLights"/> point and spot lights stay, without shadows. Every object moves to the Environment layer
    /// (23), so Somnium's controller walks on the colliders; triggers are switched off; rigidbodies are kinematic.
    /// The two ambience AudioSources (Rec Room's own clips) keep playing.</item>
    /// </list>
    /// Leaving the room destroys the adopted objects with it; then the (empty) scene is unloaded and the bundles are
    /// unloaded with their assets (<see cref="Release"/>), before a re-entry may load them again.
    /// </summary>
    public sealed class RrRecCenterCache : MonoBehaviour
    {
        public const string Area = "RecCenter";

        /// <summary>The text the cached scene's path must contain.</summary>
        public const string SceneKey = "RecCenter-RecCenter_";

        /// <summary>Room-frame pose of the scene's origin (see the class notes).</summary>
        public static readonly Vector3 FramePosition = new Vector3(2.05f, -0.09f, -7.23f);

        public const float FrameYaw = 90f;

        /// <summary>Point and spot lights kept (brightest first); the scene has 41 plus the key light.</summary>
        public const int MaxLights = 48;

        /// <summary>Give up on the bundles and the scene after this long (inside RrTravel's 90 s room-build limit).</summary>
        public const float LoadTimeout = 60f;

        /// <summary>Test flag: skip the cache and build the Dreamport rebuild.</summary>
        public static bool ForceFallback;

        /// <summary>Which Lit the scene's URP Lit materials get.</summary>
        public enum LitSource
        {
            /// <summary>"Rec Room Studio Target/Lit" from the install: what Rec Room swapped Studio materials to.</summary>
            StudioTarget,

            /// <summary>The install's own "Universal Render Pipeline/Lit" (the same asset id as the missing one).</summary>
            InstallUrpLit,

            /// <summary>The client's URP Lit (Shader.Find).</summary>
            ClientUrpLit
        }

        /// <summary>The Lit to use (the harness compares all three).</summary>
        public static LitSource Lit = LitSource.StudioTarget;

        public static RrRecCenterCache Current { get; private set; }

        /// <summary>The last load's outcome line (the one "RecCenter: using ..." / "no cached Rec Center" line).</summary>
        public static string LastOutcome { get; private set; }

        // Shader path ids inside the Studio shader bundle (equal to the install's stock shaders' ids).
        private const long IdLit = -4055751637662985151;
        private const long IdUnlit = -261421426886131751;
        private const long IdTerrain = -3885793140065008626;
        private const long IdParticle = 8918850207803687531;
        private const long IdTree = -6364362845305036051;
        private const long IdTmp = 683148379888368288;
        private const long IdTmpMobile = 7030032868776217633;

        private enum Kind
        {
            Keep,
            Lit,
            Unlit,
            Terrain,
            Particle,
            Tree,
            Tmp,
            TmpMobile
        }

        private static readonly List<AssetBundle> Open = new List<AssetBundle>();
        private static Scene _openScene;
        private static bool _releasing;
        private static bool _announced;
        private static PropertyInfo _batchRoot;
        private static bool _batchRootLooked;

        public RrCachedScene Found;
        public bool Live;
        public string Error;
        public float LoadSeconds;
        public float BundleSeconds;
        public float SceneSeconds;
        public int LightmapsBefore;
        public int LightmapsAfter;
        public int Renderers;
        public int StaticBatched;
        public int MaterialCount;
        public int ShadersMissing;
        public int ShadersUnsupported;
        public int ShadersError;
        public int ShaderSwaps;
        public int LightsKept;
        public int LightsOff;
        public int Colliders;
        public int TriggersOff;
        public int AudioSources;
        public int Disabled;

        /// <summary>The scene's root objects, now children of this container.</summary>
        public readonly List<GameObject> Roots = new List<GameObject>();

        /// <summary>The cached scene's directional key light (off; RrEnvironment's sun takes its values).</summary>
        private Light _keyLight;

        /// <summary>The cached scene's own sky material, when the client can draw it (Skybox/Cubemap), else null.</summary>
        public Material Sky;

        private readonly List<KeyValuePair<Material, Kind>> _mats = new List<KeyValuePair<Material, Kind>>();
        private readonly Dictionary<string, int> _layersBefore = new Dictionary<string, int>();
        private Shader _litShader;
        private LitSource _litUsed;

        /// <summary>This container's transform: the scene's origin in the room frame.</summary>
        public Transform Frame
        {
            get { return transform; }
        }

        // ------------------------------------------------------------------ load

        /// <summary>
        /// Finds and loads the cached Rec Center under room (a coroutine behind the loading card). done gets the live
        /// container, or null when this PC has none or it failed (then the room builds the Dreamport rebuild). Logs one
        /// outcome line either way.
        /// </summary>
        public static IEnumerator Load(Transform room, Action<RrRecCenterCache> done)
        {
            float started = Time.realtimeSinceStartup;
            if (ForceFallback)
            {
                Outcome(false, "no cached Rec Center on this PC: using the Dreamport rebuild (fallback forced by the test flag)");
                done?.Invoke(null);
                yield break;
            }

            float wait = Time.realtimeSinceStartup + 20f;
            while (_releasing && Time.realtimeSinceStartup < wait)
            {
                yield return null;
            }

            if (_releasing || Open.Count > 0)
            {
                Outcome(false, "no cached Rec Center loaded: the previous visit's bundles are still being released: using the Dreamport rebuild");
                done?.Invoke(null);
                yield break;
            }

            string pdp = null;
            try
            {
                pdp = Application.persistentDataPath;
            }
            catch (Exception)
            {
                // No data path (editor oddity): the profile folder is still tried.
            }

            string index = string.IsNullOrEmpty(pdp) ? null : Path.Combine(pdp, "SomniumRecRoom", "rr_httpcache_index.txt");
            RrCachedScene found = null;
            Func<object> job = () =>
            {
                string dir = RrHttpCache.Locate(pdp);
                return dir == null ? new RrCachedScene { Error = "no Rec Room HTTPCache folder" } : RrHttpCache.FindScene(dir, SceneKey, index);
            };
            var worker = RrMain.Instance != null ? RrMain.Instance.Worker : null;
            if (worker != null)
            {
                bool finished = false;
                Exception failure = null;
                worker.Post(job, (r, e) =>
                {
                    found = r as RrCachedScene;
                    failure = e;
                    finished = true;
                });
                while (!finished)
                {
                    yield return null;
                }

                if (failure != null)
                {
                    found = new RrCachedScene { Error = failure.GetType().Name + ": " + failure.Message };
                }
            }
            else
            {
                found = (RrCachedScene)job();
            }

            if (found == null || !found.Found)
            {
                Outcome(false, "no cached Rec Center on this PC: using the Dreamport rebuild (" + (found != null ? found.Error : "scan failed") + ")");
                done?.Invoke(null);
                yield break;
            }

            // A partial cache: only the Studio shader bundle may be missing (every material that points into it gets a
            // shader here); anything else missing would leave holes (meshes, textures), so the rebuild stands instead.
            var shaderCabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in found.MaterialShaders)
            {
                int hash = kv.Value.LastIndexOf('#');
                if (hash > 0)
                {
                    shaderCabs.Add(kv.Value.Substring(0, hash));
                }
            }

            var holes = new List<string>();
            foreach (string cab in found.MissingCabs)
            {
                if (!shaderCabs.Contains(cab))
                {
                    holes.Add(cab);
                }
            }

            if (holes.Count > 0)
            {
                Outcome(false, "the cached Rec Center is incomplete on this PC (" + holes.Count + " of its bundles are not in the cache: "
                    + string.Join(", ", holes.ToArray()) + "): using the Dreamport rebuild");
                done?.Invoke(null);
                yield break;
            }

            RrLog.Info(Area, "cache scan: " + found.FilesScanned + " files, " + found.UnityFsFiles + " UnityFS, " + found.SceneBundles + " scene bundles, "
                + found.FilesReread + " re-read, " + found.Ms.ToString("0") + " ms; scene " + found.ScenePath + " in " + Path.GetFileName(found.SceneFile)
                + " + " + found.Dependencies.Count + " dependencies, " + Mb(found.Bytes) + " MB; missing CABs: "
                + (found.MissingCabs.Count > 0 ? string.Join(", ", found.MissingCabs.ToArray()) : "none") + "; " + found.MaterialShaders.Count + " material shader hints");

            var go = new GameObject("Rec Room Rec Center (download cache)");
            go.transform.SetParent(room, false);
            go.transform.localPosition = FramePosition;
            go.transform.localRotation = Quaternion.Euler(0f, FrameYaw, 0f);
            var c = go.AddComponent<RrRecCenterCache>();
            c.Found = found;
            Current = c;
            yield return c.LoadRoutine();
            c.LoadSeconds = Time.realtimeSinceStartup - started;
            if (!c.Live)
            {
                string why = c.Error ?? "unknown failure";
                Destroy(go);
                Outcome(false, "cached Rec Center failed to load (" + why + "): using the Dreamport rebuild");
                done?.Invoke(null);
                yield break;
            }

            Outcome(true, "using Rec Room's own cached Rec Center (scene " + ShortScene(found.SceneName) + ", " + (found.Dependencies.Count + 1) + " bundles, "
                + Mb(found.Bytes) + " MB) from " + found.CacheDir + " in " + c.LoadSeconds.ToString("0.0") + " s (bundles " + c.BundleSeconds.ToString("0.0")
                + " s, scene " + c.SceneSeconds.ToString("0.0") + " s)");
            done?.Invoke(c);
        }

        private static void Outcome(bool live, string line)
        {
            LastOutcome = line;
            _announced = live;
            RrLog.Info(Area, line);
        }

        private static string ShortScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return "?";
            }

            int dash = sceneName.LastIndexOf('-');
            return dash >= 0 ? sceneName.Substring(dash + 1) : sceneName;
        }

        private static string Mb(long bytes)
        {
            return (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private IEnumerator LoadRoutine()
        {
            float t0 = Time.realtimeSinceStartup;
            float deadline = t0 + LoadTimeout;

            // The static batch root property is needed; without it the building would stand in the scene's own frame.
            if (BatchRootProperty() == null)
            {
                Error = "Renderer.staticBatchRootTransform is not available in this Unity";
                yield break;
            }

            // 1. Bundles (dependencies first; Unity needs them loaded before the scene's objects resolve).
            var paths = new List<string>(Found.Dependencies) { Found.SceneFile };
            var requests = new List<AssetBundleCreateRequest>(paths.Count);
            foreach (string p in paths)
            {
                requests.Add(AssetBundle.LoadFromFileAsync(p));
            }

            while (Time.realtimeSinceStartup < deadline)
            {
                bool all = true;
                foreach (var r in requests)
                {
                    if (r != null && !r.isDone)
                    {
                        all = false;
                        break;
                    }
                }

                if (all)
                {
                    break;
                }

                yield return null;
            }

            AssetBundle sceneBundle = null;
            for (int i = 0; i < requests.Count; i++)
            {
                var b = requests[i] != null && requests[i].isDone ? requests[i].assetBundle : null;
                if (b == null)
                {
                    if (Error == null)
                    {
                        Error = "bundle " + Path.GetFileName(paths[i]) + (requests[i] != null && !requests[i].isDone ? " timed out" : " did not open");
                    }

                    continue;
                }

                Open.Add(b);
                if (i == requests.Count - 1)
                {
                    sceneBundle = b;
                }
            }

            BundleSeconds = Time.realtimeSinceStartup - t0;
            if (Error != null || sceneBundle == null)
            {
                Error = Error ?? "the scene bundle did not open";
                yield break;
            }

            string[] scenes = sceneBundle.GetAllScenePaths();
            string scenePath = null;
            foreach (string s in scenes)
            {
                if (s.IndexOf(SceneKey, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    scenePath = s;
                }
            }

            if (scenePath == null)
            {
                Error = "the scene bundle lists no Rec Center scene (" + scenes.Length + " scenes)";
                yield break;
            }

            // 2. The scene, additively. It is sanitised in sceneLoaded (after Awake/OnEnable, before the next frame),
            // so its post Volume never reaches a rendered frame.
            LightmapsBefore = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;
            float s0 = Time.realtimeSinceStartup;
            Scene loaded = default(Scene);
            bool gotIt = false;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = (scene, mode) =>
            {
                if (gotIt || !string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase) && scene.name != Path.GetFileNameWithoutExtension(scenePath))
                {
                    return;
                }

                gotIt = true;
                loaded = scene;
                try
                {
                    // At once, in the frame the scene became active: its post Volume never renders and its colliders
                    // never stand in the scene's own frame (where the previous room or the arrival floor is).
                    var r = scene.GetRootGameObjects();
                    Sanitise(r);
                    Adopt(r);
                }
                catch (Exception e)
                {
                    RrLog.Error(Area, "sanitising the cached scene failed", e);
                }
            };
            SceneManager.sceneLoaded += onLoaded;
            AsyncOperation op = null;
            try
            {
                op = SceneManager.LoadSceneAsync(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            }
            catch (Exception e)
            {
                Error = "LoadSceneAsync threw " + e.GetType().Name + ": " + e.Message;
            }

            if (op == null)
            {
                SceneManager.sceneLoaded -= onLoaded;
                Error = Error ?? "LoadSceneAsync returned null";
                yield break;
            }

            while (!op.isDone && Time.realtimeSinceStartup < deadline + 30f)
            {
                yield return null;
            }

            SceneManager.sceneLoaded -= onLoaded;
            if (!gotIt)
            {
                loaded = SceneManager.GetSceneByPath(scenePath);
            }

            SceneSeconds = Time.realtimeSinceStartup - s0;
            if (!loaded.IsValid() || !loaded.isLoaded)
            {
                Error = op.isDone ? "the scene did not load" : "the scene load timed out";
                if (loaded.IsValid())
                {
                    _openScene = loaded;
                }

                yield break;
            }

            _openScene = loaded;
            LightmapsAfter = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;
            if (Roots.Count == 0)
            {
                var roots = loaded.GetRootGameObjects();
                Sanitise(roots);
                Adopt(roots);
            }

            if (Error != null)
            {
                yield break;
            }

            yield return null;

            // 4. Shaders.
            yield return FixShaders();
            Physics.SyncTransforms();
            Live = true;
            Report();

            // 5. The building's own environment (iteration 8): its key light, now in the room frame, is the sun; its sky
            // is its own material when the client has the shader it needs. Fog and ambient are RrEnvironment's table
            // line RecCenter_071825, read from this scene (DevNotes/iter6/cachecenter.md).
            UseOwnEnvironment();
            UseLightGrid();
        }

        /// <summary>
        /// Iteration 9: the building's point and spot lights in Rec Room's own light grid (<see cref="RrLightGrid"/>). Its
        /// materials use Rec Room Studio Target/Lit, which reads its lights from that grid and ignores URP's per-object
        /// lights, so without it the building was lit by the sun and ambient only.
        /// </summary>
        private void UseLightGrid()
        {
            try
            {
                var sources = new List<RrLightGrid.Source>();
                foreach (var l in GetComponentsInChildren<Light>(false))
                {
                    if (l == null || !l.enabled || l.type == LightType.Directional || (l.type != LightType.Point && l.type != LightType.Spot))
                    {
                        continue;
                    }

                    var s = RrLightGrid.FromLight(l, transform);
                    s.Unity = l;
                    RrLightGrid.ConfigureUnity(l, s);
                    sources.Add(s);
                }

                var grid = RrLightGrid.Attach(gameObject, transform, "RecCenter_071825", default(Bounds), sources);
                if (grid != null)
                {
                    RrLog.Info(Area, "cached scene: " + sources.Count + " point/spot lights in Rec Room's light grid for its Studio Target/Lit materials");
                }
            }
            catch (Exception e)
            {
                RrLog.Error(Area, "the cached scene's light grid failed", e);
            }
        }

        /// <summary>
        /// Hands the cached scene's key light to <see cref="RrEnvironment.UseSunFrom"/> and, when the client has
        /// Skybox/Cubemap (the Studio shader bundle the material points at is not cached), its own sky material
        /// SkyboxLockerRoom_Baked with its cubemap. Without the shader the table's gradient, made from the same cubemap's
        /// face colours in the Android build, stays.
        /// </summary>
        private void UseOwnEnvironment()
        {
            try
            {
                if (_keyLight != null)
                {
                    RrEnvironment.UseSunFrom(_keyLight);
                }

                Material sky = null;
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m != null && m.name == "SkyboxLockerRoom_Baked" && m.HasProperty("_Tex") && m.GetTexture("_Tex") is Cubemap)
                    {
                        sky = m;
                        break;
                    }
                }

                var cube = Shader.Find("Skybox/Cubemap");
                if (sky != null && cube != null && cube.isSupported)
                {
                    sky.shader = cube;
                    Sky = sky;
                    RrEnvironment.UseSky(sky);
                    RrLog.Info(Area, "cached scene: its own sky " + sky.name + " (cubemap " + sky.GetTexture("_Tex").name + ") drawn with the client's Skybox/Cubemap");
                }
                else
                {
                    RrLog.Info(Area, "cached scene: its sky material " + (sky != null ? "is loaded" : "was not found") + ", Skybox/Cubemap " + (cube != null ? (cube.isSupported ? "found" : "unsupported") : "not in this build")
                        + "; the gradient sky from the table stays");
                }
            }
            catch (Exception e)
            {
                RrLog.Error(Area, "applying the cached scene's own environment failed", e);
            }
        }

        /// <summary>
        /// 3. Adopt: the roots keep their scene coordinates as local coordinates under this container (the room-frame pose
        /// of the scene's origin), and the static-batched renderers get it as their batch root.
        /// </summary>
        private void Adopt(GameObject[] roots)
        {
            foreach (var r in roots)
            {
                if (r == null)
                {
                    continue;
                }

                r.transform.SetParent(transform, false);
                Roots.Add(r);
            }

            Renderers = 0;
            StaticBatched = 0;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                Renderers++;
                if (!r.isPartOfStaticBatch)
                {
                    continue;
                }

                try
                {
                    _batchRoot.SetValue(r, transform, null);
                    StaticBatched++;
                }
                catch (Exception e)
                {
                    Error = "setting the static batch root failed: " + e.GetType().Name + ": " + e.Message;
                    return;
                }
            }
        }

        private static PropertyInfo BatchRootProperty()
        {
            if (!_batchRootLooked)
            {
                _batchRootLooked = true;
                try
                {
                    _batchRoot = typeof(Renderer).GetProperty("staticBatchRootTransform", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (_batchRoot != null && (!_batchRoot.CanWrite || _batchRoot.PropertyType != typeof(Transform)))
                    {
                        _batchRoot = null;
                    }
                }
                catch (Exception)
                {
                    _batchRoot = null;
                }
            }

            return _batchRoot;
        }

        // ------------------------------------------------------------------ sanitise

        private void Sanitise(GameObject[] roots)
        {
            var lights = new List<Light>();
            foreach (var root in roots)
            {
                foreach (var b in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (b == null)
                    {
                        continue;
                    }

                    var volume = b as Volume;
                    if (volume != null)
                    {
                        LogVolume(volume);
                        Destroy(volume);
                        Disabled++;
                        continue;
                    }

                    if (b is Camera || b is AudioListener || b is FlareLayer)
                    {
                        b.enabled = false;
                        Disabled++;
                        RrLog.Info(Area, "cached scene: switched off " + b.GetType().Name + " on " + b.name);
                        continue;
                    }

                    var canvas = b as Canvas;
                    if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                    {
                        canvas.enabled = false;
                        Disabled++;
                        RrLog.Info(Area, "cached scene: switched off a screen-space canvas on " + b.name);
                        continue;
                    }

                    string ns = b.GetType().Namespace;
                    if (ns == "UnityEngine.EventSystems")
                    {
                        b.enabled = false;
                        Disabled++;
                        RrLog.Info(Area, "cached scene: switched off " + b.GetType().Name + " on " + b.name);
                        continue;
                    }

                    var light = b as Light;
                    if (light != null)
                    {
                        lights.Add(light);
                        continue;
                    }

                    if (b is AudioSource)
                    {
                        AudioSources++;
                    }
                }

                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger)
                    {
                        c.enabled = false;
                        TriggersOff++;
                    }
                    else
                    {
                        Colliders++;
                    }

                    string key = LayerMask.LayerToName(c.gameObject.layer);
                    key = c.gameObject.layer + (key.Length > 0 ? " " + key : "");
                    int n;
                    _layersBefore.TryGetValue(key, out n);
                    _layersBefore[key] = n + 1;
                }

                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.isKinematic = true;
                }

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = RrLayers.Environment;
                }
            }

            // Lights: the key light becomes the world's sun (RrEnvironment copies its rotation, colour, intensity and soft
            // shadows once the scene is in the room frame, and switches it off: one sun); the brightest points and
            // spots stay without shadows, as Rec Room ran them (all 42 realtime, only the key light shadowed).
            var kept = new List<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    RrLog.Info(Area, "cached scene: key light " + l.name + " (colour " + l.color + ", intensity " + l.intensity.ToString("0.00")
                        + ", rotation " + l.transform.eulerAngles.ToString("F0") + ", shadows " + l.shadows + " " + l.shadowStrength.ToString("0.00") + "): it becomes the world's sun");
                    l.enabled = false;
                    if (_keyLight == null || l.intensity > _keyLight.intensity)
                    {
                        _keyLight = l;
                    }

                    LightsOff++;
                    continue;
                }

                kept.Add(l);
            }

            kept.Sort((a, b) => (b.intensity * b.range).CompareTo(a.intensity * a.range));
            for (int i = 0; i < kept.Count; i++)
            {
                var l = kept[i];
                l.shadows = LightShadows.None;
                if (i >= MaxLights)
                {
                    l.enabled = false;
                    LightsOff++;
                }
                else if (l.enabled && l.gameObject.activeInHierarchy)
                {
                    LightsKept++;
                }
            }
        }

        /// <summary>One line per volume component with its overridden values (for the post-processing work).</summary>
        private static void LogVolume(Volume v)
        {
            var sb = new StringBuilder();
            sb.Append("cached scene post Volume '").Append(v.name).Append("': global ").Append(v.isGlobal).Append(", weight ")
              .Append(v.weight.ToString("0.##", CultureInfo.InvariantCulture)).Append(", priority ").Append(v.priority.ToString("0.##", CultureInfo.InvariantCulture))
              .Append(", profile ").Append(v.sharedProfile != null ? v.sharedProfile.name : "(none)").Append(" (destroyed; values for the post team):");
            if (v.sharedProfile != null)
            {
                foreach (var comp in v.sharedProfile.components)
                {
                    if (comp == null)
                    {
                        continue;
                    }

                    sb.Append(" [").Append(comp.GetType().Name).Append(comp.active ? "" : " inactive");
                    foreach (var f in comp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!typeof(VolumeParameter).IsAssignableFrom(f.FieldType))
                        {
                            continue;
                        }

                        var p = f.GetValue(comp) as VolumeParameter;
                        if (p == null || !p.overrideState)
                        {
                            continue;
                        }

                        object value = null;
                        try
                        {
                            var prop = p.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public);
                            value = prop != null ? prop.GetValue(p, null) : null;
                        }
                        catch (Exception)
                        {
                            value = "?";
                        }

                        sb.Append(' ').Append(f.Name).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    }

                    sb.Append(']');
                }
            }

            RrLog.Info(Area, sb.ToString());
        }

        // ------------------------------------------------------------------ shaders

        private static Kind KindOf(string hint)
        {
            if (string.IsNullOrEmpty(hint))
            {
                return Kind.Keep;
            }

            int hash = hint.LastIndexOf('#');
            long id;
            if (hash < 0 || !long.TryParse(hint.Substring(hash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                return Kind.Keep;
            }

            switch (id)
            {
                case IdLit:
                    return Kind.Lit;
                case IdUnlit:
                    return Kind.Unlit;
                case IdTerrain:
                    return Kind.Terrain;
                case IdParticle:
                    return Kind.Particle;
                case IdTree:
                    return Kind.Tree;
                case IdTmp:
                    return Kind.Tmp;
                case IdTmpMobile:
                    return Kind.TmpMobile;
                default:
                    return Kind.Keep;
            }
        }

        private static bool Broken(Shader s)
        {
            if (s == null || !s.isSupported)
            {
                return true;
            }

            string n = s.name;
            return n == "Hidden/InternalErrorShader" || n == "Standard" || n.StartsWith("Legacy Shaders/", StringComparison.Ordinal)
                || n.EndsWith("FallbackError", StringComparison.Ordinal);
        }

        private IEnumerator FixShaders()
        {
            var seen = new HashSet<int>();
            var all = new List<Material>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null && seen.Add(m.GetInstanceID()))
                    {
                        all.Add(m);
                    }
                }

                var ps = r as ParticleSystemRenderer;
                if (ps != null && ps.trailMaterial != null && seen.Add(ps.trailMaterial.GetInstanceID()))
                {
                    all.Add(ps.trailMaterial);
                }
            }

            foreach (var t in GetComponentsInChildren<Terrain>(true))
            {
                if (t.materialTemplate != null && seen.Add(t.materialTemplate.GetInstanceID()))
                {
                    all.Add(t.materialTemplate);
                }
            }

            MaterialCount = all.Count;
            var names = new Dictionary<string, int>();
            foreach (var m in all)
            {
                var s = m.shader;
                string n = s == null ? "(null)" : s.name;
                if (s == null)
                {
                    ShadersMissing++;
                }
                else if (!s.isSupported)
                {
                    ShadersUnsupported++;
                }

                if (n == "Hidden/InternalErrorShader")
                {
                    ShadersError++;
                }

                int k;
                names.TryGetValue(n, out k);
                names[n] = k + 1;
            }

            var sb = new StringBuilder();
            foreach (var kv in names)
            {
                sb.Append(sb.Length > 0 ? ", " : "").Append(kv.Key).Append(" x").Append(kv.Value);
            }

            RrLog.Info(Area, "cached scene materials as loaded: " + all.Count + " (" + ShadersMissing + " null shader, " + ShadersUnsupported + " unsupported, "
                + ShadersError + " InternalErrorShader): " + sb);

            yield return ResolveLit(Lit);
            foreach (var m in all)
            {
                string hint;
                Found.MaterialShaders.TryGetValue(m.name, out hint);
                var kind = KindOf(hint);
                if (kind == Kind.Keep && Broken(m.shader))
                {
                    // An in-bundle built-in (Standard, legacy particles) or an unknown reference: by its look.
                    kind = m.name.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0 ? Kind.Particle : Kind.Lit;
                }

                if (kind == Kind.Keep)
                {
                    continue;
                }

                _mats.Add(new KeyValuePair<Material, Kind>(m, kind));
            }

            Apply();
        }

        private IEnumerator ResolveLit(LitSource source)
        {
            Shader s = null;
            var assets = RrAssets.Instance;
            if (source == LitSource.StudioTarget && assets != null)
            {
                yield return assets.LoadRoutine<Shader>("Rec Room Studio Target/Lit", 48, x => s = x);
            }
            else if (source == LitSource.InstallUrpLit && assets != null)
            {
                yield return assets.LoadRoutine<Shader>("Universal Render Pipeline/Lit", 48, x => s = x);
            }

            if (s != null && !s.isSupported)
            {
                RrLog.Warn(Area, "cached scene: " + source + " shader " + s.name + " is not supported here; the client's URP Lit stands in");
                s = null;
            }

            if (s == null)
            {
                s = Shader.Find("Universal Render Pipeline/Lit");
                source = LitSource.ClientUrpLit;
            }

            _litShader = s;
            _litUsed = source;
        }

        /// <summary>Gives every mapped material its shader (in place: renderers keep their references).</summary>
        private void Apply()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader terrain = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            Shader particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Shader tmp = Shader.Find("TextMeshPro/Distance Field");
            Shader tmpMobile = Shader.Find("TextMeshPro/Mobile/Distance Field") ?? tmp;
            if (tmp == null && RrAssets.Instance != null)
            {
                tmp = RrAssets.Instance.Resident<Shader>("TextMeshPro/Distance Field");
                tmpMobile = tmpMobile ?? RrAssets.Instance.Resident<Shader>("TextMeshPro/Mobile/Distance Field") ?? tmp;
            }

            var counts = new Dictionary<string, int>();
            var replaced = new Dictionary<int, Material>();
            ShaderSwaps = 0;
            foreach (var kv in _mats)
            {
                var m = kv.Key;
                if (m == null)
                {
                    continue;
                }

                Shader target;
                switch (kv.Value)
                {
                    case Kind.Lit:
                    case Kind.Tree:
                        target = _litShader;
                        break;
                    case Kind.Unlit:
                        target = unlit;
                        break;
                    case Kind.Terrain:
                        target = terrain;
                        break;
                    case Kind.Particle:
                        target = particle;
                        break;
                    case Kind.Tmp:
                        target = tmp;
                        break;
                    default:
                        target = tmpMobile;
                        break;
                }

                if (target == null)
                {
                    continue;
                }

                int queue = m.renderQueue;
                m.shader = target;
                if (queue != m.renderQueue && queue >= 2450)
                {
                    m.renderQueue = queue;
                }

                if (kv.Value == Kind.Tree)
                {
                    // Rec Room's mobile tree shader (not in the install): alpha-clipped, double-sided leaves (GUESS cutoff).
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.35f);
                    m.SetFloat("_Cull", 0f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.doubleSidedGI = true;
                }

                ShaderSwaps++;
                string key = kv.Value + " -> " + target.name;
                int n;
                counts.TryGetValue(key, out n);
                counts[key] = n + 1;
                var fixedMat = RrMaterials.Fix(m);
                if (fixedMat != null && fixedMat != m)
                {
                    replaced[m.GetInstanceID()] = fixedMat;
                }
            }

            if (replaced.Count > 0)
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material f;
                        if (mats[i] != null && replaced.TryGetValue(mats[i].GetInstanceID(), out f))
                        {
                            mats[i] = f;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        r.sharedMaterials = mats;
                    }
                }
            }

            foreach (var t in GetComponentsInChildren<Terrain>(true))
            {
                Material f;
                if (t.materialTemplate != null && replaced.TryGetValue(t.materialTemplate.GetInstanceID(), out f))
                {
                    t.materialTemplate = f;
                }
            }

            var sb = new StringBuilder();
            foreach (var kv in counts)
            {
                sb.Append(sb.Length > 0 ? ", " : "").Append(kv.Key).Append(" x").Append(kv.Value);
            }

            RrLog.Info(Area, "cached scene shaders: Lit from " + _litUsed + " (" + (_litShader != null ? _litShader.name + (_litShader.isSupported ? "" : ", unsupported") : "none")
                + "); " + ShaderSwaps + " swaps: " + sb + (replaced.Count > 0 ? "; " + replaced.Count + " replaced by RrMaterials" : ""));
        }

        /// <summary>Re-maps the scene's Lit materials to another Lit (harness comparison).</summary>
        public IEnumerator UseLit(LitSource source)
        {
            yield return ResolveLit(source);
            Apply();
        }

        private void Report()
        {
            var sb = new StringBuilder();
            foreach (var kv in _layersBefore)
            {
                sb.Append(sb.Length > 0 ? ", " : "").Append(kv.Key).Append(": ").Append(kv.Value);
            }

            int audio = 0;
            foreach (var a in GetComponentsInChildren<AudioSource>(true))
            {
                if (a.enabled && a.gameObject.activeInHierarchy)
                {
                    audio++;
                }
            }

            RrLog.Info(Area, "cached scene adopted: " + Roots.Count + " roots, " + Renderers + " renderers (" + StaticBatched + " static-batched, re-rooted), "
                + MaterialCount + " materials, " + ShaderSwaps + " shader swaps, lights " + LightsKept + " kept / " + LightsOff + " off, "
                + Colliders + " colliders (" + TriggersOff + " triggers off; layers before: " + sb + "; now all " + RrLayers.Environment + "), "
                + audio + " of " + AudioSources + " audio sources playing, " + Disabled + " components switched off, lightmaps " + LightmapsBefore + " -> " + LightmapsAfter);
        }

        // ------------------------------------------------------------------ release

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            Release();
        }

        /// <summary>
        /// Unloads the cached scene (empty once adopted) and the bundles with their assets. With RrMain alive this waits
        /// a frame (the room's objects are destroyed at the end of this one) and for the scene unload; without it
        /// (the world closing) it unloads at once.
        /// </summary>
        public static void Release()
        {
            if (_releasing || (Open.Count == 0 && !_openScene.IsValid()))
            {
                return;
            }

            if (RrMain.Instance != null && RrMain.Instance.isActiveAndEnabled)
            {
                _releasing = true;
                RrMain.Instance.StartCoroutine(ReleaseRoutine());
                return;
            }

            try
            {
                if (_openScene.IsValid() && _openScene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(_openScene);
                }
            }
            catch (Exception)
            {
                // Already going.
            }

            UnloadBundles();
        }

        private static IEnumerator ReleaseRoutine()
        {
            float started = Time.realtimeSinceStartup;
            bool announce = _announced;
            _announced = false;
            yield return null;
            AsyncOperation op = null;
            try
            {
                if (_openScene.IsValid() && _openScene.isLoaded)
                {
                    op = SceneManager.UnloadSceneAsync(_openScene);
                }
            }
            catch (Exception e)
            {
                RrLog.Warn(Area, "unloading the cached scene failed: " + e.Message);
            }

            float deadline = Time.realtimeSinceStartup + 20f;
            while (op != null && !op.isDone && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            int n = UnloadBundles();
            if (announce)
            {
                // After a failed load the one outcome line has said it all.
                RrLog.Info(Area, "cached Rec Center released: scene unloaded" + (op == null ? " (was not loaded)" : "") + ", " + n + " bundles unloaded in "
                    + (Time.realtimeSinceStartup - started).ToString("0.00") + " s");
            }

            _releasing = false;
        }

        private static int UnloadBundles()
        {
            int n = 0;
            foreach (var b in Open)
            {
                if (b != null)
                {
                    try
                    {
                        b.Unload(true);
                        n++;
                    }
                    catch (Exception)
                    {
                        // Already unloaded.
                    }
                }
            }

            Open.Clear();
            _openScene = default(Scene);
            return n;
        }

        /// <summary>Loaded bundles and scene (census and tests).</summary>
        public static string Describe()
        {
            return "cache bundles open " + Open.Count + ", scene " + (_openScene.IsValid() ? _openScene.name + (_openScene.isLoaded ? " loaded" : "") : "none")
                + (_releasing ? ", releasing" : "") + (ForceFallback ? ", fallback forced" : "");
        }
    }
}
