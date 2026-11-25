using UnityEngine;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// A ping-pong paddle (`(Pingpong_Paddle)`): Rec Room's `Paddle` script is inert here, so the paddle only
    /// tracks its own velocity for <see cref="RrRcPingPongBall"/> to add to the bounce.
    /// </summary>
    internal sealed class RrRcPaddle : MonoBehaviour
    {
        private RrGrabbable _grab;
        private Vector3 _last;
        private bool _has;

        public Vector3 Velocity { get; private set; }

        public bool HeldLocally
        {
            get { return _grab != null && _grab.IsHeld; }
        }

        private void Start()
        {
            _grab = GetComponent<RrGrabbable>();
        }

        private void FixedUpdate()
        {
            var p = transform.position;
            if (_has && Time.fixedDeltaTime > 0f)
            {
                Velocity = Vector3.Lerp(Velocity, (p - _last) / Time.fixedDeltaTime, 0.6f);
            }

            _last = p;
            _has = true;
        }
    }
}
