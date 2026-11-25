using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron
{
    /// <summary>
    /// Draws a Jumbotron room's hull from DN/tools/gtscene's records (the level's editor-merged static batches, which the
    /// PC install does not have, re-measured as exact planar rectangles): "G platemat" (a level material: colour, texture,
    /// tiling, and whether the install has it), "G plate" (one quad: centre, rotation, half extents e=hx,hy, and the
    /// mesh's vertex colour c= when it is not white; its face looks along local +z, one-sided like the level's meshes)
    /// and "G dress" (a stand-in for another mesh the install lacks: bounds box, colour and material). The hull's
    /// colliders are the level's own, as "C box" plates the kit builds.
    ///
    /// Iteration 10 (the Golden Trophy's approved approach, numbers in DevNotes/iter10/jt_materials.md section 5): every
    /// surface is an install AG/Standard-* material, planned per level material by <see cref="RrJumbotronLook.Plan"/> -
    /// the level's own when the install has it, else the closest install template cloned with an install texture, tint
    /// and glow - so RrLightGrid lights it like Rec Room. The plates' c= tints are written RAW into the mesh colours
    /// (white where a plate has none), exactly what Rec Room's AG shaders multiply by _BaseColor; they are never folded
    /// into _BaseColor (SetColor linearised them: walls 3-8x too dark, the cinderblock a saturated violet-blue). A
    /// template whose shader does not read vertex colour gets one clone per colour instead, _BaseColor = the linear
    /// product converted back to gamma. _BaseColor alpha (0.354 on the opaque sci-fi covers, vents and speakers) is not
    /// transparency: only the level's real glass is drawn transparent.
    ///
    /// Plates of one material become one mesh (at most 16,000 plates = 64,000 vertices per mesh), UVs in texture
    /// repeats (metres / the plan's metres per repeat, along the plate's own axes projected from world space so
    /// neighbouring plates tile seamlessly). Dress boxes are merged per material the same way (UVs in metres from each
    /// face's corner, white vertex colour). Left out: area fog, portal glows, light shafts and the invisible nav walls
    /// (not surfaces: kept in <see cref="Data"/> for <see cref="RrJumbotronHaze"/>), and the level-only decals. Everything
    /// made here is freed with the room (<see cref="Owned"/>).
    ///
    /// Iteration 11 (headset test: see-through walls, wrong textures, random floating things): "ds=1" on a platemat (or a
    /// plate) draws the plates from both sides (a flipped copy; Rec Room's own opaque shaders are all Cull Back, so the
    /// converter only sets it where its geometry needs it); no template a look lands on may be see-through except the
    /// real glass (alpha-clipped ones are clipped solid, blended ones re-planned); plates can fit a picture (the rugs) or
    /// turn the texture (the ribbed walls); the 80s sunset is drawn as its striped disc; a cover wall's glass-pane frame
    /// record is left out (its solid box hid the glass); scaffolding trusses and lamp tripods are open frames of thin
    /// bars instead of solid bounds boxes.
    /// </summary>
    public static class RrJumbotronHull
    {
        public const int PlatesPerMesh = 16000;

        /// <summary>Dress boxes per merged mesh (24 vertices each: at most 64,000 vertices).</summary>
        public const int BoxesPerMesh = 2666;

        /// <summary>Materials, textures and meshes one room made (destroyed with the room).</summary>
        public sealed class Owned : MonoBehaviour
        {
            public readonly List<UnityEngine.Object> Things = new List<UnityEngine.Object>();
            public readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

            private void OnDestroy()
            {
                foreach (var o in Things)
                {
                    if (o != null)
                    {
                        Destroy(o);
                    }
                }

                Things.Clear();
                Materials.Clear();
            }
        }

        public sealed class Stats
        {
            public int Plates, Meshes, Materials, Dress, Skipped, InstallMaterials;
            /// <summary>Plate groups drawn with raw vertex colours / with per-colour clones (template without vertex colour).</summary>
            public int VertexColour, PerColour;
            /// <summary>Plates drawn from both sides (ds=1), and see-through templates replaced by solid ones.</summary>
            public int DoubleSided, Solidified;
            /// <summary>Dress records drawn as open frames (trusses, tripods) instead of solid bounds boxes.</summary>
            public int Framed;

            public override string ToString()
            {
                return "hull " + Plates + " plates in " + Meshes + " meshes (" + Materials + " materials, " + InstallMaterials + " from the install, " + VertexColour + " vertex-coloured"
                    + (PerColour > 0 ? ", " + PerColour + " per-colour" : "") + (DoubleSided > 0 ? ", " + DoubleSided + " double-sided" : "") + (Solidified > 0 ? ", " + Solidified + " see-through templates made solid" : "")
                    + "), " + Dress + " dress boxes" + (Framed > 0 ? " (" + Framed + " as open frames)" : "") + ", " + Skipped + " left out";
            }
        }

        internal struct MatInfo
        {
            public string Name;
            /// <summary>Unused since iteration 10 (plates are grouped per material; tints are vertex colours).</summary>
            public string Key;
            public Color Col;
            public string Tex;
            public Vector2 Scale;
            public bool Inst;
            /// <summary>"ds=1" on the platemat: every plate of the material is drawn from both sides (a flipped copy).</summary>
            public bool Ds;
            /// <summary>Iteration 16 (DevNotes/tools/rcscene's measured materials): mean=, glow=, clear=1 (<see cref="RrJumbotronLook.PlanMeasured"/>).</summary>
            public bool Measured, Clear;
            public Color? Mean, Glow;
        }

        internal struct Plate
        {
            public Vector3 C;
            public Quaternion Q;
            public float Hx, Hy;
            /// <summary>The level mesh's vertex colour (c=, raw bytes as the shader sees them); 0,0,0,0 = none (white).</summary>
            public Color32 Vc;
            /// <summary>"ds=1" on the plate: drawn from both sides.</summary>
            public bool Ds;
        }

        internal struct Dress
        {
            public string Mesh;
            public Vector3 C;
            public Quaternion Q;
            public Vector3 E;
            public Color Col;
            public string Mat;
        }

        /// <summary>The parsed hull of one room (taken out of the scene's marker list so the kit does not report them).</summary>
        public sealed class Data
        {
            internal readonly Dictionary<string, MatInfo> Mats = new Dictionary<string, MatInfo>();
            /// <summary>Plates per level material name (all of them, the not-surfaces too: the haze draws those).</summary>
            internal readonly Dictionary<string, List<Plate>> Plates = new Dictionary<string, List<Plate>>();
            internal readonly List<Dress> Dresses = new List<Dress>();
            public int Count;
        }

        /// <summary>Area fog, portal glows, light shafts and nav walls: kept in the data, never drawn as surfaces here.</summary>
        internal static bool NotASurface(string m)
        {
            return m.IndexOf("AreaFog", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("Portal_", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("LightShaft", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("InvisibleWall", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("Nav_Mat", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Takes the plate / platemat / dress markers out of the scene data (worker or main thread).</summary>
        public static Data Extract(RrSceneData d, Bounds? filter, List<Bounds> drop = null)
        {
            var h = new Data();
            var white = new Color32(255, 255, 255, 255);
            for (int i = d.Markers.Count - 1; i >= 0; i--)
            {
                var m = d.Markers[i];
                if (m.Kind != "plate" && m.Kind != "platemat" && m.Kind != "dress")
                {
                    continue;
                }

                d.Markers.RemoveAt(i);
                if (filter.HasValue && m.Kind != "platemat" && !filter.Value.Contains(m.Position))
                {
                    continue;
                }

                // Set pieces we rebuild (the Jumbotron's screen block): its small plates go, the long roof beams through it stay.
                if (drop != null && m.Kind != "platemat" && Inside(drop, m.Position) && Small(m))
                {
                    continue;
                }

                if (m.Kind == "platemat")
                {
                    var mi = new MatInfo { Name = m.Name, Col = Col(Get(m, "col"), Color.white), Tex = Get(m, "tex"), Scale = V2(Get(m, "scale"), Vector2.one), Inst = Get(m, "inst") == "1", Ds = Get(m, "ds") == "1" };
                    string mean = Get(m, "mean"), glow = Get(m, "glow");
                    mi.Clear = Get(m, "clear") == "1";
                    mi.Measured = mean != null || glow != null || mi.Clear;
                    if (mean != null)
                    {
                        mi.Mean = Col(mean, Color.white);
                    }

                    if (glow != null)
                    {
                        mi.Glow = Col(glow, Color.black);
                    }

                    h.Mats[m.Name] = mi;
                }
                else if (m.Kind == "plate")
                {
                    var e = V2(Get(m, "e"), Vector2.zero);
                    if (e.x <= 0f || e.y <= 0f)
                    {
                        continue;
                    }

                    // The mesh's vertex colour (c=, only written when not white): raw, as Rec Room's AG shaders read it.
                    string tint = Get(m, "c");
                    var vc = white;
                    if (tint != null)
                    {
                        var tc = Col(tint, Color.white);
                        vc = new Color32(Byte(tc.r), Byte(tc.g), Byte(tc.b), 255);
                    }

                    List<Plate> list;
                    if (!h.Plates.TryGetValue(m.Name, out list))
                    {
                        list = new List<Plate>();
                        h.Plates.Add(m.Name, list);
                    }

                    list.Add(new Plate { C = m.Position, Q = m.Rotation, Hx = e.x, Hy = e.y, Vc = vc, Ds = Get(m, "ds") == "1" });
                    h.Count++;
                }
                else
                {
                    var e3 = V3(Get(m, "e"), Vector3.zero);
                    h.Dresses.Add(new Dress { Mesh = m.Name, C = m.Position, Q = m.Rotation, E = e3, Col = Col(Get(m, "c"), Color.grey), Mat = Get(m, "m") ?? "" });
                }
            }

            return h;
        }

        private static byte Byte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        }

        private static bool Small(RrSceneData.MarkerRecord m)
        {
            var e = Nums(Get(m, "e"));
            float big = 0f;
            foreach (var x in e)
            {
                big = Mathf.Max(big, x);
            }

            return big < 4f;
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

        private static string Get(RrSceneData.MarkerRecord m, string key)
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

        private static float[] Nums(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return new float[0];
            }

            var p = s.Split(',');
            var r = new float[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out r[i]);
            }

            return r;
        }

        private static Color Col(string s, Color fallback)
        {
            var n = Nums(s);
            return n.Length >= 3 ? new Color(n[0], n[1], n[2], n.Length > 3 ? n[3] : 1f) : fallback;
        }

        private static Vector2 V2(string s, Vector2 fallback)
        {
            var n = Nums(s);
            return n.Length >= 2 ? new Vector2(n[0], n[1]) : fallback;
        }

        private static Vector3 V3(string s, Vector3 fallback)
        {
            var n = Nums(s);
            return n.Length >= 3 ? new Vector3(n[0], n[1], n[2]) : fallback;
        }

        // ------------------------------------------------------------------ build

        /// <summary>A planned surface after loading: the material drawn, the look it came from, and the UV scale it needs.</summary>
        private sealed class Surface
        {
            public Material Material;
            public RrJumbotronLook.Look Look;
            public bool Install;

            /// <summary>The material's own _BaseMap scale (all the AG maps use it): our UVs are repeats / this.</summary>
            public Vector2 UvScale = Vector2.one;
        }

        /// <summary>One build's caches (textures, failed loads, the surfaces log) and its frame budget.</summary>
        private sealed class Ctx
        {
            public Owned Owned;
            public readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
            public readonly HashSet<string> Failed = new HashSet<string>();
            public readonly HashSet<Material> Used = new HashSet<Material>();
            public readonly HashSet<Material> FromInstall = new HashSet<Material>();
            public readonly List<string> Log = new List<string>();
            public readonly HashSet<string> Logged = new HashSet<string>();
            public float T0;
            /// <summary>See-through templates a look was moved off (or clipped solid).</summary>
            public int Solidified;

            public bool Due()
            {
                if (Time.realtimeSinceStartup - T0 > 0.004f)
                {
                    T0 = Time.realtimeSinceStartup;
                    return true;
                }

                return false;
            }

            public void Note(string level, string text)
            {
                if (Logged.Add(level))
                {
                    Log.Add(level + "=" + text);
                }
            }
        }

        /// <summary>Builds the plates and dress boxes under root (scene point p at local p + offset), spread over frames.</summary>
        public static IEnumerator Build(Data h, Transform root, Vector3 offset, Owned owned, string name, Action<Stats> done)
        {
            var st = new Stats();
            if (h == null || root == null)
            {
                done?.Invoke(st);
                yield break;
            }

            if (owned == null)
            {
                owned = root.gameObject.AddComponent<Owned>();
            }

            var ctx = new Ctx { Owned = owned, T0 = Time.realtimeSinceStartup };
            var group = RrBuild.Group(root, "Hull " + name, offset).transform;
            foreach (var kv in h.Plates)
            {
                // Keys are level material names (a "name|tint" key of an older extract: the name part).
                int bar = kv.Key.IndexOf('|');
                string matName = bar >= 0 ? kv.Key.Substring(0, bar) : kv.Key;
                var plates = kv.Value;
                if (NotASurface(matName))
                {
                    st.Skipped += plates.Count;
                    continue;
                }

                if (matName == MirrorMaterial)
                {
                    // The Armory's dressing mirror: a real mirror (Thad's), its glass on the plate's front (+z), 3 mm out.
                    foreach (var p in plates)
                    {
                        RrMirror.Create(group, "Mirror " + name, p.C + p.Q * new Vector3(0f, 0f, 0.003f), p.Q, p.Hx * 2f, p.Hy * 2f);
                    }

                    ctx.Note(matName, "RrMirror x" + plates.Count);
                    continue;
                }

                MatInfo mi;
                if (!h.Mats.TryGetValue(matName, out mi))
                {
                    mi = new MatInfo { Name = matName, Col = new Color(0.5f, 0.5f, 0.55f), Tex = "-", Scale = Vector2.one };
                }

                var look = mi.Measured ? RrJumbotronLook.PlanMeasured(matName, mi.Col, mi.Scale, mi.Inst, mi.Mean, mi.Glow, mi.Clear) : RrJumbotronLook.Plan(matName, mi.Col, mi.Scale, mi.Inst, false);
                if (look.Skip)
                {
                    ctx.Note(matName, "skip");
                    st.Skipped += plates.Count;
                    continue;
                }

                Surface s = null;
                yield return Resolve(look, matName, mi.Col, ctx, x => s = x);
                if (group == null)
                {
                    yield break;
                }

                if (s.Look.Disc)
                {
                    // The 80s sunset: a striped disc per plate, its gradient in the vertex colours.
                    ctx.Note(matName, Describe(s) + " disc");
                    ctx.Used.Add(s.Material);
                    if (s.Install)
                    {
                        ctx.FromInstall.Add(s.Material);
                    }

                    var disc = Sunset(plates, "Jumbotron sunset " + matName);
                    owned.Things.Add(disc);
                    Draw(group, "Sunset " + matName, disc, s.Material, s.Look);
                    st.Plates += plates.Count;
                    st.Meshes++;
                    continue;
                }

                bool vcShader = RrJumbotronLook.ReadsVertexColour(s.Material);
                if (vcShader || AllWhite(plates))
                {
                    if (vcShader)
                    {
                        st.VertexColour++;
                    }

                    ctx.Note(matName, Describe(s) + (vcShader && !AllWhite(plates) ? " +vc" : "") + (mi.Ds ? " ds" : ""));
                    yield return Emit(plates, matName, s, s.Material, vcShader, mi.Ds, group, ctx, st);
                    if (group == null)
                    {
                        yield break;
                    }

                    continue;
                }

                // The shader does not read vertex colour: one clone per colour (1/20 steps), _BaseColor x the linear tint.
                var byTint = new Dictionary<int, List<Plate>>();
                foreach (var p in plates)
                {
                    var q = Quantised(p.Vc);
                    int k = (q.r << 16) | (q.g << 8) | q.b;
                    List<Plate> list;
                    if (!byTint.TryGetValue(k, out list))
                    {
                        list = new List<Plate>();
                        byTint.Add(k, list);
                    }

                    list.Add(new Plate { C = p.C, Q = p.Q, Hx = p.Hx, Hy = p.Hy, Vc = q, Ds = p.Ds });
                }

                st.PerColour++;
                ctx.Note(matName, Describe(s) + " x" + byTint.Count + " colours" + (mi.Ds ? " ds" : ""));
                foreach (var t in byTint)
                {
                    var list = t.Value;
                    var tinted = IsWhite(list[0].Vc) ? s.Material : Tinted(s.Material, list[0].Vc, owned, s.Look.Key);
                    yield return Emit(list, matName + " " + ColorUtility.ToHtmlStringRGB(list[0].Vc), s, tinted, false, mi.Ds, group, ctx, st);
                    if (group == null)
                    {
                        yield break;
                    }
                }
            }

            // Dress: the other missing meshes as boxes in their planned material, merged per material, no colliders
            // (the level's are built).
            var dressGroup = RrBuild.Group(group, "Dress").transform;
            var dressSurfaces = new Dictionary<string, Surface>();
            // Merged per material and tiling (two looks may share a material at another tile or with face UVs).
            var byMaterial = new Dictionary<string, List<Dress>>();
            var surfaceOf = new Dictionary<string, Surface>();
            foreach (var x in h.Dresses)
            {
                bool level = x.Mat.StartsWith("?", StringComparison.Ordinal);
                string m = level ? x.Mat.Substring(1) : x.Mat;
                if (m.Length == 0 || NotASurface(m) || x.E.x > 30f || x.E.y > 30f || x.E.z > 30f || GlassFrame(x.Mesh, m))
                {
                    st.Skipped++;
                    continue;
                }

                string dkey = m + "|" + ColorUtility.ToHtmlStringRGBA(x.Col);
                Surface s;
                if (!dressSurfaces.TryGetValue(dkey, out s))
                {
                    var look = RrJumbotronLook.Plan(m, x.Col, Vector2.one, !level, true);
                    if (look.Skip)
                    {
                        ctx.Note("dress " + m, "skip");
                        dressSurfaces[dkey] = null;
                        st.Skipped++;
                        continue;
                    }

                    yield return Resolve(look, m, x.Col, ctx, r => s = r);
                    if (group == null)
                    {
                        yield break;
                    }

                    dressSurfaces[dkey] = s;
                    ctx.Note("dress " + m, Describe(s));
                    ctx.Used.Add(s.Material);
                    if (s.Install)
                    {
                        ctx.FromInstall.Add(s.Material);
                    }
                }

                if (s == null)
                {
                    st.Skipped++;
                    continue;
                }

                string mkey = (s.Material != null ? s.Material.GetInstanceID() : 0) + "|" + s.Look.TileU.ToString(CultureInfo.InvariantCulture) + "|" + s.Look.V.ToString(CultureInfo.InvariantCulture)
                    + "|" + s.Look.FaceUv + "|" + s.Look.Rotate;
                List<Dress> boxes;
                if (!byMaterial.TryGetValue(mkey, out boxes))
                {
                    boxes = new List<Dress>();
                    byMaterial.Add(mkey, boxes);
                    surfaceOf[mkey] = s;
                }

                // Open frames instead of solid bounds for the see-through kit: scaffolding trusses, lamp tripods.
                st.Dress++;
                if (Frame(x, boxes))
                {
                    st.Framed++;
                }
                else
                {
                    boxes.Add(x);
                }

                if (x.Mesh == MarqueeMesh)
                {
                    Marquee(dressGroup, x, owned);
                }
            }

            foreach (var kv in byMaterial)
            {
                var s = surfaceOf[kv.Key];
                var boxes = kv.Value;
                for (int start = 0; start < boxes.Count; start += BoxesPerMesh)
                {
                    int n = Mathf.Min(BoxesPerMesh, boxes.Count - start);
                    var mesh = BoxMesh(boxes, start, n, "Jumbotron dress " + s.Look.Key, s);
                    owned.Things.Add(mesh);
                    Draw(dressGroup, "Dress " + s.Look.Key + (start > 0 ? " " + (start / BoxesPerMesh) : ""), mesh, s.Material, s.Look);
                    st.Meshes++;
                    if (ctx.Due())
                    {
                        yield return null;
                        if (group == null)
                        {
                            yield break;
                        }
                    }
                }
            }

            st.Materials = ctx.Used.Count;
            st.InstallMaterials = ctx.FromInstall.Count;
            st.Solidified = ctx.Solidified;
            var sb = new StringBuilder(name).Append(" hull surfaces:");
            foreach (var line in ctx.Log)
            {
                sb.Append(' ').Append(line).Append(';');
            }

            RrLog.Info("Jumbotron", sb.ToString());
            done?.Invoke(st);
        }

        private static string Describe(Surface s)
        {
            string m = s.Material != null ? s.Material.name : "-";
            return (s.Install ? "" : "(no install) ") + s.Look.Describe() + " [" + m + (s.Material != null && s.Material.shader != null ? " / " + s.Material.shader.name : "") + "]";
        }

        /// <summary>
        /// A plate group's meshes (at most <see cref="PlatesPerMesh"/> quads each) in one material. Double-sided plates
        /// (the material's ds=1, or the plate's own) count twice: their flipped copy is a second quad.
        /// </summary>
        private static IEnumerator Emit(List<Plate> plates, string label, Surface s, Material mat, bool vertexColour, bool doubleSided, Transform group, Ctx ctx, Stats st)
        {
            ctx.Used.Add(mat);
            if (s.Install)
            {
                ctx.FromInstall.Add(mat);
            }

            int part = 0;
            for (int start = 0; start < plates.Count; part++)
            {
                int n = 0, quads = 0;
                while (start + n < plates.Count)
                {
                    int q = doubleSided || plates[start + n].Ds ? 2 : 1;
                    if (quads + q > PlatesPerMesh)
                    {
                        break;
                    }

                    quads += q;
                    n++;
                }

                var mesh = PlateMesh(plates, start, n, quads, label, s, vertexColour, doubleSided);
                ctx.Owned.Things.Add(mesh);
                Draw(group, "Plates " + label + (part > 0 ? " " + part : ""), mesh, mat, s.Look);
                st.Plates += n;
                st.DoubleSided += quads - n;
                st.Meshes++;
                start += n;
                if (ctx.Due())
                {
                    yield return null;
                    if (group == null)
                    {
                        yield break;
                    }
                }
            }
        }

        private static void Draw(Transform parent, string name, Mesh mesh, Material mat, RrJumbotronLook.Look look)
        {
            var go = new GameObject(name);
            go.layer = RrLayers.Environment;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            // Transparent (the real glass) casts no shadow; queue 2501 is RrMaterials' depth-priming fix for opaque shaders.
            r.shadowCastingMode = look.Glass || look.Disc || (mat != null && mat.renderQueue >= 2900) ? ShadowCastingMode.Off : ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        private static bool IsWhite(Color32 c)
        {
            return (c.r == 255 && c.g == 255 && c.b == 255) || (c.r == 0 && c.g == 0 && c.b == 0 && c.a == 0);
        }

        private static bool AllWhite(List<Plate> plates)
        {
            foreach (var p in plates)
            {
                if (!IsWhite(p.Vc))
                {
                    return false;
                }
            }

            return true;
        }

        private static Color32 Quantised(Color32 c)
        {
            if (IsWhite(c))
            {
                return new Color32(255, 255, 255, 255);
            }

            return new Color32(Byte(Mathf.Round(c.r / 255f * 20f) / 20f), Byte(Mathf.Round(c.g / 255f * 20f) / 20f), Byte(Mathf.Round(c.b / 255f * 20f) / 20f), 255);
        }

        // ------------------------------------------------------------------ materials

        /// <summary>
        /// The material of a look: its install template as it is (the level's own material, or a template whose only
        /// change is the tiling, which lives in our UVs), or a clone with the look's texture, relief, tint and glow. An
        /// install material the install lacks is re-planned by name (<see cref="RrJumbotronLook.Default"/>); a missing
        /// template falls back to the paint template, then to RrLook's maker-pen colour. Cached per room in
        /// <see cref="Owned.Materials"/> ("look:" keys; the loaded install materials under "inst:").
        /// </summary>
        private static IEnumerator Resolve(RrJumbotronLook.Look look, string levelMat, Color col, Ctx ctx, Action<Surface> done)
        {
            var k = look;
            Material template = null;
            bool unclip = false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                template = null;
                if (k.Template != null)
                {
                    yield return LoadMaterial(k.Template, ctx, x => template = x);
                }

                // No hull surface but the real glass may draw see-through: an alpha-clipped opaque template is clipped
                // solid on its clone; a blended / additive one is treated as missing (re-planned by name, then the paint).
                if (template != null && !k.Glass && RrJumbotronLook.SeeThrough(template))
                {
                    ctx.Solidified++;
                    string sh = template.shader != null ? template.shader.name : "?";
                    if (RrJumbotronLook.ClipOnly(template))
                    {
                        unclip = true;
                        ctx.Note("solid " + levelMat, template.name + " [" + sh + "] clipping off");
                    }
                    else
                    {
                        ctx.Note("solid " + levelMat, template.name + " [" + sh + "] is see-through: replaced");
                        template = null;
                    }
                }

                if (template != null)
                {
                    break;
                }

                if (attempt == 0 && k.AsIs)
                {
                    k = RrJumbotronLook.Default(levelMat, col, k.TileU, k.TileV);
                }
                else if (k.Template != RrJumbotronLook.Pen)
                {
                    k = RrJumbotronLook.PenOf(k, col);
                }
                else
                {
                    break;
                }
            }

            var owned = ctx.Owned;
            if (owned == null)
            {
                // The room went away while loading: make nothing more (nothing would free it).
                done(new Surface { Material = template, Look = k, Install = template != null, UvScale = ScaleOf(template) });
                yield break;
            }

            string key = "look:" + k.Key + "|" + (template != null ? template.name : "solid") + (unclip ? "|unclip" : "");
            Material m;
            if (!owned.Materials.TryGetValue(key, out m) || m == null)
            {
                if (template == null)
                {
                    var c = k.SetTint ? k.Tint : col;
                    m = RrLook.Solid(new Color(c.r, c.g, c.b, 1f));
                }
                else if (!k.NeedsClone && !unclip)
                {
                    m = template;
                }
                else
                {
                    m = new Material(template) { name = template.name + " (Jumbotron " + k.Key + ")" };
                    owned.Things.Add(m);
                    Texture2D tex = null, bump = null, spec = null;
                    if (k.Texture != null)
                    {
                        yield return LoadTexture(k.Texture, ctx, x => tex = x);
                    }

                    if (k.Bump != null)
                    {
                        yield return LoadTexture(k.Bump, ctx, x => bump = x);
                    }

                    if (k.Spec != null)
                    {
                        yield return LoadTexture(k.Spec, ctx, x => spec = x);
                    }

                    if (m == null)
                    {
                        done(new Surface { Material = template, Look = k, Install = true, UvScale = ScaleOf(template) });
                        yield break;
                    }

                    Configure(m, k, tex, bump, spec);
                    if (unclip)
                    {
                        RrJumbotronLook.Unclip(m);
                    }
                }

                owned.Materials[key] = m;
            }

            done(new Surface { Material = m, Look = k, Install = template != null, UvScale = ScaleOf(m) });
        }

        /// <summary>Puts a look on a clone: textures, the scale reset (our UVs carry the tiling), tint and glow.</summary>
        private static void Configure(Material m, RrJumbotronLook.Look k, Texture2D tex, Texture2D bump, Texture2D spec)
        {
            if (tex != null)
            {
                foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                {
                    if (m.HasProperty(prop))
                    {
                        m.SetTexture(prop, tex);
                    }
                }
            }

            if (bump != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", bump);
                m.EnableKeyword("_NORMALMAP");
            }

            if (spec != null && m.HasProperty("_SpecGlossMap"))
            {
                m.SetTexture("_SpecGlossMap", spec);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            foreach (var prop in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_SpecGlossMap" })
            {
                if (m.HasProperty(prop))
                {
                    m.SetTextureScale(prop, Vector2.one);
                    m.SetTextureOffset(prop, Vector2.zero);
                }
            }

            if (k.SetTint)
            {
                foreach (var prop in new[] { "_BaseColor", "_Color" })
                {
                    if (m.HasProperty(prop))
                    {
                        // Alpha: the glass's own; on the opaque templates the template's (unused by the shader).
                        float a = k.Glass ? k.Tint.a : m.GetColor(prop).a;
                        m.SetColor(prop, new Color(k.Tint.r, k.Tint.g, k.Tint.b, a));
                    }
                }
            }

            if (k.Glow >= 0f && m.HasProperty("_EmissivePower"))
            {
                m.SetFloat("_EmissivePower", k.Glow);
            }
        }

        /// <summary>
        /// A clone for plates of one vertex colour when the shader does not read vertex colours: _BaseColor = the linear
        /// product of the base colour and the raw tint, converted back to gamma for SetColor (jt_materials 4c.1).
        /// </summary>
        private static Material Tinted(Material m, Color32 vc, Owned owned, string key)
        {
            string k = "tint:" + key + "|" + m.GetInstanceID() + "|" + vc.r + "," + vc.g + "," + vc.b;
            Material t;
            if (owned.Materials.TryGetValue(k, out t) && t != null)
            {
                return t;
            }

            t = new Material(m) { name = m.name + " (vc " + ColorUtility.ToHtmlStringRGB(vc) + ")" };
            owned.Things.Add(t);
            foreach (var prop in new[] { "_BaseColor", "_Color" })
            {
                if (t.HasProperty(prop))
                {
                    var b = t.GetColor(prop);
                    var lin = b.linear;
                    var c = new Color(lin.r * vc.r / 255f, lin.g * vc.g / 255f, lin.b * vc.b / 255f, 1f).gamma;
                    t.SetColor(prop, new Color(c.r, c.g, c.b, b.a));
                }
            }

            owned.Materials[k] = t;
            return t;
        }

        private static Vector2 ScaleOf(Material m)
        {
            if (m == null || !m.HasProperty("_BaseMap"))
            {
                return Vector2.one;
            }

            var s = m.GetTextureScale("_BaseMap");
            return new Vector2(Mathf.Abs(s.x) > 1e-4f ? s.x : 1f, Mathf.Abs(s.y) > 1e-4f ? s.y : 1f);
        }

        /// <summary>An install material by name (loaded once per room, repaired by RrMaterials), or null.</summary>
        private static IEnumerator LoadMaterial(string name, Ctx ctx, Action<Material> done)
        {
            Material m;
            if (ctx.Owned.Materials.TryGetValue("inst:" + name, out m) && m != null)
            {
                done(m);
                yield break;
            }

            var assets = RrAssets.Instance;
            RrEntry e;
            if (ctx.Failed.Contains("m:" + name) || assets == null || !assets.TryFind(name, 21, out e))
            {
                ctx.Failed.Add("m:" + name);
                done(null);
                yield break;
            }

            Material loaded = null;
            yield return assets.LoadRoutine<Material>(name, 21, x => loaded = x);
            m = loaded != null ? RrMaterials.Fix(loaded) : null;
            if (m != null)
            {
                ctx.Owned.Materials["inst:" + name] = m;
            }
            else
            {
                ctx.Failed.Add("m:" + name);
            }

            done(m);
        }

        /// <summary>An install texture by name, or null.</summary>
        private static IEnumerator LoadTexture(string name, Ctx ctx, Action<Texture2D> done)
        {
            Texture2D t;
            if (ctx.Textures.TryGetValue(name, out t) && t != null)
            {
                done(t);
                yield break;
            }

            var assets = RrAssets.Instance;
            RrEntry e;
            if (ctx.Failed.Contains("t:" + name) || assets == null || !assets.TryFind(name, 28, out e))
            {
                ctx.Failed.Add("t:" + name);
                done(null);
                yield break;
            }

            yield return assets.LoadRoutine<Texture2D>(name, 28, x => t = x);
            if (t != null)
            {
                ctx.Textures[name] = t;
            }
            else
            {
                ctx.Failed.Add("t:" + name);
            }

            done(t);
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>
        /// Plates as quads (quads = n + the double-sided plates' flipped copies). UVs are texture repeats along the
        /// plate's own axes projected from world space (coplanar plates tile as one surface), divided by the material's
        /// own scale; each plate is shifted by whole repeats so the numbers stay small. A fitted look (a picture) runs
        /// 0..1 over each plate instead; a turned look swaps the texture's axes. Colours are the plates' raw vertex
        /// colours (white without, or when not wanted). A double-sided plate gets a second quad on the same corners,
        /// wound the other way (its own normals), so it is seen from behind too.
        /// </summary>
        private static Mesh PlateMesh(List<Plate> plates, int start, int n, int quads, string name, Surface s, bool vertexColour, bool doubleSided)
        {
            var v = new Vector3[quads * 4];
            var uv = new Vector2[quads * 4];
            var col = new Color32[quads * 4];
            var tri = new int[quads * 6];
            double iu = 1.0 / Mathf.Max(0.001f, s.Look.TileU), iv = 1.0 / Mathf.Max(0.001f, s.Look.V);
            bool fit = s.Look.Fit, turn = s.Look.Rotate;
            double su = turn ? s.UvScale.y : s.UvScale.x, sv = turn ? s.UvScale.x : s.UvScale.y;
            var white = new Color32(255, 255, 255, 255);
            int b = 0, t = 0;
            for (int k = 0; k < n; k++)
            {
                var p = plates[start + k];
                Vector3 ax = p.Q * Vector3.right, ay = p.Q * Vector3.up;
                Vector3 u = ax * p.Hx, w = ay * p.Hy;
                v[b] = p.C - u - w;
                v[b + 1] = p.C + u - w;
                v[b + 2] = p.C + u + w;
                v[b + 3] = p.C - u + w;
                double fu = Math.Floor(Dot(p.C, ax) * iu), fv = Math.Floor(Dot(p.C, ay) * iv);
                var c = vertexColour && !IsWhite(p.Vc) ? new Color32(p.Vc.r, p.Vc.g, p.Vc.b, 255) : white;
                for (int j = 0; j < 4; j++)
                {
                    double pu, pv;
                    if (fit)
                    {
                        pu = j == 1 || j == 2 ? 1.0 : 0.0;
                        pv = j >= 2 ? 1.0 : 0.0;
                    }
                    else
                    {
                        // World-projected repeats: coplanar plates tile as one surface.
                        pu = Dot(v[b + j], ax) * iu - fu;
                        pv = Dot(v[b + j], ay) * iv - fv;
                    }

                    uv[b + j] = turn ? new Vector2((float)(pv / su), (float)(pu / sv)) : new Vector2((float)(pu / su), (float)(pv / sv));
                    col[b + j] = c;
                }

                tri[t] = b;
                tri[t + 1] = b + 1;
                tri[t + 2] = b + 2;
                tri[t + 3] = b;
                tri[t + 4] = b + 2;
                tri[t + 5] = b + 3;
                b += 4;
                t += 6;
                if (doubleSided || p.Ds)
                {
                    // The back face: the same corners, u mirrored so a texture reads the right way round from behind.
                    for (int j = 0; j < 4; j++)
                    {
                        v[b + j] = v[b - 4 + j];
                        var fuv = uv[b - 4 + j];
                        uv[b + j] = fit ? new Vector2(1f / (float)su - fuv.x, fuv.y) : new Vector2(-fuv.x, fuv.y);
                        col[b + j] = c;
                    }

                    tri[t] = b;
                    tri[t + 1] = b + 2;
                    tri[t + 2] = b + 1;
                    tri[t + 3] = b;
                    tri[t + 4] = b + 3;
                    tri[t + 5] = b + 2;
                    b += 4;
                    t += 6;
                }
            }

            var mesh = new Mesh { name = "Jumbotron hull " + name };
            if (v.Length > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = v;
            mesh.uv = uv;
            mesh.colors32 = col;
            mesh.triangles = tri;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static double Dot(Vector3 a, Vector3 b)
        {
            return (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z;
        }

        /// <summary>
        /// The retro sunset of the space walk (Sunset_Tex: a disc filling the square, solid on top, cut by five dark
        /// stripes that widen downwards, yellow at the top through orange to magenta; transparent around it). Each plate
        /// becomes that disc in rows of quads facing the plate's front; the gradient is in the raw vertex colours, which
        /// the emissive AG template multiplies into its colour and glow.
        /// </summary>
        private static Mesh Sunset(List<Plate> plates, string name)
        {
            // Bands of the disc in normalised height (1 = top, -1 = bottom), between the stripes.
            float[] bands = { 1f, -0.05f, -0.09f, -0.22f, -0.27f, -0.39f, -0.45f, -0.56f, -0.63f, -0.73f, -0.81f, -1f };
            var v = new List<Vector3>();
            var col = new List<Color32>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            foreach (var p in plates)
            {
                float r = Mathf.Min(p.Hx, p.Hy);
                Vector3 ax = p.Q * Vector3.right, ay = p.Q * Vector3.up;
                for (int bnd = 0; bnd + 1 < bands.Length; bnd += 2)
                {
                    float top = bands[bnd], bottom = bands[bnd + 1];
                    int rows = Mathf.Max(1, Mathf.CeilToInt((top - bottom) / 0.08f));
                    for (int i = 0; i < rows; i++)
                    {
                        float y1 = Mathf.Lerp(top, bottom, (float)i / rows), y0 = Mathf.Lerp(top, bottom, (float)(i + 1) / rows);
                        float x0 = Mathf.Sqrt(Mathf.Max(0f, 1f - y0 * y0)), x1 = Mathf.Sqrt(Mathf.Max(0f, 1f - y1 * y1));
                        int b = v.Count;
                        v.Add(p.C + ax * (-x0 * r) + ay * (y0 * r));
                        v.Add(p.C + ax * (x0 * r) + ay * (y0 * r));
                        v.Add(p.C + ax * (x1 * r) + ay * (y1 * r));
                        v.Add(p.C + ax * (-x1 * r) + ay * (y1 * r));
                        var c0 = SunColour(y0);
                        var c1 = SunColour(y1);
                        col.Add(c0);
                        col.Add(c0);
                        col.Add(c1);
                        col.Add(c1);
                        uv.Add(new Vector2(0.5f - 0.5f * x0, 0.5f + 0.5f * y0));
                        uv.Add(new Vector2(0.5f + 0.5f * x0, 0.5f + 0.5f * y0));
                        uv.Add(new Vector2(0.5f + 0.5f * x1, 0.5f + 0.5f * y1));
                        uv.Add(new Vector2(0.5f - 0.5f * x1, 0.5f + 0.5f * y1));
                        tri.Add(b);
                        tri.Add(b + 1);
                        tri.Add(b + 2);
                        tri.Add(b);
                        tri.Add(b + 2);
                        tri.Add(b + 3);
                    }
                }
            }

            var mesh = new Mesh { name = name };
            if (v.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetColors(col);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Sunset_Tex's gradient by normalised height: yellow on top, orange, then magenta at the bottom (raw, linear).</summary>
        private static Color32 SunColour(float y)
        {
            var topC = new Color(1f, 0.78f, 0.05f);
            var midC = new Color(1f, 0.3f, 0.08f);
            var lowC = new Color(0.78f, 0.04f, 0.42f);
            var c = y >= 0f ? Color.Lerp(midC, topC, y) : Color.Lerp(midC, lowC, -y);
            return new Color32((byte)Mathf.RoundToInt(c.r * 255f), (byte)Mathf.RoundToInt(c.g * 255f), (byte)Mathf.RoundToInt(c.b * 255f), 255);
        }

        /// <summary>
        /// Dress boxes, 6 outward quads each, white vertex colour. UVs: metres from each face's corner / the look's
        /// metres per repeat (divided by the material's own scale), or 0..1 per face for a picture (the marquee).
        /// </summary>
        private static Mesh BoxMesh(List<Dress> boxes, int start, int n, string name, Surface s)
        {
            var v = new Vector3[n * 24];
            var nr = new Vector3[n * 24];
            var uv = new Vector2[n * 24];
            var col = new Color32[n * 24];
            var tri = new int[n * 36];
            float iu = 1f / (Mathf.Max(0.001f, s.Look.TileU) * s.UvScale.x), iv = 1f / (Mathf.Max(0.001f, s.Look.V) * s.UvScale.y);
            var white = new Color32(255, 255, 255, 255);
            for (int k = 0; k < n; k++)
            {
                var x = boxes[start + k];
                var e = new Vector3(Mathf.Max(0.01f, x.E.x), Mathf.Max(0.01f, x.E.y), Mathf.Max(0.01f, x.E.z));
                for (int f = 0; f < 6; f++)
                {
                    Vector3 fn, fr, fu;
                    Face(f, out fn, out fr, out fu);
                    float en = Along(fn, e), er = Along(fr, e), eu = Along(fu, e);
                    Vector3 c = x.C + x.Q * (fn * en), r = x.Q * (fr * er), u = x.Q * (fu * eu), wn = x.Q * fn;
                    int b = (k * 6 + f) * 4;
                    v[b] = c - r - u;
                    v[b + 1] = c + r - u;
                    v[b + 2] = c + r + u;
                    v[b + 3] = c - r + u;
                    if (s.Look.FaceUv)
                    {
                        PictureUvs(uv, b, r, u, wn);
                    }
                    else if (s.Look.Rotate)
                    {
                        float mu = 2f * er / Mathf.Max(0.001f, s.Look.TileU * s.UvScale.y), mv = 2f * eu / Mathf.Max(0.001f, s.Look.V * s.UvScale.x);
                        uv[b] = new Vector2(0f, 0f);
                        uv[b + 1] = new Vector2(0f, mu);
                        uv[b + 2] = new Vector2(mv, mu);
                        uv[b + 3] = new Vector2(mv, 0f);
                    }
                    else
                    {
                        float mu = 2f * er * iu, mv = 2f * eu * iv;
                        uv[b] = new Vector2(0f, 0f);
                        uv[b + 1] = new Vector2(mu, 0f);
                        uv[b + 2] = new Vector2(mu, mv);
                        uv[b + 3] = new Vector2(0f, mv);
                    }

                    for (int j = 0; j < 4; j++)
                    {
                        nr[b + j] = wn;
                        col[b + j] = white;
                    }

                    int t = (k * 6 + f) * 6;
                    tri[t] = b;
                    tri[t + 1] = b + 1;
                    tri[t + 2] = b + 2;
                    tri[t + 3] = b;
                    tri[t + 4] = b + 2;
                    tri[t + 5] = b + 3;
                }
            }

            var mesh = new Mesh { name = name };
            if (v.Length > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = v;
            mesh.normals = nr;
            mesh.uv = uv;
            mesh.colors32 = col;
            mesh.triangles = tri;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The Armory's dressing mirror (one 3.0 x 2.2 m plate): drawn as an <see cref="RrMirror"/>.</summary>
        public const string MirrorMaterial = "Mirror_Quest_Mat";

        /// <summary>The Foyer's marquee over the stage (a dress box 8.4 x 2.4 m, 0.16 m deep).</summary>
        public const string MarqueeMesh = "Quest_Jumbotron_Marquee";

        /// <summary>
        /// A cover wall's glass pane (Cover_Glass) in its FRAME material: the record has the whole pane's bounds, so a
        /// solid box of it hid the glass - a flat glowing cyan slab (act 5's doorway). The glass record of the same mesh
        /// stays and draws the pane.
        /// </summary>
        private static bool GlassFrame(string mesh, string mat)
        {
            return mesh != null && mesh.StartsWith("Cover_Glass", StringComparison.OrdinalIgnoreCase) && mat.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>
        /// See-through kit whose bounds box would be a solid block (Rec Room's scaffolding trusses: 6 x 1 x 0.7 m boxes
        /// hanging under the ceilings; the studio-lamp tripods): drawn as open frames of thin bars in the same material.
        /// Returns false for anything else (drawn as its box).
        /// </summary>
        private static bool Frame(Dress x, List<Dress> into)
        {
            string n = x.Mesh ?? string.Empty;
            if (n.EndsWith("_vox", StringComparison.OrdinalIgnoreCase))
            {
                // A voxel shell's box (the converter's curved stand-ins): drawn as it is.
                return false;
            }

            var e = x.E;
            float big = Mathf.Max(e.x, Mathf.Max(e.y, e.z)), small = Mathf.Min(e.x, Mathf.Min(e.y, e.z));
            float mid = e.x + e.y + e.z - big - small;
            if (n.StartsWith("Scaffolding", StringComparison.OrdinalIgnoreCase) && small >= 0.15f && big >= 1.5f * mid && big >= 0.8f)
            {
                // Only the whole truss's bounds (6 or 2 m x 1.0 x 0.7 m): a single measured tube or plank of it is drawn as its box.
                Truss(x, into);
                return true;
            }

            if (n.StartsWith("Tripod", StringComparison.OrdinalIgnoreCase) && big >= 0.5f && mid >= 0.3f && small >= 0.3f)
            {
                Tripod(x, into);
                return true;
            }

            return false;
        }

        /// <summary>A box truss: 4 chords along the long axis, square rings every ~1.5 m and zig-zag braces on the 4 sides.</summary>
        private static void Truss(Dress x, List<Dress> into)
        {
            var e = x.E;
            int li = e.x >= e.y && e.x >= e.z ? 0 : (e.y >= e.z ? 1 : 2);
            int ai = (li + 1) % 3, bi = (li + 2) % 3;
            Vector3 L = Axis(li), A = Axis(ai), B = Axis(bi);
            float el = Comp(e, li), ea = Comp(e, ai), eb = Comp(e, bi);
            float t = Mathf.Clamp(Mathf.Min(ea, eb) * 0.1f, 0.012f, 0.03f);
            float ia = Mathf.Max(t, ea - t), ib = Mathf.Max(t, eb - t), il = Mathf.Max(t, el - t);
            for (int sa = -1; sa <= 1; sa += 2)
            {
                for (int sb = -1; sb <= 1; sb += 2)
                {
                    Bar(x, into, A * (sa * ia) + B * (sb * ib), L, el, A, t);
                }
            }

            int seg = Mathf.Max(1, Mathf.RoundToInt(2f * el / 1.5f));
            for (int i = 0; i <= seg; i++)
            {
                float p = -il + 2f * il * i / seg;
                for (int s = -1; s <= 1; s += 2)
                {
                    Bar(x, into, L * p + B * (s * ib), A, ea, B, t);
                    Bar(x, into, L * p + A * (s * ia), B, eb, A, t);
                }

                if (i == seg)
                {
                    break;
                }

                float q = -il + 2f * il * (i + 1) / seg, mid = 0.5f * (p + q), sg = (i & 1) == 0 ? 1f : -1f;
                for (int s = -1; s <= 1; s += 2)
                {
                    // Braces on the faces across A (in the L-B plane) and across B (in the L-A plane).
                    var db = L * (q - p) + B * (2f * sg * ib);
                    Bar(x, into, L * mid + A * (s * ia), db.normalized, 0.5f * db.magnitude, A, t);
                    var da = L * (q - p) + A * (2f * sg * ia);
                    Bar(x, into, L * mid + B * (s * ib), da.normalized, 0.5f * da.magnitude, B, t);
                }
            }
        }

        /// <summary>A lamp tripod: three splayed legs to a hub, a pole to the top and a small lamp head.</summary>
        private static void Tripod(Dress x, List<Dress> into)
        {
            var e = x.E;
            int ui = 0;
            float best = -1f;
            for (int i = 0; i < 3; i++)
            {
                float d = Mathf.Abs((x.Q * Axis(i)).y);
                if (d > best)
                {
                    best = d;
                    ui = i;
                }
            }

            Vector3 up = Axis(ui) * ((x.Q * Axis(ui)).y >= 0f ? 1f : -1f);
            Vector3 A = Axis((ui + 1) % 3), B = Axis((ui + 2) % 3);
            float h = Comp(e, ui), r = 0.85f * Mathf.Min(Comp(e, (ui + 1) % 3), Comp(e, (ui + 2) % 3));
            const float t = 0.02f;
            float hub = Mathf.Min(-h + 0.9f, h * 0.2f), top = h - 0.3f;
            if (top > hub)
            {
                Bar(x, into, up * (0.5f * (hub + top)), up, 0.5f * (top - hub), A, t);
            }

            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.0944f;
                var foot = A * (Mathf.Cos(a) * r) + B * (Mathf.Sin(a) * r) - up * h;
                var leg = foot - up * hub;
                Bar(x, into, 0.5f * (foot + up * hub), leg.normalized, 0.5f * leg.magnitude, Vector3.Cross(leg, up).sqrMagnitude > 1e-6f ? Vector3.Cross(leg, up).normalized : A, t);
            }

            var head = new Vector3(0.18f, 0.18f, 0.18f);
            head[ui] = 0.15f;
            into.Add(new Dress { Mesh = x.Mesh, C = x.C + x.Q * (up * (h - 0.15f)), Q = x.Q, E = head, Col = x.Col, Mat = x.Mat });
        }

        /// <summary>A thin square bar of a frame: centre c and unit direction d in the dress box's local space, half length h.</summary>
        private static void Bar(Dress x, List<Dress> into, Vector3 c, Vector3 d, float h, Vector3 side, float t)
        {
            if (h <= 0.001f)
            {
                return;
            }

            // Box local z along the bar, local y along a side axis perpendicular to it.
            var sideOrtho = side - d * Vector3.Dot(side, d);
            var q = Quaternion.LookRotation(d, sideOrtho.sqrMagnitude > 1e-8f ? sideOrtho.normalized : Vector3.up);
            into.Add(new Dress { Mesh = x.Mesh, C = x.C + x.Q * c, Q = x.Q * q, E = new Vector3(t, t, h + t), Col = x.Col, Mat = x.Mat });
        }

        private static Vector3 Axis(int i)
        {
            return i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
        }

        private static float Comp(Vector3 v, int i)
        {
            return i == 0 ? v.x : i == 1 ? v.y : v.z;
        }

        /// <summary>
        /// "THE RISE OF JUMBOTRON" in neon on both faces of the marquee board, as the footage shows it (cyan letters, a pink
        /// outline; DN/research/rr_jumbotron_look.md). The level's own marquee texture is level-only, so the lettering is ours.
        /// HDR face and outline colours so the post's bloom picks them up; the TMP material copies are freed with the room.
        /// </summary>
        private static void Marquee(Transform parent, Dress x, Owned owned)
        {
            // The board is thin along one local axis: its two big faces look along it. Text up = the other axis nearest world up.
            Vector3 ax = x.Q * Vector3.right, ay = x.Q * Vector3.up, az = x.Q * Vector3.forward;
            Vector3 n = ay;
            float depth = x.E.y, w = x.E.x, hgt = x.E.z;
            Vector3 side = ax, up = az;
            if (x.E.x <= x.E.y && x.E.x <= x.E.z)
            {
                n = ax;
                depth = x.E.x;
                side = ay;
                up = az;
                w = x.E.y;
                hgt = x.E.z;
            }
            else if (x.E.z <= x.E.x && x.E.z <= x.E.y)
            {
                n = az;
                depth = x.E.z;
                side = ax;
                up = ay;
                w = x.E.x;
                hgt = x.E.y;
            }

            if (Mathf.Abs(side.y) > Mathf.Abs(up.y))
            {
                var t = side;
                side = up;
                up = t;
                float f = w;
                w = hgt;
                hgt = f;
            }

            if (up.y < 0f)
            {
                up = -up;
            }

            for (int s = 0; s < 2; s++)
            {
                var face = s == 0 ? n : -n;
                // TMP reads from the side its +z looks away from: forward into the board.
                var tmp = RrBuild.Sign(parent, "Marquee " + (s == 0 ? "Front" : "Back"), "THE RISE OF\nJUMBOTRON", x.C + face * (depth + 0.02f), Quaternion.LookRotation(-face, up), 9f, Color.white, true, w * 1.8f);
                tmp.rectTransform.sizeDelta = new Vector2(w * 1.8f, hgt * 1.7f);
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 2f;
                tmp.fontSizeMax = 12f;
                tmp.lineSpacing = -10f;
                var m = tmp.fontMaterial;
                owned.Things.Add(m);
                m.SetColor("_FaceColor", new Color(0.25f, 2.6f, 3.2f, 1f));
                m.SetColor("_OutlineColor", new Color(3.2f, 0.35f, 2.2f, 1f));
                m.SetFloat("_OutlineWidth", 0.22f);
            }
        }

        /// <summary>
        /// 0..1 UVs for a picture face (the marquee) that read the right way round from outside. The quad's corners are
        /// c - r - u, c + r - u, c + r + u, c - r + u (from b). V runs along the face axis nearest to up, pointing up; U
        /// runs to the viewer's right, Cross(n, V) (Unity is left-handed, so u along r would read mirrored). A dress box's
        /// own axes can be rolled (the Foyer marquee read mirrored from the seats and upside down from the stage).
        /// </summary>
        private static void PictureUvs(Vector2[] uv, int b, Vector3 r, Vector3 u, Vector3 n)
        {
            Vector3 rd = r.normalized, ud = u.normalized;
            Vector3 vd = Mathf.Abs(rd.y) > Mathf.Abs(ud.y) ? rd : ud;
            if (vd.y < 0f)
            {
                vd = -vd;
            }

            Vector3 hd = Vector3.Cross(n.normalized, vd);
            for (int j = 0; j < 4; j++)
            {
                float a = j == 1 || j == 2 ? 1f : -1f, c = j >= 2 ? 1f : -1f;
                Vector3 d = rd * a + ud * c;
                uv[b + j] = new Vector2(0.5f + 0.5f * Vector3.Dot(d, hd), 0.5f + 0.5f * Vector3.Dot(d, vd));
            }
        }

        /// <summary>Box face f: outward normal n and the quad's axes r, u with Cross(r, u) = n (front face, as the plates).</summary>
        private static void Face(int f, out Vector3 n, out Vector3 r, out Vector3 u)
        {
            switch (f)
            {
                case 0:
                    n = Vector3.forward; r = Vector3.right; u = Vector3.up;
                    break;
                case 1:
                    n = Vector3.back; r = Vector3.left; u = Vector3.up;
                    break;
                case 2:
                    n = Vector3.right; r = Vector3.back; u = Vector3.up;
                    break;
                case 3:
                    n = Vector3.left; r = Vector3.forward; u = Vector3.up;
                    break;
                case 4:
                    n = Vector3.up; r = Vector3.right; u = Vector3.back;
                    break;
                default:
                    n = Vector3.down; r = Vector3.right; u = Vector3.forward;
                    break;
            }
        }

        /// <summary>The half extent e along a local unit axis.</summary>
        private static float Along(Vector3 axis, Vector3 e)
        {
            return Mathf.Abs(axis.x) * e.x + Mathf.Abs(axis.y) * e.y + Mathf.Abs(axis.z) * e.z;
        }
    }
}
