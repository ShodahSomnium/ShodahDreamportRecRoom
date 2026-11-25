using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.CrimsonCauldron;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>The Curse of the Crimson Cauldron's registry hook (ARCHITECTURE.md §11).</summary>
    public static partial class RrRegistry
    {
        static partial void RegisterCrimsonCauldron()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.CrimsonCauldron,
                Title = "The Curse of the Crimson Cauldron",
                Subtitle = "Quest - 1-4 players",
                Description = "A witch has cursed the land with her crimson cauldron! Arm up in the Armory, then fight goblins through the castle, the forests, the bog and the mines up to the bell tower.",
                Category = "Quests",
                MinPlayers = 1,
                MaxPlayers = 4,
                // rr_watch_ui.md / RrWatchArt: the witch camera-filter polaroid.
                CoverSprite = "Consumable_Polaroid_Witch_Tex(2048)",
                IconSprite = "Consumable_Polaroid_Witch_Tex(2048)",
                Music = RrCrimsonCauldronRoom.LobbySong,
                Networked = true,
                Create = root => root.AddComponent<RrCrimsonCauldronRoom>()
            });
        }
    }
}
