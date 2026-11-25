using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Soccer
{
    /// <summary>
    /// Play-mode harness hooks ({"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Soccer.RrSoccerTest.X"}), run after
    /// {"goto": "Soccer"}: START (alone: practice), then a shot from 6 m in front of the red goal.
    /// </summary>
    public static class RrSoccerTest
    {
        private static RrSoccerRoom Room
        {
            get { return RrTravel.CurrentRoom as RrSoccerRoom; }
        }

        /// <summary>Presses START and waits for the kick-off drop.</summary>
        public static IEnumerator Start()
        {
            var room = Room;
            if (room == null)
            {
                RrLog.Warn("SoccerTest", "not in the soccer gym");
                yield break;
            }

            room.RequestStart();
            float until = Time.realtimeSinceStartup + 12f;
            while (room.Phase != RrPhase.Playing && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("SoccerTest", "start: phase " + room.Phase + ", ball state " + room.BallState + ", kick-off " + room.Kickoff + ", team " + RrTeams.LocalTeam);
        }

        /// <summary>Kicks the ball from (0, 1, 38) into the red goal (+z) and waits for blue's goal.</summary>
        public static IEnumerator Shoot()
        {
            var room = Room;
            if (room == null || room.Ball == null)
            {
                yield break;
            }

            var ball = room.Ball;
            if (ball.Net != null)
            {
                ball.Net.LocalTouch();
            }

            Vector3 p = room.transform.TransformPoint(new Vector3(0f, 1f, 38f));
            ball.transform.position = p;
            ball.Body.position = p;
            ball.Kick(room.transform.TransformDirection(new Vector3(0f, 0.1f, 1f)), 20f, "test");
            int blue = RrNet.Instance.TeamScore(0);
            float until = Time.realtimeSinceStartup + 5f;
            while (RrNet.Instance.TeamScore(0) == blue && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("SoccerTest", "shot: blue " + RrNet.Instance.TeamScore(0) + " - red " + RrNet.Instance.TeamScore(1) + ", ball state " + room.BallState);
        }
    }
}
