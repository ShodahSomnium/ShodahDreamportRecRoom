using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// The quest framework's event ids (game-specific range, >= <see cref="RrGameEvents.FirstGameSpecific"/>) and the
    /// few wire helpers they need. Positions travel as int32 centimetres relative to the room's origin
    /// (<see cref="RrCombat.Origin"/>), because acts sit up to ~800 m from it and S16 would overflow.
    ///
    /// Direction of travel (rr_quests.md 3.6, "victim decides"):
    ///   Request      member -> authority   [code, args...]; repeated by the sender until the effect shows in the ints
    ///   EnemyAttack  authority -> all      an enemy started an attack; watchers replay it against their own player
    ///   EnemyVolley  authority -> all      scripted shots (the Goblin King's fire orbs): everyone launches them
    ///   Points       authority -> all      gold earned at a place (coin VFX + floating number)
    ///   Revive       reviver -> all        the named downed player is back up
    ///   Toast        authority -> all      a short line for everyone's HUD ("Reinforcements!")
    ///   Coconut      authority -> all      a Coconut Chaos bomb drops (every client drops and judges it the same way)
    /// </summary>
    public static class RrQuestEvents
    {
        public const byte Request = RrGameEvents.FirstGameSpecific + 0;
        public const byte EnemyAttack = RrGameEvents.FirstGameSpecific + 1;
        public const byte EnemyVolley = RrGameEvents.FirstGameSpecific + 2;
        public const byte Points = RrGameEvents.FirstGameSpecific + 3;
        public const byte Revive = RrGameEvents.FirstGameSpecific + 4;
        public const byte Toast = RrGameEvents.FirstGameSpecific + 5;
        /// <summary>Authority -> all: a Coconut Chaos bomb starts falling from a world point (12 bytes).</summary>
        public const byte Coconut = RrGameEvents.FirstGameSpecific + 6;

        /// <summary>Request codes (first byte of a <see cref="Request"/>).</summary>
        public const byte ReqAdvance = 1;
        public const byte ReqBreak = 2;
        public const byte ReqStun = 3;
        public const byte ReqHazard = 4;
        /// <summary>[flags u8]: the mod set the member wants (lobby only).</summary>
        public const byte ReqMods = 5;

        /// <summary>Points kinds (how the gold was earned: picks the effect).</summary>
        public const byte PointsKill = 0;
        public const byte PointsBreak = 1;
        public const byte PointsLava = 2;
        public const byte PointsBoss = 3;

        public static void I32(byte[] b, ref int i, int v)
        {
            b[i++] = (byte)v;
            b[i++] = (byte)(v >> 8);
            b[i++] = (byte)(v >> 16);
            b[i++] = (byte)(v >> 24);
        }

        public static int I32(byte[] b, ref int i)
        {
            if (b == null || i + 4 > b.Length)
            {
                i += 4;
                return 0;
            }

            int v = b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24);
            i += 4;
            return v;
        }

        public static void U16(byte[] b, ref int i, int v)
        {
            b[i++] = (byte)v;
            b[i++] = (byte)(v >> 8);
        }

        public static int U16(byte[] b, ref int i)
        {
            if (b == null || i + 2 > b.Length)
            {
                i += 2;
                return 0;
            }

            int v = b[i] | (b[i + 1] << 8);
            i += 2;
            return v;
        }

        public static int U8(byte[] b, ref int i)
        {
            if (b == null || i >= b.Length)
            {
                i++;
                return 0;
            }

            return b[i++];
        }

        /// <summary>A world position as 3 x int32 cm relative to the room origin (12 bytes).</summary>
        public static void Pos(byte[] b, ref int i, Vector3 world)
        {
            Vector3 p = world - RrCombat.Origin;
            I32(b, ref i, Mathf.RoundToInt(p.x * 100f));
            I32(b, ref i, Mathf.RoundToInt(p.y * 100f));
            I32(b, ref i, Mathf.RoundToInt(p.z * 100f));
        }

        public static Vector3 Pos(byte[] b, ref int i)
        {
            float x = I32(b, ref i) / 100f;
            float y = I32(b, ref i) / 100f;
            float z = I32(b, ref i) / 100f;
            return RrCombat.Origin + new Vector3(x, y, z);
        }
    }
}
