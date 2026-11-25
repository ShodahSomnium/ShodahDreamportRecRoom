using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>
    /// The maker rooms' byte formats: a small writer/reader, CRC-32, the compressed blob ("RRZ1" + raw length + raw
    /// deflate, the same DeflateStream the scene bank already inflates in the client), the 128-bit room id as bytes / four
    /// ints / a 32-bit hash, and the encodings of objects, edits, settings and catalogue entries that travel in the events
    /// of <see cref="RrMakerEvents"/>. Every event payload stays under <see cref="Net.RrEventWire.MaxPayload"/>; larger
    /// messages are split by <see cref="Split"/> and joined by <see cref="RrMakerJoiner"/>.
    /// </summary>
    public static class RrMakerWire
    {
        /// <summary>Largest payload of one event (RrEventWire.MaxPayload is 471; a little headroom).</summary>
        public const int MaxEvent = 460;

        /// <summary>Data bytes in one chunk event (26): MaxEvent minus the chunk header.</summary>
        public const int ChunkData = MaxEvent - ChunkHeader;

        /// <summary>[u16 target][u8 kind][u16 transfer][u16 index][u16 count][i32 total][u32 crc].</summary>
        public const int ChunkHeader = 2 + 1 + 2 + 2 + 2 + 4 + 4;

        // ---- edit ops (event 27) ----
        public const byte OpUpsert = 1;
        public const byte OpTransform = 2;
        public const byte OpDelete = 3;
        public const byte OpPart = 4;

        // ---- meta ops (event 29) ----
        public const byte MetaSettings = 1;
        public const byte MetaSaved = 2;

        // ---- chunk kinds (events 25 / 26) ----
        public const byte KindFile = 1;
        public const byte KindState = 2;

        private static readonly uint[] CrcTable = MakeCrcTable();

        // ------------------------------------------------------------------ ids

        /// <summary>A new room id: 32 lower-case hex characters.</summary>
        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>A 32-character hex id (anything else is not a room id).</summary>
        public static bool IsId(string id)
        {
            if (id == null || id.Length != 32)
            {
                return false;
            }

            for (int i = 0; i < 32; i++)
            {
                if (Hex(id[i]) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static byte[] IdBytes(string id)
        {
            var b = new byte[16];
            if (!IsId(id))
            {
                return b;
            }

            for (int i = 0; i < 16; i++)
            {
                b[i] = (byte)((Hex(id[i * 2]) << 4) | Hex(id[i * 2 + 1]));
            }

            return b;
        }

        public static string IdFrom(byte[] b, int offset)
        {
            if (b == null || offset < 0 || offset + 16 > b.Length)
            {
                return "";
            }

            var sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
            {
                sb.Append(b[offset + i].ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>The id as four ints (instance state custom ints 0-3).</summary>
        public static void IdInts(string id, int[] into)
        {
            var b = IdBytes(id);
            for (int i = 0; i < 4; i++)
            {
                into[i] = b[i * 4] | (b[i * 4 + 1] << 8) | (b[i * 4 + 2] << 16) | (b[i * 4 + 3] << 24);
            }
        }

        public static string IdFromInts(int a, int b, int c, int d)
        {
            if ((a | b | c | d) == 0)
            {
                return "";
            }

            var bytes = new byte[16];
            int[] v = { a, b, c, d };
            for (int i = 0; i < 4; i++)
            {
                bytes[i * 4] = (byte)(v[i] & 0xFF);
                bytes[i * 4 + 1] = (byte)((v[i] >> 8) & 0xFF);
                bytes[i * 4 + 2] = (byte)((v[i] >> 16) & 0xFF);
                bytes[i * 4 + 3] = (byte)((v[i] >> 24) & 0xFF);
            }

            return IdFrom(bytes, 0);
        }

        /// <summary>The 32-bit hash a player's presence carries for the maker room they are in (0 = none; never 0 for an id).</summary>
        public static int RoomHash(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return 0;
            }

            int h = Net.RrWire.Fnv1a(id);
            return h == 0 ? 1 : h;
        }

        private static int Hex(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }

            return -1;
        }

        // ------------------------------------------------------------------ time

        /// <summary>UTC unix seconds now.</summary>
        public static long Now()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        /// <summary>"just now", "5 min ago", "3 h ago", "2 days ago".</summary>
        public static string Ago(long unix)
        {
            if (unix <= 0)
            {
                return "never";
            }

            long s = Math.Max(0L, Now() - unix);
            if (s < 45)
            {
                return "just now";
            }

            if (s < 3600)
            {
                return Math.Max(1L, s / 60) + " min ago";
            }

            if (s < 86400)
            {
                return s / 3600 + " h ago";
            }

            long d = s / 86400;
            return d == 1 ? "yesterday" : d + " days ago";
        }

        // ------------------------------------------------------------------ CRC-32 and compression

        private static uint[] MakeCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }

                t[n] = c;
            }

            return t;
        }

        public static uint Crc32(byte[] data)
        {
            if (data == null)
            {
                return 0;
            }

            uint c = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
            {
                c = CrcTable[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            }

            return c ^ 0xFFFFFFFFu;
        }

        /// <summary>"RRZ1" + u32 raw length + raw deflate of the bytes ("RRZ0" + the bytes when deflate is not available).</summary>
        public static byte[] Pack(byte[] raw)
        {
            raw = raw ?? new byte[0];
            try
            {
                using (var ms = new MemoryStream())
                {
                    ms.WriteByte((byte)'R');
                    ms.WriteByte((byte)'R');
                    ms.WriteByte((byte)'Z');
                    ms.WriteByte((byte)'1');
                    int n = raw.Length;
                    ms.WriteByte((byte)(n & 0xFF));
                    ms.WriteByte((byte)((n >> 8) & 0xFF));
                    ms.WriteByte((byte)((n >> 16) & 0xFF));
                    ms.WriteByte((byte)((n >> 24) & 0xFF));
                    using (var z = new DeflateStream(ms, CompressionMode.Compress, true))
                    {
                        z.Write(raw, 0, raw.Length);
                    }

                    return ms.ToArray();
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("MakerRooms", "deflate", "compression not available (" + e.GetType().Name + ": " + e.Message + "): rooms travel uncompressed");
                var plain = new byte[raw.Length + 4];
                plain[0] = (byte)'R';
                plain[1] = (byte)'R';
                plain[2] = (byte)'Z';
                plain[3] = (byte)'0';
                Buffer.BlockCopy(raw, 0, plain, 4, raw.Length);
                return plain;
            }
        }

        /// <summary>The bytes of a <see cref="Pack"/> blob, or null when it is damaged.</summary>
        public static byte[] Unpack(byte[] packed)
        {
            if (packed == null || packed.Length < 4 || packed[0] != 'R' || packed[1] != 'R' || packed[2] != 'Z')
            {
                return null;
            }

            try
            {
                if (packed[3] == '0')
                {
                    var plain = new byte[packed.Length - 4];
                    Buffer.BlockCopy(packed, 4, plain, 0, plain.Length);
                    return plain;
                }

                if (packed[3] != '1' || packed.Length < 8)
                {
                    return null;
                }

                int size = packed[4] | (packed[5] << 8) | (packed[6] << 16) | (packed[7] << 24);
                if (size < 0 || size > 64 << 20)
                {
                    return null;
                }

                var bytes = new byte[size];
                using (var source = new MemoryStream(packed, 8, packed.Length - 8, false))
                using (var z = new DeflateStream(source, CompressionMode.Decompress))
                {
                    int got = 0;
                    while (got < size)
                    {
                        int n = z.Read(bytes, got, size - got);
                        if (n <= 0)
                        {
                            break;
                        }

                        got += n;
                    }

                    return got == size ? bytes : null;
                }
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "a packed room did not inflate: " + e.GetType().Name + ": " + e.Message);
                return null;
            }
        }

        public static byte[] Utf8(string s)
        {
            return new UTF8Encoding(false).GetBytes(s ?? "");
        }

        public static string FromUtf8(byte[] b)
        {
            return b == null ? "" : new UTF8Encoding(false).GetString(b);
        }

        // ------------------------------------------------------------------ objects

        private const int FlagFrozen = 1 << 2;
        private const int FlagGrabbable = 1 << 3;
        private const int FlagPoints = 1 << 4;
        private const int FlagText = 1 << 5;

        /// <summary>A whole object with its revision: [u32 id][u32 rev][u16 author][u8 kind][u8 flags][u8 material][str8 asset][pos][rot][scale][rgba][f32 width][points][text].</summary>
        public static void WriteObject(RrMakerWriter w, RrMakerObject o, uint rev, int author)
        {
            w.U32(o.Id);
            w.U32(rev);
            w.U16(author);
            w.U8((byte)o.Kind);
            int flags = ((int)o.Physics & 3) | (o.Frozen ? FlagFrozen : 0) | (o.Grabbable ? FlagGrabbable : 0) |
                        (o.Points != null && o.Points.Count > 0 ? FlagPoints : 0) | (!string.IsNullOrEmpty(o.Text) ? FlagText : 0);
            w.U8((byte)flags);
            w.U8(o.Material);
            w.Str8(o.Asset);
            w.Vec3(o.Position);
            w.Quat(o.Rotation);
            w.Vec3(o.Scale);
            w.U8(o.Color.r);
            w.U8(o.Color.g);
            w.U8(o.Color.b);
            w.U8(o.Color.a);
            w.F32(o.Width);
            if ((flags & FlagPoints) != 0)
            {
                int n = Math.Min(o.Points.Count, 4000);
                w.U16(n);
                for (int i = 0; i < n; i++)
                {
                    w.Vec3(o.Points[i]);
                }
            }

            if ((flags & FlagText) != 0)
            {
                w.Str16(o.Text);
            }
        }

        public static RrMakerObject ReadObject(RrMakerReader r, out uint rev, out int author)
        {
            var o = new RrMakerObject();
            o.Id = r.U32();
            rev = r.U32();
            author = r.U16();
            o.Kind = (RrMakerKind)r.U8();
            int flags = r.U8();
            o.Material = r.U8();
            o.Physics = (RrMakerPhysics)(flags & 3);
            o.Frozen = (flags & FlagFrozen) != 0;
            o.Grabbable = (flags & FlagGrabbable) != 0;
            o.Asset = r.Str8();
            o.Position = r.Vec3();
            o.Rotation = r.Quat();
            o.Scale = r.Vec3();
            o.Color = new Color32(r.U8(), r.U8(), r.U8(), r.U8());
            o.Width = r.F32();
            if ((flags & FlagPoints) != 0)
            {
                int n = r.U16();
                o.Points = new List<Vector3>(n);
                for (int i = 0; i < n && r.Ok; i++)
                {
                    o.Points.Add(r.Vec3());
                }
            }

            if ((flags & FlagText) != 0)
            {
                o.Text = r.Str16();
            }

            return o;
        }

        /// <summary>The same object as far as the visuals go (kind, asset, colour, material, physics, points, width, text).</summary>
        public static bool SameLook(RrMakerObject a, RrMakerObject b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.Kind != b.Kind || a.Asset != b.Asset || !a.Color.Equals(b.Color) || a.Material != b.Material || a.Physics != b.Physics ||
                a.Grabbable != b.Grabbable || Math.Abs(a.Width - b.Width) > 1e-6f || (a.Text ?? "") != (b.Text ?? ""))
            {
                return false;
            }

            int na = a.Points != null ? a.Points.Count : 0;
            int nb = b.Points != null ? b.Points.Count : 0;
            if (na != nb)
            {
                return false;
            }

            for (int i = 0; i < na; i++)
            {
                if (a.Points[i] != b.Points[i])
                {
                    return false;
                }
            }

            // A light's range / intensity / angle live in Scale.
            return a.Kind != RrMakerKind.Light || a.Scale == b.Scale;
        }

        // ------------------------------------------------------------------ splitting large messages

        /// <summary>
        /// Splits a message (an edit or meta payload starting with its op byte) into event payloads: the message itself when
        /// it fits, else OpPart pieces [OpPart][u16 msg][u8 index][u8 count][bytes].
        /// </summary>
        public static List<byte[]> Split(byte[] message, ushort msgId)
        {
            var list = new List<byte[]>();
            if (message.Length <= MaxEvent)
            {
                list.Add(message);
                return list;
            }

            const int head = 5;
            int per = MaxEvent - head;
            int count = (message.Length + per - 1) / per;
            if (count > 255)
            {
                RrLog.Warn("MakerRooms", "a message of " + message.Length + " bytes is too large to send: dropped");
                return list;
            }

            for (int i = 0; i < count; i++)
            {
                int at = i * per;
                int n = Math.Min(per, message.Length - at);
                var part = new byte[head + n];
                part[0] = OpPart;
                part[1] = (byte)(msgId & 0xFF);
                part[2] = (byte)(msgId >> 8);
                part[3] = (byte)i;
                part[4] = (byte)count;
                Buffer.BlockCopy(message, at, part, head, n);
                list.Add(part);
            }

            return list;
        }

        // ------------------------------------------------------------------ settings

        /// <summary>[str8 name][u8 flags: 1 public, 2 everyone can build][u8 max players][u8 n][i32 co-owner]*n.</summary>
        public static void WriteSettings(RrMakerWriter w, RrMakerRoomInfo info)
        {
            w.Str8(info.Name);
            w.U8((byte)((info.Public ? 1 : 0) | (info.EveryoneCanBuild ? 2 : 0)));
            w.U8((byte)Mathf.Clamp(info.MaxPlayers, 1, 40));
            int n = info.CoOwners != null ? Math.Min(info.CoOwners.Count, 32) : 0;
            w.U8((byte)n);
            for (int i = 0; i < n; i++)
            {
                w.I32(info.CoOwners[i]);
            }
        }

        public static void ReadSettings(RrMakerReader r, RrMakerRoomInfo into)
        {
            string name = r.Str8();
            int flags = r.U8();
            int max = r.U8();
            int n = r.U8();
            var co = new List<int>(n);
            for (int i = 0; i < n && r.Ok; i++)
            {
                co.Add(r.I32());
            }

            if (!r.Ok)
            {
                return;
            }

            into.Name = RrMakerRooms.CleanName(name);
            into.Public = (flags & 1) != 0;
            into.EveryoneCanBuild = (flags & 2) != 0;
            into.MaxPlayers = Mathf.Clamp(max, 1, 40);
            into.CoOwners = co;
        }
    }

    /// <summary>Little-endian writer for the maker rooms' payloads.</summary>
    public sealed class RrMakerWriter
    {
        private readonly List<byte> _b = new List<byte>(256);

        public int Length
        {
            get { return _b.Count; }
        }

        public byte[] ToArray()
        {
            return _b.ToArray();
        }

        public void U8(byte v)
        {
            _b.Add(v);
        }

        public void U16(int v)
        {
            _b.Add((byte)(v & 0xFF));
            _b.Add((byte)((v >> 8) & 0xFF));
        }

        public void I32(int v)
        {
            _b.Add((byte)(v & 0xFF));
            _b.Add((byte)((v >> 8) & 0xFF));
            _b.Add((byte)((v >> 16) & 0xFF));
            _b.Add((byte)((v >> 24) & 0xFF));
        }

        public void U32(uint v)
        {
            I32(unchecked((int)v));
        }

        public void I64(long v)
        {
            I32(unchecked((int)(v & 0xFFFFFFFF)));
            I32(unchecked((int)(v >> 32)));
        }

        public void F32(float v)
        {
            var bytes = BitConverter.GetBytes(v);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            _b.AddRange(bytes);
        }

        public void Vec3(Vector3 v)
        {
            F32(v.x);
            F32(v.y);
            F32(v.z);
        }

        public void Quat(Quaternion q)
        {
            F32(q.x);
            F32(q.y);
            F32(q.z);
            F32(q.w);
        }

        public void Bytes(byte[] b)
        {
            if (b != null)
            {
                _b.AddRange(b);
            }
        }

        /// <summary>u8 length + UTF-8 (cut to 255 bytes on a character boundary).</summary>
        public void Str8(string s)
        {
            var bytes = Cut(s, 255);
            _b.Add((byte)bytes.Length);
            _b.AddRange(bytes);
        }

        /// <summary>u16 length + UTF-8 (cut to 4000 bytes).</summary>
        public void Str16(string s)
        {
            var bytes = Cut(s, 4000);
            U16(bytes.Length);
            _b.AddRange(bytes);
        }

        private static byte[] Cut(string s, int max)
        {
            var enc = new UTF8Encoding(false);
            s = s ?? "";
            var bytes = enc.GetBytes(s);
            while (bytes.Length > max && s.Length > 0)
            {
                s = s.Substring(0, s.Length - 1);
                bytes = enc.GetBytes(s);
            }

            return bytes;
        }
    }

    /// <summary>Little-endian reader; <see cref="Ok"/> turns false (and reads return 0/"") once past the end.</summary>
    public sealed class RrMakerReader
    {
        private readonly byte[] _b;
        private int _p;

        public bool Ok { get; private set; }

        public RrMakerReader(byte[] bytes, int offset = 0)
        {
            _b = bytes ?? new byte[0];
            _p = offset;
            Ok = offset <= _b.Length;
        }

        public int Position
        {
            get { return _p; }
        }

        public int Left
        {
            get { return Math.Max(0, _b.Length - _p); }
        }

        private bool Need(int n)
        {
            if (!Ok || _p + n > _b.Length)
            {
                Ok = false;
                return false;
            }

            return true;
        }

        public byte U8()
        {
            if (!Need(1))
            {
                return 0;
            }

            return _b[_p++];
        }

        public int U16()
        {
            if (!Need(2))
            {
                return 0;
            }

            int v = _b[_p] | (_b[_p + 1] << 8);
            _p += 2;
            return v;
        }

        public int I32()
        {
            if (!Need(4))
            {
                return 0;
            }

            int v = _b[_p] | (_b[_p + 1] << 8) | (_b[_p + 2] << 16) | (_b[_p + 3] << 24);
            _p += 4;
            return v;
        }

        public uint U32()
        {
            return unchecked((uint)I32());
        }

        public long I64()
        {
            long lo = (uint)I32();
            long hi = I32();
            return lo | (hi << 32);
        }

        public float F32()
        {
            if (!Need(4))
            {
                return 0f;
            }

            float v;
            if (BitConverter.IsLittleEndian)
            {
                v = BitConverter.ToSingle(_b, _p);
            }
            else
            {
                var tmp = new byte[4];
                Buffer.BlockCopy(_b, _p, tmp, 0, 4);
                Array.Reverse(tmp);
                v = BitConverter.ToSingle(tmp, 0);
            }

            _p += 4;
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }

        public Vector3 Vec3()
        {
            return new Vector3(F32(), F32(), F32());
        }

        public Quaternion Quat()
        {
            var q = new Quaternion(F32(), F32(), F32(), F32());
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (m < 1e-4f)
            {
                return Quaternion.identity;
            }

            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        public byte[] Bytes(int n)
        {
            if (n < 0 || !Need(n))
            {
                return new byte[0];
            }

            var b = new byte[n];
            Buffer.BlockCopy(_b, _p, b, 0, n);
            _p += n;
            return b;
        }

        public byte[] Rest()
        {
            return Bytes(Left);
        }

        public string Str8()
        {
            int n = U8();
            return Text(n);
        }

        public string Str16()
        {
            int n = U16();
            return Text(n);
        }

        private string Text(int n)
        {
            if (!Need(n))
            {
                return "";
            }

            string s = new UTF8Encoding(false).GetString(_b, _p, n);
            _p += n;
            return s;
        }
    }

    /// <summary>Joins OpPart pieces back into whole messages, per sender and message id.</summary>
    public sealed class RrMakerJoiner
    {
        private sealed class Partial
        {
            public byte[][] Parts;
            public int Have;
            public float Started;
        }

        private readonly Dictionary<long, Partial> _open = new Dictionary<long, Partial>();

        /// <summary>A piece [OpPart][u16 msg][u8 index][u8 count][bytes] from sender: the whole message once complete, else null.</summary>
        public byte[] Add(int sender, byte[] piece)
        {
            if (piece == null || piece.Length < 5)
            {
                return null;
            }

            int msg = piece[1] | (piece[2] << 8);
            int index = piece[3];
            int count = piece[4];
            if (count <= 0 || index >= count)
            {
                return null;
            }

            long key = ((long)sender << 16) | (uint)msg;
            Partial p;
            if (!_open.TryGetValue(key, out p) || p.Parts.Length != count)
            {
                p = new Partial { Parts = new byte[count][], Started = Time.realtimeSinceStartup };
                _open[key] = p;
            }

            if (p.Parts[index] == null)
            {
                var data = new byte[piece.Length - 5];
                Buffer.BlockCopy(piece, 5, data, 0, data.Length);
                p.Parts[index] = data;
                p.Have++;
            }

            if (p.Have < count)
            {
                return null;
            }

            _open.Remove(key);
            int total = 0;
            foreach (var part in p.Parts)
            {
                total += part.Length;
            }

            var whole = new byte[total];
            int at = 0;
            foreach (var part in p.Parts)
            {
                Buffer.BlockCopy(part, 0, whole, at, part.Length);
                at += part.Length;
            }

            return whole;
        }

        /// <summary>Drops pieces older than 30 s (a sender that left mid-message).</summary>
        public void Expire()
        {
            if (_open.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            var dead = new List<long>();
            foreach (var kv in _open)
            {
                if (now - kv.Value.Started > 30f)
                {
                    dead.Add(kv.Key);
                }
            }

            foreach (var k in dead)
            {
                _open.Remove(k);
            }
        }

        public void Clear()
        {
            _open.Clear();
        }
    }
}
