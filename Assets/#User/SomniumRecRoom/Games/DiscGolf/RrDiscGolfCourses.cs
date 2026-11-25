using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.DiscGolf
{
    /// <summary>
    /// Disc Golf's two courses, both live at Rec Room's shutdown (rr_latest_rgames_action.md 8: "the courses did not change
    /// after 2017"): the Lake course (^DiscGolfLake, Android level9, discgolf.unity) and Propulsion ("Matter Propulsion Test
    /// Range, Rec Room Science Dept.", ^DiscGolfPropulsion, level10). Rec Room had them as two rooms; here they are one
    /// activity with the course chosen on the lobby's hologram (the LaserTag map pattern). Each level carries its own
    /// lobby (pregame spawns, the projector, the exit door), so the lobby moves with the course.
    ///
    /// The levels ship as data files (Data/DiscGolf.bytes, Data/DiscGolf_Propulsion.bytes: gtscene jt=1, the level's own
    /// discs taken out and the tree crowns thinned, DN/iter15/discgolf.md), read with <see cref="RrSceneBank.Text"/>; the
    /// names are also RrEnvironment's table keys (the levels' fog, sky, sun and lights).
    ///
    /// The holes are measured from the levels (DN/iter15/discgolf.md): each Systems/Holes/HoleN has its HoleNGoal (the
    /// DiscGolfGoal component: hole N-1 and par) and HoleNTees/[Tee_Marker] (the tee is the middle of its two arrow-sign
    /// posts; the ground under it is found at run time). Scene coordinates; room-local = scene - <see cref="Anchors"/>.
    /// </summary>
    public static class RrDiscGolfCourses
    {
        public const int Lake = 0;
        public const int Propulsion = 1;
        public const int Count = 2;
        public const int Holes = 9;

        public static readonly string[] Names = { "Lake", "Propulsion" };

        public static readonly string[] Scenes = { "DiscGolf", "DiscGolf_Propulsion" };

        /// <summary>
        /// Lake at the room's origin (its play area x -70..100, its 610 m backdrop grid to +-305); Propulsion 520 m along -x
        /// (room-local x -693..-438 with its backdrop), so a course switch builds the new level while the old one stands.
        /// </summary>
        public static readonly Vector3[] Anchors = { Vector3.zero, new Vector3(520f, 0f, 0f) };

        /// <summary>GUESS: neither level has music of its own (Propulsion's 20 audio sources have no install clip); Rec Room's RecRoomTheme2 is the research's pick for the Lake.</summary>
        public static readonly string[] Music = { "RecRoomTheme2", "RecRoomTheme2" };

        /// <summary>Tee centres (scene space; y is the ground estimate, the floor is searched around it).</summary>
        public static readonly Vector3[][] Tees =
        {
            new[]
            {
                new Vector3(54.35f, 1.06f, 80.39f), new Vector3(0.09f, 0.02f, 77.92f), new Vector3(-35.27f, 0.2f, 79.72f),
                new Vector3(-54.05f, 0.45f, -16.9f), new Vector3(-10.42f, 0.16f, -46.67f), new Vector3(-31.55f, 0.35f, -12.93f),
                new Vector3(37.66f, 0.68f, 33.54f), new Vector3(19.14f, 1.46f, -47.26f), new Vector3(39.68f, 6.02f, -67.73f),
            },
            new[]
            {
                new Vector3(19.14f, 0.62f, 18.96f), new Vector3(15.85f, 0.62f, 91.47f), new Vector3(27.47f, 16.43f, 151.5f),
                new Vector3(-69.58f, 0.61f, 138.61f), new Vector3(-53.3f, 0.61f, 63.28f), new Vector3(-82.29f, 0.61f, 10.23f),
                new Vector3(-125.01f, 0.61f, 27.18f), new Vector3(-98.52f, 8.59f, 131.34f), new Vector3(-24.48f, 0.63f, 45.83f),
            },
        };

        /// <summary>The baskets' base points (the DiscGolfGoal transforms: the FrisbeeGoal mesh's foot), scene space.</summary>
        public static readonly Vector3[][] Goals =
        {
            new[]
            {
                new Vector3(12.08f, -0.13f, 68.85f), new Vector3(-31.16f, 0.107f, 93.11f), new Vector3(-44.38f, 0.29f, 18.57f),
                new Vector3(-22.24f, 0.29f, -62.42f), new Vector3(-27.017f, 0.37f, -35.378f), new Vector3(17.85f, -0.02f, 35.43f),
                new Vector3(30.939f, 0.07f, -33.654f), new Vector3(37.35f, 6.01f, -69.62f), new Vector3(69.97f, 2.548f, 1.34f),
            },
            new[]
            {
                new Vector3(18f, 0.62f, 58.52f), new Vector3(15.24f, 0.62f, 154.5f), new Vector3(-70.06f, 0.62f, 157.2f),
                new Vector3(-69.67f, 4.48f, 86.72f), new Vector3(-71.66f, 0.587f, 15.06f), new Vector3(-121.46f, 0.601f, 8.2f),
                new Vector3(-124.73f, 1.5f, 120f), new Vector3(-42.142f, 0.54f, 79.428f), new Vector3(-4.56f, 0.64f, 22.2f),
            },
        };

        /// <summary>Par per hole (the DiscGolfGoal components' par): Lake 28, Propulsion 26.</summary>
        public static readonly int[][] Pars =
        {
            new[] { 3, 2, 3, 4, 2, 4, 3, 2, 5 },
            new[] { 3, 3, 2, 4, 2, 3, 3, 4, 2 },
        };

        /// <summary>The level's exit door (ActivityDoorSingle_Frame), scene space, and its yaw: our Rec Center portal stands there.</summary>
        public static readonly Vector3[] Doors = { new Vector3(73.028f, 1.0927f, 68.946f), new Vector3(20.443f, 0.617f, -11.077f) };

        public static readonly float[] DoorYaws = { -51.5f, -90f };

        /// <summary>Where the four discs wait in the lobby (scene space): the level's own practice discs by hole 1's tee / on the lobby lawn.</summary>
        public static readonly Vector3[][] LobbyDiscs =
        {
            new[] { new Vector3(53.68f, 1.15f, 80.76f), new Vector3(53.84f, 1.17f, 79.93f), new Vector3(54.06f, 1.18f, 79.03f), new Vector3(53.62f, 1.14f, 81.61f) },
            new[] { new Vector3(6.66f, 0.72f, -9.96f), new Vector3(6.62f, 0.72f, -7.11f), new Vector3(6.51f, 0.72f, -3.32f), new Vector3(15.76f, 0.72f, 15.89f) },
        };

        /// <summary>
        /// The course area (scene space, x0, z0, x1, z1): a disc that rests outside, or falls 15 m below the lowest tee or
        /// basket, is out (a hazard). Lake: inside its out-of-bounds boxes; Propulsion: its terrain.
        /// </summary>
        public static readonly Vector4[] Areas = { new Vector4(-90f, -120f, 120f, 140f), new Vector4(-170f, -40f, 50f, 175f) };

        public static int Clamp(int course)
        {
            return course < 0 || course >= Count ? Lake : course;
        }

        public static int Par(int course, int hole)
        {
            return hole >= 0 && hole < Holes ? Pars[Clamp(course)][hole] : 3;
        }

        public static int TotalPar(int course)
        {
            int sum = 0;
            foreach (int p in Pars[Clamp(course)])
            {
                sum += p;
            }

            return sum;
        }
    }
}
