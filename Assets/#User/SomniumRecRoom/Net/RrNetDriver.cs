using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// Runs <see cref="RrNet"/> once a frame. Created by RrNet.Init under RrMain's world root; early in the frame
    /// (DefaultExecutionOrder -100, as ETS's EtsNet) so rooms and games read this frame's network state.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class RrNetDriver : MonoBehaviour
    {
        private bool _failed;

        private void Update()
        {
            try
            {
                RrNet.Tick();
                _failed = false;
            }
            catch (Exception e)
            {
                if (!_failed)
                {
                    _failed = true;
                    RrLog.Error("Net", "tick failed", e);
                }
            }
        }

        private void OnDestroy()
        {
            RrNet.DriverDestroyed(this);
        }
    }
}
