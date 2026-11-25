using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm
{
    /// <summary>
    /// The loft ladder (<c>(Dorm_Ladder)</c>, mesh LadderShort: rails 0.48 m apart, 2.81 m high, top at the loft floor
    /// 2.75 m). Rec Room climbs by grabbing rungs hand over hand; Somnium has no climbing, so the ladder is a
    /// "climb" button: press it (desktop click, VR trigger ray or poke) or, in VR, squeeze a grip with a hand on the
    /// ladder. From the floor the player is placed on the loft facing into it; from the loft, back down at the
    /// ladder's foot facing it. The landings (rr_dorm_core §3.3 suggested top (-3.2, 2.75), bottom (-2.2, 0)) follow the
    /// ladder's z, <see cref="RrDormRoom.LadderPosition"/> (1.27 since iteration 4).
    /// </summary>
    public sealed class RrDormLadder : MonoBehaviour
    {
        public static readonly Vector3 Top = new Vector3(-3.25f, RrDormRoom.LoftFloorY + 0.05f, RrDormRoom.LadderPosition.z);
        public static readonly Vector3 Bottom = new Vector3(-2.05f, 0.05f, RrDormRoom.LadderPosition.z);
        public const float GripReach = 0.3f;

        public int Climbs { get; private set; }

        private RrDormRoom _room;
        private float _last = -10f;

        public void Setup(RrDormRoom room, RrInstance inst)
        {
            _room = room;
            var press = RrPressable.Make(inst.gameObject, () => Climb("press"));
            press.PressSound = "";
            press.Cooldown = 1f;
        }

        /// <summary>Up when the player is below the loft, down otherwise.</summary>
        public void Climb(string how)
        {
            if (_room == null || Time.time - _last < 1f)
            {
                return;
            }

            _last = Time.time;
            var t = _room.transform;
            float feetY = t.InverseTransformPoint(RrPlayer.FeetPosition).y;
            bool up = feetY < RrDormRoom.LoftFloorY * 0.5f;
            Vector3 target = t.TransformPoint(up ? Top : Bottom);
            float yaw = t.eulerAngles.y + 270f; // up: facing into the loft (-x); down: facing the ladder (-x)
            Climbs++;
            RrLog.Info("Dorm", "ladder (" + how + "): " + (up ? "up to the loft" : "down to the floor"));
            RrPlayer.Teleport(target, yaw);
        }

        private void Update()
        {
            if (!RrPlayer.InVr)
            {
                return;
            }

            CheckHand(RrHand.Left);
            CheckHand(RrHand.Right);
        }

        private void CheckHand(RrHand hand)
        {
            if (hand == null || !hand.GripDown || hand.Held != null)
            {
                return;
            }

            // In piece space the rails stand at x +-0.24, 0..2.81 m high, 0.06 m thick along z.
            Vector3 local = transform.InverseTransformPoint(hand.transform.position);
            if (local.y > -0.1f && local.y < 2.9f && Mathf.Abs(local.x) < 0.24f + GripReach && Mathf.Abs(local.z) < 0.05f + GripReach)
            {
                Climb("grip");
            }
        }
    }
}
