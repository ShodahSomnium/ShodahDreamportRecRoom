using System;
using System.Collections;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// Building blocks for quest levels (rr_quests.md 4.0): Rec Room's Maker kit props placed on the ground
    /// (<see cref="Kit"/>: stone walls and pillars, cardboard trees, rocks, campfires, barrels - spawned as scenery on
    /// the Environment layer), and procedural pieces in Rec Room's maker-pen look (<see cref="RrLook.Solid"/>):
    /// floors, castle walls with merlons, towers, torches, banners, lava pools, chests. Everything is act-local and
    /// walkable where it should be (layer 23).
    ///
    /// Palette (GUESS, matched by eye to Golden Trophy's cardboard-castle look): warm grey stone, darker trim stone,
    /// meadow green, packed-dirt brown, Rec Room wood tones, hedge green, banner red, trophy gold.
    /// </summary>
    public static class RrQuestKit
    {
        public static Material Stone, StoneDark, StoneLight, Grass, Dirt, Wood, WoodDark, Hedge, Banner, Gold, Lava, LavaGlow, Water;

        /// <summary>Kit spawns still loading (a build waits for 0 before it reports done).</summary>
        public static int Pending { get; private set; }

        public static int Spawned { get; private set; }

        /// <summary>Makes the palette and tries Rec Room's lava material (bundle 18c123d6 key 17, "Water_Lava_Mat").</summary>
        public static IEnumerator Prepare()
        {
            Stone = RrLook.Solid(new Color(0.60f, 0.60f, 0.62f));
            StoneDark = RrLook.Solid(new Color(0.43f, 0.43f, 0.47f));
            StoneLight = RrLook.Solid(new Color(0.74f, 0.72f, 0.70f));
            Grass = RrLook.Solid(new Color(0.42f, 0.64f, 0.30f));
            Dirt = RrLook.Solid(new Color(0.60f, 0.46f, 0.31f));
            Wood = RrLook.Solid(RrLook.Wood);
            WoodDark = RrLook.Solid(RrLook.DarkWood);
            Hedge = RrLook.Solid(new Color(0.27f, 0.50f, 0.25f));
            Banner = RrLook.Solid(new Color(0.78f, 0.16f, 0.16f));
            Gold = RrLook.Solid(new Color(0.95f, 0.74f, 0.20f));
            LavaGlow = RrLook.Emissive(new Color(1f, 0.3f, 0.04f), 1.1f);
            LavaCrust = RrLook.Solid(new Color(0.25f, 0.12f, 0.08f));
            Water = RrLook.Solid(new Color(0.25f, 0.5f, 0.78f));
            // Rec Room's Water_Lava_Mat (AG/TerrainWater) loads but draws nothing on a plain box (harness 2026-09-24),
            // so lava is our own glowing surface with dark crust plates.
            Lava = LavaGlow;
            var assets = RrAssets.Instance;
            if (assets != null)
            {
                if (StoneTex == null)
                {
                    yield return assets.LoadRoutine<Material>("Wall_Stone_Section_Theater_mat", 21, x => StoneTex = x != null ? RrMaterials.Fix(x) : null);
                }

                if (FloorTex == null)
                {
                    yield return assets.LoadRoutine<Material>("Stones_Modular_Theater_Mat", 21, x => FloorTex = x != null ? RrMaterials.Fix(x) : null);
                }

                if (WoodTex == null)
                {
                    yield return assets.LoadRoutine<Material>("Wood_Floor_Matte_mat", 21, x => WoodTex = x != null ? RrMaterials.Fix(x) : null);
                }
            }

            RrLog.Info("Quest", "kit ready: stone " + (StoneTex != null ? StoneTex.shader.name : "-") + ", floor " + (FloorTex != null ? FloorTex.name : "-")
                + ", wood " + (WoodTex != null ? WoodTex.name : "-"));
        }

        /// <summary>Rec Room's own surface materials (rr_quests.md 4.0), tiled onto our boxes by <see cref="Texture"/>.</summary>
        public static Material StoneTex, FloorTex, WoodTex, LavaCrust;

        /// <summary>
        /// Swaps our flat stone / wood colours on every procedural box under root for Rec Room's stone and wood
        /// materials, tiled by the box's size (one tile per 2.5 m of stone, 2 m of wood; the box's two largest faces
        /// get the right scale). Kit props keep their own materials. Instances are shared per tiling.
        /// </summary>
        public static int Texture(Transform root)
        {
            var cache = new System.Collections.Generic.Dictionary<string, Material>();
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial;
                Material src = null;
                float per = 2.5f;
                float tint = 1f;
                if (m == null)
                {
                    continue;
                }

                // Walls use the flagstone material too: Wall_Stone_Section_Theater_mat is an atlas whose wooden
                // inserts repeat as brown patches when tiled (harness 2026-09-24).
                if (m == Stone) { src = FloorTex; per = 3f; tint = 0.92f; }
                else if (m == StoneDark) { src = FloorTex; per = 3f; tint = 0.68f; }
                else if (m == StoneLight) src = FloorTex;
                else if (m == Wood) { src = WoodTex; per = 2f; }
                var mf = r.GetComponent<MeshFilter>();
                if (src == null || mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube")
                {
                    continue;
                }

                Vector3 s = r.transform.lossyScale;
                s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                Vector2 uv = s.y < Mathf.Min(s.x, s.z) ? new Vector2(s.x, s.z) : s.x >= s.z ? new Vector2(s.x, s.y) : new Vector2(s.z, s.y);
                uv = new Vector2(Mathf.Max(0.5f, Mathf.Round(uv.x / per * 2f) / 2f), Mathf.Max(0.5f, Mathf.Round(uv.y / per * 2f) / 2f));
                string key = src.GetInstanceID() + ":" + tint + ":" + uv.x + ":" + uv.y;
                Material inst;
                if (!cache.TryGetValue(key, out inst))
                {
                    inst = new Material(src) { name = src.name + " (quest " + uv.x + "x" + uv.y + ")" };
                    if (inst.HasProperty("_BaseMap"))
                    {
                        inst.SetTextureScale("_BaseMap", uv);
                    }

                    if (inst.HasProperty("_MainTex"))
                    {
                        inst.SetTextureScale("_MainTex", uv);
                    }

                    if (tint < 1f && inst.HasProperty("_BaseColor"))
                    {
                        inst.SetColor("_BaseColor", inst.GetColor("_BaseColor") * tint);
                    }

                    cache.Add(key, inst);
                }

                r.sharedMaterial = inst;
                n++;
            }

            RrLog.Info("Quest", "textured " + n + " boxes with Rec Room stone/wood (" + cache.Count + " tilings)");
            return n;
        }

        /// <summary>
        /// A Rec Room prefab placed with its lowest point on local y (its pivot is unknown), optionally centred on the
        /// point in x/z. Scenery = Environment layer, kinematic (walls, trees, rocks); solid false = visual only.
        /// </summary>
        public static void Kit(Transform parent, string prefab, Vector3 local, float yaw, float scale = 1f, bool solid = true, bool center = false, Action<RrInstance> done = null)
        {
            if (parent == null)
            {
                return;
            }

            Pending++;
            var options = solid ? RrSpawnOptions.Scenery() : RrSpawnOptions.Visual();
            options.Scale = scale;
            options.StopParticles = false;
            RrSpawn.Spawn(prefab, parent, local, Quaternion.Euler(0f, yaw, 0f), inst =>
            {
                Pending--;
                if (inst == null || parent == null)
                {
                    if (inst != null)
                    {
                        UnityEngine.Object.Destroy(inst.gameObject);
                    }

                    if (done != null)
                    {
                        done(null);
                    }

                    return;
                }

                Spawned++;
                var b = inst.WorldBounds();
                if (b.size.sqrMagnitude > 1e-4f)
                {
                    Vector3 want = parent.TransformPoint(local);
                    Vector3 shift = new Vector3(center ? want.x - b.center.x : 0f, want.y - b.min.y, center ? want.z - b.center.z : 0f);
                    inst.transform.position += shift;
                }

                if (done != null)
                {
                    done(inst);
                }
            }, options);
        }

        /// <summary>Waits until every kit spawn has answered (or the timeout).</summary>
        public static IEnumerator WaitForKit(float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (Pending > 0 && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            if (Pending > 0)
            {
                RrLog.Warn("Quest", Pending + " kit props still loading after " + timeout + " s; they appear when ready");
            }
        }

        /// <summary>A walkable slab whose top is at center.y.</summary>
        public static GameObject Floor(Transform parent, string name, Vector3 center, float sizeX, float sizeZ, Material material, float thickness = 0.4f)
        {
            return RrBuild.Floor(parent, name, center, new Vector2(sizeX, sizeZ), material, thickness);
        }

        /// <summary>A castle wall from a to b (bottom), with merlons on top when crenellated (merlons: no collider).</summary>
        public static GameObject Wall(Transform parent, string name, Vector3 a, Vector3 b, float height, Material material, bool crenellated = false, float thickness = 0.8f, params RrBuild.Hole[] holes)
        {
            var wall = RrBuild.Wall(parent, name, a, b, height, thickness, material, holes);
            if (crenellated)
            {
                Merlons(wall.transform, Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z)), height, thickness, StoneDark);
            }

            return wall;
        }

        private static void Merlons(Transform wall, float length, float height, float thickness, Material material)
        {
            // Wall frame: x along 0..length, y up, z through. Rec Room's battlements: ~0.9 m teeth every 1.8 m.
            int n = Mathf.Max(1, Mathf.FloorToInt(length / 1.8f));
            float step = length / n;
            for (int i = 0; i < n; i++)
            {
                RrBuild.Box(wall, "Merlon " + i, new Vector3(step * (i + 0.5f), height + 0.4f, 0f), new Vector3(step * 0.55f, 0.8f, thickness + 0.1f), material, false);
            }

            RrBuild.Box(wall, "Cap", new Vector3(length * 0.5f, height - 0.1f, 0f), new Vector3(length, 0.2f, thickness + 0.2f), material, false);
        }

        /// <summary>Four walls around an act-local rectangle (x0..x1, z0..z1) at floor y, with optional door holes.</summary>
        public static void Enclose(Transform parent, float x0, float x1, float z0, float z1, float y, float height, Material material, bool crenellated,
            RrBuild.Hole[] north = null, RrBuild.Hole[] south = null, RrBuild.Hole[] east = null, RrBuild.Hole[] west = null)
        {
            Wall(parent, "Wall N", new Vector3(x0, y, z1), new Vector3(x1, y, z1), height, material, crenellated, 0.8f, north ?? new RrBuild.Hole[0]);
            Wall(parent, "Wall S", new Vector3(x1, y, z0), new Vector3(x0, y, z0), height, material, crenellated, 0.8f, south ?? new RrBuild.Hole[0]);
            Wall(parent, "Wall E", new Vector3(x1, y, z1), new Vector3(x1, y, z0), height, material, crenellated, 0.8f, east ?? new RrBuild.Hole[0]);
            Wall(parent, "Wall W", new Vector3(x0, y, z0), new Vector3(x0, y, z1), height, material, crenellated, 0.8f, west ?? new RrBuild.Hole[0]);
        }

        /// <summary>A square tower with a walkable top at y + height and merlons around it.</summary>
        public static GameObject Tower(Transform parent, string name, Vector3 center, float size, float height)
        {
            var root = RrBuild.Group(parent, name, center);
            var t = root.transform;
            RrBuild.Box(t, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(size, height, size), Stone);
            RrBuild.Box(t, "Band", new Vector3(0f, height - 0.15f, 0f), new Vector3(size + 0.3f, 0.3f, size + 0.3f), StoneDark, false);
            float h = size * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                var rot = Quaternion.Euler(0f, a, 0f);
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 p = rot * new Vector3(k * h * 0.55f, height + 0.45f, h - 0.2f);
                    RrBuild.Box(t, "Merlon", p, rot, new Vector3(size * 0.28f, 0.9f, 0.4f), StoneDark, true, RrLayers.Environment);
                }
            }

            return root;
        }

        /// <summary>A wall torch: bracket, flame and a warm point light (range 9 m, no shadows).</summary>
        public static void Torch(Transform parent, Vector3 local, float yaw, bool light = true)
        {
            var root = RrBuild.Group(parent, "Torch", local, Quaternion.Euler(0f, yaw, 0f));
            var t = root.transform;
            RrBuild.Box(t, "Stick", new Vector3(0f, 0f, 0.15f), Quaternion.Euler(-25f, 0f, 0f), new Vector3(0.08f, 0.45f, 0.08f), WoodDark, false, RrLayers.Environment);
            RrBuild.Sphere(t, "Flame", new Vector3(0f, 0.28f, 0.27f), 0.11f, LavaGlow, false);
            if (light)
            {
                RrBuild.PointLight(t, "Light", new Vector3(0f, 0.35f, 0.45f), new Color(1f, 0.62f, 0.3f), 9f, 1.4f);
            }
        }

        /// <summary>A hanging banner (Golden Trophy red with a gold band) on a wall facing +z of yaw.</summary>
        public static void BannerOn(Transform parent, Vector3 local, float yaw, float height = 3f, Material cloth = null)
        {
            var root = RrBuild.Group(parent, "Banner", local, Quaternion.Euler(0f, yaw, 0f));
            var t = root.transform;
            RrBuild.Box(t, "Cloth", new Vector3(0f, -height * 0.5f, 0.05f), new Vector3(1.2f, height, 0.05f), cloth ?? Banner, false);
            RrBuild.Box(t, "Band", new Vector3(0f, -height * 0.72f, 0.08f), new Vector3(1.22f, 0.18f, 0.03f), Gold, false);
            RrBuild.Box(t, "Rod", new Vector3(0f, 0.03f, 0.08f), new Vector3(1.5f, 0.08f, 0.08f), WoodDark, false);
        }

        /// <summary>
        /// A lava pool: the glowing surface at local y (visual), a floor 0.5 m under it (walkable, so a player who falls
        /// in stands in the lava and the room's lava check takes them), and two orange lights. Returns the lava area for
        /// <see cref="RrQuestAct.Lava"/> (xz extent, max.y = surface).
        /// </summary>
        public static Bounds LavaPool(Transform parent, Vector3 center, float sizeX, float sizeZ)
        {
            var surface = RrBuild.Box(parent, "Lava", center + Vector3.down * 0.05f, new Vector3(sizeX, 0.1f, sizeZ), Lava, false);
            surface.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Dark cooling crust plates drifting on the glow (fixed layout from the pool's size).
            var rng = new System.Random(Mathf.RoundToInt(center.x * 13f + center.z * 7f));
            int plates = Mathf.RoundToInt(sizeX * sizeZ / 40f);
            for (int i = 0; i < plates; i++)
            {
                float px = ((float)rng.NextDouble() - 0.5f) * (sizeX - 2f);
                float pz = ((float)rng.NextDouble() - 0.5f) * (sizeZ - 2f);
                float w = 0.8f + 1.6f * (float)rng.NextDouble();
                RrBuild.Box(parent, "Crust", center + new Vector3(px, 0.01f, pz), Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f), new Vector3(w, 0.06f, w * 0.7f),
                    LavaCrust, false, RrLayers.Environment);
            }

            Floor(parent, "Lava Bed", center + Vector3.down * 0.5f, sizeX, sizeZ, StoneDark, 0.3f);
            RrBuild.PointLight(parent, "Lava Light A", center + new Vector3(-sizeX * 0.25f, 1.2f, 0f), new Color(1f, 0.45f, 0.15f), Mathf.Max(sizeX, sizeZ) * 0.6f, 1.2f);
            RrBuild.PointLight(parent, "Lava Light B", center + new Vector3(sizeX * 0.25f, 1.2f, 0f), new Color(1f, 0.45f, 0.15f), Mathf.Max(sizeX, sizeZ) * 0.6f, 1.2f);
            return new Bounds(new Vector3(center.x, center.y - 1f, center.z), new Vector3(sizeX, 2f, sizeZ));
        }

        /// <summary>A treasure chest (the end-of-quest reward: Rec Room's chest prefabs are not in the plain bundles, rr_quests.md 2.4).</summary>
        public static Transform Chest(Transform parent, Vector3 local, float yaw)
        {
            var root = RrBuild.Group(parent, "Chest", local, Quaternion.Euler(0f, yaw, 0f));
            var t = root.transform;
            RrBuild.Box(t, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.6f), Wood);
            RrBuild.Box(t, "Band L", new Vector3(-0.3f, 0.3f, 0f), new Vector3(0.08f, 0.62f, 0.62f), Gold, false);
            RrBuild.Box(t, "Band R", new Vector3(0.3f, 0.3f, 0f), new Vector3(0.08f, 0.62f, 0.62f), Gold, false);
            var lid = RrBuild.Group(t, "Lid", new Vector3(0f, 0.6f, -0.3f)).transform;
            RrBuild.Box(lid, "Top", new Vector3(0f, 0.12f, 0.3f), new Vector3(0.92f, 0.24f, 0.62f), WoodDark, false);
            RrBuild.Box(lid, "Lock", new Vector3(0f, 0.05f, 0.62f), new Vector3(0.14f, 0.18f, 0.05f), Gold, false);
            RrBuild.Box(t, "Coins", new Vector3(0f, 0.58f, 0f), new Vector3(0.8f, 0.06f, 0.5f), Gold, false);
            return root.transform;
        }

        /// <summary>Opens a <see cref="Chest"/>'s lid (0 closed .. 1 open).</summary>
        public static void OpenChest(Transform chest, float k)
        {
            var lid = chest != null ? chest.Find("Lid") : null;
            if (lid != null)
            {
                lid.localRotation = Quaternion.Euler(-100f * Mathf.Clamp01(k), 0f, 0f);
            }
        }
    }
}
