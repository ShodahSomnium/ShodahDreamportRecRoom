using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.StuntRunner
{
    /// <summary>
    /// One Stunt Runner level (the lobby or a course) built from Rec Room's own Android level through The Rise of
    /// Jumbotron's <see cref="RrJumbotronArea"/> (install meshes, stand-ins, colliders, lights, floors), and what its
    /// markers say, room-local (measured from DN/refdata/apk/StuntRunner_*.rrsk):
    /// - [StartingGate]/PlayerSpawns/Player1..4SpawnPoint: the four start lanes (2.03 m apart);
    /// - [Checkpoint_Scaffolding]*/Checkpoint/SpawnPoint: the checkpoint arches (the respawn point is the arch's floor);
    /// - [EndingGate]/Checkpoint/SpawnPoint and the finish button ([Button_StuntRunnerEnd] trigger, 0.82 x 1.88 x 0.85 m);
    /// - Killzone boxes (pits, pools, the floor and ceiling planes): back to the last checkpoint;
    /// - VisualRoot 1 m triggers = the KO Cola cans (Can / CanOutline / CubeGizmo meshes; the kit leaves them out and the
    ///   room spawns <c>(Stuntrunner_Pickup_Sandbox)</c> there);
    /// - install renderers SpringBoard_2x2/2x4 = bounce pads, Rope_Net_LOD0 = the climbing nets;
    /// - [CommentatorBooth]/PostRunSpawnPoints, [LeaderboardProjector] / [ScoreboardProjector] at the exit.
    /// Everything lives under <see cref="Root"/>.
    /// </summary>
    public sealed class RrStuntRunnerLevel
    {
        /// <summary>-1 = the lobby, 0..5 = the courses.</summary>
        public readonly int Course;

        public readonly string Scene;
        public readonly Vector3 Anchor;
        public readonly RrJumbotronArea Area;
        public Transform Root;
        public bool Done;
        public bool Ok;

        public readonly List<Pose> Starts = new List<Pose>();
        public readonly List<Pose> Checkpoints = new List<Pose>();
        public Pose StartCheckpoint;
        public Pose Finish;
        public Vector3 Button;
        public bool HasButton;
        public Pose PostRun;
        public Pose Board;
        public bool HasBoard;
        public readonly List<Kill> KillZones = new List<Kill>();
        public readonly List<Vector3> Pickups = new List<Vector3>();
        public readonly List<Pose> Pads = new List<Pose>();
        public readonly List<Bounds> Nets = new List<Bounds>();

        /// <summary>The lobby's arrival points (Player1..8SpawnPoint).</summary>
        public readonly List<Pose> Lobby = new List<Pose>();

        /// <summary>The level's extent (room-local).</summary>
        public Bounds Bounds;

        public struct Kill
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

        public RrStuntRunnerLevel(int course)
        {
            Course = course;
            Scene = course < 0 ? RrStuntRunnerCourses.LobbyScene : RrStuntRunnerCourses.Scenes[course];
            Anchor = course < 0 ? Vector3.zero : RrStuntRunnerCourses.Anchor(course);
            Area = new RrJumbotronArea(Scene, Anchor);
            // The KO Cola cans are the room's own pickups (they spin and vanish when taken).
            Area.Skip.Add("Can");
            Area.Skip.Add("CanOutline");
            Area.Skip.Add("CubeGizmo_LOD0");
        }

        public string Title
        {
            get { return Course < 0 ? "Lobby" : RrStuntRunnerCourses.Title(Course); }
        }

        public IEnumerator Build(Transform room, Dictionary<string, Material> surfaces)
        {
            float t0 = Time.realtimeSinceStartup;
            Root = RrBuild.Group(room, "Stunt Runner " + Title).transform;
            string text = RrSceneBank.Text(Scene);
            if (string.IsNullOrEmpty(text))
            {
                RrLog.Warn("StuntRunner", Scene + ": the scene does not carry Data/" + Scene + ".bytes (RrSceneBank); a plain stand-in is built");
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
            Ok = Course < 0 ? Lobby.Count > 0 : Starts.Count > 0 && (HasButton || Finish.position != Vector3.zero);
            if (!Ok)
            {
                Fallback();
            }

            Done = true;
            RrLog.Info("StuntRunner", Title + " built in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s: starts " + Starts.Count + ", checkpoints " + Checkpoints.Count
                                      + ", kill zones " + KillZones.Count + ", KO Colas " + Pickups.Count + ", bounce pads " + Pads.Count + ", net tiles " + Nets.Count
                                      + (HasButton ? ", finish button at " + Button.ToString("F1") : ", no finish button") + "; bounds " + Bounds.min.ToString("F0") + " .. " + Bounds.max.ToString("F0")
                                      + (Ok ? "" : " - NOT PLAYABLE (stand-in)"));
        }

        public void Destroy()
        {
            if (Root != null)
            {
                Object.Destroy(Root.gameObject);
            }

            Root = null;
        }

        // ------------------------------------------------------------------ markers

        private static Pose Flat(RrSceneMarker m)
        {
            return new Pose(m.Position, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f));
        }

        private void Read()
        {
            var starts = new SortedDictionary<string, Pose>();
            bool haveFinish = false, haveStartCp = false;
            foreach (var m in Area.Markers("spawn"))
            {
                string path = m.Get("path", "");
                string name = m.Name ?? "";
                if (Course < 0)
                {
                    if (name.StartsWith("Player"))
                    {
                        Lobby.Add(new Pose(Floor(m.Position + Vector3.up * 0.3f, 1f, 2f) + Vector3.up * 0.05f, Flat(m).rotation));
                    }

                    continue;
                }

                if (name.StartsWith("Player") && name.EndsWith("SpawnPoint"))
                {
                    starts[name] = new Pose(m.Position + Vector3.up * 0.05f, Flat(m).rotation);
                }
                else if (name.StartsWith("PostRun"))
                {
                    PostRun = new Pose(m.Position + Vector3.up * 0.05f, Flat(m).rotation);
                }
                else if (path.Contains("EndingGate"))
                {
                    Finish = Flat(m);
                    haveFinish = true;
                }
                else if (path.Contains("StartingGate"))
                {
                    StartCheckpoint = Flat(m);
                    haveStartCp = true;
                }
                else if (path.Contains("Checkpoint"))
                {
                    Checkpoints.Add(Flat(m));
                }
            }

            Starts.AddRange(starts.Values);
            if (!haveStartCp && Starts.Count > 0)
            {
                StartCheckpoint = Starts[0];
            }

            // The finish button: the biggest of the [Button_StuntRunnerEnd] trigger shapes (its press volume).
            float best = -1f;
            foreach (var m in Area.Markers("trigger_zone"))
            {
                string name = m.Name ?? "";
                var size = Size(m);
                if (name.Contains("Button_StuntRunnerEnd") || name.StartsWith("Button_Stuntrunner"))
                {
                    float v = size.x * size.y * size.z;
                    if (v > best)
                    {
                        best = v;
                        Button = m.Position;
                        HasButton = true;
                    }
                }
                else if (name == "VisualRoot")
                {
                    Pickups.Add(m.Position);
                }
            }

            foreach (var m in Area.Markers("kill_zone"))
            {
                if (m.Get("shape", "") != "box")
                {
                    continue;
                }

                var size = Size(m);
                if (size.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                KillZones.Add(new Kill { Centre = m.Position, Rotation = m.Rotation, Half = size * 0.5f });
            }

            var boards = Area.Markers("leaderboard");
            if (boards.Count == 0)
            {
                boards = Area.Markers("scoreboard");
            }

            if (boards.Count > 0)
            {
                Board = Flat(boards[0]);
                HasBoard = true;
            }

            if (!haveFinish && HasButton)
            {
                Finish = new Pose(Button, Quaternion.identity);
            }

            ReadArt();
            Bounds = Area.Area();
            if (Course < 0 && Lobby.Count == 0)
            {
                foreach (var p in Area.SpawnPoses())
                {
                    Lobby.Add(p);
                }
            }
        }

        /// <summary>Bounce pads and climbing nets from the install renderers the level places (scene space - anchor).</summary>
        private void ReadArt()
        {
            var d = Area.Data;
            if (d == null)
            {
                return;
            }

            var pads = new HashSet<int>();
            var nets = new HashSet<int>();
            for (int i = 0; i < d.Meshes.Count; i++)
            {
                string n = d.Meshes[i];
                if (n.StartsWith("SpringBoard_") && n.Contains("_Base"))
                {
                    pads.Add(i);
                }
                else if (n == "Rope_Net_LOD0")
                {
                    nets.Add(i);
                }
            }

            foreach (var r in d.Renderers)
            {
                if (pads.Contains(r.Mesh))
                {
                    Pads.Add(new Pose(r.Position - Anchor, r.Rotation));
                }
                else if (nets.Contains(r.Mesh))
                {
                    // The tile is 1 x 1 m (rr_party.md 2.3); its box, rotated and scaled, conservatively.
                    var b = new Bounds(r.Position - Anchor, Vector3.zero);
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = new Vector3((c & 1) == 0 ? -0.5f : 0.5f, (c & 2) == 0 ? -0.5f : 0.5f, (c & 4) == 0 ? -0.05f : 0.05f);
                        b.Encapsulate(r.Position - Anchor + r.Rotation * Vector3.Scale(corner, r.Scale));
                    }

                    Nets.Add(b);
                }
            }
        }

        private static Vector3 Size(RrSceneMarker m)
        {
            string s = m.Get("size", null);
            if (string.IsNullOrEmpty(s))
            {
                return Vector3.zero;
            }

            var p = s.Split(',');
            if (p.Length < 3)
            {
                return Vector3.zero;
            }

            float x, y, z;
            float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
            float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
            float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z);
            return new Vector3(x, y, z);
        }

        /// <summary>The floor under a room-local point, else the point.</summary>
        public Vector3 Floor(Vector3 local, float above, float below)
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

        /// <summary>No data (or no start lanes): a plain 12 x 60 m run with a finish button so the flow still works.</summary>
        private void Fallback()
        {
            // The lobby's spawn area, or the room-local centre of the course's slot.
            var at = Course < 0 ? new Vector3(160f, -0.7f, 115f) : new Vector3((Course - 2.5f) * 450f, 0f, 700f);

            var mat = RrLook.Solid(new Color(0.25f, 0.2f, 0.45f));
            if (Course < 0)
            {
                RrBuild.Floor(Root, "Fallback Lobby", at, new Vector2(20f, 20f), mat);
                Lobby.Clear();
                for (int i = 0; i < 8; i++)
                {
                    Lobby.Add(new Pose(at + new Vector3(4f, 0.05f, (i - 3.5f) * 1.1f), Quaternion.Euler(0f, -90f, 0f)));
                }

                Bounds = new Bounds(at, new Vector3(20f, 10f, 20f));
                return;
            }

            RrBuild.Floor(Root, "Fallback Run", at + new Vector3(0f, 0f, 25f), new Vector2(12f, 60f), mat);
            Starts.Clear();
            for (int i = 0; i < 4; i++)
            {
                Starts.Add(new Pose(at + new Vector3((i - 1.5f) * 2.03f, 0.05f, 0f), Quaternion.identity));
            }

            StartCheckpoint = Starts[0];
            Finish = new Pose(at + new Vector3(0f, 0f, 52f), Quaternion.identity);
            Button = Finish.position + new Vector3(0f, 1f, 1.5f);
            HasButton = true;
            PostRun = new Pose(Finish.position + new Vector3(3f, 0.05f, 0f), Quaternion.identity);
            Bounds = new Bounds(at + new Vector3(0f, 0f, 25f), new Vector3(12f, 20f, 60f));
        }
    }
}
