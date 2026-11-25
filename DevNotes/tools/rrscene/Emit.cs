// rrscene - scene pass: resolves references, matches against the install, writes RRSK 1 + summaries.
using System.Globalization;
using System.Text.RegularExpressions;

class Stats {
  public string Scene, File; public long Bytes, OutBytes; public int Rend, RendNB, Matched, PCov, Builtin, R, P, X, U, Col, Light, Aud, G, Slots, SlotsKnown, Inactive, Lod, Pi, F;
  // iteration 5: sounds (scene name, install clip or null, how, loop, object, volume, min, max), effects placed / not placed
  public List<(string name, string clip, string how, bool loop, string obj, double vol, double min, double max)> Sounds = new();
  public List<string> FxPlaced = new(), FxTriggered = new(), FxUnmatched = new(), FxInPrefab = new(); public List<string> ClipList = new();
  public V3 Min, Max; public bool HasB; public List<(string, int)> TopUnm = new(); public Dictionary<string, int> GKinds = new(); public string Env = "";
  public double MeshPct => RendNB == 0 ? 0 : 100.0 * (Matched + PCov) / RendNB;
  public double MatPct => Slots == 0 ? 0 : 100.0 * SlotsKnown / Slots;
  public string Verdict => Rend == 0 ? "empty" : MeshPct >= 70 ? "rebuildable from the install" : MeshPct >= 30 ? "partly" : "mostly stand-ins";
}

static class Emit {
  static readonly bool Dbg = Environment.GetEnvironmentVariable("RRSCENE_DEBUG") == "1";   // appends " #name" to X lines (debug only; not RRSK)
  const string BuiltinGuid = "0000000000000000e000000000000000";
  static string ScenesDir => Path.Combine(Ref2.Root, ".unity files");
  static IEnumerable<string> Scenes(string filter) => Directory.EnumerateFiles(ScenesDir, "*.unity", SearchOption.AllDirectories)
    .Where(f => filter == null || Regex.IsMatch(Path.GetFileName(f), filter, RegexOptions.IgnoreCase)).OrderBy(f => new FileInfo(f).Length);
  public static string Short(string path) { var n = Path.GetFileNameWithoutExtension(path); int i = n.IndexOf('-'); return i > 0 ? n.Substring(i + 1) : n; }
  static string Norm(string n) {
    var s = (n ?? "").Trim(); string prev;
    do { prev = s; s = Regex.Replace(s, @"\s*\(\d+\)$", "").Trim(); if (s.EndsWith("(Clone)")) s = s.Substring(0, s.Length - 7).Trim(); } while (s != prev);
    return s.Length == 0 ? (n ?? "").Trim() : s;
  }
  static readonly HashSet<string> PStop = new(StringComparer.OrdinalIgnoreCase) { "Cube", "Sphere", "Plane", "Quad", "Cylinder", "Capsule", "Mesh", "Model", "Root", "Art", "Visual", "Visuals", "Collider", "Colliders", "Geo", "Geometry", "LOD0", "LOD1", "Light", "Lights", "GameObject", "Audio", "Particles", "Base", "Top", "Body", "Handle", "Door", "Frame", "Trigger" };
  // scene copies of spawnables are named "[X]"; the install prefab root is "(X)"
  static string BracketP(string n) { if (n.Length > 2 && n[0] == '[' && n[^1] == ']') { var p = "(" + n.Substring(1, n.Length - 2) + ")"; if (InstDb.Prefab.Contains(p)) return p; } return null; }
  static string BuiltinName(long fid) => fid switch { 10202 => "Cube", 10206 => "Cylinder", 10207 => "Sphere", 10208 => "Capsule", 10209 => "Plane", 10210 => "Quad", _ => "Builtin" + fid };

  public static string Kind(string name) {
    var n = (name ?? "").ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
    if (n.Length == 0) return null;
    if (n.Contains("enemyspawn") || n.Contains("spawner") || n.Contains("wavespawn") || n.Contains("aispawn")) return "enemy_spawner";
    if (n.Contains("respawn")) return "respawn";
    if (n.Contains("spawnpoint") || n.Contains("playerspawn") || n.Contains("spawnlocation") || n.StartsWith("spawn")) return "spawn";
    if (n.Contains("checkpoint")) return "checkpoint";
    if (n.Contains("flagbase") || n.Contains("flagstand") || n.Contains("flagspawn") || n.Contains("capturezone") || n.Contains("capturepoint")) return "flag_base";
    if (n.Contains("weaponrack") || n.Contains("gunrack") || n.Contains("weaponspawn") || n.Contains("gunspawn") || (n.Contains("rack") && !n.Contains("track") && !n.Contains("crack") && !n.Contains("bracket"))) return "weapon_rack";
    if (n.Contains("lever")) return "lever";
    if (n.Contains("button")) return "button";
    if (n.Contains("teleport") || n.Contains("portal")) return "teleport";
    if (n.Contains("killzone") || n.Contains("deathzone") || n.Contains("killplane") || n.Contains("killvolume") || n.Contains("outofbounds") || n.Contains("killbox")) return "kill_zone";
    if (n.Contains("winzone") || n.Contains("goal") || n.Contains("finishline") || n.Contains("victory") || n.Contains("endzone")) return "win_zone";
    if (n.Contains("door")) return "door";
    if (n.Contains("trigger")) return "trigger_zone";
    if (n.Contains("chest") || n.Contains("loot") || n.Contains("pickup") || n.Contains("coin") || n.Contains("potion")) return "pickup";
    if (n.Contains("boss")) return "boss";
    return null;
  }

  // --- per scene context -------------------------------------------------------------------------------------------
  class Ctx {
    public SceneModel M; Dictionary<long, TRS> _w = new(); Dictionary<long, bool> _act = new(); public Dictionary<long, PiI> PiById = new();
    public Dictionary<long, List<RendI>> GoRend = new(); public Dictionary<long, List<MbI>> GoMb = new(); public Dictionary<long, List<ColI>> GoCol = new();
    public Ctx(SceneModel m) {
      M = m; foreach (var p in m.Pi) PiById[p.Id] = p;
      foreach (var r in m.Rend) { if (!GoRend.TryGetValue(r.Go, out var l)) GoRend[r.Go] = l = new(); l.Add(r); }
      foreach (var b in m.Mb) { if (!GoMb.TryGetValue(b.Go, out var l)) GoMb[b.Go] = l = new(); l.Add(b); }
      foreach (var c in m.Col) { if (!GoCol.TryGetValue(c.Go, out var l)) GoCol[c.Go] = l = new(); l.Add(c); }
    }
    public static TRS Compose(TRS p, V3 lp, Q lr, V3 ls) => new TRS { p = p.p + p.r.Rot(p.s.Mul(lp)), r = (p.r * lr).Norm(), s = p.s.Mul(ls) };
    public static TRS Id => new TRS { p = default, r = Q.I, s = new V3(1, 1, 1) };
    public (V3 p, Q r, V3 s, long father) Local(TrI t) {
      if (!t.Stripped || !PiById.TryGetValue(t.PrefabInst, out var pi)) return (t.P, t.R, t.S, t.Father);
      var (p, r, s) = PiLocal(pi, t.SrcFid); return (p, r, s, pi.Parent);
    }
    public static (V3, Q, V3) PiLocal(PiI pi, long target) {
      var d = new Dictionary<string, double>();
      foreach (var m in pi.Mods) if (m.tfid == target && m.path != null && (m.path.StartsWith("m_LocalPosition.") || m.path.StartsWith("m_LocalRotation.") || m.path.StartsWith("m_LocalScale."))) d[m.path] = Y.D(m.val);
      var p = new V3(d.GetValueOrDefault("m_LocalPosition.x"), d.GetValueOrDefault("m_LocalPosition.y"), d.GetValueOrDefault("m_LocalPosition.z"));
      var q = new Q(d.GetValueOrDefault("m_LocalRotation.x"), d.GetValueOrDefault("m_LocalRotation.y"), d.GetValueOrDefault("m_LocalRotation.z"), d.GetValueOrDefault("m_LocalRotation.w", 1)).Norm();
      var s = new V3(d.GetValueOrDefault("m_LocalScale.x", 1), d.GetValueOrDefault("m_LocalScale.y", 1), d.GetValueOrDefault("m_LocalScale.z", 1));
      return (p, q, s);
    }
    public TRS World(long trId, int depth = 0) {
      if (trId == 0 || depth > 200 || !M.Tr.TryGetValue(trId, out var t)) return Id;
      if (_w.TryGetValue(trId, out var w)) return w;
      var (p, r, s, fa) = Local(t);
      w = Compose(World(fa, depth + 1), p, r, s); _w[trId] = w; return w;
    }
    public TRS GoW(long go) => M.GoTr.TryGetValue(go, out var t) ? World(t) : Id;
    public bool Active(long go, int depth = 0) {
      if (go == 0) return true; if (_act.TryGetValue(go, out var a)) return a;
      if (!M.Go.TryGetValue(go, out var g)) return true;
      a = g.Active; if (a && depth < 200 && M.GoTr.TryGetValue(go, out var t) && M.Tr.TryGetValue(t, out var tr)) { var fa = Local(tr).father; if (fa != 0 && M.Tr.TryGetValue(fa, out var ft)) a = Active(ft.Go, depth + 1); }
      _act[go] = a; return a;
    }
    // AssetRipper leaves some GameObject names empty: fall back to the mesh name
    public string Name(long go) { var n = M.Go.TryGetValue(go, out var g) ? g.Name : ""; if (n.Length == 0 && M.Mf.TryGetValue(go, out var mf)) n = Ref2.Mesh(mf.guid)?.Name ?? ""; return n; }
  }

  // --- global tables ---------------------------------------------------------------------------------------------
  static Dictionary<string, (string name, double share, int n)> MatVotes = new();
  static Dictionary<string, string> ScriptLabel = new();
  static void LoadTables(string work) {
    Ref2.LoadGuids(Path.Combine(work, "guidmap.tsv"));
    InstDb.Load(work);
    FxDb.Load(work);
    var mv = Path.Combine(work, "matvotes.tsv");
    if (File.Exists(mv)) foreach (var l in File.ReadLines(mv)) { var p = l.Split('\t'); if (p.Length >= 4) MatVotes[p[0]] = (p[1], Y.D(p[2]), (int)Y.D(p[3])); }
    var sl = Path.Combine(work, "scriptlabels.tsv");
    if (File.Exists(sl)) foreach (var l in File.ReadLines(sl)) { var p = l.Split('\t'); if (p.Length >= 3 && p[2] != "-" && p[0] != "guid") ScriptLabel[p[0]] = p[2]; }
  }

  class MeshRes { public string Disp, Match; public MeshInfo B; public long Builtin; public bool Unresolved, Combined; }
  static MeshRes ResolveMesh(Ref mesh, string goName) {
    var r = new MeshRes();
    if (mesh.guid == BuiltinGuid) { r.Builtin = mesh.fid; r.Disp = BuiltinName(mesh.fid); return r; }
    var mi = Ref2.Mesh(mesh.guid);
    var gn = Norm(goName);
    // static-batched renderers point at "Combined Mesh (root: scene)" (world-space, per material): use the GameObject name instead
    if (mi != null && mi.Name.StartsWith("Combined Mesh")) { r.Combined = true; mi = null; }
    if (mi != null) { r.Disp = mi.Name; if (InstDb.Mesh.Contains(mi.Name)) r.Match = mi.Name; }
    else { r.Unresolved = !r.Combined; r.Disp = gn.Length > 0 ? gn : "?" + mesh.guid; }
    if (r.Match == null && gn.Length > 0 && InstDb.Mesh.Contains(gn)) r.Match = gn;
    r.B = mi != null && mi.HasB ? mi : Ref2.Glb(mi?.Name) ?? (mi == null ? Ref2.MeshByName(gn) : null) ?? Ref2.Glb(gn);
    if (r.B != null && !r.B.HasB) r.B = null;
    return r;
  }
  static string MostCommonAt(string mesh, int slot) {
    if (mesh == null || !InstDb.MeshMats.TryGetValue(mesh, out var ls)) return null;
    return ls.Where(x => slot < x.Length && x[slot] != "" && x[slot] != "?").GroupBy(x => x[slot]).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
  }
  static (string name, bool known, double[] col) ResolveMat(Ref r, string meshMatch, int slot) {
    var mi = Ref2.Mat(r.guid);
    double[] col = mi?.Col;
    if (mi != null && InstDb.Mat.Contains(mi.Name)) return (mi.Name, true, col ?? InstDb.MatCol.GetValueOrDefault(mi.Name));
    if (r.guid != null && MatVotes.TryGetValue(r.guid, out var v) && v.share >= 0.5 && InstDb.Mat.Contains(v.name)) return (v.name, true, col ?? InstDb.MatCol.GetValueOrDefault(v.name));
    var inf = MostCommonAt(meshMatch, slot);
    if (inf != null && InstDb.Mat.Contains(inf)) return (inf, true, col ?? InstDb.MatCol.GetValueOrDefault(inf));
    if (mi != null) return ("?" + mi.Name, false, col);
    return ("?" + (r.guid ?? "none"), false, col);
  }

  // --- pass 1: votes ------------------------------------------------------------------------------------------------
  public static void Scan(string work, string filter) {
    LoadTables(work);
    var votes = new Dictionary<string, Dictionary<string, double>>(); var uses = new Dictionary<string, int>();
    var sv = new Dictionary<string, Dictionary<string, int>>(); var sTot = new Dictionary<string, int>(); var sNames = new Dictionary<string, Dictionary<string, int>>();
    var sFields = new Dictionary<string, HashSet<string>>(); var sScenes = new Dictionary<string, HashSet<string>>();
    foreach (var path in Scenes(filter)) {
      var m = Parse.Load(path); var sc = Short(path);
      foreach (var r in m.Rend) {
        var mesh = r.Skinned ? r.Mesh : m.Mf.GetValueOrDefault(r.Go);
        var gn = m.Go.TryGetValue(r.Go, out var g) ? g.Name : "";
        var mr = ResolveMesh(mesh, gn); if (mr.Match == null || !InstDb.MeshMats.TryGetValue(mr.Match, out var ls)) continue;
        for (int k = 0; k < r.Mats.Count; k++) {
          var gd = r.Mats[k].guid; if (gd == null) continue;
          var names = ls.Where(x => k < x.Length && x[k] != "" && x[k] != "?").Select(x => x[k]).Distinct().ToList(); if (names.Count == 0) continue;
          if (!votes.TryGetValue(gd, out var vd)) votes[gd] = vd = new();
          foreach (var n in names) vd[n] = vd.GetValueOrDefault(n) + 1.0 / names.Count;
          uses[gd] = uses.GetValueOrDefault(gd) + 1;
        }
      }
      foreach (var b in m.Mb) {
        var gn = m.Go.TryGetValue(b.Go, out var g) ? g.Name : ""; var kd = Kind(gn);
        sTot[b.Script] = sTot.GetValueOrDefault(b.Script) + 1;
        if (!sv.TryGetValue(b.Script, out var d)) sv[b.Script] = d = new(); if (kd != null) d[kd] = d.GetValueOrDefault(kd) + 1;
        if (!sNames.TryGetValue(b.Script, out var nd)) sNames[b.Script] = nd = new(); var nn = Norm(gn); if (nd.Count < 400 || nd.ContainsKey(nn)) nd[nn] = nd.GetValueOrDefault(nn) + 1;
        if (!sFields.TryGetValue(b.Script, out var fs)) sFields[b.Script] = fs = new(); foreach (var f in b.Fields) fs.Add(f);
        if (!sScenes.TryGetValue(b.Script, out var ss)) sScenes[b.Script] = ss = new(); ss.Add(sc);
      }
      Console.WriteLine($"scan {sc}: {m.Go.Count} go, {m.Rend.Count} rend, {m.Mb.Count} mb");
    }
    using (var w = new StreamWriter(Path.Combine(work, "matvotes.tsv")))
      foreach (var kv in votes) { var tot = kv.Value.Values.Sum(); var best = kv.Value.OrderByDescending(x => x.Value).First(); w.WriteLine($"{kv.Key}\t{best.Key}\t{(best.Value / tot).ToString("0.###", Y.Inv)}\t{uses[kv.Key]}"); }
    using (var w = new StreamWriter(Path.Combine(work, "scriptlabels.tsv"))) {
      w.WriteLine("guid\tcount\tlabel\tscenes\tfields\ttopGameObjectNames");
      foreach (var kv in sTot.OrderByDescending(x => x.Value)) {
        string label = "-"; var d = sv[kv.Key];
        if (d.Count > 0) { var best = d.OrderByDescending(x => x.Value).First(); if (best.Value >= 3 && best.Value >= 0.6 * kv.Value) label = best.Key; }
        var top = string.Join(" | ", sNames[kv.Key].OrderByDescending(x => x.Value).Take(6).Select(x => $"{x.Key} x{x.Value}"));
        w.WriteLine($"{kv.Key}\t{kv.Value}\t{label}\t{sScenes[kv.Key].Count}\t{(sFields[kv.Key].Count == 0 ? "(none)" : string.Join(",", sFields[kv.Key].Take(20)))}\t{top}");
      }
    }
    Console.WriteLine($"votes: {votes.Count} material guids; scripts: {sTot.Count}");
  }

  // --- pass 2: write ------------------------------------------------------------------------------------------------
  public static void Write(string work, string outDir, string filter) {
    LoadTables(work); Directory.CreateDirectory(outDir); var statDir = Path.Combine(work, "stats"); Directory.CreateDirectory(statDir);
    foreach (var path in Scenes(filter)) {
      var st = One(path, outDir);
      File.WriteAllText(Path.Combine(statDir, st.Scene + ".tsv"), StatLine(st));
      WriteIndex(statDir, outDir);
      Console.WriteLine($"{st.Scene}: rend {st.Rend} mesh {st.MeshPct:0}% mat {st.MatPct:0}% R{st.R} P{st.P} X{st.X} U{st.U} C{st.Col} L{st.Light} A{st.Aud} F{st.F} G{st.G} -> {st.Verdict}");
    }
    WriteIndex(statDir, outDir);
    if (File.Exists(Path.Combine(work, "scriptlabels.tsv"))) File.Copy(Path.Combine(work, "scriptlabels.tsv"), Path.Combine(outDir, "_scripts.tsv"), true);
  }

  static Stats One(string path, string outDir) {
    var m = Parse.Load(path); var cx = new Ctx(m); var st = new Stats { Scene = Short(path), File = Path.GetFileName(path), Bytes = m.Bytes };
    ClipList(path, st);
    var meshT = new Dictionary<string, int>(); var matT = new Dictionary<string, int>();
    int MI(string n) { if (!meshT.TryGetValue(n, out var i)) meshT[n] = i = meshT.Count; return i; }
    int TI(string n) { if (!matT.TryGetValue(n, out var i)) matT[n] = i = matT.Count; return i; }
    var body = new List<string>(); var unm = new Dictionary<string, int>();
    void B(V3 p) { if (double.IsNaN(p.x) || double.IsInfinity(p.x) || Math.Abs(p.x) > 1e6 || Math.Abs(p.y) > 1e6 || Math.Abs(p.z) > 1e6) return; if (!st.HasB) { st.Min = st.Max = p; st.HasB = true; return; } st.Min = new(Math.Min(st.Min.x, p.x), Math.Min(st.Min.y, p.y), Math.Min(st.Min.z, p.z)); st.Max = new(Math.Max(st.Max.x, p.x), Math.Max(st.Max.y, p.y), Math.Max(st.Max.z, p.z)); }
    void BBox(V3 c, V3 e, Q r) { for (int i = 0; i < 8; i++) B(c + r.Rot(new V3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z))); }
    string T3(V3 v) => $"{Y.F(v.x)} {Y.F(v.y)} {Y.F(v.z)}";
    string T4(Q q) => $"{Y.F(q.x)} {Y.F(q.y)} {Y.F(q.z)} {Y.F(q.w)}";
    string Col(double[] c) => c == null ? "0.6 0.6 0.6 1" : $"{Y.F(c[0])} {Y.F(c[1])} {Y.F(c[2])} {Y.F(c.Length > 3 ? c[3] : 1)}";

    // P detection (top-down); every GO under an accepted prefab root is covered by the P record.
    var pGo = new HashSet<long>();
    var roots = m.Tr.Where(kv => cx.Local(kv.Value).father == 0).Select(kv => kv.Key).ToList();
    List<long> SubGos(long go) { var res = new List<long>(); var stack = new Stack<long>(); stack.Push(go); while (stack.Count > 0) { var g = stack.Pop(); res.Add(g); if (m.GoTr.TryGetValue(g, out var t) && m.Kids.TryGetValue(t, out var ks)) foreach (var k in ks) if (m.Tr.TryGetValue(k, out var kt) && kt.Go != 0) stack.Push(kt.Go); } return res; }
    string IsP(long go) {   // -> install prefab root name, or null
      var n = Norm(cx.Name(go)); var bn = BracketP(n); if (bn != null) return bn;
      if (n.Length < 3 || PStop.Contains(n) || !InstDb.Prefab.Contains(n)) return null;
      if (n.StartsWith("(") && n.EndsWith(")") || n.StartsWith("[") && n.EndsWith("]")) return n;   // spawnable / kit prefab naming
      if (!InstDb.PrefabMeshes.TryGetValue(n, out var pm) || pm.Count == 0) return null;
      int tot = 0, hit = 0;
      foreach (var g in SubGos(go)) if (cx.GoRend.TryGetValue(g, out var rl)) foreach (var r in rl) { tot++; var mr = ResolveMesh(r.Skinned ? r.Mesh : m.Mf.GetValueOrDefault(r.Go), cx.Name(g)); if (mr.Match != null && pm.Contains(mr.Match) || mr.Disp != null && pm.Contains(mr.Disp)) hit++; }
      return hit >= 1 && hit * 2 >= tot ? n : null;
    }
    void Walk(long trId, int depth) {
      if (!m.Tr.TryGetValue(trId, out var t) || depth > 200) return;
      string pn = t.Go != 0 ? IsP(t.Go) : null;
      if (pn != null) {
        var w = cx.World(trId); var n = pn;
        if (cx.Active(t.Go)) { body.Add($"P {Y.Enc(n)} {T3(w.p)} {T4(w.r)} {T3(w.s)}"); st.P++; B(w.p); }
        foreach (var g in SubGos(t.Go)) pGo.Add(g);
        return;
      }
      if (m.Kids.TryGetValue(trId, out var ks)) foreach (var k in ks) Walk(k, depth + 1);
    }
    foreach (var r in roots) Walk(r, 0);

    // renderers
    foreach (var r in m.Rend) {
      if (m.LodSkip.Contains(r.Id)) { st.Lod++; continue; }
      if (!r.En || !cx.Active(r.Go)) { st.Inactive++; continue; }
      var mesh = r.Skinned ? r.Mesh : m.Mf.GetValueOrDefault(r.Go);
      if (mesh.Null) continue;
      st.Rend++;
      var gn = cx.Name(r.Go); var mr = ResolveMesh(mesh, gn);
      if (mr.Builtin == 0) st.RendNB++; else st.Builtin++;
      if (pGo.Contains(r.Go)) { if (mr.Builtin == 0) st.PCov++; continue; }
      var w = cx.GoW(r.Go);
      var mats = new List<(string name, bool known, double[] col)>();
      for (int k = 0; k < Math.Max(1, r.Mats.Count); k++) { (string name, bool known, double[] col) mt = k < r.Mats.Count ? ResolveMat(r.Mats[k], mr.Match, k) : ("?none", false, null); mats.Add(mt); st.Slots++; if (mt.known) st.SlotsKnown++; }
      if (mr.Match != null) {
        st.Matched++; st.R++;
        body.Add($"R {MI(mr.Match)} {string.Join(",", mats.Select(x => TI(x.name)))} {T3(w.p)} {T4(w.r)} {T3(w.s)}");
        if (mr.B != null) BBox(w.p + w.r.Rot(w.s.Mul((mr.B.Min + mr.B.Max) * 0.5)), (mr.B.Max - mr.B.Min).Mul(w.s.Abs()) * 0.5, w.r); else B(w.p);
        continue;
      }
      string shape = "box"; V3 c, e; bool ok = true;
      if (mr.Builtin != 0) {
        switch (mr.Builtin) { case 10206: case 10208: shape = "cyl"; c = default; e = new V3(0.5, 1, 0.5); break; case 10209: c = default; e = new V3(5, 0.001, 5); break; case 10210: c = default; e = new V3(0.5, 0.5, 0.001); break; default: c = default; e = new V3(0.5, 0.5, 0.5); break; }
      } else if (mr.B != null) { c = (mr.B.Min + mr.B.Max) * 0.5; e = (mr.B.Max - mr.B.Min) * 0.5; }
      else { var bc = cx.GoCol.GetValueOrDefault(r.Go)?.FirstOrDefault(x => x.Cls == 65); if (bc != null) { c = bc.C; e = bc.Size.Abs() * 0.5; } else { c = default; e = default; ok = false; } }
      if (mr.Builtin == 0) unm[mr.Disp] = unm.GetValueOrDefault(mr.Disp) + 1;
      if (!ok) { body.Add($"#U {Y.Enc(mr.Disp)} {T3(w.p)} {T4(w.r)} {T3(w.s)}"); st.U++; B(w.p); continue; }
      var wc = w.p + w.r.Rot(w.s.Mul(c)); var we = e.Mul(w.s.Abs());
      body.Add($"X {shape} {T3(wc)} {T3(we)} {T4(w.r)} {Col(mats[0].col)}" + (Dbg ? " #" + Y.Enc(mr.Disp) : "")); st.X++; BBox(wc, we, w.r);
    }

    // colliders (+ trigger -> G), lights, audio
    var gDone = new HashSet<long>();
    foreach (var c in m.Col) {
      if (!c.En || !cx.Active(c.Go) || pGo.Contains(c.Go)) continue;
      var w = cx.GoW(c.Go); var cc = w.p + w.r.Rot(w.s.Mul(c.C)); var sa = w.s.Abs();
      string geo; string gkeys;
      switch (c.Cls) {
        case 65: { var sz = c.Size.Abs().Mul(sa); geo = $"box {T3(cc)} {T4(w.r)} {T3(sz)}"; gkeys = $"shape=box size={Y.F(sz.x)},{Y.F(sz.y)},{Y.F(sz.z)}"; BBox(cc, sz * 0.5, w.r); break; }
        case 135: { var rr = c.Rad * sa.Max(); geo = $"sphere {T3(cc)} {Y.F(rr)}"; gkeys = $"shape=sphere r={Y.F(rr)}"; B(cc); break; }
        case 136: {
          Q axis = c.Dir == 0 ? new Q(0, 0, -0.7071068, 0.7071068) : c.Dir == 2 ? new Q(0.7071068, 0, 0, 0.7071068) : Q.I;
          double hs = c.Dir == 0 ? sa.x : c.Dir == 2 ? sa.z : sa.y; double rs = c.Dir == 0 ? Math.Max(sa.y, sa.z) : c.Dir == 2 ? Math.Max(sa.x, sa.y) : Math.Max(sa.x, sa.z);
          var q = (w.r * axis).Norm(); geo = $"capsule {T3(cc)} {T4(q)} {Y.F(c.Rad * rs)} {Y.F(c.H * hs)}"; gkeys = $"shape=capsule r={Y.F(c.Rad * rs)} h={Y.F(c.H * hs)}"; B(cc); break; }
        default: {
          var mr = ResolveMesh(c.Mesh.Null ? m.Mf.GetValueOrDefault(c.Go) : c.Mesh, cx.Name(c.Go));
          if (mr.Match != null) { geo = $"mesh {MI(mr.Match)} {T3(w.p)} {T4(w.r)} {T3(w.s)}"; gkeys = $"shape=mesh mesh={Y.Enc(mr.Match)}"; }
          else if (mr.Builtin == 10202 || mr.B != null) { var mc = mr.B != null ? (mr.B.Min + mr.B.Max) * 0.5 : default; var me = mr.B != null ? (mr.B.Max - mr.B.Min) : new V3(1, 1, 1); var wc = w.p + w.r.Rot(w.s.Mul(mc)); var sz = me.Mul(sa); geo = $"box {T3(wc)} {T4(w.r)} {T3(sz)}"; gkeys = $"shape=box size={Y.F(sz.x)},{Y.F(sz.y)},{Y.F(sz.z)}"; cc = wc; }
          else { geo = null; gkeys = "shape=unknown"; }
          B(w.p); break; }
      }
      if (c.Trig) {
        var kd = Kind(cx.Name(c.Go)) ?? LabelOf(cx, c.Go) ?? "trigger_zone";
        body.Add($"G {kd} {Y.Enc(cx.Name(c.Go))} {T3(cc)} {T4(w.r)} {gkeys}"); st.G++; st.GKinds[kd] = st.GKinds.GetValueOrDefault(kd) + 1; gDone.Add(c.Go);
      } else if (geo != null) { body.Add("C " + geo); st.Col++; }
    }
    foreach (var l in m.Lights) {
      if (!l.En || !cx.Active(l.Go)) continue; var w = cx.GoW(l.Go);
      string ty = l.Type switch { 0 => "spot", 1 => "dir", 3 => "area", _ => "point" };
      body.Add($"L {ty} {T3(w.p)} {T4(w.r)} {Y.F(l.Col[0])} {Y.F(l.Col[1])} {Y.F(l.Col[2])} {Y.F(l.Int)} {Y.F(l.Range)} {Y.F(l.Spot)}"); st.Light++; if (l.Type != 1) B(w.p);
    }
    foreach (var a in m.Aud) {
      if (!a.En || !cx.Active(a.Go) || a.Clip.Null) continue; var w = cx.GoW(a.Go);
      var fn = Ref2.FileName(a.Clip.guid); string nm = fn != null ? Path.GetFileNameWithoutExtension(fn) : null;
      // exact install clip, else the same name in other case / another take number, else (loops only) the install
      // ambience of the same kind (fire, water, crowd, cave, wind, lava); the original name is kept as was=
      var (clip, how) = Fx.Clip(nm, a.Loop);
      string outn = clip ?? "?" + (nm ?? a.Clip.guid);
      string was = clip != null && how != "exact" ? " was=" + Y.Enc(nm) : "";
      // Unity roll-off (0 logarithmic is the default and left out) and spatial blend (1 = 3D left out)
      if (a.Roll != 0) was += " roll=" + a.Roll;
      if (a.Pan < 0.999) was += " pan=" + Y.F(a.Pan);
      body.Add($"A {Y.Enc(outn)} {T3(w.p)} {Y.F(a.Vol)} {Y.F(a.Min)} {Y.F(a.Max)} {(a.Loop ? 1 : 0)}{was}"); st.Aud++; B(w.p);
      st.Sounds.Add((nm ?? "?" + a.Clip.guid, clip, how, a.Loop, cx.Name(a.Go), a.Vol, a.Min, a.Max));
    }
    Effects(cx, m, pGo, body, st, B, T3, T4);
    // gameplay markers from names / script labels
    foreach (var kv in m.Go) {
      if (gDone.Contains(kv.Key) || pGo.Contains(kv.Key)) continue;
      var kd = Kind(kv.Value.Name); string src = "name";
      if (kd == null) { kd = LabelOf(cx, kv.Key); src = "script"; }
      if (kd == null) continue;
      bool hasMb = cx.GoMb.ContainsKey(kv.Key), hasR = cx.GoRend.ContainsKey(kv.Key);
      if (src == "name" && !hasMb && hasR) continue;          // decorative mesh part that only carries the word
      if (kd == "door" && !hasMb) continue;
      var w = cx.GoW(kv.Key); var act = cx.Active(kv.Key);
      body.Add($"G {kd} {Y.Enc(kv.Value.Name)} {T3(w.p)} {T4(w.r)} src={src}{(act ? "" : " active=0")}"); st.G++; st.GKinds[kd] = st.GKinds.GetValueOrDefault(kd) + 1; B(w.p);
    }
    // prefab instances (source prefabs are not in the reference set; keep name + root placement)
    foreach (var pi in m.Pi) {
      var tgt = pi.Mods.Where(x => x.path != null && x.path.StartsWith("m_LocalPosition")).GroupBy(x => x.tfid).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
      var (lp, lr, ls) = Ctx.PiLocal(pi, tgt); var w = Ctx.Compose(cx.World(pi.Parent), lp, lr, ls);
      var nm = pi.Mods.FirstOrDefault(x => x.path == "m_Name").val; var srcName = Ref2.FileName(pi.Src.guid);
      nm = Y.Unq(nm ?? (srcName != null ? Path.GetFileNameWithoutExtension(srcName) : "prefab_" + pi.Src.guid?.Substring(0, 8)));
      var n = Norm(nm); n = BracketP(n) ?? n;
      if (InstDb.Prefab.Contains(n) && !PStop.Contains(n)) { body.Add($"P {Y.Enc(n)} {T3(w.p)} {T4(w.r)} {T3(w.s)}"); st.P++; }
      else { var kd = Kind(nm) ?? "prefab_instance"; body.Add($"G {kd} {Y.Enc(nm)} {T3(w.p)} {T4(w.r)} src=prefab:{srcName ?? pi.Src.guid} scale={Y.F(w.s.x)},{Y.F(w.s.y)},{Y.F(w.s.z)}"); st.G++; st.GKinds[kd] = st.GKinds.GetValueOrDefault(kd) + 1; }
      st.Pi++; B(w.p);
    }
    // environment
    var rset = m.RS; var env = new List<string>();
    if (rset.Count > 0) { var rs = rset;
      string fm = ((int)Y.D(rs.GetValueOrDefault("m_FogMode"), 3)) switch { 1 => "linear", 2 => "exp", _ => "exp2" };
      var fc = Y.C(rs.GetValueOrDefault("m_FogColor")) ?? new double[] { 0.5, 0.5, 0.5, 1 };
      env.Add($"E fog {(Y.B(rs.GetValueOrDefault("m_Fog")) ? fm : "off")} {Y.F(fc[0])} {Y.F(fc[1])} {Y.F(fc[2])} {Y.F(Y.D(rs.GetValueOrDefault("m_FogDensity")))} {Y.F(Y.D(rs.GetValueOrDefault("m_LinearFogStart")))} {Y.F(Y.D(rs.GetValueOrDefault("m_LinearFogEnd")))}");
      var sky = Y.C(rs.GetValueOrDefault("m_AmbientSkyColor")) ?? new double[] { 0, 0, 0, 1 }; var eq = Y.C(rs.GetValueOrDefault("m_AmbientEquatorColor")); var gr = Y.C(rs.GetValueOrDefault("m_AmbientGroundColor"));
      int am = (int)Y.D(rs.GetValueOrDefault("m_AmbientMode")); string amn = am switch { 0 => "skybox", 1 => "trilight", 3 => "flat", _ => "custom" };
      env.Add($"E ambient {Y.F(sky[0])} {Y.F(sky[1])} {Y.F(sky[2])} mode={amn} intensity={Y.F(Y.D(rs.GetValueOrDefault("m_AmbientIntensity"), 1))}" + (eq != null && gr != null ? $" equator={Y.F(eq[0])},{Y.F(eq[1])},{Y.F(eq[2])} ground={Y.F(gr[0])},{Y.F(gr[1])},{Y.F(gr[2])}" : ""));
      var sk = Y.R(rs.GetValueOrDefault("m_SkyboxMaterial"));
      string skn = sk.Null ? "none" : sk.guid == "0000000000000000f000000000000000" ? "Default-Skybox" : Ref2.Mat(sk.guid) is MatInfo smi ? (InstDb.Mat.Contains(smi.Name) ? smi.Name : "?" + smi.Name) : MatVotes.TryGetValue(sk.guid, out var sv) ? sv.name : "?" + sk.guid;
      env.Add($"E sky {Y.Enc(skn)}");
      st.Env = $"fog {(Y.B(rs.GetValueOrDefault("m_Fog")) ? fm : "off")}, ambient {amn}, sky {skn}";
    }
    // write
    var sb = new StringBuilder();
    sb.Append($"# RRSK 1 {st.Scene}\n");
    sb.Append(st.HasB ? $"B {T3(st.Min)} {T3(st.Max)}\n" : "B 0 0 0 0 0 0\n");
    sb.Append($"# source {st.File} (reference only; derived placement data). Unknown names are prefixed '?'; '#U' = renderer with unknown mesh bounds.\n");
    foreach (var kv in meshT.OrderBy(x => x.Value)) sb.Append($"M {kv.Value} {Y.Enc(kv.Key)}\n");
    foreach (var kv in matT.OrderBy(x => x.Value)) sb.Append($"T {kv.Value} {Y.Enc(kv.Key)}\n");
    foreach (var l in body) sb.Append(l).Append('\n');
    foreach (var l in env) sb.Append(l).Append('\n');
    var outPath = Path.Combine(outDir, st.Scene + ".rrsk");
    File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
    st.OutBytes = new FileInfo(outPath).Length;
    st.TopUnm = unm.OrderByDescending(x => x.Value).Take(10).Select(x => (x.Key, x.Value)).ToList();
    File.WriteAllText(Path.Combine(outDir, st.Scene + ".md"), Summary(st, m), new UTF8Encoding(false));
    return st;
  }
  // --- effects (F) ------------------------------------------------------------------------------------------------
  // One F per effect root: a ParticleSystem object and the chain of ParticleSystem objects above it (nested systems
  // are one effect). Effects inside a P prefab come with it; effects that do not loop and play on awake are
  // triggered by gameplay (smash puffs, confetti, fireworks) and are listed, not placed.
  static void Effects(Ctx cx, SceneModel m, HashSet<long> pGo, List<string> body, Stats st, Action<V3> B, Func<V3, string> T3, Func<Q, string> T4) {
    if (m.Ps.Count == 0) return;
    var psByGo = new Dictionary<long, List<PsI>>();
    foreach (var p in m.Ps) { if (p.Go == 0) continue; if (!psByGo.TryGetValue(p.Go, out var l)) psByGo[p.Go] = l = new(); l.Add(p); }
    long ParentGo(long go) {
      if (!m.GoTr.TryGetValue(go, out var t) || !m.Tr.TryGetValue(t, out var tr)) return 0;
      var fa = cx.Local(tr).father; return fa != 0 && m.Tr.TryGetValue(fa, out var ft) ? ft.Go : 0;
    }
    var loops = new List<V3>();
    foreach (var a in m.Aud) if (a.En && a.Loop && !a.Clip.Null && cx.Active(a.Go)) loops.Add(cx.GoW(a.Go).p);
    var roots = new Dictionary<long, List<PsI>>(); var order = new List<long>();
    foreach (var p in m.Ps) {
      if (p.Go == 0) continue;
      long e = p.Go; int guard = 0;
      while (guard++ < 32) { var pa = ParentGo(e); if (pa == 0 || !psByGo.ContainsKey(pa)) break; e = pa; }
      if (!roots.TryGetValue(e, out var l)) { roots[e] = l = new(); order.Add(e); }
      l.Add(p);
    }
    foreach (var e in order) {
      var sys = roots[e]; var name = Norm(cx.Name(e)); var host = ParentGo(e); var hostName = host != 0 ? Norm(cx.Name(host)) : "";
      string label = name + (hostName.Length > 0 ? " < " + hostName : "");
      if (pGo.Contains(e)) { st.FxInPrefab.Add(label); continue; }
      if (!cx.Active(e)) { st.FxTriggered.Add(label + " (inactive)"); continue; }
      bool ambient = sys.Any(p => p.N("looping") != 0 && p.N("playOnAwake", 1) != 0 && cx.Active(p.Go));
      if (!ambient) { st.FxTriggered.Add(label); continue; }
      var mt = Fx.Match(name, hostName);
      if (mt == null) { st.FxUnmatched.Add(label + " [" + (Fx.KindOf(name + " " + hostName) ?? "no kind") + "]"); continue; }
      var w = cx.GoW(e);
      var main = sys.FirstOrDefault(p => p.Go == e) ?? sys.OrderByDescending(p => p.N("InitialModule.startSize.scalar", 1)).First();
      double sceneSize = main.N("InitialModule.startSize.scalar", 1);
      double lossy = Math.Max(Math.Abs(w.s.x), Math.Max(Math.Abs(w.s.y), Math.Abs(w.s.z)));
      var keys = new List<string>();
      // Scale: the scene object's world scale, applied as is when the install effect is the same one (exact, part).
      // A stand-in by kind instead takes the scene system's own numbers (ps.*: start size, lifetime, speed, rate,
      // max particles, emitter shape; sizes in world metres), because it is the same kind of Unity system with other
      // settings: a wall torch's [VFX_Flame] is Rec Room's campfire flame (circle emitter, 3 s, no speed) at 0.39 m,
      // 3/s, 5 max, radius 0.057 m against the campfire's 0.5 m, 7/s, 15, 0.161 m.
      double s = lossy;
      double ratio = Math.Clamp(sceneSize * lossy / Math.Max(1e-3, mt.InstSize), 0.05, 10);
      if (mt.Part.Length > 0) keys.Add("part=" + Y.Enc(mt.Part));
      keys.Add("kind=" + mt.Kind); keys.Add("match=" + mt.How); keys.Add("src=" + Y.Enc(name));
      if (hostName.Length > 0) keys.Add("host=" + Y.Enc(hostName));
      if (!mt.InstLoop) keys.Add("loop=1");
      if (mt.How == "kind") {
        keys.Add("fit=kind");
        keys.Add("ps.size=" + Y.F(sceneSize * lossy)); keys.Add("ps.life=" + Y.F(main.N("InitialModule.startLifetime.scalar", 5)));
        keys.Add("ps.speed=" + Y.F(main.N("InitialModule.startSpeed.scalar", 5) * lossy)); keys.Add("ps.rate=" + Y.F(main.N("EmissionModule.rateOverTime.scalar", 10)));
        keys.Add("ps.max=" + Y.F(main.N("InitialModule.maxNumParticles", 1000)));
        // Unity shape types: 0-3 sphere/hemisphere, 4 cone, 5/15/16 box, 10/11 circle, 18 rectangle
        int shape = (int)main.N("ShapeModule.type", -1);
        if (main.N("ShapeModule.enabled", 1) != 0 && shape >= 0) {
          var sc = Y.V(main.V.GetValueOrDefault("ShapeModule.m_Scale"), new V3(1, 1, 1)).Abs().Mul(w.s.Abs());
          if (shape == 5 || shape == 15 || shape == 16 || shape == 18) keys.Add($"ps.shape={shape} ps.box={Y.F(sc.x)},{Y.F(sc.y)},{Y.F(sc.z)}");
          else if (shape <= 4 || shape == 10 || shape == 11) keys.Add($"ps.shape={shape} ps.radius={Y.F(main.N("ShapeModule.radius.value", 1) * lossy)}");
        }
      }
      // wall mount: Rec Room's wall torches hold the flame ~0.29 m out from the wall along their +Z; a drinking
      // fountain's jet hangs on a wall the same way (without the wall the water arcs out of thin air: harness run 3)
      if (host != 0 && (hostName.ToLowerInvariant().Contains("torch") || hostName.ToLowerInvariant().Contains("fountain"))) {
        var hw = cx.GoW(host); var back = hw.r.Rot(new V3(0, 0, -1)); var d = w.p - hw.p; double off = -(d.x * back.x + d.y * back.y + d.z * back.z);
        keys.Add("mount=wall"); keys.Add($"back={Y.F(back.x)},{Y.F(back.y)},{Y.F(back.z)}"); keys.Add("off=" + Y.F(Math.Max(0, off)));
      }
      // sound: the install effect prefab's own loop (the campfire's crackle; GUESS for lava), quieter and shorter for a
      // smaller stand-in (volume x ratio^2, range x ratio); left out where Rec Room put an ambient loop within 4 m
      bool ownLoopNear = loops.Any(p => { var q = p - w.p; return q.x * q.x + q.y * q.y + q.z * q.z < 16; });
      if (mt.Snd != null && !ownLoopNear) {
        double r = mt.How == "kind" && mt.Kind != "lava" ? Math.Clamp(ratio, 0.2, 1) : 1;
        keys.Add("snd=" + Y.Enc(mt.Snd)); keys.Add("snd.vol=" + Y.F(mt.SndVol * r * r)); keys.Add("snd.min=" + Y.F(mt.SndMin)); keys.Add("snd.max=" + Y.F(Math.Max(mt.SndMin + 1, mt.SndMax * r)));
        if (mt.SndGuess || mt.How == "kind") keys.Add("snd.guess=1");
      }
      body.Add($"F {Y.Enc(mt.Prefab)} {T3(w.p)} {T4(w.r)} {Y.F(s)} {string.Join(" ", keys)}"); st.F++; B(w.p);
      // A wall torch's flame itself is not a particle system: it is part of the WallTorch mesh (the Golden Trophy
      // static batches name it legacy_LaternFlame_Mat) and [VFX_Flame] adds the embers. The install's own torch-flame
      // look is the candle's flame card, (Sandbox_Candle) Candle_Tall/FirePlane_fbx with LaternFlame_Mat (Shader Forge
      // PaperFlame, the same shader as the campfire's flame cards): one card per torch, upright in the torch's frame
      // (keeprot=1 keeps the card's own rotation inside the prefab), fitted to 0.3 m high (GUESS from WallTorch_LOD0's
      // 0.73 m height with the head at the top).
      if (host != 0 && hostName.ToLowerInvariant().Contains("torch") && mt.Kind == "fire" && InstDb.PrefabMeshes.TryGetValue("(Sandbox_Candle)", out var candle) && candle.Contains("FlamePlane")) {
        var hw = cx.GoW(host); var back = hw.r.Rot(new V3(0, 0, -1)); var d = w.p - hw.p; double off = -(d.x * back.x + d.y * back.y + d.z * back.z);
        body.Add($"F (Sandbox_Candle) {T3(w.p)} {T4(hw.r)} 1 part=Candle_Tall/FirePlane_fbx kind=fire match=kind src={Y.Enc(name)} host={Y.Enc(hostName)} card=1 keeprot=1 fit.height=0.3 mount=wall back={Y.F(back.x)},{Y.F(back.y)},{Y.F(back.z)} off={Y.F(Math.Max(0, off))}");
        st.F++; st.FxPlaced.Add($"{label} flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind)");
      }
      st.FxPlaced.Add($"{label} -> {mt.Prefab}{(mt.Part.Length > 0 ? " / " + mt.Part : "")} ({mt.How}{(mt.How == "kind" ? ", size x" + Y.F(ratio) : "")}{(mt.Snd != null && !ownLoopNear ? ", sound " + mt.Snd : "")})");
    }
  }

  // <scene>-AudioClips.asset next to the scene: the clips the room lists (guid -> REF2 audio/.meta -> name)
  static void ClipList(string scenePath, Stats st) {
    var dir = Path.GetDirectoryName(scenePath); var stem = Path.GetFileNameWithoutExtension(scenePath);
    var f = Path.Combine(dir, stem + "-AudioClips.asset");
    if (!File.Exists(f)) return;
    foreach (var l in File.ReadLines(f)) {
      var t = l.Trim(); if (!t.StartsWith("- {fileID")) continue;
      var g = Y.R(t).guid; var fn = Ref2.FileName(g); var nm = fn != null ? Path.GetFileNameWithoutExtension(fn) : "?" + g;
      var (clip, how) = Fx.Clip(nm, false);
      st.ClipList.Add(clip == null ? nm + " (not in the install)" : clip == nm ? nm : nm + " -> " + clip + " (" + how + ")");
    }
  }

  static string Group(IEnumerable<string> items, int max = 30) {
    var g = items.GroupBy(x => x).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).Select(x => x.Count() > 1 ? $"{x.Key} x{x.Count()}" : x.Key).ToList();
    return g.Count == 0 ? "none" : string.Join("; ", g.Take(max)) + (g.Count > max ? $"; ... ({g.Count - max} more)" : "");
  }

  static string LabelOf(Ctx cx, long go) {
    if (!cx.GoMb.TryGetValue(go, out var l)) return null;
    foreach (var b in l) if (ScriptLabel.TryGetValue(b.Script, out var lab)) return lab;
    return null;
  }
  static string BoundsStr(Stats s) => s.HasB ? $"({Y.F(s.Min.x)}, {Y.F(s.Min.y)}, {Y.F(s.Min.z)}) .. ({Y.F(s.Max.x)}, {Y.F(s.Max.y)}, {Y.F(s.Max.z)})" : "-";
  static string Summary(Stats s, SceneModel m) {
    var sb = new StringBuilder();
    sb.Append($"# {s.Scene}\n\nSource: `.unity files/.../{s.File}` ({s.Bytes / 1024} KB, {m.Go.Count} GameObjects). Output: `{s.Scene}.rrsk` ({s.OutBytes / 1024} KB).\n\n");
    sb.Append($"**Verdict: {s.Verdict}.**\n\n");
    sb.Append($"- Renderers (active, enabled, LOD0): {s.Rend} ({s.Builtin} Unity built-in primitives; {s.Lod} LOD1+ and {s.Inactive} disabled/inactive skipped)\n");
    sb.Append($"- Matched mesh: {s.MeshPct:0.#}% of non-primitive renderers ({s.Matched} as R + {s.PCov} inside P prefabs); matched material slots: {s.MatPct:0.#}%\n");
    sb.Append($"- Records: R {s.R}, P {s.P}, X {s.X}, #U {s.U}, C {s.Col}, L {s.Light}, A {s.Aud}, F {s.F}, G {s.G} (prefab instances: {s.Pi})\n");
    sb.Append($"- Bounds: {BoundsStr(s)}\n");
    if (s.Env != "") sb.Append($"- Environment: {s.Env}\n");
    if (s.GKinds.Count > 0) sb.Append($"- G kinds: {string.Join(", ", s.GKinds.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}"))}\n");
    if (s.TopUnm.Count > 0) sb.Append($"- Top unmatched meshes: {string.Join(", ", s.TopUnm.Select(x => $"{x.Item1} ({x.Item2})"))}\n");
    // iteration 5: effects and sounds
    int ps = m.Ps.Count;
    if (ps > 0 || s.Sounds.Count > 0 || s.ClipList.Count > 0) sb.Append("\n## Effects and sounds (iteration 5)\n\n");
    if (ps > 0) {
      sb.Append($"- Particle systems in the scene: {ps}. F records (ambient effects placed from the install): {s.F}.\n");
      sb.Append($"- Placed: {Group(s.FxPlaced)}\n");
      if (s.FxInPrefab.Count > 0) sb.Append($"- Come with their P prefab: {Group(s.FxInPrefab)}\n");
      if (s.FxTriggered.Count > 0) sb.Append($"- Triggered by gameplay or inactive (not looping on awake; not placed): {Group(s.FxTriggered)}\n");
      if (s.FxUnmatched.Count > 0) sb.Append($"- No install effect of that kind (skipped): {Group(s.FxUnmatched)}\n");
    }
    var loopsA = s.Sounds.Where(x => x.loop).ToList(); var shots = s.Sounds.Where(x => !x.loop).ToList();
    if (loopsA.Count > 0) sb.Append($"- Ambient loops (A, loop 1): {Group(loopsA.Select(x => (x.clip == null ? (x.name.StartsWith("?") ? x.name : "?" + x.name) + " (not in the install)" : x.clip == x.name ? x.clip : x.name + " -> " + x.clip + " (" + x.how + ")") + $" vol {Y.F(x.vol)} {Y.F(x.min)}-{Y.F(x.max)} m"))}\n");
    if (shots.Count > 0) sb.Append($"- One-shot sources (A, loop 0; the kit turns them into \"audio\" markers): {Group(shots.Select(x => (x.clip ?? "?" + x.name) + " on " + x.obj))}\n");
    if (s.ClipList.Count > 0) sb.Append($"- The room's clip list ({s.Scene}-AudioClips.asset, {s.ClipList.Count}): {Group(s.ClipList, 60)}\n");
    else if (ps > 0 || s.Sounds.Count > 0) sb.Append("- The room's clip list (AudioClips.asset): none or empty.\n");
    return sb.ToString();
  }
  static string StatLine(Stats s) => string.Join("\t", new object[] { s.Scene, s.Rend, s.MeshPct.ToString("0", Y.Inv), s.MatPct.ToString("0", Y.Inv), s.R, s.P, s.X, s.U, s.Col, s.Light, s.Aud, s.G, BoundsStr(s), s.OutBytes / 1024, string.Join(", ", s.TopUnm.Select(x => $"{x.Item1} ({x.Item2})")), s.Verdict, s.Builtin, s.Bytes / 1024 });
  static void WriteIndex(string statDir, string outDir) {
    var rows = Directory.GetFiles(statDir, "*.tsv").Select(f => File.ReadAllText(f).Split('\t')).OrderBy(p => p[0]).ToList();
    var sb = new StringBuilder();
    sb.Append("# Reference scenes -> RRSK 1 placement files (index)\n\nGenerated by DN/tools/rrscene (`rrscene write`). One row per Unity YAML scene in REF2 `.unity files/`.\n");
    sb.Append("Renderers = active, enabled, LOD0 MeshRenderer/SkinnedMeshRenderer with a mesh. Mesh % = renderers whose mesh is an install Mesh (as R) or covered by a P prefab, over non-primitive renderers. ");
    sb.Append("Mat % = material slots resolved to an install Material name. X = stand-ins (incl. Unity built-in cubes etc.), #U = unmatched with unknown size (comment lines). Paintball gameplay layers: `Paintball_*_gameplay.rrsk`.\n\n");
    sb.Append("| scene | rend | prim | mesh % | mat % | R | P | X | #U | C | L | A | G | bounds (min .. max) | rrsk KB | top unmatched meshes | verdict |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
    foreach (var p in rows) sb.Append($"| {p[0]} | {p[1]} | {p[16]} | {p[2]} | {p[3]} | {p[4]} | {p[5]} | {p[6]} | {p[7]} | {p[8]} | {p[9]} | {p[10]} | {p[11]} | {p[12]} | {p[13]} | {p[14].Replace("|", "/")} | {p[15]} |\n");
    var pbs = Directory.GetFiles(outDir, "Paintball_*_gameplay.rrsk").OrderBy(x => x).ToList();
    if (pbs.Count > 0) {
      sb.Append("\n## Paintball gameplay layers (REF1 room export paintballtroll1, saved on the official map scenes; no geometry)\n\n");
      sb.Append("Made by `rrscene paintball`. G kinds flag_base/flag/weapon_pickup/standee/game_config/game_rule/role/role_map/scoreboard/leaderboard/door = official-looking layer; `G extra` = the troll's own additions (skip them).\n\n");
      sb.Append("| file | G (official set) | flag_base | weapon_pickup | standee | game_config | P | extra | KB |\n|---|---|---|---|---|---|---|---|---|\n");
      foreach (var f in pbs) {
        var ls = File.ReadAllLines(f); int Cnt(string pre) => ls.Count(l => l.StartsWith(pre));
        sb.Append($"| {Path.GetFileName(f)} | {Cnt("G ") - Cnt("G extra")} | {Cnt("G flag_base")} | {Cnt("G weapon_pickup")} | {Cnt("G standee")} | {Cnt("G game_config")} | {Cnt("P ")} | {Cnt("G extra")} | {new FileInfo(f).Length / 1024} |\n");
      }
    }
    File.WriteAllText(Path.Combine(outDir, "INDEX.md"), sb.ToString(), new UTF8Encoding(false));
  }
}
