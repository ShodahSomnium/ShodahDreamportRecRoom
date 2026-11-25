using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron
{
    /// <summary>
    /// The Rise of Jumbotron (SciFi1, campaign 1; rr_quests.md 1.5 / 4.4, rr_latest_rgames_story.md 9): Jumbotron and his
    /// robots have taken over Coach's space ship, the SS Coach. Built entirely from Rec Room's own Android levels
    /// 38-48 (Quest_Scifi_*, DN/refdata/apk/Jumbotron_*.rrsk, <see cref="RrJumbotronArea"/>), in BuildSettings order,
    /// which is the play order (each room's exit sign names the next room):
    ///
    /// | room | level | what |
    /// | --- | --- | --- |
    /// | lobby | Foyer (38) | the theatre foyer: the start hologram [ScoreboardProjector] (1), a practice range, the Rec Center doors |
    /// | pre-start | Armory (39) | the "ARMORY" dressing room: mirror, couch, 4 laser pistols + 4 burst rifles on the wall |
    /// | 1 | Battlefield1 (40) | the SS Coach's launch bay, bat bots between the pillars (exit sign CARGO BAY) |
    /// | 2 | Hallway1 (41) | Cargo Bay: tread and mouse bots, the shotgun side room |
    /// | 3 | Hallway2 (42) | Engineering (SERVERS): waves down and up the ramps |
    /// | 4 | Reception (43) | the Bridge: assassin bots, mouse tunnels |
    /// | 5 | StadiumConcession (44) | the Flight Deck ("Departure"): the green button starts the attack |
    /// | 6 | StadiumEntry (45) | Space, outside the JUMBODOME |
    /// | 7 | Garage (46) | the Hangar: elite bat bots |
    /// | 8 | Cargo1 (47) | Supply: Kevin the goblin's room, the hardest final room |
    /// | 9 | Stadium (48) | the Jumbodome: Jumbotron (<see cref="RrJumbotronBoss"/>) |
    ///
    /// Measured (rule circuit f8 [Quest_SciFi1_Manager], rr_quests.md 2.1): currency Credits, 1,200 lost per down,
    /// stingers "JumboTron - Our Heroes Have Triumphed" / "... Have Failed", credit VFX [VFX_CreditChips], music
    /// JumboTronExplore / JumboTronCombat1 (no act-clear fanfare in the install for this quest), breakables (SciFatPot)
    /// 50 credits, magazines pistol 5 / burst 22 / shotgun 3 / rail gun 3 (RrWeapon reads them). Enemies: the install's
    /// six sci-fi Maker spawners. Waves follow the final wiki walkthrough (9.1) on each level's EncountersV2 spawn points
    /// and trigger volumes; per-bot credits are unpublished (9.3 item 4): GUESS values sized so a clean 4-player run
    /// can pass the 150k S line.
    /// </summary>
    public sealed class RrJumbotronRoom : RrQuestRoom
    {
        public const int Tread = 0, Bat = 1, Mouse = 2, Assassin = 3, EliteBat = 4, EliteTread = 5, Boss = 6, Goblin = 7;

        public const string Pistol = "(Quest_SciFi_Pistol)";
        public const string Burst = "(Quest_SciFi_AutomaticGun)";
        public const string Shotgun = "(Quest_SciFi_Shotgun)";
        public const string RailGun = "(Quest_SciFi_RailGun)";

        public const string ExploreMusic = "JumboTronExplore";
        public const string CombatMusic = "JumboTronCombat1";

        public const int ArmoryAct = 0, FlightDeckAct = 5, SpaceAct = 6, BossAct = 9;

        /// <summary>Hazard code of the Flight Deck's big green button.</summary>
        public const int HazardLaunch = 1;

        /// <summary>
        /// Top of Rec Room's [QuestKillFloor] (level49 Quest_Scifi_Additive: a 523 x 10 x 526 m box at scene y -22.2):
        /// below it a player is down ("lethal fall damage" off the Flight Deck).
        /// </summary>
        public const float KillFloorY = -17.2f;

        /// <summary>The acts' Rec Room level anchors (scene point = act-local origin): each room's arrival centre.</summary>
        private static readonly Vector3 FoyerAnchor = new Vector3(40.05f, -6.51f, 54.64f);

        /// <summary>The lobby's Rec Room scene (its lighting row in RrEnvironmentTable, and the kit scene's name).</summary>
        public const string FoyerScene = "Jumbotron_Foyer";

        /// <summary>
        /// Each act's Rec Room scene, in play order (act 0 is the Armory). The room names its lighting while an act is
        /// current (<see cref="RrEnvironment.SetArea"/>). Iteration 10 found that the automatic pick lit acts 1, 2, 5 and 8
        /// with the Foyer's violet environment, because their arrivals lie 0.4-5 m outside the kit's registered bounds.
        /// </summary>
        private static readonly string[] ActScenes =
        {
            "Jumbotron_Armory", "Jumbotron_Battlefield1", "Jumbotron_Hallway1", "Jumbotron_Hallway2", "Jumbotron_Reception",
            "Jumbotron_StadiumConcession", "Jumbotron_StadiumEntry", "Jumbotron_Garage", "Jumbotron_Cargo1", "Jumbotron_Stadium"
        };

        /// <summary>The Rec Room scene whose lighting applies in act <paramref name="act"/> (-1 or unknown: the Foyer).</summary>
        public static string SceneOf(int act)
        {
            return act >= 0 && act < ActScenes.Length ? ActScenes[act] : FoyerScene;
        }

        internal RrJumbotronBoss Jumbotron;
        internal readonly List<RrJumbotronArea> Areas = new List<RrJumbotronArea>();
        private readonly List<RrJumbotronWeaponSpot> _spots = new List<RrJumbotronWeaponSpot>();
        private readonly List<Pose> _lobbySpawns = new List<Pose>();
        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private Transform _lobbyRoot;
        private float _fallGuard;
        private int _fallAct = -1;
        private bool _launchShown;
        private float _arrivalToastAt = -1f;
        private string _envScene;

        protected override RrQuestDef CreateDef()
        {
            var d = new RrQuestDef
            {
                Name = "The Rise of Jumbotron",
                Currency = "Credits",
                DeathPenalty = 1200,
                LobbyMusic = ExploreMusic,
                ExploreMusic = ExploreMusic,
                CombatMusic = CombatMusic,
                BossMusic = CombatMusic,
                ClearFanfare = null,
                WinStinger = "JumboTron - Our Heroes Have Triumphed",
                LoseStinger = "JumboTron - Our Heroes Have Failed",
                PointsFx = "[VFX_CreditChips]",
                BossPointsFx = "[VFX_CreditChips]",
                PointsSound = "Quest_Scifi_collectable_collected02",
                BossPointsSound = "Quest_Scifi_collectable_collected02",
                MaxAlive = 10
            };
            // Kill credits: unpublished (rr_latest_rgames_story.md 9.3 item 4), GUESS by the kill cost of 1.5 (a basic
            // bot = 1 shotgun hit, an elite flyer 2, an elite ground bot 3) - about 180k for a clean 4-player run.
            d.AddEnemy("(QuestAIController)SciFiBasicGroundRanged", 700);
            d.AddEnemy("(QuestAIController)SciFiBasicFlyingRanged2020", 700);
            d.AddEnemy("(QuestAIController)SciFiGroundExplosive", 300);
            d.AddEnemy("(QuestAIController)SciFiKnightAssassin", 2500);
            d.AddEnemy("(QuestAIController)SciFiEliteFlyingRanged", 1400);
            d.AddEnemy("(QuestAIController)SciFiEliteGroundRanged", 2000);
            d.AddEnemy("(QuestAIController)SciFiKnightAssassin", 0);
            // Supply's side room: Kevin, the goblin from Golden Trophy (the wiki's Easter egg).
            d.AddEnemy("(QuestAIController)GoblinBasicGroundMelee2020", 450);
            d.BossType = Boss;
            d.BossKillPoints = 10000;
            d.Weapons.Add(Pistol);
            d.Weapons.Add(Burst);
            d.Weapons.Add(Shotgun);
            d.Weapons.Add(RailGun);
            return d;
        }

        protected override string BossName
        {
            get { return "JUMBOTRON"; }
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

        /// <summary>
        /// Room-local root of act i: a 250 m grid north of the Foyer (the largest room, Engineering, spans 88 x 79 m; the
        /// Space act is cut to 160 x 180 m), 4 columns, so no two rooms overlap and all stay within ~850 m of the origin.
        /// </summary>
        public static Vector3 ActSlot(int i)
        {
            return new Vector3((i % 4 - 1.5f) * 250f, 0f, 250f * (1 + i / 4));
        }

        // ------------------------------------------------------------------ build

        /// <summary>
        /// Every room is built here, behind the loading card (the framework numbers pots in build order, so building an
        /// act later would renumber them); only the lobby, or the current act, stays active (<see cref="ShowAreas"/>).
        /// </summary>
        protected override IEnumerator BuildLevel()
        {
            if (GetComponent<RrJumbotronHull.Owned>() == null)
            {
                gameObject.AddComponent<RrJumbotronHull.Owned>();
            }

            _lobbyRoot = RrBuild.Group(transform, "Foyer").transform;
            yield return RrJumbotronActs.Foyer(this, _lobbyRoot, FoyerAnchor, _lobbySpawns, _surfaces);
            BuildProgress = 0.16f;
            var builders = RrJumbotronActs.All();
            var acts = new List<RrQuestAct>();
            for (int i = 0; i < builders.Length; i++)
            {
                var root = RrBuild.Group(transform, "Act " + i, ActSlot(i)).transform;
                RrQuestAct act = null;
                yield return builders[i](this, root, _surfaces, x => act = x);
                if (act == null)
                {
                    RrLog.Warn("Jumbotron", "room " + i + " did not build");
                    act = new RrQuestAct { Title = "ACT " + i, Subtitle = "(missing)", Root = root, Origin = root.localPosition, Area = new Bounds(Vector3.zero, new Vector3(40f, 10f, 40f)) };
                }

                acts.Add(act);
                BuildProgress = 0.16f + 0.72f * (i + 1) / builders.Length;
                yield return null;
            }

            foreach (var act in acts)
            {
                Def.AddAct(act);
            }

            foreach (var act in acts)
            {
                RrLog.Info("Jumbotron", "act " + Def.Acts.IndexOf(act) + " \"" + act.Title + " / " + act.Subtitle + "\" root " + act.Root.localPosition.ToString("F0") + ", entry "
                    + act.Entry.ToString("F1") + " yaw " + act.EntryYaw.ToString("0") + ", " + act.Waves.Count + " waves + " + act.SideWaves.Count + " side, " + act.Groups.Count
                    + " groups, exit " + (act.ExitDoor != null ? "door" : "none"));
            }

            ShowAreas(-1);
        }

        internal void AddSpot(RrJumbotronWeaponSpot s)
        {
            if (s != null)
            {
                _spots.Add(s);
            }
        }

        internal void LobbyConsole(Transform parent, Pose pose)
        {
            RrGameConsole.ForGame(this, parent, pose, new RrGameConsoleSpec
            {
                Title = "THE RISE OF JUMBOTRON",
                TeamCount = 0,
                MinPlayers = 1,
                MaxPlayers = 4,
                StartLabel = () => Phase == RrPhase.Lobby ? "START QUEST" : Phase == RrPhase.Countdown ? "STARTING" : "IN PROGRESS"
            });
        }

        internal void LobbyStartButton(Transform parent, Vector3 local, float yaw)
        {
            StartButton(parent, local, yaw);
        }

        /// <summary>
        /// Only the lobby (outside a run) or the current act is active. Iteration 11: the next act used to be on as well, but
        /// the slots are only 250 m apart and Space (act 6) is open sky, so the Hangar's outside (one-sided walls culled,
        /// its props and floors not) floated in view, and any gap in a hull showed the next room. The act's teleport and
        /// this switch run in the same tick (RrQuestRoom.ActWatch, then QuestStep), and a room's light grid builds in
        /// 8-25 ms of 2.5 ms slices after it is switched on (iteration 10 log), so nothing is needed ahead of the arrival.
        /// </summary>
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
            if (type == Boss && Jumbotron != null)
            {
                Jumbotron.Setup(e, Mathf.Clamp(QuestInt(RrQuestInts.Party), 1, 4));
            }
        }

        protected override byte PointsKind(RrEnemy e, int type)
        {
            return type == Boss ? RrQuestEvents.PointsBoss : RrQuestEvents.PointsKill;
        }

        protected override void AuthorityActStep(int act)
        {
            if (act == BossAct && Jumbotron != null)
            {
                Jumbotron.AuthorityStep(this);
            }
        }

        protected override void AuthorityHazard(int playerId, int code, int arg)
        {
            if (code == HazardLaunch && ActIndex == FlightDeckAct && (QuestInt(RrQuestInts.Hazards) & 1) == 0)
            {
                SetQuestInt(RrQuestInts.Hazards, QuestInt(RrQuestInts.Hazards) | 1);
                RrLog.Info("Jumbotron", "the Flight Deck's green button was pressed by player " + playerId);
            }
        }

        internal void PressLaunch()
        {
            if (Phase == RrPhase.Playing && ActIndex == FlightDeckAct)
            {
                Ask(RrQuestEvents.ReqHazard, (byte)HazardLaunch, 0);
            }
        }

        protected override void QuestStep(int act)
        {
            ShowAreas(act);
            ShowLighting(act);
            bool playing = Phase == RrPhase.Playing;
            if (Jumbotron != null)
            {
                bool inBoss = act == BossAct && (playing || Phase == RrPhase.MatchOver);
                Jumbotron.ClientStep(this, inBoss && Stage != RrQuestStage.Intro, inBoss && (Stage == RrQuestStage.Clear || QuestInt(RrQuestInts.Outcome) == 1));
            }

            if (playing && act == FlightDeckAct && !_launchShown && (QuestInt(RrQuestInts.Hazards) & 1) != 0)
            {
                // Rec Room plays a projector film of the ship flying to the Jumbodome (not in the install): the shuttle's
                // lights and the toast stand in for it.
                _launchShown = true;
                Hud.Toast("Blast off! Next stop: the Jumbodome", 4f);
            }

            if (_arrivalToastAt > 0f && Time.time >= _arrivalToastAt)
            {
                _arrivalToastAt = -1f;
                Hud.Toast("Now arriving. All units, please depart the escape vessel.", 4.5f);
            }

            if (playing)
            {
                FallCheck(act);
            }
        }

        /// <summary>
        /// The room's own lighting and grade for where the team is: the Foyer in the lobby, else the current act's scene.
        /// Both calls are cheap when nothing changes. The act's teleport runs in the same tick (RrQuestRoom.ActWatch
        /// before QuestStep), so the lighting switches with the arrival. The lighting is Rec Room's table row
        /// (<see cref="RrEnvironment.SetArea"/>). The grade is RrPostFx area act + 1: area 0 is the Foyer, area 1 the
        /// Armory, and the other areas use the levels' Scifi_Default_SSFX_Profile (RrPostFxProfiles).
        /// </summary>
        private void ShowLighting(int act)
        {
            string scene = SceneOf(act);
            if (scene != _envScene)
            {
                _envScene = scene;
                RrLog.Info("Jumbotron", "lighting: " + scene + (act >= 0 ? " (act " + act + ")" : " (lobby)"));
            }

            RrEnvironment.SetArea(scene);
            RrPostFx.SetArea(act + 1);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // Travel's RrEnvironment.Leave clears the area anyway. This covers a room that goes away while its activity is
            // still the current one, and it never touches another room's lighting.
            if (_envScene != null && RrEnvironment.Activity == RrActivity.Jumbotron)
            {
                RrEnvironment.SetArea(null);
            }

            _envScene = null;
        }

        /// <summary>Below Rec Room's kill floor (level49): the local player is down and back at the act's entry.</summary>
        private void FallCheck(int act)
        {
            var a = CurrentAct;
            var h = RrHealth.Local;
            if (a == null || a.Root == null || act < 0 || act >= Areas.Count)
            {
                return;
            }

            if (act != _fallAct)
            {
                // A new act: the teleport into it lands a frame or two later; judge only after 2 s there.
                _fallAct = act;
                _fallGuard = Time.time + 2f;
            }

            if (Time.time < _fallGuard)
            {
                return;
            }

            Vector3 local = a.Root.InverseTransformPoint(RrPlayer.FeetPosition);
            float sceneY = local.y + Areas[act].Anchor.y;
            // Only over the act itself (a player being moved between acts is elsewhere for a frame).
            if (sceneY > KillFloorY || Mathf.Abs(local.x - a.Area.center.x) > a.Area.extents.x + 30f || Mathf.Abs(local.z - a.Area.center.z) > a.Area.extents.z + 30f)
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
            Hud.Toast("Lost in space!", 2f);
            RrLog.Info("Jumbotron", "local player fell below the kill floor in act " + act);
        }

        protected override void OnActStage(int act, RrQuestStage stage)
        {
            if (act == ArmoryAct && stage == RrQuestStage.Clear)
            {
                Hud.Toast("Grab a weapon, then step onto the pad by the door", 4f);
            }
            else if (act == FlightDeckAct && stage == RrQuestStage.Clear)
            {
                _arrivalToastAt = Time.time + 1.5f;
            }
            else if (act == FlightDeckAct && stage == RrQuestStage.Intro)
            {
                _launchShown = false;
            }
            else if (act == SpaceAct && stage == RrQuestStage.Intro)
            {
                // Rec Room muffles every sound in Space (voice chat too); the client's voice is not ours to touch.
                Hud.Toast("In space, no one can hear you cheer", 3f);
            }
            else if (act == BossAct && stage == RrQuestStage.Clear)
            {
                Hud.Toast("Jumbotron is offline!", 4f);
                RrLog.Info("Jumbotron", "Jumbotron is defeated");
            }
        }

        protected override void OnRunReset()
        {
            _launchShown = false;
            _arrivalToastAt = -1f;
            if (Jumbotron != null)
            {
                Jumbotron.ResetBoss();
            }

            foreach (var s in _spots)
            {
                if (s != null)
                {
                    s.ResetSpot();
                }
            }
        }

        // ------------------------------------------------------------------ harness hooks

        /// <summary>Harness: jump to room n (0 Armory .. 9 Jumbotron) during a run and wait for its combat.</summary>
        public static IEnumerator TestAct1() { return TestAct(1); }

        public static IEnumerator TestAct2() { return TestAct(2); }

        public static IEnumerator TestAct3() { return TestAct(3); }

        public static IEnumerator TestAct4() { return TestAct(4); }

        public static IEnumerator TestAct5() { return TestAct(5); }

        public static IEnumerator TestAct6() { return TestAct(6); }

        public static IEnumerator TestAct7() { return TestAct(7); }

        public static IEnumerator TestAct8() { return TestAct(8); }

        public static IEnumerator TestBoss() { return TestAct(BossAct); }

        private static IEnumerator TestAct(int a)
        {
            var q = Current as RrJumbotronRoom;
            if (q == null)
            {
                yield break;
            }

            q.DebugGoToAct(a);
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && (q.ActIndex != a || q.Stage != RrQuestStage.Combat))
            {
                yield return null;
            }

            yield return new WaitForSeconds(2f);
            RrLog.Info("Jumbotron", "JumboTest: act " + a + " - " + q.Describe() + ", player at " + RrPlayer.FeetPosition.ToString("F1"));
        }

        /// <summary>
        /// Harness: walks the local player into the zone of the current act's next Enter wave (onto the floor under
        /// the zone's centre) and waits (max 10 s) until its bots are in, so a shot can show them.
        /// </summary>
        public static IEnumerator TestFightSpawn()
        {
            var q = Current as RrJumbotronRoom;
            if (q == null || q.CurrentAct == null)
            {
                yield break;
            }

            bool moved = q.TestIntoNextZone();
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && q.Enemies.Alive == 0)
            {
                yield return null;
            }

            yield return new WaitForSeconds(2.5f);
            RrLog.Info("Jumbotron", "JumboTest: fight spawn (moved " + moved + ") - " + q.Describe() + ", player at " + RrPlayer.FeetPosition.ToString("F1"));
        }

        /// <summary>
        /// Harness: fights the current act to its clear as a player would (max 60 s): every 0.4 s each living bot
        /// takes a 400-damage hit from the local player; with nothing alive the player walks into the next Enter
        /// wave's zone. Logs whether the act cleared, with the kills and credits.
        /// </summary>
        public static IEnumerator TestFightClear()
        {
            var q = Current as RrJumbotronRoom;
            if (q == null || q.CurrentAct == null)
            {
                yield break;
            }

            int act = q.ActIndex;
            float end = Time.realtimeSinceStartup + 60f;
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

            RrLog.Info("Jumbotron", "JumboTest: fight " + (q.Stage == RrQuestStage.Clear ? "CLEAR" : "NOT CLEAR") + " in act " + act + " - " + q.Describe());
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

        /// <summary>Harness: presses the Flight Deck's button as a player would.</summary>
        public static void TestLaunch()
        {
            var q = Current as RrJumbotronRoom;
            if (q != null)
            {
                q.PressLaunch();
                RrLog.Info("Jumbotron", "JumboTest: launch pressed - hazards " + q.QuestInt(RrQuestInts.Hazards));
            }
        }

        /// <summary>Harness: presses the Foyer console's START (or reports that there is none).</summary>
        public static void TestConsoleStart()
        {
            foreach (var c in RrGameConsole.All)
            {
                if (c != null && c.Spec != null && c.Spec.Title == "THE RISE OF JUMBOTRON")
                {
                    bool ok = c.Press("test");
                    RrLog.Info("Jumbotron", "JumboTest: console START pressed (" + ok + ", label " + c.StartText + ", rows " + c.ShownPlayers + ")");
                    return;
                }
            }

            RrLog.Warn("Jumbotron", "JumboTest: no Jumbotron console");
        }
    }
}
