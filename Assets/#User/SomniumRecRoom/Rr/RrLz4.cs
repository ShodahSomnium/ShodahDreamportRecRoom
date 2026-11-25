using System;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>
    /// LZ4 block decompression, the only compression Rec Room's Addressables bundles use.
    ///
    /// Every bundle measured (all 2,787 of them in the build of 2026-09-08) has flags 0x243:
    /// LZ4HC for the blocks-and-directory info and for every data block. LZ4HC is an encoder
    /// setting only; the stream is ordinary LZ4 and decodes with the ordinary block decoder.
    ///
    /// A sequence is a token byte (high nibble literal count, low nibble match length - 4), the
    /// literals, a two-byte little-endian back offset, and the match; a nibble of 15 continues
    /// in following bytes of 255. The last sequence has literals and no match.
    /// </summary>
    public static class RrLz4
    {
        /// <summary>Decodes one block; returns the number of bytes written.</summary>
        public static int Decode(byte[] src, int srcOffset, int srcLength, byte[] dst, int dstOffset, int dstLength)
        {
            int s = srcOffset;
            int end = srcOffset + srcLength;
            int d = dstOffset;
            int dEnd = dstOffset + dstLength;

            while (s < end)
            {
                int token = src[s++];
                int literals = token >> 4;
                if (literals == 15)
                {
                    int b;
                    do
                    {
                        b = src[s++];
                        literals += b;
                    }
                    while (b == 255);
                }

                if (d + literals > dEnd || s + literals > end)
                {
                    throw new InvalidOperationException("LZ4: literal run past the end of the block");
                }

                Buffer.BlockCopy(src, s, dst, d, literals);
                s += literals;
                d += literals;
                if (s >= end)
                {
                    break;
                }

                int offset = src[s] | (src[s + 1] << 8);
                s += 2;
                int match = token & 15;
                if (match == 15)
                {
                    int b;
                    do
                    {
                        b = src[s++];
                        match += b;
                    }
                    while (b == 255);
                }

                match += 4;
                int from = d - offset;
                if (offset == 0 || from < dstOffset || d + match > dEnd)
                {
                    throw new InvalidOperationException("LZ4: bad match");
                }

                if (offset >= match)
                {
                    Buffer.BlockCopy(dst, from, dst, d, match);
                    d += match;
                }
                else
                {
                    // Overlapping copy: the match repeats bytes it is itself writing.
                    for (int i = 0; i < match; i++)
                    {
                        dst[d++] = dst[from++];
                    }
                }
            }

            return d - dstOffset;
        }
    }
}
