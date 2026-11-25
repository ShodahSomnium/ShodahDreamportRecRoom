using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.StuntRunner
{
    /// <summary>
    /// Rec Room's Stunt Runner (^StuntRunner, 2019-09-05; final rules rr_latest_rgames_story.md 11, data rr_party.md 2):
    /// up to 4 walking runners race the six courses of a TV obstacle show, lowest total time wins. Basic form:
    ///
    /// - The lobby is Rec Room's level92 (StuntRunner_Lobby), the courses levels 93-98 (<see cref="RrStuntRunnerCourses"/>,
    ///   <see cref="RrStuntRunnerLevel"/>): the lobby and course 1 are built on arrival, courses 2-6 one after another in
    ///   the background, so a run starts as soon as course 1 stands.
    /// - START on the console (StuntRunner_Config: 1-4 players, every runner is his own team, GameStartDelay 10 s): every
    ///   member becomes a runner and goes to a start lane of course 1, held in the gate (movement 0) while the
    ///   announcer counts 3-2-1; GO frees them (SR_Announcer_Go01, SR_GateDown_02).
    /// - A course: checkpoints (respawn there after a pit, pool or the floor), KO Colas, bounce pads, nets, the finish
    ///   button (<see cref="RrStuntRunnerRunner"/>). Each runner writes his own time; the instance authority ends the
    ///   course when every runner has finished or after 180 s (defaultMaxCourseTime) - an unfinished course counts 180 s.
    ///   "Course Complete! Advancing to next course..." (SR_CourseClear01), then the next course's start lanes.
    /// - After the last course: the total, the rank (S+ under 4:45; the others GUESSES), personal bests saved on this PC
    ///   (Application.persistentDataPath/SomniumRecRoom/stuntrunner_best.txt) with "NEW PERSONAL BEST!" (NewBestOoo01),
    ///   and everyone back in the lobby, where a board lists the bests and the last run.
    ///
    /// Net: instance custom ints 0 = run number, 1 = courses in this run; per-player ints (RrNet.SetPlayerInt) 0 = course+1
    /// (bits 0-3) | checkpoints (4-11) | finished (12) | run (16-23), 1..6 = each course's time in ms. Event 110 = a
    /// runner finished (others hear the crowd and see a toast).
    /// </summary>
    public sealed class RrStuntRunnerRoom : RrGameRoom
    {
        public const string LobbyMusicClip = "SR_PostRun01";
        public const string Art = "StuntRunner_Decal_Tex|70,234,880,585";
        public const string PlayModeOwner = "stuntrunner";

        public const int IntRun = 0;
        public const int IntCourses = 1;
        public const byte EvFinish = 110;

        public static RrStuntRunnerRoom Current;

        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private RrStuntRunnerLevel _lobby;
        private readonly RrStuntRunnerLevel[] _courses = new RrStuntRunnerLevel[RrStuntRunnerCourses.Count];
        private readonly RrStuntRunnerRunner[] _runners = new RrStuntRunnerRunner[RrStuntRunnerCourses.Count];
        private RrGameConsole _console;
        private RrScoreboard _lobbyBoard;
        private readonly List<RrScoreboard> _courseBoards = new List<RrScoreboard>();
        private RrQuestHud _hud;
        private bool _restStarted;

        // the local runner
        private bool _running;
        private int _runId;
        private int _course = -1;
        private int _pendingCourse = -1;
        private readonly float[] _times = new float[RrStuntRunnerCourses.Count];
        private float _postRunAt = -1f;
        private string _envScene;
        private float _boardsAt;
        private string _lastRun = "";
        private int _lastVo = -1;

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

        /// <summary>GameStartDelay 10 s before course 1; 5 s between courses.</summary>
        protected override float CountdownSeconds
        {
            get { return State.Round == 0 ? 10f : 5f; }
        }

        protected override float RoundSeconds
        {
            get { return RrStuntRunnerCourses.MaxCourseSeconds; }
        }

        protected override int Rounds
        {
            get { return Mathf.Clamp(State.Get(IntCourses), 1, RrStuntRunnerCourses.Count); }
        }

        protected override float RoundOverSeconds
        {
            get { return 5f; }
        }

        /// <summary>EndGameResultsDuration 15 s.</summary>
        protected override float MatchOverSeconds
        {
            get { return 15f; }
        }

        protected override string LobbyMusic
        {
            get { return LobbyMusicClip; }
        }

        protected override string PlayMusic
        {
            get
            {
                int r = State.Round;
                return r >= Rounds - 1 && Rounds > 1 ? "SR_FinalLap01_NoSilence" : r % 2 == 0 ? "SR_GameOn01" : "SR_DadRunner";
            }
        }

        protected override string PhaseAnnouncement(RrPhase phase)
        {
            // Stunt Runner's own announcer speaks (OnPhase / OnTick).
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
            _lobby = new RrStuntRunnerLevel(-1);
            BuildProgress = 0.05f;
            yield return _lobby.Build(t, _surfaces);
            if (this == null)
            {
                yield break;
            }

            BuildProgress = 0.4f;
            BuildLobbyFurniture();
            _hud = RrQuestHud.Create(t);
            yield return null;

            BuildProgress = 0.45f;
            yield return BuildCourse(0);
            BuildProgress = 1f;
            RrLog.Info("StuntRunner", "lobby and " + RrStuntRunnerCourses.Title(0) + " built; courses 2-" + RrStuntRunnerCourses.Count + " follow in the background (" + CoursesWithData() + " carry data)");
        }

        private IEnumerator BuildCourse(int i)
        {
            if (_courses[i] != null || this == null)
            {
                yield break;
            }

            var level = new RrStuntRunnerLevel(i);
            _courses[i] = level;
            yield return level.Build(transform, _surfaces);
            if (this == null || level.Root == null)
            {
                yield break;
            }

            var go = new GameObject("Runner " + (i + 1));
            go.transform.SetParent(level.Root, false);
            var runner = go.AddComponent<RrStuntRunnerRunner>();
            runner.Room = this;
            runner.Level = level;
            runner.SpawnPickups(level.Root);
            _runners[i] = runner;

            if (level.HasButton)
            {
                var button = new GameObject("Finish Button");
                button.transform.SetParent(level.Root, false);
                button.transform.position = transform.TransformPoint(level.Button);
                var box = button.AddComponent<BoxCollider>();
                box.size = new Vector3(0.8f, 0.6f, 0.8f);
                var press = RrPressable.Make(button, () => runner.PressFinish("button"));
                press.Volume = 0f;
            }

            if (level.HasBoard)
            {
                var b = level.Board;
                var board = RrScoreboard.Create(level.Root, b.position + Vector3.up * 2.4f, b.rotation, RrStuntRunnerCourses.Title(i).ToUpperInvariant(), 0, 3.2f);
                board.transform.SetParent(level.Root, true);
                _courseBoards.Add(board);
            }
        }

        private IEnumerator BuildRest()
        {
            for (int i = 1; i < RrStuntRunnerCourses.Count; i++)
            {
                if (this == null)
                {
                    yield break;
                }

                yield return BuildCourse(i);
                yield return null;
            }

            RrLog.Info("StuntRunner", "all " + RrStuntRunnerCourses.Count + " courses built");
        }

        private int CoursesWithData()
        {
            int n = 0;
            while (n < RrStuntRunnerCourses.Count && RrSceneBank.Has(RrStuntRunnerCourses.Scenes[n]))
            {
                n++;
            }

            return n;
        }

        private bool CourseReady(int i)
        {
            return i >= 0 && i < _courses.Length && _courses[i] != null && _courses[i].Done && _runners[i] != null;
        }

        /// <summary>The console, the board and the exit in front of the lobby's spawn points (level92 has no projector marker).</summary>
        private void BuildLobbyFurniture()
        {
            var t = transform;
            Vector3 centre = Vector3.zero;
            foreach (var p in _lobby.Lobby)
            {
                centre += p.position;
            }

            centre /= Mathf.Max(1, _lobby.Lobby.Count);
            float yaw = _lobby.Lobby.Count > 0 ? _lobby.Lobby[0].rotation.eulerAngles.y : 0f;
            var face = Quaternion.Euler(0f, yaw, 0f);
            var fwd = face * Vector3.forward;
            var right = face * Vector3.right;

            var candidates = new List<Vector3>();
            foreach (float d in new[] { 4f, 5f, 3.5f, 6f })
            {
                foreach (float s in new[] { 0f, -2f, 2f })
                {
                    candidates.Add(_lobby.Floor(centre + fwd * d + right * s + Vector3.up * 0.5f, 1f, 3f));
                }
            }

            Pose consolePose;
            string note;
            if (!RrGameConsole.FindSpot(t, centre, candidates, null, out consolePose, out note))
            {
                consolePose = new Pose(candidates[0], Quaternion.LookRotation(-fwd, Vector3.up));
                RrLog.Info("StuntRunner", "no clear console spot in the lobby (" + note + "): 4 m in front of the spawns");
            }

            _console = RrGameConsole.ForGame(this, t, consolePose, new RrGameConsoleSpec
            {
                TeamCount = 0,
                MinPlayers = 1,
                MaxPlayers = 4,
                CanStart = () => Phase == RrPhase.Lobby && CourseReady(0),
                Status = () => !CourseReady(0) ? "LOADING THE COURSES..." : null,
                Subtitle = () => Phase == RrPhase.Lobby ? CoursesWithData() + " COURSES - LOWEST TOTAL TIME WINS" : PhaseLine(),
            });

            // The board of personal bests beside the console, facing the spawns.
            var boardAt = consolePose.position + consolePose.rotation * new Vector3(-3.2f, 2.2f, 0f);
            _lobbyBoard = RrScoreboard.Create(t, boardAt, consolePose.rotation, "STUNT RUNNER", 0, 3.4f);

            // The way out behind the spawn points, facing into the lobby.
            var exit = _lobby.Floor(centre - fwd * 3f + Vector3.up * 0.5f, 1f, 3f);
            RrPortal.Create(t, RrActivity.RecCenter, exit, yaw, "REC CENTER");
        }

        private string PhaseLine()
        {
            switch (Phase)
            {
                case RrPhase.Countdown:
                case RrPhase.Playing:
                case RrPhase.RoundOver:
                    return RrStuntRunnerCourses.Title(State.Round).ToUpperInvariant();
                case RrPhase.MatchOver:
                    return "RUN COMPLETE";
            }

            return "";
        }

        // ------------------------------------------------------------------ arrival / leaving

        public override Pose Spawn(int index)
        {
            if (_lobby != null && _lobby.Lobby.Count > 0)
            {
                int n = _lobby.Lobby.Count;
                return _lobby.Lobby[((index % n) + n) % n];
            }

            return new Pose(new Vector3(160f, 0f, 115f), Quaternion.identity);
        }

        public override Pose RescuePose(int index)
        {
            var r = ActiveRunner;
            if (r != null && r.Live)
            {
                return new Pose(transform.TransformPoint(r.Respawn.position), transform.rotation * r.Respawn.rotation);
            }

            return base.RescuePose(index);
        }

        private RrStuntRunnerRunner ActiveRunner
        {
            get { return _course >= 0 && _course < _runners.Length ? _runners[_course] : null; }
        }

        public override void OnLocalEnter()
        {
            Current = this;
            base.OnLocalEnter();
            RrNet.On(EvFinish, OnFinishEvent);
            _running = false;
            _course = -1;
            _envScene = null;
            ClearInts();
            LoadBests();
            NoBumping(true);
            if (!_restStarted)
            {
                _restStarted = true;
                StartCoroutine(BuildRest());
            }

            RrLog.Info("StuntRunner", "entered: phase " + Phase + ", authority " + IsAuthority + ", members " + Members().Count + (Phase != RrPhase.Lobby ? " (a run is on: watching from the lobby)" : ""));
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            RrNet.Off(EvFinish, OnFinishEvent);
            StopRunning();
            ClearInts();
            NoBumping(false);
            if (Current == this)
            {
                Current = null;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_envScene != null && RrEnvironment.Activity == RrActivity.StuntRunner)
            {
                RrEnvironment.SetArea(null);
            }

            _envScene = null;
            RrPlayer.RequestPlayMode(PlayModeOwner, false);
        }

        // ------------------------------------------------------------------ phases

        protected override void AuthorityMatchSetup()
        {
            State.Set(IntRun, (State.Get(IntRun) + 1) & 0xFFFF);
            State.Set(IntCourses, Mathf.Max(1, CoursesWithData()));
            RrLog.Info("StuntRunner", "run " + State.Get(IntRun) + " set up: " + State.Get(IntCourses) + " courses, " + Members().Count + " in the room");
        }

        protected override void AuthorityPlaying()
        {
            // Every runner of this run done with this course: the course ends early.
            int run = State.Get(IntRun) & 0xFF;
            int course = State.Round + 1;
            int runners = 0, done = 0;
            foreach (int id in Members())
            {
                int v = RrNet.PlayerInt(id, 0);
                if ((v & 0xF) != course || ((v >> 16) & 0xFF) != run)
                {
                    continue;
                }

                runners++;
                if ((v & (1 << 12)) != 0)
                {
                    done++;
                }
            }

            float elapsed = RoundSeconds - State.TimeRemaining;
            if ((runners > 0 && done == runners && elapsed > 1f) || (runners == 0 && elapsed > 15f))
            {
                RrLog.Info("StuntRunner", "course " + course + " over: " + done + " of " + runners + " runners finished after " + elapsed.ToString("0.0") + " s");
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
                    if (State.Round == 0 && old != RrPhase.RoundOver)
                    {
                        JoinRun();
                    }

                    if (_running)
                    {
                        GoToCourse(State.Round);
                        if (State.Round == 0)
                        {
                            RrSound.PlayUi("SR_Flyover01", 0.6f);
                            RrSound.PlayUi("SR_Announcer_GetReady01", 1f);
                        }
                    }

                    _lastVo = -1;
                    break;
                case RrPhase.Playing:
                    if (_running)
                    {
                        RrSound.PlayUi("SR_Announcer_Go01", 1f);
                        RrSound.PlayUi("SR_GateDown_02", 0.8f);
                        StartCourse();
                    }

                    break;
                case RrPhase.RoundOver:
                    if (_running)
                    {
                        EndCourse();
                    }

                    break;
                case RrPhase.MatchOver:
                    if (_running)
                    {
                        EndRun();
                    }

                    break;
                case RrPhase.Lobby:
                    if (_running || _course >= 0)
                    {
                        StopRunning();
                        if (old != (RrPhase)255)
                        {
                            TeleportLocal(Spawn(Mathf.Abs(RrNet.LocalId)));
                        }
                    }

                    break;
            }

            RrLog.Info("StuntRunner", "phase " + old + " -> " + now + " (course " + (State.Round + 1) + " of " + Rounds + ", running " + _running + ")");
        }

        private void JoinRun()
        {
            _running = true;
            _runId = State.Get(IntRun) & 0xFF;
            for (int i = 0; i < _times.Length; i++)
            {
                _times[i] = -1f;
                RrNet.SetPlayerInt(1 + i, 0);
            }

            RrPlayer.RequestPlayMode(PlayModeOwner, true);
            RrLog.Info("StuntRunner", "joined run " + _runId + " (" + Rounds + " courses, " + (RrPlayer.InVr ? "VR" : "screen") + ")");
        }

        private void StopRunning()
        {
            for (int i = 0; i < _runners.Length; i++)
            {
                if (_runners[i] != null && _runners[i].Live)
                {
                    _runners[i].Restore();
                }
            }

            if (_running || _course >= 0)
            {
                RrPlayer.SetMovement(1f, true);
            }

            _running = false;
            _course = -1;
            _pendingCourse = -1;
            _postRunAt = -1f;
            RrPlayer.RequestPlayMode(PlayModeOwner, false);
            WriteProgress(0, false);
        }

        /// <summary>To the start lane of course i (rank among the members), held in the gate.</summary>
        private void GoToCourse(int i)
        {
            if (_course >= 0 && _course != i && _runners[_course] != null)
            {
                _runners[_course].Live = false;
            }

            if (!CourseReady(i))
            {
                _pendingCourse = i;
                _course = -1;
                Toast("LOADING " + RrStuntRunnerCourses.Title(i).ToUpperInvariant() + "...");
                RrLog.Info("StuntRunner", "course " + (i + 1) + " is still being built here: the runner waits in the lobby");
                WriteProgress(i + 1, false);
                return;
            }

            _pendingCourse = -1;
            _course = i;
            _postRunAt = -1f;
            var level = _courses[i];
            var members = Members();
            members.Sort();
            int rank = Mathf.Max(0, members.IndexOf(RrNet.LocalId));
            var lane = level.Starts[rank % level.Starts.Count];
            var runner = _runners[i];
            runner.ResetRun(lane);
            runner.Live = false;
            RrStuntRunnerRunner.Freeze();
            TeleportLocal(lane);
            WriteProgress(i + 1, false);
            if (_hud != null)
            {
                _hud.Title(RrStuntRunnerCourses.Title(i).ToUpperInvariant(), "GET READY!", 4f);
            }

            if (Phase == RrPhase.Playing)
            {
                StartCourse();
            }

            RrLog.Info("StuntRunner", "runner to " + level.Title + ", lane " + (rank % level.Starts.Count + 1) + " (" + level.Checkpoints.Count + " checkpoints, " + level.Pickups.Count + " KO Colas)");
        }

        private void StartCourse()
        {
            var r = ActiveRunner;
            if (r == null || r.Finished)
            {
                return;
            }

            r.Live = true;
            RrPlayer.SetMovement(1f, true);
        }

        private void EndCourse()
        {
            int i = State.Round;
            var r = ActiveRunner;
            if (i >= 0 && i < _times.Length && _times[i] < 0f)
            {
                _times[i] = RoundSeconds;
                RrNet.SetPlayerInt(1 + i, Mathf.RoundToInt(RoundSeconds * 1000f));
                RrSound.PlayUi("SR_Fail01", 0.7f);
                Toast("TIME! " + RrStuntRunnerCourses.Clock(RoundSeconds));
            }

            if (r != null && r.Live)
            {
                r.Restore();
            }

            RrStuntRunnerRunner.Freeze();
            bool more = State.Round + 1 < Rounds;
            RrSound.PlayUi("SR_CourseClear01", 0.8f);
            if (_hud != null)
            {
                _hud.Title("COURSE COMPLETE!", more ? "Advancing to next course..." : "", 4f);
            }
        }

        private void EndRun()
        {
            var r = ActiveRunner;
            if (r != null && r.Live)
            {
                r.Restore();
            }

            int courses = Rounds;
            float total = 0f;
            var sb = new StringBuilder();
            for (int i = 0; i < courses; i++)
            {
                float t = _times[i] >= 0f ? _times[i] : RoundSeconds;
                total += t;
                sb.Append(i + 1).Append(": ").Append(RrStuntRunnerCourses.Clock(t)).Append(i + 1 < courses ? "   " : "");
            }

            string rank = RrStuntRunnerCourses.Rank(total, courses);
            bool newBest = Best.SaveRun(_times, courses, total);
            _lastRun = "LAST RUN " + RrStuntRunnerCourses.Clock(total) + " (" + rank + ")";
            RrNet.SetPlayerInt(7, Mathf.RoundToInt(total * 1000f));
            RrSound.PlayUi("SR_Audience_Cheer03", 0.65f);
            if (_hud != null)
            {
                _hud.Title("RUN COMPLETE! " + RrStuntRunnerCourses.Clock(total), "You got an " + rank + " rank!   " + sb, 10f);
            }

            if (newBest)
            {
                RrSound.PlayUi("SR_CrowdReactions_NewBestOoo01", 0.8f);
                Toast("NEW PERSONAL BEST!");
            }

            RrLog.Info("StuntRunner", "run complete: total " + RrStuntRunnerCourses.Clock(total) + ", rank " + rank + " (" + sb + ")" + (newBest ? ", new personal best" : ""));
            _course = -1;
            _pendingCourse = -1;
            RrPlayer.SetMovement(1f, true);
            RrPlayer.RequestPlayMode(PlayModeOwner, false);
            TeleportLocal(Spawn(Mathf.Abs(RrNet.LocalId)));
        }

        // ------------------------------------------------------------------ the runner's news

        /// <summary>The local runner pressed the finish button.</summary>
        public void LocalFinished(string how)
        {
            int i = _course;
            if (i < 0 || Phase != RrPhase.Playing)
            {
                return;
            }

            float t = Mathf.Clamp(RoundSeconds - State.TimeRemaining, 0f, RoundSeconds);
            _times[i] = t;
            int ms = Mathf.RoundToInt(t * 1000f);
            RrNet.SetPlayerInt(1 + i, ms);
            WriteProgress(i + 1, true);
            RrNet.Send(EvFinish, new[] { (byte)i, (byte)(ms & 0xFF), (byte)((ms >> 8) & 0xFF), (byte)((ms >> 16) & 0xFF) });

            // Finish VO by the time's share of the max course time (StuntRunnerManager: <= 25 %, <= 66.7 %, else).
            float share = t / RoundSeconds;
            string vo = share <= 0.25f ? Pick("SR_Announcer_Amazing01", "SR_Announcer_Incredible01", "SR_Announcer_Outstanding01")
                : share <= 0.667f ? Pick("SR_Announcer_Adequate01", "SR_Announcer_NiceGoing01") : Pick("SR_Announcer_NiceTry01", "SR_Announcer_SoClose01");
            RrSound.PlayUi(vo, 1f);
            RrSound.PlayUi(share <= 0.667f ? Pick("SR_Audience_Cheer02", "SR_Audience_Cheer03", "SR_Audience_Cheer04") : Pick("SR_CrowdReactions_Laugh02", "SR_CrowdReactions_Laugh03"), 0.65f);
            bool best = Best.IsBetter(i, t);
            if (_hud != null)
            {
                _hud.Title("FINISHED! " + RrStuntRunnerCourses.Clock(t), best ? "NEW PERSONAL BEST!" : "Best " + RrStuntRunnerCourses.Clock(Best.Course(i)), 5f);
            }

            if (best)
            {
                RrSound.PlayUi("SR_CrowdReactions_NewBestOoo01", 0.8f);
            }

            _postRunAt = Time.unscaledTime + 3f;
            RrLog.Info("StuntRunner", "finished " + RrStuntRunnerCourses.Title(i) + " in " + RrStuntRunnerCourses.Clock(t) + " (" + how + ", " + ActiveRunner.CheckpointsTouched + " checkpoints)" + (best ? ", personal best" : ""));
        }

        /// <summary>A checkpoint was touched (the count goes out with the progress int).</summary>
        public void LocalProgress()
        {
            if (_course >= 0)
            {
                WriteProgress(_course + 1, ActiveRunner != null && ActiveRunner.Finished);
            }
        }

        private void OnFinishEvent(int sender, byte[] body)
        {
            if (body == null || body.Length < 4 || sender == RrNet.LocalId)
            {
                return;
            }

            int ms = body[1] | (body[2] << 8) | (body[3] << 16);
            Toast(NameOf(sender) + " FINISHED IN " + RrStuntRunnerCourses.Clock(ms / 1000f));
            RrSound.PlayUi("SR_CrowdReactions_Ahh01", 0.4f);
        }

        private void WriteProgress(int course, bool finished)
        {
            var r = ActiveRunner;
            int cps = r != null ? Mathf.Clamp(r.CheckpointsTouched, 0, 255) : 0;
            int v = (course & 0xF) | (cps << 4) | (finished ? 1 << 12 : 0) | ((_runId & 0xFF) << 16);
            RrNet.SetPlayerInt(0, course > 0 ? v : 0);
        }

        private static void ClearInts()
        {
            for (int i = 0; i <= 7; i++)
            {
                RrNet.SetPlayerInt(i, 0);
            }
        }

        // ------------------------------------------------------------------ every frame

        protected override void OnTick(RrInstanceState s)
        {
            float now = Time.unscaledTime;
            if (_pendingCourse >= 0 && CourseReady(_pendingCourse) && _running && (s.Phase == RrPhase.Countdown || s.Phase == RrPhase.Playing) && s.Round == _pendingCourse)
            {
                GoToCourse(_pendingCourse);
            }

            // 3-2-1 in the last seconds of the gate.
            if (_running && s.Phase == RrPhase.Countdown)
            {
                int whole = Mathf.CeilToInt(s.TimeRemaining);
                if (whole != _lastVo && whole >= 1 && whole <= 3)
                {
                    _lastVo = whole;
                    RrSound.PlayUi(whole == 3 ? "SR_Announcer_StartThree01" : whole == 2 ? "SR_Announcer_StartTwo01" : "SR_Announcer_StartOne01", 1f);
                }
            }

            // After the finish: over to the commentator booth's post-run spot to watch the others.
            if (_postRunAt > 0f && now >= _postRunAt)
            {
                _postRunAt = -1f;
                var level = _course >= 0 ? _courses[_course] : null;
                if (level != null && level.PostRun.position != Vector3.zero && s.Phase == RrPhase.Playing)
                {
                    TeleportLocal(level.PostRun);
                }
            }

            ShowLighting();
            Hud(s);
            if (now >= _boardsAt)
            {
                _boardsAt = now + 0.5f;
                Boards(s);
            }
        }

        private void ShowLighting()
        {
            string area = _course >= 0 ? RrStuntRunnerCourses.Scenes[_course] : RrStuntRunnerCourses.LobbyScene;
            if (area == _envScene)
            {
                return;
            }

            _envScene = area;
            RrEnvironment.SetArea(area);
        }

        private void Hud(RrInstanceState s)
        {
            if (_hud == null)
            {
                return;
            }

            var r = ActiveRunner;
            if (!_running || r == null)
            {
                _hud.SetScore(_running && _pendingCourse >= 0 ? "LOADING COURSE " + (_pendingCourse + 1) + "..." : "");
                _hud.SetHealth("", Color.white);
                return;
            }

            float elapsed = s.Phase == RrPhase.Playing ? RoundSeconds - s.TimeRemaining : 0f;
            float shown = r.Finished && _times[_course] >= 0f ? _times[_course] : elapsed;
            _hud.SetScore(RrStuntRunnerCourses.Title(_course).ToUpperInvariant() + "   " + RrStuntRunnerCourses.Clock(shown));
            _hud.SetHealth("CHECKPOINTS " + r.CheckpointsTouched + "/" + _courses[_course].Checkpoints.Count, r.Finished ? new Color(1f, 0.83f, 0.3f) : Color.white);
        }

        private void Boards(RrInstanceState s)
        {
            var members = Members();
            members.Sort();
            int run = s.Get(IntRun) & 0xFF;
            var sb = new StringBuilder();
            foreach (int id in members)
            {
                int v = RrNet.PlayerInt(id, 0);
                bool inRun = v != 0 && ((v >> 16) & 0xFF) == run;
                sb.Append(NameOf(id)).Append("  ");
                if (!inRun)
                {
                    int total = RrNet.PlayerInt(id, 7);
                    sb.Append(total > 0 ? "last " + RrStuntRunnerCourses.Clock(total / 1000f) : "-");
                }
                else
                {
                    int course = v & 0xF;
                    int ms = course > 0 ? RrNet.PlayerInt(id, course) : 0;
                    sb.Append("C").Append(course).Append(" ");
                    sb.Append((v & (1 << 12)) != 0 && ms > 0 ? RrStuntRunnerCourses.Clock(ms / 1000f) : "CP " + ((v >> 4) & 0xFF));
                }

                sb.Append('\n');
            }

            string players = sb.ToString();
            foreach (var b in _courseBoards)
            {
                if (b != null)
                {
                    b.Custom = players;
                }
            }

            if (_lobbyBoard != null)
            {
                var best = new StringBuilder("PERSONAL BESTS\n");
                int n = Mathf.Max(1, CoursesWithData());
                for (int i = 0; i < n; i++)
                {
                    best.Append(i + 1).Append(" ").Append(RrStuntRunnerCourses.Names[i]).Append("  ").Append(RrStuntRunnerCourses.Clock(Best.Course(i))).Append('\n');
                }

                best.Append("TOTAL ").Append(RrStuntRunnerCourses.Clock(Best.Total)).Append('\n');
                if (_lastRun.Length > 0)
                {
                    best.Append(_lastRun).Append('\n');
                }

                if (s.Phase != RrPhase.Lobby)
                {
                    best.Append(players);
                }

                _lobbyBoard.Custom = best.ToString();
            }
        }

        // ------------------------------------------------------------------ helpers

        public void TeleportLocal(Pose local)
        {
            RrPlayer.Teleport(transform.TransformPoint(local.position), (transform.rotation * local.rotation).eulerAngles.y);
        }

        public void Toast(string text)
        {
            if (_hud != null)
            {
                _hud.Toast(text, 2.5f);
            }
        }

        public void Prompt(string text)
        {
            if (_hud != null)
            {
                _hud.Prompt(text);
            }
        }

        private static string NameOf(int id)
        {
            var p = RrNet.Presence(id);
            return !string.IsNullOrEmpty(p.Nick) ? p.Nick : "Player " + id;
        }

        private static string Pick(params string[] clips)
        {
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        private static void LoadBests()
        {
            Best.Load();
        }

        /// <summary>
        /// "No bumping in the lobby" (2019-09 patch) - and none in the 2 m start lanes: the client's player-player
        /// collision is switched off while in the room (SetCollisionDisableState; the world still collides).
        /// </summary>
        private static void NoBumping(bool on)
        {
            var m = RrPlayer.Motion;
            if (m == null)
            {
                return;
            }

            try
            {
                m.SetCollisionDisableState(on);
            }
            catch (Exception e)
            {
                RrLog.Warn("StuntRunner", "player collision switch failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ personal bests (this PC)

        /// <summary>Personal bests per course and for the whole run, kept on this PC (nothing on a server).</summary>
        public static class Best
        {
            private static readonly float[] Courses = new float[RrStuntRunnerCourses.Count];
            public static float Total = -1f;
            private static bool _loaded;

            private static string FilePath
            {
                get { return Path.Combine(Path.Combine(Application.persistentDataPath, "SomniumRecRoom"), "stuntrunner_best.txt"); }
            }

            public static float Course(int i)
            {
                return i >= 0 && i < Courses.Length ? Courses[i] : -1f;
            }

            public static bool IsBetter(int i, float t)
            {
                Load();
                return i >= 0 && i < Courses.Length && t > 0f && t < RrStuntRunnerCourses.MaxCourseSeconds && (Courses[i] < 0f || t < Courses[i]);
            }

            public static void Load()
            {
                if (_loaded)
                {
                    return;
                }

                _loaded = true;
                for (int i = 0; i < Courses.Length; i++)
                {
                    Courses[i] = -1f;
                }

                try
                {
                    if (!File.Exists(FilePath))
                    {
                        return;
                    }

                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        var p = line.Split(' ');
                        int ms;
                        if (p.Length < 2 || !int.TryParse(p[1], out ms))
                        {
                            continue;
                        }

                        int c;
                        if (p[0] == "total")
                        {
                            Total = ms / 1000f;
                        }
                        else if (int.TryParse(p[0], out c) && c >= 1 && c <= Courses.Length)
                        {
                            Courses[c - 1] = ms / 1000f;
                        }
                    }
                }
                catch (Exception e)
                {
                    RrLog.Warn("StuntRunner", "personal bests not read: " + e.Message);
                }
            }

            /// <summary>Saves the course bests the run beat and the total when all courses were run; true when anything improved.</summary>
            public static bool SaveRun(float[] times, int courses, float total)
            {
                Load();
                bool better = false;
                for (int i = 0; i < courses && i < Courses.Length; i++)
                {
                    if (IsBetter(i, times[i]))
                    {
                        Courses[i] = times[i];
                        better = true;
                    }
                }

                if (courses == RrStuntRunnerCourses.Count && (Total < 0f || total < Total))
                {
                    Total = total;
                    better = true;
                }

                if (!better)
                {
                    return false;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    var sb = new StringBuilder();
                    for (int i = 0; i < Courses.Length; i++)
                    {
                        if (Courses[i] > 0f)
                        {
                            sb.Append(i + 1).Append(' ').Append(Mathf.RoundToInt(Courses[i] * 1000f)).Append('\n');
                        }
                    }

                    if (Total > 0f)
                    {
                        sb.Append("total ").Append(Mathf.RoundToInt(Total * 1000f)).Append('\n');
                    }

                    File.WriteAllText(FilePath, sb.ToString());
                }
                catch (Exception e)
                {
                    RrLog.Warn("StuntRunner", "personal bests not saved: " + e.Message);
                }

                return true;
            }
        }
    }
}
