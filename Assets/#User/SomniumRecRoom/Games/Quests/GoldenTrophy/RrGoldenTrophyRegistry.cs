using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>Quest for the Golden Trophy's registry hook (ARCHITECTURE.md §11).</summary>
    public static partial class RrRegistry
    {
        static partial void RegisterGoldenTrophy()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.GoldenTrophy,
                Title = "Quest for the Golden Trophy",
                Subtitle = "Quest - 1-4 players",
                Description = "The Goblin King has stolen Coach's Golden Trophy! Grab a sword, shield or bow and fight through nine acts of the goblin kingdom to win it back.",
                Category = "Quests",
                MinPlayers = 1,
                MaxPlayers = 4,
                // rr_watch_ui.md per-activity artwork: the Goblin King camera-filter polaroid (bundle 85515de1).
                CoverSprite = "Consumable_Polaroid_Goblin1_Tex(2048)",
                IconSprite = "Consumable_Polaroid_Goblin1_Tex(2048)",
                Music = "GoldenTrophyExplore",
                Networked = true,
                Create = root => root.AddComponent<RrGoldenTrophyRoom>()
            });
        }
    }
}
