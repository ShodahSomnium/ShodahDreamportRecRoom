// rcdress rends <work> <levelN>: every renderer of the level - enabled, active, LOD1+, mesh name, whether the PC install
// has a mesh of that name, materials, world pose (the renderer's GameObject) and the scene path.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

static class RendsCmd {
  public static void Run(string work, string level) {
    var L = new Ld(work); var m = Model.Load(L, level);
    var inst = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in File.ReadLines(Path.Combine(work, "inst_meshes.tsv"))) { var p = line.Split('\t'); if (p.Length > 2) inst.Add(p[2]); }
    Console.WriteLine("# en\tactive\tlod1\tinst\tmesh\tverts\tmats\tpos\trot\tscale\tpath");
    foreach (var r in m.Rend) {
      if (r.Mesh == null) continue; var w = m.GoW(r.Go);
      Console.WriteLine($"{(r.En ? 1 : 0)}\t{(m.Active(r.Go) ? 1 : 0)}\t{(r.Lod1 ? 1 : 0)}\t{(inst.Contains(r.Mesh.Name) ? 1 : 0)}\t{r.Mesh.Name}\t{r.Mesh.VCount}\t{string.Join(",", r.Mats.Select(x => x.name))}\t{w.p}\t{w.r}\t{w.s}\t{m.PathOf(r.Go)}");
    }
  }
}
