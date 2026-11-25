using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron
{
    /// <summary>
    /// One room of The Rise of Jumbotron (the Foyer, the Armory or an act) built from Rec Room's own Android level
    /// (DN/refdata/apk/Jumbotron_&lt;Room&gt;.rrsk, levels 38-48 = Quest_Scifi_*; generated into Scenes/) with
    /// <see cref="RrSceneKit"/> and <see cref="RrSceneKitTerrain"/>. Scene point p lands at room-local p - <see cref="Anchor"/>.
    ///
    /// What the level gives (measured, per room in DN/iter8/jumbotron.md): Rec Room's install meshes at their poses (26-72 %
    /// of the renderers), every collider as the level has it, 10-74 realtime lights, the hull's editor-merged static
    /// batches as stand-in boxes and height grids, and the gameplay markers: the team's arrival (Player7SpawnPoint (1..4),
    /// the Armory's PlayerSpawnPoint x4, the Foyer's Player1..8SpawnPoint), the exit [Quest_SciFi_RoomExitDoor] and the
    /// [Quest_SciFi_RoomDoorTrigger] pad 2.4-3.0 m in front of it, the credit breakables [SciFatPot] (50 credits), the laser
    /// weapons, and (from DN/iter8/jumbotron_markers.pl) the EncountersV2 fights: enemy spawn points, spawn regions and
    /// the Overlap Volume triggers whose world scale is their size.
    ///
    /// Clean-up before the kit builds: the sliding leaves of every sci-fi door (install meshes door_L / door_R,
    /// Door_SciFi_Cardboard_mat) are taken out of the merge and built as their own objects, so the exit door can open;
    /// pots and weapons are taken out (the quest's breakables and refill racks stand at the same poses); height grids of
    /// area fog, portal glow and invisible nav walls are dropped (they are not floors); records outside
    /// <see cref="Filter"/> (the Space act's 640 m backdrop) and inside the <see cref="DropBoxes"/> (the Jumbotron's own
    /// voxel shell, rebuilt as the boss) are left out.
    /// </summary>
    public sealed class RrJumbotronArea
    {
        public readonly string Scene;
        public readonly Vector3 Anchor;

        /// <summary>Scene-space box the room is cut to (null: everything).</summary>
        public Bounds? Filter;

        /// <summary>
        /// Iteration 10: the light grid covers only the kit's core (the placed records' box plus RrEnvironment's 3 m
        /// margin), not the level's B box cut to where its lights reach. Set for Space (StadiumEntry), where two r60
        /// spots stretched the grid over 126 x 90 x 180 m (cells 5.3 x 7.4 x 7.5 m) around a 35 x 15 x 113 m play strip.
        /// </summary>
        public bool GridToCore;

        /// <summary>Scene-space boxes whose stand-ins and colliders are dropped (set pieces we rebuild).</summary>
        public readonly List<Bounds> DropBoxes = new List<Bounds>();

        /// <summary>Install mesh names the kit leaves out (per room).</summary>
        public readonly HashSet<string> Skip = new HashSet<string>();

        public RrSceneData Data;
        public RrSceneKitResult Kit;
        public RrSceneKitTerrain.Result Terrain;
        public RrJumbotronHull.Stats Hull;
        public Transform Root;

        public readonly List<RrSceneData.MarkerRecord> Pots = new List<RrSceneData.MarkerRecord>();
        public readonly List<RrSceneData.MarkerRecord> Weapons = new List<RrSceneData.MarkerRecord>();

        /// <summary>The sci-fi doors' leaves (scene pose; mesh name), built by <see cref="BuildLeaves"/>.</summary>
        private readonly List<Leaf> _leaves = new List<Leaf>();

        private List<RrSceneKitGrid> _grids;
        private int _gridsDropped, _standInsDropped, _collidersDropped, _outside;

        private struct Leaf
        {
            public string Mesh;
            public string Material;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        /// <summary>A built door leaf: its object and where it slides to when open (local to its parent).</summary>
        public sealed class DoorLeaf
        {
            public Transform T;
            public Vector3 Closed;
            public Vector3 Open;
            public Vector3 ScenePivot;
        }

        public readonly List<DoorLeaf> Leaves = new List<DoorLeaf>();

        public RrJumbotronArea(string scene, Vector3 anchor)
        {
            Scene = scene;
            Anchor = anchor;
        }

        /// <summary>Scene point to room-local.</summary>
        public Vector3 L(float x, float y, float z)
        {
            return new Vector3(x - Anchor.x, y - Anchor.y, z - Anchor.z);
        }

        public Vector3 L(Vector3 scene)
        {
            return scene - Anchor;
        }

        // ------------------------------------------------------------------ build

        /// <summary>Parses (worker), cleans up, builds the kit, the terrain and the door leaves under root.</summary>
        public IEnumerator Build(string raw, Transform root, Dictionary<string, Material> surfaces)
        {
            Root = root;
            RrSceneData data = null;
            List<RrSceneKitGrid> grids = null;
            string error = null;
            RrJumbotronHull.Data hullData = null;
            var cut = Filter;
            var drops = new List<Bounds>(DropBoxes);
            var worker = RrMain.Instance != null ? RrMain.Instance.Worker : null;
            if (worker != null)
            {
                bool finished = false;
                worker.Post(() =>
                {
                    string plain = RrSceneKitData.Decode(raw);
                    var parsed = RrSceneData.Parse(plain);
                    var hull = parsed != null ? RrJumbotronHull.Extract(parsed, cut, drops) : null;
                    return new object[] { parsed, RrSceneKitTerrain.Parse(plain), hull };
                }, (res, e) =>
                {
                    var arr = res as object[];
                    if (arr != null)
                    {
                        data = arr[0] as RrSceneData;
                        grids = arr[1] as List<RrSceneKitGrid>;
                        hullData = arr[2] as RrJumbotronHull.Data;
                    }

                    if (e != null)
                    {
                        error = e.GetType().Name + ": " + e.Message;
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
                    string plain = RrSceneKitData.Decode(raw);
                    data = RrSceneData.Parse(plain);
                    grids = RrSceneKitTerrain.Parse(plain);
                    hullData = RrJumbotronHull.Extract(data, cut, drops);
                }
                catch (Exception e)
                {
                    error = e.GetType().Name + ": " + e.Message;
                }
            }

            if (data == null || root == null)
            {
                RrLog.Warn("Jumbotron", Scene + ": the scene data did not parse (" + (error ?? "no data") + ")");
                yield break;
            }

            Data = data;
            _grids = grids ?? new List<RrSceneKitGrid>();
            Clean(data);
            yield return null;

            // Install surfaces the height grids ask for (a named install material, or our pick for a '?name').
            foreach (var g in _grids)
            {
                string m = SurfaceFor(g);
                if (m != null && !surfaces.ContainsKey(m))
                {
                    yield return LoadSurface(m, surfaces);
                }
            }

            var options = new RrSceneKitOptions
            {
                Position = -Anchor, RootName = "Rr Scene " + Scene, SkipNames = new HashSet<string>(Skip), MaxAudio = 8, Filter = Filter, LoadTimeout = 50f,
                // Every light the level has (10-74; RrEnvironment gives them Rec Room's shadows), merged cells of 12 m so URP's
                // four lights per object are the ones near it (the goldentrophy agent's measured choice for the same levels).
                MaxLights = 64, CellSize = 12f
            };
            yield return RrSceneKit.Build(data, root, options, r => Kit = r);
            if (root == null)
            {
                yield break;
            }

            if (GridToCore)
            {
                CutGridToCore();
            }

            var topt = new RrSceneKitTerrain.Options
            {
                Position = -Anchor, RootName = "Rr Floors " + Scene, Tile = 3f, TileFor = TileFor, CutSteep = g => g.Source == "mesh",
                ClipRadius = Filter.HasValue ? Mathf.Max(Filter.Value.extents.x, Filter.Value.extents.z) : 0f,
                ClipCenter = Filter.HasValue ? Filter.Value.center : Vector3.zero
            };
            yield return RrSceneKitTerrain.Build(_grids, root, topt, g => GroundMaterial(g, surfaces), r => Terrain = r);
            var owned = root.GetComponentInParent<RrJumbotronHull.Owned>();
            if (owned == null)
            {
                owned = root.gameObject.AddComponent<RrJumbotronHull.Owned>();
            }

            yield return RrJumbotronHull.Build(hullData, root, -Anchor, owned, Scene, s => Hull = s);
            // Iteration 10: the level's mist volumes and portal glows (not surfaces, so the hull leaves them out).
            yield return RrJumbotronHaze.Build(hullData, root, -Anchor, owned, Scene);
            yield return BuildLeaves(root);
            RegisterWholeRoom(root);
            RrLog.Info("Jumbotron", Scene + ": built at room-local offset " + (-Anchor).ToString("F1") + " - kit " + (Kit != null ? Kit.Renderers + " renderers, " + Kit.StandIns + " stand-ins, "
                + (Kit.BoxColliders + Kit.MeshColliders + Kit.OtherColliders) + " colliders, " + Kit.Lights + " lights, " + Kit.Markers.Count + " markers" : "FAILED")
                + "; " + (Hull != null ? Hull.ToString() : "no hull") + "; floors " + (Terrain != null ? Terrain.Summary() : "none") + "; door leaves " + Leaves.Count + "; pots " + Pots.Count + ", weapons " + Weapons.Count
                + "; dropped: grids " + _gridsDropped + ", stand-ins " + _standInsDropped + ", colliders " + _collidersDropped + ", outside the cut " + _outside);
        }

        // ------------------------------------------------------------------ lighting (iteration 10)

        /// <summary>
        /// <see cref="GridToCore"/>: the kit's grid box (in the kit root's space, which is scene space) is cut to the scene's
        /// registered box. RrEnvironment registers that box at the end of the kit build: the placed records plus 3 m. The
        /// grid is only built once travel is done, so the cut is in place before the build. A pixel outside the box
        /// takes the nearest edge cell (RrLightGrid).
        /// </summary>
        private void CutGridToCore()
        {
            var grid = Kit != null ? Kit.Grid : null;
            Bounds world;
            if (grid == null || Kit.Root == null || !RrEnvironment.SceneWorldBounds(Scene, out world))
            {
                RrLog.Info("Jumbotron", Scene + ": no light grid or registered scene box, the grid box stays as the kit made it");
                return;
            }

            var core = ToLocal(Kit.Root.transform, world);
            var was = grid.LocalVolume;
            var lo = Vector3.Max(was.min, core.min);
            var hi = Vector3.Min(was.max, core.max);
            if (hi.x <= lo.x || hi.y <= lo.y || hi.z <= lo.z)
            {
                RrLog.Warn("Jumbotron", Scene + ": the kit core " + core.size.ToString("F0") + " misses the grid box " + was.size.ToString("F0") + ", left as it is");
                return;
            }

            var cut = new Bounds();
            cut.SetMinMax(lo, hi);
            grid.LocalVolume = cut;
            RrLog.Info("Jumbotron", Scene + ": light grid box cut to the kit core, " + was.size.ToString("F0") + " -> " + cut.size.ToString("F0") + " m (scene centre "
                + cut.center.ToString("F0") + ")" + (grid.Built ? "; the grid was already built, so the cut applies from its next build" : ""));
        }

        /// <summary>
        /// Registers the room with RrEnvironment again, with the box of everything built under
        /// <paramref name="root"/> (kit, floors, hull, haze, door leaves) and the team's arrival. The kit alone registers
        /// the placed records' pivots, and the audit found the arrivals of acts 1, 2, 5 and 8 up to 5 m outside that box.
        /// The quest names its area anyway (RrJumbotronRoom.ShowLighting); this box keeps the automatic pick right as
        /// well (harness, or any moment without a named area).
        /// </summary>
        private void RegisterWholeRoom(Transform root)
        {
            if (root == null || Kit == null || Kit.Root == null || Data == null)
            {
                return;
            }

            var host = Kit.Root.GetComponent<RrSceneKitRoot>();
            Bounds world;
            if (host == null || !RrEnvironment.SceneWorldBounds(Scene, out world))
            {
                return;
            }

            var kitRoot = Kit.Root.transform;
            var box = ToLocal(kitRoot, world);
            var was = box.size;
            // SceneBuilt adds its margin again.
            box.Expand(-2f * RrEnvironment.AreaMargin);
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer)
                {
                    continue;
                }

                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-6f || b.size.x > 2000f || b.size.z > 2000f)
                {
                    continue;
                }

                box.Encapsulate(ToLocal(kitRoot, b));
                n++;
            }

            Vector3 at;
            float yaw;
            if (Arrival(out at, out yaw))
            {
                box.Encapsulate(kitRoot.InverseTransformPoint(root.TransformPoint(at)));
            }

            RrEnvironment.SceneBuilt(host, Data, box);
            box.Expand(2f * RrEnvironment.AreaMargin);
            RrLog.Info("Jumbotron", Scene + ": lighting area " + was.ToString("F0") + " -> " + box.size.ToString("F0") + " m (" + n + " renderers and the arrival)");
        }

        /// <summary>A world box as an axis-aligned box in <paramref name="t"/>'s local space (its 8 corners).</summary>
        private static Bounds ToLocal(Transform t, Bounds world)
        {
            var c = world.center;
            var e = world.extents;
            var local = new Bounds(t.InverseTransformPoint(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                local.Encapsulate(t.InverseTransformPoint(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
            }

            return local;
        }

        /// <summary>
        /// The level's own materials for its floors: named install materials as they are; for the level-only ones
        /// ('?name') the closest install surface by kind (metal tread, concrete, the level's dark paint).
        /// </summary>
        private static string SurfaceFor(RrSceneKitGrid g)
        {
            if (!g.Visible || g.Far && g.Collider)
            {
                return null;
            }

            string m = g.Material ?? "";
            if (m.Length > 0 && !m.StartsWith("?"))
            {
                return m;
            }

            string k = m.ToLowerInvariant();
            if (k.Contains("metal") || k.Contains("tread") || k.Contains("corrugated") || k.Contains("stage"))
            {
                return "Metal_FloorTread_Shiny_Mat";
            }

            if (k.Contains("concrete") || k.Contains("garage"))
            {
                return "Concrete_LightGrey_2x_Mat";
            }

            return null;
        }

        private static float TileFor(RrSceneKitGrid g)
        {
            string m = (g.Material ?? "").ToLowerInvariant();
            return m.Contains("tread") || m.Contains("metal") ? 2f : m.Contains("concrete") ? 4f : 3f;
        }

        private static Material GroundMaterial(RrSceneKitGrid g, Dictionary<string, Material> surfaces)
        {
            string name = SurfaceFor(g);
            Material m;
            if (name != null && surfaces.TryGetValue(name, out m) && m != null)
            {
                return m;
            }

            string k = (g.Material ?? "").ToLowerInvariant();
            // Rec Room's level-only floor colours (the stand-in colours of the same materials in the level).
            if (k.Contains("linoleum"))
            {
                return RrLook.Solid(new Color(0.36f, 0.3f, 0.27f));
            }

            if (k.Contains("wall_vertcolor"))
            {
                return RrLook.Solid(new Color(0.42f, 0.44f, 0.5f));
            }

            return RrLook.Solid(new Color(0.3f, 0.31f, 0.34f));
        }

        private static IEnumerator LoadSurface(string name, Dictionary<string, Material> surfaces)
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                surfaces[name] = null;
                yield break;
            }

            RrEntry e;
            if (!assets.TryFind(name, 21, out e))
            {
                surfaces[name] = null;
                yield break;
            }

            Material m = null;
            yield return assets.LoadRoutine<Material>(name, 21, x => m = x);
            surfaces[name] = m != null ? RrMaterials.Fix(m) : null;
        }

        // ------------------------------------------------------------------ clean-up

        private bool Dropped(Vector3 scene)
        {
            foreach (var b in DropBoxes)
            {
                if (b.Contains(scene))
                {
                    return true;
                }
            }

            return false;
        }

        private void Clean(RrSceneData d)
        {
            // Door leaves: out of the merge, kept for our own animated objects.
            var leafMeshes = new HashSet<int>();
            for (int i = 0; i < d.Meshes.Count; i++)
            {
                if (d.Meshes[i] == "door_L" || d.Meshes[i] == "door_R")
                {
                    leafMeshes.Add(i);
                }
            }

            for (int i = d.Renderers.Count - 1; i >= 0; i--)
            {
                var r = d.Renderers[i];
                if (!leafMeshes.Contains(r.Mesh))
                {
                    continue;
                }

                string mat = r.MaterialCount > 0 && r.MaterialStart < d.MaterialIndices.Count ? d.Materials[d.MaterialIndices[r.MaterialStart]] : "Door_SciFi_Cardboard_mat";
                if (!Filter.HasValue || Filter.Value.Contains(r.Position))
                {
                    _leaves.Add(new Leaf { Mesh = d.Meshes[r.Mesh], Material = mat, Position = r.Position, Rotation = r.Rotation, Scale = r.Scale });
                }

                d.Renderers.RemoveAt(i);
            }

            // Pots and weapons become the quest's breakables and racks (the kit builds no G record anyway).
            foreach (var m in d.Markers)
            {
                if (m.Kind != "weapon_pickup")
                {
                    continue;
                }

                string prefab = Value(m, "prefab") ?? m.Name;
                if (prefab == "(SciFatPot)" || prefab == "(FatPot)")
                {
                    Pots.Add(m);
                }
                else if (prefab.StartsWith("(Quest_SciFi_"))
                {
                    Weapons.Add(m);
                }
            }

            if (DropBoxes.Count > 0)
            {
                _standInsDropped += d.StandIns.RemoveAll(x => Dropped(x.Center));
                _collidersDropped += d.Colliders.RemoveAll(c => c.Shape == RrSceneData.ColliderShape.Box && Dropped(c.Center));
            }

            if (Filter.HasValue)
            {
                var f = Filter.Value;
                _outside += d.StandIns.RemoveAll(x => !f.Contains(x.Center));
                _outside += d.Colliders.RemoveAll(c => !f.Contains(c.Center));
            }

            // Height grids that are not floors: area fog, portal glow, the invisible nav walls.
            _gridsDropped = _grids.RemoveAll(g =>
            {
                string m = (g.Material ?? "") + " " + g.Name;
                return m.Contains("AreaFog") || m.Contains("Portal_") || m.Contains("InvisibleWall") || m.Contains("LightShaft");
            });
        }

        private static string Value(RrSceneData.MarkerRecord m, string key)
        {
            if (m.Keys == null)
            {
                return null;
            }

            for (int i = 0; i < m.Keys.Length; i++)
            {
                if (m.Keys[i] == key)
                {
                    return m.Values[i];
                }
            }

            return null;
        }

        /// <summary>Every sci-fi door's two leaves as their own objects (install meshes, Rec Room's material).</summary>
        private IEnumerator BuildLeaves(Transform root)
        {
            if (_leaves.Count == 0 || RrAssets.Instance == null)
            {
                yield break;
            }

            var group = RrBuild.Group(root, "Door Leaves " + Scene).transform;
            var meshes = new Dictionary<string, Mesh>();
            var mats = new Dictionary<string, Material>();
            foreach (var l in _leaves)
            {
                if (!meshes.ContainsKey(l.Mesh))
                {
                    Mesh mesh = null;
                    yield return RrAssets.Instance.LoadRoutine<Mesh>(l.Mesh, 43, x => mesh = x);
                    meshes[l.Mesh] = mesh;
                }

                if (!mats.ContainsKey(l.Material))
                {
                    Material mat = null;
                    yield return RrAssets.Instance.LoadRoutine<Material>(l.Material, 21, x => mat = x);
                    mats[l.Material] = mat != null ? RrMaterials.Fix(mat) : RrLook.Solid(new Color(0.55f, 0.56f, 0.6f));
                }
            }

            if (root == null)
            {
                yield break;
            }

            foreach (var l in _leaves)
            {
                Mesh mesh;
                if (!meshes.TryGetValue(l.Mesh, out mesh) || mesh == null)
                {
                    continue;
                }

                var go = new GameObject(l.Mesh);
                go.layer = RrLayers.Environment;
                go.transform.SetParent(group, false);
                go.transform.localPosition = L(l.Position);
                go.transform.localRotation = l.Rotation;
                go.transform.localScale = l.Scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mats[l.Material];
                // A leaf slides sideways by its own width, away from the doorway's centre (door_L to its left).
                Vector3 c = mesh.bounds.center;
                Vector3 side = new Vector3(Mathf.Abs(c.x) > 0.05f ? Mathf.Sign(c.x) : (l.Mesh == "door_L" ? -1f : 1f), 0f, 0f);
                Vector3 slide = l.Rotation * Vector3.Scale(side * Mathf.Max(1f, mesh.bounds.size.x * 0.95f), l.Scale);
                Leaves.Add(new DoorLeaf { T = go.transform, Closed = go.transform.localPosition, Open = go.transform.localPosition + slide, ScenePivot = l.Position });
            }
        }

        // ------------------------------------------------------------------ markers

        public List<RrSceneMarker> Markers(string kind, string prefix = null)
        {
            var list = new List<RrSceneMarker>();
            if (Kit == null)
            {
                return list;
            }

            foreach (var m in Kit.Markers)
            {
                if (m.Kind == kind && (prefix == null || m.Name.StartsWith(prefix)))
                {
                    list.Add(m);
                }
            }

            return list;
        }

        /// <summary>The team's arrival: the centre of the level's spawn points (Player7SpawnPoint 1..4 in the acts) and their facing.</summary>
        public bool Arrival(out Vector3 at, out float yaw)
        {
            at = Vector3.zero;
            yaw = 0f;
            var spawns = Markers("spawn", "Player7SpawnPoint");
            if (spawns.Count == 0)
            {
                spawns = Markers("spawn");
            }

            if (spawns.Count == 0)
            {
                return false;
            }

            foreach (var s in spawns)
            {
                at += s.Position;
            }

            at /= spawns.Count;
            yaw = spawns[0].Rotation.eulerAngles.y;
            return true;
        }

        /// <summary>Each spawn point's pose (room-local), for the lobby's arrivals.</summary>
        public List<Pose> SpawnPoses()
        {
            var list = new List<Pose>();
            foreach (var s in Markers("spawn"))
            {
                list.Add(new Pose(s.Position + Vector3.up * 0.05f, Quaternion.Euler(0f, s.Rotation.eulerAngles.y, 0f)));
            }

            return list;
        }

        public RrSceneMarker ExitMarker()
        {
            var list = Markers("door", "[Quest_SciFi_RoomExitDoor]");
            return list.Count > 0 ? list[0] : null;
        }

        public RrSceneMarker DoorTrigger()
        {
            var list = Markers("door_trigger");
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>An encounter's trigger volume (room-local centre and size), when the level has one.</summary>
        public bool Zone(string encounter, out Bounds zone, int index = 0)
        {
            zone = default(Bounds);
            int n = 0;
            foreach (var m in Markers("enc_trigger"))
            {
                if (m.Name != encounter)
                {
                    continue;
                }

                if (n++ < index)
                {
                    continue;
                }

                zone = new Bounds(m.Position, Size(m, new Vector3(8f, 6f, 8f)));
                return true;
            }

            return false;
        }

        private static Vector3 Size(RrSceneMarker m, Vector3 fallback)
        {
            string s = m.Get("size");
            if (string.IsNullOrEmpty(s))
            {
                return fallback;
            }

            var p = s.Split(',');
            float x, y, z;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (p.Length == 3 && float.TryParse(p[0], System.Globalization.NumberStyles.Float, inv, out x) && float.TryParse(p[1], System.Globalization.NumberStyles.Float, inv, out y)
                && float.TryParse(p[2], System.Globalization.NumberStyles.Float, inv, out z))
            {
                return new Vector3(x, y, z);
            }

            return fallback;
        }

        /// <summary>
        /// Spawn points of an encounter (room-local): its enemy spawn points and 3 points along each spawn region (the
        /// region's centre and +-30 % along its long side). group filters by the point's group path (substring, null: all).
        /// </summary>
        public List<Vector3> Points(string encounter, string group = null)
        {
            var list = new List<Vector3>();
            foreach (var m in Kit != null ? Kit.Markers : new List<RrSceneMarker>())
            {
                if ((m.Kind != "enemy_spawner" && m.Kind != "enemy_region") || m.Get("enc") != encounter)
                {
                    continue;
                }

                string grp = (m.Get("grp") ?? "") + "/" + m.Name;
                if (group != null && grp.IndexOf(group, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (m.Kind == "enemy_spawner")
                {
                    list.Add(m.Position);
                    continue;
                }

                var size = Size(m, Vector3.one);
                Vector3 along = size.x >= size.z ? m.Rotation * Vector3.right * size.x : m.Rotation * Vector3.forward * size.z;
                list.Add(m.Position);
                if (along.magnitude > 1.5f)
                {
                    list.Add(m.Position + along * 0.3f);
                    list.Add(m.Position - along * 0.3f);
                }
            }

            return list;
        }

        /// <summary>A marker of a kind by name (room-local pose), or null.</summary>
        public RrSceneMarker Marker(string kind, string name)
        {
            foreach (var m in Markers(kind))
            {
                if (m.Name == name)
                {
                    return m;
                }
            }

            return null;
        }

        /// <summary>The room's bounds (room-local) from the kit, cut to the filter.</summary>
        public Bounds Area()
        {
            Bounds b;
            if (Kit != null && Kit.Bounds.size.sqrMagnitude > 1f)
            {
                b = Kit.Bounds;
            }
            else
            {
                b = new Bounds(Vector3.zero, new Vector3(60f, 20f, 60f));
            }

            if (Filter.HasValue)
            {
                var f = Filter.Value;
                var lf = new Bounds(f.center - Anchor, f.size);
                var min = Vector3.Max(b.min, lf.min);
                var max = Vector3.Min(b.max, lf.max);
                if (max.x > min.x && max.z > min.z)
                {
                    b.SetMinMax(min, max);
                }
            }

            return b;
        }
    }
}
