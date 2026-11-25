using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Our own head and torso volumes (layer 27 triggers) for every player in the local player's instance: remote
    /// players from their Bridge body transforms (<see cref="RrRemotePlayers"/>), the local player from the head
    /// pose and feet, plus practice targets games add (<see cref="AddTarget"/>). Shots and swings test these, never
    /// the avatar, which is never touched (no component on it, nothing parented to it).
    ///
    /// Shapes (somnium_player.md §8.3, rr_shooters.md §11): head sphere r 0.14 m on Body.Head; torso capsule r
    /// 0.22 m standing under the head (its x/z follow the head, not Body.Root, because a VR player's root can lag
    /// their room-scale position), from 0.45 m above the feet up to 0.18 m below the head - Rec Room counts only
    /// head and torso hits. Remote desktop players have a fixed Head under the root; a missing Head is taken 1.6 m
    /// above the root. Volumes exist only for members of the local player's own instance (isolation rule 5.10.3),
    /// refreshed twice a second; they move every frame in Update, before shots are stepped.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RrHitVolumes : MonoBehaviour
    {
        public const float HeadRadius = 0.14f;
        public const float BodyRadius = 0.22f;
        public const float BodyBottom = 0.45f;
        public const float NeckDrop = 0.18f;

        public static RrHitVolumes Instance { get; private set; }

        /// <summary>Show the volumes as see-through shapes (tests, tuning).</summary>
        public static bool ShowDebug;

        /// <summary>Include the local player's own volumes (enemy attacks and other players' visual shots stop on them).</summary>
        public bool IncludeLocal = true;

        private sealed class Entry
        {
            public int Id;
            public bool Local;
            public Transform Root;
            public Transform Head;
            public Transform Holder;
            public RrHitVolume HeadVolume;
            public RrHitVolume BodyVolume;
            public int Seen;
        }

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<int> _drop = new List<int>();
        private readonly HashSet<int> _members = new HashSet<int>();
        private readonly Dictionary<int, KeyValuePair<Transform, Transform>> _targets = new Dictionary<int, KeyValuePair<Transform, Transform>>();
        private float _membersAt;
        private int _stamp;
        private int _logged = -1;
        private int _nextTargetId = RrCombat.FirstTargetId;

        public int Count
        {
            get { return _entries.Count; }
        }

        /// <summary>The system, created under the current room (so it goes with it) or the world root.</summary>
        public static RrHitVolumes Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            RrCombat.Ensure();
            var room = RrTravel.CurrentRoom;
            Transform parent = room != null ? room.transform : RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            var go = new GameObject("Rr Hit Volumes");
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            return go.AddComponent<RrHitVolumes>();
        }

        /// <summary>Which player and body part a collider belongs to (false for anything that is not a player volume).</summary>
        public static bool Resolve(Collider c, out int playerId, out RrBodyPart part)
        {
            playerId = RrCombat.NoPlayer;
            part = RrBodyPart.None;
            RrHitVolume v;
            if (!RrHitVolume.TryGet(c, out v) || !v.IsPlayer)
            {
                return false;
            }

            playerId = v.PlayerId;
            part = v.Part;
            return true;
        }

        /// <summary>
        /// A practice target or bot that shots should treat as a player: volumes follow root (feet) and head.
        /// Returns its id (from <see cref="RrCombat.FirstTargetId"/>; no team, so everyone can hit it).
        /// </summary>
        public static int AddTarget(Transform root, Transform head)
        {
            var v = Ensure();
            int id = v._nextTargetId++;
            v._targets[id] = new KeyValuePair<Transform, Transform>(root, head);
            return id;
        }

        public static void RemoveTarget(int id)
        {
            if (Instance != null)
            {
                Instance._targets.Remove(id);
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            _stamp++;
            if (Time.unscaledTime >= _membersAt)
            {
                _membersAt = Time.unscaledTime + 0.5f;
                _members.Clear();
                try
                {
                    foreach (int id in RrNet.MembersOfMyInstance())
                    {
                        _members.Add(id);
                    }
                }
                catch (System.Exception e)
                {
                    RrLog.WarnOnce("HitVolumes", "members", "instance members unavailable: " + e.Message);
                }
            }

            int local = RrNet.LocalId;
            if (IncludeLocal)
            {
                var head = RrPlayer.HeadPosition;
                var feet = RrPlayer.FeetPosition;
                if (head != Vector3.zero || feet != Vector3.zero)
                {
                    var e = Get(local, true);
                    Place(e, feet, head);
                }
            }

            var remotes = RrRemotePlayers.All;
            if (remotes != null)
            {
                for (int i = 0; i < remotes.Count; i++)
                {
                    var r = remotes[i];
                    if (r == null || r.Id == local || r.Root == null || !_members.Contains(r.Id))
                    {
                        continue;
                    }

                    var e = Get(r.Id, false);
                    var headPos = r.Head != null ? r.Head.position : r.Root.position + Vector3.up * 1.6f;
                    Place(e, r.Root.position, headPos);
                }
            }

            foreach (var kv in _targets)
            {
                var root = kv.Value.Key;
                if (root == null)
                {
                    continue;
                }

                var head = kv.Value.Value;
                var e = Get(kv.Key, false);
                Place(e, root.position, head != null ? head.position : root.position + Vector3.up * 1.6f);
            }

            _drop.Clear();
            foreach (var kv in _entries)
            {
                if (kv.Value.Seen != _stamp)
                {
                    _drop.Add(kv.Key);
                }
            }

            foreach (int id in _drop)
            {
                var e = _entries[id];
                if (e.Holder != null)
                {
                    Destroy(e.Holder.gameObject);
                }

                _entries.Remove(id);
            }

            if (_entries.Count != _logged)
            {
                _logged = _entries.Count;
                RrLog.Info("HitVolumes", _entries.Count + " player volume set(s) (" + _members.Count + " member(s) in this instance, " + _targets.Count + " target(s))");
            }
        }

        private Entry Get(int id, bool local)
        {
            Entry e;
            if (_entries.TryGetValue(id, out e) && e.Holder != null)
            {
                e.Seen = _stamp;
                return e;
            }

            var holder = new GameObject(local ? "Rr Hit Local" : "Rr Hit " + id);
            holder.layer = RrLayers.HitVolume;
            holder.transform.SetParent(transform, false);
            var rb = holder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            e = new Entry { Id = id, Local = local, Holder = holder.transform, Seen = _stamp };
            e.HeadVolume = RrHitVolume.Create(holder.transform, "Head", RrBodyPart.Head, false, HeadRadius, 0f);
            e.BodyVolume = RrHitVolume.Create(holder.transform, "Body", RrBodyPart.Body, true, BodyRadius, 1f);
            foreach (var v in new[] { e.HeadVolume, e.BodyVolume })
            {
                v.PlayerId = id;
                v.IsLocalPlayer = local;
                if (ShowDebug)
                {
                    AddDebugShape(v);
                }
            }

            _entries[id] = e;
            return e;
        }

        private static void Place(Entry e, Vector3 feet, Vector3 head)
        {
            e.HeadVolume.transform.position = head;
            float bottom = feet.y + BodyBottom;
            float top = head.y - NeckDrop;
            if (top < bottom + 0.1f)
            {
                bottom = top - 0.1f;
            }

            var body = e.BodyVolume;
            var capsule = (CapsuleCollider)body.Collider;
            float h = top - bottom;
            body.transform.position = new Vector3(head.x, bottom + h * 0.5f, head.z);
            capsule.height = Mathf.Max(h + BodyRadius * 2f, BodyRadius * 2f);
        }

        private static void AddDebugShape(RrHitVolume v)
        {
            bool capsule = v.Collider is CapsuleCollider;
            var shape = GameObject.CreatePrimitive(capsule ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            Destroy(shape.GetComponent<Collider>());
            shape.name = "Debug";
            shape.layer = RrLayers.Default;
            shape.transform.SetParent(v.transform, false);
            float r = capsule ? ((CapsuleCollider)v.Collider).radius : ((SphereCollider)v.Collider).radius;
            shape.transform.localScale = capsule ? new Vector3(r * 2f, 0.5f, r * 2f) : Vector3.one * r * 2f;
            shape.GetComponent<MeshRenderer>().sharedMaterial = RrLook.Glass(v.Part == RrBodyPart.Head ? new Color(1f, 0.3f, 0.3f, 0.35f) : new Color(0.3f, 0.6f, 1f, 0.3f));
        }
    }
}
