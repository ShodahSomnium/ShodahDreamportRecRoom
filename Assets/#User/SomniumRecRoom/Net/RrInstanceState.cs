using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>Match phases shared by every game (instance object int 3, bits 16-23).</summary>
    public enum RrPhase : byte
    {
        Lobby = 0,
        Countdown = 1,
        Playing = 2,
        RoundOver = 3,
        MatchOver = 4
    }

    /// <summary>
    /// The state of the local player's current instance, backed by its instance object "Rr Net Instance k"
    /// (somnium_networking.md §5.6; layout in <see cref="RrInstanceWire"/>): phase, round, timer, map, mode, seed,
    /// team scores, the roster and the activity-specific ints 27-58. Everyone reads; only the instance authority
    /// writes (a write from anyone else is ignored, with one warning) and <see cref="RrNet"/> pushes the changed ints.
    /// The timer travels as remaining milliseconds with the authority's heartbeat (Fusion's clock is not a proven
    /// API) and counts down locally. Offline and before the instance is bound it is the same array, local.
    /// <see cref="RrNet.Instance"/> is the live one; <c>new RrInstanceState()</c> makes a detached local copy.
    /// </summary>
    public sealed class RrInstanceState
    {
        /// <summary>First activity-specific int and how many there are (27..58).</summary>
        public const int CustomStart = RrInstanceWire.AtCustom;
        public const int CustomCount = RrInstanceWire.CustomCount;

        private readonly bool _attached;
        private readonly int[] _local = new int[RrInstanceWire.Ints];
        private double _localTimerEnd;

        public RrInstanceState()
        {
        }

        internal RrInstanceState(bool attached)
        {
            _attached = attached;
        }

        private int[] Ints
        {
            get { return _attached ? RrNet.Core.State : _local; }
        }

        /// <summary>This client may write it now (the instance authority, or offline, or a detached copy).</summary>
        public bool CanWrite
        {
            get { return !_attached || RrNet.IsInstanceAuthority; }
        }

        private bool Writable()
        {
            if (CanWrite)
            {
                return true;
            }

            RrLog.WarnOnce("Net", "state-write", "a write to the instance state by a client that is not its authority was ignored (player " +
                                                 RrNet.InstanceAuthorityId + " runs it)");
            return false;
        }

        private void Put(int at, int value)
        {
            if (Writable())
            {
                Ints[at] = value;
            }
        }

        private void PutField(int at, int shift, int count, int value)
        {
            if (Writable())
            {
                Ints[at] = RrWire.With(Ints[at], shift, count, value);
            }
        }

        // ------------------------------------------------------------------ header

        public RrPhase Phase
        {
            get { return (RrPhase)RrWire.Field(Ints[RrInstanceWire.AtAuthority], 16, 8); }
            set { PutField(RrInstanceWire.AtAuthority, 16, 8, (int)value); }
        }

        public int Round
        {
            get { return RrWire.Field(Ints[RrInstanceWire.AtAuthority], 24, 8); }
            set { PutField(RrInstanceWire.AtAuthority, 24, 8, value); }
        }

        /// <summary>Map/variant 0..65535.</summary>
        public int Map
        {
            get { return RrWire.Field(Ints[RrInstanceWire.AtMap], 0, 16); }
            set { PutField(RrInstanceWire.AtMap, 0, 16, value); }
        }

        /// <summary>Mode 0..255.</summary>
        public int Mode
        {
            get { return RrWire.Field(Ints[RrInstanceWire.AtMap], 16, 8); }
            set { PutField(RrInstanceWire.AtMap, 16, 8, value); }
        }

        public int Seed
        {
            get { return Ints[RrInstanceWire.AtSeed]; }
            set { Put(RrInstanceWire.AtSeed, value); }
        }

        /// <summary>Option bits (RrInstanceWire.OptionFriendlyFire, OptionLocked, OptionPrivate, OptionSpectators).</summary>
        public bool Option(int option)
        {
            return option >= 0 && option < 8 && RrWire.Bit(Ints[RrInstanceWire.AtMap], 24 + option);
        }

        public void SetOption(int option, bool on)
        {
            if (option >= 0 && option < 8)
            {
                PutField(RrInstanceWire.AtMap, 24 + option, 1, on ? 1 : 0);
            }
        }

        /// <summary>The PlayerId the instance object names as its authority (-1 none / offline).</summary>
        public int AuthorityId
        {
            get { return RrWire.FromId16(Ints[RrInstanceWire.AtAuthority]); }
        }

        /// <summary>The binding's generation (0 unbound/offline).</summary>
        public int Generation
        {
            get { return RrInstanceWire.GenerationOf(Ints); }
        }

        // ------------------------------------------------------------------ timer

        /// <summary>Seconds left in the current phase (counted down locally from the last written value).</summary>
        public float TimeRemaining
        {
            get
            {
                double end = _attached ? RrNet.Core.TimerEnd : _localTimerEnd;
                double left = end > 0.0 ? end - RrNet.Now : 0.0;
                return left > 0.0 ? (float)left : 0f;
            }
        }

        /// <summary>Authority: enters a phase with a timer (0 = none).</summary>
        public void SetPhase(RrPhase phase, float seconds)
        {
            if (!Writable())
            {
                return;
            }

            Phase = phase;
            SetTimer(seconds);
        }

        /// <summary>Authority: restarts the timer of the current phase (0 = none).</summary>
        public void SetTimer(float seconds)
        {
            if (!Writable())
            {
                return;
            }

            if (_attached)
            {
                RrNet.Core.SetTimer(RrNet.Now, seconds);
            }
            else
            {
                _localTimerEnd = seconds > 0f ? RrNet.Now + seconds : 0.0;
            }
        }

        // ------------------------------------------------------------------ scores

        public int TeamScore(int team)
        {
            return team >= 0 && team < 4 ? RrInstanceWire.TeamScore(Ints, team) : 0;
        }

        public void SetTeamScore(int team, int score)
        {
            if (team >= 0 && team < 4 && Writable())
            {
                RrInstanceWire.SetTeamScore(Ints, team, score);
            }
        }

        /// <summary>Objective int 0..1 (ints 9-10: flag holders PlayerId+1 per team, 16 bits each).</summary>
        public int Objective(int i)
        {
            return i >= 0 && i < 2 ? Ints[RrInstanceWire.AtObjective + i] : 0;
        }

        public void SetObjective(int i, int value)
        {
            if (i >= 0 && i < 2)
            {
                Put(RrInstanceWire.AtObjective + i, value);
            }
        }

        // ------------------------------------------------------------------ activity-specific ints

        /// <summary>Activity-specific int i (0..31 maps to ints 27..58).</summary>
        public int Get(int i)
        {
            return i >= 0 && i < CustomCount ? Ints[CustomStart + i] : 0;
        }

        public void Set(int i, int value)
        {
            if (i >= 0 && i < CustomCount)
            {
                Put(CustomStart + i, value);
            }
        }

        /// <summary>Any of the 64 ints, read only (diagnostics, raw layouts).</summary>
        public int Raw(int i)
        {
            return i >= 0 && i < RrInstanceWire.Ints ? Ints[i] : 0;
        }

        // ------------------------------------------------------------------ roster (16 entries)

        /// <summary>Per-player score in the roster (by PlayerId), 0 when absent.</summary>
        public int PlayerScore(int playerId)
        {
            int e = RrInstanceWire.RosterFind(Ints, playerId);
            return e >= 0 ? RrInstanceWire.RosterScore(Ints, e) : 0;
        }

        /// <summary>Authority: a player's score (0..1023); adds the player to the roster when absent.</summary>
        public void SetPlayerScore(int playerId, int score)
        {
            int e = Entry(playerId, true);

            if (e >= 0)
            {
                int[] r = Ints;
                Put(RrInstanceWire.AtRoster + e, RrInstanceWire.PackRoster(playerId, RrInstanceWire.RosterTeam(r, e), RrInstanceWire.RosterReady(r, e),
                    RrInstanceWire.RosterSpectator(r, e), score));
            }
        }

        /// <summary>A player's team in the roster, -1 when absent or none.</summary>
        public int RosterTeam(int playerId)
        {
            int e = RrInstanceWire.RosterFind(Ints, playerId);
            return e >= 0 ? RrInstanceWire.RosterTeam(Ints, e) : -1;
        }

        public bool RosterReady(int playerId)
        {
            int e = RrInstanceWire.RosterFind(Ints, playerId);
            return e >= 0 && RrInstanceWire.RosterReady(Ints, e);
        }

        public bool RosterSpectator(int playerId)
        {
            int e = RrInstanceWire.RosterFind(Ints, playerId);
            return e >= 0 && RrInstanceWire.RosterSpectator(Ints, e);
        }

        /// <summary>Authority: puts a player in the roster (or updates it), keeping the score.</summary>
        public void SetRoster(int playerId, int team, bool ready, bool spectator)
        {
            int e = Entry(playerId, true);

            if (e >= 0)
            {
                Put(RrInstanceWire.AtRoster + e, RrInstanceWire.PackRoster(playerId, team, ready, spectator, RrInstanceWire.RosterScore(Ints, e)));
            }
        }

        public void RemoveFromRoster(int playerId)
        {
            int e = RrInstanceWire.RosterFind(Ints, playerId);

            if (e >= 0)
            {
                Put(RrInstanceWire.AtRoster + e, 0);
            }
        }

        /// <summary>PlayerIds in the roster, in entry order.</summary>
        public List<int> RosterPlayers()
        {
            var list = new List<int>();
            int[] r = Ints;

            for (int e = 0; e < RrInstanceWire.RosterEntries; e++)
            {
                int id = RrInstanceWire.RosterPlayer(r, e);

                if (id >= 0)
                {
                    list.Add(id);
                }
            }

            return list;
        }

        private int Entry(int playerId, bool add)
        {
            int[] r = Ints;
            int e = RrInstanceWire.RosterFind(r, playerId);

            if (e >= 0 || !add || playerId < 0)
            {
                return e;
            }

            for (int i = 0; i < RrInstanceWire.RosterEntries; i++)
            {
                if (RrInstanceWire.RosterPlayer(r, i) < 0)
                {
                    return i;
                }
            }

            RrLog.WarnOnce("Net", "roster-full", "the instance roster is full (" + RrInstanceWire.RosterEntries + "): player " + playerId + " not listed");
            return -1;
        }

        /// <summary>Authority: clears the game part of the state (phase Lobby, round 0, scores, roster, custom ints); the binding stays.</summary>
        public void ResetGame()
        {
            if (!Writable())
            {
                return;
            }

            int[] r = Ints;
            r[RrInstanceWire.AtAuthority] &= 0xFFFF;

            for (int i = RrInstanceWire.AtMap; i < RrInstanceWire.AtActorMask; i++)
            {
                r[i] = 0;
            }

            SetTimer(0f);
        }
    }

    public static partial class RrNet
    {
        private static readonly RrInstanceState LiveState = new RrInstanceState(true);

        /// <summary>The state of the local player's current instance (never null).</summary>
        public static RrInstanceState Instance
        {
            get { return LiveState; }
        }
    }
}
