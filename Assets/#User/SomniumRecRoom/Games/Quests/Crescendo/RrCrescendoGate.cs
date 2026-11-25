using System.Globalization;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Crescendo
{
    /// <summary>
    /// One of Rec Room's mid-level gates ([Quest_Dracula1_MidLevelGate], rr_latest_rgames_story.md 7.1: "3 waves of
    /// Pumpkin Spicers, then a gate"). In the level a gate is an iron door (LockableDoor, mesh fence_midgate) with an
    /// AreaBlockingGeo box collider over the whole area behind it, which Rec Room removes when the gate unlocks. The
    /// converter took both out of the level data (DN/iter13/crescendo_tools/cbm_gates.pl) and left their measured poses
    /// as the gate_door / gate_block markers; this component builds them back: the blocker as an invisible box collider
    /// of the measured size, and the door as dark iron bars across the doorway (hinge-to-centre half width, 2.8 m x the
    /// door's scale tall) that rise into the lintel when the gate opens. Open and closed come from the quest's hazard
    /// bits (the authority opens a gate once the waves before it are beaten), so every client agrees.
    /// </summary>
    public sealed class RrCrescendoGate : MonoBehaviour
    {
        public const string OpenClip = "SR_GateDown_02";

        /// <summary>The level's name of the gate ([Quest_Dracula1_MidLevelGate] (1)...).</summary>
        public string GateName;

        public bool IsOpen { get; private set; }

        private BoxCollider _blocker;
        private Transform _bars;
        private float _height = 2.8f;
        private float _k, _target;
        private float _bottom;

        /// <summary>
        /// Builds a gate under parent (the act root) from its door marker (act-local pose, scale= and hinge= in scene
        /// coordinates) and its blocker marker (act-local centre and rotation, size=). anchor is the area's scene origin
        /// (the hinge is a scene point). Null without a door marker.
        /// </summary>
        public static RrCrescendoGate Create(Transform parent, RrSceneMarker door, RrSceneMarker block, Vector3 anchor)
        {
            if (door == null)
            {
                return null;
            }

            var go = RrBuild.Group(parent, "Gate " + door.Name, door.Position, door.Rotation);
            var g = go.AddComponent<RrCrescendoGate>();
            g.GateName = door.Name;
            float scale = Mathf.Clamp(Num(door.Get("scale"), 1f), 0.3f, 3f);
            Vector3 hinge;
            float halfWidth = 1.5f * scale;
            float bottom = -0.8f * scale;
            if (TryVec(door.Get("hinge"), out hinge))
            {
                Vector3 local = Quaternion.Inverse(door.Rotation) * (hinge - anchor - door.Position);
                if (Mathf.Abs(local.z) > 0.3f)
                {
                    halfWidth = Mathf.Abs(local.z);
                }

                bottom = local.y;
            }

            g._height = 2.8f * scale;
            g.BuildBars(halfWidth, bottom);
            if (block != null)
            {
                Vector3 size;
                if (!TryVec(block.Get("size"), out size))
                {
                    size = new Vector3(6f, 20f, 10f);
                }

                var b = RrBuild.Group(parent, "Gate Blocker " + block.Name, block.Position, block.Rotation);
                b.layer = RrLayers.Environment;
                g._blocker = b.AddComponent<BoxCollider>();
                g._blocker.size = size;
            }

            g.SetOpen(false, true);
            return g;
        }

        private void BuildBars(float halfWidth, float bottom)
        {
            var iron = RrLook.Solid(new Color(0.11f, 0.105f, 0.1f));
            _bottom = bottom;
            _bars = RrBuild.Group(transform, "Bars", new Vector3(0f, bottom, 0f)).transform;
            int n = Mathf.Clamp(Mathf.RoundToInt(halfWidth * 2f / 0.28f), 3, 24);
            for (int i = 0; i <= n; i++)
            {
                float z = Mathf.Lerp(-halfWidth, halfWidth, i / (float)n);
                RrBuild.Box(_bars, "Bar", new Vector3(0f, _height * 0.5f, z), new Vector3(0.06f, _height, 0.06f), iron, false);
            }

            RrBuild.Box(_bars, "Rail Top", new Vector3(0f, _height - 0.1f, 0f), new Vector3(0.1f, 0.12f, halfWidth * 2f), iron, false);
            RrBuild.Box(_bars, "Rail Low", new Vector3(0f, 0.35f, 0f), new Vector3(0.1f, 0.1f, halfWidth * 2f), iron, false);
        }

        /// <summary>Opens (the bars rise over 1.4 s, the blocker goes) or closes; snap skips the animation and sound.</summary>
        public void SetOpen(bool open, bool snap = false)
        {
            if (open == IsOpen && !snap)
            {
                return;
            }

            if (open && !IsOpen && !snap)
            {
                RrSound.Play(OpenClip, transform.position + Vector3.up * 1.5f, 0.8f);
            }

            IsOpen = open;
            _target = open ? 1f : 0f;
            if (_blocker != null)
            {
                _blocker.enabled = !open;
            }

            if (snap)
            {
                _k = _target;
                Pose();
            }
        }

        private void Update()
        {
            if (Mathf.Approximately(_k, _target))
            {
                return;
            }

            _k = Mathf.MoveTowards(_k, _target, Time.deltaTime / 1.4f);
            Pose();
        }

        private void Pose()
        {
            if (_bars != null)
            {
                _bars.localPosition = new Vector3(0f, _bottom + Mathf.SmoothStep(0f, 1f, _k) * (_height + 0.2f), 0f);
            }
        }

        private static float Num(string s, float fallback)
        {
            if (string.IsNullOrEmpty(s))
            {
                return fallback;
            }

            int comma = s.IndexOf(',');
            float v;
            return float.TryParse(comma > 0 ? s.Substring(0, comma) : s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        /// <summary>"x,y,z" to a vector.</summary>
        public static bool TryVec(string s, out Vector3 v)
        {
            v = Vector3.zero;
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            var p = s.Split(',');
            float x, y, z;
            if (p.Length == 3 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                v = new Vector3(x, y, z);
                return true;
            }

            return false;
        }
    }
}
