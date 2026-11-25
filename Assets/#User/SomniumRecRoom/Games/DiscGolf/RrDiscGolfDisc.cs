using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.DiscGolf
{
    /// <summary>
    /// One golfer's disc: Rec Room's (DiscGolfDisc) (TOOLS key 9d: 0.30 m across, 2.8 cm thick, mass 0.1, drag 0.1, angular
    /// drag 0.01, the Frisbee physics material; PickupAudio Catch01, ThrowAudio Throw01, CollisionAudio DiscImpact) made a
    /// tool by RrTools and shared as <see cref="RrNetBody"/> prop = its slot (0-3, blue / red / green / yellow, Rec Room's
    /// TeamColorRenderer on SportDisc_Mat's _BaseColor).
    ///
    /// The flight model is DiscGolfDisc's (a Rec Room script, inert at run time; rr_sports.md 5.3 has its fields and our
    /// reading of them, GUESS on the formulas), run by the client that simulates the disc while it flies:
    /// - spin: a release spins it about its own up axis at linearToAngularVelocity(speed) = 45 rad/s * speed / 20;
    /// - lift along the disc's normal while it spins faster than minAngularVelocityForLift 6 rad/s:
    ///   airLiftModifier 0.8 * <see cref="LiftScale"/> * 0.5 * 1.2 * v^2 * 0.0707 m^2 * (0.15 + 2.5 * angle of attack) -
    ///   LiftScale 0.4 is ours, tuned offline (DN/iter15/discgolf.md) so a full 19.96 m/s throw carries about 100 m,
    ///   a 12 m/s one about 30 m; a banked disc's lift turns it (the curve);
    /// - stability: the normal is turned towards world up at angularStabilitySpeed 2 x angularStabilityPercent 0.3 per
    ///   second, and wobble (angular velocity off the spin axis) keeps angularStabilityModifier 0.9 of itself per step;
    /// - Propulsion's fans push it (15 m/s^2 along the fan's axis inside its wind capsule, GUESS from rr_sports 5.4).
    ///
    /// Screen mode (2024-05-14: "your throw will actually go towards the cursor now"): hold the use button to charge,
    /// release to throw at ThrowDiscGolfDiscToolBehaviorConfig's throwSpeedCurve (5 m/s at no charge to 19.96 m/s after
    /// maxChargeDuration 3 s) towards the point under the crosshair, lofted 6 degrees (GUESS), flat with the nose 3 degrees
    /// up. VR: the hand's own release (velocity, the disc's angle in the hand), x1 like Rec Room's tool.
    ///
    /// The room (<see cref="RrDiscGolfRoom"/>) owns the rules; the disc tells it what its local flight did: released,
    /// basket, hazard, bonus panel, came to rest.
    /// </summary>
    public sealed class RrDiscGolfDisc : MonoBehaviour
    {
        public const float Radius = 0.15f;
        public const float MinChargeSpeed = 5f;
        public const float MaxChargeSpeed = 19.96f;
        public const float MaxChargeSeconds = 3f;
        public const float ScreenLoft = 6f;
        public const float ScreenNoseUp = 3f;
        public const float MaxSpin = 45f;
        public const float SpinAtSpeed = 20f;
        public const float MinSpinForLift = 6f;
        public const float AirLiftModifier = 0.8f;
        public const float LiftScale = 0.4f;
        public const float Area = 0.0707f;
        public const float AirDensity = 1.2f;
        public const float StabilityRate = 2f * 0.3f;
        public const float WobbleKeep = 0.9f;
        public const float FanAcceleration = 15f;
        public const float MaxFlightSeconds = 25f;

        public RrDiscGolfRoom Room;
        public int Slot;
        public RrGrabbable Grab;
        public RrNetBody Net;
        public Rigidbody Body;

        /// <summary>Lobby spot (room-local) it goes back to between games.</summary>
        public Vector3 HomeLocal;

        /// <summary>The current throw counts for the local golfer (set by the room as it is released).</summary>
        [System.NonSerialized] public bool Tracked;

        /// <summary>Where the current throw left the hand (world).</summary>
        [System.NonSerialized] public Vector3 ReleasePoint;

        public int Throws;

        private bool _flying;
        private float _releasedAt, _restSince = -1f, _lastContact = -10f, _chargeStart = -1f;
        private Vector3 _prev;
        private MaterialPropertyBlock _block;

        public bool HeldLocally
        {
            get { return Grab != null && Grab.IsHeld && Grab.Holder != null; }
        }

        public bool HeldByAnyone
        {
            get { return HeldLocally || (Net != null && Net.HolderId >= 0); }
        }

        public bool LocallySimulated
        {
            get { return Net == null || Net.LocallyControlled; }
        }

        public bool Flying
        {
            get { return _flying; }
        }

        /// <summary>0..1 while the screen-mode throw charges, -1 otherwise.</summary>
        public float Charge
        {
            get { return _chargeStart < 0f ? -1f : Mathf.Clamp01((Time.time - _chargeStart) / MaxChargeSeconds); }
        }

        public void Setup(RrDiscGolfRoom room, int slot, RrGrabbable grab, RrNetBody net, Vector3 home)
        {
            Room = room;
            Slot = slot;
            Grab = grab;
            Net = net;
            Body = grab != null ? grab.Body : GetComponent<Rigidbody>();
            HomeLocal = home;
            if (Body != null)
            {
                Body.mass = 0.1f;
                Body.linearDamping = 0.1f;
                Body.angularDamping = 0.01f;
                Body.maxAngularVelocity = 60f;
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (grab != null)
            {
                grab.HasMaxThrowSpeed = true;
                grab.MaxThrowSpeed = 25f;
                grab.ThrowMultiplier = 1f;
                // The Q-hold toss (no charge): a 12 m/s throw at the crosshair.
                grab.ScreenThrow = new RrScreenThrow
                {
                    Name = "ThrowDiscGolfDiscToolBehaviorConfig (no charge)",
                    Speed = 12f,
                    AngularSpeed = 0f,
                    TowardsGazeTarget = true,
                    UseCurves = false,
                };
                if (string.IsNullOrEmpty(grab.PickupSound) && !grab.PickupClip.HasClip)
                {
                    grab.PickupSound = "Catch01";
                }

                grab.OnGrabbed += OnGrabbed;
                grab.OnReleased += OnReleased;
                grab.OnUseDown += OnUseDown;
                grab.OnUseUp += OnUseUp;
            }

            Tint(RrTeamsColor(slot));
            _prev = transform.position;
        }

        private static Color RrTeamsColor(int slot)
        {
            return RrTeams.Color(Mathf.Clamp(slot, 0, 3));
        }

        /// <summary>Rec Room's TeamColorRenderer: the slot colour on SportDisc_Mat's _BaseColor (and _Color for older shaders).</summary>
        private void Tint(Color c)
        {
            _block = _block ?? new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                {
                    continue;
                }

                r.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", c);
                _block.SetColor("_Color", c);
                r.SetPropertyBlock(_block);
            }
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

        // ------------------------------------------------------------------ hands

        private void OnGrabbed(RrHand hand)
        {
            _chargeStart = -1f;
            bool wasFlying = _flying;
            _flying = false;
            _restSince = -1f;
            if (Room != null)
            {
                Room.LocalGrabbed(this, wasFlying);
            }

            Tracked = false;
        }

        private void OnReleased(RrHand hand, Vector3 velocity)
        {
            _chargeStart = -1f;
            if (Body == null)
            {
                return;
            }

            bool screen = hand != null && RrGrab.IsScreen(hand);
            Vector3 v = Body.isKinematic ? velocity : Body.linearVelocity;
            if (v.sqrMagnitude < velocity.sqrMagnitude)
            {
                v = velocity;
            }

            float speed = v.magnitude;
            if (screen && speed > 0.5f)
            {
                // Flat, nose a little up along the throw.
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                if (flat.sqrMagnitude < 1e-4f)
                {
                    flat = Quaternion.Euler(0f, RrPlayer.Yaw, 0f) * Vector3.forward;
                }

                flat.Normalize();
                float pitch = Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg + ScreenNoseUp;
                Vector3 right = Vector3.Cross(Vector3.up, flat);
                Quaternion tilt = Quaternion.AngleAxis(-pitch, right);
                Body.rotation = tilt * Quaternion.LookRotation(flat, Vector3.up);
                transform.rotation = Body.rotation;
            }

            if (!Body.isKinematic)
            {
                Vector3 n = transform.up;
                float spin = MaxSpin * Mathf.Clamp01(speed / SpinAtSpeed);
                // Right-handed backhand spin (clockwise seen from above) - GUESS for the screen; VR keeps the hand's sense.
                float sense = 1f;
                if (!screen && hand != null)
                {
                    float s = Vector3.Dot(hand.AngularVelocity, n);
                    sense = s < 0f ? -1f : 1f;
                }

                Body.angularVelocity = n * (spin * sense);
                Body.WakeUp();
            }

            _flying = speed > 0.5f;
            _releasedAt = Time.time;
            _restSince = -1f;
            _prev = transform.position;
            ReleasePoint = transform.position;
            Throws++;
            if (Room != null)
            {
                Room.LocalReleased(this, v, screen);
            }
        }

        private void OnUseDown(RrHand hand)
        {
            if (hand != null && RrGrab.IsScreen(hand))
            {
                _chargeStart = Time.time;
            }
        }

        private void OnUseUp(RrHand hand)
        {
            if (_chargeStart < 0f || Grab == null || !Grab.IsHeld)
            {
                _chargeStart = -1f;
                return;
            }

            float charge = Charge;
            _chargeStart = -1f;
            Vector3 v = ScreenVelocity(charge);
            RrGrab.Release(Grab, v, Vector3.zero);
        }

        /// <summary>The charged screen-mode throw: towards the point under the crosshair, lofted <see cref="ScreenLoft"/> degrees.</summary>
        public Vector3 ScreenVelocity(float charge)
        {
            float speed = Mathf.Lerp(MinChargeSpeed, MaxChargeSpeed, Mathf.Clamp01(charge));
            Ray aim = RrInput.AimRay;
            Vector3 from = Body != null ? Body.worldCenterOfMass : transform.position;
            Vector3 dir = aim.direction.sqrMagnitude > 1e-6f ? aim.direction.normalized : transform.forward;
            RaycastHit hit;
            if (Physics.Raycast(aim, out hit, 250f, (1 << RrLayers.Default) | (1 << RrLayers.Environment), QueryTriggerInteraction.Ignore) && hit.distance > 1f)
            {
                Vector3 d = hit.point - from;
                if (d.sqrMagnitude > 0.25f)
                {
                    dir = d.normalized;
                }
            }

            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-6f)
            {
                flat = Quaternion.Euler(0f, RrPlayer.Yaw, 0f) * Vector3.forward;
            }

            float horizontal = flat.magnitude;
            flat /= horizontal;
            float pitch = Mathf.Clamp(Mathf.Atan2(dir.y, horizontal) * Mathf.Rad2Deg + ScreenLoft, -30f, 60f) * Mathf.Deg2Rad;
            return (flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * speed;
        }

        // ------------------------------------------------------------------ flight (the simulating client)

        private void OnCollisionEnter(Collision c)
        {
            _lastContact = Time.time;
        }

        private void OnCollisionStay(Collision c)
        {
            _lastContact = Time.time;
        }

        private void FixedUpdate()
        {
            if (Body == null || HeldLocally)
            {
                _prev = transform.position;
                return;
            }

            if (Body.isKinematic || !LocallySimulated)
            {
                // RrNetBody handed a resting disc to the instance authority before our own rest test ended: the flight is over.
                if (_flying && Time.time - _releasedAt > 0.3f)
                {
                    Land();
                }

                _prev = transform.position;
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 pos = Body.worldCenterOfMass;
            Vector3 v = Body.linearVelocity;
            float speed = v.magnitude;
            bool airborne = Time.time - _lastContact > 0.08f;

            // Fans push whatever is in their wind (a disc in the air or on the ground).
            if (Room != null)
            {
                Vector3 wind = Room.WindAt(pos);
                if (wind.sqrMagnitude > 0f)
                {
                    Body.AddForce(wind, ForceMode.Acceleration);
                    if (!_flying && speed > 0.5f)
                    {
                        _flying = true;
                        _releasedAt = Time.time;
                    }
                }
            }

            if (_flying && airborne && speed > 1f)
            {
                Vector3 n = transform.up;
                float spin = Vector3.Dot(Body.angularVelocity, n);
                if (Mathf.Abs(spin) > MinSpinForLift)
                {
                    Vector3 dir = v / speed;
                    float alpha = -Mathf.Asin(Mathf.Clamp(Vector3.Dot(dir, n), -1f, 1f));
                    float cl = Mathf.Clamp(0.15f + 2.5f * alpha, -1.2f, 1.2f);
                    Vector3 liftDir = n - dir * Vector3.Dot(n, dir);
                    if (liftDir.sqrMagnitude > 1e-6f)
                    {
                        float lift = AirLiftModifier * LiftScale * 0.5f * AirDensity * speed * speed * Area * cl;
                        Body.AddForce(liftDir.normalized * lift, ForceMode.Force);
                    }
                }

                // Stability: the normal turns towards world up; wobble dies down.
                Vector3 toUp = Vector3.Slerp(n, Vector3.up, Mathf.Clamp01(StabilityRate * dt));
                Body.MoveRotation(Quaternion.FromToRotation(n, toUp) * Body.rotation);
                Vector3 w = Body.angularVelocity;
                Vector3 along = n * Vector3.Dot(w, n);
                Body.angularVelocity = along + (w - along) * WobbleKeep;
            }

            if (_flying)
            {
                if (Room != null && Tracked)
                {
                    Room.LocalFlightStep(this, _prev, pos);
                }

                if (speed < 0.25f && Body.angularVelocity.magnitude < 3f)
                {
                    if (_restSince < 0f)
                    {
                        _restSince = Time.time;
                    }
                    else if (Time.time - _restSince > 0.4f)
                    {
                        Land();
                    }
                }
                else
                {
                    _restSince = -1f;
                }

                if (_flying && Time.time - _releasedAt > MaxFlightSeconds)
                {
                    Land();
                }
            }

            _prev = pos;
        }

        /// <summary>The flight is over (it rests, or the room ended it: basket, hazard).</summary>
        public void Land()
        {
            if (!_flying)
            {
                return;
            }

            _flying = false;
            _restSince = -1f;
            bool tracked = Tracked;
            Tracked = false;
            if (Room != null)
            {
                Room.LocalLanded(this, tracked);
            }
        }

        /// <summary>Ends a flight without telling the room (basket, hazard: the room already knows).</summary>
        public void StopFlight()
        {
            _flying = false;
            _restSince = -1f;
            Tracked = false;
        }

        /// <summary>Back to its lobby spot (the instance authority for a shared disc).</summary>
        public void ReturnHome()
        {
            StopFlight();
            if (Room == null)
            {
                return;
            }

            Vector3 world = Room.transform.TransformPoint(HomeLocal);
            Quaternion rot = Room.transform.rotation;
            if (Net != null)
            {
                Net.ResetTo(world, rot);
            }
            else
            {
                transform.SetPositionAndRotation(world, rot);
                if (Body != null && !Body.isKinematic)
                {
                    Body.linearVelocity = Vector3.zero;
                    Body.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}
