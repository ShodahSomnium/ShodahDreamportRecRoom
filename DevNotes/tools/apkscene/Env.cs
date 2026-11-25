// apkscene env: each level's lighting environment as Rec Room had it - RenderSettings in full (fog, ambient, skybox,
// reflection, sun), the skybox material with its shader and every property, every light with its shadow and bake
// settings, lightmap use, reflection probes and light probe groups. DATA ONLY: names and numbers.
//
// Output (one file per level, <outDir>/<Scene>.env, RRSK "E" lines; README "E records (iteration 8)"):
//   E fog / E ambient / E sky        as convert writes them
//   E skymat <material> shader=<shader> <prop>=<value>...     colours r,g,b,a; floats; textures tex:<name>[:WxH]
//   E sun px py pz qx qy qz qw r g b intensity shadows=<none|hard|soft> strength= bake=<realtime|mixed|baked> src=<rs|brightest> name=
//   E refl mode=<skybox|custom> intensity= bounces= res= custom=<texture>
//   E bake lightmapped=<renderers with a lightmap>/<active renderers> maps=<lightmap count> lights=<realtime>,<mixed>,<baked>
//          shadowlights=<lights with shadows> reflprobes=<n> lightprobegroups=<n> castoff=<renderers not casting>
//   E light <type> px py pz r g b intensity range spot shadows= strength= bake= name=      (every enabled light, also baked)
//   E probe px py pz sx sy sz mode=<baked|realtime|custom> intensity= box=0|1 importance= tex=<baked cubemap>
// plus a tab-separated summary row per level in <outDir>/_env.tsv.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

static class Env {
  public static void Probe(string work, string level) {
    var L = new Ld(work); var f = L.File(level);
    foreach (var o in f.Objects) if (o.ClassId == 215) { var d = L.Read(f, o); Console.WriteLine("RP " + Tools.Js(d, 3, 0)); break; }
    foreach (var o in f.Objects) if (o.ClassId == 104) { Console.WriteLine("RS size " + o.Size); Console.WriteLine(string.Join(" ", RsRaw(f, o).Select(kv => kv.Key + "=" + Show(kv.Value)))); }
    var cls = f.Objects.GroupBy(o => o.ClassId).Select(g => ClassNames.Of(g.Key) + "(" + g.Key + ")=" + g.Count());
    Console.WriteLine(string.Join(", ", cls));
  }

  static string Show(object v) => v switch { double[] a => string.Join(",", a.Select(Y.F)), float x => Y.F(x), (int a, long b) => a + ":" + b, float[] fa => string.Join(",", fa.Select(x => Y.F(x))), _ => v?.ToString() ?? "null" };

  /// RenderSettings (104) read raw (no PC type tree). Layout (Unity 6000.0, serializedVersion 10), 4-byte aligned:
  /// fog bool, fog colour, fog mode, density, linear start/end, ambient sky/equator/ground colours, ambient intensity,
  /// ambient mode, subtractive shadow colour, skybox PPtr, halo strength, flare strength, flare fade speed, halo texture
  /// PPtr, spot cookie PPtr, default reflection mode, resolution, bounces, reflection intensity, custom reflection PPtr,
  /// then the player-only fields: the ambient probe (27 SH floats, channel-major r0..r8 g0..g8 b0..b8), the generated
  /// skybox reflection PPtr, the sun PPtr, the indirect specular colour and "use radiance ambient probe" (1 byte).
  /// Measured on level113 (333 bytes): every byte consumed.
  public static Dictionary<string, object> RsRaw(AF f, AObj o) {
    int s = f.DataOff + (int)o.Start; var B = f.Data; int p = s; int end = s + (int)o.Size;
    float F() { var v = BitConverter.ToSingle(B, p); p += 4; return v; }
    int I() { var v = BitConverter.ToInt32(B, p); p += 4; return v; }
    (int, long) P() { int fid = BitConverter.ToInt32(B, p); long pid = BitConverter.ToInt64(B, p + 4); p += 12; return (fid, pid); }
    double[] C() => new double[] { F(), F(), F(), F() };
    var d = new Dictionary<string, object>();
    d["fog"] = B[p] != 0; p += 4; d["fogColor"] = C(); d["fogMode"] = I(); d["fogDensity"] = F(); d["fogStart"] = F(); d["fogEnd"] = F();
    d["ambSky"] = C(); d["ambEquator"] = C(); d["ambGround"] = C(); d["ambIntensity"] = F(); d["ambMode"] = I();
    d["subtractive"] = C(); d["sky"] = P(); d["halo"] = F(); d["flare"] = F(); d["flareFade"] = F(); d["haloTex"] = P(); d["cookie"] = P();
    d["reflMode"] = I(); d["reflRes"] = I(); d["reflBounces"] = I(); d["reflIntensity"] = F(); d["customRefl"] = P();
    var sh = new float[27]; for (int i = 0; i < 27; i++) sh[i] = F(); d["ambientProbe"] = sh; d["genRefl"] = P(); d["sun"] = P();
    d["indirectSpecular"] = C(); d["radianceProbe"] = p < end && B[p] != 0; d["consumed"] = (p + 1 - s) == (int)o.Size;
    var tail = new List<float>(); d["tail"] = tail.ToArray();
    return d;
  }

  static string ShaderName(Ld L, AF f, (int, long) sp) {
    var (sf, so) = L.Res(f, sp); if (so == null) return "?";
    var d = L.Read(sf, so);
    var n = d?.Dct("m_ParsedForm")?.Str("m_Name"); if (!string.IsNullOrEmpty(n)) return n;
    n = d?.Str("m_Name"); if (!string.IsNullOrEmpty(n)) return n;
    return "?shader@" + sf.Name + "#" + so.PathId;
  }

  static string TexName(Ld L, AF f, (int, long) tp) {
    var (tf, to) = L.Res(f, tp); if (to == null) return null;
    var d = L.Read(tf, to); var n = d?.Str("m_Name") ?? "?";
    int w = (int)(d?.Num("m_Width") ?? 0), h = (int)(d?.Num("m_Height") ?? 0);
    return ClassNames.Of(to.ClassId) + ":" + Y.Enc(n) + (w > 0 ? ":" + w + "x" + h : "");
  }

  public static string MatLine(Ld L, AF f, (int, long) mp, string label) {
    var (mf, mo) = L.Res(f, mp); if (mo == null) return null;
    var md = L.Read(mf, mo); if (md == null) return null;
    var sb = new StringBuilder($"E {label} {Y.Enc(md.Str("m_Name"))} shader={Y.Enc(ShaderName(L, mf, md.Ptr("m_Shader")))}");
    var kw = md.Lst("m_ValidKeywords"); if (kw != null && kw.Count > 0) sb.Append(" keywords=" + Y.Enc(string.Join(",", kw.Select(x => x as string))));
    var sp = md.Dct("m_SavedProperties");
    foreach (var x in sp?.Lst("m_Colors") ?? new()) { var e = (Dictionary<string, object>)x; var c = e.Dct("second"); sb.Append($" {e.Str("first")}={Y.F(c.Num("r"))},{Y.F(c.Num("g"))},{Y.F(c.Num("b"))},{Y.F(c.Num("a", 1))}"); }
    foreach (var x in sp?.Lst("m_Floats") ?? new()) { var e = (Dictionary<string, object>)x; sb.Append($" {e.Str("first")}={Y.F(e.Num("second"))}"); }
    foreach (var x in sp?.Lst("m_Ints") ?? new()) { var e = (Dictionary<string, object>)x; sb.Append($" {e.Str("first")}={(long)e.Num("second")}"); }
    foreach (var x in sp?.Lst("m_TexEnvs") ?? new()) {
      var e = (Dictionary<string, object>)x; var t = e.Dct("second"); var tn = TexName(L, mf, t.Ptr("m_Texture"));
      if (tn != null) sb.Append($" {e.Str("first")}=tex:{tn}");
    }
    return sb.ToString();
  }

  static string Bake(int lm) => lm switch { 4 => "realtime", 1 => "mixed", 2 => "baked", _ => lm.ToString() };
  static string Shadows(int t) => t switch { 0 => "none", 1 => "hard", 2 => "soft", _ => t.ToString() };
  static string LType(int t) => t switch { 0 => "spot", 1 => "dir", 2 => "point", 3 => "area", 4 => "disc", _ => t.ToString() };

  public static void Run(string work, string outDir, string[] pairs) {
    Directory.CreateDirectory(outDir);
    var L = new Ld(work);
    var tsv = new List<string>();
    string tsvPath = Path.Combine(outDir, "_env.tsv");
    if (File.Exists(tsvPath)) tsv.AddRange(File.ReadAllLines(tsvPath).Skip(1));
    foreach (var pair in pairs) {
      var kv = pair.Split('='); if (kv.Length != 2) { Console.WriteLine("skip " + pair + " (want levelN=Scene)"); continue; }
      string level = kv[0], scene = kv[1];
      var sw = System.Diagnostics.Stopwatch.StartNew();
      Model m; try { m = Model.Load(L, level); } catch (Exception e) { Console.WriteLine(level + ": " + e.Message); continue; }
      var f = m.F; var lines = new List<string> { $"# RRSK-E {scene} ({level}): lighting environment from the Android build (data only)" };
      Dictionary<string, object> rs = null;
      foreach (var o in f.Objects) if (o.ClassId == 104) rs = RsRaw(f, o);
      (int, long) sunPtr = (0, 0);
      if (rs != null) {
        var fc = (double[])rs["fogColor"]; int fm = (int)rs["fogMode"];
        lines.Add($"E fog {((bool)rs["fog"] ? fm switch { 1 => "linear", 2 => "exp", _ => "exp2" } : "off")} {Y.F(fc[0])} {Y.F(fc[1])} {Y.F(fc[2])} {Y.F((float)rs["fogDensity"])} {Y.F((float)rs["fogStart"])} {Y.F((float)rs["fogEnd"])}");
        var sky = (double[])rs["ambSky"]; var eq = (double[])rs["ambEquator"]; var gr = (double[])rs["ambGround"]; int am = (int)rs["ambMode"];
        lines.Add($"E ambient {Y.F(sky[0])} {Y.F(sky[1])} {Y.F(sky[2])} mode={(am switch { 0 => "skybox", 1 => "trilight", 3 => "flat", _ => am.ToString() })} intensity={Y.F((float)rs["ambIntensity"])} equator={Y.F(eq[0])},{Y.F(eq[1])},{Y.F(eq[2])} ground={Y.F(gr[0])},{Y.F(gr[1])},{Y.F(gr[2])}");
        var skp = ((int, long))rs["sky"]; var skn = L.NameOf(f, skp); if (skn != null) lines.Add($"E sky ?{Y.Enc(skn)}");
        var ml = MatLine(L, f, skp, "skymat"); if (ml != null) lines.Add(ml);
        sunPtr = ((int, long))rs["sun"];
        var cr = TexName(L, f, ((int, long))rs["customRefl"]);
        lines.Add($"E refl mode={((int)rs["reflMode"] == 1 ? "custom" : "skybox")} intensity={Y.F((float)rs["reflIntensity"])} bounces={rs["reflBounces"]} res={rs["reflRes"]}" + (cr != null ? " custom=" + cr : ""));

        lines.Add("E shprobe " + string.Join(" ", ((float[])rs["ambientProbe"]).Select(x => Y.F(x))));
        var isc = (double[])rs["indirectSpecular"];
        lines.Add($"# RenderSettings read {((bool)rs["consumed"] ? "exactly" : "NOT exactly")} ({rs["reflMode"]}); indirect specular {Y.F(isc[0])},{Y.F(isc[1])},{Y.F(isc[2])},{Y.F(isc[3])}; halo {Y.F((float)rs["halo"])} flare {Y.F((float)rs["flare"])}; sun ptr {sunPtr.Item1}:{sunPtr.Item2}");
      }
      // lights, in full
      int nRt = 0, nMixed = 0, nBaked = 0, nShadow = 0; string sunLine = null; double bestDir = -1; string bestDirLine = null;
      var lightLines = new List<string>();
      foreach (var o in f.Objects.Where(x => x.ClassId == 108)) {
        var d = L.Read(f, o); if (d == null) continue;
        long go = d.Ptr("m_GameObject").pid; bool en = d.Bool("m_Enabled", true) && m.Active(go); if (!en) continue;
        int type = (int)d.Num("m_Type"); var col = d.Col("m_Color") ?? new double[] { 1, 1, 1, 1 }; double inten = d.Num("m_Intensity");
        var sh = d.Dct("m_Shadows"); int st = (int)(sh?.Num("m_Type") ?? 0); double str = sh?.Num("m_Strength", 1) ?? 1;
        int lm = (int)d.Num("m_Lightmapping", 4);
        var bo = d.Dct("m_BakingOutput"); int lbt = (int)(bo?.Dct("lightmapBakeMode")?.Num("lightmapBakeType", lm) ?? lm); bool isBaked = bo?.Bool("isBaked") ?? false;
        if (lm == 4) nRt++; else if (lm == 1) nMixed++; else if (lm == 2) nBaked++;
        if (st != 0) nShadow++;
        var w = m.GoW(go);
        bool useTemp = d.Bool("m_UseColorTemperature"); double temp = d.Num("m_ColorTemperature", 6570);
        string common = $"shadows={Shadows(st)} strength={Y.F(str)} bake={Bake(lm)}" + (isBaked ? " isbaked=1" : "") + (useTemp ? $" temp={Y.F(temp)}" : "") + $" bounce={Y.F(d.Num("m_BounceIntensity", 1))}" + UrpKeys.Of(m, go, d) + $" name={Y.Enc(m.Name(go))}";
        lightLines.Add($"E light {LType(type)} {w.p} {w.r} {Y.F(col[0])} {Y.F(col[1])} {Y.F(col[2])} {Y.F(inten)} {Y.F(d.Num("m_Range"))} {Y.F(d.Num("m_SpotAngle"))} {common}");
        if (type == 1) {
          string line = $"E sun {w.p} {w.r} {Y.F(col[0])} {Y.F(col[1])} {Y.F(col[2])} {Y.F(inten)} {common}";
          if (sunPtr.Item2 != 0 && sunPtr.Item2 == o.PathId && sunPtr.Item1 == 0) sunLine = line + " src=rs";
          double lum = inten * (0.2126 * col[0] + 0.7152 * col[1] + 0.0722 * col[2]); if (lum > bestDir) { bestDir = lum; bestDirLine = line + " src=brightest"; }
        }
      }
      if (sunLine != null) lines.Add(sunLine); else if (bestDirLine != null) lines.Add(bestDirLine); else lines.Add("E sun none");
      // renderers: lightmap use, shadow casting
      int rAct = 0, rLm = 0, maxLm = -1, castOff = 0, recvOff = 0;
      foreach (var o in f.Objects.Where(x => x.ClassId == 23)) {
        var d = L.Read(f, o); if (d == null) continue; long go = d.Ptr("m_GameObject").pid;
        if (!d.Bool("m_Enabled", true) || !m.Active(go)) continue; rAct++;
        int li = (int)d.Num("m_LightmapIndex", 65535); if (li < 65534) { rLm++; maxLm = Math.Max(maxLm, li); }
        if ((int)d.Num("m_CastShadows", 1) == 0) castOff++; if ((int)d.Num("m_ReceiveShadows", 1) == 0) recvOff++;
      }
      // reflection probes, light probe groups
      var probeLines = new List<string>(); int nLpg = f.Objects.Count(x => x.ClassId == 220);
      foreach (var o in f.Objects.Where(x => x.ClassId == 215)) {
        var d = L.Read(f, o); if (d == null) continue; long go = d.Ptr("m_GameObject").pid; if (!d.Bool("m_Enabled", true) || !m.Active(go)) continue;
        var w = m.GoW(go); var bs = d.Vec("m_BoxSize"); var bofs = d.Vec("m_BoxOffset"); int mode = (int)d.Num("m_Mode");
        var tx = TexName(L, f, d.Ptr("m_BakedTexture")) ?? TexName(L, f, d.Ptr("m_CustomBakedTexture"));
        probeLines.Add($"E probe {(w.p + w.r.Rot(bofs))} {bs} mode={(mode switch { 0 => "baked", 1 => "realtime", 2 => "custom", _ => mode.ToString() })} intensity={Y.F(d.Num("m_IntensityMultiplier", 1))} box={(d.Bool("m_BoxProjection") ? 1 : 0)} importance={(int)d.Num("m_Importance", 1)}" + (tx != null ? " tex=" + tx : ""));
      }
      lines.Add($"E bake lightmapped={rLm}/{rAct} maps={maxLm + 1} lights={nRt},{nMixed},{nBaked} shadowlights={nShadow} reflprobes={probeLines.Count} lightprobegroups={nLpg} castoff={castOff} recvoff={recvOff}");
      lines.AddRange(lightLines); lines.AddRange(probeLines);
      File.WriteAllText(Path.Combine(outDir, scene + ".env"), string.Join("\n", lines) + "\n");
      tsv.RemoveAll(x => x.StartsWith(scene + "\t"));
      string skyMat = lines.FirstOrDefault(x => x.StartsWith("E skymat "))?.Split(' ').Take(4).Aggregate((a, b) => a + " " + b) ?? "-";
      tsv.Add($"{scene}\t{level}\t{skyMat}\t{(sunLine ?? bestDirLine ?? "none")}\t{rLm}/{rAct}\t{maxLm + 1}\t{nRt},{nMixed},{nBaked}\t{nShadow}\t{probeLines.Count}\t{nLpg}");
      Console.WriteLine($"{scene} ({level}): lightmapped {rLm}/{rAct}, maps {maxLm + 1}, lights rt/mixed/baked {nRt}/{nMixed}/{nBaked}, shadow lights {nShadow}, probes {probeLines.Count}, lpg {nLpg}; {skyMat}; {sw.ElapsedMilliseconds} ms");
      L.Drop(f);
    }
    File.WriteAllText(tsvPath, "scene\tlevel\tskymat\tsun\tlightmapped\tmaps\tlights_rt_mixed_baked\tshadowlights\treflprobes\tlightprobegroups\n" + string.Join("\n", tsv.OrderBy(x => x)) + "\n");
  }
}
static class EnvDbg {
  public static void Words(string work, string level) {
    var L = new Ld(work); var f = L.File(level);
    foreach (var o in f.Objects) if (o.ClassId == 104) {
      int s = f.DataOff + (int)o.Start; var B = f.Data;
      for (int i = 100; i + 4 <= o.Size; i += 4) Console.WriteLine($"{i}: int {BitConverter.ToInt32(B, s + i)} float {BitConverter.ToSingle(B, s + i)}");
      Console.WriteLine("last bytes: " + string.Join(" ", Enumerable.Range((int)o.Size - 8, 8).Select(k => B[s + k].ToString("x2"))));
    }
  }
}
static class EnvTex {
  public static void Show(string work, string level) {
    var L = new Ld(work); var f = L.File(level);
    foreach (var o in f.Objects) if (o.ClassId == 104) {
      var rs = Env.RsRaw(f, o); var mp = ((int, long))rs["sky"];
      var md = L.Read(f, mp); var (mf, _) = L.Res(f, mp);
      foreach (var x in md.Dct("m_SavedProperties").Lst("m_TexEnvs")) { var e = (Dictionary<string, object>)x; var tp = e.Dct("second").Ptr("m_Texture"); var (tf, to) = L.Res(mf, tp); if (to == null) continue;
        var d = L.Read(tf, to); Console.WriteLine(e.Str("first") + " -> " + tf.Name + "#" + to.PathId + " class " + to.ClassId + " size " + to.Size);
        Console.WriteLine(Tools.Js(d, 2, 0).Substring(0, Math.Min(3000, Tools.Js(d, 2, 0).Length))); }
    }
  }
}
/// Face averages of an Android ASTC cubemap without an ASTC decoder: the last mip (1x1) of each face is one block, and
/// an encoder writes a single texel as a "void extent" block (bits 0-8 = 0x1FC), whose constant colour is four UNORM16
/// values in bytes 8-15. Faces are stored one after another, each with its whole mip chain (+X -X +Y -Y +Z -Z).
static class CubeAvg {
  public static void Run(string work, string[] levels) {
    var L = new Ld(work);
    foreach (var level in levels) {
      var f = L.File(level); if (f == null) continue;
      foreach (var o in f.Objects) if (o.ClassId == 104) {
        var rs = Env.RsRaw(f, o); var mp = ((int, long))rs["sky"];
        foreach (var which in new[] { "sky", "refl" }) {
          (AF tf, AObj to) t = (null, null);
          if (which == "sky") {
            var md = L.Read(f, mp); if (md == null) continue; var (mf, _) = L.Res(f, mp);
            foreach (var x in md.Dct("m_SavedProperties").Lst("m_TexEnvs")) { var e = (Dictionary<string, object>)x; if (e.Str("first") != "_Tex") continue; t = L.Res(mf, e.Dct("second").Ptr("m_Texture")); }
          } else t = L.Res(f, ((int, long))rs["customRefl"]);
          if (t.to == null || t.to.ClassId != 89) continue;
          var d = L.Read(t.tf, t.to); var img = d.G("image data") as byte[]; int per = (int)d.Num("m_CompleteImageSize"); int fmt = (int)d.Num("m_TextureFormat");
          var sb = new StringBuilder($"{level} {which} {d.Str("m_Name")} fmt {fmt} {d.Num("m_Width")}px:");
          if (img == null || img.Length < per * 6) { Console.WriteLine(sb + " no inline data (" + (img?.Length ?? -1) + ")"); continue; }
          for (int face = 0; face < 6; face++) {
            int at = face * per + per - 16; int bits = img[at] | (img[at + 1] << 8);
            if ((bits & 0x1FF) != 0x1FC) { sb.Append(" [not void]"); continue; }
            double R(int k) => (img[at + 8 + k * 2] | (img[at + 9 + k * 2] << 8)) / 65535.0;
            sb.Append($" {Y.F(R(0))},{Y.F(R(1))},{Y.F(R(2))},a{Y.F(R(3))}");
          }
          Console.WriteLine(sb);
        }
      }
    }
  }
}
static class ProbeDbg {
  /// LightProbes (258) have no PC tree: find the positions (n x Vector3) and baked SH (n x 27 floats) arrays by
  /// their counts, print the mean SH DC and the range.
  public static void Run(string work, string file) {
    var L = new Ld(work); var f = L.File(file);
    foreach (var o in f.Objects) if (o.ClassId == 258) {
      int s = f.DataOff + (int)o.Start; var B = f.Data; int size = (int)o.Size;
      Console.WriteLine($"LightProbes {o.PathId} size {size}");
      for (int i = 0; i + 4 <= size; i += 4) {
        int n = BitConverter.ToInt32(B, s + i); if (n < 4 || n > 20000) continue;
        if (i + 4 + n * 108 <= size) {
          // plausible SH block: DC terms positive-ish and small
          int ok = 0; double dr = 0, dg = 0, db = 0;
          for (int k = 0; k < Math.Min(n, 50); k++) { float r0 = BitConverter.ToSingle(B, s + i + 4 + k * 108), g0 = BitConverter.ToSingle(B, s + i + 4 + k * 108 + 36), b0 = BitConverter.ToSingle(B, s + i + 4 + k * 108 + 72); if (r0 >= 0 && r0 < 20 && g0 >= 0 && g0 < 20 && b0 >= 0 && b0 < 20 && !float.IsNaN(r0)) ok++; }
          if (ok == Math.Min(n, 50) && i + 4 + n * 108 == size - 4 - n * 0 || (ok == Math.Min(n, 50) && n > 10)) {
            for (int k = 0; k < n; k++) { dr += BitConverter.ToSingle(B, s + i + 4 + k * 108); dg += BitConverter.ToSingle(B, s + i + 4 + k * 108 + 36); db += BitConverter.ToSingle(B, s + i + 4 + k * 108 + 72); }
            Console.WriteLine($"  candidate SH array at {i}: n={n}, ends at {i + 4 + n * 108} of {size}; mean DC {dr / n:0.0000},{dg / n:0.0000},{db / n:0.0000}");
          }
        }
        if (i + 4 + n * 12 <= size) {
          // positions: floats within +-2000
          int ok = 0; for (int k = 0; k < Math.Min(n, 30); k++) { float x = BitConverter.ToSingle(B, s + i + 4 + k * 12), y = BitConverter.ToSingle(B, s + i + 8 + k * 12); if (Math.Abs(x) < 2000 && Math.Abs(y) < 500 && Math.Abs(x) > 0.001) ok++; }
          if (ok == Math.Min(n, 30) && n > 10) Console.WriteLine($"  candidate positions at {i}: n={n}, first {BitConverter.ToSingle(B, s + i + 4):0.00},{BitConverter.ToSingle(B, s + i + 8):0.00},{BitConverter.ToSingle(B, s + i + 12):0.00}");
        }
      }
    }
  }
}
