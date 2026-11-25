// apkscene: shapes for meshes that are NOT in the PC install. The mesh is read to MEASURE it; only derived numbers are
// written: a coarse height grid (ground-like meshes, <= 2 m cells) or a set of boxes from a coarse voxel shell
// (everything else). No vertex or index data is written anywhere.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

static class MeshGeo {
  static readonly Dictionary<(string, long), (V3[] v, int[] t)> cache = new();
  static int FmtSize(int f) => f switch { 0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 2, 5 => 2, 6 => 1, 7 => 1, 8 => 2, 9 => 2, 10 => 4, 11 => 4, _ => 4 };
  public static (V3[] v, int[] t) Decode(Ld L, MeshInfo mi) {
    if (mi == null) return (null, null);
    if (cache.TryGetValue((mi.File, mi.Pid), out var c)) return c;
    (V3[], int[]) res = (null, null);
    try {
      var f = L.File(mi.File); var d = L.Read(f, f.ById[mi.Pid]);
      var vd = d.Dct("m_VertexData"); int n = (int)vd.Num("m_VertexCount");
      var chans = vd.Lst("m_Channels").Cast<Dictionary<string, object>>().Select(x => (s: (int)x.Num("stream"), o: (int)x.Num("offset"), fmt: (int)x.Num("format"), dim: (int)x.Num("dimension") & 15)).ToList();
      byte[] data = vd.G("m_DataSize") as byte[];
      if (data == null || data.Length == 0) {
        var sd = d.Dct("m_StreamData"); var path = Ld.Norm(sd?.Str("path")); long off = (long)(sd?.Num("offset") ?? 0); long size = (long)(sd?.Num("size") ?? 0);
        if (size > 0 && path != null && L.D.Has(path)) { var all = L.D.Get(path); data = new byte[size]; Buffer.BlockCopy(all, (int)off, data, 0, (int)size); }
      }
      if (data == null || n == 0 || chans.Count == 0 || chans[0].dim < 3) { cache[(mi.File, mi.Pid)] = res; return res; }
      int maxS = chans.Where(x => x.dim > 0).Max(x => x.s);
      var sOff = new int[maxS + 1]; var sStride = new int[maxS + 1]; int o = 0;
      for (int s = 0; s <= maxS; s++) { sOff[s] = o; sStride[s] = chans.Where(x => x.s == s && x.dim > 0).Select(x => x.o + FmtSize(x.fmt) * x.dim).DefaultIfEmpty(0).Max(); o += sStride[s] * n; o = (o + 15) & ~15; }
      var p = chans[0]; var v = new V3[n];
      for (int i = 0; i < n; i++) {
        int b = sOff[p.s] + i * sStride[p.s] + p.o;
        if (p.fmt == 0) v[i] = new V3(BitConverter.ToSingle(data, b), BitConverter.ToSingle(data, b + 4), BitConverter.ToSingle(data, b + 8));
        else if (p.fmt == 1) v[i] = new V3((double)BitConverter.ToHalf(data, b), (double)BitConverter.ToHalf(data, b + 2), (double)BitConverter.ToHalf(data, b + 4));
        else { cache[(mi.File, mi.Pid)] = res; return res; }
      }
      var ib = d.G("m_IndexBuffer") as byte[]; bool i32 = d.Num("m_IndexFormat") == 1;
      var tl = new List<int>();
      foreach (var sx in d.Lst("m_SubMeshes").Cast<Dictionary<string, object>>()) {
        if (sx.Num("topology") != 0) continue; int fb = (int)sx.Num("firstByte"), ic = (int)sx.Num("indexCount"), bv = (int)sx.Num("baseVertex");
        for (int k = 0; k < ic; k++) tl.Add((i32 ? BitConverter.ToInt32(ib, fb + k * 4) : BitConverter.ToUInt16(ib, fb + k * 2)) + bv);
      }
      res = (v, tl.ToArray());
    } catch { res = (null, null); }
    cache[(mi.File, mi.Pid)] = res; return res;
  }
}

static class Shape {
  /// Up-facing share of the triangle area (world space).
  public static double UpShare(V3[] w, int[] t) {
    double up = 0, all = 0;
    for (int i = 0; i + 2 < t.Length; i += 3) { var a = w[t[i]]; var b = w[t[i + 1]]; var c = w[t[i + 2]]; var n = Cross(b - a, c - a); double ar = n.Len(); all += ar; if (ar > 0 && Math.Abs(n.y) / ar > 0.7) up += ar; }
    return all <= 0 ? 0 : up / all;
  }
  public static V3 Cross(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  /// Top surface heights on a grid over the XZ bounds (NaN = no surface). World-space vertices.
  public static (V3 o, double dx, double dz, int nx, int nz, double[] h) Ground(V3[] w, int[] t, double cell) {
    V3 mn = w.Aggregate(new V3(1e9, 1e9, 1e9), V3.Min), mx = w.Aggregate(new V3(-1e9, -1e9, -1e9), V3.Max);
    int nx = (int)Math.Ceiling((mx.x - mn.x) / cell) + 1, nz = (int)Math.Ceiling((mx.z - mn.z) / cell) + 1;
    double dx = nx > 1 ? (mx.x - mn.x) / (nx - 1) : cell, dz = nz > 1 ? (mx.z - mn.z) / (nz - 1) : cell;
    var h = new double[nx * nz]; Array.Fill(h, double.NaN);
    for (int i = 0; i + 2 < t.Length; i += 3) {
      var a = w[t[i]]; var b = w[t[i + 1]]; var c = w[t[i + 2]];
      int x0 = Math.Max(0, (int)Math.Floor((Math.Min(a.x, Math.Min(b.x, c.x)) - mn.x) / dx)), x1 = Math.Min(nx - 1, (int)Math.Ceiling((Math.Max(a.x, Math.Max(b.x, c.x)) - mn.x) / dx));
      int z0 = Math.Max(0, (int)Math.Floor((Math.Min(a.z, Math.Min(b.z, c.z)) - mn.z) / dz)), z1 = Math.Min(nz - 1, (int)Math.Ceiling((Math.Max(a.z, Math.Max(b.z, c.z)) - mn.z) / dz));
      double den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z); if (Math.Abs(den) < 1e-12) continue;
      for (int zi = z0; zi <= z1; zi++) for (int xi = x0; xi <= x1; xi++) {
        double px = mn.x + xi * dx, pz = mn.z + zi * dz;
        double l1 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / den, l2 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / den, l3 = 1 - l1 - l2;
        const double e = -1e-4; if (l1 < e || l2 < e || l3 < e) continue;
        double y = l1 * a.y + l2 * b.y + l3 * c.y; int k = zi * nx + xi; if (double.IsNaN(h[k]) || y > h[k]) h[k] = y;
      }
    }
    return (mn, dx, dz, nx, nz, h);
  }
  /// Boxes (centre, size) in the given frame from a voxel shell of the triangles, greedily merged. Grows the voxel
  /// until the box count is <= maxBoxes.
  public static (List<(V3 c, V3 s)> boxes, double vox) Boxes(V3[] v, int[] t, int maxBoxes, double div, double sMin, double sMax) {
    V3 mn = v.Aggregate(new V3(1e9, 1e9, 1e9), V3.Min), mx = v.Aggregate(new V3(-1e9, -1e9, -1e9), V3.Max);
    double ext = (mx - mn).Max(); double s = Math.Clamp(ext / div, sMin, sMax);
    for (int attempt = 0; attempt < 6; attempt++, s *= 1.5) {
      int nx = Math.Max(1, (int)Math.Ceiling((mx.x - mn.x) / s)), ny = Math.Max(1, (int)Math.Ceiling((mx.y - mn.y) / s)), nz = Math.Max(1, (int)Math.Ceiling((mx.z - mn.z) / s));
      if ((long)nx * ny * nz > 40_000_000) continue;
      var occ = new bool[nx * ny * nz];
      int Id(int x, int y, int z) => (z * ny + y) * nx + x;
      void Mark(V3 p) { int x = Math.Clamp((int)((p.x - mn.x) / s), 0, nx - 1), y = Math.Clamp((int)((p.y - mn.y) / s), 0, ny - 1), z = Math.Clamp((int)((p.z - mn.z) / s), 0, nz - 1); occ[Id(x, y, z)] = true; }
      for (int i = 0; i + 2 < t.Length; i += 3) {
        var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
        double l = Math.Max((b - a).Len(), Math.Max((c - a).Len(), (c - b).Len())); int k = Math.Max(1, (int)Math.Ceiling(l / (s * 0.5)));
        for (int u = 0; u <= k; u++) for (int w = 0; w <= k - u; w++) { double fu = (double)u / k, fw = (double)w / k; Mark(a + (b - a) * fu + (c - a) * fw); }
      }
      var used = new bool[occ.Length]; var boxes = new List<(V3, V3)>(); bool over = false;
      for (int z = 0; z < nz && !over; z++) for (int y = 0; y < ny && !over; y++) for (int x = 0; x < nx && !over; x++) {
        if (!occ[Id(x, y, z)] || used[Id(x, y, z)]) continue;
        int x1 = x; while (x1 + 1 < nx && occ[Id(x1 + 1, y, z)] && !used[Id(x1 + 1, y, z)]) x1++;
        int z1 = z; bool ok = true; while (ok && z1 + 1 < nz) { for (int xx = x; xx <= x1; xx++) if (!occ[Id(xx, y, z1 + 1)] || used[Id(xx, y, z1 + 1)]) { ok = false; break; } if (ok) z1++; }
        int y1 = y; ok = true; while (ok && y1 + 1 < ny) { for (int zz = z; zz <= z1 && ok; zz++) for (int xx = x; xx <= x1; xx++) if (!occ[Id(xx, y1 + 1, zz)] || used[Id(xx, y1 + 1, zz)]) { ok = false; break; } if (ok) y1++; }
        for (int zz = z; zz <= z1; zz++) for (int yy = y; yy <= y1; yy++) for (int xx = x; xx <= x1; xx++) used[Id(xx, yy, zz)] = true;
        var lo = new V3(mn.x + x * s, mn.y + y * s, mn.z + z * s); var hi = new V3(Math.Min(mx.x, mn.x + (x1 + 1) * s), Math.Min(mx.y, mn.y + (y1 + 1) * s), Math.Min(mx.z, mn.z + (z1 + 1) * s));
        var size = hi - lo; size = new V3(Math.Max(size.x, 0.02), Math.Max(size.y, 0.02), Math.Max(size.z, 0.02));
        boxes.Add(((lo + hi) * 0.5, size)); if (boxes.Count > maxBoxes) over = true;
      }
      if (!over) return (boxes, s);
    }
    return (null, s);
  }
}
