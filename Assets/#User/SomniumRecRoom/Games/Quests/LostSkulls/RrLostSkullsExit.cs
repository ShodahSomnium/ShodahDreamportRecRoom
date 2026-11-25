using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls
{
    /// <summary>
    /// A room exit of the Isle: Rec Room's wooden quest door [Quest_Door_Active] (drawn by the level) with its
    /// [DoorTrigger] (1) pad 2.3-3.4 m in front of it, where the team gathers to move on (DoorUnlockTrigger). As in the
    /// Rise of Jumbotron (<see cref="RrJumbotronDoor"/>), the framework's <see cref="RrQuestDoor"/> is put on that pad
    /// facing the door with its medieval parts removed (only the logic stays), and a gold glow on the pad shows when the
    /// way is open. Levels without a pad (Beach2) use 2.8 m in front of the door.
    /// </summary>
    public sealed class RrLostSkullsExit : MonoBehaviour
    {
        private RrQuestDoor _door;
        private Renderer _glow;

        public static RrQuestDoor Create(RrJumbotronArea a, Transform parent)
        {
            RrSceneMarker exit = null;
            foreach (var m in a.Markers("door", "[Quest_Door_Active]"))
            {
                exit = m;
                break;
            }

            if (exit == null)
            {
                return null;
            }

            Vector3 into = exit.Rotation * Vector3.forward;
            into.y = 0f;
            into = into.sqrMagnitude > 1e-4f ? into.normalized : Vector3.forward;
            RrSceneMarker pad = null;
            foreach (var m in a.Markers("door_trigger"))
            {
                if (Vector3.Distance(m.Position, exit.Position) < 6f)
                {
                    pad = m;
                }
            }

            Vector3 at = pad != null ? pad.Position : exit.Position + into * 2.8f;
            at -= into * 0.4f;
            float yaw = Mathf.Atan2(-into.x, -into.z) * Mathf.Rad2Deg;
            var door = RrQuestDoor.Create(parent, at, yaw, 3f, 3f);
            door.OpenClip = null;
            for (int i = door.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(door.transform.GetChild(i).gameObject);
            }

            var x = door.gameObject.AddComponent<RrLostSkullsExit>();
            x._door = door;
            var glow = RrBuild.Cylinder(door.transform, "Way Out Glow", new Vector3(0f, 0.03f, 0.9f), 0.9f, 0.03f, RrLook.Emissive(new Color(1f, 0.78f, 0.3f), 1.8f), false);
            x._glow = glow.GetComponent<Renderer>();
            x._glow.enabled = false;
            RrLog.Info("LostSkulls", a.Scene + ": exit door at " + exit.Position.ToString("F1") + ", leave zone on the " + (pad != null ? "trigger pad " + pad.Position.ToString("F1") : "door front"));
            return door;
        }

        private void Update()
        {
            if (_door != null && _glow != null && _glow.enabled != _door.IsOpen)
            {
                _glow.enabled = _door.IsOpen;
            }
        }
    }
}
