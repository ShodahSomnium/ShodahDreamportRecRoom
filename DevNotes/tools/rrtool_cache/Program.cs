// rrtool: research tool over Rec Room's Addressables bundles.
//   rrtool index <outDir>                 build objects/containers/deps/scripts index
//   rrtool tree <bundle> <pathId> [depth]  GameObject hierarchy with components and MonoBehaviour fields
//   rrtool dump <bundle> <pathId>          one object as JSON
//   rrtool find <regex> [class]           search the index (needs index in ./idx)
//   rrtool roots <bundle>                 root GameObjects of a bundle
using System.Text;
using System.Text.RegularExpressions;

static class Db {
  // RR_BUNDLE_DIR / RR_IDX_DIR override the install folder and the index (used to read the HTTPCache *.data bundles).
  public static string BundleDir = Environment.GetEnvironmentVariable("RR_BUNDLE_DIR") ?? @"E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64";
  public static string PathOf(string bundle) { var p = Path.Combine(BundleDir, bundle + ".bundle"); return File.Exists(p) ? p : Path.Combine(BundleDir, bundle + ".data"); }
  public static bool IsUnityFS(string path) { using var s = File.OpenRead(path); var b = new byte[7]; return s.Read(b, 0, 7) == 7 && Encoding.ASCII.GetString(b) == "UnityFS"; }
  public static string IdxDir = Environment.GetEnvironmentVariable("RR_IDX_DIR") ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "idx");
  static Dictionary<string, (byte[] data, List<(long off, long size, string name)> nodes)> _open = new();
  static Dictionary<string, SFile> _files = new();
  public static Dictionary<string, string> CabToBundle;
  public static Dictionary<(string, long), string> Scripts;
  public static void LoadIdx() {
    if (CabToBundle != null) return;
    CabToBundle = new(); Scripts = new();
    foreach (var l in File.ReadLines(Path.Combine(IdxDir, "cabs.tsv"))) { var p = l.Split('\t'); CabToBundle[p[0].ToLowerInvariant()] = p[1]; }
    foreach (var l in File.ReadLines(Path.Combine(IdxDir, "scripts.tsv"))) { var p = l.Split('\t'); Scripts[(p[0], long.Parse(p[1]))] = (p[3].Length > 0 ? p[3] + "." : "") + p[4]; }
  }
  public static SFile File0(string bundle) {
    if (_files.TryGetValue(bundle, out var f)) return f;
    // "bundle#k" = the k-th serialized file of the bundle (scene bundles: #0 = .sharedassets, #1 = the scene).
    int want = 0; string bn = bundle; int hash = bundle.IndexOf('#'); if (hash > 0) { want = int.Parse(bundle.Substring(hash + 1)); bn = bundle.Substring(0, hash); }
    var (data, nodes) = Bundle.Open(PathOf(bn));
    foreach (var n in nodes) {
      if (n.name.EndsWith(".resS") || n.name.EndsWith(".resource") || n.name.EndsWith(".ress")) continue;
      if (want-- > 0) continue;
      f = Ser.Parse(n.name, data, (int)n.off); break;
    }
    _files[bundle] = f; return f;
  }
  public static SObj Obj(SFile f, long pid) => f.Objects.FirstOrDefault(o => o.PathId == pid);
  public static string ResolveScript(string bundle, SFile f, int fileId, long pathId) {
    LoadIdx();
    string b = bundle;
    if (fileId > 0) { var cab = Path.GetFileName(f.Externals[fileId - 1]).ToLowerInvariant(); if (!CabToBundle.TryGetValue(cab, out b)) return "?ext:" + cab; }
    return Scripts.TryGetValue((b, pathId), out var s) ? s : "?script";
  }
}

static class Ctx { public static string Bundle; public static SFile File; }
static class Names {
  static Dictionary<(string, long), (string cls, string name)> _n;
  static Dictionary<(string, long), (long go, string script)> _mb;
  static void Load() {
    if (_n != null) return; _n = new(); _mb = new();
    foreach (var l in File.ReadLines(Path.Combine(Db.IdxDir, "objects.tsv"))) { var p = l.Split('\t'); if (p.Length >= 4 && long.TryParse(p[1], out var pid)) _n[(p[0], pid)] = (p[2].TrimEnd('*'), p[3]); }
    foreach (var l in File.ReadLines(Path.Combine(Db.IdxDir, "mbscripts.tsv"))) { var p = l.Split('\t'); _mb[(p[0], long.Parse(p[1]))] = (long.Parse(p[2]), p[3]); }
  }
  public static (string bundle, long pid) Target(string bundle, SFile f, int fid, long pid) {
    if (fid == 0 || f == null) return (bundle, pid);
    Db.LoadIdx();
    var cab = Path.GetFileName(f.Externals[fid - 1]).ToLowerInvariant();
    return Db.CabToBundle.TryGetValue(cab, out var b) ? (b, pid) : (cab, pid);
  }
  public static string Describe(string bundle, SFile f, int fid, long pid) {
    Load();
    var (b, p) = Target(bundle, f, fid, pid);
    string where = b == bundle ? "" : "@" + (b.Length > 8 ? b.Substring(0, 8) : b);
    if (_n.TryGetValue((b, p), out var nm)) return $"{nm.cls}:{nm.name}{where}#{p}";
    if (_mb.TryGetValue((b, p), out var mb)) { var go = _n.TryGetValue((b, mb.go), out var g) ? g.name : "?"; return $"{mb.script.Split('.').Last()} on {go}{where}#{p}"; }
    if (b == bundle && f != null) { var o = f.Objects.FirstOrDefault(x => x.PathId == p); if (o != null) return $"{ClassNames.Of(o.ClassId)}{where}#{p}"; }
    return $"PPtr({fid},{pid}){where}";
  }
}

static class Json {
  public static void Write(StringBuilder sb, object o, int depth, int maxDepth, int maxArr = 40) {
    switch (o) {
      case null: sb.Append("null"); break;
      case string s: sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")).Append('"'); break;
      case bool b: sb.Append(b ? "true" : "false"); break;
      case byte[] bytes: sb.Append($"\"<{bytes.Length} bytes>\""); break;
      case float fl: sb.Append(fl.ToString("G6", System.Globalization.CultureInfo.InvariantCulture)); break;
      case double db: sb.Append(db.ToString("G8", System.Globalization.CultureInfo.InvariantCulture)); break;
      case Dictionary<string, object> d:
        if (depth >= maxDepth) { sb.Append("{…}"); break; }
        if (d.Count == 2 && d.ContainsKey("m_FileID") && d.ContainsKey("m_PathID")) {
          long pid = Convert.ToInt64(d["m_PathID"]); int fid = Convert.ToInt32(d["m_FileID"]);
          if (pid == 0) { sb.Append("null"); break; }
          sb.Append('"').Append(Names.Describe(Ctx.Bundle, Ctx.File, fid, pid).Replace("\"", "'")).Append('"'); break;
        }
        if (d.Count == 3 && d.ContainsKey("x") && d.ContainsKey("y") && d.ContainsKey("z")) { sb.Append('['); Write(sb, d["x"], 0, 1); sb.Append(','); Write(sb, d["y"], 0, 1); sb.Append(','); Write(sb, d["z"], 0, 1); sb.Append(']'); break; }
        if (d.Count == 4 && d.ContainsKey("r") && d.ContainsKey("g") && d.ContainsKey("b") && d.ContainsKey("a")) { sb.Append("\"rgba("); foreach (var k in new[] { "r", "g", "b", "a" }) { Write(sb, d[k], 0, 1); if (k != "a") sb.Append(','); } sb.Append(")\""); break; }
        sb.Append('{'); bool first = true;
        foreach (var kv in d) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(kv.Key).Append("\":"); Write(sb, kv.Value, depth + 1, maxDepth, maxArr); }
        sb.Append('}'); break;
      case List<object> l:
        if (depth >= maxDepth) { sb.Append($"[…{l.Count}]"); break; }
        sb.Append('[');
        for (int i = 0; i < Math.Min(l.Count, maxArr); i++) { if (i > 0) sb.Append(','); Write(sb, l[i], depth + 1, maxDepth, maxArr); }
        if (l.Count > maxArr) sb.Append($",\"…+{l.Count - maxArr}\"");
        sb.Append(']'); break;
      default: sb.Append(Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture)); break;
    }
  }
  public static string Of(object o, int maxDepth = 6, int maxArr = 40) { var sb = new StringBuilder(); Write(sb, o, 0, maxDepth, maxArr); return sb.ToString(); }
}

static class Raw {
  public static (int fid, long pid) PPtr(BR r) => (r.I32(), r.I64());
  public static string Str(BR r) { int len = r.I32(); if (len < 0 || len > 100000) return "?"; var s = Encoding.UTF8.GetString(r.B, r.P, len); r.P += len; r.Align(4); return s; }
  public static BR At(SFile f, SObj o) => new BR(f.Data, f.DataOff + (int)o.Start, f.BE);
  public static (List<long> comps, string name, int layer, bool active) Go(SFile f, SObj o) {
    var r = At(f, o); int n = r.I32(); var comps = new List<long>();
    for (int i = 0; i < n; i++) { var p = PPtr(r); comps.Add(p.pid); }
    int layer = r.I32(); var name = Str(r); r.U16(); bool act = r.Bool();
    return (comps, name, layer, act);
  }
  public static (long go, float[] rot, float[] pos, float[] scl, List<long> kids, long father) Tr(SFile f, SObj o) {
    var r = At(f, o); var go = PPtr(r).pid;
    var rot = new[] { r.F32(), r.F32(), r.F32(), r.F32() }; var pos = new[] { r.F32(), r.F32(), r.F32() }; var scl = new[] { r.F32(), r.F32(), r.F32() };
    int n = r.I32(); var kids = new List<long>(); for (int i = 0; i < n; i++) kids.Add(PPtr(r).pid);
    var father = PPtr(r).pid; return (go, rot, pos, scl, kids, father);
  }
  public static (long go, bool en, int sfid, long spid, string name) Mb(SFile f, SObj o) {
    var r = At(f, o); var go = PPtr(r).pid; bool en = r.Bool(); r.Align(4); var s = PPtr(r); var name = Str(r); return (go, en, s.fid, s.pid, name);
  }
  public static string Name(SFile f, SObj o) { var r = At(f, o); return Str(r); }
}

static class Program {
  static readonly HashSet<int> Named = new() { 21, 28, 43, 48, 49, 74, 83, 89, 90, 91, 117, 128, 187, 213, 221, 319, 329, 687078895, 241, 243, 273 };
  static int Main(string[] a) {
    Common.Load(@"E:\SteamLibrary\steamapps\common\RecRoom\UnityPlayer.dll");
    Console.OutputEncoding = Encoding.UTF8;
    switch (a[0]) {
      case "index": Index(a.Length > 1 ? a[1] : Db.IdxDir); break;
      case "tree": Tree(a[1], long.Parse(a[2]), a.Length > 3 ? int.Parse(a[3]) : 99, a.Contains("--fields"), a.Contains("--all")); break;
      case "dump": { var f = Db.File0(a[1]); Ctx.Bundle = a[1]; Ctx.File = f; var o = Db.Obj(f, long.Parse(a[2])); Console.WriteLine(Json.Of(Ser.Read(f, o), a.Length > 3 ? int.Parse(a[3]) : 12, 200)); break; }
      case "roots": Roots(a[1]); break;
      case "world": SceneDump.World(a[1]); break;
      case "mats": SceneDump.Mats(a[1]); break;
      case "find": Find(a[1], a.Length > 2 ? a[2] : null); break;
      case "script": ScriptUse(a[1]); break;
      case "containers": { var re = new Regex(a[1], RegexOptions.IgnoreCase); foreach (var l in File.ReadLines(Path.Combine(Db.IdxDir, "containers.tsv"))) if (re.IsMatch(l)) Console.WriteLine(l); break; }
      case "resolve": { var f = Db.File0(a[1]); Console.WriteLine(Names.Describe(a[1], f, int.Parse(a[2]), long.Parse(a[3]))); var t = Names.Target(a[1], f, int.Parse(a[2]), long.Parse(a[3])); Console.WriteLine(t.bundle + "	" + t.pid); break; }
      case "deps": { foreach (var l in File.ReadLines(Path.Combine(Db.IdxDir, "deps.tsv"))) if (l.StartsWith(a[1])) Console.WriteLine(l); break; }
      default: Console.Error.WriteLine("unknown command"); return 1;
    }
    return 0;
  }

  static void Index(string outDir) {
    Directory.CreateDirectory(outDir);
    var files = Directory.GetFiles(Db.BundleDir, "*.bundle").Concat(Directory.GetFiles(Db.BundleDir, "*.data").Where(Db.IsUnityFS)).OrderBy(x => x).ToArray();
    // pass 1: cabs + monoscripts
    using (var cabs = new StreamWriter(Path.Combine(outDir, "cabs.tsv")))
    using (var scripts = new StreamWriter(Path.Combine(outDir, "scripts.tsv")))
      foreach (var path in files) {
        var bn = Path.GetFileNameWithoutExtension(path);
        (byte[] data, List<(long off, long size, string name)> nodes) bo; try { bo = Bundle.Open(path); } catch (Exception e) { Console.Error.WriteLine("skip " + bn + " " + e.Message); continue; }
        var (data, nodes) = bo;
        foreach (var n in nodes) cabs.WriteLine($"{n.name.ToLowerInvariant()}\t{bn}");
        foreach (var n in nodes) {
          if (n.name.EndsWith(".resS") || n.name.EndsWith(".resource")) continue;
          var f = Ser.Parse(n.name, data, (int)n.off);
          foreach (var o in f.Objects.Where(o => o.ClassId == 115)) {
            var ms = Ser.Read(f, o) as Dictionary<string, object>;
            scripts.WriteLine($"{bn}\t{o.PathId}\t{ms["m_AssemblyName"]}\t{ms["m_Namespace"]}\t{ms["m_ClassName"]}");
          }
        }
      }
    Db.IdxDir = outDir; Db.LoadIdx();
    using var objs = new StreamWriter(Path.Combine(outDir, "objects.tsv"));
    using var conts = new StreamWriter(Path.Combine(outDir, "containers.tsv"));
    using var deps = new StreamWriter(Path.Combine(outDir, "deps.tsv"));
    using var mbs = new StreamWriter(Path.Combine(outDir, "mbscripts.tsv"));
    foreach (var path in files) {
      var bn = Path.GetFileNameWithoutExtension(path);
      SFile f; try { f = Db.File0(bn); } catch (Exception e) { Console.Error.WriteLine("skip " + bn + " " + e.Message); continue; }
      var depB = f.Externals.Select(e => Path.GetFileName(e).ToLowerInvariant()).Select(c => Db.CabToBundle.TryGetValue(c, out var b) ? b : c).Distinct();
      deps.WriteLine($"{bn}\t{string.Join(",", depB)}");
      var byId = f.Objects.ToDictionary(o => o.PathId);
      var goNames = new Dictionary<long, string>();
      var goRoot = new Dictionary<long, bool>();
      foreach (var o in f.Objects) {
        try {
          if (o.ClassId == 1) {
            var g = Raw.Go(f, o); goNames[o.PathId] = g.name;
            var compClasses = g.comps.Select(c => byId.TryGetValue(c, out var co) ? co.ClassId : -1).ToList();
            string compDesc = string.Join(",", g.comps.Select(c => {
              if (!byId.TryGetValue(c, out var co)) return "?";
              if (co.ClassId == 114) { var m = Raw.Mb(f, co); return Db.ResolveScript(bn, f, m.sfid, m.spid).Split('.').Last(); }
              return ClassNames.Of(co.ClassId);
            }));
            // root?
            bool root = false;
            var trId = g.comps.FirstOrDefault(c => byId.TryGetValue(c, out var co) && (co.ClassId == 4 || co.ClassId == 224));
            if (trId != 0) { var t = Raw.Tr(f, byId[trId]); root = t.father == 0; }
            objs.WriteLine($"{bn}\t{o.PathId}\tGameObject{(root ? "*" : "")}\t{g.name}\t{compDesc}");
          } else if (o.ClassId == 114) {
            var m = Raw.Mb(f, o);
            var sc = Db.ResolveScript(bn, f, m.sfid, m.spid);
            mbs.WriteLine($"{bn}\t{o.PathId}\t{m.go}\t{sc}\t{m.name}");
          } else if (Named.Contains(o.ClassId)) {
            string nm = o.ClassId == 48 ? ((Ser.Read(f, o) as Dictionary<string, object>)["m_ParsedForm"] as Dictionary<string, object>)["m_Name"] as string : Raw.Name(f, o);
            objs.WriteLine($"{bn}\t{o.PathId}\t{ClassNames.Of(o.ClassId)}\t{nm}\t");
          }
        } catch (Exception e) { objs.WriteLine($"{bn}\t{o.PathId}\t{ClassNames.Of(o.ClassId)}\tERR\t{e.Message}"); }
      }
      foreach (var o in f.Objects.Where(o => o.ClassId == 142)) {
        var ab = Ser.Read(f, o) as Dictionary<string, object>;
        foreach (Dictionary<string, object> pr in (List<object>)ab["m_Container"]) {
          var asset = (Dictionary<string, object>)((Dictionary<string, object>)pr["second"])["asset"];
          long pid = (long)asset["m_PathID"]; int fid = (int)asset["m_FileID"];
          string cls = "?", nm = "?";
          if (fid == 0 && byId.TryGetValue(pid, out var ao)) { cls = ClassNames.Of(ao.ClassId); nm = ao.ClassId == 1 ? goNames.GetValueOrDefault(pid, "?") : (Named.Contains(ao.ClassId) && ao.ClassId != 48 ? Raw.Name(f, ao) : ""); if (ao.ClassId == 114) { var m = Raw.Mb(f, ao); nm = Db.ResolveScript(bn, f, m.sfid, m.spid) + ":" + m.name; } }
          conts.WriteLine($"{bn}\t{pr["first"]}\t{pid}\t{cls}\t{nm}");
        }
      }
    }
    Console.WriteLine("index done");
  }

  static void Roots(string bundle) {
    var f = Db.File0(bundle); var byId = f.Objects.ToDictionary(o => o.PathId);
    foreach (var o in f.Objects.Where(o => o.ClassId == 1)) {
      var g = Raw.Go(f, o);
      var trId = g.comps.FirstOrDefault(c => byId.TryGetValue(c, out var co) && (co.ClassId == 4 || co.ClassId == 224));
      if (trId == 0) continue; var t = Raw.Tr(f, byId[trId]);
      if (t.father == 0) Console.WriteLine($"{o.PathId}\t{g.name}\tkids={t.kids.Count}");
    }
  }

  static void Tree(string bundle, long goId, int maxDepth, bool fields, bool all) {
    var f = Db.File0(bundle); Ctx.Bundle = bundle; Ctx.File = f; var byId = f.Objects.ToDictionary(o => o.PathId);
    void Walk(long gid, int depth) {
      var g = Raw.Go(f, byId[gid]);
      var ind = new string(' ', depth * 2);
      var comps = new List<string>(); long trId = 0;
      foreach (var c in g.comps) {
        if (!byId.TryGetValue(c, out var co)) { comps.Add("?"); continue; }
        if (co.ClassId == 4 || co.ClassId == 224) { trId = c; continue; }
        if (co.ClassId == 114) { var m = Raw.Mb(f, co); comps.Add(Db.ResolveScript(bundle, f, m.sfid, m.spid) + (m.en ? "" : "(off)") + "#" + c); }
        else comps.Add(ClassNames.Of(co.ClassId) + "#" + c);
      }
      string trs = "";
      List<long> kids = new();
      if (trId != 0) { var t = Raw.Tr(f, byId[trId]); kids = t.kids; trs = $" p=({t.pos[0]:0.###},{t.pos[1]:0.###},{t.pos[2]:0.###}) r=({t.rot[0]:0.###},{t.rot[1]:0.###},{t.rot[2]:0.###},{t.rot[3]:0.###}) s=({t.scl[0]:0.###},{t.scl[1]:0.###},{t.scl[2]:0.###})"; }
      Console.WriteLine($"{ind}- {g.name} [go#{gid} L{g.layer}{(g.active ? "" : " inactive")}]{trs}  {string.Join(" ", comps)}");
      if (fields) foreach (var c in g.comps) {
        if (!byId.TryGetValue(c, out var co)) continue;
        if (co.ClassId == 114 || all && co.ClassId != 4 && co.ClassId != 224) {
          try {
            var d = Ser.Read(f, co) as Dictionary<string, object>;
            foreach (var k in new[] { "m_GameObject", "m_Enabled", "m_Script", "m_Name", "m_EditorHideFlags", "m_EditorClassIdentifier", "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" }) d.Remove(k);
            var js = Json.Of(d, 5, 12);
            if (js.Length > 2500) js = js.Substring(0, 2500) + "…";
            Console.WriteLine($"{ind}    #{c}: {js}");
          } catch (Exception e) { Console.WriteLine($"{ind}    #{c}: ERR {e.Message}"); }
        }
      }
      if (depth < maxDepth) foreach (var k in kids) { if (byId.TryGetValue(k, out var ko)) { var t = Raw.Tr(f, ko); Walk(t.go, depth + 1); } }
    }
    Walk(goId, 0);
  }

  static void Find(string rx, string cls) {
    var re = new Regex(rx, RegexOptions.IgnoreCase);
    foreach (var l in File.ReadLines(Path.Combine(Db.IdxDir, "objects.tsv"))) {
      var p = l.Split('\t'); if (p.Length < 4) continue;
      if (cls != null && !p[2].StartsWith(cls)) continue;
      if (re.IsMatch(p[3])) Console.WriteLine(l.Length > 400 ? l.Substring(0, 400) + "…" : l);
    }
  }
  static void ScriptUse(string rx) {
    var re = new Regex(rx, RegexOptions.IgnoreCase);
    foreach (var l in File.ReadLines(Path.Combine(Db.IdxDir, "mbscripts.tsv"))) { var p = l.Split('\t'); if (re.IsMatch(p[3])) Console.WriteLine(l); }
  }
}
