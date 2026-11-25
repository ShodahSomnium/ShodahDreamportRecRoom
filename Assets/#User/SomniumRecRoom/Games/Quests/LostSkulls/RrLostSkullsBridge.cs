using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls
{
    /// <summary>
    /// The High Seas boarding bridge (level65 Encounter2/LockedDoors/DrawbridgeDoor): Rec Room's "BoardingBridge (up)",
    /// 13 planks and platforms hinged at the other ship's rail (-143.09, 5.70, -39.16) and standing 70 degrees up, with the
    /// "Blast Target to Drop Bridge" [Paintball_Target] on it (8.1: "Hit a target with a cannon to lower a bridge"). The
    /// level's raised planks are left out (RrLostSkullsActs.HighSeas' drop box) and rebuilt as one 1.7 m wide deck
    /// (Quest_Pirate1_Platform's width) from the hinge to BridgeLands (-143.10, 6.86, -26.53) on the team's ship, with a
    /// red and white target; when the act's bridge bit is set it falls onto the team's rail in 1.4 s.
    /// </summary>
    public sealed class RrLostSkullsBridge : MonoBehaviour
    {
        public const float Width = 1.7f;
        public const float RaisedPitch = 70f;
        public const string LandClip = "Quest_Pirate1_WoodShatter01";

        private float _lowPitch, _yaw, _k;
        private bool _down;
        private Transform _target;

        public bool IsDown
        {
            get { return _down; }
        }

        /// <summary>The target's centre (world), on the bridge.</summary>
        public Vector3 TargetWorld
        {
            get { return _target != null ? _target.position : transform.position; }
        }

        /// <summary>The bridge at its hinge (act-local), landing at land; the target where the level has it (raised pose).</summary>
        public static RrLostSkullsBridge Create(Transform parent, Vector3 hinge, Vector3 land, Vector3 target)
        {
            Vector3 d = land - hinge;
            float len = Mathf.Max(1f, d.magnitude);
            var go = RrBuild.Group(parent, "Boarding Bridge", hinge);
            var b = go.AddComponent<RrLostSkullsBridge>();
            b._yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            // Quaternion.Euler(x, ...) tips +z down for x > 0, so the landing end rises for a negative pitch.
            b._lowPitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            var wood = RrLook.Solid(new Color(0.45f, 0.31f, 0.18f));
            var dark = RrLook.Solid(new Color(0.28f, 0.19f, 0.11f));
            var t = go.transform;
            RrBuild.Box(t, "Deck", new Vector3(0f, 0f, len * 0.5f), new Vector3(Width, 0.1f, len), wood, true);
            RrBuild.Box(t, "Rail L", new Vector3(-Width * 0.5f + 0.05f, 0.1f, len * 0.5f), new Vector3(0.1f, 0.12f, len), dark, false);
            RrBuild.Box(t, "Rail R", new Vector3(Width * 0.5f - 0.05f, 0.1f, len * 0.5f), new Vector3(0.1f, 0.12f, len), dark, false);
            for (int i = 1; i < 6; i++)
            {
                RrBuild.Box(t, "Cleat " + i, new Vector3(0f, 0.07f, len * i / 6f), new Vector3(Width - 0.2f, 0.04f, 0.08f), dark, false);
            }

            b._k = 0f;
            b.Apply();
            // The target, placed in the raised pose where the level has it, facing the team's ship (back along the bridge).
            var tg = RrBuild.Group(t, "Target").transform;
            tg.position = go.transform.parent != null ? go.transform.parent.TransformPoint(target) : target;
            Vector3 face = -new Vector3(d.x, 0f, d.z).normalized;
            if (go.transform.parent != null)
            {
                face = go.transform.parent.TransformDirection(face);
            }

            tg.rotation = Quaternion.FromToRotation(Vector3.up, face);
            RrBuild.Cylinder(tg, "Ring Outer", Vector3.zero, 0.55f, 0.04f, RrLook.Solid(new Color(0.85f, 0.12f, 0.1f)), false);
            RrBuild.Cylinder(tg, "Ring White", new Vector3(0f, 0.01f, 0f), 0.38f, 0.04f, RrLook.Solid(new Color(0.95f, 0.93f, 0.88f)), false);
            RrBuild.Cylinder(tg, "Bullseye", new Vector3(0f, 0.02f, 0f), 0.18f, 0.04f, RrLook.Emissive(new Color(0.95f, 0.15f, 0.1f), 1.2f), false);
            b._target = tg;
            RrLog.Info("LostSkulls", "boarding bridge at " + hinge.ToString("F2") + ", " + len.ToString("F1") + " m to " + land.ToString("F2") + " (pitch " + b._lowPitch.ToString("F1") + " down, " + RaisedPitch + " up)");
            return b;
        }

        /// <summary>Every client, every frame: down while the act's bridge bit is set (a new run raises it again).</summary>
        public void Show(bool down)
        {
            if (down != _down)
            {
                _down = down;
                if (!down)
                {
                    _k = 0f;
                    Apply();
                }
            }

            if (down && _k < 1f)
            {
                _k = Mathf.MoveTowards(_k, 1f, Time.deltaTime / 1.4f);
                Apply();
                if (_k >= 1f)
                {
                    RrSound.Play(LandClip, transform.TransformPoint(new Vector3(0f, 0f, 12f)), 1f, 0.8f);
                    RrFx.Spawn(RrLostSkullsCannon.SmokeFx, transform.TransformPoint(new Vector3(0f, 0f, 12f)), Quaternion.identity, 2f);
                }
            }
        }

        private void Apply()
        {
            // Falls like a drawbridge: slow at first, fast at the end.
            float e = _k * _k;
            transform.localRotation = Quaternion.Euler(Mathf.Lerp(-RaisedPitch, _lowPitch, e), _yaw, 0f);
        }
    }
}
