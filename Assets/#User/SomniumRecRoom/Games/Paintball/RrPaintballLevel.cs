using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// Builds one Paintball level (a map or the lobby) from its generated placement text, the way Rec Room's own
    /// level has it, from the player's install:
    /// 1. on the worker: inflate the RRSZ1 text, parse the RRSK records (<see cref="RrSceneData"/>) and the height
    ///    grids (<see cref="RrSceneKitTerrain.Parse"/>);
    /// 2. <see cref="RrSceneKit.Build(RrSceneData, Transform, RrSceneKitOptions, Action{RrSceneKitResult})"/>:
    ///    Rec Room's own meshes and materials where the install has them (41-94 % of a map's renderers), its
    ///    colliders exactly as the level has them, lights, loops and the gameplay markers;
    /// 3. the terrain from the H grids, textured with install ground materials (<see cref="GroundMaterial"/>);
    /// 4. the stand-ins for meshes the install lacks (<see cref="RrPaintballDress"/>);
    /// 5. the gameplay data (<see cref="RrPaintballMap.FromMarkers"/>).
    /// The level container sits at the map's room-local slot; the kit puts the scene's anchor point there. Destroying
    /// <see cref="Root"/> cancels a build in progress and frees the level's generated meshes.
    /// </summary>
    internal sealed class RrPaintballLevel
    {
        public readonly RrPaintballMap Map;
        public GameObject Root;
        public RrSceneKitResult Kit;
        public RrSceneKitTerrain.Result Terrain;
        public RrPaintballDress.Stats Dress;
        public List<RrSceneKitGrid> Grids;
        public bool Done;
        public string Error;
        public float Progress;
        public float Seconds;

        /// <summary>Ground and surface materials by install name, shared by the room's builds (loaded once).</summary>
        public static readonly Dictionary<string, Material> Surfaces = new Dictionary<string, Material>();

        /// <summary>Install meshes the stand-ins place themselves (Tire_LOD0 for the tyre stacks), loaded once.</summary>
        public static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        public const string GrassMat = "Goblin_Ground_Green_Mat";
        public const string FloorTreadMat = "Metal_FloorTread_Shiny_Mat";

        public RrPaintballLevel(RrPaintballMap map)
        {
            Map = map;
        }

        public bool Alive
        {
            get { return Root != null; }
        }

        public IEnumerator Build(Transform room, string raw, HashSet<string> skipNames)
        {
            float t0 = Time.realtimeSinceStartup;
            Root = new GameObject("Level " + Map.Key);
            Root.transform.SetParent(room, false);
            Root.transform.localPosition = Map.Slot;
            var root = Root;
            Progress = 0.02f;

            // 1. parse (worker)
            RrSceneData data = null;
            string text = null;
            var worker = RrMain.Instance != null ? RrMain.Instance.Worker : null;
            if (worker != null)
            {
                bool finished = false;
                worker.Post(() =>
                {
                    var plain = RrSceneKitData.Decode(raw);
                    var d = RrSceneData.Parse(plain);
                    var g = RrSceneKitTerrain.Parse(plain);
                    return new object[] { d, g };
                }, (res, e) =>
                {
                    var arr = res as object[];
                    if (arr != null)
                    {
                        data = arr[0] as RrSceneData;
                        Grids = arr[1] as List<RrSceneKitGrid>;
                    }

                    if (e != null)
                    {
                        Error = e.GetType().Name + ": " + e.Message;
                    }

                    finished = true;
                });
                while (!finished)
                {
                    yield return null;
                }
            }
            else
            {
                try
                {
                    text = RrSceneKitData.Decode(raw);
                    data = RrSceneData.Parse(text);
                    Grids = RrSceneKitTerrain.Parse(text);
                }
                catch (Exception e)
                {
                    Error = e.GetType().Name + ": " + e.Message;
                }
            }

            if (data == null || root == null)
            {
                Error = Error ?? "no data";
                RrLog.Warn("Paintball", Map.Name + ": level parse failed: " + Error);
                Done = true;
                yield break;
            }

            Progress = 0.1f;
            if (Surfaces.Count == 0 || !Surfaces.ContainsKey(GrassMat))
            {
                foreach (var name in RrPaintballDress.InstallSurfaces)
                {
                    yield return LoadSurface(name);
                }

                yield return LoadSurface(GrassMat);
                yield return LoadSurface(FloorTreadMat);
            }

            // Any named install material a grid asks for (a known name, not a '?guess').
            if (Grids != null)
            {
                foreach (var g in Grids)
                {
                    string m = g.Material;
                    if (!string.IsNullOrEmpty(m) && !m.StartsWith("?") && !Surfaces.ContainsKey(m))
                    {
                        yield return LoadSurface(m);
                    }
                }
            }

            // 2. the kit
            var opt = new RrSceneKitOptions
            {
                Position = -Map.Anchor, SkipNames = skipNames, RootName = "Kit " + Map.Key, MaxLights = Map.Index < 0 ? 16 : 8, MaxAudio = 10,
                LoadTimeout = 50f
            };
            yield return RrSceneKit.Build(data, root.transform, opt, r => Kit = r);
            if (root == null || Kit == null || Kit.Cancelled)
            {
                Done = true;
                yield break;
            }

            Progress = 0.7f;

            // 3. terrain
            var topt = new RrSceneKitTerrain.Options
            {
                Position = -Map.Anchor, RootName = "Terrain " + Map.Key, TileFor = TileFor, CutSteep = CutSteep,
                ClipRadius = Map.Index >= 0 ? RrPaintballMaps.ClipRadius : 0f, ClipCenter = Map.Anchor
            };
            // A far grid sampled from a building's merged mesh (Merry Midway's stall roofs, 115 x 135 m) is not ground:
            // seen from the field it stands as grey slabs. Only terrain, fields and cliffs are drawn far.
            if (Grids != null)
            {
                Grids.RemoveAll(g => g.Far && CutSteep(g));
            }

            yield return RrSceneKitTerrain.Build(Grids, root.transform, topt, GroundMaterial, r => Terrain = r);
            if (root == null)
            {
                Done = true;
                yield break;
            }

            Progress = 0.85f;

            // 4. stand-ins (their install surfaces first)
            var dress = new List<RrSceneMarker>();
            var tints = new List<RrSceneMarker>();
            foreach (var m in Kit.Markers)
            {
                if (m.Kind == "dress")
                {
                    dress.Add(m);
                }
                else if (m.Kind == "tint")
                {
                    tints.Add(m);
                }
            }

            var need = new HashSet<string>();
            RrPaintballDress.NeededMaterials(dress, need);
            foreach (var name in need)
            {
                if (!Surfaces.ContainsKey(name))
                {
                    yield return LoadSurface(name);
                }
            }

            if (!Meshes.ContainsKey(RrPaintballDress.TyreMesh))
            {
                yield return LoadMesh(RrPaintballDress.TyreMesh);
                foreach (var rock in RrPaintballDress.RockMeshes)
                {
                    yield return LoadMesh(rock);
                }
            }

            yield return RrPaintballDress.Build(dress, tints, data.Colliders, Map.Anchor, root.transform, Surfaces, Meshes, Map.Index < 0, s => Dress = s);
            if (root == null)
            {
                Done = true;
                yield break;
            }

            // 5. gameplay data
            Map.FromMarkers(Kit.Markers);
            Map.Root = root.transform;
            Map.Built = true;
            Progress = 1f;
            Seconds = Time.realtimeSinceStartup - t0;
            Done = true;
            RrLog.Info("Paintball", Map.Name + " level built in " + Seconds.ToString("0.0") + " s: kit " + Kit.Renderers + " renderers (" +
                                    Kit.MissingMeshes.Count + " meshes missing), " + (Kit.BoxColliders + Kit.MeshColliders + Kit.OtherColliders) +
                                    " colliders; terrain " + (Terrain != null ? Terrain.Summary() : "none") + "; " + (Dress != null ? Dress.ToString() : "no stand-ins") +
                                    "; spawns " + Map.Spawns[0].Count + "/" + Map.Spawns[1].Count + ", flags " + Map.FlagStands[0].ToString("F1") + " / " +
                                    Map.FlagStands[1].ToString("F1") + ", " + Map.Weapons.Count + " weapon spots, " + Map.Boards.Count + " boards");
        }

        private static IEnumerator LoadSurface(string name)
        {
            var assets = RrAssets.Instance;
            if (assets == null || Surfaces.ContainsKey(name))
            {
                yield break;
            }

            RrEntry e;
            if (!assets.TryFind(name, 21, out e))
            {
                Surfaces[name] = null;
                yield break;
            }

            Material m = null;
            yield return assets.LoadRoutine<Material>(name, 21, x => m = x);
            Surfaces[name] = m != null ? RrMaterials.Fix(m) : null;
        }

        /// <summary>An install mesh by name (class 43), kept for the room's later builds; null when this install lacks it.</summary>
        private static IEnumerator LoadMesh(string name)
        {
            var assets = RrAssets.Instance;
            if (assets == null || Meshes.ContainsKey(name))
            {
                yield break;
            }

            RrEntry e;
            if (!assets.TryFind(name, 43, out e))
            {
                Meshes[name] = null;
                yield break;
            }

            Mesh m = null;
            yield return assets.LoadRoutine<Mesh>(name, 43, x => m = x);
            Meshes[name] = m;
            RrLog.Info("Paintball", "install mesh " + name + (m != null ? ": " + m.vertexCount + " vertices, readable " + m.isReadable : ": not loaded"));
        }

        /// <summary>
        /// Steep cells are dropped only from grids of buildings (a static batch or a merged ProBuilder mesh sampled from
        /// above: a roof edge over a floor would become a sloped sheet); cliffs, fields and ground keep every cell.
        /// </summary>
        private static bool CutSteep(RrSceneKitGrid g)
        {
            if (g.Source != "mesh")
            {
                return false;
            }

            string k = Kind(g);
            if (k == "rock" || k == "grass" || k == "dirt")
            {
                return false;
            }

            return true;
        }

        /// <summary>Metres per texture repeat for a grid's surface.</summary>
        private static float TileFor(RrSceneKitGrid g)
        {
            string k = Kind(g);
            switch (k)
            {
                case "rock": return 10f;
                case "concrete": return 4f;
                case "wood": return 2f;
                case "metal": return 2f;
                default: return 5f;
            }
        }

        /// <summary>What a grid's surface is, from its material name (the level's own when not in the install).</summary>
        private static string Kind(RrSceneKitGrid g)
        {
            string m = (g.Material ?? "") + " " + (g.Get("was") ?? "") + " " + g.Name;
            m = m.ToLowerInvariant();
            if (g.Source == "terrain")
            {
                return "grass";
            }

            if (m.Contains("rock") || m.Contains("cliff") || m.Contains("quarryfg"))
            {
                return "rock";
            }

            if (m.Contains("concrete"))
            {
                return "concrete";
            }

            if (m.Contains("dirt"))
            {
                return "dirt";
            }

            if (m.Contains("wood"))
            {
                return "wood";
            }

            if (m.Contains("metal") || m.Contains("tread") || m.Contains("rubber"))
            {
                return "metal";
            }

            if (m.Contains("tile") || m.Contains("linoleum") || m.Contains("paint_white") || m.Contains("shingle"))
            {
                return "tile";
            }

            return "grass";
        }

        /// <summary>The install material a grid is drawn with (grass, rock, concrete, wood), or a maker-pen colour.</summary>
        public static Material GroundMaterial(RrSceneKitGrid g)
        {
            Material m;
            string name = g.Material;
            if (!string.IsNullOrEmpty(name) && !name.StartsWith("?") && g.Get("mat.guess") != "1" && Surfaces.TryGetValue(name, out m) && m != null)
            {
                return m;
            }

            switch (Kind(g))
            {
                case "rock":
                    return Pick(RrPaintballDress.RockMat, new Color(0.5f, 0.48f, 0.45f));
                case "concrete":
                    return Pick(RrPaintballDress.ConcreteMat, new Color(0.7f, 0.7f, 0.68f));
                case "wood":
                    return Pick(RrPaintballDress.WoodOrangeMat, RrLook.Wood);
                case "dirt":
                    return RrLook.Solid(new Color(0.52f, 0.40f, 0.28f));
                case "metal":
                    return Pick(FloorTreadMat, new Color(0.5f, 0.52f, 0.55f));
                case "tile":
                    string was = (g.Material ?? "").ToLowerInvariant();
                    return RrLook.Solid(was.Contains("shingle") ? new Color(0.55f, 0.25f, 0.2f) : was.Contains("checker") ? new Color(0.85f, 0.85f, 0.82f) : new Color(0.8f, 0.78f, 0.72f));
                default:
                    // Rec Room's paintball grass is a bright, clean green (the install has no grass texture: the
                    // levels' terrain layers are Android-only; Goblin_Ground_Green_Mat is a patterned quest tile).
                    return RrLook.Solid(g.Far ? new Color(0.40f, 0.60f, 0.27f) : new Color(0.44f, 0.64f, 0.28f));
            }
        }

        private static Material Pick(string name, Color fallback)
        {
            Material m;
            return Surfaces.TryGetValue(name, out m) && m != null ? m : RrLook.Solid(fallback);
        }

        public void Destroy()
        {
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root);
                Root = null;
            }

            Map.Built = false;
            Map.Root = null;
        }
    }
}
