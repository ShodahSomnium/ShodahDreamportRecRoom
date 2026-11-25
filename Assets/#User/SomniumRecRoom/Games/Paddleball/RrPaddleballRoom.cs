using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paddleball
{
    /// <summary>
    /// Rec Room's Paddleball court (rr_sports.md §3, rebuild plan §9.2): "3D Pong" in a glass tube, a player (or two,
    /// our 2v2) at each end in front of their own goal - the whole back wall.
    ///
    /// Iteration 16: the room is Rec Room's own level13 (Android build) converted with gtscene jt=1 into
    /// Data/Paddleball.bytes (DN/iter16/dodge_paddle.md) and built by <see cref="RrJumbotronArea"/>; scene space is room
    /// space. The level's gym (bleachers on both long sides, the spectator end at -z with its SceneSpawnPoints) holds the
    /// court along z: drawn floor y 0.076, glass walls x +-1.30, glass roof y 2.25, end walls z +-6.95 with the red goal
    /// paint. The level puts Team1 (blue) at +z and Team2 (red) at -z (<see cref="BlueEnd"/>), its projector beside the
    /// court and the REC CENTER door on the spectators' wall. Without the data (RrSceneBank.Text null) the old iteration-1
    /// stand-in is built instead: the 3 x 3 x 12 m tube in a dark hall, blue at -z. Every number the ball and the rules
    /// test (walls, roof, floor, ends, serve points, pads) comes from the court that was built (<see cref="HalfWidth"/> ...).
    /// The objects are Rec Room's: (Paddleball_Paddle) x4 on stands, the (TennisBall) as the floating ball.
    ///
    /// Flow ([PaddleballManager] scoreName "Goal", defaultGoalsToWin 5, ballScoredStateDuration 2 s): the countdown
    /// puts each player on their end's pad with their paddle in hand (LockOnPickup); the ball floats at the
    /// server's serve point until the server hits it (or 5 s pass: a slow automatic serve; 1 s when the serving
    /// side is empty, which is how solo practice works); a ball reaching the back of an end scores for the other
    /// team: "Goal" VO, CrowdCheer01, [VFX_Default_Goal], 2 s, then the conceder serves. First to 5 wins
    /// ("AmyVO_Generic_YouWin" for the winners).
    ///
    /// Networking: the ball and paddles are <see cref="RrNetBody"/> props; the hitter takes the ball (touch-steal)
    /// and claims goals to the instance authority (event 71, repeated until the rally id moves on). The authority
    /// owns the custom ints: 0 ball state (0 waiting for the serve, 1 live, 2 scored), 1 serving team, 2 rally id,
    /// 3 hits this rally, 4 winner + 1, 5 flags (bit 0: one side empty), 8..11 the players of paddle seats 0-3
    /// (PlayerId + 1). Hits are broadcast (event 70) for the sound; goals (72) for the goal show.
    /// </summary>
    public sealed class RrPaddleballRoom : RrGameRoom
    {
        public const string Music = "SportFunk";
        public const string Art = "GiftBox_Paddleball_Tex|8,276,380,268";
        public const string PaddlePrefab = "(Paddleball_Paddle)";
        public const string BallPrefab = "(TennisBall)";

        // ---- rules and numbers (rr_sports.md §3; GUESS where the level holds them) ----
        public const int GoalsToWin = 5;
        public const float ScoredSeconds = 2f;
        public const float ServeTimeout = 5f;
        public const float EmptyServeDelay = 1f;
        public const float AutoServeSpeed = 4f;
        public const float SpeedUp = 1.05f;
        public const float MaxSpeed = 14f;
        public const float MinHitSpeed = 5f;
        public const float BallRadius = 0.1f;

        public const string SceneName = "Paddleball";

        // ---- stand-in layout (no scene data; instance-local, +Z toward red / player 2) ----
        public const float TubeHalfWidth = 1.5f;
        public const float TubeHeight = 3f;
        public const float TubeHalfLength = 6f;
        public const float PadDepth = 1.5f;
        public const float ServeZ = 4.3f;
        public const float ServeY = 1.3f;
        public const float HallHalfX = 7f;
        public const float HallHalfZ = 10f;
        public const float HallHeight = 6f;
        public const int Seats = 4;
        public const int BallPropId = 4;

        // ---- level13 layout (measured, DN/iter16/dodge_paddle.md; scene space = room space) ----
        public const float LevelHalfWidth = 1.30f;
        public const float LevelFloor = 0.076f;
        public const float LevelTop = 2.25f;
        public const float LevelHalfLength = 6.95f;

        /// <summary>The level's serve spot height and distance from the centre (its second ball, at red's end: (0.59, 0.87, -4.29)).</summary>
        public const float LevelServeY = 1.03f;
        public const float LevelServeZ = 4.29f;

        /// <summary>The level's ball at rest in the middle of the court (0, 1.028, 0).</summary>
        public static readonly Vector3 LevelCentre = new Vector3(0f, 1.028f, 0f);

        /// <summary>Team1SpawnPoint (blue, +z, facing -z) / Team2SpawnPoint (red, -z, facing +z): used when the kit reports no marker.</summary>
        private static readonly Vector3[] LevelPads = { new Vector3(0f, 0.001f, 5.6f), new Vector3(0f, 0.001f, -5.77f) };

        /// <summary>The level's (ScoreboardProjector) beside the court, facing it (used when the kit reports no marker).</summary>
        public static readonly Vector3 LevelConsoleSpot = new Vector3(-4.17f, 0f, -0.09f);

        public const float LevelConsoleYaw = 109.6f;

        /// <summary>The level's exit door (ActivityDoor_Frame at (-3.42, 0, -17.90)) on the spectators' wall, facing into the gym.</summary>
        public static readonly Vector3 LevelDoorSpot = new Vector3(-3.42f, 0f, -17.83f);

        public const float LevelDoorYaw = 0f;

        /// <summary>The level's SceneSpawnPoints at the spectator end (all face the court, +z), used when the kit reports none.</summary>
        private static readonly Vector3[] LevelLobby =
        {
            new Vector3(-6.21f, 0.001f, -15.2f), new Vector3(-1.71f, 0.001f, -13.39f), new Vector3(-0.72f, 0.001f, -14.92f), new Vector3(-2.94f, 0.001f, -15.26f),
            new Vector3(-3.2f, 0.001f, -13.62f), new Vector3(-4.16f, 0.001f, -14.92f), new Vector3(-6.07f, 0.001f, -13.32f), new Vector3(-4.65f, 0.001f, -13.47f),
        };

        // ---- the court that was built (level or stand-in); the ball, pads and goals use these ----

        /// <summary>Inner half width of the court (x).</summary>
        public float HalfWidth { get; private set; } = TubeHalfWidth;

        /// <summary>Court floor and roof heights (room-local y).</summary>
        public float Floor { get; private set; }

        public float Top { get; private set; } = TubeHeight;

        /// <summary>Distance of each end (goal plane) from the centre (z).</summary>
        public float HalfLength { get; private set; } = TubeHalfLength;

        /// <summary>Sign of blue's end along z: +1 in level13 (Team1 at +z), -1 in the stand-in tube.</summary>
        public float BlueEnd { get; private set; } = -1f;

        private float _serveY = ServeY, _serveZ = ServeZ;
        private Vector3 _centre = new Vector3(0f, 1.5f, 0f);
        private readonly Pose[] _pads = new Pose[2];
        private readonly List<Pose> _lobbySpawns = new List<Pose>();
        private RrJumbotronArea _area;
        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();

        /// <summary>Sign along z of a team's end (its goal is the back wall there).</summary>
        public float EndZ(int team)
        {
            return team == 0 ? BlueEnd : -BlueEnd;
        }

        /// <summary>The team whose end is on the side of z sign <paramref name="side"/>.</summary>
        public int TeamAtEnd(float side)
        {
            return side * BlueEnd > 0f ? 0 : 1;
        }

        // ---- events and custom ints ----
        public const byte EvPaddleHit = 70;
        public const byte EvGoalClaim = 71;
        public const byte EvGoal = 72;

        public const int IntBall = 0;
        public const int IntServer = 1;
        public const int IntRally = 2;
        public const int IntHits = 3;
        public const int IntWinner = 4;
        public const int IntFlags = 5;
        public const int IntSeats = 8;

        public const int StateWaiting = 0;
        public const int StateLive = 1;
        public const int StateScored = 2;

        public RrPaddleballBall Ball;
        public readonly RrPaddle[] Paddles = new RrPaddle[Seats];
        private readonly List<RrScoreboard> _boards = new List<RrScoreboard>();
        private readonly List<TMPro.TextMeshPro> _infos = new List<TMPro.TextMeshPro>();

        /// <summary>
        /// Rec Room's game hologram (<see cref="RrGameConsole"/>). Level13 build: on the level's (ScoreboardProjector)
        /// marker (<see cref="LevelConsoleSpot"/>). Stand-in hall (below): the level's projector stands beside the
        /// court at mid-length, 4.17 m from its axis (2.9 m outside the glass), turned to face the court: players stand
        /// between it and the tube to read it. That side of our hall is the spectator gallery, and the aisle between
        /// the glass (x 1.55) and the benches (x 4.98) is 3.4 m wide - narrower than the open hologram (4.36 m,
        /// RrGameConsole.ProjectedSize) - so the projector stands at the aisle's south end, past the tube's end
        /// glass and the benches, facing north up the aisle (yaw 0): the hologram runs from x 1.82 to 6.18 at z -5.96,
        /// clear of the glass (at x 3.8 its margin touched Glass E), the benches and the end board (x +-1.6). The arrival spots turn toward it
        /// (<see cref="ArrivalYaw"/>), the tube on their right.
        /// </summary>
        public static readonly Vector3 ConsoleSpot = new Vector3(4.0f, 0f, -6.5f);

        public const float ConsoleYaw = 0f;

        /// <summary>The arrival spots (x 4.3) face south-west: the hologram 40 degrees left, the tube 45 degrees right.</summary>
        public const float ArrivalYaw = -135f;

        private RrGameConsole _console;

        private float _stateSince;
        private int _seenBallState = -1;
        private float _slowSince = -1f;
        private float _boardAt;
        private bool _wasPlayer;
        private int _goalClaimRally = -1;
        private byte[] _goalClaim;
        private float _goalClaimNext, _goalClaimUntil;
        private int _goals;
        private int _clientBallState = -1;
        private int _clientRally = -1;

        // ------------------------------------------------------------------ tuning

        protected override float CountdownSeconds
        {
            get { return 3f; }
        }

        protected override float RoundSeconds
        {
            get { return 0f; }
        }

        protected override float RoundOverSeconds
        {
            get { return 3f; }
        }

        protected override float MatchOverSeconds
        {
            get { return 8f; }
        }

        protected override string PlayMusic
        {
            get { return Music; }
        }

        // ------------------------------------------------------------------ state

        public int BallState
        {
            get { return State.Get(IntBall); }
        }

        public int Rally
        {
            get { return State.Get(IntRally); }
        }

        public int Server
        {
            get { return State.Get(IntServer) & 1; }
        }

        /// <summary>Goal planes count (a match is on and no point is being shown).</summary>
        public bool GoalsLive
        {
            get { return Phase == RrPhase.Playing && BallState != StateScored; }
        }

        public int SeatPlayer(int seat)
        {
            return State.Get(IntSeats + seat) - 1;
        }

        public bool TeamHasPlayers(int team)
        {
            return SeatPlayer(team * 2) >= 0 || SeatPlayer(team * 2 + 1) >= 0;
        }

        public int SeatOf(int playerId)
        {
            for (int s = 0; s < Seats; s++)
            {
                if (SeatPlayer(s) == playerId)
                {
                    return s;
                }
            }

            return -1;
        }

        private Vector3 ServePoint(int team)
        {
            return transform.TransformPoint(new Vector3(0f, _serveY, EndZ(team) * _serveZ));
        }

        // ------------------------------------------------------------------ build

        public override IEnumerator Build()
        {
            BuildProgress = 0f;
            var portal = RrPortal.Prepare();
            while (portal.MoveNext())
            {
                BuildProgress = 0.05f;
                yield return portal.Current;
            }

            var t = transform;
            BuildProgress = 0.1f;
            string raw = RrSceneBank.Text(SceneName);
            if (raw != null)
            {
                _area = new RrJumbotronArea(SceneName, Vector3.zero);
                // Our paddles and ball stand where the level's do; Rec Room hides its circuit boards from players.
                foreach (var n in new[] { PaddlePrefab, "DodgeBall", "CircuitNode_Board" })
                {
                    _area.Skip.Add(n);
                }

                yield return _area.Build(raw, t, _surfaces);
            }
            else
            {
                RrLog.Warn("Paddleball", "no scene data \"" + SceneName + "\": building the stand-in tube and hall");
            }

            if (this == null)
            {
                yield break;
            }

            bool level = _area != null && _area.Kit != null;
            if (!level)
            {
                BuildHall(t);
                yield return null;
                BuildProgress = 0.25f;
                BuildTube(t);
                yield return null;
            }

            UseLayout(level);
            ReadMarkers();
            BuildProgress = 0.4f;

            Pose consolePose;
            if (level)
            {
                RrPortal.Create(t, RrActivity.RecCenter, LevelDoorSpot, LevelDoorYaw, "REC CENTER");
                consolePose = new Pose(LevelConsoleSpot, Quaternion.Euler(0f, LevelConsoleYaw, 0f));
                var holo = _area.Markers("scoreboard");
                if (holo.Count > 0)
                {
                    consolePose = new Pose(holo[0].Position, Quaternion.Euler(0f, holo[0].Rotation.eulerAngles.y, 0f));
                }
            }
            else
            {
                RrPortal.Create(t, RrActivity.RecCenter, new Vector3(HallHalfX - 0.06f, 0f, 0f), -90f, "REC CENTER");
                consolePose = new Pose(ConsoleSpot, Quaternion.Euler(0f, ConsoleYaw, 0f));
            }

            _console = RrGameConsole.ForGame(this, t, consolePose, new RrGameConsoleSpec
            {
                TeamCount = 2,

                // Alone the serves come back from the empty end (1 s auto-serve): START is practice, as the old button was.
                StartBelowMin = true,
                OnStart = id => OnStartPressed(),
                StartLabel = () => Phase == RrPhase.Lobby && Members().Count < 2 ? "PRACTICE" : RrGameConsole.PhaseStartLabel(this),
                Status = () => Phase == RrPhase.Lobby && Members().Count < 2 ? "1 MORE PLAYER NEEDED - START TO PRACTISE" : null,
                Subtitle = () => Phase == RrPhase.Lobby ? "FIRST TO " + GoalsToWin + " GOALS" : RrGameConsole.PhaseLine(this),
            });
            float[] widths;
            if (level)
            {
                // Over each end wall (seen through the glass roof from the other end) and one on the
                // far (+z) end wall for the spectators at the -z end, on its solid band (y 1.83..3.9, inner face z 17.90) between the door pairs.
                widths = new[] { 3.2f, 3.2f, 4f };
                _boards.Add(RrScoreboard.Create(t, new Vector3(0f, 3.8f, HalfLength + 0.8f), Quaternion.Euler(0f, 180f, 0f), "PADDLEBALL", 2, widths[0]));
                _boards.Add(RrScoreboard.Create(t, new Vector3(0f, 3.8f, -HalfLength - 0.8f), Quaternion.identity, "PADDLEBALL", 2, widths[1]));
                _boards.Add(RrScoreboard.Create(t, new Vector3(0f, 2.9f, 17.78f), Quaternion.Euler(0f, 180f, 0f), "PADDLEBALL", 2, widths[2]));
            }
            else
            {
                widths = new[] { 3.2f, 3.2f, 4f };
                _boards.Add(RrScoreboard.Create(t, new Vector3(0f, 4.3f, TubeHalfLength + 0.7f), Quaternion.Euler(0f, 180f, 0f), "PADDLEBALL", 2, widths[0]));
                _boards.Add(RrScoreboard.Create(t, new Vector3(0f, 4.3f, -TubeHalfLength - 0.7f), Quaternion.identity, "PADDLEBALL", 2, widths[1]));
                _boards.Add(RrScoreboard.Create(t, new Vector3(-HallHalfX + 0.15f, 3.2f, 0f), Quaternion.Euler(0f, 90f, 0f), "PADDLEBALL", 2, widths[2]));
            }

            for (int i = 0; i < _boards.Count; i++)
            {
                _infos.Add(Dodgeball.RrDodgeballRoom.InfoLine(_boards[i], widths[i]));
            }
            yield return null;
            BuildProgress = 0.5f;

            // Rec Room's objects: paddles on their stands, the ball, (stand-in only) the goal frames - the level paints its own.
            int goals = level ? 0 : 2;
            int waiting = Seats + 1 + goals;
            int total = waiting;
            for (int s = 0; s < Seats; s++)
            {
                int seat = s;
                Vector3 rest = PaddleRest(seat);
                Quaternion rot = PaddleRestRotation(seat);
                // In the level the stands are inside the court behind each pad: drawn only, so they never trip a player.
                RrBuild.Box(t, "Paddle Stand " + seat, new Vector3(rest.x, (Floor + rest.y - 0.03f) * 0.5f, rest.z), new Vector3(0.3f, rest.y - 0.03f - Floor, 0.3f), RrLook.Solid(RrTeams.Color(seat / 2)), !level);
                RrSpawn.Spawn(PaddlePrefab, t, rest, rot, inst =>
                {
                    SetupPaddle(seat, inst, rest, rot);
                    waiting--;
                });
            }

            Vector3 centre = _centre;
            RrSpawn.Spawn(BallPrefab, t, centre, Quaternion.identity, inst =>
            {
                SetupBall(inst, centre);
                waiting--;
            });

            for (int side = 0; side < goals; side++)
            {
                float z = EndZ(side) * TubeHalfLength;
                RrSpawn.Spawn("(SandboxToolGoalLarge)", t, new Vector3(0f, 1.5f, z), Quaternion.identity, inst =>
                {
                    FitGoal(inst, z);
                    waiting--;
                }, RrSpawnOptions.Visual());
            }

            float until = Time.realtimeSinceStartup + 30f;
            while (waiting > 0 && Time.realtimeSinceStartup < until)
            {
                BuildProgress = 0.5f + 0.45f * (total - waiting) / total;
                yield return null;
            }

            if (Ball == null)
            {
                SetupBall(null, centre);
            }

            BuildProgress = 1f;
            int paddles = 0;
            foreach (var p in Paddles)
            {
                paddles += p != null ? 1 : 0;
            }

            RrLog.Info("Paddleball", "court built from " + (level ? "level13 (" + SceneName + ".bytes)" : "the stand-in tube") + ": " + (HalfWidth * 2f).ToString("F2") + " x " + (Top - Floor).ToString("F2")
                + " x " + (HalfLength * 2f).ToString("F1") + " m (floor y " + Floor.ToString("F3") + "), blue at " + (BlueEnd > 0f ? "+z" : "-z") + ", lobby spawns " + _lobbySpawns.Count
                + ", pads " + _pads[0].position.ToString("F2") + " / " + _pads[1].position.ToString("F2") + ", console at " + consolePose.position.ToString("F1") + "; " + paddles + " paddles (" + PaddlePrefab
                + "), ball " + (Ball != null ? Ball.name : "none") + " r " + BallRadius + " m, zero gravity, first to " + GoalsToWin);
        }

        /// <summary>The court the ball and the rules use: the level's measured tube, or the stand-in's.</summary>
        private void UseLayout(bool level)
        {
            if (level)
            {
                HalfWidth = LevelHalfWidth;
                Floor = LevelFloor;
                Top = LevelTop;
                HalfLength = LevelHalfLength;
                BlueEnd = 1f;
                _serveY = LevelServeY;
                _serveZ = LevelServeZ;
                _centre = LevelCentre;
            }
            else
            {
                HalfWidth = TubeHalfWidth;
                Floor = 0f;
                Top = TubeHeight;
                HalfLength = TubeHalfLength;
                BlueEnd = -1f;
                _serveY = ServeY;
                _serveZ = ServeZ;
                _centre = new Vector3(0f, 1.5f, 0f);
            }
        }

        /// <summary>Lobby (the level's SceneSpawnPoints) and the two pads (Team1/Team2SpawnPoint), measured spots when missing.</summary>
        private void ReadMarkers()
        {
            _lobbySpawns.Clear();
            bool level = _area != null && _area.Kit != null;
            bool[] padSet = new bool[2];
            if (level)
            {
                foreach (var m in _area.Markers("spawn"))
                {
                    string team = m.Get("team", "");
                    var pose = new Pose(m.Position + Vector3.up * 0.05f, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f));
                    int k = team == "blue" ? 0 : team == "red" ? 1 : -1;
                    if (k >= 0)
                    {
                        // The pads' markers sit at y 0.001, under the court's floor (0.076).
                        _pads[k] = new Pose(new Vector3(pose.position.x, Mathf.Max(m.Position.y, Floor) + 0.05f, pose.position.z), pose.rotation);
                        padSet[k] = true;
                    }
                    else
                    {
                        _lobbySpawns.Add(pose);
                    }
                }

                if (_lobbySpawns.Count == 0)
                {
                    foreach (var p in LevelLobby)
                    {
                        _lobbySpawns.Add(new Pose(p + Vector3.up * 0.05f, Quaternion.identity));
                    }
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    _lobbySpawns.Add(new Pose(new Vector3(4.3f, 0.05f, -1.5f + i), Quaternion.Euler(0f, ArrivalYaw, 0f)));
                }
            }

            for (int team = 0; team < 2; team++)
            {
                if (padSet[team])
                {
                    continue;
                }

                float end = EndZ(team);
                Vector3 at = level ? LevelPads[team] : new Vector3(0f, 0f, end * (TubeHalfLength - 0.75f));
                // Facing the other end.
                _pads[team] = new Pose(new Vector3(0f, Mathf.Max(at.y, Floor) + 0.05f, at.z), Quaternion.Euler(0f, end > 0f ? 180f : 0f, 0f));
            }
        }

        private Vector3 PaddleRest(int seat)
        {
            float x = seat % 2 == 0 ? 1.1f : -1.1f;
            if (HalfWidth < 1.4f)
            {
                x *= 0.82f;
            }

            return new Vector3(x, Floor + 0.93f, EndZ(seat / 2) * (HalfLength - 0.35f));
        }

        private Quaternion PaddleRestRotation(int seat)
        {
            return Quaternion.Euler(90f, EndZ(seat / 2) < 0f ? 0f : 180f, 0f);
        }

        private void SetupPaddle(int seat, RrInstance inst, Vector3 rest, Quaternion rot)
        {
            if (inst == null)
            {
                RrLog.Warn("Paddleball", "paddle " + seat + ": " + PaddlePrefab + " did not load");
                return;
            }

            var grab = RrTools.MakeTool(inst);
            GameObject go = grab != null ? grab.gameObject : inst.gameObject;
            if (grab == null)
            {
                grab = go.AddComponent<RrGrabbable>();
            }

            var respawn = go.GetComponent<RrRespawnable>();
            if (respawn != null)
            {
                Destroy(respawn);
            }

            var rb = grab.Body;
            if (rb != null)
            {
                // Rests on its stand; the grab system keeps it kinematic when it lets go.
                rb.isKinematic = true;
            }

            var net = go.GetComponent<RrNetBody>() ?? go.AddComponent<RrNetBody>();
            net.LocalPropId = seat;
            var paddle = go.AddComponent<RrPaddle>();
            paddle.RestLocal = rest;
            paddle.RestRotation = rot;
            paddle.Setup(this, inst, seat, grab, net);
            Paddles[seat] = paddle;
        }

        private void SetupBall(RrInstance inst, Vector3 centre)
        {
            GameObject go;
            if (inst != null)
            {
                go = inst.gameObject;
                // (TennisBall) is 0.07 m; the paddleball floats at about 0.1 m (rr_sports.md §3.3).
                go.transform.localScale *= BallRadius / 0.07f;
                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                {
                    c.isTrigger = false;
                }
            }
            else
            {
                RrLog.Warn("Paddleball", BallPrefab + " did not load, using a plain ball");
                go = RrBuild.Sphere(transform, "Paddleball", centre, BallRadius, RrLook.Emissive(RrLook.Yellow, 1.2f), true, RrLayers.Prop);
            }

            var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            var net = go.GetComponent<RrNetBody>() ?? go.AddComponent<RrNetBody>();
            net.LocalPropId = BallPropId;
            Ball = go.AddComponent<RrPaddleballBall>();
            Ball.Setup(this, net, rb, BallRadius);

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = BallRadius * 1.6f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.05f;
            trail.sharedMaterial = RrLook.Unlit(new Color(1f, 0.93f, 0.55f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var glow = RrBuild.PointLight(go.transform, "Glow", Vector3.zero, new Color(1f, 0.95f, 0.6f), 2.5f, 1.2f);
            glow.transform.localScale = Vector3.one;
        }

        private void FitGoal(RrInstance inst, float z)
        {
            if (inst == null)
            {
                return;
            }

            // The sandbox goal is a 2.1 m Maker Pen frame; stretch it over the 3 x 3 m end of the tube.
            var b = inst.WorldBounds();
            float size = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y));
            float k = (TubeHalfWidth * 2f + 0.2f) / size;
            inst.transform.localScale *= k;
            b = inst.WorldBounds();
            Vector3 want = transform.TransformPoint(new Vector3(0f, TubeHeight * 0.5f, z));
            inst.transform.position += want - b.center;
        }

        private void BuildHall(Transform t)
        {
            var walls = RrLook.Solid(new Color(0.16f, 0.19f, 0.28f));
            var floor = RrLook.Solid(new Color(0.12f, 0.14f, 0.2f));
            RrBuild.RoomShell(t, "Hall", Vector3.zero, new Vector3(HallHalfX * 2f + 0.3f, HallHeight, HallHalfZ * 2f + 0.3f), floor, walls, RrLook.Solid(new Color(0.1f, 0.11f, 0.15f)));

            // Floor grid and light strips: a sporty night court.
            var grid = RrLook.Solid(new Color(0.22f, 0.26f, 0.38f));
            for (int i = -6; i <= 6; i++)
            {
                RrBuild.Box(t, "Grid X", new Vector3(i, 0.004f, 0f), new Vector3(0.03f, 0.004f, HallHalfZ * 2f), grid, false);
            }

            for (int i = -9; i <= 9; i++)
            {
                RrBuild.Box(t, "Grid Z", new Vector3(0f, 0.004f, i), new Vector3(HallHalfX * 2f, 0.004f, 0.03f), grid, false);
            }

            var strip = RrLook.Emissive(new Color(0.55f, 0.8f, 1f), 1.4f);
            RrBuild.Box(t, "Strip W", new Vector3(-HallHalfX + 0.05f, 2.5f, 0f), new Vector3(0.04f, 0.08f, HallHalfZ * 2f), strip, false);
            RrBuild.Box(t, "Strip E", new Vector3(HallHalfX - 0.05f, 2.5f, 0f), new Vector3(0.04f, 0.08f, HallHalfZ * 2f), strip, false);
            RrBuild.Box(t, "Strip N", new Vector3(0f, 2.5f, HallHalfZ - 0.05f), new Vector3(HallHalfX * 2f, 0.08f, 0.04f), RrLook.Emissive(RrLook.TeamRed, 1.4f), false);
            RrBuild.Box(t, "Strip S", new Vector3(0f, 2.5f, -HallHalfZ + 0.05f), new Vector3(HallHalfX * 2f, 0.08f, 0.04f), RrLook.Emissive(RrLook.TeamBlue, 1.4f), false);

            var panel = RrLook.Emissive(new Color(1f, 0.97f, 0.92f), 1.4f);
            float[] lz = { -7f, 0f, 7f };
            foreach (float z in lz)
            {
                RrBuild.Box(t, "Light Panel", new Vector3(0f, HallHeight - 0.06f, z), new Vector3(3f, 0.08f, 1f), panel, false);
                RrBuild.PointLight(t, "Light", new Vector3(0f, HallHeight - 1.2f, z), new Color(0.95f, 0.95f, 1f), 11f, 1.2f);
                RrBuild.PointLight(t, "Gallery Light", new Vector3(4.6f, HallHeight - 1.2f, z), new Color(1f, 0.95f, 0.88f), 9f, 0.9f);
            }

            // Spectator gallery: two benches facing the tube.
            var seat = RrLook.Solid(RrLook.DarkWood);
            RrBuild.Box(t, "Gallery Bench 1", new Vector3(5.2f, 0.23f, 0f), new Vector3(0.45f, 0.45f, 8f), seat);
            RrBuild.Box(t, "Gallery Riser", new Vector3(6.3f, 0.2f, 0f), new Vector3(1.3f, 0.4f, 9f), RrLook.Solid(new Color(0.22f, 0.25f, 0.34f)));
            RrBuild.Box(t, "Gallery Bench 2", new Vector3(6.4f, 0.63f, 0f), new Vector3(0.45f, 0.45f, 8f), seat);
            RrBuild.Sign(t, "Title", "PADDLEBALL", new Vector3(-HallHalfX + 0.1f, 4.9f, 0f), Quaternion.Euler(0f, -90f, 0f), 12f, RrLook.Orange, true, 10f);
        }

        private void BuildTube(Transform t)
        {
            var glass = RrLook.Glass(new Color(0.65f, 0.85f, 1f, 0.16f));
            const float th = 0.05f;
            float w = TubeHalfWidth;
            float h = TubeHeight;
            float l = TubeHalfLength;

            // The glass (layer 23: the avatar stays in, the VR ray hits it); the ball is reflected by its own code.
            RrBuild.Box(t, "Glass W", new Vector3(-w - th * 0.5f, h * 0.5f, 0f), new Vector3(th, h, l * 2f + 0.4f), glass);
            RrBuild.Box(t, "Glass E", new Vector3(w + th * 0.5f, h * 0.5f, 0f), new Vector3(th, h, l * 2f + 0.4f), glass);
            RrBuild.Box(t, "Glass Top", new Vector3(0f, h + th * 0.5f, 0f), new Vector3(w * 2f + th * 2f, th, l * 2f + 0.4f), glass);
            RrBuild.Box(t, "Glass Back S", new Vector3(0f, h * 0.5f, -l - 0.2f - th * 0.5f), new Vector3(w * 2f, h, th), glass);
            RrBuild.Box(t, "Glass Back N", new Vector3(0f, h * 0.5f, l + 0.2f + th * 0.5f), new Vector3(w * 2f, h, th), glass);

            // Tube floor and end pads.
            RrBuild.Box(t, "Tube Floor", new Vector3(0f, 0.006f, 0f), new Vector3(w * 2f, 0.004f, l * 2f), RrLook.Solid(new Color(0.2f, 0.24f, 0.34f)), false);
            for (int team = 0; team < 2; team++)
            {
                float sz = team == 0 ? -1f : 1f;
                Color c = RrTeams.Color(team);
                RrBuild.Box(t, "Pad " + RrTeams.Name(team), new Vector3(0f, 0.009f, sz * (l - PadDepth * 0.5f)), new Vector3(w * 2f, 0.004f, PadDepth), RrLook.Solid(Color.Lerp(c, RrLook.DarkGrey, 0.45f)), false);
                RrBuild.Box(t, "Pad Line " + RrTeams.Name(team), new Vector3(0f, 0.012f, sz * (l - PadDepth)), new Vector3(w * 2f, 0.004f, 0.05f), RrLook.Emissive(c, 1.5f), false);
                // The goal face: the back of the end glows in the team colour.
                RrBuild.Box(t, "Goal Face " + RrTeams.Name(team), new Vector3(0f, h * 0.5f, sz * (l + 0.12f)), new Vector3(w * 2f, h, 0.02f), RrLook.Glass(new Color(c.r, c.g, c.b, 0.22f)), false);
            }

            // Glowing frame: the four long edges and rings every 3 m, team colours at the ends.
            var edge = RrLook.Emissive(new Color(0.75f, 0.92f, 1f), 1.6f);
            const float e = 0.06f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = 0; sy <= 1; sy++)
                {
                    RrBuild.Box(t, "Edge", new Vector3(sx * (w + th), sy * h + (sy == 0 ? e * 0.5f : th), 0f), new Vector3(e, e, l * 2f + 0.4f), edge, false);
                }
            }

            for (int i = -2; i <= 2; i++)
            {
                float z = i * 3f;
                var m = i == -2 ? RrLook.Emissive(RrLook.TeamBlue, 2f) : i == 2 ? RrLook.Emissive(RrLook.TeamRed, 2f) : edge;
                RrBuild.Box(t, "Ring L", new Vector3(-w - th, h * 0.5f, z), new Vector3(e, h, e), m, false);
                RrBuild.Box(t, "Ring R", new Vector3(w + th, h * 0.5f, z), new Vector3(e, h, e), m, false);
                RrBuild.Box(t, "Ring T", new Vector3(0f, h + th, z), new Vector3(w * 2f + th * 2f, e, e), m, false);
            }
        }

        // ------------------------------------------------------------------ spawns

        public override Pose Spawn(int index)
        {
            if (_lobbySpawns.Count == 0)
            {
                ReadMarkers();
            }

            return _lobbySpawns[Mathf.Abs(index) % _lobbySpawns.Count];
        }

        /// <summary>A player's spot on its team's pad (the level's Team1/Team2SpawnPoint); a pair stands side by side.</summary>
        private Pose PadSpot(int team, int seatInTeam, bool pair)
        {
            var pad = _pads[team & 1];
            float x = pair ? (seatInTeam == 0 ? -1f : 1f) * Mathf.Min(0.7f, HalfWidth * 0.47f) : 0f;
            // Mirrored at the other end, so seat 0 is on the same hand for both teams (as the stand-in had it).
            if (EndZ(team) > 0f)
            {
                x = -x;
            }

            return new Pose(pad.position + new Vector3(x, 0f, 0f), pad.rotation);
        }

        private void TeleportLocal(Pose local)
        {
            RrPlayer.Teleport(transform.TransformPoint(local.position), (transform.rotation * local.rotation).eulerAngles.y);
        }

        // ------------------------------------------------------------------ lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            RrNet.On(EvPaddleHit, OnPaddleHitEvent);
            RrNet.On(EvGoalClaim, OnGoalClaim);
            RrNet.On(EvGoal, OnGoal);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RrNet.Off(EvPaddleHit, OnPaddleHitEvent);
            RrNet.Off(EvGoalClaim, OnGoalClaim);
            RrNet.Off(EvGoal, OnGoal);
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            ReleaseLocalPaddles();
        }

        private void OnStartPressed()
        {
            if (Phase == RrPhase.Lobby)
            {
                RrLog.Info("Paddleball", "start pressed");
                RequestStart();
            }
        }

        private void ReleaseLocalPaddles()
        {
            foreach (var p in Paddles)
            {
                if (p != null && p.HeldLocally)
                {
                    RrGrab.Release(p.Grab, Vector3.zero, Vector3.zero);
                }
            }
        }

        // ------------------------------------------------------------------ phases

        protected override void OnPhase(RrPhase old, RrPhase now)
        {
            switch (now)
            {
                case RrPhase.Countdown:
                    TakeYourPlace();
                    if (IsAuthority)
                    {
                        var s = State;
                        s.Set(IntBall, StateWaiting);
                        s.Set(IntServer, 0);
                        s.Set(IntRally, s.Get(IntRally) + 1);
                        s.Set(IntHits, 0);
                        s.Set(IntWinner, 0);
                        for (int i = 0; i < Seats; i++)
                        {
                            s.Set(IntSeats + i, 0);
                        }

                        ParkBall(0);
                    }

                    break;
                case RrPhase.RoundOver:
                    int winner = State.Get(IntWinner) - 1;
                    if (winner >= 0)
                    {
                        RrAnnouncer.Say(RrTeams.LocalTeam == winner ? "AmyVO_Generic_YouWin" : RrAnnouncer.TeamWins(winner));
                    }

                    break;
                case RrPhase.Lobby:
                    ReleaseLocalPaddles();
                    if (_wasPlayer && old != (RrPhase)255)
                    {
                        TeleportLocal(Spawn(Mathf.Abs(RrNet.LocalId)));
                    }

                    _wasPlayer = false;
                    break;
            }

            RrLog.Info("Paddleball", "phase " + old + " -> " + now + " (blue " + State.TeamScore(0) + " - red " + State.TeamScore(1) + ")");
        }

        protected override void OnTeamsAssigned()
        {
            if (Phase == RrPhase.Countdown)
            {
                TakeYourPlace();
            }
        }

        /// <summary>Countdown: the local player goes to its end of the tube and takes its paddle.</summary>
        private void TakeYourPlace()
        {
            int team = RrTeams.LocalTeam;
            if (team != 0 && team != 1)
            {
                return;
            }

            var members = Members();
            members.Sort();
            int rank = 0, count = 0;
            foreach (int id in members)
            {
                if (RrTeams.TeamOf(id) != team && id != RrNet.LocalId)
                {
                    continue;
                }

                if (id == RrNet.LocalId)
                {
                    rank = count;
                }

                count++;
            }

            rank = Mathf.Clamp(rank, 0, 1);
            _wasPlayer = true;
            TeleportLocal(PadSpot(team, rank, count > 1));
            var hand = RrHand.Right;
            var paddle = Paddles[team * 2 + rank];
            if (paddle == null || paddle.HeldByAnyone)
            {
                paddle = Paddles[team * 2 + (1 - rank)];
            }

            if (hand != null && paddle != null && paddle.Grab != null && !paddle.HeldByAnyone && RrGrab.HeldBy(hand) == null)
            {
                RrGrab.Give(paddle.Grab, hand);
                RrLog.Info("Paddleball", "paddle " + paddle.Slot + " given to the local player (" + RrTeams.Name(team) + ", " + (RrPlayer.InVr ? "VR" : "screen") + ")");
            }
        }

        protected override void AuthorityRoundStart(int round)
        {
            var s = State;
            var members = Members();
            members.Sort();
            int[] perTeam = new int[2];
            foreach (int id in members)
            {
                int team = RrTeams.TeamOf(id);
                if ((team != 0 && team != 1) || perTeam[team] >= 2)
                {
                    continue;
                }

                s.Set(IntSeats + team * 2 + perTeam[team], id + 1);
                perTeam[team]++;
            }

            bool solo = perTeam[0] == 0 || perTeam[1] == 0;
            s.Set(IntFlags, solo ? 1 : 0);
            StartServe(0);
            RrLog.Info("Paddleball", "match playing: blue " + perTeam[0] + " v red " + perTeam[1] + (solo ? " (one side empty: it serves itself)" : ""));
        }

        protected override void AuthorityPlaying()
        {
            float now = Time.unscaledTime;
            var s = State;
            int state = s.Get(IntBall);
            if (state != _seenBallState)
            {
                _seenBallState = state;
                _stateSince = now;
            }

            switch (state)
            {
                case StateWaiting:
                    float wait = TeamHasPlayers(Server) ? ServeTimeout : EmptyServeDelay;
                    if (now - _stateSince >= wait)
                    {
                        AutoServe();
                    }

                    break;
                case StateLive:
                    if (Ball != null && Ball.Velocity.magnitude < 0.3f)
                    {
                        if (_slowSince < 0f)
                        {
                            _slowSince = now;
                        }
                        else if (now - _slowSince > 3f)
                        {
                            RrLog.Info("Paddleball", "ball stalled: serving again");
                            StartServe(Server);
                        }
                    }
                    else
                    {
                        _slowSince = -1f;
                    }

                    break;
                case StateScored:
                    if (now - _stateSince >= ScoredSeconds)
                    {
                        StartServe(Server);
                    }

                    break;
            }
        }

        protected override void AuthorityRoundOver(int winner)
        {
            // The winner line is played by every client itself (YouWin for the winners).
        }

        protected override void AuthorityBackToLobby()
        {
            ParkBall(-1);
            foreach (var p in Paddles)
            {
                if (p != null && p.Net != null && !p.HeldByAnyone)
                {
                    p.Net.ResetTo(transform.TransformPoint(p.RestLocal), transform.rotation * p.RestRotation);
                }
            }

            var s = State;
            s.Set(IntBall, StateWaiting);
            s.Set(IntFlags, 0);
        }

        private void StartServe(int team)
        {
            var s = State;
            s.Set(IntServer, team);
            s.Set(IntRally, s.Get(IntRally) + 1);
            s.Set(IntHits, 0);
            s.Set(IntBall, StateWaiting);
            _slowSince = -1f;
            _seenBallState = StateWaiting;
            _stateSince = Time.unscaledTime;
            ParkBall(team);
        }

        private void ParkBall(int team)
        {
            if (Ball == null)
            {
                return;
            }

            Ball.Park(team < 0 ? transform.TransformPoint(_centre) : ServePoint(team));
        }

        private void AutoServe()
        {
            if (Ball == null)
            {
                return;
            }

            int server = Server;
            // Toward the other end.
            Vector3 dir = transform.TransformDirection(new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.05f, 0.1f), -EndZ(server))).normalized;
            Ball.Hit(ServePoint(server), dir * AutoServeSpeed);
            State.Set(IntBall, StateLive);
            RrLog.Info("Paddleball", "automatic serve by " + RrTeams.Name(server) + " (rally " + Rally + ")");
        }

        // ------------------------------------------------------------------ hits (the holder's client)

        /// <summary>A local paddle met the ball: the rally rules, then the ball goes (touch-steal) and everyone hears it.</summary>
        public void PaddleHit(RrPaddle paddle, Vector3 worldPos, Vector3 velocity)
        {
            if (Ball == null)
            {
                return;
            }

            bool playing = Phase == RrPhase.Playing;
            if (playing && BallState == StateScored)
            {
                return;
            }

            int team = RrTeams.LocalTeam;
            float rally = Ball.RallySpeed;
            float speed = Mathf.Clamp(Mathf.Max(velocity.magnitude, rally * SpeedUp, MinHitSpeed), MinHitSpeed, MaxSpeed);
            float toward = team == 0 || team == 1 ? -EndZ(team) : 1f;
            Vector3 dir = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : transform.TransformDirection(Vector3.forward * toward);
            Vector3 local = transform.InverseTransformDirection(dir);
            if (playing && (team == 0 || team == 1))
            {
                // Always toward the other end, with at least 35 % of the speed along the tube.
                float sign = toward;
                if (local.z * sign < 0.35f)
                {
                    local.z = 0.35f * sign;
                    Vector3 side = new Vector3(local.x, local.y, 0f);
                    float sideLen = Mathf.Sqrt(1f - 0.35f * 0.35f);
                    side = side.sqrMagnitude > 1e-6f ? side.normalized * Mathf.Min(side.magnitude, sideLen) : Vector3.zero;
                    local = new Vector3(side.x, side.y, local.z).normalized;
                }
            }

            Vector3 v = transform.TransformDirection(local) * speed;
            Ball.Hit(worldPos, v);
            paddle.PlayImpact(speed / MaxSpeed);
            int r = Rally;
            RrNet.Send(EvPaddleHit, new[] { (byte)(r & 0xFF), (byte)((r >> 8) & 0xFF), (byte)(team < 0 ? 255 : team), (byte)Mathf.Clamp(Mathf.RoundToInt(speed * 10f), 0, 255) });
            if (paddle.Hits == 1)
            {
                RrLog.Info("Paddleball", "first paddle hit: paddle " + paddle.Slot + " at " + speed.ToString("F1") + " m/s (" + (RrPlayer.InVr ? "VR" : "screen") + ")");
            }
        }

        private void OnPaddleHitEvent(int sender, byte[] body)
        {
            if (body == null || body.Length < 4)
            {
                return;
            }

            if (Ball != null)
            {
                float speed = body[3] / 10f;
                RrSound.Play("RacketHit01", Ball.transform.position, 0.45f + 0.55f * Mathf.Clamp01(speed / MaxSpeed), 1f + Random.Range(-0.2f, 0.2f), 30f);
            }

            if (!IsAuthority || Phase != RrPhase.Playing)
            {
                return;
            }

            int rally = body[0] | (body[1] << 8);
            var s = State;
            if ((rally & 0xFFFF) != (s.Get(IntRally) & 0xFFFF) || s.Get(IntBall) == StateScored)
            {
                return;
            }

            s.Set(IntHits, s.Get(IntHits) + 1);
            if (s.Get(IntBall) == StateWaiting)
            {
                s.Set(IntBall, StateLive);
            }
        }

        // ------------------------------------------------------------------ goals

        /// <summary>The simulating client saw the ball reach an end: claim the point (repeated until the rally moves on).</summary>
        public void ClaimGoal(int scorer, int rally, Vector3 where)
        {
            _goalClaimRally = rally;
            _goalClaim = new[] { (byte)(rally & 0xFF), (byte)((rally >> 8) & 0xFF), (byte)scorer };
            _goalClaimNext = Time.unscaledTime + 0.3f;
            _goalClaimUntil = Time.unscaledTime + 2f;
            RrNet.Send(EvGoalClaim, _goalClaim, true);
        }

        private void OnGoalClaim(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 3 || Phase != RrPhase.Playing)
            {
                return;
            }

            var s = State;
            int rally = body[0] | (body[1] << 8);
            int scorer = body[2] & 1;
            if (rally != (s.Get(IntRally) & 0xFFFF) || s.Get(IntBall) == StateScored)
            {
                return;
            }

            AddTeamScore(scorer, 1);
            s.Set(IntBall, StateScored);
            s.Set(IntServer, 1 - scorer);
            int blue = s.TeamScore(0), red = s.TeamScore(1);
            RrNet.Send(EvGoal, new[] { (byte)scorer, (byte)blue, (byte)red });
            if (s.TeamScore(scorer) >= GoalsToWin)
            {
                s.Set(IntWinner, scorer + 1);
                EndRound(scorer);
            }
        }

        private void OnGoal(int sender, byte[] body)
        {
            if (body == null || body.Length < 3)
            {
                return;
            }

            int scorer = body[0] & 1;
            _goals++;
            // The ball went into the conceding team's end.
            float end = EndZ(1 - scorer);
            Vector3 at = transform.TransformPoint(new Vector3(0f, (Floor + Top) * 0.5f, end * HalfLength));
            RrAnnouncer.Say("Goal");
            RrSound.Play("CrowdCheer01", at, 1f, 1f, 60f);
            // Facing back into the court.
            RrFx.Spawn("[VFX_Default_Goal]", at, transform.rotation * Quaternion.Euler(0f, end > 0f ? 180f : 0f, 0f), 1.5f);
            RrLog.Info("Paddleball", "goal for " + RrTeams.Name(scorer) + ": blue " + body[1] + " - red " + body[2] + " (goal " + _goals + ")");
        }

        // ------------------------------------------------------------------ every frame

        protected override void OnTick(RrInstanceState s)
        {
            // Every client: a whistle when a serve is set up (waiting for the server).
            int shown = s.Phase == RrPhase.Playing ? s.Get(IntBall) : -1;
            int rallyNow = s.Get(IntRally);
            if (shown == StateWaiting && (shown != _clientBallState || rallyNow != _clientRally))
            {
                RrSound.PlayUi("AttentionWhistle", 0.5f);
            }

            _clientBallState = shown;
            _clientRally = rallyNow;

            if (_goalClaim != null)
            {
                float now = Time.unscaledTime;
                bool done = now > _goalClaimUntil || (s.Get(IntRally) & 0xFFFF) != _goalClaimRally || s.Get(IntBall) == StateScored;
                if (done)
                {
                    _goalClaim = null;
                }
                else if (now >= _goalClaimNext)
                {
                    _goalClaimNext = now + 0.3f;
                    RrNet.Send(EvGoalClaim, _goalClaim, true);
                }
            }

            if (Time.unscaledTime >= _boardAt)
            {
                _boardAt = Time.unscaledTime + 0.25f;
                // Custom text replaces the board's scores, so it is used only in the lobby; the line under
                // the board carries the serve / rally.
                string text = s.Phase == RrPhase.Lobby ? "FIRST TO " + GoalsToWin : "";
                string info;
                switch (s.Phase)
                {
                    case RrPhase.Playing:
                        int state = s.Get(IntBall);
                        info = state == StateWaiting ? RrTeams.Name(Server).ToUpperInvariant() + " SERVES" : state == StateScored ? "GOAL!" : "RALLY  " + s.Get(IntHits);
                        break;
                    default:
                        info = "FIRST TO " + GoalsToWin + " POINTS";
                        break;
                }

                foreach (var b in _boards)
                {
                    if (b != null)
                    {
                        b.Custom = text;
                    }
                }

                foreach (var i in _infos)
                {
                    if (i != null && i.text != info)
                    {
                        i.text = info;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ harness hooks (RrPaddleballTest)

        internal void TestHit(Vector3 localDir, float speed)
        {
            RrPaddle paddle = null;
            foreach (var p in Paddles)
            {
                if (p != null && p.HeldLocally)
                {
                    paddle = p;
                }
            }

            paddle = paddle ?? Paddles[0];
            if (paddle == null || Ball == null)
            {
                RrLog.Warn("PaddleballTest", "no paddle or ball");
                return;
            }

            PaddleHit(paddle, Ball.transform.position, transform.TransformDirection(localDir.normalized) * speed);
        }
    }
}
