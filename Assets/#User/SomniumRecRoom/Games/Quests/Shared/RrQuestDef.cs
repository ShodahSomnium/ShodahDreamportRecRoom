using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// What a quest is (rr_quests.md 2.1, 2.7, 3.1 "RrQuestCatalog"): its currency and death penalty from the quest's
    /// QuestManager, its music and stingers, its enemy roster (the Maker "(QuestAIController)*" spawners), its weapons
    /// and its acts. A quest room fills one in <see cref="RrQuestRoom.CreateDef"/>; the framework reads nothing else.
    /// Measured values carry their source; tuning marked GUESS comes from the research's tuning table.
    ///
    /// Rec Room's FINAL rules (rr_latest_rgames_story.md 5.1, 5.3, 12.1) are the defaults: one hit downs a player,
    /// kills pay fixed points per enemy type, the rating comes from fixed gold totals, and each quest has its own player
    /// cap. Fields left at their "unset" value (0, -1, empty) are filled after <see cref="RrQuestRoom.CreateDef"/> from
    /// <see cref="RrQuestFinalRules"/> for the room's activity, so a quest only sets what differs.
    /// </summary>
    public sealed class RrQuestDef
    {
        public string Name;
        /// <summary>QuestManager score name ("Gold", "Credits", "Silver").</summary>
        public string Currency = "Gold";
        /// <summary>QuestManager deathPointsPenalty (Golden Trophy 2000), taken from the team score per player down.</summary>
        public int DeathPenalty = 2000;

        /// <summary>Music (rr_quests.md 2.7): lobby, exploring, combat, boss; act-clear fanfare; win / lose stingers (2.1).</summary>
        public string LobbyMusic;
        public string ExploreMusic;
        public string CombatMusic;
        public string BossMusic;
        public string ClearFanfare;
        public string WinStinger;
        public string LoseStinger;
        /// <summary>Clip lengths, s (the music goes back to exploring after the fanfare; the stingers are not looped).</summary>
        public float ClearFanfareSeconds = 4.5f;

        /// <summary>
        /// Coach's line when the run starts: "Game On!" (WIKI Golden Trophy tips "After Coach says 'Game On!'"; clip
        /// <c>Gameon</c>, bundle 8b1b2e31 #3496784870384460132, the one voice line the quests use). Null: silent.
        /// </summary>
        public string StartLine = RrAnnouncer.GameOn;

        /// <summary>Point VFX (QuestManager small / large point VFX) and the coin sounds.</summary>
        public string PointsFx = "[VFX_GoldCoins]";
        public string BossPointsFx = "[VFX_BossGoldCoins]";
        public string PointsSound = "Quest_Coins_Spawn01";
        public string BossPointsSound = "Quest_BossCoins";

        /// <summary>Enemy roster: index = actor type on the wire; the "(QuestAIController)..." spawner prefab names.</summary>
        public readonly List<string> Enemies = new List<string>();
        /// <summary>
        /// Gold for killing each roster type (index = type; 0 = unset: the catalog's value for the spawner, else the
        /// enemy's max health). Rec Room pays a fixed amount per enemy kind, split among the players by damage share
        /// (WIKI Quest for the Golden Trophy "Scoring": goblin 450, imp 500, elites 850, barrel knight 1,500, elite
        /// barrel knight 2,000, Goblin King 5,500). Our team score is shared, so the split does not matter here.
        /// </summary>
        public readonly List<int> KillPoints = new List<int>();
        /// <summary>Quest weapons (index + 1 is the presence hand item for remote players' held weapons).</summary>
        public readonly List<string> Weapons = new List<string>();
        public readonly List<RrQuestAct> Acts = new List<RrQuestAct>();

        /// <summary>
        /// The boss's roster type (-1: any enemy the room shows with the boss coin burst,
        /// <see cref="RrQuestRoom.PointsKind"/> = PointsBoss). Its death ends the fight: every other enemy dies with it
        /// (WIKI: the Goblin King's "death kills every other enemy"; Jumbotron's 4th panel "kills all enemies").
        /// </summary>
        public int BossType = -1;
        /// <summary>Gold for the boss (0 = unset: the catalog's, else the roster type's KillPoints). Golden Trophy 5,500.</summary>
        public int BossKillPoints;
        /// <summary>Whether the boss's death kills every other enemy (all Rec Room quest bosses with adds do).</summary>
        public bool BossDeathKillsAll = true;

        /// <summary>
        /// Player rules. Rec Room's final rule for every quest (WIKI Quests: "one hit from an enemy (or an ally's
        /// ranged weapon) will down you"; there is no player health bar): <see cref="PlayerHealth"/> 1 makes any hit
        /// that counts a down, exactly like Paintball's MaxHealth 1. A shield or blade block still cancels a melee
        /// hit (<see cref="RrMelee.TryBlock"/>), and the 3 s after a revive are the only other protection. An ally's
        /// melee weapon never downs you (only their ranged ones do). Revive range 2.5 m and stay 1 m are measured
        /// (rr_quests.md 2.5); the hold times are GUESS.
        /// </summary>
        public int PlayerHealth = 1;
        public float ReviveRange = 2.5f;
        public float ReviveStay = 1f;
        public float ReviveSecondsVr = 1.2f;
        public float ReviveSecondsDesktop = 1.5f;
        /// <summary>Health after a revive (1 = back to the one-hit rule).</summary>
        public int ReviveHealth = 1;
        public float ReviveInvincible = 3f;
        /// <summary>
        /// Ours, not Rec Room's (no source has a bleed-out; rr_latest_rgames_story.md 5.3 item 11): a downed player
        /// nobody revives gets up at the act's entry after this long, so a big party is not stuck watching. Downed
        /// players also get up with the team at the next act (GUESS), and the team fails when everyone is down.
        /// </summary>
        public float BleedOutSeconds = 30f;
        public float DownedMovement = 0.3f;

        /// <summary>
        /// Players per run (0 = unset: the catalog's). Rec Room's final caps: 1-4 for Golden Trophy, Crimson Cauldron
        /// and Jumbotron, 1-3 for Isle of Lost Skulls and Crescendo of the Blood Moon (WIKI infoboxes / Quests page).
        /// The minimum is 1 for small Somnium groups. Wave sizes scale to the party as a share of MaxPlayers.
        /// </summary>
        public int MinPlayers;
        public int MaxPlayers;

        /// <summary>Seconds of the act title before the first wave.</summary>
        public float IntroSeconds = 3f;
        /// <summary>At most this many enemies alive at once (actor slots: 30 per instance).</summary>
        public int MaxAlive = 10;
        /// <summary>Game-over screens (QuestManager 6 s; Pirate 10 s).</summary>
        public float FailSeconds = 8f;
        public float VictorySeconds = 20f;

        /// <summary>
        /// Rating from the team's total (WIKI Quest_Rating): S at or above <see cref="RatingGoldS"/>, A at or above
        /// <see cref="RatingGoldA"/>, B at or above <see cref="RatingGoldB"/>, else C. Golden Trophy 150,000 /
        /// 125,000 / 100,000 Gold (the maximum is "slightly over 180k"), Crimson Cauldron 300k / 275k / 250k, Jumbotron
        /// 150k / 125k / 100k Credits. 0 = unset: the catalog's, and with none there the old share of the max
        /// possible (<see cref="RatingS"/>... GUESS 85 / 65 / 45 %).
        /// </summary>
        public int RatingGoldS, RatingGoldA, RatingGoldB;
        /// <summary>Rec Room's best total for this quest (Golden Trophy "slightly over 180k"; 0 unknown), logged against ours.</summary>
        public int RecRoomMaxGold;
        /// <summary>Fallback rating thresholds of score / max possible when no gold totals are known (GUESS).</summary>
        public float RatingS = 0.85f, RatingA = 0.65f, RatingB = 0.45f;

        public int AddEnemy(string spawnerPrefab)
        {
            return AddEnemy(spawnerPrefab, 0);
        }

        /// <summary>Adds a roster type with its kill gold (0: from the catalog, else its max health).</summary>
        public int AddEnemy(string spawnerPrefab, int killPoints)
        {
            Enemies.Add(spawnerPrefab);
            while (KillPoints.Count < Enemies.Count)
            {
                KillPoints.Add(0);
            }

            KillPoints[Enemies.Count - 1] = killPoints > 0 ? killPoints : 0;
            return Enemies.Count - 1;
        }

        /// <summary>Sets a roster type's kill gold after the fact (0 clears it).</summary>
        public void SetKillPoints(int type, int points)
        {
            if (type < 0 || type >= Enemies.Count)
            {
                return;
            }

            while (KillPoints.Count < Enemies.Count)
            {
                KillPoints.Add(0);
            }

            KillPoints[type] = points > 0 ? points : 0;
        }

        /// <summary>A roster type's kill gold, 0 when none is known (the room then pays the enemy's max health).</summary>
        public int KillPointsOf(int type)
        {
            return type >= 0 && type < KillPoints.Count ? KillPoints[type] : 0;
        }

        public int AddAct(RrQuestAct act)
        {
            Acts.Add(act);
            return Acts.Count - 1;
        }

        /// <summary>Whether the rating uses fixed gold totals (the final rule) rather than a share of the max possible.</summary>
        public bool FixedRating
        {
            get { return RatingGoldS > 0; }
        }

        public string Rating(int score, int maxPossible)
        {
            if (FixedRating)
            {
                return score >= RatingGoldS ? "S" : score >= RatingGoldA ? "A" : score >= RatingGoldB ? "B" : "C";
            }

            float k = maxPossible > 0 ? score / (float)maxPossible : 0f;
            return k >= RatingS ? "S" : k >= RatingA ? "A" : k >= RatingB ? "B" : "C";
        }

        /// <summary>Wave-size party scale for this quest's cap (a full party gets the authored counts).</summary>
        public int Scaled(int count, int party)
        {
            return RrQuestWave.Scaled(count, party, MaxPlayers > 0 ? MaxPlayers : 4);
        }
    }
}
