using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron
{
    /// <summary>
    /// A room exit of the SS Coach: Rec Room's sci-fi door ([Quest_SciFi_RoomExitDoor]: the install's door_scifi frame
    /// with the two sliding leaves door_L / door_R in Door_SciFi_Cardboard_mat, 3.5 m wide and 2.7 m tall between the
    /// leaves' colliders in Battlefield1) opened by the quest when the act is clear.
    ///
    /// The quest framework's exit is an <see cref="RrQuestDoor"/> whose "went through" zone runs from its origin 3.5 m
    /// along +z. In Rec Room the team leaves by standing on the [Quest_SciFi_RoomDoorTrigger] pad 2.4-3.0 m in front of
    /// the open door (DoorUnlockTrigger), so the framework's door is put on that pad facing the door, its medieval parts
    /// are removed, and this component slides Rec Room's own leaves (built by <see cref="RrJumbotronArea"/>) apart when
    /// the framework opens it. The frame and the leaves' colliders stay as the level has them.
    /// </summary>
    public sealed class RrJumbotronDoor : MonoBehaviour
    {
        public const string OpenClip = "Quest_Scifi_Door_slidingopen02";

        private RrQuestDoor _door;
        private readonly List<RrJumbotronArea.DoorLeaf> _leaves = new List<RrJumbotronArea.DoorLeaf>();
        private float _k;

        /// <summary>
        /// The exit of an area: the framework door on the trigger pad (or 2.8 m in front of the door), yawed towards the
        /// door, with Rec Room's leaves at the door (within 1.5 m of its pivot). Null when the level has no exit.
        /// </summary>
        public static RrQuestDoor Create(RrJumbotronArea a, Transform parent)
        {
            var exit = a.ExitMarker();
            if (exit == null)
            {
                return null;
            }

            Vector3 into = exit.Rotation * Vector3.forward;
            into.y = 0f;
            into = into.sqrMagnitude > 1e-4f ? into.normalized : Vector3.forward;
            var pad = a.DoorTrigger();
            Vector3 at = pad != null && Vector3.Distance(pad.Position, exit.Position) < 6f ? pad.Position : exit.Position + into * 2.8f;
            // Back 0.4 m so the zone starts on the pad's near edge.
            at -= into * 0.4f;
            float yaw = Mathf.Atan2(-into.x, -into.z) * Mathf.Rad2Deg;
            var door = RrQuestDoor.Create(parent, at, yaw, 3.4f, 3f);
            door.OpenClip = OpenClip;
            // The medieval jambs, lintel, leaves and glow go; only the logic stays.
            for (int i = door.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(door.transform.GetChild(i).gameObject);
            }

            var d = door.gameObject.AddComponent<RrJumbotronDoor>();
            d._door = door;
            var exitScene = exit.ScenePosition;
            foreach (var l in a.Leaves)
            {
                if (l.T != null && Vector3.Distance(l.ScenePivot, exitScene) < 1.5f)
                {
                    d._leaves.Add(l);
                }
            }

            RrLog.Info("Jumbotron", a.Scene + ": exit door at " + exit.Position.ToString("F1") + " (yaw " + exit.Rotation.eulerAngles.y.ToString("0") + "), leave zone on the "
                + (pad != null ? "trigger pad " + pad.Position.ToString("F1") : "door front") + ", " + d._leaves.Count + " sliding leaves");
            return door;
        }

        private void Update()
        {
            if (_door == null)
            {
                return;
            }

            float target = _door.IsOpen ? 1f : 0f;
            if (Mathf.Approximately(_k, target))
            {
                return;
            }

            _k = Mathf.MoveTowards(_k, target, Time.deltaTime / 0.9f);
            float s = Mathf.SmoothStep(0f, 1f, _k);
            foreach (var l in _leaves)
            {
                if (l.T != null)
                {
                    l.T.localPosition = Vector3.Lerp(l.Closed, l.Open, s);
                }
            }
        }
    }
}
