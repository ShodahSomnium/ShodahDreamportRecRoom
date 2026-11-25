using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Crescendo
{
    /// <summary>
    /// Crescendo of the Blood Moon (Dracula1, "CBM"; rr_latest_rgames_story.md 7): the 2018 quest in its final (2024 Quest
    /// and Mobile port) form, built from Rec Room's own Android levels 79-89 (Quest_Dracula1_*, DN/refdata/apk/
    /// Crescendo_*.rrsk, shipped as Data/Crescendo_*.bytes and built by <see cref="RrJumbotronArea"/>, the same kit, hull,
    /// look and haze as The Rise of Jumbotron). The 13 acts of the wiki's act order, with the Grand Hall as the hub the
    /// team returns to four times (the checkpoint, where silver is banked):
    ///
    /// | act | level | what |
    /// | --- | --- | --- |
    /// | lobby | Foyer (79) | the theatre foyer: start hologram, practice weapons, the Rec Center door |
    /// | 1 | Township (80) | Burning Village: 3 waves of Pumpkin Spicers, a gate, fire bats and maids, a gate, the town square, the carriage |
    /// | 2 | CarriageRide (81) | Forest Road: waves from both sides of the carriage, Barrel Dogs |
    /// | 3 | CastleEntry (82) | Castle Grounds: Marksmen, Barrel Dogs, Maids up to the drawbridge |
    /// | 4, 7, 10, 12 | CastleMainFoyer (83) | the Grand Hall: bank, checkpoint, the next door (2nd floor right, 1st floor right, rear, 2nd floor back) |
    /// | 5 | Gallery (84) | the Maid Warlord (100) |
    /// | 6 | Library (85) | Chamber of Knowledge: the Mayor Marksman (400) |
    /// | 8 | Park (86) | Garden of Sorrows: President Pumpkin Head (200) |
    /// | 9 | Ballroom (87) | Inverted Ballroom: Baron Fire Bat (800) |
    /// | 11 | Dungeon (88) | Lower Sanctum: arrow traps, King Barrel Dog (1600) |
    /// | 13 | BossRoom (89) | Concert Hall: Dracula and his three light mirrors (<see cref="RrCrescendoDracula"/>) |
    ///
    /// Final rules (7.1, measured values in 7.2): 1-3 players, currency Silver, silver per kill Pumpkin Spicer 10, Fire
    /// Bat 5, Dive Bat 15, Bone Maid 10, Barrel Dog 25, Marksman 20, the five mini-bosses 100 / 400 / 200 / 800 / 1600
    /// (bundle customValue), candles 2 / 3 and pumpkins 3 (rr_quests.md 2.4), decoy bats nothing; the rank is the hidden
    /// combat score (4,400 for a deathless run, -200 per down; S+ 4,160, S 3,160, A about 6-8 downs, B 9-12) with Rec
    /// Room's rank bonuses S+ 15,000 / S 10,000 / A 6,000 / B 3,000 / C 2,000 silver on a victory. A whole-team down after
    /// the first Grand Hall visit goes back to the last visit with the silver banked there (the checkpoint). Music: the
    /// install's CBM_Gavotte (lobby and exploring), CBM_Ballroom (the Inverted Ballroom), CBM_CombatMiniboss (fights,
    /// GUESS), CBM_CombatBoss (Dracula), CBM_Victory / CBM_Failure.
    /// </summary>
    public sealed class RrCrescendoRoom : RrQuestRoom
    {
        // roster (index = actor type on the wire)
        public const int Spicer = 0, FireBat = 1, DiveBat = 2, Maid = 3, Dog = 4, Marksman = 5;
        public const int MaidWarlord = 6, MayorMarksman = 7, PumpkinHead = 8, BaronBat = 9, KingDog = 10;
        public const int Dracula = 11, DecoyBat = 12, RealBat = 13;

        public const string Shovel = "(Quest_Dracula1_Shovel)";
        public const string PitchFork = "(Quest_Dracula1_PitchFork)";
        public const string BlunderBuss = "(Quest_Dracula1_BlunderBuss)";
        public const string Crossbow = "(Quest_Dracula1_1H-Crossbow)";
        public const string HunterCrossbow = "(Quest_Dracula1_Crossbow_Hunter)";
        public const string Flintlock = "(Quest_Dracula1_Flintlock)";
        public const string Whip = "(Whip)";

        public const string ExploreMusic = "CBM_Gavotte";
        public const string BallroomMusic = "CBM_Ballroom";
        public const string CombatMusic = "CBM_CombatMiniboss";
        public const string BossMusic = "CBM_CombatBoss";

        // acts (0-based)
        public const int Township = 0, Carriage = 1, CastleGrounds = 2, Hall1 = 3, Gallery = 4, Library = 5, Hall2 = 6, Park = 7, Ballroom = 8, Hall3 = 9,
            Dungeon = 10, Hall4 = 11, BossAct = 12;

        /// <summary>Hazard codes (<see cref="RrQuestRoom.Ask"/> ReqHazard): a light mirror fired (arg = mirror index).</summary>
        public const int HazardMirror = 1;

        /// <summary>Quest-specific ints (16..30): the banked silver, the checkpoint act + 1, Dracula's mirror hits, the beam counter.</summary>
        public const int QBanked = RrQuestInts.FirstQuestSpecific, QCheckpoint = RrQuestInts.FirstQuestSpecific + 1, QHits = RrQuestInts.FirstQuestSpecific + 2,
            QBeam = RrQuestInts.FirstQuestSpecific + 3, QBonus = RrQuestInts.FirstQuestSpecific + 4;

        /// <summary>Hazard bits from this one on open the current act's gates (bit GateBit + k).</summary>
        public const int GateBit = 4;

        /// <summary>The combat score (7.1): 4,400 for a deathless run, 200 off per down.</summary>
        public const int CombatBase = 4400, CombatPerDown = 200;

        /// <summary>The lobby's Rec Room scene (its lighting row, the kit scene's name).</summary>
        public const string FoyerScene = "Crescendo_Foyer";

        /// <summary>One built level (the Grand Hall is one level used by four acts).</summary>
        public sealed class Level
        {
            public RrJumbotronArea Area;
            public Transform Root;
            public readonly List<RrCrescendoGate> Gates = new List<RrCrescendoGate>();
            /// <summary>Gate k opens once this many waves of the act have started and every enemy is beaten.</summary>
            public readonly List<int> GateAfter = new List<int>();
        }

        internal RrCrescendoDracula DraculaFight;
        internal RrCrescendoTraps Traps;
        internal readonly List<Level> Levels = new List<Level>();
        private readonly List<Level> _levelOfAct = new List<Level>();
        private readonly List<Pose> _lobbySpawns = new List<Pose>();
        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private Transform _lobbyRoot;
        private string _envScene;
        private float _wipeSince = -1f;
        private bool _rankShown;
        private int _bankedShownAct = -1;

        protected override RrQuestDef CreateDef()
        {
            var d = new RrQuestDef
            {
                Name = "Crescendo of the Blood Moon",
                Currency = "Silver",
                DeathPenalty = 0,
                LobbyMusic = ExploreMusic,
                ExploreMusic = ExploreMusic,
                CombatMusic = CombatMusic,
                BossMusic = BossMusic,
                ClearFanfare = null,
                WinStinger = "CBM_Victory",
                LoseStinger = "CBM_Failure",
                PointsFx = "[Quest_Dracula1_Silver_VFX]",
                BossPointsFx = "[Quest_Dracula1_Silver_VFX]",
                PointsSound = "Quest_Coins_Spawn01",
                BossPointsSound = "Quest_Dracula1_Silver_MiniBoss_Pickup01",
                MaxAlive = 10,
                MaxPlayers = 3,
                FailSeconds = 9.5f
            };
            d.AddEnemy("(QuestAIController)DraculaMelee", 10);
            d.AddEnemy("(QuestAIController)DraculaBatRanged", 5);
            d.AddEnemy("(QuestAIController)DraculaBatLunging", 15);
            d.AddEnemy("(QuestAIController)DraculaBoneThrower2020", 10);
            d.AddEnemy("(QuestAIController)DraculaWolfLunging2020", 25);
            d.AddEnemy("(QuestAIController)DraculaBellhop", 20);
            // the five mini-bosses on their kinds' bodies, bigger and tougher (their coin piles' customValue)
            d.AddEnemy("(QuestAIController)DraculaBoneThrower2020", 100);
            d.AddEnemy("(QuestAIController)DraculaBellhop", 400);
            d.AddEnemy("(QuestAIController)DraculaMelee", 200);
            d.AddEnemy("(QuestAIController)DraculaBatRanged", 800);
            d.AddEnemy("(QuestAIController)DraculaWolfLunging2020", 1600);
            // Dracula (not in the install: the musket skeleton's body stands in), his decoy bats and the real one
            d.AddEnemy("(QuestAIController)DraculaBellhop", 0);
            d.AddEnemy("(QuestAIController)DraculaBatLunging", 0);
            d.AddEnemy("(QuestAIController)DraculaBatLunging", 0);
            d.BossType = Dracula;
            // unpublished (7.3): GUESS, twice the King Barrel Dog
            d.BossKillPoints = 3200;
            d.Weapons.Add(Shovel);
            d.Weapons.Add(PitchFork);
            d.Weapons.Add(BlunderBuss);
            d.Weapons.Add(Crossbow);
            d.Weapons.Add(HunterCrossbow);
            d.Weapons.Add(Flintlock);
            d.Weapons.Add(Whip);
            return d;
        }

        protected override string BossName
        {
            get { return "DRACULA"; }
        }

        protected override int PointsFor(RrEnemy e, int type)
        {
            // "killing decoy bats and their waves gives no silver" (7.1, 2018-11 patch)
            return type == DecoyBat || type == RealBat ? 0 : base.PointsFor(e, type);
        }

        protected override byte PointsKind(RrEnemy e, int type)
        {
            return type == Dracula || (type >= MaidWarlord && type <= KingDog) ? RrQuestEvents.PointsBoss : RrQuestEvents.PointsKill;
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
        /// Room-local root of level i: a 300 m grid north of the Foyer (the play areas are at most 150 m across; only the
        /// lobby or the current act's level is active, so the far backdrops of Castle Grounds and the Garden may overlap).
        /// </summary>
        public static Vector3 LevelSlot(int i)
        {
            return new Vector3((i % 4 - 1.5f) * 300f, 0f, 300f * (1 + i / 4));
        }

        /// <summary>The level (scene) an act is played in.</summary>
        internal Level LevelOf(int act)
        {
            return act >= 0 && act < _levelOfAct.Count ? _levelOfAct[act] : null;
        }

        /// <summary>Whether an act is a Grand Hall visit (the hub: bank and checkpoint).</summary>
        public static bool IsHall(int act)
        {
            return act == Hall1 || act == Hall2 || act == Hall3 || act == Hall4;
        }

        // ------------------------------------------------------------------ build

        /// <summary>
        /// Every level is built here, behind the loading card (the framework numbers pots in build order), the Grand
        /// Hall once for its four visits; only the lobby or the current act's level stays active (<see cref="ShowAreas"/>).
        /// </summary>
        protected override IEnumerator BuildLevel()
        {
            if (GetComponent<RrJumbotronHull.Owned>() == null)
            {
                gameObject.AddComponent<RrJumbotronHull.Owned>();
            }

            _lobbyRoot = RrBuild.Group(transform, "Foyer").transform;
            yield return RrCrescendoActs.Foyer(this, _lobbyRoot, _lobbySpawns, _surfaces);
            BuildProgress = 0.15f;
            var acts = new List<RrQuestAct>();
            var builders = RrCrescendoActs.All();
            Level hall = null;
            for (int i = 0; i < builders.Length; i++)
            {
                Level level = IsHall(i) ? hall : null;
                if (level == null)
                {
                    level = new Level { Root = RrBuild.Group(transform, "Level " + Levels.Count, LevelSlot(Levels.Count)).transform };
                    Levels.Add(level);
                }

                RrQuestAct act = null;
                yield return builders[i](this, level, _surfaces, x => act = x);
                if (IsHall(i))
                {
                    hall = level;
                }

                if (act == null)
                {
                    RrLog.Warn("Crescendo", "act " + i + " did not build");
                    act = new RrQuestAct { Title = "ACT " + (i + 1), Subtitle = "(missing)", Root = level.Root, Origin = level.Root.localPosition, Area = new Bounds(Vector3.zero, new Vector3(40f, 10f, 40f)) };
                }

                acts.Add(act);
                _levelOfAct.Add(level);
                BuildProgress = 0.15f + 0.73f * (i + 1) / builders.Length;
                yield return null;
            }

            foreach (var act in acts)
            {
                Def.AddAct(act);
            }

            for (int i = 0; i < acts.Count; i++)
            {
                var act = acts[i];
                RrLog.Info("Crescendo", "act " + (i + 1) + " \"" + act.Title + " / " + act.Subtitle + "\" entry " + act.Entry.ToString("F1") + " yaw " + act.EntryYaw.ToString("0") + ", "
                    + act.Waves.Count + " waves + " + act.SideWaves.Count + " side, " + act.Groups.Count + " groups, gates " + _levelOfAct[i].Gates.Count + ", exit " + (act.ExitDoor != null ? "yes" : "none"));
            }

            ShowAreas(-1);
        }

        internal void LobbyConsole(Transform parent, Pose pose)
        {
            RrGameConsole.ForGame(this, parent, pose, new RrGameConsoleSpec
            {
                Title = "CRESCENDO OF THE BLOOD MOON",
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

        internal void AddRack(RrQuestRack rack)
        {
            if (rack != null)
            {
                Racks.Add(rack);
            }
        }

        /// <summary>Only the lobby (outside a run) or the current act's level is active.</summary>
        private void ShowAreas(int act)
        {
            bool inRun = act >= 0;
            if (_lobbyRoot != null && _lobbyRoot.gameObject.activeSelf == inRun)
            {
                _lobbyRoot.gameObject.SetActive(!inRun);
            }

            var cur = inRun ? LevelOf(Mathf.Max(0, act)) : null;
            foreach (var l in Levels)
            {
                bool on = l == cur;
                if (l.Root != null && l.Root.gameObject.activeSelf != on)
                {
                    l.Root.gameObject.SetActive(on);
                }
            }
        }

        /// <summary>The Rec Room scene whose lighting applies in an act (-1 or unknown: the Foyer).</summary>
        internal string SceneOf(int act)
        {
            var l = LevelOf(act);
            return l != null && l.Area != null ? l.Area.Scene : FoyerScene;
        }

        // ------------------------------------------------------------------ hooks

        protected override void OnEnemyReady(RrEnemy e, int type)
        {
            if (e == null)
            {
                return;
            }

            switch (type)
            {
                case Dracula:
                    if (DraculaFight != null)
                    {
                        DraculaFight.Setup(e);
                    }

                    break;
                case DecoyBat:
                case RealBat:
                    if (DraculaFight != null)
                    {
                        DraculaFight.SetupBat(e, type == RealBat);
                    }

                    break;
                case MaidWarlord:
                case MayorMarksman:
                case PumpkinHead:
                case BaronBat:
                case KingDog:
                    MiniBoss(e, type);
                    break;
            }
        }

        /// <summary>
        /// A mini-boss is its kind's body at 1.6 x with 8 x the health (GUESS; Rec Room's are bigger, tougher versions),
        /// and the wiki's signature attack: the Maid Warlord's bones burst in 3s, the Mayor Marksman's 5 quick shots, the
        /// Baron Fire Bat's horizontal line of 5 fireballs, the King Barrel Dog's harder charge.
        /// </summary>
        private void MiniBoss(RrEnemy e, int type)
        {
            if (e.transform.localScale.x > 1.3f)
            {
                // already made a mini-boss (a second ready call must not multiply the health again)
                return;
            }

            e.transform.localScale = Vector3.one * (type == KingDog ? 1.8f : 1.6f);
            e.MaxHealth *= 8;
            e.Health = e.MaxHealth;
            e.AggroRange = Mathf.Max(e.AggroRange, 40f);
            switch (type)
            {
                case MaidWarlord:
                    e.Burst = 3;
                    e.Splash = 1.5f;
                    break;
                case MayorMarksman:
                    e.Burst = 5;
                    e.Cooldown = 3.5f;
                    break;
                case BaronBat:
                    e.Volley = 5;
                    break;
                case KingDog:
                    e.MoveSpeed *= 1.3f;
                    break;
            }
        }

        protected override void AuthorityActStep(int act)
        {
            if (act == BossAct && DraculaFight != null)
            {
                DraculaFight.AuthorityStep(this);
            }

            GatesAuthority(act);
            CheckpointWatch(act);
        }

        /// <summary>Authority: a new run starts with nothing banked, no checkpoint, no mirror hits and no rank bonus yet.</summary>
        protected override void AuthorityRoundStart(int round)
        {
            SetQuestInt(QBanked, 0);
            SetQuestInt(QCheckpoint, 0);
            SetQuestInt(QHits, 0);
            SetQuestInt(QBonus, 0);
            base.AuthorityRoundStart(round);
        }

        protected override bool AuthorityActDone(int act)
        {
            return act != BossAct || DraculaFight == null || DraculaFight.Defeated(this);
        }

        protected override void AuthorityHazard(int playerId, int code, int arg)
        {
            if (code == HazardMirror && ActIndex == BossAct && DraculaFight != null)
            {
                DraculaFight.AuthorityMirror(this, playerId, arg);
            }
        }

        /// <summary>Every client: fires light mirror index (the authority judges the hit).</summary>
        internal void FireMirror(int index)
        {
            if (Phase == RrPhase.Playing && ActIndex == BossAct)
            {
                Ask(RrQuestEvents.ReqHazard, (byte)HazardMirror, (byte)index);
            }
        }

        /// <summary>
        /// Authority: gate k of the current act opens once GateAfter[k] waves have started, nothing is queued and every
        /// enemy is beaten (Rec Room: "3 waves of Pumpkin Spicers, then a gate"). Once open it stays open for the act.
        /// </summary>
        private void GatesAuthority(int act)
        {
            var l = LevelOf(act);
            if (l == null || IsHall(act) || l.Gates.Count == 0)
            {
                return;
            }

            int bits = QuestInt(RrQuestInts.Hazards);
            int now = bits;
            for (int k = 0; k < l.Gates.Count && k < l.GateAfter.Count; k++)
            {
                int bit = 1 << (GateBit + k);
                if ((now & bit) == 0 && QuestInt(RrQuestInts.Wave) >= l.GateAfter[k] && Director.Idle && Enemies.Alive == 0)
                {
                    now |= bit;
                    RrLog.Info("Crescendo", "act " + (act + 1) + ": gate " + l.Gates[k].GateName + " opens");
                }
            }

            if (now != bits)
            {
                SetQuestInt(RrQuestInts.Hazards, now);
                Broadcast("The gate is open!");
            }
        }

        /// <summary>
        /// Authority: after the first Grand Hall visit a whole-team down does not end the run - the team is back in the
        /// Grand Hall of its last visit with the silver it banked there (7.1: "each return banks silver and is the
        /// checkpoint"). Judged 0.5 s after the last player went down (the framework ends the run after 1 s).
        /// </summary>
        private void CheckpointWatch(int act)
        {
            int cp = QuestInt(QCheckpoint) - 1;
            if (cp < 0 || act <= cp || StandingPlayerFeet().Count > 0)
            {
                _wipeSince = -1f;
                return;
            }

            if (_wipeSince < 0f)
            {
                _wipeSince = Time.time;
                return;
            }

            if (Time.time - _wipeSince < 0.5f)
            {
                return;
            }

            _wipeSince = -1f;
            int banked = QuestInt(QBanked);
            RrLog.Info("Crescendo", "the team is down in act " + (act + 1) + ": back to the Grand Hall (act " + (cp + 1) + "), silver " + Score + " -> banked " + banked);
            SetQuestInt(RrQuestInts.Score, banked);
            EnterActAuthority(cp);
            Broadcast("Back to the Grand Hall - unbanked silver lost");
        }

        protected override void OnActStage(int act, RrQuestStage stage)
        {
            if (stage == RrQuestStage.Intro)
            {
                if (act == BossAct && DraculaFight != null)
                {
                    // a fresh fight on every arrival (also after a return to the Grand Hall)
                    DraculaFight.ResetBoss();
                }

                if (act == Ballroom)
                {
                    RrMusic.Play(BallroomMusic, 0.5f);
                }

                if (IsHall(act))
                {
                    Bank(act);
                }
                else if (act == Carriage)
                {
                    Hud.Toast("Hold on! They're coming from both sides", 4f);
                }
                else if (act == Dungeon)
                {
                    Hud.Toast("Watch out for the arrow traps", 4f);
                }
            }
            else if (stage == RrQuestStage.Clear)
            {
                if (act == Carriage)
                {
                    Hud.Toast("The carriage crashes at the castle!", 4f);
                }
                else if (act == BossAct)
                {
                    Hud.Toast("Dracula is defeated!", 4f);
                    RankBonus();
                }
                else if (IsHall(act))
                {
                    Hud.Toast("The next door is open", 3f);
                }
            }
        }

        /// <summary>A Grand Hall visit: the team's silver is banked (Rec Room: "YOU BANKED {0} SILVER") and it is the checkpoint.</summary>
        private void Bank(int act)
        {
            if (IsAuthority && Phase == RrPhase.Playing)
            {
                SetQuestInt(QBanked, Score);
                SetQuestInt(QCheckpoint, act + 1);
            }

            if (_bankedShownAct != act)
            {
                _bankedShownAct = act;
                Hud.Toast("You banked " + Score.ToString("N0") + " silver", 4f);
            }
        }

        /// <summary>Authority, as Dracula falls: Rec Room's rank bonus in silver (LootManager rankBonuses).</summary>
        private void RankBonus()
        {
            if (!IsAuthority || QuestInt(QBonus) != 0)
            {
                return;
            }

            string rank = RankOf(QuestInt(RrQuestInts.Downs));
            int bonus = rank == "S+" ? 15000 : rank == "S" ? 10000 : rank == "A" ? 6000 : rank == "B" ? 3000 : 2000;
            SetQuestInt(QBonus, bonus);
            var at = DraculaFight != null && DraculaFight.Stage != null ? DraculaFight.Stage.position : transform.position;
            AddPoints(bonus, at, RrQuestEvents.PointsBoss);
            RrLog.Info("Crescendo", "rank " + rank + " (" + CombatScore(QuestInt(RrQuestInts.Downs)) + " combat score): bonus " + bonus + " silver");
        }

        /// <summary>The hidden combat score for a number of downs (7.1).</summary>
        public static int CombatScore(int downs)
        {
            return Mathf.Max(0, CombatBase - CombatPerDown * Mathf.Max(0, downs));
        }

        /// <summary>Rec Room's rank for a number of downs: S+ >= 4,160, S >= 3,160, A up to 8 downs, B up to 12, else C.</summary>
        public static string RankOf(int downs)
        {
            int s = CombatScore(downs);
            return s >= 4160 ? "S+" : s >= 3160 ? "S" : s >= CombatBase - 8 * CombatPerDown ? "A" : s >= CombatBase - 12 * CombatPerDown ? "B" : "C";
        }

        protected override void QuestStep(int act)
        {
            ShowAreas(act);
            ShowLighting(act);
            bool playing = Phase == RrPhase.Playing;
            var l = LevelOf(act);
            if (l != null && (playing || Phase == RrPhase.MatchOver))
            {
                int bits = QuestInt(RrQuestInts.Hazards);
                for (int k = 0; k < l.Gates.Count; k++)
                {
                    if (l.Gates[k] != null)
                    {
                        l.Gates[k].SetOpen(!IsHall(act) && (bits & (1 << (GateBit + k))) != 0);
                    }
                }
            }

            if (DraculaFight != null)
            {
                DraculaFight.ClientStep(this, act == BossAct && playing && Stage != RrQuestStage.Intro);
            }

            if (Traps != null)
            {
                Traps.Step(this, act == Dungeon && playing && Stage == RrQuestStage.Combat);
            }

            if (playing)
            {
                FallRescue(act);
            }

            if (Phase == RrPhase.MatchOver && !_rankShown && QuestInt(RrQuestInts.Outcome) != 0)
            {
                ShowRank();
            }
        }

        private int _fallAct = -1;
        private float _fallGuard;

        /// <summary>
        /// Every client: a local player who fell far below the act (off the carriage, through a gap in a rebuilt wall) is
        /// put back at the act's arrival; judged from 2 s after arriving (the teleport into an act lands a frame later).
        /// </summary>
        private void FallRescue(int act)
        {
            var a = CurrentAct;
            if (a == null || a.Root == null || act < 0)
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
            float floor = Mathf.Min(a.Entry.y - 25f, a.Area.min.y - 5f);
            if (local.y > floor || Mathf.Abs(local.x - a.Area.center.x) > a.Area.extents.x + 40f || Mathf.Abs(local.z - a.Area.center.z) > a.Area.extents.z + 40f)
            {
                return;
            }

            _fallGuard = Time.time + 2f;
            var p = ActEntryWorld(act, Rank());
            RrPlayer.Teleport(p.position, p.rotation.eulerAngles.y);
            Hud.Toast("You fell - back to the start of the act", 2.5f);
            RrLog.Info("Crescendo", "local player fell below act " + (act + 1) + " (y " + local.y.ToString("F1") + "): back at its arrival");
        }

        /// <summary>The end screen with Rec Room's rank (the framework's title, its rating replaced by the combat-score rank).</summary>
        private void ShowRank()
        {
            _rankShown = true;
            bool won = QuestInt(RrQuestInts.Outcome) == 1;
            int downs = QuestInt(RrQuestInts.Downs);
            string rank = RankOf(downs);
            string sub = Score.ToString("N0") + " SILVER   RANK " + rank + "   DOWNS " + downs;
            if (won)
            {
                Hud.Title("OUR HEROES ARE VICTORIOUS!", sub, Def.VictorySeconds);
            }
            else
            {
                Hud.Title("OUR HEROES HAVE FAILED", sub, Def.FailSeconds, new Color(1f, 0.45f, 0.4f));
            }

            RrLog.Info("Crescendo", (won ? "victory" : "failed") + ": " + sub + " (combat score " + CombatScore(downs) + ")");
        }

        /// <summary>The level's own lighting (<see cref="RrEnvironment.SetArea"/>) and Rec Room's grade for act + 1 (RrPostFxProfiles' Crescendo areas).</summary>
        private void ShowLighting(int act)
        {
            string scene = SceneOf(act);
            if (scene != _envScene)
            {
                _envScene = scene;
                RrLog.Info("Crescendo", "lighting: " + scene + (act >= 0 ? " (act " + (act + 1) + ")" : " (lobby)"));
            }

            RrEnvironment.SetArea(scene);
            RrPostFx.SetArea(act + 1);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_envScene != null && RrEnvironment.Activity == RrActivity.Crescendo)
            {
                RrEnvironment.SetArea(null);
            }

            _envScene = null;
        }

        protected override void OnRunReset()
        {
            _rankShown = false;
            _wipeSince = -1f;
            _bankedShownAct = -1;
            if (DraculaFight != null)
            {
                DraculaFight.ResetBoss();
            }

            foreach (var l in Levels)
            {
                foreach (var g in l.Gates)
                {
                    if (g != null)
                    {
                        g.SetOpen(false, true);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ harness hooks

        /// <summary>Harness: presses the Foyer console's START (or reports that there is none).</summary>
        public static void TestConsoleStart()
        {
            foreach (var c in RrGameConsole.All)
            {
                if (c != null && c.Spec != null && c.Spec.Title == "CRESCENDO OF THE BLOOD MOON")
                {
                    bool ok = c.Press("test");
                    RrLog.Info("Crescendo", "CbmTest: console START pressed (" + ok + ", label " + c.StartText + ")");
                    return;
                }
            }

            RrLog.Warn("Crescendo", "CbmTest: no Crescendo console");
        }

        /// <summary>Harness: jump to act n (1-based 1..13) during a run and wait for its combat.</summary>
        public static IEnumerator TestAct2() { return TestAct(Carriage); }

        public static IEnumerator TestAct3() { return TestAct(CastleGrounds); }

        public static IEnumerator TestAct4() { return TestAct(Hall1); }

        public static IEnumerator TestAct5() { return TestAct(Gallery); }

        public static IEnumerator TestAct6() { return TestAct(Library); }

        public static IEnumerator TestAct8() { return TestAct(Park); }

        public static IEnumerator TestAct9() { return TestAct(Ballroom); }

        public static IEnumerator TestAct11() { return TestAct(Dungeon); }

        public static IEnumerator TestBoss() { return TestAct(BossAct); }

        private static IEnumerator TestAct(int a)
        {
            var q = Current as RrCrescendoRoom;
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
            RrLog.Info("Crescendo", "CbmTest: act " + (a + 1) + " - " + q.Describe() + ", player at " + RrPlayer.FeetPosition.ToString("F1"));
        }

        /// <summary>
        /// Harness: fights the current act to its clear as a player would (max 90 s): every 0.4 s each living enemy takes a
        /// 2,000-damage hit from the local player (Dracula: a mirror is fired instead); with nothing alive the player walks
        /// into the next Enter wave's zone. Logs whether the act cleared, with the kills and silver.
        /// </summary>
        public static IEnumerator TestFightClear()
        {
            var q = Current as RrCrescendoRoom;
            if (q == null || q.CurrentAct == null)
            {
                yield break;
            }

            int act = q.ActIndex;
            int mirror = 0;
            float end = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < end && q.ActIndex == act && q.Stage != RrQuestStage.Clear && q.Phase == RrPhase.Playing)
            {
                int hit = 0;
                for (int s = 0; s < RrQuestEnemies.Slots; s++)
                {
                    var e = q.Enemies.At(s);
                    if (e != null && e.Alive && e.State != RrEnemyState.Spawning && q.Enemies.TypeAt(s) != Dracula)
                    {
                        e.TakeLocalHit(2000, RrBodyPart.WeakSpot, e.Center, RrCombat.UnknownTool);
                        hit++;
                    }
                }

                if (act == BossAct)
                {
                    q.FireMirror(mirror++ % 3);
                }
                else if (hit == 0 && q.Enemies.Alive == 0)
                {
                    q.TestIntoNextZone();
                }

                yield return new WaitForSeconds(act == BossAct ? 1.5f : 0.4f);
            }

            RrLog.Info("Crescendo", "CbmTest: fight " + (q.Stage == RrQuestStage.Clear ? "CLEAR" : "NOT CLEAR") + " in act " + (act + 1) + " - " + q.Describe());
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

        /// <summary>Harness: walks the local player onto the current act's exit (the door's zone), so the team advances.</summary>
        public static void TestToExit()
        {
            var q = Current as RrCrescendoRoom;
            var act = q != null ? q.CurrentAct : null;
            if (act == null || act.ExitDoor == null)
            {
                return;
            }

            var t = act.ExitDoor.transform;
            Vector3 c = act.ExitDoor.Zone.HasValue ? act.ExitDoor.Zone.Value.center : new Vector3(0f, 0f, 1f);
            Vector3 top = t.TransformPoint(c + Vector3.up * 3f);
            RaycastHit hit;
            Vector3 feet = Physics.Raycast(top, Vector3.down, out hit, 12f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore) ? hit.point + Vector3.up * 0.05f : t.TransformPoint(c);
            RrPlayer.Teleport(feet, t.eulerAngles.y);
            RrLog.Info("Crescendo", "CbmTest: to the exit of act " + (q.ActIndex + 1) + " at " + feet.ToString("F1"));
        }
    }
}
