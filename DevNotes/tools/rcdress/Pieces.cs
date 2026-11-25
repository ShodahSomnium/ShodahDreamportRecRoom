// rcdress pieces <work> <levelN> <goNameRe> [n]: the first n renderers whose GameObject name matches: the mesh split into
// connected pieces (vertices welded at 0.1 mm), per piece and material its box in the renderer's local frame (mesh units x
// the renderer's scale), triangle area, mean colour through its own UVs, and the up-facing share. Numbers only.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

static class PiecesCmd {
  public static void Run(string work, string level, string goRe, int max) {
    var L = new Ld(work); var m = Model.Load(L, level); var re = new Regex(goRe);
    int shown = 0;
    foreach (var r in m.Rend) {
      if (r.Mesh == null || !m.Gos.TryGetValue(r.Go, out var g) || !re.IsMatch(g.Name)) continue;
      if (shown++ >= max) break;
      var w = m.GoW(r.Go); var (lv, uv, subs) = UvGeo.Decode(L, r.Mesh); if (lv == null) { Console.WriteLine("unreadable"); continue; }
      Console.WriteLine($"== {m.PathOf(r.Go)}  mesh {r.Mesh.Name} pos {w.p} rot {w.r} scale {w.s}");
      var rd = L.Read(m.F, m.F.ById[r.Id]); var mptr = rd?.Lst("m_Materials") ?? new List<object>();
      var lp = lv.Select(p => w.s.Mul(p)).ToArray();
      // weld
      var key = new Dictionary<(long, long, long), int>(); var id = new int[lp.Length];
      for (int i = 0; i < lp.Length; i++) { var k = ((long)Math.Round(lp[i].x * 1e4), (long)Math.Round(lp[i].y * 1e4), (long)Math.Round(lp[i].z * 1e4)); if (!key.TryGetValue(k, out var j)) key[k] = j = key.Count; id[i] = j; }
      var par = Enumerable.Range(0, key.Count).ToArray();
      int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
      foreach (var t in subs) for (int i = 0; i + 2 < t.Length; i += 3) { int a = Find(id[t[i]]), b = Find(id[t[i + 1]]), c = Find(id[t[i + 2]]); par[a] = b; par[Find(b)] = Find(c); }
      var acc = new Dictionary<(int, int), (double area, double up, double r, double g, double b, V3 mn, V3 mx)>();
      for (int k = 0; k < subs.Count; k++) {
        Tex tex = null; if (k < mptr.Count) { var (mf, mo) = L.Res(m.F, DX.Ptr(mptr[k])); if (mo != null) tex = PlateColPub.TexOf(L, mf, mo, k < r.Mats.Count ? r.Mats[k].name : "?"); }
        var t = subs[k];
        for (int i = 0; i + 2 < t.Length; i += 3) {
          var a = lp[t[i]]; var b = lp[t[i + 1]]; var c = lp[t[i + 2]];
          var cr = new V3((b - a).y * (c - a).z - (b - a).z * (c - a).y, (b - a).z * (c - a).x - (b - a).x * (c - a).z, (b - a).x * (c - a).y - (b - a).y * (c - a).x);
          double ar = cr.Len() * 0.5; if (ar < 1e-9) continue;
          var col = tex != null ? tex.At((uv[t[i] * 2] + uv[t[i + 1] * 2] + uv[t[i + 2] * 2]) / 3, (uv[t[i] * 2 + 1] + uv[t[i + 1] * 2 + 1] + uv[t[i + 2] * 2 + 1]) / 3) : new double[] { 1, 1, 1 };
          var pk = (Find(id[t[i]]), k);
          acc.TryGetValue(pk, out var q);
          if (q.area == 0) { q.mn = V3.Min(a, V3.Min(b, c)); q.mx = V3.Max(a, V3.Max(b, c)); }
          q.area += ar; q.r += col[0] * ar; q.g += col[1] * ar; q.b += col[2] * ar; q.up += Math.Abs(cr.y) / (2 * ar) > 0.9 ? ar : 0;
          q.mn = V3.Min(q.mn, V3.Min(a, V3.Min(b, c))); q.mx = V3.Max(q.mx, V3.Max(a, V3.Max(b, c)));
          acc[pk] = q;
        }
      }
      if (Environment.GetEnvironmentVariable("RC_VIEW") != null) {
        // side (x right, y up) and top (x right, z up) occupancy of the largest piece, 4 cm cells
        var big = acc.OrderByDescending(x => x.Value.area).First().Key; var q0 = acc[big]; double cs = 0.04;
        int nx = (int)((q0.mx.x - q0.mn.x) / cs) + 1, ny = (int)((q0.mx.y - q0.mn.y) / cs) + 1, nz = (int)((q0.mx.z - q0.mn.z) / cs) + 1;
        var side = new bool[nx, ny]; var top = new bool[nx, nz]; var t0 = subs[big.Item2];
        for (int i = 0; i + 2 < t0.Length; i += 3) {
          if (Find(id[t0[i]]) != big.Item1) continue;
          for (int s = 0; s <= 10; s++) for (int u2 = 0; u2 <= 10 - s; u2++) {
            var P = lp[t0[i]] * (1 - s / 10.0 - u2 / 10.0) + lp[t0[i + 1]] * (s / 10.0) + lp[t0[i + 2]] * (u2 / 10.0);
            int ix = Math.Clamp((int)((P.x - q0.mn.x) / cs), 0, nx - 1), iy = Math.Clamp((int)((P.y - q0.mn.y) / cs), 0, ny - 1), iz = Math.Clamp((int)((P.z - q0.mn.z) / cs), 0, nz - 1);
            side[ix, iy] = true; top[ix, iz] = true;
          }
        }
        Console.WriteLine($"  side view x {q0.mn.x:0.00}..{q0.mx.x:0.00} (right), y {q0.mn.y:0.00}..{q0.mx.y:0.00} (up), 4 cm");
        for (int y = ny - 1; y >= 0; y--) { var sb = new System.Text.StringBuilder("   "); for (int x = 0; x < nx; x++) sb.Append(side[x, y] ? '#' : '.'); Console.WriteLine(sb); }
        Console.WriteLine($"  top view z {q0.mn.z:0.00}..{q0.mx.z:0.00} (up)");
        for (int z = nz - 1; z >= 0; z--) { var sb = new System.Text.StringBuilder("   "); for (int x = 0; x < nx; x++) sb.Append(top[x, z] ? '#' : '.'); Console.WriteLine(sb); }
      }
      foreach (var kv in acc.OrderByDescending(x => x.Value.area)) {
        var q = kv.Value; var c = (q.mn + q.mx) * 0.5; var e = (q.mx - q.mn) * 0.5;
        Console.WriteLine($"  sub {kv.Key.Item2} area {q.area,7:0.0000}  up {q.up / q.area:0.00}  col {q.r / q.area:0.###},{q.g / q.area:0.###},{q.b / q.area:0.###}  centre {c}  half {e}");
      }
    }
  }
}
static class PlateColPub { public static Tex TexOf(Ld L, AF mf, AObj mo, string name) => PlateCol.TexOf(L, mf, mo, name); }
