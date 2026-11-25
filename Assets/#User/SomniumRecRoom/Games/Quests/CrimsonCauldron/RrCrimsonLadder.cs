using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.CrimsonCauldron
{
    /// <summary>
    /// One of the Goblin2 ladders ([Ladder_Hook_Short], [Ladder_Short_Dark_Grey] ...). In Rec Room a ladder is a
    /// TeleportationPortal: a thin TeleportVolume box over the rungs (about 0.1 x 3 x 0.6 m) and a TeleportDestination on
    /// the ledge above; touching the rungs puts the player on top (the 2020 update "improved visibility and flow in the
    /// ladder section of the bell-tower"). Here: the local player whose feet come within 0.45 m of the rungs (or a hand
    /// within 0.15 m) is teleported to the destination, facing its yaw, once per 1.5 s. Each client moves only itself,
    /// so nothing is networked. Going down is a drop, as in Rec Room.
    /// </summary>
    public sealed class RrCrimsonLadder : MonoBehaviour
    {
        public const float FeetReach = 0.45f;
        public const float HandReach = 0.15f;
        public const float Cooldown = 1.5f;

        private Vector3 _half;
        private Vector3 _top;
        private float _topYaw;
        private float _nextUse;

        public static RrCrimsonLadder Create(Transform parent, Vector3 local, Quaternion rotation, Vector3 size, Vector3 topLocal, float topYaw)
        {
            var go = RrBuild.Group(parent, "Ladder", local, rotation);
            var l = go.AddComponent<RrCrimsonLadder>();
            l._half = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 0.5f;
            l._top = topLocal;
            l._topYaw = topYaw;
            return l;
        }

        private void Update()
        {
            if (Time.time < _nextUse || !RrPlayer.HasClient)
            {
                return;
            }

            bool touch = Near(RrPlayer.FeetPosition + Vector3.up * 0.3f, FeetReach);
            if (!touch && RrPlayer.InVr)
            {
                for (int s = 0; s < 2 && !touch; s++)
                {
                    var hand = RrPlayer.BridgeHand(s == 0 ? RrHandSide.Left : RrHandSide.Right);
                    touch = hand != null && Near(hand.position, HandReach);
                }
            }

            if (!touch)
            {
                return;
            }

            _nextUse = Time.time + Cooldown;
            var parent = transform.parent;
            Vector3 top = parent != null ? parent.TransformPoint(_top) : _top;
            float yaw = (parent != null ? parent.eulerAngles.y : 0f) + _topYaw;
            RrPlayer.Teleport(top + Vector3.up * 0.05f, yaw);
            RrSound.Play("Quest_Longbow_Creak", transform.position, 0.5f, 1.3f);
            RrLog.Info("CrimsonCauldron", "ladder climbed to " + top.ToString("F1"));
        }

        /// <summary>A world point within <paramref name="reach"/> m of the rung box.</summary>
        private bool Near(Vector3 world, float reach)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= _half.x + reach && Mathf.Abs(p.y) <= _half.y + 0.1f && Mathf.Abs(p.z) <= _half.z + reach;
        }
    }
}
