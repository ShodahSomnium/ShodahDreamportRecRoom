using System;
using SomniumSpace.Bridge;
using SomniumSpace.Bridge.Player;
using SomniumSpace.Bridge.Player.Components;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.RecRally
{
    /// <summary>
    /// The local player in the driver's seat of their buggy - the seated pattern the Euro Truck Simulator Dreamport proved
    /// (somnium_player.md 6.2-6.3, ETS EtsTruckSeat): the client's locomotion is frozen (speeds, jump, gravity 0, teleport
    /// and fly off, no player collision, no falling animation, no VR leg IK), the body root is written onto the seat every
    /// LateUpdate with the buggy's heading, and the buggy's colliders ignore the player's capsule. Desktop: the avatar
    /// sits (PoseType.SittingStraight, re-applied every frame - the client drops poses on its cancel key) and its head is
    /// put on the driver's eye; the client's own camera follows it. VR: no pose (Rec Room's buggy has a standing back
    /// seat too); the rig is moved so the headset lands on the driver's eye, keeping the player's own lean (0.45 m).
    /// Getting out restores everything and teleports the player (podium, lobby). The Somnium avatar itself is never
    /// changed. The seat holds the player through the countdown and the race (Rec Rally's seat lock, 2021-10-06).
    /// </summary>
    public sealed class RrRecRallySeat : MonoBehaviour
    {
        private const float LeanLimit = 0.45f;

        public RrRecRallyKart Kart;
        public bool Seated { get; private set; }

        private Transform _root;
        private Transform _rig;
        private ISomniumPlayerMotion _motion;
        private ISomniumPlayerAnimation _animation;
        private ISomniumPlayerPoses _poses;
        private ISomniumPlayerAvatar _avatar;
        private CharacterController _capsule;
        private bool _poseFailed;
        private bool _restKnown;
        private Vector3 _headRest;
        private float _yawWas;

        /// <summary>Into the driver's seat of <paramref name="kart"/>.</summary>
        public void Sit(RrRecRallyKart kart)
        {
            if (Seated)
            {
                Stand();
            }

            Kart = kart;
            _root = RrPlayer.Root;
            if (_root == null || kart == null)
            {
                RrLog.Warn("RecRally", "no player body to seat (" + (_root == null ? "no body yet" : "no buggy") + ")");
                return;
            }

            var player = RrPlayer.Local;
            try
            {
                var features = player != null ? player.Features : null;
                _motion = features != null ? features.Motion : null;
                _animation = features != null ? features.Animation : null;
                _poses = features != null ? features.Poses : null;
                _avatar = features != null ? features.Avatar : null;
            }
            catch (Exception e)
            {
                RrLog.Warn("RecRally", "seat: the client's player features threw (" + e.Message + ")");
            }

            _capsule = _root.GetComponent<CharacterController>() ?? _root.GetComponentInChildren<CharacterController>() ?? _root.GetComponentInParent<CharacterController>();
            if (_capsule != null)
            {
                foreach (var c in kart.GetComponents<Collider>())
                {
                    Physics.IgnoreCollision(c, _capsule, true);
                }
            }

            try
            {
                if (_motion != null)
                {
                    _motion.SetCollisionDisableState(true);
                    _motion.SetFlyModeDisableState(true);
                    _motion.SetTeleportationDisableState(true);
                    _motion.SetMovementSpeed(0f, 0f, 0f);
                    _motion.SetGravity(0f, 0f);
                    _motion.SetJump(0f, 0f);
                }
                else
                {
                    RrPlayer.SetMovement(0f, false);
                }

                if (_animation != null)
                {
                    _animation.SetFallingAnimationDisabled(true);
                }

                if (_avatar != null)
                {
                    _avatar.SetVRIKLegMovementDisabled(true);
                }
            }
            catch (Exception e)
            {
                RrLog.Error("RecRally", "seat: freezing the player failed", e);
            }

            Seated = true;
            _restKnown = false;
            _rig = null;
            _yawWas = kart.transform.eulerAngles.y;
            RrLog.Info("RecRally", "seated in buggy " + kart.Slot + " (" + (RrPlayer.InVr ? "VR" : "screen") + (_capsule != null ? ", capsule kept off the buggy" : ", no capsule found") + ")");
        }

        /// <summary>Out of the seat: pose off, the client's locomotion back, then to <paramref name="to"/> (world feet and yaw).</summary>
        public void Stand(Vector3? to = null, float yaw = 0f, bool move = true)
        {
            if (!Seated)
            {
                if (to.HasValue && move)
                {
                    RrPlayer.Teleport(to.Value, yaw);
                }

                return;
            }

            Seated = false;
            try
            {
                if (_root != null)
                {
                    _root.rotation = Quaternion.Euler(0f, _root.eulerAngles.y, 0f);
                }

                if (_poses != null && !_poseFailed && !RrPlayer.InVr)
                {
                    _poses.SetPose(PoseType.None);
                }

                if (_avatar != null)
                {
                    _avatar.SetVRIKLegMovementDisabled(false);
                }

                if (_motion != null)
                {
                    _motion.SetCollisionDisableState(false);
                    _motion.SetFlyModeDisableState(false);
                    _motion.SetTeleportationDisableState(false);
                    _motion.SetMovementSpeed(1f, 1f, 1f);
                    _motion.SetGravity(1f, 1f);
                    _motion.SetJump(1f, 1f);
                }

                RrPlayer.SetMovement(1f, true);
                if (_animation != null)
                {
                    _animation.SetFallingAnimationDisabled(false);
                }
            }
            catch (Exception e)
            {
                RrLog.Error("RecRally", "seat: restoring the player failed", e);
            }

            if (_capsule != null && Kart != null)
            {
                foreach (var c in Kart.GetComponents<Collider>())
                {
                    if (c != null)
                    {
                        Physics.IgnoreCollision(c, _capsule, false);
                    }
                }
            }

            Vector3 where = to ?? (Kart != null ? Kart.transform.TransformPoint(new Vector3(-2.2f, 0.3f, 0f)) : RrPlayer.FeetPosition);
            float face = to.HasValue ? yaw : Kart != null ? Kart.transform.eulerAngles.y : RrPlayer.Yaw;
            if (move)
            {
                RrPlayer.Teleport(where, face);
            }

            Kart = null;
            RrLog.Info("RecRally", "out of the buggy");
        }

        private void LateUpdate()
        {
            if (!Seated || Kart == null || _root == null)
            {
                return;
            }

            var kart = Kart.transform;
            bool vr = RrPlayer.InVr;
            if (!vr && _poses != null && !_poseFailed)
            {
                try
                {
                    _poses.SetPose(PoseType.SittingStraight);
                }
                catch (Exception e)
                {
                    _poseFailed = true;
                    RrLog.Warn("RecRally", "seat pose refused by the client (" + e.Message + "); driving standing");
                }
            }

            _root.position = kart.TransformPoint(RrRecRallyKart.SeatLocal);
            _root.rotation = Quaternion.Euler(0f, kart.eulerAngles.y, 0f);
            Vector3 eye = kart.TransformPoint(RrRecRallyKart.EyeLocal);
            if (vr)
            {
                PlaceHead(eye);
            }
            else
            {
                var head = RrPlayer.HeadAnchor;
                if (head != null && head.IsChildOf(_root))
                {
                    _root.position += eye - head.position;
                }
            }

            // Desktop play mode keeps its own view yaw: turn it with the buggy so the view stays on the road.
            if (!vr && RrPlayer.InPlayMode)
            {
                float yaw = kart.eulerAngles.y;
                float delta = Mathf.DeltaAngle(_yawWas, yaw);
                if (Mathf.Abs(delta) > 0.01f)
                {
                    RrScreenCamera.SetYaw(RrScreenCamera.Yaw + delta);
                }
            }

            _yawWas = kart.eulerAngles.y;
        }

        /// <summary>Moves the rig so the headset sits on the driver's eye, keeping the head's own lean (ETS PlaceHead).</summary>
        private void PlaceHead(Vector3 eye)
        {
            var cam = RrPlayer.Camera;
            if (cam == null)
            {
                return;
            }

            var head = cam.transform;
            if (_rig == null)
            {
                bool carried = head.IsChildOf(_root);
                _rig = carried ? _root : head.root;
                RrLog.Info("RecRally", "VR seat: the camera " + (carried ? "rides on the body" : "is outside the body") + "; moving rig '" + _rig.name + "'");
            }

            if (_rig != _root)
            {
                _rig.rotation = _root.rotation;
            }

            Vector3 inRig = _rig.InverseTransformPoint(head.position);
            if (!_restKnown || (inRig - _headRest).sqrMagnitude > 0.7f * 0.7f)
            {
                _headRest = inRig;
                _restKnown = true;
            }

            Vector3 lean = Vector3.ClampMagnitude(inRig - _headRest, LeanLimit);
            Vector3 wanted = eye + _rig.TransformVector(lean);
            _rig.position += wanted - head.position;
        }

        private void OnDisable()
        {
            if (Seated)
            {
                Stand(null, 0f, false);
            }
        }
    }
}
