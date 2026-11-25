using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>The Rise of Jumbotron's registry hook (ARCHITECTURE.md §11).</summary>
    public static partial class RrRegistry
    {
        static partial void RegisterJumbotron()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.Jumbotron,
                Title = "The Rise of Jumbotron",
                Subtitle = "Quest - 1-4 players",
                Description = "Jumbotron and his robot army have taken over the SS Coach! Grab a laser pistol in the Armory and fight across the ship and the Jumbodome to shut him down.",
                Category = "Quests",
                MinPlayers = 1,
                MaxPlayers = 4,
                // rr_watch_ui.md row 22: the Jumbotron camera-filter polaroid (bundle 85515de1, subject top-right).
                CoverSprite = "Consumable_Polaroid_Jumbotron_Tex(2048)",
                IconSprite = "Consumable_Polaroid_Jumbotron_Tex(2048)",
                Music = RrJumbotronRoom.ExploreMusic,
                Networked = true,
                Create = root => root.AddComponent<RrJumbotronRoom>()
            });
        }
    }
}
