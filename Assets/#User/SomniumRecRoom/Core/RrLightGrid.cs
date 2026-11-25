using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's own point/spot light path for its own shaders (iteration 9), so the torches, lamps and fills light the
    /// rooms as they did in Rec Room.
    ///
    /// <b>Why.</b> Rec Room runs a fork of URP. Its shaders do not use URP's per-object light lists at all. The install's
    /// compiled shaders (AG/Standard-*, AG/MakerPenShape = our maker-pen walls, AG/Terrain*, AG/Fresnel*, AG/Avatar*,
    /// AG/StudioWater, "Rec Room Studio Target/Lit" = the cached Rec Center, disassembled offline, DevNotes/iter9/light9.md)
    /// find their lights in a world-space grid of 24 x 12 x 24 cells:
    /// <code>
    /// cell   = min(uint3(worldPos * _WorldSpaceToVoxelCoefficients + _VoxelVolumeWorldOriginPreMultiplied), (23, 11, 23))
    /// _Voxels[9 * (x + 24 y + 288 z)] = count, then up to 8 light indices
    /// atten  = saturate(1 - d^2 / _AdditionalLightsAttenuation[i].y)          (y = range^2, no inverse-square term)
    /// spot   = smoothstep(saturate((dot(_AdditionalLightsSpotDir[i], L) - atten.z) / (atten.w - atten.z)))^2
    /// light  = _AdditionalLightsColor[i].rgb * atten * spot * N.L;  specular x _AdditionalLightsColor[i].a
    /// </code>
    /// The Somnium client runs stock URP, which never binds <c>_Voxels</c>: the count reads 0 and every Rec Room surface
    /// gets only the sun and the ambient. That is why the Golden Trophy torches lit nothing.
    ///
    /// <b>What this does.</b> Per scene it fills that grid and Rec Room's light arrays itself and hands them to the
    /// renderers that use those shaders through one <see cref="MaterialPropertyBlock"/>: <c>_Voxels</c> (a structured
    /// buffer), the two grid vectors, and <c>_AdditionalLightsPosition/Color/Attenuation/SpotDir</c> and
    /// <c>_AdditionalShadowParams</c> in Rec Room's meaning. A property block on the renderer wins over URP's own global
    /// values of those names (URP writes them every frame in its own meaning), so URP-shaded objects (avatars, URP Lit)
    /// keep URP's lights and Rec Room's objects get Rec Room's. Each cell keeps the 8 strongest lights reaching it
    /// (Rec Room's own limit), so the client's per-object limit (2-8 lights) no longer applies to Rec Room surfaces.
    ///
    /// <b>Rec Room's URPLight component.</b> <see cref="Source.Specular"/> = specularContribution (0 on most of Rec Room's
    /// lights: no highlight), <see cref="Source.Hemisphere"/> = isHemisphereLight: a point light lighting the half space
    /// in front of it with a soft edge from the inner to the outer angle (hanging lanterns shine down, bounce lights up).
    ///
    /// <b>Cost.</b> A property block takes a renderer out of the SRP Batcher. Scenes lit this way therefore keep merged
    /// meshes (the grid picks lights per pixel, so merging no longer loses torches). Nothing global is changed: the
    /// blocks go with their renderers, the buffer with this component.
    ///
    /// <b>Not reproduced.</b> Additional-light shadows on Rec Room surfaces (URP's shadow slices are indexed by URP's own
    /// visible-light order, which a world cannot see): <c>_AdditionalShadowParams</c> says "no shadow" for every grid
    /// light. The sun's shadows are unaffected.
    /// </summary>
    public sealed class RrLightGrid : MonoBehaviour
    {
        public const string Area = "Lights";
        public const int NX = 24, NY = 12, NZ = 24;
        public const int Cells = NX * NY * NZ;
        public const int PerCell = 8;
        public const int Stride = PerCell + 1;

        /// <summary>Rec Room's light arrays hold 160 lights (cbuffer AdditionalLights, 5 x 160 float4 in the fork).</summary>
        public const int MaxLights = 160;

        /// <summary>Seconds between scans of the scope for renderers that appeared (props, pickups, enemies).</summary>
        public const float RescanSeconds = 3f;

        private static readonly int VoxelsId = Shader.PropertyToID("_Voxels");
        private static readonly int OriginId = Shader.PropertyToID("_VoxelVolumeWorldOriginPreMultiplied");
        private static readonly int CoeffId = Shader.PropertyToID("_WorldSpaceToVoxelCoefficients");
        private static readonly int PosId = Shader.PropertyToID("_AdditionalLightsPosition");
        private static readonly int ColId = Shader.PropertyToID("_AdditionalLightsColor");
        private static readonly int AttId = Shader.PropertyToID("_AdditionalLightsAttenuation");
        private static readonly int DirId = Shader.PropertyToID("_AdditionalLightsSpotDir");
        private static readonly int ShadowId = Shader.PropertyToID("_AdditionalShadowParams");
        private static readonly int AdditionalLightsId = Shader.PropertyToID("AdditionalLights");

        /// <summary>float4s bound as Rec Room's AdditionalLights cbuffer (see MakeConstantBuffer).</summary>
        public const int CbFloat4s = 2048;

        /// <summary>
        /// Bind Rec Room's whole AdditionalLights cbuffer as one GPU buffer (default) instead of four arrays: the arrays make
        /// Unity rebuild and upload the 18 KB cbuffer on every draw of every lit renderer (harness run 5: +4 to +10 ms of
        /// Camera.Render per view).
        /// </summary>
        public static bool UseConstantBuffer = true;

        /// <summary>One light as Rec Room had it, in the host's local space (the kit root: the scene's own frame).</summary>
        public struct Source
        {
            public string Name;
            public LightType Type;
            public Vector3 Position;
            public Quaternion Rotation;

            /// <summary>The light's colour as Unity's inspector shows it (sRGB).</summary>
            public Color Color;

            public float Intensity;
            public float Range;

            /// <summary>Spot: the full outer / inner cone angles (degrees).</summary>
            public float SpotAngle;

            public float InnerSpotAngle;

            /// <summary>URPLight.specularContribution (1 when the light has no URPLight).</summary>
            public float Specular;

            /// <summary>URPLight.isHemisphereLight with its inner and outer angles (full angles, degrees).</summary>
            public bool Hemisphere;

            public float HemiInner;
            public float HemiOuter;

            /// <summary>The Unity light made for it (URP-shaded objects, avatars), when there is one.</summary>
            public Light Unity;
        }

        private static readonly List<RrLightGrid> Live = new List<RrLightGrid>();
        private static readonly Dictionary<int, RrLightGrid> Claimed = new Dictionary<int, RrLightGrid>();
        private static readonly Dictionary<int, bool> ShaderUses = new Dictionary<int, bool>();
        private static readonly List<Renderer> Scan = new List<Renderer>();
        private static MaterialPropertyBlock _tmp;
        private static bool _enabled = true;
        private static int _logged;

        /// <summary>Harness before/after: false takes every grid off its renderers (iteration 8's look).</summary>
        public static bool Enabled
        {
            get { return _enabled; }
        }

        public string SceneName = "";

        /// <summary>The subtree whose renderers this grid lights (the kit root's parent: the area with its hull and dressing).</summary>
        public Transform Scope;

        /// <summary>The grid's box in the host's local space.</summary>
        public Bounds LocalVolume;

        public readonly List<Source> Sources = new List<Source>();

        /// <summary>Filled after the build (diagnostics).</summary>
        public int Renderers, Lights, CellsLit, CellsFull, MaxInCell, Dropped, Skipped, Unreached;

        public float BuildMs;
        public bool Built;

        /// <summary>The grid's world box after the build (the scene box cut to where its lights reach).</summary>
        public Bounds GridBox;

        private readonly List<Renderer> _applied = new List<Renderer>();
        private readonly HashSet<int> _rejected = new HashSet<int>();
        private bool _scanning;
        private static RrLightGrid _builder;
        private MaterialPropertyBlock _block;
        private ComputeBuffer _voxels;
        private GraphicsBuffer _lightsCb;
        private Vector4[] _pos, _col, _att, _dir, _shadow;
        private Vector4 _origin, _coeff;
        private int _state;
        private int _frames;
        private float _nextScan;
        private float _createdAt;

        /// <summary>
        /// Adds a grid to <paramref name="host"/> for <paramref name="sources"/> (host-local). It builds itself once the room
        /// has finished building (travel not busy) and the host is active, then keeps its scope's new renderers lit.
        /// </summary>
        public static RrLightGrid Attach(GameObject host, Transform scope, string sceneName, Bounds localVolume, List<Source> sources)
        {
            if (host == null || sources == null || sources.Count == 0)
            {
                return null;
            }

            var g = host.AddComponent<RrLightGrid>();
            g.SceneName = sceneName ?? host.name;
            g.Scope = scope != null ? scope : host.transform;
            g.LocalVolume = localVolume;
            // The strongest first, so a scene with more than 160 lights keeps the ones that matter.
            sources.Sort((a, b) => Score(b).CompareTo(Score(a)));
            for (int i = 0; i < sources.Count && i < MaxLights; i++)
            {
                g.Sources.Add(sources[i]);
            }

            g.Dropped = Mathf.Max(0, sources.Count - MaxLights);
            return g;
        }

        /// <summary>Rec Room's shaders that read the grid (install shader disassembly, iteration 9).</summary>
        public static bool UsesGrid(Shader s)
        {
            if (s == null)
            {
                return false;
            }

            int id = s.GetInstanceID();
            bool uses;
            if (ShaderUses.TryGetValue(id, out uses))
            {
                return uses;
            }

            string n = s.name ?? string.Empty;
            uses = n.StartsWith("AG/Standard-", System.StringComparison.Ordinal) || n.StartsWith("AG/MakerPenShape", System.StringComparison.Ordinal)
                || n.StartsWith("AG/Terrain", System.StringComparison.Ordinal) || n.StartsWith("AG/Fresnel", System.StringComparison.Ordinal)
                || n.StartsWith("AG/Avatar", System.StringComparison.Ordinal) || n == "AG/StudioWater" || n == "Rec Room Studio Target/Lit";
            ShaderUses[id] = uses;
            return uses;
        }

        public static bool UsesGrid(Renderer r)
        {
            if (r == null)
            {
                return false;
            }

            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && UsesGrid(mats[i].shader))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The stock-URP twin of a Rec Room light: URP's inverse-square falloff is far darker than Rec Room's
        /// 1 - (d/r)^2 at a distance and far brighter right at the light. The intensity is matched at half the range,
        /// kept between x0.5 and x4 (a torch's 5.8 m light x4, its 1.4 m hot spot x0.5), so avatars and URP-shaded
        /// objects near a torch are lit about as much as the Rec Room surfaces around them.
        /// </summary>
        public static float StockFactor(float range)
        {
            // Rec Room at d = r/2: 0.75 I. URP: I' / (r/2)^2 * (1 - (1/4)^2)^2 = 3.52 I' / r^2  ->  I' = 0.213 r^2 I.
            return Mathf.Clamp(0.213f * range * range, 0.5f, 4f);
        }

        /// <summary>
        /// Sets up a Unity light for URP-shaded objects the way Rec Room's light looked: hemisphere lights become wide
        /// spots (outer angle capped at 179), the intensity gets <see cref="StockFactor"/>. URP has no per-light
        /// specular switch, so specularContribution 0 is honoured on Rec Room surfaces only.
        /// </summary>
        public static void ConfigureUnity(Light light, Source s)
        {
            if (light == null)
            {
                return;
            }

            light.color = s.Color;
            light.range = Mathf.Max(0.05f, s.Range);
            light.intensity = s.Intensity * StockFactor(s.Range);
            if (s.Hemisphere && s.Type != LightType.Spot)
            {
                light.type = LightType.Spot;
                light.spotAngle = Mathf.Clamp(s.HemiOuter, 1f, 179f);
                light.innerSpotAngle = Mathf.Clamp(s.HemiInner, 0f, light.spotAngle);
            }
            else if (s.Type == LightType.Spot)
            {
                light.type = LightType.Spot;
                light.spotAngle = Mathf.Clamp(s.SpotAngle, 1f, 179f);
                light.innerSpotAngle = Mathf.Clamp(s.InnerSpotAngle >= 0f ? s.InnerSpotAngle : light.spotAngle * 0.727f, 0f, light.spotAngle);
            }
            else
            {
                light.type = LightType.Point;
            }
        }

        private static float Score(Source s)
        {
            return s.Intensity * Mathf.Max(0.05f, s.Range) * Mathf.Max(0.01f, s.Color.maxColorComponent);
        }

        // ---- lifecycle ----

        private void Awake()
        {
            Live.Add(this);
            _createdAt = Time.realtimeSinceStartup;
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_builder == this)
            {
                _builder = null;
            }

            foreach (var r in _applied)
            {
                if (r != null)
                {
                    Claimed.Remove(r.GetInstanceID());
                }
            }

            _applied.Clear();
            if (_voxels != null)
            {
                _voxels.Release();
                _voxels = null;
            }

            if (_lightsCb != null)
            {
                _lightsCb.Release();
                _lightsCb = null;
            }

            if (Live.Count == 0)
            {
                UseConstantBuffer = true;
                // Statics survive a world re-entry (the assembly stays loaded).
                Claimed.Clear();
                ShaderUses.Clear();
                _enabled = true;
                _logged = 0;
            }
        }

        private void Update()
        {
            if (_state == 0)
            {
                _frames++;
                bool busy = Rooms.RrTravel.Busy;
                // One grid builds at a time (a quest builds 11 areas at once): each keeps to 2.5 ms slices, so a frame
                // never carries more than one grid's slice.
                if (_frames > 2 && (!busy || Time.realtimeSinceStartup - _createdAt > 30f) && (_builder == null || _builder == this))
                {
                    _builder = this;
                    _state = 1;
                    StartCoroutine(Build());
                }

                return;
            }

            if (_state == 2 && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + RescanSeconds;
                if (_enabled && !_scanning)
                {
                    StartCoroutine(RescanRoutine());
                }
            }
        }

        private void OnDisable()
        {
            // A coroutine stops with its object: a hidden area must not hold the build slot or a scan flag.
            _scanning = false;
            if (_builder == this)
            {
                _builder = null;
                if (_state == 1)
                {
                    _state = 0;
                    _frames = 0;
                }
            }
        }

        // ---- the build ----

        private IEnumerator Build()
        {
            if (_voxels != null)
            {
                // a build that was cut short by the area being hidden starts again
                _voxels.Release();
                _voxels = null;
            }

            var sw = Stopwatch.StartNew();
            var slice = Stopwatch.StartNew();
            var t = transform;

            // The grid box in world space: the host's local box (its 8 corners), or, when none was given, the scope's
            // renderers that use Rec Room's shaders.
            var lb = LocalVolume;
            Bounds wb;
            if (lb.size.sqrMagnitude < 1e-4f)
            {
                wb = RendererBounds();
            }
            else
            {
                wb = new Bounds(t.TransformPoint(lb.center), Vector3.zero);
                for (int c = 0; c < 8; c++)
                {
                    var p = lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) != 0 ? 1f : -1f, (c & 2) != 0 ? 1f : -1f, (c & 4) != 0 ? 1f : -1f));
                    wb.Encapsulate(t.TransformPoint(p));
                }
            }

            // Only where the lights reach: a scene box that covers a whole map (the Paintball lobby's level spans 670 m for
            // lights in one 20 m building) would make 28 m cells. A pixel outside the grid takes the nearest edge cell,
            // whose lights are then out of range and add nothing (Rec Room's falloff is 0 beyond the range).
            bool anyLight = false;
            var reach = new Bounds();
            foreach (var s in Sources)
            {
                var p = t.TransformPoint(s.Position);
                var e = Vector3.one * Mathf.Max(0.05f, s.Range);
                var lb2 = new Bounds(p, e * 2f);
                if (!anyLight)
                {
                    reach = lb2;
                    anyLight = true;
                }
                else
                {
                    reach.Encapsulate(lb2);
                }
            }

            if (anyLight)
            {
                var lo = Vector3.Max(wb.min, reach.min);
                var hi = Vector3.Min(wb.max, reach.max);
                if (hi.x > lo.x && hi.y > lo.y && hi.z > lo.z)
                {
                    wb.SetMinMax(lo, hi);
                }
            }

            var size = new Vector3(Mathf.Max(0.5f, wb.size.x), Mathf.Max(0.5f, wb.size.y), Mathf.Max(0.5f, wb.size.z));
            GridBox = new Bounds(wb.center, size);
            var min = wb.center - size * 0.5f;
            _coeff = new Vector4(NX / size.x, NY / size.y, NZ / size.z, 0f);
            _origin = new Vector4(-min.x * _coeff.x, -min.y * _coeff.y, -min.z * _coeff.z, 0f);
            var cell = new Vector3(size.x / NX, size.y / NY, size.z / NZ);
            float cellDiag2 = cell.sqrMagnitude;

            int n = Sources.Count;
            _pos = new Vector4[n];
            _col = new Vector4[n];
            _att = new Vector4[n];
            _dir = new Vector4[n];
            _shadow = new Vector4[n];
            var wp = new Vector3[n];
            var fw = new Vector3[n];
            var cone = new Vector2[n]; // (cos outer/2, cos inner/2); x <= -1.5 = a full point light
            var lum = new float[n];
            for (int i = 0; i < n; i++)
            {
                var s = Sources[i];
                wp[i] = t.TransformPoint(s.Position);
                fw[i] = (t.rotation * s.Rotation * Vector3.forward).normalized;
                float r = Mathf.Max(0.05f, s.Range);
                var lin = s.Color.linear;
                float spec = s.Specular < 0f ? 1f : s.Specular;
                _pos[i] = new Vector4(wp[i].x, wp[i].y, wp[i].z, 1f);
                _col[i] = new Vector4(lin.r * s.Intensity, lin.g * s.Intensity, lin.b * s.Intensity, spec);
                float zc = -2f, wc = -1f;
                if (s.Hemisphere)
                {
                    zc = Mathf.Cos(Mathf.Clamp(s.HemiOuter, 1f, 360f) * 0.5f * Mathf.Deg2Rad);
                    wc = Mathf.Cos(Mathf.Clamp(s.HemiInner, 0f, s.HemiOuter) * 0.5f * Mathf.Deg2Rad);
                }
                else if (s.Type == LightType.Spot)
                {
                    float outer = Mathf.Clamp(s.SpotAngle, 1f, 179f);
                    float inner = Mathf.Clamp(s.InnerSpotAngle >= 0f ? s.InnerSpotAngle : outer * 0.727f, 0f, outer);
                    zc = Mathf.Cos(outer * 0.5f * Mathf.Deg2Rad);
                    wc = Mathf.Cos(inner * 0.5f * Mathf.Deg2Rad);
                }

                if (wc < zc + 0.001f)
                {
                    wc = zc + 0.001f;
                }

                cone[i] = new Vector2(zc, wc);
                _att[i] = new Vector4(1f / (r * r), r * r, zc, wc);
                _dir[i] = new Vector4(-fw[i].x, -fw[i].y, -fw[i].z, 0f);
                _shadow[i] = new Vector4(0f, 0f, 0f, -1f);
                lum[i] = s.Intensity * (0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b);
            }

            // Each cell keeps its 8 strongest lights: weight = brightness x Rec Room's falloff at the cell's nearest point,
            // x the cone at the cell's centre for spots and hemisphere lights (never below 0.2 for a cell the light is in or
            // next to, so a cell the cone edge crosses keeps the light).
            var ids = new int[Cells * PerCell];
            var ws = new float[Cells * PerCell];
            var count = new int[Cells];
            for (int i = 0; i < n; i++)
            {
                float r = Mathf.Max(0.05f, Sources[i].Range), r2 = r * r;
                var p = wp[i];
                int x0 = Mathf.Clamp(Mathf.FloorToInt((p.x - r - min.x) / cell.x), 0, NX - 1), x1 = Mathf.Clamp(Mathf.FloorToInt((p.x + r - min.x) / cell.x), 0, NX - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((p.y - r - min.y) / cell.y), 0, NY - 1), y1 = Mathf.Clamp(Mathf.FloorToInt((p.y + r - min.y) / cell.y), 0, NY - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((p.z - r - min.z) / cell.z), 0, NZ - 1), z1 = Mathf.Clamp(Mathf.FloorToInt((p.z + r - min.z) / cell.z), 0, NZ - 1);
                bool coned = cone[i].x > -1.5f;
                for (int z = z0; z <= z1; z++)
                {
                    for (int y = y0; y <= y1; y++)
                    {
                        for (int x = x0; x <= x1; x++)
                        {
                            var bmin = new Vector3(min.x + x * cell.x, min.y + y * cell.y, min.z + z * cell.z);
                            float dx = Mathf.Max(0f, Mathf.Max(bmin.x - p.x, p.x - (bmin.x + cell.x)));
                            float dy = Mathf.Max(0f, Mathf.Max(bmin.y - p.y, p.y - (bmin.y + cell.y)));
                            float dz = Mathf.Max(0f, Mathf.Max(bmin.z - p.z, p.z - (bmin.z + cell.z)));
                            float d2 = dx * dx + dy * dy + dz * dz;
                            if (d2 >= r2)
                            {
                                continue;
                            }

                            float w = lum[i] * (1f - d2 / r2);
                            if (coned)
                            {
                                var toCell = bmin + cell * 0.5f - p;
                                float len2 = toCell.sqrMagnitude;
                                if (len2 > cellDiag2)
                                {
                                    float cosA = Vector3.Dot(fw[i], toCell) / Mathf.Sqrt(len2);
                                    float k = Mathf.Clamp01((cosA - cone[i].x) / (cone[i].y - cone[i].x));
                                    // a cell wholly behind the cone (by more than its own size) gets nothing
                                    float slack = Mathf.Sqrt(cellDiag2 / len2);
                                    if (cosA + slack < cone[i].x)
                                    {
                                        continue;
                                    }

                                    w *= Mathf.Max(0.2f, k * k);
                                }
                            }

                            Insert(z * NX * NY + y * NX + x, i, w, ids, ws, count);
                        }
                    }
                }

                if (slice.Elapsed.TotalMilliseconds > 2.5)
                {
                    yield return null;
                    slice.Reset();
                    slice.Start();
                }
            }

            var data = new int[Cells * Stride];
            CellsLit = 0;
            CellsFull = 0;
            MaxInCell = 0;
            for (int c = 0; c < Cells; c++)
            {
                int k = count[c];
                data[c * Stride] = k;
                for (int j = 0; j < k; j++)
                {
                    data[c * Stride + 1 + j] = ids[c * PerCell + j];
                }

                if (k > 0)
                {
                    CellsLit++;
                }

                if (k >= PerCell)
                {
                    CellsFull++;
                }

                MaxInCell = Mathf.Max(MaxInCell, k);
            }

            _voxels = new ComputeBuffer(Cells * Stride, 4, ComputeBufferType.Structured);
            _voxels.SetData(data);
            MakeConstantBuffer();
            _block = new MaterialPropertyBlock();
            Fill(_block);
            Lights = n;
            yield return null;
            slice.Reset();
            slice.Start();

            var scan = RescanRoutine();
            while (scan.MoveNext())
            {
                yield return scan.Current;
            }

            BuildMs = (float)sw.Elapsed.TotalMilliseconds;
            Built = true;
            _state = 2;
            if (_builder == this)
            {
                _builder = null;
            }
            _nextScan = Time.unscaledTime + RescanSeconds;
            RrLog.Info(Area, Describe());
        }

        private Bounds RendererBounds()
        {
            bool any = false;
            var b = new Bounds(transform.position, new Vector3(40f, 10f, 40f));
            Scan.Clear();
            if (Scope != null)
            {
                Scope.GetComponentsInChildren(true, Scan);
            }

            foreach (var r in Scan)
            {
                if (r == null || r is ParticleSystemRenderer || !UsesGrid(r))
                {
                    continue;
                }

                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            Scan.Clear();
            b.Expand(1f);
            return b;
        }

        /// <summary>A Unity light as a grid source (rooms that bring their own lights: the cached Rec Center), host-local.</summary>
        public static Source FromLight(Light l, Transform host)
        {
            var t = l.transform;
            return new Source
            {
                Name = l.name,
                Type = l.type,
                Position = host != null ? host.InverseTransformPoint(t.position) : t.position,
                Rotation = host != null ? Quaternion.Inverse(host.rotation) * t.rotation : t.rotation,
                Color = l.color,
                Intensity = l.intensity,
                Range = l.range,
                SpotAngle = l.spotAngle,
                InnerSpotAngle = l.innerSpotAngle,
                Specular = 1f,
                HemiOuter = 180f,
                Unity = null
            };
        }

        /// <summary>Whether any of the grid's lights reaches a world box (built positions and ranges).</summary>
        private bool Reaches(Bounds b)
        {
            if (_pos == null)
            {
                return true;
            }

            for (int i = 0; i < _pos.Length; i++)
            {
                var p = new Vector3(_pos[i].x, _pos[i].y, _pos[i].z);
                if (b.SqrDistance(p) < _att[i].y)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Insert(int c, int light, float w, int[] ids, float[] ws, int[] count)
        {
            int k = count[c], b = c * PerCell;
            if (k < PerCell)
            {
                ids[b + k] = light;
                ws[b + k] = w;
                count[c] = k + 1;
                return;
            }

            // full: replace the weakest if this one is stronger
            int weakest = 0;
            for (int j = 1; j < PerCell; j++)
            {
                if (ws[b + j] < ws[b + weakest])
                {
                    weakest = j;
                }
            }

            if (w > ws[b + weakest])
            {
                ids[b + weakest] = light;
                ws[b + weakest] = w;
            }
        }

        private void Fill(MaterialPropertyBlock b)
        {
            b.SetBuffer(VoxelsId, _voxels);
            b.SetVector(OriginId, _origin);
            b.SetVector(CoeffId, _coeff);
            if (UseConstantBuffer && _lightsCb != null)
            {
                // Rec Room's whole "AdditionalLights" cbuffer as one GPU buffer: bound, not rebuilt per draw.
                b.SetConstantBuffer(AdditionalLightsId, _lightsCb, 0, CbFloat4s * 16);
                return;
            }

            b.SetVectorArray(PosId, _pos);
            b.SetVectorArray(ColId, _col);
            b.SetVectorArray(AttId, _att);
            b.SetVectorArray(DirId, _dir);
            b.SetVectorArray(ShadowId, _shadow);
        }

        /// <summary>
        /// Rec Room's cbuffer "AdditionalLights" as its shaders declare it (the same in every grid shader, from the
        /// disassembly): _CompactAdditionalLights{Position,Color,SpotDir}[160] at float4 0/160/320 (vertex-lit variant),
        /// then _AdditionalLightsPosition / Color / Attenuation / SpotDir [160] at 480 / 640 / 800 / 960. 2,048 float4
        /// are bound (32 KB, room for members after 1,120 the variants never read).
        /// </summary>
        private void MakeConstantBuffer()
        {
            var data = new Vector4[CbFloat4s];
            for (int i = 0; i < _pos.Length && i < MaxLights; i++)
            {
                data[480 + i] = _pos[i];
                data[640 + i] = _col[i];
                data[800 + i] = _att[i];
                data[960 + i] = _dir[i];
                // the vertex-lit variant's compact form: position + 1/r^2, colour + cos outer, direction + cos inner
                data[i] = new Vector4(_pos[i].x, _pos[i].y, _pos[i].z, _att[i].x);
                data[160 + i] = new Vector4(_col[i].x, _col[i].y, _col[i].z, _att[i].z);
                data[320 + i] = new Vector4(_dir[i].x, _dir[i].y, _dir[i].z, _att[i].w);
            }

            if (_lightsCb != null)
            {
                _lightsCb.Release();
            }

            _lightsCb = new GraphicsBuffer(GraphicsBuffer.Target.Constant, CbFloat4s, 16);
            _lightsCb.SetData(data);
        }

        /// <summary>
        /// Harness: every grid's block rebuilt in the given mode (constant buffer or per-property arrays) and put back on
        /// its renderers, to compare the two (pictures and Camera.Render time).
        /// </summary>
        public static void SetConstantBufferMode(bool on)
        {
            UseConstantBuffer = on;
            foreach (var g in Live)
            {
                if (g == null || g._block == null)
                {
                    continue;
                }

                g._block.Clear();
                g.Fill(g._block);
                foreach (var r in g._applied)
                {
                    if (r != null && _enabled)
                    {
                        r.SetPropertyBlock(g._block);
                    }
                }
            }

            RrLog.Info(Area, "blocks now carry " + (on ? "Rec Room's AdditionalLights cbuffer as one bound buffer" : "the light arrays as properties"));
        }

        /// <summary>
        /// Lights the scope's renderers that use Rec Room's shaders and are not lit yet, and gives a lit renderer its block
        /// back when someone cleared it (a hit flash ends with SetPropertyBlock(null)). Spread over frames (2 ms slices).
        /// </summary>
        private IEnumerator RescanRoutine()
        {
            if (_block == null || Scope == null || _scanning)
            {
                yield break;
            }

            _scanning = true;
            var sw = Stopwatch.StartNew();
            var list = new List<Renderer>();
            Scope.GetComponentsInChildren(true, list);
            var mine = transform;
            for (int i = 0; i < list.Count; i++)
            {
                if (sw.Elapsed.TotalMilliseconds > 2.0)
                {
                    yield return null;
                    sw.Reset();
                    sw.Start();
                    if (_block == null || !_enabled)
                    {
                        break;
                    }
                }

                var r = list[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                {
                    continue;
                }

                int id = r.GetInstanceID();
                RrLightGrid owner;
                if (Claimed.TryGetValue(id, out owner) && owner != null)
                {
                    if (owner == this && !r.HasPropertyBlock())
                    {
                        Apply(r);
                    }

                    continue;
                }

                if (_rejected.Contains(id))
                {
                    continue;
                }

                if (!UsesGrid(r) || UnderOtherGrid(r.transform, mine))
                {
                    _rejected.Add(id);
                    Skipped++;
                    continue;
                }

                // A static renderer no light reaches gains nothing from the block and keeps the SRP Batcher; anything that
                // can move (a rigidbody above it: props, pickups) is lit wherever it is.
                if (!Reaches(r.bounds) && r.GetComponentInParent<Rigidbody>() == null)
                {
                    _rejected.Add(id);
                    Unreached++;
                    continue;
                }

                Claimed[id] = this;
                _applied.Add(r);
                Renderers++;
                Apply(r);
            }

            _scanning = false;
        }

        private bool UnderOtherGrid(Transform t, Transform mine)
        {
            // A renderer inside another scene's kit root belongs to that scene's grid (Paintball: the lobby and a map
            // under one room), even before that grid has scanned.
            for (var p = t; p != null && p != Scope; p = p.parent)
            {
                if (p == mine)
                {
                    return false;
                }

                var other = p.GetComponent<RrLightGrid>();
                if (other != null && other != this)
                {
                    return true;
                }
            }

            return false;
        }

        private void Apply(Renderer r)
        {
            if (!_enabled)
            {
                return;
            }

            if (_tmp == null)
            {
                _tmp = new MaterialPropertyBlock();
            }

            if (r.HasPropertyBlock())
            {
                r.GetPropertyBlock(_tmp);
                if (_tmp.HasVector(CoeffId))
                {
                    return;
                }

                // Someone else's block (a hit flash): add ours to it.
                Fill(_tmp);
                r.SetPropertyBlock(_tmp);
                _tmp.Clear();
                return;
            }

            r.SetPropertyBlock(_block);
        }

        private void Remove()
        {
            foreach (var r in _applied)
            {
                if (r != null && r.HasPropertyBlock())
                {
                    r.SetPropertyBlock(null);
                }
            }
        }

        // ---- harness / diagnostics ----

        /// <summary>
        /// Harness before/after: off takes every grid's blocks off (Rec Room surfaces unlit by point lights, as in
        /// iteration 8) and gives the Unity lights their iteration-8 values; on puts both back.
        /// </summary>
        public static void SetEnabled(bool on)
        {
            _enabled = on;
            foreach (var g in Live)
            {
                if (g == null)
                {
                    continue;
                }

                if (on)
                {
                    foreach (var r in g._applied)
                    {
                        if (r != null)
                        {
                            g.Apply(r);
                        }
                    }
                }
                else
                {
                    g.Remove();
                }

                foreach (var s in g.Sources)
                {
                    if (s.Unity == null)
                    {
                        continue;
                    }

                    if (on)
                    {
                        ConfigureUnity(s.Unity, s);
                    }
                    else
                    {
                        // iteration 8: the record's own numbers, hemisphere lights as plain points
                        s.Unity.type = s.Type;
                        s.Unity.intensity = s.Intensity;
                        s.Unity.range = Mathf.Max(0.05f, s.Range);
                        if (s.Type == LightType.Spot)
                        {
                            s.Unity.spotAngle = Mathf.Clamp(s.SpotAngle, 1f, 179f);
                        }
                    }
                }
            }

            RrLog.Info(Area, "grids " + (on ? "on" : "off (iteration 8's lighting)") + " for " + Live.Count + " scene(s)");
        }

        public string Describe()
        {
            int hemi = 0, spec0 = 0, unity = 0;
            foreach (var s in Sources)
            {
                if (s.Hemisphere)
                {
                    hemi++;
                }

                if (s.Specular == 0f)
                {
                    spec0++;
                }

                if (s.Unity != null)
                {
                    unity++;
                }
            }

            return SceneName + ": Rec Room's light grid (24x12x24 cells, up to 8 lights each) with " + Lights + " lights (" + hemi + " hemisphere, " + spec0 + " without highlights, "
                + unity + " also as Unity lights for URP-shaded objects" + (Dropped > 0 ? ", " + Dropped + " over Rec Room's 160 left out" : "") + "); "
                + "cells " + (GridBox.size.x / NX).ToString("0.0#", CultureInfo.InvariantCulture) + " x " + (GridBox.size.y / NY).ToString("0.0#", CultureInfo.InvariantCulture) + " x " + (GridBox.size.z / NZ).ToString("0.0#", CultureInfo.InvariantCulture) + " m, "
                + CellsLit + "/" + Cells + " cells lit, " + CellsFull + " full, max " + MaxInCell + " per cell; " + Renderers + " renderers with Rec Room's shaders lit" + (Unreached > 0 ? " (" + Unreached + " out of every light's reach left batched)" : "") + "; "
                + BuildMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms";
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            sb.Append(Live.Count).Append(" light grid(s)").Append(_enabled ? "" : " (off)");
            foreach (var g in Live)
            {
                if (g != null)
                {
                    sb.Append("\n  ").Append(g.Built ? g.Describe() : g.SceneName + ": waiting (" + g.Sources.Count + " lights)");
                }
            }

            return sb.ToString();
        }

        /// <summary>The grid lighting a renderer, if any (harness probes).</summary>
        public static RrLightGrid Of(Renderer r)
        {
            RrLightGrid g;
            return r != null && Claimed.TryGetValue(r.GetInstanceID(), out g) ? g : null;
        }

        /// <summary>
        /// What Rec Room's shader computes at a world point with a normal, from this grid (harness probe): the summed
        /// diffuse colour of the cell's lights, and how many lights the cell holds.
        /// </summary>
        public Vector3 Probe(Vector3 p, Vector3 normal, out int lightsInCell)
        {
            lightsInCell = 0;
            if (_voxels == null)
            {
                return Vector3.zero;
            }

            var data = new int[Cells * Stride];
            _voxels.GetData(data);
            var v = new Vector3(p.x * _coeff.x + _origin.x, p.y * _coeff.y + _origin.y, p.z * _coeff.z + _origin.z);
            int x = Mathf.Clamp((int)Mathf.Max(0f, v.x), 0, NX - 1), y = Mathf.Clamp((int)Mathf.Max(0f, v.y), 0, NY - 1), z = Mathf.Clamp((int)Mathf.Max(0f, v.z), 0, NZ - 1);
            int c = x + NX * y + NX * NY * z;
            int k = data[c * Stride];
            lightsInCell = k;
            var sum = Vector3.zero;
            for (int j = 0; j < k; j++)
            {
                int i = data[c * Stride + 1 + j];
                var L = new Vector3(_pos[i].x, _pos[i].y, _pos[i].z) - p;
                float d2 = Mathf.Max(L.sqrMagnitude, 0.000061f);
                L /= Mathf.Sqrt(d2);
                float atten = Mathf.Clamp01(1f - d2 / _att[i].y);
                float s = Mathf.Clamp01((Vector3.Dot(new Vector3(_dir[i].x, _dir[i].y, _dir[i].z), L) - _att[i].z) / (_att[i].w - _att[i].z));
                s = s * s * (3f - 2f * s);
                atten *= s * s;
                float ndl = Mathf.Clamp01(Vector3.Dot(normal, L));
                sum += new Vector3(_col[i].x, _col[i].y, _col[i].z) * (atten * ndl);
            }

            return sum;
        }

        internal static void LogOnce(string text)
        {
            if (_logged++ < 4)
            {
                RrLog.Info(Area, text);
            }
        }

        public static IEnumerable<RrLightGrid> All
        {
            get { return Live; }
        }
    }
}
