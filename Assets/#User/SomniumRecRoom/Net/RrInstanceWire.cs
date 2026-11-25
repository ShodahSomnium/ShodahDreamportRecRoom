using System;
using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// An instance object's 64 ints, "Rr Net Instance 0..23" (NetworkBridgeData64; somnium_networking.md §5.6). The
    /// state of one running copy of an activity, bound to its key (activity, instance number) by the elected
    /// instance authority and written by nobody else. Which object k an instance is bound to no longer moves it: every
    /// room is built at the world origin (RrPlacement), by every member, so positions travel as world (= room)
    /// millimetres, and who belongs to the instance comes from presence (activity + instance number). Pure (see <see cref="RrWire"/>).
    ///
    ///   0  magic 'RRI1' = 0x52524931
    ///   1  seq, bumped on every write and at least once a second by the authority: the heartbeat that liveness
    ///      (<see cref="RrElection"/>) and binding (<see cref="FreeForBind"/>) watch
    ///   2  activity (0-7) | instance number (8-15) | generation (16-23, +1 on every binding) | wire version (24-31)
    ///   3  authority PlayerId+1 (0-15) | phase (16-23: Lobby, Countdown, Playing, RoundOver, MatchOver) | round (24-31)
    ///   4  timer: remaining ms of the current phase at the moment of writing (0 = none); rewritten with the heartbeat
    ///   5  map/variant (0-15) | mode (16-23) | options (24-31: 0 friendly fire, 1 locked, 2 private, 3 spectators)
    ///   6  RNG seed
    ///   7  score team 0 (0-15) | team 1 (16-31), signed 16 bits each
    ///   8  score team 2 | team 3
    ///   9-10  objective state (flag holders PlayerId+1 per team, 16 bits each)
    ///  11-26  roster: 16 entries, PlayerId+1 (0-15) | team (16-19, 15 none) | ready (20) | spectator (21) | score (22-31, 0..1023)
    ///  27-58  activity-specific (RrInstanceState.Get/Set 0..31)
    ///  59  actor blocks leased (bitmask over the 12 "Rr Net Actors")
    ///  60  props leased count | spare
    ///  61-63  spare
    ///
    /// Timers without a shared clock (§5.6): Fusion's simulation time is not a server-proven API, so the authority
    /// writes the remaining milliseconds with every heartbeat and receivers count down locally from the moment they
    /// see seq move (error about one-way latency).
    /// </summary>
    public static class RrInstanceWire
    {
        public const string ObjectPrefix = "Rr Net Instance ";
        public const int Magic = 0x52524931;
        public const int Version = 1;
        public const int Ints = 64;
        public const int Objects = 24;

        public const int AtMagic = 0;
        public const int AtSeq = 1;
        public const int AtKey = 2;
        public const int AtAuthority = 3;
        public const int AtTimer = 4;
        public const int AtMap = 5;
        public const int AtSeed = 6;
        public const int AtScores = 7;
        public const int AtObjective = 9;
        public const int AtRoster = 11;
        public const int RosterEntries = 16;
        public const int AtCustom = 27;
        public const int CustomCount = 32;
        public const int AtActorMask = 59;
        public const int AtPropCount = 60;

        public const int OptionFriendlyFire = 0;
        public const int OptionLocked = 1;
        public const int OptionPrivate = 2;
        public const int OptionSpectators = 3;

        /// <summary>A header silent this long, whose authority is gone or elsewhere, no longer protects the object, seconds.</summary>
        public const double FreeAfterSilence = 5.0;

        public static int Key(int activity, int number)
        {
            return (activity & 0xFF) | ((number & 0xFF) << 8);
        }

        public static int KeyActivity(int key)
        {
            return key & 0xFF;
        }

        public static int KeyNumber(int key)
        {
            return (key >> 8) & 0xFF;
        }

        public static bool Written(int[] r)
        {
            return r != null && r.Length >= Ints && r[AtMagic] == Magic;
        }

        public static int KeyOf(int[] r)
        {
            return Written(r) ? r[AtKey] & 0xFFFF : -1;
        }

        public static int GenerationOf(int[] r)
        {
            return Written(r) ? RrWire.Field(r[AtKey], 16, 8) : 0;
        }

        public static int AuthorityOf(int[] r)
        {
            return Written(r) ? RrWire.FromId16(r[AtAuthority]) : -1;
        }

        public static int Phase(int[] r)
        {
            return RrWire.Field(r[AtAuthority], 16, 8);
        }

        public static int Round(int[] r)
        {
            return RrWire.Field(r[AtAuthority], 24, 8);
        }

        public static bool Option(int[] r, int option)
        {
            return RrWire.Bit(r[AtMap], 24 + option);
        }

        /// <summary>Anyone may join it (not locked, not private).</summary>
        public static bool Joinable(int[] r)
        {
            return !Written(r) || (!Option(r, OptionLocked) && !Option(r, OptionPrivate));
        }

        /// <summary>Where a key starts its search for a free object: spreads instances over the 24 objects.</summary>
        public static int StartIndex(int key, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            uint h = (uint)key * 2654435761u;
            return (int)((h >> 16) % (uint)count);
        }

        /// <summary>
        /// A fresh binding (the claim record): every int cleared, then magic, seq, the key with the next generation
        /// and this build's wire version, and this client as the authority in phase Lobby.
        /// </summary>
        public static void WriteBind(int[] into, int key, int localId)
        {
            int generation = ((GenerationOf(into) + 1) & 0xFF);

            if (generation == 0)
            {
                generation = 1;
            }

            int seq = Written(into) ? into[AtSeq] + 1 : 1;
            Array.Clear(into, 0, into.Length);
            into[AtMagic] = Magic;
            into[AtSeq] = seq;
            into[AtKey] = (key & 0xFFFF) | (generation << 16) | (Version << 24);
            into[AtAuthority] = RrWire.Id16(localId);
        }

        /// <summary>
        /// An instance object the elected authority of an unbound instance may claim (§5.4 "free"): never written, or
        /// nobody present names it (no participant slot names k) and its header no longer protects it - its authority
        /// has left the room, or it has been silent FreeAfterSilence. A header naming a present player that is still
        /// heartbeating is never free, even before any member's slot names it: that closes the window between a
        /// binding and its authority's own presence write. Unnamed and naming this client (its own claim record, or
        /// an instance it has left): free.
        /// </summary>
        public static bool FreeForBind(int[] r, bool named, int localId, IReadOnlyList<int> present, double silentSeconds)
        {
            if (!Written(r))
            {
                return true;
            }

            if (named)
            {
                return false;
            }

            int authority = AuthorityOf(r);

            if (authority == localId)
            {
                return true;
            }

            return authority < 0 || !RrWire.Contains(present, authority) || silentSeconds >= FreeAfterSilence;
        }

        // ------------------------------------------------------------------ roster

        public static int RosterPlayer(int[] r, int entry)
        {
            return RrWire.FromId16(r[AtRoster + entry]);
        }

        public static int RosterTeam(int[] r, int entry)
        {
            int t = RrWire.Field(r[AtRoster + entry], 16, 4);
            return t == 15 ? -1 : t;
        }

        public static bool RosterReady(int[] r, int entry)
        {
            return RrWire.Bit(r[AtRoster + entry], 20);
        }

        public static bool RosterSpectator(int[] r, int entry)
        {
            return RrWire.Bit(r[AtRoster + entry], 21);
        }

        public static int RosterScore(int[] r, int entry)
        {
            return RrWire.Field(r[AtRoster + entry], 22, 10);
        }

        public static int PackRoster(int playerId, int team, bool ready, bool spectator, int score)
        {
            return RrWire.Id16(playerId) | ((team < 0 || team > 14 ? 15 : team) << 16) | (ready ? 1 << 20 : 0) | (spectator ? 1 << 21 : 0) |
                   (RrWire.Clamp(score, 0, 1023) << 22);
        }

        /// <summary>The roster entry of a player, -1 when not on it.</summary>
        public static int RosterFind(int[] r, int playerId)
        {
            if (playerId < 0)
            {
                return -1;
            }

            for (int i = 0; i < RosterEntries; i++)
            {
                if (RosterPlayer(r, i) == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        public static int TeamScore(int[] r, int team)
        {
            if (team < 0 || team > 3)
            {
                return 0;
            }

            int v = r[AtScores + team / 2];
            return team % 2 == 0 ? RrWire.Lo(v) : RrWire.Hi(v);
        }

        public static void SetTeamScore(int[] r, int team, int score)
        {
            if (team < 0 || team > 3)
            {
                return;
            }

            int at = AtScores + team / 2;
            int v = r[at];
            int s = RrWire.S16(score);
            r[at] = team % 2 == 0 ? (v & unchecked((int)0xFFFF0000)) | s : (v & 0xFFFF) | (s << 16);
        }
    }
}
