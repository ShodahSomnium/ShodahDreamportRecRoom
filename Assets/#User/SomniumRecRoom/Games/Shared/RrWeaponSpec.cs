using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// A Rec Room gun as its prefab data describes it, read once per prefab and cached.
    ///
    /// Every number comes from the prefab's RangedWeapon component (Pistol, BurstFireGun, Shotgun, AutomaticGun,
    /// GrenadeLauncher, PaintThrower, BeamGun, Crossbow...; field names as the type trees give them, rr_shooters.md
    /// §3): maximumFiringRate (shots/s per click, 5 for the paint pistol), maximumAutomaticFiringRate (while held, 12
    /// for the burst rifle), bulletFireSpeed (33 m/s pistol, 250 scope rifle), playerDamageSettings (BodyPart 0 =
    /// head: 30 / 18 pistol), enemyDamageSettings, supportsAmmunition / ammunitionMagazineSize /
    /// hasInfiniteReserveAmmunition / maximumTotalAmmunition, reloadDelay, spreadAngleRange, totalBurstAngle (5 deg
    /// climb), spreadTargets (the shotgun's 9 pellet targets 3 m ahead), hasMaxDistance / maxDistance. The bullet's
    /// own Bullet component gives radius, gravity and drag; WeaponAudio the clips; Tool.interactionSettings the
    /// grip; TeamColorRenderer which renderers take the holder's team colour.
    ///
    /// Tool ids: the Fire event names the weapon by one byte, so every client must map ids to the same prefabs.
    /// <see cref="KnownTools"/> is a fixed table in code (never reorder it once shipped); a game's own extra weapons
    /// get ids from 200 in the order it registers them, which is the same on every client.
    /// </summary>
    public sealed class RrWeaponSpec
    {
        /// <summary>Ranged weapon prefabs with fixed wire ids (index = id). Append only.</summary>
        public static readonly string[] KnownTools =
        {
            "(PaintballGun)", "(PaintballAssaultRifle)", "(PaintballShotgun)", "(PaintballRifleScoped)",
            "(PaintballGrenadeLauncher)", "(Paintball_PaintThrower)", "(Arena_Pistol)", "(Arena_AutomaticGun)",
            "(Arena_Shotgun)", "(Arena_RailGun)", "(Arena_RocketLauncher)", "(Arena_PowerWeapon_BeamGun)",
            "(Quest_SciFi_Pistol)", "(Quest_SciFi_AutomaticGun)", "(Quest_SciFi_Shotgun)", "(Quest_SciFi_RailGun)",
            "(Crossbow)", "(Crossbow_Hunter)", "(Longbow)", "(Quest_Pirate1_Flintlock)", "(Quest_Dracula1_BlunderBuss)",
            "(Quest_Goblin_Wand)", "(Quest_Dracula1_1H-Crossbow)", "(Quest_Dracula1_Crossbow_Hunter)",
            "(Quest_Dracula1_Flintlock)", "(PaintballGun)Confetti", "(Sandbox_Projectile_Launcher)"
        };

        /// <summary>Rec Room's RangedWeapon subclasses, in the order they are looked for.</summary>
        public static readonly string[] RangedClasses =
        {
            "Pistol", "BurstFireGun", "Shotgun", "AutomaticGun", "GrenadeLauncher", "PaintThrower", "BeamGun",
            "Crossbow", "CrossbowSimple", "Bow", "ForceWandWeapon", "SandboxProjectileLauncher", "RangedWeapon"
        };

        private static readonly List<string> Registered = new List<string>();
        private static readonly Dictionary<string, RrWeaponSpec> Cache = new Dictionary<string, RrWeaponSpec>();
        private static readonly Dictionary<string, List<Action<RrWeaponSpec>>> Waiting = new Dictionary<string, List<Action<RrWeaponSpec>>>();

        public string PrefabName;
        public string Class;
        public byte Tool = RrCombat.UnknownTool;
        public RrPrefabData Data;
        public readonly RrShotSpec Shot = new RrShotSpec();

        /// <summary>Everything referenced (bullet, decal, flash) is resolved; the weapon can fire before, with defaults.</summary>
        public bool Ready;

        // ---- firing ----
        public float FireRate = 5f;
        public float AutoRate;
        public bool SupportsAmmo;
        public int Magazine;
        public int TotalAmmo;
        public bool InfiniteReserve = true;
        public bool AutoReload = true;
        public float ReloadDelay = 0.5f;
        public float SpreadAngle;
        public float BurstAngle;
        public float ExplosionDelay;

        /// <summary>Pellet directions in the barrel's frame (shotguns); empty for a single projectile.</summary>
        public readonly List<Vector3> Pellets = new List<Vector3>();

        // ---- parts (resolved against the live instance) ----
        public RrTarget BarrelRef;
        public RrTarget MuzzleFlashOriginRef;

        /// <summary>Barrel pose relative to the prefab root (from the data; used when no instance is at hand).</summary>
        public Vector3 BarrelLocalPosition = new Vector3(0f, 0f, 0.3f);
        public Quaternion BarrelLocalRotation = Quaternion.identity;

        public RrEntry? MuzzleFlash;

        // ---- holding ----
        public bool HasGrip;
        public Vector3 GripPosition;
        public Quaternion GripRotation = Quaternion.identity;
        public float ThrowMultiplier = 1f;
        public float MaxThrowSpeed = 10f;
        public bool TwoHanded;

        // ---- sounds ----
        public RrClipInfo FireSound;
        public RrClipInfo ReloadSound;
        public RrClipInfo EmptySound;
        public RrClipInfo MisfireSound;
        public RrClipInfo LastRoundSound;
        public RrClipInfo PickupSound;

        // ---- team colour ----
        public readonly List<RrTarget> TeamRenderers = new List<RrTarget>();
        public readonly List<int> TeamMaterialIndices = new List<int>();
        public string ColorProperty = "_BaseColor";

        public bool Automatic
        {
            get { return AutoRate > 0f; }
        }

        // ---- tool ids ----

        public static byte ToolIdOf(string prefabName)
        {
            for (int i = 0; i < KnownTools.Length; i++)
            {
                if (KnownTools[i] == prefabName)
                {
                    return (byte)i;
                }
            }

            int r = Registered.IndexOf(prefabName);
            return r >= 0 ? (byte)(200 + r) : RrCombat.UnknownTool;
        }

        public static string PrefabOfTool(byte tool)
        {
            if (tool < KnownTools.Length)
            {
                return KnownTools[tool];
            }

            int r = tool - 200;
            return r >= 0 && r < Registered.Count ? Registered[r] : null;
        }

        /// <summary>A game's own weapon prefab outside the fixed table (call in the same order on every client).</summary>
        public static byte RegisterTool(string prefabName)
        {
            byte id = ToolIdOf(prefabName);
            if (id != RrCombat.UnknownTool)
            {
                return id;
            }

            if (Registered.Count >= 50)
            {
                return RrCombat.UnknownTool;
            }

            Registered.Add(prefabName);
            return (byte)(200 + Registered.Count - 1);
        }

        // ---- loading ----

        /// <summary>The spec of a weapon prefab (loads and parses its data once; callback on the main thread, null on failure).</summary>
        public static void Load(string prefabName, Action<RrWeaponSpec> done)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                done(null);
                return;
            }

            RrWeaponSpec spec;
            if (Cache.TryGetValue(prefabName, out spec) && (spec == null || spec.Ready))
            {
                done(spec);
                return;
            }

            List<Action<RrWeaponSpec>> list;
            if (Waiting.TryGetValue(prefabName, out list))
            {
                list.Add(done);
                return;
            }

            list = new List<Action<RrWeaponSpec>> { done };
            Waiting[prefabName] = list;
            RrEntry e;
            if (RrAssets.Instance == null || !RrAssets.Instance.TryFind(prefabName, 1, out e))
            {
                Finish(prefabName, null);
                return;
            }

            RrAssets.Instance.PrefabDataAsync(e, data =>
            {
                if (data == null)
                {
                    Finish(prefabName, null);
                    return;
                }

                FromData(prefabName, data, s => Finish(prefabName, s));
            });
        }

        /// <summary>The spec from already parsed data (a spawned weapon's RrInstance.Data); cached by name.</summary>
        public static void FromData(string prefabName, RrPrefabData data, Action<RrWeaponSpec> done)
        {
            RrWeaponSpec spec;
            if (Cache.TryGetValue(prefabName, out spec))
            {
                if (spec == null || spec.Ready)
                {
                    done(spec);
                }
                else
                {
                    List<Action<RrWeaponSpec>> list;
                    if (!Waiting.TryGetValue(prefabName, out list))
                    {
                        list = new List<Action<RrWeaponSpec>>();
                        Waiting[prefabName] = list;
                    }

                    list.Add(done);
                }

                return;
            }

            spec = Parse(prefabName, data);
            Cache[prefabName] = spec;
            if (spec == null)
            {
                done(null);
                return;
            }

            spec.ResolveReferences(() =>
            {
                spec.Ready = true;
                RrLog.Info("Weapon", spec.Describe());
                done(spec);
                List<Action<RrWeaponSpec>> list;
                if (Waiting.TryGetValue(prefabName, out list))
                {
                    Waiting.Remove(prefabName);
                    foreach (var cb in list)
                    {
                        cb(spec);
                    }
                }
            });
        }

        private static void Finish(string prefabName, RrWeaponSpec spec)
        {
            List<Action<RrWeaponSpec>> list;
            if (!Waiting.TryGetValue(prefabName, out list))
            {
                return;
            }

            Waiting.Remove(prefabName);
            if (spec == null)
            {
                RrLog.WarnOnce("Weapon", "spec:" + prefabName, "no weapon data for " + prefabName);
            }

            foreach (var cb in list)
            {
                try
                {
                    cb(spec);
                }
                catch (Exception e)
                {
                    RrLog.Error("Weapon", "spec callback for " + prefabName + " failed", e);
                }
            }
        }

        /// <summary>A known spec by tool id, loading it when needed (remote shots).</summary>
        public static void ByTool(byte tool, Action<RrWeaponSpec> done)
        {
            string name = PrefabOfTool(tool);
            if (name == null)
            {
                done(null);
                return;
            }

            Load(name, done);
        }

        /// <summary>The RangedWeapon config of a prefab (the first of <see cref="RangedClasses"/> present), or null.</summary>
        public static RrConfig RangedConfig(RrPrefabData data, out string className)
        {
            className = null;
            if (data == null)
            {
                return null;
            }

            foreach (string c in RangedClasses)
            {
                var comp = data.FindComponent(c);
                if (comp != null)
                {
                    className = c;
                    return data.Config(comp);
                }
            }

            return null;
        }

        private static RrWeaponSpec Parse(string prefabName, RrPrefabData data)
        {
            string cls;
            RrConfig w;
            try
            {
                w = RangedConfig(data, out cls);
            }
            catch (Exception e)
            {
                RrLog.Warn("Weapon", "reading the weapon config of " + prefabName + " failed: " + e.Message);
                return null;
            }

            if (w == null)
            {
                RrLog.WarnOnce("Weapon", "noranged:" + prefabName, prefabName + " has no RangedWeapon component");
                return null;
            }

            var s = new RrWeaponSpec { PrefabName = prefabName, Class = cls, Data = data, Tool = ToolIdOf(prefabName) };
            var shot = s.Shot;
            shot.Name = prefabName;
            shot.Tool = s.Tool;

            s.FireRate = Mathf.Max(0.1f, w.Float("maximumFiringRate", 5f));
            s.AutoRate = w.Float("maximumAutomaticFiringRate", 0f);
            s.SupportsAmmo = w.Bool("supportsAmmunition");
            s.Magazine = w.Int("ammunitionMagazineSize");
            s.TotalAmmo = w.Int("maximumTotalAmmunition");
            s.InfiniteReserve = w.Bool("hasInfiniteReserveAmmunition", true);
            s.AutoReload = w.Bool("automaticallyReloadMagazine", true);
            s.ReloadDelay = w.Float("reloadDelay", 0.5f);
            s.SpreadAngle = Mathf.Max(0f, w.Float("spreadAngleRange"));
            s.BurstAngle = w.Float("totalBurstAngle");
            s.ExplosionDelay = w.Float("explosionDelay");

            shot.Speed = Mathf.Max(1f, w.Float("bulletFireSpeed", 30f));
            if (w.Bool("hasMaxDistance"))
            {
                shot.MaxDistance = w.Float("maxDistance");
            }

            float life = w.Float("maxProjectileLifetime", -1f);
            if (life > 0f)
            {
                shot.Life = life;
            }

            var color = w.Color("defaultProjectileColor");
            if (color[3] > 0f)
            {
                shot.Color = new Color(color[0], color[1], color[2], 1f);
            }

            int head = -1;
            int body = -1;
            int other = -1;
            foreach (var d in w.List("playerDamageSettings"))
            {
                int part = d.Int("BodyPart", -1);
                int dmg = d.Int("Damage");
                if (part == 0)
                {
                    head = dmg;
                }
                else if (part == 1)
                {
                    body = dmg;
                }
                else if (other < 0)
                {
                    other = dmg;
                }
            }

            if (body < 0)
            {
                body = other >= 0 ? other : head >= 0 ? head : 18;
            }

            shot.BodyDamage = body;
            shot.HeadDamage = head >= 0 ? head : body;

            var enemy = w.Sub("enemyDamageSettings");
            if (enemy != null)
            {
                shot.EnemyMin = enemy.Int("MinDamage", shot.BodyDamage);
                shot.EnemyMax = Mathf.Max(shot.EnemyMin, enemy.Int("MaxDamage", shot.EnemyMin));
            }

            shot.Crits = w.Bool("canApplyCriticalHits", true);
            shot.ExplosionRadius = w.Float("explosionRadius");
            shot.ExplosionDamage = shot.BodyDamage;
            var decalScale = w.Raw("minMaxImpactDecalScale") as Dictionary<string, object>;
            if (decalScale != null)
            {
                shot.DecalMin = RrConfig.ToFloat(decalScale.ContainsKey("x") ? decalScale["x"] : null, 0.8f);
                shot.DecalMax = RrConfig.ToFloat(decalScale.ContainsKey("y") ? decalScale["y"] : null, 1.2f);
            }

            s.BarrelRef = w.Ref("barrelTransform");
            s.MuzzleFlashOriginRef = w.Ref("muzzleFlashOrigin");
            var barrelNode = s.BarrelRef.Node ?? s.MuzzleFlashOriginRef.Node ?? data.FindNode("BarrelFirePosition") ?? data.FindNode("Barrel");
            Matrix4x4 barrel = Matrix4x4.identity;
            if (barrelNode != null)
            {
                barrel = RootMatrix(barrelNode);
                s.BarrelLocalPosition = barrel.MultiplyPoint3x4(Vector3.zero);
                s.BarrelLocalRotation = barrel.rotation;
            }

            // Shotgun pellets: directions from the barrel to each spread target, in the barrel's frame.
            var toBarrel = barrel.inverse;
            foreach (var t in w.List("spreadTargets"))
            {
                var end = t.Ref("End");
                if (end.Node == null)
                {
                    continue;
                }

                var start = t.Ref("Start");
                Vector3 a = start.Node != null ? toBarrel.MultiplyPoint3x4(RootMatrix(start.Node).MultiplyPoint3x4(Vector3.zero)) : Vector3.zero;
                Vector3 b = toBarrel.MultiplyPoint3x4(RootMatrix(end.Node).MultiplyPoint3x4(Vector3.zero));
                var dir = b - a;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    s.Pellets.Add(dir.normalized);
                }
            }

            // Tool: grip, throw.
            var tool = data.Config("Tool");
            var inter = tool != null ? tool.Sub("interactionSettings") : null;
            if (inter != null)
            {
                var pick = inter.Sub("RightHandPickupTransform");
                if (pick != null)
                {
                    s.HasGrip = true;
                    s.GripPosition = Vec3(pick.Raw("Position"));
                    var q = pick.Quaternion("Rotation");
                    var rot = new Quaternion(q[0], q[1], q[2], q[3]);
                    s.GripRotation = Mathf.Abs(rot.x) + Mathf.Abs(rot.y) + Mathf.Abs(rot.z) + Mathf.Abs(rot.w) > 0.01f ? Normalize(rot) : Quaternion.identity;
                }

                s.ThrowMultiplier = inter.Float("ThrowSpeedMultiplier", 1f);
                if (inter.Bool("HasMaxThrowSpeed"))
                {
                    s.MaxThrowSpeed = inter.Float("MaxThrowSpeed", 10f);
                }
            }

            var audio = tool != null ? tool.Sub("audioSettings") : null;
            if (audio != null)
            {
                s.PickupSound = audio.Clip("PickupAudio");
            }

            s.TwoHanded = data.FindComponent("WeaponStabilizationGrip") != null;

            // WeaponAudio.
            var wa = data.Config("WeaponAudio");
            if (wa != null)
            {
                s.FireSound = wa.Clip("onFire");
                s.ReloadSound = wa.Clip("onReload");
                s.EmptySound = wa.Clip("onOutOfAmmoFire");
                s.MisfireSound = wa.Clip("onMisfire");
                s.LastRoundSound = wa.Clip("onLastRoundSpent");
                shot.HitPlayerSound = wa.Clip("onHitPlayer");
                shot.HitEnvironmentSound = wa.Clip("onHitEnvironment");
                shot.HitEnemySound = wa.Clip("onEnemyHit");
            }

            // TeamColorRenderer.
            var tc = data.Config("TeamColorRenderer");
            if (tc != null)
            {
                s.ColorProperty = tc.String("colorPropertyName", "_BaseColor");
                foreach (var r in tc.List("teamColorRenderers"))
                {
                    var target = r.Ref("renderer");
                    if (target.Node != null)
                    {
                        s.TeamRenderers.Add(target);
                        s.TeamMaterialIndices.Add(r.Int("materialIndex"));
                    }
                }
            }

            return s;
        }

        /// <summary>Resolves the bullet, flash, decal and particle prefabs the config points at (async; defaults until then).</summary>
        private void ResolveReferences(Action done)
        {
            var w = RangedConfig(Data, out Class);
            int pending = 1;
            Action one = () =>
            {
                pending--;
                if (pending == 0)
                {
                    done();
                }
            };

            var bullet = w.Ref("bulletPrefab");
            if (!bullet.IsNull)
            {
                pending++;
                RrPrefabRefs.ConfigOf(Data, bullet, b =>
                {
                    if (b != null)
                    {
                        Shot.Radius = Mathf.Max(0.005f, b.Float("colliderRadius", 0.02f));
                        Shot.Gravity = b.Bool("applyGravity", true) ? 9.81f * b.Float("gravityRatio", 1f) : 0f;
                        Shot.Drag = Mathf.Max(0f, b.Float("airDrag"));
                        float life = b.Float("selfDestructTime", 4f);
                        if (life > 0f && Shot.Life >= 4f)
                        {
                            Shot.Life = life;
                        }

                        float dist = b.Float("selfDestructDistance", -1f);
                        if (dist > 0f && (Shot.MaxDistance <= 0f || dist < Shot.MaxDistance))
                        {
                            Shot.MaxDistance = dist;
                        }
                    }

                    Shot.Laser = Shot.Gravity <= 0f;
                    one();
                });

                pending++;
                RrPrefabRefs.RootOf(Data, bullet, root =>
                {
                    Shot.BulletPrefab = root;
                    if (!root.HasValue)
                    {
                        one();
                        return;
                    }

                    RrPrefabRefs.DataOf(root.Value, bd =>
                    {
                        if (bd != null)
                        {
                            Shot.BulletHasMesh = HasMesh(bd.Root);
                            var trail = bd.Config("BulletTrail");
                            if (trail != null)
                            {
                                Shot.TrailTime = Mathf.Clamp(trail.Float("lifetime", 0.175f), 0.02f, 2f);
                                var widths = trail.Raw("widths") as Dictionary<string, object>;
                                if (widths != null && widths.ContainsKey("y"))
                                {
                                    Shot.TrailWidth = Mathf.Clamp(RrConfig.ToFloat(widths["y"], 0.03f), 0.005f, 0.3f);
                                }

                                var mats = trail.Refs("materials");
                                if (mats.Count > 0 && RrAssets.Instance != null)
                                {
                                    pending++;
                                    RrAssets.Instance.Resolve<Material>(mats[0], m =>
                                    {
                                        Shot.TrailMaterial = m != null ? RrMaterials.Fix(m) : null;
                                        one();
                                    });
                                }
                            }
                        }

                        one();
                    });
                });
            }

            ResolveRoot(w.Ref("muzzleFlashPrefab"), e => MuzzleFlash = e, ref pending, one);
            ResolveRoot(w.Ref("impactParticlePrefab"), e => Shot.ImpactParticle = e, ref pending, one);
            var explosion = w.Ref("explosionParticles");
            if (explosion.IsNull)
            {
                explosion = w.Ref("explosionParticlesPrefab");
            }

            ResolveRoot(explosion, e => Shot.ExplosionParticle = e, ref pending, one);
            var decal = w.Ref("impactDecalPrefab");
            if (!decal.IsNull)
            {
                pending++;
                RrPrefabRefs.ConfigOf(Data, decal, dc =>
                {
                    if (dc != null)
                    {
                        Shot.DecalLife = dc.Float("lifetime", 2f);
                        Shot.DecalFade = dc.Float("fadeOutTime", 1f);
                        Shot.DecalOffset = dc.Float("bias", 0.01f);
                    }

                    one();
                });
                ResolveRoot(decal, e => Shot.ImpactDecal = e, ref pending, one);
            }

            one();
        }

        private void ResolveRoot(RrTarget target, Action<RrEntry?> set, ref int pending, Action one)
        {
            if (target.IsNull)
            {
                return;
            }

            pending++;
            RrPrefabRefs.RootOf(Data, target, e =>
            {
                set(e);
                one();
            });
        }

        private static bool HasMesh(RrNodeData n)
        {
            foreach (var c in n.Components)
            {
                if (c.ClassId == 23 || c.ClassId == 137)
                {
                    return true;
                }
            }

            foreach (var k in n.Children)
            {
                if (k.Active && HasMesh(k))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A node's pose relative to the prefab root (the root's own pose is replaced when spawned).</summary>
        public static Matrix4x4 RootMatrix(RrNodeData node)
        {
            var m = Matrix4x4.identity;
            for (var n = node; n != null && n.Parent != null; n = n.Parent)
            {
                var p = n.LocalPosition;
                var r = n.LocalRotation;
                var s = n.LocalScale;
                var q = new Quaternion(r[0], r[1], r[2], r[3]);
                m = Matrix4x4.TRS(new Vector3(p[0], p[1], p[2]), Normalize(q), new Vector3(s[0], s[1], s[2])) * m;
            }

            return m;
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return mag > 1e-5f ? new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag) : Quaternion.identity;
        }

        private static Vector3 Vec3(object raw)
        {
            var list = raw as List<object>;
            if (list != null && list.Count >= 3)
            {
                return new Vector3(RrConfig.ToFloat(list[0], 0f), RrConfig.ToFloat(list[1], 0f), RrConfig.ToFloat(list[2], 0f));
            }

            var d = raw as Dictionary<string, object>;
            if (d != null)
            {
                object x, y, z;
                d.TryGetValue("x", out x);
                d.TryGetValue("y", out y);
                d.TryGetValue("z", out z);
                return new Vector3(RrConfig.ToFloat(x, 0f), RrConfig.ToFloat(y, 0f), RrConfig.ToFloat(z, 0f));
            }

            return Vector3.zero;
        }

        public string Describe()
        {
            return PrefabName + " (" + Class + ", tool " + Tool + "): " + FireRate + "/s"
                + (AutoRate > 0f ? ", auto " + AutoRate + "/s" : "")
                + ", " + Shot.Speed + " m/s" + (Shot.Gravity > 0f ? " arc" : " straight")
                + ", head " + Shot.HeadDamage + " / body " + Shot.BodyDamage + ", AI " + Shot.EnemyMin + "-" + Shot.EnemyMax
                + (SupportsAmmo ? ", magazine " + Magazine + (InfiniteReserve ? "" : " of " + TotalAmmo) : ", no ammo")
                + (Pellets.Count > 0 ? ", " + Pellets.Count + " pellets" : "")
                + (SpreadAngle > 0f ? ", spread " + SpreadAngle : "")
                + (BurstAngle > 0f ? ", burst climb " + BurstAngle : "")
                + (Shot.MaxDistance > 0f ? ", range " + Shot.MaxDistance + " m" : "")
                + ", bullet " + (Shot.BulletPrefab.HasValue ? Shot.BulletPrefab.Value.Name : "-")
                + ", decal " + (Shot.ImpactDecal.HasValue ? Shot.ImpactDecal.Value.Name : "-")
                + ", flash " + (MuzzleFlash.HasValue ? MuzzleFlash.Value.Name : "-");
        }

        internal static void ResetStatics()
        {
            Cache.Clear();
            Waiting.Clear();
            Registered.Clear();
        }
    }
}
