using SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// The Dorm Room's registry entry (ARCHITECTURE §11). The dorm is the player's private home: unnetworked (every
    /// player builds their own copy at their presence slot), one player, category "Social". Artwork from
    /// rr_watch_ui.md's table: cover <c>illustration_HomeButton_DormRoom</c> (key 597), icon
    /// <c>Illustration_Menu_Dorm</c> (the isometric dorm of the watch's quick-access row).
    /// Music is deliberately empty: Rec Room's dorm has no background music. The room itself plays the radio on the
    /// desk (Rec Room tracks) and the outdoor birds through the windows (3D loops, see <see cref="RrDormRoom"/>).
    /// </summary>
    public static partial class RrRegistry
    {
        static partial void RegisterDorm()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.Dorm,
                Title = "Dorm Room",
                Subtitle = "Your own room",
                Description = "Your room on the Rec Room campus: shoot some hoops, grab a root beer, check yourself out in the mirror, climb up to the loft and head through the door to the Rec Center.",
                MinPlayers = 1,
                MaxPlayers = 1,
                CoverSprite = "illustration_HomeButton_DormRoom",
                IconSprite = "Illustration_Menu_Dorm",
                Music = null,
                Category = "Social",
                Networked = false,
                Create = root => root.AddComponent<RrDormRoom>()
            });
        }
    }
}
