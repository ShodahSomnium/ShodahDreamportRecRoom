using System;
using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// This client's claim on one of a set of scene objects: ETS's EtsSlotClaim (EtsPlayerWire.cs:1254-1560), made
    /// generic so the same proven state machine claims both a presence slot ("Rr Net Player s") and, for an elected
    /// instance authority, a free instance object ("Rr Net Instance k"). Pure: the caller feeds it what the room says
    /// (per object: valid, this client holds its authority, free for this client, the PlayerId its record names) and
    /// carries out what it asks (RequestStateAuthority on AskAuthority; write the claim record on WriteClaim). ETS's
    /// probe 2200 ran it against a simulated Fusion ("last authority request wins, writes without authority dropped,
    /// a delay on everything") through joins, leaves, simultaneous claims and a master hand-over; SP\nettest does the
    /// same for this port.
    ///
    ///   Idle -> Asking: the first free object from Start on, not tried in this round.
    ///   Asking -> Confirming: authority came within AuthorityWait; the claim record is written.
    ///   Confirming -> Owned: ConfirmAfter later the record still names this client and it still holds authority.
    ///     Another present player named there (two claimed at once; Fusion gave the last request the authority and
    ///     dropped the other's writes) sends it on: "object k lost to player N". Authority lost while the record still
    ///     names this client is asked back after LostPatience (checker probe 2215's case).
    ///   Owned: authority lost for LostPatience with the record still naming this client is asked back once a second;
    ///     another present player named there means it was taken over and the claim starts again; read free while
    ///     holding authority, it is claimed afresh (WriteClaim again).
    /// Nothing is sent on leaving: an object whose named owner is not in the room is free (FreeFor rules).
    /// </summary>
    public sealed class RrSlotClaim
    {
        public const double AuthorityWait = 3.0;
        public const double ConfirmAfter = 1.0;
        public const double AskEvery = 1.0;
        public const double LostPatience = 1.5;
        public const double RoundPause = 5.0;

        public enum Stage
        {
            Idle,
            Asking,
            Confirming,
            Owned
        }

        public struct Inputs
        {
            public double Now;
            public int LocalId;

            /// <summary>The room is up and the objects are found (and the join snapshot has had time to arrive).</summary>
            public bool Allowed;

            /// <summary>How many objects there are.</summary>
            public int Count;

            /// <summary>Where the search for a free object starts (LocalId % count for slots; a key hash for instances).</summary>
            public int Start;
        }

        private readonly string _what;
        private bool[] _tried = new bool[0];
        private double _since, _nextAsk, _lostAt = -1.0, _roundAt = double.NegativeInfinity, _began;
        private int _localId = -1;
        private string _note;

        public RrSlotClaim(string what)
        {
            _what = what ?? "object";
        }

        public Stage Current { get; private set; }

        /// <summary>The object being claimed or held, -1 when none.</summary>
        public int Slot { get; private set; } = -1;

        /// <summary>This step: ask for this object's state authority (-1: nothing to ask).</summary>
        public int AskAuthority { get; private set; } = -1;

        /// <summary>This step: write the claim record into Slot.</summary>
        public bool WriteClaim { get; private set; }

        /// <summary>The object may be written this step (with authority): being confirmed or owned.</summary>
        public bool Holding
        {
            get { return Slot >= 0 && (Current == Stage.Confirming || Current == Stage.Owned); }
        }

        /// <summary>Time from the first ask to Owned, seconds (for the log).</summary>
        public double ClaimSeconds { get; private set; }

        /// <summary>The last thing worth a log line, once (null when nothing happened).</summary>
        public string TakeNote()
        {
            string note = _note;
            _note = null;
            return note;
        }

        public void Reset()
        {
            Current = Stage.Idle;
            Slot = -1;
            _lostAt = -1.0;
            Array.Clear(_tried, 0, _tried.Length);
            _roundAt = double.NegativeInfinity;
        }

        /// <param name="owner">Per object: the PlayerId its record names (-1 none).</param>
        /// <param name="free">Per object: this client may claim it now.</param>
        public void Step(in Inputs input, bool[] valid, bool[] authority, bool[] free, int[] owner, IReadOnlyList<int> present)
        {
            AskAuthority = -1;
            WriteClaim = false;

            if (!input.Allowed || input.LocalId < 0 || input.LocalId != _localId)
            {
                if (Current != Stage.Idle || Slot >= 0)
                {
                    _note = _what + " claim dropped (" + (!input.Allowed ? "the room is not up" : "a new local player id") + ")";
                }

                Reset();
                _localId = input.LocalId;

                if (!input.Allowed || input.LocalId < 0)
                {
                    return;
                }
            }

            double now = input.Now;
            int count = Math.Min(Math.Min(input.Count, valid.Length), Math.Min(free.Length, owner.Length));

            if (_tried.Length < count)
            {
                _tried = new bool[count];
            }

            if (Slot >= count)
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

                    int k = FindFree(input.Start, count, valid, free);

                    if (k < 0)
                    {
                        bool any = false;

                        for (int i = 0; i < count; i++)
                        {
                            any |= _tried[i];
                        }

                        Array.Clear(_tried, 0, _tried.Length);
                        _roundAt = now + RoundPause;
                        _note = any ? "no " + _what + " could be claimed this round; trying all again in " + RoundPause + " s" : "no free " + _what + " (" + count + ")";
                        return;
                    }

                    Slot = k;
                    Current = Stage.Asking;
                    _since = now;
                    _began = now;
                    _nextAsk = now + AskEvery;
                    AskAuthority = k;
                    return;
                }

                case Stage.Asking:
                {
                    if (!valid[Slot] || !free[Slot])
                    {
                        Give(_what + " " + Slot + " taken by player " + owner[Slot] + " before the claim; trying the next");
                        return;
                    }

                    if (authority[Slot])
                    {
                        WriteClaim = true;
                        Current = Stage.Confirming;
                        _since = now;
                        return;
                    }

                    if (now - _since >= AuthorityWait)
                    {
                        Give("no authority over " + _what + " " + Slot + " in " + AuthorityWait + " s; trying the next");
                        return;
                    }

                    if (now >= _nextAsk)
                    {
                        AskAuthority = Slot;
                        _nextAsk = now + AskEvery;
                    }

                    return;
                }

                case Stage.Confirming:
                {
                    int named = owner[Slot];

                    if (named >= 0 && named != input.LocalId && RrWire.Contains(present, named))
                    {
                        Give(_what + " " + Slot + " lost to player " + named);
                        return;
                    }

                    if (!authority[Slot])
                    {
                        if (named == input.LocalId)
                        {
                            // Our record stands but a later request took the authority (a claimer that read our record
                            // first and moved on, holding an authority it never uses): ask it back, as Owned does.
                            if (_lostAt < 0.0)
                            {
                                _lostAt = now;
                                _nextAsk = now + LostPatience;
                            }
                            else if (now >= _nextAsk)
                            {
                                AskAuthority = Slot;
                                _nextAsk = now + AskEvery;
                            }

                            return;
                        }

                        if (now - _since >= AuthorityWait)
                        {
                            Give("authority over " + _what + " " + Slot + " went elsewhere and nobody wrote it; trying the next");
                        }

                        return;
                    }

                    if (_lostAt >= 0.0)
                    {
                        _lostAt = -1.0;
                        _since = now;
                        return;
                    }

                    if (named != input.LocalId)
                    {
                        // Our record was overwritten (a clean-up racing it): write it again.
                        if (now - _since >= ConfirmAfter)
                        {
                            WriteClaim = true;
                            _since = now;
                        }

                        return;
                    }

                    if (now - _since >= ConfirmAfter)
                    {
                        Current = Stage.Owned;
                        ClaimSeconds = now - _began;
                        _lostAt = -1.0;
                        Array.Clear(_tried, 0, _tried.Length);
                        _note = _what + " " + Slot + " claimed in " + Math.Round(ClaimSeconds * 1000.0) + " ms";
                    }

                    return;
                }

                case Stage.Owned:
                {
                    int named = owner[Slot];

                    if (named >= 0 && named != input.LocalId && RrWire.Contains(present, named))
                    {
                        int lost = Slot;
                        Reset();
                        _note = _what + " " + lost + " was taken over by player " + named + "; claiming again";
                        return;
                    }

                    if (!authority[Slot])
                    {
                        if (_lostAt < 0.0)
                        {
                            _lostAt = now;
                            _nextAsk = now + LostPatience;
                        }
                        else if (now >= _nextAsk)
                        {
                            AskAuthority = Slot;
                            _nextAsk = now + AskEvery;
                        }

                        return;
                    }

                    _lostAt = -1.0;

                    if (named != input.LocalId)
                    {
                        WriteClaim = true;
                        _note = _what + " " + Slot + " read " + (named < 0 ? "free" : "as player " + named + "'s (gone)") + " while held; claimed again";
                    }

                    return;
                }
            }
        }

        private void Give(string why)
        {
            if (Slot >= 0 && Slot < _tried.Length)
            {
                _tried[Slot] = true;
            }

            Current = Stage.Idle;
            Slot = -1;
            _lostAt = -1.0;
            _note = why;
        }

        private int FindFree(int start, int count, bool[] valid, bool[] free)
        {
            if (count <= 0)
            {
                return -1;
            }

            int k0 = ((start % count) + count) % count;

            for (int i = 0; i < count; i++)
            {
                int k = (k0 + i) % count;

                if (valid[k] && !_tried[k] && free[k])
                {
                    return k;
                }
            }

            return -1;
        }
    }
}
