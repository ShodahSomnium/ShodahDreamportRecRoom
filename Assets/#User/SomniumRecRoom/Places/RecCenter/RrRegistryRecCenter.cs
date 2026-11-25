namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// The Rec Center's registration hook (ARCHITECTURE §11). Registering the real hub replaces the Rooms module's
    /// placeholder hall automatically (RegisterInfrastructure only fills the slot when it is empty).
    /// </summary>
    public static partial class RrRegistry
    {
        static partial void RegisterRecCenter()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.RecCenter,
                Title = "Rec Center",
                Subtitle = "The hub of Rec Room",
                Description = "Hang out between games: shoot hoops in the gym, play ping-pong, grab a coffee at the cafe, "
                    + "and pick a room behind the SPORTS & PVP, QUESTS, CREATIVE CAMPUS and FEATURED ROOMS doors - or let the Magic Door choose.",
                Category = "Social",
                MinPlayers = 1,
                MaxPlayers = 40,
                // rr_watch_ui.md artwork table: the home-button crop for the card, the building sticker as the icon.
                CoverSprite = "illustration_HomeButton_RecCenter",
                IconSprite = "Illustration_Menu_RecCenter",
                // No global music: Rec Room's hub relied on its radios (rr_reccenter.md §2.6), which play
                // positionally in the room; a 2D track would drown them.
                Music = null,
                Networked = true,
                Create = root => root.AddComponent<Places.RecCenter.RrRecCenterRoom>()
            });
        }
    }
}
