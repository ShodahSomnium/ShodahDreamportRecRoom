using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Bowling
{
    /// <summary>
    /// One lane of Lone Shoe Lanes (level91): its ten pins on the level's own spots (Rec Room's (SandboxBowlingPin) at the
    /// sandbox scale 1.5 the level uses: 0.45 m between spots, head pin 24.35 m from the foul line), a sweeper bar where the
    /// level's pin blocker hangs, and the machine cycle (rr_sports.md §6.3):
    ///
    /// Ready (pins standing, kinematic on every client) -> a ball crossing the foul line down the lane, simulated on this
    /// client, starts a roll: this client runs the pins (dynamic) -> settle (pins still 0.5 s after the ball reached them,
    /// 5 s after it, or 14 s) -> the standing mask (a pin is up while tilted under 30 degrees, within 0.22 m of its spot) ->
    /// Cycle (3.2 s, every client: the bar drops, sweeps, the rack for the next roll is set) -> Ready.
    ///
    /// The scoring lane (x 0) is <see cref="Networked"/>: its state lives in the room's instance ints and the authority
    /// accepts the roll (event 93), takes the result (94) and scores it; the roller streams the pin poses at 10 Hz
    /// (event 95: 10 x position in mm from the head pin + rotation in bytes) and everyone else draws them. A practice lane
    /// is local on every client (research §9.5: "local physics only, no scoring"): first ball, then the standing pins for
    /// the second, then a new rack.
    /// </summary>
    public sealed class RrBowlingLane : MonoBehaviour
    {
        public const float CycleSeconds = 3.2f;
        public const float SettleTimeout = 14f;
        public const float PinFallAngle = 30f;
        public const float PinMoveLimit = 0.22f;
        public const float StreamEvery = 0.1f;
        public const float LaneHalfWidth = 0.888f;
        public const float PinY = 0.13f;
        public const float SweeperRestY = 0.947f;
        public const float SweeperLowY = 0.3f;

        public const int StateReady = 0;
        public const int StateRolling = 1;
        public const int StateCycle = 2;

        public RrBowlingRoom Room;
        public int Index;
        public bool Networked;

        /// <summary>Lane centre (room-local x), the foul line, the head pin row, the back wall of the pit.</summary>
        public float X;

        public float FoulZ = 7.85f;
        public float HeadZ = -16.501f;
        public float BackZ = -18.8f;

        public readonly Vector3[] Spots = new Vector3[10];
        public readonly List<RrBowlingBall> Balls = new List<RrBowlingBall>();

        /// <summary>Rolls this client simulated (log and harness).</summary>
        public int RollsSimulated;

        private sealed class Pin
        {
            public GameObject Go;
            public Rigidbody Body;
            public Vector3 Target;
            public Quaternion TargetRotation = Quaternion.identity;
            public bool Streamed;
        }

        private readonly Pin[] _pins = new Pin[10];
        private Transform _sweeper;

        // practice lanes: the state is ours
        private int _localState, _localStanding = RrBowlingScore.FullRack, _localRoll, _localFrameRoll;

        // this client runs the roll
        private bool _simulating;
        private RrBowlingBall _simBall;
        private float _simStart, _pinsHitAt, _stillSince, _ballStopSince, _streamAt;
        private byte _seq;
        private int _simRoll = -1;

        // requests to the authority, repeated until the ints move on
        private byte[] _startClaim, _resultClaim;
        private float _claimNext, _claimUntil;

        // every client: the cycle and the state it last saw
        private float _cycleStart = -1f;
        private bool _cycleHidden, _cyclePlaced;
        private int _seenState = -1, _seenRoll = -1, _clatterRoll = -1;

        public int LaneState
        {
            get { return Networked ? Room.LaneState : _localState; }
        }

        public int Standing
        {
            get { return Networked ? Room.Standing : _localStanding; }
        }

        public int RollId
        {
            get { return Networked ? Room.RollId : _localRoll; }
        }

        public bool Simulating
        {
            get { return _simulating; }
        }

        private Transform Frame
        {
            get { return Room.transform; }
        }

        // ------------------------------------------------------------------ build

        /// <summary>The ten spots: the level's pin markers of this lane when given, else the sandbox triangle at the head pin.</summary>
        public void SetSpots(List<Vector3> marked)
        {
            if (marked != null && marked.Count == 10)
            {
                // Pin 1 first, then row by row, left to right from the bowler (facing -z: left is +x).
                marked.Sort((a, b) => Mathf.Abs(a.z - b.z) > 0.05f ? b.z.CompareTo(a.z) : b.x.CompareTo(a.x));
                for (int i = 0; i < 10; i++)
                {
                    Spots[i] = new Vector3(marked[i].x, PinY, marked[i].z);
                }

                HeadZ = Spots[0].z;
                return;
            }

            int n = 0;
            for (int row = 0; row < 4; row++)
            {
                for (int k = 0; k <= row; k++)
                {
                    float x = X + (row * 0.5f - k) * 0.45f;
                    Spots[n++] = new Vector3(x, PinY, HeadZ - row * 0.3897f);
                }
            }
        }

        public void BuildSweeper()
        {
            // The lane object sits at the room origin, so lane-local is room-local.
            var bar = RrBuild.Box(transform, "Sweeper " + Index, new Vector3(X, SweeperRestY, HeadZ + 0.22f), new Vector3(LaneHalfWidth * 2f + 0.3f, 0.27f, 0.06f),
                RrLook.Solid(new Color(0.18f, 0.18f, 0.2f)), false);
            _sweeper = bar.transform;
        }

        public void AddPin(int i, GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            _pins[i] = new Pin { Go = go, Body = rb };
            Place(i, true);
        }

        public int PinCount
        {
            get
            {
                int n = 0;
                foreach (var p in _pins)
                {
                    n += p != null ? 1 : 0;
                }

                return n;
            }
        }

        private void Place(int i, bool up)
        {
            var p = _pins[i];
            if (p == null || p.Go == null)
            {
                return;
            }

            p.Streamed = false;
            if (!up)
            {
                p.Go.SetActive(false);
                return;
            }

            p.Body.isKinematic = true;
            p.Go.transform.SetPositionAndRotation(Frame.TransformPoint(Spots[i]), Frame.rotation);
            p.Go.SetActive(true);
        }

        /// <summary>The rack for a standing mask: those pins upright on their spots (kinematic), the others away.</summary>
        public void PlaceRack(int mask)
        {
            for (int i = 0; i < 10; i++)
            {
                Place(i, ((mask >> i) & 1) != 0);
            }
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            if (Room == null || !Room.Built)
            {
                return;
            }

            int state = LaneState;
            int roll = RollId;
            if (state != _seenState || roll != _seenRoll)
            {
                StateChanged(state);
                _seenState = state;
                _seenRoll = roll;
            }

            switch (state)
            {
                case StateReady:
                    if (_simulating)
                    {
                        // Networked: waiting for the authority to take the roll; given up after 1.5 s.
                        if (Networked && Time.time - _simStart > 1.5f)
                        {
                            RrLog.Info("Bowling", "lane " + Index + ": the roll was not taken by the authority (not your turn, or the lane was busy)");
                            Abort();
                        }
                        else
                        {
                            Simulate();
                        }
                    }
                    else
                    {
                        DetectRoll();
                    }

                    break;
                case StateRolling:
                    if (_simulating)
                    {
                        Simulate();
                    }
                    else
                    {
                        DrawStream();
                    }

                    break;
                case StateCycle:
                    CycleStep();
                    break;
            }

            Claims();
            if (!Networked)
            {
                ReturnLocalBalls();
            }
        }

        private void StateChanged(int now)
        {
            switch (now)
            {
                case StateReady:
                    _cycleStart = -1f;
                    SetSweeper(SweeperRestY, HeadZ + 0.22f);
                    if (!_simulating)
                    {
                        PlaceRack(Standing);
                    }

                    break;
                case StateRolling:
                    if (_simulating && Networked && Room.RollerId != RrNet.LocalId)
                    {
                        Abort();
                    }
                    else if (_simulating)
                    {
                        _simRoll = RollId;
                    }
                    else
                    {
                        // Someone else runs the pins: ours follow the stream.
                        foreach (var p in _pins)
                        {
                            if (p != null && p.Go != null && p.Go.activeSelf)
                            {
                                p.Body.isKinematic = true;
                                p.Target = p.Go.transform.position;
                                p.TargetRotation = p.Go.transform.rotation;
                            }
                        }
                    }

                    break;
                case StateCycle:
                    _simulating = false;
                    _cycleStart = Time.time;
                    _cycleHidden = false;
                    _cyclePlaced = false;
                    foreach (var p in _pins)
                    {
                        if (p != null && p.Go != null && p.Go.activeSelf)
                        {
                            p.Body.isKinematic = true;
                        }
                    }

                    RrSound.Play("Bowling_Sweeper01", Frame.TransformPoint(new Vector3(X, 0.8f, HeadZ - 0.6f)), 0.8f, 1f, 30f);
                    break;
            }
        }

        // ------------------------------------------------------------------ the roll (this client)

        private void DetectRoll()
        {
            foreach (var ball in Room.Balls)
            {
                if (ball == null || ball.Body == null || ball.Body.isKinematic || ball.HeldByAnyone || !ball.LocallySimulated || (Networked && ball.Net == null))
                {
                    continue;
                }

                Vector3 p = Frame.InverseTransformPoint(ball.transform.position);
                Vector3 v = Frame.InverseTransformDirection(ball.Body.linearVelocity);
                if (Mathf.Abs(p.x - X) < LaneHalfWidth + 0.4f && p.z < FoulZ + 0.3f && p.z > FoulZ - 5f && p.y < 1.5f && v.z < -0.7f)
                {
                    StartRoll(ball);
                    return;
                }
            }
        }

        private void StartRoll(RrBowlingBall ball)
        {
            _simulating = true;
            _simBall = ball;
            _simStart = Time.time;
            _pinsHitAt = -1f;
            _stillSince = -1f;
            _ballStopSince = -1f;
            _streamAt = 0f;
            _seq = 0;
            foreach (var p in _pins)
            {
                if (p != null && p.Go != null && p.Go.activeSelf)
                {
                    p.Streamed = false;
                    p.Body.isKinematic = false;
                    p.Body.WakeUp();
                }
            }

            RollsSimulated++;
            if (Networked)
            {
                int r = RollId;
                _simRoll = r + 1;
                _startClaim = new[] { (byte)(r & 0xFF), (byte)((r >> 8) & 0xFF) };
                _claimNext = Time.unscaledTime + 0.25f;
                _claimUntil = Time.unscaledTime + 1.5f;
                RrNet.Send(RrBowlingRoom.EvRollStart, _startClaim, true);
            }
            else
            {
                _localRoll++;
                _simRoll = _localRoll;
                _localState = StateRolling;
                _seenState = StateRolling;
                _seenRoll = _localRoll;
            }

            if (RollsSimulated <= 2)
            {
                RrLog.Info("Bowling", "lane " + Index + (Networked ? " (scoring)" : " (practice)") + ": roll started by ball " + ball.Index + " at " + ball.Body.linearVelocity.magnitude.ToString("F1") + " m/s, "
                    + RrBowlingScore.Count(Standing) + " pins up");
            }
        }

        private void Abort()
        {
            _simulating = false;
            _startClaim = null;
            PlaceRack(Standing);
        }

        private void Simulate()
        {
            float now = Time.time;
            if (Networked && LaneState == StateRolling && now >= _streamAt)
            {
                _streamAt = now + StreamEvery;
                SendPins();
            }

            bool ballGone = _simBall == null || !_simBall.gameObject.activeInHierarchy || _simBall.HeldByAnyone;
            Vector3 bp = ballGone ? Vector3.zero : Frame.InverseTransformPoint(_simBall.transform.position);
            float ballSpeed = ballGone ? 0f : _simBall.SeenVelocity.magnitude;
            if (_pinsHitAt < 0f && ((!ballGone && bp.z < HeadZ + 0.35f && Mathf.Abs(bp.x - X) < 1.2f) || AnyPinMoving(0.3f)))
            {
                _pinsHitAt = now;
                RrSound.Play("SFX_Collision_Wood1_Mono", Frame.TransformPoint(Spots[0]), Mathf.Clamp01(0.4f + ballSpeed / 10f), 1f, 35f);
            }

            if (ballGone || ballSpeed < 0.15f || bp.y < -0.5f || bp.z < BackZ + 1.2f)
            {
                if (_ballStopSince < 0f)
                {
                    _ballStopSince = now;
                }
            }
            else
            {
                _ballStopSince = -1f;
            }

            if (AnyPinMoving(0.08f))
            {
                _stillSince = -1f;
            }
            else if (_stillSince < 0f)
            {
                _stillSince = now;
            }

            bool settled = now - _simStart > SettleTimeout
                || (_pinsHitAt > 0f && now - _pinsHitAt > 1.2f && _stillSince > 0f && now - _stillSince > 0.5f)
                || (_pinsHitAt > 0f && now - _pinsHitAt > 5f)
                || (_pinsHitAt < 0f && _ballStopSince > 0f && now - _ballStopSince > 1.5f);
            if (settled && (!Networked || LaneState == StateRolling))
            {
                Finish();
            }
        }

        private bool AnyPinMoving(float speed)
        {
            foreach (var p in _pins)
            {
                if (p == null || p.Go == null || !p.Go.activeSelf || p.Body.isKinematic)
                {
                    continue;
                }

                if (p.Body.linearVelocity.magnitude > speed || p.Body.angularVelocity.magnitude > speed * 6f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool PinUp(int i)
        {
            var p = _pins[i];
            if (p == null || p.Go == null || !p.Go.activeSelf)
            {
                return false;
            }

            Vector3 lp = Frame.InverseTransformPoint(p.Go.transform.position);
            Vector3 d = lp - Spots[i];
            float tilt = Vector3.Angle(p.Go.transform.up, Frame.up);
            return tilt < PinFallAngle && new Vector2(d.x, d.z).magnitude < PinMoveLimit && Mathf.Abs(d.y) < 0.15f;
        }

        private void Finish()
        {
            int before = Standing;
            int mask = 0;
            for (int i = 0; i < 10; i++)
            {
                if (((before >> i) & 1) != 0 && PinUp(i))
                {
                    mask |= 1 << i;
                }
            }

            _simulating = false;
            int knocked = RrBowlingScore.Count(before) - RrBowlingScore.Count(mask);
            if (RollsSimulated <= 3)
            {
                RrLog.Info("Bowling", "lane " + Index + ": pins settled " + (Time.time - _simStart).ToString("F1") + " s after the roll: " + knocked + " down, " + RrBowlingScore.Count(mask) + " standing");
            }

            if (Networked)
            {
                SendPins();
                int r = _simRoll;
                _resultClaim = new[] { (byte)(r & 0xFF), (byte)((r >> 8) & 0xFF), (byte)(mask & 0xFF), (byte)((mask >> 8) & 0x3) };
                _claimNext = Time.unscaledTime + 0.3f;
                _claimUntil = Time.unscaledTime + 3f;
                RrNet.Send(RrBowlingRoom.EvRollResult, _resultClaim, true);
                return;
            }

            // Practice: first ball, then what is standing, then a new rack.
            bool strike = before == RrBowlingScore.FullRack && mask == 0;
            bool spare = !strike && mask == 0;
            if (_localFrameRoll == 0 && mask != 0)
            {
                _localFrameRoll = 1;
                _localStanding = mask;
            }
            else
            {
                _localFrameRoll = 0;
                _localStanding = RrBowlingScore.FullRack;
            }

            _localState = StateCycle;
            Room.RollSounds(this, knocked, strike, spare);
        }

        private void Claims()
        {
            float now = Time.unscaledTime;
            if (_startClaim != null)
            {
                if (LaneState != StateReady || now > _claimUntil || !_simulating)
                {
                    _startClaim = null;
                }
                else if (now >= _claimNext)
                {
                    _claimNext = now + 0.25f;
                    RrNet.Send(RrBowlingRoom.EvRollStart, _startClaim, true);
                }
            }

            if (_resultClaim != null)
            {
                if (LaneState != StateRolling || (RollId & 0xFFFF) != (_resultClaim[0] | (_resultClaim[1] << 8)) || now > _claimUntil)
                {
                    _resultClaim = null;
                }
                else if (now >= _claimNext)
                {
                    _claimNext = now + 0.3f;
                    RrNet.Send(RrBowlingRoom.EvRollResult, _resultClaim, true);
                }
            }
        }

        // ------------------------------------------------------------------ the pin stream

        private void SendPins()
        {
            var body = new byte[4 + 10 * 10];
            int r = RollId;
            body[0] = (byte)(r & 0xFF);
            body[1] = (byte)((r >> 8) & 0xFF);
            body[2] = _seq++;
            body[3] = 10;
            Vector3 origin = Spots[0];
            for (int i = 0; i < 10; i++)
            {
                int o = 4 + i * 10;
                var p = _pins[i];
                Vector3 lp = p != null && p.Go != null ? Frame.InverseTransformPoint(p.Go.transform.position) - origin : Vector3.zero;
                Quaternion lr = p != null && p.Go != null ? Quaternion.Inverse(Frame.rotation) * p.Go.transform.rotation : Quaternion.identity;
                PutShort(body, o, lp.x);
                PutShort(body, o + 2, lp.y);
                PutShort(body, o + 4, lp.z);
                body[o + 6] = (byte)(sbyte)Mathf.RoundToInt(Mathf.Clamp(lr.x, -1f, 1f) * 127f);
                body[o + 7] = (byte)(sbyte)Mathf.RoundToInt(Mathf.Clamp(lr.y, -1f, 1f) * 127f);
                body[o + 8] = (byte)(sbyte)Mathf.RoundToInt(Mathf.Clamp(lr.z, -1f, 1f) * 127f);
                body[o + 9] = (byte)(sbyte)Mathf.RoundToInt(Mathf.Clamp(lr.w, -1f, 1f) * 127f);
            }

            RrNet.Send(RrBowlingRoom.EvPins, body);
        }

        private static void PutShort(byte[] b, int o, float metres)
        {
            int mm = Mathf.Clamp(Mathf.RoundToInt(metres * 1000f), short.MinValue, short.MaxValue);
            b[o] = (byte)(mm & 0xFF);
            b[o + 1] = (byte)((mm >> 8) & 0xFF);
        }

        private static float GetShort(byte[] b, int o)
        {
            return (short)(b[o] | (b[o + 1] << 8)) / 1000f;
        }

        /// <summary>Everyone but the roller: the roller's pin poses for the current roll.</summary>
        public void OnPins(int sender, byte[] body)
        {
            if (!Networked || _simulating || sender == RrNet.LocalId || body == null || body.Length < 4 + 10 * 10 || LaneState != StateRolling)
            {
                return;
            }

            int roll = body[0] | (body[1] << 8);
            if (roll != (RollId & 0xFFFF))
            {
                return;
            }

            Vector3 origin = Spots[0];
            for (int i = 0; i < 10; i++)
            {
                var p = _pins[i];
                if (p == null || p.Go == null || !p.Go.activeSelf)
                {
                    continue;
                }

                int o = 4 + i * 10;
                Vector3 lp = new Vector3(GetShort(body, o), GetShort(body, o + 2), GetShort(body, o + 4)) + origin;
                var q = new Quaternion((sbyte)body[o + 6] / 127f, (sbyte)body[o + 7] / 127f, (sbyte)body[o + 8] / 127f, (sbyte)body[o + 9] / 127f);
                float len = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                if (len < 0.5f)
                {
                    continue;
                }

                q = new Quaternion(q.x / len, q.y / len, q.z / len, q.w / len);
                p.Body.isKinematic = true;
                p.Target = Frame.TransformPoint(lp);
                p.TargetRotation = Frame.rotation * q;
                p.Streamed = true;
            }
        }

        private void DrawStream()
        {
            // The pin clatter where the roller's ball meets the rack, once per roll (the roller plays its own).
            if (_clatterRoll != RollId)
            {
                foreach (var ball in Balls)
                {
                    if (ball == null)
                    {
                        continue;
                    }

                    Vector3 bp = Frame.InverseTransformPoint(ball.transform.position);
                    if (bp.z < HeadZ + 0.35f && bp.z > BackZ && Mathf.Abs(bp.x - X) < 1.2f && bp.y < 0.6f)
                    {
                        _clatterRoll = RollId;
                        RrSound.Play("SFX_Collision_Wood1_Mono", Frame.TransformPoint(Spots[0]), Mathf.Clamp01(0.4f + ball.SeenVelocity.magnitude / 10f), 1f, 35f);
                        break;
                    }
                }
            }

            float k = 1f - Mathf.Exp(-18f * Time.deltaTime);
            foreach (var p in _pins)
            {
                if (p == null || p.Go == null || !p.Streamed || !p.Go.activeSelf)
                {
                    continue;
                }

                var t = p.Go.transform;
                t.SetPositionAndRotation(Vector3.Lerp(t.position, p.Target, k), Quaternion.Slerp(t.rotation, p.TargetRotation, k));
            }
        }

        // ------------------------------------------------------------------ the machine cycle (every client)

        private void CycleStep()
        {
            if (_cycleStart < 0f)
            {
                _cycleStart = Time.time;
            }

            float t = Time.time - _cycleStart;
            float sweepZ = HeadZ + 0.22f;
            if (t < 0.4f)
            {
                SetSweeper(Mathf.Lerp(SweeperRestY, SweeperLowY, t / 0.4f), sweepZ);
            }
            else if (t < 1.2f)
            {
                SetSweeper(SweeperLowY, Mathf.Lerp(sweepZ, BackZ + 0.3f, (t - 0.4f) / 0.8f));
            }
            else if (t < 1.8f)
            {
                SetSweeper(SweeperLowY, Mathf.Lerp(BackZ + 0.3f, sweepZ, (t - 1.2f) / 0.6f));
            }
            else if (t < 2.2f)
            {
                SetSweeper(Mathf.Lerp(SweeperLowY, SweeperRestY, (t - 1.8f) / 0.4f), sweepZ);
            }
            else
            {
                SetSweeper(SweeperRestY, sweepZ);
            }

            if (!_cycleHidden && t >= 0.9f)
            {
                _cycleHidden = true;
                PlaceRack(0);
            }

            if (!_cyclePlaced && t >= 2.2f)
            {
                _cyclePlaced = true;
                PlaceRack(Standing);
            }

            if (!Networked && t >= CycleSeconds)
            {
                _localState = StateReady;
            }
        }

        private void SetSweeper(float y, float z)
        {
            if (_sweeper != null)
            {
                _sweeper.localPosition = new Vector3(X, y, z);
            }
        }

        // ------------------------------------------------------------------ practice balls (local)

        private void ReturnLocalBalls()
        {
            if (LaneState == StateRolling || _simulating)
            {
                return;
            }

            float now = Time.time;
            foreach (var ball in Balls)
            {
                if (ball == null || ball.Net != null)
                {
                    continue;
                }

                if (Room.ShouldReturn(ball, this))
                {
                    if (ball.ReturnSince < 0f)
                    {
                        ball.ReturnSince = now;
                    }
                    else if (now - ball.ReturnSince > 2f)
                    {
                        ball.ReturnHome();
                    }
                }
                else
                {
                    ball.ReturnSince = -1f;
                }
            }
        }
    }
}
