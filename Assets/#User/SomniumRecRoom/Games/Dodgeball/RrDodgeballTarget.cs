using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Dodgeball
{
    /// <summary>
    /// A practice standee on the red half (Rec Room's cardboard (Standee_Male01) / (Standee_Female03), 1.68 x 1.86 m)
    /// that shots treat as a player through <see cref="RrHitVolumes.AddTarget"/>. Shown in the lobby and in solo
    /// practice, hidden during a real match. A live ball knocks it flat (0.25 s), it stands back up after 3 s.
    /// Our own addition for playing alone; Rec Room's gym used standees only in its tutorial.
    /// </summary>
    public sealed class RrDodgeballTarget : MonoBehaviour
    {
        public const float FallSeconds = 0.25f;
        public const float DownSeconds = 3f;

        public Transform Pivot;
        public Transform Head;
        public int TargetId = -1;
        public int Knocks;

        private float _downAt = -10f;
        private bool _shown;

        public bool Down
        {
            get { return Time.time - _downAt < DownSeconds + FallSeconds; }
        }

        public bool Shown
        {
            get { return _shown; }
        }

        public static RrDodgeballTarget Create(Transform parent, string name, Vector3 localPos, float yaw)
        {
            var root = RrBuild.Group(parent, name, localPos, Quaternion.Euler(0f, yaw, 0f));
            var t = root.AddComponent<RrDodgeballTarget>();
            t.Pivot = RrBuild.Group(root.transform, "Pivot").transform;
            t.Head = RrBuild.Group(t.Pivot, "Head", new Vector3(0f, 1.62f, 0f)).transform;
            // A small base so the standee reads as standing on something.
            RrBuild.Box(root.transform, "Base", new Vector3(0f, 0.03f, 0.05f), new Vector3(0.6f, 0.06f, 0.4f), RrLook.Solid(RrLook.DarkGrey), false);
            root.SetActive(false);
            return t;
        }

        public void Show(bool on)
        {
            if (on == _shown)
            {
                return;
            }

            _shown = on;
            gameObject.SetActive(on);
            if (on && TargetId < 0)
            {
                TargetId = RrHitVolumes.AddTarget(transform, Head);
            }
            else if (!on && TargetId >= 0)
            {
                RrHitVolumes.RemoveTarget(TargetId);
                TargetId = -1;
            }
        }

        public void Knock()
        {
            _downAt = Time.time;
            Knocks++;
        }

        private void Update()
        {
            if (Pivot == null)
            {
                return;
            }

            float t = Time.time - _downAt;
            float angle;
            if (t < FallSeconds)
            {
                angle = Mathf.SmoothStep(0f, -82f, t / FallSeconds);
            }
            else if (t < FallSeconds + DownSeconds)
            {
                angle = -82f;
            }
            else if (t < FallSeconds * 2f + DownSeconds)
            {
                angle = Mathf.SmoothStep(-82f, 0f, (t - FallSeconds - DownSeconds) / FallSeconds);
            }
            else
            {
                angle = 0f;
            }

            Pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        private void OnDestroy()
        {
            if (TargetId >= 0)
            {
                RrHitVolumes.RemoveTarget(TargetId);
                TargetId = -1;
            }
        }
    }
}
