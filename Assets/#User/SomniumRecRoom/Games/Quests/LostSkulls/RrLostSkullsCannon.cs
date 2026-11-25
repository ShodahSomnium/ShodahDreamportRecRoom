using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls
{
    /// <summary>
    /// A ship's cannon ([Quest_Pirate1_Cannon]; the level draws it, the install has no cannon prefab, 8.2). Rec Room lights
    /// its fuse with a torch and turns it by its handle ("small turning radius", 8.1); here the fuse is a button on the
    /// breech (poke it, point and pull the trigger, or click it) and the cannon turns itself within
    /// <see cref="Arc"/> degrees of the level's facing to the best target: the High Seas bridge target, then Ghost
    /// Beard, then the nearest skeleton; with none it fires straight ahead. A press asks the authority
    /// (<see cref="RrLostSkullsRoom.HazardCannon"/>), which resolves the hit (<see cref="Aim"/> is deterministic from
    /// the shared state) and counts the shot in a quest int; every client then plays the flash, the ball's arc and the blast.
    /// </summary>
    public sealed class RrLostSkullsCannon : MonoBehaviour
    {
        public enum Target
        {
            None,
            Bridge,
            Boss,
            Enemy
        }

        /// <summary>
        /// How far (degrees) the cannon turns from the level's facing, and its reach. 55 degrees: seen from the muzzle
        /// (1.3 m ahead), Ghost Beard's Fight Position 3 is 45.6 degrees off both boss cannons, so at 45 degrees his third
        /// hit never came (iteration 13 tour); positions 1 and 2 stay 70-75 degrees off the other cannon (the order holds).
        /// </summary>
        public const float Arc = 55f, Range = 60f;

        /// <summary>The ball's speed (m/s; rr_quests.md 4.3 GUESS 30).</summary>
        public const float BallSpeed = 30f;

        public const string MuzzleFx = "[Quest_Pirate1_FlintlockFire_VFX]";
        public const string SmokeFx = "VFX_Generic_SmokePoof01";
        public const string BlastFx = "(Arena_Grenade_Explosion_VFX)";
        public const string BoomClip = "Paintball_GrenadeExplode01";

        public int Act;
        public int Index;

        private RrLostSkullsRoom _room;

        /// <summary>The cannon at its marker (act-local position, the level's yaw), with its fuse button on the breech.</summary>
        public static RrLostSkullsCannon Create(RrLostSkullsRoom room, Transform parent, int act, int index, Vector3 local, float yaw)
        {
            var go = RrBuild.Group(parent, "Cannon " + (index + 1), local, Quaternion.Euler(0f, yaw, 0f));
            var c = go.AddComponent<RrLostSkullsCannon>();
            c._room = room;
            c.Act = act;
            c.Index = index;
            // The fuse: a red-orange button on a short post behind the barrel, facing up.
            var post = RrLook.Solid(new Color(0.25f, 0.18f, 0.12f));
            RrBuild.Box(go.transform, "Fuse Post", new Vector3(0f, 0.45f, -0.95f), new Vector3(0.12f, 0.9f, 0.12f), post, false);
            RrPressable.Create(go.transform, "Cannon Fuse", new Vector3(0f, 0.92f, -0.95f), Quaternion.identity, new Color(0.95f, 0.35f, 0.1f), 0.24f,
                () => room.PressHazard(RrLostSkullsRoom.HazardCannon, index));
            RrLog.Info("LostSkulls", "act " + (act + 1) + " cannon " + index + " at " + local.ToString("F1") + " yaw " + yaw.ToString("0"));
            return c;
        }

        public Vector3 Muzzle
        {
            get { return transform.TransformPoint(new Vector3(0f, 0.75f, 1.3f)); }
        }

        /// <summary>
        /// Where a shot lands and what it hits (world). Every client computes the same from the shared state: the High
        /// Seas bridge while it is up, Ghost Beard at his current fight position, else the nearest living skeleton in
        /// the arc, else 40 m straight ahead.
        /// </summary>
        public Vector3 Aim(RrLostSkullsRoom room, out Target kind)
        {
            kind = Target.None;
            Vector3 p;
            if (Act == RrLostSkullsRoom.HighSeasAct && room.Bridge != null && !room.Bridge.IsDown && InArc(room.Bridge.TargetWorld))
            {
                kind = Target.Bridge;
                return room.Bridge.TargetWorld;
            }

            if (Act == RrLostSkullsRoom.BossAct && room.GhostBeard != null && room.GhostBeard.Hittable(out p) && InArc(p))
            {
                kind = Target.Boss;
                return p;
            }

            float best = Arc;
            Vector3 at = Vector3.zero;
            for (int s = 0; s < RrQuestEnemies.Slots; s++)
            {
                var e = room.Enemies.At(s);
                if (e == null || !e.Alive || (room.GhostBeard != null && e == room.GhostBeard.Body))
                {
                    continue;
                }

                float a = Angle(e.Center);
                if (a < best && (e.Center - Muzzle).sqrMagnitude < Range * Range)
                {
                    best = a;
                    at = e.Center;
                    kind = Target.Enemy;
                }
            }

            if (kind == Target.Enemy)
            {
                return at;
            }

            Vector3 f = transform.forward;
            f.y = 0f;
            return Muzzle + f.normalized * 40f + Vector3.down * 4f;
        }

        private float Angle(Vector3 world)
        {
            Vector3 d = world - Muzzle;
            d.y = 0f;
            Vector3 f = transform.forward;
            f.y = 0f;
            return d.sqrMagnitude < 0.01f ? 0f : Vector3.Angle(f, d);
        }

        private bool InArc(Vector3 world)
        {
            return Angle(world) <= Arc && (world - Muzzle).sqrMagnitude < Range * Range;
        }

        /// <summary>Every client: the flash and boom at the muzzle, the ball's arc, the blast where it lands.</summary>
        public void PlayShot(Vector3 to)
        {
            Vector3 from = Muzzle;
            RrFx.Spawn(MuzzleFx, from, transform.rotation, 3f);
            RrFx.Spawn(SmokeFx, from, Quaternion.identity, 2f);
            RrSound.Play(BoomClip, from, 1f, 0.7f, 80f);
            var ball = RrBuild.Sphere(transform.parent, "Cannonball", transform.parent != null ? transform.parent.InverseTransformPoint(from) : from, 0.14f,
                RrLook.Solid(new Color(0.08f, 0.08f, 0.09f)), false);
            var b = ball.AddComponent<Ball>();
            b.From = from;
            b.To = to;
            b.Seconds = Mathf.Clamp(Vector3.Distance(from, to) / BallSpeed, 0.2f, 2.5f);
        }

        /// <summary>The ball: a low arc to the landing point, then Rec Room's grenade blast.</summary>
        private sealed class Ball : MonoBehaviour
        {
            public Vector3 From, To;
            public float Seconds;
            private float _t;

            private void Update()
            {
                _t += Time.deltaTime / Mathf.Max(0.05f, Seconds);
                float k = Mathf.Clamp01(_t);
                Vector3 p = Vector3.Lerp(From, To, k) + Vector3.up * (4f * k * (1f - k) * Mathf.Min(3f, Seconds * 2f));
                transform.position = p;
                if (_t >= 1f)
                {
                    RrFx.Spawn(BlastFx, To, Quaternion.identity, 1.5f);
                    RrSound.Play(BoomClip, To, 1f, 1f, 80f);
                    Destroy(gameObject);
                }
            }
        }
    }
}
