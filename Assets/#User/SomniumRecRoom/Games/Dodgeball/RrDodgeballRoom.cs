using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Dodgeball
{
    /// <summary>
    /// Rec Room's Dodgeball gym (rr_sports.md §2, rebuild plan §9.1). Iteration 16: the room is Rec Room's own level11
    /// (Android build) converted with gtscene jt=1 into Data/Dodgeball.bytes (DN/iter16/dodge_paddle.md) and built by
    /// <see cref="RrJumbotronArea"/>; scene space is room space. The level gives the gym (26 x 36 m, bleachers on both
    /// long sides), the raised 9.1 x 18.3 m court (drawn floor y 0.436, lines, the red centre band), the 2026 crash-pad
    /// barriers behind each back line, the pre-game corner with its 8 spawn points and (ScoreboardProjector), each team's
    /// court and sideline spawn points (Team1 = blue on -z, sideline at +x; Team2 = red on +z, sideline at -x), the kill
    /// zones just outside the lines and the practice standees; the REC CENTER door replaces the level's door nearest the
    /// pre-game corner (the other seven stay as scenery). Without the data (RrSceneBank.Text null) the old iteration-1
    /// stand-in gym is built (a 9 x 16 m court in a 16 x 22 x 9 m hall). Everything the rules test (lines, the court's
    /// edges, barriers, the hall's box, floor heights) comes from the gym that was built. The balls are Rec Room's own
    /// (DodgeballBall), three on the centre line (the level's spots x -2.5, 0, +2.5) when a round begins.
    ///
    /// Rules: Rec Room's FINAL version (June 2026; rr_latest_rgames_action.md §5), with the bundle's
    /// [DodgeballManager] scoreName "Round" and defaultScoreToWin 3. 2-6 players (1 v 1 up to 3 v 3) in two
    /// auto-sorted teams, blue on -Z and red on +Z. A live throw that hits an opponent puts only that player out (to
    /// their team's bench until the round ends); a ball that has already hit someone is dead, so it never puts out a
    /// second player ("Dodgeballs no longer count as out when they hit another player", 2025-01-14). A catch makes
    /// the ball dead and puts NOBODY out ("finally, no more catch-outs!", 2025-01-14; 2021-2024 a catch outed the
    /// thrower - <see cref="CatchOutsThrower"/> keeps that rule as an option, off). No source has a catch bringing a
    /// teammate back, so nobody comes back before the next round. Crossing the centre line or leaving the court is out
    /// (ours: Rec Room's notes only mention a 2016 penalty for leaving the court). The last team standing wins the
    /// round, 3 rounds win the match. Round start Whistle01, round over Horn01, hits dodgeball_PlayerHit_01 +
    /// [VFX_Default_HitParticles], the out player hears "Out" + OutSFX, the thrower "Hit" + HitSound01, a catch
    /// Catch01, music DodgeBaller.
    ///
    /// Court (2026-01-22 map change, "small barriers behind each side to catch dodgeballs rolling by"): the level's
    /// crash pads (two 3.7 m pieces each side of a 1.3 m gap, 0.45-0.74 m behind the back line); the stand-in builds a
    /// low padded barrier across each end, 0.9 m behind the back line (<see cref="BarrierZ"/>; height and spot GUESSES).
    /// Every gym collider carries Rec Room's DodgeballCourt_phys numbers
    /// (<see cref="CourtPhysics"/>): without them the ball's bounceCombine Minimum met Unity's default bounciness 0
    /// and the balls died on the floor instead of bouncing at 0.6.
    ///
    /// Networking ("fake master", somnium_networking.md §5): phase, round and round wins are
    /// <see cref="RrGameRoom"/>'s instance ints; this game's custom ints are 0 = out mask over the participant
    /// slots, 1 = flags (bit 0 solo practice) | round tag &lt;&lt; 8, 2 = participant count, 4..15 = participants
    /// (PlayerId + 1 | team &lt;&lt; 20), snapshotted by the authority when a round starts playing. Claims go to the
    /// authority (events 64 hit, 65 catch, 66 line) and are repeated every 0.3 s until the verdict (67, broadcast)
    /// or the out mask shows them; the authority holds a hit for 150 ms so that a catch of the same throw id wins.
    /// Offline the local player is the authority and every event loops back.
    ///
    /// Alone (one team only) the round never ends: it is practice against two cardboard standees on the red half (the
    /// level's own practice standee spots).
    /// A second player arriving ends practice so the lobby's auto-start (MinPlayers 2 in the registry) can start a
    /// 1 v 1: Rec Room's own minimum is the same 2 players, so small Somnium groups need nothing lowered.
    /// </summary>
    public sealed class RrDodgeballRoom : RrGameRoom
    {
        public const string Music = "DodgeBaller";
        public const string Art = "GiftBox_DodgeBall_Tex|8,276,380,268";
        public const string BallPrefab = "(DodgeballBall)";

        // ---- measured / research values (rr_sports.md §1.5, §2) ----
        public const int RoundsToWin = 3;
        public const int BallCount = 3;

        /// <summary>
        /// ThrowDodgeballToolBehaviorConfig throwSpeed. Since 2021-04-15 every screen-mode throw leaves at this speed:
        /// "Charging your throw will now have no impact on the dodgeball's launch speed. It merely allows you to hold
        /// it." The config's minChargeDuration 0.1 / maxChargeDuration 0.5 s only drive the wind-up now.
        /// </summary>
        public const float ScreenThrowSpeed = 14f;

        public const float MaxThrowSpeed = 15f;
        public const float GravityCompensation = 7f;
        public const float AssistDistance = 9f;
        public const float AssistAngle = 30f;
        public const float CatchWindow = 0.15f;

        public const string SceneName = "Dodgeball";

        // ---- level11 layout (measured, DN/iter16/dodge_paddle.md; scene space = room space; +Z = red half) ----

        /// <summary>The court lines' outer edges (side lines x +-4.511, back lines z +-9.074) and its drawn floor.</summary>
        public const float LevelCourtHalfX = 4.557f;
        public const float LevelCourtHalfZ = 9.129f;
        public const float LevelCourtY = 0.436f;

        /// <summary>The level's kill zones start here (RightKillzone x &gt; 4.6, FrontKillzone z &gt; 9.2 ...).</summary>
        public const float LevelKillX = 4.6f;
        public const float LevelKillZ = 9.2f;

        /// <summary>The gym's walls (x +-13.1, z +-18.1), floor (0.356) and ceiling (12.52).</summary>
        public const float LevelHallHalfX = 13.1f;
        public const float LevelHallHalfZ = 18.1f;
        public const float LevelHallFloor = 0.356f;
        public const float LevelHallTop = 12.5f;

        /// <summary>The end walls' inner face (C boxes; the drawn band at 17.917) and the boards' height on its solid band.</summary>
        public const float LevelEndWallZ = 17.90f;
        public const float LevelEndBoardY = 3.25f;

        /// <summary>The crash-pad barriers: centre z +-9.875 (inner face 9.58), x 0.65..4.35 each side of the middle gap.</summary>
        public const float LevelBarrierZ = 9.875f;
        public const float LevelBarrierInnerZ = 9.58f;
        public const float LevelBarrierHalfX = 4.35f;

        private static readonly float[] LevelBallX = { -2.5f, 0f, 2.5f };

        /// <summary>The level's (ScoreboardProjector) in the pre-game corner (used when the kit reports no marker).</summary>
        public static readonly Vector3 LevelConsoleSpot = new Vector3(2.79f, 0.356f, -11.81f);

        public const float LevelConsoleYaw = 242.5f;

        /// <summary>The level's exit door (ActivityDoor_Frame at (-5.669, 0.342, -17.908)) on the -z wall, facing into the gym.</summary>
        public static readonly Vector3 LevelDoorSpot = new Vector3(-5.669f, 0.342f, -17.84f);

        public const float LevelDoorYaw = 0f;

        /// <summary>Measured spawn points, used when the kit reports no marker: pre-game SpawnPoint1-8 (facing the projector), each
        /// team's court spawns (Team1 blue -z, Team2 red +z) and sideline spawns (blue at +x, red at -x).</summary>
        private static readonly Vector4[] LevelLobby =
        {
            new Vector4(-10.73f, 0.356f, -13.88f, 73.6f), new Vector4(-8f, 0.356f, -16.12f, 26.9f), new Vector4(-11.98f, 0.356f, -12.89f, 75.5f), new Vector4(-10.95f, 0.356f, -16.07f, 45f),
            new Vector4(-6.37f, 0.356f, -16.73f, 0.7f), new Vector4(-9.4f, 0.356f, -14.95f, 43f), new Vector4(-9.48f, 0.356f, -17.11f, 34.2f), new Vector4(-12f, 0.356f, -14.64f, 59f),
        };

        private static readonly Vector3[][] LevelCourtSpawns =
        {
            new[] { new Vector3(0f, 0.486f, -6f), new Vector3(-2.5f, 0.486f, -6f), new Vector3(2.5f, 0.486f, -6f) },
            new[] { new Vector3(0f, 0.486f, 6f), new Vector3(-2.5f, 0.486f, 6f), new Vector3(2.5f, 0.486f, 6f) },
        };

        private static readonly Vector3[][] LevelSidelineSpawns =
        {
            new[] { new Vector3(5.5f, 0.356f, -2f), new Vector3(5.5f, 0.356f, -3.25f), new Vector3(5.5f, 0.356f, -4.5f) },
            new[] { new Vector3(-5.5f, 0.356f, 2f), new Vector3(-5.5f, 0.356f, 3.25f), new Vector3(-5.5f, 0.356f, 4.5f) },
        };

        /// <summary>The level's upright practice standees on the red half ([PracticeMale_Cutout] (1), (Standee_Female01)), both facing -z.</summary>
        private static readonly Vector3[] LevelStandees = { new Vector3(2.49f, 0.436f, 6.74f), new Vector3(-2.435f, 0.436f, 3.04f) };

        // ---- stand-in layout (no scene data; GUESS sizes from rr_sports.md §2.3; +Z = red half) ----
        public const float CourtHalfX = 4.5f;
        public const float CourtHalfZ = 8f;
        public const float HallHalfX = 8f;
        public const float HallHalfZ = 11f;
        public const float HallHeight = 9f;
        public const float LineTolerance = 0.25f;
        public const float LineGrace = 1.5f;
        public const float RestRespawn = 3f;
        public const float ScreenCatchReach = 2.5f;

        // ---- the 2026 barriers behind each back line (GUESS sizes: "small barriers", ~0.5 m in the research) ----
        /// <summary>Centre of each barrier: 0.9 m behind the back line, inside the 1 m "left the court" tolerance so a
        /// player fetching a ball stopped by it is never out (its inner face is 0.8 m behind the line).</summary>
        public const float BarrierZ = CourtHalfZ + 0.9f;
        public const float BarrierHalfX = CourtHalfX + 0.5f;
        public const float BarrierHeight = 0.5f;
        public const float BarrierThickness = 0.2f;

        /// <summary>
        /// Catching a live throw puts the thrower out: Rec Room's rule only from 2021 ("rely on catch-outs",
        /// 2021-07-09) to 2025-01-14 ("no more catch-outs!"). The final game, and this default, is false: a catch
        /// makes the ball dead and nobody is out.
        /// </summary>
        public static bool CatchOutsThrower = false;

        /// <summary>
        /// Rec Room's DodgeballCourt_phys (REF2 PhysicMats; the level's own copy is in the encrypted sharedassets11):
        /// dynamic / static friction 0.6 / 0.6, bounciness 0.6, both combines Average. The install's DodgeballBall_phys
        /// (ART #-6279935958218146749) is 0.6 / 0.6 / 0.6 with friction Maximum and bounce Minimum, so a ball on this
        /// court bounces at min(0.6, 0.6) = 0.6 and grips at 0.6; on Unity's default material (bounciness 0) it bounced
        /// at min(0.6, 0) = 0.
        /// </summary>
        public static PhysicsMaterial CourtPhysics
        {
            get
            {
                if (_courtPhysics == null)
                {
                    _courtPhysics = new PhysicsMaterial("DodgeballCourt_phys")
                    {
                        dynamicFriction = 0.6f, staticFriction = 0.6f, bounciness = 0.6f,
                        frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Average,
                    };
                }

                return _courtPhysics;
            }
        }

        /// <summary>
        /// The install's DodgeballBall_phys numbers (see <see cref="CourtPhysics"/>), for a ball whose collider came
        /// without its material or the plain fallback ball. REF2's Studio copy has bounceCombine 1 (Multiply) instead
        /// of the install's 2 (Minimum); the install is the final build and is what loads (Minimum: 0.6 on this
        /// court, where Multiply would give 0.36).
        /// </summary>
        public static PhysicsMaterial BallPhysics
        {
            get
            {
                if (_ballPhysics == null)
                {
                    _ballPhysics = new PhysicsMaterial("DodgeballBall_phys")
                    {
                        dynamicFriction = 0.6f, staticFriction = 0.6f, bounciness = 0.6f,
                        frictionCombine = PhysicsMaterialCombine.Maximum, bounceCombine = PhysicsMaterialCombine.Minimum,
                    };
                }

                return _ballPhysics;
            }
        }

        private static PhysicsMaterial _courtPhysics, _ballPhysics;

        // ---- events (>= 64) and custom ints ----
        public const byte EvHit = 64;
        public const byte EvCatch = 65;
        public const byte EvLine = 66;
        public const byte EvVerdict = 67;
        public const byte RequestEndPractice = 10;

        public const int IntOut = 0;
        public const int IntFlags = 1;
        public const int IntCount = 2;
        public const int IntSlots = 4;
        public const int MaxSlots = 12;

        public const byte KindHit = 1;
        public const byte KindCatch = 2;
        public const byte KindLine = 3;
        public const byte KindLeft = 4;

        private static readonly float[] BallX = { -3f, 0f, 3f };

        public readonly List<RrDodgeball> Balls = new List<RrDodgeball>();
        public readonly List<RrDodgeballTarget> Targets = new List<RrDodgeballTarget>();
        private readonly List<RrScoreboard> _boards = new List<RrScoreboard>();
        private readonly List<TMPro.TextMeshPro> _infos = new List<TMPro.TextMeshPro>();

        /// <summary>The game hologram's floor point and facing (its +Z toward the arrival row; see BuildConsole).</summary>
        public static readonly Vector3 ConsoleSpot = new Vector3(6.3f, 0f, -8.95f);

        public const float ConsoleYaw = -60f;

        /// <summary>The arrival row turns toward the hologram (the level's pregame spawns face their projector).</summary>
        public const float ArrivalYaw = 80f;

        private RrGameConsole _console;

        // ---- the gym that was built (level11 or the stand-in); spawns, line checks and ball housekeeping use these ----
        private RrJumbotronArea _area;
        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private bool _level;
        private float _courtX = CourtHalfX, _courtZ = CourtHalfZ, _courtY;
        private float _outX = CourtHalfX + 0.5f, _outZ = CourtHalfZ + 1f;
        private float _hallX = HallHalfX, _hallZ = HallHalfZ, _hallFloor, _hallTop = HallHeight;
        private float _barrierZ = BarrierZ, _barrierX = BarrierHalfX;
        private float[] _ballX = BallX;
        private readonly List<Pose> _lobbySpawns = new List<Pose>();
        private readonly List<Pose>[] _courtSpawns = { new List<Pose>(), new List<Pose>() };
        private readonly List<Pose>[] _benchSpawns = { new List<Pose>(), new List<Pose>() };

        /// <summary>The court's drawn floor (room-local y): balls rest on it.</summary>
        public float CourtFloor
        {
            get { return _courtY; }
        }

        // authority
        private sealed class Pending
        {
            public int Key, Ball, ThrowId, Victim, Thrower;
            public float At;
        }

        private readonly List<Pending> _pending = new List<Pending>();
        private readonly Dictionary<int, byte[]> _decided = new Dictionary<int, byte[]>();

        /// <summary>Harness: fake opponents that count as present members.</summary>
        internal readonly HashSet<int> TestMembers = new HashSet<int>();

        // client
        private sealed class Claim
        {
            public byte Type;
            public byte[] Body;
            public int Key;
            public int Victim;
            public float Next, Until;
        }

        private readonly List<Claim> _claims = new List<Claim>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private bool _myOut;
        private bool _lineClaimed;
        private bool _frozen;
        private bool _wasParticipant;
        private float _playingSince = -1f;
        private float _boardAt;
        private int _verdicts;
        private bool _entered;

        // ------------------------------------------------------------------ tuning

        protected override float CountdownSeconds
        {
            get { return 3f; }
        }

        protected override float RoundSeconds
        {
            get { return 0f; }
        }

        protected override int Rounds
        {
            get { return RoundsToWin * 2 + 3; }
        }

        protected override float RoundOverSeconds
        {
            get { return 4f; }
        }

        protected override float MatchOverSeconds
        {
            get { return 8f; }
        }

        protected override string PlayMusic
        {
            get { return Music; }
        }

        // ------------------------------------------------------------------ participants

        public int LocalTeam
        {
            get { return RrTeams.LocalTeam; }
        }

        public int SlotCount
        {
            get { return Mathf.Clamp(State.Get(IntCount), 0, MaxSlots); }
        }

        public int SlotId(int slot)
        {
            return (State.Get(IntSlots + slot) & 0xFFFFF) - 1;
        }

        public int SlotTeam(int slot)
        {
            return (State.Get(IntSlots + slot) >> 20) & 3;
        }

        public int SlotOf(int playerId)
        {
            int n = SlotCount;
            for (int i = 0; i < n; i++)
            {
                if (SlotId(i) == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool SlotOut(int slot)
        {
            return slot >= 0 && (State.Get(IntOut) & (1 << slot)) != 0;
        }

        public bool Practice
        {
            get { return (State.Get(IntFlags) & 1) != 0; }
        }

        /// <summary>The local player is playing this round and is not out.</summary>
        public bool LocalActive
        {
            get
            {
                if (Phase != RrPhase.Playing)
                {
                    return false;
                }

                int slot = SlotOf(RrNet.LocalId);
                return slot >= 0 && !SlotOut(slot);
            }
        }

        public bool CanThrowLive()
        {
            return LocalActive;
        }

        /// <summary>May a live ball of throwerTeam put this player (or practice target) out?</summary>
        public bool IsValidTarget(int playerId, int throwerTeam)
        {
            if (playerId >= RrCombat.FirstTargetId)
            {
                var t = TargetById(playerId);
                return t != null && t.Shown && !t.Down;
            }

            if (Phase != RrPhase.Playing)
            {
                return false;
            }

            int slot = SlotOf(playerId);
            return slot >= 0 && !SlotOut(slot) && SlotTeam(slot) != throwerTeam;
        }

        private RrDodgeballTarget TargetById(int id)
        {
            foreach (var t in Targets)
            {
                if (t != null && t.TargetId == id)
                {
                    return t;
                }
            }

            return null;
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
                // Our balls and practice standees stand where the level's do (the coconut bombs are the Dodgebombs mod,
                // off by default); Rec Room hides its circuit boards from players. The exit door's records are cut from
                // the data (DN/iter16/dp_tools/dp_clean.awk): RrPortal takes its place.
                foreach (var n in new[] { BallPrefab, "Coconut", "PracticeMale_Cutout_LOD0", "CircuitNode_Board" })
                {
                    _area.Skip.Add(n);
                }

                yield return _area.Build(raw, t, _surfaces);
            }
            else
            {
                RrLog.Warn("Dodgeball", "no scene data \"" + SceneName + "\": building the stand-in gym");
            }

            if (this == null)
            {
                yield break;
            }

            _level = _area != null && _area.Kit != null;
            UseLayout();
            if (!_level)
            {
                BuildShell(t);
                yield return null;
                BuildProgress = 0.2f;
                BuildCourt(t);
                yield return null;
                BuildProgress = 0.3f;
                BuildDecor(t);
                BuildBarriers(t);
                yield return null;
            }

            ReadMarkers();
            BuildProgress = 0.4f;

            if (_level)
            {
                RrPortal.Create(t, RrActivity.RecCenter, LevelDoorSpot, LevelDoorYaw, "REC CENTER");
            }
            else
            {
                RrPortal.Create(t, RrActivity.RecCenter, new Vector3(0f, 0f, -HallHalfZ + 0.06f), 0f, "REC CENTER");
            }

            BuildConsole(t);
            // Stand-in: on its end walls. Level: on the solid lower band of the gym's end walls (z +-17.90, y 2.19..4.26, windows
            // above), centred between the two door pairs (the doors start 2.28 m off the middle).
            const float boardWidth = 4f;
            var south = _level ? RrScoreboard.Create(t, new Vector3(0f, LevelEndBoardY, -LevelEndWallZ + 0.12f), Quaternion.identity, "DODGEBALL", 2, boardWidth)
                : RrScoreboard.Create(t, new Vector3(0f, 5.2f, -HallHalfZ + 0.12f), Quaternion.identity, "DODGEBALL", 2, boardWidth);
            var north = _level ? RrScoreboard.Create(t, new Vector3(0f, LevelEndBoardY, LevelEndWallZ - 0.12f), Quaternion.Euler(0f, 180f, 0f), "DODGEBALL", 2, boardWidth)
                : RrScoreboard.Create(t, new Vector3(0f, 5.2f, HallHalfZ - 0.12f), Quaternion.Euler(0f, 180f, 0f), "DODGEBALL", 2, boardWidth);
            _boards.Add(south);
            _boards.Add(north);
            _infos.Add(InfoLine(south, boardWidth));
            _infos.Add(InfoLine(north, boardWidth));
            yield return null;

            // A frame later, so the colliders RrBuild removed (Destroy is deferred) are gone and not counted.
            int courtColliders = ApplyCourtPhysics(t);
            RrLog.Info("Dodgeball", "court physics: DodgeballCourt_phys (0.6 / 0.6 / bounce 0.6, Average) on " + courtColliders + " gym colliders; barriers at z +-" + _barrierZ + ", "
                + _barrierX * 2f + " m wide" + (_level ? " (the level's crash pads)" : " (stand-in, " + BarrierHeight + " m high)"));
            BuildProgress = 0.5f;

            // Rec Room's balls, on the centre line.
            int waiting = BallCount;
            for (int i = 0; i < BallCount; i++)
            {
                int index = i;
                Vector3 home = BallHome(i);
                RrSpawn.Spawn(BallPrefab, t, home, Quaternion.identity, inst =>
                {
                    SetupBall(index, inst, home);
                    waiting--;
                });
            }

            float until = Time.realtimeSinceStartup + 30f;
            while (waiting > 0 && Time.realtimeSinceStartup < until)
            {
                BuildProgress = 0.5f + 0.3f * (BallCount - waiting) / BallCount;
                yield return null;
            }

            for (int i = 0; i < BallCount; i++)
            {
                if (Balls.Exists(b => b != null && b.Index == i))
                {
                    continue;
                }

                // Never loaded: a plain ball so the game still plays.
                SetupBall(i, null, BallHome(i));
            }

            Balls.Sort((a, b) => a.Index.CompareTo(b.Index));
            BuildProgress = 0.8f;

            // Practice standees (on the level's standee spots) and, in the stand-in, cones (Rec Room props as scenery).
            string[] standees = { "(Standee_Male01)", "(Standee_Female03)" };
            var spots = StandeeSpots(standees.Length);
            waiting = standees.Length;
            for (int i = 0; i < standees.Length; i++)
            {
                var target = RrDodgeballTarget.Create(t, "Practice Target " + i, spots[i].position, spots[i].rotation.eulerAngles.y);
                Targets.Add(target);
                RrSpawn.Spawn(standees[i], target.Pivot, Vector3.zero, Quaternion.identity, inst =>
                {
                    waiting--;
                    if (inst == null)
                    {
                        RrBuild.Box(target.Pivot, "Cutout", new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 1.8f, 0.04f), RrLook.Solid(RrLook.Cream), false);
                    }
                }, RrSpawnOptions.Visual());
            }

            if (!_level)
            {
                Vector3[] cones = { new Vector3(-4.9f, 0f, -8.4f), new Vector3(4.9f, 0f, -8.4f), new Vector3(-4.9f, 0f, 8.4f), new Vector3(4.9f, 0f, 8.4f) };
                foreach (var c in cones)
                {
                    RrSpawn.Spawn("(Sandbox_Cone)", t, c, Quaternion.identity, inst => { }, RrSpawnOptions.Scenery());
                }
            }

            until = Time.realtimeSinceStartup + 20f;
            while (waiting > 0 && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            BuildProgress = 1f;
            RrLog.Info("Dodgeball", "gym built from " + (_level ? "level11 (" + SceneName + ".bytes)" : "the stand-in gym") + ": court " + (_courtX * 2f).ToString("F2") + " x " + (_courtZ * 2f).ToString("F2")
                + " m (floor y " + _courtY.ToString("F3") + ", out past x +-" + _outX.ToString("F2") + " / z +-" + _outZ.ToString("F2") + "), lobby spawns " + _lobbySpawns.Count + ", court spawns "
                + _courtSpawns[0].Count + " / " + _courtSpawns[1].Count + ", sideline spawns " + _benchSpawns[0].Count + " / " + _benchSpawns[1].Count + "; " + Balls.Count + " balls (" + BallPrefab
                + ", screen throw " + ScreenThrowSpeed + " m/s at any charge / " + MaxThrowSpeed + " m/s max), first to " + RoundsToWin + " rounds, final rules: catch = dead ball, catch outs thrower " + CatchOutsThrower);
        }

        /// <summary>The numbers the rules use: the level's measured gym, or the stand-in's.</summary>
        private void UseLayout()
        {
            if (_level)
            {
                _courtX = LevelCourtHalfX;
                _courtZ = LevelCourtHalfZ;
                _courtY = LevelCourtY;
                // Out a little past the level's kill zones (feet, so a VR lean or a reach over the line is not out), and
                // never in front of the crash pads, where a player may stand to fetch a ball they stopped.
                _outX = LevelKillX + 0.4f;
                _outZ = Mathf.Max(LevelKillZ + 0.4f, LevelBarrierInnerZ);
                _hallX = LevelHallHalfX;
                _hallZ = LevelHallHalfZ;
                _hallFloor = LevelHallFloor;
                _hallTop = LevelHallTop;
                _barrierZ = LevelBarrierZ;
                _barrierX = LevelBarrierHalfX;
                _ballX = LevelBallX;
            }
            else
            {
                _courtX = CourtHalfX;
                _courtZ = CourtHalfZ;
                _courtY = 0f;
                _outX = CourtHalfX + 0.5f;
                _outZ = CourtHalfZ + 1f;
                _hallX = HallHalfX;
                _hallZ = HallHalfZ;
                _hallFloor = 0f;
                _hallTop = HallHeight;
                _barrierZ = BarrierZ;
                _barrierX = BarrierHalfX;
                _ballX = BallX;
            }
        }

        private Vector3 BallHome(int i)
        {
            return new Vector3(_ballX[i % _ballX.Length], _courtY + RrDodgeball.Radius + 0.01f, 0f);
        }

        /// <summary>
        /// The level's spawn points: SpawnPoint1-8 (team=pregame) are the lobby, Team1/Team2CourtSpawnPoint each team's
        /// places when a round starts, Team1/Team2SidelineSpawnPoint where its players wait when they are out. Measured
        /// spots when the kit reports none; the stand-in's own rows without the level.
        /// </summary>
        private void ReadMarkers()
        {
            _lobbySpawns.Clear();
            for (int i = 0; i < 2; i++)
            {
                _courtSpawns[i].Clear();
                _benchSpawns[i].Clear();
            }

            if (_level)
            {
                foreach (var m in _area.Markers("spawn"))
                {
                    string team = m.Get("team", "");
                    var pose = new Pose(m.Position + Vector3.up * 0.05f, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f));
                    int k = team == "blue" ? 0 : team == "red" ? 1 : -1;
                    if (k < 0)
                    {
                        _lobbySpawns.Add(pose);
                    }
                    else if (m.Name.IndexOf("Sideline", StringComparison.Ordinal) >= 0)
                    {
                        _benchSpawns[k].Add(pose);
                    }
                    else
                    {
                        _courtSpawns[k].Add(pose);
                    }
                }

                if (_lobbySpawns.Count == 0)
                {
                    foreach (var p in LevelLobby)
                    {
                        _lobbySpawns.Add(new Pose(new Vector3(p.x, p.y + 0.05f, p.z), Quaternion.Euler(0f, p.w, 0f)));
                    }
                }

                for (int k = 0; k < 2; k++)
                {
                    if (_courtSpawns[k].Count == 0)
                    {
                        foreach (var p in LevelCourtSpawns[k])
                        {
                            _courtSpawns[k].Add(new Pose(p + Vector3.up * 0.05f, Quaternion.Euler(0f, k == 0 ? 0f : 180f, 0f)));
                        }
                    }

                    if (_benchSpawns[k].Count == 0)
                    {
                        foreach (var p in LevelSidelineSpawns[k])
                        {
                            _benchSpawns[k].Add(new Pose(p + Vector3.up * 0.05f, Quaternion.Euler(0f, k == 0 ? -90f : 90f, 0f)));
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    _lobbySpawns.Add(new Pose(new Vector3(-1.8f + i * 1.2f, 0.05f, -9.7f), Quaternion.Euler(0f, ArrivalYaw, 0f)));
                }

                float[] xs = { 0f, -2.5f, 2.5f };
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    foreach (float x in xs)
                    {
                        _courtSpawns[k].Add(new Pose(new Vector3(x, 0.05f, side * 6f), Quaternion.Euler(0f, k == 0 ? 0f : 180f, 0f)));
                    }

                    for (int i = 0; i < 6; i++)
                    {
                        float z = 1.5f + i * 1.1f;
                        _benchSpawns[k].Add(new Pose(new Vector3(side * 6.1f, 0.45f, side * z), Quaternion.Euler(0f, k == 0 ? 90f : -90f, 0f)));
                    }
                }
            }

            for (int k = 0; k < 2; k++)
            {
                // The centre spot first, then outwards (the stand-in's order: 0, -2.5, +2.5); the sideline from the centre line out.
                _courtSpawns[k].Sort((a, b) => Mathf.Abs(a.position.x).CompareTo(Mathf.Abs(b.position.x)));
                _benchSpawns[k].Sort((a, b) => Mathf.Abs(a.position.z).CompareTo(Mathf.Abs(b.position.z)));
            }
        }

        /// <summary>The practice standees' spots: the level's upright standees on the red half, else the stand-in's.</summary>
        private List<Pose> StandeeSpots(int count)
        {
            var list = new List<Pose>();
            if (_level)
            {
                foreach (var m in _area.Markers("standee"))
                {
                    string path = m.Get("path", "");
                    // Top-level daytime items only (not their LOD children, not the night set), standing upright.
                    if (!path.StartsWith("Daytime/PracticeItems/", StringComparison.Ordinal) || path.Split('/').Length != 3 || (m.Rotation * Vector3.up).y < 0.9f)
                    {
                        continue;
                    }

                    list.Add(new Pose(new Vector3(m.Position.x, _courtY, m.Position.z), Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f)));
                }

                if (list.Count < count)
                {
                    list.Clear();
                    foreach (var p in LevelStandees)
                    {
                        list.Add(new Pose(p, Quaternion.Euler(0f, 180f, 0f)));
                    }
                }
            }
            else
            {
                list.Add(new Pose(new Vector3(-2f, 0f, 5.5f), Quaternion.Euler(0f, 180f, 0f)));
                list.Add(new Pose(new Vector3(2f, 0f, 5.5f), Quaternion.Euler(0f, 180f, 0f)));
            }

            while (list.Count < count)
            {
                list.Add(list[list.Count - 1]);
            }

            return list;
        }

        /// <summary>
        /// Gives every solid gym collider built so far (floor, walls, pads, benches, barriers, pedestal, boards) Rec
        /// Room's court physics; the balls and props spawned later keep their own. Returns the count for the log.
        /// </summary>
        private static int ApplyCourtPhysics(Transform root)
        {
            int n = 0;
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger || c.sharedMaterial != null || c.gameObject.layer != RrLayers.Environment)
                {
                    continue;
                }

                c.sharedMaterial = CourtPhysics;
                n++;
            }

            return n;
        }

        /// <summary>
        /// The 2026-01-22 map change: "small barriers behind each side to catch dodgeballs rolling by". A padded kerb
        /// in the team colour with a white cap across each end, 0.9 m behind the back line and 0.5 m wider than the
        /// court on each side (GUESS: no size was published). It stops a rolling ball (0.22 m) where the team behind
        /// that line can fetch it; players walk round its ends (2.9 m to the side walls) from the door area.
        /// </summary>
        private void BuildBarriers(Transform t)
        {
            var cap = RrLook.Solid(RrLook.White);
            float body = BarrierHeight - 0.05f;
            for (int team = 0; team < 2; team++)
            {
                float z = team == 0 ? -BarrierZ : BarrierZ;
                string name = "Barrier " + RrTeams.Name(team);
                var pad = RrLook.Solid(Color.Lerp(RrTeams.Color(team), RrLook.DarkGrey, 0.25f));
                RrBuild.Box(t, name, new Vector3(0f, body * 0.5f, z), new Vector3(BarrierHalfX * 2f, body, BarrierThickness), pad);
                RrBuild.Box(t, name + " Cap", new Vector3(0f, body + 0.025f, z), new Vector3(BarrierHalfX * 2f + 0.04f, 0.05f, BarrierThickness + 0.04f), cap);
                for (int side = -1; side <= 1; side += 2)
                {
                    // Rounded end posts, so a player walking round does not catch a corner.
                    RrBuild.Cylinder(t, name + " Post", new Vector3(side * BarrierHalfX, BarrierHeight * 0.5f + 0.01f, z), BarrierThickness * 0.7f, BarrierHeight + 0.02f, cap);
                }
            }
        }

        private void SetupBall(int index, RrInstance inst, Vector3 home)
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
                RrLog.Warn("Dodgeball", "ball " + index + ": " + BallPrefab + " did not load, using a plain ball");
                go = RrBuild.Sphere(transform, "Dodgeball " + index, home, RrDodgeball.Radius, RrLook.Solid(RrLook.Red), true, RrLayers.Prop);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.angularDamping = 2f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    col.sharedMaterial = BallPhysics;
                }
            }

            // Rec Room's ball brings DodgeballBall_phys from ART; a collider that came without it gets our copy.
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger)
                {
                    continue;
                }

                if (c.sharedMaterial == null)
                {
                    c.sharedMaterial = BallPhysics;
                }

                if (index == 0)
                {
                    var m = c.sharedMaterial;
                    RrLog.Info("Dodgeball", "ball collider " + c.GetType().Name + " material " + m.name + ": friction " + m.dynamicFriction + " / " + m.staticFriction + " (" + m.frictionCombine + "), bounce " + m.bounciness + " (" + m.bounceCombine + ")");
                }
            }

            if (grab == null)
            {
                grab = go.GetComponent<RrGrabbable>() ?? go.AddComponent<RrGrabbable>();
                grab.ThrowSound = "Throw01";
            }

            var respawn = go.GetComponent<RrRespawnable>();
            if (respawn != null)
            {
                // The game respawns its own balls (out of the play area / resting off court for 3 s).
                Destroy(respawn);
            }

            var net = go.GetComponent<RrNetBody>() ?? go.AddComponent<RrNetBody>();
            net.LocalPropId = index;
            var ball = go.AddComponent<RrDodgeball>();
            ball.Setup(this, inst, index, grab, net);
            ball.HomeLocal = home;
            Balls.Add(ball);
        }

        private void BuildShell(Transform t)
        {
            var cream = RrLook.Solid(RrLook.Cream);
            var ceiling = RrLook.Solid(new Color(0.34f, 0.36f, 0.42f));
            RrBuild.RoomShell(t, "Gym", Vector3.zero, new Vector3(HallHalfX * 2f + 0.3f, HallHeight, HallHalfZ * 2f + 0.3f), RrLook.Solid(RrLook.DarkWood), cream, ceiling);

            // Wood planks, two tones, 1 m boards along the court.
            var woodA = RrLook.Solid(RrLook.Wood);
            var woodB = RrLook.Solid(Color.Lerp(RrLook.Wood, RrLook.Sand, 0.3f));
            for (int i = 0; i < (int)(HallHalfX * 2f); i++)
            {
                float x = -HallHalfX + 0.5f + i;
                RrBuild.Box(t, "Boards " + i, new Vector3(x, 0.005f, 0f), new Vector3(1f, 0.01f, HallHalfZ * 2f), (i & 1) == 0 ? woodA : woodB, false);
            }

            // Padded dado (2 m): team colours on the end walls, navy on the sides; the door gap in the south wall.
            var navy = RrLook.Solid(new Color(0.16f, 0.24f, 0.45f));
            var blue = RrLook.Solid(RrLook.TeamBlue);
            var red = RrLook.Solid(RrLook.TeamRed);
            const float pad = 0.12f;
            RrBuild.Box(t, "Pad W", new Vector3(-HallHalfX + pad * 0.5f, 1f, 0f), new Vector3(pad, 2f, HallHalfZ * 2f), navy);
            RrBuild.Box(t, "Pad E", new Vector3(HallHalfX - pad * 0.5f, 1f, 0f), new Vector3(pad, 2f, HallHalfZ * 2f), navy);
            RrBuild.Box(t, "Pad N", new Vector3(0f, 1f, HallHalfZ - pad * 0.5f), new Vector3(HallHalfX * 2f, 2f, pad), red);
            float gap = 1.5f;
            float w = HallHalfX - gap;
            RrBuild.Box(t, "Pad S1", new Vector3(-gap - w * 0.5f, 1f, -HallHalfZ + pad * 0.5f), new Vector3(w, 2f, pad), blue);
            RrBuild.Box(t, "Pad S2", new Vector3(gap + w * 0.5f, 1f, -HallHalfZ + pad * 0.5f), new Vector3(w, 2f, pad), blue);
            var trim = RrLook.Solid(RrLook.White);
            RrBuild.Box(t, "Trim W", new Vector3(-HallHalfX + 0.08f, 2.03f, 0f), new Vector3(0.16f, 0.06f, HallHalfZ * 2f), trim, false);
            RrBuild.Box(t, "Trim E", new Vector3(HallHalfX - 0.08f, 2.03f, 0f), new Vector3(0.16f, 0.06f, HallHalfZ * 2f), trim, false);

            // Light: panels in the ceiling and soft point lights under them.
            var panel = RrLook.Emissive(new Color(1f, 0.97f, 0.9f), 1.6f);
            for (int ix = -1; ix <= 1; ix += 2)
            {
                for (int iz = -1; iz <= 1; iz++)
                {
                    RrBuild.Box(t, "Light Panel", new Vector3(ix * 3.5f, HallHeight - 0.08f, iz * 6.5f), new Vector3(2.4f, 0.1f, 1.2f), panel, false);
                    RrBuild.PointLight(t, "Light", new Vector3(ix * 3.5f, HallHeight - 1.6f, iz * 6.5f), new Color(1f, 0.95f, 0.86f), 15f, 1.3f);
                }
            }

            // Roof trusses.
            var truss = RrLook.Solid(new Color(0.22f, 0.23f, 0.27f));
            for (int iz = -2; iz <= 2; iz++)
            {
                RrBuild.Box(t, "Truss", new Vector3(0f, HallHeight - 0.35f, iz * 4.2f), new Vector3(HallHalfX * 2f, 0.3f, 0.25f), truss, false);
            }
        }

        private void BuildCourt(Transform t)
        {
            var line = RrLook.Solid(RrLook.White);
            const float y = 0.013f;
            const float lw = 0.08f;
            Vector3 thin = new Vector3(0f, 0.004f, 0f);
            RrBuild.Box(t, "Line W", new Vector3(-CourtHalfX, y, 0f), new Vector3(lw, 0f, CourtHalfZ * 2f + lw) + thin, line, false);
            RrBuild.Box(t, "Line E", new Vector3(CourtHalfX, y, 0f), new Vector3(lw, 0f, CourtHalfZ * 2f + lw) + thin, line, false);
            RrBuild.Box(t, "Line S", new Vector3(0f, y, -CourtHalfZ), new Vector3(CourtHalfX * 2f + lw, 0f, lw) + thin, line, false);
            RrBuild.Box(t, "Line N", new Vector3(0f, y, CourtHalfZ), new Vector3(CourtHalfX * 2f + lw, 0f, lw) + thin, line, false);
            RrBuild.Box(t, "Attack Line S", new Vector3(0f, y, -3f), new Vector3(CourtHalfX * 2f, 0f, lw) + thin, line, false);
            RrBuild.Box(t, "Attack Line N", new Vector3(0f, y, 3f), new Vector3(CourtHalfX * 2f, 0f, lw) + thin, line, false);
            RrBuild.Box(t, "Centre Line", new Vector3(0f, y + 0.001f, 0f), new Vector3(CourtHalfX * 2f, 0f, 0.15f) + thin, RrLook.Solid(RrLook.Yellow), false);

            // Centre circle.
            const int segments = 32;
            const float r = 1.8f;
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / segments;
                var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                RrBuild.Box(t, "Circle", p, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), new Vector3(lw, 0.004f, 2f * Mathf.PI * r / segments + 0.02f), line, false);
            }

            // Team back zones.
            RrBuild.Box(t, "Zone Blue", new Vector3(0f, 0.011f, -6.5f), new Vector3(CourtHalfX * 2f, 0.002f, 3f), RrLook.Solid(Color.Lerp(RrLook.TeamBlue, RrLook.White, 0.35f)), false);
            RrBuild.Box(t, "Zone Red", new Vector3(0f, 0.011f, 6.5f), new Vector3(CourtHalfX * 2f, 0.002f, 3f), RrLook.Solid(Color.Lerp(RrLook.TeamRed, RrLook.White, 0.35f)), false);
        }

        private void BuildDecor(Transform t)
        {
            // Out benches: a 0.4 m riser beside each team's half, the bench against the wall.
            var riser = RrLook.Solid(new Color(0.30f, 0.34f, 0.42f));
            var seat = RrLook.Solid(RrLook.DarkWood);
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                float cz = side == 0 ? -4.25f : 4.25f;
                RrBuild.Box(t, "Bench Riser " + RrTeams.Name(side), new Vector3(sx * 6.3f, 0.2f, cz), new Vector3(1.8f, 0.4f, 7.5f), riser);
                RrBuild.Box(t, "Bench " + RrTeams.Name(side), new Vector3(sx * 7.0f, 0.62f, cz), new Vector3(0.4f, 0.45f, 7f), seat);
                RrBuild.Box(t, "Bench Stripe " + RrTeams.Name(side), new Vector3(sx * 5.42f, 0.2f, cz), new Vector3(0.05f, 0.4f, 7.5f), RrLook.Solid(RrTeams.Color(side)), false);
                RrBuild.Sign(t, "Bench Sign " + RrTeams.Name(side), RrTeams.Name(side).ToUpperInvariant() + " BENCH", new Vector3(sx * (HallHalfX - 0.15f), 2.6f, cz), Quaternion.Euler(0f, side == 0 ? -90f : 90f, 0f), 4f, RrTeams.Color(side), true, 4f);
            }

            // High windows on the long walls.
            var sky = RrLook.Emissive(new Color(0.62f, 0.8f, 0.97f), 1.2f);
            var frame = RrLook.Solid(RrLook.White);
            for (int i = -2; i <= 2; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (HallHalfX - 0.03f);
                    RrBuild.Box(t, "Window", new Vector3(x, 6.8f, i * 4.2f), new Vector3(0.04f, 1.6f, 2.6f), sky, false);
                    RrBuild.Box(t, "Window Sill", new Vector3(x - side * 0.04f, 5.97f, i * 4.2f), new Vector3(0.1f, 0.08f, 2.8f), frame, false);
                }
            }

            // Basketball backboards on the end walls (a gym is a gym).
            var board = RrLook.Solid(RrLook.White);
            var orange = RrLook.Solid(RrLook.Orange);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * (HallHalfZ - 0.35f);
                RrBuild.Box(t, "Backboard", new Vector3(-5.2f, 3.4f, z), new Vector3(1.8f, 1.05f, 0.05f), board, false);
                RrBuild.Box(t, "Backboard Square", new Vector3(-5.2f, 3.25f, z - side * 0.03f), new Vector3(0.6f, 0.45f, 0.01f), orange, false);
                RrBuild.Box(t, "Backboard Arm", new Vector3(-5.2f, 3.4f, side * (HallHalfZ - 0.17f)), new Vector3(0.1f, 0.1f, 0.35f), RrLook.Solid(RrLook.DarkGrey), false);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    var p = new Vector3(-5.2f + Mathf.Cos(a) * 0.23f, 3.05f, z - side * (0.28f + Mathf.Sin(a) * 0.23f));
                    RrBuild.Box(t, "Rim", p, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), new Vector3(0.12f, 0.02f, 0.02f), orange, false);
                }
            }

            RrBuild.Sign(t, "Title N", "DODGEBALL", new Vector3(0f, 7.4f, HallHalfZ - 0.1f), Quaternion.identity, 14f, RrLook.Orange, true, 10f);
            RrBuild.Sign(t, "Title S", "DODGEBALL", new Vector3(0f, 7.4f, -HallHalfZ + 0.1f), Quaternion.Euler(0f, 180f, 0f), 14f, RrLook.Orange, true, 10f);
        }

        // ------------------------------------------------------------------ spawns

        /// <summary>
        /// Arrival: the level's pre-game spawn points in the corner behind the blue end (they face the projector); the
        /// stand-in's row in the door area, 0.7 m behind the blue barrier's outer face (z -9.0), facing the hologram (east).
        /// </summary>
        public override Pose Spawn(int index)
        {
            if (_lobbySpawns.Count == 0)
            {
                ReadMarkers();
            }

            return _lobbySpawns[Mathf.Abs(index) % _lobbySpawns.Count];
        }

        /// <summary>A team's place when a round starts (the level's court spawn points; a fourth player and on, 1.2 m further back).</summary>
        private Pose TeamSpot(int team, int index)
        {
            var list = _courtSpawns[team & 1];
            if (list.Count == 0)
            {
                ReadMarkers();
            }

            var p = list[Mathf.Abs(index) % list.Count];
            int row = Mathf.Abs(index) / list.Count;
            float side = p.position.z >= 0f ? 1f : -1f;
            float z = side * Mathf.Min(Mathf.Abs(p.position.z) + row * 1.2f, _courtZ - 0.4f);
            return new Pose(new Vector3(p.position.x, p.position.y, z), p.rotation);
        }

        /// <summary>Where an out player waits (the level's sideline spawn points; past the third, the row goes on away from the centre line).</summary>
        private Pose BenchSpot(int team, int index)
        {
            var list = _benchSpawns[team & 1];
            if (list.Count == 0)
            {
                ReadMarkers();
            }

            int n = list.Count;
            var p = list[Mathf.Abs(index) % n];
            int row = Mathf.Abs(index) / n;
            if (row == 0 || n < 2)
            {
                return p;
            }

            float step = list[1].position.z - list[0].position.z;
            float z = p.position.z + row * (list[n - 1].position.z - list[0].position.z + step);
            float limit = (_level ? _courtZ : _hallZ) - 0.3f;
            return new Pose(new Vector3(p.position.x, p.position.y, Mathf.Clamp(z, -limit, limit)), p.rotation);
        }

        private void TeleportLocal(Pose local)
        {
            Vector3 feet = transform.TransformPoint(local.position);
            float yaw = (transform.rotation * local.rotation).eulerAngles.y;
            RrPlayer.Teleport(feet, yaw);
        }

        /// <summary>Rank of the local player among the members of its team (spreads spawn spots).</summary>
        private int LocalRank(int team)
        {
            var members = Members();
            members.Sort();
            int rank = 0;
            int me = RrNet.LocalId;
            foreach (int id in members)
            {
                if (id == me)
                {
                    break;
                }

                if (RrTeams.TeamOf(id) == team)
                {
                    rank++;
                }
            }

            return rank;
        }

        private int TeamIndexOfSlot(int slot)
        {
            int team = SlotTeam(slot);
            int k = 0;
            for (int i = 0; i < slot; i++)
            {
                if (SlotTeam(i) == team)
                {
                    k++;
                }
            }

            return k;
        }

        // ------------------------------------------------------------------ lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            RrNet.On(EvHit, OnHitClaim);
            RrNet.On(EvCatch, OnCatchClaim);
            RrNet.On(EvLine, OnLineClaim);
            RrNet.On(EvVerdict, OnVerdict);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RrNet.Off(EvHit, OnHitClaim);
            RrNet.Off(EvCatch, OnCatchClaim);
            RrNet.Off(EvLine, OnLineClaim);
            RrNet.Off(EvVerdict, OnVerdict);
        }

        public override void OnLocalEnter()
        {
            base.OnLocalEnter();
            RrHitVolumes.Ensure();
            _myOut = false;
            _entered = true;
            ShowTargets(Phase == RrPhase.Lobby || Practice);
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            _entered = false;
            Unfreeze();
            foreach (var t in Targets)
            {
                if (t != null)
                {
                    t.Show(false);
                }
            }
        }

        private void OnDestroy()
        {
            Unfreeze();
        }

        private void Freeze()
        {
            if (!_frozen)
            {
                _frozen = true;
                RrPlayer.SetMovement(0f, false);
            }
        }

        private void Unfreeze()
        {
            if (_frozen)
            {
                _frozen = false;
                RrPlayer.SetMovement(1f);
            }
        }

        /// <summary>
        /// Rec Room's game hologram (<see cref="RrGameConsole"/>): the level's (ScoreboardProjector) stands at
        /// (2.79, 0.36, -11.81), 2.7 m behind the blue back line, turned (yaw -117.5) toward the pregame spawns, which
        /// face it; level11 builds it right there (its marker, else <see cref="LevelConsoleSpot"/>). The stand-in
        /// gym ends 3 m behind the line (wall at z -11) and the open hologram is 4.36 m wide (see
        /// RrGameConsole.ProjectedSize), so the projector stands in the south-east corner (6.3, -8.95), turned (yaw -60)
        /// between the arrival row and the court: the hologram runs from (4.74, -10.57) to (6.92, -6.79), clear of the
        /// south wall's pads (at -9.2 it touched Pad S2) and the east wall and above the 0.5 m barrier. The arrival row turns toward it (<see cref="Spawn"/>, yaw 80),
        /// the court on the players' left as in the level. Alone, START is solo practice (and stops it).
        /// </summary>
        private void BuildConsole(Transform t)
        {
            var spec = new RrGameConsoleSpec
            {
                TeamCount = 2,
                StartBelowMin = true,
                OnStart = id => OnStartPressed(),
                CanStart = () => Phase == RrPhase.Lobby || (Phase == RrPhase.Playing && Practice),
                StartLabel = () => Phase == RrPhase.Playing && Practice ? "STOP PRACTICE" : Phase == RrPhase.Lobby && Members().Count < 2 ? "PRACTICE" : RrGameConsole.PhaseStartLabel(this),
                Status = () => Phase == RrPhase.Lobby && Members().Count < 2 ? "1 MORE PLAYER NEEDED - START TO PRACTISE" : null,
                Subtitle = () => Phase == RrPhase.Playing && Practice ? "SOLO PRACTICE" : Phase == RrPhase.Lobby ? "FIRST TO " + RoundsToWin + " ROUNDS" : RrGameConsole.PhaseLine(this),
            };
            var pose = new Pose(ConsoleSpot, Quaternion.Euler(0f, ConsoleYaw, 0f));
            if (_level)
            {
                pose = new Pose(LevelConsoleSpot, Quaternion.Euler(0f, LevelConsoleYaw, 0f));
                var holo = _area.Markers("scoreboard");
                if (holo.Count > 0)
                {
                    pose = new Pose(holo[0].Position, Quaternion.Euler(0f, holo[0].Rotation.eulerAngles.y, 0f));
                }
            }

            _console = RrGameConsole.ForGame(this, t, pose, spec);
        }

        private void OnStartPressed()
        {
            if (Phase == RrPhase.Lobby)
            {
                RrLog.Info("Dodgeball", "start pressed");
                RequestStart();
            }
            else if (Phase == RrPhase.Playing && Practice)
            {
                RequestEndPracticeNow();
            }
        }

        private void RequestEndPracticeNow()
        {
            RrNet.Send(RrGameEvents.GameRequest, new[] { RequestEndPractice, (byte)0 }, true);
        }

        protected override void AuthorityRequest(int playerId, byte request, byte arg)
        {
            if (request == RequestEndPractice && Phase == RrPhase.Playing && Practice)
            {
                RrLog.Info("Dodgeball", "practice ended by player " + playerId);
                State.SetPhase(RrPhase.Lobby, 0f);
            }
        }

        // ------------------------------------------------------------------ phases

        protected override void OnPhase(RrPhase old, RrPhase now)
        {
            switch (now)
            {
                case RrPhase.Countdown:
                    _myOut = false;
                    _lineClaimed = false;
                    _claims.Clear();
                    _seen.Clear();
                    DropLocalBalls();
                    PlaceForRound();
                    Freeze();
                    if (IsAuthority)
                    {
                        _pending.Clear();
                        _decided.Clear();
                        State.Set(IntOut, 0);
                        State.Set(IntCount, 0);
                        ResetBalls();
                    }

                    break;
                case RrPhase.Playing:
                    Unfreeze();
                    _playingSince = Time.time;
                    RrSound.PlayUi("Whistle01", 0.8f);
                    break;
                case RrPhase.RoundOver:
                    RrSound.PlayUi("Horn01", 0.8f);
                    break;
                case RrPhase.MatchOver:
                    Unfreeze();
                    break;
                case RrPhase.Lobby:
                    Unfreeze();
                    if (_wasParticipant && old != (RrPhase)255)
                    {
                        TeleportLocal(Spawn(Mathf.Abs(RrNet.LocalId)));
                    }

                    _wasParticipant = false;
                    _myOut = false;
                    break;
            }

            RrLog.Info("Dodgeball", "phase " + old + " -> " + now + " (round " + (State.Round + 1) + ", blue " + State.TeamScore(0) + " - red " + State.TeamScore(1) + ")");
        }

        protected override void OnTeamsAssigned()
        {
            if (Phase == RrPhase.Countdown)
            {
                PlaceForRound();
            }
        }

        private void PlaceForRound()
        {
            int team = RrTeams.LocalTeam;
            if (team != 0 && team != 1)
            {
                return;
            }

            _wasParticipant = true;
            TeleportLocal(TeamSpot(team, LocalRank(team)));
        }

        protected override void AuthorityMatchSetup()
        {
            ResetBalls();
        }

        protected override void AuthorityRoundStart(int round)
        {
            var s = State;
            var members = Members();
            members.Sort();
            int n = 0;
            bool blue = false, red = false;
            foreach (int id in members)
            {
                int team = RrTeams.TeamOf(id);
                if ((team != 0 && team != 1) || n >= MaxSlots)
                {
                    continue;
                }

                s.Set(IntSlots + n, ((id + 1) & 0xFFFFF) | (team << 20));
                blue |= team == 0;
                red |= team == 1;
                n++;
            }

            for (int i = n; i < MaxSlots; i++)
            {
                s.Set(IntSlots + i, 0);
            }

            bool practice = !(blue && red);
            s.Set(IntCount, n);
            s.Set(IntOut, 0);
            s.Set(IntFlags, (practice ? 1 : 0) | ((round & 0xFF) << 8));
            ResetBalls();
            RrLog.Info("Dodgeball", "round " + (round + 1) + " playing: " + n + " players" + (practice ? " (solo practice: no round end)" : ""));
        }

        protected override void AuthorityPlaying()
        {
            float now = Time.unscaledTime;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (now - p.At >= CatchWindow)
                {
                    _pending.RemoveAt(i);
                    Decide(KindHit, p.Key, p.Ball, p.ThrowId, p.Victim, p.Thrower);
                }
            }

            if (Practice)
            {
                if (Members().Count >= 2)
                {
                    RrLog.Info("Dodgeball", "a second player arrived: practice over, back to the lobby");
                    State.SetPhase(RrPhase.Lobby, 0f);
                }

                return;
            }

            // Participants who left the instance are out.
            var members = Members();
            int count = SlotCount;
            for (int slot = 0; slot < count; slot++)
            {
                int id = SlotId(slot);
                if (!SlotOut(slot) && !members.Contains(id) && !TestMembers.Contains(id))
                {
                    Decide(KindLeft, -1, 255, 0, id, -1);
                }
            }

            int blueIn = 0, redIn = 0;
            for (int slot = 0; slot < count; slot++)
            {
                if (SlotOut(slot))
                {
                    continue;
                }

                if (SlotTeam(slot) == 0)
                {
                    blueIn++;
                }
                else
                {
                    redIn++;
                }
            }

            if (blueIn == 0 || redIn == 0)
            {
                EndRound(blueIn > 0 ? 0 : redIn > 0 ? 1 : -1);
            }
        }

        protected override void AuthorityRoundOver(int winner)
        {
            if (winner >= 0)
            {
                AddTeamScore(winner, 1);
                AnnounceAll(State.TeamScore(winner) >= RoundsToWin ? RrAnnouncer.TeamWins(winner) : winner == 0 ? RrAnnouncer.BlueTeam : RrAnnouncer.RedTeam);
            }
            else
            {
                AnnounceAll(RrAnnouncer.Tie);
            }

            RrLog.Info("Dodgeball", "round " + (State.Round + 1) + " won by " + RrTeams.Name(winner) + ": blue " + State.TeamScore(0) + " - red " + State.TeamScore(1));
        }

        protected override bool AuthorityMatchDecided()
        {
            return State.TeamScore(0) >= RoundsToWin || State.TeamScore(1) >= RoundsToWin;
        }

        protected override void AuthorityBackToLobby()
        {
            ResetBalls();
            State.Set(IntCount, 0);
            State.Set(IntOut, 0);
            State.Set(IntFlags, 0);
        }

        // ------------------------------------------------------------------ balls (authority)

        private void ResetBalls()
        {
            foreach (var b in Balls)
            {
                ResetBall(b);
            }
        }

        private void ResetBall(RrDodgeball b)
        {
            if (b == null || b.Net == null)
            {
                return;
            }

            if (b.HeldLocally)
            {
                RrGrab.Release(b.Grab, Vector3.zero, Vector3.zero);
            }

            b.Net.ResetTo(transform.TransformPoint(b.HomeLocal), transform.rotation);
            b.Kill();
        }

        private void BallHousekeeping()
        {
            foreach (var b in Balls)
            {
                if (b == null || b.HeldByAnyone)
                {
                    continue;
                }

                Vector3 p = transform.InverseTransformPoint(b.transform.position);
                bool lost = p.y < _hallFloor - 2f || p.y > _hallTop + 2f || Mathf.Abs(p.x) > _hallX + 1f || Mathf.Abs(p.z) > _hallZ + 1f;
                float ax = Mathf.Abs(p.x);
                float az = Mathf.Abs(p.z);

                // A ball stopped by a barrier (between the back line and the barrier) stays in play for that team.
                bool onCourt = ax <= _courtX + 0.3f && az <= _courtZ + 0.3f;
                bool atBarrier = ax <= _barrierX && az > _courtZ && az < _barrierZ;
                bool offCourt = !onCourt && !atBarrier;
                if (lost || (offCourt && b.RestSeconds > RestRespawn))
                {
                    ResetBall(b);
                }
            }
        }

        private void DropLocalBalls()
        {
            foreach (var b in Balls)
            {
                if (b != null && b.HeldLocally)
                {
                    RrGrab.Release(b.Grab, Vector3.zero, Vector3.zero);
                }
            }
        }

        // ------------------------------------------------------------------ throws, catches, hits (local player)

        /// <summary>
        /// The screen-mode throw since 2021-04-15: always 14 m/s (holding the button only holds the ball), 7 degrees of
        /// gravity compensation, and a soft aim at the nearest opponent within 9 m and 30 degrees of the crosshair.
        /// </summary>
        public Vector3 ScreenThrowVelocity(RrDodgeball ball, out Vector3 spin)
        {
            float speed = ScreenThrowSpeed;
            Ray aim = RrInput.AimRay;
            Vector3 from = ball.transform.position;
            Vector3 target;
            if (!AimAssist(aim, out target))
            {
                RaycastHit hit;
                target = Physics.Raycast(aim, out hit, 60f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore) ? hit.point : aim.GetPoint(30f);
            }

            Vector3 dir = (target - from).normalized;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            float pitch = Mathf.Atan2(dir.y, Mathf.Max(flat.magnitude, 1e-4f)) + GravityCompensation * Mathf.Deg2Rad;
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : aim.direction;
            dir = flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
            spin = UnityEngine.Random.onUnitSphere * 6f;
            return dir * Mathf.Min(speed, MaxThrowSpeed);
        }

        private bool AimAssist(Ray aim, out Vector3 target)
        {
            target = Vector3.zero;
            float bestAngle = AssistAngle;
            bool found = false;
            int team = LocalTeam;
            int count = SlotCount;
            for (int slot = 0; slot < count; slot++)
            {
                if (SlotOut(slot) || SlotTeam(slot) == team)
                {
                    continue;
                }

                var head = HeadOf(SlotId(slot));
                if (head.HasValue && Consider(aim, head.Value - Vector3.up * 0.35f, ref bestAngle, ref target))
                {
                    found = true;
                }
            }

            foreach (var t in Targets)
            {
                if (t != null && t.Shown && !t.Down && Consider(aim, t.Head.position - Vector3.up * 0.4f, ref bestAngle, ref target))
                {
                    found = true;
                }
            }

            return found;
        }

        private static bool Consider(Ray aim, Vector3 chest, ref float bestAngle, ref Vector3 target)
        {
            Vector3 to = chest - aim.origin;
            if (to.magnitude > AssistDistance)
            {
                return false;
            }

            float angle = Vector3.Angle(aim.direction, to);
            if (angle >= bestAngle)
            {
                return false;
            }

            bestAngle = angle;
            target = chest;
            return true;
        }

        private Vector3? HeadOf(int playerId)
        {
            if (playerId == RrNet.LocalId)
            {
                return RrPlayer.HeadPosition;
            }

            var r = RrRemotePlayers.Get(playerId);
            if (r == null)
            {
                return null;
            }

            if (r.Head != null)
            {
                return r.Head.position;
            }

            return r.Root != null ? r.Root.position + Vector3.up * 1.6f : (Vector3?)null;
        }

        /// <summary>
        /// The local player took a ball in hand: a live opponent throw is a catch. Catch01 at once (the catcher should
        /// not wait for the authority), then the claim, which beats a hit on the catcher claimed for the same throw
        /// less than <see cref="CatchWindow"/> earlier. The ball itself dies in <see cref="RrDodgeball"/>'s grab.
        /// </summary>
        public void OnLocalGrab(RrDodgeball ball)
        {
            if (!ball.Live || !LocalActive)
            {
                return;
            }

            int thrower = ball.ThrowerId;
            if (ball.ThrowerTeam == LocalTeam || thrower == RrNet.LocalId)
            {
                return;
            }

            int throwId = ball.Net != null ? ball.Net.ThrowId & 0xFFFF : 0;
            RrLog.Info("Dodgeball", "caught ball " + ball.Index + " thrown by " + thrower + " (throw id " + throwId + "): dead ball, claiming the catch");
            RrSound.PlayUi("Catch01", 0.8f);
            SendClaim(EvCatch, Key(ball.Index, throwId), thrower, new[]
            {
                (byte)ball.Index, (byte)(throwId & 0xFF), (byte)(throwId >> 8),
                (byte)(RrNet.LocalId & 0xFF), (byte)((RrNet.LocalId >> 8) & 0xFF),
                (byte)(thrower & 0xFF), (byte)((thrower >> 8) & 0xFF),
            });
        }

        /// <summary>The simulating client's ball swept through a player's (or target's) hit volume.</summary>
        public void OnBallHitPlayer(RrDodgeball ball, int victimId, Vector3 point)
        {
            if (victimId >= RrCombat.FirstTargetId)
            {
                var target = TargetById(victimId);
                if (target == null)
                {
                    return;
                }

                target.Knock();
                RrSound.Play("dodgeball_PlayerHit_01", point, 1f);
                RrFx.Spawn("[VFX_Default_HitParticles]", point, Quaternion.identity);
                if (ball.ThrowerId == RrNet.LocalId)
                {
                    RrSound.PlayUi("HitSound01", 0.5f);
                    RrAnnouncer.Say("Hit");
                }

                RrLog.Info("Dodgeball", "practice target " + Targets.IndexOf(target) + " hit by ball " + ball.Index + " (knocks " + target.Knocks + ")");
                return;
            }

            int throwId = ball.Net != null ? ball.Net.ThrowId & 0xFFFF : 0;
            int thrower = ball.ThrowerId;
            RrLog.Info("Dodgeball", "ball " + ball.Index + " hit player " + victimId + " (thrower " + thrower + ", throw id " + throwId + "): claiming");
            SendClaim(EvHit, Key(ball.Index, throwId), victimId, new[]
            {
                (byte)ball.Index, (byte)(throwId & 0xFF), (byte)(throwId >> 8),
                (byte)(victimId & 0xFF), (byte)((victimId >> 8) & 0xFF),
                (byte)(thrower & 0xFF), (byte)((thrower >> 8) & 0xFF),
            });
        }

        private static int Key(int ball, int throwId)
        {
            return ((ball & 0xFF) << 16) | (throwId & 0xFFFF);
        }

        private void SendClaim(byte type, int key, int victim, byte[] body)
        {
            float now = Time.unscaledTime;
            _claims.Add(new Claim { Type = type, Body = body, Key = key, Victim = victim, Next = now + 0.3f, Until = now + 2f });
            RrNet.Send(type, body, true);
        }

        private void UpdateClaims()
        {
            float now = Time.unscaledTime;
            for (int i = _claims.Count - 1; i >= 0; i--)
            {
                var c = _claims[i];
                bool done = now > c.Until || (c.Key >= 0 && _seen.Contains(c.Key)) || (c.Victim >= 0 && SlotOut(SlotOf(c.Victim)));
                if (done)
                {
                    _claims.RemoveAt(i);
                }
                else if (now >= c.Next)
                {
                    c.Next = now + 0.3f;
                    RrNet.Send(c.Type, c.Body, true);
                }
            }
        }

        private void LineCheck()
        {
            if (_lineClaimed || Practice || Time.time - _playingSince < LineGrace)
            {
                return;
            }

            int slot = SlotOf(RrNet.LocalId);
            if (slot < 0 || SlotOut(slot))
            {
                return;
            }

            Vector3 p = transform.InverseTransformPoint(RrPlayer.FeetPosition);
            int team = SlotTeam(slot);
            bool crossed = team == 0 ? p.z > LineTolerance : p.z < -LineTolerance;
            // Level11: a little past its kill zones (x +-4.6, z +-9.2); stand-in: 0.5 m past the side lines, 1 m past the back lines.
            bool outside = Mathf.Abs(p.x) > _outX || Mathf.Abs(p.z) > _outZ;
            if (!crossed && !outside)
            {
                return;
            }

            _lineClaimed = true;
            int me = RrNet.LocalId;
            RrLog.Info("Dodgeball", (crossed ? "crossed the centre line" : "left the court") + " at " + p.ToString("F2") + ": out");
            SendClaim(EvLine, -1, me, new[] { (byte)(crossed ? 1 : 2), (byte)(State.Round & 0xFF), (byte)(me & 0xFF), (byte)((me >> 8) & 0xFF) });
        }

        // ------------------------------------------------------------------ claims (authority)

        private static int U16(byte[] b, int i)
        {
            return b[i] | (b[i + 1] << 8);
        }

        private void OnHitClaim(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 7)
            {
                return;
            }

            int ball = body[0];
            int throwId = U16(body, 1);
            int victim = U16(body, 3);
            int thrower = U16(body, 5);
            if (thrower == 0xFFFF)
            {
                thrower = -1;
            }

            int key = Key(ball, throwId);
            byte[] verdict;
            if (_decided.TryGetValue(key, out verdict))
            {
                RrNet.Send(EvVerdict, verdict);
                return;
            }

            if (Phase != RrPhase.Playing)
            {
                return;
            }

            int vs = SlotOf(victim);
            int ts = SlotOf(thrower);
            if (vs < 0 || SlotOut(vs) || (ts >= 0 && SlotTeam(ts) == SlotTeam(vs)))
            {
                return;
            }

            foreach (var p in _pending)
            {
                if (p.Key == key)
                {
                    return;
                }
            }

            _pending.Add(new Pending { Key = key, Ball = ball, ThrowId = throwId, Victim = victim, Thrower = thrower, At = Time.unscaledTime });
        }

        private void OnCatchClaim(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 7)
            {
                return;
            }

            int ball = body[0];
            int throwId = U16(body, 1);
            int catcher = U16(body, 3);
            int thrower = U16(body, 5);
            if (thrower == 0xFFFF)
            {
                thrower = -1;
            }

            int key = Key(ball, throwId);
            byte[] verdict;
            if (_decided.TryGetValue(key, out verdict))
            {
                RrNet.Send(EvVerdict, verdict);
                return;
            }

            if (Phase != RrPhase.Playing)
            {
                return;
            }

            int cs = SlotOf(catcher);
            if (cs < 0 || SlotOut(cs))
            {
                return;
            }

            // A catch of the same throw beats a hit claimed less than CatchWindow ago.
            int beaten = _pending.RemoveAll(p => p.Key == key);

            // Final rules: a dead ball, nobody out (the 2021-2024 catch-out only with CatchOutsThrower).
            int ts = SlotOf(thrower);
            int victim = CatchOutsThrower && ts >= 0 && !SlotOut(ts) && SlotTeam(ts) != SlotTeam(cs) ? thrower : -1;
            if (beaten > 0)
            {
                RrLog.Info("Dodgeball", "catch by " + catcher + " beat a pending hit on the same throw (ball " + ball + ", throw id " + throwId + ")");
            }

            Decide(KindCatch, key, ball, throwId, victim, catcher);
        }

        private void OnLineClaim(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 4 || Phase != RrPhase.Playing || Practice)
            {
                return;
            }

            int id = U16(body, 2);
            if (body[1] != (State.Round & 0xFF))
            {
                return;
            }

            int slot = SlotOf(id);
            if (slot < 0 || SlotOut(slot))
            {
                return;
            }

            Decide(KindLine, -1, 255, 0, id, -1);
        }

        /// <summary>
        /// Authority: applies a verdict to the out mask and tells everyone. Verdict body (9 bytes): kind, ball,
        /// throw id u16, victim u16 (0xFFFF = nobody out, a catch), other u16 (thrower of a hit / the catcher),
        /// round. Outs only ever add to the mask: nobody comes back before the next round.
        /// </summary>
        private void Decide(byte kind, int key, int ball, int throwId, int victim, int other)
        {
            int vs = SlotOf(victim);
            if (vs >= 0)
            {
                State.Set(IntOut, State.Get(IntOut) | (1 << vs));
            }

            int v = victim < 0 ? 0xFFFF : victim;
            int o = other < 0 ? 0xFFFF : other;
            var body = new[]
            {
                kind, (byte)ball, (byte)(throwId & 0xFF), (byte)((throwId >> 8) & 0xFF),
                (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)(o & 0xFF), (byte)((o >> 8) & 0xFF),
                (byte)(State.Round & 0xFF),
            };
            if (key >= 0)
            {
                _decided[key] = body;
            }

            RrNet.Send(EvVerdict, body);
        }

        // ------------------------------------------------------------------ verdicts (every client)

        private void OnVerdict(int sender, byte[] body)
        {
            if (body == null || body.Length < 9)
            {
                return;
            }

            byte kind = body[0];
            int ball = body[1];
            int throwId = U16(body, 2);
            int victim = U16(body, 4);
            int other = U16(body, 6);
            victim = victim == 0xFFFF ? -1 : victim;
            other = other == 0xFFFF ? -1 : other;
            if (ball != 255)
            {
                int key = Key(ball, throwId);
                if (!_seen.Add(key))
                {
                    return;
                }
            }

            _verdicts++;
            int me = RrNet.LocalId;
            Vector3? at = victim >= 0 ? HeadOf(victim) : null;
            switch (kind)
            {
                case KindHit:
                    if (at.HasValue)
                    {
                        Vector3 chest = at.Value - Vector3.up * 0.4f;
                        RrSound.Play("dodgeball_PlayerHit_01", chest, 1f);
                        RrFx.Spawn("[VFX_Default_HitParticles]", chest, Quaternion.identity);
                    }

                    if (other == me)
                    {
                        RrSound.PlayUi("HitSound01", 0.5f);
                        RrAnnouncer.Say("Hit");
                    }

                    break;
                case KindCatch:
                    // The catcher heard Catch01 on the grab; everyone else hears it at the catcher's hands.
                    var catcher = other >= 0 ? HeadOf(other) : null;
                    if (catcher.HasValue && other != me)
                    {
                        RrSound.Play("Catch01", catcher.Value - Vector3.up * 0.5f, 0.9f);
                    }

                    break;
                case KindLine:
                    if (at.HasValue && victim != me)
                    {
                        RrSound.Play("OutSFX", at.Value, 0.6f);
                    }

                    break;
            }

            if (victim == me && kind != KindLeft)
            {
                RrAnnouncer.Say("Out");
                RrSound.PlayUi("OutSFX", 0.7f);
            }

            string by = other < 0 ? "" : kind == KindCatch ? ", caught by " + other : ", by " + other;
            string what = kind == KindCatch && victim < 0 ? "dead ball, nobody out" : "out " + victim;
            RrLog.Info("Dodgeball", "verdict " + KindName(kind) + ": " + what + by + " (verdict " + _verdicts + ")");
        }

        private static string KindName(byte kind)
        {
            switch (kind)
            {
                case KindHit: return "hit";
                case KindCatch: return "catch";
                case KindLine: return "line";
                case KindLeft: return "left";
                default: return kind.ToString();
            }
        }

        // ------------------------------------------------------------------ every frame

        protected override void OnTick(RrInstanceState s)
        {
            var phase = s.Phase;
            if (phase == RrPhase.Playing)
            {
                int slot = SlotOf(RrNet.LocalId);
                if (slot >= 0)
                {
                    _wasParticipant = true;
                    bool outNow = SlotOut(slot);
                    if (outNow != _myOut)
                    {
                        _myOut = outNow;
                        int team = SlotTeam(slot);
                        int index = TeamIndexOfSlot(slot);
                        if (outNow)
                        {
                            DropLocalBalls();
                            TeleportLocal(BenchSpot(team, index));
                            Freeze();
                            RrLog.Info("Dodgeball", "local player out: to the " + RrTeams.Name(team) + " bench");
                        }
                        else
                        {
                            // The final rules never bring a player back mid-round; this only undoes a stale mask.
                            Unfreeze();
                            TeleportLocal(TeamSpot(team, index));
                            _lineClaimed = false;
                            _playingSince = Time.time;
                            RrLog.Info("Dodgeball", "local player in again (out mask cleared)");
                        }
                    }

                    LineCheck();
                }
            }

            bool canGrab = phase != RrPhase.Countdown && !(phase == RrPhase.Playing && _myOut);
            foreach (var b in Balls)
            {
                if (b != null && b.Grab != null)
                {
                    b.Grab.CanGrab = canGrab;
                }
            }

            ScreenCatchAssist();
            UpdateClaims();
            if (_entered)
            {
                ShowTargets(phase == RrPhase.Lobby || (phase == RrPhase.Playing && Practice));
            }

            if (IsAuthority)
            {
                BallHousekeeping();
            }

            if (Time.unscaledTime >= _boardAt)
            {
                _boardAt = Time.unscaledTime + 0.25f;
                UpdateBoards();
            }
        }

        private void ShowTargets(bool on)
        {
            foreach (var t in Targets)
            {
                if (t != null)
                {
                    t.Show(on);
                }
            }
        }

        /// <summary>Screen mode: a click or E with an empty hand catches a live ball coming at the player.</summary>
        private void ScreenCatchAssist()
        {
            if (RrPlayer.InVr || RrInput.Blocked || !(RrInput.UseDown || RrInput.GrabDown))
            {
                return;
            }

            var hand = RrHand.Right;
            if (hand == null || RrGrab.HeldBy(hand) != null)
            {
                return;
            }

            Vector3 head = RrPlayer.HeadPosition;
            Vector3 fwd = RrPlayer.HeadRotation * Vector3.forward;
            RrDodgeball best = null;
            float bestD = ScreenCatchReach;
            foreach (var b in Balls)
            {
                if (b == null || !b.Live || b.HeldByAnyone || b.Grab == null || !b.Grab.CanGrab)
                {
                    continue;
                }

                Vector3 to = b.transform.position - head;
                float d = to.magnitude;
                if (d < bestD && Vector3.Angle(fwd, to) < 70f)
                {
                    bestD = d;
                    best = b;
                }
            }

            if (best != null)
            {
                RrGrab.Grab(hand, best.Grab, true);
            }
        }

        private void UpdateBoards()
        {
            string text;
            string info = "DODGEBALL";
            var phase = Phase;
            if (phase == RrPhase.Playing && Practice)
            {
                text = "PRACTICE - press START to stop";
                info = "SOLO PRACTICE";
            }
            else if (phase == RrPhase.Playing || phase == RrPhase.RoundOver)
            {
                int blueIn = 0, redIn = 0;
                int n = SlotCount;
                for (int s = 0; s < n; s++)
                {
                    if (!SlotOut(s))
                    {
                        if (SlotTeam(s) == 0)
                        {
                            blueIn++;
                        }
                        else
                        {
                            redIn++;
                        }
                    }
                }

                // The board shows the round wins; the line under it the round and who is still in.
                info = "ROUND " + (State.Round + 1) + "  -  BLUE " + blueIn + " IN  |  RED " + redIn + " IN";
                text = "";
            }
            else if (phase == RrPhase.Lobby)
            {
                text = "FIRST TO " + RoundsToWin + " ROUNDS";
            }
            else
            {
                text = "";
                info = "FIRST TO " + RoundsToWin + " ROUNDS";
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

        /// <summary>A text line on a dark strip under a scoreboard (RrScoreboard's Custom text replaces the scores).</summary>
        internal static TMPro.TextMeshPro InfoLine(RrScoreboard board, float width)
        {
            float y = -width * 0.45f * 0.5f - 0.3f;
            RrBuild.Box(board.transform, "Info Strip", new Vector3(0f, y, -0.06f), new Vector3(width, 0.42f, 0.1f), RrLook.Solid(RrLook.DarkGrey), false);
            return RrBuild.Sign(board.transform, "Info", "", new Vector3(0f, y, 0f), Quaternion.Euler(0f, 180f, 0f), width * 0.55f, Color.white, true, width * 0.95f);
        }

        // ------------------------------------------------------------------ harness hooks (RrDodgeballTest)

        /// <summary>Verdicts this client has applied (the harness waits on it).</summary>
        internal int Verdicts
        {
            get { return _verdicts; }
        }

        /// <summary>Is this player (or fake) a participant who is out this round?</summary>
        internal bool TestIsOut(int playerId)
        {
            return SlotOut(SlotOf(playerId));
        }

        internal void TestAddOpponent(int fakeId)
        {
            var s = State;
            int n = SlotCount;
            if (n >= MaxSlots || SlotOf(fakeId) >= 0)
            {
                return;
            }

            TestMembers.Add(fakeId);
            s.Set(IntSlots + n, ((fakeId + 1) & 0xFFFFF) | (1 << 20));
            s.Set(IntCount, n + 1);
            s.Set(IntFlags, s.Get(IntFlags) & ~1);
        }

        internal void TestClaimHit(int victim, int thrower, int ball, int throwId)
        {
            SendClaim(EvHit, Key(ball, throwId), victim, new[]
            {
                (byte)ball, (byte)(throwId & 0xFF), (byte)(throwId >> 8),
                (byte)(victim & 0xFF), (byte)((victim >> 8) & 0xFF),
                (byte)(thrower & 0xFF), (byte)((thrower >> 8) & 0xFF),
            });
        }
    }
}
