// gtscene: Quest for the Golden Trophy's Android levels -> the world's scene data. DATA ONLY: the user's Android build
// is read in place by ../apkscene's reader (compiled in by path, unchanged); nothing is installed, executed, loaded or
// copied. The output is derived names and numbers (RRSK 1 text, README.md):
//   - everything ../apkscene convert writes (install renderers, prefabs, lights, markers, fog/ambient/sky), except
//     - the level's hull (the editor-merged static batches "<Room>StaticBatch_<material>" and ProBuilder meshes,
//       which the PC install does not have): re-measured as exact planar rectangles, "G plate <material> ..." (one
//       visual quad each, facing its normal) and, for the hull's mesh colliders, "C box" plates 0.2 m deep behind
//       each collider face. apkscene's 2-3 m voxel boxes / 2 m height grids for them are left out;
//     - the other stand-ins (meshes not in the install): "G dress <mesh> ... e= c= m=" (bounds + material), so the
//       quest rebuilds them by kind (torches, banners, scaffolding, tables...);
//   - gameplay markers apkscene does not know: [DoorTrigger] (the gather platform), the exit door, the boulder, lava;
//   - from the REF2 Studio port of the same level (same frame, checked on the player spawns): its enemy spawn points
//     and level effects (F records), which the Android level keeps in circuits / does not have.
// jt=1 (The Rise of Jumbotron, DevNotes/iter11/geometry.md; without it the output is byte-identical to before):
//   - hull: per 3D piece and plane, rectangles on each face's own outline axes (Plates.OfJt), from 1 cm, every
//     material; staircases only where a face's uncovered part is >= 0.02 m2 (cells any of whose 5 test points is inside the face, so a
//     diagonal edge overshoots by <= one 2-5 cm cell instead of leaving see-through teeth; iter11 integrate); small curved pieces (< 0.6 m, < 70 %
//     rectangles) left out, big ones as one fitted rectangle per triangle pair (Plates.Approx); small many-sided pieces
//     that fill their oriented box (stage-light cans, glow sticks) as one "G dress <batch>_box"; the mist blobs
//     (Scifi_AreaFog_*) as fitted rectangles; mirrored transforms' winding turned; cull-off materials drawn both ways;
//   - stand-ins: re-measured from their own mesh and materials (box / boxes / plates / mixed / vox / out, Remeasure);
//     only Quest_Jumbotron_Marquee stays a bounds box ("G dress" as before);
//   - stats=<file.tsv>: per hull submesh and stand-in, triangle / rectangle / exact areas and the outcome.
//   Knobs (environment, for trials): GTJT_REM (0.02 m2), GTJT_TAIL (0.97 of a prop's rectangle area kept),
//   GTJT_NOREFINE=1, GTJT_NOBOX=1, GTJT_STAIRCENTRE=1 (the iteration-11 geometry centre test for staircases), GTJT_DEBUG=<stand-in mesh>, GTJT_DEBUGMAT=<hull material> (per-group dumps).
// Commands:
//   prep <work> <levelN> <Name> <out.rrsk> [ref=<REF2.rrsk>] [jt=1] [stats=<file.tsv>]
//   render <in.rrsk> <out.png> cam=x,y,z look=x,y,z [fov=70] [w=960] [h=540] [col=1] [top=x0,z0,x1,z1,ppm] [skip=<material regex>]
//          (prints the background pixel count: what the plates leave see-through) [avg=1: platemats in their avg= colour]
//   cull <work> <levelN> [levelN ...]   the level materials' face culling (which surfaces Rec Room draws two-sided)
//   prep ... jt=1 poly=1   the hull as planar polygons ("G poly", GtPoly.cs; curved pieces keep their fitted rectangles);
//          poly=2: curved pieces too (one polygon per facet)
//   polymerge <layout.rrsk> <base_jt.rrsk> <poly_jt.rrsk> <out.rrsk>   a post-processed layout (trees, terrain, matdesc...) with
//          its hull rectangles (the base jt=1 prep's hull "G plate" lines) replaced by the poly=1 prep's hull lines (GtPoly.cs)
//   float <in.rrsk> <out.rrsk> <inst_meshes.tsv> [tol=] [snap=] [hang=] [skip=] [keep=] [drop=] [support=] [report=]   (jt rooms,
//          iteration 13) the support test: each floating group of drawn pieces kept (own collider), lowered, snapped, hung
//          by a string or left out (GtFloat)
// jt=1 also (iteration 13): an R record whose install mesh (the first of its name, as the runtime loads it) is not the
//   level's mesh (a name clash: the install's two "Cube" meshes) is re-measured from the level's mesh like a stand-in.
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
  public static string Idx = Environment.GetEnvironmentVariable("RRTOOL_IDX") ?? @"C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad\rrtool\idx";
  public static string Apk = Environment.GetEnvironmentVariable("APK_DIR") ?? @"C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\1c0947a6-bfa3-41e9-ae24-3394d8a31490\scratchpad\apk";
  public static string DataApk => Path.Combine(Apk, "UnityDataAssetPack.apk");
}

static class Program {
  static int Main(string[] a) {
    System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    if (a.Length < (a.Length > 0 && a[0] == "polycheck" ? 2 : 3)) { Console.WriteLine("gtscene prep <work> <levelN> <Name> <out.rrsk> [ref=<REF2.rrsk>] | render <in.rrsk> <out.png> cam=x,y,z look=x,y,z [fov=] [w=] [h=] [top=x0,z0,x1,z1,ppm]"); return 1; }
    switch (a[0]) {
      case "prep": Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll")); GtPrep.Run(a[1], a[2], a[3], a[4], a.Skip(5).ToArray()); break;
      case "render": GtRender.Run(a[1], a[2], a.Skip(3).ToArray()); break;
      case "probe": case "profile": GtProbe.Run(a); break;
      case "float": GtFloat.Run(a); break;
      case "polymerge": PolyMerge.Run(a); break;
      case "polycheck": PolyCheck.Run(a); break;
      case "find": Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll")); Find.Run(a[1], a[2], a[3], a.Length > 4 ? a[4] : "."); break;
      case "cull": Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll")); GtCull.Run(a[1], a.Skip(2).ToArray()); break;
      default: Console.WriteLine("unknown command"); return 1;
    }
    return 0;
  }
}

/// Per-submesh triangles of a level mesh (the same reader as apkscene's MeshGeo.Decode, split by submesh).
static class SubGeo {
  /// Per-vertex rgb of the last Decode (null when the mesh has no colour channel).
  public static double[] Colors;
  static int FmtSize(int f) => f switch { 0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 2, 5 => 2, 6 => 1, 7 => 1, 8 => 2, 9 => 2, 10 => 4, 11 => 4, _ => 4 };
  public static (V3[] v, List<int[]> subs) Decode(Ld L, MeshInfo mi) {
    if (mi == null) return (null, null);
    try {
      var f = L.File(mi.File); var d = L.Read(f, f.ById[mi.Pid]);
      var vd = d.Dct("m_VertexData"); int n = (int)vd.Num("m_VertexCount");
      var chans = vd.Lst("m_Channels").Cast<Dictionary<string, object>>().Select(x => (s: (int)x.Num("stream"), o: (int)x.Num("offset"), fmt: (int)x.Num("format"), dim: (int)x.Num("dimension") & 15)).ToList();
      byte[] data = vd.G("m_DataSize") as byte[];
      if (data == null || data.Length == 0) {
        var sd = d.Dct("m_StreamData"); var path = Ld.Norm(sd?.Str("path")); long off = (long)(sd?.Num("offset") ?? 0); long size = (long)(sd?.Num("size") ?? 0);
        if (size > 0 && path != null && L.D.Has(path)) { var all = L.D.Get(path); data = new byte[size]; Buffer.BlockCopy(all, (int)off, data, 0, (int)size); }
      }
      if (data == null || n == 0 || chans.Count == 0 || chans[0].dim < 3) return (null, null);
      int maxS = chans.Where(x => x.dim > 0).Max(x => x.s);
      var sOff = new int[maxS + 1]; var sStride = new int[maxS + 1]; int o = 0;
      for (int s = 0; s <= maxS; s++) { sOff[s] = o; sStride[s] = chans.Where(x => x.s == s && x.dim > 0).Select(x => x.o + FmtSize(x.fmt) * x.dim).DefaultIfEmpty(0).Max(); o += sStride[s] * n; o = (o + 15) & ~15; }
      var p = chans[0]; var v = new V3[n];
      for (int i = 0; i < n; i++) {
        int b = sOff[p.s] + i * sStride[p.s] + p.o;
        if (p.fmt == 0) v[i] = new V3(BitConverter.ToSingle(data, b), BitConverter.ToSingle(data, b + 4), BitConverter.ToSingle(data, b + 8));
        else if (p.fmt == 1) v[i] = new V3((double)BitConverter.ToHalf(data, b), (double)BitConverter.ToHalf(data, b + 2), (double)BitConverter.ToHalf(data, b + 4));
        else return (null, null);
      }
      // vertex colours (channel 3): the *VertColor* materials take their colour from them
      Colors = null;
      if (chans.Count > 3 && chans[3].dim >= 3) {
        var cc = chans[3]; var col = new double[n * 3]; bool okc = true;
        for (int i = 0; i < n && okc; i++) {
          int b = sOff[cc.s] + i * sStride[cc.s] + cc.o;
          if (cc.fmt == 2) { col[i * 3] = data[b] / 255.0; col[i * 3 + 1] = data[b + 1] / 255.0; col[i * 3 + 2] = data[b + 2] / 255.0; }
          else if (cc.fmt == 0) { col[i * 3] = BitConverter.ToSingle(data, b); col[i * 3 + 1] = BitConverter.ToSingle(data, b + 4); col[i * 3 + 2] = BitConverter.ToSingle(data, b + 8); }
          else if (cc.fmt == 1) { col[i * 3] = (double)BitConverter.ToHalf(data, b); col[i * 3 + 1] = (double)BitConverter.ToHalf(data, b + 2); col[i * 3 + 2] = (double)BitConverter.ToHalf(data, b + 4); }
          else okc = false;
        }
        if (okc) Colors = col;
      }
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
      return (v, subs);
    } catch { return (null, null); }
  }
}

/// Exact planar rectangles of a triangle soup: triangles are grouped by plane (normal to ~0.1 degree, offset to 1 cm),
/// each group is rasterised on the grid of its own vertex coordinates (so axis-aligned hull faces come out exact;
/// slanted edges are refined on a 5 cm grid) and the covered cells are merged greedily into rectangles.
static class Plates {
  public struct Rect { public V3 c, u, v, n; public double hx, hy; public bool hasCol; public double cr, cg, cb; }
  static V3 Cross(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  static double Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
  static V3 Nrm(V3 a) { var l = a.Len(); return l < 1e-12 ? new V3(0, 1, 0) : a * (1 / l); }
  static V3 Snap(V3 n) {
    if (Math.Abs(n.x) > 0.9998) return new V3(Math.Sign(n.x), 0, 0);
    if (Math.Abs(n.y) > 0.9998) return new V3(0, Math.Sign(n.y), 0);
    if (Math.Abs(n.z) > 0.9998) return new V3(0, 0, Math.Sign(n.z));
    return n;
  }
  public static int Refined;

  public static List<Rect> Of(V3[] w, int[] t, double minSide = 0.02, double[] cols = null) {
    var groups = new Dictionary<(int, int, int, long), List<int>>();
    for (int i = 0; i + 2 < t.Length; i += 3) {
      var a = w[t[i]]; var b = w[t[i + 1]]; var c = w[t[i + 2]];
      var cr = Cross(b - a, c - a); double len = cr.Len(); if (len < 1e-7) continue;
      var n = Snap(cr * (1 / len)); double d = Dot(n, a);
      var key = ((int)Math.Round(n.x * 600), (int)Math.Round(n.y * 600), (int)Math.Round(n.z * 600), (long)Math.Round(d * 100));
      if (!groups.TryGetValue(key, out var li)) groups[key] = li = new List<int>(); li.Add(i);
    }
    var res = new List<Rect>();
    foreach (var g in groups.Values) {
      V3 N = default; double D = 0, A = 0;
      foreach (int i in g) { var a = w[t[i]]; var b = w[t[i + 1]]; var c = w[t[i + 2]]; var cr = Cross(b - a, c - a); double ar = cr.Len() * 0.5; var n = Snap(Nrm(cr)); N = N + n * ar; D += Dot(n, a) * ar; A += ar; }
      if (A < 1e-6) continue;
      N = Snap(Nrm(N)); D /= A;
      V3 U, Vv;
      if (Math.Abs(N.y) > 0.99) { U = new V3(1, 0, 0); Vv = Nrm(Cross(N, U)); U = Nrm(Cross(Vv, N)); }
      else { U = Nrm(Cross(new V3(0, 1, 0), N)); Vv = Nrm(Cross(N, U)); }
      // triangles in plane coordinates
      int nt = g.Count; var ts = new double[nt * 6];
      var S = new List<double>(); var T = new List<double>();
      for (int k = 0; k < nt; k++) {
        int i = g[k];
        for (int j = 0; j < 3; j++) { var p = w[t[i + j]]; double s = Dot(p, U), tt = Dot(p, Vv); ts[k * 6 + j * 2] = s; ts[k * 6 + j * 2 + 1] = tt; S.Add(Math.Round(s, 3)); T.Add(Math.Round(tt, 3)); }
      }
      var sl = S.Distinct().OrderBy(x => x).ToList(); var tl = T.Distinct().OrderBy(x => x).ToList();
      if (sl.Count < 2 || tl.Count < 2) continue;
      var filled = Cover(ts, nt, sl, tl, false, out double fa);
      if (Math.Abs(fa - A) > 0.01 * A + 1e-4) {
        // slanted edges: refine with 5 cm lines inside the group's extent, cells by their centre
        Refined++;
        double step = 0.05; double s0 = sl[0], s1 = sl[^1], t0 = tl[0], t1 = tl[^1];
        if ((s1 - s0) / step * (t1 - t0) / step > 4e6) step = Math.Sqrt((s1 - s0) * (t1 - t0) / 4e6);
        for (double x = Math.Ceiling(s0 / step) * step; x < s1; x += step) sl.Add(Math.Round(x, 3));
        for (double y = Math.Ceiling(t0 / step) * step; y < t1; y += step) tl.Add(Math.Round(y, 3));
        sl = sl.Distinct().OrderBy(x => x).ToList(); tl = tl.Distinct().OrderBy(x => x).ToList();
        filled = Cover(ts, nt, sl, tl, true, out fa);
      }
      int ns = sl.Count - 1, nq = tl.Count - 1; var used = new bool[ns * nq];
      for (int j = 0; j < nq; j++) for (int i = 0; i < ns; i++) {
        int id = j * ns + i; if (!filled[id] || used[id]) continue;
        int i1 = i; while (i1 + 1 < ns && filled[j * ns + i1 + 1] && !used[j * ns + i1 + 1]) i1++;
        int j1 = j; bool ok = true;
        while (ok && j1 + 1 < nq) { for (int x = i; x <= i1; x++) if (!filled[(j1 + 1) * ns + x] || used[(j1 + 1) * ns + x]) { ok = false; break; } if (ok) j1++; }
        for (int y = j; y <= j1; y++) for (int x = i; x <= i1; x++) used[y * ns + x] = true;
        double ss0 = sl[i], ss1 = sl[i1 + 1], tt0 = tl[j], tt1 = tl[j1 + 1];
        if (ss1 - ss0 < minSide || tt1 - tt0 < minSide) continue;
        double sc = (ss0 + ss1) * 0.5, tc = (tt0 + tt1) * 0.5;
        var rr = new Rect { c = U * sc + Vv * tc + N * D, u = U, v = Vv, n = N, hx = (ss1 - ss0) * 0.5, hy = (tt1 - tt0) * 0.5 };
        if (cols != null) {
          // the vertex colour of the triangle under the rectangle's centre (Rec Room's VertColor walls: one colour per face)
          for (int k = 0; k < nt; k++) {
            if (!In(sc, tc, ts[k * 6], ts[k * 6 + 1], ts[k * 6 + 2], ts[k * 6 + 3], ts[k * 6 + 4], ts[k * 6 + 5])) continue;
            int v0 = t[g[k]], v1 = t[g[k] + 1], v2 = t[g[k] + 2];
            rr.cr = (cols[v0 * 3] + cols[v1 * 3] + cols[v2 * 3]) / 3; rr.cg = (cols[v0 * 3 + 1] + cols[v1 * 3 + 1] + cols[v2 * 3 + 1]) / 3; rr.cb = (cols[v0 * 3 + 2] + cols[v1 * 3 + 2] + cols[v2 * 3 + 2]) / 3; rr.hasCol = true; break;
          }
        }
        res.Add(rr);
      }
    }
    return res;
  }

  /// Counters of the Jumbotron rasteriser (per call site).
  public sealed class JtStat { public int Groups, Comps, Subs, Exact, Refined, Rects; public double TriArea, RectArea, ExactArea; public List<string> Dbg;
    public void Add(JtStat o) { Groups += o.Groups; Comps += o.Comps; Subs += o.Subs; Exact += o.Exact; Refined += o.Refined; Rects += o.Rects; TriArea += o.TriArea; RectArea += o.RectArea; ExactArea += o.ExactArea; }
  }
  /// The 3D connected pieces of a triangle list (triangles sharing a vertex position, 1 mm): the props of a merged batch.
  public static List<int[]> Pieces(V3[] w, int[] t) {
    int nt = t.Length / 3; var par = new int[nt]; for (int k = 0; k < nt; k++) par[k] = k;
    int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
    var at = new Dictionary<(long, long, long), int>();
    for (int k = 0; k < nt; k++) for (int j = 0; j < 3; j++) {
      var p = w[t[k * 3 + j]]; var key = ((long)Math.Round(p.x * 1000), (long)Math.Round(p.y * 1000), (long)Math.Round(p.z * 1000));
      if (at.TryGetValue(key, out int o)) { int ra = Find(o), rb = Find(k); if (ra != rb) par[ra] = rb; } else at[key] = k;
    }
    var map = new Dictionary<int, List<int>>();
    for (int k = 0; k < nt; k++) { int r = Find(k); if (!map.TryGetValue(r, out var l)) map[r] = l = new List<int>(); l.Add(t[k * 3]); l.Add(t[k * 3 + 1]); l.Add(t[k * 3 + 2]); }
    return map.Values.Select(l => l.ToArray()).ToList();
  }
  /// The largest side of a triangle list's world bounds.
  public static double Extent(V3[] w, int[] t) {
    double x0 = 1e18, y0 = 1e18, z0 = 1e18, x1 = -1e18, y1 = -1e18, z1 = -1e18;
    foreach (int i in t) { var p = w[i]; x0 = Math.Min(x0, p.x); y0 = Math.Min(y0, p.y); z0 = Math.Min(z0, p.z); x1 = Math.Max(x1, p.x); y1 = Math.Max(y1, p.y); z1 = Math.Max(z1, p.z); }
    return Math.Max(x1 - x0, Math.Max(y1 - y0, z1 - z0));
  }

  /// The Jumbotron's rectangles (prep jt=1): as Of, but each plane group is split into its connected pieces, and each
  /// piece's triangles are rasterised on the basis of their own outline: every triangle takes the in-plane direction
  /// (mod 90 degrees) of its edges that the piece's outline (boundary edges, by length) uses most, so a chamfer, a 45
  /// degree octagon strip or a slanted brace becomes one exact plate on its own axes instead of a 5 cm staircase on the
  /// room's axes. Only what no rectangle can be (triangle caps, curves) is refined, on a grid of 2-5 cm (1/4 of the
  /// piece's smaller side), cells by their centre (refine = false, for props: the exact cells only; a cover within 10 cm2
  /// of exact counts as exact everywhere). Rectangles are kept from minSide (1 cm), in every material.
  public static double MinStair = double.TryParse(Environment.GetEnvironmentVariable("GTJT_REM"), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) ? ms : 0.02;
  public static List<Rect> OfJt(V3[] w, int[] t, double minSide, double[] cols, JtStat js, bool refine = true) {
    var groups = new Dictionary<(int, int, int, long), List<int>>();
    for (int i = 0; i + 2 < t.Length; i += 3) {
      var a = w[t[i]]; var b = w[t[i + 1]]; var c = w[t[i + 2]];
      var cr = Cross(b - a, c - a); double len = cr.Len(); if (len < 1e-7) continue;
      var n = Snap(cr * (1 / len)); double d = Dot(n, a);
      var key = ((int)Math.Round(n.x * 600), (int)Math.Round(n.y * 600), (int)Math.Round(n.z * 600), (long)Math.Round(d * 100));
      if (!groups.TryGetValue(key, out var li)) groups[key] = li = new List<int>(); li.Add(i);
    }
    var res = new List<Rect>();
    foreach (var g in groups.Values) {
      V3 N = default; double D = 0, A = 0;
      foreach (int i in g) { var a = w[t[i]]; var b = w[t[i + 1]]; var c = w[t[i + 2]]; var cr = Cross(b - a, c - a); double ar = cr.Len() * 0.5; var n = Snap(Nrm(cr)); N = N + n * ar; D += Dot(n, a) * ar; A += ar; }
      if (A < 1e-6) continue;
      js.Groups++; js.TriArea += A;
      N = Snap(Nrm(N)); D /= A;
      V3 U, Vv;
      if (Math.Abs(N.y) > 0.99) { U = new V3(1, 0, 0); Vv = Nrm(Cross(N, U)); U = Nrm(Cross(Vv, N)); }
      else { U = Nrm(Cross(new V3(0, 1, 0), N)); Vv = Nrm(Cross(N, U)); }
      int nt = g.Count; var ts = new double[nt * 6]; var vk = new (long, long)[nt * 3];
      for (int k = 0; k < nt; k++) for (int j = 0; j < 3; j++) {
        var p = w[t[g[k] + j]]; double s = Dot(p, U), tt = Dot(p, Vv); ts[k * 6 + j * 2] = s; ts[k * 6 + j * 2 + 1] = tt;
        vk[k * 3 + j] = ((long)Math.Round(s * 1000), (long)Math.Round(tt * 1000));
      }
      // connected pieces (triangles sharing a vertex position, 1 mm)
      var par = new int[nt]; for (int k = 0; k < nt; k++) par[k] = k;
      int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
      var firstAt = new Dictionary<(long, long), int>();
      for (int k = 0; k < nt; k++) for (int j = 0; j < 3; j++) { if (firstAt.TryGetValue(vk[k * 3 + j], out int o)) { int ra = Find(o), rb = Find(k); if (ra != rb) par[ra] = rb; } else firstAt[vk[k * 3 + j]] = k; }
      var comps = new Dictionary<int, List<int>>();
      for (int k = 0; k < nt; k++) { int r = Find(k); if (!comps.TryGetValue(r, out var cl)) comps[r] = cl = new List<int>(); cl.Add(k); }
      foreach (var comp in comps.Values) {
        js.Comps++;
        // outline edges (used by one triangle of the piece) -> direction weights (0.1 degree bins, mod 90)
        var eUse = new Dictionary<((long, long), (long, long)), int>();
        ((long, long), (long, long)) EKey((long, long) p, (long, long) q) => p.CompareTo(q) < 0 ? (p, q) : (q, p);
        foreach (int k in comp) for (int j = 0; j < 3; j++) { var e = EKey(vk[k * 3 + j], vk[k * 3 + (j + 1) % 3]); eUse[e] = eUse.GetValueOrDefault(e) + 1; }
        int Bin(int k, int j, out double len) {
          double ds = ts[k * 6 + ((j + 1) % 3) * 2] - ts[k * 6 + j * 2], dt = ts[k * 6 + ((j + 1) % 3) * 2 + 1] - ts[k * 6 + j * 2 + 1];
          len = Math.Sqrt(ds * ds + dt * dt); double deg = Math.Atan2(dt, ds) * 180 / Math.PI; deg = ((deg % 90) + 90) % 90;
          int b = (int)Math.Round(deg * 10) % 900; return b <= 3 || b >= 897 ? 0 : b;
        }
        var wgt = new Dictionary<int, double>();
        foreach (int k in comp) for (int j = 0; j < 3; j++) { if (eUse[EKey(vk[k * 3 + j], vk[k * 3 + (j + 1) % 3])] != 1) continue; int b = Bin(k, j, out double l); wgt[b] = wgt.GetValueOrDefault(b) + l; }
        // clusters of bins (0.3 degrees), heaviest first
        var centres = new List<(int b, double w)>();
        foreach (var kv in wgt.OrderByDescending(x => x.Value)) {
          int hit = centres.FindIndex(c => Math.Min(Math.Abs(c.b - kv.Key), 900 - Math.Abs(c.b - kv.Key)) <= 3);
          if (hit >= 0) centres[hit] = (centres[hit].b, centres[hit].w + kv.Value); else centres.Add((kv.Key, kv.Value));
        }
        int Cl(int b) { for (int c = 0; c < centres.Count; c++) if (Math.Min(Math.Abs(centres[c].b - b), 900 - Math.Abs(centres[c].b - b)) <= 3) return c; return -1; }
        int top = centres.Count == 0 ? -1 : centres.Select((c, i) => (c, i)).OrderByDescending(x => x.c.w).First().i;
        var subs = new Dictionary<int, List<int>>();
        foreach (int k in comp) {
          int best = -1; double bw = -1, bl = -1;
          for (int j = 0; j < 3; j++) { int c = Cl(Bin(k, j, out double l)); if (c < 0) continue; double cw = centres[c].w; if (cw > bw + 1e-9 || (Math.Abs(cw - bw) <= 1e-9 && l > bl)) { best = c; bw = cw; bl = l; } }
          int ang = best >= 0 ? centres[best].b : top >= 0 ? centres[top].b : 0;
          if (!subs.TryGetValue(ang, out var sl0)) subs[ang] = sl0 = new List<int>(); sl0.Add(k);
        }
        foreach (var sg in subs) {
          js.Subs++;
          double th = sg.Key / 10.0 * Math.PI / 180, co = Math.Cos(th), si = Math.Sin(th);
          V3 Ur = sg.Key == 0 ? U : Nrm(U * co + Vv * si), Vr = sg.Key == 0 ? Vv : Nrm(Cross(N, Ur));
          var tri = sg.Value; int m = tri.Count; var rs = new double[m * 6]; double am = 0;
          var S = new List<double>(); var T = new List<double>();
          for (int q = 0; q < m; q++) {
            int k = tri[q];
            for (int j = 0; j < 3; j++) {
              double s0 = ts[k * 6 + j * 2], t0 = ts[k * 6 + j * 2 + 1];
              double s = sg.Key == 0 ? s0 : s0 * co + t0 * si, tt = sg.Key == 0 ? t0 : -s0 * si + t0 * co;
              rs[q * 6 + j * 2] = s; rs[q * 6 + j * 2 + 1] = tt; S.Add(s); T.Add(tt);
            }
            am += Math.Abs((rs[q * 6 + 2] - rs[q * 6]) * (rs[q * 6 + 5] - rs[q * 6 + 1]) - (rs[q * 6 + 4] - rs[q * 6]) * (rs[q * 6 + 3] - rs[q * 6 + 1])) * 0.5;
          }
          // the grid on the vertices' own coordinates (0.1 mm apart at least): a 2 cm trim stays exact
          var sl = Lines(S); var tl = Lines(T);
          if (sl.Count < 2 || tl.Count < 2) continue;
          var filled = Cover(rs, m, sl, tl, false, out double fa);
          // exact, or only slivers short of it (the triangle ends of a mitred strip or brace, <= 10 cm2 in all): the exact cells
          bool ex = Math.Abs(fa - am) <= 0.01 * am + 1e-5 || (fa <= am && am - fa <= 0.001);
          // the sliver case: the cells any of whose 5 test points is inside instead (a mitred strip's triangle ends are covered, a
          // hair past its short side, rather than left as see-through slits between the facets of a ribbed barrel or a frame)
          if (ex && Math.Abs(fa - am) > 0.01 * am + 1e-5) { filled = CoverTouch(rs, m, sl, tl); }
          // refine only where the part no rectangle covers is big enough to see through (MinStair m2); smaller: the exact cells
          bool stair = refine && !ex && am - fa >= MinStair;
          js.ExactArea += Math.Min(fa, am);
          if (stair) {
            js.Refined++;
            double s0 = sl[0], s1 = sl[^1], t0 = tl[0], t1 = tl[^1];
            double step = Math.Clamp(Math.Min(s1 - s0, t1 - t0) / 4, 0.02, 0.05);
            if ((s1 - s0) / step * (t1 - t0) / step > 4e6) step = Math.Sqrt((s1 - s0) * (t1 - t0) / 4e6);
            // refinement lines every step, none closer than step/4 to a vertex line (no sliver cells)
            sl = Refine(sl, step); tl = Refine(tl, step);
            // iteration 11 (integrate): the staircase cells any of whose 5 test points is inside (CoverTouch), not the centre only: a
            // diagonal edge overshoots by at most one 2-5 cm cell (hidden behind the neighbouring face in a room corner) instead of
            // leaving a row of see-through triangles along it (act 4's vestibule edges); GTJT_STAIRCENTRE=1 = the centre test
            filled = Environment.GetEnvironmentVariable("GTJT_STAIRCENTRE") == "1" ? Cover(rs, m, sl, tl, true, out fa) : CoverTouch(rs, m, sl, tl);
          } else if (ex) js.Exact++;
          js.Dbg?.Add($"n={N} d={D:F3} tris={m} area={am:F4} ang={sg.Key / 10.0} exact={ex} cells={sl.Count - 1}x{tl.Count - 1}");
          int ns = sl.Count - 1, nq = tl.Count - 1; var used = new bool[ns * nq];
          for (int j = 0; j < nq; j++) for (int i = 0; i < ns; i++) {
            int id = j * ns + i; if (!filled[id] || used[id]) continue;
            int i1 = i; while (i1 + 1 < ns && filled[j * ns + i1 + 1] && !used[j * ns + i1 + 1]) i1++;
            int j1 = j; bool ok = true;
            while (ok && j1 + 1 < nq) { for (int x = i; x <= i1; x++) if (!filled[(j1 + 1) * ns + x] || used[(j1 + 1) * ns + x]) { ok = false; break; } if (ok) j1++; }
            for (int y = j; y <= j1; y++) for (int x = i; x <= i1; x++) used[y * ns + x] = true;
            double ss0 = sl[i], ss1 = sl[i1 + 1], tt0 = tl[j], tt1 = tl[j1 + 1];
            if (ss1 - ss0 < minSide * 0.999 || tt1 - tt0 < minSide * 0.999) continue;
            double sc = (ss0 + ss1) * 0.5, tc = (tt0 + tt1) * 0.5;
            var rr = new Rect { c = Ur * sc + Vr * tc + N * D, u = Ur, v = Vr, n = N, hx = (ss1 - ss0) * 0.5, hy = (tt1 - tt0) * 0.5 };
            if (cols != null) {
              for (int q = 0; q < m; q++) {
                if (!In(sc, tc, rs[q * 6], rs[q * 6 + 1], rs[q * 6 + 2], rs[q * 6 + 3], rs[q * 6 + 4], rs[q * 6 + 5])) continue;
                int ti = g[tri[q]]; int v0 = t[ti], v1 = t[ti + 1], v2 = t[ti + 2];
                rr.cr = (cols[v0 * 3] + cols[v1 * 3] + cols[v2 * 3]) / 3; rr.cg = (cols[v0 * 3 + 1] + cols[v1 * 3 + 1] + cols[v2 * 3 + 1]) / 3; rr.cb = (cols[v0 * 3 + 2] + cols[v1 * 3 + 2] + cols[v2 * 3 + 2]) / 3; rr.hasCol = true; break;
              }
            }
            res.Add(rr); js.Rects++; js.RectArea += rr.hx * rr.hy * 4;
          }
        }
      }
    }
    return res;
  }

  /// A curved surface (jt=1: a hull piece or prop that is not rectangles) as one fitted rectangle per triangle pair: each
  /// triangle is paired with the neighbour across its longest edge (the quad's diagonal, both ways, < 25 degrees apart);
  /// the quad's rectangle runs along the mean of its opposite edges, sized to their mean lengths, at the quad's centre
  /// (neighbouring rectangles meet along the shared edges up to the curvature). A lone triangle: a rectangle of its area
  /// on its longest edge's axis at its centroid.
  public static List<Rect> Approx(V3[] w, int[] t, double minSide, double[] cols) {
    int nt = t.Length / 3; var res = new List<Rect>();
    (long, long, long) K(int vi) { var p = w[vi]; return ((long)Math.Round(p.x * 1000), (long)Math.Round(p.y * 1000), (long)Math.Round(p.z * 1000)); }
    var nn = new V3[nt]; var ar = new double[nt]; var le = new int[nt]; var ok = new bool[nt];
    var edges = new Dictionary<((long, long, long), (long, long, long)), List<int>>();
    ((long, long, long), (long, long, long)) EK(int a, int b) { var ka = K(a); var kb = K(b); return ka.CompareTo(kb) < 0 ? (ka, kb) : (kb, ka); }
    for (int k = 0; k < nt; k++) {
      var a = w[t[k * 3]]; var b = w[t[k * 3 + 1]]; var c = w[t[k * 3 + 2]]; var cr = Cross(b - a, c - a); double l = cr.Len(); if (l < 1e-9) continue;
      ok[k] = true; nn[k] = cr * (1 / l); ar[k] = l * 0.5;
      double l0 = (b - a).Len(), l1 = (c - b).Len(), l2 = (a - c).Len(); le[k] = l0 >= l1 && l0 >= l2 ? 0 : l1 >= l2 ? 1 : 2;
      var e = EK(t[k * 3 + le[k]], t[k * 3 + (le[k] + 1) % 3]); if (!edges.TryGetValue(e, out var li)) edges[e] = li = new List<int>(); li.Add(k);
    }
    var partner = new int[nt]; Array.Fill(partner, -1);
    foreach (var li in edges.Values) {
      for (int i = 0; i < li.Count; i++) for (int j = i + 1; j < li.Count; j++) {
        int p = li[i], q = li[j]; if (partner[p] >= 0 || partner[q] >= 0 || Dot(nn[p], nn[q]) < 0.906) continue;
        partner[p] = q; partner[q] = p;
      }
    }
    for (int k = 0; k < nt; k++) {
      if (!ok[k] || (partner[k] >= 0 && partner[k] < k)) continue;
      int e0 = t[k * 3 + le[k]], e1 = t[k * 3 + (le[k] + 1) % 3], ek = t[k * 3 + (le[k] + 2) % 3];
      V3 N, U, V, C; double hx, hy; var vs = new List<int> { e0, e1, ek };
      if (partner[k] >= 0) {
        int o = partner[k]; int eo = t[o * 3 + (le[o] + 2) % 3]; vs.Add(eo);
        V3 A = w[ek], B = w[e0], Cc = w[eo], Dd = w[e1];   // a - b - c - d around the quad, diagonal b-d
        N = Nrm(nn[k] * ar[k] + nn[o] * ar[o]);
        var u0 = (B - A) + (Cc - Dd); u0 = u0 - N * Dot(N, u0); if (u0.Len() < 1e-9) continue; U = Nrm(u0); V = Cross(N, U);
        C = (A + B + Cc + Dd) * 0.25;
        hx = 0.25 * (Math.Abs(Dot(B - A, U)) + Math.Abs(Dot(Cc - Dd, U))); hy = 0.25 * (Math.Abs(Dot(Dd - A, V)) + Math.Abs(Dot(Cc - B, V)));
      } else {
        N = nn[k]; U = Nrm(w[e1] - w[e0]); V = Cross(N, U); double L = (w[e1] - w[e0]).Len(), h = Math.Abs(Dot(w[ek] - w[e0], V));
        C = (w[e0] + w[e1] + w[ek]) * (1.0 / 3); hx = L * 0.375; hy = h / 3;
      }
      if (hx * 2 < minSide * 0.999 || hy * 2 < minSide * 0.999) continue;
      var rr = new Rect { c = C, u = U, v = V, n = N, hx = hx, hy = hy };
      if (cols != null) { foreach (int vi in vs) { rr.cr += cols[vi * 3]; rr.cg += cols[vi * 3 + 1]; rr.cb += cols[vi * 3 + 2]; } rr.cr /= vs.Count; rr.cg /= vs.Count; rr.cb /= vs.Count; rr.hasCol = true; }
      res.Add(rr);
    }
    return res;
  }

  /// The oriented box of a triangle list (jt=1 props): its heaviest face normal, the heaviest one square to it, and their
  /// cross; with the list's area over the box's surface (1 = a closed box, ~0.83 an octagonal tube, 0.5 a flat plate).
  public static (V3 c, V3 x, V3 y, V3 z, V3 e, double fill) Obb(V3[] w, int[] t) {
    var bins = new Dictionary<(int, int, int), (V3 n, double a)>(); double area = 0;
    for (int i = 0; i + 2 < t.Length; i += 3) {
      var cr = Cross(w[t[i + 1]] - w[t[i]], w[t[i + 2]] - w[t[i]]); double l = cr.Len(); if (l < 1e-12) continue; var n = cr * (1 / l); area += l * 0.5;
      if (n.x < -1e-6 || (Math.Abs(n.x) <= 1e-6 && (n.y < -1e-6 || (Math.Abs(n.y) <= 1e-6 && n.z < 0)))) n = n * -1;
      var key = ((int)Math.Round(n.x * 40), (int)Math.Round(n.y * 40), (int)Math.Round(n.z * 40));
      var b = bins.GetValueOrDefault(key); bins[key] = (b.n + n * (l * 0.5), b.a + l * 0.5);
    }
    if (bins.Count == 0) return (default, default, default, default, default, 0);
    var byA = bins.Values.OrderByDescending(b => b.a).ToList(); var X = Nrm(byA[0].n);
    V3 Yv = default; foreach (var b in byA.Skip(1)) { var m = Nrm(b.n); if (Math.Abs(Dot(m, X)) < 0.1) { Yv = Nrm(m - X * Dot(m, X)); break; } }
    if (Yv.Len() < 0.5) { var any = Math.Abs(X.y) < 0.9 ? new V3(0, 1, 0) : new V3(1, 0, 0); Yv = Nrm(any - X * Dot(any, X)); }
    var Z = Cross(X, Yv);
    double[] lo = { 1e18, 1e18, 1e18 }, hi = { -1e18, -1e18, -1e18 };
    foreach (int i in t) { var p = w[i]; double a = Dot(p, X), b = Dot(p, Yv), c = Dot(p, Z); lo[0] = Math.Min(lo[0], a); hi[0] = Math.Max(hi[0], a); lo[1] = Math.Min(lo[1], b); hi[1] = Math.Max(hi[1], b); lo[2] = Math.Min(lo[2], c); hi[2] = Math.Max(hi[2], c); }
    var e = new V3((hi[0] - lo[0]) * 0.5, (hi[1] - lo[1]) * 0.5, (hi[2] - lo[2]) * 0.5);
    var ctr = X * ((lo[0] + hi[0]) * 0.5) + Yv * ((lo[1] + hi[1]) * 0.5) + Z * ((lo[2] + hi[2]) * 0.5);
    double boxA = 8 * (e.x * e.y + e.y * e.z + e.z * e.x);
    return (ctr, X, Yv, Z, e, boxA > 1e-9 ? area / boxA : 0);
  }

  static List<double> Lines(List<double> v) { v.Sort(); var r = new List<double>(); foreach (var x in v) if (r.Count == 0 || x - r[^1] > 1e-4) r.Add(x); return r; }
  static List<double> Refine(List<double> l, double step) {
    var r = new List<double>(l); double a = l[0], b = l[^1]; int j = 0;
    for (double x = Math.Ceiling(a / step) * step; x < b; x += step) { while (j + 1 < l.Count && l[j + 1] <= x) j++; double near = Math.Min(Math.Abs(x - l[j]), j + 1 < l.Count ? Math.Abs(l[j + 1] - x) : 1e9); if (near > step * 0.25) r.Add(x); }
    r.Sort(); return r;
  }
  static bool In(double px, double py, double ax, double ay, double bx, double by, double cx, double cy) {
    double den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy); if (Math.Abs(den) < 1e-14) return false;
    double l1 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / den, l2 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / den, l3 = 1 - l1 - l2;
    const double e = -1e-6; return l1 >= e && l2 >= e && l3 >= e;
  }
  static int Lo(List<double> l, double x) { int i = l.BinarySearch(x); if (i < 0) i = ~i - 1; return Math.Max(0, i); }

  /// Cells of the grid (lines sl x tl) the triangles cover. Exact mode: the cell's centre and 4 points inset 10% from
  /// its corners must all be inside (any) triangle; centre mode: the centre only.
  /// Cells of the grid any triangle touches at one of the 5 test points (jt=1 slivers; see Cover).
  static bool[] CoverTouch(double[] ts, int nt, List<double> sl, List<double> tl) {
    int ns = sl.Count - 1, nq = tl.Count - 1; var f = new bool[ns * nq];
    for (int k = 0; k < nt; k++) {
      double ax = ts[k * 6], ay = ts[k * 6 + 1], bx = ts[k * 6 + 2], by = ts[k * 6 + 3], cx = ts[k * 6 + 4], cy = ts[k * 6 + 5];
      int i0 = Lo(sl, Math.Min(ax, Math.Min(bx, cx)) - 1e-4), i1 = Math.Min(ns - 1, Lo(sl, Math.Max(ax, Math.Max(bx, cx)) + 1e-4));
      int j0 = Lo(tl, Math.Min(ay, Math.Min(by, cy)) - 1e-4), j1 = Math.Min(nq - 1, Lo(tl, Math.Max(ay, Math.Max(by, cy)) + 1e-4));
      for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) {
        int id = j * ns + i; if (f[id]) continue;
        double s0 = sl[i], s1 = sl[i + 1], t0 = tl[j], t1 = tl[j + 1]; double ds = s1 - s0, dt = t1 - t0;
        if (In(s0 + ds * 0.5, t0 + dt * 0.5, ax, ay, bx, by, cx, cy) || In(s0 + ds * 0.1, t0 + dt * 0.1, ax, ay, bx, by, cx, cy) || In(s0 + ds * 0.9, t0 + dt * 0.1, ax, ay, bx, by, cx, cy)
            || In(s0 + ds * 0.1, t0 + dt * 0.9, ax, ay, bx, by, cx, cy) || In(s0 + ds * 0.9, t0 + dt * 0.9, ax, ay, bx, by, cx, cy)) f[id] = true;
      }
    }
    return f;
  }

  static bool[] Cover(double[] ts, int nt, List<double> sl, List<double> tl, bool centre, out double area) {
    int ns = sl.Count - 1, nq = tl.Count - 1; var mask = new byte[ns * nq];
    for (int k = 0; k < nt; k++) {
      double ax = ts[k * 6], ay = ts[k * 6 + 1], bx = ts[k * 6 + 2], by = ts[k * 6 + 3], cx = ts[k * 6 + 4], cy = ts[k * 6 + 5];
      int i0 = Lo(sl, Math.Min(ax, Math.Min(bx, cx)) - 1e-4), i1 = Math.Min(ns - 1, Lo(sl, Math.Max(ax, Math.Max(bx, cx)) + 1e-4));
      int j0 = Lo(tl, Math.Min(ay, Math.Min(by, cy)) - 1e-4), j1 = Math.Min(nq - 1, Lo(tl, Math.Max(ay, Math.Max(by, cy)) + 1e-4));
      for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) {
        double s0 = sl[i], s1 = sl[i + 1], t0 = tl[j], t1 = tl[j + 1]; double ds = s1 - s0, dt = t1 - t0;
        int id = j * ns + i; byte m = mask[id];
        if (centre) { if ((m & 1) == 0 && In(s0 + ds * 0.5, t0 + dt * 0.5, ax, ay, bx, by, cx, cy)) m |= 1; }
        else {
          if ((m & 1) == 0 && In(s0 + ds * 0.5, t0 + dt * 0.5, ax, ay, bx, by, cx, cy)) m |= 1;
          if ((m & 2) == 0 && In(s0 + ds * 0.1, t0 + dt * 0.1, ax, ay, bx, by, cx, cy)) m |= 2;
          if ((m & 4) == 0 && In(s0 + ds * 0.9, t0 + dt * 0.1, ax, ay, bx, by, cx, cy)) m |= 4;
          if ((m & 8) == 0 && In(s0 + ds * 0.1, t0 + dt * 0.9, ax, ay, bx, by, cx, cy)) m |= 8;
          if ((m & 16) == 0 && In(s0 + ds * 0.9, t0 + dt * 0.9, ax, ay, bx, by, cx, cy)) m |= 16;
        }
        mask[id] = m;
      }
    }
    var f = new bool[ns * nq]; area = 0; byte full = (byte)(centre ? 1 : 31);
    for (int j = 0; j < nq; j++) for (int i = 0; i < ns; i++) { int id = j * ns + i; if (mask[id] == full) { f[id] = true; area += (sl[i + 1] - sl[i]) * (tl[j + 1] - tl[j]); } }
    return f;
  }

  /// Rotation whose local x, y, z are u, v, n (orthonormal, right-handed as Unity's LookRotation(n, v)).
  public static Q Basis(V3 x, V3 y, V3 z) {
    double m00 = x.x, m01 = y.x, m02 = z.x, m10 = x.y, m11 = y.y, m12 = z.y, m20 = x.z, m21 = y.z, m22 = z.z;
    double tr = m00 + m11 + m22; double qw, qx, qy, qz;
    if (tr > 0) { double s = Math.Sqrt(tr + 1.0) * 2; qw = 0.25 * s; qx = (m21 - m12) / s; qy = (m02 - m20) / s; qz = (m10 - m01) / s; }
    else if (m00 > m11 && m00 > m22) { double s = Math.Sqrt(1.0 + m00 - m11 - m22) * 2; qw = (m21 - m12) / s; qx = 0.25 * s; qy = (m01 + m10) / s; qz = (m02 + m20) / s; }
    else if (m11 > m22) { double s = Math.Sqrt(1.0 + m11 - m00 - m22) * 2; qw = (m02 - m20) / s; qx = (m01 + m10) / s; qy = 0.25 * s; qz = (m12 + m21) / s; }
    else { double s = Math.Sqrt(1.0 + m22 - m00 - m11) * 2; qw = (m10 - m01) / s; qx = (m02 + m20) / s; qy = (m12 + m21) / s; qz = 0.25 * s; }
    var q = new Q(qx, qy, qz, qw).Norm(); if (q.w < 0) q = new Q(-q.x, -q.y, -q.z, -q.w); return q;
  }
}

static class GtPrep {
  static readonly Regex Organic = new(@"^(Cliff|Rock|Tree|Bush|Boulder|Stalag|Mushroom)", RegexOptions.IgnoreCase);
  static readonly Regex HullName = new(@"^Quest_\w+\.Quest_\w+_root\.|^CachedProBuilderMesh|StaticBatch", RegexOptions.IgnoreCase);
  static readonly Regex Extra = new(@"^\[DoorTrigger\]|^\[Quest_Door_Active\]|Boulder|^\[QuestLavaFloor\]|^\[?Quest_?Chest|TreasureChest|Throne|^Boss", RegexOptions.IgnoreCase);
  // hull materials: drawn in full (rectangles from 2 cm); the level's small detailed parts in other materials keep rectangles from 0.02 m2;
  // Rec Room's activity-door, gather-platform, light-pool, cobweb and rope meshes are left out (the quest draws its own).
  static readonly Regex SkipMat = new(@"^ActivityDoor|Spiderweb|^Rope|LockerFountainStageLight|LightShaft|^DoorTrigger|Glow|Invisible|_Nav_|^Goblin_|^VFX_", RegexOptions.IgnoreCase);
  static readonly Regex MainMat = new(@"Wood_Floor|Wallpaper|Plaster|Cinderblock|Brick|Stone|StageTop|Unlit_Black|Lava|Carpet|Tile|Concrete|Wall|Floor|Ground", RegexOptions.IgnoreCase);
  static readonly Regex RockMat = new(@"^Rocks_|^Rock_|Cliff", RegexOptions.IgnoreCase);
  // jt=1: the same, but the glow sticks (opaque AG/Standard-Opaque sticks in the level colour, not glow cards) and the
  // LockerFountainStageLight props (stage-light cans, fountains: the quest's light cones start at them) stay; the exit
  // pads' lock icon (Rec Room shows it only while the door is locked) goes
  static readonly Regex JtSkipMat = new(@"^ActivityDoor|Spiderweb|^Rope|LightShaft|^DoorTrigger|StageLight_Glow|DoorGlow|GlowBeam|LockIcon|Invisible|_Nav_|^Goblin_|^VFX_", RegexOptions.IgnoreCase);
  // drawn by the quest's haze (or not a surface): never doubled
  static readonly Regex HazeMat = new(@"AreaFog|Portal_|LightShaft|InvisibleWall|Nav_Mat|StageLight_Glow|LaserTrail|Beacon|Holographic|Lens", RegexOptions.IgnoreCase);
  // jt=1: stand-ins the quest rebuilds itself stay "G dress" boxes
  static readonly double MinSide = double.TryParse(Environment.GetEnvironmentVariable("GTJT_MINSIDE"), NumberStyles.Float, CultureInfo.InvariantCulture, out var mside) ? mside : 0.01;
  static readonly double Tail = double.TryParse(Environment.GetEnvironmentVariable("GTJT_TAIL"), NumberStyles.Float, CultureInfo.InvariantCulture, out var tl) ? tl : 0.97;
  static readonly Regex KeepDress = new(@"^Quest_Jumbotron_Marquee$", RegexOptions.IgnoreCase);
  static string F3(double v) { var r = Math.Round(v, 3); if (r == 0) r = 0; return r.ToString("0.###", CultureInfo.InvariantCulture); }
  static string P3(V3 v) => $"{F3(v.x)} {F3(v.y)} {F3(v.z)}";
  static double TriArea(V3[] w, int[] t) { double a = 0; for (int i = 0; i + 2 < t.Length; i += 3) { var e1 = w[t[i + 1]] - w[t[i]]; var e2 = w[t[i + 2]] - w[t[i]]; a += new V3(e1.y * e2.z - e1.z * e2.y, e1.z * e2.x - e1.x * e2.z, e1.x * e2.y - e1.y * e2.x).Len() * 0.5; } return a; }

  public static void Run(string work, string level, string name, string outPath, string[] opt) {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var L = new Ld(work); var m = Model.Load(L, level); var ix = InstIdx.Load(work);
    string refPath = opt.FirstOrDefault(o => o.StartsWith("ref="))?.Substring(4);
    var sl = Scan.Scenes(L); int li = int.TryParse(level.Replace("level", ""), out int q) ? q : -1; string scenePath = li >= 0 && li < sl.Count ? sl[li] : "?";
    bool InInstall(MeshInfo mi) => mi != null && !string.IsNullOrEmpty(mi.Name) && (ix.Mesh.ContainsKey(mi.Name) || ix.MeshCI.ContainsKey(mi.Name));
    var extra = new List<string>(); var notes = new List<string>();

    // 1. the hull: renderers and mesh colliders whose meshes the install lacks and that are editor-merged or big
    bool Hull(MeshInfo mi, TRS w) => mi != null && !string.IsNullOrEmpty(mi.Name) && !InInstall(mi) && !Organic.IsMatch(mi.Name) && (HullName.IsMatch(mi.Name) || mi.E.Mul(w.s.Abs()).Max() > 6);
    var hullR = m.Rend.Where(r => !r.Lod1 && r.En && m.Active(r.Go) && Hull(r.Mesh, m.GoW(r.Go))).ToList();
    var hullC = m.Col.Where(c => c.Cls == 64 && c.En && !c.Trig && m.Active(c.Go) && Hull(c.Mesh, m.GoW(c.Go))).ToList();
    m.Rend.RemoveAll(r => hullR.Contains(r)); m.Col.RemoveAll(c => hullC.Contains(c));
    var plates = new List<string>(); var mats = new Dictionary<string, string>(); var skipped = new Dictionary<string, int>(); int smallDropped = 0, rockN = 0, colored = 0; var rocks = new List<string>(); int plateN = 0, colN = 0; double plateArea = 0;
    // jt=1 (The Rise of Jumbotron): Plates.OfJt (own-axis rectangles from 1 cm, every material), mirrored transforms'
    // winding fixed, cull-off materials drawn both ways, the stand-ins re-measured (step 5). The default is unchanged.
    bool jt = opt.Contains("jt=1"); string statsPath = opt.FirstOrDefault(o => o.StartsWith("stats="))?.Substring(6);
    // poly=1 (with jt=1, GtPoly.cs): the hull pieces jt=1 draws as rectangles, and the planar non-rectangular ones (big coplanar
    // groups: fans, wedges, trapezoids), as clean planar polygons ("G poly"); curved pieces keep their fitted rectangles
    // poly=2: the curved pieces too, each facet a polygon (exact; about as many records as their fitted rectangles)
    bool poly = jt && (opt.Contains("poly=1") || opt.Contains("poly=2")); double polyShare = opt.Contains("poly=2") ? 0 : Polys.Share; var polySrc = new Dictionary<string, Polys.Src>(); int polyPieces = 0, polyCurved = 0; double polyPieceA = 0;
    var polyNot = new Regex(Environment.GetEnvironmentVariable("GTPOLY_SKIP") ?? "Tree|Bush|Leaf|Foliage|Palm|Mirror", RegexOptions.IgnoreCase);
    var stats = new List<string>(); var matTri = new Dictionary<string, double>(); var matRect = new Dictionary<string, double>(); var matRects = new Dictionary<string, int>();
    int mirrored = 0, twoSidedN = 0, curvedDropN = 0, approxN = 0, approxR = 0, smallBoxN = 0, smallBoxR = 0, tailN = 0; var pieceBoxes = new List<string>(); double curvedDropA = 0; var cullCache = new Dictionary<string, bool>();
    void AddMat(RendN r, List<object> mptr, int k, string mat) {
      if (mats.ContainsKey(mat)) return;
      var col = k < r.Mats.Count ? r.Mats[k].col : null; string tex = "-", ts = "1,1";
      if (k < mptr.Count) {
        var md = L.Read(m.F, DX.Ptr(mptr[k]));
        foreach (var x in md?.Dct("m_SavedProperties")?.Lst("m_TexEnvs") ?? new List<object>()) {
          var p = x as Dictionary<string, object>; var key = p?.G("first") as string;
          if (key != "_MainTex" && key != "_BaseMap" && key != "_Albedo") continue;
          var sd = p.Dct("second"); var (mf, mo) = L.Res(m.F, DX.Ptr(mptr[k])); var tn = mf != null ? L.NameOf(mf, sd.Ptr("m_Texture")) : null;
          if (tn != null) { tex = tn; var sc = sd.Vec("m_Scale"); ts = F3(sc.x) + "," + F3(sc.z == 0 ? sc.y : sc.y); break; }
        }
      }
      mats[mat] = $"G platemat {Y.Enc(mat)} 0 0 0 0 0 0 1 col={(col == null ? "1,1,1,1" : string.Join(",", col.Select(F3)))} tex={Y.Enc(tex)} scale={ts} inst={(ix.Mat.Contains(mat) ? 1 : 0)}";
    }
    // Rec Room draws a surface two-sided when its shader's first pass culls nothing (fixed Cull Off or its _Cull = 0).
    bool TwoSided(List<object> mptr, int k, string mat) {
      if (cullCache.TryGetValue(mat, out bool two)) return two;
      two = false;
      if (k < mptr.Count) {
        var (mf, mo) = L.Res(m.F, DX.Ptr(mptr[k])); var md = mo != null ? L.Read(mf, mo) : null;
        var shd = md != null ? L.Read(mf, md.Ptr("m_Shader")) : null;
        var ps = ((shd?.Dct("m_ParsedForm")?.Lst("m_SubShaders")?.FirstOrDefault() as Dictionary<string, object>)?.Lst("m_Passes")?.FirstOrDefault() as Dictionary<string, object>)?.Dct("m_State")?.Dct("culling");
        if (ps != null) {
          string pn = ps.Str("name"); double v = ps.Num("val");
          if (!string.IsNullOrEmpty(pn) && pn != "<noninit>") foreach (var x in md.Dct("m_SavedProperties")?.Lst("m_Floats") ?? new List<object>()) { var p = (Dictionary<string, object>)x; if (p.G("first") as string == pn) v = p.Num("second"); }
          two = (int)v == 0;
        }
      }
      return cullCache[mat] = two;
    }
    void JtPlates(string mat, List<Plates.Rect> rects, bool two, List<string> into) {
      foreach (var re in rects) {
        string vc = re.hasCol && (re.cr < 0.97 || re.cg < 0.97 || re.cb < 0.97) ? $" c={F3(re.cr)},{F3(re.cg)},{F3(re.cb)}" : "";
        if (vc != "") colored++;
        into.Add($"G plate {Y.Enc(mat)} {P3(re.c)} {Plates.Basis(re.u, re.v, re.n)} e={F3(re.hx)},{F3(re.hy)}{vc}"); plateN++; plateArea += re.hx * re.hy * 4;
        // the back face: the same rectangle turned about its v axis
        if (two) { into.Add($"G plate {Y.Enc(mat)} {P3(re.c)} {Plates.Basis(re.u * -1, re.v, re.n * -1)} e={F3(re.hx)},{F3(re.hy)}{vc}"); plateN++; twoSidedN++; }
      }
    }
    int[] Flip(int[] t) { var f = (int[])t.Clone(); for (int i = 0; i + 2 < f.Length; i += 3) (f[i + 1], f[i + 2]) = (f[i + 2], f[i + 1]); return f; }
    foreach (var r in hullR) {
      var w = m.GoW(r.Go); var (lv, subs) = SubGeo.Decode(L, r.Mesh); if (lv == null) { notes.Add("hull mesh unreadable: " + r.Mesh.Name); continue; }
      var vcols = SubGeo.Colors;
      var wv = lv.Select(p => w.p + w.r.Rot(w.s.Mul(p))).ToArray();
      // a mirrored transform (negative scale) turns the winding over: Unity draws the other side (jt only)
      bool flip = jt && w.s.x * w.s.y * w.s.z < 0; if (flip) mirrored++;
      // the renderer's materials again, for their main texture
      var rd = L.Read(m.F, m.F.ById[r.Id]); var mptr = rd?.Lst("m_Materials") ?? new List<object>();
      for (int k = 0; k < subs.Count; k++) {
        string mat = k < r.Mats.Count ? r.Mats[k].name ?? "?" : r.Mats.Count > 0 ? r.Mats[^1].name ?? "?" : "?";
        AddMat(r, mptr, k, mat);
        if ((jt ? JtSkipMat : SkipMat).IsMatch(mat)) { skipped[mat] = skipped.GetValueOrDefault(mat) + 1; if (jt) stats.Add($"hullskip\t{r.Mesh.Name}\t{mat}\t{TriArea(wv, subs[k]):F2}"); continue; }
        bool main = MainMat.IsMatch(mat);
        if (jt && !RockMat.IsMatch(mat) && !HazeMat.IsMatch(mat)) {
          // per 3D piece (one prop of the merged batch): a small (< 0.6 m) piece that is not rectangles (< 70 % exact:
          // a round grille, a knob, a cable) is left out rather than drawn as a 1 cm staircase
          var js = new Plates.JtStat(); var rects = new List<Plates.Rect>(); double curvedA = 0; int curvedN = 0;
          foreach (var pc in Plates.Pieces(wv, flip ? Flip(subs[k]) : subs[k])) {
            var pj = new Plates.JtStat(); if (Environment.GetEnvironmentVariable("GTJT_DEBUGMAT") == mat) pj.Dbg = new List<string>();
            var pr = Plates.OfJt(wv, pc, MinSide, vcols, pj, Environment.GetEnvironmentVariable("GTJT_NOREFINE") != "1");
            if (pj.Dbg != null && pj.Rects > 4) { Console.WriteLine($"piece tris={pc.Length / 3} ext={Plates.Extent(wv, pc):F2} area={pj.TriArea:F3} ex={pj.ExactArea:F3} rects={pj.Rects} fill={Plates.Obb(wv, pc).fill:F2}"); foreach (var x in pj.Dbg) Console.WriteLine("   " + x); }
            // a small (< 0.6 m) many-sided piece that fills its oriented box (a stage-light can, a glow stick, a knob block):
            // that box, one "G dress <batch>_box" in its material, instead of 8+ facets
            if (pr.Count >= 8 && pj.ExactArea >= 0.7 * pj.TriArea && Plates.Extent(wv, pc) < 0.6 && Environment.GetEnvironmentVariable("GTJT_NOBOX") != "1") {
              var ob = Plates.Obb(wv, pc);
              if (ob.fill >= 0.7 && ob.fill <= 1.15 && Math.Min(ob.e.x, Math.Min(ob.e.y, ob.e.z)) >= 0.004) {
                var mc = k < r.Mats.Count ? r.Mats[k].col : null;
                pieceBoxes.Add($"G dress {Y.Enc(r.Mesh.Name)}_box {P3(ob.c)} {Plates.Basis(ob.x, ob.y, ob.z)} e={F3(ob.e.x)},{F3(ob.e.y)},{F3(ob.e.z)} c={(mc == null ? "0.7,0.7,0.7,1" : string.Join(",", mc.Take(3).Select(F3)) + "," + F3(mc.Length > 3 ? Math.Clamp(mc[3], 0, 1) : 1))} m={Y.Enc(ix.Mat.Contains(mat) ? mat : "?" + mat)}");
                smallBoxN++; smallBoxR += pr.Count; continue;
              }
            }
            if (pj.ExactArea < 0.7 * pj.TriArea) {
              if (Plates.Extent(wv, pc) < 0.6) { curvedA += pj.TriArea; curvedN++; continue; }
              // poly=1: a planar piece that is not rectangles (half its area in coplanar groups of 3+ triangles) as polygons
              if (poly && !polyNot.IsMatch(mat) && Polys.PlanarShare(wv, pc) >= polyShare) { Polys.Add(polySrc, mat, TwoSided(mptr, k, mat) && !HazeMat.IsMatch(mat), wv, pc, vcols); polyPieces++; polyCurved++; polyPieceA += pj.TriArea; js.Add(pj); continue; }
              // a big curved piece (curtain folds, a crane arm, a tyre): fitted rectangles, one per triangle pair
              pr = Plates.Approx(wv, pc, 0.01, vcols); pj.Rects = pr.Count; pj.RectArea = pr.Sum(x => x.hx * x.hy * 4); approxN++; approxR += pr.Count;
            }
            else if (poly && !polyNot.IsMatch(mat)) { Polys.Add(polySrc, mat, TwoSided(mptr, k, mat) && !HazeMat.IsMatch(mat), wv, pc, vcols); polyPieces++; polyPieceA += pj.TriArea; js.Add(pj); continue; }
            // a busy prop of the batch (< 1.6 m, over 60 rectangles per m2: a ribbed barrel, a grille): its largest rectangles
            // that make 97 % of its area (GTJT_TAIL), like a stand-in; walls and big pieces keep every trim
            if (pr.Count > 12 && pj.TriArea > 0 && pr.Count > 60 * pj.TriArea && Plates.Extent(wv, pc) < 1.6) {
              var srt = pr.OrderByDescending(x => x.hx * x.hy).ToList(); double tot = srt.Sum(x => x.hx * x.hy), acc = 0; var kept = new List<Plates.Rect>();
              foreach (var x in srt) { if (acc >= Tail * tot && kept.Count >= 6) break; kept.Add(x); acc += x.hx * x.hy; }
              tailN += pr.Count - kept.Count; pr = kept; pj.Rects = pr.Count;
            }
            rects.AddRange(pr); js.Add(pj);
          }
          curvedDropN += curvedN; curvedDropA += curvedA;
          matTri[mat] = matTri.GetValueOrDefault(mat) + js.TriArea; matRect[mat] = matRect.GetValueOrDefault(mat) + js.RectArea; matRects[mat] = matRects.GetValueOrDefault(mat) + js.Rects;
          stats.Add($"hull\t{r.Mesh.Name}\t{mat}\t{js.TriArea:F2}\t{js.RectArea:F2}\t{js.ExactArea:F2}\t{js.Rects}\t{js.Groups}\t{js.Comps}\t{js.Subs}\t{js.Exact}\t{js.Refined}\t{(flip ? "mirrored" : "")}");
          JtPlates(mat, rects, TwoSided(mptr, k, mat) && !HazeMat.IsMatch(mat), plates);
          continue;
        }
        if (RockMat.IsMatch(mat)) {
          // rock sculpts are not planar: a coarse voxel shell (as apkscene does for them), drawn as rock by the quest
          var (bx, vox) = Shape.Boxes(wv, subs[k], 60, 12, 0.5, 3);
          if (bx != null) foreach (var b in bx) { rockN++; rocks.Add($"G dress rockvox {P3(b.c)} 0 0 0 1 e={F3(b.s.x * 0.5)},{F3(b.s.y * 0.5)},{F3(b.s.z * 0.5)} c=0.56,0.55,0.52,1 m={Y.Enc(mat)}"); }
          continue;
        }
        if (jt) {
          // the mist blobs (Scifi_AreaFog_*, low-poly ellipsoids of ~220 triangles): one fitted rectangle per triangle pair (~140)
          // instead of a 5 cm staircase (1.5-3 k blocky cells); the other haze plates (portal quads) as before
          var ap = Plates.Approx(wv, subs[k], 0.01, vcols); stats.Add($"haze	{r.Mesh.Name}	{mat}	{TriArea(wv, subs[k]):F2}	{subs[k].Length / 3}	approx {ap.Count}	pieces {Plates.Pieces(wv, subs[k]).Count}");
          if (Regex.IsMatch(mat, "AreaFog", RegexOptions.IgnoreCase)) { JtPlates(mat, ap, false, plates); continue; }
        }
        foreach (var re in Plates.Of(wv, subs[k], 0.02, vcols)) {
          if (!main && re.hx * re.hy * 4 < 0.05) { smallDropped++; continue; }
          var qr = Plates.Basis(re.u, re.v, re.n);
          // c= the face's vertex colour (VertColor materials are tinted per face by it); left out when white
          string vc = re.hasCol && (re.cr < 0.97 || re.cg < 0.97 || re.cb < 0.97) ? $" c={F3(re.cr)},{F3(re.cg)},{F3(re.cb)}" : "";
          if (vc != "") colored++;
          plates.Add($"G plate {Y.Enc(mat)} {P3(re.c)} {qr} e={F3(re.hx)},{F3(re.hy)}{vc}"); plateN++; plateArea += re.hx * re.hy * 4;
        }
      }
    }
    foreach (var c in hullC) {
      var w = m.GoW(c.Go); var (lv, subs) = SubGeo.Decode(L, c.Mesh); if (lv == null) { notes.Add("collider mesh unreadable: " + c.Mesh.Name); continue; }
      var wv = lv.Select(p => w.p + w.r.Rot(w.s.Mul(p))).ToArray();
      var all = subs.SelectMany(x => x).ToArray();
      const double depth = 0.2;
      foreach (var re in Plates.Of(wv, all, 0.03)) {
        var qr = Plates.Basis(re.u, re.v, re.n); var cc = re.c - re.n * (depth * 0.5);
        plates.Add($"C box {P3(cc)} {qr} {F3(re.hx * 2)} {F3(re.hy * 2)} {F3(depth)}"); colN++;
      }
    }

    // 2. apkscene's own emission for the rest (debug names on the stand-ins)
    Environment.SetEnvironmentVariable("APKSCENE_DEBUG", "1");
    var tmp = Path.GetTempFileName();
    var st = Emit.Write(m, ix, name, tmp, $"Android build 2026-03-23 (APKPure XAPK) {level} = {scenePath}");
    var src = File.ReadAllLines(tmp); File.Delete(tmp);

    // 3. markers apkscene does not know (poses from the level; shapes from their own box colliders)
    var colByGo = m.Col.GroupBy(c => c.Go).ToDictionary(g => g.Key, g => g.ToList());
    foreach (var g in m.Gos.Values) {
      if (!Extra.IsMatch(g.Name)) continue;
      var w = m.GoW(g.Id); var keys = new List<string>();
      var bc = colByGo.GetValueOrDefault(g.Id)?.FirstOrDefault(c => c.Cls == 65);
      if (bc != null) { var sz = bc.Size.Abs().Mul(w.s.Abs()); keys.Add($"shape=box size={F3(sz.x)},{F3(sz.y)},{F3(sz.z)} center={P3(w.p + w.r.Rot(w.s.Mul(bc.C)))}"); }
      if (!m.Active(g.Id)) keys.Add("active=0");
      var path = m.PathOf(g.Id); keys.Add("path=" + Y.Enc(path.Length > 100 ? path.Substring(path.Length - 100) : path));
      extra.Add($"G extra {Y.Enc(g.Name)} {P3(w.p)} {w.r} {string.Join(" ", keys)}");
    }

    // 4. REF2: enemy spawn points, the boss stage and level effects of the same level
    var refLines = new List<string>(); int refSp = 0, refF = 0;
    if (refPath != null && File.Exists(refPath)) {
      foreach (var l in File.ReadLines(refPath)) {
        if (l.StartsWith("F ")) { refLines.Add(l); refF++; }
        else if (l.StartsWith("G enemy_spawner ") || l.StartsWith("G boss ")) { refLines.Add(l); refSp++; }
        else if (l.StartsWith("G spawn Spawn%20Region")) { refLines.Add("G enemy_region" + l.Substring(7)); refSp++; }
      }
    }

    // 5. rewrite: stand-ins -> dress markers; door / lava triggers -> their kinds
    // jt=1: each stand-in's renderer is re-measured like the hull (its own materials): rectangles when >= 70 % of its
    // area is exact rectangles; a box-shaped mesh of one material stays the dress box (same shape); else a coarse
    // voxel shell when bigger than 0.6 m, else it is left out (no floating bounds box).
    Dictionary<string, List<(RendN r, V3 wc)>> standIn = null; var dressPlates = new List<string>(); var jtOut = new Dictionary<string, int>();
    if (jt) {
      standIn = new Dictionary<string, List<(RendN r, V3 wc)>>();
      foreach (var r in m.Rend) {
        if (r.Lod1 || !r.En || !m.Active(r.Go) || r.Mesh == null || string.IsNullOrEmpty(r.Mesh.Name) || InInstall(r.Mesh)) continue;
        var w = m.GoW(r.Go); var wc = w.p + w.r.Rot(w.s.Mul(r.Mesh.C)); var key = Y.Enc(r.Mesh.Name);
        if (!standIn.TryGetValue(key, out var sli)) standIn[key] = sli = new List<(RendN, V3)>(); sli.Add((r, wc));
      }
    }
    // the outcome for one stand-in: "plates" (lines into dressPlates), "box" (keep the dress line), "vox" (voxLines), "out"
    string Remeasure(RendN r, string meshEnc, string colKey, string matKey, List<string> voxLines) {
      var w = m.GoW(r.Go); var (lv, subs) = SubGeo.Decode(L, r.Mesh); if (lv == null) return "box";
      var vcols = SubGeo.Colors; var wv = lv.Select(p => w.p + w.r.Rot(w.s.Mul(p))).ToArray(); bool flip = w.s.x * w.s.y * w.s.z < 0;
      var rd = L.Read(m.F, m.F.ById[r.Id]); var mptr = rd?.Lst("m_Materials") ?? new List<object>();
      // per submesh (material) and 3D piece: its exact rectangles (no staircase), its oriented box and how well it fills it
      double triA = 0, rectA = 0, exA = 0, boxlikeA = 0; int nRects = 0, nBoxlike = 0, boxlikeRects = 0, tris = 0; var used = new HashSet<string>();
      var parts = new List<(int k, string mat, bool two, int[] pc, List<Plates.Rect> rects, double area, bool boxlike, (V3 c, V3 x, V3 y, V3 z, V3 e, double fill) obb, double ex)>();
      for (int k = 0; k < subs.Count; k++) {
        if (subs[k].Length < 3) continue;
        string mat = k < r.Mats.Count ? r.Mats[k].name ?? "?" : r.Mats.Count > 0 ? r.Mats[^1].name ?? "?" : "?";
        if (JtSkipMat.IsMatch(mat) || HazeMat.IsMatch(mat)) continue;
        bool two = TwoSided(mptr, k, mat); used.Add(mat); AddMat(r, mptr, k, mat);
        foreach (var pc in Plates.Pieces(wv, flip ? Flip(subs[k]) : subs[k])) {
          var js = new Plates.JtStat(); var rects = Plates.OfJt(wv, pc, 0.01, vcols, js, false); var ob = Plates.Obb(wv, pc);
          bool boxlike = ob.fill >= 0.75 && ob.fill <= 1.15 && Math.Min(ob.e.x, Math.Min(ob.e.y, ob.e.z)) >= 0.004;
          triA += js.TriArea; rectA += js.RectArea; exA += js.ExactArea; nRects += js.Rects; tris += pc.Length / 3;
          if (boxlike) { nBoxlike++; boxlikeA += js.TriArea; boxlikeRects += js.Rects; }
          parts.Add((k, mat, two, pc, rects, js.TriArea, boxlike, ob, js.ExactArea));
        }
      }
      var ext = r.Mesh.E.Mul(w.s.Abs()); double big = ext.Max() * 2, boxA = 8 * (ext.x * ext.y + ext.y * ext.z + ext.z * ext.x);
      // "boxes": the box-like pieces (tubes, planks, panels with thickness) as one oriented box each, when that is at
      // least half of the mesh and costs under half the rectangles; the rest of it as rectangles
      // (a structure of 2+ such pieces - trusses, cover walls, doors, containers - or a one-piece prop that really is a box,
      // fill >= 0.85; a one-piece prop less box-shaped keeps its real faces as rectangles, e.g. a terminal's slanted screen)
      bool boxes = nBoxlike > 0 && boxlikeA >= 0.5 * triA && nBoxlike * 1.3 < 0.5 * boxlikeRects && (nBoxlike >= 2 || (parts.Count == 1 && parts[0].obb.fill >= 0.85));
      string res;
      if (triA < 1e-6) res = "out";
      else if (used.Count == 1 && exA >= 0.95 * triA && Math.Abs(rectA - boxA) <= 0.05 * boxA) res = "box";
      else if (boxes) res = "boxes";
      else if (exA >= 0.7 * triA) res = "plates";
      else if (big > 0.6) res = tris <= 1200 || (big > 3 && tris <= 5000) ? "mixed" : "vox";
      else res = "out";
      string MatKey(string mat) => "m=" + Y.Enc(ix.Mat.Contains(mat) ? mat : "?" + mat);
      string ColKey(int k) { var c = k < r.Mats.Count ? r.Mats[k].col : null; return "c=" + (c == null ? "0.7,0.7,0.7,1" : string.Join(",", c.Take(3).Select(F3)) + "," + F3(c.Length > 3 ? Math.Clamp(c[3], 0, 1) : 1)); }
      int made = 0;
      // a prop's rectangles: the largest ones that make 97 % of its rectangle area (its greebles' 1-3 cm tail goes)
      var keep = new HashSet<Plates.Rect>(); {
        var allR = parts.Where(x => res == "plates" || (res == "boxes" && !x.boxlike)).SelectMany(x => x.rects).OrderByDescending(x => x.hx * x.hy).ToList();
        double tot = allR.Sum(x => x.hx * x.hy), acc = 0; foreach (var x in allR) { if (acc >= Tail * tot && keep.Count >= 6) break; keep.Add(x); acc += x.hx * x.hy; }
      }
      foreach (var x in parts) {
        if (res == "plates" || (res == "boxes" && !x.boxlike)) { var kr = x.rects.Where(keep.Contains).ToList(); JtPlates(x.mat, kr, x.two, dressPlates); made += kr.Count; }
        else if (res == "boxes") {
          var o = x.obb; var q = Plates.Basis(o.x, o.y, o.z);
          voxLines.Add($"G dress {meshEnc}_box {P3(o.c)} {q} e={F3(Math.Max(o.e.x, 0.005))},{F3(Math.Max(o.e.y, 0.005))},{F3(Math.Max(o.e.z, 0.005))} {ColKey(x.k)} {MatKey(x.mat)}"); made++;
        }
        else if (res == "mixed") {
          // a curved prop, piece by piece: a box-like piece (a rope, a rod) as its box, a rectangular one as its rectangles,
          // a curved one (a sandbag, a clock face) as fitted rectangles when over 15 cm, else left out
          if (x.boxlike && x.rects.Count > 6) { var o = x.obb; voxLines.Add($"G dress {meshEnc}_box {P3(o.c)} {Plates.Basis(o.x, o.y, o.z)} e={F3(Math.Max(o.e.x, 0.005))},{F3(Math.Max(o.e.y, 0.005))},{F3(Math.Max(o.e.z, 0.005))} {ColKey(x.k)} {MatKey(x.mat)}"); made++; }
          else if (x.ex >= 0.7 * x.area) { JtPlates(x.mat, x.rects, x.two, dressPlates); made += x.rects.Count; }
          else if (Plates.Extent(wv, x.pc) >= 0.15) { var ap = Plates.Approx(wv, x.pc, 0.01, vcols); JtPlates(x.mat, ap, x.two, dressPlates); made += ap.Count; }
        }
      }
      if (res == "vox") {
        var sv = lv.Select(p => p.Mul(w.s)).ToArray(); var all = subs.SelectMany(x => x).ToArray();
        var (bx, vox) = Shape.Boxes(sv, all, 40, 8, 0.1, 1.0);
        if (bx == null) res = "out";
        else foreach (var b in bx) { voxLines.Add($"G dress {meshEnc}_vox {P3(w.p + w.r.Rot(b.c))} {w.r} e={F3(b.s.x * 0.5)},{F3(b.s.y * 0.5)},{F3(b.s.z * 0.5)} {colKey} {matKey}"); made++; }
      }
      stats.Add($"dress\t{r.Mesh.Name}\t{string.Join(",", used)}\t{triA:F3}\t{rectA:F3}\t{exA:F3}\t{nRects}\t{big:F2}\t{boxA:F3}\t{res}\t{(flip ? "mirrored" : "")}\tpieces {parts.Count} boxlike {nBoxlike} made {made} fills {string.Join(",", parts.Take(4).Select(x => x.obb.fill.ToString("0.00", CultureInfo.InvariantCulture)))}");
      return res;
    }
    // jt=1 (iteration 13): an R record whose install mesh is not the level's. apkscene matches install meshes by name and
    // the runtime catalog loads the FIRST mesh of that name (inst_meshes.tsv order); on a name clash that is another shape
    // (the install's two "Cube" meshes: a 0.13 x 3.5 m pole first, the 1.14 x 0.78 m cardboard box second, so Cargo1's
    // cardboard boxes were poles lying in the air). Such a record is re-measured from the level's own mesh like a stand-in
    // (a box-shaped one-material mesh: its bounds box). The activity doors keep their records (the Foyer's door code).
    var meshNames = new Dictionary<int, string>(); var matNames = new Dictionary<int, string>(); int clashN = 0; var clashOut = new Dictionary<string, int>();
    bool Clash(string mn, MeshInfo lm) {
      if (lm == null || !ix.Mesh.TryGetValue(mn, out var cands) || cands.Count < 2 || mn.StartsWith("ActivityDoor", StringComparison.OrdinalIgnoreCase)) return false;
      var c0 = cands[0]; double tol = 0.02 + 0.05 * Math.Max(lm.E.x, Math.Max(lm.E.y, lm.E.z));
      return Math.Abs(c0.c.x - lm.C.x) > tol || Math.Abs(c0.c.y - lm.C.y) > tol || Math.Abs(c0.c.z - lm.C.z) > tol || Math.Abs(c0.e.x - lm.E.x) > tol || Math.Abs(c0.e.y - lm.E.y) > tol || Math.Abs(c0.e.z - lm.E.z) > tol;
    }
    var body = new List<string>(); var head = new List<string>(); int dress = 0; bool inHead = true;
    foreach (var l0 in src) {
      var l = l0;
      if (inHead && (l.StartsWith("#") || l.StartsWith("B ") || l.StartsWith("M ") || l.StartsWith("T ") || l.StartsWith("E "))) {
        head.Add(l);
        if (l.StartsWith("M ") || l.StartsWith("T ")) { var hp = l.Split(' ', 3); if (hp.Length == 3 && int.TryParse(hp[1], out int hi)) (l[0] == 'M' ? meshNames : matNames)[hi] = hp[2]; }
        continue;
      }
      inHead = false;
      if (jt && l.StartsWith("R ")) {
        var p = l.Split(' ');
        if (p.Length >= 13 && int.TryParse(p[1], out int rmi) && meshNames.TryGetValue(rmi, out var rmn)) {
          var pos = new V3(double.Parse(p[3], CultureInfo.InvariantCulture), double.Parse(p[4], CultureInfo.InvariantCulture), double.Parse(p[5], CultureInfo.InvariantCulture));
          var lr = m.Rend.FirstOrDefault(r => !r.Lod1 && !r.Skinned && r.En && r.Mesh != null && Y.Enc(r.Mesh.Name) == rmn && m.Active(r.Go) && (m.GoW(r.Go).p - pos).Len() < 0.01);
          if (lr != null && Clash(lr.Mesh.Name, lr.Mesh)) {
            var w = m.GoW(lr.Go); string lmat = lr.Mats.Count > 0 ? lr.Mats[0].name ?? "?" : "?"; var lc = lr.Mats.Count > 0 ? lr.Mats[0].col : null;
            string colKey = "c=" + (lc == null ? "0.7,0.7,0.7,1" : string.Join(",", lc.Take(3).Select(F3)) + "," + F3(lc.Length > 3 ? Math.Clamp(lc[3], 0, 1) : 1));
            string matKey = "m=" + Y.Enc(ix.Mat.Contains(lmat) ? lmat : "?" + lmat);
            var vox = new List<string>(); var outcome = Remeasure(lr, rmn, colKey, matKey, vox);
            clashN++; clashOut[outcome] = clashOut.GetValueOrDefault(outcome) + 1;
            if (outcome == "box") { var e = lr.Mesh.E.Mul(w.s.Abs()); body.Add($"G dress {rmn}_box {P3(w.p + w.r.Rot(w.s.Mul(lr.Mesh.C)))} {w.r} e={F3(Math.Max(e.x, 0.005))},{F3(Math.Max(e.y, 0.005))},{F3(Math.Max(e.z, 0.005))} {colKey} {matKey}"); dress++; }
            else { body.AddRange(vox); dress += vox.Count; }
            continue;
          }
        }
      }
      if (l.StartsWith("X ") && l.Contains(" #")) {
        int h = l.IndexOf(" #"); var ann = l.Substring(h + 2); var p = l.Substring(0, h).Split(' ');
        int at = ann.LastIndexOf('@'); string mesh = at >= 0 ? ann.Substring(0, at) : ann, mat = at >= 0 ? ann.Substring(at + 1) : "?";
        if (jt && !KeepDress.IsMatch(mesh)) {
          var cen = new V3(double.Parse(p[2], CultureInfo.InvariantCulture), double.Parse(p[3], CultureInfo.InvariantCulture), double.Parse(p[4], CultureInfo.InvariantCulture));
          var cand = standIn.GetValueOrDefault(mesh); var hit = cand?.OrderBy(x => (x.wc - cen).Len()).FirstOrDefault();
          string outcome = "box"; var vox = new List<string>();
          if (hit != null && hit.Value.r != null && (hit.Value.wc - cen).Len() < 0.01)
            outcome = Remeasure(hit.Value.r, mesh, $"c={p[12]},{p[13]},{p[14]},{(p.Length > 15 ? p[15] : "1")}", $"m={mat}", vox);
          else outcome = "nomatch";
          jtOut[outcome] = jtOut.GetValueOrDefault(outcome) + 1;
          if (outcome == "plates" || outcome == "out") continue;
          if (outcome == "vox" || outcome == "boxes" || outcome == "mixed") { body.AddRange(vox); dress += vox.Count; continue; }
        }
        // X <box|cyl> cx cy cz ex ey ez qx qy qz qw r g b a
        body.Add($"G dress {mesh} {p[2]} {p[3]} {p[4]} {p[8]} {p[9]} {p[10]} {p[11]} e={p[5]},{p[6]},{p[7]} c={p[12]},{p[13]},{p[14]},{(p.Length > 15 ? p[15] : "1")} m={mat}{(p[1] == "cyl" ? " cyl=1" : "")}");
        dress++; continue;
      }
      if (l.StartsWith("G trigger_zone [Quest_Door_Active]") || l.StartsWith("G trigger_zone [QuestLavaFloor]")) {
        var p = l.Split(' '); p[1] = l.Contains("[QuestLavaFloor]") ? "lava" : "door_zone"; l = string.Join(" ", p);
      }
      body.Add(l);
    }
    var pst = new Polys.Stat(); var polyLines = poly ? Polys.Build(polySrc, pst) : new List<string>();
    var sb = new StringBuilder();
    sb.Append(head[0]).Append('\n');
    foreach (var l in head.Skip(1)) sb.Append(l).Append('\n');
    sb.Append($"# gtscene: hull {hullR.Count} renderers -> {plateN} plates ({plateArea:F0} m2, {mats.Count} materials), {hullC.Count} mesh colliders -> {colN} C box plates; {dress} stand-ins -> G dress; {extra.Count} extra markers; REF2 {Path.GetFileName(refPath ?? "-")}: {refSp} spawner markers, {refF} effects\n");
    if (!jt) sb.Append($"# gtscene: left out: {string.Join(", ", skipped.Select(kv => kv.Key + " x" + kv.Value))}; {smallDropped} small detail rectangles under 0.05 m2; {colored} plates carry their vertex colour (c=)\n");
    else {
      double tA = matTri.Values.Sum(), rA = matRect.Values.Sum();
      sb.Append($"# gtscene jt=1: left out: {string.Join(", ", skipped.Select(kv => kv.Key + " x" + kv.Value))}; hull rectangles from 1 cm in every material on their own axes: {rA:F0} of {tA:F0} m2 of hull triangles ({(tA > 0 ? rA / tA * 100 : 0):F1} %); {curvedDropN} small curved pieces (< 0.6 m, < 70 % rectangles) left out ({curvedDropA:F1} m2); {approxN} big curved pieces as {approxR} fitted rectangles (one per triangle pair); {smallBoxN} small many-sided pieces as oriented boxes (instead of {smallBoxR} rectangles); {tailN} tail rectangles of busy props left out; {mirrored} mirrored hull renderers turned; {twoSidedN} back faces of cull-off materials; {colored} plates carry their vertex colour (c=)\n");
      sb.Append($"# gtscene jt=1: stand-ins re-measured: {string.Join(", ", jtOut.OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value))} ({dressPlates.Count} plates)\n");
      if (clashN > 0) sb.Append($"# gtscene jt=1: {clashN} install-mesh records whose install mesh (the first of its name) is not the level's, re-measured: {string.Join(", ", clashOut.OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value))}\n");
      if (poly) {
        sb.Append($"# gtscene poly={(polyShare == 0 ? 2 : 1)}: {polyPieces} hull pieces ({polyPieceA:F0} m2; {polyCurved} of them {(polyShare == 0 ? "curved or planar but not rectangles: every facet" : "planar but not rectangles")}) as {pst.Records} planar polygons (\"G poly\": {pst.Rings} rings, {pst.Holes} holes, {pst.PtsOut} points from {pst.PtsIn} outline points, at most {pst.MaxPts} in one; {pst.Groups} plane+colour groups of {pst.Srcs} materials from {pst.Tris} triangles, {pst.TriArea:F0} m2 -> {pst.PolyArea:F0} m2); {pst.Split} overlapping groups split, {pst.TriFallback} parts by triangle ({pst.TriFallbackTris}), {pst.Reverted} rings kept unsimplified, {pst.TjSplits} T-junction splits; tolerances angle {Polys.AngTol} deg, offset {Polys.OffTol} m, colour {Polys.ColTol}, outline {Polys.Tol} m\n");
        foreach (var x in pst.Log.Take(20)) sb.Append("# gtscene poly=1: ").Append(x).Append('\n');
      }
    }
    foreach (var n in notes) sb.Append("# ").Append(n).Append('\n');
    foreach (var l in body) sb.Append(l).Append('\n');
    foreach (var l in mats.Values) sb.Append(l).Append('\n');
    foreach (var l in plates) sb.Append(l).Append('\n');
    foreach (var l in polyLines) sb.Append(l).Append('\n');
    foreach (var l in dressPlates) sb.Append(l).Append('\n');
    if (statsPath != null) File.WriteAllLines(statsPath, stats);
    foreach (var l in rocks) sb.Append(l).Append('\n');
    foreach (var l in pieceBoxes) sb.Append(l).Append('\n');
    foreach (var l in extra) sb.Append(l).Append('\n');
    foreach (var l in refLines) sb.Append(l).Append('\n');
    File.WriteAllText(outPath, sb.ToString());
    Console.WriteLine($"{name}: {level} {scenePath}: plates {plateN} ({plateArea:F0} m2, {mats.Count} mats: {string.Join(",", mats.Keys)}), collider plates {colN} from {hullC.Count}, dress {dress}, extra {extra.Count}, ref {refSp}+{refF}F, refined groups {Plates.Refined}; {new FileInfo(outPath).Length / 1024} KB [{sw.Elapsed.TotalSeconds:F1} s]");
  }
}

/// A quick software render of a prepared scene (plates, dress and stand-in boxes, optionally colliders) to a PNG, to
/// check a hull offline without Unity. Flat shading, z-buffer, one-sided plates like the level's meshes.
static class GtRender {
  sealed class Tri { public V3 a, b, c; public double r, g, bl; public bool two; }
  static double[] Vec(string s) => s.Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
  public static void Run(string inPath, string outPath, string[] opt) {
    string Opt(string k, string d) => opt.FirstOrDefault(o => o.StartsWith(k + "="))?.Substring(k.Length + 1) ?? d;
    int W = int.Parse(Opt("w", "960")), H = int.Parse(Opt("h", "540")); double fov = double.Parse(Opt("fov", "70"));
    var skipRe = Opt("skip", null) is string sk ? new Regex(sk, RegexOptions.IgnoreCase) : null;
    // vc=1: plates and polygons tinted by their c= vertex colour (Rec Room's AG shaders multiply it in)
    bool vcol = Opt("vc", "0") == "1"; int polyN = 0, polyBad = 0, polyTris = 0;
    bool cols = Opt("col", "0") == "1"; string top = Opt("top", null); double ymax = double.Parse(Opt("ymax", "1e9")), ymin = double.Parse(Opt("ymin", "-1e9")); bool nodress = Opt("nodress", "0") == "1";
    var mcol = new Dictionary<string, double[]>(); var tris = new List<Tri>();
    void Quad(V3 c, Q q, double hx, double hy, double[] rgb, bool two) {
      var u = q.Rot(new V3(hx, 0, 0)); var v = q.Rot(new V3(0, hy, 0));
      var p0 = c - u - v; var p1 = c + u - v; var p2 = c + u + v; var p3 = c - u + v;
      tris.Add(new Tri { a = p0, b = p1, c = p2, r = rgb[0], g = rgb[1], bl = rgb[2], two = two }); tris.Add(new Tri { a = p0, b = p2, c = p3, r = rgb[0], g = rgb[1], bl = rgb[2], two = two });
    }
    void Box(V3 c, Q q, V3 e, double[] rgb) {
      for (int ax = 0; ax < 3; ax++) for (int sgn = -1; sgn <= 1; sgn += 2) {
        V3 n = ax == 0 ? new V3(sgn, 0, 0) : ax == 1 ? new V3(0, sgn, 0) : new V3(0, 0, sgn);
        V3 a1 = ax == 0 ? new V3(0, e.y, 0) : new V3(e.x, 0, 0), a2 = ax == 2 ? new V3(0, e.y, 0) : new V3(0, 0, e.z);
        var fc = c + q.Rot(new V3(n.x * e.x, n.y * e.y, n.z * e.z)); var u = q.Rot(a1); var v = q.Rot(a2);
        tris.Add(new Tri { a = fc - u - v, b = fc + u - v, c = fc + u + v, r = rgb[0], g = rgb[1], bl = rgb[2], two = true }); tris.Add(new Tri { a = fc - u - v, b = fc + u + v, c = fc - u + v, r = rgb[0], g = rgb[1], bl = rgb[2], two = true });
      }
    }
    double[] Hash(string s) { var pal = new[] { new[] {0.9,0.3,0.3}, new[] {0.3,0.8,0.3}, new[] {0.3,0.4,0.95}, new[] {0.95,0.85,0.2}, new[] {0.8,0.3,0.9}, new[] {0.2,0.85,0.85}, new[] {0.95,0.55,0.15}, new[] {0.6,0.6,0.6}, new[] {0.55,0.35,0.2}, new[] {1.0,1.0,1.0} }; int h = 0; foreach (var ch in s) h = (h * 31 + ch) & 0x7fffffff; return pal[h % pal.Length]; }
    var lines = File.ReadAllLines(inPath);
    // avg=1 (2026-11-23): materials in their measured mean colour (the platemat's avg=, texture mean x colour) where it has one
    bool useAvg = Opt("avg", "0") == "1";
    foreach (var l in lines) if (l.StartsWith("G platemat ")) { var p = l.Split(' '); var c = Vec(p.First(x => x.StartsWith("col=")).Substring(4)); var av = useAvg ? p.FirstOrDefault(x => x.StartsWith("avg=")) : null; if (av != null) { mcol[p[2]] = Vec(av.Substring(4)); continue; } mcol[p[2]] = c[0] > 0.97 && c[1] > 0.97 && c[2] > 0.97 ? Hash(p[2]) : c; }
    foreach (var kv in mcol) Console.WriteLine("  " + kv.Key + " = " + string.Join(",", kv.Value.Select(x => x.ToString("0.00"))));
    double[] Tint(double[] rgb, string c) { if (c == null) return rgb; var v = Vec(c.Substring(2)); return new[] { rgb[0] * v[0], rgb[1] * v[1], rgb[2] * v[2] }; }
    // avg=1 (rcscene round 2): a record's own measured colour sa= (+ half its emission sg=) wins over the platemat's
    double[] Sa(string[] p, double[] rgb) { if (!useAvg) return rgb; var s = p.FirstOrDefault(x => x.StartsWith("sa=")); if (s == null) return rgb; var v = Vec(s.Substring(3)); var g = p.FirstOrDefault(x => x.StartsWith("sg=")); if (g != null) { var e = Vec(g.Substring(3)); for (int k = 0; k < 3; k++) v[k] = Math.Min(1, v[k] + 0.5 * e[k]); } return v; }
    double[] N(string[] p, int i) => new[] { double.Parse(p[i]), double.Parse(p[i + 1]), double.Parse(p[i + 2]) };
    foreach (var l in lines) {
      var p = l.Split(' ');
      if (l.StartsWith("G plate ") && skipRe != null && skipRe.IsMatch(p[2])) continue;
      if (l.StartsWith("G plate ")) { var c = N(p, 3); var e = Vec(p[10].Substring(2)); Quad(new V3(c[0], c[1], c[2]), new Q(double.Parse(p[6]), double.Parse(p[7]), double.Parse(p[8]), double.Parse(p[9])), e[0], e[1], Tint(Sa(p, mcol.GetValueOrDefault(p[2]) ?? Hash(p[2])), vcol ? p.FirstOrDefault(x => x.StartsWith("c=")) : null), false); }
      else if (l.StartsWith("G poly ")) {
        if (skipRe != null && skipRe.IsMatch(p[2])) continue;
        var tl = Polys.Triangles(l, out var pn, out double ratio, out var pc, out bool two, out string pm); polyN++;
        if (tl == null || ratio < 0.99 || ratio > 1.01) { polyBad++; if (polyBad <= 5) Console.WriteLine("  poly not triangulated (" + ratio.ToString("0.000") + "): " + l.Substring(0, Math.Min(160, l.Length))); if (tl == null) continue; }
        var rgb = Tint(Sa(p, mcol.GetValueOrDefault(pm) ?? Hash(pm)), vcol ? p.FirstOrDefault(x => x.StartsWith("c=")) : null);
        foreach (var t in tl) { tris.Add(new Tri { a = t.a, b = t.b, c = t.c, r = rgb[0], g = rgb[1], bl = rgb[2], two = two }); polyTris++; }
      }
      else if (!nodress && l.StartsWith("G dress ")) { var c = N(p, 3); var e = Vec(p[10].Substring(2)); var cc = Vec(p[11].Substring(2)); Box(new V3(c[0], c[1], c[2]), new Q(double.Parse(p[6]), double.Parse(p[7]), double.Parse(p[8]), double.Parse(p[9])), new V3(e[0], e[1], e[2]), Sa(p, new[] { cc[0], cc[1], cc[2] })); }
      else if (l.StartsWith("X box ")) { var c = N(p, 2); var e = N(p, 5); Box(new V3(c[0], c[1], c[2]), new Q(double.Parse(p[8]), double.Parse(p[9]), double.Parse(p[10]), double.Parse(p[11])), new V3(e[0], e[1], e[2]), new[] { double.Parse(p[12]), double.Parse(p[13]), double.Parse(p[14]) }); }
      else if (cols && l.StartsWith("C box ")) { var c = N(p, 2); var s = N(p, 9); Box(new V3(c[0], c[1], c[2]), new Q(double.Parse(p[5]), double.Parse(p[6]), double.Parse(p[7]), double.Parse(p[8])), new V3(s[0] / 2, s[1] / 2, s[2] / 2), new[] { 0.9, 0.2, 0.9 }); }
      else if (l.StartsWith("G ") && !l.StartsWith("G plate") && p.Length > 5 && (p[1] == "spawn" || p[1] == "enemy_spawner" || p[1] == "extra" || p[1] == "door_zone" || p[1] == "weapon_pickup" || p[1] == "scoreboard")) {
        var c = N(p, 3); var rgb = p[1] == "spawn" ? new[] { 0.1, 0.9, 0.1 } : p[1] == "enemy_spawner" ? new[] { 1.0, 0.1, 0.1 } : p[1] == "weapon_pickup" ? new[] { 1.0, 0.9, 0.1 } : new[] { 0.1, 0.6, 1.0 };
        Box(new V3(c[0], c[1] + 0.5, c[2]), Q.I, new V3(0.15, 0.5, 0.15), rgb);
      }
    }
    // camera
    V3 cam, fwd, right, up; double f; bool ortho = top != null; double[] tw = null;
    if (ortho) { tw = Vec(top); W = (int)((tw[2] - tw[0]) * tw[4]); H = (int)((tw[3] - tw[1]) * tw[4]); cam = default; fwd = new V3(0, -1, 0); right = new V3(1, 0, 0); up = new V3(0, 0, 1); f = 0; }
    else {
      var cp = Vec(Opt("cam", "0,2,0")); var lk = Vec(Opt("look", "0,2,10")); cam = new V3(cp[0], cp[1], cp[2]);
      fwd = new V3(lk[0] - cp[0], lk[1] - cp[1], lk[2] - cp[2]); fwd = fwd * (1 / fwd.Len());
      right = new V3(fwd.z, 0, -fwd.x); right = right * (1 / right.Len()); up = new V3(fwd.y * right.z - fwd.z * right.y, fwd.z * right.x - fwd.x * right.z, fwd.x * right.y - fwd.y * right.x);
      f = H * 0.5 / Math.Tan(fov * Math.PI / 360);
    }
    var zb = new double[W * H]; Array.Fill(zb, double.MaxValue); var img = new byte[W * H * 3];
    for (int i = 0; i < img.Length; i += 3) { img[i] = 40; img[i + 1] = 44; img[i + 2] = 52; }
    var light = new V3(0.35, 0.8, 0.45); light = light * (1 / light.Len());
    double Dt(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
    foreach (var tr in tris) {
      if (Math.Max(tr.a.y, Math.Max(tr.b.y, tr.c.y)) > ymax || Math.Min(tr.a.y, Math.Min(tr.b.y, tr.c.y)) < ymin) continue;
      var e1 = tr.b - tr.a; var e2 = tr.c - tr.a; var n = new V3(e1.y * e2.z - e1.z * e2.y, e1.z * e2.x - e1.x * e2.z, e1.x * e2.y - e1.y * e2.x); double nl = n.Len(); if (nl < 1e-12) continue; n = n * (1 / nl);
      // plates are one-sided: seen from behind they are culled (Unity culls back faces)
      V3 toCam = ortho ? new V3(0, 1, 0) : cam - tr.a;
      if (!tr.two && Dt(n, toCam) < 0) continue;
      double shade = 0.35 + 0.65 * Math.Abs(Dt(n, light));
      (double x, double y, double z) Pr(V3 p) {
        if (ortho) return ((p.x - tw[0]) * tw[4], (tw[3] - p.z) * tw[4], 1000 - p.y);
        var d = p - cam; double z = Dt(d, fwd); if (z < 0.049) return (0, 0, -1); return (W * 0.5 + Dt(d, right) * f / z, H * 0.5 - Dt(d, up) * f / z, z);
      }
      // near-plane clipping (camera space z >= 0.05), then a fan of screen triangles
      var poly = new List<V3> { tr.a, tr.b, tr.c };
      if (!ortho) {
        const double near = 0.05; var outp = new List<V3>();
        for (int k = 0; k < poly.Count; k++) {
          var p0 = poly[k]; var p1 = poly[(k + 1) % poly.Count]; double z0 = Dt(p0 - cam, fwd), z1 = Dt(p1 - cam, fwd);
          if (z0 >= near) outp.Add(p0);
          if ((z0 >= near) != (z1 >= near)) { double tt = (near - z0) / (z1 - z0); outp.Add(p0 + (p1 - p0) * tt); }
        }
        poly = outp; if (poly.Count < 3) continue;
      }
      byte R = (byte)Math.Clamp(tr.r * shade * 255, 0, 255), G = (byte)Math.Clamp(tr.g * shade * 255, 0, 255), Bb = (byte)Math.Clamp(tr.bl * shade * 255, 0, 255);
      for (int fi = 1; fi + 1 < poly.Count; fi++) {
      var A = Pr(poly[0]); var B = Pr(poly[fi]); var C = Pr(poly[fi + 1]); if (A.z < 0 || B.z < 0 || C.z < 0) continue;
      int x0 = Math.Max(0, (int)Math.Floor(Math.Min(A.x, Math.Min(B.x, C.x)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(A.x, Math.Max(B.x, C.x))));
      int y0 = Math.Max(0, (int)Math.Floor(Math.Min(A.y, Math.Min(B.y, C.y)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(A.y, Math.Max(B.y, C.y))));
      double den = (B.y - C.y) * (A.x - C.x) + (C.x - B.x) * (A.y - C.y); if (Math.Abs(den) < 1e-9) continue;
      for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) {
        double px = x + 0.5, py = y + 0.5;
        double l1 = ((B.y - C.y) * (px - C.x) + (C.x - B.x) * (py - C.y)) / den, l2 = ((C.y - A.y) * (px - C.x) + (A.x - C.x) * (py - C.y)) / den, l3 = 1 - l1 - l2;
        if (l1 < 0 || l2 < 0 || l3 < 0) continue;
        double z = ortho ? l1 * A.z + l2 * B.z + l3 * C.z : 1 / (l1 / A.z + l2 / B.z + l3 / C.z);
        int id = y * W + x; if (z >= zb[id]) continue; zb[id] = z; img[id * 3] = R; img[id * 3 + 1] = G; img[id * 3 + 2] = Bb;
      }
      }
    }
    Png.Write(outPath, W, H, img);
    int holes = 0; foreach (var z in zb) if (z == double.MaxValue) holes++;
    Console.WriteLine($"rendered {tris.Count} triangles to {outPath} ({W}x{H}), {holes} background pixels; polygons {polyN} ({polyTris} triangles, {polyBad} not triangulated)");
  }
}

static class Png {
  public static void Write(string path, int w, int h, byte[] rgb) {
    using var ms = new MemoryStream();
    using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Fastest, true)) {
      for (int y = 0; y < h; y++) { z.WriteByte(0); z.Write(rgb, y * w * 3, w * 3); }
    }
    using var fs = File.Create(path);
    fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    void Chunk(string type, byte[] data) {
      var t = Encoding.ASCII.GetBytes(type); var len = BitConverter.GetBytes(data.Length); Array.Reverse(len); fs.Write(len); fs.Write(t); fs.Write(data);
      uint crc = Crc(t, 0xFFFFFFFF); crc = Crc(data, crc) ^ 0xFFFFFFFF; var cb = BitConverter.GetBytes(crc); Array.Reverse(cb); fs.Write(cb);
    }
    var ih = new byte[13]; var wb = BitConverter.GetBytes(w); Array.Reverse(wb); var hb = BitConverter.GetBytes(h); Array.Reverse(hb);
    Buffer.BlockCopy(wb, 0, ih, 0, 4); Buffer.BlockCopy(hb, 0, ih, 4, 4); ih[8] = 8; ih[9] = 2; ih[10] = 0; ih[11] = 0; ih[12] = 0;
    Chunk("IHDR", ih); Chunk("IDAT", ms.ToArray()); Chunk("IEND", new byte[0]);
  }
  static uint[] table;
  static uint Crc(byte[] d, uint c) {
    if (table == null) { table = new uint[256]; for (uint n = 0; n < 256; n++) { uint x = n; for (int k = 0; k < 8; k++) x = (x & 1) != 0 ? 0xEDB88320 ^ (x >> 1) : x >> 1; table[n] = x; } }
    foreach (var b in d) c = table[(c ^ b) & 0xff] ^ (c >> 8); return c;
  }
}

/// cull <work> <levelN> [levelN ...]: every material on the levels' renderers with its shader's face culling (per pass:
/// a fixed Off/Front/Back or the property it reads, and that property's value on the material) - which level surfaces
/// Rec Room draws two-sided. One line per material: name | shader | cull (0 off, 1 front, 2 back) | renderers.
static class GtCull {
  public static void Run(string work, string[] levels) {
    var L = new Ld(work); var seen = new Dictionary<string, (string sh, string cull, int n)>();
    foreach (var level in levels) {
      var m = Model.Load(L, level);
      foreach (var r in m.Rend) {
        if (r.Lod1) continue;
        var rd = L.Read(m.F, m.F.ById[r.Id]); var mptr = rd?.Lst("m_Materials") ?? new List<object>();
        foreach (var mp in mptr) {
          var (mf, mo) = L.Res(m.F, DX.Ptr(mp)); if (mo == null) continue;
          var md = L.Read(mf, mo); var name = md?.Str("m_Name"); if (name == null) continue;
          if (seen.TryGetValue(name, out var had)) { seen[name] = (had.sh, had.cull, had.n + 1); continue; }
          var shd = L.Read(mf, md.Ptr("m_Shader")); var pf = shd?.Dct("m_ParsedForm"); string sname = pf?.Str("m_Name") ?? shd?.Str("m_Name") ?? "?";
          var floats = new Dictionary<string, double>();
          foreach (var x in md.Dct("m_SavedProperties")?.Lst("m_Floats") ?? new List<object>()) { var p = (Dictionary<string, object>)x; if (p.G("first") is string k) floats[k] = p.Num("second"); }
          var states = new List<string>();
          foreach (var ss in pf?.Lst("m_SubShaders") ?? new List<object>())
            foreach (var ps in (ss as Dictionary<string, object>)?.Lst("m_Passes") ?? new List<object>()) {
              var cu = (ps as Dictionary<string, object>)?.Dct("m_State")?.Dct("culling"); if (cu == null) continue;
              string pn = cu.Str("name"); double v = cu.Num("val");
              string s = string.IsNullOrEmpty(pn) || pn == "<noninit>" ? ((int)v).ToString() : pn + "=" + (floats.TryGetValue(pn, out var fv) ? ((int)fv).ToString() : "?(" + (int)v + ")");
              if (!states.Contains(s)) states.Add(s);
            }
          seen[name] = (sname, states.Count == 0 ? (pf == null ? "noparsed" : "-") : string.Join(",", states), 1);
        }
      }
    }
    foreach (var kv in seen.OrderBy(k => k.Key, StringComparer.Ordinal)) Console.WriteLine($"{kv.Key} | {kv.Value.sh} | {kv.Value.cull} | {kv.Value.n}");
  }
}

/// Offline floor probe over a prepared scene's box colliders (C box records): a ray straight down from each point,
/// the first box it enters. For placing spawn groups and paths without Unity.
///   probe <in.rrsk> x,y,z [x,y,z ...]            each: first hit y below the point (or "none")
///   profile <in.rrsk> x0,z0 x1,z1 n ystart        n samples along the line from ystart down
static class GtProbe {
  sealed class Bx { public V3 c; public Q q, qi; public V3 h; }
  static List<Bx> Load(string path) {
    var list = new List<Bx>();
    foreach (var l in File.ReadLines(path)) {
      if (!l.StartsWith("C box ")) continue; var p = l.Split(' ');
      double D(int i) => double.Parse(p[i], CultureInfo.InvariantCulture);
      var q = new Q(D(5), D(6), D(7), D(8)).Norm();
      list.Add(new Bx { c = new V3(D(2), D(3), D(4)), q = q, qi = new Q(-q.x, -q.y, -q.z, q.w), h = new V3(D(9) / 2, D(10) / 2, D(11) / 2) });
    }
    return list;
  }
  static double Down(List<Bx> boxes, V3 o, double maxD) {
    double best = double.PositiveInfinity; var dir = new V3(0, -1, 0);
    foreach (var b in boxes) {
      var lo = b.qi.Rot(o - b.c); var ld = b.qi.Rot(dir); double t0 = 0, t1 = maxD; bool ok = true;
      double[] O = { lo.x, lo.y, lo.z }, Dd = { ld.x, ld.y, ld.z }, Hh = { b.h.x, b.h.y, b.h.z };
      for (int k = 0; k < 3 && ok; k++) {
        if (Math.Abs(Dd[k]) < 1e-9) { if (O[k] < -Hh[k] || O[k] > Hh[k]) ok = false; continue; }
        double a = (-Hh[k] - O[k]) / Dd[k], c = (Hh[k] - O[k]) / Dd[k]; if (a > c) (a, c) = (c, a);
        t0 = Math.Max(t0, a); t1 = Math.Min(t1, c); if (t0 > t1) ok = false;
      }
      if (ok && t0 < best) best = t0;
    }
    return best;
  }
  public static void Run(string[] a) {
    var boxes = Load(a[1]);
    if (a[0] == "probe") {
      foreach (var s in a.Skip(2)) { var v = s.Split(',').Select(double.Parse).ToArray(); var d = Down(boxes, new V3(v[0], v[1], v[2]), 30); Console.WriteLine($"{s}: {(double.IsInfinity(d) ? "none" : (v[1] - d).ToString("0.00"))}"); }
    } else {
      var p0 = a[2].Split(',').Select(double.Parse).ToArray(); var p1 = a[3].Split(',').Select(double.Parse).ToArray(); int n = int.Parse(a[4]); double ys = double.Parse(a[5]);
      var sb = new StringBuilder();
      for (int i = 0; i <= n; i++) { double f = (double)i / n, x = p0[0] + (p1[0] - p0[0]) * f, z = p0[1] + (p1[1] - p0[1]) * f; var d = Down(boxes, new V3(x, ys, z), 40); sb.Append($"{x:0.0},{z:0.0}:{(double.IsInfinity(d) ? "-" : (ys - d).ToString("0.0"))}  "); }
      Console.WriteLine(sb.ToString());
    }
  }
}

/// float <in.rrsk> <out.rrsk> <inst_meshes.tsv> [tol=0.04] [snap=0.6] [hang=1.5] [skip=<mesh>,..] [keep=<regex>] [drop=<material regex>]
///       [support=cx,cy,cz,sx,sy,sz[;..]] [report=<file.tsv>]
/// (The Rise of Jumbotron, iteration 13: "still some random things floating".) The support test of a prepared room and
/// its fix. Every piece the room draws is an oriented box - the hull plates and stand-in boxes the runtime draws (the
/// RrJumbotronHull / Look rules: no fog, portal, shaft or nav plates, no skipped decals, no dress over 30 m, no glass
/// frames), the install props (R: the runtime's mesh, the first of its name in inst_meshes.tsv; not the skip= meshes, the
/// light shafts or hidden materials) and support= boxes the runtime builds (the Jumbotron's block); P props are physics.
/// Two pieces touch within tol (SAT); the connected pieces with the most area are the room. Every other group of
/// pieces is a floater and gets one fix, biggest first (a fixed group supports the ones after it):
///   keep  - a fifth of its area in keep= pieces (material or mesh regex) and not 0-25 cm over a support: a body that
///           floats in Rec Room too (the Departure's stage platforms over the portal pit and its hanging screen curtains,
///           Space's boulder). Having its own colliders is not enough (Cargo's barrels and cover wall 3 m over the floor
///           stood on shelves the level switched off); the report shows their footprint and the group's top area;
///   drop  - its material matches drop= (set pieces the runtime rebuilds);
///   down  - something is under it within snap and not much farther than its nearest neighbour: lowered onto it, 1 cm
///           left (couches, lockers, chairs, a ladder 5-18 cm up; a tripod's lamp head over its short stand-in pole);
///   hang  - a glow stick with a surface above within hang: an 8 mm dark string up to it (the Foyer's hanging sticks);
///   snap  - (not a glow stick) its nearest piece is within snap: moved along the separating axis onto it, 1 cm left
///           (decor panels and terminal screens off their walls, lamp cans off the ceiling, the marquee off its pillars);
///   out   - else left out (glow sticks in mid-air, pieces of hidden / rebuilt objects).
/// Only G plate / G dress / R lines change; the report lists every group with its fix.
static class GtFloat {
  sealed class Pc { public int Line; public char Kind; public string Name, Mat; public V3 C; public V3[] A = new V3[3]; public double[] E = new double[3]; public V3 Mn, Mx; public double Area; public int Id; }
  static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
  static string Key(string[] t, string k) { foreach (var x in t) if (x.StartsWith(k + "=")) return x.Substring(k.Length + 1); return null; }
  static bool NotASurface(string m) => Regex.IsMatch(m, "AreaFog|Portal_|LightShaft|InvisibleWall|Nav_Mat", RegexOptions.IgnoreCase);
  static readonly Regex LookSkip = new(@"^(EventRoom_Moon_Mat|GarageMarkings_Mat|LockIcon_Mat|Wires_Mat|PingPongTable_Net_Mat)$|^StageLight_Glow", RegexOptions.IgnoreCase);
  static readonly Regex Glow = new("Glowstick", RegexOptions.IgnoreCase);

  static Pc Box(char kind, string name, string mat, V3 c, Q q, V3 e, int line) {
    var p = new Pc { Kind = kind, Name = name, Mat = mat, C = c, Line = line };
    p.A[0] = q.Rot(new V3(1, 0, 0)); p.A[1] = q.Rot(new V3(0, 1, 0)); p.A[2] = q.Rot(new V3(0, 0, 1));
    p.E[0] = Math.Abs(e.x); p.E[1] = Math.Abs(e.y); p.E[2] = Math.Abs(e.z);
    double rx = 0, ry = 0, rz = 0;
    for (int i = 0; i < 3; i++) { rx += Math.Abs(p.A[i].x) * p.E[i]; ry += Math.Abs(p.A[i].y) * p.E[i]; rz += Math.Abs(p.A[i].z) * p.E[i]; }
    p.Mn = new V3(c.x - rx, c.y - ry, c.z - rz); p.Mx = new V3(c.x + rx, c.y + ry, c.z + rz);
    var s = p.E.OrderByDescending(x => x).ToArray(); p.Area = 4 * s[0] * s[1];
    return p;
  }
  static double Dt(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
  static V3 Cr(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  /// The largest separation of two oriented boxes over the 15 SAT axes (negative: overlapping), and that axis pointing a -> b
  /// (a moved by off first).
  static double Gap(Pc a, Pc b, V3 off, out V3 axis) {
    var T = b.C - (a.C + off); double best = -1e9; var bestAxis = new V3(0, 1, 0);
    void Test(V3 L) {
      double ra = 0, rb = 0; for (int i = 0; i < 3; i++) { ra += a.E[i] * Math.Abs(Dt(a.A[i], L)); rb += b.E[i] * Math.Abs(Dt(b.A[i], L)); }
      double t = Dt(T, L), s = Math.Abs(t) - ra - rb; if (s > best) { best = s; bestAxis = t >= 0 ? L : L * -1; }
    }
    for (int i = 0; i < 3; i++) { Test(a.A[i]); Test(b.A[i]); }
    for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) { var c = Cr(a.A[i], b.A[j]); double l = c.Len(); if (l > 1e-4) Test(c * (1 / l)); }
    axis = bestAxis; return best;
  }
  /// Distance along the unit direction d from o to the oriented box b (slabs in its own frame; a plate is a zero-thick
  /// slab), 0 from inside it, 1e9 when the ray misses it.
  static double Ray(Pc b, V3 o, V3 d) {
    var r = o - b.C; double tmin = -1e9, tmax = 1e9;
    for (int i = 0; i < 3; i++) {
      double oi = Dt(r, b.A[i]), di = Dt(d, b.A[i]), e = b.E[i] + 1e-6;
      if (Math.Abs(di) < 1e-9) { if (Math.Abs(oi) > e) return 1e9; continue; }
      double t1 = (-e - oi) / di, t2 = (e - oi) / di; if (t1 > t2) (t1, t2) = (t2, t1);
      tmin = Math.Max(tmin, t1); tmax = Math.Min(tmax, t2); if (tmin > tmax) return 1e9;
    }
    return tmax < 0 ? 1e9 : Math.Max(tmin, 0);
  }
  static bool Near(V3 amn, V3 amx, V3 bmn, V3 bmx, double t) => !(bmn.x > amx.x + t || amn.x > bmx.x + t || bmn.y > amx.y + t || amn.y > bmx.y + t || bmn.z > amx.z + t || amn.z > bmx.z + t);
  static string P(V3 v) => $"{Y.F(v.x)} {Y.F(v.y)} {Y.F(v.z)}";

  public static void Run(string[] a) {
    string inPath = a[1], outPath = a[2], tsv = a[3];
    string Opt(string k, string d) => a.Skip(4).FirstOrDefault(o => o.StartsWith(k + "="))?.Substring(k.Length + 1) ?? d;
    double tol = D(Opt("tol", "0.04")), snap = D(Opt("snap", "0.6")), hang = D(Opt("hang", "1.5")); const double leave = 0.01;
    var skip = new HashSet<string>(Opt("skip", "").Split(',', StringSplitOptions.RemoveEmptyEntries));
    var dropRe = Opt("drop", null) is string dr ? new Regex(dr, RegexOptions.IgnoreCase) : null; string report = Opt("report", null);
    var keepRe = Opt("keep", null) is string kr ? new Regex(kr, RegexOptions.IgnoreCase) : null;
    var mb = new Dictionary<string, (V3 c, V3 e)>();
    foreach (var l in File.ReadLines(tsv)) { var f = l.Split('\t'); if (f.Length < 6) continue; var n = f[5].Split(' ').Select(D).ToArray(); if (!mb.ContainsKey(f[2])) mb[f[2]] = (new V3(n[0], n[1], n[2]), new V3(n[3], n[4], n[5])); }
    var lines = File.ReadAllLines(inPath); var M = new Dictionary<int, string>(); var T = new Dictionary<int, string>();
    var vis = new List<Pc>(); var col = new List<Pc>();
    for (int li = 0; li < lines.Length; li++) {
      var l = lines[li]; if (l.Length < 2 || l[0] == '#') continue; var t = l.Split(' ');
      if (t[0] == "M") { M[int.Parse(t[1])] = t[2]; continue; }
      if (t[0] == "T") { T[int.Parse(t[1])] = t[2]; continue; }
      if (t[0] == "R" || (t[0] == "C" && t[1] == "mesh")) {
        int o = t[0] == "R" ? 1 : 2; string name = M.GetValueOrDefault(int.Parse(t[o]), "?");
        if (!mb.TryGetValue(name, out var bb)) continue;
        var p = new V3(D(t[3]), D(t[4]), D(t[5])); var q = new Q(D(t[6]), D(t[7]), D(t[8]), D(t[9])); var s = new V3(D(t[10]), D(t[11]), D(t[12]));
        var pc = Box(t[0] == "R" ? 'R' : 'c', name, "", p + q.Rot(bb.c.Mul(s)), q, bb.e.Mul(s), li);
        if (t[0] == "C") { col.Add(pc); continue; }
        string mat = T.GetValueOrDefault(int.Parse(t[2].Split(',')[0]), "?"); pc.Mat = mat.TrimStart('?');
        if (skip.Contains(name) || name.StartsWith("LightArchShaft", StringComparison.OrdinalIgnoreCase) || NotASurface(pc.Mat) || LookSkip.IsMatch(pc.Mat)) continue;
        vis.Add(pc); continue;
      }
      if (t[0] == "C" && t[1] == "box") { col.Add(Box('c', "box", "", new V3(D(t[2]), D(t[3]), D(t[4])), new Q(D(t[5]), D(t[6]), D(t[7]), D(t[8])), new V3(D(t[9]) / 2, D(t[10]) / 2, D(t[11]) / 2), li)); continue; }
      if (t[0] == "C" && t[1] == "sphere") { double r = D(t[5]); col.Add(Box('c', "sphere", "", new V3(D(t[2]), D(t[3]), D(t[4])), Q.I, new V3(r, r, r), li)); continue; }
      if (t[0] == "C" && t[1] == "capsule") { double r = D(t[9]), h = D(t[10]); col.Add(Box('c', "capsule", "", new V3(D(t[2]), D(t[3]), D(t[4])), new Q(D(t[5]), D(t[6]), D(t[7]), D(t[8])), new V3(r, Math.Max(r, h / 2), r), li)); continue; }
      if (t[0] == "G" && (t[1] == "plate" || t[1] == "dress")) {
        var e = (Key(t, "e") ?? "0,0").Split(',').Select(D).ToArray(); var c = new V3(D(t[3]), D(t[4]), D(t[5])); var q = new Q(D(t[6]), D(t[7]), D(t[8]), D(t[9]));
        if (t[1] == "plate") { if (e[0] <= 0 || e[1] <= 0 || NotASurface(t[2]) || LookSkip.IsMatch(t[2])) continue; vis.Add(Box('p', t[2], t[2], c, q, new V3(e[0], e[1], 0), li)); }
        else {
          string m = (Key(t, "m") ?? "").TrimStart('?');
          if (e.Length < 3 || m.Length == 0 || NotASurface(m) || LookSkip.IsMatch(m) || e.Max() > 30 || (t[2].StartsWith("Cover_Glass", StringComparison.OrdinalIgnoreCase) && m.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) < 0)) continue;
          vis.Add(Box('d', t[2], m, c, q, new V3(e[0], e[1], e[2]), li));
        }
      }
    }
    foreach (var sp in Opt("support", "").Split(';', StringSplitOptions.RemoveEmptyEntries)) { var v = sp.Split(',').Select(D).ToArray(); vis.Add(Box('s', "support", "", new V3(v[0], v[1], v[2]), Q.I, new V3(v[3] / 2, v[4] / 2, v[5] / 2), -1)); }
    for (int i = 0; i < vis.Count; i++) vis[i].Id = i;
    // connected groups (sweep and prune on x)
    var par = Enumerable.Range(0, vis.Count).ToArray();
    int Find(int i) { while (par[i] != i) { par[i] = par[par[i]]; i = par[i]; } return i; }
    bool Touch(Pc x, Pc y, V3 off) => Gap(x, y, off, out _) <= tol;
    var sorted = vis.OrderBy(p => p.Mn.x).ToList();
    for (int i = 0; i < sorted.Count; i++) {
      var x = sorted[i];
      for (int j = i + 1; j < sorted.Count; j++) {
        var y = sorted[j]; if (y.Mn.x > x.Mx.x + tol) break;
        if (!Near(x.Mn, x.Mx, y.Mn, y.Mx, tol) || Find(x.Id) == Find(y.Id)) continue;
        if (Touch(x, y, default)) par[Find(x.Id)] = Find(y.Id);
      }
    }
    var groups = vis.GroupBy(p => Find(p.Id)).Select(g => g.ToList()).OrderByDescending(g => g.Sum(p => p.Area)).ToList();
    // the supports so far (the room, then each fixed group) in a 2 m grid; very big pieces in a list
    var grid = new Dictionary<(int, int, int), List<Pc>>(); var bigs = new List<Pc>(); const double cell = 2;
    (int, int, int) Cl(V3 v) => ((int)Math.Floor(v.x / cell), (int)Math.Floor(v.y / cell), (int)Math.Floor(v.z / cell));
    void AddSupport(Pc p, V3 off) {
      var s = new Pc { Kind = p.Kind, Name = p.Name, Mat = p.Mat, C = p.C + off, A = p.A, E = p.E, Mn = p.Mn + off, Mx = p.Mx + off, Area = p.Area, Line = p.Line, Id = p.Id };
      var a0 = Cl(s.Mn); var a1 = Cl(s.Mx); long n = (long)(a1.Item1 - a0.Item1 + 1) * (a1.Item2 - a0.Item2 + 1) * (a1.Item3 - a0.Item3 + 1);
      if (n > 4000) { bigs.Add(s); return; }
      for (int x = a0.Item1; x <= a1.Item1; x++) for (int y = a0.Item2; y <= a1.Item2; y++) for (int z = a0.Item3; z <= a1.Item3; z++) { if (!grid.TryGetValue((x, y, z), out var li)) grid[(x, y, z)] = li = new List<Pc>(); li.Add(s); }
    }
    List<Pc> Around(V3 mn, V3 mx, double r) {
      var res = new HashSet<Pc>(bigs.Where(b => Near(mn, mx, b.Mn, b.Mx, r)));
      var a0 = Cl(mn - new V3(r, r, r)); var a1 = Cl(mx + new V3(r, r, r));
      for (int x = a0.Item1; x <= a1.Item1; x++) for (int y = a0.Item2; y <= a1.Item2; y++) for (int z = a0.Item3; z <= a1.Item3; z++)
        if (grid.TryGetValue((x, y, z), out var li)) foreach (var s in li) if (Near(mn, mx, s.Mn, s.Mx, r)) res.Add(s);
      return res.ToList();
    }
    foreach (var p in groups[0]) AddSupport(p, default);
    var drop = new HashSet<int>(); var move = new Dictionary<int, V3>(); var added = new List<string>(); var rep = new StringBuilder();
    rep.Append("group\tpieces\tarea\tcentre\tfix\tgap\tbelow\tabove\tmove\tnearest\tnames\tlines\n");
    var count = new Dictionary<string, int>();
    for (int gi = 1; gi < groups.Count; gi++) {
      var g = groups[gi];
      if (g.All(p => p.Kind == 's')) { foreach (var p in g) AddSupport(p, default); continue; }
      var mn = g.Select(p => p.Mn).Aggregate(V3.Min); var mx = g.Select(p => p.Mx).Aggregate(V3.Max); var cen = (mn + mx) * 0.5;
      bool glow = g.All(p => Glow.IsMatch(p.Mat ?? "") || Glow.IsMatch(p.Name));
      var near = Around(mn, mx, Math.Max(snap, hang) + 0.1);
      // the nearest support (SAT gap and its axis), the nearest one under the group's footprint and over its centre
      double gap = 1e9; V3 axis = default; string who = "-";
      foreach (var p in g) foreach (var s in near) { if (!Near(p.Mn, p.Mx, s.Mn, s.Mx, snap + 0.1)) continue; double d = Gap(p, s, default, out var ax); if (d < gap) { gap = d; axis = ax; who = s.Kind + ":" + s.Name; } }
      // the nearest support straight under its pieces (rays down from each piece's lowest point, the 60 biggest) and
      // straight over its centre (a ray up from its top): the surfaces themselves, not their bounds (tilted plates)
      double below = 1e9, above = 1e9;
      foreach (var p in g.OrderByDescending(p => p.Area).Take(60)) {
        var o = new V3(p.C.x, p.Mn.y + 0.005, p.C.z);
        foreach (var s in near) { if (s.Mx.x < o.x - 0.01 || s.Mn.x > o.x + 0.01 || s.Mx.z < o.z - 0.01 || s.Mn.z > o.z + 0.01 || s.Mn.y > o.y) continue; double t = Ray(s, o, new V3(0, -1, 0)); if (t < below) below = t; }
      }
      {
        var o = new V3(cen.x, mx.y - 0.005, cen.z);
        foreach (var s in near) { if (s.Mx.x < o.x - 0.01 || s.Mn.x > o.x + 0.01 || s.Mx.z < o.z - 0.01 || s.Mn.z > o.z + 0.01 || s.Mx.y < o.y) continue; double t = Ray(s, o, new V3(0, 1, 0)); if (t < above) above = t; }
      }
      if (below < 1e8) below = Math.Max(0, below - 0.005);
      if (above < 1e8) above = Math.Max(0, above - 0.005);
      // its own colliders (touching the group and nothing of the supports) and their footprint: a body to stand on or
      // walk into (a platform, a boulder), not a prop's own collider (barrels, a lamp can)
      double ownFoot = 0;
      foreach (var c in col) {
        if (!Near(mn, mx, c.Mn, c.Mx, tol) || !g.Any(p => Near(p.Mn, p.Mx, c.Mn, c.Mx, tol) && Touch(p, c, default))) continue;
        if (!Around(c.Mn, c.Mx, tol).Any(s => Near(c.Mn, c.Mx, s.Mn, s.Mx, tol) && Touch(c, s, default))) ownFoot += (c.Mx.x - c.Mn.x) * (c.Mx.z - c.Mn.z);
      }
      // and its up-facing area (plates facing up, the tops of boxes): something to stand on
      double flat = 0;
      foreach (var p in g) for (int i = 0; i < 3; i++) if (Math.Abs(p.A[i].y) > 0.9 && (p.Kind != 'p' || i == 2)) flat += 4 * p.E[(i + 1) % 3] * p.E[(i + 2) % 3];
      double area = g.Sum(p => p.Area);
      // keep= names the bodies that float in Rec Room too: a fifth of the group's area in them
      double keepA = keepRe == null ? 0 : g.Where(p => keepRe.IsMatch(p.Mat ?? "") || keepRe.IsMatch(p.Name)).Sum(p => p.Area);
      string fix; V3 off = default;
      if (keepA >= 0.2 * area && !(below > 0 && below <= 0.25)) fix = "keep";
      else if (dropRe != null && g.All(p => dropRe.IsMatch(p.Mat ?? ""))) fix = "drop";
      else if (below > 0 && below <= snap && below <= gap * 2 + 0.03) { fix = "down"; off = new V3(0, -(below - leave), 0); }
      else if (glow && gap <= 0.1) { fix = "snap"; off = axis * (gap - leave); }
      else if (glow && above <= hang) fix = "hang";
      else if (!glow && gap <= snap) { fix = "snap"; off = axis * (gap - leave); }
      else fix = "out";
      if (fix == "snap" || fix == "down") {
        // the separating axis (or the ray down) may leave the group apart on another axis: a few more steps towards the nearest support
        for (int it = 0; it < 4; it++) {
          double g2 = 1e9; V3 ax2 = default;
          foreach (var p in g) foreach (var s in near) { if (!Near(p.Mn + off, p.Mx + off, s.Mn, s.Mx, snap + 0.1)) continue; double d = Gap(p, s, off, out var ax); if (d < g2) { g2 = d; ax2 = ax; } }
          if (g2 <= tol) break;
          off = off + ax2 * (g2 - leave);
        }
      }
      if (fix == "drop" || fix == "out") { foreach (var p in g) if (p.Line >= 0) drop.Add(p.Line); }
      else {
        if (fix == "down" || fix == "snap") foreach (var p in g) if (p.Line >= 0) move[p.Line] = off;
        if (fix == "hang") {
          var sc = new V3(cen.x, mx.y + above * 0.5, cen.z);
          added.Add($"G dress Glowstick_String {P(sc)} 0 0 0 1 e=0.004,{Y.F3(above * 0.5 + 0.01)},0.004 c=0.08,0.08,0.08,1 m=Metal_Dull_Dark_Mat");
        }
        foreach (var p in g) AddSupport(p, off);
      }
      count[fix] = count.GetValueOrDefault(fix) + 1;
      var names = string.Join(",", g.GroupBy(p => p.Kind + ":" + p.Name + (p.Kind == 'd' ? "|" + p.Mat : "")).OrderByDescending(x => x.Count()).Take(4).Select(x => x.Key + "x" + x.Count()));
      rep.Append($"{gi}\t{g.Count}\t{g.Sum(p => p.Area):0.00}\t{P(cen)}\t{fix} (collider {ownFoot:0.0} m2, top {flat:0.0} m2)\t{(gap < 1e8 ? gap.ToString("0.000") : "-")}\t{(below < 1e8 ? below.ToString("0.000") : "-")}\t{(above < 1e8 ? above.ToString("0.000") : "-")}\t{P(off)}\t{who}\t{names}\t{string.Join(",", g.Take(4).Select(p => p.Line + 1))}\n");
    }
    // rewrite: the summary after the header comments, dropped lines out, moved lines shifted, the strings at the end
    var sb = new StringBuilder(); bool noted = false;
    string summary = $"# gtscene float (iteration 13 support test, tol {tol} m): {groups.Count - 1} floating groups of {vis.Count} drawn pieces: {string.Join(", ", count.OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value))}; {drop.Count} pieces left out, {move.Count} moved, {added.Count} strings";
    for (int li = 0; li < lines.Length; li++) {
      var l = lines[li];
      if (l.StartsWith("# gtscene float ")) continue;
      if (!noted && li > 0 && !l.StartsWith("#") && !l.StartsWith("B ")) { sb.Append(summary).Append('\n'); noted = true; }
      if (drop.Contains(li)) continue;
      if (move.TryGetValue(li, out var off)) {
        var t = l.Split(' ');
        t[3] = Y.F(D(t[3]) + off.x); t[4] = Y.F(D(t[4]) + off.y); t[5] = Y.F(D(t[5]) + off.z); l = string.Join(" ", t);
      }
      sb.Append(l).Append('\n');
    }
    foreach (var l in added) sb.Append(l).Append('\n');
    File.WriteAllText(outPath, sb.ToString());
    if (report != null) File.WriteAllText(report, rep.ToString());
    Console.WriteLine($"{Path.GetFileName(inPath)}: {summary.Substring(2)}");
  }
}
