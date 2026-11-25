using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// The quest's breakable loot (rr_quests.md 2.4): Rec Room's own clay pots "(FatPot)" (50 gold) and
    /// "(FatPot_Large)" (100 gold, BreakablePointsContainer + PointsDropper, measured) made into tools with
    /// <see cref="RrTools.MakeTool"/> (so they shatter with Rec Room's chunks and <c>Quest_PotterySmash</c> sounds, and
    /// can be picked up and thrown). Broken state is shared through a bitset actor block (networking 5.8): the
    /// breaker's client shatters its pot and asks the authority (repeated until the bit shows); the authority sets
    /// the bit and pays the gold once; every other client shatters its copy when it sees the bit. A swung blade
    /// (tip faster than 3 m/s within 0.35 m) or a click with a held blade in screen mode breaks a pot too.
    /// Pots are not moved over the network (only their broken state is).
    /// </summary>
    public sealed class RrQuestBreakables
    {
        public const int Block = 2;

        private sealed class Pot
        {
            public string Prefab;
            public Transform Parent;
            public Vector3 Local;
            public float Yaw;
            public int Points;
            public RrInstance Inst;
            public RrBreakable Breakable;
            public bool Broken;
            public float AskedAt = -10f;
            public float FirstAsk = -10f;
        }

        private readonly RrQuestRoom _room;
        private readonly List<Pot> _pots = new List<Pot>();
        private readonly RrNetActors _bits;
        private bool _applying;

        public RrQuestBreakables(RrQuestRoom room)
        {
            _room = room;
            _bits = RrNetActors.Get(Block);
        }

        public void Dispose()
        {
            RrNetActors.Release(Block);
        }

        public int Count
        {
            get { return _pots.Count; }
        }

        /// <summary>Sum of every pot's gold (added to the run's max possible at the start).</summary>
        public int TotalPoints
        {
            get
            {
                int n = 0;
                foreach (var p in _pots)
                {
                    n += p.Points;
                }

                return n;
            }
        }

        public int BrokenCount
        {
            get
            {
                int n = 0;
                foreach (var p in _pots)
                {
                    n += p.Broken ? 1 : 0;
                }

                return n;
            }
        }

        /// <summary>Places a pot (act-local position); its index is its order of adding (the same on every client).</summary>
        public void Add(Transform parent, string prefab, Vector3 local, float yaw, int points)
        {
            var pot = new Pot { Prefab = prefab, Parent = parent, Local = local, Yaw = yaw, Points = points };
            _pots.Add(pot);
            Respawn(pot);
        }

        /// <summary>Spawns (again) every pot that is missing or broken - a new run.</summary>
        public void ResetAll()
        {
            foreach (var p in _pots)
            {
                if (p.Inst != null)
                {
                    Object.Destroy(p.Inst.gameObject);
                }

                p.Inst = null;
                p.Breakable = null;
                p.Broken = false;
                Respawn(p);
            }

            if (_bits.IsWriter)
            {
                _bits.Clear();
            }
        }

        private void Respawn(Pot pot)
        {
            if (pot.Parent == null)
            {
                return;
            }

            RrSpawn.Spawn(pot.Prefab, pot.Parent, pot.Local + Vector3.up * 0.02f, Quaternion.Euler(0f, pot.Yaw, 0f), inst =>
            {
                if (inst == null)
                {
                    return;
                }

                if (pot.Parent == null || pot.Inst != null)
                {
                    Object.Destroy(inst.gameObject);
                    return;
                }

                pot.Inst = inst;
                RrTools.MakeTool(inst);
                pot.Breakable = inst.GetComponent<RrBreakable>();
                if (pot.Breakable != null)
                {
                    pot.Breakable.OnBroken += point => LocalBroken(pot, point);
                }

                if (pot.Broken)
                {
                    Shatter(pot);
                }
            }, RrSpawnOptions.Prop());
        }

        /// <summary>Every frame on every client.</summary>
        public void Step()
        {
            for (int i = 0; i < _pots.Count; i++)
            {
                var p = _pots[i];
                bool bit = _bits.GetBit(i);
                if (bit && !p.Broken)
                {
                    p.Broken = true;
                    Shatter(p);
                }
                else if (!bit && p.Broken && !_room.IsAuthority && Time.time - p.FirstAsk < 5f && Time.time - p.AskedAt > 0.5f)
                {
                    Ask(i, p);
                }
            }

            Blades();
        }

        private void Blades()
        {
            bool click = RrInput.UseDown && !RrPlayer.InVr;
            foreach (var m in RrMelee.All)
            {
                if (m == null || !m.IsHeld)
                {
                    continue;
                }

                Vector3 tip = m.transform.TransformPoint(m.BladeTip);
                Vector3 mid = m.transform.TransformPoint((m.BladeTip + m.BladeBase) * 0.5f);
                for (int i = 0; i < _pots.Count; i++)
                {
                    var p = _pots[i];
                    if (p.Broken || p.Inst == null)
                    {
                        continue;
                    }

                    Vector3 c = p.Inst.transform.position + Vector3.up * 0.25f;
                    bool swing = m.TipSpeed > 3f && (Vector3.Distance(tip, c) < 0.4f || Vector3.Distance(mid, c) < 0.35f);
                    bool clicked = click && m.ScreenHeld && Vector3.Distance(RrPlayer.FeetPosition, p.Inst.transform.position) < 2.4f && Aimed(c);
                    if (swing || clicked)
                    {
                        if (p.Breakable != null)
                        {
                            p.Breakable.Break(c, (c - RrPlayer.HeadPosition).normalized * 3f);
                        }
                        else
                        {
                            LocalBroken(p, c);
                        }
                    }
                }
            }
        }

        private static bool Aimed(Vector3 c)
        {
            var ray = RrInput.AimRay;
            float along = Vector3.Dot(c - ray.origin, ray.direction);
            return along > 0f && Vector3.Distance(ray.origin + ray.direction * along, c) < 0.45f;
        }

        private void LocalBroken(Pot pot, Vector3 point)
        {
            if (_applying || pot.Broken)
            {
                return;
            }

            pot.Broken = true;
            pot.FirstAsk = Time.time;
            if (pot.Breakable == null)
            {
                Shatter(pot);
            }

            Ask(_pots.IndexOf(pot), pot);
        }

        private void Ask(int index, Pot pot)
        {
            pot.AskedAt = Time.time;
            _room.Ask(RrQuestEvents.ReqBreak, (byte)(index & 255), (byte)(index >> 8));
        }

        private void Shatter(Pot pot)
        {
            if (pot.Inst == null)
            {
                return;
            }

            if (pot.Breakable != null)
            {
                if (!pot.Breakable.IsBroken)
                {
                    _applying = true;
                    try
                    {
                        pot.Breakable.Break(pot.Inst.transform.position + Vector3.up * 0.25f, Vector3.zero);
                    }
                    finally
                    {
                        _applying = false;
                    }
                }

                return;
            }

            // No BreakableObject config: our own smash (sound, dust, hide).
            Vector3 c = pot.Inst.transform.position + Vector3.up * 0.25f;
            RrSound.Play("Quest_PotterySmash0" + Random.Range(1, 4), c, 1f, 1f);
            RrFx.Spawn("VFX_Generic_SmokePoof01", c, Quaternion.identity, 0.6f);
            pot.Inst.gameObject.SetActive(false);
        }

        /// <summary>Test hook: breaks pot index as if the local player had smashed it.</summary>
        public void DebugBreak(int index)
        {
            if (index < 0 || index >= _pots.Count || _pots[index].Broken || _pots[index].Inst == null)
            {
                return;
            }

            var p = _pots[index];
            Vector3 c = p.Inst.transform.position + Vector3.up * 0.25f;
            if (p.Breakable != null)
            {
                p.Breakable.Break(c, Vector3.down);
            }
            else
            {
                LocalBroken(p, c);
            }
        }

        /// <summary>Authority: a member broke pot index; pays once.</summary>
        public void AuthorityBreak(int index)
        {
            if (index < 0 || index >= _pots.Count || _bits.GetBit(index))
            {
                return;
            }

            _bits.SetBit(index, true);
            var p = _pots[index];
            Vector3 at = p.Inst != null ? p.Inst.transform.position + Vector3.up * 0.4f : p.Parent != null ? p.Parent.TransformPoint(p.Local) : Vector3.zero;
            _room.AddPoints(p.Points, at, RrQuestEvents.PointsBreak);
        }
    }
}
