// rrscene - effects (F records) and sounds (A record clip resolution) for the scene pass (iteration 5).
// Reads <work>/inst_fx.tsv (rrscene instfx). See README.md "F records" and "Sounds".
using System.Globalization;
using System.Text.RegularExpressions;

static class FxDb {
  public sealed class Row {
    public string Kind, Bundle, Root, Path, Leaf, Mats, Clip; public bool IsP, Loop, PlayOnAwake;
    public double Dur, MaxP, Rate, Size = 1, Life, Vol, Min, Max, Spatial;
  }
  public static readonly List<Row> Ps = new(), As = new();
  public static readonly Dictionary<string, List<Row>> PsByRoot = new(), PsByLeaf = new(), AsByRoot = new();
  public static void Load(string work) {
    var f = Path.Combine(work, "inst_fx.tsv");
    if (!File.Exists(f)) { Console.WriteLine("(no inst_fx.tsv: run 'rrscene instfx <work>' first; F records need it)"); return; }
    foreach (var l in File.ReadLines(f)) {
      var p = l.Split('\t'); if (p.Length < 13) continue;
      var r = new Row { Kind = p[0], Bundle = p[1], Root = p[3], IsP = p[4] == "1", Path = p[5] };
      r.Leaf = r.Path.Length == 0 ? r.Root : r.Path.Substring(r.Path.LastIndexOf('/') + 1);
      if (r.Kind == "ps") {
        r.Mats = p[6]; r.Loop = p[7] == "1"; r.Dur = Y.D(p[8]); r.MaxP = Y.D(p[9]); r.Rate = Y.D(p[10]);
        if (p.Length > 13) { r.Size = Y.D(p[12], 1); r.Life = Y.D(p[13]); }
        Ps.Add(r); Add(PsByRoot, r.Root, r); Add(PsByLeaf, r.Leaf, r);
      } else {
        r.Clip = p[6]; r.Vol = Y.D(p[7], 1); r.Min = Y.D(p[8], 1); r.Max = Y.D(p[9], 500); r.Loop = p[10] == "1"; r.PlayOnAwake = p[11] == "1"; r.Spatial = Y.D(p[12], 1);
        As.Add(r); Add(AsByRoot, r.Root, r);
      }
    }
  }
  static void Add(Dictionary<string, List<Row>> d, string k, Row r) { if (!d.TryGetValue(k, out var l)) d[k] = l = new(); l.Add(r); }
}

sealed class FxMatch {
  public string Prefab, Part = "", Kind, How;     // How: exact (same prefab root) | part (same object inside an install prefab) | kind (closest by kind)
  public double InstSize = 1; public bool InstLoop;
  public string Snd; public double SndVol, SndMin, SndMax; public bool SndGuess;
}

static class Fx {
  static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase) { "Particle System", "ParticleSystem", "Particles", "Particle", "VFX", "FX", "Effect", "Effects", "PS" };
  static string Strip(string n) => n.Trim().TrimStart('[', '(').TrimEnd(']', ')').Trim();
  public static bool IsGeneric(string n) => Generic.Contains(n.Trim());

  // The install ambient/fire loops Rec Room's own effects carry (inst_fx 'as' rows), and the GUESS ones for effects without one.
  static FxDb.Row LoopOf(string root) => FxDb.AsByRoot.TryGetValue(root, out var l) ? l.FirstOrDefault(x => x.Loop && x.Clip.Length > 0) : null;

  static FxMatch Root(string root, string how, string kind) {
    if (!FxDb.PsByRoot.TryGetValue(root, out var rows)) return null;
    var main = rows.FirstOrDefault(x => x.Path.Length == 0) ?? rows[0];
    if (!main.IsP) return null;
    return Sound(new FxMatch { Prefab = root, Kind = kind, How = how, InstSize = main.Size, InstLoop = rows.Any(x => x.Loop) });
  }
  static FxMatch Part(string leaf, string how, string kind, string preferRoot = null) {
    if (!FxDb.PsByLeaf.TryGetValue(leaf, out var rows)) return null;
    // the smallest install prefab holding that object (fewest particle systems), or the one asked for
    var cands = rows.Where(x => x.IsP && x.Path.Length > 0).ToList();
    if (cands.Count == 0) return null;
    var best = cands.Where(x => x.Root == preferRoot).OrderBy(x => x.Path.Length).FirstOrDefault() ?? cands.OrderBy(x => FxDb.PsByRoot[x.Root].Count).ThenBy(x => x.Root.Length).First();
    return Sound(new FxMatch { Prefab = best.Root, Part = best.Path, Kind = kind, How = how, InstSize = best.Size, InstLoop = best.Loop });
  }
  static FxMatch Sound(FxMatch m) {
    var a = LoopOf(m.Prefab);
    if (a != null) { m.Snd = a.Clip; m.SndVol = a.Vol; m.SndMin = a.Min; m.SndMax = a.Max; }
    return m;
  }

  public static string KindOf(string text) {
    var n = text.ToLowerInvariant();
    if (n.Contains("lava") || n.Contains("magma")) return "lava";
    if (n.Contains("firefl")) return "fireflies";
    if (n.Contains("flame") || n.Contains("fire") || n.Contains("torch") || n.Contains("candle") || n.Contains("brazier") || n.Contains("ember")) return "fire";
    if (n.Contains("fog") || n.Contains("mist") || n.Contains("steam")) return "fog";
    if (n.Contains("wind")) return "wind";
    if (n.Contains("leaf") || n.Contains("leaves")) return "leaves";
    if (n.Contains("fountain") || n.Contains("water") || n.Contains("foam") || n.Contains("spray") || n.Contains("drip") || n.Contains("sprinkl")) return "water";
    if (n.Contains("spark")) return "sparks";
    if (n.Contains("smoke")) return "smoke";
    if (n.Contains("confetti") || n.Contains("firework")) return "celebration";
    if (n.Contains("rain")) return "rain";
    return null;
  }

  /// <summary>Scene effect object (name, host name) to an install effect; null = no match (listed in the summary).</summary>
  public static FxMatch Match(string name, string host) {
    var n = name.Trim(); var h = host ?? "";
    string key = IsGeneric(n) ? h : n;
    // 1. the same prefab root: "[X]" or "(X)" as in the install, or "[X]" whose install root is "(X)"
    foreach (var cand in new[] { key, "(" + Strip(key) + ")", "[" + Strip(key) + "]", Strip(key) }) {
      var m = Root(cand, "exact", KindOf(cand) ?? "effect"); if (m != null) return m;
    }
    // 2. the same object inside an install prefab ("[VFX_CampfireFlame]" is under (Sandbox_Campfire)/VFX)
    if (!IsGeneric(n)) { var m = Part(n, "part", KindOf(n) ?? "effect", "(Sandbox_Campfire)"); if (m != null) return m; }
    // 3. the closest install effect by kind (measured choices, rr docs in DN/iter5/fxsound.md)
    var kind = KindOf(n + " " + h);
    FxMatch k = kind switch {
      "fire" => Part("[VFX_CampfireFlame]", "kind", "fire", "(Sandbox_Campfire)"),
      "lava" => Root("VFX_Lava_Bubbles01", "kind", "lava"),
      "fireflies" => Root("[Sandbox_Fireflies_VFX]", "kind", "fireflies"),
      "fog" => Root("[Sandbox_Fog_VFX]", "kind", "fog"),
      "wind" => Root("[Sandbox_Wind_Wavy_VFX]", "kind", "wind"),
      "leaves" => Root("[Sandbox_Leaf_VFX]", "kind", "leaves"),
      "water" => (n + " " + h).ToLowerInvariant().Contains("fountain") && Part("Particle System", "kind", "water", "(WaterBottle)") is FxMatch w && w.Prefab == "(WaterBottle)" ? w : null,   // a drinking fountain's jet = the bottle's pour stream; waterfalls have no install stand-in
      _ => null
    };
    if (k != null && kind == "lava" && k.Snd == null) { k.Snd = "Lava Loop Low"; k.SndVol = 0.35; k.SndMin = 2; k.SndMax = 18; k.SndGuess = true; }
    return k;
  }

  // ---- sounds: scene AudioSource clip names -> install clips ----------------------------------------------------
  static Dictionary<string, string> _ci;
  static readonly (string word, string clip)[] LoopKinds = {
    ("fire", "campfire_02_30_loop"), ("flame", "campfire_02_30_loop"),
    ("river", "Quest_Pirate1_Ambiance_BrookLocal01"), ("brook", "Quest_Pirate1_Ambiance_BrookLocal01"), ("stream", "Quest_Pirate1_Ambiance_BrookLocal01"),
    ("waterfall", "Quest_Pirate1_Ambiance_BrookLocal01"), ("runningwater", "Quest_Pirate1_Ambiance_BrookLocal01"),
    ("crowd", "Quest_Pirate1_CrowdAmbiance"), ("people", "sfx_onboarding_outdoor_ambiance_local_people02"),
    ("outdoorambience", "AmbianceSummerOutdoor"), ("birds", "AmbianceSummerOutdoor"),
    ("draft", "Quest_Pirate1_Caves_GhastlyAmbiance01"), ("cave", "Quest_Pirate1_Caves_GhastlyAmbiance01"), ("mine", "Quest_Goblin2_Ambiance_Mines01"),
    ("gust", "RecRoyale_LookoutTower_GustLocal01"), ("wind", "RecRoyale_LookoutTower_GustLocal01"),
    ("lava", "Lava Loop Low"),
  };
  /// <summary>(install clip name or null, how: exact | case | number | kind).</summary>
  public static (string clip, string how) Clip(string name, bool loop) {
    if (string.IsNullOrEmpty(name)) return (null, null);
    if (InstDb.Audio.Contains(name)) return (name, "exact");
    if (_ci == null) { _ci = new(StringComparer.OrdinalIgnoreCase); foreach (var a in InstDb.Audio) _ci.TryAdd(a, a); }
    if (_ci.TryGetValue(name, out var c)) return (c, "case");
    // the same clip family with another take number (..._Local03 -> ..._Local01)
    var stem = Regex.Replace(name, @"_?\d+$", "");
    if (stem != name) { var hit = InstDb.Audio.Where(a => a.StartsWith(stem, StringComparison.OrdinalIgnoreCase) && Regex.Replace(a, @"_?\d+$", "").Equals(stem, StringComparison.OrdinalIgnoreCase)).OrderBy(a => a).FirstOrDefault(); if (hit != null) return (hit, "number"); }
    if (!loop) return (null, null);
    var low = name.ToLowerInvariant().Replace("_", "");
    foreach (var (w, clip) in LoopKinds) if (low.Contains(w) && InstDb.Audio.Contains(clip)) return (clip, "kind");
    return (null, null);
  }
}

// Debug: rrscene psdump <work> <sceneRegex> <objectNameRegex> - the particle-system values the F pass reads.
static class PsDump {
  public static void Run(string work, string sceneRx, string nameRx) {
    Ref2.LoadGuids(Path.Combine(work, "guidmap.tsv"));
    var rx = new Regex(nameRx, RegexOptions.IgnoreCase);
    foreach (var f in Directory.EnumerateFiles(Path.Combine(Ref2.Root, ".unity files"), "*.unity", SearchOption.AllDirectories).Where(x => Regex.IsMatch(Path.GetFileName(x), sceneRx, RegexOptions.IgnoreCase))) {
      var m = Parse.Load(f);
      foreach (var p in m.Ps) {
        var n = m.Go.TryGetValue(p.Go, out var g) ? g.Name : "?"; if (!rx.IsMatch(n)) continue;
        Console.WriteLine($"== {Path.GetFileName(f)} {n} (go {p.Go})");
        foreach (var k in new[] { "looping", "playOnAwake", "lengthInSec", "scalingMode", "InitialModule.startSize.scalar", "InitialModule.startLifetime.scalar", "InitialModule.startSpeed.scalar", "InitialModule.maxNumParticles",
                                  "EmissionModule.rateOverTime.scalar", "ShapeModule.enabled", "ShapeModule.type", "ShapeModule.radius.value", "ShapeModule.m_Scale", "ShapeModule.m_Position", "ShapeModule.m_Rotation" })
          Console.WriteLine($"   {k} = {p.V.GetValueOrDefault(k, "-")}");
      }
    }
  }
}
