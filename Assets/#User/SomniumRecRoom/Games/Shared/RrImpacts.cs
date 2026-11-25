using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// What a shot leaves behind: Rec Room's paint splat decal on the surface, its impact particles, muzzle flashes,
    /// and the hit sounds, all pooled.
    ///
    /// A burst rifle fires 12 shots a second; spawning a fresh Rec Room effect per shot costs 0.3-1.4 ms each (clone,
    /// clean, material fix: harness 2026-09-24) and logs its missing-script warnings every time, so every effect
    /// prefab gets a pool and instances are reused (particles cleared and replayed). Decals follow Rec Room's
    /// ImpactDecal config: (PaintballImpactDecal) lives lifetime 2 s then fades over fadeOutTime 1 s, scaled 0.8-1.2
    /// (the weapon's minMaxImpactDecalScale), pushed off the surface by its bias 0.01 m, tinted with the shooter's
    /// paint; at most <see cref="MaxDecals"/> at once, the oldest recycled first. Splats are never put on players
    /// (the avatar is not ours) or on moving props. Laser "decals" are particle scorches and play as effects.
    /// </summary>
    public static class RrImpacts
    {
        public static int MaxDecals = 200;

        /// <summary>Impact sounds allowed per frame (a shotgun's 9 pellets would otherwise play 9 clips at once).</summary>
        public static int SoundsPerFrame = 2;

        private sealed class Pool
        {
            public RrEntry Entry;
            public GameObject Asset;
            public RrPrefabData Data;
            public bool Loading;
            public bool Failed;
            public bool IsParticle;
            public bool IsDecal;
            public Vector3 NormalAxis = Vector3.forward;
            public float Life = 1f;
            public readonly Stack<Item> Free = new Stack<Item>();
            public readonly List<Action> Pending = new List<Action>();
        }

        private sealed class Item
        {
            public Pool Pool;
            public GameObject Go;
            public ParticleSystem[] Systems;
            public ParticleSystem.MinMaxGradient[] Original;
            public Renderer[] Renderers;
            public float Born;
            public float ReleaseAt;
            public float Life;
            public float Fade;
            public Vector3 Scale;
            public Color Color;
            public bool Tinted;
        }

        private static readonly Dictionary<long, Pool> Pools = new Dictionary<long, Pool>();
        private static readonly List<Item> ActiveFx = new List<Item>();
        private static readonly List<Item> ActiveDecals = new List<Item>();
        private static Transform _root;
        private static MaterialPropertyBlock _block;
        private static int _soundFrame = -1;
        private static int _soundsThisFrame;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        public static int DecalCount
        {
            get { return ActiveDecals.Count; }
        }

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var host = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
                    _root = new GameObject("Rr Impacts").transform;
                    _root.SetParent(host, false);
                }

                return _root;
            }
        }

        // ---- what shots call ----

        /// <summary>A shot hit the world: splat (or scorch), impact particles and the environment sound.</summary>
        public static void Surface(RrShotSpec shot, Vector3 point, Vector3 normal, Color color, bool decal)
        {
            if (shot == null)
            {
                return;
            }

            if (decal && shot.ImpactDecal.HasValue)
            {
                Decal(shot.ImpactDecal.Value, point, normal, Random.Range(shot.DecalMin, shot.DecalMax), color, shot.DecalLife, shot.DecalFade, shot.DecalOffset, !shot.Laser);
            }

            if (shot.ImpactParticle.HasValue)
            {
                Fx(shot.ImpactParticle.Value, point, Quaternion.LookRotation(normal), 1f, shot.Laser ? (Color?)null : color);
            }

            Sound(shot.HitEnvironmentSound, shot.HitEnvironmentClip, point, 0.75f);
        }

        /// <summary>A shot hit a player or an enemy: a puff of paint (never a decal on them) and the hit sound.</summary>
        public static void Body(RrShotSpec shot, Vector3 point, Vector3 normal, Color color, bool enemy)
        {
            if (shot == null)
            {
                return;
            }

            if (shot.ImpactParticle.HasValue)
            {
                Fx(shot.ImpactParticle.Value, point, Quaternion.LookRotation(normal), 1f, shot.Laser ? (Color?)null : color);
            }

            if (enemy)
            {
                Sound(shot.HitEnemySound, shot.HitEnemyClip, point, 0.8f);
            }
            else
            {
                Sound(shot.HitPlayerSound, shot.HitPlayerClip, point, 0.85f);
            }
        }

        /// <summary>A muzzle flash at the barrel, following it while it plays.</summary>
        public static void Muzzle(RrWeaponSpec weapon, Vector3 position, Quaternion rotation, Color color)
        {
            if (weapon != null && weapon.MuzzleFlash.HasValue)
            {
                Fx(weapon.MuzzleFlash.Value, position, rotation, 1f, weapon.Shot.Laser ? (Color?)null : color);
            }
        }

        /// <summary>Plays a clip struct, or a named clip when the struct is empty, with a per-frame cap.</summary>
        public static void Sound(RrClipInfo info, string fallbackClip, Vector3 point, float fallbackVolume)
        {
            if (_soundFrame != Time.frameCount)
            {
                _soundFrame = Time.frameCount;
                _soundsThisFrame = 0;
            }

            if (_soundsThisFrame >= SoundsPerFrame)
            {
                return;
            }

            _soundsThisFrame++;
            if (info.HasClip)
            {
                RrSound.Play(info, point);
            }
            else if (!string.IsNullOrEmpty(fallbackClip))
            {
                RrSound.Play(fallbackClip, point, fallbackVolume, Random.Range(0.92f, 1.08f));
            }
        }

        // ---- pooled effects ----

        /// <summary>A pooled Rec Room particle effect; tint recolours every system's start colour (paint).</summary>
        public static void Fx(RrEntry prefab, Vector3 position, Quaternion rotation, float scale, Color? tint)
        {
            WithPool(prefab, pool =>
            {
                var item = Take(pool);
                if (item == null)
                {
                    return;
                }

                var t = item.Go.transform;
                t.SetParent(Root, false);
                t.SetPositionAndRotation(position, rotation);
                t.localScale = item.Scale * scale;
                Tint(item, tint);
                item.Go.SetActive(true);
                if (item.Systems != null)
                {
                    foreach (var ps in item.Systems)
                    {
                        ps.Clear(false);
                        ps.Play(false);
                    }
                }

                item.Born = Time.time;
                item.ReleaseAt = Time.time + pool.Life;
                ActiveFx.Add(item);
            });
        }

        /// <summary>A pooled effect by name (enemy deaths, explosions); see <see cref="Fx(RrEntry, Vector3, Quaternion, float, Color?)"/>.</summary>
        public static void Fx(string prefabName, Vector3 position, Quaternion rotation, float scale = 1f, Color? tint = null)
        {
            RrEntry e;
            if (RrAssets.Instance != null && !string.IsNullOrEmpty(prefabName) && RrAssets.Instance.TryFind(prefabName, 1, out e))
            {
                Fx(e, position, rotation, scale, tint);
            }
            else
            {
                RrLog.WarnOnce("Impacts", "fx:" + prefabName, "no effect prefab named " + prefabName);
            }
        }

        /// <summary>A splat on a surface: the decal's thin axis along the normal, random spin, tinted, fading as Rec Room's.</summary>
        public static void Decal(RrEntry prefab, Vector3 point, Vector3 normal, float scale, Color color, float life, float fade, float offset, bool tinted)
        {
            WithPool(prefab, pool =>
            {
                if (pool.IsParticle || !pool.IsDecal)
                {
                    Fx(prefab, point, Quaternion.LookRotation(normal), scale, tinted ? (Color?)color : null);
                    return;
                }

                Item item;
                if (ActiveDecals.Count >= MaxDecals && ActiveDecals.Count > 0)
                {
                    item = ActiveDecals[0];
                    ActiveDecals.RemoveAt(0);
                    Release(item);
                }

                item = Take(pool);
                if (item == null)
                {
                    return;
                }

                var t = item.Go.transform;
                t.SetParent(Root, false);
                var spin = Quaternion.AngleAxis(Random.Range(0f, 360f), normal);
                t.SetPositionAndRotation(point + normal * Mathf.Max(0.004f, offset), spin * Quaternion.FromToRotation(pool.NormalAxis, normal));
                t.localScale = item.Scale * scale;
                item.Color = color;
                item.Tinted = tinted;
                SetDecalColor(item, 1f);
                item.Go.SetActive(true);
                item.Born = Time.time;
                item.Life = life > 0f ? Mathf.Max(2f, life) : 0f;
                item.Fade = Mathf.Max(0.1f, fade);
                ActiveDecals.Add(item);
            });
        }

        /// <summary>Removes every splat and effect (round reset).</summary>
        public static void Clear()
        {
            foreach (var d in ActiveDecals)
            {
                Release(d);
            }

            ActiveDecals.Clear();
            foreach (var f in ActiveFx)
            {
                Release(f);
            }

            ActiveFx.Clear();
        }

        /// <summary>Called every frame by the combat driver: returns finished effects, fades old splats.</summary>
        public static void Tick()
        {
            float now = Time.time;
            for (int i = ActiveFx.Count - 1; i >= 0; i--)
            {
                var f = ActiveFx[i];
                if (f.Go == null)
                {
                    ActiveFx.RemoveAt(i);
                }
                else if (now >= f.ReleaseAt)
                {
                    ActiveFx.RemoveAt(i);
                    Release(f);
                }
            }

            for (int i = ActiveDecals.Count - 1; i >= 0; i--)
            {
                var d = ActiveDecals[i];
                if (d.Go == null)
                {
                    ActiveDecals.RemoveAt(i);
                    continue;
                }

                if (d.Life <= 0f)
                {
                    continue;
                }

                float age = now - d.Born;
                if (age >= d.Life + d.Fade)
                {
                    ActiveDecals.RemoveAt(i);
                    Release(d);
                }
                else if (age > d.Life)
                {
                    SetDecalColor(d, 1f - (age - d.Life) / d.Fade);
                }
            }
        }

        // ---- pools ----

        private static void WithPool(RrEntry entry, Action<Pool> use)
        {
            long key = RrCatalog.Key(entry.Bundle, entry.PathId);
            Pool pool;
            if (!Pools.TryGetValue(key, out pool))
            {
                pool = new Pool { Entry = entry };
                Pools[key] = pool;
            }

            if (pool.Failed)
            {
                return;
            }

            if (pool.Asset != null)
            {
                use(pool);
                return;
            }

            if (pool.Pending.Count < 8)
            {
                pool.Pending.Add(() => use(pool));
            }

            if (pool.Loading || RrAssets.Instance == null)
            {
                return;
            }

            pool.Loading = true;
            RrAssets.Instance.LoadPrefab(entry, (asset, data) =>
            {
                pool.Loading = false;
                pool.Asset = asset;
                pool.Data = data;
                if (asset == null)
                {
                    pool.Failed = true;
                    pool.Pending.Clear();
                    return;
                }

                Measure(pool);
                var pending = pool.Pending.ToArray();
                pool.Pending.Clear();
                foreach (var a in pending)
                {
                    try
                    {
                        a();
                    }
                    catch (Exception e)
                    {
                        RrLog.Error("Impacts", "effect " + entry.Name + " failed", e);
                    }
                }
            });
        }

        /// <summary>Life of an effect (longest system) and, for decals, which local axis is the surface normal.</summary>
        private static void Measure(Pool pool)
        {
            var go = pool.Asset;
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            pool.IsParticle = systems.Length > 0;
            float life = 0.5f;
            foreach (var ps in systems)
            {
                var main = ps.main;
                float l = main.loop ? 1.5f : main.duration + main.startLifetime.constantMax + main.startDelay.constantMax;
                life = Mathf.Max(life, l);
            }

            foreach (var a in go.GetComponentsInChildren<AudioSource>(true))
            {
                if (a.clip != null)
                {
                    life = Mathf.Max(life, a.clip.length);
                }
            }

            pool.Life = Mathf.Min(life, 8f);
            var mf = go.GetComponentInChildren<MeshFilter>(true);
            pool.IsDecal = !pool.IsParticle && mf != null && mf.sharedMesh != null;
            if (pool.IsDecal)
            {
                var size = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                if (size.x <= size.y && size.x <= size.z)
                {
                    pool.NormalAxis = Vector3.right;
                }
                else if (size.y <= size.x && size.y <= size.z)
                {
                    pool.NormalAxis = Vector3.up;
                }
                else
                {
                    pool.NormalAxis = Vector3.forward;
                }

                // Face the viewer: when the mesh's normals are readable, point the axis the way they do.
                var mesh = mf.sharedMesh;
                if (mesh.isReadable)
                {
                    var normals = mesh.normals;
                    if (normals != null && normals.Length > 0)
                    {
                        var n = mf.transform.localRotation * normals[0];
                        if (Vector3.Dot(n, pool.NormalAxis) < 0f)
                        {
                            pool.NormalAxis = -pool.NormalAxis;
                        }
                    }
                }

                RrLog.Info("Impacts", "decal " + pool.Entry.Name + ": mesh " + mesh.name + " " + size.ToString("F3") + ", normal axis " + pool.NormalAxis);
            }
        }

        private static Item Take(Pool pool)
        {
            while (pool.Free.Count > 0)
            {
                var it = pool.Free.Pop();
                if (it.Go != null)
                {
                    return it;
                }
            }

            RrInstance inst = null;
            try
            {
                inst = RrSpawn.SpawnLoaded(pool.Asset, pool.Data, Root, Vector3.zero, Quaternion.identity,
                    new RrSpawnOptions { StripColliders = true, StripRigidbodies = true, Layer = RrLayers.Default });
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Impacts", "spawn:" + pool.Entry.Name, "effect " + pool.Entry.Name + " could not be spawned: " + e.Message);
            }

            if (inst == null)
            {
                return null;
            }

            var item = new Item { Pool = pool, Go = inst.gameObject, Scale = inst.transform.localScale };
            item.Systems = inst.GetComponentsInChildren<ParticleSystem>(true);
            item.Original = new ParticleSystem.MinMaxGradient[item.Systems.Length];
            for (int i = 0; i < item.Systems.Length; i++)
            {
                var main = item.Systems[i].main;
                main.playOnAwake = false;
                item.Original[i] = main.startColor;
            }

            item.Renderers = inst.GetComponentsInChildren<Renderer>(true);
            return item;
        }

        private static void Tint(Item item, Color? tint)
        {
            if (item.Systems == null)
            {
                return;
            }

            for (int i = 0; i < item.Systems.Length; i++)
            {
                var main = item.Systems[i].main;
                if (tint.HasValue)
                {
                    var c = tint.Value;
                    var o = item.Original[i];
                    float a = o.mode == ParticleSystemGradientMode.Color ? o.color.a : 1f;
                    main.startColor = new Color(c.r, c.g, c.b, a);
                }
                else
                {
                    main.startColor = item.Original[i];
                }
            }
        }

        private static void SetDecalColor(Item item, float alpha)
        {
            if (item.Renderers == null)
            {
                return;
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            if (!item.Tinted && alpha >= 0.999f)
            {
                foreach (var r in item.Renderers)
                {
                    if (r != null)
                    {
                        r.SetPropertyBlock(null);
                    }
                }

                return;
            }

            var c = item.Tinted ? item.Color : Color.white;
            c.a = Mathf.Clamp01(alpha);
            foreach (var r in item.Renderers)
            {
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                _block.SetColor(ColorId, c);
                _block.SetColor(TintColorId, c);
                r.SetPropertyBlock(_block);
            }

            if (alpha < 1f)
            {
                item.Go.transform.localScale = item.Go.transform.localScale * Mathf.Lerp(0.97f, 1f, alpha);
            }
        }

        private static void Release(Item item)
        {
            if (item == null || item.Go == null)
            {
                return;
            }

            item.Go.SetActive(false);
            if (item.Pool != null && item.Pool.Free.Count < 64)
            {
                item.Pool.Free.Push(item);
            }
            else
            {
                Object.Destroy(item.Go);
            }
        }

        internal static void ResetStatics()
        {
            Pools.Clear();
            ActiveFx.Clear();
            ActiveDecals.Clear();
            _root = null;
            _block = null;
            _soundFrame = -1;
        }
    }
}
