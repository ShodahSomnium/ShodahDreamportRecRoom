using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// The local hold of a synced prop (somnium_networking.md §5.5.3), pure so SP\nettest can race it. The hold is
    /// optimistic: the prop is in the hand at once and the prop object's state authority is asked for; when it
    /// arrives the holder writes Held. Fusion gives a contested object to the last request, so two players grabbing the
    /// same free ball both hold it for a moment; the one that ends without the authority, reading the other written as
    /// holder, lets go (and snaps to the network pose). No authority within Timeout (NetworkGrabbable's 2 s): let go.
    /// A holder that loses the authority with its own Held record still standing (an adoption race) asks it back after
    /// LostPatience, once a second.
    /// </summary>
    public sealed class RrGrabClaim
    {
        public const double Timeout = 2.0;
        public const double LostPatience = 0.5;
        public const double AskEvery = 1.0;

        public enum Stage
        {
            Idle,
            Pending,
            Holding
        }

        private double _since, _nextAsk, _lostAt = -1.0;
        private bool _askNow;

        public Stage Current { get; private set; }

        /// <summary>This step: ask for the prop object's state authority.</summary>
        public bool Ask { get; private set; }

        /// <summary>This step: the authority arrived; write state Held with this client as holder.</summary>
        public bool WriteHeld { get; private set; }

        /// <summary>This step: the hold was lost; drop the prop locally and follow the network.</summary>
        public bool Drop { get; private set; }

        /// <summary>Why the last drop happened (for the log).</summary>
        public string Why { get; private set; }

        public void Begin(double now)
        {
            Current = Stage.Pending;
            _since = now;
            _nextAsk = now + AskEvery;
            _lostAt = -1.0;
            _askNow = true;
        }

        public void End()
        {
            Current = Stage.Idle;
            _lostAt = -1.0;
        }

        /// <param name="haveAuthority">This client holds the prop object's state authority.</param>
        /// <param name="record">The prop object's ints as this client reads them.</param>
        public void Step(double now, bool haveAuthority, int[] record, int localId, IReadOnlyList<int> present)
        {
            Ask = _askNow && Current == Stage.Pending;
            _askNow = false;
            WriteHeld = false;
            Drop = false;

            if (Current == Stage.Idle)
            {
                Ask = false;
                return;
            }

            int state = RrPropWire.State(record);
            int writer = RrPropWire.Writer(record);
            bool otherHolds = state == RrPropWire.StateHeld && writer >= 0 && writer != localId && RrWire.Contains(present, writer);

            if (Current == Stage.Pending)
            {
                if (haveAuthority)
                {
                    Current = Stage.Holding;
                    WriteHeld = true;
                    _lostAt = -1.0;
                    return;
                }

                if (otherHolds)
                {
                    Lose("player " + writer + " holds it");
                    return;
                }

                if (now - _since >= Timeout)
                {
                    Lose("no authority in " + Timeout + " s");
                    return;
                }

                if (now >= _nextAsk)
                {
                    Ask = true;
                    _nextAsk = now + AskEvery;
                }

                return;
            }

            // Holding
            if (haveAuthority)
            {
                _lostAt = -1.0;

                if (state != RrPropWire.StateHeld || writer != localId)
                {
                    WriteHeld = true;
                }

                return;
            }

            if (otherHolds)
            {
                Lose("taken by player " + writer);
                return;
            }

            if (_lostAt < 0.0)
            {
                _lostAt = now;
                _nextAsk = now + LostPatience;
            }
            else if (now >= _nextAsk)
            {
                Ask = true;
                _nextAsk = now + AskEvery;
            }

            if (now - _lostAt >= Timeout)
            {
                Lose("authority lost for " + Timeout + " s");
            }
        }

        private void Lose(string why)
        {
            Current = Stage.Idle;
            Drop = true;
            Why = why;
            _lostAt = -1.0;
        }
    }
}
