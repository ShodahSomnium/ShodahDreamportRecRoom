using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Dodgeball
{
    /// <summary>
    /// Play-mode harness hooks ({"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Dodgeball.RrDodgeballTest.X"}),
    /// run after {"goto": "Dodgeball"}. Offline the local player is the authority, so the whole claim / verdict
    /// path runs on one client (events loop back).
    /// </summary>
    public static class RrDodgeballTest
    {
        public const int FakeOpponent = 77;

        private static RrDodgeballRoom Room
        {
            get { return RrTravel.CurrentRoom as RrDodgeballRoom; }
        }

        /// <summary>Presses START and waits for the round to be playing (solo: practice).</summary>
        public static IEnumerator Start()
        {
            var room = Room;
            if (room == null)
            {
                RrLog.Warn("DodgeballTest", "not in the dodgeball gym");
                yield break;
            }

            room.RequestStart();
            float until = Time.realtimeSinceStartup + 10f;
            while (room.Phase != RrPhase.Playing && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("DodgeballTest", "start: phase " + room.Phase + ", practice " + room.Practice + ", balls " + room.Balls.Count + ", local team " + room.LocalTeam);
        }

        /// <summary>Throws ball 1 from the blue half at practice standee 0 the way a screen-mode throw does.</summary>
        public static IEnumerator Throw()
        {
            var room = Room;
            if (room == null || room.Balls.Count < 2 || room.Targets.Count < 1)
            {
                RrLog.Warn("DodgeballTest", "throw: nothing to throw at");
                yield break;
            }

            var ball = room.Balls[1];
            var target = room.Targets[0];
            Vector3 from = room.transform.TransformPoint(new Vector3(-0.5f, room.CourtFloor + 1.4f, -1.5f));
            Vector3 chest = target.Head.position - Vector3.up * 0.4f;
            Vector3 dir = (chest - from).normalized;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
            float pitch = Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) + RrDodgeballRoom.GravityCompensation * Mathf.Deg2Rad;
            Vector3 v = (flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * RrDodgeballRoom.ScreenThrowSpeed;
            ball.Net.ResetTo(from, Quaternion.identity);
            if (ball.Body != null)
            {
                ball.Body.isKinematic = false;
                ball.Body.linearVelocity = v;
            }

            ball.MarkLive(0, RrNet.LocalId);
            Vector3 spin;
            float screen = room.ScreenThrowVelocity(ball, out spin).magnitude;
            RrLog.Info("DodgeballTest", "threw ball 1 at " + v.magnitude.ToString("F1") + " m/s toward standee 0 (live " + ball.Live + "); the screen throw gives " + screen.ToString("F1") + " m/s at any hold");
            float until = Time.realtimeSinceStartup + 2f;
            int before = target.Knocks;
            while (target.Knocks == before && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("DodgeballTest", "throw result: standee knocks " + target.Knocks + ", ball live " + ball.Live + ", hits " + ball.PlayerHits);
        }

        /// <summary>
        /// Drops ball 2 from 1.5 m on the blue half and measures the first rebound: with Rec Room's court physics the
        /// ball (bounce 0.6, Minimum) should come back up with e ~0.6 (bottom 1.39 m -> ~0.5 m); on Unity's default
        /// floor it stayed down (e = 0).
        /// </summary>
        public static IEnumerator Bounce()
        {
            var room = Room;
            if (room == null || room.Balls.Count < 3 || room.Balls[2].Body == null)
            {
                RrLog.Warn("DodgeballTest", "bounce: no ball 2");
                yield break;
            }

            var ball = room.Balls[2];
            var body = ball.Body;
            float drop = 1.5f;
            // Measured from the court's drawn floor (y 0.436 in level11, 0 in the stand-in).
            ball.Net.ResetTo(room.transform.TransformPoint(new Vector3(3f, room.CourtFloor + drop, -1.5f)), Quaternion.identity);
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            float floorY = room.transform.TransformPoint(new Vector3(0f, room.CourtFloor, 0f)).y;
            bool bounced = false;
            float peak = 0f;
            float lastVy = 0f;
            float until = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return new WaitForFixedUpdate();
                float vy = body.linearVelocity.y;
                float bottom = body.position.y - floorY - RrDodgeball.Radius;
                if (!bounced && lastVy < -1f && vy > 0f)
                {
                    bounced = true;
                }

                if (bounced)
                {
                    peak = Mathf.Max(peak, bottom);
                    if (vy < 0f)
                    {
                        break;
                    }
                }

                lastVy = vy;
            }

            float fall = drop - RrDodgeball.Radius;
            float e = bounced ? Mathf.Sqrt(Mathf.Max(0f, peak) / fall) : 0f;
            RrLog.Info("DodgeballTest", "bounce result: dropped " + fall.ToString("F2") + " m, rebound " + peak.ToString("F2") + " m, restitution " + e.ToString("F2") + (bounced ? "" : " (no bounce)"));
        }

        /// <summary>
        /// The final catch rule: a fake red opponent (id 77, ends practice) throws ball 2 live at the local player; its
        /// client claims the hit, the local player catches the same throw inside the 150 ms window. Expected: a
        /// "catch" verdict with nobody out (no catch-out, nobody back), still playing.
        /// </summary>
        public static IEnumerator Catch()
        {
            var room = Room;
            if (room == null || room.Phase != RrPhase.Playing || room.Balls.Count < 3)
            {
                RrLog.Warn("DodgeballTest", "catch: not playing");
                yield break;
            }

            room.TestAddOpponent(FakeOpponent);
            yield return null;
            var ball = room.Balls[2];
            ball.MarkLive(1, FakeOpponent);
            int throwId = ball.Net != null ? ball.Net.ThrowId & 0xFFFF : 0;
            int before = room.Verdicts;
            int me = RrNet.LocalId;
            room.TestClaimHit(me, FakeOpponent, ball.Index, throwId);
            room.OnLocalGrab(ball);
            ball.Kill();
            float until = Time.realtimeSinceStartup + 2f;
            while (room.Verdicts == before && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            // Past the catch window, so a hit that was not beaten would have been decided by now.
            float settle = Time.realtimeSinceStartup + 0.4f;
            while (Time.realtimeSinceStartup < settle)
            {
                yield return null;
            }

            bool meOut = room.TestIsOut(me);
            bool himOut = room.TestIsOut(FakeOpponent);
            RrLog.Info("DodgeballTest", "catch result: verdicts " + (room.Verdicts - before) + ", local out " + meOut + ", thrower out " + himOut + ", phase " + room.Phase + ", ball live " + ball.Live + (!meOut && !himOut && room.Phase == RrPhase.Playing ? ": nobody out" : ": WRONG"));
        }

        /// <summary>Adds a fake red opponent (ends practice) and claims a hit on it: verdict, round over, blue +1.</summary>
        public static IEnumerator Opponent()
        {
            var room = Room;
            if (room == null || room.Phase != RrPhase.Playing)
            {
                RrLog.Warn("DodgeballTest", "opponent: not playing");
                yield break;
            }

            room.TestAddOpponent(FakeOpponent);
            yield return null;
            room.TestClaimHit(FakeOpponent, RrNet.LocalId, 0, 1);
            float until = Time.realtimeSinceStartup + 3f;
            while (room.Phase == RrPhase.Playing && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("DodgeballTest", "opponent result: phase " + room.Phase + ", blue " + RrNet.Instance.TeamScore(0) + " - red " + RrNet.Instance.TeamScore(1));
        }
    }
}
