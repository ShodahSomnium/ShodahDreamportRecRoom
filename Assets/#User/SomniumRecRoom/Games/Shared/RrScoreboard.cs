using SomniumSpace.Worlds.SomniumRecRoom.Net;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// A wall scoreboard (facing its local +z: stand on the +z side to read it) in Rec Room's arena style: a dark panel with the title, the clock, and each team's score
    /// in its colour, read every frame from the instance state (<see cref="RrNet.Instance"/>), so every member
    /// sees the authority's numbers. Games with their own layout (bowling frames, golf strokes) set
    /// <see cref="Custom"/> text instead of team scores.
    /// </summary>
    public sealed class RrScoreboard : MonoBehaviour
    {
        public int Teams = 2;
        public string Title = "";

        /// <summary>When set, shown in place of the team scores.</summary>
        public string Custom;

        private TextMeshPro _title;
        private TextMeshPro _clock;
        private TextMeshPro[] _scores;
        private TextMeshPro _custom;

        public static RrScoreboard Create(Transform parent, Vector3 localPos, Quaternion localRot, string title, int teams = 2, float width = 3.2f)
        {
            var root = RrBuild.Group(parent, "Scoreboard", localPos, localRot).transform;
            float height = width * 0.45f;
            RrBuild.Box(root, "Panel", new Vector3(0f, 0f, -0.06f), new Vector3(width, height, 0.1f), RrLook.Solid(RrLook.DarkGrey), false);
            RrBuild.Box(root, "Trim", new Vector3(0f, height * 0.5f + 0.03f, -0.06f), new Vector3(width + 0.1f, 0.06f, 0.14f), RrLook.Solid(RrLook.Orange), false);
            var board = root.gameObject.AddComponent<RrScoreboard>();
            board.Teams = teams;
            board.Title = title;
            board._title = RrBuild.Sign(root, "Title", title, new Vector3(0f, height * 0.33f, 0f), Quaternion.Euler(0f, 180f, 0f), width * 0.9f, Color.white, true, width);
            board._clock = RrBuild.Sign(root, "Clock", "", new Vector3(0f, height * 0.12f, 0f), Quaternion.Euler(0f, 180f, 0f), width * 0.8f, new Color(1f, 0.85f, 0.3f), true, width);
            board._scores = new TextMeshPro[Mathf.Max(0, teams)];
            for (int t = 0; t < board._scores.Length; t++)
            {
                float x = teams == 1 ? 0f : Mathf.Lerp(width * 0.3f, -width * 0.3f, t / (float)Mathf.Max(1, teams - 1));
                board._scores[t] = RrBuild.Sign(root, "Score " + t, "0", new Vector3(x, -height * 0.18f, 0f), Quaternion.Euler(0f, 180f, 0f), width * 1.6f, RrTeams.Color(t), true, width * 0.5f);
            }

            board._custom = RrBuild.Sign(root, "Custom", "", new Vector3(0f, -height * 0.18f, 0f), Quaternion.Euler(0f, 180f, 0f), width * 0.5f, Color.white, false, width * 0.95f);
            return board;
        }

        private void Update()
        {
            var s = RrNet.Instance;
            if (_clock != null)
            {
                string phase;
                switch (s.Phase)
                {
                    case RrPhase.Lobby: phase = "WAITING FOR PLAYERS"; break;
                    case RrPhase.Countdown: phase = "STARTING IN " + Mathf.CeilToInt(s.TimeRemaining); break;
                    case RrPhase.RoundOver: phase = "ROUND OVER"; break;
                    case RrPhase.MatchOver: phase = "GAME OVER"; break;
                    default:
                        float t = s.TimeRemaining;
                        phase = t > 0f ? string.Format("{0}:{1:00}", (int)(t / 60f), (int)(t % 60f)) : "";
                        break;
                }

                _clock.text = phase;
            }

            bool custom = !string.IsNullOrEmpty(Custom);
            if (_custom != null)
            {
                _custom.text = custom ? Custom : "";
            }

            if (_scores != null)
            {
                for (int i = 0; i < _scores.Length; i++)
                {
                    _scores[i].text = custom ? "" : s.TeamScore(i).ToString();
                }
            }
        }
    }
}
