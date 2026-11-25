// apkscene: one Android level file -> scene model (GameObjects, world transforms, renderers with resolved mesh and
// material names, colliders, lights, terrains, audio, MonoBehaviour script names, render settings).
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

sealed class GoN { public long Id; public string Name = ""; public int Layer; public bool Self = true; public string Tag; public List<(AF f, AObj o)> Comps = new(); public long Tr; }
sealed class TrN { public long Id; public long Go; public long Father; public List<long> Kids = new(); public V3 lp; public Q lr = Q.I; public V3 ls = new(1, 1, 1); public TRS W; public bool Done; }
sealed class MeshInfo {
  public string Name; public string File; public long Pid; public int VCount; public List<(long ic, long vc, V3 c, V3 e)> Subs = new(); public V3 C, E; public bool Combined;
}
sealed class RendN { public long Id; public long Go; public bool En; public bool Skinned; public MeshInfo Mesh; public List<(string name, double[] col)> Mats = new(); public int FirstSub, SubCount; public bool Lod1; }
sealed class ColN { public long Go; public int Cls; public bool En, Trig, Convex; public V3 C, Size; public double Rad, H; public int Dir; public MeshInfo Mesh; }
sealed class LightN { public long Go; public bool En; public int Type; public double[] Col; public double Int, Range, Spot; public int Shadows; public double Strength = 1; public long Pid; }
sealed class AudN { public long Go; public bool En; public string Clip; public double Vol, Min, Max; public bool Loop, Awake; public double Blend; public int Roll; }
sealed class TerrN { public long Go; public bool En; public string Name; public int Res; public V3 Scale; public short[] H; public string Mat; public List<(string layer, string tex, double tile)> Layers = new(); public bool Collider; public string Note; }
sealed class MbN { public long Go; public string Script; public bool En; public Dictionary<string, object> D; }

sealed class Model {
  public string Level; public AF F; public Ld L;
  public Dictionary<long, GoN> Gos = new(); public Dictionary<long, TrN> Trs = new();
  public List<RendN> Rend = new(); public List<ColN> Col = new(); public List<LightN> Lights = new(); public List<AudN> Aud = new();
  public List<TerrN> Terr = new(); public List<MbN> Mbs = new(); public Dictionary<string, object> RS; public HashSet<long> LodSkip = new();
  public Dictionary<long, long> MfMesh = new(); // go -> mesh (via MeshFilter)
  readonly Dictionary<(string, long), MeshInfo> meshCache = new();

  public bool Active(long go) {
    if (!Gos.TryGetValue(go, out var g)) return false;
    for (long t = g.Tr; t != 0 && Trs.TryGetValue(t, out var tn);) { if (!Gos.TryGetValue(tn.Go, out var gg) || !gg.Self) return false; t = tn.Father; }
    return true;
  }
  public TRS World(long tr) {
    if (!Trs.TryGetValue(tr, out var t)) return TRS.I;
    if (t.Done) return t.W;
    var pw = t.Father != 0 ? World(t.Father) : TRS.I;
    t.W = new TRS { p = pw.p + pw.r.Rot(pw.s.Mul(t.lp)), r = (pw.r * t.lr).Norm(), s = pw.s.Mul(t.ls) }; t.Done = true; return t.W;
  }
  public TRS GoW(long go) => Gos.TryGetValue(go, out var g) ? World(g.Tr) : TRS.I;
  public string Name(long go) => Gos.TryGetValue(go, out var g) ? g.Name : "?";
  public string PathOf(long go) { var parts = new List<string>(); var g = Gos.GetValueOrDefault(go); int n = 0; while (g != null && n++ < 64) { parts.Add(g.Name); var t = Trs.GetValueOrDefault(g.Tr); if (t == null || t.Father == 0) break; var pt = Trs.GetValueOrDefault(t.Father); g = pt == null ? null : Gos.GetValueOrDefault(pt.Go); } parts.Reverse(); return string.Join("/", parts); }
  public long Parent(long go) { var g = Gos.GetValueOrDefault(go); var t = g == null ? null : Trs.GetValueOrDefault(g.Tr); if (t == null || t.Father == 0) return 0; return Trs.GetValueOrDefault(t.Father)?.Go ?? 0; }

  public MeshInfo MeshOf(AF from, (int fid, long pid) p) {
    var (f, o) = L.Res(from, p); if (o == null) return null;
    if (meshCache.TryGetValue((f.Name, o.PathId), out var mi)) return mi;
    var d = L.Read(f, o);
    mi = new MeshInfo { File = f.Name, Pid = o.PathId };
    if (d != null) {
      mi.Name = d.Str("m_Name") ?? "";
      foreach (var x in d.Lst("m_SubMeshes") ?? new()) { var s = (Dictionary<string, object>)x; var bb = s.Dct("localAABB"); mi.Subs.Add(((long)s.Num("indexCount"), (long)s.Num("vertexCount"), bb.Vec("m_Center"), bb.Vec("m_Extent"))); }
      mi.VCount = (int)(d.Dct("m_VertexData")?.Num("m_VertexCount") ?? 0);
      var ab = d.Dct("m_LocalAABB"); mi.C = ab.Vec("m_Center"); mi.E = ab.Vec("m_Extent");
      mi.Combined = mi.Name.StartsWith("Combined Mesh");
    }
    meshCache[(f.Name, o.PathId)] = mi; return mi;
  }

  public static Model Load(Ld L, string level) {
    var m = new Model { Level = level, L = L, F = L.File(level) };
    var f = m.F; if (f == null) throw new Exception("no level " + level);
    // pass 1: GameObjects and transforms
    foreach (var o in f.Objects) {
      if (o.ClassId == 1) {
        var d = L.Read(f, o); if (d == null) continue;
        var g = new GoN { Id = o.PathId, Name = d.Str("m_Name") ?? "", Layer = (int)d.Num("m_Layer"), Self = d.Bool("m_IsActive", true) };
        foreach (var c in d.Lst("m_Component") ?? new()) { var p = DX.Ptr(((Dictionary<string, object>)c).G("component")); var r = L.Res(f, p); if (r.o != null) g.Comps.Add(r); }
        m.Gos[o.PathId] = g;
      } else if (o.ClassId == 4 || o.ClassId == 224) {
        var d = L.Read(f, o); if (d == null) continue;
        var t = new TrN { Id = o.PathId, Go = d.Ptr("m_GameObject").pid, Father = d.Ptr("m_Father").pid, lp = d.Vec("m_LocalPosition"), lr = d.Quat("m_LocalRotation"), ls = d.Vec("m_LocalScale") };
        m.Trs[o.PathId] = t;
      }
    }
    foreach (var t in m.Trs.Values) if (m.Gos.TryGetValue(t.Go, out var g)) g.Tr = t.Id;
    foreach (var t in m.Trs.Values) if (t.Father != 0 && m.Trs.TryGetValue(t.Father, out var pt)) pt.Kids.Add(t.Id);
    // pass 2: components
    foreach (var o in f.Objects) {
      switch (o.ClassId) {
        case 33: { var d = L.Read(f, o); if (d == null) break; var go = d.Ptr("m_GameObject").pid; var mesh = d.Ptr("m_Mesh"); if (mesh.pid != 0) { m.MfMesh[go] = o.PathId; m.mfPtr[go] = mesh; } break; }
        case 23: case 137: {
          var d = L.Read(f, o); if (d == null) break;
          var r = new RendN { Id = o.PathId, Go = d.Ptr("m_GameObject").pid, En = d.Bool("m_Enabled", true), Skinned = o.ClassId == 137 };
          foreach (var mp in d.Lst("m_Materials") ?? new()) { var md = L.Read(f, DX.Ptr(mp)); r.Mats.Add((md?.Str("m_Name"), md == null ? null : Inst.MatColor(md))); }
          var sb = d.Dct("m_StaticBatchInfo"); if (sb != null) { r.FirstSub = (int)sb.Num("firstSubMesh"); r.SubCount = (int)sb.Num("subMeshCount"); }
          if (r.Skinned) r.Mesh = m.MeshOf(f, d.Ptr("m_Mesh"));
          m.Rend.Add(r); break;
        }
        case 64: case 65: case 135: case 136: {
          var d = L.Read(f, o); if (d == null) break;
          var c = new ColN { Go = d.Ptr("m_GameObject").pid, Cls = o.ClassId, En = d.Bool("m_Enabled", true), Trig = d.Bool("m_IsTrigger"), C = d.Vec("m_Center"), Size = d.Vec("m_Size"), Rad = d.Num("m_Radius"), H = d.Num("m_Height"), Dir = (int)d.Num("m_Direction", 1), Convex = d.Bool("m_Convex") };
          if (o.ClassId == 64) c.Mesh = m.MeshOf(f, d.Ptr("m_Mesh"));
          m.Col.Add(c); break;
        }
        case 108: {
          var d = L.Read(f, o); if (d == null) break;
          m.Lights.Add(new LightN { Go = d.Ptr("m_GameObject").pid, En = d.Bool("m_Enabled", true), Type = (int)d.Num("m_Type"), Col = d.Col("m_Color"), Int = d.Num("m_Intensity"), Range = d.Num("m_Range"), Spot = d.Num("m_SpotAngle"), Shadows = (int)(d.Dct("m_Shadows")?.Num("m_Type") ?? 0), Strength = d.Dct("m_Shadows")?.Num("m_Strength", 1) ?? 1, Pid = o.PathId }); break;
        }
        case 82: {
          var d = L.Read(f, o); if (d == null) break;
          var clip = L.NameOf(f, d.Ptr("m_audioClip"));
          m.Aud.Add(new AudN { Go = d.Ptr("m_GameObject").pid, En = d.Bool("m_Enabled", true), Clip = clip, Vol = d.Num("m_Volume", 1), Min = d.Num("MinDistance", 1), Max = d.Num("MaxDistance", 500), Loop = d.Bool("Loop"), Awake = d.Bool("m_PlayOnAwake"), Roll = (int)d.Num("rolloffMode") }); break;
        }
        case 205: {
          var d = L.Read(f, o); if (d == null) break; int li = 0;
          foreach (var lod in d.Lst("m_LODs") ?? new()) { if (li++ == 0) continue; foreach (var rr in ((Dictionary<string, object>)lod).Lst("renderers") ?? new()) { var p = ((Dictionary<string, object>)rr).Ptr("renderer"); if (p.fid == 0 && p.pid != 0) m.LodSkip.Add(p.pid); } }
          break;
        }
        case 114: {
          var d = L.Read(f, o);
          var go = d != null ? d.Ptr("m_GameObject").pid : 0;
          m.Mbs.Add(new MbN { Go = go, Script = L.Script(f, o, d), En = d?.Bool("m_Enabled", true) ?? true, D = d }); break;
        }
        case 104: m.RS = RenderSettingsRaw(f, o); break;
      }
    }
    foreach (var r in m.Rend) if (!r.Skinned && m.mfPtr.TryGetValue(r.Go, out var mp)) r.Mesh = m.MeshOf(f, mp);
    foreach (var r in m.Rend) if (m.LodSkip.Contains(r.Id)) r.Lod1 = true;
    // pass 3: terrains (Terrain 218 and TerrainCollider 154 have no type tree in the PC bundles: found by their PPtrs)
    var tcol = new HashSet<long>();
    foreach (var o in f.Objects.Where(x => x.ClassId == 154)) { var go = GoOfRaw(f, o); var td = FindPtr(L, f, o, 156); if (td.o != null) tcol.Add(td.o.PathId); }
    foreach (var o in f.Objects.Where(x => x.ClassId == 218)) {
      var go = GoOfRaw(f, o); var td = FindPtr(L, f, o, 156); var mat = FindPtr(L, f, o, 21);
      var tn = new TerrN { Go = go, En = true, Collider = td.o != null && tcol.Contains(td.o.PathId) };
      if (mat.o != null) tn.Mat = L.Read(mat.f, mat.o)?.Str("m_Name");
      if (td.o != null) ReadTerrainData(L, td.f, td.o, tn); else tn.Note = "no TerrainData pointer";
      m.Terr.Add(tn);
    }
    return m;
  }
  readonly Dictionary<long, (int, long)> mfPtr = new();

  static long GoOfRaw(AF f, AObj o) { int s = f.DataOff + (int)o.Start; return BitConverter.ToInt64(f.Data, s + 4); } // m_GameObject is the first field of every Component
  /// First PPtr in a tree-less object that resolves to an object of class <cls>.
  public static (AF f, AObj o) FindPtr(Ld L, AF f, AObj o, int cls, int from = 12) {
    int s = f.DataOff + (int)o.Start;
    for (int i = from; i + 12 <= o.Size; i += 4) {
      int fid = BitConverter.ToInt32(f.Data, s + i); if (fid < 0 || fid > f.Ext.Count) continue;
      long pid = BitConverter.ToInt64(f.Data, s + i + 4); if (pid == 0) continue;
      var r = L.Res(f, (fid, pid)); if (r.o != null && r.o.ClassId == cls) return r;
    }
    return (null, null);
  }
  static string RawName(AF f, AObj o) { int s = f.DataOff + (int)o.Start; int n = BitConverter.ToInt32(f.Data, s); if (n < 0 || n > 512 || n + 4 > o.Size) return null; return Encoding.UTF8.GetString(f.Data, s + 4, n); }
  static void ReadTerrainData(Ld L, AF f, AObj o, TerrN tn) {
    tn.Name = RawName(f, o);
    int s = f.DataOff + (int)o.Start; var B = f.Data; int size = (int)o.Size;
    int[] rs = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
    // terrain layers: m_SplatDatabase.m_TerrainLayers follows the name
    int p0 = 4 + ((BitConverter.ToInt32(B, s) + 3) & ~3);
    int nl = BitConverter.ToInt32(B, s + p0);
    if (nl > 0 && nl < 32) for (int i = 0; i < nl; i++) {
      int fid = BitConverter.ToInt32(B, s + p0 + 4 + i * 12); long pid = BitConverter.ToInt64(B, s + p0 + 8 + i * 12);
      var r = L.Res(f, (fid, pid)); if (r.o == null) continue;
      var ln = RawName(r.f, r.o); var tex = FindPtr(L, r.f, r.o, 28, 4 + ((BitConverter.ToInt32(r.f.Data, r.f.DataOff + (int)r.o.Start) + 3) & ~3));
      string tname = tex.o != null ? L.Read(tex.f, tex.o)?.Str("m_Name") : null;
      tn.Layers.Add((ln, tname, 0));
    }
    for (int i = p0; i + 8 <= size; i += 4) {
      int c = BitConverter.ToInt32(B, s + i); int R = Array.IndexOf(rs.Select(x => x * x).ToArray(), c); if (R < 0) continue; R = rs[R];
      int q = i + 4 + c * 2; if (q > size) continue;
      q = (q + 3) & ~3;
      // after m_Heights: m_Holes, m_HolesLOD (byte vectors), m_EnableHolesTextureCompression, m_PrecomputedError, m_MinMaxPatchHeights (float vectors), m_Resolution, m_Levels, m_Scale
      bool ok = false; int qq = q;
      try {
        int h1 = BitConverter.ToInt32(B, s + qq); qq += 4 + h1; qq = (qq + 3) & ~3;
        int h2 = BitConverter.ToInt32(B, s + qq); qq += 4 + h2; qq = (qq + 3) & ~3;
        qq += 4; // bool + align
        int e1 = BitConverter.ToInt32(B, s + qq); qq += 4 + e1 * 4;
        int e2 = BitConverter.ToInt32(B, s + qq); qq += 4 + e2 * 4;
        int res = BitConverter.ToInt32(B, s + qq); ok = res == R && qq + 20 <= size;
        if (ok) { tn.Scale = new V3(BitConverter.ToSingle(B, s + qq + 8), BitConverter.ToSingle(B, s + qq + 12), BitConverter.ToSingle(B, s + qq + 16)); }
      } catch { ok = false; }
      if (!ok) { // fallback: the first int == R after the heights, followed by a small level count and a positive scale
        for (int k = q; k + 20 <= size; k += 4) if (BitConverter.ToInt32(B, s + k) == R) { int lv = BitConverter.ToInt32(B, s + k + 4); var sc = new V3(BitConverter.ToSingle(B, s + k + 8), BitConverter.ToSingle(B, s + k + 12), BitConverter.ToSingle(B, s + k + 16)); if (lv >= 0 && lv < 16 && sc.x > 0.01 && sc.y > 0.01 && sc.z > 0.01 && sc.x < 100 && sc.z < 100 && sc.y < 10000) { tn.Scale = sc; ok = true; tn.Note = "scale by fallback scan"; break; } }
      }
      if (!ok) continue;
      tn.Res = R; tn.H = new short[c]; Buffer.BlockCopy(B, s + i + 4, tn.H, 0, c * 2); return;
    }
    tn.Note = "no heightmap found";
  }
  /// RenderSettings (104) has no type tree in the PC bundles; its leading fields have been stable for many versions.
  static Dictionary<string, object> RenderSettingsRaw(AF f, AObj o) {
    int s = f.DataOff + (int)o.Start; var B = f.Data; int p = s;
    float F() { var v = BitConverter.ToSingle(B, p); p += 4; return v; }
    int I() { var v = BitConverter.ToInt32(B, p); p += 4; return v; }
    double[] C() => new double[] { F(), F(), F(), F() };
    var d = new Dictionary<string, object>();
    d["m_Fog"] = B[p] != 0; p += 4; d["m_FogColor"] = C(); d["m_FogMode"] = I(); d["m_FogDensity"] = F(); d["m_LinearFogStart"] = F(); d["m_LinearFogEnd"] = F();
    d["m_AmbientSkyColor"] = C(); d["m_AmbientEquatorColor"] = C(); d["m_AmbientGroundColor"] = C(); d["m_AmbientIntensity"] = F(); d["m_AmbientMode"] = I();
    d["m_SubtractiveShadowColor"] = C(); int sf = I(); long sp = BitConverter.ToInt64(B, p); p += 8; d["sky"] = (sf, sp);
    return d;
  }
}
