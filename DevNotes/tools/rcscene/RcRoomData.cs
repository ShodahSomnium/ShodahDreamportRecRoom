// rcscene roomdata: the July 2025 Rec Center's room data save (HTTPCache/182.data, plain protobuf, the objects Rec Room
// spawns at load: doors, hoops, radios, ping-pong, Friend-O-Tron, mirror...). Read in place; only poses, kinds and the
// doors' texts come out (names and numbers). No schema: the message layout was worked out from the bytes (see
// DevNotes/tools/rcscene/README.md, "room data").
using System.Globalization;
using System.Text;

/// A protobuf field: number, wire type, varint / fixed value or the bytes of a length-delimited one.
sealed class Pf {
  public int No, Wire; public ulong V; public int Off, Len; public byte[] B;
  public float F32 => BitConverter.ToSingle(BitConverter.GetBytes((uint)V), 0);
  public ReadOnlySpan<byte> Bytes => new ReadOnlySpan<byte>(B, Off, Len);
  public string Str => Encoding.UTF8.GetString(B, Off, Len);
  public List<Pf> Sub => Pb.Parse(B, Off, Len);
  public string Guid => Len == 16 ? Convert.ToHexString(B, Off, 16).ToLowerInvariant() : null;
}

static class Pb {
  public static ulong Varint(byte[] b, ref int p, int end) {
    ulong v = 0; int s = 0;
    while (p < end) { byte x = b[p++]; v |= (ulong)(x & 0x7f) << s; if ((x & 0x80) == 0) return v; s += 7; if (s > 63) throw new FormatException("varint"); }
    throw new FormatException("varint end");
  }
  /// The fields of a message, or null when the bytes are not a well-formed message.
  public static List<Pf> Parse(byte[] b, int off, int len) {
    var r = new List<Pf>(); int p = off, end = off + len;
    try {
      while (p < end) {
        ulong key = Varint(b, ref p, end); int no = (int)(key >> 3), w = (int)(key & 7);
        if (no <= 0 || no > 100000) return null;
        var f = new Pf { No = no, Wire = w, B = b };
        switch (w) {
          case 0: f.V = Varint(b, ref p, end); break;
          case 1: if (p + 8 > end) return null; f.V = BitConverter.ToUInt64(b, p); f.Off = p; f.Len = 8; p += 8; break;
          case 5: if (p + 4 > end) return null; f.V = BitConverter.ToUInt32(b, p); f.Off = p; f.Len = 4; p += 4; break;
          case 2: { ulong l = Varint(b, ref p, end); if (l > (ulong)(end - p)) return null; f.Off = p; f.Len = (int)l; p += (int)l; break; }
          default: return null;
        }
        r.Add(f);
      }
    } catch { return null; }
    return r;
  }
  public static Pf Get(List<Pf> m, int no) => m?.FirstOrDefault(x => x.No == no);
  public static IEnumerable<Pf> All(List<Pf> m, int no) => m == null ? Enumerable.Empty<Pf>() : m.Where(x => x.No == no);
  static bool Printable(Pf f) { if (f.Len == 0) return false; for (int i = 0; i < f.Len; i++) { byte c = f.B[f.Off + i]; if (c < 9 || (c > 13 && c < 32)) return false; } try { var s = f.Str; return !s.Contains('\uFFFD'); } catch { return false; } }

  /// A generic view: nested messages when they parse, strings when printable, floats for fixed32.
  public static void Dump(List<Pf> m, string ind, int depth, StringBuilder sb) {
    foreach (var f in m) {
      sb.Append(ind).Append(f.No).Append(':');
      if (f.Wire == 0) sb.Append(" v ").Append(f.V);
      else if (f.Wire == 5) sb.Append(" f ").Append(f.F32.ToString("0.####", CultureInfo.InvariantCulture));
      else if (f.Wire == 1) sb.Append(" d ").Append(BitConverter.Int64BitsToDouble((long)f.V).ToString("0.####", CultureInfo.InvariantCulture));
      else {
        if (Printable(f)) { sb.Append(" \"").Append(f.Str.Replace("\n", "\\n")).Append("\"\n"); continue; }
        var sub = depth > 0 && f.Len > 0 ? f.Sub : null;
        if (sub != null && sub.Count > 0) { sb.Append(" {\n"); Dump(sub, ind + "  ", depth - 1, sb); sb.Append(ind).Append("}\n"); continue; }
        sb.Append(f.Len == 16 ? " guid " + f.Guid : " bytes " + f.Len);
      }
      sb.Append('\n');
    }
  }
}

static class RcRoomData {
  public static string PathOf() => Path.Combine(Rc.CacheDir, "182.data");

  public static void DumpCmd(string[] a) {
    var b = File.ReadAllBytes(PathOf()); var top = Pb.Parse(b, 0, b.Length);
    int depth = a.Length > 1 ? int.Parse(a[1]) : 6; int skip = a.Length > 2 ? int.Parse(a[2]) : 0, take = a.Length > 3 ? int.Parse(a[3]) : 40;
    Console.WriteLine($"182.data: {b.Length} bytes, {top.Count} top fields: " + string.Join(", ", top.GroupBy(x => x.No).Select(g => $"{g.Key} x{g.Count()}")));
    var sb = new StringBuilder(); Pb.Dump(top.Skip(skip).Take(take).ToList(), "", depth, sb); Console.Write(sb);
  }
}

static class RcRoomScan {
  static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
  public static string Vec(Pf f, int n) { var s = f?.Sub; if (s == null) return "-"; var v = new float[n]; if (n == 4) v[3] = 0; foreach (var x in s) if (x.Wire == 5 && x.No >= 1 && x.No <= n) v[x.No - 1] = x.F32; return string.Join(",", v.Select(F)); }
  public static void Run(string[] a) {
    var b = File.ReadAllBytes(RcRoomData.PathOf()); var top = Pb.Parse(b, 0, b.Length);
    var r28 = Pb.Get(top, 28).Sub; var g2 = Pb.All(r28, 2).ToList();
    Console.WriteLine($"28: {r28.Count} fields; 28.2 x{g2.Count}");
    int k = 0;
    foreach (var c in g2) {
      var cs = c.Sub; Console.WriteLine($"28.2[{k++}]: " + string.Join(" ", cs.GroupBy(x => x.No).Select(g => $"{g.Key}x{g.Count()}")));
      foreach (var o in cs.Where(x => x.No == int.Parse(a.Length > 1 ? a[1] : "11"))) {
        var s = o.Sub; if (s == null) continue;
        var nm = Pb.Get(s, 6); var tr = Pb.Get(s, 8)?.Sub;
        Console.WriteLine($"  {(nm != null && nm.Wire == 2 ? nm.Str : "?")}\tp={Vec(Pb.Get(tr, 2), 3)}\tq={Vec(Pb.Get(tr, 3), 4)}\ts={Vec(Pb.Get(tr, 4), 3)}\t12={Pb.Get(Pb.Get(s, 12)?.Sub, 1)?.Guid}\t13={Pb.Get(Pb.Get(s, 13)?.Sub, 1)?.Guid}\tf=" + string.Join(",", s.Select(x => x.No).Distinct()));
      }
    }
  }
}

static class RcRoomTop {
  public static void Run(string[] a) {
    var b = File.ReadAllBytes(RcRoomData.PathOf()); var top = Pb.Parse(b, 0, b.Length);
    foreach (var o in Pb.All(top, 2)) {
      var s = o.Sub; var p53 = Pb.Get(s, 53)?.Sub; var p65 = Pb.Get(s, 65)?.Sub;
      Console.WriteLine($"{Pb.Get(s, 1)?.Guid}\tpf={Pb.Get(Pb.Get(s, 11)?.Sub, 1)?.Guid}\t53.1={RcRoomScan.Vec(Pb.Get(p53, 1), 3)}\t53.2={RcRoomScan.Vec(Pb.Get(p53, 2), 4)}\t65.4={RcRoomScan.Vec(Pb.Get(p65, 4), 3)}\t65.5={RcRoomScan.Vec(Pb.Get(p65, 5), 3)}\t65.2={Pb.Get(p65, 2)?.Guid}\t65.3={Pb.Get(p65, 3)?.Guid}\t100={Pb.Get(s, 100)?.V}\tf=" + string.Join(",", s.Select(x => x.No).Distinct()) + "\t53f=" + string.Join(",", p53?.Select(x => x.No) ?? new int[0]) + "\t65f=" + string.Join(",", p65?.Select(x => x.No) ?? new int[0]));
    }
  }
}

static class RcFindGuid {
  static byte[] Hex(string h) => Convert.FromHexString(h);
  static string Swap(string h) => h.Substring(6, 2) + h.Substring(4, 2) + h.Substring(2, 2) + h.Substring(0, 2) + h.Substring(10, 2) + h.Substring(8, 2) + h.Substring(14, 2) + h.Substring(12, 2) + h.Substring(16);
  static int Find(byte[] d, int s, int e, byte[] p) { for (int i = s; i + p.Length <= e; i++) { int j = 0; while (j < p.Length && d[i + j] == p[j]) j++; if (j == p.Length) return i; } return -1; }
  /// The scene's objects (MonoBehaviours first) holding a room-data GUID: raw 16 bytes or as hex text (either byte order).
  public static void Run(string[] a) {
    var f = Rc.Scene; var nodes = RcConvert.NodeList(); var byGo = new Dictionary<long, Node>(); foreach (var n in nodes) byGo[n.Go] = n;
    foreach (var g in a.Skip(1)) {
      var pats = new List<(string k, byte[] p)> { ("raw", Hex(g)), ("hex", Encoding.ASCII.GetBytes(g)), ("hexsw", Encoding.ASCII.GetBytes(Swap(g))) };
      var sw = Swap(g); pats.Add(("dash", Encoding.ASCII.GetBytes($"{sw.Substring(0, 8)}-{sw.Substring(8, 4)}-{sw.Substring(12, 4)}-{sw.Substring(16, 4)}-{sw.Substring(20)}")));
      int hits = 0;
      foreach (var o in f.Objects) {
        int s = (int)(f.DataOff + o.Start), e = s + (int)o.Size;
        foreach (var (k, p) in pats) {
          if (Find(f.Data, s, e, p) < 0) continue;
          string path = "?";
          if (o.ClassId == 114) { long go = BitConverter.ToInt64(f.Data, s + 4); if (byGo.TryGetValue(go, out var n)) path = n.Path + $" @ {n.M[3]:F3},{n.M[7]:F3},{n.M[11]:F3}"; }
          Console.WriteLine($"{g}\t{k}\t{ClassNames.Of(o.ClassId)}\t{o.PathId}\t{path}"); hits++;
        }
      }
      if (hits == 0) Console.WriteLine($"{g}\t-");
    }
  }
}

/// The room data's named scene objects with their world poses (28.2.11: 6 = name, 8.2 = position, 8.3 = rotation x,y,z,w;
/// the July 2025 save stores them in the scene's world space = the level7 frame), as "G spot <kind>" markers.
static class RcRoomSpots {
  public sealed class Obj { public string Name; public V3 P; public Q R; }
  static string F(double v) => Y.F3(v);

  public static List<Obj> Objects() {
    var b = File.ReadAllBytes(RcRoomData.PathOf()); var top = Pb.Parse(b, 0, b.Length); var res = new List<Obj>();
    foreach (var g2 in Pb.All(Pb.Get(top, 28)?.Sub, 2)) foreach (var o in Pb.All(g2.Sub, 11)) {
      var s = o.Sub; var nm = Pb.Get(s, 6); if (nm == null || nm.Wire != 2) continue; var tr = Pb.Get(s, 8)?.Sub;
      double[] V(Pf f, int n) { var v = new double[n]; if (n == 4) v[3] = 1; var ss = f?.Sub; if (ss != null) { if (n == 4) v[3] = 0; foreach (var x in ss) if (x.Wire == 5 && x.No >= 1 && x.No <= n) v[x.No - 1] = x.F32; } return v; }
      var p = V(Pb.Get(tr, 2), 3); var q = V(Pb.Get(tr, 3), 4);
      // a missing rotation field is the identity; a stored one with every field 0 is the default (w is written only when not 0)
      if (q[0] == 0 && q[1] == 0 && q[2] == 0 && q[3] == 0) q[3] = 1;
      res.Add(new Obj { Name = nm.Str, P = new V3(p[0], p[1], p[2]), R = new Q(q[0], q[1], q[2], q[3]) });
    }
    return res;
  }

  static string Kind(string n) => n switch {
    "[ActivityDoor_RRS]" or "[ActivityDoor_Menu_RRS]" or "[LoungeDoor_RRS]" => "door",
    "[Backboard]" => "hoop", "Basketball Score Detection" => "hoop_score", "Bell" => "bell", "Replicator" => "vending",
    "[CafeStorefront_RRS]" => "cafe_store", "(CommunityBoard_RRS)" => "community_board", "[DrinkingFountain]" => "drinking_fountain",
    "[LaserFan_Mag_RRS]" or "[Arena_Cyber_LaserFan2000_Mag_RRS]" => "laser_fan", "SocialTryOn" => "try_on", "Welcome Mat" => "welcome_mat",
    "[Grid_Menu]" => "store_menu", "Invisible Collision" => "invisible_collision", _ => null };

  static string Cat(string sign) => sign.ToUpperInvariant() switch {
    "DORM ROOM" => "Dorm", "SPORTS & PVP" => "SportsPvp", "QUESTS" => "Quests", "FEATURED ROOMS" => "FeaturedRooms",
    "MAKERWORLD" => "MakerWorld", "CAMPUS JAMS" => "CampusJams", _ => "?" };

  public static List<string> Lines(List<(string t, V3 p)> signs) {
    var objs = Objects(); var res = new List<string>();
    var score = objs.Where(o => o.Name == "Basketball Score Detection").ToList();
    // the score volume's offset in its backboard's frame (for a backboard without one: its rim estimated from the other)
    V3? rimLocal = null;
    foreach (var bb in objs.Where(o => o.Name == "[Backboard]")) { var s = score.FirstOrDefault(x => (x.P - bb.P).Len() < 6); if (s != null) rimLocal = new Q(-bb.R.x, -bb.R.y, -bb.R.z, bb.R.w).Rot(s.P - bb.P); }
    foreach (var o in objs) {
      var k = Kind(o.Name); if (k == null) continue;
      string extra = "";
      if (k == "door") {
        var near = signs.Select(s => (s.t, d: (s.p - o.P).Len())).OrderBy(x => x.d).FirstOrDefault();
        if (near.t != null && near.d < 3.5) extra = $" cat={Cat(near.t)} sign={Y.Enc(near.t)} sign_d={F(near.d)}";
        else extra = " cat=?";
        // a Studio door's room side is its -Z (the QUESTS door at the theatre's far wall stands unrotated, the room at lower z)
        extra += $" front_yaw={((Yaw(o.R) + 180) % 360):F1}";
        extra += o.Name == "[LoungeDoor_RRS]" ? " double=1" : o.Name == "[ActivityDoor_Menu_RRS]" ? " menu=1" : "";
      } else if (k == "hoop" && rimLocal.HasValue) {
        var s = score.FirstOrDefault(x => (x.P - o.P).Len() < 6); var rim = s != null ? s.P : o.P + o.R.Rot(rimLocal.Value);
        extra = $" score={F(rim.x)},{F(rim.y)},{F(rim.z)}" + (s == null ? " score.est=1" : "");
      }
      double yaw = Yaw(o.R);
      res.Add($"G spot {k} {F(o.P.x)} {F(o.P.y)} {F(o.P.z)} {o.R} yaw={yaw:F1} src=roomdata name={Y.Enc(o.Name)}{extra}");
    }
    return res;
  }

  static double Yaw(Q r) { double y = Math.Atan2(2 * (r.w * r.y + r.x * r.z), 1 - 2 * (r.y * r.y + r.x * r.x)) * 180 / Math.PI; return y < 0 ? y + 360 : y; }
  public static void Dump() { foreach (var o in Objects()) Console.WriteLine($"{o.Name}\t{F(o.P.x)},{F(o.P.y)},{F(o.P.z)}\t{o.R}"); }
}

/// framecheck <cache.rrsk> <level7.rrsk>: the cache -> room transform, fitted on the pieces both layouts place from the
/// install (R records of the same mesh, nearest pairs within 1 m after a first identity pass): yaw, offset, residuals.
static class RcFrameCheck {
  static List<(string mesh, V3 p)> Rs(string path) {
    var meshes = new Dictionary<int, string>(); var res = new List<(string, V3)>();
    foreach (var l in File.ReadLines(path)) {
      var t = l.Split(' ');
      if (t[0] == "M" && t.Length >= 3) meshes[int.Parse(t[1])] = t[2];
      else if (t[0] == "R" && t.Length >= 6 && meshes.TryGetValue(int.Parse(t[1]), out var m)) res.Add((m, new V3(double.Parse(t[3], CultureInfo.InvariantCulture), double.Parse(t[4], CultureInfo.InvariantCulture), double.Parse(t[5], CultureInfo.InvariantCulture))));
    }
    return res;
  }
  public static void Run(string[] a) {
    var A = Rs(a[1]); var B = Rs(a[2]); var pairs = new List<(V3 a, V3 b, string m)>();
    foreach (var x in A) { var c = B.Where(y => y.mesh == x.mesh).Select(y => (y.p, d: (y.p - x.p).Len())).OrderBy(y => y.d).FirstOrDefault(); if (c.d > 0 && c.d < 1.0 || (c.d == 0 && B.Any(y => y.mesh == x.mesh))) pairs.Add((x.p, c.p, x.mesh)); }
    if (pairs.Count < 3) { Console.WriteLine($"framecheck: {pairs.Count} pairs"); return; }
    // 2D rigid fit (Kabsch in XZ) + mean Y offset
    double ax = pairs.Average(p => p.a.x), az = pairs.Average(p => p.a.z), bx = pairs.Average(p => p.b.x), bz = pairs.Average(p => p.b.z);
    double sxx = 0, sxz = 0; foreach (var p in pairs) { double u = p.a.x - ax, v = p.a.z - az, U = p.b.x - bx, V = p.b.z - bz; sxx += u * U + v * V; sxz += u * V - v * U; }
    double th = Math.Atan2(sxz, sxx), c0 = Math.Cos(th), s0 = Math.Sin(th);
    double tx = bx - (c0 * ax - s0 * az), tz = bz - (s0 * ax + c0 * az), ty = pairs.Average(p => p.b.y - p.a.y);
    var res = pairs.Select(p => { double X = c0 * p.a.x - s0 * p.a.z + tx, Z = s0 * p.a.x + c0 * p.a.z + tz; return Math.Sqrt((X - p.b.x) * (X - p.b.x) + (p.a.y + ty - p.b.y) * (p.a.y + ty - p.b.y) + (Z - p.b.z) * (Z - p.b.z)); }).OrderBy(x => x).ToList();
    Console.WriteLine($"framecheck: {pairs.Count} install pieces in both ({pairs.Select(p => p.m).Distinct().Count()} meshes): rotation {th * 180 / Math.PI:F4} deg, offset ({tx:F4}, {ty:F4}, {tz:F4}); residual median {res[res.Count / 2] * 1000:F1} mm, 90% {res[(int)(res.Count * 0.9)] * 1000:F1} mm, max {res[^1] * 1000:F1} mm");
  }
}
