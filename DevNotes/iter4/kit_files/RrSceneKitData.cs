using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// How an RRSK placement text travels inside the world's code, and how it is read back.
    ///
    /// The world ships no data files, so a scene's placement text is a C# string constant. One generated file per
    /// scene (DevNotes/tools/rrscene/rrsk2cs.sh), for example <c>Places/Scenes/RrScene_GTact1.cs</c>:
    /// <code>
    /// public static class RrScene_GTact1
    /// {
    ///     public const string Name = "GTact1";
    ///     public const int Lines = 2381;                       // RRSK records in the source
    ///     public const bool Compressed = true;                 // RRSZ1 form (see below)
    ///     public static string Text() { return RrSceneKitData.Join(C0, C1, C2); }
    ///     private const string C0 = "...";                    // each chunk at most 16,000 characters
    ///     ...
    /// }
    /// </code>
    /// and a room builds it with <c>yield return RrSceneKit.Build(RrScene_GTact1.Text(), parent, options, done)</c>.
    ///
    /// <b>Why chunks.</b> Very long literals are a known trouble spot for tools that read C# sources (editors, the
    /// upload server's sanitiser, diff viewers); 16,000 characters per constant keeps every literal, and in the
    /// compressed form every source line, short. Concatenation happens once, at build time.
    ///
    /// <b>Why a compressed form.</b> Every string literal of an assembly goes into its #US metadata heap, which
    /// holds at most 16 MB of UTF-16, about 8.3 million characters for the whole SomniumRecRoom assembly (Roslyn
    /// then stops with CS8103). Measured (DevNotes/iter4/kit.md) on the 50 refdata scenes, 100k records: 8.08M
    /// characters as plain RRSK (Rec Royale alone 1.85M, a 2.3 MB .cs file), 7.64M compacted; all of them plain
    /// compile only just (30 MB DLL) and leave no room for anything else, and plain plus compressed copies together
    /// did fail with CS8103. The generator therefore writes, by default:
    ///
    /// - <b>RRSK text</b>, compacted without loss (trailing zeros trimmed, identity rotations and unit scales
    ///   dropped: <see cref="RrSceneData"/> reads both forms), when it is at most 64k characters; or
    /// - <b>RRSZ1</b>: the prefix "RRSZ1:" and base64 of the gzip (deflate) of that text: 2.66M characters for all
    ///   50 scenes (Rec Royale 0.71 MB of .cs), a third of plain. DeflateStream is proven on the Somnium client
    ///   (AoM's .scx reader, ETS's HashFs). Inflating runs on the worker with the parse.
    ///
    /// <see cref="Decode"/> accepts either and returns RRSK text.
    /// </summary>
    public static class RrSceneKitData
    {
        /// <summary>Prefix of the compressed form: "RRSZ1:" then base64 of gzip (or raw deflate) of RRSK text.</summary>
        public const string CompressedPrefix = "RRSZ1:";

        /// <summary>Largest chunk the generator writes (characters of one string constant).</summary>
        public const int MaxChunk = 16000;

        /// <summary>Joins a generated file's chunks.</summary>
        public static string Join(params string[] chunks)
        {
            return chunks == null ? string.Empty : string.Concat(chunks);
        }

        public static bool IsCompressed(string text)
        {
            return text != null && text.StartsWith(CompressedPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// RRSK text from either form. Pure (no Unity calls): safe on the worker. Throws on a damaged payload.
        /// </summary>
        public static string Decode(string text)
        {
            if (!IsCompressed(text))
            {
                return text ?? string.Empty;
            }

            // Base64 of the whole payload; whitespace (line breaks a tool may have added) is skipped by the decoder.
            byte[] packed = Convert.FromBase64String(text.Substring(CompressedPrefix.Length));
            int start = 0;
            int size = -1;
            if (packed.Length >= 18 && packed[0] == 0x1f && packed[1] == 0x8b)
            {
                start = GzipHeaderLength(packed);
                // gzip's trailer ends with the inflated size (mod 2^32): read into one exact buffer instead of a
                // growing reader (measured on the 50 refdata scenes: 58 MB allocated with StreamReader).
                int p = packed.Length - 4;
                size = packed[p] | (packed[p + 1] << 8) | (packed[p + 2] << 16) | (packed[p + 3] << 24);
            }

            using (var source = new MemoryStream(packed, start, packed.Length - start, false))
            using (var deflate = new DeflateStream(source, CompressionMode.Decompress))
            {
                if (size >= 0 && size < 256 << 20)
                {
                    var bytes = new byte[size];
                    int got = 0;
                    while (got < size)
                    {
                        int n = deflate.Read(bytes, got, size - got);
                        if (n <= 0)
                        {
                            break;
                        }

                        got += n;
                    }

                    if (got != size)
                    {
                        throw new InvalidDataException("RRSZ1 payload: inflated " + got + " of " + size + " bytes");
                    }

                    return new UTF8Encoding(false).GetString(bytes);
                }

                using (var reader = new StreamReader(deflate, new UTF8Encoding(false)))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        // RFC 1952: 10 fixed bytes, then optional extra field, name, comment and header CRC (flags byte 3).
        // DeflateStream stops at the end of the deflate data, so the 8-byte trailer needs no handling.
        private static int GzipHeaderLength(byte[] b)
        {
            if (b[2] != 8)
            {
                throw new InvalidDataException("RRSZ1 payload: gzip method " + b[2] + " is not deflate");
            }

            int flags = b[3];
            int p = 10;
            if ((flags & 4) != 0 && p + 2 <= b.Length)
            {
                p += 2 + (b[p] | (b[p + 1] << 8));
            }

            if ((flags & 8) != 0)
            {
                while (p < b.Length && b[p] != 0)
                {
                    p++;
                }

                p++;
            }

            if ((flags & 16) != 0)
            {
                while (p < b.Length && b[p] != 0)
                {
                    p++;
                }

                p++;
            }

            if ((flags & 2) != 0)
            {
                p += 2;
            }

            if (p >= b.Length)
            {
                throw new InvalidDataException("RRSZ1 payload: truncated gzip header");
            }

            return p;
        }
    }
}
