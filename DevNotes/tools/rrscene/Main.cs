// rrscene entry point. Commands (see README.md):
//   rrscene inst  <work>                      scan install bundles -> work/inst_renderers.tsv, work/inst_materials.tsv
//   rrscene scan  <work> [filter]             pass 1 over all reference scenes -> work/matvotes.tsv, work/scriptlabels.tsv
//   rrscene write <work> <outDir> [filter]    pass 2: <outDir>/<scene>.rrsk + .md, _scripts.tsv, INDEX.md
//   rrscene paintball <tsvDir> <exDir> <outDir> [spawnable.tsv]   paintball gameplay layers from the decoded room exports (REF1)
static class Program {
  static int Main(string[] a) {
    Console.OutputEncoding = Encoding.UTF8;
    if (a.Length == 0) { Console.Error.WriteLine("usage: rrscene inst|scan|write|paintball ..."); return 1; }
    switch (a[0]) {
      case "inst": Inst.Scan(a[1]); break;
      case "instfx": InstFx.Scan(a[1]); break;
      case "psdump": PsDump.Run(a[1], a[2], a[3]); break;
      case "scan": Emit.Scan(a[1], a.Length > 2 ? a[2] : null); break;
      case "write": Emit.Write(a[1], a[2], a.Length > 3 ? a[3] : null); break;
      case "paintball": Paintball.Run(a[1], a[2], a[3], a.Length > 4 ? a[4] : Path.Combine(AppContext.BaseDirectory, "..", "..", "roomexport", "spawnable_typeids_from_bundles.tsv")); break;
      default: Console.Error.WriteLine("unknown command " + a[0]); return 1;
    }
    return 0;
  }
}
