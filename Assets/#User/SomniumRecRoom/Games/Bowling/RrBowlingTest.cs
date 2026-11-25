using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Bowling
{
    /// <summary>
    /// Play-mode harness hooks ({"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Bowling.RrBowlingTest.X"}), run after
    /// {"goto": "Bowling"}: START (alone: one bowler), then rolls down the scoring lane until the game ends.
    /// </summary>
    public static class RrBowlingTest
    {
        private static RrBowlingRoom Room
        {
            get { return RrTravel.CurrentRoom as RrBowlingRoom; }
        }

        /// <summary>Presses START and waits for play.</summary>
        public static IEnumerator Start()
        {
            var room = Room;
            if (room == null)
            {
                RrLog.Warn("BowlingTest", "not in the bowling alley");
                yield break;
            }

            room.RequestStart();
            float until = Time.realtimeSinceStartup + 12f;
            while ((room.Phase != RrPhase.Playing || room.LaneState != RrBowlingLane.StateReady) && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("BowlingTest", "start: phase " + room.Phase + ", bowlers " + room.BowlerCount + ", lane state " + room.LaneState);
        }

        /// <summary>One roll: a scoring ball set on the approach and sent down the lane at 7 m/s; waits for the next Ready.</summary>
        public static IEnumerator Roll()
        {
            var room = Room;
            var lane = room != null ? room.Lanes[RrBowlingRoom.ScoringLane] : null;
            if (lane == null || lane.Balls.Count == 0)
            {
                yield break;
            }

            var ball = lane.Balls[0];
            if (ball.Net != null)
            {
                ball.Net.LocalTouch();
            }

            int roll = room.RollId;
            Vector3 p = room.transform.TransformPoint(new Vector3(0.05f, 0.3f, 9f));
            ball.Body.isKinematic = false;
            ball.transform.position = p;
            ball.Body.position = p;
            ball.Body.linearVelocity = room.transform.TransformDirection(new Vector3(-0.02f, 0f, -7f));
            float until = Time.realtimeSinceStartup + 25f;
            while ((room.RollId == roll || room.LaneState != RrBowlingLane.StateReady) && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("BowlingTest", "roll " + room.RollId + ": frame " + (room.Frame + 1) + " ball " + (room.FrameRoll + 1) + ", standing " + RrBowlingScore.Count(room.Standing) + ", phase " + room.Phase);
        }

        /// <summary>Rolls until the game is over (at most 21 rolls).</summary>
        public static IEnumerator Game()
        {
            for (int i = 0; i < 21 && Room != null && Room.Phase == RrPhase.Playing; i++)
            {
                yield return Roll();
                yield return new WaitForSecondsRealtime(3f);
            }

            RrLog.Info("BowlingTest", "game: phase " + (Room != null ? Room.Phase.ToString() : "-"));
        }
    }
}
