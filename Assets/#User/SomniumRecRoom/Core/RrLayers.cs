using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// The physics layers this world puts things on, chosen against the client's own collision matrix (which
    /// the SDK project shares byte for byte; conventions §12):
    ///
    /// - Environment (23) for room shells and floors: the avatar capsule (2) stands on it in desktop and VR, the
    ///   VR far ray hits it, and it collides with our props (10) and projectiles (26).
    /// - Grabbable (10) for props people pick up: the avatar capsule does not touch it, so a ball never shoves
    ///   the player and the player never kicks a ball into orbit; the VR far ray still reaches it; it collides
    ///   with Environment and with other props.
    /// - Click (16) for handles pressed by the client's own pointer (desktop PhysicsRaycaster, VR far ray).
    /// - Bullet (26) and HitVolume (27, "ShootingPlayer"): a pair made for shooters - pellets on 26 hit volumes
    ///   on 27, which collide with nothing else (not the avatar capsule).
    /// - Rec Room's own prefabs arrive on 10 (tools), 17 (broken pieces) and 2 (mouth triggers).
    /// </summary>
    public static class RrLayers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Ui = 5;
        public const int Grabbable = 10;
        public const int Prop = Grabbable;
        public const int Click = 16;
        public const int Environment = 23;
        public const int Bullet = 26;
        public const int HitVolume = 27;
        public const int Vehicle = 28;

        /// <summary>Everything a "what is under this ray" test in the world should hit.</summary>
        public const int WorldMask = (1 << Default) | (1 << Environment) | (1 << Grabbable) | (1 << Click) | (1 << Vehicle);

        /// <summary>Surfaces to stand things on.</summary>
        public const int GroundMask = (1 << Default) | (1 << Environment);

        public static void Set(GameObject go, int layer)
        {
            if (go == null)
            {
                return;
            }

            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                Set(t.GetChild(i).gameObject, layer);
            }
        }

        /// <summary>
        /// Rec Room's layers mapped onto ours when a prefab is spawned without an explicit layer: UI stays UI,
        /// everything else becomes a prop. Room builders pass Environment for pieces people stand on. Rec Room's
        /// layer 2 mouth triggers must not stay on 2, the avatar capsule's layer.
        /// </summary>
        public static int MapRecRoomLayer(int recRoomLayer)
        {
            return recRoomLayer == 5 ? Ui : Grabbable;
        }
    }
}
