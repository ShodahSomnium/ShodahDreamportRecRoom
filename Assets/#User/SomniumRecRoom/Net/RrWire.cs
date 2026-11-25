using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// The small encodings every layout of this world packs into Fusion's networked ints (somnium_networking.md
    /// §4.9, ported from ETS's EtsPlayerWire/EtsNetWire). Pure: no Fusion, no scene and no Unity engine calls - only
    /// Vector3/Quaternion field arithmetic, which is plain C# in UnityEngine.CoreModule - so the offline test
    /// (SP\nettest) runs every method here outside Unity. That is also why rotations never go through
    /// Quaternion.Euler, Slerp or Inverse (native calls) in this file or any other *Wire / RrNetCore file.
    ///
    /// Two traps the siblings fell into, avoided here:
    /// - PlayerIds are session-scoped and grow with every (re)join, so they are packed as PlayerId + 1 in 16 bits,
    ///   never in a byte (ETS EtsPlayerWire.cs:243).
    /// - Community Modules' NetValue.SetByte/SetShort cast the merged int to short and destroy the upper bits
    ///   (Q\Networking\Utils\NetValue.cs:65,86); every field here is masked and shifted explicitly instead.
    /// </summary>
    public static class RrWire
    {
        /// <summary>sqrt(0.5): the largest a non-largest quaternion component can be.</summary>
        private const float SmallestThreeRange = 0.70710678f;

        // ------------------------------------------------------------------ bit fields

        /// <summary>count bits of v starting at shift (unsigned).</summary>
        public static int Field(int v, int shift, int count)
        {
            return (int)(((uint)v >> shift) & ((1u << count) - 1u));
        }

        /// <summary>v with count bits at shift replaced by field (masked).</summary>
        public static int With(int v, int shift, int count, int field)
        {
            uint mask = ((1u << count) - 1u) << shift;
            return (int)(((uint)v & ~mask) | (((uint)field << shift) & mask));
        }

        public static bool Bit(int v, int bit)
        {
            return (v & (1 << bit)) != 0;
        }

        public static int WithBit(int v, int bit, bool on)
        {
            return on ? v | (1 << bit) : v & ~(1 << bit);
        }

        /// <summary>A PlayerId as it is stored (PlayerId + 1 in 16 bits; 0 = nobody).</summary>
        public static int Id16(int playerId)
        {
            return playerId < 0 ? 0 : (playerId + 1) & 0xFFFF;
        }

        /// <summary>The PlayerId in the low 16 bits (-1 = nobody).</summary>
        public static int FromId16(int v)
        {
            return (v & 0xFFFF) - 1;
        }

        // ------------------------------------------------------------------ numbers

        /// <summary>A turn in 16 bits: round(deg / 360 * 65536), wrapped (0.0055 degree steps).</summary>
        public static int Angle16(float degrees)
        {
            double t = degrees / 360.0;
            t -= Math.Floor(t);
            return (int)Math.Round(t * 65536.0) & 0xFFFF;
        }

        /// <summary>-180..180 degrees from the low 16 bits.</summary>
        public static float FromAngle16(int value)
        {
            double d = (value & 0xFFFF) * (360.0 / 65536.0);
            return (float)(d > 180.0 ? d - 360.0 : d);
        }

        /// <summary>A value clamped to a signed 16-bit range, in the low 16 bits.</summary>
        public static int S16(double value)
        {
            double r = Math.Round(value);
            int v = r < -32768.0 ? -32768 : r > 32767.0 ? 32767 : (int)r;
            return v & 0xFFFF;
        }

        /// <summary>The signed value of the low 16 bits.</summary>
        public static int FromS16(int v)
        {
            return (short)(v & 0xFFFF);
        }

        public static int Pair(int lo, int hi)
        {
            return (lo & 0xFFFF) | (hi << 16);
        }

        /// <summary>Signed low half.</summary>
        public static int Lo(int v)
        {
            return (short)(v & 0xFFFF);
        }

        /// <summary>Signed high half.</summary>
        public static int Hi(int v)
        {
            return (short)((v >> 16) & 0xFFFF);
        }

        /// <summary>Metres to whole millimetres, clamped to int32 (+-2147 km).</summary>
        public static int Mm(float metres)
        {
            double r = Math.Round(metres * 1000.0);
            return r < int.MinValue ? int.MinValue : r > int.MaxValue ? int.MaxValue : (int)r;
        }

        public static float FromMm(int mm)
        {
            return (float)(mm / 1000.0);
        }

        public static int Clamp(int v, int low, int high)
        {
            return v < low ? low : v > high ? high : v;
        }

        public static int Bits(float value)
        {
            return BitConverter.SingleToInt32Bits(value);
        }

        public static float Float(int bits)
        {
            return BitConverter.Int32BitsToSingle(bits);
        }

        // ------------------------------------------------------------------ vectors and rotations

        public static void PutMm(int[] into, int at, Vector3 v)
        {
            into[at] = Mm(v.x);
            into[at + 1] = Mm(v.y);
            into[at + 2] = Mm(v.z);
        }

        public static Vector3 GetMm(int[] from, int at)
        {
            return new Vector3(FromMm(from[at]), FromMm(from[at + 1]), FromMm(from[at + 2]));
        }

        /// <summary>
        /// Smallest-three rotation in 32 bits (2-bit index of the largest component, then three 10-bit components in
        /// +-0.7071): about 0.16 degree steps, plenty for a thrown ball or a held tool, and one int instead of four.
        /// </summary>
        public static int PackRot(Quaternion q)
        {
            float x = q.x, y = q.y, z = q.z, w = q.w;
            float ax = Math.Abs(x), ay = Math.Abs(y), az = Math.Abs(z), aw = Math.Abs(w);
            int big = 0;
            float max = ax;

            if (ay > max)
            {
                big = 1;
                max = ay;
            }

            if (az > max)
            {
                big = 2;
                max = az;
            }

            if (aw > max)
            {
                big = 3;
            }

            float sign = (big == 0 ? x : big == 1 ? y : big == 2 ? z : w) < 0f ? -1f : 1f;
            int packed = big;
            int shift = 2;

            for (int i = 0; i < 4; i++)
            {
                if (i == big)
                {
                    continue;
                }

                float c = i == 0 ? x : i == 1 ? y : i == 2 ? z : w;
                float v = c * sign / SmallestThreeRange;
                int u = (int)Math.Round((v * 0.5f + 0.5f) * 1023f);
                packed |= (u < 0 ? 0 : u > 1023 ? 1023 : u) << shift;
                shift += 10;
            }

            return packed;
        }

        public static Quaternion UnpackRot(int packed)
        {
            int big = packed & 3;
            float x = 0f, y = 0f, z = 0f, w = 0f, sum = 0f;
            int shift = 2;

            for (int i = 0; i < 4; i++)
            {
                if (i == big)
                {
                    continue;
                }

                float c = (((packed >> shift) & 1023) / 1023f * 2f - 1f) * SmallestThreeRange;
                sum += c * c;
                shift += 10;

                if (i == 0) x = c;
                else if (i == 1) y = c;
                else if (i == 2) z = c;
                else w = c;
            }

            float l = (float)Math.Sqrt(Math.Max(0f, 1f - sum));

            if (big == 0) x = l;
            else if (big == 1) y = l;
            else if (big == 2) z = l;
            else w = l;

            return Normalize(new Quaternion(x, y, z, w));
        }

        /// <summary>Angular velocity as three signed 10-bit components in 0.1 rad/s (+-51 rad/s).</summary>
        public static int PackAngular(Vector3 w)
        {
            return S10(w.x * 10f) | (S10(w.y * 10f) << 10) | (S10(w.z * 10f) << 20);
        }

        public static Vector3 UnpackAngular(int v)
        {
            return new Vector3(FromS10(v) / 10f, FromS10(v >> 10) / 10f, FromS10(v >> 20) / 10f);
        }

        private static int S10(float value)
        {
            double r = Math.Round(value);
            int v = r < -511.0 ? -511 : r > 511.0 ? 511 : (int)r;
            return v & 0x3FF;
        }

        private static int FromS10(int v)
        {
            v &= 0x3FF;
            return v >= 512 ? v - 1024 : v;
        }

        public static Quaternion Normalize(Quaternion q)
        {
            float length = (float)Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);

            // An unwritten or damaged rotation is not a promise: identity rather than NaN.
            return length > 1e-4f && !float.IsNaN(length) ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : new Quaternion(0f, 0f, 0f, 1f);
        }

        /// <summary>A rotation about +y by degrees (pure: Quaternion.Euler is a native call).</summary>
        public static Quaternion FromYaw(float degrees)
        {
            double h = degrees * Math.PI / 360.0;
            return new Quaternion(0f, (float)Math.Sin(h), 0f, (float)Math.Cos(h));
        }

        /// <summary>The heading of a rotation about +y, degrees -180..180 (pure).</summary>
        public static float YawOf(Quaternion q)
        {
            // Forward (0, 0, 1) rotated by q, projected on the ground.
            double fx = 2.0 * (q.x * q.z + q.w * q.y);
            double fz = 1.0 - 2.0 * (q.x * q.x + q.y * q.y);
            return (float)(Math.Atan2(fx, fz) * 180.0 / Math.PI);
        }

        /// <summary>
        /// Normalised lerp along the short way round. Over the 30-60 ms between two samples it is indistinguishable
        /// from a slerp, and unlike Quaternion.Slerp it is plain C# (ETS EtsNetWire.Nlerp).
        /// </summary>
        public static Quaternion Nlerp(Quaternion a, Quaternion b, float t)
        {
            float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
            float sign = dot < 0f ? -1f : 1f;
            float x = a.x + (b.x * sign - a.x) * t;
            float y = a.y + (b.y * sign - a.y) * t;
            float z = a.z + (b.z * sign - a.z) * t;
            float w = a.w + (b.w * sign - a.w) * t;
            float length = (float)Math.Sqrt(x * x + y * y + z * z + w * w);
            return length > 1e-6f ? new Quaternion(x / length, y / length, z / length, w / length) : b;
        }

        /// <summary>The angle between two rotations, degrees (pure).</summary>
        public static float AngleBetween(Quaternion a, Quaternion b)
        {
            float dot = Math.Abs(a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w);
            return dot >= 1f ? 0f : (float)(2.0 * Math.Acos(dot) * 180.0 / Math.PI);
        }

        /// <summary>Cubic Hermite between two positions with their velocities over a span of seconds.</summary>
        public static Vector3 Hermite(Vector3 p0, Vector3 v0, Vector3 p1, Vector3 v1, float span, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            float h00 = 2f * u3 - 3f * u2 + 1f;
            float h10 = u3 - 2f * u2 + u;
            float h01 = -2f * u3 + 3f * u2;
            float h11 = u3 - u2;
            return p0 * h00 + v0 * (h10 * span) + p1 * h01 + v1 * (h11 * span);
        }

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        public static bool Finite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        // ------------------------------------------------------------------ identity and lists

        /// <summary>FNV-1a 32 of a string (a stable UserId hash for presence int 6; 0 for null).</summary>
        public static int Fnv1a(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            uint h = 2166136261u;

            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619u;
            }

            return (int)h;
        }

        public static bool Contains(IReadOnlyList<int> list, int value)
        {
            if (list == null || value < 0)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
