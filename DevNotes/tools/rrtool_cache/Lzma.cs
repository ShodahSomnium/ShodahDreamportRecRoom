// Minimal LZMA (LZMA1, Unity bundle blocks: 5-byte props + raw stream, known output size).
// Port of the LZMA SDK reference decoder (LzmaSpec.cpp, public domain).
static class Lzma {
  class RC {
    public byte[] B; public int P; public uint Range = 0xFFFFFFFF, Code;
    public RC(byte[] b, int p) { B = b; P = p; P++; for (int i = 0; i < 4; i++) Code = (Code << 8) | B[P++]; }
    void Norm() { if (Range < (1u << 24)) { Range <<= 8; Code = (Code << 8) | B[P++]; } }
    public uint Direct(int n) {
      uint res = 0;
      do { Range >>= 1; Code -= Range; uint t = 0 - (Code >> 31); Code += Range & t; Norm(); res <<= 1; res += t + 1; } while (--n > 0);
      return res;
    }
    public uint Bit(ushort[] p, int i) {
      uint v = p[i]; uint bound = (Range >> 11) * v; uint s;
      if (Code < bound) { v += ((1u << 11) - v) >> 5; Range = bound; s = 0; }
      else { v -= v >> 5; Code -= bound; Range -= bound; s = 1; }
      p[i] = (ushort)v; Norm(); return s;
    }
    public uint Tree(ushort[] p, int off, int bits) { uint m = 1; for (int i = 0; i < bits; i++) m = (m << 1) + Bit(p, off + (int)m); return m - (1u << bits); }
    public uint Rev(ushort[] p, int off, int bits) { uint m = 1, s = 0; for (int i = 0; i < bits; i++) { uint b = Bit(p, off + (int)m); m <<= 1; m += b; s |= b << i; } return s; }
  }
  static ushort[] Probs(int n) { var a = new ushort[n]; Array.Fill(a, (ushort)1024); return a; }
  class Len {
    ushort[] c = Probs(2), low = Probs(16 << 3), mid = Probs(16 << 3), high = Probs(256);
    public uint Dec(RC rc, int ps) {
      if (rc.Bit(c, 0) == 0) return rc.Tree(low, ps << 3, 3);
      if (rc.Bit(c, 1) == 0) return 8 + rc.Tree(mid, ps << 3, 3);
      return 16 + rc.Tree(high, 0, 8);
    }
  }
  public static void Decode(byte[] src, int off, byte[] dst, int dstOff, int outSize) {
    int d = src[off]; int lc = d % 9; d /= 9; int lp = d % 5; int pb = d / 5;
    var rc = new RC(src, off + 5);
    var lit = Probs(0x300 << (lc + lp));
    var posSlot = Probs(4 << 6); var posDec = Probs(115); var align = Probs(16);
    var isMatch = Probs(12 << 4); var isRep = Probs(12); var isRepG0 = Probs(12); var isRepG1 = Probs(12); var isRepG2 = Probs(12); var isRep0Long = Probs(12 << 4);
    var lenD = new Len(); var repLenD = new Len();
    uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0; int state = 0;
    int pos = dstOff, end = dstOff + outSize;
    while (pos < end) {
      int total = pos - dstOff;
      int ps = total & ((1 << pb) - 1);
      if (rc.Bit(isMatch, (state << 4) + ps) == 0) {
        int prev = total > 0 ? dst[pos - 1] : 0;
        int ls = ((total & ((1 << lp) - 1)) << lc) + (prev >> (8 - lc));
        int bo = 0x300 * ls; uint sym = 1;
        if (state >= 7) {
          uint mb = dst[pos - (int)rep0 - 1];
          do { uint mbit = (mb >> 7) & 1; mb <<= 1; uint bit = rc.Bit(lit, bo + (int)(((1 + mbit) << 8) + sym)); sym = (sym << 1) | bit; if (mbit != bit) break; } while (sym < 0x100);
        }
        while (sym < 0x100) sym = (sym << 1) | rc.Bit(lit, bo + (int)sym);
        dst[pos++] = (byte)(sym - 0x100);
        state = state < 4 ? 0 : state < 10 ? state - 3 : state - 6;
        continue;
      }
      uint len;
      if (rc.Bit(isRep, state) != 0) {
        if (rc.Bit(isRepG0, state) == 0) {
          if (rc.Bit(isRep0Long, (state << 4) + ps) == 0) { state = state < 7 ? 9 : 11; dst[pos] = dst[pos - (int)rep0 - 1]; pos++; continue; }
        } else {
          uint dist;
          if (rc.Bit(isRepG1, state) == 0) dist = rep1;
          else { if (rc.Bit(isRepG2, state) == 0) dist = rep2; else { dist = rep3; rep3 = rep2; } rep2 = rep1; }
          rep1 = rep0; rep0 = dist;
        }
        len = repLenD.Dec(rc, ps); state = state < 7 ? 8 : 11;
      } else {
        rep3 = rep2; rep2 = rep1; rep1 = rep0;
        len = lenD.Dec(rc, ps); state = state < 7 ? 7 : 10;
        int lstate = (int)Math.Min(len, 3u);
        uint slot = rc.Tree(posSlot, lstate << 6, 6);
        if (slot < 4) rep0 = slot;
        else {
          int nd = (int)((slot >> 1) - 1); uint dist = (2 | (slot & 1)) << nd;
          if (slot < 14) dist += rc.Rev(posDec, (int)(dist - slot), nd);
          else { dist += rc.Direct(nd - 4) << 4; dist += rc.Rev(align, 0, 4); }
          rep0 = dist;
        }
        if (rep0 == 0xFFFFFFFF) break;
      }
      len += 2;
      int n = (int)Math.Min(len, (uint)(end - pos));
      for (int i = 0; i < n; i++) { dst[pos] = dst[pos - (int)rep0 - 1]; pos++; }
    }
  }
}
