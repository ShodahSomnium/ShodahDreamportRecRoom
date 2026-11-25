using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// Harness hooks ({"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball.RrPaintballTest.X"}), offline: the
    /// local player is the instance authority. They drive the real code paths (StartCountdown, the flag requests
    /// through RrNet events, RrHealth's out and respawn, the authority's hit count) and log "PaintballTest: ...".
    /// The world never calls them.
    /// </summary>
    public static class RrPaintballTest
    {
        private static RrPaintballRoom Room
        {
            get { return RrPaintballRoom.Current; }
        }

        private static void Log(string text)
        {
            RrLog.Info("Paintball", "PaintballTest: " + text);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        public static IEnumerator StartCtfRiver()
        {
            return StartMatch(RrPaintballMaps.River, RrPaintballRoom.ModeCtf);
        }

        public static IEnumerator StartTeamBattleDriveIn()
        {
            return StartMatch(RrPaintballMaps.DriveIn, RrPaintballRoom.ModeTeamBattle);
        }

        public static IEnumerator ShowRiver() { return ShowMap(RrPaintballMaps.River); }
        public static IEnumerator ShowHomestead() { return ShowMap(RrPaintballMaps.Homestead); }
        public static IEnumerator ShowQuarry() { return ShowMap(RrPaintballMaps.Quarry); }
        public static IEnumerator ShowClearcut() { return ShowMap(RrPaintballMaps.Clearcut); }
        public static IEnumerator ShowSpillway() { return ShowMap(RrPaintballMaps.Spillway); }
        public static IEnumerator ShowDriveIn() { return ShowMap(RrPaintballMaps.DriveIn); }
        public static IEnumerator ShowMerryMidway() { return ShowMap(RrPaintballMaps.MerryMidway); }

        /// <summary>Chooses a map in the lobby, waits for its level (up to 90 s) and shows it (the lobby hidden) for shots.</summary>
        private static IEnumerator ShowMap(int map)
        {
            var room = Room;
            if (room == null || !room.Built)
            {
                Log("no built paintball room");
                yield break;
            }

            float t0 = Time.realtimeSinceStartup;
            room.TestSelectMap(map);
            float until = t0 + 90f;
            while (!room.MapReady && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            room.SetArea(true);
            yield return Wait(0.5f);
            var m = room.ActiveMap;
            Log("map " + m.Name + ": ready " + room.MapReady + " after " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s; spawns " + m.Spawns[0].Count + "/" + m.Spawns[1].Count +
                ", blue flag " + m.FlagStands[0].ToString("F1") + ", red flag " + m.FlagStands[1].ToString("F1") + ", pickups " + room.Racks.Count + ", kill y " + m.KillY.ToString("0.0") +
                ", blue spawn 0 " + m.SpawnPose(0, 0).position.ToString("F1") + ", red spawn 0 " + m.SpawnPose(1, 0).position.ToString("F1"));
        }

        /// <summary>Shows the lobby again (the map hidden).</summary>
        public static IEnumerator ShowLobby()
        {
            var room = Room;
            if (room != null)
            {
                room.SetArea(false);
            }

            yield return Wait(0.3f);
            Log("lobby shown");
        }

        /// <summary>Plays a shooting-gallery round with 7 scored hits (the bookkeeping of the hit path).</summary>
        public static IEnumerator Gallery()
        {
            var g = RrPaintballGallery.Current;
            if (g == null)
            {
                Log("gallery: none");
                yield break;
            }

            g.TestScore(7);
            yield return Wait(0.5f);
            Log("gallery: score " + g.Score + ", running " + g.Running);
        }

        /// <summary>START with one player: refused (the final rule is 2 players).</summary>
        public static void PressStartAlone()
        {
            var room = Room;
            if (room != null)
            {
                room.PressStart();
                Log("START alone: phase " + room.Phase);
            }
        }

        /// <summary>The lobby's game-start hologram: waits up to 6 s for it to open, logs what it shows, presses its START (1 player: refused).</summary>
        public static IEnumerator Hologram()
        {
            var room = Room;
            var h = room != null ? room.Hologram : null;
            if (h == null)
            {
                Log("hologram: none");
                yield break;
            }

            float until = Time.realtimeSinceStartup + 6f;
            while (!h.IsOpen && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Log("hologram: open " + h.IsOpen + ", mount " + h.MountMode + ", " + h.ShownPlayers + " player row(s), START '" + h.StartText + "' enabled " + h.StartEnabled +
                " at " + h.transform.position.ToString("F1"));
            h.Press("test");
            yield return Wait(0.3f);
            Log("hologram START pressed: phase " + room.Phase);
        }

        private static IEnumerator StartMatch(int map, int mode)
        {
            var room = Room;
            if (room == null || !room.Built)
            {
                Log("no built paintball room");
                yield break;
            }

            room.TestStart(map, mode);
            yield return Wait(0.2f);
            Log("countdown: phase " + room.Phase + ", " + RrNet.Instance.TimeRemaining.ToString("0.0") + " s left, team " + RrTeams.LocalTeam);
            room.TestSkipTimer(0.3f);
            float until = Time.realtimeSinceStartup + 90f;
            while ((room.Phase != RrPhase.Playing || !room.OnMap) && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return Wait(2f);
            Log("playing " + RrPaintballMaps.Names[room.MapIndex] + " mode " + room.Mode + ": phase " + room.Phase + ", team " + RrTeams.LocalTeam +
                ", feet " + room.transform.InverseTransformPoint(RrPlayer.FeetPosition) + " (room-local), loadout " + RrLoadout.Given.Count +
                ", health " + (RrHealth.Local != null ? RrHealth.Local.Health + "/" + RrHealth.Local.MaxHealth : "none"));
        }

        /// <summary>Fires the held gun 8 times toward the enemy flag, from where the player stands (splats on whatever is in the way).</summary>
        public static IEnumerator Fire()
        {
            var room = Room;
            RrWeapon gun = null;
            for (int i = 0; i < RrWeapon.All.Count; i++)
            {
                if (RrWeapon.All[i] != null && RrWeapon.All[i].IsHeld)
                {
                    gun = RrWeapon.All[i];
                }
            }

            if (room == null || gun == null)
            {
                Log("fire: no held weapon (" + RrWeapon.All.Count + " weapons)");
                yield break;
            }

            int team = Mathf.Clamp(RrTeams.LocalTeam, 0, 1);
            var map = room.ActiveMap;
            Vector3 from = RrPlayer.HeadPosition;
            Vector3 enemy = room.transform.TransformPoint(map.FlagStands[1 - team]);
            Vector3 dir = enemy - from;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            int fired = 0;
            for (int i = 0; i < 8; i++)
            {
                Vector3 side = Vector3.Cross(Vector3.up, dir) * ((i % 4) - 1.5f) * 0.8f;
                if (gun.FireToward(from + dir * 12f + side + Vector3.down * (0.4f + (i / 4) * 0.5f)))
                {
                    fired++;
                }

                yield return Wait(0.3f);
            }

            yield return Wait(0.8f);
            Log("fired " + fired + " of 8 " + gun.PrefabName + " shots toward the enemy flag");
        }

        public static IEnumerator TakeFlag()
        {
            var room = Room;
            if (room == null || room.Flags == null)
            {
                yield break;
            }

            int enemy = 1 - Mathf.Clamp(RrTeams.LocalTeam, 0, 1);
            Vector3 stand = room.Flags.StandPoint(enemy);
            RrPlayer.Teleport(stand + (room.transform.position - stand).normalized * 0.4f, RrPlayer.Yaw);
            float until = Time.realtimeSinceStartup + 5f;
            while (room.Flags.StateOf(enemy) != RrPaintballFlags.Carried && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Log("take flag: " + RrTeams.Name(enemy) + " flag state " + room.Flags.StateOf(enemy) + ", holder " + room.Flags.HolderOf(enemy) + " (me " + RrNet.LocalId + ")");
            yield return Wait(1f);
        }

        public static IEnumerator Capture()
        {
            var room = Room;
            if (room == null || room.Flags == null)
            {
                yield break;
            }

            int team = Mathf.Clamp(RrTeams.LocalTeam, 0, 1);
            int before = RrNet.Instance.TeamScore(team);
            Vector3 stand = room.Flags.StandPoint(team);
            RrPlayer.Teleport(stand + Vector3.right * 0.6f, RrPlayer.Yaw);
            float until = Time.realtimeSinceStartup + 5f;
            while (RrNet.Instance.TeamScore(team) == before && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Log("capture: " + RrTeams.Name(team) + " score " + before + " -> " + RrNet.Instance.TeamScore(team) + ", enemy flag state " + room.Flags.StateOf(1 - team));
            yield return Wait(2f);
        }

        /// <summary>The local player is hit (out), then respawns in the base 5 s later.</summary>
        public static IEnumerator Out()
        {
            var h = RrHealth.Local;
            var room = Room;
            if (h == null || room == null)
            {
                Log("out: no health");
                yield break;
            }

            Vector3 before = RrPlayer.FeetPosition;
            h.ApplyDamage(18, 99, RrBodyPart.Body, before + Vector3.up, 1);
            Log("out: hit for 18 of " + h.MaxHealth + " -> out " + h.IsOut + ", status " + h.Status);
            yield return Wait(5.8f);
            Log("respawned: out " + h.IsOut + ", moved " + Vector3.Distance(before, RrPlayer.FeetPosition).ToString("0.0") + " m to " +
                room.transform.InverseTransformPoint(RrPlayer.FeetPosition) + " (room-local), invincible " + h.Invincible);
        }

        /// <summary>Team Battle scoring through the authority's hit count (3 hits for Red).</summary>
        public static IEnumerator Hits()
        {
            var room = Room;
            if (room == null)
            {
                yield break;
            }

            for (int i = 0; i < 3; i++)
            {
                room.AuthorityCountHit(1);
                yield return Wait(0.1f);
            }

            Log("hits: Blue " + RrNet.Instance.TeamScore(0) + " - Red " + RrNet.Instance.TeamScore(1) + " (mode " + room.Mode + ")");
        }

        public static IEnumerator Rack()
        {
            var room = Room;
            if (room == null)
            {
                yield break;
            }

            RrPaintballRack pick = null;
            for (int i = 0; i < room.Racks.Count; i++)
            {
                var r = room.Racks[i];
                if (r.Networked && r.Prefab == RrPaintballRoom.Shotgun && room.ActiveMap.Contains(room.transform.InverseTransformPoint(r.transform.position)))
                {
                    pick = r;
                    break;
                }
            }

            if (pick == null)
            {
                Log("rack: none on this map");
                yield break;
            }

            bool took = pick.Take("test", RrHandSide.Right);
            yield return Wait(1.5f);
            Log("rack " + pick.Index + ": took " + took + ", available " + pick.Available + ", loadout " + RrLoadout.Given.Count +
                (RrLoadout.Given.Count > 0 && RrLoadout.Given[0] != null ? " (" + RrLoadout.Given[0].name + ")" : ""));
        }

        /// <summary>Takes a field grenade and bursts it in the hand (the thrower's splash path, EvBoom).</summary>
        public static IEnumerator Grenade()
        {
            var room = Room;
            if (room == null)
            {
                yield break;
            }

            RrPaintballRack pick = null;
            for (int i = 0; i < room.Racks.Count; i++)
            {
                if (room.Racks[i] != null && room.Racks[i].Prefab == RrPaintballRoom.Grenade && room.Racks[i].Available)
                {
                    pick = room.Racks[i];
                    break;
                }
            }

            if (pick == null)
            {
                Log("grenade: no grenade pickup on this map");
                yield break;
            }

            bool took = pick.Take("test", RrHandSide.Right);
            yield return Wait(1.5f);
            RrPaintballThrowable held = null;
            for (int i = 0; i < RrLoadout.Given.Count; i++)
            {
                if (RrLoadout.Given[i] != null)
                {
                    held = RrLoadout.Given[i].GetComponent<RrPaintballThrowable>();
                }
            }

            Log("grenade: took " + took + ", throwable " + (held != null));
            if (held != null)
            {
                held.TestBurst();
            }

            yield return Wait(1f);
        }

        public static void Report()
        {
            var room = Room;
            if (room == null)
            {
                Log("report: no room");
                return;
            }

            var s = RrNet.Instance;
            Log("report: phase " + s.Phase + ", map " + RrPaintballMaps.Names[room.MapIndex] + ", mode " + room.Mode + ", score " + s.TeamScore(0) + "-" + s.TeamScore(1) +
                ", flags " + (room.Flags != null ? room.Flags.StateOf(0) + "/" + room.Flags.StateOf(1) : "none") + ", racks " + room.Racks.Count +
                ", team " + RrTeams.LocalTeam + ", music " + RrMusic.Current);
        }
    }
}
