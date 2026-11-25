using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// One wave of an act (rr_quests.md 3.3): a list of (enemy, count, spawn group) entries, authored for 4 players and
    /// scaled down for smaller parties, plus the condition that starts it. Waves of an act trigger in order; the act
    /// is clear when every wave has spawned and nothing is alive.
    /// </summary>
    public sealed class RrQuestWave
    {
        public enum When
        {
            /// <summary>As soon as the act's combat starts (the first wave).</summary>
            ActStart,
            /// <summary>The previous wave has fully spawned and every enemy is dead.</summary>
            PrevCleared,
            /// <summary>The previous wave has fully spawned and at most <see cref="Param"/> enemies are alive.</summary>
            PrevBelow,
            /// <summary>A living player stands inside <see cref="Zone"/> (act-local); after the first wave also 25 s after the previous one was cleared.</summary>
            Enter,
            /// <summary><see cref="Param"/> seconds after the previous wave started, or the previous wave is cleared.</summary>
            After
        }

        public struct Entry
        {
            /// <summary>Index into <see cref="RrQuestDef.Enemies"/>.</summary>
            public int Enemy;
            /// <summary>How many, for a party of 4.</summary>
            public int Count;
            /// <summary>Spawn group name of the act (<see cref="RrQuestAct.Groups"/>).</summary>
            public string Group;
        }

        public readonly List<Entry> Entries = new List<Entry>();
        public When Trigger = When.PrevCleared;
        public float Param;
        public Bounds Zone;
        /// <summary>Seconds between two spawns of this wave (the Rec Room "pop, pop, pop").</summary>
        public float Interval = 0.45f;
        /// <summary>Optional toast when it starts ("Reinforcements!").</summary>
        public string Toast;
        /// <summary>
        /// A side wave (<see cref="RrQuestAct.SideWave"/>): it starts only when a living player walks into its zone,
        /// in any order, at most once per act, and the act never waits for it. Rec Room's optional tunnel spawns
        /// (Golden Trophy act 4: "two side tunnels each spawn 2 extra goblins when entered", act 6: a left tunnel with
        /// 2 elite goblins): their gold is paid only if the team goes in and kills them.
        /// </summary>
        public bool Side;

        public RrQuestWave(When trigger, float param = 0f)
        {
            Trigger = trigger;
            Param = param;
        }

        /// <summary>Adds an entry; chainable.</summary>
        public RrQuestWave Add(int enemy, int count, string group)
        {
            Entries.Add(new Entry { Enemy = enemy, Count = count, Group = group });
            return this;
        }

        /// <summary>An Enter wave's zone (act-local centre and size); chainable.</summary>
        public RrQuestWave In(Vector3 center, Vector3 size)
        {
            Zone = new Bounds(center, size);
            return this;
        }

        /// <summary>Count for a party size (rr_quests.md 3.3, GUESS): 1 player 55 %, 2 70 %, 3 85 %, 4 100 %.</summary>
        public static int Scaled(int count4, int party)
        {
            return Scaled(count4, party, 4);
        }

        /// <summary>
        /// Count for a party size in a quest capped at maxParty players (Isle of Lost Skulls and Crescendo: 3): the
        /// authored count is for a full party, and the share is 40 % + 60 % x party / maxParty (GUESS; with a cap of 4
        /// that is the 55 / 70 / 85 / 100 % of <see cref="Scaled(int,int)"/>).
        /// </summary>
        public static int Scaled(int count, int party, int maxParty)
        {
            maxParty = Mathf.Max(1, maxParty);
            party = Mathf.Clamp(party, 1, maxParty);
            return Mathf.Max(1, Mathf.RoundToInt(count * (0.4f + 0.6f * party / maxParty)));
        }
    }
}
