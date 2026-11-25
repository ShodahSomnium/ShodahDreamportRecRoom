using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Teams as Rec Room colours them: team 0 blue, team 1 red (paintball, laser tag, dodgeball, soccer), then
    /// green and yellow for games with more sides. A player's team lives in their presence slot; the instance
    /// authority balances teams at the start of a match (<see cref="Balance"/>), keeping players where they are
    /// when it can, and tells everyone with <see cref="RrGameEvents.TeamAssign"/>.
    /// </summary>
    public static class RrTeams
    {
        public const int None = -1;
        public const int Blue = 0;
        public const int Red = 1;

        public static readonly Color[] Colors =
        {
            new Color(0.18f, 0.51f, 0.95f),
            new Color(0.95f, 0.27f, 0.22f),
            new Color(0.30f, 0.78f, 0.35f),
            new Color(1.00f, 0.80f, 0.20f)
        };

        public static readonly string[] Names = { "Blue", "Red", "Green", "Yellow" };

        public static Color Color(int team)
        {
            return team >= 0 && team < Colors.Length ? Colors[team] : new Color(0.8f, 0.8f, 0.8f);
        }

        public static string Name(int team)
        {
            return team >= 0 && team < Names.Length ? Names[team] : "Nobody";
        }

        /// <summary>The team of a member as their presence says (the local player's own choice included).</summary>
        public static int TeamOf(int playerId)
        {
            var p = RrNet.Presence(playerId);
            return p.Valid ? p.Team : LocalTeam;
        }

        /// <summary>The local player's team (kept here too, so offline play works).</summary>
        public static int LocalTeam = None;

        public static void SetLocalTeam(int team)
        {
            LocalTeam = team;
            RrNet.SetTeam(team);
        }

        /// <summary>
        /// Authority: a balanced assignment of the members to teamCount teams, keeping current teams where the
        /// counts allow. Deterministic in member order (lowest PlayerId first).
        /// </summary>
        public static Dictionary<int, int> Balance(List<int> members, int teamCount)
        {
            var result = new Dictionary<int, int>();
            if (teamCount <= 0)
            {
                return result;
            }

            var sorted = new List<int>(members);
            sorted.Sort();
            int cap = (sorted.Count + teamCount - 1) / teamCount;
            var counts = new int[teamCount];
            var unassigned = new List<int>();
            foreach (int id in sorted)
            {
                int t = TeamOf(id);
                if (t >= 0 && t < teamCount && counts[t] < cap)
                {
                    result[id] = t;
                    counts[t]++;
                }
                else
                {
                    unassigned.Add(id);
                }
            }

            foreach (int id in unassigned)
            {
                int best = 0;
                for (int t = 1; t < teamCount; t++)
                {
                    if (counts[t] < counts[best])
                    {
                        best = t;
                    }
                }

                result[id] = best;
                counts[best]++;
            }

            // A spread wider than one after keeping choices: move from the largest to the smallest.
            bool moved = true;
            while (moved)
            {
                moved = false;
                int max = 0;
                int min = 0;
                for (int t = 1; t < teamCount; t++)
                {
                    if (counts[t] > counts[max])
                    {
                        max = t;
                    }

                    if (counts[t] < counts[min])
                    {
                        min = t;
                    }
                }

                if (counts[max] - counts[min] > 1)
                {
                    for (int i = sorted.Count - 1; i >= 0; i--)
                    {
                        if (result[sorted[i]] == max)
                        {
                            result[sorted[i]] = min;
                            counts[max]--;
                            counts[min]++;
                            moved = true;
                            break;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>Encodes an assignment for <see cref="RrGameEvents.TeamAssign"/>.</summary>
        public static byte[] Encode(Dictionary<int, int> assignment)
        {
            var bytes = new byte[assignment.Count * 3];
            int i = 0;
            foreach (var kv in assignment)
            {
                bytes[i++] = (byte)(kv.Key & 0xFF);
                bytes[i++] = (byte)((kv.Key >> 8) & 0xFF);
                bytes[i++] = (byte)kv.Value;
            }

            return bytes;
        }

        /// <summary>Applies a received assignment to the local player (others apply their own).</summary>
        public static void Apply(byte[] body)
        {
            int me = RrNet.LocalId;
            for (int i = 0; i + 2 < body.Length; i += 3)
            {
                int id = body[i] | (body[i + 1] << 8);
                if (id == me)
                {
                    SetLocalTeam(body[i + 2]);
                }
            }
        }

        public static void Reset()
        {
            LocalTeam = None;
        }
    }
}
