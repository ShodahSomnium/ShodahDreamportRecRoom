using System.Collections;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Bowling
{
    /// <summary>
    /// Rec Room's Bowling, "Lone Shoe Lanes" (^Bowling, 2018-10-11; rules unchanged since: rr_sports.md §6,
    /// rr_latest_rgames_action.md §9). The room is Rec Room's own level91 (Android build, BowlingAlley.unity) converted with
    /// gtscene jt=1 into Data/Bowling.bytes (the pins' own colliders cut, DN/iter13/sports_tools/bowl_clean.awk) and built by
    /// <see cref="RrJumbotronArea"/>: three lanes (x -2.9 / 0 / 2.93, 1.78 m wide, foul line z 7.85, head pin z -16.5), ball
    /// returns, the seating, the ball shelf, the lounge with the pool table and the vending machine. Scene space is room space.
    ///
    /// One scoring lane, the middle one, for up to 6 bowlers, and two practice lanes (local physics, no scoring). START on the
    /// hologram (the level's (ScoreboardProjector) spot) makes the room's members the bowlers in join order; each bowls a
    /// whole frame in turn, ten frames, standard ten-pin scoring (<see cref="RrBowlingScore"/>); the highest total wins.
    /// Only the bowler whose turn it is can pick up a scoring-lane ball ("other players cannot grab your ball", 2018). The
    /// overhead monitors show the sheet. Outside a match the scoring lane is free practice for anyone.
    ///
    /// Networking: the 4 scoring balls are <see cref="RrNetBody"/> props 0-3; the pins are simulated by the roller's client
    /// and streamed (<see cref="RrBowlingLane"/>). Custom ints: 0 lane state (0 ready, 1 rolling, 2 machine cycle), 1 roll id,
    /// 2 roller (PlayerId + 1), 3 standing mask, 4 turn (bowler | frame &lt;&lt; 4 | ball &lt;&lt; 8), 5 bowler count, 6 winner + 1,
    /// 8-13 the bowlers (PlayerId + 1), 14-31 the six score sheets (3 ints each). Events 93 roll start, 94 roll result (to the
    /// authority), 95 pin stream, 96 roll scored (sounds).
    /// </summary>
    public sealed class RrBowlingRoom : RrGameRoom
    {
        public const string Music = "Bowling_MusicGameplay";
        public const string Art = "SpawnableToolTex_BOWLING_PIN";
        public const string SceneName = "Bowling";
        public const string BallPrefab = "(BowlingBall)";
        public const string PinPrefab = "(SandboxBowlingPin)";

        public const int MaxBowlers = 6;
        public const int ScoringBalls = 4;
        public const int ScoringLane = 1;
        public const float RollTimeout = 20f;

        // ---- events and custom ints ----
        public const byte EvRollStart = 93;
        public const byte EvRollResult = 94;
        public const byte EvPins = 95;
        public const byte EvRollScored = 96;

        public const int IntLane = 0;
        public const int IntRoll = 1;
        public const int IntRoller = 2;
        public const int IntStanding = 3;
        public const int IntTurn = 4;
        public const int IntCount = 5;
        public const int IntWinner = 6;
        public const int IntBowlers = 8;
        public const int IntSheets = 14;

        // ---- layout (level91, measured; room-local = scene space) ----
        public static readonly float[] LaneX = { -2.9f, 0f, 2.93f };

        /// <summary>The level's 14 balls on the shelf behind the seating (y 0.751, z 17.874).</summary>
        private static readonly float[] ShelfX = { -7.208f, -6.374f, -5.655f, -5.204f, -1.363f, -0.694f, -0.328f, 0.327f, 1.092f, 1.383f, 4.813f, 5.133f, 6.094f, 6.956f };

        public const float ShelfY = 0.751f;
        public const float ShelfZ = 17.874f;

        /// <summary>The ball return beside the scoring lane (x -1.44, top 0.786, z 9.36-10.30): where rolled balls come back.</summary>
        public static readonly Vector3 ReturnBase = new Vector3(-1.438f, 0.915f, 9.48f);

        public const float ReturnStep = 0.265f;

        public static readonly Vector3 ConsoleSpot = new Vector3(-5.832f, 0.127f, 11.686f);
        public const float ConsoleYaw = 58f;
        public static readonly Vector3 DoorSpot = new Vector3(0f, 0.373f, 35.3f);
        public const float DoorYaw = 180f;

        /// <summary>The sheet over the approach, in front of the level's overhead monitors (x +-3.84, y 3.8-6.4, z 7.9-9.3).</summary>
        public static readonly Vector3 MonitorSpot = new Vector3(0f, 5.1f, 9.45f);

        public readonly List<RrBowlingBall> Balls = new List<RrBowlingBall>();
        public readonly RrBowlingLane[] Lanes = new RrBowlingLane[3];

        private RrJumbotronArea _area;
        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private RrGameConsole _console;
        private TMPro.TextMeshPro _sheet;
        private float _sheetAt, _grabAt, _returnAt;
        private int _seenLane = -1;
        private float _laneSince;
        private int _seenTurn = -1;
        private int _scoredRolls;

        // ------------------------------------------------------------------ tuning

        protected override int TeamCount
        {
            get { return 0; }
        }

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
            get { return 4f; }
        }

        protected override float MatchOverSeconds
        {
            get { return 10f; }
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

        public int LaneState
        {
            get { return State.Get(IntLane); }
        }

        public int RollId
        {
            get { return State.Get(IntRoll); }
        }

        public int RollerId
        {
            get { return State.Get(IntRoller) - 1; }
        }

        /// <summary>The scoring lane's rack (0 is never a rack: a fresh instance starts with all ten).</summary>
        public int Standing
        {
            get
            {
                int v = State.Get(IntStanding) & RrBowlingScore.FullRack;
                return v == 0 ? RrBowlingScore.FullRack : v;
            }
        }

        public int Turn
        {
            get { return State.Get(IntTurn) & 0xF; }
        }

        public int Frame
        {
            get { return (State.Get(IntTurn) >> 4) & 0xF; }
        }

        public int FrameRoll
        {
            get { return (State.Get(IntTurn) >> 8) & 0x3; }
        }

        public int BowlerCount
        {
            get { return Mathf.Clamp(State.Get(IntCount), 0, MaxBowlers); }
        }

        public int BowlerId(int slot)
        {
            return slot >= 0 && slot < MaxBowlers ? State.Get(IntBowlers + slot) - 1 : -1;
        }

        /// <summary>The player whose turn it is during a match, -1 otherwise.</summary>
        public int CurrentBowlerId
        {
            get { return Phase == RrPhase.Playing && Frame < RrBowlingScore.Frames ? BowlerId(Turn) : -1; }
        }

        private static int SheetAt(int slot)
        {
            return IntSheets + slot * RrBowlingScore.IntsPerSheet;
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
            string raw = RrSceneBank.Text(SceneName);
            if (raw != null)
            {
                _area = new RrJumbotronArea(SceneName, Vector3.zero);
                // Our pins and door stand where the level's do; the pin blockers become our sweepers.
                foreach (var n in new[] { "BowlingAlley_Pin", "ActivityDoor_Frame", "ActivityDoor_Glow", "ActivityDoor_Emit", "ActivityDoor_Left", "ActivityDoor_Right" })
                {
                    _area.Skip.Add(n);
                }

                foreach (float x in LaneX)
                {
                    _area.DropBoxes.Add(new Bounds(new Vector3(x, 0.947f, -16.281f), new Vector3(0.4f, 0.4f, 0.4f)));
                }

                yield return _area.Build(raw, t, _surfaces);
            }
            else
            {
                RrLog.Warn("Bowling", "no scene data \"" + SceneName + "\": building the plain stand-in alley");
                BuildFallback(t);
            }

            if (this == null)
            {
                yield break;
            }

            BuildProgress = 0.6f;
            BuildLanes(t);
            RrPortal.Create(t, RrActivity.RecCenter, DoorSpot, DoorYaw, "REC CENTER");
            Pose consolePose = new Pose(ConsoleSpot, Quaternion.Euler(0f, ConsoleYaw, 0f));
            var holo = _area != null ? _area.Markers("scoreboard") : new List<RrSceneMarker>();
            if (holo.Count > 0)
            {
                consolePose = new Pose(holo[0].Position, Quaternion.Euler(0f, holo[0].Rotation.eulerAngles.y, 0f));
            }

            _console = RrGameConsole.ForGame(this, t, consolePose, new RrGameConsoleSpec
            {
                TeamCount = 0,
                MinPlayers = 1,
                MaxPlayers = MaxBowlers,
                OnStart = id => OnStartPressed(),
                Status = () => Phase == RrPhase.Playing ? NowBowlingLine() : null,
                Subtitle = () => Phase == RrPhase.Lobby ? "10 FRAMES - HIGHEST SCORE WINS" : RrGameConsole.PhaseLine(this),
            });

            _sheet = RrBuild.Sign(t, "Score Sheet", "", MonitorSpot, Quaternion.Euler(0f, 180f, 0f), 1.6f, Color.white, false, 7.4f);
            _sheet.rectTransform.sizeDelta = new Vector2(7.4f, 2.6f);
            _sheet.alignment = TMPro.TextAlignmentOptions.Top;
            _sheet.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            RrBuild.Box(t, "Score Sheet Back", MonitorSpot + new Vector3(0f, -0.05f, -0.04f), new Vector3(7.6f, 2.7f, 0.02f), RrLook.Solid(new Color(0.06f, 0.06f, 0.1f)), false);
            yield return null;

            BuildProgress = 0.7f;
            int waiting = 0;
            for (int l = 0; l < Lanes.Length; l++)
            {
                var lane = Lanes[l];
                for (int i = 0; i < 10; i++)
                {
                    int pin = i;
                    waiting++;
                    RrSpawn.Spawn(PinPrefab, t, lane.Spots[i], Quaternion.identity, inst =>
                    {
                        SetupPin(lane, pin, inst);
                        waiting--;
                    });
                }
            }

            int scoring = 0, left = 0, right = 0;
            for (int k = 0; k < ShelfX.Length; k++)
            {
                float x = ShelfX[k];
                RrBowlingLane lane;
                int index;
                if (Mathf.Abs(x) < 1.2f)
                {
                    if (scoring >= ScoringBalls)
                    {
                        continue;
                    }

                    lane = Lanes[ScoringLane];
                    index = scoring++;
                }
                else if (x < 0f)
                {
                    lane = Lanes[0];
                    index = 10 + left++;
                }
                else
                {
                    lane = Lanes[2];
                    index = 20 + right++;
                }

                var home = new Vector3(x, ShelfY, ShelfZ);
                waiting++;
                RrSpawn.Spawn(BallPrefab, t, home, Quaternion.identity, inst =>
                {
                    SetupBall(lane, index, inst, home);
                    waiting--;
                });
            }

            float until = Time.realtimeSinceStartup + 30f;
            int total = waiting;
            while (waiting > 0 && Time.realtimeSinceStartup < until)
            {
                BuildProgress = 0.7f + 0.28f * (total - waiting) / Mathf.Max(1, total);
                yield return null;
            }

            BuildProgress = 1f;
            RrLog.Info("Bowling", "alley built from " + (_area != null ? "level91 (" + SceneName + ".bytes)" : "the stand-in alley") + ": lanes " + Lanes[0].PinCount + "/" + Lanes[1].PinCount + "/" + Lanes[2].PinCount
                + " pins (" + PinPrefab + "), " + Balls.Count + " balls (" + BallPrefab + ", " + ScoringBalls + " shared on the scoring lane), head pin z " + Lanes[1].HeadZ.ToString("F2") + ", console at " + consolePose.position.ToString("F1"));
        }

        private void BuildLanes(Transform t)
        {
            var marked = new List<Vector3>[3] { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
            if (_area != null)
            {
                foreach (var m in _area.Markers("weapon_pickup"))
                {
                    string set = m.Get("set", "");
                    int lane = set == "BowlingLanePins" ? 1 : set == "PracticeLanePins1" ? 2 : set == "PracticeLanePins2" ? 0 : -1;
                    if (lane >= 0)
                    {
                        marked[lane].Add(m.Position);
                    }
                }
            }

            for (int l = 0; l < 3; l++)
            {
                var go = RrBuild.Group(t, "Lane " + l);
                var lane = go.AddComponent<RrBowlingLane>();
                lane.Room = this;
                lane.Index = l;
                lane.Networked = l == ScoringLane;
                lane.X = LaneX[l];
                lane.SetSpots(marked[l]);
                lane.BuildSweeper();
                Lanes[l] = lane;
            }

            RrLog.Info("Bowling", "pin spots from the level: " + marked[0].Count + " / " + marked[1].Count + " / " + marked[2].Count + " (10 each expected; else the sandbox triangle)");
        }

        /// <summary>No scene data: three plain lanes of the level's size so the game still works.</summary>
        private void BuildFallback(Transform t)
        {
            var wood = RrLook.Solid(new Color(0.86f, 0.7f, 0.55f));
            RrBuild.Box(t, "Floor", new Vector3(0f, -0.05f, 10f), new Vector3(20f, 0.1f, 60f), RrLook.Solid(new Color(0.2f, 0.12f, 0.25f)));
            foreach (float x in LaneX)
            {
                RrBuild.Box(t, "Lane", new Vector3(x, 0.07f, -5.7f), new Vector3(1.776f, 0.114f, 27.1f), wood);
            }

            RrBuild.Box(t, "Approach", new Vector3(0f, 0.07f, 9.85f), new Vector3(9f, 0.114f, 4f), wood);
            RrBuild.Box(t, "Back Wall", new Vector3(0f, 1f, -19.2f), new Vector3(9f, 2f, 0.6f), RrLook.Solid(RrLook.DarkGrey));
            RrBuild.Box(t, "Shelf", new Vector3(0f, 0.55f, ShelfZ + 0.1f), new Vector3(15f, 0.1f, 0.5f), RrLook.Solid(RrLook.DarkWood));
            RrBuild.PointLight(t, "Light", new Vector3(0f, 4f, 0f), Color.white, 40f, 1.2f);
            RrBuild.PointLight(t, "Light Pins", new Vector3(0f, 3f, -15f), Color.white, 15f, 1.2f);
        }

        private void SetupPin(RrBowlingLane lane, int i, RrInstance inst)
        {
            GameObject go;
            if (inst != null)
            {
                go = inst.gameObject;
                var respawn = go.GetComponent<RrRespawnable>();
                if (respawn != null)
                {
                    Destroy(respawn);
                }

                foreach (var g in go.GetComponentsInChildren<RrGrabbable>(true))
                {
                    Destroy(g);
                }

                // The level's pins are the sandbox pin at its 1.5 root scale (0.55 m tall).
                var b = inst.WorldBounds();
                if (b.size.y > 0.05f && (b.size.y < 0.45f || b.size.y > 0.7f))
                {
                    go.transform.localScale *= 0.55f / b.size.y;
                }

                if (lane.Index == 0 && i == 0)
                {
                    RrLog.Info("Bowling", "pin " + PinPrefab + ": " + b.size.ToString("F3") + " m -> " + inst.WorldBounds().size.ToString("F3") + " m");
                }
            }
            else
            {
                go = RrBuild.Cylinder(transform, "Pin", lane.Spots[i] + Vector3.up * 0.275f, 0.07f, 0.55f, RrLook.Solid(RrLook.White), true, RrLayers.Prop);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 1.5f;
                rb.angularDamping = 1f;
            }

            lane.AddPin(i, go);
        }

        private void SetupBall(RrBowlingLane lane, int index, RrInstance inst, Vector3 home)
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
                go = RrBuild.Sphere(transform, "Bowling Ball " + index, home, RrBowlingBall.Radius, RrLook.Solid(RrLook.Teal), true, RrLayers.Prop);
                go.AddComponent<Rigidbody>();
            }

            if (grab == null)
            {
                grab = go.GetComponent<RrGrabbable>() ?? go.AddComponent<RrGrabbable>();
                grab.PickupSound = "Bowling_Pickup01";
            }

            var respawn = go.GetComponent<RrRespawnable>();
            if (respawn != null)
            {
                Destroy(respawn);
            }

            RrNetBody net = null;
            if (lane.Networked)
            {
                net = go.GetComponent<RrNetBody>() ?? go.AddComponent<RrNetBody>();
                net.LocalPropId = index;
            }

            var ball = go.AddComponent<RrBowlingBall>();
            ball.Setup(this, lane, index, grab, net, home);
            ball.ReturnHome();
            lane.Balls.Add(ball);
            Balls.Add(ball);
        }

        // ------------------------------------------------------------------ spawns

        public override Pose Spawn(int index)
        {
            // Behind the approach, facing the pins; the ball returns beside the scoring lane on the left.
            float x = -1.2f + (Mathf.Abs(index) % 4) * 0.8f;
            return new Pose(new Vector3(x, 0.1f, 13.1f), Quaternion.Euler(0f, 180f, 0f));
        }

        // ------------------------------------------------------------------ lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            RrNet.On(EvRollStart, OnRollStart);
            RrNet.On(EvRollResult, OnRollResult);
            RrNet.On(EvPins, OnPins);
            RrNet.On(EvRollScored, OnRollScored);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RrNet.Off(EvRollStart, OnRollStart);
            RrNet.Off(EvRollResult, OnRollResult);
            RrNet.Off(EvPins, OnPins);
            RrNet.Off(EvRollScored, OnRollScored);
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            foreach (var b in Balls)
            {
                if (b != null && b.HeldLocally)
                {
                    RrGrab.Release(b.Grab, Vector3.zero, Vector3.zero);
                }
            }
        }

        private void OnStartPressed()
        {
            if (Phase == RrPhase.Lobby)
            {
                RrLog.Info("Bowling", "start pressed (" + Members().Count + " in the alley)");
                RequestStart();
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!Built || Lanes[ScoringLane] == null)
            {
                return;
            }

            if (IsAuthority)
            {
                AuthorityLane();
            }

            if (Time.unscaledTime >= _grabAt)
            {
                _grabAt = Time.unscaledTime + 0.2f;
                bool playing = Phase == RrPhase.Playing;
                int current = CurrentBowlerId;
                foreach (var b in Lanes[ScoringLane].Balls)
                {
                    if (b != null && b.Grab != null)
                    {
                        b.Grab.CanGrab = !playing || current == RrNet.LocalId || b.HeldLocally;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ phases

        protected override void AuthorityMatchSetup()
        {
            var s = State;
            var members = Members();
            members.Sort();
            int n = Mathf.Min(MaxBowlers, members.Count);
            s.Set(IntCount, n);
            for (int i = 0; i < MaxBowlers; i++)
            {
                s.Set(IntBowlers + i, i < n ? members[i] + 1 : 0);
                RrBowlingScore.Clear(s, SheetAt(i));
                if (i < n)
                {
                    s.SetPlayerScore(members[i], 0);
                }
            }

            SetTurn(0, 0, 0);
            s.Set(IntWinner, 0);
            NewRack();
            RrLog.Info("Bowling", "game set up: " + n + " bowler(s), 10 frames on the scoring lane");
        }

        protected override void AuthorityRoundStart(int round)
        {
            RrLog.Info("Bowling", "game on: " + NameOf(BowlerId(0)) + " bowls first");
        }

        protected override void AuthorityRoundOver(int winner)
        {
            // Every client says its own line (YouWin for the winner).
        }

        protected override void AuthorityBackToLobby()
        {
            // The sheet stays up; the lane is free practice again.
            SetTurn(0, 0, 0);
            NewRack();
        }

        protected override void OnPhase(RrPhase old, RrPhase now)
        {
            if (now == RrPhase.RoundOver)
            {
                int winner = State.Get(IntWinner) - 1;
                if (winner >= 0 && winner == RrNet.LocalId)
                {
                    RrAnnouncer.Say("AmyVO_Generic_YouWin");
                }
                else
                {
                    RrSound.PlayUi("CrowdCheer01", 0.6f);
                }

                RrLog.Info("Bowling", "game over: " + SheetSummary() + (winner >= 0 ? "; winner " + NameOf(winner) : ""));
            }

            RrLog.Info("Bowling", "phase " + old + " -> " + now);
        }

        private void SetTurn(int bowler, int frame, int roll)
        {
            State.Set(IntTurn, (bowler & 0xF) | ((frame & 0xF) << 4) | ((roll & 0x3) << 8));
        }

        /// <summary>Authority: a full new rack after the machine cycle.</summary>
        private void NewRack()
        {
            var s = State;
            s.Set(IntStanding, RrBowlingScore.FullRack);
            s.Set(IntRoller, 0);
            s.Set(IntLane, RrBowlingLane.StateCycle);
            _seenLane = RrBowlingLane.StateCycle;
            _laneSince = Time.unscaledTime;
        }

        // ------------------------------------------------------------------ the scoring lane (authority)

        private void AuthorityLane()
        {
            float now = Time.unscaledTime;
            int lane = LaneState;
            if (lane != _seenLane)
            {
                _seenLane = lane;
                _laneSince = now;
            }

            if (lane == RrBowlingLane.StateRolling && now - _laneSince > RollTimeout)
            {
                RrLog.Info("Bowling", "roll " + RollId + " timed out without a result (the roller left?): counted as nothing down");
                ApplyResult(Standing);
            }
            else if (lane == RrBowlingLane.StateCycle && now - _laneSince >= RrBowlingLane.CycleSeconds)
            {
                State.Set(IntLane, RrBowlingLane.StateReady);
                if (Phase == RrPhase.Playing && Frame >= RrBowlingScore.Frames)
                {
                    int winner = Winner();
                    State.Set(IntWinner, winner + 1);
                    EndRound(winner);
                }
            }

            if (now >= _returnAt)
            {
                _returnAt = now + 0.5f;
                ReturnScoringBalls(now);
                if (Phase == RrPhase.Playing && LaneState == RrBowlingLane.StateReady && Frame < RrBowlingScore.Frames)
                {
                    CheckBowlers();
                }
            }
        }

        /// <summary>A bowler who left loses the turn; nobody left ends the game.</summary>
        private void CheckBowlers()
        {
            var members = Members();
            bool any = false;
            for (int i = 0; i < BowlerCount; i++)
            {
                any |= members.Contains(BowlerId(i));
            }

            if (!any)
            {
                RrLog.Info("Bowling", "every bowler left: game over");
                State.Set(IntWinner, Winner() + 1);
                EndRound(-1);
                return;
            }

            if (!members.Contains(BowlerId(Turn)))
            {
                RrLog.Info("Bowling", "bowler " + Turn + " left: next bowler");
                AdvanceTurn();
            }
        }

        private void AdvanceTurn()
        {
            int n = Mathf.Max(1, BowlerCount);
            int turn = Turn, frame = Frame;
            var members = Members();
            for (int k = 0; k < n * 11; k++)
            {
                turn++;
                if (turn >= n)
                {
                    turn = 0;
                    frame++;
                }

                if (frame >= RrBowlingScore.Frames)
                {
                    SetTurn(0, RrBowlingScore.Frames, 0);
                    return;
                }

                if (members.Contains(BowlerId(turn)))
                {
                    SetTurn(turn, frame, 0);
                    return;
                }
            }

            SetTurn(0, RrBowlingScore.Frames, 0);
        }

        private void OnRollStart(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 2 || LaneState != RrBowlingLane.StateReady)
            {
                return;
            }

            int roll = body[0] | (body[1] << 8);
            if (roll != (RollId & 0xFFFF))
            {
                return;
            }

            if (Phase == RrPhase.Playing && sender != CurrentBowlerId)
            {
                return;
            }

            var s = State;
            s.Set(IntRoll, RollId + 1);
            s.Set(IntRoller, sender + 1);
            s.Set(IntLane, RrBowlingLane.StateRolling);
        }

        private void OnRollResult(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 4 || LaneState != RrBowlingLane.StateRolling || sender != RollerId)
            {
                return;
            }

            int roll = body[0] | (body[1] << 8);
            if (roll != (RollId & 0xFFFF))
            {
                return;
            }

            ApplyResult((body[2] | (body[3] << 8)) & RrBowlingScore.FullRack);
        }

        /// <summary>Authority: the pins still up after the roll - score it, set the next rack, run the machine cycle.</summary>
        private void ApplyResult(int mask)
        {
            var s = State;
            int before = Standing;
            int after = mask & before;
            int knocked = RrBowlingScore.Count(before) - RrBowlingScore.Count(after);
            bool strike = before == RrBowlingScore.FullRack && after == 0;
            bool spare = !strike && after == 0;
            int next;
            int bowler = -1, frame = -1, ball = FrameRoll;
            int roller = RollerId;
            if (Phase == RrPhase.Playing && Frame < RrBowlingScore.Frames && roller >= 0 && roller == BowlerId(Turn))
            {
                bowler = Turn;
                frame = Frame;
                int at = SheetAt(bowler);
                var slots = RrBowlingScore.Read(s, at);
                int slot = RrBowlingScore.SlotOf(frame, ball);
                slots[slot] = knocked;
                RrBowlingScore.Write(s, at, slot, knocked);
                bool done, full;
                int nextRoll;
                RrBowlingScore.Advance(slots, frame, ball, knocked, before, out done, out nextRoll, out full);
                next = full ? RrBowlingScore.FullRack : after;
                s.SetPlayerScore(roller, RrBowlingScore.Total(slots));
                if (done)
                {
                    AdvanceTurn();
                }
                else
                {
                    SetTurn(bowler, frame, nextRoll);
                }
            }
            else if (Phase == RrPhase.Playing)
            {
                // A roll that is not the bowler's (a stale one): the rack stays as it was, the turn does not move.
                next = before;
            }
            else
            {
                // Practice on the scoring lane: first ball, then what is standing, then a new rack.
                if (ball == 0 && after != 0)
                {
                    next = after;
                    SetTurn(Turn, Frame, 1);
                }
                else
                {
                    next = RrBowlingScore.FullRack;
                    SetTurn(Turn, Frame, 0);
                }
            }

            s.Set(IntStanding, next);
            s.Set(IntLane, RrBowlingLane.StateCycle);
            _seenLane = RrBowlingLane.StateCycle;
            _laneSince = Time.unscaledTime;
            byte flags = (byte)((strike ? 1 : 0) | (spare ? 2 : 0));
            RrNet.Send(EvRollScored, new[] { (byte)(bowler + 1), (byte)(frame + 1), (byte)(ball + 1), (byte)knocked, flags });
        }

        /// <summary>Whether a ball should go back (off the approach on the lanes or in the pit and slow, or lost).</summary>
        public bool ShouldReturn(RrBowlingBall ball, RrBowlingLane lane)
        {
            if (ball == null || ball.HeldByAnyone || ball.AtHome)
            {
                return false;
            }

            Vector3 p = transform.InverseTransformPoint(ball.transform.position);
            if (p.y < -2f || Mathf.Abs(p.x) > 40f || p.z < -40f || p.z > 60f)
            {
                return true;
            }

            float speed = ball.SeenVelocity.magnitude;
            bool onLanes = p.z < lane.FoulZ - 0.3f && Mathf.Abs(p.x) < 5.5f && p.y < 1.2f;
            return onLanes && (speed < 0.3f || p.z < lane.BackZ + 1.5f);
        }

        /// <summary>Authority: rolled scoring balls come back onto the ball return (2 s after they stop or reach the pit).</summary>
        private void ReturnScoringBalls(float now)
        {
            var lane = Lanes[ScoringLane];
            if (LaneState == RrBowlingLane.StateRolling || lane.Simulating)
            {
                return;
            }

            foreach (var ball in lane.Balls)
            {
                if (!ShouldReturn(ball, lane))
                {
                    ball.ReturnSince = -1f;
                    continue;
                }

                if (ball.ReturnSince < 0f)
                {
                    ball.ReturnSince = now;
                }
                else if (now - ball.ReturnSince > 2f)
                {
                    ball.HomeLocal = ReturnBase + new Vector3(0f, 0f, ReturnStep * Mathf.Clamp(ball.Index, 0, 3));
                    ball.ReturnHome();
                }
            }
        }

        private int Winner()
        {
            int best = -1, bestScore = -1;
            for (int i = 0; i < BowlerCount; i++)
            {
                int total = RrBowlingScore.Total(RrBowlingScore.Read(State, SheetAt(i)));
                if (total > bestScore)
                {
                    bestScore = total;
                    best = BowlerId(i);
                }
            }

            return best;
        }

        // ------------------------------------------------------------------ every client

        private void OnPins(int sender, byte[] body)
        {
            var lane = Lanes[ScoringLane];
            if (lane != null)
            {
                lane.OnPins(sender, body);
            }
        }

        private void OnRollScored(int sender, byte[] body)
        {
            if (body == null || body.Length < 5 || Lanes[ScoringLane] == null)
            {
                return;
            }

            int knocked = body[3];
            bool strike = (body[4] & 1) != 0;
            bool spare = (body[4] & 2) != 0;
            RollSounds(Lanes[ScoringLane], knocked, strike, spare);
            _scoredRolls++;
            int bowler = body[0] - 1;
            RrLog.Info("Bowling", (bowler >= 0 ? NameOf(BowlerId(bowler)) + " frame " + body[1] + " ball " + body[2] : "practice") + ": " + knocked + " down" + (strike ? " - STRIKE" : spare ? " - SPARE" : ""));
        }

        /// <summary>The crowd and the machine after a roll (Bowling_MegaStrike01 for a strike, SR_Crowd_Aww_01 for a gutter ball).</summary>
        public void RollSounds(RrBowlingLane lane, int knocked, bool strike, bool spare)
        {
            Vector3 at = transform.TransformPoint(lane.Spots[0]);
            if (strike)
            {
                RrSound.Play("Bowling_MegaStrike01", at, 1f, 1f, 60f);
                RrSound.Play("CrowdCheer01", at, 0.6f, 1f, 60f);
            }
            else if (spare)
            {
                RrSound.Play("CrowdCheer01", at, 0.7f, 1f, 60f);
            }
            else if (knocked == 0)
            {
                RrSound.Play("SR_Crowd_Aww_01", at, 0.7f, 1f, 60f);
            }
        }

        protected override void OnTick(RrInstanceState s)
        {
            int turnKey = s.Phase == RrPhase.Playing ? s.Get(IntTurn) & 0xFF : -1;
            if (turnKey != _seenTurn)
            {
                _seenTurn = turnKey;
                if (CurrentBowlerId == RrNet.LocalId && CurrentBowlerId >= 0)
                {
                    RrSound.PlayUi("AttentionWhistle", 0.45f);
                    RrLog.Info("Bowling", "your turn: frame " + (Frame + 1));
                }
            }

            if (_sheet != null && Time.unscaledTime >= _sheetAt)
            {
                _sheetAt = Time.unscaledTime + 0.3f;
                string text = SheetText();
                if (_sheet.text != text)
                {
                    _sheet.text = text;
                }
            }
        }

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

        private string NowBowlingLine()
        {
            if (Frame >= RrBowlingScore.Frames)
            {
                return "LAST FRAME DONE";
            }

            return "NOW BOWLING: " + NameOf(CurrentBowlerId).ToUpperInvariant() + " - FRAME " + (Frame + 1) + ", BALL " + (FrameRoll + 1);
        }

        private string SheetText()
        {
            var sb = new StringBuilder(1024);
            sb.Append("<mspace=0.62em>");
            int n = BowlerCount;
            switch (Phase)
            {
                case RrPhase.Playing:
                    sb.Append(NowBowlingLine());
                    if (Frame == 9 && FrameRoll >= 1)
                    {
                        sb.Append("  - BOWL AGAIN!");
                    }

                    break;
                case RrPhase.Lobby:
                    sb.Append(n > 0 ? "LAST GAME - PRESS START FOR A NEW ONE" : "LONE SHOE LANES - PRESS START TO BOWL");
                    break;
                default:
                    sb.Append(RrGameConsole.PhaseLine(this));
                    break;
            }

            sb.Append('\n');
            if (n == 0)
            {
                sb.Append("\nFree practice on every lane.");
                sb.Append("</mspace>");
                return sb.ToString();
            }

            sb.Append(Pad("", 9));
            for (int f = 0; f < RrBowlingScore.Frames; f++)
            {
                sb.Append(Pad((f + 1).ToString(), 4));
            }

            sb.Append(" TOTAL\n");
            for (int i = 0; i < n; i++)
            {
                var slots = RrBowlingScore.Read(State, SheetAt(i));
                var totals = RrBowlingScore.Totals(slots);
                string name = NameOf(BowlerId(i));
                bool now = Phase == RrPhase.Playing && i == Turn;
                sb.Append(now ? "<color=#FFD24A>" : "");
                sb.Append(Pad(name.Length > 8 ? name.Substring(0, 8) : name, 9));
                for (int f = 0; f < RrBowlingScore.Frames; f++)
                {
                    sb.Append(Pad(RrBowlingScore.Marks(slots, f), 4));
                }

                sb.Append(' ').Append(RrBowlingScore.Total(slots).ToString().PadLeft(4));
                sb.Append(now ? "</color>\n" : "\n");
                sb.Append(Pad("", 9));
                for (int f = 0; f < RrBowlingScore.Frames; f++)
                {
                    sb.Append(Pad(totals[f] >= 0 ? totals[f].ToString() : "", 4));
                }

                sb.Append('\n');
            }

            sb.Append("</mspace>");
            return sb.ToString();
        }

        private static string Pad(string s, int n)
        {
            return s.Length >= n ? s : s + new string(' ', n - s.Length);
        }

        private string SheetSummary()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < BowlerCount; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(NameOf(BowlerId(i))).Append(' ').Append(RrBowlingScore.Total(RrBowlingScore.Read(State, SheetAt(i))));
            }

            return sb.Length > 0 ? sb.ToString() : "no bowlers";
        }
    }
}
