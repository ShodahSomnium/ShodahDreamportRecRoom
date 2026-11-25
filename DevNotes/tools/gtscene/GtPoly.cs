// gtscene prep poly=1 (2026-11-23, the Rec Center shell as clean planar polygons): the level-only hull triangles that
// jt=1 would turn into rectangle plates are grouped per material, plane (normal within GTPOLY_ANG = 1 degree, every
// vertex within GTPOLY_OFF = 1 cm of the plane) and vertex colour (within GTPOLY_COL = 0.03 per channel, so Rec Room's
// Wall_Vertcolor bands stay apart); each group's triangles are united into its outline rings (welded at 0.5 mm,
// T-junctions split, interior edges cancelled, the boundary traced face by face: outer rings counter-clockwise seen from
// the front, holes clockwise), and the rings are simplified (near-collinear points removed, Douglas-Peucker within
// GTPOLY_TOL = 1 cm in 3D, between points where the set of polygons sharing a boundary point changes, so two
// neighbours simplify their shared edge identically: no new gaps, no overlaps). One "G poly" record per outer ring with
// its holes (RrJumbotronPoly.cs: the format and the triangulator, compiled in from the world by path). A group whose
// triangles overlap each other (the union is not the sum) is split into its edge-connected parts; a part that still
// overlaps is written triangle by triangle. Data only: names and numbers (outlines, colours).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using SomniumSpace.Worlds.RecRoomDreamport.Games.Quests.Jumbotron;

static class Polys {
  static double Env(string k, double d) => double.TryParse(Environment.GetEnvironmentVariable(k), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;
  // GTPOLY_SHARE: a curved piece (not rectangles) becomes polygons when this share of its area is in coplanar groups of 3+
  // triangles (0 = every curved piece as its facets, exact; 1.01 = none: fitted rectangles as jt=1)
  public static readonly double Share = Env("GTPOLY_SHARE", 0.5);
  public static readonly double Tol = Env("GTPOLY_TOL", 0.01), AngTol = Env("GTPOLY_ANG", 1.0), OffTol = Env("GTPOLY_OFF", 0.01), ColTol = Env("GTPOLY_COL", 0.03);
  const double Weld = 0.0005;

  /// The triangles of one material collected for polygons (world space, front faces as the level draws them; rgb per triangle).
  public sealed class Src { public string Mat; public bool Two, HasCol; public readonly List<V3> P = new(); public readonly List<double> C = new(); public int Tris => P.Count / 3;
    /// (rcscene round 2) per triangle: the measured colour sa r,g,b + emission sg r,g,b (null: none) and a record suffix
    /// (" ua=.. rep=.. node=..", null: none); triangles group only with equal suffixes and sa/sg within ColTol
    public readonly List<double[]> SA = new(); public readonly List<string> Tag = new(); }

  public static void Add(Dictionary<string, Src> into, string mat, bool two, V3[] w, int[] t, double[] cols) {
    if (!into.TryGetValue(mat, out var s)) into[mat] = s = new Src { Mat = mat, Two = two };
    s.Two |= two; if (cols != null) s.HasCol = true;
    for (int i = 0; i + 2 < t.Length; i += 3) {
      s.P.Add(w[t[i]]); s.P.Add(w[t[i + 1]]); s.P.Add(w[t[i + 2]]);
      if (cols != null) for (int ch = 0; ch < 3; ch++) s.C.Add((cols[t[i] * 3 + ch] + cols[t[i + 1] * 3 + ch] + cols[t[i + 2] * 3 + ch]) / 3);
      else { s.C.Add(1); s.C.Add(1); s.C.Add(1); }
      s.SA.Add(null); s.Tag.Add(null);
    }
  }

  /// Add with per-triangle measurements (rcscene round 2): sa(i) = the i-th triangle's sa r,g,b + sg r,g,b (or null),
  /// tag = the record suffix every triangle of this call carries (or null).
  public static void AddTagged(Dictionary<string, Src> into, string mat, bool two, V3[] w, int[] t, double[] cols, Func<int, double[]> sa, string tag) {
    int before = into.TryGetValue(mat, out var s0) ? s0.Tris : 0;
    Add(into, mat, two, w, t, cols);
    var s = into[mat];
    for (int i = 0; i + 2 < t.Length; i += 3) { s.SA[before + i / 3] = sa?.Invoke(i / 3); s.Tag[before + i / 3] = tag; }
  }

  static bool SameExtra(Src s, int a, int b) {
    if (s.Tag[a] != s.Tag[b]) return false;
    var x = s.SA[a]; var y = s.SA[b]; if (x == null || y == null) return x == y;
    for (int i = 0; i < x.Length; i++) if (Math.Abs(x[i] - y[i]) > ColTol) return false;
    return true;
  }

  /// The share of a piece's triangle area that lies in coplanar groups of 3 or more triangles (normal to ~0.3 degree,
  /// offset 1 cm): ~1 for a planar fan or wedge floor, ~0 for a curved surface (each facet its own pair of triangles).
  public static double PlanarShare(V3[] w, int[] t) {
    var by = new Dictionary<(int, int, int, long), (int n, double a)>(); double all = 0;
    for (int i = 0; i + 2 < t.Length; i += 3) {
      var cr = Cross(w[t[i + 1]] - w[t[i]], w[t[i + 2]] - w[t[i]]); double l = cr.Len(); if (l < 1e-12) continue;
      var n = Snap(cr * (1 / l)); var key = ((int)Math.Round(n.x * 200), (int)Math.Round(n.y * 200), (int)Math.Round(n.z * 200), (long)Math.Round(Dot(n, w[t[i]]) * 100));
      var v = by.GetValueOrDefault(key); by[key] = (v.n + 1, v.a + l * 0.5); all += l * 0.5;
    }
    return all > 0 ? by.Values.Where(v => v.n >= 3).Sum(v => v.a) / all : 0;
  }

  static V3 Cross(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  static double Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
  static V3 Nrm(V3 a) { var l = a.Len(); return l < 1e-15 ? new V3(0, 1, 0) : a * (1 / l); }
  static V3 Snap(V3 n) {
    if (Math.Abs(n.x) > 0.99985) return new V3(Math.Sign(n.x), 0, 0);
    if (Math.Abs(n.y) > 0.99985) return new V3(0, Math.Sign(n.y), 0);
    if (Math.Abs(n.z) > 0.99985) return new V3(0, 0, Math.Sign(n.z));
    return n;
  }

  /// One plane + colour group of one material: its welded outline points and rings.
  sealed class Grp {
    public int Id; public Src S; public V3 N, U, V; public double D, Area, R, G, B; public List<int> T = new(); public string Suffix = "";
    public readonly List<V3> P3 = new(); public readonly List<double> X = new(), Y = new();
    public List<List<int>> Loops = new(), Simp; public string Note;
  }

  public sealed class Stat {
    public int Srcs, Tris, Groups, Records, Rings, Holes, PtsIn, PtsOut, Split, TriFallback, TriFallbackTris, Reverted, TjSplits, BadTri, MaxPts; public double TriArea, PolyArea; public List<string> Log = new();
  }

  /// The records for every collected material (sorted by material, then plane), and the counts.
  public static List<string> Build(Dictionary<string, Src> srcs, Stat st) {
    var groups = new List<Grp>();
    foreach (var s in srcs.Values.OrderBy(x => x.Mat, StringComparer.Ordinal)) { st.Srcs++; st.Tris += s.Tris; foreach (var g in Planes(s)) { g.Id = groups.Count; groups.Add(g); } }
    // outline rings per group; a group whose triangles overlap is split (edge-connected parts, then single triangles)
    var all = new List<Grp>();
    foreach (var g in groups) Outline(g, all, st);
    for (int i = 0; i < all.Count; i++) all[i].Id = i;
    st.Groups = all.Count;
    Simplify(all, st);
    var res = new List<string>();
    foreach (var g in all) Emit(g, res, st);
    return res;
  }

  // ------------------------------------------------------------------ planes and colours

  static List<Grp> Planes(Src s) {
    int nt = s.Tris; var n = new V3[nt]; var ar = new double[nt]; var ok = new bool[nt];
    for (int k = 0; k < nt; k++) {
      var cr = Cross(s.P[k * 3 + 1] - s.P[k * 3], s.P[k * 3 + 2] - s.P[k * 3]); double l = cr.Len();
      if (l < 2e-8) continue; ok[k] = true; n[k] = Snap(cr * (1 / l)); ar[k] = l * 0.5;
    }
    var order = Enumerable.Range(0, nt).Where(k => ok[k]).OrderByDescending(k => ar[k]).ThenBy(k => k).ToList();
    // normal clusters (within AngTol of the cluster's first, biggest triangle)
    double cosT = Math.Cos(AngTol * Math.PI / 180);
    var ncell = new Dictionary<(int, int, int), List<int>>(); var nrep = new List<V3>(); var nmem = new List<List<int>>();
    (int, int, int) NK(V3 v) => ((int)Math.Round(v.x * 20), (int)Math.Round(v.y * 20), (int)Math.Round(v.z * 20));
    foreach (int k in order) {
      var key = NK(n[k]); int found = -1;
      for (int dx = -1; dx <= 1 && found < 0; dx++) for (int dy = -1; dy <= 1 && found < 0; dy++) for (int dz = -1; dz <= 1 && found < 0; dz++)
        if (ncell.TryGetValue((key.Item1 + dx, key.Item2 + dy, key.Item3 + dz), out var li)) foreach (int c in li) if (Dot(nrep[c], n[k]) >= cosT) { found = c; break; }
      if (found < 0) { found = nrep.Count; nrep.Add(n[k]); nmem.Add(new List<int>()); if (!ncell.TryGetValue(key, out var li2)) ncell[key] = li2 = new List<int>(); li2.Add(found); }
      nmem[found].Add(k);
    }
    var res = new List<Grp>();
    for (int c = 0; c < nrep.Count; c++) {
      var N = nrep[c];
      // offsets along the cluster's normal (triangle centroids), swept in OffTol windows; every vertex must lie within OffTol
      var byD = nmem[c].Select(k => (k, d: Dot(N, (s.P[k * 3] + s.P[k * 3 + 1] + s.P[k * 3 + 2]) * (1.0 / 3)))).OrderBy(x => x.d).ThenBy(x => x.k).ToList();
      int i = 0;
      while (i < byD.Count) {
        int j = i; double d0 = byD[i].d; while (j + 1 < byD.Count && byD[j + 1].d - d0 <= OffTol) j++;
        var mem = byD.Skip(i).Take(j - i + 1).Select(x => x.k).ToList(); i = j + 1;
        // the plane: area-weighted normal (snapped) and offset
        V3 Ns = default; double A = 0; foreach (int k in mem) { Ns = Ns + n[k] * ar[k]; A += ar[k]; }
        Ns = Snap(Nrm(Ns)); double D = 0; foreach (int k in mem) D += Dot(Ns, (s.P[k * 3] + s.P[k * 3 + 1] + s.P[k * 3 + 2]) * (1.0 / 3)) * ar[k]; D /= A;
        var inPlane = new List<int>(); var outPlane = new List<int>();
        foreach (int k in mem) { bool f = true; for (int v = 0; v < 3; v++) if (Math.Abs(Dot(Ns, s.P[k * 3 + v]) - D) > OffTol) f = false; (f ? inPlane : outPlane).Add(k); }
        // a triangle off the plane (a big one turned a hair more than the cluster's): its own plane
        foreach (int k in outPlane) res.AddRange(Colours(s, new List<int> { k }, n[k], Dot(n[k], s.P[k * 3]), ar));
        if (inPlane.Count > 0) res.AddRange(Colours(s, inPlane, Ns, D, ar));
      }
    }
    return res;
  }

  static IEnumerable<Grp> Colours(Src s, List<int> mem, V3 N, double D, double[] ar) {
    var groups = new List<Grp>();
    foreach (int k in mem.OrderByDescending(k => ar[k]).ThenBy(k => k)) {
      double r = s.C[k * 3], g = s.C[k * 3 + 1], b = s.C[k * 3 + 2];
      var hit = groups.FirstOrDefault(x => Math.Abs(x.R - r) <= ColTol && Math.Abs(x.G - g) <= ColTol && Math.Abs(x.B - b) <= ColTol && SameExtra(s, x.T[0], k));
      if (hit == null) { hit = new Grp { S = s, N = N, D = D, R = r, G = g, B = b }; groups.Add(hit); }
      hit.T.Add(k);
    }
    foreach (var gr in groups) {
      // the mean colour, area-weighted
      double A = 0, R = 0, G = 0, B = 0; foreach (int k in gr.T) { A += ar[k]; R += s.C[k * 3] * ar[k]; G += s.C[k * 3 + 1] * ar[k]; B += s.C[k * 3 + 2] * ar[k]; }
      gr.Area = A; gr.R = R / A; gr.G = G / A; gr.B = B / A;
      // rcscene round 2: the measured sa= (and sg=) as the group's area-weighted mean, then the tag
      if (s.SA[gr.T[0]] != null) {
        var m = new double[s.SA[gr.T[0]].Length]; double W = 0;
        foreach (int k in gr.T) { var x = s.SA[k]; if (x == null) continue; W += ar[k]; for (int i = 0; i < m.Length; i++) m[i] += x[i] * ar[k]; }
        if (W > 0) {
          for (int i = 0; i < m.Length; i++) m[i] /= W;
          gr.Suffix = $" sa={F3(m[0])},{F3(m[1])},{F3(m[2])}";
          if (m.Length >= 6 && m[3] + m[4] + m[5] > 0.012) gr.Suffix += $" sg={F3(m[3])},{F3(m[4])},{F3(m[5])}";
        }
      }
      gr.Suffix += s.Tag[gr.T[0]] ?? "";
      RrJumbotronPolyMath.Frame(N.x, N.y, N.z, out double ux, out double uy, out double uz, out double vx, out double vy, out double vz);
      gr.U = new V3(ux, uy, uz); gr.V = new V3(vx, vy, vz);
      yield return gr;
    }
  }

  // ------------------------------------------------------------------ union outline

  sealed class Weld2 {
    readonly Dictionary<(long, long), List<int>> cell = new(); readonly Grp g;
    public Weld2(Grp g) { this.g = g; }
    public int Id(V3 p) {
      double x = Dot(p, g.U), y = Dot(p, g.V); long cx = (long)Math.Floor(x / Weld), cy = (long)Math.Floor(y / Weld);
      for (long a = cx - 1; a <= cx + 1; a++) for (long b = cy - 1; b <= cy + 1; b++)
        if (cell.TryGetValue((a, b), out var li)) foreach (int i in li) { double dx = g.X[i] - x, dy = g.Y[i] - y; if (dx * dx + dy * dy <= Weld * Weld) return i; }
      int id = g.X.Count; g.X.Add(x); g.Y.Add(y); g.P3.Add(p);
      if (!cell.TryGetValue((cx, cy), out var l2)) cell[(cx, cy)] = l2 = new List<int>(); l2.Add(id); return id;
    }
  }

  /// The group's rings into g.Loops; overlapping triangles split it (into list `into`).
  static void Outline(Grp g, List<Grp> into, Stat st) {
    var w = new Weld2(g); var tris = new List<(int a, int b, int c)>();
    foreach (int k in g.T) {
      int a = w.Id(g.S.P[k * 3]), b = w.Id(g.S.P[k * 3 + 1]), c = w.Id(g.S.P[k * 3 + 2]);
      if (a == b || b == c || a == c) continue;
      double sa = (g.X[b] - g.X[a]) * (g.Y[c] - g.Y[a]) - (g.Y[b] - g.Y[a]) * (g.X[c] - g.X[a]);
      if (Math.Abs(sa) < 1e-9) continue;
      if (sa < 0) { st.BadTri++; (b, c) = (c, b); }
      tris.Add((a, b, c));
    }
    st.TriArea += g.Area;
    if (tris.Count == 0) return;
    var loops = Loops(g, tris, st, out bool overlap);
    if (!overlap) { g.Loops = loops; into.Add(g); return; }
    // overlapping triangles: their edge-connected parts, each on its own
    st.Split++;
    var parts = EdgeParts(tris);
    st.Log.Add($"{g.S.Mat}: {tris.Count} triangles overlap on plane n=({g.N.x:F3},{g.N.y:F3},{g.N.z:F3}) d={g.D:F3} c=({g.R:F2},{g.G:F2},{g.B:F2}): {parts.Count} edge-connected parts");
    foreach (var part in parts) {
      var pl = Loops(g, part, st, out bool ov2);
      var sub = new Grp { S = g.S, N = g.N, U = g.U, V = g.V, D = g.D, R = g.R, G = g.G, B = g.B, Area = 0, Suffix = g.Suffix };
      // the sub-group shares the parent's welded points
      sub.X.AddRange(g.X); sub.Y.AddRange(g.Y); sub.P3.AddRange(g.P3);
      if (!ov2) { sub.Loops = pl; into.Add(sub); continue; }
      st.TriFallback++; st.TriFallbackTris += part.Count;
      st.Log.Add($"{g.S.Mat}:   a part of {part.Count} triangles still overlaps: written triangle by triangle");
      foreach (var t in part) { var s1 = new Grp { S = g.S, N = g.N, U = g.U, V = g.V, D = g.D, R = g.R, G = g.G, B = g.B, Suffix = g.Suffix }; s1.X.AddRange(g.X); s1.Y.AddRange(g.Y); s1.P3.AddRange(g.P3); s1.Loops = new List<List<int>> { new List<int> { t.a, t.b, t.c } }; into.Add(s1); }
    }
  }

  static List<List<(int a, int b, int c)>> EdgeParts(List<(int a, int b, int c)> tris) {
    int n = tris.Count; var par = new int[n]; for (int i = 0; i < n; i++) par[i] = i;
    int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
    var at = new Dictionary<(int, int), int>();
    for (int i = 0; i < n; i++) {
      var t = tris[i];
      foreach (var e in new[] { (t.a, t.b), (t.b, t.c), (t.c, t.a) }) {
        var k = e.Item1 < e.Item2 ? e : (e.Item2, e.Item1);
        if (at.TryGetValue(k, out int o)) { int ra = Find(o), rb = Find(i); if (ra != rb) par[ra] = rb; } else at[k] = i;
      }
    }
    return Enumerable.Range(0, n).GroupBy(Find).Select(gr => gr.Select(i => tris[i]).ToList()).ToList();
  }

  /// Boundary rings of a triangle set: T-junctions split, opposite edges cancelled, traced keeping the face on the left.
  static List<List<int>> Loops(Grp g, List<(int a, int b, int c)> tris, Stat st, out bool overlap) {
    overlap = false;
    // points of this set, for T-junction splits (a point within 0.5 mm of an edge, strictly between its ends)
    var used = new HashSet<int>(); foreach (var t in tris) { used.Add(t.a); used.Add(t.b); used.Add(t.c); }
    double x0 = 1e18, y0 = 1e18, x1 = -1e18, y1 = -1e18; foreach (int i in used) { x0 = Math.Min(x0, g.X[i]); y0 = Math.Min(y0, g.Y[i]); x1 = Math.Max(x1, g.X[i]); y1 = Math.Max(y1, g.Y[i]); }
    double h = Math.Max(0.02, Math.Max(x1 - x0, y1 - y0) / 256);
    var grid = new Dictionary<(long, long), List<int>>();
    foreach (int i in used) { var k = ((long)Math.Floor(g.X[i] / h), (long)Math.Floor(g.Y[i] / h)); if (!grid.TryGetValue(k, out var li)) grid[k] = li = new List<int>(); li.Add(i); }
    var cnt = new Dictionary<long, int>();
    void AddEdge(int a, int b) { long k = ((long)a << 32) | (uint)b; cnt[k] = cnt.GetValueOrDefault(k) + 1; }
    var on = new List<(double t, int i)>(); var seen = new HashSet<int>();
    foreach (var t in tris) {
      foreach (var (a, b) in new[] { (t.a, t.b), (t.b, t.c), (t.c, t.a) }) {
        double ax = g.X[a], ay = g.Y[a], dx = g.X[b] - ax, dy = g.Y[b] - ay, L2 = dx * dx + dy * dy, L = Math.Sqrt(L2);
        on.Clear(); seen.Clear();
        int steps = (int)Math.Ceiling(L / (h * 0.5)) + 1;
        for (int s = 0; s <= steps; s++) {
          double px = ax + dx * s / steps, py = ay + dy * s / steps; long cx = (long)Math.Floor(px / h), cy = (long)Math.Floor(py / h);
          for (long u = cx - 1; u <= cx + 1; u++) for (long v = cy - 1; v <= cy + 1; v++) {
            if (!grid.TryGetValue((u, v), out var li)) continue;
            foreach (int i in li) {
              if (i == a || i == b || !seen.Add(i)) continue;
              double qx = g.X[i] - ax, qy = g.Y[i] - ay, tt = (qx * dx + qy * dy) / L2; if (tt <= 1e-9 || tt >= 1 - 1e-9) continue;
              double ex = qx - dx * tt, ey = qy - dy * tt; if (ex * ex + ey * ey > Weld * Weld) continue;
              on.Add((tt, i));
            }
          }
        }
        if (on.Count == 0) { AddEdge(a, b); continue; }
        st.TjSplits += on.Count;
        on.Sort((p, q) => p.t.CompareTo(q.t)); int prev = a; foreach (var o in on) { if (o.i != prev) AddEdge(prev, o.i); prev = o.i; } AddEdge(prev, b);
      }
    }
    // cancel opposite edges; what is left is the boundary (a net count over 1: overlapping triangles)
    var outs = new Dictionary<int, List<int>>(); int edges = 0;
    foreach (var kv in cnt) {
      int a = (int)(kv.Key >> 32), b = (int)(uint)kv.Key; long rk = ((long)b << 32) | (uint)a;
      int net = kv.Value - cnt.GetValueOrDefault(rk);
      if (net <= 0) continue;
      if (net > 1) overlap = true;
      for (int r = 0; r < net; r++) { if (!outs.TryGetValue(a, out var li)) outs[a] = li = new List<int>(); li.Add(b); edges++; }
    }
    // trace: at a point with several ways on, the one just clockwise of the way back (the face on the left stays one ring)
    var loops = new List<List<int>>();
    foreach (int start in outs.Keys.OrderBy(x => x).ToList()) {
      while (outs.TryGetValue(start, out var sl) && sl.Count > 0) {
        var loop = new List<int> { start }; int prev = start, cur = sl[0]; sl.RemoveAt(0); int guard = 0;
        while (cur != start && guard++ < 10000000) {
          loop.Add(cur);
          if (!outs.TryGetValue(cur, out var cl) || cl.Count == 0) { overlap = true; break; }
          int pick = 0;
          if (cl.Count > 1) {
            double bx = g.X[prev] - g.X[cur], by = g.Y[prev] - g.Y[cur], ba = Math.Atan2(by, bx), best = -1;
            for (int q = 0; q < cl.Count; q++) {
              double ang = Math.Atan2(g.Y[cl[q]] - g.Y[cur], g.X[cl[q]] - g.X[cur]) - ba; while (ang <= 1e-12) ang += 2 * Math.PI; while (ang > 2 * Math.PI) ang -= 2 * Math.PI;
              if (ang > best) { best = ang; pick = q; }
            }
          }
          int nx = cl[pick]; cl.RemoveAt(pick); prev = cur; cur = nx;
        }
        if (loop.Count >= 3) loops.Add(loop);
      }
    }
    if (overlap) return loops;
    // the rings must be the union: no boundary edges crossing, and outer / hole rings nested with the right parity
    if (Crossing(g, loops)) { overlap = true; return loops; }
    var area = loops.Select(l => Area(g, l)).ToList();
    for (int i = 0; i < loops.Count; i++) {
      // a point just left of ring i's first edge (0.1 mm): inside the face for an outer ring and a hole alike, so the
      // winding number of all rings there must be 1 (2 or more: triangles over each other)
      var l = loops[i]; int a = l[0], b = l[1]; double ex = g.X[b] - g.X[a], ey = g.Y[b] - g.Y[a], el = Math.Sqrt(ex * ex + ey * ey); if (el < 1e-9) continue;
      double px = (g.X[a] + g.X[b]) * 0.5 - ey / el * 1e-4, py = (g.Y[a] + g.Y[b]) * 0.5 + ex / el * 1e-4;
      int wn = 0; foreach (var lj in loops) wn += Winding(g, lj, px, py);
      if (wn != 1) { overlap = true; return loops; }
    }
    return loops;
  }

  static double Area(Grp g, List<int> l) { double a = 0; for (int i = 0; i < l.Count; i++) { int p = l[i], q = l[(i + 1) % l.Count]; a += g.X[p] * g.Y[q] - g.X[q] * g.Y[p]; } return a * 0.5; }

  static int Winding(Grp g, List<int> l, double px, double py) {
    int wn = 0;
    for (int i = 0; i < l.Count; i++) {
      double ax = g.X[l[i]], ay = g.Y[l[i]], bx = g.X[l[(i + 1) % l.Count]], by = g.Y[l[(i + 1) % l.Count]];
      double s = (bx - ax) * (py - ay) - (px - ax) * (by - ay);
      if (ay <= py) { if (by > py && s > 0) wn++; } else if (by <= py && s < 0) wn--;
    }
    return wn;
  }

  /// Whether two ring edges cross (touching at a shared point is not crossing).
  static bool Crossing(Grp g, List<List<int>> loops) {
    var segs = new List<(int a, int b)>(); foreach (var l in loops) for (int i = 0; i < l.Count; i++) segs.Add((l[i], l[(i + 1) % l.Count]));
    if (segs.Count < 4) return false;
    double x0 = 1e18, y0 = 1e18, x1 = -1e18, y1 = -1e18; foreach (var s in segs) { x0 = Math.Min(x0, g.X[s.a]); y0 = Math.Min(y0, g.Y[s.a]); x1 = Math.Max(x1, g.X[s.a]); y1 = Math.Max(y1, g.Y[s.a]); }
    int n = Math.Clamp((int)Math.Sqrt(segs.Count), 1, 256); double cw = Math.Max(1e-6, (x1 - x0) / n), ch = Math.Max(1e-6, (y1 - y0) / n);
    var cells = new Dictionary<(int, int), List<int>>();
    for (int k = 0; k < segs.Count; k++) {
      var s = segs[k]; int i0 = (int)((Math.Min(g.X[s.a], g.X[s.b]) - x0) / cw), i1 = (int)((Math.Max(g.X[s.a], g.X[s.b]) - x0) / cw), j0 = (int)((Math.Min(g.Y[s.a], g.Y[s.b]) - y0) / ch), j1 = (int)((Math.Max(g.Y[s.a], g.Y[s.b]) - y0) / ch);
      for (int i = i0; i <= i1; i++) for (int j = j0; j <= j1; j++) { if (!cells.TryGetValue((i, j), out var li)) cells[(i, j)] = li = new List<int>(); li.Add(k); }
    }
    var tried = new HashSet<long>();
    foreach (var li in cells.Values) for (int p = 0; p < li.Count; p++) for (int q = p + 1; q < li.Count; q++) {
      int s1 = Math.Min(li[p], li[q]), s2 = Math.Max(li[p], li[q]); if (!tried.Add(((long)s1 << 32) | (uint)s2)) continue;
      var A = segs[s1]; var B = segs[s2];
      if (A.a == B.a || A.a == B.b || A.b == B.a || A.b == B.b) continue;
      if (Proper(g.X[A.a], g.Y[A.a], g.X[A.b], g.Y[A.b], g.X[B.a], g.Y[B.a], g.X[B.b], g.Y[B.b])) return true;
    }
    return false;
  }

  static bool Proper(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy) {
    double d1 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax), d2 = (bx - ax) * (dy - ay) - (by - ay) * (dx - ax);
    double d3 = (dx - cx) * (ay - cy) - (dy - cy) * (ax - cx), d4 = (dx - cx) * (by - cy) - (dy - cy) * (bx - cx);
    const double e = 1e-12;
    if (((d1 > e && d2 < -e) || (d1 < -e && d2 > e)) && ((d3 > e && d4 < -e) || (d3 < -e && d4 > e))) return true;
    // a point of one lying on the other (collinear overlap or T): crossing too
    bool On(double px, double py, double qx, double qy, double rx, double ry, double d) => Math.Abs(d) <= e && Math.Min(px, qx) - 1e-9 <= rx && rx <= Math.Max(px, qx) + 1e-9 && Math.Min(py, qy) - 1e-9 <= ry && ry <= Math.Max(py, qy) + 1e-9;
    return On(ax, ay, bx, by, cx, cy, d1) || On(ax, ay, bx, by, dx, dy, d2) || On(cx, cy, dx, dy, ax, ay, d3) || On(cx, cy, dx, dy, bx, by, d4);
  }

  // ------------------------------------------------------------------ simplification shared by neighbours

  static (long, long, long) K3(V3 p) => ((long)Math.Round(p.x * 1000), (long)Math.Round(p.y * 1000), (long)Math.Round(p.z * 1000));

  static void Simplify(List<Grp> all, Stat st) {
    // which groups use each boundary point (1 mm)
    var users = new Dictionary<(long, long, long), List<int>>();
    foreach (var g in all) foreach (var l in g.Loops) foreach (int i in l) { var k = K3(g.P3[i]); if (!users.TryGetValue(k, out var li)) users[k] = li = new List<int>(); if (li.Count == 0 || li[^1] != g.Id) if (!li.Contains(g.Id)) li.Add(g.Id); }
    var setId = new Dictionary<(long, long, long), int>(); var sets = new Dictionary<string, int>();
    foreach (var kv in users) { kv.Value.Sort(); var s = string.Join(",", kv.Value); if (!sets.TryGetValue(s, out int id)) sets[s] = id = sets.Count; setId[kv.Key] = id; }
    foreach (var g in all) {
      g.Simp = new List<List<int>>();
      foreach (var l in g.Loops) {
        st.PtsIn += l.Count; int n = l.Count;
        var key = l.Select(i => K3(g.P3[i])).ToArray(); var sid = key.Select(k => setId[k]).ToArray();
        var cnt = new Dictionary<(long, long, long), int>(); foreach (var k in key) cnt[k] = cnt.GetValueOrDefault(k) + 1;
        var fix = new bool[n]; int nf = 0;
        for (int i = 0; i < n; i++) { if (sid[i] != sid[(i + n - 1) % n] || sid[i] != sid[(i + 1) % n] || cnt[key[i]] > 1) { fix[i] = true; nf++; } }
        if (nf == 0) {
          // one set all round: the smallest point and the one farthest from it (the same pair from either side)
          int a = 0; for (int i = 1; i < n; i++) if (key[i].CompareTo(key[a]) < 0) a = i;
          int b = -1; double bd = -1; for (int i = 0; i < n; i++) { if (i == a) continue; double d = (g.P3[l[i]] - g.P3[l[a]]).Len(); if (d > bd + 1e-12 || (Math.Abs(d - bd) <= 1e-12 && key[i].CompareTo(key[b]) < 0)) { bd = d; b = i; } }
          fix[a] = true; if (b >= 0) fix[b] = true;
        }
        var keep = (bool[])fix.Clone();
        int f0 = Array.IndexOf(fix, true);
        for (int s = 0; s < n; ) {
          int i = (f0 + s) % n; int j = s + 1; while (j < n && !fix[(f0 + j) % n]) j++;
          // chain i .. (f0 + j) % n (inclusive), Douglas-Peucker in 3D, ties to the smaller point key
          var chain = new List<int>(); for (int q = s; q <= j; q++) chain.Add((f0 + q) % n);
          Dp(g, l, key, chain, 0, chain.Count - 1, keep);
          s = j;
        }
        var sl = new List<int>(); for (int i = 0; i < n; i++) if (keep[i]) sl.Add(l[i]);
        g.Simp.Add(sl);
      }
      // still the same rings: each at least 3 points, same orientation, area within the tolerance, no crossings
      bool ok = true; double per = 0;
      for (int r = 0; r < g.Loops.Count && ok; r++) {
        var a0 = Area(g, g.Loops[r]); var a1 = g.Simp[r].Count >= 3 ? Area(g, g.Simp[r]) : 0;
        per = 0; var l = g.Loops[r]; for (int i = 0; i < l.Count; i++) { int p = l[i], q = l[(i + 1) % l.Count]; per += Math.Sqrt((g.X[q] - g.X[p]) * (g.X[q] - g.X[p]) + (g.Y[q] - g.Y[p]) * (g.Y[q] - g.Y[p])); }
        if (g.Simp[r].Count < 3 || Math.Sign(a0) != Math.Sign(a1) || Math.Abs(a1 - a0) > Tol * per + 1e-6) ok = false;
      }
      if (ok && Crossing(g, g.Simp)) ok = false;
      if (!ok) { st.Reverted++; g.Simp = g.Loops; }
      foreach (var l in g.Simp) st.PtsOut += l.Count;
    }
  }

  static void Dp(Grp g, List<int> l, (long, long, long)[] key, List<int> chain, int lo, int hi, bool[] keep) {
    if (hi - lo < 2) return;
    var A = g.P3[l[chain[lo]]]; var B = g.P3[l[chain[hi]]]; var ab = B - A; double L2 = Dot(ab, ab);
    int best = -1; double bd = -1;
    for (int q = lo + 1; q < hi; q++) {
      var p = g.P3[l[chain[q]]]; double d;
      if (L2 < 1e-18) d = (p - A).Len();
      else { double t = Math.Clamp(Dot(p - A, ab) / L2, 0, 1); d = (p - (A + ab * t)).Len(); }
      if (d > bd + 1e-9 || (Math.Abs(d - bd) <= 1e-9 && key[chain[q]].CompareTo(key[chain[best]]) < 0)) { bd = d; best = q; }
    }
    if (bd <= Tol) return;
    keep[chain[best]] = true;
    Dp(g, l, key, chain, lo, best, keep); Dp(g, l, key, chain, best, hi, keep);
  }

  // ------------------------------------------------------------------ records

  static string F3(double v) { var r = Math.Round(v, 3); if (r == 0) r = 0; return r.ToString("0.###", CultureInfo.InvariantCulture); }

  static void Emit(Grp g, List<string> res, Stat st) {
    var rings = g.Simp ?? g.Loops; if (rings.Count == 0) return;
    var outer = new List<int>(); var holes = new List<int>(); var area = rings.Select(l => Area(g, l)).ToList();
    for (int r = 0; r < rings.Count; r++) (area[r] > 0 ? outer : holes).Add(r);
    // each hole to the smallest outer ring around it
    var mine = outer.ToDictionary(o => o, o => new List<int>());
    foreach (int hidx in holes) {
      var hl = rings[hidx]; int best = -1; double ba = double.MaxValue;
      foreach (int o in outer) {
        if (area[o] >= ba) continue;
        // a hole point not on the outer ring, inside it
        int inside = 0, outside = 0;
        foreach (int i in hl.Take(8)) { if (rings[o].Contains(i)) continue; if (Winding(g, rings[o], g.X[i], g.Y[i]) != 0) inside++; else outside++; }
        if (inside > outside) { best = o; ba = area[o]; }
      }
      if (best >= 0) mine[best].Add(hidx); else st.Log.Add($"{g.S.Mat}: a hole of {-area[hidx]:F3} m2 in no ring (left out)");
    }
    foreach (int o in outer) {
      var ringList = new List<List<int>> { rings[o] }; ringList.AddRange(mine[o].Select(x => rings[x]));
      // the centre (bounds of the outer ring, on the plane)
      double x0 = 1e18, y0 = 1e18, x1 = -1e18, y1 = -1e18; foreach (int i in rings[o]) { x0 = Math.Min(x0, g.X[i]); y0 = Math.Min(y0, g.Y[i]); x1 = Math.Max(x1, g.X[i]); y1 = Math.Max(y1, g.Y[i]); }
      var c = g.U * ((x0 + x1) * 0.5) + g.V * ((y0 + y1) * 0.5) + g.N * g.D;
      string Ring(List<int> l) => string.Join(",", l.Select(i => $"{F3(g.P3[i].x)},{F3(g.P3[i].y)},{F3(g.P3[i].z)}"));
      var sb = new StringBuilder();
      sb.Append("G poly ").Append(Y.Enc(g.S.Mat)).Append(' ').Append(F3(c.x)).Append(' ').Append(F3(c.y)).Append(' ').Append(F3(c.z));
      sb.Append(" n=").Append(Y.F(g.N.x)).Append(',').Append(Y.F(g.N.y)).Append(',').Append(Y.F(g.N.z));
      sb.Append(" o=").Append(Ring(rings[o]));
      if (mine[o].Count > 0) sb.Append(" h=").Append(string.Join(";", mine[o].Select(x => Ring(rings[x]))));
      if (g.S.HasCol && (g.R < 0.97 || g.G < 0.97 || g.B < 0.97)) sb.Append(" c=").Append(F3(g.R)).Append(',').Append(F3(g.G)).Append(',').Append(F3(g.B));
      if (g.S.Two) sb.Append(" ds=1");
      sb.Append(g.Suffix);
      res.Add(sb.ToString());
      st.Records++; st.Rings += ringList.Count; st.Holes += ringList.Count - 1; st.MaxPts = Math.Max(st.MaxPts, ringList.Sum(x => x.Count));
      st.PolyArea += area[o] + mine[o].Sum(x => area[x]);
    }
  }

  /// Checks a "G poly" record the way the world reads it (RrJumbotronPolyMath): its triangles (scene points) and the
  /// triangulated over polygon area; null when malformed.
  public static List<(V3 a, V3 b, V3 c)> Triangles(string line, out V3 n, out double ratio, out double[] col, out bool two, out string mat) {
    n = default; ratio = 0; col = null; two = false; mat = null;
    var p = line.Split(' '); if (p.Length < 7 || p[0] != "G" || p[1] != "poly") return null;
    mat = p[2]; var cx = double.Parse(p[3], CultureInfo.InvariantCulture); var cy = double.Parse(p[4], CultureInfo.InvariantCulture); var cz = double.Parse(p[5], CultureInfo.InvariantCulture);
    string Key(string k) => p.Skip(6).FirstOrDefault(x => x.StartsWith(k + "="))?.Substring(k.Length + 1);
    var nn = Key("n")?.Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray(); if (nn == null || nn.Length < 3) return null;
    n = Nrm(new V3(nn[0], nn[1], nn[2]));
    var xyz = new List<double>(); var starts = new List<int>();
    if (!RrJumbotronPolyMath.ParseRings(Key("o"), xyz, starts)) return null;
    var h = Key("h"); if (!string.IsNullOrEmpty(h) && !RrJumbotronPolyMath.ParseRings(h, xyz, starts)) return null;
    var cs = Key("c"); if (cs != null) col = cs.Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
    two = Key("ds") == "1";
    RrJumbotronPolyMath.Frame(n.x, n.y, n.z, out double ux, out double uy, out double uz, out double vx, out double vy, out double vz);
    int count = xyz.Count / 3; var xy = new double[count * 2]; var pts = new V3[count];
    for (int i = 0; i < count; i++) { double px = xyz[i * 3] - cx, py = xyz[i * 3 + 1] - cy, pz = xyz[i * 3 + 2] - cz; xy[i * 2] = px * ux + py * uy + pz * uz; xy[i * 2 + 1] = px * vx + py * vy + pz * vz; pts[i] = new V3(xyz[i * 3], xyz[i * 3 + 1], xyz[i * 3 + 2]); }
    var tris = new List<int>(); ratio = RrJumbotronPolyMath.Triangulate(xy, count, starts, tris);
    var res = new List<(V3, V3, V3)>(); for (int t = 0; t + 2 < tris.Count; t += 3) res.Add((pts[tris[t]], pts[tris[t + 1]], pts[tris[t + 2]]));
    return res;
  }
}

/// polymerge: a layout that was post-processed after its jt=1 prep (tree markers, terrain, K records, underlays...) gets
/// the poly=1 prep's hull instead of the base prep's hull rectangles. The hull "G plate" lines of the base prep (the
/// first plates-minus-stand-in-plates "G plate" lines, from its "# gtscene:" counts) are taken out of the layout wherever
/// they are (a multiset: the post-processing kept them verbatim); in their place go the poly prep's hull lines ("G plate"
/// of the curved pieces and every "G poly"), except those of materials the layout no longer has any hull plate of (the
/// trees it turned into "G tree" markers). Everything else of the layout stays as it is, in order.
static class PolyMerge {
  static (List<string> hull, int dress) Hull(string[] lines) {
    int n = 0, dress = 0;
    foreach (var l in lines) {
      var m = Regex.Match(l, @"^# gtscene: hull \d+ renderers -> (\d+) plates"); if (m.Success) n = int.Parse(m.Groups[1].Value);
      var d = Regex.Match(l, @"^# gtscene jt=1: stand-ins re-measured: .*\((\d+) plates\)"); if (d.Success) dress = int.Parse(d.Groups[1].Value);
    }
    int hullN = n - dress; var hull = new List<string>(); int k = 0;
    foreach (var l in lines) {
      if (l.StartsWith("G plate ")) { if (k++ < hullN) hull.Add(l); }
      else if (l.StartsWith("G poly ")) hull.Add(l);
    }
    return (hull, hullN);
  }
  static string Mat(string l) => l.Split(' ', 4)[2];
  public static void Run(string[] a) {
    var lay = File.ReadAllLines(a[1]); var (baseHull, bn) = Hull(File.ReadAllLines(a[2])); var (polyHull, pn) = Hull(File.ReadAllLines(a[3]));
    var take = new Dictionary<string, int>(); foreach (var l in baseHull) take[l] = take.GetValueOrDefault(l) + 1;
    var removed = new Dictionary<string, int>(); var kept = new HashSet<string>(); var outl = new List<string>(); int at = -1, gone = 0;
    foreach (var l in lay) {
      if (l.StartsWith("G plate ") && take.TryGetValue(l, out int c) && c > 0) { take[l] = c - 1; if (at < 0) at = outl.Count; gone++; kept.Add(Mat(l)); continue; }
      outl.Add(l);
    }
    // materials whose hull the layout dropped (their plates are all gone from it): not brought back
    var baseMats = new HashSet<string>(baseHull.Select(Mat));
    var dropped = new HashSet<string>(baseMats.Where(m => !kept.Contains(m)));
    var add = polyHull.Where(l => !dropped.Contains(Mat(l))).ToList();
    int polys = add.Count(l => l.StartsWith("G poly ")), plates = add.Count - polys;
    if (at < 0) at = outl.Count;
    outl.InsertRange(at, add);
    // the note, after the header's last comment line before the first record
    int hdr = outl.FindIndex(l => !l.StartsWith("#") && !l.StartsWith("B ")); if (hdr < 0) hdr = 0;
    outl.Insert(hdr, $"# 2026-11-23 (gtscene polymerge): the hull's {gone} rectangle plates (of the base jt=1 prep's {bn}) replaced by the poly=1 prep's {polys} planar polygons (\"G poly\") and {plates} fitted rectangles of curved pieces; left to the layout's own markers: {string.Join(", ", dropped.OrderBy(x => x))}");
    File.WriteAllText(a[4], string.Join("\n", outl) + "\n");
    Console.WriteLine($"polymerge: {gone} hull plates out ({take.Values.Sum()} of the base hull not in the layout), {polys} polygons + {plates} plates in; dropped materials: {string.Join(",", dropped)}; {new FileInfo(a[4]).Length / 1024} KB");
  }
}

/// polycheck <layout.rrsk>: coplanar overlaps between "G poly" records of different materials (same plane within 0.1
/// degree and 3 mm, overlap sampled at 2 cm), and between polygons and "G plate" rectangles of other materials; the
/// triangulation of every record (the world's RrJumbotronPolyMath) and the vertex / size totals.
static class PolyCheck {
  sealed class P { public string Mat, Line; public V3 N; public double D; public List<(V3 a, V3 b, V3 c)> T; public V3 Mn, Mx; public double Area; }
  static double Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
  static bool InTri(V3 p, (V3 a, V3 b, V3 c) t, V3 n) {
    double s1 = Dot(Cr(t.b - t.a, p - t.a), n), s2 = Dot(Cr(t.c - t.b, p - t.b), n), s3 = Dot(Cr(t.a - t.c, p - t.c), n);
    return s1 >= -1e-9 && s2 >= -1e-9 && s3 >= -1e-9;
  }
  static V3 Cr(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  public static void Run(string[] a) {
    var ps = new List<P>(); int bad = 0, pts = 0; long chars = 0;
    foreach (var l in File.ReadLines(a[1])) {
      if (!l.StartsWith("G poly ")) continue; chars += l.Length + 1;
      var t = Polys.Triangles(l, out var n, out double ratio, out var col, out bool two, out string mat); if (t == null || ratio < 0.99 || ratio > 1.01) { bad++; if (t == null) continue; }
      var p = new P { Mat = mat, Line = l, N = n, T = t, Mn = new V3(1e9, 1e9, 1e9), Mx = new V3(-1e9, -1e9, -1e9) };
      foreach (var x in t) foreach (var v in new[] { x.a, x.b, x.c }) { p.Mn = V3.Min(p.Mn, v); p.Mx = V3.Max(p.Mx, v); }
      p.D = Dot(n, t[0].a); p.Area = t.Sum(x => Cr(x.b - x.a, x.c - x.a).Len() * 0.5);
      pts += l.Split(' ').Where(x => x.StartsWith("o=") || x.StartsWith("h=")).Sum(x => x.Split(',', ';').Length / 3);
      ps.Add(p);
    }
    Console.WriteLine($"polycheck: {ps.Count} polygons, {pts} points, {ps.Sum(x => x.T.Count)} triangles, {ps.Sum(x => x.Area):F0} m2, {chars / 1024} KB of records; {bad} not triangulated to their area");
    // coplanar pairs of different materials (bounding boxes overlap): area of the smaller one inside the other, 2 cm samples
    var byPlane = ps.GroupBy(p => ((int)Math.Round(p.N.x * 100), (int)Math.Round(p.N.y * 100), (int)Math.Round(p.N.z * 100), (long)Math.Round(p.D * 100)));
    double total = 0; int pairs = 0; var worst = new List<(double, string)>();
    var all = byPlane.ToDictionary(g => g.Key, g => g.ToList());
    foreach (var kv in all) {
      var cand = new List<P>(kv.Value);
      for (int dd = -1; dd <= 1; dd += 2) { var k2 = (kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Key.Item4 + dd); if (all.TryGetValue(k2, out var o)) cand.AddRange(o); }
      foreach (var p in kv.Value) foreach (var q in cand) {
        if (string.CompareOrdinal(p.Mat, q.Mat) >= 0 || Dot(p.N, q.N) < 0.99999 || Math.Abs(p.D - Dot(p.N, q.T[0].a)) > 0.003) continue;
        if (p.Mn.x > q.Mx.x + 1e-3 || q.Mn.x > p.Mx.x + 1e-3 || p.Mn.y > q.Mx.y + 1e-3 || q.Mn.y > p.Mx.y + 1e-3 || p.Mn.z > q.Mx.z + 1e-3 || q.Mn.z > p.Mx.z + 1e-3) continue;
        var small = p.Area <= q.Area ? p : q; var big = small == p ? q : p; double ov = 0;
        foreach (var t in small.T) {
          // samples: a 2 cm lattice of the triangle (barycentric)
          double la = Math.Max((t.b - t.a).Len(), Math.Max((t.c - t.b).Len(), (t.a - t.c).Len())); int m = Math.Clamp((int)(la / 0.02), 2, 200);
          double ta = Cr(t.b - t.a, t.c - t.a).Len() * 0.5; int hit = 0, tot = 0;
          for (int i = 0; i < m; i++) for (int j = 0; i + j < m; j++) { var s = t.a + (t.b - t.a) * ((i + 0.33) / m) + (t.c - t.a) * ((j + 0.33) / m); tot++; if (big.T.Any(u => InTri(s, u, big.N))) hit++; }
          ov += ta * hit / Math.Max(1, tot);
        }
        if (ov > 1e-4) { pairs++; total += ov; worst.Add((ov, $"{small.Mat} in {big.Mat} at {small.Line.Split(' ')[3]},{small.Line.Split(' ')[4]},{small.Line.Split(' ')[5]}: {ov:F3} m2")); }
      }
    }
    Console.WriteLine($"polycheck: coplanar overlaps between materials: {pairs} pairs, {total:F2} m2");
    foreach (var w in worst.OrderByDescending(x => x.Item1).Take(15)) Console.WriteLine("  " + w.Item2);
    // plates of other materials lying on a polygon (their centre on its face, same plane within 3 mm): the world lifts them
    var idx = ps.GroupBy(p => ((int)Math.Round(p.N.x * 100), (int)Math.Round(p.N.y * 100), (int)Math.Round(p.N.z * 100))).ToDictionary(g => g.Key, g => g.ToList());
    int on = 0, plates = 0; var mats = new Dictionary<string, int>();
    foreach (var l in File.ReadLines(a[1])) {
      if (!l.StartsWith("G plate ")) continue; plates++;
      var t = l.Split(' '); var c = new V3(double.Parse(t[3], CultureInfo.InvariantCulture), double.Parse(t[4], CultureInfo.InvariantCulture), double.Parse(t[5], CultureInfo.InvariantCulture));
      var q = new Q(double.Parse(t[6], CultureInfo.InvariantCulture), double.Parse(t[7], CultureInfo.InvariantCulture), double.Parse(t[8], CultureInfo.InvariantCulture), double.Parse(t[9], CultureInfo.InvariantCulture)); var n = q.Rot(new V3(0, 0, 1));
      if (!idx.TryGetValue(((int)Math.Round(n.x * 100), (int)Math.Round(n.y * 100), (int)Math.Round(n.z * 100)), out var li)) continue;
      foreach (var p in li) {
        if (p.Mat == t[2] || Dot(p.N, n) < 0.9995 || Math.Abs(Dot(p.N, c) - p.D) > 0.003 || c.x < p.Mn.x - 1e-3 || c.x > p.Mx.x + 1e-3 || c.y < p.Mn.y - 1e-3 || c.y > p.Mx.y + 1e-3 || c.z < p.Mn.z - 1e-3 || c.z > p.Mx.z + 1e-3) continue;
        if (p.T.Any(u => InTri(c, u, p.N))) { on++; mats[t[2] + " on " + p.Mat] = mats.GetValueOrDefault(t[2] + " on " + p.Mat) + 1; break; }
      }
    }
    Console.WriteLine($"polycheck: {on} of {plates} plates lie on a polygon of another material: {string.Join(", ", mats.OrderByDescending(x => x.Value).Take(12).Select(x => x.Key + " " + x.Value))}");
  }
}
