namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>Where the current act is in its life (instance custom int <see cref="RrQuestInts.Stage"/>).</summary>
    public enum RrQuestStage
    {
        /// <summary>The team has just arrived: title card, no enemies yet (a few seconds).</summary>
        Intro = 0,
        /// <summary>Waves are running.</summary>
        Combat = 1,
        /// <summary>Every wave is beaten: fanfare, the exit door is open (the final act: victory follows).</summary>
        Clear = 2
    }

    /// <summary>
    /// The quest's slice of the instance state: <see cref="Net.RrInstanceState.Get"/> custom ints 0..31
    /// (instance ints 27..58), written only by the instance authority (rr_quests.md 3.10, simplified to one value per
    /// int because there is room). Phase, timer and roster stay in the base ints of <see cref="RrGameRoom"/>.
    /// </summary>
    public static class RrQuestInts
    {
        /// <summary>Current act index, -1 in the lobby.</summary>
        public const int Act = 0;
        /// <summary><see cref="RrQuestStage"/> of the current act.</summary>
        public const int Stage = 1;
        /// <summary>Index of the next wave of the current act to trigger.</summary>
        public const int Wave = 2;
        /// <summary>Team score in the quest's currency (Gold).</summary>
        public const int Score = 3;
        /// <summary>Everything the team could have earned so far (spawned enemies + breakables), for the rating.</summary>
        public const int MaxPossible = 4;
        /// <summary>0 while playing, 1 victory, 2 failed.</summary>
        public const int Outcome = 5;
        /// <summary>The RRO mod bits picked in the lobby (<see cref="RrQuestMods"/>: 1 Turbo, 2 Coconut Chaos, 4 Laser Weapons).</summary>
        public const int Mods = 6;
        /// <summary>Party size when the run started (1-4): scales wave sizes and boss health.</summary>
        public const int Party = 7;
        /// <summary>Times a player went down (each costs the death penalty).</summary>
        public const int Downs = 8;
        /// <summary>Enemies killed this run.</summary>
        public const int Kills = 9;
        /// <summary>Revives this run.</summary>
        public const int Revives = 10;
        /// <summary>Boss health in permille (0 = no boss bar).</summary>
        public const int BossHealth = 11;
        /// <summary>Boss phase (quest-specific; the Goblin King's add waves).</summary>
        public const int BossPhase = 12;
        /// <summary>Hazard/puzzle bits of the current act (quest-specific: bit 0 = boulder released...).</summary>
        public const int Hazards = 13;
        /// <summary>
        /// A running hazard's total duration, ms; its progress is that minus the instance timer (the timer travels as
        /// remaining ms, so every client agrees without a shared clock). See <see cref="RrQuestRoom.HazardSeconds"/>.
        /// </summary>
        public const int HazardTime = 14;
        /// <summary>Seed of the run (spawn jitter).</summary>
        public const int Seed = 15;
        /// <summary>First int a quest may use for itself (16..30).</summary>
        public const int FirstQuestSpecific = 16;
        /// <summary>Last int a quest may use for itself (31 is the framework's <see cref="SideWaves"/>).</summary>
        public const int LastQuestSpecific = 30;
        /// <summary>Bits of the current act's side waves that have started (<see cref="RrQuestAct.SideWave"/>).</summary>
        public const int SideWaves = 31;
    }
}
