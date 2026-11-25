using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Runs for a visitor who is not on the whitelist, in place of the whole Dreamport: it only ticks the remote-player
    /// list, whose isolation hides and silences every other player (a visitor knows nobody's presence). The isolation
    /// restores everyone when the world unloads.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class RrVisitorDriver : MonoBehaviour
    {
        private void LateUpdate()
        {
            try
            {
                Player.RrRemotePlayers.Tick();
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Access", "visitor-tick", "hiding other players failed: " + e.Message);
            }
        }
    }
}
