using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Bowling
{
    /// <summary>
    /// One bowling ball: Rec Room's (BowlingBall) (TOOLS key a5a: 0.25 m, mass 8, BowlingBall physics 0.14 / 0 / bounce 0.25,
    /// MaxThrowSpeed 15, throwSpeedMultiplier 1.1, PickupAudio Bowling_Pickup01) made a tool by RrTools. The scoring lane's
    /// balls are <see cref="RrNetBody"/> props (ids 0-3); the practice lanes' balls are local only (research §9.5).
    ///
    /// It waits on the rack kinematic (the level's shelf has no collider we can trust), so a release makes it dynamic and
    /// applies the throw: Rec Room's BowlingBall halves the upward release speed (YUpVelocityScalar 0.5) and spins with the
    /// wrist (AngularVelocityThrowMultiplier 1.5); a screen release rolls it flat along the view at 8 m/s at least
    /// (ThrowBowlingBallToolBehaviorConfig, simplified). Sounds on every client from the drawn motion: the rolling rumble
    /// (Bowling_BallRoll02, volume by speed^2 up to 6 - only while it rolls, the 2025 fix), the gutter, the backboard and a
    /// hard drop on the lane (Bowling_MegaThud01).
    /// </summary>
    public sealed class RrBowlingBall : MonoBehaviour
    {
        public const float Radius = 0.125f;
        public const float ScreenRollSpeed = 8f;

        public RrBowlingRoom Room;
        public RrBowlingLane Lane;
        public int Index;
        public RrGrabbable Grab;
        public RrNetBody Net;
        public Rigidbody Body;

        /// <summary>Where the ball goes back to (room-local): the level's rack spot first, then the ball return.</summary>
        public Vector3 HomeLocal;

        public int Throws;

        /// <summary>Authority / owner bookkeeping for the ball return (seconds it has been ready to go back, -1 not).</summary>
        [System.NonSerialized] public float ReturnSince = -1f;

        private AudioSource _rumble;
        private Vector3 _lastPos, _est, _prevEst;
        private bool _gutterPlayed, _backPlayed;
        private float _soundAt;

        public bool HeldLocally
        {
            get { return Grab != null && Grab.IsHeld && Grab.Holder != null; }
        }

        public bool HeldByAnyone
        {
            get { return HeldLocally || (Net != null && Net.HolderId >= 0); }
        }

        /// <summary>This client simulates the ball (always for a practice ball).</summary>
        public bool LocallySimulated
        {
            get { return Net == null || Net.LocallyControlled; }
        }

        /// <summary>Velocity from the simulation when this client runs it, else from the drawn motion.</summary>
        public Vector3 SeenVelocity
        {
            get { return LocallySimulated && Body != null && !Body.isKinematic ? Body.linearVelocity : _est; }
        }

        public void Setup(RrBowlingRoom room, RrBowlingLane lane, int index, RrGrabbable grab, RrNetBody net, Vector3 home)
        {
            Room = room;
            Lane = lane;
            Index = index;
            Grab = grab;
            Net = net;
            Body = grab != null ? grab.Body : GetComponent<Rigidbody>();
            HomeLocal = home;
            if (Body != null)
            {
                Body.mass = 8f;
                Body.angularDamping = 0f;
                Body.maxAngularVelocity = 30f;
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Body.isKinematic = true;
            }

            if (grab != null)
            {
                grab.HasMaxThrowSpeed = true;
                grab.MaxThrowSpeed = 15f;
                grab.ThrowMultiplier = 1.1f;
                grab.ScreenThrow = new RrScreenThrow
                {
                    Name = "ThrowBowlingBallToolBehaviorConfig",
                    Speed = ScreenRollSpeed,
                    AngularSpeed = 0f,
                    TowardsGazeTarget = false,
                    UseCurves = false,
                };
                grab.OnGrabbed += OnGrabbed;
                grab.OnReleased += OnReleased;
            }

            _rumble = RrSound.Loop("Bowling_BallRoll02", transform, 0f, 25f);
            _lastPos = transform.position;
        }

        private void OnDestroy()
        {
            if (Grab != null)
            {
                Grab.OnGrabbed -= OnGrabbed;
                Grab.OnReleased -= OnReleased;
            }

            RrSound.Stop(_rumble);
        }

        private void OnGrabbed(RrHand hand)
        {
            _gutterPlayed = false;
            _backPlayed = false;
            ReturnSince = -1f;
        }

        private void OnReleased(RrHand hand, Vector3 velocity)
        {
            if (Body == null)
            {
                return;
            }

            Vector3 v = velocity;
            if (hand != null && RrGrab.IsScreen(hand) && v.magnitude > 1f)
            {
                // Screen bowling: roll it flat along the view, a little down onto the lane.
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                if (flat.sqrMagnitude < 1e-4f)
                {
                    flat = Quaternion.Euler(0f, RrPlayer.Yaw, 0f) * Vector3.forward;
                }

                v = flat.normalized * Mathf.Max(v.magnitude, ScreenRollSpeed) + Vector3.down * 0.5f;
            }
            else if (v.y > 0f)
            {
                v.y *= 0.5f;
            }

            Body.isKinematic = false;
            Body.linearVelocity = Vector3.ClampMagnitude(v, 15f);
            Body.angularVelocity = (hand != null ? hand.AngularVelocity : Vector3.zero) * 1.5f;
            Body.WakeUp();
            Throws++;
            if (Throws == 1)
            {
                RrLog.Info("Bowling", "ball " + Index + " (lane " + (Lane != null ? Lane.Index : -1) + "): first throw at " + v.magnitude.ToString("F1") + " m/s (" + (hand != null && RrGrab.IsScreen(hand) ? "screen" : "VR") + ")");
            }
        }

        /// <summary>Back on its rack spot (the owner of a practice ball, the instance authority for a shared one).</summary>
        public void ReturnHome()
        {
            ReturnSince = -1f;
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
            }

            if (Body != null)
            {
                if (!Body.isKinematic)
                {
                    Body.linearVelocity = Vector3.zero;
                    Body.angularVelocity = Vector3.zero;
                }

                Body.isKinematic = true;
            }

            _gutterPlayed = false;
            _backPlayed = false;
        }

        /// <summary>Whether it sits on its rack spot.</summary>
        public bool AtHome
        {
            get { return Room != null && (Room.transform.InverseTransformPoint(transform.position) - HomeLocal).sqrMagnitude < 0.01f; }
        }

        // ------------------------------------------------------------------ sounds (every client)

        private void LateUpdate()
        {
            Vector3 pos = transform.position;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            _prevEst = _est;
            _est = (pos - _lastPos) / dt;
            _lastPos = pos;
            if (Room == null || Lane == null)
            {
                return;
            }

            Vector3 local = Room.transform.InverseTransformPoint(pos);
            Vector3 lv = Room.transform.InverseTransformDirection(_est);
            bool onLane = !HeldByAnyone && local.y < 0.45f && local.z < Lane.FoulZ + 4.5f;
            float speed = new Vector2(lv.x, lv.z).magnitude;
            if (_rumble != null)
            {
                float want = onLane && speed < 30f ? Mathf.Clamp01(speed * speed / 6f) : 0f;
                _rumble.volume = Mathf.MoveTowards(_rumble.volume, want * RrSound.Volume, dt * 4f);
            }

            if (!onLane || speed > 30f)
            {
                return;
            }

            float dx = Mathf.Abs(local.x - Lane.X);
            if (!_gutterPlayed && dx > RrBowlingLane.LaneHalfWidth && dx < RrBowlingLane.LaneHalfWidth + 0.35f && local.z < Lane.FoulZ - 0.5f && speed > 0.5f)
            {
                _gutterPlayed = true;
                RrSound.Play("Bowling_Gutter01", pos, 0.8f, 1f, 30f);
            }

            if (!_backPlayed && local.z < Lane.BackZ + 0.45f)
            {
                _backPlayed = true;
                RrSound.Play("Bowling_BackboardThud01", pos, 0.9f, 1f, 30f);
            }

            float fall = _prevEst.y;
            if (Time.time >= _soundAt && fall < -2f && _est.y > -0.3f && local.y < 0.35f)
            {
                _soundAt = Time.time + 0.3f;
                RrSound.Play(fall < -3.5f ? "Bowling_MegaThud01" : "Bowling_Thud01", pos, Mathf.Clamp01(-fall / 5f), 1f, 30f);
            }
        }
    }
}
