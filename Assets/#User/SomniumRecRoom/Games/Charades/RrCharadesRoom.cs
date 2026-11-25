using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Charades
{
    /// <summary>
    /// 3D Charades in The Ink Space (rr_party.md §1).
    ///
    /// Venue: Rec Room's whole Ink Space is one sandbox prefab, <c>(Charades_InkSpace_MainHull_Sandbox)Variant</c>
    /// (BIG key "84": 20.8 x 7.1 x 23 m, 40 collision boxes, its own lights, posters, the neon sign), spawned as
    /// scenery and lifted 3.24 m so its main hall floor (hull y -3.24) is our y = 0; the stage piece
    /// <c>(Charades_Stage)</c> (key "23f") sits on the raised strip in the middle, couches on the lounge rugs. The
    /// hull's own activity door is hidden and an <see cref="RrPortal"/> back to the Rec Center stands in its place.
    ///
    /// Rules (the game-rule wrappers' fields): Co-op Classic - everyone on one team, 480 s game, 60 s per performer,
    /// performers rotate; the performer presses CORRECT when someone guesses (we use performer-confirms, as the
    /// original: <c>CharadesManager.guessButton</c> is room-assigned), which scores a "Round" and passes the pen on.
    /// Team Lightning - Blue vs Red, 4 team rounds of 90 s alternating, 45 s per performer, a correct guess scores for
    /// the team and deals the performer a new word at once. SKIP deals a new word in both modes. A performer's turn
    /// starts with a short intermission (co-op 4 s, GUESS) while they are moved onto the stage and handed the pen and
    /// card; the timer loop <c>Charades_IndividualTimerLoop</c> (60.00 s, exactly the co-op turn) plays from
    /// 60 - turn length so it ends with the turn; <c>Charades_TeamScoreUp</c> and <c>[VFX_Charades_Goal]</c> on a
    /// correct guess, <c>SFX_Failure_Stereo</c> when time runs out.
    ///
    /// Network ("fake master", networking doc §5.6 and rr_party.md §7.3): the match flow is RrGameRoom's; this
    /// game's ints (custom 0-5) hold the performer and word, the turn and word counters (the ink key), the
    /// game-remaining times at which drawing starts and the turn ends (a second clock derived from the phase timer,
    /// so no shared clock is needed), the last result, and the rotation counters. Requests (start, correct, skip)
    /// go to the authority as event 67 and are repeated every 0.5 s until the state shows them handled; the
    /// authority checks them against the current turn, so repeats are harmless. Ink travels as events 64-68
    /// (<see cref="RrCharadesInk"/>); the pen is a shared prop (<see cref="RrNetBody"/> id 0) only the performer can
    /// pick up (Rec Room's 2020 rule: guessers cannot grab the drawing).
    /// </summary>
    public sealed class RrCharadesRoom : RrGameRoom
    {
        // ---- Rec Room assets (rr_party.md §1.3) ----
        public const string HullPrefab = "(Charades_InkSpace_MainHull_Sandbox)Variant";
        public const string StagePrefab = "(Charades_Stage)";
        public const string PenPrefab = "(CharadesPen)";
        public const string CouchPrefab = "(Sandbox_Couch)";
        public const string InkSpaceMusic = "MUS_BGM_3DCharades2b_Music";
        public const string PianoLoungeMusic = "MUS_BGM_3DCharades2_Music";
        public const string SuccessClip = "Charades_TeamScoreUp";
        public const string TimerLoopClip = "Charades_IndividualTimerLoop";
        public const string ExpiredClip = "SFX_Failure_Stereo";
        public const string SkipClip = "Kiosk_StateChange01";
        public const string GoalFx = "[VFX_Charades_Goal]";

        // ---- measured timings (CharadesCoOpClassic / CharadesTeamLightning game-rule wrappers) ----
        public const float CoopGameSeconds = 480f;
        public const float CoopPerformerSeconds = 60f;
        public const float LightningRoundSeconds = 90f;
        public const float LightningPerformerSeconds = 45f;
        public const int LightningRounds = 4;
        public const float CoopIntermission = 4f;
        public const float RevealSeconds = 4f;

        public const int ModeCoop = 0;
        public const int ModeLightning = 1;

        // ---- events and requests ----
        public const byte EvRequest = 67;
        public const byte ReqStartCoop = 1;
        public const byte ReqStartLightning = 2;
        public const byte ReqCorrect = 3;
        public const byte ReqSkip = 4;

        // ---- results ----
        public const int ResultSolved = 1;
        public const int ResultSkipped = 2;
        public const int ResultTimeout = 3;
        public const int ResultLeft = 4;

        // ---- custom instance ints ----
        private const int KPerf = 0;     // performer PlayerId+1 (16) | word index << 16
        private const int KTurn = 1;     // turn counter (16) | word counter << 16
        private const int KDrawAt = 2;   // game-remaining ms at which drawing starts
        private const int KTurnEnd = 3;  // game-remaining ms at which the performer's turn ends
        private const int KResult = 4;   // result (8) | word index << 8 | result counter << 24
        private const int KRot = 5;      // rotation: co-op (10) | blue << 10 | red << 20

        // ---- layout, room-local (the hull is lifted so the main hall floor is y = 0) ----
        public static readonly Vector3 HullPosition = new Vector3(0f, 3.24f, 0f);
        public static Vector3 StageCentre = new Vector3(2.0f, 0.62f, 4.0f);
        public static Vector3 PerformerMark = new Vector3(2.0f, 0.62f, 4.0f);
        public static float PerformerYaw = 270f;
        public static readonly Vector3 DoorPosition = new Vector3(-3.66f, 1.74f, -11.02f);
        public static readonly float[] RugZ = { 0.9f, 4.0f, 7.0f };
        public const float RugX = -8.8f;

        // ---- built ----
        private RrCharadesInk _ink;
        private RrCharadesPen _penDraw;
        private RrGrabbable _pen;
        private RrNetBody _penNet;
        private Vector3 _penHome;
        private Quaternion _penHomeRot = Quaternion.identity;
        private RrCharadesCard _handCard;
        private RrCharadesCard _podiumCard;
        private RrScoreboard _board;
        private TextMeshPro _status;
        private GameObject _lobbyGroup;
        private GameObject _playGroup;

        /// <summary>The game hologram's floor point before grounding (front corner of the stage, see BuildConsole).</summary>
        public static readonly Vector3 ConsoleSpot = new Vector3(-0.6f, 0.65f, 0.2f);

        private RrGameConsole _console;
        private int _consoleMode;
        private readonly List<Transform> _swatches = new List<Transform>();
        private Light _spot;
        private AudioSource _tick;
        private AudioClip _tickClip;
        private readonly List<Pose> _spawns = new List<Pose>();
        private RrInstance _hull;

        // ---- authority ----
        private readonly HashSet<int> _usedWords = new HashSet<int>();
        private float _penIdleSince = -1f;

        // ---- client ----
        private bool _primed;
        private int _seenTurnRaw = int.MinValue;
        private int _seenResult;
        private bool _wasMine;
        private int _tickTurn = -1;
        private string _reveal;
        private float _revealUntil;
        private float _boardAt;
        private byte _pendKind;
        private int _pendTurn;
        private float _pendFirst;
        private float _pendLast;
        private bool _askedInk;

        /// <summary>The room the local player is in (harness hooks), or null.</summary>
        public static RrCharadesRoom Current { get; private set; }

        public RrCharadesInk Ink
        {
            get { return _ink; }
        }

        public RrGrabbable Pen
        {
            get { return _pen; }
        }

        // ------------------------------------------------------------------ tuning

        protected override int TeamCount
        {
            get { return State.Mode == ModeLightning ? 2 : 0; }
        }

        protected override int MinPlayersToStart
        {
            get { return 1; }
        }

        protected override bool AutoStart
        {
            get { return false; }
        }

        protected override float RoundSeconds
        {
            get { return State.Mode == ModeLightning ? LightningRoundSeconds : CoopGameSeconds; }
        }

        protected override int Rounds
        {
            get { return State.Mode == ModeLightning ? LightningRounds : 1; }
        }

        protected override float RoundOverSeconds
        {
            get { return 4f; }
        }

        protected override string LobbyMusic
        {
            get { return InkSpaceMusic; }
        }

        // ------------------------------------------------------------------ state helpers

        public int Mode
        {
            get { return State.Mode; }
        }

        public int PerformerId
        {
            get { return (State.Get(KPerf) & 0xFFFF) - 1; }
        }

        public int WordIndex
        {
            get { return (State.Get(KPerf) >> 16) & 0x7FFF; }
        }

        public string Word
        {
            get { return RrCharadesWords.Get(WordIndex); }
        }

        public int TurnNumber
        {
            get { return State.Get(KTurn) & 0xFFFF; }
        }

        public int WordNumber
        {
            get { return (State.Get(KTurn) >> 16) & 0x7FFF; }
        }

        /// <summary>The ink key: turn and word, so every new word starts a clean drawing.</summary>
        public int DrawKey
        {
            get { return (((TurnNumber & 0x3FF) << 6) | (WordNumber & 0x3F)) & 0xFFFF; }
        }

        private float DrawAt
        {
            get { return State.Get(KDrawAt) * 0.001f; }
        }

        private float TurnEnd
        {
            get { return State.Get(KTurnEnd) * 0.001f; }
        }

        public bool TurnActive
        {
            get { return Phase == RrPhase.Playing && PerformerId >= 0; }
        }

        /// <summary>The performer may draw now (after the intermission, before the turn's end).</summary>
        public bool DrawingNow
        {
            get { return TurnActive && State.TimeRemaining <= DrawAt + 0.05f && TurnLeft > 0f; }
        }

        /// <summary>Seconds left in the performer's turn.</summary>
        public float TurnLeft
        {
            get { return Mathf.Max(0f, State.TimeRemaining - TurnEnd); }
        }

        public bool LocalIsPerformer
        {
            get { return TurnActive && PerformerId == RrNet.LocalId; }
        }

        private int ActiveTeam
        {
            get { return Mode == ModeLightning ? State.Round % 2 : 0; }
        }

        // ------------------------------------------------------------------ build

        public override IEnumerator Build()
        {
            BuildProgress = 0f;
            RrLog.Info("Charades", "building The Ink Space at " + Origin);
            yield return RrPortal.Prepare();
            BuildProgress = 0.1f;

            // the venue
            RrInstance hull = null;
            bool done = false;
            RrSpawn.Spawn(HullPrefab, transform, HullPosition, Quaternion.identity, i => { hull = i; done = true; }, RrSpawnOptions.Scenery());
            yield return WaitFor(() => done, 90f);
            _hull = hull;
            if (hull != null)
            {
                var door = hull.Find("[ActivityDoor]");
                if (door != null)
                {
                    door.gameObject.SetActive(false);
                }

                int lights = 0;
                var sources = new List<RrLightGrid.Source>();
                foreach (var l in hull.GetComponentsInChildren<Light>(true))
                {
                    l.shadows = LightShadows.None;
                    lights++;
                    if (l.enabled && (l.type == LightType.Point || l.type == LightType.Spot))
                    {
                        var s = RrLightGrid.FromLight(l, transform);
                        s.Unity = l;
                        RrLightGrid.ConfigureUnity(l, s);
                        sources.Add(s);
                    }
                }

                // Iteration 9: the hull's Rec Room shaders read their lights from Rec Room's light grid, not URP's per-object
                // lights, so without it the hall was lit by the sun and ambient only (as the cached Rec Center was).
                var grid = RrLightGrid.Attach(gameObject, transform, "Charades_InkSpace", default(Bounds), sources);
                var b = hull.WorldBounds();
                RrLog.Info("Charades", "Ink Space hull spawned: bounds " + b.size.ToString("F2") + " centre " + (b.center - Origin).ToString("F2") + ", " + lights + " lights (shadows off)"
                    + (grid != null ? ", " + sources.Count + " in Rec Room's light grid" : ""));
            }
            else
            {
                RrLog.Warn("Charades", "Ink Space hull " + HullPrefab + " did not load: building a plain hall");
                BuildFallbackHall();
            }

            BuildProgress = 0.45f;
            yield return null;

            // the stage piece on the middle strip
            RrInstance stage = null;
            done = false;
            RrSpawn.Spawn(StagePrefab, transform, StageCentre, Quaternion.identity, i => { stage = i; done = true; }, RrSpawnOptions.Scenery());
            yield return WaitFor(() => done, 30f);
            if (stage != null)
            {
                var sb = stage.WorldBounds();
                var want = transform.TransformPoint(StageCentre);
                stage.transform.position += new Vector3(want.x - sb.center.x, want.y - sb.max.y, want.z - sb.center.z);
                RrLog.Info("Charades", "stage spawned: " + sb.size.ToString("F2") + ", top at " + StageCentre.y.ToString("F2"));
            }
            else
            {
                RrBuild.Box(transform, "Stage", new Vector3(StageCentre.x, StageCentre.y * 0.5f, StageCentre.z), new Vector3(3.2f, StageCentre.y, 9.5f), RrLook.Solid(RrLook.DarkWood));
            }

            BuildProgress = 0.55f;
            yield return null;
            Physics.SyncTransforms();
            PerformerMark = Ground(PerformerMark);

            // couches on the lounge rugs, facing the stage
            for (int r = 0; r < RugZ.Length; r++)
            {
                RrInstance couch = null;
                done = false;
                var p = Ground(new Vector3(RugX, 0.65f, RugZ[r]));
                RrSpawn.Spawn(CouchPrefab, transform, p, Quaternion.Euler(0f, 90f, 0f), i => { couch = i; done = true; }, RrSpawnOptions.Scenery());
                yield return WaitFor(() => done, 20f);
                if (couch != null)
                {
                    var cb = couch.WorldBounds();
                    couch.transform.position += Vector3.up * (transform.TransformPoint(p).y - cb.min.y);
                }
            }

            BuildProgress = 0.65f;
            yield return null;

            // ink canvas at the performer's mark
            var canvas = RrBuild.Group(transform, "Charades Ink", PerformerMark);
            _ink = canvas.AddComponent<RrCharadesInk>();

            BuildPodium();
            BuildBoard();
            BuildProgress = 0.75f;
            yield return null;

            // the Charades Pen
            RrInstance pen = null;
            done = false;
            RrSpawn.Spawn(PenPrefab, transform, _penHome, _penHomeRot, i => { pen = i; done = true; }, RrSpawnOptions.Prop());
            yield return WaitFor(() => done, 30f);
            SetupPen(pen);
            BuildProgress = 0.85f;
            yield return null;

            // cards, spotlight, timer loop, exit door
            _handCard = RrCharadesCard.Build(transform, "Charades Hand Card", 1f);
            _handCard.Follow = true;
            _handCard.gameObject.SetActive(false);

            var spotGo = new GameObject("Performer Spotlight");
            spotGo.transform.SetParent(transform, false);
            spotGo.transform.localPosition = PerformerMark + new Vector3(-2.0f, 5.0f, 0f);
            _spot = spotGo.AddComponent<Light>();
            _spot.type = LightType.Spot;
            _spot.color = new Color(1f, 0.93f, 0.8f);
            _spot.intensity = 25f;
            _spot.range = 12f;
            _spot.spotAngle = 32f;
            _spot.shadows = LightShadows.None;
            _spot.enabled = false;

            _tick = gameObject.AddComponent<AudioSource>();
            _tick.playOnAwake = false;
            _tick.loop = false;
            _tick.spatialBlend = 0f;
            _tick.volume = 0.35f;
            if (RrAssets.Instance != null)
            {
                RrAssets.Instance.Load<AudioClip>(TimerLoopClip, 83, c => _tickClip = c);
            }

            var portal = RrPortal.Create(transform, RrActivity.RecCenter, DoorPosition, 0f);
            if (portal == null)
            {
                RrLog.Warn("Charades", "exit door to the Rec Center could not be made");
            }

            Physics.SyncTransforms();
            ComputeSpawns();
            BuildConsole();
            BuildProgress = 1f;
            RrLog.Info("Charades", "The Ink Space built: stage mark " + PerformerMark.ToString("F2") + ", pen " + (_pen != null ? "ready" : "MISSING") + ", " + RrCharadesWords.All.Length + " words");
        }

        private static IEnumerator WaitFor(Func<bool> done, float timeout)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (!done() && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        /// <summary>Drops a room-local point onto the walkable collision below it (hull boxes are layer 23).</summary>
        private Vector3 Ground(Vector3 local)
        {
            var world = transform.TransformPoint(local);
            RaycastHit hit;
            if (Physics.Raycast(world + Vector3.up * 2.5f, Vector3.down, out hit, 6f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore))
            {
                return transform.InverseTransformPoint(hit.point);
            }

            return local;
        }

        private void BuildFallbackHall()
        {
            RrBuild.RoomShell(transform, "Hall", new Vector3(0f, 3f, 4f), new Vector3(20f, 6f, 16f), RrLook.Solid(RrLook.Wood), RrLook.Solid(RrLook.Red), RrLook.Solid(RrLook.DarkGrey));
            RrBuild.PointLight(transform, "Light", new Vector3(0f, 5f, 4f), new Color(1f, 0.9f, 0.75f), 20f, 1.5f);
        }

        private Vector3 Forward
        {
            get { return Quaternion.Euler(0f, PerformerYaw, 0f) * Vector3.forward; }
        }

        private Vector3 Right
        {
            get { return Quaternion.Euler(0f, PerformerYaw, 0f) * Vector3.right; }
        }

        /// <summary>The performer's podium: CORRECT / SKIP / undo / clear / 12 colours, the start buttons, the word card.</summary>
        private void BuildPodium()
        {
            var pos = PerformerMark + Right * 1.35f + Forward * 0.25f;
            var toPerformer = PerformerMark - pos;
            toPerformer.y = 0f;
            var rot = Quaternion.LookRotation(toPerformer.normalized, Vector3.up);
            var podium = RrBuild.Group(transform, "Charades Podium", pos, rot).transform;
            RrBuild.Box(podium, "Body", new Vector3(0f, 0.48f, -0.1f), new Vector3(1.1f, 0.96f, 0.55f), RrLook.Solid(RrLook.DarkWood));
            RrBuild.Box(podium, "Trim", new Vector3(0f, 0.72f, 0.18f), new Vector3(1.14f, 0.06f, 0.04f), RrLook.Solid(RrLook.Orange), false);

            var panel = RrBuild.Group(podium, "Panel", new Vector3(0f, 1.1f, -0.08f), Quaternion.Euler(30f, 0f, 0f)).transform;
            RrBuild.Box(panel, "Top", new Vector3(0f, -0.02f, 0f), new Vector3(1.24f, 0.04f, 0.7f), RrLook.Solid(RrLook.DarkGrey));

            _playGroup = RrBuild.Group(panel, "Play").gameObject;
            var play = _playGroup.transform;
            Button(play, "Correct", new Vector3(0.36f, 0f, -0.2f), RrLook.Green, 0.24f, "CORRECT!", () => Request(ReqCorrect));
            Button(play, "Skip", new Vector3(0.02f, 0f, -0.2f), RrLook.Orange, 0.17f, "SKIP", () => Request(ReqSkip));
            Button(play, "Undo", new Vector3(-0.25f, 0f, -0.2f), RrLook.Grey, 0.1f, "UNDO", () => { if (LocalIsPerformer && _penDraw != null) { _penDraw.Undo(); } });
            Button(play, "Clear", new Vector3(-0.46f, 0f, -0.2f), RrLook.Red, 0.1f, "CLEAR", () => { if (LocalIsPerformer && _ink != null) { _ink.ClearAll(); } });
            for (int i = 0; i < RrCharadesInk.Palette.Length; i++)
            {
                int index = i;
                float x = 0.45f - (i % 6) * 0.18f;
                float z = 0.1f + (i / 6) * 0.11f;
                var sw = RrPressable.Create(play, "Colour " + i, new Vector3(x, 0f, z), Quaternion.identity, RrCharadesInk.Palette[i], 0.08f, () => PickColour(index));
                _swatches.Add(sw.transform);
            }

            // The lobby's start buttons moved to the game hologram (BuildConsole): the podium's lobby face stays empty.
            _lobbyGroup = RrBuild.Group(panel, "Lobby").gameObject;

            // the big word card behind the panel, facing the performer (read from its -z side: turned round)
            _podiumCard = RrCharadesCard.Build(podium, "Charades Podium Card", 2.6f);
            _podiumCard.transform.localPosition = new Vector3(0f, 1.62f, -0.36f);
            _podiumCard.transform.localRotation = Quaternion.Euler(-12f, 180f, 0f);

            _penHome = podium.localPosition + rot * new Vector3(0.62f, 1.0f, 0.05f);
            _penHomeRot = rot;
            RrBuild.Box(podium, "Pen Tray", new Vector3(0.62f, 0.96f, 0.05f), new Vector3(0.14f, 0.04f, 0.3f), RrLook.Solid(RrLook.Grey));
            RrLog.Info("Charades", "podium at " + pos.ToString("F2") + " facing the performer");
        }

        /// <summary>
        /// Rec Room's game hologram (<see cref="RrGameConsole"/>) with the two game modes on its mode toggle and START.
        /// The Ink Space level has no projector (its start is the performer's [Charades_Button]); the classic Charades
        /// hall stands two at its side walls facing the middle. The open hologram is 4.36 x 3.27 m and the hull has
        /// pillars in the audience area (the first harness run put it behind one), so the spot is searched: floor
        /// points on the audience side of the stage (x -4 to 0, z -7 to 10, every 0.5 m), nearest first to a point
        /// 4.5 m ahead and 2.5 m right of the arrival spots, each facing the arrival spots, where the hologram and the
        /// projector touch no hull collider (<see cref="RrGameConsole.IsClear"/>), the arrival eye sees it, it stands
        /// 3.5-9 m from the arrival spots and within 60 degrees of their facing, 1.2 m from each, it does not cut the
        /// audience's line to the performer, and it is seen whole (both sides, both heights: run 2 stood it with a
        /// hull post in front of its left third: the posts have no colliders, so FindSpot also tests their renderer bounds).
        /// The mode picked on the toggle is this client's until START: the start request carries it to the authority
        /// (<see cref="Request"/>), which writes the mode into the instance state.
        /// </summary>
        private void BuildConsole()
        {
            var viewer = Vector3.zero;
            foreach (var s in _spawns)
            {
                viewer += s.position;
            }

            viewer = _spawns.Count > 0 ? viewer / _spawns.Count : new Vector3(-4.85f, 0.65f, 3.4f);
            var ideal = viewer + new Vector3(4.5f, 0f, -2.5f);
            var candidates = new List<Vector3>();
            for (float x = -4f; x <= 0.01f; x += 0.5f)
            {
                for (float z = -7f; z <= 10.01f; z += 0.5f)
                {
                    candidates.Add(Ground(new Vector3(x, 0.65f, z)));
                }
            }

            candidates.Sort((a, b) => Flat(a - ideal).CompareTo(Flat(b - ideal)));
            var performer = new Vector2(PerformerMark.x, PerformerMark.z);
            var eye = new Vector2(viewer.x, viewer.z);
            Pose pose;
            string note;
            bool found = RrGameConsole.FindSpot(transform, viewer, candidates, p =>
            {
                float d = Flat(p.position - viewer);
                if (d < 3.5f || d > 9f)
                {
                    return false;
                }

                // In the arrival view: within 60 degrees of the spots' facing (+X, yaw 90).
                Vector3 dir = p.position - viewer;
                dir.y = 0f;
                if (Vector3.Angle(dir, Vector3.right) > 60f)
                {
                    return false;
                }

                foreach (var s in _spawns)
                {
                    if (Flat(p.position - s.position) < 1.2f)
                    {
                        return false;
                    }
                }

                // The hologram's footprint (its width at its centre) must not cross the audience's line to the stage mark.
                Vector3 c = p.position + p.rotation * RrGameConsole.ProjectedCentre;
                Vector3 r = p.rotation * Vector3.right * (RrGameConsole.ProjectedSize.x * 0.5f);
                return !Crosses(new Vector2(c.x - r.x, c.z - r.z), new Vector2(c.x + r.x, c.z + r.z), eye, performer);
            }, out pose, out note);
            if (!found)
            {
                pose = new Pose(Ground(ConsoleSpot), Quaternion.Euler(0f, PerformerYaw, 0f));
            }

            var spec = new RrGameConsoleSpec
            {
                MinPlayers = 1,
                TeamCount = 2,
                Modes = new[] { "Co-op Classic", "Team Lightning" },
                ModeIndex = () => Phase == RrPhase.Lobby ? _consoleMode : State.Mode == ModeLightning ? 1 : 0,
                OnMode = i => _consoleMode = i,
                OnStart = id => Request(_consoleMode == 1 ? ReqStartLightning : ReqStartCoop),
            };
            _console = RrGameConsole.ForGame(this, transform, pose, spec);
            RrLog.Info("Charades", "game hologram at " + pose.position.ToString("F2") + ", yaw " + pose.rotation.eulerAngles.y.ToString("0") + ": " + (found ? note : "fallback (" + note + ")"));
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        /// <summary>Do the segments ab and cd cross (2D)?</summary>
        private static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(d - c, a - c);
            float d2 = Cross(d - c, b - c);
            float d3 = Cross(b - a, c - a);
            float d4 = Cross(b - a, d - a);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        private static float Cross(Vector2 u, Vector2 v)
        {
            return u.x * v.y - u.y * v.x;
        }

        private void Button(Transform parent, string name, Vector3 pos, Color cap, float diameter, string label, Action onPressed)
        {
            RrPressable.Create(parent, name, pos, Quaternion.identity, cap, diameter, onPressed);
            var sign = RrBuild.Sign(parent, name + " Label", label, pos + new Vector3(0f, 0.003f, diameter * 0.5f + 0.045f), Quaternion.Euler(90f, 180f, 0f), 0.42f, Color.white, true, 0.36f);
            sign.enableAutoSizing = true;
            sign.fontSizeMin = 0.2f;
            sign.fontSizeMax = 0.42f;
            sign.rectTransform.sizeDelta = new Vector2(0.36f, 0.08f);
        }

        /// <summary>The wall board above the stage: title, clock, score, and the performer / reveal line.</summary>
        private void BuildBoard()
        {
            var pos = PerformerMark + Forward * -1.3f + new Vector3(0f, 3.1f, 0f);
            var rot = Quaternion.Euler(0f, PerformerYaw, 0f);
            _board = RrScoreboard.Create(transform, pos, rot, "3D CHARADES", 2, 3.6f);
            var statusPanel = RrBuild.Group(_board.transform, "Status").transform;
            statusPanel.localPosition = new Vector3(0f, -1.15f, 0f);
            RrBuild.Box(statusPanel, "Panel", new Vector3(0f, 0f, -0.06f), new Vector3(3.6f, 0.55f, 0.1f), RrLook.Solid(new Color(0.12f, 0.12f, 0.15f)), false);
            _status = RrBuild.Sign(statusPanel, "Line", "", Vector3.zero, Quaternion.Euler(0f, 180f, 0f), 2.4f, RrCharadesCard.GoldenOrange, true, 3.4f);
            _status.enableAutoSizing = true;
            _status.fontSizeMin = 1f;
            _status.fontSizeMax = 2.4f;
            _status.rectTransform.sizeDelta = new Vector2(3.4f, 0.5f);
        }

        private void SetupPen(RrInstance pen)
        {
            if (pen == null)
            {
                RrLog.Warn("Charades", "the Charades Pen " + PenPrefab + " did not load: no drawing this visit");
                return;
            }

            foreach (var line in pen.GetComponentsInChildren<LineRenderer>(true))
            {
                line.enabled = false; // the laser pointer
            }

            var menu = pen.Find("ToolMenu");
            if (menu != null)
            {
                menu.gameObject.SetActive(false);
            }

            if (pen.GetComponent<Rigidbody>() == null)
            {
                var rb = pen.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
            }

            _penNet = pen.gameObject.AddComponent<RrNetBody>();
            _penNet.LocalPropId = 0;
            _pen = RrTools.MakeTool(pen);
            if (_pen == null)
            {
                _pen = pen.gameObject.AddComponent<RrGrabbable>();
            }

            _pen.CanGrab = false;
            var respawn = pen.GetComponent<RrRespawnable>();
            if (respawn != null)
            {
                respawn.SetHome();
            }

            _penDraw = pen.gameObject.AddComponent<RrCharadesPen>();
            _penDraw.Grab = _pen;
            _penDraw.Ink = _ink;
            _penDraw.Tip = pen.Find("ExtrusionPoint");
            _penDraw.CanDraw = () => DrawingNow && LocalIsPerformer;
            RrLog.Info("Charades", "Charades Pen ready: tip " + (_penDraw.Tip != null ? "ExtrusionPoint" : "fallback (0,0.036,0.152)") + ", lock on pickup " + _pen.LockOnPickup);
        }

        private void ComputeSpawns()
        {
            _spawns.Clear();
            for (int i = 0; i < 8; i++)
            {
                var p = Ground(new Vector3(-6.2f + (i % 4) * 0.9f, 0.65f, 2.2f + (i / 4) * 2.4f));
                _spawns.Add(new Pose(p + Vector3.up * 0.02f, Quaternion.Euler(0f, 90f, 0f)));
            }
        }

        public override Pose Spawn(int index)
        {
            if (_spawns.Count == 0)
            {
                return new Pose(new Vector3(-5f, 0.7f, 3f), Quaternion.Euler(0f, 90f, 0f));
            }

            return _spawns[Mathf.Abs(index) % _spawns.Count];
        }

        // ------------------------------------------------------------------ lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            RrNet.On(EvRequest, OnRequest);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RrNet.Off(EvRequest, OnRequest);
        }

        public override void OnLocalEnter()
        {
            base.OnLocalEnter();
            Current = this;
            _primed = false;
            _askedInk = false;
            _wasMine = false;
            _seenTurnRaw = int.MinValue;
        }

        public override void OnLocalExit()
        {
            StopPerforming(false);
            if (_tick != null)
            {
                _tick.Stop();
            }

            if (Current == this)
            {
                Current = null;
            }

            base.OnLocalExit();
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            RrNet.Off(EvRequest, OnRequest);
        }

        // ------------------------------------------------------------------ requests (any client -> authority)

        /// <summary>Asks the authority for something; repeated every 0.5 s (for 4 s) until the state shows it done.</summary>
        public void Request(byte kind)
        {
            if ((kind == ReqCorrect || kind == ReqSkip) && !LocalIsPerformer)
            {
                RrLog.Info("Charades", "only the performer can press " + (kind == ReqCorrect ? "CORRECT" : "SKIP"));
                return;
            }

            if ((kind == ReqStartCoop || kind == ReqStartLightning) && Phase != RrPhase.Lobby)
            {
                return;
            }

            if (IsAuthority)
            {
                HandleRequest(RrNet.LocalId, kind, State.Get(KTurn));
                return;
            }

            _pendKind = kind;
            _pendTurn = State.Get(KTurn);
            _pendFirst = Time.unscaledTime;
            _pendLast = -10f;
            SendPending();
        }

        private void SendPending()
        {
            _pendLast = Time.unscaledTime;
            var body = new byte[5];
            body[0] = _pendKind;
            body[1] = (byte)(_pendTurn & 0xFF);
            body[2] = (byte)((_pendTurn >> 8) & 0xFF);
            body[3] = (byte)((_pendTurn >> 16) & 0xFF);
            body[4] = (byte)((_pendTurn >> 24) & 0xFF);
            RrNet.Send(EvRequest, body, true);
        }

        private void TickPending()
        {
            if (_pendKind == 0)
            {
                return;
            }

            bool start = _pendKind == ReqStartCoop || _pendKind == ReqStartLightning;
            bool seen = start ? Phase != RrPhase.Lobby : State.Get(KTurn) != _pendTurn || !TurnActive;
            if (seen || Time.unscaledTime - _pendFirst > 4f)
            {
                _pendKind = 0;
                return;
            }

            if (Time.unscaledTime - _pendLast >= 0.5f)
            {
                SendPending();
            }
        }

        private void OnRequest(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 5)
            {
                return;
            }

            int turn = body[1] | (body[2] << 8) | (body[3] << 16) | (body[4] << 24);
            HandleRequest(sender, body[0], turn);
        }

        /// <summary>Authority: a start / correct / skip request (stale repeats fail the checks and do nothing).</summary>
        private void HandleRequest(int sender, byte kind, int turnRaw)
        {
            if (!IsAuthority)
            {
                return;
            }

            switch (kind)
            {
                case ReqStartCoop:
                case ReqStartLightning:
                    if (Phase != RrPhase.Lobby)
                    {
                        return;
                    }

                    State.Mode = kind == ReqStartLightning ? ModeLightning : ModeCoop;
                    RrLog.Info("Charades", "player " + sender + " starts " + (State.Mode == ModeLightning ? "Team Lightning (4 x 90 s, 45 s per performer)" : "Co-op Classic (480 s, 60 s per performer)"));
                    StartCountdown();
                    return;
                case ReqCorrect:
                case ReqSkip:
                    if (!DrawingNow || turnRaw != State.Get(KTurn) || sender != PerformerId)
                    {
                        return;
                    }

                    if (kind == ReqCorrect)
                    {
                        AddTeamScore(ActiveTeam, 1);
                        SetResult(ResultSolved);
                        RrLog.Info("Charades", "solved \"" + Word + "\" (" + (Mode == ModeLightning ? RrTeams.Name(ActiveTeam) + " " : "") + "score " + State.TeamScore(ActiveTeam) + ")");
                        if (Mode == ModeLightning)
                        {
                            NewWord();
                        }
                        else
                        {
                            StartTurn();
                        }
                    }
                    else
                    {
                        SetResult(ResultSkipped);
                        RrLog.Info("Charades", "skipped \"" + Word + "\"");
                        NewWord();
                    }

                    return;
            }
        }

        // ------------------------------------------------------------------ authority flow

        protected override void AuthorityMatchSetup()
        {
            _usedWords.Clear();
            State.Set(KPerf, 0);
            State.Set(KDrawAt, 0);
            State.Set(KTurnEnd, 0);
            State.Set(KRot, 0);
        }

        protected override void AuthorityRoundStart(int round)
        {
            StartTurn();
        }

        protected override void AuthorityPlaying()
        {
            int perf = PerformerId;
            if (perf < 0)
            {
                if (State.TimeRemaining > 1f)
                {
                    StartTurn();
                }

                return;
            }

            if (!Members().Contains(perf))
            {
                RrLog.Info("Charades", "the performer left: next turn");
                SetResult(ResultLeft);
                StartTurn();
                return;
            }

            if (TurnEnd > 0f && State.TimeRemaining <= TurnEnd)
            {
                SetResult(ResultTimeout);
                StartTurn();
            }

            // a dropped pen goes back to the podium after 5 s (or when nobody performs)
            if (_pen != null && Time.frameCount % 30 == 0)
            {
                AuthorityPenCleanup();
            }
        }

        protected override void AuthorityRoundOver(int winner)
        {
            if (PerformerId >= 0)
            {
                SetResult(ResultTimeout);
            }

            State.Set(KPerf, 0);
            if (Mode == ModeLightning && State.Round + 1 >= Rounds)
            {
                int blue = State.TeamScore(0);
                int red = State.TeamScore(1);
                AnnounceAll(blue == red ? RrAnnouncer.Tie : RrAnnouncer.TeamWins(blue > red ? 0 : 1));
            }
        }

        protected override void AuthorityBackToLobby()
        {
            State.Set(KPerf, 0);
            AuthorityPenCleanup();
        }

        private void StartTurn()
        {
            var members = Members();
            members.Sort();
            if (members.Count == 0)
            {
                State.Set(KPerf, 0);
                return;
            }

            int rot = State.Get(KRot);
            int slot = Mode == ModeLightning ? 1 + ActiveTeam : 0;
            int counter = (rot >> (slot * 10)) & 0x3FF;
            var pool = members;
            if (Mode == ModeLightning)
            {
                pool = new List<int>();
                foreach (var id in members)
                {
                    if (RrTeams.TeamOf(id) == ActiveTeam)
                    {
                        pool.Add(id);
                    }
                }

                if (pool.Count == 0)
                {
                    pool = members;
                }
            }

            int perf = pool[counter % pool.Count];
            counter = (counter + 1) & 0x3FF;
            rot = (rot & ~(0x3FF << (slot * 10))) | (counter << (slot * 10));
            State.Set(KRot, rot);

            int word = PickWord();
            int turn = (TurnNumber + 1) & 0xFFFF;
            State.Set(KPerf, ((perf + 1) & 0xFFFF) | (word << 16));
            State.Set(KTurn, turn);
            float now = State.TimeRemaining;
            float drawAt = Mathf.Max(0f, now - (Mode == ModeLightning ? 0f : CoopIntermission));
            float length = Mode == ModeLightning ? LightningPerformerSeconds : CoopPerformerSeconds;
            State.Set(KDrawAt, Mathf.RoundToInt(drawAt * 1000f));
            State.Set(KTurnEnd, Mathf.RoundToInt(Mathf.Max(0f, drawAt - length) * 1000f));
            RrLog.Info("Charades", "turn " + turn + ": performer " + perf + (Mode == ModeLightning ? " (" + RrTeams.Name(ActiveTeam) + ")" : "") + ", " + length + " s, word #" + word);
        }

        private void NewWord()
        {
            int word = PickWord();
            State.Set(KPerf, ((PerformerId + 1) & 0xFFFF) | (word << 16));
            State.Set(KTurn, TurnNumber | (((WordNumber + 1) & 0x7FFF) << 16));
        }

        private int PickWord()
        {
            int n = RrCharadesWords.All.Length;
            if (_usedWords.Count >= n - 1)
            {
                _usedWords.Clear();
            }

            int w = UnityEngine.Random.Range(0, n);
            for (int tries = 0; tries < 50 && _usedWords.Contains(w); tries++)
            {
                w = UnityEngine.Random.Range(0, n);
            }

            _usedWords.Add(w);
            return w;
        }

        private void SetResult(int code)
        {
            int seq = ((State.Get(KResult) >> 24) + 1) & 0x7F;
            State.Set(KResult, (code & 0xFF) | ((WordIndex & 0xFFFF) << 8) | (seq << 24));
        }

        private void AuthorityPenCleanup()
        {
            if (_pen == null)
            {
                return;
            }

            bool held = _pen.IsHeld || (_penNet != null && _penNet.HolderId >= 0);
            bool away = (_pen.transform.position - transform.TransformPoint(_penHome)).sqrMagnitude > 0.09f;
            if (held || !away)
            {
                _penIdleSince = -1f;
                return;
            }

            if (_penIdleSince < 0f)
            {
                _penIdleSince = Time.unscaledTime;
            }

            if (Time.unscaledTime - _penIdleSince > 5f || !TurnActive)
            {
                ResetPen();
                _penIdleSince = -1f;
            }
        }

        private void ResetPen()
        {
            if (_pen == null)
            {
                return;
            }

            var pos = transform.TransformPoint(_penHome);
            var rot = transform.rotation * _penHomeRot;
            if (_penNet != null && _penNet.Networked)
            {
                if (_penNet.LocallyControlled)
                {
                    _penNet.ResetTo(pos, rot);
                }

                return;
            }

            _pen.transform.SetPositionAndRotation(pos, rot);
            var rb = _pen.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------ every client

        protected override void OnPhase(RrPhase old, RrPhase now)
        {
            switch (now)
            {
                case RrPhase.Playing:
                    RrMusic.Stop(1.5f); // the performer's ticking loop carries the round
                    break;
                case RrPhase.MatchOver:
                    RrMusic.Play(InkSpaceMusic);
                    break;
            }
        }

        protected override void OnTeamsAssigned()
        {
            if (Mode == ModeLightning && RrTeams.LocalTeam >= 0)
            {
                RrAnnouncer.Say(RrTeams.LocalTeam == 0 ? RrAnnouncer.BlueTeam : RrAnnouncer.RedTeam);
            }
        }

        protected override void OnTick(RrInstanceState state)
        {
            if (_ink == null)
            {
                return;
            }

            bool mine = LocalIsPerformer;
            int perf = TurnActive ? PerformerId : -1;
            int turnRaw = State.Get(KTurn);
            int result = State.Get(KResult);

            if (!_primed)
            {
                _primed = true;
                _seenResult = result;
            }

            // results: sound, effect and the word revealed
            if (((result >> 24) & 0x7F) != ((_seenResult >> 24) & 0x7F))
            {
                _seenResult = result;
                ShowResult(result & 0xFF, (result >> 8) & 0xFFFF);
            }

            _ink.SetDrawing(TurnActive ? DrawKey : 0, perf, mine);

            if (turnRaw != _seenTurnRaw || mine != _wasMine)
            {
                _seenTurnRaw = turnRaw;
                if (mine && !_wasMine)
                {
                    BecomePerformer();
                }
                else if (!mine && _wasMine)
                {
                    StopPerforming(true);
                }

                _wasMine = mine;
            }

            // a late joiner asks the performer for the drawing once
            if (!_askedInk && !mine && DrawingNow)
            {
                _askedInk = true;
                if (Time.realtimeSinceStartup - RrTravel.ArrivedAt < 30f && TurnLeft < (Mode == ModeLightning ? LightningPerformerSeconds : CoopPerformerSeconds) - 2f)
                {
                    _ink.RequestResend();
                }
            }

            if (_pen != null)
            {
                _pen.CanGrab = mine;
            }

            TickTimerLoop();
            TickPending();
            TickSpot(perf, mine);

            if (_handCard != null)
            {
                bool show = mine;
                if (_handCard.gameObject.activeSelf != show)
                {
                    _handCard.gameObject.SetActive(show);
                }

                if (show)
                {
                    _handCard.SetWord(Word);
                }
            }

            if (_podiumCard != null)
            {
                _podiumCard.SetWord(mine ? Word : Phase == RrPhase.Lobby ? "Press Start!" : RrCharadesCard.Hidden);
            }

            bool lobby = Phase == RrPhase.Lobby || Phase == RrPhase.MatchOver;
            if (_lobbyGroup != null && _lobbyGroup.activeSelf != lobby)
            {
                _lobbyGroup.SetActive(lobby);
                _playGroup.SetActive(!lobby);
            }

            if (Time.unscaledTime >= _boardAt)
            {
                _boardAt = Time.unscaledTime + 0.2f;
                UpdateBoard(perf, mine);
            }
        }

        private void BecomePerformer()
        {
            RrLog.Info("Charades", "you are the performer: \"" + Word + "\"");
            _askedInk = true;
            var mark = transform.TransformPoint(PerformerMark);
            float yaw = transform.eulerAngles.y + PerformerYaw;
            RrPlayer.Teleport(mark + Vector3.up * 0.02f, yaw, () =>
            {
                if (_pen != null && LocalIsPerformer && !_pen.IsHeld)
                {
                    var hand = RrHand.Right;
                    if (hand != null)
                    {
                        _pen.CanGrab = true;
                        RrGrab.Give(_pen, hand);
                    }
                }
            });
            RrSound.PlayUi(RrPressable.DefaultSound, 0.6f);
        }

        private void StopPerforming(bool moveOff)
        {
            if (_pen != null && _pen.IsHeld)
            {
                _pen.ForceRelease();
                ResetPen();
            }

            if (_handCard != null)
            {
                _handCard.gameObject.SetActive(false);
            }

            if (!moveOff)
            {
                return;
            }

            var feet = RrPlayer.FeetPosition;
            var mark = transform.TransformPoint(PerformerMark);
            if (new Vector2(feet.x - mark.x, feet.z - mark.z).magnitude < 2.2f)
            {
                var pose = WorldSpawn(Mathf.Abs(RrNet.LocalId) % 8);
                RrPlayer.Teleport(pose.position, pose.rotation.eulerAngles.y);
            }
        }

        private void ShowResult(int code, int word)
        {
            string w = RrCharadesWords.Get(word).ToUpperInvariant();
            var at = transform.TransformPoint(PerformerMark + Forward * 1.0f + Vector3.up * 1.4f);
            switch (code)
            {
                case ResultSolved:
                    RrSound.Play(SuccessClip, at, 1f, 1f, 60f);
                    RrFx.Spawn(GoalFx, at, Quaternion.identity, 1.5f);
                    _reveal = "CORRECT!  " + w;
                    break;
                case ResultSkipped:
                    RrSound.Play(SkipClip, at, 0.6f, 1f, 40f);
                    _reveal = "SKIPPED:  " + w;
                    break;
                default:
                    RrSound.Play(ExpiredClip, at, 0.8f, 1f, 60f);
                    _reveal = (code == ResultLeft ? "PERFORMER LEFT:  " : "TIME'S UP!  It was ") + w;
                    break;
            }

            _revealUntil = Time.unscaledTime + RevealSeconds;
            _boardAt = 0f;
        }

        private void TickTimerLoop()
        {
            if (_tick == null)
            {
                return;
            }

            if (Phase != RrPhase.Playing)
            {
                _tickTurn = -1;
                if (_tick.isPlaying)
                {
                    _tick.Stop();
                }

                return;
            }

            bool drawing = DrawingNow;
            if (drawing && _tickTurn != TurnNumber && _tickClip != null)
            {
                _tickTurn = TurnNumber;
                _tick.clip = _tickClip;
                _tick.volume = 0.35f * RrSound.Volume;
                _tick.time = Mathf.Clamp(_tickClip.length - TurnLeft, 0f, Mathf.Max(0f, _tickClip.length - 0.1f));
                _tick.Play();
            }
            else if (!drawing && _tick.isPlaying)
            {
                _tick.Stop();
            }
        }

        private void TickSpot(int perf, bool mine)
        {
            if (_spot == null)
            {
                return;
            }

            bool on = perf >= 0;
            if (_spot.enabled != on)
            {
                _spot.enabled = on;
            }

            if (!on)
            {
                return;
            }

            Vector3 target = transform.TransformPoint(PerformerMark);
            if (mine)
            {
                target = RrPlayer.FeetPosition;
            }
            else
            {
                var remote = RrRemotePlayers.Get(perf);
                if (remote != null && remote.Root != null)
                {
                    target = remote.Root.position;
                }
            }

            _spot.transform.rotation = Quaternion.LookRotation(target + Vector3.up * 1.1f - _spot.transform.position);
        }

        private string NickOf(int id)
        {
            if (id == RrNet.LocalId)
            {
                return RrPlayer.Nick;
            }

            var p = RrNet.Presence(id);
            if (p.Valid && !string.IsNullOrEmpty(p.Nick))
            {
                return p.Nick;
            }

            var remote = RrRemotePlayers.Get(id);
            return remote != null && !string.IsNullOrEmpty(remote.Nick) ? remote.Nick : "Player " + id;
        }

        private void UpdateBoard(int perf, bool mine)
        {
            if (_board == null)
            {
                return;
            }

            bool lightning = Mode == ModeLightning;
            _board.Title = Phase == RrPhase.Lobby ? "3D CHARADES" : lightning ? "TEAM LIGHTNING" : "CO-OP CLASSIC";
            var title = _board.transform.Find("Title");
            if (title != null)
            {
                var t = title.GetComponent<TextMeshPro>();
                if (t != null && t.text != _board.Title)
                {
                    t.text = _board.Title;
                }
            }

            if (Phase == RrPhase.Lobby)
            {
                _board.Custom = "PRESS START ON THE HOLOGRAM";
            }
            else if (!lightning)
            {
                _board.Custom = "WORDS SOLVED: " + State.TeamScore(0);
            }
            else
            {
                _board.Custom = null;
            }

            string line;
            if (_reveal != null && Time.unscaledTime < _revealUntil)
            {
                line = _reveal;
            }
            else
            {
                switch (Phase)
                {
                    case RrPhase.Lobby:
                        line = "DRAW IT!  GUESS IT!";
                        break;
                    case RrPhase.Countdown:
                        line = lightning ? RrTeams.Name(ActiveTeam).ToUpperInvariant() + " TEAM, GET READY!" : "GET READY!";
                        break;
                    case RrPhase.Playing:
                        if (perf < 0)
                        {
                            line = "";
                        }
                        else if (!DrawingNow && TurnLeft > 0f)
                        {
                            line = "UP NEXT: " + (mine ? "YOU!" : NickOf(perf).ToUpperInvariant());
                        }
                        else
                        {
                            float left = TurnLeft;
                            string who = mine ? "YOU ARE" : NickOf(perf).ToUpperInvariant() + " IS";
                            line = (lightning ? RrTeams.Name(ActiveTeam).ToUpperInvariant() + ": " : "") + who + " DRAWING  " + (int)(left / 60f) + ":" + ((int)(left % 60f)).ToString("00");
                        }

                        break;
                    case RrPhase.RoundOver:
                        line = lightning ? "SWITCHING TEAMS" : "TIME'S UP!";
                        break;
                    default:
                        if (lightning)
                        {
                            int blue = State.TeamScore(0);
                            int red = State.TeamScore(1);
                            line = blue == red ? "TIE GAME  " + blue + " - " + red : (blue > red ? "BLUE" : "RED") + " TEAM WINS  " + Mathf.Max(blue, red) + " - " + Mathf.Min(blue, red);
                        }
                        else
                        {
                            line = "YOU SOLVED " + State.TeamScore(0) + " WORDS!";
                        }

                        break;
                }
            }

            if (_status != null && _status.text != line)
            {
                _status.text = line;
            }

            for (int i = 0; i < _swatches.Count; i++)
            {
                float s = _penDraw != null && _penDraw.Colour == i ? 1.3f : 1f;
                _swatches[i].localScale = new Vector3(s, 1f, s);
            }
        }

        private void PickColour(int index)
        {
            if (!LocalIsPerformer || _penDraw == null)
            {
                return;
            }

            _penDraw.Colour = (byte)index;
            _boardAt = 0f;
        }

        // ------------------------------------------------------------------ harness

        /// <summary>Harness: skips the countdown / intermission waits (authority only).</summary>
        internal void TestSkipWaits()
        {
            if (!IsAuthority)
            {
                return;
            }

            if (Phase == RrPhase.Countdown && State.TimeRemaining > 0.2f)
            {
                State.SetTimer(0.05f);
            }
            else if (Phase == RrPhase.Playing && PerformerId >= 0 && !DrawingNow)
            {
                State.Set(KDrawAt, Mathf.RoundToInt(State.TimeRemaining * 1000f));
            }
        }

        /// <summary>Harness: the state in one line.</summary>
        internal string Describe()
        {
            return "phase " + Phase + ", mode " + (Mode == ModeLightning ? "lightning" : "co-op") + ", performer " + PerformerId + ", word \"" + Word + "\", turn " + TurnNumber + "/" + WordNumber +
                   ", turn left " + TurnLeft.ToString("F1") + " s, drawing " + DrawingNow + ", scores " + State.TeamScore(0) + "/" + State.TeamScore(1) +
                   ", ink " + (_ink != null ? _ink.StrokeCount + " strokes " + _ink.PointCount + " points, sent " + _ink.SentEvents + " recv " + _ink.ReceivedEvents : "none") +
                   ", pen " + (_pen != null ? (_pen.IsHeld ? "held" : "free") : "none");
        }
    }
}
