using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.LaserTag
{
    /// <summary>
    /// Rec Room's Laser Tag as it was at the shutdown (rr_latest_rgames_action.md 2, rr_shooters.md 1.2 / 4.2), on its two
    /// maps rebuilt from Rec Room's own Android levels (<see cref="RrLaserTagLevel"/>): Hangar and Cyber Junk City, each
    /// with the lobby it has next to its arena. The final rules:
    /// - team battle, 4 v 4: first team to 400 points, or the most points after 480 s (the wrapper's defaults; the wiki's
    ///   "10 minutes" is older text); 10 points per tagged opponent, a self-destruct (your own grenade, falling out) costs
    ///   your team 10 (2019-01);
    /// - 100 health (the combat UI's health bar; GUESS for the number), respawn after 5 s (GUESS) at the team spawn point
    ///   farthest from the opponents, 3 s of spawn invincibility (2019; GUESS that it stayed 3 s);
    /// - no friendly fire (bullets and grenades), but a grenade hurts its thrower (2019-01); head and torso only;
    /// - everyone spawns with the laser pistol; the level's own pickups (burst rifles, shotguns, railguns, grenades)
    ///   and the map's power weapon (Laser Orb Launcher / Beam Cannon; the spot is a GUESS) lie where the level has them;
    /// - START on the lobby's hologram console (at the level's ScoreboardProjector), with the map choice; teams are
    ///   balanced as the countdown starts (10 s). Minimum 1 player so one tester can walk the arena (Rec Room fills
    ///   Laser Tag with bots; this build has none yet).
    ///
    /// One map exists at a time; choosing the other builds it in its own slot (<see cref="RrLaserTagMaps.Anchors"/>) while
    /// the lobby still stands, then moves the players over and frees the old one. Everyone builds the map in State.Map, so
    /// the pickups' indices agree.
    ///
    /// Networking: phase, map and scores are instance ints (authority only); the map vote is a GameRequest repeated until
    /// the int shows it; pickups are <see cref="EvPickup"/> (map, spot), grenade blasts <see cref="EvBlast"/>; hits,
    /// deaths and respawns go through the shared combat kit (RrCombat, RrHealth).
    /// </summary>
    public sealed class RrLaserTagRoom : RrGameRoom
    {
        public const string Pistol = "(Arena_Pistol)";
        public const string Rifle = "(Arena_AutomaticGun)";
        public const string Shotgun = "(Arena_Shotgun)";
        public const string Railgun = "(Arena_RailGun)";
        public const string OrbLauncher = "(Arena_RocketLauncher)";
        public const string BeamGun = "(Arena_PowerWeapon_BeamGun)";
        public const string Grenade = "(Arena_Grenade)";

        public const int ScoreToWin = 400;
        public const int TagPoints = 10;
        public const int SelfDestructPoints = -10;
        public const int MinPlayers = 1;

        /// <summary>Pickups come back 15 s after they are taken (Paintball's spawners; GUESS for Laser Tag), the power weapon after 60 s (GUESS).</summary>
        public const float PickupSeconds = 15f;
        public const float PowerSeconds = 60f;

        /// <summary>Game events (>= 64): a pickup taken (map, spot), a grenade blast (room-local position).</summary>
        public const byte EvPickup = 64, EvBlast = 65;

        /// <summary>GameRequest code (RrGameEvents.GameRequest): the map vote.</summary>
        public const byte ReqMap = 16;

        /// <summary>The grenade's wire tool id (outside RrWeaponSpec.KnownTools' range, as Paintball's).</summary>
        public const byte GrenadeTool = 240;

        private const string PlayMode = "lasertag";

        private static readonly string[] PistolLoadout = { Pistol };

        public static RrLaserTagRoom Current { get; private set; }

        private readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();
        private readonly List<KeyValuePair<float, string>> _vo = new List<KeyValuePair<float, string>>();
        private RrLaserTagLevel _level;
        private RrLaserTagLevel _building;
        private bool _subscribed;
        private bool _entered;
        private int _pendingMap = -1;
        private float _switchAt;
        private int _spawnedTeam = -2;
        private float _spawnAt = -1f;
        private float _pistolAt = -1f;
        private float _nextVote;
        private int _wantMap = -1;
        private float _nextBoundsCheck;
        private float _voFree;
        private string _envScene;

        // ---- rules ----

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
            get { return 480f; }
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
            get { return RrLaserTagMaps.PlayMusic[MapIndex]; }
        }

        protected override string LobbyMusic
        {
            get { return RrLaserTagMaps.LobbyMusic[MapIndex]; }
        }

        protected override string[] Loadout
        {
            get { return PistolLoadout; }
        }

        protected override int PlayerMaxHealth
        {
            get { return 100; }
        }

        protected override float RespawnSeconds
        {
            get { return 5f; }
        }

        protected override float RespawnInvincibility
        {
            get { return 3f; }
        }

        public int MapIndex
        {
            get { return RrLaserTagMaps.Clamp(State.Map); }
        }

        /// <summary>The chosen map is built (spawns, pickups and the console exist).</summary>
        public bool MapReady
        {
            get { return _level != null && _level.Done && _level.Alive && _level.Map == MapIndex; }
        }

        internal RrLaserTagLevel Level
        {
            get { return _level; }
        }

        /// <summary>Field weapons can be taken while playing and not out.</summary>
        internal bool CanTakeGuns
        {
            get { return Phase == RrPhase.Playing && (RrHealth.Local == null || !RrHealth.Local.IsOut); }
        }

        public static string GunName(string prefab)
        {
            switch (prefab)
            {
                case Pistol: return "Laser Pistol";
                case Rifle: return "Burst Rifle";
                case Shotgun: return "Laser Shotgun";
                case Railgun: return "Railgun";
                case OrbLauncher: return "Laser Orb Launcher";
                case BeamGun: return "Beam Cannon";
                case Grenade: return "Grenade";
                default: return prefab;
            }
        }

        // ------------------------------------------------------------------ build

        /// <summary>The chosen map (its lobby is where everyone arrives), behind the loading card.</summary>
        public override IEnumerator Build()
        {
            Current = this;
            float t0 = Time.realtimeSinceStartup;
            yield return RrPortal.Prepare();
            var level = new RrLaserTagLevel(MapIndex);
            _building = level;
            yield return level.Build(transform, _surfaces);
            _building = null;
            _level = level;
            Setup(level);
            BuildProgress = 1f;
            RrLog.Info("LaserTag", "Laser Tag built in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s on " + level.Name + ": " + level.Pickups.Count + " pickups, " +
                                   level.Boards.Count + " scoreboards, console " + (level.Hologram != null ? "ready" : "MISSING"));
        }

        /// <summary>What the room adds to a built map: the pickups, the console, scoreboards, the Rec Center door.</summary>
        private void Setup(RrLaserTagLevel level)
        {
            if (level == null || !level.Alive)
            {
                return;
            }

            var root = level.Root;
            foreach (var w in level.Weapons)
            {
                AddPickup(level, w.Prefab, w.Pose, PickupSeconds);
            }

            var power = level.Floor(level.Area.L(RrLaserTagMaps.PowerSpots[level.Map]), 1f, 3f) + Vector3.up * 1f;
            AddPickup(level, RrLaserTagMaps.PowerWeapons[level.Map], new Pose(power, Quaternion.identity), PowerSeconds);
            level.Hologram = Console(level);
            LobbyBoard(level);
            BaseBoards(level);
            Portal(level);
            float lowest = transform.TransformPoint(new Vector3(0f, level.Bounds.min.y, 0f)).y - 2f;
            LowestFloor = float.IsNaN(LowestFloor) ? lowest : Mathf.Min(LowestFloor, lowest);
            RrLog.Info("LaserTag", level.Name + " set up: " + level.Pickups.Count + " pickups (power weapon " + GunName(RrLaserTagMaps.PowerWeapons[level.Map]) + " at " +
                                   power.ToString("F1") + ", GUESS), lowest floor y " + LowestFloor.ToString("0.0") + (root != null ? "" : " (no root)"));
        }

        private void AddPickup(RrLaserTagLevel level, string prefab, Pose local, float respawn)
        {
            var p = RrLaserTagPickup.Create(this, level.Root, local, prefab, respawn);
            p.Index = level.Pickups.Count;
            p.MapIndex = level.Map;
            level.Pickups.Add(p);
        }

        /// <summary>Rec Room's game-start hologram at the level's ScoreboardProjector: map choice, the one mode, START.</summary>
        private RrGameConsole Console(RrLaserTagLevel level)
        {
            var maps = new string[RrLaserTagMaps.Count];
            for (int i = 0; i < maps.Length; i++)
            {
                maps[i] = RrLaserTagMaps.Names[i].ToUpperInvariant();
            }

            var spec = new RrGameConsoleSpec
            {
                Title = "LASER TAG",
                MinPlayers = MinPlayers,
                TeamCount = 2,
                Modes = new[] { "TEAM BATTLE" },
                ModeIndex = () => 0,
                OnMode = m => { },
                Maps = maps,
                MapIndex = () => MapIndex,
                OnMap = m => VoteMap(m),
                OnStart = id => PressStart(),
                CanStart = () => Phase == RrPhase.Lobby && MapReady,
                Status =() => Phase == RrPhase.Lobby && !MapReady ? "LOADING " + RrLaserTagMaps.Names[MapIndex].ToUpperInvariant() + "..." : null
            };
            var c = RrGameConsole.ForGame(this, level.Root, level.Console, spec);
            RrLog.Info("LaserTag", level.Name + ": start hologram at the level's ScoreboardProjector " + level.Console.position.ToString("F1") + " yaw " +
                                   level.Console.rotation.eulerAngles.y.ToString("0"));
            return c;
        }

        /// <summary>A scoreboard beside the console, facing the lobby.</summary>
        private void LobbyBoard(RrLaserTagLevel level)
        {
            var rot = level.Console.rotation;
            var pos = level.Console.position + rot * new Vector3(2.9f, 2.1f, 0f);
            level.Boards.Add(RrScoreboard.Create(level.Root, pos, rot, "LASER TAG", 2, 2.6f));
        }

        /// <summary>A scoreboard in each base: on the wall the match-start spots face (within 8 m), else 5 m ahead of them.</summary>
        private void BaseBoards(RrLaserTagLevel level)
        {
            for (int t = 0; t < 2; t++)
            {
                var list = level.MatchStart[t];
                if (list.Count == 0)
                {
                    continue;
                }

                var c = Vector3.zero;
                var f = Vector3.zero;
                foreach (var p in list)
                {
                    c += p.position;
                    f += p.rotation * Vector3.forward;
                }

                c /= list.Count;
                f = f.sqrMagnitude > 1e-4f ? f.normalized : list[0].rotation * Vector3.forward;
                Vector3 from = transform.TransformPoint(c + Vector3.up * 1.6f);
                Vector3 wf = transform.TransformDirection(f);
                RaycastHit hit;
                Vector3 at = Physics.Raycast(from, wf, out hit, 8f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore)
                    ? transform.InverseTransformPoint(hit.point) - f * 0.12f
                    : c + f * 5f;
                at.y = c.y + 2.6f;
                var rot = Quaternion.LookRotation(-f, Vector3.up);
                level.Boards.Add(RrScoreboard.Create(level.Root, at, rot, RrTeams.Name(t).ToUpperInvariant() + " BASE", 2, 2.4f));
            }
        }

        /// <summary>The door back to the Rec Center, on the lobby wall across from the console (or 4 m behind the arrivals).</summary>
        private void Portal(RrLaserTagLevel level)
        {
            if (level.Lobby.Count == 0)
            {
                return;
            }

            var c = Vector3.zero;
            foreach (var p in level.Lobby)
            {
                c += p.position;
            }

            c /= level.Lobby.Count;
            var fwd = level.Console.rotation * Vector3.forward;
            Vector3 from = transform.TransformPoint(c + Vector3.up * 1.5f);
            RaycastHit hit;
            Vector3 at = Physics.Raycast(from, transform.TransformDirection(fwd), out hit, 14f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore)
                ? transform.InverseTransformPoint(hit.point) - fwd * 0.06f
                : c + fwd * 4f;
            at = level.Floor(new Vector3(at.x, c.y + 0.3f, at.z), 1.5f, 3f);
            RrPortal.Create(level.Root, RrActivity.RecCenter, at, level.Console.rotation.eulerAngles.y + 180f, "REC CENTER");
        }

        // ------------------------------------------------------------------ the map level

        /// <summary>Every client: keep the chosen map built (after a short settle while votes change it).</summary>
        private void KeepMapBuilt()
        {
            int want = MapIndex;
            if (_building != null)
            {
                return;
            }

            if (_level != null && _level.Map == want)
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
            _building = new RrLaserTagLevel(want);
            StartCoroutine(SwitchMap(_building));
        }

        /// <summary>Builds the new map in its slot, moves the local player over, frees the old map.</summary>
        private IEnumerator SwitchMap(RrLaserTagLevel level)
        {
            RrLog.Info("LaserTag", "map switch: building " + level.Name + " (" + level.Scene + ")");
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

            if (level.Map != MapIndex)
            {
                // The choice moved on while it was building: the next check builds the right one.
                level.Destroy();
                yield break;
            }

            var old = _level;
            _level = level;
            Setup(level);
            if (_entered)
            {
                if (Phase == RrPhase.Countdown || Phase == RrPhase.Playing)
                {
                    _spawnAt = Time.time;
                }
                else
                {
                    GoToLobby();
                    if (!string.IsNullOrEmpty(LobbyMusic))
                    {
                        RrMusic.Play(LobbyMusic);
                    }
                    else
                    {
                        RrMusic.Stop();
                    }
                }
            }

            if (old != null)
            {
                old.Destroy();
            }

            RrLog.Info("LaserTag", "map switch: now on " + level.Name + (old != null ? ", " + old.Name + " freed" : ""));
        }

        /// <summary>The room's own lighting for the map the player is on (RrEnvironment's table row of the level).</summary>
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

        /// <summary>Arrival: the lobby of the built map, in front of its console.</summary>
        public override Pose Spawn(int index)
        {
            var level = _level;
            if (level != null && level.Lobby.Count > 0)
            {
                var p = level.Lobby[((index % level.Lobby.Count) + level.Lobby.Count) % level.Lobby.Count];
                return new Pose(p.position + Vector3.up * 0.05f, p.rotation);
            }

            return new Pose(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
        }

        public override void OnLocalEnter()
        {
            Current = this;
            base.OnLocalEnter();
            if (!_subscribed)
            {
                _subscribed = true;
                RrNet.On(EvPickup, OnPickupEvent);
                RrNet.On(EvBlast, OnBlastEvent);
                RrCombat.OnDeath += OnDeath;
                RrHealth.Ensure().OnRespawned += OnRespawned;
                RrHealth.Local.OnOut += OnLocalOut;
            }

            RrCombat.FriendlyFire = false;
            _entered = true;
            _spawnedTeam = -2;
            _envScene = null;
            ShowLighting();
            RrLog.Info("LaserTag", "entered: " + RrLaserTagMaps.Names[MapIndex] + ", phase " + Phase + ", authority " + IsAuthority + ", members " + Members().Count);
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            _entered = false;
            Unsubscribe();
            RrPlayer.RequestPlayMode(PlayMode, false);
            if (RrHealth.Local != null)
            {
                RrHealth.Local.RespawnPoint = null;
                RrHealth.Local.Accepts = null;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_envScene != null && RrEnvironment.Activity == RrActivity.LaserTag)
            {
                RrEnvironment.SetArea(null);
            }

            _envScene = null;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            RrNet.Off(EvPickup, OnPickupEvent);
            RrNet.Off(EvBlast, OnBlastEvent);
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

        // ------------------------------------------------------------------ authority

        protected override void AuthorityMatchSetup()
        {
            RrLog.Info("LaserTag", "match starting: team battle on " + RrLaserTagMaps.Names[MapIndex] + ", " + Members().Count + " player(s), first to " + ScoreToWin + " or " +
                                   RoundSeconds.ToString("0") + " s");
        }

        protected override void AuthorityRoundOver(int winner)
        {
            RrLog.Info("LaserTag", "match over: " + (winner >= 0 ? RrTeams.Name(winner) + " wins" : "tie") + " (" + State.TeamScore(0) + " - " + State.TeamScore(1) + ")");
            base.AuthorityRoundOver(winner);
        }

        protected override void AuthorityRequest(int playerId, byte request, byte arg)
        {
            if (Phase == RrPhase.Lobby && request == ReqMap && arg < RrLaserTagMaps.Count)
            {
                State.Map = arg;
            }
        }

        /// <summary>
        /// Every client sees every out (Death events); the authority scores them: +10 to the tagger's team for an
        /// opponent, -10 to the victim's team for a self-destruct (own grenade, falling out), -10 for a teammate tag
        /// (unreachable while friendly fire is off; ArenaManager's playerFriendlyFireKillPoints).
        /// </summary>
        private void OnDeath(RrCombat.DeathInfo d)
        {
            if (!IsAuthority || Phase != RrPhase.Playing)
            {
                return;
            }

            int victimTeam = RrCombat.TeamOf(d.Victim);
            if (d.Killer == d.Victim || d.Killer < 0)
            {
                if (victimTeam >= 0)
                {
                    AddPoints(victimTeam, SelfDestructPoints);
                    RrLog.Info("LaserTag", "self-destruct by player " + d.Victim + ": " + RrTeams.Name(victimTeam) + " " + SelfDestructPoints);
                }

                return;
            }

            int killerTeam = RrCombat.TeamOf(d.Killer);
            if (killerTeam < 0)
            {
                return;
            }

            if (killerTeam == victimTeam)
            {
                AddPoints(killerTeam, -TagPoints);
                return;
            }

            AddPoints(killerTeam, TagPoints);
            if (State.TeamScore(killerTeam) >= ScoreToWin)
            {
                EndRound(killerTeam);
            }
        }

        /// <summary>Authority: team points, never below 0 (GUESS: Rec Room's board showed no negative scores).</summary>
        private void AddPoints(int team, int points)
        {
            if (IsAuthority && team >= 0 && team < 2)
            {
                State.SetTeamScore(team, Mathf.Max(0, State.TeamScore(team) + points));
            }
        }

        // ------------------------------------------------------------------ every client

        /// <summary>
        /// The victim's rule on a hit (RrHealth.Accepts): an opponent's hit counts, a teammate's never (no friendly fire in
        /// Laser Tag), your own only from your grenade (2019-01: grenades can hit yourself).
        /// </summary>
        public static bool AcceptHit(int damage, int attacker, byte tool)
        {
            if (attacker == RrNet.LocalId)
            {
                return tool == GrenadeTool;
            }

            if (attacker < 0)
            {
                return true;
            }

            int a = RrCombat.TeamOf(attacker), me = RrTeams.LocalTeam;
            return a < 0 || me < 0 || a != me;
        }

        protected override void OnPhase(RrPhase old, RrPhase now)
        {
            switch (now)
            {
                case RrPhase.Lobby:
                    if (old == RrPhase.MatchOver || old == RrPhase.RoundOver || old == RrPhase.Playing || old == RrPhase.Countdown)
                    {
                        GoToLobby();
                    }

                    Restock();
                    RrPlayer.RequestPlayMode(PlayMode, false);
                    _spawnedTeam = -2;
                    _spawnAt = -1f;
                    if (string.IsNullOrEmpty(LobbyMusic))
                    {
                        RrMusic.Stop();
                    }

                    break;
                case RrPhase.Countdown:
                    RrLoadout.Clear();
                    Restock();
                    SayAt(RrAnnouncer.TeamBattle, Time.time + 1.2f);
                    _spawnAt = Time.time + 0.6f;
                    _spawnedTeam = -2;
                    RrPlayer.RequestPlayMode(PlayMode, true);
                    break;
                case RrPhase.Playing:
                    if (_spawnedTeam < 0)
                    {
                        // Joined in progress (or the countdown was missed): the smaller team, then a respawn point.
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

                    RrPlayer.RequestPlayMode(PlayMode, true);
                    break;
                case RrPhase.RoundOver:
                    RrPlayer.RequestPlayMode(PlayMode, false);
                    break;
                case RrPhase.MatchOver:
                    SayAt(RrAnnouncer.MatchOver, Time.time + 2f);
                    break;
            }
        }

        protected override void OnTeamsAssigned()
        {
            // A late team assignment moves the player to the right base.
            if ((Phase == RrPhase.Countdown || Phase == RrPhase.Playing) && _spawnedTeam >= 0 && _spawnedTeam != RrTeams.LocalTeam)
            {
                _spawnAt = Time.time;
            }
        }

        protected override void OnTick(RrInstanceState s)
        {
            KeepMapBuilt();
            if (!_entered)
            {
                return;
            }

            ShowLighting();
            if (_spawnAt > 0f && Time.time >= _spawnAt)
            {
                if (s.Phase != RrPhase.Countdown && s.Phase != RrPhase.Playing)
                {
                    _spawnAt = -1f;
                }
                else if (MapReady)
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
                if (s.Phase == RrPhase.Playing && !RrLoadout.Busy && RrLoadout.Given.Count == 0 && (RrHealth.Local == null || !RrHealth.Local.IsOut))
                {
                    RrLoadout.GiveAll(PistolLoadout);
                }
            }

            if (s.Phase == RrPhase.Lobby)
            {
                RetryVotes(s);
            }

            if (s.Phase == RrPhase.Playing && _spawnedTeam >= 0 && Time.time >= _nextBoundsCheck)
            {
                _nextBoundsCheck = Time.time + 0.25f;
                FellOff();
            }

            PlayVo();
        }

        /// <summary>Below the level (or far outside it) is out: a self-destruct.</summary>
        private void FellOff()
        {
            var h = RrHealth.Local;
            if (h == null || h.IsOut || !MapReady)
            {
                return;
            }

            var b = _level.Bounds;
            Vector3 feet = transform.InverseTransformPoint(RrPlayer.FeetPosition);
            bool below = feet.y < b.min.y - 1f;
            var wide = b;
            wide.Expand(new Vector3(40f, 60f, 40f));
            if (below || !wide.Contains(feet))
            {
                RrLog.Info("LaserTag", "fell out of " + _level.Name + " (feet " + feet.ToString("F1") + "): out");
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
            var pose = Phase == RrPhase.Countdown ? MatchStartPose(team) : RespawnPoseFor(team);
            RrPlayer.Teleport(pose.position, pose.rotation.eulerAngles.y);
            SayAt(team == 0 ? RrAnnouncer.BlueTeam : RrAnnouncer.RedTeam, Time.time + 3.2f);
            RrLog.Info("LaserTag", "spawned in the " + RrTeams.Name(team) + " base on " + _level.Name + " at " + transform.InverseTransformPoint(pose.position).ToString("F1") + " (room-local)");
        }

        private void GoToLobby()
        {
            var pose = WorldSpawn(Mathf.Max(0, Members().IndexOf(RrNet.LocalId)));
            RrPlayer.Teleport(pose.position, pose.rotation.eulerAngles.y);
        }

        /// <summary>The team's four match-start spots, one per teammate (by PlayerId order).</summary>
        private Pose MatchStartPose(int team)
        {
            var list = _level.MatchStart[Mathf.Clamp(team, 0, 1)];
            if (list.Count == 0)
            {
                return WorldSpawn(0);
            }

            int rank = 0;
            foreach (int id in Members())
            {
                if (id != RrNet.LocalId && id < RrNet.LocalId && RrCombat.TeamOf(id) == team)
                {
                    rank++;
                }
            }

            var p = list[rank % list.Count];
            return new Pose(transform.TransformPoint(p.position + Vector3.up * 0.05f), transform.rotation * p.rotation);
        }

        private Pose LocalRespawnPose()
        {
            return RespawnPoseFor(RrTeams.LocalTeam);
        }

        /// <summary>Rec Room's rule (as in Paintball): respawn at the team's spawn point farthest from the nearest opponent.</summary>
        public Pose RespawnPoseFor(int team)
        {
            team = Mathf.Clamp(team, 0, 1);
            if (!MapReady || _level.Respawns[team].Count == 0)
            {
                return WorldSpawn(0);
            }

            var list = _level.Respawns[team];
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
            return MapReady && _spawnedTeam >= 0 && (Phase == RrPhase.Playing || Phase == RrPhase.Countdown) ? RespawnPoseFor(RrTeams.LocalTeam) : WorldSpawn(index);
        }

        private int SmallerTeam()
        {
            int blue = 0, red = 0;
            foreach (int id in Members())
            {
                if (id == RrNet.LocalId)
                {
                    continue;
                }

                int t = RrCombat.TeamOf(id);
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
                RrSound.PlayUi("Arena_PlayerOut01", 0.7f);
            }
        }

        /// <summary>After an out the player comes back with the laser pistol (field weapons are lost).</summary>
        private void OnRespawned()
        {
            if (Phase != RrPhase.Playing)
            {
                return;
            }

            bool pistol = false;
            foreach (var go in RrLoadout.Given)
            {
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

        /// <summary>A grenade left the hand: the pistol comes back shortly after.</summary>
        internal void GivePistolSoon()
        {
            _pistolAt = Time.time + 0.6f;
        }

        private void Restock()
        {
            if (_level == null)
            {
                return;
            }

            foreach (var p in _level.Pickups)
            {
                if (p != null)
                {
                    p.Restock();
                }
            }
        }

        /// <summary>Another member took a pickup: body = map, spot.</summary>
        private void OnPickupEvent(int sender, byte[] body)
        {
            if (sender == RrNet.LocalId || body == null || body.Length < 2 || _level == null || body[0] != _level.Map)
            {
                return;
            }

            int i = body[1];
            if (i < _level.Pickups.Count && _level.Pickups[i] != null)
            {
                _level.Pickups[i].Hide();
            }
        }

        private void OnBlastEvent(int sender, byte[] body)
        {
            if (sender != RrNet.LocalId)
            {
                RrLaserTagGrenade.Receive(this, body);
            }
        }

        // ---- lobby console ----

        public void VoteMap(int map)
        {
            if (Phase != RrPhase.Lobby || map < 0 || map >= RrLaserTagMaps.Count)
            {
                return;
            }

            _wantMap = map;
            _nextVote = 0f;
            RrLog.Info("LaserTag", "map vote: " + RrLaserTagMaps.Names[map]);
        }

        /// <summary>The console's START: starts the countdown once the chosen map is built here.</summary>
        public void PressStart()
        {
            if (Phase != RrPhase.Lobby)
            {
                return;
            }

            if (!MapReady)
            {
                RrLog.Info("LaserTag", "START pressed while " + RrLaserTagMaps.Names[MapIndex] + " is still building: ignored");
                return;
            }

            RrLog.Info("LaserTag", "START pressed (" + Members().Count + " in the lobby, " + RrLaserTagMaps.Names[MapIndex] + ")");
            RequestStart();
        }

        /// <summary>The vote goes to the authority and is repeated every 0.4 s until the int shows it.</summary>
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

            RrNet.Send(RrGameEvents.GameRequest, new[] { ReqMap, (byte)_wantMap }, true);
        }

        // ---- announcer queue (clips must not talk over each other) ----

        private void SayAt(string clip, float time)
        {
            if (!string.IsNullOrEmpty(clip))
            {
                _vo.Add(new KeyValuePair<float, string>(time, clip));
            }
        }

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
            _voFree = Time.time + 1.4f;
        }

        // ---- harness hooks ("call" steps) ----

        /// <summary>Harness: press START on the console as the local player would.</summary>
        public static void TestConsoleStart()
        {
            var room = Current;
            if (room == null || room._level == null || room._level.Hologram == null)
            {
                RrLog.Warn("LaserTag", "LaserTagTest: no room or console");
                return;
            }

            bool ok = room._level.Hologram.Press("test");
            RrLog.Info("LaserTag", "LaserTagTest: console START pressed (" + ok + "), phase " + room.Phase + ", map " + RrLaserTagMaps.Names[room.MapIndex]);
        }

        /// <summary>Harness: choose Cyber Junk City / Hangar (the authority writes the map; every client rebuilds).</summary>
        public static void TestCyberJunk()
        {
            TestMap(RrLaserTagMaps.CyberJunk);
        }

        public static void TestHangar()
        {
            TestMap(RrLaserTagMaps.Hangar);
        }

        private static void TestMap(int map)
        {
            var room = Current;
            if (room != null && room.IsAuthority && room.Phase == RrPhase.Lobby)
            {
                room.State.Map = map;
                RrLog.Info("LaserTag", "LaserTagTest: map set to " + RrLaserTagMaps.Names[map]);
            }
        }

        /// <summary>Harness: the round timer to 5 s (ends the match by time).</summary>
        public static void TestEndSoon()
        {
            var room = Current;
            if (room != null && room.IsAuthority && room.Phase == RrPhase.Playing)
            {
                room.State.SetTimer(5f);
            }
        }

        /// <summary>Harness: take the nearest pickup, then burst a grenade 3 m ahead (self damage check).</summary>
        public static void TestPickupAndBlast()
        {
            var room = Current;
            if (room == null || room._level == null)
            {
                return;
            }

            RrLaserTagPickup nearest = null;
            float best = float.MaxValue;
            foreach (var p in room._level.Pickups)
            {
                float d = p != null ? Vector3.Distance(p.transform.position, RrPlayer.FeetPosition) : float.MaxValue;
                if (d < best)
                {
                    best = d;
                    nearest = p;
                }
            }

            bool took = nearest != null && nearest.Take("test", RrHandSide.Right);
            var cam = RrPlayer.Camera;
            var ahead = RrPlayer.FeetPosition + (cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward) * 3f + Vector3.up;
            int hits = RrLaserTagGrenade.Explode(room, ahead);
            RrLog.Info("LaserTag", "LaserTagTest: took " + (nearest != null ? GunName(nearest.Prefab) : "nothing") + " (" + took + ", " + best.ToString("0.0") + " m), blast hit " + hits +
                                   ", health " + (RrHealth.Local != null ? RrHealth.Local.Health.ToString() : "-") + ", scores " + room.State.TeamScore(0) + " - " + room.State.TeamScore(1));
        }
    }
}
