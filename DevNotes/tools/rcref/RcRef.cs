// rcref: a REFERENCE software renderer for an Android level (made for the built-in Rec Center, level7), LOCAL ONLY.
// The user's Android build is read in place as DATA ONLY by ../apkscene's reader (compiled in by path, unchanged).
// The images it writes are comparison references for DevNotes; they are never shipped and never go into the world's
// Assets. Nothing of the level (textures, UVs, vertex buffers) is written anywhere except as rendered pixels of these
// local reference PNGs.
//
// What is drawn: every active, enabled, LOD0 MeshRenderer / SkinnedMeshRenderer of the level (level-only shell meshes
// and install-matched props alike) with its real geometry (all submeshes, world transforms), the material's main
// texture (ASTC / ETC / uncompressed, mip <= texmax) sampled with the mesh's UV0 x tiling + offset, times the material
// colour and the mesh vertex colours as Rec Room's AG shaders do (albedo = tex.rgb * lerp(1, vc*tint, tex.a) where the
// shader has that alpha-masked tint, else tex.rgb * vc * tint), alpha test / fade / additive from apkscene matdesc's
// material analysis (MatDesc.Describe, the GLES3 GLSL evidence), the terrain (height grid, splat-blended layers).
// Lighting (linear space): RenderSettings trilight ambient + the directional KeyLight (Lambert) with a sun shadow map
// (3x3 PCF, strength from the light) + URP point/spot lights (URP distance attenuation; Rec Room's URPLight hemisphere
// lights as a cone around the light's forward), emission, linear fog, the level's skybox cubemap as the background.
//
// Commands:
//   poses <scenario.json> <tiles.txt> <summary.txt> <out poses.json> [extra=<extra poses.json>]
//         the scenario's 'shot' steps (frame room = level coordinates; 'aim' shots from the run's summary.txt),
//         the 'views' tiles (tiles.txt camera + yaw, 4.6 deg down as RrTourSteps.EyeView), plus extra poses
//   render <work> <levelN> <poses.json> <outDir> [only=<name regex>] [texmax=512] [shadow=4096] [sky=1] [exp=1]
//   list <work> <levelN>      the renderers and materials as drawn (debugging)
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

static class Paths {
  public const string Install = @"E:\SteamLibrary\steamapps\common\RecRoom";
  public static string BundleDir => Install + @"\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64";
  public static string Idx = Environment.GetEnvironmentVariable("RRTOOL_IDX") ?? @"D:\Dreamport\Rec Room\Rec Room\DevNotes\rrtoolidx";
  public static string Apk = Environment.GetEnvironmentVariable("APK_DIR") ?? @"D:\Dreamport\Rec Room\Rec Room\DevNotes\apkwork\apk";
  public static string DataApk => Path.Combine(Apk, "UnityDataAssetPack.apk");
}

static class Program {
  static int Main(string[] a) {
    System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
    if (a.Length < 2) { Console.WriteLine("rcref poses <scenario.json> <tiles.txt> <summary.txt> <out.json> [extra=<json>] | render <work> <levelN> <poses.json> <outDir> [only=] [texmax=512] [shadow=4096] | list <work> <levelN>"); return 1; }
    switch (a[0]) {
      case "poses": PoseCmd.Run(a[1], a[2], a[3], a[4], a.Skip(5).ToArray()); break;
      case "render": Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll")); RefRender.Run(a[1], a[2], a[3], a[4], a.Skip(5).ToArray()); break;
      case "list": Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll")); RefRender.List(a[1], a[2]); break;
      case "nodes": Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll")); NodesCmd.Run(a[1], a[2], a.Length > 3 ? a[3] : "."); break;
      case "psdump": Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll")); PsDump.Run(a[1]); break;
      case "glsl": Common.Load(Path.Combine(Paths.Install, "UnityPlayer.dll")); GlslFull.Run(a[1], a[2], a[3], a[4], a.Length > 5 ? a[5] : null); break;
      default: Console.WriteLine("unknown command"); return 1;
    }
    return 0;
  }
}

// ------------------------------------------------------------------------------------------------ poses
sealed class Pose { public string Name, Src; public double[] Pos, Look; public double Fov = 60; public int W = 1280, H = 720; public string Sheet; public int Tile; public double YMax = 1e9, Grid; }

static class PoseCmd {
  static double[] Arr(string s) => s.Split(',').Select(x => double.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
  static string F(double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);
  public static string Json(List<Pose> ps) {
    var sb = new StringBuilder("{\n  \"note\": \"rcref camera poses (level coordinates = the Rec Center room frame). fov = vertical degrees, Unity-style.\",\n  \"poses\": [\n");
    for (int i = 0; i < ps.Count; i++) {
      var p = ps[i];
      sb.Append($"    {{\"name\": \"{p.Name}\", \"pos\": [{string.Join(", ", p.Pos.Select(F))}], \"look\": [{string.Join(", ", p.Look.Select(F))}], \"fov\": {F(p.Fov)}, \"w\": {p.W}, \"h\": {p.H}");
      if (p.Sheet != null) sb.Append($", \"sheet\": \"{p.Sheet}\", \"tile\": {p.Tile}");
      sb.Append($", \"src\": \"{(p.Src ?? "").Replace("\"", "'")}\"}}{(i + 1 < ps.Count ? "," : "")}\n");
    }
    return sb.Append("  ]\n}\n").ToString();
  }
  public static List<Pose> Read(string path) {
    var res = new List<Pose>(); var txt = File.ReadAllText(path);
    foreach (Match m in Regex.Matches(txt, @"\{[^{}]*""name""[^{}]*\}")) {
      var o = m.Value; string S(string k) { var mm = Regex.Match(o, "\"" + k + "\"\\s*:\\s*\"([^\"]*)\""); return mm.Success ? mm.Groups[1].Value : null; }
      string N(string k) { var mm = Regex.Match(o, "\"" + k + "\"\\s*:\\s*(-?[0-9.eE+-]+)"); return mm.Success ? mm.Groups[1].Value : null; }
      double[] V(string k) { var mm = Regex.Match(o, "\"" + k + "\"\\s*:\\s*\\[([^\\]]*)\\]"); return mm.Success ? Arr(mm.Groups[1].Value) : null; }
      var p = new Pose { Name = S("name"), Pos = V("pos"), Look = V("look"), Src = S("src"), Sheet = S("sheet") };
      if (N("fov") is string fv) p.Fov = double.Parse(fv); if (N("w") is string w) p.W = int.Parse(w); if (N("h") is string h) p.H = int.Parse(h); if (N("tile") is string t) p.Tile = int.Parse(t); if (N("ymax") is string ym) p.YMax = double.Parse(ym); if (N("grid") is string gd) p.Grid = double.Parse(gd);
      if (p.Name != null && p.Pos != null && p.Look != null) res.Add(p);
    }
    return res;
  }
  public static void Run(string scenario, string tiles, string summary, string outPath, string[] opt) {
    var ps = new List<Pose>(); var txt = File.ReadAllText(scenario); var sum = File.Exists(summary) ? File.ReadAllText(summary) : "";
    bool inRc = false; double viewsFov = 80; string viewsName = null;
    foreach (var line in txt.Split('\n')) {
      var l = line.Trim();
      var g = Regex.Match(l, @"""goto""\s*:\s*""([^""]+)"""); if (g.Success) { inRc = g.Groups[1].Value == "RecCenter"; continue; }
      if (!inRc) continue;
      string S(string k) { var mm = Regex.Match(l, "\"" + k + "\"\\s*:\\s*\"([^\"]*)\""); return mm.Success ? mm.Groups[1].Value : null; }
      double N(string k, double d) { var mm = Regex.Match(l, "\"" + k + "\"\\s*:\\s*(-?[0-9.]+)"); return mm.Success ? double.Parse(mm.Groups[1].Value) : d; }
      double[] V(string k) { var mm = Regex.Match(l, "\"" + k + "\"\\s*:\\s*\\[([^\\]]*)\\]"); return mm.Success ? Arr(mm.Groups[1].Value) : null; }
      if (S("views") is string vn) { viewsName = vn; viewsFov = N("fov", 80); continue; }
      if (S("shot") is string sn) {
        var p = new Pose { Name = sn, Fov = N("fov", 60), W = (int)N("w", 1280), H = (int)N("h", 720) };
        if (S("aim") is string aim) {
          // the run computed it (RrTourSteps.Aim): "rim (x, y, z) ... camera (x, y, z)" in summary.txt; look = rim + 0.3 up
          var mm = Regex.Match(sum, @"aimed at hoop[^\n]*?rim \((-?[0-9.]+), (-?[0-9.]+), (-?[0-9.]+)\)[^\n]*camera \((-?[0-9.]+), (-?[0-9.]+), (-?[0-9.]+)\)[^\n]*" + Regex.Escape(sn) + @"\.png");
          if (!mm.Success) { Console.WriteLine($"shot {sn}: aim '{aim}' not found in summary.txt, skipped"); continue; }
          double G(int i) => double.Parse(mm.Groups[i].Value);
          p.Pos = new[] { G(4), G(5), G(6) }; p.Look = new[] { G(1), G(2) + 0.3, G(3) }; p.Src = $"rc_dorm.json shot (aim {aim}, pose from summary.txt: rim + 0.3 up)";
        } else {
          p.Pos = V("pos"); p.Look = V("look"); var fr = S("frame") ?? "world"; p.Src = "rc_dorm.json shot frame=" + fr;
          if (p.Pos == null || p.Look == null) continue;
        }
        ps.Add(p);
      }
    }
    if (viewsName != null && File.Exists(tiles)) {
      foreach (var l in File.ReadLines(tiles)) {
        var mm = Regex.Match(l, @"^tile (\d+) \(([^)]*)\): eye view yaw (-?[0-9.]+),.*camera \((-?[0-9.]+), (-?[0-9.]+), (-?[0-9.]+)\)");
        if (!mm.Success) continue;
        int t = int.Parse(mm.Groups[1].Value); double yaw = double.Parse(mm.Groups[3].Value) * Math.PI / 180;
        var pos = new[] { double.Parse(mm.Groups[4].Value), double.Parse(mm.Groups[5].Value), double.Parse(mm.Groups[6].Value) };
        // RrTourSteps.EyeView: LookRotation(dir * 10 - up * 0.8)
        var look = new[] { pos[0] + Math.Sin(yaw) * 10, pos[1] - 0.8, pos[2] + Math.Cos(yaw) * 10 };
        ps.Add(new Pose { Name = $"{viewsName}_{t}", Pos = pos, Look = look, Fov = viewsFov, W = 1280, H = 720, Sheet = viewsName, Tile = t, Src = $"{Path.GetFileName(tiles)} tile {t} ({mm.Groups[2].Value}), yaw {mm.Groups[3].Value}; camera rounded to 0.1 m by the run" });
      }
    }
    foreach (var o in opt.Where(x => x.StartsWith("extra="))) ps.AddRange(Read(o.Substring(6)));
    File.WriteAllText(outPath, Json(ps));
    Console.WriteLine($"{ps.Count} poses -> {outPath}");
    foreach (var p in ps) Console.WriteLine($"  {p.Name}: pos {string.Join(",", p.Pos)} look {string.Join(",", p.Look)} fov {p.Fov}");
  }
}

// ------------------------------------------------------------------------------------------------ data
sealed class RTex {
  public string Name; public int W, H; public List<byte[]> Mips = new(); public List<(int w, int h)> Size = new();
  public static readonly float[] Lin = Enumerable.Range(0, 256).Select(i => (float)(i / 255.0 <= 0.04045 ? i / 255.0 / 12.92 : Math.Pow((i / 255.0 + 0.055) / 1.055, 2.4))).ToArray();
  public void Build(byte[] px, int w, int h) {
    Mips.Add(px); Size.Add((w, h)); W = w; H = h;
    while (w > 1 || h > 1) {
      int nw = Math.Max(1, w / 2), nh = Math.Max(1, h / 2); var o = new byte[nw * nh * 4]; var p = Mips[^1];
      for (int y = 0; y < nh; y++) for (int x = 0; x < nw; x++) for (int c = 0; c < 4; c++) {
        int x0 = Math.Min(w - 1, x * 2), x1 = Math.Min(w - 1, x * 2 + 1), y0 = Math.Min(h - 1, y * 2), y1 = Math.Min(h - 1, y * 2 + 1);
        o[(y * nw + x) * 4 + c] = (byte)((p[(y0 * w + x0) * 4 + c] + p[(y0 * w + x1) * 4 + c] + p[(y1 * w + x0) * 4 + c] + p[(y1 * w + x1) * 4 + c] + 2) >> 2);
      }
      Mips.Add(o); Size.Add((nw, nh)); w = nw; h = nh;
    }
  }
  /// bilinear, repeat; rgb linear, a 0..1. lod = log2(texels per pixel) at mip 0.
  public void Sample(double u, double v, double lod, out double r, out double g, out double b, out double a) {
    int m = Math.Clamp((int)Math.Floor(lod), 0, Mips.Count - 1); var p = Mips[m]; var (w, h) = Size[m];
    double x = u * w - 0.5, y = v * h - 0.5; double fx = Math.Floor(x), fy = Math.Floor(y); double tx = x - fx, ty = y - fy;
    int x0 = Mod((int)fx, w), y0 = Mod((int)fy, h), x1 = (x0 + 1) % w, y1 = (y0 + 1) % h;
    int i00 = (y0 * w + x0) * 4, i10 = (y0 * w + x1) * 4, i01 = (y1 * w + x0) * 4, i11 = (y1 * w + x1) * 4;
    double w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
    r = Lin[p[i00]] * w00 + Lin[p[i10]] * w10 + Lin[p[i01]] * w01 + Lin[p[i11]] * w11;
    g = Lin[p[i00 + 1]] * w00 + Lin[p[i10 + 1]] * w10 + Lin[p[i01 + 1]] * w01 + Lin[p[i11 + 1]] * w11;
    b = Lin[p[i00 + 2]] * w00 + Lin[p[i10 + 2]] * w10 + Lin[p[i01 + 2]] * w01 + Lin[p[i11 + 2]] * w11;
    a = (p[i00 + 3] * w00 + p[i10 + 3] * w10 + p[i01 + 3] * w01 + p[i11 + 3] * w11) / 255.0;
  }
  public double AlphaAt(double u, double v) { var p = Mips[0]; int x = Mod((int)Math.Floor(u * W), W), y = Mod((int)Math.Floor(v * H), H); return p[(y * W + x) * 4 + 3] / 255.0; }
  static int Mod(int a, int n) { int r = a % n; return r < 0 ? r + n : r; }
}

sealed class RMat {
  public string Name, Shader, Mode = "opaque", TexName = "-", Note = "";
  public RTex Tex; public double[] Tile = { 1, 1 }, Toff = { 0, 0 };
  public double[] Tint = { 1, 1, 1 }; public double TintA = 1; public double[] Flat; // linear
  public bool Vc, Amask, AlphaTest, Unlit, Hide, Glass; public double Cut = 0.5; public int Cull = 2;
  public double[] Emit; public double Glow;
  public double[] Spec = { 0, 0, 0 }; public double Smooth = 0; public bool NoShadow; public int Src = -1, Dst = -1; public bool Lit; // Lit: the AG/URP lit path (SH + main light + vertex lights + env specular)
  // terrain
  public bool Terrain; public List<(RTex t, double[] tile, double[] remap, double[] mean)> Layers; public RTex Control; public double[] TerrOrg, TerrSize;
  public int Uses;
}

static class RefRender {
  // geometry (world space)
  static float[] P, Nn, UV, C; static int NV;
  static int[] I, TM; static float[] TK; static int NT; // TK: uv units per metre (sqrt area ratio)
  static bool[] TCast;
  static List<RMat> Mats = new();
  static double[] AmbSky, AmbEq, AmbGround; static double AmbInt = 1;
  static double[] SunCol; static V3 SunDir; static double SunStrength = 1; static bool HasSun;
  sealed class PL { public V3 p, fwd; public double[] col; public double range; public int type; public double cosIn = -2, cosOut = -2; public double spotIn, spotOut; }
  static List<PL> Lights = new();
  static double[] FogCol; static double FogStart = 1e9, FogEnd = 1e9; static int FogMode; static double FogDens; static bool Fog;
  static byte[][] SkyFaces; static int SkyN; static double SkyExp = 1;
  static double Exposure = 1; static int SS = 2;
  // ambient: irradiance by normal (trilight integrated, table over n.y) or the serialized SH probe (amb=probe)
  static double[][] AmbTab; static float[] Probe; static bool UseProbe;
  // custom reflection cubemap (unity_SpecCube0): mips of 6 faces, RGBM
  static List<byte[][]> EnvMips; static List<int> EnvN;
  // shadow map
  static float[] SM; static int SMN; static V3 SU, SV, SF; static double SU0, SV0, SScale;

  static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
  static double Gam(double c) { c = Math.Clamp(c, 0, 1); return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055; }
  static double[] LinC(double[] c) => new[] { Lin(c[0]), Lin(c[1]), Lin(c[2]) };
  static double Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
  static V3 Cross(V3 a, V3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  static V3 Nrm(V3 a) { var l = a.Len(); return l < 1e-12 ? new V3(0, 1, 0) : a * (1 / l); }

  // -------------------------------------------------------------------------------------------- mesh decode
  sealed class MGeo { public int N; public float[] P, Nm, UV, C; public List<int[]> Subs = new(); }
  static readonly Dictionary<(string, long), MGeo> geoCache = new();
  static int FmtSize(int f) => f switch { 0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 2, 5 => 2, 6 => 1, 7 => 1, 8 => 2, 9 => 2, 10 => 4, 11 => 4, _ => 4 };
  public static int MeshFail;
  static MGeo Decode(Ld L, MeshInfo mi) {
    if (mi == null) return null;
    if (geoCache.TryGetValue((mi.File, mi.Pid), out var gc)) return gc;
    MGeo g = null;
    try {
      var f = L.File(mi.File); var d = L.Read(f, f.ById[mi.Pid]);
      var vd = d.Dct("m_VertexData"); int n = (int)vd.Num("m_VertexCount");
      var chans = vd.Lst("m_Channels").Cast<Dictionary<string, object>>().Select(x => (s: (int)x.Num("stream"), o: (int)x.Num("offset"), fmt: (int)x.Num("format"), dim: (int)x.Num("dimension") & 15)).ToList();
      byte[] data = vd.G("m_DataSize") as byte[];
      if (data == null || data.Length == 0) {
        var sd = d.Dct("m_StreamData"); var path = Ld.Norm(sd?.Str("path")); long off = (long)(sd?.Num("offset") ?? 0); long size = (long)(sd?.Num("size") ?? 0);
        if (size > 0 && path != null && L.D.Has(path)) { var all = L.D.Get(path); data = new byte[size]; Buffer.BlockCopy(all, (int)off, data, 0, (int)size); }
      }
      if (data != null && n > 0 && chans.Count > 0 && chans[0].dim >= 3) {
        int maxS = chans.Where(x => x.dim > 0).Max(x => x.s);
        var sOff = new int[maxS + 1]; var sStride = new int[maxS + 1]; int o = 0;
        for (int s = 0; s <= maxS; s++) { sOff[s] = o; sStride[s] = chans.Where(x => x.s == s && x.dim > 0).Select(x => x.o + FmtSize(x.fmt) * x.dim).DefaultIfEmpty(0).Max(); o += sStride[s] * n; o = (o + 15) & ~15; }
        double Rd(int fm, int at) => fm switch { 0 => BitConverter.ToSingle(data, at), 1 => (double)BitConverter.ToHalf(data, at), 2 => data[at] / 255.0, 3 => Math.Max(-1, (sbyte)data[at] / 127.0), 4 => BitConverter.ToUInt16(data, at) / 65535.0, 5 => Math.Max(-1, BitConverter.ToInt16(data, at) / 32767.0), 6 => data[at], 7 => (sbyte)data[at], 8 => BitConverter.ToUInt16(data, at), 9 => BitConverter.ToInt16(data, at), 10 => BitConverter.ToUInt32(data, at), 11 => BitConverter.ToInt32(data, at), _ => 0 };
        float[] Chan(int ci, int want) {
          if (ci >= chans.Count || chans[ci].dim == 0) return null; var c = chans[ci]; var r = new float[n * want]; int fs = FmtSize(c.fmt);
          for (int i = 0; i < n; i++) { int b = sOff[c.s] + i * sStride[c.s] + c.o; for (int k = 0; k < want; k++) r[i * want + k] = k < c.dim ? (float)Rd(c.fmt, b + k * fs) : (k == 3 ? 1f : 0f); }
          return r;
        }
        g = new MGeo { N = n, P = Chan(0, 3), Nm = Chan(1, 3), C = Chan(3, 4), UV = Chan(4, 2) };
        var ib = d.G("m_IndexBuffer") as byte[]; bool i32 = d.Num("m_IndexFormat") == 1;
        foreach (var sx in d.Lst("m_SubMeshes").Cast<Dictionary<string, object>>()) {
          var tl = new List<int>();
          if (sx.Num("topology") == 0 && ib != null) {
            int fb = (int)sx.Num("firstByte"), ic = (int)sx.Num("indexCount"), bv = (int)sx.Num("baseVertex");
            for (int k = 0; k < ic; k++) { int at = fb + k * (i32 ? 4 : 2); if (at + (i32 ? 4 : 2) > ib.Length) break; tl.Add((i32 ? BitConverter.ToInt32(ib, at) : BitConverter.ToUInt16(ib, at)) + bv); }
          }
          g.Subs.Add(tl.ToArray());
        }
      }
    } catch (Exception e) { g = null; Console.Error.WriteLine("mesh " + mi.Name + ": " + e.Message); }
    if (g == null) MeshFail++;
    geoCache[(mi.File, mi.Pid)] = g; return g;
  }

  // -------------------------------------------------------------------------------------------- textures
  static int MipBytes(int fmt, int w, int h) {
    int bs = TexDec.AstcBlock(fmt); if (bs > 0) return ((w + bs - 1) / bs) * ((h + bs - 1) / bs) * 16;
    return fmt switch { 34 or 45 or 46 => ((w + 3) / 4) * ((h + 3) / 4) * 8, 47 => ((w + 3) / 4) * ((h + 3) / 4) * 16, 4 or 5 or 14 => w * h * 4, 3 => w * h * 3, 1 or 63 => w * h, 62 or 7 or 13 or 2 => w * h * 2, _ => -1 };
  }
  static readonly Dictionary<(string, long), RTex> texCache = new();
  public static int TexMax = 512;
  static RTex LoadTex(Ld L, AF from, (int, long) ptr, out string err) {
    var t = LoadTex0(L, from, ptr, out err);
    if (t == null && err != null && err.Contains("OutOfMemory")) { var (tf, to) = L.Res(from, ptr); texCache.Remove((tf.Name, to.PathId)); GC.Collect(2, GCCollectionMode.Forced, true, true); t = LoadTex0(L, from, ptr, out err); }
    return t;
  }
  static RTex LoadTex0(Ld L, AF from, (int, long) ptr, out string err) {
    err = null; var (tf, to) = L.Res(from, ptr); if (to == null) { err = "no texture"; return null; }
    if (to.ClassId != 28) { err = "class " + to.ClassId; return null; }
    if (texCache.TryGetValue((tf.Name, to.PathId), out var tc)) return tc;
    RTex t = null; string dbg = "";
    try {
      var d = L.Read(tf, to); int w = (int)d.Num("m_Width"), h = (int)d.Num("m_Height"), fmt = (int)d.Num("m_TextureFormat"), mips = (int)d.Num("m_MipCount", 1); dbg = $"{w}x{h} fmt {fmt} mips {mips} size {d.Dct("m_StreamData")?.Num("size")} {d.Dct("m_StreamData")?.Str("path")}";
      var b = TexDec.Bytes(L, d, out var e1);
      if (b == null) err = e1;
      else {
        int off = 0, m = 0, mw = w, mh = h;
        while (m + 1 < mips && Math.Max(mw, mh) > TexMax) { int sz = MipBytes(fmt, mw, mh); if (sz < 0 || off + sz > b.Length) break; off += sz; mw = Math.Max(1, mw / 2); mh = Math.Max(1, mh / 2); m++; }
        int need = MipBytes(fmt, mw, mh); byte[] slice;
        if (need > 0 && off + need <= b.Length) { slice = new byte[need]; Buffer.BlockCopy(b, off, slice, 0, need); } else { slice = b; mw = w; mh = h; }
        var px = TexDec.Rgba(fmt, slice, mw, mh, out var e2);
        if (px == null) err = e2 ?? "decode";
        else { t = new RTex { Name = d.Str("m_Name") }; t.Build(px, mw, mh); }
      }
      L.Drop(tf);
    } catch (Exception e) { err = e.GetType().Name + " (" + dbg + ")"; t = null; }
    texCache[(tf.Name, to.PathId)] = t; return t;
  }

  // -------------------------------------------------------------------------------------------- materials
  static readonly System.Reflection.MethodInfo DescribeM = typeof(MatDesc).GetMethod("Describe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
  static readonly System.Reflection.MethodInfo ShaderM = typeof(MatDesc).GetMethod("Shader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
  static readonly Dictionary<(string, long), int> matIdx = new();
  public static Regex SkipMat;
  static Regex ShadeDbg = System.Environment.GetEnvironmentVariable("RCREF_SHADE") is string sdb ? new Regex(sdb) : null; static int shadeDbgN;
  static (int x, int y) DbgPix = System.Environment.GetEnvironmentVariable("RCREF_PIX") is string dp ? (int.Parse(dp.Split(',')[0]), int.Parse(dp.Split(',')[1])) : (-1, -1);
  static readonly Regex LitRe = new(@"\b(unity_SHAr|_MainLightColor)\b", RegexOptions.Compiled);
  static int MatOf(Ld L, AF from, (int, long) ptr) {
    var (mf, mo) = L.Res(from, ptr); if (mo == null || mo.ClassId != 21) return -1;
    if (matIdx.TryGetValue((mf.Name, mo.PathId), out var ix)) { Mats[ix].Uses++; return ix; }
    var md = L.Read(mf, mo); var name = md?.Str("m_Name") ?? "?";
    var rm = new RMat { Name = name, Uses = 1 };
    try {
      var r = (MDesc)DescribeM.Invoke(null, new object[] { L, mf, mo, name });
      rm.Shader = r.Shader; rm.Mode = r.Mode; rm.Vc = r.Vc; rm.AlphaTest = r.AlphaTest; rm.Cut = r.Cut; rm.Hide = r.Hide;
      rm.Amask = r.AmaskSh; rm.Tile = r.Tile; rm.Toff = r.Toff; rm.Glow = r.Glow; rm.TexName = r.TexName;
      rm.Tint = LinC(r.Col); rm.TintA = r.Col.Length > 3 ? r.Col[3] : 1;
      if (r.Emit != null) rm.Emit = LinC(r.Emit);
      rm.Unlit = Regex.IsMatch(r.Shader ?? "", "Unlit|Skybox|UI/|Sprites/|Particles?/|Text", RegexOptions.IgnoreCase);
      rm.Glass = Regex.IsMatch(name + " " + r.Shader, "glass|window", RegexOptions.IgnoreCase);
      // the texture itself
      if (r.TexSlot != "-" && r.TexSlot != "_Splat*") {
        foreach (var x in md.Dct("m_SavedProperties")?.Lst("m_TexEnvs") ?? new()) {
          var p = (Dictionary<string, object>)x; if ((p.G("first") as string) != r.TexSlot) continue;
          rm.Tex = LoadTex(L, mf, p.Dct("second").Ptr("m_Texture"), out var err); if (rm.Tex == null) rm.Note += "tex " + err + "; ";
        }
      }
      if (rm.Tex == null) {
        // flat: the texture mean (matdesc's avg, sRGB, tint included) else the colour
        rm.Flat = r.Avg != null ? LinC(r.Avg) : (r.Mean != null ? LinC(r.Mean.Zip(r.Col, (a, b) => a * b).ToArray()) : (double[])rm.Tint.Clone());
      }
      // face culling of the forward pass (0 off, 1 front, 2 back)
      var si = (ShInfo)ShaderM.Invoke(null, new object[] { L, mf, md });
      var cu = si?.Pass?.Dct("m_State")?.Dct("culling");
      if (cu != null) {
        string pn = cu.Str("name"); double v = cu.Num("val");
        if (!string.IsNullOrEmpty(pn) && pn != "<noninit>") foreach (var x in md.Dct("m_SavedProperties")?.Lst("m_Floats") ?? new()) { var p = (Dictionary<string, object>)x; if ((p.G("first") as string) == pn) v = p.Num("second"); }
        rm.Cull = (int)Math.Round(v);
      }
      // lit or not, specular, shadows: from the forward pass GLSL when there is one
      rm.Src = r.Src; rm.Dst = r.Dst;
      var kws = (r.Kws ?? "").Split(' ');
      rm.NoShadow = kws.Contains("_RECEIVE_SHADOWS_OFF");
      if (si != null && si.Progs.Count > 0) {
        rm.Lit = si.Progs.Any(p => LitRe.IsMatch(p.fs) || LitRe.IsMatch(p.vs)); // hlslcc renames unused uniforms Xhlslcc_UnusedX<name>
        rm.Unlit = !rm.Lit;
        if (si.Progs.Any(p => Regex.IsMatch(p.fs, @"\b_SpecColor\b"))) {
          foreach (var x in md.Dct("m_SavedProperties")?.Lst("m_Colors") ?? new()) { var p = (Dictionary<string, object>)x; if ((p.G("first") as string) == "_SpecColor") { var c = p.Dct("second"); rm.Spec = LinC(new[] { c.Num("r"), c.Num("g"), c.Num("b") }); } }
          foreach (var x in md.Dct("m_SavedProperties")?.Lst("m_Floats") ?? new()) { var p = (Dictionary<string, object>)x; if ((p.G("first") as string) == "_Smoothness") rm.Smooth = p.Num("second"); }
        }
      } else rm.Lit = !rm.Unlit;
    } catch (Exception e) { rm.Note += "describe failed: " + (e.InnerException?.Message ?? e.Message); rm.Flat = new[] { 0.35, 0.35, 0.37 }; }
    Mats.Add(rm); matIdx[(mf.Name, mo.PathId)] = Mats.Count - 1; return Mats.Count - 1;
  }

  // -------------------------------------------------------------------------------------------- scene build
  static List<float> lp = new(), ln = new(), luv = new(), lc = new(); static List<int> li = new(), ltm = new(); static List<bool> lcast = new(); static List<int> ltr = new(); public static List<string> RendNames = new(); static int[] TR;
  static int AddVert(V3 p, V3 n, double u, double v, double r, double g, double b, double a) {
    lp.Add((float)p.x); lp.Add((float)p.y); lp.Add((float)p.z); ln.Add((float)n.x); ln.Add((float)n.y); ln.Add((float)n.z);
    luv.Add((float)u); luv.Add((float)v); lc.Add((float)r); lc.Add((float)g); lc.Add((float)b); lc.Add((float)a); return lp.Count / 3 - 1;
  }
  sealed class Stat { public int Rend, Drawn, Inactive, Disabled, Lod, NoMesh, BadMesh, Hidden, Tris; public Dictionary<string, int> Modes = new(); }
  static Stat Build(Ld L, Model m, bool verbose) {
    var st = new Stat(); var f = m.F;
    foreach (var r in m.Rend) {
      st.Rend++;
      if (r.Lod1) { st.Lod++; continue; }
      if (!r.En) { st.Disabled++; if (verbose) Console.WriteLine("  disabled " + (r.Mesh?.Name ?? "?") + " @ " + m.PathOf(r.Go)); continue; }
      if (!m.Active(r.Go)) { st.Inactive++; if (verbose) Console.WriteLine("  inactive " + (r.Mesh?.Name ?? "?") + " @ " + m.PathOf(r.Go)); continue; }
      if (r.Mesh == null) { st.NoMesh++; continue; }
      var g = Decode(L, r.Mesh); if (g == null || g.P == null) { st.BadMesh++; if (verbose) Console.WriteLine("  bad mesh " + r.Mesh.Name + " at " + m.PathOf(r.Go)); continue; }
      var rd = L.Read(f, f.ById[r.Id]); var mptr = rd?.Lst("m_Materials") ?? new List<object>();
      bool cast = rd == null || rd.Num("m_CastShadows", 1) != 0;
      var w = m.GoW(r.Go);
      bool mirror = w.s.x * w.s.y * w.s.z < 0;
      int subFirst = 0, subCount = g.Subs.Count;
      if (r.SubCount > 0) { subFirst = r.FirstSub; subCount = r.SubCount; }
      int baseV = -1; bool drew = false;
      for (int k = 0; k < Math.Max(subCount, mptr.Count); k++) {
        int si = Math.Min(subFirst + Math.Min(k, subCount - 1), g.Subs.Count - 1); if (si < 0) continue;
        if (k >= mptr.Count) break; // more submeshes than materials: Unity leaves them undrawn
        int mi = MatOf(L, f, DX.Ptr(mptr[k])); if (mi < 0) continue; var mat = Mats[mi];
        if (mat.Hide || mat.Mode == "none") { st.Hidden++; continue; }
        var tl = g.Subs[si]; if (tl.Length < 3) continue;
        if (baseV < 0) {
          baseV = lp.Count / 3;
          for (int i = 0; i < g.N; i++) {
            var lpv = new V3(g.P[i * 3], g.P[i * 3 + 1], g.P[i * 3 + 2]); var wp = w.p + w.r.Rot(w.s.Mul(lpv));
            V3 wn = new V3(0, 1, 0);
            if (g.Nm != null) { var nl = new V3(g.Nm[i * 3], g.Nm[i * 3 + 1], g.Nm[i * 3 + 2]); wn = Nrm(w.r.Rot(new V3(nl.x / NZ(w.s.x), nl.y / NZ(w.s.y), nl.z / NZ(w.s.z)))); }
            double u = g.UV != null ? g.UV[i * 2] : 0, v = g.UV != null ? g.UV[i * 2 + 1] : 0;
            double cr = 1, cg = 1, cb = 1, ca = 1; if (g.C != null) { cr = g.C[i * 4]; cg = g.C[i * 4 + 1]; cb = g.C[i * 4 + 2]; ca = g.C[i * 4 + 3]; }
            AddVert(wp, wn, u, v, cr, cg, cb, ca);
          }
          if (g.Nm == null) st.Modes["(no normals)"] = st.Modes.GetValueOrDefault("(no normals)") + 1;
        }
        for (int t = 0; t + 2 < tl.Length; t += 3) {
          int a = tl[t], b = tl[t + 1], c = tl[t + 2]; if (a >= g.N || b >= g.N || c >= g.N) continue;
          if (mirror) { li.Add(baseV + a); li.Add(baseV + c); li.Add(baseV + b); } else { li.Add(baseV + a); li.Add(baseV + b); li.Add(baseV + c); }
          ltm.Add(mi); ltr.Add(RendNames.Count); lcast.Add(cast && mat.Mode != "fade" && mat.Mode != "add"); st.Tris++;
        }
        st.Modes[mat.Mode] = st.Modes.GetValueOrDefault(mat.Mode) + 1; drew = true;
        if (System.Environment.GetEnvironmentVariable("RCREF_DBG") is string dbgRe && Regex.IsMatch(mat.Name, dbgRe)) Console.WriteLine($"  dbg {mat.Name} tex1x1 {(mat.Tex == null ? "-" : string.Join(",", mat.Tex.Mips[^1].Take(4)) + " mip8 row0 " + string.Join(" ", Enumerable.Range(0, mat.Tex.Size[Math.Min(6, mat.Tex.Mips.Count - 1)].w).Select(i => mat.Tex.Mips[Math.Min(6, mat.Tex.Mips.Count - 1)][i * 4] + "/" + mat.Tex.Mips[Math.Min(6, mat.Tex.Mips.Count - 1)][i * 4 + 3])) + " col0 " + string.Join(" ", Enumerable.Range(0, mat.Tex.Size[Math.Min(6, mat.Tex.Mips.Count - 1)].h).Select(j => mat.Tex.Mips[Math.Min(6, mat.Tex.Mips.Count - 1)][j * mat.Tex.Size[Math.Min(6, mat.Tex.Mips.Count - 1)].w * 4] + "/" + mat.Tex.Mips[Math.Min(6, mat.Tex.Mips.Count - 1)][j * mat.Tex.Size[Math.Min(6, mat.Tex.Mips.Count - 1)].w * 4 + 3])))} {r.Mesh.Name} at {m.PathOf(r.Go)} pos {w.p} scale {w.s} sub {si} tris {tl.Length / 3} verts {g.N} vc {(g.C == null ? "none" : string.Join(" ", Enumerable.Range(0, Math.Min(6, g.N)).Select(i => $"({g.C[i * 4]:0.##},{g.C[i * 4 + 1]:0.##},{g.C[i * 4 + 2]:0.##},{g.C[i * 4 + 3]:0.##})")))} uv {(g.UV == null ? "none" : $"{g.UV.Where((x, i) => i % 2 == 0).Min():0.##}..{g.UV.Where((x, i) => i % 2 == 0).Max():0.##},{g.UV.Where((x, i) => i % 2 == 1).Min():0.##}..{g.UV.Where((x, i) => i % 2 == 1).Max():0.##}")}");
      }
      if (drew) { st.Drawn++; } RendNames.Add(r.Mesh.Name + " @ " + m.PathOf(r.Go));
    }
    return st;
  }
  static double NZ(double s) => Math.Abs(s) < 1e-6 ? 1e-6 : s;

  /// terrain: the height grid as triangles, a terrain material that blends its layers by the splat map
  static void BuildTerrain(Ld L, Model m) {
    var f = m.F;
    foreach (var tn in m.Terr) {
      if (tn.H == null || tn.Res < 2 || !m.Active(tn.Go)) continue;
      var w = m.GoW(tn.Go); int R = tn.Res; double sx = tn.Scale.x, sz = tn.Scale.z, sy = tn.Scale.y;
      var rm = new RMat { Name = "terrain:" + tn.Name, Shader = "terrain", Terrain = true, Layers = new(), Uses = 1, Cull = 2, TerrOrg = new[] { w.p.x, w.p.z }, TerrSize = new[] { sx * (R - 1), sz * (R - 1) } };
      // TerrainData raw: name, layers (PPtrs), alpha textures (PPtrs)
      foreach (var o in f.Objects.Where(x => x.ClassId == 218)) {
        var td = Model.FindPtr(L, f, o, 156); if (td.o == null) continue; var B = td.f.Data; int s = td.f.DataOff + (int)td.o.Start;
        int nlen = BitConverter.ToInt32(B, s); if (Encoding.UTF8.GetString(B, s + 4, Math.Max(0, Math.Min(nlen, 256))) != tn.Name) continue;
        int p0 = 4 + ((nlen + 3) & ~3); int nl = BitConverter.ToInt32(B, s + p0); if (nl <= 0 || nl > 16) break;
        for (int i = 0; i < nl; i++) {
          var lr = L.Res(td.f, (BitConverter.ToInt32(B, s + p0 + 4 + i * 12), BitConverter.ToInt64(B, s + p0 + 8 + i * 12)));
          RTex tx = null; double[] tile = { 1, 1 }, remap = { 1, 1, 1 };
          if (lr.o != null) {
            var LB = lr.f.Data; int ls = lr.f.DataOff + (int)lr.o.Start; int q0 = 4 + ((BitConverter.ToInt32(LB, ls) + 3) & ~3);
            if (q0 + 12 <= lr.o.Size) tx = LoadTex(L, lr.f, (BitConverter.ToInt32(LB, ls + q0), BitConverter.ToInt64(LB, ls + q0 + 4)), out _);
            if (q0 + 44 <= lr.o.Size) tile = new double[] { BitConverter.ToSingle(LB, ls + q0 + 36), BitConverter.ToSingle(LB, ls + q0 + 40) };
            if (q0 + 112 <= lr.o.Size) { var sc = Enumerable.Range(0, 3).Select(k => (double)(BitConverter.ToSingle(LB, ls + q0 + 96 + k * 4) - BitConverter.ToSingle(LB, ls + q0 + 80 + k * 4))).ToArray(); if (sc.All(v => v >= 0 && v <= 8) && sc.Any(v => v > 0)) remap = sc; }
          }
          if (tile[0] <= 0.01 || tile[1] <= 0.01) tile = new double[] { 2, 2 };
          rm.Layers.Add((tx, tile, remap, null));
        }
        int q = p0 + 4 + nl * 12; int na = BitConverter.ToInt32(B, s + q);
        if (na > 0 && na <= 4) { var ar = L.Res(td.f, (BitConverter.ToInt32(B, s + q + 4), BitConverter.ToInt64(B, s + q + 8))); if (ar.o != null && ar.o.ClassId == 28) { var px = TexStat.Pixels(L, ar.f, ar.o, out int aw, out int ah); if (px != null) { rm.Control = new RTex { Name = "control" }; rm.Control.Mips.Add(px); rm.Control.Size.Add((aw, ah)); rm.Control.W = aw; rm.Control.H = ah; } } }
        break;
      }
      Mats.Add(rm); int mi = Mats.Count - 1; int baseV = lp.Count / 3;
      double Hh(int x, int z) => tn.H[Math.Clamp(z, 0, R - 1) * R + Math.Clamp(x, 0, R - 1)] / 32766.0 * sy;
      for (int z = 0; z < R; z++) for (int x = 0; x < R; x++) {
        var p = new V3(w.p.x + x * sx, w.p.y + Hh(x, z), w.p.z + z * sz);
        var n = Nrm(new V3((Hh(x - 1, z) - Hh(x + 1, z)) / (2 * sx), 1, (Hh(x, z - 1) - Hh(x, z + 1)) / (2 * sz)));
        AddVert(p, n, (double)x / (R - 1), (double)z / (R - 1), 1, 1, 1, 1);
      }
      for (int z = 0; z + 1 < R; z++) for (int x = 0; x + 1 < R; x++) {
        int a = baseV + z * R + x, b = a + 1, c = a + R, d = c + 1;
        li.Add(a); li.Add(c); li.Add(b); ltm.Add(mi); lcast.Add(true);
        li.Add(b); li.Add(c); li.Add(d); ltm.Add(mi); lcast.Add(true);
      }
      Console.WriteLine($"terrain {tn.Name}: {R}x{R} at {w.p}, layers {string.Join(",", rm.Layers.Select(l => (l.t?.Name ?? "?") + "/" + Y.F(l.tile[0])))} control {(rm.Control != null ? rm.Control.W + "x" + rm.Control.H : "none")}");
    }
  }

  static void SetupEnv(Ld L, Model m) {
    var rs = m.RS;
    double[] C4(string k) => rs != null && rs.TryGetValue(k, out var o) ? (double[])o : null;
    AmbSky = LinC(C4("m_AmbientSkyColor") ?? new double[] { 0.5, 0.5, 0.5 }); AmbEq = LinC(C4("m_AmbientEquatorColor") ?? new double[] { 0.4, 0.4, 0.4 }); AmbGround = LinC(C4("m_AmbientGroundColor") ?? new double[] { 0.3, 0.3, 0.3 });
    AmbInt = rs != null && rs.TryGetValue("m_AmbientIntensity", out var ai) ? Convert.ToDouble(ai) : 1;
    if ((int)(rs?.GetValueOrDefault("m_AmbientMode") is int am ? am : 1) != 1) { /* flat / skybox: use the sky colour everywhere */ }
    Fog = rs != null && (bool)rs["m_Fog"]; FogCol = LinC(C4("m_FogColor") ?? new double[] { 1, 1, 1 }); FogMode = Convert.ToInt32(rs?["m_FogMode"] ?? 1);
    FogStart = Convert.ToDouble(rs?["m_LinearFogStart"] ?? 0); FogEnd = Convert.ToDouble(rs?["m_LinearFogEnd"] ?? 1e9); FogDens = Convert.ToDouble(rs?["m_FogDensity"] ?? 0);
    Console.WriteLine($"ambient trilight sky {string.Join(",", AmbSky.Select(Y.F))} eq {string.Join(",", AmbEq.Select(Y.F))} ground {string.Join(",", AmbGround.Select(Y.F))} (linear) x{Y.F(AmbInt)}; fog {Fog} mode {FogMode} {Y.F(FogStart)}-{Y.F(FogEnd)} dens {Y.F(FogDens)}");
    // ambient irradiance: trilight radiance (sky..equator..ground by elevation) cosine-integrated, by n.y (65 steps)
    AmbTab = new double[65][];
    {
      int NS = 96; var dirs = new List<(V3 d, double[] L)>();
      for (int i = 0; i < NS; i++) for (int j = 0; j < 2 * NS; j++) {
        double th = (i + 0.5) / NS * Math.PI, ph = (j + 0.5) / (2 * NS) * 2 * Math.PI; var d = new V3(Math.Sin(th) * Math.Cos(ph), Math.Cos(th), Math.Sin(th) * Math.Sin(ph));
        double t = d.y; var Lr = new double[3]; for (int k = 0; k < 3; k++) Lr[k] = (t > 0 ? AmbEq[k] + (AmbSky[k] - AmbEq[k]) * t : AmbEq[k] + (AmbGround[k] - AmbEq[k]) * -t) * Math.Sin(th);
        dirs.Add((d, Lr));
      }
      double dw = (Math.PI / NS) * (2 * Math.PI / (2 * NS));
      for (int s = 0; s <= 64; s++) {
        double ny = -1 + 2.0 * s / 64; var n = new V3(Math.Sqrt(Math.Max(0, 1 - ny * ny)), ny, 0); var acc = new double[3];
        foreach (var (d, Lr) in dirs) { double c = Dot(n, d); if (c <= 0) continue; for (int k = 0; k < 3; k++) acc[k] += Lr[k] * c * dw; }
        AmbTab[s] = acc.Select(v => v / Math.PI * AmbInt).ToArray();
      }
      Console.WriteLine($"ambient irradiance (trilight integrated, linear): up {string.Join(",", AmbTab[64].Select(Y.F))} side {string.Join(",", AmbTab[32].Select(Y.F))} down {string.Join(",", AmbTab[0].Select(Y.F))}");
    }
    foreach (var o in m.F.Objects.Where(x => x.ClassId == 104)) {
      var raw = Env.RsRaw(m.F, o); Probe = raw["ambientProbe"] as float[];
      if (Probe != null) { var up = ProbeEval(new V3(0, 1, 0)); var dn = ProbeEval(new V3(0, -1, 0)); Console.WriteLine($"serialized ambient probe (amb=probe): up {string.Join(",", up.Select(Y.F))} down {string.Join(",", dn.Select(Y.F))}"); }
      // custom reflection cubemap
      try {
        var (cf, co) = L.Res(m.F, ((int, long))raw["customRefl"]);
        if (co != null && co.ClassId == 89) {
          var d = L.Read(cf, co); var img = d.G("image data") as byte[]; int per = (int)d.Num("m_CompleteImageSize"), fmt = (int)d.Num("m_TextureFormat"), w = (int)d.Num("m_Width"), mips = (int)d.Num("m_MipCount", 1);
          if ((img == null || img.Length == 0)) img = TexDec.Bytes(L, d, out _);
          int bsz = TexDec.AstcBlock(fmt);
          if (img != null && bsz > 0) {
            EnvMips = new(); EnvN = new(); int at = 0, size = w;
            for (int mm = 0; mm < mips; mm++) {
              var faces = new byte[6][]; for (int fc = 0; fc < 6; fc++) faces[fc] = Astc.Image(img, fc * per + at, size, size, bsz, bsz);
              EnvMips.Add(faces); EnvN.Add(size); int bx = (size + bsz - 1) / bsz; at += bx * bx * 16; size = Math.Max(1, size / 2);
            }
            Console.WriteLine($"reflection cubemap {d.Str("m_Name")} {w}x{w}, {mips} mips (format {fmt})");
          } else Console.WriteLine($"reflection cubemap {d.Str("m_Name")}: format {fmt} not decoded");
        }
      } catch (Exception e) { Console.WriteLine("reflection cubemap: " + e.Message); EnvMips = null; }
    }
    // lights: Rec Room's compact additional lights (per vertex in the AG shaders): smoothstep(1 - d^2/r^2) range falloff,
    // smoothstep(spot)^2 cone (hemisphere lights: their inner/outer angles), no inverse-square term
    double best = -1;
    foreach (var l in m.Lights) {
      if (!l.En || !m.Active(l.Go)) continue;
      var w = m.GoW(l.Go); var fwd = Nrm(w.r.Rot(new V3(0, 0, 1))); var col = LinC(l.Col);
      if (l.Type == 1) { if (l.Int > best) { best = l.Int; HasSun = true; SunDir = fwd * -1; SunCol = col.Select(c => c * l.Int).ToArray(); SunStrength = l.Shadows > 0 ? l.Strength : 0; } continue; }
      if (l.Type != 2 && l.Type != 0) continue;
      var pl = new PL { p = w.p, fwd = fwd, col = col.Select(c => c * l.Int).ToArray(), range = l.Range, type = l.Type };
      var ld = L.Read(m.F, m.F.ById[l.Pid]);
      var keys = UrpKeys.Of(m, l.Go, ld);
      var hm = Regex.Match(keys, @"hemi=1 hin=([0-9.]+) hout=([0-9.]+)");
      if (hm.Success) { pl.cosIn = Math.Cos(double.Parse(hm.Groups[1].Value) * Math.PI / 360); pl.cosOut = Math.Cos(double.Parse(hm.Groups[2].Value) * Math.PI / 360); }
      if (l.Type == 0) { double inner = ld?.Num("m_InnerSpotAngle", l.Spot * 0.8) ?? l.Spot * 0.8; pl.spotOut = Math.Cos(l.Spot * Math.PI / 360); pl.spotIn = Math.Cos(Math.Min(inner, l.Spot - 0.01) * Math.PI / 360); }
      Lights.Add(pl);
    }
    Console.WriteLine($"sun {(HasSun ? $"dir-to-sun {SunDir} col {string.Join(",", SunCol.Select(Y.F))} shadow strength {Y.F(SunStrength)}" : "none")}; {Lights.Count} point/spot lights ({Lights.Count(x => x.cosIn > -2)} hemisphere)");
    // sky cubemap
    try {
      SkyFaces = SkyBands.Faces(L, m.Level, 2, out SkyN, out var sname);
      if (SkyFaces != null) {
        var sm = L.Read(m.F, ((int, long))m.RS["sky"]); double ex = 1, tint = 0.5;
        foreach (var x in sm?.Dct("m_SavedProperties")?.Lst("m_Floats") ?? new()) { var p = (Dictionary<string, object>)x; if ((p.G("first") as string) == "_Exposure") ex = p.Num("second"); }
        foreach (var x in sm?.Dct("m_SavedProperties")?.Lst("m_Colors") ?? new()) { var p = (Dictionary<string, object>)x; if ((p.G("first") as string) == "_Tint") tint = p.Dct("second").Num("r"); }
        SkyExp = ex * Lin(tint) * 4.59479; // Skybox/Cubemap: tex * _Tint * unity_ColorSpaceDouble (linear 4.59) * _Exposure
        Console.WriteLine($"sky cubemap {sname} {SkyN}x{SkyN} (mip 2), exposure {Y.F(ex)} tint {Y.F(tint)} -> x{Y.F(SkyExp)}");
      }
    } catch (Exception e) { Console.WriteLine("sky: " + e.Message); SkyFaces = null; }
  }

  static double[] Sky(V3 d) {
    if (SkyFaces == null) { double t = Math.Clamp(d.y, -1, 1); return t > 0 ? new[] { AmbEq[0] + (AmbSky[0] - AmbEq[0]) * t, AmbEq[1] + (AmbSky[1] - AmbEq[1]) * t, AmbEq[2] + (AmbSky[2] - AmbEq[2]) * t } : new[] { AmbEq[0] + (AmbGround[0] - AmbEq[0]) * -t, AmbEq[1] + (AmbGround[1] - AmbEq[1]) * -t, AmbEq[2] + (AmbGround[2] - AmbEq[2]) * -t }; }
    double ax = Math.Abs(d.x), ay = Math.Abs(d.y), az = Math.Abs(d.z); int f; double u, v;
    if (ax >= ay && ax >= az) { if (d.x > 0) { f = 0; u = -d.z / ax; v = -d.y / ax; } else { f = 1; u = d.z / ax; v = -d.y / ax; } }
    else if (ay >= az) { if (d.y > 0) { f = 2; u = d.x / ay; v = d.z / ay; } else { f = 3; u = d.x / ay; v = -d.z / ay; } }
    else { if (d.z > 0) { f = 4; u = d.x / az; v = -d.y / az; } else { f = 5; u = -d.x / az; v = -d.y / az; } }
    int i = Math.Clamp((int)((u + 1) * 0.5 * SkyN), 0, SkyN - 1), j = Math.Clamp((int)((v + 1) * 0.5 * SkyN), 0, SkyN - 1);
    int o = (j * SkyN + i) * 4; var fc = SkyFaces[f]; double mm = fc[o + 3] / 255.0 * 5;
    return new[] { Lin(fc[o] / 255.0 * mm) * SkyExp, Lin(fc[o + 1] / 255.0 * mm) * SkyExp, Lin(fc[o + 2] / 255.0 * mm) * SkyExp };
  }

  /// unity_SHA*/SHB*/SHC from the serialized probe (channel-major sh[c*9+i]) evaluated as the shader does
  static double[] ProbeEval(V3 n) {
    var r = new double[3];
    for (int c = 0; c < 3; c++) {
      double S(int i) => Probe[c * 9 + i];
      double l01 = S(3) * n.x + S(1) * n.y + S(2) * n.z + (S(0) - S(6));
      double l2 = S(4) * (n.x * n.y) + S(5) * (n.y * n.z) + S(6) * 3 * (n.z * n.z) + S(7) * (n.z * n.x) + S(8) * (n.x * n.x - n.y * n.y);
      r[c] = Math.Max(0, l01 + l2);
    }
    return r;
  }
  static double[] Ambient(V3 n) {
    if (UseProbe && Probe != null) return ProbeEval(n);
    double s = (Math.Clamp(n.y, -1, 1) + 1) * 32; int i = Math.Min(63, (int)s); double f = s - i; var a = AmbTab[i]; var b = AmbTab[i + 1];
    return new[] { a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f };
  }
  /// the custom reflection cubemap at a roughness mip (DecodeHDR RGBM as the sky: Lin(rgb * 5a))
  static double[] EnvCube(V3 d, double mip) {
    if (EnvMips == null) return Ambient(d);
    int m = Math.Clamp((int)Math.Round(mip), 0, EnvMips.Count - 1); int N = EnvN[m]; var faces = EnvMips[m];
    double ax = Math.Abs(d.x), ay = Math.Abs(d.y), az = Math.Abs(d.z); int f; double u, v;
    if (ax >= ay && ax >= az) { if (d.x > 0) { f = 0; u = -d.z / ax; v = -d.y / ax; } else { f = 1; u = d.z / ax; v = -d.y / ax; } }
    else if (ay >= az) { if (d.y > 0) { f = 2; u = d.x / ay; v = d.z / ay; } else { f = 3; u = d.x / ay; v = -d.z / ay; } }
    else { if (d.z > 0) { f = 4; u = d.x / az; v = -d.y / az; } else { f = 5; u = -d.x / az; v = -d.y / az; } }
    int i = Math.Clamp((int)((u + 1) * 0.5 * N), 0, N - 1), j = Math.Clamp((int)((v + 1) * 0.5 * N), 0, N - 1);
    int o = (j * N + i) * 4; var fc = faces[f]; double mm = fc[o + 3] / 255.0 * 5;
    return new[] { Lin(fc[o] / 255.0 * mm), Lin(fc[o + 1] / 255.0 * mm), Lin(fc[o + 2] / 255.0 * mm) };
  }

  // -------------------------------------------------------------------------------------------- shadow map
  static void BuildShadow(int size) {
    if (!HasSun || SunStrength <= 0) return;
    SF = SunDir * -1; // light travel direction
    SU = Nrm(Cross(Math.Abs(SF.y) > 0.9 ? new V3(1, 0, 0) : new V3(0, 1, 0), SF)); SV = Cross(SF, SU);
    double u0 = 1e18, u1 = -1e18, v0 = 1e18, v1 = -1e18;
    { var us = new List<double>(); var vs = new List<double>(); for (int t = 0; t < NT; t += 1) { if (!TCast[t] || Mats[TM[t]].Terrain) continue; int vi = I[t * 3]; var p = new V3(P[vi * 3], P[vi * 3 + 1], P[vi * 3 + 2]); us.Add(Dot(p, SU)); vs.Add(Dot(p, SV)); } us.Sort(); vs.Sort(); double Pc(List<double> l, double q) => l.Count == 0 ? 0 : l[Math.Clamp((int)(q * (l.Count - 1)), 0, l.Count - 1)]; u0 = Pc(us, 0.01) - 4; u1 = Pc(us, 0.99) + 4; v0 = Pc(vs, 0.01) - 4; v1 = Pc(vs, 0.99) + 4; } // the level's core (1st-99th percentile of casters, terrain left out) + 4 m
    SMN = size; SU0 = u0 - 0.5; SV0 = v0 - 0.5; SScale = (size - 1) / Math.Max(u1 - u0 + 1, v1 - v0 + 1);
    SM = new float[size * size]; Array.Fill(SM, float.MaxValue);
    for (int t = 0; t < NT; t++) {
      if (!TCast[t]) continue; var mat = Mats[TM[t]];
      double[] X = new double[3], Yv = new double[3], Z = new double[3];
      for (int k = 0; k < 3; k++) { int vi = I[t * 3 + k]; var p = new V3(P[vi * 3], P[vi * 3 + 1], P[vi * 3 + 2]); X[k] = (Dot(p, SU) - SU0) * SScale; Yv[k] = (Dot(p, SV) - SV0) * SScale; Z[k] = Dot(p, SF); }
      int x0 = Math.Max(0, (int)Math.Floor(X.Min())), x1 = Math.Min(size - 1, (int)Math.Ceiling(X.Max())), y0 = Math.Max(0, (int)Math.Floor(Yv.Min())), y1 = Math.Min(size - 1, (int)Math.Ceiling(Yv.Max()));
      double den = (Yv[1] - Yv[2]) * (X[0] - X[2]) + (X[2] - X[1]) * (Yv[0] - Yv[2]); if (Math.Abs(den) < 1e-12) continue;
      bool at = mat.AlphaTest && mat.Tex != null;
      for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) {
        double px = x + 0.5, py = y + 0.5;
        double l1 = ((Yv[1] - Yv[2]) * (px - X[2]) + (X[2] - X[1]) * (py - Yv[2])) / den, l2 = ((Yv[2] - Yv[0]) * (px - X[2]) + (X[0] - X[2]) * (py - Yv[2])) / den, l3 = 1 - l1 - l2;
        if (l1 < 0 || l2 < 0 || l3 < 0) continue;
        double z = l1 * Z[0] + l2 * Z[1] + l3 * Z[2]; int id = y * size + x; if (z >= SM[id]) continue;
        if (at) { var (u, v) = UvAt(t, l1, l2, l3, mat); if (mat.Tex.AlphaAt(u, v) < mat.Cut) continue; }
        SM[id] = (float)z;
      }
    }
  }
  static double ShadowAt(V3 p, V3 n) {
    if (SM == null) return 1;
    var q = p + n * 0.04 + SunDir * 0.02; double u = (Dot(q, SU) - SU0) * SScale, v = (Dot(q, SV) - SV0) * SScale, z = Dot(q, SF);
    int cx = (int)Math.Floor(u), cy = (int)Math.Floor(v); double lit = 0; int cnt = 0;
    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
      int x = cx + dx, y = cy + dy; cnt++; if (x < 0 || y < 0 || x >= SMN || y >= SMN) { lit++; continue; }
      if (z <= SM[y * SMN + x] + 0.03) lit++;
    }
    return lit / cnt;
  }

  static (double u, double v) UvAt(int t, double b0, double b1, double b2, RMat mat) {
    int a = I[t * 3], b = I[t * 3 + 1], c = I[t * 3 + 2];
    double u = UV[a * 2] * b0 + UV[b * 2] * b1 + UV[c * 2] * b2, v = UV[a * 2 + 1] * b0 + UV[b * 2 + 1] * b1 + UV[c * 2 + 1] * b2;
    return (u * mat.Tile[0] + mat.Toff[0], v * mat.Tile[1] + mat.Toff[1]);
  }

  // -------------------------------------------------------------------------------------------- shading
  /// linear rgb of triangle t at barycentrics (b0,b1,b2) seen from eye; alpha out (fade / add)
  static double[] Shade(int t, double b0, double b1, double b2, V3 eye, double pixAngle, out double alpha) {
    var mat = Mats[TM[t]]; int a = I[t * 3], b = I[t * 3 + 1], c = I[t * 3 + 2];
    var wp = new V3(P[a * 3] * b0 + P[b * 3] * b1 + P[c * 3] * b2, P[a * 3 + 1] * b0 + P[b * 3 + 1] * b1 + P[c * 3 + 1] * b2, P[a * 3 + 2] * b0 + P[b * 3 + 2] * b1 + P[c * 3 + 2] * b2);
    var n = Nrm(new V3(Nn[a * 3] * b0 + Nn[b * 3] * b1 + Nn[c * 3] * b2, Nn[a * 3 + 1] * b0 + Nn[b * 3 + 1] * b1 + Nn[c * 3 + 1] * b2, Nn[a * 3 + 2] * b0 + Nn[b * 3 + 2] * b1 + Nn[c * 3 + 2] * b2));
    var toEye = eye - wp; double dist = toEye.Len(); var vdir = toEye * (1 / Math.Max(dist, 1e-9));
    // geometric face: two-sided materials seen from behind light their back
    var pa = new V3(P[a * 3], P[a * 3 + 1], P[a * 3 + 2]); var fn = Cross(new V3(P[b * 3], P[b * 3 + 1], P[b * 3 + 2]) - pa, new V3(P[c * 3], P[c * 3 + 1], P[c * 3 + 2]) - pa);
    if (Dot(fn, toEye) < 0) n = n * -1;
    if (Dot(n, vdir) < 0 && Dot(fn, toEye) >= 0) { /* interpolated normal turned away: keep */ }
    double r, g, bl; alpha = 1; double ta = 1; double er = 0, eg = 0, eb = 0;
    // texel footprint: uv units per metre x texture size x metres per pixel at this distance / angle
    if (mat.Terrain) {
      double fx = (wp.x - mat.TerrOrg[0]) / mat.TerrSize[0], fz = (wp.z - mat.TerrOrg[1]) / mat.TerrSize[1];
      double[] wts = { 1, 0, 0, 0 };
      if (mat.Control != null) { var cp = mat.Control.Mips[0]; int cx = Math.Clamp((int)(fx * mat.Control.W), 0, mat.Control.W - 1), cz = Math.Clamp((int)(fz * mat.Control.H), 0, mat.Control.H - 1); int o = (cz * mat.Control.W + cx) * 4; wts = new[] { cp[o] / 255.0, cp[o + 1] / 255.0, cp[o + 2] / 255.0, cp[o + 3] / 255.0 }; }
      r = g = bl = 0; double ws = 0;
      for (int k = 0; k < Math.Min(4, mat.Layers.Count); k++) {
        if (wts[k] <= 0.001) continue; var ly = mat.Layers[k]; double lr = 0.3, lg = 0.35, lb = 0.2;
        if (ly.t != null) { double lod = Math.Log2(Math.Max(1e-9, ly.t.W / ly.tile[0] * dist * pixAngle / Math.Sqrt(Math.Max(0.05, Math.Abs(Dot(n, vdir)))))); ly.t.Sample(wp.x / ly.tile[0], wp.z / ly.tile[1], lod, out lr, out lg, out lb, out _); }
        r += wts[k] * lr * ly.remap[0]; g += wts[k] * lg * ly.remap[1]; bl += wts[k] * lb * ly.remap[2]; ws += wts[k];
      }
      if (ws > 0) { r /= ws; g /= ws; bl /= ws; }
    } else {
      double tr, tg, tb; ta = 1;
      if (mat.Tex != null) {
        var (u, v) = UvAt(t, b0, b1, b2, mat);
        double lod = Math.Log2(Math.Max(1e-9, TK[t] * Math.Max(mat.Tex.W, mat.Tex.H) * dist * pixAngle / Math.Sqrt(Math.Max(0.05, Math.Abs(Dot(n, vdir))))));
        mat.Tex.Sample(u, v, lod, out tr, out tg, out tb, out ta);
      } else { tr = mat.Flat[0]; tg = mat.Flat[1]; tb = mat.Flat[2]; }
      // k = vertex colour x _BaseColor (the AG shaders' tint)
      double kr = mat.Tint[0], kg = mat.Tint[1], kb = mat.Tint[2], va = 1;
      if (mat.Vc) { kr *= C[a * 4] * b0 + C[b * 4] * b1 + C[c * 4] * b2; kg *= C[a * 4 + 1] * b0 + C[b * 4 + 1] * b1 + C[c * 4 + 1] * b2; kb *= C[a * 4 + 2] * b0 + C[b * 4 + 2] * b1 + C[c * 4 + 2] * b2; va = C[a * 4 + 3] * b0 + C[b * 4 + 3] * b1 + C[c * 4 + 3] * b2; }
      if (mat.Tex == null) { kr = mat.Vc ? kr / Math.Max(1e-6, mat.Tint[0]) : 1; kg = mat.Vc ? kg / Math.Max(1e-6, mat.Tint[1]) : 1; kb = mat.Vc ? kb / Math.Max(1e-6, mat.Tint[2]) : 1; } // Flat already holds the tint
      if (mat.Amask && mat.Tex != null) { r = tr * (1 - ta + ta * kr); g = tg * (1 - ta + ta * kg); bl = tb * (1 - ta + ta * kb); }
      else { r = tr * kr; g = tg * kg; bl = tb * kb; }
      if (mat.Mode == "fade" || mat.Mode == "add") { alpha = Math.Clamp((mat.Tex != null ? ta : 1) * mat.TintA * va, 0, 1); if (mat.Glass && mat.Mode == "fade") alpha = Math.Min(alpha, 0.3); }
      // AG glow: tex.rgb * tex.a * _EmissivePower * (vc * _BaseColor)
      if (mat.Glow > 0) { double ga = mat.Tex != null ? ta : 1; er = tr * ga * mat.Glow * kr; eg = tg * ga * mat.Glow * kg; eb = tb * ga * mat.Glow * kb; }
    }
    double or, og, ob;
    if (!mat.Lit && !mat.Terrain) { or = r; og = g; ob = bl; }
    else {
      // the AG/URP forward path: diffuse = albedo * (1 - max(spec)); SH ambient + env specular + main light (+ GGX-ish
      // highlight) + Rec Room's per-vertex additional lights
      double sp = Math.Max(mat.Spec[0], Math.Max(mat.Spec[1], mat.Spec[2])); double dr = r * (1 - sp), dg = g * (1 - sp), db = bl * (1 - sp);
      var amb = Ambient(n); or = amb[0] * dr; og = amb[1] * dg; ob = amb[2] * db;
      double pr = 1 - mat.Smooth, rough = Math.Max(pr * pr, 0.0078125), r2 = rough * rough;
      if (sp > 0 || mat.Smooth > 0) {
        double nv = Math.Clamp(Dot(n, vdir), 0, 1); var R = n * (2 * Dot(n, vdir)) - vdir; double fres = Math.Pow(1 - nv, 4); double grazing = Math.Clamp(sp + mat.Smooth, 0, 1);
        var env = EnvCube(R, pr * (1.7 - 0.7 * pr) * 6); double sr = 1 / (r2 + 1);
        or += env[0] * sr * (mat.Spec[0] + (grazing - mat.Spec[0]) * fres); og += env[1] * sr * (mat.Spec[1] + (grazing - mat.Spec[1]) * fres); ob += env[2] * sr * (mat.Spec[2] + (grazing - mat.Spec[2]) * fres);
      }
      if (HasSun) {
        double nl = Dot(n, SunDir);
        if (nl > 0) {
          double sh = mat.NoShadow ? 1 : 1 - SunStrength * (1 - ShadowAt(wp, n));
          double hs = 0;
          if (sp > 0) { var Hh = Nrm(vdir + SunDir); double nh = Math.Clamp(Dot(n, Hh), 0, 1), lh = Math.Clamp(Dot(SunDir, Hh), 0, 1); double dd = nh * nh * (r2 - 1) + 1.00001; hs = Math.Clamp(r2 / (dd * dd * Math.Max(lh * lh, 0.1) * (rough * 4 + 2)) - 6.10351562e-05, 0, 1000); }
          or += (dr + mat.Spec[0] * hs) * SunCol[0] * nl * sh; og += (dg + mat.Spec[1] * hs) * SunCol[1] * nl * sh; ob += (db + mat.Spec[2] * hs) * SunCol[2] * nl * sh;
        }
      }
      double vr = 0, vg = 0, vb = 0;
      foreach (var pl in Lights) {
        var L2 = pl.p - wp; double d2 = Dot(L2, L2); double rr = pl.range * pl.range; if (d2 >= rr) continue; var ld = L2 * (1 / Math.Sqrt(Math.Max(d2, 6.1e-5)));
        double nl = Math.Clamp(Dot(n, ld), 0, 1); if (nl <= 0) continue;
        double s = Math.Clamp(1 - d2 / rr, 0, 1); double att = s * s * (3 - 2 * s);
        double cd = -Dot(ld, pl.fwd); double cin = pl.type == 0 ? pl.spotIn : pl.cosIn, cout = pl.type == 0 ? pl.spotOut : pl.cosOut;
        if (cin > -2) { double q = Math.Clamp((cd - cout) / Math.Max(1e-4, cin - cout), 0, 1); q = q * q * (3 - 2 * q); att *= q * q; }
        vr += pl.col[0] * nl * att; vg += pl.col[1] * nl * att; vb += pl.col[2] * nl * att;
      }
      or += vr * dr; og += vg * dg; ob += vb * db;
    }
    or += er; og += eg; ob += eb;
    if (mat.Emit != null && mat.Glow <= 0) { or += mat.Emit[0]; og += mat.Emit[1]; ob += mat.Emit[2]; }
    if (ShadeDbg != null && ShadeDbg.IsMatch(mat.Name) && System.Threading.Interlocked.Increment(ref shadeDbgN) < 4) Console.WriteLine($"   shade {mat.Name} r {r:0.###},{g:0.###},{bl:0.###} e {er:0.###} emit {(mat.Emit == null ? "-" : string.Join(",", mat.Emit))} lit {mat.Lit} tint {string.Join(",", mat.Tint)} vc {mat.Vc} C {C[a * 4]},{C[a * 4 + 1]},{C[a * 4 + 2]},{C[a * 4 + 3]} tex {mat.Tex?.W} out {or:0.###},{og:0.###},{ob:0.###}");
    if (Fog && mat.Mode != "add") {
      double fk = FogMode == 1 ? Math.Clamp((FogEnd - dist) / Math.Max(1e-6, FogEnd - FogStart), 0, 1) : FogMode == 2 ? Math.Exp(-FogDens * dist) : Math.Exp(-(FogDens * dist) * (FogDens * dist));
      or = FogCol[0] + (or - FogCol[0]) * fk; og = FogCol[1] + (og - FogCol[1]) * fk; ob = FogCol[2] + (ob - FogCol[2]) * fk;
    }
    return new[] { or, og, ob };
  }

  // -------------------------------------------------------------------------------------------- the camera
  sealed class Cam { public V3 pos, fwd, right, up; public double f; public int W, H; public double pixAngle; }
  static Cam MakeCam(Pose p, int W, int H) {
    var cam = new Cam { pos = new V3(p.Pos[0], p.Pos[1], p.Pos[2]), W = W, H = H };
    cam.fwd = Nrm(new V3(p.Look[0] - p.Pos[0], p.Look[1] - p.Pos[1], p.Look[2] - p.Pos[2]));
    cam.right = Nrm(new V3(cam.fwd.z, 0, -cam.fwd.x)); if (cam.right.Len() < 0.5) cam.right = new V3(1, 0, 0);
    cam.up = Cross(cam.fwd, cam.right); cam.f = H * 0.5 / Math.Tan(p.Fov * Math.PI / 360); cam.pixAngle = 1 / cam.f; return cam;
  }

  static byte[] RenderView(Pose pose, int W, int H) {
    var cam = MakeCam(pose, W, H); const double near = 0.05;
    var zb = new float[W * H]; Array.Fill(zb, float.MaxValue); var tid = new int[W * H]; Array.Fill(tid, -1); var bb = new float[W * H * 2];
    // camera-space vertices
    var cx = new float[NV]; var cy = new float[NV]; var cz = new float[NV];
    for (int i = 0; i < NV; i++) { var d = new V3(P[i * 3] - cam.pos.x, P[i * 3 + 1] - cam.pos.y, P[i * 3 + 2] - cam.pos.z); cx[i] = (float)Dot(d, cam.right); cy[i] = (float)Dot(d, cam.up); cz[i] = (float)Dot(d, cam.fwd); }
    double tanX = W * 0.5 / cam.f + 0.01, tanY = H * 0.5 / cam.f + 0.01;
    var trans = new List<(int t, double depth)>();
    for (int t = 0; t < NT; t++) {
      int a = I[t * 3], b = I[t * 3 + 1], c = I[t * 3 + 2];
      if (cz[a] < near && cz[b] < near && cz[c] < near) continue;
      if (pose.YMax < 1e8 && Math.Min(P[a * 3 + 1], Math.Min(P[b * 3 + 1], P[c * 3 + 1])) > pose.YMax) continue;
      // frustum reject
      if (cx[a] > tanX * cz[a] && cx[b] > tanX * cz[b] && cx[c] > tanX * cz[c]) continue;
      if (-cx[a] > tanX * cz[a] && -cx[b] > tanX * cz[b] && -cx[c] > tanX * cz[c]) continue;
      if (cy[a] > tanY * cz[a] && cy[b] > tanY * cz[b] && cy[c] > tanY * cz[c]) continue;
      if (-cy[a] > tanY * cz[a] && -cy[b] > tanY * cz[b] && -cy[c] > tanY * cz[c]) continue;
      var mat = Mats[TM[t]];
      // back-face test in camera space (cross of camera-space edges against the vector to the eye)
      if (mat.Cull != 0) {
        double e1x = cx[b] - cx[a], e1y = cy[b] - cy[a], e1z = cz[b] - cz[a], e2x = cx[c] - cx[a], e2y = cy[c] - cy[a], e2z = cz[c] - cz[a];
        double nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x;
        double s = -(nx * cx[a] + ny * cy[a] + nz * cz[a]); // dot(n, eye - a), eye = origin
        if (mat.Cull == 2 && s < 0) continue; if (mat.Cull == 1 && s > 0) continue;
      }
      if (SkipMat != null && SkipMat.IsMatch(mat.Name)) continue;
      if (mat.Mode == "fade" || mat.Mode == "add") { trans.Add((t, (cz[a] + cz[b] + cz[c]) / 3)); continue; }
      Raster(t, cx, cy, cz, cam, near, (x, y, z, b0, b1, b2) => {
        int id = y * W + x; if (z >= zb[id]) return;
        if (mat.AlphaTest && mat.Tex != null) { var (u, v) = UvAt(t, b0, b1, b2, mat); if (mat.Tex.AlphaAt(u, v) < mat.Cut) return; }
        zb[id] = (float)z; tid[id] = t; bb[id * 2] = (float)b0; bb[id * 2 + 1] = (float)b1;
      });
    }
    var img = new double[W * H * 3];
    Parallel.For(0, H, y => {
      for (int x = 0; x < W; x++) {
        int id = y * W + x; double[] col;
        if (tid[id] < 0) {
          var dir = Nrm(cam.fwd + cam.right * ((x + 0.5 - W * 0.5) / cam.f) + cam.up * ((H * 0.5 - y - 0.5) / cam.f)); col = Sky(dir);
        } else { double b0 = bb[id * 2], b1 = bb[id * 2 + 1]; col = Shade(tid[id], b0, b1, 1 - b0 - b1, cam.pos, cam.pixAngle, out _); }
        if (DbgPix.x == x && DbgPix.y == y && tid[id] >= 0) Console.WriteLine($"   pix {x},{y} opaque tri {tid[id]} mat {Mats[TM[tid[id]]].Name} renderer {(TR[tid[id]] < RendNames.Count ? RendNames[TR[tid[id]]] : "terrain")} col {col[0]:0.###},{col[1]:0.###},{col[2]:0.###}");
        if (pose.Grid > 0 && tid[id] >= 0) { int t = tid[id]; double b0 = bb[id * 2], b1 = bb[id * 2 + 1], b2 = 1 - b0 - b1; int ia = I[t * 3], ib = I[t * 3 + 1], ic = I[t * 3 + 2]; double wx = P[ia * 3] * b0 + P[ib * 3] * b1 + P[ic * 3] * b2, wz = P[ia * 3 + 2] * b0 + P[ib * 3 + 2] * b1 + P[ic * 3 + 2] * b2; double gx = Math.Abs(wx - Math.Round(wx / pose.Grid) * pose.Grid), gz = Math.Abs(wz - Math.Round(wz / pose.Grid) * pose.Grid); if (gx < 0.06 || gz < 0.06) { bool ax = Math.Abs(wx) < 0.06 || Math.Abs(wz) < 0.06; col = ax ? new[] { 1.0, 0, 0 } : new[] { 0.0, 0, 0 }; } }
        img[id * 3] = col[0]; img[id * 3 + 1] = col[1]; img[id * 3 + 2] = col[2];
      }
    });
    // transparent: back to front, depth-tested against the opaque buffer, no depth write
    foreach (var (t, _) in trans.OrderByDescending(x => x.depth)) {
      var mat = Mats[TM[t]];
      Raster(t, cx, cy, cz, cam, near, (x, y, z, b0, b1, b2) => {
        int id = y * W + x; if (z >= zb[id]) return;
        var col = Shade(t, b0, b1, b2, cam.pos, cam.pixAngle, out double al);
        if (DbgPix.x == x && DbgPix.y == y) Console.WriteLine($"   pix {x},{y} tri {t} {mat.Name} mode {mat.Mode} src {mat.Src} col {col[0]:0.###},{col[1]:0.###},{col[2]:0.###} al {al:0.###} before {img[id * 3]:0.###},{img[id * 3 + 1]:0.###},{img[id * 3 + 2]:0.###}");
        if (mat.Mode == "add") { double k = mat.Src == 1 ? 1 : al; img[id * 3] += col[0] * k; img[id * 3 + 1] += col[1] * k; img[id * 3 + 2] += col[2] * k; }
        else if (mat.Src == 1) { img[id * 3] = img[id * 3] * (1 - al) + col[0]; img[id * 3 + 1] = img[id * 3 + 1] * (1 - al) + col[1]; img[id * 3 + 2] = img[id * 3 + 2] * (1 - al) + col[2]; }
        else { img[id * 3] = img[id * 3] * (1 - al) + col[0] * al; img[id * 3 + 1] = img[id * 3 + 1] * (1 - al) + col[1] * al; img[id * 3 + 2] = img[id * 3 + 2] * (1 - al) + col[2] * al; }
      });
    }
    // supersampled: box-filter SS x SS in linear space, then sRGB
    int w0 = W / SS, h0 = H / SS; var outb = new byte[w0 * h0 * 3]; double inv = 1.0 / (SS * SS);
    for (int y = 0; y < h0; y++) for (int x = 0; x < w0; x++) for (int ch = 0; ch < 3; ch++) {
      double s = 0; for (int dy = 0; dy < SS; dy++) for (int dx = 0; dx < SS; dx++) s += Math.Clamp(img[((y * SS + dy) * W + x * SS + dx) * 3 + ch] * Exposure, 0, 1);
      outb[(y * w0 + x) * 3 + ch] = (byte)Math.Round(Gam(s * inv) * 255);
    }
    return outb;
  }

  delegate void Frag(int x, int y, double z, double b0, double b1, double b2);
  /// near-clipped, perspective-correct rasterisation of triangle t; the callback gets the original triangle's barycentrics
  static void Raster(int t, float[] cx, float[] cy, float[] cz, Cam cam, double near, Frag frag) {
    int[] vi = { I[t * 3], I[t * 3 + 1], I[t * 3 + 2] };
    // polygon in camera space with barycentric weights
    var poly = new List<(double x, double y, double z, double b0, double b1, double b2)>(4);
    for (int k = 0; k < 3; k++) poly.Add((cx[vi[k]], cy[vi[k]], cz[vi[k]], k == 0 ? 1 : 0, k == 1 ? 1 : 0, k == 2 ? 1 : 0));
    if (poly.Any(p => p.z < near)) {
      var o = new List<(double x, double y, double z, double b0, double b1, double b2)>(4);
      for (int k = 0; k < poly.Count; k++) {
        var p0 = poly[k]; var p1 = poly[(k + 1) % poly.Count];
        if (p0.z >= near) o.Add(p0);
        if ((p0.z >= near) != (p1.z >= near)) { double s = (near - p0.z) / (p1.z - p0.z); o.Add((p0.x + (p1.x - p0.x) * s, p0.y + (p1.y - p0.y) * s, near, p0.b0 + (p1.b0 - p0.b0) * s, p0.b1 + (p1.b1 - p0.b1) * s, p0.b2 + (p1.b2 - p0.b2) * s)); }
      }
      poly = o; if (poly.Count < 3) return;
    }
    int W = cam.W, H = cam.H; double f = cam.f;
    var sx = new double[poly.Count]; var sy = new double[poly.Count];
    for (int k = 0; k < poly.Count; k++) { sx[k] = W * 0.5 + poly[k].x * f / poly[k].z; sy[k] = H * 0.5 - poly[k].y * f / poly[k].z; }
    for (int fi = 1; fi + 1 < poly.Count; fi++) {
      int A = 0, B = fi, Cc = fi + 1;
      double ax = sx[A], ay = sy[A], bx = sx[B], by = sy[B], ccx = sx[Cc], ccy = sy[Cc];
      double den = (by - ccy) * (ax - ccx) + (ccx - bx) * (ay - ccy); if (Math.Abs(den) < 1e-12) continue;
      int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, ccx)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, ccx))));
      int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, ccy)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, ccy))));
      if (x0 > x1 || y0 > y1) continue;
      double iza = 1 / poly[A].z, izb = 1 / poly[B].z, izc = 1 / poly[Cc].z;
      var pa = poly[A]; var pb = poly[B]; var pc = poly[Cc];
      for (int y = y0; y <= y1; y++) {
        double py = y + 0.5;
        for (int x = x0; x <= x1; x++) {
          double px = x + 0.5;
          double l1 = ((by - ccy) * (px - ccx) + (ccx - bx) * (py - ccy)) / den; if (l1 < 0) continue;
          double l2 = ((ccy - ay) * (px - ccx) + (ax - ccx) * (py - ccy)) / den; if (l2 < 0) continue;
          double l3 = 1 - l1 - l2; if (l3 < 0) continue;
          double w1 = l1 * iza, w2 = l2 * izb, w3 = l3 * izc; double ws = w1 + w2 + w3; double z = 1 / ws; w1 *= z; w2 *= z; w3 *= z;
          frag(x, y, z, pa.b0 * w1 + pb.b0 * w2 + pc.b0 * w3, pa.b1 * w1 + pb.b1 * w2 + pc.b1 * w3, pa.b2 * w1 + pb.b2 * w2 + pc.b2 * w3);
        }
      }
    }
  }

  // -------------------------------------------------------------------------------------------- commands
  static Model Load(string work, string level, bool verbose, out Ld L) {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    L = new Ld(work); var m = Model.Load(L, level);
    Console.WriteLine($"{level}: {m.Gos.Count} GOs, {m.Rend.Count} renderers, {m.Lights.Count} lights ({sw.Elapsed.TotalSeconds:F1} s)");
    var st = Build(L, m, verbose); BuildTerrain(L, m);
    while (ltr.Count < ltm.Count) ltr.Add(int.MaxValue); TR = ltr.ToArray(); P = lp.ToArray(); Nn = ln.ToArray(); UV = luv.ToArray(); C = lc.ToArray(); I = li.ToArray(); TM = ltm.ToArray(); TCast = lcast.ToArray(); NV = P.Length / 3; NT = TM.Length;
    lp = ln = luv = lc = null; li = ltm = null; lcast = null;
    // texel density per triangle: sqrt(uv area / world area) in uv units per metre (tiling included)
    TK = new float[NT];
    for (int t = 0; t < NT; t++) {
      var mat = Mats[TM[t]]; if (mat.Tex == null) continue; int a = I[t * 3], b = I[t * 3 + 1], c = I[t * 3 + 2];
      var pa = new V3(P[a * 3], P[a * 3 + 1], P[a * 3 + 2]); double wa = Cross(new V3(P[b * 3], P[b * 3 + 1], P[b * 3 + 2]) - pa, new V3(P[c * 3], P[c * 3 + 1], P[c * 3 + 2]) - pa).Len();
      double ua = Math.Abs((UV[b * 2] - UV[a * 2]) * (UV[c * 2 + 1] - UV[a * 2 + 1]) - (UV[c * 2] - UV[a * 2]) * (UV[b * 2 + 1] - UV[a * 2 + 1])) * Math.Abs(mat.Tile[0] * mat.Tile[1]);
      TK[t] = wa > 1e-12 ? (float)Math.Sqrt(ua / wa) : 0;
    }
    Console.WriteLine($"renderers {st.Rend}: drawn {st.Drawn}, LOD1+ {st.Lod}, disabled {st.Disabled}, inactive {st.Inactive}, no mesh {st.NoMesh}, mesh not decoded {st.BadMesh}, hidden-shader submeshes {st.Hidden}; {NT} triangles, {NV} vertices; materials {Mats.Count} (with texture {Mats.Count(x => x.Tex != null)}); submeshes by mode {string.Join(" ", st.Modes.Select(k => k.Key + "=" + k.Value))} ({sw.Elapsed.TotalSeconds:F1} s)");
    SetupEnv(L, m);
    return m;
  }

  public static void List(string work, string level) {
    var m = Load(work, level, true, out var L);
    foreach (var mt in Mats.OrderByDescending(x => x.Uses)) Console.WriteLine($"{mt.Uses,4} {mt.Name} | {mt.Shader} | {mt.Mode}{(mt.AlphaTest ? " cut=" + Y.F(mt.Cut) : "")} cull={mt.Cull} | tex {mt.TexName} {(mt.Tex != null ? mt.Tex.W + "x" + mt.Tex.H : "(flat " + (mt.Flat == null ? "-" : string.Join(",", mt.Flat.Select(v => Y.F(Gam(v))))) + ")")} tile {Y.F(mt.Tile[0])},{Y.F(mt.Tile[1])} | tint {string.Join(",", mt.Tint.Select(v => Y.F(Gam(v))))} a={Y.F(mt.TintA)}{(mt.Vc ? " vc" : "")}{(mt.Amask ? " amask" : "")}{(mt.Unlit ? " unlit" : "")}{(mt.Emit != null ? " emit" : "")}{(mt.Glow > 0 ? " glow=" + Y.F(mt.Glow) : "")} blend={mt.Src}/{mt.Dst}{(mt.Lit ? " lit" : "")}{(mt.Smooth > 0 ? " smooth=" + Y.F(mt.Smooth) : "")}{(mt.Spec.Max() > 0 ? " spec=" + string.Join(",", mt.Spec.Select(v => Y.F(Gam(v)))) : "")}{(mt.NoShadow ? " noshadow" : "")} {mt.Note}");
  }

  public static void Run(string work, string level, string posesPath, string outDir, string[] opt) {
    string Opt(string k, string d) => opt.FirstOrDefault(o => o.StartsWith(k + "="))?.Substring(k.Length + 1) ?? d;
    TexMax = int.Parse(Opt("texmax", "512")); Exposure = double.Parse(Opt("exp", "1")); UseProbe = Opt("amb", "trilight") == "probe"; SS = Math.Max(1, int.Parse(Opt("ss", "2")));
    var only = Opt("only", null) is string on ? new Regex(on) : null;
    if (Opt("skipmat", null) is string skm) SkipMat = new Regex(skm);
    var poses = PoseCmd.Read(posesPath).Where(p => only == null || only.IsMatch(p.Name)).ToList();
    Console.WriteLine($"{poses.Count} poses from {posesPath}");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var m = Load(work, level, false, out var L);
    if (Opt("sky", "1") == "0") SkyFaces = null;
    if (Opt("shadow", "4096") != "0") { BuildShadow(int.Parse(Opt("shadow", "4096"))); Console.WriteLine($"shadow map {SMN}x{SMN}, {1 / Math.Max(1e-9, SScale) * 100:F1} cm per texel ({sw.Elapsed.TotalSeconds:F1} s)"); }
    Directory.CreateDirectory(outDir);
    var sheets = new Dictionary<string, SortedDictionary<int, byte[]>>();
    int done = 0;
    Parallel.ForEach(poses, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, System.Environment.ProcessorCount / 2) }, p => {
      var t0 = System.Diagnostics.Stopwatch.StartNew();
      var img = RenderView(p, p.W * SS, p.H * SS); RcPng.Write(Path.Combine(outDir, p.Name + ".png"), p.W, p.H, img);
      if (p.Sheet != null) { var tile = RenderView(p, 800 * SS, 450 * SS); lock (sheets) { if (!sheets.TryGetValue(p.Sheet, out var sd)) sheets[p.Sheet] = sd = new(); sd[p.Tile] = tile; } }
      lock (sheets) { done++; Console.WriteLine($"  [{done}/{poses.Count}] {p.Name}.png ({t0.Elapsed.TotalSeconds:F1} s)"); }
    });
    // 2x2 sheets like RrTourSteps (800x450 tiles, 4 px dark/white/white/dark lines, tile 1 top left)
    foreach (var kv in sheets) {
      int n = kv.Value.Count, cols = 2, rows = (n + 1) / 2, tw = 800, th = 450, gap = 4; int sw2 = tw * cols + gap * (cols - 1), sh = th * rows + gap * (rows - 1);
      var px = new byte[sw2 * sh * 3]; Array.Fill(px, (byte)255);
      int i = 0;
      foreach (var tile in kv.Value.Values) {
        int ox = (i % cols) * (tw + gap), oy = (i / cols) * (th + gap);
        for (int y = 0; y < th; y++) Buffer.BlockCopy(tile, y * tw * 3, px, ((oy + y) * sw2 + ox) * 3, tw * 3);
        i++;
      }
      for (int c = 1; c < cols; c++) { int x0 = c * (tw + gap) - gap; for (int y = 0; y < sh; y++) foreach (int k in new[] { 0, gap - 1 }) { int o = (y * sw2 + x0 + k) * 3; px[o] = px[o + 1] = px[o + 2] = 40; } }
      for (int r = 1; r < rows; r++) { int y0 = r * (th + gap) - gap; for (int x = 0; x < sw2; x++) foreach (int k in new[] { 0, gap - 1 }) { int o = ((y0 + k) * sw2 + x) * 3; px[o] = px[o + 1] = px[o + 2] = 40; } }
      RcPng.Write(Path.Combine(outDir, kv.Key + ".png"), sw2, sh, px);
      Console.WriteLine($"  sheet {kv.Key}.png ({sw2}x{sh}, {n} tiles)");
    }
    Console.WriteLine($"done in {sw.Elapsed.TotalSeconds:F1} s");
  }
}

static class RcPng {
  public static void Write(string path, int w, int h, byte[] rgb) {
    using var ms = new MemoryStream();
    using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) { for (int y = 0; y < h; y++) { z.WriteByte(0); z.Write(rgb, y * w * 3, w * 3); } }
    using var fs = File.Create(path);
    fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    void Chunk(string type, byte[] data) {
      var t = Encoding.ASCII.GetBytes(type); var len = BitConverter.GetBytes(data.Length); Array.Reverse(len); fs.Write(len); fs.Write(t); fs.Write(data);
      uint crc = Crc(t, 0xFFFFFFFF); crc = Crc(data, crc) ^ 0xFFFFFFFF; var cb = BitConverter.GetBytes(crc); Array.Reverse(cb); fs.Write(cb);
    }
    var ih = new byte[13]; var wb = BitConverter.GetBytes(w); Array.Reverse(wb); var hb = BitConverter.GetBytes(h); Array.Reverse(hb);
    Buffer.BlockCopy(wb, 0, ih, 0, 4); Buffer.BlockCopy(hb, 0, ih, 4, 4); ih[8] = 8; ih[9] = 2;
    Chunk("IHDR", ih); Chunk("IDAT", ms.ToArray()); Chunk("IEND", new byte[0]);
  }
  static uint[] table;
  static uint Crc(byte[] d, uint c) {
    if (table == null) { var tb = new uint[256]; for (uint n = 0; n < 256; n++) { uint x = n; for (int k = 0; k < 8; k++) x = (x & 1) != 0 ? 0xEDB88320 ^ (x >> 1) : x >> 1; tb[n] = x; } table = tb; }
    foreach (var b in d) c = table[(c ^ b) & 0xff] ^ (c >> 8); return c;
  }
}

/// glsl <work> <levelN> <matRe> <out.txt> [kwRe]: the full GLES3 text (vertex + fragment) of the forward pass variant of
/// the first matching material whose keywords match kwRe (default: the variant with the fewest keywords). Debugging.
static class GlslFull {
  public static void Run(string work, string level, string nameRe, string outPath, string kwRe) {
    var L = new Ld(work); var m = Model.Load(L, level); var f = m.F; var re = new Regex(nameRe, RegexOptions.IgnoreCase);
    foreach (var o in f.Objects.Where(x => x.ClassId == 23 || x.ClassId == 137)) {
      var d = L.Read(f, o); if (d == null) continue;
      foreach (var mp in d.Lst("m_Materials") ?? new()) {
        var (mf, mo) = L.Res(f, DX.Ptr(mp)); if (mo == null) continue; var md = L.Read(mf, mo); var name = md?.Str("m_Name"); if (name == null || !re.IsMatch(name)) continue;
        var (sf, so) = L.Res(mf, md.Ptr("m_Shader")); var sd = L.Read(sf, so); var pf = sd.Dct("m_ParsedForm");
        var sb = new StringBuilder($"== {name} {pf.Str("m_Name")} material keywords: {md.Str("m_ShaderKeywords")} {string.Join(" ", (md.Lst("m_ValidKeywords") ?? new()).Select(x => x?.ToString()))}\n");
        foreach (Dictionary<string, object> ss in pf.Lst("m_SubShaders")) foreach (Dictionary<string, object> ps in ss.Lst("m_Passes")) {
          var pn = ps.Dct("m_State")?.Str("m_Name") ?? ""; if (!Regex.IsMatch(pn, "Forward|Unlit|^$", RegexOptions.IgnoreCase)) continue;
          var progs = ShaderGlsl.Pass(sd, ps); if (progs.Count == 0) continue;
          var pick = kwRe != null ? progs.FirstOrDefault(p => Regex.IsMatch(string.Join(" ", p.kws), kwRe)) : progs.OrderBy(p => p.kws.Length).First();
          if (pick.text == null) pick = progs.OrderBy(p => p.kws.Length).First();
          sb.Append($"-- pass {pn}: {progs.Count} variants; this one: {string.Join(" ", pick.kws)}\n").Append(pick.text).Append('\n');
          File.WriteAllText(outPath, sb.ToString()); Console.WriteLine($"{name}: pass {pn} variant [{string.Join(" ", pick.kws)}] -> {outPath}"); return;
        }
      }
    }
    Console.WriteLine("no match");
  }
}

/// psdump <work>: PlayerSettings (class 129, no type tree) raw words after productName (debugging: m_ActiveColorSpace)
static class PsDump {
  public static void Run(string work) {
    var L = new Ld(work); var f = L.File("globalgamemanagers");
    foreach (var o in f.Objects.Where(x => x.ClassId == 129)) {
      int s = f.DataOff + (int)o.Start; var B = f.Data; int n = (int)o.Size;
      var txt = Encoding.ASCII.GetString(B, s, Math.Min(n, 4096));
      int k = txt.IndexOf("Rec Room", StringComparison.Ordinal); Console.WriteLine($"PlayerSettings {n} bytes, 'Rec Room' at {k}");
      if (k < 0) return; int p = (k + 8 + 3) & ~3;
      for (int i = 0; i < 90; i++) { int at = s + p + i * 4; Console.WriteLine($"  +{p + i * 4,4}: int {BitConverter.ToInt32(B, at),12} float {BitConverter.ToSingle(B, at),14:G6}"); }
    }
  }
}

/// nodes <work> <levelN> [pathRe]: every GameObject (TSV): path, active, world position, yaw (heading of +Z, degrees),
/// lossy scale, mesh name and the world AABB of its renderer's mesh bounds (for frame matching: rcref_cache frame).
static class NodesCmd {
  public static void Run(string work, string level, string re) {
    var L = new Ld(work); var m = Model.Load(L, level); var rx = new Regex(re, RegexOptions.IgnoreCase);
    var rend = new Dictionary<long, RendN>(); foreach (var r in m.Rend) if (!r.Lod1 && !rend.ContainsKey(r.Go)) rend[r.Go] = r;
    Console.WriteLine("path\tactive\tx\ty\tz\tyaw\tscale\tmesh\tmin\tmax");
    foreach (var go in m.Gos.Keys) {
      var path = m.PathOf(go); if (!rx.IsMatch(path)) continue;
      var w = m.GoW(go); var fw = w.r.Rot(new V3(0, 0, 1)); double yaw = Math.Atan2(fw.x, fw.z) * 180 / Math.PI;
      string mesh = "", mn = "", mx = "";
      if (rend.TryGetValue(go, out var rr) && rr.Mesh != null) {
        mesh = rr.Mesh.Name ?? "";
        var lo = new V3(1e9, 1e9, 1e9); var hi = new V3(-1e9, -1e9, -1e9); var c = rr.Mesh.C; var e = rr.Mesh.E;
        for (int i = 0; i < 8; i++) { var lp = new V3(c.x + ((i & 1) == 0 ? -e.x : e.x), c.y + ((i & 2) == 0 ? -e.y : e.y), c.z + ((i & 4) == 0 ? -e.z : e.z)); var p = w.p + w.r.Rot(w.s.Mul(lp)); lo = V3.Min(lo, p); hi = V3.Max(hi, p); }
        mn = $"{Y.F3(lo.x)},{Y.F3(lo.y)},{Y.F3(lo.z)}"; mx = $"{Y.F3(hi.x)},{Y.F3(hi.y)},{Y.F3(hi.z)}";
      }
      Console.WriteLine($"{path}\t{(m.Active(go) ? 1 : 0)}\t{Y.F3(w.p.x)}\t{Y.F3(w.p.y)}\t{Y.F3(w.p.z)}\t{yaw:F1}\t{Y.F3(w.s.x)},{Y.F3(w.s.y)},{Y.F3(w.s.z)}\t{mesh}\t{mn}\t{mx}");
    }
  }
}
