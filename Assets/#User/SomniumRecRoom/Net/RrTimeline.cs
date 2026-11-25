using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>One motion sample off the network: a prop, a kart or an AI actor as its writer last described it.</summary>
    public struct RrNetState
    {
        /// <summary>The writer's sample key (dedup).</summary>
        public int Seq;

        /// <summary>The writer's clock in ms (Time.fixedTimeAsDouble): samples are placed on it, not on arrival.</summary>
        public int StampMs;

        /// <summary>Bumped on a reset, a throw, a new writer: the timeline snaps instead of sliding.</summary>
        public int Epoch;

        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
    }

    /// <summary>
    /// The receiving end of a moving thing, drawn a beat behind so there is always a real sample either side of the
    /// moment shown: ETS's EtsNetTimeline (EtsNetWire.cs:221-481) with Rec Room's sample type. Measured with ETS in
    /// two-player runs: drawn 117-228 ms behind, about 135 ms typically (DESIGN.md F11). What it does beyond a
    /// two-sample lerp:
    ///   - samples are placed on the WRITER's clock (the stamp), not on when they were noticed here, so network
    ///     jitter does not become position jitter;
    ///   - the render lag is steered gently toward "least-delayed recent arrival + recent spread + 1.1 send
    ///     intervals + 5 ms" (clamped 0.03..0.4 s, at most 0.05 s per s), never stepped;
    ///   - positions follow a cubic Hermite through the samples' velocities (a thrown ball's arc is a curve, not
    ///     chords), rotations an Nlerp;
    ///   - past the newest sample it carries on along the velocity for up to 0.25 s, then holds;
    ///   - LeadIn: a thing that first appears moving (a ball just thrown) is shown back along its own velocity, so it
    ///     arrives already moving instead of standing for the drawing delay and then jumping (ETS measured a 23 m/s
    ///     jump per new car without it).
    /// A new epoch, or a jump over 50 m between two samples, is snapped to. Pure: no Fusion, no scene, no engine calls.
    /// </summary>
    public sealed class RrTimeline
    {
        public struct Entry
        {
            public double Time;
            public RrNetState State;
        }

        private const int Capacity = 64;
        private const int LagCapacity = 64;
        private const double LagWindow = 2.5;
        private const double Slew = 0.05;
        private const float JumpMetres = 50f;

        private readonly Entry[] _samples = new Entry[Capacity];
        private int _count;
        private int _lastSeq = int.MinValue;
        private int _lastEpoch = -1;

        private readonly double[] _arrivals = new double[LagCapacity];
        private readonly double[] _lags = new double[LagCapacity];
        private int _lagCount;
        private int _lagNext;

        private double _renderLag = double.NaN;
        private double _lastNow = double.NaN;
        private long _stampBase;
        private int _lastStamp;
        private bool _haveStamp;
        private bool _snapPending;

        public float MinDelay = 0.03f;
        public float MaxDelay = 0.4f;
        public float MaxExtrapolation = 0.25f;
        public bool LeadIn;
        public float MaxLeadIn = 0.5f;

        public int Count
        {
            get { return _count; }
        }

        /// <summary>How far behind the newest data the thing is drawn, seconds.</summary>
        public float Delay { get; private set; }

        /// <summary>True for the one Sample() result that followed an epoch change or a jump.</summary>
        public bool WasSnap { get; private set; }

        /// <summary>The newest sample added (default until one is).</summary>
        public RrNetState Newest
        {
            get { return _count > 0 ? _samples[_count - 1].State : default; }
        }

        public void Clear()
        {
            _count = 0;
            _lagCount = 0;
            _lagNext = 0;
            _renderLag = double.NaN;
            _lastEpoch = -1;

            // A cleared timeline takes the state it is handed next even if it is the one it had (ETS: a watcher that
            // went local and back re-reads an unchanged record).
            _lastSeq = int.MinValue;
        }

        /// <summary>A state read off the network at local time now. False if it was not new.</summary>
        public bool Add(RrNetState state, double now)
        {
            if (state.Seq == _lastSeq)
            {
                return false;
            }

            _lastSeq = state.Seq;
            double stamp = Unwrap(state.StampMs);

            if (state.Epoch != _lastEpoch)
            {
                _lastEpoch = state.Epoch;
                _count = 0;
                _lagCount = 0;
                _lagNext = 0;
                _renderLag = double.NaN;
                _snapPending = true;
            }

            if (_count > 0 && stamp <= _samples[_count - 1].Time)
            {
                return false;
            }

            if (_count > 0 && (state.Position - _samples[_count - 1].State.Position).sqrMagnitude > JumpMetres * JumpMetres)
            {
                _count = 0;
                _snapPending = true;
            }

            _arrivals[_lagNext] = now;
            _lags[_lagNext] = now - stamp;
            _lagNext = (_lagNext + 1) % LagCapacity;
            _lagCount = Math.Min(_lagCount + 1, LagCapacity);

            if (_count == Capacity)
            {
                Array.Copy(_samples, 1, _samples, 0, Capacity - 1);
                _count--;
            }

            _samples[_count++] = new Entry { Time = stamp, State = state };
            return true;
        }

        /// <summary>The state at local time now. False until there is anything to show.</summary>
        public bool Sample(double now, out RrNetState state)
        {
            state = default;

            if (_count == 0)
            {
                return false;
            }

            double best = double.MaxValue, worst = double.MinValue;

            for (int i = 0; i < _lagCount; i++)
            {
                if (now - _arrivals[i] > LagWindow)
                {
                    continue;
                }

                best = Math.Min(best, _lags[i]);
                worst = Math.Max(worst, _lags[i]);
            }

            if (best == double.MaxValue)
            {
                best = worst = _lags[(_lagNext + LagCapacity - 1) % LagCapacity];
            }

            double interval = _count > 1 ? (_samples[_count - 1].Time - _samples[Math.Max(0, _count - 9)].Time) / Math.Min(8, _count - 1) : 0.05;
            double delay = Clamp(worst - best + interval * 1.1 + 0.005, MinDelay, MaxDelay);
            double target = best + delay;
            double step = double.IsNaN(_lastNow) ? 0.0 : Math.Max(0.0, now - _lastNow);
            _lastNow = now;

            if (double.IsNaN(_renderLag) || Math.Abs(target - _renderLag) > 0.5)
            {
                _renderLag = target;
            }
            else
            {
                double most = Slew * step;
                _renderLag += Clamp(target - _renderLag, -most, most);
            }

            Delay = (float)(_renderLag - best);
            double t = now - _renderLag;
            WasSnap = _snapPending;
            _snapPending = false;

            if (_count == 1 || t <= _samples[0].Time)
            {
                state = _samples[0].State;

                if (LeadIn)
                {
                    double lead = Clamp(t - _samples[0].Time, -MaxLeadIn, MaxExtrapolation);
                    state.Position += state.Velocity * (float)lead;
                }

                return true;
            }

            Entry newest = _samples[_count - 1];

            if (t >= newest.Time)
            {
                float ahead = (float)Math.Min(t - newest.Time, MaxExtrapolation);
                state = newest.State;
                state.Position += newest.State.Velocity * ahead;
                return true;
            }

            int b = 1;

            while (b < _count - 1 && _samples[b].Time < t)
            {
                b++;
            }

            Entry from = _samples[b - 1], to = _samples[b];
            float span = (float)(to.Time - from.Time);
            float u = span > 1e-4f ? (float)((t - from.Time) / span) : 1f;
            u = u < 0f ? 0f : u > 1f ? 1f : u;

            state = u < 0.5f ? from.State : to.State;
            state.Position = RrWire.Hermite(from.State.Position, from.State.Velocity, to.State.Position, to.State.Velocity, span, u);
            state.Rotation = RrWire.Nlerp(from.State.Rotation, to.State.Rotation, u);
            state.Velocity = RrWire.Lerp(from.State.Velocity, to.State.Velocity, u);
            state.AngularVelocity = RrWire.Lerp(from.State.AngularVelocity, to.State.AngularVelocity, u);

            if (b > 2)
            {
                int drop = b - 2;
                Array.Copy(_samples, drop, _samples, 0, _count - drop);
                _count -= drop;
            }

            return true;
        }

        private static double Clamp(double value, double low, double high)
        {
            return value < low ? low : value > high ? high : value;
        }

        /// <summary>The writer's millisecond clock, carried past int wrap-round (24 days).</summary>
        private double Unwrap(int stampMs)
        {
            if (_haveStamp && stampMs < _lastStamp && (long)_lastStamp - stampMs > int.MaxValue / 2)
            {
                _stampBase += 1L << 32;
            }

            _haveStamp = true;
            _lastStamp = stampMs;
            return (_stampBase + (uint)stampMs) / 1000.0;
        }
    }
}
