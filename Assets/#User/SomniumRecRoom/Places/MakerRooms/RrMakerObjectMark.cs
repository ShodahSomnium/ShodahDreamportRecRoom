using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>
    /// On every maker object's container: its id and room, so a collider or renderer the build tools hit is turned back
    /// into an object id (<see cref="IRrMakerRoomHost.IdOf"/>).
    /// </summary>
    public sealed class RrMakerObjectMark : MonoBehaviour
    {
        public uint Id;
        public RrMakerRoom Room;
    }
}
