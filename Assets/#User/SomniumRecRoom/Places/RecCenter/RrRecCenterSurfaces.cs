using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>One surface of the procedural shell: a material and the world size of one texture repeat.</summary>
    internal struct RrRcSurface
    {
        public Material Material;

        /// <summary>Metres per texture repeat (the box meshes carry metre UVs divided by this).</summary>
        public float Tile;

        /// <summary>Metres per repeat along the texture's V (0: same as <see cref="Tile"/>).</summary>
        public float TileV;

        /// <summary>True when the material is Rec Room's own (false: an RrLook fallback colour).</summary>
        public bool RecRoom;

        public RrRcSurface(Material material, float tile, bool recRoom, float tileV = 0f)
        {
            Material = material;
            Tile = tile;
            TileV = tileV;
            RecRoom = recRoom;
        }
    }

    /// <summary>
    /// The final Rec Center's surfaces (rr_reccenter_cache.md §6, colours read from the July 2025 scene; wall paint
    /// from the rec.net picture, because the scene stores it in vertex colours that were not read). Rec Room's own
    /// materials are not containers: they become resident when a container that uses them is loaded
    /// (rr_reccenter.md §2.7). The Charades hull (key 84, loaded anyway by RrPortal for the Dorm door) brings the
    /// wood floors, paints, brick, carpet, fluorescent panels and posters; `(Sandbox_Wall)` brings Paint_Blue_Mat;
    /// `(Sandbox_Table_Coffee_Large)` the dark-brown timber (Wood_Horizontal_DarkBrown_5x_Mat, the desaturated
    /// plank texture the scene's beams use); the jukebox prefab only its clear glass (the jukebox itself is gone);
    /// Wood_Horizontal_RedBrown_mat is a direct material container. Each is copied once with its texture scale reset
    /// (our boxes carry metre UVs) and tinted to the measured colour. Anything missing falls back to an RrLook colour.
    /// </summary>
    internal sealed class RrRcSurfaces
    {
        /// <summary>`Wood_Floor_Mat` _BaseColor in the July 2025 scene (light orange-brown planks).</summary>
        public static readonly Color FloorWood = new Color(0.972f, 0.633f, 0.327f);

        /// <summary>`Wood_Horizontal_DarkBrown_3x_Mat`: all timber beams, posts, sills and stair rails.</summary>
        public static readonly Color TimberColor = new Color(0.345f, 0.197f, 0.130f);

        /// <summary>`LockerRoom_Metal_Mat`: every window and skylight frame, the balcony rail posts (navy).</summary>
        public static readonly Color NavyFrame = new Color(0.070f, 0.171f, 0.382f);

        /// <summary>`RecCenter_CarpetBorder_Mat`: the gold border of every rug.</summary>
        public static readonly Color GoldBorder = new Color(0.858f, 0.614f, 0.336f);

        /// <summary>`Curtain_Red_Mat` (stage curtains).</summary>
        public static readonly Color CurtainRed = new Color(0.509f, 0.094f, 0.094f);

        /// <summary>`Orientation_Carpet` (the green carpet in front of the Campus Jams door, now the Magic Door's).</summary>
        public static readonly Color GreenCarpet = new Color(0.339f, 0.509f, 0.465f);

        // Wall paint from the rec.net picture (GUESS values for what the scene keeps in vertex colours).
        public static readonly Color CreamPaint = new Color(0.96f, 0.89f, 0.74f);
        public static readonly Color BluePaint = new Color(0.20f, 0.47f, 0.74f);
        public static readonly Color TerracottaPaint = new Color(0.82f, 0.43f, 0.27f);
        public static readonly Color OrangePaint = new Color(0.953f, 0.477f, 0.140f);
        public static readonly Color TealPaint = new Color(0.22f, 0.56f, 0.56f);
        public static readonly Color PlumPaint = new Color(0.36f, 0.25f, 0.34f);

        public RrRcSurface Floor;
        public RrRcSurface LightWood;
        public RrRcSurface Cream;
        public RrRcSurface Blue;
        public RrRcSurface Terracotta;
        public RrRcSurface Orange;
        public RrRcSurface Teal;
        public RrRcSurface Plum;
        public RrRcSurface Brick;
        public RrRcSurface Timber;
        public RrRcSurface Navy;
        public RrRcSurface CarpetNavy;
        public RrRcSurface CarpetBrown;
        public RrRcSurface CarpetTaupe;
        public RrRcSurface CarpetGreen;
        public RrRcSurface Gold;
        public RrRcSurface Curtain;
        public RrRcSurface Fluorescent;
        public RrRcSurface Glass;
        public RrRcSurface Grass;
        public RrRcSurface Ceiling;
        public RrRcSurface Black;
        public RrRcSurface Chalk;
        public RrRcSurface WarmGrey;

        public Material HydratePoster;
        public Material OpenMicPoster;
        public Material CharadesPoster;
        public Material ExitSign;

        public int FoundCount;
        public int MissingCount;

        private readonly List<Object> _owned;

        public RrRcSurfaces(List<Object> owned)
        {
            _owned = owned;
        }

        /// <summary>Makes the helper containers resident, then picks the materials (one scan of the loaded materials).</summary>
        public IEnumerator Load()
        {
            var assets = RrAssets.Instance;
            if (assets != null)
            {
                int pending = 0;
                string[] prefabs = { "(Sandbox_Wall)", "(Sandbox_Table_Coffee_Large)", "(BowlingAlley_Jukebox_Sandbox)Variant" };
                for (int i = 0; i < prefabs.Length; i++)
                {
                    pending++;
                    assets.LoadPrefab(prefabs[i], (go, data) => pending--);
                }

                pending++;
                assets.Load<Material>("Wood_Horizontal_RedBrown_mat", 21, m => pending--);
                float until = Time.realtimeSinceStartup + 20f;
                while (pending > 0 && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                }
            }

            var byName = new Dictionary<string, Material>();
            var all = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < all.Length; i++)
            {
                var m = all[i];
                if (m != null && !byName.ContainsKey(m.name))
                {
                    byName[m.name] = m;
                }
            }

            // Rec Room tiles its plank texture 10 x 40 (long, narrow boards): 3 m along the planks (X), 0.75 m across
            // (5 rows of 0.15 m). Swapping the two sizes gives 0.6 m wide stubs that read as a checkerboard (harness).
            Floor = Make(byName, "Wood_Floor_Matte_mat", 3f, FloorWood, FloorWood, 0.75f);
            // The faded plank texture reads grey untinted; the picture's counter tops are light warm wood.
            LightWood = Make(byName, "Wood_Floor_Faded_Matte_Mat", 3f, new Color(0.86f, 0.70f, 0.50f), new Color(1.0f, 0.84f, 0.62f), 0.75f);
            Cream = Make(byName, "Paint_White_Mat", 2f, CreamPaint, CreamPaint);
            Blue = Make(byName, "Paint_Blue_Mat", 2f, BluePaint, BluePaint);
            Terracotta = Make(byName, "Paint_White_Mat", 2f, TerracottaPaint, TerracottaPaint);
            Orange = Make(byName, "Paint_White_Mat", 2f, OrangePaint, OrangePaint);
            Teal = Make(byName, "Paint_White_Mat", 2f, TealPaint, TealPaint);
            Plum = Make(byName, "Paint_White_Mat", 2f, PlumPaint, PlumPaint);
            WarmGrey = Make(byName, "Paint_Warm_Grey_Mat", 2f, new Color(0.453f, 0.409f, 0.374f), null);
            Brick = Make(byName, "Brick_Red_Mat", 1.2f, new Color(0.62f, 0.26f, 0.20f), null);
            Timber = Make(byName, "Wood_Horizontal_DarkBrown_5x_Mat", 2f, TimberColor, null);
            if (!Timber.RecRoom)
            {
                MissingCount--;
                Timber = Make(byName, "Wood_Horizontal_RedBrown_mat", 2f, TimberColor, new Color(0.55f, 0.38f, 0.30f));
            }

            Navy = new RrRcSurface(RrLook.Solid(NavyFrame), 1f, false);
            CarpetNavy = Make(byName, "Carpet_LowPile_Striped_Blue_Mat", 1f, new Color(0.20f, 0.26f, 0.45f), new Color(0.42f, 0.48f, 0.78f));
            CarpetBrown = Make(byName, "Charades_Carpet_Taupe_mat", 1f, new Color(0.47f, 0.31f, 0.21f), new Color(0.62f, 0.40f, 0.26f));
            CarpetTaupe = Make(byName, "Charades_Carpet_Taupe_mat", 1f, new Color(0.396f, 0.379f, 0.341f), null);
            CarpetGreen = new RrRcSurface(RrLook.Solid(GreenCarpet), 1f, false);
            Gold = new RrRcSurface(RrLook.Solid(GoldBorder), 1f, false);
            Curtain = new RrRcSurface(RrLook.Solid(new Color(0.66f, 0.13f, 0.13f)), 1f, false);
            // Light_Fluorescent_mat is a fixture atlas (tubes on a dark housing): on a 0.35 x 1.25 m box its metre UVs
            // show mostly housing (a dark block, harness), so the fixtures glow in our own emissive white.
            Fluorescent = new RrRcSurface(RrLook.Emissive(new Color(1f, 0.98f, 0.93f), 1.6f), 1f, false);

            Glass = Make(byName, "Glass_Clear_mat", 1f, new Color(0.8f, 0.9f, 1f), null);
            if (!Glass.RecRoom)
            {
                Glass = new RrRcSurface(RrLook.Glass(new Color(0.75f, 0.88f, 0.95f, 0.25f)), 1f, false);
            }

            // Ceilings face away from Somnium's sun and get only grazing point light, which leaves them muddy grey;
            // the picture's plaster ceiling reads as flat warm beige, so it is unlit.
            Ceiling = new RrRcSurface(RrLook.Unlit(new Color(0.88f, 0.82f, 0.70f)), 2f, false);
            Grass = new RrRcSurface(RrLook.Solid(new Color(0.42f, 0.64f, 0.30f)), 2f, false);
            Black = new RrRcSurface(RrLook.Solid(new Color(0.10f, 0.09f, 0.11f)), 2f, false);
            Chalk = new RrRcSurface(RrLook.Solid(new Color(0.14f, 0.20f, 0.18f)), 1f, false);

            HydratePoster = Find(byName, "Poster_StayHydrated_Mat");
            OpenMicPoster = Find(byName, "Poster_OpenMic_SignUp_Mat");
            CharadesPoster = Find(byName, "Charades_Posters_Mat");
            ExitSign = Find(byName, "ExitSign_mat");

            RrLog.Info("RecCenter", "surfaces: " + FoundCount + " Rec Room materials, " + MissingCount + " fallback colour(s)"
                + (Timber.RecRoom ? ", timber" : "") + (Brick.RecRoom ? ", brick" : "") + (HydratePoster != null ? ", posters" : ""));
        }

        private static Material Find(Dictionary<string, Material> byName, string name)
        {
            Material m;
            byName.TryGetValue(name, out m);
            return m != null ? RrMaterials.Fix(m) : null;
        }

        private RrRcSurface Make(Dictionary<string, Material> byName, string name, float tile, Color fallback, Color? tint, float tileV = 0f)
        {
            Material src;
            if (byName.TryGetValue(name, out src) && src != null)
            {
                var m = new Material(src) { name = name + " (Rr Rec Center)" };
                ResetScale(m, "_BaseMap");
                ResetScale(m, "_MainTex");
                ResetScale(m, "_BumpMap");
                if (tint.HasValue && m.HasProperty("_BaseColor"))
                {
                    m.SetColor("_BaseColor", tint.Value);
                }

                _owned.Add(m);
                var fixedMaterial = RrMaterials.Fix(m);
                FoundCount++;
                return new RrRcSurface(fixedMaterial, tile, true, tileV);
            }

            MissingCount++;
            return new RrRcSurface(RrLook.Solid(fallback), tile, false, tileV);
        }

        private static void ResetScale(Material m, string property)
        {
            if (m.HasProperty(property))
            {
                m.SetTextureScale(property, Vector2.one);
                m.SetTextureOffset(property, Vector2.zero);
            }
        }
    }
}
