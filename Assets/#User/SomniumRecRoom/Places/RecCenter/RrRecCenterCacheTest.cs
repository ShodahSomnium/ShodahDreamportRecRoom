using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// Play-mode harness hooks for Rec Room's cached Rec Center ({"call": "SomniumSpace.Worlds.SomniumRecRoom.Places.
    /// RecCenter.RrRecCenterCacheTest.X"}). Each logs "RecCenter cache test:" lines.
    /// </summary>
    public static class RrRecCenterCacheTest
    {
        private const string Area = "RecCenter";

        /// <summary>
        /// Feasibility probe inside the current Rec Center: hides the rebuilt shell, loads the cached scene into the room
        /// frame (unless the room already did) and logs what came in.
        /// </summary>
        public static IEnumerator Probe()
        {
            var room = RrTravel.CurrentRoom;
            if (room == null)
            {
                RrLog.Warn(Area, "cache test: no current room");
                yield break;
            }

            var c = RrRecCenterCache.Current;
            if (c == null)
            {
                SetRebuild(room.transform, false);
                RrLog.Info(Area, "cache test: probe loading into " + room.Activity + " (lightmaps " + Lightmaps() + ")");
                yield return RrRecCenterCache.Load(room.transform, x => c = x);
            }

            if (c == null)
            {
                RrLog.Warn(Area, "cache test: probe: no cached Rec Center (" + RrRecCenterCache.LastOutcome + ")");
                SetRebuild(room.transform, true);
                yield break;
            }

            Inventory(c);
        }

        /// <summary>Hides/shows the Dreamport rebuild's shell, outdoors and effects (probe only).</summary>
        private static void SetRebuild(Transform room, bool on)
        {
            foreach (string n in new[] { "Shell", "Outdoors", "Fx", "Rec Center Fx", "Light Shafts" })
            {
                var t = room.Find(n);
                if (t != null)
                {
                    t.gameObject.SetActive(on);
                    RrLog.Info(Area, "cache test: rebuild '" + n + "' " + (on ? "shown" : "hidden"));
                }
            }
        }

        private static int Lightmaps()
        {
            return LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;
        }

        /// <summary>Counts of what the adopted scene holds (roots, renderers, shaders, lights, colliders by layer...).</summary>
        public static void Inventory(RrRecCenterCache c)
        {
            var f = c.Found;
            long largest = 0;
            string largestName = "";
            foreach (string d in f.Dependencies)
            {
                long n = new System.IO.FileInfo(d).Length;
                if (n > largest)
                {
                    largest = n;
                    largestName = System.IO.Path.GetFileName(d);
                }
            }

            RrLog.Info(Area, "cache test: bundles " + (f.Dependencies.Count + 1) + " (" + (f.Bytes / 1048576.0).ToString("0.0") + " MB; scene "
                + System.IO.Path.GetFileName(f.SceneFile) + " " + (new System.IO.FileInfo(f.SceneFile).Length / 1048576.0).ToString("0.0") + " MB; largest dep "
                + largestName + " " + (largest / 1048576.0).ToString("0.0") + " MB), load " + c.LoadSeconds.ToString("0.00") + " s (bundles "
                + c.BundleSeconds.ToString("0.00") + ", scene " + c.SceneSeconds.ToString("0.00") + "), lightmaps " + c.LightmapsBefore + " -> " + c.LightmapsAfter
                + " (now " + Lightmaps() + ")");

            var roots = new StringBuilder();
            foreach (var r in c.Roots)
            {
                roots.Append(roots.Length > 0 ? ", " : "").Append(r.name).Append(r.activeSelf ? "" : " (off)");
            }

            RrLog.Info(Area, "cache test: " + c.Roots.Count + " roots: " + roots);
            var root = c.Frame;
            int renderers = 0, enabled = 0, skinned = 0, particles = 0, lod = 0, batched = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                if (r.enabled && r.gameObject.activeInHierarchy)
                {
                    enabled++;
                }

                if (r is SkinnedMeshRenderer)
                {
                    skinned++;
                }

                if (r is ParticleSystemRenderer)
                {
                    particles++;
                }

                if (r.isPartOfStaticBatch)
                {
                    batched++;
                }
            }

            lod = root.GetComponentsInChildren<LODGroup>(true).Length;
            var shaders = new Dictionary<string, int>();
            var seen = new HashSet<int>();
            int unsupported = 0, error = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !seen.Add(m.GetInstanceID()))
                    {
                        continue;
                    }

                    string n = m.shader == null ? "(null)" : m.shader.name;
                    if (m.shader != null && !m.shader.isSupported)
                    {
                        unsupported++;
                    }

                    if (n == "Hidden/InternalErrorShader")
                    {
                        error++;
                    }

                    int k;
                    shaders.TryGetValue(n, out k);
                    shaders[n] = k + 1;
                }
            }

            var sh = new StringBuilder();
            foreach (var kv in shaders)
            {
                sh.Append(sh.Length > 0 ? ", " : "").Append(kv.Key).Append(" x").Append(kv.Value);
            }

            RrLog.Info(Area, "cache test: renderers " + renderers + " (" + enabled + " enabled, " + batched + " static-batched, " + skinned + " skinned, " + particles
                + " particle systems, " + lod + " LOD groups); materials now " + seen.Count + " (" + unsupported + " unsupported, " + error + " InternalErrorShader): " + sh);

            int lights = 0, lightsOn = 0, dir = 0, colliders = 0, triggers = 0, meshColliders = 0;
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                lights++;
                if (l.enabled && l.gameObject.activeInHierarchy)
                {
                    lightsOn++;
                }

                if (l.type == LightType.Directional)
                {
                    dir++;
                }
            }

            var layers = new Dictionary<int, int>();
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                colliders++;
                if (col.isTrigger)
                {
                    triggers++;
                }

                if (col is MeshCollider)
                {
                    meshColliders++;
                }

                int k;
                layers.TryGetValue(col.gameObject.layer, out k);
                layers[col.gameObject.layer] = k + 1;
            }

            var ly = new StringBuilder();
            foreach (var kv in layers)
            {
                ly.Append(ly.Length > 0 ? ", " : "").Append(kv.Key).Append(": ").Append(kv.Value);
            }

            RrLog.Info(Area, "cache test: lights " + lights + " (" + lightsOn + " on, " + dir + " directional); colliders " + colliders + " (" + meshColliders + " mesh, "
                + triggers + " triggers) on layers " + ly + "; cameras " + root.GetComponentsInChildren<Camera>(true).Length + ", audio listeners "
                + root.GetComponentsInChildren<AudioListener>(true).Length + ", volumes " + root.GetComponentsInChildren<Volume>(true).Length + ", canvases "
                + root.GetComponentsInChildren<Canvas>(true).Length + ", audio sources " + root.GetComponentsInChildren<AudioSource>(true).Length + ", reflection probes "
                + root.GetComponentsInChildren<ReflectionProbe>(true).Length + ", terrains " + root.GetComponentsInChildren<Terrain>(true).Length + ", animators "
                + root.GetComponentsInChildren<Animator>(true).Length + "; " + RrRecCenterCache.Describe());

            // Where the floor is under a few room-frame spots (the adopted colliders, layer 23).
            Physics.SyncTransforms();
            var probes = new[]
            {
                new Vector3(0f, 0f, -12f), new Vector3(-0.65f, 0f, -16.6f), new Vector3(0f, 0f, 5f), new Vector3(-11.5f, 3.1f, -2f), new Vector3(-19.9f, 0f, -1.6f),
                new Vector3(-9.9f, 0.2f, 19.5f), new Vector3(16.8f, -0.79f, -4.4f), new Vector3(6f, 0f, 12f), new Vector3(-0.65f, 0f, -24f), new Vector3(0f, 0.25f, 0f)
            };
            var fl = new StringBuilder();
            var room = RrTravel.CurrentRoom != null ? RrTravel.CurrentRoom.transform : root.parent;
            foreach (var p in probes)
            {
                var from = room.TransformPoint(p + Vector3.up * 1.5f);
                RaycastHit hit;
                bool ok = Physics.Raycast(from, Vector3.down, out hit, 6f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore);
                fl.Append(fl.Length > 0 ? "; " : "").Append(p.ToString("F1")).Append(" -> ")
                  .Append(ok ? room.InverseTransformPoint(hit.point).y.ToString("0.00") + " (" + hit.collider.name + (hit.collider.transform.IsChildOf(root) ? "" : ", ours") + ")" : "none");
            }

            RrLog.Info(Area, "cache test: floor under spots (room frame): " + fl);
        }

        public static IEnumerator LitStudio()
        {
            var c = RrRecCenterCache.Current;
            if (c != null)
            {
                yield return c.UseLit(RrRecCenterCache.LitSource.StudioTarget);
            }
        }

        public static IEnumerator LitInstall()
        {
            var c = RrRecCenterCache.Current;
            if (c != null)
            {
                yield return c.UseLit(RrRecCenterCache.LitSource.InstallUrpLit);
            }
        }

        public static IEnumerator LitClient()
        {
            var c = RrRecCenterCache.Current;
            if (c != null)
            {
                yield return c.UseLit(RrRecCenterCache.LitSource.ClientUrpLit);
            }
        }

        /// <summary>The cached key light back on, without shadows (look comparison).</summary>
        public static void KeyLightOn()
        {
            var c = RrRecCenterCache.Current;
            if (c == null)
            {
                return;
            }

            foreach (var l in c.Frame.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Directional)
                {
                    l.shadows = LightShadows.None;
                    l.enabled = true;
                    RrLog.Info(Area, "cache test: key light " + l.name + " on (no shadows)");
                }
            }
        }

        public static void KeyLightOff()
        {
            var c = RrRecCenterCache.Current;
            if (c == null)
            {
                return;
            }

            foreach (var l in c.Frame.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Directional)
                {
                    l.enabled = false;
                }
            }
        }

        /// <summary>Forces the fallback (the Dreamport rebuild) for the next Rec Center builds.</summary>
        public static void ForceFallback()
        {
            RrRecCenterCache.ForceFallback = true;
            RrLog.Info(Area, "cache test: fallback forced");
        }

        public static void AllowCache()
        {
            RrRecCenterCache.ForceFallback = false;
            RrLog.Info(Area, "cache test: cache allowed");
        }

        public static void Status()
        {
            RrLog.Info(Area, "cache test: status: " + RrRecCenterCache.Describe() + "; current " + (RrRecCenterCache.Current != null ? "live" : "none")
                + "; last: " + RrRecCenterCache.LastOutcome);
        }
    }
}
