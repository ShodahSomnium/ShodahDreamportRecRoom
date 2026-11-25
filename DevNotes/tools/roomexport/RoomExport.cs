// RoomExport: decode Rec Room room-export protobufs (persisted_room_data.binpb etc.) using the export's own
// descriptor_set.binpb (a google.protobuf.FileDescriptorSet). No protoc / nuget needed.
//
//   RoomExport schema <descriptor_set.binpb> [filter]                 -> list messages (and fields) whose name contains filter
//   RoomExport decode <descriptor_set.binpb> <MessageType> <file.binpb> <out.json>
//   RoomExport guess  <descriptor_set.binpb> <file.binpb>              -> rank root message types by how cleanly they parse
//
// Reference/measurement tool only. Nothing it reads is shipped or loaded at runtime.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public sealed class PField { public string Name; public int Number; public int Label; public int Type; public string TypeName; public bool Packed; }
public sealed class PMsg { public string FullName; public List<PField> Fields = new List<PField>(); public Dictionary<int, PField> ByNum = new Dictionary<int, PField>(); public bool MapEntry; }
public sealed class PEnum { public string FullName; public Dictionary<int, string> Values = new Dictionary<int, string>(); }

public static class Wire
{
    public static ulong Varint(byte[] b, ref int p)
    {
        ulong r = 0; int s = 0;
        while (true)
        {
            if (p >= b.Length) throw new Exception("varint past end");
            byte x = b[p++]; r |= (ulong)(x & 0x7F) << s;
            if ((x & 0x80) == 0) return r;
            s += 7; if (s > 63) throw new Exception("varint too long");
        }
    }
    public struct Item { public int Num, WT; public ulong V; public int Off, Len; }
    // Parses one message level; throws on malformed data.
    public static List<Item> Parse(byte[] b, int off, int len)
    {
        var list = new List<Item>(); int p = off, end = off + len;
        while (p < end)
        {
            ulong key = Varint(b, ref p); int num = (int)(key >> 3), wt = (int)(key & 7);
            if (num <= 0) throw new Exception("bad field number");
            var it = new Item { Num = num, WT = wt };
            switch (wt)
            {
                case 0: it.V = Varint(b, ref p); break;
                case 1: if (p + 8 > end) throw new Exception("fixed64 past end"); it.V = BitConverter.ToUInt64(b, p); it.Off = p; p += 8; break;
                case 5: if (p + 4 > end) throw new Exception("fixed32 past end"); it.V = BitConverter.ToUInt32(b, p); it.Off = p; p += 4; break;
                case 2:
                    ulong l = Varint(b, ref p); if (l > (ulong)(end - p)) throw new Exception("len past end");
                    it.Off = p; it.Len = (int)l; p += (int)l; break;
                default: throw new Exception("unsupported wire type " + wt);
            }
            list.Add(it);
        }
        if (p != end) throw new Exception("overrun");
        return list;
    }
    public static string Str(byte[] b, int off, int len) => Encoding.UTF8.GetString(b, off, len);
}

public sealed class Schema
{
    public Dictionary<string, PMsg> Msgs = new Dictionary<string, PMsg>();
    public Dictionary<string, PEnum> Enums = new Dictionary<string, PEnum>();

    public static Schema Load(string path)
    {
        var s = new Schema(); var b = File.ReadAllBytes(path);
        foreach (var f in Wire.Parse(b, 0, b.Length)) if (f.Num == 1 && f.WT == 2) s.LoadFile(b, f.Off, f.Len);
        return s;
    }
    void LoadFile(byte[] b, int off, int len)
    {
        string pkg = ""; var items = Wire.Parse(b, off, len);
        foreach (var i in items) if (i.Num == 2 && i.WT == 2) pkg = Wire.Str(b, i.Off, i.Len);
        string prefix = pkg.Length > 0 ? "." + pkg : "";
        foreach (var i in items)
        {
            if (i.Num == 4 && i.WT == 2) LoadMsg(b, i.Off, i.Len, prefix);
            else if (i.Num == 5 && i.WT == 2) LoadEnum(b, i.Off, i.Len, prefix);
        }
    }
    void LoadMsg(byte[] b, int off, int len, string prefix)
    {
        var items = Wire.Parse(b, off, len); var m = new PMsg();
        foreach (var i in items) if (i.Num == 1 && i.WT == 2) m.FullName = prefix + "." + Wire.Str(b, i.Off, i.Len);
        foreach (var i in items)
        {
            if (i.Num == 2 && i.WT == 2)
            {
                var fld = new PField();
                foreach (var j in Wire.Parse(b, i.Off, i.Len))
                {
                    if (j.Num == 1 && j.WT == 2) fld.Name = Wire.Str(b, j.Off, j.Len);
                    else if (j.Num == 3 && j.WT == 0) fld.Number = (int)j.V;
                    else if (j.Num == 4 && j.WT == 0) fld.Label = (int)j.V;
                    else if (j.Num == 5 && j.WT == 0) fld.Type = (int)j.V;
                    else if (j.Num == 6 && j.WT == 2) fld.TypeName = Wire.Str(b, j.Off, j.Len);
                    else if (j.Num == 8 && j.WT == 2) foreach (var k in Wire.Parse(b, j.Off, j.Len)) if (k.Num == 2 && k.WT == 0) fld.Packed = k.V != 0;
                }
                m.Fields.Add(fld); m.ByNum[fld.Number] = fld;
            }
            else if (i.Num == 3 && i.WT == 2) LoadMsg(b, i.Off, i.Len, m.FullName);
            else if (i.Num == 4 && i.WT == 2) LoadEnum(b, i.Off, i.Len, m.FullName);
            else if (i.Num == 7 && i.WT == 2) foreach (var k in Wire.Parse(b, i.Off, i.Len)) if (k.Num == 7 && k.WT == 0) m.MapEntry = k.V != 0;
        }
        Msgs[m.FullName] = m;
    }
    void LoadEnum(byte[] b, int off, int len, string prefix)
    {
        var e = new PEnum(); var items = Wire.Parse(b, off, len);
        foreach (var i in items) if (i.Num == 1 && i.WT == 2) e.FullName = prefix + "." + Wire.Str(b, i.Off, i.Len);
        foreach (var i in items) if (i.Num == 2 && i.WT == 2)
        {
            string n = null; int v = 0;
            foreach (var j in Wire.Parse(b, i.Off, i.Len)) { if (j.Num == 1 && j.WT == 2) n = Wire.Str(b, j.Off, j.Len); else if (j.Num == 2 && j.WT == 0) v = (int)j.V; }
            e.Values[v] = n;
        }
        Enums[e.FullName] = e;
    }
    public PMsg Find(string name)
    {
        if (Msgs.TryGetValue(name, out var m)) return m;
        if (Msgs.TryGetValue("." + name, out m)) return m;
        var c = Msgs.Values.Where(x => x.FullName.EndsWith("." + name)).ToList();
        return c.Count == 1 ? c[0] : null;
    }
}

public sealed class Decoder
{
    readonly Schema S; readonly byte[] B; readonly StringBuilder O = new StringBuilder();
    public int Errors; public long FieldsDecoded, Unknown;
    public Decoder(Schema s, byte[] b) { S = s; B = b; }

    static string Esc(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            if (c == '"') sb.Append("\\\""); else if (c == '\\') sb.Append("\\\\");
            else if (c == '\n') sb.Append("\\n"); else if (c == '\r') sb.Append("\\r"); else if (c == '\t') sb.Append("\\t");
            else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
    static string F(double d) => double.IsNaN(d) || double.IsInfinity(d) ? "\"" + d.ToString(CultureInfo.InvariantCulture) + "\"" : d.ToString("R", CultureInfo.InvariantCulture);
    static string F32(float d) => float.IsNaN(d) || float.IsInfinity(d) ? "\"" + d.ToString(CultureInfo.InvariantCulture) + "\"" : ((double)d).ToString("0.######", CultureInfo.InvariantCulture);

    public string Run(PMsg root) { Msg(root, 0, B.Length, 0); return O.ToString(); }

    void Ind(int d) { O.Append('\n').Append(' ', d * 2); }

    void Msg(PMsg m, int off, int len, int depth)
    {
        List<Wire.Item> items;
        try { items = Wire.Parse(B, off, len); }
        catch (Exception e) { Errors++; O.Append("{\"#parse_error\":" + Esc(e.Message) + "}"); return; }
        // any: google.protobuf.Any
        if (m.FullName == ".google.protobuf.Any")
        {
            string url = null; Wire.Item? val = null;
            foreach (var i in items) { if (i.Num == 1 && i.WT == 2) url = Wire.Str(B, i.Off, i.Len); else if (i.Num == 2 && i.WT == 2) val = i; }
            var tm = url == null ? null : S.Find(url.Substring(url.LastIndexOf('/') + 1));
            O.Append("{\"@type\":" + Esc(url ?? "") + ",\"value\":");
            if (tm != null && val.HasValue) Msg(tm, val.Value.Off, val.Value.Len, depth + 1); else O.Append("null");
            O.Append('}'); return;
        }
        // group by field number preserving first-seen order
        var order = new List<int>(); var groups = new Dictionary<int, List<Wire.Item>>();
        foreach (var i in items) { if (!groups.TryGetValue(i.Num, out var g)) { groups[i.Num] = g = new List<Wire.Item>(); order.Add(i.Num); } g.Add(i); }
        O.Append('{'); bool first = true;
        foreach (int num in order)
        {
            var g = groups[num]; m.ByNum.TryGetValue(num, out var fd);
            if (!first) O.Append(','); first = false; Ind(depth + 1);
            if (fd == null)
            {
                Unknown++;
                O.Append(Esc("#" + num)).Append(':');
                O.Append('['); for (int k = 0; k < g.Count; k++) { if (k > 0) O.Append(','); Raw(g[k], depth + 1); }
                O.Append(']'); continue;
            }
            O.Append(Esc(fd.Name)).Append(':');
            bool rep = fd.Label == 3;
            PMsg sub = fd.Type == 11 && fd.TypeName != null && S.Msgs.TryGetValue(fd.TypeName, out var sm) ? sm : null;
            if (rep && sub != null && sub.MapEntry)
            {
                O.Append('{'); bool f2 = true;
                foreach (var it in g)
                {
                    if (!f2) O.Append(','); f2 = false; Ind(depth + 2);
                    // decode key + value
                    var kv = Wire.Parse(B, it.Off, it.Len); string key = "?"; Wire.Item? vi = null;
                    foreach (var x in kv) { if (x.Num == 1) key = ScalarText(sub.ByNum[1], x, true); else if (x.Num == 2) vi = x; }
                    O.Append(Esc(key)).Append(':');
                    if (vi.HasValue) Value(sub.ByNum[2], vi.Value, depth + 2); else O.Append("null");
                }
                O.Append('}'); continue;
            }
            var vals = new List<Action>();
            if (rep) O.Append('[');
            bool fv = true;
            foreach (var it in g)
            {
                if (it.WT == 2 && fd.Type != 9 && fd.Type != 12 && fd.Type != 11 && fd.Type != 10)
                {
                    // packed scalars
                    int p = it.Off, end = it.Off + it.Len;
                    while (p < end)
                    {
                        var x = new Wire.Item { Num = num };
                        if (fd.Type == 1 || fd.Type == 6 || fd.Type == 16) { x.WT = 1; x.V = BitConverter.ToUInt64(B, p); p += 8; }
                        else if (fd.Type == 2 || fd.Type == 7 || fd.Type == 15) { x.WT = 5; x.V = BitConverter.ToUInt32(B, p); p += 4; }
                        else { x.WT = 0; x.V = Wire.Varint(B, ref p); }
                        if (!fv) O.Append(','); fv = false; O.Append(ScalarText(fd, x, false));
                    }
                    continue;
                }
                if (!fv) O.Append(','); fv = false;
                if (rep && sub != null) Ind(depth + 2);
                Value(fd, it, depth + 1);
            }
            if (rep) O.Append(']');
            FieldsDecoded += g.Count;
        }
        if (!first) Ind(depth);
        O.Append('}');
    }

    void Value(PField fd, Wire.Item it, int depth)
    {
        if (fd.Type == 11 || fd.Type == 10)
        {
            if (it.WT != 2 || !S.Msgs.TryGetValue(fd.TypeName ?? "", out var sm)) { Errors++; Raw(it, depth); return; }
            Msg(sm, it.Off, it.Len, depth); return;
        }
        O.Append(ScalarText(fd, it, false));
    }

    string ScalarText(PField fd, Wire.Item it, bool bare)
    {
        switch (fd.Type)
        {
            case 1: return F(BitConverter.Int64BitsToDouble((long)it.V));
            case 2: return F32(BitConverter.Int32BitsToSingle((int)(uint)it.V));
            case 3: case 16: return bare ? ((long)it.V).ToString() : "\"" + ((long)it.V) + "\"";
            case 4: case 6: return bare ? it.V.ToString() : "\"" + it.V + "\"";
            case 5: case 15: return ((int)(long)it.V).ToString();
            case 13: case 7: return ((uint)it.V).ToString();
            case 8: return it.V != 0 ? "true" : "false";
            case 17: { long v = (long)(it.V >> 1) ^ -(long)(it.V & 1); return v.ToString(); }
            case 18: { long v = (long)(it.V >> 1) ^ -(long)(it.V & 1); return bare ? v.ToString() : "\"" + v + "\""; }
            case 14:
                if (fd.TypeName != null && S.Enums.TryGetValue(fd.TypeName, out var en) && en.Values.TryGetValue((int)(long)it.V, out var nm)) return bare ? nm : Esc(nm);
                return ((int)(long)it.V).ToString();
            case 9: { var s = Wire.Str(B, it.Off, it.Len); return bare ? s : Esc(s); }
            case 12:
                if (it.Len == 16) { var g = new Guid(B.Skip(it.Off).Take(16).ToArray()); return bare ? g.ToString() : Esc("guid:" + g); }
                { var bs = Convert.ToBase64String(B, it.Off, Math.Min(it.Len, 96)); if (it.Len > 96) bs += "...(" + it.Len + " bytes)"; return bare ? bs : Esc("b64:" + bs); }
        }
        return "\"?type" + fd.Type + "\"";
    }

    void Raw(Wire.Item it, int depth)
    {
        switch (it.WT)
        {
            case 0: O.Append(Esc("varint:" + it.V)); break;
            case 1: O.Append(Esc("f64:" + it.V + "/" + BitConverter.Int64BitsToDouble((long)it.V).ToString("R", CultureInfo.InvariantCulture))); break;
            case 5: O.Append(Esc("f32:" + it.V + "/" + BitConverter.Int32BitsToSingle((int)(uint)it.V).ToString("R", CultureInfo.InvariantCulture))); break;
            default: O.Append(Esc("len:" + it.Len + ":" + Convert.ToBase64String(B, it.Off, Math.Min(it.Len, 64)))); break;
        }
    }
}

// Light object tree for queries (table mode).
public sealed class Node
{
    public PMsg T; public List<KeyValuePair<PField, object>> F = new List<KeyValuePair<PField, object>>();
    public IEnumerable<object> All(string n) => F.Where(k => k.Key.Name == n).Select(k => k.Value);
    public object V(string n) => All(n).FirstOrDefault();
    public Node N(string n) => V(n) as Node;
    public float Fl(string n, float def = 0) { var v = V(n); return v is float f ? f : v is double d ? (float)d : def; }

    public static Node Build(Schema s, PMsg m, byte[] b, int off, int len)
    {
        var node = new Node { T = m };
        foreach (var it in Wire.Parse(b, off, len))
        {
            if (!m.ByNum.TryGetValue(it.Num, out var fd)) continue;
            if (fd.Type == 11)
            {
                if (it.WT == 2 && s.Msgs.TryGetValue(fd.TypeName, out var sm)) node.F.Add(new KeyValuePair<PField, object>(fd, Build(s, sm, b, it.Off, it.Len)));
                continue;
            }
            if (it.WT == 2 && fd.Type != 9 && fd.Type != 12)
            {
                int p = it.Off, end = it.Off + it.Len;
                while (p < end)
                {
                    var x = new Wire.Item { Num = it.Num };
                    if (fd.Type == 1 || fd.Type == 6 || fd.Type == 16) { x.WT = 1; x.V = BitConverter.ToUInt64(b, p); p += 8; }
                    else if (fd.Type == 2 || fd.Type == 7 || fd.Type == 15) { x.WT = 5; x.V = BitConverter.ToUInt32(b, p); p += 4; }
                    else { x.WT = 0; x.V = Wire.Varint(b, ref p); }
                    node.F.Add(new KeyValuePair<PField, object>(fd, Scalar(s, fd, x, b)));
                }
                continue;
            }
            node.F.Add(new KeyValuePair<PField, object>(fd, Scalar(s, fd, it, b)));
        }
        return node;
    }
    static object Scalar(Schema s, PField fd, Wire.Item it, byte[] b)
    {
        switch (fd.Type)
        {
            case 1: return BitConverter.Int64BitsToDouble((long)it.V);
            case 2: return BitConverter.Int32BitsToSingle((int)(uint)it.V);
            case 8: return it.V != 0;
            case 9: return Wire.Str(b, it.Off, it.Len);
            case 12: return b.Skip(it.Off).Take(it.Len).ToArray();
            case 14: if (s.Enums.TryGetValue(fd.TypeName ?? "", out var en) && en.Values.TryGetValue((int)(long)it.V, out var nm)) return nm; return (long)it.V;
            case 17: case 18: return (long)(it.V >> 1) ^ -(long)(it.V & 1);
            case 5: case 15: return (long)(int)(long)it.V;
            default: return (long)it.V;
        }
    }
    // every string in the subtree (with field name)
    public void Strings(List<string> acc, string path)
    {
        foreach (var kv in F)
        {
            if (kv.Value is string str && str.Length > 0) acc.Add(kv.Key.Name + "=" + str);
            else if (kv.Value is Node n) n.Strings(acc, path + "/" + kv.Key.Name);
        }
    }
    public int CountNamed(string n) { int c = 0; foreach (var kv in F) { if (kv.Key.Name == n) c++; if (kv.Value is Node x) c += x.CountNamed(n); } return c; }
}

public static class Table
{
    public static Dictionary<Guid, string> Prefabs(string csv)
    {
        var d = new Dictionary<Guid, string>();
        if (!File.Exists(csv)) return d;
        foreach (var line in File.ReadAllLines(csv).Skip(1))
        {
            var p = line.Split(','); if (p.Length < 3) continue;
            if (Guid.TryParse(p[0].Trim('"'), out var g)) d[g] = p[2].Trim('"');
        }
        return d;
    }
    public static (double, double, double) Euler(float w, float x, float y, float z)
    {
        // Unity Quaternion.eulerAngles convention (Z, then X, then Y)
        double sx = 2 * (w * x - y * z); sx = Math.Max(-1, Math.Min(1, sx));
        double ex = Math.Asin(sx);
        double ey = Math.Atan2(2 * (w * y + x * z), 1 - 2 * (x * x + y * y));
        double ez = Math.Atan2(2 * (w * z + x * y), 1 - 2 * (x * x + z * z));
        double N(double r) { var d = r * 180 / Math.PI; d %= 360; if (d < 0) d += 360; if (d > 359.95) d = 0; return d; }
        return (N(ex), N(ey), N(ez));
    }
    static readonly HashSet<string> Common = new HashSet<string> { "id", "tool_entity_data", "transform", "spawnable_tool_data", "creation_object_data", "tool_cleanup_data", "sandbox_colorable_data", "tagged_tool_data", "encoded_entity_idx", "child_views", "DEPRECATED_is_scene_object" };
    static string I(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    public static void Run(Schema s, string dir, string outTsv, string label)
    {
        var bin = Path.Combine(dir, "persisted_room_data.binpb");
        if (!File.Exists(bin)) { Console.WriteLine(label + ": no persisted_room_data"); return; }
        var b = File.ReadAllBytes(bin);
        var root = Node.Build(s, s.Find("rec_room.PersistedRoomData"), b, 0, b.Length);
        var pref = Prefabs(Path.Combine(dir, "PrefabIds.csv"));
        var sb = new StringBuilder("idx\ttype\tx\ty\tz\trx\try\trz\tscale\ttags\tcomponents\tstrings\tcolor\tshapes\tchildren\n");
        var counts = new Dictionary<string, int>(); int idx = 0, totalShapes = 0;
        foreach (Node v in root.All("persistence_views"))
        {
            string type = "?";
            var pid = v.N("spawnable_tool_data")?.V("prefab_id") as byte[];
            if (pid != null && pid.Length == 16) { var g = new Guid(pid); type = pref.TryGetValue(g, out var t) ? t : "guid:" + g; }
            else if (pid == null) type = "(no prefab)";
            var tr = v.N("transform"); var pos = tr?.N("position"); var q = tr?.N("quaternion_rotation");
            float px = pos?.Fl("x") ?? 0, py = pos?.Fl("y") ?? 0, pz = pos?.Fl("z") ?? 0;
            (double, double, double) e = (0, 0, 0);
            if (q != null) e = Euler(q.Fl("w"), q.Fl("x"), q.Fl("y"), q.Fl("z"));
            float sc = tr?.Fl("scale", 1) ?? 1;
            var tags = v.N("tagged_tool_data")?.N("tag_data")?.All("tags").Cast<string>().ToList() ?? new List<string>();
            var comps = v.F.Select(k => k.Key.Name).Where(n => !Common.Contains(n)).Distinct().ToList();
            var strs = new List<string>(); foreach (var kv in v.F) if (kv.Key.Name != "tagged_tool_data" && kv.Value is Node cn) cn.Strings(strs, kv.Key.Name);
            string joined = string.Join(" | ", strs.Distinct()).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
            if (joined.Length > 160) joined = joined.Substring(0, 160) + "...";
            var col = v.N("sandbox_colorable_data"); string color = "";
            if (col != null)
            {
                var c = col.V("color"); var rgb = col.N("rgbcolor");
                if (rgb != null) color = "rgb(" + I(rgb.Fl("r")) + "," + I(rgb.Fl("g")) + "," + I(rgb.Fl("b")) + ")";
                else if (c is long cl && cl >= 0) color = "pal" + cl;
            }
            int shapes = v.CountNamed("shapes") > 0 ? CountShapes(v) : 0; totalShapes += shapes;
            int kids = v.All("child_views").Count();
            sb.Append(idx++).Append('\t').Append(type).Append('\t').Append(I(px)).Append('\t').Append(I(py)).Append('\t').Append(I(pz)).Append('\t')
              .Append(I(e.Item1)).Append('\t').Append(I(e.Item2)).Append('\t').Append(I(e.Item3)).Append('\t').Append(I(sc)).Append('\t')
              .Append(string.Join(",", tags)).Append('\t').Append(string.Join(",", comps)).Append('\t').Append(joined).Append('\t').Append(color).Append('\t')
              .Append(shapes).Append('\t').Append(kids).Append('\n');
            counts[type] = counts.TryGetValue(type, out var n0) ? n0 + 1 : 1;
        }
        File.WriteAllText(outTsv, sb.ToString());
        Console.WriteLine(label + ": " + idx + " views, " + totalShapes + " maker-pen shapes; " +
            string.Join(", ", counts.OrderByDescending(k => k.Value).Select(k => k.Key + (k.Value > 1 ? " x" + k.Value : ""))));
    }
    static int CountShapes(Node v)
    {
        int c = 0;
        foreach (var kv in v.F)
        {
            if (kv.Value is Node n)
            {
                if (n.T.FullName == ".rec_room.ShapeDataCollectionData") c += n.All("shapes").Count();
                else c += CountShapes(n);
            }
        }
        return c;
    }
}

public static class Program
{
    static string TypeName(Schema s, PField f)
    {
        string[] n = { "", "double", "float", "int64", "uint64", "int32", "fixed64", "fixed32", "bool", "string", "group", "msg", "bytes", "uint32", "enum", "sfixed32", "sfixed64", "sint32", "sint64" };
        string t = f.Type < n.Length ? n[f.Type] : "?";
        if (f.TypeName != null) t = f.TypeName;
        return (f.Label == 3 ? "repeated " : "") + t;
    }

    public static int Main(string[] a)
    {
        if (a.Length < 2) { Console.WriteLine("usage: schema <desc> [filter] | decode <desc> <Type> <bin> <out> | guess <desc> <bin>"); return 2; }
        var s = Schema.Load(a[1]);
        if (a[0] == "schema")
        {
            string filter = a.Length > 2 ? a[2] : "";
            foreach (var m in s.Msgs.Values.OrderBy(x => x.FullName))
            {
                if (!m.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                Console.WriteLine("message " + m.FullName + (m.MapEntry ? " (map entry)" : ""));
                if (a.Length > 3) foreach (var f in m.Fields) Console.WriteLine("   " + f.Number + " " + f.Name + " : " + TypeName(s, f));
            }
            Console.WriteLine("(" + s.Msgs.Count + " messages, " + s.Enums.Count + " enums)");
            return 0;
        }
        if (a[0] == "enum")
        {
            foreach (var e in s.Enums.Values.Where(x => x.FullName.Contains(a[2], StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine(e.FullName + ": " + string.Join(", ", e.Values.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value)));
            return 0;
        }
        if (a[0] == "decode")
        {
            var root = s.Find(a[2]); if (root == null) { Console.WriteLine("type not found: " + a[2]); return 1; }
            var d = new Decoder(s, File.ReadAllBytes(a[3]));
            File.WriteAllText(a[4], d.Run(root));
            Console.WriteLine($"{Path.GetFileName(a[3])}: {d.FieldsDecoded} fields, {d.Unknown} unknown, {d.Errors} errors");
            return 0;
        }
        if (a[0] == "table")
        {
            // table <desc> <subroomDir> <out.tsv> [label]
            Table.Run(s, a[2], a[3], a.Length > 4 ? a[4] : Path.GetFileName(a[2]));
            return 0;
        }
        if (a[0] == "guess")
        {
            var bin = File.ReadAllBytes(a[2]);
            var top = Wire.Parse(bin, 0, bin.Length);
            var nums = top.Select(i => (i.Num, i.WT)).Distinct().ToList();
            Console.WriteLine("top-level fields: " + string.Join(" ", nums.Select(x => x.Num + "/" + x.WT)));
            var res = new List<(string, int, long, long)>();
            foreach (var m in s.Msgs.Values)
            {
                if (!nums.All(x => m.ByNum.ContainsKey(x.Num))) continue;
                var d = new Decoder(s, bin); d.Run(m);
                res.Add((m.FullName, d.Errors, d.Unknown, d.FieldsDecoded));
            }
            foreach (var r in res.OrderBy(x => x.Item2).ThenBy(x => x.Item3).ThenByDescending(x => x.Item4).Take(10))
                Console.WriteLine($"{r.Item1}: errors {r.Item2}, unknown {r.Item3}, fields {r.Item4}");
            return 0;
        }
        return 2;
    }
}
