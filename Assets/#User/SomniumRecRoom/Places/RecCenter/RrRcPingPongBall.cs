using UnityEngine;
using SomniumSpace.Worlds.SomniumRecRoom.Net;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// The ping-pong ball's paddle hit (Rec Room's `Paddle`/`Ball` logic is not in the bundles): when a paddle the
    /// local player holds touches it, the ball takes its pre-hit velocity reflected about the contact normal
    /// (x0.85) plus the paddle's velocity, at least 1 m/s away from the paddle, at most 14 m/s. A shared ball
    /// (RrNetBody) is claimed with LocalTouch first, so the hitter simulates the return. Table and paddle clicks
    /// come from the ball's own CollisionAudio (tags set by the room).
    /// </summary>
    internal sealed class RrRcPingPongBall : MonoBehaviour
    {
        public const float MaxSpeed = 14f;

        private Rigidbody _body;
        private RrNetBody _net;
        private Vector3 _lastVelocity;

        public int Hits { get; private set; }

        private void Start()
        {
            _body = GetComponent<Rigidbody>();
            _net = GetComponent<RrNetBody>();
            if (_body != null)
            {
                _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        private void FixedUpdate()
        {
            if (_body != null)
            {
                _lastVelocity = _body.linearVelocity;
            }
        }

        private void OnCollisionEnter(Collision c)
        {
            var paddle = c.collider != null ? c.collider.GetComponentInParent<RrRcPaddle>() : null;
            if (paddle == null || !paddle.HeldLocally || _body == null)
            {
                return;
            }

            if (_net != null)
            {
                _net.LocalTouch();
            }

            var point = c.contactCount > 0 ? c.GetContact(0).point : c.collider.ClosestPoint(transform.position);
            var n = transform.position - point;
            if (n.sqrMagnitude < 1e-8f)
            {
                n = paddle.Velocity;
            }

            if (n.sqrMagnitude < 1e-8f)
            {
                return;
            }

            n.Normalize();
            var pv = paddle.Velocity;
            var v = Vector3.Reflect(_lastVelocity - pv, n) * 0.85f + pv;
            float away = Vector3.Dot(v, n);
            if (away < 1f)
            {
                v += n * (1f - away);
            }

            if (v.magnitude > MaxSpeed)
            {
                v = v.normalized * MaxSpeed;
            }

            _body.linearVelocity = v;
            Hits++;
        }
    }
}
