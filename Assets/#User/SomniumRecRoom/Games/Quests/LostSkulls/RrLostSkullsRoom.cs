using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls
{
    /// <summary>
    /// Isle of Lost Skulls (Pirate1; rr_quests.md 4.3, rr_latest_rgames_story.md 8): the team wakes up locked in the brig
    /// of a pirate ship and fights its way through eight acts to Ghost Beard. Built entirely from Rec Room's own Android
    /// levels 62-70 (Quest_Pirate1_*, re-converted with gtscene jt=1 + DevNotes/iter13/isle_tools, carried as
    /// Data/Isle_&lt;Room&gt;.bytes and read with <see cref="RrSceneBank"/>), each built by the Rise of Jumbotron's level
    /// builder (<see cref="RrJumbotronArea"/>: install meshes, hull plates, colliders, lights, markers), in play order:
    ///
    /// | act | level | final version (8.1) |
    /// | --- | --- | --- |
    /// | lobby | Foyer (62) | the pier: hologram console, practice skeleton standees, the Rec Center door |
    /// | 1 | Hallway1 (63) | Galley: locked in a jail cell (break the lock), swashbuckler waves, a gate |
    /// | 2 | Hallway2 (64) | Pirate Banquet: jug and bottle throwers, the Bar Fight |
    /// | 3 | ShipDeck (65) | High Seas: cannoneers, captains on the other ship; a cannon shot at the target drops the bridge |
    /// | 4 | Beach1 (66) | Port Plunder: flintlocks, captains in the towers, the gate's 5 + 4 captains |
    /// | 5 | Beach2 (67) | Gibbet Jungle: gibbeted skeletons, throwers on the platforms |
    /// | 6 | Caves1 (68) | Caves: the bridge throwers, a gibbet, blunderbuss |
    /// | 7 | SunkenShip (69) | Sunken Ship |
    /// | 8 | BossArena (70) | Ghost Beard's Gauntlet: only cannons hurt him, 3 hits (<see cref="RrGhostBeard"/>) |
    ///
    /// Final rules (<see cref="RrQuestFinalRules"/>, 8.1): 1-3 players, gold, 500 per skeleton / 1,000 per gibbet and
    /// cannoneer / 10,000 for Ghost Beard, 2,000 lost per down, stingers PirateQuestHeroesAreVictorious / PirateQuest_Fail,
    /// game-over screens 10 s; music PirateQuestShanty2 (explore), Quest_Pirate1_PirateBrawl01 (combat, the Bar Fight),
    /// PirateShoppingChannel (the lobby's shop). Weapons are found in the acts where the levels lay them (no racks):
    /// cutlasses, stools, pans, brooms, oars, fish, bottles, mugs, plates, rum jugs, flintlocks, the caves' blunderbuss.
    /// Basic form: the mid-level gates stay open, the caves are lit, no mines / puzzle pieces / torch. Checkpoints: a team
    /// wipe restarts the current act at its arrival (RrQuestRoom.RestartActOnWipe; gold back to the act's start).
    /// </summary>
    public sealed class RrLostSkullsRoom : RrQuestRoom
    {
        public const int Swash = 0, Jug = 1, Bottle = 2, Captain = 3, Gibbet = 4, Cannoneer = 5, Boss = 6;

        public const string ShopMusic = "PirateShoppingChannel";
        public const string ExploreMusic = "PirateQuestShanty2";
        public const string CombatMusic = "Quest_Pirate1_PirateBrawl01";

        public const int GalleyAct = 0, BanquetAct = 1, HighSeasAct = 2, PortAct = 3, JungleAct = 4, CavesAct = 5, SunkenAct = 6, BossAct = 7;

        /// <summary>Hazard codes (members' requests): the brig's lock, a cannon's fuse (argument: cannon index in the act).</summary>
        public const int HazardLock = 1, HazardCannon = 2;

        /// <summary>Bits of the act's hazard int (cleared on every act entry): the brig is open, the High Seas bridge is down.</summary>
        public const int BitCell = 1, BitBridge = 2;

        /// <summary>Quest int: the act's cannon shots, (count &lt;&lt; 3) | cannon index, so every client plays each shot once.</summary>
        public const int IntCannonShot = RrQuestInts.FirstQuestSpecific;

        /// <summary>The lobby's Rec Room scene (its lighting row in RrEnvironmentTable and the kit scene's name).</summary>
        public const string FoyerScene = "Isle_Foyer";

        private static readonly string[] ActScenes =
        {
            "Isle_Hallway1", "Isle_Hallway2", "Isle_ShipDeck", "Isle_Beach1", "Isle_Beach2", "Isle_Caves1", "Isle_SunkenShip", "Isle_BossArena"
        };

        /// <summary>The Foyer's spawn points' centre (Player1..3SpawnPoint, scene space): the lobby's origin.</summary>
        private static readonly Vector3 FoyerAnchor = new Vector3(-68.01f, -6.47f, 158.94f);

        public static string SceneOf(int act)
        {
            return act >= 0 && act < ActScenes.Length ? ActScenes[act] : FoyerScene;
        }

        internal readonly List<RrJumbotronArea> Areas = new List<RrJumbotronArea>();
        internal readonly List<RrLostSkullsCannon> Cannons = new List<RrLostSkullsCannon>();
        internal RrLostSkullsCell Cell;
        internal RrLostSkullsBridge Bridge;
        internal RrGhostBeard GhostBeard;

        private readonly List<RrJumbotronWeaponSpot> _spots = new List<RrJumbotronWeaponSpot>();
        private readonly List<Pose> _lobbySpawns = new List<Pose>();
        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private readonly Dictionary<int, float> _cannonReady = new Dictionary<int, float>();
        private Transform _lobbyRoot;
        private string _envScene;
        private int _seenShot;
        private int _fallAct = -1;
        private float _fallGuard;
        private float _bridgeWaitSince = -1f;
        private bool _barFightShown;

        protected override RrQuestDef CreateDef()
        {
            var d = new RrQuestDef
            {
                Name = "Isle of Lost Skulls",
                Currency = "Gold",
                DeathPenalty = 2000,
                LobbyMusic = ShopMusic,
                ExploreMusic = ExploreMusic,
                CombatMusic = CombatMusic,
                BossMusic = CombatMusic,
                ClearFanfare = null,
                // rr_quests.md 2.1: the [Quest_Pirate1_Manager]'s stingers, game-over screens 10 s.
                WinStinger = "PirateQuestHeroesAreVictorious",
                LoseStinger = "PirateQuest_Fail",
                FailSeconds = 10f,
                PointsSound = "Quest_Pirate1_Collectable_GoldPickup01",
                MaxAlive = 10
            };
            // rr_quests.md 2.2.1 spawner prefabs; kill gold from the final wiki (8.1): skeletons 500, gibbet and cannoneer 1,000.
            d.AddEnemy("(QuestAIController)PirateGroundMelee2020", 500);
            d.AddEnemy("(QuestAIController)PirateExplosiveThrowing", 500);
            d.AddEnemy("(QuestAIController)PirateBottleThrowing", 500);
            d.AddEnemy("(QuestAIController)PirateFlintock2020", 500);
            d.AddEnemy("(QuestAIController)PirateGibbet", 1000);
            // The cannoneer (half HP, never leaves its cannon): a flintlock skeleton kept at the enemy ship's cannon.
            d.AddEnemy("(QuestAIController)PirateFlintock2020", 1000);
            // Ghost Beard is not in the plain bundles (8.2): a flintlock skeleton body carries his health, kept hidden
            // under our ghost (RrGhostBeard); only cannon shots reach it.
            d.AddEnemy("(QuestAIController)PirateFlintock2020", 0);
            d.BossType = Boss;
            d.BossKillPoints = 10000;
            foreach (var w in new[]
            {
                "(Quest_Pirate1_Cutlass)", "(Quest_Pirate1_BarStool)", "(Quest_Pirate1_FryingPan)", "(Quest_Pirate1_Oar)", "(Quest_Pirate1_PushBroom)",
                "(Quest_Pirate1_BreakableFish)", "(Quest_Pirate1_Flintlock)", "(Quest_Dracula1_BlunderBuss)"
            })
            {
                d.Weapons.Add(w);
            }

            return d;
        }

        protected override string BossName
        {
            get { return "GHOST BEARD"; }
        }

        /// <summary>The final Isle has a checkpoint at every act (8.1): a team wipe restarts the act, it does not end the run.</summary>
        protected override bool RestartActOnWipe
        {
            get { return true; }
        }

        /// <summary>The act starts again: its set pieces as on arrival (the hazard bits - brig, bridge - are cleared by the framework).</summary>
        protected override void OnActRestart(int act)
        {
            _bridgeWaitSince = -1f;
            _cannonReady.Clear();
            if (act == BanquetAct)
            {
                _barFightShown = false;
            }

            if (act == BossAct && GhostBeard != null)
            {
                // A whole Ghost Beard again (his old body went with the act's enemies; the director spawns a new one).
                GhostBeard.ResetBoss();
            }
        }

        protected override Pose LobbySpawn(int index)
        {
            if (_lobbySpawns.Count > 0)
            {
                var p = _lobbySpawns[index % _lobbySpawns.Count];
                return new Pose(p.position + p.rotation * new Vector3(0f, 0f, -1.2f * (index / _lobbySpawns.Count)), p.rotation);
            }

            return new Pose(new Vector3((index % 3 - 1f) * 1.5f, 0.05f, 0f), Quaternion.identity);
        }

        /// <summary>
        /// Room-local root of act i: a 400 m grid north of the Foyer (the biggest level, Caves1, spans 313 x 132 m;
        /// only the current act is ever active), 4 columns.
        /// </summary>
        public static Vector3 ActSlot(int i)
        {
            return new Vector3((i % 4 - 1.5f) * 400f, 0f, 400f * (1 + i / 4));
        }

        // ------------------------------------------------------------------ build

        /// <summary>Every room is built behind the loading card; only the lobby or the current act stays active.</summary>
        protected override IEnumerator BuildLevel()
        {
            if (GetComponent<RrJumbotronHull.Owned>() == null)
            {
                gameObject.AddComponent<RrJumbotronHull.Owned>();
            }

            _lobbyRoot = RrBuild.Group(transform, "Foyer").transform;
            yield return RrLostSkullsActs.Foyer(this, _lobbyRoot, FoyerAnchor, _lobbySpawns, _surfaces);
            BuildProgress = 0.15f;
            var builders = RrLostSkullsActs.All();
            var acts = new List<RrQuestAct>();
            for (int i = 0; i < builders.Length; i++)
            {
                var root = RrBuild.Group(transform, "Act " + (i + 1), ActSlot(i)).transform;
                RrQuestAct act = null;
                yield return builders[i](this, root, _surfaces, x => act = x);
                if (act == null)
                {
                    RrLog.Warn("LostSkulls", "act " + (i + 1) + " did not build");
                    act = new RrQuestAct { Title = "ACT " + (i + 1), Subtitle = "(missing)", Root = root, Origin = root.localPosition, Area = new Bounds(Vector3.zero, new Vector3(40f, 10f, 40f)) };
                }

                acts.Add(act);
                BuildProgress = 0.15f + 0.75f * (i + 1) / builders.Length;
                yield return null;
            }

            foreach (var act in acts)
            {
                Def.AddAct(act);
            }

            for (int i = 0; i < acts.Count; i++)
            {
                var act = acts[i];
                RrLog.Info("LostSkulls", "act " + (i + 1) + " \"" + act.Title + " / " + act.Subtitle + "\" root " + act.Root.localPosition.ToString("F0") + ", entry " + act.Entry.ToString("F1")
                    + " yaw " + act.EntryYaw.ToString("0") + ", " + act.Waves.Count + " waves, " + act.Groups.Count + " groups, exit " + (act.ExitDoor != null ? "door" : "none"));
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
                Title = "ISLE OF LOST SKULLS",
                TeamCount = 0,
                MinPlayers = 1,
                MaxPlayers = 3,
                StartLabel = () => Phase == RrPhase.Lobby ? "START QUEST" : Phase == RrPhase.Countdown ? "STARTING" : "IN PROGRESS"
            });
        }

        internal void LobbyStartButton(Transform parent, Vector3 local, float yaw)
        {
            StartButton(parent, local, yaw);
        }

        /// <summary>Only the lobby (outside a run) or the current act is active (the Jumbotron's iteration-11 rule).</summary>
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
            if (type == Boss && GhostBeard != null)
            {
                GhostBeard.Setup(e);
            }
            else if (type == Cannoneer)
            {
                // "Never leaves its cannon", half the health (8.1).
                e.MoveSpeed = 0f;
                e.MaxHealth = Mathf.Max(1, e.MaxHealth / 2);
                e.Health = e.MaxHealth;
            }
        }

        protected override byte PointsKind(RrEnemy e, int type)
        {
            return type == Boss ? RrQuestEvents.PointsBoss : RrQuestEvents.PointsKill;
        }

        protected override bool AuthorityActDone(int act)
        {
            // High Seas: the way on is over the bridge the cannon drops.
            return act != HighSeasAct || (QuestInt(RrQuestInts.Hazards) & BitBridge) != 0;
        }

        protected override void AuthorityActStep(int act)
        {
            if (act == BossAct && GhostBeard != null)
            {
                GhostBeard.AuthorityStep(this);
            }
            else if (act == HighSeasAct)
            {
                BridgeFallback();
            }
        }

        /// <summary>
        /// High Seas without a cannon hit: once the waves before the boarding wave are spawned and dead for 20 s (before
        /// the boarding wave's own 25 s), the bridge drops by itself (the basic form must never strand a team that cannot
        /// find the target).
        /// </summary>
        private void BridgeFallback()
        {
            var act = CurrentAct;
            if (act == null || (QuestInt(RrQuestInts.Hazards) & BitBridge) != 0)
            {
                _bridgeWaitSince = -1f;
                return;
            }

            bool waiting = Enemies.Alive == 0 && QuestInt(RrQuestInts.Wave) >= Mathf.Max(1, act.Waves.Count - 2);
            if (!waiting)
            {
                _bridgeWaitSince = -1f;
                return;
            }

            if (_bridgeWaitSince < 0f)
            {
                _bridgeWaitSince = Time.time;
                Broadcast("Fire a cannon at the target to drop the bridge!");
            }
            else if (Time.time - _bridgeWaitSince > 20f)
            {
                SetQuestInt(RrQuestInts.Hazards, QuestInt(RrQuestInts.Hazards) | BitBridge);
                Broadcast("The bridge comes crashing down!");
                RrLog.Info("LostSkulls", "High Seas: the bridge dropped by itself (no cannon hit in 20 s)");
            }
        }

        protected override void AuthorityHazard(int playerId, int code, int arg)
        {
            int act = ActIndex;
            if (code == HazardLock && act == GalleyAct && (QuestInt(RrQuestInts.Hazards) & BitCell) == 0)
            {
                SetQuestInt(RrQuestInts.Hazards, QuestInt(RrQuestInts.Hazards) | BitCell);
                RrLog.Info("LostSkulls", "the brig's lock was broken by player " + playerId);
            }
            else if (code == HazardCannon)
            {
                AuthorityCannon(playerId, act, arg);
            }
        }

        /// <summary>
        /// A cannon's fuse (authority): one shot per cannon every 2.5 s. The ball flies to the cannon's aim point
        /// (<see cref="RrLostSkullsCannon.Aim"/>, the same on every client); it drops the High Seas bridge when it
        /// reaches the target, hits Ghost Beard when he is in the cannon's arc, and blasts skeletons within 3 m.
        /// </summary>
        private void AuthorityCannon(int playerId, int act, int index)
        {
            var c = CannonAt(act, index);
            float ready;
            if (c == null || Stage == RrQuestStage.Intro || (_cannonReady.TryGetValue(act * 16 + index, out ready) && Time.time < ready))
            {
                return;
            }

            _cannonReady[act * 16 + index] = Time.time + 2.5f;
            int shots = (QuestInt(IntCannonShot) >> 3) + 1;
            SetQuestInt(IntCannonShot, (shots << 3) | (index & 7));
            RrLostSkullsCannon.Target kind;
            Vector3 hit = c.Aim(this, out kind);
            if (kind == RrLostSkullsCannon.Target.Bridge && (QuestInt(RrQuestInts.Hazards) & BitBridge) == 0)
            {
                SetQuestInt(RrQuestInts.Hazards, QuestInt(RrQuestInts.Hazards) | BitBridge);
                Broadcast("Bullseye! The bridge is down!");
                RrLog.Info("LostSkulls", "High Seas: player " + playerId + "'s cannon shot hit the target, the bridge drops");
            }
            else if (kind == RrLostSkullsCannon.Target.Boss && GhostBeard != null)
            {
                GhostBeard.AuthorityCannonHit(this, index);
            }

            // The blast: skeletons within 3 m of the landing point (the cannoneers on the other ship).
            for (int s = 0; s < RrQuestEnemies.Slots; s++)
            {
                var e = Enemies.At(s);
                if (e != null && e.Alive && (GhostBeard == null || e != GhostBeard.Body) && (e.Center - hit).sqrMagnitude < 9f)
                {
                    e.TakeLocalHit(400, RrBodyPart.Body, e.Center, RrCombat.UnknownTool);
                }
            }
        }

        internal RrLostSkullsCannon CannonAt(int act, int index)
        {
            foreach (var c in Cannons)
            {
                if (c != null && c.Act == act && c.Index == index)
                {
                    return c;
                }
            }

            return null;
        }

        /// <summary>A member pressed a cannon's fuse (or the brig's lock): the request goes to the authority.</summary>
        internal void PressHazard(int code, int arg)
        {
            if (Phase == RrPhase.Playing)
            {
                Ask(RrQuestEvents.ReqHazard, (byte)code, (byte)arg);
            }
        }

        protected override void QuestStep(int act)
        {
            ShowAreas(act);
            ShowLighting(act);
            bool playing = Phase == RrPhase.Playing;
            int hazards = playing ? QuestInt(RrQuestInts.Hazards) : 0;
            if (Cell != null)
            {
                Cell.Show(act == GalleyAct && (hazards & BitCell) != 0);
            }

            if (Bridge != null)
            {
                Bridge.Show(act == HighSeasAct && (hazards & BitBridge) != 0);
            }

            if (GhostBeard != null)
            {
                bool inBoss = act == BossAct && (playing || Phase == RrPhase.MatchOver);
                GhostBeard.ClientStep(this, inBoss && Stage != RrQuestStage.Intro, inBoss && (Stage == RrQuestStage.Clear || QuestInt(RrQuestInts.Outcome) == 1));
            }

            CannonShots(act);
            if (playing && act == BanquetAct && !_barFightShown && CurrentAct != null && QuestInt(RrQuestInts.Wave) >= CurrentAct.Waves.Count - 1 && Stage == RrQuestStage.Combat)
            {
                _barFightShown = true;
                Hud.Toast("BAR FIGHT!", 3f);
            }

            if (playing)
            {
                FallCheck(act);
            }
        }

        /// <summary>Every client: a new cannon shot in the ints plays its flash, ball and blast here.</summary>
        private void CannonShots(int act)
        {
            int v = Phase == RrPhase.Playing ? QuestInt(IntCannonShot) : 0;
            if (v == _seenShot)
            {
                return;
            }

            bool fresh = (v >> 3) > (_seenShot >> 3);
            _seenShot = v;
            if (!fresh)
            {
                return;
            }

            var c = CannonAt(act, v & 7);
            if (c != null)
            {
                RrLostSkullsCannon.Target kind;
                c.PlayShot(c.Aim(this, out kind));
            }
        }

        private void ShowLighting(int act)
        {
            string scene = SceneOf(act);
            if (scene != _envScene)
            {
                _envScene = scene;
                RrLog.Info("LostSkulls", "lighting: " + scene + (act >= 0 ? " (act " + (act + 1) + ")" : " (lobby)"));
            }

            RrEnvironment.SetArea(scene);
            // RrPostFxProfiles maps LostSkulls area 0 (lobby), 2 (Pirate Banquet), 4 (Port Plunder), 5 (Gibbet Jungle).
            RrPostFx.SetArea(act + 1);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_envScene != null && RrEnvironment.Activity == RrActivity.LostSkulls)
            {
                RrEnvironment.SetArea(null);
            }

            _envScene = null;
        }

        /// <summary>
        /// Overboard: a local player more than 25 m under the act's arrival (the sea, the caves' holed bridge) goes back
        /// to the act's entry (Rec Room's out-of-bounds respawn).
        /// </summary>
        private void FallCheck(int act)
        {
            var a = CurrentAct;
            if (a == null || a.Root == null || act < 0 || act >= Areas.Count)
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
            if (local.y > a.Entry.y - 25f || Mathf.Abs(local.x - a.Area.center.x) > a.Area.extents.x + 40f || Mathf.Abs(local.z - a.Area.center.z) > a.Area.extents.z + 40f)
            {
                return;
            }

            _fallGuard = Time.time + 2f;
            var p = ActEntryWorld(act, Rank());
            RrPlayer.Teleport(p.position, p.rotation.eulerAngles.y);
            Hud.Toast("Man overboard!", 2f);
            RrLog.Info("LostSkulls", "local player fell out of act " + (act + 1) + ", back at the entry");
        }

        protected override void OnActStage(int act, RrQuestStage stage)
        {
            if (act == GalleyAct && stage == RrQuestStage.Intro)
            {
                Hud.Toast("You're locked in the brig! Break the lock to escape", 5f);
            }
            else if (act == HighSeasAct && stage == RrQuestStage.Combat)
            {
                Hud.Toast("Man the cannons: press the fuse!", 4f);
            }
            else if (act == BossAct && stage == RrQuestStage.Combat)
            {
                Hud.Toast("Only the ship's cannons can hurt Ghost Beard!", 4f);
            }
            else if (act == BossAct && stage == RrQuestStage.Clear)
            {
                Hud.Toast("Ghost Beard is sunk!", 4f);
                RrLog.Info("LostSkulls", "Ghost Beard is defeated");
            }
        }

        protected override void OnRunReset()
        {
            _barFightShown = false;
            _bridgeWaitSince = -1f;
            _seenShot = QuestInt(IntCannonShot);
            _cannonReady.Clear();
            if (GhostBeard != null)
            {
                GhostBeard.ResetBoss();
            }

            if (Cell != null)
            {
                Cell.Show(false);
            }

            if (Bridge != null)
            {
                Bridge.Show(false);
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

        /// <summary>Harness: jump to act n (1 Galley .. 8 Ghost Beard) during a run and wait for its combat.</summary>
        public static IEnumerator TestAct2() { return TestAct(1); }

        public static IEnumerator TestAct3() { return TestAct(2); }

        public static IEnumerator TestAct4() { return TestAct(3); }

        public static IEnumerator TestAct5() { return TestAct(4); }

        public static IEnumerator TestAct6() { return TestAct(5); }

        public static IEnumerator TestAct7() { return TestAct(6); }

        public static IEnumerator TestBoss() { return TestAct(BossAct); }

        private static IEnumerator TestAct(int a)
        {
            var q = Current as RrLostSkullsRoom;
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
            RrLog.Info("LostSkulls", "IsleTest: act " + (a + 1) + " - " + q.Describe() + ", player at " + RrPlayer.FeetPosition.ToString("F1"));
        }

        /// <summary>Harness: breaks the brig's lock as a player would.</summary>
        public static void TestBreakLock()
        {
            var q = Current as RrLostSkullsRoom;
            if (q != null)
            {
                q.PressHazard(HazardLock, 0);
                RrLog.Info("LostSkulls", "IsleTest: lock pressed - hazards " + q.QuestInt(RrQuestInts.Hazards));
            }
        }

        /// <summary>Harness: fires every cannon of the current act once (the High Seas target, Ghost Beard).</summary>
        public static void TestFireCannons()
        {
            var q = Current as RrLostSkullsRoom;
            if (q == null)
            {
                return;
            }

            int n = 0;
            foreach (var c in q.Cannons)
            {
                if (c != null && c.Act == q.ActIndex)
                {
                    q.PressHazard(HazardCannon, c.Index);
                    n++;
                }
            }

            RrLog.Info("LostSkulls", "IsleTest: fired " + n + " cannon(s) in act " + (q.ActIndex + 1) + " - hazards " + q.QuestInt(RrQuestInts.Hazards) + ", boss " + q.QuestInt(RrQuestInts.BossHealth));
        }

        /// <summary>
        /// Harness: fights the current act to its clear as a player would (max 60 s): every 0.4 s each living skeleton
        /// takes a 400-damage hit; with nothing alive the player walks into the next Enter wave's zone; the lock and the
        /// cannons are pressed on the way.
        /// </summary>
        public static IEnumerator TestFightClear()
        {
            var q = Current as RrLostSkullsRoom;
            if (q == null || q.CurrentAct == null)
            {
                yield break;
            }

            int act = q.ActIndex;
            float end = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < end && q.ActIndex == act && q.Stage != RrQuestStage.Clear && q.Phase == RrPhase.Playing)
            {
                if (act == GalleyAct)
                {
                    q.PressHazard(HazardLock, 0);
                }

                int hit = 0;
                for (int s = 0; s < RrQuestEnemies.Slots; s++)
                {
                    var e = q.Enemies.At(s);
                    if (e != null && e.Alive && e.State != RrEnemyState.Spawning && (q.GhostBeard == null || e != q.GhostBeard.Body))
                    {
                        e.TakeLocalHit(400, RrBodyPart.WeakSpot, e.Center, RrCombat.UnknownTool);
                        hit++;
                    }
                }

                if (act == HighSeasAct || act == BossAct)
                {
                    TestFireCannons();
                }

                if (hit == 0 && q.Enemies.Alive == 0)
                {
                    q.TestIntoNextZone();
                }

                yield return new WaitForSeconds(0.4f);
            }

            RrLog.Info("LostSkulls", "IsleTest: fight " + (q.Stage == RrQuestStage.Clear ? "CLEAR" : "NOT CLEAR") + " in act " + (act + 1) + " - " + q.Describe());
        }

        private void TestIntoNextZone()
        {
            var act = CurrentAct;
            if (act == null || act.Root == null)
            {
                return;
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
                return;
            }
        }

        /// <summary>Harness: presses the Foyer console's START (or reports that there is none).</summary>
        public static void TestConsoleStart()
        {
            foreach (var c in RrGameConsole.All)
            {
                if (c != null && c.Spec != null && c.Spec.Title == "ISLE OF LOST SKULLS")
                {
                    bool ok = c.Press("test");
                    RrLog.Info("LostSkulls", "IsleTest: console START pressed (" + ok + ", label " + c.StartText + ", rows " + c.ShownPlayers + ")");
                    return;
                }
            }

            RrLog.Warn("LostSkulls", "IsleTest: no Isle of Lost Skulls console");
        }
    }
}
