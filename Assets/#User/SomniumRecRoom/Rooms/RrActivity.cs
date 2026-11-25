namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Every place a player can be. The numbers are wire ids (presence slot int 4, instance object int 2;
    /// somnium_networking.md §5.1) and must never be renumbered once a build has shipped.
    /// </summary>
    public enum RrActivity : byte
    {
        None = 0,
        Dorm = 1,
        RecCenter = 2,
        Charades = 3,
        Bowling = 4,
        Crescendo = 5,
        DiscGolf = 6,
        Dodgeball = 7,
        LostSkulls = 8,
        LaserTag = 9,
        Midnight = 10,
        LittleMonsters = 11,
        Paddleball = 12,
        Paintball = 13,
        PunchUp = 14,
        GoldenTrophy = 15,
        RecRally = 16,
        RecRoyale = 17,
        Showdown = 18,
        Soccer = 19,
        StuntRunner = 20,
        CrimsonCauldron = 21,
        Jumbotron = 22,

        /// <summary>A player-made room (Places/MakerRooms): which room is in the instance's ints, not the instance number.</summary>
        MakerRoom = 23
    }

    /// <summary>A player's state inside their current activity (presence slot int 4, bits 16-19).</summary>
    public enum RrStatus : byte
    {
        None = 0,
        Loading = 1,
        Lobby = 2,
        Playing = 3,
        Spectating = 4,
        Down = 5
    }
}
