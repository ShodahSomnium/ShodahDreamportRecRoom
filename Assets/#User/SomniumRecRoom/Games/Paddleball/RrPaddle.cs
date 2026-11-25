using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paddleball
{
    /// <summary>
    /// A paddleball paddle: Rec Room's (Paddleball_Paddle) (TOOLS key 2d: LockOnPickup, mass 10, face collider
    /// 0.24 x 0.28 x 0.02 m under a 1.25-scaled "Colliders" node, RacketHit01 collision audio, an
    /// [VFX_Impact_Particles] child on the net) made a tool by RrTools, held solid (<see cref="RrGrabbable.SolidWhileHeld"/>)
    /// and shared through an <see cref="RrNetBody"/> (local prop ids 0-3 = team * 2 + seat).
    ///
    /// Rec Room drives its paddle through a spring (SpringForce 50000) so the physics engine does the hit; a held
    /// paddle here is a kinematic child of the hand, so the hit is ours and runs on the holder's client only (the
    /// touch-steal owner of the ball, rr_sports.md §9.2): each frame the ball's centre is taken into the face
    /// box's frame now and one frame ago; if it touched or crossed the face inside its outline while closing in,
    /// the ball leaves with v_out = reflect(v_ball - v_face, n) + v_face (the face's own speed from its motion),
    /// and <see cref="RrPaddleballRoom.PaddleHit"/> applies the rally rules (5 % faster per hit, 14 m/s cap).
    ///
    /// Screen mode (Rec Room's PaddleToolBehaviorConfig: click plays primarySwing at PlaySpeed 4): a click swings
    /// the paddle for 0.25 s, and a ball within 2.4 m in front of the player during the swing is returned along
    /// the crosshair.
    /// </summary>
    [DefaultExecutionOrder(20010)]
    public sealed class RrPaddle : MonoBehaviour
    {
        public const float SwingSeconds = 0.25f;
        public const float ScreenReach = 2.4f;
        public const float HitCooldown = 0.15f;

        public int Slot;
        public RrPaddleballRoom Room;
        public RrGrabbable Grab;
        public RrNetBody Net;
        public BoxCollider Face;
        public Vector3 RestLocal;
        public Quaternion RestRotation = Quaternion.identity;
        public int Hits;

        private Transform _pivot;
        private Quaternion _pivotRest = Quaternion.identity;
        private ParticleSystem[] _impact;
        private Vector3 _prevC, _prevN, _prevBall;
        private bool _has;
        private float _cooldown;
        private float _swingStart = -10f;
        private bool _swingHit;

        public int Team
        {
            get { return Slot / 2; }
        }

        public bool HeldLocally
        {
            get { return Grab != null && Grab.IsHeld && Grab.Holder != null; }
        }

        public bool HeldByAnyone
        {
            get { return HeldLocally || (Net != null && Net.HolderId >= 0); }
        }

        public void Setup(RrPaddleballRoom room, RrInstance inst, int slot, RrGrabbable grab, RrNetBody net)
        {
            Room = room;
            Slot = slot;
            Grab = grab;
            Net = net;
            float best = 0f;
            foreach (var box in GetComponentsInChildren<BoxCollider>(true))
            {
                Vector3 s = Vector3.Scale(box.size, box.transform.lossyScale);
                float area = Mathf.Abs(s.x * s.y);
                if (area > best && !box.isTrigger)
                {
                    best = area;
                    Face = box;
                }
            }

            if (inst != null)
            {
                _pivot = inst.Find("Pivot");
                var fx = inst.Find("[VFX_Impact_Particles]");
                if (fx != null)
                {
                    _impact = fx.GetComponentsInChildren<ParticleSystem>(true);
                    fx.gameObject.SetActive(true);
                    foreach (var ps in _impact)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            if (_pivot != null)
            {
                _pivotRest = _pivot.localRotation;
            }

            if (grab != null)
            {
                grab.SolidWhileHeld = true;
                grab.LockOnPickup = true;
                grab.OnUseDown += OnUseDown;
            }

            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                RrRefs.SetTag(c, RrRefs.Paddle);
            }
        }

        private void OnDestroy()
        {
            if (Grab != null)
            {
                Grab.OnUseDown -= OnUseDown;
            }
        }

        private void OnUseDown(RrHand hand)
        {
            if (!RrPlayer.InVr)
            {
                _swingStart = Time.time;
                _swingHit = false;
            }
        }

        /// <summary>The hit feedback on the holder's paddle (Rec Room's net particles, haptics).</summary>
        public void PlayImpact(float strength)
        {
            Hits++;
            if (_impact != null)
            {
                foreach (var ps in _impact)
                {
                    ps.Play(true);
                }
            }

            if (Grab != null && Grab.Holder != null)
            {
                Grab.Holder.Haptic(Mathf.Lerp(0.3f, 0.9f, strength), 0.06f);
            }
        }

        private void LateUpdate()
        {
            Swing();
            var ball = Room != null ? Room.Ball : null;
            if (!HeldLocally || ball == null || Face == null)
            {
                _has = false;
                return;
            }

            Transform ft = Face.transform;
            Vector3 c = ft.TransformPoint(Face.center);
            Vector3 n = ft.forward;
            Vector3 ax = ft.right;
            Vector3 ay = ft.up;
            Vector3 ls = ft.lossyScale;
            float hx = Mathf.Abs(Face.size.x * ls.x) * 0.5f;
            float hy = Mathf.Abs(Face.size.y * ls.y) * 0.5f;
            float hz = Mathf.Abs(Face.size.z * ls.z) * 0.5f;
            Vector3 b = ball.transform.position;
            float r = ball.Radius;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);

            if (_has && Time.time >= _cooldown)
            {
                Vector3 rel = b - c;
                float s = Vector3.Dot(rel, n);
                float s0 = Vector3.Dot(_prevBall - _prevC, _prevN);
                float u = Vector3.Dot(rel, ax);
                float w = Vector3.Dot(rel, ay);
                bool inside = Mathf.Abs(u) <= hx + r && Mathf.Abs(w) <= hy + r;
                bool touching = Mathf.Abs(s) <= hz + r;
                bool crossed = s0 * s < 0f && Mathf.Abs(s0) < 0.6f;
                if (inside && (touching || crossed))
                {
                    float side = Mathf.Abs(s0) > 1e-4f ? Mathf.Sign(s0) : Mathf.Sign(s + 1e-5f);
                    Vector3 nOut = n * side;
                    Vector3 vFace = (c - _prevC) / dt;
                    Vector3 vRel = ball.Velocity - vFace;
                    if (Vector3.Dot(vRel, nOut) < 0f)
                    {
                        Vector3 vOut = Vector3.Reflect(vRel, nOut) + vFace;
                        Vector3 pos = c + ax * Mathf.Clamp(u, -hx, hx) + ay * Mathf.Clamp(w, -hy, hy) + nOut * (hz + r + 0.01f);
                        _cooldown = Time.time + HitCooldown;
                        Room.PaddleHit(this, pos, vOut);
                    }
                }
            }

            _prevC = c;
            _prevN = n;
            _prevBall = b;
            _has = true;

            ScreenSwingHit(ball);
        }

        private void ScreenSwingHit(RrPaddleballBall ball)
        {
            if (RrPlayer.InVr || _swingHit || Time.time - _swingStart > SwingSeconds + 0.05f || Time.time < _cooldown)
            {
                return;
            }

            Vector3 head = RrPlayer.HeadPosition;
            Vector3 to = ball.transform.position - head;
            Ray aim = RrInput.AimRay;
            if (to.magnitude > ScreenReach || Vector3.Angle(aim.direction, to) > 75f)
            {
                return;
            }

            _swingHit = true;
            _cooldown = Time.time + HitCooldown;
            Vector3 v = aim.direction * (ball.Velocity.magnitude + RrPaddleballRoom.MinHitSpeed);
            Room.PaddleHit(this, ball.transform.position, v);
        }

        private void Swing()
        {
            if (_pivot == null)
            {
                return;
            }

            float t = (Time.time - _swingStart) / SwingSeconds;
            float angle = t >= 0f && t < 1f ? -65f * Mathf.Sin(t * Mathf.PI) : 0f;
            _pivot.localRotation = _pivotRest * Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
