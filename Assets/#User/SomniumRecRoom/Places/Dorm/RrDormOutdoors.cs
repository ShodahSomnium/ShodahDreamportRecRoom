using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm
{
    /// <summary>
    /// What the dorm's windows look out on: the Rec Room campus on a sunny day. The dorm is upstairs (the lawn 3 m
    /// below its floor), so from inside the high windows show mostly sky with tree tops and campus roofs; the skylight
    /// shows sky and clouds. Low-poly Rec Room style: lollipop trees, flat-coloured buildings with coloured roofs, the
    /// Rec Center's orange-trimmed hall towards the big window (+x), puffy clouds, a gradient sky dome. No colliders,
    /// no shadows (the dome must not shade the sun), all under the room so it goes with it. A few hundred triangles.
    /// </summary>
    public static class RrDormOutdoors
    {
        public const float LawnY = -3f;
        public const float SkyRadius = 140f;

        public static GameObject Build(Transform room)
        {
            var root = RrBuild.Group(room, "Outdoors");
            var t = root.transform;
            var rng = new System.Random(1710); // fixed layout

            // Sky dome (URP Unlit, both faces), vertical gradient: horizon haze to Rec Room's saturated blue.
            var sky = RrBuild.Primitive(PrimitiveType.Sphere, t, "Sky", new Vector3(0f, LawnY, 0f), Quaternion.identity,
                Vector3.one * SkyRadius * 2f, SkyMaterial(), false, RrLayers.Default);
            Quiet(sky);

            // Lawn with paths.
            Quiet(RrBuild.Cylinder(t, "Lawn", new Vector3(0f, LawnY - 0.1f, 0f), 120f, 0.2f, RrLook.Solid(new Color(0.45f, 0.70f, 0.32f)), false));
            var path = RrLook.Solid(new Color(0.86f, 0.80f, 0.66f));
            Quiet(RrBuild.Box(t, "Path X", new Vector3(20f, LawnY + 0.01f, 6f), new Vector3(90f, 0.02f, 3f), path, false));
            Quiet(RrBuild.Box(t, "Path Z", new Vector3(14f, LawnY + 0.01f, -10f), new Vector3(3f, 0.02f, 70f), path, false));

            // Campus buildings (cream walls, coloured roofs); the Rec Center out of the big window.
            var cream = RrLook.Solid(new Color(0.95f, 0.91f, 0.82f));
            Building(t, "Rec Center", new Vector3(46f, 0f, 8f), new Vector3(24f, 13f, 30f), cream, RrLook.Solid(RrLook.Orange));
            Building(t, "Hall East", new Vector3(34f, 0f, -34f), new Vector3(18f, 10f, 12f), cream, RrLook.Solid(new Color(0.30f, 0.48f, 0.75f)));
            Building(t, "Hall North", new Vector3(-4f, 0f, 44f), new Vector3(26f, 11f, 12f), cream, RrLook.Solid(new Color(0.76f, 0.30f, 0.25f)));
            Building(t, "Gym", new Vector3(-40f, 0f, 30f), new Vector3(20f, 9f, 22f), cream, RrLook.Solid(new Color(0.36f, 0.62f, 0.60f)));
            Building(t, "Library", new Vector3(-26f, 0f, -42f), new Vector3(22f, 12f, 14f), cream, RrLook.Solid(new Color(0.55f, 0.38f, 0.66f)));

            // Trees: a ring around the dorm, clear of its 10 x 6 m footprint.
            var trunk = RrLook.Solid(new Color(0.45f, 0.30f, 0.19f));
            var leaves = new[]
            {
                RrLook.Solid(new Color(0.33f, 0.62f, 0.27f)), RrLook.Solid(new Color(0.40f, 0.70f, 0.30f)),
                RrLook.Solid(new Color(0.28f, 0.54f, 0.29f))
            };
            for (int i = 0; i < 22; i++)
            {
                float a = (i / 22f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
                float d = 17f + (float)rng.NextDouble() * 26f;
                var p = new Vector3(Mathf.Cos(a) * d, LawnY, Mathf.Sin(a) * d * 0.9f);
                float s = 0.65f + (float)rng.NextDouble() * 0.4f;
                Quiet(RrBuild.Cylinder(t, "Trunk", p + Vector3.up * 2.6f * s, 0.3f * s, 5.2f * s, trunk, false));
                var leaf = leaves[i % leaves.Length];
                Quiet(RrBuild.Sphere(t, "Leaves", p + Vector3.up * 6.4f * s, 2.6f * s, leaf, false));
                Quiet(RrBuild.Sphere(t, "Leaves", p + new Vector3(0.9f, 7.9f, 0.4f) * s, 1.8f * s, leaf, false));
            }

            // Puffy clouds.
            var cloud = RrLook.Unlit(Color.white);
            for (int i = 0; i < 9; i++)
            {
                float a = (i / 9f) * Mathf.PI * 2f + 0.3f;
                float d = 45f + (float)rng.NextDouble() * 50f;
                var c = new Vector3(Mathf.Cos(a) * d, 38f + (float)rng.NextDouble() * 22f, Mathf.Sin(a) * d);
                for (int k = 0; k < 3; k++)
                {
                    var puff = RrBuild.Primitive(PrimitiveType.Sphere, t, "Cloud", c + new Vector3((k - 1) * 5f, k == 1 ? 1.5f : 0f, 0f),
                        Quaternion.identity, new Vector3(9f, 4.5f, 7f) * (k == 1 ? 1.2f : 0.9f), cloud, false, RrLayers.Default);
                    Quiet(puff);
                }
            }

            return root;
        }

        private static void Building(Transform t, string name, Vector3 pos, Vector3 size, Material walls, Material roof)
        {
            var baseY = LawnY;
            Quiet(RrBuild.Box(t, name, new Vector3(pos.x, baseY + size.y * 0.5f, pos.z), size, walls, false));
            Quiet(RrBuild.Box(t, name + " Roof", new Vector3(pos.x, baseY + size.y + 0.6f, pos.z), new Vector3(size.x + 1f, 1.2f, size.z + 1f), roof, false));
            // A band of windows on the side facing the dorm.
            var glass = RrLook.Solid(new Color(0.55f, 0.72f, 0.85f));
            Vector3 toDorm = new Vector3(-pos.x, 0f, -pos.z).normalized;
            bool alongX = Mathf.Abs(toDorm.x) > Mathf.Abs(toDorm.z);
            float half = alongX ? size.x * 0.5f : size.z * 0.5f;
            Vector3 face = alongX ? new Vector3(Mathf.Sign(toDorm.x) * (half + 0.05f), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toDorm.z) * (half + 0.05f));
            Vector3 bandSize = alongX ? new Vector3(0.1f, 1.6f, size.z * 0.8f) : new Vector3(size.x * 0.8f, 1.6f, 0.1f);
            for (int f = 0; f < 2; f++)
            {
                Quiet(RrBuild.Box(t, name + " Windows", new Vector3(pos.x, baseY + 3f + f * 4f, pos.z) + face, bandSize, glass, false));
            }
        }

        private static Material SkyMaterial()
        {
            var m = new Material(RrLook.Unlit(Color.white)) { name = "Dorm Sky" };
            const int h = 64;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { name = "Dorm Sky Gradient", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[2 * h];
            var ground = new Color(0.62f, 0.74f, 0.62f);
            var horizon = new Color(0.84f, 0.92f, 0.97f);
            var zenith = new Color(0.30f, 0.60f, 0.93f);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);     // sphere uv.v: 0 bottom pole .. 1 top pole
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
