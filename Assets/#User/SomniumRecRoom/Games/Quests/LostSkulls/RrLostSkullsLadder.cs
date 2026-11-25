using System.Globalization;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls
{
    /// <summary>
    /// A rope ladder of the pirate levels ([Ladder_Rope], [Quest_Pirate1_RopeLadder]: a TeleportVolume at its foot whose
    /// sibling TeleportDestination is the top, 2-4 m up; isle_markers.pl writes them as "ladder" markers with dest=). As in
    /// Rec Room, the local player who steps into the foot's volume (0.8 m cube, widened to 1.4 x 2 m here) is moved to
    /// the top, facing the way they looked. Local only (each client moves its own player).
    /// </summary>
    public sealed class RrLostSkullsLadder : MonoBehaviour
    {
        private Vector3 _top;
        private Vector3 _half;
        private float _guard;

        /// <summary>Every ladder marker of the area, under parent (act-local).</summary>
        public static int AddAll(RrJumbotronArea a, Transform parent)
        {
            int n = 0;
            foreach (var m in a.Markers("ladder"))
            {
                Vector3 dest;
                if (!Parse(m.Get("dest"), out dest))
                {
                    continue;
                }

                var go = RrBuild.Group(parent, "Ladder " + m.Name, m.Position);
                var l = go.AddComponent<RrLostSkullsLadder>();
                l._top = a.L(dest);
                Vector3 size;
                l._half = Parse(m.Get("size"), out size) ? Vector3.Max(size * 0.5f, new Vector3(0.7f, 1f, 0.7f)) : new Vector3(0.7f, 1f, 0.7f);
                n++;
            }

            if (n > 0)
            {
                RrLog.Info("LostSkulls", a.Scene + ": " + n + " rope ladders");
            }

            return n;
        }

        private static bool Parse(string s, out Vector3 v)
        {
            v = Vector3.zero;
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            var p = s.Split(',');
            float x, y, z;
            var inv = CultureInfo.InvariantCulture;
            if (p.Length == 3 && float.TryParse(p[0], NumberStyles.Float, inv, out x) && float.TryParse(p[1], NumberStyles.Float, inv, out y) && float.TryParse(p[2], NumberStyles.Float, inv, out z))
            {
                v = new Vector3(x, y, z);
                return true;
            }

            return false;
        }

        private void Update()
        {
            if (Time.time < _guard)
            {
                return;
            }

            Vector3 d = transform.InverseTransformPoint(RrPlayer.FeetPosition);
            if (Mathf.Abs(d.x) > _half.x || Mathf.Abs(d.z) > _half.z || d.y < -_half.y || d.y > _half.y + 0.6f)
            {
                return;
            }

            _guard = Time.time + 1.5f;
            var parent = transform.parent;
            Vector3 top = parent != null ? parent.TransformPoint(_top) : _top;
            RrPlayer.Teleport(top + Vector3.up * 0.05f, RrPlayer.Yaw);
        }
    }
}
