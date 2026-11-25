using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paddleball
{
    /// <summary>
    /// Play-mode harness hooks ({"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Paddleball.RrPaddleballTest.X"}),
    /// run after {"goto": "Paddleball"}. Alone, the red end is empty, so the red side serves itself after 1 s.
    /// </summary>
    public static class RrPaddleballTest
    {
        private static RrPaddleballRoom Room
        {
            get { return RrTravel.CurrentRoom as RrPaddleballRoom; }
        }

        /// <summary>Presses START and waits for play (the local player is blue, in the -Z end with a paddle).</summary>
        public static IEnumerator Start()
        {
            var room = Room;
            if (room == null)
            {
                RrLog.Warn("PaddleballTest", "not on the paddleball court");
                yield break;
            }

            room.RequestStart();
            float until = Time.realtimeSinceStartup + 10f;
            while (room.Phase != RrPhase.Playing && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            int held = 0;
            foreach (var p in room.Paddles)
            {
                held += p != null && p.HeldLocally ? 1 : 0;
            }

            RrLog.Info("PaddleballTest", "start: phase " + room.Phase + ", ball state " + room.BallState + ", server " + room.Server + ", paddles held locally " + held);
        }

        /// <summary>The blue player's serve: the paddle hit path with a forward stroke; waits for the goal at the red end.</summary>
        public static IEnumerator Serve()
        {
            var room = Room;
            if (room == null || room.Ball == null)
            {
                yield break;
            }

            // Toward red's end (+z in the stand-in tube, -z in level13).
            room.TestHit(new Vector3(0.25f, 0.15f, -room.EndZ(0)), 9f);
            RrLog.Info("PaddleballTest", "served at 9 m/s, ball state " + room.BallState);
            float until = Time.realtimeSinceStartup + 4f;
            int blue = RrNet.Instance.TeamScore(0);
            while (RrNet.Instance.TeamScore(0) == blue && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("PaddleballTest", "serve result: blue " + RrNet.Instance.TeamScore(0) + " - red " + RrNet.Instance.TeamScore(1) + ", wall bounces " + room.Ball.WallBounces);
        }

        /// <summary>Waits for the empty red side's automatic serve and lets it pass the blue player: red scores.</summary>
        public static IEnumerator Concede()
        {
            var room = Room;
            if (room == null)
            {
                yield break;
            }

            float until = Time.realtimeSinceStartup + 12f;
            int red = RrNet.Instance.TeamScore(1);
            while (RrNet.Instance.TeamScore(1) == red && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("PaddleballTest", "concede result: blue " + RrNet.Instance.TeamScore(0) + " - red " + RrNet.Instance.TeamScore(1) + ", server " + room.Server);
        }

        /// <summary>Waits until the ball is in flight (for a mid-rally shot).</summary>
        public static IEnumerator WaitFlight()
        {
            var room = Room;
            if (room == null || room.Ball == null)
            {
                yield break;
            }

            float until = Time.realtimeSinceStartup + 8f;
            while (room.Ball.Velocity.magnitude < 1f && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.35f);
            RrLog.Info("PaddleballTest", "ball in flight at " + room.transform.InverseTransformPoint(room.Ball.transform.position).ToString("F2") + ", " + room.Ball.Velocity.magnitude.ToString("F1") + " m/s");
        }
    }
}
