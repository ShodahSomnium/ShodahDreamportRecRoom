using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// One of Rec Room's seven final Paintball maps (or the lobby), read from its own level: the static data of
    /// <see cref="RrPaintballMaps"/> plus what <see cref="FromMarkers"/> takes from the built level's markers. All
    /// poses are room-local: the level is built with its anchor (the midpoint of the two flag bases) at
    /// <see cref="Slot"/>, so room-local = Slot + (scene point - Anchor).
    ///
    /// Teams: team 0 is Blue and defends the flag base the level files under "Blue", team 1 Red. The levels name
    /// their spawn groups Team1SpawnPoint / Team2SpawnPoint, but which group belongs to which base differs per map
    /// (on River Team1 is by the Blue base, on Drive-In by the Red one), so each team gets the group whose centre is
    /// nearest its own flag base. Rec Room's 2020 rule then picks, at every respawn, the team spawn farthest from
    /// the nearest opponent (<see cref="RrPaintballRoom.RespawnPoseFor"/>).
    /// </summary>
    public sealed class RrPaintballMap
    {
        public int Index;
        public string Name;
        public string Key;

        /// <summary>Gameplay music (the map's own track where the bundles have one).</summary>
        public string Music;

        /// <summary>The lobby radio station of this map (PaintballLobby_*Radio; none for Merry Midway).</summary>
        public string Radio;

        /// <summary>Room-local position the level's anchor is built at.</summary>
        public Vector3 Slot;

        /// <summary>Scene point that goes to <see cref="Slot"/>.</summary>
        public Vector3 Anchor;

        /// <summary>The built level (null until built; destroyed when another map is chosen).</summary>
        public Transform Root;

        public bool Built;

        /// <summary>Room-local centre of the play area (the flag midpoint) and the half size of the kill floor.</summary>
        public Vector3 Center;

        public Vector2 HalfSize = new Vector2(60f, 80f);

        /// <summary>Room-local height below which a player has fallen off (the level's KillFloor top), or -inf.</summary>
        public float KillY = float.NegativeInfinity;

        public readonly List<Pose>[] Spawns = { new List<Pose>(), new List<Pose>() };
        public readonly List<Pose> Pregame = new List<Pose>();
        public readonly Vector3[] FlagStands = new Vector3[2];
        public readonly Quaternion[] FlagRotations = { Quaternion.identity, Quaternion.identity };

        /// <summary>Room-local pose of each team's (FlagTool) at home, as the level places it on its stand (NaN x: none).</summary>
        public readonly Vector3[] FlagHomes = { new Vector3(float.NaN, 0f, 0f), new Vector3(float.NaN, 0f, 0f) };

        public readonly Quaternion[] FlagHomeRotations = { Quaternion.identity, Quaternion.identity };

        /// <summary>The level's own weapon spots (set "Weapons", the one active in a normal match), room-local.</summary>
        public readonly List<Weapon> Weapons = new List<Weapon>();

        /// <summary>Base scoreboards (the levels' Scoreboard_V2 boards), room-local centre, rotation, half size.</summary>
        public readonly List<Pose> Boards = new List<Pose>();

        public readonly List<Vector3> BoardSizes = new List<Vector3>();

        /// <summary>Spectator cameras of the level (harness overview shots).</summary>
        public readonly List<Pose> Spectators = new List<Pose>();

        public struct Weapon
        {
            public string Prefab;
            public string Set;
            public Pose Pose;
        }

        public Pose SpawnPose(int team, int index)
        {
            var list = Spawns[Mathf.Clamp(team, 0, 1)];
            if (list.Count == 0)
            {
                return new Pose(Center, Quaternion.identity);
            }

            return list[((index % list.Count) + list.Count) % list.Count];
        }

        public bool Contains(Vector3 roomLocal, float margin = 0f)
        {
            return Mathf.Abs(roomLocal.x - Center.x) <= HalfSize.x + margin && Mathf.Abs(roomLocal.z - Center.z) <= HalfSize.y + margin;
        }

        /// <summary>Scene point to room-local.</summary>
        public Vector3 Local(Vector3 scene)
        {
            return Slot + (scene - Anchor);
        }

        /// <summary>Fills the gameplay data from the kit's markers (their positions are in the level container's space).</summary>
        public void FromMarkers(IList<RrSceneMarker> markers)
        {
            for (int t = 0; t < 2; t++)
            {
                Spawns[t].Clear();
            }

            Pregame.Clear();
            Weapons.Clear();
            Boards.Clear();
            BoardSizes.Clear();
            Spectators.Clear();
            var group1 = new List<Pose>();
            var group2 = new List<Pose>();
            bool[] haveFlag = new bool[2];
            foreach (var m in markers)
            {
                Vector3 p = Slot + m.Position;
                var pose = new Pose(p, Flat(m.Rotation));
                switch (m.Kind)
                {
                    case "spawn":
                        if (m.Get("team") == "pregame" || m.Name.StartsWith("Player"))
                        {
                            Pregame.Add(pose);
                        }
                        else if (m.Name.StartsWith("Team1"))
                        {
                            group1.Add(pose);
                        }
                        else if (m.Name.StartsWith("Team2"))
                        {
                            group2.Add(pose);
                        }

                        break;
                    case "flag_base":
                    {
                        int t = m.Get("team") == "red" ? 1 : 0;
                        FlagStands[t] = p;
                        FlagRotations[t] = Flat(m.Rotation);
                        haveFlag[t] = true;
                        break;
                    }
                    case "flag":
                    {
                        int t = m.Get("team") == "red" ? 1 : 0;
                        FlagHomes[t] = p;
                        FlagHomeRotations[t] = m.Rotation;
                        break;
                    }
                    case "weapon_pickup":
                    {
                        string set = m.Get("set", "");
                        string prefab = m.Get("prefab");
                        if (!string.IsNullOrEmpty(prefab))
                        {
                            Weapons.Add(new Weapon { Prefab = prefab, Set = set, Pose = new Pose(p, m.Rotation) });
                        }

                        break;
                    }
                    case "kill_zone":
                        if (m.Name.StartsWith("KillFloor"))
                        {
                            Vector3 size = RrPaintballDress.Vec(m.Get("size"), Vector3.zero);
                            Vector3 w = new Vector3(Mathf.Abs((m.Rotation * new Vector3(size.x, 0f, 0f)).x) + Mathf.Abs((m.Rotation * new Vector3(0f, size.y, 0f)).x) + Mathf.Abs((m.Rotation * new Vector3(0f, 0f, size.z)).x),
                                                    Mathf.Abs((m.Rotation * new Vector3(size.x, 0f, 0f)).y) + Mathf.Abs((m.Rotation * new Vector3(0f, size.y, 0f)).y) + Mathf.Abs((m.Rotation * new Vector3(0f, 0f, size.z)).y),
                                                    Mathf.Abs((m.Rotation * new Vector3(size.x, 0f, 0f)).z) + Mathf.Abs((m.Rotation * new Vector3(0f, size.y, 0f)).z) + Mathf.Abs((m.Rotation * new Vector3(0f, 0f, size.z)).z));
                            KillY = p.y + w.y * 0.5f;
                            if (w.x > 10f && w.z > 10f)
                            {
                                HalfSize = new Vector2(w.x * 0.5f, w.z * 0.5f);
                            }
                        }

                        break;
                    case "scoreboard_v2":
                        Boards.Add(new Pose(p, m.Rotation));
                        BoardSizes.Add(RrPaintballDress.Vec(m.Get("e"), new Vector3(2f, 1.2f, 0.1f)));
                        break;
                    case "spectator":
                        Spectators.Add(new Pose(p, m.Rotation));
                        break;
                }
            }

            if (haveFlag[0] && haveFlag[1])
            {
                Center = (FlagStands[0] + FlagStands[1]) * 0.5f;
            }

            // Each team takes the spawn group nearest its own base.
            Vector3 c1 = Mean(group1), c2 = Mean(group2);
            bool swap = group1.Count > 0 && group2.Count > 0 &&
                        (Flat3(c1 - FlagStands[0]).sqrMagnitude + Flat3(c2 - FlagStands[1]).sqrMagnitude) >
                        (Flat3(c2 - FlagStands[0]).sqrMagnitude + Flat3(c1 - FlagStands[1]).sqrMagnitude);
            Spawns[0].AddRange(swap ? group2 : group1);
            Spawns[1].AddRange(swap ? group1 : group2);
        }

        private static Vector3 Mean(List<Pose> list)
        {
            if (list.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 s = Vector3.zero;
            foreach (var p in list)
            {
                s += p.position;
            }

            return s / list.Count;
        }

        private static Vector3 Flat3(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>Only the yaw of a rotation (spawn points face along their +z).</summary>
        public static Quaternion Flat(Quaternion q)
        {
            Vector3 f = q * Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up) : Quaternion.identity;
        }
    }
}
