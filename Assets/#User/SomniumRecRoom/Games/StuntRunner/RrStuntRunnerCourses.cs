using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.StuntRunner
{
    /// <summary>
    /// Stunt Runner's levels (rr_party.md 2, rr_latest_rgames_story.md 11: the 2019 game, unchanged): the lobby (Android
    /// level92) and the six courses (levels 93-98), each Rec Room's own level converted by apkscene into
    /// DN/refdata/apk/StuntRunner_*.rrsk and shipped as Data/StuntRunner_*.bytes (rrsk2bytes --quant 3; plain kit records,
    /// no jt=1 hull - 91-97 % of the course renderers are install meshes). The names are the RRSK scene names, the data
    /// names and the RrEnvironment table keys.
    ///
    /// Every level is stored in its own world frame and the frames overlap, so each gets its own slot: scene point p lands
    /// at room-local p - Anchor. The lobby keeps its frame; the courses are laid out in a row 450 m apart, 700 m north of
    /// it, their floors at their own heights.
    /// </summary>
    public static class RrStuntRunnerCourses
    {
        public const int Count = 6;

        public const string LobbyScene = "StuntRunner_Lobby";

        public static readonly string[] Scenes =
        {
            "StuntRunner_Course1", "StuntRunner_Course2", "StuntRunner_Course3", "StuntRunner_Course4", "StuntRunner_Course5", "StuntRunner_Course6"
        };

        /// <summary>The courses' names as the lobby's signs give them (level92 sign texts; course 2 from rr_party.md 2.1).</summary>
        public static readonly string[] Names = { "Anticipation", "Daring", "Patience", "Caution", "Trust", "Perseverance" };

        /// <summary>The centre (x, z) of each course's stored bounds (the RRSK B record).</summary>
        private static readonly Vector2[] Centres =
        {
            new Vector2(89.76f, 33.84f), new Vector2(201.82f, 13.05f), new Vector2(-9.07f, -22.78f),
            new Vector2(-115.76f, 40.94f), new Vector2(138.93f, -141.67f), new Vector2(-49.31f, -54.70f)
        };

        /// <summary>Scene point p of course i lands at room-local p - Anchor(i).</summary>
        public static Vector3 Anchor(int course)
        {
            var c = Centres[Mathf.Clamp(course, 0, Count - 1)];
            var slot = new Vector3((course - 2.5f) * 450f, 0f, 700f);
            return new Vector3(c.x - slot.x, 0f, c.y - slot.z);
        }

        public static string Title(int course)
        {
            course = Mathf.Clamp(course, 0, Count - 1);
            return (course == Count - 1 ? "Finale" : "Course " + (course + 1)) + " - " + Names[course];
        }

        /// <summary>The max course time ([StuntRunner_GameRuleWrapperCircuit] defaultMaxCourseTime 180).</summary>
        public const float MaxCourseSeconds = 180f;

        /// <summary>
        /// The rank from the total of the course times (rr_party.md 2.1): S+ under 4:45 is Rec Room's; the others are
        /// GUESSES (S &lt; 5:30, A &lt; 6:30, B &lt; 8:00, else C). For a shorter run (fewer courses built) the limits scale.
        /// </summary>
        public static string Rank(float totalSeconds, int courses)
        {
            float k = Mathf.Max(1, courses) / (float)Count;
            if (totalSeconds < 285f * k)
            {
                return "S+";
            }

            if (totalSeconds < 330f * k)
            {
                return "S";
            }

            if (totalSeconds < 390f * k)
            {
                return "A";
            }

            return totalSeconds < 480f * k ? "B" : "C";
        }

        public static string Clock(float seconds)
        {
            if (seconds < 0f)
            {
                return "--:--";
            }

            int cs = Mathf.RoundToInt(seconds * 100f);
            return string.Format("{0}:{1:00}.{2:00}", cs / 6000, (cs / 100) % 60, cs % 100);
        }
    }
}
