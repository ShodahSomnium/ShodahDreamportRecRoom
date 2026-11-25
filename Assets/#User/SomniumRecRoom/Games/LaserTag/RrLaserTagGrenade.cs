using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.LaserTag
{
    /// <summary>
    /// Laser Tag's grenade ((Arena_Grenade), rr_shooters.md 3.2: 100 splash damage, radius 6 m, fuse 3 s once armed) in the
    /// local player's hand. The trigger arms it; a grenade thrown unarmed arms as it leaves the hand. It bursts where it is
    /// when the fuse runs out, held or not.
    ///
    /// The 2019-01 rules (rr_latest_rgames_action.md 2.1): no friendly fire in Laser Tag, but a grenade can hit its own
    /// thrower. The burst is decided on the thrower's client: opponents in the radius get a Hit (full damage to 0.71 of the
    /// radius, then down to 0.2 at the edge, the paint grenade's falloff from the same Grenade config), teammates nothing,
    /// the thrower the same falloff on their own health (an out that way is a self-destruct, -10 points). Everyone sees
    /// the blast (<see cref="RrLaserTagRoom.EvBlast"/>).
    /// </summary>
    public sealed class RrLaserTagGrenade : MonoBehaviour
    {
        public const float Fuse = 3f;
        public const float Radius = 6f;
        public const int Damage = 100;
        public const float FullDamageFraction = 0.71f;
        public const string BlastFx = "(Arena_Grenade_Explosion_VFX)";

        /// <summary>GUESS: the Arena grenade's own WeaponAudio clips are not documented; the paint grenade's are used.</summary>
        public const string ArmClip = "Paintball_GrenadeClick01", FuseClip = "Paintball_GrenadeFuseLoop01", ThrowClip = "Paintball_GrenadeRelease01",
                            BlastClip = "Paintball_GrenadeExplode01";

        private static readonly Collider[] Overlaps = new Collider[64];

        private RrLaserTagRoom _room;
        private RrGrabbable _grab;
        private float _burstAt = -1f;
        private bool _thrown;
        private AudioSource _fuse;

        public static RrLaserTagGrenade Attach(Component held, RrLaserTagRoom room)
        {
            if (held == null)
            {
                return null;
            }

            var g = held.gameObject.GetComponent<RrLaserTagGrenade>();
            if (g == null)
            {
                g = held.gameObject.AddComponent<RrLaserTagGrenade>();
            }

            g._room = room;
            g._grab = held.GetComponent<RrGrabbable>();
            if (g._grab != null)
            {
                g._grab.OnUseDown += g.OnUse;
                g._grab.OnReleased += g.OnReleased;
            }

            return g;
        }

        private void OnDestroy()
        {
            if (_grab != null)
            {
                _grab.OnUseDown -= OnUse;
                _grab.OnReleased -= OnReleased;
            }

            if (_fuse != null)
            {
                RrSound.Stop(_fuse);
            }
        }

        private void OnUse(RrHand hand)
        {
            Arm();
        }

        private void Arm()
        {
            if (_burstAt > 0f)
            {
                return;
            }

            _burstAt = Time.time + Fuse;
            RrSound.Play(ArmClip, transform.position, 0.8f);
            _fuse = RrSound.Loop(FuseClip, transform, 0.6f, 20f);
        }

        private void OnReleased(RrHand hand, Vector3 velocity)
        {
            if (_thrown)
            {
                return;
            }

            _thrown = true;
            transform.SetParent(_room != null ? _room.transform : null, true);
            RrLoadout.Given.Remove(gameObject);
            Arm();
            RrSound.Play(ThrowClip, transform.position, 0.7f);
            if (_room != null)
            {
                _room.GivePistolSoon();
            }
        }

        private void Update()
        {
            if (_room == null || _room.Phase != RrPhase.Playing)
            {
                // The round ended with it in the air: it goes with the round.
                if (_thrown)
                {
                    Destroy(gameObject);
                }

                return;
            }

            if (_burstAt > 0f && Time.time >= _burstAt)
            {
                Explode(_room, transform.position);
                if (_grab != null && _grab.IsHeld)
                {
                    RrGrab.Drop(_grab.Holder);
                    _room.GivePistolSoon();
                }

                Destroy(gameObject);
            }
        }

        /// <summary>The thrower's client: hits the opponents in the radius, and the thrower, and tells the instance.</summary>
        public static int Explode(RrLaserTagRoom room, Vector3 point)
        {
            Show(point);
            if (room != null)
            {
                Vector3 local = room.transform.InverseTransformPoint(point);
                var b = new byte[6];
                Put(b, 0, local.x);
                Put(b, 2, local.y);
                Put(b, 4, local.z);
                RrNet.Send(RrLaserTagRoom.EvBlast, b);
            }

            RrCombat.SyncPhysics();
            int n = Physics.OverlapSphereNonAlloc(point, Radius, Overlaps, RrHitVolume.Mask, QueryTriggerInteraction.Collide);
            var done = new HashSet<int>();
            int myTeam = RrTeams.LocalTeam;
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                RrHitVolume v;
                if (!RrHitVolume.TryGet(Overlaps[i], out v) || v.Enemy != null || v.IsLocalPlayer || v.PlayerId < 0 || v.PlayerId == RrNet.LocalId)
                {
                    continue;
                }

                int team = RrCombat.TeamOf(v.PlayerId);
                if (team >= 0 && team == myTeam)
                {
                    continue;
                }

                if (!done.Add(v.PlayerId))
                {
                    continue;
                }

                float d = Vector3.Distance(point, Overlaps[i].ClosestPoint(point));
                RrCombat.SendHit(v.PlayerId, Falloff(d), RrBodyPart.Body, RrCombat.NextSeq(), point, RrLaserTagRoom.GrenadeTool);
                hits++;
            }

            // The thrower: the distance to the body (feet to head).
            var h = RrHealth.Local;
            bool self = false;
            if (h != null && h.InCombat && !h.IsOut)
            {
                Vector3 feet = RrPlayer.FeetPosition + Vector3.up * 0.2f;
                Vector3 head = RrPlayer.HeadPosition;
                Vector3 seg = head - feet;
                float k = seg.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(point - feet, seg) / seg.sqrMagnitude) : 0f;
                float d = Vector3.Distance(point, feet + seg * k);
                if (d <= Radius)
                {
                    self = h.ApplyDamage(Falloff(d), RrNet.LocalId, RrBodyPart.Body, point, RrLaserTagRoom.GrenadeTool) > 0;
                }
            }

            RrLog.Info("LaserTag", "grenade burst at " + (room != null ? room.transform.InverseTransformPoint(point).ToString("F1") : point.ToString("F1")) + ": " + hits +
                                   " opponent(s) hit" + (self ? ", and the thrower" : ""));
            return hits;
        }

        private static int Falloff(float d)
        {
            float full = Radius * FullDamageFraction;
            float k = d <= full ? 1f : Mathf.Lerp(1f, 0.2f, (d - full) / Mathf.Max(0.01f, Radius - full));
            return Mathf.Max(1, Mathf.RoundToInt(Damage * k));
        }

        /// <summary>Every client: the blast's burst and sound.</summary>
        public static void Show(Vector3 point)
        {
            RrImpacts.Fx(BlastFx, point, Quaternion.identity, 1f, null);
            RrSound.Play(BlastClip, point, 0.9f);
        }

        /// <summary>A remote blast (EvBlast): room-local position in decimetres.</summary>
        public static void Receive(RrLaserTagRoom room, byte[] body)
        {
            if (room == null || body == null || body.Length < 6)
            {
                return;
            }

            var local = new Vector3(Get(body, 0), Get(body, 2), Get(body, 4));
            Show(room.transform.TransformPoint(local));
        }

        private static void Put(byte[] b, int i, float v)
        {
            int c = Mathf.Clamp(Mathf.RoundToInt(v * 10f), short.MinValue, short.MaxValue);
            b[i] = (byte)(c & 0xff);
            b[i + 1] = (byte)((c >> 8) & 0xff);
        }

        private static float Get(byte[] b, int i)
        {
            return (short)(b[i] | (b[i + 1] << 8)) * 0.1f;
        }
    }
}
