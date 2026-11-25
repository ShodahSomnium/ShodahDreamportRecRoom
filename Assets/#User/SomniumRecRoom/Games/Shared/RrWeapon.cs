using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// A held Rec Room gun: the spawned prefab (its own meshes, colliders and grip) with our behaviour on the root.
    /// The numbers are the prefab's RangedWeapon data (<see cref="RrWeaponSpec"/>): a click fires at most
    /// maximumFiringRate shots/s (5 for the paint pistol), an automatic gun keeps firing at
    /// maximumAutomaticFiringRate while the trigger is held (12 for the burst rifle, climbing up to totalBurstAngle 5
    /// deg), a shotgun sends its 9 spreadTargets pellets, guns with supportsAmmunition empty their magazine and reload
    /// after reloadDelay.
    ///
    /// One trigger pull = one Fire event (origin, direction, speed, seed): every other client rebuilds the same
    /// pellets and spread from the seed (<see cref="Directions"/>) and flies them as visuals
    /// (<see cref="ShowRemoteShot"/>); only this client decides hits. In screen mode the shot converges on what the
    /// crosshair ray hits (Rec Room screen mode aims at the reticle; the gun sits lower right), in VR it leaves along
    /// the barrel. Reload: VR right A for a gun in either hand (the client owns B/Y and left X) or the free hand's
    /// trigger with that hand on the gun (<see cref="ReloadAsked"/>), desktop RrInput.ReloadDown, and automatically
    /// when the magazine runs dry.
    /// </summary>
    public sealed class RrWeapon : MonoBehaviour
    {
        public string PrefabName;
        public RrWeaponSpec Spec;
        public RrInstance Instance;
        public RrGrabbable Grabbable;
        public Transform Barrel;
        public Transform MuzzleOrigin;

        public int Ammo;
        public int Reserve;
        public bool Reloading { get; private set; }

        /// <summary>Held by the loadout's own screen anchor (no RrHand yet): input comes from RrInput directly.</summary>
        public bool ScreenHeld;

        /// <summary>Paint colour override (null: the holder's team colour, or the shot's default).</summary>
        public Color? Paint;

        /// <summary>A trigger pull went out (after the projectiles were launched).</summary>
        public event Action<RrWeapon> OnFired;

        public static readonly List<RrWeapon> All = new List<RrWeapon>();

        private static readonly List<Vector3> Dirs = new List<Vector3>();
        private float _nextShot;
        private float _reloadDone;
        private bool _trigger;
        private int _burstShots;
        private int _appliedTeam = int.MinValue;
        private MaterialPropertyBlock _block;
        private static bool _loggedFirstFire;
        private static bool _loggedRemote;

        public bool Ready
        {
            get { return Spec != null; }
        }

        public RrHand Hand
        {
            get { return Grabbable != null ? Grabbable.Holder : null; }
        }

        public bool IsHeld
        {
            get { return ScreenHeld || Hand != null; }
        }

        public byte Tool
        {
            get { return Spec != null ? Spec.Tool : RrCombat.UnknownTool; }
        }

        // ---- creation ----

        /// <summary>Spawns a gun prefab and makes it a held weapon (grabbable, spec read from its data).</summary>
        public static void Create(string prefabName, Transform parent, Vector3 localPos, Quaternion localRot, Action<RrWeapon> done)
        {
            RrSpawn.Spawn(prefabName, parent, localPos, localRot, inst =>
            {
                if (inst == null)
                {
                    RrLog.WarnOnce("Weapon", "spawn:" + prefabName, "could not spawn " + prefabName);
                    if (done != null)
                    {
                        done(null);
                    }

                    return;
                }

                Attach(inst, prefabName, done);
            }, RrSpawnOptions.Prop());
        }

        /// <summary>Makes an already spawned Rec Room gun a weapon. ready: once its spec is read (null if it is not a gun).</summary>
        public static RrWeapon Attach(RrInstance inst, string prefabName, Action<RrWeapon> ready = null)
        {
            var w = inst.GetComponent<RrWeapon>();
            if (w == null)
            {
                w = inst.gameObject.AddComponent<RrWeapon>();
            }

            w.Instance = inst;
            w.PrefabName = string.IsNullOrEmpty(prefabName) ? inst.name : prefabName;
            w.Grabbable = inst.GetComponent<RrGrabbable>();
            if (w.Grabbable == null)
            {
                w.Grabbable = inst.gameObject.AddComponent<RrGrabbable>();
            }

            w.Grabbable.LockOnPickup = true;
            w.Grabbable.OnUseDown += w.HandUseDown;
            w.Grabbable.OnUseUp += w.HandUseUp;
            w.Grabbable.OnReleased += w.HandReleased;
            w.Grabbable.OnGrabbed += w.HandGrabbed;
            RrWeaponSpec.FromData(w.PrefabName, inst.Data, spec =>
            {
                if (w == null)
                {
                    return;
                }

                if (spec == null)
                {
                    RrLog.WarnOnce("Weapon", "nospec:" + w.PrefabName, w.PrefabName + " has no RangedWeapon data: not a gun");
                }
                else
                {
                    w.Setup(spec);
                }

                if (ready != null)
                {
                    ready(spec != null ? w : null);
                }
            });
            return w;
        }

        private void Setup(RrWeaponSpec spec)
        {
            Spec = spec;
            Barrel = Instance != null ? Instance.NodeOf(spec.BarrelRef) : null;
            if (Barrel == null)
            {
                var b = new GameObject("Rr Barrel").transform;
                b.SetParent(transform, false);
                b.localPosition = spec.BarrelLocalPosition;
                b.localRotation = spec.BarrelLocalRotation;
                Barrel = b;
            }

            MuzzleOrigin = Instance != null ? Instance.NodeOf(spec.MuzzleFlashOriginRef) : null;
            if (MuzzleOrigin == null)
            {
                MuzzleOrigin = Barrel;
            }

            var g = Grabbable;
            if (g != null)
            {
                // The hold pose (the gun's pose in Rec Room's hand frame, legacy first) and the screen-mode hand come
                // from the prefab's Tool, read like every other tool's; the spec's copy is the fallback.
                if (!RrTools.ApplyHold(Instance, g) && spec.HasGrip && spec.GripPosition.sqrMagnitude > 1e-8f)
                {
                    g.HasGrip = true;
                    g.GripSource = "weapon spec";
                    g.GripPosition = spec.GripPosition;
                    g.GripRotation = spec.GripRotation;
                    g.LeftGripPosition = new Vector3(-spec.GripPosition.x, spec.GripPosition.y, spec.GripPosition.z);
                    g.LeftGripRotation = new Quaternion(spec.GripRotation.x, -spec.GripRotation.y, -spec.GripRotation.z, spec.GripRotation.w);
                }

                g.ThrowMultiplier = spec.ThrowMultiplier;
                g.HasMaxThrowSpeed = true;
                g.MaxThrowSpeed = spec.MaxThrowSpeed;
                g.TwoHanded = spec.TwoHanded;
                if (spec.PickupSound.HasClip)
                {
                    g.PickupClip = spec.PickupSound;
                }
            }

            Ammo = spec.SupportsAmmo ? Mathf.Max(1, spec.Magazine) : 0;
            Reserve = spec.SupportsAmmo ? (spec.InfiniteReserve ? int.MaxValue : Mathf.Max(0, spec.TotalAmmo - Ammo)) : 0;
            ApplyTeamColor(true);
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            _trigger = false;
        }

        private void OnDestroy()
        {
            if (Grabbable != null)
            {
                Grabbable.OnUseDown -= HandUseDown;
                Grabbable.OnUseUp -= HandUseUp;
                Grabbable.OnReleased -= HandReleased;
                Grabbable.OnGrabbed -= HandGrabbed;
            }
        }

        // ---- input ----

        private void HandGrabbed(RrHand hand)
        {
            _trigger = false;
            ApplyTeamColor(true);
        }

        private void HandUseDown(RrHand hand)
        {
            _trigger = true;
            _burstShots = 0;
            TryFire();
        }

        private void HandUseUp(RrHand hand)
        {
            _trigger = false;
            _burstShots = 0;
        }

        private void HandReleased(RrHand hand, Vector3 velocity)
        {
            _trigger = false;
            _burstShots = 0;
        }

        private void Update()
        {
            if (Spec == null)
            {
                return;
            }

            if (Reloading && Time.time >= _reloadDone)
            {
                int take = Mathf.Min(Spec.Magazine - Ammo, Reserve);
                Ammo += take;
                if (Reserve != int.MaxValue)
                {
                    Reserve -= take;
                }

                Reloading = false;
            }

            if (ScreenHeld && Hand == null)
            {
                if (RrInput.UseDown)
                {
                    HandUseDown(null);
                }
                else if (RrInput.UseUp || (_trigger && !RrInput.UseHeld))
                {
                    HandUseUp(null);
                }
            }

            if (!IsHeld)
            {
                _trigger = false;
                return;
            }

            var hand = Hand;
            if ((hand == null || !RrPlayer.InVr) && RrInput.ReloadDown)
            {
                Reload();
            }
            else if (hand != null && RrPlayer.InVr && ReloadAsked(hand))
            {
                Reload();
            }

            if (_trigger && Spec.Automatic)
            {
                TryFire();
            }

            if ((Time.frameCount & 31) == 0)
            {
                ApplyTeamColor(false);
            }
        }

        // ---- firing ----

        /// <summary>One shot if the rate, the magazine and the holder allow it, aimed as the holder aims.</summary>
        public bool TryFire()
        {
            if (Spec == null || !RrHealth.CanAct || Time.time < _nextShot)
            {
                return false;
            }

            return FireToward(AimPoint());
        }

        /// <summary>
        /// A trigger pull toward a world point (tests, bots, scripted guns): checks the magazine but not the rate.
        /// Returns false when empty (and plays the empty click).
        /// </summary>
        public bool FireToward(Vector3 point)
        {
            if (Spec == null)
            {
                RrLog.WarnOnce("Weapon", "notready:" + PrefabName, PrefabName + ": fired before its data was read");
                return false;
            }

            if (Reloading)
            {
                return false;
            }

            Vector3 origin = Barrel != null ? Barrel.position : transform.position;
            if (Spec.SupportsAmmo && Ammo <= 0)
            {
                RrImpacts.Sound(Spec.EmptySound, "PaintballMisfire01", origin, 0.6f);
                _nextShot = Time.time + 0.25f;
                if (Spec.AutoReload)
                {
                    Reload();
                }

                return false;
            }

            Vector3 dir = point - origin;
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = Barrel != null ? Barrel.forward : transform.forward;
            }

            dir.Normalize();
            float rate = _trigger && Spec.Automatic ? Mathf.Max(Spec.AutoRate, 0.5f) : Mathf.Max(Spec.FireRate, 0.5f);
            _nextShot = Time.time + 1f / rate;
            if (Spec.BurstAngle > 0f && _trigger)
            {
                float climb = Mathf.Min(Spec.BurstAngle, _burstShots * 1f);
                var right = Vector3.Cross(Vector3.up, dir);
                if (right.sqrMagnitude > 1e-4f)
                {
                    dir = Quaternion.AngleAxis(-climb, right.normalized) * dir;
                }
            }

            _burstShots++;
            Fire(origin, dir);
            if (Spec.SupportsAmmo)
            {
                Ammo--;
                if (Ammo == 1)
                {
                    RrImpacts.Sound(Spec.LastRoundSound, null, origin, 0.5f);
                }

                if (Ammo <= 0 && Spec.AutoReload)
                {
                    Reload();
                }
            }

            return true;
        }

        private void Fire(Vector3 origin, Vector3 dir)
        {
            byte seed = (byte)UnityEngine.Random.Range(0, 256);
            byte seq = RrCombat.NextSeq();
            int me = RrCombat.LocalId;
            Directions(Spec, dir, seed, Dirs);
            for (int i = 0; i < Dirs.Count; i++)
            {
                RrProjectile.Launch(Spec.Shot, origin, Dirs[i] * Spec.Shot.Speed, me, true, seq, transform, null, Paint);
            }

            RrCombat.SendFire(Spec.Tool, seq, origin, dir, Spec.Shot.Speed, seed);
            var color = Paint ?? (Spec.Shot.TeamColored ? RrCombat.ColorOf(me, Spec.Shot.Color) : Spec.Shot.Color);
            var muzzle = MuzzleOrigin != null ? MuzzleOrigin : Barrel;
            RrImpacts.Muzzle(Spec, muzzle != null ? muzzle.position : origin, Quaternion.LookRotation(dir), color);
            RrImpacts.Sound(Spec.FireSound, Spec.Shot.Laser ? "LaserPistol_01" : "PaintballShoot01", origin, 0.9f);
            var hand = Hand;
            if (hand != null)
            {
                hand.Haptic(0.45f, 0.05f);
            }

            if (!_loggedFirstFire)
            {
                _loggedFirstFire = true;
                RrLog.Info("Weapon", "first local shot: " + PrefabName + " tool " + Spec.Tool + ", " + Dirs.Count + " projectile(s) at " + Spec.Shot.Speed + " m/s from " + origin.ToString("F2")
                    + " along " + dir.ToString("F2") + " (barrel forward " + (Barrel != null ? Barrel.forward.ToString("F2") : "-") + ")"
                    + (Spec.SupportsAmmo ? ", magazine " + Ammo + "/" + Spec.Magazine : ""));
            }

            var h = OnFired;
            if (h != null)
            {
                h(this);
            }
        }

        /// <summary>How close the free hand must come to the gun (its drawn bounds) for a trigger pull to reload it (m).</summary>
        public const float OffHandReach = 0.14f;

        /// <summary>
        /// VR reload, in either hand: the right controller's A (the client keeps X, B and Y: a gun in the left hand had no
        /// reload at all) while the right hand is free or holds this gun, or the free hand's trigger pulled with that hand
        /// on the gun - a magazine slap. The free hand's grip still takes the gun over (hand swap).
        /// </summary>
        private bool ReloadAsked(RrHand hand)
        {
            var right = RrHand.Get(RrHandSide.Right);
            if (right != null && right.PrimaryDown && (right == hand || right.Held == null))
            {
                return true;
            }

            var other = RrHand.Get(hand.Side == RrHandSide.Left ? RrHandSide.Right : RrHandSide.Left);
            if (other == null || other == hand || other.Held != null || !other.Tracked || !other.TriggerDown || !Touching(other.Position))
            {
                return false;
            }

            other.Haptic(0.35f, 0.05f);
            return true;
        }

        private Renderer[] _drawn;

        /// <summary>Whether a point is within <see cref="OffHandReach"/> of the gun's drawn meshes.</summary>
        private bool Touching(Vector3 p)
        {
            if (_drawn == null || _drawn.Length == 0)
            {
                _drawn = GetComponentsInChildren<Renderer>(false);
            }

            float reach = OffHandReach * OffHandReach;
            foreach (var r in _drawn)
            {
                if (r != null && r.enabled && r.bounds.SqrDistance(p) <= reach)
                {
                    return true;
                }
            }

            return false;
        }

        public void Reload()
        {
            if (Spec == null || !Spec.SupportsAmmo || Reloading || Ammo >= Spec.Magazine || Reserve <= 0)
            {
                return;
            }

            Reloading = true;
            _reloadDone = Time.time + Mathf.Max(0.1f, Spec.ReloadDelay);
            RrImpacts.Sound(Spec.ReloadSound, "Scifi_reload_generic_01", transform.position, 0.4f);
        }

        /// <summary>What the holder aims at: the crosshair's world point in screen mode, the barrel line in VR.</summary>
        private Vector3 AimPoint()
        {
            Vector3 origin = Barrel != null ? Barrel.position : transform.position;
            Vector3 fwd = Barrel != null ? Barrel.forward : transform.forward;
            var hand = Hand;
            bool screen = !RrPlayer.InVr && (hand == null || hand.Side == RrHandSide.Right);
            if (!screen)
            {
                return origin + fwd * 100f;
            }

            var ray = RrInput.AimRay;
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 200f, RrProjectile.WorldMask, QueryTriggerInteraction.Ignore) && hit.distance > 0.5f)
            {
                return hit.point;
            }

            return ray.GetPoint(100f);
        }

        /// <summary>
        /// The directions of one trigger pull, the same on every client for the same seed: the shotgun pattern (in the
        /// frame looking along dir, roll ignored) or the single shot, each jittered inside spreadAngleRange.
        /// </summary>
        public static void Directions(RrWeaponSpec spec, Vector3 dir, byte seed, List<Vector3> into)
        {
            into.Clear();
            if (dir.sqrMagnitude < 1e-8f)
            {
                dir = Vector3.forward;
            }

            dir.Normalize();
            var frame = Mathf.Abs(dir.y) > 0.999f ? Quaternion.LookRotation(dir, Vector3.forward) : Quaternion.LookRotation(dir, Vector3.up);
            var rng = new System.Random(seed);
            float spread = spec != null ? spec.SpreadAngle : 0f;
            if (spec != null && spec.Pellets.Count > 0)
            {
                foreach (var p in spec.Pellets)
                {
                    into.Add(Jitter(frame * p, frame, spread, rng));
                }
            }
            else
            {
                into.Add(Jitter(dir, frame, spread, rng));
            }
        }

        private static Vector3 Jitter(Vector3 d, Quaternion frame, float spread, System.Random rng)
        {
            if (spread <= 0f)
            {
                return d;
            }

            float yaw = ((float)rng.NextDouble() - 0.5f) * spread;
            float pitch = ((float)rng.NextDouble() - 0.5f) * spread;
            return frame * Quaternion.Euler(pitch, yaw, 0f) * Quaternion.Inverse(frame) * d;
        }

        /// <summary>Another member's trigger pull (from Fire): the same projectiles as visuals, the sound and the flash.</summary>
        public static void ShowRemoteShot(RrCombat.FireInfo f)
        {
            RrWeaponSpec.ByTool(f.Tool, spec =>
            {
                try
                {
                    var shot = spec != null ? spec.Shot : RrShotSpec.Paintball();
                    float speed = f.Speed > 0.1f ? f.Speed : shot.Speed;
                    Directions(spec, f.Direction, f.Seed, Dirs);
                    foreach (var d in Dirs)
                    {
                        RrProjectile.Launch(shot, f.Origin, d * speed, f.Shooter, false, f.Seq);
                    }

                    var color = shot.TeamColored ? RrCombat.ColorOf(f.Shooter, shot.Color) : shot.Color;
                    RrImpacts.Muzzle(spec, f.Origin, Quaternion.LookRotation(f.Direction.sqrMagnitude > 1e-6f ? f.Direction : Vector3.forward), color);
                    RrImpacts.Sound(spec != null ? spec.FireSound : default(RrClipInfo), shot.Laser ? "LaserPistol_01" : "PaintballShoot01", f.Origin, 0.9f);
                    if (!_loggedRemote)
                    {
                        _loggedRemote = true;
                        RrLog.Info("Weapon", "first remote shot drawn: player " + f.Shooter + ", tool " + f.Tool + " (" + (spec != null ? spec.PrefabName : "unknown, paintball look") + "), " + Dirs.Count + " projectile(s)");
                    }
                }
                catch (Exception e)
                {
                    RrLog.Error("Weapon", "remote shot failed", e);
                }
            });
        }

        // ---- team colour ----

        /// <summary>TeamColorRenderer targets take the holder's team colour (rr_shooters.md §11: colour the held gun).</summary>
        private void ApplyTeamColor(bool force)
        {
            if (Spec == null || Instance == null || Spec.TeamRenderers.Count == 0)
            {
                return;
            }

            int team = RrTeams.LocalTeam;
            if (!force && team == _appliedTeam)
            {
                return;
            }

            _appliedTeam = team;
            var c = Paint ?? (team >= 0 ? RrTeams.Color(team) : Spec.Shot.Color);
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            for (int i = 0; i < Spec.TeamRenderers.Count; i++)
            {
                var node = Instance.NodeOf(Spec.TeamRenderers[i]);
                var r = node != null ? node.GetComponent<Renderer>() : null;
                if (r == null)
                {
                    continue;
                }

                int index = i < Spec.TeamMaterialIndices.Count ? Spec.TeamMaterialIndices[i] : -1;
                if (index >= 0 && index < r.sharedMaterials.Length)
                {
                    r.GetPropertyBlock(_block, index);
                    _block.SetColor(Spec.ColorProperty, c);
                    r.SetPropertyBlock(_block, index);
                }
                else
                {
                    r.GetPropertyBlock(_block);
                    _block.SetColor(Spec.ColorProperty, c);
                    r.SetPropertyBlock(_block);
                }
            }
        }

        internal static void ResetStatics()
        {
            All.Clear();
            Dirs.Clear();
            _loggedFirstFire = false;
            _loggedRemote = false;
        }
    }
}
