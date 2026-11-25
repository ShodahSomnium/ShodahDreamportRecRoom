namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// The playlist of Rec Room's `(Radio)` (41 SFXTracks, randomInitialTrack), in the prefab's order (rr_reccenter.md §2.6, `_rc_tracks.tsv`): AudioClip names, all loadable by name
    /// from their Addressables containers, and the friendly names Rec Room showed.
    /// </summary>
    internal static class RrRecCenterTracks
    {
        public static readonly string[] RadioClips =
        {
            "SportFunk", "InTheBassment", "SecretRoom", "GoatAttitude", "Organism", "RiverPredator", "FlightBeam", "Electar",
            "JumboTronExplore", "ArenaIntense", "ClearShot", "FoodCourt", "CrimsonCauldronGoblinWorkSong", "RangeFinder",
            "Spillway", "GoldenTrophyExplore", "GoldenTrophyCombat2", "JumboTronCombat1", "BeeDMHoldtheBees", "SR_GameOn01",
            "SR_FinalLap01_NoSilence", "SR_PostRun01", "SR_DadRunner", "CBM_Ballroom", "DodgeBaller", "LoneShoeBlues",
            "PirateQuestShanty2", "PirateShoppingChannel", "CBM_Gavotte", "OOBE_IntroSong", "RecRoomTheme2", "CBM_CombatBoss",
            "CBM_CombatMiniboss", "Arena_Cyberjunk_combat04b", "Arena_CyberJunk_Explore03b", "Bowling_MusicGameplay",
            "CrimsonCauldronExploringtheMines", "CrimsonCauldronTheBalladofVotaVader", "PirateQuestShanty_Piano",
            "Quest_Pirate1_PirateBrawl01", "PtblHomestead_Varmint"
        };

        public static readonly string[] RadioTitles =
        {
            "Sport Funk", "In The Bassment", "Secret Room", "Goat Attitude", "Organism", "River Predator", "Flight Beam", "Electar",
            "JumboTron Explore", "Hangar", "Clearcut", "Food Court", "Goblin Work Song", "Range Finder", "Spillway",
            "Golden Trophy Explore", "Golden Trophy Combat", "JumboTron Combat", "BeeDM (Radio Edit)", "Stunt Runners",
            "Stunt Runners (Final Lap)", "Victory Lap", "New Jack Runner", "Crescendo Ballroom", "Dodge Baller", "Lone Shoe Blues",
            "Shanty", "Pirate Shopping Channel", "Crescendo Gavotte", "Intro", "Rec Room Theme", "Crescendo Boss",
            "Crescendo Miniboss", "Cyberjunk Combat", "Cyberjunk Explore", "Bowling", "Crimson Cauldron Mines",
            "The Ballad of VotaVader", "Shanty Piano Version", "Pirate Brawl", "Homestead Varmint"
        };

        /// <summary>Index of "Rec Room Theme" (the kiosk radio's first track).</summary>
        public const int RadioTheme = 30;

        /// <summary>Index of "Food Court" (the cafe radio's first track, §3.4 G).</summary>
        public const int RadioFoodCourt = 11;

        /// <summary>Index of "Lone Shoe Blues" (the upstairs lounge radio's first track; the jukebox played it before iteration 3).</summary>
        public const int RadioLoneShoe = 25;
    }
}
