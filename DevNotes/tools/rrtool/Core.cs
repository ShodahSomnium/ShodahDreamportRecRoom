// Scratch scanner: catalogues Rec Room's Addressables bundles (UnityFS, serialized v22, type trees).
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

static class Lz4Dec {
  public static int Decode(byte[] src, int srcOff, int srcLen, byte[] dst, int dstOff = 0) {
    int s = srcOff, end = srcOff + srcLen, d = dstOff;
    while (s < end) {
      int tok = src[s++];
      int lit = tok >> 4;
      if (lit == 15) { int b; do { b = src[s++]; lit += b; } while (b == 255); }
      Buffer.BlockCopy(src, s, dst, d, lit); s += lit; d += lit;
      if (s >= end) break;
      int off = src[s] | (src[s + 1] << 8); s += 2;
      int ml = tok & 15;
      if (ml == 15) { int b; do { b = src[s++]; ml += b; } while (b == 255); }
      ml += 4;
      for (int i = 0; i < ml; i++) { dst[d] = dst[d - off]; d++; }
    }
    return d - dstOff;
  }
}

class BR {
  public byte[] B; public int P; public bool BE;
  public BR(byte[] b, int p = 0, bool be = false) { B = b; P = p; BE = be; }
  public byte U8() => B[P++];
  public bool Bool() => B[P++] != 0;
  public short I16() { short v = BE ? (short)(B[P] << 8 | B[P + 1]) : BitConverter.ToInt16(B, P); P += 2; return v; }
  public ushort U16() => (ushort)I16();
  public int I32() { int v = BE ? (B[P] << 24 | B[P + 1] << 16 | B[P + 2] << 8 | B[P + 3]) : BitConverter.ToInt32(B, P); P += 4; return v; }
  public uint U32() => (uint)I32();
  public long I64() { long v; if (BE) { v = 0; for (int i = 0; i < 8; i++) v = (v << 8) | B[P + i]; } else v = BitConverter.ToInt64(B, P); P += 8; return v; }
  public float F32() { float v = BitConverter.ToSingle(B, P); P += 4; return v; }
  public double F64() { double v = BitConverter.ToDouble(B, P); P += 8; return v; }
  public string CStr() { int s = P; while (B[P] != 0) P++; var r = Encoding.UTF8.GetString(B, s, P - s); P++; return r; }
  public void Align(int n = 4) { P = (P + n - 1) / n * n; }
  public byte[] Bytes(int n) { var r = new byte[n]; Buffer.BlockCopy(B, P, r, 0, n); P += n; return r; }
}

class TNode { public string Type, Name; public int Level, Size, Index, Meta; public List<TNode> Kids = new(); }
class SType { public int ClassId; public short ScriptIdx; public TNode Root; public byte[] ScriptHash; }
class SObj { public long PathId; public long Start; public uint Size; public int TypeIdx; public int ClassId; }
class SFile {
  public string Name; public byte[] Data; public int DataOff; public bool BE;
  public List<SType> Types = new(); public List<SObj> Objects = new(); public List<string> Externals = new();
  public string UnityVersion; public int Platform;
}

static class Common {
  public static Dictionary<uint, string> Strings = new();
  public static void Load(string unityPlayer) {
    var b = File.ReadAllBytes(unityPlayer);
    var pat = Encoding.ASCII.GetBytes("AABB\0AnimationClip\0AnimationCurve\0");
    int at = -1;
    for (int i = 0; i < b.Length - pat.Length; i++) { int j = 0; while (j < pat.Length && b[i + j] == pat[j]) j++; if (j == pat.Length) { at = i; break; } }
    if (at < 0) throw new Exception("common strings not found");
    int p = at; uint off = 0;
    while (true) {
      int s = p; while (b[p] != 0) p++;
      if (p == s) break;
      Strings[off] = Encoding.ASCII.GetString(b, s, p - s);
      off += (uint)(p - s + 1); p++;
      if (off > 8000) break;
    }
  }
}

static class Ser {
  public static SFile Parse(string name, byte[] data, int off) {
    var f = new SFile { Name = name };
    var r = new BR(data, off, true);
    r.U32(); r.U32(); uint ver = r.U32(); r.U32();
    bool be = r.U8() != 0; r.P += 3;
    long metaSize = r.U32(); long fileSize = r.I64(); long dataOff = r.I64(); r.I64();
    if (ver < 22) throw new Exception("ver " + ver);
    f.DataOff = off + (int)dataOff; f.BE = be; f.Data = data;
    r.BE = be;
    f.UnityVersion = r.CStr(); f.Platform = r.I32();
    bool tt = r.Bool();
    int nt = r.I32();
    for (int i = 0; i < nt; i++) f.Types.Add(ReadType(r, tt, false));
    int no = r.I32();
    for (int i = 0; i < no; i++) {
      r.Align(4);
      var o = new SObj { PathId = r.I64(), Start = r.I64(), Size = r.U32(), TypeIdx = r.I32() };
      o.ClassId = f.Types[o.TypeIdx].ClassId; f.Objects.Add(o);
    }
    int ns = r.I32();
    for (int i = 0; i < ns; i++) { r.I32(); r.Align(4); r.I64(); }
    int ne = r.I32();
    for (int i = 0; i < ne; i++) { r.CStr(); r.Bytes(16); r.I32(); f.Externals.Add(r.CStr()); }
    return f;
  }
  static SType ReadType(BR r, bool tt, bool isRef) {
    var t = new SType { ClassId = r.I32() };
    r.Bool(); t.ScriptIdx = r.I16();
    if (isRef || t.ClassId == 114) t.ScriptHash = r.Bytes(16);
    r.Bytes(16);
    if (tt) {
      int nn = r.I32(), sbs = r.I32();
      var raw = new List<(ushort v, byte lvl, byte fl, uint ts, uint ns, int sz, int idx, int meta)>();
      for (int i = 0; i < nn; i++) { raw.Add((r.U16(), r.U8(), r.U8(), r.U32(), r.U32(), r.I32(), r.I32(), r.I32())); r.I64(); }
      var sb = r.Bytes(sbs);
      string S(uint o) { if ((o & 0x80000000) != 0) return Common.Strings.TryGetValue(o & 0x7fffffff, out var c) ? c : "?" + (o & 0x7fffffff); int e = (int)o; while (sb[e] != 0) e++; return Encoding.UTF8.GetString(sb, (int)o, e - (int)o); }
      var stack = new List<TNode>();
      foreach (var n in raw) {
        var node = new TNode { Type = S(n.ts), Name = S(n.ns), Level = n.lvl, Size = n.sz, Index = n.idx, Meta = n.meta };
        if (n.lvl == 0) { t.Root = node; stack.Clear(); stack.Add(node); }
        else { while (stack.Count > n.lvl) stack.RemoveAt(stack.Count - 1); stack[^1].Kids.Add(node); stack.Add(node); }
      }
      int nd = r.I32(); for (int i = 0; i < nd; i++) r.I32();
    }
    return t;
  }
  // Generic type-tree reader -> object graph (Dictionary / List / primitives)
  public static object Read(SFile f, SObj o) {
    var t = f.Types[o.TypeIdx];
    var r = new BR(f.Data, f.DataOff + (int)o.Start, f.BE);
    return ReadNode(r, t.Root);
  }
  static object ReadNode(BR r, TNode n) {
    object v;
    switch (n.Type) {
      case "SInt8": v = (sbyte)r.U8(); break;
      case "UInt8": case "char": v = r.U8(); break;
      case "bool": v = r.Bool(); break;
      case "SInt16": case "short": v = r.I16(); break;
      case "UInt16": case "unsigned short": v = r.U16(); break;
      case "SInt32": case "int": case "Type*": v = r.I32(); break;
      case "UInt32": case "unsigned int": v = r.U32(); break;
      case "SInt64": case "long long": v = r.I64(); break;
      case "UInt64": case "unsigned long long": case "FileSize": v = (ulong)r.I64(); break;
      case "float": v = r.F32(); break;
      case "double": v = r.F64(); break;
      case "string": { int len = r.I32(); v = Encoding.UTF8.GetString(r.B, r.P, len); r.P += len; r.Align(4); return v; }
      case "TypelessData": { int len = r.I32(); v = r.Bytes(len); break; }
      default:
        if (n.Kids.Count == 1 && n.Kids[0].Type == "Array" || n.Type == "vector" && n.Kids.Count == 1) {
          var arr = n.Kids[0].Type == "Array" ? n.Kids[0] : n.Kids[0];
          v = ReadArray(r, arr);
        } else if (n.Type == "Array") {
          v = ReadArray(r, n);
        } else {
          var d = new Dictionary<string, object>();
          foreach (var k in n.Kids) d[k.Name] = ReadNode(r, k);
          v = d;
        }
        break;
    }
    if ((n.Meta & 0x4000) != 0) r.Align(4);
    return v;
  }
  static object ReadArray(BR r, TNode arr) {
    int count = r.I32();
    var el = arr.Kids[1];
    if (count < 0 || count > 50_000_000) throw new Exception("bad count " + count);
    object res;
    if (el.Type == "UInt8" || el.Type == "char") { res = r.Bytes(count); }
    else {
      var l = new List<object>(Math.Min(count, 100000));
      for (int i = 0; i < count; i++) l.Add(ReadNode(r, el));
      res = l;
    }
    if ((arr.Meta & 0x4000) != 0) r.Align(4);
    return res;
  }
}

static class Bundle {
  public static (byte[] data, List<(long off, long size, string name)> nodes) Open(string path) {
    var b = File.ReadAllBytes(path);
    var r = new BR(b, 0, true);
    var sig = r.CStr(); if (sig != "UnityFS") throw new Exception("not UnityFS: " + sig);
    var ver = r.U32(); r.CStr(); r.CStr();
    long size = r.I64(); uint csz = r.U32(), usz = r.U32(), flags = r.U32();
    if (ver >= 7) r.Align(16);
    byte[] bi = new byte[usz];
    int biStart = (flags & 0x80) != 0 ? (int)(b.Length - csz) : r.P;
    int comp = (int)(flags & 0x3f);
    if (comp == 2 || comp == 3) Lz4Dec.Decode(b, biStart, (int)csz, bi); else if (comp == 0) Buffer.BlockCopy(b, biStart, bi, 0, (int)usz); else throw new Exception("bi comp " + comp);
    if ((flags & 0x80) == 0) r.P += (int)csz;
    if ((flags & 0x200) != 0) r.Align(16);
    var q = new BR(bi, 16, true);
    int nb = q.I32();
    var blocks = new List<(uint u, uint c, ushort f)>();
    for (int i = 0; i < nb; i++) blocks.Add((q.U32(), q.U32(), q.U16()));
    int nn = q.I32();
    var nodes = new List<(long, long, string)>();
    for (int i = 0; i < nn; i++) { var o = q.I64(); var s = q.I64(); q.U32(); nodes.Add((o, s, q.CStr())); }
    long total = blocks.Sum(x => (long)x.u);
    var data = new byte[total]; int dp = 0; int sp = r.P;
    foreach (var bl in blocks) {
      int c = bl.f & 0x3f;
      if (c == 0) Buffer.BlockCopy(b, sp, data, dp, (int)bl.c);
      else if (c == 2 || c == 3) Lz4Dec.Decode(b, sp, (int)bl.c, data, dp);
      else throw new Exception("block comp " + c);
      sp += (int)bl.c; dp += (int)bl.u;
    }
    return (data, nodes);
  }
}

static class ClassNames {
  public static readonly Dictionary<int, string> N = new() {
    {1,"GameObject"},{4,"Transform"},{21,"Material"},{23,"MeshRenderer"},{28,"Texture2D"},{33,"MeshFilter"},{43,"Mesh"},{48,"Shader"},{49,"TextAsset"},
    {54,"Rigidbody"},{64,"MeshCollider"},{65,"BoxCollider"},{74,"AnimationClip"},{82,"AudioSource"},{83,"AudioClip"},{89,"Cubemap"},{90,"Avatar"},{91,"AnimatorController"},
    {95,"Animator"},{96,"TrailRenderer"},{108,"Light"},{111,"Animation"},{114,"MonoBehaviour"},{115,"MonoScript"},{117,"Texture3D"},{120,"LineRenderer"},{128,"Font"},
    {135,"SphereCollider"},{136,"CapsuleCollider"},{137,"SkinnedMeshRenderer"},{142,"AssetBundle"},{150,"PreloadData"},{153,"ConfigurableJoint"},{154,"TerrainCollider"},
    {156,"TerrainData"},{157,"LightmapSettings"},{187,"Texture2DArray"},{198,"ParticleSystem"},{199,"ParticleSystemRenderer"},{205,"LODGroup"},{212,"SpriteRenderer"},{213,"Sprite"},
    {215,"ReflectionProbe"},{220,"LightProbeGroup"},{221,"AnimatorOverrideController"},{222,"CanvasRenderer"},{223,"Canvas"},{224,"RectTransform"},{225,"CanvasGroup"},
    {258,"LightProbes"},{319,"AvatarMask"},{328,"VideoPlayer"},{329,"VideoClip"},{331,"SpriteMask"},{1001,"PrefabInstance"},{1101,"AnimatorStateTransition"},{1102,"AnimatorState"},
    {1107,"AnimatorStateMachine"},{1120,"LightingDataAsset"},{104,"RenderSettings"},{127,"LevelGameManager"},{196,"NavMeshSettings"},{238,"NavMeshData"},{29,"OcclusionCullingSettings"},
    {157*0+30,"GraphicsSettings"},{68,"EdgeCollider2D"},{55,"PhysicsManager"},{57,"HingeJoint"},{59,"FixedJoint"},{60,"SpringJoint"},{92,"GUITexture"},{102,"TextMesh"},
    {84,"RenderTexture"},{86,"CustomRenderTexture"},{1953259897,"AudioMixerSnapshot"},{241,"AudioMixerController"},{243,"AudioMixerGroupController"},{245,"AudioMixerSnapshotController"},
    {244,"AudioMixerEffectController"},{273,"AudioMixerGroup"},{687078895,"SpriteAtlas"},{2083052967,"VisualEffect"},{2058629511,"VisualEffectAsset"},{73398921,"VFXRenderer"},
  };
  public static string Of(int id) => N.TryGetValue(id, out var s) ? s : "class" + id;
}

