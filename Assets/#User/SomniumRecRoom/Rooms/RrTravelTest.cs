using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Harness hooks for the one-room-at-the-origin travel (scenario travel_origin.json). Without the client the
    /// editor rig stands in for the body: <see cref="SimulateClientRespawn"/> moves it to the scene spawn the way
    /// the client's respawn does (not through RrPlayer.Teleport), <see cref="SimulateFall"/> drops it 40 m under the
    /// room's lowest floor; each then waits for travel's rescue and logs "TravelTest: ... rescued" or "... NOT
    /// rescued". <see cref="Report"/> logs a census.
    /// </summary>
    public static class RrTravelTest
    {
        /// <summary>The client's respawn: a jump to (0, 0.9, 0) that no teleport of ours explains.</summary>
        public static IEnumerator SimulateClientRespawn()
        {
            var room = RrTravel.CurrentRoom;
            if (room == null)
            {
                RrLog.Warn("TravelTest", "respawn: no current room");
                yield break;
            }

            // Stand somewhere at least 3 m from the scene spawn first (our own teleport), then let its grace pass.
            Vector3 feet = RrPlayer.FeetPosition;
            if (new Vector2(feet.x, feet.z).magnitude < RrTravel.RespawnJump + 1f)
            {
                RrPlayer.Teleport(feet + new Vector3(0f, 0f, -(RrTravel.RespawnJump + 2f)), RrPlayer.Yaw);
            }

            yield return Wait(RrTravel.OwnTeleportGrace + 0.5f);
            Vector3 before = RrPlayer.FeetPosition;
            if (!RrPlayer.SimulateExternalMove(RrPlacement.SceneSpawn))
            {
                RrLog.Warn("TravelTest", "respawn: no editor rig to move (client present?)");
                yield break;
            }

            RrLog.Info("TravelTest", "respawn: body moved from " + Fmt(before) + " to the scene spawn " + Fmt(RrPlacement.SceneSpawn) + " as the client does");
            yield return WaitRescue("respawn", room);
        }

        /// <summary>A fall out of the room: 40 m under its lowest floor.</summary>
        public static IEnumerator SimulateFall()
        {
            var room = RrTravel.CurrentRoom;
            if (room == null)
            {
                RrLog.Warn("TravelTest", "fall: no current room");
                yield break;
            }

            yield return Wait(3.5f);
            float floor = float.IsNaN(room.LowestFloor) ? room.Origin.y : room.LowestFloor;
            Vector3 feet = RrPlayer.FeetPosition;
            Vector3 below = new Vector3(feet.x, floor - 40f, feet.z);
            if (!RrPlayer.SimulateExternalMove(below))
            {
                RrLog.Warn("TravelTest", "fall: no editor rig to move (client present?)");
                yield break;
            }

            RrLog.Info("TravelTest", "fall: body dropped to " + Fmt(below) + " (lowest floor y " + floor.ToString("0.0") + ")");
            yield return WaitRescue("fall", room);
        }

        public static void Report()
        {
            RrTravel.Census("TravelTest report");
        }

        private static IEnumerator WaitRescue(string what, RrRoom room)
        {
            float until = Time.realtimeSinceStartup + 2.5f;
            float movedAt = RrPlayer.LastTeleportAt;
            while (Time.realtimeSinceStartup < until)
            {
                if (RrPlayer.LastTeleportAt > movedAt)
                {
                    yield return Wait(0.3f);
                    Vector3 feet = RrPlayer.FeetPosition;
                    Vector3 target = RrPlayer.LastTeleportTarget;
                    RrLog.Info("TravelTest", what + ": rescued in " + room.Activity + " -> feet " + Fmt(feet) + " (target " + Fmt(target) + ")");
                    yield break;
                }

                yield return null;
            }

            RrLog.Warn("TravelTest", what + ": NOT rescued within 2.5 s (feet " + Fmt(RrPlayer.FeetPosition) + ")");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0") + ")";
        }
    }
}
