using System;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// An instance authority leasing one scene object (a "Rr Net Prop p" for a ball, a "Rr Net Actors a" for a block
    /// of quest enemies) for its instance: a shorter cousin of <see cref="RrSlotClaim"/>. A lease is a record in the
    /// object naming the instance (object k and its generation) and the thing's own number within it; everyone finds
    /// the object by scanning for that record, so once written nobody has to "own" it (a ball's writer changes with
    /// every throw). Two authorities of different instances leasing the same free object at once: Fusion gives the
    /// last request the authority and drops the other's write, the loser reads a lease that is not its own and tries
    /// the next free object. Pure; the caller asks for authority on AskAuthority and writes the lease on WriteLease.
    /// </summary>
    public sealed class RrLease
    {
        public const double AuthorityWait = 3.0;
        public const double ConfirmAfter = 0.5;
        public const double AskEvery = 1.0;
        public const double RoundPause = 3.0;

        public enum Stage
        {
            Idle,
            Asking,
            Confirming,
            Done
        }

        private bool[] _tried = new bool[0];
        private double _since, _nextAsk, _roundAt = double.NegativeInfinity;
        private string _note;

        public Stage Current { get; private set; }

        /// <summary>The object being leased (or leased), -1 when none.</summary>
        public int Object { get; private set; } = -1;

        public int AskAuthority { get; private set; } = -1;

        public bool WriteLease { get; private set; }

        public string TakeNote()
        {
            string note = _note;
            _note = null;
            return note;
        }

        public void Reset()
        {
            Current = Stage.Idle;
            Object = -1;
            Array.Clear(_tried, 0, _tried.Length);
            _roundAt = double.NegativeInfinity;
        }

        /// <param name="matches">Per object: its record is this lease (this instance and this thing's number).</param>
        public void Step(double now, int start, int count, bool[] valid, bool[] authority, bool[] free, bool[] matches)
        {
            AskAuthority = -1;
            WriteLease = false;
            count = Math.Min(count, Math.Min(valid.Length, Math.Min(free.Length, matches.Length)));

            if (_tried.Length < count)
            {
                _tried = new bool[count];
            }

            if (Object >= count)
            {
                Reset();
            }

            switch (Current)
            {
                case Stage.Idle:
                {
                    if (now < _roundAt)
                    {
                        return;
                    }

                    int k = -1;
                    int k0 = count > 0 ? ((start % count) + count) % count : 0;

                    for (int i = 0; i < count; i++)
                    {
                        int j = (k0 + i) % count;

                        if (valid[j] && free[j] && !_tried[j])
                        {
                            k = j;
                            break;
                        }
                    }

                    if (k < 0)
                    {
                        Array.Clear(_tried, 0, _tried.Length);
                        _roundAt = now + RoundPause;
                        _note = "no free object to lease (" + count + ")";
                        return;
                    }

                    Object = k;
                    Current = Stage.Asking;
                    _since = now;
                    _nextAsk = now + AskEvery;
                    AskAuthority = k;
                    return;
                }

                case Stage.Asking:
                {
                    if (matches[Object])
                    {
                        Current = Stage.Done;
                        return;
                    }

                    if (!valid[Object] || !free[Object])
                    {
                        Give("object " + Object + " was leased by another instance first");
                        return;
                    }

                    if (authority[Object])
                    {
                        WriteLease = true;
                        Current = Stage.Confirming;
                        _since = now;
                        return;
                    }

                    if (now - _since >= AuthorityWait)
                    {
                        Give("no authority over object " + Object + " in " + AuthorityWait + " s");
                        return;
                    }

                    if (now >= _nextAsk)
                    {
                        AskAuthority = Object;
                        _nextAsk = now + AskEvery;
                    }

                    return;
                }

                case Stage.Confirming:
                {
                    if (now - _since < ConfirmAfter)
                    {
                        return;
                    }

                    if (matches[Object])
                    {
                        Current = Stage.Done;
                        Array.Clear(_tried, 0, _tried.Length);
                        return;
                    }

                    Give("lease of object " + Object + " lost to another instance");
                    return;
                }
            }
        }

        private void Give(string why)
        {
            if (Object >= 0 && Object < _tried.Length)
            {
                _tried[Object] = true;
            }

            Current = Stage.Idle;
            Object = -1;
            _note = why;
        }
    }
}
