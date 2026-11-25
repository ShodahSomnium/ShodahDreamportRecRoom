using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// Reads the player's input once a frame before any other script's Update (execution order -10000), so every
    /// …Down/…Up edge in <see cref="RrInput"/> and on <see cref="RrHand"/> is seen exactly once, in the frame it
    /// happens, by everything that runs after it. Order matters: the VR hands read their grip/trigger first (RrInput's VR
    /// flags are the right hand's), then RrInput, then the screen hand mirrors RrInput (desktop). On "Rr Player", made by
    /// <see cref="RrPlayer.Init"/>.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class RrInputPump : MonoBehaviour
    {
        private void Update()
        {
            RrXr.Poll();
            bool vr = RrPlayer.InVr;
            RrHand left = RrHand.Left;
            RrHand right = RrHand.Right;
            if (vr)
            {
                if (left != null)
                {
                    left.PollVr();
                }

                if (right != null)
                {
                    right.PollVr();
                }
            }

            RrInput.Poll();
            if (vr)
            {
                return;
            }

            if (right != null)
            {
                right.MirrorScreen();
            }

            if (left != null)
            {
                left.ClearButtons();
            }

            if (RrInput.ThirdPersonDown)
            {
                RrPlayer.ToggleThirdPerson();
            }
        }
    }
}
