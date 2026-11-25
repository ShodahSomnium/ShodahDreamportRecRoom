using SomniumSpace.Worlds.SomniumRecRoom.Games.DiscGolf;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    public static partial class RrRegistry
    {
        /// <summary>
        /// Disc Golf (^DiscGolfLake, ^DiscGolfPropulsion; courses unchanged since 2017): 1-4 players throw their disc from
        /// each tee into the basket in as few throws as they can, 9 holes, lowest total wins (rr_sports.md 5,
        /// rr_latest_rgames_action.md 8). Both courses from the Android build (level9 Lake, level10 Propulsion), chosen
        /// on the lobby hologram. Art: the Disc Golf gift-box print (the watch's own pick); music: RecRoomTheme2 (GUESS).
        /// </summary>
        static partial void RegisterDiscGolf()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.DiscGolf,
                Title = "Disc Golf",
                Subtitle = "Sports - 1-4 players",
                Description = "Nine holes on the Lake course or the Propulsion test range. Throw your disc from the tee and get it into the basket in as few throws as you can - watch out for the water, the hazard panels and the fans. Lowest score wins.",
                Category = "Sports",
                MinPlayers = 1,
                MaxPlayers = 4,
                CoverSprite = RrDiscGolfRoom.Art,
                IconSprite = RrDiscGolfRoom.Art,
                Music = RrDiscGolfRoom.Music,
                Networked = true,
                Create = root => root.AddComponent<RrDiscGolfRoom>(),
            });
        }
    }
}
