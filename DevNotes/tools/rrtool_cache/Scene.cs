// "world" = every GameObject of a scene file with WORLD transforms, renderer mesh/material names, world AABBs, lights.
//   Static-batched renderers point at "Combined Mesh (root: scene)" whose vertices are already in world space, so their
//   world AABB is the union of the combined mesh's submesh AABBs [firstSubMesh, +subMeshCount); the original mesh name
//   is lost there, but a MeshCollider (if any) still names the original mesh ("col:").
// "mats"  = every Material of a serialized file: shader, colours, float props, textures.
using System.Globalization;
using System.Text;
static class SceneDump {
  static readonly CultureInfo IC = CultureInfo.InvariantCulture;
  static string F(double v) => v.ToString("0.###", IC);
  static Dictionary<string, object> Dx(object o) => o as Dictionary<string, object>;
  static double N(object o) => Convert.ToDouble(o, IC);
  static string Base(string b) => b.Split('#')[0];
  // 4x4 row-major TRS
  static double[] TRS(float[] p, float[] q, float[] s) {
    double x = q[0], y = q[1], z = q[2], w = q[3];
    double[] r = {
      1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w),
      2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w),
      2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) };
    return new double[] { r[0] * s[0], r[1] * s[1], r[2] * s[2], p[0], r[3] * s[0], r[4] * s[1], r[5] * s[2], p[1], r[6] * s[0], r[7] * s[1], r[8] * s[2], p[2], 0, 0, 0, 1 };
  }
  static double[] Mul(double[] a, double[] b) { var m = new double[16]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { double s = 0; for (int k = 0; k < 4; k++) s += a[i * 4 + k] * b[k * 4 + j]; m[i * 4 + j] = s; } return m; }
  static double[] P(double[] m, double x, double y, double z) => new[] { m[0] * x + m[1] * y + m[2] * z + m[3], m[4] * x + m[5] * y + m[6] * z + m[7], m[8] * x + m[9] * y + m[10] * z + m[11] };
  class MI { public string Name; public double[] C, E; public List<double[]> Subs = new(); }
  static readonly Dictionary<(string, long), MI> MeshCache = new();
  static MI MeshInfo(string bundle, SFile f, int fid, long pid) {
    var (b, p) = Names.Target(bundle, f, fid, pid);
    if (fid == 0) b = bundle;
    if (MeshCache.TryGetValue((b, p), out var mi)) return mi;
    mi = new MI { Name = "?" + b + "#" + p };
    try {
      var mf = Db.File0(b); var o = Db.Obj(mf, p);
      if (o != null) {
        var r = Raw.At(mf, o); mi.Name = Raw.Str(r) + (Base(b) == Base(bundle) ? "" : "@" + b);
        var d = Dx(Ser.Read(mf, o)); var aabb = Dx(d["m_LocalAABB"]); var c = Dx(aabb["m_Center"]); var e = Dx(aabb["m_Extent"]);
        mi.C = new[] { N(c["x"]), N(c["y"]), N(c["z"]) }; mi.E = new[] { N(e["x"]), N(e["y"]), N(e["z"]) };
        foreach (var sm in (d["m_SubMeshes"] as List<object>)) { var la = Dx(Dx(sm)["localAABB"]); var sc = Dx(la["m_Center"]); var se = Dx(la["m_Extent"]); mi.Subs.Add(new[] { N(sc["x"]), N(sc["y"]), N(sc["z"]), N(se["x"]), N(se["y"]), N(se["z"]) }); }
      }
    } catch (Exception ex) { mi.Name = "?err " + ex.Message; }
    MeshCache[(b, p)] = mi; return mi;
  }
  static string NameOf(string bundle, SFile f, int fid, long pid) {
    if (pid == 0) return "null";
    var (b, p) = Names.Target(bundle, f, fid, pid);
    if (fid == 0) b = bundle;
    try { var of = Db.File0(b); var o = Db.Obj(of, p); if (o != null) { var r = Raw.At(of, o); return Raw.Str(r) + (Base(b) == Base(bundle) ? "" : "@" + b); } } catch { }
    return "?" + b + "#" + p;
  }
  public static void World(string bundle) {
    var f = Db.File0(bundle); Ctx.Bundle = bundle; Ctx.File = f; var byId = f.Objects.ToDictionary(o => o.PathId);
    var sb = new StringBuilder();
    sb.AppendLine("path\tgo\tactive\tlayer\tx\ty\tz\tyaw\tpitch\troll\tsx\tsy\tsz\tcomps\tmesh\tmaterials\tmin\tmax\textra");
    void Walk(long trId, double[] parent, string path, bool act) {
      var t = Raw.Tr(f, byId[trId]); var g = Raw.Go(f, byId[t.go]);
      var m = Mul(parent, TRS(t.pos, t.rot, t.scl));
      bool a = act && g.active; string pth = path.Length == 0 ? g.name : path + "/" + g.name;
      var comps = new List<string>(); string mesh = "", mats = "", mn = "", mx = "", extra = "";
      MI meshInfo = null; int firstSub = 0, subCount = 0;
      foreach (var c in g.comps) {
        if (!byId.TryGetValue(c, out var co)) { comps.Add("?"); continue; }
        int cid = co.ClassId; if (cid == 4 || cid == 224) continue;
        if (cid == 114) { var mb = Raw.Mb(f, co); comps.Add(Db.ResolveScript(bundle, f, mb.sfid, mb.spid).Split('.').Last() + (mb.en ? "" : "(off)") + "#" + c); continue; }
        comps.Add(ClassNames.Of(cid));
        try {
          if (cid == 33 || cid == 137) {
            var d = Dx(Ser.Read(f, co)); var pp = Dx(d["m_Mesh"]);
            long pid = Convert.ToInt64(pp["m_PathID"]); int fid = Convert.ToInt32(pp["m_FileID"]);
            if (pid != 0) { meshInfo = MeshInfo(bundle, f, fid, pid); mesh = meshInfo.Name + mesh; }
          }
          if (cid == 64) { // MeshCollider keeps the original mesh
            var d = Dx(Ser.Read(f, co)); var pp = Dx(d["m_Mesh"]);
            long pid = Convert.ToInt64(pp["m_PathID"]); int fid = Convert.ToInt32(pp["m_FileID"]);
            if (pid != 0) mesh += " col:" + MeshInfo(bundle, f, fid, pid).Name;
          }
          if (cid == 23 || cid == 137) {
            var d = Dx(Ser.Read(f, co)); var ml = d["m_Materials"] as List<object>;
            mats = string.Join("|", ml.Select(x => { var pp = Dx(x); return NameOf(bundle, f, Convert.ToInt32(pp["m_FileID"]), Convert.ToInt64(pp["m_PathID"])); }));
            if (d.TryGetValue("m_Enabled", out var en) && en is bool eb && !eb) extra += "renderer-off ";
            if (d.TryGetValue("m_StaticBatchInfo", out var sbi)) { firstSub = Convert.ToInt32(Dx(sbi)["firstSubMesh"]); subCount = Convert.ToInt32(Dx(sbi)["subMeshCount"]); }
          }
          if (cid == 108) { var d = Dx(Ser.Read(f, co)); extra += "light " + Json.Of(new Dictionary<string, object> { ["type"] = d["m_Type"], ["color"] = d["m_Color"], ["intensity"] = d["m_Intensity"], ["range"] = d["m_Range"], ["spot"] = d["m_SpotAngle"], ["shadows"] = Dx(d["m_Shadows"])["m_Type"], ["mode"] = d.GetValueOrDefault("m_Lightmapping") }, 3) + " "; }
          if (cid == 65 || cid == 136 || cid == 135) { var d = Dx(Ser.Read(f, co)); if (d.TryGetValue("m_Size", out var sz)) extra += "boxcol " + Json.Of(sz) + " c=" + Json.Of(d["m_Center"]) + " "; if (d.TryGetValue("m_IsTrigger", out var trg) && trg is bool tb && tb) extra += "trigger "; }
        } catch (Exception ex) { extra += "ERR " + ex.Message + " "; }
      }
      if (meshInfo != null && meshInfo.C != null) {
        double[] lo = { 1e9, 1e9, 1e9 }, hi = { -1e9, -1e9, -1e9 };
        if (subCount > 0 && meshInfo.Name.StartsWith("Combined Mesh")) {
          for (int s = firstSub; s < firstSub + subCount && s < meshInfo.Subs.Count; s++) { var q = meshInfo.Subs[s]; for (int k = 0; k < 3; k++) { lo[k] = Math.Min(lo[k], q[k] - q[k + 3]); hi[k] = Math.Max(hi[k], q[k] + q[k + 3]); } }
          extra += "static-batched ";
        } else {
          for (int i = 0; i < 8; i++) { var w = P(m, meshInfo.C[0] + ((i & 1) == 0 ? -1 : 1) * meshInfo.E[0], meshInfo.C[1] + ((i & 2) == 0 ? -1 : 1) * meshInfo.E[1], meshInfo.C[2] + ((i & 4) == 0 ? -1 : 1) * meshInfo.E[2]); for (int k = 0; k < 3; k++) { lo[k] = Math.Min(lo[k], w[k]); hi[k] = Math.Max(hi[k], w[k]); } }
        }
        mn = $"{F(lo[0])},{F(lo[1])},{F(lo[2])}"; mx = $"{F(hi[0])},{F(hi[1])},{F(hi[2])}";
      }
      var fw = new[] { m[2], m[6], m[10] }; var up = new[] { m[1], m[5], m[9] }; var rt = new[] { m[0], m[4], m[8] };
      double sx = Math.Sqrt(rt[0] * rt[0] + rt[1] * rt[1] + rt[2] * rt[2]), sy = Math.Sqrt(up[0] * up[0] + up[1] * up[1] + up[2] * up[2]), sz2 = Math.Sqrt(fw[0] * fw[0] + fw[1] * fw[1] + fw[2] * fw[2]);
      double yaw = Math.Atan2(fw[0], fw[2]) * 180 / Math.PI, pitch = -Math.Asin(Math.Clamp(fw[1] / Math.Max(sz2, 1e-9), -1, 1)) * 180 / Math.PI;
      double roll = Math.Atan2(rt[1] / Math.Max(sx, 1e-9), up[1] / Math.Max(sy, 1e-9)) * 180 / Math.PI;
      sb.Append(pth).Append('\t').Append(t.go).Append('\t').Append(a ? "1" : "0").Append('\t').Append(g.layer).Append('\t')
        .Append($"{F(m[3])}\t{F(m[7])}\t{F(m[11])}\t{F(yaw)}\t{F(pitch)}\t{F(roll)}\t{F(sx)}\t{F(sy)}\t{F(sz2)}\t")
        .Append(string.Join(",", comps)).Append('\t').Append(mesh.Trim()).Append('\t').Append(mats).Append('\t').Append(mn).Append('\t').Append(mx).Append('\t').Append(extra.Trim()).Append('\n');
      foreach (var k in t.kids) if (byId.ContainsKey(k)) Walk(k, m, pth, a);
    }
    var id = new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
    foreach (var o in f.Objects.Where(o => o.ClassId == 4 || o.ClassId == 224)) { var t = Raw.Tr(f, o); if (t.father == 0) Walk(o.PathId, id, "", true); }
    Console.Write(sb.ToString());
  }
  public static void Mats(string bundle) {
    var f = Db.File0(bundle); Ctx.Bundle = bundle; Ctx.File = f;
    foreach (var o in f.Objects.Where(o => o.ClassId == 21)) {
      try {
        var d = Dx(Ser.Read(f, o)); var sp = Dx(d["m_SavedProperties"]);
        var sh = Dx(d["m_Shader"]); string shader = NameOf(bundle, f, Convert.ToInt32(sh["m_FileID"]), Convert.ToInt64(sh["m_PathID"]));
        var cols = (sp["m_Colors"] as List<object>).Select(x => { var p = Dx(x); return p["first"] + "=" + Json.Of(p["second"]); });
        var fl = (sp["m_Floats"] as List<object>).Select(x => { var p = Dx(x); return p["first"] + "=" + Json.Of(p["second"]); }).Where(s => s.StartsWith("_Metallic") || s.StartsWith("_Smooth") || s.StartsWith("_Glossiness") || s.StartsWith("_Emission") || s.StartsWith("_Surface") || s.StartsWith("_Cutoff") || s.StartsWith("_Mode"));
        var tx = (sp["m_TexEnvs"] as List<object>).Select(x => { var p = Dx(x); var t = Dx(Dx(p["second"])["m_Texture"]); long pid = Convert.ToInt64(t["m_PathID"]); if (pid == 0) return null; var sc = Dx(Dx(p["second"])["m_Scale"]); return p["first"] + "=" + NameOf(bundle, f, Convert.ToInt32(t["m_FileID"]), pid) + " x" + F(N(sc["x"])) + "," + F(N(sc["y"])); }).Where(s => s != null);
        var kw = d.GetValueOrDefault("m_ValidKeywords") is List<object> vk ? string.Join(" ", vk) : d.GetValueOrDefault("m_ShaderKeywords") as string;
        Console.WriteLine($"{o.PathId}\t{d["m_Name"]}\t{shader}\t{string.Join(" ", cols)}\t{string.Join(" ", fl)}\t{string.Join(" ", tx)}\t{kw}");
      } catch (Exception e) { Console.WriteLine($"{o.PathId}\tERR {e.Message}"); }
    }
  }
}
