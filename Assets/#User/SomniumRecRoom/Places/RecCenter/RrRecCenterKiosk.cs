using System.Collections.Generic;
using UnityEngine;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using L = SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter.RrRecCenterLayout;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// The information kiosk at the centre (InfoKiosk, rr_reccenter_cache.md §3), as the rec.net picture shows it from
    /// the entrance: a long low blue counter with a light wood top (the ring counter's AABB x -3.19..2.72,
    /// z -2.97..2.86, here 1.05 m high so the bell sits at hand height), four 0.38 m dark timber posts to the beams
    /// (8.4 m) at (-2.32 / 2.18, -2.2 / 2.4), a top frame at 4.29..4.58, the tall blue back panel (kiosk_walls, to
    /// 4.54) with a grid shelf of merch, and the REC ROOM logo sign between the front posts, facing the entrance
    /// (RecRoomLogo_fbx x -2.12..1.96, y 4.56..7.42, 0.26 m deep).
    /// <para>
    /// The logo mesh is only in the Studio scene, not in the install, so the sign is built from Rec Room's own logo
    /// texture (<c>RecRoom_Logo_SteamSplash_Tex</c>, Startup bundle: the orange REC ROOM with the bucket icon and its
    /// grey outline, alpha-cut): a front face, 7 darker layers behind it for the letters' 0.26 m depth, and a back
    /// face so it reads from the far end too. Until (or unless) the texture loads, TMP letters stand in.
    /// </para>
    /// </summary>
    internal static class RrRecCenterKiosk
    {
        /// <summary>The logo's crop inside the 512 x 512 splash texture (pixels, origin bottom-left): the letters and the icon.</summary>
        public const string LogoArt = "RecRoom_Logo_SteamSplash_Tex|80,146,356,256";

        public const float LogoBottom = 4.58f;
        public const float LogoTop = 7.42f;
        public const float LogoDepth = 0.26f;
        public const int LogoLayers = 7;

        public static void Build(Transform shell, RrRcSurfaces s)
        {
            var k = RrBuild.Group(shell, "Kiosk").transform;
            var c = L.Kiosk;
            float xl = c.x - 2.25f;
            float xr = c.x + 2.25f;
            float zf = c.z - 2.3f;
            float zb = c.z + 2.3f;
            float top = L.KioskCounterTop;
            var timber = new RrRcBatch(s.Timber);

            // Posts to the beams, and the top frame on them.
            float[] px = { xl, xr };
            float[] pz = { zf, zb };
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    timber.Box(new Vector3(px[i], L.BeamBottom * 0.5f, pz[j]), Quaternion.identity, new Vector3(0.38f, L.BeamBottom, 0.38f), true);
                }
            }

            float fy = (4.29f + LogoBottom) * 0.5f;
            timber.Box(new Vector3(c.x, fy, zf), Quaternion.identity, new Vector3(xr - xl, LogoBottom - 4.29f, 0.3f));
            timber.Box(new Vector3(c.x, fy, zb), Quaternion.identity, new Vector3(xr - xl, LogoBottom - 4.29f, 0.3f));
            timber.Box(new Vector3(xl, fy, c.z), Quaternion.identity, new Vector3(0.3f, LogoBottom - 4.29f, zb - zf));
            timber.Box(new Vector3(xr, fy, c.z), Quaternion.identity, new Vector3(0.3f, LogoBottom - 4.29f, zb - zf));

            // Platform inside (kiosk_floor, top 0.25) and the ring counter: the long front counter, sides and back.
            RrRcGeo.Slab(k, "Kiosk Platform", new Vector3(xl, 0f, zf), new Vector3(xr, 0.25f, zb), s.LightWood);
            Counter(k, s, -3.19f, -2.97f, 2.72f, zf + 0.05f, top);
            Counter(k, s, -3.19f, zf + 0.05f, xl + 0.2f, zb - 0.05f, top);
            Counter(k, s, xr - 0.2f, zf + 0.05f, 2.72f, zb - 0.05f, top);
            Counter(k, s, -3.19f, zb - 0.05f, 2.72f, 2.86f, top);

            // The tall blue back panel between the back posts, with the merch grid shelf on its entrance side.
            RrRcGeo.Slab(k, "Kiosk Back Panel", new Vector3(xl + 0.19f, 0.25f, zb - 0.28f), new Vector3(xr - 0.19f, 4.29f, zb - 0.08f), s.Blue);
            float gx0 = c.x - 1.35f;
            float gx1 = c.x + 1.35f;
            float gy0 = 1.3f;
            float gy1 = 2.75f;
            float gz = zb - 0.28f;
            for (int i = 0; i <= 4; i++)
            {
                float x = Mathf.Lerp(gx0, gx1, i / 4f);
                timber.Box(new Vector3(x, (gy0 + gy1) * 0.5f, gz - 0.15f), Quaternion.identity, new Vector3(0.05f, gy1 - gy0, 0.3f));
            }

            for (int j = 0; j <= 3; j++)
            {
                float y = Mathf.Lerp(gy0, gy1, j / 3f);
                timber.Box(new Vector3(c.x, y, gz - 0.15f), Quaternion.identity, new Vector3(gx1 - gx0, 0.05f, 0.3f));
            }

            Color[] merch = { new Color(0.95f, 0.55f, 0.18f), new Color(0.96f, 0.93f, 0.86f), new Color(0.25f, 0.62f, 0.72f), new Color(0.86f, 0.33f, 0.52f) };
            var batches = new RrRcBatch[merch.Length];
            for (int m = 0; m < merch.Length; m++)
            {
                batches[m] = new RrRcBatch(new RrRcSurface(RrLook.Solid(merch[m]), 1f, false));
            }

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float x = Mathf.Lerp(gx0, gx1, (i + 0.5f) / 4f);
                    float y = Mathf.Lerp(gy0, gy1, j / 3f) + 0.02f;
                    var b = batches[(i + j * 2) % merch.Length];
                    bool tall = (i + j) % 2 == 0;
                    b.Box(new Vector3(x, y + (tall ? 0.17f : 0.1f), gz - 0.15f), Quaternion.Euler(0f, (i * 17 + j * 9) % 20 - 10f, 0f), tall ? new Vector3(0.3f, 0.34f, 0.18f) : new Vector3(0.42f, 0.2f, 0.22f));
                }
            }

            for (int m = 0; m < batches.Length; m++)
            {
                batches[m].Build(k, "Kiosk Merch");
            }

            timber.Build(k, "Kiosk Timber");
            RrBuild.PointLight(k, "Kiosk Light", new Vector3(c.x, 3.6f, c.z - 0.9f), new Color(1f, 0.97f, 0.92f), 5f, 0.8f);
            Logo(k, -0.08f, zf);
        }

        /// <summary>A counter segment: blue body, a cream band and a light wood top overhanging 3 cm.</summary>
        private static void Counter(Transform k, RrRcSurfaces s, float x0, float z0, float x1, float z1, float top)
        {
            RrRcGeo.Slab(k, "Kiosk Counter", new Vector3(x0, 0f, z0), new Vector3(x1, top - 0.14f, z1), s.Blue);
            RrRcGeo.Slab(k, "Kiosk Counter Band", new Vector3(x0 - 0.01f, top - 0.14f, z0 - 0.01f), new Vector3(x1 + 0.01f, top - 0.05f, z1 + 0.01f), s.Cream);
            RrRcGeo.Slab(k, "Kiosk Counter Top", new Vector3(x0 - 0.03f, top - 0.05f, z0 - 0.03f), new Vector3(x1 + 0.03f, top, z1 + 0.03f), s.LightWood);
        }

        /// <summary>The REC ROOM sign between the front posts, facing the entrance (-Z), lettering first, texture when loaded.</summary>
        private static void Logo(Transform k, float x, float zPosts)
        {
            float w = 4.08f;
            float h = LogoTop - LogoBottom;
            var root = RrBuild.Group(k, "REC ROOM Logo", new Vector3(x, (LogoBottom + LogoTop) * 0.5f, zPosts - 0.13f + LogoDepth * 0.5f)).transform;
            var fallback = new List<GameObject>();
            var orange = new Color(0.86f, 0.36f, 0.12f);
            var a = RrRcGeo.Label(root, "Logo Text", "REC", new Vector3(-0.25f, h * 0.22f, -LogoDepth * 0.5f - 0.01f), 0f, 1.1f, w, orange);
            var b = RrRcGeo.Label(root, "Logo Text", "ROOM", new Vector3(0f, -h * 0.25f, -LogoDepth * 0.5f - 0.01f), 0f, 1.1f, w, orange);
            fallback.Add(a.gameObject);
            fallback.Add(b.gameObject);

            RrCoverArt.Load(LogoArt, art =>
            {
                if (root == null || art == null || art.Texture == null)
                {
                    if (root != null)
                    {
                        RrLog.Warn("RecCenter", "REC ROOM logo texture not found: lettering stands in");
                    }

                    return;
                }

                float aspect = art.Aspect > 0.1f ? art.Aspect : w / h;
                var size = new Vector2(h * aspect, h);
                var front = LogoMaterial(art, Color.white);
                var side = LogoMaterial(art, new Color(0.62f, 0.34f, 0.2f));
                var back = LogoMaterial(art, new Color(0.8f, 0.72f, 0.66f));
                for (int i = LogoLayers; i >= 1; i--)
                {
                    float z = -LogoDepth * 0.5f + LogoDepth * i / (LogoLayers + 1f);
                    RrRcGeo.Quad(root, "Logo Depth", new Vector3(0f, 0f, z), Quaternion.identity, size, side);
                }

                RrRcGeo.Quad(root, "Logo Back", new Vector3(0f, 0f, LogoDepth * 0.5f), Quaternion.Euler(0f, 180f, 0f), size, back);
                RrRcGeo.Quad(root, "Logo Front", new Vector3(0f, 0f, -LogoDepth * 0.5f), Quaternion.identity, size, front);
                for (int i = 0; i < fallback.Count; i++)
                {
                    if (fallback[i] != null)
                    {
                        Object.Destroy(fallback[i]);
                    }
                }

                RrLog.Info("RecCenter", "REC ROOM logo from " + art.Texture.name + " (" + size.x.ToString("0.00") + " x " + size.y.ToString("0.00") + " m, " + (LogoLayers + 2) + " layers)");
            });
        }

        /// <summary>URP Lit transparent (as RrLook.Glass) with the logo texture and its crop: the alpha cuts the letters out.</summary>
        private static Material LogoMaterial(RrArt art, Color tint)
        {
            var m = new Material(RrLook.Glass(Color.white)) { name = "Rr Rec Center Logo" };
            m.SetTexture("_BaseMap", art.Texture);
            m.SetTextureScale("_BaseMap", new Vector2(art.Uv.width, art.Uv.height));
            m.SetTextureOffset("_BaseMap", new Vector2(art.Uv.x, art.Uv.y));
            m.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 1f));
            m.SetFloat("_Smoothness", 0.2f);
            RrRcGeo.Own(m);
            return m;
        }
    }
}
