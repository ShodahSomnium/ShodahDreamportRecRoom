using System.Collections;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.DiscGolf
{
    /// <summary>
    /// Rec Room's Disc Golf as it was at the shutdown (rr_latest_rgames_action.md 8, rr_sports.md 5): 1-4 players, a
    /// 9-hole course, stroke play, the mercy rule at 7, a throw counts only when the disc travels more than 2.5 m
    /// ([DiscGolfManager] strokeIncrementThreshold), lowest total wins. On Rec Room's own two courses, rebuilt from the
    /// Android levels (<see cref="RrDiscGolfLevel"/>): the Lake (par 28: water hazards that no longer KO players since 2019,
    /// three minus-stroke bonus panels) and Propulsion (par 26: fans, red hazard panels, power-ups); the course is chosen
    /// on the lobby hologram (the LaserTag map pattern: one course exists at a time, the other is built in its own slot
    /// while the lobby still stands).
    ///
    /// Flow: START on the hologram (the level's ScoreboardProjector by the pregame spawns) makes the members the golfers
    /// (join order, up to 4; each gets the disc of their slot's colour). Countdown: everyone to hole 1's tee. Each hole:
    /// every golfer throws from the tee (no turn order, GUESS); when the disc comes to rest the golfer is moved beside it
    /// with the disc back in hand (GUESS: Rec Room's gravityPickupDistanceOverride 1.5 suggests the disc was picked up
    /// where it lay) and throws again; the disc in the basket ends the golfer's hole ([VFX_DiscGoalRays], CrowdCheer01,
    /// the score word); 7 strokes end it with 7 (mercy). A hazard (water, a red panel, out of the course) costs the throw
    /// plus one stroke and puts the golfer and the disc back where the throw left (GUESS, research 5.2); a bonus panel
    /// flown through takes one stroke off (once per panel per game). When every golfer still here is done, 4 s later the
    /// whole group moves to the next tee; after hole 9 the lowest total wins, the card stays up through the results and
    /// everyone returns to the lobby.
    ///
    /// Networking: the golfer's client simulates and judges its own disc (RrNetBody: the thrower simulates until rest) and
    /// reports its hole (<see cref="EvStatus"/> to the authority, repeated until the instance int shows it); the authority
    /// writes the card (<see cref="RrDiscGolfScore"/>), moves the group on and picks the winner. Basket, hazard and bonus
    /// effects go to everyone (<see cref="EvHoled"/>, <see cref="EvHazard"/>, <see cref="EvBonus"/>). The course is State.Map.
    /// </summary>
    public sealed class RrDiscGolfRoom : RrGameRoom
    {
        public const string Music = "RecRoomTheme2";
        public const string Art = "Giftbox_DiscGolf_Tex";
        public const string DiscPrefab = "(DiscGolfDisc)";

        public const float StrokeThreshold = 2.5f;
        public const float NextHoleSeconds = 4f;

        /// <summary>A hole open this long ends for whoever is still on it (mercy score; ours, so a lost player cannot stall the group).</summary>
        public const float HoleTimeout = 360f;

        // ---- events (player -> authority; to all) ----
        public const byte EvStatus = 100;
        public const byte EvHoled = 101;
        public const byte EvHazard = 102;
        public const byte EvBonus = 103;

        public static RrDiscGolfRoom Current { get; private set; }

        public readonly RrDiscGolfDisc[] Discs = new RrDiscGolfDisc[RrDiscGolfScore.MaxGolfers];

        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private RrDiscGolfLevel _level;
        private RrDiscGolfLevel _building;
        private bool _entered;
        private int _pendingMap = -1;
        private float _switchAt;
        private int _wantMap = -1;
        private float _nextVote;
        private string _envScene;
        private bool _discsSpawned;

        // ---- the local golfer ----
        private int _seenHole = -1;
        private bool _wasGolfer;
        private RrDiscGolfScore.Live _live = RrDiscGolfScore.Live.For(-1);
        private float _statusAt;
        private Vector3 _lie;
        private readonly HashSet<int> _bonusTaken = new HashSet<int>();
        private float _giveAt = -1f;
        private bool _teleportBeforeGive;
        private Pose _teleportTo;
        private int _localThrows;

        // ---- every client: boards, beam, HUD, toast ----
        private TMPro.TextMeshPro _lobbySheet, _teeSheet, _hud, _toast;
        private GameObject _beam;
        private float _sheetAt, _grabAt, _toastUntil;
        private int _beamHole = -2;
        private Vector3 _hudPos;
        private bool _hudPlaced;

        // ---- authority ----
        private float _allDoneAt = -1f;
        private float _holeOpenedAt;
        private int _authHole = -1;

        // ------------------------------------------------------------------ tuning

        protected override int TeamCount
        {
            get { return 0; }
        }

        protected override int MinPlayersToStart
        {
            get { return 1; }
        }

        protected override float CountdownSeconds
        {
            get { return 5f; }
        }

        protected override float RoundSeconds
        {
            get { return 0f; }
        }

        protected override float RoundOverSeconds
        {
            get { return 5f; }
        }

        protected override float MatchOverSeconds
        {
            get { return 12f; }
        }

        protected override bool AutoStart
        {
            get { return false; }
        }

        protected override string PlayMusic
        {
            get { return RrDiscGolfCourses.Music[CourseIndex]; }
        }

        protected override string LobbyMusic
        {
            get { return RrDiscGolfCourses.Music[CourseIndex]; }
        }

        // ------------------------------------------------------------------ state

        public int CourseIndex
        {
            get { return RrDiscGolfCourses.Clamp(State.Map); }
        }

        public bool CourseReady
        {
            get { return _level != null && _level.Done && _level.Alive && _level.Course == CourseIndex; }
        }

        public int Hole
        {
            get { return State.Get(RrDiscGolfScore.IntHole); }
        }

        public int MySlot
        {
            get { return Phase == RrPhase.Lobby ? -1 : RrDiscGolfScore.SlotOf(State, RrNet.LocalId); }
        }

        private bool LocalPlaying
        {
            get { return Phase == RrPhase.Playing && MySlot >= 0 && Hole < RrDiscGolfCourses.Holes && CourseReady; }
        }

        // ------------------------------------------------------------------ build

        /// <summary>The chosen course (its lobby is where everyone arrives) and the four discs, behind the loading card.</summary>
        public override IEnumerator Build()
        {
            Current = this;
            float t0 = Time.realtimeSinceStartup;
            BuildProgress = 0f;
            var portal = RrPortal.Prepare();
            while (portal.MoveNext())
            {
                BuildProgress = 0.03f;
                yield return portal.Current;
            }

            BuildProgress = 0.05f;
            var level = new RrDiscGolfLevel(CourseIndex);
            _building = level;
            yield return level.Build(transform, _surfaces);
            _building = null;
            if (this == null)
            {
                yield break;
            }

            _level = level;
            BuildProgress = 0.8f;
            Setup(level);
            BuildRoomUi();
            yield return SpawnDiscs(level);
            BuildProgress = 1f;
            RrLog.Info("DiscGolf", "Disc Golf built in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s on " + level.Name + " (" + (level.FromData ? level.Scene + ".bytes" : "stand-in greens") +
                                   "): " + CountDiscs() + " discs (" + DiscPrefab + "), console " + (level.Hologram != null ? "ready" : "MISSING") + ", par " + RrDiscGolfCourses.TotalPar(level.Course));
        }

        private int CountDiscs()
        {
            int n = 0;
            foreach (var d in Discs)
            {
                n += d != null ? 1 : 0;
            }

            return n;
        }

        /// <summary>What the room adds to a built course: the console, the Rec Center door, the lobby card, the tee card, the basket beam.</summary>
        private void Setup(RrDiscGolfLevel level)
        {
            if (level == null || !level.Alive)
            {
                return;
            }

            var root = level.Root;
            level.Hologram = Console(level);
            RrPortal.Create(root, RrActivity.RecCenter, level.L(RrDiscGolfCourses.Doors[level.Course]), RrDiscGolfCourses.DoorYaws[level.Course], "REC CENTER");

            // The card beside the console, readable from the lobby (a TMP label reads from behind its +Z).
            var c = level.Console;
            var back = c.rotation * Quaternion.Euler(0f, 180f, 0f);
            _lobbySheet = Sheet(root, "Lobby Card", c.position + c.rotation * new Vector3(-5.4f, 2.3f, 0.2f), back);
            _teeSheet = Sheet(root, "Tee Card", level.TeeBoards[0].position, level.TeeBoards[0].rotation);
            _beam = RrBuild.Cylinder(root, "Basket Beam", level.Goals[0] + Vector3.up * 21.3f, 0.07f, 40f, RrLook.Emissive(RrLook.Yellow, 2f), false);
            _beamHole = -2;
            float lowest = transform.TransformPoint(new Vector3(0f, level.OutBelow, 0f)).y - 5f;
            LowestFloor = float.IsNaN(LowestFloor) ? lowest : Mathf.Min(LowestFloor, lowest);
            _sheetAt = 0f;
        }

        private static TMPro.TextMeshPro Sheet(Transform root, string name, Vector3 at, Quaternion rot)
        {
            var tmp = RrBuild.Sign(root, name, "", at, rot, 1.1f, Color.white, false, 5.6f);
            tmp.rectTransform.sizeDelta = new Vector2(5.6f, 2.4f);
            tmp.alignment = TMPro.TextAlignmentOptions.Top;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            RrBuild.Box(tmp.transform, "Back", new Vector3(0f, -0.15f, 0.03f), new Vector3(5.8f, 2.6f, 0.02f), RrLook.Solid(new Color(0.05f, 0.09f, 0.08f)), false);
            return tmp;
        }

        /// <summary>The HUD line and the score-word toast (room-level, they outlive a course switch).</summary>
        private void BuildRoomUi()
        {
            if (_hud == null)
            {
                _hud = RrBuild.Sign(transform, "Disc Golf HUD", "", Vector3.zero, Quaternion.identity, 0.9f, Color.white, false, 2.6f);
                _hud.gameObject.SetActive(false);
            }

            if (_toast == null)
            {
                _toast = RrBuild.Sign(transform, "Disc Golf Toast", "", Vector3.zero, Quaternion.identity, 3f, RrLook.Yellow, true, 8f);
                _toast.gameObject.SetActive(false);
            }
        }

        private RrGameConsole Console(RrDiscGolfLevel level)
        {
            var maps = new string[RrDiscGolfCourses.Count];
            for (int i = 0; i < maps.Length; i++)
            {
                maps[i] = RrDiscGolfCourses.Names[i].ToUpperInvariant();
            }

            var spec = new RrGameConsoleSpec
            {
                Title = "DISC GOLF",
                TeamCount = 0,
                MinPlayers = 1,
                MaxPlayers = RrDiscGolfScore.MaxGolfers,
                Maps = maps,
                MapIndex = () => CourseIndex,
                OnMap = m => VoteCourse(m),
                OnStart = id => PressStart(),
                CanStart = () => Phase == RrPhase.Lobby && CourseReady,
                Status = () => Phase == RrPhase.Lobby && !CourseReady ? "LOADING " + RrDiscGolfCourses.Names[CourseIndex].ToUpperInvariant() + "..." : Phase == RrPhase.Playing ? HoleLine() : null,
                Timer = () => RrGameConsole.PhaseTimer(this),
                Subtitle = () => Phase == RrPhase.Lobby ? "9 HOLES - PAR " + RrDiscGolfCourses.TotalPar(CourseIndex) + " - LOWEST SCORE WINS" : RrGameConsole.PhaseLine(this),
            };
            return RrGameConsole.ForGame(this, level.Root, level.Console, spec);
        }

        /// <summary>The four discs (net props 0-3), under the room so they outlive a course switch.</summary>
        private IEnumerator SpawnDiscs(RrDiscGolfLevel level)
        {
            if (_discsSpawned)
            {
                yield break;
            }

            _discsSpawned = true;
            int waiting = 0;
            for (int i = 0; i < RrDiscGolfScore.MaxGolfers; i++)
            {
                int slot = i;
                Vector3 home = LobbyDiscSpot(level, slot);
                waiting++;
                RrSpawn.Spawn(DiscPrefab, transform, home, Quaternion.identity, inst =>
                {
                    SetupDisc(slot, inst, home);
                    waiting--;
                });
            }

            float until = Time.realtimeSinceStartup + 30f;
            while (waiting > 0 && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            for (int i = 0; i < Discs.Length; i++)
            {
                if (Discs[i] == null)
                {
                    SetupDisc(i, null, LobbyDiscSpot(level, i));
                }
            }
        }

        private static Vector3 LobbyDiscSpot(RrDiscGolfLevel level, int slot)
        {
            return level.L(RrDiscGolfCourses.LobbyDiscs[level.Course][slot]);
        }

        private void SetupDisc(int slot, RrInstance inst, Vector3 home)
        {
            GameObject go;
            RrGrabbable grab = null;
            if (inst != null)
            {
                grab = RrTools.MakeTool(inst);
                go = grab != null ? grab.gameObject : inst.gameObject;
            }
            else
            {
                RrLog.Warn("DiscGolf", DiscPrefab + " did not load: a plain disc stands in for slot " + slot);
                go = RrBuild.Cylinder(transform, "Disc " + slot, home, RrDiscGolfDisc.Radius, 0.028f, RrLook.Solid(RrTeams.Color(slot)), true, RrLayers.Prop);
                go.AddComponent<Rigidbody>();
            }

            if (grab == null)
            {
                grab = go.GetComponent<RrGrabbable>() ?? go.AddComponent<RrGrabbable>();
                grab.PickupSound = "Catch01";
            }

            var respawn = go.GetComponent<RrRespawnable>();
            if (respawn != null)
            {
                Destroy(respawn);
            }

            var net = go.GetComponent<RrNetBody>() ?? go.AddComponent<RrNetBody>();
            net.LocalPropId = slot;
            var disc = go.AddComponent<RrDiscGolfDisc>();
            disc.Setup(this, slot, grab, net, home);
            disc.ReturnHome();
            Discs[slot] = disc;
            if (slot == 0)
            {
                var b = inst != null ? inst.WorldBounds() : new Bounds(go.transform.position, Vector3.one * 0.3f);
                RrLog.Info("DiscGolf", "disc " + DiscPrefab + ": " + b.size.ToString("F3") + " m, grab " + (grab != null) + ", screen throw charge " + RrDiscGolfDisc.MinChargeSpeed + " -> " + RrDiscGolfDisc.MaxChargeSpeed + " m/s over " +
                                       RrDiscGolfDisc.MaxChargeSeconds + " s");
            }
        }

        // ------------------------------------------------------------------ the course level (LaserTag's map switch)

        private void KeepCourseBuilt()
        {
            int want = CourseIndex;
            if (_building != null)
            {
                return;
            }

            if (_level != null && _level.Course == want)
            {
                _pendingMap = -1;
                return;
            }

            if (_pendingMap != want)
            {
                _pendingMap = want;
                _switchAt = Time.time + (Phase == RrPhase.Lobby ? 1.2f : 0f);
                return;
            }

            if (Time.time < _switchAt)
            {
                return;
            }

            _pendingMap = -1;
            _building = new RrDiscGolfLevel(want);
            StartCoroutine(SwitchCourse(_building));
        }

        private IEnumerator SwitchCourse(RrDiscGolfLevel level)
        {
            RrLog.Info("DiscGolf", "course switch: building " + level.Name + " (" + level.Scene + ")");
            yield return level.Build(transform, _surfaces);
            if (this == null)
            {
                yield break;
            }

            _building = null;
            if (!level.Alive)
            {
                yield break;
            }

            if (level.Course != CourseIndex)
            {
                level.Destroy();
                yield break;
            }

            var old = _level;
            _level = level;
            Setup(level);
            foreach (var d in Discs)
            {
                if (d != null)
                {
                    d.HomeLocal = LobbyDiscSpot(level, d.Slot);
                    if (d.HeldLocally)
                    {
                        RrGrab.Release(d.Grab, Vector3.zero, Vector3.zero);
                    }

                    if (IsAuthority || !RrNet.Online)
                    {
                        d.ReturnHome();
                    }
                }
            }

            if (_entered)
            {
                TeleportLocal(Spawn(Mathf.Abs(RrNet.LocalId)));
                RrMusic.Play(LobbyMusic);
            }

            if (old != null)
            {
                old.Destroy();
            }

            RrLog.Info("DiscGolf", "course switch: now on " + level.Name + (old != null ? ", " + old.Name + " freed" : ""));
        }

        private void ShowLighting()
        {
            string scene = _level != null ? _level.Scene : null;
            if (scene == _envScene)
            {
                return;
            }

            _envScene = scene;
            RrEnvironment.SetArea(scene);
        }

        // ------------------------------------------------------------------ arrival / leaving

        public override Pose Spawn(int index)
        {
            var level = _level;
            if (level != null && level.Lobby.Count > 0)
            {
                var p = level.Lobby[((index % level.Lobby.Count) + level.Lobby.Count) % level.Lobby.Count];
                return new Pose(p.position, p.rotation);
            }

            return new Pose(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
        }

        private void TeleportLocal(Pose local, System.Action done = null)
        {
            RrPlayer.Teleport(transform.TransformPoint(local.position), (transform.rotation * local.rotation).eulerAngles.y, done);
        }

        public override void OnLocalEnter()
        {
            Current = this;
            base.OnLocalEnter();
            _entered = true;
            _envScene = null;
            _seenHole = -1;
            ShowLighting();
            RrLog.Info("DiscGolf", "entered: " + RrDiscGolfCourses.Names[CourseIndex] + ", phase " + Phase + ", authority " + IsAuthority + ", members " + Members().Count);
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            _entered = false;
            ReleaseHeldDiscs();
            if (_hud != null)
            {
                _hud.gameObject.SetActive(false);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RrNet.On(EvStatus, OnStatus);
            RrNet.On(EvHoled, OnHoled);
            RrNet.On(EvHazard, OnHazard);
            RrNet.On(EvBonus, OnBonus);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RrNet.Off(EvStatus, OnStatus);
            RrNet.Off(EvHoled, OnHoled);
            RrNet.Off(EvHazard, OnHazard);
            RrNet.Off(EvBonus, OnBonus);
            if (_envScene != null && RrEnvironment.Activity == RrActivity.DiscGolf)
            {
                RrEnvironment.SetArea(null);
            }

            _envScene = null;
        }

        private void OnDestroy()
        {
            if (_building != null)
            {
                _building.Destroy();
                _building = null;
            }

            if (Current == this)
            {
                Current = null;
            }
        }

        private void ReleaseHeldDiscs()
        {
            foreach (var d in Discs)
            {
                if (d != null && d.HeldLocally)
                {
                    RrGrab.Release(d.Grab, Vector3.zero, Vector3.zero);
                }
            }
        }

        // ------------------------------------------------------------------ the lobby console

        public void VoteCourse(int course)
        {
            if (Phase != RrPhase.Lobby || course < 0 || course >= RrDiscGolfCourses.Count)
            {
                return;
            }

            _wantMap = course;
            _nextVote = 0f;
            RrLog.Info("DiscGolf", "course vote: " + RrDiscGolfCourses.Names[course]);
        }

        public void PressStart()
        {
            if (Phase != RrPhase.Lobby)
            {
                return;
            }

            if (!CourseReady)
            {
                RrLog.Info("DiscGolf", "START pressed while " + RrDiscGolfCourses.Names[CourseIndex] + " is still building: ignored");
                return;
            }

            RrLog.Info("DiscGolf", "START pressed (" + Members().Count + " in the lobby, " + RrDiscGolfCourses.Names[CourseIndex] + ")");
            RequestStart();
        }

        private void RetryVotes(RrInstanceState s)
        {
            if (_wantMap >= 0 && s.Map == _wantMap)
            {
                _wantMap = -1;
            }

            if (_wantMap < 0 || Time.time < _nextVote)
            {
                return;
            }

            _nextVote = Time.time + 0.4f;
            if (IsAuthority)
            {
                s.Map = _wantMap;
                return;
            }

            RrNet.Send(RrGameEvents.GameRequest, new[] { RrGameEvents.Requests.VoteMap, (byte)_wantMap }, true);
        }

        protected override void AuthorityRequest(int playerId, byte request, byte arg)
        {
            if (Phase == RrPhase.Lobby && request == RrGameEvents.Requests.VoteMap && arg < RrDiscGolfCourses.Count)
            {
                State.Map = arg;
            }
        }

        // ------------------------------------------------------------------ authority

        protected override void AuthorityMatchSetup()
        {
            var s = State;
            var members = Members();
            members.Sort();
            int n = Mathf.Min(RrDiscGolfScore.MaxGolfers, members.Count);
            s.Set(RrDiscGolfScore.IntCount, n);
            for (int i = 0; i < RrDiscGolfScore.MaxGolfers; i++)
            {
                s.Set(RrDiscGolfScore.IntGolfers + i, i < n ? members[i] + 1 : 0);
                s.Set(RrDiscGolfScore.IntLive + i, 0);
                RrDiscGolfScore.ClearSheet(s, i);
                if (i < n)
                {
                    s.SetPlayerScore(members[i], 0);
                }
            }

            s.Set(RrDiscGolfScore.IntHole, 0);
            s.Set(RrDiscGolfScore.IntWinner, 0);
            _allDoneAt = -1f;
            _authHole = -1;
            RrLog.Info("DiscGolf", "game set up on " + RrDiscGolfCourses.Names[CourseIndex] + ": " + n + " golfer(s), 9 holes, par " + RrDiscGolfCourses.TotalPar(CourseIndex));
        }

        protected override void AuthorityRoundStart(int round)
        {
            _holeOpenedAt = Time.unscaledTime;
            _authHole = 0;
            RrLog.Info("DiscGolf", "tee off: hole 1 (par " + RrDiscGolfCourses.Par(CourseIndex, 0) + ")");
        }

        protected override void AuthorityPlaying()
        {
            var s = State;
            int hole = s.Get(RrDiscGolfScore.IntHole);
            if (hole >= RrDiscGolfCourses.Holes)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (hole != _authHole)
            {
                _authHole = hole;
                _holeOpenedAt = now;
                _allDoneAt = -1f;
            }

            var members = Members();
            int n = RrDiscGolfScore.GolferCount(s);
            bool anyHere = false, allDone = true;
            for (int i = 0; i < n; i++)
            {
                int id = RrDiscGolfScore.GolferId(s, i);
                if (!members.Contains(id))
                {
                    continue;
                }

                anyHere = true;
                var live = RrDiscGolfScore.LiveOf(s, i);
                if (live.Hole != hole || !live.Done)
                {
                    allDone = false;
                }
            }

            if (!anyHere)
            {
                RrLog.Info("DiscGolf", "every golfer left: game over");
                FinishGame();
                return;
            }

            if (!allDone && now - _holeOpenedAt > HoleTimeout)
            {
                RrLog.Info("DiscGolf", "hole " + (hole + 1) + " open for " + HoleTimeout + " s: the golfers still on it take the mercy score");
                for (int i = 0; i < n; i++)
                {
                    var live = RrDiscGolfScore.LiveOf(s, i);
                    if (live.Hole != hole || !live.Done)
                    {
                        var m = RrDiscGolfScore.Live.For(hole);
                        m.Done = true;
                        m.Mercy = true;
                        ApplyStatus(i, m);
                    }
                }

                allDone = true;
            }

            if (!allDone)
            {
                _allDoneAt = -1f;
                return;
            }

            if (_allDoneAt < 0f)
            {
                _allDoneAt = now;
            }
            else if (now - _allDoneAt >= NextHoleSeconds)
            {
                _allDoneAt = -1f;
                if (hole + 1 >= RrDiscGolfCourses.Holes)
                {
                    FinishGame();
                }
                else
                {
                    s.Set(RrDiscGolfScore.IntHole, hole + 1);
                    RrLog.Info("DiscGolf", "everyone is done with hole " + (hole + 1) + ": on to hole " + (hole + 2) + " (par " + RrDiscGolfCourses.Par(CourseIndex, hole + 1) + ")");
                }
            }
        }

        private void FinishGame()
        {
            var s = State;
            s.Set(RrDiscGolfScore.IntHole, RrDiscGolfCourses.Holes);
            int winner = Winner();
            s.Set(RrDiscGolfScore.IntWinner, winner + 1);
            RrLog.Info("DiscGolf", "round over: " + CardSummary() + (winner >= 0 ? "; winner " + NameOf(winner) : ""));
            EndRound(winner);
        }

        /// <summary>Lowest total among the golfers with the most holes played (a golfer who left early does not win).</summary>
        private int Winner()
        {
            var s = State;
            int n = RrDiscGolfScore.GolferCount(s);
            int best = -1, bestTotal = int.MaxValue, bestPlayed = -1;
            for (int i = 0; i < n; i++)
            {
                int played;
                int total = RrDiscGolfScore.Total(s, i, out played);
                if (played > bestPlayed || (played == bestPlayed && total < bestTotal))
                {
                    bestPlayed = played;
                    bestTotal = total;
                    best = RrDiscGolfScore.GolferId(s, i);
                }
            }

            return bestPlayed > 0 ? best : -1;
        }

        protected override void AuthorityRoundOver(int winner)
        {
            // Every client says its own line (YouWin for the winner).
        }

        protected override void AuthorityBackToLobby()
        {
            StartCoroutine(DiscsHomeSoon());
        }

        private IEnumerator DiscsHomeSoon()
        {
            // The golfers drop what they hold as the lobby starts; then the discs go back to their spots.
            yield return new WaitForSeconds(0.8f);
            foreach (var d in Discs)
            {
                if (d != null && !d.HeldByAnyone)
                {
                    d.ReturnHome();
                }
            }
        }

        /// <summary>Authority: a golfer's report for the current hole.</summary>
        private void OnStatus(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 4 || Phase != RrPhase.Playing)
            {
                return;
            }

            var s = State;
            int slot = body[0];
            if (slot >= RrDiscGolfScore.GolferCount(s) || RrDiscGolfScore.GolferId(s, slot) != sender)
            {
                return;
            }

            var live = RrDiscGolfScore.Live.Unpack(body[1] | (body[2] << 8) | (body[3] << 16));
            if (live.Hole != s.Get(RrDiscGolfScore.IntHole))
            {
                return;
            }

            var was = RrDiscGolfScore.LiveOf(s, slot);
            if (was.Hole == live.Hole && was.Done)
            {
                return;
            }

            ApplyStatus(slot, live);
        }

        private void ApplyStatus(int slot, RrDiscGolfScore.Live live)
        {
            var s = State;
            s.Set(RrDiscGolfScore.IntLive + slot, live.Pack());
            if (live.Done)
            {
                RrDiscGolfScore.SetHole(s, slot, live.Hole, live.Score);
                int played;
                int total = RrDiscGolfScore.Total(s, slot, out played);
                int id = RrDiscGolfScore.GolferId(s, slot);
                if (id >= 0)
                {
                    s.SetPlayerScore(id, Mathf.Clamp(total, 0, 1023));
                }

                RrLog.Info("DiscGolf", NameOf(id) + " finished hole " + (live.Hole + 1) + " with " + live.Score + (live.Mercy ? " (mercy)" : "") + " - " + live.Throws + " throws, " +
                                       live.Penalties + " penalties, " + live.Bonuses + " bonus; total " + total + " after " + played);
            }
        }

        // ------------------------------------------------------------------ phases (every client)

        protected override void OnPhase(RrPhase old, RrPhase now)
        {
            switch (now)
            {
                case RrPhase.Countdown:
                    _bonusTaken.Clear();
                    _seenHole = -1;
                    _live = RrDiscGolfScore.Live.For(-1);
                    TakeYourPlace(0);
                    break;
                case RrPhase.RoundOver:
                    int winner = State.Get(RrDiscGolfScore.IntWinner) - 1;
                    if (winner >= 0 && winner == RrNet.LocalId)
                    {
                        RrAnnouncer.Say("AmyVO_Generic_YouWin");
                    }
                    else
                    {
                        RrSound.PlayUi("CrowdCheer01", 0.6f);
                    }

                    if (winner >= 0)
                    {
                        Toast(NameOf(winner).ToUpperInvariant() + " WINS!", 6f);
                    }

                    break;
                case RrPhase.Lobby:
                    _giveAt = -1f;
                    ReleaseHeldDiscs();
                    if (_wasGolfer && old != (RrPhase)255)
                    {
                        TeleportLocal(Spawn(Mathf.Abs(RrNet.LocalId)));
                    }

                    _wasGolfer = false;
                    _seenHole = -1;
                    break;
            }

            RrLog.Info("DiscGolf", "phase " + old + " -> " + now + (now == RrPhase.Lobby ? "" : " (" + CardSummary() + ")"));
        }

        /// <summary>The local golfer to its spot at a tee (four spots side by side, facing the basket).</summary>
        private void TakeYourPlace(int hole, System.Action done = null)
        {
            int slot = RrDiscGolfScore.SlotOf(State, RrNet.LocalId);
            if (slot < 0 || _level == null || hole < 0 || hole >= RrDiscGolfCourses.Holes)
            {
                return;
            }

            _wasGolfer = true;
            var spot = TeeSpot(hole, slot);
            _lie = spot.position;
            TeleportLocal(spot, done);
        }

        private Pose TeeSpot(int hole, int slot)
        {
            var tee = _level.Tees[hole];
            Vector3 side = tee.rotation * Vector3.right;
            Vector3 back = tee.rotation * Vector3.back;
            Vector3 p = tee.position + side * ((slot - 1.5f) * 1.2f) + back * 0.6f;
            p = _level.Floor(p, 1.5f, 3f) + Vector3.up * 0.05f;
            return new Pose(p, tee.rotation);
        }

        /// <summary>A new hole for the local golfer: to the tee, disc in hand.</summary>
        private void NewHole(int hole)
        {
            int slot = MySlot;
            if (slot < 0 || _level == null)
            {
                return;
            }

            _live = RrDiscGolfScore.Live.For(hole);
            _statusAt = 0f;
            var disc = Discs[slot];
            if (disc != null)
            {
                disc.StopFlight();
            }

            _teleportBeforeGive = false;
            _giveAt = -1f;
            TakeYourPlace(hole, () => _giveAt = Time.time + 0.2f);
            _giveAt = Time.time + 1.5f;
            RrSound.PlayUi("AttentionWhistle", 0.4f);
            Toast("HOLE " + (hole + 1) + " - PAR " + RrDiscGolfCourses.Par(CourseIndex, hole), 3f);
            RrLog.Info("DiscGolf", "hole " + (hole + 1) + ": local golfer (slot " + slot + ", " + RrTeams.Name(slot) + " disc) to the tee " + _lie.ToString("F1") + ", basket " +
                                   Vector3.Distance(_lie, _level.Goals[hole]).ToString("0") + " m away, par " + RrDiscGolfCourses.Par(CourseIndex, hole));
        }

        // ------------------------------------------------------------------ the local golfer's disc (called by RrDiscGolfDisc)

        private bool IsMyTrackedDisc(RrDiscGolfDisc disc)
        {
            return disc != null && LocalPlaying && disc.Slot == MySlot && _live.Hole == Hole && !_live.Done;
        }

        internal void LocalGrabbed(RrDiscGolfDisc disc, bool wasFlying)
        {
            _giveAt = -1f;
            if (wasFlying && disc.Tracked && IsMyTrackedDisc(disc))
            {
                // Caught in the air: the throw ends where it was caught.
                Judge(disc, disc.transform.position, true);
            }
        }

        internal void LocalReleased(RrDiscGolfDisc disc, Vector3 velocity, bool screen)
        {
            disc.Tracked = IsMyTrackedDisc(disc) && disc.Flying;
            if (disc.Tracked)
            {
                _localThrows++;
                if (_localThrows <= 3)
                {
                    RrLog.Info("DiscGolf", "throw " + _localThrows + " (" + (screen ? "screen" : "VR") + ") at " + velocity.magnitude.ToString("F1") + " m/s, hole " + (Hole + 1) + ", stroke " + (_live.Strokes + 1));
                }
            }
        }

        /// <summary>The local flight of the golfer's own disc: basket, hazards, bonus panels.</summary>
        internal void LocalFlightStep(RrDiscGolfDisc disc, Vector3 from, Vector3 to)
        {
            if (!IsMyTrackedDisc(disc))
            {
                disc.Tracked = false;
                return;
            }

            Vector3 a = transform.InverseTransformPoint(from), b = transform.InverseTransformPoint(to);
            int hole = Hole;
            Vector3 goal = _level.Goals[hole];
            Vector2 flat = new Vector2(b.x - goal.x, b.z - goal.z);
            float up = b.y - goal.y;
            if (flat.sqrMagnitude < 0.36f * 0.36f && up > 0.42f && up < 1.3f)
            {
                Holed(disc);
                return;
            }

            if (InHazard(b))
            {
                Hazard(disc, b);
                return;
            }

            for (int i = 0; i < _level.Bonuses.Count; i++)
            {
                if (!_bonusTaken.Contains(i) && _level.Bonuses[i].Crossed(a, b))
                {
                    _bonusTaken.Add(i);
                    _live.Bonuses++;
                    SendStatus();
                    RrNet.Send(EvBonus, new[] { (byte)MySlot, (byte)i });
                    RrLog.Info("DiscGolf", "bonus panel " + i + " flown through: -1 on hole " + (hole + 1));
                }
            }
        }

        private bool InHazard(Vector3 local)
        {
            if (_level == null)
            {
                return false;
            }

            if (local.y < _level.OutBelow)
            {
                return true;
            }

            var area = _level.CourseArea;
            if (local.x < area.x || local.x > area.z || local.z < area.y || local.z > area.w)
            {
                return true;
            }

            foreach (var h in _level.Hazards)
            {
                // The Lake's water boxes end at the surface (top y 0.01): a disc floating on it is in.
                if (h.Contains(local, 0.1f))
                {
                    return true;
                }
            }

            return false;
        }

        internal void LocalLanded(RrDiscGolfDisc disc, bool tracked)
        {
            if (tracked && IsMyTrackedDisc(disc))
            {
                Judge(disc, disc.transform.position, false);
            }
        }

        /// <summary>A throw ended (rest or catch): counted when it went past 2.5 m; then the golfer goes to the disc.</summary>
        private void Judge(RrDiscGolfDisc disc, Vector3 world, bool caught)
        {
            disc.StopFlight();
            Vector3 local = transform.InverseTransformPoint(world);
            if (InHazard(local))
            {
                Hazard(disc, local);
                return;
            }

            float moved = Vector3.Distance(world, disc.ReleasePoint);
            bool counted = moved > StrokeThreshold;
            if (counted)
            {
                _live.Throws++;
                _lie = local;
            }

            Mercy();
            SendStatus();
            RrLog.Info("DiscGolf", "throw " + (counted ? "counted" : "not counted") + ": " + moved.ToString("F1") + " m" + (caught ? " (caught)" : "") + ", " +
                                   Vector3.Distance(local, _level.Goals[Hole]).ToString("0") + " m from the basket; hole " + (Hole + 1) + " strokes " + _live.Strokes + (_live.Done ? " - MERCY" : ""));
            if (_live.Done || caught)
            {
                return;
            }

            if (counted)
            {
                // Beside the disc, facing the basket, the disc back in hand.
                Vector3 to = _level.Goals[Hole] - local;
                to.y = 0f;
                Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward;
                Vector3 feet = _level.Floor(local - dir * 0.7f, 1.5f, 4f) + Vector3.up * 0.05f;
                _teleportTo = new Pose(feet, Quaternion.LookRotation(dir, Vector3.up));
                _teleportBeforeGive = true;
                _giveAt = Time.time + 0.8f;
            }
            else
            {
                _teleportBeforeGive = false;
                _giveAt = Time.time + 0.5f;
            }
        }

        private void Holed(RrDiscGolfDisc disc)
        {
            disc.StopFlight();
            _live.Throws++;
            _live.Done = true;
            SendStatus();
            int hole = Hole;
            int score = _live.Score;
            RrNet.Send(EvHoled, new[] { (byte)MySlot, (byte)hole, (byte)Mathf.Clamp(score + 8, 0, 255) });
            RrLog.Info("DiscGolf", "IN THE BASKET: hole " + (hole + 1) + " in " + score + " (par " + RrDiscGolfCourses.Par(CourseIndex, hole) + ", " + _live.Throws + " throws, " + _live.Penalties + " penalties, " + _live.Bonuses + " bonus)");
        }

        private void Hazard(RrDiscGolfDisc disc, Vector3 local)
        {
            disc.StopFlight();
            _live.Throws++;
            _live.Penalties++;
            Mercy();
            SendStatus();
            RrNet.Send(EvHazard, new[] { (byte)MySlot });
            RrLog.Info("DiscGolf", "hazard at " + local.ToString("F1") + ": +1, back to " + _lie.ToString("F1") + "; hole " + (Hole + 1) + " strokes " + _live.Strokes + (_live.Done ? " - MERCY" : ""));
            if (_live.Done)
            {
                return;
            }

            Vector3 to = _level.Goals[Hole] - _lie;
            to.y = 0f;
            Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward;
            _teleportTo = new Pose(_lie + Vector3.up * 0.05f, Quaternion.LookRotation(dir, Vector3.up));
            _teleportBeforeGive = true;
            _giveAt = Time.time + 0.6f;
        }

        private void Mercy()
        {
            if (!_live.Done && _live.Strokes >= RrDiscGolfScore.MercyScore)
            {
                _live.Done = true;
                _live.Mercy = true;
                Toast("MERCY RULE - " + RrDiscGolfScore.MercyScore, 3f);
            }
        }

        private void SendStatus()
        {
            int slot = MySlot;
            if (slot < 0)
            {
                return;
            }

            int v = _live.Pack();
            _statusAt = Time.unscaledTime + 0.5f;
            RrNet.Send(EvStatus, new[] { (byte)slot, (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF) }, true);
        }

        /// <summary>The golfer's pending move (to the disc / back to the lie) and the disc back in hand.</summary>
        private void GiveStep()
        {
            if (_giveAt < 0f || Time.time < _giveAt)
            {
                return;
            }

            _giveAt = -1f;
            int slot = MySlot;
            if (!LocalPlaying || slot < 0 || _live.Done)
            {
                return;
            }

            var disc = Discs[slot];
            if (disc == null)
            {
                return;
            }

            if (_teleportBeforeGive)
            {
                _teleportBeforeGive = false;
                TeleportLocal(_teleportTo, () => _giveAt = Time.time + 0.15f);
                _giveAt = Time.time + 1.5f;
                return;
            }

            if (disc.HeldLocally)
            {
                return;
            }

            var hand = RrHand.Get(RrHandSide.Right);
            if (hand != null && disc.Grab != null)
            {
                RrGrab.Give(disc.Grab, hand);
            }
            else if (disc.Grab != null)
            {
                // No hand to give it to (editor harness): the disc waits at the feet.
                disc.transform.position = RrPlayer.FeetPosition + Vector3.up * 0.3f;
            }
        }

        // ------------------------------------------------------------------ effects (every client)

        private void OnHoled(int sender, byte[] body)
        {
            if (body == null || body.Length < 3 || _level == null)
            {
                return;
            }

            int slot = body[0], hole = body[1], score = body[2] - 8;
            if (hole < 0 || hole >= RrDiscGolfCourses.Holes)
            {
                return;
            }

            Vector3 at = transform.TransformPoint(_level.Goals[hole] + Vector3.up * 0.8f);
            RrSound.Play("CrowdCheer01", at, 0.8f, 1f, 80f);
            RrFx.Spawn("[VFX_DiscGoalRays]", at, transform.rotation, 1f);
            int par = RrDiscGolfCourses.Par(CourseIndex, hole);
            string word = RrDiscGolfScore.Word(score, par);
            Toast((sender == RrNet.LocalId ? "" : NameOf(sender).ToUpperInvariant() + ": ") + word.ToUpperInvariant(), 3.5f);
            if (score <= par)
            {
                RrSound.Play("GolfClap", at, 0.7f, 1f, 80f);
            }
        }

        private void OnHazard(int sender, byte[] body)
        {
            if (body == null || body.Length < 1)
            {
                return;
            }

            int slot = body[0];
            var disc = slot < Discs.Length ? Discs[slot] : null;
            Vector3 at = disc != null ? disc.transform.position : RrPlayer.FeetPosition;
            RrSound.Play("OutSFX", at, 0.9f, 1f, 60f);
            if (sender == RrNet.LocalId)
            {
                Toast("HAZARD +1", 2.5f);
            }
        }

        private void OnBonus(int sender, byte[] body)
        {
            if (body == null || body.Length < 1)
            {
                return;
            }

            int slot = body[0];
            var disc = slot < Discs.Length ? Discs[slot] : null;
            Vector3 at = disc != null ? disc.transform.position : RrPlayer.FeetPosition;
            RrSound.Play("DiscGolf Propulsion SFX - Powerup 1", at, 0.9f, 1f, 60f);
            if (sender == RrNet.LocalId)
            {
                Toast("BONUS -1", 2.5f);
            }
        }

        /// <summary>Wind at a world point (Propulsion's fans).</summary>
        public Vector3 WindAt(Vector3 world)
        {
            if (_level == null || _level.Fans.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 local = transform.InverseTransformPoint(world);
            Vector3 sum = Vector3.zero;
            foreach (var f in _level.Fans)
            {
                if (f.Contains(local))
                {
                    sum += transform.TransformDirection(f.Axis) * RrDiscGolfDisc.FanAcceleration;
                }
            }

            return sum;
        }

        // ------------------------------------------------------------------ every frame

        protected override void OnTick(RrInstanceState s)
        {
            KeepCourseBuilt();
            if (!_entered)
            {
                return;
            }

            ShowLighting();
            if (s.Phase == RrPhase.Lobby)
            {
                RetryVotes(s);
            }

            float now = Time.unscaledTime;
            if (now >= _grabAt)
            {
                _grabAt = now + 0.2f;
                bool playing = s.Phase == RrPhase.Playing;
                int mine = MySlot;
                foreach (var d in Discs)
                {
                    if (d != null && d.Grab != null)
                    {
                        d.Grab.CanGrab = !playing || d.Slot == mine || d.HeldLocally;
                    }
                }
            }

            if (s.Phase == RrPhase.Playing && CourseReady)
            {
                int hole = s.Get(RrDiscGolfScore.IntHole);
                if (hole != _seenHole)
                {
                    _seenHole = hole;
                    if (hole < RrDiscGolfCourses.Holes)
                    {
                        NewHole(hole);
                    }
                }

                GiveStep();
                if (MySlot >= 0 && _live.Hole == hole && hole < RrDiscGolfCourses.Holes && now >= _statusAt)
                {
                    var seen = RrDiscGolfScore.LiveOf(s, MySlot);
                    if (seen.Pack() != _live.Pack())
                    {
                        SendStatus();
                    }
                    else
                    {
                        _statusAt = now + 0.5f;
                    }
                }
            }

            UpdateBeamAndTeeCard(s);
            if (now >= _sheetAt)
            {
                _sheetAt = now + 0.5f;
                string text = CardText(s);
                if (_lobbySheet != null && _lobbySheet.text != text)
                {
                    _lobbySheet.text = text;
                }

                if (_teeSheet != null && _teeSheet.text != text)
                {
                    _teeSheet.text = text;
                }
            }

            if (_toast != null && _toast.gameObject.activeSelf && now > _toastUntil)
            {
                _toast.gameObject.SetActive(false);
            }
        }

        private void UpdateBeamAndTeeCard(RrInstanceState s)
        {
            if (_level == null || !_level.Alive)
            {
                return;
            }

            int hole = s.Phase == RrPhase.Playing || s.Phase == RrPhase.Countdown ? Mathf.Min(s.Get(RrDiscGolfScore.IntHole), RrDiscGolfCourses.Holes - 1) : 0;
            bool show = s.Phase == RrPhase.Playing || s.Phase == RrPhase.Countdown;
            if (hole == _beamHole && _beam != null && _beam.activeSelf == show)
            {
                return;
            }

            _beamHole = hole;
            if (_beam != null)
            {
                _beam.SetActive(show);
                _beam.transform.localPosition = _level.Goals[hole] + Vector3.up * 21.3f;
            }

            if (_teeSheet != null)
            {
                _teeSheet.transform.localPosition = _level.TeeBoards[hole].position;
                _teeSheet.transform.localRotation = _level.TeeBoards[hole].rotation;
            }
        }

        private void LateUpdate()
        {
            if (!_entered || _hud == null)
            {
                return;
            }

            bool show = LocalPlaying;
            var cam = RrPlayer.HeadPosition;
            var headRot = RrPlayer.HeadRotation;
            if (show)
            {
                var fwd = headRot * Vector3.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 want = cam + fwd * 1.6f + Vector3.down * 0.55f;
                _hudPos = _hudPlaced ? Vector3.Lerp(_hudPos, want, Mathf.Clamp01(Time.deltaTime * 4f)) : want;
                _hudPlaced = true;
                _hud.transform.position = _hudPos;
                _hud.transform.rotation = Quaternion.LookRotation(_hudPos - cam, Vector3.up);
                string text = HudLine();
                if (_hud.text != text)
                {
                    _hud.text = text;
                }
            }
            else
            {
                _hudPlaced = false;
            }

            if (_hud.gameObject.activeSelf != show)
            {
                _hud.gameObject.SetActive(show);
            }

            if (_toast != null && _toast.gameObject.activeSelf)
            {
                var fwd = headRot * Vector3.forward;
                Vector3 at = cam + fwd * 4f + Vector3.up * 0.6f;
                _toast.transform.position = at;
                _toast.transform.rotation = Quaternion.LookRotation(at - cam, Vector3.up);
            }
        }

        private void Toast(string text, float seconds)
        {
            if (_toast == null)
            {
                return;
            }

            _toast.text = text;
            _toastUntil = Time.unscaledTime + seconds;
            _toast.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ text

        private string NameOf(int playerId)
        {
            if (playerId < 0)
            {
                return "-";
            }

            var p = RrNet.Presence(playerId);
            string name = p.Valid && !string.IsNullOrEmpty(p.Nick) ? p.Nick : playerId == RrNet.LocalId ? RrPlayer.Nick : "Player " + playerId;
            return string.IsNullOrEmpty(name) ? "Player " + playerId : name;
        }

        private string HoleLine()
        {
            int hole = Hole;
            if (hole >= RrDiscGolfCourses.Holes)
            {
                return "ROUND COMPLETE";
            }

            return "HOLE " + (hole + 1) + " OF 9 - PAR " + RrDiscGolfCourses.Par(CourseIndex, hole);
        }

        private string HudLine()
        {
            int hole = Hole;
            if (hole >= RrDiscGolfCourses.Holes)
            {
                return "";
            }

            var sb = new StringBuilder(96);
            sb.Append("HOLE ").Append(hole + 1).Append("  PAR ").Append(RrDiscGolfCourses.Par(CourseIndex, hole)).Append("  STROKES ").Append(_live.Hole == hole ? _live.Strokes - _live.Bonuses : 0);
            if (_live.Hole == hole && _live.Done)
            {
                sb.Append("\n<size=70%>DONE - WAITING FOR THE GROUP</size>");
            }
            else
            {
                int slot = MySlot;
                var disc = slot >= 0 ? Discs[slot] : null;
                if (disc != null && disc.Charge >= 0f)
                {
                    int bars = Mathf.RoundToInt(disc.Charge * 12f);
                    sb.Append("\n<size=70%>POWER ").Append(new string('|', bars)).Append(new string('.', 12 - bars)).Append("</size>");
                }
                else if (!RrPlayer.InVr)
                {
                    sb.Append("\n<size=60%>HOLD CLICK TO CHARGE, RELEASE TO THROW AT THE CROSSHAIR</size>");
                }
            }

            return sb.ToString();
        }

        private string CardText(RrInstanceState s)
        {
            int course = CourseIndex;
            var sb = new StringBuilder(1024);
            sb.Append("<mspace=0.6em>");
            int n = RrDiscGolfScore.GolferCount(s);
            switch (s.Phase)
            {
                case RrPhase.Playing:
                    sb.Append(RrDiscGolfCourses.Names[course].ToUpperInvariant()).Append(" - ").Append(HoleLine());
                    break;
                case RrPhase.Lobby:
                    sb.Append(n > 0 ? "LAST ROUND - PRESS START FOR A NEW ONE" : "DISC GOLF " + RrDiscGolfCourses.Names[course].ToUpperInvariant() + " - PRESS START");
                    break;
                default:
                    sb.Append(RrGameConsole.PhaseLine(this));
                    break;
            }

            sb.Append("\n\n");
            sb.Append(Pad("HOLE", 9));
            for (int h = 0; h < RrDiscGolfCourses.Holes; h++)
            {
                sb.Append((h + 1).ToString().PadLeft(3));
            }

            sb.Append("  TOT  +/-\n");
            sb.Append(Pad("PAR", 9));
            for (int h = 0; h < RrDiscGolfCourses.Holes; h++)
            {
                sb.Append(RrDiscGolfCourses.Par(course, h).ToString().PadLeft(3));
            }

            sb.Append(RrDiscGolfCourses.TotalPar(course).ToString().PadLeft(5)).Append('\n');
            int current = s.Phase == RrPhase.Playing ? s.Get(RrDiscGolfScore.IntHole) : -1;
            for (int i = 0; i < n; i++)
            {
                int id = RrDiscGolfScore.GolferId(s, i);
                string name = NameOf(id);
                string hex = ColorUtility.ToHtmlStringRGB(RrTeams.Color(i));
                sb.Append("<color=#").Append(hex).Append('>').Append(Pad(name.Length > 8 ? name.Substring(0, 8) : name, 9)).Append("</color>");
                var live = RrDiscGolfScore.LiveOf(s, i);
                for (int h = 0; h < RrDiscGolfCourses.Holes; h++)
                {
                    var v = RrDiscGolfScore.Hole(s, i, h);
                    string cell;
                    if (v.HasValue)
                    {
                        cell = v.Value.ToString();
                    }
                    else if (h == current && live.Hole == h)
                    {
                        cell = "<color=#FFD24A>" + (live.Strokes - live.Bonuses) + "</color>";
                        cell = new string(' ', Mathf.Max(0, 3 - (live.Strokes - live.Bonuses).ToString().Length)) + cell;
                        sb.Append(cell);
                        continue;
                    }
                    else
                    {
                        cell = h == current ? "_" : ".";
                    }

                    sb.Append(cell.PadLeft(3));
                }

                int played;
                int total = RrDiscGolfScore.Total(s, i, out played);
                sb.Append(total.ToString().PadLeft(5)).Append(RrDiscGolfScore.ToPar(s, i, course).PadLeft(5)).Append('\n');
            }

            if (n == 0)
            {
                sb.Append("\nFree practice: grab a disc and throw it at the basket.");
            }

            int winner = s.Get(RrDiscGolfScore.IntWinner) - 1;
            if (winner >= 0 && s.Phase != RrPhase.Playing && s.Phase != RrPhase.Countdown)
            {
                sb.Append("\nWINNER: ").Append(NameOf(winner).ToUpperInvariant());
            }

            sb.Append("</mspace>");
            return sb.ToString();
        }

        private static string Pad(string s, int n)
        {
            return s.Length >= n ? s : s + new string(' ', n - s.Length);
        }

        private string CardSummary()
        {
            var s = State;
            var sb = new StringBuilder();
            int n = RrDiscGolfScore.GolferCount(s);
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                int played;
                int total = RrDiscGolfScore.Total(s, i, out played);
                sb.Append(NameOf(RrDiscGolfScore.GolferId(s, i))).Append(' ').Append(total).Append(" (").Append(RrDiscGolfScore.ToPar(s, i, CourseIndex)).Append(", ").Append(played).Append(" holes)");
            }

            return sb.Length > 0 ? sb.ToString() : "no golfers";
        }
    }
}
