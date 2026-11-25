using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>Isle of Lost Skulls' registry hook (ARCHITECTURE.md §11).</summary>
    public static partial class RrRegistry
    {
        static partial void RegisterLostSkulls()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.LostSkulls,
                Title = "Isle of Lost Skulls",
                Subtitle = "Quest - 1-3 players",
                Description = "Locked in the brig of a pirate ship! Break out, fight skeleton pirates across the high seas, the port, the jungle and the caves, and sink Ghost Beard with the ship's cannons.",
                Category = "Quests",
                MinPlayers = 1,
                // rr_latest_rgames_story.md 8.1: "Up to three players" (the final cap).
                MaxPlayers = 3,
                // rr_watch_ui.md: the Ghost Beard camera-filter polaroid (the watch art uses the same sprite).
                CoverSprite = "Consumable_Polaroid_GhostBeard_Tex(2048)",
                IconSprite = "Consumable_Polaroid_GhostBeard_Tex(2048)",
                Music = RrLostSkullsRoom.ShopMusic,
                Networked = true,
                Create = root => root.AddComponent<RrLostSkullsRoom>()
            });
        }
    }
}
