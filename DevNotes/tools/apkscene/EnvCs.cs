// apkscene envcs: the per-level .env files (Env.cs) -> Assets/#User/SomniumRecRoom/Core/RrEnvironmentTable.cs, the
// lighting table the world's RrEnvironment applies. One compact line per scene (format in RrEnvironmentSetting.cs):
//   key=<scene> src=<where the numbers come from> fog=... amb=... refl=... sun=... sky=... lights=... shadow=...
// The sky becomes colours for the install's own sky shader (AG/Skybox/Simple Gradient):
//   - AG/Skybox/Simple Gradient (Stunt Runner): the material's own six colours, exposure, power and transition;
//   - Skybox/Cubemap (a baked cubemap that is not in the PC install): the cubemap's face averages from the Android
//     file (the 1x1 mip of each face is an ASTC void-extent block: its RGBM colour x 5 is the face's mean), with the
//     level's fog colour as the horizon (Rec Room fogs to the horizon colour);
//   - Skybox/Procedural (quests): Unity's procedural sky model evaluated at six elevations with the material's own
//     tint, ground colour, thickness and exposure (the runtime uses the shader itself when the client has it).
// Nothing but numbers is written.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

static class EnvCs {
  static readonly CultureInfo IC = CultureInfo.InvariantCulture;
  static string F(double v) => Y.F(v);
  static double P(string s) => double.Parse(s, IC);

  public static void Run(string work, string envDir, string outCs, string[] extraDirs) {
    var L = new Ld(work);
    var files = Directory.GetFiles(envDir, "*.env").ToList();
    foreach (var d in extraDirs) if (Directory.Exists(d)) files.AddRange(Directory.GetFiles(d, "*.env"));
    var lines = new List<(string key, string line, string note)>();
    foreach (var path in files.OrderBy(x => Path.GetFileName(x), StringComparer.Ordinal)) {
      string key = Path.GetFileNameWithoutExtension(path);
      var rec = File.ReadAllLines(path);
      string level = null; var head = rec.FirstOrDefault(x => x.StartsWith("# RRSK-E ")); if (head != null) { int a = head.IndexOf('('), b = head.IndexOf(')'); if (a > 0 && b > a) level = head.Substring(a + 1, b - a - 1); }
      string src = rec.FirstOrDefault(x => x.StartsWith("# src "))?.Substring(6).Trim() ?? ("apk " + (level ?? "?"));
      var sb = new StringBuilder("key=" + key + " src=" + src.Replace(' ', '_'));
      string[] fog = null, amb = null, sun = null, skymat = null, refl = null, bake = null; string skyOverride = null;
      var shadowLights = new List<string>(); int nLights = 0; var rig = new List<string>(); var urp = new List<string>();
      foreach (var r in rec) {
        var t = r.Split(' ');
        if (r.StartsWith("E fog ")) fog = t; else if (r.StartsWith("E ambient ")) amb = t; else if (r.StartsWith("E sun ")) sun = t;
        else if (r.StartsWith("E skymat ")) skymat = t; else if (r.StartsWith("E refl ")) refl = t; else if (r.StartsWith("E bake ")) bake = t;
        else if (r.StartsWith("E skygrad ")) skyOverride = r.Substring(10).Trim();
        else if (r.StartsWith("E light ")) {
          if (t[2] != "dir") { nLights++; rig.Add($"{t[2]},{t[3]},{t[4]},{t[5]},{t[6]},{t[7]},{t[8]},{t[9]},{t[10]},{t[11]},{t[12]},{t[13]},{t[14]},{t[15]},{((Kv(t, "shadows") ?? "none") != "none" ? 1 : 0)}"); }
          if (t[2] != "dir" && Kv(t, "shadows") is string sh && sh != "none") shadowLights.Add($"{t[3]},{t[4]},{t[5]}");
          // iteration 9: Rec Room's URPLight values (specular contribution, hemisphere lights) and spot inner angles, by position
          if (t[2] != "dir" && ((Kv(t, "spec") is string sp && sp != "1") || Kv(t, "hemi") == "1" || t[2] == "spot")) urp.Add($"{t[3]},{t[4]},{t[5]},{Kv(t, "spec") ?? "1"},{Kv(t, "hemi") ?? "0"},{Kv(t, "hin") ?? "0"},{Kv(t, "hout") ?? "180"},{Kv(t, "ispot") ?? "-1"}");
        }
      }
      if (fog != null) sb.Append($" fog={fog[2]},{fog[3]},{fog[4]},{fog[5]},{fog[6]},{fog[7]},{fog[8]}");
      if (amb != null) {
        string mode = Kv(amb, "mode") ?? "trilight"; string eq = Kv(amb, "equator") ?? $"{amb[2]},{amb[3]},{amb[4]}"; string gr = Kv(amb, "ground") ?? eq;
        sb.Append($" amb={mode},{amb[2]},{amb[3]},{amb[4]},{eq},{gr},{Kv(amb, "intensity") ?? "1"}");
      }
      // reflc=1: the level reflected a baked environment cubemap (RenderSettings custom reflection); else its sky
      if (refl != null) sb.Append(" refl=" + (Kv(refl, "intensity") ?? "1") + (Kv(refl, "mode") == "custom" ? " reflc=1" : ""));
      if (sun != null && sun.Length > 12 && sun[2] != "none") {
        // E sun px py pz qx qy qz qw r g b intensity key=...
        sb.Append($" sun={sun[5]},{sun[6]},{sun[7]},{sun[8]},{sun[9]},{sun[10]},{sun[11]},{sun[12]},{Kv(sun, "shadows") ?? "soft"},{Kv(sun, "strength") ?? "1"}");
      }
      string sky = skyOverride ?? Sky(L, level, skymat, fog, sun);
      if (sky != null) sb.Append(" sky=" + sky);
      if (skymat != null && skymat[3] == "shader=Skybox/Procedural") {
        sb.Append($" proc={Kv(skymat, "_SkyTint") ?? "0.5,0.5,0.5,1"}/{Kv(skymat, "_GroundColor") ?? "0.369,0.349,0.341,1"}/{Kv(skymat, "_AtmosphereThickness") ?? "1"}/{Kv(skymat, "_Exposure") ?? "1.3"}/{Kv(skymat, "_SunDisk") ?? "2"}/{Kv(skymat, "_SunSize") ?? "0.04"}/{Kv(skymat, "_SunSizeConvergence") ?? "5"}");
      }
      sb.Append(" lights=" + nLights);
      if (shadowLights.Count > 0) sb.Append(" shadow=" + string.Join(";", shadowLights.Take(8)));
      if (urp.Count > 0) sb.Append(" urp=" + string.Join(";", urp.Take(160)));
      // the whole light rig for small rooms (the world builds it for a room that is not a kit scene, e.g. the Dorm)
      if (rig.Count > 0 && rig.Count <= 16) sb.Append(" rig=" + string.Join(";", rig));
      string note = (skymat != null ? skymat[2] + " (" + skymat[3].Replace("shader=", "").Replace("%20", " ") + ")" : "no sky material") + (bake != null ? "; " + string.Join(" ", bake.Skip(2)) : "");
      lines.Add((key, sb.ToString(), note));
      Console.WriteLine(sb.ToString());
    }
    var cs = new StringBuilder();
    cs.Append("// <auto-generated>\n// Generated by DevNotes/tools/apkscene (envcs) from DevNotes/refdata/apk/env/*.env and DevNotes/refdata/env_extra/*.env.\n// Numbers only (colours, directions, intensities) measured from Rec Room's levels; nothing of Rec Room is embedded. Do not edit: regenerate.\n// </auto-generated>\n");
    cs.Append("namespace SomniumSpace.Worlds.SomniumRecRoom\n{\n");
    cs.Append("    /// <summary>Rec Room's lighting per scene, one line each (format: <see cref=\"RrEnvironmentSetting\"/>). " + lines.Count + " scenes.</summary>\n");
    cs.Append("    public static class RrEnvironmentTable\n    {\n        public static readonly string[] Lines =\n        {\n");
    foreach (var (key, line, note) in lines) cs.Append($"            // {key}: {note.Replace("*/", "")}\n            \"{line}\",\n");
    cs.Append("        };\n    }\n}\n");
    File.WriteAllText(outCs, cs.ToString().Replace("\n", "\r\n"));
    Console.WriteLine($"wrote {outCs}: {lines.Count} scenes");
  }

  static string Kv(string[] t, string k) { foreach (var x in t) if (x.StartsWith(k + "=")) return x.Substring(k.Length + 1); return null; }

  static double[] Col(string s) { if (s == null) return null; var p = s.Split(','); return p.Select(P).ToArray(); }

  static string C3(double[] c) => $"{F(Math.Clamp(c[0], 0, 8))},{F(Math.Clamp(c[1], 0, 8))},{F(Math.Clamp(c[2], 0, 8))}";

  /// Sky gradient: "top/mid/horizon/groundHorizon/groundMid/groundBottom/exposure/power/transition", colours sRGB r,g,b.
  static string Sky(Ld L, string level, string[] skymat, string[] fog, string[] sun) {
    if (skymat == null) return null;
    string shader = skymat[3].Replace("shader=", "").Replace("%20", " ");
    if (shader == "AG/Skybox/Simple Gradient") {
      var top = Col(Kv(skymat, "_SkyTopColor")); var mid = Col(Kv(skymat, "_SkyMidColor")); var hor = Col(Kv(skymat, "_SkyHorizonColor"));
      var gh = Col(Kv(skymat, "_GroundHorizonColor")); var gm = Col(Kv(skymat, "_GroundMidColor")); var gt = Col(Kv(skymat, "_GroundTopColor"));
      if (top == null || mid == null || hor == null || gh == null || gm == null || gt == null) return null;
      return $"{C3(top)}/{C3(mid)}/{C3(hor)}/{C3(gh)}/{C3(gm)}/{C3(gt)}/{Kv(skymat, "_Exposure") ?? "1"}/{Kv(skymat, "_GradientPow") ?? "1"}/{Kv(skymat, "_GroundToSkyTransition") ?? "0.03"}";
    }
    if (shader == "Skybox/Cubemap" && level != null) {
      // The cubemap itself, decoded (Astc.cs, checked against REF2's Dormroom2_Baked_Skybox.png: every band within 0.015),
      // averaged by elevation where AG/Skybox/Simple Gradient puts each colour (harness sky probe, iteration 8: top at 90,
      // mid at +40, sky horizon at +20, ground horizon AT the horizon (it blends up to the sky horizon by +20), ground mid
      // at -30, ground "top" at -90): zenith 70-90, 35-50, 10-35, -3..3 (the haze line), -60..-10, -90..-60.
      {
        double ex = P(Kv(skymat, "_Exposure") ?? "1"); var ti = Col(Kv(skymat, "_Tint")) ?? new[] { 0.5, 0.5, 0.5 };
        var cf = SkyBands.Faces(L, level, 3, out int n, out string cname);
        if (cf != null) {
          var (bands, _) = SkyBands.Measure(cf, n, false, true);
          double[] M(double[] c) => c == null ? new[] { 0.0, 0.0, 0.0 } : new[] { c[0] * ti[0] * 2 * ex, c[1] * ti[1] * 2 * ex, c[2] * ti[2] * 2 * ex };
          double[] Avg(double[] p, double[] q) => p == null ? q : q == null ? p : Lerp(p, q, 0.5);
          var skyHorizon = Avg(bands[7], bands[8]); var groundHorizon = Avg(bands[4], bands[5]); var groundMid = Avg(bands[1], bands[2]);
          return $"{C3(M(bands[11]))}/{C3(M(bands[9]))}/{C3(M(skyHorizon))}/{C3(M(groundHorizon))}/{C3(M(groundMid))}/{C3(M(bands[0]))}/1/1/0.03";
        }
      }
      var faces = CubeFaces(L, level);
      if (faces == null) return null;
      double exposure = P(Kv(skymat, "_Exposure") ?? "1"); var tint = Col(Kv(skymat, "_Tint")) ?? new[] { 0.5, 0.5, 0.5 };
      double[] Mul(double[] c) => new[] { c[0] * tint[0] * 2 * exposure, c[1] * tint[1] * 2 * exposure, c[2] * tint[2] * 2 * exposure };
      var top = Mul(faces[2]); var bottom = Mul(faces[3]);
      var side = Mul(new[] { (faces[0][0] + faces[1][0] + faces[4][0] + faces[5][0]) / 4, (faces[0][1] + faces[1][1] + faces[4][1] + faces[5][1]) / 4, (faces[0][2] + faces[1][2] + faces[4][2] + faces[5][2]) / 4 });
      // A side face spans -45..45 degrees: about half sky just above the horizon, half ground; the sky half is what
      // remains when the ground (the bottom face's colour) is taken out.
      var low = new[] { Math.Clamp(2 * side[0] - bottom[0], 0, 1), Math.Clamp(2 * side[1] - bottom[1], 0, 1), Math.Clamp(2 * side[2] - bottom[2], 0, 1) };
      double[] hor = low;
      if (fog != null && fog[2] != "off") hor = new[] { P(fog[3]), P(fog[4]), P(fog[5]) };
      var mid = Lerp(top, low, 0.5);
      var gh = Lerp(bottom, hor, 0.35);
      return $"{C3(top)}/{C3(mid)}/{C3(hor)}/{C3(gh)}/{C3(bottom)}/{C3(bottom)}/1/1/0.03";
    }
    if (shader == "Custom/SkyBox") {
      // Rec Room's own procedural sky (Drive-In, Merry Midway, Rec Rally): sky top/bottom, a horizon band, ground
      // top/bottom, as its material sets them (HDR values kept up to 2).
      var st = Col(Kv(skymat, "_SkyTop")); var sbm = Col(Kv(skymat, "_SkyBottom")); var hz = Col(Kv(skymat, "_HorizonColor"));
      var gt = Col(Kv(skymat, "_GroundTop")); var gb = Col(Kv(skymat, "_GroundBottom"));
      if (st == null || sbm == null || gt == null || gb == null) return null;
      double hs = P(Kv(skymat, "_HorizonStrength") ?? "0"); double ex = P(Kv(skymat, "_Exposure") ?? "1");
      var hor = hz != null ? Lerp(sbm, hz, Math.Clamp(hs, 0, 1)) : sbm;
      double[] X(double[] c) => new[] { Math.Min(2, c[0] * ex), Math.Min(2, c[1] * ex), Math.Min(2, c[2] * ex) };
      return $"{C3(X(st))}/{C3(X(Lerp(st, sbm, 0.6)))}/{C3(X(hor))}/{C3(X(gt))}/{C3(X(Lerp(gt, gb, 0.5)))}/{C3(X(gb))}/1/1/0.03";
    }
    if (shader == "Skybox/Procedural") {
      var tint = Col(Kv(skymat, "_SkyTint")) ?? new[] { 0.5, 0.5, 0.5 }; var ground = Col(Kv(skymat, "_GroundColor")) ?? new[] { 0.369, 0.349, 0.341 };
      double thick = P(Kv(skymat, "_AtmosphereThickness") ?? "1"), exposure = P(Kv(skymat, "_Exposure") ?? "1.3");
      // the sun: the scene's key light, else URP's default main-light direction when there is none (0, 0, 1)
      var toSun = new[] { 0.0, 0.0, 1.0 };
      if (sun != null && sun.Length > 9 && sun[2] != "none") { var q = new Q(P(sun[5]), P(sun[6]), P(sun[7]), P(sun[8])); var f = q.Rot(new V3(0, 0, 1)); toSun = new[] { -f.x, -f.y, -f.z }; }
      var el = new[] { 90.0, 40, 20, 1, -30, -90 };  // where the gradient shader shows each colour
      var c = el.Select(e => Procedural(e, toSun, tint, ground, thick, exposure)).ToArray();
      return $"{C3(c[0])}/{C3(c[1])}/{C3(c[2])}/{C3(c[3])}/{C3(c[4])}/{C3(c[5])}/1/1/0.03";
    }
    return null;
  }

  static double[] Lerp(double[] a, double[] b, double t) => new[] { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t };

  /// Face means (sRGB, 0..~1) of the level's sky cubemap: +X -X +Y -Y +Z -Z (see CubeAvg).
  static double[][] CubeFaces(Ld L, string level) {
    var f = L.File(level); if (f == null) return null;
    foreach (var o in f.Objects) if (o.ClassId == 104) {
      var rs = Env.RsRaw(f, o); var mp = ((int, long))rs["sky"];
      var md = L.Read(f, mp); if (md == null) return null; var (mf, _) = L.Res(f, mp);
      foreach (var x in md.Dct("m_SavedProperties").Lst("m_TexEnvs")) {
        var e = (Dictionary<string, object>)x; if (e.Str("first") != "_Tex") continue;
        var (tf, to) = L.Res(mf, e.Dct("second").Ptr("m_Texture")); if (to == null || to.ClassId != 89) return null;
        var d = L.Read(tf, to); var img = d.G("image data") as byte[]; int per = (int)d.Num("m_CompleteImageSize");
        if (img == null || img.Length < per * 6) return null;
        var faces = new double[6][];
        for (int face = 0; face < 6; face++) {
          int at = face * per + per - 16; int bits = img[at] | (img[at + 1] << 8); if ((bits & 0x1FF) != 0x1FC) return null;
          double R(int k) => (img[at + 8 + k * 2] | (img[at + 9 + k * 2] << 8)) / 65535.0;
          double m = R(3) * 5; faces[face] = new[] { R(0) * m, R(1) * m, R(2) * m };
        }
        return faces;
      }
    }
    return null;
  }

  // ---- Unity's Skybox/Procedural (built-in shader, "Skybox-Procedural.shader"), evaluated for one view ray ----
  const double OuterRadius = 1.025, InnerRadius = 1.0, CameraHeight = 0.0001, Mie = 0.0010, SunBrightness = 20.0, MaxScatter = 50.0;
  const double Scale = 1.0 / (OuterRadius - 1.0), ScaleDepth = 0.25, ScaleOverScaleDepth = (1.0 / (OuterRadius - 1.0)) / 0.25, Samples = 2.0;
  static double ScaleF(double inCos) { double x = 1.0 - inCos; return 0.25 * Math.Exp(-0.00287 + x * (0.459 + x * (3.83 + x * (-6.80 + x * 5.25)))); }
  static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
  static double Gam(double c) { c = Math.Max(0, c); return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055; }

  /// sRGB colour of the procedural sky at an elevation (degrees), linear colour space (as the world renders).
  static double[] Procedural(double elevationDeg, double[] toSun, double[] skyTintGamma, double[] groundGamma, double thickness, double exposure) {
    double rad = elevationDeg * Math.PI / 180; var ray = new[] { 0.0, Math.Sin(rad), Math.Cos(rad) };
    double kRayleigh = 0.0025 * Math.Pow(thickness, 2.5);
    var wl = new double[3]; var invWl = new double[3]; double[] baseWl = { 0.65, 0.57, 0.475 };
    for (int i = 0; i < 3; i++) { wl[i] = (baseWl[i] - 0.15) + (0.3) * (1 - skyTintGamma[i]); invWl[i] = 1.0 / Math.Pow(wl[i], 4); }
    double kKrESun = kRayleigh * SunBrightness, kKr4PI = kRayleigh * 4 * Math.PI, kKmESun = Mie * SunBrightness, kKm4PI = Mie * 4 * Math.PI;
    var cam = new[] { 0.0, InnerRadius + CameraHeight, 0.0 };
    double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    double Len(double[] a) => Math.Sqrt(Dot(a, a));
    var cIn = new double[3]; var cOut = new double[3]; var front = new double[3];
    if (ray[1] >= 0) {
      double far = Math.Sqrt(OuterRadius * OuterRadius + InnerRadius * InnerRadius * ray[1] * ray[1] - InnerRadius * InnerRadius) - InnerRadius * ray[1];
      double height = InnerRadius + CameraHeight; double depth = Math.Exp(ScaleOverScaleDepth * (-CameraHeight));
      double startAngle = Dot(ray, cam) / height; double startOffset = depth * ScaleF(startAngle);
      double sampleLength = far / Samples; double scaledLength = sampleLength * Scale;
      var sampleRay = new[] { ray[0] * sampleLength, ray[1] * sampleLength, ray[2] * sampleLength };
      var sp = new[] { cam[0] + sampleRay[0] * 0.5, cam[1] + sampleRay[1] * 0.5, cam[2] + sampleRay[2] * 0.5 };
      for (int s = 0; s < 2; s++) {
        double h = Len(sp); double dep = Math.Exp(ScaleOverScaleDepth * (InnerRadius - h));
        double lightAngle = Dot(toSun, sp) / h, cameraAngle = Dot(ray, sp) / h;
        double scatter = startOffset + dep * (ScaleF(lightAngle) - ScaleF(cameraAngle));
        for (int i = 0; i < 3; i++) { double att = Math.Exp(-Math.Clamp(scatter, 0, MaxScatter) * (invWl[i] * kKr4PI + kKm4PI)); front[i] += att * (dep * scaledLength); }
        for (int i = 0; i < 3; i++) sp[i] += sampleRay[i];
      }
      for (int i = 0; i < 3; i++) { cIn[i] = front[i] * (invWl[i] * kKrESun); cOut[i] = front[i] * kKmESun; }
      double eyeCos = Dot(toSun, new[] { -ray[0], -ray[1], -ray[2] }); double phase = 0.75 + 0.75 * eyeCos * eyeCos;
      var col = new double[3]; for (int i = 0; i < 3; i++) col[i] = exposure * cIn[i] * phase;
      return new[] { Gam(col[0]), Gam(col[1]), Gam(col[2]) };
    } else {
      double far = (-CameraHeight) / Math.Min(-0.001, ray[1]);
      var pos = new[] { cam[0] + far * ray[0], cam[1] + far * ray[1], cam[2] + far * ray[2] };
      double depth = Math.Exp((-CameraHeight) * (1.0 / ScaleDepth));
      double cameraAngle = Dot(new[] { -ray[0], -ray[1], -ray[2] }, pos), lightAngle = Dot(toSun, pos);
      double cameraScale = ScaleF(cameraAngle), lightScale = ScaleF(lightAngle); double cameraOffset = depth * cameraScale; double temp = lightScale + cameraScale;
      double sampleLength = far, scaledLength = sampleLength * Scale;
      var sp = new[] { cam[0] + ray[0] * sampleLength * 0.5, cam[1] + ray[1] * sampleLength * 0.5, cam[2] + ray[2] * sampleLength * 0.5 };
      double h = Len(sp); double dep = Math.Exp(ScaleOverScaleDepth * (InnerRadius - h)); double scatter = dep * temp - cameraOffset;
      var col = new double[3];
      for (int i = 0; i < 3; i++) {
        double att = Math.Exp(-Math.Clamp(scatter, 0, MaxScatter) * (invWl[i] * kKr4PI + kKm4PI));
        double fr = att * (dep * scaledLength); cIn[i] = fr * (invWl[i] * kKrESun + kKmESun); cOut[i] = Math.Clamp(att, 0, 1);
        col[i] = exposure * (cIn[i] + Lin(groundGamma[i]) * cOut[i]);
      }
      return new[] { Gam(col[0]), Gam(col[1]), Gam(col[2]) };
    }
  }
}
