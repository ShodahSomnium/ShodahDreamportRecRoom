using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// The editor's stand-in for the Somnium player, so the world can be walked without the client (the user's editor,
    /// the play-mode harness). Made by <see cref="RrPlayer"/> only while <c>SomniumBridge.PlayersContainer</c> is null,
    /// never once a client has been seen - so never in the client.
    ///
    /// A first-person capsule of ours on layer 0: a CharacterController (1.8 m, radius 0.3, pivot at the feet) with a
    /// "Head" at Rec Room's screen-mode eye height (<see cref="EyeHeight"/> 1.55 m). WASD/arrows walk (3 m/s, Shift x2,
    /// times <see cref="SpeedScale"/>), Space jumps when <see cref="JumpAllowed"/>, the right mouse button held looks
    /// around; in desktop play mode (<see cref="RrPlayer.InPlayMode"/>) the mouse looks with the cursor locked, as the
    /// client's camera would under <see cref="RrScreenCamera"/>. Nothing moves while a menu or text field has the input.
    /// Falling under y = -100 (the world's respawn height) puts it back at the last teleport target.
    ///
    /// The view: when no camera renders, the rig makes its own ("Rr Rig Camera", MainCamera, AudioListener, FOV 60 x
    /// <see cref="RrPlayer.FovKick"/>); when one already renders (the harness's stand-in for the client camera), the rig
    /// drives that one instead of adding a second camera and listener.
    /// </summary>
    public sealed class RrFallbackRig : MonoBehaviour
    {
        public const float EyeHeight = 1.55f;
        public const float Height = 1.8f;
        public const float Radius = 0.3f;
        public const float WalkSpeed = 3f;
        public const float SprintScale = 2f;
        public const float JumpSpeed = 4.5f;
        public const float Gravity = 15f;
        public const float LookSensitivity = 2f;
        public const float RespawnY = -100f;

        /// <summary>Walk speed multiplier (<see cref="RrPlayer.SetMovement"/>), 0 = frozen.</summary>
        public float SpeedScale = 1f;

        public bool JumpAllowed = true;

        public Transform Head { get; private set; }

        public Camera Camera { get; private set; }

        /// <summary>True when the rig made its camera; false when it drives one that was already rendering.</summary>
        public bool OwnCamera { get; private set; }

        private CharacterController _body;
        private float _yaw;
        private float _pitch;
        private float _vy;
        private bool _cursorLocked;
        private bool _axisFailed;
        private Vector3 _spawnFeet;
        private float _spawnYaw;
        private int _respawns;

        internal static RrFallbackRig Create(Transform parent, Camera existing, bool hasPose, Vector3 feet, float yaw)
        {
            var go = new GameObject("Rr Fallback Rig");
            go.layer = RrLayers.Default;
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<RrFallbackRig>();
            rig.Build(existing, hasPose, feet, yaw);
            return rig;
        }

        private void Build(Camera existing, bool hasPose, Vector3 feet, float yaw)
        {
            _body = gameObject.AddComponent<CharacterController>();
            _body.height = Height;
            _body.radius = Radius;
            _body.center = new Vector3(0f, Height * 0.5f, 0f);
            _body.stepOffset = 0.35f;
            _body.slopeLimit = 50f;
            _body.skinWidth = 0.04f;

            var head = new GameObject("Head");
            head.layer = RrLayers.Default;
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            Head = head.transform;

            string placedBy;
            if (hasPose)
            {
                placedBy = "the pending teleport";
            }
            else if (existing != null)
            {
                Transform t = existing.transform;
                feet = Ground(t.position - Vector3.up * EyeHeight, t.position.y + 0.5f);
                Vector3 e = t.rotation.eulerAngles;
                yaw = e.y;
                _pitch = Mathf.Clamp(e.x > 180f ? e.x - 360f : e.x, -89f, 89f);
                placedBy = "camera '" + existing.name + "'";
            }
            else
            {
                feet = Ground(Vector3.zero, 3f);
                yaw = 0f;
                placedBy = "the world origin";
            }

            if (existing != null)
            {
                Camera = existing;
                OwnCamera = false;
            }
            else
            {
                var camGo = new GameObject("Rr Rig Camera");
                camGo.layer = RrLayers.Default;
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(Head, false);
                Camera cam = camGo.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 5000f;
                cam.fieldOfView = 60f;
                camGo.AddComponent<AudioListener>();
                Camera = cam;
                OwnCamera = true;
            }

            Place(feet, yaw);
            _spawnFeet = feet;
            _spawnYaw = yaw;
            ApplyView();
            RrLog.Info("Player", "fallback rig at " + RrHand.Fmt(feet) + " yaw " + yaw.ToString("0") + " (placed by " + placedBy + "), " +
                                 (OwnCamera ? "own camera" : "driving camera '" + Camera.name + "'") +
                                 "; WASD walk, Shift run, Space jump, right mouse look");
        }

        /// <summary>The floor under <paramref name="p"/> (searched from <paramref name="fromY"/> down 60 m), else p.</summary>
        private static Vector3 Ground(Vector3 p, float fromY)
        {
            var origin = new Vector3(p.x, fromY, p.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return p;
        }

        internal void Teleport(Vector3 feet, float yaw)
        {
            Place(feet, yaw);
            _pitch = 0f;
            _spawnFeet = feet;
            _spawnYaw = yaw;
            ApplyView();
        }

        private void Place(Vector3 feet, float yaw)
        {
            bool was = _body.enabled;
            _body.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            _body.enabled = was;
            _yaw = yaw;
            _vy = 0f;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            bool vr = RrPlayer.InVr;
            bool usable = !RrInput.Blocked && Application.isFocused;
            bool play = RrPlayer.InPlayMode;
            bool rmbLook = !play && !vr && usable && Input.GetMouseButton(1);
            bool look = !vr && usable && (play || rmbLook);

            if (look)
            {
                if (!_cursorLocked || Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    _cursorLocked = true;
                }

                Vector2 d = MouseDelta();
                _yaw = Mathf.Repeat(_yaw + d.x * LookSensitivity, 360f);
                _pitch = Mathf.Clamp(_pitch - d.y * LookSensitivity, -89f, 89f);
            }
            else if (_cursorLocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _cursorLocked = false;
            }

            Vector3 move = Vector3.zero;
            if (usable)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                {
                    move.z += 1f;
                }

                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                {
                    move.z -= 1f;
                }

                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                {
                    move.x += 1f;
                }

                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                {
                    move.x -= 1f;
                }
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            float speed = WalkSpeed * SpeedScale * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? SprintScale : 1f);
            Quaternion body = Quaternion.Euler(0f, _yaw, 0f);
            transform.rotation = body;
            Vector3 velocity = body * move * speed;

            if (_body.isGrounded)
            {
                _vy = -1f;
                if (usable && JumpAllowed && SpeedScale > 0f && Input.GetKeyDown(KeyCode.Space))
                {
                    _vy = JumpSpeed;
                }
            }
            else
            {
                _vy -= Gravity * dt;
            }

            velocity.y = _vy;
            if (_body.enabled)
            {
                _body.Move(velocity * dt);
            }

            if (transform.position.y < RespawnY)
            {
                _respawns++;
                if (_respawns <= 3)
                {
                    RrLog.Warn("Player", "fallback rig fell under y " + RespawnY.ToString("0") + ": back to " + RrHand.Fmt(_spawnFeet));
                }

                Place(_spawnFeet, _spawnYaw);
            }

            ApplyView();
        }

        private void ApplyView()
        {
            Head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            if (Camera == null)
            {
                return;
            }

            if (OwnCamera)
            {
                Camera.transform.localPosition = Vector3.zero;
                Camera.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Camera.transform.SetPositionAndRotation(Head.position, Head.rotation);
            }

            Camera.fieldOfView = 60f * RrPlayer.FovKick;
        }

        private Vector2 MouseDelta()
        {
            if (_axisFailed)
            {
                return Vector2.zero;
            }

            try
            {
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            }
            catch (ArgumentException e)
            {
                _axisFailed = true;
                RrLog.Warn("Player", "no 'Mouse X/Y' axes in this Input Manager - rig mouse look off: " + e.Message);
                return Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            if (_cursorLocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _cursorLocked = false;
            }
        }
    }
}
