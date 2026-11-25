using SomniumSpace.Worlds.SomniumRecRoom.Games.StuntRunner;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    public static partial class RrRegistry
    {
        /// <summary>
        /// Stunt Runner (^StuntRunner, 2019-09-05; rr_latest_rgames_story.md 11, rr_party.md 2): up to 4 walking runners
        /// race six obstacle courses on a TV sound stage, lowest total time wins (<see cref="RrStuntRunnerRoom"/>). Rec Room's
        /// own levels 92-98 are the lobby and the courses. Art: the Stunt Runner decal (as RrWatchArt); lobby music
        /// SR_PostRun01.
        /// </summary>
        static partial void RegisterStuntRunner()
        {
            RrRooms.Register(new RrRoomInfo
            {
                Activity = RrActivity.StuntRunner,
                Title = "Stunt Runner",
                Subtitle = "Racing - 1-4 runners",
                Description = "Rec Room's obstacle-course game show: sprint, climb, wall-run and slide through six courses - Anticipation, Daring, Patience, " +
                              "Caution, Trust and Perseverance. Grab KO Colas for a boost, hit the checkpoints and slap the button at the finish. Lowest total time wins!",
                Category = "Racing",
                MinPlayers = 1,
                MaxPlayers = 4,
                CoverSprite = RrStuntRunnerRoom.Art,
                IconSprite = RrStuntRunnerRoom.Art,
                Music = RrStuntRunnerRoom.LobbyMusicClip,
                Networked = true,
                Create = root => root.AddComponent<RrStuntRunnerRoom>(),
            });
        }
    }
}
