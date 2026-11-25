using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// A Rec Room quest (rr_quests.md 3): a lobby with the weapon racks, then acts that open one after another,
    /// each with waves of enemies, breakable pots, and an exit door that opens when the act is clear; the last act
    /// ends in victory (win stinger, rating, chests) and the whole team being down ends it in failure (lose stinger,
    /// back to the lobby: Golden Trophy, Crimson Cauldron and Jumbotron have no checkpoints). A quest with checkpoints
    /// opts in: <see cref="RestartActOnWipe"/> (the Isle: the current act starts again) or its own (Crescendo's Grand Hall).
    ///
    /// On top of <see cref="RrGameRoom"/>: the lobby is <see cref="RrPhase.Lobby"/> (started with the start button,
    /// never automatically), the whole run is one <see cref="RrPhase.Playing"/> round, and the end screen is
    /// <see cref="RrPhase.MatchOver"/>. Inside the round the instance authority runs the act machine in the custom
    /// ints (<see cref="RrQuestInts"/>: act, stage Intro/Combat/Clear, wave, team gold, rating base, boss health...),
    /// the wave director (<see cref="RrQuestDirector"/>) and every enemy (<see cref="RrQuestEnemies"/>, actor
    /// blocks 0-1); pots are in actor block 2 (<see cref="RrQuestBreakables"/>). Every client reacts to what the ints
    /// say: it moves its own player into the new act, opens doors, plays Rec Room's music and stingers, and judges
    /// hits on its own player (victim decides). Members' requests (advance through the open door, a broken pot, a
    /// parry stun, a hazard) go to the authority as <see cref="RrQuestEvents.Request"/> and are repeated until the ints
    /// show the effect.
    ///
    /// A quest subclass fills <see cref="CreateDef"/> (currency, music, enemy roster, weapons, acts with waves and
    /// spawn groups) and builds its lobby and act rooms in <see cref="BuildLevel"/> (each act's
    /// <see cref="RrQuestAct.Root"/>, exit <see cref="RrQuestDoor"/>, racks, pots via <see cref="Pots"/>). Hooks let it
    /// add set pieces: <see cref="AuthorityActStep"/> / <see cref="AuthorityActDone"/> (bosses, puzzles),
    /// <see cref="OnActStage"/> and <see cref="QuestStep"/> (what every client shows), <see cref="OnEnemyReady"/>
    /// (boss bodies), <see cref="AuthorityHazard"/> (a member pulled a lever), <see cref="OnRunReset"/>.
    ///
    /// Rec Room's FINAL quest rules (rr_latest_rgames_story.md 5.3, 12.1) are applied here for every quest: the
    /// definition's unset fields come from <see cref="RrQuestFinalRules"/> for the room's activity; one hit downs a
    /// player (an ally's melee weapon never does); kills pay the fixed gold per enemy type; the rating uses fixed gold
    /// totals; Coach says "Game On!" as the run starts and no other announcer line plays; the boss's death kills every
    /// other enemy (<see cref="AuthorityBossDefeated"/>); the party is capped at the quest's MaxPlayers. The RRO mods
    /// (<see cref="RrQuestMods"/>) are picked on a lobby board (<see cref="ModBoard"/>).
    /// </summary>
    public abstract class RrQuestRoom : RrGameRoom
    {
        public static RrQuestRoom Current { get; private set; }

        /// <summary>
        /// Harness only (set by <see cref="RrQuestTest.Start"/>): hits on the local player are logged, not counted, so a
        /// scripted run survives the one-hit rule. <see cref="RrQuestTest.Down"/> still downs the player.
        /// </summary>
        public static bool TestGuard;

        /// <summary>Tool ids from 200 are the ones a game registers at run time; only RrMelee registers (swords, shields).</summary>
        private const int FirstRegisteredTool = 200;

        public RrQuestDef Def { get; private set; }
        /// <summary>The final rules applied to <see cref="Def"/> (null: not one of Rec Room's five quests).</summary>
        public RrQuestFinalRules.Rules FinalRules { get; private set; }
        public RrQuestEnemies Enemies { get; private set; }
        public RrQuestDirector Director { get; private set; }
        public RrQuestBreakables Pots { get; private set; }
        public RrQuestHud Hud { get; private set; }
        public RrQuestRevive Revive { get; private set; }
        public RrQuestGear Gear { get; private set; }
        public RrQuestMods Mods { get; private set; }

        protected readonly List<RrQuestRack> Racks = new List<RrQuestRack>();

        private int _seenAct = -2;
        private int _seenStage = -1;
        private int _seenOutcome;
        private float _nextAsk;
        private float _lavaGuard;
        private float _exploreAt = -1f;
        private float _allDownSince = -1f;
        private float _restartGrace = -1f;
        private int _actStartAct = -1;
        private int _actStartScore;
        private int _actStartMax;
        private readonly Dictionary<int, bool> _wasDown = new Dictionary<int, bool>();
        private readonly List<int> _members = new List<int>();
        private readonly List<Vector3> _feet = new List<Vector3>();
        private bool _loggedOneHit, _loggedAllyMelee, _loggedGuard, _loggedBossKill;
        private static bool _loggedStartLine;

        // ------------------------------------------------------------------ subclass contract

        /// <summary>The quest's definition (called once at the start of Build).</summary>
        protected abstract RrQuestDef CreateDef();

        /// <summary>Builds the lobby and every act (set each act's Root and ExitDoor). Spread over frames; set BuildProgress 0.1..0.9.</summary>
        protected abstract IEnumerator BuildLevel();

        /// <summary>Lobby spawn point (local), per arrival index.</summary>
        protected virtual Pose LobbySpawn(int index)
        {
            return new Pose(new Vector3((index % 4 - 1.5f) * 1.2f, 0.05f, -4f), Quaternion.identity);
        }

        /// <summary>Every client: the current act's stage changed (fresh: the act was just entered).</summary>
        protected virtual void OnActStage(int act, RrQuestStage stage)
        {
        }

        /// <summary>Authority, every frame of an act's Combat stage (after the director).</summary>
        protected virtual void AuthorityActStep(int act)
        {
        }

        /// <summary>Authority: an extra condition for an act to be clear (default: nothing beyond the waves).</summary>
        protected virtual bool AuthorityActDone(int act)
        {
            return true;
        }

        /// <summary>Every client: an enemy's body is ready (per-type setup, bosses).</summary>
        protected virtual void OnEnemyReady(RrEnemy e, int type)
        {
        }

        /// <summary>
        /// Gold for killing an enemy: the boss's <see cref="RrQuestDef.BossKillPoints"/>, else the roster type's
        /// <see cref="RrQuestDef.KillPoints"/> (Rec Room's fixed gold per enemy kind; Golden Trophy's come from
        /// <see cref="RrQuestFinalRules"/>), else its max health (the first build's GUESS, for quests with no
        /// published values).
        /// </summary>
        protected virtual int PointsFor(RrEnemy e, int type)
        {
            if (Def != null)
            {
                if (Def.BossKillPoints > 0 && IsBoss(e, type))
                {
                    return Def.BossKillPoints;
                }

                int p = Def.KillPointsOf(type);
                if (p > 0)
                {
                    return p;
                }
            }

            return e != null ? Mathf.Max(0, e.MaxHealth) : 0;
        }

        /// <summary>
        /// Whether an enemy is the quest's boss: <see cref="RrQuestDef.BossType"/> when set, else any enemy the room
        /// shows with the boss coin burst (<see cref="PointsKind"/> = PointsBoss, the Goblin King).
        /// </summary>
        public bool IsBoss(RrEnemy e, int type)
        {
            if (Def == null)
            {
                return false;
            }

            return Def.BossType >= 0 ? type == Def.BossType : PointsKind(e, type) == RrQuestEvents.PointsBoss;
        }

        /// <summary>How a kill is shown (<see cref="RrQuestEvents.PointsKill"/>, or PointsBoss for the big coin burst).</summary>
        protected virtual byte PointsKind(RrEnemy e, int type)
        {
            return RrQuestEvents.PointsKill;
        }

        /// <summary>Authority: a member asked for a quest hazard (lever, boulder...): code and argument are the quest's own.</summary>
        protected virtual void AuthorityHazard(int playerId, int code, int arg)
        {
        }

        /// <summary>Every client, every frame while a run is on (act index, -1 in the lobby).</summary>
        protected virtual void QuestStep(int act)
        {
        }

        /// <summary>Every client: back in the lobby after a run (reset set pieces).</summary>
        protected virtual void OnRunReset()
        {
        }

        /// <summary>
        /// Same-act checkpoints (opt-in; false = Rec Room's older rule, a team wipe ends the run): when the whole team has
        /// been down for a second, the authority restarts the current act instead of failing - its arrival again, waves
        /// re-armed, hazards reset, everyone up (<see cref="EnterActLocal"/>), gold back to what it was when the act began
        /// (if lower, the lower stays). Not while the act is Clear (nothing left to restart; the bleed-out gets the team up).
        /// </summary>
        protected virtual bool RestartActOnWipe
        {
            get { return false; }
        }

        /// <summary>Every client: the current act was restarted at its checkpoint (reset the act's set pieces), before the team is moved back.</summary>
        protected virtual void OnActRestart(int act)
        {
        }

        /// <summary>The boss bar's name while the boss int is set.</summary>
        protected virtual string BossName
        {
            get { return "BOSS"; }
        }

        /// <summary>The toast when the local player falls into the act's lava (a quest may name its own hazard).</summary>
        protected virtual string LavaToast
        {
            get { return "Burned in the lava!"; }
        }

        // ------------------------------------------------------------------ RrGameRoom settings

        protected override int TeamCount
        {
            get { return 0; }
        }

        /// <summary>The quest's minimum (1 for every quest: small Somnium groups can always play).</summary>
        protected override int MinPlayersToStart
        {
            get { return Def != null && Def.MinPlayers > 0 ? Def.MinPlayers : 1; }
        }

        /// <summary>
        /// Silent phases: the quests' only voice line is Coach's "Game On!" (rr_latest_rgames_story.md 12.1: "no VO"
        /// except Gameon at the start), said by <see cref="SayStartLine"/> with a load check; no "Ready" before and no
        /// "Game Over" over the win / lose stinger.
        /// </summary>
        protected override string PhaseAnnouncement(RrPhase phase)
        {
            return null;
        }

        protected override float CountdownSeconds
        {
            get { return 5f; }
        }

        protected override float RoundSeconds
        {
            get { return 0f; }
        }

        protected override int Rounds
        {
            get { return 1; }
        }

        protected override bool AutoStart
        {
            get { return false; }
        }

        protected override string PlayMusic
        {
            get { return null; }
        }

        protected override string LobbyMusic
        {
            get { return Def != null ? Def.LobbyMusic : null; }
        }

        protected override string[] Loadout
        {
            get { return null; }
        }

        protected override int PlayerMaxHealth
        {
            get { return Def != null ? Def.PlayerHealth : 100; }
        }

        protected override float RespawnSeconds
        {
            get { return 0f; }
        }

        protected override float RespawnInvincibility
        {
            get { return Def != null ? Def.ReviveInvincible : 3f; }
        }

        public override Pose Spawn(int index)
        {
            return LobbySpawn(index);
        }

        // ------------------------------------------------------------------ state

        public int QuestInt(int i)
        {
            return State.Get(i);
        }

        /// <summary>Authority only (other clients' writes are ignored by the instance state).</summary>
        public void SetQuestInt(int i, int value)
        {
            if (IsAuthority && State.Get(i) != value)
            {
                State.Set(i, value);
            }
        }

        public int ActIndex
        {
            get { return Phase == RrPhase.Playing || Phase == RrPhase.MatchOver ? QuestInt(RrQuestInts.Act) : -1; }
        }

        public RrQuestStage Stage
        {
            get { return (RrQuestStage)QuestInt(RrQuestInts.Stage); }
        }

        public RrQuestAct CurrentAct
        {
            get
            {
                int a = ActIndex;
                return Def != null && a >= 0 && a < Def.Acts.Count ? Def.Acts[a] : null;
            }
        }

        public int Score
        {
            get { return QuestInt(RrQuestInts.Score); }
        }

        /// <summary>Seconds a running hazard has been going (0 when none): its duration int minus the synced timer.</summary>
        public float HazardSeconds
        {
            get
            {
                int ms = QuestInt(RrQuestInts.HazardTime);
                return ms > 0 ? Mathf.Max(0f, ms / 1000f - State.TimeRemaining) : 0f;
            }
        }

        public Pose ActEntryWorld(int act, int rank = 0)
        {
            var a = Def != null && act >= 0 && act < Def.Acts.Count ? Def.Acts[act] : null;
            if (a == null || a.Root == null)
            {
                return WorldSpawn(rank);
            }

            var rot = Quaternion.Euler(0f, a.EntryYaw, 0f);
            Vector3 local = a.Entry + rot * new Vector3((rank % 4 - 1.5f) * 1.1f, 0.05f, -(rank / 4) * 1.1f);
            return new Pose(a.Root.TransformPoint(local), a.Root.rotation * rot);
        }

        // ------------------------------------------------------------------ build

        public override IEnumerator Build()
        {
            Current = this;
            Def = CreateDef();
            FinalRules = RrQuestFinalRules.Apply(Info != null ? RrQuestFinalRules.For(Info.Activity) : null, Def);
            Mods = new RrQuestMods(this);
            Mods.AddWeapons(Def);
            LogRules();
            BuildProgress = 0.02f;
            var portal = RrPortal.Prepare();
            while (portal.MoveNext())
            {
                yield return portal.Current;
            }

            BuildProgress = 0.05f;
            yield return RrQuestKit.Prepare();
            Enemies = new RrQuestEnemies(this);
            Enemies.OnKilled += EnemyKilled;
            Enemies.OnReady += EnemyReady;
            Director = new RrQuestDirector(this);
            Pots = new RrQuestBreakables(this);
            Gear = new RrQuestGear(this);
            Hud = RrQuestHud.Create(transform);
            Revive = RrQuestRevive.Create(this);
            BuildProgress = 0.1f;
            yield return BuildLevel();
            BuildProgress = 0.9f;
            yield return null;
            RrQuestKit.Texture(transform);
            BuildProgress = 0.92f;
            yield return RrQuestKit.WaitForKit(45f);
            foreach (var act in Def.Acts)
            {
                if (act.ExitDoor != null)
                {
                    act.ExitDoor.SetOpen(false, true);
                }
            }

            BuildProgress = 1f;
            RrLog.Info("Quest", Def.Name + " built at " + Origin.ToString("F0") + ": " + Def.Acts.Count + " acts, " + Def.Enemies.Count + " enemy types, "
                + Pots.Count + " pots (" + Pots.TotalPoints + " " + Def.Currency + "), " + Racks.Count + " racks, " + RrQuestKit.Spawned + " kit props");
            LogPotential();
        }

        /// <summary>One line with the final rules this quest runs on (and a warning when the registry's cap differs).</summary>
        private void LogRules()
        {
            var kill = new System.Text.StringBuilder();
            for (int t = 0; t < Def.Enemies.Count; t++)
            {
                string n = Def.Enemies[t] ?? "?";
                int cut = n.IndexOf(')');
                kill.Append(t > 0 ? ", " : "").Append(cut >= 0 && cut + 1 < n.Length ? n.Substring(cut + 1) : n).Append(' ').Append(Def.KillPointsOf(t) > 0 ? Def.KillPointsOf(t).ToString() : "max HP");
            }

            string rating = Def.FixedRating
                ? "S " + Def.RatingGoldS.ToString("N0") + " / A " + Def.RatingGoldA.ToString("N0") + " / B " + Def.RatingGoldB.ToString("N0") + " " + Def.Currency
                : "share of the max possible (no published totals)";
            RrLog.Info("Quest", "final rules: " + (Def.PlayerHealth <= 1 ? "one hit downs" : Def.PlayerHealth + " HP") + ", players " + Def.MinPlayers + "-" + Def.MaxPlayers
                + ", death -" + Def.DeathPenalty + ", kill gold " + kill + (Def.BossKillPoints > 0 ? ", boss " + Def.BossKillPoints : "") + ", rating " + rating
                + (Def.StartLine != null ? ", start line " + Def.StartLine : ""));
            if (Info != null && (Info.MaxPlayers != Def.MaxPlayers || Info.MinPlayers > Def.MinPlayers))
            {
                RrLog.Warn("Quest", "the registry lists " + Info.MinPlayers + "-" + Info.MaxPlayers + " players, Rec Room's final cap is " + Def.MinPlayers + "-" + Def.MaxPlayers);
            }
        }

        /// <summary>
        /// What the level can pay with a full party (every wave's enemies at their kill gold plus every pot), against
        /// the fixed rating totals: if it is well under Rec Room's best, S is out of reach and the waves need more.
        /// Boss adds and optional zone spawns come on top.
        /// </summary>
        private void LogPotential()
        {
            int total = Pots != null ? Pots.TotalPoints : 0;
            int count = 0;
            int unknown = 0;
            foreach (var act in Def.Acts)
            {
                foreach (var w in act.Waves)
                {
                    foreach (var en in w.Entries)
                    {
                        int n = Def.Scaled(en.Count, Def.MaxPlayers);
                        int p = PotentialPoints(en.Enemy);
                        count += n;
                        total += n * p;
                        if (p <= 0)
                        {
                            unknown += n;
                        }
                    }
                }
            }

            RrLog.Info("Quest", "level potential with " + Def.MaxPlayers + " players: about " + total.ToString("N0") + " " + Def.Currency + " from " + count + " wave enemies"
                + (unknown > 0 ? " (" + unknown + " with no fixed gold)" : "") + " and " + (Pots != null ? Pots.Count : 0) + " pots, plus boss adds and optional side waves"
                + (Def.FixedRating ? "; S needs " + Def.RatingGoldS.ToString("N0") : "") + (Def.RecRoomMaxGold > 0 ? ", Rec Room's best ~" + Def.RecRoomMaxGold.ToString("N0") : ""));
        }

        private int PotentialPoints(int type)
        {
            bool boss = type == Def.BossType;
            if (Def.BossType < 0)
            {
                try
                {
                    // No enemy exists yet; the quests' PointsKind decides by type alone.
                    boss = PointsKind(null, type) == RrQuestEvents.PointsBoss;
                }
                catch (System.Exception)
                {
                    boss = false;
                }
            }

            return boss && Def.BossKillPoints > 0 ? Def.BossKillPoints : Def.KillPointsOf(type);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RrNet.On(RrQuestEvents.Request, OnRequest);
            RrNet.On(RrQuestEvents.Points, OnPoints);
            RrNet.On(RrQuestEvents.Toast, OnToast);
            RrNet.On(RrQuestEvents.Coconut, OnCoconut);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RrNet.Off(RrQuestEvents.Request, OnRequest);
            RrNet.Off(RrQuestEvents.Points, OnPoints);
            RrNet.Off(RrQuestEvents.Toast, OnToast);
            RrNet.Off(RrQuestEvents.Coconut, OnCoconut);
        }

        private void OnCoconut(int sender, byte[] body)
        {
            if (Mods != null)
            {
                Mods.OnCoconut(sender, body);
            }
        }

        private void OnDestroy()
        {
            if (Enemies != null)
            {
                Enemies.Dispose();
            }

            if (Pots != null)
            {
                Pots.Dispose();
            }

            if (Gear != null)
            {
                Gear.Clear();
            }

            if (Current == this)
            {
                Current = null;
            }
        }

        public override void OnLocalEnter()
        {
            base.OnLocalEnter();
            RrEnemy.ExternalSync = true;
            _seenAct = -2;
            _seenStage = -1;
            _seenOutcome = 0;
            RrLog.Info("Quest", "entered " + (Def != null ? Def.Name : "quest") + " (authority " + IsAuthority + ", members " + Members().Count + ")");
        }

        public override void OnLocalExit()
        {
            base.OnLocalExit();
            RrEnemy.ExternalSync = false;
            if (Gear != null)
            {
                Gear.Clear();
            }

            if (Enemies != null)
            {
                Enemies.ClearAll(false);
            }
        }

        // ------------------------------------------------------------------ requests

        /// <summary>Sends a request to the instance authority (the authority handles its own at once).</summary>
        public void Ask(byte code, params byte[] args)
        {
            var b = new byte[1 + (args != null ? args.Length : 0)];
            b[0] = code;
            if (args != null)
            {
                System.Array.Copy(args, 0, b, 1, args.Length);
            }

            RrNet.Send(RrQuestEvents.Request, b, true);
        }

        private void OnRequest(int sender, byte[] body)
        {
            if (!IsAuthority || body == null || body.Length < 1 || Def == null)
            {
                return;
            }

            int i = 1;
            switch (body[0])
            {
                case RrQuestEvents.ReqAdvance:
                {
                    int act = RrQuestEvents.U8(body, ref i);
                    if (Phase == RrPhase.Playing && act == ActIndex && Stage == RrQuestStage.Clear && act + 1 < Def.Acts.Count)
                    {
                        EnterActAuthority(act + 1);
                    }

                    break;
                }
                case RrQuestEvents.ReqBreak:
                    Pots.AuthorityBreak(RrQuestEvents.U16(body, ref i));
                    break;
                case RrQuestEvents.ReqStun:
                {
                    int slot = RrQuestEvents.U8(body, ref i);
                    int serial = RrQuestEvents.U8(body, ref i);
                    int tenths = RrQuestEvents.U8(body, ref i);
                    var e = Enemies.At(slot);
                    if (e != null && e.Id == 1 + slot * 8 + (serial & 7))
                    {
                        e.Stun(tenths / 10f);
                    }

                    break;
                }
                case RrQuestEvents.ReqHazard:
                    AuthorityHazard(sender, RrQuestEvents.U8(body, ref i), RrQuestEvents.U8(body, ref i));
                    break;
                case RrQuestEvents.ReqMods:
                    if (Mods != null)
                    {
                        Mods.AuthoritySet(RrQuestEvents.U8(body, ref i));
                    }

                    break;
            }
        }

        /// <summary>Authority: a toast on every member's HUD.</summary>
        public void Broadcast(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                RrNet.Send(RrQuestEvents.Toast, System.Text.Encoding.UTF8.GetBytes(text));
            }
        }

        private void OnToast(int sender, byte[] body)
        {
            if (Hud != null && body != null)
            {
                Hud.Toast(System.Text.Encoding.UTF8.GetString(body), 2.5f);
            }
        }

        // ------------------------------------------------------------------ gold

        /// <summary>Authority: adds (or with a negative amount takes) team gold and shows it where it happened.</summary>
        public void AddPoints(int amount, Vector3 at, byte kind)
        {
            if (!IsAuthority || amount == 0)
            {
                return;
            }

            SetQuestInt(RrQuestInts.Score, Mathf.Max(0, Score + amount));
            var b = new byte[4 + 12 + 1];
            int i = 0;
            RrQuestEvents.I32(b, ref i, amount);
            RrQuestEvents.Pos(b, ref i, at);
            b[i] = kind;
            RrNet.Send(RrQuestEvents.Points, b);
        }

        private void OnPoints(int sender, byte[] body)
        {
            if (body == null || body.Length < 17 || Def == null)
            {
                return;
            }

            int i = 0;
            int amount = RrQuestEvents.I32(body, ref i);
            Vector3 at = RrQuestEvents.Pos(body, ref i);
            byte kind = body[i];
            if (amount > 0)
            {
                bool boss = kind == RrQuestEvents.PointsBoss;
                RrFx.Spawn(boss ? Def.BossPointsFx : Def.PointsFx, at, Quaternion.identity, 1f);
                RrSound.Play(boss ? Def.BossPointsSound : Def.PointsSound, at, 0.9f, 1f);
                if (kind == RrQuestEvents.PointsLava)
                {
                    RrFx.Spawn("VFX_Lava_Bubbles_Death01", at + Vector3.down * 0.8f, Quaternion.identity, 1f);
                    RrSound.Play("Lava Sink", at, 0.8f);
                }

                Hud.Pop(at + Vector3.up * 0.3f, "+" + amount, new Color(1f, 0.84f, 0.25f));
            }
            else
            {
                Hud.Pop(at + Vector3.up * 1.9f, amount.ToString(), new Color(1f, 0.3f, 0.25f));
            }
        }

        private void EnemyKilled(RrEnemy e, int type, int killer)
        {
            SetQuestInt(RrQuestInts.Kills, QuestInt(RrQuestInts.Kills) + 1);
            var act = CurrentAct;
            bool lava = act != null && act.InLava(e.transform.position, 0.6f);
            AddPoints(PointsFor(e, type), e.Center, lava ? RrQuestEvents.PointsLava : PointsKind(e, type));
            if (Def.BossDeathKillsAll && IsBoss(e, type))
            {
                AuthorityBossDefeated(e);
            }
        }

        /// <summary>
        /// Authority: the boss is dead, so the fight is over. Every other enemy dies with it, with no gold (Rec Room's
        /// Goblin King: "his death kills every other enemy", WIKI; whether they paid is unpublished, GUESS none), the
        /// act's remaining waves and queued spawns are dropped so the act clears, and the boss bar goes. The room
        /// calls it itself when the boss type dies (<see cref="RrQuestDef.BossDeathKillsAll"/>); boss code may call it
        /// too (a scripted death, Jumbotron's 4th panel). Safe to call more than once. Returns how many enemies died.
        /// </summary>
        public int AuthorityBossDefeated(RrEnemy boss)
        {
            if (!IsAuthority || Enemies == null || Phase != RrPhase.Playing)
            {
                return 0;
            }

            int n = Enemies.KillAllExcept(boss);
            var act = CurrentAct;
            if (act != null)
            {
                Director.Reset(QuestInt(RrQuestInts.Seed) + ActIndex * 7919);
                SetQuestInt(RrQuestInts.Wave, act.Waves.Count);
                SetQuestInt(RrQuestInts.SideWaves, 0xFFFF);
            }

            SetQuestInt(RrQuestInts.BossHealth, 0);
            if (n > 0 || !_loggedBossKill)
            {
                _loggedBossKill = true;
                RrLog.Info("Quest", "the boss is down: " + n + " other enemies die with it (act " + (ActIndex + 1) + ")");
            }

            return n;
        }

        private void EnemyReady(RrEnemy e, int type)
        {
            OnEnemyReady(e, type);
            if (Mods != null)
            {
                // After the quest's own setup (the king sets his speeds), so Turbo speeds up everything.
                Mods.EnemyReady(e);
            }

            if (IsAuthority && Phase == RrPhase.Playing)
            {
                SetQuestInt(RrQuestInts.MaxPossible, QuestInt(RrQuestInts.MaxPossible) + PointsFor(e, type));
            }
        }

        // ------------------------------------------------------------------ authority: the run

        protected override void AuthorityRoundStart(int round)
        {
            int party = Mathf.Clamp(QuestMembers().Count, 1, Def.MaxPlayers > 0 ? Def.MaxPlayers : 4);
            int seed = Random.Range(1, 1 << 20);
            SetQuestInt(RrQuestInts.Score, 0);
            SetQuestInt(RrQuestInts.MaxPossible, Pots != null ? Pots.TotalPoints : 0);
            SetQuestInt(RrQuestInts.Outcome, 0);
            SetQuestInt(RrQuestInts.Party, party);
            SetQuestInt(RrQuestInts.Downs, 0);
            SetQuestInt(RrQuestInts.Kills, 0);
            SetQuestInt(RrQuestInts.Revives, 0);
            SetQuestInt(RrQuestInts.Seed, seed);
            _wasDown.Clear();
            _allDownSince = -1f;
            _restartGrace = -1f;
            Enemies.ClearAll();
            EnterActAuthority(0);
            int mods = QuestInt(RrQuestInts.Mods);
            RrLog.Info("Quest", Def.Name + ": run started with " + party + " player(s)" + (mods != 0 ? ", mods " + Mods.Describe(mods) : ""));
        }

        /// <summary>Authority: moves the run to an act (its Intro stage).</summary>
        protected void EnterActAuthority(int act)
        {
            Enemies.ClearAll();
            SetQuestInt(RrQuestInts.Act, act);
            SetQuestInt(RrQuestInts.Stage, (int)RrQuestStage.Intro);
            SetQuestInt(RrQuestInts.Wave, 0);
            SetQuestInt(RrQuestInts.Hazards, 0);
            SetQuestInt(RrQuestInts.HazardTime, 0);
            SetQuestInt(RrQuestInts.SideWaves, 0);
            SetQuestInt(RrQuestInts.BossHealth, 0);
            SetQuestInt(RrQuestInts.BossPhase, 0);
            State.SetTimer(Def.IntroSeconds);
            Director.Reset(QuestInt(RrQuestInts.Seed) + act * 7919);
            // The checkpoint's gold (RestartActOnWipe): only this authority knows it; a new authority restarts without it.
            _actStartAct = act;
            _actStartScore = QuestInt(RrQuestInts.Score);
            _actStartMax = QuestInt(RrQuestInts.MaxPossible);
            RrLog.Info("Quest", "act " + (act + 1) + "/" + Def.Acts.Count + " \"" + Def.Acts[act].Title + "\"");
        }

        /// <summary>Authority: the whole team is down and <see cref="RestartActOnWipe"/>: the current act starts again.</summary>
        private void RestartActAuthority()
        {
            int a = ActIndex;
            _allDownSince = -1f;
            _restartGrace = Time.time + Def.IntroSeconds + 2f;
            string gold = "gold kept (this authority did not start the act)";
            if (_actStartAct == a)
            {
                // Enemies killed in the failed attempt pay nothing (they come back); the downs' penalties stay if they cost more.
                int score = Mathf.Min(QuestInt(RrQuestInts.Score), _actStartScore);
                SetQuestInt(RrQuestInts.Score, score);
                SetQuestInt(RrQuestInts.MaxPossible, _actStartMax);
                gold = "gold back to " + score;
            }

            EnterActAuthority(a);
            RrLog.Info("Quest", Def.Name + ": the whole team is down in act " + (a + 1) + " - back to the act's checkpoint (" + gold + ")");
        }

        protected override void AuthorityPlaying()
        {
            if (Def == null)
            {
                return;
            }

            int a = ActIndex;
            var act = CurrentAct;
            if (act == null)
            {
                return;
            }

            DownsAndFail();
            if (Phase != RrPhase.Playing)
            {
                return;
            }

            switch (Stage)
            {
                case RrQuestStage.Intro:
                    if (State.TimeRemaining <= 0f)
                    {
                        SetQuestInt(RrQuestInts.Stage, (int)RrQuestStage.Combat);
                    }

                    break;
                case RrQuestStage.Combat:
                    Director.Step(act);
                    AuthorityActStep(a);
                    LavaKills(act);
                    if (Director.AllSpawned(act) && Enemies.Alive == 0 && AuthorityActDone(a))
                    {
                        SetQuestInt(RrQuestInts.Stage, (int)RrQuestStage.Clear);
                        State.SetTimer(a + 1 >= Def.Acts.Count ? 6f : 0f);
                        RrLog.Info("Quest", "act " + (a + 1) + " clear (gold " + Score + ")");
                    }

                    break;
                case RrQuestStage.Clear:
                    if (a + 1 >= Def.Acts.Count && State.TimeRemaining <= 0f)
                    {
                        Finish(true);
                    }

                    break;
            }
        }

        private void LavaKills(RrQuestAct act)
        {
            if (act.Lava.Count == 0)
            {
                return;
            }

            for (int s = 0; s < RrQuestEnemies.Slots; s++)
            {
                var e = Enemies.At(s);
                if (e == null || !e.Alive || e.State == RrEnemyState.Spawning)
                {
                    continue;
                }

                bool flyer = e.Kind == RrEnemyKind.Flyer;
                // Ground enemies that step into the lava, and flyers knocked down (stunned) over it, die (wiki: act 4 imps).
                if (act.InLava(e.transform.position, flyer ? 1.5f : 0.3f) && (!flyer || e.State == RrEnemyState.Stunned))
                {
                    e.Die(RrCombat.NoPlayer, true);
                }
            }
        }

        private List<int> QuestMembers()
        {
            _members.Clear();
            _members.AddRange(Members());
            if (!_members.Contains(RrNet.LocalId))
            {
                _members.Add(RrNet.LocalId);
            }

            _members.Sort();
            return _members;
        }

        private bool IsDown(int id)
        {
            if (id == RrNet.LocalId)
            {
                var h = RrHealth.Local;
                return h != null && h.IsOut;
            }

            return RrNet.Presence(id).Status == RrStatus.Down;
        }

        private void DownsAndFail()
        {
            var members = QuestMembers();
            int up = 0;
            foreach (int id in members)
            {
                bool down = IsDown(id);
                bool was;
                _wasDown.TryGetValue(id, out was);
                if (down && !was)
                {
                    SetQuestInt(RrQuestInts.Downs, QuestInt(RrQuestInts.Downs) + 1);
                    Vector3 at = id == RrNet.LocalId ? RrPlayer.FeetPosition : RrRemotePlayers.Get(id) != null && RrRemotePlayers.Get(id).Root != null ? RrRemotePlayers.Get(id).Root.position : Origin;
                    AddPoints(-Def.DeathPenalty, at, RrQuestEvents.PointsKill);
                    RrLog.Info("Quest", "player " + id + " is down: -" + Def.DeathPenalty + " " + Def.Currency);
                }

                _wasDown[id] = down;
                if (!down)
                {
                    up++;
                }
            }

            if (up > 0 || members.Count == 0)
            {
                _allDownSince = -1f;
                return;
            }

            bool checkpoint = RestartActOnWipe && ActIndex >= 0;
            if (checkpoint && (Stage == RrQuestStage.Clear || Time.time < _restartGrace))
            {
                // Clear: nothing to restart (the bleed-out gets everyone up). Grace: the members are still getting up
                // from the last restart (their status takes a moment to arrive).
                _allDownSince = -1f;
                return;
            }

            if (_allDownSince < 0f)
            {
                _allDownSince = Time.time;
            }
            else if (Time.time - _allDownSince > 1f)
            {
                if (checkpoint)
                {
                    RestartActAuthority();
                }
                else
                {
                    Finish(false);
                }
            }
        }

        /// <summary>Authority: ends the run (victory or failure screen, then the lobby).</summary>
        protected void Finish(bool victory)
        {
            if (!IsAuthority || Phase != RrPhase.Playing)
            {
                return;
            }

            SetQuestInt(RrQuestInts.Outcome, victory ? 1 : 2);
            if (!victory)
            {
                Enemies.ClearAll();
            }

            State.SetPhase(RrPhase.MatchOver, victory ? Def.VictorySeconds : Def.FailSeconds);
            RrLog.Info("Quest", Def.Name + (victory ? ": VICTORY" : ": FAILED") + " in act " + (ActIndex + 1) + ", " + Score + " " + Def.Currency
                + " of " + QuestInt(RrQuestInts.MaxPossible) + " (rating " + Def.Rating(Score, QuestInt(RrQuestInts.MaxPossible))
                + (Def.FixedRating ? " by the fixed totals S " + Def.RatingGoldS + " / A " + Def.RatingGoldA + " / B " + Def.RatingGoldB : " by share") + "), kills "
                + QuestInt(RrQuestInts.Kills) + ", downs " + QuestInt(RrQuestInts.Downs) + ", revives " + QuestInt(RrQuestInts.Revives));
        }

        public override void OnAuthorityChanged(bool isAuthority)
        {
            base.OnAuthorityChanged(isAuthority);
            // The checkpoint's gold is only known to the authority that started the act (a stale copy must not be used);
            // a new authority waits a moment before judging a wipe (members may still be getting up from a restart).
            _actStartAct = -1;
            _restartGrace = isAuthority ? Time.time + 3f : -1f;
        }

        protected override void AuthorityBackToLobby()
        {
            _actStartAct = -1;
            Enemies.ClearAll();
            SetQuestInt(RrQuestInts.Act, -1);
            SetQuestInt(RrQuestInts.Stage, 0);
            SetQuestInt(RrQuestInts.Outcome, 0);
            SetQuestInt(RrQuestInts.BossHealth, 0);
        }

        // ------------------------------------------------------------------ every client

        protected override void OnPhase(RrPhase old, RrPhase now)
        {
            var h = RrHealth.Local;
            if (now == RrPhase.Playing)
            {
                if (h != null && Def != null)
                {
                    h.OutMovementScale = Def.DownedMovement;
                    h.RespawnPoint = () => ActEntryWorld(Mathf.Max(0, ActIndex), Rank());
                    h.Accepts = AcceptHit;
                }

                _seenAct = -2;
                SayStartLine();
            }
            else if (now == RrPhase.Lobby && (old == RrPhase.MatchOver || old == RrPhase.Playing))
            {
                BackToLobby();
            }
        }

        private void BackToLobby()
        {
            _seenAct = -2;
            _seenStage = -1;
            _seenOutcome = 0;
            _exploreAt = -1f;
            Enemies.ClearAll(false);
            Pots.ResetAll();
            foreach (var r in Racks)
            {
                if (r != null)
                {
                    r.ResetRack();
                }
            }

            foreach (var act in Def.Acts)
            {
                if (act.ExitDoor != null)
                {
                    act.ExitDoor.SetOpen(false, true);
                }
            }

            Hud.ClearTitle();
            Hud.SetBoss(null, -1f);
            var p = WorldSpawn(Rank());
            RrPlayer.Teleport(p.position, p.rotation.eulerAngles.y);
            OnRunReset();
            RrLog.Info("Quest", "back in the lobby");
        }

        protected int Rank()
        {
            var m = QuestMembers();
            int i = m.IndexOf(RrNet.LocalId);
            return Mathf.Max(0, i);
        }

        /// <summary>
        /// The local player's hit veto while a run is on (<see cref="RrHealth.Accepts"/>). Rec Room: "one hit from an
        /// enemy (or an ally's ranged weapon) will down you" - so an ally's sword or shield (a registered melee tool)
        /// is ignored, and everything else downs at once (PlayerHealth 1). In a harness run every hit is only logged.
        /// </summary>
        private bool AcceptHit(int damage, int attacker, byte tool)
        {
            string from = attacker >= 0 ? "player " + attacker : "an enemy";
            if (attacker >= 0 && tool >= FirstRegisteredTool && tool < RrCombat.EnemyTool)
            {
                if (!_loggedAllyMelee)
                {
                    _loggedAllyMelee = true;
                    RrLog.Info("Quest", "an ally's melee hit (" + from + ", tool " + tool + ") does not down: only enemies and allies' ranged weapons do");
                }

                return false;
            }

            if (TestGuard)
            {
                if (!_loggedGuard)
                {
                    _loggedGuard = true;
                    RrLog.Info("Quest", "harness guard: a " + damage + "-damage hit from " + from + " would down the local player (one hit downs); ignored so the scenario goes on");
                }

                return false;
            }

            if (!_loggedOneHit)
            {
                _loggedOneHit = true;
                RrLog.Info("Quest", "one hit downs: a " + damage + "-damage hit from " + from + " downs the local player (health " + (RrHealth.Local != null ? RrHealth.Local.MaxHealth : 0) + ")");
            }

            return true;
        }

        /// <summary>Every client as the run starts: Coach's "Game On!" (clip Gameon), with a one-time log that it loaded.</summary>
        private void SayStartLine()
        {
            string clip = Def != null ? Def.StartLine : null;
            if (string.IsNullOrEmpty(clip))
            {
                return;
            }

            RrAnnouncer.Say(clip, 1f, loaded =>
            {
                if (loaded == null)
                {
                    RrLog.WarnOnce("Quest", "start-line", "Coach's start line \"" + clip + "\" is not in the install: the run starts silently");
                }
                else if (!_loggedStartLine)
                {
                    _loggedStartLine = true;
                    RrLog.Info("Quest", "Coach: \"Game On!\" (" + loaded.name + ", " + loaded.length.ToString("0.00") + " s, " + loaded.frequency + " Hz)");
                }
            });
        }

        /// <summary>Feet of every member who is not down (the local player and the remote bodies in this instance).</summary>
        public List<Vector3> StandingPlayerFeet()
        {
            _feet.Clear();
            foreach (int id in QuestMembers())
            {
                if (IsDown(id))
                {
                    continue;
                }

                if (id == RrNet.LocalId)
                {
                    _feet.Add(RrPlayer.FeetPosition);
                    continue;
                }

                var r = RrRemotePlayers.Get(id);
                if (r != null && r.Root != null)
                {
                    _feet.Add(r.Root.position);
                }
            }

            return _feet;
        }

        /// <summary>Every client: every rack re-hangs its weapons (the rack set changed: Laser Weapons).</summary>
        public void RefillRacks()
        {
            foreach (var r in Racks)
            {
                if (r != null)
                {
                    r.Restock();
                }
            }

            RrLog.Info("Quest", "racks restocked (" + Racks.Count + ")" + (Mods != null && Mods.Has(RrQuestMods.LaserWeapons) ? " with Laser Tag guns" : " with the quest's weapons"));
        }

        /// <summary>Seconds between a wave's spawns are multiplied by this (Turbo: 0.67).</summary>
        public float WaveIntervalScale
        {
            get { return Mods != null && Mods.Has(RrQuestMods.Turbo) ? RrQuestMods.TurboCooldown : 1f; }
        }

        protected override void OnTick(RrInstanceState state)
        {
            if (Def == null || Enemies == null)
            {
                return;
            }

            Enemies.Step();
            Pots.Step();
            Gear.Step();
            Mods.Step();
            bool playing = Phase == RrPhase.Playing;
            Revive.Step(playing);
            if (playing)
            {
                ActWatch();
                ExitCheck();
                LocalLava();
            }
            else if (Phase == RrPhase.MatchOver)
            {
                ShowOutcome();
            }

            if (_exploreAt > 0f && Time.time >= _exploreAt)
            {
                _exploreAt = -1f;
                if (Phase == RrPhase.Playing && !string.IsNullOrEmpty(Def.ExploreMusic))
                {
                    RrMusic.Play(Def.ExploreMusic, 0.5f);
                }
            }

            UpdateHud();
            QuestStep(ActIndex);
        }

        private void ActWatch()
        {
            int a = ActIndex;
            if (a < 0 || a >= Def.Acts.Count)
            {
                return;
            }

            if (a != _seenAct)
            {
                _seenAct = a;
                _seenStage = -1;
                EnterActLocal(a);
            }

            int st = (int)Stage;
            if (st != _seenStage)
            {
                // Within an act the stage only moves on (Intro, Combat, Clear): back to Intro in the same act is the
                // authority restarting it at its checkpoint (RestartActOnWipe; the act int does not change).
                if (st == (int)RrQuestStage.Intro && _seenStage > (int)RrQuestStage.Intro)
                {
                    OnActRestart(a);
                    EnterActLocal(a);
                    Hud.Toast("Back to the checkpoint!", 3f);
                    RrLog.Info("Quest", "act " + (a + 1) + " restarted at its checkpoint: local player back at the arrival");
                }

                _seenStage = st;
                StageChanged(a, (RrQuestStage)st);
            }

            for (int i = 0; i < Def.Acts.Count; i++)
            {
                var door = Def.Acts[i].ExitDoor;
                if (door != null)
                {
                    door.SetOpen(i < a || (i == a && Stage == RrQuestStage.Clear));
                }
            }
        }

        private void EnterActLocal(int a)
        {
            var act = Def.Acts[a];
            var h = RrHealth.Local;
            if (h != null && h.InCombat)
            {
                // A player still down gets up with the team in the new act (GUESS), and everyone starts it healed (GUESS).
                if (h.IsOut)
                {
                    h.Respawn(false);
                }
                else
                {
                    h.Heal(h.MaxHealth);
                }
            }

            var p = ActEntryWorld(a, Rank());
            RrPlayer.Teleport(p.position, p.rotation.eulerAngles.y);
            if (!act.Prelude)
            {
                Hud.Title(act.Title, act.Subtitle, Def.IntroSeconds + 0.5f);
            }

            if (!string.IsNullOrEmpty(act.ArrivalToast))
            {
                Hud.Toast(act.ArrivalToast, 5f);
            }

            if (!act.Boss && !string.IsNullOrEmpty(Def.ExploreMusic))
            {
                RrMusic.Play(Def.ExploreMusic, 0.5f);
            }

            RrLog.Info("Quest", "local player into act " + (a + 1) + " at " + p.position.ToString("F1"));
        }

        private void StageChanged(int a, RrQuestStage stage)
        {
            var act = Def.Acts[a];
            if (act.Prelude)
            {
                // The dressing room: its door just opens (no combat music, fanfare or toast).
            }
            else if (stage == RrQuestStage.Combat)
            {
                string m = act.Boss && !string.IsNullOrEmpty(Def.BossMusic) ? Def.BossMusic : Def.CombatMusic;
                if (!string.IsNullOrEmpty(m))
                {
                    RrMusic.Play(m, 0.55f);
                }

                _exploreAt = -1f;
            }
            else if (stage == RrQuestStage.Clear)
            {
                if (!string.IsNullOrEmpty(Def.ClearFanfare))
                {
                    RrMusic.Play(Def.ClearFanfare, 0.7f, false);
                    _exploreAt = Time.time + Def.ClearFanfareSeconds + 0.3f;
                }

                if (a + 1 < Def.Acts.Count)
                {
                    Hud.Toast("The way is open!", 3f);
                }
            }

            OnActStage(a, stage);
        }

        private void ExitCheck()
        {
            int a = ActIndex;
            var act = CurrentAct;
            if (act == null || act.ExitDoor == null || Stage != RrQuestStage.Clear || a + 1 >= Def.Acts.Count || Time.time < _nextAsk)
            {
                return;
            }

            if (act.ExitDoor.Through(RrPlayer.FeetPosition))
            {
                _nextAsk = Time.time + 0.5f;
                Ask(RrQuestEvents.ReqAdvance, (byte)a);
            }
        }

        private void LocalLava()
        {
            var act = CurrentAct;
            var h = RrHealth.Local;
            if (act == null || h == null || !h.InCombat || Time.time < _lavaGuard || act.Lava.Count == 0)
            {
                return;
            }

            Vector3 feet = RrPlayer.FeetPosition;
            if (!act.InLava(feet, 0.25f))
            {
                return;
            }

            _lavaGuard = Time.time + 2f;
            RrFx.Spawn("VFX_Lava_Bubbles_Death01", feet, Quaternion.identity, 1f);
            // Rec Room's floor-is-lava clips: the touch hiss, then the sink.
            RrSound.Play("Lava Touch", feet, 1f);
            RrSound.Play("Lava Sink", feet, 0.8f);
            if (!h.IsOut && !h.Invincible)
            {
                h.GoOut(RrCombat.NoPlayer, RrCombat.EnemyTool, feet);
            }

            var p = ActEntryWorld(ActIndex, Rank());
            RrPlayer.Teleport(p.position, p.rotation.eulerAngles.y);
            Hud.Toast(LavaToast, 2f);
            RrLog.Info("Quest", "local player fell into the lava");
        }

        private void ShowOutcome()
        {
            int outcome = QuestInt(RrQuestInts.Outcome);
            if (outcome == _seenOutcome || outcome == 0)
            {
                return;
            }

            _seenOutcome = outcome;
            int max = QuestInt(RrQuestInts.MaxPossible);
            string sub = Score.ToString("N0") + " " + Def.Currency.ToUpperInvariant() + "   RATING " + Def.Rating(Score, max);
            Mods.RunOver(outcome == 1);
            if (outcome == 1)
            {
                RrMusic.Play(Def.WinStinger, 0.8f, false);
                Hud.Title("OUR HEROES ARE VICTORIOUS!", sub, Def.VictorySeconds);
            }
            else
            {
                RrMusic.Play(Def.LoseStinger, 0.8f, false);
                Hud.Title("OUR HEROES HAVE FAILED", sub, Def.FailSeconds, new Color(1f, 0.45f, 0.4f));
            }

            RrLog.Info("Quest", "outcome shown: " + (outcome == 1 ? "victory" : "failed") + " - " + sub);
        }

        private void UpdateHud()
        {
            if (Hud == null)
            {
                return;
            }

            bool run = Phase == RrPhase.Playing || Phase == RrPhase.MatchOver;
            Hud.SetScore(run ? Def.Currency.ToUpperInvariant() + "  " + Score.ToString("N0") : "");
            // Rec Room's quests have no health bar (one hit downs): only the down state is shown.
            var h = RrHealth.Local;
            if (run && h != null && h.InCombat && h.IsOut)
            {
                Hud.SetHealth("DOWN", new Color(1f, 0.35f, 0.3f));
            }
            else if (run && h != null && h.InCombat && Def.PlayerHealth > 1)
            {
                Hud.SetHealth("HP " + h.Health, h.Health <= 30 ? new Color(1f, 0.6f, 0.3f) : Color.white);
            }
            else
            {
                Hud.SetHealth("", Color.white);
            }

            var act = CurrentAct;
            int boss = QuestInt(RrQuestInts.BossHealth);
            Hud.SetBoss(BossName, Phase == RrPhase.Playing && act != null && act.Boss && boss > 0 ? boss / 1000f : -1f);
        }

        // ------------------------------------------------------------------ lobby helpers

        /// <summary>The lobby's start button (Rec Room's quest start): starts the run from any member.</summary>
        protected RrPressable StartButton(Transform parent, Vector3 local, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            RrBuild.Box(parent, "Start Pedestal", local + Vector3.up * 0.5f, rot, new Vector3(0.7f, 1f, 0.7f), RrQuestKit.StoneDark, true, RrLayers.Environment);
            var b = RrPressable.Create(parent, "Start Button", local + Vector3.up * 1.03f, rot, new Color(0.95f, 0.25f, 0.2f), 0.4f, () =>
            {
                if (Phase == RrPhase.Lobby)
                {
                    RrLog.Info("Quest", "start pressed");
                    RequestStart();
                }
            });
            RrBuild.Sign(parent, "Start Sign", "START QUEST", local + rot * new Vector3(0f, 1.8f, 0f), rot, 3f, new Color(1f, 0.85f, 0.4f), true, 3f);
            return b;
        }

        /// <summary>
        /// The lobby's RRO mod board (Golden Trophy 2025-12: Turbo, Coconut Chaos, Laser Weapons; see
        /// <see cref="RrQuestMods"/>): a 2.7 m table with three toggle buttons and lamps, players on its -z side of
        /// yaw, as at the start button. Call it from BuildLevel (or a level builder), where the lobby is built.
        /// </summary>
        public Transform ModBoard(Transform parent, Vector3 local, float yaw)
        {
            return Mods.Board(parent, local, yaw);
        }

        // ------------------------------------------------------------------ test hooks (RrQuestTest)

        /// <summary>Authority: kills every enemy (no gold) and marks every wave of the act as spawned.</summary>
        public void DebugClearAct()
        {
            if (!IsAuthority || CurrentAct == null)
            {
                return;
            }

            Enemies.KillAll();
            Director.Reset(0);
            SetQuestInt(RrQuestInts.Stage, (int)RrQuestStage.Combat);
            SetQuestInt(RrQuestInts.Wave, CurrentAct.Waves.Count);
            SetQuestInt(RrQuestInts.SideWaves, 0xFFFF);
        }

        /// <summary>Authority: jumps the run to an act (Intro).</summary>
        public void DebugGoToAct(int act)
        {
            if (IsAuthority && Phase == RrPhase.Playing && Def != null && act >= 0 && act < Def.Acts.Count)
            {
                EnterActAuthority(act);
            }
        }

        public string Describe()
        {
            return (Def != null ? Def.Name : "?") + " phase " + Phase + " act " + (ActIndex + 1) + " stage " + Stage + " wave " + QuestInt(RrQuestInts.Wave)
                + " alive " + (Enemies != null ? Enemies.Alive : 0) + " gold " + Score + "/" + QuestInt(RrQuestInts.MaxPossible) + " kills " + QuestInt(RrQuestInts.Kills)
                + " pots broken " + (Pots != null ? Pots.BrokenCount : 0) + " outcome " + QuestInt(RrQuestInts.Outcome);
        }
    }
}
