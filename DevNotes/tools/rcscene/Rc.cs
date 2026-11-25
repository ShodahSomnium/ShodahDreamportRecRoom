// rcscene: Rec Room's own final Rec Center - the July 2025 Studio room "RecCenter-RecCenter_071825" that Rec Room left in
// the user's download cache (%USERPROFILE%\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache: scene bundle 1AF.data and
// its dependency bundles 183..1AE) - measured into RRSK 1 placement text for the world's data build of the Rec Center
// (Places/RecCenter/RrRecCenterRoom: every player without that cache).
//
// MEASUREMENTS ONLY (iteration 16, DevNotes/iter16/rooms_same.md): the bundles are read in place with ../rrtool_cache's
// reader; no file, mesh or texture is copied anywhere; the output is names and numbers:
//   - every drawn surface as gtscene's exact rectangles (Plates, compiled in from ../gtscene/Gt.cs unchanged), with the
//     jt=1 rules per 3D piece: exact rectangles on each face's own axes; big curved pieces as one fitted rectangle per
//     triangle pair; small (< 0.6 m) curved bits left out; small boxy many-sided bits as one "G dress ..._box"; the
//     mesh's vertex colour on each plate (c=); one "G platemat" per material: base colour, main texture and tiling,
//     the texture's measured mean colour (mean=), emission (glow=), two-sided (ds=1), transparent (clear=1), inst=1
//     when the PC install has a material of that name;
//   - renderers whose mesh the PC install has (same name, same vertex count, same bounds within 1 cm) and whose
//     materials the install has as "R" records (the world loads them from the player's install);
//   - colliders as the scene has them: boxes, spheres and capsules; mesh colliders as "C box" plates 0.2 m deep behind
//     each face (gtscene's way); triggers left out;
//   - the point and spot lights ("L"), the key light ("L dir"), the door signs' texts ("G sign") and named spots the
//     room code looks up ("G spot": prefab roots such as [VendingMachine_Static], BulletinBoard);
// Frame: the scene's own world space. The room code builds it under the cached building's container
// (RrRecCenterCache.FramePosition (2.05, -0.09, -7.23), FrameYaw 90), so both builds stand in exactly the same place.
//
// Commands (env RC_CACHE overrides the cache folder, RRTOOL_IDX the install index (DevNotes/rrtoolidx)):
//   convert <out.rrsk> [stats=<file.tsv>] [name=RecCenter_071825]
//   mats        every material of the scene with its properties and texture format
//   nodes [re]  the scene's GameObjects (path, active, world position, components)
//   convert <out.rrsk> poly=1 [name=] [stats=]   (2026-11-23) the layout for the world's Rec Center build in the ROOM frame (= the
//               scene's own world space = the Android level7 frame; framecheck measures it): the shell as clean planar polygons
//               ("G poly", ../gtscene/GtPoly.cs compiled in unchanged, + the world's RrJumbotronPoly.cs by path), platemats with
//               avg= (texture mean x colour) and itex= (the install has the texture), R records also for install meshes with
//               level-only materials ("?" T + K records), level-only trees as "G tree" markers, the room data's spawned
//               objects (HTTPCache/182.data, RcRoomData.cs) as "G spot <kind>" markers, the terrain lowered under the floors, gcol=
//               round 2 (2026-11-23, README): E records (RenderSettings), sa=/sg= measured under each piece's own UVs, colour
//               regions of the marquee / quest rug / poster cases as polygons, ua=/rep= texture frames, G tree avg=, re-exported
//               static-batched install meshes as R records, the community board as the install BulletinBoard, curved
//               pieces as bars (G dress) or facets (G poly) instead of fitted rectangles (RcSample.cs)
//   roomdata    the room data's named objects and world poses; pbdump [depth] [skip] [take], pbscan, pbtop: protobuf views
//   framecheck <cache.rrsk> <level7.rrsk>   the cache -> room transform fitted on the install pieces both place
//   findguid <hex32>...   scene objects holding a room-data GUID (raw or as text)
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

static class RcMain {
  static int Main(string[] a) {
    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    Common.Load(@"E:\SteamLibrary\steamapps\common\RecRoom\UnityPlayer.dll");
    if (a.Length == 0) { Console.WriteLine("rcscene convert <out.rrsk> [stats=<tsv>] | mats | nodes [regex]"); return 1; }
    var sw = System.Diagnostics.Stopwatch.StartNew();
    if (a[0] == "pbdump") { RcRoomData.DumpCmd(a); return 0; }
    if (a[0] == "pbscan") { RcRoomScan.Run(a); return 0; }
    if (a[0] == "pbtop") { RcRoomTop.Run(a); return 0; }
    if (a[0] == "roomdata") { RcRoomSpots.Dump(); return 0; }
    if (a[0] == "framecheck") { RcFrameCheck.Run(a); return 0; }
    if (a[0] == "pngprofile") { RcPng.Profile(a[1], int.Parse(a[2]), int.Parse(a[3]), int.Parse(a[4])); return 0; }
    Rc.OpenScene();
    switch (a[0]) {
      case "convert": RcConvert.Run(a[1], a.Skip(2).ToArray()); break;
      case "mats": RcConvert.DumpMats(); break;
      case "ext": for (int i = 0; i < Rc.Scene.Externals.Count; i++) Console.WriteLine($"{i + 1}	{Rc.Scene.Externals[i]}	{(Rc.FileOf(Path.GetFileName(Rc.Scene.Externals[i])) != null ? "ok" : "-")}"); break;
      case "findguid": RcFindGuid.Run(a); break;
      case "nodes": RcConvert.DumpNodes(a.Length > 1 ? a[1] : "."); break;
      case "texprofile": RcConvert.TexProfile(a.Length > 1 ? a[1] : "."); break;
      case "texpattern": RcConvert.TexProfile(a.Length > 1 ? a[1] : ".", true); break;
      case "dump": { var f = a.Length > 3 ? Rc.FileOf(a[3]) : Rc.Scene; var o = Rc.Obj(f, long.Parse(a[1])); RcDump.Show(Rc.Read(f, o), "", a.Length > 2 ? int.Parse(a[2]) : 2); break; }
      case "comps": foreach (var n in RcConvert.NodeList().Where(x => Regex.IsMatch(x.Path, a[1]))) foreach (var c in n.Comps) Console.WriteLine($"{n.Path}	{ClassNames.Of(c.ClassId)}	{c.PathId}"); break;
      default: Console.WriteLine("unknown command"); return 1;
    }
    Console.Error.WriteLine($"[{sw.Elapsed.TotalSeconds:F1} s]");
    return 0;
  }
}

/// The cache bundles (read in place), their serialized files and resource streams, and PPtr resolution.
static class Rc {
  public static string CacheDir = Environment.GetEnvironmentVariable("RC_CACHE") ?? @"C:\Users\derii\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache";
  public const string InstallDir = @"E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64";
  public static string IdxDir = Environment.GetEnvironmentVariable("RRTOOL_IDX") ?? @"D:\Dreamport\Rec Room\Rec Room\DevNotes\rrtoolidx";
  public static readonly CultureInfo IC = CultureInfo.InvariantCulture;
  static readonly Dictionary<string, (byte[] data, List<(long off, long size, string name)> nodes)> Opened = new();
  static readonly Dictionary<string, string> NodeBundle = new(StringComparer.OrdinalIgnoreCase);
  static readonly Dictionary<string, SFile> Files = new(StringComparer.OrdinalIgnoreCase);
  static readonly Dictionary<SFile, Dictionary<long, SObj>> ById = new();
  public static SFile Scene;

  /// The scene bundle 1AF and the cache bundles downloaded with it (183..1AE, the same second of 26 Jul 2025).
  public static void OpenScene() {
    for (int i = 0x183; i <= 0x1AF; i++) {
      var b = i.ToString("X3"); var p = Path.Combine(CacheDir, b + ".data");
      if (!File.Exists(p) || !Db.IsUnityFS(p)) continue;
      var bo = Bundle.Open(p); Opened[b] = bo;
      foreach (var n in bo.nodes) NodeBundle[n.name] = b;
    }
    var sb = Opened["1AF"]; var sceneNode = sb.nodes.First(n => !n.name.EndsWith(".resS") && !n.name.EndsWith(".sharedAssets") && !n.name.EndsWith(".resource"));
    Scene = FileOf(sceneNode.name);
  }

  public static SFile FileOf(string node) {
    if (Files.TryGetValue(node, out var f)) return f;
    if (!NodeBundle.TryGetValue(node, out var b)) return null;
    var bo = Opened[b]; var n = bo.nodes.First(x => x.name.Equals(node, StringComparison.OrdinalIgnoreCase));
    f = Ser.Parse(n.name, bo.data, (int)n.off); Files[node] = f; ById[f] = f.Objects.ToDictionary(o => o.PathId);
    return f;
  }

  public static SObj Obj(SFile f, long pid) => f != null && ById.TryGetValue(f, out var d) && d.TryGetValue(pid, out var o) ? o : null;

  /// A PPtr {m_FileID, m_PathID} from file f -> (file, object), or (null, null) outside the cache's bundles.
  public static (SFile f, SObj o) Ptr(SFile from, object pptr) {
    var d = pptr as Dictionary<string, object>; if (d == null) return (null, null);
    int fid = Convert.ToInt32(d["m_FileID"]); long pid = Convert.ToInt64(d["m_PathID"]);
    if (pid == 0) return (null, null);
    SFile f = from;
    if (fid > 0) { if (fid - 1 >= from.Externals.Count) return (null, null); f = FileOf(Path.GetFileName(from.Externals[fid - 1])); }
    var o = Obj(f, pid); return o == null ? (null, null) : (f, o);
  }

  /// Resource stream bytes (m_StreamData: "archive:/CAB-x/CAB-x.resS").
  public static byte[] Stream(string path, long off, long size) {
    var node = Path.GetFileName(path ?? ""); if (!NodeBundle.TryGetValue(node, out var b)) return null;
    var bo = Opened[b]; var n = bo.nodes.First(x => x.name.Equals(node, StringComparison.OrdinalIgnoreCase));
    var r = new byte[size]; Buffer.BlockCopy(bo.data, (int)(n.off + off), r, 0, (int)size); return r;
  }

  public static Dictionary<string, object> Read(SFile f, SObj o) => o == null ? null : Ser.Read(f, o) as Dictionary<string, object>;
  public static Dictionary<string, object> D(object o, string k) => (o as Dictionary<string, object>)?.GetValueOrDefault(k) as Dictionary<string, object>;
  public static List<object> L(object o, string k) => (o as Dictionary<string, object>)?.GetValueOrDefault(k) as List<object>;
  public static double N(object o, string k) { var v = (o as Dictionary<string, object>)?.GetValueOrDefault(k); return v == null ? 0 : v is bool bb ? (bb ? 1 : 0) : Convert.ToDouble(v, IC); }
  public static string S(object o, string k) => (o as Dictionary<string, object>)?.GetValueOrDefault(k) as string;
  public static V3 V(object o) { var d = o as Dictionary<string, object>; return d == null ? default : new V3(N(d, "x"), N(d, "y"), N(d, "z")); }
  public static double[] Col(object o) { var d = o as Dictionary<string, object>; return d == null ? null : new[] { N(d, "r"), N(d, "g"), N(d, "b"), N(d, "a") }; }
}

/// One Unity mesh: positions, per-submesh triangles, vertex colours, bounds.
sealed class MeshGeo {
  public string Name; public V3[] V; public List<int[]> Subs = new(); public double[] Cols, UV; public V3[] Nrm; public V3 C, E; public int VertexCount;
  public List<(V3 c, V3 e)> SubBox = new(); public List<int> SubIndexCount = new(); public string Error;

  static int FmtSize(int f) => f switch { 0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 2, 5 => 2, 6 => 1, 7 => 1, 8 => 2, 9 => 2, 10 => 4, 11 => 4, _ => 4 };
  static readonly Dictionary<(SFile, long), MeshGeo> Cache = new();

  /// Reads a mesh (geometry = false: name, bounds and counts only).
  public static MeshGeo Of(SFile f, SObj o, bool geometry = true, Func<string, long, long, byte[]> stream = null) {
    if (f == null || o == null) return null;
    if (geometry && Cache.TryGetValue((f, o.PathId), out var c0)) return c0;
    var m = new MeshGeo();
    try {
      var d = Rc.Read(f, o); m.Name = Rc.S(d, "m_Name");
      var ab = Rc.D(d, "m_LocalAABB"); m.C = Rc.V(ab?["m_Center"]); m.E = Rc.V(ab?["m_Extent"]);
      var vd = Rc.D(d, "m_VertexData"); int n = (int)Rc.N(vd, "m_VertexCount"); m.VertexCount = n;
      foreach (var sx in Rc.L(d, "m_SubMeshes")) { var la = Rc.D(sx, "localAABB"); m.SubBox.Add((Rc.V(la?["m_Center"]), Rc.V(la?["m_Extent"]))); m.SubIndexCount.Add((int)Rc.N(sx, "indexCount")); }
      if (!geometry) return m;
      var chans = Rc.L(vd, "m_Channels").Select(x => (s: (int)Rc.N(x, "stream"), o: (int)Rc.N(x, "offset"), fmt: (int)Rc.N(x, "format"), dim: (int)Rc.N(x, "dimension") & 15)).ToList();
      byte[] data = vd.GetValueOrDefault("m_DataSize") as byte[];
      if (data == null || data.Length == 0) {
        var sd = Rc.D(d, "m_StreamData"); long size = (long)Rc.N(sd, "size");
        if (size > 0) data = (stream ?? Rc.Stream)(Rc.S(sd, "path"), (long)Rc.N(sd, "offset"), size);
      }
      var cm = Rc.D(Rc.D(d, "m_CompressedMesh"), "m_Vertices");
      if ((data == null || data.Length == 0) && Rc.N(cm, "m_NumItems") > 0) { m.Error = "compressed mesh"; return Done(f, o, m); }
      if (data == null || n == 0 || chans.Count == 0 || chans[0].dim < 3) { m.Error = "no vertex data"; return Done(f, o, m); }
      int maxS = chans.Where(x => x.dim > 0).Max(x => x.s);
      var sOff = new int[maxS + 1]; var sStride = new int[maxS + 1]; int at = 0;
      for (int s = 0; s <= maxS; s++) { sOff[s] = at; sStride[s] = chans.Where(x => x.s == s && x.dim > 0).Select(x => x.o + FmtSize(x.fmt) * x.dim).DefaultIfEmpty(0).Max(); at += sStride[s] * n; at = (at + 15) & ~15; }
      var p = chans[0]; m.V = new V3[n];
      for (int i = 0; i < n; i++) {
        int b = sOff[p.s] + i * sStride[p.s] + p.o;
        if (p.fmt == 0) m.V[i] = new V3(BitConverter.ToSingle(data, b), BitConverter.ToSingle(data, b + 4), BitConverter.ToSingle(data, b + 8));
        else if (p.fmt == 1) m.V[i] = new V3((double)BitConverter.ToHalf(data, b), (double)BitConverter.ToHalf(data, b + 2), (double)BitConverter.ToHalf(data, b + 4));
        else { m.Error = "position format " + p.fmt; m.V = null; return Done(f, o, m); }
      }
      if (chans.Count > 1 && chans[1].dim >= 3 && chans[1].fmt <= 1) {
        var nc = chans[1]; m.Nrm = new V3[n];
        for (int i = 0; i < n; i++) { int b = sOff[nc.s] + i * sStride[nc.s] + nc.o; m.Nrm[i] = nc.fmt == 0 ? new V3(BitConverter.ToSingle(data, b), BitConverter.ToSingle(data, b + 4), BitConverter.ToSingle(data, b + 8)) : new V3((double)BitConverter.ToHalf(data, b), (double)BitConverter.ToHalf(data, b + 2), (double)BitConverter.ToHalf(data, b + 4)); }
      }
      if (chans.Count > 3 && chans[3].dim >= 3) {
        var cc = chans[3]; var col = new double[n * 3]; bool ok = true;
        for (int i = 0; i < n && ok; i++) {
          int b = sOff[cc.s] + i * sStride[cc.s] + cc.o;
          if (cc.fmt == 2) { col[i * 3] = data[b] / 255.0; col[i * 3 + 1] = data[b + 1] / 255.0; col[i * 3 + 2] = data[b + 2] / 255.0; }
          else if (cc.fmt == 0) { col[i * 3] = BitConverter.ToSingle(data, b); col[i * 3 + 1] = BitConverter.ToSingle(data, b + 4); col[i * 3 + 2] = BitConverter.ToSingle(data, b + 8); }
          else if (cc.fmt == 1) { col[i * 3] = (double)BitConverter.ToHalf(data, b); col[i * 3 + 1] = (double)BitConverter.ToHalf(data, b + 2); col[i * 3 + 2] = (double)BitConverter.ToHalf(data, b + 4); }
          else ok = false;
        }
        if (ok) m.Cols = col;
      }
      // the first UV channel (2026-11-23 round 2: per-piece sampled colours sa=/sg= and texture frames ua=/rep= are measured
      // under each piece's own UV triangles; the UVs stay in memory, only the measured numbers are written)
      if (chans.Count > 4 && chans[4].dim >= 2) {
        var uc = chans[4]; var uv = new double[n * 2]; int fs = FmtSize(uc.fmt); bool ok = true;
        for (int i = 0; i < n && ok; i++) for (int k = 0; k < 2; k++) {
          int b = sOff[uc.s] + i * sStride[uc.s] + uc.o + k * fs;
          uv[i * 2 + k] = uc.fmt switch { 0 => BitConverter.ToSingle(data, b), 1 => (double)BitConverter.ToHalf(data, b), 2 => data[b] / 255.0, 4 => BitConverter.ToUInt16(data, b) / 65535.0, _ => double.NaN };
          if (double.IsNaN(uv[i * 2 + k])) ok = false;
        }
        if (ok) m.UV = uv;
      }
      var ib = d.GetValueOrDefault("m_IndexBuffer") as byte[]; bool i32 = Rc.N(d, "m_IndexFormat") == 1;
      foreach (var sx in Rc.L(d, "m_SubMeshes")) {
        var tl = new List<int>();
        if (Rc.N(sx, "topology") == 0 && ib != null) {
          int fb = (int)Rc.N(sx, "firstByte"), ic = (int)Rc.N(sx, "indexCount"), bv = (int)Rc.N(sx, "baseVertex");
          for (int k = 0; k < ic; k++) tl.Add((i32 ? BitConverter.ToInt32(ib, fb + k * 4) : BitConverter.ToUInt16(ib, fb + k * 2)) + bv);
        }
        m.Subs.Add(tl.ToArray());
      }
    } catch (Exception e) { m.Error = e.GetType().Name + ": " + e.Message; m.V = null; }
    return Done(f, o, m);
  }
  static MeshGeo Done(SFile f, SObj o, MeshGeo m) { Cache[(f, o.PathId)] = m; return m; }

  /// Unity's built-in meshes (external "unity default resources": 10210 Quad facing -z, 10202 Cube, 10209 Plane 10 x 10
  /// facing +y), made from their known shapes; null for anything else.
  public static MeshGeo Builtin(SFile from, Dictionary<string, object> pptr) {
    if (pptr == null) return null;
    int fid = Convert.ToInt32(pptr["m_FileID"]); long pid = Convert.ToInt64(pptr["m_PathID"]);
    if (fid <= 0 || fid - 1 >= from.Externals.Count || !from.Externals[fid - 1].Contains("unity default resources")) return null;
    var m = new MeshGeo { Name = pid == 10210 ? "Quad" : pid == 10202 ? "Cube" : pid == 10209 ? "Plane" : "builtin" + pid };
    var v = new List<V3>(); var t = new List<int>();
    void Face(V3 c, V3 u, V3 w) { int b = v.Count; v.Add(c - u - w); v.Add(c + u - w); v.Add(c + u + w); v.Add(c - u + w); t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 }); }
    V3 X = new(1, 0, 0), Yv = new(0, 1, 0), Z = new(0, 0, 1);
    // each face's u x w = its outward normal (Plates take the normal as cross(b - a, c - a))
    if (pid == 10210) Face(new V3(0, 0, 0), Yv * 0.5, X * 0.5);
    else if (pid == 10209) Face(new V3(0, 0, 0), Z * 5, X * 5);
    else if (pid == 10202) {
      Face(X * 0.5, Yv * 0.5, Z * 0.5); Face(X * -0.5, Z * 0.5, Yv * 0.5); Face(Yv * 0.5, Z * 0.5, X * 0.5);
      Face(Yv * -0.5, X * 0.5, Z * 0.5); Face(Z * 0.5, X * 0.5, Yv * 0.5); Face(Z * -0.5, Yv * 0.5, X * 0.5);
    } else { m.Error = "built-in mesh " + pid; return m; }
    m.V = v.ToArray(); m.VertexCount = m.V.Length; m.Subs.Add(t.ToArray());
    m.UV = new double[m.V.Length * 2]; for (int i = 0; i < m.V.Length; i++) { int q = i & 3; m.UV[i * 2] = q == 1 || q == 2 ? 1 : 0; m.UV[i * 2 + 1] = q >= 2 ? 1 : 0; } m.SubIndexCount.Add(t.Count); m.SubBox.Add((default, default));
    return m;
  }
}

/// The PC install's mesh and material names (DevNotes/rrtoolidx objects.tsv) and its meshes' counts and bounds.
static class Inst {
  public static readonly HashSet<string> Mats = new(StringComparer.Ordinal), Texs = new(StringComparer.Ordinal);
  static readonly Dictionary<string, List<(string bundle, long pid)>> Meshes = new(StringComparer.Ordinal);
  static readonly Dictionary<string, (byte[] data, List<(long off, long size, string name)> nodes)> Opened = new();
  static readonly Dictionary<string, SFile> Files = new();
  static readonly Dictionary<string, List<MeshGeo>> Info = new();
  public static void Load() {
    foreach (var l in File.ReadLines(Path.Combine(Rc.IdxDir, "objects.tsv"))) {
      var p = l.Split('\t'); if (p.Length < 4) continue;
      if (p[2] == "Material") Mats.Add(p[3]);
      else if (p[2] == "Texture2D") Texs.Add(p[3]);
      else if (p[2] == "Mesh" && long.TryParse(p[1], out var pid)) { if (!Meshes.TryGetValue(p[3], out var li)) Meshes[p[3]] = li = new(); li.Add((p[0], pid)); }
    }
  }
  public static bool HasMesh(string n) => n != null && Meshes.ContainsKey(n);
  /// Every install mesh of that name (name, bounds, vertex and index counts).
  public static List<MeshGeo> Of(string name) {
    if (Info.TryGetValue(name, out var r)) return r;
    r = new List<MeshGeo>();
    if (Meshes.TryGetValue(name, out var li)) foreach (var (b, pid) in li.Take(8)) {
      try {
        if (!Files.TryGetValue(b, out var f)) {
          var bo = Bundle.Open(Path.Combine(Rc.InstallDir, b + ".bundle")); Opened[b] = bo;
          var n = bo.nodes.First(x => !x.name.EndsWith(".resS") && !x.name.EndsWith(".resource"));
          f = Ser.Parse(n.name, bo.data, (int)n.off); Files[b] = f;
        }
        var o = f.Objects.FirstOrDefault(x => x.PathId == pid); var mg = MeshGeo.Of(f, o, false); if (mg != null) r.Add(mg);
      } catch { }
    }
    Info[name] = r; return r;
  }
}

/// A scene GameObject with its world matrix (row-major 4x4) and components.
sealed class Node {
  public string Path, Name; public int Layer; public bool Active; public double[] M; public List<SObj> Comps = new(); public long Go; public Node Parent;
  /// The world box of everything this object and its children draw (Lo.x > Hi.x: nothing).
  public V3 Lo = new(1e9, 1e9, 1e9), Hi = new(-1e9, -1e9, -1e9);
  public SObj Comp(int cls) => Comps.FirstOrDefault(c => c.ClassId == cls);
}

static class RcConvert {
  static string F3(double v) => Y.F3(v);
  static string P3(V3 v) => $"{Y.F(v.x)} {Y.F(v.y)} {Y.F(v.z)}";
  static double[] TRS(float[] p, float[] q, float[] s) {
    double x = q[0], y = q[1], z = q[2], w = q[3];
    double[] r = { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) };
    return new double[] { r[0] * s[0], r[1] * s[1], r[2] * s[2], p[0], r[3] * s[0], r[4] * s[1], r[5] * s[2], p[1], r[6] * s[0], r[7] * s[1], r[8] * s[2], p[2], 0, 0, 0, 1 };
  }
  static double[] Mul(double[] a, double[] b) { var m = new double[16]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { double s = 0; for (int k = 0; k < 4; k++) s += a[i * 4 + k] * b[k * 4 + j]; m[i * 4 + j] = s; } return m; }
  static V3 P(double[] m, V3 v) => new(m[0] * v.x + m[1] * v.y + m[2] * v.z + m[3], m[4] * v.x + m[5] * v.y + m[6] * v.z + m[7], m[8] * v.x + m[9] * v.y + m[10] * v.z + m[11]);
  static V3 Dir(double[] m, V3 v) => new(m[0] * v.x + m[1] * v.y + m[2] * v.z, m[4] * v.x + m[5] * v.y + m[6] * v.z, m[8] * v.x + m[9] * v.y + m[10] * v.z);
  static V3 Cross(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  static double Det(double[] m) => m[0] * (m[5] * m[10] - m[6] * m[9]) - m[1] * (m[4] * m[10] - m[6] * m[8]) + m[2] * (m[4] * m[9] - m[5] * m[8]);
  /// World position, rotation and lossy scale (a mirrored matrix: x negative).
  static (V3 p, Q r, V3 s) Decompose(double[] m) {
    var cx = new V3(m[0], m[4], m[8]); var cy = new V3(m[1], m[5], m[9]); var cz = new V3(m[2], m[6], m[10]);
    double sx = cx.Len(), sy = cy.Len(), sz = cz.Len(); if (Det(m) < 0) sx = -sx;
    var x = cx * (1 / (sx == 0 ? 1 : sx)); var y = cy * (1 / (sy == 0 ? 1 : sy)); var z = cz * (1 / (sz == 0 ? 1 : sz));
    return (new V3(m[3], m[7], m[11]), Plates.Basis(x, y, z), new V3(sx, sy, sz));
  }

  static List<Node> _nodes;
  public static List<Node> NodeList() => Nodes();
  static List<Node> Nodes() {
    if (_nodes != null) return _nodes;
    var f = Rc.Scene; var byId = f.Objects.ToDictionary(o => o.PathId); var res = new List<Node>();
    void Walk(long trId, double[] parent, Node par, string path, bool act) {
      var t = Raw.Tr(f, byId[trId]); var g = Raw.Go(f, byId[t.go]);
      var n = new Node { Name = g.name, Layer = g.layer, Go = t.go, Parent = par, M = Mul(parent, TRS(t.pos, t.rot, t.scl)), Active = act && g.active };
      n.Path = path.Length == 0 ? g.name : path + "/" + g.name;
      foreach (var c in g.comps) if (byId.TryGetValue(c, out var co)) n.Comps.Add(co);
      res.Add(n);
      foreach (var k in t.kids) if (byId.ContainsKey(k)) Walk(k, n.M, n, n.Path, n.Active);
    }
    var id = new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
    foreach (var o in f.Objects.Where(o => o.ClassId == 4 || o.ClassId == 224)) { var t = Raw.Tr(f, o); if (t.father == 0) Walk(o.PathId, id, null, "", true); }
    return _nodes = res;
  }

  public static void DumpNodes(string re) {
    var rx = new Regex(re, RegexOptions.IgnoreCase);
    foreach (var n in Nodes()) {
      if (!rx.IsMatch(n.Path)) continue;
      Console.WriteLine($"{n.Path}\t{(n.Active ? 1 : 0)}\t{n.Layer}\t{F3(n.M[3])},{F3(n.M[7])},{F3(n.M[11])}\t{string.Join(",", n.Comps.Select(c => ClassNames.Of(c.ClassId)))}");
    }
  }

  // ------------------------------------------------------------------ materials

  sealed class Mat {
    public string Name, Shader, Tex = "-", TexFormat = ""; public double[] Col = { 1, 1, 1, 1 }, Glow, Mean; public double Su = 1, Sv = 1, Coverage = 1; public bool Two, Clear, Inst, Cutout; public int Queue; public double Cut = 0.5;
    /// <summary>avg=: the texture mean x the colour (sRGB, as the platemats of gtscene write it); the colour alone without a texture mean.</summary>
    public double[] Avg => Mean != null ? new[] { Math.Clamp(Mean[0] * Col[0], 0, 1), Math.Clamp(Mean[1] * Col[1], 0, 1), Math.Clamp(Mean[2] * Col[2], 0, 1) } : new[] { Math.Clamp(Col[0], 0, 1), Math.Clamp(Col[1], 0, 1), Math.Clamp(Col[2], 0, 1) };
    public bool ITex => Tex != "-" && global::Inst.Texs.Contains(Tex);
  }
  static readonly Dictionary<(SFile, long), Mat> MatCache = new();
  /// The Studio shader bundle's shaders by pathId (DevNotes/iter6/cachecenter.md: the missing CAB's pathIds = the install's stock shaders).
  static readonly Dictionary<long, string> ShaderNames = new() {
    [-4055751637662985151] = "Universal Render Pipeline/Lit", [-261421426886131751] = "Universal Render Pipeline/Unlit",
    [-3885793140065008626] = "Universal Render Pipeline/Terrain/Lit", [683148379888368288] = "TextMeshPro/Distance Field",
    [7030032868776217633] = "TextMeshPro/Mobile/Distance Field", [8918850207803687531] = "Particles (Studio)", [-6364362845305036051] = "Nature/Leaf (Studio tree)" };

  static Mat MatOf(SFile f, SObj o) {
    if (o == null) return null;
    if (MatCache.TryGetValue((f, o.PathId), out var m)) return m;
    m = new Mat();
    try {
      var d = Rc.Read(f, o); m.Name = Rc.S(d, "m_Name") ?? "?"; m.Inst = Inst.Mats.Contains(m.Name);
      m.Queue = (int)Rc.N(d, "m_CustomRenderQueue");
      { var shp = Rc.D(d, "m_Shader"); long sid = shp == null ? 0 : Convert.ToInt64(shp["m_PathID"]); m.Shader = ShaderNames.GetValueOrDefault(sid, "?" + sid); }
      var sp = Rc.D(d, "m_SavedProperties");
      var cols = new Dictionary<string, double[]>(); foreach (var x in Rc.L(sp, "m_Colors") ?? new()) cols[Rc.S(x, "first") ?? ""] = Rc.Col(Rc.D(x, "second") ?? (x as Dictionary<string, object>)?["second"] as Dictionary<string, object>);
      var fl = new Dictionary<string, double>(); foreach (var x in Rc.L(sp, "m_Floats") ?? new()) fl[Rc.S(x, "first") ?? ""] = Rc.N(x, "second");
      m.Col = cols.GetValueOrDefault("_BaseColor") ?? cols.GetValueOrDefault("_Color") ?? new double[] { 1, 1, 1, 1 };
      string kw = d.GetValueOrDefault("m_ValidKeywords") is List<object> vk ? string.Join(" ", vk) : d.GetValueOrDefault("m_ShaderKeywords") as string ?? "";
      bool emissive = kw.Contains("_EMISSION") || (fl.GetValueOrDefault("_EmissionEnabled") > 0);
      var ec = cols.GetValueOrDefault("_EmissionColor");
      if (emissive && ec != null && ec[0] + ec[1] + ec[2] > 0.02) m.Glow = ec;
      m.Two = fl.TryGetValue("_Cull", out var cull) && cull == 0;
      if (fl.TryGetValue("_Cutoff", out var cutv)) m.Cut = cutv;
      m.Clear = fl.GetValueOrDefault("_Surface") == 1 || m.Queue >= 2900 || kw.Contains("_SURFACE_TYPE_TRANSPARENT");
      foreach (var x in Rc.L(sp, "m_TexEnvs") ?? new()) {
        var key = Rc.S(x, "first"); if (key != "_BaseMap" && key != "_MainTex") continue;
        var sec = Rc.D(x, "second"); var (tf, to) = Rc.Ptr(f, sec?["m_Texture"]); if (to == null) continue;
        m.Tex = Raw.Name(tf, to); var sc = Rc.D(sec, "m_Scale"); m.Su = Rc.N(sc, "x"); m.Sv = Rc.N(sc, "y");
        bool cut = fl.GetValueOrDefault("_AlphaClip") > 0 || m.Clear || Regex.IsMatch(m.Name, "Tree|Bush|Leaf|Foliage", RegexOptions.IgnoreCase); m.Mean = TexMean(tf, to, out m.TexFormat, cut, out m.Coverage); m.Cutout = fl.GetValueOrDefault("_AlphaClip") > 0; break;
      }
      // the glow as it shows on average: _EmissionColor x the emission map's mean (the marquee's bulbs, the poster cases' rims)
      if (m.Glow != null) foreach (var x in Rc.L(sp, "m_TexEnvs") ?? new()) {
        if (Rc.S(x, "first") != "_EmissionMap") continue;
        var (tf, to) = Rc.Ptr(f, Rc.D(x, "second")?["m_Texture"]); if (to == null) break;
        var em = TexMean(tf, to, out _, false, out _); if (em != null) m.Glow = new[] { m.Glow[0] * em[0], m.Glow[1] * em[1], m.Glow[2] * em[2], 1 };
        break;
      }
    } catch (Exception e) { m.Name ??= "?err"; Console.Error.WriteLine("material " + o.PathId + ": " + e.Message); }
    MatCache[(f, o.PathId)] = m; return m;
  }

  /// The mean colour (gamma, as the texture stores it) of a texture's smallest mip of at least 4 x 4 texels, for the
  /// formats Rec Room's Studio bundles use (uncompressed, DXT1/BC1, DXT5/BC3, BC7 mode-agnostic via its endpoints is
  /// not attempted: null).
  static double[] TexMean(SFile f, SObj o, out string fmt, bool weighted, out double coverage) {
    fmt = "?"; coverage = 1;
    try {
      var d = Rc.Read(f, o); int w = (int)Rc.N(d, "m_Width"), h = (int)Rc.N(d, "m_Height"), tf = (int)Rc.N(d, "m_TextureFormat"), mips = Math.Max(1, (int)Rc.N(d, "m_MipCount"));
      fmt = tf + ":" + w + "x" + h + "m" + mips;
      byte[] img = d.GetValueOrDefault("image data") as byte[];
      if (img == null || img.Length == 0) { var sd = Rc.D(d, "m_StreamData"); long size = (long)Rc.N(sd, "size"); if (size > 0) img = Rc.Stream(Rc.S(sd, "path"), (long)Rc.N(sd, "offset"), size); }
      if (img == null) return null;
      int bpp = tf switch { 1 => 1, 3 => 3, 4 => 4, 5 => 4, 14 => 4, _ => 0 };
      bool bc = tf == 10 || tf == 12; if (bpp == 0 && !bc) return null;
      // walk the mips to the smallest one that still has >= 16 x 16 texels (a 2 m wall texture's mean, not one texel)
      long off = 0; int mw = w, mh = h;
      for (int k = 0; k < mips; k++) {
        long size = bc ? (long)Math.Max(1, (mw + 3) / 4) * Math.Max(1, (mh + 3) / 4) * (tf == 10 ? 8 : 16) : (long)mw * mh * bpp;
        if (k == mips - 1 || Math.Max(1, mw / 2) < 16 || Math.Max(1, mh / 2) < 16 || off + size > img.Length) {
          if (off + size > img.Length) return null;
          double r = 0, g = 0, b = 0, a = 0, wr = 0, wg = 0, wb = 0, ws = 0; int n = 0;
          var al = new double[16];
          if (bc) {
            int bw = Math.Max(1, (mw + 3) / 4), bh = Math.Max(1, (mh + 3) / 4), bs = tf == 10 ? 8 : 16;
            for (int i = 0; i < bw * bh; i++) {
              int p0 = (int)off + i * bs, p = p0 + (tf == 12 ? 8 : 0);
              ushort c0 = BitConverter.ToUInt16(img, p), c1 = BitConverter.ToUInt16(img, p + 2); uint bits = BitConverter.ToUInt32(img, p + 4);
              var pal = new double[4][]; pal[0] = C565(c0); pal[1] = C565(c1); bool punch = false;
              if (c0 > c1 || tf == 12) { pal[2] = Lerp(pal[0], pal[1], 1 / 3.0); pal[3] = Lerp(pal[0], pal[1], 2 / 3.0); } else { pal[2] = Lerp(pal[0], pal[1], 0.5); pal[3] = new double[] { 0, 0, 0 }; punch = true; }
              // DXT5 alpha block: two endpoints and 16 3-bit indices; DXT1 3-colour mode: index 3 is transparent
              if (tf == 12) {
                double a0 = img[p0] / 255.0, a1 = img[p0 + 1] / 255.0; ulong ab = 0; for (int q = 0; q < 6; q++) ab |= (ulong)img[p0 + 2 + q] << (8 * q);
                var ap = new double[8]; ap[0] = a0; ap[1] = a1;
                if (a0 > a1) for (int q = 1; q < 7; q++) ap[q + 1] = ((7 - q) * a0 + q * a1) / 7; else { for (int q = 1; q < 5; q++) ap[q + 1] = ((5 - q) * a0 + q * a1) / 5; ap[6] = 0; ap[7] = 1; }
                for (int t = 0; t < 16; t++) al[t] = ap[(int)((ab >> (3 * t)) & 7)];
              } else for (int t = 0; t < 16; t++) al[t] = punch && ((bits >> (t * 2)) & 3) == 3 ? 0 : 1;
              for (int t = 0; t < 16; t++) { var c = pal[(bits >> (t * 2)) & 3]; r += c[0]; g += c[1]; b += c[2]; wr += c[0] * al[t]; wg += c[1] * al[t]; wb += c[2] * al[t]; ws += al[t]; n++; }
            }
          } else {
            for (int i = 0; i < mw * mh; i++) {
              int p = (int)off + i * bpp; double cr = 1, cg = 1, cb = 1, ca = 1;
              if (tf == 1) { ca = img[p] / 255.0; }
              else if (tf == 3 || tf == 4) { cr = img[p] / 255.0; cg = img[p + 1] / 255.0; cb = img[p + 2] / 255.0; if (tf == 4) ca = img[p + 3] / 255.0; }
              else if (tf == 5) { ca = img[p] / 255.0; cr = img[p + 1] / 255.0; cg = img[p + 2] / 255.0; cb = img[p + 3] / 255.0; }
              else if (tf == 14) { cb = img[p] / 255.0; cg = img[p + 1] / 255.0; cr = img[p + 2] / 255.0; ca = img[p + 3] / 255.0; }
              r += cr; g += cg; b += cb; wr += cr * ca; wg += cg * ca; wb += cb * ca; ws += ca; n++;
            }
          }
          coverage = n == 0 ? 1 : ws / n;
          if (n == 0) return null;
          return weighted && ws > 0.02 * n ? new[] { wr / ws, wg / ws, wb / ws } : new[] { r / n, g / n, b / n };
        }
        off += size; mw = Math.Max(1, mw / 2); mh = Math.Max(1, mh / 2);
      }
    } catch { }
    return null;
  }
  static double[] C565(ushort c) => new[] { ((c >> 11) & 31) / 31.0, ((c >> 5) & 63) / 63.0, (c & 31) / 31.0 };
  static double[] Lerp(double[] a, double[] b, double t) => new[] { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t };

  /// texprofile <material re>: the seam lines of the materials' main textures (RcSample.Profile)
  public static void TexProfile(string re, bool pattern = false) {
    var rx = new Regex(re, RegexOptions.IgnoreCase); var seen = new HashSet<string>();
    foreach (var n in Nodes()) {
      var r = n.Comp(23); if (r == null) continue; var rd = Rc.Read(Rc.Scene, r);
      foreach (var mp in Rc.L(rd, "m_Materials") ?? new()) { var (mf, mo) = Rc.Ptr(Rc.Scene, mp); if (mo == null) continue; var ti = RcSample.Of(mf, mo); if (ti.Mat == null || !rx.IsMatch(ti.Mat) || !seen.Add(ti.Mat)) continue; if (pattern) RcSample.Pattern(ti); else RcSample.Profile(ti); }
    }
  }

  public static void DumpMats() {
    Inst.Load();
    foreach (var n in Nodes()) {
      var r = n.Comp(23); if (r == null) continue; var rd = Rc.Read(Rc.Scene, r);
      foreach (var mp in Rc.L(rd, "m_Materials") ?? new()) { var (mf, mo) = Rc.Ptr(Rc.Scene, mp); MatOf(mf, mo); }
    }
    foreach (var m in MatCache.Values.OrderBy(x => x.Name)) Console.WriteLine($"{m.Name}\tinst={(m.Inst ? 1 : 0)}\tcol={string.Join(",", m.Col.Select(F3))}\ttex={m.Tex} {m.TexFormat} x{F3(m.Su)},{F3(m.Sv)}\tmean={(m.Mean == null ? "-" : string.Join(",", m.Mean.Select(F3)))}\tglow={(m.Glow == null ? "-" : string.Join(",", m.Glow.Select(F3)))}\t{(m.Two ? "two " : "")}{(m.Clear ? "clear " : "")}q{m.Queue}");
  }

  // ------------------------------------------------------------------ convert

  const double MinSide = 0.01, Tail = 0.97, FoliageKeep = 0.75;
  const int FoliageMax = 300;
  static readonly Regex Foliage = new(@"Tree|Bush|Leaf|Foliage", RegexOptions.IgnoreCase);
  static readonly Regex SkipMatRe = new(@"LightShaft|Invisible|_Nav_|Shadow_?Blob|^Default-Particle|Cloud|Scuff|Flame|Decal", RegexOptions.IgnoreCase);
  // objects the room code replaces with its own (RrRecCenterRoom: our vending machine and Friend-O-Tron stand where the
  // two static vending machines were, our door signs at the doors): their
  // geometry and colliders are left out, their spots ("G spot", at their drawn centre) stay
  // (round 2: the community board CommunityOutpost/BulletinBoard is no longer replaced in poly=1: it is the install's
  // BulletinBoard mesh with DormRoom_BulletinBoard_mat, an R record; the room code no longer builds a board there)
  static readonly Regex ReplacedRe = new(@"(^|/)\[VendingMachine_Static\]|(^|/)\[Door_Sign\]", RegexOptions.IgnoreCase);
  // the iteration-16 build (convert without poly=1) still leaves the community board out, so it stays byte-identical
  static readonly Regex ReplacedLegacy = new(@"(^|/)\[VendingMachine_Static\]|CommunityOutpost/BulletinBoard|(^|/)\[Door_Sign\]", RegexOptions.IgnoreCase);
  static readonly Regex Outdoor = new(@"^(Trees|Terrain|MovementBlockers)|_backdrop|fence", RegexOptions.IgnoreCase);
  static readonly Regex SpotRe = new(@"^\[|^BulletinBoard", RegexOptions.IgnoreCase);
  // round 2 (2026-11-23): which materials get what (env overrides: RC_VEC, RC_FRAME)
  //   VecRe: a texture's colour regions vectorised into their own polygons (contract C3; alpha-test materials too)
  //   FrameRe: tiling level textures with their measured frame ua= / rep= (contract C4)
  //            (round 3: + RecCenter_Carpet_mat, the navy carpet the world draws from its measured star pattern)
  //   SaMat: per-piece sampled colour sa= (sg=) for atlases (the world's IsAtlasTexture rule) and level-only (itex=0)
  //          non-tiling textures (scale 1,1, no wood/brick/carpet/tile/... in the texture name)
  static readonly Regex VecRe = new(Environment.GetEnvironmentVariable("RC_VEC") ?? @"^(RecCenter_Marquee_Mat|Banner_Mat|Quest_Poster_Case_Mat)$", RegexOptions.IgnoreCase);
  static readonly Regex FrameRe = new(Environment.GetEnvironmentVariable("RC_FRAME") ?? @"^(Wood_Floor_Mat|RecCenter_Brick_Red|LockerRoom_FloorTile_mat|RecCenter_Carpet_Taupe_Mat|RecCenter_Carpet_mat|Fence_Brick_Mat)$", RegexOptions.IgnoreCase);
  static readonly Regex TilingKw = new(@"wood|brick|concrete|paint|carpet|tile|grass|rock|plaster|metal|glass", RegexOptions.IgnoreCase);
  /// The world's RrJumbotronHull.IsAtlasTexture, the same words.
  static bool IsAtlas(string tex) {
    if (string.IsNullOrEmpty(tex) || tex == "-") return false; var t = tex.ToLowerInvariant();
    if (t.Contains("tirestack")) return true;
    if (Regex.IsMatch(t, "wood|brick|concrete|paint|carpet|tile|grass|rock|plaster|metal")) return false;
    return Regex.IsMatch(t, "assets|atlas|props|arcade|bowling|cafe|crate|jailbars|porthole|vendingmachine|minifridge");
  }
  static bool SaMat(Mat m) => m != null && m.Tex != "-" && !FrameRe.IsMatch(m.Name) && !Foliage.IsMatch(m.Name)
    && (IsAtlas(m.Tex) || (!m.ITex && Math.Abs(m.Su - 1) < 1e-6 && Math.Abs(m.Sv - 1) < 1e-6 && !TilingKw.IsMatch(m.Tex)));
  /// round 2 (contract C6): RecCenter_Posters_Mat pieces carry their poster prefab's name (node=, the nearest "[...]"
  /// ancestor, e.g. "[RecCenter_Posters] (1)") and the drawn object's (part=, e.g. "Poster3")
  static string PosterTag(Node n, string mat) {
    if (mat != "RecCenter_Posters_Mat") return null;
    var a = n; while (a != null && !a.Name.StartsWith("[")) a = a.Parent;
    return $" node={Y.Enc((a ?? n).Name)} part={Y.Enc(n.Name)}";
  }

  static double Hue(double[] c) {
    double mx = c.Take(3).Max(), mn = c.Take(3).Min(), d = mx - mn; if (d < 1e-9) return 0;
    double h = mx == c[0] ? (c[1] - c[2]) / d % 6 : mx == c[1] ? (c[2] - c[0]) / d + 2 : (c[0] - c[1]) / d + 4; h *= 60; return h < 0 ? h + 360 : h;
  }

  /// round 2 (contract C1): the scene's RenderSettings as "E" records, the format and colour convention of apkscene's
  /// (colours as serialized, sRGB): fog, trilight ambient, the key light as the sun (RenderSettings.sun, else the brightest
  /// enabled directional light), the reflection intensity. No "E sky": the cache's skybox material is level-only.
  static List<string> EnvLines(List<Node> nodes) {
    var res = new List<string>(); var f = Rc.Scene; var rso = f.Objects.FirstOrDefault(o => o.ClassId == 104); if (rso == null) return res;
    var rs = Rc.Read(f, rso); string F(double v) => Y.F(v);
    double[] C(string k) => Rc.Col(rs.GetValueOrDefault(k)) ?? new double[] { 1, 1, 1, 1 };
    var fc = C("m_FogColor"); int fm = (int)Rc.N(rs, "m_FogMode");
    res.Add($"E fog {(Rc.N(rs, "m_Fog") != 0 ? fm switch { 1 => "linear", 2 => "exp", _ => "exp2" } : "off")} {F(fc[0])} {F(fc[1])} {F(fc[2])} {F(Rc.N(rs, "m_FogDensity"))} {F(Rc.N(rs, "m_LinearFogStart"))} {F(Rc.N(rs, "m_LinearFogEnd"))}");
    var sky = C("m_AmbientSkyColor"); var eq = C("m_AmbientEquatorColor"); var gr = C("m_AmbientGroundColor"); int am = (int)Rc.N(rs, "m_AmbientMode");
    res.Add($"E ambient {F(sky[0])} {F(sky[1])} {F(sky[2])} mode={(am switch { 0 => "skybox", 1 => "trilight", 3 => "flat", _ => am.ToString() })} intensity={F(Rc.N(rs, "m_AmbientIntensity"))} equator={F(eq[0])},{F(eq[1])},{F(eq[2])} ground={F(gr[0])},{F(gr[1])},{F(gr[2])}");
    // the key light
    Node key = null; double best = -1; long sunPid = 0;
    try { var sp = Rc.D(rs, "m_Sun"); if (sp != null && Convert.ToInt32(sp["m_FileID"]) == 0) sunPid = Convert.ToInt64(sp["m_PathID"]); } catch { }
    foreach (var n in nodes) {
      var lc = n.Comp(108); if (lc == null || !n.Active) continue; var ld = Rc.Read(f, lc);
      if (Rc.N(ld, "m_Enabled") == 0 || (int)Rc.N(ld, "m_Type") != 1) continue;
      if (sunPid != 0 && lc.PathId == sunPid) { key = n; break; }
      var c = Rc.Col(ld["m_Color"]); double lum = Rc.N(ld, "m_Intensity") * (0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]); if (lum > best) { best = lum; key = n; }
    }
    if (key != null) {
      var ld = Rc.Read(f, key.Comp(108)); var c = Rc.Col(ld["m_Color"]); var (p, q, _) = Decompose(key.M); var sh = Rc.D(ld, "m_Shadows"); int st = (int)Rc.N(sh, "m_Type");
      res.Add($"E sun {F(p.x)} {F(p.y)} {F(p.z)} {q} {F(c[0])} {F(c[1])} {F(c[2])} {F(Rc.N(ld, "m_Intensity"))} shadows={(st == 0 ? "none" : st == 1 ? "hard" : "soft")} strength={F(Rc.N(sh, "m_Strength"))} name={Y.Enc(key.Name)}");
    } else res.Add("E sun none");
    res.Add($"E refl intensity={F(Rc.N(rs, "m_ReflectionIntensity"))} mode={((int)Rc.N(rs, "m_DefaultReflectionMode") == 1 ? "custom" : "skybox")}");
    return res;
  }
  public static void Run(string outPath, string[] opts) {
    string statsPath = opts.FirstOrDefault(x => x.StartsWith("stats="))?.Substring(6);
    string name = opts.FirstOrDefault(x => x.StartsWith("name="))?.Substring(5) ?? "RecCenter_071825";
    // poly=1 (2026-11-23): the shell as clean planar polygons ("G poly", ../gtscene/GtPoly.cs), level-only trees as "G tree" markers,
    // the room data's spawned objects as "G spot" markers, the terrain lowered under the floors, gcol=, avg=/itex=, R records
    // with level-only materials ("?" T entries + K records). Frame: the scene's own = the Android level7 frame (see Frame below).
    bool poly = opts.Contains("poly=1"); var replRe = poly ? ReplacedRe : ReplacedLegacy;
    var polySrc = new Dictionary<string, Polys.Src>(); int polyPieces = 0, polyCurved = 0; double polyPieceA = 0; var polyMatA = new Dictionary<string, double>();
    var polyNot = new Regex(Environment.GetEnvironmentVariable("GTPOLY_SKIP") ?? "Tree|Bush|Leaf|Foliage|Palm|Mirror", RegexOptions.IgnoreCase);
    var treeLines = new List<string>(); int treeRenderers = 0; var signs = new List<(string t, V3 p)>(); var vcMats = new HashSet<string>();
    var Treed = new Regex(@"^Trees/");
    Inst.Load();
    var f = Rc.Scene; var nodes = Nodes(); var byGo = nodes.ToDictionary(n => n.Go);
    var stats = new List<string>(); var notes = new List<string>();
    // LOD1+ renderers (LODGroup m_LODs[k>0].renderers) are not drawn at the room's distances
    var lodSkip = new HashSet<long>();
    foreach (var n in nodes) {
      var lg = n.Comp(205); if (lg == null) continue; var lods = Rc.L(Rc.Read(f, lg), "m_LODs"); if (lods == null) continue;
      for (int k = 1; k < lods.Count; k++) foreach (var rr in Rc.L(lods[k], "renderers") ?? new()) { var pp = Rc.D(rr, "renderer"); if (pp != null) lodSkip.Add(Convert.ToInt64(pp["m_PathID"])); }
    }
    var meshT = new List<string>(); var meshIx = new Dictionary<string, int>(); var matT = new List<string>(); var matIx = new Dictionary<string, int>();
    int MIx(string s) { if (!meshIx.TryGetValue(s, out int i)) { i = meshT.Count; meshT.Add(s); meshIx[s] = i; } return i; }
    int TIx(string s) { if (!matIx.TryGetValue(s, out int i)) { i = matT.Count; matT.Add(s); matIx[s] = i; } return i; }
    var rLines = new List<string>(); var cLines = new List<string>(); var lLines = new List<string>(); var gLines = new List<string>(); var plates = new List<string>(); var dress = new List<string>();
    var mats = new Dictionary<string, Mat>(); var texOf = new Dictionary<string, TexInfo>();
    V3 lo = new(1e9, 1e9, 1e9), hi = new(-1e9, -1e9, -1e9);
    // B = the building (the light grid covers B): the trees, the grass backdrop, the fence and the terrain outside do not count
    bool outdoor = false;
    void Grow(V3 p) { if (outdoor) return; lo = V3.Min(lo, p); hi = V3.Max(hi, p); }
    int barN = 0, facetN = 0, studN = 0, sliverN = 0, saPieces = 0, framePieces = 0; double vecDropA = 0; var vecSt = new RcSample.VecStat(); var frameLog = new List<string>();
    int flippedBatch = 0, tmpN = 0, replacedN = 0, foliageN = 0, rN = 0, rSkippedOff = 0, rLod = 0, rInst = 0, rPlates = 0, plateN = 0, dressN = 0, curvedN = 0, approxN = 0, tailN = 0, colN = 0, colPlates = 0, trig = 0, skinned = 0, ps = 0, unread = 0;
    double plateArea = 0, curvedA = 0;
    var matArea = new Dictionary<string, double>();
    int[] Flip(int[] t) { var r = (int[])t.Clone(); for (int i = 0; i + 2 < r.Length; i += 3) (r[i + 1], r[i + 2]) = (r[i + 2], r[i + 1]); return r; }

    // round 2 (contract C5): per leaf material, the mean of its leaf triangles' own UV texels passing the alpha test x colour,
    // weighted by each triangle's world area x passing share (what the cards show; an atlas's bark and branch texels do not count)
    var leafAcc = new Dictionary<string, double[]>();
    void LeafSample(Mat m, TexInfo ti, MeshGeo mesh, int[] tl, Func<int, V3> W) {
      if (m == null || ti == null || !ti.Ok || mesh.UV == null) return;
      if (!leafAcc.TryGetValue(m.Name, out var acc)) leafAcc[m.Name] = acc = new double[4];
      for (int i = 0; i + 2 < tl.Length; i += 3) {
        var x = RcSample.TriMean(ti, mesh.UV, tl[i], tl[i + 1], tl[i + 2], out double ps); if (x == null) continue;
        var a = W(tl[i]); var e1 = W(tl[i + 1]) - a; var e2 = W(tl[i + 2]) - a; double ar = Cross(e1, e2).Len() * 0.5 * ps;
        acc[0] += x[0] * ar; acc[1] += x[1] * ar; acc[2] += x[2] * ar; acc[3] += ar;
      }
    }
    // ---- trees (poly=1): per tree root (Trees/<group>/<tree>), its LOD0 vertices: all (top) and the leaves' (not trunk/bark)
    var treeAll = new Dictionary<Node, List<V3>>(); var treeLeaf = new Dictionary<Node, List<V3>>(); var treeInfo = new Dictionary<Node, (string mesh, string mat)>();
    void TreeCollect(Node n, MeshGeo mesh, bool batched, int first, int count, List<Mat> rmats, List<TexInfo> rtex) {
      var root = n; while (root.Parent != null && root.Parent.Parent != null && root.Parent.Parent.Parent != null) root = root.Parent;
      if (!treeAll.TryGetValue(root, out var all)) { treeAll[root] = all = new List<V3>(); treeLeaf[root] = new List<V3>(); }
      var leaf = treeLeaf[root]; int s0 = batched ? first : 0, s1 = batched ? first + count : mesh.Subs.Count;
      for (int k = s0; k < s1 && k < mesh.Subs.Count; k++) {
        var m = rmats.Count == 0 ? null : rmats[Math.Min(k - s0, rmats.Count - 1)]; bool isLeaf = m != null && !Regex.IsMatch(m.Name, "Trunk|Bark", RegexOptions.IgnoreCase);
        foreach (int vi in mesh.Subs[k]) { var w = batched ? mesh.V[vi] : P(n.M, mesh.V[vi]); all.Add(w); if (isLeaf) leaf.Add(w); }
        if (isLeaf && !treeInfo.ContainsKey(root)) treeInfo[root] = (mesh.Name, m.Name);
        if (isLeaf) LeafSample(m, rtex.Count == 0 ? null : rtex[Math.Min(k - s0, rtex.Count - 1)], mesh, mesh.Subs[k], vi => batched ? mesh.V[vi] : P(n.M, mesh.V[vi]));
      }
    }
    void PolyAdd(string mat, V3[] wv, int[] pc, double[] cols, double area, double[] uv = null, TexInfo ti = null, string nodeTag = null) {
      polyPieces++; polyPieceA += area; polyMatA[mat] = polyMatA.GetValueOrDefault(mat) + area;
      foreach (int i in pc) Grow(wv[i]);
      var mm = mats[mat]; bool okT = poly && ti != null && ti.Ok && uv != null;
      // round 2 (contract C3): a texture's colour regions as their own polygons (the marquee, the quest rug, the poster cases)
      if (okT && (VecRe.IsMatch(mat) || (ti.Cutout && !Foliage.IsMatch(mat)))) {
        // each planar facet group of the piece (normal within 1 degree, 1 cm) of at least 0.02 m2 on its own; the rest
        // (curved bits, small facets) keeps per-triangle colours below
        var rest = new List<int>(); bool any = false;
        foreach (var (gt, ga) in RcSample.PlanarGroups(wv, pc)) {
          if (ga < 0.02) { rest.AddRange(gt); continue; }
          var r = RcSample.Vectorise(ti, wv, uv, gt, vecSt, out bool drop);
          if (Environment.GetEnvironmentVariable("RC_VECDBG") != null) Console.Error.WriteLine($"vec {mat} tris {gt.Length / 3} area {ga:F3} at {P3(wv[gt[0]])} -> {(drop ? "dropped" : r == null ? "per-triangle" : r.Value.t.Length / 3 + " region triangles")}");
          if (drop) { vecDropA += ga; any = true; continue; }
          if (r == null) { rest.AddRange(gt); continue; }
          var rv = r.Value; Polys.AddTagged(polySrc, mat, mm.Two, rv.w, rv.t, null, i => rv.sa[i], string.IsNullOrEmpty(nodeTag) ? null : nodeTag); any = true;
        }
        if (any) saPieces++;
        if (rest.Count == 0) return;
        pc = rest.ToArray();
      }
      double[][] per = null;
      if (okT && SaMat(mm)) { per = RcSample.PerTri(ti, wv, uv, pc, out _, out _); saPieces++; }
      if (poly && uv != null && ti != null && FrameRe.IsMatch(mat)) {
        // contract C4: each UV island's frame (ua=, rep=) on its own triangles
        framePieces++;
        foreach (var (gt, fr) in RcSample.FrameGroups(ti, wv, uv, pc)) {
          string ft = RcSample.FrameTag(fr) + (nodeTag ?? ""); double ga = 0;
          for (int i = 0; i + 2 < gt.Length; i += 3) ga += Cross(wv[gt[i + 1]] - wv[gt[i]], wv[gt[i + 2]] - wv[gt[i]]).Len() * 0.5;
          if (fr != null) frameLog.Add($"frame\t{mat}\t{F3(ga)}\t{ft.Trim()}\t{P3(wv[gt[0]])}");
          Polys.AddTagged(polySrc, mat, mm.Two, wv, gt, cols, null, ft.Length == 0 ? null : ft);
        }
        return;
      }
      string tag = nodeTag ?? "";
      Polys.AddTagged(polySrc, mat, mm.Two, wv, pc, cols, per == null ? null : i => per[i], tag.Length == 0 ? null : tag);
    }
    string PieceSa(string mat, V3[] wv, int[] pc, double[] uv, TexInfo ti) {
      if (!poly || ti == null || !ti.Ok || uv == null || !(SaMat(mats[mat]) || VecRe.IsMatch(mat))) return "";
      RcSample.PerTri(ti, wv, uv, pc, out var mean, out _); return RcSample.Sa(mean);
    }
    void EmitPlates(string mat, V3[] wv, int[] tris, double[] cols, string label, double[] uv = null, TexInfo ti = null, string nodeTag = null) {
      var js = new Plates.JtStat(); int before = plates.Count;
      // foliage (Rec Room's mobile trees and bushes: alpha-cut leaf cards, two-sided): the cards as fitted rectangles,
      // the biggest that make FoliageKeep of the tree's card area, at most FoliageMax per tree (seen through the windows)
      if (Foliage.IsMatch(mat)) {
        var ap = Plates.Approx(wv, tris, 0.05, cols).OrderByDescending(x => x.hx * x.hy).ToList();
        double tot = ap.Sum(x => x.hx * x.hy), acc = 0; int kept = 0;
        foreach (var re in ap) {
          if (kept >= FoliageMax || acc >= FoliageKeep * tot) break;
          acc += re.hx * re.hy; kept++;
          plates.Add($"G plate {Y.Enc(mat)} {P3(re.c)} {Plates.Basis(re.u, re.v, re.n)} e={F3(re.hx)},{F3(re.hy)}");
          plateN++; plateArea += re.hx * re.hy * 4; matArea[mat] = matArea.GetValueOrDefault(mat) + re.hx * re.hy * 4; Grow(re.c);
        }
        foliageN += ap.Count - kept; stats.Add($"foliage\t{label}\t{mat}\t{ap.Count}\t{kept}"); return;
      }
      foreach (var pc in Plates.Pieces(wv, tris)) {
        var pj = new Plates.JtStat(); var pr = Plates.OfJt(wv, pc, MinSide, cols, pj, true);
        double ext = Plates.Extent(wv, pc);
        if (pr.Count >= 8 && pj.ExactArea >= 0.7 * pj.TriArea && ext < 0.6) {
          var ob = Plates.Obb(wv, pc);
          if (ob.fill >= 0.7 && ob.fill <= 1.15 && Math.Min(ob.e.x, Math.Min(ob.e.y, ob.e.z)) >= 0.004) {
            var mc = mats[mat].Col; string cs = cols != null ? AvgCol(cols, pc) : null;
            dress.Add($"G dress {Y.Enc(label)}_box {P3(ob.c)} {Plates.Basis(ob.x, ob.y, ob.z)} e={F3(ob.e.x)},{F3(ob.e.y)},{F3(ob.e.z)} c={cs ?? "1,1,1,1"} m={Y.Enc(mat)}{PieceSa(mat, wv, pc, uv, ti)}"); dressN++; continue;
          }
        }
        if (pj.ExactArea < 0.7 * pj.TriArea) {
          if (ext < 0.6) {
            // round 2: the small curved bits of the measured-colour materials (the quest poster cases' bulb studs, the
            // marquee's bulbs) as one fitted "G dress ..._stud" box each with their sa=/sg=; others are left out as before
            if (poly && VecRe.IsMatch(mat) && ti != null && ti.Ok && uv != null) {
              var ob = Plates.Obb(wv, pc);
              if (Math.Min(ob.e.x, Math.Min(ob.e.y, ob.e.z)) >= 0.002) {
                string cs = cols != null ? AvgCol(cols, pc) : null;
                dress.Add($"G dress {Y.Enc(label)}_stud {P3(ob.c)} {Plates.Basis(ob.x, ob.y, ob.z)} e={F3(ob.e.x)},{F3(ob.e.y)},{F3(ob.e.z)} c={cs ?? "1,1,1,1"} m={Y.Enc(mat)}{PieceSa(mat, wv, pc, uv, ti)}"); dressN++; studN++; continue;
              }
            }
            curvedN++; curvedA += pj.TriArea; stats.Add($"curved\t{label}\t{mat}\t{pj.TriArea:F4}\t{P3(wv[pc[0]])}"); continue;
          }
          if (poly && !polyNot.IsMatch(mat) && Polys.PlanarShare(wv, pc) >= Polys.Share) { PolyAdd(mat, wv, pc, cols, pj.TriArea, uv, ti, nodeTag); polyCurved++; js.Add(pj); continue; }
          if (poly && !polyNot.IsMatch(mat)) {
            // round 2 (T7): a big curved piece is no longer one fitted rectangle per triangle pair. A bar (the tilted sash
            // bars, the locker-room fans: its box is long and thin and filled) -> one oriented "G dress" box fitted to it;
            // anything else (the balcony fascia and underside, curved floor trims) -> its facets as "G poly" (GtPoly groups
            // each planar facet group into its own polygon: exact, gap-free)
            var ob = Plates.Obb(wv, pc); var es = new[] { ob.e.x, ob.e.y, ob.e.z }.OrderBy(x => x).ToArray();
            if (es[2] >= 3 * es[1] && es[1] <= 0.12 && ob.fill >= 0.35 && ob.fill <= 1.2 && es[0] >= 0.002) {
              string cs = cols != null ? AvgCol(cols, pc) : null;
              dress.Add($"G dress {Y.Enc(label)}_box {P3(ob.c)} {Plates.Basis(ob.x, ob.y, ob.z)} e={F3(ob.e.x)},{F3(ob.e.y)},{F3(ob.e.z)} c={cs ?? "1,1,1,1"} m={Y.Enc(mat)}{PieceSa(mat, wv, pc, uv, ti)}"); dressN++; barN++; js.Add(pj); continue;
            }
            PolyAdd(mat, wv, pc, cols, pj.TriArea, uv, ti, nodeTag); facetN++; js.Add(pj); continue;
          }
          pr = Plates.Approx(wv, pc, 0.01, cols); approxN++;
        }
        else if (poly && !polyNot.IsMatch(mat)) { PolyAdd(mat, wv, pc, cols, pj.TriArea, uv, ti, nodeTag); js.Add(pj); continue; }
        if (pr.Count > 12 && pj.TriArea > 0 && pr.Count > 60 * pj.TriArea && ext < 1.6) {
          var srt = pr.OrderByDescending(x => x.hx * x.hy).ToList(); double tot = srt.Sum(x => x.hx * x.hy), acc = 0; var kept = new List<Plates.Rect>();
          foreach (var x in srt) { if (acc >= Tail * tot && kept.Count >= 6) break; kept.Add(x); acc += x.hx * x.hy; }
          tailN += pr.Count - kept.Count; pr = kept;
        }
        string psa = PieceSa(mat, wv, pc, uv, ti);
        foreach (var re in pr) {
          // round 2 (T7a): a fitted rectangle under 1 cm across is a sliver of a curved piece, not a surface
          if (poly && Math.Min(re.hx, re.hy) < 0.005) { sliverN++; continue; }
          string vc = re.hasCol && (re.cr < 0.97 || re.cg < 0.97 || re.cb < 0.97) ? $" c={F3(re.cr)},{F3(re.cg)},{F3(re.cb)}" : "";
          plates.Add($"G plate {Y.Enc(mat)} {P3(re.c)} {Plates.Basis(re.u, re.v, re.n)} e={F3(re.hx)},{F3(re.hy)}{vc}{psa}");
          plateN++; plateArea += re.hx * re.hy * 4; matArea[mat] = matArea.GetValueOrDefault(mat) + re.hx * re.hy * 4; Grow(re.c);
        }
        js.Add(pj);
      }
      stats.Add($"plates\t{label}\t{mat}\t{js.TriArea:F2}\t{plates.Count - before}");
    }

    foreach (var n in nodes) {
      // ---- renderers
      outdoor = Outdoor.IsMatch(n.Path);
      var rend = n.Comp(23); var filt = n.Comp(33);
      if (n.Comp(137) != null) skinned++;
      if (n.Comp(199) != null) ps++;
      if (rend != null && filt != null) {
        rN++;
        var rd = Rc.Read(f, rend); bool en = Rc.N(rd, "m_Enabled") != 0;
        if (!n.Active || !en) { rSkippedOff++; goto colliders; }
        if (lodSkip.Contains(rend.PathId)) { rLod++; goto colliders; }
        var mptr = Rc.L(rd, "m_Materials") ?? new(); var rmats = new List<Mat>(); var rtex = new List<TexInfo>();
        foreach (var mp in mptr) { var (mf, mo) = Rc.Ptr(f, mp); var m = MatOf(mf, mo) ?? new Mat { Name = "?" }; rmats.Add(m); if (!mats.ContainsKey(m.Name)) mats[m.Name] = m; var tinfo = poly && mo != null ? RcSample.Of(mf, mo) : null; rtex.Add(tinfo); if (tinfo != null && !texOf.ContainsKey(m.Name)) texOf[m.Name] = tinfo; }
        var sbi = Rc.D(rd, "m_StaticBatchInfo"); int first = (int)Rc.N(sbi, "firstSubMesh"), count = (int)Rc.N(sbi, "subMeshCount");
        var mref = Rc.D(Rc.Read(f, filt), "m_Mesh");
        var (mfile, mobj) = Rc.Ptr(f, mref);
        var mesh = MeshGeo.Of(mfile, mobj) ?? MeshGeo.Builtin(f, mref);
        if (mesh?.Cols != null && mesh.Cols.Any(x => x < 0.97)) foreach (var m in rmats) vcMats.Add(m.Name);
        if (mesh == null || mesh.V == null) {
          // TextMeshPro labels have no saved mesh (TMP builds it at runtime): their texts are the "G sign" records
          if (n.Comps.Any(c => c.ClassId == 114) && rmats.Any(m => m.Name.Contains("SDF"))) { tmpN++; goto colliders; }
          unread++; notes.Add($"unreadable mesh at {n.Path}: {mesh?.Name} {mesh?.Error}"); goto colliders;
        }
        bool batched = count > 0 && mesh.Name != null && mesh.Name.StartsWith("Combined Mesh");
        {
          // the drawn bounds, for the named spots above this renderer
          V3 blo = new(1e9, 1e9, 1e9), bhi = new(-1e9, -1e9, -1e9); int s0b = batched ? first : 0, s1b = batched ? first + count : mesh.Subs.Count;
          for (int k = s0b; k < s1b && k < mesh.Subs.Count; k++) foreach (int vi in mesh.Subs[k]) { var w = batched ? mesh.V[vi] : P(n.M, mesh.V[vi]); blo = V3.Min(blo, w); bhi = V3.Max(bhi, w); }
          if (bhi.x >= blo.x) for (var a = n; a != null; a = a.Parent) { if (a.Lo.x > a.Hi.x) { a.Lo = blo; a.Hi = bhi; } else { a.Lo = V3.Min(a.Lo, blo); a.Hi = V3.Max(a.Hi, bhi); } }
        }
        // poly=1: the level-only trees outside are "G tree" markers (measured below from their LOD0 leaves), not leaf-card plates
        if (poly && Treed.IsMatch(n.Path)) { treeRenderers++; TreeCollect(n, mesh, batched, first, count, rmats, rtex); goto colliders; }
        if (replRe.IsMatch(n.Path)) { replacedN++; goto colliders; }
        // install mesh? (non-batched: the mesh's own name; batched: the GameObject's name, checked on the batch's bounds)
        string instMesh = null;
        if (poly || rmats.All(m => m.Inst)) {
          var cands = new List<string>();
          if (!batched) cands.Add(mesh.Name);
          else {
            // the GameObject name, with " (N)" off; and (round 2) with the prefab brackets off: "[Light_Flourescent]" is the mesh Light_Flourescent
            var gn = Regex.Replace(n.Name, @" \(\d+\)$", ""); var gs = gn.Replace("[", "").Replace("]", "");
            foreach (var x in poly ? new[] { gn, gn + "_LOD0", gs, gs + "_LOD0" } : new[] { gn, gn + "_LOD0" }) if (!cands.Contains(x)) cands.Add(x);
          }
          foreach (var cn in cands) {
            if (!Inst.HasMesh(cn)) continue;
            var im = Inst.Of(cn); if (im.Count == 0) continue;
            bool ok = im.All(x => SameMesh(x, mesh, batched, first, count, n.M, poly ? cn : null));
            if (Environment.GetEnvironmentVariable("RC_DEBUG") is string dbg && cn.Contains(dbg))
              Console.Error.WriteLine($"match {n.Path} -> {cn}: batched {batched} sub {first}+{count}; scene v{mesh.VertexCount} c {mesh.C} e {mesh.E} idx {string.Join(",", mesh.SubIndexCount.Skip(batched ? first : 0).Take(batched ? count : 99))}; install " + string.Join(" | ", im.Select(x => $"v{x.VertexCount} c {x.C} e {x.E} idx {string.Join(",", x.SubIndexCount)}")) + $" -> {ok}");
            if (ok) { instMesh = cn; break; }
          }
        }
        if (instMesh != null) {
          var (p, q, s) = Decompose(n.M);
          var ts = string.Join(",", rmats.Select(m => TIx(m.Inst ? m.Name : "?" + m.Name)));
          rLines.Add($"R {MIx(instMesh)} {ts} {P3(p)} {q} {P3(s)}"); rInst++; Grow(p);
          stats.Add($"inst\t{n.Path}\t{instMesh}\t{string.Join("|", rmats.Select(m => m.Name))}");
          goto colliders;
        }
        rPlates++;
        V3[] wv = batched ? mesh.V : mesh.V.Select(v => P(n.M, v)).ToArray();
        bool flip = !batched && Det(n.M) < 0;
        int s0 = batched ? first : 0, s1 = batched ? first + count : mesh.Subs.Count;
        for (int k = s0; k < s1 && k < mesh.Subs.Count; k++) {
          var m = rmats.Count == 0 ? new Mat { Name = "?" } : rmats[Math.Min(k - s0, rmats.Count - 1)];
          if (SkipMatRe.IsMatch(m.Name)) { stats.Add($"skipmat\t{n.Path}\t{m.Name}"); continue; }
          var tris = flip ? Flip(mesh.Subs[k]) : mesh.Subs[k]; if (tris.Length < 3) continue;
          // a static batch keeps the source's winding even for a mirrored object (its vertex normals say which side is out):
          // the triangles whose winding most disagrees with their normals are turned
          if (batched && mesh.Nrm != null) {
            int agree = 0, against = 0;
            for (int i = 0; i + 2 < tris.Length; i += 3) {
              var a = wv[tris[i]]; var cr = Cross(wv[tris[i + 1]] - a, wv[tris[i + 2]] - a); var nn = mesh.Nrm[tris[i]] + mesh.Nrm[tris[i + 1]] + mesh.Nrm[tris[i + 2]];
              double d = cr.x * nn.x + cr.y * nn.y + cr.z * nn.z; if (d > 0) agree++; else if (d < 0) against++;
            }
            if (against > agree) { tris = Flip(tris); flippedBatch++; stats.Add($"flipped\t{n.Path}\t{m.Name}\t{agree}\t{against}"); }
          }
          var ti = rtex.Count == 0 ? null : rtex[Math.Min(k - s0, rtex.Count - 1)];
          EmitPlates(m.Name, wv, tris, mesh.Cols, Regex.Replace(n.Name, @"[\[\]\(\) ]", ""), mesh.UV, ti, PosterTag(n, m.Name));
        }
      }
    colliders:
      if (!n.Active) continue;
      bool replaced = replRe.IsMatch(n.Path);
      var (np, nq, ns) = Decompose(n.M);
      foreach (var c in n.Comps) {
        if (replaced || (c.ClassId != 65 && c.ClassId != 64 && c.ClassId != 135 && c.ClassId != 136)) continue;
        var cd = Rc.Read(f, c); if (Rc.N(cd, "m_Enabled") == 0) continue;
        if (Rc.N(cd, "m_IsTrigger") != 0) { trig++; continue; }
        colN++;
        var sa = ns.Abs();
        if (c.ClassId == 65) {
          var ctr = P(n.M, Rc.V(cd["m_Center"])); var sz = Rc.V(cd["m_Size"]).Mul(sa);
          cLines.Add($"C box {P3(ctr)} {nq} {F3(sz.x)} {F3(sz.y)} {F3(sz.z)}"); Grow(ctr);
        } else if (c.ClassId == 135) {
          var ctr = P(n.M, Rc.V(cd["m_Center"])); cLines.Add($"C sphere {P3(ctr)} {F3(Rc.N(cd, "m_Radius") * sa.Max())}");
        } else if (c.ClassId == 136) {
          var ctr = P(n.M, Rc.V(cd["m_Center"])); int dir = (int)Rc.N(cd, "m_Direction"); double r = Rc.N(cd, "m_Radius"), h = Rc.N(cd, "m_Height");
          // along local Y of the record's rotation: turn the node's x / z axis onto y
          Q q = nq; double rs, hs;
          if (dir == 0) { q = nq * new Q(0, 0, Math.Sqrt(0.5), Math.Sqrt(0.5)); hs = sa.x; rs = Math.Max(sa.y, sa.z); }
          else if (dir == 2) { q = nq * new Q(Math.Sqrt(0.5), 0, 0, Math.Sqrt(0.5)); hs = sa.z; rs = Math.Max(sa.x, sa.y); }
          else { hs = sa.y; rs = Math.Max(sa.x, sa.z); }
          cLines.Add($"C capsule {P3(ctr)} {q} {F3(r * rs)} {F3(h * hs)}");
        } else {
          var (cf, co) = Rc.Ptr(f, cd["m_Mesh"]); var cm = MeshGeo.Of(cf, co);
          if (cm == null || cm.V == null) { notes.Add($"collider mesh unreadable at {n.Path}: {cm?.Name} {cm?.Error}"); continue; }
          var wv = cm.V.Select(v => P(n.M, v)).ToArray(); var all = cm.Subs.SelectMany(x => x).ToArray();
          const double depth = 0.2;
          foreach (var re in Plates.Of(wv, all, 0.03)) {
            var cc = re.c - re.n * (depth * 0.5);
            cLines.Add($"C box {P3(cc)} {Plates.Basis(re.u, re.v, re.n)} {F3(re.hx * 2)} {F3(re.hy * 2)} {F3(depth)}"); colPlates++;
          }
        }
      }
      // ---- lights
      var lc = n.Comp(108);
      if (lc != null) {
        var ld = Rc.Read(f, lc);
        if (Rc.N(ld, "m_Enabled") != 0) {
          int type = (int)Rc.N(ld, "m_Type"); var col = Rc.Col(ld["m_Color"]); string kind = type == 0 ? "spot" : type == 1 ? "dir" : type == 2 ? "point" : "area";
          bool sh = Rc.N(Rc.D(ld, "m_Shadows"), "m_Type") > 0;
          lLines.Add($"L {kind} {P3(np)} {nq} {F3(col[0])} {F3(col[1])} {F3(col[2])} {F3(Rc.N(ld, "m_Intensity"))} {F3(Rc.N(ld, "m_Range"))} {F3(Rc.N(ld, "m_SpotAngle"))}{(sh ? " shadows=1" : "")} name={Y.Enc(n.Name)}");
        }
      }
      // ---- the door signs' texts and the named spots the room code looks up
      foreach (var c in n.Comps.Where(c => c.ClassId == 114)) {
        try {
          var md = Rc.Read(f, c); var txt = Rc.S(md, "m_text");
          if (!string.IsNullOrWhiteSpace(txt)) { gLines.Add($"G sign {Y.Enc(txt.Trim())} {P3(np)} {nq} path={Y.Enc(n.Path)}{(n.Active ? "" : " active=0")}"); if (n.Active) signs.Add((txt.Trim(), np)); }
        } catch { }
      }
    }
    // the named spots (prefab roots "[...]", the boards): at the centre of what they draw (RrRecCenterFit.Nearest and
    // Centre measure the cached building's objects by their renderers' bounds), with their own rotation; pivot= and size=
    // the drawn box
    foreach (var n in nodes) {
      if (!n.Active || !SpotRe.IsMatch(n.Name)) continue;
      if (n.Parent != null && n.Parent.Name.StartsWith(n.Name.Split(' ')[0], StringComparison.Ordinal) && SpotRe.IsMatch(n.Parent.Name)) continue;
      var (np, nq, ns) = Decompose(n.M); bool drawn = n.Hi.x >= n.Lo.x; var c = drawn ? (n.Lo + n.Hi) * 0.5 : np; var sz = drawn ? n.Hi - n.Lo : new V3(0, 0, 0);
      gLines.Add($"G spot {Y.Enc(n.Name)} {P3(c)} {nq} pivot={F3(np.x)},{F3(np.y)},{F3(np.z)} size={F3(sz.x)},{F3(sz.y)},{F3(sz.z)}{(replRe.IsMatch(n.Path) ? " replaced=1" : "")}");
    }

    // ---- poly=1: the shell's polygons (GtPoly.cs, gtscene's code unchanged)
    var pst = new Polys.Stat(); var polyLines = poly ? Polys.Build(polySrc, pst) : new List<string>();
    // the floors (up-facing polygons up to 1 m above the hall floor), for the terrain under the building
    var floorTris = new List<(V3 a, V3 b, V3 c)>();
    foreach (var l in polyLines) {
      var t = Polys.Triangles(l, out var pn, out _, out _, out _, out string pm); if (t == null || pn.y < 0.9 || Regex.IsMatch(pm ?? "", "Grass|Terrain|Ground|Water", RegexOptions.IgnoreCase)) continue;
      foreach (var x in t) if (Math.Max(x.a.y, Math.Max(x.b.y, x.c.y)) < 1.0) floorTris.Add(x);
    }

    // ---- poly=1: the trees as "G tree" markers (the world draws them itself, RrSceneKitTrees): base = the tree's root,
    // h = its top above the base, r = the 90th percentile of its leaves' horizontal distance, crown = the 5th percentile of
    // its leaves' height above the base (lowest leaves); kind by the prefab's name (oaks as broadleaf "aspen")
    // round 2 (contract C5): avg= the leaf texture's mean over the texels passing its alpha test x the colour
    var leafAvg = new Dictionary<string, string>();
    string TreeAvg(string mat) {
      if (leafAvg.TryGetValue(mat, out var s)) return s;
      texOf.TryGetValue(mat, out var lt);
      var a = leafAcc.TryGetValue(mat, out var acc) && acc[3] > 0 ? new[] { Math.Clamp(acc[0] / acc[3], 0, 1), Math.Clamp(acc[1] / acc[3], 0, 1), Math.Clamp(acc[2] / acc[3], 0, 1) } : lt != null ? RcSample.LeafAvg(lt) : null;
      if (lt != null && RcSample.LeafAvg(lt) is double[] whole) stats.Add($"leafavg-whole-texture\t{mat}\t{string.Join(",", whole.Select(x => x.ToString("0.###", Rc.IC)))}\thue={Hue(whole):F0}");
      s = a == null ? "" : $" avg={string.Join(",", a.Select(x => x.ToString("0.####", Rc.IC)))}";
      if (a != null) stats.Add($"leafavg\t{mat}\t{lt.TexName}\tcut={F3(lt.Cut)}\t{s.Trim()}\thue={Hue(a):F0}");
      return leafAvg[mat] = s;
    }
    foreach (var kv in treeAll.OrderBy(k => k.Key.Path, StringComparer.Ordinal)) {
      var root = kv.Key; var (rp, rq, _) = Decompose(root.M); var leaf = treeLeaf[root].Count > 0 ? treeLeaf[root] : kv.Value;
      double top = kv.Value.Max(v => v.y) - rp.y;
      var dist = leaf.Select(v => Math.Sqrt((v.x - rp.x) * (v.x - rp.x) + (v.z - rp.z) * (v.z - rp.z))).OrderBy(x => x).ToList();
      var hy = leaf.Select(v => v.y - rp.y).OrderBy(x => x).ToList();
      double r = dist[(int)(0.9 * (dist.Count - 1))], crown = Math.Max(0, hy[(int)(0.05 * (hy.Count - 1))]);
      string nm = root.Name; string kind = Regex.IsMatch(nm, "Pine", RegexOptions.IgnoreCase) ? "pine" : Regex.IsMatch(nm, "Bush", RegexOptions.IgnoreCase) ? "bush" : "aspen";
      var info = treeInfo.GetValueOrDefault(root, ("?", "?"));
      treeLines.Add($"G tree {kind} {F3(rp.x)} {F3(rp.y)} {F3(rp.z)} 0 0 0 1 h={top:F2} r={r:F2} crown={crown:F2} m={Y.Enc(info.Item2)} mesh={Y.Enc(info.Item1)} prefab={Y.Enc(Regex.Replace(nm, @" \(\d+\)$", ""))}" + TreeAvg(info.Item2));
    }

    // ---- poly=1: the room data's spawned objects (HTTPCache/182.data) as "G spot <kind>" markers, doors with their category
    var spotLines = poly ? RcRoomSpots.Lines(signs) : new List<string>();

    // ---- the terrain round the building (Unity Terrain, a 64 m square): a 2 m height grid, drawn and walkable
    var hLines = new List<string>(); string terrainNote = "";
    foreach (var n in nodes.Where(x => x.Active && x.Comp(218) != null)) {
      var tcd = Rc.Read(f, n.Comp(218)); var (df, dobj) = Rc.Ptr(f, tcd["m_TerrainData"]); var dd = Rc.Read(df, dobj); if (dd == null) continue;
      var hm = Rc.D(dd, "m_Heightmap"); int res = (int)Rc.N(hm, "m_Resolution"); var sc = Rc.V(hm["m_Scale"]); var hs = Rc.L(hm, "m_Heights");
      if (hs == null || res < 2) continue;
      double H(int x, int z) => Convert.ToDouble(hs[Math.Clamp(z, 0, res - 1) * res + Math.Clamp(x, 0, res - 1)], Rc.IC) / 32766.0 * sc.y;
      double Sample(double u, double v) { double fx = u / sc.x, fz = v / sc.z; int x0 = (int)Math.Floor(fx), z0 = (int)Math.Floor(fz); double tx = fx - x0, tz = fz - z0;
        return H(x0, z0) * (1 - tx) * (1 - tz) + H(x0 + 1, z0) * tx * (1 - tz) + H(x0, z0 + 1) * (1 - tx) * tz + H(x0 + 1, z0 + 1) * tx * tz; }
      double sx = sc.x * (res - 1), sz = sc.z * (res - 1); int nx = (int)Math.Ceiling(sx / 2) + 1, nz = (int)Math.Ceiling(sz / 2) + 1; double dx = sx / (nx - 1), dz = sz / (nz - 1);
      var hv = new double[nx * nz]; double mn = 1e9;
      for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++) { hv[j * nx + i] = Sample(i * dx, j * dz); mn = Math.Min(mn, hv[j * nx + i]); }
      var layers = new List<string>(); double[] gcol = null;
      foreach (var lp in Rc.L(Rc.D(dd, "m_SplatDatabase"), "m_TerrainLayers") ?? new()) {
        var (lf, lo2) = Rc.Ptr(df, lp); var ld = Rc.Read(lf, lo2); var (tf2, to2) = Rc.Ptr(lf, ld?["m_DiffuseTexture"]); if (to2 == null) continue;
        layers.Add(Raw.Name(tf2, to2));
        // gcol=: the first (base) layer's texture mean x its remap tint (the grass)
        if (gcol == null) { var mean = TexMean(tf2, to2, out _, false, out _); var rmv = ld?.GetValueOrDefault("m_DiffuseRemapMax") as Dictionary<string, object>; var rm = rmv == null ? new double[] { 1, 1, 1 } : rmv.ContainsKey("x") ? new[] { Rc.N(rmv, "x"), Rc.N(rmv, "y"), Rc.N(rmv, "z") } : new[] { Rc.N(rmv, "r"), Rc.N(rmv, "g"), Rc.N(rmv, "b") }; Console.Error.WriteLine($"terrain layer {Raw.Name(tf2, to2)}: remap max {string.Join(",", rm)}, mean {(mean == null ? "-" : string.Join(",", mean))}"); if (mean != null) gcol = new[] { mean[0] * rm[0], mean[1] * rm[1], mean[2] * rm[2] }; }
      }
      if (poly && floorTris.Count > 0) {
        // the same rule as the Android layout: the terrain lowered under the building's floors, so it never pokes through them.
        // A grid point whose 4 cells touch a floor (within one cell, XZ) goes 10 cm under the lowest such floor if higher.
        double ox = n.M[3], oy = n.M[7], oz = n.M[11]; int lowered = 0; double maxDrop = 0;
        var need = new double[nx * nz]; for (int q = 0; q < need.Length; q++) need[q] = 1e9;
        foreach (var t in floorTris) {
          double x0 = Math.Min(t.a.x, Math.Min(t.b.x, t.c.x)), x1 = Math.Max(t.a.x, Math.Max(t.b.x, t.c.x)), z0 = Math.Min(t.a.z, Math.Min(t.b.z, t.c.z)), z1 = Math.Max(t.a.z, Math.Max(t.b.z, t.c.z));
          double ty = Math.Min(t.a.y, Math.Min(t.b.y, t.c.y));
          int i0 = Math.Max(0, (int)Math.Floor((x0 - ox) / dx) - 1), i1 = Math.Min(nx - 1, (int)Math.Ceiling((x1 - ox) / dx) + 1), j0 = Math.Max(0, (int)Math.Floor((z0 - oz) / dz) - 1), j1 = Math.Min(nz - 1, (int)Math.Ceiling((z1 - oz) / dz) + 1);
          for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) {
            double px = ox + i * dx, pz = oz + j * dz;
            // the triangle meets the square of cells round the point (cheap test: box overlap, then the triangle's XZ
            // distance to the point within one cell diagonal)
            if (x1 < px - dx || x0 > px + dx || z1 < pz - dz || z0 > pz + dz) continue;
            if (DistXZ(px, pz, t.a, t.b, t.c) > Math.Max(dx, dz)) continue;
            need[j * nx + i] = Math.Min(need[j * nx + i], ty - 0.10);
          }
        }
        for (int q = 0; q < need.Length; q++) { double w = oy + hv[q]; if (need[q] < 1e8 && w > need[q]) { maxDrop = Math.Max(maxDrop, w - need[q]); hv[q] = need[q] - oy; lowered++; } }
        mn = hv.Min(); terrainNote = $"# 2026-11-23 (rcscene poly=1): {Rc.S(dd, "m_Name")} lowered under the building: {lowered} of {hv.Length} grid points within one 2 m cell of a floor polygon put 10 cm under the lowest such floor (at most {maxDrop:F2} m down); no underlays (the floors are gap-free polygons)\n";
      }
      string id = Y.Enc(Rc.S(dd, "m_Name") ?? "Terrain") + "#0";
      hLines.Add($"H grid {id} {Y.F(n.M[3])} {Y.F(n.M[7] + mn)} {Y.F(n.M[11])} {Y.F(dx)} {Y.F(dz)} {nx} {nz} src=terrain mat=Goblin_Ground_Green_Mat mat.guess=1 was={Y.Enc(Rc.S(dd, "m_Name"))} layers={Y.Enc(string.Join(",", layers))} res={res} size={F3(sx)},{F3(sc.y)},{F3(sz)} collider=1 vis=1" + (poly && gcol != null ? $" gcol={string.Join(",", gcol.Select(x => x.ToString("0.####", Rc.IC)))}" : ""));
      for (int j = 0; j < nz; j++) hLines.Add($"H row {id} {j} " + string.Join(" ", Enumerable.Range(0, nx).Select(i => (hv[j * nx + i] - mn).ToString("0.##", Rc.IC))));

    }

    var envLines = poly ? EnvLines(nodes) : new List<string>();
    // ---- write
    var sb = new StringBuilder();
    sb.Append($"# RRSK 1 {name}\n");
    sb.Append($"B {P3(lo)} {P3(hi)}\n");
    sb.Append($"# rcscene: Rec Room's July 2025 Rec Center (Studio scene RecCenter-RecCenter_071825, the user's HTTPCache 1AF.data), measured in place (DevNotes/tools/rcscene); scene frame\n");
    sb.Append($"# rcscene: {rN} renderers: {rSkippedOff} off, {rLod} LOD1+, {unread} unreadable, {rInst} as install R records, {rPlates} as {plateN} plates ({plateArea:F0} m2) + {dressN} small boxes; {curvedN} small curved pieces left out ({curvedA:F1} m2), {approxN} big curved pieces as fitted rectangles, {tailN} tail rectangles left out; {colN} colliders ({colPlates} mesh-collider C plates), {trig} triggers left out; {lLines.Count} lights; {skinned} skinned meshes and {ps} particle systems not drawn\n");
    if (poly) {
      sb.Append("# frame: the scene's own world space, which IS the Android level7 frame (the room frame of Layouts/RecCenter_Builtin and RrRecCenterLayout): cache -> room = identity (rotation 0, offset 0, scale 1), measured on matching pieces (rcscene framecheck)\n");
      sb.Append($"# rcscene poly=1: {polyPieces} shell pieces ({polyPieceA:F0} m2; {polyCurved} planar but not rectangles) as {pst.Records} planar polygons (\"G poly\": {pst.Rings} rings, {pst.Holes} holes, {pst.PtsOut} points from {pst.PtsIn}; {pst.Groups} plane+colour groups of {pst.Srcs} materials from {pst.Tris} triangles, {pst.TriArea:F0} m2 -> {pst.PolyArea:F0} m2); {pst.Split} overlapping groups split, {pst.TriFallback} parts by triangle ({pst.TriFallbackTris}), {pst.Reverted} rings unsimplified; tolerances angle {Polys.AngTol} deg, offset {Polys.OffTol} m, colour {Polys.ColTol}, outline {Polys.Tol} m\n");
      foreach (var x in pst.Log.Take(20)) sb.Append("# rcscene poly=1: ").Append(x).Append('\n');
      sb.Append($"# rcscene poly=1: {treeRenderers} tree renderers -> {treeLines.Count} \"G tree\" markers; {spotLines.Count} room-data \"G spot\" markers (HTTPCache/182.data, rcscene roomdata); R records also for install meshes with level-only materials (\"?\" T entries described by K records)\n");
      sb.Append("# G tree <pine|aspen|bush> x y z qx qy qz qw h= r= crown= m= mesh=: a level tree (root pose; height above it, crown radius = 90th pct of its leaves' horizontal reach, crown = its lowest leaves (5th pct) above the base); oaks as aspen (broadleaf)\n");
      sb.Append("# G spot <kind> x y z qx qy qz qw src=roomdata name= [cat= sign=]: an object Rec Room spawns from the room data at load (its world pose); doors carry the category of the door sign within 2.5 m\n");
      sb.Append(terrainNote);
      sb.Append($"# rcscene round 2: {envLines.Count} \"E\" records (RenderSettings: fog, trilight ambient, key light as the sun, reflection; colours as serialized, sRGB); {saPieces} pieces with measured sa=/sg= (their own UV triangles' level-texture mean x colour; vertex colour not included, c= keeps it); {vecSt.Pieces} pieces of {VecRe} vectorised into their colour regions ({vecSt.Clusters} colour clusters, {vecSt.Cells} cells, {vecSt.Tris} region triangles; {vecSt.Dropped} mostly cut away dropped, {vecDropA:F2} m2); {framePieces} tiling pieces with their texture frame ua=/rep=; {studN} small curved bits as \"G dress ..._stud\" boxes with sa=; big curved pieces: {barN} bars as one fitted \"G dress ..._box\" (the same name as the small boxy bits, so the room code's dress filters see them), {facetN} as their facets (\"G poly\"); {sliverN} sub-cm fitted rectangles dropped\n");
      sb.Append("# round 2 keys: G poly/plate/dress sa=r,g,b (measured albedo, sRGB 0..1) [sg=r,g,b (measured emission, _EmissionColor x map, may exceed 1)]; G poly/plate ua=x,y,z rep=mu,mv (world +u direction, metres per texture repeat along u and v incl. the material tiling); RecCenter_Posters_Mat polys node=<prefab> part=<object>; G tree avg=r,g,b (leaf texture mean over the alpha-test texels x colour)\n");
    }
    foreach (var x in notes.Take(40)) sb.Append("# ").Append(x).Append('\n');
    for (int i = 0; i < meshT.Count; i++) sb.Append($"M {i} {Y.Enc(meshT[i])}\n");
    for (int i = 0; i < matT.Count; i++) sb.Append($"T {i} {Y.Enc(matT[i])}\n");
    // K: how to draw a "?" T entry (an install mesh's level-only material): colour, measured mean, mode, its install texture
    for (int i = 0; i < matT.Count; i++) {
      if (!matT[i].StartsWith("?") || !mats.TryGetValue(matT[i].Substring(1), out var km)) continue;
      string mode = km.Clear ? "fade" : (km.Cutout || Foliage.IsMatch(km.Name)) ? "cutout" : "opaque";
      // round 2 (contract C2): without an install texture the colour IS the measured mean (col = avg)
      var kcol = km.ITex ? km.Col.Select(x => F3(Math.Clamp(x, 0, 8))) : km.Avg.Select(F3);
      sb.Append($"K {i} col={string.Join(",", kcol)} avg={string.Join(",", km.Avg.Select(F3))} mode={mode}" + (mode == "cutout" ? $" cut={F3(km.Cut)}" : "")
        + (km.ITex ? $" tex={Y.Enc(km.Tex)} tile={F3(km.Su)},{F3(km.Sv)}" : "") + (km.Glow != null ? $" emit={string.Join(",", km.Glow.Take(3).Select(F3))}" : "")
        + (vcMats.Contains(km.Name) ? " vc=1" : "") + $" shader={Y.Enc(km.Shader ?? "?")}\n");
    }
    foreach (var l in envLines) sb.Append(l).Append('\n');
    foreach (var l in rLines) sb.Append(l).Append('\n');
    foreach (var l in cLines) sb.Append(l).Append('\n');
    foreach (var l in lLines) sb.Append(l).Append('\n');
    foreach (var m in mats.Values.OrderBy(x => x.Name)) {
      if (!matArea.ContainsKey(m.Name) && !polyMatA.ContainsKey(m.Name) && !dress.Any(d => (d + " ").Contains(" m=" + Y.Enc(m.Name) + " "))) continue;
      sb.Append($"G platemat {Y.Enc(m.Name)} 0 0 0 0 0 0 1 col={string.Join(",", m.Col.Select(x => F3(Math.Clamp(x, 0, 8))))} tex={Y.Enc(m.Tex)} scale={F3(m.Su)},{F3(m.Sv)} inst={(m.Inst ? 1 : 0)}"
        + (m.Mean != null ? $" mean={string.Join(",", m.Mean.Select(F3))}" : "") + (m.Glow != null ? $" glow={string.Join(",", m.Glow.Take(3).Select(F3))}" : "")
        + (m.Two ? " ds=1" : "") + (m.Clear ? " clear=1" : "")
        + (poly ? $" avg={string.Join(",", m.Avg.Select(x => x.ToString("0.####", Rc.IC)))} itex={(m.ITex ? 1 : 0)}" : "") + "\n");
    }
    foreach (var l in plates) sb.Append(l).Append('\n');
    foreach (var l in polyLines) sb.Append(l).Append('\n');
    foreach (var l in dress) sb.Append(l).Append('\n');
    foreach (var l in gLines) sb.Append(l).Append('\n');
    foreach (var l in spotLines) sb.Append(l).Append('\n');
    foreach (var l in treeLines) sb.Append(l).Append('\n');
    foreach (var l in hLines) sb.Append(l).Append('\n');
    File.WriteAllText(outPath, sb.ToString());
    if (statsPath != null) {
      stats.AddRange(frameLog);
      stats.AddRange(matArea.OrderByDescending(kv => kv.Value).Select(kv => $"matarea\t{kv.Key}\t{kv.Value:F1}"));
      File.WriteAllLines(statsPath, stats);
    }
    Console.WriteLine($"{name}: {rN} renderers ({rSkippedOff} off, {rLod} LOD1+, {unread} unreadable): {rInst} R, {rPlates} -> {plateN} plates {plateArea:F0} m2, {dressN} boxes, curved dropped {curvedN} ({curvedA:F1} m2), approx {approxN}, tail {tailN}; colliders {colN} ({colPlates} C plates), triggers {trig}; lights {lLines.Count}; G {gLines.Count}; {new FileInfo(outPath).Length / 1024} KB");
  }

  /// The XZ distance from a point to a triangle (0 inside).
  static double DistXZ(double px, double pz, V3 a, V3 b, V3 c) {
    double S(V3 p, V3 q) => (q.x - p.x) * (pz - p.z) - (q.z - p.z) * (px - p.x);
    double s1 = S(a, b), s2 = S(b, c), s3 = S(c, a);
    if ((s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0)) return 0;
    double Seg(V3 p, V3 q) { double ex = q.x - p.x, ez = q.z - p.z, l2 = ex * ex + ez * ez; double t = l2 < 1e-12 ? 0 : Math.Clamp(((px - p.x) * ex + (pz - p.z) * ez) / l2, 0, 1); double dx = p.x + ex * t - px, dz = p.z + ez * t - pz; return Math.Sqrt(dx * dx + dz * dz); }
    return Math.Min(Seg(a, b), Math.Min(Seg(b, c), Seg(c, a)));
  }

  static string AvgCol(double[] cols, int[] tris) {
    double r = 0, g = 0, b = 0; foreach (int i in tris) { r += cols[i * 3]; g += cols[i * 3 + 1]; b += cols[i * 3 + 2]; }
    int n = Math.Max(1, tris.Length); return $"{F3(r / n)},{F3(g / n)},{F3(b / n)},1";
  }

  /// Whether an install mesh is the scene's mesh: same vertex count (non-batched) or the same index counts (batched),
  /// and the same bounds within 1 cm (batched: the install mesh's bounds through the node's matrix against the batch's
  /// world-space submesh bounds).
  /// The inverse of an affine 4x4 (row-major); null when singular.
  static double[] Inv(double[] m) {
    double d = Det(m); if (Math.Abs(d) < 1e-15) return null; double id = 1 / d;
    var r = new double[16];
    r[0] = (m[5] * m[10] - m[6] * m[9]) * id; r[1] = (m[2] * m[9] - m[1] * m[10]) * id; r[2] = (m[1] * m[6] - m[2] * m[5]) * id;
    r[4] = (m[6] * m[8] - m[4] * m[10]) * id; r[5] = (m[0] * m[10] - m[2] * m[8]) * id; r[6] = (m[2] * m[4] - m[0] * m[6]) * id;
    r[8] = (m[4] * m[9] - m[5] * m[8]) * id; r[9] = (m[1] * m[8] - m[0] * m[9]) * id; r[10] = (m[0] * m[5] - m[1] * m[4]) * id;
    r[3] = -(r[0] * m[3] + r[1] * m[7] + r[2] * m[11]); r[7] = -(r[4] * m[3] + r[5] * m[7] + r[6] * m[11]); r[11] = -(r[8] * m[3] + r[9] * m[7] + r[10] * m[11]); r[15] = 1;
    return r;
  }

  // the re-export rule leaves these to the room code (RrRecCenterDress already stands the install Curtain_LOD0 at the four
  // [Curtain] poses and drops their Curtain_Red_Mat pieces; an R record here as well would draw each curtain twice)
  static readonly Regex ReexportSkip = new(Environment.GetEnvironmentVariable("RC_REEXPORT_SKIP") ?? @"^Curtain_LOD0$");
  static bool SameMesh(MeshGeo inst, MeshGeo scene, bool batched, int first, int count, double[] m, string cand = null) {
    if (!batched) {
      // the same vertex count within 1 cm, or a re-export of the same model (the install's RecCenter_Couch_LOD0 has
      // 1,923 vertices to the July 2025 scene's 1,633 on the very same bounds): bounds within 5 mm
      double tol = inst.VertexCount == scene.VertexCount ? 0.01 : 0.005;
      return Math.Abs(inst.C.x - scene.C.x) < tol && Math.Abs(inst.C.y - scene.C.y) < tol && Math.Abs(inst.C.z - scene.C.z) < tol
        && Math.Abs(inst.E.x - scene.E.x) < tol && Math.Abs(inst.E.y - scene.E.y) < tol && Math.Abs(inst.E.z - scene.E.z) < tol;
    }
    if (inst.SubIndexCount.Count != count) return false;
    bool sameIdx = true;
    for (int k = 0; k < count; k++) if (first + k >= scene.SubIndexCount.Count || inst.SubIndexCount[k] != scene.SubIndexCount[first + k]) sameIdx = false;
    if (!sameIdx) {
      if (cand == null || ReexportSkip.IsMatch(cand)) return false;
      // round 2: a re-export of the same model (July 2025's chair_stackable_LOD0 has 3,048 indices to the install's 4,308;
      // Fig_LOD0 3,492/462 to 3,504/648): the same submesh count, and the batch's vertices taken back into the node's own
      // space have the install mesh's local bounds within 2 cm per axis (in metres, through the node's scale) - the couch
      // rule above for static batches
      if (scene.V == null || first + count > scene.Subs.Count) return false;
      var inv = Inv(m); if (inv == null) return false;
      V3 llo = new(1e9, 1e9, 1e9), lhi = new(-1e9, -1e9, -1e9);
      for (int k = first; k < first + count; k++) foreach (int vi in scene.Subs[k]) { var l = P(inv, scene.V[vi]); llo = V3.Min(llo, l); lhi = V3.Max(lhi, l); }
      if (lhi.x < llo.x) return false;
      double sx = new V3(m[0], m[4], m[8]).Len(), sy = new V3(m[1], m[5], m[9]).Len(), sz = new V3(m[2], m[6], m[10]).Len();
      var ilo = inst.C - inst.E; var ihi = inst.C + inst.E; const double tol = 0.02;
      return Math.Abs(llo.x - ilo.x) * sx < tol && Math.Abs(lhi.x - ihi.x) * sx < tol && Math.Abs(llo.y - ilo.y) * sy < tol && Math.Abs(lhi.y - ihi.y) * sy < tol
        && Math.Abs(llo.z - ilo.z) * sz < tol && Math.Abs(lhi.z - ihi.z) * sz < tol;
    }
    V3 lo = new(1e9, 1e9, 1e9), hi = new(-1e9, -1e9, -1e9), blo = lo, bhi = hi;
    for (int i = 0; i < 8; i++) { var w = P(m, new V3(inst.C.x + ((i & 1) == 0 ? -1 : 1) * inst.E.x, inst.C.y + ((i & 2) == 0 ? -1 : 1) * inst.E.y, inst.C.z + ((i & 4) == 0 ? -1 : 1) * inst.E.z)); lo = V3.Min(lo, w); hi = V3.Max(hi, w); }
    for (int k = first; k < first + count; k++) { var (c, e) = scene.SubBox[k]; blo = V3.Min(blo, c - e); bhi = V3.Max(bhi, c + e); }
    // the batch's AABB is that of the transformed vertices: inside the transformed install box, touching it on every axis
    // for an axis-aligned prop; a rotated prop's box is smaller, so allow the difference but not a different size class
    var d1 = blo - lo; var d2 = hi - bhi;
    return d1.x > -0.01 && d1.y > -0.01 && d1.z > -0.01 && d2.x > -0.01 && d2.y > -0.01 && d2.z > -0.01
      && (bhi - blo).Len() > 0.5 * (hi - lo).Len();
  }
}

static class RcDump {
  /// A shallow view of an object (scalars, vectors, list sizes, pointers as file#pid) for exploring the scene.
  public static void Show(object o, string ind, int depth) {
    if (o is Dictionary<string, object> d) {
      foreach (var kv in d) {
        var v = kv.Value;
        if (v is Dictionary<string, object> dd && dd.ContainsKey("m_PathID")) Console.WriteLine($"{ind}{kv.Key}: ptr {dd["m_FileID"]}#{dd["m_PathID"]}");
        else if (v is Dictionary<string, object> d2) { if (depth > 0) { Console.WriteLine($"{ind}{kv.Key}:"); Show(d2, ind + "  ", depth - 1); } else Console.WriteLine($"{ind}{kv.Key}: {{...}}"); }
        else if (v is List<object> l) { Console.WriteLine($"{ind}{kv.Key}: list {l.Count}"); if (depth > 0 && l.Count > 0) { int all = Environment.GetEnvironmentVariable("RC_DUMPALL") != null ? l.Count : 1; for (int i = 0; i < all; i++) Show(l[i], ind + $"  [{i}] ", depth - 1); } }
        else if (v is byte[] b) Console.WriteLine($"{ind}{kv.Key}: bytes {b.Length}");
        else Console.WriteLine($"{ind}{kv.Key}: {Convert.ToString(v, CultureInfo.InvariantCulture)}");
      }
    } else Console.WriteLine(ind + Convert.ToString(o, CultureInfo.InvariantCulture));
  }
}
