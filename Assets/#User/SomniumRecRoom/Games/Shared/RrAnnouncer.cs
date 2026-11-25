namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Rec Room's announcer and game stingers, by the clip names in its bundles (all measured in the index):
    /// "Blue Team", "Red Team", "Blueteamscores", "Redteamscores", "Blueteamwins2", "Redteamwins2", "Tiegame",
    /// "Gameon", "Gameover", "Ready", "Start", "Over", "One minute remaining", "10 seconds remaining",
    /// "The Winner Is", "Team Battle", "Capture The Flag", "MatchOver01", the Stunt Runner "SR_*" set and the
    /// "10up".."1up" count. Played 2D so everyone in the room hears them the same.
    /// </summary>
    public static class RrAnnouncer
    {
        public const string BlueTeam = "Blue Team";
        public const string RedTeam = "Red Team";
        public const string BlueScores = "Blueteamscores";
        public const string RedScores = "Redteamscores";
        public const string BlueWins = "Blueteamwins2";
        public const string RedWins = "Redteamwins2";
        public const string Tie = "Tiegame";
        public const string GameOn = "Gameon";
        public const string GameOver = "Gameover";
        public const string Ready = "Ready";
        public const string Start = "Start";
        public const string Over = "Over";
        public const string OneMinute = "One minute remaining";
        public const string TenSeconds = "10 seconds remaining";
        public const string WinnerIs = "The Winner Is";
        public const string TeamBattle = "Team Battle";
        public const string CaptureTheFlag = "Capture The Flag";
        public const string MatchOver = "MatchOver01";
        public const string CountdownTick = "SR_CountdownTick";
        public const string CountdownGo = "SR_CountdownGO";

        /// <summary>The spoken count "10up" .. "1up".</summary>
        public static string Count(int n)
        {
            return n >= 1 && n <= 10 ? n + "up" : null;
        }

        public static string TeamWins(int team)
        {
            return team == 0 ? BlueWins : team == 1 ? RedWins : Tie;
        }

        public static string TeamScores(int team)
        {
            return team == 0 ? BlueScores : RedScores;
        }

        public static void Say(string clip, float volume = 1f)
        {
            if (!string.IsNullOrEmpty(clip))
            {
                RrSound.PlayUi(clip, volume);
            }
        }

        /// <summary>
        /// Says a line and reports the loaded clip (null when it is not in the install), so a game can log that its
        /// line really plays (the quests' "Game On!").
        /// </summary>
        public static void Say(string clip, float volume, System.Action<UnityEngine.AudioClip> loaded)
        {
            if (string.IsNullOrEmpty(clip) || RrAssets.Instance == null)
            {
                if (loaded != null)
                {
                    loaded(null);
                }

                return;
            }

            // 83 = AudioClip's class id, the same lookup RrSound.PlayUi makes by name.
            RrAssets.Instance.Load<UnityEngine.AudioClip>(clip, 83, c =>
            {
                if (c != null)
                {
                    RrSound.PlayUi(c, volume);
                }

                if (loaded != null)
                {
                    loaded(c);
                }
            });
        }
    }
}
