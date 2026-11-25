// apkscene tree / mat: research views of one Android level (data only).
//   tree <work> <levelN> <nameRe> [max]   subtrees of the GameObjects whose name matches: world position, active flag,
//                                          and per component the fields that matter for lighting (Light, URPLight,
//                                          renderers with their materials, particle systems, other scripts by name)
//   mat  <work> <levelN> <matNameRe>       every material of that name used by the level's renderers: shader, keywords,
//                                          colours, floats and texture slots
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

static class TreeCmd {
  public static void Run(string work, string level, string nameRe, int max) {
    var L = new Ld(work); var m = Model.Load(L, level);
    var re = new Regex(nameRe, RegexOptions.IgnoreCase);
    int shown = 0;
    // roots only: a match whose ancestor also matched is printed inside that ancestor's tree
    var hits = m.Gos.Values.Where(g => re.IsMatch(g.Name)).Select(g => g.Id).ToHashSet();
    foreach (var id in hits) {
      bool inner = false; for (long p = m.Parent(id); p != 0; p = m.Parent(p)) if (hits.Contains(p)) { inner = true; break; }
      if (inner) continue;
      if (shown++ >= max) continue;
      Console.WriteLine("== " + m.PathOf(id));
      Print(L, m, id, 0);
    }
    Console.WriteLine(hits.Count + " matching GameObjects, " + shown + " roots");
  }

  static void Print(Ld L, Model m, long go, int depth) {
    var g = m.Gos[go]; var w = m.GoW(go); string pad = new string(' ', depth * 2);
    Console.WriteLine($"{pad}{g.Name}  pos {w.p} rot {w.r} scale {w.s} {(m.Active(go) ? "" : "(inactive)")} layer {g.Layer}");
    foreach (var (f, o) in g.Comps) {
      var d = L.Read(f, o);
      switch (o.ClassId) {
        case 4: case 224: break;
        case 108:
          if (d == null) { Console.WriteLine(pad + "  Light (no tree)"); break; }
          var sh = d.Dct("m_Shadows");
          Console.WriteLine($"{pad}  Light type {(int)d.Num("m_Type")} en {d.Bool("m_Enabled", true)} col {C(d.Col("m_Color"))} int {d.Num("m_Intensity"):0.###} range {d.Num("m_Range"):0.###} spot {d.Num("m_SpotAngle"):0.#}/{d.Num("m_InnerSpotAngle"):0.#} shadows {sh?.Num("m_Type")} str {sh?.Num("m_Strength"):0.##} lightmapping {d.Num("m_Lightmapping")} bounce {d.Num("m_BounceIntensity"):0.##} mask {d.Dct("m_CullingMask")?.Num("m_Bits")} render {d.Num("m_RenderMode")}");
          break;
        case 23: case 137:
          if (d == null) { Console.WriteLine(pad + "  Renderer (no tree)"); break; }
          var mesh = o.ClassId == 137 ? m.MeshOf(f, d.Ptr("m_Mesh")) : (m.MfMesh.ContainsKey(go) ? MfMesh(L, m, f, go) : null);
          var mats = (d.Lst("m_Materials") ?? new()).Select(x => L.NameOf(f, DX.Ptr(x)) ?? "null");
          Console.WriteLine($"{pad}  {(o.ClassId == 23 ? "MeshRenderer" : "Skinned")} en {d.Bool("m_Enabled", true)} mesh {mesh?.Name ?? "?"} ({mesh?.VCount} v) mats [{string.Join(", ", mats)}] shadows {d.Num("m_CastShadows")} probes {d.Num("m_LightProbeUsage")}");
          break;
        case 198: Console.WriteLine($"{pad}  ParticleSystem"); break;
        case 199: {
          var mats2 = (d?.Lst("m_Materials") ?? new()).Select(x => L.NameOf(f, DX.Ptr(x)) ?? "null");
          Console.WriteLine($"{pad}  ParticleSystemRenderer mats [{string.Join(", ", mats2)}]"); break; }
        case 114: {
          var s = L.Script(f, o, d);
          var fields = d == null ? "" : string.Join(" ", d.Where(kv => kv.Key != "m_GameObject" && kv.Key != "m_Script" && kv.Key != "m_Name" && kv.Key != "m_Enabled" && (kv.Value is float || kv.Value is int || kv.Value is bool || kv.Value is byte || kv.Value is double || kv.Value is string)).Take(14).Select(kv => kv.Key + "=" + (kv.Value is float fl ? Y.F(fl) : kv.Value.ToString())));
          Console.WriteLine($"{pad}  MB {s} {fields}"); break; }
        case 33: break;
        default: Console.WriteLine($"{pad}  {ClassNames.Of(o.ClassId)}"); break;
      }
    }
    var t = m.Trs.GetValueOrDefault(g.Tr);
    if (t != null) foreach (var k in t.Kids) { var kt = m.Trs.GetValueOrDefault(k); if (kt != null && m.Gos.ContainsKey(kt.Go)) Print(L, m, kt.Go, depth + 1); }
  }

  static MeshInfo MfMesh(Ld L, Model m, AF f, long go) {
    var mf = f.ById.GetValueOrDefault(m.MfMesh[go]); var d = L.Read(f, mf); return d == null ? null : m.MeshOf(f, d.Ptr("m_Mesh"));
  }

  static string C(double[] c) => c == null ? "?" : $"({c[0]:0.###},{c[1]:0.###},{c[2]:0.###})";

  public static void Mat(string work, string level, string nameRe) {
    var L = new Ld(work); var m = Model.Load(L, level); var f = m.F;
    var re = new Regex(nameRe, RegexOptions.IgnoreCase); var seen = new HashSet<(string, long)>();
    foreach (var o in f.Objects.Where(x => x.ClassId == 23 || x.ClassId == 137 || x.ClassId == 199)) {
      var d = L.Read(f, o); if (d == null) continue;
      foreach (var mp in d.Lst("m_Materials") ?? new()) {
        var (mf, mo) = L.Res(f, DX.Ptr(mp)); if (mo == null) continue;
        var md = L.Read(mf, mo); var name = md?.Str("m_Name"); if (name == null || !re.IsMatch(name) || !seen.Add((mf.Name, mo.PathId))) continue;
        var shd = L.Read(mf, md.Ptr("m_Shader")); var sname = shd?.Dct("m_ParsedForm")?.Str("m_Name") ?? shd?.Str("m_Name") ?? "?";
        Console.WriteLine($"== {name} ({mf.Name}#{mo.PathId}) shader {sname} queue {md.Num("m_CustomRenderQueue")}");
        var kw = md.Lst("m_ValidKeywords") ?? new(); var kw2 = md.Str("m_ShaderKeywords");
        Console.WriteLine("  keywords " + string.Join(" ", kw.Select(x => x.ToString())) + " " + kw2 + " invalid " + string.Join(" ", (md.Lst("m_InvalidKeywords") ?? new()).Select(x => x.ToString())));
        Console.WriteLine("  flags " + md.Num("m_LightmapFlags") + " gi " + md.Num("m_EnableInstancingVariants"));
        var sp = md.Dct("m_SavedProperties");
        foreach (var x in sp?.Lst("m_Colors") ?? new()) { var p = (Dictionary<string, object>)x; var v = p.Dct("second"); Console.WriteLine($"  col {p.G("first")} ({v.Num("r"):0.####},{v.Num("g"):0.####},{v.Num("b"):0.####},{v.Num("a"):0.####})"); }
        Console.WriteLine("  floats " + string.Join(" ", (sp?.Lst("m_Floats") ?? new()).Select(x => { var p = (Dictionary<string, object>)x; return p.G("first") + "=" + Y.F((float)p.Num("second")); })));
        foreach (var x in sp?.Lst("m_TexEnvs") ?? new()) { var p = (Dictionary<string, object>)x; var v = p.Dct("second"); var tn = L.NameOf(mf, v.Ptr("m_Texture")); if (tn != null) Console.WriteLine($"  tex {p.G("first")} = {tn} scale {v.Vec("m_Scale")}"); }
        var tags = md.Dct("stringTagMap"); if (tags != null) Console.WriteLine("  tags " + string.Join(" ", tags.Select(kv => kv.Key + "=" + kv.Value)));
      }
    }
  }
}

static class ScriptsCmd {
  /// scripts <work> <scriptRe> [fileRe]: every MonoBehaviour in the Android Data files whose script name matches, dumped.
  public static void Run(string work, string scriptRe, string fileRe) {
    var L = new Ld(work); var re = new Regex(scriptRe, RegexOptions.IgnoreCase); var fre = new Regex(fileRe ?? ".", RegexOptions.IgnoreCase);
    int n = 0;
    foreach (var name in L.D.Files.OrderBy(x => x)) {
      if (!fre.IsMatch(name) || name.EndsWith(".resource") || name.EndsWith(".resS")) continue;
      AF f; try { f = L.File(name); } catch { continue; } if (f == null) continue;
      foreach (var o in f.Objects) {
        if (o.ClassId != 114) continue;
        var t = f.TypeOf(o); var sn = L.Bank.ScriptName.GetValueOrDefault(t.ScriptId) ?? "";
        if (!re.IsMatch(sn)) continue;
        var d = L.Read(f, o);
        Console.WriteLine($"== {name}#{o.PathId} {sn} {d?.Str("m_Name")}");
        if (d != null && n++ < 12) Console.WriteLine(Tools.Js(d, 5, 0));
      }
      L.Drop(f);
    }
  }
}

/// Rec Room's own light component (UnityEngine.Rendering.Universal.URPLight, its URP fork) on a light's GameObject, as
/// key=value text for the L records and the env lines (iteration 9): spec=<specularContribution> hemi=1 hin=<inner>
/// hout=<outer> (hemisphere lights only), ispot=<Light.m_InnerSpotAngle> (spots only). Empty when the light has none.
static class UrpKeys {
  public static string Of(Model m, long go, Dictionary<string, object> light) {
    var sb = new System.Text.StringBuilder();
    var u = m.Mbs.FirstOrDefault(x => x.Go == go && x.Script == "UnityEngine.Rendering.Universal.URPLight" && x.D != null);
    if (u != null) {
      sb.Append(" spec=" + Y.F(u.D.Num("specularContribution", 1)));
      if (u.D.Num("isHemisphereLight") != 0) sb.Append(" hemi=1 hin=" + Y.F(u.D.Num("hemisphereLightInnerAngle", 0)) + " hout=" + Y.F(u.D.Num("hemisphereLightOuterAngle", 180)));
    }
    if (light != null && (int)light.Num("m_Type") == 0) sb.Append(" ispot=" + Y.F(light.Num("m_InnerSpotAngle", 21.8)));
    return sb.ToString();
  }
}
