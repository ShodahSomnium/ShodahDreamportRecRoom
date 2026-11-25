using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy
{
    /// <summary>
    /// Quest for the Golden Trophy (Goblin1, campaign 0; rr_quests.md 1.2, 2.1, 4.1): the Goblin King stole Coach's
    /// Golden Trophy. Iteration 8: the whole run is Rec Room's own levels from the Android build (2026-03-23, levels
    /// 19-29 in play order, <see cref="RrGoldenTrophyRefActs"/>): the theatre lobby with the start hologram and the mod
    /// board, the dressing room (the pre-start room where the team takes its weapons from the wall), acts 1-9 and the
    /// Goblin King's feast hall, where the king (<see cref="RrGoblinKing"/>) guards the trophy "(Golden_Bucket)".
    ///
    /// Flow as Rec Room had it: the team gathers in the lobby at the hologram ([ScoreboardProjector] (1): who is in the
    /// room, START); START takes everyone into the dressing room (act index 0, a <see cref="RrQuestAct.Prelude"/>: no
    /// title card, music or fanfare; its door opens after the intro); walking onto the platform by a door takes the team
    /// into the next room; the king's death ends the run in victory; the whole team down ends it back in the lobby.
    ///
    /// Measured (rule circuit fe6, [Quest_Goblin1_Manager]): currency Gold, death penalty 2000, win stinger
    /// "Crimson Cauldron - Our Heroes Have Triumphed" (reused by Goblin1), lose stinger "Golden Trophy - Our Heroes
    /// Have Failed", coin VFX [VFX_GoldCoins] / [VFX_BossGoldCoins], Quest_Coins_Spawn01 / Quest_BossCoins; music
    /// GoldenTrophyExplore / GoldenTrophyCombat2 / GoldenTrophyClearFanfare2 (2.7); enemies from the Maker spawners
    /// (2.2.1: sword goblin 100 HP 3 m/s, imp 100 HP, elite goblin 200 HP 4 m/s, elite imp 200 HP, barrel knights
    /// 250/333 HP charging 8 m at 12 m/s, hit only on the back target). Rooms, props, lights, weapons and pots are Rec
    /// Room's; waves (types from the final walkthrough) and acts 7-9's spawn groups are reconstructions.
    /// </summary>
    public sealed class RrGoldenTrophyRoom : RrQuestRoom
    {
        public const int Goblin = 0, Imp = 1, EliteGoblin = 2, EliteImp = 3, KnightGreen = 4, KnightRed = 5, King = 6;

        public const string Sword = "(QuestSword)";
        public const string Shield = "(QuestShield)";
        public const string Bow = "(Longbow)";
        public const string Crossbow = "(Crossbow)";

        /// <summary>Hazard code of the act-3 boulder lever.</summary>
        public const int HazardBoulder = 1;

        /// <summary>Act indices: 0 is the dressing room, 1..9 Rec Room's acts 1..9.</summary>
        public const int DressingRoom = 0, BoulderAct = 3, BossAct = 9;

        /// <summary>The level shown for each act index (the lobby is GoldenTrophy_Foyer): the names of the scene data and of the env data.</summary>
        public static readonly string[] Levels =
        {
            "GoldenTrophy_Armory", "GoldenTrophy_Hallway1", "GoldenTrophy_Hallway2", "GoldenTrophy_Hallway3", "GoldenTrophy_DeadIsland",
            "GoldenTrophy_Hallway4", "GoldenTrophy_Library", "GoldenTrophy_Hallway5", "GoldenTrophy_Battlefield", "GoldenTrophy_Cafeteria"
        };

        internal RrGoldenTrophyBoulder Boulder;
        internal Transform TrophySpot;
        internal readonly List<Transform> Chests = new List<Transform>();
        private GameObject _trophy;
        private float _victoryAt = -1f;

        /// <summary>The victory chests have creaked open this run (their sound plays once).</summary>
        private bool _chestsSounded;
        private Transform _lobbyRoot;
        private readonly List<Pose> _lobbySpawns = new List<Pose>();
        private readonly List<RrQuestDoor> _gatherDoors = new List<RrQuestDoor>();
        private readonly List<GameObject> _gatherPlatforms = new List<GameObject>();
        private RrGoldenTrophyHull _hull;
        private RrGameConsole _console;
        private string _shownLevel;

        protected override RrQuestDef CreateDef()
        {
            var d = new RrQuestDef
            {
                Name = "Quest for the Golden Trophy",
                Currency = "Gold",
                DeathPenalty = 2000,
                LobbyMusic = "GoldenTrophyExplore",
                ExploreMusic = "GoldenTrophyExplore",
                CombatMusic = "GoldenTrophyCombat2",
                BossMusic = "GoldenTrophyCombat2",
                ClearFanfare = "GoldenTrophyClearFanfare2",
                ClearFanfareSeconds = 4.5f,
                WinStinger = "Crimson Cauldron - Our Heroes Have Triumphed",
                LoseStinger = "Golden Trophy - Our Heroes Have Failed",
                PointsFx = "[VFX_GoldCoins]",
                BossPointsFx = "[VFX_BossGoldCoins]"
            };
            d.AddEnemy("(QuestAIController)GoblinBasicGroundMelee2020");
            d.AddEnemy("(QuestAIController)GoblinBasicFlyingRanged");
            d.AddEnemy("(QuestAIController)GoblinEliteGroundMelee");
            d.AddEnemy("(QuestAIController)GoblinEliteFlyingRanged");
            d.AddEnemy("(QuestAIController)GoblinKnightCharge");
            d.AddEnemy("(QuestAIController)GoblinKnightSpin");
            d.AddEnemy("(QuestAIController)GoblinKnightSpin");
            d.Weapons.Add(Sword);
            d.Weapons.Add(Shield);
            d.Weapons.Add(Bow);
            d.Weapons.Add(Crossbow);
            return d;
        }

        /// <summary>Harness hook: pulls act 3's boulder lever (the run must be in act 3's combat).</summary>
        public static void TestPushBoulder()
        {
            var q = Current as RrGoldenTrophyRoom;
            if (q != null)
            {
                q.Ask(RrQuestEvents.ReqHazard, (byte)HazardBoulder, 0);
                RrLog.Info("Quest", "QuestTest: boulder lever - hazards " + q.QuestInt(RrQuestInts.Hazards) + ", " + q.Describe());
            }
        }

        /// <summary>Harness hook: presses START on the lobby hologram as a player would (logs whether it was pressable).</summary>
        public static void TestPressStart()
        {
            var q = Current as RrGoldenTrophyRoom;
            if (q != null)
            {
                bool ok = q._console != null && q._console.Press("test");
                RrLog.Info("Quest", "QuestTest: lobby hologram START " + (ok ? "pressed" : "not pressable") + (q._console != null ? " (" + q._console.StartText + ", " + q._console.ShownPlayers + " player row(s))" : " (no console)"));
            }
        }

        /// <summary>Harness hook: clears the current act (every enemy dies, every wave counts as spawned): its door opens.</summary>
        public static void TestClearAct()
        {
            var q = Current as RrGoldenTrophyRoom;
            if (q != null)
            {
                q.DebugClearAct();
                RrLog.Info("Quest", "QuestTest: cleared act index " + q.ActIndex + " (" + (q.ActIndex >= 0 && q.ActIndex < Levels.Length ? Levels[q.ActIndex] : "-") + ")");
            }
        }

        internal void RegisterRack(RrQuestRack rack)
        {
            Racks.Add(rack);
        }

        protected override string BossName
        {
            get { return "THE GOBLIN KING"; }
        }

        protected override Pose LobbySpawn(int index)
        {
            if (_lobbySpawns.Count > 0)
            {
                // Rec Room's Player1..8SpawnPoint facing the stage; a ninth and later player one row further back.
                var p = _lobbySpawns[index % _lobbySpawns.Count];
                return new Pose(p.position + p.rotation * new Vector3(0f, 0f, -1.2f * (index / _lobbySpawns.Count)), p.rotation);
            }

            return new Pose(new Vector3((index % 4 - 1.5f) * 1.2f, 0.05f, -1.2f * (index / 4)), Quaternion.Euler(0f, -90f, 0f));
        }

        // ------------------------------------------------------------------ build

        /// <summary>
        /// Room-local root of act index i (0 = the dressing room): a grid of 4 columns 200 m apart and rows 220 m apart
        /// north of the lobby (the largest room, Hallway5, reaches 86 m from its arrival), so no two areas overlap and
        /// every area stays within ~750 m of the world origin.
        /// </summary>
        public static Vector3 ActSlot(int i)
        {
            return new Vector3(-300f + 200f * (i % 4), 0f, 220f + 220f * (i / 4));
        }

        /// <summary>
        /// Builds the lobby, the dressing room and the nine acts (<see cref="RrGoldenTrophyRefActs"/>), all behind the
        /// loading card: the framework registers pots and racks in build order (the pot index is the network id), so
        /// building an area later would renumber them. Only the current area and the next one stay active
        /// (<see cref="ShowAreas"/>), so the others cost neither draw calls nor physics.
        /// </summary>
        protected override IEnumerator BuildLevel()
        {
            _hull = new RrGoldenTrophyHull(transform);
            _lobbyRoot = RrBuild.Group(transform, "Lobby").transform;
            yield return RrGoldenTrophyRefActs.Lobby(this, _lobbyRoot, _lobbySpawns, _hull);
            BuildProgress = 0.16f;
            yield return null;
            var acts = new List<RrQuestAct>();
            var builders = new System.Func<RrGoldenTrophyRoom, Transform, List<RrQuestAct>, RrGoldenTrophyHull, IEnumerator>[]
            {
                RrGoldenTrophyRefActs.Armory, RrGoldenTrophyRefActs.Act1, RrGoldenTrophyRefActs.Act2, RrGoldenTrophyRefActs.Act3, RrGoldenTrophyRefActs.Act4,
                RrGoldenTrophyRefActs.Act5, RrGoldenTrophyRefActs.Act6, RrGoldenTrophyRefActs.Act7, RrGoldenTrophyRefActs.Act8, RrGoldenTrophyRefActs.Act9
            };
            for (int i = 0; i < builders.Length; i++)
            {
                var root = RrBuild.Group(transform, i == DressingRoom ? "Dressing Room" : "Act " + i, ActSlot(i)).transform;
                int before = acts.Count;
                yield return builders[i](this, root, acts, _hull);
                if (acts.Count == before)
                {
                    RrLog.Warn("Quest", Levels[i] + " did not build");
                }

                BuildProgress = 0.16f + 0.74f * (i + 1) / builders.Length;
                yield return null;
            }

            foreach (var act in acts)
            {
                Def.AddAct(act);
            }

            foreach (var act in acts)
            {
                int i = Def.Acts.IndexOf(act);
                RrLog.Info("Quest", "act index " + i + " (" + Levels[i] + ") \"" + act.Subtitle + "\" root " + act.Root.localPosition.ToString("F0") + ", entry " + act.Entry.ToString("F1")
                    + " yaw " + act.EntryYaw.ToString("0") + ", " + act.Waves.Count + " waves + " + act.SideWaves.Count + " side, " + act.Groups.Count + " groups, " + act.Lava.Count + " lava, exit " + (act.ExitDoor != null ? "door" : "none")
                    + (act.Prelude ? ", prelude" : "") + (act.Boss ? ", boss" : ""));
            }

            RrLog.Info("Quest", "GoldenTrophy hull: " + _hull.Quads + " rectangles in " + _hull.Batches + " merged meshes (" + _hull.Vertices + " vertices), " + _hull.ItemsBuilt + " stand-ins rebuilt, "
                + _hull.ItemsSkipped + " left out");
            ShowAreas(-1);
        }

        /// <summary>Only the lobby (outside a run) or the current area and the next one are active.</summary>
        private void ShowAreas(int act)
        {
            bool inRun = act >= 0;
            if (_lobbyRoot != null && _lobbyRoot.gameObject.activeSelf == inRun)
            {
                _lobbyRoot.gameObject.SetActive(!inRun);
            }

            if (Def == null)
            {
                return;
            }

            int cur = Mathf.Max(0, act);
            for (int i = 0; i < Def.Acts.Count; i++)
            {
                var root = Def.Acts[i].Root;
                bool on = i == cur || i == cur + 1;
                if (root != null && root.gameObject.activeSelf != on)
                {
                    root.gameObject.SetActive(on);
                }
            }

            string level = inRun && act < Levels.Length ? Levels[act] : "GoldenTrophy_Foyer";
            if (level != _shownLevel)
            {
                _shownLevel = level;
                AreaShown(level);
            }
        }

        /// <summary>
        /// Every client: the team now stands in another of Rec Room's rooms. The room's own lights are part of its area;
        /// the level's lighting environment (ambient, fog, sky) is applied by name by the environment system (iter8 env,
        /// DevNotes/refdata/apk/env/&lt;level&gt;.env); nothing global is set here (no extra ambient or fill light).
        /// </summary>
        private void AreaShown(string level)
        {
            RrLog.Info("Quest", "shown area " + level);
        }

        internal void LobbyStart(Transform parent, Vector3 local, float yaw)
        {
            StartButton(parent, local, yaw);
        }

        /// <summary>
        /// The lobby's start hologram at Rec Room's [ScoreboardProjector] (1): the party (co-op, one list, 1-4 players)
        /// and START QUEST (RrGameConsole, iter8 console), which asks the authority to start the run.
        /// </summary>
        internal void LobbyConsole(Transform parent, Pose pose)
        {
            _console = RrGameConsole.ForGame(this, parent, pose, new RrGameConsoleSpec
            {
                Title = "QUEST FOR THE GOLDEN TROPHY",
                TeamCount = 0,
                StartLabel = () => Phase == RrPhase.Lobby ? "START QUEST" : RrGameConsole.PhaseStartLabel(this)
            });
            RrLog.Info("Quest", "lobby hologram at Rec Room's [ScoreboardProjector] (" + pose.position.ToString("F1") + ", yaw " + pose.rotation.eulerAngles.y.ToString("0") + ")");
        }

        /// <summary>
        /// An act's own (ScoreboardProjector) (Rec Room has one in every act room): the party and the team's gold while
        /// the run is on; START is greyed there.
        /// </summary>
        internal void ActHologram(RrGoldenTrophyArea a, Transform parent, string subtitle)
        {
            var list = a.Markers("scoreboard");
            if (list.Count == 0)
            {
                return;
            }

            string sub = (subtitle ?? string.Empty).ToUpperInvariant();
            RrGameConsole.Create(parent, list[0].Pose, new RrGameConsoleSpec
            {
                Title = "QUEST FOR THE GOLDEN TROPHY",
                TeamCount = 0,
                MinPlayers = 1,
                MaxPlayers = Def != null && Def.MaxPlayers > 0 ? Def.MaxPlayers : 4,
                Subtitle = () => sub,
                Status = () => Def != null ? Def.Currency.ToUpperInvariant() + "  " + Score.ToString("N0") : "",
                StartLabel = () => "IN PROGRESS",
                CanStart = () => false
            });
        }

        /// <summary>A door's gather platform glows while the door is open (Rec Room's pulsing [DoorTrigger] beam).</summary>
        internal void AddGatherPlatform(RrQuestDoor door, GameObject platform)
        {
            if (door != null && platform != null)
            {
                _gatherDoors.Add(door);
                _gatherPlatforms.Add(platform);
            }
        }

        /// <summary>Harness hooks: jump to act n (1-9) or the dressing room during a run and wait for its combat.</summary>
        public static IEnumerator TestDressingRoom() { return TestAct(DressingRoom); }

        public static IEnumerator TestAct1() { return TestAct(1); }

        public static IEnumerator TestAct2() { return TestAct(2); }

        public static IEnumerator TestAct3() { return TestAct(3); }

        public static IEnumerator TestAct4() { return TestAct(4); }

        public static IEnumerator TestAct5() { return TestAct(5); }

        public static IEnumerator TestAct6() { return TestAct(6); }

        public static IEnumerator TestAct7() { return TestAct(7); }

        public static IEnumerator TestAct8() { return TestAct(8); }

        public static IEnumerator TestAct9() { return TestAct(9); }

        private static IEnumerator TestAct(int a)
        {
            var q = Current as RrGoldenTrophyRoom;
            if (q == null)
            {
                yield break;
            }

            q.DebugGoToAct(a);
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && (q.ActIndex != a || q.Stage == RrQuestStage.Intro))
            {
                yield return null;
            }

            yield return new WaitForSeconds(2f);
            RrLog.Info("Quest", "QuestTest: " + Levels[a] + " (act index " + a + ") - " + q.Describe() + ", player at " + Player.RrPlayer.FeetPosition.ToString("F1"));
        }

        // ------------------------------------------------------------------ hooks

        protected override void OnEnemyReady(RrEnemy e, int type)
        {
            if (type == King)
            {
                var k = e.gameObject.GetComponent<RrGoblinKing>();
                if (k == null)
                {
                    k = e.gameObject.AddComponent<RrGoblinKing>();
                }

                k.Setup(this, e, Mathf.Clamp(QuestInt(RrQuestInts.Party), 1, 4));
            }
        }

        protected override byte PointsKind(RrEnemy e, int type)
        {
            return type == King ? RrQuestEvents.PointsBoss : RrQuestEvents.PointsKill;
        }

        protected override void AuthorityActStep(int act)
        {
            if (act == BoulderAct && Boulder != null)
            {
                Boulder.AuthorityStep(this);
            }

            if (act == BossAct)
            {
                RrGoblinKing.AuthorityStep(this);
            }
        }

        protected override void AuthorityHazard(int playerId, int code, int arg)
        {
            if (code == HazardBoulder && ActIndex == BoulderAct && Stage == RrQuestStage.Combat && (QuestInt(RrQuestInts.Hazards) & 1) == 0 && Boulder != null)
            {
                float seconds = Boulder.Length / RrGoldenTrophyBoulder.Speed + 1.5f;
                SetQuestInt(RrQuestInts.Hazards, QuestInt(RrQuestInts.Hazards) | 1);
                SetQuestInt(RrQuestInts.HazardTime, Mathf.RoundToInt(seconds * 1000f));
                State.SetTimer(seconds);
                RrLog.Info("Quest", "boulder released by player " + playerId + " (" + seconds.ToString("0.0") + " s roll)");
            }
        }

        protected override void QuestStep(int act)
        {
            ShowAreas(Phase == RrPhase.Playing || Phase == RrPhase.MatchOver ? act : -1);
            RrPostFx.SetArea(act + 1);
            for (int i = 0; i < _gatherDoors.Count; i++)
            {
                var p = _gatherPlatforms[i];
                bool on = _gatherDoors[i] != null && _gatherDoors[i].IsOpen;
                if (p != null && p.activeSelf != on)
                {
                    p.SetActive(on);
                }
            }

            if (Boulder != null)
            {
                Boulder.ClientStep(this, act == BoulderAct && Phase == RrPhase.Playing);
            }

            if (_victoryAt > 0f)
            {
                float k = Mathf.Clamp01((Time.time - _victoryAt - 1.5f) / 1.2f);
                foreach (var c in Chests)
                {
                    RrQuestKit.OpenChest(c, k);
                }

                if (k > 0f && !_chestsSounded)
                {
                    // No chest clip is in the install: a slowed wooden creak for the lid (GUESS), then the gold.
                    _chestsSounded = true;
                    foreach (var c in Chests)
                    {
                        if (c != null)
                        {
                            RrSound.Play("Quest_Longbow_Creak", c.position, 0.8f, 0.6f);
                            RrSound.Play(Def.PointsSound, c.position + Vector3.up * 0.4f, 0.8f, 1f);
                        }
                    }
                }

                if (_trophy != null && TrophySpot != null)
                {
                    float f = Mathf.Clamp01((Time.time - _victoryAt) / 1.2f);
                    _trophy.transform.position = TrophySpot.position + Vector3.up * (4f * (1f - f * f));
                    _trophy.transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
                }
            }
        }

        protected override void OnActStage(int act, RrQuestStage stage)
        {
            if (act == BossAct && stage == RrQuestStage.Clear)
            {
                Victory();
            }
            else if (act == BoulderAct && Boulder != null && stage == RrQuestStage.Intro)
            {
                Boulder.ResetBoulder();
            }
        }

        /// <summary>Every client: the king is down - the trophy drops in front of his stage and the chests open.</summary>
        private void Victory()
        {
            _victoryAt = Time.time;
            if (TrophySpot != null && _trophy == null)
            {
                RrSpawn.Spawn("(Golden_Bucket)", TrophySpot, Vector3.zero, Quaternion.identity, inst =>
                {
                    if (inst == null)
                    {
                        return;
                    }

                    if (this == null)
                    {
                        Destroy(inst.gameObject);
                        return;
                    }

                    _trophy = inst.gameObject;
                    inst.transform.localScale = Vector3.one * 2.2f;
                }, RrSpawnOptions.Visual());
                RrFx.Spawn(Def.BossPointsFx, TrophySpot.position + Vector3.up, Quaternion.identity, 1.5f);
            }

            Hud.Toast("The Golden Trophy is yours!", 4f);
            RrLog.Info("Quest", "the Golden Trophy drops in the feast hall");
        }

        protected override void OnRunReset()
        {
            _victoryAt = -1f;
            if (_trophy != null)
            {
                Destroy(_trophy);
                _trophy = null;
            }

            _chestsSounded = false;
            foreach (var c in Chests)
            {
                RrQuestKit.OpenChest(c, 0f);
            }

            if (Boulder != null)
            {
                Boulder.ResetBoulder();
            }
        }
    }
}
