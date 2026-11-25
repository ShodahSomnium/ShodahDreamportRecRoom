using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// Paint splash from a thrown grenade or a paint mine, decided by the client that threw it (as every Paintball
    /// hit is decided by the shooter). Numbers from the bundles' configs (rr_shooters.md 3.1): grenade 100 damage,
    /// radius 6 m, full damage to 0.71 of the radius then down to 0.2 at the edge; mine 100, radius 5 m.
    /// The final configs have FriendlyFireSettings.GrenadesCanDamageTeammates = 1, so the splash hits everyone in
    /// the radius, teammates too (never the thrower); the victims' <see cref="RrPaintballRoom.AcceptHit"/> lets
    /// splash tools through and vetoes a teammate's bullets. Everyone in the instance sees the blast
    /// (<see cref="RrPaintballRoom.EvBoom"/>).
    /// </summary>
    public static class RrPaintballBoom
    {
        /// <summary>Tool ids for the kill feed and the teammate rule (outside RrWeaponSpec.KnownTools' range).</summary>
        public const byte GrenadeTool = 240, MineTool = 241;

        public const string GrenadeFx = "(Arena_Grenade_Explosion_VFX)";
        public const string MineFx = "[ProximityMine_Explosion_VFX]";

        private static readonly Collider[] Overlaps = new Collider[64];

        /// <summary>The thrower's client: hits everyone in the radius and tells the instance.</summary>
        public static int Explode(Vector3 point, byte tool)
        {
            bool mine = tool == MineTool;
            float radius = mine ? 5f : 6f;
            int damage = 100;
            float full = mine ? 0.666f : 0.71f;
            Show(point, tool);
            var room = RrPaintballRoom.Current;
            if (room != null)
            {
                Vector3 local = room.transform.InverseTransformPoint(point);
                var b = new byte[7];
                b[0] = tool;
                Put(b, 1, local.x);
                Put(b, 3, local.y);
                Put(b, 5, local.z);
                RrNet.Send(RrPaintballRoom.EvBoom, b);
            }

            RrCombat.SyncPhysics();
            int n = Physics.OverlapSphereNonAlloc(point, radius, Overlaps, RrHitVolume.Mask, QueryTriggerInteraction.Collide);
            var done = new HashSet<int>();
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                RrHitVolume v;
                if (!RrHitVolume.TryGet(Overlaps[i], out v) || v.Enemy != null || v.IsLocalPlayer || v.PlayerId < 0 || v.PlayerId == RrNet.LocalId)
                {
                    continue;
                }

                if (!done.Add(v.PlayerId))
                {
                    continue;
                }

                float d = Vector3.Distance(point, Overlaps[i].ClosestPoint(point));
                float k = d <= radius * full ? 1f : Mathf.Lerp(1f, 0.2f, (d - radius * full) / Mathf.Max(0.01f, radius * (1f - full)));
                RrCombat.SendHit(v.PlayerId, Mathf.Max(1, Mathf.RoundToInt(damage * k)), RrBodyPart.Body, RrCombat.NextSeq(), point, tool);
                hits++;
            }

            RrLog.Info("Paintball", (mine ? "paint mine" : "grenade") + " burst at " + (room != null ? room.transform.InverseTransformPoint(point).ToString("F1") : point.ToString("F1")) +
                                    ": " + hits + " player(s) hit");
            return hits;
        }

        /// <summary>Every client: the blast's paint burst and sound.</summary>
        public static void Show(Vector3 point, byte tool)
        {
            bool mine = tool == MineTool;
            RrImpacts.Fx(mine ? MineFx : GrenadeFx, point, Quaternion.identity, 1f, null);
            RrSound.Play(mine ? "sfx_paintball_paintmine_detonate01" : "Paintball_GrenadeExplode01", point, 0.9f);
        }

        /// <summary>A remote blast (EvBoom): positions travel room-local in decimetres.</summary>
        public static void Receive(byte[] body)
        {
            var room = RrPaintballRoom.Current;
            if (room == null || body == null || body.Length < 7)
            {
                return;
            }

            var local = new Vector3(Get(body, 1), Get(body, 3), Get(body, 5));
            Show(room.transform.TransformPoint(local), body[0]);
        }

        private static void Put(byte[] b, int i, float v)
        {
            int c = Mathf.Clamp(Mathf.RoundToInt(v * 10f), short.MinValue, short.MaxValue);
            b[i] = (byte)(c & 0xff);
            b[i + 1] = (byte)((c >> 8) & 0xff);
        }

        private static float Get(byte[] b, int i)
        {
            return (short)(b[i] | (b[i + 1] << 8)) * 0.1f;
        }
    }
}
