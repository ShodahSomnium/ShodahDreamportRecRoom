// apkscene: scene model -> RRSK 1 text (+ H terrain grids) and a per-scene .md report.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

sealed class InstIdx {
  public readonly Dictionary<string, List<(string b, long pid, int vc, string subs, V3 c, V3 e)>> Mesh = new(StringComparer.Ordinal);
  public readonly Dictionary<string, string> MeshCI = new(StringComparer.OrdinalIgnoreCase);
  public readonly HashSet<string> Mat = new(StringComparer.Ordinal); public readonly Dictionary<string, string> MatCI = new(StringComparer.OrdinalIgnoreCase);
  public readonly HashSet<string> Roots = new(StringComparer.Ordinal); public readonly HashSet<string> Clips = new(StringComparer.Ordinal); public readonly Dictionary<string, string> ClipCI = new(StringComparer.OrdinalIgnoreCase);
  public static InstIdx Load(string work) {
    var x = new InstIdx();
    foreach (var l in File.ReadLines(Path.Combine(work, "inst_meshes.tsv"))) {
      var p = l.Split('\t'); if (p.Length < 6) continue; var bb = p[5].Split(' ').Select(double.Parse).ToArray();
      if (!x.Mesh.TryGetValue(p[2], out var li)) x.Mesh[p[2]] = li = new(); li.Add((p[0], long.Parse(p[1]), int.Parse(p[3]), p[4], new V3(bb[0], bb[1], bb[2]), new V3(bb[3], bb[4], bb[5])));
      x.MeshCI.TryAdd(p[2], p[2]);
    }
    foreach (var l in File.ReadLines(Path.Combine(work, "inst_materials.tsv"))) { var p = l.Split('\t'); if (p.Length < 3) continue; x.Mat.Add(p[2]); x.MatCI.TryAdd(p[2], p[2]); }
    foreach (var l in File.ReadLines(Path.Combine(Paths.Idx, "objects.tsv"))) {
      var p = l.Split('\t'); if (p.Length < 4) continue;
      if (p[2] == "GameObject*") x.Roots.Add(p[3]); else if (p[2] == "AudioClip") { x.Clips.Add(p[3]); x.ClipCI.TryAdd(p[3], p[3]); }
    }
    return x;
  }
}

sealed class EmitStats {
  public int HMesh, XGround, XVox, CMeshH, CMeshVox, CVoxBoxes, Rend, RendLod, RendInactive, RendInPrefab, RendNoMesh, Matched, MatchedCI, X, U, Slots, SlotsKnown, P, PInactive, G, C, CMesh, CMeshBox, CBox, CSph, CCap, CInPrefab, L, A, AKnown, H, Trig;
  public Dictionary<string, int> GKinds = new(); public Dictionary<string, int> Unmatched = new(); public Dictionary<string, int> UnmatchedMat = new(); public Dictionary<string, int> PNames = new();
  public List<string> Signs = new(); public List<string> Notes = new(); public int Gos; public bool Terrain;
  public double MatchPct => Rend - RendNoMesh <= 0 ? 0 : 100.0 * Matched / (Rend - RendNoMesh);
}

static class Emit {
  static readonly Regex Suffix = new(@"(\s*\(\d+\))+$|\s+Variant$", RegexOptions.Compiled);
  public static string Norm(string n) { n = (n ?? "").Trim(); for (int i = 0; i < 4; i++) { var k = Suffix.Replace(n, "").Trim(); if (k == n) break; n = k; } if (n.EndsWith("(Clone)")) n = n.Substring(0, n.Length - 7).Trim(); return n; }
  static string T3(V3 v) => v.ToString(); static string T4(Q q) => q.ToString();

  /// Gameplay kind of a GameObject from its prefab name, scripts and path; null = not gameplay.
  public static string Kind(string norm, List<string> scr, string path) {
    bool S(string s) => scr.Any(x => x == s || x.EndsWith("." + s));
    string n = norm.Trim('(', ')', '[', ']');
    if (S("FlagGoal") || n == "FlagGoal") return "flag_base";
    if (n == "FlagTool" || S("FlagTool")) return "flag";
    if (S("GamePlayerSpawnPoint") || S("PlayerSpawnPoint")) return "spawn";
    if (n.StartsWith("Standee") || n.Contains("Cutout")) return "standee";
    if (n.Contains("ScoreboardProjector") || S("GameScoreboard")) return "scoreboard";
    if (n.Contains("LeaderboardProjector")) return "leaderboard";
    if (n.Contains("_GameConfiguration")) return "game_config";
    if (n.Contains("GameRuleWrapperCircuit")) return "game_rule";
    if (S("OverlapVolume")) return Regex.IsMatch(n, "Staging|SafeZone|Base", RegexOptions.IgnoreCase) ? "team_zone" : Regex.IsMatch(n, "Spawn", RegexOptions.IgnoreCase) ? "spawn_area" : "bounds";
    if (Regex.IsMatch(n, "KillFloor|KillVolume|KillZone|OutOfBounds|DeathVolume", RegexOptions.IgnoreCase) || S("KillVolume") || S("PlayerKillVolume")) return "kill_zone";
    if (S("GameTeleportRestriction") || Regex.IsMatch(n, "^TeleportBlocker", RegexOptions.IgnoreCase)) return "teleport_block";
    if (S("SpectatorStreamPoint")) return "spectator";
    if (n.Contains("Storefront")) return "store";
    if (S("HoldToInteractHandler") && path.Contains("ModInteractions")) return "mod_portal";
    bool tool = S("Tool") || S("SpawnableTool");
    if (tool && (scr.Any(x => Regex.IsMatch(x, @"Weapon|Pistol|Shotgun|Grenade|Rifle|Blaster|Shield|PaintThrower|Launcher|Bow")) || Regex.IsMatch(n, "Paintball|Grenade|Gun|Rifle|Shield|Crossbow|Blaster"))) return "weapon_pickup";
    return null;
  }
  static readonly HashSet<string> PlayKinds = new() { "bounds", "team_zone", "spawn_area", "spawn", "flag_base", "weapon_pickup" };
  static readonly HashSet<string> GOnly = new() { "flag_base", "flag", "weapon_pickup", "standee", "scoreboard", "leaderboard", "game_config", "game_rule", "store" };
  static string Team(string path) {
    var p = path.ToLowerInvariant();
    if (Regex.IsMatch(p, @"blue|team1\b|team1spawn")) return "blue"; if (Regex.IsMatch(p, @"red|team2\b|team2spawn")) return "red";
    if (p.Contains("pregame") || p.Contains("holdingarea") || p.Contains("lobby")) return "pregame"; return null;
  }

  public static EmitStats Write(Model m, InstIdx ix, string scene, string outPath, string source) {
    var st = new EmitStats { Gos = m.Gos.Count };
    var body = new List<string>(); var meshT = new Dictionary<string, int>(); var matT = new Dictionary<string, int>();
    int MI(string n) { if (!meshT.TryGetValue(n, out int i)) meshT[n] = i = meshT.Count; return i; }
    int TI(string n) { if (!matT.TryGetValue(n, out int i)) matT[n] = i = matT.Count; return i; }
    V3 bmin = new(1e9, 1e9, 1e9), bmax = new(-1e9, -1e9, -1e9);
    void B(V3 p) { bmin = V3.Min(bmin, p); bmax = V3.Max(bmax, p); }
    void BBox(V3 c, V3 e, Q r) { for (int i = 0; i < 8; i++) B(c + r.Rot(new V3((i & 1) != 0 ? e.x : -e.x, (i & 2) != 0 ? e.y : -e.y, (i & 4) != 0 ? e.z : -e.z))); }
    var scrOf = m.Mbs.GroupBy(x => x.Go).ToDictionary(g => g.Key, g => g.Select(x => x.Script).ToList());
    List<string> Scr(long go) => scrOf.GetValueOrDefault(go) ?? new List<string>();
    var covered = new HashSet<long>(); var gDone = new HashSet<long>();
    (V3 mn, V3 mx) play = (new V3(1e9, 1e9, 1e9), new V3(-1e9, -1e9, -1e9));
    var colByGo = m.Col.GroupBy(c => c.Go).ToDictionary(g => g.Key, g => g.ToList());
    var rendByGo = m.Rend.GroupBy(r => r.Go).ToDictionary(g => g.Key, g => g.ToList());
    IEnumerable<long> Sub(long go) { var g = m.Gos.GetValueOrDefault(go); if (g == null) yield break; yield return go; var t = m.Trs.GetValueOrDefault(g.Tr); if (t == null) yield break; foreach (var k in t.Kids) { var kt = m.Trs.GetValueOrDefault(k); if (kt == null) continue; foreach (var x in Sub(kt.Go)) yield return x; } }

    string Pref(string norm) {
      if (norm.Length < 3) return null;
      if (norm[0] == '(' && norm[^1] == ')' && ix.Roots.Contains(norm)) return norm;
      if (norm[0] == '[' && norm[^1] == ']') { if (ix.Roots.Contains(norm)) return norm; var pn = "(" + norm.Substring(1, norm.Length - 2) + ")"; if (ix.Roots.Contains(pn)) return pn; }
      return null;
    }
    // G for a gameplay object; shape keys from its own colliders
    void G(long go, string kind, string prefab) {
      if (!gDone.Add(go)) return;
      var w = m.GoW(go); var path = m.PathOf(go); var keys = new List<string>();
      var team = Team(path); if (team != null) keys.Add("team=" + team);
      if (kind == "weapon_pickup" || kind == "flag" || kind == "flag_base") { var par = m.Parent(go); var pn = par != 0 ? m.Name(par) : ""; if (pn != "" && !pn.StartsWith("(")) keys.Add("set=" + Y.Enc(pn)); }
      if (prefab != null) keys.Add("prefab=" + Y.Enc(prefab));
      V3 pos = w.p;
      var cols = colByGo.GetValueOrDefault(go);
      if (cols != null && cols.Count > 0 && !GOnly.Contains(kind)) {
        var c = cols[0]; var sa = w.s.Abs(); var cc = w.p + w.r.Rot(w.s.Mul(c.C));
        switch (c.Cls) {
          case 65: { var sz = c.Size.Abs().Mul(sa); keys.Add($"shape=box size={Y.F(sz.x)},{Y.F(sz.y)},{Y.F(sz.z)}"); pos = cc; BBox(cc, sz * 0.5, w.r); break; }
          case 135: keys.Add($"shape=sphere r={Y.F(c.Rad * sa.Max())}"); pos = cc; break;
          case 136: keys.Add($"shape=capsule r={Y.F(c.Rad * sa.Max())} h={Y.F(c.H * sa.Max())} dir={c.Dir}"); pos = cc; break;
          case 64: if (c.Mesh != null) { var mc = w.p + w.r.Rot(w.s.Mul(c.Mesh.C)); var sz = c.Mesh.E.Mul(sa) * 2; keys.Add($"shape=box size={Y.F(sz.x)},{Y.F(sz.y)},{Y.F(sz.z)} from=mesh"); pos = mc; } break;
        }
        if (cols.Count > 1) keys.Add("shapes=" + cols.Count);
      }
      if (!m.Active(go)) keys.Add("active=0");
      keys.Add("path=" + Y.Enc(path.Length > 120 ? path.Substring(path.Length - 120) : path));
      if (PlayKinds.Contains(kind) && m.Active(go)) { play.mn = V3.Min(play.mn, pos); play.mx = V3.Max(play.mx, pos); }
      body.Add($"G {kind} {Y.Enc(m.Name(go))} {T3(pos)} {T4(w.r)} {string.Join(" ", keys)}"); st.G++; st.GKinds[kind] = st.GKinds.GetValueOrDefault(kind) + 1; B(pos);
    }

    // 1. hierarchy walk: prefabs -> P (or G only for gameplay prefabs); gameplay scripts -> G
    void Walk(long trId, int depth) {
      var t = m.Trs.GetValueOrDefault(trId); if (t == null || depth > 200) return;
      var go = t.Go; var g = m.Gos.GetValueOrDefault(go); if (g == null) return;
      var norm = Norm(g.Name); var pref = Pref(norm); var scr = Scr(go); var path = m.PathOf(go);
      var kind = Kind(norm, scr, path);
      if (pref != null) {
        var sub = Sub(go).ToList(); bool hasRend = sub.Any(x => rendByGo.ContainsKey(x));
        if (kind != null && GOnly.Contains(kind)) { G(go, kind, pref); foreach (var x in sub) covered.Add(x); return; }
        if (hasRend) {
          var w = m.GoW(go);
          if (m.Active(go)) { body.Add($"P {Y.Enc(pref)} {T3(w.p)} {T4(w.r)} {T3(w.s)}"); st.P++; st.PNames[pref] = st.PNames.GetValueOrDefault(pref) + 1; B(w.p); } else st.PInactive++;
          if (kind != null) G(go, kind, pref);
          foreach (var x in sub) covered.Add(x); return;
        }
      }
      if (kind != null) G(go, kind, null);
      foreach (var k in t.Kids) Walk(k, depth + 1);
    }
    foreach (var t in m.Trs.Values.Where(t => t.Father == 0).OrderBy(t => m.Name(t.Go), StringComparer.Ordinal)) Walk(t.Id, 0);

    // measured shapes for meshes that are not in the install (Shape.cs): ground-like -> H grid, else voxel boxes
    var hDone = new HashSet<(long, string, long)>();
    int hN = 0;
    bool GroundLike(MeshInfo mi, TRS w, out V3[] wv, out int[] tris) {
      wv = null; tris = null; var (lv, lt) = MeshGeo.Decode(m.L, mi); if (lv == null || lt.Length < 3) return false;
      wv = lv.Select(p => w.p + w.r.Rot(w.s.Mul(p))).ToArray(); tris = lt;
      V3 mn = wv.Aggregate(new V3(1e9, 1e9, 1e9), V3.Min), mx = wv.Aggregate(new V3(-1e9, -1e9, -1e9), V3.Max);
      return mx.x - mn.x >= 24 && mx.z - mn.z >= 24 && Shape.UpShare(wv, lt) >= 0.6;
    }
    var grounds = new Dictionary<string, (long go, MeshInfo mi, V3[] wv, int[] tris, bool col, bool vis, string mat, double[] rgba)>();
    void EmitGround(long go, MeshInfo mi, V3[] wv, int[] tris, bool col, bool vis, string mat, double[] rgba = null) {
      var o = wv.Aggregate(new V3(1e9, 1e9, 1e9), V3.Min); var key = $"{mi.File}|{mi.Pid}|{Math.Round(o.x, 1)},{Math.Round(o.y, 1)},{Math.Round(o.z, 1)}";
      if (grounds.TryGetValue(key, out var g)) { g.col |= col; g.vis |= vis; g.mat ??= mat; g.rgba ??= rgba; grounds[key] = g; } else grounds[key] = (go, mi, wv, tris, col, vis, mat, rgba);
    }
    List<(V3 c, V3 s)> LocalBoxes(MeshInfo mi, TRS w, int max, double div, double smin, double smax, out double vox) {
      vox = 0; var (lv, lt) = MeshGeo.Decode(m.L, mi); if (lv == null || lt.Length < 3) return null;
      var sv = lv.Select(p => p.Mul(w.s)).ToArray(); var r = Shape.Boxes(sv, lt, max, div, smin, smax); vox = r.vox; return r.boxes;
    }
    var meshUse = m.Rend.Where(x => x.Mesh != null && !x.Lod1 && x.Mesh.Name != null).GroupBy(x => x.Mesh.Name).ToDictionary(g => g.Key, g => g.Count());
    // 2. renderers
    string MatchMesh(MeshInfo mi, out bool ci) {
      ci = false; if (mi == null || string.IsNullOrEmpty(mi.Name)) return null;
      if (ix.Mesh.ContainsKey(mi.Name)) return mi.Name;
      if (ix.MeshCI.TryGetValue(mi.Name, out var n)) { ci = true; return n; }
      return null;
    }
    foreach (var r in m.Rend) {
      if (r.Lod1) { st.RendLod++; continue; }
      if (covered.Contains(r.Go)) { st.RendInPrefab++; continue; }
      if (!r.En || !m.Active(r.Go)) { st.RendInactive++; continue; }
      st.Rend++;
      if (r.Mesh == null || string.IsNullOrEmpty(r.Mesh.Name)) { st.RendNoMesh++; continue; } // TextMeshPro etc. build their mesh at runtime
      var w = m.GoW(r.Go);
      var mats = new List<string>(); double[] col0 = null;
      int ns = Math.Max(1, Math.Min(r.Mats.Count, Math.Max(1, r.Mesh.Subs.Count)));
      for (int k = 0; k < ns; k++) {
        var mt = k < r.Mats.Count ? r.Mats[k] : (null, null); col0 ??= mt.col; st.Slots++;
        string nm = mt.name == null ? "?none" : ix.Mat.Contains(mt.name) ? mt.name : ix.MatCI.TryGetValue(mt.name, out var cn) ? cn : "?" + mt.name;
        if (!nm.StartsWith("?")) st.SlotsKnown++; else st.UnmatchedMat[nm] = st.UnmatchedMat.GetValueOrDefault(nm) + 1;
        mats.Add(nm);
      }
      var match = MatchMesh(r.Mesh, out bool ci);
      if (match != null) {
        st.Matched++; if (ci) st.MatchedCI++;
        body.Add($"R {MI(match)} {string.Join(",", mats.Select(TI))} {T3(w.p)} {T4(w.r)} {T3(w.s)}");
        BBox(w.p + w.r.Rot(w.s.Mul(r.Mesh.C)), r.Mesh.E.Mul(w.s.Abs()), w.r); continue;
      }
      st.Unmatched[r.Mesh.Name] = st.Unmatched.GetValueOrDefault(r.Mesh.Name) + 1;
      if (r.Mesh.E.Mul(w.s.Abs()).Max() > 6) {
        if (GroundLike(r.Mesh, w, out var gwv, out var gtr)) { bool hasCol = colByGo.GetValueOrDefault(r.Go)?.Any(c => c.En && !c.Trig && c.Cls == 64 && c.Mesh != null && c.Mesh.Pid == r.Mesh.Pid && c.Mesh.File == r.Mesh.File) ?? false; EmitGround(r.Go, r.Mesh, gwv, gtr, hasCol, true, mats.FirstOrDefault(x => !x.StartsWith("?")) ?? mats.FirstOrDefault(), col0); st.XGround++; continue; }
        double vx = 0; var bx = meshUse.GetValueOrDefault(r.Mesh.Name) <= 2 ? LocalBoxes(r.Mesh, w, 60, 12, 0.5, 3, out vx) : null;
        if (bx != null) { var cc0 = col0 ?? new double[] { 0.7, 0.7, 0.7, 1 }; body.Add($"# X from a {Y.F(vx)} m voxel shell of ?{Y.Enc(r.Mesh.Name)} ({bx.Count} boxes)" + (Environment.GetEnvironmentVariable("APKSCENE_DEBUG") == "1" ? " @" + Y.Enc(mats.FirstOrDefault() ?? "?") : "")); foreach (var b in bx) { var bc = w.p + w.r.Rot(b.c); body.Add($"X box {T3(bc)} {T3(b.s * 0.5)} {T4(w.r)} {Y.F(cc0[0])} {Y.F(cc0[1])} {Y.F(cc0[2])} {Y.F(cc0.Length > 3 ? Math.Clamp(cc0[3], 0, 1) : 1)}"); BBox(bc, b.s * 0.5, w.r); } st.X += bx.Count; st.XVox++; continue; }
      }
      if (r.Mesh.E.Max() <= 0) { body.Add($"#U {Y.Enc(r.Mesh.Name)} {T3(w.p)} {T4(w.r)} {T3(w.s)}"); st.U++; B(w.p); continue; }
      var wc = w.p + w.r.Rot(w.s.Mul(r.Mesh.C)); var we = r.Mesh.E.Mul(w.s.Abs());
      var c = col0 ?? new double[] { 0.7, 0.7, 0.7, 1 };
      body.Add($"X box {T3(wc)} {T3(we)} {T4(w.r)} {Y.F(c[0])} {Y.F(c[1])} {Y.F(c[2])} {Y.F(c.Length > 3 ? Math.Clamp(c[3], 0, 1) : 1)}"); st.X++; BBox(wc, we, w.r);
      if (Environment.GetEnvironmentVariable("APKSCENE_DEBUG") == "1") body[^1] += " #" + Y.Enc(r.Mesh.Name) + "@" + Y.Enc(mats.FirstOrDefault() ?? "?");
    }

    // 3. colliders (walkable shape, exactly as the level has them); triggers of non-gameplay objects -> G trigger_zone
    foreach (var c in m.Col) {
      if (covered.Contains(c.Go)) { st.CInPrefab++; continue; }
      if (!c.En || !m.Active(c.Go)) continue;
      if (gDone.Contains(c.Go)) continue; // a gameplay volume's shape is on its G record
      var w = m.GoW(c.Go); var cc = w.p + w.r.Rot(w.s.Mul(c.C)); var sa = w.s.Abs();
      string geo = null, gk = null;
      switch (c.Cls) {
        case 65: { var sz = c.Size.Abs().Mul(sa); geo = $"box {T3(cc)} {T4(w.r)} {T3(sz)}"; gk = $"shape=box size={Y.F(sz.x)},{Y.F(sz.y)},{Y.F(sz.z)}"; BBox(cc, sz * 0.5, w.r); st.CBox++; break; }
        case 135: { var rr = c.Rad * sa.Max(); geo = $"sphere {T3(cc)} {Y.F(rr)}"; gk = $"shape=sphere r={Y.F(rr)}"; B(cc); st.CSph++; break; }
        case 136: {
          Q axis = c.Dir == 0 ? new Q(0, 0, -0.7071068, 0.7071068) : c.Dir == 2 ? new Q(0.7071068, 0, 0, 0.7071068) : Q.I;
          double hs = c.Dir == 0 ? sa.x : c.Dir == 2 ? sa.z : sa.y; double rs = c.Dir == 0 ? Math.Max(sa.y, sa.z) : c.Dir == 2 ? Math.Max(sa.x, sa.y) : Math.Max(sa.x, sa.z);
          var q = (w.r * axis).Norm(); geo = $"capsule {T3(cc)} {T4(q)} {Y.F(c.Rad * rs)} {Y.F(c.H * hs)}"; gk = $"shape=capsule r={Y.F(c.Rad * rs)} h={Y.F(c.H * hs)}"; B(cc); st.CCap++; break; }
        default: {
          var mi = c.Mesh; var mm = MatchMesh(mi, out _);
          if (mm != null) { geo = $"mesh {MI(mm)} {T3(w.p)} {T4(w.r)} {T3(w.s)}"; gk = $"shape=mesh mesh={Y.Enc(mm)}"; st.CMesh++; if (mi != null) BBox(w.p + w.r.Rot(w.s.Mul(mi.C)), mi.E.Mul(sa), w.r); }
          else if (mi != null && !c.Trig && GroundLike(mi, w, out var cwv, out var ctr)) { var rm = rendByGo.GetValueOrDefault(c.Go)?.FirstOrDefault(x => x.Mesh != null && x.Mesh.Pid == mi.Pid && x.Mesh.File == mi.File && x.En && !x.Lod1); EmitGround(c.Go, mi, cwv, ctr, true, false, null); st.CMeshH++; continue; }
          else if (mi != null && !c.Trig && LocalBoxes(mi, w, 120, 16, 0.5, 2, out double cvx) is List<(V3 c, V3 s)> cbx) { body.Add($"# C from a {Y.F(cvx)} m voxel shell of ?{Y.Enc(mi.Name)} (mesh not in the install; {cbx.Count} boxes)"); foreach (var b in cbx) { var bc = w.p + w.r.Rot(b.c); body.Add($"C box {T3(bc)} {T4(w.r)} {T3(b.s)}"); BBox(bc, b.s * 0.5, w.r); } st.C += cbx.Count; st.CMeshVox++; st.CVoxBoxes += cbx.Count; continue; }
          else if (mi != null && mi.E.Max() > 0) { var wc = w.p + w.r.Rot(w.s.Mul(mi.C)); var sz = mi.E.Mul(sa) * 2; geo = $"box {T3(wc)} {T4(w.r)} {T3(sz)}"; gk = $"shape=box size={Y.F(sz.x)},{Y.F(sz.y)},{Y.F(sz.z)} from=mesh"; cc = wc; st.CMeshBox++; BBox(wc, sz * 0.5, w.r); if (!c.Trig) body.Add($"# C mesh ?{Y.Enc(mi.Name)} -> box (mesh not in the install)"); }
          break; }
      }
      if (geo == null) continue;
      if (c.Trig) { var kd = Kind(Norm(m.Name(c.Go)), Scr(c.Go), m.PathOf(c.Go)) ?? "trigger_zone"; body.Add($"G {kd} {Y.Enc(m.Name(c.Go))} {T3(cc)} {T4(w.r)} {gk}"); st.G++; st.Trig++; st.GKinds[kd] = st.GKinds.GetValueOrDefault(kd) + 1; }
      else { body.Add("C " + geo); st.C++; }
    }

    // 4. height grids: terrains and ground-like meshes that are not in the install. Inside the play area (+30 m) the
    // cell is <= 2 m (collider + visual); the rest of a large grid is a visual-only "far" grid (<= 64 cells a side).
    V3 pmn = play.mn, pmx = play.mx; bool hasPlay = pmn.x < pmx.x;
    if (!hasPlay) { pmn = bmin; pmx = bmax; }
    pmn = pmn - new V3(30, 30, 30); pmx = pmx + new V3(30, 30, 30);
    int gridN = 0;
    void EmitGrid(string nm, double x0, double z0, double x1, double z1, double cell, Func<double, double, double> hAt, string keys) {
      int nx = Math.Max(2, (int)Math.Ceiling((x1 - x0) / cell) + 1), nz = Math.Max(2, (int)Math.Ceiling((z1 - z0) / cell) + 1); double dx = (x1 - x0) / (nx - 1), dz = (z1 - z0) / (nz - 1);
      var h = new double[nx * nz]; double hmin = 1e9, hmax = -1e9;
      for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++) { var v = hAt(x0 + i * dx, z0 + j * dz); h[j * nx + i] = v; if (!double.IsNaN(v)) { hmin = Math.Min(hmin, v); hmax = Math.Max(hmax, v); } }
      if (hmin > hmax) return;
      var id = Y.Enc(nm) + "#" + (gridN++);
      body.Add($"H grid {id} {Y.F(x0)} {Y.F(hmin)} {Y.F(z0)} {Y.F(dx)} {Y.F(dz)} {nx} {nz} {keys}");
      for (int j = 0; j < nz; j++) { var sb = new StringBuilder(); sb.Append($"H row {id} {j}"); for (int i = 0; i < nx; i++) { var v = h[j * nx + i]; sb.Append(' ').Append(double.IsNaN(v) ? "n" : Y.F(Math.Round(v - hmin, 2))); } body.Add(sb.ToString()); }
      st.H++; B(new V3(x0, hmin, z0)); B(new V3(x1, hmax, z1));
      st.Notes.Add($"H {id}: {nx}x{nz} cells of {Y.F(dx)} x {Y.F(dz)} m from x {Y.F(x0)}..{Y.F(x1)}, z {Y.F(z0)}..{Y.F(z1)}, y {Y.F(hmin)}..{Y.F(hmax)}; {keys}");
    }
    void EmitRegions(string nm, V3 mn, V3 mx, Func<double, double, double> hAt, bool col, bool vis, string keys) {
      double ix0 = Math.Max(mn.x, pmn.x), iz0 = Math.Max(mn.z, pmn.z), ix1 = Math.Min(mx.x, pmx.x), iz1 = Math.Min(mx.z, pmx.z);
      bool inter = ix1 > ix0 && iz1 > iz0;
      bool beyond = !inter || ix0 - mn.x > 10 || iz0 - mn.z > 10 || mx.x - ix1 > 10 || mx.z - iz1 > 10;
      if (!col) { double ex = Math.Max(mx.x - mn.x, mx.z - mn.z); double cl = Math.Max(2.0, ex / 64); if (vis) EmitGrid(nm, mn.x, mn.z, mx.x, mx.z, cl, hAt, $"{keys} collider=0 vis=1" + (cl > 2 ? " far=1" : "")); return; }
      if (inter) EmitGrid(nm, ix0, iz0, ix1, iz1, 2.0, hAt, $"{keys} collider=1 vis={(vis ? 1 : 0)}");
      if (beyond && vis) { double ext = Math.Max(mx.x - mn.x, mx.z - mn.z); EmitGrid(nm + "~far", mn.x, mn.z, mx.x, mx.z, Math.Max(2.0, ext / 64), hAt, $"{keys} collider=0 vis=1 far=1"); }
    }
    foreach (var t in m.Terr) {
      if (t.H == null || t.Res < 2) { st.Notes.Add($"terrain {t.Name}: {t.Note}"); continue; }
      if (!m.Active(t.Go)) { st.Notes.Add($"terrain {t.Name}: inactive"); continue; }
      var w = m.GoW(t.Go); double sx = t.Scale.x * (t.Res - 1), sz = t.Scale.z * (t.Res - 1), sy = t.Scale.y;
      if (sx <= 0 || sz <= 0 || sy <= 0) { st.Notes.Add($"terrain {t.Name}: zero size (scale {t.Scale}), skipped"); continue; }
      st.Terrain = true;
      double HS(double wx, double wz) { // heights[z * res + x], normalised by 32766
        double u = Math.Clamp((wx - w.p.x) / t.Scale.x, 0, t.Res - 1), v = Math.Clamp((wz - w.p.z) / t.Scale.z, 0, t.Res - 1); int x0 = (int)Math.Floor(u), z0 = (int)Math.Floor(v); int x1 = Math.Min(x0 + 1, t.Res - 1), z1 = Math.Min(z0 + 1, t.Res - 1); double fx = u - x0, fz = v - z0;
        double h(int x, int z) => t.H[z * t.Res + x] / 32766.0 * sy;
        return w.p.y + (h(x0, z0) * (1 - fx) + h(x1, z0) * fx) * (1 - fz) + (h(x0, z1) * (1 - fx) + h(x1, z1) * fx) * fz;
      }
      string mat = t.Mat != null && ix.Mat.Contains(t.Mat) ? t.Mat : "Goblin_Ground_Green_Mat";
      var layers = string.Join(",", t.Layers.Select(l => Y.Enc(l.tex ?? "?")));
      EmitRegions(t.Name, w.p, w.p + new V3(sx, 0, sz), HS, t.Collider, true, $"src=terrain mat={Y.Enc(mat)}{(mat != t.Mat ? " mat.guess=1 was=" + Y.Enc(t.Mat ?? "?") : "")} layers={layers} res={t.Res} size={Y.F(sx)},{Y.F(sy)},{Y.F(sz)}");
      st.Notes.Add($"terrain {t.Name}: {t.Res}x{t.Res} heightmap, {Y.F(sx)} x {Y.F(sz)} m, height scale {Y.F(sy)} m, at {w.p}; material {t.Mat}; layers {string.Join(", ", t.Layers.Select(l => l.tex))}; collider {t.Collider}");
    }
    foreach (var g in grounds.Values) {
      V3 mn = g.wv.Aggregate(new V3(1e9, 1e9, 1e9), V3.Min), mx = g.wv.Aggregate(new V3(-1e9, -1e9, -1e9), V3.Max);
      if (!g.col && mx.y - mn.y < 0.3) { var c = g.rgba ?? new double[] { 0.5, 0.6, 0.7, 1 }; var cc = (mn + mx) * 0.5; var e = (mx - mn) * 0.5; e = new V3(e.x, Math.Max(e.y, 0.02), e.z); body.Add($"X box {T3(cc)} {T3(e)} 0 0 0 1 {Y.F(c[0])} {Y.F(c[1])} {Y.F(c[2])} {Y.F(c.Length > 3 ? Math.Clamp(c[3], 0, 1) : 1)}"); st.X++; BBox(cc, e, Q.I); continue; }
      double ext = Math.Max(mx.x - mn.x, mx.z - mn.z); double rc = ext > 600 ? 2.0 : 1.0;
      var gr = Shape.Ground(g.wv, g.tris, rc);
      double HM(double wx, double wz) { int i = (int)Math.Round((wx - gr.o.x) / gr.dx), j = (int)Math.Round((wz - gr.o.z) / gr.dz); if (i < 0 || j < 0 || i >= gr.nx || j >= gr.nz) return double.NaN; return gr.h[j * gr.nx + i]; }
      EmitRegions(g.mi.Name, mn, mx, HM, g.col, g.vis, $"src=mesh mat={Y.Enc(g.mat ?? "?")} go={Y.Enc(m.Name(g.go))}");
      st.HMesh++;
    }

    // 5. lights, audio
    foreach (var l in m.Lights) {
      if (!l.En || !m.Active(l.Go) || covered.Contains(l.Go)) continue; var w = m.GoW(l.Go); var col = l.Col ?? new double[] { 1, 1, 1, 1 };
      string ty = l.Type switch { 0 => "spot", 1 => "dir", 3 => "area", _ => "point" };
      // iteration 8: shadows= and strength= after the numbers (older kits read the 13 numbers and ignore the rest)
      body.Add($"L {ty} {T3(w.p)} {T4(w.r)} {Y.F(col[0])} {Y.F(col[1])} {Y.F(col[2])} {Y.F(l.Int)} {Y.F(l.Range)} {Y.F(l.Spot)}" + (l.Shadows != 0 ? $" shadows={(l.Shadows == 1 ? "hard" : "soft")} strength={Y.F(l.Strength)}" : "") + UrpKeys.Of(m, l.Go, m.L.Read(m.F, m.F.ById[l.Pid]))); st.L++; if (l.Type != 1) B(w.p);
    }
    foreach (var a in m.Aud) {
      if (!a.En || !m.Active(a.Go) || a.Clip == null || covered.Contains(a.Go)) continue; var w = m.GoW(a.Go);
      string cn = ix.Clips.Contains(a.Clip) ? a.Clip : ix.ClipCI.TryGetValue(a.Clip, out var c2) ? c2 : null;
      if (cn != null) st.AKnown++;
      body.Add($"A {(cn != null ? Y.Enc(cn) : "?" + Y.Enc(a.Clip))} {T3(w.p)} {Y.F(a.Vol)} {Y.F(a.Min)} {Y.F(a.Max)} {(a.Loop ? 1 : 0)}" + (a.Roll == 1 || a.Roll == 2 ? " roll=" + a.Roll : "")); st.A++;
    }
    // signs (TextMeshPro text) for the report
    foreach (var mb in m.Mbs) if (mb.Script.StartsWith("TMPro.TextMeshPro") && mb.D?.Str("m_text") is string tx && tx.Trim() != "" && m.Active(mb.Go)) st.Signs.Add(tx.Replace("\n", " ").Trim());

    // environment
    var env = new List<string>();
    if (m.RS != null) {
      var rs = m.RS; var fc = (double[])rs["m_FogColor"]; int fm = (int)rs["m_FogMode"];
      env.Add($"E fog {((bool)rs["m_Fog"] ? fm switch { 1 => "linear", 2 => "exp", _ => "exp2" } : "off")} {Y.F(fc[0])} {Y.F(fc[1])} {Y.F(fc[2])} {Y.F((float)rs["m_FogDensity"])} {Y.F((float)rs["m_LinearFogStart"])} {Y.F((float)rs["m_LinearFogEnd"])}");
      var sky = (double[])rs["m_AmbientSkyColor"]; var eq = (double[])rs["m_AmbientEquatorColor"]; var gr = (double[])rs["m_AmbientGroundColor"]; int am = (int)rs["m_AmbientMode"];
      env.Add($"E ambient {Y.F(sky[0])} {Y.F(sky[1])} {Y.F(sky[2])} mode={(am switch { 0 => "skybox", 1 => "trilight", 3 => "flat", _ => am.ToString() })} intensity={Y.F((float)rs["m_AmbientIntensity"])} equator={Y.F(eq[0])},{Y.F(eq[1])},{Y.F(eq[2])} ground={Y.F(gr[0])},{Y.F(gr[1])},{Y.F(gr[2])}");
      var sp = ((int, long))rs["sky"]; var skn = m.L.NameOf(m.F, sp); if (skn != null) env.Add($"E sky {(ix.Mat.Contains(skn) ? Y.Enc(skn) : "?" + Y.Enc(skn))}");
      // iteration 8: the full RenderSettings (Env.RsRaw) for the reflection strength and RenderSettings.sun, and the key
      // light as "E sun" (RenderSettings.sun, else the brightest enabled directional light); see Env.cs / README.
      foreach (var o in m.F.Objects) if (o.ClassId == 104) {
        var full = Env.RsRaw(m.F, o);
        env.Add($"E refl intensity={Y.F((float)full["reflIntensity"])} mode={((int)full["reflMode"] == 1 ? "custom" : "skybox")}");
        var sunPtr = ((int, long))full["sun"]; LightN key = null; double best = -1;
        foreach (var l in m.Lights) {
          if (!l.En || l.Type != 1 || !m.Active(l.Go)) continue;
          if (sunPtr.Item1 == 0 && sunPtr.Item2 == l.Pid) { key = l; break; }
          var c = l.Col ?? new double[] { 1, 1, 1, 1 }; double lum = l.Int * (0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]); if (lum > best) { best = lum; key = l; }
        }
        if (key != null) {
          var w = m.GoW(key.Go); var c = key.Col ?? new double[] { 1, 1, 1, 1 };
          env.Add($"E sun {T3(w.p)} {T4(w.r)} {Y.F(c[0])} {Y.F(c[1])} {Y.F(c[2])} {Y.F(key.Int)} shadows={(key.Shadows == 0 ? "none" : key.Shadows == 1 ? "hard" : "soft")} strength={Y.F(key.Strength)}");
        } else env.Add("E sun none");
      }
    }

    var sbo = new StringBuilder();
    sbo.Append($"# RRSK 1 {scene}\n");
    sbo.Append($"B {T3(bmin)} {T3(bmax)}\n");
    sbo.Append($"# source: {source}; frame = the level's stored Unity world space (Y up), nothing re-centred.\n");
    sbo.Append($"# derived placement data only (names and numbers); meshes/materials are loaded from the player's PC install by name.\n");
    sbo.Append($"# renderers {st.Rend} (runtime-mesh {st.RendNoMesh}), install mesh match {st.Matched}/{st.Rend - st.RendNoMesh} = {st.MatchPct:F1}%, stand-ins {st.X}, material slots known {st.SlotsKnown}/{st.Slots}; colliders {st.C} (mesh {st.CMesh}, mesh->box {st.CMeshBox}); P {st.P}; G {st.G}; terrain grids {st.H}\n");
    sbo.Append("# H grid <name> ox oy oz dx dz nx nz key=..  +  H row <name> <j> h0..h(nx-1): terrain heights (m above oy) at x = ox + i*dx, z = oz + j*dz\n");
    foreach (var kv in meshT.OrderBy(x => x.Value)) sbo.Append($"M {kv.Value} {Y.Enc(kv.Key)}\n");
    foreach (var kv in matT.OrderBy(x => x.Value)) sbo.Append($"T {kv.Value} {(kv.Key.StartsWith("?") ? "?" + Y.Enc(kv.Key.Substring(1)) : Y.Enc(kv.Key))}\n");
    foreach (var e in env) sbo.Append(e).Append('\n');
    foreach (var l in body) sbo.Append(l).Append('\n');
    File.WriteAllText(outPath, sbo.ToString());
    return st;
  }
}
