using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.LaserTag
{
    /// <summary>
    /// Laser Tag's two maps, both live at Rec Room's shutdown (rr_latest_rgames_action.md 2.1; one ^LaserTag room since
    /// the 2018 Costume Party update, "you can choose a specific map"): Hangar (Android level76) and Cyber Junk City
    /// (level77). Each level carries its own lobby next to the arena (the ScoreboardProjector, the storefront, the
    /// standee, the water-bottle table with its bucket), so the lobby moves with the chosen map.
    ///
    /// The levels ship as data files (Data/LaserTag_Hangar.bytes, Data/LaserTag_CyberjunkCity.bytes: gtscene jt=1 hull
    /// plates, rrsk2bytes --quant 2), read with <see cref="RrSceneBank.Text"/>. Music from the install's Laser Tag
    /// bundle (rr_shooters.md 4.2): ArenaIntense, Arena_CyberJunk_Explore03b, Arena_Cyberjunk_combat04b.
    /// </summary>
    public static class RrLaserTagMaps
    {
        public const int Hangar = 0;
        public const int CyberJunk = 1;
        public const int Count = 2;

        public static readonly string[] Names = { "Hangar", "Cyber Junk City" };

        /// <summary>RRSK scene names = data file names = RrEnvironment table keys (LaserTag_Hangar, LaserTag_CyberjunkCity).</summary>
        public static readonly string[] Scenes = { "LaserTag_Hangar", "LaserTag_CyberjunkCity" };

        /// <summary>
        /// Scene point p lands at room-local p - Anchor. The maps get their own slots (Cyber Junk City 300 m along -x of
        /// the scene, room-local x 217..345; Hangar x -40..61), so a map switch can build the new level while the lobby
        /// still stands in the old one.
        /// </summary>
        public static readonly Vector3[] Anchors = { Vector3.zero, new Vector3(-300f, 0f, 0f) };

        /// <summary>Match music: ArenaIntense on Hangar, the Cyber Junk combat loop on Cyber Junk City.</summary>
        public static readonly string[] PlayMusic = { "ArenaIntense", "Arena_Cyberjunk_combat04b" };

        /// <summary>
        /// Lobby music. Hangar's lobby has the level's own FoodCourt loop (an audio record over its storefront that the kit
        /// plays), so no music is laid over it; Cyber Junk City's lobby has none of its own and gets the Explore loop.
        /// </summary>
        public static readonly string[] LobbyMusic = { null, "Arena_CyberJunk_Explore03b" };

        /// <summary>The power weapon of each map (rr_shooters.md 1.2): the Laser Orb Launcher on Hangar, the Beam Cannon on Cyber Junk City.</summary>
        public static readonly string[] PowerWeapons = { RrLaserTagRoom.OrbLauncher, RrLaserTagRoom.BeamGun };

        /// <summary>
        /// Where the power weapon lies (scene space). Neither level has a marker for it (it came from a spawner the
        /// Android levels do not carry), so both are GUESSES: on Hangar the 1.23 m pad in the middle of the centre pit
        /// (collider at 0.49, -0.19, 0.04), on Cyber Junk City a metre off the street-level grenade between the two bases.
        /// </summary>
        public static readonly Vector3[] PowerSpots = { new Vector3(0.49f, -0.08f, 0.04f), new Vector3(-10.46f, 2.07f, -57.82f) };

        public static int Clamp(int map)
        {
            return map < 0 || map >= Count ? Hangar : map;
        }
    }
}
