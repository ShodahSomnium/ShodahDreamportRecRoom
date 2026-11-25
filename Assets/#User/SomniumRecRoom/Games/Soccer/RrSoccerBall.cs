using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Soccer
{
    /// <summary>
    /// The soccer ball: Rec Room's (SoccerBall) (0.715 m radius, mass 0.75, bounciness 0.6, angular drag 0.15, MaxThrowSpeed
    /// 35), never grabbed. Rec Room kicks it with a "locomotion attack" (SoccerBall min/maxLocomotionAttackForce 20): walking
    /// or teleporting into it, and hand hits. Here every client kicks it with its own player only:
    ///
    /// - body: the local body (a 0.3 m capsule from the feet to the head) touching the ball while moving toward it sends it
    ///   off along (ball - body) flattened, 0.3 up, at 8-20 m/s by the approach speed (GUESS: the bundle's 20 read as the
    ///   top speed); a ball that rolls into a standing player bounces off (0.6);
    /// - teleport: the feet jumping more than 1.2 m in a frame and landing on the ball - the full 20 m/s along the jump;
    /// - VR hands: a hand faster than 1.5 m/s inside the ball's reach - hand velocity x 1.5 (4-35 m/s);
    /// - screen: the use button within 1.6 m of the ball - 16 m/s along the view (Rec Room's screen players kick by walking;
    ///   the click is our help for mouse players).
    ///
    /// A kick takes the prop's simulation (<see cref="RrNetBody.LocalTouch"/>, 0.25 s cooldown) and broadcasts the kick for
    /// the sound. The simulating client checks the goal zones every physics step and claims goals to the room. Floor and
    /// wall sounds come from the drawn motion on every client (Soccer_BallHitsFloor_01, Soccer_BallHitsEnvironment_01).
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public sealed class RrSoccerBall : MonoBehaviour
    {
        public const float KickMin = 8f;
        public const float KickMax = 20f;
        public const float ClickKick = 16f;
        public const float HandFactor = 1.5f;
        public const float MaxSpeed = 35f;
        public const float BodyRadius = 0.3f;
        public const float KickCooldown = 0.25f;

        public RrSoccerRoom Room;
        public RrNetBody Net;
        public Rigidbody Body;
        public float Radius = RrSoccerRoom.BallRadius;

        /// <summary>Local kicks (log and harness).</summary>
        public int Kicks;

        private Vector3 _lastFeet;
        private bool _hasFeet;
        private float _kickAt;
        private int _claimedKickoff = -1;
        private Vector3 _lastPos, _est, _prevEst;
        private float _soundAt;

        /// <summary>Velocity: the simulated one on the controlling client, else estimated from the drawn motion.</summary>
        public Vector3 Velocity
        {
            get { return Net != null && Net.LocallyControlled && Body != null ? Body.linearVelocity : _est; }
        }

        public void Setup(RrSoccerRoom room, RrNetBody net, Rigidbody body, float radius)
        {
            Room = room;
            Net = net;
            Body = body;
            Radius = radius > 0.1f ? radius : RrSoccerRoom.BallRadius;
            _lastPos = transform.position;
        }

        /// <summary>Authority: puts the ball at a spot at rest (it falls from there: the kick-off drop).</summary>
        public void Park(Vector3 worldPos)
        {
            if (Net != null)
            {
                Net.ResetTo(worldPos, Quaternion.identity);
            }
            else
            {
                transform.position = worldPos;
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------ local kicks

        private void Update()
        {
            if (Room == null || Body == null || !Room.Built)
            {
                return;
            }

            Vector3 feet = RrPlayer.FeetPosition;
            Vector3 head = RrPlayer.HeadPosition;
            Vector3 move = _hasFeet ? feet - _lastFeet : Vector3.zero;
            _lastFeet = feet;
            _hasFeet = true;
            if (Time.time < _kickAt)
            {
                return;
            }

            bool jumped = move.magnitude > 1.2f;
            Vector3 vel = jumped ? Vector3.zero : move / Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 c = transform.position;

            // The body: a capsule from just above the feet to just under the head.
            Vector3 a = feet + Vector3.up * 0.25f;
            Vector3 b = head - Vector3.up * 0.15f;
            if (b.y < a.y)
            {
                b = a;
            }

            Vector3 p = Closest(a, b, c);
            Vector3 n = c - p;
            float d = n.magnitude;
            if (d < Radius + BodyRadius)
            {
                Vector3 flat = new Vector3(n.x, 0f, n.z);
                if (flat.sqrMagnitude < 1e-4f)
                {
                    flat = Quaternion.Euler(0f, RrPlayer.Yaw, 0f) * Vector3.forward;
                }

                flat.Normalize();
                if (jumped)
                {
                    Vector3 along = new Vector3(move.x, 0f, move.z).normalized;
                    Kick((flat + along).normalized + Vector3.up * 0.3f, KickMax, "teleport");
                    return;
                }

                Vector3 bv = Velocity;
                float approach = Vector3.Dot(vel - bv, flat);
                if (approach > 0.4f)
                {
                    float speed = Mathf.Lerp(KickMin, KickMax, Mathf.Clamp01((approach - 0.4f) / 4f));
                    Kick(flat + Vector3.up * 0.3f, speed, "body");
                    return;
                }

                // The ball runs into a standing player: it bounces off (bounciness 0.6).
                float into = Vector3.Dot(bv, -flat);
                if (into > 1f)
                {
                    Block(bv + flat * into * 1.6f);
                    return;
                }
            }

            if (RrPlayer.InVr)
            {
                if (HandKick(RrHand.Left, c) || HandKick(RrHand.Right, c))
                {
                    return;
                }
            }
            else
            {
                var hand = RrHand.Right;
                if (hand != null && hand.TriggerDown && d < Radius + 1.6f)
                {
                    Vector3 f = RrPlayer.HeadRotation * Vector3.forward;
                    f = new Vector3(f.x, 0f, f.z);
                    if (f.sqrMagnitude < 1e-4f)
                    {
                        f = new Vector3(c.x - feet.x, 0f, c.z - feet.z);
                    }

                    Kick(f.normalized + Vector3.up * 0.35f, ClickKick, "click");
                }
            }
        }

        private bool HandKick(RrHand hand, Vector3 c)
        {
            if (hand == null || !hand.Tracked || hand.Velocity.magnitude < 1.5f)
            {
                return false;
            }

            Vector3 hn = c - hand.Position;
            if (hn.magnitude > Radius + 0.1f || Vector3.Dot(hand.Velocity, hn) <= 0f)
            {
                return false;
            }

            float speed = Mathf.Clamp(hand.Velocity.magnitude * HandFactor, 4f, MaxSpeed);
            Kick(hand.Velocity, speed, "hand");
            hand.Haptic(0.6f, 0.08f);
            return true;
        }

        /// <summary>The local player sends the ball off: take its simulation, set the velocity, tell everyone.</summary>
        public void Kick(Vector3 direction, float speed, string how)
        {
            _kickAt = Time.time + KickCooldown;
            if (Net != null)
            {
                Net.LocalTouch();
            }

            speed = Mathf.Min(speed, MaxSpeed);
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            Body.WakeUp();
            Body.linearVelocity = dir * speed;
            Body.angularVelocity = Vector3.Cross(Vector3.up, dir) * (speed / Mathf.Max(0.1f, Radius));
            Kicks++;
            if (Room != null)
            {
                Room.LocalKick(speed, how);
            }
        }

        private void Block(Vector3 velocity)
        {
            _kickAt = Time.time + KickCooldown;
            if (Net != null)
            {
                Net.LocalTouch();
            }

            Body.WakeUp();
            Body.linearVelocity = Vector3.ClampMagnitude(velocity, MaxSpeed);
        }

        private static Vector3 Closest(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            if (len < 1e-6f)
            {
                return a;
            }

            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
            return a + ab * t;
        }

        // ------------------------------------------------------------------ the simulating client

        private void FixedUpdate()
        {
            if (Room == null || Body == null || Net == null || !Net.LocallyControlled)
            {
                return;
            }

            if (Body.linearVelocity.sqrMagnitude > MaxSpeed * MaxSpeed)
            {
                Body.linearVelocity = Body.linearVelocity.normalized * MaxSpeed;
            }

            if (!Room.GoalsLive)
            {
                return;
            }

            Vector3 local = Room.transform.InverseTransformPoint(Body.position);
            for (int g = 0; g < RrSoccerRoom.Goals.Length; g++)
            {
                if (!RrSoccerRoom.Goals[g].Contains(local))
                {
                    continue;
                }

                int kickoff = Room.Kickoff;
                if (_claimedKickoff != kickoff)
                {
                    _claimedKickoff = kickoff;
                    // The ball is in the goal team g defends: a point for the other team.
                    Room.ClaimGoal(1 - g, kickoff, Body.position);
                }

                return;
            }
        }

        // ------------------------------------------------------------------ sounds (every client)

        private void LateUpdate()
        {
            Vector3 pos = transform.position;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            _prevEst = _est;
            _est = (pos - _lastPos) / dt;
            _lastPos = pos;
            if (Room == null || Time.time < _soundAt || _est.magnitude > 60f)
            {
                return;
            }

            // Floor: the fall turns into a rise near the ground. Walls: a fast horizontal component flips.
            Vector3 up = Room.transform.up;
            float fall = Vector3.Dot(_prevEst, up);
            float rise = Vector3.Dot(_est, up);
            float height = Room.transform.InverseTransformPoint(pos).y;
            if (fall < -1.5f && rise > 0.2f && height < Radius + 0.5f)
            {
                _soundAt = Time.time + 0.1f;
                RrSound.Play("Soccer_BallHitsFloor_01", pos, Mathf.Clamp01(0.3f + -fall / 12f), 1f + Random.Range(-0.13f, 0.13f), 50f);
                return;
            }

            Vector3 a = Room.transform.InverseTransformDirection(_prevEst);
            Vector3 b = Room.transform.InverseTransformDirection(_est);
            bool wall = (a.x * b.x < 0f && Mathf.Abs(a.x) > 2.5f) || (a.z * b.z < 0f && Mathf.Abs(a.z) > 2.5f);
            if (wall)
            {
                _soundAt = Time.time + 0.1f;
                RrSound.Play("Soccer_BallHitsEnvironment_01", pos, Mathf.Clamp01(0.35f + a.magnitude / 20f), 1f + Random.Range(-0.1f, 0.1f), 50f);
            }
        }
    }
}
