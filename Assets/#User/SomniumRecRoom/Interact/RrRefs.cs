using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Rec Room's collision surface tags, kept by collider. CollisionAudio mappings can require the other object's
    /// tag (CollisionSfx_Table on the hoop's rim and tables, CollisionSfx_Paddle, Pool/Ball, the bowling lane, gutter
    /// and backboard). The tags live in the bundles as GameObject tag indices whose name table is in the encrypted
    /// globalgamemanagers, and Unity cannot add tags at runtime, so builders name surfaces here instead
    /// (<see cref="RrHoop"/> tags its rim; a room tags its tables).
    /// </summary>
    public static class RrRefs
    {
        public const string Table = "CollisionSfx_Table";
        public const string Paddle = "CollisionSfx_Paddle";
        public const string PoolBall = "Pool/Ball";
        public const string BowlingLane = "CollisionSfx/Bowling/Lane";
        public const string BowlingGutter = "CollisionSfx/Bowling/Gutter";
        public const string BowlingBackboard = "CollisionSfx/Bowling/Backboard";

        private static readonly Dictionary<Collider, string> Tags = new Dictionary<Collider, string>();

        public static void SetTag(Collider c, string tag)
        {
            if (c == null)
            {
                return;
            }

            if (Tags.Count > 1024)
            {
                Prune();
            }

            if (string.IsNullOrEmpty(tag))
            {
                Tags.Remove(c);
            }
            else
            {
                Tags[c] = tag;
            }
        }

        /// <summary>Tags every collider under a GameObject (inactive ones too).</summary>
        public static void SetTag(GameObject root, string tag)
        {
            if (root == null)
            {
                return;
            }

            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                SetTag(c, tag);
            }
        }

        /// <summary>The Rec Room surface tag of a collider, or null.</summary>
        public static string TagOf(Collider c)
        {
            string tag;
            return c != null && Tags.TryGetValue(c, out tag) ? tag : null;
        }

        private static void Prune()
        {
            var dead = new List<Collider>();
            foreach (var kv in Tags)
            {
                if (kv.Key == null)
                {
                    dead.Add(kv.Key);
                }
            }

            foreach (var c in dead)
            {
                Tags.Remove(c);
            }
        }

        internal static void Shutdown()
        {
            Tags.Clear();
        }
    }
}
