using System;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>What to keep of a Rec Room prefab when it is spawned.</summary>
    public sealed class RrSpawnOptions
    {
        /// <summary>Put the whole tree on this layer; null maps Rec Room's layers (UI stays UI, the rest are props).</summary>
        public int? Layer;

        /// <summary>Keep UnityEngine.UI and TextMeshPro components (UI prefabs); every other bound script is removed.</summary>
        public bool KeepUiScripts;

        public bool StripColliders;
        public bool StripRigidbodies;

        /// <summary>Keep rigidbodies but make them kinematic (scenery that must not fall or be pushed).</summary>
        public bool Kinematic;

        public bool StripAudioSources;
        public bool StripLights;
        public bool StripParticles;

        /// <summary>Stop (not remove) particle systems that play on awake; the owning behaviour starts them.</summary>
        public bool StopParticles;

        /// <summary>Uniform scale applied to the root.</summary>
        public float Scale = 1f;

        /// <summary>
        /// Instantiate the prefab itself instead of the clean copy (<see cref="RrClone"/>). Every Rec Room script
        /// component then logs a missing-script warning; only for prefabs the clean copy cannot represent.
        /// UI prefabs (RectTransform roots) and KeepUiScripts always take this path.
        /// </summary>
        public bool FullInstantiate;

        public static RrSpawnOptions Prop()
        {
            return new RrSpawnOptions();
        }

        /// <summary>Static scenery: environment layer, kinematic, no sounds of its own.</summary>
        public static RrSpawnOptions Scenery()
        {
            return new RrSpawnOptions { Layer = RrLayers.Environment, Kinematic = true };
        }

        /// <summary>Pure visuals: no physics at all.</summary>
        public static RrSpawnOptions Visual()
        {
            return new RrSpawnOptions { StripColliders = true, StripRigidbodies = true };
        }
    }

    /// <summary>
    /// Spawns Rec Room prefabs so that nothing in them can act on the Somnium client.
    ///
    /// The prefab is instantiated under an inactive holder, so no Awake or OnEnable runs while it is cleaned:
    /// Rec Room's own scripts are inert missing scripts already, but 43 Unity classes do bind inside the client
    /// (UGUI, TMP, URP Volume and camera/light data, EventSystem...) and a stray Volume or EventSystem would change
    /// the client's post-processing or input. Every bound MonoBehaviour is destroyed (UI and TMP kept on request),
    /// cameras and audio listeners always go, materials are repaired (<see cref="RrMaterials"/>), layers mapped,
    /// and only then is the object moved under its parent and woken.
    /// </summary>
    public static class RrSpawn
    {
        private static int _spawned;
        private static int _skippedOrphans;

        public static int Spawned
        {
            get { return _spawned; }
        }

        /// <summary>Spawns dropped because their parent (a room that was left) was destroyed before the prefab loaded.</summary>
        public static int SkippedOrphans
        {
            get { return _skippedOrphans; }
        }

        /// <summary>Loads the prefab (and its data) if needed, spawns it, calls back on the main thread (null on failure).</summary>
        public static void Spawn(string prefabName, Transform parent, Vector3 localPos, Quaternion localRot, Action<RrInstance> done, RrSpawnOptions options = null)
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                if (done != null)
                {
                    done(null);
                }

                return;
            }

            // A parent given but destroyed before the load finished (the room was left) compares equal to null: the
            // copy would land at the scene root, at the room's local position, in the next room. Skip it instead.
            bool hadParent = !ReferenceEquals(parent, null);
            assets.LoadPrefab(prefabName, (asset, data) =>
            {
                RrInstance inst = null;
                if (hadParent && parent == null)
                {
                    if (++_skippedOrphans == 1)
                    {
                        RrLog.Info("Spawn", "a spawn finished after its parent was destroyed (" + prefabName + "): not spawned; later ones are counted in RrSpawn.SkippedOrphans");
                    }
                }
                else if (asset != null)
                {
                    try
                    {
                        inst = SpawnLoaded(asset, data, parent, localPos, localRot, options);
                    }
                    catch (Exception e)
                    {
                        RrLog.Error("Spawn", "spawning " + prefabName + " failed", e);
                    }
                }

                if (done != null)
                {
                    done(inst);
                }
            });
        }

        /// <summary>Spawns an already loaded prefab asset.</summary>
        public static RrInstance SpawnLoaded(GameObject prefabAsset, RrPrefabData data, Transform parent, Vector3 localPos, Quaternion localRot, RrSpawnOptions options = null)
        {
            if (prefabAsset == null)
            {
                return null;
            }

            var o = options ?? new RrSpawnOptions();
            var holder = new GameObject("Rr spawn holder");
            holder.SetActive(false);
            GameObject go;
            try
            {
                bool full = o.FullInstantiate || o.KeepUiScripts || prefabAsset.transform is RectTransform;
                if (full)
                {
                    go = Object.Instantiate(prefabAsset, holder.transform, false);
                }
                else
                {
                    go = RrClone.Clone(prefabAsset, holder.transform);
                }

                go.name = prefabAsset.name;
                Clean(go, o);
                RrMaterials.FixTree(go);
                if (o.Layer.HasValue)
                {
                    RrLayers.Set(go, o.Layer.Value);
                }
                else
                {
                    MapLayers(go.transform);
                }

                var inst = go.AddComponent<RrInstance>();
                inst.Data = data;
                inst.PrefabName = prefabAsset.name;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localRotation = localRot;
                if (Math.Abs(o.Scale - 1f) > 1e-4f)
                {
                    go.transform.localScale = go.transform.localScale * o.Scale;
                }

                _spawned++;
                return inst;
            }
            finally
            {
                Object.Destroy(holder);
            }
        }

        private static void Clean(GameObject go, RrSpawnOptions o)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                {
                    continue;
                }

                if (o.KeepUiScripts && IsUi(mb))
                {
                    continue;
                }

                TryDestroy(mb);
            }

            foreach (var c in go.GetComponentsInChildren<Camera>(true))
            {
                TryDestroy(c);
            }

            foreach (var l in go.GetComponentsInChildren<AudioListener>(true))
            {
                TryDestroy(l);
            }

            foreach (var p in go.GetComponentsInChildren<ReflectionProbe>(true))
            {
                TryDestroy(p);
            }

            if (o.StripLights)
            {
                foreach (var l in go.GetComponentsInChildren<Light>(true))
                {
                    TryDestroy(l);
                }
            }

            if (o.StripAudioSources)
            {
                foreach (var a in go.GetComponentsInChildren<AudioSource>(true))
                {
                    TryDestroy(a);
                }
            }

            if (o.StripParticles)
            {
                foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    TryDestroy(r);
                }

                foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    TryDestroy(p);
                }
            }
            else if (o.StopParticles)
            {
                foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = p.main;
                    main.playOnAwake = false;
                    p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (o.StripColliders)
            {
                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                {
                    TryDestroy(c);
                }
            }

            if (o.StripRigidbodies)
            {
                foreach (var j in go.GetComponentsInChildren<Joint>(true))
                {
                    TryDestroy(j);
                }

                foreach (var r in go.GetComponentsInChildren<Rigidbody>(true))
                {
                    TryDestroy(r);
                }
            }
            else if (o.Kinematic)
            {
                foreach (var r in go.GetComponentsInChildren<Rigidbody>(true))
                {
                    r.isKinematic = true;
                }
            }
        }

        private static bool IsUi(MonoBehaviour mb)
        {
            string ns = mb.GetType().Namespace ?? "";
            if (ns == "TMPro")
            {
                return true;
            }

            if (ns != "UnityEngine.UI")
            {
                return false;
            }

            // Interactive UGUI would take clicks from the client's own input module.
            return !(mb is UnityEngine.UI.Selectable);
        }

        private static void TryDestroy(Object o)
        {
            try
            {
                Object.DestroyImmediate(o);
            }
            catch (Exception)
            {
                // A component another one requires: left in place, the holder keeps it asleep until cleaned.
            }
        }

        private static void MapLayers(Transform t)
        {
            t.gameObject.layer = RrLayers.MapRecRoomLayer(t.gameObject.layer);
            for (int i = 0; i < t.childCount; i++)
            {
                MapLayers(t.GetChild(i));
            }
        }
    }
}
