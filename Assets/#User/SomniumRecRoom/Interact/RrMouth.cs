using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Where the local player's mouth is, for drinking and eating. Rec Room tests the bottle's mouth trigger or the
    /// food against the player's head (DisposableItem.minDistanceFromHeadSq: 0.04 = 0.2 m for most food, 0.1 = 0.32 m
    /// for cake; DrinkableBottle radii 0.25 m). Somnium gives us the head as the camera (both eyes' centre), so the mouth
    /// is taken 9 cm below and 7 cm in front of it - roughly an adult face - which keeps a bottle raised to the eyes
    /// from counting as a sip while one at the lips does.
    /// </summary>
    public static class RrMouth
    {
        /// <summary>Stands in for the mouth when set (the harness's fake head; a seated pose).</summary>
        public static Transform Override;

        public static readonly Vector3 FromHead = new Vector3(0f, -0.09f, 0.07f);

        public static Vector3 Position
        {
            get
            {
                if (Override != null)
                {
                    return Override.position;
                }

                return RrPlayer.HeadPosition + RrPlayer.HeadRotation * FromHead;
            }
        }

        public static Quaternion Rotation
        {
            get { return Override != null ? Override.rotation : RrPlayer.HeadRotation; }
        }

        internal static void Shutdown()
        {
            Override = null;
        }
    }
}
