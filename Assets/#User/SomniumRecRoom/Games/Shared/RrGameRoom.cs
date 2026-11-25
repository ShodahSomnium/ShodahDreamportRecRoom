using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// A room that runs matches the way Rec Room's games do: a lobby until enough players are in, a countdown,
    /// the match (timed or until a score), round and match results, and back to the lobby.
    ///
    /// The phase and its timer live in the instance state (<see cref="RrNet.Instance"/>), written only by the
    /// instance's authority - the "fake master", the longest-present member, re-elected when it leaves - so a
    /// match survives its host leaving. Every client reacts to phase changes the same way (announcer, music,
    /// resetting its own player), and subclasses fill in the game with the Authority* hooks (rules, scoring,
    /// spawning) and the On* hooks (what every client shows).
    ///
    /// Teams: balanced by the authority as the countdown starts (<see cref="RrTeams.Balance"/>), sent to all
    /// with <see cref="RrGameEvents.TeamAssign"/>; each client writes its own team into its presence slot.
    /// </summary>
    public abstract class RrGameRoom : RrRoom
    {
        // ---- tuning (override per game) ----

        /// <summary>0 for free-for-all.</summary>
        protected virtual int TeamCount
        {
            get { return 2; }
        }

        protected virtual int MinPlayersToStart
        {
            get { return Info != null ? Mathf.Max(1, Info.MinPlayers) : 1; }
        }

        /// <summary>Seconds the lobby waits once enough players are in (a player can start it sooner with a button).</summary>
        protected virtual float LobbySeconds
        {
            get { return 15f; }
        }

        protected virtual float CountdownSeconds
        {
            get { return 5f; }
        }

        /// <summary>0: untimed (the game ends a round itself with EndRound).</summary>
        protected virtual float RoundSeconds
        {
            get { return 300f; }
        }

        protected virtual int Rounds
        {
            get { return 1; }
        }

        protected virtual float RoundOverSeconds
        {
            get { return 5f; }
        }

        protected virtual float MatchOverSeconds
        {
            get { return 10f; }
        }

        /// <summary>Start automatically when enough players are in the lobby.</summary>
        protected virtual bool AutoStart
        {
            get { return true; }
        }

        /// <summary>Music while playing (Rec Room clip name), and in the lobby.</summary>
        protected virtual string PlayMusic
        {
            get { return null; }
        }

        protected virtual string LobbyMusic
        {
            get { return Info != null ? Info.Music : null; }
        }

        /// <summary>Tools handed to the local player when play starts (Rec Room prefab names; first right hand, second left in VR).</summary>
        protected virtual string[] Loadout
        {
            get { return null; }
        }

        /// <summary>Local hit points while playing: 0 = no health (non-combat games), 1 = one hit is out (Paintball).</summary>
        protected virtual int PlayerMaxHealth
        {
            get { return 0; }
        }

        /// <summary>Seconds from out to respawn, and the invincibility after it (Paintball: 5 and 5).</summary>
        protected virtual float RespawnSeconds
        {
            get { return 5f; }
        }

        protected virtual float RespawnInvincibility
        {
            get { return 5f; }
        }

        /// <summary>Health and loadout follow the phase (Playing: full health + tools; Lobby / MatchOver: taken back).</summary>
        private void CombatPhase(RrPhase now)
        {
            if (now == RrPhase.Playing && PlayerMaxHealth > 0)
            {
                RrHealth.Ensure().Begin(PlayerMaxHealth, RespawnSeconds, RespawnInvincibility);
            }
            else if ((now == RrPhase.Lobby || now == RrPhase.MatchOver) && RrHealth.Local != null)
            {
                RrHealth.Local.End();
            }

            RrLoadout.OnPhase(now, Loadout);
        }

        // ---- state ----

        protected RrInstanceState State
        {
            get { return RrNet.Instance; }
        }

        public RrPhase Phase
        {
            get { return State.Phase; }
        }

        private RrPhase _seenPhase = (RrPhase)255;
        private float _lobbyReadyAt = -1f;
        private int _lastWholeSecond = -1;
        private bool _saidMinute;
        private bool _saidTen;

        protected virtual void OnEnable()
        {
            RrNet.On(RrGameEvents.TeamAssign, OnTeamAssign);
            RrNet.On(RrGameEvents.GameRequest, OnGameRequest);
            RrNet.On(RrGameEvents.Announce, OnAnnounce);
        }

        protected virtual void OnDisable()
        {
            RrNet.Off(RrGameEvents.TeamAssign, OnTeamAssign);
            RrNet.Off(RrGameEvents.GameRequest, OnGameRequest);
            RrNet.Off(RrGameEvents.Announce, OnAnnounce);
        }

        public override void OnLocalEnter()
        {
            base.OnLocalEnter();
            _seenPhase = (RrPhase)255;
            if (!string.IsNullOrEmpty(LobbyMusic))
            {
                RrMusic.Play(LobbyMusic);
            }
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            RrTeams.Reset();
            RrMusic.Stop();
            RrLoadout.Clear();
            RrProjectile.ClearAll();
            if (RrHealth.Local != null)
            {
                RrHealth.Local.End();
            }
        }

        protected virtual void Update()
        {
            if (!Built)
            {
                return;
            }

            if (IsAuthority)
            {
                AuthorityStep();
            }

            if (State.Phase != _seenPhase)
            {
                var old = _seenPhase;
                _seenPhase = State.Phase;
                PhaseChanged(old, _seenPhase);
            }

            ClientStep();
        }

        // ---- authority ----

        private void AuthorityStep()
        {
            var s = State;
            switch (s.Phase)
            {
                case RrPhase.Lobby:
                    if (AutoStart && Members().Count >= MinPlayersToStart)
                    {
                        if (_lobbyReadyAt < 0f)
                        {
                            _lobbyReadyAt = Time.unscaledTime + LobbySeconds;
                        }

                        if (Time.unscaledTime >= _lobbyReadyAt)
                        {
                            StartCountdown();
                        }
                    }
                    else
                    {
                        _lobbyReadyAt = -1f;
                    }

                    break;
                case RrPhase.Countdown:
                    if (s.TimeRemaining <= 0f)
                    {
                        s.SetPhase(RrPhase.Playing, RoundSeconds);
                        AuthorityRoundStart(s.Round);
                    }

                    break;
                case RrPhase.Playing:
                    if (RoundSeconds > 0f && s.TimeRemaining <= 0f)
                    {
                        EndRound(AuthorityRoundWinner());
                    }
                    else
                    {
                        AuthorityPlaying();
                    }

                    break;
                case RrPhase.RoundOver:
                    if (s.TimeRemaining <= 0f)
                    {
                        if (s.Round + 1 < Rounds && !AuthorityMatchDecided())
                        {
                            s.Round++;
                            s.SetPhase(RrPhase.Countdown, CountdownSeconds);
                        }
                        else
                        {
                            s.SetPhase(RrPhase.MatchOver, MatchOverSeconds);
                            AuthorityMatchOver();
                        }
                    }

                    break;
                case RrPhase.MatchOver:
                    if (s.TimeRemaining <= 0f)
                    {
                        s.SetPhase(RrPhase.Lobby, 0f);
                        _lobbyReadyAt = -1f;
                        AuthorityBackToLobby();
                    }

                    break;
            }
        }

        /// <summary>Authority: start now (the lobby's Start button, or AutoStart).</summary>
        protected void StartCountdown()
        {
            if (!IsAuthority || State.Phase != RrPhase.Lobby)
            {
                return;
            }

            var s = State;
            s.Round = 0;
            for (int t = 0; t < 4; t++)
            {
                s.SetTeamScore(t, 0);
            }

            if (TeamCount > 0)
            {
                var assignment = RrTeams.Balance(Members(), TeamCount);
                RrNet.Send(RrGameEvents.TeamAssign, RrTeams.Encode(assignment));
                int me = RrNet.LocalId;
                int mine;
                if (assignment.TryGetValue(me, out mine))
                {
                    RrTeams.SetLocalTeam(mine);
                }
            }

            AuthorityMatchSetup();
            s.SetPhase(RrPhase.Countdown, CountdownSeconds);
        }

        /// <summary>Authority: ends the current round with a winner (team, or player id for FFA; -1 draw).</summary>
        protected void EndRound(int winner)
        {
            if (!IsAuthority || State.Phase != RrPhase.Playing)
            {
                return;
            }

            AuthorityRoundOver(winner);
            State.SetPhase(RrPhase.RoundOver, RoundOverSeconds);
        }

        /// <summary>Authority: adds to a team's score.</summary>
        protected void AddTeamScore(int team, int points)
        {
            if (IsAuthority)
            {
                State.SetTeamScore(team, State.TeamScore(team) + points);
            }
        }

        /// <summary>The members of this instance (PlayerIds; just the local player offline).</summary>
        protected List<int> Members()
        {
            return RrNet.MembersOfMyInstance();
        }

        /// <summary>Authority: plays an announcer line for everyone.</summary>
        protected void AnnounceAll(string clip)
        {
            if (string.IsNullOrEmpty(clip))
            {
                return;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(clip);
            RrNet.Send(RrGameEvents.Announce, bytes);
        }

        private void OnAnnounce(int sender, byte[] body)
        {
            RrAnnouncer.Say(System.Text.Encoding.UTF8.GetString(body));
        }

        private void OnTeamAssign(int sender, byte[] body)
        {
            RrTeams.Apply(body);
            OnTeamsAssigned();
        }

        private void OnGameRequest(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 1)
            {
                return;
            }

            byte request = body[0];
            byte arg = body.Length > 1 ? body[1] : (byte)0;
            if (request == RrGameEvents.Requests.Start)
            {
                StartCountdown();
            }
            else
            {
                AuthorityRequest(sender, request, arg);
            }
        }

        /// <summary>Any client: ask the authority to start the match now (lobby button).</summary>
        public void RequestStart()
        {
            if (IsAuthority)
            {
                StartCountdown();
            }
            else
            {
                RrNet.Send(RrGameEvents.GameRequest, new[] { RrGameEvents.Requests.Start, (byte)0 }, true);
            }
        }

        // ---- every client ----

        private void PhaseChanged(RrPhase old, RrPhase now)
        {
            _lastWholeSecond = -1;
            _saidMinute = false;
            _saidTen = false;
            switch (now)
            {
                case RrPhase.Lobby:
                    if (!string.IsNullOrEmpty(LobbyMusic))
                    {
                        RrMusic.Play(LobbyMusic);
                    }

                    break;
                case RrPhase.Countdown:
                    RrAnnouncer.Say(PhaseAnnouncement(now));
                    break;
                case RrPhase.Playing:
                    RrAnnouncer.Say(PhaseAnnouncement(now));
                    if (!string.IsNullOrEmpty(PlayMusic))
                    {
                        RrMusic.Play(PlayMusic);
                    }

                    break;
                case RrPhase.MatchOver:
                    RrAnnouncer.Say(PhaseAnnouncement(now));
                    break;
            }

            CombatPhase(now);
            OnPhase(old, now);
        }

        private void ClientStep()
        {
            var s = State;
            float left = s.TimeRemaining;
            int whole = Mathf.CeilToInt(left);
            if (s.Phase == RrPhase.Countdown && whole != _lastWholeSecond && whole > 0 && whole <= 5)
            {
                RrSound.PlayUi(RrAnnouncer.CountdownTick, 0.7f);
            }

            if (s.Phase == RrPhase.Playing && RoundSeconds > 0f)
            {
                if (!_saidMinute && left <= 60f && left > 58f && RoundSeconds > 90f)
                {
                    _saidMinute = true;
                    RrAnnouncer.Say(RrAnnouncer.OneMinute);
                }

                if (!_saidTen && left <= 10f && left > 8f)
                {
                    _saidTen = true;
                    RrAnnouncer.Say(RrAnnouncer.TenSeconds);
                }
            }

            _lastWholeSecond = whole;
            OnTick(s);
        }

        // ---- hooks ----

        /// <summary>Authority, as the countdown starts: reset the arena, balls, scores of the match.</summary>
        protected virtual void AuthorityMatchSetup()
        {
        }

        /// <summary>Authority, as a round starts playing.</summary>
        protected virtual void AuthorityRoundStart(int round)
        {
        }

        /// <summary>Authority, every frame while playing (rules, win checks: call EndRound).</summary>
        protected virtual void AuthorityPlaying()
        {
        }

        /// <summary>Authority: who won when the round timer ran out (team or player id; -1 draw). Default: best team score.</summary>
        protected virtual int AuthorityRoundWinner()
        {
            if (TeamCount <= 0)
            {
                return -1;
            }

            int best = -1;
            int bestScore = int.MinValue;
            bool tie = false;
            for (int t = 0; t < TeamCount; t++)
            {
                int sc = State.TeamScore(t);
                if (sc > bestScore)
                {
                    bestScore = sc;
                    best = t;
                    tie = false;
                }
                else if (sc == bestScore)
                {
                    tie = true;
                }
            }

            return tie ? -1 : best;
        }

        protected virtual void AuthorityRoundOver(int winner)
        {
            if (TeamCount > 0)
            {
                AnnounceAll(RrAnnouncer.TeamWins(winner));
            }
        }

        /// <summary>Authority: true when the match is decided before all rounds are played.</summary>
        protected virtual bool AuthorityMatchDecided()
        {
            return false;
        }

        protected virtual void AuthorityMatchOver()
        {
        }

        protected virtual void AuthorityBackToLobby()
        {
        }

        /// <summary>Authority: a request from a member (switch team, vote...).</summary>
        protected virtual void AuthorityRequest(int playerId, byte request, byte arg)
        {
        }

        /// <summary>
        /// Every client: the announcer line as a phase starts ("Ready" for the countdown, "Gameon" for play,
        /// "Gameover" for the results); null keeps it silent. The quests override it: their only voice line is
        /// Coach's "Game On!", which they say themselves, and their end is the win / lose stinger.
        /// </summary>
        protected virtual string PhaseAnnouncement(RrPhase phase)
        {
            switch (phase)
            {
                case RrPhase.Countdown:
                    return RrAnnouncer.Ready;
                case RrPhase.Playing:
                    return RrAnnouncer.GameOn;
                case RrPhase.MatchOver:
                    return RrAnnouncer.GameOver;
            }

            return null;
        }

        /// <summary>Every client: the phase changed (reset the local player, show results...).</summary>
        protected virtual void OnPhase(RrPhase old, RrPhase now)
        {
        }

        /// <summary>Every client, every frame after the phase logic.</summary>
        protected virtual void OnTick(RrInstanceState state)
        {
        }

        /// <summary>Every client: teams were (re)assigned; RrTeams.LocalTeam is current.</summary>
        protected virtual void OnTeamsAssigned()
        {
        }
    }
}
