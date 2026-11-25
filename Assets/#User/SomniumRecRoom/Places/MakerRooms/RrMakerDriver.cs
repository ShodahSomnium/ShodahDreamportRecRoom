using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>
    /// "Rr Maker Rooms" under the world root: ticks <see cref="RrMakerRooms"/> (the catalogue broadcast, transfers, pending
    /// downloads) and runs its coroutines. Destroyed with the world; <see cref="RrMakerRooms.Shutdown"/> clears the statics.
    /// </summary>
    public sealed class RrMakerDriver : MonoBehaviour
    {
        private void Update()
        {
            RrMakerRooms.Tick();
        }

        private void OnDestroy()
        {
            RrMakerRooms.DriverGone(this);
        }
    }
}
