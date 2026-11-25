using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// The Maker Pen's network events and palette. Events are instance-scoped (<see cref="Net.RrNet.Send"/>): only the
    /// members of the sender's instance get them, and in an unnetworked place (the Dorm) they only loop back, so ink
    /// never leaks between places. Ids 20-23 sit in the shared catalogue range (1-31), which game-specific ids
    /// (64+) never reuse, so the pen works inside every game room too.
    ///
    /// Points are room-local (every room stands at the world origin): a segment carries a base point in cm (s16,
    /// +-327 m) and each point as a mm offset from it (s16), with its radius in 0.25 mm steps (u8), so the author's
    /// own tube and everyone else's are built from the same numbers. A segment carries its start index, so a
    /// re-send is idempotent.
    /// </summary>
    public static class RrMakerPenWire
    {
        /// <summary>Author -> all: [stroke u16][colour u8][start u16][n u8][base x,y,z s16 cm] + n x (dx,dy,dz s16 mm, r u8).</summary>
        public const byte EvPoints = 20;

        /// <summary>Author -> all: [count u8] + count x stroke u16; count 0 removes all of the author's strokes.</summary>
        public const byte EvDelete = 21;

        /// <summary>Anyone -> all: [] "send me your ink" (a late joiner, or a gap was seen).</summary>
        public const byte EvResend = 22;

        /// <summary>Author -> all, 5 Hz while held: [held u8][x,y,z s16 cm][rotation 4 x s8] for the pen others see.</summary>
        public const byte EvPen = 23;

        public const int PointsHeader = 12;
        public const int PointBytes = 7;
        public const int MaxPointsPerEvent = 60;
        public const int MaxIdsPerDelete = 100;
        public const int PenBytes = 11;

        /// <summary>MakerPen.defaultColor in the prefab data.</summary>
        public const byte DefaultColour = 1;

        /// <summary>
        /// 32 swatches in the Maker Pen palette's style (Rec Room fills its swatches at runtime and its 60 colours
        /// are not in the bundles - GUESS): neutrals, the eight hues, pink and two browns (0-15, the iter13 set; ink
        /// events carry these indices), then a light row and a dark row (16-31, iter14). 8 per palette row.
        /// </summary>
        public static readonly Color[] Palette =
        {
            new Color(0.10f, 0.10f, 0.12f), new Color(0.95f, 0.95f, 0.93f), new Color(0.70f, 0.71f, 0.72f), new Color(0.36f, 0.37f, 0.40f),
            new Color(0.90f, 0.20f, 0.18f), new Color(1.00f, 0.55f, 0.05f), new Color(1.00f, 0.85f, 0.15f), new Color(0.62f, 0.84f, 0.20f),
            new Color(0.25f, 0.65f, 0.25f), new Color(0.15f, 0.70f, 0.65f), new Color(0.35f, 0.70f, 0.95f), new Color(0.20f, 0.40f, 0.90f),
            new Color(0.55f, 0.30f, 0.80f), new Color(0.97f, 0.50f, 0.70f), new Color(0.50f, 0.32f, 0.18f), new Color(0.85f, 0.70f, 0.50f),
            new Color(1.00f, 0.62f, 0.60f), new Color(1.00f, 0.78f, 0.55f), new Color(1.00f, 0.95f, 0.62f), new Color(0.75f, 0.95f, 0.70f),
            new Color(0.62f, 0.92f, 0.86f), new Color(0.68f, 0.84f, 1.00f), new Color(0.80f, 0.72f, 0.96f), new Color(1.00f, 0.78f, 0.88f),
            new Color(0.50f, 0.09f, 0.11f), new Color(0.70f, 0.32f, 0.12f), new Color(0.46f, 0.45f, 0.13f), new Color(0.10f, 0.36f, 0.16f),
            new Color(0.05f, 0.38f, 0.40f), new Color(0.10f, 0.16f, 0.46f), new Color(0.31f, 0.13f, 0.46f), new Color(0.28f, 0.17f, 0.10f)
        };

        public static Color ColourOf(byte index)
        {
            return Palette[index % Palette.Length];
        }

        // ------------------------------------------------------------------ points

        /// <summary>The wire form of points [start, start + n) of one stroke (room-local, already quantised).</summary>
        public static byte[] EncodePoints(int strokeId, byte colour, int start, List<Vector3> points, List<float> radii, int n)
        {
            var body = new byte[PointsHeader + PointBytes * n];
            Put16(body, 0, strokeId);
            body[2] = colour;
            Put16(body, 3, start);
            body[5] = (byte)n;
            var first = points[start];
            int bx = Mathf.RoundToInt(first.x * 100f);
            int by = Mathf.RoundToInt(first.y * 100f);
            int bz = Mathf.RoundToInt(first.z * 100f);
            PutS16(body, 6, bx);
            PutS16(body, 8, by);
            PutS16(body, 10, bz);
            int o = PointsHeader;
            for (int i = 0; i < n; i++)
            {
                var p = points[start + i];
                PutS16(body, o, Mathf.RoundToInt(p.x * 1000f) - bx * 10);
                PutS16(body, o + 2, Mathf.RoundToInt(p.y * 1000f) - by * 10);
                PutS16(body, o + 4, Mathf.RoundToInt(p.z * 1000f) - bz * 10);
                body[o + 6] = (byte)Mathf.Clamp(Mathf.RoundToInt(radii[start + i] * 4000f), 1, 255);
                o += PointBytes;
            }

            return body;
        }

        /// <summary>Reads a points header; false when the body is short or damaged.</summary>
        public static bool DecodePoints(byte[] body, out int strokeId, out byte colour, out int start, out int n)
        {
            strokeId = start = n = 0;
            colour = 0;
            if (body == null || body.Length < PointsHeader)
            {
                return false;
            }

            strokeId = Get16(body, 0);
            colour = body[2];
            start = Get16(body, 3);
            n = body[5];
            return strokeId != 0 && body.Length >= PointsHeader + PointBytes * n;
        }

        /// <summary>Point i of a points body (room-local, metres).</summary>
        public static Vector3 PointAt(byte[] body, int i)
        {
            int o = PointsHeader + PointBytes * i;
            int bx = GetS16(body, 6) * 10, by = GetS16(body, 8) * 10, bz = GetS16(body, 10) * 10;
            return new Vector3((bx + GetS16(body, o)) * 0.001f, (by + GetS16(body, o + 2)) * 0.001f, (bz + GetS16(body, o + 4)) * 0.001f);
        }

        public static float RadiusAt(byte[] body, int i)
        {
            return body[PointsHeader + PointBytes * i + 6] * 0.00025f;
        }

        public static Vector3 Quantise(Vector3 p)
        {
            return new Vector3(Mathf.Round(p.x * 1000f) * 0.001f, Mathf.Round(p.y * 1000f) * 0.001f, Mathf.Round(p.z * 1000f) * 0.001f);
        }

        public static float QuantiseRadius(float r)
        {
            return Mathf.Clamp(Mathf.Round(r * 4000f), 1f, 255f) * 0.00025f;
        }

        // ------------------------------------------------------------------ deletes

        public static byte[] EncodeDelete(List<int> ids)
        {
            int n = ids == null ? 0 : Mathf.Min(ids.Count, MaxIdsPerDelete);
            var body = new byte[1 + 2 * n];
            body[0] = (byte)n;
            for (int i = 0; i < n; i++)
            {
                Put16(body, 1 + 2 * i, ids[i]);
            }

            return body;
        }

        // ------------------------------------------------------------------ pen pose

        public static byte[] EncodePen(bool held, Vector3 local, Quaternion rotation)
        {
            var body = new byte[PenBytes];
            body[0] = (byte)(held ? 1 : 0);
            PutS16(body, 1, Mathf.RoundToInt(local.x * 100f));
            PutS16(body, 3, Mathf.RoundToInt(local.y * 100f));
            PutS16(body, 5, Mathf.RoundToInt(local.z * 100f));
            rotation = Quaternion.Normalize(rotation);
            body[7] = (byte)(sbyte)Mathf.RoundToInt(rotation.x * 127f);
            body[8] = (byte)(sbyte)Mathf.RoundToInt(rotation.y * 127f);
            body[9] = (byte)(sbyte)Mathf.RoundToInt(rotation.z * 127f);
            body[10] = (byte)(sbyte)Mathf.RoundToInt(rotation.w * 127f);
            return body;
        }

        public static bool DecodePen(byte[] body, out bool held, out Vector3 local, out Quaternion rotation)
        {
            held = false;
            local = Vector3.zero;
            rotation = Quaternion.identity;
            if (body == null || body.Length < PenBytes)
            {
                return false;
            }

            held = body[0] != 0;
            local = new Vector3(GetS16(body, 1) * 0.01f, GetS16(body, 3) * 0.01f, GetS16(body, 5) * 0.01f);
            var q = new Quaternion((sbyte)body[7] / 127f, (sbyte)body[8] / 127f, (sbyte)body[9] / 127f, (sbyte)body[10] / 127f);
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            rotation = m > 0.01f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : Quaternion.identity;
            return true;
        }

        // ------------------------------------------------------------------ bytes

        public static void Put16(byte[] b, int o, int v)
        {
            b[o] = (byte)(v & 0xFF);
            b[o + 1] = (byte)((v >> 8) & 0xFF);
        }

        public static int Get16(byte[] b, int o)
        {
            return b[o] | (b[o + 1] << 8);
        }

        public static void PutS16(byte[] b, int o, int v)
        {
            v = Mathf.Clamp(v, short.MinValue, short.MaxValue);
            Put16(b, o, v & 0xFFFF);
        }

        public static int GetS16(byte[] b, int o)
        {
            return (short)(b[o] | (b[o + 1] << 8));
        }
    }
}
