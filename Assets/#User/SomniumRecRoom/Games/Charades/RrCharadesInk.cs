using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Charades
{
    /// <summary>
    /// The Charades Pen's 3D ink: tubes drawn by the performer, seen by everyone in the instance.
    ///
    /// Rec Room's pen (a Maker Pen variant, rr_party.md §1.3) extrudes an 8-sided tube from the pen's
    /// ExtrusionPoint whose thickness follows the trigger (VR) or the cursor speed (screen mode). Here a stroke is a
    /// list of points sampled every <see cref="MinStep"/> m, each with its own radius, quantised to 1 mm (radius to
    /// 0.25 mm) in the canvas's space (the stage), so the performer's own tube and everyone else's are built from the
    /// same numbers. The performer draws locally without latency and streams the new points as
    /// <see cref="EvPoints"/> events (one stroke segment per event, at most 60 points, flushed every 0.1 s); other
    /// clients ignore anything not from the current performer or not for the current drawing
    /// (<see cref="Key"/>: the turn and word, so a late event from the previous word never draws on the new one).
    /// Segments carry their start index, so a re-send (a late joiner's <see cref="EvResend"/>) is idempotent.
    /// The ink is cleared whenever the key changes (the original removes the drawing when the performer's turn or
    /// word ends).
    ///
    /// Tubes use <see cref="RrLook.Solid"/> (Rec Room's maker-pen paint material, per colour) so they look like
    /// Maker Pen ink; <c>AG-MakerPenShape</c> itself has no container key (rr_party.md §9).
    /// </summary>
    public sealed class RrCharadesInk : MonoBehaviour
    {
        /// <summary>Performer -> all: [key u16][stroke u16][colour u8][start u16][n u8] + n x (x,y,z s16 mm, r u8 0.25 mm).</summary>
        public const byte EvPoints = 64;
        /// <summary>Performer -> all: [key u16][stroke u16] removes one stroke (undo).</summary>
        public const byte EvUndo = 65;
        /// <summary>Performer -> all: [key u16] removes the whole drawing.</summary>
        public const byte EvClear = 66;
        /// <summary>Anyone -> all: [key u16] "send me the drawing" (late joiner, or a gap was seen).</summary>
        public const byte EvResend = 68;

        public const int Sides = 8;
        public const int MaxPointsPerStroke = 240;
        public const int MaxPointsPerEvent = 60;
        public const int MaxPointsPerDrawing = 12000;
        public const float MinStep = 0.008f;
        public const float FlushEvery = 0.1f;

        /// <summary>The pen's 12 colours (Rec Room sets its swatches at runtime; GUESS palette in its style).</summary>
        public static readonly Color[] Palette =
        {
            new Color(0.95f, 0.95f, 0.93f), new Color(0.10f, 0.10f, 0.12f), new Color(0.90f, 0.20f, 0.18f),
            new Color(1.00f, 0.55f, 0.05f), new Color(1.00f, 0.85f, 0.15f), new Color(0.35f, 0.75f, 0.25f),
            new Color(0.15f, 0.70f, 0.65f), new Color(0.20f, 0.45f, 0.90f), new Color(0.55f, 0.30f, 0.80f),
            new Color(0.97f, 0.50f, 0.70f), new Color(0.50f, 0.32f, 0.18f), new Color(0.55f, 0.56f, 0.58f)
        };

        private sealed class Stroke
        {
            public int Id;
            public byte Colour;
            public readonly List<Vector3> Points = new List<Vector3>();
            public readonly List<float> Radii = new List<float>();
            public GameObject Go;
            public Mesh Mesh;
            public bool Dirty;
            public int Sent;
            public bool Local;
        }

        private readonly Dictionary<int, Stroke> _strokes = new Dictionary<int, Stroke>();
        private readonly List<Stroke> _order = new List<Stroke>();
        private readonly List<Stroke> _pool = new List<Stroke>();
        private Stroke _active;
        private int _nextId = 1;
        private int _totalPoints;
        private float _flushAt;
        private float _resendAskedAt = -10f;
        private float _resendServedAt = -10f;
        private bool _handlers;

        // mesh scratch
        private static readonly List<Vector3> Verts = new List<Vector3>();
        private static readonly List<Vector3> Normals = new List<Vector3>();
        private static readonly List<int> Tris = new List<int>();

        /// <summary>The drawing being shown (turn and word, 16 bits); ink for any other key is ignored.</summary>
        public int Key { get; private set; } = -1;

        /// <summary>PlayerId allowed to draw (the performer), -1 for nobody.</summary>
        public int PerformerId { get; private set; } = -1;

        /// <summary>This client is the one drawing (its strokes are sent; incoming ink is ignored).</summary>
        public bool LocalPerformer { get; private set; }

        public int StrokeCount
        {
            get { return _order.Count; }
        }

        public int PointCount
        {
            get { return _totalPoints; }
        }

        public bool Drawing
        {
            get { return _active != null; }
        }

        /// <summary>Counters for the log and the harness.</summary>
        public int SentEvents, ReceivedEvents;

        private void OnEnable()
        {
            if (_handlers)
            {
                return;
            }

            _handlers = true;
            RrNet.On(EvPoints, OnPoints);
            RrNet.On(EvUndo, OnUndo);
            RrNet.On(EvClear, OnClear);
            RrNet.On(EvResend, OnResend);
        }

        private void OnDisable()
        {
            if (!_handlers)
            {
                return;
            }

            _handlers = false;
            RrNet.Off(EvPoints, OnPoints);
            RrNet.Off(EvUndo, OnUndo);
            RrNet.Off(EvClear, OnClear);
            RrNet.Off(EvResend, OnResend);
        }

        private void OnDestroy()
        {
            OnDisable();
            ClearLocal();
            foreach (var s in _pool)
            {
                if (s.Mesh != null)
                {
                    Destroy(s.Mesh);
                }
            }

            _pool.Clear();
        }

        /// <summary>Called every frame by the room: which drawing is current and who may draw it.</summary>
        public void SetDrawing(int key, int performerId, bool localPerformer)
        {
            key &= 0xFFFF;
            if (key != Key)
            {
                if (_order.Count > 0)
                {
                    RrLog.Info("Charades", "ink cleared for drawing " + key + " (" + _order.Count + " strokes, " + _totalPoints + " points)");
                }

                ClearLocal();
                Key = key;
                _nextId = 1;
            }

            PerformerId = performerId;
            if (LocalPerformer && !localPerformer)
            {
                EndStroke();
            }

            LocalPerformer = localPerformer;
        }

        // ------------------------------------------------------------------ performer API

        /// <summary>Starts a stroke at a world point (performer only).</summary>
        public void BeginStroke(byte colour, Vector3 world, float radius)
        {
            if (!LocalPerformer)
            {
                return;
            }

            EndStroke();
            if (_totalPoints >= MaxPointsPerDrawing)
            {
                RrLog.WarnOnce("Charades", "ink-full", "the drawing reached " + MaxPointsPerDrawing + " points; undo or clear to draw more");
                return;
            }

            _active = NewStroke(_nextId, colour, true);
            _nextId = _nextId >= 0xFFFF ? 1 : _nextId + 1;
            AddQuantised(_active, Quantise(transform.InverseTransformPoint(world)), QuantiseRadius(radius));
        }

        /// <summary>Extends the current stroke when the point is at least <see cref="MinStep"/> from the last one.</summary>
        public void AddPoint(Vector3 world, float radius)
        {
            if (_active == null)
            {
                return;
            }

            var local = Quantise(transform.InverseTransformPoint(world));
            var last = _active.Points[_active.Points.Count - 1];
            if ((local - last).sqrMagnitude < MinStep * MinStep)
            {
                return;
            }

            if (_totalPoints >= MaxPointsPerDrawing)
            {
                EndStroke();
                return;
            }

            if (_active.Points.Count >= MaxPointsPerStroke)
            {
                // continue as a new stroke from the last point so meshes and events stay small
                byte colour = _active.Colour;
                float lastRadius = _active.Radii[_active.Radii.Count - 1];
                _active = NewStroke(_nextId, colour, true);
                _nextId = _nextId >= 0xFFFF ? 1 : _nextId + 1;
                AddQuantised(_active, last, lastRadius);
            }

            AddQuantised(_active, local, QuantiseRadius(radius));
        }

        public void EndStroke()
        {
            _active = null;
        }

        /// <summary>Removes the performer's last stroke (sent to everyone).</summary>
        public bool Undo()
        {
            if (!LocalPerformer || _order.Count == 0)
            {
                return false;
            }

            EndStroke();
            var s = _order[_order.Count - 1];
            RemoveStroke(s);
            var body = new byte[4];
            Put16(body, 0, Key);
            Put16(body, 2, s.Id);
            Send(EvUndo, body);
            return true;
        }

        /// <summary>Removes the whole drawing (performer: sent to everyone).</summary>
        public void ClearAll()
        {
            if (!LocalPerformer)
            {
                return;
            }

            ClearLocal();
            var body = new byte[2];
            Put16(body, 0, Key);
            Send(EvClear, body);
        }

        /// <summary>Asks the performer for the current drawing (late joiner); throttled to once per 3 s.</summary>
        public void RequestResend()
        {
            if (LocalPerformer || Key < 0 || Time.unscaledTime - _resendAskedAt < 3f)
            {
                return;
            }

            _resendAskedAt = Time.unscaledTime;
            var body = new byte[2];
            Put16(body, 0, Key);
            Send(EvResend, body);
            RrLog.Info("Charades", "asked the performer for drawing " + Key);
        }

        /// <summary>Removes every stroke here without telling anyone.</summary>
        public void ClearLocal()
        {
            _active = null;
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                Recycle(_order[i]);
            }

            _order.Clear();
            _strokes.Clear();
            _totalPoints = 0;
        }

        /// <summary>World-space centre of the drawing (for effects), or the canvas origin when empty.</summary>
        public Vector3 Centre()
        {
            if (_order.Count == 0)
            {
                return transform.position;
            }

            var b = new Bounds();
            bool any = false;
            foreach (var s in _order)
            {
                if (s.Go == null)
                {
                    continue;
                }

                var r = s.Go.GetComponent<Renderer>();
                if (r == null)
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

            return any ? b.center : transform.position;
        }

        // ------------------------------------------------------------------ frame

        private void LateUpdate()
        {
            if (LocalPerformer && Time.unscaledTime >= _flushAt)
            {
                _flushAt = Time.unscaledTime + FlushEvery;
                Flush(6);
            }

            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i].Dirty)
                {
                    Rebuild(_order[i]);
                }
            }
        }

        /// <summary>Sends unsent points of local strokes, at most <paramref name="maxEvents"/> events.</summary>
        private void Flush(int maxEvents)
        {
            int events = 0;
            for (int i = 0; i < _order.Count && events < maxEvents; i++)
            {
                var s = _order[i];
                if (!s.Local)
                {
                    continue;
                }

                while (s.Sent < s.Points.Count && events < maxEvents)
                {
                    int n = Mathf.Min(MaxPointsPerEvent, s.Points.Count - s.Sent);
                    Send(EvPoints, EncodePoints(Key, s.Id, s.Colour, s.Sent, s.Points, s.Radii, n));
                    s.Sent += n;
                    events++;
                }
            }
        }

        /// <summary>The wire form of one stroke segment (public for the harness's decode check).</summary>
        public static byte[] EncodePoints(int key, int strokeId, byte colour, int start, List<Vector3> points, List<float> radii, int n)
        {
            var body = new byte[8 + 7 * n];
            Put16(body, 0, key);
            Put16(body, 2, strokeId);
            body[4] = colour;
            Put16(body, 5, start);
            body[7] = (byte)n;
            int o = 8;
            for (int i = 0; i < n; i++)
            {
                var p = points[start + i];
                PutS16(body, o, Mathf.RoundToInt(p.x * 1000f));
                PutS16(body, o + 2, Mathf.RoundToInt(p.y * 1000f));
                PutS16(body, o + 4, Mathf.RoundToInt(p.z * 1000f));
                body[o + 6] = (byte)Mathf.Clamp(Mathf.RoundToInt(radii[start + i] * 4000f), 1, 255);
                o += 7;
            }

            return body;
        }

        private void Send(byte type, byte[] body)
        {
            SentEvents++;
            RrNet.Send(type, body);
        }

        // ------------------------------------------------------------------ receiving

        private bool Accept(int sender, byte[] body, int minLength)
        {
            if (body == null || body.Length < minLength || LocalPerformer)
            {
                return false;
            }

            if (sender == RrNet.LocalId && !RrNet.Online)
            {
                return false; // our own loop-back offline
            }

            if (sender != PerformerId)
            {
                return false;
            }

            return Get16(body, 0) == Key;
        }

        private void OnPoints(int sender, byte[] body)
        {
            if (!Accept(sender, body, 8))
            {
                return;
            }

            ReceivedEvents++;
            ApplyPoints(body);
        }

        /// <summary>Draws one received segment (no sender checks: the harness feeds it directly).</summary>
        public void ApplyPoints(byte[] body)
        {
            if (body == null || body.Length < 8)
            {
                return;
            }

            int id = Get16(body, 2);
            byte colour = body[4];
            int start = Get16(body, 5);
            int n = body[7];
            if (body.Length < 8 + 7 * n)
            {
                return;
            }

            Stroke s;
            if (!_strokes.TryGetValue(id, out s))
            {
                s = NewStroke(id, colour, false);
            }

            if (start > s.Points.Count)
            {
                RequestResend(); // a gap: something was lost; ask again and draw what we have
            }

            int o = 8;
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(GetS16(body, o) * 0.001f, GetS16(body, o + 2) * 0.001f, GetS16(body, o + 4) * 0.001f);
                float r = body[o + 6] * 0.00025f;
                int at = start + i;
                if (at < s.Points.Count)
                {
                    s.Points[at] = p;
                    s.Radii[at] = r;
                    s.Dirty = true;
                }
                else if (_totalPoints < MaxPointsPerDrawing)
                {
                    AddQuantised(s, p, r);
                }

                o += 7;
            }
        }

        private void OnUndo(int sender, byte[] body)
        {
            if (!Accept(sender, body, 4))
            {
                return;
            }

            ReceivedEvents++;
            Stroke s;
            if (_strokes.TryGetValue(Get16(body, 2), out s))
            {
                RemoveStroke(s);
            }
        }

        private void OnClear(int sender, byte[] body)
        {
            if (!Accept(sender, body, 2))
            {
                return;
            }

            ReceivedEvents++;
            ClearLocal();
        }

        private void OnResend(int sender, byte[] body)
        {
            if (!LocalPerformer || sender == RrNet.LocalId || body == null || body.Length < 2 || Get16(body, 0) != Key)
            {
                return;
            }

            if (Time.unscaledTime - _resendServedAt < 2f)
            {
                return;
            }

            _resendServedAt = Time.unscaledTime;
            foreach (var s in _order)
            {
                s.Sent = 0; // the next flushes send everything again, a few events per flush
            }

            RrLog.Info("Charades", "re-sending drawing " + Key + " (" + _order.Count + " strokes) for player " + sender);
        }

        // ------------------------------------------------------------------ strokes

        private Stroke NewStroke(int id, byte colour, bool local)
        {
            Stroke s;
            if (_pool.Count > 0)
            {
                s = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                s.Points.Clear();
                s.Radii.Clear();
            }
            else
            {
                s = new Stroke();
                s.Go = new GameObject("Stroke");
                s.Go.transform.SetParent(transform, false);
                s.Mesh = new Mesh { name = "Charades Ink" };
                s.Mesh.MarkDynamic();
                s.Go.AddComponent<MeshFilter>().sharedMesh = s.Mesh;
                var mr = s.Go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
            }

            s.Id = id;
            s.Colour = colour;
            s.Local = local;
            s.Sent = 0;
            s.Dirty = true;
            s.Go.name = "Stroke " + id;
            s.Go.GetComponent<MeshRenderer>().sharedMaterial = RrLook.Solid(Palette[colour % Palette.Length]);
            s.Go.SetActive(true);
            _strokes[id] = s;
            _order.Add(s);
            return s;
        }

        private void AddQuantised(Stroke s, Vector3 local, float radius)
        {
            s.Points.Add(local);
            s.Radii.Add(radius);
            s.Dirty = true;
            _totalPoints++;
        }

        private void RemoveStroke(Stroke s)
        {
            if (_active == s)
            {
                _active = null;
            }

            _strokes.Remove(s.Id);
            _order.Remove(s);
            Recycle(s);
        }

        private void Recycle(Stroke s)
        {
            _totalPoints -= s.Points.Count;
            if (_totalPoints < 0)
            {
                _totalPoints = 0;
            }

            s.Points.Clear();
            s.Radii.Clear();
            if (s.Mesh != null)
            {
                s.Mesh.Clear();
            }

            if (s.Go != null)
            {
                s.Go.SetActive(false);
                _pool.Add(s);
            }
        }

        private static Vector3 Quantise(Vector3 p)
        {
            return new Vector3(Mathf.Round(p.x * 1000f) * 0.001f, Mathf.Round(p.y * 1000f) * 0.001f, Mathf.Round(p.z * 1000f) * 0.001f);
        }

        private static float QuantiseRadius(float r)
        {
            return Mathf.Clamp(Mathf.Round(r * 4000f), 1f, 255f) * 0.00025f;
        }

        /// <summary>An 8-sided tube along the points with parallel-transported rings and cone-rounded ends.</summary>
        private static void Rebuild(Stroke s)
        {
            s.Dirty = false;
            var mesh = s.Mesh;
            mesh.Clear();
            int n = s.Points.Count;
            if (n == 0)
            {
                return;
            }

            Verts.Clear();
            Normals.Clear();
            Tris.Clear();

            var pts = s.Points;
            Vector3 prevT = Vector3.up;
            if (n > 1)
            {
                prevT = (pts[1] - pts[0]).normalized;
                if (prevT.sqrMagnitude < 0.5f)
                {
                    prevT = Vector3.up;
                }
            }

            Vector3 nrm = Vector3.Cross(prevT, Mathf.Abs(prevT.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 firstT = prevT, lastT = prevT;
            int rings = n == 1 ? 2 : n;
            for (int i = 0; i < rings; i++)
            {
                Vector3 p = n == 1 ? pts[0] + prevT * (i == 0 ? -0.001f : 0.001f) : pts[i];
                Vector3 t;
                if (n == 1)
                {
                    t = prevT;
                }
                else
                {
                    t = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                    t = t.sqrMagnitude > 1e-10f ? t.normalized : prevT;
                }

                if (i > 0)
                {
                    nrm = Quaternion.FromToRotation(prevT, t) * nrm;
                    nrm = (nrm - Vector3.Dot(nrm, t) * t).normalized;
                    if (nrm.sqrMagnitude < 0.5f)
                    {
                        nrm = Vector3.Cross(t, Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    }
                }
                else
                {
                    firstT = t;
                }

                lastT = t;
                prevT = t;
                Vector3 bin = Vector3.Cross(t, nrm);
                float r = s.Radii[Mathf.Min(i, n - 1)];
                for (int k = 0; k < Sides; k++)
                {
                    float a = k * (Mathf.PI * 2f / Sides);
                    Vector3 dir = Mathf.Cos(a) * nrm + Mathf.Sin(a) * bin;
                    Verts.Add(p + dir * r);
                    Normals.Add(dir);
                }
            }

            for (int i = 0; i < rings - 1; i++)
            {
                int ring = i * Sides;
                int next = ring + Sides;
                for (int k = 0; k < Sides; k++)
                {
                    int a = ring + k;
                    int b = ring + (k + 1) % Sides;
                    int c = next + k;
                    int d = next + (k + 1) % Sides;
                    Tris.Add(a);
                    Tris.Add(b);
                    Tris.Add(c);
                    Tris.Add(b);
                    Tris.Add(d);
                    Tris.Add(c);
                }
            }

            // rounded-looking ends: a short cone on each end
            Vector3 p0 = n == 1 ? pts[0] - firstT * 0.001f : pts[0];
            Vector3 p1 = n == 1 ? pts[0] + lastT * 0.001f : pts[n - 1];
            float r0 = s.Radii[0];
            float r1 = s.Radii[n - 1];
            int tip0 = Verts.Count;
            Verts.Add(p0 - firstT * r0 * 0.9f);
            Normals.Add(-firstT);
            int tip1 = Verts.Count;
            Verts.Add(p1 + lastT * r1 * 0.9f);
            Normals.Add(lastT);
            int lastRing = (rings - 1) * Sides;
            for (int k = 0; k < Sides; k++)
            {
                int k1 = (k + 1) % Sides;
                Tris.Add(tip0);
                Tris.Add(k1);
                Tris.Add(k);
                Tris.Add(tip1);
                Tris.Add(lastRing + k);
                Tris.Add(lastRing + k1);
            }

            mesh.SetVertices(Verts);
            mesh.SetNormals(Normals);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();
        }

        // ------------------------------------------------------------------ bytes

        public static void Put16(byte[] b, int o, int v)
        {
            b[o] = (byte)(v & 0xFF);
            b[o + 1] = (byte)((v >> 8) & 0xFF);
        }

        public static int Get16(byte[] b, int o)
        {
            return b[o] | (b[o + 1] << 8);
        }

        private static void PutS16(byte[] b, int o, int v)
        {
            v = Mathf.Clamp(v, short.MinValue, short.MaxValue);
            Put16(b, o, v & 0xFFFF);
        }

        private static int GetS16(byte[] b, int o)
        {
            return (short)(b[o] | (b[o + 1] << 8));
        }
    }
}
