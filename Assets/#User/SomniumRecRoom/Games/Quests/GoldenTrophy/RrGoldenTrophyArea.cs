using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy
{
    /// <summary>
    /// One room of Quest for the Golden Trophy (the lobby, the dressing room or an act) built from Rec Room's own level
    /// in the Android build (DevNotes/refdata/apk/goldentrophy/GoldenTrophy_&lt;Room&gt;.rrsk, made by DevNotes/tools/gtscene;
    /// generated into Scenes/RrScene_GoldenTrophy_&lt;Room&gt;.cs). Scene point p lands at area-local p - <see cref="Anchor"/>.
    ///
    /// What is built, in order:
    /// - <see cref="RrSceneKit"/>: Rec Room's props from the install at their exact poses, the level's colliders exactly as
    ///   it has them (its box colliders, plus the hull's mesh colliders measured into box plates 0.2 m deep behind each
    ///   face), every light of the level (MaxLights 64: Hallway1 has 30, the Foyer 40; merged cells 12 m so the client's
    ///   per-object limit of 4 lights picks the near ones), and the markers (spawns, weapons, pots, doors, lava);
    /// - <see cref="RrGoldenTrophyHull"/>: the walls, floors and ceilings ("plate" records) and the other stand-ins
    ///   ("dress" records), which the install does not have.
    /// Nothing is added for light: Rec Room's quest rooms are dark (Hallway1 ambient trilight sky 0.03, equator 0.13,
    /// fog linear 2-33 m; the lobby ambient 0.015) and lit by their own point and spot lights.
    ///
    /// Gameplay pieces come from the level's markers: the arrival (Player7SpawnPoint 1..4, or the dressing room's
    /// PlayerSpawnPoint), the exit ([Quest_Door_Active], whose forward looks into the room, and the [DoorTrigger] platform
    /// ~3 m in front of it), pots ((FatPot) 50, (FatPot_Large) 100) and Rec Room's "Default Weapons" at their own poses,
    /// lava ([QuestLavaFloor]: its top is the lava surface). Acts 1-6 also carry the REF2 Studio port's enemy spawn points
    /// (the same levels in the same frame: identical player spawns); the Android levels keep theirs in circuits.
    /// </summary>
    public sealed class RrGoldenTrophyArea
    {
        public readonly string Scene;
        /// <summary>The scene point at the area root's origin: given, or (Vector3.zero) the centre of the arrival spawns.</summary>
        public Vector3 Anchor { get; private set; }

        public RrSceneData Data;
        public RrSceneKitResult Kit;
        public Transform Root;

        /// <summary>Child of <see cref="Root"/> at -Anchor: the level's own coordinates (hull, dressing).</summary>
        public Transform Frame;

        /// <summary>The level's floor rectangles' extent (scene frame), for the act area.</summary>
        public Bounds SceneExtent;

        public int Plates, Dressed, Torches;
        public bool HasEffects;

        private static readonly HashSet<string> SkipNames = new HashSet<string>
        {
            // Rec Room's activity-door leaves and their lit inside (our RrQuestDoor's leaves swing in Rec Room's own door
            // frame, ActivityDoor_Frame, which stays) and the stage-light beams, which draw as solid slabs without Rec Room's
            // fade (harness 2026-09-24).
            "ActivityDoor_Left", "ActivityDoor_Right", "ActivityDoor_Emit", "ActivityDoor_Glow", "LightArchShaft"
        };

        public RrGoldenTrophyArea(string scene, Vector3 anchor)
        {
            Scene = scene;
            Anchor = anchor;
        }

        public Vector3 L(float x, float y, float z)
        {
            return new Vector3(x - Anchor.x, y - Anchor.y, z - Anchor.z);
        }

        public Vector3 L(Vector3 scene)
        {
            return scene - Anchor;
        }

        /// <summary>Parses, builds the kit, then the hull and the dressing under root (spread over frames).</summary>
        public IEnumerator Build(RrGoldenTrophyHull hull, string text, Transform root)
        {
            Root = root;
            RrSceneData data = null;
            string error = null;
            yield return RrSceneKit.Parse(text, (d, e) =>
            {
                data = d;
                error = e;
            });
            if (data == null)
            {
                RrLog.Warn("Quest", Scene + ": the scene data did not parse (" + error + ")");
                yield break;
            }

            Data = data;
            if (Anchor == Vector3.zero)
            {
                Anchor = SpawnCentre(data);
            }

            var plates = new List<RrGoldenTrophyHull.Plate>();
            var items = new List<RrGoldenTrophyHull.Item>();
            var mats = new HashSet<string>();
            bool any = false;
            var min = Vector3.zero;
            var max = Vector3.zero;
            foreach (var m in data.Markers)
            {
                if (m.Kind == "plate")
                {
                    var e = RrGoldenTrophyHull.Vec2(RrGoldenTrophyHull.Key(m, "e"), new Vector2(0.5f, 0.5f));
                    plates.Add(new RrGoldenTrophyHull.Plate { Mat = m.Name, C = m.Position, Q = m.Rotation, Hx = e.x, Hy = e.y, Tint = RrGoldenTrophyHull.Col(RrGoldenTrophyHull.Key(m, "c"), Color.white) });
                    mats.Add(m.Name);
                    // Floors (facing up) set the playable extent.
                    if ((m.Rotation * Vector3.forward).y > 0.7f && m.Name != "Unlit_Black_mat")
                    {
                        var lo = m.Position - new Vector3(e.x, 0f, e.y);
                        var hi = m.Position + new Vector3(e.x, 0f, e.y);
                        if (!any)
                        {
                            min = Vector3.Min(lo, hi);
                            max = Vector3.Max(lo, hi);
                            any = true;
                        }
                        else
                        {
                            min = Vector3.Min(min, Vector3.Min(lo, hi));
                            max = Vector3.Max(max, Vector3.Max(lo, hi));
                        }
                    }
                }
                else if (m.Kind == "platemat")
                {
                    hull.AddInfo(m.Name, new RrGoldenTrophyHull.MatInfo
                    {
                        Col = RrGoldenTrophyHull.Col(RrGoldenTrophyHull.Key(m, "col"), Color.white),
                        Tex = RrSceneData.Decode(RrGoldenTrophyHull.Key(m, "tex") ?? "-"),
                        Inst = RrGoldenTrophyHull.Key(m, "inst") == "1",
                        Scale = RrGoldenTrophyHull.Vec2(RrGoldenTrophyHull.Key(m, "scale"), Vector2.one)
                    });
                }
            }

            SceneExtent = any ? new Bounds((min + max) * 0.5f, max - min) : new Bounds(Anchor, new Vector3(40f, 0f, 40f));
            data.Markers.RemoveAll(m => m.Kind == "plate" || m.Kind == "platemat");
            HasEffects = data.Effects.Count > 0;
            Plates = plates.Count;
            yield return hull.Prepare(mats);
            var options = new RrSceneKitOptions
            {
                Position = -Anchor,
                RootName = "Rr Scene " + Scene,
                SkipNames = SkipNames
            };
            yield return RrSceneKit.Build(data, root, options, r => Kit = r);
            Frame = RrBuild.Group(root, Scene + " frame", -Anchor).transform;
            yield return hull.Build(Frame, Scene, plates);
            // The stand-ins come back from the kit: it drops the ones an install renderer or prefab already draws in the
            // same place (FixOverlaps, box IoU >= 0.5), so nothing is drawn twice. Their poses are area-local.
            if (Kit != null)
            {
                foreach (var m in Kit.Markers)
                {
                    if (m.Kind != "dress")
                    {
                        continue;
                    }

                    items.Add(new RrGoldenTrophyHull.Item
                    {
                        Kind = m.Name,
                        C = m.Position,
                        Q = m.Rotation,
                        E = RrGoldenTrophyHull.Vec(m.Get("e"), new Vector3(0.1f, 0.1f, 0.1f)),
                        Col = RrGoldenTrophyHull.Col(m.Get("c"), Color.grey),
                        Mat = m.Get("m")
                    });
                }
            }

            Torches = hull.Dress(Root, Scene, items, !HasEffects);
            Dressed = items.Count;
            // The floor probes (FloorAt) cast rays at the colliders made this frame.
            Physics.SyncTransforms();
            RrLog.Info("Quest", Scene + ": built at area-local offset " + (-Anchor).ToString("F1") + ": " + Plates + " hull rectangles, " + Dressed + " stand-ins (" + Torches + " torches), "
                + (Kit != null ? Kit.Markers.Count + " markers, " + Kit.Lights + " lights, " + Kit.BoxColliders + " box colliders" : "no kit") + (HasEffects ? ", level effects" : ""));
        }

        /// <summary>
        /// The arrival's centre in scene space: Player7SpawnPoint (1..4) in the acts, PlayerSpawnPoint in the dressing
        /// room, else every spawn (the lobby's Player1..8SpawnPoint), so the team arrives at the area root.
        /// </summary>
        private static Vector3 SpawnCentre(RrSceneData data)
        {
            foreach (var prefix in new[] { "Player7SpawnPoint", "PlayerSpawnPoint", "Player" })
            {
                var sum = Vector3.zero;
                int n = 0;
                foreach (var m in data.Markers)
                {
                    if (m.Kind == "spawn" && m.Name.StartsWith(prefix))
                    {
                        sum += m.Position;
                        n++;
                    }
                }

                if (n > 0)
                {
                    return sum / n;
                }
            }

            return data.HasBounds ? data.Bounds.center : Vector3.zero;
        }

        // ------------------------------------------------------------------ markers

        /// <summary>Markers of a kind whose name starts with prefix (area-local poses).</summary>
        public List<RrSceneMarker> Markers(string kind, string prefix = null)
        {
            var list = new List<RrSceneMarker>();
            if (Kit == null)
            {
                return list;
            }

            foreach (var m in Kit.Markers)
            {
                if (m.Kind == kind && (prefix == null || m.Name.StartsWith(prefix)))
                {
                    list.Add(m);
                }
            }

            return list;
        }

        /// <summary>The first "extra" marker (gtscene: [DoorTrigger], [Quest_Door_Active], [Quest_Boulder], Boss Spawn Point...).</summary>
        public RrSceneMarker Extra(string prefix, bool activeOnly = true)
        {
            foreach (var m in Markers("extra", prefix))
            {
                if (!activeOnly || m.Get("active") != "0")
                {
                    return m;
                }
            }

            return null;
        }

        /// <summary>The REF2 enemy spawn points (EnemySpawnPoint*, GroundEnemySpawnPoint1..4; not the group objects).</summary>
        public List<RrSceneMarker> Spawners()
        {
            var list = new List<RrSceneMarker>();
            foreach (var m in Markers("enemy_spawner"))
            {
                if (!m.Name.EndsWith("Points"))
                {
                    list.Add(m);
                }
            }

            return list;
        }

        /// <summary>Area-local points of the spawners inside a scene-space rectangle (and height band).</summary>
        public Vector3[] SpawnersIn(float x0, float z0, float x1, float z1, float y0 = -1e5f, float y1 = 1e5f)
        {
            var list = new List<Vector3>();
            foreach (var m in Spawners())
            {
                var s = m.ScenePosition;
                if (s.x >= Mathf.Min(x0, x1) && s.x <= Mathf.Max(x0, x1) && s.z >= Mathf.Min(z0, z1) && s.z <= Mathf.Max(z0, z1) && s.y >= y0 && s.y <= y1)
                {
                    list.Add(m.Position);
                }
            }

            return list.ToArray();
        }

        /// <summary>The team's arrival: the centre of Player7SpawnPoint (1..4) (or any PlayerSpawnPoint) and their facing.</summary>
        public bool Arrival(out Vector3 at, out float yaw)
        {
            var spawns = Markers("spawn", "Player7SpawnPoint");
            if (spawns.Count == 0)
            {
                spawns = Markers("spawn", "PlayerSpawnPoint");
            }

            at = Vector3.zero;
            yaw = 0f;
            if (spawns.Count == 0)
            {
                return false;
            }

            foreach (var s in spawns)
            {
                at += s.Position;
            }

            at /= spawns.Count;
            yaw = spawns[0].Rotation.eulerAngles.y;
            return true;
        }

        // ------------------------------------------------------------------ floors

        /// <summary>
        /// The walkable height under an area-local point: a ray down from 1.5 m above it onto the level's colliders
        /// (layer Environment). NaN when there is nothing within reach.
        /// </summary>
        public float FloorAt(Vector3 local, float above = 1.5f, float reach = 6f)
        {
            if (Root == null)
            {
                return float.NaN;
            }

            RaycastHit hit;
            var from = Root.TransformPoint(local + Vector3.up * above);
            if (Physics.Raycast(from, Vector3.down, out hit, above + reach, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore))
            {
                return Root.InverseTransformPoint(hit.point).y;
            }

            return float.NaN;
        }

        /// <summary>Area-local points set down onto the floor under them (kept as they are where no floor is found).</summary>
        public Vector3[] Ground(params Vector3[] local)
        {
            var list = new Vector3[local.Length];
            for (int i = 0; i < local.Length; i++)
            {
                float h = FloorAt(local[i], 0.8f, 5f);
                list[i] = float.IsNaN(h) ? local[i] : new Vector3(local[i].x, h + 0.05f, local[i].z);
            }

            return list;
        }

        /// <summary>
        /// Up to n spawn points on the level's own floor inside a scene-space rectangle, for rooms without reference
        /// spawn points: a 1.5 m grid of rays straight down (from 1.3 m above the band's top) onto the colliders; a point
        /// counts when it lands on a level surface (normal within 45 degrees of up) between y0 and y1 (scene heights) with
        /// 2.1 m of headroom. The chosen points are spread evenly over the candidates. Area-local, 5 cm above the floor.
        /// </summary>
        public Vector3[] FloorPoints(float x0, float z0, float x1, float z1, float y0, float y1, int n, float step = 1.5f)
        {
            var cand = new List<Vector3>();
            if (Root == null)
            {
                return cand.ToArray();
            }

            int mask = 1 << RrLayers.Environment;
            float xa = Mathf.Min(x0, x1), xb = Mathf.Max(x0, x1), za = Mathf.Min(z0, z1), zb = Mathf.Max(z0, z1);
            for (float z = za + step * 0.5f; z < zb; z += step)
            {
                for (float x = xa + step * 0.5f; x < xb; x += step)
                {
                    RaycastHit hit;
                    var from = Root.TransformPoint(L(x, y1 + 1.3f, z));
                    if (!Physics.Raycast(from, Vector3.down, out hit, (y1 - y0) + 1.6f, mask, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7f)
                    {
                        continue;
                    }

                    var local = Root.InverseTransformPoint(hit.point);
                    float sy = local.y + Anchor.y;
                    if (sy < y0 - 0.05f || sy > y1 + 0.05f || Physics.Raycast(hit.point + Vector3.up * 0.1f, Vector3.up, 2.1f, mask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    cand.Add(local + Vector3.up * 0.05f);
                }
            }

            if (cand.Count <= n)
            {
                return cand.ToArray();
            }

            var res = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                res[i] = cand[Mathf.Min(cand.Count - 1, (int)((i + 0.5f) * cand.Count / n))];
            }

            return res;
        }

        /// <summary>Scene points (x, y, z triples) to area-local points on the floor.</summary>
        public Vector3[] Pts(params float[] xyz)
        {
            var list = new Vector3[xyz.Length / 3];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = L(xyz[i * 3], xyz[i * 3 + 1], xyz[i * 3 + 2]);
            }

            return Ground(list);
        }

        // ------------------------------------------------------------------ gameplay pieces

        /// <summary>Pots at the level's own poses (index order = scene order): "Collectibles/Collectables/Decorative/Store" sets.</summary>
        public int AddPots(RrQuestBreakables pots, Transform parent)
        {
            int n = 0, gold = 0;
            foreach (var m in Markers("weapon_pickup"))
            {
                string prefab = m.Get("prefab");
                if ((prefab != "(FatPot)" && prefab != "(FatPot_Large)") || m.Get("active") == "0")
                {
                    continue;
                }

                int pts = prefab == "(FatPot_Large)" ? 100 : 50;
                pots.Add(parent, prefab, m.Position, m.Rotation.eulerAngles.y, pts);
                n++;
                gold += pts;
            }

            if (n > 0)
            {
                RrLog.Info("Quest", Scene + ": " + n + " pots at Rec Room's poses (" + gold + " Gold)");
            }

            return n;
        }

        /// <summary>
        /// Rec Room's "Default Weapons" of the room (swords, shields, bows, crossbows) at their own poses, as refill
        /// pegs (<see cref="RrQuestRack.AtPoses"/>). The "Laser Weapons" set the level also holds is the RRO mod's; the
        /// mod swaps the quest weapons on the pegs instead.
        /// </summary>
        public RrQuestRack AddWeapons(RrGoldenTrophyRoom room, Transform parent)
        {
            var prefabs = new List<string>();
            var pos = new List<Vector3>();
            var rot = new List<Quaternion>();
            foreach (var m in Markers("weapon_pickup"))
            {
                string prefab = m.Get("prefab");
                string set = m.Get("set");
                if (set != "Default Weapons" || m.Get("active") == "0")
                {
                    continue;
                }

                if (prefab != RrGoldenTrophyRoom.Sword && prefab != RrGoldenTrophyRoom.Shield && prefab != RrGoldenTrophyRoom.Bow && prefab != RrGoldenTrophyRoom.Crossbow)
                {
                    continue;
                }

                prefabs.Add(prefab);
                pos.Add(m.Position);
                rot.Add(m.Rotation);
            }

            if (prefabs.Count == 0)
            {
                return null;
            }

            var rack = RrQuestRack.AtPoses(parent, Scene + " Weapons", prefabs.ToArray(), pos.ToArray(), rot.ToArray());
            room.RegisterRack(rack);
            RrLog.Info("Quest", Scene + ": " + prefabs.Count + " weapons at Rec Room's poses (" + string.Join(", ", prefabs) + ")");
            return rack;
        }

        /// <summary>
        /// The exit: our <see cref="RrQuestDoor"/> on Rec Room's [Quest_Door_Active], facing into the room so its leaves
        /// open towards the team and its "went through" zone (the 3.5 m in front of the door) covers Rec Room's
        /// [DoorTrigger] platform, where the team gathers (2.9-3.0 m in front of the door in every act). A glowing
        /// platform marks the trigger; <paramref name="platform"/> is shown when the door opens.
        /// </summary>
        public RrQuestDoor ExitDoor(Transform parent, out GameObject platform)
        {
            platform = null;
            var m = Extra("[Quest_Door_Active]");
            if (m == null)
            {
                return null;
            }

            float yaw = m.Rotation.eulerAngles.y;
            var into = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            // Rec Room's door (2.05 x 2.15 m, its trigger box) on the wall: our leaves only - the stone frame of the
            // procedural door would stand 0.5 m into the room and into the weapons beside the dressing-room door.
            var door = RrQuestDoor.Create(parent, m.Position + into * 0.12f, yaw, 2.05f, 2.15f);
            foreach (var part in new[] { "Jamb L", "Jamb R", "Lintel", "Keystone" })
            {
                var child = door.transform.Find(part);
                if (child != null)
                {
                    Object.Destroy(child.gameObject);
                }
            }

            var trig = Extra("[DoorTrigger]");
            var at = trig != null ? trig.Position : m.Position + into * 2.9f;
            // Walking onto the platform, or up to the door across it, takes the team on, as Rec Room's trigger did: the
            // zone runs from 0.2 m in front of the leaves to the platform's far edge, 2.6 m wide, feet up to 2 m.
            var zl = door.transform.InverseTransformPoint(parent.TransformPoint(at));
            float far = Mathf.Max(1.6f, zl.z + 1.2f);
            door.Zone = new Bounds(new Vector3(0f, zl.y + 0.9f, (0.2f + far) * 0.5f), new Vector3(2.6f, 3f, far - 0.2f));
            // Shown while the door is open: the glowing platform and, in the doorway, the lit inside of Rec Room's activity
            // door (ActivityDoor_Emit, left out of the kit: it cannot open) as a warm pane just in front of the wall.
            platform = RrBuild.Group(parent, "Gather", Vector3.zero);
            var glow = RrLook.Emissive(new Color(1f, 0.62f, 0.22f), 0.9f);
            var pad = RrBuild.Box(platform.transform, "Gather Platform", at + Vector3.up * 0.02f, Quaternion.Euler(0f, yaw, 0f), new Vector3(2.2f, 0.03f, 2.2f), glow, false);
            pad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pane = RrBuild.Box(platform.transform, "Door Light", m.Position + into * 0.04f + Vector3.up * 1.07f, Quaternion.Euler(0f, yaw, 0f), new Vector3(1.95f, 2.1f, 0.02f),
                RrLook.Emissive(new Color(1f, 0.85f, 0.55f), 1.2f), false);
            pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            platform.SetActive(false);
            return door;
        }

        /// <summary>The lava areas of the level ([QuestLavaFloor]: centre and size; max.y is the surface), area-local.</summary>
        public List<Bounds> Lava()
        {
            var list = new List<Bounds>();
            foreach (var m in Markers("lava"))
            {
                var size = RrGoldenTrophyHull.Vec(SizeOf(m), new Vector3(10f, 1f, 10f));
                list.Add(new Bounds(m.Position, size));
            }

            return list;
        }

        private static string SizeOf(RrSceneMarker m)
        {
            // "shape=box size=x,y,z" is written as two pairs: shape -> box, size -> x,y,z.
            return m.Get("size");
        }

        /// <summary>The act's playable bounds (area-local) from the level's floors.</summary>
        public Bounds Area()
        {
            var b = SceneExtent;
            b.center -= Anchor;
            b.Expand(new Vector3(2f, 12f, 2f));
            return b;
        }
    }
}
