// apkscene matdesc: Texture2D top-mip decoding, used only to MEASURE colour statistics (means). No pixels are written
// anywhere; only the resulting numbers leave the tool.
using System;
using System.Linq;
using System.Collections.Generic;

static class TexDec {
  static readonly Dictionary<string, byte[]> streams = new(StringComparer.OrdinalIgnoreCase); static long streamBytes;
  public static byte[] Bytes(Ld L, Dictionary<string, object> d, out string err) {
    err = null; var img = d.G("image data") as byte[];
    if (img != null && img.Length > 0) return img;
    var sd = d.Dct("m_StreamData"); var path = Ld.Norm(sd?.Str("path")); long off = (long)(sd?.Num("offset") ?? 0), size = (long)(sd?.Num("size") ?? 0);
    if (size <= 0 || string.IsNullOrEmpty(path)) { err = "no image data"; return null; }
    if (!streams.TryGetValue(path, out var all)) {
      if (!L.D.Has(path)) { err = "stream " + path + " missing"; return null; }
      if (streamBytes > (1500L << 20)) { streams.Clear(); streamBytes = 0; }
      all = L.D.Get(path); streams[path] = all; streamBytes += all.Length;
    }
    if (off + size > all.Length) { err = "stream range"; return null; }
    var r = new byte[size]; Buffer.BlockCopy(all, (int)off, r, 0, (int)size); return r;
  }
  public static int AstcBlock(int fmt) => fmt switch { 48 => 4, 49 => 5, 50 => 6, 51 => 8, 52 => 10, 53 => 12, _ => 0 };
  /// Top mip as RGBA8 (row 0 = first stored row). null = format not handled.
  public static byte[] Rgba(int fmt, byte[] b, int w, int h, out string err) {
    err = null; int n = w * h; var o = new byte[n * 4];
    int bs = AstcBlock(fmt);
    if (bs > 0) { int need = ((w + bs - 1) / bs) * ((h + bs - 1) / bs) * 16; if (b.Length < need) { err = "short"; return null; } return Astc.Image(b, 0, w, h, bs, bs); }
    int px(int bpp) => n * bpp;
    switch (fmt) {
      case 4: if (b.Length < px(4)) break; Buffer.BlockCopy(b, 0, o, 0, n * 4); return o; // RGBA32
      case 5: if (b.Length < px(4)) break; for (int i = 0; i < n; i++) { o[i * 4] = b[i * 4 + 1]; o[i * 4 + 1] = b[i * 4 + 2]; o[i * 4 + 2] = b[i * 4 + 3]; o[i * 4 + 3] = b[i * 4]; } return o; // ARGB32
      case 14: if (b.Length < px(4)) break; for (int i = 0; i < n; i++) { o[i * 4] = b[i * 4 + 2]; o[i * 4 + 1] = b[i * 4 + 1]; o[i * 4 + 2] = b[i * 4]; o[i * 4 + 3] = b[i * 4 + 3]; } return o; // BGRA32
      case 3: if (b.Length < px(3)) break; for (int i = 0; i < n; i++) { o[i * 4] = b[i * 3]; o[i * 4 + 1] = b[i * 3 + 1]; o[i * 4 + 2] = b[i * 3 + 2]; o[i * 4 + 3] = 255; } return o; // RGB24
      case 1: if (b.Length < px(1)) break; for (int i = 0; i < n; i++) { o[i * 4] = o[i * 4 + 1] = o[i * 4 + 2] = 255; o[i * 4 + 3] = b[i]; } return o; // Alpha8
      case 63: if (b.Length < px(1)) break; for (int i = 0; i < n; i++) { o[i * 4] = b[i]; o[i * 4 + 3] = 255; } return o; // R8
      case 62: if (b.Length < px(2)) break; for (int i = 0; i < n; i++) { o[i * 4] = b[i * 2]; o[i * 4 + 1] = b[i * 2 + 1]; o[i * 4 + 3] = 255; } return o; // RG16
      case 7: if (b.Length < px(2)) break; for (int i = 0; i < n; i++) { int v = BitConverter.ToUInt16(b, i * 2); o[i * 4] = (byte)((v >> 11) * 255 / 31); o[i * 4 + 1] = (byte)(((v >> 5) & 63) * 255 / 63); o[i * 4 + 2] = (byte)((v & 31) * 255 / 31); o[i * 4 + 3] = 255; } return o; // RGB565
      case 13: if (b.Length < px(2)) break; for (int i = 0; i < n; i++) { int v = BitConverter.ToUInt16(b, i * 2); o[i * 4] = (byte)((v >> 12) * 17); o[i * 4 + 1] = (byte)(((v >> 8) & 15) * 17); o[i * 4 + 2] = (byte)(((v >> 4) & 15) * 17); o[i * 4 + 3] = (byte)((v & 15) * 17); } return o; // RGBA4444
      case 2: if (b.Length < px(2)) break; for (int i = 0; i < n; i++) { int v = BitConverter.ToUInt16(b, i * 2); o[i * 4 + 3] = (byte)((v >> 12) * 17); o[i * 4] = (byte)(((v >> 8) & 15) * 17); o[i * 4 + 1] = (byte)(((v >> 4) & 15) * 17); o[i * 4 + 2] = (byte)((v & 15) * 17); } return o; // ARGB4444
      case 34: case 45: case 46: case 47: return Etc.Image(fmt, b, w, h, out err);
    }
    err ??= "format " + fmt + " not decoded"; return null;
  }
}

/// ETC1 / ETC2 (RGB, RGB+A1 punch-through, RGBA8 with EAC alpha) block decoder, enough to measure mean colours.
static class Etc {
  static readonly int[,] Mod = { { 2, 8 }, { 5, 17 }, { 9, 29 }, { 13, 42 }, { 18, 60 }, { 24, 80 }, { 33, 106 }, { 47, 183 } };
  static readonly int[] Dist = { 3, 6, 11, 16, 23, 32, 41, 64 };
  static int C(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
  static int Ext4(int v) => v * 17; static int Ext5(int v) => (v << 3) | (v >> 2); static int Ext6(int v) => (v << 2) | (v >> 4); static int Ext7(int v) => (v << 1) | (v >> 6);
  static readonly int[][] EacMod = {
    new[]{-3,-6,-9,-15,2,5,8,14}, new[]{-3,-7,-10,-13,2,6,9,12}, new[]{-2,-5,-8,-13,1,4,7,12}, new[]{-2,-4,-6,-13,1,3,5,12},
    new[]{-3,-6,-8,-12,2,5,7,11}, new[]{-3,-7,-9,-11,2,6,8,10}, new[]{-4,-7,-8,-11,3,6,7,10}, new[]{-3,-5,-8,-11,2,4,7,10},
    new[]{-2,-6,-8,-10,1,5,7,9}, new[]{-2,-5,-8,-10,1,4,7,9}, new[]{-2,-4,-8,-10,1,3,7,9}, new[]{-2,-5,-7,-10,1,4,6,9},
    new[]{-3,-4,-7,-10,2,3,6,9}, new[]{-1,-2,-3,-10,0,1,2,9}, new[]{-4,-6,-8,-9,3,5,7,8}, new[]{-3,-5,-7,-9,2,4,6,8} };
  static int S3(int v) => (v & 4) != 0 ? v - 8 : v;
  /// One 8-byte colour block -> 4x4 texels rgba, row-major.
  static void ColourBlock(byte[] b, int o, byte[] px, bool etc2, bool punch) {
    ulong v = 0; for (int i = 0; i < 8; i++) v = (v << 8) | b[o + i];
    int hi = (int)(v >> 32); uint lo = (uint)v;
    bool diff = ((hi >> 1) & 1) != 0, flip = (hi & 1) != 0;
    bool opaque = !punch || diff; if (punch) diff = true;
    int Sel(int i) { int k = i; return (int)(((lo >> k) & 1) | (((lo >> (k + 16)) & 1) << 1)); } // i = x*4+y
    void Put(int i, int R, int G, int B, int A) { int x = i >> 2, y = i & 3; int q = (y * 4 + x) * 4; px[q] = (byte)C(R); px[q + 1] = (byte)C(G); px[q + 2] = (byte)C(B); px[q + 3] = (byte)A; }
    int r0, r1, g0, g1, b0, b1;
    if (!diff) {
      r0 = Ext4((hi >> 28) & 15); r1 = Ext4((hi >> 24) & 15); g0 = Ext4((hi >> 20) & 15); g1 = Ext4((hi >> 16) & 15); b0 = Ext4((hi >> 12) & 15); b1 = Ext4((hi >> 8) & 15);
    } else {
      int R = (hi >> 27) & 31, dR = S3((hi >> 24) & 7), G = (hi >> 19) & 31, dG = S3((hi >> 16) & 7), B = (hi >> 11) & 31, dB = S3((hi >> 8) & 7);
      if (etc2 && (R + dR < 0 || R + dR > 31)) { // T mode
        int ra = Ext4((((hi >> 27) & 3) << 2) | ((hi >> 24) & 3)), ga = Ext4((hi >> 20) & 15), ba = Ext4((hi >> 16) & 15), rb = Ext4((hi >> 12) & 15), gb = Ext4((hi >> 8) & 15), bb = Ext4((hi >> 4) & 15);
        int d = Dist[(((hi >> 2) & 3) << 1) | (hi & 1)];
        var P = new[] { (ra, ga, ba), (rb + d, gb + d, bb + d), (rb, gb, bb), (rb - d, gb - d, bb - d) };
        for (int i = 0; i < 16; i++) { int s = Sel(i); if (punch && !opaque && s == 2) { Put(i, 0, 0, 0, 0); continue; } Put(i, P[s].Item1, P[s].Item2, P[s].Item3, 255); }
        return;
      }
      if (etc2 && (G + dG < 0 || G + dG > 31)) { // H mode
        int ra4 = (hi >> 27) & 15, ga4 = (((hi >> 24) & 7) << 1) | ((hi >> 20) & 1), ba4 = (((hi >> 19) & 1) << 3) | ((hi >> 15) & 7), rb4 = (hi >> 11) & 15, gb4 = (hi >> 7) & 15, bb4 = (hi >> 3) & 15;
        int c1 = (ra4 << 8) | (ga4 << 4) | ba4, c2 = (rb4 << 8) | (gb4 << 4) | bb4;
        int d = Dist[(((hi >> 2) & 1) << 2) | ((hi & 1) << 1) | (c1 >= c2 ? 1 : 0)];
        int ra = Ext4(ra4), ga = Ext4(ga4), ba = Ext4(ba4), rb = Ext4(rb4), gb = Ext4(gb4), bb = Ext4(bb4);
        var P = new[] { (ra + d, ga + d, ba + d), (ra - d, ga - d, ba - d), (rb + d, gb + d, bb + d), (rb - d, gb - d, bb - d) };
        for (int i = 0; i < 16; i++) { int s = Sel(i); if (punch && !opaque && s == 2) { Put(i, 0, 0, 0, 0); continue; } Put(i, P[s].Item1, P[s].Item2, P[s].Item3, 255); }
        return;
      }
      if (etc2 && (B + dB < 0 || B + dB > 31)) { // planar
        int ro = Ext6((hi >> 25) & 63), go = Ext7((((hi >> 24) & 1) << 6) | ((hi >> 17) & 63)), bo = Ext6((((hi >> 16) & 1) << 5) | (((hi >> 11) & 3) << 3) | ((hi >> 7) & 7));
        int rh = Ext6((((hi >> 2) & 31) << 1) | (hi & 1)), gh = Ext7((int)(lo >> 25)), bh = Ext6((int)((lo >> 19) & 63)), rv = Ext6((int)((lo >> 13) & 63)), gv = Ext7((int)((lo >> 6) & 127)), bv = Ext6((int)(lo & 63));
        for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) { int q = (y * 4 + x) * 4; px[q] = (byte)C((x * (rh - ro) + y * (rv - ro) + 4 * ro + 2) >> 2); px[q + 1] = (byte)C((x * (gh - go) + y * (gv - go) + 4 * go + 2) >> 2); px[q + 2] = (byte)C((x * (bh - bo) + y * (bv - bo) + 4 * bo + 2) >> 2); px[q + 3] = 255; }
        return;
      }
      r0 = Ext5(R); r1 = Ext5(R + dR); g0 = Ext5(G); g1 = Ext5(G + dG); b0 = Ext5(B); b1 = Ext5(B + dB);
    }
    int t0 = (hi >> 5) & 7, t1 = (hi >> 2) & 7;
    for (int i = 0; i < 16; i++) {
      int x = i >> 2, y = i & 3; int sub = flip ? (y >= 2 ? 1 : 0) : (x >= 2 ? 1 : 0); int s = Sel(i); int tb = sub == 0 ? t0 : t1;
      if (punch && !opaque && s == 2) { Put(i, 0, 0, 0, 0); continue; }
      int m = s switch { 0 => Mod[tb, 0], 1 => Mod[tb, 1], 2 => -Mod[tb, 0], _ => -Mod[tb, 1] };
      if (punch && !opaque && s == 0) m = 0; // punch-through without the opaque bit: index 0 is the base colour itself
      Put(i, (sub == 0 ? r0 : r1) + m, (sub == 0 ? g0 : g1) + m, (sub == 0 ? b0 : b1) + m, 255);
    }
  }
  static void EacAlpha(byte[] b, int o, byte[] px) {
    int a0 = b[o], mul = b[o + 1] >> 4, ti = b[o + 1] & 15; ulong bits = 0; for (int i = 2; i < 8; i++) bits = (bits << 8) | b[o + i];
    for (int i = 0; i < 16; i++) { int x = i >> 2, y = i & 3; int idx = (int)((bits >> (45 - 3 * i)) & 7); px[(y * 4 + x) * 4 + 3] = (byte)C(a0 + EacMod[ti][idx] * mul); }
  }
  public static byte[] Image(int fmt, byte[] b, int w, int h, out string err) {
    err = null; int bx = (w + 3) / 4, by = (h + 3) / 4; int bsz = fmt == 47 ? 16 : 8;
    if (b.Length < bx * by * bsz) { err = "short"; return null; }
    var img = new byte[w * h * 4]; var px = new byte[64];
    for (int y = 0; y < by; y++) for (int x = 0; x < bx; x++) {
      int o = (y * bx + x) * bsz;
      if (fmt == 47) { ColourBlock(b, o + 8, px, true, false); EacAlpha(b, o, px); }
      else ColourBlock(b, o, px, fmt != 34, fmt == 46);
      for (int t = 0; t < 4; t++) for (int s = 0; s < 4; s++) { int X = x * 4 + s, Yy = y * 4 + t; if (X >= w || Yy >= h) continue; Buffer.BlockCopy(px, (t * 4 + s) * 4, img, (Yy * w + X) * 4, 4); }
    }
    return img;
  }
}

/// Colour statistics of a texture's top mip in sRGB (stored) space: per alpha value 0..255, texel count and RGB sums.
sealed class TexStat {
  public string Name, Err; public int W, H, Fmt; public double[] Cnt = new double[256]; public double[,] Sum = new double[256, 3];
  public bool Ok => Err == null && Cnt.Sum() > 0;
  public double MeanA() { double s = 0, n = 0; for (int a = 0; a < 256; a++) { s += a / 255.0 * Cnt[a]; n += Cnt[a]; } return n > 0 ? s / n : 1; }
  /// mean rgb over texels with alpha >= minA (0..255), optionally weighted by alpha, times a tint; with amask the tint
  /// applies in proportion to the texel's alpha: rgb * lerp(1, tint, a) (Rec Room's AG shaders: lerp(tex, tex*tint, tex.a))
  public double[] Mean(int minA, bool alphaWeight, double[] tint, bool amask) {
    double r = 0, g = 0, b = 0, n = 0;
    for (int a = minA; a < 256; a++) {
      if (Cnt[a] == 0) continue; double af = a / 255.0; double w = alphaWeight ? af : 1;
      double tr = tint[0], tg = tint[1], tb = tint[2];
      if (amask) { tr = 1 - af + af * tint[0]; tg = 1 - af + af * tint[1]; tb = 1 - af + af * tint[2]; }
      r += Sum[a, 0] / 255.0 * w * tr; g += Sum[a, 1] / 255.0 * w * tg; b += Sum[a, 2] / 255.0 * w * tb; n += Cnt[a] * w;
    }
    if (n <= 0) return null; return new[] { r / n, g / n, b / n };
  }
  static readonly Dictionary<(string, long), TexStat> cache = new();
  public static TexStat Of(Ld L, AF f, AObj o) {
    if (cache.TryGetValue((f.Name, o.PathId), out var ts)) return ts;
    ts = new TexStat(); cache[(f.Name, o.PathId)] = ts;
    try {
      if (o.ClassId != 28) { ts.Err = "class " + o.ClassId; return ts; }
      var d = L.Read(f, o); if (d == null) { ts.Err = "unreadable"; return ts; }
      ts.Name = d.Str("m_Name"); ts.W = (int)d.Num("m_Width"); ts.H = (int)d.Num("m_Height"); ts.Fmt = (int)d.Num("m_TextureFormat");
      var b = TexDec.Bytes(L, d, out var e1); if (b == null) { ts.Err = e1; return ts; }
      var px = TexDec.Rgba(ts.Fmt, b, ts.W, ts.H, out var e2); if (px == null) { ts.Err = e2; return ts; }
      for (int i = 0; i + 3 < px.Length; i += 4) { int a = px[i + 3]; ts.Cnt[a]++; ts.Sum[a, 0] += px[i]; ts.Sum[a, 1] += px[i + 1]; ts.Sum[a, 2] += px[i + 2]; }
      L.Drop(f); // texture dictionaries hold the image bytes; do not keep them cached
    } catch (Exception e) { ts.Err = e.GetType().Name + ": " + e.Message; }
    return ts;
  }
  /// raw RGBA8 of a texture's top mip (terrain alphamaps); null on failure
  public static byte[] Pixels(Ld L, AF f, AObj o, out int w, out int h) {
    w = h = 0; var d = L.Read(f, o); if (d == null) return null; w = (int)d.Num("m_Width"); h = (int)d.Num("m_Height");
    var b = TexDec.Bytes(L, d, out _); return b == null ? null : TexDec.Rgba((int)d.Num("m_TextureFormat"), b, w, h, out _);
  }
}
