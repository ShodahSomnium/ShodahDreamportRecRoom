using System;
using System.Text;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>
    /// A cursor over a byte array in either byte order. Unity writes bundle headers and the
    /// serialized-file header big-endian and everything after the header in the file's own
    /// order, which for every Windows file is little-endian; one reader switches between them.
    /// </summary>
    public sealed class RrReader
    {
        public byte[] Bytes;
        public int Position;
        public bool BigEndian;

        public RrReader(byte[] bytes, int position = 0, bool bigEndian = false)
        {
            Bytes = bytes;
            Position = position;
            BigEndian = bigEndian;
        }

        public byte U8()
        {
            return Bytes[Position++];
        }

        public bool Bool()
        {
            return Bytes[Position++] != 0;
        }

        public short I16()
        {
            short v = BigEndian
                ? (short)((Bytes[Position] << 8) | Bytes[Position + 1])
                : (short)(Bytes[Position] | (Bytes[Position + 1] << 8));
            Position += 2;
            return v;
        }

        public ushort U16()
        {
            return (ushort)I16();
        }

        public int I32()
        {
            int p = Position;
            int v = BigEndian
                ? (Bytes[p] << 24) | (Bytes[p + 1] << 16) | (Bytes[p + 2] << 8) | Bytes[p + 3]
                : Bytes[p] | (Bytes[p + 1] << 8) | (Bytes[p + 2] << 16) | (Bytes[p + 3] << 24);
            Position += 4;
            return v;
        }

        public uint U32()
        {
            return (uint)I32();
        }

        public long I64()
        {
            long v = 0;
            if (BigEndian)
            {
                for (int i = 0; i < 8; i++)
                {
                    v = (v << 8) | Bytes[Position + i];
                }
            }
            else
            {
                for (int i = 7; i >= 0; i--)
                {
                    v = (v << 8) | Bytes[Position + i];
                }
            }

            Position += 8;
            return v;
        }

        public float F32()
        {
            float v;
            if (BigEndian)
            {
                var tmp = new byte[4] { Bytes[Position + 3], Bytes[Position + 2], Bytes[Position + 1], Bytes[Position] };
                v = BitConverter.ToSingle(tmp, 0);
            }
            else
            {
                v = BitConverter.ToSingle(Bytes, Position);
            }

            Position += 4;
            return v;
        }

        public double F64()
        {
            double v = BitConverter.Int64BitsToDouble(I64());
            return v;
        }

        /// <summary>A zero-terminated UTF-8 string, as in headers and type-tree string buffers.</summary>
        public string CString()
        {
            int start = Position;
            while (Bytes[Position] != 0)
            {
                Position++;
            }

            string s = Encoding.UTF8.GetString(Bytes, start, Position - start);
            Position++;
            return s;
        }

        /// <summary>A length-prefixed string as serialized objects store them, followed by 4-byte alignment.</summary>
        public string AlignedString()
        {
            int length = I32();
            if (length < 0 || Position + length > Bytes.Length)
            {
                throw new InvalidOperationException("bad string length " + length);
            }

            string s = Encoding.UTF8.GetString(Bytes, Position, length);
            Position += length;
            Align(4);
            return s;
        }

        public byte[] Take(int count)
        {
            var r = new byte[count];
            Buffer.BlockCopy(Bytes, Position, r, 0, count);
            Position += count;
            return r;
        }

        public void Align(int n)
        {
            Position = (Position + n - 1) / n * n;
        }
    }
}
