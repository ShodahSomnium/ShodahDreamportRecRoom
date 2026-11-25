// rcref_cache: the cache bundle reader, a snapshot COPY of ../rcscene/Rc.cs (classes Rc, MeshGeo, Node and the scene
// walk of RcConvert.Nodes), taken 2026-11-23 so this tool does not break while rcscene changes. Read in place, data only.
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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
  public string Name; public V3[] V; public List<int[]> Subs = new(); public double[] Cols; public V3[] Nrm; public V3 C, E; public int VertexCount;
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
    m.V = v.ToArray(); m.VertexCount = m.V.Length; m.Subs.Add(t.ToArray()); m.SubIndexCount.Add(t.Count); m.SubBox.Add((default, default));
    return m;
  }
}

/// The PC install's mesh and material names (DevNotes/rrtoolidx objects.tsv) and its meshes' counts and bounds.

/// A scene GameObject with its world matrix (row-major 4x4) and components.
sealed class Node {
  public string Path, Name; public int Layer; public bool Active; public double[] M; public List<SObj> Comps = new(); public long Go; public Node Parent;
  public SObj Comp(int cls) => Comps.FirstOrDefault(c => c.ClassId == cls);
}

static class RcConvert {
  static double[] TRS(float[] p, float[] q, float[] s) {
    double x = q[0], y = q[1], z = q[2], w = q[3];
    double[] r = { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) };
    return new double[] { r[0] * s[0], r[1] * s[1], r[2] * s[2], p[0], r[3] * s[0], r[4] * s[1], r[5] * s[2], p[1], r[6] * s[0], r[7] * s[1], r[8] * s[2], p[2], 0, 0, 0, 1 };
  }
  static double[] Mul(double[] a, double[] b) { var m = new double[16]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { double s = 0; for (int k = 0; k < 4; k++) s += a[i * 4 + k] * b[k * 4 + j]; m[i * 4 + j] = s; } return m; }
  static List<Node> _nodes;
  /// every GameObject of the scene with its world matrix (parent matrix x local TRS) and active-in-hierarchy flag
  public static List<Node> NodeList() {
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
}
