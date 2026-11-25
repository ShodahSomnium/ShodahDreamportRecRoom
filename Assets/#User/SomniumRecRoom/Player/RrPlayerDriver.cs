using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// The player's per-frame work after everything else has moved (execution order 10000: after the client's
    /// Cinemachine brain at 100 and every default LateUpdate, like Quake's <c>Q3CameraControl</c>): the client's tablet,
    /// desktop play mode, the editor rig decision, both hands' poses and the remote-player list
    /// (<see cref="RrPlayer.LateTick"/>). On "Rr Player", made by <see cref="RrPlayer.Init"/>.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class RrPlayerDriver : MonoBehaviour
    {
        private int _errors;

        private void LateUpdate()
        {
            try
            {
                RrPlayer.LateTick();
            }
            catch (Exception e)
            {
                // One bad frame must not stop the hands for good; report the first few.
                _errors++;
                if (_errors <= 3)
                {
                    RrLog.Error("Player", "driver LateUpdate threw (#" + _errors + ")", e);
                }
            }
        }

        private void OnDestroy()
        {
            RrPlayer.OnDriverDestroyed();
        }
    }
}
