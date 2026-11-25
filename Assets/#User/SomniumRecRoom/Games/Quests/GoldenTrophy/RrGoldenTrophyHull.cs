using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy
{
    /// <summary>
    /// Rec Room's walls, floors and ceilings for the Golden Trophy rooms, and the level's stand-ins, drawn from the
    /// scene data DevNotes/tools/gtscene derived from the Android build (names and numbers only):
    /// - "plate" records: the hull. Each room's shell is an editor-merged static batch per material
    ///   ("Hallway1StaticBatch_Wood_Floor_Wide_mat"...) that the PC install does not have; gtscene measured it into
    ///   exact planar rectangles (Hallway1: 16 hull meshes -> 4,976 rectangles, 4,579 m2). Each one is a one-sided
    ///   quad facing its normal, as the level's own mesh (Rec Room's rooms also carry outward-facing Unlit_Black back
    ///   faces, which stay invisible from inside). Quads are merged per material and 12 m cell (rectangles over 12 m
    ///   are cut), so URP's per-object limit (4 additional lights in the client's Forward renderer) picks lights near
    ///   each piece. Measured: 4 m pieces in 8 m cells (harness run 4) doubled the meshes (2,764 vs 1,441 for all 11
    ///   rooms) and changed nothing visible, so the pieces stay large.
    /// - surfaces: the level material when the install has it (Metal_Dull_Dark_Mat, Stones_Modular_Theater_Mat,
    ///   Unlit_Black_mat...), else an install material of the same kind with the level's own texture (when the install
    ///   has that texture: Plaster_tex, Paint_Tex, "Wood FloorTiled_tex"...) and the level's colour (Wood_Floor_Wide_mat
    ///   0.578/0.460/0.395, Wallpaper_Dark_Mat 0.182, DormRoom_PlasterWalls_DarkGray_Mat 0.345, Brick_Painted_DarkRed
    ///   0.597/0.261/0.170...), tiled in metres (UVs are scene metres / tile, so neighbouring quads line up).
    /// - "dress" records: the other meshes the install lacks (bounds, rotation, colour, material), rebuilt by kind in
    ///   their real places: wall torches, banners and rugs, the theatre's metal scaffolding trusses, feast tables, open
    ///   crates, book stacks, lamp posts and lanterns, crenellations, ladders, rock sculpts (voxel boxes). Lamps and
    ///   flames glow but add no light: the level's own lights light the rooms.
    /// Nothing here collides: the level's colliders (its box colliders and gtscene's collider plates) are built by the kit.
    /// </summary>
    public sealed class RrGoldenTrophyHull
    {
        public const float Cell = 12f;

        /// <summary>Largest side of one hull piece (bigger rectangles are cut, see <see cref="Build"/>).</summary>
        public const float MaxPiece = 12f;
        public const int MaxVerts = 60000;

        public struct Plate
        {
            public string Mat;
            public Vector3 C;
            public Quaternion Q;
            public float Hx, Hy;
            /// <summary>The face's vertex colour (VertColor materials; white or alpha 0 = none).</summary>
            public Color Tint;
        }

        public struct Item
        {
            public string Kind;
            public Vector3 C;
            public Quaternion Q;
            public Vector3 E;
            public Color Col;
            public string Mat;
        }

        public sealed class MatInfo
        {
            public Color Col = Color.white;
            public string Tex;
            public bool Inst;
            /// <summary>The level material's texture scale (_MainTex / _BaseMap): Rec Room's hull meshes are ProBuilder's, with UVs in metres, so one repeat is 1 / scale metres.</summary>
            public Vector2 Scale = Vector2.one;
        }

        /// <summary>How a level material is drawn on our quads (see <see cref="Plan"/>).</summary>
        private sealed class Look
        {
            public string Install;      // install material to use as it is, or as the template
            public string Texture;      // install texture put on the template (null = the template's own)
            public Color Tint = Color.white;
            public bool SetTint;
            public float Tile = 2f;     // metres per texture repeat along the quad's u (its first side)
            public float TileV;         // ... along v (0 = the same as u)
            public bool Glow;
            public Color GlowColor;
            public bool Skip;
        }

        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private readonly Dictionary<string, Vector2> _tile = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Material> _tinted = new Dictionary<string, Material>();
        private readonly Dictionary<string, MatInfo> _infos = new Dictionary<string, MatInfo>();
        private readonly Dictionary<string, Material> _loaded = new Dictionary<string, Material>();
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> _failed = new HashSet<string>();
        private readonly RrGoldenTrophyHullRoot _owner;
        private static readonly Color[] BookColours =
        {
            new Color(0.16f, 0.45f, 0.26f), new Color(0.62f, 0.18f, 0.15f), new Color(0.78f, 0.58f, 0.2f), new Color(0.2f, 0.3f, 0.6f), new Color(0.45f, 0.3f, 0.18f)
        };

        public int Quads, Batches, Vertices, ItemsBuilt, ItemsSkipped;
        private bool _loggedSurfaces;

        public RrGoldenTrophyHull(Transform room)
        {
            var go = new GameObject("Hull Materials");
            go.transform.SetParent(room, false);
            _owner = go.AddComponent<RrGoldenTrophyHullRoot>();
        }

        /// <summary>
        /// The surface of each level material (install name, texture, tint, metres per tile). The level's own
        /// numbers are in the platemat records; the install substitutes are the closest kind (checked in the install
        /// index: Brick_Red_Mat is AG/Standard-Opaque with Brick_Grout_tex, Wood_Floor_Matte_mat has WoodFloorTiled_tex,
        /// DormRoom_PlasterWalls_Black_mat and Plaster_Black_mat have Plaster_tex, Stones_Modular_Theater_Mat has
        /// Quest_Goblin2_ModularStones_Tex; all use _BaseMap / _BaseColor).
        /// </summary>
        private Look Plan(string mat, MatInfo info)
        {
            string l = (mat ?? string.Empty).ToLowerInvariant();
            var col = info != null ? info.Col : Color.white;
            var look = new Look { Tint = col };
            if (info != null && info.Inst)
            {
                look.Install = mat;
                look.Tile = l.Contains("metal") ? 1f : l.Contains("stone") || l.Contains("rock") ? 3f : 2f;
                return look;
            }

            if (l.Contains("lava"))
            {
                look.Glow = true;
                look.GlowColor = new Color(1f, 0.36f, 0.05f);
                return look;
            }

            if (l.Contains("windows") || l.Contains("glow") || l.Contains("lightshaft"))
            {
                look.Glow = true;
                look.GlowColor = col;
                return look;
            }

            if (l.StartsWith("wood_floor"))
            {
                look.Install = "Wood_Floor_Matte_mat";
                look.Texture = info != null ? info.Tex : null;
                look.SetTint = true;
                look.Tile = 3f;
                return look;
            }

            if (l.StartsWith("stagetop") || l.StartsWith("wood_planks") || l.StartsWith("propbox") || l.Contains("wood_horizontal"))
            {
                look.Install = l.StartsWith("wood_planks") ? "Wood_Horizontal_Orange_Mat" : "Wood_Horizontal_DarkBrown_5x_Mat";
                look.Tint = l.StartsWith("stagetop") ? Brighter(col, 1.5f) : l.StartsWith("propbox") ? new Color(0.72f, 0.62f, 0.5f) : Color.white;
                look.SetTint = !l.StartsWith("wood_planks") || col != Color.white;
                look.Tile = 2f;
                return look;
            }

            if (l.Contains("plasterwalls") || l.StartsWith("wallpaper_dark") || l.StartsWith("plaster"))
            {
                look.Install = "DormRoom_PlasterWalls_Black_mat";
                look.Texture = info != null ? info.Tex : null;
                look.SetTint = true;
                look.Tile = 2f;
                return look;
            }

            if (l.StartsWith("wallpaper_rockwall") || l.Contains("stone"))
            {
                // Rec Room's Stone_Brick_Tex is not in the install: its quest flagstone (Goblin2 modular stones) is.
                look.Install = "Stones_Modular_Theater_Mat";
                look.Tint = new Color(0.62f, 0.6f, 0.58f);
                look.SetTint = true;
                look.Tile = 3f;
                return look;
            }

            if (l.StartsWith("brick") || l.StartsWith("cinderblock"))
            {
                look.Install = "Brick_Red_Mat";
                look.Tint = l.StartsWith("cinderblock") ? new Color(0.78f, 0.76f, 0.72f) : Brighter(col, 1.35f);
                look.SetTint = true;
                look.Tile = 2f;
                return look;
            }

            if (l.StartsWith("banner") || l.Contains("curtain") || l.Contains("rug"))
            {
                look.Install = "Curtain_Red_Mat";
                look.Tile = 2f;
                return look;
            }

            if (l.StartsWith("concrete"))
            {
                look.Install = "Concrete_LightGrey_2x_Mat";
                look.Tint = col;
                look.SetTint = true;
                look.Tile = 3f;
                return look;
            }

            // Anything else: a maker-pen colour (white or unknown -> a quiet grey).
            look.Tint = col.r > 0.97f && col.g > 0.97f && col.b > 0.97f ? new Color(0.55f, 0.55f, 0.56f) : col;
            return look;
        }

        private static Color Brighter(Color c, float k)
        {
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
        }

        // ------------------------------------------------------------------ materials

        public void AddInfo(string mat, MatInfo info)
        {
            if (!string.IsNullOrEmpty(mat) && !_infos.ContainsKey(mat))
            {
                _infos.Add(mat, info);
            }
        }

        /// <summary>Loads the install materials and textures the given level materials need (once per room).</summary>
        public IEnumerator Prepare(IEnumerable<string> mats)
        {
            var assets = RrAssets.Instance;
            var need = new List<string>(mats);
            need.Add("Curtain_Red_Mat");
            need.Add("Barrel_Table_Crate_Mat");
            need.Add("Rocks_Tiling_Background_Mat");
            need.Add("Wood_Horizontal_RedOrange_Mat");
            need.Add("Wood_Horizontal_DarkBrown_5x_Mat");
            foreach (var mat in need)
            {
                if (_surfaces.ContainsKey(mat))
                {
                    continue;
                }

                MatInfo info;
                _infos.TryGetValue(mat, out info);
                var look = Plan(mat, info);
                LevelTiling(look, info);
                Material m = null;
                if (look.Glow)
                {
                    m = RrLook.Emissive(look.GlowColor, 1.1f);
                }
                else if (!string.IsNullOrEmpty(look.Install) && assets != null)
                {
                    Material template = null;
                    if (!_loaded.TryGetValue(look.Install, out template) && !_failed.Contains(look.Install))
                    {
                        yield return assets.LoadRoutine<Material>(look.Install, 21, x => template = x != null ? RrMaterials.Fix(x) : null);
                        if (template != null)
                        {
                            _loaded[look.Install] = template;
                        }
                        else
                        {
                            _failed.Add(look.Install);
                        }
                    }

                    Texture2D tex = null;
                    if (!string.IsNullOrEmpty(look.Texture) && look.Texture != "-" && !_textures.TryGetValue(look.Texture, out tex) && !_failed.Contains("tex:" + look.Texture))
                    {
                        yield return assets.LoadRoutine<Texture2D>(look.Texture, 28, x => tex = x);
                        if (tex != null)
                        {
                            _textures[look.Texture] = tex;
                        }
                        else
                        {
                            _failed.Add("tex:" + look.Texture);
                        }
                    }

                    if (template != null)
                    {
                        if (!look.SetTint && tex == null && look.Install == mat)
                        {
                            m = template;
                        }
                        else
                        {
                            m = new Material(template) { name = template.name + " (GT " + mat + ")" };
                            _owner.Owned.Add(m);
                            if (tex != null)
                            {
                                if (m.HasProperty("_BaseMap"))
                                {
                                    m.SetTexture("_BaseMap", tex);
                                }

                                if (m.HasProperty("_MainTex"))
                                {
                                    m.SetTexture("_MainTex", tex);
                                }
                            }

                            if (look.SetTint)
                            {
                                if (m.HasProperty("_BaseColor"))
                                {
                                    m.SetColor("_BaseColor", look.Tint);
                                }

                                if (m.HasProperty("_Color"))
                                {
                                    m.SetColor("_Color", look.Tint);
                                }
                            }
                        }

                        // Our UVs are metres / tile: the template's own tiling (made for its mesh's UVs) is reset.
                        if (m != template)
                        {
                            if (m.HasProperty("_BaseMap"))
                            {
                                m.SetTextureScale("_BaseMap", Vector2.one);
                            }

                            if (m.HasProperty("_MainTex"))
                            {
                                m.SetTextureScale("_MainTex", Vector2.one);
                            }
                        }
                    }
                }

                if (m == null)
                {
                    m = RrLook.Solid(look.Tint);
                }

                _surfaces[mat] = m;
                _tile[mat] = new Vector2(look.Tile, look.TileV > 0f ? look.TileV : look.Tile);
            }

            if (!_loggedSurfaces)
            {
                _loggedSurfaces = true;
                var sb = new System.Text.StringBuilder("GoldenTrophy hull surfaces:");
                foreach (var kv in _surfaces)
                {
                    sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value != null ? kv.Value.name : "-");
                }

                RrLog.Info("Quest", sb.ToString());
            }
        }

        /// <summary>
        /// Rec Room's hull meshes are ProBuilder's (CachedProBuilderMesh, the static batches), whose UVs are in metres: one
        /// texture repeat is 1 / the material's scale (Wood_Floor_Wide_mat 0.2 x 0.75: 5 x 1.33 m; the plaster 15: 6.7 cm;
        /// Brick_Painted 0.5: 2 m). Our quads' UVs are metres too, so the level's own tiling applies as it is.
        /// </summary>
        private static void LevelTiling(Look look, MatInfo info)
        {
            if (look.Glow || info == null || info.Scale.x < 1e-4f || info.Scale.y < 1e-4f)
            {
                return;
            }

            look.Tile = Mathf.Clamp(1f / info.Scale.x, 0.05f, 12f);
            look.TileV = Mathf.Clamp(1f / info.Scale.y, 0.05f, 12f);
        }

        private Material Surface(string mat, out float tile)
        {
            Vector2 t2;
            var m = Surface2(mat, out t2);
            tile = t2.x;
            return m;
        }

        private Material Surface2(string mat, out Vector2 tile)
        {
            Material m;
            if (!_surfaces.TryGetValue(mat, out m) || m == null)
            {
                MatInfo info;
                _infos.TryGetValue(mat, out info);
                var look = Plan(mat, info);
                LevelTiling(look, info);
                m = look.Glow ? RrLook.Emissive(look.GlowColor, 1.1f) : RrLook.Solid(look.Tint);
                _surfaces[mat] = m;
                _tile[mat] = new Vector2(look.Tile, look.TileV > 0f ? look.TileV : look.Tile);
            }

            if (!_tile.TryGetValue(mat, out tile))
            {
                tile = new Vector2(2f, 2f);
            }

            return m;
        }

        /// <summary>
        /// A surface multiplied by a face's vertex colour (Rec Room's *VertColor* materials: Cinderblock_VertColor_Mat
        /// walls take their colour per face from the mesh; Hallway1's lockers are teal 0.15/0.63/0.71), cached per
        /// colour at 1/16 steps.
        /// </summary>
        private Material Tinted(string mat, Material m, Color tint, out string key)
        {
            var q = new Color(Mathf.Round(tint.r * 16f) / 16f, Mathf.Round(tint.g * 16f) / 16f, Mathf.Round(tint.b * 16f) / 16f, 1f);
            key = mat + "|" + q.r + "," + q.g + "," + q.b;
            Material t;
            if (_tinted.TryGetValue(key, out t) && t != null)
            {
                return t;
            }

            t = new Material(m) { name = m.name + " (tint " + q.r + "," + q.g + "," + q.b + ")" };
            _owner.Owned.Add(t);
            foreach (var prop in new[] { "_BaseColor", "_Color" })
            {
                if (t.HasProperty(prop))
                {
                    t.SetColor(prop, t.GetColor(prop) * q);
                }
            }

            _tinted[key] = t;
            return t;
        }

        // ------------------------------------------------------------------ hull

        /// <summary>
        /// Builds the plates under frame (the scene frame: plates are in the level's own coordinates) as merged
        /// meshes per material and 12 m cell. Spread over frames (one batch per yield).
        /// </summary>
        public IEnumerator Build(Transform frame, string scene, List<Plate> plates)
        {
            var root = new GameObject(scene + " hull");
            root.transform.SetParent(frame, false);
            var owner = root.AddComponent<RrGoldenTrophyHullRoot>();
            var batches = new Dictionary<string, Acc>();
            int n = 0;
            foreach (var p in plates)
            {
                Vector2 tile;
                var m = Surface2(p.Mat, out tile);
                string key = p.Mat;
                if (p.Tint.a > 0f && (p.Tint.r < 0.97f || p.Tint.g < 0.97f || p.Tint.b < 0.97f))
                {
                    m = Tinted(p.Mat, m, p.Tint, out key);
                }

                // A big floor or wall (Hallway4's wood floor has 44 m runs) is cut into pieces of at
                // most MaxPiece, so each cell gets its own renderer and its own nearest lights.
                int nx = Mathf.Max(1, Mathf.CeilToInt(p.Hx * 2f / MaxPiece)), ny = Mathf.Max(1, Mathf.CeilToInt(p.Hy * 2f / MaxPiece));
                Vector3 ax = p.Q * Vector3.right, ay = p.Q * Vector3.up;
                float sx = p.Hx / nx, sy = p.Hy / ny;
                for (int iy = 0; iy < ny; iy++)
                {
                    for (int ix = 0; ix < nx; ix++)
                    {
                        var c = p.C + ax * (-p.Hx + sx * (2 * ix + 1)) + ay * (-p.Hy + sy * (2 * iy + 1));
                        Get(batches, key + "|" + CellKey(c), m, tile).Quad(c, p.Q, sx, sy);
                    }
                }

                n++;
            }

            int made = 0;
            foreach (var acc in batches.Values)
            {
                for (var a = acc; a != null; a = a.Next)
                {
                    if (a.V.Count == 0)
                    {
                        continue;
                    }

                    var mesh = a.Make(scene + " hull " + a.Material.name);
                    owner.Owned.Add(mesh);
                    var go = new GameObject("Hull " + a.Material.name);
                    go.layer = RrLayers.Environment;
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = a.Material;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    Batches++;
                    Vertices += a.V.Count;
                    made++;
                    if (made % 12 == 0)
                    {
                        yield return null;
                    }
                }
            }

            Quads += n;
        }

        private static string CellKey(Vector3 p)
        {
            return Mathf.FloorToInt(p.x / Cell) + "," + Mathf.FloorToInt(p.y / Cell) + "," + Mathf.FloorToInt(p.z / Cell);
        }

        private static Acc Get(Dictionary<string, Acc> batches, string key, Material m, float tile)
        {
            return Get(batches, key, m, new Vector2(tile, tile));
        }

        private static Acc Get(Dictionary<string, Acc> batches, string key, Material m, Vector2 tile)
        {
            Acc head;
            if (!batches.TryGetValue(key, out head))
            {
                head = new Acc { Material = m, Tile = tile.x, TileV = tile.y };
                batches.Add(key, head);
            }

            var a = head;
            while (a.Next != null)
            {
                a = a.Next;
            }

            if (a.V.Count > MaxVerts - 24)
            {
                a.Next = new Acc { Material = m, Tile = tile.x, TileV = tile.y };
                a = a.Next;
            }

            return a;
        }

        // ------------------------------------------------------------------ dressing

        /// <summary>
        /// Rebuilds the level's stand-ins by kind under frame (scene coordinates). flames: add a glowing flame to
        /// each wall torch (the scenes without Rec Room's torch effects, "F" records, which the kit places itself).
        /// Returns the dressing's torch count.
        /// </summary>
        public int Dress(Transform frame, string scene, List<Item> items, bool flames)
        {
            var root = new GameObject(scene + " dressing");
            root.transform.SetParent(frame, false);
            var owner = root.AddComponent<RrGoldenTrophyHullRoot>();
            var batches = new Dictionary<string, Acc>();
            float t;
            var crate = Surface("Barrel_Table_Crate_Mat", out t);
            var cloth = Surface("Curtain_Red_Mat", out t);
            var rock = Surface("Rocks_Tiling_Background_Mat", out t);
            var ladder = Surface("Wood_Horizontal_RedOrange_Mat", out t);
            var dark = Surface("Wood_Horizontal_DarkBrown_5x_Mat", out t);
            var truss = RrLook.Solid(new Color(0.32f, 0.33f, 0.35f));
            // Iteration 9: Rec Room's lantern glass and torch cups glow orange (LanternPost_Mat: emission 0.915/0.36/0 x 0.76 on its
            // texture's glow areas, the candle cards AG/Unlit-Additive 1.30/0.09/0 HDR): warm and above the bloom threshold, so a
            // lantern reads as a light source across the hall (was a pale cream box, 1/0.72/0.35 x 1.4).
            var glow = RrLook.Emissive(new Color(1f, 0.5f, 0.14f), 1.7f);
            var flame = RrLook.Emissive(new Color(1f, 0.42f, 0.08f), 2f);
            var stone = Surface("Stones_Modular_Theater_Mat", out t);
            int torches = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var x = items[i];
                string k = x.Kind ?? string.Empty;
                Vector3 e = x.E;
                Vector3 ax = x.Q * Vector3.right, ay = x.Q * Vector3.up, az = x.Q * Vector3.forward;
                if (k.StartsWith("Book"))
                {
                    Box(batches, RrLook.Solid(BookColours[i % BookColours.Length]), 1f, x.C, x.Q, e * 0.96f);
                }
                else if (k.StartsWith("Scaffolding"))
                {
                    // The theatre's metal truss: four corner rails along its long axis and rungs every metre.
                    Vector3 hl, h1, h2;
                    Axes(x.Q, e, out hl, out h1, out h2);
                    float rail = 0.035f;
                    for (int s1 = -1; s1 <= 1; s1 += 2)
                    {
                        for (int s2 = -1; s2 <= 1; s2 += 2)
                        {
                            var c = x.C + h1 * (s1 * 0.85f) + h2 * (s2 * 0.85f);
                            Beam(batches, truss, c - hl, c + hl, rail);
                        }
                    }

                    int rungs = Mathf.Max(1, Mathf.RoundToInt(hl.magnitude * 2f));
                    for (int r = 0; r <= rungs; r++)
                    {
                        var along = x.C + hl * (-1f + 2f * r / rungs);
                        Beam(batches, truss, along - h1 * 0.85f - h2 * 0.85f, along + h1 * 0.85f - h2 * 0.85f, 0.022f);
                        Beam(batches, truss, along - h1 * 0.85f + h2 * 0.85f, along + h1 * 0.85f + h2 * 0.85f, 0.022f);
                        Beam(batches, truss, along - h1 * 0.85f - h2 * 0.85f, along - h1 * 0.85f + h2 * 0.85f, 0.022f);
                        Beam(batches, truss, along + h1 * 0.85f - h2 * 0.85f, along + h1 * 0.85f + h2 * 0.85f, 0.022f);
                    }
                }
                else if (k.StartsWith("WallTorch"))
                {
                    // Rec Room's [WallTorch]: its -Z is the wall (README "F records"); the stand-in centre is ~0.2 m out.
                    var fwd = Flat(az);
                    var wall = x.C - fwd * Mathf.Max(0.1f, e.z);
                    var up = Vector3.up;
                    Beam(batches, dark, wall + up * -0.3f, wall + fwd * 0.26f + up * 0.12f, 0.04f);
                    Box(batches, dark, 1f, wall + fwd * 0.27f + up * 0.16f, Quaternion.LookRotation(fwd, up), new Vector3(0.07f, 0.06f, 0.07f));
                    if (flames)
                    {
                        Box(batches, flame, 1f, wall + fwd * 0.27f + up * 0.3f, Quaternion.LookRotation(fwd, up) * Quaternion.Euler(0f, 45f, 0f), new Vector3(0.06f, 0.1f, 0.06f));
                    }

                    torches++;
                }
                else if (k.StartsWith("Banner") || k.StartsWith("Rug") || k.StartsWith("Treasure_Banner"))
                {
                    Box(batches, cloth, 2f, x.C, x.Q, new Vector3(Mathf.Max(0.012f, e.x), Mathf.Max(0.012f, e.y), Mathf.Max(0.012f, e.z)));
                }
                else if (k.StartsWith("FeastTable"))
                {
                    // A trestle table: top slab and two trestles across the short side.
                    Vector3 up, wide, deep;
                    Frame(x.Q, e, out up, out wide, out deep);
                    Slab(batches, crate, x.C + up * 0.95f, wide, deep, up.normalized * 0.04f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Slab(batches, crate, x.C + wide * (0.75f * s), wide.normalized * 0.05f, deep * 0.8f, up * 0.9f);
                    }
                }
                else if (k.StartsWith("OpenCrate"))
                {
                    Vector3 up, wide, deep;
                    Frame(x.Q, e, out up, out wide, out deep);
                    Slab(batches, crate, x.C - up * 0.95f, wide, deep, up.normalized * 0.03f);
                    Slab(batches, crate, x.C + wide * 0.95f, wide.normalized * 0.03f, deep, up);
                    Slab(batches, crate, x.C - wide * 0.95f, wide.normalized * 0.03f, deep, up);
                    Slab(batches, crate, x.C + deep * 0.95f, wide, deep.normalized * 0.03f, up);
                    Slab(batches, crate, x.C - deep * 0.95f, wide, deep.normalized * 0.03f, up);
                }
                else if (k.StartsWith("Crenellations"))
                {
                    // Three merlons on a low base along the long axis.
                    Vector3 up, wide, deep;
                    Frame(x.Q, e, out up, out wide, out deep);
                    Slab(batches, stone, x.C - up * 0.6f, wide, deep, up * 0.4f);
                    for (int m = 0; m < 3; m++)
                    {
                        Slab(batches, stone, x.C + wide * (-0.66f + 0.66f * m) + up * 0.4f, wide * 0.2f, deep, up * 0.6f);
                    }
                }
                else if (k.StartsWith("LanternPost"))
                {
                    Vector3 up, wide, deep;
                    Frame(x.Q, e, out up, out wide, out deep);
                    var basePt = x.C - up;
                    float h = up.magnitude * 2f;
                    Beam(batches, dark, basePt, basePt + Vector3.up * (h - 0.35f), 0.06f);
                    Box(batches, glow, 1f, basePt + Vector3.up * (h - 0.2f), Quaternion.identity, new Vector3(0.14f, 0.18f, 0.14f));
                }
                else if (k.StartsWith("HangingLantern") || k.StartsWith("HangingSign"))
                {
                    Box(batches, glow, 1f, x.C, x.Q, new Vector3(0.12f, 0.2f, 0.12f));
                    Box(batches, dark, 1f, x.C + Vector3.up * 0.26f, x.Q, new Vector3(0.16f, 0.05f, 0.16f));
                }
                else if (k == "rockvox")
                {
                    Box(batches, rock, 3f, x.C, x.Q, e);
                }
                else if (k.StartsWith("Ladder"))
                {
                    Vector3 up, wide, deep;
                    Frame(x.Q, e, out up, out wide, out deep);
                    Beam(batches, ladder, x.C + wide * 0.9f - up, x.C + wide * 0.9f + up, 0.03f);
                    Beam(batches, ladder, x.C - wide * 0.9f - up, x.C - wide * 0.9f + up, 0.03f);
                    int rungs = Mathf.Max(2, Mathf.RoundToInt(up.magnitude * 2f / 0.35f));
                    for (int r = 1; r < rungs; r++)
                    {
                        var c = x.C - up + up * (2f * r / rungs);
                        Beam(batches, ladder, c - wide * 0.9f, c + wide * 0.9f, 0.025f);
                    }
                }
                else if (k.StartsWith("MirrorTable"))
                {
                    // The dressing room's make-up table (4.0 wide, 3.6 tall with its mirror): a 0.85 m dark wood counter
                    // and a pale glass pane above it.
                    Vector3 up, wide, deep;
                    Frame(x.Q, e, out up, out wide, out deep);
                    var n = up.normalized;
                    var floor = x.C - up;
                    Slab(batches, dark, floor + n * 0.425f, wide * 0.98f, deep * 0.98f, n * 0.425f);
                    float top = up.magnitude * 2f;
                    Slab(batches, RrLook.Glass(new Color(0.75f, 0.82f, 0.88f, 0.55f)), floor + n * (1f + (top - 1.2f) * 0.5f), wide * 0.7f, deep.normalized * 0.01f, n * Mathf.Max(0.3f, (top - 1.2f) * 0.5f));
                }
                else if (Skipped(k))
                {
                    ItemsSkipped++;
                    continue;
                }
                else
                {
                    Box(batches, RrLook.Solid(x.Col.a > 0.1f ? new Color(x.Col.r, x.Col.g, x.Col.b) : Color.grey), 1f, x.C, x.Q, e);
                }

                ItemsBuilt++;
            }

            foreach (var acc in batches.Values)
            {
                for (var a = acc; a != null; a = a.Next)
                {
                    if (a.V.Count == 0)
                    {
                        continue;
                    }

                    var mesh = a.Make(scene + " dressing " + a.Material.name);
                    owner.Owned.Add(mesh);
                    var go = new GameObject("Dressing " + a.Material.name);
                    go.layer = RrLayers.Environment;
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = a.Material;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    Batches++;
                    Vertices += a.V.Count;
                }
            }

            return torches;
        }

        /// <summary>Stand-ins not drawn: lock icons, fog blobs, light-pool cards, stage-light stands, mannequins, cables, the mods board (ours stands there).</summary>
        private static bool Skipped(string k)
        {
            return k == "Quad" || k == "Plane" || k.StartsWith("fog") || k.StartsWith("Fountain") || k.StartsWith("StageLight") || k.StartsWith("Tripod")
                || k.StartsWith("Goblin_") || k.StartsWith("Cable") || k.StartsWith("PaintballModsBoard") || k.StartsWith("Clock_Base") || k.StartsWith("Quest_")
                || k.StartsWith("GoldenBucket") || k.StartsWith("DrinkingFountain");
        }

        // ------------------------------------------------------------------ geometry helpers

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        /// <summary>The box's half axes (world), longest first.</summary>
        private static void Axes(Quaternion q, Vector3 e, out Vector3 longest, out Vector3 second, out Vector3 third)
        {
            var a = new[] { q * new Vector3(e.x, 0f, 0f), q * new Vector3(0f, e.y, 0f), q * new Vector3(0f, 0f, e.z) };
            System.Array.Sort(a, (u, v) => v.sqrMagnitude.CompareTo(u.sqrMagnitude));
            longest = a[0];
            second = a[1];
            third = a[2];
        }

        /// <summary>
        /// A stand-in's half axes as up (the most vertical one, turned to +y), wide (the longer of the other two) and
        /// deep (the shorter one), whatever order the mesh stores them in.
        /// </summary>
        private static void Frame(Quaternion q, Vector3 e, out Vector3 up, out Vector3 wide, out Vector3 deep)
        {
            var a = new[] { q * new Vector3(e.x, 0f, 0f), q * new Vector3(0f, e.y, 0f), q * new Vector3(0f, 0f, e.z) };
            int u = 0;
            for (int i = 1; i < 3; i++)
            {
                if (Mathf.Abs(a[i].normalized.y) > Mathf.Abs(a[u].normalized.y))
                {
                    u = i;
                }
            }

            up = a[u].y < 0f ? -a[u] : a[u];
            var o1 = a[(u + 1) % 3];
            var o2 = a[(u + 2) % 3];
            if (o2.sqrMagnitude > o1.sqrMagnitude)
            {
                var sw = o1;
                o1 = o2;
                o2 = sw;
            }

            wide = o1;
            deep = o2;
        }

        /// <summary>The half axis that points most up (sign flipped to +y).</summary>
        private static Vector3 UpMost(Quaternion q, Vector3 e)
        {
            var a = new[] { q * new Vector3(e.x, 0f, 0f), q * new Vector3(0f, e.y, 0f), q * new Vector3(0f, 0f, e.z) };
            int best = 0;
            for (int i = 1; i < 3; i++)
            {
                if (Mathf.Abs(a[i].normalized.y) > Mathf.Abs(a[best].normalized.y))
                {
                    best = i;
                }
            }

            return a[best].y < 0f ? -a[best] : a[best];
        }

        private static float HeightOf(Quaternion q, Vector3 e)
        {
            return UpMost(q, e).magnitude;
        }

        private static void Box(Dictionary<string, Acc> batches, Material m, float tile, Vector3 c, Quaternion q, Vector3 e)
        {
            Get(batches, "d|" + m.GetInstanceID() + "|" + CellKey(c), m, tile).Box(c, q * Vector3.right * e.x, q * Vector3.up * e.y, q * Vector3.forward * e.z);
        }

        /// <summary>A box from three half axes (any orientation).</summary>
        private static void Slab(Dictionary<string, Acc> batches, Material m, Vector3 c, Vector3 a, Vector3 b, Vector3 d)
        {
            Get(batches, "d|" + m.GetInstanceID() + "|" + CellKey(c), m, 1f).Box(c, a, b, d);
        }

        /// <summary>A square beam from a to b.</summary>
        private static void Beam(Dictionary<string, Acc> batches, Material m, Vector3 a, Vector3 b, float half)
        {
            var dir = b - a;
            if (dir.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var q = Quaternion.LookRotation(dir.normalized, Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up);
            Get(batches, "d|" + m.GetInstanceID() + "|" + CellKey((a + b) * 0.5f), m, 1f).Box((a + b) * 0.5f, q * Vector3.right * half, q * Vector3.up * half, dir * 0.5f);
        }

        /// <summary>A mesh under construction: quads with UVs in scene metres / tile and tangents along u.</summary>
        private sealed class Acc
        {
            public Material Material;
            public float Tile = 2f;
            public float TileV;
            public Acc Next;
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector4> Tg = new List<Vector4>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            /// <summary>A one-sided quad at c, rotation q (local x/y = its sides, local z = its facing), half sizes hx, hy.</summary>
            public void Quad(Vector3 c, Quaternion q, float hx, float hy)
            {
                Face(c, q * Vector3.right * hx, q * Vector3.up * hy, q * Vector3.forward);
            }

            /// <summary>A face centred at c spanned by half axes a (u) and b (v), facing n (a x b points along n).</summary>
            public void Face(Vector3 c, Vector3 a, Vector3 b, Vector3 n)
            {
                int i = V.Count;
                V.Add(c - a - b);
                V.Add(c + a - b);
                V.Add(c + a + b);
                V.Add(c - a + b);
                var u = a.sqrMagnitude > 1e-12f ? a.normalized : Vector3.right;
                var v = b.sqrMagnitude > 1e-12f ? b.normalized : Vector3.up;
                float inv = 1f / Mathf.Max(0.05f, Tile), invV = 1f / Mathf.Max(0.05f, TileV > 0f ? TileV : Tile);
                for (int k = i; k < i + 4; k++)
                {
                    N.Add(n);
                    Tg.Add(new Vector4(u.x, u.y, u.z, 1f));
                    U.Add(new Vector2(Vector3.Dot(V[k], u) * inv, Vector3.Dot(V[k], v) * invV));
                }

                // Unity's front faces wind clockwise seen from the front: (0,1,2),(0,2,3) with a x b along n.
                // (Vector3.Cross(p1 - p0, p2 - p0) is the front normal of a clockwise, i.e. front, triangle.)
                if (Vector3.Dot(Vector3.Cross(a, b), n) >= 0f)
                {
                    T.Add(i); T.Add(i + 1); T.Add(i + 2);
                    T.Add(i); T.Add(i + 2); T.Add(i + 3);
                }
                else
                {
                    T.Add(i); T.Add(i + 2); T.Add(i + 1);
                    T.Add(i); T.Add(i + 3); T.Add(i + 2);
                }
            }

            /// <summary>A closed box from its centre and three half axes.</summary>
            public void Box(Vector3 c, Vector3 a, Vector3 b, Vector3 d)
            {
                Face(c + d, a, b, d.normalized);
                Face(c - d, b, a, -d.normalized);
                Face(c + a, b, d, a.normalized);
                Face(c - a, d, b, -a.normalized);
                Face(c + b, d, a, b.normalized);
                Face(c - b, a, d, -b.normalized);
            }

            public Mesh Make(string name)
            {
                var mesh = new Mesh { name = name };
                if (V.Count > 65000)
                {
                    mesh.indexFormat = IndexFormat.UInt32;
                }

                mesh.SetVertices(V);
                mesh.SetNormals(N);
                mesh.SetTangents(Tg);
                mesh.SetUVs(0, U);
                mesh.SetTriangles(T, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        // ------------------------------------------------------------------ records

        /// <summary>A key of a marker record (null when it has none).</summary>
        public static string Key(RrSceneData.MarkerRecord m, string key)
        {
            if (m.Keys != null)
            {
                for (int i = 0; i < m.Keys.Length; i++)
                {
                    if (m.Keys[i] == key)
                    {
                        return m.Values[i];
                    }
                }
            }

            return null;
        }

        public static Vector3 Vec(string s, Vector3 fallback)
        {
            if (string.IsNullOrEmpty(s))
            {
                return fallback;
            }

            var p = s.Split(',');
            float x, y, z;
            if (p.Length >= 3 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                return new Vector3(x, y, z);
            }

            return fallback;
        }

        public static Vector2 Vec2(string s, Vector2 fallback)
        {
            if (string.IsNullOrEmpty(s))
            {
                return fallback;
            }

            var p = s.Split(',');
            float x, y;
            if (p.Length >= 2 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            {
                return new Vector2(x, y);
            }

            return fallback;
        }

        public static Color Col(string s, Color fallback)
        {
            if (string.IsNullOrEmpty(s))
            {
                return fallback;
            }

            var p = s.Split(',');
            float r, g, b, a = 1f;
            if (p.Length >= 3 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out r) && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out g)
                && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out b))
            {
                if (p.Length >= 4)
                {
                    float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out a);
                }

                return new Color(r, g, b, a);
            }

            return fallback;
        }
    }
}
