using SomniumSpace.Worlds.SomniumRecRoom.Games.Dodgeball;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    public static partial class RrRegistry
    {
        /// <summary>
        /// Dodgeball, one of Rec Room's launch games (2016), with its final (June 2026) rules: 2-6 players (1 v 1 to
        /// 3 v 3) in two auto-sorted teams on a gym court, three balls on the centre line, only the player hit is out,
        /// a catch is a dead ball (no catch-outs since 2025-01-14), first team to win 3 rounds (rr_sports.md §2,
        /// rr_latest_rgames_action.md §5). MinPlayers 2 is Rec Room's own minimum; alone, START is practice.
        /// Art: the Dodgeball gift-box print (rr_watch_ui.md §3.3, id 7); music: DodgeBaller (150 s).
        /// </summary>
        static partial void RegisterDodgeball()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.Dodgeball,
                Title = "Dodgeball",
                Subtitle = "Sports - 2-6 players",
                Description = "Two teams, three balls, one gym. Hit an opponent with a live throw to put them out; a caught throw is just dead. Stay on your side of the centre line. First to win 3 rounds takes the match. 1 v 1 and up, or START alone to practise.",
                Category = "Sports",
                MinPlayers = 2,
                MaxPlayers = 6,
                CoverSprite = RrDodgeballRoom.Art,
                IconSprite = RrDodgeballRoom.Art,
                Music = RrDodgeballRoom.Music,
                Create = root => root.AddComponent<RrDodgeballRoom>(),
            });
        }
    }
}
