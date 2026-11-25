namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// 3D Charades' registry hook (ARCHITECTURE.md §11). Rec Room's own numbers: 2-8 players (rr_party.md §1.1), the
    /// Ink Space music (MUS_BGM_3DCharades2b_Music, bundle 7b3ac31f key "a", 146 s), and the gift-box print as the
    /// cover (rr_watch_ui.md §3.3: GiftBox_Charades_Tex, crop 8,276,380,268; Rec Room's real covers came from rec.net).
    /// </summary>
    public static partial class RrRegistry
    {
        static partial void RegisterCharades()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.Charades,
                Title = "3D Charades",
                Subtitle = "Draw it. Guess it.",
                Description = "One player draws a secret word in 3D with the Charades Pen while everyone else shouts " +
                              "out guesses. Co-op Classic: solve as many words as you can in 8 minutes. Team " +
                              "Lightning: Blue and Red race for the most words.",
                Category = "Party",
                MinPlayers = 2,
                MaxPlayers = 8,
                CoverSprite = "GiftBox_Charades_Tex|8,276,380,268",
                IconSprite = "GiftBox_Charades_Tex|8,276,380,268",
                Music = Games.Charades.RrCharadesRoom.InkSpaceMusic,
                Networked = true,
                Create = root => root.AddComponent<Games.Charades.RrCharadesRoom>()
            });
        }
    }
}
