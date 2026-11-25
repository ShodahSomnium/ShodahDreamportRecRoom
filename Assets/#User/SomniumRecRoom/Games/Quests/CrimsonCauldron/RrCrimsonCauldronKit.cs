using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.CrimsonCauldron
{
    /// <summary>
    /// The Curse of the Crimson Cauldron's pieces read from each room's own level markers (DN/refdata/apk/Crimson_*.rrsk,
    /// Android levels 50-60 = Quest_Goblin2_*, DevNotes/iter13/crimson.md). The rooms are built by the Jumbotron's generic
    /// level builder (<see cref="RrJumbotronArea"/>: install props, colliders, lights, hull plates, haze); this class turns
    /// the Goblin2 markers into quest parts:
    ///
    /// | marker | what it becomes |
    /// | --- | --- |
    /// | weapon_pickup (FatPot) / (FatPot_Large) | the quest's breakable pots, 50 / 100 gold (rr_latest_rgames_story.md 6.1) |
    /// | weapon_pickup (QuestSword) (QuestShield) (Longbow) (Crossbow) (Crossbow_Hunter) (Quest_Goblin_Wand) (Quest_Goblin_FirePotion) | a <see cref="RrQuestRack"/> at Rec Room's own poses |
    /// | door [Quest_Door_Active] + door_trigger [DoorTrigger] | the act exit: the medieval <see cref="RrQuestDoor"/> leaves in Rec Room's frame, the gather pad |
    /// | enc_trigger (an encounter's Triggers/Overlap Volume; world scale = size, turned by its yaw) | an Enter wave's zone |
    /// | enemy_spawner / enemy_region (enc=, grp=) | the spawn groups (<see cref="RrJumbotronArea.Points"/>) |
    /// | lava [QuestLavaFloor] | the bog water and the boiler pit: a down (<see cref="RrQuestAct.Lava"/>) |
    /// | ladder / ladder_top (lad= pairs them) | <see cref="RrCrimsonLadder"/> |
    /// | bounds OverlapVolume under a [Quest_SteamTrap] | <see cref="RrCrimsonSteam"/> |
    /// | killzone PitKillzone | the bell tower's fall line (<see cref="KillFloorY"/>) |
    /// </summary>
    public static class RrCrimsonCauldronKit
    {
        /// <summary>The weapons Rec Room lays out in the Goblin2 rooms (all behave through <see cref="RrQuestRack.Arm"/>).</summary>
        public static readonly HashSet<string> WeaponPrefabs = new HashSet<string>
        {
            "(QuestSword)", "(QuestShield)", "(Longbow)", "(Crossbow)", "(Crossbow_Hunter)", "(Quest_Goblin_Wand)", "(Quest_Goblin_FirePotion)", "(QuestMeatSword)"
        };

        /// <summary>Install meshes left out of every room: the activity-door leaves (our door swings in Rec Room's frame) and the light-arch beams.</summary>
        public static readonly string[] SkipMeshes = { "ActivityDoor_Left", "ActivityDoor_Right", "ActivityDoor_Emit", "ActivityDoor_Glow", "LightArchShaft" };

        /// <summary>Size of a steam trap's kill volume (the level's OverlapVolume world scale, measured in Mines1).</summary>
        public static readonly Vector3 SteamSize = new Vector3(1.2f, 2.51f, 2.28f);

        /// <summary>A new area for scene name <paramref name="scene"/> with the room-wide skips.</summary>
        public static RrJumbotronArea Area(string scene, Vector3 anchor)
        {
            var a = new RrJumbotronArea(scene, anchor);
            foreach (var n in SkipMeshes)
            {
                a.Skip.Add(n);
            }

            return a;
        }

        // ------------------------------------------------------------------ zones

        /// <summary>
        /// The index-th trigger volume of an encounter (area-local axis-aligned box): the Overlap Volume's world scale is
        /// its size along its own axes, so a turned volume (Random Flyers: 90 degrees) is widened to its box.
        /// </summary>
        public static bool Zone(RrJumbotronArea a, string encounter, int index, out Bounds zone)
        {
            zone = default(Bounds);
            int n = 0;
            foreach (var m in a.Markers("enc_trigger"))
            {
                if (m.Name != encounter || n++ < index)
                {
                    continue;
                }

                zone = Turned(m.Position, m.Rotation, Vec(m.Get("size"), new Vector3(8f, 6f, 8f)));
                return true;
            }

            return false;
        }

        /// <summary>The axis-aligned box around an oriented box.</summary>
        public static Bounds Turned(Vector3 center, Quaternion rotation, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 ax = rotation * new Vector3(h.x, 0f, 0f), ay = rotation * new Vector3(0f, h.y, 0f), az = rotation * new Vector3(0f, 0f, h.z);
            var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Bounds(center, ext * 2f);
        }

        /// <summary>"x,y,z" to a vector (invariant culture), or the fallback.</summary>
        public static Vector3 Vec(string s, Vector3 fallback)
        {
            if (string.IsNullOrEmpty(s))
            {
                return fallback;
            }

            var p = s.Split(',');
            float x, y, z;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var st = System.Globalization.NumberStyles.Float;
            if (p.Length == 3 && float.TryParse(p[0], st, inv, out x) && float.TryParse(p[1], st, inv, out y) && float.TryParse(p[2], st, inv, out z))
            {
                return new Vector3(x, y, z);
            }

            return fallback;
        }

        // ------------------------------------------------------------------ pots and weapons

        /// <summary>Rec Room's clay pots at their poses: (FatPot) 50 gold, (FatPot_Large) 100 (the merch booth's display pot is left out).</summary>
        public static int AddPots(RrCrimsonCauldronRoom room, RrJumbotronArea a, Transform parent)
        {
            int n = 0;
            foreach (var m in a.Markers("weapon_pickup"))
            {
                string prefab = m.Get("prefab");
                if (m.Get("set") == "Store" || (prefab != "(FatPot)" && prefab != "(FatPot_Large)"))
                {
                    continue;
                }

                room.Pots.Add(parent, prefab, m.Position, m.Rotation.eulerAngles.y, prefab == "(FatPot_Large)" ? 100 : 50);
                n++;
            }

            return n;
        }

        /// <summary>The room's weapons as one rack at Rec Room's own poses (refilled 3 s after one is taken); null when there are none.</summary>
        public static RrQuestRack AddWeapons(RrCrimsonCauldronRoom room, RrJumbotronArea a, Transform parent)
        {
            var prefabs = new List<string>();
            var pos = new List<Vector3>();
            var rot = new List<Quaternion>();
            foreach (var m in a.Markers("weapon_pickup"))
            {
                string prefab = m.Get("prefab");
                if (prefab == null || !WeaponPrefabs.Contains(prefab) || m.Get("active") == "0")
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

            var rack = RrQuestRack.AtPoses(parent, a.Scene + " Weapons", prefabs.ToArray(), pos.ToArray(), rot.ToArray());
            room.RegisterRack(rack);
            RrLog.Info("CrimsonCauldron", a.Scene + ": " + prefabs.Count + " weapons at Rec Room's poses (" + string.Join(", ", prefabs) + ")");
            return rack;
        }

        // ------------------------------------------------------------------ the exit

        /// <summary>
        /// The exit: our <see cref="RrQuestDoor"/> on Rec Room's [Quest_Door_Active] (its forward looks into the room), only
        /// its two leaves in Rec Room's own frame (the procedural jambs and lintel go), with the "went through" zone over
        /// the [DoorTrigger] pad 2.9-3.0 m in front of it, where Rec Room's team gathered. Golden Trophy's door
        /// (RrGoldenTrophyArea.ExitDoor) on the same Goblin door. Null when the level has no exit (the Finale).
        /// </summary>
        public static RrQuestDoor ExitDoor(RrJumbotronArea a, Transform parent, out GameObject platform)
        {
            platform = null;
            var list = a.Markers("door", "[Quest_Door_Active]");
            if (list.Count == 0)
            {
                return null;
            }

            var m = list[0];
            float yaw = m.Rotation.eulerAngles.y;
            var into = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var door = RrQuestDoor.Create(parent, m.Position + into * 0.12f, yaw, 2.05f, 2.15f);
            // The bell tower stairs' exit is a hatch in the ceiling (the door turned 90 degrees about x, reached by a
            // ladder Rec Room only shows once the act is clear): no leaves there, the pad below it takes the team on.
            bool hatch = Mathf.Abs(Vector3.Dot(m.Rotation * Vector3.up, Vector3.up)) < 0.5f;
            for (int i = door.transform.childCount - 1; i >= 0; i--)
            {
                var child = door.transform.GetChild(i);
                string n = child.name;
                if (hatch || n == "Jamb L" || n == "Jamb R" || n == "Lintel" || n == "Keystone")
                {
                    Object.Destroy(child.gameObject);
                }
            }

            var pads = a.Markers("door_trigger");
            Vector3 at = pads.Count > 0 && Vector3.Distance(pads[0].Position, m.Position) < 6f ? pads[0].Position : m.Position + into * 2.9f;
            var zl = door.transform.InverseTransformPoint(parent.TransformPoint(at));
            float far = Mathf.Max(1.6f, zl.z + 1.2f);
            door.Zone = new Bounds(new Vector3(0f, zl.y + 0.9f, (0.2f + far) * 0.5f), new Vector3(2.6f, 3f, far - 0.2f));
            platform = RrBuild.Group(parent, "Gather", Vector3.zero);
            var pad = RrBuild.Box(platform.transform, "Gather Platform", at + Vector3.up * 0.02f, Quaternion.Euler(0f, yaw, 0f), new Vector3(2.2f, 0.03f, 2.2f),
                RrLook.Emissive(new Color(0.85f, 0.2f, 0.95f), 0.9f), false);
            pad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            platform.SetActive(false);
            RrLog.Info("CrimsonCauldron", a.Scene + ": exit door at " + m.Position.ToString("F1") + " (yaw " + yaw.ToString("0") + ")" + (hatch ? ", a ceiling hatch" : "") + ", gather pad " + at.ToString("F1"));
            return door;
        }

        // ------------------------------------------------------------------ hazards

        /// <summary>Rec Room's [QuestLavaFloor] boxes (area-local; max.y is the surface): the bog water, the boiler pit.</summary>
        public static List<Bounds> Lava(RrJumbotronArea a)
        {
            var list = new List<Bounds>();
            foreach (var m in a.Markers("lava"))
            {
                list.Add(Turned(m.Position, m.Rotation, Vec(m.Get("size"), new Vector3(10f, 1f, 10f))));
            }

            return list;
        }

        /// <summary>Rec Room's ladders (a TeleportVolume on the rungs and its TeleportDestination at the top) as <see cref="RrCrimsonLadder"/>s.</summary>
        public static int Ladders(RrJumbotronArea a, Transform parent)
        {
            var tops = new Dictionary<string, RrSceneMarker>();
            foreach (var m in a.Markers("ladder_top"))
            {
                string key = m.Get("lad");
                if (key != null && m.Get("active") != "0")
                {
                    tops[key] = m;
                }
            }

            int n = 0;
            foreach (var m in a.Markers("ladder"))
            {
                RrSceneMarker top;
                string key = m.Get("lad");
                if (key == null || m.Get("active") == "0" || !tops.TryGetValue(key, out top))
                {
                    continue;
                }

                RrCrimsonLadder.Create(parent, m.Position, m.Rotation, Vec(m.Get("size"), new Vector3(0.1f, 3f, 0.6f)), top.Position, top.Rotation.eulerAngles.y);
                n++;
            }

            return n;
        }

        /// <summary>The steam traps ([Quest_SteamTrap]/OverlapVolume) as <see cref="RrCrimsonSteam"/> jets.</summary>
        public static int Steam(RrJumbotronArea a, Transform parent)
        {
            int n = 0;
            foreach (var m in a.Markers("bounds"))
            {
                string path = m.Get("path") ?? string.Empty;
                if (m.Name != "OverlapVolume" || path.IndexOf("SteamTrap", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                RrCrimsonSteam.Create(parent, m.Position, m.Rotation, SteamSize, n);
                n++;
            }

            return n;
        }

        /// <summary>The top of Rec Room's PitKillzone (area-local y), or null: below it a player has fallen out of the room.</summary>
        public static float? KillFloorY(RrJumbotronArea a)
        {
            foreach (var m in a.Markers("killzone"))
            {
                var size = Vec(m.Get("size"), Vector3.zero);
                return m.Position.y + size.y * 0.5f;
            }

            return null;
        }
    }
}
