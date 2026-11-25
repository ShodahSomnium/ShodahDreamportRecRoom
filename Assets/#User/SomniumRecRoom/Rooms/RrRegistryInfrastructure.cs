namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// The Rooms module's own registry hook (ARCHITECTURE.md §11). It runs after every place's hook, so it only
    /// fills gaps: a placeholder dorm and Rec Center (<see cref="RrPlaceholderRoom"/>) while the Places modules
    /// have not registered the real ones - travel, the watch and the doors then work from the first build.
    /// </summary>
    public static partial class RrRegistry
    {
        static partial void RegisterInfrastructure()
        {
            RegisterPlaceholder(RrActivity.Dorm);
            RegisterPlaceholder(RrActivity.RecCenter);
        }

        private static void RegisterPlaceholder(RrActivity activity)
        {
            if (RrRooms.Get(activity) != null)
            {
                return;
            }

            RrRooms.Register(RrPlaceholderRoom.InfoFor(activity));
            RrLog.Info("Rooms", "no " + activity + " registered: using the placeholder " + activity);
        }
    }
}
