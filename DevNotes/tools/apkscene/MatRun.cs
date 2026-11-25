// apkscene matdesc (2026-11-22): material descriptions for a layout's materials that are not in the PC install.
//   matdesc <work> levelN <layout.rrsk|.bytes> [out=<file>] [tsv=<rows.tsv>] [scene=<name>]
//     - K records: one per '?name' T entry, inserted right after the last T line
//         K <t> col=r,g,b,a [avg=r,g,b] [tex=<install Texture2D>] [tile=sx,sy] [toff=ox,oy] mode=opaque|cutout|fade|add
//           [cut=x] [amask=1] [vc=1] [emit=r,g,b] shader=<android shader>          (or: K <t> col=0.62,0.62,0.64,1 miss=1)
//     - " gcol=r,g,b" appended to every "H grid" line whose mat is a guess (mat.guess=1) or '?name'
//     - " avg=r,g,b itex=0|1" appended to every "G platemat" line whose material is not in the install (inst=0) or
//       whose tex is not an install Texture2D
//     Every other line is kept byte-identical and in order. Re-running first removes what an earlier run added.
//     Without out= nothing is written (dry run: the new records are printed).
//   matcheck <work> <original> <rewritten>   the rewritten file = the original + K lines + gcol/avg/itex keys only
//   matreport <rows.tsv> <out.md>            the human sanity-check report
// Data only: the Android level is read to MEASURE colours (texture means) and read material/shader settings; only names
// and numbers are written.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;

sealed class ShInfo {
  public string Name = "?"; public string[] Kw = new string[0]; public HashSet<string> Props = new();
  public Dictionary<string, object> Pass; public Dictionary<string, string> Tags = new(StringComparer.OrdinalIgnoreCase);
  public List<(string vs, string fs, HashSet<string> kws)> Progs = new(); public HashSet<string> PassKw = new();
}

sealed class MDesc {
  public string Name, Where = "", Shader = "?", ColProp = "-", TexSlot = "-", TexName = "-", TexErr = "", VcWhy = "", EmitWhy = "", Kws = "", Notes = "", Evid = "";
  public bool Found, TexInst, AlphaTest, Amask, Vc, Hide; public int Dups, Uses, TexFmt = -1, TexW, TexH, Queue = -1, Src = -1, Dst = -1;
  public double[] Col = { 1, 1, 1, 1 }, Tile = { 1, 1 }, Toff = { 0, 0 }, Mean, Avg, Emit; public double MeanA = 1, Cut = 0.5, Opa = -1, Glow;
  public TexStat Ts; public bool AmaskSh;
  public string Mode = "opaque";
  public string K(int t) {
    if (!Found) return $"K {t} col=0.62,0.62,0.64,1 miss=1";
    var sb = new StringBuilder($"K {t} col={MatDesc.C(Col, 4)}");
    if (Avg != null) sb.Append(" avg=").Append(MatDesc.C(Avg, 3));
    if (TexInst) sb.Append(" tex=").Append(Y.Enc(TexName));
    if (Mean != null || TexInst) {
      if (Math.Abs(Tile[0] - 1) > 1e-4 || Math.Abs(Tile[1] - 1) > 1e-4) sb.Append(" tile=").Append(Y.F(Tile[0])).Append(',').Append(Y.F(Tile[1]));
      if (Math.Abs(Toff[0]) > 1e-4 || Math.Abs(Toff[1]) > 1e-4) sb.Append(" toff=").Append(Y.F(Toff[0])).Append(',').Append(Y.F(Toff[1]));
    }
    sb.Append(" mode=").Append(Mode);
    if (Hide) { sb.Append(" hide=1 shader=").Append(Y.Enc(Shader)); return sb.ToString(); }
    if (AlphaTest) sb.Append(" cut=").Append(Y.F(Cut));
    if (Opa >= 0 && Opa < 0.9995) sb.Append(" opa=").Append(Y.F(Opa));
    if (Amask) sb.Append(" amask=1");
    if (Vc) sb.Append(" vc=1");
    if (Emit != null) sb.Append(" emit=").Append(MatDesc.C(Emit, 3));
    if (Glow > 0) sb.Append(" glow=").Append(Y.F(Glow));
    sb.Append(" shader=").Append(Y.Enc(Shader));
    return sb.ToString();
  }
}

static class MatDesc {
  public static string C(double[] c, int n) => string.Join(",", c.Take(n).Select(Y.F));
  static string Dec(string s) => s.Replace("%20", " ").Replace("%09", "\t").Replace("%0A", "\n").Replace("%25", "%");
  static readonly HashSet<string> instTex = new(StringComparer.Ordinal); static readonly HashSet<string> instMat = new(StringComparer.Ordinal);
  static void LoadInstall(string work) {
    if (instTex.Count > 0) return;
    foreach (var l in File.ReadLines(Path.Combine(Paths.Idx, "objects.tsv"))) { var p = l.Split('\t'); if (p.Length >= 4 && p[2] == "Texture2D") instTex.Add(p[3]); }
    foreach (var l in File.ReadLines(Path.Combine(work, "inst_materials.tsv"))) { var p = l.Split('\t'); if (p.Length >= 3) instMat.Add(p[2]); }
  }
  public static string ReadText(string path) {
    var raw = File.ReadAllBytes(path);
    if (raw.Length > 2 && raw[0] == 0x1f && raw[1] == 0x8b) { using var gz = new GZipStream(new MemoryStream(raw), CompressionMode.Decompress); using var ms = new MemoryStream(); gz.CopyTo(ms); raw = ms.ToArray(); }
    return new UTF8Encoding(false).GetString(raw);
  }

  // ---------------------------------------------------------------- material lookup in the level
  sealed class MatIdx {
    readonly Ld L; readonly Model m;
    readonly Dictionary<string, Dictionary<(string, long), (AF f, AObj o, int uses)>> used = new(StringComparer.Ordinal);
    Dictionary<string, (AF f, AObj o)> scanMat, scanTex;
    public MatIdx(Ld L, Model m) {
      this.L = L; this.m = m; var f = m.F;
      foreach (var o in f.Objects) {
        if (o.ClassId != 23 && o.ClassId != 137 && o.ClassId != 199) continue;
        var d = L.Read(f, o); if (d == null) continue;
        foreach (var mp in d.Lst("m_Materials") ?? new()) Add(f, DX.Ptr(mp));
      }
      foreach (var o in f.Objects.Where(x => x.ClassId == 218)) { var r = Model.FindPtr(L, f, o, 21); if (r.o != null) Add(r.f, (0, r.o.PathId), r.f); }
    }
    void Add(AF from, (int fid, long pid) p, AF direct = null) {
      var (mf, mo) = direct != null ? (direct, direct.ById.GetValueOrDefault(p.pid)) : L.Res(from, p); if (mo == null || mo.ClassId != 21) return;
      var name = RawName(mf, mo); if (name == null) return;
      if (!used.TryGetValue(name, out var dd)) used[name] = dd = new();
      var k = (mf.Name, mo.PathId); dd[k] = (mf, mo, dd.TryGetValue(k, out var e) ? e.uses + 1 : 1);
    }
    public static string RawName(AF f, AObj o) { int s = f.DataOff + (int)o.Start; int n = BitConverter.ToInt32(f.Data, s); if (n < 0 || n > 1024 || n + 4 > o.Size) return null; return Encoding.UTF8.GetString(f.Data, s + 4, n); }
    IEnumerable<AF> Files() { yield return m.F; foreach (var e in m.F.Ext) { var x = L.File(e.Path); if (x != null) yield return x; } }
    void Scan() {
      if (scanMat != null) return; scanMat = new(StringComparer.Ordinal); scanTex = new(StringComparer.Ordinal);
      foreach (var f in Files()) foreach (var o in f.Objects) {
        if (o.ClassId == 21) { var n = RawName(f, o); if (n != null) scanMat.TryAdd(n, (f, o)); }
        else if (o.ClassId == 28) { var n = RawName(f, o); if (n != null) scanTex.TryAdd(n, (f, o)); }
      }
    }
    public (AF f, AObj o, string where, int dups, int uses) Find(string name) {
      if (used.TryGetValue(name, out var dd)) { var b = dd.Values.OrderByDescending(x => x.uses).First(); return (b.f, b.o, "renderers:" + b.f.Name, dd.Count, b.uses); }
      Scan();
      if (scanMat.TryGetValue(name, out var s)) return (s.f, s.o, "scan:" + s.f.Name, 1, 0);
      var ci = used.Keys.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
      if (ci != null) { var b = used[ci].Values.OrderByDescending(x => x.uses).First(); return (b.f, b.o, "renderers(case):" + b.f.Name, used[ci].Count, b.uses); }
      return (null, null, "", 0, 0);
    }
    /// a texture of the level by name: through the level's materials' texture slots first, else a scan of the level and its externals
    readonly Dictionary<string, (AF f, AObj o)> texByName = new(StringComparer.Ordinal); bool texBuilt;
    public (AF f, AObj o) Tex(string name) {
      if (!texBuilt) {
        texBuilt = true;
        foreach (var dd in used.Values) foreach (var (f, o, _) in dd.Values) {
          var md = L.Read(f, o); foreach (var x in md?.Dct("m_SavedProperties")?.Lst("m_TexEnvs") ?? new()) {
            var tp = ((Dictionary<string, object>)x).Dct("second")?.Ptr("m_Texture") ?? (0, 0); var (tf, to) = L.Res(f, tp); if (to == null || to.ClassId != 28) continue;
            var tn = RawName(tf, to); if (tn != null) texByName.TryAdd(tn, (tf, to));
          }
        }
      }
      if (texByName.TryGetValue(name, out var r)) return r;
      Scan(); return scanTex.TryGetValue(name, out r) ? r : (null, null);
    }
  }

  // ---------------------------------------------------------------- shader analysis (parsed form + GLES3 GLSL)
  static readonly Dictionary<(string, long), ShInfo> shCache = new();
  static readonly string[] Excl = { "SHADOWCASTER", "DEPTHONLY", "DEPTHNORMALS", "DEPTHNORMALSONLY", "META", "MOTIONVECTORS", "UNIVERSAL2D", "SCENESELECTIONPASS", "PICKING", "UNIVERSALGBUFFER" };
  static Dictionary<string, string> Tags(Dictionary<string, object> t) {
    var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var x in t?.Lst("tags") ?? new()) { var p = x as Dictionary<string, object>; var k = p?.G("first")?.ToString(); if (k != null) r[k] = p.G("second")?.ToString() ?? ""; }
    return r;
  }
  static ShInfo Shader(Ld L, AF mf, Dictionary<string, object> md) {
    var (sf, so) = L.Res(mf, md.Ptr("m_Shader")); if (so == null) return null;
    if (shCache.TryGetValue((sf.Name, so.PathId), out var si)) return si;
    si = new ShInfo(); shCache[(sf.Name, so.PathId)] = si;
    var sd = L.Read(sf, so); if (sd == null) return si;
    var pf = sd.Dct("m_ParsedForm"); si.Name = pf?.Str("m_Name") ?? sd.Str("m_Name") ?? "?";
    si.Kw = (pf?.Lst("m_KeywordNames") ?? new()).Select(x => x?.ToString() ?? "").ToArray();
    foreach (var p in pf?.Dct("m_PropInfo")?.Lst("m_Props") ?? new()) { var n = (p as Dictionary<string, object>)?.Str("m_Name"); if (n != null) si.Props.Add(n); }
    Dictionary<string, object> firstPass = null; Dictionary<string, string> firstTags = null;
    foreach (Dictionary<string, object> ss in pf?.Lst("m_SubShaders") ?? new()) {
      var ssTags = Tags(ss.Dct("m_Tags"));
      var cands = new List<(Dictionary<string, object> ps, Dictionary<string, string> tags, bool pref)>();
      foreach (Dictionary<string, object> ps in ss.Lst("m_Passes") ?? new()) {
        var tags = Tags(ps.Dct("m_State")?.Dct("m_Tags")); foreach (var kv in ssTags) tags.TryAdd(kv.Key, kv.Value);
        var lm = (tags.GetValueOrDefault("LIGHTMODE") ?? "").ToUpperInvariant(); var pn = (ps.Dct("m_State")?.Str("m_Name") ?? "").ToUpperInvariant();
        if (Excl.Contains(lm) || Excl.Contains(pn)) continue;
        cands.Add((ps, tags, lm.StartsWith("UNIVERSALFORWARD") || lm == "FORWARDBASE" || lm == "SRPDEFAULTUNLIT"));
      }
      if (cands.Count == 0) continue;
      var pick = cands.FirstOrDefault(c => c.pref); if (pick.ps == null) pick = cands[0];
      if (firstPass == null) { firstPass = pick.ps; firstTags = pick.tags; }
      var progs = ShaderGlsl.Pass(sd, pick.ps);
      if (progs.Count > 0) {
        si.Pass = pick.ps; si.Tags = pick.tags;
        foreach (var (txt, kws) in progs) { int fi = txt.IndexOf("#ifdef FRAGMENT", StringComparison.Ordinal); si.Progs.Add((fi > 0 ? txt.Substring(0, fi) : txt, fi > 0 ? txt.Substring(fi) : "", kws.ToHashSet())); foreach (var k in kws) si.PassKw.Add(k); }
        break;
      }
    }
    if (si.Pass == null) { si.Pass = firstPass; si.Tags = firstTags ?? new(StringComparer.OrdinalIgnoreCase); }
    L.Drop(sf);
    return si;
  }
  static int QueueOf(string tag) {
    if (string.IsNullOrEmpty(tag)) return 2000;
    var m = Regex.Match(tag.Trim(), @"^([A-Za-z]+)\s*([+-]\s*\d+)?$"); if (!m.Success) return int.TryParse(tag, out int q) ? q : 2000;
    int b = m.Groups[1].Value.ToLowerInvariant() switch { "background" => 1000, "geometry" => 2000, "alphatest" => 2450, "transparent" => 3000, "overlay" => 4000, _ => 2000 };
    return b + (m.Groups[2].Success ? int.Parse(m.Groups[2].Value.Replace(" ", "")) : 0);
  }
  static bool Uses(string code, string prop) => Regex.IsMatch(code, @"(?<![A-Za-z0-9_])" + Regex.Escape(prop) + @"\b");
  static bool Samples(string fs, string slot) => Regex.IsMatch(fs, @"sampler2D\s+" + Regex.Escape(slot) + @"\s*;");
  static bool VcRead(string vs, string fs) {
    foreach (Match mm in Regex.Matches(vs, @"(vs_\w+)(\.\w+)?\s*=[^;]*\bin_COLOR0")) { var v = mm.Groups[1].Value; if (Regex.Matches(fs, @"\b" + Regex.Escape(v) + @"\b").Count >= 2) return true; }
    return false;
  }
  static readonly Regex AmaskRe = new(@"(\w+)\.xyz = (\w+)\.www \* \1\.xyz \+ \2\.xyz;", RegexOptions.Compiled);
  static readonly Regex VcName = new(@"vert(ex)?_?colou?r|vercolou?r", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  static readonly string[] ColProps = { "_BaseColor", "_Color", "_MainColor", "_TintColor" };
  static readonly string[] TexSlots = { "_BaseMap", "_MainTex", "_MainTexture", "_Albedo", "_AlbedoMap", "_BaseColorMap", "_Diffuse", "_DiffuseMap", "_Tex" };

  /// Everything about one Android material that the K record needs.
  static MDesc Describe(Ld L, AF mf, AObj mo, string name) {
    var r = new MDesc { Name = name, Found = true };
    var md = L.Read(mf, mo); if (md == null) { r.Found = false; r.Notes = "material unreadable"; return r; }
    var sp = md.Dct("m_SavedProperties");
    var cols = new Dictionary<string, double[]>(); foreach (var x in sp?.Lst("m_Colors") ?? new()) { var p = (Dictionary<string, object>)x; var v = p.Dct("second"); cols[p.G("first")?.ToString() ?? ""] = new[] { v.Num("r"), v.Num("g"), v.Num("b"), v.Num("a", 1) }; }
    var fl = new Dictionary<string, double>(); foreach (var x in sp?.Lst("m_Floats") ?? new()) { var p = (Dictionary<string, object>)x; fl[p.G("first")?.ToString() ?? ""] = p.Num("second"); }
    var tex = new Dictionary<string, ((int, long) ptr, double[] sc, double[] of)>();
    foreach (var x in sp?.Lst("m_TexEnvs") ?? new()) { var p = (Dictionary<string, object>)x; var v = p.Dct("second"); var s = v.Dct("m_Scale"); var o = v.Dct("m_Offset"); tex[p.G("first")?.ToString() ?? ""] = (v.Ptr("m_Texture"), new[] { s?.Num("x", 1) ?? 1, s?.Num("y", 1) ?? 1 }, new[] { o?.Num("x") ?? 0, o?.Num("y") ?? 0 }); }
    var kw = new HashSet<string>((md.Lst("m_ValidKeywords") ?? new()).Select(x => x?.ToString()).Where(x => !string.IsNullOrEmpty(x)));
    foreach (var k in (md.Str("m_ShaderKeywords") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)) kw.Add(k);
    r.Kws = string.Join(" ", kw.OrderBy(x => x));
    var si = Shader(L, mf, md) ?? new ShInfo(); r.Shader = si.Name;
    // the GLES3 variants that match the material's keywords (fewest extra keywords: the base variant(s))
    var progs = si.Progs; List<(string vs, string fs, HashSet<string> kws)> cand = new();
    if (progs.Count > 0) {
      var need = kw.Where(k => si.PassKw.Contains(k)).ToHashSet();
      cand = progs.Where(p => need.IsSubsetOf(p.kws)).ToList(); if (cand.Count == 0) cand = progs;
      int minX = cand.Min(p => p.kws.Count(k => !need.Contains(k))); cand = cand.Where(p => p.kws.Count(k => !need.Contains(k)) == minX).ToList();
    }
    bool G = cand.Count > 0;
    double Vote(Func<(string vs, string fs, HashSet<string> kws), bool> f) => G ? cand.Count(f) / (double)cand.Count : 0;
    // colour: the first of _BaseColor/_Color/_MainColor/_TintColor the material has (and, with GLSL, the shader reads)
    string cp = null;
    if (G) cp = ColProps.FirstOrDefault(c => cols.ContainsKey(c) && Vote(p => Uses(p.fs, c) || Uses(p.vs, c)) >= 0.5);
    cp ??= ColProps.FirstOrDefault(c => cols.ContainsKey(c));
    if (cp != null) { r.ColProp = cp; r.Col = cols[cp]; }
    // main texture slot
    string slot = null;
    if (G) slot = TexSlots.FirstOrDefault(s => tex.TryGetValue(s, out var t) && t.ptr.Item2 != 0 && Vote(p => Samples(p.fs, s) || Samples(p.vs, s)) >= 0.5);
    else slot = TexSlots.FirstOrDefault(s => tex.TryGetValue(s, out var t) && t.ptr.Item2 != 0);
    if (G && slot == null && TexSlots.Any(s => tex.TryGetValue(s, out var t) && t.ptr.Item2 != 0)) r.Notes += "main texture set but not sampled by the shader; ";
    // blend state of the forward pass (property-bound values resolved through the material's floats)
    var st = si.Pass?.Dct("m_State");
    int BV(string key) { var v = st?.Dct("rtBlend0")?.Dct(key); if (v == null) return -1; var nm = v.Str("name"); double val = v.Num("val"); if (nm != null && nm != "<noninit>" && fl.TryGetValue(nm, out var fv)) val = fv; return (int)Math.Round(val); }
    r.Src = BV("srcBlend"); r.Dst = BV("destBlend");
    int cq = (int)md.Num("m_CustomRenderQueue", -1); r.Queue = cq >= 0 ? cq : QueueOf(si.Tags.GetValueOrDefault("QUEUE"));
    var stm = md.Dct("stringTagMap"); string rtype = null; foreach (var kv in stm ?? new()) if (kv.Key == "RenderType") rtype = kv.Value?.ToString();
    rtype ??= si.Tags.GetValueOrDefault("RenderType") ?? "";
    // alpha test: the GLSL's discard when we have it, else keywords / queue / RenderType
    bool atKwShader = si.Kw.Any(k => Regex.IsMatch(k, "ALPHATEST|CUTOUT|ALPHACLIP", RegexOptions.IgnoreCase));
    bool atRule = atKwShader ? kw.Any(k => Regex.IsMatch(k, "ALPHATEST|CUTOUT|ALPHACLIP", RegexOptions.IgnoreCase)) : (r.Queue >= 2450 && r.Queue < 3000) || rtype.Equals("TransparentCutout", StringComparison.OrdinalIgnoreCase) || si.Name.Contains("Cutout", StringComparison.OrdinalIgnoreCase);
    double vDiscard = Vote(p => p.fs.Contains("discard;"));
    r.AlphaTest = G ? vDiscard >= 0.5 : atRule;
    if (G && (vDiscard >= 0.5) != atRule) r.Notes += $"alpha test: glsl discard {vDiscard:0.##} vs keyword/queue rule {atRule}; ";
    if (r.AlphaTest) r.Cut = fl.TryGetValue("_Cutoff", out var cu) ? cu : 0.5;
    // mode from the blend factors (UnityEngine.Rendering.BlendMode: 0 Zero, 1 One, 2 DstColor, 3 SrcColor, 5 SrcAlpha, 10 OneMinusSrcAlpha)
    if (r.Src >= 0 && r.Dst >= 0) {
      if (r.Dst == 1) r.Mode = "add";
      else if (r.Src == 1 && r.Dst == 0) r.Mode = r.AlphaTest ? "cutout" : "opaque";
      else { r.Mode = "fade"; if (!(r.Dst == 10 && (r.Src == 5 || r.Src == 1))) r.Notes += $"blend {r.Src}/{r.Dst} written as fade; "; }
    } else {
      r.Mode = r.Queue >= 3000 ? (Regex.IsMatch(si.Name, "Additive|/Add", RegexOptions.IgnoreCase) ? "add" : "fade") : r.AlphaTest ? "cutout" : "opaque";
      r.Notes += "no blend state, mode from queue; ";
    }
    // vertex colours: the GLSL fragment reads a varying fed from in_COLOR0, or the names say so
    double vVc = Vote(p => VcRead(p.vs, p.fs)); bool nameVc = VcName.IsMatch(name) || VcName.IsMatch(si.Name);
    bool shVc = G ? vVc >= 0.5 : Regex.IsMatch(si.Name, "Particle|VertexLit|VertColor|VertexColor", RegexOptions.IgnoreCase);
    r.Vc = shVc || nameVc; r.VcWhy = (G ? $"glsl {vVc:0.##}" : "noglsl") + (nameVc ? " +name" : "");
    // alpha-masked tint (Rec Room's AG shaders: albedo = lerp(tex, tex * tint, tex.a))
    bool amaskSh = G && Vote(p => AmaskRe.IsMatch(p.fs)) >= 0.5;
    // texture statistics
    TexStat ts = null;
    if (slot != null) {
      var t = tex[slot]; r.TexSlot = slot; r.Tile = t.sc; r.Toff = t.of;
      var (tf, to) = L.Res(mf, t.ptr);
      if (to != null && to.ClassId == 28) {
        ts = TexStat.Of(L, tf, to); r.TexName = ts.Name ?? MatIdxName(tf, to) ?? "?"; r.TexFmt = ts.Fmt; r.TexW = ts.W; r.TexH = ts.H; r.TexErr = ts.Err ?? "";
        r.TexInst = r.TexName != "?" && instTex.Contains(r.TexName);
      } else if (to != null) { r.TexName = MatIdxName(tf, to) ?? "?"; r.TexErr = "slot holds class " + to.ClassId; r.TexInst = instTex.Contains(r.TexName); }
      else r.TexErr = "texture not found";
    }
    if (ts != null && ts.Ok) {
      r.MeanA = ts.MeanA(); r.Mean = ts.Mean(0, false, new double[] { 1, 1, 1 }, false);
      r.Ts = ts; r.AmaskSh = amaskSh;
      r.Amask = amaskSh && r.MeanA < 0.98 && (r.Vc || r.Col.Take(3).Any(v => Math.Abs(v - 1) > 0.002));
      r.Avg = AvgOf(ts, r.Mode, r.AlphaTest, r.Cut, r.Col, amaskSh);
    }
    // splat-blend ground shaders (AG/TerrainSplats: albedo = sum_i splat_i.rgb * control_i * _Splat{i}_Tint): the mean is
    // the control map's channel means weighting the splat textures' means times their tints
    if (r.Avg == null && tex.TryGetValue("_Control0", out var ctl) && ctl.ptr.Item2 != 0 && (!G || Vote(p => Samples(p.fs, "_Control0")) >= 0.5)) {
      var acc = new double[3]; double wsum = 0; var parts = new List<string>();
      for (int c = 0; c < 2; c++) {
        if (!tex.TryGetValue("_Control" + c, out var ct) || ct.ptr.Item2 == 0) continue;
        var (cf, co) = L.Res(mf, ct.ptr); var cs = co != null && co.ClassId == 28 ? TexStat.Of(L, cf, co) : null; if (cs == null || !cs.Ok) continue;
        double n = cs.Cnt.Sum(); var w = new double[4]; for (int a = 0; a < 256; a++) for (int k = 0; k < 3; k++) w[k] += cs.Sum[a, k] / 255.0 / n; w[3] = cs.MeanA();
        for (int k = 0; k < 4; k++) {
          int si2 = c * 4 + k; if (!tex.TryGetValue("_Splat" + si2, out var sp2) || sp2.ptr.Item2 == 0 || w[k] <= 1e-4) continue;
          var (sf2, so2) = L.Res(mf, sp2.ptr); var ss = so2 != null && so2.ClassId == 28 ? TexStat.Of(L, sf2, so2) : null; if (ss == null || !ss.Ok) continue;
          var mean = ss.Mean(0, false, new double[] { 1, 1, 1 }, false); var tint = cols.TryGetValue($"_Splat{si2}_Tint", out var tt) ? tt : new double[] { 1, 1, 1, 1 };
          for (int q = 0; q < 3; q++) acc[q] += w[k] * mean[q] * tint[q]; wsum += w[k]; parts.Add($"{ss.Name}:{w[k]:0.00}");
        }
      }
      if (wsum > 0) { r.Avg = acc.Select(v => Math.Clamp(v / wsum, 0, 1)).ToArray(); r.TexSlot = "_Splat*"; r.Notes += "splat blend " + string.Join(",", parts) + "; "; }
    }
    if (r.Mode == "fade" || r.Mode == "add") r.Opa =Math.Clamp((ts != null && ts.Ok ? r.MeanA : 1) * r.Col[3], 0, 1);
    // emission, as the forward pass computes it:
    //  - _EmissionColor (x the mean of _EmissionMap when the shader samples one: AG/Standard-Opaque-Emission), when the
    //    GLSL reads _EmissionColor (no GLSL: the _EMISSION keyword where the shader has it, else the property);
    //  - Rec Room's AG/Standard glow: + tex.rgb * tex.a * _EmissivePower * tint (main texture), when the GLSL reads
    //    _EmissivePower and it is > 0; written as glow=<power> and folded into emit= as its mean
    var emit = new double[3]; bool anyEmit = false;
    if (cols.TryGetValue("_EmissionColor", out var ec) && Math.Max(ec[0], Math.Max(ec[1], ec[2])) > 0.5 / 255) {
      bool on; if (G) { on = Vote(p => Uses(p.fs, "_EmissionColor") || Uses(p.vs, "_EmissionColor")) >= 0.5; r.EmitWhy = "glsl"; }
      else if (si.Kw.Contains("_EMISSION")) { on = kw.Contains("_EMISSION"); r.EmitWhy = "keyword"; }
      else { on = si.Props.Contains("_EmissionColor"); r.EmitWhy = "property"; }
      if (on) {
        double[] em = { 1, 1, 1 };
        if (G && Vote(p => Samples(p.fs, "_EmissionMap")) >= 0.5 && tex.TryGetValue("_EmissionMap", out var et) && et.ptr.Item2 != 0) {
          var (ef, eo) = L.Res(mf, et.ptr); var es = eo != null && eo.ClassId == 28 ? TexStat.Of(L, ef, eo) : null;
          if (es != null && es.Ok) { em = es.Mean(0, false, new double[] { 1, 1, 1 }, false); r.EmitWhy += $" x _EmissionMap {es.Name} mean"; }
        }
        for (int k = 0; k < 3; k++) emit[k] += ec[k] * em[k]; anyEmit = true;
      } else r.EmitWhy += ":off";
    }
    // other emission colour names that Rec Room's Shader Forge / unlit shaders read (GLSL evidence only)
    if (G) foreach (var en in new[] { "_Emission", "_EmisColor", "_EmissiveColor" }) {
      if (!cols.TryGetValue(en, out var e2) || Math.Max(e2[0], Math.Max(e2[1], e2[2])) <= 0.5 / 255) continue;
      if (Vote(p => Uses(p.fs, en) || Uses(p.vs, en)) < 0.5) continue;
      for (int k = 0; k < 3; k++) emit[k] += e2[k]; anyEmit = true; r.EmitWhy += (r.EmitWhy.Length > 0 ? " + " : "") + "glsl " + en;
    }
    if (G && Vote(p => Uses(p.fs, "_EmissivePower")) >= 0.5 && fl.TryGetValue("_EmissivePower", out var ep) && ep > 0) {
      r.Glow = ep;
      if (ts != null && ts.Ok) {
        double[] g = new double[3]; double n = 0; for (int a = 0; a < 256; a++) { n += ts.Cnt[a]; for (int k = 0; k < 3; k++) g[k] += ts.Sum[a, k] / 255.0 * (a / 255.0); }
        for (int k = 0; k < 3; k++) emit[k] += g[k] / Math.Max(1, n) * ep * r.Col[k]; anyEmit = true; r.EmitWhy += (r.EmitWhy.Length > 0 ? " + " : "") + $"glow tex*a*{Y.F(ep)}*col";
      }
    }
    if (anyEmit && Math.Max(emit[0], Math.Max(emit[1], emit[2])) > 0.5 / 255) r.Emit = emit;
    // a shader that draws nothing (AG/DoNotRender: gl_Position = 0 in every vertex program)
    if (G && Vote(p => Regex.IsMatch(p.vs, @"void main\(\)\s*\{\s*gl_Position = vec4\(0\.0, 0\.0, 0\.0, 0\.0\);\s*return;\s*\}")) >= 0.5) { r.Hide = true; r.Mode = "none"; r.Avg = null; r.Emit = null; r.Notes += "shader draws nothing (gl_Position = 0); "; }
    r.Evid = $"q={r.Queue} blend={r.Src}/{r.Dst} rt={rtype} variants={cand.Count}/{progs.Count} amaskSh={(amaskSh ? 1 : 0)}" + (r.Opa >= 0 ? $" opa={Y.F(r.Opa)}" : "") + (r.Glow > 0 ? $" glow={Y.F(r.Glow)}" : "") + (r.Hide ? " hide=1" : "");
    return r;
  }
  static string MatIdxName(AF f, AObj o) => MatIdx.RawName(f, o);
  /// the flat colour to draw without the texture: the top mip's mean (sRGB space) times the tint; cutout: over the
  /// texels that survive the alpha test; fade: alpha-weighted; amask: tint applied in proportion to the texel alpha
  static double[] AvgOf(TexStat ts, string mode, bool at, double cut, double[] col, bool amask) {
    int minA = at ? (int)Math.Ceiling(Math.Clamp(cut, 0, 1) * 255 - 1e-6) : 0; bool aw = mode == "fade" || mode == "add";
    var a = ts.Mean(minA, aw, col, amask) ?? ts.Mean(0, aw, col, amask);
    return a?.Select(v => Math.Clamp(v, 0, 1)).ToArray();
  }

  // ---------------------------------------------------------------- grounds
  static double[] TerrainColour(Ld L, Model m, string tname, double x0, double z0, double x1, double z1, out string why) {
    why = ""; var f = m.F;
    // a Terrain's TerrainData first; else a TerrainData that only a TerrainCollider uses (then: whole-map coverage)
    foreach (var o in f.Objects.Where(x => x.ClassId == 218).Concat(f.Objects.Where(x => x.ClassId == 154))) {
      var td = Model.FindPtr(L, f, o, 156); if (td.o == null) continue;
      if (MatIdx.RawName(td.f, td.o) != tname) continue;
      long go = BitConverter.ToInt64(f.Data, f.DataOff + (int)o.Start + 4); var w = m.GoW(go);
      var terr = m.Terr.FirstOrDefault(t => t.Name == tname); bool whole = terr == null || terr.Res < 2;
      if (whole) { x0 = z0 = double.NegativeInfinity; x1 = z1 = double.PositiveInfinity; why = "collider-only TerrainData, whole map; "; }
      double sx = whole ? 1 : terr.Scale.x * (terr.Res - 1), sz = whole ? 1 : terr.Scale.z * (terr.Res - 1);
      var B = td.f.Data; int s = td.f.DataOff + (int)td.o.Start;
      int p0 = 4 + ((BitConverter.ToInt32(B, s) + 3) & ~3); int nl = BitConverter.ToInt32(B, s + p0); if (nl <= 0 || nl > 32) { why = "no layers"; return null; }
      var means = new List<double[]>(); var lnames = new List<string>();
      for (int i = 0; i < nl; i++) {
        var lr = L.Res(td.f, (BitConverter.ToInt32(B, s + p0 + 4 + i * 12), BitConverter.ToInt64(B, s + p0 + 8 + i * 12))); double[] mean = null; string tn = "?";
        // TerrainLayer (no PC type tree): m_Name, m_DiffuseTexture, m_NormalMapTexture, m_MaskMapTexture (PPtrs), m_TileSize,
        // m_TileOffset, m_Specular, m_Metallic, m_Smoothness, m_NormalScale, m_DiffuseRemapMin, m_DiffuseRemapMax, ...
        // The terrain shader multiplies the (linear) diffuse by the remap scale (max - min; min is 0 in every level here):
        // Rec Room's frontier layers are grey textures tinted this way.
        if (lr.o != null) {
          var LB = lr.f.Data; int ls = lr.f.DataOff + (int)lr.o.Start; int q0 = 4 + ((BitConverter.ToInt32(LB, ls) + 3) & ~3);
          var tx = q0 + 12 <= lr.o.Size ? L.Res(lr.f, (BitConverter.ToInt32(LB, ls + q0), BitConverter.ToInt64(LB, ls + q0 + 4))) : (null, null);
          if (tx.o != null && tx.o.ClassId == 28) {
            var ts = TexStat.Of(L, tx.f, tx.o); tn = ts.Name ?? "?";
            if (ts.Ok) {
              mean = ts.Mean(0, false, new double[] { 1, 1, 1 }, false);
              if (q0 + 36 + 112 <= lr.o.Size) {
                var sc = Enumerable.Range(0, 3).Select(k => BitConverter.ToSingle(LB, ls + q0 + 96 + k * 4) - BitConverter.ToSingle(LB, ls + q0 + 80 + k * 4)).ToArray();
                if (sc.All(v => v >= 0 && v <= 8) && sc.Any(v => Math.Abs(v - 1) > 1e-3)) { mean = mean.Select((v, k) => Gam(Lin(v) * sc[k])).ToArray(); tn += $"(x{string.Join(",", sc.Select(v => Y.F(v)))})"; }
              }
            }
          }
        }
        means.Add(mean); lnames.Add(tn);
      }
      // splat maps: m_AlphaTextures follows the layer list
      int q = p0 + 4 + nl * 12; int na = BitConverter.ToInt32(B, s + q); var cov = new double[nl]; bool got = false;
      if (na > 0 && na <= 8) for (int a = 0; a < na; a++) {
        var ar = L.Res(td.f, (BitConverter.ToInt32(B, s + q + 4 + a * 12), BitConverter.ToInt64(B, s + q + 8 + a * 12))); if (ar.o == null || ar.o.ClassId != 28) continue;
        var px = TexStat.Pixels(L, ar.f, ar.o, out int aw, out int ah); if (px == null) continue;
        double[] Acc(bool region) {
          var c = new double[4]; int n = 0;
          for (int j = 0; j < ah; j++) for (int i = 0; i < aw; i++) {
            double wx = w.p.x + (i + 0.5) / aw * sx, wz = w.p.z + (j + 0.5) / ah * sz;
            if (region && (wx < x0 || wx > x1 || wz < z0 || wz > z1)) continue;
            int k = (j * aw + i) * 4; for (int ch = 0; ch < 4; ch++) c[ch] += px[k + ch] / 255.0; n++;
          }
          return n > 0 ? c : null;
        }
        var cc = Acc(true) ?? Acc(false); if (cc == null) continue; got = true;
        for (int ch = 0; ch < 4; ch++) if (a * 4 + ch < nl) cov[a * 4 + ch] += cc[ch];
      }
      double[] res = new double[3]; double tw = 0;
      if (got) for (int i = 0; i < nl; i++) { if (means[i] == null || cov[i] <= 0) continue; for (int c = 0; c < 3; c++) res[c] += means[i][c] * cov[i]; tw += cov[i]; }
      if (tw > 0) { double tot = cov.Sum(); why += "splat " + string.Join(",", Enumerable.Range(0, nl).Select(i => $"{lnames[i]}:{cov[i] / tot:0.00}")); return res.Select(v => v / tw).ToArray(); }
      var first = means.FirstOrDefault(x => x != null); why += first != null ? "first layer (no splat map read) " + lnames[means.IndexOf(first)] : "no layer texture decoded";
      return first;
    }
    // collider-only grids added later by the Disc Golf tools are named "<terrain>Collider": same ground
    if (tname.EndsWith("Collider") && tname.Length > 8) { var r2 = TerrainColour(L, m, tname.Substring(0, tname.Length - 8), x0, z0, x1, z1, out var w2); why = "as " + tname.Substring(0, tname.Length - 8) + ": " + w2; return r2; }
    why = "terrain " + tname + " not found"; return null;
  }
  /// mean vertex colour (area-weighted over up-facing triangles of submesh `sub`, or all) of a level mesh; null when it has none
  static double[] MeshVc(Ld L, MeshInfo mi, TRS w, int sub) {
    try {
      var f = L.File(mi.File); var d = L.Read(f, f.ById[mi.Pid]); var vd = d.Dct("m_VertexData"); int n = (int)vd.Num("m_VertexCount");
      var chans = vd.Lst("m_Channels").Cast<Dictionary<string, object>>().Select(x => (s: (int)x.Num("stream"), o: (int)x.Num("offset"), fmt: (int)x.Num("format"), dim: (int)x.Num("dimension") & 15)).ToList();
      if (chans.Count <= 3 || chans[3].dim < 3) return null;
      byte[] data = vd.G("m_DataSize") as byte[];
      if (data == null || data.Length == 0) { var sd = d.Dct("m_StreamData"); var path = Ld.Norm(sd?.Str("path")); long off = (long)(sd?.Num("offset") ?? 0), size = (long)(sd?.Num("size") ?? 0); if (size <= 0 || path == null || !L.D.Has(path)) return null; var all = L.D.Get(path); data = new byte[size]; Buffer.BlockCopy(all, (int)off, data, 0, (int)size); }
      int FS(int fm) => fm switch { 0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 2, 5 => 2, 6 => 1, 7 => 1, 8 => 2, 9 => 2, 10 => 4, 11 => 4, _ => 4 };
      int maxS = chans.Where(x => x.dim > 0).Max(x => x.s); var sOff = new int[maxS + 1]; var sStride = new int[maxS + 1]; int o2 = 0;
      for (int s = 0; s <= maxS; s++) { sOff[s] = o2; sStride[s] = chans.Where(x => x.s == s && x.dim > 0).Select(x => x.o + FS(x.fmt) * x.dim).DefaultIfEmpty(0).Max(); o2 += sStride[s] * n; o2 = (o2 + 15) & ~15; }
      double Rd(int fm, int at) => fm switch { 0 => BitConverter.ToSingle(data, at), 1 => (double)BitConverter.ToHalf(data, at), 2 => data[at] / 255.0, 3 => (sbyte)data[at] / 127.0, 4 => BitConverter.ToUInt16(data, at) / 65535.0, _ => 0 };
      var p = chans[0]; var c = chans[3]; if (p.fmt > 1) return null;
      V3 Pos(int i) { int b = sOff[p.s] + i * sStride[p.s] + p.o; var v = new V3(Rd(p.fmt, b), Rd(p.fmt, b + FS(p.fmt)), Rd(p.fmt, b + 2 * FS(p.fmt))); return w.p + w.r.Rot(w.s.Mul(v)); }
      double[] Col(int i) { int b = sOff[c.s] + i * sStride[c.s] + c.o; var r = new double[4]; for (int k = 0; k < Math.Min(4, c.dim); k++) r[k] = Rd(c.fmt, b + k * FS(c.fmt)); if (c.dim < 4) r[3] = 1; return r; }
      var ib = d.G("m_IndexBuffer") as byte[]; bool i32 = d.Num("m_IndexFormat") == 1; var subs = d.Lst("m_SubMeshes").Cast<Dictionary<string, object>>().ToList();
      double[] acc = new double[3]; double ta = 0;
      for (int si = 0; si < subs.Count; si++) {
        if (sub >= 0 && sub < subs.Count && si != sub) continue; var sx = subs[si]; if (sx.Num("topology") != 0) continue;
        int fb = (int)sx.Num("firstByte"), ic = (int)sx.Num("indexCount"), bv = (int)sx.Num("baseVertex");
        int Ix(int k) => (i32 ? BitConverter.ToInt32(ib, fb + k * 4) : BitConverter.ToUInt16(ib, fb + k * 2)) + bv;
        for (int k = 0; k + 2 < ic; k += 3) {
          int a = Ix(k), b = Ix(k + 1), e = Ix(k + 2); var pa = Pos(a); var nn = Shape.Cross(Pos(b) - pa, Pos(e) - pa); double ar = nn.Len(); if (ar <= 0 || Math.Abs(nn.y) / ar < 0.7) continue;
          var ca = Col(a); var cb = Col(b); var ce = Col(e); for (int ch = 0; ch < 3; ch++) acc[ch] += ar * (ca[ch] + cb[ch] + ce[ch]) / 3; ta += ar;
        }
      }
      return ta > 0 ? acc.Select(v => v / ta).ToArray() : null;
    } catch { return null; }
  }
  static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
  static double Gam(double c) { c = Math.Clamp(c, 0, 1); return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055; }

  // ---------------------------------------------------------------- the command
  public static void Run(string work, string level, string layout, string[] opt) {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    string outPath = opt.FirstOrDefault(o => o.StartsWith("out="))?.Substring(4), tsv = opt.FirstOrDefault(o => o.StartsWith("tsv="))?.Substring(4);
    string scene = opt.FirstOrDefault(o => o.StartsWith("scene="))?.Substring(6) ?? Regex.Replace(Path.GetFileName(layout), @"\.(rrsk|bytes|txt)$", "");
    LoadInstall(work);
    var text = ReadText(layout); var lines = text.Split('\n').ToList();
    // undo an earlier run: K lines, gcol on H grid lines, avg/itex on G platemat lines
    int removed = lines.RemoveAll(l => l.StartsWith("K "));
    for (int i = 0; i < lines.Count; i++) {
      if (lines[i].StartsWith("H grid ")) lines[i] = Regex.Replace(lines[i], @" gcol=\S*", "");
      else if (lines[i].StartsWith("G platemat ")) lines[i] = Regex.Replace(lines[i], @" (avg|itex)=\S*", "");
    }
    var L = new Ld(work); var m = Model.Load(L, level); var mx = new MatIdx(L, m);
    var rows = new List<string>(); var descCache = new Dictionary<string, MDesc>(StringComparer.Ordinal);
    MDesc DescOf(string name) {
      if (descCache.TryGetValue(name, out var d)) return d;
      var (f, o, where, dups, uses) = mx.Find(name);
      d = o == null ? new MDesc { Name = name, Found = false } : Describe(L, f, o, name);
      d.Where = where; d.Dups = dups; d.Uses = uses; descCache[name] = d; return d;
    }
    string Row(string kind, string id, MDesc d, string extra) =>
      string.Join("\t", kind, scene, level, id, d.Name, d.Found ? 1 : 0, d.Where, d.Dups, d.Shader, d.Mode, d.AlphaTest ? Y.F(d.Cut) : "", d.ColProp, C(d.Col, 4), d.TexSlot, d.TexName,
        d.TexFmt, d.TexW + "x" + d.TexH, d.TexInst ? 1 : 0, C(d.Tile, 2), d.Mean == null ? "" : C(d.Mean, 3), Y.F(d.MeanA), d.Avg == null ? "" : C(d.Avg, 3), d.Vc ? 1 : 0, d.VcWhy, d.Amask ? 1 : 0,
        d.Emit == null ? "" : C(d.Emit, 3), d.EmitWhy, d.Evid, d.Kws, (d.TexErr + " " + d.Notes).Trim(), extra);
    // K records
    int lastT = -1; var kl = new List<string>(); int nQ = 0, nMiss = 0, nTex = 0, nAvg = 0;
    for (int i = 0; i < lines.Count; i++) {
      if (!lines[i].StartsWith("T ")) continue; lastT = i;
      var p = lines[i].Split(' ', 3); if (p.Length < 3 || !p[2].StartsWith("?") || !int.TryParse(p[1], out int t)) continue;
      nQ++; var name = Dec(p[2].Substring(1)); var d = name.Length == 0 || name == "none" ? new MDesc { Name = name, Found = false } : DescOf(name);
      kl.Add(d.K(t)); if (!d.Found) nMiss++; if (d.TexInst) nTex++; if (d.Avg != null) nAvg++;
      rows.Add(Row("K", t.ToString(), d, ""));
    }
    // H grid gcol
    int nG = 0, nGc = 0;
    for (int i = 0; i < lines.Count; i++) {
      var l = lines[i]; if (!l.StartsWith("H grid ")) continue;
      var tok = l.Split(' '); var kv = tok.Skip(10).Select(x => x.Split('=', 2)).Where(x => x.Length == 2).GroupBy(x => x[0]).ToDictionary(g => g.Key, g => g.First()[1]);
      var mat = kv.GetValueOrDefault("mat") ?? ""; bool guess = kv.GetValueOrDefault("mat.guess") == "1";
      if (!guess && !(mat.StartsWith("?") && mat.Length > 1)) continue;
      nG++;
      double ox = double.Parse(tok[3], CultureInfo.InvariantCulture), oz = double.Parse(tok[5], CultureInfo.InvariantCulture), dx = double.Parse(tok[6], CultureInfo.InvariantCulture), dz = double.Parse(tok[7], CultureInfo.InvariantCulture);
      int nx = int.Parse(tok[8]), nz = int.Parse(tok[9]);
      var gid = Dec(Regex.Replace(Regex.Replace(tok[2], @"#\d+$", ""), @"~far$", ""));
      double[] gc = null; string why = ""; MDesc gd = null;
      if (kv.GetValueOrDefault("src") == "terrain") gc = TerrainColour(L, m, gid, ox, oz, ox + (nx - 1) * dx, oz + (nz - 1) * dz, out why);
      else {
        var mname = Dec(mat.StartsWith("?") ? mat.Substring(1) : mat); gd = DescOf(mname);
        var baseC = gd.Avg ?? (gd.Found ? gd.Col.Take(3).ToArray() : null); why = gd.Avg != null ? "material avg" : gd.Found ? "material col" : "material not found";
        if (baseC != null && gd.Vc) {
          var go = Dec(kv.GetValueOrDefault("go") ?? "");
          var rr = m.Rend.FirstOrDefault(r => r.Mesh != null && r.Mesh.Name == gid && m.Name(r.Go) == go) ?? m.Rend.FirstOrDefault(r => r.Mesh != null && r.Mesh.Name == gid);
          if (rr != null) {
            int slot = rr.Mats.FindIndex(x => x.name == mname); var vc = MeshVc(L, rr.Mesh, m.GoW(rr.Go), slot);
            if (vc != null) {
              // the vertex colour is part of the tint (AG: tint = vertex colour * col, applied as lerp(tex, tex * tint, tex.a));
              // it is a raw (linear) factor, so in sRGB space it scales by Gam(vc)
              var tint = Enumerable.Range(0, 3).Select(k => gd.Col[k] * Gam(vc[k])).ToArray();
              if (gd.Ts != null && gd.Ts.Ok) baseC = AvgOf(gd.Ts, gd.Mode, gd.AlphaTest, gd.Cut, tint, gd.AmaskSh) ?? baseC; else baseC = baseC.Select((v, k) => v * Gam(vc[k])).ToArray();
              why += $" with vertex colour {C(vc, 3)} (linear) in the tint" + (gd.AmaskSh ? " (alpha-masked)" : "");
            }
          }
        }
        gc = baseC;
      }
      if (gc == null) { rows.Add(Row("H", tok[2], gd ?? new MDesc { Name = mat }, "no gcol: " + why)); continue; }
      nGc++; lines[i] = l + " gcol=" + C(gc.Select(v => Math.Clamp(v, 0, 1)).ToArray(), 3);
      rows.Add(Row("H", tok[2], gd ?? new MDesc { Name = mat, Found = true, Avg = gc }, $"gcol={C(gc, 3)} {why}"));
    }
    // G platemat avg / itex
    int nP = 0, nPa = 0;
    for (int i = 0; i < lines.Count; i++) {
      var l = lines[i]; if (!l.StartsWith("G platemat ")) continue;
      var tok = l.Split(' '); var kv = tok.Skip(3).Select(x => x.Split('=', 2)).Where(x => x.Length == 2).GroupBy(x => x[0]).ToDictionary(g => g.Key, g => g.First()[1]);
      var mname = Dec(tok[2]); var tn = Dec(kv.GetValueOrDefault("tex") ?? ""); bool inst = kv.GetValueOrDefault("inst") == "1"; bool itex = tn.Length > 0 && instTex.Contains(tn);
      if (inst && itex) continue;
      nP++;
      var col = (kv.GetValueOrDefault("col") ?? "1,1,1,1").Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
      var d = DescOf(mname); double[] avg = null; string why;
      var (tf, to) = tn.Length > 0 && tn != "-" && tn != "?" ? mx.Tex(tn) : (null, null);
      if (to != null) {
        var ts = TexStat.Of(L, tf, to);
        if (ts.Ok) { bool am = d.Found && d.Evid.Contains("amaskSh=1"); avg = AvgOf(ts, d.Found ? d.Mode : "opaque", d.Found && d.AlphaTest, d.Cut, col, am); why = $"texture {tn} {ts.W}x{ts.H} fmt {ts.Fmt}" + (d.Found ? $" mode {d.Mode}" + (am ? " amask" : "") : " (material not found: opaque)"); }
        else why = "texture " + tn + ": " + ts.Err;
      } else if (tn.Length == 0 || tn == "-" || tn == "?") { avg = col.Take(3).ToArray(); why = "no texture: col"; }
      else why = "texture " + tn + " not found in the level";
      if (avg == null) { lines[i] = l + " itex=" + (itex ? 1 : 0); rows.Add(Row("P", mname, d, $"tex={tn} itex={(itex ? 1 : 0)} no avg: {why}")); continue; }
      nPa++; lines[i] = l + " avg=" + C(avg, 3) + " itex=" + (itex ? 1 : 0);
      rows.Add(Row("P", mname, d, $"tex={tn} itex={(itex ? 1 : 0)} avg={C(avg, 3)} {why}"));
    }
    if (lastT >= 0) lines.InsertRange(lastT + 1, kl);
    var result = string.Join("\n", lines);
    var summary = $"{scene} ({level}): ?T {nQ} described {nQ - nMiss} (avg {nAvg}, install tex {nTex}) missing {nMiss}; H grids {nG} gcol {nGc}; platemats {nP} avg {nPa}; removed old K {removed} [{sw.Elapsed.TotalSeconds:F1} s]";
    if (outPath != null) File.WriteAllBytes(outPath, new UTF8Encoding(false).GetBytes(result));
    else { foreach (var k in kl) Console.WriteLine(k); foreach (var l in lines.Where(x => x.Contains(" gcol=") || (x.StartsWith("G platemat") && x.Contains(" itex=")))) Console.WriteLine(l.Length > 300 ? l.Substring(0, 300) + "..." : l); }
    if (tsv != null) {
      if (!File.Exists(tsv)) File.WriteAllText(tsv, "kind\tscene\tlevel\tid\tname\tfound\twhere\tdups\tshader\tmode\tcut\tcolprop\tcol\ttexslot\ttex\tfmt\tsize\ttexinst\ttile\tmean\tmeanA\tavg\tvc\tvcwhy\tamask\temit\temitwhy\tevid\tkws\tnotes\textra\n");
      File.AppendAllLines(tsv, rows);
    }
    Console.WriteLine(summary);
  }

  // ---------------------------------------------------------------- verification
  public static int Check(string orig, string neu) {
    var a = ReadText(orig).Split('\n'); var b = ReadText(neu).Split('\n').ToList();
    var qT = a.Where(l => l.StartsWith("T ") && l.Split(' ', 3).Length == 3 && l.Split(' ', 3)[2].StartsWith("?")).Select(l => l.Split(' ')[1]).ToHashSet();
    var kLines = b.Where(l => l.StartsWith("K ")).ToList(); int bad = 0;
    foreach (var k in kLines) if (!qT.Contains(k.Split(' ')[1])) { Console.WriteLine("K for a non-? T entry: " + k); bad++; }
    if (kLines.Select(k => k.Split(' ')[1]).Distinct().Count() != kLines.Count) { Console.WriteLine("duplicate K"); bad++; }
    if (qT.Count != kLines.Count) { Console.WriteLine($"? T entries {qT.Count} but K lines {kLines.Count}"); bad++; }
    // K lines must sit right after the last T line
    int lastT = b.FindLastIndex(l => l.StartsWith("T ")); if (kLines.Count > 0 && (lastT < 0 || !b.Skip(lastT + 1).Take(kLines.Count).All(l => l.StartsWith("K ")))) { Console.WriteLine("K lines not right after the last T line"); bad++; }
    var stripped = b.Where(l => !l.StartsWith("K ")).Select(l => l.StartsWith("H grid ") ? Regex.Replace(l, @" gcol=[0-9.]+,[0-9.]+,[0-9.]+$", "") : l.StartsWith("G platemat ") ? Regex.Replace(l, @"( avg=[0-9.]+,[0-9.]+,[0-9.]+)? itex=[01]$", "") : l).ToList();
    if (stripped.Count != a.Length) { Console.WriteLine($"line count {stripped.Count} vs {a.Length}"); bad++; }
    else for (int i = 0; i < a.Length; i++) if (stripped[i] != a[i]) { if (bad++ < 5) Console.WriteLine($"line {i + 1} differs:\n  - {a[i]}\n  + {stripped[i]}"); }
    int g = b.Count(l => l.StartsWith("H grid ") && l.Contains(" gcol=")), p = b.Count(l => l.StartsWith("G platemat ") && l.Contains(" itex="));
    Console.WriteLine($"{(bad == 0 ? "OK" : "FAIL")} {Path.GetFileName(neu)}: {a.Length} original lines kept; +{kLines.Count} K, {g} gcol, {p} platemat keys; {bad} problems");
    return bad == 0 ? 0 : 1;
  }

  // ---------------------------------------------------------------- report
  public static void Report(string tsv, string outMd) {
    var hdr = File.ReadLines(tsv).First().Split('\t'); int I(string c) => Array.IndexOf(hdr, c);
    var rows = File.ReadLines(tsv).Skip(1).Select(l => l.Split('\t')).Where(r => r.Length >= hdr.Length).ToList();
    var sb = new StringBuilder();
    var K = rows.Where(r => r[0] == "K").ToList();
    sb.Append("## Per layout\n\n| layout | level | ? T entries | described | avg (texture mean) | with install tex | colour only | missing | cutout | fade | add | vc=1 | emit | H gcol | platemat avg |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
    foreach (var g in rows.GroupBy(r => r[I("scene")]).OrderBy(g => g.Key, StringComparer.Ordinal)) {
      var k = g.Where(r => r[0] == "K").ToList(); var found = k.Where(r => r[I("found")] == "1").ToList();
      int hg = g.Count(r => r[0] == "H" && r[I("extra")].StartsWith("gcol=")), ht = g.Count(r => r[0] == "H"); int pa = g.Count(r => r[0] == "P" && r[I("extra")].Contains(" avg=")), pt = g.Count(r => r[0] == "P");
      sb.Append($"| {g.Key} | {g.First()[I("level")]} | {k.Count} | {found.Count} | {found.Count(r => r[I("avg")] != "")} | {found.Count(r => r[I("texinst")] == "1")} | {found.Count(r => r[I("avg")] == "")} | {k.Count - found.Count} | {found.Count(r => r[I("mode")] == "cutout")} | {found.Count(r => r[I("mode")] == "fade")} | {found.Count(r => r[I("mode")] == "add")} | {found.Count(r => r[I("vc")] == "1")} | {found.Count(r => r[I("emit")] != "")} | {hg}/{ht} | {pa}/{pt} |\n");
    }
    var kf = K.Where(r => r[I("found")] == "1").ToList();
    sb.Append($"\nTotals: {K.Count} '?' T entries; {kf.Count} described ({kf.Count(r => r[I("avg")] != "")} with a texture-mean avg, {kf.Count(r => r[I("texinst")] == "1")} with an install tex=, {kf.Count(r => r[I("avg")] == "")} colour only); {K.Count - kf.Count} missing (miss=1). Modes: " +
      string.Join(", ", kf.GroupBy(r => r[I("mode")]).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count())) + $"; vc=1 {kf.Count(r => r[I("vc")] == "1")}; emit {kf.Count(r => r[I("emit")] != "")}; amask {kf.Count(r => r[I("amask")] == "1")}.\n\n");
    sb.Append("## The '?' materials, most common first (one row per material name; values from its first layout)\n\n| material | layouts | shader | mode | cut | col | tex (install?) | size fmt | tile | texture mean | avg | vc | emit | notes |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
    foreach (var g in K.GroupBy(r => r[I("name")]).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)) {
      var r = g.First(); var varies = g.Select(x => x[I("avg")] + "|" + x[I("col")]).Distinct().Count() > 1 ? " (differs between levels)" : "";
      if (r[I("found")] != "1") { sb.Append($"| {r[I("name")]} | {g.Count()} | - | MISSING | | | | | | | | | | not in the level |\n"); continue; }
      sb.Append($"| {r[I("name")]} | {g.Count()} | {r[I("shader")]} | {r[I("mode")]} | {r[I("cut")]} | {r[I("col")]} | {r[I("tex")]} ({(r[I("texinst")] == "1" ? "yes" : "no")}) | {r[I("size")]} {r[I("fmt")]} | {r[I("tile")]} | {r[I("mean")]} | {r[I("avg")]} | {(r[I("vc")] == "1" ? "1" : "")} {r[I("vcwhy")]} | {r[I("emit")]} | {r[I("notes")]}{varies} |\n");
    }
    sb.Append("\n## H grid ground colours (gcol)\n\n| layout | grid | material | gcol / why |\n|---|---|---|---|\n");
    foreach (var r in rows.Where(r => r[0] == "H")) sb.Append($"| {r[I("scene")]} | {r[I("id")]} | {r[I("name")]} | {r[I("extra")]} |\n");
    var P = rows.Where(r => r[0] == "P").ToList();
    sb.Append($"\n## G platemat (plate hull materials not in the install or with a non-install texture)\n\n{P.Count} lines; {P.Count(r => r[I("extra")].Contains(" avg="))} got avg=; {P.Count(r => r[I("extra")].Contains("itex=1"))} have an install texture (itex=1).\n\n| material | lines | tex | itex | avg | how |\n|---|---|---|---|---|---|\n");
    foreach (var g in P.GroupBy(r => r[I("id")]).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)) {
      var e = g.First()[I("extra")]; var tex = Regex.Match(e, @"tex=(\S*)").Groups[1].Value; var it = Regex.Match(e, @"itex=(\d)").Groups[1].Value; var av = Regex.Match(e, @"avg=(\S+)").Groups[1].Value; var how = Regex.Replace(e, @"^tex=\S* itex=\d (avg=\S+ )?", "");
      sb.Append($"| {g.Key} | {g.Count()} | {tex} | {it} | {av} | {how}{(g.Select(x => Regex.Match(x[I("extra")], @"avg=(\S+)").Groups[1].Value).Distinct().Count() > 1 ? " (differs between levels)" : "")} |\n");
    }
    File.WriteAllText(outMd, sb.ToString());
    Console.WriteLine($"report: {rows.Count} rows -> {outMd}");
  }
}
