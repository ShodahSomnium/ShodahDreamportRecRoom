using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Soccer
{
    /// <summary>
    /// Rec Room's Soccer (^Soccer; rr_sports.md §4, rr_latest_rgames_action.md §7: unchanged since 2019). The room is Rec
    /// Room's own level72 (Android build, soccer.unity) converted with gtscene jt=1 into Data/Soccer.bytes and built by
    /// <see cref="RrJumbotronArea"/> (install meshes, hull rectangles, colliders, lights): the gym with its bleachers, a
    /// 44 x 88 m glass-walled court and two 20 m goals (5.6 m deep boxes from z +-44.25 to +-49.8), and the pre-game
    /// hall west of the court with the level's (ScoreboardProjector) spot and the exit door. Scene space is room space.
    ///
    /// Flow ([Soccer_GameRuleWrapperCircuit] defaultGameDuration 180): START on the hologram (alone it is practice); the
    /// countdown puts each team on its own spawn points (Team1 = blue defends -z, Team2 = red +z) and the ball on the centre
    /// spot; play starts with the ball dropped from the level's ball spawn (0, 5.18, 0) - "kick it before it lands". A ball
    /// in a goal's trigger zone (the level's Team1Goal / Team2Goal, 20 x 4.8 x 4.5 m) scores for the other team: goal horn,
    /// "Goal" and the team line, 3 s, then a new drop at the centre (players are not moved, GUESS §4.2). After 3 minutes the
    /// most goals wins (a draw says "Tiegame"). The goal boxes in front of each goal (the level's GoalboxMovementBlocker,
    /// 21.5 x 6.8 m) keep players out while a match is on.
    ///
    /// The ball is Rec Room's (SoccerBall) (TOOLS key 00: root scale 0.65 = 0.715 m radius, mass 0.75, bounciness 0.6,
    /// CanBePickedUp 0), shared as <see cref="RrNetBody"/> prop 0; <see cref="RrSoccerBall"/> kicks it from the local
    /// player's body, teleports, VR hands and the desktop click, and the simulating client claims goals to the authority
    /// (event 91, repeated until the kick-off id moves on - the Paddleball pattern). Custom ints: 0 ball state (0 idle,
    /// 1 live, 2 scored), 1 kick-off id.
    /// </summary>
    public sealed class RrSoccerRoom : RrGameRoom
    {
        public const string Music = "SportFunk";
        public const string Art = "GiftBox_Soccer_Tex|8,276,380,268";
        public const string SceneName = "Soccer";
        public const string BallPrefab = "(SoccerBall)";

        // ---- rules (bundle / research; GUESS where marked) ----
        public const float MatchSeconds = 180f;
        public const float ScoredSeconds = 3f;
        public const float BallRadius = 0.715f;
        public const int BallPropId = 0;

        // ---- events and custom ints ----
        public const byte EvKick = 90;
        public const byte EvGoalClaim = 91;
        public const byte EvGoal = 92;

        public const int IntBall = 0;
        public const int IntKickoff = 1;

        public const int StateIdle = 0;
        public const int StateLive = 1;
        public const int StateScored = 2;

        // ---- layout (level72, measured; room-local = scene space) ----
        public static readonly Vector3 KickoffSpot = new Vector3(0f, 5.18f, 0f);

        /// <summary>The goal trigger zones by the team that defends them (Team1Goal = blue at -z), grown by the ball's radius.</summary>
        public static readonly Bounds[] Goals =
        {
            new Bounds(new Vector3(0f, 2.238f, -47.81f), new Vector3(20f, 4.8f, 4.5f) + Vector3.one * (BallRadius * 2f)),
            new Bounds(new Vector3(0f, 2.11f, 47.83f), new Vector3(20f, 4.8f, 4.5f) + Vector3.one * (BallRadius * 2f)),
        };

        /// <summary>The goal boxes players may not stand in during a match (GoalboxMovementBlocker, 21.46 x 10 x 6.79 m).</summary>
        public static readonly Bounds[] GoalBoxes =
        {
            new Bounds(new Vector3(0f, 4.85f, -40.76f), new Vector3(21.46f, 10.02f, 6.79f)),
            new Bounds(new Vector3(0f, 4.85f, 40.97f), new Vector3(21.46f, 10.02f, 6.79f)),
        };

        /// <summary>The level's (ScoreboardProjector) in the pre-game hall (used when the kit reports no marker).</summary>
        public static readonly Vector3 ConsoleSpot = new Vector3(-24.93f, -0.05f, 2.97f);

        public const float ConsoleYaw = 225f;

        /// <summary>The level's exit door (ActivityDoorSingle_Frame) on the pre-game hall's west wall, facing into the hall.</summary>
        public static readonly Vector3 DoorSpot = new Vector3(-38.6f, -0.04f, 0.35f);

        public const float DoorYaw = 90f;

        private static readonly Vector3[] FallbackLobby =
        {
            new Vector3(-24.39f, 0f, -4.18f), new Vector3(-31.03f, 0f, -0.11f), new Vector3(-27.63f, 0f, -4.03f), new Vector3(-30.79f, 0f, -3.36f),
            new Vector3(-33.12f, 0f, 1.42f), new Vector3(-34.25f, 0f, -1.62f), new Vector3(-25.42f, 0f, -0.76f), new Vector3(-28.27f, 0f, -0.67f),
        };

        public RrSoccerBall Ball;
        private RrJumbotronArea _area;
        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private readonly List<Pose> _lobbySpawns = new List<Pose>();
        private readonly List<Pose>[] _teamSpawns = { new List<Pose>(), new List<Pose>() };
        private readonly List<RrScoreboard> _boards = new List<RrScoreboard>();
        private RrGameConsole _console;
        private PhysicsMaterial _fallbackPhysics;

        private int _seenBallState = -1;
        private float _stateSince;
        private bool _wasPlayer;
        private int _goalClaimKickoff = -1;
        private byte[] _goalClaim;
        private float _goalClaimNext, _goalClaimUntil;
        private float _teamLineAt = -1f;
        private int _teamLineTeam;
        private float _boxCheckAt;
        private int _goals;

        // ------------------------------------------------------------------ tuning

        protected override int MinPlayersToStart
        {
            get { return 2; }
        }

        protected override float CountdownSeconds
        {
            get { return 5f; }
        }

        protected override float RoundSeconds
        {
            get { return MatchSeconds; }
        }

        protected override float RoundOverSeconds
        {
            get { return 4f; }
        }

        protected override float MatchOverSeconds
        {
            get { return 8f; }
        }

        protected override bool AutoStart
        {
            get { return false; }
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

        public int Kickoff
        {
            get { return State.Get(IntKickoff); }
        }

        /// <summary>Goals count (a match is on and no goal is being shown).</summary>
        public bool GoalsLive
        {
            get { return Phase == RrPhase.Playing && BallState == StateLive; }
        }

        // ------------------------------------------------------------------ build

        public override IEnumerator Build()
        {
            BuildProgress = 0f;
            var portal = RrPortal.Prepare();
            while (portal.MoveNext())
            {
                BuildProgress = 0.03f;
                yield return portal.Current;
            }

            var t = transform;
            BuildProgress = 0.05f;
            string raw = RrSceneBank.Text(SceneName);
            if (raw != null)
            {
                _area = new RrJumbotronArea(SceneName, Vector3.zero);
                // Our own ball and door stand where the level's do.
                foreach (var n in new[] { BallPrefab, "ActivityDoorSingle_Frame", "ActivityDoor_Glow", "ActivityDoor_Emit", "ActivityDoor_Frame", "ActivityDoor_Left", "ActivityDoor_Right" })
                {
                    _area.Skip.Add(n);
                }

                yield return _area.Build(raw, t, _surfaces);
            }
            else
            {
                RrLog.Warn("Soccer", "no scene data \"" + SceneName + "\": building the plain stand-in court");
                BuildFallback(t);
            }

            if (this == null)
            {
                yield break;
            }

            BuildProgress = 0.7f;
            ReadMarkers();
            RrPortal.Create(t, RrActivity.RecCenter, DoorSpot, DoorYaw, "REC CENTER");
            Pose consolePose = new Pose(ConsoleSpot, Quaternion.Euler(0f, ConsoleYaw, 0f));
            var holo = _area != null ? _area.Markers("scoreboard") : new List<RrSceneMarker>();
            if (holo.Count > 0)
            {
                consolePose = new Pose(holo[0].Position, Quaternion.Euler(0f, holo[0].Rotation.eulerAngles.y, 0f));
            }

            _console = RrGameConsole.ForGame(this, t, consolePose, new RrGameConsoleSpec
            {
                TeamCount = 2,
                StartBelowMin = true,
                MinPlayers = 2,
                OnStart = id => OnStartPressed(),
                StartLabel = () => Phase == RrPhase.Lobby && Members().Count < 2 ? "PRACTICE" : RrGameConsole.PhaseStartLabel(this),
                Status = () => Phase == RrPhase.Lobby && Members().Count < 2 ? "1 MORE PLAYER NEEDED - START TO PRACTISE" : null,
                Timer = () => RrGameConsole.PhaseTimer(this),
                Subtitle = () => Phase == RrPhase.Lobby ? "3 MINUTES - MOST GOALS WINS" : RrGameConsole.PhaseLine(this),
            });

            // The level's big orange panels over each end carry our boards (clock + team scores).
            _boards.Add(RrScoreboard.Create(t, new Vector3(0.2f, 14.68f, 51.55f), Quaternion.Euler(0f, 180f, 0f), "SOCCER", 2, 10f));
            _boards.Add(RrScoreboard.Create(t, new Vector3(0.2f, 14.68f, -51.55f), Quaternion.identity, "SOCCER", 2, 10f));
            yield return null;

            BuildProgress = 0.8f;
            bool spawned = false;
            RrSpawn.Spawn(BallPrefab, t, CentreSpot(), Quaternion.identity, inst =>
            {
                SetupBall(inst);
                spawned = true;
            });

            float until = Time.realtimeSinceStartup + 30f;
            while (!spawned && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            if (Ball == null)
            {
                SetupBall(null);
            }

            BuildProgress = 1f;
            RrLog.Info("Soccer", "field built from " + (_area != null ? "level72 (" + SceneName + ".bytes)" : "the stand-in court") + ": lobby spawns " + _lobbySpawns.Count + ", blue spawns " + _teamSpawns[0].Count
                + ", red spawns " + _teamSpawns[1].Count + ", console at " + consolePose.position.ToString("F1") + "; ball " + (Ball != null ? Ball.name + " r " + Ball.Radius.ToString("F3") + " m" : "none")
                + "; " + MatchSeconds + " s match, goals 20 x 4.8 m at z +-44.25");
        }

        private void ReadMarkers()
        {
            _lobbySpawns.Clear();
            _teamSpawns[0].Clear();
            _teamSpawns[1].Clear();
            if (_area != null)
            {
                foreach (var m in _area.Markers("spawn"))
                {
                    string team = m.Get("team", "");
                    var pose = new Pose(m.Position + Vector3.up * 0.05f, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f));
                    if (team == "blue")
                    {
                        _teamSpawns[0].Add(pose);
                    }
                    else if (team == "red")
                    {
                        _teamSpawns[1].Add(pose);
                    }
                    else
                    {
                        _lobbySpawns.Add(pose);
                    }
                }
            }

            if (_lobbySpawns.Count == 0)
            {
                foreach (var p in FallbackLobby)
                {
                    _lobbySpawns.Add(new Pose(p + Vector3.up * 0.05f, Quaternion.Euler(0f, 20f, 0f)));
                }
            }

            for (int team = 0; team < 2; team++)
            {
                var list = _teamSpawns[team];
                if (list.Count == 0)
                {
                    float z = team == 0 ? -1f : 1f;
                    list.Add(new Pose(new Vector3(-2f, 0.05f, z * 5f), Quaternion.Euler(0f, team == 0 ? 0f : 180f, 0f)));
                    list.Add(new Pose(new Vector3(2f, 0.05f, z * 5f), Quaternion.Euler(0f, team == 0 ? 0f : 180f, 0f)));
                    list.Add(new Pose(new Vector3(0f, 0.05f, z * 28f), Quaternion.Euler(0f, team == 0 ? 0f : 180f, 0f)));
                }

                // The two forwards near the centre first, the one far back last.
                list.Sort((a, b) => Mathf.Abs(a.position.z).CompareTo(Mathf.Abs(b.position.z)));
            }
        }

        /// <summary>No scene data: a plain court of the level's size so the game still works (floor, walls, goal boxes).</summary>
        private void BuildFallback(Transform t)
        {
            var wall = RrLook.Solid(new Color(0.85f, 0.87f, 0.9f));
            RrBuild.Box(t, "Court Floor", new Vector3(0f, -0.05f, 0f), new Vector3(44f, 0.1f, 88.5f), RrLook.Solid(RrLook.Grass));
            RrBuild.Box(t, "Lobby Floor", new Vector3(-31f, -0.05f, 0f), new Vector3(18f, 0.1f, 14f), RrLook.Solid(RrLook.Grey));
            RrBuild.Box(t, "Wall E", new Vector3(22.1f, 2f, 0f), new Vector3(0.2f, 4f, 88.5f), wall);
            RrBuild.Box(t, "Wall W N", new Vector3(-22.1f, 2f, 25f), new Vector3(0.2f, 4f, 38.5f), wall);
            RrBuild.Box(t, "Wall W S", new Vector3(-22.1f, 2f, -25f), new Vector3(0.2f, 4f, 38.5f), wall);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * 44.25f;
                RrBuild.Box(t, "End Wall L", new Vector3(-16f, 2f, z), new Vector3(12f, 4f, 0.2f), wall);
                RrBuild.Box(t, "End Wall R", new Vector3(16f, 2f, z), new Vector3(12f, 4f, 0.2f), wall);
                RrBuild.Box(t, "Goal Back", new Vector3(0f, 2.35f, side * 49.8f), new Vector3(20.5f, 4.7f, 0.2f), RrLook.Solid(RrTeams.Color(side < 0 ? 0 : 1)));
                RrBuild.Box(t, "Goal Roof", new Vector3(0f, 4.75f, side * 47f), new Vector3(20.5f, 0.1f, 5.6f), wall);
                RrBuild.Box(t, "Goal Side L", new Vector3(-10.2f, 2.35f, side * 47f), new Vector3(0.2f, 4.7f, 5.6f), wall);
                RrBuild.Box(t, "Goal Side R", new Vector3(10.2f, 2.35f, side * 47f), new Vector3(0.2f, 4.7f, 5.6f), wall);
                RrBuild.Box(t, "Goal Floor", new Vector3(0f, -0.05f, side * 47f), new Vector3(20.5f, 0.1f, 5.6f), RrLook.Solid(RrLook.Grass));
            }

            RrBuild.PointLight(t, "Light", new Vector3(0f, 12f, 0f), Color.white, 60f, 1.2f);
        }

        private Vector3 CentreSpot()
        {
            return new Vector3(0f, BallRadius + 0.05f, 0f);
        }

        private void SetupBall(RrInstance inst)
        {
            GameObject go;
            float radius = BallRadius;
            if (inst != null)
            {
                go = inst.gameObject;
                // CanBePickedUp 0: the soccer ball is never a tool.
                foreach (var g in go.GetComponentsInChildren<Interact.RrGrabbable>(true))
                {
                    Destroy(g);
                }

                var respawn = go.GetComponent<Interact.RrRespawnable>();
                if (respawn != null)
                {
                    Destroy(respawn);
                }

                var sphere = go.GetComponentInChildren<SphereCollider>(true);
                if (sphere != null)
                {
                    sphere.isTrigger = false;
                    var s = sphere.transform.lossyScale;
                    radius = sphere.radius * Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                }
            }
            else
            {
                RrLog.Warn("Soccer", BallPrefab + " did not load, using a plain ball");
                go = RrBuild.Sphere(transform, "Soccer Ball", CentreSpot(), BallRadius, RrLook.Solid(RrLook.White), true, RrLayers.Prop);
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    _fallbackPhysics = new PhysicsMaterial("SoccerBall") { dynamicFriction = 0.3f, staticFriction = 0.2f, bounciness = 0.6f };
                    col.sharedMaterial = _fallbackPhysics;
                }
            }

            var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 0.75f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.15f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var net = go.GetComponent<RrNetBody>() ?? go.AddComponent<RrNetBody>();
            net.LocalPropId = BallPropId;
            Ball = go.AddComponent<RrSoccerBall>();
            Ball.Setup(this, net, rb, radius);
        }

        private void OnDestroy()
        {
            if (_fallbackPhysics != null)
            {
                Destroy(_fallbackPhysics);
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

        private void TeleportLocal(Pose local)
        {
            RrPlayer.Teleport(transform.TransformPoint(local.position), (transform.rotation * local.rotation).eulerAngles.y);
        }

        // ------------------------------------------------------------------ lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            RrNet.On(EvKick, OnKickEvent);
            RrNet.On(EvGoalClaim, OnGoalClaim);
            RrNet.On(EvGoal, OnGoal);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RrNet.Off(EvKick, OnKickEvent);
            RrNet.Off(EvGoalClaim, OnGoalClaim);
            RrNet.Off(EvGoal, OnGoal);
        }

        private void OnStartPressed()
        {
            if (Phase == RrPhase.Lobby)
            {
                RrLog.Info("Soccer", "start pressed (" + Members().Count + " in the room)");
                RequestStart();
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
                        State.Set(IntBall, StateIdle);
                        ParkBall(CentreSpot());
                    }

                    break;
                case RrPhase.RoundOver:
                    RrSound.PlayUi("SR_Buzzer_Long01", 0.8f);
                    break;
                case RrPhase.Lobby:
                    if (_wasPlayer && old != (RrPhase)255)
                    {
                        TeleportLocal(Spawn(Mathf.Abs(RrNet.LocalId)));
                    }

                    _wasPlayer = false;
                    break;
            }

            RrLog.Info("Soccer", "phase " + old + " -> " + now + " (blue " + State.TeamScore(0) + " - red " + State.TeamScore(1) + ")");
        }

        protected override void OnTeamsAssigned()
        {
            if (Phase == RrPhase.Countdown)
            {
                TakeYourPlace();
            }
        }

        /// <summary>Countdown: the local player goes to one of its team's spawn points (the level's Team1/Team2SpawnPoint).</summary>
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

            var spots = _teamSpawns[team];
            if (spots.Count == 0)
            {
                ReadMarkers();
            }

            _wasPlayer = true;
            TeleportLocal(spots[rank % spots.Count]);
            RrLog.Info("Soccer", "local player to " + RrTeams.Name(team) + " spawn " + (rank % spots.Count) + " (" + (RrPlayer.InVr ? "VR" : "screen") + ")");
        }

        protected override void AuthorityRoundStart(int round)
        {
            DropBall();
            RrLog.Info("Soccer", "kick-off: ball dropped from " + KickoffSpot.y + " m (" + Members().Count + " players)");
        }

        protected override void AuthorityPlaying()
        {
            float now = Time.unscaledTime;
            int state = State.Get(IntBall);
            if (state != _seenBallState)
            {
                _seenBallState = state;
                _stateSince = now;
            }

            if (state == StateScored && now - _stateSince >= ScoredSeconds)
            {
                DropBall();
                return;
            }

            // A ball that left the gym (through a gap, or the floor) comes back as a new drop.
            if (Ball != null && now - _stateSince > 1f)
            {
                Vector3 p = transform.InverseTransformPoint(Ball.transform.position);
                if (p.y < -8f || Mathf.Abs(p.x) > 70f || Mathf.Abs(p.z) > 90f)
                {
                    RrLog.Info("Soccer", "ball out of the gym at " + p.ToString("F1") + ": new drop");
                    DropBall();
                }
            }
        }

        protected override void AuthorityBackToLobby()
        {
            State.Set(IntBall, StateIdle);
            ParkBall(CentreSpot());
        }

        /// <summary>Authority: a new kick-off - the ball falls from the level's ball spawn above the centre spot.</summary>
        private void DropBall()
        {
            var s = State;
            s.Set(IntKickoff, s.Get(IntKickoff) + 1);
            s.Set(IntBall, StateLive);
            _seenBallState = StateLive;
            _stateSince = Time.unscaledTime;
            ParkBall(KickoffSpot);
        }

        private void ParkBall(Vector3 local)
        {
            if (Ball != null)
            {
                Ball.Park(transform.TransformPoint(local));
            }
        }

        // ------------------------------------------------------------------ kicks

        /// <summary>The local player kicked the ball (its client now simulates it): everyone hears it.</summary>
        public void LocalKick(float speed, string how)
        {
            RrNet.Send(EvKick, new[] { (byte)Mathf.Clamp(Mathf.RoundToInt(speed * 5f), 0, 255) });
            if (Ball != null && Ball.Kicks <= 3)
            {
                RrLog.Info("Soccer", "kick " + Ball.Kicks + " (" + how + ") at " + speed.ToString("F1") + " m/s (" + (RrPlayer.InVr ? "VR" : "screen") + ")");
            }
        }

        private void OnKickEvent(int sender, byte[] body)
        {
            if (Ball == null || body == null || body.Length < 1)
            {
                return;
            }

            float speed = body[0] / 5f;
            bool heavy = speed >= 10f;
            RrSound.Play(heavy ? "Soccer_HitBallHeavy_01" : (sender == RrNet.LocalId ? "Soccer_HitBallLight_01" : "Soccer_OtherPlayersLightHit_02"), Ball.transform.position,
                heavy ? 0.6f : 0.8f, 1f + Random.Range(-0.08f, 0.08f), 50f);
        }

        // ------------------------------------------------------------------ goals

        /// <summary>The simulating client saw the ball in a goal: claim it (repeated until the kick-off id moves on).</summary>
        public void ClaimGoal(int scorer, int kickoff, Vector3 where)
        {
            _goalClaimKickoff = kickoff;
            _goalClaim = new[] { (byte)(kickoff & 0xFF), (byte)((kickoff >> 8) & 0xFF), (byte)scorer };
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
            int kickoff = body[0] | (body[1] << 8);
            int scorer = body[2] & 1;
            if (kickoff != (s.Get(IntKickoff) & 0xFFFF) || s.Get(IntBall) != StateLive)
            {
                return;
            }

            AddTeamScore(scorer, 1);
            s.Set(IntBall, StateScored);
            RrNet.Send(EvGoal, new[] { (byte)scorer, (byte)s.TeamScore(0), (byte)s.TeamScore(1) });
        }

        private void OnGoal(int sender, byte[] body)
        {
            if (body == null || body.Length < 3)
            {
                return;
            }

            int scorer = body[0] & 1;
            _goals++;
            // The ball went into the goal the other team defends.
            var zone = Goals[1 - scorer];
            Vector3 at = transform.TransformPoint(zone.center);
            RrSound.Play("Soccer_Goal01", at, 1f, 1f, 150f);
            RrAnnouncer.Say("Goal");
            _teamLineAt = Time.unscaledTime + 1.4f;
            _teamLineTeam = scorer;
            RrFx.Spawn("[VFX_Default_Goal]", at, transform.rotation * Quaternion.Euler(0f, scorer == 0 ? 0f : 180f, 0f), 4f);
            RrLog.Info("Soccer", "goal for " + RrTeams.Name(scorer) + ": blue " + body[1] + " - red " + body[2] + " (goal " + _goals + ")");
        }

        // ------------------------------------------------------------------ every frame

        protected override void OnTick(RrInstanceState s)
        {
            float now = Time.unscaledTime;
            if (_teamLineAt > 0f && now >= _teamLineAt)
            {
                _teamLineAt = -1f;
                RrAnnouncer.Say(RrAnnouncer.TeamScores(_teamLineTeam));
            }

            if (_goalClaim != null)
            {
                bool done = now > _goalClaimUntil || (s.Get(IntKickoff) & 0xFFFF) != _goalClaimKickoff || s.Get(IntBall) != StateLive;
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

            if (s.Phase == RrPhase.Playing && now >= _boxCheckAt)
            {
                _boxCheckAt = now + 0.2f;
                KeepOutOfGoalBoxes();
            }

            foreach (var b in _boards)
            {
                if (b != null)
                {
                    b.Custom = s.Phase == RrPhase.Lobby ? "PRESS START" : "";
                }
            }
        }

        /// <summary>The goal boxes are closed to players during a match: a local player inside is put back on the field.</summary>
        private void KeepOutOfGoalBoxes()
        {
            if (!RrPlayer.HasClient && RrPlayer.PlayerObject == null)
            {
                return;
            }

            Vector3 feet = transform.InverseTransformPoint(RrPlayer.FeetPosition);
            for (int i = 0; i < GoalBoxes.Length; i++)
            {
                var box = GoalBoxes[i];
                if (feet.x < box.min.x || feet.x > box.max.x || feet.z < box.min.z || feet.z > box.max.z || feet.y > box.max.y)
                {
                    continue;
                }

                float z = i == 0 ? box.max.z + 0.8f : box.min.z - 0.8f;
                var out_ = new Vector3(feet.x, 0.05f, z);
                RrPlayer.Teleport(transform.TransformPoint(out_), RrPlayer.Yaw);
                _boxCheckAt = Time.unscaledTime + 1.5f;
                RrLog.Info("Soccer", "local player left the " + RrTeams.Name(i) + " goal box (players may not enter it during a match)");
                return;
            }
        }
    }
}
