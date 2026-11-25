using System;
using System.Collections.Generic;
using System.IO;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>
    /// One UnityFS asset bundle opened for reading its directory and, on demand, the bytes of one
    /// of the files inside it.
    ///
    /// Layout (big-endian), as Rec Room's Addressables bundles carry it:
    ///
    ///     "UnityFS\0"  u32 format (8)  "5.x.x\0"  engine version ("0.0.0\0" - stripped)
    ///     i64 total size  u32 compressed info size  u32 uncompressed info size  u32 flags
    ///     [pad to 16]  blocks-and-directory info (LZ4)  [pad to 16 when flag 0x200]  data blocks
    ///
    /// Flags: low six bits compression (3 = LZ4HC), 0x40 info combined, 0x80 info at the end of
    /// the file, 0x200 data padded to 16. The info is a 16-byte hash, the block table (u32
    /// uncompressed, u32 compressed, u16 flags per block) and the directory (i64 offset, i64 size,
    /// u32 flags, name per file). A bundle holds one serialized file ("CAB-" plus a hash) and,
    /// when it has textures or meshes streamed from disk, a ".resS" and/or a ".resource" file.
    ///
    /// Only the serialized file is ever wanted here - the names, the hierarchy, the configs - and
    /// it is usually a small part of the bundle, so only the blocks it spans are decompressed.
    /// Texture and audio payloads are left to Unity's own loader.
    /// </summary>
    public sealed class RrUnityFs
    {
        public struct Node
        {
            public long Offset;
            public long Size;
            public uint Flags;
            public string Name;
        }

        private struct Block
        {
            public uint Uncompressed;
            public uint Compressed;
            public ushort Flags;
            public long UncompressedStart;
            public long FileStart;
        }

        public readonly string Path;
        public readonly List<Node> Nodes = new List<Node>();
        private readonly List<Block> _blocks = new List<Block>();

        public RrUnityFs(string path)
        {
            Path = path;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            {
                var head = new byte[Math.Min(fs.Length, 256)];
                ReadExactly(fs, head, 0, head.Length);
                var r = new RrReader(head, 0, true);
                string signature = r.CString();
                if (signature != "UnityFS")
                {
                    throw new InvalidDataException("not a UnityFS bundle: " + signature);
                }

                uint format = r.U32();
                r.CString();
                r.CString();
                r.I64();
                int compressedInfo = (int)r.U32();
                int uncompressedInfo = (int)r.U32();
                uint flags = r.U32();
                if (format >= 7)
                {
                    r.Align(16);
                }

                long infoStart = (flags & 0x80) != 0 ? fs.Length - compressedInfo : r.Position;
                var packed = new byte[compressedInfo];
                fs.Position = infoStart;
                ReadExactly(fs, packed, 0, compressedInfo);
                var info = new byte[uncompressedInfo];
                int infoCompression = (int)(flags & 0x3f);
                if (infoCompression == 2 || infoCompression == 3)
                {
                    RrLz4.Decode(packed, 0, compressedInfo, info, 0, uncompressedInfo);
                }
                else if (infoCompression == 0)
                {
                    Buffer.BlockCopy(packed, 0, info, 0, uncompressedInfo);
                }
                else
                {
                    throw new InvalidDataException("unsupported info compression " + infoCompression);
                }

                long dataStart = r.Position;
                if ((flags & 0x80) == 0)
                {
                    dataStart += compressedInfo;
                }

                if ((flags & 0x200) != 0)
                {
                    dataStart = (dataStart + 15) / 16 * 16;
                }

                var q = new RrReader(info, 16, true);
                int blockCount = q.I32();
                long u = 0;
                long c = dataStart;
                for (int i = 0; i < blockCount; i++)
                {
                    var b = new Block { Uncompressed = q.U32(), Compressed = q.U32(), Flags = q.U16(), UncompressedStart = u, FileStart = c };
                    _blocks.Add(b);
                    u += b.Uncompressed;
                    c += b.Compressed;
                }

                int nodeCount = q.I32();
                for (int i = 0; i < nodeCount; i++)
                {
                    Nodes.Add(new Node { Offset = q.I64(), Size = q.I64(), Flags = q.U32(), Name = q.CString() });
                }
            }
        }

        /// <summary>The serialized file of the bundle: the node that is neither .resS nor .resource.</summary>
        public int SerializedNodeIndex
        {
            get
            {
                for (int i = 0; i < Nodes.Count; i++)
                {
                    string n = Nodes[i].Name;
                    if (!n.EndsWith(".resS", StringComparison.Ordinal) && !n.EndsWith(".resource", StringComparison.Ordinal))
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        /// <summary>Reads the whole of one node, decompressing only the blocks it spans.</summary>
        public byte[] ReadNode(int index)
        {
            var node = Nodes[index];
            var result = new byte[node.Size];
            long want0 = node.Offset;
            long want1 = node.Offset + node.Size;
            byte[] packed = null;
            byte[] unpacked = null;
            using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
            {
                foreach (var b in _blocks)
                {
                    long b0 = b.UncompressedStart;
                    long b1 = b0 + b.Uncompressed;
                    if (b1 <= want0 || b0 >= want1)
                    {
                        continue;
                    }

                    if (packed == null || packed.Length < b.Compressed)
                    {
                        packed = new byte[b.Compressed];
                    }

                    if (unpacked == null || unpacked.Length < b.Uncompressed)
                    {
                        unpacked = new byte[b.Uncompressed];
                    }

                    fs.Position = b.FileStart;
                    ReadExactly(fs, packed, 0, (int)b.Compressed);
                    int compression = b.Flags & 0x3f;
                    if (compression == 0)
                    {
                        Buffer.BlockCopy(packed, 0, unpacked, 0, (int)b.Uncompressed);
                    }
                    else if (compression == 2 || compression == 3)
                    {
                        RrLz4.Decode(packed, 0, (int)b.Compressed, unpacked, 0, (int)b.Uncompressed);
                    }
                    else
                    {
                        throw new InvalidDataException("unsupported block compression " + compression);
                    }

                    long from = Math.Max(b0, want0);
                    long to = Math.Min(b1, want1);
                    Buffer.BlockCopy(unpacked, (int)(from - b0), result, (int)(from - want0), (int)(to - from));
                }
            }

            return result;
        }

        /// <summary>
        /// Reads count bytes of one node from offset (clamped to the node), decompressing only the blocks that
        /// range spans: the metadata of a big serialized file, or a few of its objects, without the rest.
        /// Throws InvalidDataException for block compressions other than none and LZ4 (LZMA bundles).
        /// </summary>
        public byte[] ReadNodeRange(int index, long offset, long count)
        {
            var node = Nodes[index];
            offset = Math.Max(0, Math.Min(offset, node.Size));
            count = Math.Max(0, Math.Min(count, node.Size - offset));
            var result = new byte[count];
            long want0 = node.Offset + offset;
            long want1 = want0 + count;
            byte[] packed = null;
            byte[] unpacked = null;
            using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
            {
                foreach (var b in _blocks)
                {
                    long b0 = b.UncompressedStart;
                    long b1 = b0 + b.Uncompressed;
                    if (b1 <= want0 || b0 >= want1)
                    {
                        continue;
                    }

                    if (packed == null || packed.Length < b.Compressed)
                    {
                        packed = new byte[b.Compressed];
                    }

                    if (unpacked == null || unpacked.Length < b.Uncompressed)
                    {
                        unpacked = new byte[b.Uncompressed];
                    }

                    fs.Position = b.FileStart;
                    ReadExactly(fs, packed, 0, (int)b.Compressed);
                    int compression = b.Flags & 0x3f;
                    if (compression == 0)
                    {
                        Buffer.BlockCopy(packed, 0, unpacked, 0, (int)b.Uncompressed);
                    }
                    else if (compression == 2 || compression == 3)
                    {
                        RrLz4.Decode(packed, 0, (int)b.Compressed, unpacked, 0, (int)b.Uncompressed);
                    }
                    else
                    {
                        throw new InvalidDataException("unsupported block compression " + compression);
                    }

                    long from = Math.Max(b0, want0);
                    long to = Math.Min(b1, want1);
                    Buffer.BlockCopy(unpacked, (int)(from - b0), result, (int)(from - want0), (int)(to - from));
                }
            }

            return result;
        }

        private static void ReadExactly(Stream s, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int n = s.Read(buffer, offset, count);
                if (n <= 0)
                {
                    throw new EndOfStreamException();
                }

                offset += n;
                count -= n;
            }
        }
    }
}
