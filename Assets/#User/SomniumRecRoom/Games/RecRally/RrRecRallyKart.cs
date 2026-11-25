using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.RecRally
{
    /// <summary>
    /// One Rally Buggy. The look is Rec Room's own <c>(Vehicle_RallyBuggy)</c> (container 20d; colliders, rigidbody and
    /// scripts stripped, a box buggy if it does not load); the driving is ours, an arcade raycast-wheel car on Rec Room's
    /// numbers where they fit (rr_party.md 3.2): wheels at (+-1.0, 0.524, +1.246 / -1.274), radius 0.46 m, 0.6 m of travel
    /// resting 40 % compressed with damping ratio 0.8, 35 degree steering (eased to 12 at top speed - GUESS, for keyboard
    /// stability), both axles driven, tyre friction 1.7, the handbrake drift (rear lateral grip x0.306), the drift boost
    /// after a 10 degree+ slide (GUESS 1 s at x1.6), nitro: 3 cans = 1 boost of 3 s at +35 % force and x1.25 top speed.
    /// Top speed 24 m/s (GUESS; Rec Room's 700 RPM on 0.46 m wheels gives ~34). Mass 600 kg (Rec Room 1000; lighter is
    /// steadier with our simple tyres).
    ///
    /// The local player's buggy is simulated here and published through the presence slot's vehicle ints 20-28
    /// (<see cref="Publish"/>); the others' buggies are kinematic and follow their owners' ints, dead-reckoned with the
    /// sent velocity, so the local buggy bumps into them (bumps are felt by the one who hits, as in GUESS rr_party 3.3).
    /// </summary>
    public sealed class RrRecRallyKart : MonoBehaviour
    {
        public const string Prefab = "(Vehicle_RallyBuggy)";
        public const float Mass = 600f;
        public const float WheelRadius = 0.46f;
        public const float Travel = 0.6f;
        public const float TopSpeed = 24f;
        public const float ReverseTop = 8f;
        public const float DriveForce = 3900f;
        public const float BrakeForce = 7000f;
        public const float Friction = 1.7f;
        public const float MaxSteer = 35f;
        public const float BoostSeconds = 3f;
        public const int CansPerBoost = 3;

        /// <summary>The driver's seat (chassis space) and the driver's eye above it.</summary>
        public static readonly Vector3 SeatLocal = new Vector3(-0.364f, 0.771f, -0.156f);
        public static readonly Vector3 EyeLocal = new Vector3(-0.364f, 1.52f, -0.08f);

        private static readonly Vector3[] Mounts =
        {
            new Vector3(-1f, 0.524f, 1.246f), new Vector3(1f, 0.524f, 1.246f), new Vector3(-1f, 0.524f, -1.274f), new Vector3(1f, 0.524f, -1.274f)
        };

        public RrRecRallyRoom Room;
        public int OwnerId;
        public int Slot;
        public bool Local;

        // ---- driver input (local) ----
        public float Throttle;
        public float Steer;
        public bool Drift;
        public bool BoostPressed;
        public bool Locked = true;

        public int Cans;
        public bool Boosting { get { return Time.time < _boostUntil; } }
        public bool DriftBoosting { get { return Time.time < _driftBoostUntil; } }
        public float Speed { get; private set; }
        public bool Grounded { get; private set; }

        private Rigidbody _rb;
        private float _steerAngle;
        private float _boostUntil, _driftBoostUntil;
        private float _driftSlideTime;
        private bool _wasDrifting;
        private readonly float[] _spin = new float[4];
        private readonly bool[] _wheelGround = new bool[4];
        private readonly Transform[] _tires = new Transform[4];
        private readonly Quaternion[] _tireRest = new Quaternion[4];
        private AudioSource _idle, _accel, _boostLoop;

        // ---- remote following ----
        private int _seenSeq = int.MinValue;
        private Vector3 _netPos, _netVel;
        private Quaternion _netRot = Quaternion.identity;
        private float _netAt = -1f;
        public float LastHeard { get; private set; }

        // ---- local publishing ----
        private int _seq;
        private float _publishAt;

        public Rigidbody Body
        {
            get { return _rb; }
        }

        // ------------------------------------------------------------------ making

        public static RrRecRallyKart Create(RrRecRallyRoom room, Transform parent, Pose local, int owner, int slot, bool isLocal)
        {
            var go = new GameObject((isLocal ? "My Buggy " : "Buggy ") + slot + " (player " + owner + ")");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local.position;
            go.transform.localRotation = local.rotation;
            go.layer = RrLayers.Prop;
            var kart = go.AddComponent<RrRecRallyKart>();
            kart.Room = room;
            kart.OwnerId = owner;
            kart.Slot = slot;
            kart.Local = isLocal;
            kart.Setup();
            return kart;
        }

        private void Setup()
        {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = Mass;
            _rb.linearDamping = 0.05f;
            _rb.angularDamping = 0.8f;
            _rb.interpolation = Local ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            _rb.collisionDetectionMode = Local ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            _rb.isKinematic = true;

            // The body: the buggy's floor-to-roll-cage box and the skid plate (rr_party 3.2: SkidPlate 1.25 x 0.25 x 3.1).
            var body = gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 1.05f, -0.1f);
            body.size = new Vector3(2.0f, 1.1f, 3.6f);
            var skid = gameObject.AddComponent<BoxCollider>();
            skid.center = new Vector3(0f, 0.45f, 0f);
            skid.size = new Vector3(1.25f, 0.3f, 3.1f);
            _rb.centerOfMass = new Vector3(0f, 0.35f, 0f);

            RrSpawn.Spawn(Prefab, transform, Vector3.zero, Quaternion.identity, inst =>
            {
                if (this == null)
                {
                    return;
                }

                if (inst == null)
                {
                    RrLog.Warn("RecRally", Prefab + " did not load: a box buggy stands in");
                    BoxBuggy();
                    return;
                }

                FindTires(inst.transform);
            }, new RrSpawnOptions { StripColliders = true, StripRigidbodies = true, StripAudioSources = true, Layer = RrLayers.Prop });

            if (Local)
            {
                _idle = RrSound.Loop("sfx_rec_rally_utv_idle_loop_01", transform, 0.5f, 40f);
                _accel = RrSound.Loop("sfx_rec_rally_utv_acceleration_loop_09", transform, 0f, 60f);
                RrSound.Play("sfx_rec_rally_utv_enginestart_rev_01", transform.position, 0.7f);
            }
            else
            {
                _idle = RrSound.Loop("sfx_rec_rally_utv_idle_loop_01", transform, 0.35f, 40f);
            }
        }

        private void BoxBuggy()
        {
            var colour = RrTeams.Color(Slot % 4);
            var m = RrLook.Solid(colour);
            var dark = RrLook.Solid(RrLook.DarkGrey);
            RrBuild.Box(transform, "Floor", new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.2f, 3.4f), m, false, RrLayers.Prop);
            RrBuild.Box(transform, "Hood", new Vector3(0f, 0.9f, 1.3f), new Vector3(1.6f, 0.4f, 0.8f), m, false, RrLayers.Prop);
            RrBuild.Box(transform, "Cage L", new Vector3(-0.85f, 1.5f, -0.3f), new Vector3(0.08f, 1.4f, 0.08f), dark, false, RrLayers.Prop);
            RrBuild.Box(transform, "Cage R", new Vector3(0.85f, 1.5f, -0.3f), new Vector3(0.08f, 1.4f, 0.08f), dark, false, RrLayers.Prop);
            RrBuild.Box(transform, "Cage Top", new Vector3(0f, 2.2f, -0.3f), new Vector3(1.78f, 0.08f, 1.2f), dark, false, RrLayers.Prop);
            for (int i = 0; i < 4; i++)
            {
                var w = RrBuild.Cylinder(transform, "Wheel " + i, Mounts[i] - Vector3.up * 0.36f, WheelRadius, 0.35f, dark, false, RrLayers.Prop);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                _tires[i] = w.transform;
                _tireRest[i] = Quaternion.Inverse(transform.rotation) * w.transform.rotation;
            }
        }

        /// <summary>The prefab's tyre joints (Kart_Tire_*_Jnt), told apart by where they sit (front +z, left -x).</summary>
        private void FindTires(Transform root)
        {
            var found = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Kart_Tire") && t.name.EndsWith("_Jnt"))
                {
                    found.Add(t);
                }
            }

            foreach (var t in found)
            {
                var l = transform.InverseTransformPoint(t.position);
                int i = (l.z > 0f ? 0 : 2) + (l.x < 0f ? 0 : 1);
                if (_tires[i] == null)
                {
                    _tires[i] = t;
                    _tireRest[i] = Quaternion.Inverse(transform.rotation) * t.rotation;
                }
            }

            if (Local)
            {
                RrLog.Info("RecRally", "buggy " + Prefab + " in: " + found.Count + " tyre joints");
            }
        }

        // ------------------------------------------------------------------ driving (local)

        /// <summary>Lets it go (the start) or holds it (the countdown's seat lock).</summary>
        public void SetLocked(bool locked)
        {
            Locked = locked;
            if (_rb == null)
            {
                return;
            }

            if (Local)
            {
                _rb.isKinematic = locked;
                if (!locked)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
            }
        }

        /// <summary>Puts the buggy at a room-local pose, standing still (start, reset).</summary>
        public void Place(Pose local)
        {
            var room = transform.parent;
            var p = room != null ? room.TransformPoint(local.position) : local.position;
            var r = room != null ? room.rotation * local.rotation : local.rotation;
            bool kin = _rb.isKinematic;
            _rb.isKinematic = true;
            transform.SetPositionAndRotation(p, r);
            _rb.position = p;
            _rb.rotation = r;
            if (!kin)
            {
                _rb.isKinematic = false;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            _netPos = p;
            _netRot = r;
            _netVel = Vector3.zero;
        }

        /// <summary>Spends the nitro: three cans make one 3 s boost (VehicleBoost capacity 3, cost 3).</summary>
        public bool TryBoost()
        {
            if (Cans < CansPerBoost || Boosting)
            {
                return false;
            }

            Cans = 0;
            _boostUntil = Time.time + BoostSeconds;
            RrSound.Play("Locomotion_Boost_Initiate02", transform.position, 0.8f);
            if (_boostLoop == null)
            {
                _boostLoop = RrSound.Loop("Locomotion_Boost_Looping01", transform, 0.5f, 40f);
            }

            return true;
        }

        public void AddCan()
        {
            Cans = Mathf.Min(CansPerBoost, Cans + 1);
        }

        private void FixedUpdate()
        {
            if (!Local || _rb == null || _rb.isKinematic)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            var up = transform.up;
            var vel = _rb.linearVelocity;
            float fwdSpeed = Vector3.Dot(vel, transform.forward);
            Speed = vel.magnitude;

            // Steering: eased in over 0.25 s (SteeringWheelScreenToolBehaviorConfig_Kart smoothing), less lock at speed.
            float lockAngle = Mathf.Lerp(MaxSteer, 12f, Mathf.Clamp01(Mathf.Abs(fwdSpeed) / TopSpeed));
            _steerAngle = Mathf.MoveTowards(_steerAngle, Steer * lockAngle, MaxSteer / 0.25f * dt);

            bool boost = Boosting;
            bool driftBoost = DriftBoosting;
            float top = TopSpeed * (boost ? 1.25f : 1f) * (driftBoost ? 1.1f : 1f);
            float force = DriveForce * (boost ? 1.35f : 1f) * (driftBoost ? 1.6f : 1f);
            float k = Mass * 9.81f / 4f / (0.4f * Travel);
            float c = 2f * 0.8f * Mathf.Sqrt(k * Mass / 4f);
            int grounded = 0;
            float slip = 0f;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                var mount = transform.TransformPoint(Mounts[i]);
                RaycastHit hit;
                _wheelGround[i] = false;
                if (!Physics.Raycast(mount, -up, out hit, Travel + WheelRadius, (1 << RrLayers.Environment) | 1, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (hit.rigidbody == _rb)
                {
                    continue;
                }

                _wheelGround[i] = true;
                grounded++;
                float compression = Travel + WheelRadius - hit.distance;
                var pv = _rb.GetPointVelocity(mount);
                float spring = Mathf.Max(0f, k * compression - c * Vector3.Dot(pv, up));
                _rb.AddForceAtPosition(up * spring, mount);

                var heading = front ? Quaternion.AngleAxis(_steerAngle, up) * transform.forward : transform.forward;
                var fwd = Vector3.ProjectOnPlane(heading, hit.normal).normalized;
                var side = Vector3.Cross(hit.normal, fwd).normalized;
                float vf = Vector3.Dot(pv, fwd);
                float vs = Vector3.Dot(pv, side);
                float grip = Friction * spring;
                float lateralGrip = 1f;
                if (Drift)
                {
                    lateralGrip = front ? 0.85f : 0.306f;
                }

                float lat = Mathf.Clamp(-vs * (Mass / 4f) / dt * 0.5f, -grip * lateralGrip, grip * lateralGrip);
                float lon = 0f;
                if (Throttle > 0.01f)
                {
                    lon = Throttle * force / 4f * Mathf.Clamp01(1f - fwdSpeed / top);
                }
                else if (Throttle < -0.01f)
                {
                    lon = fwdSpeed > 0.8f ? Throttle * BrakeForce / 4f : Throttle * force * 0.5f / 4f * Mathf.Clamp01(1f + fwdSpeed / ReverseTop);
                }
                else
                {
                    lon = -vf * 60f;
                }

                if (Drift && !front)
                {
                    lon *= 0.9f;
                }

                lon = Mathf.Clamp(lon, -grip, grip);
                // Applied at the centre of mass's height so the buggy does not roll over on the turns.
                var at = transform.TransformPoint(new Vector3(Mounts[i].x * 0.6f, _rb.centerOfMass.y, Mounts[i].z));
                _rb.AddForceAtPosition(fwd * lon + side * lat, at);
                _spin[i] += vf / WheelRadius * Mathf.Rad2Deg * dt;
                if (!front)
                {
                    slip = Mathf.Max(slip, Mathf.Abs(Mathf.Atan2(vs, Mathf.Max(0.1f, Mathf.Abs(vf))) * Mathf.Rad2Deg));
                }
            }

            Grounded = grounded > 0;
            _rb.angularDamping = Grounded ? 0.8f : 2f;
            if (!Grounded)
            {
                // A little air control and self-righting, so jumps land on the wheels.
                var tilt = Vector3.Cross(transform.up, Vector3.up);
                _rb.AddTorque(tilt * Mass * 2.5f, ForceMode.Force);
            }
            else
            {
                // Downforce: the buggy stays on the dirt over crests.
                _rb.AddForce(-up * Mathf.Min(vel.sqrMagnitude, 900f) * 2f);
            }

            // Drift boost (VehicleSteeringAssist): a slide of 10 degrees or more, then letting go of the handbrake.
            bool drifting = Drift && Grounded && slip >= 10f && Speed > 5f;
            if (drifting)
            {
                _driftSlideTime += dt;
            }

            if (_wasDrifting && !Drift && _driftSlideTime > 0.6f)
            {
                _driftBoostUntil = Time.time + 1f;
                RrSound.Play("Locomotion_Boost_Initiate02", transform.position, 0.4f, 1.3f);
            }

            if (!Drift)
            {
                _driftSlideTime = 0f;
            }

            _wasDrifting = Drift;
        }

        private void Update()
        {
            float now = Time.time;
            if (!Local)
            {
                Follow();
            }
            else if (!Boosting && _boostLoop != null)
            {
                RrSound.Stop(_boostLoop);
                _boostLoop = null;
                RrSound.Play("Locomotion_Boost_Spent01", transform.position, 0.5f);
            }

            // Engine: idle pitch with speed, the acceleration loop with the throttle (throttle cap 14 m/s).
            float speed = Local ? Speed : _netVel.magnitude;
            if (_idle != null)
            {
                _idle.pitch = 0.8f + Mathf.Clamp01(speed / 24f) * 0.9f;
            }

            if (_accel != null)
            {
                _accel.volume = Mathf.MoveTowards(_accel.volume, Mathf.Abs(Throttle) > 0.05f && !Locked ? 0.55f : 0f, Time.deltaTime * 2f);
                _accel.pitch = 0.85f + Mathf.Clamp01(speed / 14f) * 0.5f;
            }

            AnimateTires();
            if (Local && now >= _publishAt)
            {
                _publishAt = now + 0.066f;
                Publish();
            }
        }

        private void AnimateTires()
        {
            for (int i = 0; i < 4; i++)
            {
                var t = _tires[i];
                if (t == null)
                {
                    continue;
                }

                if (!Local)
                {
                    _spin[i] += Vector3.Dot(_netVel, transform.forward) / WheelRadius * Mathf.Rad2Deg * Time.deltaTime;
                }

                var steer = i < 2 ? Quaternion.AngleAxis(_steerAngle, Vector3.up) : Quaternion.identity;
                t.rotation = transform.rotation * steer * Quaternion.Euler(_spin[i] % 360f, 0f, 0f) * _tireRest[i];
            }
        }

        // ------------------------------------------------------------------ network

        /// <summary>
        /// The local buggy into the own presence record, vehicle ints 20-28 (RrPresenceWire): 20 seq, 21 stamp, 22-24
        /// position mm (room-local), 25 rotation (smallest three), 26 velocity x|y cm/s, 27 velocity z cm/s | steer,
        /// 28 flags: bit 0 in a buggy, 1-3 grid slot, 4 boosting, 5 handbrake, 6-7 cans.
        /// </summary>
        private void Publish()
        {
            var core = RrNet.Core;
            if (core == null)
            {
                return;
            }

            var own = core.Own;
            var room = transform.parent;
            var p = room != null ? room.InverseTransformPoint(transform.position) : transform.position;
            var r = room != null ? Quaternion.Inverse(room.rotation) * transform.rotation : transform.rotation;
            var v = _rb != null && !_rb.isKinematic ? _rb.linearVelocity : Vector3.zero;
            if (room != null)
            {
                v = Quaternion.Inverse(room.rotation) * v;
            }

            int at = RrPresenceWire.AtVehicle;
            own[at] = ++_seq;
            own[at + 1] = (int)(Time.realtimeSinceStartup * 1000f);
            RrWire.PutMm(own, at + 2, p);
            own[at + 5] = RrWire.PackRot(r);
            own[at + 6] = RrWire.Pair(RrWire.S16(v.x * 100.0), RrWire.S16(v.y * 100.0));
            own[at + 7] = RrWire.Pair(RrWire.S16(v.z * 100.0), RrWire.S16(_steerAngle * 100.0));
            own[at + 8] = 1 | ((Slot & 7) << 1) | (Boosting ? 1 << 4 : 0) | (Drift ? 1 << 5 : 0) | ((Cans & 3) << 6);
        }

        /// <summary>Takes the local buggy out of the presence record (ints 20-28 back to 0).</summary>
        public static void Unpublish()
        {
            var core = RrNet.Core;
            if (core == null)
            {
                return;
            }

            for (int i = 0; i < 9; i++)
            {
                core.Own[RrPresenceWire.AtVehicle + i] = 0;
            }
        }

        /// <summary>Whether a presence record carries a buggy, and its grid slot.</summary>
        public static bool InBuggy(int[] r, out int slot)
        {
            slot = 0;
            if (r == null || r.Length < RrPresenceWire.Ints)
            {
                return false;
            }

            int flags = r[RrPresenceWire.AtVehicle + 8];
            slot = (flags >> 1) & 7;
            return (flags & 1) != 0;
        }

        /// <summary>A remote buggy reads its owner's record (new sample when seq moved).</summary>
        public void Hear(int[] r)
        {
            int at = RrPresenceWire.AtVehicle;
            int seq = r[at];
            if (seq == _seenSeq)
            {
                return;
            }

            _seenSeq = seq;
            var room = transform.parent;
            var p = RrWire.GetMm(r, at + 2);
            var q = RrWire.UnpackRot(r[at + 5]);
            var v = new Vector3(RrWire.Lo(r[at + 6]) / 100f, RrWire.Hi(r[at + 6]) / 100f, RrWire.Lo(r[at + 7]) / 100f);
            _steerAngle = RrWire.Hi(r[at + 7]) / 100f;
            if (room != null)
            {
                p = room.TransformPoint(p);
                q = room.rotation * q;
                v = room.rotation * v;
            }

            bool first = _netAt < 0f;
            _netPos = p;
            _netRot = q;
            _netVel = v;
            _netAt = Time.time;
            LastHeard = Time.time;
            if (first)
            {
                transform.SetPositionAndRotation(p, q);
            }
        }

        private void Follow()
        {
            if (_netAt < 0f)
            {
                return;
            }

            float ahead = Mathf.Min(Time.time - _netAt, 0.5f);
            var target = _netPos + _netVel * ahead;
            float a = 1f - Mathf.Exp(-12f * Time.deltaTime);
            var pos = (target - transform.position).sqrMagnitude > 400f ? target : Vector3.Lerp(transform.position, target, a);
            var rot = Quaternion.Slerp(transform.rotation, _netRot, a);
            if (_rb != null)
            {
                _rb.MovePosition(pos);
                _rb.MoveRotation(rot);
            }

            transform.SetPositionAndRotation(pos, rot);
        }

        private void OnDestroy()
        {
            foreach (var s in new[] { _idle, _accel, _boostLoop })
            {
                if (s != null)
                {
                    RrSound.Stop(s);
                }
            }
        }
    }
}
