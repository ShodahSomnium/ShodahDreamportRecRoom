using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.RecRally
{
    /// <summary>
    /// Rec Rally's one course, Chaparral in Pioneer Peak (Android level107, RecRally_Chaparral.unity), built from
    /// Data/RecRally_Chaparral.bytes through <see cref="RrJumbotronArea"/>. The data is gtscene jt=1 with the terrain's
    /// collider over the whole 500 x 500 m heightmap (2 m cells), the vegetation plates and the six parked grid karts left
    /// out, thinned to ~39k plates (DN/iter15/stuntrally.md). Scene space is room space.
    ///
    /// What the level gives (G markers):
    /// - PlayGame/Team1..6SpawnPoint: the six grid slots (facing +z, 5 m apart in two rows);
    /// - PreGame/Player1..8SpawnPoint: the holding area (lobby), PostGame/Player1SpawnPoint (First/Second/Third place):
    ///   the winners' podium, (ScoreboardProjector): the lobby's board;
    /// - LapTriggerCollider1..12 (3a/3b, 4a/4b and 12a/12b are the two ways round a split) and LapFinishTriggerCollider
    ///   (244 x 143 x 34 m across the start straight): a lap is the twelve gates in order, then the finish;
    /// - VisualRoot 2 m triggers = the 75 nitro cans ([RecRally_Pickup]; NitroBoost / NitroBoost_Outline / CubeGizmo meshes,
    ///   left out of the kit - the room spawns (UGC_RecRally_Pickup_Sandbox) there);
    /// - KillZone (2000 x 63 x 2000 m under the map).
    /// </summary>
    public sealed class RrRecRallyTrack
    {
        public const string Scene = "RecRally_Chaparral";
        public const int Gates = 12;

        public readonly RrJumbotronArea Area = new RrJumbotronArea(Scene, Vector3.zero);
        public Transform Root;
        public bool Done;
        public bool Ok;

        public readonly List<Pose> Grid = new List<Pose>();
        public readonly List<Pose> Lobby = new List<Pose>();
        public readonly List<Pose> Podium = new List<Pose>();
        public Pose Console;
        public bool HasConsole;

        /// <summary>The gate boxes by gate number 1..12 (index 0 unused); a gate with two boxes is a split.</summary>
        public readonly List<Box>[] GateBoxes = new List<Box>[Gates + 1];

        public Box Finish;
        public bool HasFinish;
        public readonly List<Vector3> Pickups = new List<Vector3>();
        public float KillY = -40f;
        public Bounds Bounds;

        public struct Box
        {
            public Vector3 Centre;
            public Quaternion Rotation;
            public Vector3 Half;

            public bool Contains(Vector3 p)
            {
                var l = Quaternion.Inverse(Rotation) * (p - Centre);
                return Mathf.Abs(l.x) <= Half.x && Mathf.Abs(l.y) <= Half.y && Mathf.Abs(l.z) <= Half.z;
            }
        }

        public RrRecRallyTrack()
        {
            for (int i = 0; i <= Gates; i++)
            {
                GateBoxes[i] = new List<Box>();
            }

            Area.Skip.Add("NitroBoost");
            Area.Skip.Add("NitroBoost_Outline");
            Area.Skip.Add("CubeGizmo_LOD0");
        }

        public IEnumerator Build(Transform room, Dictionary<string, Material> surfaces)
        {
            float t0 = Time.realtimeSinceStartup;
            Root = RrBuild.Group(room, "Rec Rally Chaparral").transform;
            string text = RrSceneBank.Text(Scene);
            if (string.IsNullOrEmpty(text))
            {
                RrLog.Warn("RecRally", Scene + ": the scene does not carry Data/" + Scene + ".bytes (RrSceneBank); a flat stand-in oval is built");
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
            int gates = 0;
            for (int i = 1; i <= Gates; i++)
            {
                if (GateBoxes[i].Count > 0)
                {
                    gates++;
                }
            }

            Ok = Grid.Count > 0 && HasFinish && gates == Gates;
            if (!Ok)
            {
                RrLog.Warn("RecRally", "the level's markers are incomplete (grid " + Grid.Count + ", gates " + gates + "/" + Gates + ", finish " + HasFinish + "): the stand-in oval is used");
                Fallback();
            }

            Done = true;
            RrLog.Info("RecRally", "Chaparral built in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s: grid " + Grid.Count + ", lobby spawns " + Lobby.Count + ", podium " + Podium.Count
                                   + ", gates " + gates + ", nitro cans " + Pickups.Count + ", console " + (HasConsole ? Console.position.ToString("F1") : "none") + ", bounds " + Bounds.min.ToString("F0") + " .. " + Bounds.max.ToString("F0"));
        }

        private void Read()
        {
            var grid = new SortedDictionary<string, Pose>();
            var lobby = new SortedDictionary<string, Pose>();
            Pose first = default(Pose), second = default(Pose), third = default(Pose);
            int podium = 0;
            foreach (var m in Area.Markers("spawn"))
            {
                string name = m.Name ?? "";
                string path = m.Get("path", "");
                var pose = new Pose(m.Position, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f));
                if (path.Contains("PlayGame") || name.StartsWith("Team"))
                {
                    grid[name] = pose;
                }
                else if (path.Contains("PreGame"))
                {
                    lobby[name] = new Pose(pose.position + Vector3.up * 0.05f, pose.rotation);
                }
                else if (path.Contains("PostGame"))
                {
                    podium |= name.Contains("First") ? 1 : name.Contains("Second") ? 2 : name.Contains("Third") ? 4 : 0;
                    if (name.Contains("First"))
                    {
                        first = pose;
                    }
                    else if (name.Contains("Second"))
                    {
                        second = pose;
                    }
                    else if (name.Contains("Third"))
                    {
                        third = pose;
                    }
                }
            }

            Grid.AddRange(grid.Values);
            Lobby.AddRange(lobby.Values);
            if (podium == 7)
            {
                Podium.Add(first);
                Podium.Add(second);
                Podium.Add(third);
            }

            foreach (var m in Area.Markers("trigger_zone"))
            {
                string name = m.Name ?? "";
                if (name == "VisualRoot")
                {
                    Pickups.Add(m.Position);
                    continue;
                }

                if (!name.StartsWith("Lap"))
                {
                    continue;
                }

                var box = new Box { Centre = m.Position, Rotation = m.Rotation, Half = Size(m) * 0.5f };
                if (name.StartsWith("LapFinish"))
                {
                    Finish = box;
                    HasFinish = true;
                    continue;
                }

                // LapTriggerCollider<number><a|b>
                const string prefix = "LapTriggerCollider";
                if (!name.StartsWith(prefix))
                {
                    continue;
                }

                string digits = "";
                foreach (char c in name.Substring(prefix.Length))
                {
                    if (char.IsDigit(c))
                    {
                        digits += c;
                    }
                    else
                    {
                        break;
                    }
                }

                int gate;
                if (int.TryParse(digits, out gate) && gate >= 1 && gate <= Gates)
                {
                    GateBoxes[gate].Add(box);
                }
            }

            foreach (var m in Area.Markers("kill_zone"))
            {
                var size = Size(m);
                if (size.y > 0f)
                {
                    KillY = m.Position.y + size.y * 0.5f;
                }
            }

            var boards = Area.Markers("scoreboard");
            if (boards.Count > 0)
            {
                Console = new Pose(boards[0].Position, Quaternion.Euler(0f, boards[0].Rotation.eulerAngles.y, 0f));
                HasConsole = true;
            }

            Bounds = Area.Area();
        }

        private static Vector3 Size(RrSceneMarker m)
        {
            string s = m.Get("size", null);
            if (string.IsNullOrEmpty(s))
            {
                return Vector3.zero;
            }

            var p = s.Split(',');
            float x = 0f, y = 0f, z = 0f;
            if (p.Length >= 3)
            {
                float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
            }

            return new Vector3(x, y, z);
        }

        /// <summary>The ground under a room-local point, else the point.</summary>
        public Vector3 Ground(Vector3 local, float above, float below)
        {
            if (Root == null)
            {
                return local;
            }

            var room = Root.parent != null ? Root.parent : Root;
            RaycastHit hit;
            if (Physics.Raycast(room.TransformPoint(local + Vector3.up * above), Vector3.down, out hit, above + below, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore))
            {
                return room.InverseTransformPoint(hit.point);
            }

            return local;
        }

        /// <summary>No data or incomplete markers: a flat 200 x 120 m stand-in with an oval of twelve gates.</summary>
        private void Fallback()
        {
            Grid.Clear();
            Lobby.Clear();
            Podium.Clear();
            Pickups.Clear();
            for (int i = 0; i <= Gates; i++)
            {
                GateBoxes[i].Clear();
            }

            var at = new Vector3(0f, 0f, 400f);
            RrBuild.Floor(Root, "Fallback Ground", at, new Vector2(240f, 160f), RrLook.Solid(new Color(0.72f, 0.56f, 0.38f)), 0.5f);
            for (int i = 0; i < 6; i++)
            {
                Grid.Add(new Pose(at + new Vector3(-80f + (i % 2) * 6f, 0f, -10f - (i / 2) * 7f), Quaternion.identity));
            }

            for (int i = 0; i < 8; i++)
            {
                Lobby.Add(new Pose(at + new Vector3(-100f, 0.05f, (i - 3.5f) * 1.2f), Quaternion.Euler(0f, 90f, 0f)));
            }

            for (int g = 1; g <= Gates; g++)
            {
                float a = g / (float)(Gates + 1) * Mathf.PI * 2f;
                var c = at + new Vector3(-Mathf.Cos(a) * 80f, 5f, Mathf.Sin(a) * 50f);
                GateBoxes[g].Add(new Box { Centre = c, Rotation = Quaternion.identity, Half = new Vector3(30f, 10f, 30f) });
            }

            Finish = new Box { Centre = at + new Vector3(-80f, 5f, 8f), Rotation = Quaternion.identity, Half = new Vector3(20f, 10f, 4f) };
            HasFinish = true;
            Console = new Pose(at + new Vector3(-96f, 0f, 6f), Quaternion.Euler(0f, -90f, 0f));
            HasConsole = true;
            Bounds = new Bounds(at, new Vector3(240f, 40f, 160f));
            KillY = -20f;
        }
    }
}
