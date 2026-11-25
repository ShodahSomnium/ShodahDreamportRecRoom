using SomniumSpace.Worlds.SomniumRecRoom.Games.Soccer;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    public static partial class RrRegistry
    {
        /// <summary>
        /// Soccer (^Soccer, "Shield Soccer" in 2016; unchanged since 2019): two teams kick a giant ball (1.43 m, it
        /// cannot be picked up) into the other team's goal; 3-minute match, most goals wins, a draw is possible
        /// (rr_sports.md §4, rr_latest_rgames_action.md §7). 2-6 players in Rec Room; START works alone for practice.
        /// Level72 (Android build) is the layout. Art: the Soccer gift-box print; music: SportFunk (GUESS, §8.3).
        /// </summary>
        static partial void RegisterSoccer()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.Soccer,
                Title = "Soccer",
                Subtitle = "Sports - 1-6 players",
                Description = "Kick the giant ball into the other team's goal. Run into it, teleport into it or slap it with your hands - it cannot be picked up. Most goals after 3 minutes wins.",
                Category = "Sports",
                MinPlayers = 1,
                MaxPlayers = 6,
                CoverSprite = RrSoccerRoom.Art,
                IconSprite = RrSoccerRoom.Art,
                Music = RrSoccerRoom.Music,
                Networked = true,
                Create = root => root.AddComponent<RrSoccerRoom>(),
            });
        }
    }
}
