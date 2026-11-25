using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// Harness hooks for any quest room ({"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.RrQuestTest.X"}),
    /// run offline where the local player is the authority. Each logs one "QuestTest:" line.
    ///
    /// <see cref="Start"/> turns on <see cref="RrQuestRoom.TestGuard"/>: with Rec Room's one-hit downs the first
    /// goblin swing would end a scripted solo run, so hits on the harness player are logged instead of counted.
    /// </summary>
    public static class RrQuestTest
    {
        /// <summary>Presses start and waits (max 15 s) until act 1's combat has begun.</summary>
        public static IEnumerator Start()
        {
            var q = RrQuestRoom.Current;
            if (q == null)
            {
                RrLog.Warn("Quest", "QuestTest: no quest room");
                yield break;
            }

            RrQuestRoom.TestGuard = true;
            q.RequestStart();
            float end = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < end && !(q.Phase == RrPhase.Playing && q.Stage == RrQuestStage.Combat))
            {
                yield return null;
            }

            yield return new WaitForSeconds(2.5f);
            RrLog.Info("Quest", "QuestTest: started - " + q.Describe());
        }

        /// <summary>Clears the current act and walks the local player through the exit door (max 8 s).</summary>
        public static IEnumerator NextAct()
        {
            var q = RrQuestRoom.Current;
            if (q == null || q.CurrentAct == null)
            {
                yield break;
            }

            int from = q.ActIndex;
            q.DebugClearAct();
            float end = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < end && q.Stage != RrQuestStage.Clear)
            {
                yield return null;
            }

            var door = q.CurrentAct.ExitDoor;
            if (door != null)
            {
                yield return new WaitForSeconds(1.3f);
                RrPlayer.Teleport(door.transform.TransformPoint(new Vector3(0f, 0.05f, 1.2f)), door.transform.eulerAngles.y);
                while (Time.realtimeSinceStartup < end && q.ActIndex == from)
                {
                    yield return null;
                }
            }

            yield return new WaitForSeconds(1f);
            RrLog.Info("Quest", "QuestTest: next act - " + q.Describe());
        }

        /// <summary>Jumps to the boss act (the last) and waits for its combat.</summary>
        public static IEnumerator Boss()
        {
            var q = RrQuestRoom.Current;
            if (q == null || q.Def == null)
            {
                yield break;
            }

            q.DebugGoToAct(q.Def.Acts.Count - 1);
            float end = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < end && q.Stage != RrQuestStage.Combat)
            {
                yield return null;
            }

            yield return new WaitForSeconds(4f);
            RrLog.Info("Quest", "QuestTest: boss - " + q.Describe());
        }

        /// <summary>Jumps to act 4 (lava bridges in Golden Trophy).</summary>
        public static IEnumerator Act4()
        {
            var q = RrQuestRoom.Current;
            if (q == null)
            {
                yield break;
            }

            q.DebugGoToAct(3);
            yield return new WaitForSeconds(6f);
            RrLog.Info("Quest", "QuestTest: act 4 - " + q.Describe());
        }

        /// <summary>Jumps to act 7 (the hedge maze in Golden Trophy).</summary>
        public static IEnumerator Act7()
        {
            var q = RrQuestRoom.Current;
            if (q == null)
            {
                yield break;
            }

            q.DebugGoToAct(6);
            yield return new WaitForSeconds(4f);
            RrLog.Info("Quest", "QuestTest: act 7 - " + q.Describe());
        }

        /// <summary>Jumps to act 3 (the boulder ramps in Golden Trophy).</summary>
        public static IEnumerator Act3()
        {
            var q = RrQuestRoom.Current;
            if (q == null)
            {
                yield break;
            }

            q.DebugGoToAct(2);
            yield return new WaitForSeconds(6f);
            RrLog.Info("Quest", "QuestTest: act 3 - " + q.Describe());
        }

        /// <summary>Hits every living enemy with 60 damage from the local player (as a weapon would).</summary>
        public static void HitAll()
        {
            var q = RrQuestRoom.Current;
            if (q == null)
            {
                return;
            }

            int n = 0;
            for (int s = 0; s < RrQuestEnemies.Slots; s++)
            {
                var e = q.Enemies.At(s);
                if (e != null && e.Alive)
                {
                    e.TakeLocalHit(60, e.Kind == RrEnemyKind.Charger ? RrBodyPart.WeakSpot : RrBodyPart.Body, e.Center, RrCombat.UnknownTool);
                    n++;
                }
            }

            RrLog.Info("Quest", "QuestTest: hit " + n + " enemies - " + q.Describe());
        }

        /// <summary>Smashes the first three pots (act 1's in Golden Trophy) as the local player would.</summary>
        public static void BreakPots()
        {
            var q = RrQuestRoom.Current;
            if (q == null)
            {
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                q.Pots.DebugBreak(i);
            }

            RrLog.Info("Quest", "QuestTest: broke pots - " + q.Describe());
        }

        /// <summary>
        /// Puts the local player down (to see the downed HUD and the fail flow when alone): one enemy hit, the final
        /// rule; with the harness guard on (it stops hits) the player is downed directly.
        /// </summary>
        public static void Down()
        {
            var h = RrHealth.Local;
            if (h != null)
            {
                h.ApplyDamage(1, RrCombat.NoPlayer, RrBodyPart.Body, RrPlayer.FeetPosition, RrCombat.EnemyTool);
                if (!h.IsOut && h.InCombat)
                {
                    h.GoOut(RrCombat.NoPlayer, RrCombat.EnemyTool, RrPlayer.FeetPosition);
                }
            }

            RrLog.Info("Quest", "QuestTest: down - health " + (h != null ? h.Health : -1) + ", out " + (h != null && h.IsOut));
        }

        /// <summary>
        /// Logs the final rules in force: kill gold per roster type, the boss's gold, the player caps and what the
        /// rating says at the published totals (checks the S/A/B/C edges).
        /// </summary>
        public static void Rules()
        {
            var q = RrQuestRoom.Current;
            if (q == null || q.Def == null)
            {
                return;
            }

            var d = q.Def;
            var sb = new System.Text.StringBuilder();
            for (int t = 0; t < d.Enemies.Count; t++)
            {
                sb.Append(t > 0 ? ", " : "").Append(t).Append('=').Append(d.KillPointsOf(t));
            }

            string edges = "";
            if (d.FixedRating)
            {
                int[] probe = { d.RatingGoldS, d.RatingGoldS - 1, d.RatingGoldA, d.RatingGoldB, d.RatingGoldB - 1 };
                foreach (int g in probe)
                {
                    edges += " " + g + ":" + d.Rating(g, 0);
                }
            }

            RrLog.Info("Quest", "QuestTest: rules - kill gold [" + sb + "], boss " + d.BossKillPoints + ", players " + d.MinPlayers + "-" + d.MaxPlayers
                + ", health " + d.PlayerHealth + ", rating" + (d.FixedRating ? edges : " by share") + ", mods " + q.QuestInt(RrQuestInts.Mods));
        }

        /// <summary>Kills the boss as a player's weapon would (gold and all), to see every other enemy die with it.</summary>
        public static void KillBoss()
        {
            var q = RrQuestRoom.Current;
            if (q == null || q.Enemies == null)
            {
                return;
            }

            int before = q.Enemies.Alive;
            RrEnemy boss = null;
            for (int s = 0; s < RrQuestEnemies.Slots; s++)
            {
                var e = q.Enemies.At(s);
                if (e != null && e.Alive && q.IsBoss(e, q.Enemies.TypeAt(s)))
                {
                    boss = e;
                    break;
                }
            }

            if (boss != null)
            {
                boss.Die(RrCombat.LocalId, true);
            }

            RrLog.Info("Quest", "QuestTest: kill boss - " + (boss != null ? "found" : "no boss alive") + ", alive " + before + " -> " + q.Enemies.Alive + ", " + q.Describe());
        }

        /// <summary>
        /// Look check of the lobby kit without touching the quest's level: a mod board at room (-4, 0, 5.2) facing
        /// +z players, and Golden Trophy's dressing-room wall rack (4 swords, 2 shields, 2 bows) on a 5.4 x 2.6 m
        /// test wall at room (6, 0, -7.2), facing +z.
        /// </summary>
        public static void LobbyKit()
        {
            var q = RrQuestRoom.Current;
            if (q == null || q.Mods == null)
            {
                return;
            }

            q.ModBoard(q.transform, new Vector3(-4f, 0f, 5.2f), 0f);
            var wall = RrQuestKit.StoneLight ?? RrLook.Solid(RrLook.Wood);
            RrBuild.Box(q.transform, "Test Wall", new Vector3(6f, 1.3f, -7.3f), new Vector3(5.4f, 2.6f, 0.2f), wall);
            var rack = RrQuestRack.DressingRoomWall(q.transform, new Vector3(6f, 0f, -7.2f), 180f, "(QuestSword)", "(QuestShield)", "(Longbow)");
            RrLog.Info("Quest", "QuestTest: lobby kit - mod board and a " + rack.Count + "-peg wall rack");
        }

        /// <summary>Sets the RRO mods from the lobby (bits: 1 Turbo, 2 Coconut Chaos, 4 Laser Weapons) and waits for the int.</summary>
        public static IEnumerator ModsAll()
        {
            yield return SetMods(RrQuestMods.Turbo | RrQuestMods.CoconutChaos | RrQuestMods.LaserWeapons);
        }

        public static IEnumerator ModsOff()
        {
            yield return SetMods(0);
        }

        private static IEnumerator SetMods(int flags)
        {
            var q = RrQuestRoom.Current;
            if (q == null || q.Mods == null)
            {
                yield break;
            }

            q.Ask(RrQuestEvents.ReqMods, (byte)flags);
            float end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end && q.QuestInt(RrQuestInts.Mods) != flags)
            {
                yield return null;
            }

            yield return new WaitForSeconds(1.5f);
            RrLog.Info("Quest", "QuestTest: mods " + q.QuestInt(RrQuestInts.Mods) + " (" + q.Mods.Describe(q.QuestInt(RrQuestInts.Mods)) + ")");
        }

        public static void Report()
        {
            var q = RrQuestRoom.Current;
            RrLog.Info("Quest", "QuestTest: report - " + (q != null ? q.Describe() : "no quest room") + ", player at " + RrPlayer.FeetPosition.ToString("F1"));
        }
    }
}
