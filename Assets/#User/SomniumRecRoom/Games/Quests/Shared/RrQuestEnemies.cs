using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// A quest instance's enemies as 30 networked actors (rr_quests.md 3.5/3.6, networking 5.8): two
    /// <see cref="RrNetActors"/> blocks of 15. The instance authority simulates every <see cref="RrEnemy"/> (the
    /// "director"), and publishes its state each frame (sent at 10 Hz); every other member draws a follower copy per
    /// used slot (spawned from the roster prefab of the actor's type, snapped when the slot's serial changes) and
    /// feeds it <see cref="RrEnemy.ApplyState"/>. Enemy ids are derived from the slot (1 + slot * 8 + serial), so a
    /// watcher's weapon hit on its copy reaches the right enemy on the authority (ActorHit to the authority).
    ///
    /// Attacks follow the victim-decides rule: when an authority enemy starts an attack it sends
    /// <see cref="RrQuestEvents.EnemyAttack"/> with the aim point; each watcher replays it on its copy against its own
    /// player only (melee sphere with parry, the same bolts/orbs as RrEnemy.Shoot, charge contact). The authority's
    /// own player is judged by the real RrEnemy. Arrows and bolts stun basic goblins and imps for 1.5 s (wiki; elites
    /// are immune), a parry on a watcher stuns through a request.
    /// </summary>
    public sealed class RrQuestEnemies
    {
        public const int Slots = 30;
        public const int BlockA = 0, BlockB = 1;

        private readonly RrQuestRoom _room;
        private readonly RrEnemy[] _enemy = new RrEnemy[Slots];
        private readonly int[] _serial = new int[Slots];
        private readonly int[] _type = new int[Slots];
        private readonly float[] _deadAt = new float[Slots];
        private readonly RrEnemyState[] _seen = new RrEnemyState[Slots];
        private readonly bool[] _chargeHit = new bool[Slots];
        private readonly RrNetActors[] _blocks = new RrNetActors[2];
        private byte _bowTool = RrCombat.UnknownTool, _crossbowTool = RrCombat.UnknownTool;

        /// <summary>Authority: an enemy of a slot was killed (enemy, roster type, killer PlayerId or -1).</summary>
        public event Action<RrEnemy, int, int> OnKilled;

        /// <summary>Every client: an enemy's body is ready (enemy, roster type) - for per-type setup such as bosses.</summary>
        public event Action<RrEnemy, int> OnReady;

        public RrQuestEnemies(RrQuestRoom room)
        {
            _room = room;
            _blocks[0] = RrNetActors.Get(BlockA);
            _blocks[1] = RrNetActors.Get(BlockB);
            RrEnemy.OnKilled += EnemyKilled;
            RrCombat.OnActorHit += ActorHit;
            RrNet.On(RrQuestEvents.EnemyAttack, OnAttackEvent);
            RrNet.On(RrQuestEvents.EnemyVolley, OnVolleyEvent);
            _bowTool = RrWeaponSpec.ToolIdOf("(Longbow)");
            _crossbowTool = RrWeaponSpec.ToolIdOf("(Crossbow)");
        }

        public void Dispose()
        {
            RrEnemy.OnKilled -= EnemyKilled;
            RrCombat.OnActorHit -= ActorHit;
            RrNet.Off(RrQuestEvents.EnemyAttack, OnAttackEvent);
            RrNet.Off(RrQuestEvents.EnemyVolley, OnVolleyEvent);
            ClearAll(false);
            RrNetActors.Release(BlockA);
            RrNetActors.Release(BlockB);
        }

        /// <summary>Living enemies (spawning included).</summary>
        public int Alive
        {
            get
            {
                int n = 0;
                for (int s = 0; s < Slots; s++)
                {
                    if (_enemy[s] != null && _enemy[s].Alive)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public RrEnemy At(int slot)
        {
            return slot >= 0 && slot < Slots ? _enemy[slot] : null;
        }

        public int TypeAt(int slot)
        {
            return slot >= 0 && slot < Slots ? _type[slot] : -1;
        }

        public int SlotOf(RrEnemy e)
        {
            for (int s = 0; s < Slots; s++)
            {
                if (_enemy[s] == e && e != null)
                {
                    return s;
                }
            }

            return -1;
        }

        private static int IdOf(int slot, int serial)
        {
            return 1 + slot * 8 + (serial & 7);
        }

        // ------------------------------------------------------------------ authority

        /// <summary>Authority: spawns roster enemy type at a world position; null when every slot is busy.</summary>
        public RrEnemy Spawn(int type, Vector3 world, float yaw)
        {
            var def = _room.Def;
            if (def == null || type < 0 || type >= def.Enemies.Count)
            {
                return null;
            }

            int slot = -1;
            for (int s = 0; s < Slots; s++)
            {
                if (_enemy[s] == null && Time.time - _deadAt[s] > 1.5f)
                {
                    slot = s;
                    break;
                }
            }

            if (slot < 0)
            {
                RrLog.WarnOnce("Quest", "slots-full", "all " + Slots + " enemy slots are busy: spawn skipped");
                return null;
            }

            _serial[slot] = (_serial[slot] + 1) & 7;
            return Make(slot, type, world, yaw);
        }

        private RrEnemy Make(int slot, int type, Vector3 world, float yaw)
        {
            _type[slot] = type;
            _deadAt[slot] = -1f;
            _seen[slot] = RrEnemyState.Spawning;
            _chargeHit[slot] = false;
            int s = slot;
            var e = RrEnemy.Spawn(_room.Def.Enemies[type], _room.transform, world, yaw, ready =>
            {
                if (ready != null && OnReady != null)
                {
                    OnReady(ready, _type[s]);
                }
            }, IdOf(slot, _serial[slot]));
            _enemy[slot] = e;
            return e;
        }

        /// <summary>Removes every enemy (act reset, back to the lobby); the authority also clears the actor slots.</summary>
        public void ClearAll(bool publish = true)
        {
            for (int s = 0; s < Slots; s++)
            {
                if (_enemy[s] != null)
                {
                    UnityEngine.Object.Destroy(_enemy[s].gameObject);
                }

                _enemy[s] = null;
                _deadAt[s] = -10f;
                if (publish && _blocks[s / 15].IsWriter)
                {
                    _blocks[s / 15].Put(s % 15, default(RrActorState));
                }
            }
        }

        /// <summary>Kills every living enemy (test hook, act skip), giving no points.</summary>
        public void KillAll()
        {
            for (int s = 0; s < Slots; s++)
            {
                if (_enemy[s] != null && _enemy[s].Alive)
                {
                    _enemy[s].Die(RrCombat.NoPlayer, false);
                }
            }
        }

        /// <summary>
        /// Kills every living enemy except one (a boss's death takes its minions with it), giving no points. Returns
        /// how many died.
        /// </summary>
        public int KillAllExcept(RrEnemy keep)
        {
            int n = 0;
            for (int s = 0; s < Slots; s++)
            {
                var e = _enemy[s];
                if (e != null && e != keep && e.Alive)
                {
                    e.Die(RrCombat.NoPlayer, false);
                    n++;
                }
            }

            return n;
        }

        private void EnemyKilled(RrEnemy e, int killer)
        {
            int slot = SlotOf(e);
            if (slot < 0 || !_room.IsAuthority)
            {
                return;
            }

            var h = OnKilled;
            if (h != null)
            {
                h(e, _type[slot], killer);
            }
        }

        private void ActorHit(RrCombat.ActorHitInfo a)
        {
            bool arrow = a.Tool != RrCombat.UnknownTool && (a.Tool == _bowTool || a.Tool == _crossbowTool);
            if (!_room.IsAuthority || !(arrow || (_room.Mods != null && _room.Mods.StunsLikeArrow(a.Tool))))
            {
                return;
            }

            var e = RrEnemy.Find(a.Actor);
            if (e != null && e.Alive && !e.Elite && (e.Type == 0 || e.Type == 1))
            {
                e.Stun(1.5f);
            }
        }

        // ------------------------------------------------------------------ every frame

        public void Step()
        {
            bool authority = _room.IsAuthority;
            for (int s = 0; s < Slots; s++)
            {
                var block = _blocks[s / 15];
                int actor = s % 15;
                if (authority && block.IsWriter)
                {
                    Publish(s, block, actor);
                }
                else if (!authority)
                {
                    Follow(s, block, actor);
                }

                if (!authority)
                {
                    WatchCharge(s);
                }
            }
        }

        private void Publish(int s, RrNetActors block, int actor)
        {
            var e = _enemy[s];
            if (e == null)
            {
                return;
            }

            var st = e.CaptureState();
            st.Position += RrCombat.Origin;
            st.Type = _type[s];
            st.Serial = _serial[s];
            if (!e.Alive)
            {
                st.Flags &= ~RrActorWire.FlagAlive;
                if (_deadAt[s] < 0f)
                {
                    _deadAt[s] = Time.time;
                }
            }

            block.Put(actor, st);
            if (e.State != _seen[s])
            {
                if (e.State == RrEnemyState.Attack)
                {
                    AnnounceAttack(s, e);
                }

                _seen[s] = e.State;
            }

            // A dead enemy's slot is freed once the watchers have seen it die (its object destroys itself).
            if (!e.Alive && Time.time - _deadAt[s] > 1f)
            {
                block.Put(actor, default(RrActorState));
                _enemy[s] = null;
            }
        }

        private void Follow(int s, RrNetActors block, int actor)
        {
            RrActorState st;
            bool used = block.Sample(actor, out st);
            var e = _enemy[s];
            if (!used)
            {
                if (e != null)
                {
                    if (e.Alive)
                    {
                        e.Die(RrCombat.NoPlayer, false);
                    }

                    _enemy[s] = null;
                }

                return;
            }

            var def = _room.Def;
            if (e == null || _serial[s] != st.Serial || _type[s] != st.Type)
            {
                if (e != null)
                {
                    UnityEngine.Object.Destroy(e.gameObject);
                    _enemy[s] = null;
                }

                if (!st.Alive || def == null || st.Type < 0 || st.Type >= def.Enemies.Count)
                {
                    return;
                }

                _serial[s] = st.Serial;
                e = Make(s, st.Type, st.Position, st.Yaw);
            }

            if (e != null)
            {
                st.Position -= RrCombat.Origin;
                e.ApplyState(st);
            }
        }

        // ------------------------------------------------------------------ attacks (victim decides)

        private void AnnounceAttack(int slot, RrEnemy e)
        {
            if (!RrNet.Online || e.Kind == RrEnemyKind.Charger || e.Kind == RrEnemyKind.Lunger || e.Kind == RrEnemyKind.Exploder)
            {
                return;
            }

            var b = new byte[2 + 12];
            int i = 0;
            b[i++] = (byte)slot;
            b[i++] = (byte)_serial[slot];
            RrQuestEvents.Pos(b, ref i, TargetChest(e));
            RrNet.Send(RrQuestEvents.EnemyAttack, b);
        }

        /// <summary>Where an enemy aims: its target's chest (the local player or a remote's body), else ahead.</summary>
        public static Vector3 TargetChest(RrEnemy e)
        {
            int t = e.Target;
            if (t == RrCombat.LocalId && t != RrCombat.NoPlayer)
            {
                return RrPlayer.HeadPosition + Vector3.down * 0.35f;
            }

            var r = RrRemotePlayers.Get(t);
            if (r != null && r.Root != null)
            {
                return r.Head != null ? r.Head.position + Vector3.down * 0.35f : r.Root.position + Vector3.up * 1.2f;
            }

            return e.Center + e.transform.forward * 5f;
        }

        private void OnAttackEvent(int sender, byte[] body)
        {
            if (_room.IsAuthority || body == null || body.Length < 14)
            {
                return;
            }

            int i = 0;
            int slot = body[i++];
            int serial = body[i++];
            Vector3 aim = RrQuestEvents.Pos(body, ref i);
            if (slot >= Slots || _serial[slot] != serial || _enemy[slot] == null)
            {
                return;
            }

            var e = _enemy[slot];
            if (e.Kind == RrEnemyKind.Melee)
            {
                float reach = e.AttackRange > 1.5f ? 1.2f : 0.9f;
                Vector3 center = e.transform.position + e.transform.forward * reach + Vector3.up * (e.Height * 0.5f);
                RrSound.Play("Throw01", center, 0.6f, UnityEngine.Random.Range(0.9f, 1.1f));
                RrMelee blocker;
                if (RrMelee.TryBlock(center, 0.6f, out blocker))
                {
                    _room.Ask(RrQuestEvents.ReqStun, (byte)slot, (byte)serial, (byte)(e.Type == 21 || e.Type == 34 ? 20 : 5));
                    return;
                }

                if (LocalWithin(center, 0.6f))
                {
                    HurtLocal(e.Damage, center);
                }

                return;
            }

            _room.StartCoroutine(Shots(e, serial, slot, aim));
        }

        private IEnumerator Shots(RrEnemy e, int serial, int slot, Vector3 aim)
        {
            var spec = ShotOf(e);
            int burst = Mathf.Max(1, e.Burst);
            for (int n = 0; n < burst; n++)
            {
                if (e == null || !e.Alive || _serial[slot] != serial)
                {
                    yield break;
                }

                Vector3 origin = e.transform.position + Vector3.up * (e.Height * 0.6f) + e.transform.forward * (e.Radius + 0.15f);
                Vector3 v = (aim - origin).normalized * spec.Speed;
                int count = Mathf.Max(1, e.Volley);
                for (int k = 0; k < count; k++)
                {
                    float yaw = count > 1 ? Mathf.Lerp(-20f, 20f, k / (float)(count - 1)) : 0f;
                    RrProjectile.Launch(spec, origin, Quaternion.Euler(0f, yaw, 0f) * v, RrCombat.NoPlayer, false, 0, e.transform, e, e.ShotColor);
                }

                if (!string.IsNullOrEmpty(e.ShotClip))
                {
                    RrSound.Play(e.ShotClip, origin, 0.7f, UnityEngine.Random.Range(0.85f, 1f));
                }

                yield return new WaitForSeconds(0.15f);
            }
        }

        private static RrShotSpec ShotOf(RrEnemy e)
        {
            var s = RrShotSpec.Bolt(Mathf.Max(e.ProjectileSpeed, 3f), e.Damage, e.ShotColor);
            if (e.Splash > 0f)
            {
                s.ExplosionRadius = e.Splash;
                s.ExplosionDamage = e.Damage;
            }

            s.Tool = RrCombat.EnemyTool;
            s.Life = 6f;
            return s;
        }

        private void WatchCharge(int s)
        {
            var e = _enemy[s];
            if (e == null || (e.Kind != RrEnemyKind.Charger && e.Kind != RrEnemyKind.Lunger))
            {
                return;
            }

            if (e.State != RrEnemyState.Attack)
            {
                _chargeHit[s] = false;
                return;
            }

            if (!_chargeHit[s] && LocalWithin(e.Center, e.Radius + 0.45f))
            {
                _chargeHit[s] = true;
                HurtLocal(e.Damage, e.Center);
            }
        }

        // ------------------------------------------------------------------ scripted volleys (bosses)

        /// <summary>
        /// Authority: an enemy fires count orbs from origin at target in a fan (spread degrees total): every client
        /// launches them on its copy, so each player's own client judges their hits. speed m/s, radius m, splash m.
        /// </summary>
        public void Volley(RrEnemy e, Vector3 origin, Vector3 target, int count, float spread, float speed, float radius, int damage, float splash)
        {
            int slot = SlotOf(e);
            if (slot < 0)
            {
                return;
            }

            var b = new byte[2 + 24 + 6];
            int i = 0;
            b[i++] = (byte)slot;
            b[i++] = (byte)_serial[slot];
            RrQuestEvents.Pos(b, ref i, origin);
            RrQuestEvents.Pos(b, ref i, target);
            b[i++] = (byte)Mathf.Clamp(count, 1, 12);
            b[i++] = (byte)Mathf.Clamp(Mathf.RoundToInt(spread), 0, 180);
            b[i++] = (byte)Mathf.Clamp(Mathf.RoundToInt(speed * 4f), 1, 255);
            b[i++] = (byte)Mathf.Clamp(Mathf.RoundToInt(radius * 100f), 1, 255);
            b[i++] = (byte)Mathf.Clamp(damage, 0, 255);
            b[i++] = (byte)Mathf.Clamp(Mathf.RoundToInt(splash * 10f), 0, 255);
            RrNet.Send(RrQuestEvents.EnemyVolley, b);
        }

        private void OnVolleyEvent(int sender, byte[] body)
        {
            if (body == null || body.Length < 32)
            {
                return;
            }

            int i = 0;
            int slot = body[i++];
            int serial = body[i++];
            Vector3 origin = RrQuestEvents.Pos(body, ref i);
            Vector3 target = RrQuestEvents.Pos(body, ref i);
            int count = body[i++];
            float spread = body[i++];
            float speed = body[i++] / 4f;
            float radius = body[i++] / 100f;
            int damage = body[i++];
            float splash = body[i++] / 10f;
            var e = slot < Slots && _serial[slot] == serial ? _enemy[slot] : null;
            var spec = new RrShotSpec
            {
                Name = "enemy orb", Speed = speed, Gravity = 0f, Radius = radius, Color = new Color(1f, 0.45f, 0.1f), TeamColored = false,
                HeadDamage = damage, BodyDamage = damage, Crits = false, Laser = true, TrailTime = 0.3f, TrailWidth = radius * 1.6f,
                ExplosionRadius = splash, ExplosionDamage = damage, Tool = RrCombat.EnemyTool, Life = 8f,
                HitPlayerClip = "Arena_PlayerDamaged01", HitEnvironmentClip = "EnemyHit_02"
            };
            Vector3 v = (target - origin).normalized * speed;
            for (int k = 0; k < count; k++)
            {
                float yaw = count > 1 ? Mathf.Lerp(-spread * 0.5f, spread * 0.5f, k / (float)(count - 1)) : 0f;
                RrProjectile.Launch(spec, origin, Quaternion.Euler(0f, yaw, 0f) * v, RrCombat.NoPlayer, false, 0, e != null ? e.transform : null, e, spec.Color);
            }

            RrFx.Spawn("[VFX_Wand_MuzzelFlash]", origin, Quaternion.LookRotation(v.sqrMagnitude > 0f ? v : Vector3.forward), 2f);
            RrSound.Play("Goblin2_RomanCandle_Fire01", origin, 0.9f, 0.7f);
        }

        // ------------------------------------------------------------------ helpers

        private static Vector3 ClosestOnLocal(Vector3 p)
        {
            Vector3 a = RrPlayer.FeetPosition;
            Vector3 b = RrPlayer.HeadPosition;
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t;
        }

        /// <summary>The local player's body (capsule feet-head, r 0.25) meets a sphere.</summary>
        public static bool LocalWithin(Vector3 center, float radius)
        {
            return RrPlayer.Camera != null && Vector3.Distance(ClosestOnLocal(center), center) <= radius + 0.25f;
        }

        /// <summary>
        /// An enemy's hit on the local player. With the quests' one-hit rule (<see cref="RrQuestDef.PlayerHealth"/> 1)
        /// any damage that counts is a down; the amount only matters to a quest that sets more health.
        /// </summary>
        public static void HurtLocal(int damage, Vector3 point)
        {
            var h = RrHealth.Local;
            if (h != null && damage > 0 && h.InCombat)
            {
                h.ApplyDamage(damage, RrCombat.NoPlayer, RrBodyPart.Body, point, RrCombat.EnemyTool);
            }
        }
    }
}
