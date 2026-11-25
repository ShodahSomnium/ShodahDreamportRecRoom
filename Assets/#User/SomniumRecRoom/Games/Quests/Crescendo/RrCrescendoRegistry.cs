using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Crescendo;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>Crescendo of the Blood Moon's registry hook (ARCHITECTURE.md §11).</summary>
    public static partial class RrRegistry
    {
        static partial void RegisterCrescendo()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.Crescendo,
                Title = "Crescendo of the Blood Moon",
                Subtitle = "Quest - 1-3 players",
                Description = "The blood moon rises over Dracula's castle. Fight through the burning village, ride the carriage to the castle and hunt the Count down with his own mirrors.",
                Category = "Quests",
                MinPlayers = 1,
                MaxPlayers = 3,
                // rr_watch_ui.md row 5: the Dracula camera-filter polaroid (bundle 85515de1, cut-out render, right half).
                CoverSprite = "Consumable_Polaroid_Dracula_Tex(2048)",
                IconSprite = "Consumable_Polaroid_Dracula_Tex(2048)",
                Music = RrCrescendoRoom.ExploreMusic,
                Networked = true,
                Create = root => root.AddComponent<RrCrescendoRoom>()
            });
        }
    }
}
