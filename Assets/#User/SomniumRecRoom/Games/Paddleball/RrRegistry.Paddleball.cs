using SomniumSpace.Worlds.SomniumRecRoom.Games.Paddleball;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    public static partial class RrRegistry
    {
        /// <summary>
        /// Paddleball, a Rec Room launch game (2016): "3D Pong" with a zero-gravity ball in a glass tube, 1v1 (we
        /// also allow 2v2), first to 5 points (rr_sports.md §3). Art: the Paddleball gift-box print
        /// (rr_watch_ui.md §3.3, id 12); music: SportFunk (GUESS, §8.3).
        /// </summary>
        static partial void RegisterPaddleball()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.Paddleball,
                Title = "Paddleball",
                Subtitle = "Sports - 2-4 players",
                Description = "3D Pong in a glass tube. The ball floats with no gravity: serve it, then keep it from reaching the back wall behind you and hit it past your opponent. First to 5 points wins.",
                Category = "Sports",
                MinPlayers = 2,
                MaxPlayers = 4,
                CoverSprite = RrPaddleballRoom.Art,
                IconSprite = RrPaddleballRoom.Art,
                Music = RrPaddleballRoom.Music,
                Create = root => root.AddComponent<RrPaddleballRoom>(),
            });
        }
    }
}
