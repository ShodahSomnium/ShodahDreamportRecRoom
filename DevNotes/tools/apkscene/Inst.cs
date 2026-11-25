// apkscene: one pass over the PC install's plain bundles -> type-tree bank (bank.bin) + install mesh index
// (inst_meshes.tsv: bundle, pathId, name, vertexCount, submeshes "indexCount:vertexCount;...", aabb cx cy cz ex ey ez)
// + install material names (inst_materials.tsv).
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

static class Inst {
  public static void BuildBank(string work) {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    using var mw = new StreamWriter(Path.Combine(work, "inst_meshes.tsv"));
    using var tw = new StreamWriter(Path.Combine(work, "inst_materials.tsv"));
    int meshes = 0, mats = 0, bad = 0;
    var bank = Bank.Harvest(Paths.BundleDir, Path.Combine(Paths.Idx, "mbscripts.tsv"), (bn, f) => {
      foreach (var o in f.Objects) {
        if (o.ClassId != 43 && o.ClassId != 21) continue;
        var t = f.TypeOf(o); if (t.Tree == null) continue;
        var d = TR.Read(f, o, t.Tree, out bool ok, out _); if (!ok) { bad++; continue; }
        if (o.ClassId == 21) {
          var col = MatColor(d);
          tw.WriteLine($"{bn}\t{o.PathId}\t{d.Str("m_Name")}\t{(col == null ? "" : string.Join(",", col.Select(Y.F)))}\t{Shader(d)}"); mats++; continue;
        }
        var subs = d.Lst("m_SubMeshes") ?? new();
        var ss = string.Join(";", subs.Select(x => { var s = (Dictionary<string, object>)x; return $"{(long)s.Num("indexCount")}:{(long)s.Num("vertexCount")}"; }));
        var vd = d.Dct("m_VertexData"); var bb = d.Dct("m_LocalAABB");
        var c = bb.Vec("m_Center"); var e = bb.Vec("m_Extent");
        mw.WriteLine($"{bn}\t{o.PathId}\t{d.Str("m_Name")}\t{(long)vd.Num("m_VertexCount")}\t{ss}\t{c} {e}"); meshes++;
      }
    });
    bank.Save(Path.Combine(work, "bank.bin"));
    Console.WriteLine($"bank: {bank.Files} serialized files, {bank.ByHash.Count} trees ({bank.ByClass.Count} classes, {bank.ByScript.Count} scripts, {bank.ScriptName.Count} script names); meshes {meshes}, materials {mats}, unreadable {bad}; {sw.Elapsed.TotalSeconds:F0} s");
  }
  public static string Shader(Dictionary<string, object> d) => ""; // shader name needs a cross-file lookup; not needed
  /// A material's main colour: _BaseColor, else _Color, else _MainColor (URP / built-in / Rec Room shaders).
  public static double[] MatColor(Dictionary<string, object> d) {
    var cols = d.Dct("m_SavedProperties")?.Lst("m_Colors"); if (cols == null) return null;
    double[] Find(string k) { foreach (var x in cols) { var p = x as Dictionary<string, object>; if (p == null) continue; if ((p.G("first") as string) == k) { var v = p.Dct("second"); return new[] { v.Num("r"), v.Num("g"), v.Num("b"), v.Num("a", 1) }; } } return null; }
    return Find("_BaseColor") ?? Find("_Color") ?? Find("_MainColor") ?? Find("_TintColor");
  }
}
