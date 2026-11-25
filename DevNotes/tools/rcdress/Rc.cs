// rcdress: the built-in Rec Center's (Android level 7) dressing, measured for the world. DATA ONLY: the user's Android
// build is read in place by ../apkscene's reader (compiled in by path, unchanged); nothing is installed, executed, loaded
// or copied. Only derived numbers leave the tool (colours per layout plate); no pixels, UVs or vertices are written.
// Commands:
//   platecol <work> <levelN> <layout.rrsk> <out.tsv> <mat,mat,...>
//       for every "G plate <mat>" of the layout whose material is listed: the mean colour the level draws on that
//       rectangle - the level's hull triangles of that material that lie in the plate's plane and inside it, sampled
//       through their own UVs on the material's main texture (top mip, decoded in memory), times the material colour.
//       One row per plate: mat, centre tokens, rotation tokens, e=, r g b (0..1, the texture's stored sRGB), the share of
//       the plate the triangles cover, the colour spread (rms), the darkest/lightest quarter means.
//   meshcol <work> <levelN> <goNameRe> <mat>
//       the colours a level object's mesh shows (its own UVs on its material's texture), grouped by face direction:
//       per group the area and mean colour (debugging the kiosk counter and other level-only props).
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

static class Paths {
  public const string Install = @"E:\SteamLibrary\steamapps\common\RecRoom";
  public static string BundleDir => Install + @"\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64";
  public static string Idx = Environment.GetEnvironmentVariable("RRTOOL_IDX") ?? @"D:\Dreamport\Rec Room\Rec Room\DevNotes\rrtoolidx";
  public static string Apk = Environment.GetEnvironmentVariable("APK_DIR") ?? @"D:\Dreamport\Rec Room\Rec Room\DevNotes\apkwork\apk";
  public static string DataApk => Path.Combine(Apk, "UnityDataAssetPack.apk");
}

static class Program {
  static int Main(string[] a) {
    System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    if (a.Length < 3) { Console.WriteLine("rcdress platecol <work> <levelN> <layout.rrsk> <out.tsv> <mats> | meshcol <work> <levelN> <goNameRe> <mat>"); return 1; }
    Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll"));
    switch (a[0]) {
      case "platecol": PlateCol.Run(a[1], a[2], a[3], a[4], a[5].Split(',')); break;
      case "meshcol": PlateCol.MeshCol(a[1], a[2], a[3], a[4]); break;
      case "rends": RendsCmd.Run(a[1], a[2]); break;
      case "pieces": PiecesCmd.Run(a[1], a[2], a[3], a.Length > 4 ? int.Parse(a[4]) : 1); break;
      default: Console.WriteLine("unknown command"); return 1;
    }
    return 0;
  }
}

/// A level mesh with its first UV set, split by submesh.
static class UvGeo {
  static int FmtSize(int f) => f switch { 0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 2, 5 => 2, 6 => 1, 7 => 1, 8 => 2, 9 => 2, 10 => 4, 11 => 4, _ => 4 };
  public static (V3[] v, double[] uv, List<int[]> subs) Decode(Ld L, MeshInfo mi) {
    if (mi == null) return (null, null, null);
    try {
      var f = L.File(mi.File); var d = L.Read(f, f.ById[mi.Pid]);
      var vd = d.Dct("m_VertexData"); int n = (int)vd.Num("m_VertexCount");
      var chans = vd.Lst("m_Channels").Cast<Dictionary<string, object>>().Select(x => (s: (int)x.Num("stream"), o: (int)x.Num("offset"), fmt: (int)x.Num("format"), dim: (int)x.Num("dimension") & 15)).ToList();
      byte[] data = vd.G("m_DataSize") as byte[];
      if (data == null || data.Length == 0) {
        var sd = d.Dct("m_StreamData"); var path = Ld.Norm(sd?.Str("path")); long off = (long)(sd?.Num("offset") ?? 0); long size = (long)(sd?.Num("size") ?? 0);
        if (size > 0 && path != null && L.D.Has(path)) { var all = L.D.Get(path); data = new byte[size]; Buffer.BlockCopy(all, (int)off, data, 0, (int)size); }
      }
      if (data == null || n == 0 || chans.Count < 5 || chans[0].dim < 3 || chans[4].dim < 2) return (null, null, null);
      int maxS = chans.Where(x => x.dim > 0).Max(x => x.s);
      var sOff = new int[maxS + 1]; var sStride = new int[maxS + 1]; int o = 0;
      for (int s = 0; s <= maxS; s++) { sOff[s] = o; sStride[s] = chans.Where(x => x.s == s && x.dim > 0).Select(x => x.o + FmtSize(x.fmt) * x.dim).DefaultIfEmpty(0).Max(); o += sStride[s] * n; o = (o + 15) & ~15; }
      double Rd(int fmt, int b) => fmt switch {
        0 => BitConverter.ToSingle(data, b), 1 => (double)BitConverter.ToHalf(data, b),
        2 => data[b] / 255.0, 3 => (sbyte)data[b] / 127.0, 4 => BitConverter.ToUInt16(data, b) / 65535.0, 5 => BitConverter.ToInt16(data, b) / 32767.0, _ => double.NaN };
      var p = chans[0]; var v = new V3[n];
      for (int i = 0; i < n; i++) { int b = sOff[p.s] + i * sStride[p.s] + p.o; int sz = FmtSize(p.fmt); v[i] = new V3(Rd(p.fmt, b), Rd(p.fmt, b + sz), Rd(p.fmt, b + 2 * sz)); }
      var t = chans[4]; var uv = new double[n * 2];
      for (int i = 0; i < n; i++) { int b = sOff[t.s] + i * sStride[t.s] + t.o; int sz = FmtSize(t.fmt); uv[i * 2] = Rd(t.fmt, b); uv[i * 2 + 1] = Rd(t.fmt, b + sz); }
      var ib = d.G("m_IndexBuffer") as byte[]; bool i32 = d.Num("m_IndexFormat") == 1;
      var subs = new List<int[]>();
      foreach (var sx in d.Lst("m_SubMeshes").Cast<Dictionary<string, object>>()) {
        var tl = new List<int>();
        if (sx.Num("topology") == 0) {
          int fb = (int)sx.Num("firstByte"), ic = (int)sx.Num("indexCount"), bv = (int)sx.Num("baseVertex");
          for (int k = 0; k < ic; k++) tl.Add((i32 ? BitConverter.ToInt32(ib, fb + k * 4) : BitConverter.ToUInt16(ib, fb + k * 2)) + bv);
        }
        subs.Add(tl.ToArray());
      }
      return (v, uv, subs);
    } catch { return (null, null, null); }
  }
}

sealed class Tex {
  public byte[] Px; public int W, H; public double Sx = 1, Sy = 1, Ox, Oy; public double[] Col = { 1, 1, 1 }; public string Name;
  public double[] At(double u, double v) {
    u = u * Sx + Ox; v = v * Sy + Oy;
    u -= Math.Floor(u); v -= Math.Floor(v);
    int x = Math.Min(W - 1, (int)(u * W)), y = Math.Min(H - 1, (int)(v * H));
    int i = (y * W + x) * 4;
    return new[] { Px[i] / 255.0 * Col[0], Px[i + 1] / 255.0 * Col[1], Px[i + 2] / 255.0 * Col[2] };
  }
}

struct Tri { public V3 a, b, c, n; public double area; public double ua, va, ub, vb, uc, vc; public Tex T; public V3 mn, mx; }

static class PlateCol {
  static V3 Cross(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  static double Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
  static readonly string[] TexSlots = { "_BaseMap", "_MainTex", "_MainTexture", "_Albedo", "_AlbedoMap", "_BaseColorMap", "_Diffuse", "_DiffuseMap", "_Tex" };
  static readonly string[] ColProps = { "_BaseColor", "_Color", "_MainColor", "_TintColor" };
  static readonly Dictionary<(string, long), Tex> texCache = new();

  public static Tex TexOf(Ld L, AF mf, AObj mo, string name) {
    if (texCache.TryGetValue((mf.Name, mo.PathId), out var c)) return c;
    Tex r = null;
    var md = L.Read(mf, mo);
    var sp = md?.Dct("m_SavedProperties");
    if (sp != null) {
      var cols = new Dictionary<string, double[]>(); foreach (var x in sp.Lst("m_Colors") ?? new()) { var p = (Dictionary<string, object>)x; var v = p.Dct("second"); cols[p.G("first")?.ToString() ?? ""] = new[] { v.Num("r"), v.Num("g"), v.Num("b") }; }
      foreach (var slot in TexSlots) {
        foreach (var x in sp.Lst("m_TexEnvs") ?? new()) {
          var p = (Dictionary<string, object>)x; if (p.G("first")?.ToString() != slot) continue;
          var v = p.Dct("second"); var ptr = v.Ptr("m_Texture"); if (ptr.pid == 0) continue;
          var (tf, to) = L.Res(mf, ptr); if (to == null) continue;
          var px = TexStat.Pixels(L, tf, to, out int w, out int h); if (px == null) continue;
          var s = v.Dct("m_Scale"); var o = v.Dct("m_Offset");
          r = new Tex { Px = px, W = w, H = h, Sx = s?.Num("x", 1) ?? 1, Sy = s?.Num("y", 1) ?? 1, Ox = o?.Num("x") ?? 0, Oy = o?.Num("y") ?? 0, Name = L.NameOf(mf, ptr) };
          var cp = ColProps.FirstOrDefault(cols.ContainsKey); if (cp != null) r.Col = cols[cp];
          break;
        }
        if (r != null) break;
      }
    }
    if (r == null) Console.WriteLine("  no main texture read for " + name);
    texCache[(mf.Name, mo.PathId)] = r; return r;
  }

  /// The level's enabled, active renderers' triangles of the given materials (hull = meshes not in the install, or all).
  static List<Tri> Tris(Ld L, Model m, HashSet<string> mats, Func<RendN, bool> want) {
    var tris = new List<Tri>();
    foreach (var r in m.Rend) {
      if (r.Mesh == null || r.Lod1 || !want(r)) continue;
      if (!r.Mats.Any(x => x.name != null && mats.Contains(x.name))) continue;
      var w = m.GoW(r.Go); var (lv, uv, subs) = UvGeo.Decode(L, r.Mesh); if (lv == null) { Console.WriteLine("  unreadable " + r.Mesh.Name); continue; }
      var wv = lv.Select(p => w.p + w.r.Rot(w.s.Mul(p))).ToArray();
      var rd = L.Read(m.F, m.F.ById[r.Id]); var mptr = rd?.Lst("m_Materials") ?? new List<object>();
      int nt = 0;
      for (int k = 0; k < subs.Count; k++) {
        string mat = k < r.Mats.Count ? r.Mats[k].name : null; if (mat == null || !mats.Contains(mat) || k >= mptr.Count) continue;
        var (mf, mo) = L.Res(m.F, DX.Ptr(mptr[k])); if (mo == null) continue;
        var tex = TexOf(L, mf, mo, mat); if (tex == null) continue;
        var t = subs[k];
        for (int i = 0; i + 2 < t.Length; i += 3) {
          var a = wv[t[i]]; var b = wv[t[i + 1]]; var c = wv[t[i + 2]];
          var cr = Cross(b - a, c - a); double ar = cr.Len() * 0.5; if (ar < 1e-8) continue;
          tris.Add(new Tri { a = a, b = b, c = c, n = cr * (0.5 / ar), area = ar, T = tex, ua = uv[t[i] * 2], va = uv[t[i] * 2 + 1], ub = uv[t[i + 1] * 2], vb = uv[t[i + 1] * 2 + 1], uc = uv[t[i + 2] * 2], vc = uv[t[i + 2] * 2 + 1],
            mn = V3.Min(a, V3.Min(b, c)), mx = V3.Max(a, V3.Max(b, c)) });
          nt++;
        }
      }
      Console.WriteLine($"  {r.Mesh.Name}: {nt} triangles of {string.Join("/", r.Mats.Select(x => x.name))}");
    }
    return tris;
  }

  static HashSet<string> InstallMeshes(string work) {
    var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in File.ReadLines(Path.Combine(work, "inst_meshes.tsv"))) { var p = line.Split('\t'); if (p.Length > 2) hs.Add(p[2]); }
    return hs;
  }

  static double Num(string s) => double.Parse(s, CultureInfo.InvariantCulture);

  public static void Run(string work, string level, string layout, string outTsv, string[] matList) {
    var mats = new HashSet<string>(matList);
    var L = new Ld(work); var m = Model.Load(L, level);
    var inst = InstallMeshes(work);
    var tris = Tris(L, m, mats, r => r.En && m.Active(r.Go) && !inst.Contains(r.Mesh.Name));
    Console.WriteLine(tris.Count + " level triangles");
    const double cell = 0.5;
    var grid = new Dictionary<(int, int, int), List<int>>();
    for (int i = 0; i < tris.Count; i++) {
      var t = tris[i];
      for (int x = (int)Math.Floor(t.mn.x / cell); x <= (int)Math.Floor(t.mx.x / cell); x++)
      for (int y = (int)Math.Floor(t.mn.y / cell); y <= (int)Math.Floor(t.mx.y / cell); y++)
      for (int z = (int)Math.Floor(t.mn.z / cell); z <= (int)Math.Floor(t.mx.z / cell); z++) {
        if (!grid.TryGetValue((x, y, z), out var l)) grid[(x, y, z)] = l = new List<int>(); l.Add(i);
      }
    }
    var sb = new StringBuilder(); int rows = 0, none = 0;
    sb.Append("# mat\tpos\trot\te\tr g b\tcover\tspread\tdark rgb\tlight rgb\n");
    foreach (var line in File.ReadLines(layout)) {
      if (!line.StartsWith("G plate ")) continue;
      var p = line.Split(' '); string mat = p[2].Replace("%20", " ").Replace("%25", "%"); if (!mats.Contains(mat)) continue;
      var C = new V3(Num(p[3]), Num(p[4]), Num(p[5])); var q = new Q(Num(p[6]), Num(p[7]), Num(p[8]), Num(p[9]));
      string e = p.FirstOrDefault(x => x.StartsWith("e="))?.Substring(2) ?? "0,0"; var ee = e.Split(','); double hx = Num(ee[0]), hy = Num(ee[1]);
      var u = q.Rot(new V3(1, 0, 0)); var v = q.Rot(new V3(0, 1, 0)); var n = q.Rot(new V3(0, 0, 1));
      var ext = u.Abs() * hx + v.Abs() * hy + new V3(0.02, 0.02, 0.02);
      var mn = C - ext; var mx = C + ext;
      var seen = new HashSet<int>();
      double wsum = 0; double[] sum = new double[3]; var samples = new List<(double w, double r, double g, double b)>();
      for (int x = (int)Math.Floor(mn.x / cell); x <= (int)Math.Floor(mx.x / cell); x++)
      for (int y = (int)Math.Floor(mn.y / cell); y <= (int)Math.Floor(mx.y / cell); y++)
      for (int z = (int)Math.Floor(mn.z / cell); z <= (int)Math.Floor(mx.z / cell); z++) {
        if (!grid.TryGetValue((x, y, z), out var l)) continue;
        foreach (var i in l) {
          if (!seen.Add(i)) continue; var t = tris[i];
          if (Math.Abs(Dot(t.n, n)) < 0.95) continue;
          if (Math.Abs(Dot(t.a - C, n)) > 0.02) continue;
          double edge = Math.Max((t.b - t.a).Len(), Math.Max((t.c - t.b).Len(), (t.a - t.c).Len()));
          int S = Math.Clamp((int)Math.Ceiling(edge / 0.02), 2, 60); double wPt = t.area / (S * S);
          for (int ii = 0; ii < S; ii++) for (int jj = 0; jj < S - ii; jj++) {
            // two sub-triangles per cell; their centroids
            for (int kk = 0; kk < 2; kk++) {
              double b1, b2;
              if (kk == 0) { b1 = (ii + 1.0 / 3) / S; b2 = (jj + 1.0 / 3) / S; } else { if (ii + jj + 1 >= S) continue; b1 = (ii + 2.0 / 3) / S; b2 = (jj + 2.0 / 3) / S; }
              double b0 = 1 - b1 - b2;
              var P = t.a * b0 + t.b * b1 + t.c * b2; var d = P - C;
              if (Math.Abs(Dot(d, u)) > hx || Math.Abs(Dot(d, v)) > hy) continue;
              var col = t.T.At(t.ua * b0 + t.ub * b1 + t.uc * b2, t.va * b0 + t.vb * b1 + t.vc * b2);
              wsum += wPt; sum[0] += col[0] * wPt; sum[1] += col[1] * wPt; sum[2] += col[2] * wPt; samples.Add((wPt, col[0], col[1], col[2]));
            }
          }
        }
      }
      double area = 4 * hx * hy;
      if (wsum <= 0) { none++; sb.Append($"{p[2]}\t{p[3]} {p[4]} {p[5]}\t{p[6]} {p[7]} {p[8]} {p[9]}\t{e}\t-\t0\t0\t-\t-\n"); rows++; continue; }
      double R = sum[0] / wsum, G = sum[1] / wsum, B = sum[2] / wsum;
      double var = samples.Sum(s => s.w * ((s.r - R) * (s.r - R) + (s.g - G) * (s.g - G) + (s.b - B) * (s.b - B))) / wsum;
      var byL = samples.OrderBy(s => s.r * 0.3 + s.g * 0.59 + s.b * 0.11).ToList();
      string Q4(IEnumerable<(double w, double r, double g, double b)> xs) { var a = xs.ToList(); double ww = a.Sum(s => s.w); return ww <= 0 ? "-" : $"{a.Sum(s => s.r * s.w) / ww:0.###},{a.Sum(s => s.g * s.w) / ww:0.###},{a.Sum(s => s.b * s.w) / ww:0.###}"; }
      int qn = Math.Max(1, byL.Count / 4);
      sb.Append($"{p[2]}\t{p[3]} {p[4]} {p[5]}\t{p[6]} {p[7]} {p[8]} {p[9]}\t{e}\t{R:0.###} {G:0.###} {B:0.###}\t{Math.Min(9.99, wsum / area):0.##}\t{Math.Sqrt(var):0.###}\t{Q4(byL.Take(qn))}\t{Q4(byL.Skip(byL.Count - qn))}\n");
      rows++;
    }
    File.WriteAllText(outTsv, sb.ToString());
    Console.WriteLine($"{rows} plates written to {outTsv} ({none} with no level triangle found)");
  }

  public static void MeshCol(string work, string level, string goRe, string mat) {
    var L = new Ld(work); var m = Model.Load(L, level); var re = new Regex(goRe);
    var ids = m.Gos.Values.Where(g => re.IsMatch(g.Name)).Select(g => g.Id).ToHashSet();
    var tris = Tris(L, m, new HashSet<string> { mat }, r => ids.Contains(r.Go));
    // group by snapped normal and by colour (1/8 steps)
    var groups = new Dictionary<string, (double a, double r, double g, double b, V3 mn, V3 mx)>();
    foreach (var t in tris) {
      var c = t.T.At((t.ua + t.ub + t.uc) / 3, (t.va + t.vb + t.vc) / 3);
      string nk = $"{Math.Round(t.n.x, 1):0.0},{Math.Round(t.n.y, 1):0.0},{Math.Round(t.n.z, 1):0.0}";
      string ck = $"{Math.Round(c[0] * 8) / 8:0.###},{Math.Round(c[1] * 8) / 8:0.###},{Math.Round(c[2] * 8) / 8:0.###}";
      string k = nk + " | " + ck;
      groups.TryGetValue(k, out var gq);
      if (gq.a == 0) { gq.mn = V3.Min(t.a, V3.Min(t.b, t.c)); gq.mx = V3.Max(t.a, V3.Max(t.b, t.c)); }
      gq.a += t.area; gq.r += c[0] * t.area; gq.g += c[1] * t.area; gq.b += c[2] * t.area; gq.mn = V3.Min(gq.mn, V3.Min(t.a, V3.Min(t.b, t.c))); gq.mx = V3.Max(gq.mx, V3.Max(t.a, V3.Max(t.b, t.c)));
      groups[k] = gq;
    }
    foreach (var kv in groups.OrderByDescending(x => x.Value.a)) {
      var g = kv.Value; Console.WriteLine($"{g.a,8:0.000} m2  n {kv.Key.Split('|')[0]} col {g.r / g.a:0.###},{g.g / g.a:0.###},{g.b / g.a:0.###}  box {g.mn} .. {g.mx}");
    }
  }
}
