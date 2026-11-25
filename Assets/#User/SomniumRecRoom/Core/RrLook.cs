using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Materials for the geometry this world builds itself (room shells, arenas, courses), in Rec Room's own look.
    ///
    /// Rec Room's creators build rooms with the Maker Pen, and its shapes use one material, "AG-MakerPenShape"
    /// (shader AG/MakerPenShape), coloured per object through _BaseColor - the Dorm kit's walls are exactly that
    /// (Dorm_Wall_1: a 5.56 x 6.30 m panel, default orange 1, 0.58, 0). Solid surfaces here are copies of that
    /// material in the wanted colour, so a rebuilt wall looks like a Rec Room wall. The material is loaded from
    /// the player's bundles at start; if it cannot be, URP Lit with the same colour stands in (logged).
    ///
    /// AG/MakerPenShape has no DepthNormals pass, so its copies get the same queue repair as every Rec Room
    /// material (<see cref="RrMaterials"/>). Colours are given in sRGB, as Rec Room's palette is, and passed
    /// to the material as they are (Unity converts colour properties for a linear project).
    /// </summary>
    public static class RrLook
    {
        public const string MakerPenMaterial = "AG-MakerPenShape";

        private static Material _makerPen;
        private static Shader _lit;
        private static Shader _unlit;
        private static readonly Dictionary<int, Material> Solids = new Dictionary<int, Material>();
        private static readonly Dictionary<int, Material> Emissives = new Dictionary<int, Material>();
        private static readonly Dictionary<int, Material> Unlits = new Dictionary<int, Material>();
        private static readonly Dictionary<int, Material> Glasses = new Dictionary<int, Material>();
        private static bool _loaded;

        public static bool UsingRecRoomMaterial
        {
            get { return _makerPen != null; }
        }

        // Rec Room's palette, as its maker pen offers it (sRGB).
        public static readonly Color White = new Color(0.93f, 0.93f, 0.92f);
        public static readonly Color Cream = new Color(0.96f, 0.91f, 0.80f);
        public static readonly Color Grey = new Color(0.55f, 0.56f, 0.58f);
        public static readonly Color DarkGrey = new Color(0.22f, 0.23f, 0.26f);
        public static readonly Color Red = new Color(0.87f, 0.23f, 0.21f);
        public static readonly Color Orange = new Color(1f, 0.58f, 0f);
        public static readonly Color Yellow = new Color(1f, 0.83f, 0.23f);
        public static readonly Color Green = new Color(0.36f, 0.72f, 0.33f);
        public static readonly Color Teal = new Color(0.20f, 0.70f, 0.70f);
        public static readonly Color Blue = new Color(0.20f, 0.49f, 0.87f);
        public static readonly Color Purple = new Color(0.52f, 0.33f, 0.73f);
        public static readonly Color Pink = new Color(0.95f, 0.51f, 0.67f);
        public static readonly Color Wood = new Color(0.66f, 0.45f, 0.27f);
        public static readonly Color DarkWood = new Color(0.42f, 0.27f, 0.16f);
        public static readonly Color Grass = new Color(0.42f, 0.66f, 0.30f);
        public static readonly Color Sand = new Color(0.90f, 0.80f, 0.58f);
        public static readonly Color Water = new Color(0.25f, 0.55f, 0.80f);
        public static readonly Color TeamBlue = new Color(0.18f, 0.51f, 0.95f);
        public static readonly Color TeamRed = new Color(0.95f, 0.27f, 0.22f);

        /// <summary>Loads the maker-pen material; RrMain waits for it before rooms are built.</summary>
        public static IEnumerator Load()
        {
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            _unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (RrAssets.Instance != null)
            {
                yield return RrAssets.Instance.LoadRoutine<Material>(MakerPenMaterial, 21, m => _makerPen = m);
            }

            _loaded = true;
            RrLog.Info("Look", _makerPen != null
                ? "maker-pen material from the bundles (" + (_makerPen.shader != null ? _makerPen.shader.name : "?") + ")"
                : "maker-pen material not found: URP Lit stands in");
        }

        public static void Init()
        {
            // Everything happens in Load (awaited by RrMain); kept for the boot step list.
        }

        /// <summary>A flat painted surface in Rec Room's maker-pen look.</summary>
        public static Material Solid(Color c)
        {
            int key = Key(c);
            Material m;
            if (Solids.TryGetValue(key, out m) && m != null)
            {
                return m;
            }

            if (_makerPen != null)
            {
                m = new Material(_makerPen) { name = "Rr Solid " + ColorUtility.ToHtmlStringRGB(c) };
                m.SetColor("_BaseColor", c);
                m = RrMaterials.Fix(m);
            }
            else
            {
                m = new Material(_lit) { name = "Rr Solid (lit) " + ColorUtility.ToHtmlStringRGB(c) };
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", 0.15f);
            }

            m.enableInstancing = true;
            Solids[key] = m;
            return m;
        }

        /// <summary>A glowing surface (signs, door frames, lane lights).</summary>
        public static Material Emissive(Color c, float intensity = 1.5f)
        {
            int key = Key(c) ^ (int)(intensity * 1000f);
            Material m;
            if (Emissives.TryGetValue(key, out m) && m != null)
            {
                return m;
            }

            m = new Material(_lit) { name = "Rr Emissive " + ColorUtility.ToHtmlStringRGB(c) };
            m.SetColor("_BaseColor", c);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Emissives[key] = m;
            return m;
        }

        /// <summary>Unlit colour (screens, markers, UI in the world).</summary>
        public static Material Unlit(Color c)
        {
            int key = Key(c);
            Material m;
            if (Unlits.TryGetValue(key, out m) && m != null)
            {
                return m;
            }

            m = new Material(_unlit != null ? _unlit : _lit) { name = "Rr Unlit " + ColorUtility.ToHtmlStringRGB(c) };
            m.SetColor("_BaseColor", c);
            Unlits[key] = m;
            return m;
        }

        /// <summary>See-through tinted surface (windows, force fields, circle walls).</summary>
        public static Material Glass(Color c)
        {
            int key = Key(c);
            Material m;
            if (Glasses.TryGetValue(key, out m) && m != null)
            {
                return m;
            }

            m = new Material(_lit) { name = "Rr Glass " + ColorUtility.ToHtmlStringRGB(c) };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Smoothness", 0.9f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetColor("_BaseColor", c);
            Glasses[key] = m;
            return m;
        }

        private static int Key(Color c)
        {
            return (Mathf.RoundToInt(c.r * 255f) << 24) ^ (Mathf.RoundToInt(c.g * 255f) << 16) ^ (Mathf.RoundToInt(c.b * 255f) << 8) ^ Mathf.RoundToInt(c.a * 255f);
        }

        public static void Shutdown()
        {
            DestroyAll(Solids);
            DestroyAll(Emissives);
            DestroyAll(Unlits);
            DestroyAll(Glasses);
            _makerPen = null;
            _loaded = false;
        }

        private static void DestroyAll(Dictionary<int, Material> d)
        {
            foreach (var kv in d)
            {
                if (kv.Value != null)
                {
                    Object.Destroy(kv.Value);
                }
            }

            d.Clear();
        }
    }
}
