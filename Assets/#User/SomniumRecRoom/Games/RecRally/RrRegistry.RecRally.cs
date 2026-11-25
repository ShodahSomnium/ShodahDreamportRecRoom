using SomniumSpace.Worlds.SomniumRecRoom.Games.RecRally;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    public static partial class RrRegistry
    {
        /// <summary>
        /// Rec Rally (^RecRally, 2021-09-29; rr_latest_rgames_story.md 10, rr_party.md 3): an off-road buggy race on
        /// Chaparral in Pioneer Peak - 3 laps, nitro cans, the top three on the podium; 4 players race since 2025-10-15
        /// (up to 6 on the grid; fewer can practise) (<see cref="RrRecRallyRoom"/>). Rec Room's own level107 is the course.
        /// Art: the Rec Rally gift-box print (as RrWatchArt); music RecRally_MainGameplayLoop while racing.
        /// </summary>
        static partial void RegisterRecRally()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.RecRally,
                Title = "Rec Rally",
                Subtitle = "Racing - 4-6 drivers",
                Description = "Boost, bump and powerslide your Rally Buggy round Chaparral in Pioneer Peak: past the red barn, over the bridge, along the railway " +
                              "and through the cave. Three cans of nitro make a boost. Three laps - first across the line wins!",
                Category = "Racing",
                MinPlayers = RrRecRallyRoom.MinRacers,
                MaxPlayers = RrRecRallyRoom.MaxRacers,
                CoverSprite = RrRecRallyRoom.Art,
                IconSprite = RrRecRallyRoom.Art,
                Music = null,
                Networked = true,
                Create = root => root.AddComponent<RrRecRallyRoom>(),
            });
        }
    }
}
