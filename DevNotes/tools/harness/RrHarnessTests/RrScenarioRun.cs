using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace RrHarness
{
    /// <summary>
    /// One scenario run: boots the world (the "Rec Room" object with RrMain, or the saved world scene), executes the
    /// scenario's steps and writes summary.txt. A failing step is recorded and the run goes on; only a boot that
    /// never reaches RrMain.Ready (or an unreadable scenario) is a failure of the test.
    ///
    /// Scenario file: {"name": "...", "scene": "Assets/Rec Room Scene.unity" (optional), "boot_timeout": 240,
    /// "arrival": false, "steps": [ ... ]} or just the array of steps. "arrival": true keeps the player in the
    /// arrival room at start (RrTravel.StayInArrival) - for scenarios that test things on the Arrival floor, which
    /// every room now overlaps (all rooms are built at the world origin). Steps (the first action key decides the kind):
    ///   {"log": "text"}                                   a marker line in the log and the summary
    ///   {"wait": seconds}                                 real time
    ///   {"wait_frames": n}
    ///   {"spawn": "(Prefab)", "pos": [x,y,z], "yaw": deg, "physics": false, "ground": true, "scale": 1,
    ///    "options": "prop|scenery|visual", "timeout": 180}  RrSpawn.Spawn under RrMain.WorldRoot; ground puts the
    ///                                                       renderers' lowest point at pos.y; physics false = kinematic
    ///   {"goto": "Dorm", "instance": -1, "timeout": 120}  Rooms.RrTravel.GoTo by reflection, waits for Current
    ///   {"shot": "name", "pos": [x,y,z], "look": [x,y,z], "fov": 60, "w": 1280, "h": 720, "msaa": 4,
    ///    "post": false, "bg": [r,g,b], "frame": "world"} own camera -> RenderTexture -> out/name.png; "frame":
    ///                                                       "room" = pos/look relative to RrTravel.CurrentRoom's Origin
    ///   {"call": "Namespace.Type.StaticMethod"}           no arguments; an IEnumerator result is run to the end
    ///   {"expect_log": "substring", "timeout": 0}          any log line so far (or within timeout) contains it
    ///   {"dump": "name", "root": "GameObject name"}      hierarchy + components + materials -> out/dump_name.txt
    ///   {"clear": true}                                   destroys what the harness spawned
    /// </summary>
    public sealed class RrScenarioRun
    {
        public const string HarnessRoot = @"C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad\harness";

        private static readonly string[] Kinds = { "spawn", "goto", "shot", "call", "expect_log", "dump", "clear", "wait_frames", "log", "wait" };

        public string ScenarioPath;
        public string Name;
        public string OutDir;
        public string BootFailure;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly object _lock = new object();
        private readonly List<LogLine> _logs = new List<LogLine>();
        private readonly List<StepResult> _steps = new List<StepResult>();
        private readonly List<string> _pngs = new List<string>();
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly DateTime _started = DateTime.Now;
        private Dictionary<string, object> _scenario;
        private List<object> _stepList;
        private RrMain _main;
        private TMP_Text _label;
        private Scene _loadedScene;
        private double _readyAt = -1;
        private string _environment = "";
        private float _windowMaxFrame;
        private int _windowFrames;
        private float _runMaxFrame;

        private sealed class LogLine
        {
            public double T;
            public LogType Type;
            public string Text;
            public string Stack;
        }

        private sealed class StepResult
        {
            public int Index;
            public string Kind;
            public string Arg;
            public string Status = "ok";
            public string Message = "";
            public double Ms;
            public float MaxFrameMs;
            public int Frames;

            public void Fail(string m)
            {
                Status = "FAIL";
                Message = m;
            }

            public void Skip(string m)
            {
                Status = "skip";
                Message = m;
            }
        }

        // ------------------------------------------------------------------ driver

        /// <summary>Called by the test once per frame with the frame's duration.</summary>
        public void Frame(float dt)
        {
            float ms = dt * 1000f;
            _windowMaxFrame = Mathf.Max(_windowMaxFrame, ms);
            _windowFrames++;
            _runMaxFrame = Mathf.Max(_runMaxFrame, ms);
        }

        public IEnumerator Execute()
        {
            Application.logMessageReceivedThreaded += OnLog;
            try
            {
                if (!Prepare())
                {
                    yield break;
                }

                // ---- boot
                var boot = Boot();

                while (boot.MoveNext())
                {
                    yield return boot.Current;
                }

                if (BootFailure != null)
                {
                    yield break;
                }

                // ---- steps
                for (int i = 0; i < _stepList.Count; i++)
                {
                    var step = _stepList[i] as Dictionary<string, object>;
                    var r = new StepResult { Index = i + 1 };
                    _steps.Add(r);

                    if (step == null)
                    {
                        r.Kind = "?";
                        r.Fail("not an object");
                        continue;
                    }

                    r.Kind = Kinds.FirstOrDefault(step.ContainsKey) ?? "?";
                    r.Arg = r.Kind == "?" ? string.Join(",", step.Keys.ToArray()) : Short(step[r.Kind]);
                    Debug.Log("[RrHarness] step " + r.Index + " " + r.Kind + " " + r.Arg);
                    var sw = Stopwatch.StartNew();
                    _windowMaxFrame = 0f;
                    _windowFrames = 0;
                    var d = Drive(Step(r.Kind, step, r), r);

                    while (d.MoveNext())
                    {
                        yield return d.Current;
                    }

                    r.Ms = sw.Elapsed.TotalMilliseconds;

                    // A frame's duration is known only when the next one starts: one more frame so the work the step
                    // did after its last yield (a shot's render) is counted in its own max frame.
                    yield return null;
                    r.MaxFrameMs = _windowMaxFrame;
                    r.Frames = _windowFrames;
                    Debug.Log("[RrHarness] step " + r.Index + " " + r.Status + " (" + r.Ms.ToString("0", CultureInfo.InvariantCulture) + " ms) " + r.Message);
                }
            }
            finally
            {
                WriteSummary();
            }
        }

        /// <summary>Runs a step's iterator, turning an exception into a failed step (iterators cannot catch around a yield).</summary>
        private static IEnumerator Drive(IEnumerator inner, StepResult r)
        {
            while (true)
            {
                object current;

                try
                {
                    if (!inner.MoveNext())
                    {
                        yield break;
                    }

                    current = inner.Current;
                }
                catch (Exception e)
                {
                    var ex = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                    r.Fail(ex.GetType().Name + ": " + ex.Message + " @ " + FirstLines(ex.StackTrace, 2));
                    yield break;
                }

                yield return current;
            }
        }

        private IEnumerator Step(string kind, Dictionary<string, object> s, StepResult r)
        {
            switch (kind)
            {
                case "log": return StepLog(s, r);
                case "wait": return StepWait(s, r);
                case "wait_frames": return StepWaitFrames(s, r);
                case "spawn": return StepSpawn(s, r);
                case "goto": return StepGoto(s, r);
                case "shot": return StepShot(s, r);
                case "call": return StepCall(s, r);
                case "expect_log": return StepExpectLog(s, r);
                case "dump": return StepDump(s, r);
                case "clear": return StepClear(r);
                default: return Unknown(s, r);
            }
        }

        // ------------------------------------------------------------------ setup and boot

        private bool Prepare()
        {
            ScenarioPath = Environment.GetEnvironmentVariable("RR_SCENARIO");

            if (string.IsNullOrEmpty(ScenarioPath))
            {
                ScenarioPath = Path.Combine(HarnessRoot, "scenarios", "smoke.json");
            }

            Name = Path.GetFileNameWithoutExtension(ScenarioPath);
            OutDir = Environment.GetEnvironmentVariable("RR_OUT");

            if (string.IsNullOrEmpty(OutDir))
            {
                OutDir = Path.Combine(HarnessRoot, "out", Name);
            }

            Directory.CreateDirectory(OutDir);
            Debug.Log("[RrHarness] scenario " + ScenarioPath + " -> " + OutDir);

            try
            {
                object parsed = RrJson.Parse(File.ReadAllText(ScenarioPath));
                _scenario = parsed as Dictionary<string, object> ?? new Dictionary<string, object> { { "steps", parsed } };
                _stepList = _scenario.ContainsKey("steps") ? _scenario["steps"] as List<object> : null;

                if (_stepList == null)
                {
                    throw new FormatException("no \"steps\" array");
                }

                if (_scenario.ContainsKey("name") && _scenario["name"] is string)
                {
                    Name = (string)_scenario["name"];
                }
            }
            catch (Exception e)
            {
                BootFailure = "scenario " + ScenarioPath + " unreadable: " + e.Message;
                return false;
            }

            return true;
        }

        private IEnumerator Boot()
        {
#if UNITY_EDITOR
            // Compile every shader variant before drawing with it: asynchronous compilation draws a cyan placeholder.
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            AudioListener.volume = 0f;
            float timeout = (float)Num(_scenario, "boot_timeout", 240);
            string scenePath = Str(_scenario, "scene", null);

            // The camera an arrived player would have (Camera.main for RrPlayer), and the only AudioListener.
            var head = new GameObject("Harness Camera");
            _created.Add(head);
            head.tag = "MainCamera";
            var headCam = head.AddComponent<Camera>();
            headCam.nearClipPlane = 0.02f;
            headCam.farClipPlane = 5000f;
            head.AddComponent<AudioListener>();
            head.transform.position = Vec(_scenario, "camera", new Vector3(0f, 1.6f, -3f));
            head.transform.LookAt(new Vector3(0f, 1f, 0f));

            // Rooms are built at the world origin, over the Arrival floor: a scenario testing things on that floor
            // keeps the player in the arrival room (set before RrMain boots; RrTravel.Shutdown clears it).
            SomniumSpace.Worlds.SomniumRecRoom.Rooms.RrTravel.StayInArrival = Bool(_scenario, "arrival", false);
            if (SomniumSpace.Worlds.SomniumRecRoom.Rooms.RrTravel.StayInArrival)
            {
                Debug.Log("[RrHarness] arrival: the player stays in the arrival room (no auto trip home)");
            }

            if (!string.IsNullOrEmpty(scenePath))
            {
                var load = LoadWorldScene(scenePath);

                while (load.MoveNext())
                {
                    yield return load.Current;
                }

                if (BootFailure != null)
                {
                    yield break;
                }
            }
            else
            {
                CreateWorld();
            }

            float start = Time.realtimeSinceStartup;
            string lastStatus = null;

            while (!RrMain.Ready)
            {
                if (_main == null)
                {
                    BootFailure = "RrMain was destroyed during boot";
                    yield break;
                }

                string status = _label != null ? _label.text : "";

                if (status != lastStatus)
                {
                    lastStatus = status;
                }

                if (_main.Failure != null)
                {
                    BootFailure = "boot failed: " + _main.Failure + " (status: " + status + ")";
                    yield break;
                }

                if (Time.realtimeSinceStartup - start > timeout)
                {
                    BootFailure = "RrMain.Ready not reached in " + timeout + " s; status: '" + status + "'";
                    yield break;
                }

                yield return null;
            }

            _readyAt = _clock.Elapsed.TotalSeconds;
            Debug.Log("[RrHarness] RrMain.Ready after " + _readyAt.ToString("0.0", CultureInfo.InvariantCulture) + " s");
        }

        private void CreateWorld()
        {
            // The scene's sun, floor and render settings, from the editor tool itself when it is compiled in.
            MethodInfo build = FindType("SomniumSpace.Worlds.SomniumRecRoom.EditorTools.RrSceneSetup")?.GetMethod("BuildEnvironment", BindingFlags.Public | BindingFlags.Static);
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());

            if (build != null)
            {
                build.Invoke(null, null);
                _environment = "RrSceneSetup.BuildEnvironment (the world scene's light, Arrival floor and render settings)";
            }
            else
            {
                var sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.96f, 0.9f);
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Arrival";
                floor.transform.position = new Vector3(0f, -0.25f, 0f);
                floor.transform.localScale = new Vector3(20f, 0.5f, 20f);
                floor.layer = RrLayers.Environment;
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.78f, 0.86f, 1f);
                RenderSettings.ambientEquatorColor = new Color(0.8f, 0.8f, 0.78f);
                RenderSettings.ambientGroundColor = new Color(0.52f, 0.48f, 0.44f);
                RenderSettings.sun = sun;
                _environment = "harness fallback (RrSceneSetup not compiled in)";
            }

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!before.Contains(root))
                {
                    _created.Add(root);
                }
            }

            // Status label: screen overlay, as the scene's Status Canvas.
            var canvasGo = new GameObject("Status Canvas");
            _created.Add(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var textGo = new GameObject("Status");
            textGo.transform.SetParent(canvasGo.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();

            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.TopLeft;
            _label = text;

            // The world object, wired before it wakes (RrMain reads _status in Start).
            var go = new GameObject("Rec Room");
            go.SetActive(false);
            _created.Add(go);
            _main = go.AddComponent<RrMain>();
            FieldInfo status = typeof(RrMain).GetField("_status", BindingFlags.NonPublic | BindingFlags.Instance);

            if (status != null)
            {
                status.SetValue(_main, text);
            }
            else
            {
                Debug.LogWarning("[RrHarness] RrMain has no _status field; the label stays empty");
            }

            go.SetActive(true);
        }

        private IEnumerator LoadWorldScene(string path)
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));

            if (op == null)
            {
                BootFailure = "could not load scene " + path;
                yield break;
            }

            while (!op.isDone)
            {
                yield return null;
            }

            _loadedScene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(_loadedScene);
            _main = RrMain.Instance;

            if (_main == null)
            {
                BootFailure = "scene " + path + " has no active RrMain";
                yield break;
            }

            _label = _main.StatusLabel;
            _environment = "world scene " + path + " (its own light, floor, uploader and network objects)";
#else
            BootFailure = "scene mode needs the editor";
            yield break;
#endif
        }

        public IEnumerator TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.Destroy(_created[i]);
                }
            }

            yield return null;

            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(_loadedScene);

                while (op != null && !op.isDone)
                {
                    yield return null;
                }
            }

            Application.logMessageReceivedThreaded -= OnLog;
        }

        // ------------------------------------------------------------------ steps

        private static IEnumerator Unknown(Dictionary<string, object> s, StepResult r)
        {
            r.Fail("unknown step (keys: " + string.Join(", ", s.Keys.ToArray()) + ")");
            yield break;
        }

        private static IEnumerator StepLog(Dictionary<string, object> s, StepResult r)
        {
            r.Message = Str(s, "log", "");
            yield break;
        }

        private static IEnumerator StepWait(Dictionary<string, object> s, StepResult r)
        {
            float end = Time.realtimeSinceStartup + (float)Num(s, "wait", 1);

            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        private static IEnumerator StepWaitFrames(Dictionary<string, object> s, StepResult r)
        {
            int n = (int)Num(s, "wait_frames", 1);

            for (int i = 0; i < n; i++)
            {
                yield return null;
            }
        }

        private IEnumerator StepSpawn(Dictionary<string, object> s, StepResult r)
        {
            string prefab = Str(s, "spawn", "");
            Vector3 pos = Vec(s, "pos", Vector3.zero);
            float yaw = (float)Num(s, "yaw", 0);
            bool physics = Bool(s, "physics", false);
            RrSpawnOptions options;

            switch (Str(s, "options", "prop"))
            {
                case "scenery": options = RrSpawnOptions.Scenery(); break;
                case "visual": options = RrSpawnOptions.Visual(); break;
                default: options = RrSpawnOptions.Prop(); break;
            }

            options.Kinematic = options.Kinematic || !physics;
            options.Scale = (float)Num(s, "scale", 1);
            bool done = false;
            RrInstance inst = null;
            RrSpawn.Spawn(prefab, _main.WorldRoot, pos, Quaternion.Euler(0f, yaw, 0f), i =>
            {
                inst = i;
                done = true;
            }, options);
            float timeout = (float)Num(s, "timeout", 180);
            float start = Time.realtimeSinceStartup;

            while (!done)
            {
                if (Time.realtimeSinceStartup - start > timeout)
                {
                    r.Fail("no callback in " + timeout + " s");
                    yield break;
                }

                yield return null;
            }

            if (inst == null)
            {
                r.Fail("RrSpawn returned null (see the Assets/Spawn warnings)");
                yield break;
            }

            _spawned.Add(inst.gameObject);

            if (Bool(s, "ground", true))
            {
                Bounds? b = RenderBounds(inst.gameObject);

                if (b.HasValue)
                {
                    inst.transform.position += Vector3.up * (pos.y - b.Value.min.y);
                }
            }

            r.Message = Describe(inst.gameObject);
        }

        private static IEnumerator StepGoto(Dictionary<string, object> s, StepResult r)
        {
            string target = Str(s, "goto", "");
            Type travel = FindType("SomniumSpace.Worlds.SomniumRecRoom.Rooms.RrTravel");
            MethodInfo goTo = travel?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "GoTo" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType.IsEnum);

            if (goTo == null)
            {
                r.Skip("Rooms.RrTravel.GoTo(activity, ...) not in this build");
                yield break;
            }

            ParameterInfo[] ps = goTo.GetParameters();
            object activity = Enum.Parse(ps[0].ParameterType, target, true);
            var args = new object[ps.Length];
            args[0] = activity;

            for (int i = 1; i < ps.Length; i++)
            {
                args[i] = ps[i].ParameterType == typeof(int) && s.ContainsKey("instance") ? (int)Num(s, "instance", -1) : ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
            }

            goTo.Invoke(null, args);
            PropertyInfo current = travel.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo busy = travel.GetProperty("Busy", BindingFlags.Public | BindingFlags.Static);
            float timeout = (float)Num(s, "timeout", 120);
            float start = Time.realtimeSinceStartup;

            while (true)
            {
                bool arrived = current != null && Equals(current.GetValue(null), activity) && (busy == null || !(bool)busy.GetValue(null));

                if (arrived)
                {
                    r.Message = "arrived in " + (Time.realtimeSinceStartup - start).ToString("0.0", CultureInfo.InvariantCulture) + " s";
                    yield break;
                }

                if (Time.realtimeSinceStartup - start > timeout)
                {
                    r.Fail("not arrived after " + timeout + " s (RrTravel.Current = " + (current != null ? current.GetValue(null) : "?") + ")");
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator StepShot(Dictionary<string, object> s, StepResult r)
        {
            string name = Str(s, "shot", "shot");
            Vector3 pos = Vec(s, "pos", new Vector3(0f, 1.6f, -3f));
            Vector3 look = Vec(s, "look", Vector3.zero);
            int w = (int)Num(s, "w", 1280);
            int h = (int)Num(s, "h", 720);
            int msaa = Mathf.Clamp((int)Num(s, "msaa", 4), 1, 8);
            bool post = Bool(s, "post", false);
            Color? bg = s.ContainsKey("bg") ? (Color?)ToColor(Vec(s, "bg", Vector3.one)) : null;

            // "frame": "room" - pos and look are relative to the current room's origin (default: world).
            string frame = Str(s, "frame", "world");
            string frameNote = "";
            if (frame == "room")
            {
                var room = SomniumSpace.Worlds.SomniumRecRoom.Rooms.RrTravel.CurrentRoom;
                if (room == null)
                {
                    r.Fail("frame \"room\" but there is no current room");
                    yield break;
                }

                Vector3 origin = room.Origin;
                pos += origin;
                look += origin;
                frameNote = "room frame (" + room.Activity + " origin " + origin.ToString("F1") + "); ";
            }
            else if (frame != "world")
            {
                r.Fail("unknown frame \"" + frame + "\" (world or room)");
                yield break;
            }

            // One frame for transforms and newly loaded textures to settle (WaitForEndOfFrame never resumes in batch mode).
            yield return null;

            var go = new GameObject("Harness Shot Camera");
            RenderTexture rt = null;
            RenderTexture resolved = null;
            Texture2D tex = null;

            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.fieldOfView = (float)Num(s, "fov", 60);
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 5000f;
                cam.allowHDR = true;

                if (bg.HasValue)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = bg.Value;
                }
                else
                {
                    cam.clearFlags = CameraClearFlags.Skybox;
                }

                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation((look - pos).sqrMagnitude > 1e-6f ? look - pos : Vector3.forward, Vector3.up);
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = post;
                data.antialiasing = post ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;

                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = msaa };
                rt.Create();
                cam.targetTexture = rt;
                var sw = Stopwatch.StartNew();
                cam.Render();
                double renderMs = sw.Elapsed.TotalMilliseconds;
                resolved = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(rt, resolved);
                var prev = RenderTexture.active;
                RenderTexture.active = resolved;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                string file = Path.Combine(OutDir, name + ".png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                _pngs.Add(file + "  (" + w + "x" + h + ", " + PixelStats(tex) + ")");
                r.Message = frameNote + file + " rendered in " + renderMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms; " + PixelStats(tex);
            }
            finally
            {
                Object.Destroy(go);

                if (rt != null)
                {
                    rt.Release();
                    Object.Destroy(rt);
                }

                if (resolved != null)
                {
                    RenderTexture.ReleaseTemporary(resolved);
                }

                if (tex != null)
                {
                    Object.Destroy(tex);
                }
            }
        }

        private static IEnumerator StepCall(Dictionary<string, object> s, StepResult r)
        {
            string spec = Str(s, "call", "");
            int dot = spec.LastIndexOf('.');

            if (dot <= 0)
            {
                r.Fail("expected Namespace.Type.Method");
                yield break;
            }

            Type t = FindType(spec.Substring(0, dot));
            MethodInfo m = t?.GetMethod(spec.Substring(dot + 1), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);

            if (m == null)
            {
                r.Fail(t == null ? "type not found" : "no static parameterless method " + spec.Substring(dot + 1));
                yield break;
            }

            object result = m.Invoke(null, null);

            if (result is IEnumerator routine)
            {
                while (routine.MoveNext())
                {
                    yield return routine.Current;
                }

                r.Message = "coroutine finished";
            }
            else
            {
                r.Message = m.ReturnType == typeof(void) ? "returned" : "returned " + (result ?? "null");
            }
        }

        private IEnumerator StepExpectLog(Dictionary<string, object> s, StepResult r)
        {
            string text = Str(s, "expect_log", "");
            float timeout = (float)Num(s, "timeout", 0);
            float start = Time.realtimeSinceStartup;

            while (true)
            {
                LogLine hit;

                lock (_lock)
                {
                    hit = _logs.FirstOrDefault(l => l.Text != null && l.Text.Contains(text) && !l.Text.StartsWith("[RrHarness] step", StringComparison.Ordinal));
                }

                if (hit != null)
                {
                    r.Message = "seen at +" + hit.T.ToString("0.00", CultureInfo.InvariantCulture) + " s: " + Short(hit.Text);
                    yield break;
                }

                if (Time.realtimeSinceStartup - start >= timeout)
                {
                    r.Fail("no log line contains '" + text + "'");
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator StepDump(Dictionary<string, object> s, StepResult r)
        {
            string name = Str(s, "dump", "world");
            string rootName = Str(s, "root", null);
            var sb = new StringBuilder();
            Transform root = null;

            if (!string.IsNullOrEmpty(rootName))
            {
                var go = GameObject.Find(rootName);
                root = go != null ? go.transform : null;
            }
            else if (_main != null)
            {
                root = _main.WorldRoot;
            }

            if (root == null)
            {
                r.Fail("root not found: " + (rootName ?? "WorldRoot"));
                yield break;
            }

            int nodes = DumpNode(root, 0, sb);
            string file = Path.Combine(OutDir, "dump_" + name + ".txt");
            File.WriteAllText(file, sb.ToString());
            r.Message = file + " (" + nodes + " nodes)";
        }

        private IEnumerator StepClear(StepResult r)
        {
            int n = 0;

            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                    n++;
                }
            }

            _spawned.Clear();
            r.Message = n + " destroyed";
            yield return null;
        }

        // ------------------------------------------------------------------ description helpers

        private static Bounds? RenderBounds(GameObject go)
        {
            Bounds b = default(Bounds);
            bool has = false;

            foreach (var rend in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!rend.enabled || rend is ParticleSystemRenderer || rend.bounds.size.sqrMagnitude < 1e-10f)
                {
                    continue;
                }

                if (!has)
                {
                    b = rend.bounds;
                    has = true;
                }
                else
                {
                    b.Encapsulate(rend.bounds);
                }
            }

            return has ? (Bounds?)b : null;
        }

        private static string Describe(GameObject go)
        {
            Bounds? b = RenderBounds(go);
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var shaders = new Dictionary<string, int>();

            foreach (var rend in renderers)
            {
                foreach (var m in rend.sharedMaterials)
                {
                    string key = m == null ? "(null material)" : (m.shader != null ? m.shader.name : "(no shader)") + (m.shader != null && !m.shader.isSupported ? " UNSUPPORTED" : "") + " q" + m.renderQueue;
                    shaders.TryGetValue(key, out int n);
                    shaders[key] = n + 1;
                }
            }

            return "at " + go.transform.position.ToString("F2") + (b.HasValue ? ", bounds size " + b.Value.size.ToString("F2") + " min y " + b.Value.min.y.ToString("F2", CultureInfo.InvariantCulture) : ", no visible renderer") +
                   ", " + go.GetComponentsInChildren<Transform>(true).Length + " nodes, " + renderers.Length + " renderers, " + go.GetComponentsInChildren<Collider>(true).Length + " colliders, " +
                   go.GetComponentsInChildren<Rigidbody>(true).Length + " rigidbodies, layer " + go.layer + "; materials: " + string.Join("; ", shaders.Select(kv => kv.Key + " x" + kv.Value).ToArray());
        }

        private static int DumpNode(Transform t, int depth, StringBuilder sb)
        {
            int n = 1;
            sb.Append(' ', depth * 2).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (inactive)").Append("  L").Append(t.gameObject.layer)
              .Append(" pos ").Append(t.position.ToString("F2")).Append('\n');

            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null)
                {
                    sb.Append(' ', depth * 2 + 4).Append("(missing script)\n");
                    continue;
                }

                if (c is Transform)
                {
                    continue;
                }

                sb.Append(' ', depth * 2 + 4).Append(c.GetType().Name);
                var rend = c as Renderer;

                if (rend != null)
                {
                    sb.Append(rend.enabled ? "" : " (disabled)").Append(": ");
                    sb.Append(string.Join(", ", rend.sharedMaterials.Select(m => m == null ? "null" : m.name + " [" + (m.shader != null ? m.shader.name : "?") + " q" + m.renderQueue + "]").ToArray()));
                }

                sb.Append('\n');
            }

            for (int i = 0; i < t.childCount; i++)
            {
                n += DumpNode(t.GetChild(i), depth + 1, sb);
            }

            return n;
        }

        private static string PixelStats(Texture2D tex)
        {
            Color32[] px = tex.GetPixels32();
            double sum = 0, sum2 = 0;
            int n = 0;

            for (int i = 0; i < px.Length; i += 7)
            {
                double l = 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
                sum += l;
                sum2 += l * l;
                n++;
            }

            double mean = sum / Math.Max(1, n);
            double sd = Math.Sqrt(Math.Max(0, sum2 / Math.Max(1, n) - mean * mean));
            return "mean luma " + mean.ToString("0", CultureInfo.InvariantCulture) + ", sd " + sd.ToString("0", CultureInfo.InvariantCulture) + (sd < 3 ? " (BLANK?)" : "");
        }

        // ------------------------------------------------------------------ summary

        private void OnLog(string text, string stack, LogType type)
        {
            lock (_lock)
            {
                _logs.Add(new LogLine { T = _clock.Elapsed.TotalSeconds, Type = type, Text = text, Stack = stack });
            }
        }

        private void WriteSummary()
        {
            try
            {
                Directory.CreateDirectory(OutDir);
                List<LogLine> logs;

                lock (_lock)
                {
                    logs = _logs.ToList();
                }

                var sb = new StringBuilder();
                var ic = CultureInfo.InvariantCulture;
                sb.AppendLine("RrScenario '" + Name + "'  " + ScenarioPath);
                sb.AppendLine("started " + _started.ToString("yyyy-MM-dd HH:mm:ss", ic) + ", total " + _clock.Elapsed.TotalSeconds.ToString("0.0", ic) + " s; Unity " + Application.unityVersion + ", " + SystemInfo.graphicsDeviceType + " '" + SystemInfo.graphicsDeviceName + "', rp " +
                              (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "builtin") + ", " + QualitySettings.activeColorSpace);
                sb.AppendLine("environment: " + (_environment.Length > 0 ? _environment : "-"));
                sb.AppendLine("output: " + OutDir);
                sb.AppendLine();
                sb.AppendLine("== Result: " + (BootFailure != null ? "BOOT FAILED - " + BootFailure : "booted; " + _steps.Count(x => x.Status == "FAIL") + " step failure(s), " + _steps.Count(x => x.Status == "skip") + " skipped, of " + _steps.Count));
                sb.AppendLine();
                sb.AppendLine("== Timings");
                sb.AppendLine("  RrMain.Ready: " + (_readyAt >= 0 ? "+" + _readyAt.ToString("0.00", ic) + " s" : "never"));

                // first line of each [SomniumRecRoom] area = the boot phases
                var firstByArea = new List<string>();
                var seenAreas = new HashSet<string>();

                foreach (var l in logs)
                {
                    if (l.Text == null || !l.Text.StartsWith(RrLog.Prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string rest = l.Text.Substring(RrLog.Prefix.Length);
                    int colon = rest.IndexOf(':');
                    string area = colon > 0 ? rest.Substring(0, colon) : rest;

                    if (seenAreas.Add(area))
                    {
                        firstByArea.Add("  +" + l.T.ToString("0.00", ic).PadLeft(7) + " s  first " + area + ": " + Short(rest.Substring(Math.Min(rest.Length, colon + 1)).Trim()));
                    }
                }

                foreach (var line in firstByArea)
                {
                    sb.AppendLine(line);
                }

                sb.AppendLine("  longest frame of the run: " + _runMaxFrame.ToString("0.0", ic) + " ms");
                sb.AppendLine();
                sb.AppendLine("== Steps");

                foreach (var st in _steps)
                {
                    sb.AppendLine("  #" + st.Index + " " + st.Kind + " " + st.Arg + " -> " + st.Status + " (" + st.Ms.ToString("0", ic) + " ms, " + st.Frames + " frames, max frame " + st.MaxFrameMs.ToString("0.0", ic) + " ms)" +
                                  (st.Message.Length > 0 ? "\n      " + st.Message : ""));
                }

                sb.AppendLine();
                sb.AppendLine("== PNG (" + _pngs.Count + ")");

                foreach (var p in _pngs)
                {
                    sb.AppendLine("  " + p);
                }

                sb.AppendLine();
                sb.AppendLine("== Systems");

                try
                {
                    sb.AppendLine("  assets: " + (RrAssets.Instance != null ? RrAssets.Instance.Describe() : "(no RrAssets)"));
                    sb.AppendLine("  " + RrMaterials.Describe() + "; spawned " + RrSpawn.Spawned);
                }
                catch (Exception e)
                {
                    sb.AppendLine("  (describe failed: " + e.Message + ")");
                }

                var errors = logs.Where(l => l.Type == LogType.Error || l.Type == LogType.Exception || l.Type == LogType.Assert).ToList();
                sb.AppendLine();
                sb.AppendLine("== Errors and exceptions (" + errors.Count + ")");

                foreach (var g in errors.GroupBy(l => l.Type + ": " + FirstLines(l.Text, 1)))
                {
                    var e = g.First();
                    sb.AppendLine("  +" + e.T.ToString("0.00", ic) + " s " + (g.Count() > 1 ? "(x" + g.Count() + ") " : "") + g.Key);

                    if (e.Type == LogType.Exception || !e.Text.StartsWith(RrLog.Prefix, StringComparison.Ordinal))
                    {
                        string stack = FirstLines(e.Stack, 4);

                        if (stack.Length > 0)
                        {
                            sb.AppendLine("      " + stack.Replace("\n", "\n      "));
                        }
                    }
                }

                var warnings = logs.Where(l => l.Type == LogType.Warning && (l.Text == null || !l.Text.StartsWith(RrLog.Prefix, StringComparison.Ordinal))).ToList();

                // Rec Room's own scripts are missing scripts here by design; Unity warns for each one it loads.
                int missing = warnings.Count(l => l.Text != null && l.Text.StartsWith("The referenced script", StringComparison.Ordinal) && l.Text.Contains("is missing"));
                warnings = warnings.Where(l => l.Text == null || !(l.Text.StartsWith("The referenced script", StringComparison.Ordinal) && l.Text.Contains("is missing"))).ToList();
                sb.AppendLine();
                sb.AppendLine("== Other warnings (not [SomniumRecRoom]): " + missing + " 'referenced script is missing' (Rec Room's inert scripts, expected) + " + warnings.Count + " others, first 25 distinct:");

                foreach (var g in warnings.GroupBy(l => FirstLines(l.Text, 1)).Take(25))
                {
                    sb.AppendLine("  " + (g.Count() > 1 ? "(x" + g.Count() + ") " : "") + Short(g.Key));
                }

                var rr = logs.Where(l => l.Text != null && (l.Text.StartsWith(RrLog.Prefix, StringComparison.Ordinal) || l.Text.StartsWith("[RrHarness]", StringComparison.Ordinal))).ToList();
                sb.AppendLine();
                sb.AppendLine("== [SomniumRecRoom] log (" + rr.Count(l => l.Text.StartsWith(RrLog.Prefix, StringComparison.Ordinal)) + " lines, with [RrHarness] markers)");

                foreach (var l in rr)
                {
                    string tag = l.Type == LogType.Log ? "  " : l.Type == LogType.Warning ? "W " : "E ";
                    sb.AppendLine("  +" + l.T.ToString("0.00", ic).PadLeft(7) + " " + tag + l.Text.Replace("\n", "\n            "));
                }

                File.WriteAllText(Path.Combine(OutDir, "summary.txt"), sb.ToString());
                Debug.Log("[RrHarness] summary written: " + Path.Combine(OutDir, "summary.txt"));
            }
            catch (Exception e)
            {
                Debug.LogError("[RrHarness] summary failed: " + e);
            }
        }

        // ------------------------------------------------------------------ small helpers

        internal static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = assembly.GetType(fullName, false);

                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        private static string Str(Dictionary<string, object> d, string key, string fallback)
        {
            return d.TryGetValue(key, out object v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : fallback;
        }

        private static double Num(Dictionary<string, object> d, string key, double fallback)
        {
            return d.TryGetValue(key, out object v) && v is double ? (double)v : fallback;
        }

        private static bool Bool(Dictionary<string, object> d, string key, bool fallback)
        {
            return d.TryGetValue(key, out object v) && v is bool ? (bool)v : fallback;
        }

        private static Vector3 Vec(Dictionary<string, object> d, string key, Vector3 fallback)
        {
            if (!d.TryGetValue(key, out object v) || !(v is List<object> l) || l.Count < 3)
            {
                return fallback;
            }

            return new Vector3(Convert.ToSingle(l[0], CultureInfo.InvariantCulture), Convert.ToSingle(l[1], CultureInfo.InvariantCulture), Convert.ToSingle(l[2], CultureInfo.InvariantCulture));
        }

        private static Color ToColor(Vector3 v)
        {
            // accepts 0-1 or 0-255
            float k = v.x > 1f || v.y > 1f || v.z > 1f ? 1f / 255f : 1f;
            return new Color(v.x * k, v.y * k, v.z * k);
        }

        private static string Short(object o)
        {
            string s = o is List<object> l ? "[" + string.Join(",", l.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)).ToArray()) + "]" : Convert.ToString(o, CultureInfo.InvariantCulture) ?? "null";
            s = s.Replace("\n", " | ");
            return s.Length > 300 ? s.Substring(0, 300) + "..." : s;
        }

        private static string FirstLines(string s, int n)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }

            var lines = s.Replace("\r", "").Split('\n').Where(x => x.Trim().Length > 0).Take(n).ToArray();
            return string.Join("\n", lines);
        }
    }
}
