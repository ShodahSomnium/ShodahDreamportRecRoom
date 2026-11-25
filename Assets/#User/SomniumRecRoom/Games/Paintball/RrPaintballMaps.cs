using System;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball.Scenes;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// The seven Paintball maps at Rec Room's shutdown (rr_latest_rgames_action.md 1.1: nothing was retired, all
    /// seven were live) and the 2025 lobby, each from its own level in the Android build (DevNotes/refdata/apk/INDEX.md:
    /// level14 River, 15 Homestead = paintball2_open, 16 Quarry = Paintball_Castle, 17 Clearcut, 18 Spillway =
    /// Paintball_Dam, 103 Drive-In, 104 Merry Midway, 113 the lobby). Their placement text is generated
    /// (Scenes/RrScene_Paintball_*.cs, DevNotes/tools/pbscene/pbprep.sh).
    ///
    /// Music: each map's gameplay track where the bundles have one (RiverPredator, PtblHomestead_Varmint,
    /// Spillway, PtblSunset; rr_shooters.md 7); Quarry, Clearcut and Merry Midway have none of their own, so they
    /// play PaintballManager's gameplayMusic, RiverPredator (GUESS). Lobby radio: the map's PaintballLobby_*Radio
    /// station (none exists for Merry Midway: River's).
    ///
    /// Placement (ARCHITECTURE.md 6, a room within +-900 m of its origin): the lobby at the room origin; the one
    /// map in play at <see cref="MapSlot"/>, 560 m south, built when it is chosen and torn down when another is.
    /// </summary>
    public static class RrPaintballMaps
    {
        public const int River = 0, Homestead = 1, Quarry = 2, Clearcut = 3, Spillway = 4, DriveIn = 5, MerryMidway = 6;
        public const int Count = 7;

        /// <summary>Room-local position of the built map's anchor (its flag-base midpoint).</summary>
        public static readonly Vector3 MapSlot = new Vector3(0f, 0f, -560f);

        /// <summary>Cells of the maps' far terrain beyond this radius (m) from the anchor are left out.</summary>
        public const float ClipRadius = 330f;

        public static readonly string[] Names = { "River", "Homestead", "Quarry", "Clearcut", "Spillway", "Sunset Drive-In", "Merry Midway" };
        public static readonly string[] Short = { "RIVER", "HOMESTEAD", "QUARRY", "CLEARCUT", "SPILLWAY", "DRIVE-IN", "MIDWAY" };
        public static readonly string[] Keys = { "River", "Homestead", "Quarry", "Clearcut", "Spillway", "DriveIn", "MerryMidway" };
        public static readonly string[] Music = { "RiverPredator", "PtblHomestead_Varmint", "RiverPredator", "RiverPredator", "Spillway", "PtblSunset", "RiverPredator" };

        public static readonly string[] Radios =
        {
            "PaintballLobby_RiverRadio", "PaintballLobby_HomesteadRadio", "PaintballLobby_QuarryRadio", "PaintballLobby_ClearcutRadio",
            "PaintballLobby_SpillwayRadio", "PaintballLobby_SunsetRadio", "PaintballLobby_RiverRadio"
        };

        /// <summary>Map ambience loops from the bundles (null: the level's own sounds only).</summary>
        public static readonly string[] Ambience = { null, null, null, "Paintball_Ambiance_ClearCut01", "Paintball_Dam_Crickets_AmbianceFill01", null, null };

        /// <summary>The generated placement text of map i (RRSZ1).</summary>
        public static string Text(int map)
        {
            switch (map)
            {
                case River: return RrScene_Paintball_River.Text();
                case Homestead: return RrScene_Paintball_Homestead.Text();
                case Quarry: return RrScene_Paintball_Quarry.Text();
                case Clearcut: return RrScene_Paintball_Clearcut.Text();
                case Spillway: return RrScene_Paintball_Spillway.Text();
                case DriveIn: return RrScene_Paintball_DriveIn.Text();
                case MerryMidway: return RrScene_Paintball_MerryMidway.Text();
                default: return null;
            }
        }

        public static string LobbyText()
        {
            return RrScene_Paintball_Lobby.Text();
        }

        /// <summary>
        /// The scene point each map is anchored on: the midpoint of its two flag bases (x, z) at the flag bases'
        /// height, measured from the levels (DevNotes/refdata/apk/Paintball_*.rrsk "G flag_base").
        /// </summary>
        public static readonly Vector3[] Anchors =
        {
            new Vector3(-0.0415f, 0.7738f, 0.0785f),      // River: (-0.04, 0.77, -49.73) / (-0.04, 0.77, 49.89)
            new Vector3(-19.491f, -5.242f, 41.0219f),     // Homestead: (-27.91, -5.17, 88.88) / (-11.07, -5.31, -6.84)
            new Vector3(1.5567f, 1.9018f, 0.409f),        // Quarry: (-1.55, 1.88, 49.31) / (4.66, 1.92, -48.49)
            new Vector3(4.0325f, -4.6102f, -1.926f),      // Clearcut: (16.10, -4.63, -35.20) / (-8.03, -4.59, 31.35)
            new Vector3(4.4799f, 13.3234f, -29.61f),      // Spillway: (7.36, 13.56, 14.77) / (1.60, 13.09, -73.99)
            new Vector3(16.946f, 18.1343f, -20.7898f),    // Drive-In: (13.99, 18.26, 28.13) / (19.91, 18.01, -69.71)
            new Vector3(55.635f, 0.9689f, 56.6979f)       // Merry Midway: (97.82, 0.99, 24.91) / (13.45, 0.94, 88.48)
        };

        /// <summary>The lobby's anchor: the centre of its eight pregame spawn points (Player1..8SpawnPoint).</summary>
        public static readonly Vector3 LobbyAnchor = new Vector3(-61.9383f, -5.2463f, 75.9875f);

        public static RrPaintballMap Create(int i)
        {
            i = Mathf.Clamp(i, 0, Count - 1);
            return new RrPaintballMap
            {
                Index = i, Name = Names[i], Key = Keys[i], Music = Music[i], Radio = Radios[i], Slot = MapSlot, Anchor = Anchors[i],
                Center = MapSlot
            };
        }

        public static RrPaintballMap CreateLobby()
        {
            return new RrPaintballMap { Index = -1, Name = "Lobby", Key = "Lobby", Slot = Vector3.zero, Anchor = LobbyAnchor, Center = Vector3.zero, HalfSize = new Vector2(40f, 40f) };
        }
    }
}
