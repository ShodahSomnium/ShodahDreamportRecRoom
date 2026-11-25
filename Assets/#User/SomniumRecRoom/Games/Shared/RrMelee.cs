using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// A held Rec Room melee tool (quest sword, cutlass, frying pan, shield...) that hurts by being swung. The numbers
    /// are the prefab's MeleeWeapon data (rr_quests.md 2.3, 3.6): enemyDamageSettings MinDamage..MaxDamage (sword
    /// 67-100) scaled by the blade tip's speed between minSpeedImpulseSpeed and maxSpeedImpulseSpeed (3-15 m/s,
    /// linear: GUESS), x2 on a head or weak spot when canApplyCriticalHits; playerDamageSettings for other players
    /// (friendly-fire rules apply); canBlockEnemyMeleeAttacks lets the blade parry (<see cref="TryBlock"/>);
    /// supportsThrowDamage x throwDamageMultiplier while thrown.
    ///
    /// The blade is the tool's longest mesh axis, swept every frame from its previous pose (a capsule overlap plus a
    /// sphere cast along the tip's path) against the hit volumes on layer 27 and the world, so a fast swing cannot
    /// skip a goblin. Each target is hit at most once per 0.35 s. Screen mode has no arm to swing: a click strikes
    /// along the aim ray (reach 2.2 m) at 60 % of the speed range.
    /// </summary>
    public sealed class RrMelee : MonoBehaviour
    {
        public string PrefabName;
        public RrInstance Instance;
        public RrGrabbable Grabbable;
        public byte Tool = RrCombat.UnknownTool;

        public int EnemyMin = 67;
        public int EnemyMax = 100;
        public int PlayerDamage = 100;
        public float MinSpeed = 3f;
        public float MaxSpeed = 15f;
        public bool Crits = true;
        public bool CanBlock = true;
        public bool ThrowDamage;
        public float ThrowMultiplier = 1f;

        public string SwingClip = "Throw01";
        public string HitEnemyClip = "Quest_MeleeImpact_01";
        public string HitHeavyClip = "Quest_Sword_ImpactHeavy01";
        public string HitEnvironmentClip = "Quest_Sword_ImpactEnvironment01";
        public string BlockClip = "Quest_Sword_Impactshield";

        /// <summary>Blade segment and thickness in the tool's own frame (from the mesh bounds).</summary>
        public Vector3 BladeBase;
        public Vector3 BladeTip = new Vector3(0f, 0f, 0.8f);
        public float BladeRadius = 0.07f;

        /// <summary>Held by the loadout's screen hand (no RrHand): clicks come from RrInput.</summary>
        public bool ScreenHeld;

        public float TipSpeed { get; private set; }

        public static readonly List<RrMelee> All = new List<RrMelee>();

        private static readonly Collider[] Overlaps = new Collider[16];
        private static readonly RaycastHit[] Casts = new RaycastHit[16];
        private readonly Dictionary<int, float> _recent = new Dictionary<int, float>();
        private Vector3 _lastTip;
        private Vector3 _lastBase;
        private bool _hasLast;
        private float _thrownUntil;
        private float _nextSwingSound;
        private float _nextWorldHit;
        private float _nextClick;
        private float _swingAnim;
        private static bool _loggedFirst;

        public bool IsHeld
        {
            get { return ScreenHeld || (Grabbable != null && Grabbable.IsHeld); }
        }

        /// <summary>Makes a spawned Rec Room tool a melee weapon (numbers from its MeleeWeapon data when present).</summary>
        public static RrMelee Attach(RrInstance inst, string prefabName)
        {
            var m = inst.GetComponent<RrMelee>();
            if (m == null)
            {
                m = inst.gameObject.AddComponent<RrMelee>();
            }

            m.Instance = inst;
            m.PrefabName = string.IsNullOrEmpty(prefabName) ? inst.name : prefabName;
            m.Tool = RrWeaponSpec.RegisterTool(m.PrefabName);
            m.Grabbable = inst.GetComponent<RrGrabbable>();
            if (m.Grabbable == null)
            {
                m.Grabbable = inst.gameObject.AddComponent<RrGrabbable>();
            }

            m.Grabbable.OnUseDown += m.UseDown;
            m.Grabbable.OnReleased += m.Released;
            m.Grabbable.OnGrabbed += m.Grabbed;
            m.ReadConfig(inst.Data);
            RrTools.ApplyHold(inst, m.Grabbable); // the hold pose as every tool reads it (Rec Room hand frame, legacy first)
            m.MeasureBlade();
            RrLog.Info("Melee", m.PrefabName + ": AI " + m.EnemyMin + "-" + m.EnemyMax + " over " + m.MinSpeed + "-" + m.MaxSpeed + " m/s, players " + m.PlayerDamage
                + (m.CanBlock ? ", blocks" : "") + (m.ThrowDamage ? ", thrown x" + m.ThrowMultiplier : "") + ", blade " + (m.BladeTip - m.BladeBase).magnitude.ToString("0.00") + " m");
            return m;
        }

        private void ReadConfig(RrPrefabData data)
        {
            var w = data != null ? data.Config("MeleeWeapon") : null;
            if (w == null)
            {
                return;
            }

            var enemy = w.Sub("enemyDamageSettings");
            if (enemy != null)
            {
                EnemyMin = enemy.Int("MinDamage", EnemyMin);
                EnemyMax = Mathf.Max(EnemyMin, enemy.Int("MaxDamage", EnemyMin));
            }

            int body = -1;
            foreach (var d in w.List("playerDamageSettings"))
            {
                int part = d.Int("BodyPart", -1);
                if (part == 1 || body < 0)
                {
                    body = d.Int("Damage", PlayerDamage);
                }
            }

            if (body >= 0)
            {
                PlayerDamage = body;
            }

            MinSpeed = w.Float("minSpeedImpulseSpeed", MinSpeed);
            MaxSpeed = Mathf.Max(MinSpeed + 0.1f, w.Float("maxSpeedImpulseSpeed", MaxSpeed));
            Crits = w.Bool("canApplyCriticalHits", Crits);
            CanBlock = w.Bool("canBlockEnemyMeleeAttacks", CanBlock);
            ThrowDamage = w.Bool("supportsThrowDamage", ThrowDamage);
            ThrowMultiplier = w.Float("throwDamageMultiplier", ThrowMultiplier);

            var tool = data.Config("Tool");
            var inter = tool != null ? tool.Sub("interactionSettings") : null;
            var pick = inter != null ? inter.Sub("RightHandPickupTransform") : null;
            if (pick != null && Grabbable != null)
            {
                var p = pick.Vector3("Position");
                var q = pick.Quaternion("Rotation");
                if (p != null && p.Length >= 3 && q != null && q.Length >= 4 && Mathf.Abs(q[0]) + Mathf.Abs(q[1]) + Mathf.Abs(q[2]) + Mathf.Abs(q[3]) > 0.01f)
                {
                    var rot = new Quaternion(q[0], q[1], q[2], q[3]);
                    Grabbable.HasGrip = true;
                    Grabbable.GripPosition = new Vector3(p[0], p[1], p[2]);
                    Grabbable.GripRotation = rot;
                    Grabbable.LeftGripPosition = new Vector3(-p[0], p[1], p[2]);
                    Grabbable.LeftGripRotation = new Quaternion(rot.x, -rot.y, -rot.z, rot.w);
                }

                if (inter.Bool("HasMaxThrowSpeed"))
                {
                    Grabbable.HasMaxThrowSpeed = true;
                    Grabbable.MaxThrowSpeed = inter.Float("MaxThrowSpeed", 8f);
                }
            }
        }

        /// <summary>The blade: the longest axis of the meshes' bounds in the tool's frame, from the grip end to the far end.</summary>
        private void MeasureBlade()
        {
            var root = transform;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                {
                    continue;
                }

                var mb = mf.sharedMesh.bounds;
                var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any)
                    {
                        b = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(p);
                    }
                }
            }

            if (!any)
            {
                return;
            }

            var e = b.extents;
            Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
            float half = Vector3.Dot(e, axis);
            // The hand in the sword's frame: the hold pose is the sword's pose in the hand, so the hand is at -(Inverse(gr) * gp).
            Vector3 grip = Grabbable != null && Grabbable.HasGrip ? Quaternion.Inverse(Grabbable.GripRotation) * -Grabbable.GripPosition : Vector3.zero;
            Vector3 endA = b.center + axis * half;
            Vector3 endB = b.center - axis * half;
            BladeTip = (endA - grip).sqrMagnitude >= (endB - grip).sqrMagnitude ? endA : endB;
            Vector3 other = BladeTip == endA ? endB : endA;
            BladeBase = Vector3.Lerp(other, BladeTip, 0.3f);
            float thick = (e.x + e.y + e.z - half) * 0.5f;
            BladeRadius = Mathf.Clamp(thick, 0.04f, 0.2f);
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            _hasLast = false;
        }

        private void OnDestroy()
        {
            if (Grabbable != null)
            {
                Grabbable.OnUseDown -= UseDown;
                Grabbable.OnReleased -= Released;
                Grabbable.OnGrabbed -= Grabbed;
            }
        }

        private void Grabbed(RrHand hand)
        {
            _thrownUntil = 0f;
            _hasLast = false;
        }

        private void Released(RrHand hand, Vector3 velocity)
        {
            _thrownUntil = ThrowDamage && velocity.magnitude > MinSpeed ? Time.time + 3f : 0f;
        }

        private void UseDown(RrHand hand)
        {
            if (!RrPlayer.InVr)
            {
                ScreenStrike();
            }
        }

        private void Update()
        {
            if (ScreenHeld && (Grabbable == null || !Grabbable.IsHeld) && RrInput.UseDown)
            {
                ScreenStrike();
            }

            if (ScreenHeld && _swingAnim > 0f)
            {
                _swingAnim = Mathf.Max(0f, _swingAnim - Time.deltaTime * 4f);
                float k = Mathf.Sin(_swingAnim * Mathf.PI);
                transform.localRotation = Quaternion.Euler(-60f + 95f * k, -25f * k, 0f);
            }
        }

        private void LateUpdate()
        {
            bool thrown = !IsHeld && Time.time < _thrownUntil;
            if ((!IsHeld && !thrown) || !RrHealth.CanAct)
            {
                _hasLast = false;
                return;
            }

            var t = transform;
            Vector3 tip = t.TransformPoint(BladeTip);
            Vector3 bas = t.TransformPoint(BladeBase);
            float dt = Time.deltaTime;
            if (!_hasLast || dt <= 0f)
            {
                _lastTip = tip;
                _lastBase = bas;
                _hasLast = true;
                TipSpeed = 0f;
                return;
            }

            TipSpeed = (tip - _lastTip).magnitude / dt;
            if (TipSpeed > MinSpeed * 1.5f && Time.time >= _nextSwingSound && IsHeld)
            {
                _nextSwingSound = Time.time + 0.45f;
                RrSound.Play(SwingClip, tip, 0.5f, Random.Range(0.9f, 1.1f));
            }

            if (TipSpeed >= MinSpeed * 0.8f)
            {
                float k = Mathf.Clamp01((TipSpeed - MinSpeed) / (MaxSpeed - MinSpeed));
                Sweep(bas, tip, k, thrown ? ThrowMultiplier : 1f);
            }

            _lastTip = tip;
            _lastBase = bas;
        }

        private void Sweep(Vector3 bas, Vector3 tip, float k, float multiplier)
        {
            RrCombat.SyncPhysics();
            int n = Physics.OverlapCapsuleNonAlloc(bas, tip, BladeRadius, Overlaps, RrHitVolume.Mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                HitCollider(Overlaps[i], Overlaps[i].ClosestPoint(tip), k, multiplier);
            }

            Vector3 path = tip - _lastTip;
            float len = path.magnitude;
            if (len > 1e-4f)
            {
                int c = Physics.SphereCastNonAlloc(_lastTip, BladeRadius, path / len, Casts, len, RrHitVolume.Mask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < c; i++)
                {
                    HitCollider(Casts[i].collider, Casts[i].point == Vector3.zero ? tip : Casts[i].point, k, multiplier);
                }

                c = Physics.SphereCastNonAlloc(_lastTip, BladeRadius, path / len, Casts, len, RrProjectile.WorldMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < c; i++)
                {
                    var col = Casts[i].collider;
                    if (col == null || col.transform.IsChildOf(transform) || Time.time < _nextWorldHit)
                    {
                        continue;
                    }

                    _nextWorldHit = Time.time + 0.25f;
                    RrSound.Play(HitEnvironmentClip, Casts[i].point, 0.6f, Random.Range(0.92f, 1.08f));
                    Buzz(0.4f);
                    if (!IsHeld)
                    {
                        _thrownUntil = 0f;
                    }

                    break;
                }
            }
        }

        private void HitCollider(Collider c, Vector3 point, float k, float multiplier)
        {
            RrHitVolume v;
            if (!RrHitVolume.TryGet(c, out v) || v.IsLocalPlayer)
            {
                return;
            }

            int key = v.Enemy != null ? v.Enemy.GetInstanceID() : 1000000 + v.PlayerId;
            float last;
            if (_recent.TryGetValue(key, out last) && Time.time - last < 0.35f)
            {
                return;
            }

            if (v.Enemy != null)
            {
                if (!v.Enemy.Alive)
                {
                    return;
                }

                _recent[key] = Time.time;
                int dmg = Mathf.RoundToInt(Mathf.Lerp(EnemyMin, EnemyMax, k) * multiplier);
                if (Crits && (v.Part == RrBodyPart.Head || v.Part == RrBodyPart.WeakSpot))
                {
                    dmg *= 2;
                }

                v.Enemy.TakeLocalHit(Mathf.Max(1, dmg), v.Part, point, Tool);
                RrSound.Play(k > 0.6f ? HitHeavyClip : HitEnemyClip, point, 0.8f, Random.Range(0.95f, 1.05f));
                Buzz(0.8f);
                LogFirst("enemy", dmg, k);
            }
            else if (v.PlayerId >= 0 && RrCombat.CanDamage(RrCombat.LocalId, v.PlayerId))
            {
                _recent[key] = Time.time;
                int dmg = Mathf.RoundToInt(PlayerDamage * multiplier);
                RrCombat.SendHit(v.PlayerId, dmg, v.Part, RrCombat.NextSeq(), point, Tool);
                RrSound.Play(HitEnemyClip, point, 0.8f, 1f);
                Buzz(0.8f);
                LogFirst("player " + v.PlayerId, dmg, k);
            }
        }

        /// <summary>Screen mode: a click strikes what the aim ray finds within reach (no arm to swing).</summary>
        private void ScreenStrike()
        {
            if (Time.time < _nextClick || !RrHealth.CanAct)
            {
                return;
            }

            _nextClick = Time.time + 0.5f;
            _swingAnim = 1f;
            var ray = RrInput.AimRay;
            RrSound.Play(SwingClip, ray.origin + ray.direction, 0.5f, Random.Range(0.9f, 1.1f));
            RrCombat.SyncPhysics();
            int n = Physics.SphereCastNonAlloc(ray, 0.3f, Casts, 2.2f, RrHitVolume.Mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                HitCollider(Casts[i].collider, Casts[i].point == Vector3.zero ? ray.GetPoint(Casts[i].distance) : Casts[i].point, 0.6f, 1f);
            }

            RaycastHit hit;
            if (Physics.SphereCast(ray, 0.1f, out hit, 2.2f, RrProjectile.WorldMask, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
            {
                RrSound.Play(HitEnvironmentClip, hit.point, 0.6f, 1f);
            }
        }

        /// <summary>
        /// An enemy's melee attack volume (centre, radius) meets a held blade that can block: the attack is parried
        /// (rr_quests.md 3.6: the defender's client checks its own tool during the active window).
        /// </summary>
        public static bool TryBlock(Vector3 center, float radius, out RrMelee by)
        {
            by = null;
            foreach (var m in All)
            {
                if (m == null || !m.CanBlock || !m.IsHeld)
                {
                    continue;
                }

                var t = m.transform;
                Vector3 a = t.TransformPoint(m.BladeBase);
                Vector3 b = t.TransformPoint(m.BladeTip);
                Vector3 ab = b - a;
                float s = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(center - a, ab) / ab.sqrMagnitude) : 0f;
                if (Vector3.Distance(a + ab * s, center) <= radius + m.BladeRadius)
                {
                    by = m;
                    RrSound.Play(m.BlockClip, a + ab * s, 0.8f, 1f);
                    m.Buzz(1f);
                    return true;
                }
            }

            return false;
        }

        private void Buzz(float amp)
        {
            var hand = Grabbable != null ? Grabbable.Holder : null;
            if (hand != null)
            {
                hand.Haptic(amp, 0.06f);
            }
        }

        private void LogFirst(string what, int dmg, float k)
        {
            if (!_loggedFirst)
            {
                _loggedFirst = true;
                RrLog.Info("Melee", "first melee hit: " + PrefabName + " on " + what + " for " + dmg + " (speed " + TipSpeed.ToString("0.0") + " m/s, k " + k.ToString("0.00") + ")");
            }
        }

        internal static void ResetStatics()
        {
            All.Clear();
            _loggedFirst = false;
        }
    }
}
