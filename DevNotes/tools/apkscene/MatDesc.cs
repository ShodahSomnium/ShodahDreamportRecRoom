// apkscene matdesc: material descriptions for a layout's unknown ('?name') materials, from the Android level (data only).
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

static class ShaderDbg {
  public static void Run(string work, string level, string nameRe) {
    var L = new Ld(work); var m = Model.Load(L, level); var f = m.F;
    var re = new Regex(nameRe, RegexOptions.IgnoreCase); var seen = new HashSet<(string, long)>();
    foreach (var o in f.Objects.Where(x => x.ClassId == 23 || x.ClassId == 137)) {
      var d = L.Read(f, o); if (d == null) continue;
      foreach (var mp in d.Lst("m_Materials") ?? new()) {
        var (mf, mo) = L.Res(f, DX.Ptr(mp)); if (mo == null) continue;
        var md = L.Read(mf, mo); var name = md?.Str("m_Name"); if (name == null || !re.IsMatch(name)) continue;
        var sp = md.Ptr("m_Shader"); var (sf, so) = L.Res(mf, sp); if (so == null || !seen.Add((sf.Name, so.PathId))) continue;
        var sd = L.Read(sf, so);
        Console.WriteLine($"== material {name} shader {sf.Name}#{so.PathId} size {so.Size} keys: {string.Join(",", sd?.Keys ?? Enumerable.Empty<string>())}");
        var pf = sd?.Dct("m_ParsedForm"); if (pf == null) continue;
        Console.WriteLine("  parsedform keys: " + string.Join(",", pf.Keys));
        Console.WriteLine("  name " + pf.Str("m_Name"));
        var subs = pf.Lst("m_SubShaders"); Console.WriteLine("  subshaders " + subs?.Count);
        if (subs == null) continue;
        foreach (Dictionary<string, object> ss in subs.Take(2)) {
          Console.WriteLine("   ss keys: " + string.Join(",", ss.Keys));
          Console.WriteLine("   ss tags: " + Tools.Js(ss.G("m_Tags"), 4, 2));
          foreach (Dictionary<string, object> ps in (ss.Lst("m_Passes") ?? new()).Take(3)) {
            Console.WriteLine("    pass keys: " + string.Join(",", ps.Keys));
            var st = ps.Dct("m_State");
            Console.WriteLine("    state: " + Tools.Js(st, 3, 3).Replace("\n", " ").Substring(0, Math.Min(3000, Tools.Js(st, 3, 3).Replace("\n", " ").Length)));
            var vp = ps.Dct("progVertex");
            Console.WriteLine("    progVertex keys: " + string.Join(",", vp?.Keys ?? Enumerable.Empty<string>()));
          }
        }
        Console.WriteLine("  platforms " + Tools.Js(sd.G("platforms"), 2, 1) + " compressed " + Tools.Js(sd.G("compressedLengths"), 2, 1));
      }
    }
  }
}
static class ShaderBlob {
  /// Decompressed GLES3 (platform 9) segments of a shader; null when the shader has none.
  public static List<byte[]> Gles3(Dictionary<string, object> sd) {
    var plats = sd.G("platforms") as int[]; if (plats == null) { var pl = sd.Lst("platforms"); if (pl != null) plats = pl.Select(x => Convert.ToInt32(x)).ToArray(); }
    if (plats == null) return null; int pi = Array.IndexOf(plats, 9); if (pi < 0) return null;
    var offs = sd.Lst("offsets"); var cl = sd.Lst("compressedLengths"); var dl = sd.Lst("decompressedLengths"); var blob = sd.G("compressedBlob") as byte[];
    if (offs == null || cl == null || dl == null || blob == null) return null;
    int[] A(object o) => o is int[] ia ? ia : o is uint[] ua ? ua.Select(x => (int)x).ToArray() : o is List<object> lo ? lo.Select(x => Convert.ToInt32(x is Dictionary<string, object> dd ? dd.Values.First() : x)).ToArray() : null;
    var o1 = A(offs[pi]); var c1 = A(cl[pi]); var d1 = A(dl[pi]); if (o1 == null || c1 == null || d1 == null) return null;
    var res = new List<byte[]>();
    for (int s = 0; s < o1.Length; s++) { var dst = new byte[d1[s]]; Lz4Dec.Decode(blob, o1[s], c1[s], dst, 0); res.Add(dst); }
    return res;
  }
}
static class ShaderGlsl {
  /// GLSL texts (vertex+fragment in one string, "#ifdef VERTEX ... #ifdef FRAGMENT ...") of a pass's GLES3 variants,
  /// with each variant's keyword names.
  public static List<(string text, string[] kws)> Pass(Dictionary<string, object> sd, Dictionary<string, object> pass) {
    var res = new List<(string, string[])>();
    var segs = ShaderBlob.Gles3(sd); if (segs == null || segs.Count < 1) return res;
    var t = segs[0]; int n = BitConverter.ToInt32(t, 0); if (n <= 0 || 4 + n * 12 > t.Length) return res;
    var kwn = (sd.Dct("m_ParsedForm")?.Lst("m_KeywordNames") ?? new()).Select(x => x?.ToString()).ToArray();
    var vp = pass.Dct("progVertex"); var lists = vp?.Lst("m_PlayerSubPrograms"); if (lists == null) return res;
    var seen = new HashSet<int>();
    foreach (var li in lists) foreach (Dictionary<string, object> sp in (li as List<object>) ?? new()) {
      int ty = (int)sp.Num("m_GpuProgramType"); if (ty < 2 || ty > 5) continue;
      int bi = (int)sp.Num("m_BlobIndex"); if (bi < 0 || bi >= n || !seen.Add(bi)) continue;
      int off = BitConverter.ToInt32(t, 4 + bi * 12), len = BitConverter.ToInt32(t, 8 + bi * 12), seg = BitConverter.ToInt32(t, 12 + bi * 12);
      if (seg < 0 || seg >= segs.Count || off < 0 || off + len > segs[seg].Length) continue;
      var txt = Encoding.UTF8.GetString(segs[seg], off, len); int k = txt.IndexOf("#ifdef VERTEX"); if (k < 0) continue; int z = txt.IndexOf((char)0, k); if (z > k) txt = txt.Substring(0, z);
      var ki = sp.G("m_KeywordIndices") as ushort[] ?? new ushort[0];
      res.Add((txt.Substring(k), ki.Where(i => i < kwn.Length).Select(i => kwn[i]).ToArray()));
    }
    return res;
  }
}
static class GlslDbg {
  public static void Run(string work, string level, string nameRe, string passRe) {
    var L = new Ld(work); var m = Model.Load(L, level); var f = m.F; var re = new Regex(nameRe, RegexOptions.IgnoreCase); var seen = new HashSet<(string, long)>();
    foreach (var o in f.Objects.Where(x => x.ClassId == 23 || x.ClassId == 137)) {
      var d = L.Read(f, o); if (d == null) continue;
      foreach (var mp in d.Lst("m_Materials") ?? new()) {
        var (mf, mo) = L.Res(f, DX.Ptr(mp)); if (mo == null) continue; var md = L.Read(mf, mo); var name = md?.Str("m_Name"); if (name == null || !re.IsMatch(name)) continue;
        var (sf, so) = L.Res(mf, md.Ptr("m_Shader")); if (so == null || !seen.Add((sf.Name, so.PathId))) continue; var sd = L.Read(sf, so);
        var pf = sd.Dct("m_ParsedForm"); Console.WriteLine($"== {name} {pf.Str("m_Name")}");
        foreach (Dictionary<string, object> ss in pf.Lst("m_SubShaders")) foreach (Dictionary<string, object> ps in ss.Lst("m_Passes")) {
          var pn = ps.Dct("m_State")?.Str("m_Name") ?? ""; var progs = ShaderGlsl.Pass(sd, ps);
          Console.WriteLine($"  pass {pn}: {progs.Count} gles3 variants");
          if (!Regex.IsMatch(pn, passRe)) continue;
          foreach (var (txt, kws) in progs.Take(1)) {
            Console.WriteLine("   kws " + string.Join(" ", kws));
            int fi = txt.IndexOf("#ifdef FRAGMENT"); var vs = fi > 0 ? txt.Substring(0, fi) : txt; var fs = fi > 0 ? txt.Substring(fi) : "";
            foreach (var line in vs.Split('\n').Where(l => l.Contains("COLOR") || l.StartsWith("in ") || l.StartsWith("out "))) Console.WriteLine("   V| " + line);
            var vcv = Regex.Matches(vs, @"(vs_\w+)(\.\w+)?\s*=[^;]*in_COLOR0").Select(x => x.Groups[1].Value).Distinct().ToList(); Console.WriteLine("   colour varyings: " + string.Join(",", vcv));
            foreach (var line in fs.Split((char)10).Where(l => l.Contains("COLOR") || vcv.Any(v => l.Contains(v)) || l.Contains("SV_Target0 ="))) Console.WriteLine("   F| " + line);
          }
        }
      }
    }
  }
}
