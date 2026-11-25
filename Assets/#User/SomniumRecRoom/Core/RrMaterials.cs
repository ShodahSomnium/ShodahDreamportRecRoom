using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Makes Rec Room's own materials safe in the Somnium client while keeping their look.
    ///
    /// Rec Room is a URP game: its shaders (AG/Standard-Opaque and friends) are supported under the project's URP
    /// 17.3 and render the game's look exactly - recolour masks, emission, the maker-pen palette - where a
    /// conversion to URP Lit turned the dorm bunk bed white (load test, 2026-09-23). So materials are kept, with
    /// three repairs:
    ///
    /// 1. Depth priming. The client forces depth priming with a DepthNormals prepass (HBAO), and draws opaques
    ///    with ZTest Equal against it; an opaque shader with no DepthNormals pass is then invisible (AoM's ground
    ///    vanished for two uploads this way). AG/Standard-Opaque has the pass; AG/Standard-Opaque-Instanced,
    ///    AG/Standard-Opaque-Emission and AG/MakerPenShape have only ForwardLit, ShadowCaster and DepthOnly. Such
    ///    materials are moved to queue 2501, the first transparent queue, where they draw with their own ZWrite
    ///    and ZTest, unaffected by priming.
    /// 2. The built-in Sprites/Default, used by many of Rec Room's particle materials, has no stereo instancing
    ///    and draws into one eye only in VR; it becomes URP Particles/Unlit with the same texture and colour.
    /// 3. A shader the client cannot run (isSupported false) becomes URP Lit with the base map and colour copied.
    ///
    /// Replacements are remembered per source material so every renderer of it shares one; each is tagged with
    /// the room scope it was made in (RrAssets.Scope), and <see cref="Release"/> forgets a left room's, so
    /// Resources.UnloadUnusedAssets can free them (and the textures they hold) once no renderer uses them.
    /// </summary>
    public static class RrMaterials
    {
        private static readonly HashSet<int> Done = new HashSet<int>();
        private static readonly Dictionary<int, Material> Replaced = new Dictionary<int, Material>();
        private static readonly Dictionary<int, int> ReplacedScope = new Dictionary<int, int>();
        private static readonly ShaderTagId LightMode = new ShaderTagId("LightMode");
        private static readonly ShaderTagId RenderPipelineTag = new ShaderTagId("RenderPipeline");
        private static Shader _particleUnlit;
        private static Shader _lit;
        private static int _queueFixes;
        private static int _swaps;

        public static string Describe()
        {
            return "materials checked " + Done.Count + ", queue fixes " + _queueFixes + ", shader swaps " + _swaps;
        }

        /// <summary>Repairs one material in place, or returns a replacement to use instead.</summary>
        public static Material Fix(Material m)
        {
            if (m == null)
            {
                return null;
            }

            Material replacement;
            if (Replaced.TryGetValue(m.GetInstanceID(), out replacement))
            {
                return replacement;
            }

            if (!Done.Add(m.GetInstanceID()))
            {
                return m;
            }

            var shader = m.shader;
            if (shader == null || !shader.isSupported)
            {
                replacement = ToLit(m);
            }
            else if (shader.name == "Sprites/Default" || shader.name.StartsWith("Legacy Shaders/Particles", System.StringComparison.Ordinal) || shader.name == "Particles/Standard Unlit")
            {
                replacement = ToParticle(m);
            }
            else if (m.renderQueue <= 2500 && !HasDepthNormals(shader))
            {
                m.renderQueue = 2501;
                _queueFixes++;
            }

            if (replacement != null)
            {
                _swaps++;
                Replaced[m.GetInstanceID()] = replacement;
                ReplacedScope[m.GetInstanceID()] = RrAssets.Instance != null ? RrAssets.Instance.Scope : 0;
                Done.Add(replacement.GetInstanceID());
                return replacement;
            }

            return m;
        }

        /// <summary>Every renderer under a root: fixes its materials and swaps replacements in.</summary>
        public static void FixTree(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var f = Fix(mats[i]);
                    if (f != mats[i])
                    {
                        mats[i] = f;
                        changed = true;
                    }
                }

                if (changed)
                {
                    r.sharedMaterials = mats;
                }

                var ps = r as ParticleSystemRenderer;
                if (ps != null && ps.trailMaterial != null)
                {
                    var t = Fix(ps.trailMaterial);
                    if (t != ps.trailMaterial)
                    {
                        ps.trailMaterial = t;
                    }
                }
            }
        }

        /// <summary>
        /// Whether the subshader URP draws with has a DepthNormals pass. Shader.passCount and FindPassTagValue(pass)
        /// describe the *active* subshader, which Unity chooses with the global RenderPipeline tag that URP sets
        /// when it first renders; a material checked before that (the play-mode harness, 2026-09-24) saw Rec Room's
        /// built-in fallback subshader - one unnamed pass - and every AG/Standard-Opaque material was moved to the
        /// transparent queue. So the subshaders are walked explicitly: the first one tagged for URP (or untagged)
        /// is the one URP uses.
        /// </summary>
        private static bool HasDepthNormals(Shader shader)
        {
            int subshaders = shader.subshaderCount;
            for (int ss = 0; ss < subshaders; ss++)
            {
                string pipeline = shader.FindSubshaderTagValue(ss, RenderPipelineTag).name;
                if (pipeline.Length > 0 && pipeline != "UniversalPipeline" && pipeline != "UniversalRenderPipeline")
                {
                    continue;
                }

                int n = shader.GetPassCountInSubshader(ss);
                for (int p = 0; p < n; p++)
                {
                    string mode = shader.FindPassTagValue(ss, p, LightMode).name;
                    if (mode == "DepthNormals" || mode == "DepthNormalsOnly")
                    {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        private static Material ToParticle(Material m)
        {
            if (_particleUnlit == null)
            {
                _particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }

            if (_particleUnlit == null)
            {
                RrLog.WarnOnce("Materials", "noparticle", "URP Particles/Unlit not found; keeping " + m.shader.name);
                return null;
            }

            var r = new Material(_particleUnlit) { name = m.name + " (rr particle)" };
            Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Color c = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            r.SetTexture("_BaseMap", tex);
            r.SetColor("_BaseColor", c);
            bool additive = false;
            if (m.HasProperty("_DstBlend"))
            {
                additive = (int)m.GetFloat("_DstBlend") == (int)BlendMode.One;
            }

            r.SetFloat("_Surface", 1f);
            r.SetFloat("_Blend", additive ? 2f : 0f);
            r.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            r.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            r.SetFloat("_ZWrite", 0f);
            r.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            r.renderQueue = m.renderQueue > 2500 ? m.renderQueue : 3000;
            return r;
        }

        private static Material ToLit(Material m)
        {
            if (_lit == null)
            {
                _lit = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (_lit == null)
            {
                return null;
            }

            var r = new Material(_lit) { name = m.name + " (rr lit)" };
            Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            r.SetTexture("_BaseMap", tex);
            r.SetColor("_BaseColor", c);
            r.SetFloat("_Smoothness", 0.3f);
            RrLog.WarnOnce("Materials", "unsupported:" + (m.shader == null ? "null" : m.shader.name), "shader not supported here, using URP Lit: " + (m.shader == null ? "(none)" : m.shader.name));
            return r;
        }

        /// <summary>
        /// Forgets the replacements made while a room (scope) was built and visited; returns how many. They are
        /// not destroyed (a shared object may still draw with one): UnloadUnusedAssets frees the unused ones.
        /// </summary>
        public static int Release(int scope)
        {
            if (scope <= 0 || ReplacedScope.Count == 0)
            {
                return 0;
            }

            var gone = new List<int>();
            foreach (var kv in ReplacedScope)
            {
                if (kv.Value == scope)
                {
                    gone.Add(kv.Key);
                }
            }

            foreach (int id in gone)
            {
                ReplacedScope.Remove(id);
                Material replacement;
                if (Replaced.TryGetValue(id, out replacement))
                {
                    Replaced.Remove(id);
                    if (replacement != null)
                    {
                        Done.Remove(replacement.GetInstanceID());
                    }
                }

                Done.Remove(id);
            }

            return gone.Count;
        }

        public static void Shutdown()
        {
            foreach (var kv in Replaced)
            {
                if (kv.Value != null)
                {
                    Object.Destroy(kv.Value);
                }
            }

            Replaced.Clear();
            ReplacedScope.Clear();
            Done.Clear();
            _queueFixes = 0;
            _swaps = 0;
        }
    }
}
