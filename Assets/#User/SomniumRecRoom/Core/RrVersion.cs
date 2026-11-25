namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Which build of the world this is. The Dreamport is worked on in short iterations, each uploaded and tested in
    /// the client (the README's "Iterations" list says what each one changed). The stamp is logged at boot, shown on
    /// the status label while the world starts, and at the foot of the watch's Settings page, so a tester (or the
    /// team watching) can tell at a glance which iteration they are in.
    /// </summary>
    public static class RrVersion
    {
        public const int Iteration = 17;
        public const string Date = "2026-11-10";
        public const string Summary = "Same rooms for everyone: the final Rec Center from shipped data when a player has no Rec Room cache, Dodgeball and Paddleball from Rec Room's levels, Rec Room found in any Steam folder";

        public static string Label
        {
            get { return "Iteration " + Iteration + " (" + Date + ")"; }
        }
    }
}
