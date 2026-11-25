// rrscene - install side: scans Rec Room's plain Addressables bundles (read-only) with rrtool's Core.cs reader and writes
//   inst_renderers.tsv : bundle, rootPid, rootName, goPath (relative to root, '/'), meshName, materials (comma, per submesh)
//   inst_materials.tsv : materialName, r g b a (_BaseColor/_Color/_MainColor/_TintColor), mainTexture name, shader name
// Used by the scene pass to (a) infer material names for scene renderers whose mesh matches an install mesh,
// (b) verify P (whole prefab) matches by comparing mesh sets, (c) give X stand-ins a colour.
using System.Globalization;

static class Inst {
  public static string RrIdx = @"C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad\rrtool\idx";
  public static string BundleDir = @"E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64";
  public static string UnityPlayer = @"E:\SteamLibrary\steamapps\common\RecRoom\UnityPlayer.dll";

  static (int fid, long pid) PPtr(BR r) => (r.I32(), r.I64());
  static string Str(BR r) { int len = r.I32(); if (len < 0 || len > 100000) return "?"; var s = Encoding.UTF8.GetString(r.B, r.P, len); r.P += len; r.Align(4); return s; }
  static BR At(SFile f, SObj o) => new BR(f.Data, f.DataOff + (int)o.Start, f.BE);

  public static void Scan(string outDir) {
    Common.Load(UnityPlayer);
    var cab = new Dictionary<string, string>();
    foreach (var l in File.ReadLines(Path.Combine(RrIdx, "cabs.tsv"))) { var p = l.Split('\t'); cab[p[0].ToLowerInvariant()] = p[1]; }
    var names = new Dictionary<(string, long), string>();
    foreach (var l in File.ReadLines(Path.Combine(RrIdx, "objects.tsv"))) {
      var p = l.Split('\t'); if (p.Length < 4) continue;
      if (p[2] == "Mesh" || p[2] == "Material" || p[2] == "Texture2D" || p[2] == "Shader") if (long.TryParse(p[1], out var pid)) names[(p[0], pid)] = p[3];
    }
    var files = Directory.GetFiles(BundleDir, "*.bundle").OrderBy(x => x).ToArray();
    using var wr = new StreamWriter(Path.Combine(outDir, "inst_renderers.tsv"));
    using var wm = new StreamWriter(Path.Combine(outDir, "inst_materials.tsv"));
    var matSeen = new HashSet<string>();
    int nb = 0;
    foreach (var path in files) {
      var bn = Path.GetFileNameWithoutExtension(path);
      SFile f = null;
      try {
        var (data, nodes) = Bundle.Open(path);
        foreach (var n in nodes) { if (n.name.EndsWith(".resS") || n.name.EndsWith(".resource")) continue; f = Ser.Parse(n.name, data, (int)n.off); break; }
      } catch { continue; }
      if (f == null) continue;
      nb++;
      string Nm(int fid, long pid) {
        if (pid == 0) return "";
        string b = bn;
        if (fid > 0) { var c = Path.GetFileName(f.Externals[fid - 1]).ToLowerInvariant(); if (!cab.TryGetValue(c, out b)) return "?"; }
        return names.TryGetValue((b, pid), out var s) ? s : "?";
      }
      bool hasR = f.Objects.Any(o => o.ClassId == 23 || o.ClassId == 137);
      if (hasR) {
        var goName = new Dictionary<long, string>(); var goTr = new Dictionary<long, long>();
        var trGo = new Dictionary<long, long>(); var trFather = new Dictionary<long, long>();
        var mf = new Dictionary<long, (int, long)>();
        foreach (var o in f.Objects) {
          try {
            if (o.ClassId == 1) {
              var r = At(f, o); int n = r.I32(); var comps = new List<long>();
              for (int i = 0; i < n; i++) comps.Add(PPtr(r).pid);
              r.I32(); goName[o.PathId] = Str(r);
              goTr[o.PathId] = comps.Count > 0 ? comps[0] : 0; // transform is always component 0
            } else if (o.ClassId == 4 || o.ClassId == 224) {
              var r = At(f, o); var go = PPtr(r).pid; r.P += 40; int n = r.I32(); r.P += n * 12; var fa = PPtr(r).pid;
              trGo[o.PathId] = go; trFather[o.PathId] = fa;
            } else if (o.ClassId == 33) {
              var r = At(f, o); var go = PPtr(r).pid; var m = PPtr(r); mf[go] = m;
            }
          } catch { }
        }
        foreach (var o in f.Objects.Where(o => o.ClassId == 23 || o.ClassId == 137)) {
          try {
            var d = Ser.Read(f, o) as Dictionary<string, object>;
            var gop = (Dictionary<string, object>)d["m_GameObject"]; long go = Convert.ToInt64(gop["m_PathID"]);
            string mesh = "";
            if (o.ClassId == 137 && d.TryGetValue("m_Mesh", out var mm)) { var mp = (Dictionary<string, object>)mm; mesh = Nm(Convert.ToInt32(mp["m_FileID"]), Convert.ToInt64(mp["m_PathID"])); }
            else if (mf.TryGetValue(go, out var m)) mesh = Nm(m.Item1, m.Item2);
            var mats = new List<string>();
            foreach (Dictionary<string, object> mp in (List<object>)d["m_Materials"]) mats.Add(Nm(Convert.ToInt32(mp["m_FileID"]), Convert.ToInt64(mp["m_PathID"])));
            // path to root
            var parts = new List<string>(); long cur = go; long root = go; int guard = 0;
            while (guard++ < 64 && goTr.TryGetValue(cur, out var tr) && tr != 0) {
              parts.Add(goName.GetValueOrDefault(cur, "?")); root = cur;
              var fa = trFather.GetValueOrDefault(tr, 0); if (fa == 0) break;
              cur = trGo.GetValueOrDefault(fa, 0); if (cur == 0) break;
            }
            parts.Reverse();
            string rootName = parts.Count > 0 ? parts[0] : "?";
            string rel = string.Join("/", parts.Skip(1));
            wr.WriteLine($"{bn}\t{root}\t{rootName}\t{rel}\t{mesh}\t{string.Join(",", mats)}");
          } catch { }
        }
      }
      foreach (var o in f.Objects.Where(o => o.ClassId == 21)) {
        try {
          var d = Ser.Read(f, o) as Dictionary<string, object>;
          var name = (string)d["m_Name"]; if (!matSeen.Add(name)) continue;
          var sp = (Dictionary<string, object>)d["m_SavedProperties"];
          double[] col = null; string tex = "";
          var colors = new Dictionary<string, double[]>();
          foreach (Dictionary<string, object> pr in (List<object>)sp["m_Colors"]) {
            var c = (Dictionary<string, object>)pr["second"];
            colors[(string)pr["first"]] = new[] { Convert.ToDouble(c["r"]), Convert.ToDouble(c["g"]), Convert.ToDouble(c["b"]), Convert.ToDouble(c["a"]) };
          }
          foreach (var k in new[] { "_BaseColor", "_Color", "_MainColor", "_TintColor", "_Tint" }) if (colors.TryGetValue(k, out col)) break;
          foreach (Dictionary<string, object> pr in (List<object>)sp["m_TexEnvs"]) {
            var k = (string)pr["first"]; if (k != "_BaseMap" && k != "_MainTex" && k != "_Albedo") continue;
            var te = (Dictionary<string, object>)((Dictionary<string, object>)pr["second"])["m_Texture"];
            var tn = Nm(Convert.ToInt32(te["m_FileID"]), Convert.ToInt64(te["m_PathID"])); if (tn != "") { tex = tn; break; }
          }
          var sh = (Dictionary<string, object>)d["m_Shader"];
          string shader = Nm(Convert.ToInt32(sh["m_FileID"]), Convert.ToInt64(sh["m_PathID"]));
          string cs = col == null ? "-" : string.Join(" ", col.Select(x => x.ToString("0.###", CultureInfo.InvariantCulture)));
          wm.WriteLine($"{name}\t{cs}\t{tex}\t{shader}");
        } catch { }
      }
    }
    Console.WriteLine($"inst scan: {nb} bundles");
  }
}
