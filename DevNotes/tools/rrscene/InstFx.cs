// rrscene - install side, effects and sounds (iteration 5): scans Rec Room's plain Addressables bundles (read-only)
// and writes <work>/inst_fx.tsv, one row per ParticleSystem or AudioSource in any GameObject tree:
//   ps  bundle rootPid rootName rootIsPrefab path  materials  looping duration maxParticles rateOverTime prewarm startSize startLifetime
//   as  bundle rootPid rootName rootIsPrefab path  clip       volume  minDist  maxDist  loop  playOnAwake spatial(0..1)
// rootIsPrefab = 1 when the root is a catalog prefab root ("GameObject*" in rrtool's objects.tsv), i.e. spawnable by
// name through RrAssets/RrSpawn. The scene pass uses this table to match a reference scene's effect objects (F records)
// and to know which install effects already carry their own sound.
using System.Globalization;

static class InstFx {
  static (int fid, long pid) PPtr(BR r) => (r.I32(), r.I64());
  static string Str(BR r) { int len = r.I32(); if (len < 0 || len > 100000) return "?"; var s = Encoding.UTF8.GetString(r.B, r.P, len); r.P += len; r.Align(4); return s; }
  static BR At(SFile f, SObj o) => new BR(f.Data, f.DataOff + (int)o.Start, f.BE);
  static string F(double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);
  static object Get(object d, params string[] path) {
    foreach (var k in path) { if (d is Dictionary<string, object> m && m.TryGetValue(k, out var v)) d = v; else return null; }
    return d;
  }
  static double Num(object o, double def = 0) { try { return o == null ? def : Convert.ToDouble(o, CultureInfo.InvariantCulture); } catch { return def; } }

  public static void Scan(string outDir) {
    Common.Load(Inst.UnityPlayer);
    var cab = new Dictionary<string, string>();
    foreach (var l in File.ReadLines(Path.Combine(Inst.RrIdx, "cabs.tsv"))) { var p = l.Split('\t'); cab[p[0].ToLowerInvariant()] = p[1]; }
    var names = new Dictionary<(string, long), string>();
    var prefabRoots = new HashSet<(string, long)>();
    foreach (var l in File.ReadLines(Path.Combine(Inst.RrIdx, "objects.tsv"))) {
      var p = l.Split('\t'); if (p.Length < 4 || !long.TryParse(p[1], out var pid)) continue;
      if (p[2] == "Material" || p[2] == "AudioClip") names[(p[0], pid)] = p[3];
      else if (p[2] == "GameObject*") prefabRoots.Add((p[0], pid));
    }
    var files = Directory.GetFiles(Inst.BundleDir, "*.bundle").OrderBy(x => x).ToArray();
    using var w = new StreamWriter(Path.Combine(outDir, "inst_fx.tsv"));
    int nb = 0, nps = 0, nas = 0;
    foreach (var path in files) {
      var bn = Path.GetFileNameWithoutExtension(path);
      SFile f = null;
      try {
        var (data, nodes) = Bundle.Open(path);
        foreach (var n in nodes) { if (n.name.EndsWith(".resS") || n.name.EndsWith(".resource")) continue; f = Ser.Parse(n.name, data, (int)n.off); break; }
      } catch { continue; }
      if (f == null || !f.Objects.Any(o => o.ClassId == 198 || o.ClassId == 82)) continue;
      nb++;
      string Nm(int fid, long pid) {
        if (pid == 0) return "";
        string b = bn;
        if (fid > 0) { var c = Path.GetFileName(f.Externals[fid - 1]).ToLowerInvariant(); if (!cab.TryGetValue(c, out b)) return "?"; }
        return names.TryGetValue((b, pid), out var s) ? s : "?";
      }
      var goName = new Dictionary<long, string>(); var goTr = new Dictionary<long, long>();
      var trGo = new Dictionary<long, long>(); var trFather = new Dictionary<long, long>();
      var psrMats = new Dictionary<long, string>();
      foreach (var o in f.Objects) {
        try {
          if (o.ClassId == 1) {
            var r = At(f, o); int n = r.I32(); var comps = new List<long>();
            for (int i = 0; i < n; i++) comps.Add(PPtr(r).pid);
            r.I32(); goName[o.PathId] = Str(r);
            goTr[o.PathId] = comps.Count > 0 ? comps[0] : 0;
          } else if (o.ClassId == 4 || o.ClassId == 224) {
            var r = At(f, o); var go = PPtr(r).pid; r.P += 40; int n = r.I32(); r.P += n * 12; var fa = PPtr(r).pid;
            trGo[o.PathId] = go; trFather[o.PathId] = fa;
          }
        } catch { }
      }
      foreach (var o in f.Objects.Where(o => o.ClassId == 199)) {
        try {
          var d = Ser.Read(f, o) as Dictionary<string, object>;
          long go = Convert.ToInt64(Get(d, "m_GameObject", "m_PathID"));
          var mats = new List<string>();
          if (d["m_Materials"] is List<object> ml) foreach (Dictionary<string, object> mp in ml) mats.Add(Nm(Convert.ToInt32(mp["m_FileID"]), Convert.ToInt64(mp["m_PathID"])));
          psrMats[go] = string.Join(",", mats);
        } catch { }
      }
      (long root, string rootName, string rel) Chain(long go) {
        var parts = new List<string>(); long cur = go, root = go; int guard = 0;
        while (guard++ < 64 && goTr.TryGetValue(cur, out var tr) && tr != 0) {
          parts.Add(goName.GetValueOrDefault(cur, "?")); root = cur;
          var fa = trFather.GetValueOrDefault(tr, 0); if (fa == 0) break;
          cur = trGo.GetValueOrDefault(fa, 0); if (cur == 0) break;
        }
        parts.Reverse();
        return (root, parts.Count > 0 ? parts[0] : "?", string.Join("/", parts.Skip(1)));
      }
      foreach (var o in f.Objects.Where(o => o.ClassId == 198 || o.ClassId == 82)) {
        try {
          var d = Ser.Read(f, o) as Dictionary<string, object>;
          long go = Convert.ToInt64(Get(d, "m_GameObject", "m_PathID"));
          var (root, rootName, rel) = Chain(go);
          int isP = prefabRoots.Contains((bn, root)) ? 1 : 0;
          if (o.ClassId == 198) {
            double dur = Num(Get(d, "lengthInSec"), 5); int loop = Num(Get(d, "looping"), 0) != 0 ? 1 : 0; int pre = Num(Get(d, "prewarm"), 0) != 0 ? 1 : 0;
            double maxP = Num(Get(d, "InitialModule", "maxNumParticles"), 0);
            double rate = Num(Get(d, "EmissionModule", "rateOverTime", "scalar"), 0);
            double size = Num(Get(d, "InitialModule", "startSize", "scalar"), 1), life = Num(Get(d, "InitialModule", "startLifetime", "scalar"), 5);
            w.WriteLine($"ps\t{bn}\t{root}\t{rootName}\t{isP}\t{rel}\t{psrMats.GetValueOrDefault(go, "")}\t{loop}\t{F(dur)}\t{F(maxP)}\t{F(rate)}\t{pre}\t{F(size)}\t{F(life)}");
            nps++;
          } else {
            var clip = Get(d, "m_audioClip") as Dictionary<string, object>;
            string cn = clip == null ? "" : Nm(Convert.ToInt32(clip["m_FileID"]), Convert.ToInt64(clip["m_PathID"]));
            double spatial = 1;
            if (Get(d, "panLevelCustomCurve", "m_Curve") is List<object> keys && keys.Count > 0) spatial = Num(Get(keys[0], "value"), 1);
            w.WriteLine($"as\t{bn}\t{root}\t{rootName}\t{isP}\t{rel}\t{cn}\t{F(Num(Get(d, "m_Volume"), 1))}\t{F(Num(Get(d, "MinDistance"), 1))}\t{F(Num(Get(d, "MaxDistance"), 500))}\t{(Num(Get(d, "Loop"), 0) != 0 ? 1 : 0)}\t{(Num(Get(d, "m_PlayOnAwake"), 1) != 0 ? 1 : 0)}\t{F(spatial)}");
            nas++;
          }
        } catch { }
      }
    }
    Console.WriteLine($"instfx: {nb} bundles, {nps} particle systems, {nas} audio sources");
  }
}
