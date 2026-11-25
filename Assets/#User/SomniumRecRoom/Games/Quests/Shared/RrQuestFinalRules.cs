using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// Rec Room's FINAL (pre-shutdown) quest rules per quest, from rr_latest_rgames_story.md 5.1 (Golden Trophy), 6.1
    /// (Crimson Cauldron), 7.1 (Crescendo), 8.1 (Isle), 9.1 (Jumbotron) and 12.1 (the cross-cutting table). The
    /// quest room applies them right after <see cref="RrQuestRoom.CreateDef"/> to every field the definition left
    /// unset (<see cref="Apply"/>), so a quest gets the final rules without repeating them, and a quest that sets a
    /// value itself keeps it. Unpublished numbers stay 0 (unset) instead of being guessed here.
    /// </summary>
    public static class RrQuestFinalRules
    {
        public sealed class Rules
        {
            public RrActivity Activity;
            public string Currency;
            /// <summary>Team score lost per down (QuestManager deathPointsPenalty; Jumbotron's wiki "lose 1200 credits").</summary>
            public int DeathPenalty;
            public int MinPlayers = 1;
            public int MaxPlayers = 4;
            /// <summary>Absolute rating totals (0 = not published).</summary>
            public int RatingS, RatingA, RatingB;
            /// <summary>Rec Room's best total ("slightly over 180k" for Golden Trophy; 0 unknown).</summary>
            public int MaxGold;
            /// <summary>Kill gold by spawner prefab (roster types are looked up by their spawner name).</summary>
            public readonly Dictionary<string, int> KillPoints = new Dictionary<string, int>();
            public int BossKillPoints;
            /// <summary>Checkpoints between acts (only Isle, every act, and Crescendo, the Grand Hall).</summary>
            public bool Checkpoints;
        }

        private static Dictionary<RrActivity, Rules> _all;

        /// <summary>The final rules of a quest activity, or null for anything that is not a quest.</summary>
        public static Rules For(RrActivity activity)
        {
            if (_all == null)
            {
                Build();
            }

            Rules r;
            return _all.TryGetValue(activity, out r) ? r : null;
        }

        private static void Build()
        {
            _all = new Dictionary<RrActivity, Rules>();

            // Quest for the Golden Trophy (5.1): 1-4 players, WIKI "Scoring" kill points, Quest_Rating S/A/B, no checkpoints.
            var gt = Add(RrActivity.GoldenTrophy, "Gold", 2000, 4, 150000, 125000, 100000, 180000);
            gt.KillPoints["(QuestAIController)GoblinBasicGroundMelee2020"] = 450;
            gt.KillPoints["(QuestAIController)GoblinBasicFlyingRanged"] = 500;
            gt.KillPoints["(QuestAIController)GoblinEliteGroundMelee"] = 850;
            gt.KillPoints["(QuestAIController)GoblinEliteFlyingRanged"] = 850;
            gt.KillPoints["(QuestAIController)GoblinKnightCharge"] = 1500;
            gt.KillPoints["(QuestAIController)GoblinKnightSpin"] = 2000;
            gt.BossKillPoints = 5500;

            // The Curse of the Crimson Cauldron (6.1): 1-4, S 300k / A 275k / B 250k; per-enemy points unpublished.
            Add(RrActivity.CrimsonCauldron, "Gold", 2000, 4, 300000, 275000, 250000, 0);

            // The Rise of Jumbotron (9.1): 1-4, "every time a player was shot by a bot ... lose 1200 credits", S/A/B as Golden Trophy.
            Add(RrActivity.Jumbotron, "Credits", 1200, 4, 150000, 125000, 100000, 0);

            // Isle of Lost Skulls (8.1): 1-3, a hidden combat score (per-enemy 500 / 1,000, the boss 10,000; deathless
            // ~155,500) whose rank thresholds are unpublished; the bundle's penalty 2000; checkpoints at every act.
            var isle = Add(RrActivity.LostSkulls, "Gold", 2000, 3, 0, 0, 0, 155500);
            isle.BossKillPoints = 10000;
            isle.Checkpoints = true;

            // Crescendo of the Blood Moon (7.1): 1-3, silver; the rank is a hidden combat score (base 4,400, S+ 4,160,
            // S 3,160, -200 per death), not a silver total, so no gold thresholds and no silver penalty here (both
            // unpublished); the Grand Hall is the checkpoint.
            var cbm = Add(RrActivity.Crescendo, "Silver", 0, 3, 0, 0, 0, 0);
            cbm.Checkpoints = true;
        }

        private static Rules Add(RrActivity a, string currency, int penalty, int maxPlayers, int s, int ra, int b, int maxGold)
        {
            var r = new Rules
            {
                Activity = a, Currency = currency, DeathPenalty = penalty, MinPlayers = 1, MaxPlayers = maxPlayers,
                RatingS = s, RatingA = ra, RatingB = b, MaxGold = maxGold
            };
            _all[a] = r;
            return r;
        }

        /// <summary>
        /// Fills the definition's unset fields from the activity's final rules: player caps, rating totals, Rec Room's
        /// best total, kill gold per roster type (by spawner name) and the boss's gold. Returns the rules used (null:
        /// not a known quest, the definition's own values and the framework defaults stay).
        /// </summary>
        public static Rules Apply(RrActivity activity, RrQuestDef def)
        {
            return Apply(For(activity), def);
        }

        /// <summary>The same with the rules in hand (null: only the framework's floor, 1-4 players).</summary>
        public static Rules Apply(Rules r, RrQuestDef def)
        {
            if (def == null)
            {
                return r;
            }

            if (r != null)
            {
                if (def.MinPlayers <= 0)
                {
                    def.MinPlayers = r.MinPlayers;
                }

                if (def.MaxPlayers <= 0)
                {
                    def.MaxPlayers = r.MaxPlayers;
                }

                if (def.RatingGoldS <= 0 && r.RatingS > 0)
                {
                    def.RatingGoldS = r.RatingS;
                    def.RatingGoldA = r.RatingA;
                    def.RatingGoldB = r.RatingB;
                }

                if (def.RecRoomMaxGold <= 0)
                {
                    def.RecRoomMaxGold = r.MaxGold;
                }

                for (int t = 0; t < def.Enemies.Count; t++)
                {
                    int p;
                    if (def.KillPointsOf(t) <= 0 && def.Enemies[t] != null && r.KillPoints.TryGetValue(def.Enemies[t], out p))
                    {
                        def.SetKillPoints(t, p);
                    }
                }

                if (def.BossKillPoints <= 0)
                {
                    def.BossKillPoints = r.BossKillPoints;
                }
            }

            // Whatever is still unset gets the framework's floor: at least one player, Rec Room's usual cap of 4.
            def.MinPlayers = def.MinPlayers > 0 ? def.MinPlayers : 1;
            def.MaxPlayers = def.MaxPlayers > 0 ? def.MaxPlayers : 4;
            if (def.MinPlayers > def.MaxPlayers)
            {
                def.MinPlayers = def.MaxPlayers;
            }

            return r;
        }
    }
}
