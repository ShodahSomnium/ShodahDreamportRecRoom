using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// One flying shot: a paintball on its arc (gravity x gravityRatio, air drag) or a straight laser bolt, stepped
    /// once a frame by sphere casts of the Bullet's colliderRadius along the frame's path - no rigidbody, no
    /// physics callbacks, so a 250 m/s scope-rifle round cannot tunnel through a wall between two physics steps.
    ///
    /// Two casts per step: the world (Default, Grabbable, Terrain, Click, Environment, Vehicle; triggers ignored,
    /// the shooter's own gun skipped) and the hit volumes on layer 27 (triggers). The nearer valid hit wins.
    /// Friendly fire is off by default (<see cref="RrCombat.FriendlyFire"/>): a teammate's volumes are passed
    /// through, as Rec Room paint passes a teammate. Only the shooter's client decides hits on players and AI
    /// (<see cref="DecidesHits"/>); every other client flies the same shot from the Fire event as a visual that
    /// splats where it lands. Enemy shots are simulated on every client and each tests only its own local player
    /// (the victim decides, rr_quests.md 3.6).
    ///
    /// The object sits on layer 26 (Bullet) and is pooled per look: our own ball + trail for paint (Rec Room's
    /// [Paintball] is nothing but a BulletTrail: 0.175 s, 0.03 m), the Rec Room bullet prefab itself for bolts that
    /// have meshes ([Quest_SciFi_PistolLaser] streak).
    /// </summary>
    public sealed class RrProjectile : MonoBehaviour
    {
        /// <summary>Surfaces a shot stops on (and splats): everything solid in a room.</summary>
        public const int WorldMask = (1 << RrLayers.Default) | (1 << RrLayers.Grabbable) | (1 << 14) | (1 << RrLayers.Click) | (1 << RrLayers.Environment) | (1 << RrLayers.Vehicle);

        public const int MaxActive = 256;

        public RrShotSpec Shot;
        public int Owner = RrCombat.NoPlayer;
        public RrEnemy OwnerEnemy;
        public bool DecidesHits;
        public byte Seq;
        public Color Color;
        public Vector3 Velocity;
        public float Age;
        public float Travelled;

        /// <summary>Colliders under this root are never hit (the shooter's own gun).</summary>
        public Transform IgnoreRoot;

        private string _poolKey;
        private Transform _visual;
        private TrailRenderer _trail;
        private Renderer[] _renderers;
        private bool _alive;

        private static readonly List<RrProjectile> Active = new List<RrProjectile>();
        private static readonly Dictionary<string, Stack<RrProjectile>> Pools = new Dictionary<string, Stack<RrProjectile>>();
        private static readonly Dictionary<string, GameObject> BoltAssets = new Dictionary<string, GameObject>();
        private static readonly HashSet<string> BoltLoading = new HashSet<string>();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlaps = new Collider[32];
        private static Transform _root;
        private static Mesh _ball;
        private static Material _trailMaterial;
        private static MaterialPropertyBlock _block;
        private static int _launched;
        private static bool _loggedFirst;

        public static int ActiveCount
        {
            get { return Active.Count; }
        }

        public static int Launched
        {
            get { return _launched; }
        }

        public bool Alive
        {
            get { return _alive; }
        }

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var host = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
                    _root = new GameObject("Rr Projectiles").transform;
                    _root.SetParent(host, false);
                }

                return _root;
            }
        }

        /// <summary>
        /// Fires a shot. decidesHits: this client judges hits on players and AI (the local shooter). color: the
        /// paint (team colour); null takes the shot's default. ignoreRoot: the shooter's gun.
        /// </summary>
        public static RrProjectile Launch(RrShotSpec shot, Vector3 origin, Vector3 velocity, int owner, bool decidesHits, byte seq = 0,
            Transform ignoreRoot = null, RrEnemy ownerEnemy = null, Color? color = null)
        {
            if (shot == null)
            {
                return null;
            }

            RrCombat.Ensure();
            if (Active.Count >= MaxActive)
            {
                Active[0].Despawn();
            }

            string key = PoolKey(shot);
            var p = Take(key, shot);
            if (p == null)
            {
                return null;
            }

            p.Shot = shot;
            p.Owner = owner;
            p.OwnerEnemy = ownerEnemy;
            p.DecidesHits = decidesHits;
            p.Seq = seq;
            p.IgnoreRoot = ignoreRoot;
            p.Color = color ?? (shot.TeamColored ? RrCombat.ColorOf(owner, shot.Color) : shot.Color);
            p.Velocity = velocity;
            p.Age = 0f;
            p.Travelled = 0f;
            p._alive = true;
            var t = p.transform;
            t.SetParent(Root, false);
            t.SetPositionAndRotation(origin, velocity.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(velocity) : Quaternion.identity);
            p.ApplyLook();
            p.gameObject.SetActive(true);
            if (p._trail != null)
            {
                p._trail.Clear();
                p._trail.emitting = true;
            }

            Active.Add(p);
            _launched++;
            if (!_loggedFirst)
            {
                _loggedFirst = true;
                RrLog.Info("Projectile", "first shot: " + shot.Name + " at " + velocity.magnitude.ToString("0.#") + " m/s, gravity " + shot.Gravity.ToString("0.##")
                    + ", radius " + shot.Radius + ", life " + shot.Life + " s, look " + key);
            }

            return p;
        }

        /// <summary>Removes every shot in flight (round reset).</summary>
        public static void ClearAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                Active[i].Despawn();
            }
        }

        /// <summary>Called once a frame by the combat driver.</summary>
        public static void StepAll(float dt)
        {
            if (Active.Count == 0 || dt <= 0f)
            {
                return;
            }

            RrCombat.SyncPhysics();
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (i >= Active.Count)
                {
                    continue;
                }

                var p = Active[i];
                if (p == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                p.Step(dt);
            }
        }

        private void Step(float dt)
        {
            Age += dt;
            if (Age > Shot.Life || (Shot.MaxDistance > 0f && Travelled > Shot.MaxDistance))
            {
                Despawn();
                return;
            }

            var t = transform;
            Vector3 p0 = t.position;
            Velocity.y -= Shot.Gravity * dt;
            if (Shot.Drag > 0f)
            {
                Velocity *= Mathf.Max(0f, 1f - Shot.Drag * dt);
            }

            Vector3 step = Velocity * dt;
            float len = step.magnitude;
            if (len < 1e-6f)
            {
                return;
            }

            var dir = step / len;
            var ray = new Ray(p0, dir);
            float best = len;
            bool hitWorld = false;
            RaycastHit worldHit = default(RaycastHit);
            RrHitVolume hitVolume = null;
            RaycastHit volumeHit = default(RaycastHit);

            int n = Physics.SphereCastNonAlloc(ray, Shot.Radius, Hits, len, WorldMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.distance <= 0f && h.point == Vector3.zero)
                {
                    continue;
                }

                if (IgnoreRoot != null && h.collider != null && h.collider.transform.IsChildOf(IgnoreRoot))
                {
                    continue;
                }

                if (h.distance < best)
                {
                    best = h.distance;
                    worldHit = h;
                    hitWorld = true;
                }
            }

            n = Physics.SphereCastNonAlloc(ray, Shot.Radius, Hits, len, RrHitVolume.Mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                RrHitVolume v;
                if (!RrHitVolume.TryGet(h.collider, out v) || !Accepts(v))
                {
                    continue;
                }

                if (h.distance <= 0f && h.point == Vector3.zero)
                {
                    // The step started inside the volume (a point-blank shot): it hits where the step starts.
                    h.point = p0;
                    h.distance = 0f;
                }

                if (h.distance <= best)
                {
                    best = h.distance;
                    volumeHit = h;
                    hitVolume = v;
                }
            }

            if (hitVolume != null)
            {
                t.position = volumeHit.point;
                HitVolume(hitVolume, volumeHit.point, dir);
                return;
            }

            if (hitWorld)
            {
                t.position = worldHit.point;
                HitWorld(worldHit, dir);
                return;
            }

            Travelled += len;
            t.SetPositionAndRotation(p0 + step, Quaternion.LookRotation(dir));
        }

        /// <summary>Which hit volumes stop this shot.</summary>
        private bool Accepts(RrHitVolume v)
        {
            if (v.Enemy != null)
            {
                return OwnerEnemy == null && v.Enemy.Alive;
            }

            if (OwnerEnemy != null)
            {
                return true;
            }

            if (v.IsLocalPlayer && Owner == RrCombat.LocalId)
            {
                return false;
            }

            if (v.PlayerId == Owner)
            {
                return false;
            }

            return RrCombat.CanDamage(Owner, v.PlayerId);
        }

        private void HitVolume(RrHitVolume v, Vector3 point, Vector3 dir)
        {
            bool enemy = v.Enemy != null;
            if (enemy)
            {
                if (DecidesHits && OwnerEnemy == null)
                {
                    v.Enemy.TakeLocalHit(Shot.EnemyDamage(v.Part), v.Part, point, Shot.Tool);
                }
            }
            else if (OwnerEnemy != null)
            {
                if (v.IsLocalPlayer && RrHealth.Local != null)
                {
                    RrHealth.Local.ApplyDamage(Shot.PlayerDamage(v.Part), RrCombat.NoPlayer, v.Part, point, RrCombat.EnemyTool);
                }
            }
            else if (DecidesHits && v.PlayerId >= 0)
            {
                RrCombat.SendHit(v.PlayerId, Shot.PlayerDamage(v.Part), v.Part, Seq, point, Shot.Tool);
            }

            RrImpacts.Body(Shot, point, -dir, Color, enemy);
            if (Shot.ExplosionRadius > 0f)
            {
                Explode(point);
            }

            Despawn();
        }

        private void HitWorld(RaycastHit h, Vector3 dir)
        {
            RrImpacts.Surface(Shot, h.point, h.normal, Color, h.rigidbody == null);
            if (Shot.ExplosionRadius > 0f)
            {
                Explode(h.point + h.normal * 0.05f);
            }

            Despawn();
        }

        /// <summary>
        /// Splash (launchers): Rec Room's falloff is full damage to 0.666 of the radius, then down to 0.2 at the
        /// edge. The shooter judges AI and other players; an enemy's blast is judged by each client for itself.
        /// </summary>
        private void Explode(Vector3 point)
        {
            float r = Shot.ExplosionRadius;
            if (Shot.ExplosionParticle.HasValue)
            {
                RrImpacts.Fx(Shot.ExplosionParticle.Value, point, Quaternion.identity, 1f, Shot.Laser ? (Color?)null : Color);
            }

            if (!DecidesHits && OwnerEnemy == null)
            {
                return;
            }

            RrCombat.SyncPhysics();
            int n = Physics.OverlapSphereNonAlloc(point, r, Overlaps, RrHitVolume.Mask, QueryTriggerInteraction.Collide);
            var done = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                RrHitVolume v;
                if (!RrHitVolume.TryGet(Overlaps[i], out v))
                {
                    continue;
                }

                float d = Vector3.Distance(point, Overlaps[i].ClosestPoint(point));
                float k = d <= r * 0.666f ? 1f : Mathf.Lerp(1f, 0.2f, (d - r * 0.666f) / Mathf.Max(0.01f, r * 0.334f));
                if (v.Enemy != null)
                {
                    if (OwnerEnemy == null && v.Enemy.Alive && done.Add(v.Enemy.GetInstanceID()))
                    {
                        int dmg = Mathf.RoundToInt(Mathf.Lerp(Shot.EnemyMin, Shot.EnemyMax, k));
                        v.Enemy.TakeLocalHit(Mathf.Max(1, dmg), RrBodyPart.Body, point, Shot.Tool);
                    }
                }
                else if (OwnerEnemy != null)
                {
                    if (v.IsLocalPlayer && done.Add(-1) && RrHealth.Local != null)
                    {
                        RrHealth.Local.ApplyDamage(Mathf.RoundToInt(Shot.BodyDamage * k), RrCombat.NoPlayer, RrBodyPart.Body, point, RrCombat.EnemyTool);
                    }
                }
                else if (!v.IsLocalPlayer && v.PlayerId >= 0 && v.PlayerId != Owner && RrCombat.CanDamage(Owner, v.PlayerId) && done.Add(v.PlayerId + 100000))
                {
                    RrCombat.SendHit(v.PlayerId, Mathf.RoundToInt(Shot.ExplosionDamage * k), RrBodyPart.Body, Seq, point, Shot.Tool);
                }
            }
        }

        public void Despawn()
        {
            if (!_alive)
            {
                return;
            }

            _alive = false;
            Active.Remove(this);
            if (_trail != null)
            {
                _trail.emitting = false;
            }

            gameObject.SetActive(false);
            Stack<RrProjectile> pool;
            if (!Pools.TryGetValue(_poolKey, out pool))
            {
                pool = new Stack<RrProjectile>();
                Pools[_poolKey] = pool;
            }

            if (pool.Count < 64)
            {
                pool.Push(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // ---- looks ----

        private static string PoolKey(RrShotSpec shot)
        {
            if (shot.BulletHasMesh && shot.BulletPrefab.HasValue)
            {
                var e = shot.BulletPrefab.Value;
                string key = "bolt:" + e.Bundle + ":" + e.PathId;
                if (BoltAssets.ContainsKey(key) && BoltAssets[key] != null)
                {
                    return key;
                }

                if (!BoltLoading.Contains(key) && RrAssets.Instance != null)
                {
                    BoltLoading.Add(key);
                    RrAssets.Instance.LoadPrefab(e, (asset, data) =>
                    {
                        BoltAssets[key] = asset;
                        BoltData[key] = data;
                    });
                }
            }

            return "ball";
        }

        private static readonly Dictionary<string, RrPrefabData> BoltData = new Dictionary<string, RrPrefabData>();

        private static RrProjectile Take(string key, RrShotSpec shot)
        {
            Stack<RrProjectile> pool;
            if (Pools.TryGetValue(key, out pool))
            {
                while (pool.Count > 0)
                {
                    var p = pool.Pop();
                    if (p != null)
                    {
                        return p;
                    }
                }
            }

            var go = new GameObject("Rr Projectile");
            go.layer = RrLayers.Bullet;
            go.SetActive(false);
            go.transform.SetParent(Root, false);
            var proj = go.AddComponent<RrProjectile>();
            proj._poolKey = key;
            if (key == "ball")
            {
                proj.BuildBall();
            }
            else
            {
                GameObject asset;
                RrPrefabData data;
                BoltAssets.TryGetValue(key, out asset);
                BoltData.TryGetValue(key, out data);
                var inst = asset != null ? RrSpawn.SpawnLoaded(asset, data, go.transform, Vector3.zero, Quaternion.identity, RrSpawnOptions.Visual()) : null;
                if (inst != null)
                {
                    RrLayers.Set(inst.gameObject, RrLayers.Bullet);
                    proj._visual = inst.transform;
                    proj._renderers = inst.GetComponentsInChildren<Renderer>(true);
                }
                else
                {
                    proj.BuildBall();
                }
            }

            return proj;
        }

        private void BuildBall()
        {
            if (_ball == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _ball = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
            }

            var v = new GameObject("Ball");
            v.layer = RrLayers.Bullet;
            v.transform.SetParent(transform, false);
            v.AddComponent<MeshFilter>().sharedMesh = _ball;
            var mr = v.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _visual = v.transform;
            _renderers = new Renderer[] { mr };
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.minVertexDistance = 0.05f;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            _trail.numCapVertices = 2;
            _trail.autodestruct = false;
        }

        private void ApplyLook()
        {
            var c = Color;
            if (_trail != null)
            {
                float d = Mathf.Max(0.035f, Shot.Radius * 2f);
                _visual.localScale = new Vector3(d, d, Shot.Laser ? d * 3f : d);
                var mr = _renderers[0];
                mr.sharedMaterial = Shot.Laser ? RrLook.Emissive(c, 2f) : RrLook.Unlit(c);
                _trail.time = Shot.TrailTime;
                _trail.startWidth = Mathf.Max(Shot.TrailWidth, d * 0.8f);
                _trail.endWidth = 0f;
                _trail.sharedMaterial = Shot.TrailMaterial != null ? Shot.TrailMaterial : TrailMaterial();
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
                _trail.colorGradient = g;
            }
            else if (_renderers != null && Shot.TeamColored)
            {
                if (_block == null)
                {
                    _block = new MaterialPropertyBlock();
                }

                foreach (var r in _renderers)
                {
                    if (r == null)
                    {
                        continue;
                    }

                    r.GetPropertyBlock(_block);
                    _block.SetColor("_BaseColor", c);
                    _block.SetColor("_Color", c);
                    _block.SetColor("_TintColor", c);
                    r.SetPropertyBlock(_block);
                }
            }
        }

        /// <summary>Transparent, vertex-coloured, stereo-safe (URP Particles/Unlit) for our own trails.</summary>
        private static Material TrailMaterial()
        {
            if (_trailMaterial != null)
            {
                return _trailMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            _trailMaterial = new Material(shader) { name = "Rr Shot Trail" };
            _trailMaterial.SetFloat("_Surface", 1f);
            _trailMaterial.SetFloat("_Blend", 0f);
            _trailMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _trailMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _trailMaterial.SetFloat("_ZWrite", 0f);
            _trailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _trailMaterial.SetColor("_BaseColor", Color.white);
            _trailMaterial.renderQueue = 3000;
            return _trailMaterial;
        }

        internal static void ResetStatics()
        {
            Active.Clear();
            Pools.Clear();
            BoltAssets.Clear();
            BoltData.Clear();
            BoltLoading.Clear();
            _root = null;
            _block = null;
            _launched = 0;
            _loggedFirst = false;
            if (_trailMaterial != null)
            {
                Destroy(_trailMaterial);
                _trailMaterial = null;
            }

            _ball = null;
        }
    }
}
