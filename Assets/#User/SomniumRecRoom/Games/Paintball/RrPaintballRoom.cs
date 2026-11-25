using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// Rec Room's Paintball as it was at the shutdown: the 2025 lobby and all seven maps (River, Homestead, Quarry,
    /// Clearcut, Spillway, Sunset Drive-In, Merry Midway), each rebuilt from Rec Room's own level in the Android
    /// build (<see cref="RrPaintballLevel"/>), with the final rules (rr_latest_rgames_action.md 1.2):
    /// - 4 v 4, one hit and you are out (MaxHealth 1), respawn after 5 s at the team spawn farthest from the
    ///   opponents, 5 s of spawn protection;
    /// - Capture the Flag (3 captures or 600 s) or Team Battle (50 hits or 600 s);
    /// - START only (AutomaticGameStartSupported = 0) with at least 2 players (ManualGameStartRequirement
    ///   MinPlayerCount 2, one per team; the user's call: the final rules, with a minimum small groups reach);
    ///   GameStartDelay 10 s is the countdown, EndGameResultsDuration 15 s the round-over plus match-over;
    /// - only head and torso count (RrHitVolumes has only those two volumes);
    /// - bullets never hurt teammates, but grenade, launcher and mine splash does
    ///   (FriendlyFireSettings.GrenadesCanDamageTeammates = 1): friendly fire is on for the shooter and each
    ///   victim vetoes a teammate's non-splash hit (<see cref="AcceptHit"/>);
    /// - the weapons lie where each level has them (its "Weapons" set: pistols, shotguns, scope rifles, the burst
    ///   rifle on Clearcut, the paint launcher on Spillway, the paint thrower on Drive-In, grenades and shields),
    ///   plus Drive-In's six paint-mine piles and a Splat Hammer in each base on Merry Midway; the pistol is handed
    ///   out at spawn.
    ///
    /// One map exists at a time: the one chosen in the lobby is built in the background (560 m south of the lobby)
    /// and replaced when another is chosen; the area the local player is not in is hidden. Everyone in the
    /// instance builds the same map from State.Map, so the pickups' indices agree.
    ///
    /// Networking: phase, map, mode and scores are instance ints (authority only); flags as before (custom ints
    /// 0-11, <see cref="RrPaintballFlags"/>); votes and team choice are GameRequests repeated until the ints or the
    /// presence show them; pickups are <see cref="EvRack"/> (map, spot); blasts <see cref="EvBoom"/>.
    /// </summary>
    public sealed class RrPaintballRoom : RrGameRoom
    {
        public const string Pistol = "(PaintballGun)";
        public const string Rifle = "(PaintballAssaultRifle)";
        public const string Shotgun = "(PaintballShotgun)";
        public const string Sniper = "(PaintballRifleScoped)";
        public const string Launcher = "(PaintballGrenadeLauncher)";
        public const string Thrower = "(Paintball_PaintThrower)";
        public const string Shield = "(PaintballShield)";
        public const string Grenade = "(Grenade)";
        public const string Mine = "(ProximityMine)";
        public const string Hammer = "(Paintball_SplatHammer)";

        public const int ModeCtf = 0, ModeTeamBattle = 1;
        public const int HitsToWin = 50;
        public const int MinPlayers = 2;

        /// <summary>Game events (>= 64): flag requests to the authority, pickups and blasts to everyone.</summary>
        public const byte EvFlag = 64, EvRack = 65, EvBoom = 66;

        /// <summary>GameRequest codes (RrGameEvents.GameRequest): map and mode votes.</summary>
        public const byte ReqMap = 16, ReqMode = 17;

        /// <summary>The launcher's wire tool id (RrWeaponSpec.KnownTools index): its splash may hit teammates.</summary>
        public const byte LauncherTool = 4;

        private static readonly string[] PistolLoadout = { Pistol };

        public static RrPaintballRoom Current { get; private set; }

        public readonly RrPaintballMap[] Maps = new RrPaintballMap[RrPaintballMaps.Count];
        public RrPaintballMap Lobby { get; private set; }
        public RrPaintballFlags Flags { get; private set; }
        /// <summary>Rec Room's game-start hologram in the lobby (RrGameConsole at the level's second projector).</summary>
        internal RrGameConsole Hologram;
        internal TextMeshPro LobbyNext;
        internal TextMeshPro LobbyStatus;
        internal readonly List<TextMeshPro> MapLabels = new List<TextMeshPro>();
        internal readonly List<TextMeshPro> ModeLabels = new List<TextMeshPro>();
        private readonly List<RrPaintballRack> _lobbyRacks = new List<RrPaintballRack>();
        private readonly List<RrPaintballRack> _fieldRacks = new List<RrPaintballRack>();
        private readonly List<KeyValuePair<float, string>> _vo = new List<KeyValuePair<float, string>>();
        private readonly List<TextMeshPro> _baseBoards = new List<TextMeshPro>();
        private RrPaintballLevel _lobbyLevel;
        private RrPaintballLevel _mapLevel;
        private Transform _fieldRoot;
        private RrPaintballMarkers _markers;
        private RrPaintballBuilder _builder;
        private bool _subscribed;
        private int _spawnedTeam = -2;
        private float _spawnAt = -1f;
        private float _nextVote;
        private int _wantMap = -1, _wantMode = -1;
        private int _countdownSaid = -1;
        private int _lastRadioMap = -1;
        private int _pendingMap = -1;
        private float _switchAt;
        private bool _onMap;
        private float _pistolAt = -1f;
        private float _lobbyFloor = float.NaN;
        private float _nextBoundsCheck;
        private string _needPlayersUntilText;
        private float _needPlayersUntil;

        // ---- rules (PaintballCTF / TeamBattle configs, final build) ----

        protected override int MinPlayersToStart
        {
            get { return MinPlayers; }
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
            get { return 600f; }
        }

        protected override float RoundOverSeconds
        {
            get { return 4f; }
        }

        protected override float MatchOverSeconds
        {
            get { return 11f; }
        }

        protected override string PlayMusic
        {
            get { return RrPaintballMaps.Music[MapIndex]; }
        }

        protected override string LobbyMusic
        {
            get { return RrPaintballMaps.Radios[MapIndex]; }
        }

        protected override string[] Loadout
        {
            get { return PistolLoadout; }
        }

        protected override int PlayerMaxHealth
        {
            get { return 1; }
        }

        protected override float RespawnSeconds
        {
            get { return 5f; }
        }

        protected override float RespawnInvincibility
        {
            get { return 5f; }
        }

        public int MapIndex
        {
            get { return Mathf.Clamp(State.Map, 0, RrPaintballMaps.Count - 1); }
        }

        public int Mode
        {
            get { return State.Mode == ModeTeamBattle ? ModeTeamBattle : ModeCtf; }
        }

        public RrPaintballMap ActiveMap
        {
            get { return Maps[MapIndex] ?? Maps[0]; }
        }

        /// <summary>The map level exists and is fully built (flags, pickups and spawns are known).</summary>
        public bool MapReady
        {
            get { return _mapLevel != null && _mapLevel.Done && _mapLevel.Alive && _mapLevel.Map.Index == MapIndex && _mapLevel.Map.Built; }
        }

        public bool OnMap
        {
            get { return _onMap; }
        }

        internal RrPaintballLevel LobbyLevel
        {
            get { return _lobbyLevel; }
        }

        internal RrPaintballLevel MapLevel
        {
            get { return _mapLevel; }
        }

        public static string GunName(string prefab)
        {
            switch (prefab)
            {
                case Pistol: return "Pistol";
                case Rifle: return "Burst Rifle";
                case Shotgun: return "Shotgun";
                case Sniper: return "Scope Rifle";
                case Launcher: return "Paint Launcher";
                case Thrower: return "Paint Thrower";
                case Shield: return "Shield";
                case Grenade: return "Grenade";
                case Mine: return "Paint Mine";
                case Hammer: return "Splat Hammer";
                default: return prefab;
            }
        }

        // ------------------------------------------------------------------ build

        public override IEnumerator Build()
        {
            Current = this;
            float t0 = Time.realtimeSinceStartup;
            _builder = new RrPaintballBuilder(transform);
            BuildProgress = 0.02f;
            yield return RrPortal.Prepare();
            for (int i = 0; i < RrPaintballMaps.Count; i++)
            {
                Maps[i] = RrPaintballMaps.Create(i);
            }

            Lobby = RrPaintballMaps.CreateLobby();
            _lobbyLevel = new RrPaintballLevel(Lobby);
            BuildProgress = 0.05f;
            var build = _lobbyLevel.Build(transform, RrPaintballMaps.LobbyText(), RrPaintballLobby.SkipNames);
            while (build.MoveNext())
            {
                BuildProgress = 0.05f + 0.8f * _lobbyLevel.Progress;
                yield return build.Current;
            }

            RrPaintballLobby.Build(_builder, this, _lobbyLevel);
            BuildProgress = 0.9f;
            Flags = RrPaintballFlags.Create(this, _builder);
            var mk = new GameObject("Team Markers");
            mk.transform.SetParent(transform, false);
            _markers = mk.AddComponent<RrPaintballMarkers>();
            yield return _builder.WaitAll(30f, p => BuildProgress = 0.9f + 0.08f * p);
            RrLog.Info("Paintball", "built the lobby from Rec Room's level in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s: " +
                                    (_lobbyLevel.Kit != null ? _lobbyLevel.Kit.Renderers + " renderers" : "no kit") + ", " + _builder.Spawned + " props (" + _builder.Failed +
                                    " missing), " + _lobbyRacks.Count + " practice spots, flags " + (Flags.Ready ? "ready" : "MISSING") + "; maps are built when chosen");
            BuildProgress = 1f;
        }

        internal void RegisterLobbyRack(RrPaintballRack rack)
        {
            _lobbyRacks.Add(rack);
        }

        internal bool CanTakeGuns(bool field)
        {
            if (!field)
            {
                return true;
            }

            return Phase == RrPhase.Playing && (RrHealth.Local == null || !RrHealth.Local.IsOut);
        }

        // ------------------------------------------------------------------ the map level

        /// <summary>Every client: keep the chosen map built (after a short settle while votes change it).</summary>
        private void KeepMapBuilt()
        {
            int want = MapIndex;
            if (_mapLevel != null && _mapLevel.Map.Index == want)
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
            TearDownMap();
            _mapLevel = new RrPaintballLevel(Maps[want]);
            StartCoroutine(BuildMap(_mapLevel));
        }

        private void TearDownMap()
        {
            for (int i = 0; i < _fieldRacks.Count; i++)
            {
                if (_fieldRacks[i] != null)
                {
                    Destroy(_fieldRacks[i].gameObject);
                }
            }

            _fieldRacks.Clear();
            _baseBoards.Clear();
            if (_fieldRoot != null)
            {
                Destroy(_fieldRoot.gameObject);
                _fieldRoot = null;
            }

            if (_mapLevel != null)
            {
                _mapLevel.Destroy();
                _mapLevel = null;
            }

            if (Flags != null)
            {
                Flags.ResetSeen();
            }
        }

        private IEnumerator BuildMap(RrPaintballLevel level)
        {
            RrLog.Info("Paintball", "building " + level.Map.Name + " (Rec Room's level, " + RrPaintballMaps.Keys[level.Map.Index] + ")");
            yield return level.Build(transform, RrPaintballMaps.Text(level.Map.Index), null);
            if (level != _mapLevel || !level.Alive || !level.Map.Built)
            {
                yield break;
            }

            var map = level.Map;
            _fieldRoot = RrBuild.Group(transform, "Field " + map.Key).transform;
            Pickups(map);
            BaseBoards(map);
            if (!string.IsNullOrEmpty(RrPaintballMaps.Ambience[map.Index]))
            {
                RrSound.Loop(RrPaintballMaps.Ambience[map.Index], _fieldRoot, 0.35f, 400f);
            }

            // The travel rescue (10 m under the room's lowest floor) must not catch a player on a map below the lobby.
            if (float.IsNaN(_lobbyFloor))
            {
                _lobbyFloor = LowestFloor;
            }

            float kill = float.IsNegativeInfinity(map.KillY) ? map.Slot.y - 40f : map.KillY;
            float mapFloor = transform.TransformPoint(new Vector3(0f, kill, 0f)).y - 2f;
            LowestFloor = float.IsNaN(_lobbyFloor) ? mapFloor : Mathf.Min(_lobbyFloor, mapFloor);
            ApplyVisibility();
            RrLog.Info("Paintball", map.Name + " ready: " + _fieldRacks.Count + " pickups, " + _baseBoards.Count + " boards, kill floor y " + kill.ToString("0.0") +
                                    " (room), lowest floor y " + LowestFloor.ToString("0.0"));
            if ((Phase == RrPhase.Countdown || Phase == RrPhase.Playing) && _spawnedTeam < 0 && _spawnAt < 0f)
            {
                _spawnAt = Time.time;
            }
        }

        /// <summary>
        /// The level's own weapon spots: its "Weapons" set (the one active in a normal match; the mod sets are
        /// not used), Drive-In's six mine piles (MineGroup sets, one refilling pickup each) and, on Merry Midway, the
        /// Splat Hammer (research: the hammer's map) at the HammersOnly spot nearest each base (GUESS).
        /// </summary>
        private void Pickups(RrPaintballMap map)
        {
            var mines = new List<Vector3>();
            var hammers = new List<RrPaintballMap.Weapon>();
            for (int i = 0; i < map.Weapons.Count; i++)
            {
                var w = map.Weapons[i];
                if (w.Set == "Weapons")
                {
                    AddField(map, w.Prefab, w.Pose);
                }
                else if (map.Index == RrPaintballMaps.DriveIn && w.Set.StartsWith("MineGroup"))
                {
                    bool near = false;
                    foreach (var m in mines)
                    {
                        near |= Vector3.Distance(m, w.Pose.position) < 1.5f;
                    }

                    if (!near)
                    {
                        mines.Add(w.Pose.position);
                        AddField(map, Mine, w.Pose);
                    }
                }
                else if (map.Index == RrPaintballMaps.MerryMidway && w.Set == "HammersOnlyWeapons" && w.Prefab == Hammer)
                {
                    hammers.Add(w);
                }
            }

            for (int t = 0; t < 2 && hammers.Count > 0; t++)
            {
                int best = 0;
                for (int i = 1; i < hammers.Count; i++)
                {
                    if (Vector3.Distance(hammers[i].Pose.position, map.FlagStands[t]) < Vector3.Distance(hammers[best].Pose.position, map.FlagStands[t]))
                    {
                        best = i;
                    }
                }

                AddField(map, Hammer, hammers[best].Pose);
                hammers.RemoveAt(best);
            }
        }

        private void AddField(RrPaintballMap map, string prefab, Pose roomLocal)
        {
            var rack = RrPaintballRack.CreateSpot(this, _builder, _fieldRoot, roomLocal.position, roomLocal.rotation, prefab, true);
            rack.Index = _fieldRacks.Count;
            rack.MapIndex = map.Index;
            _fieldRacks.Add(rack);
        }

        /// <summary>Our scoreboards on the level's own base boards (Scoreboard_V2), facing the way the board did.</summary>
        private void BaseBoards(RrPaintballMap map)
        {
            // A big board came from the converter as several voxel boxes: one of ours per 6 m cluster, at the
            // cluster's centre and as wide as the cluster.
            var used = new bool[map.Boards.Count];
            for (int i = 0; i < map.Boards.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                var b = new Bounds(map.Boards[i].position, map.BoardSizes[i] * 2f);
                for (int j = i + 1; j < map.Boards.Count; j++)
                {
                    if (!used[j] && Vector3.Distance(map.Boards[i].position, map.Boards[j].position) < 6f)
                    {
                        used[j] = true;
                        b.Encapsulate(new Bounds(map.Boards[j].position, map.BoardSizes[j] * 2f));
                    }
                }

                var pose = new Pose(b.center, map.Boards[i].rotation);
                var e = b.extents;
                float width = Mathf.Clamp(Mathf.Max(e.x, e.z) * 2f * 0.9f, 1.6f, 4f);
                // The board mesh's readable side: its local +z (Scoreboard_V2 is stored with the text on +z).
                var rot = RrPaintballMap.Flat(pose.rotation);
                var board = RrScoreboard.Create(_fieldRoot, pose.position + rot * new Vector3(0f, 0f, 0.08f), rot, "PAINTBALL", 2, width);
                var line = RrBuild.Sign(board.transform, "Mode", "", new Vector3(0f, -0.72f, 0.03f), Quaternion.Euler(0f, 180f, 0f), 1.2f, Color.white, true, width);
                _baseBoards.Add(line);
            }
        }

        /// <summary>Shows the area the local player is in (the lobby or the map) and hides the other.</summary>
        private void ApplyVisibility()
        {
            if (_lobbyLevel != null && _lobbyLevel.Root != null)
            {
                _lobbyLevel.Root.SetActive(!_onMap);
            }

            var lobbyExtras = transform.Find("Lobby");
            if (lobbyExtras != null)
            {
                lobbyExtras.gameObject.SetActive(!_onMap);
            }

            if (_mapLevel != null && _mapLevel.Root != null && _mapLevel.Done)
            {
                _mapLevel.Root.SetActive(_onMap);
            }

            if (_fieldRoot != null)
            {
                _fieldRoot.gameObject.SetActive(_onMap);
            }
        }

        internal void SetArea(bool onMap)
        {
            if (_onMap == onMap)
            {
                return;
            }

            _onMap = onMap;
            ApplyVisibility();
        }

        // ------------------------------------------------------------------ arrival / leaving

        /// <summary>Arrival: Rec Room's own pregame spawn points in the lobby's locker building.</summary>
        public override Pose Spawn(int index)
        {
            if (Lobby != null && Lobby.Pregame.Count > 0)
            {
                var p = Lobby.Pregame[((index % Lobby.Pregame.Count) + Lobby.Pregame.Count) % Lobby.Pregame.Count];
                return new Pose(p.position + Vector3.up * 0.05f, p.rotation);
            }

            return new Pose(new Vector3(0f, 0.05f, 0f), Quaternion.Euler(0f, 162f, 0f));
        }

        public override void OnLocalEnter()
        {
            Current = this;
            base.OnLocalEnter();
            if (!_subscribed)
            {
                _subscribed = true;
                RrNet.On(EvFlag, OnFlagEvent);
                RrNet.On(EvRack, OnRackEvent);
                RrNet.On(EvBoom, OnBoomEvent);
                RrCombat.OnDeath += OnDeath;
                RrHealth.Ensure().OnRespawned += OnRespawned;
                RrHealth.Local.OnOut += OnLocalOut;
            }

            RrCombat.FriendlyFire = true;
            _spawnedTeam = -2;
            _onMap = false;
            ApplyVisibility();
            if (Flags != null)
            {
                Flags.ResetSeen();
            }

            RrLog.Info("Paintball", "entered: " + RrPaintballMaps.Names[MapIndex] + ", " + ModeName(Mode) + ", phase " + Phase + ", authority " + IsAuthority +
                                    ", members " + Members().Count);
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            Unsubscribe();
            RrCombat.FriendlyFire = false;
            RrPlayer.RequestPlayMode("paintball", false);
            if (RrHealth.Local != null)
            {
                RrHealth.Local.RespawnPoint = null;
                RrHealth.Local.Accepts = null;
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            RrNet.Off(EvFlag, OnFlagEvent);
            RrNet.Off(EvRack, OnRackEvent);
            RrNet.Off(EvBoom, OnBoomEvent);
            RrCombat.OnDeath -= OnDeath;
            if (RrHealth.Local != null)
            {
                RrHealth.Local.OnRespawned -= OnRespawned;
                RrHealth.Local.OnOut -= OnLocalOut;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Current == this)
            {
                Current = null;
            }

            RrPaintballLevel.Surfaces.Clear();
            RrPaintballLevel.Meshes.Clear();
        }

        public static string ModeName(int mode)
        {
            return mode == ModeTeamBattle ? "Team Battle" : "Capture the Flag";
        }

        // ------------------------------------------------------------------ authority

        protected override void AuthorityMatchSetup()
        {
            if (Flags != null)
            {
                Flags.AuthorityReset();
            }

            RrLog.Info("Paintball", "match starting: " + RrPaintballMaps.Names[MapIndex] + ", " + ModeName(Mode) + ", " + Members().Count + " players");
        }

        protected override void AuthorityRoundStart(int round)
        {
            if (Flags != null)
            {
                Flags.AuthorityReset();
            }
        }

        protected override void AuthorityPlaying()
        {
            if (Mode == ModeCtf && Flags != null)
            {
                Flags.AuthorityStep();
            }
        }

        /// <summary>Authority: a team captured the enemy flag (3 wins).</summary>
        internal void AuthorityCapture(int team)
        {
            AddTeamScore(team, 1);
            if (State.TeamScore(team) >= RrPaintballFlags.CapturesToWin)
            {
                EndRound(team);
            }
        }

        protected override void AuthorityRoundOver(int winner)
        {
            RrLog.Info("Paintball", "round over: " + (winner >= 0 ? RrTeams.Name(winner) + " wins" : "tie") + " (" + State.TeamScore(0) + " - " + State.TeamScore(1) + ")");
        }

        /// <summary>Authority, back in the lobby: the same map and mode stay chosen (the lobby picks the next one).</summary>
        protected override void AuthorityBackToLobby()
        {
            if (Flags != null)
            {
                Flags.AuthorityReset();
            }
        }

        protected override void AuthorityRequest(int playerId, byte request, byte arg)
        {
            if (Phase != RrPhase.Lobby)
            {
                return;
            }

            if (request == ReqMap && arg < RrPaintballMaps.Count)
            {
                State.Map = arg;
            }
            else if (request == ReqMode && arg <= 1)
            {
                State.Mode = arg;
            }
        }

        private void OnFlagEvent(int sender, byte[] body)
        {
            if (IsAuthority && Flags != null && body != null && body.Length >= 2)
            {
                Flags.AuthorityRequest(sender, body[0], body[1]);
            }
        }

        /// <summary>Another member took a field pickup: body = map, spot.</summary>
        private void OnRackEvent(int sender, byte[] body)
        {
            if (sender == RrNet.LocalId || body == null || body.Length < 2 || body[0] != MapIndex)
            {
                return;
            }

            int i = body[1];
            if (i < _fieldRacks.Count && _fieldRacks[i] != null)
            {
                _fieldRacks[i].Hide();
            }
        }

        private void OnBoomEvent(int sender, byte[] body)
        {
            if (sender != RrNet.LocalId)
            {
                RrPaintballBoom.Receive(body);
            }
        }

        /// <summary>Every client sees every out (Death events); the authority counts Team Battle hits and drops flags.</summary>
        private void OnDeath(RrCombat.DeathInfo d)
        {
            if (!IsAuthority || Phase != RrPhase.Playing)
            {
                return;
            }

            if (Flags != null)
            {
                Vector3 at = d.Point;
                if (d.Victim == RrNet.LocalId)
                {
                    at = RrPlayer.FeetPosition;
                }
                else
                {
                    var r = RrRemotePlayers.Get(d.Victim);
                    if (r != null && r.Root != null)
                    {
                        at = r.Root.position;
                    }
                }

                Flags.AuthorityDrop(d.Victim, at);
            }

            if (Mode != ModeTeamBattle)
            {
                return;
            }

            int killerTeam = RrCombat.TeamOf(d.Killer);
            int victimTeam = RrCombat.TeamOf(d.Victim);
            if (killerTeam >= 0 && killerTeam != victimTeam)
            {
                AuthorityCountHit(killerTeam);
            }
        }

        internal void AuthorityCountHit(int team)
        {
            AddTeamScore(team, 1);
            if (State.TeamScore(team) >= HitsToWin)
            {
                EndRound(team);
            }
        }

        // ------------------------------------------------------------------ every client

        /// <summary>
        /// The victim's rule on a hit (RrHealth.Accepts): an opponent's hit always counts; a teammate's only from a
        /// splash tool (grenade, mine, paint launcher: GrenadesCanDamageTeammates = 1).
        /// </summary>
        public static bool AcceptHit(int damage, int attacker, byte tool)
        {
            if (attacker < 0 || attacker == RrNet.LocalId)
            {
                return attacker < 0;
            }

            int a = RrCombat.TeamOf(attacker), me = RrTeams.LocalTeam;
            if (a < 0 || me < 0 || a != me)
            {
                return true;
            }

            return tool == RrPaintballBoom.GrenadeTool || tool == RrPaintballBoom.MineTool || tool == LauncherTool;
        }

        protected override void OnPhase(RrPhase old, RrPhase now)
        {
            _countdownSaid = -1;
            switch (now)
            {
                case RrPhase.Lobby:
                    if (old == RrPhase.MatchOver || old == RrPhase.RoundOver || old == RrPhase.Playing || old == RrPhase.Countdown)
                    {
                        GoToLobby();
                    }

                    RestockRacks();
                    RrPlayer.RequestPlayMode("paintball", false);
                    _spawnedTeam = -2;
                    break;
                case RrPhase.Countdown:
                    RrLoadout.Clear();
                    RestockRacks();
                    RrMusic.Play("PtblRiver_10Sec", 0.5f, false);
                    SayAt(Mode == ModeCtf ? RrAnnouncer.CaptureTheFlag : RrAnnouncer.TeamBattle, Time.time + 1.2f);
                    _spawnAt = Time.time + 0.6f;
                    _spawnedTeam = -2;
                    RrPlayer.RequestPlayMode("paintball", true);
                    break;
                case RrPhase.Playing:
                    if (_spawnedTeam < 0)
                    {
                        // Joined in progress (or the countdown was missed): pick the smaller team, then spawn.
                        if (RrTeams.LocalTeam < 0 || RrTeams.LocalTeam > 1)
                        {
                            RrTeams.SetLocalTeam(SmallerTeam());
                        }

                        _spawnAt = Time.time;
                    }

                    if (RrHealth.Local != null)
                    {
                        RrHealth.Local.RespawnPoint = LocalRespawnPose;
                        RrHealth.Local.Accepts = AcceptHit;
                    }

                    RrPlayer.RequestPlayMode("paintball", true);
                    break;
                case RrPhase.RoundOver:
                {
                    int blue = State.TeamScore(0), red = State.TeamScore(1);
                    SayAt(RrAnnouncer.TeamWins(blue == red ? -1 : blue > red ? 0 : 1), Time.time + 1.6f);
                    RrPlayer.RequestPlayMode("paintball", false);
                    break;
                }
                case RrPhase.MatchOver:
                    SayAt(RrAnnouncer.MatchOver, Time.time + 2f);
                    break;
            }
        }

        protected override void OnTeamsAssigned()
        {
            // A late team assignment moves the player to the right spawn room.
            if ((Phase == RrPhase.Countdown || Phase == RrPhase.Playing) && _spawnedTeam >= 0 && _spawnedTeam != RrTeams.LocalTeam)
            {
                _spawnAt = Time.time;
            }
        }

        protected override void OnTick(RrInstanceState s)
        {
            if (!Built)
            {
                return;
            }

            KeepMapBuilt();
            if (_spawnAt > 0f && Time.time >= _spawnAt)
            {
                if (MapReady)
                {
                    _spawnAt = -1f;
                    SpawnInBase();
                }
                else
                {
                    _spawnAt = Time.time + 0.25f;
                }
            }

            if (_pistolAt > 0f && Time.time >= _pistolAt)
            {
                _pistolAt = -1f;
                if (Phase == RrPhase.Playing && !RrLoadout.Busy && RrLoadout.Given.Count == 0 && (RrHealth.Local == null || !RrHealth.Local.IsOut))
                {
                    RrLoadout.GiveAll(PistolLoadout);
                }
            }

            if (s.Phase == RrPhase.Countdown)
            {
                int left = Mathf.CeilToInt(s.TimeRemaining);
                if (left != _countdownSaid && left >= 1 && left <= 5)
                {
                    _countdownSaid = left;
                    SayAt(left + "up", Time.time);
                }
            }

            if (s.Phase == RrPhase.Lobby)
            {
                RetryVotes(s);
                if (_lastRadioMap != MapIndex)
                {
                    if (_lastRadioMap >= 0 && !string.IsNullOrEmpty(LobbyMusic))
                    {
                        RrMusic.Play(LobbyMusic);
                    }

                    _lastRadioMap = MapIndex;
                }
            }

            if (s.Phase == RrPhase.Playing && _onMap && Time.time >= _nextBoundsCheck)
            {
                _nextBoundsCheck = Time.time + 0.25f;
                FellOff();
            }

            PlayVo();
            UpdateSigns(s);
        }

        /// <summary>Rec Room's KillFloor under each map: a player below it (or far outside the map) is out.</summary>
        private void FellOff()
        {
            var h = RrHealth.Local;
            var map = ActiveMap;
            if (h == null || h.IsOut || !MapReady)
            {
                return;
            }

            Vector3 feet = transform.InverseTransformPoint(RrPlayer.FeetPosition);
            // The KillFloor volume catches the body, not the feet: Spillway's spawns stand 0.3 m inside its top.
            bool below = !float.IsNegativeInfinity(map.KillY) && feet.y + 1.2f < map.KillY;
            bool outside = !map.Contains(feet, 60f);
            if (below || outside)
            {
                RrLog.Info("Paintball", "fell off " + map.Name + " (feet " + feet.ToString("F1") + ", kill floor " + map.KillY.ToString("0.0") + "): out");
                h.GoOut(RrCombat.NoPlayer, RrCombat.UnknownTool, RrPlayer.FeetPosition);
            }
        }

        private void SpawnInBase()
        {
            int team = RrTeams.LocalTeam;
            if (team < 0 || team > 1)
            {
                team = RrTeams.TeamOf(RrNet.LocalId);
            }

            if (team < 0 || team > 1)
            {
                team = SmallerTeam();
                RrTeams.SetLocalTeam(team);
            }

            _spawnedTeam = team;
            var pose = SpawnPoseFor(team, Members().IndexOf(RrNet.LocalId));
            SetArea(true);
            RrPlayer.Teleport(pose.position, pose.rotation.eulerAngles.y);
            SayAt(team == 0 ? RrAnnouncer.BlueTeam : RrAnnouncer.RedTeam, Time.time + 3.2f);
            RrLog.Info("Paintball", "spawned in the " + RrTeams.Name(team) + " base on " + RrPaintballMaps.Names[MapIndex] + " at " +
                                    transform.InverseTransformPoint(pose.position).ToString("F1") + " (room-local)");
        }

        private void GoToLobby()
        {
            SetArea(false);
            var pose = WorldSpawn(Mathf.Max(0, Members().IndexOf(RrNet.LocalId)));
            RrPlayer.Teleport(pose.position, pose.rotation.eulerAngles.y);
        }

        private Pose SpawnPoseFor(int team, int index)
        {
            var p = ActiveMap.SpawnPose(team, Mathf.Max(0, index));
            return new Pose(transform.TransformPoint(p.position + Vector3.up * 0.05f), transform.rotation * p.rotation);
        }

        private Pose LocalRespawnPose()
        {
            return RespawnPoseFor(RrTeams.LocalTeam);
        }

        /// <summary>Rec Room's 2020 rule: respawn at the team spawn point farthest from the nearest opponent.</summary>
        public Pose RespawnPoseFor(int team)
        {
            team = Mathf.Clamp(team, 0, 1);
            var list = ActiveMap.Spawns[team];
            if (list.Count == 0 || !MapReady)
            {
                return WorldSpawn(0);
            }

            int best = Random.Range(0, list.Count);
            float bestScore = -1f;
            var members = Members();
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 w = transform.TransformPoint(list[i].position);
                float nearest = 1e6f;
                for (int m = 0; m < members.Count; m++)
                {
                    int id = members[m];
                    if (id == RrNet.LocalId || RrCombat.TeamOf(id) == team)
                    {
                        continue;
                    }

                    var r = RrRemotePlayers.Get(id);
                    if (r != null && r.Root != null)
                    {
                        nearest = Mathf.Min(nearest, Vector3.Distance(w, r.Root.position));
                    }
                }

                float score = nearest + Random.value;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            var p = list[best];
            return new Pose(transform.TransformPoint(p.position + Vector3.up * 0.05f), transform.rotation * p.rotation);
        }

        public override Pose RescuePose(int index)
        {
            return _onMap && MapReady && (Phase == RrPhase.Playing || Phase == RrPhase.Countdown) ? RespawnPoseFor(RrTeams.LocalTeam) : WorldSpawn(index);
        }

        private int SmallerTeam()
        {
            int blue = 0, red = 0;
            var members = Members();
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] == RrNet.LocalId)
                {
                    continue;
                }

                int t = RrCombat.TeamOf(members[i]);
                if (t == 0)
                {
                    blue++;
                }
                else if (t == 1)
                {
                    red++;
                }
            }

            return red < blue ? 1 : 0;
        }

        private void OnLocalOut(int killer, byte tool)
        {
            if (Phase == RrPhase.Playing)
            {
                SayAt("Out", Time.time + 0.25f);
            }
        }

        /// <summary>After an out the player comes back with the pistol (field weapons are lost, as at spawn).</summary>
        private void OnRespawned()
        {
            if (Phase != RrPhase.Playing)
            {
                return;
            }

            bool pistol = false;
            for (int i = 0; i < RrLoadout.Given.Count; i++)
            {
                var go = RrLoadout.Given[i];
                if (go != null && go.name.Contains(Pistol))
                {
                    pistol = true;
                }
            }

            if (!pistol && !RrLoadout.Busy)
            {
                RrLoadout.Clear();
                RrLoadout.GiveAll(PistolLoadout);
            }
        }

        /// <summary>A grenade or mine left the hand: the pistol comes back shortly after.</summary>
        internal void GivePistolSoon()
        {
            _pistolAt = Time.time + 0.6f;
        }

        private void RestockRacks()
        {
            for (int i = 0; i < _fieldRacks.Count; i++)
            {
                if (_fieldRacks[i] != null)
                {
                    _fieldRacks[i].Restock();
                }
            }
        }

        // ---- lobby console ----

        public void VoteMap(int map)
        {
            if (Phase != RrPhase.Lobby || map < 0 || map >= RrPaintballMaps.Count)
            {
                return;
            }

            _wantMap = map;
            _nextVote = 0f;
            RrLog.Info("Paintball", "map vote: " + RrPaintballMaps.Names[map]);
        }

        public void VoteMode(int mode)
        {
            if (Phase != RrPhase.Lobby)
            {
                return;
            }

            _wantMode = mode;
            _nextVote = 0f;
            RrLog.Info("Paintball", "mode vote: " + ModeName(mode));
        }

        public void JoinTeam(int team)
        {
            if (Phase != RrPhase.Lobby || team < 0 || team > 1 || RrTeams.LocalTeam == team)
            {
                return;
            }

            RrTeams.SetLocalTeam(team);
            RrSound.PlayUi("Join", 0.8f);
            SayAt(team == 0 ? RrAnnouncer.BlueTeam : RrAnnouncer.RedTeam, Time.time + 0.3f);
            RrLog.Info("Paintball", "joined the " + RrTeams.Name(team) + " team in the lobby");
        }

        /// <summary>The START button: starts the match when at least two players are in the instance.</summary>
        public void PressStart()
        {
            if (Phase != RrPhase.Lobby)
            {
                return;
            }

            int n = Members().Count;
            if (n < MinPlayers)
            {
                _needPlayersUntil = Time.time + 4f;
                _needPlayersUntilText = "NEED " + MinPlayers + " PLAYERS TO START";
                RrLog.Info("Paintball", "START pressed with " + n + " player(s): " + MinPlayers + " needed");
                return;
            }

            RrSound.PlayUi("Start", 0.8f);
            RrLog.Info("Paintball", "START pressed (" + n + " in the lobby, " + RrPaintballMaps.Names[MapIndex] + ", " + ModeName(Mode) + ")");
            RequestStart();
        }

        /// <summary>Votes go to the authority and are repeated every 0.4 s until the ints show them.</summary>
        private void RetryVotes(RrInstanceState s)
        {
            if (_wantMap >= 0 && s.Map == _wantMap)
            {
                _wantMap = -1;
            }

            if (_wantMode >= 0 && s.Mode == _wantMode)
            {
                _wantMode = -1;
            }

            if ((_wantMap < 0 && _wantMode < 0) || Time.time < _nextVote)
            {
                return;
            }

            _nextVote = Time.time + 0.4f;
            if (IsAuthority)
            {
                if (_wantMap >= 0)
                {
                    s.Map = _wantMap;
                }

                if (_wantMode >= 0)
                {
                    s.Mode = _wantMode;
                }

                return;
            }

            if (_wantMap >= 0)
            {
                RrNet.Send(RrGameEvents.GameRequest, new[] { ReqMap, (byte)_wantMap }, true);
            }

            if (_wantMode >= 0)
            {
                RrNet.Send(RrGameEvents.GameRequest, new[] { ReqMode, (byte)_wantMode }, true);
            }
        }

        private void UpdateSigns(RrInstanceState s)
        {
            string mode = Mode == ModeCtf ? "CAPTURE THE FLAG" : "TEAM BATTLE";
            string goal = Mode == ModeCtf ? "FIRST TO 3 CAPTURES" : "FIRST TO " + HitsToWin + " HITS";
            if (LobbyNext != null)
            {
                LobbyNext.text = mode + "  -  " + RrPaintballMaps.Names[MapIndex].ToUpperInvariant();
            }

            for (int i = 0; i < MapLabels.Count; i++)
            {
                if (MapLabels[i] != null)
                {
                    MapLabels[i].color = i == MapIndex ? RrLook.Yellow : Color.white;
                }
            }

            for (int i = 0; i < ModeLabels.Count; i++)
            {
                if (ModeLabels[i] != null)
                {
                    ModeLabels[i].color = i == Mode ? RrLook.Yellow : Color.white;
                }
            }

            if (LobbyStatus != null)
            {
                int n = Members().Count;
                string status;
                switch (s.Phase)
                {
                    case RrPhase.Lobby:
                        if (Time.time < _needPlayersUntil)
                        {
                            status = _needPlayersUntilText;
                        }
                        else if (!MapReady)
                        {
                            status = "LOADING " + RrPaintballMaps.Names[MapIndex].ToUpperInvariant() + "...";
                        }
                        else
                        {
                            status = RrTeams.LocalTeam < 0 ? "PICK A TEAM - PRESS START" : n < MinPlayers ? "WAITING FOR PLAYERS (" + n + "/" + MinPlayers + ")" : "PRESS START";
                        }

                        break;
                    case RrPhase.Countdown:
                        status = "STARTING IN " + Mathf.CeilToInt(s.TimeRemaining);
                        break;
                    default:
                        status = "GAME IN PROGRESS";
                        break;
                }

                LobbyStatus.text = status;
            }

            string line = mode + " - " + goal;
            if (s.Phase == RrPhase.MatchOver || s.Phase == RrPhase.RoundOver)
            {
                int b = s.TeamScore(0), r = s.TeamScore(1);
                line = b == r ? "TIE GAME" : (b > r ? "BLUE" : "RED") + " TEAM WINS";
            }

            for (int i = 0; i < _baseBoards.Count; i++)
            {
                if (_baseBoards[i] != null)
                {
                    _baseBoards[i].text = line;
                }
            }
        }

        // ---- announcer queue (clips must not talk over each other) ----

        internal void SayAt(string clip, float time)
        {
            if (!string.IsNullOrEmpty(clip))
            {
                _vo.Add(new KeyValuePair<float, string>(time, clip));
            }
        }

        private float _voFree;

        private void PlayVo()
        {
            if (_vo.Count == 0 || Time.time < _voFree)
            {
                return;
            }

            int pick = -1;
            for (int i = 0; i < _vo.Count; i++)
            {
                if (_vo[i].Key <= Time.time && (pick < 0 || _vo[i].Key < _vo[pick].Key))
                {
                    pick = i;
                }
            }

            if (pick < 0)
            {
                return;
            }

            string clip = _vo[pick].Value;
            _vo.RemoveAt(pick);
            RrAnnouncer.Say(clip);
            _voFree = Time.time + (clip.EndsWith("up") ? 0.9f : 1.4f);
        }

        // ---- harness hooks (RrPaintballTest) ----

        internal void TestStart(int map, int mode)
        {
            if (!IsAuthority)
            {
                return;
            }

            if (Phase != RrPhase.Lobby)
            {
                State.SetPhase(RrPhase.Lobby, 0f);
            }

            State.Map = map;
            State.Mode = mode;
            if (RrTeams.LocalTeam < 0)
            {
                RrTeams.SetLocalTeam(0);
            }

            StartCountdown();
        }

        internal void TestSelectMap(int map)
        {
            if (IsAuthority)
            {
                State.Map = Mathf.Clamp(map, 0, RrPaintballMaps.Count - 1);
                _pendingMap = State.Map;
                _switchAt = 0f;
            }
        }

        internal void TestSkipTimer(float seconds)
        {
            if (IsAuthority)
            {
                State.SetTimer(seconds);
            }
        }

        internal IReadOnlyList<RrPaintballRack> Racks
        {
            get { return _fieldRacks; }
        }

        internal IReadOnlyList<RrPaintballRack> LobbyRacks
        {
            get { return _lobbyRacks; }
        }
    }
}
