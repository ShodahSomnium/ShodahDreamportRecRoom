using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.DiscGolf
{
    /// <summary>
    /// One Disc Golf course built from Rec Room's own Android level (<see cref="RrDiscGolfCourses"/>) through The Rise of
    /// Jumbotron's <see cref="RrJumbotronArea"/> (install meshes where the install has them - the baskets, rocks, the
    /// door -, measured plates elsewhere, the level's colliders, terrain grids and lights). Everything is under
    /// <see cref="Root"/>, so <see cref="Destroy"/> frees the whole course.
    ///
    /// What the level gives besides the table's tees and baskets (G markers in the data, DN/iter15/discgolf.md):
    /// - the pregame spawns (team=pregame) = the lobby arrivals, and the (ScoreboardProjector) nearest to them = the
    ///   game console; the other projectors stand at the tees (each tee's scorecard goes on the nearest one);
    /// - hazards: the Lake's water (Static/Lake/KillVolume boxes) and kill floor, Propulsion's red hazard panels
    ///   ([DiscGolfHazardGizmo*] boxes) and both levels' tall unnamed "Cube" boxes around the course (out of bounds,
    ///   GUESS): a disc that touches one is out;
    /// - Propulsion's fans ([DiscGolfFanGizmo*]: the wind capsule; it blows from the fan's face through the capsule);
    /// - the bonus panels ([DiscGolfPowerupGizmo*]: a Gizmo_Face trigger that belongs to no fan or hazard): a disc that
    ///   flies through one takes a stroke off its hole.
    /// </summary>
    public sealed class RrDiscGolfLevel
    {
        /// <summary>An oriented box (room-local).</summary>
        public struct Box
        {
            public Vector3 Centre;
            public Quaternion Rotation;
            public Vector3 Half;

            public bool Contains(Vector3 p, float margin)
            {
                Vector3 l = Quaternion.Inverse(Rotation) * (p - Centre);
                return Mathf.Abs(l.x) <= Half.x + margin && Mathf.Abs(l.y) <= Half.y + margin && Mathf.Abs(l.z) <= Half.z + margin;
            }
        }

        /// <summary>A fan's wind: a capsule from <see cref="From"/> along <see cref="Axis"/> (room-local).</summary>
        public struct Fan
        {
            public Vector3 From;
            public Vector3 Axis;
            public float Length;
            public float Radius;

            public bool Contains(Vector3 p)
            {
                Vector3 d = p - From;
                float along = Vector3.Dot(d, Axis);
                if (along < -Radius || along > Length + Radius)
                {
                    return false;
                }

                return (d - Axis * Mathf.Clamp(along, 0f, Length)).sqrMagnitude <= Radius * Radius;
            }
        }

        /// <summary>A bonus panel: a square face (room-local centre, rotation, half size) the disc must fly through.</summary>
        public struct Panel
        {
            public Vector3 Centre;
            public Quaternion Rotation;
            public float Half;

            /// <summary>Whether the move from a to b passes through the face.</summary>
            public bool Crossed(Vector3 a, Vector3 b)
            {
                var inv = Quaternion.Inverse(Rotation);
                Vector3 la = inv * (a - Centre), lb = inv * (b - Centre);
                if (Mathf.Sign(la.z) == Mathf.Sign(lb.z) || Mathf.Abs(la.z - lb.z) < 1e-5f)
                {
                    return false;
                }

                float t = la.z / (la.z - lb.z);
                Vector3 c = Vector3.Lerp(la, lb, t);
                return Mathf.Abs(c.x) <= Half + 0.15f && Mathf.Abs(c.y) <= Half + 0.15f;
            }
        }

        public readonly int Course;
        public readonly RrJumbotronArea Area;
        public Transform Root;
        public bool Done;
        public bool FromData;

        /// <summary>Per hole (room-local): the tee centre on the floor facing the basket, the basket's foot, the scorecard's spot.</summary>
        public readonly Pose[] Tees = new Pose[RrDiscGolfCourses.Holes];
        public readonly Vector3[] Goals = new Vector3[RrDiscGolfCourses.Holes];
        public readonly Pose[] TeeBoards = new Pose[RrDiscGolfCourses.Holes];

        public readonly List<Box> Hazards = new List<Box>();

        /// <summary>How many of <see cref="Hazards"/> are the levels' out-of-bounds boxes.</summary>
        public int OutOfBounds;

        public readonly List<Fan> Fans = new List<Fan>();
        public readonly List<Panel> Bonuses = new List<Panel>();

        /// <summary>The lobby's projector floor point and facing, and the arrivals (room-local).</summary>
        public Pose Console = new Pose(Vector3.zero, Quaternion.identity);
        public readonly List<Pose> Lobby = new List<Pose>();

        /// <summary>The course area (room-local x0, z0, x1, z1) and the height under which a disc is out.</summary>
        public Vector4 CourseArea;
        public float OutBelow;

        /// <summary>What the room made for this course (destroyed with <see cref="Root"/>).</summary>
        public RrGameConsole Hologram;

        public RrDiscGolfLevel(int course)
        {
            Course = RrDiscGolfCourses.Clamp(course);
            Area = new RrJumbotronArea(RrDiscGolfCourses.Scenes[Course], RrDiscGolfCourses.Anchors[Course]);
        }

        public string Name
        {
            get { return RrDiscGolfCourses.Names[Course]; }
        }

        public string Scene
        {
            get { return RrDiscGolfCourses.Scenes[Course]; }
        }

        public bool Alive
        {
            get { return Root != null; }
        }

        /// <summary>Scene point to room-local.</summary>
        public Vector3 L(Vector3 scene)
        {
            return Area.L(scene);
        }

        // ------------------------------------------------------------------ build

        public IEnumerator Build(Transform room, Dictionary<string, Material> surfaces)
        {
            float t0 = Time.realtimeSinceStartup;
            Root = RrBuild.Group(room, "Course " + Name).transform;
            string text = RrSceneBank.Text(Scene);
            if (string.IsNullOrEmpty(text))
            {
                RrLog.Warn("DiscGolf", Scene + ": the scene does not carry Data/" + Scene + ".bytes (RrSceneBank); plain greens and baskets stand in");
                Fallback();
                Read();
                Done = true;
                yield break;
            }

            // Our own door stands where the level's does.
            foreach (var n in new[] { "ActivityDoorSingle_Frame", "ActivityDoor_Glow", "ActivityDoor_Emit", "ActivityDoor_Frame", "ActivityDoor_Left", "ActivityDoor_Right" })
            {
                Area.Skip.Add(n);
            }

            yield return Area.Build(text, Root, surfaces);
            if (Root == null)
            {
                Done = true;
                yield break;
            }

            FromData = true;
            Physics.SyncTransforms();
            Read();
            Done = true;
            RrLog.Info("DiscGolf", Name + " built in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s: " + Lobby.Count + " lobby arrivals, console at " + Console.position.ToString("F1") + ", " +
                                   Hazards.Count + " hazard volumes (" + OutOfBounds + " out of bounds), " + Fans.Count + " fans, " + Bonuses.Count + " bonus panels; hole 1 tee " + Tees[0].position.ToString("F1") + " -> basket " +
                                   Goals[0].ToString("F1") + " (" + Vector3.Distance(Tees[0].position, Goals[0]).ToString("0") + " m), par " + RrDiscGolfCourses.TotalPar(Course));
        }

        public void Destroy()
        {
            if (Root != null)
            {
                Object.Destroy(Root.gameObject);
            }

            Root = null;
            Hologram = null;
        }

        // ------------------------------------------------------------------ markers

        private void Read()
        {
            int c = Course;
            var area = RrDiscGolfCourses.Areas[c];
            Vector3 a0 = L(new Vector3(area.x, 0f, area.y)), a1 = L(new Vector3(area.z, 0f, area.w));
            CourseArea = new Vector4(a0.x, a0.z, a1.x, a1.z);
            float lowest = float.MaxValue;
            for (int h = 0; h < RrDiscGolfCourses.Holes; h++)
            {
                Vector3 goal = L(RrDiscGolfCourses.Goals[c][h]);
                Goals[h] = goal;
                Vector3 tee = Floor(L(RrDiscGolfCourses.Tees[c][h]), 2.5f, 4f);
                Vector3 to = goal - tee;
                to.y = 0f;
                var face = to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to.normalized, Vector3.up) : Quaternion.identity;
                Tees[h] = new Pose(tee, face);
                lowest = Mathf.Min(lowest, Mathf.Min(tee.y, goal.y));
            }

            OutBelow = lowest - 15f;
            var boards = new List<RrSceneMarker>();
            var spawns = new List<Pose>();
            if (FromData)
            {
                foreach (var m in Area.Markers("scoreboard"))
                {
                    if (m.Get("prefab", "") == "(ScoreboardProjector)")
                    {
                        boards.Add(m);
                    }
                }

                foreach (var m in Area.Markers("spawn"))
                {
                    if (m.Get("team", "") == "pregame")
                    {
                        spawns.Add(new Pose(Floor(m.Position, 1f, 2.5f) + Vector3.up * 0.05f, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f)));
                    }
                }

                ReadHazards();
            }

            // The lobby: the pregame spawns, else around the first tee.
            Lobby.Clear();
            Lobby.AddRange(spawns);
            Vector3 centre = Vector3.zero;
            if (Lobby.Count > 0)
            {
                foreach (var p in Lobby)
                {
                    centre += p.position;
                }

                centre /= Lobby.Count;
            }
            else
            {
                centre = Tees[0].position - Tees[0].rotation * Vector3.forward * 4f;
            }

            // The console: the projector nearest the arrivals (Lake: by hole 1's tee; Propulsion: on the lawn north of them).
            RrSceneMarker best = null;
            float bestD = float.MaxValue;
            foreach (var m in boards)
            {
                float d = (m.Position - centre).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = m;
                }
            }

            Console = best != null
                ? new Pose(best.Position, Quaternion.Euler(0f, best.Rotation.eulerAngles.y, 0f))
                : new Pose(centre + Vector3.forward * 4f, Quaternion.Euler(0f, 180f, 0f));
            if (Lobby.Count == 0)
            {
                var fwd = Console.rotation * Vector3.forward;
                var right = Console.rotation * Vector3.right;
                var faceIt = Quaternion.Euler(0f, Console.rotation.eulerAngles.y + 180f, 0f);
                for (int i = 0; i < 8; i++)
                {
                    var p = Console.position + fwd * (3.5f + (i / 4) * 1.5f) + right * ((i % 4 - 1.5f) * 1.1f);
                    Lobby.Add(new Pose(Floor(p + Vector3.up * 0.3f, 2f, 4f) + Vector3.up * 0.05f, faceIt));
                }
            }

            // Each tee's scorecard: on the projector by it (within 15 m), else beside the tee.
            for (int h = 0; h < RrDiscGolfCourses.Holes; h++)
            {
                var tee = Tees[h];
                RrSceneMarker near = null;
                float nd = 15f * 15f;
                foreach (var m in boards)
                {
                    // The console's projector carries the hologram: hole 1's card goes beside its tee instead.
                    if (m == best)
                    {
                        continue;
                    }

                    float d = (m.Position - tee.position).sqrMagnitude;
                    if (d < nd)
                    {
                        nd = d;
                        near = m;
                    }
                }

                if (near != null)
                {
                    // Facing the tee.
                    Vector3 look = tee.position - near.Position;
                    look.y = 0f;
                    var rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(-look.normalized, Vector3.up) : near.Rotation;
                    TeeBoards[h] = new Pose(near.Position + Vector3.up * 2.4f, rot);
                }
                else
                {
                    Vector3 side = tee.rotation * Vector3.right;
                    Vector3 at = tee.position + side * 3f + tee.rotation * Vector3.forward * 1f;
                    TeeBoards[h] = new Pose(at + Vector3.up * 2.4f, Quaternion.LookRotation(side, Vector3.up));
                }
            }
        }

        private void ReadHazards()
        {
            Hazards.Clear();
            OutOfBounds = 0;
            Fans.Clear();
            Bonuses.Clear();
            foreach (var m in Area.Markers("kill_zone"))
            {
                Vector3 size;
                if (m.Get("shape", "") == "box" && Size(m, out size))
                {
                    Hazards.Add(new Box { Centre = m.Position, Rotation = m.Rotation, Half = size * 0.5f });
                }
            }

            var faces = new List<RrSceneMarker>();
            var hazardFaces = new List<Vector3>();
            foreach (var m in Area.Markers("trigger_zone"))
            {
                string n = m.Name ?? "";
                if (n == "Gizmo_Face")
                {
                    faces.Add(m);
                    continue;
                }

                Vector3 size;
                if (n.StartsWith("Cube") && m.Get("shape", "") == "box" && Size(m, out size))
                {
                    // The levels' unnamed tall boxes around (and in a few gaps of) the course: out of bounds (GUESS; no tee,
                    // basket, bonus panel or lobby spawn of either level is inside one - checked offline).
                    Hazards.Add(new Box { Centre = m.Position, Rotation = m.Rotation, Half = size * 0.5f });
                    OutOfBounds++;
                }
                else if (n.StartsWith("[DiscGolfHazardGizmo") && Size(m, out size))
                {
                    Hazards.Add(new Box { Centre = m.Position, Rotation = m.Rotation, Half = size * 0.5f });
                    hazardFaces.Add(m.Position);
                }
                else if (n.StartsWith("[DiscGolfFanGizmo") && m.Get("shape", "") == "capsule")
                {
                    float r = m.GetFloat("r", 1.12f), h = m.GetFloat("h", 10f);
                    // Until its face is found: the capsule centre, and Length = half the capsule.
                    Fans.Add(new Fan { From = m.Position, Axis = Vector3.zero, Length = h * 0.5f, Radius = r });
                }
            }

            // A fan's wind capsule sits along its face's normal (Hole 6's 3 x 3 fan: centre 4.45 m out along the normal, h 18.8):
            // the wind blows from the face (the Gizmo_Face whose normal line passes through the capsule) through the
            // capsule's centre to its far end; the half behind the face is left out.
            var used = new bool[faces.Count];
            for (int i = 0; i < Fans.Count; i++)
            {
                var f = Fans[i];
                Vector3 centre = f.From;
                float half = f.Length;
                int bestFace = -1;
                float bestScore = float.MaxValue;
                for (int k = 0; k < faces.Count; k++)
                {
                    Vector3 d = centre - faces[k].Position;
                    float dist = d.magnitude;
                    if (dist > half + 2f)
                    {
                        continue;
                    }

                    Vector3 n = faces[k].Rotation * Vector3.forward;
                    float off = dist < 0.01f ? 0f : (d - n * Vector3.Dot(d, n)).magnitude;
                    float score = off + dist * 0.01f;
                    if (off <= f.Radius + 0.5f && score < bestScore)
                    {
                        bestScore = score;
                        bestFace = k;
                    }
                }

                if (bestFace >= 0)
                {
                    used[bestFace] = true;
                    Vector3 from = faces[bestFace].Position;
                    Vector3 d = centre - from;
                    f.From = from;
                    f.Axis = d.sqrMagnitude > 0.04f ? d.normalized : faces[bestFace].Rotation * Vector3.forward;
                    f.Length = d.magnitude + half;
                }
                else
                {
                    // No face: the whole capsule, blowing up (GUESS).
                    f.From = centre - Vector3.up * half;
                    f.Axis = Vector3.up;
                    f.Length = half * 2f;
                }

                Fans[i] = f;
            }

            for (int k = 0; k < faces.Count; k++)
            {
                if (used[k])
                {
                    continue;
                }

                bool hazard = false;
                foreach (var hp in hazardFaces)
                {
                    hazard |= (hp - faces[k].Position).sqrMagnitude < 1f;
                }

                Vector3 size;
                if (hazard || !Size(faces[k], out size))
                {
                    continue;
                }

                Bonuses.Add(new Panel { Centre = faces[k].Position, Rotation = faces[k].Rotation, Half = Mathf.Max(size.x, size.y) * 0.5f });
            }
        }

        private static bool Size(RrSceneMarker m, out Vector3 size)
        {
            size = Vector3.zero;
            string s = m.Get("size");
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            var p = s.Split(',');
            float x, y, z;
            if (p.Length == 3 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                size = new Vector3(x, y, z);
                return true;
            }

            return false;
        }

        /// <summary>The floor under a room-local point (searched from <paramref name="above"/> over it to <paramref name="below"/> under it), else the point.</summary>
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

        /// <summary>The level data is missing: a green pad under every tee and basket, a strip between them, plain baskets.</summary>
        private void Fallback()
        {
            int c = Course;
            var grass = RrLook.Solid(RrLook.Grass);
            var pole = RrLook.Solid(RrLook.Grey);
            var orange = RrLook.Solid(RrLook.Orange);
            for (int h = 0; h < RrDiscGolfCourses.Holes; h++)
            {
                Vector3 tee = L(RrDiscGolfCourses.Tees[c][h]);
                Vector3 goal = L(RrDiscGolfCourses.Goals[c][h]);
                Vector3 mid = (tee + goal) * 0.5f;
                Vector3 d = goal - tee;
                d.y = 0f;
                float len = d.magnitude + 10f;
                var rot = d.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
                float y = Mathf.Min(tee.y, goal.y) - 0.1f;
                RrBuild.Box(Root, "Fairway " + (h + 1), new Vector3(mid.x, y, mid.z), rot, new Vector3(14f, 0.2f, len), grass);
                RrBuild.Box(Root, "Tee " + (h + 1), tee + Vector3.down * 0.05f, rot, new Vector3(3f, 0.1f, 2f), pole);
                RrBuild.Cylinder(Root, "Basket Pole " + (h + 1), goal + Vector3.up * 1.2f, 0.03f, 2.4f, pole);
                RrBuild.Cylinder(Root, "Basket Tray " + (h + 1), goal + Vector3.up * 0.66f, 0.32f, 0.26f, orange);
            }

            Vector3 door = L(RrDiscGolfCourses.Doors[c]);
            RrBuild.Floor(Root, "Lobby", door + new Vector3(0f, -0.1f, 0f), new Vector2(30f, 30f), grass);
            RrBuild.PointLight(Root, "Light", door + Vector3.up * 10f, Color.white, 80f, 1f);
        }
    }
}
