// apkscene: Unity serialized files (v22) with or without type trees, a generic type-tree reader, and the type-tree bank
// harvested from the PC install's plain Addressables bundles (same Unity 6000.0.27f1).
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

sealed class AType { public int ClassId; public bool Stripped; public short ScriptIdx; public string ScriptId = ""; public string Hash = ""; public TNode Tree; }
sealed class AObj { public long PathId; public long Start; public uint Size; public int TypeIdx; public int ClassId; }
sealed class AExt { public string Path; public string Guid; public int Type; }

sealed class AF {
  public string Name; public byte[] Data; public int DataOff; public bool TT; public int Version; public string UnityVersion; public int Platform;
  public List<AType> Types = new(); public List<AObj> Objects = new(); public Dictionary<long, AObj> ById = new(); public List<AExt> Ext = new();
  static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

  public static AF Parse(string name, byte[] data, int off = 0) {
    var f = new AF { Name = name, Data = data };
    var r = new BR(data, off, true);
    r.U32(); r.U32(); f.Version = (int)r.U32(); r.U32();
    bool be = r.U8() != 0; r.P += 3;
    if (f.Version < 22) throw new Exception(name + ": serialized version " + f.Version);
    r.U32(); r.I64(); long dataOff = r.I64(); r.I64();
    f.DataOff = off + (int)dataOff; if (be) throw new Exception(name + ": big-endian data");
    r.BE = false;
    f.UnityVersion = r.CStr(); f.Platform = r.I32(); f.TT = r.Bool();
    int nt = r.I32();
    for (int i = 0; i < nt; i++) f.Types.Add(ReadType(r, f.TT, false));
    int no = r.I32();
    for (int i = 0; i < no; i++) {
      r.Align(4);
      var o = new AObj { PathId = r.I64(), Start = r.I64(), Size = r.U32(), TypeIdx = r.I32() };
      o.ClassId = f.Types[o.TypeIdx].ClassId; f.Objects.Add(o); f.ById[o.PathId] = o;
    }
    int ns = r.I32(); for (int i = 0; i < ns; i++) { r.I32(); r.Align(4); r.I64(); }
    int ne = r.I32();
    for (int i = 0; i < ne; i++) { r.CStr(); var g = r.Bytes(16); int ty = r.I32(); f.Ext.Add(new AExt { Guid = Hex(g), Type = ty, Path = r.CStr() }); }
    return f;
  }
  static AType ReadType(BR r, bool tt, bool isRef) {
    var t = new AType { ClassId = r.I32() };
    t.Stripped = r.Bool(); t.ScriptIdx = r.I16();
    if (isRef || t.ClassId == 114) t.ScriptId = Hex(r.Bytes(16));
    t.Hash = Hex(r.Bytes(16));
    if (tt) {
      int nn = r.I32(), sbs = r.I32();
      var raw = new List<(ushort v, byte lvl, byte fl, uint ts, uint ns, int sz, int idx, int meta)>();
      for (int i = 0; i < nn; i++) { raw.Add((r.U16(), r.U8(), r.U8(), r.U32(), r.U32(), r.I32(), r.I32(), r.I32())); r.I64(); }
      var sb = r.Bytes(sbs);
      string S(uint o) { if ((o & 0x80000000) != 0) return Common.Strings.TryGetValue(o & 0x7fffffff, out var c) ? c : "?" + (o & 0x7fffffff); int e = (int)o; while (sb[e] != 0) e++; return Encoding.UTF8.GetString(sb, (int)o, e - (int)o); }
      var stack = new List<TNode>();
      foreach (var n in raw) {
        var node = new TNode { Type = S(n.ts), Name = S(n.ns), Level = n.lvl, Size = n.sz, Index = n.idx, Meta = n.meta };
        if (n.lvl == 0) { t.Tree = node; stack.Clear(); stack.Add(node); }
        else { while (stack.Count > n.lvl) stack.RemoveAt(stack.Count - 1); stack[^1].Kids.Add(node); stack.Add(node); }
      }
      int nd = r.I32(); for (int i = 0; i < nd; i++) r.I32();
    }
    return t;
  }
  public AType TypeOf(AObj o) => Types[o.TypeIdx];
}

/// Generic type-tree reader into Dictionary / typed arrays / primitives. On a read error it returns what it has so far
/// (Ok = false). Ok also requires that exactly the object's size was consumed.
sealed class TR {
  readonly byte[] B; int P; readonly int End; public bool Ok = true; public string Err;
  TR(byte[] b, int p, int end) { B = b; P = p; End = end; }
  public static Dictionary<string, object> Read(AF f, AObj o, TNode root, out bool ok, out string err) {
    var r = new TR(f.Data, f.DataOff + (int)o.Start, f.DataOff + (int)o.Start + (int)o.Size);
    var d = new Dictionary<string, object>();
    try { foreach (var k in root.Kids) { d[k.Name] = r.Node(k); } if ((root.Meta & 0x4000) != 0) r.Align(); if (r.P != r.End) { r.Ok = false; r.Err = $"consumed {r.P - (r.End - (int)o.Size)} of {o.Size}"; } }
    catch (Exception e) { r.Ok = false; r.Err = e.Message; }
    ok = r.Ok; err = r.Err; return d;
  }
  void Align() { P = (P + 3) & ~3; }
  void Need(int n) { if (P + n > End || n < 0) throw new Exception("read past object end"); }
  object Node(TNode n) {
    object v;
    switch (n.Type) {
      case "SInt8": Need(1); v = (sbyte)B[P++]; break;
      case "UInt8": case "char": Need(1); v = B[P++]; break;
      case "bool": Need(1); v = B[P++] != 0; break;
      case "SInt16": case "short": Need(2); v = BitConverter.ToInt16(B, P); P += 2; break;
      case "UInt16": case "unsigned short": Need(2); v = BitConverter.ToUInt16(B, P); P += 2; break;
      case "SInt32": case "int": case "Type*": Need(4); v = BitConverter.ToInt32(B, P); P += 4; break;
      case "UInt32": case "unsigned int": Need(4); v = BitConverter.ToUInt32(B, P); P += 4; break;
      case "SInt64": case "long long": Need(8); v = BitConverter.ToInt64(B, P); P += 8; break;
      case "UInt64": case "unsigned long long": case "FileSize": Need(8); v = BitConverter.ToUInt64(B, P); P += 8; break;
      case "float": Need(4); v = BitConverter.ToSingle(B, P); P += 4; break;
      case "double": Need(8); v = BitConverter.ToDouble(B, P); P += 8; break;
      case "string": { Need(4); int len = BitConverter.ToInt32(B, P); P += 4; Need(len); v = Encoding.UTF8.GetString(B, P, len); P += len; Align(); return v; }
      case "TypelessData": { Need(4); int len = BitConverter.ToInt32(B, P); P += 4; Need(len); var bb = new byte[len]; Buffer.BlockCopy(B, P, bb, 0, len); P += len; v = bb; break; }
      default:
        if (n.Kids.Count == 1 && n.Kids[0].Type == "Array") v = Arr(n.Kids[0]);
        else if (n.Type == "Array") v = Arr(n);
        else { var d = new Dictionary<string, object>(); foreach (var k in n.Kids) d[k.Name] = Node(k); v = d; }
        break;
    }
    if ((n.Meta & 0x4000) != 0) Align();
    return v;
  }
  object Arr(TNode arr) {
    Need(4); int count = BitConverter.ToInt32(B, P); P += 4;
    var el = arr.Kids[1];
    if (count < 0 || count > 50_000_000) throw new Exception("bad count " + count);
    object res;
    switch (el.Type) {
      case "UInt8": case "char": case "SInt8": case "bool": { Need(count); var a = new byte[count]; Buffer.BlockCopy(B, P, a, 0, count); P += count; res = a; break; }
      case "SInt16": case "short": { Need(count * 2); var a = new short[count]; Buffer.BlockCopy(B, P, a, 0, count * 2); P += count * 2; res = a; break; }
      case "UInt16": case "unsigned short": { Need(count * 2); var a = new ushort[count]; Buffer.BlockCopy(B, P, a, 0, count * 2); P += count * 2; res = a; break; }
      case "SInt32": case "int": case "UInt32": case "unsigned int": { Need(count * 4); var a = new int[count]; Buffer.BlockCopy(B, P, a, 0, count * 4); P += count * 4; res = a; break; }
      case "float": { Need(count * 4); var a = new float[count]; Buffer.BlockCopy(B, P, a, 0, count * 4); P += count * 4; res = a; break; }
      case "SInt64": case "long long": case "UInt64": case "unsigned long long": { Need(count * 8); var a = new long[count]; Buffer.BlockCopy(B, P, a, 0, count * 8); P += count * 8; res = a; break; }
      default: { var l = new List<object>(Math.Min(count, 100000)); for (int i = 0; i < count; i++) l.Add(Node(el)); res = l; break; }
    }
    if ((el.Meta & 0x4000) != 0 && res is Array) Align();
    if ((arr.Meta & 0x4000) != 0) Align();
    return res;
  }
}

static class DX {
  public static object G(this Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;
  public static Dictionary<string, object> Dct(this Dictionary<string, object> d, string k) => d.G(k) as Dictionary<string, object>;
  public static List<object> Lst(this Dictionary<string, object> d, string k) => d.G(k) as List<object>;
  public static string Str(this Dictionary<string, object> d, string k) => d.G(k) as string;
  public static double Num(this Dictionary<string, object> d, string k, double def = 0) => d.G(k) switch { float f => f, double x => x, int i => i, uint u => u, long l => l, ulong ul => ul, short s => s, ushort us => us, byte b => b, sbyte sb => sb, bool bo => bo ? 1 : 0, _ => def };
  public static bool Bool(this Dictionary<string, object> d, string k, bool def = false) => d.G(k) switch { bool b => b, byte b2 => b2 != 0, int i => i != 0, _ => def };
  public static (int fid, long pid) Ptr(object o) { var d = o as Dictionary<string, object>; if (d == null) return (0, 0); return ((int)d.Num("m_FileID"), d.G("m_PathID") is long l ? l : (long)d.Num("m_PathID")); }
  public static (int fid, long pid) Ptr(this Dictionary<string, object> d, string k) => Ptr(d.G(k));
  public static V3 Vec(this Dictionary<string, object> d, string k) { var v = d.Dct(k); return v == null ? default : new V3(v.Num("x"), v.Num("y"), v.Num("z")); }
  public static Q Quat(this Dictionary<string, object> d, string k) { var v = d.Dct(k); return v == null ? Q.I : new Q(v.Num("x"), v.Num("y"), v.Num("z"), v.Num("w")); }
  public static double[] Col(this Dictionary<string, object> d, string k) { var v = d.Dct(k); return v == null ? null : new[] { v.Num("r"), v.Num("g"), v.Num("b"), v.Num("a", 1) }; }
}

/// Type trees by (class id, old type hash) and, for MonoBehaviours, by script id; plus script id -> script name.
sealed class Bank {
  public readonly Dictionary<string, TNode> ByHash = new();          // "cls:hash"
  public readonly Dictionary<int, List<(string hash, TNode t)>> ByClass = new();
  public readonly Dictionary<string, List<(string hash, TNode t)>> ByScript = new(); // scriptId -> trees
  public readonly Dictionary<string, string> ScriptName = new();     // scriptId -> namespace.Class
  public readonly Dictionary<string, string> KeyScript = new();      // "114:hash" -> scriptId
  public int Files;

  public TNode Find(AType t, out string how) {
    if (ByHash.TryGetValue(t.ClassId + ":" + t.Hash, out var tr)) { how = "hash"; return tr; }
    if (t.ClassId == 114) { if (ByScript.TryGetValue(t.ScriptId, out var l) && l.Count > 0) { how = "script"; return l[0].t; } how = null; return null; }
    if (ByClass.TryGetValue(t.ClassId, out var cl) && cl.Count > 0) { how = "class"; return cl[0].t; }
    how = null; return null;
  }

  public static Bank Harvest(string bundleDir, string mbscriptsTsv, Action<string, AF> perFile = null) {
    var b = new Bank();
    var mb = new Dictionary<(string, long), string>();
    foreach (var l in File.ReadLines(mbscriptsTsv)) { var p = l.Split('\t'); if (p.Length >= 4 && long.TryParse(p[1], out long pid)) mb[(p[0], pid)] = p[3]; }
    foreach (var path in Directory.GetFiles(bundleDir, "*.bundle")) {
      var bn = Path.GetFileNameWithoutExtension(path);
      (byte[] data, List<(long off, long size, string name)> nodes) bu;
      try { bu = Bundle.Open(path); } catch { continue; }
      foreach (var nd in bu.nodes) {
        if (nd.name.EndsWith(".resS") || nd.name.EndsWith(".resource")) continue;
        AF f; try { f = AF.Parse(nd.name, bu.data, (int)nd.off); } catch { continue; }
        b.Files++;
        var firstOfType = new Dictionary<int, AObj>();
        foreach (var o in f.Objects) if (!firstOfType.ContainsKey(o.TypeIdx)) firstOfType[o.TypeIdx] = o;
        for (int ti = 0; ti < f.Types.Count; ti++) {
          var t = f.Types[ti]; if (t.Tree == null) continue;
          var key = t.ClassId + ":" + t.Hash;
          if (!b.ByHash.ContainsKey(key)) {
            b.ByHash[key] = t.Tree;
            if (!b.ByClass.TryGetValue(t.ClassId, out var cl)) b.ByClass[t.ClassId] = cl = new();
            cl.Add((t.Hash, t.Tree));
            if (t.ClassId == 114) { b.KeyScript[key] = t.ScriptId; if (!b.ByScript.TryGetValue(t.ScriptId, out var sl)) b.ByScript[t.ScriptId] = sl = new(); sl.Add((t.Hash, t.Tree)); }
          }
          if (t.ClassId == 114 && !b.ScriptName.ContainsKey(t.ScriptId) && firstOfType.TryGetValue(ti, out var fo) && mb.TryGetValue((bn, fo.PathId), out var sn)) b.ScriptName[t.ScriptId] = sn;
        }
        perFile?.Invoke(bn, f);
      }
    }
    return b;
  }

  // cache: text-free binary
  public void Save(string path) {
    using var w = new BinaryWriter(File.Create(path));
    w.Write(ByHash.Count);
    foreach (var kv in ByHash) {
      w.Write(kv.Key); var sid = KeyScript.GetValueOrDefault(kv.Key, ""); w.Write(sid);
      var flat = new List<TNode>(); void F(TNode n) { flat.Add(n); foreach (var k in n.Kids) F(k); } F(kv.Value);
      w.Write(flat.Count); foreach (var n in flat) { w.Write((byte)n.Level); w.Write(n.Type); w.Write(n.Name); w.Write(n.Size); w.Write(n.Meta); }
    }
    w.Write(ScriptName.Count); foreach (var kv in ScriptName) { w.Write(kv.Key); w.Write(kv.Value); }
    w.Write(Files);
  }
  public static Bank Load(string path) {
    var b = new Bank(); using var r = new BinaryReader(File.OpenRead(path));
    int n = r.ReadInt32();
    for (int i = 0; i < n; i++) {
      var key = r.ReadString(); var sid = r.ReadString(); int c = r.ReadInt32(); TNode root = null; var st = new List<TNode>();
      for (int j = 0; j < c; j++) {
        var nd = new TNode { Level = r.ReadByte(), Type = r.ReadString(), Name = r.ReadString(), Size = r.ReadInt32(), Meta = r.ReadInt32() };
        if (nd.Level == 0) { root = nd; st.Clear(); st.Add(nd); } else { while (st.Count > nd.Level) st.RemoveAt(st.Count - 1); st[^1].Kids.Add(nd); st.Add(nd); }
      }
      b.ByHash[key] = root; int cls = int.Parse(key.Substring(0, key.IndexOf(':'))); var h = key.Substring(key.IndexOf(':') + 1);
      if (!b.ByClass.TryGetValue(cls, out var cl)) b.ByClass[cls] = cl = new(); cl.Add((h, root));
      if (sid != "") { b.KeyScript[key] = sid; if (!b.ByScript.TryGetValue(sid, out var sl)) b.ByScript[sid] = sl = new(); sl.Add((h, root)); }
    }
    int m = r.ReadInt32(); for (int i = 0; i < m; i++) b.ScriptName[r.ReadString()] = r.ReadString();
    b.Files = r.ReadInt32();
    return b;
  }
}
