using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// What the Rec Center's big windows look out on (the July 2025 scene's Terrain, Trees and Props/Fence, room frame):
    /// a lawn, the red-brick fence round the lot (27 x `[RecCenter_fence]`, 3.2 m tall, at x 22.21, z 30.97, x -35.63
    /// and z -9.99), the bushes along the curved windows and the cafe bay, oaks and golden aspens a little further
    /// and pines behind them, under a gradient sky dome. Low-poly Rec Room style like the Dorm's outdoors (lollipop
    /// trees of spheres), a few hundred triangles, no colliders and no shadows, all under the room so it goes with it.
    /// The lawn leaves out the theatre's footprint: the theatre floor is 0.79 m below the lawn's height.
    /// </summary>
    internal static class RrRecCenterOutdoors
    {
        public const float LawnY = -0.05f;
        public const float SkyRadius = 150f;

        // Measured tree spots (Trees/Oaks, Trees/Trees, Trees/Bushes; x, z).
        private static readonly Vector2[] Oaks = { new Vector2(-35.38f, -10.92f), new Vector2(-41.33f, 23.88f), new Vector2(28.55f, 24.38f), new Vector2(-29.35f, 29.39f), new Vector2(-44.93f, 0.58f), new Vector2(40.07f, 0.12f), new Vector2(5.28f, 38.48f) };
        private static readonly Vector2[] Aspens = { new Vector2(12.93f, 32.67f), new Vector2(-20.15f, 34.18f), new Vector2(-38.92f, 41.84f), new Vector2(-38.66f, 16.02f), new Vector2(-41.12f, 10.48f), new Vector2(-28.91f, -20.60f), new Vector2(23.97f, 15.33f) };
        private static readonly Vector2[] Pines =
        {
            new Vector2(-52.06f, 16.55f), new Vector2(-58.63f, 2.24f), new Vector2(-49.82f, -12.55f), new Vector2(-44.50f, -22.06f), new Vector2(-32.75f, -27.74f),
            new Vector2(-59.63f, 32.87f), new Vector2(-46.92f, 38.82f), new Vector2(-42.20f, 53.24f), new Vector2(-28.90f, 48.06f), new Vector2(-19.35f, 43.71f),
            new Vector2(-13.41f, 49.03f), new Vector2(-1.97f, 49.90f), new Vector2(19.99f, 38.93f), new Vector2(32.53f, 36.06f), new Vector2(41.97f, 27.81f),
            new Vector2(39.12f, 14.92f), new Vector2(28.75f, 8.72f), new Vector2(28.11f, -0.29f), new Vector2(-21.33f, 71.37f), new Vector2(-2.36f, 71.96f),
            new Vector2(16.06f, 60.90f), new Vector2(31.04f, 54.89f)
        };

        private static readonly Vector2[] Bushes =
        {
            new Vector2(13.78f, 10.11f), new Vector2(12.48f, 12.56f), new Vector2(10.53f, 14.91f), new Vector2(8.25f, 16.99f), new Vector2(5.84f, 18.34f),
            new Vector2(3.26f, 19.32f), new Vector2(0.56f, 19.96f), new Vector2(-26.49f, -2.70f), new Vector2(-24.29f, 2.01f), new Vector2(-21.35f, 5.04f),
            new Vector2(-19.16f, 6.01f), new Vector2(-33.31f, 8.75f), new Vector2(-33.47f, 14.00f), new Vector2(-31.97f, 26.93f), new Vector2(-21.49f, 30.22f),
            new Vector2(-12.31f, 29.84f), new Vector2(-9.74f, 29.21f), new Vector2(-5.27f, 29.09f), new Vector2(-0.59f, 29.65f), new Vector2(7.12f, 28.74f),
            new Vector2(19.44f, 28.20f), new Vector2(19.43f, 9.76f), new Vector2(15.76f, 5.79f)
        };

        public static GameObject Build(Transform room, RrRcSurfaces s)
        {
            var root = RrBuild.Group(room, "Outdoors");
            var t = root.transform;

            var sky = RrBuild.Primitive(PrimitiveType.Sphere, t, "Sky", new Vector3(0f, -10f, 0f), Quaternion.identity, Vector3.one * SkyRadius * 2f, SkyMaterial(), false, RrLayers.Default);
            Quiet(sky);

            // Lawn round the theatre's footprint (x 11.5..22.0, z -10.7..2.2).
            var grass = RrLook.Solid(new Color(0.44f, 0.66f, 0.31f));
            Lawn(t, grass, -160f, 11.5f, -160f, 160f);
            Lawn(t, grass, 22.0f, 160f, -160f, 160f);
            Lawn(t, grass, 11.5f, 22.0f, -160f, -10.7f);
            Lawn(t, grass, 11.5f, 22.0f, 2.2f, 160f);
            var path = RrLook.Solid(new Color(0.86f, 0.80f, 0.66f));
            Quiet(RrBuild.Box(t, "Path", new Vector3(24f, LawnY + 0.02f, 18f), new Vector3(3f, 0.02f, 26f), path, false));

            // The brick fence (3.2 m) round the lot, as three long runs.
            if (s != null)
            {
                Quiet(RrRcGeo.Slab(t, "Fence", new Vector3(22.0f, LawnY, -12f), new Vector3(22.4f, 3.2f, 31.2f), s.Brick, false));
                Quiet(RrRcGeo.Slab(t, "Fence", new Vector3(-35.8f, LawnY, 30.8f), new Vector3(22.4f, 3.2f, 31.2f), s.Brick, false));
                Quiet(RrRcGeo.Slab(t, "Fence", new Vector3(-35.8f, LawnY, -10.2f), new Vector3(-35.4f, 3.2f, 31.2f), s.Brick, false));
                Quiet(RrRcGeo.Slab(t, "Fence", new Vector3(-35.8f, LawnY, -10.2f), new Vector3(-27.5f, 3.2f, -9.8f), s.Brick, false));
            }

            var trunk = RrLook.Solid(new Color(0.45f, 0.30f, 0.19f));
            var greens = new[] { RrLook.Solid(new Color(0.33f, 0.60f, 0.27f)), RrLook.Solid(new Color(0.40f, 0.68f, 0.30f)), RrLook.Solid(new Color(0.28f, 0.52f, 0.28f)) };
            var gold = new[] { RrLook.Solid(new Color(0.86f, 0.70f, 0.24f)), RrLook.Solid(new Color(0.93f, 0.62f, 0.20f)) };
            var pine = RrLook.Solid(new Color(0.20f, 0.42f, 0.28f));
            for (int i = 0; i < Oaks.Length; i++)
            {
                Tree(t, Oaks[i], 1.3f, trunk, i % 3 == 0 ? gold[0] : greens[i % greens.Length]);
            }

            for (int i = 0; i < Aspens.Length; i++)
            {
                Tree(t, Aspens[i], 1.0f, trunk, gold[i % gold.Length]);
            }

            for (int i = 0; i < Pines.Length; i++)
            {
                var p = new Vector3(Pines[i].x, LawnY, Pines[i].y);
                Quiet(RrBuild.Cylinder(t, "Pine Trunk", p + Vector3.up * 1.5f, 0.35f, 3f, trunk, false));
                Quiet(RrBuild.Primitive(PrimitiveType.Sphere, t, "Pine", p + Vector3.up * 7.5f, Quaternion.identity, new Vector3(5f, 12f, 5f), pine, false, RrLayers.Default));
            }

            for (int i = 0; i < Bushes.Length; i++)
            {
                var p = new Vector3(Bushes[i].x, LawnY, Bushes[i].y);
                Quiet(RrBuild.Primitive(PrimitiveType.Sphere, t, "Bush", p + Vector3.up * 0.7f, Quaternion.identity, new Vector3(2.2f, 1.6f, 2.0f), greens[i % greens.Length], false, RrLayers.Default));
            }

            // Puffy clouds seen through the skylights.
            var cloud = RrLook.Unlit(Color.white);
            var rng = new System.Random(2507);
            for (int i = 0; i < 10; i++)
            {
                float a = (i / 10f) * Mathf.PI * 2f + 0.4f;
                float d = 35f + (float)rng.NextDouble() * 60f;
                var c = new Vector3(Mathf.Cos(a) * d, 45f + (float)rng.NextDouble() * 25f, Mathf.Sin(a) * d);
                for (int k = 0; k < 3; k++)
                {
                    Quiet(RrBuild.Primitive(PrimitiveType.Sphere, t, "Cloud", c + new Vector3((k - 1) * 5f, k == 1 ? 1.5f : 0f, 0f), Quaternion.identity,
                        new Vector3(9f, 4.5f, 7f) * (k == 1 ? 1.2f : 0.9f), cloud, false, RrLayers.Default));
                }
            }

            return root;
        }

        private static void Lawn(Transform t, Material m, float x0, float x1, float z0, float z1)
        {
            Quiet(RrBuild.Box(t, "Lawn", new Vector3((x0 + x1) * 0.5f, LawnY - 0.1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.2f, z1 - z0), m, false));
        }

        private static void Tree(Transform t, Vector2 at, float scale, Material trunk, Material leaves)
        {
            var p = new Vector3(at.x, LawnY, at.y);
            Quiet(RrBuild.Cylinder(t, "Trunk", p + Vector3.up * 2.8f * scale, 0.35f * scale, 5.6f * scale, trunk, false));
            Quiet(RrBuild.Sphere(t, "Leaves", p + Vector3.up * 7.0f * scale, 3.0f * scale, leaves, false));
            Quiet(RrBuild.Sphere(t, "Leaves", p + new Vector3(1.2f, 8.6f, 0.5f) * scale, 2.1f * scale, leaves, false));
            Quiet(RrBuild.Sphere(t, "Leaves", p + new Vector3(-1.0f, 8.2f, -0.8f) * scale, 1.9f * scale, leaves, false));
        }

        private static Material SkyMaterial()
        {
            var m = new Material(RrLook.Unlit(Color.white)) { name = "Rec Center Sky" };
            const int h = 64;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { name = "Rec Center Sky Gradient", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[2 * h];
            var ground = new Color(0.60f, 0.72f, 0.58f);
            var horizon = new Color(0.86f, 0.93f, 0.97f);
            var zenith = new Color(0.32f, 0.60f, 0.92f);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                Color c = v < 0.5f ? Color.Lerp(ground, horizon, Mathf.SmoothStep(0.35f, 0.5f, v)) : Color.Lerp(horizon, zenith, Mathf.Pow((v - 0.5f) * 2f, 0.6f));
                px[y * 2] = c;
                px[y * 2 + 1] = c;
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Cull"))
            {
                m.SetFloat("_Cull", 0f);
            }

            RrRcGeo.Own(m);
            RrRcGeo.Own(tex);
            return m;
        }

        private static void Quiet(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }
}
