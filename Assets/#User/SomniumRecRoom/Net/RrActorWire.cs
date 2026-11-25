using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>One AI actor (a quest enemy) as an actor block carries it: pose in the room's own frame, and what it is doing.</summary>
    public struct RrActorState
    {
        /// <summary>Position relative to the instance's origin (the room's local frame), metres.</summary>
        public Vector3 Position;

        /// <summary>Heading about +y, degrees.</summary>
        public float Yaw;

        /// <summary>What kind of actor (0-255, the game's own table).</summary>
        public int Type;

        /// <summary>Animation state (0-255, the game's own table).</summary>
        public int Anim;

        /// <summary>Health in percent, 0-100.</summary>
        public int Health;

        /// <summary>RrActorWire.Flag* bits.</summary>
        public int Flags;

        /// <summary>0-7, bumped when a new actor takes the slot: watchers snap instead of sliding.</summary>
        public int Serial;

        public bool Used
        {
            get { return (Flags & RrActorWire.FlagUsed) != 0; }
        }

        public bool Alive
        {
            get { return (Flags & RrActorWire.FlagAlive) != 0; }
        }
    }

    /// <summary>
    /// An actor block's 64 ints, "Rr Net Actors 0..11" (NetworkBridgeData64; somnium_networking.md §5.8), shaped like
    /// ETS's traffic blocks and AoM's unit slots: 15 AI actors written at 10 Hz by the instance authority that leased
    /// the block, only changed ints on the wire, drawn by watchers on <see cref="RrTimeline"/>. On an authority
    /// migration the new authority adopts each actor from its last written pose. The same 60 ints can instead hold a
    /// bitset (1,920 bits: loot taken, breakables destroyed) through the raw accessors. Pure (see <see cref="RrWire"/>).
    ///
    ///   0  magic 'RRA' (8-31) | local block number (0-7): the doc's 'RRA1' with the '1' giving the instance's own
    ///      numbering of its blocks, so an instance can lease several and every client knows which is which
    ///   1  seq (bumped per write, and at least once a second: the heartbeat)
    ///   2  stamp ms (the writer's clock)
    ///   3  writer PlayerId+1 (0-15) | instance object k+1 (16-23) | instance generation (24-31)
    ///   4..63  15 actors x 4 ints:
    ///       +0 x mm relative to the instance origin (int32)  +1 z mm (int32)
    ///       +2 y S16 cm | yaw Angle16
    ///       +3 type (0-7) | anim (8-15) | health % (16-22) | flags (23-28) | serial (29-31)
    /// </summary>
    public static class RrActorWire
    {
        public const string ObjectPrefix = "Rr Net Actors ";
        public const int Ints = 64;
        public const int Objects = 12;
        public const int ActorsPerBlock = 15;
        public const int MagicBase = 0x52524100;

        public const int AtMagic = 0;
        public const int AtSeq = 1;
        public const int AtStamp = 2;
        public const int AtLease = 3;
        public const int AtActors = 4;

        public const int FlagUsed = 1;
        public const int FlagAlive = 2;
        public const int FlagAttacking = 4;
        public const int FlagHurt = 8;
        public const int FlagStunned = 16;
        public const int FlagSpare = 32;

        public static bool Written(int[] r)
        {
            return r != null && r.Length >= Ints && (r[AtMagic] & unchecked((int)0xFFFFFF00)) == MagicBase;
        }

        public static int LocalBlock(int[] r)
        {
            return r[AtMagic] & 0xFF;
        }

        public static int Writer(int[] r)
        {
            return RrWire.FromId16(r[AtLease]);
        }

        public static int LeaseObject(int[] r)
        {
            return RrWire.Field(r[AtLease], 16, 8) - 1;
        }

        public static int LeaseGeneration(int[] r)
        {
            return RrWire.Field(r[AtLease], 24, 8);
        }

        public static int PackLease(int writer, int instanceObject, int generation)
        {
            return RrWire.Id16(writer) | (((instanceObject + 1) & 0xFF) << 16) | ((generation & 0xFF) << 24);
        }

        public static bool LeaseMatches(int[] r, int instanceObject, int generation, int localBlock)
        {
            return Written(r) && LocalBlock(r) == (localBlock & 0xFF) && LeaseObject(r) == instanceObject && LeaseGeneration(r) == (generation & 0xFF);
        }

        /// <summary>A block a lease may take: never written, or leased to an instance object whose generation moved on or that nobody present names.</summary>
        public static bool FreeForLease(int[] r, int[] instanceGeneration, bool[] instanceLive)
        {
            if (!Written(r))
            {
                return true;
            }

            int k = LeaseObject(r);

            if (k < 0 || instanceGeneration == null || k >= instanceGeneration.Length)
            {
                return true;
            }

            return instanceGeneration[k] != LeaseGeneration(r) || !instanceLive[k];
        }

        /// <summary>A fresh lease: every int cleared, magic with the local block number, seq + 1, stamp, the lease.</summary>
        public static void WriteLease(int[] into, int localBlock, int writer, int instanceObject, int generation, int stampMs)
        {
            int seq = Written(into) ? into[AtSeq] + 1 : 1;
            System.Array.Clear(into, 0, into.Length);
            into[AtMagic] = MagicBase | (localBlock & 0xFF);
            into[AtSeq] = seq;
            into[AtStamp] = stampMs;
            into[AtLease] = PackLease(writer, instanceObject, generation);
        }

        public static void PutActor(int[] r, int actor, RrActorState s)
        {
            int at = AtActors + actor * 4;
            r[at] = RrWire.Mm(s.Position.x);
            r[at + 1] = RrWire.Mm(s.Position.z);
            r[at + 2] = RrWire.Pair(RrWire.S16(s.Position.y * 100.0), RrWire.Angle16(s.Yaw));
            r[at + 3] = (s.Type & 0xFF) | ((s.Anim & 0xFF) << 8) | (RrWire.Clamp(s.Health, 0, 127) << 16) | ((s.Flags & 0x3F) << 23) | ((s.Serial & 0x7) << 29);
        }

        public static RrActorState GetActor(int[] r, int actor)
        {
            int at = AtActors + actor * 4;
            int packed = r[at + 3];
            return new RrActorState
            {
                Position = new Vector3(RrWire.FromMm(r[at]), RrWire.Lo(r[at + 2]) / 100f, RrWire.FromMm(r[at + 1])),
                Yaw = RrWire.FromAngle16(r[at + 2] >> 16),
                Type = RrWire.Field(packed, 0, 8),
                Anim = RrWire.Field(packed, 8, 8),
                Health = RrWire.Field(packed, 16, 7),
                Flags = RrWire.Field(packed, 23, 6),
                Serial = RrWire.Field(packed, 29, 3)
            };
        }
    }
}
