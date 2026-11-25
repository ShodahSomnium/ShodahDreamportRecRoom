using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// The scene kit's terrain pass for RRSK "H" records (an addition of 2026-09-24; <see cref="RrSceneKit"/> itself
    /// skips H as an unknown record). The Android build's levels keep their ground as Unity Terrains and big ground
    /// meshes that are not in the PC install; the converter samples them into height grids, and this pass turns the
    /// grids back into meshes and colliders textured with install materials.
    ///
    /// <see cref="Parse"/> is pure (worker-safe) and reads the decoded RRSK text. <see cref="Build"/> is a coroutine
    /// under the same frame budget as the kit: chunks of at most <see cref="Options.ChunkCells"/> cells a side
    /// (a 48 x 48 chunk is 2,401 vertices and 4,608 triangles), UVs in scene metres divided by the tile size (so one
    /// shared material tiles evenly across chunks), white vertex colours, the environment layer; near grids cast shadows (iteration 8).
    /// Rules:
    /// - a mesh grid drops cells whose corners differ by more than <see cref="Options.MaxStep"/> (a roof edge above a
    ///   floor would otherwise become a sloped sheet); terrain grids keep every cell;
    /// - a far grid ("far=1", visual only) leaves out the cells its near grid covers and sits
    ///   <see cref="Options.FarDrop"/> lower, so the fine play-area surface is always the one drawn;
    /// - collider=1 grids get a MeshCollider (pre-cooked on the worker like the kit's mesh colliders);
    /// - <see cref="Options.ClipRadius"/> leaves out cells beyond a radius (keeps a room inside its 1.8 km square).
    /// </summary>
    public static class RrSceneKitTerrain
    {
        public const string Area = "SceneKit";

        public sealed class Options
        {
            /// <summary>Pose of the scene frame in the parent (the same as the kit's options for the same scene).</summary>
            public Vector3 Position;

            public Quaternion Rotation = Quaternion.identity;

            /// <summary>Metres per texture repeat (per grid through <see cref="TileFor"/>).</summary>
            public float Tile = 4f;

            public Func<RrSceneKitGrid, float> TileFor;

            /// <summary>Largest corner height difference of a drawn cell in a mesh grid, metres.</summary>
            public float MaxStep = 2.5f;

            /// <summary>Which grids drop steep cells (default: mesh grids; cliffs and hills should keep theirs).</summary>
            public Func<RrSceneKitGrid, bool> CutSteep;

            /// <summary>How much lower a far grid is drawn than its heights (metres).</summary>
            public float FarDrop = 0.08f;

            /// <summary>0 = no clip; else cells whose centre is farther than this (x/z) from ClipCenter are left out.</summary>
            public float ClipRadius;

            /// <summary>Scene-frame centre of the clip circle.</summary>
            public Vector3 ClipCenter;

            public int ChunkCells = 48;
            public bool Colliders = true;
            public int Layer = RrLayers.Environment;
            public float FrameBudgetMs = 4f;
            public bool BakeCollidersOnWorker = true;
            public string RootName;
        }

        public sealed class Result
        {
            public GameObject Root;
            public int Grids;
            public int Chunks;
            public int Triangles;
            public int ColliderChunks;
            public int CellsCut;
            public int CellsClipped;
            public int CellsSteep;
            public int Baked;
            public float BuildMs;
            public float LongestStepMs;
            public readonly List<string> Notes = new List<string>();

            public string Summary()
            {
                return Grids + " grids, " + Chunks + " chunks, " + Triangles + " triangles, " + ColliderChunks + " collider chunks (" + Baked +
                       " baked), cells cut under near grids " + CellsCut + ", clipped " + CellsClipped + ", steep " + CellsSteep + ", " +
                       BuildMs.ToString("0") + " ms (longest step " + LongestStepMs.ToString("0.0") + " ms)";
            }
        }

        // ------------------------------------------------------------------ parse

        /// <summary>Reads every "H grid" / "H row" record of decoded RRSK text (pure; safe on the worker).</summary>
        public static List<RrSceneKitGrid> Parse(string text)
        {
            var list = new List<RrSceneKitGrid>();
            if (string.IsNullOrEmpty(text))
            {
                return list;
            }

            var byId = new Dictionary<string, RrSceneKitGrid>();
            int pos = 0, n = text.Length;
            var tok = new List<string>(80);
            while (pos < n)
            {
                int end = text.IndexOf('\n', pos);
                if (end < 0)
                {
                    end = n;
                }

                if (end - pos > 7 && text[pos] == 'H' && text[pos + 1] == ' ')
                {
                    Split(text, pos, end, tok);
                    if (tok.Count >= 10 && tok[1] == "grid")
                    {
                        var g = new RrSceneKitGrid { Id = tok[2] };
                        g.Name = NameOf(g.Id);
                        g.Origin = new Vector3(F(tok[3]), F(tok[4]), F(tok[5]));
                        g.Dx = F(tok[6]);
                        g.Dz = F(tok[7]);
                        g.Nx = (int)F(tok[8]);
                        g.Nz = (int)F(tok[9]);
                        if (g.Nx >= 2 && g.Nz >= 2 && g.Nx * g.Nz <= 4000000 && g.Dx > 0f && g.Dz > 0f)
                        {
                            g.H = new float[g.Nx * g.Nz];
                            for (int k = 0; k < g.H.Length; k++)
                            {
                                g.H[k] = float.NaN;
                            }

                            for (int k = 10; k < tok.Count; k++)
                            {
                                int eq = tok[k].IndexOf('=');
                                if (eq > 0)
                                {
                                    g.Keys[tok[k].Substring(0, eq)] = Unescape(tok[k].Substring(eq + 1));
                                }
                            }

                            byId[g.Id] = g;
                            list.Add(g);
                        }
                    }
                    else if (tok.Count >= 4 && tok[1] == "row")
                    {
                        RrSceneKitGrid g;
                        int j;
                        if (byId.TryGetValue(tok[2], out g) && int.TryParse(tok[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out j) && j >= 0 && j < g.Nz)
                        {
                            int count = Mathf.Min(g.Nx, tok.Count - 4);
                            for (int i = 0; i < count; i++)
                            {
                                string s = tok[4 + i];
                                float v;
                                if (s != "n" && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                                {
                                    g.H[j * g.Nx + i] = g.Origin.y + v;
                                }
                            }
                        }
                    }
                }

                pos = end + 1;
            }

            return list;
        }

        private static void Split(string t, int start, int end, List<string> into)
        {
            into.Clear();
            int i = start;
            while (i < end)
            {
                while (i < end && (t[i] == ' ' || t[i] == '\r' || t[i] == '\t'))
                {
                    i++;
                }

                int s = i;
                while (i < end && t[i] != ' ' && t[i] != '\r' && t[i] != '\t')
                {
                    i++;
                }

                if (i > s)
                {
                    into.Add(t.Substring(s, i - s));
                }
            }
        }

        private static float F(string s)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        private static string Unescape(string s)
        {
            return s.IndexOf('%') < 0 ? s : s.Replace("%20", " ").Replace("%25", "%");
        }

        private static string NameOf(string id)
        {
            int hash = id.LastIndexOf('#');
            string n = hash > 0 ? id.Substring(0, hash) : id;
            if (n.EndsWith("~far", StringComparison.Ordinal))
            {
                n = n.Substring(0, n.Length - 4);
            }

            return Unescape(n);
        }

        // ------------------------------------------------------------------ build

        /// <summary>
        /// Builds the grids under parent (scene frame placed by options). material gives each drawn grid its material
        /// (null = collider only). done gets the result (never null).
        /// </summary>
        public static IEnumerator Build(List<RrSceneKitGrid> grids, Transform parent, Options options, Func<RrSceneKitGrid, Material> material, Action<Result> done)
        {
            var o = options ?? new Options();
            var r = new Result();
            var total = Stopwatch.StartNew();
            var step = Stopwatch.StartNew();
            if (grids == null || grids.Count == 0 || parent == null)
            {
                if (done != null)
                {
                    done(r);
                }

                yield break;
            }

            var root = new GameObject(string.IsNullOrEmpty(o.RootName) ? "Terrain" : o.RootName);
            root.layer = o.Layer;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = o.Position;
            root.transform.localRotation = o.Rotation;
            r.Root = root;
            var bake = new HashSet<Mesh>();
            foreach (var g in grids)
            {
                if (g == null || g.H == null)
                {
                    continue;
                }

                bool vis = g.Visible;
                bool col = o.Colliders && g.Collider && !g.Far;
                if (!vis && !col)
                {
                    continue;
                }

                r.Grids++;
                RrSceneKitGrid near = null;
                if (g.Far)
                {
                    foreach (var other in grids)
                    {
                        if (other != g && !other.Far && other.Name == g.Name && other.Visible)
                        {
                            near = other;
                            break;
                        }
                    }
                }

                Material mat = vis && material != null ? material(g) : null;
                float tile = o.TileFor != null ? o.TileFor(g) : o.Tile;
                if (tile <= 0.01f)
                {
                    tile = o.Tile;
                }

                bool steepCut = o.CutSteep != null ? o.CutSteep(g) : g.Source == "mesh";
                float drop = g.Far ? o.FarDrop : 0f;
                int cs = Mathf.Clamp(o.ChunkCells, 4, 120);
                var grp = new GameObject(g.Name + (g.Far ? " (far)" : ""));
                grp.layer = o.Layer;
                grp.transform.SetParent(root.transform, false);
                for (int cj = 0; cj < g.Nz - 1; cj += cs)
                {
                    for (int ci = 0; ci < g.Nx - 1; ci += cs)
                    {
                        var mesh = Chunk(g, ci, cj, Mathf.Min(cs, g.Nx - 1 - ci), Mathf.Min(cs, g.Nz - 1 - cj), near, steepCut ? o.MaxStep : -1f, drop, tile, o, r);
                        if (mesh != null)
                        {
                            var go = new GameObject("Chunk " + ci + "," + cj);
                            go.layer = o.Layer;
                            go.transform.SetParent(grp.transform, false);
                            if (vis && mat != null)
                            {
                                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                                var mr = go.AddComponent<MeshRenderer>();
                                mr.sharedMaterial = mat;
                                // Iteration 8: the play-area ground casts (hills, banks) as Rec Room's terrains did under its key light;
                                // the far backdrop does not (beyond the client's 50-250 m shadow distance anyway).
                                mr.shadowCastingMode = g.Far ? ShadowCastingMode.Off : ShadowCastingMode.On;
                                mr.lightProbeUsage = LightProbeUsage.Off;
                                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                            }

                            go.AddComponent<RrSceneKitTerrainChunk>().Mesh = mesh;
                            if (col)
                            {
                                bake.Add(mesh);
                                r.ColliderChunks++;
                            }

                            r.Chunks++;
                        }

                        if (step.Elapsed.TotalMilliseconds > o.FrameBudgetMs)
                        {
                            r.LongestStepMs = Mathf.Max(r.LongestStepMs, (float)step.Elapsed.TotalMilliseconds);
                            yield return null;
                            step.Reset();
                            step.Start();
                            if (root == null)
                            {
                                r.Notes.Add("cancelled: root destroyed");
                                if (done != null)
                                {
                                    done(r);
                                }

                                yield break;
                            }
                        }
                    }
                }
            }

            // Colliders: cook on the worker first (thread-safe Physics.BakeMesh), then assign.
            var worker = RrMain.Instance != null ? RrMain.Instance.Worker : null;
            if (bake.Count > 0 && o.BakeCollidersOnWorker && worker != null && worker.Threaded)
            {
                int pending = 0;
                foreach (var m in bake)
                {
                    int id = m.GetInstanceID();
                    pending++;
                    worker.Post(() =>
                    {
#pragma warning disable 618
                        Physics.BakeMesh(id, false);
#pragma warning restore 618
                        return null;
                    }, (res, e) =>
                    {
                        pending--;
                        if (e == null)
                        {
                            r.Baked++;
                        }
                    });
                }

                float until = Time.realtimeSinceStartup + 20f;
                while (pending > 0 && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                }

                step.Reset();
                step.Start();
            }

            if (root != null)
            {
                foreach (var c in root.GetComponentsInChildren<RrSceneKitTerrainChunk>(true))
                {
                    if (c.Mesh != null && bake.Contains(c.Mesh))
                    {
                        var mc = c.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = c.Mesh;
                    }

                    if (step.Elapsed.TotalMilliseconds > o.FrameBudgetMs)
                    {
                        r.LongestStepMs = Mathf.Max(r.LongestStepMs, (float)step.Elapsed.TotalMilliseconds);
                        yield return null;
                        step.Reset();
                        step.Start();
                        if (root == null)
                        {
                            break;
                        }
                    }
                }
            }

            r.LongestStepMs = Mathf.Max(r.LongestStepMs, (float)step.Elapsed.TotalMilliseconds);
            r.BuildMs = (float)total.Elapsed.TotalMilliseconds;
            RrLog.Info(Area, "terrain: " + r.Summary());
            if (done != null)
            {
                done(r);
            }
        }

        private static Mesh Chunk(RrSceneKitGrid g, int i0, int j0, int ci, int cj, RrSceneKitGrid near, float maxStep, float drop, float tile, Options o, Result r)
        {
            int vx = ci + 1, vz = cj + 1;
            var verts = new List<Vector3>(vx * vz);
            var norms = new List<Vector3>(vx * vz);
            var uvs = new List<Vector2>(vx * vz);
            var tris = new List<int>(ci * cj * 6);
            float inv = 1f / tile;
            for (int j = 0; j < vz; j++)
            {
                for (int i = 0; i < vx; i++)
                {
                    int gi = i0 + i, gj = j0 + j;
                    float h = g.Height(gi, gj);
                    float x = g.Origin.x + gi * g.Dx, z = g.Origin.z + gj * g.Dz;
                    verts.Add(new Vector3(x, float.IsNaN(h) ? g.Origin.y : h - drop, z));
                    norms.Add(Normal(g, gi, gj));
                    uvs.Add(new Vector2(x * inv, z * inv));
                }
            }

            float clip2 = o.ClipRadius > 0f ? o.ClipRadius * o.ClipRadius : -1f;
            for (int j = 0; j < cj; j++)
            {
                for (int i = 0; i < ci; i++)
                {
                    int gi = i0 + i, gj = j0 + j;
                    float a = g.Height(gi, gj), b = g.Height(gi + 1, gj), c = g.Height(gi, gj + 1), d = g.Height(gi + 1, gj + 1);
                    if (float.IsNaN(a) || float.IsNaN(b) || float.IsNaN(c) || float.IsNaN(d))
                    {
                        continue;
                    }

                    float cx = g.Origin.x + (gi + 0.5f) * g.Dx, cz = g.Origin.z + (gj + 0.5f) * g.Dz;
                    if (near != null && near.Covers(cx, cz))
                    {
                        r.CellsCut++;
                        continue;
                    }

                    if (clip2 > 0f)
                    {
                        float ddx = cx - o.ClipCenter.x, ddz = cz - o.ClipCenter.z;
                        if (ddx * ddx + ddz * ddz > clip2)
                        {
                            r.CellsClipped++;
                            continue;
                        }
                    }

                    if (maxStep > 0f && Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d)) - Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d)) > maxStep)
                    {
                        r.CellsSteep++;
                        continue;
                    }

                    int v00 = j * vx + i, v10 = v00 + 1, v01 = v00 + vx, v11 = v01 + 1;
                    // Split along the diagonal with the smaller height difference (ridges and valleys stay sharp).
                    if (Mathf.Abs(a - d) <= Mathf.Abs(b - c))
                    {
                        tris.Add(v00); tris.Add(v11); tris.Add(v10);
                        tris.Add(v00); tris.Add(v01); tris.Add(v11);
                    }
                    else
                    {
                        tris.Add(v00); tris.Add(v01); tris.Add(v10);
                        tris.Add(v10); tris.Add(v01); tris.Add(v11);
                    }
                }
            }

            if (tris.Count == 0)
            {
                return null;
            }

            var mesh = new Mesh { name = "Rr Terrain " + g.Name + " " + i0 + "," + j0 };
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            var tangents = new Vector4[verts.Count];
            var colors = new Color32[verts.Count];
            for (int k = 0; k < tangents.Length; k++)
            {
                tangents[k] = new Vector4(1f, 0f, 0f, -1f);
                colors[k] = new Color32(255, 255, 255, 255);
            }

            mesh.tangents = tangents;
            mesh.colors32 = colors;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            r.Triangles += tris.Count / 3;
            return mesh;
        }

        private static Vector3 Normal(RrSceneKitGrid g, int i, int j)
        {
            float h = g.Height(i, j);
            if (float.IsNaN(h))
            {
                return Vector3.up;
            }

            float l = Valid(g, i - 1, j, h), rr = Valid(g, i + 1, j, h), d = Valid(g, i, j - 1, h), u = Valid(g, i, j + 1, h);
            var n = new Vector3((l - rr) / (2f * g.Dx), 1f, (d - u) / (2f * g.Dz));
            return n.normalized;
        }

        private static float Valid(RrSceneKitGrid g, int i, int j, float fallback)
        {
            if (i < 0 || j < 0 || i >= g.Nx || j >= g.Nz)
            {
                return fallback;
            }

            float v = g.Height(i, j);
            return float.IsNaN(v) ? fallback : v;
        }

        /// <summary>Height of the grid surface at a scene (x, z), NaN outside or over a hole (bilinear).</summary>
        public static float Sample(RrSceneKitGrid g, float x, float z)
        {
            if (g == null || g.H == null)
            {
                return float.NaN;
            }

            float u = (x - g.Origin.x) / g.Dx, v = (z - g.Origin.z) / g.Dz;
            if (u < 0f || v < 0f || u > g.Nx - 1 || v > g.Nz - 1)
            {
                return float.NaN;
            }

            int i = Mathf.Min((int)u, g.Nx - 2), j = Mathf.Min((int)v, g.Nz - 2);
            float fx = u - i, fz = v - j;
            float a = g.Height(i, j), b = g.Height(i + 1, j), c = g.Height(i, j + 1), d = g.Height(i + 1, j + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }
    }
}
