using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
static class Seg {
  // writes each decompressed segment to <outDir>/seg<p>_<s>.bin
  public static void Dump(Dictionary<string, object> d, string outDir) {
    var blob = d["compressedBlob"] as byte[]; var plats = (List<object>)d["platforms"];
    var offs = (List<object>)d["offsets"]; var cl = (List<object>)d["compressedLengths"]; var dl = (List<object>)d["decompressedLengths"];
    for (int p = 0; p < plats.Count; p++) {
      var po = Shdis.Flat(offs[p]); var pc = Shdis.Flat(cl[p]); var pd = Shdis.Flat(dl[p]);
      for (int s = 0; s < po.Count; s++) { var dec = new byte[pd[s]]; Lz4Dec.Decode(blob, (int)po[s], (int)pc[s], dec); File.WriteAllBytes(Path.Combine(outDir, $"seg{plats[p]}_{s}.bin"), dec); }
    }
  }
  // strings (>= 4 printable chars) in [from, to) of a file
  public static void Strings(string file, long from, long to) {
    var b = File.ReadAllBytes(file); to = Math.Min(to, b.Length); var sb = new StringBuilder(); long start = from;
    for (long i = from; i < to; i++) { byte c = b[i]; if (c >= 32 && c < 127) { if (sb.Length == 0) start = i; sb.Append((char)c); } else { if (sb.Length >= 4) Console.WriteLine($"{start:X}: {sb}"); sb.Clear(); } }
  }
}
static class Names {
  public static void Run(Dictionary<string, object> d) {
    var pf = (Dictionary<string, object>)d["m_ParsedForm"];
    var ni = new List<object>(); foreach (var ss in (List<object>)pf["m_SubShaders"]) foreach (var ps in (List<object>)((Dictionary<string, object>)ss)["m_Passes"]) ni.Add(((Dictionary<string, object>)ps)["m_NameIndices"]);
    void W(object x, int depth) {
      if (x is List<object> l) { foreach (var y in l) W(y, depth + 1); }
      else if (x is Dictionary<string, object> dd) {
        if (dd.TryGetValue("first", out var k) && k is string s && dd.TryGetValue("second", out var v) && !(v is Dictionary<string, object>) && !(v is List<object>)) Console.WriteLine(v + "\t" + s);
        else foreach (var vv in dd.Values) W(vv, depth + 1);
      }
    }
    W(ni, 0);
  }
}
static class Has {
  public static void Run(Dictionary<string, object> d, string[] needles) {
    var blob = d["compressedBlob"] as byte[]; var plats = (List<object>)d["platforms"];
    var offs = (List<object>)d["offsets"]; var cl = (List<object>)d["compressedLengths"]; var dl = (List<object>)d["decompressedLengths"];
    var counts = new int[needles.Length]; int dxbc = 0;
    var nb = needles.Select(n => Encoding.ASCII.GetBytes(n)).ToArray();
    for (int p = 0; p < plats.Count; p++) {
      var po = Shdis.Flat(offs[p]); var pc = Shdis.Flat(cl[p]); var pd = Shdis.Flat(dl[p]);
      for (int s = 0; s < po.Count; s++) {
        var dec = new byte[pd[s]]; Lz4Dec.Decode(blob, (int)po[s], (int)pc[s], dec);
        for (int i = 0; i + 4 < dec.Length; i++) {
          if (dec[i] == 'D' && dec[i + 1] == 'X' && dec[i + 2] == 'B' && dec[i + 3] == 'C') dxbc++;
          for (int k = 0; k < nb.Length; k++) { var n = nb[k]; if (i + n.Length > dec.Length || dec[i] != n[0]) continue; int j = 1; while (j < n.Length && dec[i + j] == n[j]) j++; if (j == n.Length) counts[k]++; }
        }
      }
    }
    Console.WriteLine("programs " + dxbc + " " + string.Join(" ", needles.Select((n, k) => n + "=" + counts[k])));
  }
}
