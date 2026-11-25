using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// The authority's wave runner for the current act (rr_quests.md 3.3): checks the next wave's trigger, queues its
    /// entries (scaled to the party size at the start of the run), and pops them one by one at the wave's interval
    /// while fewer than <see cref="RrQuestDef.MaxAlive"/> enemies live (the room adds each spawned enemy's points to
    /// the run's max-possible score for the rating once its config is read). Spawn points rotate through the group
    /// with a little jitter; flyers appear 2.5 m above their point. The next wave index lives in the instance ints,
    /// so a new authority carries on with the next wave (a half-spawned wave's rest is lost on a hand-over).
    /// </summary>
    public sealed class RrQuestDirector
    {
        private struct Pending
        {
            public int Enemy;
            public string Group;
        }

        private readonly RrQuestRoom _room;
        private readonly Queue<Pending> _queue = new Queue<Pending>();
        private readonly Dictionary<string, int> _cursor = new Dictionary<string, int>();
        private float _nextSpawn;
        private float _interval = 0.45f;
        private float _waveAt;
        private float _clearedAt = -1f;
        private System.Random _rng = new System.Random(1);

        public RrQuestDirector(RrQuestRoom room)
        {
            _room = room;
        }

        public bool Idle
        {
            get { return _queue.Count == 0; }
        }

        /// <summary>A new act: forget the old queue.</summary>
        public void Reset(int seed)
        {
            _queue.Clear();
            _cursor.Clear();
            _rng = new System.Random(seed);
            _waveAt = Time.time;
        }

        /// <summary>Every wave triggered and fully spawned.</summary>
        public bool AllSpawned(RrQuestAct act)
        {
            return act == null || (_room.QuestInt(RrQuestInts.Wave) >= act.Waves.Count && _queue.Count == 0);
        }

        public void Step(RrQuestAct act)
        {
            if (act == null)
            {
                return;
            }

            int w = _room.QuestInt(RrQuestInts.Wave);
            if (w < act.Waves.Count && Triggered(act, act.Waves[w], w))
            {
                StartWave(act, act.Waves[w]);
                _room.SetQuestInt(RrQuestInts.Wave, w + 1);
            }

            SideWaves(act);

            if (_queue.Count > 0 && Time.time >= _nextSpawn && _room.Enemies.Alive < _room.Def.MaxAlive)
            {
                var p = _queue.Dequeue();
                SpawnOne(act, p);
                _nextSpawn = Time.time + _interval;
            }
        }

        private bool Triggered(RrQuestAct act, RrQuestWave wave, int index)
        {
            int alive = _room.Enemies.Alive;
            bool prevCleared = _queue.Count == 0 && alive == 0;
            if (!prevCleared)
            {
                _clearedAt = -1f;
            }
            else if (_clearedAt < 0f)
            {
                _clearedAt = Time.time;
            }

            // Zone and timer waves after the first also start once the team has idled 25 s with nothing alive (no soft-lock).
            bool stale = index > 0 && prevCleared && Time.time - _clearedAt > 25f;
            switch (wave.Trigger)
            {
                case RrQuestWave.When.ActStart:
                    return true;
                case RrQuestWave.When.PrevCleared:
                    return prevCleared;
                case RrQuestWave.When.PrevBelow:
                    return _queue.Count == 0 && alive <= Mathf.RoundToInt(wave.Param);
                case RrQuestWave.When.After:
                    return (index > 0 && prevCleared) || (_queue.Count == 0 && Time.time - _waveAt >= wave.Param);
                case RrQuestWave.When.Enter:
                    return stale || AnyPlayerIn(act, wave.Zone);
            }

            return false;
        }

        /// <summary>
        /// Optional side waves: each starts once when a living player enters its zone (the started bits live in
        /// <see cref="RrQuestInts.SideWaves"/>, so a new authority does not start one twice). Their enemies share
        /// the spawn queue; the act waits only for what they spawned to die.
        /// </summary>
        private void SideWaves(RrQuestAct act)
        {
            int bits = _room.QuestInt(RrQuestInts.SideWaves);
            for (int i = 0; i < act.SideWaves.Count && i < 16; i++)
            {
                if ((bits & (1 << i)) != 0 || !AnyPlayerIn(act, act.SideWaves[i].Zone))
                {
                    continue;
                }

                bits |= 1 << i;
                _room.SetQuestInt(RrQuestInts.SideWaves, bits);
                StartWave(act, act.SideWaves[i]);
                RrLog.Info("Quest", "act " + (_room.ActIndex + 1) + " side wave " + (i + 1) + " triggered (optional: paid only if killed)");
            }
        }

        private static bool AnyPlayerIn(RrQuestAct act, Bounds zone)
        {
            if (act.Root == null)
            {
                return false;
            }

            var h = RrHealth.Local;
            if ((h == null || !h.IsOut) && Inside(act, zone, RrPlayer.FeetPosition))
            {
                return true;
            }

            var all = RrRemotePlayers.All;
            if (all != null)
            {
                foreach (var r in all)
                {
                    // Every room shares the world origin: only players in this instance can stand in its zones.
                    if (r != null && r.Root != null && r.WithMe && Inside(act, zone, r.Root.position))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Inside(RrQuestAct act, Bounds zone, Vector3 world)
        {
            Vector3 p = act.Root.InverseTransformPoint(world);
            return p.x >= zone.min.x && p.x <= zone.max.x && p.z >= zone.min.z && p.z <= zone.max.z && p.y >= zone.min.y - 1f && p.y <= zone.max.y + 2f;
        }

        private void StartWave(RrQuestAct act, RrQuestWave wave)
        {
            var def = _room.Def;
            int party = Mathf.Clamp(_room.QuestInt(RrQuestInts.Party), 1, def.MaxPlayers > 0 ? def.MaxPlayers : 4);
            _interval = Mathf.Max(0.1f, wave.Interval * _room.WaveIntervalScale);
            _waveAt = Time.time;
            int total = 0;
            // Interleave the entries (goblin, imp, goblin, imp...) the way Rec Room's waves pop in.
            var left = new List<int>();
            for (int i = 0; i < wave.Entries.Count; i++)
            {
                left.Add(def.Scaled(wave.Entries[i].Count, party));
            }

            bool any = true;
            while (any)
            {
                any = false;
                for (int i = 0; i < wave.Entries.Count; i++)
                {
                    if (left[i] > 0)
                    {
                        left[i]--;
                        any = true;
                        total++;
                        _queue.Enqueue(new Pending { Enemy = wave.Entries[i].Enemy, Group = wave.Entries[i].Group });
                    }
                }
            }

            if (!string.IsNullOrEmpty(wave.Toast))
            {
                _room.Broadcast(wave.Toast);
            }

            RrLog.Info("Quest", "act " + (_room.ActIndex + 1) + (wave.Side ? " side wave " + (act.SideWaves.IndexOf(wave) + 1) : " wave " + (act.Waves.IndexOf(wave) + 1))
                + ": " + total + " enemies (party " + party + ")");
        }

        private void SpawnOne(RrQuestAct act, Pending p)
        {
            List<Vector3> points;
            Vector3 local;
            if (p.Group != null && act.Groups.TryGetValue(p.Group, out points) && points.Count > 0)
            {
                int c;
                _cursor.TryGetValue(p.Group, out c);
                local = points[c % points.Count];
                _cursor[p.Group] = c + 1;
            }
            else
            {
                local = act.Area.center;
            }

            local += new Vector3((float)_rng.NextDouble() - 0.5f, 0f, (float)_rng.NextDouble() - 0.5f);
            string prefab = _room.Def.Enemies[p.Enemy];
            bool flyer = prefab.Contains("Flying") || prefab.Contains("Volley") || prefab.Contains("Bat") || prefab.Contains("Drone");
            if (flyer)
            {
                local.y += 2.5f;
            }

            Vector3 world = act.Root != null ? act.Root.TransformPoint(local) : local;
            Vector3 center = act.Root != null ? act.Root.TransformPoint(act.Area.center) : world;
            Vector3 face = center - world;
            float yaw = face.sqrMagnitude > 0.01f ? Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg : 0f;
            _room.Enemies.Spawn(p.Enemy, world, yaw);
        }
    }
}
