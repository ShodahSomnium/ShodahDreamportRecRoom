using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// The Maker Pen's networking, always running on the watch's root (every place has the watch): it keeps one
    /// <see cref="RrMakerPenInk"/> under the current room (<see cref="RrTravel.CurrentRoom"/>; a new room gets a
    /// fresh, empty one, and leaving frees it with the room), routes the pen events to it, and draws the pens other
    /// players hold (a visual copy of <c>(MakerPen)</c> at the pose their pen reports 5 times a second).
    ///
    /// Isolation comes from the Net layer: events reach only the sender's instance, and an unnetworked place (the
    /// Dorm) keeps its ink local. Late joiners: on arriving (and once the place binds) the client asks everyone
    /// for their ink (<see cref="RrMakerPenWire.EvResend"/>, again after 5 s); each author re-sends their own
    /// strokes. An author who is no longer a member of this instance for <see cref="AbsentGrace"/> s has their
    /// strokes and pen removed; a new generation of the instance (the place was reset) clears all ink here.
    /// </summary>
    public sealed class RrMakerPenService : MonoBehaviour
    {
        public const float AbsentGrace = 3f;
        public const float PenTimeout = 2f;
        public const float ResendThrottle = 1.5f;
        public const float ServeThrottle = 2f;

        private sealed class Ghost
        {
            public int Author;
            public RrInstance Pen;
            public bool Loading;
            public bool Held;
            public bool Placed;
            public Vector3 Position;
            public Quaternion Rotation = Quaternion.identity;
            public float LastAt;
        }

        public static RrMakerPenService Instance { get; private set; }

        /// <summary>The ink of the current place (null while travelling).</summary>
        public RrMakerPenInk Ink { get; private set; }

        /// <summary>Counters for the log and the harness.</summary>
        public int ResendsAsked, ResendsServed, AuthorsRemoved;

        private RrRoom _room;
        private int _generation;
        private float _enteredAt;
        private float _askAt = -1f, _askAgainAt = -1f;
        private float _askedAt = -10f, _servedAt = -10f;
        private float _membersAt;
        private readonly Dictionary<int, Ghost> _ghosts = new Dictionary<int, Ghost>();
        private readonly Dictionary<int, float> _absentSince = new Dictionary<int, float>();
        private readonly HashSet<int> _members = new HashSet<int>();
        private readonly List<int> _scratch = new List<int>();

        public int GhostCount
        {
            get { return _ghosts.Count; }
        }

        private void Awake()
        {
            Instance = this;
            RrNet.On(RrMakerPenWire.EvPoints, OnPoints);
            RrNet.On(RrMakerPenWire.EvDelete, OnDelete);
            RrNet.On(RrMakerPenWire.EvResend, OnResend);
            RrNet.On(RrMakerPenWire.EvPen, OnPen);
        }

        private void OnDestroy()
        {
            RrNet.Off(RrMakerPenWire.EvPoints, OnPoints);
            RrNet.Off(RrMakerPenWire.EvDelete, OnDelete);
            RrNet.Off(RrMakerPenWire.EvResend, OnResend);
            RrNet.Off(RrMakerPenWire.EvPen, OnPen);
            ClearGhosts();
            if (Ink != null)
            {
                Destroy(Ink.gameObject);
            }

            Ink = null;
            if (Instance == this)
            {
                Instance = null;
            }

            RrMakerPen.Shutdown();
        }

        private void Update()
        {
            var room = RrTravel.CurrentRoom;
            if (room != _room || (Ink == null && room != null))
            {
                SwitchRoom(room);
            }

            if (Ink == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            int gen = RrNet.InstanceGeneration;
            if (gen != _generation)
            {
                if (_generation != 0 && gen != 0 && now - _enteredAt > AbsentGrace)
                {
                    int n = Ink.StrokeCount;
                    Ink.ClearLocal();
                    ClearGhosts();
                    RrLog.Info("MakerPen", "the place was reset (generation " + _generation + " -> " + gen + "): " + n + " strokes cleared");
                }

                _generation = gen;
                if (gen != 0)
                {
                    _askAt = now + 0.5f;
                    _askAgainAt = now + 5f;
                }
            }

            if (_askAt > 0f && now >= _askAt)
            {
                _askAt = -1f;
                RequestResend();
            }

            if (_askAgainAt > 0f && now >= _askAgainAt)
            {
                _askAgainAt = -1f;
                RequestResend();
            }

            if (now >= _membersAt)
            {
                _membersAt = now + 1f;
                CheckMembers(now);
            }

            UpdateGhosts(now);
        }

        private void SwitchRoom(RrRoom room)
        {
            if (Ink != null)
            {
                Destroy(Ink.gameObject);
            }

            Ink = null;
            ClearGhosts();
            _absentSince.Clear();
            _room = room;
            _askAt = _askAgainAt = -1f;
            if (room == null)
            {
                return;
            }

            var go = new GameObject("Maker Pen Ink");
            go.transform.SetParent(room.transform, false);
            Ink = go.AddComponent<RrMakerPenInk>();
            _enteredAt = Time.unscaledTime;
            _generation = RrNet.InstanceGeneration;
            bool shared = RrNet.Online && RrNet.InstanceObjectIndex >= 0;
            if (RrNet.Online)
            {
                _askAt = _enteredAt + 1.5f;
                _askAgainAt = _enteredAt + 5f;
            }

            RrLog.Info("MakerPen", "ink ready in " + room.name + (shared ? " (shared with this place)" : " (local until the place is shared)"));
        }

        // ------------------------------------------------------------------ local pen

        /// <summary>Publishes the local pen's pose (held) or its disappearance, for the others' copy of it.</summary>
        public void SendPen(bool held, Transform pen)
        {
            if (Ink == null || pen == null || !RrNet.Online || RrNet.InstanceObjectIndex < 0)
            {
                return;
            }

            var local = Ink.transform.InverseTransformPoint(pen.position);
            var rot = Quaternion.Inverse(Ink.transform.rotation) * pen.rotation;
            RrNet.Send(RrMakerPenWire.EvPen, RrMakerPenWire.EncodePen(held, local, rot));
        }

        /// <summary>Asks the other members for their ink (throttled).</summary>
        public void RequestResend()
        {
            float now = Time.unscaledTime;
            if (!RrNet.Online || RrNet.InstanceObjectIndex < 0 || now - _askedAt < ResendThrottle)
            {
                return;
            }

            _askedAt = now;
            ResendsAsked++;
            RrNet.Send(RrMakerPenWire.EvResend, new byte[0]);
        }

        // ------------------------------------------------------------------ events

        private void OnPoints(int sender, byte[] body)
        {
            if (sender == RrNet.LocalId || Ink == null)
            {
                return; // our own loop-back: drawn already
            }

            if (Ink.ApplyRemote(sender, body))
            {
                RequestResend();
            }
        }

        private void OnDelete(int sender, byte[] body)
        {
            if (sender == RrNet.LocalId || Ink == null)
            {
                return;
            }

            Ink.DeleteRemote(sender, body);
        }

        private void OnResend(int sender, byte[] body)
        {
            if (sender == RrNet.LocalId || Ink == null)
            {
                return;
            }

            var tool = RrMakerPen.Tool;
            if (tool != null)
            {
                tool.PoseSoon();
            }

            float now = Time.unscaledTime;
            if (Ink.MyStrokes == 0 || now - _servedAt < ServeThrottle)
            {
                return;
            }

            _servedAt = now;
            ResendsServed++;
            Ink.ResendMine();
            RrLog.Info("MakerPen", "re-sending my " + Ink.MyStrokes + " strokes for player " + sender);
        }

        private void OnPen(int sender, byte[] body)
        {
            if (sender == RrNet.LocalId || Ink == null)
            {
                return;
            }

            bool held;
            Vector3 local;
            Quaternion rot;
            if (!RrMakerPenWire.DecodePen(body, out held, out local, out rot))
            {
                return;
            }

            Ghost g;
            if (!_ghosts.TryGetValue(sender, out g))
            {
                if (!held)
                {
                    return;
                }

                g = new Ghost { Author = sender };
                _ghosts[sender] = g;
            }

            g.Held = held;
            g.Position = local;
            g.Rotation = rot;
            g.LastAt = Time.unscaledTime;
            if (held && g.Pen == null && !g.Loading)
            {
                SpawnGhost(g);
            }
        }

        // ------------------------------------------------------------------ members

        private void CheckMembers(float now)
        {
            _members.Clear();
            foreach (int id in RrNet.MembersOfMyInstance())
            {
                _members.Add(id);
            }

            _scratch.Clear();
            Ink.RemoteAuthors(_scratch);
            foreach (var kv in _ghosts)
            {
                if (!_scratch.Contains(kv.Key))
                {
                    _scratch.Add(kv.Key);
                }
            }

            foreach (int a in _scratch)
            {
                if (_members.Contains(a))
                {
                    _absentSince.Remove(a);
                    continue;
                }

                float since;
                if (!_absentSince.TryGetValue(a, out since))
                {
                    _absentSince[a] = now;
                    continue;
                }

                if (now - since < AbsentGrace)
                {
                    continue;
                }

                _absentSince.Remove(a);
                int n = Ink.RemoveAuthor(a);
                RemoveGhost(a);
                AuthorsRemoved++;
                if (n > 0)
                {
                    RrLog.Info("MakerPen", "player " + a + " left this place: " + n + " of their strokes removed");
                }
            }
        }

        // ------------------------------------------------------------------ other players' pens

        private void SpawnGhost(Ghost g)
        {
            g.Loading = true;
            RrSpawn.Spawn(RrMakerPen.Prefab, Ink.transform, g.Position, g.Rotation, inst =>
            {
                g.Loading = false;
                if (inst == null)
                {
                    return;
                }

                Ghost current;
                if (Ink == null || !_ghosts.TryGetValue(g.Author, out current) || current != g)
                {
                    Destroy(inst.gameObject);
                    return;
                }

                RrMakerPen.StripExtras(inst);
                inst.name = "Maker Pen of player " + g.Author;
                g.Pen = inst;
                g.Placed = false;
            }, RrSpawnOptions.Visual());
        }

        private void UpdateGhosts(float now)
        {
            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            foreach (var kv in _ghosts)
            {
                var g = kv.Value;
                if (g.Pen == null)
                {
                    continue;
                }

                bool show = g.Held && now - g.LastAt < PenTimeout;
                if (g.Pen.gameObject.activeSelf != show)
                {
                    g.Pen.gameObject.SetActive(show);
                }

                if (!show)
                {
                    g.Placed = false;
                    continue;
                }

                var t = g.Pen.transform;
                if (!g.Placed)
                {
                    t.localPosition = g.Position;
                    t.localRotation = g.Rotation;
                    g.Placed = true;
                }
                else
                {
                    t.localPosition = Vector3.Lerp(t.localPosition, g.Position, k);
                    t.localRotation = Quaternion.Slerp(t.localRotation, g.Rotation, k);
                }
            }
        }

        private void RemoveGhost(int author)
        {
            Ghost g;
            if (!_ghosts.TryGetValue(author, out g))
            {
                return;
            }

            if (g.Pen != null)
            {
                Destroy(g.Pen.gameObject);
            }

            _ghosts.Remove(author);
        }

        private void ClearGhosts()
        {
            foreach (var kv in _ghosts)
            {
                if (kv.Value.Pen != null)
                {
                    Destroy(kv.Value.Pen.gameObject);
                }
            }

            _ghosts.Clear();
        }

        /// <summary>One line for the log and the harness.</summary>
        public string Report()
        {
            if (Ink == null)
            {
                return "no ink (travelling)";
            }

            return "strokes " + Ink.StrokeCount + " (mine " + Ink.MyStrokes + ", " + Ink.MyPoints + " points; others " + Ink.RemoteStrokes +
                   " from " + Ink.RemoteAuthorCount + "), pens shown " + _ghosts.Count + ", events sent " + Ink.SentEvents + " received " +
                   Ink.ReceivedEvents + ", resends asked " + ResendsAsked + " served " + ResendsServed;
        }
    }
}
