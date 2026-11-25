// rcref_cache: a REFERENCE software renderer for Rec Room's own July 2025 Rec Center, LOCAL ONLY.
// Source: the Studio scene "RecCenter-RecCenter_071825" that Rec Room left in the user's download cache
// (%USERPROFILE%\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache: scene bundle 1AF.data + dependency bundles
// 183..1AE). The bundles are read IN PLACE as data through ../rcscene's reader (Rc.cs, compiled in unchanged). The only
// outputs are rendered pixels of local comparison PNGs under DevNotes/refdata/rc_cache_ref/ and the frame numbers in
// DevNotes/refdata/rc_cache/frame.json. Nothing (mesh, texture, UV) is copied anywhere, nothing goes into the world.
//
// Sibling of ../rcref (the Android level7 renderer): the same rasteriser, pose files, PNG writer and lighting model,
// fed from the cache scene instead of the APK:
//   - every active, enabled, LOD0 MeshRenderer / SkinnedMeshRenderer (bind pose) with its mesh: static-batched
//     renderers draw their range of the "Combined Mesh (root: scene)" (vertices already in world space);
//   - materials from their saved properties (the Studio shader bundle is not in the cache; the shader PPtr ids name the
//     stock shaders: URP Lit / Unlit / Terrain Lit / TMP / particles / trees): _BaseMap x tiling/offset, _BaseColor,
//     _Surface/_Blend (fade, additive, premultiplied), _AlphaClip/_Cutoff, _Cull, _EMISSION + _EmissionColor x
//     _EmissionMap, _Smoothness/_Metallic;
//   - albedo (linear) as Rec Room's AG shaders: tex.rgb * lerp(1, vc * tint, tex.a) for opaque materials (DXT1: a = 1,
//     so tex * vc * tint); transparent and cut-out materials: tex.rgb * vc * tint with alpha = tex.a * tint.a * vc.a;
//   - PC textures decoded here: DXT1/BC1, DXT5/BC3, BC4, BC5, BC7, Alpha8/R8, RGB24, RGBA32, ARGB32, BGRA32, RGB565,
//     RGBAHalf (cubemaps), with a box-filtered mip pyramid, bilinear repeat sampling, per-pixel mip;
//   - RenderSettings trilight ambient (or the serialized SH probe), linear fog, the skybox material, the custom
//     reflection cubemap when decodable; the directional KeyLight with a sun shadow map; every enabled point / spot
//     light (Rec Room's compact falloff as ../rcref, or URP's with att=urp); the terrain (height grid + splat layers).
//
// Commands:
//   nodes [pathRe]                       every GameObject: path, active, world position, yaw, scale, mesh, world AABB
//   mats                                 every drawn material as interpreted (mode, texture, alpha mean, tint, glow)
//   env                                  lights and RenderSettings as read
//   frame <level7_nodes.tsv> <out.json>  rigid transform cache scene -> room frame (= Android level7 frame) from the
//                                        same building pieces (tsv from: bash ../rcref/run.sh nodes ../../apkwork level7)
//   render <poses.json> <frame.json> <outDir> [only=<re>] [ss=2] [texmax=1024] [shadow=4096] [att=rr|urp]
//          [amb=trilight|probe] [exp=1] [spec=1] [skipmat=<re>] [shafts=0|1] [lights=1]
//                                        poses in the ROOM frame (rcref's poses.json) are moved into the cache frame
//                                        with frame.json; one <name>.png per pose (+ sheets like ../rcref).
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

static class CRMain {
  static int Main(string[] a) {
    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
    if (a.Length == 0) { Console.WriteLine("rcref_cache nodes [re] | mats | env | frame <l7nodes.tsv> <out.json> | render <poses.json> <frame.json> <outDir> [opts]"); return 1; }
    Common.Load(@"E:\SteamLibrary\steamapps\common\RecRoom\UnityPlayer.dll");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    Rc.OpenScene();
    switch (a[0]) {
      case "nodes": CRNodes.Dump(a.Length > 1 ? a[1] : ".", Console.Out); break;
      case "mats": CRScene.Load(1024, true); CRScene.ListMats(); break;
      case "env": CRScene.Load(64, false); break;
      case "skydump": CRScene.Load(64, false); CRRender.SkyDump(a[1]); break;
      case "frame": CRFrame.Run(a[1], a[2]); break;
      case "render": CRRender.Run(a[1], a[2], a[3], a.Skip(4).ToArray()); break;
      default: Console.WriteLine("unknown command"); return 1;
    }
    Console.Error.WriteLine($"[{sw.Elapsed.TotalSeconds:F1} s]");
    return 0;
  }
}

// ------------------------------------------------------------------------------------------------ math helpers
static class CM {
  public static double Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
  public static V3 Cross(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  public static V3 Nrm(V3 a) { var l = a.Len(); return l < 1e-12 ? new V3(0, 1, 0) : a * (1 / l); }
  public static V3 P(double[] m, V3 v) => new(m[0] * v.x + m[1] * v.y + m[2] * v.z + m[3], m[4] * v.x + m[5] * v.y + m[6] * v.z + m[7], m[8] * v.x + m[9] * v.y + m[10] * v.z + m[11]);
  public static V3 D(double[] m, V3 v) => new(m[0] * v.x + m[1] * v.y + m[2] * v.z, m[4] * v.x + m[5] * v.y + m[6] * v.z, m[8] * v.x + m[9] * v.y + m[10] * v.z);
  public static double Det(double[] m) => m[0] * (m[5] * m[10] - m[6] * m[9]) - m[1] * (m[4] * m[10] - m[6] * m[8]) + m[2] * (m[4] * m[9] - m[5] * m[8]);
  /// inverse transpose of the 3x3 part (normals)
  public static double[] NormalMat(double[] m) {
    double a = m[0], b = m[1], c = m[2], d = m[4], e = m[5], f = m[6], g = m[8], h = m[9], i = m[10];
    double det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g); if (Math.Abs(det) < 1e-18) det = 1e-18;
    // inverse = adj / det; transpose of inverse = cofactor / det
    return new[] { (e * i - f * h) / det, -(d * i - f * g) / det, (d * h - e * g) / det, -(b * i - c * h) / det, (a * i - c * g) / det, -(a * h - b * g) / det, (b * f - c * e) / det, -(a * f - c * d) / det, (a * e - b * d) / det };
  }
  public static V3 N3(double[] n, V3 v) => new(n[0] * v.x + n[1] * v.y + n[2] * v.z, n[3] * v.x + n[4] * v.y + n[5] * v.z, n[6] * v.x + n[7] * v.y + n[8] * v.z);
  public static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
  public static double Gam(double c) { c = Math.Clamp(c, 0, 1); return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055; }
  public static double[] LinC(double[] c) => new[] { Lin(c[0]), Lin(c[1]), Lin(c[2]) };
  public static string F(double v) => Y.F3(v);
  /// Unity yaw (degrees, clockwise seen from above): (x, z) -> (x cos + z sin, -x sin + z cos)
  public static V3 Yaw(V3 p, double deg) { double r = deg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r); return new V3(p.x * c + p.z * s, p.y, -p.x * s + p.z * c); }
}

// ------------------------------------------------------------------------------------------------ nodes and bounds
static class CRNodes {
  static HashSet<long> lodSkip;
  public static HashSet<long> LodSkip() {
    if (lodSkip != null) return lodSkip; lodSkip = new();
    foreach (var n in RcConvert.NodeList()) {
      var lg = n.Comp(205); if (lg == null) continue; var lods = Rc.L(Rc.Read(Rc.Scene, lg), "m_LODs"); if (lods == null) continue;
      for (int k = 1; k < lods.Count; k++) foreach (var rr in Rc.L(lods[k], "renderers") ?? new()) { var pp = Rc.D(rr, "renderer"); if (pp != null) lodSkip.Add(Convert.ToInt64(pp["m_PathID"])); }
    }
    return lodSkip;
  }
  /// world AABB of a node's renderer (static batch: the union of its submesh boxes, which are in world space), mesh name
  public static (V3 lo, V3 hi, string mesh, bool batched)? Bounds(Node n) {
    var f = Rc.Scene; var rend = n.Comp(23) ?? n.Comp(137); if (rend == null) return null;
    var rd = Rc.Read(f, rend); object mref;
    if (rend.ClassId == 137) mref = rd.GetValueOrDefault("m_Mesh"); else { var filt = n.Comp(33); if (filt == null) return null; mref = Rc.Read(f, filt).GetValueOrDefault("m_Mesh"); }
    var (mf, mo) = Rc.Ptr(f, mref); if (mo == null) return null;
    var mg = MeshGeo.Of(mf, mo, false); if (mg == null) return null;
    var sbi = Rc.D(rd, "m_StaticBatchInfo"); int first = (int)Rc.N(sbi, "firstSubMesh"), count = (int)Rc.N(sbi, "subMeshCount");
    bool batched = count > 0 && (mg.Name ?? "").StartsWith("Combined Mesh");
    V3 lo = new(1e9, 1e9, 1e9), hi = new(-1e9, -1e9, -1e9);
    if (batched) { for (int k = first; k < first + count && k < mg.SubBox.Count; k++) { var (c, e) = mg.SubBox[k]; lo = V3.Min(lo, c - e); hi = V3.Max(hi, c + e); } }
    else for (int i = 0; i < 8; i++) { var p = CM.P(n.M, new V3(mg.C.x + ((i & 1) == 0 ? -mg.E.x : mg.E.x), mg.C.y + ((i & 2) == 0 ? -mg.E.y : mg.E.y), mg.C.z + ((i & 4) == 0 ? -mg.E.z : mg.E.z))); lo = V3.Min(lo, p); hi = V3.Max(hi, p); }
    if (hi.x < lo.x) return null;
    return (lo, hi, mg.Name, batched);
  }
  public static void Dump(string re, TextWriter o) {
    var rx = new Regex(re, RegexOptions.IgnoreCase);
    o.WriteLine("path\tactive\tx\ty\tz\tyaw\tscale\tmesh\tmin\tmax");
    foreach (var n in RcConvert.NodeList()) {
      if (!rx.IsMatch(n.Path)) continue;
      var fw = CM.D(n.M, new V3(0, 0, 1)); double yaw = Math.Atan2(fw.x, fw.z) * 180 / Math.PI;
      var sx = new V3(n.M[0], n.M[4], n.M[8]).Len(); var sy = new V3(n.M[1], n.M[5], n.M[9]).Len(); var sz = new V3(n.M[2], n.M[6], n.M[10]).Len();
      var b = Bounds(n);
      o.WriteLine($"{n.Path}\t{(n.Active ? 1 : 0)}\t{CM.F(n.M[3])}\t{CM.F(n.M[7])}\t{CM.F(n.M[11])}\t{yaw:F1}\t{CM.F(sx)},{CM.F(sy)},{CM.F(sz)}\t{(b == null ? "" : b.Value.mesh + (b.Value.batched ? "*" : ""))}\t{(b == null ? "" : $"{CM.F(b.Value.lo.x)},{CM.F(b.Value.lo.y)},{CM.F(b.Value.lo.z)}")}\t{(b == null ? "" : $"{CM.F(b.Value.hi.x)},{CM.F(b.Value.hi.y)},{CM.F(b.Value.hi.z)}")}");
    }
  }
}

// ------------------------------------------------------------------------------------------------ frame
static class CRFrame {
  sealed class Pc { public string Key, Full, Path, Mesh; public V3 P, Lo, Hi; public bool HasBox, Active; }
  static List<Pc> ReadTsv(IEnumerable<string> lines) {
    var r = new List<Pc>();
    foreach (var l in lines.Skip(1)) {
      var c = l.Split('\t'); if (c.Length < 10) continue;
      double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
      var p = new Pc { Path = c[0], Active = c[1] == "1", P = new V3(D(c[2]), D(c[3]), D(c[4])), Mesh = c[7].TrimEnd('*') };
      if (c[8].Length > 0) { var a = c[8].Split(',').Select(D).ToArray(); var b = c[9].Split(',').Select(D).ToArray(); p.Lo = new V3(a[0], a[1], a[2]); p.Hi = new V3(b[0], b[1], b[2]); p.HasBox = true; }
      var seg = p.Path.Split('/'); p.Key = seg.Length >= 2 ? seg[^2] + "/" + seg[^1] : seg[^1]; p.Full = Regex.Replace(p.Path, "^StaticObjects/", "");
      r.Add(p);
    }
    return r;
  }
  static readonly Regex Piece = new(@"kiosk|skylight|Skylight|beam|Beam|RecRoomLogo|Door_Sign|pCube|window|Window|post|Post|frame|upperlevel|stair|Stair|rail|Rail|walls|floor|ceiling|roof|Curtain|Marquee|Locker|Couch|Fig|Poster", RegexOptions.None);

  public static void Run(string l7Path, string outPath) {
    var room = ReadTsv(File.ReadLines(l7Path));
    var sw = new StringWriter(); CRNodes.Dump(".", sw);
    var cache = ReadTsv(sw.ToString().Split('\n').Select(x => x.TrimEnd('\r')));
    Console.WriteLine($"level7 nodes {room.Count}, cache nodes {cache.Count}");
    // candidate pairs: same parent/name, unique on both sides, both drawn (render AABB), active
    var rk = room.Where(x => x.HasBox && x.Active).GroupBy(x => x.Key).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
    var ck = cache.Where(x => x.HasBox && x.Active).GroupBy(x => x.Key).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
    var rf = room.Where(x => x.HasBox && x.Active).GroupBy(x => x.Full).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
    var cf = cache.Where(x => x.HasBox && x.Active).GroupBy(x => x.Full).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
    var pairs = rf.Keys.Intersect(cf.Keys).Select(k => (k, r: rf[k], c: cf[k])).ToList();
    int byPath = pairs.Count;
    foreach (var k in rk.Keys.Intersect(ck.Keys)) if (!pairs.Any(p => ReferenceEquals(p.r, rk[k]) || ReferenceEquals(p.c, ck[k]))) pairs.Add((k, rk[k], ck[k]));
    Console.WriteLine($"pairs drawn in both: {byPath} by the same path (Android root StaticObjects/ dropped), {pairs.Count - byPath} more by the same unique parent/name");
    V3 Ctr(Pc p) => (p.Lo + p.Hi) * 0.5; V3 Sz(Pc p) => p.Hi - p.Lo;
    // yaw multiple of 90: the one with the most pairs whose box sizes agree and whose centres agree with the median shift
    double bestYaw = 0; V3 bestT = default; int bestN = -1; List<(string k, Pc r, Pc c)> bestIn = null;
    foreach (double yaw in new[] { 0.0, 90, 180, 270 }) {
      var ok = pairs.Where(p => { var s = Sz(p.c); var sr = (yaw % 180 == 0) ? s : new V3(s.z, s.y, s.x); var d = (sr - Sz(p.r)).Abs(); return d.Max() < 0.10 && Sz(p.r).Max() > 0.2; }).ToList();
      if (ok.Count == 0) continue;
      var ts = ok.Select(p => Ctr(p.r) - CM.Yaw(Ctr(p.c), yaw)).ToList();
      var med = new V3(Med(ts.Select(t => t.x)), Med(ts.Select(t => t.y)), Med(ts.Select(t => t.z)));
      var inl = ok.Where(p => (Ctr(p.r) - CM.Yaw(Ctr(p.c), yaw) - med).Len() < 0.15).ToList();
      Console.WriteLine($"yaw {yaw}: size-agreeing pairs {ok.Count}, median shift {med}, inliers (< 15 cm) {inl.Count}");
      if (inl.Count > bestN) { bestN = inl.Count; bestYaw = yaw; bestT = med; bestIn = inl; }
    }
    // least squares on the inliers: translation = mean residual; free yaw (2D Procrustes) as a check
    var tl = bestIn.Select(p => Ctr(p.r) - CM.Yaw(Ctr(p.c), bestYaw)).ToList();
    var T = new V3(tl.Average(t => t.x), tl.Average(t => t.y), tl.Average(t => t.z));
    double sxx = 0, sxz = 0; var cr = new V3(bestIn.Average(p => Ctr(p.r).x), 0, bestIn.Average(p => Ctr(p.r).z)); var cc = new V3(bestIn.Average(p => Ctr(p.c).x), 0, bestIn.Average(p => Ctr(p.c).z));
    foreach (var p in bestIn) { var a = CM.Yaw(Ctr(p.c) - cc, bestYaw); var b = Ctr(p.r) - cr; sxx += a.x * b.x + a.z * b.z; sxz += a.z * b.x - a.x * b.z; }
    double freeYaw = Math.Atan2(sxz, sxx) * 180 / Math.PI; // extra yaw (Unity sense) beyond bestYaw
    var res = pairs.Select(p => (p.k, p.r, p.c, d: Ctr(p.r) - (CM.Yaw(Ctr(p.c), bestYaw) + T), pv: p.r.P - (CM.Yaw(p.c.P, bestYaw) + T))).OrderBy(x => x.d.Len()).ToList();
    bool Inl((string k, Pc r, Pc c, V3 d, V3 pv) x) => x.d.Len() < 0.15;
    var inRes = res.Where(Inl).Select(x => x.d.Len()).OrderBy(x => x).ToList(); int inlN = inRes.Count;
    double Q(List<double> l, double q) => l.Count == 0 ? 0 : l[Math.Clamp((int)Math.Round(q * (l.Count - 1)), 0, l.Count - 1)];
    Console.WriteLine($"best: yaw {bestYaw}, T {CM.F(T.x)},{CM.F(T.y)},{CM.F(T.z)}, fit on {bestIn.Count} size-agreeing pairs; {inlN}/{pairs.Count} pairs within 15 cm: residual median {Q(inRes, 0.5) * 100:F1} cm, p90 {Q(inRes, 0.9) * 100:F1} cm, max {inRes.Max() * 100:F1} cm; free yaw check {freeYaw:F3} deg");
    // the building pieces Rec Room renamed in the Studio port (Beams/Beams, Windows/Window_Skylight, ...): matched by
    // geometry under the solved transform (nearest box: centre within 0.5 m, size within 0.25 m), as a check only
    var usedR = new HashSet<Pc>(pairs.Select(p => p.r)); var usedC = new HashSet<Pc>(pairs.Select(p => p.c));
    var geo = new List<(string k, Pc r, Pc c, V3 d)>();
    var cat = new Regex(@"Beams/|Window|Skylight|[Kk]iosk|Door_Sign|RecRoomLogo|Railing|Curtain|Marquee|Collision");
    var freeR = room.Where(x => x.HasBox && x.Active && !usedR.Contains(x)).ToList();
    foreach (var c0 in cache.Where(x => x.HasBox && x.Active && !usedC.Contains(x) && cat.IsMatch(x.Path))) {
      var cc0 = CM.Yaw(Ctr(c0), bestYaw) + T; var s0 = Sz(c0); var sr0 = (bestYaw % 180 == 0) ? s0 : new V3(s0.z, s0.y, s0.x);
      Pc bestR = null; double bestCost = 1e9;
      foreach (var r0 in freeR) { var dc = (Ctr(r0) - cc0).Len(); if (dc > 0.5) continue; var ds = (Sz(r0) - sr0).Abs().Max(); if (ds > 0.25) continue; if (dc + ds < bestCost) { bestCost = dc + ds; bestR = r0; } }
      if (bestR != null) { freeR.Remove(bestR); geo.Add((c0.Path + "  <->  " + bestR.Path, bestR, c0, Ctr(bestR) - cc0)); }
    }
    var geoRes = geo.Select(x => x.d.Len()).OrderBy(x => x).ToList();
    Console.WriteLine($"renamed building pieces matched by geometry: {geo.Count}; residual median {Q(geoRes, 0.5) * 100:F1} cm, p90 {Q(geoRes, 0.9) * 100:F1} cm, max {(geoRes.Count > 0 ? geoRes.Max() * 100 : 0):F1} cm");
    foreach (var x in geo.Where(x => Regex.IsMatch(x.k, "Beams/Beams|Skylight|[Kk]iosk|Door_Sign")).Take(60)) Console.WriteLine($"  geo {x.k,-110} res {CM.F(x.d.x)},{CM.F(x.d.y)},{CM.F(x.d.z)}");
    // the named building pieces the task asked about
    var named = res.Where(x => Piece.IsMatch(x.k)).ToList();
    var sb = new StringBuilder();
    sb.Append("{\n");
    sb.Append("  \"note\": \"Rigid transform from Rec Room's cached July 2025 Studio Rec Center scene (HTTPCache 1AF.data, RecCenter-RecCenter_071825.unity, world space as saved) to the ROOM frame = the Android level7 world frame (DevNotes/refdata/rc_ref/poses.json, Layouts/RecCenter_Builtin.bytes, RrRecCenterLayout). room = Ry(yaw_deg) * cache + translation; Ry = Unity yaw (x' = x cos + z sin, z' = -x sin + z cos). Measured by rcref_cache frame from the world AABB centres of the same building pieces (same parent/name, unique and drawn in both scenes, box sizes agreeing within 10 cm).\",\n");
    sb.Append($"  \"yaw_deg\": {bestYaw},\n  \"translation\": [{CM.F(T.x)}, {CM.F(T.y)}, {CM.F(T.z)}],\n");
    sb.Append($"  \"inverse\": \"cache = Ry(-yaw_deg) * (room - translation)\",\n");
    sb.Append($"  \"free_yaw_check_deg\": {freeYaw:F4},\n");
    sb.Append($"  \"pairs_candidate\": {pairs.Count}, \"pairs_fit\": {bestIn.Count}, \"pairs_within_15cm\": {inlN},\n");
    sb.Append($"  \"residual_cm\": {{\"median\": {Q(inRes, 0.5) * 100:F2}, \"p90\": {Q(inRes, 0.9) * 100:F2}, \"max\": {inRes.Max() * 100:F2}}},\n");
    sb.Append("  \"old_world_frame\": \"RrRecCenterCache.FramePosition (2.05, -0.09, -7.23) / FrameYaw 90 maps the cache scene into the frame of research/rr_reccenter_cache.md (entrance at -Z, origin = kiosk floor centre, Y 0 = hall floor), NOT the level7 frame: doc = Ry(90) * cache + (2.05, -0.09, -7.23). With cache ~= level7 (this file) the doc frame is doc = Ry(90) * room + (2.05, -0.09, -7.23) up to the residuals here.\",\n");
    sb.Append("  \"pieces\": [\n");
    var list = named.Concat(res.Where(x => !Piece.IsMatch(x.k))).ToList();
    for (int i = 0; i < list.Count; i++) {
      var x = list[i]; bool inl = Inl(x);
      sb.Append($"    {{\"piece\": \"{x.k.Replace("\"", "'")}\", \"mesh\": \"{x.r.Mesh}\", \"cache_centre\": [{CM.F(Ctr(x.c).x)}, {CM.F(Ctr(x.c).y)}, {CM.F(Ctr(x.c).z)}], \"room_centre\": [{CM.F(Ctr(x.r).x)}, {CM.F(Ctr(x.r).y)}, {CM.F(Ctr(x.r).z)}], \"residual_m\": [{CM.F(x.d.x)}, {CM.F(x.d.y)}, {CM.F(x.d.z)}], \"pivot_residual_m\": [{CM.F(x.pv.x)}, {CM.F(x.pv.y)}, {CM.F(x.pv.z)}], \"inlier\": {(inl ? "true" : "false")}}}{(i + 1 < list.Count ? "," : "")}\n");
    }
    sb.Append("  ],\n");
    sb.Append($"  \"geometric_check\": {{\"note\": \"pieces renamed in the Studio port (e.g. Beams/Beams = Android beams/pCube*, Windows/Window_Skylight = skylight/frame), matched as the nearest box under the transform above (centre < 0.5 m, size < 0.25 m); not used for the fit\", \"count\": {geo.Count}, \"residual_cm\": {{\"median\": {Q(geoRes, 0.5) * 100:F2}, \"p90\": {Q(geoRes, 0.9) * 100:F2}, \"max\": {(geoRes.Count > 0 ? geoRes.Max() * 100 : 0):F2}}}, \"pieces\": [\n");
    for (int i = 0; i < geo.Count; i++) { var x = geo[i]; sb.Append($"    {{\"cache\": \"{x.c.Path.Replace("\"", "'")}\", \"room\": \"{x.r.Path.Replace("\"", "'")}\", \"room_centre\": [{CM.F(Ctr(x.r).x)}, {CM.F(Ctr(x.r).y)}, {CM.F(Ctr(x.r).z)}], \"residual_m\": [{CM.F(x.d.x)}, {CM.F(x.d.y)}, {CM.F(x.d.z)}]}}{(i + 1 < geo.Count ? "," : "")}\n"); }
    sb.Append("  ]}\n}\n");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
    File.WriteAllText(outPath, sb.ToString());
    Console.WriteLine($"named building pieces ({named.Count}):");
    foreach (var x in named.Take(80)) Console.WriteLine($"  {x.k,-60} res {CM.F(x.d.x)},{CM.F(x.d.y)},{CM.F(x.d.z)}  {(Inl(x) ? "" : "(outlier)")}");
    Console.WriteLine("-> " + outPath);
  }
  static double Med(IEnumerable<double> v) { var l = v.OrderBy(x => x).ToList(); return l.Count == 0 ? 0 : l[l.Count / 2]; }
  /// reads frame.json (yaw_deg, translation)
  public static (double yaw, V3 t) Read(string path) {
    var s = File.ReadAllText(path);
    var y = double.Parse(Regex.Match(s, "\"yaw_deg\"\\s*:\\s*(-?[0-9.eE+-]+)").Groups[1].Value, CultureInfo.InvariantCulture);
    var t = Regex.Match(s, "\"translation\"\\s*:\\s*\\[([^\\]]*)\\]").Groups[1].Value.Split(',').Select(x => double.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
    return (y, new V3(t[0], t[1], t[2]));
  }
}

// ------------------------------------------------------------------------------------------------ textures
sealed class CTex {
  public float[] Hdr; // linear rgb per texel (BC6H / half cubemaps)
  public string Name; public int W, H; public List<byte[]> Mips = new(); public List<(int w, int h)> Size = new(); public double MeanA = 1; public bool HasAlpha;
  public static readonly float[] Lin = Enumerable.Range(0, 256).Select(i => (float)CM.Lin(i / 255.0)).ToArray();
  public void Build(byte[] px, int w, int h) {
    Mips.Add(px); Size.Add((w, h)); W = w; H = h;
    double sa = 0; for (int i = 3; i < px.Length; i += 4) sa += px[i]; MeanA = sa / 255.0 / Math.Max(1, w * h);
    while (w > 1 || h > 1) {
      int nw = Math.Max(1, w / 2), nh = Math.Max(1, h / 2); var o = new byte[nw * nh * 4]; var p = Mips[^1];
      for (int y = 0; y < nh; y++) for (int x = 0; x < nw; x++) for (int c = 0; c < 4; c++) {
        int x0 = Math.Min(w - 1, x * 2), x1 = Math.Min(w - 1, x * 2 + 1), y0 = Math.Min(h - 1, y * 2), y1 = Math.Min(h - 1, y * 2 + 1);
        o[(y * nw + x) * 4 + c] = (byte)((p[(y0 * w + x0) * 4 + c] + p[(y0 * w + x1) * 4 + c] + p[(y1 * w + x0) * 4 + c] + p[(y1 * w + x1) * 4 + c] + 2) >> 2);
      }
      Mips.Add(o); Size.Add((nw, nh)); w = nw; h = nh;
    }
  }
  /// bilinear, repeat; rgb linear (srgb texture) or raw (linear texture), a 0..1. lod = log2(texels per pixel) at mip 0.
  public bool Linear;
  public void Sample(double u, double v, double lod, out double r, out double g, out double b, out double a) {
    int m = Math.Clamp((int)Math.Floor(lod), 0, Mips.Count - 1); var p = Mips[m]; var (w, h) = Size[m];
    double x = u * w - 0.5, y = v * h - 0.5; double fx = Math.Floor(x), fy = Math.Floor(y); double tx = x - fx, ty = y - fy;
    int x0 = Mod((int)fx, w), y0 = Mod((int)fy, h), x1 = (x0 + 1) % w, y1 = (y0 + 1) % h;
    int i00 = (y0 * w + x0) * 4, i10 = (y0 * w + x1) * 4, i01 = (y1 * w + x0) * 4, i11 = (y1 * w + x1) * 4;
    double w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
    if (Linear) {
      r = (p[i00] * w00 + p[i10] * w10 + p[i01] * w01 + p[i11] * w11) / 255.0; g = (p[i00 + 1] * w00 + p[i10 + 1] * w10 + p[i01 + 1] * w01 + p[i11 + 1] * w11) / 255.0; b = (p[i00 + 2] * w00 + p[i10 + 2] * w10 + p[i01 + 2] * w01 + p[i11 + 2] * w11) / 255.0;
    } else {
      r = Lin[p[i00]] * w00 + Lin[p[i10]] * w10 + Lin[p[i01]] * w01 + Lin[p[i11]] * w11;
      g = Lin[p[i00 + 1]] * w00 + Lin[p[i10 + 1]] * w10 + Lin[p[i01 + 1]] * w01 + Lin[p[i11 + 1]] * w11;
      b = Lin[p[i00 + 2]] * w00 + Lin[p[i10 + 2]] * w10 + Lin[p[i01 + 2]] * w01 + Lin[p[i11 + 2]] * w11;
    }
    a = (p[i00 + 3] * w00 + p[i10 + 3] * w10 + p[i01 + 3] * w01 + p[i11 + 3] * w11) / 255.0;
  }
  public double AlphaAt(double u, double v) { var p = Mips[0]; int x = Mod((int)Math.Floor(u * W), W), y = Mod((int)Math.Floor(v * H), H); return p[(y * W + x) * 4 + 3] / 255.0; }
  static int Mod(int a, int n) { int r = a % n; return r < 0 ? r + n : r; }
}

static class TexDec {
  public static int MipBytes(int fmt, int w, int h) => fmt switch {
    10 or 26 => ((w + 3) / 4) * ((h + 3) / 4) * 8, 12 or 27 or 24 or 25 => ((w + 3) / 4) * ((h + 3) / 4) * 16,
    4 or 5 or 14 => w * h * 4, 3 => w * h * 3, 1 or 63 => w * h, 7 or 13 or 2 => w * h * 2, 17 => w * h * 8, 20 => w * h * 16, _ => -1 };
  public static bool Known(int fmt) => MipBytes(fmt, 4, 4) > 0 && fmt != 24 && fmt != 20;
  public static byte[] Bytes(Dictionary<string, object> d) {
    var img = d.GetValueOrDefault("image data") as byte[];
    if (img == null || img.Length == 0) { var sd = Rc.D(d, "m_StreamData"); long size = (long)Rc.N(sd, "size"); if (size > 0) img = Rc.Stream(Rc.S(sd, "path"), (long)Rc.N(sd, "offset"), size); }
    return img;
  }
  static void C565(ushort c, out int r, out int g, out int b) { r = ((c >> 11) & 31) * 255 / 31; g = ((c >> 5) & 63) * 255 / 63; b = (c & 31) * 255 / 31; }
  static void ColorBlock(byte[] s, int o, byte[] dst, int w, int h, int bx, int by, bool dxt1) {
    ushort c0 = BitConverter.ToUInt16(s, o), c1 = BitConverter.ToUInt16(s, o + 2); uint bits = BitConverter.ToUInt32(s, o + 4);
    var pal = new int[4, 4]; C565(c0, out pal[0, 0], out pal[0, 1], out pal[0, 2]); C565(c1, out pal[1, 0], out pal[1, 1], out pal[1, 2]); pal[0, 3] = pal[1, 3] = 255;
    bool four = !dxt1 || c0 > c1;
    for (int k = 0; k < 3; k++) {
      if (four) { pal[2, k] = (2 * pal[0, k] + pal[1, k]) / 3; pal[3, k] = (pal[0, k] + 2 * pal[1, k]) / 3; }
      else { pal[2, k] = (pal[0, k] + pal[1, k]) / 2; pal[3, k] = 0; }
    }
    pal[2, 3] = 255; pal[3, 3] = four ? 255 : 0;
    for (int t = 0; t < 16; t++) {
      int x = bx * 4 + (t & 3), y = by * 4 + (t >> 2); if (x >= w || y >= h) continue; int ix = (int)((bits >> (t * 2)) & 3); int q = (y * w + x) * 4;
      dst[q] = (byte)pal[ix, 0]; dst[q + 1] = (byte)pal[ix, 1]; dst[q + 2] = (byte)pal[ix, 2]; dst[q + 3] = (byte)pal[ix, 3];
    }
  }
  static void AlphaBlock(byte[] s, int o, byte[] dst, int w, int h, int bx, int by, int ch) {
    int a0 = s[o], a1 = s[o + 1]; ulong ab = 0; for (int q = 0; q < 6; q++) ab |= (ulong)s[o + 2 + q] << (8 * q);
    var ap = new int[8]; ap[0] = a0; ap[1] = a1;
    if (a0 > a1) for (int q = 1; q < 7; q++) ap[q + 1] = ((7 - q) * a0 + q * a1) / 7; else { for (int q = 1; q < 5; q++) ap[q + 1] = ((5 - q) * a0 + q * a1) / 5; ap[6] = 0; ap[7] = 255; }
    for (int t = 0; t < 16; t++) { int x = bx * 4 + (t & 3), y = by * 4 + (t >> 2); if (x >= w || y >= h) continue; dst[(y * w + x) * 4 + ch] = (byte)ap[(int)((ab >> (3 * t)) & 7)]; }
  }
  /// RGBA8 pixels of one mip (row 0 = v 0, as Unity stores them)
  public static byte[] Rgba(int fmt, byte[] s, int off, int w, int h) {
    var d = new byte[w * h * 4]; int bw = (w + 3) / 4, bh = (h + 3) / 4;
    switch (fmt) {
      case 10: for (int by = 0; by < bh; by++) for (int bx = 0; bx < bw; bx++) ColorBlock(s, off + (by * bw + bx) * 8, d, w, h, bx, by, true); break;
      case 12: for (int by = 0; by < bh; by++) for (int bx = 0; bx < bw; bx++) { int o = off + (by * bw + bx) * 16; ColorBlock(s, o + 8, d, w, h, bx, by, false); AlphaBlock(s, o, d, w, h, bx, by, 3); } break;
      case 26: for (int by = 0; by < bh; by++) for (int bx = 0; bx < bw; bx++) AlphaBlock(s, off + (by * bw + bx) * 8, d, w, h, bx, by, 0); for (int i = 0; i < w * h; i++) { d[i * 4 + 1] = d[i * 4 + 2] = d[i * 4]; d[i * 4 + 3] = 255; } break;
      case 27: for (int by = 0; by < bh; by++) for (int bx = 0; bx < bw; bx++) { int o = off + (by * bw + bx) * 16; AlphaBlock(s, o, d, w, h, bx, by, 0); AlphaBlock(s, o + 8, d, w, h, bx, by, 1); } for (int i = 0; i < w * h; i++) { d[i * 4 + 2] = 255; d[i * 4 + 3] = 255; } break;
      case 25: Bc7.Decode(s, off, w, h, d); break;
      case 1: for (int i = 0; i < w * h; i++) { d[i * 4] = d[i * 4 + 1] = d[i * 4 + 2] = 255; d[i * 4 + 3] = s[off + i]; } break;
      case 63: for (int i = 0; i < w * h; i++) { d[i * 4] = d[i * 4 + 1] = d[i * 4 + 2] = s[off + i]; d[i * 4 + 3] = 255; } break;
      case 3: for (int i = 0; i < w * h; i++) { d[i * 4] = s[off + i * 3]; d[i * 4 + 1] = s[off + i * 3 + 1]; d[i * 4 + 2] = s[off + i * 3 + 2]; d[i * 4 + 3] = 255; } break;
      case 4: Buffer.BlockCopy(s, off, d, 0, w * h * 4); break;
      case 5: for (int i = 0; i < w * h; i++) { d[i * 4 + 3] = s[off + i * 4]; d[i * 4] = s[off + i * 4 + 1]; d[i * 4 + 1] = s[off + i * 4 + 2]; d[i * 4 + 2] = s[off + i * 4 + 3]; } break;
      case 14: for (int i = 0; i < w * h; i++) { d[i * 4 + 2] = s[off + i * 4]; d[i * 4 + 1] = s[off + i * 4 + 1]; d[i * 4] = s[off + i * 4 + 2]; d[i * 4 + 3] = s[off + i * 4 + 3]; } break;
      case 7: for (int i = 0; i < w * h; i++) { C565(BitConverter.ToUInt16(s, off + i * 2), out int r, out int g, out int b); d[i * 4] = (byte)r; d[i * 4 + 1] = (byte)g; d[i * 4 + 2] = (byte)b; d[i * 4 + 3] = 255; } break;
      case 17: for (int i = 0; i < w * h; i++) for (int k = 0; k < 4; k++) d[i * 4 + k] = (byte)Math.Clamp(Math.Round((double)BitConverter.ToHalf(s, off + i * 8 + k * 2) * 255), 0, 255); break;
      default: return null;
    }
    return d;
  }
}

/// BC7 (mode-complete) block decoder.
static class Bc7 {
  static readonly int[] NS = { 3, 2, 3, 2, 1, 1, 1, 2 }, PB = { 4, 6, 6, 0, 0, 0, 0, 6 }, RB = { 0, 0, 0, 0, 2, 2, 0, 0 }, ISB = { 0, 0, 0, 0, 1, 0, 0, 0 };
  static readonly int[] CB = { 4, 6, 5, 7, 5, 7, 7, 5 }, AB = { 0, 0, 0, 0, 6, 8, 7, 5 }, EPB = { 1, 0, 0, 1, 0, 0, 1, 1 }, SPB = { 0, 1, 0, 0, 0, 0, 0, 0 }, IB = { 3, 3, 2, 2, 2, 2, 4, 2 }, IB2 = { 0, 0, 0, 0, 3, 2, 0, 0 };
  static readonly uint[] P2 = { 0xCCCC, 0x8888, 0xEEEE, 0xECC8, 0xC880, 0xFEEC, 0xFEC8, 0xEC80, 0xC800, 0xFFEC, 0xFE80, 0xE800, 0xFFE8, 0xFF00, 0xFFF0, 0xF000, 0xF710, 0x008E, 0x7100, 0x08CE, 0x008C, 0x7310, 0x3100, 0x8CCE, 0x088C, 0x3110, 0x6666, 0x366C, 0x17E8, 0x0FF0, 0x718E, 0x399C, 0xaaaa, 0xf0f0, 0x5a5a, 0x33cc, 0x3c3c, 0x55aa, 0x9696, 0xa55a, 0x73ce, 0x13c8, 0x324c, 0x3bdc, 0x6996, 0xc33c, 0x9966, 0x0660, 0x0272, 0x04e4, 0x4e40, 0x2720, 0xc936, 0x936c, 0x39c6, 0x639c, 0x9336, 0x9cc6, 0x817e, 0xe718, 0xccf0, 0x0fcc, 0x7744, 0xee22 };
  static readonly string[] P3 = {
    "0011001102210222","0001001122112221","0000200122112211","0222002200110111","0000000011221122","0011001100220022","0022002211111111","0011001122112211",
    "0000000011112222","0000111111112222","0000111122222222","0012001200120012","0112011201120112","0122012201220122","0011011211221222","0011200122002220",
    "0001001101121122","0111001120012200","0000112211221122","0022002200221111","0111011102220222","0001000122212221","0000001101220122","0000110022102210",
    "0122012200110000","0012001211222222","0110122112210110","0000011012211221","0022110211020022","0110011020022222","0011012201220011","0000200022112221",
    "0000000211221222","0222002200120011","0011001200220222","0120012001200120","0000111122220000","0120120120120120","0120201212010120","0011220011220011",
    "0011112222000011","0101010122222222","0000000021212121","0022112200221122","0022001100220011","0220122102201221","0101222222220101","0000212121212121",
    "0101010101012222","0222011102220111","0002111200021112","0000211221122112","0222011101110222","0002111211121112","0110011001100222","0000000021122112",
    "0110011022222222","0022001100110022","0022112211220022","0000000000002112","0002000100020001","0222122202221222","0101222222222222","0111201122012220" };
  static readonly int[] A2 = { 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 2, 8, 2, 2, 8, 8, 15, 2, 8, 2, 2, 8, 8, 2, 2, 15, 15, 6, 8, 2, 8, 15, 15, 2, 8, 2, 2, 2, 15, 15, 6, 6, 2, 6, 8, 15, 15, 2, 2, 15, 15, 15, 15, 15, 2, 2, 15 };
  static readonly int[] A3a = { 3, 3, 15, 15, 8, 3, 15, 15, 8, 8, 6, 6, 6, 5, 3, 3, 3, 3, 8, 15, 3, 3, 6, 10, 5, 8, 8, 6, 8, 5, 15, 15, 8, 15, 3, 5, 6, 10, 8, 15, 15, 3, 15, 5, 15, 15, 15, 15, 3, 15, 5, 5, 5, 8, 5, 10, 5, 10, 8, 13, 15, 12, 3, 3 };
  static readonly int[] A3b = { 15, 8, 8, 3, 15, 15, 3, 8, 15, 15, 15, 15, 15, 15, 15, 8, 15, 8, 15, 3, 15, 8, 15, 8, 3, 15, 6, 10, 15, 15, 10, 8, 15, 3, 15, 10, 10, 8, 9, 10, 6, 15, 8, 15, 3, 6, 6, 8, 15, 3, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 3, 15, 15, 8 };
  static readonly int[] W2 = { 0, 21, 43, 64 }, W3 = { 0, 9, 18, 27, 37, 46, 55, 64 }, W4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };
  sealed class Bits { readonly byte[] b; readonly int o; int p; public Bits(byte[] b, int o) { this.b = b; this.o = o; } public int Get(int n) { int v = 0; for (int i = 0; i < n; i++) { int q = p + i; v |= ((b[o + (q >> 3)] >> (q & 7)) & 1) << i; } p += n; return v; } }
  static int[] Wt(int n) => n == 2 ? W2 : n == 3 ? W3 : W4;
  static int Interp(int a, int b, int w) => (a * (64 - w) + b * w + 32) >> 6;
  public static void Decode(byte[] s, int off, int w, int h, byte[] d) {
    int bw = (w + 3) / 4, bh = (h + 3) / 4;
    for (int by = 0; by < bh; by++) for (int bx = 0; bx < bw; bx++) {
      var px = Block(s, off + (by * bw + bx) * 16);
      for (int t = 0; t < 16; t++) { int x = bx * 4 + (t & 3), y = by * 4 + (t >> 2); if (x >= w || y >= h) continue; int q = (y * w + x) * 4; for (int k = 0; k < 4; k++) d[q + k] = px[t * 4 + k]; }
    }
  }
  static byte[] Block(byte[] s, int o) {
    var r = new byte[64]; var bt = new Bits(s, o); int mode = 0; while (mode < 8 && bt.Get(1) == 0) mode++;
    if (mode >= 8) return r;
    int ns = NS[mode]; int part = bt.Get(PB[mode]); int rot = bt.Get(RB[mode]); int isb = bt.Get(ISB[mode]);
    int cb = CB[mode], ab = AB[mode]; var ep = new int[ns * 2, 4];
    for (int k = 0; k < 3; k++) for (int i = 0; i < ns * 2; i++) ep[i, k] = bt.Get(cb);
    for (int i = 0; i < ns * 2; i++) ep[i, 3] = ab > 0 ? bt.Get(ab) : 255;
    if (EPB[mode] == 1) { for (int i = 0; i < ns * 2; i++) { int pbit = bt.Get(1); for (int k = 0; k < 4; k++) { if (k == 3 && ab == 0) continue; ep[i, k] = (ep[i, k] << 1) | pbit; } } }
    else if (SPB[mode] == 1) { for (int j = 0; j < ns; j++) { int pbit = bt.Get(1); for (int i = j * 2; i < j * 2 + 2; i++) for (int k = 0; k < 4; k++) { if (k == 3 && ab == 0) continue; ep[i, k] = (ep[i, k] << 1) | pbit; } } }
    int cbits = cb + (EPB[mode] | SPB[mode]), abits = ab > 0 ? ab + (EPB[mode] | SPB[mode]) : 0;
    for (int i = 0; i < ns * 2; i++) { ep[i, 0] = Ex(ep[i, 0], cbits); ep[i, 1] = Ex(ep[i, 1], cbits); ep[i, 2] = Ex(ep[i, 2], cbits); ep[i, 3] = ab > 0 ? Ex(ep[i, 3], abits) : 255; }
    int Sub(int t) => ns == 1 ? 0 : ns == 2 ? (int)((P2[part] >> t) & 1) : P3[part][t] - '0';
    bool Anchor(int t) { if (t == 0) return true; if (ns == 2) return t == A2[part]; if (ns == 3) return t == A3a[part] || t == A3b[part]; return false; }
    int ib = IB[mode], ib2 = IB2[mode]; var i1 = new int[16]; var i2 = new int[16];
    for (int t = 0; t < 16; t++) i1[t] = bt.Get(Anchor(t) ? ib - 1 : ib);
    if (ib2 > 0) for (int t = 0; t < 16; t++) i2[t] = bt.Get(t == 0 ? ib2 - 1 : ib2);
    for (int t = 0; t < 16; t++) {
      int sIdx = Sub(t); int e0 = sIdx * 2, e1 = e0 + 1; int cr, cg, cbb, ca;
      if (ib2 == 0) { var wt = Wt(ib)[i1[t]]; cr = Interp(ep[e0, 0], ep[e1, 0], wt); cg = Interp(ep[e0, 1], ep[e1, 1], wt); cbb = Interp(ep[e0, 2], ep[e1, 2], wt); ca = Interp(ep[e0, 3], ep[e1, 3], wt); }
      else {
        int cw = isb == 0 ? Wt(ib)[i1[t]] : Wt(ib2)[i2[t]], aw = isb == 0 ? Wt(ib2)[i2[t]] : Wt(ib)[i1[t]];
        cr = Interp(ep[e0, 0], ep[e1, 0], cw); cg = Interp(ep[e0, 1], ep[e1, 1], cw); cbb = Interp(ep[e0, 2], ep[e1, 2], cw); ca = Interp(ep[e0, 3], ep[e1, 3], aw);
      }
      if (rot == 1) (ca, cr) = (cr, ca); else if (rot == 2) (ca, cg) = (cg, ca); else if (rot == 3) (ca, cbb) = (cbb, ca);
      r[t * 4] = (byte)cr; r[t * 4 + 1] = (byte)cg; r[t * 4 + 2] = (byte)cbb; r[t * 4 + 3] = (byte)ca;
    }
    return r;
  }
  static int Ex(int v, int bits) { v <<= 8 - bits; return v | (v >> bits); }
}

// ------------------------------------------------------------------------------------------------ materials / scene
sealed class CMat {
  public string Name, Shader = "lit", Mode = "opaque", TexName = "-", Note = "", TexFmt = "";
  public CTex Tex, EmTex, MaskTex; public double[] Tile = { 1, 1 }, Toff = { 0, 0 }, EmTile = { 1, 1 }, EmOff = { 0, 0 };
  public double[] Tint = { 1, 1, 1 }; public double TintA = 1; public double[] Emit; // linear
  public bool Amask, AlphaTest, Lit = true, Hide, Premul; public double Cut = 0.5; public int Cull = 2;
  public double Smooth = 0.5, Metal = 0; public bool NoShadow, NoSpecHi, NoEnv; public bool SpecSetup; public double[] SpecCol = { 0.2, 0.2, 0.2 }; public CTex SpecTex;
  public bool Terrain; public List<(CTex t, double[] tile, double[] remap)> Layers; public CTex Control; public double[] TerrOrg, TerrSize;
  public int Uses;
}

static class CRScene {
  // geometry (cache frame, world space)
  public static float[] P, Nn, UV, C; public static int NV; public static int[] I, TM, TR; public static float[] TK; public static int NT; public static bool[] TCast;
  public static List<CMat> Mats = new(); public static List<string> RendNames = new();
  public static double[] AmbSky, AmbEq, AmbGround; public static double AmbInt = 1; public static int AmbMode; public static double[][] AmbTab; public static double[] ProbeSh; public static bool UseProbe;
  public static double[] SunCol; public static V3 SunDir; public static double SunStrength = 1; public static bool HasSun;
  public sealed class PL { public V3 p, fwd; public double[] col; public double range; public int type; public double spotIn = -2, spotOut = -2; public string name; }
  public static List<PL> Lights = new();
  public static double[] FogCol; public static double FogStart = 1e9, FogEnd = 1e9, FogDens; public static int FogMode; public static bool Fog;
  public static CTex[] SkyFaces; public static double SkyExp = 1; public static double[] SkyTint = { 1, 1, 1 }; public static bool SkyRgbm; public static string SkyNote = "none";
  public static CTex[] EnvFaces, SkyEnv; public static bool EnvRgbm; public static string EnvNote = "none";
  public static int TexMax = 1024; public static Regex SkipMat; public static bool Shafts = false;

  // shader PPtr ids of the Rec Room Studio shader bundle (not in the cache): the install's stock shaders (iter6 notes)
  static readonly Dictionary<long, string> ShaderIds = new() {
    [-4055751637662985151] = "lit", [-261421426886131751] = "unlit", [-3885793140065008626] = "terrain", [683148379888368288] = "tmp", [7030032868776217633] = "tmp",
    [8918850207803687531] = "particle", [-6364362845305036051] = "tree" };

  // ---------------------------------------------------------------- textures
  static readonly Dictionary<(SFile, long), CTex> texCache = new();
  public static CTex LoadTex(SFile from, object pptr, out string info) {
    info = ""; var (tf, to) = Rc.Ptr(from, pptr); if (to == null) { info = "none"; return null; }
    if (to.ClassId != 28) { info = "class " + to.ClassId; return null; }
    if (texCache.TryGetValue((tf, to.PathId), out var tc)) { info = tc?.Name ?? "?"; return tc; }
    CTex t = null;
    try {
      var d = Rc.Read(tf, to); int w = (int)Rc.N(d, "m_Width"), h = (int)Rc.N(d, "m_Height"), fmt = (int)Rc.N(d, "m_TextureFormat"), mips = Math.Max(1, (int)Rc.N(d, "m_MipCount"));
      info = $"{Rc.S(d, "m_Name")} fmt {fmt} {w}x{h}";
      var b = TexDec.Bytes(d);
      if (b != null && TexDec.Known(fmt)) {
        int off = 0, m = 0, mw = w, mh = h;
        while (m + 1 < mips && Math.Max(mw, mh) > TexMax) { int sz = TexDec.MipBytes(fmt, mw, mh); if (off + sz > b.Length) break; off += sz; mw = Math.Max(1, mw / 2); mh = Math.Max(1, mh / 2); m++; }
        if (off + TexDec.MipBytes(fmt, mw, mh) <= b.Length) {
          var px = TexDec.Rgba(fmt, b, off, mw, mh);
          if (px != null) { t = new CTex { Name = Rc.S(d, "m_Name"), HasAlpha = fmt == 12 || fmt == 4 || fmt == 5 || fmt == 14 || fmt == 1 || fmt == 25 || fmt == 17 }; t.Linear = Rc.N(d, "m_ColorSpace") == 0; t.Build(px, mw, mh); }
        } else info += " (short data)";
      } else info += " (format not decoded)";
    } catch (Exception e) { info += " " + e.Message; t = null; }
    texCache[(tf, to.PathId)] = t; return t;
  }
  /// a cubemap (class 89) as 6 faces (+x -x +y -y +z -z), at the first mip <= maxSize
  static CTex[] LoadCube(SFile from, object pptr, int maxSize, out string info) {
    info = ""; var (cf, co) = Rc.Ptr(from, pptr); if (co == null) { info = "no cubemap"; return null; }
    if (co.ClassId != 89) { info = "class " + co.ClassId; return null; }
    var d = Rc.Read(cf, co); int w = (int)Rc.N(d, "m_Width"), fmt = (int)Rc.N(d, "m_TextureFormat"), mips = Math.Max(1, (int)Rc.N(d, "m_MipCount")); long per = (long)Rc.N(d, "m_CompleteImageSize");
    info = $"{Rc.S(d, "m_Name")} {w}x{w} fmt {fmt} mips {mips}";
    var b = TexDec.Bytes(d); bool hdr = fmt == 24 || fmt == 17; if (b == null || (!TexDec.Known(fmt) && fmt != 24)) { info += " (not decoded)"; return null; }
    if (per <= 0) per = b.Length / 6;
    int off = 0, size = w, m = 0; while (m + 1 < mips && size > maxSize) { off += TexDec.MipBytes(fmt, size, size); size = Math.Max(1, size / 2); m++; }
    var faces = new CTex[6];
    for (int fc = 0; fc < 6; fc++) {
      faces[fc] = new CTex { Name = "face" + fc, W = size, H = size };
      if (fmt == 24) { faces[fc].Hdr = new float[size * size * 3]; Bc6.Decode(b, (int)(fc * per) + off, size, size, faces[fc].Hdr); }
      else if (fmt == 17) { var hf = new float[size * size * 3]; int o0 = (int)(fc * per) + off; for (int i = 0; i < size * size; i++) for (int k = 0; k < 3; k++) hf[i * 3 + k] = (float)BitConverter.ToHalf(b, o0 + i * 8 + k * 2); faces[fc].Hdr = hf; }
      else { var px = TexDec.Rgba(fmt, b, (int)(fc * per) + off, size, size); if (px == null) return null; faces[fc].Mips.Add(px); faces[fc].Size.Add((size, size)); }
    }
    info += $" -> {size}x{size}";
    return faces;
  }

  // ---------------------------------------------------------------- materials
  static readonly Dictionary<(SFile, long), int> matIdx = new();
  static int MatOf(SFile from, object pptr) {
    var (mf, mo) = Rc.Ptr(from, pptr); if (mo == null || mo.ClassId != 21) return -1;
    if (matIdx.TryGetValue((mf, mo.PathId), out var ix)) { Mats[ix].Uses++; return ix; }
    var d = Rc.Read(mf, mo); var m = new CMat { Name = Rc.S(d, "m_Name") ?? "?", Uses = 1 };
    try {
      var shp = Rc.D(d, "m_Shader"); long spid = shp == null ? 0 : Convert.ToInt64(shp["m_PathID"]);
      m.Shader = ShaderIds.GetValueOrDefault(spid) ?? ("local:" + (Rc.Ptr(mf, shp).o is SObj so ? Raw.Name(Rc.Ptr(mf, shp).f, so) : spid.ToString()));
      var sp = Rc.D(d, "m_SavedProperties");
      var cols = new Dictionary<string, double[]>(); foreach (var x in Rc.L(sp, "m_Colors") ?? new()) cols[Rc.S(x, "first") ?? ""] = Rc.Col((x as Dictionary<string, object>)?["second"]);
      var fl = new Dictionary<string, double>(); foreach (var x in Rc.L(sp, "m_Floats") ?? new()) fl[Rc.S(x, "first") ?? ""] = Rc.N(x, "second");
      var texs = new Dictionary<string, Dictionary<string, object>>(); foreach (var x in Rc.L(sp, "m_TexEnvs") ?? new()) texs[Rc.S(x, "first") ?? ""] = Rc.D(x, "second");
      string kw = d.GetValueOrDefault("m_ValidKeywords") is List<object> vk ? string.Join(" ", vk) : ""; kw += " " + (d.GetValueOrDefault("m_ShaderKeywords") as string ?? "");
      int queue = (int)Rc.N(d, "m_CustomRenderQueue");
      var col = cols.GetValueOrDefault("_BaseColor") ?? cols.GetValueOrDefault("_Color") ?? new double[] { 1, 1, 1, 1 };
      m.Tint = CM.LinC(col); m.TintA = col[3];
      var mt = texs.GetValueOrDefault("_BaseMap"); if (mt == null || Rc.Ptr(mf, mt.GetValueOrDefault("m_Texture")).o == null) mt = texs.GetValueOrDefault("_MainTex");
      if (mt != null && Rc.Ptr(mf, mt.GetValueOrDefault("m_Texture")).o != null) {
        m.Tex = LoadTex(mf, mt["m_Texture"], out var info); m.TexName = info; var sc = Rc.D(mt, "m_Scale"); var of = Rc.D(mt, "m_Offset");
        m.Tile = new[] { Rc.N(sc, "x"), Rc.N(sc, "y") }; m.Toff = new[] { Rc.N(of, "x"), Rc.N(of, "y") };
      }
      m.Cull = fl.TryGetValue("_Cull", out var cu) ? (int)cu : 2;
      bool transparent = fl.GetValueOrDefault("_Surface") == 1 || kw.Contains("_SURFACE_TYPE_TRANSPARENT") || queue >= 2900;
      int blend = (int)fl.GetValueOrDefault("_Blend");
      if (transparent) { m.Mode = blend == 2 ? "add" : "fade"; m.Premul = blend == 1; }
      if (fl.GetValueOrDefault("_AlphaClip") > 0 || kw.Contains("_ALPHATEST_ON") || m.Shader == "tree") { m.AlphaTest = true; m.Cut = fl.TryGetValue("_Cutoff", out var co) ? co : 0.5; if (m.Mode == "opaque") m.Mode = "cut"; }
      if (m.Shader == "particle") { m.Lit = false; m.Mode = kw.Contains("_COLORADDSUBDIFF_ON") || blend == 2 ? "add" : "fade"; }
      if (m.Shader == "unlit") m.Lit = false;
      if (m.Shader == "tmp") m.Hide = true;
      m.Smooth = fl.TryGetValue("_Smoothness", out var smo) ? smo : fl.GetValueOrDefault("_Glossiness", 0.5); m.Metal = fl.GetValueOrDefault("_Metallic");
      // URP Lit specular workflow: _SpecColor (or _SpecGlossMap.rgb with _METALLICSPECGLOSSMAP), smoothness = _Smoothness (x map alpha)
      m.SpecSetup = kw.Contains("_SPECULAR_SETUP") || fl.GetValueOrDefault("_WorkflowMode", 1) == 0;
      if (m.SpecSetup) {
        var scol = cols.GetValueOrDefault("_SpecColor"); if (scol != null) m.SpecCol = CM.LinC(scol);
        var sg = texs.GetValueOrDefault("_SpecGlossMap");
        if (kw.Contains("_METALLICSPECGLOSSMAP") && sg != null && Rc.Ptr(mf, sg.GetValueOrDefault("m_Texture")).o != null) m.SpecTex = LoadTex(mf, sg["m_Texture"], out _);
      }
      m.NoShadow = kw.Contains("_RECEIVE_SHADOWS_OFF") || fl.GetValueOrDefault("_ReceiveShadows", 1) == 0;
      m.NoSpecHi = kw.Contains("_SPECULARHIGHLIGHTS_OFF") || fl.GetValueOrDefault("_SpecularHighlights", 1) == 0;
      m.NoEnv = kw.Contains("_ENVIRONMENTREFLECTIONS_OFF") || fl.GetValueOrDefault("_EnvironmentReflections", 1) == 0;
      // the AG alpha-masked tint (opaque, texture with alpha): tex.rgb * lerp(1, vc * tint, tex.a)
      m.Amask = m.Mode == "opaque" && m.Tex != null && m.Tex.HasAlpha;
      bool emissive = kw.Contains("_EMISSION");
      var ec = cols.GetValueOrDefault("_EmissionColor");
      if (emissive && ec != null && ec[0] + ec[1] + ec[2] > 0.001) {
        m.Emit = CM.LinC(ec);
        var et = texs.GetValueOrDefault("_EmissionMap");
        if (et != null && Rc.Ptr(mf, et.GetValueOrDefault("m_Texture")).o != null) { m.EmTex = LoadTex(mf, et["m_Texture"], out _); var sc = Rc.D(et, "m_Scale"); var of = Rc.D(et, "m_Offset"); m.EmTile = new[] { Rc.N(sc, "x"), Rc.N(sc, "y") }; m.EmOff = new[] { Rc.N(of, "x"), Rc.N(of, "y") }; }
      }
      m.Note = $"kw[{kw.Trim()}] q{queue} surf{fl.GetValueOrDefault("_Surface")} blend{blend} smooth {Y.F3(m.Smooth)} metal {Y.F3(m.Metal)}";
    } catch (Exception e) { m.Note += " error " + e.Message; m.Tint = new[] { 0.3, 0.3, 0.3 }; }
    Mats.Add(m); matIdx[(mf, mo.PathId)] = Mats.Count - 1; return Mats.Count - 1;
  }

  // ---------------------------------------------------------------- meshes (with UVs)
  sealed class MG { public int N; public float[] P, Nm, UV, C; public List<int[]> Subs = new(); public string Name; }
  static readonly Dictionary<(SFile, long), MG> geoCache = new();
  static int FmtSize(int f) => f switch { 0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 2, 5 => 2, 6 => 1, 7 => 1, 8 => 2, 9 => 2, 10 => 4, 11 => 4, _ => 4 };
  static MG Decode(SFile f, SObj o) {
    if (geoCache.TryGetValue((f, o.PathId), out var gc)) return gc;
    MG g = null;
    try {
      var d = Rc.Read(f, o); var vd = Rc.D(d, "m_VertexData"); int n = (int)Rc.N(vd, "m_VertexCount");
      var chans = Rc.L(vd, "m_Channels").Select(x => (s: (int)Rc.N(x, "stream"), o: (int)Rc.N(x, "offset"), fmt: (int)Rc.N(x, "format"), dim: (int)Rc.N(x, "dimension") & 15)).ToList();
      byte[] data = vd.GetValueOrDefault("m_DataSize") as byte[];
      if (data == null || data.Length == 0) { var sd = Rc.D(d, "m_StreamData"); long size = (long)Rc.N(sd, "size"); if (size > 0) data = Rc.Stream(Rc.S(sd, "path"), (long)Rc.N(sd, "offset"), size); }
      if (data != null && n > 0 && chans.Count > 0 && chans[0].dim >= 3) {
        int maxS = chans.Where(x => x.dim > 0).Max(x => x.s);
        var sOff = new int[maxS + 1]; var sStride = new int[maxS + 1]; int at = 0;
        for (int s = 0; s <= maxS; s++) { sOff[s] = at; sStride[s] = chans.Where(x => x.s == s && x.dim > 0).Select(x => x.o + FmtSize(x.fmt) * x.dim).DefaultIfEmpty(0).Max(); at += sStride[s] * n; at = (at + 15) & ~15; }
        double Rd(int fm, int p) => fm switch { 0 => BitConverter.ToSingle(data, p), 1 => (double)BitConverter.ToHalf(data, p), 2 => data[p] / 255.0, 3 => Math.Max(-1, (sbyte)data[p] / 127.0), 4 => BitConverter.ToUInt16(data, p) / 65535.0, 5 => Math.Max(-1, BitConverter.ToInt16(data, p) / 32767.0), 6 => data[p], 7 => (sbyte)data[p], 8 => BitConverter.ToUInt16(data, p), 9 => BitConverter.ToInt16(data, p), 10 => BitConverter.ToUInt32(data, p), 11 => BitConverter.ToInt32(data, p), _ => 0 };
        float[] Chan(int ci, int want) {
          if (ci >= chans.Count || chans[ci].dim == 0) return null; var c = chans[ci]; var r = new float[n * want]; int fs = FmtSize(c.fmt);
          for (int i = 0; i < n; i++) { int b = sOff[c.s] + i * sStride[c.s] + c.o; for (int k = 0; k < want; k++) r[i * want + k] = k < c.dim ? (float)Rd(c.fmt, b + k * fs) : (k == 3 ? 1f : 0f); }
          return r;
        }
        g = new MG { N = n, P = Chan(0, 3), Nm = Chan(1, 3), C = Chan(3, 4), UV = Chan(4, 2), Name = Rc.S(d, "m_Name") };
        var ib = d.GetValueOrDefault("m_IndexBuffer") as byte[]; bool i32 = Rc.N(d, "m_IndexFormat") == 1;
        foreach (var sx in Rc.L(d, "m_SubMeshes")) {
          var tl = new List<int>();
          if (Rc.N(sx, "topology") == 0 && ib != null) {
            int fb = (int)Rc.N(sx, "firstByte"), ic = (int)Rc.N(sx, "indexCount"), bv = (int)Rc.N(sx, "baseVertex");
            for (int k = 0; k < ic; k++) { int p = fb + k * (i32 ? 4 : 2); if (p + (i32 ? 4 : 2) > ib.Length) break; tl.Add((i32 ? BitConverter.ToInt32(ib, p) : BitConverter.ToUInt16(ib, p)) + bv); }
          }
          g.Subs.Add(tl.ToArray());
        }
      }
    } catch (Exception e) { g = null; Console.Error.WriteLine("mesh " + o.PathId + ": " + e.Message); }
    geoCache[(f, o.PathId)] = g; return g;
  }
  static MG FromBuiltin(SFile from, Dictionary<string, object> pptr) {
    var mg = MeshGeo.Builtin(from, pptr); if (mg == null || mg.V == null) return null;
    var g = new MG { N = mg.V.Length, P = new float[mg.V.Length * 3], UV = new float[mg.V.Length * 2], Name = mg.Name };
    for (int i = 0; i < mg.V.Length; i++) { g.P[i * 3] = (float)mg.V[i].x; g.P[i * 3 + 1] = (float)mg.V[i].y; g.P[i * 3 + 2] = (float)mg.V[i].z; int q = i & 3; g.UV[i * 2] = q == 1 || q == 2 ? 1 : 0; g.UV[i * 2 + 1] = q >= 2 ? 1 : 0; }
    g.Subs.AddRange(mg.Subs); return g;
  }

  // ---------------------------------------------------------------- build
  static List<float> lp = new(), ln = new(), luv = new(), lc = new(); static List<int> li = new(), ltm = new(), ltr = new(); static List<bool> lcast = new();
  static int AddVert(V3 p, V3 n, double u, double v, double r, double g, double b, double a) {
    lp.Add((float)p.x); lp.Add((float)p.y); lp.Add((float)p.z); ln.Add((float)n.x); ln.Add((float)n.y); ln.Add((float)n.z);
    luv.Add((float)u); luv.Add((float)v); lc.Add((float)r); lc.Add((float)g); lc.Add((float)b); lc.Add((float)a); return lp.Count / 3 - 1;
  }
  static readonly Dictionary<(SFile, long), int> batchBase = new();
  static readonly Regex ShaftRe = new(@"LightShaft|Cloud_Locker", RegexOptions.IgnoreCase);
  public static void Load(int texMax, bool verbose) {
    TexMax = texMax; var f = Rc.Scene; var nodes = RcConvert.NodeList(); var lod = CRNodes.LodSkip();
    int rN = 0, off = 0, lodN = 0, noMesh = 0, drawn = 0, batchedN = 0, skinned = 0, flipped = 0, hidden = 0, tris = 0, shafts = 0;
    foreach (var n in nodes) {
      var rend = n.Comp(23) ?? n.Comp(137); if (rend == null) continue; rN++;
      var rd = Rc.Read(f, rend);
      if (!n.Active || Rc.N(rd, "m_Enabled") == 0) { off++; continue; }
      if (lod.Contains(rend.PathId)) { lodN++; continue; }
      object mref; if (rend.ClassId == 137) { mref = rd.GetValueOrDefault("m_Mesh"); skinned++; } else { var filt = n.Comp(33); if (filt == null) { noMesh++; continue; } mref = Rc.Read(f, filt).GetValueOrDefault("m_Mesh"); }
      var (mf, mo) = Rc.Ptr(f, mref); MG g = mo != null ? Decode(mf, mo) : FromBuiltin(f, mref as Dictionary<string, object>);
      if (g == null || g.P == null) { noMesh++; continue; }
      var mptr = Rc.L(rd, "m_Materials") ?? new();
      bool cast = Rc.N(rd, "m_CastShadows") != 0;
      var sbi = Rc.D(rd, "m_StaticBatchInfo"); int first = (int)Rc.N(sbi, "firstSubMesh"), count = (int)Rc.N(sbi, "subMeshCount");
      bool batched = count > 0 && (g.Name ?? "").StartsWith("Combined Mesh"); if (batched) batchedN++;
      int s0 = batched ? first : 0, sc = batched ? count : g.Subs.Count;
      bool mirror = !batched && CM.Det(n.M) < 0; var nm = CM.NormalMat(n.M);
      int baseV = batched && batchBase.TryGetValue((mf, mo.PathId), out var bb0) ? bb0 : -1; bool any = false;
      for (int k = 0; k < sc && k < mptr.Count; k++) {
        int si = s0 + k; if (si >= g.Subs.Count) break;
        int mi = MatOf(f, mptr[k]); if (mi < 0) continue; var mat = Mats[mi];
        if (mat.Hide) { hidden++; continue; }
        if (ShaftRe.IsMatch(mat.Name) && !Shafts) { shafts++; continue; }
        var tl = g.Subs[si]; if (tl.Length < 3) continue;
        if (baseV < 0) {
          baseV = lp.Count / 3; if (batched) batchBase[(mf, mo.PathId)] = baseV;
          for (int i = 0; i < g.N; i++) {
            var v = new V3(g.P[i * 3], g.P[i * 3 + 1], g.P[i * 3 + 2]); var wp = batched ? v : CM.P(n.M, v);
            V3 wn = new(0, 1, 0); if (g.Nm != null) { var nl = new V3(g.Nm[i * 3], g.Nm[i * 3 + 1], g.Nm[i * 3 + 2]); wn = CM.Nrm(batched ? nl : CM.N3(nm, nl)); }
            double u = g.UV != null ? g.UV[i * 2] : 0, vv = g.UV != null ? g.UV[i * 2 + 1] : 0;
            double cr = 1, cg = 1, cb = 1, ca = 1; if (g.C != null) { cr = g.C[i * 4]; cg = g.C[i * 4 + 1]; cb = g.C[i * 4 + 2]; ca = g.C[i * 4 + 3]; }
            AddVert(wp, wn, u, vv, cr, cg, cb, ca);
          }
        }
        // static batches keep the source winding; when most triangles disagree with their vertex normals, turn them
        bool flip = mirror;
        if (batched && g.Nm != null) {
          int agree = 0, against = 0;
          for (int t = 0; t + 2 < tl.Length; t += 3) {
            int a = tl[t], b = tl[t + 1], c = tl[t + 2]; var pa = new V3(g.P[a * 3], g.P[a * 3 + 1], g.P[a * 3 + 2]);
            var cr = CM.Cross(new V3(g.P[b * 3], g.P[b * 3 + 1], g.P[b * 3 + 2]) - pa, new V3(g.P[c * 3], g.P[c * 3 + 1], g.P[c * 3 + 2]) - pa);
            var nn = new V3(g.Nm[a * 3] + g.Nm[b * 3] + g.Nm[c * 3], g.Nm[a * 3 + 1] + g.Nm[b * 3 + 1] + g.Nm[c * 3 + 1], g.Nm[a * 3 + 2] + g.Nm[b * 3 + 2] + g.Nm[c * 3 + 2]);
            double dd = CM.Dot(cr, nn); if (dd > 0) agree++; else if (dd < 0) against++;
          }
          if (against > agree) { flip = true; flipped++; }
        }
        for (int t = 0; t + 2 < tl.Length; t += 3) {
          int a = tl[t], b = tl[t + 1], c = tl[t + 2]; if (a >= g.N || b >= g.N || c >= g.N) continue;
          if (flip) { li.Add(baseV + a); li.Add(baseV + c); li.Add(baseV + b); } else { li.Add(baseV + a); li.Add(baseV + b); li.Add(baseV + c); }
          ltm.Add(mi); ltr.Add(RendNames.Count); lcast.Add(cast && mat.Mode != "fade" && mat.Mode != "add"); tris++;
        }
        any = true;
      }
      if (any) drawn++;
      RendNames.Add((g.Name ?? "?") + " @ " + n.Path);
    }
    BuildTerrain(nodes);
    while (ltr.Count < ltm.Count) ltr.Add(int.MaxValue);
    TR = ltr.ToArray(); P = lp.ToArray(); Nn = ln.ToArray(); UV = luv.ToArray(); C = lc.ToArray(); I = li.ToArray(); TM = ltm.ToArray(); TCast = lcast.ToArray(); NV = P.Length / 3; NT = TM.Length;
    lp = ln = luv = lc = null; li = ltm = null; lcast = null;
    TK = new float[NT];
    for (int t = 0; t < NT; t++) {
      var mat = Mats[TM[t]]; if (mat.Tex == null && mat.EmTex == null) continue; int a = I[t * 3], b = I[t * 3 + 1], c = I[t * 3 + 2];
      var pa = new V3(P[a * 3], P[a * 3 + 1], P[a * 3 + 2]); double wa = CM.Cross(new V3(P[b * 3], P[b * 3 + 1], P[b * 3 + 2]) - pa, new V3(P[c * 3], P[c * 3 + 1], P[c * 3 + 2]) - pa).Len();
      double ua = Math.Abs((UV[b * 2] - UV[a * 2]) * (UV[c * 2 + 1] - UV[a * 2 + 1]) - (UV[c * 2] - UV[a * 2]) * (UV[b * 2 + 1] - UV[a * 2 + 1])) * Math.Abs(mat.Tile[0] * mat.Tile[1]);
      TK[t] = wa > 1e-12 ? (float)Math.Sqrt(ua / wa) : 0;
    }
    Console.WriteLine($"renderers {rN}: drawn {drawn} ({batchedN} static-batched, {skinned} skinned), off {off}, LOD1+ {lodN}, no mesh {noMesh}, TMP submeshes {hidden}, light-shaft submeshes left out {shafts}, batches turned {flipped}; {NT} triangles, {NV} vertices; materials {Mats.Count} (with texture {Mats.Count(x => x.Tex != null)})");
    SetupEnv(nodes, verbose);
  }

  // ---------------------------------------------------------------- terrain
  static void BuildTerrain(List<Node> nodes) {
    var f = Rc.Scene;
    foreach (var n in nodes.Where(x => x.Active && x.Comp(218) != null)) {
      try {
        var tcd = Rc.Read(f, n.Comp(218)); if (Rc.N(tcd, "m_Enabled") == 0) continue;
        var (df, dobj) = Rc.Ptr(f, tcd["m_TerrainData"]); var dd = Rc.Read(df, dobj); if (dd == null) continue;
        var hm = Rc.D(dd, "m_Heightmap"); int R = (int)Rc.N(hm, "m_Resolution"); var sc = Rc.V(hm["m_Scale"]); var hs = Rc.L(hm, "m_Heights"); if (hs == null || R < 2) continue;
        var H = hs.Select(x => Convert.ToDouble(x, Rc.IC)).ToArray();
        var rm = new CMat { Name = "terrain:" + Rc.S(dd, "m_Name"), Shader = "terrain", Terrain = true, Layers = new(), Uses = 1, Cull = 2, TerrOrg = new[] { n.M[3], n.M[11] }, TerrSize = new[] { sc.x * (R - 1), sc.z * (R - 1) }, Smooth = 0 };
        var spl = Rc.D(dd, "m_SplatDatabase");
        foreach (var lptr in Rc.L(spl, "m_TerrainLayers") ?? new()) {
          var (lf, lo) = Rc.Ptr(df, lptr); var ld = Rc.Read(lf, lo); if (ld == null) continue;
          var tx = LoadTex(lf, ld["m_DiffuseTexture"], out _); var ts = Rc.D(ld, "m_TileSize"); var tile = new[] { Math.Max(0.01, Rc.N(ts, "x")), Math.Max(0.01, Rc.N(ts, "y")) };
          var rmin = Rc.D(ld, "m_DiffuseRemapMin"); var rmax = Rc.D(ld, "m_DiffuseRemapMax"); var remap = rmax == null ? new double[] { 1, 1, 1 } : new[] { Rc.N(rmax, "x") - Rc.N(rmin, "x"), Rc.N(rmax, "y") - Rc.N(rmin, "y"), Rc.N(rmax, "z") - Rc.N(rmin, "z") };
          if (remap.Any(v => v <= 0 || v > 8)) remap = new double[] { 1, 1, 1 };
          rm.Layers.Add((tx, tile, remap));
        }
        var at = Rc.L(spl, "m_AlphaTextures"); if (at != null && at.Count > 0) { rm.Control = LoadTex(df, at[0], out _); if (rm.Control != null) rm.Control.Linear = true; }
        Mats.Add(rm); int mi = Mats.Count - 1; int baseV = lp.Count / 3;
        double Hh(int x, int z) => H[Math.Clamp(z, 0, R - 1) * R + Math.Clamp(x, 0, R - 1)] / 32766.0 * sc.y;
        for (int z = 0; z < R; z++) for (int x = 0; x < R; x++) {
          var p = new V3(n.M[3] + x * sc.x, n.M[7] + Hh(x, z), n.M[11] + z * sc.z);
          var nn = CM.Nrm(new V3((Hh(x - 1, z) - Hh(x + 1, z)) / (2 * sc.x), 1, (Hh(x, z - 1) - Hh(x, z + 1)) / (2 * sc.z)));
          AddVert(p, nn, (double)x / (R - 1), (double)z / (R - 1), 1, 1, 1, 1);
        }
        for (int z = 0; z + 1 < R; z++) for (int x = 0; x + 1 < R; x++) {
          int a = baseV + z * R + x, b = a + 1, c = a + R, d = c + 1;
          li.Add(a); li.Add(c); li.Add(b); ltm.Add(mi); lcast.Add(true); li.Add(b); li.Add(c); li.Add(d); ltm.Add(mi); lcast.Add(true);
        }
        Console.WriteLine($"terrain {rm.Name}: {R}x{R} at {CM.F(n.M[3])},{CM.F(n.M[7])},{CM.F(n.M[11])} size {CM.F(rm.TerrSize[0])}x{CM.F(rm.TerrSize[1])}, layers {string.Join(",", rm.Layers.Select(l => (l.t?.Name ?? "?") + "/" + Y.F(l.tile[0])))}, control {(rm.Control != null ? rm.Control.W + "x" + rm.Control.H : "none")}");
      } catch (Exception e) { Console.WriteLine("terrain: " + e.Message); }
    }
  }

  // ---------------------------------------------------------------- environment
  static void SetupEnv(List<Node> nodes, bool verbose) {
    var f = Rc.Scene; var rso = f.Objects.FirstOrDefault(o => o.ClassId == 104); var rs = rso == null ? null : Rc.Read(f, rso);
    double[] C4(string k) => rs == null ? null : Rc.Col(rs.GetValueOrDefault(k));
    AmbSky = CM.LinC(C4("m_AmbientSkyColor") ?? new double[] { 0.5, 0.5, 0.5 }); AmbEq = CM.LinC(C4("m_AmbientEquatorColor") ?? new double[] { 0.4, 0.4, 0.4 }); AmbGround = CM.LinC(C4("m_AmbientGroundColor") ?? new double[] { 0.3, 0.3, 0.3 });
    AmbInt = rs == null ? 1 : Rc.N(rs, "m_AmbientIntensity"); AmbMode = rs == null ? 1 : (int)Rc.N(rs, "m_AmbientMode");
    Fog = rs != null && Rc.N(rs, "m_Fog") != 0; FogCol = CM.LinC(C4("m_FogColor") ?? new double[] { 1, 1, 1 }); FogMode = rs == null ? 1 : (int)Rc.N(rs, "m_FogMode");
    FogStart = Rc.N(rs, "m_LinearFogStart"); FogEnd = Rc.N(rs, "m_LinearFogEnd"); FogDens = Rc.N(rs, "m_FogDensity");
    Console.WriteLine($"RenderSettings: ambient mode {AmbMode} (1 = trilight) sky {string.Join(",", (C4("m_AmbientSkyColor") ?? new double[3]).Take(3).Select(Y.F3))} equator {string.Join(",", (C4("m_AmbientEquatorColor") ?? new double[3]).Take(3).Select(Y.F3))} ground {string.Join(",", (C4("m_AmbientGroundColor") ?? new double[3]).Take(3).Select(Y.F3))} (sRGB) x{Y.F3(AmbInt)}; fog {Fog} mode {FogMode} {Y.F3(FogStart)}-{Y.F3(FogEnd)} colour {string.Join(",", (C4("m_FogColor") ?? new double[3]).Take(3).Select(Y.F3))}");
    // trilight irradiance table by n.y
    AmbTab = new double[65][];
    {
      int NS = 64; var dirs = new List<(V3 d, double[] L)>();
      for (int i = 0; i < NS; i++) for (int j = 0; j < 2 * NS; j++) {
        double th = (i + 0.5) / NS * Math.PI, ph = (j + 0.5) / (2 * NS) * 2 * Math.PI; var d = new V3(Math.Sin(th) * Math.Cos(ph), Math.Cos(th), Math.Sin(th) * Math.Sin(ph));
        double t = d.y; var Lr = new double[3]; for (int k = 0; k < 3; k++) Lr[k] = (t > 0 ? AmbEq[k] + (AmbSky[k] - AmbEq[k]) * t : AmbEq[k] + (AmbGround[k] - AmbEq[k]) * -t) * Math.Sin(th);
        dirs.Add((d, Lr));
      }
      double dw = (Math.PI / NS) * (2 * Math.PI / (2 * NS));
      for (int s = 0; s <= 64; s++) {
        double ny = -1 + 2.0 * s / 64; var nrm = new V3(Math.Sqrt(Math.Max(0, 1 - ny * ny)), ny, 0); var acc = new double[3];
        foreach (var (d, Lr) in dirs) { double c = CM.Dot(nrm, d); if (c <= 0) continue; for (int k = 0; k < 3; k++) acc[k] += Lr[k] * c * dw; }
        AmbTab[s] = acc.Select(v => v / Math.PI * AmbInt).ToArray();
      }
      Console.WriteLine($"ambient irradiance (trilight integrated, linear): up {string.Join(",", AmbTab[64].Select(Y.F3))} side {string.Join(",", AmbTab[32].Select(Y.F3))} down {string.Join(",", AmbTab[0].Select(Y.F3))}");
    }
    try {
      var pr = Rc.D(rs, "m_AmbientProbe"); var sh = Rc.L(pr, "sh");
      if (sh != null && sh.Count == 27) { ProbeSh = sh.Select(x => Convert.ToDouble(x, Rc.IC)).ToArray(); var up = ProbeEval(new V3(0, 1, 0)); var dn = ProbeEval(new V3(0, -1, 0)); Console.WriteLine($"serialized ambient probe (amb=probe): up {string.Join(",", up.Select(Y.F3))} down {string.Join(",", dn.Select(Y.F3))}"); }
    } catch { }
    // skybox material
    try {
      var (smf, smo) = Rc.Ptr(f, rs?.GetValueOrDefault("m_SkyboxMaterial"));
      if (smo != null) {
        var sd = Rc.Read(smf, smo); var sp = Rc.D(sd, "m_SavedProperties"); string sname = Rc.S(sd, "m_Name");
        double ex = 1; double[] tint = { 0.5, 0.5, 0.5, 0.5 }; object cube = null;
        foreach (var x in Rc.L(sp, "m_Floats") ?? new()) if (Rc.S(x, "first") == "_Exposure") ex = Rc.N(x, "second");
        foreach (var x in Rc.L(sp, "m_Colors") ?? new()) if (Rc.S(x, "first") == "_Tint") tint = Rc.Col((x as Dictionary<string, object>)["second"]);
        foreach (var x in Rc.L(sp, "m_TexEnvs") ?? new()) if (Rc.S(x, "first") == "_Tex" || Rc.S(x, "first") == "_Cube" || Rc.S(x, "first") == "_MainTex") { var tp = Rc.D(x, "second")?.GetValueOrDefault("m_Texture"); if (Rc.Ptr(smf, tp).o != null) cube = tp; }
        SkyFaces = cube == null ? null : LoadCube(smf, cube, 256, out SkyNote);
        SkyEnv = cube == null ? null : LoadCube(smf, cube, 16, out _);
        // Skybox/Cubemap: tex * _Tint * unity_ColorSpaceDouble (linear 4.59) * _Exposure
        SkyTint = CM.LinC(tint).Select(v => v * 4.59479 * ex).ToArray();
        Console.WriteLine($"skybox material {sname}: cubemap {SkyNote}; exposure {Y.F3(ex)} tint {string.Join(",", tint.Take(3).Select(Y.F3))}");
      }
      var (rf, ro) = Rc.Ptr(f, rs?.GetValueOrDefault("m_CustomReflection"));
      if (ro != null) { EnvFaces = LoadCube(rf, rs["m_CustomReflection"], 64, out EnvNote); Console.WriteLine($"custom reflection: {EnvNote}"); }
    } catch (Exception e) { Console.WriteLine("sky: " + e.Message); }
    // lights
    double best = -1;
    foreach (var n in nodes) {
      var lc = n.Comp(108); if (lc == null) continue; var ld = Rc.Read(f, lc);
      bool en = n.Active && Rc.N(ld, "m_Enabled") != 0;
      int type = (int)Rc.N(ld, "m_Type"); var col = CM.LinC(Rc.Col(ld["m_Color"])); double inten = Rc.N(ld, "m_Intensity"), range = Rc.N(ld, "m_Range"), spot = Rc.N(ld, "m_SpotAngle"), inner = Rc.N(ld, "m_InnerSpotAngle");
      var shd = Rc.D(ld, "m_Shadows"); int shType = (int)Rc.N(shd, "m_Type"); double shStr = Rc.N(shd, "m_Strength");
      var fwd = CM.Nrm(CM.D(n.M, new V3(0, 0, 1))); var pos = new V3(n.M[3], n.M[7], n.M[11]);
      if (verbose || true) Console.WriteLine($"  light {(en ? "on " : "off")} {(type == 0 ? "spot" : type == 1 ? "dir" : type == 2 ? "point" : "area" + type)} {n.Path} at {CM.F(pos.x)},{CM.F(pos.y)},{CM.F(pos.z)} fwd {CM.F(fwd.x)},{CM.F(fwd.y)},{CM.F(fwd.z)} col(lin) {string.Join(",", col.Select(Y.F3))} I {Y.F3(inten)} range {Y.F3(range)} spot {Y.F3(spot)}/{Y.F3(inner)} shadows {shType}/{Y.F3(shStr)} mode {Rc.N(ld, "m_Lightmapping")}");
      if (!en) continue;
      if (type == 1) { if (inten > best) { best = inten; HasSun = true; SunDir = fwd * -1; SunCol = col.Select(c => c * inten).ToArray(); SunStrength = shType > 0 ? shStr : 0; } continue; }
      if (type != 0 && type != 2) continue;
      var pl = new PL { p = pos, fwd = fwd, col = col.Select(c => c * inten).ToArray(), range = range, type = type, name = n.Name };
      if (type == 0) { if (inner <= 0) inner = spot * 0.8; pl.spotOut = Math.Cos(spot * Math.PI / 360); pl.spotIn = Math.Cos(Math.Min(inner, spot - 0.01) * Math.PI / 360); }
      Lights.Add(pl);
    }
    Console.WriteLine($"sun {(HasSun ? $"dir-to-sun {CM.F(SunDir.x)},{CM.F(SunDir.y)},{CM.F(SunDir.z)} col {string.Join(",", SunCol.Select(Y.F3))} shadow strength {Y.F3(SunStrength)}" : "none")}; {Lights.Count} point/spot lights");
  }
  public static double[] ProbeEval(V3 n) {
    var r = new double[3];
    for (int c = 0; c < 3; c++) {
      double S(int i) => ProbeSh[c * 9 + i];
      // Unity SphericalHarmonicsL2 raw coefficients (channel-major, 9 per channel): evaluate with the standard basis constants
      double v = S(0) * 0.282095 + S(1) * 0.488603 * n.y + S(2) * 0.488603 * n.z + S(3) * 0.488603 * n.x + S(4) * 1.092548 * n.x * n.y + S(5) * 1.092548 * n.y * n.z + S(6) * 0.315392 * (3 * n.z * n.z - 1) + S(7) * 1.092548 * n.x * n.z + S(8) * 0.546274 * (n.x * n.x - n.y * n.y);
      r[c] = Math.Max(0, v);
    }
    return r;
  }

  public static void ListMats() {
    foreach (var m in Mats.OrderByDescending(x => x.Uses))
      Console.WriteLine($"{m.Uses,4} {m.Name} | {m.Shader} {m.Mode}{(m.AlphaTest ? " cut=" + Y.F3(m.Cut) : "")} cull={m.Cull}{(m.Amask ? " amask" : "")} | tex {m.TexName} {(m.Tex != null ? $"meanA {Y.F3(m.Tex.MeanA)}" : "")} tile {Y.F3(m.Tile[0])},{Y.F3(m.Tile[1])} | tint(lin) {string.Join(",", m.Tint.Select(Y.F3))} a {Y.F3(m.TintA)} | emit {(m.Emit == null ? "-" : string.Join(",", m.Emit.Select(Y.F3)) + (m.EmTex != null ? " x " + m.EmTex.Name : ""))} | spec {(m.SpecSetup ? string.Join(",", m.SpecCol.Select(Y.F3)) + (m.SpecTex != null ? " x " + m.SpecTex.Name : "") : "metallic")} | {m.Note}");
  }
}

// ------------------------------------------------------------------------------------------------ poses
sealed class CPose { public string Name, Src; public double[] Pos, Look; public double Fov = 60; public int W = 1280, H = 720; public string Sheet; public int Tile; public double YMax = 1e9; }
static class CPoses {
  static double[] Arr(string s) => s.Split(',').Select(x => double.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
  public static List<CPose> Read(string path) {
    var res = new List<CPose>(); var txt = File.ReadAllText(path);
    foreach (Match m in Regex.Matches(txt, @"\{[^{}]*""name""[^{}]*\}")) {
      var o = m.Value; string S(string k) { var mm = Regex.Match(o, "\"" + k + "\"\\s*:\\s*\"([^\"]*)\""); return mm.Success ? mm.Groups[1].Value : null; }
      string N(string k) { var mm = Regex.Match(o, "\"" + k + "\"\\s*:\\s*(-?[0-9.eE+-]+)"); return mm.Success ? mm.Groups[1].Value : null; }
      double[] V(string k) { var mm = Regex.Match(o, "\"" + k + "\"\\s*:\\s*\\[([^\\]]*)\\]"); return mm.Success ? Arr(mm.Groups[1].Value) : null; }
      var p = new CPose { Name = S("name"), Pos = V("pos"), Look = V("look"), Src = S("src"), Sheet = S("sheet") };
      if (N("fov") is string fv) p.Fov = double.Parse(fv); if (N("w") is string w) p.W = int.Parse(w); if (N("h") is string h) p.H = int.Parse(h); if (N("tile") is string t) p.Tile = int.Parse(t); if (N("ymax") is string ym) p.YMax = double.Parse(ym);
      if (p.Name != null && p.Pos != null && p.Look != null) res.Add(p);
    }
    return res;
  }
}

// ------------------------------------------------------------------------------------------------ renderer
static class CRRender {
  static double Exposure = 1; static int SS = 2; static bool AttUrp, Spec = true, UseLights = true;
  static float[] P => CRScene.P; static float[] Nn => CRScene.Nn; static float[] UV => CRScene.UV; static float[] C => CRScene.C; static int[] I => CRScene.I; static int[] TM => CRScene.TM; static List<CMat> Mats => CRScene.Mats;
  // shadow map
  static float[] SM; static int SMN; static V3 SU, SV, SF; static double SU0, SV0, SScale;
  static (int x, int y) DbgPix = Environment.GetEnvironmentVariable("RCREF_PIX") is string dp ? (int.Parse(dp.Split(',')[0]), int.Parse(dp.Split(',')[1])) : (-1, -1);

  static double[] Ambient(V3 n) {
    if (CRScene.UseProbe && CRScene.ProbeSh != null) return CRScene.ProbeEval(n);
    double s = (Math.Clamp(n.y, -1, 1) + 1) * 32; int i = Math.Min(63, (int)s); double f = s - i; var a = CRScene.AmbTab[i]; var b = CRScene.AmbTab[i + 1];
    return new[] { a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f };
  }
  static double[] Cube(CTex[] faces, V3 d, double scale, double[] tint) {
    double ax = Math.Abs(d.x), ay = Math.Abs(d.y), az = Math.Abs(d.z); int f; double u, v;
    if (ax >= ay && ax >= az) { if (d.x > 0) { f = 0; u = -d.z / ax; v = -d.y / ax; } else { f = 1; u = d.z / ax; v = -d.y / ax; } }
    else if (ay >= az) { if (d.y > 0) { f = 2; u = d.x / ay; v = d.z / ay; } else { f = 3; u = d.x / ay; v = -d.z / ay; } }
    else { if (d.z > 0) { f = 4; u = d.x / az; v = -d.y / az; } else { f = 5; u = -d.x / az; v = -d.y / az; } }
    var fc = faces[f]; int N = fc.W; int i = Math.Clamp((int)((u + 1) * 0.5 * N), 0, N - 1), j = Math.Clamp((int)((v + 1) * 0.5 * N), 0, N - 1);
    if (fc.Hdr != null) { int q = (j * N + i) * 3; return new[] { fc.Hdr[q] * tint[0] * scale, fc.Hdr[q + 1] * tint[1] * scale, fc.Hdr[q + 2] * tint[2] * scale }; }
    int o = (j * N + i) * 4; var px = fc.Mips[0];
    return new[] { CM.Lin(px[o] / 255.0) * tint[0] * scale, CM.Lin(px[o + 1] / 255.0) * tint[1] * scale, CM.Lin(px[o + 2] / 255.0) * tint[2] * scale };
  }
  static double[] Sky(V3 d) {
    if (CRScene.SkyFaces != null) return Cube(CRScene.SkyFaces, d, 1, CRScene.SkyTint);
    double t = Math.Clamp(d.y, -1, 1); var s = CRScene.AmbSky; var e = CRScene.AmbEq; var g = CRScene.AmbGround;
    return t > 0 ? new[] { e[0] + (s[0] - e[0]) * t, e[1] + (s[1] - e[1]) * t, e[2] + (s[2] - e[2]) * t } : new[] { e[0] + (g[0] - e[0]) * -t, e[1] + (g[1] - e[1]) * -t, e[2] + (g[2] - e[2]) * -t };
  }
  static double[] Env(V3 d) => CRScene.EnvFaces != null ? Cube(CRScene.EnvFaces, d, 1, new double[] { 1, 1, 1 }) : CRScene.SkyEnv != null && UseSkyEnv ? Cube(CRScene.SkyEnv, d, 1, CRScene.SkyTint) : Ambient(d);
  static bool UseSkyEnv = true, SpecMap;

  static void BuildShadow(int size) {
    if (!CRScene.HasSun || CRScene.SunStrength <= 0) return;
    SF = CRScene.SunDir * -1; SU = CM.Nrm(CM.Cross(Math.Abs(SF.y) > 0.9 ? new V3(1, 0, 0) : new V3(0, 1, 0), SF)); SV = CM.Cross(SF, SU);
    // cover the building (not the backdrop dome or the terrain's far edges): 2nd..98th percentile of caster vertices
    var us = new List<double>(); var vs = new List<double>();
    for (int t = 0; t < CRScene.NT; t += 3) { if (!CRScene.TCast[t] || Mats[TM[t]].Terrain) continue; int vi = I[t * 3]; var p = new V3(P[vi * 3], P[vi * 3 + 1], P[vi * 3 + 2]); us.Add(CM.Dot(p, SU)); vs.Add(CM.Dot(p, SV)); }
    us.Sort(); vs.Sort(); double Pc(List<double> l, double q) => l.Count == 0 ? 0 : l[Math.Clamp((int)(q * (l.Count - 1)), 0, l.Count - 1)];
    double u0 = Pc(us, 0.02), u1 = Pc(us, 0.98), v0 = Pc(vs, 0.02), v1 = Pc(vs, 0.98);
    SMN = size; SU0 = u0 - 2; SV0 = v0 - 2; SScale = (size - 1) / Math.Max(u1 - u0 + 4, v1 - v0 + 4);
    SM = new float[size * size]; Array.Fill(SM, float.MaxValue);
    for (int t = 0; t < CRScene.NT; t++) {
      if (!CRScene.TCast[t]) continue; var mat = Mats[TM[t]];
      double[] X = new double[3], Yv = new double[3], Z = new double[3];
      for (int k = 0; k < 3; k++) { int vi = I[t * 3 + k]; var p = new V3(P[vi * 3], P[vi * 3 + 1], P[vi * 3 + 2]); X[k] = (CM.Dot(p, SU) - SU0) * SScale; Yv[k] = (CM.Dot(p, SV) - SV0) * SScale; Z[k] = CM.Dot(p, SF); }
      int x0 = Math.Max(0, (int)Math.Floor(X.Min())), x1 = Math.Min(size - 1, (int)Math.Ceiling(X.Max())), y0 = Math.Max(0, (int)Math.Floor(Yv.Min())), y1 = Math.Min(size - 1, (int)Math.Ceiling(Yv.Max()));
      if (x0 > x1 || y0 > y1) continue;
      double den = (Yv[1] - Yv[2]) * (X[0] - X[2]) + (X[2] - X[1]) * (Yv[0] - Yv[2]); if (Math.Abs(den) < 1e-12) continue;
      bool at = mat.AlphaTest && mat.Tex != null;
      for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) {
        double px = x + 0.5, py = y + 0.5;
        double l1 = ((Yv[1] - Yv[2]) * (px - X[2]) + (X[2] - X[1]) * (py - Yv[2])) / den, l2 = ((Yv[2] - Yv[0]) * (px - X[2]) + (X[0] - X[2]) * (py - Yv[2])) / den, l3 = 1 - l1 - l2;
        if (l1 < 0 || l2 < 0 || l3 < 0) continue;
        double z = l1 * Z[0] + l2 * Z[1] + l3 * Z[2]; int id = y * size + x; if (z >= SM[id]) continue;
        if (at) { var (u, v) = UvAt(t, l1, l2, l3, mat.Tile, mat.Toff); if (mat.Tex.AlphaAt(u, v) < mat.Cut) continue; }
        SM[id] = (float)z;
      }
    }
  }
  static double ShadowAt(V3 p, V3 n) {
    if (SM == null) return 1;
    var q = p + n * 0.04 + CRScene.SunDir * 0.02; double u = (CM.Dot(q, SU) - SU0) * SScale, v = (CM.Dot(q, SV) - SV0) * SScale, z = CM.Dot(q, SF);
    int cx = (int)Math.Floor(u), cy = (int)Math.Floor(v); double lit = 0; int cnt = 0;
    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int x = cx + dx, y = cy + dy; cnt++; if (x < 0 || y < 0 || x >= SMN || y >= SMN) { lit++; continue; } if (z <= SM[y * SMN + x] + 0.03) lit++; }
    return lit / cnt;
  }
  static (double u, double v) UvAt(int t, double b0, double b1, double b2, double[] tile, double[] off) {
    int a = I[t * 3], b = I[t * 3 + 1], c = I[t * 3 + 2];
    double u = UV[a * 2] * b0 + UV[b * 2] * b1 + UV[c * 2] * b2, v = UV[a * 2 + 1] * b0 + UV[b * 2 + 1] * b1 + UV[c * 2 + 1] * b2;
    return (u * tile[0] + off[0], v * tile[1] + off[1]);
  }

  /// linear rgb of triangle t at barycentrics seen from eye; alpha out (fade / add)
  static double[] Shade(int t, double b0, double b1, double b2, V3 eye, double pixAngle, out double alpha) {
    var mat = Mats[TM[t]]; int a = I[t * 3], b = I[t * 3 + 1], c = I[t * 3 + 2];
    var wp = new V3(P[a * 3] * b0 + P[b * 3] * b1 + P[c * 3] * b2, P[a * 3 + 1] * b0 + P[b * 3 + 1] * b1 + P[c * 3 + 1] * b2, P[a * 3 + 2] * b0 + P[b * 3 + 2] * b1 + P[c * 3 + 2] * b2);
    var n = CM.Nrm(new V3(Nn[a * 3] * b0 + Nn[b * 3] * b1 + Nn[c * 3] * b2, Nn[a * 3 + 1] * b0 + Nn[b * 3 + 1] * b1 + Nn[c * 3 + 1] * b2, Nn[a * 3 + 2] * b0 + Nn[b * 3 + 2] * b1 + Nn[c * 3 + 2] * b2));
    var toEye = eye - wp; double dist = toEye.Len(); var vdir = toEye * (1 / Math.Max(dist, 1e-9));
    var pa = new V3(P[a * 3], P[a * 3 + 1], P[a * 3 + 2]); var fn = CM.Cross(new V3(P[b * 3], P[b * 3 + 1], P[b * 3 + 2]) - pa, new V3(P[c * 3], P[c * 3 + 1], P[c * 3 + 2]) - pa);
    if (CM.Dot(fn, toEye) < 0) { n = n * -1; fn = fn * -1; }
    if (CM.Dot(n, CM.Nrm(fn)) < -0.2) n = CM.Nrm(fn); // normals pointing into the face (bad data): use the face
    double r, g, bl; alpha = 1; double er = 0, eg = 0, eb = 0;
    if (mat.Terrain) {
      double fx = (wp.x - mat.TerrOrg[0]) / mat.TerrSize[0], fz = (wp.z - mat.TerrOrg[1]) / mat.TerrSize[1];
      double[] wts = { 1, 0, 0, 0 };
      if (mat.Control != null) { var cp = mat.Control.Mips[0]; int cx = Math.Clamp((int)(fx * mat.Control.W), 0, mat.Control.W - 1), cz = Math.Clamp((int)(fz * mat.Control.H), 0, mat.Control.H - 1); int o = (cz * mat.Control.W + cx) * 4; wts = new[] { cp[o] / 255.0, cp[o + 1] / 255.0, cp[o + 2] / 255.0, cp[o + 3] / 255.0 }; }
      r = g = bl = 0; double ws = 0;
      for (int k = 0; k < Math.Min(4, mat.Layers.Count); k++) {
        if (wts[k] <= 0.001) continue; var ly = mat.Layers[k]; double lr = 0.2, lg = 0.3, lb = 0.1;
        if (ly.t != null) { double lod = Math.Log2(Math.Max(1e-9, ly.t.W / ly.tile[0] * dist * pixAngle / Math.Sqrt(Math.Max(0.05, Math.Abs(CM.Dot(n, vdir)))))); ly.t.Sample(wp.x / ly.tile[0], wp.z / ly.tile[1], lod, out lr, out lg, out lb, out _); }
        r += wts[k] * lr * ly.remap[0]; g += wts[k] * lg * ly.remap[1]; bl += wts[k] * lb * ly.remap[2]; ws += wts[k];
      }
      if (ws > 0) { r /= ws; g /= ws; bl /= ws; }
    } else {
      double tr = 1, tg = 1, tb = 1, ta = 1;
      if (mat.Tex != null) {
        var (u, v) = UvAt(t, b0, b1, b2, mat.Tile, mat.Toff);
        double lod = Math.Log2(Math.Max(1e-9, CRScene.TK[t] * Math.Max(mat.Tex.W, mat.Tex.H) * dist * pixAngle / Math.Sqrt(Math.Max(0.05, Math.Abs(CM.Dot(n, vdir))))));
        mat.Tex.Sample(u, v, lod, out tr, out tg, out tb, out ta);
      }
      double vr = C[a * 4] * b0 + C[b * 4] * b1 + C[c * 4] * b2, vg = C[a * 4 + 1] * b0 + C[b * 4 + 1] * b1 + C[c * 4 + 1] * b2, vb = C[a * 4 + 2] * b0 + C[b * 4 + 2] * b1 + C[c * 4 + 2] * b2, va = C[a * 4 + 3] * b0 + C[b * 4 + 3] * b1 + C[c * 4 + 3] * b2;
      double kr = mat.Tint[0] * vr, kg = mat.Tint[1] * vg, kb = mat.Tint[2] * vb;
      if (mat.Amask) { r = tr * (1 - ta + ta * kr); g = tg * (1 - ta + ta * kg); bl = tb * (1 - ta + ta * kb); }
      else { r = tr * kr; g = tg * kg; bl = tb * kb; }
      if (mat.Mode == "fade" || mat.Mode == "add") alpha = Math.Clamp(ta * mat.TintA * va, 0, 1);
      if (mat.Emit != null) {
        double mr = 1, mg = 1, mb = 1;
        if (mat.EmTex != null) { var (u, v) = UvAt(t, b0, b1, b2, mat.EmTile, mat.EmOff); mat.EmTex.Sample(u, v, Math.Log2(Math.Max(1e-9, CRScene.TK[t] * mat.EmTex.W * dist * pixAngle)), out mr, out mg, out mb, out _); }
        er = mat.Emit[0] * mr; eg = mat.Emit[1] * mg; eb = mat.Emit[2] * mb;
      }
    }
    double or, og, ob;
    if (!mat.Lit) { or = r; og = g; ob = bl; }
    else {
      // URP Lit (metallic workflow): diffuse = albedo * (1 - metallic) * 0.96, spec = lerp(0.04, albedo, metallic)
      double oneMinusRefl, s0r, s0g, s0b, smooth = mat.Smooth;
      if (mat.SpecSetup) {
        s0r = mat.SpecCol[0]; s0g = mat.SpecCol[1]; s0b = mat.SpecCol[2];
        if (mat.SpecTex != null && SpecMap) { var (u, v) = UvAt(t, b0, b1, b2, mat.Tile, mat.Toff); mat.SpecTex.Sample(u, v, Math.Log2(Math.Max(1e-9, CRScene.TK[t] * mat.SpecTex.W * dist * pixAngle)), out s0r, out s0g, out s0b, out double sga); smooth = sga * mat.Smooth; }
        oneMinusRefl = 1 - Math.Max(s0r, Math.Max(s0g, s0b));
      } else { oneMinusRefl = 0.96 * (1 - mat.Metal); s0r = 0.04 + (r - 0.04) * mat.Metal; s0g = 0.04 + (g - 0.04) * mat.Metal; s0b = 0.04 + (bl - 0.04) * mat.Metal; }
      double dr = r * oneMinusRefl, dg = g * oneMinusRefl, db = bl * oneMinusRefl;
      var amb = Ambient(n); or = amb[0] * dr; og = amb[1] * dg; ob = amb[2] * db;
      double pr = 1 - smooth, rough = Math.Max(pr * pr, 0.0078125), r2 = rough * rough;
      if (Spec && !mat.NoEnv && !mat.Terrain) {
        double nv = Math.Clamp(CM.Dot(n, vdir), 0, 1); var R = n * (2 * CM.Dot(n, vdir)) - vdir; double fres = Math.Pow(1 - nv, 4); double grazing = Math.Clamp(smooth + 1 - oneMinusRefl, 0, 1);
        var env = Env(R); double sr = 1 / (r2 + 1);
        // URP: indirect specular from the probe is blurred by roughness; the env here is the low mip (no blur): weight by smoothness
        or += env[0] * sr * (s0r + (grazing - s0r) * fres); og += env[1] * sr * (s0g + (grazing - s0g) * fres); ob += env[2] * sr * (s0b + (grazing - s0b) * fres);
      }
      if (CRScene.HasSun) {
        double nl = CM.Dot(n, CRScene.SunDir);
        if (nl > 0) {
          double sh = mat.NoShadow ? 1 : 1 - CRScene.SunStrength * (1 - ShadowAt(wp, n));
          double hs = 0;
          if (Spec && !mat.NoSpecHi && !mat.Terrain) { var Hh = CM.Nrm(vdir + CRScene.SunDir); double nh = Math.Clamp(CM.Dot(n, Hh), 0, 1), lh = Math.Clamp(CM.Dot(CRScene.SunDir, Hh), 0, 1); double dd = nh * nh * (r2 - 1) + 1.00001; hs = Math.Clamp(r2 / (dd * dd * Math.Max(lh * lh, 0.1) * (rough * 4 + 2)), 0, 100); }
          var sc = CRScene.SunCol; or += (dr + s0r * hs) * sc[0] * nl * sh; og += (dg + s0g * hs) * sc[1] * nl * sh; ob += (db + s0b * hs) * sc[2] * nl * sh;
        }
      }
      if (UseLights) {
        double lr = 0, lg = 0, lb = 0;
        foreach (var pl in CRScene.Lights) {
          var L2 = pl.p - wp; double d2 = CM.Dot(L2, L2); double rr = pl.range * pl.range; if (d2 >= rr) continue; var ld = L2 * (1 / Math.Sqrt(Math.Max(d2, 6.1e-5)));
          double nl = Math.Clamp(CM.Dot(n, ld), 0, 1); if (nl <= 0) continue;
          double att;
          if (AttUrp) { double f = d2 / rr; double s = Math.Clamp(1 - f * f, 0, 1); att = s * s / Math.Max(d2, 1e-4); }
          else { double s = Math.Clamp(1 - d2 / rr, 0, 1); att = s * s * (3 - 2 * s); }
          if (pl.type == 0) { double cd = -CM.Dot(ld, pl.fwd); double q = Math.Clamp((cd - pl.spotOut) / Math.Max(1e-4, pl.spotIn - pl.spotOut), 0, 1); att *= AttUrp ? q * q : (q * q * (3 - 2 * q)) * (q * q * (3 - 2 * q)); }
          lr += pl.col[0] * nl * att; lg += pl.col[1] * nl * att; lb += pl.col[2] * nl * att;
        }
        or += lr * dr; og += lg * dg; ob += lb * db;
      }
    }
    or += er; og += eg; ob += eb;
    if (CRScene.Fog && mat.Mode != "add") {
      double fk = CRScene.FogMode == 1 ? Math.Clamp((CRScene.FogEnd - dist) / Math.Max(1e-6, CRScene.FogEnd - CRScene.FogStart), 0, 1) : CRScene.FogMode == 2 ? Math.Exp(-CRScene.FogDens * dist) : Math.Exp(-(CRScene.FogDens * dist) * (CRScene.FogDens * dist));
      var fc = CRScene.FogCol; or = fc[0] + (or - fc[0]) * fk; og = fc[1] + (og - fc[1]) * fk; ob = fc[2] + (ob - fc[2]) * fk;
    }
    return new[] { or, og, ob };
  }

  sealed class Cam { public V3 pos, fwd, right, up; public double f; public int W, H; public double pixAngle; }
  static Cam MakeCam(CPose p, int W, int H) {
    var cam = new Cam { pos = new V3(p.Pos[0], p.Pos[1], p.Pos[2]), W = W, H = H };
    cam.fwd = CM.Nrm(new V3(p.Look[0] - p.Pos[0], p.Look[1] - p.Pos[1], p.Look[2] - p.Pos[2]));
    cam.right = CM.Nrm(new V3(cam.fwd.z, 0, -cam.fwd.x)); if (cam.right.Len() < 0.5) cam.right = new V3(1, 0, 0);
    cam.up = CM.Cross(cam.fwd, cam.right); cam.f = H * 0.5 / Math.Tan(p.Fov * Math.PI / 360); cam.pixAngle = 1 / cam.f; return cam;
  }
  public static Regex SkipMat;
  /// the skybox as an equirectangular 1024 x 512 PNG (up = top, yaw 0 = +z in the middle)
  public static void SkyDump(string path) {
    int W = 1024, H = 512; var b = new byte[W * H * 3];
    for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
      double lon = (x + 0.5) / W * 2 * Math.PI - Math.PI, lat = Math.PI / 2 - (y + 0.5) / H * Math.PI;
      var d = new V3(Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat), Math.Cos(lat) * Math.Cos(lon)); var c = Sky(d);
      for (int k = 0; k < 3; k++) b[(y * W + x) * 3 + k] = (byte)Math.Round(CM.Gam(c[k]) * 255);
    }
    CRPng.Write(path, W, H, b); Console.WriteLine("BC6H modes used: " + string.Join(" ", Bc6.ModeUse.Select((n, i) => (n, i)).Where(x => x.n > 0).Select(x => $"m{x.i + 1}:{x.n}")));
  }
  static byte[] RenderView(CPose pose, int W, int H) {
    var cam = MakeCam(pose, W, H); const double near = 0.05; int NV = CRScene.NV, NT = CRScene.NT;
    var zb = new float[W * H]; Array.Fill(zb, float.MaxValue); var tid = new int[W * H]; Array.Fill(tid, -1); var bb = new float[W * H * 2];
    var cx = new float[NV]; var cy = new float[NV]; var cz = new float[NV];
    for (int i = 0; i < NV; i++) { var d = new V3(P[i * 3] - cam.pos.x, P[i * 3 + 1] - cam.pos.y, P[i * 3 + 2] - cam.pos.z); cx[i] = (float)CM.Dot(d, cam.right); cy[i] = (float)CM.Dot(d, cam.up); cz[i] = (float)CM.Dot(d, cam.fwd); }
    double tanX = W * 0.5 / cam.f + 0.01, tanY = H * 0.5 / cam.f + 0.01;
    var trans = new List<(int t, double depth)>();
    for (int t = 0; t < NT; t++) {
      int a = I[t * 3], b = I[t * 3 + 1], c = I[t * 3 + 2];
      if (cz[a] < near && cz[b] < near && cz[c] < near) continue;
      if (pose.YMax < 1e8 && Math.Min(P[a * 3 + 1], Math.Min(P[b * 3 + 1], P[c * 3 + 1])) > pose.YMax) continue;
      if (cx[a] > tanX * cz[a] && cx[b] > tanX * cz[b] && cx[c] > tanX * cz[c]) continue;
      if (-cx[a] > tanX * cz[a] && -cx[b] > tanX * cz[b] && -cx[c] > tanX * cz[c]) continue;
      if (cy[a] > tanY * cz[a] && cy[b] > tanY * cz[b] && cy[c] > tanY * cz[c]) continue;
      if (-cy[a] > tanY * cz[a] && -cy[b] > tanY * cz[b] && -cy[c] > tanY * cz[c]) continue;
      var mat = Mats[TM[t]];
      if (mat.Cull != 0) {
        double e1x = cx[b] - cx[a], e1y = cy[b] - cy[a], e1z = cz[b] - cz[a], e2x = cx[c] - cx[a], e2y = cy[c] - cy[a], e2z = cz[c] - cz[a];
        double nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x;
        // Unity: clockwise front faces in a left-handed frame = this cross product points away from the eye for a front face
        double s = -(nx * cx[a] + ny * cy[a] + nz * cz[a]);
        if (mat.Cull == 2 && s < 0) continue; if (mat.Cull == 1 && s > 0) continue;
      }
      if (SkipMat != null && SkipMat.IsMatch(mat.Name)) continue;
      if (mat.Mode == "fade" || mat.Mode == "add") { trans.Add((t, (cz[a] + cz[b] + cz[c]) / 3)); continue; }
      Raster(t, cx, cy, cz, cam, near, (x, y, z, b0, b1, b2) => {
        int id = y * W + x; if (z >= zb[id]) return;
        if (mat.AlphaTest && mat.Tex != null) { var (u, v) = UvAt(t, b0, b1, b2, mat.Tile, mat.Toff); if (mat.Tex.AlphaAt(u, v) * mat.TintA < mat.Cut) return; }
        zb[id] = (float)z; tid[id] = t; bb[id * 2] = (float)b0; bb[id * 2 + 1] = (float)b1;
      });
    }
    var img = new double[W * H * 3];
    Parallel.For(0, H, y => {
      for (int x = 0; x < W; x++) {
        int id = y * W + x; double[] col;
        if (tid[id] < 0) { var dir = CM.Nrm(cam.fwd + cam.right * ((x + 0.5 - W * 0.5) / cam.f) + cam.up * ((H * 0.5 - y - 0.5) / cam.f)); col = Sky(dir); }
        else { double b0 = bb[id * 2], b1 = bb[id * 2 + 1]; col = Shade(tid[id], b0, b1, 1 - b0 - b1, cam.pos, cam.pixAngle, out _); }
        if (DbgPix.x == x && DbgPix.y == y && tid[id] >= 0) Console.WriteLine($"   pix {x},{y} tri {tid[id]} mat {Mats[TM[tid[id]]].Name} renderer {(CRScene.TR[tid[id]] < CRScene.RendNames.Count ? CRScene.RendNames[CRScene.TR[tid[id]]] : "terrain")} col {col[0]:0.###},{col[1]:0.###},{col[2]:0.###}");
        img[id * 3] = col[0]; img[id * 3 + 1] = col[1]; img[id * 3 + 2] = col[2];
      }
    });
    foreach (var (t, _) in trans.OrderByDescending(x => x.depth)) {
      var mat = Mats[TM[t]];
      Raster(t, cx, cy, cz, cam, near, (x, y, z, b0, b1, b2) => {
        int id = y * W + x; if (z >= zb[id]) return;
        var col = Shade(t, b0, b1, b2, cam.pos, cam.pixAngle, out double al);
        if (mat.Mode == "add") { img[id * 3] += col[0] * al; img[id * 3 + 1] += col[1] * al; img[id * 3 + 2] += col[2] * al; }
        else if (mat.Premul) { img[id * 3] = img[id * 3] * (1 - al) + col[0]; img[id * 3 + 1] = img[id * 3 + 1] * (1 - al) + col[1]; img[id * 3 + 2] = img[id * 3 + 2] * (1 - al) + col[2]; }
        else { img[id * 3] = img[id * 3] * (1 - al) + col[0] * al; img[id * 3 + 1] = img[id * 3 + 1] * (1 - al) + col[1] * al; img[id * 3 + 2] = img[id * 3 + 2] * (1 - al) + col[2] * al; }
      });
    }
    int w0 = W / SS, h0 = H / SS; var outb = new byte[w0 * h0 * 3]; double inv = 1.0 / (SS * SS);
    for (int y = 0; y < h0; y++) for (int x = 0; x < w0; x++) for (int ch = 0; ch < 3; ch++) {
      double s = 0; for (int dy = 0; dy < SS; dy++) for (int dx = 0; dx < SS; dx++) s += Math.Clamp(img[((y * SS + dy) * W + x * SS + dx) * 3 + ch] * Exposure, 0, 1);
      outb[(y * w0 + x) * 3 + ch] = (byte)Math.Round(CM.Gam(s * inv) * 255);
    }
    return outb;
  }

  delegate void Frag(int x, int y, double z, double b0, double b1, double b2);
  static void Raster(int t, float[] cx, float[] cy, float[] cz, Cam cam, double near, Frag frag) {
    int[] vi = { I[t * 3], I[t * 3 + 1], I[t * 3 + 2] };
    var poly = new List<(double x, double y, double z, double b0, double b1, double b2)>(4);
    for (int k = 0; k < 3; k++) poly.Add((cx[vi[k]], cy[vi[k]], cz[vi[k]], k == 0 ? 1 : 0, k == 1 ? 1 : 0, k == 2 ? 1 : 0));
    if (poly.Any(p => p.z < near)) {
      var o = new List<(double x, double y, double z, double b0, double b1, double b2)>(4);
      for (int k = 0; k < poly.Count; k++) {
        var p0 = poly[k]; var p1 = poly[(k + 1) % poly.Count];
        if (p0.z >= near) o.Add(p0);
        if ((p0.z >= near) != (p1.z >= near)) { double s = (near - p0.z) / (p1.z - p0.z); o.Add((p0.x + (p1.x - p0.x) * s, p0.y + (p1.y - p0.y) * s, near, p0.b0 + (p1.b0 - p0.b0) * s, p0.b1 + (p1.b1 - p0.b1) * s, p0.b2 + (p1.b2 - p0.b2) * s)); }
      }
      poly = o; if (poly.Count < 3) return;
    }
    int W = cam.W, H = cam.H; double f = cam.f;
    var sx = new double[poly.Count]; var sy = new double[poly.Count];
    for (int k = 0; k < poly.Count; k++) { sx[k] = W * 0.5 + poly[k].x * f / poly[k].z; sy[k] = H * 0.5 - poly[k].y * f / poly[k].z; }
    for (int fi = 1; fi + 1 < poly.Count; fi++) {
      int A = 0, B = fi, Cc = fi + 1;
      double ax = sx[A], ay = sy[A], bx = sx[B], by = sy[B], ccx = sx[Cc], ccy = sy[Cc];
      double den = (by - ccy) * (ax - ccx) + (ccx - bx) * (ay - ccy); if (Math.Abs(den) < 1e-12) continue;
      int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, ccx)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, ccx))));
      int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, ccy)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, ccy))));
      if (x0 > x1 || y0 > y1) continue;
      double iza = 1 / poly[A].z, izb = 1 / poly[B].z, izc = 1 / poly[Cc].z; var pa = poly[A]; var pb = poly[B]; var pc = poly[Cc];
      for (int y = y0; y <= y1; y++) {
        double py = y + 0.5;
        for (int x = x0; x <= x1; x++) {
          double px = x + 0.5;
          double l1 = ((by - ccy) * (px - ccx) + (ccx - bx) * (py - ccy)) / den; if (l1 < 0) continue;
          double l2 = ((ccy - ay) * (px - ccx) + (ax - ccx) * (py - ccy)) / den; if (l2 < 0) continue;
          double l3 = 1 - l1 - l2; if (l3 < 0) continue;
          double w1 = l1 * iza, w2 = l2 * izb, w3 = l3 * izc; double ws = w1 + w2 + w3; double z = 1 / ws; w1 *= z; w2 *= z; w3 *= z;
          frag(x, y, z, pa.b0 * w1 + pb.b0 * w2 + pc.b0 * w3, pa.b1 * w1 + pb.b1 * w2 + pc.b1 * w3, pa.b2 * w1 + pb.b2 * w2 + pc.b2 * w3);
        }
      }
    }
  }

  public static void Run(string posesPath, string framePath, string outDir, string[] opt) {
    string Opt(string k, string d) => opt.FirstOrDefault(o => o.StartsWith(k + "="))?.Substring(k.Length + 1) ?? d;
    Exposure = double.Parse(Opt("exp", "1")); SS = Math.Max(1, int.Parse(Opt("ss", "2"))); AttUrp = Opt("att", "rr") == "urp"; Spec = Opt("spec", "1") == "1"; UseSkyEnv = Opt("env", "amb") == "sky"; SpecMap = Opt("specmap", "0") == "1"; UseLights = Opt("lights", "1") == "1";
    CRScene.UseProbe = Opt("amb", "trilight") == "probe"; CRScene.Shafts = Opt("shafts", "0") == "1";
    if (Opt("skipmat", null) is string skm) SkipMat = new Regex(skm);
    var only = Opt("only", null) is string on ? new Regex(on) : null;
    var (yaw, T) = CRFrame.Read(framePath);
    var poses = CPoses.Read(posesPath).Where(p => only == null || only.IsMatch(p.Name)).ToList();
    // room frame -> cache frame: cache = Ry(-yaw) * (room - T)
    foreach (var p in poses) {
      var a = CM.Yaw(new V3(p.Pos[0], p.Pos[1], p.Pos[2]) - T, -yaw); var b = CM.Yaw(new V3(p.Look[0], p.Look[1], p.Look[2]) - T, -yaw);
      p.Pos = new[] { a.x, a.y, a.z }; p.Look = new[] { b.x, b.y, b.z };
    }
    Console.WriteLine($"{poses.Count} poses from {posesPath}, moved into the cache frame (yaw {yaw}, T {T})");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    CRScene.Load(int.Parse(Opt("texmax", "1024")), false);
    Console.WriteLine($"scene loaded ({sw.Elapsed.TotalSeconds:F1} s)");
    if (Opt("sky", "1") == "0") CRScene.SkyFaces = null;
    if (Opt("shadow", "4096") != "0") { BuildShadow(int.Parse(Opt("shadow", "4096"))); Console.WriteLine($"shadow map {SMN}x{SMN}, {1 / Math.Max(1e-9, SScale) * 100:F1} cm per texel ({sw.Elapsed.TotalSeconds:F1} s)"); }
    Directory.CreateDirectory(outDir);
    var sheets = new Dictionary<string, SortedDictionary<int, byte[]>>(); int done = 0;
    Parallel.ForEach(poses, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, p => {
      var t0 = System.Diagnostics.Stopwatch.StartNew();
      var img = RenderView(p, p.W * SS, p.H * SS); CRPng.Write(Path.Combine(outDir, p.Name + ".png"), p.W, p.H, img);
      if (p.Sheet != null) { var tile = RenderView(p, 800 * SS, 450 * SS); lock (sheets) { if (!sheets.TryGetValue(p.Sheet, out var sd)) sheets[p.Sheet] = sd = new(); sd[p.Tile] = tile; } }
      lock (sheets) { done++; Console.WriteLine($"  [{done}/{poses.Count}] {p.Name}.png ({t0.Elapsed.TotalSeconds:F1} s)"); }
    });
    foreach (var kv in sheets) {
      int n = kv.Value.Count, cols = 2, rows = (n + 1) / 2, tw = 800, th = 450, gap = 4; int sw2 = tw * cols + gap * (cols - 1), sh = th * rows + gap * (rows - 1);
      var px = new byte[sw2 * sh * 3]; Array.Fill(px, (byte)255); int i = 0;
      foreach (var tile in kv.Value.Values) { int ox = (i % cols) * (tw + gap), oy = (i / cols) * (th + gap); for (int y = 0; y < th; y++) Buffer.BlockCopy(tile, y * tw * 3, px, ((oy + y) * sw2 + ox) * 3, tw * 3); i++; }
      for (int c = 1; c < cols; c++) { int x0 = c * (tw + gap) - gap; for (int y = 0; y < sh; y++) foreach (int k in new[] { 0, gap - 1 }) { int o = (y * sw2 + x0 + k) * 3; px[o] = px[o + 1] = px[o + 2] = 40; } }
      for (int r = 1; r < rows; r++) { int y0 = r * (th + gap) - gap; for (int x = 0; x < sw2; x++) foreach (int k in new[] { 0, gap - 1 }) { int o = ((y0 + k) * sw2 + x) * 3; px[o] = px[o + 1] = px[o + 2] = 40; } }
      CRPng.Write(Path.Combine(outDir, kv.Key + ".png"), sw2, sh, px);
      Console.WriteLine($"  sheet {kv.Key}.png ({sw2}x{sh}, {n} tiles)");
    }
    Console.WriteLine($"done in {sw.Elapsed.TotalSeconds:F1} s");
  }
}

static class CRPng {
  public static void Write(string path, int w, int h, byte[] rgb) {
    using var ms = new MemoryStream();
    using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) { for (int y = 0; y < h; y++) { z.WriteByte(0); z.Write(rgb, y * w * 3, w * 3); } }
    using var fs = File.Create(path);
    fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    void Chunk(string type, byte[] data) {
      var t = Encoding.ASCII.GetBytes(type); var len = BitConverter.GetBytes(data.Length); Array.Reverse(len); fs.Write(len); fs.Write(t); fs.Write(data);
      uint crc = Crc(t, 0xFFFFFFFF); crc = Crc(data, crc) ^ 0xFFFFFFFF; var cb = BitConverter.GetBytes(crc); Array.Reverse(cb); fs.Write(cb);
    }
    var ih = new byte[13]; var wb = BitConverter.GetBytes(w); Array.Reverse(wb); var hb = BitConverter.GetBytes(h); Array.Reverse(hb);
    Buffer.BlockCopy(wb, 0, ih, 0, 4); Buffer.BlockCopy(hb, 0, ih, 4, 4); ih[8] = 8; ih[9] = 2;
    Chunk("IHDR", ih); Chunk("IDAT", ms.ToArray()); Chunk("IEND", new byte[0]);
  }
  static uint[] table;
  static uint Crc(byte[] d, uint c) {
    if (table == null) { var tb = new uint[256]; for (uint n = 0; n < 256; n++) { uint x = n; for (int k = 0; k < 8; k++) x = (x & 1) != 0 ? 0xEDB88320 ^ (x >> 1) : x >> 1; tb[n] = x; } table = tb; }
    foreach (var b in d) c = table[(c ^ b) & 0xff] ^ (c >> 8); return c;
  }
}

/// BC6H (unsigned half, Unity format 24) block decoder: the 14 modes' bit layouts as in the D3D11 BC6H spec
/// ("f[a:b]" = the stored bits go from position b to position a).
static class Bc6 {
  // mode value (2 or 5 low bits) -> (layout, endpoint bits, delta bits r/g/b, transformed, two regions)
  static readonly (int val, int mbits, string lay, int epb, int dr, int dg, int db, bool tr, bool two)[] Modes = {
    (0x00, 2, "g2[4] b2[4] b3[4] r0[9:0] g0[9:0] b0[9:0] r1[4:0] g3[4] g2[3:0] g1[4:0] b3[0] g3[3:0] b1[4:0] b3[1] b2[3:0] r2[4:0] b3[2] r3[4:0] b3[3]", 10, 5, 5, 5, true, true),
    (0x01, 2, "g2[5] g3[4] g3[5] r0[6:0] b3[0] b3[1] b2[4] g0[6:0] b2[5] b3[2] g2[4] b0[6:0] b3[3] b3[5] b3[4] r1[5:0] g2[3:0] g1[5:0] g3[3:0] b1[5:0] b2[3:0] r2[5:0] r3[5:0]", 7, 6, 6, 6, true, true),
    (0x02, 5, "r0[9:0] g0[9:0] b0[9:0] r1[4:0] r0[10] g2[3:0] g1[3:0] g0[10] b3[0] g3[3:0] b1[3:0] b0[10] b3[1] b2[3:0] r2[4:0] b3[2] r3[4:0] b3[3]", 11, 5, 4, 4, true, true),
    (0x06, 5, "r0[9:0] g0[9:0] b0[9:0] r1[3:0] r0[10] g3[4] g2[3:0] g1[4:0] g0[10] g3[3:0] b1[3:0] b0[10] b3[1] b2[3:0] r2[3:0] b3[0] b3[2] r3[3:0] g2[4] b3[3]", 11, 4, 5, 4, true, true),
    (0x0A, 5, "r0[9:0] g0[9:0] b0[9:0] r1[3:0] r0[10] b2[4] g2[3:0] g1[3:0] g0[10] b3[0] g3[3:0] b1[4:0] b0[10] b2[3:0] r2[3:0] b3[1] b3[2] r3[3:0] b3[4] b3[3]", 11, 4, 4, 5, true, true),
    (0x0E, 5, "r0[8:0] b2[4] g0[8:0] g2[4] b0[8:0] b3[4] r1[4:0] g3[4] g2[3:0] g1[4:0] b3[0] g3[3:0] b1[4:0] b3[1] b2[3:0] r2[4:0] b3[2] r3[4:0] b3[3]", 9, 5, 5, 5, true, true),
    (0x12, 5, "r0[7:0] g3[4] b2[4] g0[7:0] b3[2] g2[4] b0[7:0] b3[3] b3[4] r1[5:0] g2[3:0] g1[4:0] b3[0] g3[3:0] b1[4:0] b3[1] b2[3:0] r2[5:0] r3[5:0]", 8, 6, 5, 5, true, true),
    (0x16, 5, "r0[7:0] b3[0] b2[4] g0[7:0] g2[5] g2[4] b0[7:0] g3[5] b3[4] r1[4:0] g3[4] g2[3:0] g1[5:0] g3[3:0] b1[4:0] b3[1] b2[3:0] r2[4:0] b3[2] r3[4:0] b3[3]", 8, 5, 6, 5, true, true),
    (0x1A, 5, "r0[7:0] b3[1] b2[4] g0[7:0] b2[5] g2[4] b0[7:0] b3[5] b3[4] r1[4:0] g3[4] g2[3:0] g1[4:0] b3[0] g3[3:0] b1[5:0] b2[3:0] r2[4:0] b3[2] r3[4:0] b3[3]", 8, 5, 5, 6, true, true),
    (0x1E, 5, "r0[5:0] g3[4] b3[0] b3[1] b2[4] g0[5:0] g2[5] b2[5] b3[2] g2[4] b0[5:0] g3[5] b3[3] b3[5] b3[4] r1[5:0] g2[3:0] g1[5:0] g3[3:0] b1[5:0] b2[3:0] r2[5:0] r3[5:0]", 6, 6, 6, 6, false, true),
    (0x03, 5, "r0[9:0] g0[9:0] b0[9:0] r1[9:0] g1[9:0] b1[9:0]", 10, 10, 10, 10, false, false),
    (0x07, 5, "r0[9:0] g0[9:0] b0[9:0] r1[8:0] r0[10] g1[8:0] g0[10] b1[8:0] b0[10]", 11, 9, 9, 9, true, false),
    (0x0B, 5, "r0[9:0] g0[9:0] b0[9:0] r1[7:0] r0[10:11] g1[7:0] g0[10:11] b1[7:0] b0[10:11]", 12, 8, 8, 8, true, false),
    (0x0F, 5, "r0[9:0] g0[9:0] b0[9:0] r1[3:0] r0[10:15] g1[3:0] g0[10:15] b1[3:0] b0[10:15]", 16, 4, 4, 4, true, false) };
  static readonly List<(int ch, int ep, int pos)>[] Parsed = Modes.Select(m => {
    var l = new List<(int, int, int)>();
    foreach (Match t in Regex.Matches(m.lay, @"([rgb])(\d)\[(\d+)(?::(\d+))?\]")) {
      int ch = "rgb".IndexOf(t.Groups[1].Value[0]), ep = t.Groups[2].Value[0] - '0', a = int.Parse(t.Groups[3].Value);
      if (!t.Groups[4].Success) { l.Add((ch, ep, a)); continue; }
      int b = int.Parse(t.Groups[4].Value); int step = a >= b ? 1 : -1; for (int p = b; ; p += step) { l.Add((ch, ep, p)); if (p == a) break; }
    }
    return l;
  }).ToArray();
  static readonly uint[] P2 = { 0xCCCC, 0x8888, 0xEEEE, 0xECC8, 0xC880, 0xFEEC, 0xFEC8, 0xEC80, 0xC800, 0xFFEC, 0xFE80, 0xE800, 0xFFE8, 0xFF00, 0xFFF0, 0xF000, 0xF710, 0x008E, 0x7100, 0x08CE, 0x008C, 0x7310, 0x3100, 0x8CCE, 0x088C, 0x3110, 0x6666, 0x366C, 0x17E8, 0x0FF0, 0x718E, 0x399C };
  static readonly int[] A2 = { 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 2, 8, 2, 2, 8, 8, 15, 2, 8, 2, 2, 8, 8, 2, 2 };
  static readonly int[] W3 = { 0, 9, 18, 27, 37, 46, 55, 64 }, W4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };
  public static int[] ModeUse = new int[15];
  public static void Decode(byte[] s, int off, int w, int h, float[] dst) {
    int bw = (w + 3) / 4, bh = (h + 3) / 4; var px = new float[48];
    for (int by = 0; by < bh; by++) for (int bx = 0; bx < bw; bx++) {
      Block(s, off + (by * bw + bx) * 16, px);
      for (int t = 0; t < 16; t++) { int x = bx * 4 + (t & 3), y = by * 4 + (t >> 2); if (x >= w || y >= h) continue; int q = (y * w + x) * 3; dst[q] = px[t * 3]; dst[q + 1] = px[t * 3 + 1]; dst[q + 2] = px[t * 3 + 2]; }
    }
  }
  static void Block(byte[] s, int o, float[] outp) {
    int p = 0; int Bit() { int v = (s[o + (p >> 3)] >> (p & 7)) & 1; p++; return v; }
    int Bits(int n) { int v = 0; for (int i = 0; i < n; i++) v |= Bit() << i; return v; }
    int m2 = Bits(2); int mi = -1;
    if (m2 < 2) mi = m2; else { p = 0; int m5 = Bits(5); for (int i = 2; i < Modes.Length; i++) if (Modes[i].val == m5) { mi = i; break; } }
    if (mi < 0) { Array.Clear(outp); return; }
    ModeUse[mi]++;
    var md = Modes[mi]; var e = new int[3, 4];
    foreach (var (ch, ep, pos) in Parsed[mi]) e[ch, ep] |= Bit() << pos;
    int part = md.two ? Bits(5) : 0; int ne = md.two ? 4 : 2;
    int mask = (1 << md.epb) - 1; int[] db = { md.dr, md.dg, md.db };
    if (md.tr) for (int c = 0; c < 3; c++) for (int k = 1; k < ne; k++) e[c, k] = (e[c, 0] + SignExt(e[c, k], db[c])) & mask;
    for (int c = 0; c < 3; c++) for (int k = 0; k < ne; k++) e[c, k] = Unq(e[c, k], md.epb);
    int ib = md.two ? 3 : 4; var wt = md.two ? W3 : W4;
    for (int t = 0; t < 16; t++) {
      bool anchor = t == 0 || (md.two && t == A2[part]);
      int idx = Bits(anchor ? ib - 1 : ib);
      int sub = md.two ? (int)((P2[part] >> t) & 1) : 0;
      for (int c = 0; c < 3; c++) {
        int a = e[c, sub * 2], b = e[c, sub * 2 + 1]; int v = (a * (64 - wt[idx]) + b * wt[idx] + 32) >> 6;
        v = (v * 31) >> 6; outp[t * 3 + c] = (float)BitConverter.UInt16BitsToHalf((ushort)v);
      }
    }
  }
  static int SignExt(int v, int bits) => (v & (1 << (bits - 1))) != 0 ? v - (1 << bits) : v;
  static int Unq(int x, int epb) { if (epb >= 15) return x; if (x == 0) return 0; if (x == (1 << epb) - 1) return 0xFFFF; return ((x << 16) + 0x8000) >> epb; }
}
