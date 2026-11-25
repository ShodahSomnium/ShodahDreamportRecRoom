using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron
{
    /// <summary>
    /// Iteration 10: Rec Room's coloured haze in a Jumbotron room - the Scifi_AreaFog_* mist volumes and the Portal_* glows,
    /// which <see cref="RrJumbotronHull"/> does not draw as surfaces - and the glow of the kit's level-only props (light
    /// shafts, laser beams, glowing crates, explosive barrels), which the kit draws in its grey stand-in material. Built from
    /// the same hull data, under the same root, freed with the room (<see cref="RrJumbotronHull.Owned"/>). Numbers:
    /// DN/iter10/jt_lighting.md (findings C, F, H; 4.3-4.5) and jt_materials.md section 6, measured from the Android
    /// levels' materials (data only; nothing of them is loaded - every material here is a clone of a PC install one).
    ///
    /// <b>Mist.</b> The levels' FogSin mist (additive, invisible within ~2 m of the eye, full from 10-20 m, soft where it
    /// meets geometry, back faces culled) is not in the install; its closest install relative is Sandbox_Fog_Mat
    /// ("Shader Forge/Particles/AdditiveFadeDistance": the same camera fade, Cull Back, but alpha-blended 2 x tex x vertex
    /// colour x _TintColor). With _TintColor = colour / (2 x <see cref="HazeAlpha"/>) and alpha <see cref="HazeAlpha"/> it
    /// adds FogSin's colour while darkening what is behind by only 10 % per layer. The shader's depth-texture softness is
    /// off (we cannot rely on the client's depth texture): FogSin's edge fade saturate(gap / _EdgeDistance)^_EdgePower is
    /// baked per vertex into the vertex alpha, the gap measured by a ray from the vertex along -normal against the room's
    /// colliders. Rec Room measures the gap along the view ray: a horizontal face, always seen at a grazing angle from
    /// standing height, bakes gap / <see cref="GrazingSin"/> (the steep views right below the eye are inside the camera fade).
    ///
    /// <b>Portals.</b> PortalFog (14 of the 19 materials: additive, camera fade, depth fade, a (u + U)(v + V) mask, no
    /// fresnel) is not in the install: it uses the mist's AdditiveFadeDistance clone with the portal's colour x alpha and
    /// camera fade, the mask and depth fade baked into the vertex alpha. PortalFogFresnel (the other 5) is: the install's
    /// Stagelight_Fog_Mat with the level's colour and numbers, its own depth fade made 1 and the baked one in the vertex
    /// colour (the _VERTEX_COLOR variant multiplies it in). Portals are seen from both sides, so each plate is two
    /// one-sided copies (front and flipped back; the fresnel is max(0, N.V)), each baked with the gap behind it. Big plates
    /// (the Foyer's 34 x 14 m stage curtain, the Stadium's 31 x 47 m red sheet) are cut into 2.5 m cells so the bake follows
    /// the room; UVs run 0..1 across the plate, v from its lower edge up (horizontal sheets: mask 1).
    ///
    /// <b>Kit glow.</b> The kit gives every material the install lacks ("?name") one grey stand-in and merges those
    /// renderers into shared batches. For the level-only glowing materials (and the black curtains, the invisible nav wall)
    /// this swaps the material of the kit's separate objects in place; merged grey batches are hidden and their records'
    /// grey submeshes rebuilt here, the glowing ones in install clones (LightShaft_Yellow_Mat with the level colour for the
    /// light shafts and red lasers, Door_SciFi_Cardboard_mat for the cyan-strip crates, the Arena_Cyber_Emit templates for
    /// the barrels and the Cargo strip, Curtain_Red_Mat for the black curtains), the rest in the same grey.
    /// No colliders, no shadows from the haze, nothing on the light grid (its shaders are not grid shaders).
    ///
    /// <b>Light shafts (iteration 11).</b> The levels' shafts are the install's skinned LightArchShaft posed by its bones; the
    /// kit drew it unskinned (a solid cone or box by the lamp). The kit's shafts are hidden and each one is rebuilt as a
    /// soft additive frustum between its measured corner bones (<see cref="ShaftData"/>). The Flight Deck's EmitBake cube
    /// (the light-cyan square in its doorway) is not drawn.
    /// </summary>
    public static class RrJumbotronHaze
    {
        public const string Area = "Jumbotron";

        /// <summary>The haze's alpha: 10 % darkening per layer, the colour scaled up so the added light equals Rec Room's.</summary>
        public const float HazeAlpha = 0.1f;

        /// <summary>sin of the typical view angle onto a horizontal haze face (standing eye ~1 m above it, ~10 m away).</summary>
        public const float GrazingSin = 0.1f;

        /// <summary>Largest cell a portal / mist plate is cut into for the per-vertex bake (m).</summary>
        public const float Cell = 2.5f;

        /// <summary>A ray that hits nothing within this counts as open space (m).</summary>
        public const float MaxGap = 50f;

        public const int MaxVertices = 60000;
        private const float Budget = 0.004f;
        private const float Tiny = 0.001f;

        private static readonly string[] MistTemplates = { "Sandbox_Fog_Mat" };
        private static readonly string[] PortalTemplates = { "Stagelight_Fog_Mat", "Stagelight_Sandbox_Fog_Mat" };
        private static readonly string[] ShaftTemplates = { "LightShaft_Yellow_Mat", "GlowBeam_Green_Mat", "DoorGlow_Mat" };

        // ------------------------------------------------------------------ level numbers (measured, data only)

        /// <summary>FogSin: _EdgeDistance, _EdgePower, _StartDistance, _EndDistance (mats_glow.txt).</summary>
        private struct MistSpec
        {
            public string Name;
            public float Edge, EdgePower, Start, End;

            public MistSpec(string name, float edge, float edgePower, float start, float end)
            {
                Name = name;
                Edge = edge;
                EdgePower = edgePower;
                Start = start;
                End = end;
            }
        }

        private static readonly MistSpec[] Mists =
        {
            new MistSpec("Scifi_AreaFog_Cyan_Mat", 5.55f, 1.01f, 20f, 2f),
            new MistSpec("Scifi_AreaFog_Pink_Mat", 2.23f, 1.01f, 10f, 2f),
            new MistSpec("Scifi_AreaFog_Red_Mat", 1.97f, 1.01f, 10f, 1f),
            new MistSpec("Scifi_AreaFog_Purple_Mat", 2.95f, 1.01f, 10f, 2f),
        };

        private static readonly MistSpec DefaultMist = new MistSpec("", 3f, 1f, 10f, 2f);

        /// <summary>PortalFog(Fresnel): _DistancePower, _EndDistance, _FadeDistance, _StartDistance, _UValue, _VValue (portal_values.txt).</summary>
        private struct PortalSpec
        {
            public string Name;
            public float Power, End, Fade, Start, U, V;
            /// <summary>PortalFogFresnel (in the install) rather than PortalFog.</summary>
            public bool Fresnel;

            public PortalSpec(string name, float power, float end, float fade, float start, float u, float v, bool fresnel = false)
            {
                Name = name;
                Power = power;
                End = end;
                Fade = fade;
                Start = start;
                U = u;
                V = v;
                Fresnel = fresnel;
            }
        }

        private static readonly PortalSpec[] Portals =
        {
            new PortalSpec("Portal_Blue_Cargo1_Mat", 1.35f, 0.1f, 12.2f, 20f, 1.27f, 0.07f),
            new PortalSpec("Portal_Blue_Concession_Mat", 1.05f, 1f, 18.66f, 11.5f, 1.27f, 0.07f),
            new PortalSpec("Portal_Blue_Garage_Mat", 1.35f, 6f, 12.2f, 20f, 1.27f, -0.32f),
            new PortalSpec("Portal_Blue_Mat", 1.35f, 6f, 12.2f, 20f, 1.27f, 0.07f),
            new PortalSpec("Portal_Cyan_Concession_Mat", 2.24f, 5f, 19.78f, 10f, 1.2f, -0.12f, true),
            new PortalSpec("Portal_Cyan_Mat", 1.35f, 6f, 12.2f, 20f, 1.27f, 0.07f),
            new PortalSpec("Portal_Cyan_StadiumEntry_Mat", 1.88f, 6f, 12.2f, 20f, 1.05f, -0.18f),
            new PortalSpec("Portal_Pink_Concession_Mat", 1.64f, 1f, 18.66f, 11.5f, 0f, 0f),
            new PortalSpec("Portal_Pink_Foyer_Mat", 1f, 3f, 28.71f, 8.92f, 1f, 0.02f),
            new PortalSpec("Portal_Pink_Mat", 1f, 3f, 28.71f, 8.92f, 0f, 1f),
            new PortalSpec("Portal_Purple_Cargo_Mat", 1.35f, 2f, 16.8f, 10f, 0f, 1f, true),
            new PortalSpec("Portal_Purple_Concession_Mat", 1.35f, 6f, 22.3f, 15.77f, 0.29f, -0.02f, true),
            new PortalSpec("Portal_Purple_Hallway2_Faint_Mat", 1.35f, 1.5f, 22.8f, 10f, 1f, 0f),
            new PortalSpec("Portal_Purple_Hallway2_Mat", 1.71f, 2f, 6.3f, 20f, 0f, 1f),
            new PortalSpec("Portal_Purple_StadiumEntry2_Mat", 1.35f, 6f, 12.2f, 20f, 1.27f, 0.07f),
            new PortalSpec("Portal_Purple_StadiumEntry_Mat", 1.35f, 1f, 19.26f, 15f, 1.27f, 0.07f, true),
            new PortalSpec("Portal_Red_Hallway2_Mat", 2f, 2f, 7.88f, 10f, 1f, 1f),
            new PortalSpec("Portal_Red_Mat", 1.35f, 6f, 12.2f, 20f, 1.27f, 0.07f),
            new PortalSpec("Portal_Red_Stadium_Mat", 1.51f, 6f, 6.98f, 10f, 1f, 1f, true),
        };

        private static readonly PortalSpec DefaultPortal = new PortalSpec("", 1.35f, 6f, 12.2f, 20f, 1.27f, 0.07f);

        private enum GlowKind
        {
            /// <summary>AG/Unlit-Additive(-Soft) clone: light shafts, laser beams.</summary>
            Additive,

            /// <summary>AG/Standard-* template clone with the level's tint and emissive power.</summary>
            Opaque,

            /// <summary>Not drawn (an invisible nav wall, a light shaft switched off).</summary>
            Hidden,

            /// <summary>Iteration 11: RrLook's solid (the kit grey's own maker-pen shader) in the level material's colour.</summary>
            Solid
        }

        /// <summary>A level-only kit material and its install stand-in (jt_lighting finding H / 4.5, jt_materials 5).</summary>
        private struct GlowSpec
        {
            public string Name;
            public GlowKind Kind;
            public string Template;
            public Color Tint;
            /// <summary>_InvFade (additive) or _EmissivePower (opaque; &lt; 0 = the template's).</summary>
            public float Value;
            /// <summary>Additive with no texture (the lasers): a white base map instead of the shaft gradient.</summary>
            public bool White;
            public string Label;

            public GlowSpec(string name, GlowKind kind, string template, Color tint, float value, bool white, string label)
            {
                Name = name;
                Kind = kind;
                Template = template;
                Tint = tint;
                Value = value;
                White = white;
                Label = label;
            }
        }

        private static readonly GlowSpec[] Glows =
        {
            // Light shafts on the install's LightArchShaft mesh; AG/Unlit-Additive-Soft (InvFade 3) or AG/Unlit-Additive (no
            // soft factor: a large InvFade), GradientHorizontal from the template.
            new GlowSpec("LightShaft_Cyan_Mat", GlowKind.Additive, null, new Color(0.0243f, 0.3489f, 0.7608f, 0.4118f), 3f, false, "shafts"),
            new GlowSpec("LightShaft_HotPink_Mat", GlowKind.Additive, null, new Color(0.8396f, 0f, 0.5832f, 0.4902f), 100f, false, "shafts"),
            new GlowSpec("LightShaft_Red_Mat", GlowKind.Additive, null, new Color(0.9623f, 0f, 0.0955f, 0.3843f), 100f, false, "shafts"),
            new GlowSpec("LightShaft_Purple_Mat", GlowKind.Additive, null, new Color(0.6966f, 0f, 1f, 0.3373f), 100f, false, "shafts"),
            new GlowSpec("LightShaft_Green_Mat", GlowKind.Additive, null, new Color(0.2599f, 0.934f, 0.4846f, 0.1176f), 100f, false, "shafts"),
            new GlowSpec("LightShaft_Blue_Stadium_Mat", GlowKind.Additive, null, new Color(0f, 0.3326f, 0.7725f, 0.2588f), 100f, false, "shafts"),
            new GlowSpec("LightShaftQuest_Orange_Mat", GlowKind.Additive, null, new Color(2f, 0.2562f, 0f, 0.256f), 3f, false, "shafts"),
            new GlowSpec("LightShaftQuest_Projector_Mat", GlowKind.Hidden, null, Color.clear, 0f, false, "shafts off"),
            // The Reception's one install-material shaft (LightShaft_Yellow_Mat's own colour), for the rebuilt shaft.
            new GlowSpec("LightShaft_Yellow_Mat", GlowKind.Additive, null, new Color(0.76f, 0.39f, 0.07f, 0.18f), 3f, false, "shafts"),
            // Red HDR laser beams (Engineering, Space): AG/Unlit-Additive-Soft (2,0,0,1), InvFade 1, no texture.
            new GlowSpec("Quest_SciFi_LaserTrail", GlowKind.Additive, null, new Color(2f, 0f, 0f, 1f), 1f, true, "lasers"),
            // Dark crates with cyan glowing logos: the install's grey sci-fi panel with glow strips in its alpha.
            new GlowSpec("PropBox_SciFi_mat", GlowKind.Opaque, "Door_SciFi_Cardboard_mat", new Color(0.096f, 0.7875f, 1f, 1f), 2.32f, false, "crates"),
            // Explosive barrels: the level texture's mean colour, its 9 % glow mask as a flat power.
            new GlowSpec("Barrel_SciFi_Explosive_Mat", GlowKind.Opaque, "Arena_Cyber_Emit_Orange_Mat", new Color(0.597f, 0.279f, 0.239f, 1f), 0.09f, false, "barrels"),
            // Iteration 11: not drawn. Its only use is a 1 m Cube in the Flight Deck, 9.7 m ahead of the arrival at 2.5 m
            // (184.37, 2.46, 57.66), and its APK shader "Shader Forge/Lighting/EmitBake" is a light-bake helper. Drawn as a
            // teal emissive box, it was the flat light-cyan square hanging in the doorway.
            new GlowSpec("SciFi_Blue_Emit_Cargo1_Mat", GlowKind.Hidden, null, Color.clear, 0f, false, "bake helpers"),
            // Black theatre curtains (the Golden Trophy's approved template).
            new GlowSpec("Curtain_Black_Mat", GlowKind.Opaque, "Curtain_Red_Mat", new Color(0.245f, 0.245f, 0.245f, 1f), -1f, false, "curtains"),
            new GlowSpec("InvisibleWall_Nav_Mat", GlowKind.Hidden, null, Color.clear, 0f, false, "nav walls"),
            // Iteration 11: the commonest level-only props that stayed the kit's light grey (jt10 materials: APK base colour x
            // texture mean). Supply's 44 cardboard cubes (Box_Cardboard_Discs_Tex srgb mean 0.72, 0.44, 0.12) on the
            // install's cardboard, the orange sci-fi dumpsters on the install's recolourable dumpster (level colour), and the
            // sagging cable bundles (CoiledWires_Tex mean 0.42; black in the footage) and dark prop boxes (Prop_Box_Tex
            // mean 0.28) as darker solids.
            new GlowSpec("Box_Cardboard_Discs_Mat", GlowKind.Opaque, "Box_Cardboard_Blank_Mat", new Color(0.8019f, 0.4721f, 0.0946f, 1f), -1f, false, "cardboard"),
            new GlowSpec("Dumpster_SciFi_Orange_mat", GlowKind.Opaque, "Dumpsters_Recolorable_mat", new Color(0.8019f, 0.3653f, 0.0794f, 1f), -1f, false, "dumpsters"),
            new GlowSpec("Coiled_Wires_Mat", GlowKind.Solid, null, new Color(0.16f, 0.16f, 0.18f, 1f), 0f, false, "cables"),
            new GlowSpec("PropBox_mat", GlowKind.Solid, null, new Color(0.28f, 0.28f, 0.29f, 1f), 0f, false, "prop boxes"),
        };

        private static int GlowIndex(string plain)
        {
            for (int i = 0; i < Glows.Length; i++)
            {
                if (string.Equals(Glows[i].Name, plain, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            // Other shafts: cyan (the commonest).
            return plain.StartsWith("LightShaft", StringComparison.OrdinalIgnoreCase) ? 0 : -1;
        }

        private sealed class Stats
        {
            public int MistPlates, MistMeshes, MistMaterials, PortalPlates, PortalMeshes, PortalMaterials, Rays;
            public int Swapped, Hidden, BatchesHidden, Rebuilt, RebuiltMeshes;
            public int Shafts, ShaftMeshes, KitShaftsHidden;
            public readonly Dictionary<string, int> Labels = new Dictionary<string, int>();
            public string Missing = "";

            public void Count(string label)
            {
                int n;
                Labels.TryGetValue(label, out n);
                Labels[label] = n + 1;
            }

            public override string ToString()
            {
                var s = "haze: mist " + MistPlates + " plates (" + MistMaterials + " materials, " + MistMeshes + " meshes), portals " + PortalPlates + " plates ("
                    + PortalMaterials + " materials, " + PortalMeshes + " meshes), " + Rays + " bake rays; kit glow";
                if (Labels.Count == 0)
                {
                    s += " none";
                }
                else
                {
                    foreach (var kv in Labels)
                    {
                        s += " " + kv.Value + " " + kv.Key + ",";
                    }

                    s += " " + Swapped + " submeshes swapped in place, " + BatchesHidden + " grey batches rebuilt as " + RebuiltMeshes + " meshes (" + Rebuilt + " records), " + Hidden + " hidden";
                }

                s += "; light shafts " + Shafts + " rebuilt from their bones (" + ShaftMeshes + " meshes), " + KitShaftsHidden + " kit shafts hidden";
                return s + (Missing.Length > 0 ? "; missing install materials:" + Missing : "");
            }
        }

        private sealed class Clock
        {
            private float _t0 = Time.realtimeSinceStartup;

            public bool Due
            {
                get { return Time.realtimeSinceStartup - _t0 > Budget; }
            }

            public void Reset()
            {
                _t0 = Time.realtimeSinceStartup;
            }
        }

        // ------------------------------------------------------------------ build

        /// <summary>Builds the room's mist and portal glows (scene point p at local p + offset), spread over frames.</summary>
        public static IEnumerator Build(RrJumbotronHull.Data h, Transform root, Vector3 offset, RrJumbotronHull.Owned owned, string name)
        {
            if (h == null || root == null || owned == null)
            {
                yield break;
            }

            float started = Time.realtimeSinceStartup;
            var st = new Stats();
            var clock = new Clock();
            var group = RrBuild.Group(root, "Haze " + name, offset).transform;

            // The room's colliders (built frames ago) must be where their transforms are for the bake rays.
            Physics.SyncTransforms();

            // Plates of one level material (the hull keys a tinted surface "name|tint"; the haze ones carry no tint).
            var mist = new Dictionary<string, List<RrJumbotronHull.Plate>>();
            var portal = new Dictionary<string, List<RrJumbotronHull.Plate>>();
            foreach (var kv in h.Plates)
            {
                int bar = kv.Key.IndexOf('|');
                string mat = bar >= 0 ? kv.Key.Substring(0, bar) : kv.Key;
                var into = mat.IndexOf("AreaFog", StringComparison.OrdinalIgnoreCase) >= 0 ? mist : mat.StartsWith("Portal_", StringComparison.OrdinalIgnoreCase) ? portal : null;
                if (into == null)
                {
                    continue;
                }

                List<RrJumbotronHull.Plate> list;
                if (!into.TryGetValue(mat, out list))
                {
                    list = new List<RrJumbotronHull.Plate>();
                    into.Add(mat, list);
                }

                list.AddRange(kv.Value);
            }

            foreach (var kv in mist)
            {
                Material template = null;
                yield return Template(MistTemplates, "AdditiveFadeDistance", owned, st, m => template = m);
                if (group == null)
                {
                    yield break;
                }

                if (template == null)
                {
                    break;
                }

                var spec = MistOf(kv.Key);
                var mat = HazeMaterial(template, "mist:" + kv.Key, kv.Key + " (Jumbotron mist on " + template.name + ")", ColourOf(h, kv.Key, new Color(0f, 0.447f, 0.553f, 1f)), 1f,
                    spec.Start, spec.End, owned);
                st.MistMaterials++;
                var bake = new Bake { Fade = spec.Edge, Power = spec.EdgePower, IntoAlpha = true, EdgeBlack = true };
                yield return Plates(kv.Value, group, mat, "Mist " + kv.Key, bake, owned, st, clock);
                if (group == null)
                {
                    yield break;
                }
            }

            foreach (var kv in portal)
            {
                // PortalFog on the mist's shader (mask and depth fade in the vertex alpha); PortalFogFresnel on the install's own.
                var spec = PortalOf(kv.Key);
                bool fresnel = spec.Fresnel;
                Material template = null;
                yield return Template(fresnel ? PortalTemplates : MistTemplates, fresnel ? "PortalFog" : "AdditiveFadeDistance", owned, st, m => template = m);
                if (template == null)
                {
                    // The other one rather than none.
                    fresnel = !fresnel;
                    yield return Template(fresnel ? PortalTemplates : MistTemplates, fresnel ? "PortalFog" : "AdditiveFadeDistance", owned, st, m => template = m);
                }

                if (group == null)
                {
                    yield break;
                }

                if (template == null)
                {
                    continue;
                }

                var col = ColourOf(h, kv.Key, new Color(0f, 0.9172f, 1f, 1f));
                var mat = fresnel ? PortalMaterial(template, kv.Key, col, spec, owned)
                    : HazeMaterial(template, "portal:" + kv.Key, kv.Key + " (Jumbotron portal on " + template.name + ")", col, col.a, spec.Start, spec.End, owned);
                st.PortalMaterials++;
                var bake = new Bake { TwoSided = true, Fade = spec.Fade, Power = spec.Power, IntoAlpha = !fresnel, Mask = !fresnel, U = spec.U, V = spec.V };
                yield return Plates(kv.Value, group, mat, "Portal " + kv.Key, bake, owned, st, clock);
                if (group == null)
                {
                    yield break;
                }
            }

            yield return KitGlow(root, name, owned, st, clock);
            if (group == null)
            {
                yield break;
            }

            // Iteration 11: the light shafts as the level poses them (the kit's unskinned ones are hidden in KitGlow).
            yield return Shafts(group, name, owned, st);
            if (group == null)
            {
                yield break;
            }

            RrLog.Info(Area, name + ": " + st + " (" + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0") + " ms)");
        }

        private static MistSpec MistOf(string mat)
        {
            foreach (var m in Mists)
            {
                if (string.Equals(m.Name, mat, StringComparison.OrdinalIgnoreCase))
                {
                    return m;
                }
            }

            return DefaultMist;
        }

        private static PortalSpec PortalOf(string mat)
        {
            foreach (var p in Portals)
            {
                if (string.Equals(p.Name, mat, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            return DefaultPortal;
        }

        private static Color ColourOf(RrJumbotronHull.Data h, string mat, Color fallback)
        {
            RrJumbotronHull.MatInfo mi;
            return h.Mats.TryGetValue(mat, out mi) ? mi.Col : fallback;
        }

        // ------------------------------------------------------------------ materials

        /// <summary>The first install material of the list that loads with the expected shader (fixed, shared; clone before use).</summary>
        private static IEnumerator Template(string[] names, string shaderPart, RrJumbotronHull.Owned owned, Stats st, Action<Material> done)
        {
            string key = "haze:template:" + names[0];
            Material m;
            if (owned.Materials.TryGetValue(key, out m))
            {
                done(m);
                yield break;
            }

            var assets = RrAssets.Instance;
            m = null;
            if (assets != null)
            {
                foreach (var n in names)
                {
                    RrEntry e;
                    if (!assets.TryFind(n, 21, out e))
                    {
                        continue;
                    }

                    Material loaded = null;
                    yield return assets.LoadRoutine<Material>(n, 21, x => loaded = x);
                    var f = loaded != null ? RrMaterials.Fix(loaded) : null;
                    // A shader the client cannot run comes back as URP Lit: an opaque haze box is worse than none.
                    if (f != null && f.shader != null && (shaderPart == null || f.shader.name.IndexOf(shaderPart, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        m = f;
                        break;
                    }
                }
            }

            if (m == null)
            {
                st.Missing += " " + names[0];
                RrLog.WarnOnce(Area, "haze:" + names[0], "haze: no usable install material " + string.Join("/", names) + (shaderPart != null ? " (" + shaderPart + ")" : "") + "; left out");
            }
            else
            {
                // Shared by the rooms; the template itself is the install's (RrAssets frees it), not ours.
                owned.Materials[key] = m;
            }

            done(m);
        }

        private static void SetFloat(Material m, string prop, float v)
        {
            if (m.HasProperty(prop))
            {
                m.SetFloat(prop, v);
            }
        }

        private static void SetMap(Material m, Texture tex)
        {
            foreach (var prop in new[] { "_BaseMap", "_MainTex" })
            {
                if (m.HasProperty(prop))
                {
                    m.SetTexture(prop, tex);
                    m.SetTextureScale(prop, Vector2.one);
                    m.SetTextureOffset(prop, Vector2.zero);
                }
            }
        }

        /// <summary>
        /// An additive haze (FogSin mist, PortalFog glow) on AdditiveFadeDistance (see the class summary): the level colour
        /// (as serialized) x strength added, camera fade from end to start distance.
        /// </summary>
        private static Material HazeMaterial(Material template, string id, string name, Color col, float strength, float start, float end, RrJumbotronHull.Owned owned)
        {
            string key = "haze:" + id;
            Material m;
            if (owned.Materials.TryGetValue(key, out m) && m != null)
            {
                return m;
            }

            m = new Material(template) { name = name };
            owned.Things.Add(m);
            SetMap(m, Texture2D.whiteTexture);
            // Linear values, as the shader sees them: 2 x tint x alpha = the level's (linear) colour x strength.
            var lin = new Color(col.r, col.g, col.b, 1f).linear;
            float k = strength / (2f * HazeAlpha);
            if (m.HasProperty("_TintColor"))
            {
                m.SetVector("_TintColor", new Vector4(lin.r * k, lin.g * k, lin.b * k, HazeAlpha));
            }

            SetFloat(m, "_StartDistance", start);
            SetFloat(m, "_EndDistance", end);
            // The shader's own depth softness always 1 (baked into the vertex alpha instead).
            SetFloat(m, "_SoftParticles", Tiny);
            m.DisableKeyword("_DEPTH_TEXTURE");
            m.renderQueue = 3050;
            owned.Materials[key] = m;
            return m;
        }

        /// <summary>The level's PortalFog numbers on the install's PortalFogFresnel (see the class summary).</summary>
        private static Material PortalMaterial(Material template, string level, Color col, PortalSpec spec, RrJumbotronHull.Owned owned)
        {
            string key = "haze:portal:" + level;
            Material m;
            if (owned.Materials.TryGetValue(key, out m) && m != null)
            {
                return m;
            }

            m = new Material(template) { name = level + " (Jumbotron portal on " + template.name + ")" };
            owned.Things.Add(m);
            // As Rec Room's own material: the serialized colour (converted to linear by Unity), alpha as it is.
            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", col);
            }

            SetFloat(m, "_StartDistance", spec.Start);
            SetFloat(m, "_EndDistance", spec.End);
            SetFloat(m, "_UValue", spec.U);
            SetFloat(m, "_VValue", spec.V);
            // The shader's depth fade always 1 (baked into the vertex colour instead).
            SetFloat(m, "_FadeDistance", Tiny);
            SetFloat(m, "_DistancePower", 1f);
            SetFloat(m, "_MobileDistanceEstimate", 10000f);
            m.EnableKeyword("_VERTEX_COLOR");
            m.DisableKeyword("_DEPTH_TEXTURE");
            m.renderQueue = 3050;
            owned.Materials[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ plates

        private sealed class Buf
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();
        }

        /// <summary>What a plate's vertices carry (see <see cref="AddPlate"/>).</summary>
        private struct Bake
        {
            /// <summary>Front and flipped back copy (portals), else the front only (mist, culled like FogSin).</summary>
            public bool TwoSided;
            /// <summary>Into the vertex alpha (AdditiveFadeDistance), else into the vertex colour (PortalFogFresnel).</summary>
            public bool IntoAlpha;
            /// <summary>FogSin's lerp from its black _EdgeColor over the first metre.</summary>
            public bool EdgeBlack;
            /// <summary>PortalFog's saturate((u + U)(v + V)) (PortalFogFresnel computes its own).</summary>
            public bool Mask;
            public float Fade, Power, U, V;
        }

        /// <summary>
        /// One-sided mist plates or two-sided portal plates, each vertex baked with the fade of the gap behind its face,
        /// merged per material into meshes of at most <see cref="MaxVertices"/>.
        /// </summary>
        private static IEnumerator Plates(List<RrJumbotronHull.Plate> plates, Transform group, Material mat, string label, Bake bake, RrJumbotronHull.Owned owned, Stats st, Clock clock)
        {
            bool portal = bake.TwoSided;
            var buf = new Buf();
            int part = 0;
            Matrix4x4 toWorld = group.localToWorldMatrix;
            foreach (var p in plates)
            {
                if (clock.Due)
                {
                    yield return null;
                    clock.Reset();
                    if (group == null)
                    {
                        yield break;
                    }

                    toWorld = group.localToWorldMatrix;
                }

                if (p.Hx <= 0f || p.Hy <= 0f)
                {
                    continue;
                }

                int nx = Mathf.Clamp(Mathf.CeilToInt(2f * p.Hx / Cell), 1, 32);
                int ny = Mathf.Clamp(Mathf.CeilToInt(2f * p.Hy / Cell), 1, 32);
                int per = (nx + 1) * (ny + 1) * (portal ? 2 : 1);
                if (buf.V.Count + per > MaxVertices)
                {
                    Flush(buf, group, mat, label, part++, owned, st, portal);
                }

                AddPlate(buf, p, nx, ny, toWorld, bake, st);
                if (portal)
                {
                    st.PortalPlates++;
                }
                else
                {
                    st.MistPlates++;
                }
            }

            Flush(buf, group, mat, label, part, owned, st, portal);
        }

        /// <summary>
        /// One plate cut into nx x ny cells; portal plates twice (front and flipped back). Per vertex the gap behind the face
        /// (a ray along the face's -normal, <see cref="MaxGap"/> when open; / <see cref="GrazingSin"/> on a horizontal face)
        /// gives saturate(gap / fade)^power (x FogSin's edge lerp, x PortalFog's mask): the vertex colour of a
        /// PortalFogFresnel face, else the vertex alpha.
        /// </summary>
        private static void AddPlate(Buf buf, RrJumbotronHull.Plate p, int nx, int ny, Matrix4x4 toWorld, Bake bake, Stats st)
        {
            bool portal = bake.TwoSided;
            Vector3 ax = p.Q * Vector3.right, ay = p.Q * Vector3.up, n = p.Q * Vector3.forward;
            int w = nx + 1, count = w * (ny + 1);
            var pos = new Vector3[count];
            var uv = new Vector2[count];
            // The mask's v runs from the plate's lower edge up; a horizontal sheet has mask 1 (uv 1,1).
            bool flat = Mathf.Abs(n.y) > 0.7f;
            bool vAlongY = Mathf.Abs(ay.y) >= Mathf.Abs(ax.y);
            bool up = vAlongY ? ay.y >= 0f : ax.y >= 0f;
            for (int j = 0; j <= ny; j++)
            {
                for (int i = 0; i <= nx; i++)
                {
                    float fx = (float)i / nx, fy = (float)j / ny;
                    int k = j * w + i;
                    pos[k] = p.C + ax * (p.Hx * (2f * fx - 1f)) + ay * (p.Hy * (2f * fy - 1f));
                    if (flat)
                    {
                        uv[k] = Vector2.one;
                    }
                    else if (vAlongY)
                    {
                        uv[k] = new Vector2(fx, up ? fy : 1f - fy);
                    }
                    else
                    {
                        uv[k] = new Vector2(fy, up ? fx : 1f - fx);
                    }
                }
            }

            float inv = bake.Fade > Tiny ? 1f / bake.Fade : 1f / Tiny;
            for (int f = 0; f < (portal ? 2 : 1); f++)
            {
                var nn = f == 0 ? n : -n;
                var behind = toWorld.MultiplyVector(-nn).normalized;
                int b = buf.V.Count;
                for (int k = 0; k < count; k++)
                {
                    float gap = Gap(toWorld.MultiplyPoint3x4(pos[k]), behind);
                    st.Rays++;
                    if (flat)
                    {
                        // Rec Room's gap is along the view ray, which meets a horizontal face at a grazing angle.
                        gap = Mathf.Min(MaxGap, gap / GrazingSin);
                    }

                    float x = Mathf.Pow(Mathf.Clamp01(gap * inv), bake.Power);
                    if (bake.EdgeBlack)
                    {
                        // FogSin also darkens towards its _EdgeColor (black, w 1 m) right at the geometry.
                        x *= Mathf.Clamp01(gap);
                    }

                    if (bake.Mask)
                    {
                        x *= Mathf.Clamp01((uv[k].x + bake.U) * (uv[k].y + bake.V));
                    }

                    buf.V.Add(pos[k]);
                    buf.N.Add(nn);
                    buf.Uv.Add(uv[k]);
                    buf.C.Add(bake.IntoAlpha ? new Color(1f, 1f, 1f, x) : new Color(x, x, x, 1f));
                }

                for (int j = 0; j < ny; j++)
                {
                    for (int i = 0; i < nx; i++)
                    {
                        int a = b + j * w + i, c = a + w + 1;
                        // The hull's winding: (a, a + 1, c) faces along +normal.
                        if (f == 0)
                        {
                            buf.T.Add(a);
                            buf.T.Add(a + 1);
                            buf.T.Add(c);
                            buf.T.Add(a);
                            buf.T.Add(c);
                            buf.T.Add(a + w);
                        }
                        else
                        {
                            buf.T.Add(a);
                            buf.T.Add(c);
                            buf.T.Add(a + 1);
                            buf.T.Add(a);
                            buf.T.Add(a + w);
                            buf.T.Add(c);
                        }
                    }
                }
            }
        }

        /// <summary>Distance to the room's geometry from a point along a direction (environment colliders only).</summary>
        private static float Gap(Vector3 from, Vector3 dir)
        {
            RaycastHit hit;
            if (Physics.Raycast(from + dir * 0.01f, dir, out hit, MaxGap, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore))
            {
                return hit.distance + 0.01f;
            }

            return MaxGap;
        }

        private static void Flush(Buf buf, Transform group, Material mat, string label, int part, RrJumbotronHull.Owned owned, Stats st, bool portal)
        {
            if (buf.V.Count == 0 || group == null || mat == null)
            {
                Clear(buf);
                return;
            }

            string n = label + (part > 0 ? " " + part : "");
            var mesh = new Mesh { name = "Jumbotron haze " + n };
            mesh.SetVertices(buf.V);
            mesh.SetNormals(buf.N);
            mesh.SetUVs(0, buf.Uv);
            mesh.SetColors(buf.C);
            mesh.SetTriangles(buf.T, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            owned.Things.Add(mesh);
            var go = new GameObject(n);
            go.layer = RrLayers.Environment;
            go.transform.SetParent(group, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            Quiet(r);
            if (portal)
            {
                st.PortalMeshes++;
            }
            else
            {
                st.MistMeshes++;
            }

            Clear(buf);
        }

        private static void Clear(Buf buf)
        {
            buf.V.Clear();
            buf.N.Clear();
            buf.Uv.Clear();
            buf.C.Clear();
            buf.T.Clear();
        }

        /// <summary>A glow / haze renderer: no shadows either way, no probes.</summary>
        private static void Quiet(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ------------------------------------------------------------------ kit glow

        /// <summary>A level-only kit material's install clone (shared by the rooms through owned.Materials), or null.</summary>
        private static IEnumerator GlowMaterial(GlowSpec g, RrJumbotronHull.Owned owned, Stats st, Action<Material> done)
        {
            string key = "haze:glow:" + g.Name;
            Material m;
            if (owned.Materials.TryGetValue(key, out m))
            {
                done(m);
                yield break;
            }

            if (g.Kind == GlowKind.Solid)
            {
                // RrLook's shared solid (not ours to free).
                m = RrLook.Solid(g.Tint);
                if (m != null)
                {
                    owned.Materials[key] = m;
                }

                done(m);
                yield break;
            }

            Material template = null;
            if (g.Kind == GlowKind.Additive)
            {
                yield return Template(ShaftTemplates, "Unlit-Additive", owned, st, x => template = x);
            }
            else
            {
                yield return Template(new[] { g.Template }, "AG/", owned, st, x => template = x);
            }

            m = null;
            if (template != null)
            {
                m = new Material(template) { name = g.Name + " (Jumbotron glow on " + template.name + ")" };
                owned.Things.Add(m);
                if (m.HasProperty("_BaseColor"))
                {
                    m.SetColor("_BaseColor", g.Tint);
                }

                if (g.Kind == GlowKind.Additive)
                {
                    SetFloat(m, "_InvFade", g.Value);
                    if (g.White)
                    {
                        SetMap(m, Texture2D.whiteTexture);
                    }

                    m.DisableKeyword("_DEPTH_TEXTURE");
                }
                else if (g.Value >= 0f)
                {
                    SetFloat(m, "_EmissivePower", g.Value);
                }

                owned.Materials[key] = m;
            }

            done(m);
        }

        private sealed class Bucket
        {
            public Material Material;
            public bool Additive;
            public readonly List<CombineInstance> Items = new List<CombineInstance>();
            public int Vertices;
            /// <summary>Whole source meshes (CombineMeshes may copy all of a mesh's vertices for one submesh, as the kit counts).</summary>
            public int FullVertices;
            public int Made;
        }

        /// <summary>
        /// The kit's level-only glowing props (see the class summary). Separate kit objects get the install clone in place;
        /// the kit's merged grey batches (level-only materials share one grey, so a shaft shares its batch with a poster) are
        /// hidden and their records' grey submeshes rebuilt: the glowing ones in their clone, the others in the same grey.
        /// </summary>
        private static IEnumerator KitGlow(Transform root, string name, RrJumbotronHull.Owned owned, Stats st, Clock clock)
        {
            var kit = FindKit(root, name);
            if (kit == null)
            {
                yield break;
            }

            HideKitShafts(kit, st);
            var d = kit.Result.Data;
            var missing = new HashSet<string>(kit.Result.MissingMaterials);
            int count = d.Materials.Count;
            var grey = new bool[count];
            var spec = new int[count];
            var used = new bool[Glows.Length];
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                spec[i] = -1;
                string m = d.Materials[i];
                if (string.IsNullOrEmpty(m))
                {
                    grey[i] = true;
                    continue;
                }

                bool unknown = m[0] == '?';
                if (!unknown && !missing.Contains(m))
                {
                    continue;
                }

                grey[i] = true;
                spec[i] = GlowIndex(unknown ? m.Substring(1) : m);
                if (spec[i] >= 0)
                {
                    used[spec[i]] = true;
                    any = true;
                }
            }

            if (!any)
            {
                yield break;
            }

            var mats = new Material[Glows.Length];
            for (int g = 0; g < Glows.Length; g++)
            {
                if (!used[g] || Glows[g].Kind == GlowKind.Hidden)
                {
                    continue;
                }

                Material m = null;
                yield return GlowMaterial(Glows[g], owned, st, x => m = x);
                if (kit == null)
                {
                    yield break;
                }

                mats[g] = m;
            }

            // Records with a grey submesh, by position (a separate kit object sits at its record's position).
            var cand = new List<int>();
            var byPos = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < d.Renderers.Count; i++)
            {
                var r = d.Renderers[i];
                if (!HasGrey(d, r, grey))
                {
                    continue;
                }

                cand.Add(i);
                var key = Mm(r.Position);
                List<int> list;
                if (!byPos.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    byPos.Add(key, list);
                }

                list.Add(i);
            }

            var handled = new HashSet<int>();
            var batches = new List<MeshRenderer>();
            Material fallback = null;
            var scan = kit.transform.Find("Renderers");
            if (scan == null)
            {
                scan = kit.transform;
            }

            foreach (var mr in scan.GetComponentsInChildren<MeshRenderer>(true))
            {
                var shared = mr.sharedMaterials;
                int at = -1;
                for (int s = 0; s < shared.Length && at < 0; s++)
                {
                    if (IsFallback(shared[s]))
                    {
                        at = s;
                    }
                }

                if (at < 0)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = shared[at];
                }

                string goName = mr.gameObject.name;
                if (goName.StartsWith("Batch ", StringComparison.Ordinal))
                {
                    batches.Add(mr);
                    continue;
                }

                List<int> recs;
                if (IsShaft(goName) || goName.StartsWith("Stand-ins ", StringComparison.Ordinal) || !byPos.TryGetValue(Mm(mr.transform.localPosition), out recs))
                {
                    continue;
                }

                int ri = -1;
                foreach (int x in recs)
                {
                    if (!handled.Contains(x) && (ri < 0 || d.MeshName(d.Renderers[x].Mesh) == goName))
                    {
                        ri = x;
                    }
                }

                if (ri >= 0)
                {
                    handled.Add(ri);
                    Swap(mr, shared, d, d.Renderers[ri], grey, spec, mats, st);
                }
            }

            if (batches.Count == 0 || fallback == null)
            {
                yield break;
            }

            // Merged grey: rebuild what the kit put in those batches (same filters as the kit: its batch boxes stand in
            // for the scene filter, higher LODs and duplicates dropped; mirrored / unreadable / big meshes were separate).
            var boxes = new List<Bounds>();
            foreach (var b in batches)
            {
                var bb = b.bounds;
                bb.Expand(0.5f);
                boxes.Add(bb);
            }

            var names = new HashSet<string>();
            foreach (var n in d.Meshes)
            {
                if (n != null)
                {
                    names.Add(n);
                }
            }

            var assets = RrAssets.Instance;
            var meshes = new Dictionary<int, Mesh>();
            var placed = new List<int>();
            var buckets = new Dictionary<long, Bucket>();
            var order = new List<Bucket>();
            Matrix4x4 kitWorld = kit.transform.localToWorldMatrix;
            foreach (int ri in cand)
            {
                if (clock.Due)
                {
                    yield return null;
                    clock.Reset();
                    if (kit == null)
                    {
                        yield break;
                    }

                    kitWorld = kit.transform.localToWorldMatrix;
                }

                if (handled.Contains(ri))
                {
                    continue;
                }

                var r = d.Renderers[ri];
                string mn = d.MeshName(r.Mesh);
                if (mn == null || IsHigherLod(mn, names) || r.Scale.x * r.Scale.y * r.Scale.z < 0f)
                {
                    continue;
                }

                if (IsShaft(mn))
                {
                    // Rebuilt from its bones (Shafts); the unskinned mesh is not drawn.
                    st.KitShaftsHidden++;
                    continue;
                }

                Mesh mesh;
                if (!meshes.TryGetValue(r.Mesh, out mesh))
                {
                    Mesh loaded = null;
                    RrEntry e;
                    if (assets != null && assets.TryFind(mn, 43, out e))
                    {
                        yield return assets.LoadRoutine<Mesh>(mn, 43, x => loaded = x);
                        if (kit == null)
                        {
                            yield break;
                        }
                    }

                    mesh = loaded;
                    meshes[r.Mesh] = mesh;
                }

                if (mesh == null || !mesh.isReadable || mesh.vertexCount > MaxVertices)
                {
                    continue;
                }

                var trs = Matrix4x4.TRS(r.Position, r.Rotation, r.Scale);
                if (!Inside(boxes, kitWorld.MultiplyPoint3x4(trs.MultiplyPoint3x4(mesh.bounds.center))) || Duplicate(d, placed, r))
                {
                    continue;
                }

                int subs = mesh.subMeshCount;
                int n = r.MaterialCount == 0 ? subs : Math.Min(subs, r.MaterialCount);
                bool triangles = n > 0;
                for (int s = 0; s < n; s++)
                {
                    triangles &= mesh.GetTopology(s) == MeshTopology.Triangles;
                }

                if (!triangles)
                {
                    continue;
                }

                placed.Add(ri);
                int mask = Mask(mesh);
                string label = null;
                for (int s = 0; s < n; s++)
                {
                    int mi;
                    if (!GreyAt(d, r, s, grey, out mi))
                    {
                        continue;
                    }

                    int g = mi >= 0 ? spec[mi] : -1;
                    var target = fallback;
                    bool additive = false;
                    if (g >= 0 && Glows[g].Kind == GlowKind.Hidden)
                    {
                        label = label ?? Glows[g].Label;
                        st.Hidden++;
                        continue;
                    }

                    if (g >= 0 && mats[g] != null)
                    {
                        target = mats[g];
                        additive = Glows[g].Kind == GlowKind.Additive;
                        label = label ?? Glows[g].Label;
                    }

                    long key = ((long)(uint)target.GetInstanceID() << 32) | (uint)mask;
                    Bucket bucket;
                    if (!buckets.TryGetValue(key, out bucket))
                    {
                        bucket = new Bucket { Material = target, Additive = additive };
                        buckets.Add(key, bucket);
                        order.Add(bucket);
                    }

                    int sv = mesh.GetSubMesh(s).vertexCount;
                    if (bucket.Items.Count > 0 && bucket.Vertices + sv > MaxVertices)
                    {
                        Combine(bucket, kit.transform, batches[0], owned, st);
                    }

                    bucket.Items.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = trs });
                    bucket.Vertices += sv;
                    bucket.FullVertices += mesh.vertexCount;
                }

                st.Rebuilt++;
                if (label != null)
                {
                    st.Count(label);
                }
            }

            if (kit == null)
            {
                yield break;
            }

            // Swap in one frame: the kit's grey batches off, ours on.
            foreach (var b in batches)
            {
                b.enabled = false;
                st.BatchesHidden++;
            }

            foreach (var bucket in order)
            {
                Combine(bucket, kit.transform, batches[0], owned, st);
            }
        }

        /// <summary>The kit root this room's area built under root ("Rr Scene &lt;name&gt;"), with its scene data, or null.</summary>
        private static RrSceneKitRoot FindKit(Transform root, string name)
        {
            RrSceneKitRoot exact = null, any = null;
            for (int i = 0; i < root.childCount; i++)
            {
                var k = root.GetChild(i).GetComponent<RrSceneKitRoot>();
                if (k == null || k.Result == null || k.Result.Data == null)
                {
                    continue;
                }

                any = k;
                if (k.gameObject.name == "Rr Scene " + name || k.Result.Scene == name)
                {
                    exact = k;
                }
            }

            return exact != null ? exact : any;
        }

        /// <summary>The kit's grey stand-in for materials it could not load (RrSceneKit.MaterialOf: RrLook.Solid 0.62, 0.62, 0.64).</summary>
        private static bool IsFallback(Material m)
        {
            return m != null && m.name.StartsWith("Rr Solid", StringComparison.Ordinal) && m.name.IndexOf("9E9EA3", StringComparison.Ordinal) >= 0;
        }

        private static bool HasGrey(RrSceneData d, RrSceneData.RendererRecord r, bool[] grey)
        {
            if (r.MaterialCount == 0)
            {
                return true;
            }

            for (int s = 0; s < r.MaterialCount; s++)
            {
                int mi;
                if (GreyAt(d, r, s, grey, out mi))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether submesh s of a record drew in the kit's grey (as RrSceneKit.MaterialOf); mi = its material index or -1.</summary>
        private static bool GreyAt(RrSceneData d, RrSceneData.RendererRecord r, int s, bool[] grey, out int mi)
        {
            mi = -1;
            if (s >= r.MaterialCount)
            {
                return true;
            }

            int at = r.MaterialStart + s;
            int m = at >= 0 && at < d.MaterialIndices.Count ? d.MaterialIndices[at] : -1;
            if (m < 0 || m >= grey.Length)
            {
                return true;
            }

            mi = m;
            return grey[m];
        }

        /// <summary>A separate kit object: its glowing submeshes get their clone; all-hidden objects are switched off.</summary>
        private static void Swap(MeshRenderer mr, Material[] shared, RrSceneData d, RrSceneData.RendererRecord r, bool[] grey, int[] spec, Material[] mats, Stats st)
        {
            bool changed = false, additive = true;
            int hidden = 0;
            string label = null;
            for (int s = 0; s < shared.Length; s++)
            {
                int mi;
                if (!IsFallback(shared[s]) || !GreyAt(d, r, s, grey, out mi))
                {
                    additive = false;
                    continue;
                }

                int g = mi >= 0 ? spec[mi] : -1;
                if (g >= 0 && Glows[g].Kind == GlowKind.Hidden)
                {
                    hidden++;
                    label = label ?? Glows[g].Label;
                    continue;
                }

                if (g < 0 || mats[g] == null)
                {
                    additive = false;
                    continue;
                }

                shared[s] = mats[g];
                changed = true;
                additive &= Glows[g].Kind == GlowKind.Additive;
                label = label ?? Glows[g].Label;
                st.Swapped++;
            }

            if (hidden > 0 && hidden == shared.Length)
            {
                mr.enabled = false;
                st.Hidden++;
            }
            else if (changed)
            {
                mr.sharedMaterials = shared;
                if (additive)
                {
                    Quiet(mr);
                }
            }

            if (label != null)
            {
                st.Count(label);
            }
        }

        private static void Combine(Bucket b, Transform kitRoot, MeshRenderer like, RrJumbotronHull.Owned owned, Stats st)
        {
            if (b.Items.Count == 0 || kitRoot == null)
            {
                return;
            }

            var mesh = new Mesh { name = "Jumbotron glow " + b.Material.name + " " + b.Made };
            if (b.Vertices > 65000 || b.FullVertices > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            try
            {
                mesh.CombineMeshes(b.Items.ToArray(), true, true, false);
            }
            catch (Exception e)
            {
                RrLog.WarnOnce(Area, "haze:combine:" + b.Material.name, "haze: merging " + b.Items.Count + " kit renderers of " + b.Material.name + " failed (" + e.GetType().Name + ": " + e.Message + "); they are left out");
                UnityEngine.Object.Destroy(mesh);
                b.Items.Clear();
                b.Vertices = 0;
                b.FullVertices = 0;
                return;
            }

            mesh.UploadMeshData(true);
            owned.Things.Add(mesh);
            // In the kit root's own space (the records' positions), next to its renderers; destroyed with it.
            var group = kitRoot.Find("Haze glow");
            if (group == null)
            {
                group = RrBuild.Group(kitRoot, "Haze glow").transform;
                group.gameObject.layer = RrLayers.Environment;
            }

            var go = new GameObject("Glow " + b.Material.name + " " + b.Made);
            go.layer = RrLayers.Environment;
            go.transform.SetParent(group, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = b.Material;
            if (b.Additive)
            {
                Quiet(r);
            }
            else
            {
                r.shadowCastingMode = like != null ? like.shadowCastingMode : ShadowCastingMode.On;
                r.lightProbeUsage = LightProbeUsage.Off;
            }

            st.RebuiltMeshes++;
            b.Made++;
            b.Items.Clear();
            b.Vertices = 0;
            b.FullVertices = 0;
        }

        private static Vector3Int Mm(Vector3 p)
        {
            return new Vector3Int(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));
        }

        private static bool Inside(List<Bounds> boxes, Vector3 p)
        {
            foreach (var b in boxes)
            {
                if (b.Contains(p))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>As the kit: the same mesh within 2 cm, the same rotation and scale, is built once.</summary>
        private static bool Duplicate(RrSceneData d, List<int> placed, RrSceneData.RendererRecord r)
        {
            foreach (int i in placed)
            {
                var o = d.Renderers[i];
                if (o.Mesh == r.Mesh && (o.Position - r.Position).sqrMagnitude < 0.0004f && Mathf.Abs(Quaternion.Dot(o.Rotation, r.Rotation)) > 0.99991f
                    && (o.Scale - r.Scale).sqrMagnitude <= 0.0001f * Mathf.Max(0.01f, r.Scale.sqrMagnitude))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>As the kit: "Name_LOD2" (or "NameLOD2") whose "..._LOD0" is in the table.</summary>
        private static bool IsHigherLod(string name, HashSet<string> names)
        {
            if (name == null || name.Length < 5)
            {
                return false;
            }

            int d = name.Length - 1;
            char c = name[d];
            if (c < '1' || c > '7' || string.CompareOrdinal(name, d - 3, "LOD", 0, 3) != 0)
            {
                return false;
            }

            return names.Contains(name.Substring(0, d) + "0");
        }

        /// <summary>As the kit: the vertex attributes a merged mesh must agree on.</summary>
        private static int Mask(Mesh mesh)
        {
            int m = 0;
            if (mesh.HasVertexAttribute(VertexAttribute.Normal)) m |= 1;
            if (mesh.HasVertexAttribute(VertexAttribute.Tangent)) m |= 2;
            if (mesh.HasVertexAttribute(VertexAttribute.Color)) m |= 4;
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) m |= 8;
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord1)) m |= 16;
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord2)) m |= 32;
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord3)) m |= 64;
            return m;
        }

        // ------------------------------------------------------------------ light shafts (iteration 11)

        public const string ShaftMesh = "LightArchShaft";

        /// <summary>Segments along a rebuilt shaft, and vertices across each of its four sides.</summary>
        private const int ShaftRows = 10, ShaftCols = 5;

        /// <summary>
        /// Every light shaft the levels draw (48 in 11 rooms; the Flight Deck's two projector shafts, alpha 0, are left out).
        /// A shaft is the install's skinned mesh LightArchShaft posed by a rig of 13 LightArchBone_* bones. Each line: scene,
        /// level material, the shaft's position (its R record), then the world positions of its eight corner bones:
        /// Root_Bot_R, Root_Bot_L, Root_Top_R, Root_Top_L (the lamp's aperture) and Bot_R, Bot_L, Top_R, Top_L (where the
        /// light lands). Measured from the Android levels' transforms (NSP/jt11/shafts.pl over the level dumps; numbers
        /// only, DN/iter11/layout.md). Iteration 13: two apertures follow their lamps, which the support test moved onto
        /// their supports (DN/iter13/jumbotron.md): the Concession's exit-door lamp 0.24 m up, the Stadium's
        /// BossCinematic [StageLight] (8) 0.17 m down.
        /// </summary>
        private static readonly string[] ShaftData =
        {
            "Jumbotron_Foyer LightShaft_Cyan_Mat 32.205 -3.040 66.741 32.205 -3.040 66.741 32.280 -3.040 66.507 32.205 -2.828 66.741 32.280 -2.828 66.507 21.057 -5.853 64.672 22.385 -5.853 60.533 20.807 -1.884 65.477 22.471 -1.884 60.296",
            "Jumbotron_Foyer LightShaft_HotPink_Mat 32.322 1.428 50.745 32.322 1.428 50.745 32.276 1.432 50.504 32.266 1.632 50.760 32.220 1.636 50.518 20.167 -5.345 55.810 19.415 -5.326 50.103 14.422 0.550 57.708 14.293 1.564 50.618",
            "Jumbotron_Foyer LightShaft_Cyan_Mat 31.537 -3.040 43.131 31.537 -3.040 43.131 31.415 -3.040 42.917 31.537 -2.828 43.131 31.415 -2.828 42.917 22.468 -5.950 49.926 20.361 -5.950 46.245 22.884 -1.524 50.636 20.247 -1.524 46.028",
            "Jumbotron_Armory LightShaft_Green_Mat 68.417 0.312 31.217 68.417 0.312 31.217 68.501 0.312 30.986 68.417 0.523 31.217 68.501 0.523 30.986 57.409 -1.985 28.582 58.596 -1.985 25.311 57.187 1.635 29.219 58.673 1.635 25.124",
            "Jumbotron_Battlefield1 LightShaft_Cyan_Mat 186.292 1.709 -49.434 186.292 1.709 -49.434 186.270 1.709 -49.189 186.292 1.921 -49.434 186.270 1.921 -49.189 195.933 -3.550 -50.537 195.516 -3.550 -45.918 196.006 1.820 -51.434 195.483 1.820 -45.651",
            "Jumbotron_Battlefield1 LightShaft_HotPink_Mat 182.324 -0.601 -82.310 182.324 -0.601 -82.310 182.080 -0.604 -82.345 182.312 -0.401 -82.241 182.068 -0.404 -82.276 182.682 -3.776 -81.555 180.259 -3.770 -81.995 182.877 -1.393 -76.897 177.402 -0.480 -76.475",
            "Jumbotron_Battlefield1 LightShaft_Cyan_Mat 185.640 -1.249 -65.267 185.640 -1.249 -65.267 185.451 -1.249 -65.425 185.640 -1.037 -65.267 185.451 -1.037 -65.425 181.138 -5.657 -54.380 175.554 -5.657 -59.031 182.226 2.497 -53.484 175.236 2.497 -59.307",
            "Jumbotron_Hallway1 LightShaft_Red_Mat -90.023 0.461 113.093 -90.023 0.461 113.093 -89.986 0.457 112.849 -90.091 0.661 113.079 -90.054 0.656 112.835 -94.254 -4.200 114.906 -92.851 -4.193 109.674 -99.772 0.442 114.467 -100.222 0.695 107.389",
            "Jumbotron_Hallway1 LightShaft_Cyan_Mat -102.675 -1.351 109.559 -102.675 -1.351 109.559 -102.452 -1.351 109.663 -102.664 -1.142 109.534 -102.440 -1.142 109.638 -99.258 -5.097 99.181 -96.103 -5.097 100.649 -99.674 -1.507 98.476 -95.724 -1.507 100.313",
            "Jumbotron_Hallway1 LightShaft_Red_Mat -37.463 4.013 120.531 -37.463 4.013 120.531 -37.449 4.007 120.285 -37.531 4.212 120.522 -37.518 4.207 120.276 -42.809 -0.651 121.787 -43.050 -0.687 117.894 -47.793 3.437 121.801 -48.589 3.215 116.768",
            "Jumbotron_Hallway2 LightShaft_Red_Mat 74.802 3.521 70.191 74.802 3.521 70.191 74.758 3.528 70.433 74.935 3.684 70.210 74.891 3.692 70.452 77.504 -1.913 67.906 77.059 -1.963 75.119 79.541 2.742 69.509 79.908 1.726 76.017",
            "Jumbotron_Hallway2 LightShaft_Cyan_Mat 100.230 0.607 72.893 100.230 0.607 72.893 100.461 0.607 72.808 100.230 0.818 72.893 100.461 0.818 72.808 97.620 -2.277 65.308 102.313 -2.277 63.579 96.712 1.343 65.650 102.588 1.343 63.487",
            "Jumbotron_Hallway2 LightShaft_Red_Mat 38.068 5.732 122.965 38.062 5.694 122.975 38.305 5.663 122.999 38.093 5.896 122.921 38.336 5.865 122.945 35.484 1.437 119.489 41.697 2.654 119.544 34.005 5.750 118.193 42.027 6.331 118.542",
            "Jumbotron_Reception LightShaft_Cyan_Mat 230.495 1.013 9.730 230.495 1.013 9.730 230.518 1.013 9.975 230.495 1.224 9.730 230.518 1.224 9.975 241.625 -5.561 7.974 242.055 -5.561 12.490 241.533 0.970 7.099 242.071 0.970 12.753",
            "Jumbotron_Reception LightShaft_Cyan_Mat 223.184 2.954 10.759 223.184 2.954 10.759 223.197 2.972 10.514 223.105 3.150 10.770 223.118 3.168 10.524 219.572 -0.887 12.995 219.525 -1.367 8.471 210.133 2.435 14.102 210.528 2.458 5.708",
            "Jumbotron_Reception LightShaft_Yellow_Mat 202.345 -0.196 10.774 202.345 -0.196 10.774 202.176 -0.200 10.953 202.340 0.015 10.773 202.170 0.012 10.952 207.711 -1.717 13.745 206.027 -2.341 16.098 208.501 2.339 13.872 205.722 2.266 16.358",
            "Jumbotron_StadiumConcession LightShaft_Cyan_Mat 152.305 5.330 62.483 152.304 5.565 62.483 152.370 5.576 62.246 152.269 5.773 62.483 152.335 5.785 62.246 139.030 -0.605 61.035 139.094 -0.594 54.869 138.992 5.188 61.038 139.115 5.732 54.660",
            "Jumbotron_StadiumConcession LightShaftQuest_Orange_Mat 168.251 5.573 63.171 168.251 5.573 63.171 168.413 5.573 62.986 168.181 5.763 63.109 168.343 5.763 62.924 161.294 0.738 60.460 165.465 0.258 57.146 160.910 3.888 60.158 163.439 3.973 56.256",
            "Jumbotron_StadiumEntry LightShaft_Cyan_Mat -75.333 -4.088 -142.270 -75.333 -4.088 -142.270 -75.374 -4.065 -142.512 -75.363 -3.880 -142.246 -75.404 -3.857 -142.488 -85.278 -10.243 -138.551 -86.227 -9.731 -144.084 -86.106 -3.290 -136.651 -87.294 -2.648 -143.577",
            "Jumbotron_StadiumEntry LightShaft_Cyan_Mat -97.649 -0.031 -43.477 -97.649 -0.031 -43.477 -97.636 -0.031 -43.231 -97.649 0.181 -43.477 -97.636 0.181 -43.231 -86.543 -3.817 -46.408 -86.251 -3.817 -40.968 -86.607 2.676 -47.463 -86.243 2.676 -40.652",
            "Jumbotron_StadiumEntry LightShaft_Purple_Mat -69.518 -0.579 -48.582 -69.518 -0.579 -48.582 -69.287 -0.581 -48.667 -69.536 -0.375 -48.633 -69.305 -0.376 -48.718 -73.182 -6.635 -57.917 -68.162 -6.662 -59.773 -74.607 -1.433 -58.853 -68.322 -1.468 -61.177",
            "Jumbotron_Garage LightShaft_Cyan_Mat 122.624 0.642 -30.320 122.624 0.642 -30.320 122.587 0.642 -30.564 122.583 0.849 -30.314 122.546 0.849 -30.558 113.722 -7.080 -22.869 112.771 -7.080 -29.109 112.627 -0.587 -21.464 111.436 -0.588 -29.276",
            "Jumbotron_Garage LightShaft_Cyan_Mat 119.316 -2.495 1.990 119.316 -2.495 1.990 119.072 -2.495 1.962 119.316 -2.283 1.990 119.072 -2.283 1.962 118.865 -5.712 13.255 114.392 -5.712 12.757 119.734 -0.714 13.343 114.134 -0.714 12.720",
            "Jumbotron_Cargo1 LightShaft_HotPink_Mat 10.313 3.027 -1.268 10.313 3.027 -1.268 10.492 3.027 -1.437 10.241 3.210 -1.344 10.420 3.210 -1.514 8.255 -0.313 -3.446 9.193 -0.313 -4.333 6.343 2.562 -3.902 7.943 2.741 -5.471",
            "Jumbotron_Cargo1 LightShaft_Purple_Mat 36.366 3.150 -6.561 36.366 3.150 -6.561 36.609 3.149 -6.519 36.379 3.350 -6.629 36.622 3.349 -6.587 35.511 -4.668 -16.044 41.569 -4.693 -14.982 34.794 2.543 -18.692 42.378 2.511 -17.363",
            "Jumbotron_Cargo1 LightShaft_Cyan_Mat 25.884 3.212 -15.239 25.884 3.212 -15.239 25.896 3.190 -14.994 25.934 3.417 -15.223 25.946 3.395 -14.977 35.838 -2.810 -18.076 36.164 -3.432 -11.281 36.967 2.228 -19.001 37.374 1.450 -10.494",
            "Jumbotron_Cargo1 LightShaft_HotPink_Mat -0.736 4.013 13.320 -0.736 4.013 13.320 -0.490 4.007 13.334 -0.728 4.212 13.252 -0.482 4.207 13.265 -1.992 -0.651 7.974 1.901 -0.687 7.733 -2.006 3.437 2.990 3.027 3.215 2.194",
            "Jumbotron_Cargo1 LightShaft_Cyan_Mat 40.748 2.158 -19.428 40.748 2.158 -19.428 40.950 2.158 -19.286 40.788 2.358 -19.485 40.990 2.358 -19.344 43.276 -1.993 -25.389 47.619 -1.959 -25.106 43.049 2.057 -27.294 49.154 2.272 -27.912",
            "Jumbotron_Stadium LightShaft_HotPink_Mat 100.920 2.654 -108.197 100.920 2.654 -108.197 101.108 2.673 -108.039 100.948 2.445 -108.206 101.136 2.464 -108.047 91.568 2.936 -100.585 93.961 4.554 -93.870 89.286 -4.902 -99.320 94.304 -5.122 -93.166",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 83.868 2.228 -92.562 83.868 2.228 -92.562 84.034 2.262 -92.384 83.859 2.437 -92.594 84.025 2.471 -92.416 90.221 -3.516 -102.407 98.821 -2.442 -98.236 91.066 1.342 -106.953 99.639 3.665 -100.038",
            "Jumbotron_Stadium LightShaft_Cyan_Mat 89.621 0.023 -83.197 89.621 0.023 -83.197 89.868 0.024 -83.199 89.621 -0.145 -83.327 89.868 -0.143 -83.329 88.330 -1.396 -78.958 91.223 -1.400 -78.938 88.028 -5.006 -80.163 91.043 -5.005 -80.162",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 101.028 2.403 -96.198 101.028 2.403 -96.198 101.011 2.403 -96.412 101.017 2.614 -96.197 101.000 2.614 -96.411 89.758 -1.220 -92.315 85.633 -1.335 -100.965 86.397 3.052 -89.512 83.856 4.087 -103.097",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 86.741 2.274 -84.853 86.741 2.106 -84.851 86.986 2.108 -84.824 86.741 2.317 -84.862 86.986 2.319 -84.836 85.664 -2.163 -96.521 95.431 -2.204 -98.924 83.871 2.729 -100.353 95.660 3.688 -100.607",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 93.069 2.234 -84.782 93.069 2.234 -84.782 93.307 2.248 -84.843 93.052 2.444 -84.800 93.290 2.458 -84.861 88.249 -2.853 -95.169 96.550 -2.609 -100.843 84.952 1.749 -98.294 95.835 3.190 -102.703",
            "Jumbotron_Stadium LightShaft_Red_Mat 74.265 -0.134 -115.359 74.265 -0.134 -115.359 74.076 -0.139 -115.201 74.258 0.078 -115.360 74.069 0.073 -115.201 83.885 -2.097 -108.685 77.638 -2.268 -103.443 84.324 3.638 -109.237 80.315 3.529 -105.870",
            "Jumbotron_Stadium LightShaft_Red_Mat 104.922 -0.098 -78.992 104.922 -0.098 -78.992 105.098 -0.103 -79.164 104.944 0.111 -78.974 105.120 0.107 -79.147 95.978 -0.844 -86.262 98.462 -0.907 -88.699 95.888 2.754 -85.483 98.997 2.675 -88.533",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 100.843 2.462 -89.268 100.843 2.462 -89.268 100.974 2.461 -89.477 100.825 2.672 -89.280 100.956 2.671 -89.489 90.026 -2.481 -93.213 92.116 -2.784 -103.048 85.633 2.260 -93.428 90.502 3.022 -104.184",
            "Jumbotron_Stadium LightShaft_Red_Mat 81.635 -0.141 -77.574 81.635 -0.141 -77.574 81.846 -0.150 -77.447 81.642 0.070 -77.572 81.853 0.062 -77.445 85.003 -3.195 -88.642 87.987 -3.319 -86.856 84.654 3.643 -88.898 88.388 3.488 -86.661",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 81.505 2.238 -104.924 81.505 2.238 -104.924 81.474 2.229 -104.680 81.503 2.450 -104.916 81.472 2.440 -104.672 93.602 -2.688 -107.151 95.901 -2.934 -97.814 97.122 3.637 -108.530 97.208 4.116 -97.284",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 84.723 2.255 -99.582 84.723 2.255 -99.582 84.683 2.247 -99.340 84.729 2.466 -99.575 84.688 2.459 -99.332 96.633 -2.474 -100.669 98.560 -2.769 -91.251 100.416 3.724 -101.923 100.085 4.234 -90.684",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 81.302 2.451 -109.424 81.302 2.451 -109.424 81.513 2.423 -109.547 81.274 2.241 -109.424 81.485 2.214 -109.548 85.451 3.719 -98.165 92.481 2.728 -99.741 84.640 -0.082 -95.543 92.360 -1.800 -99.059",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 95.323 -1.615 -111.466 95.323 -1.615 -111.466 95.129 -1.612 -111.618 95.359 -1.411 -111.507 95.165 -1.408 -111.659 90.081 -0.192 -101.274 88.362 -0.064 -102.652 90.620 2.319 -101.719 89.051 2.342 -102.945",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 100.883 2.456 -84.936 100.883 2.456 -84.936 101.060 2.457 -85.107 100.864 2.666 -84.955 101.041 2.667 -85.126 91.320 -0.152 -91.653 92.413 -0.605 -97.495 87.771 3.039 -93.540 91.185 3.380 -99.129",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 95.127 2.255 -84.704 95.127 2.255 -84.704 95.358 2.263 -84.791 95.115 2.466 -84.715 95.345 2.474 -84.802 89.033 -2.418 -94.605 96.658 -2.251 -101.163 85.552 2.356 -97.234 95.918 3.614 -102.794",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 83.992 3.689 -88.259 83.992 3.689 -88.259 83.792 3.671 -88.402 83.979 3.482 -88.215 83.779 3.464 -88.359 93.276 3.321 -96.168 89.247 1.993 -103.186 96.018 -3.156 -96.716 90.604 -4.668 -102.330",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 83.803 2.248 -84.858 83.803 2.248 -84.858 84.015 2.271 -84.735 83.785 2.458 -84.865 83.997 2.481 -84.743 86.961 -3.039 -96.622 96.814 -2.016 -94.901 86.087 4.811 -100.884 96.823 6.849 -96.368",
            "Jumbotron_Stadium LightShaft_Blue_Stadium_Mat 100.924 2.466 -102.842 100.924 2.466 -102.842 100.856 2.475 -103.045 100.902 2.676 -102.825 100.834 2.685 -103.028 91.147 -2.111 -96.623 85.064 -2.087 -104.027 88.335 1.777 -92.831 82.526 3.298 -105.321",
            "Jumbotron_Stadium LightShaft_Red_Mat 103.475 -0.096 -104.911 103.475 -0.096 -104.911 103.308 -0.097 -105.092 103.487 0.115 -104.924 103.321 0.114 -105.105 91.811 -1.959 -93.436 88.429 -2.023 -97.776 92.320 2.739 -92.751 85.678 2.808 -98.087",
        };

        private static bool IsShaft(string mesh)
        {
            return mesh != null && mesh.StartsWith(ShaftMesh, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Iteration 11: the kit's light shafts off. The kit draws LightArchShaft unskinned, in its bind pose (x 18.8): a solid
        /// cone or box hanging by the lamp (the Foyer's magenta one). <see cref="Shafts"/> rebuilds each shaft from its bones.
        /// A separate kit object is named after its mesh. A merged batch of install shaft materials only (the Reception's
        /// LightShaft_Yellow_Mat) goes as well. The kit's grey batches are rebuilt without their shafts (<see cref="KitGlow"/>).
        /// </summary>
        private static void HideKitShafts(RrSceneKitRoot kit, Stats st)
        {
            var scan = kit.transform.Find("Renderers");
            if (scan == null)
            {
                scan = kit.transform;
            }

            foreach (var mr in scan.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null || !mr.enabled)
                {
                    continue;
                }

                string n = mr.gameObject.name;
                bool shaft = IsShaft(n);
                if (!shaft && n.StartsWith("Batch ", StringComparison.Ordinal))
                {
                    var shared = mr.sharedMaterials;
                    shaft = shared.Length > 0;
                    foreach (var m in shared)
                    {
                        if (m == null || m.name.IndexOf("LightShaft", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            shaft = false;
                            break;
                        }
                    }
                }

                if (shaft)
                {
                    mr.enabled = false;
                    st.KitShaftsHidden++;
                }
            }
        }

        /// <summary>
        /// The room's light shafts from <see cref="ShaftData"/> (scene coordinates under the haze group): each one a
        /// frustum from the lamp's aperture to the far quad, its four sides soft across (0 at the edges, 1 in the middle)
        /// and fading along (full at the lamp, 0 where the light lands), in a white-map clone of the level material's
        /// additive stand-in (<see cref="Glows"/>), merged per material.
        /// </summary>
        private static IEnumerator Shafts(Transform group, string scene, RrJumbotronHull.Owned owned, Stats st)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var style = System.Globalization.NumberStyles.Float;
            var bufs = new Dictionary<int, Buf>();
            var order = new List<int>();
            string prefix = scene + " ";
            var c = new Vector3[8];
            foreach (var line in ShaftData)
            {
                if (!line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var f = line.Split(' ');
                int g = f.Length == 29 ? GlowIndex(f[1]) : -1;
                if (g < 0 || Glows[g].Kind != GlowKind.Additive)
                {
                    continue;
                }

                bool ok = true;
                for (int i = 0; i < 8 && ok; i++)
                {
                    float x = 0f, y = 0f, z = 0f;
                    ok = float.TryParse(f[5 + 3 * i], style, inv, out x) && float.TryParse(f[6 + 3 * i], style, inv, out y) && float.TryParse(f[7 + 3 * i], style, inv, out z);
                    c[i] = new Vector3(x, y, z);
                }

                if (!ok)
                {
                    continue;
                }

                Buf buf;
                if (!bufs.TryGetValue(g, out buf))
                {
                    buf = new Buf();
                    bufs.Add(g, buf);
                    order.Add(g);
                }

                AddShaft(buf, c);
                st.Shafts++;
            }

            foreach (int g in order)
            {
                Material m = null;
                yield return ShaftMaterial(Glows[g], owned, st, x => m = x);
                if (group == null)
                {
                    yield break;
                }

                var buf = bufs[g];
                if (m == null || buf.V.Count == 0)
                {
                    continue;
                }

                var mesh = new Mesh { name = "Jumbotron shafts " + Glows[g].Name };
                mesh.SetVertices(buf.V);
                mesh.SetNormals(buf.N);
                mesh.SetUVs(0, buf.Uv);
                mesh.SetColors(buf.C);
                mesh.SetTriangles(buf.T, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                owned.Things.Add(mesh);
                var go = new GameObject("Shafts " + Glows[g].Name);
                go.layer = RrLayers.Environment;
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                Quiet(r);
                st.ShaftMeshes++;
            }
        }

        /// <summary>
        /// One frustum: corners c = near BR, BL, TR, TL, far BR, BL, TR, TL. Four sides (bottom, top, right, left), each
        /// <see cref="ShaftRows"/> x (<see cref="ShaftCols"/> - 1) cells, vertex alpha = sin(pi s) x the fade along.
        /// </summary>
        private static void AddShaft(Buf buf, Vector3[] c)
        {
            for (int k = 0; k < 16; k += 4)
            {
                Vector3 na = c[ShaftSides[k]], nb = c[ShaftSides[k + 1]], fa = c[ShaftSides[k + 2]], fb = c[ShaftSides[k + 3]];
                var n = Vector3.Cross(fa - na, nb - na);
                n = n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.up;
                int b = buf.V.Count;
                for (int j = 0; j <= ShaftRows; j++)
                {
                    float t = (float)j / ShaftRows;
                    // Full at the lamp (a short ramp, so the aperture has no hard edge), none where the light lands.
                    float along = Mathf.SmoothStep(0f, 1f, t / 0.06f) * Mathf.Pow(1f - t, 1.3f);
                    for (int i = 0; i < ShaftCols; i++)
                    {
                        float s = (float)i / (ShaftCols - 1);
                        buf.V.Add(Vector3.Lerp(Vector3.Lerp(na, nb, s), Vector3.Lerp(fa, fb, s), t));
                        buf.N.Add(n);
                        buf.Uv.Add(new Vector2(s, t));
                        buf.C.Add(new Color(1f, 1f, 1f, Mathf.Sin(Mathf.PI * s) * along));
                    }
                }

                for (int j = 0; j < ShaftRows; j++)
                {
                    for (int i = 0; i < ShaftCols - 1; i++)
                    {
                        int a = b + j * ShaftCols + i, up = a + ShaftCols;
                        buf.T.Add(a);
                        buf.T.Add(a + 1);
                        buf.T.Add(up + 1);
                        buf.T.Add(a);
                        buf.T.Add(up + 1);
                        buf.T.Add(up);
                    }
                }
            }
        }

        /// <summary>The frustum's sides as corner indices (near a, near b, far a, far b): bottom, top, right, left.</summary>
        private static readonly int[] ShaftSides = { 0, 1, 4, 5, 2, 3, 6, 7, 0, 2, 4, 6, 1, 3, 5, 7 };

        /// <summary>The level material's additive stand-in (<see cref="GlowMaterial"/>) with a white map: the fade is in the vertex alpha.</summary>
        private static IEnumerator ShaftMaterial(GlowSpec g, RrJumbotronHull.Owned owned, Stats st, Action<Material> done)
        {
            string key = "haze:shaft:" + g.Name;
            Material m;
            if (owned.Materials.TryGetValue(key, out m) && m != null)
            {
                done(m);
                yield break;
            }

            Material glow = null;
            yield return GlowMaterial(g, owned, st, x => glow = x);
            m = null;
            if (glow != null)
            {
                m = new Material(glow) { name = g.Name + " (Jumbotron light shaft on " + glow.name + ")" };
                owned.Things.Add(m);
                SetMap(m, Texture2D.whiteTexture);
                owned.Materials[key] = m;
            }

            done(m);
        }
    }
}
