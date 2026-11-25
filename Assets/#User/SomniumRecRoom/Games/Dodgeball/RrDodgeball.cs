using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Dodgeball
{
    /// <summary>
    /// One of the three balls of a dodgeball court: Rec Room's (DodgeballBall) (TOOLS key 1bf; radius 0.11 m,
    /// mass 1, DodgeballBall_phys bounciness 0.6, MaxThrowSpeed 15 m/s, Bounce03 collision audio) made a tool
    /// by RrTools and shared through an <see cref="RrNetBody"/> (local prop id = ball index).
    ///
    /// "Live" is Rec Room's "on fire" ball (rr_sports.md §2.2): a throw by an active player stays live until it
    /// touches anything (floor, wall, another ball, a held ball = a block) or for <see cref="LiveSeconds"/>; only
    /// a live ball puts a player out, and while it is live the ball's own [DodgeballFireTrail] child burns. The
    /// flag, the thrower's team and PlayerId ride in the prop's kind int (11) so every client draws the fire and
    /// the catcher knows whose throw it caught; the throw id (prop int 9) keys the hit/catch arbitration.
    ///
    /// Hits are thrower-authoritative: the client that simulates the flying ball (the thrower holds the prop's
    /// authority) sweeps it against the layer-27 hit volumes every physics step and claims the hit to the
    /// instance authority (<see cref="RrDodgeballRoom"/>), which waits the 150 ms catch window.
    ///
    /// Screen mode (Rec Room's ThrowDodgeballToolBehaviorConfig, as reworked on 2021-04-15): left mouse down winds
    /// up and holds the ball, release throws at 14 m/s however long it was held ("Charging your throw will now have no
    /// impact on the dodgeball's launch speed"), with 7 degrees of gravity compensation and a soft aim toward the
    /// nearest opponent within 9 m and 30 degrees of the crosshair. Q held (RrGrab's toss) uses the same 14 m/s
    /// config straight at the crosshair. VR throws are hand velocity (x1, clamped to 15 m/s).
    ///
    /// Catching: taking a live opponent throw in hand (VR grab, or the screen catch assist) plays Catch01, puts the
    /// fire out and makes the ball dead; under the final rules that is all a catch does.
    /// </summary>
    public sealed class RrDodgeball : MonoBehaviour
    {
        public const float Radius = 0.11f;
        public const float LiveSeconds = 4f;
        public const float SweepRadius = 0.13f;

        /// <summary>A release slower than this is a drop, never a live throw.</summary>
        public const float MinLiveSpeed = 2f;

        public int Index;
        public RrDodgeballRoom Room;
        public RrGrabbable Grab;
        public RrNetBody Net;
        public Rigidbody Body;
        public Vector3 HomeLocal;

        /// <summary>Counters for the log and the harness.</summary>
        public int LiveThrows, PlayerHits, ScreenThrows;

        private static readonly RaycastHit[] Buffer = new RaycastHit[16];

        private GameObject _fire;
        private TrailRenderer[] _trails;
        private Vector3 _prev;
        private bool _hadPrev;
        private float _liveUntil;
        private float _releasedAt = -10f;
        private float _chargeStart = -1f;
        private bool _shownLive;
        private Vector3 _lastPos;
        private float _restSince = -1f;

        /// <summary>Speed estimated from the drawn position (works for puppets too).</summary>
        public float SeenSpeed { get; private set; }

        /// <summary>Seconds the ball has been still (&lt; 0.2 m/s), -1 while moving or held.</summary>
        public float RestSeconds
        {
            get { return _restSince < 0f ? -1f : Time.time - _restSince; }
        }

        public static int Pack(int team, int throwerId)
        {
            return 1 | ((team & 3) << 1) | (((throwerId + 1) & 0xFFFFF) << 3);
        }

        public bool Live
        {
            get { return Net != null && (Net.KindInt & 1) != 0; }
        }

        public int ThrowerTeam
        {
            get { return Net != null ? (Net.KindInt >> 1) & 3 : -1; }
        }

        public int ThrowerId
        {
            get { return Net != null ? ((Net.KindInt >> 3) & 0xFFFFF) - 1 : -1; }
        }

        public bool HeldLocally
        {
            get { return Grab != null && Grab.IsHeld && Grab.Holder != null; }
        }

        public bool HeldByAnyone
        {
            get { return HeldLocally || (Net != null && Net.HolderId >= 0); }
        }

        public void Setup(RrDodgeballRoom room, RrInstance inst, int index, RrGrabbable grab, RrNetBody net)
        {
            Room = room;
            Index = index;
            Grab = grab;
            Net = net;
            Body = grab != null ? grab.Body : GetComponent<Rigidbody>();
            HomeLocal = room.transform.InverseTransformPoint(transform.position);
            if (inst != null)
            {
                var fire = inst.Find("[DodgeballFireTrail]");
                _fire = fire != null ? fire.gameObject : null;
            }

            if (_fire != null)
            {
                _fire.SetActive(false);
            }

            _trails = GetComponentsInChildren<TrailRenderer>(true);
            SetTrails(false);
            if (grab != null)
            {
                grab.ScreenThrow = new RrScreenThrow
                {
                    Name = "ThrowDodgeballToolBehaviorConfig",
                    Speed = RrDodgeballRoom.ScreenThrowSpeed,
                    AngularSpeed = 360f,
                    TowardsGazeTarget = true,
                    UseCurves = false,
                };
                grab.HasMaxThrowSpeed = true;
                grab.MaxThrowSpeed = RrDodgeballRoom.MaxThrowSpeed;
                grab.OnGrabbed += OnGrabbed;
                grab.OnReleased += OnReleased;
                grab.OnUseDown += OnUseDown;
                grab.OnUseUp += OnUseUp;
            }

            _lastPos = transform.position;
        }

        private void OnDestroy()
        {
            if (Grab != null)
            {
                Grab.OnGrabbed -= OnGrabbed;
                Grab.OnReleased -= OnReleased;
                Grab.OnUseDown -= OnUseDown;
                Grab.OnUseUp -= OnUseUp;
            }
        }

        // ------------------------------------------------------------------ local hands

        private void OnGrabbed(RrHand hand)
        {
            _chargeStart = -1f;
            if (Room != null)
            {
                Room.OnLocalGrab(this);
            }

            Kill();
        }

        private void OnReleased(RrHand hand, Vector3 velocity)
        {
            _chargeStart = -1f;
            _hadPrev = false;
            if (Room != null && velocity.magnitude >= MinLiveSpeed && Room.CanThrowLive())
            {
                MarkLive(Room.LocalTeam, RrNet.LocalId);
            }
        }

        /// <summary>The local player just threw it: live for the thrower's team (also the harness hook).</summary>
        public void MarkLive(int team, int throwerId)
        {
            if (Net != null)
            {
                Net.SetKindInt(Pack(team, throwerId));
            }

            _releasedAt = Time.time;
            _liveUntil = Time.time + LiveSeconds;
            _hadPrev = false;
            LiveThrows++;
            if (LiveThrows == 1)
            {
                RrLog.Info("Dodgeball", "ball " + Index + ": first live throw (team " + team + ", thrower " + throwerId + ", throw id " + (Net != null ? Net.ThrowId : 0) + ")");
            }
        }

        public void Kill()
        {
            if (Net != null && (Net.KindInt & 1) != 0)
            {
                Net.SetKindInt(0);
            }
        }

        private void OnUseDown(RrHand hand)
        {
            if (!RrPlayer.InVr)
            {
                _chargeStart = Time.time;
            }
        }

        private void OnUseUp(RrHand hand)
        {
            if (_chargeStart < 0f || Grab == null || !Grab.IsHeld || Room == null)
            {
                _chargeStart = -1f;
                return;
            }

            float held = Time.time - _chargeStart;
            _chargeStart = -1f;
            Vector3 spin;
            Vector3 v = Room.ScreenThrowVelocity(this, out spin);
            ScreenThrows++;
            if (ScreenThrows == 1)
            {
                RrLog.Info("Dodgeball", "ball " + Index + ": first screen throw at " + v.magnitude.ToString("F1") + " m/s after holding " + held.ToString("F2") + " s (the hold never changes the speed)");
            }

            RrGrab.Release(Grab, v, spin);
        }

        // ------------------------------------------------------------------ flight (the simulating client)

        private void FixedUpdate()
        {
            if (Body == null || Net == null || !Live || !Net.LocallyControlled || Grab == null || Grab.IsHeld)
            {
                _hadPrev = false;
                return;
            }

            if (Time.time > _liveUntil && _liveUntil > 0f)
            {
                Kill();
                return;
            }

            Vector3 pos = Body.worldCenterOfMass;
            if (_hadPrev)
            {
                Sweep(_prev, pos);
            }

            _prev = pos;
            _hadPrev = true;
        }

        private void Sweep(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            Vector3 dir = dist > 1e-4f ? d / dist : Vector3.forward;
            int n = Physics.SphereCastNonAlloc(from, SweepRadius, dir, Buffer, Mathf.Max(dist, 0.01f), RrHitVolume.Mask, QueryTriggerInteraction.Collide);
            int thrower = ThrowerId;
            int team = ThrowerTeam;
            float best = float.MaxValue;
            int bestId = -1;
            Vector3 bestPoint = to;
            for (int i = 0; i < n; i++)
            {
                int id;
                RrBodyPart part;
                if (!RrHitVolumes.Resolve(Buffer[i].collider, out id, out part) || id == thrower)
                {
                    continue;
                }

                if (Room == null || !Room.IsValidTarget(id, team))
                {
                    continue;
                }

                if (Buffer[i].distance < best)
                {
                    best = Buffer[i].distance;
                    bestId = id;
                    bestPoint = Buffer[i].point == Vector3.zero ? to : Buffer[i].point;
                }
            }

            if (bestId < 0)
            {
                return;
            }

            PlayerHits++;
            Room.OnBallHitPlayer(this, bestId, bestPoint);
            Kill();
            // Bounce off the body (the hit volumes are triggers the ball would otherwise fly through).
            Body.linearVelocity = -Body.linearVelocity * 0.25f + Vector3.up * 1.5f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Live || Net == null || !Net.LocallyControlled || Time.time - _releasedAt < 0.05f)
            {
                return;
            }

            // The VR hand boxes (layer 7) brushing the ball right after release do not end a throw.
            if (collision.collider != null && collision.collider.gameObject.layer == 7)
            {
                return;
            }

            Kill();
        }

        // ------------------------------------------------------------------ visuals (every client)

        private void Update()
        {
            Vector3 p = transform.position;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            SeenSpeed = Mathf.Lerp(SeenSpeed, (p - _lastPos).magnitude / dt, 0.5f);
            _lastPos = p;
            bool held = HeldByAnyone;
            if (!held && SeenSpeed < 0.2f)
            {
                if (_restSince < 0f)
                {
                    _restSince = Time.time;
                }
            }
            else
            {
                _restSince = -1f;
            }

            bool live = Live && !held;
            if (live != _shownLive)
            {
                _shownLive = live;
                if (_fire != null)
                {
                    _fire.SetActive(live);
                    if (live)
                    {
                        foreach (var ps in _fire.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            ps.Play(true);
                        }
                    }
                }
            }

            SetTrails(!held && (live || SeenSpeed > 4f));
        }

        private void SetTrails(bool on)
        {
            if (_trails == null)
            {
                return;
            }

            foreach (var t in _trails)
            {
                if (t != null && t.emitting != on)
                {
                    t.emitting = on;
                }
            }
        }
    }
}
