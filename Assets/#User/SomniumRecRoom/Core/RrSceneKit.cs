using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rebuilds a scene placement ("RRSK 1" text, see <see cref="RrSceneData"/>) from the player's own Rec Room
    /// install, under a parent, as a coroutine. Nothing of Rec Room ships with the world: the text names install
    /// meshes, materials, prefabs and clips, and positions them.
    ///
    /// Steps, each spread over frames (at most <see cref="RrSceneKitOptions.FrameBudgetMs"/> of main-thread work
    /// before a yield):
    ///
    /// 1. Parse on the worker (and inflate an RRSZ1 payload, <see cref="RrSceneKitData"/>).
    /// 2. Load every mesh, material and prefab the kept records name, through <see cref="RrAssets"/> by exact
    ///    name (catalog lookup first, so a name the install lacks costs nothing and no warning), 24 in flight,
    ///    60 s at most (inside RrTravel's 90 s room-build limit).
    ///    Materials go through <see cref="RrMaterials.Fix"/> once each.
    /// 3. Renderers. A scene is thousands of static renderers (the REF2 quest acts hold 450-3,850, Rec Rally
    ///    8,000); one object and one draw each would cost VR its frame. Readable install meshes (24 of a 28-mesh
    ///    sample) are merged with Mesh.CombineMeshes per material, vertex layout (the attribute set: merging a mesh
    ///    without vertex colours into one with them would give it default colours) and 32 m cell (so the far
    ///    parts of a big map are culled), up to 60,000 vertices per merged mesh. Unreadable meshes, mirrored
    ///    placements (negative scale flips the winding) and meshes too big to merge keep their own renderer
    ///    sharing the install mesh. Shadows on (iteration 8: Rec Room's levels are realtime-lit, no lightmaps).
    ///    A scene without a key light but with many point lights (a quest area) keeps its renderers apart when it
    ///    has at most 400, else merges in 6 m cells (URP forward lights an object with at most 4 of them, the
    ///    client's per-object limit, so a hall-sized merged mesh would lose the torches).
    /// 4. Prefabs (P) through <see cref="RrSpawn.SpawnLoaded"/> with Scenery options (environment layer, kinematic).
    /// 5. Stand-ins (X) as merged boxes and cylinders in <see cref="RrLook.Solid"/> colours (Glass if translucent),
    ///    after the prefabs so a stand-in that repeats an install renderer or prefab can be left out.
    /// 6. Colliders on <see cref="RrLayers.Environment"/>. Boxes with the same rotation share one object (one
    ///    static body, many shapes: a map's 1-5k boxes become tens of objects); mesh colliders are pre-cooked on
    ///    the worker with Physics.BakeMesh (thread safe) so assigning them does not cook on the main thread.
    /// 7. Lights: point and spot only (the key light is <see cref="RrEnvironment"/>'s sun); as many as Rec Room ran
    ///    (<see cref="RrSceneKitOptions.EnvironmentLights"/>), the ones it shadowed with soft shadows. Audio: looping
    ///    sources through <see cref="RrSound.Loop"/>, loudest first, capped; one-shot sources become "audio" markers.
    /// 8. Environment: the finished scene registers with <see cref="RrEnvironment"/>, which applies its lighting
    ///    (fog, ambient, sun, sky) while the player is inside it. The old per-root ApplyAmbient/ApplySky still work.
    ///
    /// Overlaps (iteration 8, <see cref="RrSceneKitOptions.FixOverlaps"/>): exact duplicate records are built once;
    /// a stand-in or a "dress" marker whose box is the same object as an install renderer or prefab (IoU of the
    /// world boxes at least 0.5) is left out; stand-in faces are inset by 3-6 mm (varying per box) so a face that
    /// lies on install art or on another stand-in cannot z-fight. The counts are in the build's summary line.
    /// 9. Level effects (F, iteration 5): one clean template per install effect (a prefab root or a child of one),
    ///    then <see cref="RrSceneKitFx"/> places the records when the room is built (a wall torch's flame only on a
    ///    wall the room has) and keeps the nearest 40 running and their nearest 8 loops playing.
    /// 10. Gameplay markers (G) are returned in <see cref="RrSceneKitResult.Markers"/>, posed in the parent.
    ///
    /// One summary line is logged per build. The kit's root carries <see cref="RrSceneKitRoot"/>, which destroys
    /// the merged meshes with the room; install assets stay RrAssets' (freed with the room's scope).
    /// </summary>
    public static class RrSceneKit
    {
        public const string Area = "SceneKit";

        private static readonly List<RrSceneKitRoot> Live = new List<RrSceneKitRoot>();
        private static int _builds;

        /// <summary>Builds finished since the world started.</summary>
        public static int Builds
        {
            get { return _builds; }
        }

        /// <summary>Scenes built and not destroyed yet.</summary>
        public static int LiveScenes
        {
            get { return Live.Count; }
        }

        /// <summary>The last build's result (diagnostics and harness).</summary>
        public static RrSceneKitResult Last { get; private set; }

        /// <summary>
        /// Builds RRSK text (or an RRSZ1 payload) under parent; done gets the result (also on failure; never
        /// null). Run it with StartCoroutine or yield it from a room's Build.
        /// </summary>
        public static IEnumerator Build(string rrsk, Transform parent, RrSceneKitOptions options, Action<RrSceneKitResult> done)
        {
            CheckWorld();
            return new Job(rrsk, null, parent, options, done).Run();
        }

        /// <summary>Builds already parsed data (parse once, build a scene more than once or read markers first).</summary>
        public static IEnumerator Build(RrSceneData data, Transform parent, RrSceneKitOptions options, Action<RrSceneKitResult> done)
        {
            CheckWorld();
            return new Job(null, data, parent, options, done).Run();
        }

        /// <summary>Parses on the worker (inline if there is none); done gets the data, or null and the error text.</summary>
        public static IEnumerator Parse(string rrsk, Action<RrSceneData, string> done)
        {
            RrSceneData data = null;
            string error = null;
            var worker = RrMain.Instance != null ? RrMain.Instance.Worker : null;
            if (worker != null)
            {
                bool finished = false;
                worker.Post(() => RrSceneData.Parse(rrsk), (result, e) =>
                {
                    data = result as RrSceneData;
                    error = e != null ? e.GetType().Name + ": " + e.Message : null;
                    finished = true;
                });
                while (!finished)
                {
                    yield return null;
                }
            }
            else
            {
                try
                {
                    data = RrSceneData.Parse(rrsk);
                }
                catch (Exception e)
                {
                    error = e.GetType().Name + ": " + e.Message;
                }
            }

            if (data == null && error == null)
            {
                error = "no data";
            }

            if (done != null)
            {
                done(data, error);
            }
        }

        internal static void Register(RrSceneKitRoot root)
        {
            if (root != null && !Live.Contains(root))
            {
                Live.Add(root);
            }
        }

        internal static void Forget(RrSceneKitRoot root)
        {
            Live.Remove(root);
        }

        // The RrMain the statics belong to: a new one (world re-entry) clears them even if nobody called Shutdown.
        private static RrMain _world;

        private static void CheckWorld()
        {
            if (!ReferenceEquals(_world, RrMain.Instance))
            {
                Shutdown();
                _world = RrMain.Instance;
            }
        }

        /// <summary>Clears the statics (RrMain.OnDestroy; the assembly survives a world re-entry).</summary>
        public static void Shutdown()
        {
            Live.Clear();
            Last = null;
            _builds = 0;
            _world = null;
        }

        /// <summary>
        /// Harness before/after: every live kit scene's renderers cast shadows (true, iteration 8) or not (false, as
        /// iteration 7 built them). Returns the renderers changed.
        /// </summary>
        public static int SetShadows(bool on)
        {
            int n = 0;
            foreach (var root in Live)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    mr.shadowCastingMode = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    n++;
                }
            }

            return n;
        }

        /// <summary>A record's identity for the duplicate test: kind, a number (mesh index / shape / name hash), pose to 1 mm and 0.001, scale to 1 mm.</summary>
        private struct PoseKey : IEquatable<PoseKey>
        {
            private int _k, _n, _x, _y, _z, _qx, _qy, _qz, _qw, _sx, _sy, _sz;

            public static PoseKey Of(int kind, int number, Vector3 p, Quaternion q, Vector3 s)
            {
                if (q.w < 0f)
                {
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                }

                return new PoseKey
                {
                    _k = kind, _n = number,
                    _x = Mathf.RoundToInt(p.x * 1000f), _y = Mathf.RoundToInt(p.y * 1000f), _z = Mathf.RoundToInt(p.z * 1000f),
                    _qx = Mathf.RoundToInt(q.x * 1000f), _qy = Mathf.RoundToInt(q.y * 1000f), _qz = Mathf.RoundToInt(q.z * 1000f), _qw = Mathf.RoundToInt(q.w * 1000f),
                    _sx = Mathf.RoundToInt(s.x * 1000f), _sy = Mathf.RoundToInt(s.y * 1000f), _sz = Mathf.RoundToInt(s.z * 1000f)
                };
            }

            public bool Equals(PoseKey o)
            {
                return _k == o._k && _n == o._n && _x == o._x && _y == o._y && _z == o._z && _qx == o._qx && _qy == o._qy && _qz == o._qz && _qw == o._qw && _sx == o._sx && _sy == o._sy && _sz == o._sz;
            }

            public override bool Equals(object obj)
            {
                return obj is PoseKey && Equals((PoseKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = _k;
                    h = h * 397 ^ _n;
                    h = h * 397 ^ _x;
                    h = h * 397 ^ _y;
                    h = h * 397 ^ _z;
                    h = h * 397 ^ _qx;
                    h = h * 397 ^ _qy;
                    h = h * 397 ^ _qz;
                    h = h * 397 ^ _qw;
                    h = h * 397 ^ _sx;
                    h = h * 397 ^ _sy;
                    h = h * 397 ^ _sz;
                    return h;
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------

        private sealed class PrefabAsset
        {
            public GameObject Go;
            public RrPrefabData Data;
        }

        private sealed class Batch
        {
            public Material Material;
            public readonly List<CombineInstance> Items = new List<CombineInstance>();
            public int Vertices;       // of the submeshes merged
            public int FullVertices;   // of the whole source meshes (guards the index format)
            public int Made;
        }

        private sealed class ShapeBatch
        {
            public Material Material;
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> T = new List<int>();
            public int Made;
        }

        private sealed class ColliderHost
        {
            public GameObject Go;
            public Quaternion Inverse;
            public int Count;
        }

        private sealed class Job
        {
            private readonly string _text;
            private RrSceneData _d;
            private readonly Transform _parent;
            private readonly bool _hadParent;
            private readonly RrSceneKitOptions _o;
            private readonly Action<RrSceneKitResult> _done;
            private readonly RrSceneKitResult _r = new RrSceneKitResult();

            private readonly Stopwatch _total = new Stopwatch();
            private readonly Stopwatch _slice = new Stopwatch();
            private double _work;
            private bool _stop;

            private GameObject _root;
            private RrSceneKitRoot _host;
            private Transform _renderersT;
            private Transform _collidersT;
            private Transform _prefabsT;
            private Transform _lightsT;
            private Transform _audioT;

            // kept records
            private readonly List<int> _rend = new List<int>();
            private readonly List<int> _stand = new List<int>();
            private readonly List<int> _pref = new List<int>();
            private readonly List<int> _col = new List<int>();
            private readonly List<int> _fx = new List<int>();
            private readonly System.Text.StringBuilder _loopsText = new System.Text.StringBuilder();

            // assets by table index / name
            private Mesh[] _meshes;
            private Material[] _materials;
            private int[] _masks;
            private bool[] _meshWanted;
            private bool[] _materialWanted;
            private readonly Dictionary<string, PrefabAsset> _prefabs = new Dictionary<string, PrefabAsset>();
            private Material _skyMaterial;
            private Material _fallback;
            private int _inFlight;

            // lighting and overlaps (iteration 8)
            private float _cellSize;
            private bool _separateAll;
            private RrEnvironmentSetting _env;
            private Bounds _core;                  // the placed records' bounds (scene space = the root's space)
            private bool _hasCore;
            private readonly List<Bounds> _art = new List<Bounds>();                        // world boxes of install renderers and prefabs
            private readonly Dictionary<long, List<int>> _artGrid = new Dictionary<long, List<int>>();
            private int[] _artStamp = new int[0];
            private int _stampNow;
            private readonly HashSet<PoseKey> _dressSeen = new HashSet<PoseKey>();

            public Job(string text, RrSceneData data, Transform parent, RrSceneKitOptions options, Action<RrSceneKitResult> done)
            {
                _text = text;
                _d = data;
                _parent = parent;
                _hadParent = !ReferenceEquals(parent, null);
                _o = options ?? new RrSceneKitOptions();
                _done = done;
            }

            // ---- budget and cancellation ----

            private bool Over
            {
                get
                {
                    if (!_slice.IsRunning)
                    {
                        _slice.Start();
                    }

                    return _slice.Elapsed.TotalMilliseconds >= _o.FrameBudgetMs;
                }
            }

            /// <summary>Ends a slice of main-thread work (the caller yields null next).</summary>
            private object Pause()
            {
                double ms = _slice.Elapsed.TotalMilliseconds;
                _work += ms;
                if (ms > _r.LongestStepMs)
                {
                    _r.LongestStepMs = (float)ms;
                }

                _slice.Reset();
                _r.Frames++;
                return null;
            }

            /// <summary>After a yield: starts the next slice; false (and the build stops) if the parent or root is gone.</summary>
            private bool Resume()
            {
                _slice.Start();
                bool gone = (_hadParent && _parent == null) || (!ReferenceEquals(_root, null) && _root == null);
                if (gone && !_stop)
                {
                    _stop = true;
                    _r.Cancelled = true;
                    _r.Error = "the parent or the scene root was destroyed during the build";
                }

                return !_stop;
            }

            private void Fail(string error)
            {
                _stop = true;
                if (_r.Error == null)
                {
                    _r.Error = error;
                }
            }

            // ---- the build ----

            public IEnumerator Run()
            {
                _total.Start();
                _slice.Start();
                var phases = new Func<IEnumerator>[] { ParsePhase, SelectPhase, LoadPhase, RendererPhase, PrefabPhase, StandInPhase, ColliderPhase, LightAudioEnvPhase, EffectPhase, MarkerPhase };
                for (int i = 0; i < phases.Length && !_stop; i++)
                {
                    IEnumerator e = null;
                    try
                    {
                        e = phases[i]();
                    }
                    catch (Exception ex)
                    {
                        RrLog.Error(Area, "phase " + i + " failed to start", ex);
                        Fail(ex.GetType().Name + ": " + ex.Message);
                        break;
                    }

                    while (!_stop)
                    {
                        bool more;
                        try
                        {
                            more = e.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            RrLog.Error(Area, (_d != null ? _d.Name : "?") + ": build phase " + i + " threw", ex);
                            Fail(ex.GetType().Name + ": " + ex.Message);
                            break;
                        }

                        if (!more)
                        {
                            break;
                        }

                        yield return e.Current;
                    }
                }

                Finish();
            }

            private void Finish()
            {
                _work += _slice.Elapsed.TotalMilliseconds;
                _slice.Reset();
                _r.WorkMs = (float)_work;
                _r.BuildMs = (float)_total.Elapsed.TotalMilliseconds;
                if (_stop && _root != null && _r.Cancelled)
                {
                    Object.Destroy(_root);
                    _root = null;
                }

                _r.Root = _root;
                _builds++;
                Last = _r;
                if (_root != null && !_stop && _host != null && _d != null)
                {
                    try
                    {
                        RrEnvironment.SceneBuilt(_host, _d, _hasCore ? _core : _d.Bounds);
                    }
                    catch (Exception e)
                    {
                        RrLog.Error(Area, _r.Scene + ": registering the scene's lighting failed", e);
                    }
                }
                if (_r.Error != null && !_r.Cancelled)
                {
                    RrLog.Warn(Area, _r.Summary());
                }
                else
                {
                    RrLog.Info(Area, _r.Summary());
                }

                if (_done != null)
                {
                    try
                    {
                        _done(_r);
                    }
                    catch (Exception e)
                    {
                        RrLog.Error(Area, "the build's callback threw", e);
                    }
                }
            }

            // 1. parse
            private IEnumerator ParsePhase()
            {
                if (_d == null)
                {
                    var sw = Stopwatch.StartNew();
                    RrSceneData parsed = null;
                    string error = null;
                    var parse = RrSceneKit.Parse(_text, (data, e) =>
                    {
                        parsed = data;
                        error = e;
                    });
                    while (parse.MoveNext())
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }
                    }

                    _r.ParseMs = (float)sw.Elapsed.TotalMilliseconds;
                    if (parsed == null)
                    {
                        _r.Scene = "?";
                        Fail("parse failed: " + error);
                        yield break;
                    }

                    _d = parsed;
                }

                _r.Data = _d;
                _r.Scene = string.IsNullOrEmpty(_d.Name) ? "(unnamed)" : _d.Name;
                if (_d.Bad > 0)
                {
                    RrLog.Warn(Area, _r.Scene + ": " + _d.Bad + " malformed line(s), first: " + (_d.Problems.Count > 0 ? _d.Problems[0] : "?"));
                }

                if (_d.Version != 1)
                {
                    RrLog.Warn(Area, _r.Scene + ": header says RRSK version " + _d.Version + " (this kit reads 1)");
                }
            }

            // 2. root and the records to build
            private IEnumerator SelectPhase()
            {
                if (_hadParent && _parent == null)
                {
                    Fail("the parent was destroyed before the build started");
                    _r.Cancelled = true;
                    yield break;
                }

                _root = new GameObject(string.IsNullOrEmpty(_o.RootName) ? "Rr Scene " + _r.Scene : _o.RootName);
                _root.layer = RrLayers.Environment;
                _root.transform.SetParent(_parent, false);
                _root.transform.localPosition = _o.Position;
                _root.transform.localRotation = _o.Rotation;
                _host = _root.AddComponent<RrSceneKitRoot>();
                _host.Result = _r;
                Register(_host);
                _renderersT = Group("Renderers");
                _collidersT = Group("Colliders");
                _prefabsT = Group("Prefabs");
                _lightsT = Group("Lights");
                _audioT = Group("Audio");

                _r.SceneBounds = _d.Bounds;
                _r.Bounds = Offset(_d.Bounds);

                _meshes = new Mesh[_d.Meshes.Count];
                _masks = new int[_d.Meshes.Count];
                _meshWanted = new bool[_d.Meshes.Count];
                _materials = new Material[_d.Materials.Count];
                _materialWanted = new bool[_d.Materials.Count];
                for (int i = 0; i < _masks.Length; i++)
                {
                    _masks[i] = -1;
                }

                // Higher LOD levels whose LOD0 is in the same table.
                bool[] higherLod = new bool[_d.Meshes.Count];
                if (_o.DropHigherLods)
                {
                    var names = new HashSet<string>();
                    foreach (var n in _d.Meshes)
                    {
                        if (n != null)
                        {
                            names.Add(n);
                        }
                    }

                    for (int i = 0; i < _d.Meshes.Count; i++)
                    {
                        higherLod[i] = IsHigherLod(_d.Meshes[i], names);
                    }
                }

                // The same record twice (same mesh, pose and scale; same stand-in box; same prefab and pose) is built
                // once: two copies draw on top of each other and z-fight.
                var seen = _o.FixOverlaps ? new HashSet<PoseKey>() : null;
                var nearR = new Dictionary<long, List<NearItem>>();
                var nearX = new Dictionary<long, List<NearItem>>();
                if (!_o.SkipRenderers)
                {
                    for (int i = 0; i < _d.Renderers.Count; i++)
                    {
                        var r = _d.Renderers[i];
                        string mesh = _d.MeshName(r.Mesh);
                        if (mesh == null || !Inside(r.Position) || Skipped(mesh))
                        {
                            _r.SkippedRenderers++;
                            continue;
                        }

                        if (higherLod[r.Mesh])
                        {
                            _r.LodsDropped++;
                            continue;
                        }

                        if (seen != null && (!seen.Add(PoseKey.Of(1, r.Mesh, r.Position, r.Rotation, r.Scale)) || Near(nearR, r.Mesh, r.Position, r.Rotation, r.Scale)))
                        {
                            _r.DuplicateRenderers++;
                            continue;
                        }

                        Core(r.Position);
                        _rend.Add(i);
                        _meshWanted[r.Mesh] = true;
                        for (int k = 0; k < r.MaterialCount; k++)
                        {
                            int m = _d.MaterialIndices[r.MaterialStart + k];
                            if (m >= 0 && m < _materialWanted.Length)
                            {
                                _materialWanted[m] = true;
                            }
                        }
                    }
                }

                if (!_o.SkipStandIns)
                {
                    for (int i = 0; i < _d.StandIns.Count; i++)
                    {
                        var x = _d.StandIns[i];
                        if (!Inside(x.Center))
                        {
                            continue;
                        }

                        if (seen != null && (!seen.Add(PoseKey.Of(2, x.Cylinder ? 1 : 0, x.Center, x.Rotation, x.Extents)) || Near(nearX, x.Cylinder ? 1 : 0, x.Center, x.Rotation, x.Extents)))
                        {
                            _r.DuplicateStandIns++;
                            continue;
                        }

                        Core(x.Center);
                        _stand.Add(i);
                    }
                }

                if (!_o.SkipPrefabs)
                {
                    for (int i = 0; i < _d.Prefabs.Count; i++)
                    {
                        var p = _d.Prefabs[i];
                        if (!Inside(p.Position) || Skipped(p.Name))
                        {
                            continue;
                        }

                        if (seen != null && !seen.Add(PoseKey.Of(3, p.Name != null ? p.Name.GetHashCode() : 0, p.Position, p.Rotation, p.Scale)))
                        {
                            _r.DuplicatePrefabs++;
                            continue;
                        }

                        Core(p.Position);
                        _pref.Add(i);
                    }
                }

                // Rec Room's own light rig for this scene (RrEnvironment's table, else the scene's records), and the merge
                // cell: URP forward lights an object with at most 4 additional lights (the client's per-object limit), so a
                // scene that keeps many point lights merges in small cells, each lit by the torches near it.
                _env = _o.EnvironmentLights ? RrEnvironment.ForScene(_d) : null;
                _cellSize = _o.CellSize;
                // Iteration 9: with Rec Room's light grid its shaders pick up to 8 lights per cell of about 2 m, per pixel,
                // whatever the mesh size, so merged meshes keep their torches: no small cells or separate renderers.
                if (!_o.SkipLights && !_o.LightGrid && _o.LightCellSize > 0f && (_cellSize <= 0f || _cellSize > _o.LightCellSize * 1.5f))
                {
                    int lit = 0;
                    foreach (var l in _d.Lights)
                    {
                        if (l.Type != LightType.Directional && Inside(l.Position))
                        {
                            lit++;
                        }
                    }

                    // Only where the lights ARE the lighting (no key light: the quests, Charades); under a sun the point lights
                    // are accents and a map-wide 10 m grid would multiply the draws (Spillway: 145 lights over 300 m).
                    if (Mathf.Min(lit, LightBudget(lit)) > _o.LightCellThreshold && (_env == null || !_env.HasSun))
                    {
                        _cellSize = _o.LightCellSize;

                        // A small torch-lit room keeps Rec Room's own renderers apart (the Android levels have no static
                        // batching): each then takes its own 4 nearest lights, as in Rec Room, instead of 4 per merged cell
                        // (harness run 5: the Golden Trophy foyer, 168 renderers and 40 lights, drew as a dark hall).
                        if (_rend.Count <= _o.LightSeparateMax)
                        {
                            _separateAll = true;
                        }
                    }
                }

                _r.CellSize = _cellSize;

                if (!_o.SkipEffects)
                {
                    for (int i = 0; i < _d.Effects.Count; i++)
                    {
                        var e = _d.Effects[i];
                        if (Inside(e.Position) && !Skipped(e.Prefab))
                        {
                            _fx.Add(i);
                        }
                    }
                }

                if (!_o.SkipColliders)
                {
                    for (int i = 0; i < _d.Colliders.Count; i++)
                    {
                        var c = _d.Colliders[i];
                        if (!Inside(c.Center))
                        {
                            continue;
                        }

                        if (c.Shape == RrSceneData.ColliderShape.Mesh)
                        {
                            string mesh = _d.MeshName(c.Mesh);
                            if (_o.SkipMeshColliders || mesh == null || Skipped(mesh))
                            {
                                continue;
                            }

                            _meshWanted[c.Mesh] = true;
                        }

                        _col.Add(i);
                    }
                }

                yield break;
            }

            // 3. assets
            private IEnumerator LoadPhase()
            {
                var assets = RrAssets.Instance;
                if (assets == null)
                {
                    RrLog.Warn(Area, _r.Scene + ": no RrAssets - install meshes, materials and prefabs are skipped");
                    yield break;
                }

                var sw = Stopwatch.StartNew();
                // Requests: kind 0 mesh, 1 material, 2 prefab, 3 sky material.
                var kinds = new List<byte>();
                var indices = new List<int>();
                var entries = new List<RrEntry>();
                for (int i = 0; i < _meshWanted.Length; i++)
                {
                    if (_meshWanted[i])
                    {
                        Want(assets, 0, i, _d.Meshes[i], 43, kinds, indices, entries, _r.MissingMeshes);
                    }
                }

                for (int i = 0; i < _materialWanted.Length; i++)
                {
                    if (_materialWanted[i] && _d.Materials[i] != null)
                    {
                        Want(assets, 1, i, _d.Materials[i], 21, kinds, indices, entries, _r.MissingMaterials);
                    }
                }

                var prefabNames = new List<string>();
                foreach (int i in _pref)
                {
                    string n = _d.Prefabs[i].Name;
                    if (!_prefabs.ContainsKey(n))
                    {
                        _prefabs[n] = null;
                        if (Want(assets, 2, prefabNames.Count, n, 1, kinds, indices, entries, _r.MissingPrefabs))
                        {
                            prefabNames.Add(n);
                        }
                    }
                }

                // Effect prefabs (F) load with the P prefabs; a name both use loads once.
                foreach (int i in _fx)
                {
                    string n = _d.Effects[i].Prefab;
                    if (!_prefabs.ContainsKey(n))
                    {
                        _prefabs[n] = null;
                        if (Want(assets, 2, prefabNames.Count, n, 1, kinds, indices, entries, _r.MissingEffects))
                        {
                            prefabNames.Add(n);
                        }
                    }
                }

                if (_o.ApplySky && !string.IsNullOrEmpty(_d.Sky))
                {
                    Want(assets, 3, 0, _d.Sky, 21, kinds, indices, entries, _r.MissingMaterials);
                }

                float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, _o.LoadTimeout);
                int next = 0;
                while (next < kinds.Count || _inFlight > 0)
                {
                    while (_inFlight < Mathf.Max(1, _o.MaxConcurrentLoads) && next < kinds.Count)
                    {
                        Start(assets, kinds[next], indices[next], entries[next], prefabNames);
                        next++;
                    }

                    if (next >= kinds.Count && _inFlight == 0)
                    {
                        break;
                    }

                    if (Time.realtimeSinceStartup > deadline)
                    {
                        RrLog.Warn(Area, _r.Scene + ": asset loads timed out after " + _o.LoadTimeout + " s (" + _inFlight + " in flight, " + (kinds.Count - next) + " not started); building with what arrived");
                        break;
                    }

                    yield return Pause();
                    if (!Resume())
                    {
                        yield break;
                    }
                }

                _r.LoadMs = (float)sw.Elapsed.TotalMilliseconds;
            }

            private bool Want(RrAssets assets, byte kind, int index, string name, int classId, List<byte> kinds, List<int> indices, List<RrEntry> entries, List<string> missing)
            {
                RrEntry e;
                if (IsUnknown(name))
                {
                    _r.UnknownNames++;
                    return false;
                }

                if (!assets.TryFind(name, classId, out e))
                {
                    missing.Add(name);
                    return false;
                }

                kinds.Add(kind);
                indices.Add(index);
                entries.Add(e);
                return true;
            }

            private void Start(RrAssets assets, byte kind, int index, RrEntry e, List<string> prefabNames)
            {
                _inFlight++;
                switch (kind)
                {
                    case 0:
                        assets.Load<Mesh>(e, m =>
                        {
                            _inFlight--;
                            if (m != null)
                            {
                                _meshes[index] = m;
                                _r.MeshesLoaded++;
                            }
                            else
                            {
                                _r.MissingMeshes.Add(e.Name);
                            }
                        });
                        break;
                    case 1:
                        assets.Load<Material>(e, m =>
                        {
                            _inFlight--;
                            if (m != null)
                            {
                                _materials[index] = RrMaterials.Fix(m);
                                _r.MaterialsLoaded++;
                            }
                            else
                            {
                                _r.MissingMaterials.Add(e.Name);
                            }
                        });
                        break;
                    case 2:
                    {
                        string name = prefabNames[index];
                        assets.LoadPrefab(e, (go, data) =>
                        {
                            _inFlight--;
                            if (go != null)
                            {
                                _prefabs[name] = new PrefabAsset { Go = go, Data = data };
                            }
                            else
                            {
                                _r.MissingPrefabs.Add(name);
                            }
                        });
                        break;
                    }
                    default:
                        assets.Load<Material>(e, m =>
                        {
                            _inFlight--;
                            _skyMaterial = m != null ? RrMaterials.Fix(m) : null;
                        });
                        break;
                }
            }

            // 4. renderers
            private IEnumerator RendererPhase()
            {
                var batches = new Dictionary<long, Batch>();
                var order = new List<Batch>();
                Matrix4x4 rootMatrix = _root.transform.localToWorldMatrix;
                for (int n = 0; n < _rend.Count; n++)
                {
                    if (Over)
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }

                        rootMatrix = _root.transform.localToWorldMatrix;
                    }

                    var r = _d.Renderers[_rend[n]];
                    var mesh = _meshes[r.Mesh];
                    if (mesh == null)
                    {
                        _r.SkippedRenderers++;
                        continue;
                    }

                    // Install art's world box, for the stand-in / dress overlap test.
                    if (_o.FixOverlaps)
                    {
                        AddArt(WorldBox(rootMatrix * Matrix4x4.TRS(r.Position, r.Rotation, r.Scale), mesh.bounds));
                    }

                    int subs = mesh.subMeshCount;
                    int count = r.MaterialCount == 0 ? subs : Math.Min(subs, r.MaterialCount);
                    if (count <= 0)
                    {
                        _r.SkippedRenderers++;
                        continue;
                    }

                    bool mirrored = r.Scale.x * r.Scale.y * r.Scale.z < 0f;
                    bool mergeable = _o.Combine && !_separateAll && !mirrored && mesh.isReadable && mesh.vertexCount <= _o.MaxBatchVertices;
                    for (int s = 0; s < count && mergeable; s++)
                    {
                        if (mesh.GetTopology(s) != MeshTopology.Triangles)
                        {
                            mergeable = false;
                        }
                    }

                    _r.Renderers++;
                    if (!mergeable)
                    {
                        Separate(r, mesh, count);
                        _r.Separate++;
                        if (mirrored)
                        {
                            _r.Mirrored++;
                        }
                        else if (_o.Combine && !mesh.isReadable)
                        {
                            _r.Unreadable++;
                        }

                        continue;
                    }

                    var matrix = Matrix4x4.TRS(r.Position, r.Rotation, r.Scale);
                    int mask = Mask(r.Mesh, mesh);
                    long cell = Cell(r.Position);
                    for (int s = 0; s < count; s++)
                    {
                        var mat = MaterialOf(r, s);
                        long key = ((long)(uint)mat.GetInstanceID() << 32) | ((long)(mask & 0xFF) << 24) | cell;
                        Batch b;
                        if (!batches.TryGetValue(key, out b))
                        {
                            b = new Batch { Material = mat };
                            batches[key] = b;
                            order.Add(b);
                        }

                        int sv = mesh.GetSubMesh(s).vertexCount;
                        if (b.Items.Count > 0 && b.Vertices + sv > _o.MaxBatchVertices)
                        {
                            Flush(b);
                        }

                        b.Items.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = matrix });
                        b.Vertices += sv;
                        b.FullVertices += mesh.vertexCount;
                    }
                }

                for (int i = 0; i < order.Count; i++)
                {
                    if (Over)
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }
                    }

                    Flush(order[i]);
                }
            }

            private Material MaterialOf(RrSceneData.RendererRecord r, int sub)
            {
                if (sub < r.MaterialCount)
                {
                    int m = _d.MaterialIndices[r.MaterialStart + sub];
                    if (m >= 0 && m < _materials.Length && _materials[m] != null)
                    {
                        return _materials[m];
                    }
                }

                if (_fallback == null)
                {
                    _fallback = RrLook.Solid(new Color(0.62f, 0.62f, 0.64f));
                }

                return _fallback;
            }

            private void Separate(RrSceneData.RendererRecord r, Mesh mesh, int count)
            {
                var go = new GameObject(mesh.name);
                go.layer = RrLayers.Environment;
                var t = go.transform;
                t.SetParent(_renderersT, false);
                t.localPosition = r.Position;
                t.localRotation = r.Rotation;
                t.localScale = r.Scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                var mats = new Material[count];
                for (int i = 0; i < count; i++)
                {
                    mats[i] = MaterialOf(r, i);
                }

                mr.sharedMaterials = mats;
                mr.shadowCastingMode = _o.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }

            private void Flush(Batch b)
            {
                if (b.Items.Count == 0)
                {
                    return;
                }

                var mesh = new Mesh { name = "Rr Scene Batch " + b.Material.name + " " + b.Made };
                if (b.FullVertices > 65000 || b.Vertices > 65000)
                {
                    mesh.indexFormat = IndexFormat.UInt32;
                }

                try
                {
                    mesh.CombineMeshes(b.Items.ToArray(), true, true, false);
                }
                catch (Exception e)
                {
                    // One bad source mesh must not stop the room: this batch is dropped and counted.
                    RrLog.WarnOnce(Area, "combine:" + b.Material.name, _r.Scene + ": merging " + b.Items.Count + " renderers of " + b.Material.name + " failed (" + e.GetType().Name + ": " + e.Message + "); they are left out");
                    Object.Destroy(mesh);
                    _r.BatchesFailed++;
                    b.Items.Clear();
                    b.Vertices = 0;
                    b.FullVertices = 0;
                    return;
                }

                int verts = mesh.vertexCount;
                mesh.UploadMeshData(true);
                _host.Own(mesh);

                var go = new GameObject("Batch " + b.Material.name + " " + b.Made);
                go.layer = RrLayers.Environment;
                go.transform.SetParent(_renderersT, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = b.Material;
                mr.shadowCastingMode = _o.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                // One probe sample for a mesh spread over a room would light all of it like its centre.
                mr.lightProbeUsage = LightProbeUsage.Off;
                _r.Batches++;
                _r.Vertices += verts;
                b.Made++;
                b.Items.Clear();
                b.Vertices = 0;
                b.FullVertices = 0;
            }

            // The vertex attributes a mesh has (merge only meshes with the same set).
            private int Mask(int index, Mesh mesh)
            {
                if (_masks[index] >= 0)
                {
                    return _masks[index];
                }

                int m = 0;
                if (mesh.HasVertexAttribute(VertexAttribute.Normal)) m |= 1;
                if (mesh.HasVertexAttribute(VertexAttribute.Tangent)) m |= 2;
                if (mesh.HasVertexAttribute(VertexAttribute.Color)) m |= 4;
                if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) m |= 8;
                if (mesh.HasVertexAttribute(VertexAttribute.TexCoord1)) m |= 16;
                if (mesh.HasVertexAttribute(VertexAttribute.TexCoord2)) m |= 32;
                if (mesh.HasVertexAttribute(VertexAttribute.TexCoord3)) m |= 64;
                _masks[index] = m;
                return m;
            }

            private long Cell(Vector3 p)
            {
                if (_cellSize <= 0f)
                {
                    return 0;
                }

                // Cells centred on the scene origin (cell 0 spans -16..16 m at the default 32 m), so a room built around
                // its origin is not cut in two there.
                int x = Mathf.RoundToInt(p.x / _cellSize) & 0xFF;
                int y = Mathf.RoundToInt(p.y / _cellSize) & 0xFF;
                int z = Mathf.RoundToInt(p.z / _cellSize) & 0xFF;
                return ((long)x << 16) | ((long)y << 8) | (long)z;
            }

            // 5. stand-ins
            private IEnumerator StandInPhase()
            {
                if (_stand.Count == 0)
                {
                    yield break;
                }

                var batches = new Dictionary<long, ShapeBatch>();
                var order = new List<ShapeBatch>();
                int steps = Mathf.Clamp(_o.ColourSteps, 2, 256) - 1;
                for (int n = 0; n < _stand.Count; n++)
                {
                    if (Over)
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }
                    }

                    var x = _d.StandIns[_stand[n]];
                    var extents = x.Extents;
                    if (_o.FixOverlaps)
                    {
                        // A stand-in that is the same object as install art already built (the level's unmatched LOD or
                        // copy of it) is left out.
                        if (_art.Count > 0 && OverArt(WorldBox(_root.transform.localToWorldMatrix * Matrix4x4.TRS(x.Center, x.Rotation, Vector3.one), new Bounds(Vector3.zero, extents * 2f))))
                        {
                            _r.StandInsOverArt++;
                            continue;
                        }

                        // Faces 3-6 mm in (varying per box): a face lying on install art or on a neighbouring stand-in
                        // no longer shares its plane, so the two cannot z-fight; boxes under 2 cm keep their size.
                        float inset = 0.003f + 0.0005f * (n % 7);
                        var shrunk = new Vector3(extents.x > 0.01f ? extents.x - inset : extents.x, extents.y > 0.01f ? extents.y - inset : extents.y, extents.z > 0.01f ? extents.z - inset : extents.z);
                        if (shrunk != extents)
                        {
                            extents = shrunk;
                            _r.StandInsInset++;
                        }
                    }

                    var c = new Color(Q(x.Color.r, steps), Q(x.Color.g, steps), Q(x.Color.b, steps), Q(x.Color.a, steps));
                    var mat = c.a < 0.99f ? RrLook.Glass(c) : RrLook.Solid(new Color(c.r, c.g, c.b, 1f));
                    long key = ((long)(uint)mat.GetInstanceID() << 32) | Cell(x.Center);
                    ShapeBatch b;
                    if (!batches.TryGetValue(key, out b))
                    {
                        b = new ShapeBatch { Material = mat };
                        batches[key] = b;
                        order.Add(b);
                    }

                    int add = x.Cylinder ? CylinderVertices : 24;
                    if (b.V.Count > 0 && b.V.Count + add > _o.MaxBatchVertices)
                    {
                        FlushShapes(b);
                    }

                    if (x.Cylinder)
                    {
                        AppendCylinder(b, x.Center, x.Rotation, extents);
                    }
                    else
                    {
                        AppendBox(b, x.Center, x.Rotation, extents);
                    }

                    if (_o.StandInColliders)
                    {
                        AddBox(x.Center, x.Rotation, x.Extents * 2f);
                    }

                    _r.StandIns++;
                }

                for (int i = 0; i < order.Count; i++)
                {
                    if (Over)
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }
                    }

                    FlushShapes(order[i]);
                }
            }

            private static float Q(float v, int steps)
            {
                return Mathf.Round(Mathf.Clamp01(v) * steps) / steps;
            }

            private void FlushShapes(ShapeBatch b)
            {
                if (b.V.Count == 0)
                {
                    return;
                }

                var mesh = new Mesh { name = "Rr Scene Stand-ins " + b.Material.name + " " + b.Made };
                if (b.V.Count > 65000)
                {
                    mesh.indexFormat = IndexFormat.UInt32;
                }

                mesh.SetVertices(b.V);
                mesh.SetNormals(b.N);
                mesh.SetUVs(0, b.Uv);
                mesh.SetTriangles(b.T, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                _host.Own(mesh);
                var go = new GameObject("Stand-ins " + b.Material.name + " " + b.Made);
                go.layer = RrLayers.Environment;
                go.transform.SetParent(_renderersT, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = b.Material;
                mr.shadowCastingMode = _o.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.lightProbeUsage = LightProbeUsage.Off;
                _r.StandInBatches++;
                b.Made++;
                b.V.Clear();
                b.N.Clear();
                b.Uv.Clear();
                b.T.Clear();
            }

            private static readonly Vector3[] FaceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            private static readonly Vector3[] FaceU = { Vector3.forward, Vector3.back, Vector3.right, Vector3.right, Vector3.left, Vector3.right };

            // A box of half extents e: 6 faces, 4 vertices each, UVs in metres on the face.
            private static void AppendBox(ShapeBatch b, Vector3 c, Quaternion q, Vector3 e)
            {
                for (int f = 0; f < 6; f++)
                {
                    var n = FaceNormals[f];
                    var u = FaceU[f];
                    var v = Vector3.Cross(n, u);
                    float hn = Mathf.Abs(Vector3.Dot(e, Abs(n)));
                    float hu = Mathf.Abs(Vector3.Dot(e, Abs(u)));
                    float hv = Mathf.Abs(Vector3.Dot(e, Abs(v)));
                    int start = b.V.Count;
                    var wn = q * n;
                    for (int k = 0; k < 4; k++)
                    {
                        float su = k == 0 || k == 3 ? -1f : 1f;
                        float sv = k < 2 ? -1f : 1f;
                        var local = n * hn + u * (su * hu) + v * (sv * hv);
                        b.V.Add(c + q * local);
                        b.N.Add(wn);
                        b.Uv.Add(new Vector2(su * hu, sv * hv));
                    }

                    // Front face outwards: with v = n x u, u x v = n, so 0-1-2 and 0-2-3 give Cross(b - a, c - a) along n
                    // (Unity draws the side that cross product points to; RrRcGeo.Tri uses the same test).
                    b.T.Add(start);
                    b.T.Add(start + 1);
                    b.T.Add(start + 2);
                    b.T.Add(start);
                    b.T.Add(start + 2);
                    b.T.Add(start + 3);
                }
            }

            private const int Segments = 16;
            private const int CylinderVertices = (Segments + 1) * 2 + (Segments + 1) * 2;

            // A cylinder along local Y: radii e.x / e.z, half height e.y.
            private static void AppendCylinder(ShapeBatch b, Vector3 c, Quaternion q, Vector3 e)
            {
                int side = b.V.Count;
                for (int i = 0; i <= Segments; i++)
                {
                    float a = i * Mathf.PI * 2f / Segments;
                    float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                    var n = q * new Vector3(cx / Mathf.Max(e.x, 1e-4f), 0f, cz / Mathf.Max(e.z, 1e-4f)).normalized;
                    for (int k = 0; k < 2; k++)
                    {
                        b.V.Add(c + q * new Vector3(cx * e.x, k == 0 ? -e.y : e.y, cz * e.z));
                        b.N.Add(n);
                        b.Uv.Add(new Vector2(i / (float)Segments * 2f * Mathf.PI * Mathf.Max(e.x, e.z), k == 0 ? -e.y : e.y));
                    }
                }

                for (int i = 0; i < Segments; i++)
                {
                    int a0 = side + i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                    b.T.Add(a0);
                    b.T.Add(a1);
                    b.T.Add(b0);
                    b.T.Add(b0);
                    b.T.Add(a1);
                    b.T.Add(b1);
                }

                for (int k = 0; k < 2; k++)
                {
                    float y = k == 0 ? -e.y : e.y;
                    var n = q * (k == 0 ? Vector3.down : Vector3.up);
                    int centre = b.V.Count;
                    b.V.Add(c + q * new Vector3(0f, y, 0f));
                    b.N.Add(n);
                    b.Uv.Add(Vector2.zero);
                    for (int i = 0; i < Segments; i++)
                    {
                        float a = i * Mathf.PI * 2f / Segments;
                        var p = new Vector3(Mathf.Cos(a) * e.x, y, Mathf.Sin(a) * e.z);
                        b.V.Add(c + q * p);
                        b.N.Add(n);
                        b.Uv.Add(new Vector2(p.x, p.z));
                    }

                    for (int i = 0; i < Segments; i++)
                    {
                        int p0 = centre + 1 + i, p1 = centre + 1 + (i + 1) % Segments;
                        if (k == 1)
                        {
                            b.T.Add(centre);
                            b.T.Add(p1);
                            b.T.Add(p0);
                        }
                        else
                        {
                            b.T.Add(centre);
                            b.T.Add(p0);
                            b.T.Add(p1);
                        }
                    }
                }
            }

            private static Vector3 Abs(Vector3 v)
            {
                return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            }

            // 6. prefabs
            private IEnumerator PrefabPhase()
            {
                for (int n = 0; n < _pref.Count; n++)
                {
                    if (Over)
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }
                    }

                    var p = _d.Prefabs[_pref[n]];
                    PrefabAsset asset;
                    if (!_prefabs.TryGetValue(p.Name, out asset) || asset == null || asset.Go == null)
                    {
                        continue;
                    }

                    var options = RrSpawnOptions.Scenery();
                    options.StripLights = !_o.PrefabLights;
                    RrInstance inst = null;
                    try
                    {
                        inst = RrSpawn.SpawnLoaded(asset.Go, asset.Data, _prefabsT, p.Position, p.Rotation, options);
                    }
                    catch (Exception e)
                    {
                        RrLog.Error(Area, "spawning " + p.Name + " failed", e);
                    }

                    if (inst != null)
                    {
                        inst.transform.localScale = p.Scale;
                        _r.Prefabs++;
                        if (_o.FixOverlaps)
                        {
                            var box = inst.WorldBounds();
                            if (box.size.sqrMagnitude > 1e-6f)
                            {
                                AddArt(box);
                            }
                        }
                    }
                }
            }

            // 7. colliders
            private readonly Dictionary<long, ColliderHost> _hosts = new Dictionary<long, ColliderHost>();

            private IEnumerator ColliderPhase()
            {
                if (_col.Count == 0)
                {
                    yield break;
                }

                // Pre-cook the triangle meshes on the worker: assigning a MeshCollider then reuses the cooked data.
                var worker = RrMain.Instance != null ? RrMain.Instance.Worker : null;
                if (_o.BakeMeshCollidersOnWorker && worker != null && worker.Threaded)
                {
                    var ids = new List<int>();
                    var seen = new HashSet<int>();
                    foreach (int i in _col)
                    {
                        var c = _d.Colliders[i];
                        if (c.Shape != RrSceneData.ColliderShape.Mesh)
                        {
                            continue;
                        }

                        var mesh = _meshes[c.Mesh];
                        if (mesh != null && mesh.isReadable && seen.Add(c.Mesh))
                        {
                            ids.Add(mesh.GetInstanceID());
                        }
                    }

                    int pending = ids.Count;
                    int baked = 0;
                    string failure = null;
                    foreach (int id in ids)
                    {
                        int meshId = id;
                        worker.Post(() =>
                        {
                            // The int overload: obsolete in 6000.3 in favour of EntityId, still present in the 6000.3.21 client.
#pragma warning disable 618
                            Physics.BakeMesh(meshId, false);
#pragma warning restore 618
                            return null;
                        }, (result, e) =>
                        {
                            pending--;
                            if (e == null)
                            {
                                baked++;
                            }
                            else if (failure == null)
                            {
                                failure = e.GetType().Name + ": " + e.Message;
                            }
                        });
                    }

                    float until = Time.realtimeSinceStartup + 30f;
                    while (pending > 0 && Time.realtimeSinceStartup < until)
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }
                    }

                    _r.MeshesBaked = baked;
                    if (failure != null)
                    {
                        RrLog.WarnOnce(Area, "bake", "pre-cooking mesh colliders on the worker failed (" + failure + "); they cook on assignment");
                    }
                }

                for (int n = 0; n < _col.Count; n++)
                {
                    if (Over)
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }
                    }

                    var c = _d.Colliders[_col[n]];
                    switch (c.Shape)
                    {
                        case RrSceneData.ColliderShape.Box:
                            AddBox(c.Center, c.Rotation, c.Size);
                            break;
                        case RrSceneData.ColliderShape.Sphere:
                        {
                            var h = Host(Quaternion.identity);
                            var s = h.Go.AddComponent<SphereCollider>();
                            s.center = c.Center;
                            s.radius = Mathf.Max(0.001f, c.Radius);
                            _r.OtherColliders++;
                            break;
                        }
                        case RrSceneData.ColliderShape.Capsule:
                        {
                            var h = Host(c.Rotation);
                            var s = h.Go.AddComponent<CapsuleCollider>();
                            s.center = h.Inverse * c.Center;
                            s.radius = Mathf.Max(0.001f, c.Radius);
                            s.height = Mathf.Max(s.radius * 2f, c.Height);
                            s.direction = 1;
                            _r.OtherColliders++;
                            break;
                        }
                        default:
                        {
                            var mesh = _meshes[c.Mesh];
                            if (mesh == null)
                            {
                                continue;
                            }

                            if (!mesh.isReadable)
                            {
                                // A non-readable install mesh cannot be cooked at run time (Unity logs an error and the
                                // collider stays empty; Crimson's Quest_Rock01, iteration 13 tour): its bounds as a box instead.
                                var bounds = mesh.bounds;
                                AddBox(c.Center + c.Rotation * Vector3.Scale(bounds.center, c.Size), c.Rotation, Vector3.Scale(bounds.size, c.Size));
                                RrLog.WarnOnce("SceneKit", "unreadable collider:" + mesh.name, "mesh collider " + mesh.name + " is not readable: its bounds are used as a box");
                                break;
                            }

                            var go = new GameObject("Mesh Collider " + mesh.name);
                            go.layer = RrLayers.Environment;
                            var t = go.transform;
                            t.SetParent(_collidersT, false);
                            t.localPosition = c.Center;
                            t.localRotation = c.Rotation;
                            t.localScale = c.Size;
                            go.AddComponent<MeshCollider>().sharedMesh = mesh;
                            _r.MeshColliders++;
                            break;
                        }
                    }
                }
            }

            private void AddBox(Vector3 centre, Quaternion rotation, Vector3 size)
            {
                var h = Host(rotation);
                var b = h.Go.AddComponent<BoxCollider>();
                b.center = h.Inverse * centre;
                b.size = new Vector3(Mathf.Max(0.001f, Mathf.Abs(size.x)), Mathf.Max(0.001f, Mathf.Abs(size.y)), Mathf.Max(0.001f, Mathf.Abs(size.z)));
                _r.BoxColliders++;
            }

            // The object carrying colliders of (about) this rotation: components rounded to 1/1000 (at 10 m from the
            // object's origin that is under 1 cm), at most MaxCollidersPerObject each.
            private ColliderHost Host(Quaternion q)
            {
                if (q.w < 0f)
                {
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                }

                long key = (Round(q.x) << 48) | (Round(q.y) << 32) | (Round(q.z) << 16) | Round(q.w);
                ColliderHost h;
                if (_hosts.TryGetValue(key, out h) && h.Go != null && h.Count < Mathf.Max(1, _o.MaxCollidersPerObject))
                {
                    h.Count++;
                    return h;
                }

                var go = new GameObject("Colliders " + _r.ColliderObjects);
                go.layer = RrLayers.Environment;
                go.transform.SetParent(_collidersT, false);
                go.transform.localRotation = q;
                h = new ColliderHost { Go = go, Inverse = Quaternion.Inverse(q), Count = 1 };
                _hosts[key] = h;
                _r.ColliderObjects++;
                return h;
            }

            private static long Round(float v)
            {
                return (long)(Mathf.RoundToInt(v * 1000f) + 32768) & 0xFFFF;
            }

            // 8. lights, audio, environment
            private IEnumerator LightAudioEnvPhase()
            {
                if (!_o.SkipLights && _d.Lights.Count > 0)
                {
                    var keep = new List<int>();
                    for (int i = 0; i < _d.Lights.Count; i++)
                    {
                        var l = _d.Lights[i];
                        if (l.Type == LightType.Directional ? _o.DirectionalLights : Inside(l.Position))
                        {
                            keep.Add(i);
                        }
                    }

                    // Rec Room's shadowed lights (the table lists their positions; a scene without the list uses Rec Room's
                    // pattern: the big spots, intensity 2+ and range 15 m+, are the shadow casters of every quest area).
                    var shadowed = new HashSet<int>();
                    if (_env != null)
                    {
                        foreach (int i in keep)
                        {
                            var l = _d.Lights[i];
                            if (l.Shadows)
                            {
                                shadowed.Add(i);
                                continue;
                            }

                            foreach (var at in _env.ShadowLights)
                            {
                                if ((at - l.Position).sqrMagnitude < 0.09f)
                                {
                                    shadowed.Add(i);
                                    break;
                                }
                            }
                        }

                        if (shadowed.Count == 0)
                        {
                            foreach (int i in keep)
                            {
                                var l = _d.Lights[i];
                                if (l.Type == LightType.Spot && l.Intensity >= 2f && l.Range >= 15f)
                                {
                                    shadowed.Add(i);
                                }
                            }
                        }
                    }

                    keep.Sort((a, b) => (Score(_d.Lights[b]) + (shadowed.Contains(b) ? 1e6f : 0f)).CompareTo(Score(_d.Lights[a]) + (shadowed.Contains(a) ? 1e6f : 0f)));
                    int max = LightBudget(keep.Count);
                    int spots = 0, points = 0;
                    // Iteration 9: every point/spot light of the scene (up to Rec Room's 160) goes into Rec Room's own light
                    // grid for its shaders (RrLightGrid: the client's URP never fed them); the Unity lights below stay for
                    // URP-shaded objects and avatars, set up as the same Rec Room light (hemisphere, falloff factor).
                    var sources = _o.LightGrid ? new List<RrLightGrid.Source>() : null;
                    if (sources != null)
                    {
                        for (int n = max; n < keep.Count; n++)
                        {
                            var l = _d.Lights[keep[n]];
                            if (l.Type != LightType.Directional)
                            {
                                sources.Add(SourceOf(l, null));
                            }
                        }
                    }

                    for (int n = 0; n < keep.Count && n < max; n++)
                    {
                        var l = _d.Lights[keep[n]];
                        var go = new GameObject("Light " + l.Type + " " + n);
                        go.transform.SetParent(_lightsT, false);
                        go.transform.localPosition = l.Position;
                        go.transform.localRotation = l.Rotation;
                        var light = go.AddComponent<Light>();
                        light.type = l.Type;
                        light.color = l.Color;
                        light.intensity = l.Intensity * _o.LightIntensityScale;
                        light.range = l.Range > 0f ? l.Range : 10f;
                        if (l.Type == LightType.Spot)
                        {
                            light.spotAngle = Mathf.Clamp(l.SpotAngle, 1f, 179f);
                        }

                        light.shadows = LightShadows.None;
                        // Iteration 9: with the grid, Rec Room's surfaces cannot sample URP's additional-light shadow slices
                        // (indexed by URP's own light order), so rendering them would cost shadow maps (six faces for a point)
                        // for the few URP-shaded objects only: the Unity twins cast none; the sun keeps its shadows.
                        if (sources == null && shadowed.Contains(keep[n]))
                        {
                            bool give = l.Type == LightType.Spot ? spots < _o.MaxShadowSpots : points < _o.MaxShadowPoints;
                            if (give)
                            {
                                if (l.Type == LightType.Spot)
                                {
                                    spots++;
                                }
                                else
                                {
                                    points++;
                                }

                                light.shadows = LightShadows.Soft;
                                light.shadowStrength = 1f;
                                light.shadowNearPlane = 0.1f;
                                _r.ShadowLights++;
                            }
                        }

                        if (sources != null && l.Type != LightType.Directional)
                        {
                            var src = SourceOf(l, light);
                            RrLightGrid.ConfigureUnity(light, src);
                            light.intensity *= _o.LightIntensityScale;
                            sources.Add(src);
                        }

                        _r.Lights++;
                    }

                    _r.LightsDropped = _d.Lights.Count - _r.Lights;
                    if (sources != null && sources.Count > 0)
                    {
                        // The grid covers the level (B record, else every record), cut to the filter when one is set; its
                        // scope is the kit root's parent, so the room's own walls and dressing built there are lit too.
                        var vol = _d.HasBounds ? _d.Bounds : _core;
                        if (_o.Filter.HasValue)
                        {
                            var f = _o.Filter.Value;
                            var lo = Vector3.Max(vol.min, f.min);
                            var hi = Vector3.Min(vol.max, f.max);
                            if (hi.x > lo.x && hi.y > lo.y && hi.z > lo.z)
                            {
                                vol.SetMinMax(lo, hi);
                            }
                        }

                        vol.Expand(1f);
                        var parent = _root.transform.parent;
                        _r.Grid = RrLightGrid.Attach(_root, parent != null ? parent : _root.transform, _r.Scene, vol, sources);
                        _r.GridLights = sources.Count;
                    }
                }

                if (!_o.SkipAudio && _d.Audio.Count > 0)
                {
                    var assets = RrAssets.Instance;
                    var loops = new List<int>();
                    for (int i = 0; i < _d.Audio.Count; i++)
                    {
                        var a = _d.Audio[i];
                        if (!Inside(a.Position))
                        {
                            continue;
                        }

                        if (a.Loop)
                        {
                            loops.Add(i);
                        }
                        else if (!_o.SkipMarkers)
                        {
                            var m = NewMarker("audio", a.Clip, a.Position, Quaternion.identity);
                            m.Values["clip"] = a.Clip;
                            m.Values["volume"] = a.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            m.Values["minDist"] = a.MinDistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            m.Values["maxDist"] = a.MaxDistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            _r.Markers.Add(m);
                        }
                    }

                    loops.Sort((a, b) => (_d.Audio[b].Volume * _d.Audio[b].MaxDistance).CompareTo(_d.Audio[a].Volume * _d.Audio[a].MaxDistance));
                    for (int n = 0; n < loops.Count; n++)
                    {
                        var a = _d.Audio[loops[n]];
                        RrEntry e;
                        if (IsUnknown(a.Clip))
                        {
                            _r.UnknownNames++;
                            continue;
                        }

                        if (assets == null || !assets.TryFind(a.Clip, 83, out e))
                        {
                            if (!_r.MissingClips.Contains(a.Clip))
                            {
                                _r.MissingClips.Add(a.Clip);
                            }

                            continue;
                        }

                        if (_r.AudioLoops >= _o.MaxAudio)
                        {
                            _r.AudioDropped++;
                            continue;
                        }

                        var anchor = new GameObject("Audio " + a.Clip);
                        anchor.transform.SetParent(_audioT, false);
                        anchor.transform.localPosition = a.Position;
                        var source = RrSound.Loop(a.Clip, anchor.transform, a.Volume * _o.AudioVolumeScale, a.MaxDistance > 0f ? a.MaxDistance : 30f);
                        if (source != null)
                        {
                            source.minDistance = Mathf.Max(0.1f, a.MinDistance > 0f ? a.MinDistance : 1.5f);
                            // Rec Room's own roll-off and 2D/3D blend when the extractor read them (roll=, pan=): a
                            // custom curve (Rec Room's cafe counter) is taken as linear, which also ends in silence.
                            if (a.Rolloff == 1 || a.Rolloff == 2)
                            {
                                source.rolloffMode = AudioRolloffMode.Linear;
                            }

                            if (a.Spatial >= 0f)
                            {
                                source.spatialBlend = Mathf.Clamp01(a.Spatial);
                            }

                            _r.AudioLoops++;
                            if (_r.AudioLoops <= 6)
                            {
                                _loopsText.Append(_r.AudioLoops > 1 ? ", " : "").Append(a.Clip).Append(a.Was != null ? " (for " + a.Was + ")" : "").Append(" at ").Append(Short(anchor.transform.position))
                                          .Append(' ').Append(a.Volume.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append(' ')
                                          .Append(a.MinDistance.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append('-').Append(a.MaxDistance.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(" m");
                            }
                        }
                    }

                    if (_r.AudioLoops > 0)
                    {
                        RrLog.Info(Area, _r.Scene + ": ambient loops started " + _r.AudioLoops + ": " + _loopsText + (_r.AudioLoops > 6 ? ", ..." : ""));
                    }
                }

                if (_d.HasFog)
                {
                    _r.Fog = _d.FogMode + " #" + ColorUtility.ToHtmlStringRGB(_d.FogColor) + " density " + _d.FogDensity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + " start " + _d.FogStart.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) +
                             " end " + _d.FogEnd.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                }

                if (_d.HasAmbient)
                {
                    _r.HasAmbient = true;
                    _r.Ambient = _d.Ambient;
                    if (_o.ApplyAmbient)
                    {
                        bool trilight = string.Equals(_d.AmbientMode, "trilight", StringComparison.OrdinalIgnoreCase);
                        _host.ApplyAmbient(_d.Ambient, trilight ? _d.AmbientEquator : _d.Ambient, trilight ? _d.AmbientGround : _d.Ambient, _d.AmbientIntensity, trilight);
                        _r.AmbientApplied = true;
                    }
                }

                if (!string.IsNullOrEmpty(_d.Sky))
                {
                    _r.Sky = _d.Sky;
                    if (_o.ApplySky && _skyMaterial != null)
                    {
                        _host.ApplySky(_skyMaterial);
                        _r.SkyApplied = true;
                    }
                }

                yield break;
            }

            // 9. effects: one clean template per install effect (prefab root or child), then RrSceneKitFx places the
            // records once the room is built (wall-mounted flames need the room's walls) and keeps the nearest running.
            private IEnumerator EffectPhase()
            {
                if (_fx.Count == 0)
                {
                    yield break;
                }

                var fxT = Group("Effects");
                var holder = new GameObject("Effect templates");
                holder.SetActive(false);
                holder.transform.SetParent(_root.transform, false);
                var fx = fxT.gameObject.AddComponent<RrSceneKitFx>();
                fx.Result = _r;
                fx.Data = _d;
                fx.Options = _o;
                var options = new RrSpawnOptions { StripColliders = true, StripRigidbodies = true, StripLights = true, StripAudioSources = true, Layer = RrLayers.Default };
                foreach (int i in _fx)
                {
                    if (Over)
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }
                    }

                    var e = _d.Effects[i];
                    string part = e.Get("part");
                    string key = RrSceneKitFx.Key(e.Prefab, part);
                    if (!fx.Templates.ContainsKey(key))
                    {
                        Quaternion rot;
                        fx.Templates[key] = Template(e.Prefab, part, holder.transform, options, out rot);
                        fx.TemplateRotations[key] = rot;
                    }

                    if (fx.Templates[key] != null)
                    {
                        fx.Pending.Add(i);
                    }
                    else if (!_r.MissingEffects.Contains(key))
                    {
                        _r.MissingEffects.Add(key);
                    }
                }

                _r.Effects = fx.Pending.Count;
                fx.BuiltAt = Time.realtimeSinceStartup;
            }

            // The install effect as a clean copy (RrSpawn), cut down to part (a child path) when one is named; null if
            // the prefab did not load or has no such child.
            private GameObject Template(string prefab, string part, Transform holder, RrSpawnOptions options, out Quaternion rotation)
            {
                rotation = Quaternion.identity;
                PrefabAsset asset;
                if (!_prefabs.TryGetValue(prefab, out asset) || asset == null || asset.Go == null)
                {
                    return null;
                }

                RrInstance inst = null;
                try
                {
                    inst = RrSpawn.SpawnLoaded(asset.Go, asset.Data, holder, Vector3.zero, Quaternion.identity, options);
                }
                catch (Exception ex)
                {
                    RrLog.Error(Area, "effect template " + prefab + " failed", ex);
                }

                if (inst == null)
                {
                    return null;
                }

                Transform t = string.IsNullOrEmpty(part) ? inst.transform : inst.transform.Find(part);
                if (t == null)
                {
                    Object.Destroy(inst.gameObject);
                    return null;
                }

                if (t != inst.transform)
                {
                    rotation = Quaternion.Inverse(inst.transform.rotation) * t.rotation;
                    t.SetParent(holder, false);
                    Object.Destroy(inst.gameObject);
                }
                else
                {
                    Object.Destroy(inst);
                }

                var src = string.IsNullOrEmpty(part) ? asset.Go.transform : asset.Go.transform.Find(part);
                if (src != null)
                {
                    Additive(t, src);
                }

                // An effect whose card shader the client cannot run came back as URP Lit (RrMaterials): an opaque plane
                // where a flame should be. Such a template is dropped (listed as a missing effect).
                foreach (var mr in t.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mm = mr.sharedMaterial;
                    if (mm != null && mm.name.EndsWith("(rr lit)", StringComparison.Ordinal))
                    {
                        RrLog.WarnOnce(Area, "fxlit:" + prefab, "effect template " + prefab + (string.IsNullOrEmpty(part) ? "" : "/" + part) + " left out: its shader is not supported here (" + mm.name + ")");
                        Object.Destroy(t.gameObject);
                        return null;
                    }
                }

                t.gameObject.SetActive(false);
                _r.EffectTemplates++;
                return t.gameObject;
            }

            // Rec Room's fire and glow particles use Legacy Shaders/Particles/Additive (Soft) (the campfire flame's
            // VFX_SquareParticle_mat, a 64 px DXT1 "Square" with no alpha). RrMaterials gives legacy particle shaders
            // URP Particles/Unlit, additive only when the old material says so through _DstBlend, which the legacy
            // additive shaders do not have (their blend is fixed), so the flames came out alpha-blended. The clean copy
            // keeps the prefab's child order, so the source renderer is found by walking both trees together.
            private static void Additive(Transform copy, Transform source)
            {
                var pr = copy.GetComponent<ParticleSystemRenderer>();
                var sr = source.GetComponent<ParticleSystemRenderer>();
                if (pr != null && sr != null && sr.sharedMaterial != null && sr.sharedMaterial.shader != null && pr.sharedMaterial != null)
                {
                    string shader = sr.sharedMaterial.shader.name;
                    var m = pr.sharedMaterial;
                    if (shader.StartsWith("Legacy Shaders/Particles/Additive", StringComparison.Ordinal) && m.HasProperty("_Blend") && Mathf.RoundToInt(m.GetFloat("_DstBlend")) != (int)BlendMode.One)
                    {
                        m.SetFloat("_Blend", 2f);
                        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        m.SetFloat("_DstBlend", (float)BlendMode.One);
                        if (sr.sharedMaterial.HasProperty("_TintColor"))
                        {
                            // Legacy additive draws 2 x tint x texture x vertex colour.
                            var tint = sr.sharedMaterial.GetColor("_TintColor");
                            m.SetColor("_BaseColor", new Color(tint.r * 2f, tint.g * 2f, tint.b * 2f, Mathf.Clamp01(tint.a * 2f)));
                        }
                    }
                }

                int n = Mathf.Min(copy.childCount, source.childCount);
                for (int i = 0; i < n; i++)
                {
                    Additive(copy.GetChild(i), source.GetChild(i));
                }
            }

            /// <summary>
            /// A scene light as Rec Room ran it (iteration 9): its URPLight values from the record (apkscene keys) or, for
            /// scenes converted before those keys existed, from RrEnvironmentTable's "urp=" list by position.
            /// </summary>
            private RrLightGrid.Source SourceOf(RrSceneData.LightRecord l, Light unity)
            {
                var s = new RrLightGrid.Source
                {
                    Name = l.Type + " " + l.Position.ToString("F1"),
                    Type = l.Type,
                    Position = l.Position,
                    Rotation = l.Rotation,
                    Color = l.Color,
                    Intensity = l.Intensity,
                    Range = l.Range > 0f ? l.Range : 10f,
                    SpotAngle = l.SpotAngle,
                    InnerSpotAngle = l.InnerSpot,
                    Specular = l.Specular,
                    Hemisphere = l.Hemisphere,
                    HemiInner = l.HemiInner,
                    HemiOuter = l.HemiOuter > 0f ? l.HemiOuter : 180f,
                    Unity = unity
                };

                RrEnvironmentSetting.UrpLight u;
                if (l.Specular < 0f && _env != null && _env.FindUrp(l.Position, out u))
                {
                    s.Specular = u.Specular;
                    s.Hemisphere = u.Hemisphere;
                    s.HemiInner = u.HemiInner;
                    s.HemiOuter = u.HemiOuter > 0f ? u.HemiOuter : 180f;
                    if (s.InnerSpotAngle < 0f && u.InnerSpot >= 0f)
                    {
                        s.InnerSpotAngle = u.InnerSpot;
                    }
                }

                if (s.Specular < 0f)
                {
                    // A light without Rec Room's URPLight: Rec Room's default contribution.
                    s.Specular = 1f;
                }

                return s;
            }

            private static float Score(RrSceneData.LightRecord l)
            {
                return l.Type == LightType.Directional ? float.MaxValue : l.Intensity * Mathf.Max(0.1f, l.Range);
            }

            // 9. markers
            private IEnumerator MarkerPhase()
            {
                if (_o.SkipMarkers)
                {
                    yield break;
                }

                for (int i = 0; i < _d.Markers.Count; i++)
                {
                    if (Over)
                    {
                        yield return Pause();
                        if (!Resume())
                        {
                            yield break;
                        }
                    }

                    var g = _d.Markers[i];
                    if (!Inside(g.Position) || (_o.SkipMarkerKinds != null && _o.SkipMarkerKinds.Contains(g.Kind)))
                    {
                        continue;
                    }

                    // The same "dress" marker twice (Spillway's data has 84 exact copies) is handed out once.
                    if (_o.FixOverlaps && g.Kind == "dress")
                    {
                        int hash = g.Name != null ? g.Name.GetHashCode() : 0;
                        if (g.Values != null)
                        {
                            for (int k = 0; k < g.Values.Length; k++)
                            {
                                hash = hash * 31 + (g.Values[k] != null ? g.Values[k].GetHashCode() : 0);
                            }
                        }

                        if (!_dressSeen.Add(PoseKey.Of(4, hash, g.Position, g.Rotation, Vector3.one)))
                        {
                            _r.DuplicateStandIns++;
                            continue;
                        }
                    }

                    // A "dress" marker (a stand-in the room dresses itself, e=half extents) that is the same object as
                    // install art the kit built is not handed out: it would be drawn a second time over it.
                    Vector3 half;
                    if (_o.FixOverlaps && _art.Count > 0 && g.Kind == "dress" && HalfExtents(g, out half)
                        && OverArt(WorldBox(_root.transform.localToWorldMatrix * Matrix4x4.TRS(g.Position, g.Rotation, Vector3.one), new Bounds(Vector3.zero, half * 2f))))
                    {
                        _r.DressOverArt++;
                        continue;
                    }

                    var m = NewMarker(g.Kind, g.Name, g.Position, g.Rotation);
                    if (g.Keys != null)
                    {
                        for (int k = 0; k < g.Keys.Length; k++)
                        {
                            m.Values[g.Keys[k]] = g.Values[k];
                        }
                    }

                    _r.Markers.Add(m);
                }
            }

            private RrSceneMarker NewMarker(string kind, string name, Vector3 p, Quaternion q)
            {
                return new RrSceneMarker
                {
                    Kind = kind,
                    Name = name,
                    ScenePosition = p,
                    SceneRotation = q,
                    Position = _o.Position + _o.Rotation * p,
                    Rotation = _o.Rotation * q
                };
            }

            // ---- lighting and overlap helpers (iteration 8) ----

            private struct NearItem
            {
                public int Number;
                public Vector3 P;
                public Quaternion Q;
                public Vector3 S;
            }

            private const float NearCell = 0.05f;

            /// <summary>
            /// A record that almost repeats one already kept (same mesh / shape, within 2 cm, 1.5 degrees and 1% of size):
            /// two copies that close draw on top of each other and z-fight. Registers the record when it is new.
            /// </summary>
            private static bool Near(Dictionary<long, List<NearItem>> grid, int number, Vector3 p, Quaternion q, Vector3 s)
            {
                int cx = Mathf.FloorToInt(p.x / NearCell), cy = Mathf.FloorToInt(p.y / NearCell), cz = Mathf.FloorToInt(p.z / NearCell);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            List<NearItem> list;
                            if (!grid.TryGetValue(ArtKey(cx + dx, cy + dy, cz + dz), out list))
                            {
                                continue;
                            }

                            foreach (var it in list)
                            {
                                if (it.Number == number && (it.P - p).sqrMagnitude < 0.0004f && Mathf.Abs(Quaternion.Dot(it.Q, q)) > 0.99991f
                                    && (it.S - s).sqrMagnitude <= 0.0001f * Mathf.Max(0.01f, s.sqrMagnitude))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                long key = ArtKey(cx, cy, cz);
                List<NearItem> own;
                if (!grid.TryGetValue(key, out own))
                {
                    own = new List<NearItem>();
                    grid[key] = own;
                }

                own.Add(new NearItem { Number = number, P = p, Q = q, S = s });
                return false;
            }

            /// <summary>Point and spot lights kept: MaxLights, or as many as Rec Room ran (capped) with EnvironmentLights.</summary>
            private int LightBudget(int available)
            {
                int max = Mathf.Max(0, _o.MaxLights);
                if (_env != null && _o.EnvironmentLights)
                {
                    int rr = _env.Lights > 0 ? _env.Lights : available;
                    max = Mathf.Max(max, Mathf.Min(_o.MaxEnvironmentLights, rr));
                }

                return max;
            }

            private void Core(Vector3 p)
            {
                if (_hasCore)
                {
                    _core.Encapsulate(p);
                }
                else
                {
                    _core = new Bounds(p, Vector3.zero);
                    _hasCore = true;
                }
            }

            private const float ArtCell = 4f;
            private const float ArtMaxSide = 80f;

            private static long ArtKey(int x, int y, int z)
            {
                return ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);
            }

            /// <summary>A world box of install art into the overlap grid (4 m cells; boxes over 80 m, terrain-sized, are not).</summary>
            private void AddArt(Bounds b)
            {
                var s = b.size;
                if (s.x > ArtMaxSide || s.y > ArtMaxSide || s.z > ArtMaxSide || s.x + s.y + s.z < 0.01f)
                {
                    return;
                }

                int index = _art.Count;
                _art.Add(b);
                Vector3 mn = b.min, mx = b.max;
                for (int x = Mathf.FloorToInt(mn.x / ArtCell); x <= Mathf.FloorToInt(mx.x / ArtCell); x++)
                {
                    for (int y = Mathf.FloorToInt(mn.y / ArtCell); y <= Mathf.FloorToInt(mx.y / ArtCell); y++)
                    {
                        for (int z = Mathf.FloorToInt(mn.z / ArtCell); z <= Mathf.FloorToInt(mx.z / ArtCell); z++)
                        {
                            long key = ArtKey(x, y, z);
                            List<int> list;
                            if (!_artGrid.TryGetValue(key, out list))
                            {
                                list = new List<int>();
                                _artGrid[key] = list;
                            }

                            list.Add(index);
                        }
                    }
                }
            }

            /// <summary>Whether a world box is the same object as some install art: IoU of the boxes at least OverlapIoU.</summary>
            private bool OverArt(Bounds b)
            {
                var s = b.size;
                if (s.x > ArtMaxSide || s.y > ArtMaxSide || s.z > ArtMaxSide)
                {
                    return false;
                }

                if (_artStamp.Length < _art.Count)
                {
                    _artStamp = new int[_art.Count + 256];
                }

                _stampNow++;
                Vector3 mn = b.min, mx = b.max;
                for (int x = Mathf.FloorToInt(mn.x / ArtCell); x <= Mathf.FloorToInt(mx.x / ArtCell); x++)
                {
                    for (int y = Mathf.FloorToInt(mn.y / ArtCell); y <= Mathf.FloorToInt(mx.y / ArtCell); y++)
                    {
                        for (int z = Mathf.FloorToInt(mn.z / ArtCell); z <= Mathf.FloorToInt(mx.z / ArtCell); z++)
                        {
                            List<int> list;
                            if (!_artGrid.TryGetValue(ArtKey(x, y, z), out list))
                            {
                                continue;
                            }

                            foreach (int k in list)
                            {
                                if (_artStamp[k] == _stampNow)
                                {
                                    continue;
                                }

                                _artStamp[k] = _stampNow;
                                if (IoU(b, _art[k]) >= _o.OverlapIoU)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            /// <summary>Intersection over union of two boxes, each at least 5 cm thick (flat art and flat stand-ins still compare).</summary>
            private static float IoU(Bounds a, Bounds b)
            {
                a.size = Vector3.Max(a.size, new Vector3(0.05f, 0.05f, 0.05f));
                b.size = Vector3.Max(b.size, new Vector3(0.05f, 0.05f, 0.05f));
                Vector3 mn = Vector3.Max(a.min, b.min), mx = Vector3.Min(a.max, b.max);
                if (mx.x <= mn.x || mx.y <= mn.y || mx.z <= mn.z)
                {
                    return 0f;
                }

                float inter = (mx.x - mn.x) * (mx.y - mn.y) * (mx.z - mn.z);
                float va = a.size.x * a.size.y * a.size.z, vb = b.size.x * b.size.y * b.size.z;
                return inter / Mathf.Max(1e-9f, va + vb - inter);
            }

            /// <summary>The world axis-aligned box of a local box under a matrix.</summary>
            private static Bounds WorldBox(Matrix4x4 m, Bounds local)
            {
                Vector3 c = m.MultiplyPoint3x4(local.center), e = local.extents;
                var ext = new Vector3(
                    Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                    Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                    Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);
                return new Bounds(c, ext * 2f);
            }

            /// <summary>A marker's "e=x,y,z" half extents.</summary>
            private static bool HalfExtents(RrSceneData.MarkerRecord g, out Vector3 half)
            {
                half = Vector3.zero;
                if (g.Keys == null)
                {
                    return false;
                }

                for (int k = 0; k < g.Keys.Length; k++)
                {
                    if (g.Keys[k] != "e" || g.Values[k] == null)
                    {
                        continue;
                    }

                    var p = g.Values[k].Split(',');
                    float x, y, z;
                    if (p.Length == 3 && float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x)
                        && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y)
                        && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                    {
                        half = new Vector3(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z));
                        return true;
                    }
                }

                return false;
            }

            // ---- helpers ----

            private Transform Group(string name)
            {
                var go = new GameObject(name);
                go.layer = RrLayers.Environment;
                go.transform.SetParent(_root.transform, false);
                return go.transform;
            }

            private bool Inside(Vector3 p)
            {
                return !_o.Filter.HasValue || _o.Filter.Value.Contains(p);
            }

            private bool Skipped(string name)
            {
                return _o.SkipNames != null && name != null && _o.SkipNames.Contains(name);
            }

            // The scene's bounds seen from the parent (axis-aligned around the rotated box).
            private Bounds Offset(Bounds b)
            {
                var result = new Bounds(_o.Position + _o.Rotation * b.center, Vector3.zero);
                var e = b.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    result.Encapsulate(_o.Position + _o.Rotation * corner);
                }

                return result;
            }

            private static string Short(Vector3 p)
            {
                return "(" + p.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ", " + p.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ", " +
                       p.z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ")";
            }

            // The extractor marks names it could not match to the install with a leading "?" (kept for reference).
            private static bool IsUnknown(string name)
            {
                return string.IsNullOrEmpty(name) || name[0] == '?';
            }

            // "Name_LOD2" (or "NameLOD2") whose "..._LOD0" is in the table.
            private static bool IsHigherLod(string name, HashSet<string> names)
            {
                if (name == null || name.Length < 5)
                {
                    return false;
                }

                int d = name.Length - 1;
                char c = name[d];
                if (c < '1' || c > '7' || string.CompareOrdinal(name, d - 3, "LOD", 0, 3) != 0)
                {
                    return false;
                }

                return names.Contains(name.Substring(0, d) + "0");
            }
        }
    }
}
