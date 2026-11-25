// rrscene paintball <tsvDir> <exDir> <outDir>
// Converts the paintball gameplay layers found in the decoded room exports (REF1 room "paintballtroll1": one subroom per
// official Paintball map, saved on the official map scenes) into RRSK 1 files Paintball_<Map>_gameplay.rrsk.
// Input: the per-subroom tables written by DN/tools/roomexport (`table` command: idx type x y z rx ry rz scale tags
// components strings color shapes children; room frame = the official scene's Unity world; rotation = Unity euler deg),
// the subroom's PrefabIds.csv (prefab GUID -> type) and DN/tools/roomexport/spawnable_typeids_from_bundles.tsv
// (prefab GUID -> install prefab root). Official-looking gameplay objects -> G (+ P when the install has the prefab);
// the troll's own additions -> G extra (prefab=... key) only, so a consumer can skip them.
using System.Text.RegularExpressions;

static class Paintball {
  static readonly Dictionary<string, string> Kinds = new() {
    ["FLAG_GOAL"] = "flag_base", ["DEPRECATED_FLAG_RED"] = "flag", ["DEPRECATED_FLAG_BLUE"] = "flag", ["FLAG"] = "flag",
    ["GAME_CONFIGURATION"] = "game_config", ["GAME_ROLE_NODE"] = "role", ["PLAYER_ROLE_MAP_NODE"] = "role_map",
    ["SCOREBOARD_PROJECTOR"] = "scoreboard", ["LEADERBOARD_PROJECTOR"] = "leaderboard", ["ROOM_DOOR"] = "door",
  };
  static string KindOf(string type) {
    if (Kinds.TryGetValue(type, out var k)) return k;
    if (type.EndsWith("_GAME_RULE_CIRCUIT")) return "game_rule";
    if (type.StartsWith("PAINTBALL_") || type == "PAINT_THROWER") return "weapon_pickup";
    if (type.StartsWith("STANDEE_")) return "standee";
    return null;
  }
  static Q Euler(double x, double y, double z) {   // Unity: Z, then X, then Y  => q = qy * qx * qz
    double r = Math.PI / 180;
    Q ax(double a, int i) { var s = Math.Sin(a * r / 2); var c = Math.Cos(a * r / 2); return i == 0 ? new Q(s, 0, 0, c) : i == 1 ? new Q(0, s, 0, c) : new Q(0, 0, s, c); }
    return (ax(y, 1) * ax(x, 0) * ax(z, 2)).Norm();
  }
  public static void Run(string tsvDir, string exDir, string outDir, string spawnTsv) {
    var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var l in File.ReadLines(spawnTsv)) { var p = l.Split('\t'); if (p.Length >= 2) roots[p[0]] = p[1].Split('#')[0]; }
    foreach (var f in Directory.GetFiles(tsvDir, "paintballtro__*.tsv")) {
      var m = Regex.Match(Path.GetFileNameWithoutExtension(f), @"__(\d+)_(.+)$"); if (!m.Success) continue;
      string id = m.Groups[1].Value, map = m.Groups[2].Value;
      var typeGuid = new Dictionary<string, string>();
      var sub = Directory.GetDirectories(exDir, "SubRoom_" + id + "_*").FirstOrDefault();
      if (sub != null && File.Exists(Path.Combine(sub, "PrefabIds.csv")))
        foreach (var l in File.ReadLines(Path.Combine(sub, "PrefabIds.csv")).Skip(1)) { var p = l.Split(','); if (p.Length >= 3) typeGuid[p[2].Trim('"')] = p[0].Trim('"'); }
      var g = new List<string>(); var pl = new List<string>(); var ex = new List<string>();
      V3 mn = default, mx = default; bool hb = false; int nG = 0, nP = 0, nX = 0; var kinds = new Dictionary<string, int>();
      foreach (var l in File.ReadLines(f).Skip(1)) {
        var c = l.Split('\t'); if (c.Length < 12) continue;
        string type = c[1]; var pos = new V3(Y.D(c[2]), Y.D(c[3]), Y.D(c[4])); var q = Euler(Y.D(c[5]), Y.D(c[6]), Y.D(c[7])); double sc = Y.D(c[8], 1);
        string root = typeGuid.TryGetValue(type, out var gd) && roots.TryGetValue(gd, out var rn) ? rn : null;
        var keys = new List<string> { "type=" + type };
        if (root != null) keys.Add("prefab=" + Y.Enc(root));
        if (Math.Abs(sc - 1) > 1e-4) keys.Add("scale=" + Y.F(sc));
        if (c[11].Length > 0) {
          var kv = new Dictionary<string, List<string>>();
          foreach (var part in c[11].Split(" / ")) { int e = part.IndexOf('='); string k = e > 0 ? part.Substring(0, e) : "text", v = e > 0 ? part.Substring(e + 1) : part; if (!kv.TryGetValue(k, out var lst)) kv[k] = lst = new(); lst.Add(v); }
          foreach (var kvp in kv) keys.Add(Regex.Replace(kvp.Key, @"[^\w]", "_") + "=" + Y.Enc(string.Join(",", kvp.Value)));
        }
        string ps = $"{Y.F(pos.x)} {Y.F(pos.y)} {Y.F(pos.z)} {Y.F(q.x)} {Y.F(q.y)} {Y.F(q.z)} {Y.F(q.w)}";
        var kind = KindOf(type);
        if (kind != null) {
          g.Add($"G {kind} {Y.Enc(root ?? type)} {ps} {string.Join(" ", keys)}"); nG++; kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
          if (root != null && kind != "game_config" && kind != "game_rule" && kind != "role" && kind != "role_map") { pl.Add($"P {Y.Enc(root)} {ps} {Y.F(sc)} {Y.F(sc)} {Y.F(sc)}"); nP++; }
        } else { ex.Add($"G extra {Y.Enc(root ?? type)} {ps} {string.Join(" ", keys)}"); nX++; }
        if (!hb) { mn = mx = pos; hb = true; } else { mn = new(Math.Min(mn.x, pos.x), Math.Min(mn.y, pos.y), Math.Min(mn.z, pos.z)); mx = new(Math.Max(mx.x, pos.x), Math.Max(mx.y, pos.y), Math.Max(mx.z, pos.z)); }
      }
      var sb = new StringBuilder();
      sb.Append($"# RRSK 1 Paintball_{map}_gameplay\n");
      sb.Append($"B {Y.F(mn.x)} {Y.F(mn.y)} {Y.F(mn.z)} {Y.F(mx.x)} {Y.F(mx.y)} {Y.F(mx.z)}\n");
      sb.Append($"# source: REF1 room export paintballtroll1, subroom {id} ({map}); frame = the official Paintball {map} scene (geometry not included).\n");
      sb.Append("# G kinds: flag_base, flag, weapon_pickup, standee, game_config, game_rule, role, role_map, scoreboard, leaderboard, door = official-looking layer; extra = player additions.\n");
      foreach (var s in pl) sb.Append(s).Append('\n');
      foreach (var s in g) sb.Append(s).Append('\n');
      foreach (var s in ex) sb.Append(s).Append('\n');
      File.WriteAllText(Path.Combine(outDir, $"Paintball_{map}_gameplay.rrsk"), sb.ToString(), new UTF8Encoding(false));
      Console.WriteLine($"Paintball_{map}: G {nG} ({string.Join(", ", kinds.OrderByDescending(x => x.Value).Select(x => x.Key + " " + x.Value))}), P {nP}, extra {nX}");
    }
  }
}
