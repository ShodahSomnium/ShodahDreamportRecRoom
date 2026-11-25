using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// The hover glow: Rec Room brightens a tool when a hand can grab it (Tool.renderSettings lists the renderers
    /// that do and do not highlight). Here it is a tint written through MaterialPropertyBlocks - the base colour
    /// multiplied (both _BaseColor and the legacy _Color, whichever the shader declares; Rec Room's AG/Standard
    /// shaders declare both) plus a little emission where the shader has _EmissionColor. Nothing is instantiated and
    /// the shared materials are never touched, so switching it on and off costs a property block per material and
    /// the renderer goes back to its batch when the block is cleared.
    ///
    /// Only the objects under hover carry a block (at most one per hand), and particle, trail and line renderers
    /// are left alone.
    /// </summary>
    public static class RrHighlight
    {
        /// <summary>Base colour multiplier while highlighted.</summary>
        public static float Brighten = 1.35f;

        /// <summary>Emission added while highlighted (shaders with _EmissionColor).</summary>
        public static Color Emission = new Color(0.12f, 0.12f, 0.11f, 0f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private static readonly Dictionary<Component, List<Renderer>> Lit = new Dictionary<Component, List<Renderer>>();
        private static readonly List<Renderer> Scratch = new List<Renderer>();
        private static MaterialPropertyBlock _block;
        private static readonly List<Material> Materials = new List<Material>();

        public static bool IsLit(Component owner)
        {
            return owner != null && Lit.ContainsKey(owner);
        }

        /// <summary>Highlights a grabbable (its Tool.highlightRenderers, or every renderer under it).</summary>
        public static void Set(RrGrabbable item, bool on)
        {
            if (item == null)
            {
                return;
            }

            Set(item, item.HighlightRenderers, on);
        }

        /// <summary>Highlights the renderers under an owner (a button's cap, a door): <paramref name="only"/> or all of them.</summary>
        public static void Set(Component owner, IList<Renderer> only, bool on)
        {
            if (ReferenceEquals(owner, null))
            {
                return;
            }

            List<Renderer> lit;
            bool isLit = Lit.TryGetValue(owner, out lit);
            if (on == isLit)
            {
                return;
            }

            if (!on)
            {
                Lit.Remove(owner);
                foreach (var r in lit)
                {
                    Restore(r);
                }

                return;
            }

            if (owner == null)
            {
                return;
            }

            lit = new List<Renderer>();
            if (only != null)
            {
                foreach (var r in only)
                {
                    if (r != null && r.gameObject.activeInHierarchy)
                    {
                        lit.Add(r);
                    }
                }
            }
            else
            {
                Scratch.Clear();
                owner.GetComponentsInChildren(false, Scratch);
                foreach (var r in Scratch)
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled)
                    {
                        continue;
                    }

                    lit.Add(r);
                }

                Scratch.Clear();
            }

            Lit[owner] = lit;
            foreach (var r in lit)
            {
                Apply(r);
            }
        }

        private static void Apply(Renderer r)
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            Materials.Clear();
            r.GetSharedMaterials(Materials);
            for (int i = 0; i < Materials.Count; i++)
            {
                var m = Materials[i];
                if (m == null)
                {
                    continue;
                }

                _block.Clear();
                bool any = false;
                if (m.HasProperty(BaseColorId))
                {
                    _block.SetColor(BaseColorId, Bright(m.GetColor(BaseColorId)));
                    any = true;
                }

                if (m.HasProperty(ColorId))
                {
                    _block.SetColor(ColorId, Bright(m.GetColor(ColorId)));
                    any = true;
                }

                if (m.HasProperty(EmissionId))
                {
                    _block.SetColor(EmissionId, m.GetColor(EmissionId) + Emission);
                    any = true;
                }

                if (any)
                {
                    r.SetPropertyBlock(_block, i);
                }
            }

            Materials.Clear();
        }

        private static Color Bright(Color c)
        {
            return new Color(c.r * Brighten, c.g * Brighten, c.b * Brighten, c.a);
        }

        private static void Restore(Renderer r)
        {
            if (r == null)
            {
                return;
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _block.Clear();
            int n = r.sharedMaterials.Length;
            for (int i = 0; i < n; i++)
            {
                // An empty block clears the per-material override (and lets the renderer batch again).
                r.SetPropertyBlock(_block, i);
            }
        }

        /// <summary>Turns every highlight off (room change, shutdown).</summary>
        public static void Clear()
        {
            foreach (var kv in Lit)
            {
                foreach (var r in kv.Value)
                {
                    Restore(r);
                }
            }

            Lit.Clear();
        }

        internal static void Shutdown()
        {
            Clear();
            _block = null;
        }
    }
}
