using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.CrimsonCauldron
{
    /// <summary>
    /// The Curse of the Crimson Cauldron (Goblin2, campaign 2; rr_latest_rgames_story.md 6.1, rr_quests.md 1.3 / 2.x), in
    /// basic form, built entirely from Rec Room's own Android levels 50-60 (Quest_Goblin2_*, DN/refdata/apk/Crimson_*.rrsk,
    /// shipped as Data/Crimson_*.bytes and read with <see cref="RrSceneBank"/>; DevNotes/iter13/crimson.md), in
    /// BuildSettings order, which is the play order of the final version's nine acts:
    ///
    /// | room | level | what |
    /// | --- | --- | --- |
    /// | lobby | Foyer (50) | the theatre foyer: the start hologram [ScoreboardProjector] (1), practice sword, bow, wand and potions, the merch booth |
    /// | pre-start | Armory (51) | the dressing room: 4 swords, 2 shields, 2 bows, 3 wands and a potion on Rec Room's hooks |
    /// | 1 | CastleCourtyard (52) | Castle Courtyard: goblins and a bomber on the right, bombers on the pillars, 6 shield goblins at the exit |
    /// | 2 | Forest1 (53) | Forest Path: the entry hall, the Huntsman's cabin, the goblin encampment, attacks from behind |
    /// | 3 | GoblinCamp (54) | Campsite: the campfire clearing, bombers, a hunting party from behind |
    /// | 4 | Forest2 (55) | Deep Forest: imp witches, the stairs, the bridge, the pool, the showers |
    /// | 5 | Bog (56) | Bog: the imp horde, the floats over the swamp water (a down), imp witches, the bog monster |
    /// | 6 | Mines1 (57) | Mines: steam traps (a down), the stairs, the lower level's guards and barrel knights |
    /// | 7 | BoilerRoom (58) | Deep Mines: the ramp's elites and red barrel knight, the two green knights |
    /// | 8 | BellTowerStairs (59) | Bell Tower Stairs: the ladders up the tower |
    /// | 9 | BellTowerArena (60) | Finale: the Crimson Witch (<see cref="RrCrimsonWitch"/>); falling out of the tower is a down |
    ///
    /// Measured (rule circuit 935 [Quest_Goblin2_Manager], rr_quests.md 2.1): Gold, 2,000 per down, the win stinger
    /// "Crimson Cauldron - Our Heroes Have Triumphed", the lose stinger "Crimson Cauldron - Our Heroes Have Failed"; music
    /// CrimsonCauldronTheBalladofVotaVader (lobby, boss), CrimsonCauldronExploringtheMines (explore); combat music and the
    /// act-clear fanfare reuse Golden Trophy's (GUESS, 2.7: the install has no Goblin2 combat track). Enemies: the install's
    /// goblin Maker spawners (2.2.1). Waves follow the final wiki walkthrough on each level's encounter spawn points and
    /// trigger volumes; per-enemy gold is unpublished (6.1): GUESS values about twice Golden Trophy's, sized so a clean
    /// 4-player run can pass the 300k S line. Every room is built at load behind the loading card; only the lobby or the
    /// current act is active.
    /// </summary>
    public sealed class RrCrimsonCauldronRoom : RrQuestRoom
    {
        public const int Goblin = 0, Imp = 1, EliteGoblin = 2, EliteImp = 3, Shield = 4, Bomber = 5, KnightGreen = 6, KnightRed = 7, ImpWitch = 8, BogMonster = 9, Witch = 10;

        public const string LobbySong = "CrimsonCauldronTheBalladofVotaVader";
        public const string ExploreSong = "CrimsonCauldronExploringtheMines";
        public const string CombatSong = "GoldenTrophyCombat2";

        public const int ArmoryAct = 0, BogAct = 5, MinesAct = 6, DeepMinesAct = 7, StairsAct = 8, BossAct = 9;

        /// <summary>The lobby's Rec Room scene (its data file and lighting key).</summary>
        public const string FoyerScene = "Crimson_Foyer";

        /// <summary>The lobby's anchor: the centre of the Foyer's Player1..8SpawnPoint (scene frame).</summary>
        public static readonly Vector3 FoyerAnchor = new Vector3(24.98f, -7.779f, 64.28f);

        /// <summary>Each act's Rec Room scene, in play order (act 0 is the Armory).</summary>
        public static readonly string[] ActScenes =
        {
            "Crimson_Armory", "Crimson_CastleCourtyard", "Crimson_Forest1", "Crimson_GoblinCamp", "Crimson_Forest2", "Crimson_Bog",
            "Crimson_Mines1", "Crimson_BoilerRoom", "Crimson_BellTowerStairs", "Crimson_BellTowerArena"
        };

        /// <summary>The Rec Room scene of act <paramref name="act"/> (-1 or unknown: the Foyer).</summary>
        public static string SceneOf(int act)
        {
            return act >= 0 && act < ActScenes.Length ? ActScenes[act] : FoyerScene;
        }

        /// <summary>
        /// Room-local root of act i: a 320 m grid north of the Foyer (the largest level, the bell tower arena with its
        /// landscape, spans 237 x 270 m around its arrival), 4 columns; only one area is ever active, so the grid only
        /// keeps the areas' own boxes apart, within ~1 km of the origin.
        /// </summary>
        public static Vector3 ActSlot(int i)
        {
            return new Vector3((i % 4 - 1.5f) * 320f, 0f, 320f * (1 + i / 4));
        }

        internal readonly List<RrJumbotronArea> Areas = new List<RrJumbotronArea>();

        /// <summary>The Finale's reward chests on Rec Room's four GiftBoxSpawnPoints; they open with the victory.</summary>
        internal readonly List<Transform> Chests = new List<Transform>();

        /// <summary>Per act: the area-local height below which a player has fallen out (null: no kill floor).</summary>
        internal readonly List<float?> KillFloors = new List<float?>();

        private readonly List<Pose> _lobbySpawns = new List<Pose>();
        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private readonly List<RrQuestDoor> _gatherDoors = new List<RrQuestDoor>();
        private readonly List<GameObject> _gatherPlatforms = new List<GameObject>();
        private Transform _lobbyRoot;
        private RrGameConsole _console;
        private string _envScene;
        private float _fallGuard;
        private int _fallAct = -1;
        private float _victoryAt = -1f;
        private bool _chestsSounded;

        protected override RrQuestDef CreateDef()
        {
            var d = new RrQuestDef
            {
                Name = "The Curse of the Crimson Cauldron",
                Currency = "Gold",
                DeathPenalty = 2000,
                LobbyMusic = LobbySong,
                ExploreMusic = ExploreSong,
                CombatMusic = CombatSong,
                BossMusic = LobbySong,
                ClearFanfare = "GoldenTrophyClearFanfare2",
                ClearFanfareSeconds = 4.5f,
                WinStinger = "Crimson Cauldron - Our Heroes Have Triumphed",
                LoseStinger = "Crimson Cauldron - Our Heroes Have Failed",
                PointsFx = "[VFX_GoldCoins]",
                BossPointsFx = "[VFX_BossGoldCoins]",
                MaxAlive = 10
            };
            // Kill gold: unpublished (6.1, "GUESS: the Golden Trophy values x1.5-2"): about 2x Golden Trophy's.
            d.AddEnemy("(QuestAIController)GoblinBasicGroundMelee2020", 900);
            d.AddEnemy("(QuestAIController)GoblinBasicFlyingRanged", 1000);
            d.AddEnemy("(QuestAIController)GoblinEliteGroundMelee", 1600);
            d.AddEnemy("(QuestAIController)GoblinEliteFlyingRanged", 1600);
            d.AddEnemy("(QuestAIController)GoblinBasicMeleePike", 1200);
            d.AddEnemy("(QuestAIController)GoblinBasicGroundRanged", 1200);
            d.AddEnemy("(QuestAIController)GoblinKnightCharge", 2500);
            d.AddEnemy("(QuestAIController)GoblinKnightSpin", 3500);
            // The Imp Witch (no spawner in the bundles): Rec Room's red volley flyer stands in.
            d.AddEnemy("(QuestAIController)GoblinVolley", 2500);
            // The Bog's monster (not in the bundles): a green barrel knight x1.6 (6.3 item 4 GUESS).
            d.AddEnemy("(QuestAIController)GoblinKnightCharge", 8000);
            d.AddEnemy("(QuestAIController)GoblinEliteFlyingRanged", 0);
            d.BossType = Witch;
            d.BossKillPoints = 15000;
            d.Weapons.Add("(QuestSword)");
            d.Weapons.Add("(QuestShield)");
            d.Weapons.Add("(Longbow)");
            d.Weapons.Add("(Crossbow_Hunter)");
            d.Weapons.Add("(Quest_Goblin_Wand)");
            return d;
        }

        protected override string BossName
        {
            get { return ActIndex == BogAct ? "THE BOG MONSTER" : "THE CRIMSON WITCH"; }
        }

        /// <summary>The Bog's water uses the framework's lava rule; its toast names the bog.</summary>
        protected override string LavaToast
        {
            get { return ActIndex == BogAct ? "Swallowed by the bog!" : "Burned in the lava!"; }
        }

        protected override Pose LobbySpawn(int index)
        {
            if (_lobbySpawns.Count > 0)
            {
                var p = _lobbySpawns[index % _lobbySpawns.Count];
                return new Pose(p.position + p.rotation * new Vector3(0f, 0f, -1.2f * (index / _lobbySpawns.Count)), p.rotation);
            }

            return new Pose(new Vector3((index % 4 - 1.5f) * 1.2f, 0.05f, 0f), Quaternion.Euler(0f, -90f, 0f));
        }

        // ------------------------------------------------------------------ build

        /// <summary>
        /// Every room is built here, behind the loading card (the framework numbers pots and racks in build order, so
        /// building an act later would renumber them); only the lobby, or the current act, stays active.
        /// </summary>
        protected override IEnumerator BuildLevel()
        {
            if (GetComponent<RrJumbotronHull.Owned>() == null)
            {
                gameObject.AddComponent<RrJumbotronHull.Owned>();
            }

            _lobbyRoot = RrBuild.Group(transform, "Foyer").transform;
            yield return RrCrimsonCauldronActs.Foyer(this, _lobbyRoot, _lobbySpawns, _surfaces);
            BuildProgress = 0.16f;
            var builders = RrCrimsonCauldronActs.All();
            var acts = new List<RrQuestAct>();
            for (int i = 0; i < builders.Length; i++)
            {
                var root = RrBuild.Group(transform, i == ArmoryAct ? "Armory" : "Act " + i, ActSlot(i)).transform;
                RrQuestAct act = null;
                yield return builders[i](this, root, _surfaces, x => act = x);
                if (act == null)
                {
                    RrLog.Warn("CrimsonCauldron", ActScenes[i] + " did not build");
                    act = new RrQuestAct { Title = "ACT " + i, Subtitle = "(missing)", Root = root, Origin = root.localPosition, Area = new Bounds(Vector3.zero, new Vector3(40f, 10f, 40f)) };
                }

                acts.Add(act);
                while (Areas.Count < acts.Count)
                {
                    Areas.Add(null);
                }

                while (KillFloors.Count < acts.Count)
                {
                    KillFloors.Add(null);
                }

                BuildProgress = 0.16f + 0.72f * (i + 1) / builders.Length;
                yield return null;
            }

            foreach (var act in acts)
            {
                Def.AddAct(act);
            }

            foreach (var act in acts)
            {
                int i = Def.Acts.IndexOf(act);
                RrLog.Info("CrimsonCauldron", "act " + i + " (" + SceneOf(i) + ") \"" + act.Title + " / " + act.Subtitle + "\" root " + act.Root.localPosition.ToString("F0") + ", entry "
                    + act.Entry.ToString("F1") + " yaw " + act.EntryYaw.ToString("0") + ", " + act.Waves.Count + " waves + " + act.SideWaves.Count + " side, " + act.Groups.Count
                    + " groups, " + act.Lava.Count + " lava, exit " + (act.ExitDoor != null ? "door" : "none") + (act.Prelude ? ", prelude" : "") + (act.Boss ? ", boss" : ""));
            }

            ShowAreas(-1);
        }

        internal void RegisterRack(RrQuestRack rack)
        {
            if (rack != null)
            {
                Racks.Add(rack);
            }
        }

        /// <summary>An act's area and kill floor, by act index (the builders call it in order).</summary>
        internal void AddArea(RrJumbotronArea a, float? killFloor)
        {
            Areas.Add(a);
            KillFloors.Add(killFloor);
        }

        /// <summary>A door's gather pad glows while the door is open (Rec Room's pulsing [DoorTrigger] beam).</summary>
        internal void AddGatherPlatform(RrQuestDoor door, GameObject platform)
        {
            if (door != null && platform != null)
            {
                _gatherDoors.Add(door);
                _gatherPlatforms.Add(platform);
            }
        }

        /// <summary>
        /// The lobby's start hologram at Rec Room's [ScoreboardProjector] (1): the party (co-op, one list, 1-4 players) and
        /// START QUEST (<see cref="RrGameConsole"/>), which asks the authority to start the run.
        /// </summary>
        internal void LobbyConsole(Transform parent, Pose pose)
        {
            _console = RrGameConsole.ForGame(this, parent, pose, new RrGameConsoleSpec
            {
                Title = "THE CURSE OF THE CRIMSON CAULDRON",
                TeamCount = 0,
                MinPlayers = 1,
                MaxPlayers = 4,
                StartLabel = () => Phase == RrPhase.Lobby ? "START QUEST" : RrGameConsole.PhaseStartLabel(this)
            });
            RrLog.Info("CrimsonCauldron", "lobby hologram at Rec Room's [ScoreboardProjector] (" + pose.position.ToString("F1") + ", yaw " + pose.rotation.eulerAngles.y.ToString("0") + ")");
        }

        internal void LobbyStartButton(Transform parent, Vector3 local, float yaw)
        {
            StartButton(parent, local, yaw);
        }

        /// <summary>Only the lobby (outside a run) or the current act is active (the Jumbotron's rule: one room at a time).</summary>
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
                bool on = inRun && i == cur;
                if (root != null && root.gameObject.activeSelf != on)
                {
                    root.gameObject.SetActive(on);
                }
            }
        }

        // ------------------------------------------------------------------ hooks

        protected override void OnEnemyReady(RrEnemy e, int type)
        {
            int party = Mathf.Clamp(QuestInt(RrQuestInts.Party), 1, 4);
            if (type == Witch)
            {
                var w = e.gameObject.GetComponent<RrCrimsonWitch>();
                if (w == null)
                {
                    w = e.gameObject.AddComponent<RrCrimsonWitch>();
                }

                w.Setup(e, party);
            }
            else if (type == BogMonster && !Mathf.Approximately(e.transform.localScale.x, 1.6f))
            {
                // "A lowly bog monster will rise up from its home" (WIKI): a green barrel knight x1.6, 3x its health.
                e.transform.localScale = Vector3.one * 1.6f;
                e.Height *= 1.6f;
                e.Radius *= 1.6f;
                e.MaxHealth = Mathf.RoundToInt(e.MaxHealth * 3f * (0.55f + 0.15f * party));
                e.Health = e.MaxHealth;
                e.Damage = 40;
                e.AggroRange = 50f;
                if (Hud != null)
                {
                    Hud.Toast("A lowly bog monster rises from its home!", 3.5f);
                }
            }
        }

        protected override byte PointsKind(RrEnemy e, int type)
        {
            return type == Witch || type == BogMonster ? RrQuestEvents.PointsBoss : RrQuestEvents.PointsKill;
        }

        protected override void AuthorityActStep(int act)
        {
            if (act == BossAct)
            {
                RrCrimsonWitch.AuthorityStep(this);
            }
            else if (act == BogAct)
            {
                BogMonsterBar();
            }
        }

        /// <summary>Authority: the bog monster's health bar while it lives.</summary>
        private void BogMonsterBar()
        {
            int bar = 0;
            for (int s = 0; s < RrQuestEnemies.Slots; s++)
            {
                var e = Enemies.At(s);
                if (e != null && e.Alive && e.Type == BogMonster)
                {
                    bar = Mathf.Clamp(Mathf.CeilToInt(1000f * e.Health / Mathf.Max(1, e.MaxHealth)), 1, 1000);
                    break;
                }
            }

            if (QuestInt(RrQuestInts.BossHealth) != bar)
            {
                SetQuestInt(RrQuestInts.BossHealth, bar);
            }
        }

        protected override void QuestStep(int act)
        {
            bool run = Phase == RrPhase.Playing || Phase == RrPhase.MatchOver;
            ShowAreas(run ? act : -1);
            ShowLighting(run ? act : -1);
            for (int i = 0; i < _gatherDoors.Count; i++)
            {
                var p = _gatherPlatforms[i];
                bool on = _gatherDoors[i] != null && _gatherDoors[i].IsOpen;
                if (p != null && p.activeSelf != on)
                {
                    p.SetActive(on);
                }
            }

            if (Phase == RrPhase.Playing)
            {
                FallCheck(act);
            }

            OpenChests();
        }

        /// <summary>
        /// Every client, after the witch falls: the reward chests on the four GiftBoxSpawnPoints open (Rec Room hands out
        /// a gift box per player there; its chest prefabs are not in the plain bundles, rr_quests.md 5.2) with the gold.
        /// </summary>
        private void OpenChests()
        {
            if (_victoryAt < 0f)
            {
                return;
            }

            float k = Mathf.Clamp01((Time.time - _victoryAt - 1.5f) / 1.2f);
            foreach (var c in Chests)
            {
                RrQuestKit.OpenChest(c, k);
            }

            if (k > 0f && !_chestsSounded)
            {
                _chestsSounded = true;
                foreach (var c in Chests)
                {
                    if (c != null)
                    {
                        RrSound.Play(Def.BossPointsSound, c.position + Vector3.up * 0.4f, 0.8f, 1f);
                        RrFx.Spawn(Def.PointsFx, c.position + Vector3.up * 0.6f, Quaternion.identity, 1f);
                    }
                }
            }
        }

        /// <summary>
        /// The room's own lighting for where the team is: the Foyer in the lobby, else the current act's scene (its
        /// records, <see cref="RrEnvironment.SetArea"/>), and the grade of area act + 1. Both calls are cheap when nothing
        /// changes.
        /// </summary>
        private void ShowLighting(int act)
        {
            string scene = SceneOf(act);
            if (scene != _envScene)
            {
                _envScene = scene;
                RrLog.Info("CrimsonCauldron", "lighting: " + scene + (act >= 0 ? " (act " + act + ")" : " (lobby)"));
            }

            RrEnvironment.SetArea(scene);
            RrPostFx.SetArea(act + 1);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_envScene != null && RrEnvironment.Activity == RrActivity.CrimsonCauldron)
            {
                RrEnvironment.SetArea(null);
            }

            _envScene = null;
            RrCrimsonWitch.Clear();
        }

        /// <summary>
        /// Below the act's kill floor (the bell tower's PitKillzone: falling out of a window): the local player is down
        /// and back at the act's entry.
        /// </summary>
        private void FallCheck(int act)
        {
            var a = CurrentAct;
            var h = RrHealth.Local;
            if (a == null || a.Root == null || act < 0 || act >= KillFloors.Count || !KillFloors[act].HasValue)
            {
                return;
            }

            if (act != _fallAct)
            {
                _fallAct = act;
                _fallGuard = Time.time + 2f;
            }

            if (Time.time < _fallGuard)
            {
                return;
            }

            Vector3 local = a.Root.InverseTransformPoint(RrPlayer.FeetPosition);
            if (local.y > KillFloors[act].Value || Mathf.Abs(local.x - a.Area.center.x) > a.Area.extents.x + 30f || Mathf.Abs(local.z - a.Area.center.z) > a.Area.extents.z + 30f)
            {
                return;
            }

            _fallGuard = Time.time + 2f;
            if (h != null && h.InCombat && !h.IsOut && !h.Invincible)
            {
                h.GoOut(RrCombat.NoPlayer, RrCombat.EnemyTool, RrPlayer.FeetPosition);
            }

            var p = ActEntryWorld(act, Rank());
            RrPlayer.Teleport(p.position, p.rotation.eulerAngles.y);
            Hud.Toast("You fell from the tower!", 2f);
            RrLog.Info("CrimsonCauldron", "local player fell below the kill floor in act " + act);
        }

        protected override void OnActStage(int act, RrQuestStage stage)
        {
            if (act == ArmoryAct && stage == RrQuestStage.Clear)
            {
                Hud.Toast("Grab your weapons, then gather on the glowing pad by the door", 4f);
            }
            else if (act == BogAct && stage == RrQuestStage.Intro)
            {
                Hud.Toast("Don't touch the bog water! Cross on the floats", 4f);
            }
            else if ((act == MinesAct || act == DeepMinesAct) && stage == RrQuestStage.Intro)
            {
                Hud.Toast("Watch out: the steam kills you!", 4f);
            }
            else if (act == StairsAct && stage == RrQuestStage.Intro)
            {
                Hud.Toast("Touch a ladder to climb it", 3.5f);
            }
            else if (act == BossAct && stage == RrQuestStage.Clear)
            {
                Hud.Toast("The Crimson Witch is defeated! The curse is lifted!", 5f);
                RrLog.Info("CrimsonCauldron", "the Crimson Witch is defeated; " + Chests.Count + " reward chests open");
                _victoryAt = Time.time;
            }
        }

        protected override void OnRunReset()
        {
            _fallAct = -1;
            _victoryAt = -1f;
            _chestsSounded = false;
            foreach (var c in Chests)
            {
                RrQuestKit.OpenChest(c, 0f);
            }
        }

        // ------------------------------------------------------------------ harness hooks

        /// <summary>Harness: presses the Foyer hologram's START as a player would (logs whether it was pressable).</summary>
        public static void TestConsoleStart()
        {
            var q = Current as RrCrimsonCauldronRoom;
            if (q == null)
            {
                RrLog.Warn("CrimsonCauldron", "CrimsonTest: no Crimson Cauldron room");
                return;
            }

            bool ok = q._console != null && q._console.Press("test");
            RrLog.Info("CrimsonCauldron", "CrimsonTest: console START " + (ok ? "pressed" : "not pressable") + (q._console != null ? " (" + q._console.StartText + ", " + q._console.ShownPlayers + " player row(s))" : " (no console)"));
        }

        /// <summary>Harness: clears the current act (every enemy dies, every wave counts as spawned): its door opens.</summary>
        public static void TestClearAct()
        {
            var q = Current as RrCrimsonCauldronRoom;
            if (q != null)
            {
                q.DebugClearAct();
                RrLog.Info("CrimsonCauldron", "CrimsonTest: cleared act " + q.ActIndex + " (" + SceneOf(q.ActIndex) + ")");
            }
        }

        public static IEnumerator TestAct1() { return TestAct(1); }

        public static IEnumerator TestAct2() { return TestAct(2); }

        public static IEnumerator TestAct3() { return TestAct(3); }

        public static IEnumerator TestAct4() { return TestAct(4); }

        public static IEnumerator TestAct5() { return TestAct(5); }

        public static IEnumerator TestAct6() { return TestAct(6); }

        public static IEnumerator TestAct7() { return TestAct(7); }

        public static IEnumerator TestAct8() { return TestAct(8); }

        public static IEnumerator TestBoss() { return TestAct(BossAct); }

        /// <summary>Harness: jump to act n (0 Armory .. 9 Finale) during a run and wait for its combat.</summary>
        private static IEnumerator TestAct(int a)
        {
            var q = Current as RrCrimsonCauldronRoom;
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
            RrLog.Info("CrimsonCauldron", "CrimsonTest: act " + a + " (" + SceneOf(a) + ") - " + q.Describe() + ", player at " + RrPlayer.FeetPosition.ToString("F1"));
        }

        /// <summary>
        /// Harness: fights the current act to its clear as a player would (max 90 s): every 0.4 s each living enemy takes
        /// a 400-damage hit from the local player; with nothing alive the player walks into the next Enter wave's zone.
        /// </summary>
        public static IEnumerator TestFightClear()
        {
            var q = Current as RrCrimsonCauldronRoom;
            if (q == null || q.CurrentAct == null)
            {
                yield break;
            }

            int act = q.ActIndex;
            float end = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < end && q.ActIndex == act && q.Stage != RrQuestStage.Clear && q.Phase == RrPhase.Playing)
            {
                int hit = 0;
                for (int s = 0; s < RrQuestEnemies.Slots; s++)
                {
                    var e = q.Enemies.At(s);
                    if (e != null && e.Alive && e.State != RrEnemyState.Spawning)
                    {
                        e.TakeLocalHit(400, RrBodyPart.WeakSpot, e.Center, RrCombat.UnknownTool);
                        hit++;
                    }
                }

                if (hit == 0 && q.Enemies.Alive == 0)
                {
                    q.TestIntoNextZone();
                }

                yield return new WaitForSeconds(0.4f);
            }

            RrLog.Info("CrimsonCauldron", "CrimsonTest: fight " + (q.Stage == RrQuestStage.Clear ? "CLEAR" : "NOT CLEAR") + " in act " + act + " - " + q.Describe());
        }

        private bool TestIntoNextZone()
        {
            var act = CurrentAct;
            if (act == null || act.Root == null)
            {
                return false;
            }

            for (int i = QuestInt(RrQuestInts.Wave); i < act.Waves.Count; i++)
            {
                var w = act.Waves[i];
                if (w.Trigger != RrQuestWave.When.Enter)
                {
                    continue;
                }

                Vector3 top = act.Root.TransformPoint(new Vector3(w.Zone.center.x, w.Zone.max.y, w.Zone.center.z));
                RaycastHit hit;
                Vector3 feet = Physics.Raycast(top, Vector3.down, out hit, w.Zone.size.y + 4f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore)
                    ? hit.point + Vector3.up * 0.05f
                    : act.Root.TransformPoint(w.Zone.center);
                RrPlayer.Teleport(feet, act.Root.eulerAngles.y + act.EntryYaw);
                return true;
            }

            return false;
        }
    }
}
