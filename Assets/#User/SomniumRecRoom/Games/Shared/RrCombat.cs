using System;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// The combat events every shooter, quest and brawler shares, and who decides what (somnium_networking.md
    /// §5.7, Quake's split): the shooter's client simulates its own projectiles and decides hits on players
    /// against the hit volumes it sees ("favour the shooter") and sends Hit; the victim's client applies the
    /// damage to itself (<see cref="RrHealth"/>) and sends Death; the instance authority counts Deaths into scores.
    /// Every other client draws the shot from Fire alone (origin, direction, speed, seed) - projectiles are never
    /// network objects. Hits on AI go to the authority as ActorHit.
    ///
    /// Bodies (after the Net layer's own instance prefix; positions are S16 centimetres from the room origin, so a
    /// room must keep its fights within +-327 m of it, which every rebuilt map does):
    ///   Fire     15 B: tool u8, seq u8, origin 3xS16, direction yaw+pitch 2xAngle16, speed u16 cm/s, seed u8
    ///   Hit      12 B: victim u16, damage u8, part u8, seq u8, point 3xS16, tool u8
    ///   Death     9 B: killer u16 (0xFFFF none), tool u8, point 3xS16
    ///   ActorHit 13 B: actor u16, damage u16, part u8, tool u8, point 3xS16
    /// A client never relies on hearing its own event back (offline RrNet loops events back, online it may not):
    /// everything local is raised directly when sent, and received events from our own PlayerId are ignored.
    /// </summary>
    public static class RrCombat
    {
        public const int NoPlayer = -1;

        /// <summary>Practice targets and test dummies use ids from here (never a Fusion PlayerId); they have no team.</summary>
        public const int FirstTargetId = 60000;

        /// <summary>Tool id of an enemy's attack (Death events name no player killer then).</summary>
        public const byte EnemyTool = 250;

        public const byte UnknownTool = 255;

        public struct FireInfo
        {
            public int Shooter;
            public byte Tool;
            public byte Seq;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public byte Seed;
        }

        public struct HitInfo
        {
            public int Shooter;
            public int Victim;
            public int Damage;
            public RrBodyPart Part;
            public byte Seq;
            public Vector3 Point;
            public byte Tool;
        }

        public struct DeathInfo
        {
            public int Victim;
            public int Killer;
            public byte Tool;
            public Vector3 Point;
        }

        public struct ActorHitInfo
        {
            public int Attacker;
            public int Actor;
            public int Damage;
            public RrBodyPart Part;
            public byte Tool;
            public Vector3 Point;
        }

        /// <summary>Friendly fire for the current game (Rec Room's configs: off, except paint grenades).</summary>
        public static bool FriendlyFire;

        /// <summary>A shot from another member of the instance (the kit already draws it; games may add to it).</summary>
        public static event Action<FireInfo> OnRemoteFire;

        /// <summary>Every hit on a player: the local player's own hits as they are decided, others' as they arrive.</summary>
        public static event Action<HitInfo> OnHit;

        /// <summary>Every out in the instance (the local one included), for scores and kill feeds.</summary>
        public static event Action<DeathInfo> OnDeath;

        /// <summary>A member hit an AI actor (raised on the authority, and locally for the local player's own hits).</summary>
        public static event Action<ActorHitInfo> OnActorHit;

        public static int LocalKills;
        public static int LocalDeaths;
        public static int LocalHits;

        private static bool _registered;
        private static RrCombatDriver _driver;
        private static byte _seq;
        private static int _syncFrame = -1;

        public static int LocalId
        {
            get { return RrNet.LocalId; }
        }

        /// <summary>World position of the current room's (0, 0, 0): the reference of every position on the wire.</summary>
        public static Vector3 Origin
        {
            get
            {
                var room = RrTravel.CurrentRoom;
                return room != null ? room.Origin : Vector3.zero;
            }
        }

        /// <summary>Starts the kit: network handlers and the driver that steps projectiles. Safe to call often.</summary>
        public static void Ensure()
        {
            if (_driver == null)
            {
                var host = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
                var go = new GameObject("Rr Combat");
                go.transform.SetParent(host, false);
                _driver = go.AddComponent<RrCombatDriver>();
            }

            if (!_registered)
            {
                _registered = true;
                RrNet.On(RrGameEvents.Fire, ReceiveFire);
                RrNet.On(RrGameEvents.Hit, ReceiveHit);
                RrNet.On(RrGameEvents.Death, ReceiveDeath);
                RrNet.On(RrGameEvents.ActorHit, ReceiveActorHit);
                RrLog.Info("Combat", "started (friendly fire " + (FriendlyFire ? "on" : "off") + ", local id " + LocalId + ")");
            }
        }

        /// <summary>Physics queries see moved colliders only after a sync; hit volumes and enemies move in Update.</summary>
        public static void SyncPhysics()
        {
            if (_syncFrame != Time.frameCount)
            {
                _syncFrame = Time.frameCount;
                Physics.SyncTransforms();
            }
        }

        public static byte NextSeq()
        {
            _seq++;
            return _seq;
        }

        // ---- teams ----

        /// <summary>A player's team: the local choice for us, the presence slot for others, none for practice targets.</summary>
        public static int TeamOf(int playerId)
        {
            if (playerId < 0 || playerId >= FirstTargetId)
            {
                return RrTeams.None;
            }

            if (playerId == LocalId)
            {
                return RrTeams.LocalTeam;
            }

            var p = RrNet.Presence(playerId);
            return p.Valid ? p.Team : RrTeams.None;
        }

        /// <summary>Whether a player's attack may hurt another (never themselves; teammates only with friendly fire).</summary>
        public static bool CanDamage(int attackerId, int victimId)
        {
            if (attackerId == victimId && attackerId != NoPlayer)
            {
                return false;
            }

            if (FriendlyFire || attackerId < 0)
            {
                return true;
            }

            int a = TeamOf(attackerId);
            int v = TeamOf(victimId);
            return a < 0 || v < 0 || a != v;
        }

        /// <summary>The colour of a player's paint: their team's, or the weapon's default when they have none.</summary>
        public static Color ColorOf(int playerId, Color fallback)
        {
            int team = TeamOf(playerId);
            return team >= 0 ? RrTeams.Color(team) : fallback;
        }

        // ---- sending ----

        public static void SendFire(byte tool, byte seq, Vector3 origin, Vector3 direction, float speed, byte seed)
        {
            var b = new byte[15];
            int i = 0;
            b[i++] = tool;
            b[i++] = seq;
            RrCombatWire.Pos(b, ref i, origin);
            RrCombatWire.Dir(b, ref i, direction);
            RrCombatWire.U16(b, ref i, Mathf.Clamp(Mathf.RoundToInt(speed * 100f), 0, 65534));
            b[i] = seed;
            Send(RrGameEvents.Fire, b);
        }

        /// <summary>The local shooter decided a hit on a player: tell the victim (and raise it here at once).</summary>
        public static void SendHit(int victim, int damage, RrBodyPart part, byte seq, Vector3 point, byte tool)
        {
            LocalHits++;
            var b = new byte[12];
            int i = 0;
            RrCombatWire.U16(b, ref i, victim);
            b[i++] = (byte)Mathf.Clamp(damage, 0, 255);
            b[i++] = (byte)part;
            b[i++] = seq;
            RrCombatWire.Pos(b, ref i, point);
            b[i] = tool;
            Send(RrGameEvents.Hit, b);
            Raise(new HitInfo { Shooter = LocalId, Victim = victim, Damage = damage, Part = part, Seq = seq, Point = point, Tool = tool });
        }

        /// <summary>The local player is out: tell everyone (and raise it here at once).</summary>
        public static void SendDeath(int killer, byte tool, Vector3 point)
        {
            var b = new byte[9];
            int i = 0;
            RrCombatWire.U16(b, ref i, killer);
            b[i++] = tool;
            RrCombatWire.Pos(b, ref i, point);
            Send(RrGameEvents.Death, b);
            RaiseDeath(new DeathInfo { Victim = LocalId, Killer = killer, Tool = tool, Point = point });
        }

        /// <summary>
        /// The local player hit an AI actor: to the authority (raised here too, for local feedback and offline play).
        /// toAuthority false broadcasts it instead, for enemies every client simulates itself (no actor sync yet).
        /// </summary>
        public static void SendActorHit(int actor, int damage, RrBodyPart part, byte tool, Vector3 point, bool alsoSend, bool toAuthority = true)
        {
            if (alsoSend)
            {
                var b = new byte[13];
                int i = 0;
                RrCombatWire.U16(b, ref i, actor);
                RrCombatWire.U16(b, ref i, Mathf.Clamp(damage, 0, 65534));
                b[i++] = (byte)part;
                b[i++] = tool;
                RrCombatWire.Pos(b, ref i, point);
                Send(RrGameEvents.ActorHit, b, toAuthority);
            }

            var h = OnActorHit;
            if (h != null)
            {
                h(new ActorHitInfo { Attacker = LocalId, Actor = actor, Damage = damage, Part = part, Tool = tool, Point = point });
            }
        }

        private static void Send(byte type, byte[] body, bool toAuthority = false)
        {
            try
            {
                RrNet.Send(type, body, toAuthority);
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Combat", "send" + type, "sending event " + type + " failed: " + e.Message);
            }
        }

        // ---- receiving ----

        private static void ReceiveFire(int sender, byte[] b)
        {
            if (sender == LocalId || b == null || b.Length < 15)
            {
                return;
            }

            int i = 0;
            var f = new FireInfo { Shooter = sender, Tool = b[i++], Seq = b[i++] };
            f.Origin = RrCombatWire.Pos(b, ref i);
            f.Direction = RrCombatWire.Dir(b, ref i);
            f.Speed = RrCombatWire.U16(b, ref i) / 100f;
            f.Seed = b[i];
            RrWeapon.ShowRemoteShot(f);
            var h = OnRemoteFire;
            if (h != null)
            {
                h(f);
            }
        }

        private static void ReceiveHit(int sender, byte[] b)
        {
            if (sender == LocalId || b == null || b.Length < 11)
            {
                return;
            }

            int i = 0;
            var info = new HitInfo { Shooter = sender };
            info.Victim = RrCombatWire.U16(b, ref i);
            info.Damage = b[i++];
            info.Part = (RrBodyPart)b[i++];
            info.Seq = b[i++];
            info.Point = RrCombatWire.Pos(b, ref i);
            info.Tool = b.Length > i ? b[i] : UnknownTool;
            Raise(info);
        }

        private static void ReceiveDeath(int sender, byte[] b)
        {
            if (sender == LocalId || b == null || b.Length < 9)
            {
                return;
            }

            int i = 0;
            var d = new DeathInfo { Victim = sender };
            d.Killer = RrCombatWire.U16(b, ref i);
            d.Tool = b[i++];
            d.Point = RrCombatWire.Pos(b, ref i);
            RaiseDeath(d);
        }

        private static void ReceiveActorHit(int sender, byte[] b)
        {
            if (sender == LocalId || b == null || b.Length < 13)
            {
                return;
            }

            int i = 0;
            var a = new ActorHitInfo { Attacker = sender };
            a.Actor = RrCombatWire.U16(b, ref i);
            a.Damage = RrCombatWire.U16(b, ref i);
            a.Part = (RrBodyPart)b[i++];
            a.Tool = b[i++];
            a.Point = RrCombatWire.Pos(b, ref i);
            RrEnemy.ApplyRemoteHit(a);
            var h = OnActorHit;
            if (h != null)
            {
                h(a);
            }
        }

        private static void Raise(HitInfo info)
        {
            var h = OnHit;
            if (h != null)
            {
                try
                {
                    h(info);
                }
                catch (Exception e)
                {
                    RrLog.Error("Combat", "hit handler failed", e);
                }
            }
        }

        private static void RaiseDeath(DeathInfo d)
        {
            if (d.Killer == LocalId && d.Victim != LocalId)
            {
                LocalKills++;
            }

            if (d.Victim == LocalId)
            {
                LocalDeaths++;
            }

            var h = OnDeath;
            if (h != null)
            {
                try
                {
                    h(d);
                }
                catch (Exception e)
                {
                    RrLog.Error("Combat", "death handler failed", e);
                }
            }
        }

        /// <summary>Clears the kit's statics (the world's assembly survives a re-entry). Called by the driver's OnDestroy.</summary>
        public static void Shutdown()
        {
            if (_registered)
            {
                RrNet.Off(RrGameEvents.Fire, ReceiveFire);
                RrNet.Off(RrGameEvents.Hit, ReceiveHit);
                RrNet.Off(RrGameEvents.Death, ReceiveDeath);
                RrNet.Off(RrGameEvents.ActorHit, ReceiveActorHit);
                _registered = false;
            }

            _driver = null;
            OnRemoteFire = null;
            OnHit = null;
            OnDeath = null;
            OnActorHit = null;
            FriendlyFire = false;
            LocalKills = 0;
            LocalDeaths = 0;
            LocalHits = 0;
            _seq = 0;
            RrProjectile.ResetStatics();
            RrImpacts.ResetStatics();
            RrWeaponSpec.ResetStatics();
            RrHitVolume.ResetStatics();
            RrEnemy.ResetStatics();
            RrLoadout.ResetStatics();
            RrWeapon.ResetStatics();
            RrMelee.ResetStatics();
            RrHealth.ResetStatics();
            RrPrefabRefs.ResetStatics();
            RrPresenceStats.ResetStatics();
        }
    }

    /// <summary>Packing helpers for the combat bodies (little-endian, positions relative to the room origin).</summary>
    internal static class RrCombatWire
    {
        public static void U16(byte[] b, ref int i, int v)
        {
            b[i++] = (byte)(v & 0xFF);
            b[i++] = (byte)((v >> 8) & 0xFF);
        }

        public static int U16(byte[] b, ref int i)
        {
            int v = b[i] | (b[i + 1] << 8);
            i += 2;
            return v == 0xFFFF ? -1 : v;
        }

        private static void S16(byte[] b, ref int i, float v)
        {
            int s = Mathf.Clamp(Mathf.RoundToInt(v), short.MinValue, short.MaxValue);
            b[i++] = (byte)(s & 0xFF);
            b[i++] = (byte)((s >> 8) & 0xFF);
        }

        private static float S16(byte[] b, ref int i)
        {
            short s = (short)(b[i] | (b[i + 1] << 8));
            i += 2;
            return s;
        }

        public static void Pos(byte[] b, ref int i, Vector3 world)
        {
            var p = (world - RrCombat.Origin) * 100f;
            S16(b, ref i, p.x);
            S16(b, ref i, p.y);
            S16(b, ref i, p.z);
        }

        public static Vector3 Pos(byte[] b, ref int i)
        {
            float x = S16(b, ref i);
            float y = S16(b, ref i);
            float z = S16(b, ref i);
            return RrCombat.Origin + new Vector3(x, y, z) * 0.01f;
        }

        public static void Dir(byte[] b, ref int i, Vector3 d)
        {
            if (d.sqrMagnitude < 1e-8f)
            {
                d = Vector3.forward;
            }

            d.Normalize();
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
            U16(b, ref i, Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 360f * 65535f));
            U16(b, ref i, Mathf.RoundToInt((pitch + 90f) / 180f * 65535f));
        }

        public static Vector3 Dir(byte[] b, ref int i)
        {
            int yv = b[i] | (b[i + 1] << 8);
            int pv = b[i + 2] | (b[i + 3] << 8);
            i += 4;
            float yaw = yv / 65535f * 360f * Mathf.Deg2Rad;
            float pitch = (pv / 65535f * 180f - 90f) * Mathf.Deg2Rad;
            float c = Mathf.Cos(pitch);
            return new Vector3(Mathf.Sin(yaw) * c, Mathf.Sin(pitch), Mathf.Cos(yaw) * c);
        }
    }
}
