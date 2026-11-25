using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player.Avatars
{
    /// <summary>
    /// <see cref="RrAvatars"/>' per-frame work, after the player driver (order 10000: hands, remote list, isolation)
    /// so the avatars use this frame's poses and the Somnium-avatar hiding runs after isolation's. On "Rr Avatars".
    /// </summary>
    [DefaultExecutionOrder(10020)]
    public sealed class RrAvatarDriver : MonoBehaviour
    {
        private void LateUpdate()
        {
            RrAvatars.LateTick();
        }

        private void OnDestroy()
        {
            RrAvatars.OnDriverDestroyed();
        }
    }
}
