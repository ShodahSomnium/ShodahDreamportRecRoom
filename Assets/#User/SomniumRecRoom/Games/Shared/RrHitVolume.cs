using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Where a shot or a swing landed. The numbers are Rec Room's own BodyPart values as its weapons store them in
    /// playerDamageSettings: 0 is the head (the fandom "Weapon Damage" table matches the bundle's head column),
    /// 1 the torso. Rec Room turned off hits on arms, legs and feet for full-body avatars (2025 notes), so only
    /// these two have volumes; the other stored parts (2, 3, 5, 6, 7, -1) all carry the body value anyway.
    /// WeakSpot is ours: the back target of a barrel knight or the battery of an assassin bot (rr_quests.md 3.4).
    /// </summary>
    public enum RrBodyPart : byte
    {
        Head = 0,
        Body = 1,
        WeakSpot = 2,
        None = 255
    }

    /// <summary>
    /// One of our own hit colliders on layer 27 (ShootingPlayer): a player's head or torso, or part of an enemy.
    /// The layer collides only with 3, 6, 7, 26 and 31 in the client's matrix (conventions §12), so these never
    /// touch the avatar's CharacterController (layer 2) or the room; they are triggers because layer 7 (the VR
    /// hands' physical boxes) would otherwise bump a player's own volumes. Queries against them pass
    /// QueryTriggerInteraction.Collide. Nothing here is ever a component on the Somnium avatar: the volumes are
    /// separate GameObjects that copy the Bridge body transforms (<see cref="RrHitVolumes"/>).
    /// </summary>
    public sealed class RrHitVolume : MonoBehaviour
    {
        /// <summary>Layer mask of every hit volume.</summary>
        public const int Mask = 1 << RrLayers.HitVolume;

        /// <summary>The player this volume belongs to (Fusion PlayerId; practice targets use ids from 60000), or -1.</summary>
        public int PlayerId = -1;

        /// <summary>The enemy this volume belongs to, or null.</summary>
        public RrEnemy Enemy;

        public RrBodyPart Part = RrBodyPart.Body;

        /// <summary>True for the local player's own volumes (enemy attacks test these; the player's own shots skip them).</summary>
        public bool IsLocalPlayer;

        [System.NonSerialized] public Collider Collider;

        private static readonly Dictionary<int, RrHitVolume> ByCollider = new Dictionary<int, RrHitVolume>();

        public bool IsPlayer
        {
            get { return PlayerId >= 0 && Enemy == null; }
        }

        /// <summary>Makes a trigger volume on layer 27 under a parent.</summary>
        public static RrHitVolume Create(Transform parent, string name, RrBodyPart part, bool capsule, float radius, float height)
        {
            var go = new GameObject(name);
            go.layer = RrLayers.HitVolume;
            go.transform.SetParent(parent, false);
            Collider c;
            if (capsule)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.direction = 1;
                cc.radius = radius;
                cc.height = Mathf.Max(height, radius * 2f);
                c = cc;
            }
            else
            {
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = radius;
                c = sc;
            }

            c.isTrigger = true;
            var v = go.AddComponent<RrHitVolume>();
            v.Part = part;
            v.Collider = c;
            ByCollider[c.GetInstanceID()] = v;
            return v;
        }

        /// <summary>The volume behind a collider a query returned (no allocation, no GetComponent).</summary>
        public static bool TryGet(Collider c, out RrHitVolume volume)
        {
            volume = null;
            if (c == null)
            {
                return false;
            }

            if (ByCollider.TryGetValue(c.GetInstanceID(), out volume) && volume != null)
            {
                return true;
            }

            volume = c.GetComponent<RrHitVolume>();
            if (volume != null)
            {
                ByCollider[c.GetInstanceID()] = volume;
                return true;
            }

            return false;
        }

        private void OnDestroy()
        {
            if (Collider != null)
            {
                ByCollider.Remove(Collider.GetInstanceID());
            }
        }

        /// <summary>Statics survive a world re-entry: cleared with the combat kit.</summary>
        internal static void ResetStatics()
        {
            ByCollider.Clear();
        }
    }
}
