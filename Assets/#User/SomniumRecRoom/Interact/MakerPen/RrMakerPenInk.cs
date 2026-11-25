using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// The Maker Pen ink of one place: every player's tubes, under the room (so leaving the room frees them).
    ///
    /// Strokes are owned by their author. The local player's strokes ("mine") are drawn at once and streamed as
    /// <see cref="RrMakerPenWire.EvPoints"/> segments (flushed every <see cref="FlushEvery"/> s, the stroke being
    /// drawn first); undo and erase remove whole strokes and send <see cref="RrMakerPenWire.EvDelete"/>. Other
    /// players' strokes are kept per author (PlayerId) and only change on that author's events. Caps per author:
    /// <see cref="MaxStrokesPerAuthor"/> strokes and <see cref="MaxPointsPerAuthor"/> points, enforced by the
    /// author and again by every receiver. A stroke longer than <see cref="MaxPointsPerStroke"/> points continues
    /// as a new piece of the same group (one undo removes the whole group).
    ///
    /// Tubes use <see cref="RrLook.Solid"/> (Rec Room's maker-pen material <c>AG-MakerPenShape</c>, per colour).
    /// </summary>
    public sealed class RrMakerPenInk : MonoBehaviour
    {
        public const int MaxPointsPerStroke = 240;
        public const int MaxStrokesPerAuthor = 150;
        public const int MaxPointsPerAuthor = 6000;
        public const float MinStep = 0.008f;

        /// <summary>A jump longer than this (a teleport while drawing) starts a new stroke instead of a long spike.</summary>
        public const float MaxStep = 1f;

        public const float FlushEvery = 0.1f;
        public const int EventsPerFlush = 3;
        private const int PoolLimit = 64;

        private sealed class Stroke
        {
            public int Id;
            public int Group;
            public byte Colour;
            public readonly List<Vector3> Points = new List<Vector3>();
            public readonly List<float> Radii = new List<float>();
            public GameObject Go;
            public Mesh Mesh;
            public bool Dirty;
            public int Sent;
            public Vector3 Min, Max;
            public float MaxRadius;
        }

        private sealed class Author
        {
            public int Id;
            public readonly List<Stroke> Strokes = new List<Stroke>();
            public readonly Dictionary<int, Stroke> ById = new Dictionary<int, Stroke>();
            public int Points;
        }

        private readonly Author _mine = new Author { Id = -1 };
        private readonly Dictionary<int, Author> _remote = new Dictionary<int, Author>();
        private readonly List<Stroke> _all = new List<Stroke>();
        private readonly List<Stroke> _pool = new List<Stroke>();
        private readonly List<int> _ids = new List<int>();
        private Stroke _active;
        private int _nextId = 1;
        private float _flushAt;

        /// <summary>Counters for the log and the harness.</summary>
        public int SentEvents, ReceivedEvents;

        public bool Drawing
        {
            get { return _active != null; }
        }

        public int StrokeCount
        {
            get { return _all.Count; }
        }

        public int MyStrokes
        {
            get { return _mine.Strokes.Count; }
        }

        public int MyPoints
        {
            get { return _mine.Points; }
        }

        public int RemoteStrokes
        {
            get { return _all.Count - _mine.Strokes.Count; }
        }

        public int RemoteAuthorCount
        {
            get { return _remote.Count; }
        }

        private void OnDestroy()
        {
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

        // ------------------------------------------------------------------ my strokes

        /// <summary>Starts one of my strokes at a world point; false when my ink is full.</summary>
        public bool BeginStroke(byte colour, Vector3 world, float radius)
        {
            EndStroke();
            if (_mine.Strokes.Count >= MaxStrokesPerAuthor || _mine.Points >= MaxPointsPerAuthor)
            {
                RrLog.WarnOnce("MakerPen", "ink-full", "your Maker Pen ink is full (" + MaxStrokesPerAuthor + " strokes / " + MaxPointsPerAuthor + " points): undo or erase to draw more");
                return false;
            }

            _active = NewStroke(_mine, NextId(), colour);
            _active.Group = _active.Id;
            Add(_active, _mine, RrMakerPenWire.Quantise(transform.InverseTransformPoint(world)), RrMakerPenWire.QuantiseRadius(radius));
            return true;
        }

        /// <summary>Extends my current stroke when the point is at least <see cref="MinStep"/> from the last one.</summary>
        public void AddPoint(Vector3 world, float radius)
        {
            if (_active == null)
            {
                return;
            }

            var local = RrMakerPenWire.Quantise(transform.InverseTransformPoint(world));
            var last = _active.Points[_active.Points.Count - 1];
            float d2 = (local - last).sqrMagnitude;
            if (d2 < MinStep * MinStep)
            {
                return;
            }

            if (d2 > MaxStep * MaxStep)
            {
                byte c = _active.Colour;
                BeginStroke(c, world, radius);
                return;
            }

            if (_mine.Points >= MaxPointsPerAuthor)
            {
                EndStroke();
                return;
            }

            if (_active.Points.Count >= MaxPointsPerStroke)
            {
                if (_mine.Strokes.Count >= MaxStrokesPerAuthor)
                {
                    EndStroke();
                    return;
                }

                // continue as a new piece from the last point so meshes and events stay small
                int group = _active.Group;
                byte colour = _active.Colour;
                float lastRadius = _active.Radii[_active.Radii.Count - 1];
                _active = NewStroke(_mine, NextId(), colour);
                _active.Group = group;
                Add(_active, _mine, last, lastRadius);
            }

            Add(_active, _mine, local, RrMakerPenWire.QuantiseRadius(radius));
        }

        public void EndStroke()
        {
            _active = null;
        }

        /// <summary>Removes my last stroke (sent to everyone); false when there is none.</summary>
        public bool UndoMine()
        {
            EndStroke();
            if (_mine.Strokes.Count == 0)
            {
                return false;
            }

            return DeleteMine(_mine.Strokes[_mine.Strokes.Count - 1].Group);
        }

        /// <summary>Removes one of my strokes (a group id from <see cref="FindMineNear"/> / <see cref="FindMineAlong"/>).</summary>
        public bool DeleteMine(int group)
        {
            if (group <= 0)
            {
                return false;
            }

            _ids.Clear();
            for (int i = _mine.Strokes.Count - 1; i >= 0; i--)
            {
                var s = _mine.Strokes[i];
                if (s.Group == group)
                {
                    _ids.Add(s.Id);
                    Remove(_mine, s);
                }
            }

            if (_ids.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _ids.Count; i += RrMakerPenWire.MaxIdsPerDelete)
            {
                var part = _ids.GetRange(i, Mathf.Min(RrMakerPenWire.MaxIdsPerDelete, _ids.Count - i));
                Send(RrMakerPenWire.EvDelete, RrMakerPenWire.EncodeDelete(part));
            }

            return true;
        }

        /// <summary>Removes all my strokes (sent to everyone); returns how many there were.</summary>
        public int ClearMine()
        {
            EndStroke();
            int n = _mine.Strokes.Count;
            if (n == 0)
            {
                return 0;
            }

            for (int i = n - 1; i >= 0; i--)
            {
                Remove(_mine, _mine.Strokes[i]);
            }

            Send(RrMakerPenWire.EvDelete, new byte[] { 0 });
            return n;
        }

        /// <summary>The group of my stroke whose tube is within <paramref name="tolerance"/> m of a world point (0 none).</summary>
        public int FindMineNear(Vector3 world, float tolerance)
        {
            var p = transform.InverseTransformPoint(world);
            for (int i = _mine.Strokes.Count - 1; i >= 0; i--)
            {
                var s = _mine.Strokes[i];
                float reach = s.MaxRadius + tolerance;
                if (p.x < s.Min.x - reach || p.y < s.Min.y - reach || p.z < s.Min.z - reach ||
                    p.x > s.Max.x + reach || p.y > s.Max.y + reach || p.z > s.Max.z + reach)
                {
                    continue;
                }

                var pts = s.Points;
                if (pts.Count == 1)
                {
                    float r = s.Radii[0] + tolerance;
                    if ((pts[0] - p).sqrMagnitude <= r * r)
                    {
                        return s.Group;
                    }

                    continue;
                }

                for (int j = 0; j + 1 < pts.Count; j++)
                {
                    float r = Mathf.Max(s.Radii[j], s.Radii[j + 1]) + tolerance;
                    if (RrMakerPenTube.SqrDistanceToSegment(p, pts[j], pts[j + 1]) <= r * r)
                    {
                        return s.Group;
                    }
                }
            }

            return 0;
        }

        /// <summary>The group of my nearest stroke the world ray passes within 3 cm of, up to <paramref name="reach"/> m (0 none).</summary>
        public int FindMineAlong(Ray ray, float reach)
        {
            var o = transform.InverseTransformPoint(ray.origin);
            var d = transform.InverseTransformDirection(ray.direction).normalized;
            var local = new Ray(o, d);
            const float slack = 0.03f;
            float best = reach;
            int found = 0;
            for (int i = _mine.Strokes.Count - 1; i >= 0; i--)
            {
                var s = _mine.Strokes[i];
                float grow = s.MaxRadius + slack;
                var box = new Bounds((s.Min + s.Max) * 0.5f, s.Max - s.Min + Vector3.one * (2f * grow));
                float enter;
                if (!box.IntersectRay(local, out enter) || enter > best)
                {
                    continue;
                }

                var pts = s.Points;
                int segments = Mathf.Max(1, pts.Count - 1);
                for (int j = 0; j < segments; j++)
                {
                    var b = pts[Mathf.Min(j + 1, pts.Count - 1)];
                    float along;
                    float d2 = RrMakerPenTube.SqrDistanceSegmentRay(pts[j], b, o, d, best, out along);
                    float r = s.Radii[j] + slack;
                    if (d2 <= r * r && along < best)
                    {
                        best = along;
                        found = s.Group;
                    }
                }
            }

            return found;
        }

        /// <summary>A late joiner asked: my strokes are sent again from the start, a few events per flush.</summary>
        public void ResendMine()
        {
            foreach (var s in _mine.Strokes)
            {
                s.Sent = 0;
            }
        }

        // ------------------------------------------------------------------ other players' strokes

        /// <summary>Applies an author's points event; true when a gap was seen (something was lost: ask again).</summary>
        public bool ApplyRemote(int author, byte[] body)
        {
            int id, start, n;
            byte colour;
            if (!RrMakerPenWire.DecodePoints(body, out id, out colour, out start, out n))
            {
                return false;
            }

            ReceivedEvents++;
            var a = RemoteAuthor(author);
            Stroke s;
            if (!a.ById.TryGetValue(id, out s))
            {
                if (a.Strokes.Count >= MaxStrokesPerAuthor)
                {
                    RrLog.WarnOnce("MakerPen", "remote-full-" + author, "player " + author + " reached the Maker Pen stroke cap: later strokes ignored");
                    return false;
                }

                s = NewStroke(a, id, colour);
            }

            bool gap = start > s.Points.Count;
            for (int i = 0; i < n; i++)
            {
                int at = start + i;
                var p = RrMakerPenWire.PointAt(body, i);
                float r = RrMakerPenWire.RadiusAt(body, i);
                if (at < s.Points.Count)
                {
                    s.Points[at] = p;
                    s.Radii[at] = r;
                    s.Dirty = true;
                }
                else if (at == s.Points.Count && at < MaxPointsPerStroke && a.Points < MaxPointsPerAuthor)
                {
                    Add(s, a, p, r);
                }
            }

            return gap;
        }

        /// <summary>Applies an author's delete event (listed strokes, or all of theirs).</summary>
        public void DeleteRemote(int author, byte[] body)
        {
            Author a;
            if (body == null || body.Length < 1 || !_remote.TryGetValue(author, out a))
            {
                return;
            }

            ReceivedEvents++;
            int n = body[0];
            if (n == 0)
            {
                RemoveAuthor(author);
                return;
            }

            for (int i = 0; i < n && 2 + 2 * i < body.Length; i++)
            {
                Stroke s;
                if (a.ById.TryGetValue(RrMakerPenWire.Get16(body, 1 + 2 * i), out s))
                {
                    Remove(a, s);
                }
            }
        }

        /// <summary>Removes every stroke of one author here (they left the place).</summary>
        public int RemoveAuthor(int author)
        {
            Author a;
            if (!_remote.TryGetValue(author, out a))
            {
                return 0;
            }

            int n = a.Strokes.Count;
            for (int i = n - 1; i >= 0; i--)
            {
                Remove(a, a.Strokes[i]);
            }

            _remote.Remove(author);
            return n;
        }

        /// <summary>Adds the PlayerIds that have strokes here.</summary>
        public void RemoteAuthors(List<int> into)
        {
            foreach (var kv in _remote)
            {
                into.Add(kv.Key);
            }
        }

        /// <summary>Removes every stroke here, mine included, without telling anyone (the place was reset or left).</summary>
        public void ClearLocal()
        {
            _active = null;
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var s = _all[i];
                s.Points.Clear();
                s.Radii.Clear();
                Park(s);
            }

            _all.Clear();
            _mine.Strokes.Clear();
            _mine.ById.Clear();
            _mine.Points = 0;
            _remote.Clear();
        }

        // ------------------------------------------------------------------ frame

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _flushAt)
            {
                _flushAt = Time.unscaledTime + FlushEvery;

                // Unbound (offline, the Dorm): nothing to send; the strokes go out if the place binds later.
                if (RrNet.Online && RrNet.InstanceObjectIndex >= 0)
                {
                    Flush(EventsPerFlush);
                }
            }

            for (int i = 0; i < _all.Count; i++)
            {
                var s = _all[i];
                if (s.Dirty)
                {
                    s.Dirty = false;
                    RrMakerPenTube.Build(s.Mesh, s.Points, s.Radii);
                }
            }
        }

        private void Flush(int maxEvents)
        {
            int events = _active != null ? FlushStroke(_active, 1) : 0;
            for (int i = 0; i < _mine.Strokes.Count && events < maxEvents; i++)
            {
                events += FlushStroke(_mine.Strokes[i], maxEvents - events);
            }
        }

        private int FlushStroke(Stroke s, int budget)
        {
            int events = 0;
            while (s.Sent < s.Points.Count && events < budget)
            {
                int n = Mathf.Min(RrMakerPenWire.MaxPointsPerEvent, s.Points.Count - s.Sent);
                Send(RrMakerPenWire.EvPoints, RrMakerPenWire.EncodePoints(s.Id, s.Colour, s.Sent, s.Points, s.Radii, n));
                s.Sent += n;
                events++;
            }

            return events;
        }

        private void Send(byte type, byte[] body)
        {
            SentEvents++;
            RrNet.Send(type, body);
        }

        // ------------------------------------------------------------------ strokes

        private int NextId()
        {
            for (int tries = 0; tries < 400; tries++)
            {
                int id = _nextId;
                _nextId = _nextId >= 0xFFFF ? 1 : _nextId + 1;
                if (!_mine.ById.ContainsKey(id))
                {
                    return id;
                }
            }

            return _nextId;
        }

        private Author RemoteAuthor(int id)
        {
            Author a;
            if (!_remote.TryGetValue(id, out a))
            {
                a = new Author { Id = id };
                _remote[id] = a;
            }

            return a;
        }

        private Stroke NewStroke(Author author, int id, byte colour)
        {
            Stroke s;
            if (_pool.Count > 0)
            {
                s = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                s = new Stroke();
                s.Go = new GameObject("Stroke");
                s.Go.transform.SetParent(transform, false);
                s.Mesh = new Mesh { name = "Maker Pen Ink" };
                s.Mesh.MarkDynamic();
                s.Go.AddComponent<MeshFilter>().sharedMesh = s.Mesh;
                var mr = s.Go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
            }

            s.Points.Clear();
            s.Radii.Clear();
            s.Id = id;
            s.Group = id;
            s.Colour = colour;
            s.Sent = 0;
            s.Dirty = true;
            s.MaxRadius = 0f;
            s.Go.name = author.Id < 0 ? "Stroke " + id : "Stroke " + author.Id + "." + id;
            s.Go.GetComponent<MeshRenderer>().sharedMaterial = RrLook.Solid(RrMakerPenWire.ColourOf(colour));
            s.Go.SetActive(true);
            author.Strokes.Add(s);
            author.ById[id] = s;
            _all.Add(s);
            return s;
        }

        private static void Add(Stroke s, Author author, Vector3 local, float radius)
        {
            if (s.Points.Count == 0)
            {
                s.Min = s.Max = local;
            }
            else
            {
                s.Min = Vector3.Min(s.Min, local);
                s.Max = Vector3.Max(s.Max, local);
            }

            s.Points.Add(local);
            s.Radii.Add(radius);
            s.MaxRadius = Mathf.Max(s.MaxRadius, radius);
            s.Dirty = true;
            author.Points++;
        }

        private void Remove(Author author, Stroke s)
        {
            if (_active == s)
            {
                _active = null;
            }

            author.Points = Mathf.Max(0, author.Points - s.Points.Count);
            author.Strokes.Remove(s);
            author.ById.Remove(s.Id);
            _all.Remove(s);
            s.Points.Clear();
            s.Radii.Clear();
            Park(s);
        }

        private void Park(Stroke s)
        {
            if (s.Mesh != null)
            {
                s.Mesh.Clear();
            }

            if (s.Go == null)
            {
                return;
            }

            if (_pool.Count < PoolLimit)
            {
                s.Go.SetActive(false);
                _pool.Add(s);
                return;
            }

            if (s.Mesh != null)
            {
                Destroy(s.Mesh);
            }

            Destroy(s.Go);
        }
    }
}
