// rcscene round 2 (2026-11-23): measurements taken under each piece's own UV triangles of the cache scene's level
// textures, decoded in memory by ../rcref_cache's decoders (CTex / TexDec / Bc7 / CRScene.LoadTex, compiled in by path).
// Only numbers leave this file: per-piece mean colours (sa=), emission (sg=), the texture frame (ua= / rep=), and the
// outlines of a texture's colour regions (vectorised to polygons by ../gtscene/GtPoly.cs). No texel, UV or image is written.
//
// Colour convention: the same as the platemats' avg= (gamma texel values as stored, averaged, x the material colour as
// serialized). sa = mean(albedo) where albedo = tex.rgb x col.rgb (opaque), tex.rgb x lerp(1, col.rgb, tex.a) (Rec Room's AG
// alpha-masked tint, "amask"), only texels with tex.a x col.a >= cutoff (alpha test). The vertex colour is NOT in sa (c=
// keeps it). sg = mean(_EmissionColor (as serialized, may exceed 1) x the emission map texel), as the platemats' glow=.
using System.Text.RegularExpressions;
using SomniumSpace.Worlds.RecRoomDreamport.Games.Quests.Jumbotron;

sealed class TexInfo {
  public string Mat, TexName = "-"; public CTex Base, Em; public double[] Tile = { 1, 1 }, Off = { 0, 0 }, EmTile = { 1, 1 }, EmOff = { 0, 0 };
  public double[] Col = { 1, 1, 1, 1 }, EmCol; public bool Amask, Cutout; public double Cut = 0.5;
  public bool Ok => Base != null;
}

static class RcSample {
  static readonly Dictionary<(SFile, long), TexInfo> Cache = new();
  const long TreeShader = -6364362845305036051;

  /// The material's textures and how its albedo is formed (rcref_cache's material rules).
  public static TexInfo Of(SFile f, SObj o) {
    if (o == null) return new TexInfo();
    if (Cache.TryGetValue((f, o.PathId), out var ti)) return ti;
    ti = new TexInfo();
    try {
      var d = Rc.Read(f, o); ti.Mat = Rc.S(d, "m_Name");
      var sp = Rc.D(d, "m_SavedProperties");
      var cols = new Dictionary<string, double[]>(); foreach (var x in Rc.L(sp, "m_Colors") ?? new()) cols[Rc.S(x, "first") ?? ""] = Rc.Col((x as Dictionary<string, object>)?["second"]);
      var fl = new Dictionary<string, double>(); foreach (var x in Rc.L(sp, "m_Floats") ?? new()) fl[Rc.S(x, "first") ?? ""] = Rc.N(x, "second");
      var texs = new Dictionary<string, Dictionary<string, object>>(); foreach (var x in Rc.L(sp, "m_TexEnvs") ?? new()) texs[Rc.S(x, "first") ?? ""] = Rc.D(x, "second");
      string kw = (d.GetValueOrDefault("m_ValidKeywords") is List<object> vk ? string.Join(" ", vk) : "") + " " + (d.GetValueOrDefault("m_ShaderKeywords") as string ?? "");
      var shp = Rc.D(d, "m_Shader"); long sid = shp == null ? 0 : Convert.ToInt64(shp["m_PathID"]);
      ti.Col = cols.GetValueOrDefault("_BaseColor") ?? cols.GetValueOrDefault("_Color") ?? new double[] { 1, 1, 1, 1 };
      var mt = texs.GetValueOrDefault(Environment.GetEnvironmentVariable("RC_TEXPROP") ?? "_BaseMap"); if (mt == null || Rc.Ptr(f, mt.GetValueOrDefault("m_Texture")).o == null) mt = texs.GetValueOrDefault("_MainTex");
      if (mt != null && Rc.Ptr(f, mt.GetValueOrDefault("m_Texture")).o != null) {
        ti.Base = CRScene.LoadTex(f, mt["m_Texture"], out _); ti.TexName = ti.Base?.Name ?? "-";
        var sc = Rc.D(mt, "m_Scale"); var of = Rc.D(mt, "m_Offset"); ti.Tile = new[] { Rc.N(sc, "x"), Rc.N(sc, "y") }; ti.Off = new[] { Rc.N(of, "x"), Rc.N(of, "y") };
      }
      int queue = (int)Rc.N(d, "m_CustomRenderQueue");
      bool transparent = fl.GetValueOrDefault("_Surface") == 1 || kw.Contains("_SURFACE_TYPE_TRANSPARENT") || queue >= 2900;
      ti.Cutout = fl.GetValueOrDefault("_AlphaClip") > 0 || kw.Contains("_ALPHATEST_ON") || sid == TreeShader;
      if (fl.TryGetValue("_Cutoff", out var cut)) ti.Cut = cut;
      ti.Amask = !transparent && !ti.Cutout && ti.Base != null && ti.Base.HasAlpha;
      var ec = cols.GetValueOrDefault("_EmissionColor");
      if (kw.Contains("_EMISSION") && ec != null && ec[0] + ec[1] + ec[2] > 0.001) {
        ti.EmCol = new[] { ec[0], ec[1], ec[2] };
        var et = texs.GetValueOrDefault("_EmissionMap");
        if (et != null && Rc.Ptr(f, et.GetValueOrDefault("m_Texture")).o != null) {
          ti.Em = CRScene.LoadTex(f, et["m_Texture"], out _); var sc = Rc.D(et, "m_Scale"); var of = Rc.D(et, "m_Offset");
          ti.EmTile = new[] { Rc.N(sc, "x"), Rc.N(sc, "y") }; ti.EmOff = new[] { Rc.N(of, "x"), Rc.N(of, "y") };
        }
      }
    } catch (Exception e) { Console.Error.WriteLine("texinfo " + o.PathId + ": " + e.Message); }
    Cache[(f, o.PathId)] = ti; return ti;
  }

  static int Mod(int a, int n) { int r = a % n; return r < 0 ? r + n : r; }
  static void Texel(CTex t, double u, double v, out double r, out double g, out double b, out double a) {
    var p = t.Mips[0]; var (w, h) = t.Size[0];
    int x = Mod((int)Math.Floor(u * w), w), y = Mod((int)Math.Floor(v * h), h); int i = (y * w + x) * 4;
    r = p[i] / 255.0; g = p[i + 1] / 255.0; b = p[i + 2] / 255.0; a = p[i + 3] / 255.0;
  }

  /// One sample: albedo (3), emission (3), whether it passes the alpha test.
  public static bool Sample(TexInfo ti, double u, double v, double[] o6) {
    Texel(ti.Base, u * ti.Tile[0] + ti.Off[0], v * ti.Tile[1] + ti.Off[1], out double r, out double g, out double b, out double a);
    bool pass = !ti.Cutout || a * ti.Col[3] >= ti.Cut;
    if (ti.Amask) { o6[0] = r * (1 + (ti.Col[0] - 1) * a); o6[1] = g * (1 + (ti.Col[1] - 1) * a); o6[2] = b * (1 + (ti.Col[2] - 1) * a); }
    else { o6[0] = r * ti.Col[0]; o6[1] = g * ti.Col[1]; o6[2] = b * ti.Col[2]; }
    if (ti.EmCol != null) {
      if (ti.Em != null) { Texel(ti.Em, u * ti.EmTile[0] + ti.EmOff[0], v * ti.EmTile[1] + ti.EmOff[1], out double er, out double eg, out double eb, out _); o6[3] = ti.EmCol[0] * er; o6[4] = ti.EmCol[1] * eg; o6[5] = ti.EmCol[2] * eb; }
      else { o6[3] = ti.EmCol[0]; o6[4] = ti.EmCol[1]; o6[5] = ti.EmCol[2]; }
    } else o6[3] = o6[4] = o6[5] = 0;
    return pass;
  }

  /// The mean albedo + emission (6) over a UV triangle (passing texels only; null when none pass) and the passing share.
  public static double[] TriMean(TexInfo ti, double[] uv, int a, int b, int c, out double passShare) {
    passShare = 0; if (!ti.Ok || uv == null) return null;
    double u0 = uv[a * 2], v0 = uv[a * 2 + 1], u1 = uv[b * 2] - u0, v1 = uv[b * 2 + 1] - v0, u2 = uv[c * 2] - u0, v2 = uv[c * 2 + 1] - v0;
    double texA = Math.Abs(u1 * v2 - u2 * v1) * 0.5 * Math.Abs(ti.Tile[0] * ti.Tile[1]) * ti.Base.W * ti.Base.H;
    int k = (int)Math.Clamp(Math.Ceiling(Math.Sqrt(2 * Math.Clamp(texA, 3, 1500))), 2, 56);
    var s = new double[6]; var o6 = new double[6]; int n = 0, pass = 0;
    void At(double b1, double b2) { n++; if (!Sample(ti, u0 + u1 * b1 + u2 * b2, v0 + v1 * b1 + v2 * b2, o6)) return; pass++; for (int q = 0; q < 6; q++) s[q] += o6[q]; }
    for (int i = 0; i < k; i++) for (int j = 0; i + j < k; j++) {
      At((i + 1 / 3.0) / k, (j + 1 / 3.0) / k);
      if (i + j < k - 1) At((i + 2 / 3.0) / k, (j + 2 / 3.0) / k);
    }
    passShare = n == 0 ? 0 : (double)pass / n;
    if (pass == 0) return null;
    for (int q = 0; q < 6; q++) s[q] /= pass; return s;
  }

  static double TriArea(V3[] w, int a, int b, int c) { var e1 = w[b] - w[a]; var e2 = w[c] - w[a]; return new V3(e1.y * e2.z - e1.z * e2.y, e1.z * e2.x - e1.x * e2.z, e1.x * e2.y - e1.y * e2.x).Len() * 0.5; }

  /// Per triangle of t (index list into w / uv): sa+sg (6) or null; and the piece's area-weighted mean (6) and pass share.
  public static double[][] PerTri(TexInfo ti, V3[] w, double[] uv, int[] t, out double[] mean, out double passShare) {
    var res = new double[t.Length / 3][]; mean = null; passShare = 0; if (!ti.Ok || uv == null) return res;
    var m = new double[6]; double W = 0, WA = 0, P = 0;
    for (int i = 0; i + 2 < t.Length; i += 3) {
      var x = TriMean(ti, uv, t[i], t[i + 1], t[i + 2], out double ps); double ar = TriArea(w, t[i], t[i + 1], t[i + 2]);
      WA += ar; P += ar * ps; res[i / 3] = x; if (x == null) continue; W += ar * ps; for (int q = 0; q < 6; q++) m[q] += x[q] * ar * ps;
    }
    passShare = WA > 0 ? P / WA : 0;
    if (W > 0) { for (int q = 0; q < 6; q++) m[q] /= W; mean = m; }
    return res;
  }

  public static string Sa(double[] m) {
    if (m == null) return "";
    string s = $" sa={Y.F3(Math.Clamp(m[0], 0, 1))},{Y.F3(Math.Clamp(m[1], 0, 1))},{Y.F3(Math.Clamp(m[2], 0, 1))}";
    if (m[3] + m[4] + m[5] > 0.012) s += $" sg={Y.F3(m[3])},{Y.F3(m[4])},{Y.F3(m[5])}";
    return s;
  }

  /// The texture frame of a piece (contract C4): the world direction of +u (dP/du, area-weighted, the biggest triangle's
  /// sign) and the metres per one texture repeat along u and v (|dP/du| / tile.x, |dP/dv| / tile.y). Null when the UVs
  /// are degenerate.
  public static (V3 ua, double ru, double rv)? Frame(TexInfo ti, V3[] w, double[] uv, int[] t) {
    if (uv == null) return null;
    V3 T = default; double RU = 0, RV = 0, A = 0; V3 first = default; bool have = false;
    var order = Enumerable.Range(0, t.Length / 3).OrderByDescending(i => TriArea(w, t[i * 3], t[i * 3 + 1], t[i * 3 + 2])).ToList();
    foreach (int i in order) {
      int a = t[i * 3], b = t[i * 3 + 1], c = t[i * 3 + 2];
      var e1 = w[b] - w[a]; var e2 = w[c] - w[a];
      double du1 = uv[b * 2] - uv[a * 2], dv1 = uv[b * 2 + 1] - uv[a * 2 + 1], du2 = uv[c * 2] - uv[a * 2], dv2 = uv[c * 2 + 1] - uv[a * 2 + 1];
      double det = du1 * dv2 - du2 * dv1; if (Math.Abs(det) < 1e-12) continue;
      var dPdu = (e1 * dv2 - e2 * dv1) * (1 / det); var dPdv = (e2 * du1 - e1 * du2) * (1 / det);
      double ar = TriArea(w, a, b, c); if (ar < 1e-9 || dPdu.Len() < 1e-9) continue;
      if (!have) { first = dPdu; have = true; }
      if (first.x * dPdu.x + first.y * dPdu.y + first.z * dPdu.z < 0) dPdu = dPdu * -1;
      T = T + dPdu * ar; RU += dPdu.Len() * ar; RV += dPdv.Len() * ar; A += ar;
    }
    if (A <= 0 || T.Len() < 1e-9) return null;
    return (T * (1 / T.Len()), RU / A / Math.Max(1e-9, Math.Abs(ti.Tile[0])), RV / A / Math.Max(1e-9, Math.Abs(ti.Tile[1])));
  }

  /// The piece's triangles split by texture frame: triangles whose +u direction agrees within 5 degrees and whose metres
  /// per repeat agree within 5 % share one frame (a floor piece can carry differently turned UV islands); biggest first.
  public static List<(int[] t, (V3 ua, double ru, double rv)? fr)> FrameGroups(TexInfo ti, V3[] w, double[] uv, int[] t) {
    var res = new List<(int[] t, (V3 ua, double ru, double rv)? fr)>(); if (uv == null) { res.Add((t, null)); return res; }
    var groups = new List<(V3 u, double ru, double rv, List<int> m, double a)>(); var bad = new List<int>(); double cosT = Math.Cos(5 * Math.PI / 180);
    foreach (int i in Enumerable.Range(0, t.Length / 3).OrderByDescending(i => TriArea(w, t[i * 3], t[i * 3 + 1], t[i * 3 + 2])).ThenBy(i => i)) {
      var one = new[] { t[i * 3], t[i * 3 + 1], t[i * 3 + 2] }; var f = Frame(ti, w, uv, one);
      if (f == null) { bad.Add(i); continue; }
      var (u, ru, rv) = f.Value; int gi = -1;
      for (int g = 0; g < groups.Count && gi < 0; g++) { var G = groups[g]; if (u.x * G.u.x + u.y * G.u.y + u.z * G.u.z >= cosT && Math.Abs(ru / G.ru - 1) < 0.05 && Math.Abs(rv / G.rv - 1) < 0.05) gi = g; }
      double ar = TriArea(w, one[0], one[1], one[2]);
      if (gi < 0) groups.Add((u, ru, rv, new List<int> { i }, ar)); else { var G = groups[gi]; G.m.Add(i); G.a += ar; groups[gi] = G; }
    }
    // degenerate-UV triangles join the biggest group
    if (bad.Count > 0) { if (groups.Count == 0) { res.Add((t, null)); return res; } groups[0].m.AddRange(bad); }
    foreach (var G in groups.OrderByDescending(g => g.a)) {
      var tt = G.m.SelectMany(i => new[] { t[i * 3], t[i * 3 + 1], t[i * 3 + 2] }).ToArray();
      res.Add((tt, Frame(ti, w, uv, tt)));
    }
    return res;
  }

  static string Sig3(double v) => v == 0 ? "0" : Math.Round(v, Math.Max(0, 2 - (int)Math.Floor(Math.Log10(Math.Abs(v))))).ToString("0.#####", Rc.IC);
  public static string FrameTag((V3 ua, double ru, double rv)? fr) => fr == null ? "" : $" ua={Y.F3(Math.Round(fr.Value.ua.x, 2))},{Y.F3(Math.Round(fr.Value.ua.y, 2))},{Y.F3(Math.Round(fr.Value.ua.z, 2))} rep={Sig3(fr.Value.ru)},{Sig3(fr.Value.rv)}";

  // ------------------------------------------------------------------ colour regions (vectorised)

  public sealed class VecStat { public int Pieces, Dropped, Cells, Clusters, Tris; }

  /// texprofile: a texture's seam lines (brick grout courses, plank joints) counted along v (rows) and u (columns): the
  /// luminance profile's dark dips (a row/column whose mean is below its neighbourhood by 6 %+), so rep= converts to a
  /// brick or plank size. Numbers only.
  public static void Profile(TexInfo ti) {
    if (!ti.Ok) { Console.WriteLine($"{ti.Mat}: no texture"); return; }
    var p = ti.Base.Mips[0]; var (w, h) = ti.Base.Size[0];
    var row = new double[h]; var col = new double[w];
    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { int i = (y * w + x) * 4; double l = 0.2126 * p[i] + 0.7152 * p[i + 1] + 0.0722 * p[i + 2]; row[y] += l / w; col[x] += l / h; }
    int Dips(double[] a, out string at) {
      int n = a.Length, r = Math.Max(2, n / 64), c = 0; var pos = new List<int>();
      for (int i = 0; i < n; i++) {
        double loc = 0; int k = 0; for (int d = -r; d <= r; d++) { loc += a[(i + d + n) % n]; k++; } loc /= k;
        bool min = a[i] <= a[(i + 1) % n] && a[i] <= a[(i - 1 + n) % n];
        if (min && a[i] < 0.94 * loc && (pos.Count == 0 || i - pos[^1] > r)) pos.Add(i);
      }
      at = string.Join(",", pos); return pos.Count;
    }
    int nr = Dips(row, out var ra), nc = Dips(col, out var ca);
    Console.WriteLine($"{ti.Mat}: {ti.TexName} {w}x{h} tile {Y.F3(ti.Tile[0])},{Y.F3(ti.Tile[1])}: {nr} dark rows (seams across v) at {ra}; {nc} dark columns (seams across u) at {ca}");
    int Peaks(double[] a, out string at) { var neg = a.Select(x => 255 - x).ToArray(); return Dips(neg, out at); }
    int pr = Peaks(row, out var pra), pcn = Peaks(col, out var pca);
    Console.WriteLine($"  light rows (light seams across v) {pr} at {pra}; light columns {pcn} at {pca}");
    if (Environment.GetEnvironmentVariable("RC_PROFDUMP") != null) { Console.WriteLine("  rows: " + string.Join(" ", Enumerable.Range(0, h / 4).Select(i => ((int)(row[i * 4])).ToString()))); Console.WriteLine("  cols: " + string.Join(" ", Enumerable.Range(0, w / 4).Select(i => ((int)(col[i * 4])).ToString()))); }
  }

  /// texpattern (round 3, 2026-11-23): a tiling texture's sparse motif measured as numbers - the ground colour (median
  /// texel), the motif texels (chroma distance from the ground > 0.12), their mean colour and share, and the motif blobs
  /// (4-connected, wrapping): count, mean size, mean bounding box, and their centres in UV (0..1) so the lattice can be read
  /// off. Colours as the platemats' avg (gamma texel x material colour). No texel or image is written.
  public static void Pattern(TexInfo ti) {
    if (!ti.Ok) { Console.WriteLine($"{ti.Mat}: no texture"); return; }
    var p = ti.Base.Mips[0]; var (w, h) = ti.Base.Size[0]; int n = w * h;
    double C(int i, int c) => p[i * 4 + c] / 255.0 * ti.Col[c];
    double Med(int c) { var a = new double[n]; for (int i = 0; i < n; i++) a[i] = C(i, c); Array.Sort(a); return a[n / 2]; }
    var g = new[] { Med(0), Med(1), Med(2) };
    var mean = new double[3]; for (int i = 0; i < n; i++) for (int c = 0; c < 3; c++) mean[c] += C(i, c) / n;
    double thr = double.Parse(Environment.GetEnvironmentVariable("RC_PATTHR") ?? "0.12", System.Globalization.CultureInfo.InvariantCulture);
    var on = new bool[n]; var dc = new double[3]; int dn = 0; var gc = new double[3]; int gn = 0;
    for (int i = 0; i < n; i++) {
      double d = Math.Sqrt(Math.Pow(C(i, 0) - g[0], 2) + Math.Pow(C(i, 1) - g[1], 2) + Math.Pow(C(i, 2) - g[2], 2));
      if (d > thr) { on[i] = true; dn++; for (int c = 0; c < 3; c++) dc[c] += C(i, c); } else { gn++; for (int c = 0; c < 3; c++) gc[c] += C(i, c); }
    }
    string F(double[] a, int k) => string.Join(",", a.Select(x => Y.F3(x / Math.Max(1, k))));
    Console.WriteLine($"{ti.Mat}: {ti.TexName} {w}x{h} tile {Y.F3(ti.Tile[0])},{Y.F3(ti.Tile[1])} col {string.Join(",", ti.Col.Select(Y.F3))}");
    Console.WriteLine($"  mean {F(mean, 1)}  median ground {F(g, 1)}  ground(non-motif) mean {F(gc, gn)}  motif mean {F(dc, dn)} share {Y.F3((double)dn / n)}");
    // ground luminance spread (pile noise)
    double lm = 0, lv = 0; int ln = 0; for (int i = 0; i < n; i++) if (!on[i]) { double l = 0.2126 * C(i, 0) + 0.7152 * C(i, 1) + 0.0722 * C(i, 2); lm += l; lv += l * l; ln++; }
    lm /= Math.Max(1, ln); Console.WriteLine($"  ground luminance mean {Y.F3(lm)} sd {Y.F3(Math.Sqrt(Math.Max(0, lv / Math.Max(1, ln) - lm * lm)))}");
    var lab = new int[n]; var blobs = new List<(double cx, double cy, int sz, int bw, int bh)>();
    var st = new Stack<int>();
    for (int s = 0; s < n; s++) {
      if (!on[s] || lab[s] != 0) continue; int id = blobs.Count + 1; lab[s] = id; st.Push(s);
      int sz = 0; double ax = 0, ay = 0; int x0 = s % w, y0 = s / w; int minx = 0, maxx = 0, miny = 0, maxy = 0;
      var rel = new Dictionary<int, (int rx, int ry)> { [s] = (0, 0) };
      while (st.Count > 0) {
        int i = st.Pop(); var (rx, ry) = rel[i]; sz++; ax += rx; ay += ry; minx = Math.Min(minx, rx); maxx = Math.Max(maxx, rx); miny = Math.Min(miny, ry); maxy = Math.Max(maxy, ry);
        int x = i % w, y = i / w;
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) {
          int j = Mod(y + dy, h) * w + Mod(x + dx, w); if (!on[j] || lab[j] != 0) continue; lab[j] = id; rel[j] = (rx + dx, ry + dy); st.Push(j);
        }
      }
      blobs.Add(((x0 + ax / sz + 0.5) / w % 1.0, (y0 + ay / sz + 0.5) / h % 1.0, sz, maxx - minx + 1, maxy - miny + 1));
    }
    int minSz = int.Parse(Environment.GetEnvironmentVariable("RC_PATMIN") ?? "4");
    var big = blobs.Where(b => b.sz >= minSz).ToList();
    Console.WriteLine($"  blobs {blobs.Count} (>= {minSz} texels: {big.Count}); mean size {Y.F3(big.Count == 0 ? 0 : big.Average(b => b.sz))} texels, bbox {Y.F3(big.Count == 0 ? 0 : big.Average(b => b.bw))} x {Y.F3(big.Count == 0 ? 0 : big.Average(b => b.bh))}");
    // the motif's shape as row widths (texels on per row through the first big blob's bbox, averaged over all big blobs)
    if (big.Count > 0) {
      int bw = (int)Math.Round(big.Average(b => b.bw)), bh = (int)Math.Round(big.Average(b => b.bh)); var rw = new double[bh]; var cw = new double[bw];
      foreach (var b in big) {
        int cx = (int)Math.Floor(b.cx * w), cy = (int)Math.Floor(b.cy * h);
        for (int yy = 0; yy < bh; yy++) for (int xx = 0; xx < bw; xx++) if (on[Mod(cy - bh / 2 + yy, h) * w + Mod(cx - bw / 2 + xx, w)]) { rw[yy] += 1.0 / big.Count; cw[xx] += 1.0 / big.Count; }
      }
      Console.WriteLine($"  motif row widths {string.Join(" ", rw.Select(x => x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)))}; column heights {string.Join(" ", cw.Select(x => x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)))}");
    }
    foreach (var b in big.OrderBy(b => b.cy).ThenBy(b => b.cx).Take(int.Parse(Environment.GetEnvironmentVariable("RC_PATLIST") ?? "64")))
      Console.WriteLine($"    {Y.F3(b.cx)} {Y.F3(b.cy)} sz {b.sz} {b.bw}x{b.bh}");
  }

  /// A piece's triangles grouped by plane (normal within 1 degree of the group's biggest triangle, every vertex within
  /// 1 cm of its plane), biggest first: (triangle index list, area).
  public static List<(int[] t, double a)> PlanarGroups(V3[] w, int[] pc) {
    int nt = pc.Length / 3; var n = new V3[nt]; var ar = new double[nt];
    for (int k = 0; k < nt; k++) { var e1 = w[pc[k * 3 + 1]] - w[pc[k * 3]]; var e2 = w[pc[k * 3 + 2]] - w[pc[k * 3]]; var cr = new V3(e1.y * e2.z - e1.z * e2.y, e1.z * e2.x - e1.x * e2.z, e1.x * e2.y - e1.y * e2.x); ar[k] = cr.Len() * 0.5; n[k] = ar[k] > 1e-12 ? cr * (0.5 / ar[k]) : new V3(0, 0, 0); }
    var groups = new List<(V3 n, double d, List<int> m, double a)>(); double cosT = Math.Cos(Math.PI / 180);
    foreach (int k in Enumerable.Range(0, nt).OrderByDescending(k => ar[k]).ThenBy(k => k)) {
      if (ar[k] <= 1e-12) continue; int gi = -1;
      for (int g = 0; g < groups.Count && gi < 0; g++) {
        var G = groups[g]; if (n[k].x * G.n.x + n[k].y * G.n.y + n[k].z * G.n.z < cosT) continue;
        bool ok = true; for (int v = 0; v < 3; v++) { var p = w[pc[k * 3 + v]]; if (Math.Abs(p.x * G.n.x + p.y * G.n.y + p.z * G.n.z - G.d) > 0.01) ok = false; }
        if (ok) gi = g;
      }
      if (gi < 0) { var p = w[pc[k * 3]]; groups.Add((n[k], p.x * n[k].x + p.y * n[k].y + p.z * n[k].z, new List<int> { k }, ar[k])); }
      else { var G = groups[gi]; G.m.Add(k); G.a += ar[k]; groups[gi] = G; }
    }
    return groups.OrderByDescending(g => g.a).Select(g => (g.m.SelectMany(k => new[] { pc[k * 3], pc[k * 3 + 1], pc[k * 3 + 2] }).ToArray(), g.a)).ToList();
  }

  /// A planar piece's texture split into its colour regions: a grid of cells (about 2 texels, at least 1.5 cm) on the
  /// piece's plane, each cell's albedo+emission sampled through the piece's own UVs; the cells clustered into at most 8
  /// colours (deterministic farthest-point seeds + Lloyd), a 3x3 majority filter, regions under 8 cells joined to their
  /// neighbour; then each triangle clipped to the row runs of equal colour -> output triangles (world, exactly on the piece's
  /// triangles) with their cluster's mean sa+sg. Cut-away cells (alpha test) leave holes; a piece with less than half of
  /// its area passing is dropped. Returns null when the piece cannot be vectorised (no UVs / texture).
  public static (V3[] w, int[] t, double[][] sa)? Vectorise(TexInfo ti, V3[] w, double[] uv, int[] pc, VecStat st, out bool drop) {
    drop = false; if (!ti.Ok || uv == null || pc.Length < 3) return null;
    // plane frame
    V3 N = default; double worldA = 0, texA = 0;
    for (int i = 0; i + 2 < pc.Length; i += 3) {
      var e1 = w[pc[i + 1]] - w[pc[i]]; var e2 = w[pc[i + 2]] - w[pc[i]]; var cr = new V3(e1.y * e2.z - e1.z * e2.y, e1.z * e2.x - e1.x * e2.z, e1.x * e2.y - e1.y * e2.x);
      N = N + cr; worldA += cr.Len() * 0.5;
      double u1 = uv[pc[i + 1] * 2] - uv[pc[i] * 2], v1 = uv[pc[i + 1] * 2 + 1] - uv[pc[i] * 2 + 1], u2 = uv[pc[i + 2] * 2] - uv[pc[i] * 2], v2 = uv[pc[i + 2] * 2 + 1] - uv[pc[i] * 2 + 1];
      texA += Math.Abs(u1 * v2 - u2 * v1) * 0.5 * Math.Abs(ti.Tile[0] * ti.Tile[1]) * ti.Base.W * ti.Base.H;
    }
    if (N.Len() < 1e-12 || worldA < 1e-6) return null;
    N = N * (1 / N.Len());
    // planar pieces only (every vertex within 1 cm of the mean plane): a curved piece keeps its per-triangle colours
    { double dm = 0; foreach (int i in pc) dm += N.x * w[i].x + N.y * w[i].y + N.z * w[i].z; dm /= pc.Length;
      foreach (int i in pc) if (Math.Abs(N.x * w[i].x + N.y * w[i].y + N.z * w[i].z - dm) > 0.01) return null; }
    RrJumbotronPolyMath.Frame(N.x, N.y, N.z, out double ux, out double uy, out double uz, out double vx, out double vy, out double vz);
    var U = new V3(ux, uy, uz); var V = new V3(vx, vy, vz);
    double texel = texA > 0 ? Math.Sqrt(worldA / texA) : 0.01;
    double s = Math.Max(0.015, 2 * texel);
    double x0 = 1e18, y0 = 1e18, x1 = -1e18, y1 = -1e18;
    var X = new double[w.Length]; var Yc = new double[w.Length];
    foreach (int i in pc) { X[i] = w[i].x * U.x + w[i].y * U.y + w[i].z * U.z; Yc[i] = w[i].x * V.x + w[i].y * V.y + w[i].z * V.z; x0 = Math.Min(x0, X[i]); x1 = Math.Max(x1, X[i]); y0 = Math.Min(y0, Yc[i]); y1 = Math.Max(y1, Yc[i]); }
    while ((x1 - x0) / s * (y1 - y0) / s > 300000) s *= 1.25;
    int nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / s)), ny = Math.Max(1, (int)Math.Ceiling((y1 - y0) / s));
    var feat = new double[nx * ny][]; var pass = new bool[nx * ny]; var inside = new bool[nx * ny];
    var o6 = new double[6]; int nIn = 0, nPass = 0;
    for (int i = 0; i + 2 < pc.Length; i += 3) {
      int a = pc[i], b = pc[i + 1], c = pc[i + 2];
      double ax = X[a], ay = Yc[a], bx = X[b] - ax, by = Yc[b] - ay, cx = X[c] - ax, cy = Yc[c] - ay; double det = bx * cy - cx * by; if (Math.Abs(det) < 1e-14) continue;
      int i0 = Math.Max(0, (int)Math.Floor((Math.Min(ax, Math.Min(X[b], X[c])) - x0) / s)), i1 = Math.Min(nx - 1, (int)Math.Floor((Math.Max(ax, Math.Max(X[b], X[c])) - x0) / s));
      int j0 = Math.Max(0, (int)Math.Floor((Math.Min(ay, Math.Min(Yc[b], Yc[c])) - y0) / s)), j1 = Math.Min(ny - 1, (int)Math.Floor((Math.Max(ay, Math.Max(Yc[b], Yc[c])) - y0) / s));
      for (int j = j0; j <= j1; j++) for (int q = i0; q <= i1; q++) {
        int cell = j * nx + q; if (inside[cell]) continue;
        double px = x0 + (q + 0.5) * s - ax, py = y0 + (j + 0.5) * s - ay;
        double b1 = (px * cy - cx * py) / det, b2 = (bx * py - px * by) / det; if (b1 < -1e-9 || b2 < -1e-9 || b1 + b2 > 1 + 1e-9) continue;
        // 4 samples in the cell (quarter offsets), averaged
        var f6 = new double[6]; int pn = 0;
        for (int sx = 0; sx < 2; sx++) for (int sy = 0; sy < 2; sy++) {
          double qx = px + (sx - 0.5) * s * 0.5, qy = py + (sy - 0.5) * s * 0.5; double c1 = (qx * cy - cx * qy) / det, c2 = (bx * qy - qx * by) / det;
          double u = uv[a * 2] + (uv[b * 2] - uv[a * 2]) * c1 + (uv[c * 2] - uv[a * 2]) * c2, v = uv[a * 2 + 1] + (uv[b * 2 + 1] - uv[a * 2 + 1]) * c1 + (uv[c * 2 + 1] - uv[a * 2 + 1]) * c2;
          if (Sample(ti, u, v, o6)) { pn++; for (int k = 0; k < 6; k++) f6[k] += o6[k]; }
        }
        inside[cell] = true; nIn++;
        if (pn >= 2) { for (int k = 0; k < 6; k++) f6[k] /= pn; feat[cell] = f6; pass[cell] = true; nPass++; }
      }
    }
    st.Pieces++;
    if (nIn == 0) return null;
    if (nPass < 0.5 * nIn) { drop = true; st.Dropped++; return null; }
    st.Cells += nIn;
    // clusters: features albedo rgb + emission rgb x 0.5
    double Dist(double[] p, double[] q) { double d = 0; for (int k = 0; k < 6; k++) { double e = (p[k] - q[k]) * (k >= 3 ? 0.5 : 1); d += e * e; } return d; }
    var cellsIn = Enumerable.Range(0, nx * ny).Where(cc => pass[cc]).ToList();
    var mean0 = new double[6]; foreach (int cc in cellsIn) for (int k = 0; k < 6; k++) mean0[k] += feat[cc][k] / cellsIn.Count;
    var cent = new List<double[]>();
    // seed: the cell nearest the mean, then farthest points while they are > 0.1 away
    cent.Add((double[])feat[cellsIn.OrderBy(cc => Dist(feat[cc], mean0)).ThenBy(cc => cc).First()].Clone());
    var dmin = cellsIn.Select(cc => Dist(feat[cc], cent[0])).ToArray();
    while (cent.Count < 8) {
      int bi = -1; double bd = 0.01; for (int z = 0; z < cellsIn.Count; z++) if (dmin[z] > bd) { bd = dmin[z]; bi = z; }
      if (bi < 0) break; cent.Add((double[])feat[cellsIn[bi]].Clone());
      for (int z = 0; z < cellsIn.Count; z++) dmin[z] = Math.Min(dmin[z], Dist(feat[cellsIn[z]], cent[^1]));
    }
    var lab = new int[nx * ny]; for (int z = 0; z < lab.Length; z++) lab[z] = -1;
    for (int it = 0; it < 12; it++) {
      var sum = cent.Select(_ => new double[7]).ToList();
      foreach (int cc in cellsIn) { int best = 0; double bd = 1e18; for (int k = 0; k < cent.Count; k++) { double d = Dist(feat[cc], cent[k]); if (d < bd) { bd = d; best = k; } } lab[cc] = best; for (int k = 0; k < 6; k++) sum[best][k] += feat[cc][k]; sum[best][6]++; }
      for (int k = 0; k < cent.Count; k++) if (sum[k][6] > 0) for (int q = 0; q < 6; q++) cent[k][q] = sum[k][q] / sum[k][6];
    }
    // centres closer than 0.06 (rms ~0.035 per channel) merge
    var map = Enumerable.Range(0, cent.Count).ToArray();
    for (int k = 0; k < cent.Count; k++) for (int q = 0; q < k; q++) if (map[q] == q && Dist(cent[k], cent[q]) < 0.0036) { map[k] = q; break; }
    foreach (int cc in cellsIn) lab[cc] = map[lab[cc]];
    // cells inside but cut away stay -1; inside cells without a passing sample keep -1 (holes)
    // 3x3 majority filter (passing cells only)
    var lab2 = (int[])lab.Clone(); var cnt = new int[8];
    foreach (int cc in cellsIn) {
      int q = cc % nx, j = cc / nx; Array.Clear(cnt);
      for (int dj = -1; dj <= 1; dj++) for (int dq = -1; dq <= 1; dq++) { int qq = q + dq, jj = j + dj; if (qq < 0 || jj < 0 || qq >= nx || jj >= ny) continue; int l = lab[jj * nx + qq]; if (l >= 0) cnt[l]++; }
      int bl = lab[cc]; for (int l = 0; l < 8; l++) if (cnt[l] > cnt[bl]) bl = l;
      if (cnt[bl] >= 5) lab2[cc] = bl;
    }
    lab = lab2;
    // small regions (< 8 cells) join their most common neighbouring label
    for (int pass2 = 0; pass2 < 3; pass2++) {
      var comp = new int[nx * ny]; for (int z = 0; z < comp.Length; z++) comp[z] = -1; int nc = 0; var stack = new Stack<int>(); bool changed = false;
      foreach (int c0 in cellsIn) {
        if (comp[c0] >= 0 || lab[c0] < 0) continue; var mem = new List<int>(); stack.Push(c0); comp[c0] = nc;
        while (stack.Count > 0) { int cc = stack.Pop(); mem.Add(cc); int q = cc % nx, j = cc / nx;
          foreach (var (dq, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int qq = q + dq, jj = j + dj; if (qq < 0 || jj < 0 || qq >= nx || jj >= ny) continue; int z = jj * nx + qq; if (comp[z] < 0 && lab[z] == lab[c0]) { comp[z] = nc; stack.Push(z); } } }
        nc++;
        if (mem.Count >= 8) continue;
        Array.Clear(cnt);
        foreach (int cc in mem) { int q = cc % nx, j = cc / nx; foreach (var (dq, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int qq = q + dq, jj = j + dj; if (qq < 0 || jj < 0 || qq >= nx || jj >= ny) continue; int l = lab[jj * nx + qq]; if (l >= 0 && l != lab[c0]) cnt[l]++; } }
        int bl = -1, bc = 0; for (int l = 0; l < 8; l++) if (cnt[l] > bc) { bc = cnt[l]; bl = l; }
        if (bl >= 0) { foreach (int cc in mem) lab[cc] = bl; changed = true; }
      }
      if (!changed) break;
    }
    // label of the edge cells whose centre fell outside every triangle: their nearest labelled neighbour (2 rings)
    for (int ring = 0; ring < 2; ring++) {
      var add = new List<(int, int)>();
      for (int cc = 0; cc < nx * ny; cc++) {
        if (lab[cc] >= 0 || (inside[cc] && !pass[cc])) continue; int q = cc % nx, j = cc / nx;
        foreach (var (dq, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) }) { int qq = q + dq, jj = j + dj; if (qq < 0 || jj < 0 || qq >= nx || jj >= ny) continue; int l = lab[jj * nx + qq]; if (l >= 0) { add.Add((cc, l)); break; } }
      }
      foreach (var (cc, l) in add) lab[cc] = l;
    }
    // the clusters' measured means (over their final cells)
    var cm = new double[8][]; var cn = new int[8];
    for (int l = 0; l < 8; l++) cm[l] = new double[6];
    foreach (int cc in cellsIn) { int l = lab[cc]; if (l < 0) continue; cn[l]++; for (int k = 0; k < 6; k++) cm[l][k] += feat[cc][k]; }
    for (int l = 0; l < 8; l++) if (cn[l] > 0) for (int k = 0; k < 6; k++) cm[l][k] /= cn[l];
    st.Clusters += cn.Count(x => x > 0);
    // any cell a triangle touches that is still unlabelled (thin slivers): sampled at the cell centre clamped into the
    // triangle, nearest cluster
    for (int i = 0; i + 2 < pc.Length; i += 3) {
      int a = pc[i], b = pc[i + 1], c = pc[i + 2];
      double ax = X[a], ay = Yc[a], bx = X[b] - ax, by = Yc[b] - ay, cx = X[c] - ax, cy = Yc[c] - ay; double det = bx * cy - cx * by; if (Math.Abs(det) < 1e-14) continue;
      int i0 = Math.Max(0, (int)Math.Floor((Math.Min(ax, Math.Min(X[b], X[c])) - x0) / s)), i1 = Math.Min(nx - 1, (int)Math.Floor((Math.Max(ax, Math.Max(X[b], X[c])) - x0) / s));
      int j0 = Math.Max(0, (int)Math.Floor((Math.Min(ay, Math.Min(Yc[b], Yc[c])) - y0) / s)), j1 = Math.Min(ny - 1, (int)Math.Floor((Math.Max(ay, Math.Max(Yc[b], Yc[c])) - y0) / s));
      for (int j = j0; j <= j1; j++) for (int q = i0; q <= i1; q++) {
        int cell = j * nx + q; if (lab[cell] >= 0 || (inside[cell] && !pass[cell])) continue;
        double px = x0 + (q + 0.5) * s - ax, py = y0 + (j + 0.5) * s - ay;
        double b1 = Math.Clamp((px * cy - cx * py) / det, 0, 1), b2 = Math.Clamp((bx * py - px * by) / det, 0, 1); if (b1 + b2 > 1) { double z = b1 + b2; b1 /= z; b2 /= z; }
        double u = uv[a * 2] + (uv[b * 2] - uv[a * 2]) * b1 + (uv[c * 2] - uv[a * 2]) * b2, v = uv[a * 2 + 1] + (uv[b * 2 + 1] - uv[a * 2 + 1]) * b1 + (uv[c * 2 + 1] - uv[a * 2 + 1]) * b2;
        if (!Sample(ti, u, v, o6)) continue;
        int best = -1; double bd = 1e18; for (int l = 0; l < 8; l++) { if (cn[l] == 0) continue; double d = Dist(o6, cm[l]); if (d < bd) { bd = d; best = l; } }
        lab[cell] = best;
      }
    }
    // clip each triangle to the row runs of equal label
    var ow = new List<V3>(); var ot = new List<int>(); var osa = new List<double[]>();
    for (int i = 0; i + 2 < pc.Length; i += 3) {
      int a = pc[i], b = pc[i + 1], c = pc[i + 2];
      double ax = X[a], ay = Yc[a], bx = X[b] - ax, by = Yc[b] - ay, cx = X[c] - ax, cy = Yc[c] - ay; double det = bx * cy - cx * by; if (Math.Abs(det) < 1e-14) continue;
      var tri = new List<(double x, double y)> { (X[a], Yc[a]), (X[b], Yc[b]), (X[c], Yc[c]) };
      int i0 = Math.Max(0, (int)Math.Floor((tri.Min(p => p.x) - x0) / s)), i1 = Math.Min(nx - 1, (int)Math.Floor((tri.Max(p => p.x) - x0) / s));
      int j0 = Math.Max(0, (int)Math.Floor((tri.Min(p => p.y) - y0) / s)), j1 = Math.Min(ny - 1, (int)Math.Floor((tri.Max(p => p.y) - y0) / s));
      for (int j = j0; j <= j1; j++) {
        int q = i0;
        while (q <= i1) {
          int l = lab[j * nx + q]; int qe = q; while (qe + 1 <= i1 && lab[j * nx + qe + 1] == l) qe++;
          if (l >= 0) {
            // run [q, qe] of row j: clip the triangle to its rectangle (the outer cells reach the triangle's own edge)
            double rx0 = q == i0 ? -1e18 : x0 + q * s, rx1 = qe == i1 ? 1e18 : x0 + (qe + 1) * s, ry0 = j == j0 ? -1e18 : y0 + j * s, ry1 = j == j1 ? 1e18 : y0 + (j + 1) * s;
            var poly = Clip(tri, rx0, rx1, ry0, ry1);
            if (poly.Count >= 3) {
              double pa = 0; for (int z = 0; z < poly.Count; z++) { var p0 = poly[z]; var p1 = poly[(z + 1) % poly.Count]; pa += p0.x * p1.y - p1.x * p0.y; }
              if (Math.Abs(pa) > 2e-8) {
                int bse = ow.Count;
                foreach (var p in poly) { double px = p.x - ax, py = p.y - ay; double b1 = (px * cy - cx * py) / det, b2 = (bx * py - px * by) / det; ow.Add(w[a] + (w[b] - w[a]) * b1 + (w[c] - w[a]) * b2); }
                for (int z = 1; z + 1 < poly.Count; z++) { ot.Add(bse); ot.Add(bse + z); ot.Add(bse + z + 1); osa.Add(cm[l]); }
              }
            }
          }
          q = qe + 1;
        }
      }
    }
    st.Tris += osa.Count;
    return (ow.ToArray(), ot.ToArray(), osa.ToArray());
  }

  /// A convex polygon clipped to an axis-aligned rectangle (Sutherland-Hodgman), keeping the winding.
  static List<(double x, double y)> Clip(List<(double x, double y)> p, double x0, double x1, double y0, double y1) {
    List<(double x, double y)> Edge(List<(double x, double y)> pin, Func<(double x, double y), double> f) {
      var o = new List<(double x, double y)>(); if (pin.Count == 0) return o;
      for (int i = 0; i < pin.Count; i++) {
        var a = pin[i]; var b = pin[(i + 1) % pin.Count]; double fa = f(a), fb = f(b);
        if (fa >= 0) o.Add(a);
        if ((fa >= 0) != (fb >= 0)) { double t = fa / (fa - fb); o.Add((a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t)); }
      }
      return o;
    }
    var r = p;
    if (x0 > -1e17) r = Edge(r, q => q.x - x0);
    if (x1 < 1e17) r = Edge(r, q => x1 - q.x);
    if (y0 > -1e17) r = Edge(r, q => q.y - y0);
    if (y1 < 1e17) r = Edge(r, q => y1 - q.y);
    return r;
  }

  /// The leaf colour of a tree material (contract C5): the texture mean over the texels that pass the alpha test x the
  /// colour (the platemat avg= definition), from the whole texture (mip 0).
  public static double[] LeafAvg(TexInfo ti) {
    if (!ti.Ok) return null; var p = ti.Base.Mips[0]; double r = 0, g = 0, b = 0; int n = 0;
    for (int i = 0; i + 3 < p.Length; i += 4) { double a = p[i + 3] / 255.0; if (ti.Cutout && a * ti.Col[3] < ti.Cut) continue; r += p[i]; g += p[i + 1]; b += p[i + 2]; n++; }
    if (n == 0) return null;
    return new[] { Math.Clamp(r / n / 255 * ti.Col[0], 0, 1), Math.Clamp(g / n / 255 * ti.Col[1], 0, 1), Math.Clamp(b / n / 255 * ti.Col[2], 0, 1) };
  }
}

/// pngprofile <png> <x> <y0> <y1>: one column's luminance of a local comparison PNG (a reference render), to measure
/// a repeat in the picture (8-bit RGB/RGBA, non-interlaced). Debug only; prints numbers.
static class RcPng {
  public static void Profile(string path, int x, int y0, int y1) {
    var b = File.ReadAllBytes(path); int w = 0, h = 0, ct = 0; var idat = new MemoryStream(); int p = 8;
    while (p < b.Length) {
      int len = (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]; string type = System.Text.Encoding.ASCII.GetString(b, p + 4, 4);
      if (type == "IHDR") { w = (b[p + 8] << 24) | (b[p + 9] << 16) | (b[p + 10] << 8) | b[p + 11]; h = (b[p + 12] << 24) | (b[p + 13] << 16) | (b[p + 14] << 8) | b[p + 15]; ct = b[p + 17]; }
      if (type == "IDAT") idat.Write(b, p + 8, len);
      p += 12 + len;
    }
    idat.Position = 0; using var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress); var raw = new MemoryStream(); z.CopyTo(raw); var r = raw.ToArray();
    int bpp = ct == 6 ? 4 : 3, stride = w * bpp; var img = new byte[h * stride]; var prev = new byte[stride];
    for (int y = 0; y < h; y++) {
      int f = r[y * (stride + 1)]; var cur = new byte[stride];
      for (int i = 0; i < stride; i++) {
        int a = i >= bpp ? cur[i - bpp] : 0, up = prev[i], c = i >= bpp ? prev[i - bpp] : 0, v = r[y * (stride + 1) + 1 + i];
        int pr = f switch { 0 => 0, 1 => a, 2 => up, 3 => (a + up) / 2, 4 => Paeth(a, up, c), _ => 0 };
        cur[i] = (byte)(v + pr);
      }
      Buffer.BlockCopy(cur, 0, img, y * stride, stride); prev = cur;
    }
    Console.WriteLine(string.Join(" ", Enumerable.Range(y0, y1 - y0).Select(y => { int i = y * stride + x * bpp; return ((int)(0.2126 * img[i] + 0.7152 * img[i + 1] + 0.0722 * img[i + 2])).ToString(); })));
  }
  static int Paeth(int a, int b, int c) { int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c); return pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
}
