using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.DiscGolf
{
    /// <summary>
    /// The scorecard in the instance's custom ints (written by the instance authority only) and Rec Room's score words.
    ///
    /// Layout (custom ints 0-18): 0 the group's hole (0-8, 9 = the round is over), 1 the golfer count (up to 4), 2-5 the
    /// golfers (PlayerId + 1, slot = disc colour), 6-9 each golfer's live hole (<see cref="Live"/>), 10-17 the card: two ints
    /// per golfer, five bits per hole holding score + 8 (0 = not played; holes 0-5 in the first int, 6-8 in the second),
    /// 18 the winner (PlayerId + 1).
    ///
    /// A hole's score is the counted throws plus hazard penalties minus bonus panels; the mercy rule
    /// ([DiscGolfManager] mercyRuleHoleScore 7) ends a hole at 7 strokes and writes 7.
    /// </summary>
    public static class RrDiscGolfScore
    {
        public const int MaxGolfers = 4;
        public const int MercyScore = 7;

        public const int IntHole = 0;
        public const int IntCount = 1;
        public const int IntGolfers = 2;
        public const int IntLive = 6;
        public const int IntSheets = 10;
        public const int IntWinner = 18;

        /// <summary>One golfer's current hole: throws, penalties, bonuses, holed / mercy, and which hole it is.</summary>
        public struct Live
        {
            public int Throws;
            public int Penalties;
            public int Bonuses;
            public bool Done;
            public bool Mercy;

            /// <summary>The hole these numbers belong to (-1: none yet).</summary>
            public int Hole;

            public int Score
            {
                get { return Mercy ? MercyScore : Throws + Penalties - Bonuses; }
            }

            /// <summary>Strokes towards the mercy rule (throws and penalties).</summary>
            public int Strokes
            {
                get { return Throws + Penalties; }
            }

            public int Pack()
            {
                return (Mathf.Clamp(Throws, 0, 31)) | (Mathf.Clamp(Penalties, 0, 15) << 5) | (Mathf.Clamp(Bonuses, 0, 15) << 9) | (Done ? 1 << 13 : 0) | (Mercy ? 1 << 14 : 0) |
                       ((Mathf.Clamp(Hole + 1, 0, 15)) << 16);
            }

            public static Live Unpack(int v)
            {
                return new Live
                {
                    Throws = v & 31,
                    Penalties = (v >> 5) & 15,
                    Bonuses = (v >> 9) & 15,
                    Done = (v & (1 << 13)) != 0,
                    Mercy = (v & (1 << 14)) != 0,
                    Hole = ((v >> 16) & 15) - 1,
                };
            }

            public static Live For(int hole)
            {
                return new Live { Hole = hole };
            }
        }

        public static int GolferCount(RrInstanceState s)
        {
            return Mathf.Clamp(s.Get(IntCount), 0, MaxGolfers);
        }

        public static int GolferId(RrInstanceState s, int slot)
        {
            return slot >= 0 && slot < MaxGolfers ? s.Get(IntGolfers + slot) - 1 : -1;
        }

        /// <summary>The slot of a player among the golfers, -1 when not playing.</summary>
        public static int SlotOf(RrInstanceState s, int playerId)
        {
            int n = GolferCount(s);
            for (int i = 0; i < n; i++)
            {
                if (GolferId(s, i) == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        public static Live LiveOf(RrInstanceState s, int slot)
        {
            return slot >= 0 && slot < MaxGolfers ? Live.Unpack(s.Get(IntLive + slot)) : Live.For(-1);
        }

        /// <summary>A hole's score on the card, or null when it is not played.</summary>
        public static int? Hole(RrInstanceState s, int slot, int hole)
        {
            if (slot < 0 || slot >= MaxGolfers || hole < 0 || hole >= RrDiscGolfCourses.Holes)
            {
                return null;
            }

            int v = s.Get(IntSheets + slot * 2 + hole / 6);
            int raw = (v >> ((hole % 6) * 5)) & 31;
            return raw == 0 ? (int?)null : raw - 8;
        }

        /// <summary>Authority: writes a hole's score (clamped to -7..23).</summary>
        public static void SetHole(RrInstanceState s, int slot, int hole, int score)
        {
            if (slot < 0 || slot >= MaxGolfers || hole < 0 || hole >= RrDiscGolfCourses.Holes)
            {
                return;
            }

            int at = IntSheets + slot * 2 + hole / 6;
            int shift = (hole % 6) * 5;
            int v = s.Get(at) & ~(31 << shift);
            v |= Mathf.Clamp(score + 8, 1, 31) << shift;
            s.Set(at, v);
        }

        public static void ClearSheet(RrInstanceState s, int slot)
        {
            if (slot >= 0 && slot < MaxGolfers)
            {
                s.Set(IntSheets + slot * 2, 0);
                s.Set(IntSheets + slot * 2 + 1, 0);
            }
        }

        /// <summary>The card's total so far, and how many holes it has.</summary>
        public static int Total(RrInstanceState s, int slot, out int played)
        {
            int sum = 0;
            played = 0;
            for (int h = 0; h < RrDiscGolfCourses.Holes; h++)
            {
                var v = Hole(s, slot, h);
                if (v.HasValue)
                {
                    sum += v.Value;
                    played++;
                }
            }

            return sum;
        }

        /// <summary>The total against the par of the holes played ("+2", "-1", "E").</summary>
        public static string ToPar(RrInstanceState s, int slot, int course)
        {
            int diff = 0;
            for (int h = 0; h < RrDiscGolfCourses.Holes; h++)
            {
                var v = Hole(s, slot, h);
                if (v.HasValue)
                {
                    diff += v.Value - RrDiscGolfCourses.Par(course, h);
                }
            }

            return diff == 0 ? "E" : diff > 0 ? "+" + diff : diff.ToString();
        }

        /// <summary>[DiscGolfManager]'s score words for a hole against its par (rr_sports.md 1.1, exact strings).</summary>
        public static string Word(int score, int par)
        {
            if (score == 1)
            {
                return "Hole In One!";
            }

            int d = score - par;
            if (d <= -3)
            {
                return "Albatross -3";
            }

            switch (d)
            {
                case -2: return "Eagle -2";
                case -1: return "Birdie -1";
                case 0: return "Par";
                case 1: return "Bogey +1";
                case 2: return "Double Bogey +2";
                default: return "Triple Bogey +3";
            }
        }
    }
}
