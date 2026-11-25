using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's particle effects (splashes, sparks, paint splats, confetti, smash puffs) spawned where they
    /// happen and removed when they are done. Effect prefabs are ordinary Rec Room prefabs whose particle systems
    /// play on awake; they arrive through <see cref="RrSpawn"/> without colliders or rigidbodies, and are destroyed
    /// after their longest system's duration plus particle lifetime (3 s for looping ones, 12 s at most).
    ///
    /// Effects without a parent of their own live under one holder, "Rr Effects" under the world root, so that
    /// <see cref="Clear"/> can take them all when the room they belong to is left: every room is built at the
    /// world origin, and a splash still playing would otherwise show up in the next room.
    /// </summary>
    public static class RrFx
    {
        public static void Spawn(string prefabName, Vector3 position, Quaternion rotation, float scale = 1f, Transform parent = null)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return;
            }

            RrSpawn.Spawn(prefabName, parent != null ? parent : Root(), Vector3.zero, Quaternion.identity, inst => Place(inst, position, rotation, scale), Options(scale));
        }

        /// <summary>An effect prefab referenced by a Rec Room config (e.g. BreakableObject.smashParticlePrefab).</summary>
        public static void Spawn(RrTarget prefabRef, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (prefabRef.IsNull || RrAssets.Instance == null)
            {
                return;
            }

            // Rec Room often references a component of the effect (a PooledParticle), not its root.
            RrAssets.Instance.PrefabRootOf(prefabRef, entry =>
            {
                if (!entry.HasValue)
                {
                    RrLog.WarnOnce("Fx", "ref:" + prefabRef.Bundle + ":" + prefabRef.PathId, "effect reference has no prefab root in the catalog");
                    return;
                }

                RrAssets.Instance.LoadPrefab(entry.Value, (asset, data) =>
                {
                    if (asset == null)
                    {
                        return;
                    }

                    var inst = RrSpawn.SpawnLoaded(asset, data, Root(), Vector3.zero, Quaternion.identity, Options(scale));
                    Place(inst, position, rotation, scale);
                });
            });
        }

        private static RrSpawnOptions Options(float scale)
        {
            return new RrSpawnOptions { StripColliders = true, StripRigidbodies = true, Layer = RrLayers.Default, Scale = scale };
        }

        private static Transform _holder;

        private static Transform Root()
        {
            if (_holder == null)
            {
                _holder = new GameObject("Rr Effects").transform;
                _holder.SetParent(RrMain.Instance != null ? RrMain.Instance.WorldRoot : null, false);
            }

            return _holder;
        }

        /// <summary>Removes every effect still playing under the holder (called by travel when a room is left); returns how many.</summary>
        public static int Clear()
        {
            if (_holder == null)
            {
                return 0;
            }

            int n = _holder.childCount;
            for (int i = n - 1; i >= 0; i--)
            {
                Object.Destroy(_holder.GetChild(i).gameObject);
            }

            return n;
        }

        private static void Place(RrInstance inst, Vector3 position, Quaternion rotation, float scale)
        {
            if (inst == null)
            {
                return;
            }

            inst.transform.SetPositionAndRotation(position, rotation);
            float life = 0.5f;
            foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                float l = main.loop ? 3f : main.duration + main.startLifetime.constantMax + main.startDelay.constantMax;
                life = Mathf.Max(life, l);
                if (!ps.isPlaying)
                {
                    ps.Play(false);
                }
            }

            foreach (var a in inst.GetComponentsInChildren<AudioSource>(true))
            {
                if (a.clip != null)
                {
                    life = Mathf.Max(life, a.clip.length);
                }
            }

            Object.Destroy(inst.gameObject, Mathf.Min(life, 12f));
        }
    }
}
