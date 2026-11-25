// apkscene: reads the user's Android build (DATA ONLY: never installed, executed or loaded) and writes derived RRSK
// placement text. See README.md.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

static class Paths {
  public const string Install = @"E:\SteamLibrary\steamapps\common\RecRoom";
  public static string BundleDir => Install + @"\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64";
  public static string Idx = Environment.GetEnvironmentVariable("RRTOOL_IDX") ?? @"C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad\rrtool\idx";
  public static string Apk = Environment.GetEnvironmentVariable("APK_DIR") ?? @"C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\1c0947a6-bfa3-41e9-ae24-3394d8a31490\scratchpad\apk";
  public static string DataApk => Path.Combine(Apk, "UnityDataAssetPack.apk");
}

static class Program {
  static int Main(string[] a) {
    System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    if (a.Length == 0) { Console.WriteLine("apkscene bank <work> | info <work> <file> | scan <work> | convert <work> <outDir> <levelN> <SceneName> | dump <work> <file> <pathId>"); return 1; }
    Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll"));
    var work = a[1]; Directory.CreateDirectory(work);
    switch (a[0]) {
      case "bank": Inst.BuildBank(work); break;
      case "info": Tools.Info(work, a[2]); break;
      case "dump": Tools.Dump(work, a[2], long.Parse(a[3]), a.Length > 4 ? int.Parse(a[4]) : 6); break;
      case "scan": Scan.Run(work, a[2]); break;
      case "stats": Conv.Stats(work, a[2]); break;
      case "meshdbg": MeshDbg.Run(work, a[2], a[3]); break;
      case "hcheck": HCheck.Run(work, a[2]); break;
      case "find": Find.Run(work, a[2], a[3], a.Length > 4 ? a[4] : "."); break;
      case "convert": Conv.Run(work, a[2], a[3], a[4], a.Skip(5).ToArray()); break;
      case "envprobe": Env.Probe(work, a[2]); break;
      case "env": Env.Run(work, a[2], a.Skip(3).ToArray()); break;
      case "envwords": EnvDbg.Words(work, a[2]); break;
      case "envtex": EnvTex.Show(work, a[2]); break;
      case "cubeavg": CubeAvg.Run(work, a.Skip(2).ToArray()); break;
      case "envcs": EnvCs.Run(work, a[2], a[3], a.Skip(4).ToArray()); break;
      case "skybands": SkyBands.Test(work, a[2], a.Length > 3 ? int.Parse(a[3]) : 3); break;
      case "probes": ProbeDbg.Run(work, a[2]); break;
      case "tree": TreeCmd.Run(work, a[2], a[3], a.Length > 4 ? int.Parse(a[4]) : 3); break;
      case "mat": TreeCmd.Mat(work, a[2], a[3]); break;
      case "scripts": ScriptsCmd.Run(work, a[2], a.Length > 3 ? a[3] : null); break;
      case "shaderdbg": ShaderDbg.Run(work, a[2], a[3]); break;
      case "glsl": GlslDbg.Run(work, a[2], a[3], a.Length > 4 ? a[4] : "Forward"); break;
      case "matdesc": MatDesc.Run(work, a[2], a[3], a.Skip(4).ToArray()); break;
      case "matcheck": return MatDesc.Check(a[2], a[3]);
      case "matreport": MatDesc.Report(a[2], a[3]); break;
      case "vcdbg": VcDbg.Run(work, a[2], a[3]); break;
      case "layerdbg": LayerDbg.Run(work, a[2]); break;
      case "vchist": VcHist.Run(work, a[2], a[3]); break;
      default: Console.WriteLine("unknown command"); return 1;
    }
    return 0;
  }
}
