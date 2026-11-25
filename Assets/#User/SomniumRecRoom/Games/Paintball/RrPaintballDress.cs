using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// Builds the Paintball levels' stand-ins: what the Android levels draw with meshes the PC install lacks (Rec
    /// Room's aspen, bush and pine trees, the forts, sheds and dam walls merged in the editor, the shipping
    /// containers, fences, tyre stacks, cliffs) and the levels' Unity cubes. DevNotes/tools/pbscene/pbprep.awk wrote
    /// each as a "dress" marker with its kind, exact bounds (centre, half extents, rotation), material colour and
    /// material name; this class rebuilds them in their real sizes and places:
    /// - structures ("struct": the editor-merged meshes, which the converter could only give as coarse voxel boxes)
    ///   are drawn from the level's own box colliders inside them: Rec Room's forts, sheds and the dam are walls,
    ///   floors, ramps and railings of box colliders, so the rebuild shows their real shapes, doorways and floors.
    ///   A collider belongs to the smallest structure holding it (iteration 7 took the first: the lobby's roof
    ///   collider fell into the 7 x 5 x 12 m light-shaft volume and was drawn see-through, so the locker room had no
    ///   roof), light shafts and fog volumes are never structures, and a voxel box that the drawn colliders cover
    ///   less than 30 % is drawn as well in the lobby (a clerestory with no collider stays; on the maps such boxes are
    ///   the envelope of a merged mesh over open streets and stay out);
    /// - the lobby building's surfaces by face (iteration 8, the 2025 footage: "red-orange walls" outside,
    ///   "grey-brown walls and dark grey floor" inside): a face with a roof over the space in front of it and a wall
    ///   across from it is inside (two physics rays against the level's colliders) and takes Paint_Warm_Grey_Mat,
    ///   else Wood_Horizontal_RedOrange_Mat; ceilings Paint_White_Mat, covered floors Paint_DarkGrey_Mat, the corrugated roof
    ///   Metal_Galvanized_Mat (all install materials);
    /// - trees become low-poly trees of our own (the install has no 3D tree: its only trees are the cardboard
    ///   (Sandbox_FlatTree*) standees and a dead pine), in the level's own crown colours (the "tint" markers:
    ///   Tree_Aspen_Gold / _Red / _Dark_Green, Tree_Bush_Gold / _Red...): crowns of 5-8 faceted clumps in two shades
    ///   (Rec Room's mobile aspens and bush trees are clumpy, not one egg), white aspen trunks, 4-tier pines;
    /// - tyre stacks are Rec Room's own tyre (Tire_LOD0 with Paintball_TractorTire_Mat, 2.25 x 0.53 m at scale 1)
    ///   stacked to the stack's measured size (1.53 x 1.08 m: 3 tyres at scale 0.68), not one black drum;
    /// - shipping containers get their corrugation ribs and rails; cliffs and rocks become faceted boulders;
    /// - boxes take their surface by material (<see cref="Surface"/>): the install material itself when the
    ///   level's is in the install, else the nearest install surface (wood, concrete) tiled in metres, else a
    ///   maker-pen colour from the material's name (container orange, crane red, tarp stripes, lit light cubes);
    /// - everything is merged into one mesh per surface and 48 m cell and casts shadows (iteration 7 had shadows
    ///   off: "no shadows or anything"). Nothing here collides: the levels' own colliders (C records) are built by
    ///   the kit exactly as they are.
    /// </summary>
    public static class RrPaintballDress
    {
        public const float Cell = 48f;
        public const int MaxVerts = 60000;

        // Install surfaces (Material names in the PC bundles; each loads once per room).
        public const string RockMat = "Rocks_Tiling_Background_Mat";
        public const string WoodOrangeMat = "Wood_Horizontal_Orange_Mat";
        public const string WoodRedMat = "Wood_Horizontal_RedOrange_Mat";
        public const string WoodDarkMat = "Wood_Horizontal_DarkBrown_5x_Mat";
        public const string WoodDarkOrangeMat = "Wood_Horizontal_DarkOrange_5x_Mat";
        public const string WoodRedBrownMat = "Wood_Horizontal_RedBrown_mat";
        public const string WoodYellowMat = "Wood_Horizontal_YellowBrown_Mat";
        public const string ConcreteMat = "Concrete_LightGrey_2x_Mat";
        public const string PaintWhiteMat = "Paint_White_Mat";
        public const string WarmGreyMat = "Paint_Warm_Grey_Mat";
        public const string DarkGreyMat = "Paint_DarkGrey_Mat";
        public const string GalvanizedMat = "Metal_Galvanized_Mat";
        public const string TyreMat = "Paintball_TractorTire_Mat";
        public const string TyreMesh = "Tire_LOD0";

        /// <summary>Rec Room's own low-poly rocks (bundle 43ea1176, (Rock_*)Variant_Sandbox prefabs), 124-184 vertices each.</summary>
        public static readonly string[] RockMeshes = { "RecRoyale_Rock_Cliff", "RecRoyale_Rock_Tall", "RecRoyale_Rock_Plateau", "RecRoyale_Rock_Cone", "RecRoyale_Rock_Stump" };

        public const string RockFrontierMat = "Rocks_Frontier_Mat";

        /// <summary>Tire_LOD0's half extents at scale 1 (install mesh bounds): 1.1272 m radius, 0.2655 m half height.</summary>
        private const float TyreRadius = 1.1272f, TyreHalfHeight = 0.2655f;

        public static readonly string[] InstallSurfaces =
        {
            RockMat, WoodOrangeMat, WoodRedMat, WoodDarkMat, WoodDarkOrangeMat, WoodRedBrownMat, WoodYellowMat, ConcreteMat, PaintWhiteMat, WarmGreyMat, DarkGreyMat,
            GalvanizedMat, TyreMat, RockFrontierMat
        };

        private static readonly Color[] Aspen =
        {
            new Color(0.55f, 0.70f, 0.27f), new Color(0.47f, 0.64f, 0.24f), new Color(0.30f, 0.50f, 0.22f), new Color(0.95f, 0.72f, 0.20f),
            new Color(0.86f, 0.33f, 0.16f)
        };

        private static readonly Color[] Containers =
        {
            new Color(0.93f, 0.46f, 0.13f), new Color(0.93f, 0.46f, 0.13f), new Color(0.93f, 0.46f, 0.13f),
            new Color(0.16f, 0.56f, 0.60f), new Color(0.72f, 0.18f, 0.15f), new Color(0.22f, 0.40f, 0.74f)
        };

        /// <summary>Sticker colours for the lobby's small sign-atlas pieces (the lockers are "covered in stickers").</summary>
        private static readonly Color[] Stickers =
        {
            new Color(0.96f, 0.94f, 0.88f), new Color(1f, 0.83f, 0.23f), new Color(0.20f, 0.70f, 0.70f), new Color(0.95f, 0.51f, 0.67f),
            new Color(0.52f, 0.33f, 0.73f), new Color(1f, 0.58f, 0f), new Color(0.36f, 0.72f, 0.33f)
        };

        private static readonly Color PineGreen = new Color(0.17f, 0.37f, 0.22f);
        private static readonly Color PineDark = new Color(0.12f, 0.29f, 0.18f);
        private static readonly Color PineLight = new Color(0.22f, 0.44f, 0.25f);
        private static readonly Color AspenBark = new Color(0.88f, 0.86f, 0.80f);
        private static readonly Color Bark = new Color(0.42f, 0.28f, 0.17f);
        private static readonly Color Rubber = new Color(0.11f, 0.11f, 0.12f);
        private static readonly Color RockGrey = new Color(0.56f, 0.55f, 0.52f);

        public sealed class Stats
        {
            public int Items;
            public int Trees;
            public int Boxes;
            public int Rocks;
            public int Tyres;
            public int StructsFromColliders;
            public int CollidersDrawn;
            public int VoxKept;
            public int FacesInside;
            public int FacesOutside;
            public int Batches;
            public int Vertices;
            public int Skipped;
            public float Ms;
            public readonly Dictionary<string, int> Kinds = new Dictionary<string, int>();

            public override string ToString()
            {
                var s = new System.Text.StringBuilder();
                foreach (var kv in Kinds)
                {
                    s.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
                }

                return Items + " stand-ins (" + Trees + " trees, " + Rocks + " rocks, " + Tyres + " install tyres, " + Boxes + " boxes; " + StructsFromColliders +
                       " structures drawn from " + CollidersDrawn + " of their colliders (+" + VoxKept + " uncovered voxel boxes), building faces " + FacesInside +
                       " inside / " + FacesOutside + " outside, " + Skipped + " left out) in " + Batches + " meshes, " + Vertices + " vertices, " + Ms.ToString("0") + " ms;" + s;
            }
        }

        /// <summary>A resolved surface: an install material (tiled), or a colour (solid, glowing or see-through).</summary>
        public struct Look
        {
            public string Material;
            public Color Tint;
            public float Tile;
            public bool Glow;
            public bool Skip;

            public string Key
            {
                get { return Material != null ? "m:" + Material : (Glow ? "g:" : "c:") + ColorKey(Tint); }
            }
        }

        /// <summary>What a structure's material says about the building it belongs to (lobby face rules).</summary>
        private enum Bld
        {
            None,
            Wall,
            Concrete,
            Roof
        }

        /// <summary>Install material names the markers can ask for (to load before <see cref="Build"/>).</summary>
        public static void NeededMaterials(List<RrSceneMarker> items, HashSet<string> into)
        {
            foreach (var n in InstallSurfaces)
            {
                into.Add(n);
            }

            foreach (var m in items)
            {
                string mat = m.Get("m");
                if (!string.IsNullOrEmpty(mat) && !mat.StartsWith("?") && Usable(mat))
                {
                    into.Add(mat);
                }
            }
        }

        /// <summary>Install materials that draw well on our boxes (not unlit vertex colour, sky or moon).</summary>
        private static bool Usable(string mat)
        {
            string l = mat.ToLowerInvariant();
            return !l.Contains("unlit") && !l.Contains("moon") && !l.Contains("fog") && !l.Contains("sky") && !l.Contains("glow") && !l.Contains("emit");
        }

        /// <summary>A light shaft, fog or projector volume: not a surface (drawn as a box it hides what is inside).</summary>
        private static bool Volume(string mat)
        {
            string l = (mat ?? "").ToLowerInvariant();
            return l.Contains("lightshaft") || l.Contains("light_shaft") || l.Contains("godray") || l.Contains("volumetric") || l.Contains("fog") ||
                   l.Contains("projector");
        }

        private static Bld BuildingKind(string mat)
        {
            string l = (mat ?? "").ToLowerInvariant();
            if (l.Contains("paintedwall") || l.Contains("wall_vertcolor"))
            {
                return Bld.Wall;
            }

            if (l.Contains("concrete_vertcolor"))
            {
                return Bld.Concrete;
            }

            return l.Contains("corrugated") ? Bld.Roof : Bld.None;
        }

        /// <summary>
        /// Builds the dress markers under parent (their poses are in parent space). colliders are the level's own
        /// (scene frame; parent space = scene - anchor), for the structures. meshes holds install meshes the stand-ins
        /// use (Tire_LOD0). lobby turns on the lobby building's face rules.
        /// </summary>
        public static IEnumerator Build(List<RrSceneMarker> items, List<RrSceneMarker> tints, List<RrSceneData.ColliderRecord> colliders, Vector3 anchor,
                                        Transform parent, Dictionary<string, Material> surfaces, Dictionary<string, Mesh> meshes, bool lobby, Action<Stats> done)
        {
            var st = new Stats();
            var sw = Stopwatch.StartNew();
            var step = Stopwatch.StartNew();
            var root = new GameObject("Stand-ins");
            root.transform.SetParent(parent, false);
            var batches = new Dictionary<string, Acc>();
            var tyres = new List<Matrix4x4>();
            var rocks = new List<KeyValuePair<int, Matrix4x4>>();
            if (lobby)
            {
                // The face rules cast rays against the kit's colliders, which were made this frame or earlier.
                Physics.SyncTransforms();
            }

            // 1. Structures: their regions, and the level's box colliders that lie in them.
            var regions = new List<Bounds>();
            var regionItem = new List<int>();
            var regionVol = new List<float>();
            var grid = new Dictionary<long, List<int>>();
            for (int n = 0; n < items.Count; n++)
            {
                if (items[n].Name != "struct" && items[n].Name != "stair")
                {
                    continue;
                }

                if (Volume(items[n].Get("m")))
                {
                    continue;
                }

                // Only the coarse pieces are replaced: a voxel-shell box, or a bulky bounds box of a merged mesh. A thin
                // plain piece (a ProBuilder floor, wall or panel) is its own exact shape and stays.
                Vector3 e = Vec(items[n].Get("e"), new Vector3(0.5f, 0.5f, 0.5f));
                if (items[n].Get("vox") != "1" && Mathf.Min(e.x, Mathf.Min(e.y, e.z)) < 0.6f)
                {
                    continue;
                }

                var b = new Bounds(items[n].Position, WorldHalf(items[n].Rotation, e) * 2f + new Vector3(1.2f, 1.2f, 1.2f));
                int ri = regions.Count;
                regions.Add(b);
                regionItem.Add(n);
                regionVol.Add(b.size.x * b.size.y * b.size.z);
                for (int gx = Mathf.FloorToInt(b.min.x / 8f); gx <= Mathf.FloorToInt(b.max.x / 8f); gx++)
                {
                    for (int gz = Mathf.FloorToInt(b.min.z / 8f); gz <= Mathf.FloorToInt(b.max.z / 8f); gz++)
                    {
                        long key = ((long)gx << 32) ^ (uint)gz;
                        List<int> list;
                        if (!grid.TryGetValue(key, out list))
                        {
                            grid[key] = list = new List<int>();
                        }

                        list.Add(ri);
                    }
                }
            }

            var drawnRegion = new bool[regions.Count];
            var regionBoxes = new List<Bounds>[regions.Count];
            if (colliders != null && regions.Count > 0)
            {
                for (int i = 0; i < colliders.Count; i++)
                {
                    var c = colliders[i];
                    if (c.Shape != RrSceneData.ColliderShape.Box)
                    {
                        continue;
                    }

                    Vector3 p = c.Center - anchor;
                    Vector3 half = new Vector3(Mathf.Abs(c.Size.x), Mathf.Abs(c.Size.y), Mathf.Abs(c.Size.z)) * 0.5f;
                    if (half.x > 40f || half.z > 40f || (half.x < 0.02f && half.y < 0.02f) || (half.y < 0.02f && half.z < 0.02f))
                    {
                        continue;
                    }

                    List<int> list;
                    long key = ((long)Mathf.FloorToInt(p.x / 8f) << 32) ^ (uint)Mathf.FloorToInt(p.z / 8f);
                    if (!grid.TryGetValue(key, out list))
                    {
                        continue;
                    }

                    // The smallest structure holding the collider's centre.
                    int best = -1;
                    foreach (int ri in list)
                    {
                        if (regions[ri].Contains(p) && (best < 0 || regionVol[ri] < regionVol[best]))
                        {
                            best = ri;
                        }
                    }

                    if (best < 0)
                    {
                        continue;
                    }

                    var m = items[regionItem[best]];
                    string mat = m.Get("m");
                    var look = Surface("struct", Col(m.Get("c"), Color.white), mat, Hash(p), p);
                    if (!look.Skip)
                    {
                        var bk = lobby ? BuildingKind(mat) : Bld.None;
                        if (bk != Bld.None)
                        {
                            BuildingBox(batches, surfaces, parent, p, c.Rotation, half, bk, look, st);
                        }
                        else
                        {
                            Get(batches, look, p, surfaces).Box(p, c.Rotation, half, look.Tile);
                        }

                        st.CollidersDrawn++;
                    }

                    drawnRegion[best] = true;
                    if (regionBoxes[best] == null)
                    {
                        regionBoxes[best] = new List<Bounds>();
                    }

                    regionBoxes[best].Add(new Bounds(p, WorldHalf(c.Rotation, half) * 2f));
                    if (step.Elapsed.TotalMilliseconds > 3f)
                    {
                        yield return null;
                        step.Reset();
                        step.Start();
                        if (root == null)
                        {
                            yield break;
                        }
                    }
                }
            }

            // A drawn structure's own boxes are left out unless its colliders cover less than 30 % of a box.
            var skipItem = new HashSet<int>();
            for (int ri = 0; ri < regions.Count; ri++)
            {
                if (!drawnRegion[ri])
                {
                    continue;
                }

                var m = items[regionItem[ri]];
                var own = new Bounds(m.Position, WorldHalf(m.Rotation, Vec(m.Get("e"), new Vector3(0.5f, 0.5f, 0.5f))) * 2f);
                // Lobby only: on the maps the uncovered boxes of a merged mesh's voxel shell are mostly the shell's
                // envelope over open streets (run 1 drew cream slabs over Merry Midway and the Drive-In).
                if (!lobby || Coverage(own, regionBoxes[ri]) >= 0.3f)
                {
                    skipItem.Add(regionItem[ri]);
                }
                else
                {
                    st.VoxKept++;
                }
            }

            st.StructsFromColliders = skipItem.Count;

            // 2. Everything else.
            for (int n = 0; n < items.Count; n++)
            {
                var m = items[n];
                string kind = m.Name;
                st.Items++;
                int k;
                st.Kinds.TryGetValue(kind, out k);
                st.Kinds[kind] = k + 1;
                if (skipItem.Contains(n))
                {
                    continue;
                }

                Vector3 e = Vec(m.Get("e"), new Vector3(0.5f, 0.5f, 0.5f));
                Color c = Col(m.Get("c"), Color.white);
                string mat = m.Get("m");
                Quaternion q = m.Rotation;
                Vector3 p = m.Position;
                int hash = Hash(p);

                // A huge voxel box (a 25-60 m mesh the converter could only give as 25 m+ cubes) that no collider
                // replaced is a backdrop, a sky piece or a plaza slab: drawn as a cube it hides the map. Left out
                // beyond the play area (70 m from the flags' midpoint) or high above it.
                if (m.Get("vox") == "1" && kind != "rock" && Mathf.Max(e.x, Mathf.Max(e.y, e.z)) > 12f)
                {
                    var wh = WorldHalf(q, e);
                    if (new Vector2(p.x, p.z).magnitude > 70f || p.y - wh.y > 12f)
                    {
                        st.Skipped++;
                        continue;
                    }
                }

                switch (kind)
                {
                    case "tree_aspen":
                    case "tree_bush":
                    case "tree_oak":
                    case "tree_small":
                    case "tree_pine":
                    {
                        // The stand-in's own material names its colour (?Tree_Aspen_Red_Mobile_mat); else the crown part's.
                        string own = mat ?? "";
                        string tint = own.Contains("Gold") || own.Contains("Red") || own.Contains("Green") ? own : TintNear(tints, p) ?? own;
                        Tree(batches, surfaces, p, q, e, hash, kind, tint, st);
                        break;
                    }
                    case "tree_leaves":
                    {
                        // Spillway's pines are a leaves part (PineTree_Mat) over a trunk part: the leaves' bounds as 3 stacked cones.
                        var w = WorldHalf(q, e);
                        var b0 = new Vector3(p.x, p.y - w.y, p.z);
                        float rr = Mathf.Max(0.3f, (w.x + w.z) * 0.5f), hh2 = w.y * 2f;
                        Get(batches, Solid(PineDark), p, surfaces).Cone(b0, rr, hh2 * 0.55f, 7, hash);
                        Get(batches, Solid(PineGreen), p, surfaces).Cone(b0 + Vector3.up * hh2 * 0.3f, rr * 0.75f, hh2 * 0.45f, 7, hash + 1);
                        Get(batches, Solid(PineLight), p, surfaces).Cone(b0 + Vector3.up * hh2 * 0.56f, rr * 0.5f, hh2 * 0.44f, 7, hash + 2);
                        st.Trees++;
                        break;
                    }
                    case "tree_trunk":
                    {
                        var w = WorldHalf(q, e);
                        Get(batches, Solid(Bark), p, surfaces).Cylinder(p, Quaternion.identity, Mathf.Max(0.12f, Mathf.Min(w.x, w.z) * 0.8f), w.y, 7);
                        break;
                    }
                    case "tire":
                        TyreStack(batches, surfaces, meshes, tyres, p, q, e, hash, st);
                        break;
                    case "rock":
                    {
                        // Rec Room's own low-poly rock (RecRoyale_Rock_*) whose shape fits the cliff's rotated bounds, else
                        // a faceted boulder filling them.
                        st.Rocks++;
                        if (Rock(meshes, rocks, p, q, e, hash))
                        {
                            break;
                        }

                        var look = Surface(kind, c, mat, hash, p);
                        Get(batches, look, p, surfaces).Blob(p, q, e * 0.98f, 9, 5, hash);
                        break;
                    }
                    case "container":
                    {
                        var look = Surface(kind, c, mat, hash, p);
                        Container(batches, surfaces, p, q, e, look);
                        st.Boxes++;
                        break;
                    }
                    case "locker":
                        Locker(batches, surfaces, p, q, e);
                        st.Boxes++;
                        break;
                    default:
                    {
                        var look = Surface(kind, c, mat, hash, p);
                        if (look.Skip || (lobby && ((mat ?? "").Contains("Banner_Mat") || (mat ?? "").Contains("Standee_Bonky"))))
                        {
                            // Banner_Mat is the lounge rug (Rug_Quest): RrPaintballSigns draws it red with its border. The
                            // gallery's Bonky standee (its texture is not in the install; a blank 2 x 2.2 m box) becomes
                            // the install's own male practice cutout there (RrPaintballSigns).
                            st.Skipped++;
                            break;
                        }

                        // The lobby building's own panels (a roof slab, a wall piece with no collider) by face, as its colliders.
                        var bk = lobby ? BuildingKind(mat) : Bld.None;
                        if (bk != Bld.None)
                        {
                            BuildingBox(batches, surfaces, parent, p, q, e, bk, look, st);
                            st.Boxes++;
                            break;
                        }

                        // The lobby's sign atlas: pieces under 0.4 m are the stickers on the lockers, in sticker colours.
                        if ((mat ?? "").Contains("Signs_") && Mathf.Max(e.x, Mathf.Max(e.y, e.z)) < 0.4f)
                        {
                            look = Solid(Stickers[(hash >> 4) % Stickers.Length]);
                        }

                        Get(batches, look, p, surfaces).Box(p, q, e, look.Tile);
                        st.Boxes++;
                        break;
                    }
                }

                if (step.Elapsed.TotalMilliseconds > 3f)
                {
                    yield return null;
                    step.Reset();
                    step.Start();
                    if (root == null)
                    {
                        yield break;
                    }
                }
            }

            // 3. The meshes.
            foreach (var kv in batches)
            {
                for (var a = kv.Value; a != null; a = a.Next)
                {
                    if (a.V.Count == 0)
                    {
                        continue;
                    }

                    var go = new GameObject("Stand-ins " + kv.Key);
                    go.transform.SetParent(root.transform, false);
                    var mesh = a.Make("Rr Paintball " + kv.Key);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<RrSceneKitTerrainChunk>().Mesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    bool seeThrough = a.Material == null && a.Tint.a < 0.99f;
                    mr.sharedMaterial = a.Material != null ? a.Material : seeThrough ? RrLook.Glass(a.Tint) : a.Glow ? RrLook.Emissive(a.Tint, 1.6f) : RrLook.Solid(a.Tint);
                    mr.shadowCastingMode = seeThrough || a.Glow ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    mr.receiveShadows = !seeThrough;
                    mr.lightProbeUsage = LightProbeUsage.Off;
                    mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    st.Batches++;
                    st.Vertices += a.V.Count;
                    if (step.Elapsed.TotalMilliseconds > 3f)
                    {
                        yield return null;
                        step.Reset();
                        step.Start();
                        if (root == null)
                        {
                            yield break;
                        }
                    }
                }
            }

            // 4. Rec Room's own tyres and rocks, merged per 48 m cell when the install mesh is readable.
            if (tyres.Count > 0)
            {
                BuildTyres(root.transform, meshes, surfaces, tyres, st);
            }

            if (rocks.Count > 0)
            {
                BuildRocks(root.transform, meshes, surfaces, rocks, st);
            }

            st.Ms = (float)sw.Elapsed.TotalMilliseconds;
            if (done != null)
            {
                done(st);
            }
        }

        /// <summary>The share of a box's volume that the given boxes overlap (axis-aligned, overlaps summed, capped at 1).</summary>
        private static float Coverage(Bounds own, List<Bounds> boxes)
        {
            if (boxes == null)
            {
                return 0f;
            }

            Vector3 s = own.size;
            float vol = Mathf.Max(0.3f, s.x) * Mathf.Max(0.3f, s.y) * Mathf.Max(0.3f, s.z);
            float cov = 0f;
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                float ox = Mathf.Min(own.max.x, b.max.x) - Mathf.Max(own.min.x, b.min.x);
                float oy = Mathf.Min(own.max.y, b.max.y) - Mathf.Max(own.min.y, b.min.y);
                float oz = Mathf.Min(own.max.z, b.max.z) - Mathf.Max(own.min.z, b.min.z);
                if (ox <= 0f || oy <= 0f || oz <= 0f)
                {
                    continue;
                }

                cov += Mathf.Max(0.3f, ox) * Mathf.Max(0.3f, oy) * Mathf.Max(0.3f, oz);
                if (cov >= vol)
                {
                    return 1f;
                }
            }

            return cov / vol;
        }

        /// <summary>
        /// One collider box of the lobby building, face by face: a face is inside when the space 8 cm in front of it
        /// has something overhead within 10 m and something across within 25 m (the level's own colliders).
        /// </summary>
        private static void BuildingBox(Dictionary<string, Acc> batches, Dictionary<string, Material> surfaces, Transform parent, Vector3 p, Quaternion q, Vector3 e, Bld kind,
                                        Look plain, Stats st)
        {
            Vector3 x = q * Vector3.right, y = q * Vector3.up, z = q * Vector3.forward;
            for (int f = 0; f < 6; f++)
            {
                Vector3 n, v;
                float eu, ev, en;
                switch (f)
                {
                    case 0: n = z; v = y; eu = e.x; ev = e.y; en = e.z; break;
                    case 1: n = -z; v = y; eu = e.x; ev = e.y; en = e.z; break;
                    case 2: n = x; v = y; eu = e.z; ev = e.y; en = e.x; break;
                    case 3: n = -x; v = y; eu = e.z; ev = e.y; en = e.x; break;
                    case 4: n = y; v = z; eu = e.x; ev = e.z; en = e.y; break;
                    default: n = -y; v = z; eu = e.x; ev = e.z; en = e.y; break;
                }

                Vector3 fc = p + n * en;
                bool up = n.y > 0.6f, down = n.y < -0.6f;
                bool inside = Inside(parent, fc, n, up || down);
                if (inside)
                {
                    st.FacesInside++;
                }
                else
                {
                    st.FacesOutside++;
                }

                Look look;
                switch (kind)
                {
                    case Bld.Wall:
                        look = down ? Install(PaintWhiteMat, 2f) : inside ? Install(WarmGreyMat, 2f) : Install(WoodRedMat, 2f);
                        break;
                    case Bld.Concrete:
                        look = up && inside ? Install(DarkGreyMat, 3f) : plain;
                        break;
                    default:
                        look = down ? Install(PaintWhiteMat, 2f) : Install(GalvanizedMat, 2f);
                        break;
                }

                Get(batches, look, p, surfaces).Face(fc, n, v, eu, ev, look.Tile);
            }
        }

        /// <summary>Is the space in front of a face (parent space) under a roof and across from a wall?</summary>
        private static bool Inside(Transform parent, Vector3 fc, Vector3 n, bool horizontal)
        {
            Vector3 o = parent.TransformPoint(fc + n * 0.08f);
            if (horizontal)
            {
                // A floor's top or a ceiling: inside when covered (a floor) or when it is itself the cover.
                return n.y > 0f ? Physics.Raycast(o, Vector3.up, 10f, ~0, QueryTriggerInteraction.Ignore) : true;
            }

            return Physics.Raycast(o, Vector3.up, 10f, ~0, QueryTriggerInteraction.Ignore) &&
                   Physics.Raycast(o, parent.TransformDirection(n), 25f, ~0, QueryTriggerInteraction.Ignore);
        }

        private static Look Solid(Color c)
        {
            return new Look { Tint = c, Tile = 2f };
        }

        private static Look Install(string mat, float tile)
        {
            return new Look { Material = mat, Tint = Color.white, Tile = tile };
        }

        /// <summary>
        /// The surface of a stand-in: the level's material itself when it is in the install, else by the material's
        /// name (the levels' own names, e.g. ?Wood_BoardWall_Mat, ?Crane_Red_Mat, ?Light_Cube_On_Blue_Mat), else by
        /// kind and colour.
        /// </summary>
        public static Look Surface(string kind, Color c, string mat, int hash, Vector3 p)
        {
            string raw = mat ?? "?";
            bool known = !raw.StartsWith("?");
            string n = (known ? raw : raw.Substring(1)).ToLowerInvariant();
            bool white = c.r > 0.9f && c.g > 0.9f && c.b > 0.9f;

            switch (kind)
            {
                case "rock":
                    return new Look { Material = RockMat, Tint = RockGrey, Tile = 9f };
                case "container":
                    // Doors share their container's colour: the hash of a 6 m cell, not of the door itself.
                    return Solid(Containers[Hash(new Vector3(Mathf.Round(p.x / 6f), 0f, Mathf.Round(p.z / 6f))) % Containers.Length]);
                case "locker":
                    return Solid((hash & 1) == 0 ? new Color(0.18f, 0.62f, 0.64f) : new Color(0.80f, 0.24f, 0.20f));
                case "lamp":
                    return new Look { Tint = new Color(1f, 0.86f, 0.62f), Glow = true, Tile = 2f };
                case "window":
                    return Solid(new Color(0.62f, 0.80f, 0.90f, 0.55f));
                case "water":
                    return Solid(new Color(0.30f, 0.58f, 0.80f, 0.8f));
            }

            // By the material's name.
            if (Volume(n) || n.Contains("backdrop") || n.Contains("waterfall") || n.Contains("moon") || n.Contains("sky"))
            {
                return n.Contains("waterfall") ? Solid(new Color(0.55f, 0.75f, 0.92f, 0.55f)) : new Look { Skip = true };
            }

            if (known && Usable(raw) && !n.Contains("vertcolor") && !n.Contains("sign"))
            {
                return Install(raw, n.Contains("concrete") || n.Contains("rock") ? 4f : 2f);
            }

            if (n.Contains("light_cube_on"))
            {
                return new Look { Tint = n.Contains("blue") ? new Color(0.35f, 0.6f, 1f) : n.Contains("red") ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.85f, 0.45f), Glow = true, Tile = 2f };
            }

            if (n.Contains("light_cube_off"))
            {
                return Solid(new Color(0.3f, 0.3f, 0.32f));
            }

            if (n.Contains("wood") || n.Contains("propbox") || n.Contains("pallet") || n.Contains("crate") || n.Contains("board") || n.Contains("refreshment"))
            {
                if (n.Contains("white"))
                {
                    return Solid(new Color(0.93f, 0.91f, 0.86f));
                }

                if (n.Contains("blue"))
                {
                    return Solid(new Color(0.25f, 0.50f, 0.85f));
                }

                if (n.Contains("red") && !n.Contains("redorange"))
                {
                    return Install(WoodRedBrownMat, 2f);
                }

                if (n.Contains("darkorange"))
                {
                    return Install(WoodDarkOrangeMat, 2f);
                }

                if (n.Contains("boardwall") || n.Contains("dark") || n.Contains("medbrown"))
                {
                    return Install(WoodDarkMat, 2f);
                }

                if (n.Contains("neutral") || n.Contains("yellow"))
                {
                    return Install(WoodYellowMat, 2f);
                }

                if (n.Contains("paintedwall") || n.Contains("painted"))
                {
                    return Solid(new Color(0.86f, 0.80f, 0.68f));
                }

                return Install(n.Contains("redorange") ? WoodRedMat : WoodOrangeMat, 2f);
            }

            if (n.Contains("concrete") || n.Contains("cinderblock") || n.Contains("dam_"))
            {
                // Rec Room's dam and lobby concrete is mid grey (Concrete_LightGrey_2x_Mat draws nearly white).
                return Solid(n.Contains("cinderblock") ? new Color(0.70f, 0.68f, 0.64f) : new Color(0.62f, 0.62f, 0.60f));
            }

            if (n.Contains("paint_white") || n.Contains("plastic_white"))
            {
                return Install(PaintWhiteMat, 2f);
            }

            if (n.Contains("paint_grey") || n.Contains("perforated") || n.Contains("turbine") || n.Contains("galvanized"))
            {
                return Solid(new Color(0.55f, 0.56f, 0.58f));
            }

            if (n.Contains("lampladder") || n.Contains("metal_dull") || n.Contains("girder"))
            {
                return Solid(new Color(0.42f, 0.43f, 0.46f));
            }

            if (n.Contains("crane_red") || n.Contains("_red") || n.Contains("brick"))
            {
                return Solid(new Color(0.78f, 0.22f, 0.18f));
            }

            if (n.Contains("crane_blue") || n.Contains("ribbed_blue") || n.Contains("_blue"))
            {
                return Solid(new Color(0.22f, 0.42f, 0.80f));
            }

            if (n.Contains("cyan") || n.Contains("teal") || n.Contains("booth"))
            {
                return Solid(new Color(0.2f, 0.62f, 0.64f));
            }

            if (n.Contains("orange"))
            {
                return Solid(new Color(0.93f, 0.50f, 0.15f));
            }

            if (n.Contains("brown") || n.Contains("mud"))
            {
                return Solid(new Color(0.48f, 0.34f, 0.22f));
            }

            if (n.Contains("tarp_striped") || n.Contains("stripe"))
            {
                return Solid(new Color(0.86f, 0.24f, 0.22f));
            }

            if (n.Contains("tarp") || n.Contains("cloth") || n.Contains("curtain"))
            {
                return Solid(new Color(0.88f, 0.82f, 0.70f));
            }

            if (n.Contains("sign"))
            {
                return Solid(new Color(0.93f, 0.52f, 0.16f));
            }

            if (n.Contains("carpet"))
            {
                return Solid(new Color(0.62f, 0.16f, 0.16f));
            }

            if (n.Contains("wall") || n.Contains("plastic"))
            {
                return Solid(new Color(0.88f, 0.84f, 0.76f));
            }

            if (n.Contains("glass") || n.Contains("window"))
            {
                return Solid(new Color(0.62f, 0.80f, 0.90f, 0.55f));
            }

            if (n.Contains("cardboard") || n.Contains("box_"))
            {
                return Solid(new Color(0.74f, 0.57f, 0.38f));
            }

            if (n.Contains("ferris") || n.Contains("carousel"))
            {
                return Solid(new Color(0.92f, 0.90f, 0.86f));
            }

            if (n.Contains("potty"))
            {
                return Solid(new Color(0.25f, 0.45f, 0.80f));
            }

            if (n.Contains("metal") || n.Contains("corrugated"))
            {
                return Solid(new Color(0.60f, 0.62f, 0.64f));
            }

            if (c.a < 0.99f)
            {
                return Solid(new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp(c.a, 0.25f, 0.9f)));
            }

            if (kind == "wood" || Woodish(c))
            {
                return Install(Woody(c), 2f);
            }

            if (Greyish(c) && c.r > 0.55f && !white)
            {
                return Install(ConcreteMat, 4f);
            }

            return Solid(white ? new Color(0.86f, 0.86f, 0.84f) : new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)));
        }

        private static bool Woodish(Color c)
        {
            return c.r > c.g && c.g > c.b && c.r - c.b > 0.25f && c.r > 0.35f;
        }

        private static bool Greyish(Color c)
        {
            return Mathf.Abs(c.r - c.g) < 0.06f && Mathf.Abs(c.g - c.b) < 0.08f;
        }

        private static string Woody(Color c)
        {
            if (c.r < 0.55f)
            {
                return WoodDarkMat;
            }

            if (c.g / Mathf.Max(0.01f, c.r) > 0.72f)
            {
                return WoodYellowMat;
            }

            return c.g / Mathf.Max(0.01f, c.r) < 0.5f ? WoodRedMat : WoodOrangeMat;
        }

        /// <summary>The level's crown material at a tree (its LOD-named crown part within 4 m), or null.</summary>
        private static string TintNear(List<RrSceneMarker> tints, Vector3 p)
        {
            if (tints == null)
            {
                return null;
            }

            string best = null;
            float bestD = 16f;
            for (int i = 0; i < tints.Count; i++)
            {
                Vector3 d = tints[i].Position - p;
                d.y = 0f;
                float dd = d.sqrMagnitude;
                if (dd < bestD)
                {
                    bestD = dd;
                    best = tints[i].Name;
                }
            }

            return best;
        }

        private static Color CrownColor(string tint, string kind, int hash)
        {
            string t = (tint ?? "").ToLowerInvariant();
            if (t.Contains("gold"))
            {
                return new Color(0.95f, 0.72f, 0.20f);
            }

            if (t.Contains("red"))
            {
                return new Color(0.86f, 0.32f, 0.15f);
            }

            if (t.Contains("dark_green") || t.Contains("darkgreen"))
            {
                return new Color(0.28f, 0.47f, 0.21f);
            }

            if (t.Length > 0)
            {
                return kind == "tree_small" ? new Color(0.36f, 0.58f, 0.24f) : new Color(0.52f, 0.68f, 0.27f);
            }

            return Aspen[(hash >> 3) % Aspen.Length];
        }

        private static Color Shade(Color c, float k)
        {
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        }

        /// <summary>A small deterministic random stream (the same trees on every client).</summary>
        private struct Rng
        {
            private uint _s;

            public Rng(int seed)
            {
                _s = (uint)seed * 2654435761u + 0x9E3779B9u;
                if (_s == 0)
                {
                    _s = 1;
                }
            }

            public float Next()
            {
                _s ^= _s << 13;
                _s ^= _s >> 17;
                _s ^= _s << 5;
                return (_s & 0xFFFFFF) / 16777216f;
            }

            public float Range(float a, float b)
            {
                return a + (b - a) * Next();
            }
        }

        /// <summary>
        /// A low-poly tree filling its bounds: the local axis that points most up is the height, the other two the
        /// crown's radii (tree meshes are stored rotated -90 about x). Rec Room's mobile trees, by kind:
        /// - aspen: a slim white trunk to 80 % of the height and a tall crown of 5-6 clumps stacked up it, alternately
        ///   left and right, narrowing to the top, lower clumps a shade darker;
        /// - bush tree and oak: a short brown trunk and a broad crown of one big clump ringed by 5 smaller ones and a
        ///   top clump;
        /// - small bush: three clumps on the ground;
        /// - pine: a trunk and 4 stacked cones, darker at the bottom.
        /// </summary>
        private static void Tree(Dictionary<string, Acc> batches, Dictionary<string, Material> surfaces, Vector3 p, Quaternion q, Vector3 e, int hash, string kind, string tint, Stats st)
        {
            st.Trees++;
            Vector3 ax = q * Vector3.right, ay = q * Vector3.up, az = q * Vector3.forward;
            float ux = Mathf.Abs(ax.y), uy = Mathf.Abs(ay.y), uz = Mathf.Abs(az.y);
            float hh, r1, r2;
            if (uz >= ux && uz >= uy)
            {
                hh = e.z; r1 = e.x; r2 = e.y;
            }
            else if (uy >= ux)
            {
                hh = e.y; r1 = e.x; r2 = e.z;
            }
            else
            {
                hh = e.x; r1 = e.y; r2 = e.z;
            }

            float h = Mathf.Max(0.5f, hh * 2f);
            float r = Mathf.Max(0.3f, (r1 + r2) * 0.5f);
            float bottom = p.y - hh;
            var basePos = new Vector3(p.x, bottom, p.z);
            var rng = new Rng(hash);
            if (kind == "tree_pine")
            {
                float tr = Mathf.Clamp(h * 0.025f, 0.12f, 0.5f);
                Get(batches, Solid(Bark), p, surfaces).Frustum(basePos, tr, tr * 0.55f, h * 0.3f, 6);
                var tiers = new[] { PineDark, (hash & 1) == 0 ? PineGreen : PineDark, PineGreen, PineLight };
                for (int t = 0; t < 4; t++)
                {
                    float y0 = h * (0.12f + t * 0.19f);
                    float ch = h * (0.40f - t * 0.05f);
                    float cr = r * (1f - t * 0.21f) * rng.Range(0.92f, 1.05f);
                    Get(batches, Solid(tiers[t]), p, surfaces).Cone(basePos + Vector3.up * y0, cr, ch, 7, hash + t * 37);
                }

                return;
            }

            var col = CrownColor(tint, kind, hash);
            var dark = Get(batches, Solid(Shade(col, 0.82f)), p, surfaces);
            var mid = Get(batches, Solid(col), p, surfaces);
            if (kind == "tree_small")
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = (i * 120f + rng.Range(-25f, 25f)) * Mathf.Deg2Rad;
                    var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * 0.35f;
                    float cr = r * rng.Range(0.5f, 0.62f);
                    (i == 0 ? dark : mid).Blob(basePos + off + Vector3.up * h * 0.45f, Quaternion.identity, new Vector3(cr, h * 0.45f, cr), 7, 4, hash + i * 11);
                }

                return;
            }

            if (kind == "tree_aspen")
            {
                float tr = Mathf.Clamp(h * 0.02f, 0.08f, 0.4f);
                Get(batches, Solid(AspenBark), p, surfaces).Frustum(basePos, tr, tr * 0.6f, h * 0.8f, 6);
                int clumps = h > 7f ? 6 : 5;
                float side = ((hash >> 5) & 1) == 0 ? 1f : -1f;
                for (int i = 0; i < clumps; i++)
                {
                    float t = i / (float)(clumps - 1);
                    float cy = h * Mathf.Lerp(0.36f, 0.9f, t);
                    float cr = r * Mathf.Lerp(0.62f, 0.34f, t) * rng.Range(0.9f, 1.08f);
                    float a = rng.Range(0f, Mathf.PI * 2f);
                    var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * 0.22f * side * (i % 2 == 0 ? 1f : -1f);
                    (i < 2 ? dark : mid).Blob(basePos + off + Vector3.up * cy, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), new Vector3(cr, h * 0.14f, cr * 0.92f), 7, 4, hash + i * 13);
                }

                return;
            }

            // Bush tree and oak.
            {
                float tr = Mathf.Clamp(h * 0.035f, 0.12f, 0.6f);
                Get(batches, Solid(Bark), p, surfaces).Frustum(basePos, tr, tr * 0.6f, h * 0.5f, 6);
                float cy = h * 0.62f;
                mid.Blob(basePos + Vector3.up * cy, Quaternion.identity, new Vector3(r * 0.62f, h * 0.26f, r * 0.62f), 8, 4, hash);
                for (int i = 0; i < 5; i++)
                {
                    float a = (i * 72f + rng.Range(-20f, 20f)) * Mathf.Deg2Rad;
                    var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * rng.Range(0.42f, 0.52f);
                    float y = h * rng.Range(0.5f, 0.68f);
                    float cr = r * rng.Range(0.36f, 0.46f);
                    (i % 2 == 0 ? dark : mid).Blob(basePos + off + Vector3.up * y, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), new Vector3(cr, h * 0.17f, cr), 7, 4, hash + i * 17);
                }

                mid.Blob(basePos + Vector3.up * h * 0.84f, Quaternion.identity, new Vector3(r * 0.4f, h * 0.15f, r * 0.4f), 7, 4, hash + 97);
            }
        }

        /// <summary>
        /// A tyre stack (or a lone tyre) as Rec Room's own tyres: the stack's measured width gives the scale, its height
        /// the count (3 at 0.36 m for the 1.53 x 1.08 m TireStack), each turned and nudged a little. Without the
        /// install mesh: black rings.
        /// </summary>
        private static void TyreStack(Dictionary<string, Acc> batches, Dictionary<string, Material> surfaces, Dictionary<string, Mesh> meshes, List<Matrix4x4> tyres,
                                      Vector3 p, Quaternion q, Vector3 e, int hash, Stats st)
        {
            // The stack's own axes (a turned stack's world box is wider than the stack): the one nearest up is its height.
            float ux = Mathf.Abs((q * Vector3.right).y), uy = Mathf.Abs((q * Vector3.up).y), uz = Mathf.Abs((q * Vector3.forward).y);
            float half = uy >= ux && uy >= uz ? e.y : ux >= uz ? e.x : e.z;
            float radius = Mathf.Max(0.2f, (e.x + e.y + e.z - half) * 0.5f);
            float scale = radius / TyreRadius;
            float th = TyreHalfHeight * 2f * scale;
            int count = Mathf.Clamp(Mathf.RoundToInt(half * 2f / th), 1, 8);
            float bottom = p.y - half;
            var rng = new Rng(hash);
            Mesh mesh;
            bool install = meshes != null && meshes.TryGetValue(TyreMesh, out mesh) && mesh != null;
            for (int i = 0; i < count; i++)
            {
                var c = new Vector3(p.x + rng.Range(-0.03f, 0.03f), bottom + th * (i + 0.5f), p.z + rng.Range(-0.03f, 0.03f));
                if (install)
                {
                    tyres.Add(Matrix4x4.TRS(c, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), Vector3.one * scale));
                    st.Tyres++;
                }
                else
                {
                    var acc = Get(batches, Solid(Rubber), p, surfaces);
                    acc.Cylinder(c, Quaternion.identity, radius, th * 0.46f, 14);
                }
            }

            st.Boxes++;
        }

        private static void BuildTyres(Transform root, Dictionary<string, Mesh> meshes, Dictionary<string, Material> surfaces, List<Matrix4x4> tyres, Stats st)
        {
            Mesh mesh = meshes[TyreMesh];
            Material mat;
            if (!surfaces.TryGetValue(TyreMat, out mat) || mat == null)
            {
                mat = RrLook.Solid(Rubber);
            }

            var group = new GameObject("Tyres (Tire_LOD0)");
            group.transform.SetParent(root, false);
            if (mesh.isReadable)
            {
                var cells = new Dictionary<long, List<CombineInstance>>();
                foreach (var t in tyres)
                {
                    Vector3 c = t.GetColumn(3);
                    long key = ((long)Mathf.FloorToInt(c.x / Cell) << 32) ^ (uint)Mathf.FloorToInt(c.z / Cell);
                    List<CombineInstance> list;
                    if (!cells.TryGetValue(key, out list))
                    {
                        cells[key] = list = new List<CombineInstance>();
                    }

                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = t });
                }

                foreach (var kv in cells)
                {
                    var merged = new Mesh { name = "Rr Paintball tyres", indexFormat = IndexFormat.UInt32 };
                    merged.CombineMeshes(kv.Value.ToArray(), true, true, false);
                    var go = new GameObject("Tyres");
                    go.transform.SetParent(group.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = merged;
                    go.AddComponent<RrSceneKitTerrainChunk>().Mesh = merged;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    st.Batches++;
                    st.Vertices += merged.vertexCount;
                }

                return;
            }

            foreach (var t in tyres)
            {
                var go = new GameObject("Tyre");
                go.transform.SetParent(group.transform, false);
                go.transform.localPosition = t.GetColumn(3);
                go.transform.localRotation = t.rotation;
                go.transform.localScale = t.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
            }

            st.Batches += tyres.Count;
        }

        /// <summary>
        /// A cliff or rock as one of Rec Room's RecRoyale rocks: the rock whose proportions (width, height, depth, either
        /// way round) are nearest the item's rotated bounds, stretched to fill them, standing on the item's most upward
        /// axis. False when no rock mesh is loaded.
        /// </summary>
        private static bool Rock(Dictionary<string, Mesh> meshes, List<KeyValuePair<int, Matrix4x4>> rocks, Vector3 p, Quaternion q, Vector3 e, int hash)
        {
            if (meshes == null)
            {
                return false;
            }

            Vector3[] ax = { q * Vector3.right, q * Vector3.up, q * Vector3.forward };
            float[] ex = { e.x, e.y, e.z };
            int upI = 0;
            for (int i = 1; i < 3; i++)
            {
                if (Mathf.Abs(ax[i].y) > Mathf.Abs(ax[upI].y))
                {
                    upI = i;
                }
            }

            Vector3 up = ax[upI].y < 0f ? -ax[upI] : ax[upI];
            int a = (upI + 1) % 3, b = (upI + 2) % 3;
            int best = -1, bestSwap = 0;
            float bestScore = float.MaxValue;
            for (int k = 0; k < RockMeshes.Length; k++)
            {
                Mesh m;
                if (!meshes.TryGetValue(RockMeshes[k], out m) || m == null)
                {
                    continue;
                }

                Vector3 mh = m.bounds.extents;
                for (int swap = 0; swap < 2; swap++)
                {
                    float sx = (swap == 0 ? ex[b] : ex[a]) / Mathf.Max(0.01f, mh.x);
                    float sy = ex[upI] / Mathf.Max(0.01f, mh.y);
                    float sz = (swap == 0 ? ex[a] : ex[b]) / Mathf.Max(0.01f, mh.z);
                    float lx = Mathf.Log(sx), ly = Mathf.Log(sy), lz = Mathf.Log(sz), mean = (lx + ly + lz) / 3f;
                    float score = Mathf.Abs(lx - mean) + Mathf.Abs(ly - mean) + Mathf.Abs(lz - mean) + ((hash >> (k + swap)) & 1) * 0.05f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = k;
                        bestSwap = swap;
                    }
                }
            }

            if (best < 0)
            {
                return false;
            }

            var mesh = meshes[RockMeshes[best]];
            Vector3 fwd = bestSwap == 0 ? ax[a] : ax[b];
            fwd = Vector3.ProjectOnPlane(fwd, up).normalized;
            if (fwd.sqrMagnitude < 0.5f)
            {
                return false;
            }

            var rot = Quaternion.LookRotation(fwd, up);
            Vector3 mhx = mesh.bounds.extents;
            var scale = new Vector3((bestSwap == 0 ? ex[b] : ex[a]) / Mathf.Max(0.01f, mhx.x), ex[upI] / Mathf.Max(0.01f, mhx.y),
                                    (bestSwap == 0 ? ex[a] : ex[b]) / Mathf.Max(0.01f, mhx.z));
            rocks.Add(new KeyValuePair<int, Matrix4x4>(best, Matrix4x4.TRS(p, rot, scale) * Matrix4x4.Translate(-mesh.bounds.center)));
            return true;
        }

        private static void BuildRocks(Transform root, Dictionary<string, Mesh> meshes, Dictionary<string, Material> surfaces, List<KeyValuePair<int, Matrix4x4>> rocks, Stats st)
        {
            Material mat;
            if (!surfaces.TryGetValue(RockFrontierMat, out mat) || mat == null)
            {
                surfaces.TryGetValue(RockMat, out mat);
            }

            if (mat == null)
            {
                mat = RrLook.Solid(RockGrey);
            }

            var group = new GameObject("Rocks (RecRoyale_Rock_*)");
            group.transform.SetParent(root, false);
            var cells = new Dictionary<long, List<CombineInstance>>();
            foreach (var kv in rocks)
            {
                var mesh = meshes[RockMeshes[kv.Key]];
                Vector3 c = kv.Value.GetColumn(3);
                if (!mesh.isReadable)
                {
                    // Not readable: its own object (position, rotation and scale from the matrix).
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(group.transform, false);
                    go.transform.localPosition = c;
                    go.transform.localRotation = kv.Value.rotation;
                    go.transform.localScale = kv.Value.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    st.Batches++;
                    continue;
                }

                long key = ((long)Mathf.FloorToInt(c.x / (Cell * 2f)) << 32) ^ (uint)Mathf.FloorToInt(c.z / (Cell * 2f));
                List<CombineInstance> list;
                if (!cells.TryGetValue(key, out list))
                {
                    cells[key] = list = new List<CombineInstance>();
                }

                list.Add(new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = kv.Value });
            }

            foreach (var kv in cells)
            {
                var merged = new Mesh { name = "Rr Paintball rocks", indexFormat = IndexFormat.UInt32 };
                merged.CombineMeshes(kv.Value.ToArray(), true, true, false);
                merged.RecalculateNormals();
                var go = new GameObject("Rocks");
                go.transform.SetParent(group.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = merged;
                go.AddComponent<RrSceneKitTerrainChunk>().Mesh = merged;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                st.Batches++;
                st.Vertices += merged.vertexCount;
            }
        }

        /// <summary>
        /// A shipping container: its box, and on a hull (sides over 1.8 x 1.2 m) the vertical corrugation ribs
        /// down its long sides (all round on a near-square cover block) every 0.55 m and the dark top and bottom rails, so it reads as Rec Room's container.
        /// </summary>
        private static void Container(Dictionary<string, Acc> batches, Dictionary<string, Material> surfaces, Vector3 p, Quaternion q, Vector3 e, Look look)
        {
            var body = Get(batches, look, p, surfaces);
            body.Box(p, q, e, look.Tile);
            Vector3[] axes = { q * Vector3.right, q * Vector3.up, q * Vector3.forward };
            float[] ext = { e.x, e.y, e.z };
            int upI = 0;
            for (int i = 1; i < 3; i++)
            {
                if (Mathf.Abs(axes[i].y) > Mathf.Abs(axes[upI].y))
                {
                    upI = i;
                }
            }

            int a = (upI + 1) % 3, b = (upI + 2) % 3;
            int longI = ext[a] >= ext[b] ? a : b, shortI = longI == a ? b : a;
            float el = ext[longI], es = ext[shortI], eu = ext[upI];
            if (el < 0.9f || es < 0.6f || eu < 0.6f)
            {
                return;
            }

            var ribs = Get(batches, Solid(Shade(look.Tint, 0.86f)), p, surfaces);
            var rails = Get(batches, Solid(Shade(look.Tint, 0.62f)), p, surfaces);
            Ribs(ribs, rails, p, axes[longI], axes[shortI], axes[upI], el, es, eu);
            if (el < es * 1.4f)
            {
                // A short, near-square box (the arena's cover blocks): ribbed all round.
                Ribs(ribs, rails, p, axes[shortI], axes[longI], axes[upI], es, el, eu);
            }
        }

        /// <summary>Ribs every 0.55 m and top and bottom rails on the two faces of a box across S (running along L, U up).</summary>
        private static void Ribs(Acc ribs, Acc rails, Vector3 p, Vector3 L, Vector3 S, Vector3 U, float el, float es, float eu)
        {
            var rot = Quaternion.LookRotation(S, U);   // local z = S (out of the face), y = up, x = along
            int count = Mathf.Max(2, Mathf.FloorToInt(el * 2f / 0.55f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 face = p + S * (side * (es + 0.025f));
                for (int i = 1; i < count; i++)
                {
                    float along = -el + i * (el * 2f / count);
                    ribs.Box(face + L * along, rot, new Vector3(0.08f, eu * 0.86f, 0.025f), 2f);
                }

                rails.Box(face + U * (eu - 0.07f), rot, new Vector3(el, 0.07f, 0.035f), 2f);
                rails.Box(face - U * (eu - 0.07f), rot, new Vector3(el, 0.07f, 0.035f), 2f);
            }
        }

        /// <summary>
        /// A locker hull or door (Locker_Individual_Hull 0.74-0.78 x 2.0 x 0.67 m, _Door 0.64 x 1.74 x 0.09 m): red or
        /// teal by its place along the row (hull and door share it), a door with three vent slits and a handle on both
        /// faces (the one inside the hull is hidden).
        /// </summary>
        private static void Locker(Dictionary<string, Acc> batches, Dictionary<string, Material> surfaces, Vector3 p, Quaternion q, Vector3 e)
        {
            Vector3 along = q * Vector3.right;
            int slot = Mathf.FloorToInt(Vector3.Dot(p, along) / 0.78f + 1000.5f);
            var col = (slot % 3) == 1 ? new Color(0.80f, 0.24f, 0.20f) : new Color(0.18f, 0.62f, 0.64f);
            Get(batches, Solid(col), p, surfaces).Box(p, q, e, 2f);
            if (e.z > 0.08f)
            {
                return;
            }

            var dark = Get(batches, Solid(Shade(col, 0.45f)), p, surfaces);
            Vector3 n = q * Vector3.forward, up = q * Vector3.up;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 face = p + n * (side * (e.z + 0.004f));
                for (int k = 0; k < 3; k++)
                {
                    dark.Box(face + up * (e.y * 0.62f + k * 0.075f), q, new Vector3(e.x * 0.62f, 0.013f, 0.004f), 2f);
                }

                dark.Box(face + along * (e.x * 0.7f) + n * (side * 0.012f), q, new Vector3(0.018f, 0.075f, 0.012f), 2f);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static Acc Get(Dictionary<string, Acc> batches, Look look, Vector3 p, Dictionary<string, Material> surfaces)
        {
            Material m = null;
            if (look.Material != null && (surfaces == null || !surfaces.TryGetValue(look.Material, out m) || m == null))
            {
                // Not loaded (not in this install): its colour instead.
                look = Solid(look.Material == RockMat ? RockGrey : look.Material.Contains("Concrete") ? new Color(0.78f, 0.78f, 0.76f) :
                             look.Material.Contains("Paint_White") ? new Color(0.92f, 0.90f, 0.86f) : look.Material == WarmGreyMat ? new Color(0.45f, 0.41f, 0.37f) :
                             look.Material == DarkGreyMat ? new Color(0.33f, 0.33f, 0.33f) : look.Material == GalvanizedMat ? new Color(0.62f, 0.64f, 0.66f) : RrLook.Wood);
                m = null;
            }

            string key = look.Key + "@" + Mathf.FloorToInt(p.x / Cell) + "," + Mathf.FloorToInt(p.z / Cell);
            Acc a;
            if (!batches.TryGetValue(key, out a))
            {
                a = new Acc { Material = m, Tint = look.Tint, Glow = look.Glow };
                batches[key] = a;
            }

            while (a.V.Count > MaxVerts - 600)
            {
                if (a.Next == null)
                {
                    a.Next = new Acc { Material = a.Material, Tint = a.Tint, Glow = a.Glow };
                }

                a = a.Next;
            }

            return a;
        }

        private static string ColorKey(Color c)
        {
            return Mathf.RoundToInt(c.r * 40f) + "," + Mathf.RoundToInt(c.g * 40f) + "," + Mathf.RoundToInt(c.b * 40f) + "," + Mathf.RoundToInt(c.a * 10f);
        }

        private static int Hash(Vector3 p)
        {
            unchecked
            {
                int h = Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.y * 10f) * 19349663 ^ Mathf.RoundToInt(p.z * 10f) * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }

        private static Vector3 WorldHalf(Quaternion q, Vector3 e)
        {
            Vector3 ax = q * new Vector3(e.x, 0f, 0f), ay = q * new Vector3(0f, e.y, 0f), az = q * new Vector3(0f, 0f, e.z);
            return new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
                               Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
        }

        internal static Vector3 Vec(string s, Vector3 fallback)
        {
            if (string.IsNullOrEmpty(s))
            {
                return fallback;
            }

            var p = s.Split(',');
            float x, y, z;
            if (p.Length >= 3 && Num(p[0], out x) && Num(p[1], out y) && Num(p[2], out z))
            {
                return new Vector3(x, y, z);
            }

            return fallback;
        }

        internal static Color Col(string s, Color fallback)
        {
            if (string.IsNullOrEmpty(s))
            {
                return fallback;
            }

            var p = s.Split(',');
            float r, g, b, a = 1f;
            if (p.Length >= 3 && Num(p[0], out r) && Num(p[1], out g) && Num(p[2], out b))
            {
                if (p.Length >= 4)
                {
                    Num(p[3], out a);
                }

                return new Color(r, g, b, a);
            }

            return fallback;
        }

        private static bool Num(string s, out float v)
        {
            return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);
        }

        /// <summary>A mesh under construction: flat-shaded faces, UVs in metres / tile, white vertex colours.</summary>
        private sealed class Acc
        {
            public Material Material;
            public Color Tint;
            public bool Glow;
            public Acc Next;
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(n); N.Add(n); N.Add(n); N.Add(n);
                U.Add(ua); U.Add(ub); U.Add(uc); U.Add(ud);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

            private void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = V.Count;
                var n = Vector3.Cross(b - a, c - a).normalized;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
                U.Add(new Vector2(a.x + a.y, a.z) * 0.25f); U.Add(new Vector2(b.x + b.y, b.z) * 0.25f); U.Add(new Vector2(c.x + c.y, c.z) * 0.25f);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            /// <summary>
            /// One face of a box: centre, outward normal n, in-plane axis v (u = n x v), half sizes along u and v. Wound
            /// clockwise seen from outside (corners -u-v, -u+v, +u+v, +u-v with u x v = -n); UVs are metres / tile.
            /// </summary>
            public void Face(Vector3 c, Vector3 n, Vector3 v, float eu, float ev, float tile)
            {
                float inv = 1f / Mathf.Max(0.05f, tile);
                Vector3 u = Vector3.Cross(n, v);
                Vector3 U2 = u * eu, V2 = v * ev;
                float su = eu * 2f * inv, sv = ev * 2f * inv;
                Quad(c - U2 - V2, c - U2 + V2, c + U2 + V2, c + U2 - V2, n, Vector2.zero, new Vector2(0f, sv), new Vector2(su, sv), new Vector2(su, 0f));
            }

            /// <summary>An oriented box (centre, rotation, half extents); each face's UVs are its size in metres / tile.</summary>
            public void Box(Vector3 c, Quaternion q, Vector3 e, float tile)
            {
                Vector3 x = q * Vector3.right, y = q * Vector3.up, z = q * Vector3.forward;
                Face(c + z * e.z, z, y, e.x, e.y, tile);
                Face(c - z * e.z, -z, y, e.x, e.y, tile);
                Face(c + x * e.x, x, y, e.z, e.y, tile);
                Face(c - x * e.x, -x, y, e.z, e.y, tile);
                Face(c + y * e.y, y, z, e.x, e.z, tile);
                Face(c - y * e.y, -y, z, e.x, e.z, tile);
            }

            /// <summary>A cylinder along q's up axis (centre, radius, half height), flat-shaded sides and caps.</summary>
            public void Cylinder(Vector3 c, Quaternion q, float r, float hh, int sides)
            {
                Vector3 up = q * Vector3.up;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = q * new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, d1 = q * new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
                    Vector3 n = (d0 + d1).normalized;
                    float u0 = a0 * r, u1 = a1 * r;
                    Quad(c + d0 - up * hh, c + d0 + up * hh, c + d1 + up * hh, c + d1 - up * hh, n,
                         new Vector2(u0, 0f), new Vector2(u0, hh * 2f), new Vector2(u1, hh * 2f), new Vector2(u1, 0f));
                    Tri(c + up * hh, c + d1 + up * hh, c + d0 + up * hh);
                    Tri(c - up * hh, c + d0 - up * hh, c + d1 - up * hh);
                }
            }

            /// <summary>An upright tapered trunk: base centre, base and top radius, height; flat-shaded, capped on top.</summary>
            public void Frustum(Vector3 b, float r0, float r1, float h, int sides)
            {
                Vector3 top = b + Vector3.up * h;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 n = (d0 + d1).normalized;
                    Quad(b + d0 * r0, top + d0 * r1, top + d1 * r1, b + d1 * r0, n,
                         new Vector2(a0 * r0, 0f), new Vector2(a0 * r0, h), new Vector2(a1 * r0, h), new Vector2(a1 * r0, 0f));
                    Tri(top, top + d1 * r1, top + d0 * r1);
                }
            }

            /// <summary>An upright cone (base centre, radius, height).</summary>
            public void Cone(Vector3 b, float r, float h, int sides, int seed)
            {
                Vector3 top = b + Vector3.up * h;
                float spin = (seed % 360) * Mathf.Deg2Rad;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = spin + i * Mathf.PI * 2f / sides, a1 = spin + (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 p0 = b + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, p1 = b + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
                    Tri(top, p1, p0);
                    Tri(b, p0, p1);
                }
            }

            /// <summary>A faceted ellipsoid (centre, rotation, radii): Rec Room's low-poly crowns and rocks, jittered by seed.</summary>
            public void Blob(Vector3 c, Quaternion q, Vector3 rad, int seg, int rings, int seed)
            {
                var pts = new Vector3[(rings + 1) * seg];
                for (int j = 0; j <= rings; j++)
                {
                    float v = Mathf.PI * j / rings;
                    for (int i = 0; i < seg; i++)
                    {
                        float u = Mathf.PI * 2f * i / seg + (j % 2) * Mathf.PI / seg + (seed % 1000) * 0.0063f;
                        float jitter = 1f + ((((seed >> ((i + j) % 17)) & 3)) - 1.5f) * 0.06f;
                        var local = new Vector3(Mathf.Sin(v) * Mathf.Cos(u) * rad.x * jitter, Mathf.Cos(v) * rad.y, Mathf.Sin(v) * Mathf.Sin(u) * rad.z * jitter);
                        pts[j * seg + i] = c + q * local;
                    }
                }

                for (int j = 0; j < rings; j++)
                {
                    for (int i = 0; i < seg; i++)
                    {
                        int i1 = (i + 1) % seg;
                        Vector3 a = pts[j * seg + i], b = pts[j * seg + i1], d = pts[(j + 1) * seg + i], e = pts[(j + 1) * seg + i1];
                        if (j > 0)
                        {
                            Tri(a, b, d);
                        }

                        if (j < rings - 1)
                        {
                            Tri(b, e, d);
                        }
                    }
                }
            }

            public Mesh Make(string name)
            {
                var m = new Mesh { name = name };
                m.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetUVs(0, U);
                var tan = new Vector4[V.Count];
                var col = new Color32[V.Count];
                for (int i = 0; i < tan.Length; i++)
                {
                    var n = N[i];
                    var t = Vector3.Cross(Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up, n).normalized;
                    tan[i] = new Vector4(t.x, t.y, t.z, -1f);
                    col[i] = new Color32(255, 255, 255, 255);
                }

                m.tangents = tan;
                m.colors32 = col;
                m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
