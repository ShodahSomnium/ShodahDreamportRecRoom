using System;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Rec Room's Basketball script ((Basketball): mass 2, drag 0.1, continuous collision, a 0.248 m ball with
    /// bounciness 0.8). Bounce and rim sounds come from its CollisionAudio (<see cref="RrCollisionSound"/>); a ball
    /// without one bounces with BasketballBounce01 (v0.79, full from 0.25 m/s) and BasketballHitRim01 (v0.15).
    /// Passing a hoop's rim downward (<see cref="RrHoop"/>) is a basket: PassedRimAudio (DingDing v0.7) and
    /// PassedRimParticle ([VFX_Basketball_PassedRim]) play at the rim and <see cref="OnScored"/> fires for games.
    /// Rim and backboard contacts are reported too. The FireTrail (a "hot streak" flame, GUESS: after
    /// <see cref="StreakForFire"/> baskets in a row from the same throws) burns until a throw misses.
    /// </summary>
    public sealed class RrBall : MonoBehaviour
    {
        public const string BounceClip = "BasketballBounce01";
        public const string RimClip = "BasketballHitRim01";

        public RrClipInfo PassedRimAudio;
        public RrTarget PassedRimFx;
        public string PassedRimFxName = "[VFX_Basketball_PassedRim]";
        public ParticleSystem FireTrail;
        public int StreakForFire = 3;

        /// <summary>A ball scored in a hoop (ball, hoop).</summary>
        public static event Action<RrBall, RrHoop> OnScored;

        /// <summary>A ball hit a hoop's rim (ball, hoop, impact speed m/s).</summary>
        public static event Action<RrBall, RrHoop, float> OnRimHit;

        /// <summary>A ball hit a hoop's backboard or bracket (ball, hoop, impact speed m/s).</summary>
        public static event Action<RrBall, RrHoop, float> OnBackboardHit;

        /// <summary>Any bounce (ball, impact speed m/s), for games that count bounces.</summary>
        public static event Action<RrBall, float> OnBounce;

        public int Scores { get; private set; }
        public int Streak { get; private set; }
        public int Bounces { get; private set; }
        public int RimHits { get; private set; }
        public int BoardHits { get; private set; }

        /// <summary>Who threw it last: the local player's id when a local hand released it, -1 unknown.</summary>
        public int LastThrowerId { get; private set; } = -1;

        private RrGrabbable _grabbable;
        private Rigidbody _body;
        private RrCollisionSound _sound;
        private RrHoop _entering;
        private bool _scoredThisThrow;
        private bool _thrown;
        private float _lastBounceSound = -10f;
        private float _armedAt;

        public void Setup(RrInstance inst, RrConfig c)
        {
            PassedRimAudio = c.Clip("PassedRimAudio");
            PassedRimFx = c.Ref("PassedRimParticle");
            var trail = RrTools.NodeOf(inst, c, "FireTrail");
            FireTrail = trail != null ? trail.GetComponent<ParticleSystem>() : null;
            SetFire(false);
        }

        private void Awake()
        {
            _grabbable = GetComponent<RrGrabbable>();
            _body = GetComponent<Rigidbody>();
            _armedAt = Time.time + 0.5f;
        }

        private void OnEnable()
        {
            if (_grabbable == null)
            {
                _grabbable = GetComponent<RrGrabbable>();
            }

            if (_grabbable != null)
            {
                _grabbable.OnReleased += Released;
            }
        }

        private void OnDisable()
        {
            if (_grabbable != null)
            {
                _grabbable.OnReleased -= Released;
            }
        }

        private void Start()
        {
            _sound = GetComponent<RrCollisionSound>();
        }

        private void Released(RrHand hand, Vector3 velocity)
        {
            if (_thrown && !_scoredThisThrow && Streak > 0)
            {
                Streak = 0;
                SetFire(false);
            }

            _thrown = true;
            _scoredThisThrow = false;
            _entering = null;
            LastThrowerId = RrPlayer.LocalId;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider == null || Time.time < _armedAt)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            var hoop = RrHoop.Of(collision.collider);
            bool rim = hoop != null && hoop.IsRim(collision.collider);
            if (hoop != null)
            {
                if (rim)
                {
                    RimHits++;
                    if (OnRimHit != null)
                    {
                        OnRimHit(this, hoop, speed);
                    }
                }
                else
                {
                    BoardHits++;
                    if (OnBackboardHit != null)
                    {
                        OnBackboardHit(this, hoop, speed);
                    }
                }
            }

            if (speed > 0.25f)
            {
                Bounces++;
                if (OnBounce != null)
                {
                    OnBounce(this, speed);
                }
            }

            if (_sound == null && speed > 0.05f && Time.time - _lastBounceSound > 0.05f && collision.collider.gameObject.layer != 7)
            {
                _lastBounceSound = Time.time;
                RrSound.Play(rim ? RimClip : BounceClip, transform.position, rim ? 0.15f : 0.79f * Mathf.Clamp01(speed / 0.25f), 1f, 100f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var hoop = RrHoop.Of(other);
            if (hoop == null || other != hoop.ScoreTrigger || _body == null)
            {
                return;
            }

            if (_body.linearVelocity.y < 0f && transform.position.y > other.transform.position.y)
            {
                _entering = hoop;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var hoop = RrHoop.Of(other);
            if (hoop == null || hoop != _entering || other != hoop.ScoreTrigger)
            {
                return;
            }

            _entering = null;
            if (transform.position.y < other.transform.position.y)
            {
                Score(hoop);
            }
        }

        /// <summary>A basket through this hoop (also the test hook).</summary>
        public void Score(RrHoop hoop)
        {
            Scores++;
            Streak++;
            _scoredThisThrow = true;
            Vector3 at = hoop != null ? hoop.RimCenter : transform.position;
            RrTools.Play(PassedRimAudio, at, 1f, 60f);
            if (!PassedRimFx.IsNull)
            {
                RrTools.SpawnFx(PassedRimFx, PassedRimFxName, at, Quaternion.identity);
            }

            if (Streak >= StreakForFire)
            {
                SetFire(true);
            }

            RrTools.Once("Ball", "score", "first basket: " + name + " (streak " + Streak + ")");
            if (hoop != null)
            {
                hoop.NotifyScored(this);
            }

            if (OnScored != null)
            {
                OnScored(this, hoop);
            }
        }

        private void SetFire(bool on)
        {
            if (FireTrail == null)
            {
                return;
            }

            if (on)
            {
                FireTrail.gameObject.SetActive(true);
                FireTrail.Play(true);
            }
            else
            {
                FireTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        internal static void ClearStatics()
        {
            OnScored = null;
            OnRimHit = null;
            OnBackboardHit = null;
            OnBounce = null;
        }
    }
}
