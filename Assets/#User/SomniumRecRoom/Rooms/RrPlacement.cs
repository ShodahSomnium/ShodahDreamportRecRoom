using System;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Where every place is built in the world: all of them at the world origin (0, 0, 0), the arrival room too.
    ///
    /// Rooms used to be kept apart in space - a dorm per presence slot 250 m apart around (-1125, 0, -2000), a
    /// shared instance per instance object 2 km apart from (-5000, 0, 2000), up to about 9 km out with a room's own
    /// areas. That cost precision: a float32 position steps by 2^(e-23) m, so 0.24 mm at 2-4 km, 0.49 mm at
    /// 4-8 km and 0.98 mm at 8-16 km, and vertices, bones, the camera and the XR rig all snapped to that grid every
    /// frame - the headset showed it as everything wobbling. At the origin the step is at most 7.6 um within 128 m
    /// (a whole dorm, the Rec Center, every arena and lobby), 31 um at Paintball's River and Drive-In (450 m) and
    /// 61 um at the farthest quest act (about 800 m): the Rec Center went from 0.24 mm to 7.6 um (32 times finer),
    /// Paintball's lobby from 0.49 mm to 7.6 um (64 times), its River from 0.49 mm to 31 um (16 times).
    ///
    /// Only this client's current room exists (RrTravel destroys the old one before building the next), so rooms
    /// do not need space between them, and players are kept apart by presence instead: what a room shows of other
    /// players depends on the activity and instance number they publish, never on where they stand. Every member
    /// of an instance builds it at the same origin, so positions on the wire are room-local millimetres as before.
    ///
    /// The scene's "Arrival" slab (20 m square, top at y = 0) is at the origin as well; RrTravel hides it and turns
    /// its colliders off while the player is in a room and shows it again when they are back in the arrival room
    /// (between two rooms, or when nothing could be built). Every floor is at y = 0 or close to it, far above the
    /// client's respawn height (-100).
    ///
    /// The dorm slot and the instance object k still matter to the network (which presence record, which instance
    /// state object), just not to placement: <see cref="DormOrigin"/> and <see cref="InstanceOrigin"/> keep their
    /// signatures for callers and return <see cref="RoomOrigin"/>.
    /// </summary>
    public static class RrPlacement
    {
        /// <summary>Where every room's (0, 0, 0) is: the world origin.</summary>
        public static readonly Vector3 RoomOrigin = Vector3.zero;

        /// <summary>
        /// Half the side of the square a room may use around its origin: under 1024 m a float32 steps by at most
        /// 61 um, everywhere in a room.
        /// </summary>
        public const float RoomHalfExtent = 900f;

        /// <summary>Instance objects in the scene ("Rr Net Instance 0..23").</summary>
        public const int InstanceObjects = 24;

        /// <summary>Presence slots in the scene ("Rr Net Player 0..49").</summary>
        public const int PresenceSlots = 50;

        /// <summary>The arrival room's floor centre (the scene's spawn stands 0.9 m above it).</summary>
        public static readonly Vector3 ArrivalOrigin = Vector3.zero;

        /// <summary>Where a player waits in the arrival room between two rooms (feet, facing +z).</summary>
        public static readonly Vector3 ArrivalSpot = new Vector3(0f, 0.05f, 0f);

        /// <summary>
        /// The scene's spawn point (WorldConfig.SpawnPointPos, RrSceneSetup.SpawnPoint): where the client puts the
        /// player on entering the world, after a fall below the respawn height and when they press Respawn.
        /// </summary>
        public static readonly Vector3 SceneSpawn = new Vector3(0f, 0.9f, 0f);

        /// <summary>The dorm of a presence slot: at the origin like every room (the slot no longer moves it).</summary>
        public static Vector3 DormOrigin(int slot)
        {
            return RoomOrigin;
        }

        /// <summary>A shared instance bound to instance object k: at the origin like every room (k no longer moves it).</summary>
        public static Vector3 InstanceOrigin(int instanceObject)
        {
            return RoomOrigin;
        }

        /// <summary>The instance object an activity uses when there is no network (k = activity id - 1).</summary>
        public static int OfflineInstanceObject(RrActivity activity)
        {
            return Mathf.Clamp((int)activity - 1, 0, InstanceObjects - 1);
        }

        /// <summary>
        /// The local player's dorm slot: the presence slot the network claimed; slot 0 offline. Online without a
        /// slot (the claim has not come through) a number past the scene's 50 (50 + LocalId % 50) names it in logs;
        /// it no longer affects where the dorm stands.
        /// </summary>
        public static int LocalDormSlot()
        {
            int slot = RrNet.SlotIndex;
            if (slot >= 0)
            {
                return slot;
            }

            return RrNet.Online ? PresenceSlots + (Mathf.Abs(RrNet.LocalId) % PresenceSlots) : 0;
        }

        /// <summary>
        /// True when a world position is within the room square around an origin. Every room now shares the same
        /// square, so this says nothing about which room (or instance) someone is in: use presence for that.
        /// </summary>
        [Obsolete("Every room is at the world origin: position no longer tells rooms apart. Use presence (activity + instance number).")]
        public static bool Inside(Vector3 origin, Vector3 position, float margin = 0f)
        {
            float half = RoomHalfExtent + margin;
            return Mathf.Abs(position.x - origin.x) <= half && Mathf.Abs(position.z - origin.z) <= half;
        }
    }
}
