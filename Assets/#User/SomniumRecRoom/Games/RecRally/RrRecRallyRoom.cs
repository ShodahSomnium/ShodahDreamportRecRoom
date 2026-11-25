using System.Collections;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using UnityEngine.XR;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.RecRally
{
    /// <summary>
    /// Rec Room's Rec Rally (^RecRally, 2021-09-29; final rules rr_latest_rgames_story.md 10, data rr_party.md 3): an
    /// off-road buggy race on Chaparral in Pioneer Peak, 3 laps, nitro cans, podium for the top three. Basic form:
    ///
    /// - The holding area of Rec Room's level107 is the lobby (<see cref="RrRecRallyTrack"/>); the console stands on the
    ///   level's ScoreboardProjector. START (a race needs 4 since 2025-10-15; fewer can practise) - every member up to the
    ///   six grid slots gets a buggy (<see cref="RrRecRallyKart"/>) on Team1..6SpawnPoint and is seated in it
    ///   (<see cref="RrRecRallySeat"/>), locked through the 10 s countdown (SR announcer 3-2-1, as Rec Rally borrowed
    ///   Stunt Runner's race lights).
    /// - Driving: desktop W/S gas and brake/reverse, A/D steer, Left Shift handbrake drift, Space boost, T reset (Rec
    ///   Room's screen keys; Esc and R are the client's); VR right trigger gas, left trigger brake, left stick steer
    ///   (Rec Room's "joystick" mode), grip drift, A/X boost, B/Y reset. A buggy upside down or below the map for 3 s is
    ///   reset by itself to where it last drove well.
    /// - A lap is the level's twelve lap gates in order (either side of a split) and then the finish trigger; the first
    ///   pass of the finish line starts lap 1. Three laps finish. Positions: finished first by time, then lap, gate and
    ///   the distance to the next gate. Nitro cans: 3 = one 3 s boost, cans come back after 5 s (per player).
    /// - The race ends when every racer has finished, 60 s after the first finisher (GUESS) or after 10 minutes (GUESS);
    ///   the top three stand on the podium (PostGame spawn points), the others go back to the holding area.
    ///
    /// Net: instance custom int 0 = race number. Per-player int 0 = racing (bit 0) | grid slot (1-3) | gate (4-7) | lap
    /// (8-10) | finished (11) | race (16-23); int 1 = the finish time in ms. The buggy's pose is in the presence slot's
    /// vehicle ints 20-28 (<see cref="RrRecRallyKart.Publish"/>). Event 120 = a racer finished.
    /// </summary>
    public sealed class RrRecRallyRoom : RrGameRoom
    {
        public const string Music = "RecRally_MainGameplayLoop";
        public const string Art = "GiftBox_RecRally_Tex|8,276,380,268";
        public const string PickupPrefab = "(UGC_RecRally_Pickup_Sandbox)";

        public const int Laps = 3;
        public const int MinRacers = 4;
        public const int MaxRacers = 6;
        public const float RaceSeconds = 600f;
        public const float AfterFirstFinish = 60f;
        public const int IntRace = 0;
        public const byte EvFinish = 120;

        public static RrRecRallyRoom Current;

        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private RrRecRallyTrack _track;
        private RrGameConsole _console;
        private RrScoreboard _board;
        private RrQuestHud _hud;
        private RrRecRallySeat _seat;

        // the local racer
        private bool _racing;
        private int _raceId;
        private int _slot;
        private RrRecRallyKart _kart;
        private int _lap, _gate;
        private bool _finished;
        private float _finishTime = -1f;
        private int _place;
        private readonly Queue<Pose> _good = new Queue<Pose>();
        private float _goodAt, _badSince = -1f;
        private int _lastVo = -1;
        private int _gateLogs;
        private bool _vrSecondaryWas, _vrPrimaryWas;
        private string _results = "";

        // everyone else's buggies
        private readonly Dictionary<int, RrRecRallyKart> _remote = new Dictionary<int, RrRecRallyKart>();
        private readonly List<int> _drop = new List<int>();

        // nitro cans (per player, as Rec Room's TriggerCollectible respawns them for whoever takes them)
        private readonly List<Can> _cans = new List<Can>();

        // authority
        private float _firstDoneAt = -1f;

        private sealed class Can
        {
            public Vector3 At;
            public GameObject Visual;
            public float BackAt = -1f;
        }

        // ------------------------------------------------------------------ tuning

        protected override int TeamCount
        {
            get { return 0; }
        }

        protected override int MinPlayersToStart
        {
            get { return 1; }
        }

        protected override bool AutoStart
        {
            get { return false; }
        }

        protected override float CountdownSeconds
        {
            get { return 10f; }
        }

        protected override float RoundSeconds
        {
            get { return RaceSeconds; }
        }

        protected override float RoundOverSeconds
        {
            get { return 6f; }
        }

        protected override float MatchOverSeconds
        {
            get { return 12f; }
        }

        protected override string PlayMusic
        {
            get { return Music; }
        }

        protected override string LobbyMusic
        {
            get { return null; }
        }

        protected override string PhaseAnnouncement(RrPhase phase)
        {
            // The race's own sounds speak (SR 3-2-1, the GO horn).
            return null;
        }

        // ------------------------------------------------------------------ build

        public override IEnumerator Build()
        {
            BuildProgress = 0f;
            var portal = RrPortal.Prepare();
            while (portal.MoveNext())
            {
                BuildProgress = 0.02f;
                yield return portal.Current;
            }

            var t = transform;
            _track = new RrRecRallyTrack();
            BuildProgress = 0.05f;
            yield return _track.Build(t, _surfaces);
            if (this == null)
            {
                yield break;
            }

            BuildProgress = 0.85f;
            Vector3 centre = Vector3.zero;
            foreach (var p in _track.Lobby)
            {
                centre += p.position;
            }

            centre /= Mathf.Max(1, _track.Lobby.Count);
            float yaw = _track.Lobby.Count > 0 ? _track.Lobby[0].rotation.eulerAngles.y : 0f;
            var consolePose = _track.HasConsole ? _track.Console : new Pose(centre + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 4f, Quaternion.Euler(0f, yaw + 180f, 0f));
            _console = RrGameConsole.ForGame(this, t, consolePose, new RrGameConsoleSpec
            {
                TeamCount = 0,
                MinPlayers = MinRacers,
                MaxPlayers = MaxRacers,
                StartBelowMin = true,
                StartLabel = () => Phase == RrPhase.Lobby && Members().Count < MinRacers ? "PRACTICE" : RrGameConsole.PhaseStartLabel(this),
                Status = () => Phase == RrPhase.Lobby && Members().Count < MinRacers ? MinRacers + " PLAYERS RACE - START TO PRACTISE" : null,
                Subtitle = () => Phase == RrPhase.Lobby ? "CHAPARRAL - " + Laps + " LAPS" : PhaseLine(),
            });

            var boardAt = consolePose.position + consolePose.rotation * new Vector3(3.4f, 2.3f, 0f);
            _board = RrScoreboard.Create(t, boardAt, consolePose.rotation, "REC RALLY", 0, 3.4f);
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            RrPortal.Create(t, RrActivity.RecCenter, _track.Ground(centre - fwd * 3f + Vector3.up * 0.5f, 1f, 3f), yaw, "REC CENTER");

            _hud = RrQuestHud.Create(t);
            _seat = gameObject.AddComponent<RrRecRallySeat>();
            SpawnCans();
            BuildProgress = 1f;
        }

        private void SpawnCans()
        {
            var parent = _track.Root;
            foreach (var p in _track.Pickups)
            {
                var can = new Can { At = p };
                _cans.Add(can);
                RrSpawn.Spawn(PickupPrefab, parent, p, Quaternion.identity, inst =>
                {
                    if (inst == null)
                    {
                        can.Visual = RrBuild.Cylinder(parent, "Nitro Can", p, 0.25f, 0.6f, RrLook.Emissive(new Color(0.2f, 0.7f, 1f), 1.2f), false);
                    }
                    else
                    {
                        can.Visual = inst.gameObject;
                    }

                    if (can.BackAt > 0f)
                    {
                        can.Visual.SetActive(false);
                    }
                }, RrSpawnOptions.Visual());
            }
        }

        private string PhaseLine()
        {
            switch (Phase)
            {
                case RrPhase.Countdown:
                    return "GET READY";
                case RrPhase.Playing:
                    return "RACE ON";
                default:
                    return "RESULTS";
            }
        }

        // ------------------------------------------------------------------ arrival / leaving

        public override Pose Spawn(int index)
        {
            if (_track != null && _track.Lobby.Count > 0)
            {
                int n = _track.Lobby.Count;
                return _track.Lobby[((index % n) + n) % n];
            }

            return new Pose(new Vector3(-99f, 1.4f, 29f), Quaternion.Euler(0f, 90f, 0f));
        }

        public override Pose RescuePose(int index)
        {
            if (_racing && _kart != null)
            {
                return new Pose(_kart.transform.position, _kart.transform.rotation);
            }

            return base.RescuePose(index);
        }

        public override void OnLocalEnter()
        {
            Current = this;
            base.OnLocalEnter();
            RrNet.On(EvFinish, OnFinishEvent);
            _racing = false;
            ClearInts();
            RrLog.Info("RecRally", "entered: phase " + Phase + ", authority " + IsAuthority + ", members " + Members().Count + (Phase != RrPhase.Lobby ? " (a race is on: watching from the holding area)" : ""));
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            RrNet.Off(EvFinish, OnFinishEvent);
            LeaveRace(false);
            ClearInts();
            foreach (var k in _remote.Values)
            {
                if (k != null)
                {
                    Destroy(k.gameObject);
                }
            }

            _remote.Clear();
            if (Current == this)
            {
                Current = null;
            }
        }

        // ------------------------------------------------------------------ phases

        protected override void AuthorityMatchSetup()
        {
            State.Set(IntRace, (State.Get(IntRace) + 1) & 0xFFFF);
            _firstDoneAt = -1f;
            RrLog.Info("RecRally", "race " + State.Get(IntRace) + " set up with " + Members().Count + " in the room" + (Members().Count < MinRacers ? " (practice: fewer than " + MinRacers + ")" : ""));
        }

        protected override void AuthorityRoundStart(int round)
        {
            _firstDoneAt = -1f;
        }

        protected override void AuthorityPlaying()
        {
            int race = State.Get(IntRace) & 0xFF;
            int racers = 0, done = 0;
            foreach (int id in Members())
            {
                int v = RrNet.PlayerInt(id, 0);
                if ((v & 1) == 0 || ((v >> 16) & 0xFF) != race)
                {
                    continue;
                }

                racers++;
                if ((v & (1 << 11)) != 0)
                {
                    done++;
                }
            }

            float now = Time.unscaledTime;
            float elapsed = RoundSeconds - State.TimeRemaining;
            if (done > 0 && _firstDoneAt < 0f)
            {
                _firstDoneAt = now;
            }

            bool allDone = racers > 0 && done == racers;
            bool late = _firstDoneAt > 0f && now - _firstDoneAt > AfterFirstFinish;
            bool empty = racers == 0 && elapsed > 15f;
            if (allDone || late || empty)
            {
                RrLog.Info("RecRally", "race over: " + done + " of " + racers + " finished after " + elapsed.ToString("0") + " s" + (late ? " (60 s after the first finisher)" : ""));
                EndRound(-1);
            }
        }

        protected override int AuthorityRoundWinner()
        {
            return -1;
        }

        protected override void OnPhase(RrPhase old, RrPhase now)
        {
            switch (now)
            {
                case RrPhase.Countdown:
                    JoinRace();
                    _lastVo = -1;
                    break;
                case RrPhase.Playing:
                    if (_racing && _kart != null)
                    {
                        _kart.SetLocked(false);
                        RrSound.PlayUi("SR_CountdownGO", 0.9f);
                        RrSound.PlayUi("SR_Announcer_Go01", 1f);
                        if (_hud != null)
                        {
                            _hud.Title("GO!", "", 1.5f);
                        }
                    }

                    break;
                case RrPhase.RoundOver:
                    if (_racing)
                    {
                        RaceOver();
                    }

                    break;
                case RrPhase.Lobby:
                    if (_racing)
                    {
                        LeaveRace(true);
                    }

                    break;
            }

            RrLog.Info("RecRally", "phase " + old + " -> " + now + " (racing " + _racing + ", lap " + _lap + ", gate " + _gate + ")");
        }

        private void JoinRace()
        {
            if (_racing || _track == null)
            {
                return;
            }

            var members = Members();
            members.Sort();
            int rank = Mathf.Max(0, members.IndexOf(RrNet.LocalId));
            if (rank >= Mathf.Min(MaxRacers, _track.Grid.Count))
            {
                Toast("THE GRID IS FULL - WATCH THIS ONE");
                return;
            }

            _racing = true;
            _raceId = State.Get(IntRace) & 0xFF;
            _slot = rank;
            _lap = 0;
            _gate = 0;
            _finished = false;
            _finishTime = -1f;
            _place = 0;
            _good.Clear();
            _badSince = -1f;
            var grid = _track.Grid[rank];
            var pose = new Pose(grid.position + Vector3.up * 0.35f, grid.rotation);
            _kart = RrRecRallyKart.Create(this, transform, pose, RrNet.LocalId, _slot, true);
            _kart.SetLocked(true);
            _seat.Sit(_kart);
            WriteProgress();
            RrNet.SetPlayerInt(1, 0);
            foreach (var c in _cans)
            {
                c.BackAt = -1f;
                if (c.Visual != null)
                {
                    c.Visual.SetActive(true);
                }
            }

            if (_hud != null)
            {
                _hud.Title("REC RALLY - " + Laps + " LAPS", RrPlayer.InVr ? "Right trigger gas - left trigger brake - left stick steer - grip drift - A/X boost - B/Y reset"
                    : "W/S gas and brake - A/D steer - Shift drift - Space boost - T reset", 9f);
            }

            RrLog.Info("RecRally", "on the grid: slot " + (_slot + 1) + " of " + Mathf.Min(members.Count, MaxRacers) + " (race " + _raceId + ")");
        }

        private void RaceOver()
        {
            int place = Placement(RrNet.LocalId);
            string line = _finished ? "You finished " + Ordinal(place) + " in " + Clock(_finishTime) : "Out of time - " + Ordinal(place) + " place";
            if (_hud != null)
            {
                _hud.Title("RACE OVER", line, 6f);
            }

            _results = ResultsText();
            Pose to = place >= 1 && place <= 3 && _track.Podium.Count == 3 ? _track.Podium[place - 1] : Spawn(Mathf.Abs(RrNet.LocalId));
            LeaveRace(false);
            _seat.Stand(transform.TransformPoint(to.position + Vector3.up * 0.05f), (transform.rotation * to.rotation).eulerAngles.y);
            RrSound.PlayUi(place == 1 ? "SR_Audience_Cheer03" : "SR_Audience_Cheer02", 0.6f);
            RrLog.Info("RecRally", "race over for the local player: " + line);
        }

        /// <summary>Out of the race: the seat (when <paramref name="move"/>, back to the holding area), the buggy, the ints.</summary>
        private void LeaveRace(bool move)
        {
            if (_seat != null && _seat.Seated)
            {
                if (move)
                {
                    var p = Spawn(Mathf.Abs(RrNet.LocalId));
                    _seat.Stand(transform.TransformPoint(p.position), (transform.rotation * p.rotation).eulerAngles.y);
                }
                else
                {
                    _seat.Stand(null, 0f, false);
                }
            }

            if (_kart != null)
            {
                Destroy(_kart.gameObject);
                _kart = null;
            }

            RrRecRallyKart.Unpublish();
            _racing = false;
            RrNet.SetPlayerInt(0, 0);
        }

        // ------------------------------------------------------------------ every frame

        protected override void OnTick(RrInstanceState s)
        {
            float now = Time.unscaledTime;
            if (_racing && s.Phase == RrPhase.Countdown)
            {
                int whole = Mathf.CeilToInt(s.TimeRemaining);
                if (whole != _lastVo && whole >= 1 && whole <= 3)
                {
                    _lastVo = whole;
                    RrSound.PlayUi(whole == 3 ? "SR_Announcer_StartThree01" : whole == 2 ? "SR_Announcer_StartTwo01" : "SR_Announcer_StartOne01", 1f);
                }
            }

            if (_racing && _kart != null)
            {
                Drive(s);
                if (s.Phase == RrPhase.Playing)
                {
                    Rules(s);
                }
            }

            Cans(now);
            Remotes();
            Hud(s);
            if (_board != null)
            {
                _board.Custom = s.Phase == RrPhase.Playing || s.Phase == RrPhase.Countdown ? LiveText() : _results;
            }
        }

        private void Drive(RrInstanceState s)
        {
            float throttle = 0f, steer = 0f;
            bool drift = false, boost = false, reset = false;
            if (RrPlayer.InVr)
            {
                throttle = RrXr.Trigger(RrHandSide.Right) - RrXr.Trigger(RrHandSide.Left);
                Vector2 stick;
                var left = RrXr.Controller(RrHandSide.Left);
                if (left.isValid && left.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick))
                {
                    steer = Mathf.Abs(stick.x) > 0.15f ? stick.x : 0f;
                }

                drift = RrXr.Grip(RrHandSide.Left) > 0.7f || RrXr.Grip(RrHandSide.Right) > 0.7f;
                bool primary = RrXr.Primary(RrHandSide.Left) || RrXr.Primary(RrHandSide.Right);
                boost = primary && !_vrPrimaryWas;
                _vrPrimaryWas = primary;
                bool secondary = Secondary(RrHandSide.Left) || Secondary(RrHandSide.Right);
                reset = secondary && !_vrSecondaryWas;
                _vrSecondaryWas = secondary;
            }
            else if (!RrInput.Blocked)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                {
                    throttle += 1f;
                }

                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                {
                    throttle -= 1f;
                }

                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                {
                    steer -= 1f;
                }

                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                {
                    steer += 1f;
                }

                drift = Input.GetKey(KeyCode.LeftShift);
                boost = Input.GetKeyDown(KeyCode.Space);
                reset = Input.GetKeyDown(KeyCode.T);
            }

            bool live = s.Phase == RrPhase.Playing;
            _kart.Throttle = live ? Mathf.Clamp(throttle, -1f, 1f) : 0f;
            _kart.Steer = Mathf.Clamp(steer, -1f, 1f);
            _kart.Drift = live && drift;
            if (live && boost)
            {
                if (_kart.TryBoost())
                {
                    Toast("BOOST!");
                }
                else if (_kart.Cans < RrRecRallyKart.CansPerBoost)
                {
                    RrSound.PlayUi("Locomotion_Boost_Misfire01", 0.5f);
                }
            }

            if (live && reset)
            {
                ResetKart("asked");
            }
        }

        private static bool Secondary(RrHandSide side)
        {
            bool v;
            var d = RrXr.Controller(side);
            return d.isValid && d.TryGetFeatureValue(CommonUsages.secondaryButton, out v) && v;
        }

        /// <summary>Laps, gates, the finish, resets - for the local buggy.</summary>
        private void Rules(RrInstanceState s)
        {
            float now = Time.unscaledTime;
            var kt = _kart.transform;
            Vector3 pos = transform.InverseTransformPoint(kt.position);

            // Where it last drove well (on its wheels, moving): the reset spot, about three seconds back.
            if (_kart.Grounded && kt.up.y > 0.8f && _kart.Speed > 3f && now >= _goodAt)
            {
                _goodAt = now + 1f;
                _good.Enqueue(new Pose(pos, Quaternion.Euler(0f, kt.eulerAngles.y - transform.eulerAngles.y, 0f)));
                while (_good.Count > 3)
                {
                    _good.Dequeue();
                }
            }

            bool bad = kt.up.y < 0.3f || pos.y < _track.KillY || (_kart.Speed < 0.5f && !_kart.Grounded);
            if (bad)
            {
                if (_badSince < 0f)
                {
                    _badSince = now;
                }
                else if (now - _badSince > 3f || pos.y < _track.KillY)
                {
                    ResetKart(pos.y < _track.KillY ? "fell off the map" : "stuck");
                }
            }
            else
            {
                _badSince = -1f;
            }

            if (_finished)
            {
                return;
            }

            if (_lap == 0)
            {
                if (_track.Finish.Contains(pos))
                {
                    _lap = 1;
                    _gate = 0;
                    WriteProgress();
                }

                return;
            }

            // The next gate - or the one after it: one gate may be missed (the level's gate numbers are read as the
            // driving order; this keeps a lap countable should two of them be the other way round).
            bool passed = false;
            for (int next = _gate + 1; next <= Mathf.Min(_gate + 2, RrRecRallyTrack.Gates) && !passed; next++)
            {
                foreach (var box in _track.GateBoxes[next])
                {
                    if (box.Contains(pos))
                    {
                        if (_lap == 1 && _gateLogs++ < 16)
                        {
                            RrLog.Info("RecRally", "gate " + next + " passed at " + Clock(RoundSeconds - s.TimeRemaining) + (next > _gate + 1 ? " (gate " + (_gate + 1) + " missed)" : ""));
                        }

                        _gate = next;
                        WriteProgress();
                        passed = true;
                        break;
                    }
                }
            }

            if (!passed && _gate >= RrRecRallyTrack.Gates - 1 && _track.Finish.Contains(pos))
            {
                _lap++;
                _gate = 0;
                if (_lap > Laps)
                {
                    Finished(s);
                }
                else
                {
                    RrSound.PlayUi(_lap == Laps ? "SR_CountdownGO" : "SR_Collectible_Collect01", 0.7f);
                    if (_hud != null)
                    {
                        _hud.Title(_lap == Laps ? "FINAL LAP!" : "LAP " + _lap + "/" + Laps, "", 2f);
                    }
                }

                WriteProgress();
            }
        }

        private void Finished(RrInstanceState s)
        {
            _finished = true;
            _lap = Laps;
            _gate = RrRecRallyTrack.Gates;
            _finishTime = Mathf.Clamp(RoundSeconds - s.TimeRemaining, 0f, RoundSeconds);
            int ms = Mathf.RoundToInt(_finishTime * 1000f);
            RrNet.SetPlayerInt(1, ms);
            WriteProgress();
            int place = Placement(RrNet.LocalId);
            RrNet.Send(EvFinish, new[] { (byte)place, (byte)(ms & 0xFF), (byte)((ms >> 8) & 0xFF), (byte)((ms >> 16) & 0xFF) });
            RrSound.PlayUi("SR_ButtonPressed_01", 0.8f);
            RrSound.PlayUi(place == 1 ? "SR_Audience_Cheer03" : "SR_Audience_Cheer02", 0.65f);
            if (_hud != null)
            {
                _hud.Title("FINISHED " + Ordinal(place) + "!", Clock(_finishTime), 5f);
            }

            RrLog.Info("RecRally", "finished " + Ordinal(place) + " in " + Clock(_finishTime));
        }

        private void ResetKart(string why)
        {
            Pose to;
            if (_good.Count > 0)
            {
                to = _good.Peek();
            }
            else
            {
                var g = _track.Grid[Mathf.Clamp(_slot, 0, _track.Grid.Count - 1)];
                to = new Pose(g.position, g.rotation);
            }

            _kart.Place(new Pose(to.position + Vector3.up * 1.0f, to.rotation));
            _badSince = -1f;
            Toast("RESET");
            RrLog.Info("RecRally", "buggy reset (" + why + ") to " + to.position.ToString("F0"));
        }

        private void Cans(float now)
        {
            Vector3 at = _kart != null ? transform.InverseTransformPoint(_kart.transform.position + _kart.transform.up * 0.8f) : Vector3.zero;
            foreach (var c in _cans)
            {
                if (c.BackAt > 0f)
                {
                    if (now >= c.BackAt)
                    {
                        c.BackAt = -1f;
                        if (c.Visual != null)
                        {
                            c.Visual.SetActive(true);
                        }
                    }

                    continue;
                }

                if (c.Visual != null)
                {
                    c.Visual.transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                }

                if (_kart == null || Phase != RrPhase.Playing || (c.At - at).sqrMagnitude > 2.6f * 2.6f)
                {
                    continue;
                }

                c.BackAt = now + 5f;
                if (c.Visual != null)
                {
                    c.Visual.SetActive(false);
                }

                _kart.AddCan();
                RrSound.Play("sfx_rec_rally_boostpickup_collect_04", _kart.transform.position, 0.8f, 1f + Random.Range(-0.08f, 0.08f), 20f);
                if (_kart.Cans >= RrRecRallyKart.CansPerBoost)
                {
                    Toast(RrPlayer.InVr ? "BOOST READY - A / X" : "BOOST READY - SPACE");
                }
            }
        }

        /// <summary>Everyone else's buggies from their presence records: made, followed, removed.</summary>
        private void Remotes()
        {
            var members = Members();
            foreach (int id in members)
            {
                if (id == RrNet.LocalId)
                {
                    continue;
                }

                var r = RrNet.PresenceRecord(id);
                int slot;
                if (!RrRecRallyKart.InBuggy(r, out slot))
                {
                    continue;
                }

                RrRecRallyKart k;
                if (!_remote.TryGetValue(id, out k) || k == null)
                {
                    var p = _track != null && slot < _track.Grid.Count ? _track.Grid[slot] : new Pose(Vector3.zero, Quaternion.identity);
                    k = RrRecRallyKart.Create(this, transform, new Pose(p.position + Vector3.up * 0.35f, p.rotation), id, slot, false);
                    _remote[id] = k;
                    RrLog.Info("RecRally", "buggy of player " + id + " (slot " + (slot + 1) + ") appears");
                }

                k.Hear(r);
            }

            _drop.Clear();
            foreach (var kv in _remote)
            {
                int slot;
                if (kv.Value == null || !members.Contains(kv.Key) || !RrRecRallyKart.InBuggy(RrNet.PresenceRecord(kv.Key), out slot))
                {
                    _drop.Add(kv.Key);
                }
            }

            foreach (int id in _drop)
            {
                RrRecRallyKart k;
                if (_remote.TryGetValue(id, out k) && k != null)
                {
                    Destroy(k.gameObject);
                }

                _remote.Remove(id);
            }
        }

        // ------------------------------------------------------------------ positions

        private struct Racer
        {
            public int Id;
            public float Score;
            public bool Finished;
            public int Ms;
            public int Lap;
        }

        private List<Racer> Racers()
        {
            var list = new List<Racer>();
            int race = State.Get(IntRace) & 0xFF;
            foreach (int id in Members())
            {
                int v = RrNet.PlayerInt(id, 0);
                bool local = id == RrNet.LocalId;
                if (local && _racing)
                {
                    v = Progress();
                }

                if ((v & 1) == 0 || ((v >> 16) & 0xFF) != race)
                {
                    continue;
                }

                int gate = (v >> 4) & 0xF;
                int lap = (v >> 8) & 0x7;
                bool done = (v & (1 << 11)) != 0;
                int ms = local && _finished ? Mathf.RoundToInt(_finishTime * 1000f) : RrNet.PlayerInt(id, 1);
                float dist = 999f;
                Transform kt = local ? (_kart != null ? _kart.transform : null) : (_remote.ContainsKey(id) && _remote[id] != null ? _remote[id].transform : null);
                if (kt != null && _track != null)
                {
                    Vector3 pos = transform.InverseTransformPoint(kt.position);
                    Vector3 next = gate < RrRecRallyTrack.Gates && _track.GateBoxes[gate + 1].Count > 0 ? _track.GateBoxes[gate + 1][0].Centre : _track.Finish.Centre;
                    dist = Mathf.Min(999f, Vector3.Distance(pos, next));
                }

                float score = done ? 1e8f - ms / 10f : lap * 100000f + gate * 1000f - dist;
                list.Add(new Racer { Id = id, Score = score, Finished = done, Ms = ms, Lap = lap });
            }

            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            return list;
        }

        private int Placement(int id)
        {
            var list = Racers();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == id)
                {
                    return i + 1;
                }
            }

            return list.Count + 1;
        }

        private string LiveText()
        {
            var sb = new StringBuilder();
            var list = Racers();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append(i + 1).Append(". ").Append(NameOf(r.Id)).Append("  ");
                sb.Append(r.Finished ? Clock(r.Ms / 1000f) : r.Lap == 0 ? "START" : "LAP " + Mathf.Min(r.Lap, Laps) + "/" + Laps).Append('\n');
            }

            return sb.ToString();
        }

        private string ResultsText()
        {
            var sb = new StringBuilder("LAST RACE\n");
            var list = Racers();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append(i + 1).Append(". ").Append(NameOf(r.Id)).Append("  ").Append(r.Finished ? Clock(r.Ms / 1000f) : "DNF").Append('\n');
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ HUD, net, helpers

        private void Hud(RrInstanceState s)
        {
            if (_hud == null)
            {
                return;
            }

            if (!_racing || _kart == null)
            {
                _hud.SetScore("");
                _hud.SetHealth("", Color.white);
                return;
            }

            float elapsed = s.Phase == RrPhase.Playing ? RoundSeconds - s.TimeRemaining : 0f;
            var list = Racers();
            int place = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == RrNet.LocalId)
                {
                    place = i + 1;
                }
            }

            _place = place;
            string lap = _finished ? "FINISHED" : _lap == 0 ? "LAP 1/" + Laps : "LAP " + Mathf.Min(_lap, Laps) + "/" + Laps;
            _hud.SetScore(lap + "   " + Ordinal(place) + " / " + list.Count + "   " + Clock(_finished ? _finishTime : elapsed));
            string nitro = "NITRO " + new string('|', _kart.Cans) + new string('.', RrRecRallyKart.CansPerBoost - _kart.Cans);
            _hud.SetHealth(_kart.Boosting ? "BOOST!" : _kart.Cans >= RrRecRallyKart.CansPerBoost ? nitro + "  BOOST READY" : nitro,
                _kart.Boosting || _kart.Cans >= RrRecRallyKart.CansPerBoost ? new Color(0.4f, 0.85f, 1f) : Color.white);
        }

        private int Progress()
        {
            return (_racing ? 1 : 0) | ((_slot & 7) << 1) | ((_gate & 0xF) << 4) | ((Mathf.Clamp(_lap, 0, 7) & 7) << 8) | (_finished ? 1 << 11 : 0) | ((_raceId & 0xFF) << 16);
        }

        private void WriteProgress()
        {
            RrNet.SetPlayerInt(0, _racing ? Progress() : 0);
        }

        private static void ClearInts()
        {
            RrNet.SetPlayerInt(0, 0);
            RrNet.SetPlayerInt(1, 0);
            RrRecRallyKart.Unpublish();
        }

        private void OnFinishEvent(int sender, byte[] body)
        {
            if (body == null || body.Length < 4 || sender == RrNet.LocalId)
            {
                return;
            }

            int ms = body[1] | (body[2] << 8) | (body[3] << 16);
            Toast(NameOf(sender) + " FINISHED " + Ordinal(body[0]) + " - " + Clock(ms / 1000f));
        }

        private void Toast(string text)
        {
            if (_hud != null)
            {
                _hud.Toast(text, 2.5f);
            }
        }

        private static string NameOf(int id)
        {
            var p = RrNet.Presence(id);
            return !string.IsNullOrEmpty(p.Nick) ? p.Nick : "Player " + id;
        }

        public static string Ordinal(int n)
        {
            if (n <= 0)
            {
                return "-";
            }

            int t = n % 100;
            string s = t >= 11 && t <= 13 ? "th" : n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th";
            return n + s;
        }

        public static string Clock(float seconds)
        {
            if (seconds < 0f)
            {
                return "--:--";
            }

            int cs = Mathf.RoundToInt(seconds * 100f);
            return string.Format("{0}:{1:00}.{2:00}", cs / 6000, (cs / 100) % 60, cs % 100);
        }
    }
}
