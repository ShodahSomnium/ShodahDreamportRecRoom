namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Registers every place with <see cref="RrRooms"/>. Each place or game implements its own hook below in its
    /// own folder, as a partial method of this class:
    ///
    ///     public static partial class RrRegistry
    ///     {
    ///         static partial void RegisterPaintball() { RrRooms.Register(new RrRoomInfo { ... }); }
    ///     }
    ///
    /// so modules are added without anyone editing this file; a hook nobody implements compiles away. The order
    /// of the calls is the watch menu's order within a category.
    /// </summary>
    public static partial class RrRegistry
    {
        public static void RegisterAll()
        {
            RrRooms.Clear();

            RegisterDorm();
            RegisterRecCenter();

            RegisterPaintball();
            RegisterLaserTag();
            RegisterRecRoyale();
            RegisterShowdown();
            RegisterPunchUp();

            RegisterGoldenTrophy();
            RegisterCrimsonCauldron();
            RegisterLostSkulls();
            RegisterJumbotron();
            RegisterCrescendo();

            RegisterDodgeball();
            RegisterPaddleball();
            RegisterSoccer();
            RegisterDiscGolf();
            RegisterBowling();

            RegisterCharades();
            RegisterStuntRunner();
            RegisterRecRally();
            RegisterMidnight();
            RegisterLittleMonsters();

            RegisterMakerRoom();

            RegisterInfrastructure();
        }

        static partial void RegisterDorm();

        static partial void RegisterRecCenter();

        static partial void RegisterPaintball();

        static partial void RegisterLaserTag();

        static partial void RegisterRecRoyale();

        static partial void RegisterShowdown();

        static partial void RegisterPunchUp();

        static partial void RegisterGoldenTrophy();

        static partial void RegisterCrimsonCauldron();

        static partial void RegisterLostSkulls();

        static partial void RegisterJumbotron();

        static partial void RegisterCrescendo();

        static partial void RegisterDodgeball();

        static partial void RegisterPaddleball();

        static partial void RegisterSoccer();

        static partial void RegisterDiscGolf();

        static partial void RegisterBowling();

        static partial void RegisterCharades();

        static partial void RegisterStuntRunner();

        static partial void RegisterRecRally();

        static partial void RegisterMidnight();

        static partial void RegisterLittleMonsters();

        /// <summary>Player-made rooms (Places/MakerRooms): RrActivity.MakerRoom, and the maker rooms' manager.</summary>
        static partial void RegisterMakerRoom();

        /// <summary>Rooms-module infrastructure (e.g. a placeholder dorm while none is registered); implemented in Rooms/.</summary>
        static partial void RegisterInfrastructure();
    }
}
