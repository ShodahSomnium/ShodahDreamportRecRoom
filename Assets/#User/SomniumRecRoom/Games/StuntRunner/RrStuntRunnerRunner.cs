using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using UnityEngine.XR;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.StuntRunner
{
    /// <summary>
    /// The local runner on one course: checkpoints, kill zones, KO Cola boosts, bounce pads, the finish button, and
    /// Stunt Runner's movement as far as the Somnium client lets a world shape it (somnium_player.md 6.1, 6.4: the
    /// Bridge has speed, jump and gravity multipliers and teleports, no velocity). The client's own walk stays in
    /// charge of the body; this only turns its knobs:
    ///
    /// - walking at Rec Room's 3 m/s (desktop walk 1.6 m/s x 1.9, VR stick 3 m/s; Shift sprint 6 x 0.75 = 4.5 m/s,
    ///   Rec Room's sprint factor 1.5); VR teleport off (Stunt Runner is walk-only, rr_latest_rgames_story.md 11.3);
    /// - KO Cola: x1.45 speed for 2 s per can, stacking (StuntRunnerBoostManager), Locomotion_KOCola_Initiate01;
    /// - bounce pads (SpringBoard_2x2/2x4): the jump is x2.4 while standing on one (Rec Room launches by itself: GUESS
    ///   stand-in, the client has no impulse call), SR_SpringBoard_Bounce01;
    /// - wall run: airborne beside a wall the fall is slowed to x0.15 gravity for up to 2.5 s (Rec Room: up to 10 s with a
    ///   0.6 drop rate; the client's gravity multiplier only acts while falling), Locomotion_WallRun_Attach01 / Detach01;
    /// - slide: C (desktop) or the right stick click (VR) while moving: 1.5 s at x1.6 speed with the field of view
    ///   widened x1.3 (Rec Room: slide up to 2 x sprint, FOV 1.3), Locomotion_Sliding_SlowLoop01. The capsule cannot be
    ///   lowered from the world, so the slide does not pass under low bars;
    /// - rope nets: touching a net, Space (desktop) or a grip on it (VR) climbs it - the runner is put on the ledge above
    ///   (Rec Room climbs hand over hand; the client has no climbing a world can switch on at run time).
    /// </summary>
    public sealed class RrStuntRunnerRunner : MonoBehaviour
    {
        public const float WalkScale = 1.9f;
        public const float SprintScale = 0.75f;
        public const float BoostFactor = 1.45f;
        public const float BoostSecondsPerCan = 2f;
        public const float PadJump = 2.4f;
        public const float WallRunGravity = 0.15f;
        public const float WallRunMax = 2.5f;
        public const float SlideSeconds = 1.5f;
        public const float SlideFactor = 1.6f;
        public const float PickupRespawn = 5f;
        public const string PickupPrefab = "(Stuntrunner_Pickup_Sandbox)";

        public RrStuntRunnerRoom Room;
        public RrStuntRunnerLevel Level;

        /// <summary>The respawn point: the last checkpoint touched (room-local), the start lane at first.</summary>
        public Pose Respawn;

        public int CheckpointsTouched;
        public bool Finished;
        public bool Live;

        private readonly HashSet<int> _touched = new HashSet<int>();
        private readonly List<Pickup> _pickups = new List<Pickup>();
        private float _boostUntil;
        private float _slideUntil = -1f;
        private float _wallSince = -1f;
        private bool _wallOn;
        private bool _onPad;
        private float _padUntil;
        private float _safeUntil;
        private float _climbAt;
        private float _nextCheck;
        private AudioSource _slideLoop;
        private Vector3 _lastFeet;
        private float _speed;
        private bool _climbPrompt;
        private bool _stickWas;

        // last values sent to the client, so it is only called on change (and every 2 s)
        private float _sentSpeed = -1f, _sentJump = -1f, _sentGravity = -1f;
        private float _resendAt;
        private bool _teleportOff;

        private sealed class Pickup
        {
            public Vector3 At;
            public GameObject Visual;
            public float BackAt = -1f;
        }

        // ------------------------------------------------------------------ setup

        /// <summary>Puts the KO Cola cans of the course in (install prefab, a plain can if it does not load).</summary>
        public void SpawnPickups(Transform parent)
        {
            foreach (var p in Level.Pickups)
            {
                var pickup = new Pickup { At = p };
                _pickups.Add(pickup);
                RrSpawn.Spawn(PickupPrefab, parent, p, Quaternion.identity, inst =>
                {
                    if (inst == null)
                    {
                        pickup.Visual = RrBuild.Cylinder(parent, "KO Cola", p, 0.1f, 0.47f, RrLook.Emissive(new Color(1f, 0.3f, 0.25f), 1.2f), false);
                        return;
                    }

                    pickup.Visual = inst.gameObject;
                    if (pickup.BackAt > 0f)
                    {
                        pickup.Visual.SetActive(false);
                    }
                }, RrSpawnOptions.Visual());
            }
        }

        /// <summary>A new attempt at the course: nothing touched, the respawn on the start lane.</summary>
        public void ResetRun(Pose start)
        {
            Respawn = start;
            CheckpointsTouched = 0;
            Finished = false;
            _touched.Clear();
            _boostUntil = 0f;
            _slideUntil = -1f;
            _wallOn = false;
            _wallSince = -1f;
            foreach (var p in _pickups)
            {
                p.BackAt = -1f;
                if (p.Visual != null)
                {
                    p.Visual.SetActive(true);
                }
            }
        }

        // ------------------------------------------------------------------ motion knobs

        /// <summary>Frozen in the start gate (the client's walk and jump at zero, teleport off).</summary>
        public static void Freeze()
        {
            RrPlayer.SetMovement(0f, false);
        }

        /// <summary>Everything back to the client's own (multipliers 1, teleport on, gravity 1).</summary>
        public void Restore()
        {
            Live = false;
            StopSlide();
            RrPlayer.SetMovement(1f, true);
            var m = RrPlayer.Motion;
            if (m != null)
            {
                try
                {
                    m.SetGravity(1f, 1f);
                    m.SetTeleportationDisableState(false);
                }
                catch (System.Exception e)
                {
                    RrLog.Warn("StuntRunner", "restoring the client's gravity failed: " + e.Message);
                }
            }

            _teleportOff = false;
            _sentSpeed = _sentJump = _sentGravity = -1f;
            RrPlayer.FovKick = 1f;
        }

        private void Apply(float speed, float jump, float gravity)
        {
            float now = Time.unscaledTime;
            if (Mathf.Approximately(speed, _sentSpeed) && Mathf.Approximately(jump, _sentJump) && Mathf.Approximately(gravity, _sentGravity) && now < _resendAt)
            {
                return;
            }

            _resendAt = now + 2f;
            var m = RrPlayer.Motion;
            if (m == null)
            {
                // Editor rig: speed only.
                if (!Mathf.Approximately(speed, _sentSpeed))
                {
                    RrPlayer.SetMovement(speed * WalkScale * 0.5f, true);
                }

                _sentSpeed = speed;
                _sentJump = jump;
                _sentGravity = gravity;
                return;
            }

            try
            {
                m.SetMovementSpeed(WalkScale * speed, speed, SprintScale * speed);
                m.SetJump(jump, jump);
                m.SetGravity(gravity, gravity);
                if (!_teleportOff)
                {
                    m.SetTeleportationDisableState(true);
                    m.SetFlyModeDisableState(false);
                    _teleportOff = true;
                }
            }
            catch (System.Exception e)
            {
                RrLog.Warn("StuntRunner", "setting the runner's movement failed: " + e.Message);
            }

            if (_sentSpeed < 0f)
            {
                RrLog.Info("StuntRunner", "runner movement on: walk x" + WalkScale + ", sprint x" + SprintScale + ", VR teleport off");
            }

            _sentSpeed = speed;
            _sentJump = jump;
            _sentGravity = gravity;
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            if (Level == null || Room == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            Pickups(now);
            if (!Live)
            {
                return;
            }

            var room = Room.transform;
            Vector3 feetWorld = RrPlayer.FeetPosition;
            Vector3 feet = room.InverseTransformPoint(feetWorld);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var flat = feetWorld - _lastFeet;
            flat.y = 0f;
            _speed = Mathf.Lerp(_speed, flat.magnitude / dt, 0.2f);
            _lastFeet = feetWorld;

            if (now >= _nextCheck)
            {
                _nextCheck = now + 0.066f;
                if (now >= _safeUntil && OutOfCourse(feet))
                {
                    Fall();
                    return;
                }

                Checkpoints(feet);
                Collect(feet, now);
                _onPad = OnPad(feet);
                if (_onPad)
                {
                    _padUntil = now + 0.35f;
                }

                WallRun(feetWorld, now);
                Finish(feet);
            }

            Slide(now);
            Climb(feet, now);

            float speed = 1f;
            if (now < _boostUntil)
            {
                speed *= BoostFactor;
            }

            if (now < _slideUntil)
            {
                speed *= SlideFactor;
            }

            float jump = now < _padUntil ? PadJump : 1f;
            float gravity = _wallOn ? WallRunGravity : 1f;
            Apply(speed, jump, gravity);
        }

        private void OnDisable()
        {
            if (Live)
            {
                Restore();
            }

            StopSlide();
        }

        // ------------------------------------------------------------------ course rules

        private bool OutOfCourse(Vector3 feet)
        {
            foreach (var k in Level.KillZones)
            {
                if (k.Contains(feet + Vector3.up * 0.3f))
                {
                    return true;
                }
            }

            return feet.y < Level.Bounds.min.y - 4f;
        }

        /// <summary>Out (pit, pool, off the stage): buzzer, a taunt, back to the last checkpoint.</summary>
        public void Fall()
        {
            _safeUntil = Time.unscaledTime + 1.5f;
            _wallOn = false;
            StopSlide();
            RrSound.PlayUi("SR_Buzzer_Short01", 0.6f);
            string[] taunts = { "SR_Announcer_AlmostHadIt01", "SR_Announcer_FeelThatInTheMorning01", "SR_Announcer_Ooh01", "SR_Announcer_ThatsGottaHurt01", "SR_Announcer_ThatsGottaHurt02", "SR_Announcer_WatchOut01" };
            if (Random.value < 0.6f)
            {
                RrSound.PlayUi(taunts[Random.Range(0, taunts.Length)], 0.9f);
            }

            RrSound.PlayUi(Random.value < 0.5f ? "SR_CrowdReactions_Laugh02" : "SR_Crowd_Aww_01", 0.5f);
            Room.TeleportLocal(Respawn);
            Room.Toast("BACK TO THE CHECKPOINT");
        }

        private void Checkpoints(Vector3 feet)
        {
            for (int i = 0; i < Level.Checkpoints.Count; i++)
            {
                if (_touched.Contains(i))
                {
                    continue;
                }

                var c = Level.Checkpoints[i];
                // The arch is ~13 m wide (rr_party.md 2.3): a box 14 x 5 x 5 m on its floor, so it fires only inside it
                // (the 2019 course 4 fix: never from outside the tower).
                var l = Quaternion.Inverse(c.rotation) * (feet - c.position);
                if (Mathf.Abs(l.x) > 7f || Mathf.Abs(l.z) > 2.5f || l.y < -1f || l.y > 4f)
                {
                    continue;
                }

                _touched.Add(i);
                CheckpointsTouched++;
                Respawn = new Pose(c.position + Vector3.up * 0.05f, c.rotation);
                RrSound.PlayUi("SR_Collectible_Collect01", 0.6f);
                Room.Toast("CH-CH-CHECKPOINT!");
                Room.LocalProgress();
            }
        }

        private bool OnPad(Vector3 feet)
        {
            foreach (var p in Level.Pads)
            {
                var l = Quaternion.Inverse(p.rotation) * (feet - p.position);
                if (Mathf.Abs(l.x) < 1.3f && Mathf.Abs(l.z) < 2.3f && l.y > -0.5f && l.y < 1.2f)
                {
                    if (!_onPad)
                    {
                        RrSound.Play("SR_SpringBoard_Bounce01", Room.transform.TransformPoint(p.position), 0.7f);
                        Room.Prompt(RrPlayer.InVr ? "JUMP - BOUNCE PAD" : "SPACE - BOUNCE PAD");
                    }

                    return true;
                }
            }

            return false;
        }

        private void Finish(Vector3 feet)
        {
            if (Finished || !Level.HasButton)
            {
                return;
            }

            // Slapping the button: any hand (VR), the head or the body within reach of it.
            var room = Room.transform;
            Vector3 b = room.TransformPoint(Level.Button);
            bool near = (RrPlayer.HeadPosition - b).sqrMagnitude < 1.0f || (room.TransformPoint(feet) + Vector3.up - b).sqrMagnitude < 1.1f * 1.1f;
            if (!near)
            {
                foreach (var side in new[] { RrHandSide.Left, RrHandSide.Right })
                {
                    var h = RrPlayer.BridgeHand(side);
                    if (h != null && (h.position - b).sqrMagnitude < 0.45f * 0.45f)
                    {
                        near = true;
                    }
                }
            }

            if (near)
            {
                PressFinish("reach");
            }
        }

        /// <summary>The finish button was pressed (reach, click or poke).</summary>
        public void PressFinish(string how)
        {
            if (!Live || Finished)
            {
                return;
            }

            Finished = true;
            StopSlide();
            RrSound.PlayUi("SR_ButtonPressed_01", 0.9f);
            Room.LocalFinished(how);
        }

        private void Collect(Vector3 feet, float now)
        {
            Vector3 chest = feet + Vector3.up * 0.9f;
            foreach (var p in _pickups)
            {
                if (p.BackAt > 0f || (p.At - chest).sqrMagnitude > 1.3f * 1.3f)
                {
                    continue;
                }

                p.BackAt = now + PickupRespawn;
                if (p.Visual != null)
                {
                    p.Visual.SetActive(false);
                }

                _boostUntil = Mathf.Max(_boostUntil, now) + BoostSecondsPerCan;
                RrSound.PlayUi("SR_KOCola_Collect0" + Random.Range(1, 4), 0.6f);
                RrSound.PlayUi("Locomotion_KOCola_Initiate01", 0.7f);
                Room.Toast("KO COLA! BOOST");
            }
        }

        private void Pickups(float now)
        {
            foreach (var p in _pickups)
            {
                if (p.BackAt > 0f && now >= p.BackAt)
                {
                    p.BackAt = -1f;
                    if (p.Visual != null)
                    {
                        p.Visual.SetActive(true);
                    }
                }
                else if (p.BackAt < 0f && p.Visual != null)
                {
                    // Spins 50 deg/s (BoostCollectible).
                    p.Visual.transform.Rotate(0f, 50f * Time.deltaTime, 0f, Space.World);
                }
            }
        }

        private void WallRun(Vector3 feetWorld, float now)
        {
            int mask = 1 << RrLayers.Environment;
            bool airborne = !Physics.Raycast(feetWorld + Vector3.up * 0.3f, Vector3.down, 0.6f, mask, QueryTriggerInteraction.Ignore);
            bool wall = false;
            if (airborne && _speed > 2f)
            {
                var yaw = Quaternion.Euler(0f, RrPlayer.Yaw, 0f);
                var from = feetWorld + Vector3.up * 1f;
                RaycastHit hit;
                foreach (var dir in new[] { yaw * Vector3.right, yaw * Vector3.left })
                {
                    if (Physics.Raycast(from, dir, out hit, 0.9f, mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.normal.y) < 0.3f)
                    {
                        wall = true;
                        break;
                    }
                }
            }

            if (!airborne)
            {
                _wallSince = -1f;
            }

            bool on = wall && (_wallSince < 0f || now - _wallSince < WallRunMax);
            if (on && _wallSince < 0f)
            {
                _wallSince = now;
            }

            if (on != _wallOn)
            {
                _wallOn = on;
                RrSound.PlayUi(on ? "Locomotion_WallRun_Attach01" : "Locomotion_WallRun_Detach01", on ? 0.25f : 0.22f);
            }
        }

        private void Slide(float now)
        {
            bool pressed = false;
            if (RrPlayer.InVr)
            {
                bool click;
                var right = RrXr.Controller(RrHandSide.Right);
                bool down = right.isValid && right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out click) && click;
                pressed = down && !_stickWas;
                _stickWas = down;
            }
            else if (!RrInput.Blocked)
            {
                pressed = Input.GetKeyDown(KeyCode.C);
            }

            if (pressed && _speed > 1.5f && now >= _slideUntil)
            {
                _slideUntil = now + SlideSeconds;
                RrSound.PlayUi("Locomotion_WallRun_Attach01", 0.35f);
                if (_slideLoop == null && RrPlayer.PlayerObject != null)
                {
                    _slideLoop = RrSound.Loop("Locomotion_Sliding_SlowLoop01", RrPlayer.PlayerObject, 0.65f, 15f);
                }

                RrPlayer.FovKick = 1.3f;
            }

            if (_slideUntil > 0f && now >= _slideUntil)
            {
                StopSlide();
            }
        }

        private void StopSlide()
        {
            _slideUntil = -1f;
            if (_slideLoop != null)
            {
                RrSound.Stop(_slideLoop);
                _slideLoop = null;
            }

            if (Live)
            {
                RrPlayer.FovKick = 1f;
            }
        }

        // ------------------------------------------------------------------ climbing

        private void Climb(Vector3 feet, float now)
        {
            if (Level.Nets.Count == 0)
            {
                return;
            }

            Vector3 chest = feet + Vector3.up * 1.1f;
            int touching = -1;
            for (int i = 0; i < Level.Nets.Count; i++)
            {
                var b = Level.Nets[i];
                b.Expand(1.2f);
                if (b.Contains(chest))
                {
                    touching = i;
                    break;
                }
            }

            if (touching < 0)
            {
                _climbPrompt = false;
                return;
            }

            if (!_climbPrompt)
            {
                _climbPrompt = true;
            }

            Room.Prompt(RrPlayer.InVr ? "GRIP THE NET TO CLIMB" : "SPACE - CLIMB THE NET");
            bool go = false;
            if (RrPlayer.InVr)
            {
                go = RrXr.Grip(RrHandSide.Left) > 0.8f && RrXr.Grip(RrHandSide.Right) > 0.8f || NetGrip(RrHandSide.Left, touching) || NetGrip(RrHandSide.Right, touching);
            }
            else if (!RrInput.Blocked)
            {
                go = Input.GetKeyDown(KeyCode.Space);
            }

            if (!go || now < _climbAt)
            {
                return;
            }

            _climbAt = now + 0.8f;
            Vector3 top;
            if (FindTop(chest, touching, out top))
            {
                RrSound.PlayUi("Locomotion_Climbing_GrabWall01", 0.5f);
                RrSound.PlayUi("Locomotion_Clamber_03", 0.5f);
                _safeUntil = now + 1f;
                Room.TeleportLocal(new Pose(top + Vector3.up * 0.05f, Quaternion.Euler(0f, RrPlayer.Yaw - Room.transform.eulerAngles.y, 0f)));
            }
        }

        private bool NetGrip(RrHandSide side, int net)
        {
            if (RrXr.Grip(side) < 0.8f)
            {
                return false;
            }

            var h = RrPlayer.BridgeHand(side);
            if (h == null)
            {
                return false;
            }

            var b = Level.Nets[net];
            b.Expand(0.3f);
            return b.Contains(Room.transform.InverseTransformPoint(h.position));
        }

        /// <summary>The ledge above the net wall the chest touches: the wall's top, then the floor just past it.</summary>
        private bool FindTop(Vector3 chest, int first, out Vector3 top)
        {
            top = Vector3.zero;
            var wall = Level.Nets[first];
            bool grew = true;
            int guard = 0;
            while (grew && guard++ < 40)
            {
                grew = false;
                var reach = wall;
                reach.Expand(0.3f);
                foreach (var n in Level.Nets)
                {
                    var flat = new Vector2(n.center.x - chest.x, n.center.z - chest.z);
                    if (flat.sqrMagnitude > 2.5f * 2.5f || !reach.Intersects(n) || (wall.Contains(n.min) && wall.Contains(n.max)))
                    {
                        continue;
                    }

                    wall.Encapsulate(n);
                    grew = true;
                }
            }

            var dir = new Vector3(wall.center.x - chest.x, 0f, wall.center.z - chest.z);
            if (dir.sqrMagnitude < 1e-3f)
            {
                dir = Quaternion.Euler(0f, RrPlayer.Yaw - Room.transform.eulerAngles.y, 0f) * Vector3.forward;
            }

            dir.Normalize();
            var room = Room.transform;
            foreach (float d in new[] { 0.9f, 1.5f, 2.2f, 0.4f })
            {
                var from = new Vector3(chest.x, wall.max.y + 1.5f, chest.z) + dir * d;
                RaycastHit hit;
                if (Physics.Raycast(room.TransformPoint(from), Vector3.down, out hit, 3.5f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore))
                {
                    var p = room.InverseTransformPoint(hit.point);
                    if (p.y > chest.y - 0.6f && hit.normal.y > 0.6f)
                    {
                        top = p;
                        return true;
                    }
                }
            }

            Room.Toast("NOWHERE TO CLIMB TO");
            return false;
        }
    }
}
