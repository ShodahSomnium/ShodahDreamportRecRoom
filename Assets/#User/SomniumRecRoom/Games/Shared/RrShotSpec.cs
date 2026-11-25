using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Everything one projectile needs: flight (Rec Room's Bullet component: colliderRadius 0.02, applyGravity with
    /// gravityRatio, airDrag, selfDestructTime 4 s; the weapon's bulletFireSpeed and maxDistance), damage (the
    /// weapon's playerDamageSettings per body part and enemyDamageSettings Min..Max) and looks (the bullet prefab
    /// and its BulletTrail: 0.175 s, 0.03 m for a paintball; the impact decal, particles and WeaponAudio hit
    /// clips). Weapons fill it from their configs (<see cref="RrWeaponSpec"/>); enemies build theirs in code
    /// (<see cref="RrEnemyType"/>). Paint arcs under gravity; lasers fly straight.
    /// </summary>
    public sealed class RrShotSpec
    {
        public string Name = "";
        public byte Tool = RrCombat.UnknownTool;

        // ---- flight ----
        public float Speed = 33f;
        public float Radius = 0.02f;

        /// <summary>Downward acceleration in m/s² (9.81 x gravityRatio when the Bullet applies gravity, else 0).</summary>
        public float Gravity = 9.81f;

        /// <summary>Rec Room's airDrag: velocity loses this fraction per second (paint thrower 0.585).</summary>
        public float Drag;

        public float Life = 4f;

        /// <summary>Metres after which the shot vanishes (shotgun 15, burst rifle 50); 0 for none.</summary>
        public float MaxDistance;

        /// <summary>A laser bolt (straight, glowing, scorch) rather than paint (arc, splat).</summary>
        public bool Laser;

        /// <summary>defaultProjectileColor: green paint for a player without a team, laser blue.</summary>
        public Color Color = new Color(0f, 1f, 0f, 1f);

        /// <summary>Paint and bolts take the shooter's team colour.</summary>
        public bool TeamColored = true;

        // ---- damage ----
        public int HeadDamage = 18;
        public int BodyDamage = 18;
        public int EnemyMin = 18;
        public int EnemyMax = 18;
        public bool Crits = true;

        /// <summary>Splash radius (launchers 2-5 m); 0 for a direct hit only.</summary>
        public float ExplosionRadius;
        public int ExplosionDamage;

        // ---- looks ----
        public RrEntry? BulletPrefab;

        /// <summary>The bullet prefab has meshes of its own (laser bolts); paintballs are only a trail.</summary>
        public bool BulletHasMesh;

        public float TrailTime = 0.175f;
        public float TrailWidth = 0.03f;
        public Material TrailMaterial;

        public RrEntry? ImpactDecal;
        public float DecalMin = 0.8f;
        public float DecalMax = 1.2f;
        public float DecalOffset = 0f;
        public float DecalLife = 2f;
        public float DecalFade = 1f;
        public RrEntry? ImpactParticle;
        public RrEntry? ExplosionParticle;

        public RrClipInfo HitPlayerSound;
        public RrClipInfo HitEnvironmentSound;
        public RrClipInfo HitEnemySound;

        /// <summary>Clip names used when the weapon's own clip structs are empty.</summary>
        public string HitPlayerClip = "PaintballPlayerHit01";
        public string HitEnvironmentClip = "PaintballEnvironmentHit01";
        public string HitEnemyClip = "EnemyHit_01";

        public int PlayerDamage(RrBodyPart part)
        {
            return part == RrBodyPart.Head ? HeadDamage : BodyDamage;
        }

        /// <summary>Damage to an AI: Min..Max (random), doubled on the head or weak spot when the weapon can crit (GUESS factor).</summary>
        public int EnemyDamage(RrBodyPart part)
        {
            int d = EnemyMax > EnemyMin ? Random.Range(EnemyMin, EnemyMax + 1) : EnemyMin;
            if (Crits && (part == RrBodyPart.Head || part == RrBodyPart.WeakSpot))
            {
                d *= 2;
            }

            return d;
        }

        /// <summary>Paint for enemies' attacks and tests: a plain paintball.</summary>
        public static RrShotSpec Paintball()
        {
            return new RrShotSpec { Name = "paintball" };
        }

        /// <summary>A straight glowing bolt (enemy lasers, orbs).</summary>
        public static RrShotSpec Bolt(float speed, int damage, Color color)
        {
            return new RrShotSpec
            {
                Name = "bolt",
                Speed = speed,
                Gravity = 0f,
                Laser = true,
                Radius = 0.05f,
                Color = color,
                TeamColored = false,
                HeadDamage = damage,
                BodyDamage = damage,
                TrailTime = 0.08f,
                TrailWidth = 0.05f,
                Crits = false,
                HitPlayerClip = "Arena_PlayerDamaged01",
                HitEnvironmentClip = "EnemyHit_02"
            };
        }
    }
}
