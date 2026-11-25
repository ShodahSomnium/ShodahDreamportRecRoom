using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// One act (room) of a quest (rr_quests.md 3.3 / 4.0). Acts are separate rooms built once at the quest's origin
    /// + <see cref="Origin"/> (80 m apart along +z), so they never overlap; the team is moved from one to the next
    /// through the exit door. Everything here is act-local: +z is "forward", the entry is at the south end.
    /// </summary>
    public sealed class RrQuestAct
    {
        public string Title;
        public string Subtitle;
        /// <summary>Act root position relative to the quest room.</summary>
        public Vector3 Origin;
        /// <summary>Where the team arrives (and respawns: the act is the checkpoint), act-local, facing yaw.</summary>
        public Vector3 Entry;
        public float EntryYaw;
        /// <summary>Exit door position (act-local, the door faces -z into the act); no door when null (the final act).</summary>
        public Vector3? Exit;
        public float ExitYaw;
        /// <summary>Act-local bounds of the playable area (players inside count as "in the act").</summary>
        public Bounds Area;
        public readonly List<RrQuestWave> Waves = new List<RrQuestWave>();
        /// <summary>Side waves (at most 16): optional zone spawns outside the wave order (<see cref="SideWave"/>).</summary>
        public readonly List<RrQuestWave> SideWaves = new List<RrQuestWave>();
        /// <summary>Named spawn points (act-local). Flyers spawn 2.5 m above their point.</summary>
        public readonly Dictionary<string, List<Vector3>> Groups = new Dictionary<string, List<Vector3>>();
        /// <summary>Lava / deadly liquid areas (act-local): xz extent and the surface height (Bounds.max.y).</summary>
        public readonly List<Bounds> Lava = new List<Bounds>();
        /// <summary>A boss act: combat music stays on and the clear means victory when it is the last act.</summary>
        public bool Boss;
        /// <summary>
        /// A pre-start room (Golden Trophy's dressing room, Rec Room's Quest_Armory level: after START the team arrives
        /// there, takes its weapons from the wall and walks to the door together). No title card, no combat music, no
        /// clear fanfare or "the way is open" toast; with no waves its exit opens when the intro ends.
        /// </summary>
        public bool Prelude;
        /// <summary>Optional toast every client shows on arriving in the act (a prelude's instruction); null = none.</summary>
        public string ArrivalToast;
        /// <summary>Set by the level builder.</summary>
        public Transform Root;
        public RrQuestDoor ExitDoor;

        /// <summary>Adds spawn points to a group; chainable.</summary>
        public RrQuestAct Group(string name, params Vector3[] points)
        {
            List<Vector3> list;
            if (!Groups.TryGetValue(name, out list))
            {
                list = new List<Vector3>();
                Groups.Add(name, list);
            }

            list.AddRange(points);
            return this;
        }

        public RrQuestWave Wave(RrQuestWave.When trigger, float param = 0f)
        {
            var w = new RrQuestWave(trigger, param);
            Waves.Add(w);
            return w;
        }

        /// <summary>
        /// An optional side wave (a tunnel, a side room): it spawns once, when a living player enters the act-local
        /// zone, whatever the main waves are doing, and the act does not wait for it (only for whatever it spawned to
        /// die). Chain <see cref="RrQuestWave.Add"/> for its enemies. At most 16 per act.
        /// </summary>
        public RrQuestWave SideWave(Vector3 zoneCenter, Vector3 zoneSize)
        {
            var w = new RrQuestWave(RrQuestWave.When.Enter) { Side = true, Zone = new Bounds(zoneCenter, zoneSize) };
            if (SideWaves.Count < 16)
            {
                SideWaves.Add(w);
            }

            return w;
        }

        /// <summary>Is this world position over one of the act's lava areas and below its surface + margin?</summary>
        public bool InLava(Vector3 world, float margin)
        {
            if (Root == null || Lava.Count == 0)
            {
                return false;
            }

            Vector3 p = Root.InverseTransformPoint(world);
            for (int i = 0; i < Lava.Count; i++)
            {
                var b = Lava[i];
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && p.y <= b.max.y + margin)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Contains(Vector3 world)
        {
            if (Root == null)
            {
                return false;
            }

            Vector3 p = Root.InverseTransformPoint(world);
            return p.x >= Area.min.x && p.x <= Area.max.x && p.z >= Area.min.z && p.z <= Area.max.z && p.y >= Area.min.y - 3f && p.y <= Area.max.y;
        }
    }
}
