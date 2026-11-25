using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// A thrown paint grenade or paint mine in the local player's hand (Rec Room's Grenade / ProximityMine tools;
    /// the shared kit knows guns and melee tools, not throwables). Numbers from the configs (rr_shooters.md 3.1):
    /// - (Grenade): the trigger arms it (Paintball_GrenadeClick01 and the fuse loop), it bursts 3 s later
    ///   (explosionTimeout) wherever it is. A grenade thrown without arming arms as it leaves the hand (Somnium's
    ///   screen players have no trigger-while-holding habit);
    /// - (ProximityMine): thrown, it sticks where it lands (SimpleSticky), arms after 1.5 s, and bursts when an
    ///   opponent walks into its 6 x 3 x 6 m trip volume; it expires after 90 s.
    /// The burst is <see cref="RrPaintballBoom.Explode"/> on the thrower's client. After a throw the pistol comes
    /// back to the hand (the level's pickups replace the held tool, as the racks do).
    /// </summary>
    public sealed class RrPaintballThrowable : MonoBehaviour
    {
        public const float Fuse = 3f;
        public const float MineArm = 1.5f;
        public const float MineLife = 90f;

        public bool Mine;
        private RrGrabbable _grab;
        private float _burstAt = -1f;
        private bool _thrown;
        private bool _stuck;
        private float _armedAt = -1f;
        private float _expireAt = -1f;
        private float _nextCheck;
        private AudioSource _fuse;

        public static RrPaintballThrowable Attach(Component held, bool mine)
        {
            if (held == null)
            {
                return null;
            }

            var t = held.gameObject.GetComponent<RrPaintballThrowable>();
            if (t == null)
            {
                t = held.gameObject.AddComponent<RrPaintballThrowable>();
            }

            t.Mine = mine;
            t._grab = held.GetComponent<RrGrabbable>();
            if (t._grab != null)
            {
                t._grab.OnUseDown += t.OnUse;
                t._grab.OnReleased += t.OnReleased;
            }

            return t;
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
            if (!Mine)
            {
                Arm();
            }
        }

        private void Arm()
        {
            if (_burstAt > 0f)
            {
                return;
            }

            _burstAt = Time.time + Fuse;
            RrSound.Play("Paintball_GrenadeClick01", transform.position, 0.8f);
            _fuse = RrSound.Loop("Paintball_GrenadeFuseLoop01", transform, 0.6f, 20f);
            RrImpacts.Fx("(VFX_GrenadeArmed)", transform.position, transform.rotation, 1f, null);
        }

        private void OnReleased(RrHand hand, Vector3 velocity)
        {
            if (_thrown)
            {
                return;
            }

            _thrown = true;
            transform.SetParent(RrPaintballRoom.Current != null ? RrPaintballRoom.Current.transform : null, true);
            RrLoadout.Given.Remove(gameObject);
            if (Mine)
            {
                RrSound.Play("sfx_paintball_paintmine_throw01", transform.position, 0.7f);
                _expireAt = Time.time + MineLife;
            }
            else
            {
                Arm();
                RrSound.Play("Paintball_GrenadeRelease01", transform.position, 0.7f);
            }

            RrPaintballRoom.Current?.GivePistolSoon();
        }

        private void OnCollisionEnter(Collision c)
        {
            if (!Mine || !_thrown || _stuck)
            {
                return;
            }

            _stuck = true;
            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            if (c.contactCount > 0)
            {
                var ct = c.GetContact(0);
                transform.position = ct.point + ct.normal * 0.03f;
                transform.rotation = Quaternion.FromToRotation(transform.up, ct.normal) * transform.rotation;
            }

            _armedAt = Time.time + MineArm;
            RrSound.Play("sfx_paintball_paintmine_impactandattach01", transform.position, 0.7f);
        }

        private void Update()
        {
            var room = RrPaintballRoom.Current;
            if (room == null || room.Phase != RrPhase.Playing)
            {
                // The round ended with it in the air or on the ground: it goes with the round.
                if (_thrown)
                {
                    Destroy(gameObject);
                }

                return;
            }

            if (!Mine)
            {
                if (_burstAt > 0f && Time.time >= _burstAt)
                {
                    RrPaintballBoom.Explode(transform.position, RrPaintballBoom.GrenadeTool);
                    if (_grab != null && _grab.IsHeld)
                    {
                        RrGrab.Drop(_grab.Holder);
                        RrPaintballRoom.Current?.GivePistolSoon();
                    }

                    Destroy(gameObject);
                }

                return;
            }

            if (!_thrown)
            {
                return;
            }

            if (_expireAt > 0f && Time.time >= _expireAt)
            {
                Destroy(gameObject);
                return;
            }

            if (!_stuck && _grab != null && _grab.Body != null && !_grab.Body.isKinematic && _grab.Body.linearVelocity.sqrMagnitude < 0.01f && Time.time > _expireAt - MineLife + 1f)
            {
                // Landed without a collision callback (resting contact): stick here.
                _stuck = true;
                _grab.Body.isKinematic = true;
                _armedAt = Time.time + MineArm;
            }

            if (!_stuck || _armedAt < 0f || Time.time < _armedAt || Time.time < _nextCheck)
            {
                return;
            }

            if (_armedAt > 0f && Time.time - _armedAt < 0.05f)
            {
                RrSound.Play("sfx_paintball_paintmine_arm01", transform.position, 0.6f);
            }

            _nextCheck = Time.time + 0.15f;
            if (Tripped(room))
            {
                RrSound.Play("sfx_paintball_paintmine_trip01", transform.position, 0.8f);
                RrPaintballBoom.Explode(transform.position + transform.up * 0.2f, RrPaintballBoom.MineTool);
                Destroy(gameObject);
            }
        }

        /// <summary>An opponent inside the trip volume (6 x 3 x 6 m, centre 1.5 m above the mine).</summary>
        private bool Tripped(RrPaintballRoom room)
        {
            int myTeam = RrTeams.LocalTeam;
            Vector3 c = transform.position + Vector3.up * 1.5f;
            foreach (int id in RrNet.MembersOfMyInstance())
            {
                if (id == RrNet.LocalId || RrCombat.TeamOf(id) == myTeam)
                {
                    continue;
                }

                var r = RrRemotePlayers.Get(id);
                if (r == null || r.Root == null)
                {
                    continue;
                }

                Vector3 d = r.Root.position + Vector3.up * 0.9f - c;
                if (Mathf.Abs(d.x) <= 3f && Mathf.Abs(d.z) <= 3f && Mathf.Abs(d.y) <= 1.5f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Harness: burst now (the fuse and the trip check skipped).</summary>
        internal void TestBurst()
        {
            RrPaintballBoom.Explode(transform.position, Mine ? RrPaintballBoom.MineTool : RrPaintballBoom.GrenadeTool);
            Destroy(gameObject);
        }
    }
}
