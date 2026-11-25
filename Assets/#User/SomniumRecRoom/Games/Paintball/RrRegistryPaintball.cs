using SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Paintball's registry hook (ARCHITECTURE.md §11). Rec Room's Paintball (2016-07-20) is a 2-8 player, 4 v 4 team
    /// shooter: Capture the Flag (3 captures or 600 s) and Team Battle (50 hits or 600 s), one hit and you are out.
    /// The card art is the paintball gift-box print (rr_watch_ui.md, activity 13: the official covers came from
    /// rec.net and are not in the bundles); the arrival music is the lobby radio of the first map, River.
    /// </summary>
    public static partial class RrRegistry
    {
        static partial void RegisterPaintball()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.Paintball,
                Title = "Paintball",
                Subtitle = "Capture the Flag and Team Battle",
                Description = "Rec Room's original team shooter, on all seven of its maps: River, Homestead, Quarry, Clearcut, " +
                              "Spillway, Sunset Drive-In and Merry Midway. Pick a side, grab a paint gun, press START. One hit and you're out!",
                Category = "PvP",
                MinPlayers = 2,
                MaxPlayers = 8,
                CoverSprite = "GiftBox_Paintball_Tex|8,276,380,268",
                IconSprite = "GiftBox_Paintball_Tex|8,276,380,268",
                Music = RrPaintballMaps.Radios[0],
                Networked = true,
                Create = root => root.AddComponent<RrPaintballRoom>()
            });
        }
    }
}
