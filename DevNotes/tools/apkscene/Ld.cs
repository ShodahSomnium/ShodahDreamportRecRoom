// apkscene: the Android Data folder as a set of serialized files, read through the bank's type trees, with PPtr
// resolution across externals (sharedassetsN.assets, GUID-named single-asset files, unity default resources).
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

sealed class Ld {
  public readonly DataDir D; public readonly Bank Bank;
  readonly Dictionary<string, AF> files = new(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<(string, long), Dictionary<string, object>> cache = new();
  public readonly Dictionary<string, int> TreeHow = new(); public readonly Dictionary<int, int> NoTree = new(); public readonly Dictionary<int, int> BadRead = new();
  public Ld(string work) { D = new DataDir(Paths.DataApk, Path.Combine(Paths.Apk, "base")); Bank = Bank.Load(Path.Combine(work, "bank.bin")); }
  public static string Norm(string p) {
    if (p == null) return null; p = p.Replace('\\', '/');
    int i = p.LastIndexOf('/'); if (i >= 0) p = p.Substring(i + 1);
    return p;
  }
  public AF File(string name) {
    name = Norm(name);
    if (files.TryGetValue(name, out var f)) return f;
    string real = name;
    if (!D.Has(real)) {
      // "unity default resources" / "unity_builtin_extra" / "globalgamemanagers.assets" are not in this pack
      if (name.Equals("Library/unity default resources", StringComparison.OrdinalIgnoreCase) || name.Equals("unity default resources", StringComparison.OrdinalIgnoreCase)) real = "unity default resources";
      else if (name.Equals("unity_builtin_extra", StringComparison.OrdinalIgnoreCase)) real = "0000000000000000f000000000000000";
    }
    if (!D.Has(real)) { files[name] = null; return null; }
    try { f = AF.Parse(name, D.Get(real)); } catch (Exception e) { Console.Error.WriteLine("parse " + name + ": " + e.Message); f = null; }
    files[name] = f; return f;
  }
  public AF Ext(AF from, int fid) { if (fid == 0) return from; if (fid - 1 >= from.Ext.Count) return null; return File(from.Ext[fid - 1].Path); }
  public string ExtName(AF from, int fid) => fid == 0 ? from.Name : fid - 1 < from.Ext.Count ? Norm(from.Ext[fid - 1].Path) : "?";
  public (AF f, AObj o) Res(AF from, (int fid, long pid) p) {
    if (p.pid == 0) return (null, null);
    var f = Ext(from, p.fid); if (f == null) return (null, null);
    return f.ById.TryGetValue(p.pid, out var o) ? (f, o) : (f, null);
  }
  public Dictionary<string, object> Read(AF f, AObj o) {
    if (f == null || o == null) return null;
    if (cache.TryGetValue((f.Name, o.PathId), out var d)) return d;
    var t = f.TypeOf(o);
    TNode tree = t.Tree; string how = "own";
    if (tree == null) tree = Bank.Find(t, out how);
    if (tree == null) { NoTree[o.ClassId] = NoTree.GetValueOrDefault(o.ClassId) + 1; cache[(f.Name, o.PathId)] = null; return null; }
    TreeHow[how] = TreeHow.GetValueOrDefault(how) + 1;
    d = TR.Read(f, o, tree, out bool ok, out string err);
    if (!ok) { BadRead[o.ClassId] = BadRead.GetValueOrDefault(o.ClassId) + 1; d["__bad"] = err; }
    cache[(f.Name, o.PathId)] = d; return d;
  }
  public Dictionary<string, object> Read(AF from, (int fid, long pid) p) { var (f, o) = Res(from, p); return Read(f, o); }
  public string NameOf(AF from, (int fid, long pid) p) { var (f, o) = Res(from, p); if (o == null) return null; var d = Read(f, o); return d?.Str("m_Name"); }
  public void Drop(AF f) { foreach (var k in cache.Keys.Where(k => k.Item1 == f.Name).ToList()) cache.Remove(k); }
  /// Script name of a MonoBehaviour object: through m_Script -> MonoScript when that file is present, else the bank's
  /// script-id -> name map (harvested from the PC bundles).
  public string Script(AF f, AObj o, Dictionary<string, object> d) {
    if (d != null) {
      var ms = Read(f, d.Ptr("m_Script"));
      if (ms != null && ms.Str("m_ClassName") is string cn && cn != "") { var ns = ms.Str("m_Namespace"); return string.IsNullOrEmpty(ns) ? cn : ns + "." + cn; }
    }
    var t = f.TypeOf(o);
    return Bank.ScriptName.TryGetValue(t.ScriptId, out var n) ? n : "?script:" + t.ScriptId.Substring(0, Math.Min(8, t.ScriptId.Length));
  }
}

static class Tools {
  public static void Info(string work, string file) {
    var ld = new Ld(work); var f = ld.File(file);
    if (f == null) { Console.WriteLine("no such file"); return; }
    Console.WriteLine($"{f.Name}: v{f.Version} {f.UnityVersion} platform {f.Platform} typetrees {f.TT}; {f.Types.Count} types, {f.Objects.Count} objects, {f.Ext.Count} externals, {f.Data.Length} bytes");
    for (int i = 0; i < f.Ext.Count; i++) Console.WriteLine($"  ext {i + 1}: '{f.Ext[i].Path}' guid {f.Ext[i].Guid} type {f.Ext[i].Type} present={ld.File(f.Ext[i].Path) != null}");
    var byT = f.Objects.GroupBy(o => o.TypeIdx).ToDictionary(g => g.Key, g => g.ToList());
    for (int i = 0; i < f.Types.Count; i++) {
      var t = f.Types[i]; var tr = t.Tree ?? ld.Bank.Find(t, out _); ld.Bank.Find(t, out var how);
      int n = byT.GetValueOrDefault(i)?.Count ?? 0; int bad = 0; string err = null;
      if (tr != null && byT.TryGetValue(i, out var os)) foreach (var o in os.Take(50)) { var d = ld.Read(f, o); if (d == null || d.ContainsKey("__bad")) { bad++; err ??= d?.Str("__bad"); } }
      string sn = t.ClassId == 114 ? " " + (ld.Bank.ScriptName.GetValueOrDefault(t.ScriptId) ?? "?" + t.ScriptId) : "";
      Console.WriteLine($"  type {i}: {ClassNames.Of(t.ClassId)}({t.ClassId}){sn} n={n} tree={(t.Tree != null ? "own" : how ?? "NONE")} bad={bad}/{Math.Min(n, 50)} {err}");
    }
  }
  public static void Dump(string work, string file, long pid, int depth) {
    var ld = new Ld(work); var f = ld.File(file); var o = f.ById[pid]; var d = ld.Read(f, o);
    Console.WriteLine($"{ClassNames.Of(o.ClassId)} size {o.Size}"); Console.WriteLine(Js(d, depth, 0));
  }
  public static string Js(object o, int depth, int ind) {
    string pad = new string(' ', ind * 2);
    switch (o) {
      case null: return "null";
      case string s: return "\"" + s + "\"";
      case Dictionary<string, object> d: if (depth <= 0) return "{..}"; return "{\n" + string.Join(",\n", d.Select(kv => pad + "  " + kv.Key + ": " + Js(kv.Value, depth - 1, ind + 1))) + "\n" + pad + "}";
      case List<object> l: if (depth <= 0 || l.Count > 40) return $"[{l.Count} items]" + (l.Count > 0 && depth > 0 ? " first=" + Js(l[0], depth - 1, ind + 1) : ""); return "[" + string.Join(", ", l.Select(x => Js(x, depth - 1, ind + 1))) + "]";
      case Array a: return $"<{a.GetType().GetElementType().Name}[{a.Length}]>" + (a.Length <= 16 ? " " + string.Join(",", a.Cast<object>()) : "");
      case float fl: return Y.F(fl);
      default: return o.ToString();
    }
  }
}
