using SomniumSpace.Worlds.SomniumRecRoom.Games.Bowling;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    public static partial class RrRegistry
    {
        /// <summary>
        /// Bowling, "Lone Shoe Lanes" (^Bowling, 2018; rules unchanged since): one scoring lane for up to 6 bowlers with
        /// automatic ten-pin scoring, two practice lanes (rr_sports.md §6, rr_latest_rgames_action.md §9). Level91 of the
        /// Android build is the layout. Art: the bowling pin tool icon (the watch's own pick); music: Bowling_MusicGameplay.
        /// </summary>
        static partial void RegisterBowling()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.Bowling,
                Title = "Bowling",
                Subtitle = "Sports - 1-6 players",
                Description = "Lone Shoe Lanes: ten frames on the middle lane, automatic scoring, strikes and spares count the way they should. Grab a ball from the shelf, roll it and knock down the pins. Two practice lanes on the sides.",
                Category = "Sports",
                MinPlayers = 1,
                MaxPlayers = 6,
                CoverSprite = RrBowlingRoom.Art,
                IconSprite = RrBowlingRoom.Art,
                Music = RrBowlingRoom.Music,
                Networked = true,
                Create = root => root.AddComponent<RrBowlingRoom>(),
            });
        }
    }
}
