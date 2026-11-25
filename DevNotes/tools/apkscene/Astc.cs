using System.Linq;
// apkscene: an ASTC LDR 2D block decoder (Khronos data format spec, ASTC chapter), enough to read the Android build's
// sky cubemaps (8x8 / 6x6 blocks, LDR endpoint modes) for the lighting table. Output is 8-bit RGBA per texel. Used only
// to measure numbers (sky colour by elevation); no decoded image is written anywhere.
using System;

static class Astc {
  static int Bits(byte[] d, int off, int start, int count) {
    int v = 0;
    for (int i = 0; i < count; i++) { int b = start + i; if (b < 0 || b > 127) continue; v |= ((d[off + (b >> 3)] >> (b & 7)) & 1) << i; }
    return v;
  }

  // weight / colour ranges as (trits, quints, bits)
  static readonly int[] RangeVals = { 2, 3, 4, 5, 6, 8, 10, 12, 16, 20, 24, 32, 40, 48, 64, 80, 96, 128, 160, 192, 256 };
  static void Split(int range, out int trits, out int quints, out int bits) {
    trits = 0; quints = 0; bits = 0;
    switch (range) {
      case 2: bits = 1; break; case 3: trits = 1; break; case 4: bits = 2; break; case 5: quints = 1; break;
      case 6: trits = 1; bits = 1; break; case 8: bits = 3; break; case 10: quints = 1; bits = 1; break; case 12: trits = 1; bits = 2; break;
      case 16: bits = 4; break; case 20: quints = 1; bits = 2; break; case 24: trits = 1; bits = 3; break; case 32: bits = 5; break;
      case 40: quints = 1; bits = 3; break; case 48: trits = 1; bits = 4; break; case 64: bits = 6; break; case 80: quints = 1; bits = 4; break;
      case 96: trits = 1; bits = 5; break; case 128: bits = 7; break; case 160: quints = 1; bits = 5; break; case 192: trits = 1; bits = 6; break;
      case 256: bits = 8; break;
    }
  }
  static int IseBits(int n, int range) {
    Split(range, out int t, out int q, out int b);
    if (t != 0) return (8 * n + 4) / 5 + n * b;
    if (q != 0) return (7 * n + 2) / 3 + n * b;
    return n * b;
  }

  // Integer sequence decode from a bit reader
  static int[] Ise(Func<int, int, int> raw, int start, int n, int range) {
    Split(range, out int trits, out int quints, out int bits);
    var outv = new int[n]; int pos = start;
    // bits past the sequence's own length read as 0 (a last, partial trit or quint group)
    int end = start + IseBits(n, range);
    int read(int s, int c) { int v = raw(s, c); int keep = Math.Max(0, Math.Min(c, end - s)); return keep >= 31 ? v : v & ((1 << keep) - 1); }
    if (trits != 0) {
      for (int blk = 0; blk < n; blk += 5) {
        var m = new int[5]; int T = 0;
        int[] tb = { 2, 2, 1, 2, 1 }; int tshift = 0;
        for (int k = 0; k < 5; k++) {
          m[k] = read(pos, bits); pos += bits;
          T |= read(pos, tb[k]) << tshift; pos += tb[k]; tshift += tb[k];
        }
        var t = DecodeTrits(T);
        for (int k = 0; k < 5 && blk + k < n; k++) outv[blk + k] = (t[k] << bits) | m[k];
      }
    } else if (quints != 0) {
      for (int blk = 0; blk < n; blk += 3) {
        var m = new int[3]; int Q = 0; int[] qb = { 3, 2, 2 }; int qshift = 0;
        for (int k = 0; k < 3; k++) {
          m[k] = read(pos, bits); pos += bits;
          Q |= read(pos, qb[k]) << qshift; pos += qb[k]; qshift += qb[k];
        }
        var q = DecodeQuints(Q);
        for (int k = 0; k < 3 && blk + k < n; k++) outv[blk + k] = (q[k] << bits) | m[k];
      }
    } else {
      for (int i = 0; i < n; i++) { outv[i] = read(pos, bits); pos += bits; }
    }
    return outv;
  }

  static int B(int v, int i) => (v >> i) & 1;
  static int[] DecodeTrits(int T) {
    int C, t4, t3, t2, t1, t0;
    if (((T >> 2) & 7) == 7) { C = (((T >> 5) & 7) << 2) | (T & 3); t4 = 2; t3 = 2; }
    else { C = T & 0x1F; if (((T >> 5) & 3) == 3) { t4 = 2; t3 = B(T, 7); } else { t4 = B(T, 7); t3 = (T >> 5) & 3; } }
    if ((C & 3) == 3) { t2 = 2; t1 = B(C, 4); t0 = (B(C, 3) << 1) | (B(C, 2) & ~B(C, 3) & 1); }
    else if (((C >> 2) & 3) == 3) { t2 = 2; t1 = 2; t0 = C & 3; }
    else { t2 = B(C, 4); t1 = (C >> 2) & 3; t0 = (B(C, 1) << 1) | (B(C, 0) & ~B(C, 1) & 1); }
    return new[] { t0, t1, t2, t3, t4 };
  }
  static int[] DecodeQuints(int Q) {
    int q2, q1, q0, C;
    if (((Q >> 1) & 3) == 3 && ((Q >> 5) & 3) == 0) {
      q2 = (B(Q, 0) << 2) | ((B(Q, 4) & ~B(Q, 0) & 1) << 1) | (B(Q, 3) & ~B(Q, 0) & 1); q1 = 4; q0 = 4;
    } else {
      if (((Q >> 1) & 3) == 3) { q2 = 4; C = (((Q >> 3) & 3) << 3) | ((~(Q >> 5) & 3) << 1) | B(Q, 0); }
      else { q2 = (Q >> 5) & 3; C = Q & 0x1F; }
      if ((C & 7) == 5) { q1 = 4; q0 = (C >> 3) & 3; } else { q1 = (C >> 3) & 3; q0 = C & 7; }
    }
    return new[] { q0, q1, q2 };
  }

  static int UnqColour(int v, int range) {
    Split(range, out int trits, out int quints, out int bits);
    if (trits == 0 && quints == 0) { int x = v << (8 - bits); int r = x; int s = bits; while (s < 8) { r |= x >> s; s += bits; } return r & 0xFF; }
    int m = v & ((1 << bits) - 1); int D = v >> bits;
    if (bits == 0) return trits != 0 ? new[] { 0, 128, 255 }[D] : new[] { 0, 64, 128, 191, 255 }[D];
    int A = (m & 1) != 0 ? 0x1FF : 0; int b = B(m, 1), c = B(m, 2), d = B(m, 3), e = B(m, 4), f = B(m, 5);
    int Bv = 0, Cv = 0;
    if (trits != 0) {
      switch (bits) {
        case 1: Bv = 0; Cv = 204; break;
        case 2: Bv = (b << 8) | (b << 4) | (b << 2) | (b << 1); Cv = 93; break;
        case 3: Bv = (c << 8) | (b << 7) | (c << 3) | (b << 2) | (c << 1) | b; Cv = 44; break;
        case 4: Bv = (d << 8) | (c << 7) | (b << 6) | (d << 2) | (c << 1) | b; Cv = 22; break;
        case 5: Bv = (e << 8) | (d << 7) | (c << 6) | (b << 5) | (e << 1) | d; Cv = 11; break;
        case 6: Bv = (f << 8) | (e << 7) | (d << 6) | (c << 5) | (b << 4) | f; Cv = 5; break;
      }
    } else {
      switch (bits) {
        case 1: Bv = 0; Cv = 113; break;
        case 2: Bv = (b << 8) | (b << 3) | (b << 2); Cv = 54; break;
        case 3: Bv = (c << 8) | (b << 7) | (c << 2) | (b << 1) | c; Cv = 26; break;
        case 4: Bv = (d << 8) | (c << 7) | (b << 6) | (d << 1) | c; Cv = 13; break;
        case 5: Bv = (e << 8) | (d << 7) | (c << 6) | (b << 5) | e; Cv = 6; break;
      }
    }
    int T = D * Cv + Bv; T ^= A; T = (A & 0x80) | (T >> 2); return T & 0xFF;
  }

  static int UnqWeight(int v, int range) {
    Split(range, out int trits, out int quints, out int bits);
    int r;
    if (trits == 0 && quints == 0) { int x = v << (6 - bits); r = x; int s = bits; while (s < 6) { r |= x >> s; s += bits; } r &= 0x3F; }
    else {
      int m = v & ((1 << bits) - 1); int D = v >> bits;
      if (bits == 0) r = trits != 0 ? new[] { 0, 32, 63 }[D] : new[] { 0, 16, 32, 47, 63 }[D];
      else {
        int A = (m & 1) != 0 ? 0x7F : 0; int b = B(m, 1), c = B(m, 2); int Bv = 0, Cv = 0;
        if (trits != 0) { if (bits == 1) { Cv = 50; } else if (bits == 2) { Bv = (b << 6) | (b << 2) | b; Cv = 23; } else { Bv = (c << 6) | (b << 5) | (c << 1) | b; Cv = 11; } }
        else { if (bits == 1) { Cv = 28; } else { Bv = (b << 6) | (b << 1); Cv = 13; } }
        int T = D * Cv + Bv; T ^= A; r = (A & 0x20) | (T >> 2);
      }
    }
    if (r > 32) r++;
    return r;
  }

  static uint Hash52(uint p) {
    p ^= p >> 15; p -= p << 17; p += p << 7; p += p << 4; p ^= p >> 5; p += p << 16; p ^= p >> 7; p ^= p >> 3; p ^= p << 6; p ^= p >> 17; return p;
  }
  static int Partition(int seed, int x, int y, int z, int count, bool small) {
    if (small) { x <<= 1; y <<= 1; z <<= 1; }
    seed += (count - 1) * 1024;
    uint rnum = Hash52((uint)seed);
    int s1 = (int)(rnum & 0xF), s2 = (int)((rnum >> 4) & 0xF), s3 = (int)((rnum >> 8) & 0xF), s4 = (int)((rnum >> 12) & 0xF);
    int s5 = (int)((rnum >> 16) & 0xF), s6 = (int)((rnum >> 20) & 0xF), s7 = (int)((rnum >> 24) & 0xF), s8 = (int)((rnum >> 28) & 0xF);
    int s9 = (int)((rnum >> 18) & 0xF), s10 = (int)((rnum >> 22) & 0xF), s11 = (int)((rnum >> 26) & 0xF), s12 = (int)(((rnum >> 30) | (rnum << 2)) & 0xF);
    s1 *= s1; s2 *= s2; s3 *= s3; s4 *= s4; s5 *= s5; s6 *= s6; s7 *= s7; s8 *= s8; s9 *= s9; s10 *= s10; s11 *= s11; s12 *= s12;
    int sh1, sh2;
    if ((seed & 1) != 0) { sh1 = (seed & 2) != 0 ? 4 : 5; sh2 = count == 3 ? 6 : 5; } else { sh1 = count == 3 ? 6 : 5; sh2 = (seed & 2) != 0 ? 4 : 5; }
    int sh3 = (seed & 0x10) != 0 ? sh1 : sh2;
    s1 >>= sh1; s2 >>= sh2; s3 >>= sh1; s4 >>= sh2; s5 >>= sh1; s6 >>= sh2; s7 >>= sh1; s8 >>= sh2; s9 >>= sh3; s10 >>= sh3; s11 >>= sh3; s12 >>= sh3;
    int a = (int)((s1 * x + s2 * y + s11 * z + (rnum >> 14)) & 0x3F);
    int b = (int)((s3 * x + s4 * y + s12 * z + (rnum >> 10)) & 0x3F);
    int c = (int)((s5 * x + s6 * y + s9 * z + (rnum >> 6)) & 0x3F);
    int d = (int)((s7 * x + s8 * y + s10 * z + (rnum >> 2)) & 0x3F);
    if (count < 4) d = 0; if (count < 3) c = 0;
    if (a >= b && a >= c && a >= d) return 0; if (b >= c && b >= d) return 1; if (c >= d) return 2; return 3;
  }

  static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
  static void Transfer(ref int a, ref int b) { b >>= 1; b |= a & 0x80; a >>= 1; a &= 0x3F; if ((a & 0x20) != 0) a -= 0x40; }
  static int[] Bc(int r, int g, int b, int a) => new[] { (r + b) >> 1, (g + b) >> 1, b, a };

  static bool Endpoints(int cem, int[] v, out int[] e0, out int[] e1) {
    e0 = e1 = null;
    switch (cem) {
      case 0: e0 = new[] { v[0], v[0], v[0], 255 }; e1 = new[] { v[1], v[1], v[1], 255 }; return true;
      case 1: { int l0 = (v[0] >> 2) | (v[1] & 0xC0); int l1 = Math.Min(255, l0 + (v[1] & 0x3F)); e0 = new[] { l0, l0, l0, 255 }; e1 = new[] { l1, l1, l1, 255 }; return true; }
      case 4: e0 = new[] { v[0], v[0], v[0], v[2] }; e1 = new[] { v[1], v[1], v[1], v[3] }; return true;
      case 5: { int a0 = v[0], a1 = v[1], b0 = v[2], b1 = v[3]; Transfer(ref a1, ref a0); Transfer(ref b1, ref b0);
          e0 = new[] { a0, a0, a0, b0 }; e1 = new[] { Clamp(a0 + a1), Clamp(a0 + a1), Clamp(a0 + a1), Clamp(b0 + b1) }; return true; }
      case 6: e1 = new[] { v[0], v[1], v[2], 255 }; e0 = new[] { (v[0] * v[3]) >> 8, (v[1] * v[3]) >> 8, (v[2] * v[3]) >> 8, 255 }; return true;
      case 8: { int s0 = v[0] + v[2] + v[4], s1 = v[1] + v[3] + v[5];
          if (s1 >= s0) { e0 = new[] { v[0], v[2], v[4], 255 }; e1 = new[] { v[1], v[3], v[5], 255 }; }
          else { e0 = Bc(v[1], v[3], v[5], 255); e1 = Bc(v[0], v[2], v[4], 255); }
          return true; }
      case 9: { int r0 = v[0], r1 = v[1], g0 = v[2], g1 = v[3], b0 = v[4], b1 = v[5]; Transfer(ref r1, ref r0); Transfer(ref g1, ref g0); Transfer(ref b1, ref b0);
          if (r1 + g1 + b1 >= 0) { e0 = new[] { r0, g0, b0, 255 }; e1 = new[] { Clamp(r0 + r1), Clamp(g0 + g1), Clamp(b0 + b1), 255 }; }
          else { e0 = Bc(Clamp(r0 + r1), Clamp(g0 + g1), Clamp(b0 + b1), 255); e1 = Bc(r0, g0, b0, 255); }
          for (int i = 0; i < 4; i++) { e0[i] = Clamp(e0[i]); e1[i] = Clamp(e1[i]); }
          return true; }
      case 10: e1 = new[] { v[0], v[1], v[2], v[5] }; e0 = new[] { (v[0] * v[3]) >> 8, (v[1] * v[3]) >> 8, (v[2] * v[3]) >> 8, v[4] }; return true;
      case 12: { int s0 = v[0] + v[2] + v[4], s1 = v[1] + v[3] + v[5];
          if (s1 >= s0) { e0 = new[] { v[0], v[2], v[4], v[6] }; e1 = new[] { v[1], v[3], v[5], v[7] }; }
          else { e0 = Bc(v[1], v[3], v[5], v[7]); e1 = Bc(v[0], v[2], v[4], v[6]); }
          return true; }
      case 13: { int r0 = v[0], r1 = v[1], g0 = v[2], g1 = v[3], b0 = v[4], b1 = v[5], a0 = v[6], a1 = v[7];
          Transfer(ref r1, ref r0); Transfer(ref g1, ref g0); Transfer(ref b1, ref b0); Transfer(ref a1, ref a0);
          if (r1 + g1 + b1 >= 0) { e0 = new[] { r0, g0, b0, a0 }; e1 = new[] { Clamp(r0 + r1), Clamp(g0 + g1), Clamp(b0 + b1), Clamp(a0 + a1) }; }
          else { e0 = Bc(Clamp(r0 + r1), Clamp(g0 + g1), Clamp(b0 + b1), Clamp(a0 + a1)); e1 = Bc(r0, g0, b0, a0); }
          for (int i = 0; i < 4; i++) { e0[i] = Clamp(e0[i]); e1[i] = Clamp(e1[i]); }
          return true; }
      default: return false; // HDR modes
    }
  }

  public static int Errors;

  /// Decodes one 16-byte block at data[off] with footprint bw x bh into rgba (bh*bw*4 bytes). False = error block (magenta written).
  public static bool Block(byte[] data, int off, int bw, int bh, byte[] rgba) {
    int Read(int s, int c) => Bits(data, off, s, c);
    void Fill(int r, int g, int b, int a) { for (int i = 0; i < bw * bh; i++) { rgba[i * 4] = (byte)r; rgba[i * 4 + 1] = (byte)g; rgba[i * 4 + 2] = (byte)b; rgba[i * 4 + 3] = (byte)a; } }
    int mode = Read(0, 11);
    if ((mode & 0x1FF) == 0x1FC) {
      if ((mode & 0x200) != 0) { Errors++; Fill(255, 0, 255, 255); return false; } // HDR void extent
      Fill(Read(64, 16) >> 8, Read(80, 16) >> 8, Read(96, 16) >> 8, Read(112, 16) >> 8); return true;
    }
    // block mode
    int baseQ = (mode >> 4) & 1, H = (mode >> 9) & 1, D = (mode >> 10) & 1, A = (mode >> 5) & 3, W, Hh;
    if ((mode & 3) != 0) {
      baseQ |= (mode & 3) << 1; int Bb = (mode >> 7) & 3;
      switch ((mode >> 2) & 3) {
        case 0: W = Bb + 4; Hh = A + 2; break;
        case 1: W = Bb + 8; Hh = A + 2; break;
        case 2: W = A + 2; Hh = Bb + 8; break;
        default: Bb &= 1; if ((mode & 0x100) != 0) { W = Bb + 2; Hh = A + 2; } else { W = A + 2; Hh = Bb + 6; } break;
      }
    } else {
      baseQ |= ((mode >> 2) & 3) << 1; if (((mode >> 2) & 3) == 0) { Errors++; Fill(255, 0, 255, 255); return false; }
      int Bb = (mode >> 9) & 3;
      switch ((mode >> 7) & 3) {
        case 0: W = 12; Hh = A + 2; break;
        case 1: W = A + 2; Hh = 12; break;
        case 2: W = A + 6; Hh = Bb + 6; D = 0; H = 0; break;
        default:
          switch ((mode >> 5) & 3) { case 0: W = 6; Hh = 10; break; case 1: W = 10; Hh = 6; break; default: Errors++; Fill(255, 0, 255, 255); return false; }
          break;
      }
    }
    int wq = (baseQ - 2) + 6 * H; if (wq < 0 || wq > 11) { Errors++; Fill(255, 0, 255, 255); return false; }
    int wRange = RangeVals[wq]; bool dual = D != 0; int nW = W * Hh * (dual ? 2 : 1);
    int wBits = IseBits(nW, wRange);
    if (nW > 64 || wBits < 24 || wBits > 96 || W > bw || Hh > bh) { Errors++; Fill(255, 0, 255, 255); return false; }
    int parts = Read(11, 2) + 1; if (parts == 4 && dual) { Errors++; Fill(255, 0, 255, 255); return false; }
    var cems = new int[parts]; int colourStart; int extraBits = 0; int seed = 0;
    if (parts == 1) { cems[0] = Read(13, 4); colourStart = 17; }
    else {
      seed = Read(13, 10); int low = Read(23, 6); colourStart = 29;
      if ((low & 3) == 0) { for (int i = 0; i < parts; i++) cems[i] = (low >> 2) & 0xF; }
      else {
        extraBits = 3 * parts - 4; int high = Read(128 - wBits - extraBits, extraBits); int enc = low | (high << 6);
        int baseClass = (enc & 3) - 1; int o = 2;
        for (int i = 0; i < parts; i++) cems[i] = ((((enc >> (o + i)) & 1) + baseClass) << 2);
        o += parts;
        for (int i = 0; i < parts; i++) cems[i] |= (enc >> (o + 2 * i)) & 3;
      }
    }
    int ccsPos = 128 - wBits - extraBits - 2; int ccs = dual ? Read(ccsPos, 2) : -1;
    int nC = 0; foreach (var c in cems) nC += ((c >> 2) + 1) * 2;
    if (nC > 18) { Errors++; Fill(255, 0, 255, 255); return false; }
    int avail = 128 - colourStart - wBits - extraBits - (dual ? 2 : 0);
    int cRange = -1; for (int q = RangeVals.Length - 1; q >= 0; q--) if (IseBits(nC, RangeVals[q]) <= avail) { cRange = RangeVals[q]; break; }
    if (cRange < 6) { Errors++; Fill(255, 0, 255, 255); return false; }
    var craw = Ise(Read, colourStart, nC, cRange); var cv = new int[nC]; for (int i = 0; i < nC; i++) cv[i] = UnqColour(craw[i], cRange);
    // weights: stored bit-reversed from the top of the block
    var rev = new byte[16]; for (int i = 0; i < 128; i++) if (((data[off + (i >> 3)] >> (i & 7)) & 1) != 0) { int j = 127 - i; rev[j >> 3] |= (byte)(1 << (j & 7)); }
    int ReadRev(int s, int c) => Bits(rev, 0, s, c);
    var wraw = Ise(ReadRev, 0, nW, wRange); var wv = new int[nW]; for (int i = 0; i < nW; i++) wv[i] = UnqWeight(wraw[i], wRange);
    // endpoints per partition
    var e0 = new int[parts][]; var e1 = new int[parts][]; int ci = 0;
    for (int p = 0; p < parts; p++) {
      int n = ((cems[p] >> 2) + 1) * 2; var v = new int[8]; Array.Copy(cv, ci, v, 0, n); ci += n;
      if (!Endpoints(cems[p], v, out e0[p], out e1[p])) { Errors++; Fill(255, 0, 255, 255); return false; }
    }
    // infill and interpolate
    int Ds = (1024 + bw / 2) / (bw - 1), Dt = (1024 + bh / 2) / (bh - 1); bool small = bw * bh < 31;
    int planes = dual ? 2 : 1;
    for (int t = 0; t < bh; t++) for (int s = 0; s < bw; s++) {
      int cs = Ds * s, ct = Dt * t; int gs = (cs * (W - 1) + 32) >> 6, gt = (ct * (Hh - 1) + 32) >> 6;
      int js = gs >> 4, fs = gs & 0xF, jt = gt >> 4, ft = gt & 0xF;
      int w11 = (fs * ft + 8) >> 4, w10 = ft - w11, w01 = fs - w11, w00 = 16 - fs - ft + w11;
      var weight = new int[2];
      for (int pl = 0; pl < planes; pl++) {
        int WAt(int gx, int gy) { if (gx >= W || gy >= Hh) return 0; return wv[(gy * W + gx) * planes + pl]; }
        weight[pl] = (WAt(js, jt) * w00 + WAt(js + 1, jt) * w01 + WAt(js, jt + 1) * w10 + WAt(js + 1, jt + 1) * w11 + 8) >> 4;
      }
      int part = parts > 1 ? Partition(seed, s, t, 0, parts, small) : 0;
      int o2 = (t * bw + s) * 4;
      for (int ch = 0; ch < 4; ch++) {
        int w = dual && ch == ccs ? weight[1] : weight[0];
        int c0 = e0[part][ch] * 257, c1 = e1[part][ch] * 257;
        int c = (c0 * (64 - w) + c1 * w + 32) >> 6;
        rgba[o2 + ch] = (byte)(c >> 8);
      }
    }
    return true;
  }

  /// Decodes a whole mip level (w x h texels, bw x bh blocks) starting at data[off]; returns rgba8.
  public static byte[] Image(byte[] data, int off, int w, int h, int bw, int bh) {
    int bx = (w + bw - 1) / bw, by = (h + bh - 1) / bh; var img = new byte[w * h * 4]; var blk = new byte[bw * bh * 4];
    for (int y = 0; y < by; y++) for (int x = 0; x < bx; x++) {
      Block(data, off + (y * bx + x) * 16, bw, bh, blk);
      for (int t = 0; t < bh; t++) for (int s = 0; s < bw; s++) {
        int px = x * bw + s, py = y * bh + t; if (px >= w || py >= h) continue;
        Buffer.BlockCopy(blk, (t * bw + s) * 4, img, (py * w + px) * 4, 4);
      }
    }
    return img;
  }
}

/// Sky cubemap by elevation from the Android file itself: decodes one mip of each face (ASTC, RGBM x5) and averages
/// by elevation band. flipV: face rows stored bottom-up.
static class SkyBands {
  public static double[] Bands = { -90, -60, -30, -10, -3, 0, 3, 10, 20, 35, 50, 70, 90 };
  static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
  static double Gam(double c) { c = Math.Max(0, c); return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055; }
  static void Dir(int f, double u, double v, out double x, out double y, out double z) {
    switch (f) {
      case 0: x = 1; y = -v; z = -u; break;
      case 1: x = -1; y = -v; z = u; break;
      case 2: x = u; y = 1; z = v; break;
      case 3: x = u; y = -1; z = -v; break;
      case 4: x = u; y = -v; z = 1; break;
      default: x = -u; y = -v; z = -1; break;
    }
  }
  /// faces: 6 rgba8 images n x n (RGBM). Returns per band sRGB colour (null where empty) and the 6 face means.
  public static (double[][] bands, double[][] faces) Measure(byte[][] faces, int n, bool flipV, bool rgbm) {
    var bs = new double[Bands.Length - 1, 4]; var fm = new double[6][];
    for (int f = 0; f < 6; f++) {
      double fr = 0, fg = 0, fb = 0; int cnt = 0;
      for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) {
        int row = flipV ? n - 1 - j : j; int o = (row * n + i) * 4;
        double m = rgbm ? faces[f][o + 3] / 255.0 * 5 : 1;
        double R = faces[f][o] / 255.0 * m, G = faces[f][o + 1] / 255.0 * m, Bc = faces[f][o + 2] / 255.0 * m;
        double r = Lin(Math.Min(1, R)), g = Lin(Math.Min(1, G)), b = Lin(Math.Min(1, Bc));
        fr += r; fg += g; fb += b; cnt++;
        double uu = (i + 0.5) / n * 2 - 1, vv = (j + 0.5) / n * 2 - 1; Dir(f, uu, vv, out double x, out double y, out double z);
        double el = Math.Asin(y / Math.Sqrt(x * x + y * y + z * z)) * 180 / Math.PI; double w = 1 / Math.Pow(1 + uu * uu + vv * vv, 1.5);
        for (int k = 0; k + 1 < Bands.Length; k++) if (el >= Bands[k] && el < Bands[k + 1]) { bs[k, 0] += r * w; bs[k, 1] += g * w; bs[k, 2] += b * w; bs[k, 3] += w; }
      }
      fm[f] = new[] { Gam(fr / cnt), Gam(fg / cnt), Gam(fb / cnt) };
    }
    var outb = new double[Bands.Length - 1][];
    for (int k = 0; k + 1 < Bands.Length; k++) outb[k] = bs[k, 3] > 0 ? new[] { Gam(bs[k, 0] / bs[k, 3]), Gam(bs[k, 1] / bs[k, 3]), Gam(bs[k, 2] / bs[k, 3]) } : null;
    return (outb, fm);
  }

  /// The level's sky cubemap: faces decoded at mip `mip` (block size from the format).
  public static byte[][] Faces(Ld L, string level, int mip, out int n, out string name) {
    n = 0; name = null;
    var f = L.File(level); if (f == null) return null;
    foreach (var o in f.Objects) if (o.ClassId == 104) {
      var rs = Env.RsRaw(f, o); var mp = ((int, long))rs["sky"];
      var md = L.Read(f, mp); if (md == null) return null; var (mf, _) = L.Res(f, mp);
      foreach (var x in md.Dct("m_SavedProperties").Lst("m_TexEnvs")) {
        var e = (System.Collections.Generic.Dictionary<string, object>)x; if (e.Str("first") != "_Tex") continue;
        var (tf, to) = L.Res(mf, e.Dct("second").Ptr("m_Texture")); if (to == null || to.ClassId != 89) return null;
        var d = L.Read(tf, to); var img = d.G("image data") as byte[]; int per = (int)d.Num("m_CompleteImageSize"); int fmt = (int)d.Num("m_TextureFormat");
        int w = (int)d.Num("m_Width"); name = d.Str("m_Name");
        int bsz = fmt switch { 48 => 4, 49 => 5, 50 => 6, 51 => 8, 52 => 10, 53 => 12, _ => 0 }; if (bsz == 0 || img == null) return null;
        int at = 0, size = w; for (int m = 0; m < mip; m++) { int bx = (size + bsz - 1) / bsz; at += bx * bx * 16; size = Math.Max(1, size / 2); }
        n = size; var faces = new byte[6][];
        for (int face = 0; face < 6; face++) faces[face] = Astc.Image(img, face * per + at, size, size, bsz, bsz);
        return faces;
      }
    }
    return null;
  }

  public static void Test(string work, string level, int mip) {
    var L = new Ld(work); Astc.Errors = 0;
    var faces = Faces(L, level, mip, out int n, out string name); if (faces == null) { Console.WriteLine("no cubemap"); return; }
    Console.WriteLine($"{level} {name} mip {mip}: {n}x{n}, error blocks {Astc.Errors}");
    foreach (var flip in new[] { false, true }) {
      var (bands, fm) = Measure(faces, n, flip, true);
      Console.WriteLine($" flipV={flip} faces: " + string.Join(" ", fm.Select(c => $"{c[0]:0.000},{c[1]:0.000},{c[2]:0.000}")));
      for (int k = 0; k < bands.Length; k++) if (bands[k] != null) Console.WriteLine($"   {Bands[k]}..{Bands[k + 1]}: {bands[k][0]:0.000},{bands[k][1]:0.000},{bands[k][2]:0.000}");
    }
  }
}
