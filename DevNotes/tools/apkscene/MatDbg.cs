// apkscene matdesc debugging: a level mesh's vertex channels and colour values (numbers only, printed).
using System;
using System.Linq;
using System.Collections.Generic;
static class VcDbg {
  public static void Run(string work, string level, string mesh) {
    var L = new Ld(work); var m = Model.Load(L, level);
    var r = m.Rend.FirstOrDefault(x => x.Mesh != null && x.Mesh.Name == mesh); if (r == null) { Console.WriteLine("no renderer"); return; }
    var f = L.File(r.Mesh.File); var d = L.Read(f, f.ById[r.Mesh.Pid]); var vd = d.Dct("m_VertexData");
    Console.WriteLine("vertices " + vd.Num("m_VertexCount") + " data " + ((vd.G("m_DataSize") as byte[])?.Length ?? -1) + " stream " + Tools.Js(d.G("m_StreamData"), 2, 0).Replace("\n", " "));
    int i = 0; foreach (Dictionary<string, object> c in vd.Lst("m_Channels")) Console.WriteLine($"  ch {i++}: stream {c.Num("stream")} offset {c.Num("offset")} format {c.Num("format")} dim {c.Num("dimension")}");
    var data = vd.G("m_DataSize") as byte[]; if (data == null || data.Length == 0) return;
    int n = (int)vd.Num("m_VertexCount"); Console.WriteLine("bytes per vertex " + (double)data.Length / n);
  }
}
static class LayerDbg {
  public static void Run(string work, string level) {
    var L = new Ld(work); var m = Model.Load(L, level); var f = m.F;
    foreach (var o in f.Objects.Where(x => x.ClassId == 218)) {
      var td = Model.FindPtr(L, f, o, 156); if (td.o == null) continue;
      var B = td.f.Data; int s = td.f.DataOff + (int)td.o.Start; int p0 = 4 + ((BitConverter.ToInt32(B, s) + 3) & ~3); int nl = BitConverter.ToInt32(B, s + p0);
      Console.WriteLine("terrain data " + System.Text.Encoding.UTF8.GetString(B, s + 4, BitConverter.ToInt32(B, s)) + " layers " + nl);
      for (int i = 0; i < nl; i++) {
        var lr = L.Res(td.f, (BitConverter.ToInt32(B, s + p0 + 4 + i * 12), BitConverter.ToInt64(B, s + p0 + 8 + i * 12))); if (lr.o == null) continue;
        var LB = lr.f.Data; int ls = lr.f.DataOff + (int)lr.o.Start; int nn = BitConverter.ToInt32(LB, ls); int q = 4 + ((nn + 3) & ~3);
        Console.WriteLine($"  layer {System.Text.Encoding.UTF8.GetString(LB, ls + 4, nn)} size {lr.o.Size}");
        for (int k = 0; k < 3; k++) { int fid = BitConverter.ToInt32(LB, ls + q + k * 12); long pid = BitConverter.ToInt64(LB, ls + q + 4 + k * 12); var tr = L.Res(lr.f, (fid, pid)); string tn = tr.o == null ? "null" : MatDescDbgName(tr.f, tr.o); Console.WriteLine($"    pptr{k} {fid}:{pid} -> {tn} class {tr.o?.ClassId}"); if (tr.o != null && tr.o.ClassId == 28) { var ts = TexStat.Of(L, tr.f, tr.o); Console.WriteLine($"      {ts.W}x{ts.H} fmt {ts.Fmt} err {ts.Err} mean {(ts.Ok ? string.Join(",", ts.Mean(0, false, new double[] { 1, 1, 1 }, false).Select(Y.F)) : "-")} meanA {Y.F(ts.MeanA())}"); } }
        var fl = new List<string>(); for (int k = q + 36; k + 4 <= lr.o.Size; k += 4) fl.Add(Y.F(BitConverter.ToSingle(LB, ls + k)));
        Console.WriteLine("    floats " + string.Join(" ", fl));
      }
    }
  }
  static string MatDescDbgName(AF f, AObj o) { int s = f.DataOff + (int)o.Start; int n = BitConverter.ToInt32(f.Data, s); return n > 0 && n < 512 ? System.Text.Encoding.UTF8.GetString(f.Data, s + 4, n) : "?"; }
}
static class VcHist {
  public static void Run(string work, string level, string mesh) {
    var L = new Ld(work); var m = Model.Load(L, level);
    var r = m.Rend.FirstOrDefault(x => x.Mesh != null && x.Mesh.Name == mesh); if (r == null) { Console.WriteLine("no renderer"); return; }
    var f = L.File(r.Mesh.File); var d = L.Read(f, f.ById[r.Mesh.Pid]); var vd = d.Dct("m_VertexData"); var data = vd.G("m_DataSize") as byte[]; int n = (int)vd.Num("m_VertexCount"); int stride = data.Length / n;
    var h = new Dictionary<string, int>(); for (int i = 0; i < n; i++) { int b = i * stride + 40; var k = $"{data[b]},{data[b + 1]},{data[b + 2]},{data[b + 3]}"; h[k] = h.GetValueOrDefault(k) + 1; }
    foreach (var kv in h.OrderByDescending(x => x.Value).Take(12)) Console.WriteLine($"  rgba {kv.Key}: {kv.Value}");
  }
}
