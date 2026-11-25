using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// A medieval act door (rr_quests.md 4.0: "a procedural wooden double door in a stone frame"): two plank leaves
    /// on hinges that swing open when the act is cleared, and a pass-through zone behind the doorway. The leaves are
    /// solid (Environment layer) so a closed door blocks the way. The door faces -z of its yaw: players stand on the
    /// -z side, walk through towards +z.
    /// </summary>
    public sealed class RrQuestDoor : MonoBehaviour
    {
        public float Width = 3.2f;
        public float Height = 4f;
        public string OpenClip = "Quest_Sword_ImpactEnvironment01";

        public bool IsOpen { get; private set; }

        private Transform _left, _right;
        private float _k, _target;
        private Light _glow;

        public static RrQuestDoor Create(Transform parent, Vector3 localPos, float yaw, float width = 3.2f, float height = 4f)
        {
            var go = RrBuild.Group(parent, "Quest Door", localPos, Quaternion.Euler(0f, yaw, 0f));
            var d = go.AddComponent<RrQuestDoor>();
            d.Width = width;
            d.Height = height;
            d.BuildParts();
            return d;
        }

        private void BuildParts()
        {
            var t = transform;
            float hw = Width * 0.5f;
            var stone = RrQuestKit.StoneDark ?? RrLook.Solid(RrLook.Grey);
            var wood = RrQuestKit.WoodDark ?? RrLook.Solid(RrLook.DarkWood);
            var gold = RrQuestKit.Gold ?? RrLook.Solid(RrLook.Yellow);
            RrBuild.Box(t, "Jamb L", new Vector3(-hw - 0.4f, Height * 0.5f + 0.25f, 0f), new Vector3(0.8f, Height + 0.5f, 1.0f), stone);
            RrBuild.Box(t, "Jamb R", new Vector3(hw + 0.4f, Height * 0.5f + 0.25f, 0f), new Vector3(0.8f, Height + 0.5f, 1.0f), stone);
            RrBuild.Box(t, "Lintel", new Vector3(0f, Height + 0.35f, 0f), new Vector3(Width + 1.6f, 0.7f, 1.0f), stone);
            RrBuild.Box(t, "Keystone", new Vector3(0f, Height + 0.35f, -0.52f), new Vector3(0.6f, 0.8f, 0.1f), gold, false);
            _left = Leaf(t, "Leaf L", new Vector3(-hw, 0f, 0f), 1f, wood, gold);
            _right = Leaf(t, "Leaf R", new Vector3(hw, 0f, 0f), -1f, wood, gold);
            _glow = RrBuild.PointLight(t, "Glow", new Vector3(0f, Height * 0.6f, 1.5f), new Color(1f, 0.85f, 0.5f), 7f, 0f);
        }

        private Transform Leaf(Transform parent, string name, Vector3 hinge, float side, Material wood, Material trim)
        {
            var pivot = RrBuild.Group(parent, name, hinge).transform;
            float w = Width * 0.5f;
            RrBuild.Box(pivot, "Planks", new Vector3(side * w * 0.5f, Height * 0.5f, 0f), new Vector3(w, Height, 0.18f), wood);
            RrBuild.Box(pivot, "Band Top", new Vector3(side * w * 0.5f, Height * 0.78f, -0.1f), new Vector3(w, 0.14f, 0.04f), trim, false);
            RrBuild.Box(pivot, "Band Low", new Vector3(side * w * 0.5f, Height * 0.22f, -0.1f), new Vector3(w, 0.14f, 0.04f), trim, false);
            RrBuild.Box(pivot, "Ring", new Vector3(side * (w - 0.3f), Height * 0.5f, -0.12f), new Vector3(0.12f, 0.12f, 0.05f), trim, false);
            return pivot;
        }

        /// <summary>Opens or closes (animated over 1.2 s; instant when snap).</summary>
        public void SetOpen(bool open, bool snap = false)
        {
            if (open == IsOpen && !snap)
            {
                return;
            }

            if (open && !IsOpen && !snap && !string.IsNullOrEmpty(OpenClip))
            {
                RrSound.Play(OpenClip, transform.position + Vector3.up * 2f, 0.8f, 0.6f);
            }

            IsOpen = open;
            _target = open ? 1f : 0f;
            if (snap)
            {
                _k = _target;
                Pose();
            }
        }

        /// <summary>
        /// Optional "went through" zone in the door's local space (a quest level's gather platform in front of a door
        /// that stands against a wall, Golden Trophy's [DoorTrigger]); null = the doorway and the 3.5 m behind it.
        /// </summary>
        public Bounds? Zone;

        /// <summary>Is this world position in the doorway or just past it (the "went through" zone)?</summary>
        public bool Through(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            if (Zone.HasValue)
            {
                return Zone.Value.Contains(p);
            }

            return Mathf.Abs(p.x) < Width * 0.5f + 0.3f && p.z > -0.2f && p.z < 3.5f && p.y > -1f && p.y < Height;
        }

        private void Update()
        {
            if (Mathf.Approximately(_k, _target))
            {
                return;
            }

            _k = Mathf.MoveTowards(_k, _target, Time.deltaTime / 1.2f);
            Pose();
        }

        private void Pose()
        {
            float a = 100f * Mathf.SmoothStep(0f, 1f, _k);
            if (_left != null)
            {
                _left.localRotation = Quaternion.Euler(0f, -a, 0f);
            }

            if (_right != null)
            {
                _right.localRotation = Quaternion.Euler(0f, a, 0f);
            }

            if (_glow != null)
            {
                _glow.intensity = 1.6f * _k;
            }
        }
    }
}
