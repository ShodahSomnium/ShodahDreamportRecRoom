using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Player-made rooms' registry hook (ARCHITECTURE §11): RrActivity.MakerRoom, networked (one instance per room, picked
    /// by <see cref="RrMakerRooms.ChooseInstanceNumber"/>; which room is in the instance's custom ints). Its title follows the
    /// room being entered (<see cref="RrMakerRooms.SetTravelTitle"/>), so the loading card and This Room show the room's
    /// name. Category "Social": no arrival sting and the short loading card, as for the Dorm and the Rec Center. Unlisted: it
    /// is not an activity card on the Play / Home pages, the Dorm board or the Magic Door; its rooms are listed by the watch's
    /// Rooms page and the Rec Center's MAKERWORLD / CAMPUS JAMS doors (whose "Maker Rooms" card opens the Rooms page,
    /// RrWatchUi.ShowRoom). Also starts the maker rooms' manager.
    /// </summary>
    public static partial class RrRegistry
    {
        static partial void RegisterMakerRoom()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.MakerRoom,
                Title = RrMakerRooms.DefaultTitle,
                Subtitle = "Rooms made by players",
                Description = "Rooms made by the players here with the Maker Pen: create your own on a base room (Create), or join one (Rooms).",
                MinPlayers = 1,
                MaxPlayers = 20,
                CoverSprite = "Illustration_CreateSubRoom",
                IconSprite = "Illustration_CreateSubRoom",
                Music = null,
                Category = "Social",
                Networked = true,
                Unlisted = true,
                Create = root => root.AddComponent<RrMakerRoom>()
            });

            RrMakerRooms.Init();
        }
    }
}
