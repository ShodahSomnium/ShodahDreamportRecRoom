using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.LaserTag
{
    /// <summary>
    /// One Laser Tag map built from Rec Room's own Android level (<see cref="RrLaserTagMaps"/>): the scene kit, the jt=1
    /// hull plates and dress boxes, the level's lights and floors, all through The Rise of Jumbotron's
    /// <see cref="RrJumbotronArea"/> (the same sci-fi kit: install meshes where the install has them, measured plates in
    /// the closest install material elsewhere, lit by RrLightGrid). Everything is under <see cref="Root"/> (with its own
    /// RrJumbotronHull.Owned), so <see cref="Destroy"/> frees the whole map.
    ///
    /// What the level gives (G markers, measured in DevNotes/iter13/lasertag.md): each team's spawn points under
    /// Blue/RedBase SpawnPoints, split into "Match Start" (4 each) and the random respawns (24-26 each); the weapon
    /// pickups of the "Weapons" set (grenades, burst rifles, shotguns, railguns); the lobby's ScoreboardProjector (our game
    /// console stands there); nothing marks the power weapon (<see cref="RrLaserTagMaps.PowerSpots"/>).
    /// </summary>
    public sealed class RrLaserTagLevel
    {
        public struct Weapon
        {
            public string Prefab;
            public Pose Pose;
        }

        public readonly int Map;
        public readonly RrJumbotronArea Area;
        public Transform Root;

        /// <summary>The build ran to its end (check <see cref="Ok"/>).</summary>
        public bool Done;

        /// <summary>Built with both teams' spawn points: playable.</summary>
        public bool Ok;

        /// <summary>Room-local spawn poses per team (0 blue, 1 red): the four match starts, and the random respawns.</summary>
        public readonly List<Pose>[] MatchStart = { new List<Pose>(), new List<Pose>() };
        public readonly List<Pose>[] Respawns = { new List<Pose>(), new List<Pose>() };

        /// <summary>The level's weapon pickups (room-local poses, as the level lays them).</summary>
        public readonly List<Weapon> Weapons = new List<Weapon>();

        /// <summary>The lobby's projector floor point and facing (+Z towards the lobby), room-local.</summary>
        public Pose Console = new Pose(Vector3.zero, Quaternion.identity);

        /// <summary>Arrival points in the lobby, in front of the console and facing it (room-local).</summary>
        public readonly List<Pose> Lobby = new List<Pose>();

        /// <summary>The map's extent (room-local), for the out-of-bounds rule.</summary>
        public Bounds Bounds;

        /// <summary>What the room made for this map (pickups, boards, the console): destroyed with <see cref="Root"/>.</summary>
        public readonly List<RrLaserTagPickup> Pickups = new List<RrLaserTagPickup>();
        public readonly List<RrScoreboard> Boards = new List<RrScoreboard>();
        public RrGameConsole Hologram;

        public RrLaserTagLevel(int map)
        {
            Map = RrLaserTagMaps.Clamp(map);
            Area = new RrJumbotronArea(RrLaserTagMaps.Scenes[Map], RrLaserTagMaps.Anchors[Map]);
        }

        public string Name
        {
            get { return RrLaserTagMaps.Names[Map]; }
        }

        public string Scene
        {
            get { return RrLaserTagMaps.Scenes[Map]; }
        }

        public bool Alive
        {
            get { return Root != null; }
        }

        /// <summary>Builds the level under <paramref name="room"/> (spread over frames) and reads its markers.</summary>
        public IEnumerator Build(Transform room, Dictionary<string, Material> surfaces)
        {
            float t0 = Time.realtimeSinceStartup;
            Root = RrBuild.Group(room, "Map " + Name).transform;
            string text = RrSceneBank.Text(Scene);
            if (string.IsNullOrEmpty(text))
            {
                RrLog.Warn("LaserTag", Scene + ": the scene does not carry Data/" + Scene + ".bytes (RrSceneBank); a bare floor stands in");
                Fallback();
                Done = true;
                yield break;
            }

            yield return Area.Build(text, Root, surfaces);
            if (Root == null)
            {
                Done = true;
                yield break;
            }

            Physics.SyncTransforms();
            Read();
            Ok = MatchStart[0].Count > 0 && MatchStart[1].Count > 0;
            if (!Ok)
            {
                Fallback();
            }

            Done = true;
            RrLog.Info("LaserTag", Name + " built in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s: match starts " + MatchStart[0].Count + " / " + MatchStart[1].Count +
                                   ", respawns " + Respawns[0].Count + " / " + Respawns[1].Count + ", " + Weapons.Count + " weapon pickups, console at " + Console.position.ToString("F1") +
                                   ", bounds " + Bounds.min.ToString("F0") + " .. " + Bounds.max.ToString("F0") + (Ok ? "" : " - NOT PLAYABLE (no spawns)"));
        }

        public void Destroy()
        {
            if (Root != null)
            {
                Object.Destroy(Root.gameObject);
            }

            Root = null;
            Pickups.Clear();
            Boards.Clear();
            Hologram = null;
        }

        // ------------------------------------------------------------------ markers

        private void Read()
        {
            foreach (var m in Area.Markers("spawn"))
            {
                string team = m.Get("team", "");
                int t = team == "blue" ? 0 : team == "red" ? 1 : -1;
                if (t < 0)
                {
                    continue;
                }

                var pose = new Pose(Floor(m.Position, 1f, 2.5f), Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f));
                string path = m.Get("path", "");
                if (path.Contains("Match"))
                {
                    MatchStart[t].Add(pose);
                }
                else
                {
                    Respawns[t].Add(pose);
                }
            }

            for (int t = 0; t < 2; t++)
            {
                if (Respawns[t].Count == 0)
                {
                    Respawns[t].AddRange(MatchStart[t]);
                }
            }

            foreach (var m in Area.Markers("weapon_pickup"))
            {
                if (m.Get("set", "Weapons") != "Weapons")
                {
                    continue;
                }

                string prefab = m.Get("prefab", m.Name);
                if (!prefab.StartsWith("(Arena_"))
                {
                    continue;
                }

                Weapons.Add(new Weapon { Prefab = prefab, Pose = new Pose(m.Position, m.Rotation) });
            }

            var boards = Area.Markers("scoreboard");
            if (boards.Count > 0)
            {
                Console = new Pose(boards[0].Position, Quaternion.Euler(0f, boards[0].Rotation.eulerAngles.y, 0f));
            }
            else if (MatchStart[0].Count > 0)
            {
                Console = new Pose(MatchStart[0][0].position + MatchStart[0][0].rotation * new Vector3(0f, 0f, 3f), MatchStart[0][0].rotation);
            }

            Bounds = Area.Area();
            LobbyPoses();
        }

        /// <summary>
        /// Eight arrival points 3.5-5.5 m in front of the projector, facing it (Rec Room's levels have no pregame spawn
        /// points: the lobby spawn came from the room's own spawner), each put on the floor under it.
        /// </summary>
        private void LobbyPoses()
        {
            Lobby.Clear();
            var fwd = Console.rotation * Vector3.forward;
            var right = Console.rotation * Vector3.right;
            var face = Quaternion.Euler(0f, Console.rotation.eulerAngles.y + 180f, 0f);
            for (int i = 0; i < 8; i++)
            {
                float side = (i % 4 - 1.5f) * 1.1f;
                float depth = 3.5f + (i / 4) * 1.5f;
                var p = Console.position + fwd * depth + right * side;
                Lobby.Add(new Pose(Floor(p + Vector3.up * 0.3f, 2f, 4f), face));
            }
        }

        /// <summary>The floor under a room-local point (searched from <paramref name="above"/> over it, <paramref name="below"/> under it), else the point.</summary>
        public Vector3 Floor(Vector3 local, float above, float below)
        {
            if (Root == null)
            {
                return local;
            }

            var room = Root.parent != null ? Root.parent : Root;
            Vector3 from = room.TransformPoint(local + Vector3.up * above);
            RaycastHit hit;
            if (Physics.Raycast(from, Vector3.down, out hit, above + below, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore))
            {
                return room.InverseTransformPoint(hit.point);
            }

            return local;
        }

        /// <summary>The level data is missing or has no spawns: a bare 24 x 24 m floor so nobody falls, the console on it.</summary>
        private void Fallback()
        {
            var at = -Area.Anchor;
            RrBuild.Floor(Root, "Fallback Floor", at, new Vector2(24f, 24f), RrLook.Solid(new Color(0.16f, 0.12f, 0.24f)));
            Console = new Pose(at + new Vector3(0f, 0f, -6f), Quaternion.identity);
            Bounds = new Bounds(at, new Vector3(24f, 20f, 24f));
            for (int t = 0; t < 2; t++)
            {
                if (MatchStart[t].Count == 0)
                {
                    var p = new Pose(at + new Vector3(t == 0 ? -8f : 8f, 0.05f, 0f), Quaternion.Euler(0f, t == 0 ? 90f : -90f, 0f));
                    MatchStart[t].Add(p);
                    Respawns[t].Add(p);
                }
            }

            Lobby.Clear();
            for (int i = 0; i < 8; i++)
            {
                Lobby.Add(new Pose(at + new Vector3((i % 4 - 1.5f) * 1.1f, 0.05f, -2f + (i / 4) * 1.5f), Quaternion.Euler(0f, 180f, 0f)));
            }
        }
    }
}
