using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Harness hooks for iteration 9's lighting (scenario light9_tour.json, "call" steps; nothing here runs in the client
    /// unless a scenario calls it). Each Pair shot renders the same camera twice with post-processing: "_before" with
    /// Rec Room's light grid off (iteration 8: Rec Room's shaders lit by the sun and ambient only, Unity lights at the
    /// records' own numbers) and "_after" with it on.
    /// </summary>
    public static class RrLightTest
    {
        private const string Area = "LightTest";

        public static void GridOff()
        {
            RrLightGrid.SetEnabled(false);
        }

        public static void GridOn()
        {
            RrLightGrid.SetEnabled(true);
        }

        /// <summary>Waits (up to 15 s) until every grid on an active object has built, then logs the report.</summary>
        public static IEnumerator GridWait()
        {
            float until = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < until)
            {
                bool all = true;
                foreach (var g in RrLightGrid.All)
                {
                    if (g != null && g.isActiveAndEnabled && !g.Built)
                    {
                        all = false;
                    }
                }

                if (all)
                {
                    break;
                }

                yield return null;
            }

            Report();
        }

        /// <summary>The grids, and the current room's renderers: how many use Rec Room's grid shaders, how many are lit by a grid, draw estimate.</summary>
        public static void Report()
        {
            RrLog.Info(Area, RrLightGrid.Report());
            var room = Rooms.RrTravel.CurrentRoom;
            if (room == null)
            {
                return;
            }

            int total = 0, grid = 0, lit = 0, urp = 0, draws = 0, gridDraws = 0, lights = 0, shadowed = 0;
            foreach (var r in room.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer)
                {
                    continue;
                }

                total++;
                int mats = r.sharedMaterials.Length;
                draws += mats;
                if (RrLightGrid.UsesGrid(r))
                {
                    grid++;
                    if (RrLightGrid.Of(r) != null && r.HasPropertyBlock())
                    {
                        lit++;
                        gridDraws += mats;
                    }
                }
                else
                {
                    urp++;
                }
            }

            foreach (var l in room.GetComponentsInChildren<Light>(false))
            {
                if (l != null && l.enabled && l.type != LightType.Directional)
                {
                    lights++;
                    if (l.shadows != LightShadows.None)
                    {
                        shadowed++;
                    }
                }
            }

            int limit = -1;
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (rp != null)
            {
                limit = rp.maxAdditionalLightsCount;
            }

            RrLog.Info(Area, "room " + room.name + ": " + total + " active renderers (" + draws + " material draws): " + grid + " with Rec Room's grid shaders (" + lit + " lit by a grid, "
                + gridDraws + " draws outside the SRP Batcher), " + urp + " URP-shaded (their lights: URP's per-object limit " + limit + "); " + lights + " active Unity point/spot lights ("
                + shadowed + " shadowed)");
        }

        /// <summary>
        /// What reaches Rec Room's surfaces in the Golden Trophy lobby, from the grid (the shader's own formula): the floor
        /// under each wall torch and 3 m from it, and the spawn.
        /// </summary>
        public static void ProbeFoyer()
        {
            Probe("GoldenTrophy_Foyer", new[]
            {
                new Vector3(115.2f, -6.5f, 45.8f), new Vector3(114.96f, -6.5f, 43.4f), new Vector3(114.96f, -6.5f, 48.2f), new Vector3(117.3f, -6.5f, 43.4f),
                new Vector3(112.0f, -6.5f, 45.8f), new Vector3(110.0f, -6.5f, 45.8f), new Vector3(108.3f, -6.5f, 43.5f), new Vector3(98.7f, -6.5f, 46.1f)
            });
        }

        private static void Probe(string key, Vector3[] scenePoints)
        {
            var root = RrEnvironment.SceneRoot(key);
            if (root == null)
            {
                RrLog.Warn(Area, "probe: no scene " + key);
                return;
            }

            var grid = root.GetComponent<RrLightGrid>();
            if (grid == null || !grid.Built)
            {
                RrLog.Warn(Area, "probe: " + key + " has no built grid");
                return;
            }

            var sb = new StringBuilder(key + " floor light (Rec Room's formula, linear, up-facing):");
            foreach (var p in scenePoints)
            {
                int n;
                var w = root.TransformPoint(p);
                var c = grid.Probe(w + Vector3.up * 0.01f, Vector3.up, out n);
                sb.Append(' ').Append(p.ToString("F1")).Append(": ").Append(c.x.ToString("0.00", CultureInfo.InvariantCulture)).Append('/').Append(c.y.ToString("0.00", CultureInfo.InvariantCulture))
                  .Append('/').Append(c.z.ToString("0.00", CultureInfo.InvariantCulture)).Append(" (").Append(n).Append(" lights in cell)");
            }

            RrLog.Info(Area, sb.ToString());
        }

        /// <summary>The scene grid's scope: renderers per shader (instancing on/off), how many the grid claimed and hold a block.</summary>
        public static void DescribeScope(string key)
        {
            var root = RrEnvironment.SceneRoot(key);
            var grid = root != null ? root.GetComponent<RrLightGrid>() : null;
            if (grid == null || grid.Scope == null)
            {
                RrLog.Warn(Area, "scope: no grid for " + key);
                return;
            }

            var rows = new SortedDictionary<string, int[]>();
            foreach (var r in grid.Scope.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || r is ParticleSystemRenderer)
                {
                    continue;
                }

                var m = r.sharedMaterial;
                string k = (m != null && m.shader != null ? m.shader.name : "?") + (m != null && m.enableInstancing ? " [inst]" : "") + " q" + (m != null ? m.renderQueue : 0);
                int[] v;
                if (!rows.TryGetValue(k, out v))
                {
                    v = new int[3];
                    rows[k] = v;
                }

                v[0]++;
                if (RrLightGrid.Of(r) == grid)
                {
                    v[1]++;
                }

                if (r.HasPropertyBlock())
                {
                    v[2]++;
                }
            }

            var sb = new StringBuilder(key + " grid scope " + grid.Scope.name + ":");
            foreach (var kv in rows)
            {
                sb.Append(" [").Append(kv.Key).Append(": ").Append(kv.Value[0]).Append(" / grid ").Append(kv.Value[1]).Append(" / block ").Append(kv.Value[2]).Append(']');
            }

            RrLog.Info(Area, sb.ToString());
        }

        public static void DescribeJumboFoyer()
        {
            DescribeScope("Jumbotron_Foyer");
        }

        /// <summary>
        /// Harness experiment: the Jumbotron foyer again with GPU instancing off on the grid-lit materials that have it (our
        /// maker-pen copies), to tell whether an instanced draw drops a property block's non-instanced values.
        /// </summary>
        public static IEnumerator PairJumboNoInstancing()
        {
            var root = RrEnvironment.SceneRoot("Jumbotron_Foyer");
            var grid = root != null ? root.GetComponent<RrLightGrid>() : null;
            int n = 0;
            if (grid != null && grid.Scope != null)
            {
                foreach (var r in grid.Scope.GetComponentsInChildren<Renderer>(false))
                {
                    if (RrLightGrid.Of(r) != grid)
                    {
                        continue;
                    }

                    foreach (var m in r.sharedMaterials)
                    {
                        if (m != null && m.enableInstancing)
                        {
                            m.enableInstancing = false;
                            n++;
                        }
                    }
                }
            }

            RrLog.Info(Area, "instancing off on " + n + " grid-lit material slots in Jumbotron_Foyer");
            var e = PairScene("Jumbotron_Foyer", "light9_jt_foyer_noinst", new Vector3(12.0f, -5.0f, 54.5f), new Vector3(35.0f, -5.4f, 54.5f), 75f);
            while (e.MoveNext())
            {
                yield return e.Current;
            }

            Probe("Jumbotron_Foyer", new[] { new Vector3(22f, -4f, 50.5f), new Vector3(22f, -4f, 58.5f), new Vector3(30f, -6.5f, 54.5f), new Vector3(20f, -6.5f, 54.5f) });
        }

        /// <summary>
        /// CPU cost of the grid's property blocks (they take renderers out of the SRP Batcher): 40 renders of the current
        /// room from its spawn view with the grids off, then on, the mean Camera.Render time each (post off, 1280x720).
        /// </summary>
        public static IEnumerator PerfRoom()
        {
            var room = Rooms.RrTravel.CurrentRoom;
            if (room == null)
            {
                yield break;
            }

            var cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : room.transform.position + Vector3.up * 1.6f;
            Quaternion rot = cam != null ? cam.transform.rotation : room.transform.rotation;
            var go = new GameObject("Light Perf Camera");
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var c = go.AddComponent<Camera>();
            c.enabled = false;
            c.targetTexture = rt;
            c.fieldOfView = 80f;
            c.farClipPlane = 2000f;
            go.transform.SetPositionAndRotation(eye, rot);
            var sb = new StringBuilder("perf " + room.name + " from " + eye.ToString("F1") + ":");
            try
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    RrLightGrid.SetEnabled(pass > 0);
                    RrLightGrid.SetConstantBufferMode(pass == 1);
                    c.Render();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < 40; i++)
                    {
                        c.Render();
                    }

                    sb.Append(pass == 0 ? " grids off " : pass == 1 ? ", grids on (bound cbuffer) " : ", grids on (arrays) ").Append((sw.Elapsed.TotalMilliseconds / 40.0).ToString("0.00", CultureInfo.InvariantCulture)).Append(" ms per render");
                }
            }
            finally
            {
                RrLightGrid.SetEnabled(true);
                RrLightGrid.SetConstantBufferMode(true);
                c.targetTexture = null;
                Object.Destroy(go);
                rt.Release();
                Object.Destroy(rt);
            }

            RrLog.Info(Area, sb.ToString());
            yield return null;
        }

        // ---- before/after pairs ----

        /// <summary>The Golden Trophy lobby from the players' spawn, looking at the start hologram between the torches.</summary>
        public static IEnumerator PairFoyer()
        {
            return PairScene("GoldenTrophy_Foyer", "light9_gt_foyer", new Vector3(117.9f, -4.85f, 45.8f), new Vector3(108.3f, -5.1f, 44.2f), 75f);
        }

        /// <summary>The lobby from the stage end: the lamp posts and hanging lanterns down the hall.</summary>
        public static IEnumerator PairFoyerHall()
        {
            return PairScene("GoldenTrophy_Foyer", "light9_gt_hall", new Vector3(104.0f, -4.9f, 45.8f), new Vector3(86.0f, -5.4f, 46.5f), 75f);
        }

        public static IEnumerator PairArmory()
        {
            return PairCentre("GoldenTrophy_Armory", "light9_gt_armory");
        }

        /// <summary>A wall torch of act 1, close: its flame, the wall and the floor under it.</summary>
        public static IEnumerator PairAct1Torch()
        {
            return PairTorch("GoldenTrophy_Hallway1", "light9_gt_act1_torch", 2);
        }

        public static IEnumerator PairAct1()
        {
            return PairCentre("GoldenTrophy_Hallway1", "light9_gt_act1");
        }

        /// <summary>The Paintball lobby's interior from the pregame spawns (its 179-degree downlights and bounce light).</summary>
        public static IEnumerator PairPbInterior()
        {
            return PairScene("Paintball_Lobby", "light9_pb_interior", new Vector3(-52.0f, -3.65f, 62.0f), new Vector3(-47.5f, -4.6f, 70.0f), 75f);
        }

        public static IEnumerator PairJumboFoyer()
        {
            // from the audience (floor y -6.6) toward the stage and its key light
            return PairScene("Jumbotron_Foyer", "light9_jt_foyer", new Vector3(12.0f, -5.0f, 54.5f), new Vector3(35.0f, -5.4f, 54.5f), 75f);
        }

        private static IEnumerator PairCentre(string key, string name)
        {
            Bounds b;
            if (!RrEnvironment.SceneWorldBounds(key, out b))
            {
                RrLog.Warn(Area, "pair " + name + ": no registered scene " + key + "; " + RrEnvironment.DescribeScenes());
                yield break;
            }

            RaycastHit hit;
            Vector3 eye = Physics.Raycast(b.center, Vector3.down, out hit, b.extents.y + 5f, 1 << RrLayers.Environment) ? hit.point + Vector3.up * 1.6f : b.center;
            Vector3 along = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
            var e = PairWorld(key, name, eye, eye + along * 10f - Vector3.up * 0.3f, 80f);
            while (e.MoveNext())
            {
                yield return e.Current;
            }
        }

        private static IEnumerator PairScene(string key, string name, Vector3 eyeScene, Vector3 lookScene, float fov)
        {
            var root = RrEnvironment.SceneRoot(key);
            if (root == null)
            {
                RrLog.Warn(Area, "pair " + name + ": no scene " + key);
                yield break;
            }

            var e = PairWorld(key, name, root.TransformPoint(eyeScene), root.TransformPoint(lookScene), fov);
            while (e.MoveNext())
            {
                yield return e.Current;
            }
        }

        private static IEnumerator PairTorch(string key, string name, int index)
        {
            var root = RrEnvironment.SceneRoot(key);
            var fx = root != null ? root.GetComponentInChildren<RrSceneKitFx>(true) : null;
            if (fx == null || fx.OnWalls.Count == 0)
            {
                RrLog.Warn(Area, "pair " + name + ": " + key + " has no placed wall torch");
                yield break;
            }

            var ray = fx.OnWalls[Mathf.Clamp(index, 0, fx.OnWalls.Count - 1)];
            var n = ray.direction;
            n.y = 0f;
            n = n.sqrMagnitude > 1e-4f ? n.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, n);
            var eye = ray.origin + n * 3.2f + side * 1.2f + Vector3.up * 0.2f;
            var look = ray.origin - Vector3.up * 0.9f;
            var e = PairWorld(key, name, eye, look, 70f);
            while (e.MoveNext())
            {
                yield return e.Current;
            }
        }

        private static IEnumerator PairWorld(string key, string name, Vector3 eye, Vector3 look, float fov)
        {
            string was = RrEnvironment.ForcedKey;
            RrEnvironment.ForcedKey = key;
            RrLightGrid.SetEnabled(false);
            yield return null;
            yield return null;
            Capture(name + "_before", eye, look, fov);
            RrLightGrid.SetEnabled(true);
            yield return null;
            yield return null;
            Capture(name + "_after", eye, look, fov);
            RrEnvironment.ForcedKey = was;
        }

        /// <summary>A post-processed 1280x720 shot to the harness output folder (RR_OUT) as &lt;name&gt;.png, with its mean luma logged.</summary>
        public static void Capture(string name, Vector3 eye, Vector3 look, float fov)
        {
            string outDir = System.Environment.GetEnvironmentVariable("RR_OUT");
            var go = new GameObject("Light Shot Camera");
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 2000f;
                cam.allowHDR = true;
                cam.clearFlags = CameraClearFlags.Skybox;
                var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
                data.renderPostProcessing = true;
                go.transform.position = eye;
                go.transform.rotation = Quaternion.LookRotation(look - eye, Vector3.up);
                cam.targetTexture = rt;
                cam.Render();
                var resolved = RenderTexture.GetTemporary(1280, 720, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(rt, resolved);
                var prev = RenderTexture.active;
                RenderTexture.active = resolved;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(resolved);
                cam.targetTexture = null;
                var px = tex.GetPixels32();
                double sum = 0;
                for (int i = 0; i < px.Length; i += 7)
                {
                    sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
                }

                double luma = sum / ((px.Length + 6) / 7);
                string file = string.IsNullOrEmpty(outDir) ? null : System.IO.Path.Combine(outDir, name + ".png");
                if (file != null)
                {
                    System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
                }

                RrLog.Info(Area, "shot " + name + " from " + eye.ToString("F1") + " toward " + look.ToString("F1") + ": mean luma " + luma.ToString("0.0", CultureInfo.InvariantCulture)
                    + (file != null ? " -> " + file : " (no RR_OUT: not written)"));
            }
            finally
            {
                Object.Destroy(go);
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
        }
    }
}
