// rrscene - reads AssetRipper-style Unity YAML scenes (reference only) and writes RRSK 1 placement text.
// See README.md for build / run / format. Never writes into the reference folders or the Unity project.
using System.Globalization;
using System.Text.RegularExpressions;

struct V3 { public double x, y, z; public V3(double a, double b, double c) { x = a; y = b; z = c; }
  public static V3 operator +(V3 a, V3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
  public static V3 operator -(V3 a, V3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
  public static V3 operator *(V3 a, double s) => new(a.x * s, a.y * s, a.z * s);
  public V3 Mul(V3 b) => new(x * b.x, y * b.y, z * b.z);
  public V3 Abs() => new(Math.Abs(x), Math.Abs(y), Math.Abs(z));
  public double Max() => Math.Max(x, Math.Max(y, z));
  public static V3 Cross(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
}
struct Q { public double x, y, z, w; public Q(double a, double b, double c, double d) { x = a; y = b; z = c; w = d; }
  public static Q I => new(0, 0, 0, 1);
  public static Q operator *(Q a, Q b) => new(
    a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
    a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w, a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
  public V3 Rot(V3 v) { var u = new V3(x, y, z); var t = V3.Cross(u, v) * 2; return v + t * w + V3.Cross(u, t); }
  public Q Norm() { var n = Math.Sqrt(x * x + y * y + z * z + w * w); return n < 1e-9 ? I : new(x / n, y / n, z / n, w / n); }
}
struct TRS { public V3 p; public Q r; public V3 s; }
struct Ref { public long fid; public string guid; public bool Null => fid == 0 && (guid == null || guid.Trim('0') == ""); }

class Doc { public int Cls; public long Id; public bool Stripped; public Dictionary<string, string> K = new(); public List<string> L = new(); }
class GoI { public string Name = ""; public int Layer; public bool Active = true; public List<long> Comps = new(); }
class TrI { public long Go; public V3 P, S = new(1, 1, 1); public Q R = Q.I; public long Father; public long PrefabInst; public long SrcFid; public bool Stripped; }
class RendI { public long Id, Go; public bool En = true; public List<Ref> Mats = new(); public Ref Mesh; public bool Skinned; }
class ColI { public int Cls; public long Go; public bool Trig, En = true; public V3 C, Size; public double Rad, H; public int Dir = 1; public Ref Mesh; }
class LightI { public long Go; public int Type; public double[] Col; public double Int, Range, Spot; public bool En = true; }
class AudI { public long Go; public Ref Clip; public double Vol = 1, Min = 1, Max = 500, Pan = 1; public int Roll; public bool Loop, En = true; }
class MbI { public long Go; public string Script; public bool En = true; public List<string> Fields = new(); }
// ParticleSystem (198): nested YAML values by dotted path ("InitialModule.startSize.scalar"), the renderer (199) material guids.
class PsI { public long Go; public Dictionary<string, string> V = new(); public double N(string k, double d = 0) => V.TryGetValue(k, out var s) ? Y.D(s, d) : d; }
class PsrI { public long Go; public bool En = true; public List<Ref> Mats = new(); }
class PiI { public long Id; public long Parent; public Ref Src; public List<(long tfid, string path, string val, Ref oref)> Mods = new(); }

class SceneModel {
  public string Name, Path; public long Bytes;
  public Dictionary<long, GoI> Go = new(); public Dictionary<long, TrI> Tr = new(); public Dictionary<long, long> GoTr = new();
  public Dictionary<long, Ref> Mf = new(); public List<RendI> Rend = new(); public List<ColI> Col = new(); public List<LightI> Lights = new();
  public List<AudI> Aud = new(); public List<MbI> Mb = new(); public List<PsI> Ps = new(); public List<PsrI> Psr = new(); public HashSet<long> LodSkip = new(); public List<PiI> Pi = new();
  public Dictionary<string, string> RS = new(); public Dictionary<long, List<long>> Kids = new();
}

static class Y {
  public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
  static readonly Regex Hdr = new(@"^--- !u!(\d+) &(-?\d+)( stripped)?", RegexOptions.Compiled);
  static readonly Regex KeyRx = new(@"^\s*[A-Za-z_][\w\.\[\]]*:( |$)", RegexOptions.Compiled);
  static int Indent(string s) { int i = 0; while (i < s.Length && s[i] == ' ') i++; return i; }
  static int Bal(string s) { int b = 0; bool q = false; foreach (var c in s) { if (c == '\'') q = !q; if (q) continue; if (c == '{' || c == '[') b++; else if (c == '}' || c == ']') b--; } return b; }
  public static IEnumerable<Doc> Docs(string path) {
    using var sr = new StreamReader(path, Encoding.UTF8, true, 1 << 20);
    Doc d = null; string buf = null; int bufInd = 0;
    void Flush() { if (d != null && buf != null) { d.L.Add(buf); var t = buf.TrimStart(); if (bufInd == 2) { int c = t.IndexOf(':'); if (c > 0) { var k = t.Substring(0, c); if (!d.K.ContainsKey(k)) d.K[k] = t.Substring(c + 1).Trim(); } } } buf = null; }
    string line;
    while ((line = sr.ReadLine()) != null) {
      if (line.StartsWith("--- ")) {
        Flush(); if (d != null) yield return d;
        var m = Hdr.Match(line); d = m.Success ? new Doc { Cls = int.Parse(m.Groups[1].Value), Id = long.Parse(m.Groups[2].Value), Stripped = m.Groups[3].Success } : null;
        continue;
      }
      if (d == null) continue;
      if (buf != null) {
        int ind = Indent(line); var t = line.TrimStart();
        bool cont = Bal(buf) > 0;
        if (!cont) {
          var bt = buf.TrimStart(); int c = bt.IndexOf(": ");
          string val = c > 0 ? bt.Substring(c + 2) : "";
          if (val.StartsWith("\"") && (val.Length == 1 || !val.EndsWith("\"") || val.EndsWith("\\\""))) cont = true;
          else if (val.StartsWith("'") && (val.Length == 1 || !val.EndsWith("'"))) cont = true;
          else if (val.Length > 0 && ind > bufInd && !t.StartsWith("- ") && !KeyRx.IsMatch(line) && !bt.StartsWith("- ")) cont = true;
        }
        if (cont) { buf += " " + t; continue; }
        Flush();
      }
      buf = line; bufInd = Indent(line);
    }
    Flush(); if (d != null) yield return d;
  }
  static readonly Regex Num = new(@"([a-zA-Z]+):\s*(-?[0-9.eE+\-]+|-?Infinity|NaN)", RegexOptions.Compiled);
  public static Dictionary<string, double> Map(string s) { var r = new Dictionary<string, double>(); if (s == null) return r; foreach (Match m in Num.Matches(s)) if (double.TryParse(m.Groups[2].Value, NumberStyles.Float, Inv, out var v)) r[m.Groups[1].Value] = v; return r; }
  public static V3 V(string s, V3 def = default) { var m = Map(s); if (!m.ContainsKey("x")) return def; return new(m.GetValueOrDefault("x"), m.GetValueOrDefault("y"), m.GetValueOrDefault("z")); }
  public static Q Qt(string s) { var m = Map(s); if (!m.ContainsKey("w")) return Q.I; return new Q(m.GetValueOrDefault("x"), m.GetValueOrDefault("y"), m.GetValueOrDefault("z"), m.GetValueOrDefault("w")).Norm(); }
  public static double[] C(string s) { var m = Map(s); if (!m.ContainsKey("r")) return null; return new[] { m["r"], m.GetValueOrDefault("g"), m.GetValueOrDefault("b"), m.GetValueOrDefault("a", 1) }; }
  static readonly Regex RefRx = new(@"fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-fA-F]+))?", RegexOptions.Compiled);
  public static Ref R(string s) { var r = new Ref(); if (s == null) return r; var m = RefRx.Match(s); if (!m.Success) return r; r.fid = long.Parse(m.Groups[1].Value); r.guid = m.Groups[2].Success ? m.Groups[2].Value.ToLowerInvariant() : null; return r; }
  public static double D(string s, double def = 0) => s != null && double.TryParse(s, NumberStyles.Float, Inv, out var v) ? v : def;
  public static bool B(string s, bool def = false) => s == null ? def : s.Trim() == "1" || s.Trim() == "true";
  public static string Unq(string s) {
    if (s == null) return "";
    s = s.Trim();
    if (s.Length >= 2 && s[0] == '\'' && s[^1] == '\'') return s.Substring(1, s.Length - 2).Replace("''", "'");
    if (s.Length >= 2 && s[0] == '"' && s[^1] == '"') { s = s.Substring(1, s.Length - 2); s = Regex.Replace(s, @"\\u([0-9a-fA-F]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString()); return s.Replace("\\\"", "\"").Replace("\\\\", "\\"); }
    return s;
  }
  public static string F(double v) { var r = Math.Round(v, 4); if (r == 0) r = 0; return r.ToString("0.####", Inv); }
  public static string Enc(string s) => string.IsNullOrEmpty(s) ? "_" : s.Replace("%", "%25").Replace(" ", "%20").Replace("\t", "%09").Replace("\n", "%0A").Replace("\r", "");
}

static class Parse {
  static readonly HashSet<string> MbStd = new() { "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier" };
  public static SceneModel Load(string path) {
    var sm = new SceneModel { Path = path, Bytes = new FileInfo(path).Length };
    foreach (var d in Y.Docs(path)) {
      var k = d.K;
      switch (d.Cls) {
        case 1: {
          var g = new GoI { Name = Y.Unq(k.GetValueOrDefault("m_Name")), Layer = (int)Y.D(k.GetValueOrDefault("m_Layer")), Active = Y.B(k.GetValueOrDefault("m_IsActive"), true) };
          foreach (var l in d.L) { var t = l.TrimStart(); if (t.StartsWith("- component:")) g.Comps.Add(Y.R(t).fid); }
          sm.Go[d.Id] = g; break; }
        case 4: case 224: {
          var t = new TrI { Go = Y.R(k.GetValueOrDefault("m_GameObject")).fid, P = Y.V(k.GetValueOrDefault("m_LocalPosition")), R = Y.Qt(k.GetValueOrDefault("m_LocalRotation")),
            S = Y.V(k.GetValueOrDefault("m_LocalScale"), new V3(1, 1, 1)), Father = Y.R(k.GetValueOrDefault("m_Father")).fid, Stripped = d.Stripped,
            PrefabInst = Y.R(k.GetValueOrDefault("m_PrefabInstance")).fid, SrcFid = Y.R(k.GetValueOrDefault("m_CorrespondingSourceObject")).fid };
          sm.Tr[d.Id] = t; if (t.Go != 0) sm.GoTr[t.Go] = d.Id; break; }
        case 33: sm.Mf[Y.R(k.GetValueOrDefault("m_GameObject")).fid] = Y.R(k.GetValueOrDefault("m_Mesh")); break;
        case 23: case 137: {
          var r = new RendI { Id = d.Id, Go = Y.R(k.GetValueOrDefault("m_GameObject")).fid, En = Y.B(k.GetValueOrDefault("m_Enabled"), true), Skinned = d.Cls == 137 };
          if (d.Cls == 137) r.Mesh = Y.R(k.GetValueOrDefault("m_Mesh"));
          bool inM = false;
          foreach (var l in d.L) { if (l.StartsWith("  m_Materials:")) { inM = true; continue; } if (inM) { if (l.StartsWith("  - ")) r.Mats.Add(Y.R(l)); else inM = false; } }
          sm.Rend.Add(r); break; }
        case 65: case 64: case 135: case 136: {
          var c = new ColI { Cls = d.Cls, Go = Y.R(k.GetValueOrDefault("m_GameObject")).fid, Trig = Y.B(k.GetValueOrDefault("m_IsTrigger")), En = Y.B(k.GetValueOrDefault("m_Enabled"), true),
            C = Y.V(k.GetValueOrDefault("m_Center")), Size = Y.V(k.GetValueOrDefault("m_Size"), new V3(1, 1, 1)), Rad = Y.D(k.GetValueOrDefault("m_Radius"), 0.5), H = Y.D(k.GetValueOrDefault("m_Height"), 2),
            Dir = (int)Y.D(k.GetValueOrDefault("m_Direction"), 1), Mesh = Y.R(k.GetValueOrDefault("m_Mesh")) };
          sm.Col.Add(c); break; }
        case 108: sm.Lights.Add(new LightI { Go = Y.R(k.GetValueOrDefault("m_GameObject")).fid, Type = (int)Y.D(k.GetValueOrDefault("m_Type"), 2), Col = Y.C(k.GetValueOrDefault("m_Color")) ?? new double[] { 1, 1, 1, 1 },
          Int = Y.D(k.GetValueOrDefault("m_Intensity"), 1), Range = Y.D(k.GetValueOrDefault("m_Range"), 10), Spot = Y.D(k.GetValueOrDefault("m_SpotAngle"), 30), En = Y.B(k.GetValueOrDefault("m_Enabled"), true) }); break;
        case 82: {
          var au = new AudI { Go = Y.R(k.GetValueOrDefault("m_GameObject")).fid, Clip = Y.R(k.GetValueOrDefault("m_audioClip")), Vol = Y.D(k.GetValueOrDefault("m_Volume"), 1),
            Min = Y.D(k.GetValueOrDefault("MinDistance"), 1), Max = Y.D(k.GetValueOrDefault("MaxDistance"), 500), Loop = Y.B(k.GetValueOrDefault("Loop")), En = Y.B(k.GetValueOrDefault("m_Enabled"), true),
            Roll = (int)Y.D(k.GetValueOrDefault("rolloffMode"), 0) };
          // spatial blend = the first key of panLevelCustomCurve (0 = 2D: Rec Center's AmbianceLightCrowd)
          bool inPan = false;
          foreach (var l in d.L) { var t = l.TrimStart(); if (l.StartsWith("  panLevelCustomCurve:")) { inPan = true; continue; } if (inPan && t.StartsWith("value:")) { au.Pan = Y.D(t.Substring(6).Trim(), 1); break; } if (inPan && l.StartsWith("  ") && !l.StartsWith("   ")) break; }
          sm.Aud.Add(au); break; }
        case 114: {
          var m = new MbI { Go = Y.R(k.GetValueOrDefault("m_GameObject")).fid, Script = Y.R(k.GetValueOrDefault("m_Script")).guid ?? "none", En = Y.B(k.GetValueOrDefault("m_Enabled"), true) };
          foreach (var key in k.Keys) if (!MbStd.Contains(key)) m.Fields.Add(key);
          sm.Mb.Add(m); break; }
        case 205: {
          int lod = -1;
          foreach (var l in d.L) { var t = l.TrimStart(); if (t.StartsWith("- screenRelativeHeight")) lod++; else if (t.StartsWith("- renderer:") && lod > 0) sm.LodSkip.Add(Y.R(t).fid); }
          break; }
        case 1001: {
          var p = new PiI { Id = d.Id };
          foreach (var l in d.L) {
            var t = l.TrimStart();
            if (t.StartsWith("m_TransformParent:")) p.Parent = Y.R(t).fid;
            else if (t.StartsWith("m_SourcePrefab:")) p.Src = Y.R(t);
            else if (t.StartsWith("- target:")) p.Mods.Add((Y.R(t).fid, null, null, default));
            else if (t.StartsWith("propertyPath:") && p.Mods.Count > 0) { var x = p.Mods[^1]; x.path = t.Substring(13).Trim(); p.Mods[^1] = x; }
            else if (t.StartsWith("value:") && p.Mods.Count > 0) { var x = p.Mods[^1]; x.val = t.Substring(6).Trim(); p.Mods[^1] = x; }
            else if (t.StartsWith("objectReference:") && p.Mods.Count > 0) { var x = p.Mods[^1]; x.oref = Y.R(t); p.Mods[^1] = x; }
          }
          sm.Pi.Add(p); break; }
        case 104: foreach (var kv in k) sm.RS[kv.Key] = kv.Value; break;
        case 198: {
          // Nested module values: keys by indentation (2 spaces per level), first occurrence wins; list items skipped.
          var p = new PsI { Go = Y.R(k.GetValueOrDefault("m_GameObject")).fid };
          var stack = new string[16];
          foreach (var l in d.L) {
            int ind = 0; while (ind < l.Length && l[ind] == ' ') ind++;
            if (ind < 2 || ind >= l.Length || l[ind] == '-') continue;
            int c = l.IndexOf(':', ind); if (c < 0) continue;
            int depth = ind / 2 - 1; if (depth >= stack.Length) continue;
            stack[depth] = l.Substring(ind, c - ind);
            var val = l.Substring(c + 1).Trim(); if (val.Length == 0) continue;
            var key = string.Join(".", stack, 0, depth + 1);
            if (!p.V.ContainsKey(key)) p.V[key] = val;
          }
          sm.Ps.Add(p); break; }
        case 199: {
          var r = new PsrI { Go = Y.R(k.GetValueOrDefault("m_GameObject")).fid, En = Y.B(k.GetValueOrDefault("m_Enabled"), true) };
          bool inM = false;
          foreach (var l in d.L) { if (l.StartsWith("  m_Materials:")) { inM = true; continue; } if (inM) { if (l.StartsWith("  - ")) r.Mats.Add(Y.R(l)); else inM = false; } }
          sm.Psr.Add(r); break; }
      }
    }
    foreach (var kv in sm.Tr) if (kv.Value.Father != 0) { if (!sm.Kids.TryGetValue(kv.Value.Father, out var l)) sm.Kids[kv.Value.Father] = l = new(); l.Add(kv.Key); }
    return sm;
  }
}

class MeshInfo { public string Name; public bool HasB; public V3 Min, Max; public int Sub = 1; }
class MatInfo { public string Name; public double[] Col; public string Tex; public string ShaderGuid; }

static class Ref2 {
  public static string Root = @"D:\Dreamport\EXTERNAL FILES FOR REFERNCE\Downloads\RecRoomAssets";
  public static Dictionary<string, string> Guid = new();
  static Dictionary<string, MeshInfo> _mesh = new(); static Dictionary<string, MatInfo> _mat = new(); static Dictionary<string, MeshInfo> _glb = new();
  static HashSet<string> _glbFiles;
  public static void LoadGuids(string cache) {
    if (!File.Exists(cache)) {
      using var w = new StreamWriter(cache);
      foreach (var f in Directory.EnumerateFiles(Root, "*.meta", SearchOption.AllDirectories)) {
        try { using var sr = new StreamReader(f); for (int i = 0; i < 4; i++) { var l = sr.ReadLine(); if (l == null) break; if (l.StartsWith("guid: ")) { w.WriteLine(l.Substring(6).Trim() + "\t" + f.Substring(Root.Length + 1, f.Length - Root.Length - 6)); break; } } } catch { }
      }
    }
    foreach (var l in File.ReadLines(cache)) { var p = l.Split('\t'); Guid[p[0]] = p[1]; }
  }
  static string Head(string path, int max = 65536) { using var fs = File.OpenRead(path); var b = new byte[Math.Min(max, fs.Length)]; int n = fs.Read(b, 0, b.Length); return Encoding.UTF8.GetString(b, 0, n); }
  public static MeshInfo Mesh(string guid) {
    if (guid == null || !Guid.TryGetValue(guid, out var rel) || !rel.EndsWith(".asset")) return null;
    if (_mesh.TryGetValue(guid, out var mi)) return mi;
    mi = new MeshInfo();
    try {
      var txt = Head(System.IO.Path.Combine(Root, rel));
      var lines = txt.Split('\n'); string lastC = null; int sub = 0;
      foreach (var raw in lines) {
        var t = raw.TrimStart();
        if (t.StartsWith("m_Name:") && mi.Name == null) mi.Name = Y.Unq(t.Substring(7));
        else if (t.StartsWith("- serializedVersion") && raw.StartsWith("  - ")) sub++;
        else if (t.StartsWith("m_Center:")) lastC = t;
        else if (t.StartsWith("m_Extent:") && lastC != null) { var c = Y.V(lastC.Substring(9)); var e = Y.V(t.Substring(9)).Abs(); Add(mi, c - e); Add(mi, c + e); lastC = null; }
        else if (t.StartsWith("m_Shapes:") || t.StartsWith("m_IndexBuffer")) break;
      }
      mi.Sub = Math.Max(1, sub);
    } catch { }
    if (string.IsNullOrEmpty(mi.Name)) mi.Name = System.IO.Path.GetFileNameWithoutExtension(rel);
    return _mesh[guid] = mi;
  }
  static void Add(MeshInfo mi, V3 p) { if (!mi.HasB) { mi.Min = mi.Max = p; mi.HasB = true; return; } mi.Min = new(Math.Min(mi.Min.x, p.x), Math.Min(mi.Min.y, p.y), Math.Min(mi.Min.z, p.z)); mi.Max = new(Math.Max(mi.Max.x, p.x), Math.Max(mi.Max.y, p.y), Math.Max(mi.Max.z, p.z)); }
  static readonly Regex PosAcc = new("\\{\"name\":\"POSITION\"[^{}]*?\"max\":\\[([^\\]]*)\\],\"min\":\\[([^\\]]*)\\]", RegexOptions.Compiled);
  // Mesh/<name>.glb (+ <name>_0.glb, _1 ... for sub-meshes). SharpGLTF export: glTF is right-handed, X is mirrored vs Unity.
  public static MeshInfo Glb(string name) {
    if (string.IsNullOrEmpty(name)) return null;
    if (_glb.TryGetValue(name, out var mi)) return mi;
    if (_glbFiles == null) { _glbFiles = new(StringComparer.OrdinalIgnoreCase); foreach (var f in Directory.EnumerateFiles(System.IO.Path.Combine(Root, "Mesh"), "*.glb")) _glbFiles.Add(System.IO.Path.GetFileNameWithoutExtension(f)); }
    mi = null;
    var cands = new List<string>(); if (_glbFiles.Contains(name)) cands.Add(name); for (int i = 0; i < 64 && _glbFiles.Contains(name + "_" + i); i++) cands.Add(name + "_" + i);
    foreach (var c in cands) {
      try {
        using var fs = File.OpenRead(System.IO.Path.Combine(Root, "Mesh", c + ".glb")); var h = new byte[20]; fs.Read(h, 0, 20);
        int len = BitConverter.ToInt32(h, 12); var j = new byte[len]; fs.Read(j, 0, len); var js = Encoding.UTF8.GetString(j);
        foreach (Match m in PosAcc.Matches(js)) {
          var mx = m.Groups[1].Value.Split(',').Select(s => Y.D(s)).ToArray(); var mn = m.Groups[2].Value.Split(',').Select(s => Y.D(s)).ToArray();
          if (mx.Length < 3 || mn.Length < 3) continue;
          mi ??= new MeshInfo { Name = name };
          Add(mi, new V3(-mx[0], mn[1], mn[2])); Add(mi, new V3(-mn[0], mx[1], mx[2]));
        }
      } catch { }
    }
    return _glb[name] = mi;
  }
  public static MatInfo Mat(string guid) {
    if (guid == null || !Guid.TryGetValue(guid, out var rel) || !rel.EndsWith(".mat")) return null;
    if (_mat.TryGetValue(guid, out var mi)) return mi;
    mi = new MatInfo();
    try {
      string sec = null;
      foreach (var raw in File.ReadLines(System.IO.Path.Combine(Root, rel))) {
        var t = raw.TrimStart();
        if (t.StartsWith("m_Name:") && mi.Name == null) mi.Name = Y.Unq(t.Substring(7));
        else if (t.StartsWith("m_Shader:")) mi.ShaderGuid = Y.R(t).guid;
        else if (t.StartsWith("- _BaseMap:") || t.StartsWith("- _MainTex:")) sec = "tex";
        else if (t.StartsWith("- _")) sec = null;
        else if (sec == "tex" && t.StartsWith("m_Texture:")) { var g = Y.R(t).guid; if (g != null && mi.Tex == null && Guid.TryGetValue(g, out var tp)) mi.Tex = System.IO.Path.GetFileNameWithoutExtension(tp); sec = null; }
        if ((t.StartsWith("- _BaseColor:") || t.StartsWith("- _Color:")) && mi.Col == null) mi.Col = Y.C(t);
      }
    } catch { }
    mi.Name ??= System.IO.Path.GetFileNameWithoutExtension(rel);
    return _mat[guid] = mi;
  }
  static Dictionary<string, string> _byName;
  // .assets/<name>.asset -> guid (file stem is the mesh name for all but a few renamed duplicates)
  public static MeshInfo MeshByName(string name) {
    if (string.IsNullOrEmpty(name)) return null;
    if (_byName == null) { _byName = new(StringComparer.OrdinalIgnoreCase); foreach (var kv in Guid) if (kv.Value.StartsWith(".assets") && kv.Value.EndsWith(".asset")) _byName[System.IO.Path.GetFileNameWithoutExtension(kv.Value)] = kv.Key; }
    return _byName.TryGetValue(name, out var g) ? Mesh(g) : null;
  }
  public static string FileName(string guid) => guid != null && Guid.TryGetValue(guid, out var rel) ? System.IO.Path.GetFileName(rel) : null;
}

static class InstDb {
  public static HashSet<string> Mesh = new(), Mat = new(), Prefab = new(), Audio = new();
  public static Dictionary<string, List<string[]>> MeshMats = new();          // mesh -> material lists seen with it in the install
  public static Dictionary<string, HashSet<string>> PrefabMeshes = new();     // prefab root name -> mesh names in its subtree
  public static Dictionary<string, double[]> MatCol = new();
  public static void Load(string work) {
    foreach (var l in File.ReadLines(System.IO.Path.Combine(Inst.RrIdx, "objects.tsv"))) {
      var p = l.Split('\t'); if (p.Length < 4) continue;
      switch (p[2]) { case "Mesh": Mesh.Add(p[3]); break; case "Material": Mat.Add(p[3]); break; case "GameObject*": Prefab.Add(p[3]); break; case "AudioClip": Audio.Add(p[3]); break; }
    }
    var ir = System.IO.Path.Combine(work, "inst_renderers.tsv");
    if (File.Exists(ir)) foreach (var l in File.ReadLines(ir)) {
      var p = l.Split('\t'); if (p.Length < 6) continue;
      if (p[4] != "" && p[4] != "?") { if (!MeshMats.TryGetValue(p[4], out var ml)) MeshMats[p[4]] = ml = new(); if (ml.Count < 16) ml.Add(p[5].Split(',')); }
      if (!PrefabMeshes.TryGetValue(p[2], out var s)) PrefabMeshes[p[2]] = s = new(); if (p[4] != "") s.Add(p[4]);
    }
    var im = System.IO.Path.Combine(work, "inst_materials.tsv");
    if (File.Exists(im)) foreach (var l in File.ReadLines(im)) { var p = l.Split('\t'); if (p.Length < 2 || p[1] == "-") continue; var c = p[1].Split(' ').Select(x => Y.D(x)).ToArray(); if (c.Length >= 3) MatCol[p[0]] = c; }
  }
}
