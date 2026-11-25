using SomniumSpace.Worlds.SomniumRecRoom.Games.LaserTag;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Laser Tag's registry hook (ARCHITECTURE.md §11). Rec Room's Laser Tag (2017-11-18; one room with a map choice since
    /// 2018) is a 4 v 4 team battle with the Arena laser guns on Hangar and Cyber Junk City: first team to 400 points or the
    /// most after 480 s, 10 per tag (<see cref="RrLaserTagRoom"/>). The card art is the Laser Tag T-shirt print (the same
    /// crop as RrWatchArt); the music is the room's (RrLaserTagMaps: ArenaIntense, the Cyber Junk loops).
    /// </summary>
    public static partial class RrRegistry
    {
        static partial void RegisterLaserTag()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.LaserTag,
                Title = "Laser Tag",
                Subtitle = "Team Battle - Hangar and Cyber Junk City",
                Description = "Rec Room's laser team battle, on both of its maps: Hangar and Cyber Junk City. Spawn with a laser pistol, grab rifles, " +
                              "shotguns, railguns, grenades and the power weapon in the arena. First team to 400 points wins!",
                Category = "PvP",
                MinPlayers = RrLaserTagRoom.MinPlayers,
                MaxPlayers = 8,
                CoverSprite = "MaleTShirt_Shirt_LaserTag_Alpha_DECAL|80,414,860,530",
                IconSprite = "MaleTShirt_Shirt_LaserTag_Alpha_DECAL|80,414,860,530",
                // No arrival music: Hangar's lobby plays the level's own FoodCourt loop, Cyber Junk City's gets its Explore loop from the room.
                Music = null,
                Networked = true,
                Create = root => root.AddComponent<RrLaserTagRoom>()
            });
        }
    }
}
