using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paddleball
{
    /// <summary>
    /// The paddleball: Rec Room's (TennisBall) (TOOLS key 787, radius 0.07 m) scaled to ~0.1 m, with gravity off
    /// and no drag - Rec Room's paddleball "floats" (rr_sports.md §3.2). Its own ball lives in the encrypted level,
    /// so the flight model is ours: the tube is an axis-aligned box, so the simulating client reflects the ball off
    /// its walls perfectly (restitution 1, no friction) and keeps it at the rally speed, with at least a quarter
    /// of that speed along the tube so a rally never stalls sideways. Physics contacts are off
    /// (detectCollisions = false): only the paddles (<see cref="RrPaddle"/>) and the walls move it, never a hand or a body.
    ///
    /// Shared through an <see cref="RrNetBody"/> (local prop id 4): the last paddle to touch it takes its
    /// authority (LocalTouch, 0.25 s cooldown) and streams it; that client also detects the goal planes at
    /// the court's ends (<see cref="RrPaddleballRoom.HalfLength"/>: +-6.95 in level13, +-6 in the stand-in; walls, floor and
    /// roof likewise from the built court) and claims the point to the instance authority. Every client plays the wall sounds from the ball's
    /// drawn motion, so puppets sound the same as the simulation.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public sealed class RrPaddleballBall : MonoBehaviour
    {
        public float Radius = 0.1f;
        public RrPaddleballRoom Room;
        public RrNetBody Net;
        public Rigidbody Body;

        /// <summary>Speed the simulating client holds the ball at (m/s); 0 at rest.</summary>
        public float RallySpeed;

        public int WallBounces;

        private Vector3 _lastPos;
        private Vector3 _est;
        private Vector3 _prevEst;
        private int _claimedRally = -1;
        private float _soundAt;

        /// <summary>Velocity: the simulated one on the controlling client, else estimated from the drawn motion.</summary>
        public Vector3 Velocity
        {
            get { return Net != null && Net.LocallyControlled && Body != null ? Body.linearVelocity : _est; }
        }

        public void Setup(RrPaddleballRoom room, RrNetBody net, Rigidbody body, float radius)
        {
            Room = room;
            Net = net;
            Body = body;
            Radius = radius;
            Body.useGravity = false;
            Body.isKinematic = false;
            Body.linearDamping = 0f;
            Body.angularDamping = 0.5f;
            Body.detectCollisions = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _lastPos = transform.position;
        }

        /// <summary>This client hits the ball: take the simulation and send it off.</summary>
        public void Hit(Vector3 worldPos, Vector3 velocity)
        {
            if (Net != null)
            {
                Net.LocalTouch();
            }

            transform.position = worldPos;
            Body.position = worldPos;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Random.onUnitSphere * 8f;
            RallySpeed = velocity.magnitude;
        }

        /// <summary>Authority: parks the ball (serve point, lobby).</summary>
        public void Park(Vector3 worldPos)
        {
            RallySpeed = 0f;
            if (Net != null)
            {
                Net.ResetTo(worldPos, Quaternion.identity);
            }
            else
            {
                transform.position = worldPos;
                Body.linearVelocity = Vector3.zero;
            }
        }

        private void FixedUpdate()
        {
            if (Room == null || Body == null || Net == null || !Net.LocallyControlled)
            {
                return;
            }

            Vector3 v = Room.transform.InverseTransformDirection(Body.linearVelocity);
            float speed = v.magnitude;
            if (speed < 0.05f)
            {
                return;
            }

            if (RallySpeed <= 0f)
            {
                RallySpeed = speed;
            }

            speed = RallySpeed;
            v = v / v.magnitude * speed;

            // Never drift sideways for ever: at least a quarter of the speed along the tube.
            float minZ = speed * 0.25f;
            if (Mathf.Abs(v.z) < minZ)
            {
                float sign = v.z != 0f ? Mathf.Sign(v.z) : -Mathf.Sign(Room.transform.InverseTransformPoint(Body.position).z + 0.001f);
                Vector3 side = new Vector3(v.x, v.y, 0f);
                float sideSpeed = Mathf.Sqrt(Mathf.Max(0f, speed * speed - minZ * minZ));
                side = side.sqrMagnitude > 1e-6f ? side.normalized * sideSpeed : Vector3.zero;
                v = new Vector3(side.x, side.y, sign * minZ);
            }

            Vector3 p = Room.transform.InverseTransformPoint(Body.position);
            float r = Radius;
            float hx = Room.HalfWidth - r;
            float lo = Room.Floor + r, hi = Room.Top - r;
            if ((p.x > hx && v.x > 0f) || (p.x < -hx && v.x < 0f))
            {
                v.x = -v.x;
                p.x = Mathf.Clamp(p.x, -hx, hx);
            }

            if ((p.y < lo && v.y < 0f) || (p.y > hi && v.y > 0f))
            {
                v.y = -v.y;
                p.y = Mathf.Clamp(p.y, lo, hi);
            }

            float end = Room.HalfLength - r;
            if ((p.z > end && v.z > 0f) || (p.z < -end && v.z < 0f))
            {
                int scorer = 1 - Room.TeamAtEnd(Mathf.Sign(p.z)); // the ball reached the back of the other team's area
                if (Room.GoalsLive)
                {
                    int rally = Room.Rally;
                    if (_claimedRally != rally)
                    {
                        _claimedRally = rally;
                        Room.ClaimGoal(scorer, rally, Room.transform.TransformPoint(new Vector3(p.x, p.y, Mathf.Sign(p.z) * end)));
                    }

                    p.z = Mathf.Sign(p.z) * end;
                    Body.position = Room.transform.TransformPoint(p);
                    Body.linearVelocity = Vector3.zero;
                    RallySpeed = 0f;
                    return;
                }

                // Lobby / practice between points: the back walls are just walls.
                v.z = -v.z;
                p.z = Mathf.Clamp(p.z, -end, end);
            }

            Body.position = Room.transform.TransformPoint(p);
            Body.linearVelocity = Room.transform.TransformDirection(v);
        }

        private void LateUpdate()
        {
            Vector3 pos = transform.position;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            _prevEst = _est;
            _est = (pos - _lastPos) / dt;
            _lastPos = pos;

            // Wall sounds from the drawn motion: a velocity component flipping near a wall.
            if (Room == null || Time.time < _soundAt)
            {
                return;
            }

            Vector3 p = Room.transform.InverseTransformPoint(pos);
            Vector3 a = Room.transform.InverseTransformDirection(_prevEst);
            Vector3 b = Room.transform.InverseTransformDirection(_est);
            bool wallX = Mathf.Abs(p.x) > Room.HalfWidth - Radius - 0.15f && a.x * b.x < 0f && Mathf.Abs(a.x) > 0.8f;
            bool wallY = (p.y < Room.Floor + Radius + 0.15f || p.y > Room.Top - Radius - 0.15f) && a.y * b.y < 0f && Mathf.Abs(a.y) > 0.8f;
            if (wallX || wallY)
            {
                WallBounces++;
                _soundAt = Time.time + 0.08f;
                float loud = Mathf.Clamp01(a.magnitude / RrPaddleballRoom.MaxSpeed);
                RrSound.Play("TennisBall_Impact01a", pos, 0.35f + 0.5f * loud, 1f + Random.Range(-0.15f, 0.15f), 25f);
            }
        }
    }
}
