using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// A basketball hoop ((Basketballhoop), container 78). Rec Room's hoop has no score trigger - its Basketball script
    /// notices passing the rim - so one is added: a thin box (0.36 x 0.06 x 0.36 m) at the rim centre, measured at
    /// (0, -0.335, +0.371) from the hoop's pivot (rim 0.50 m outer diameter, board 1.83 x 1.22 m).
    /// <see cref="RrBall"/> counts a basket when it enters the box moving down from above and leaves it below.
    /// The rim colliders (Rim.ProxyInner, Rim.ProxyOuter, BrickCollision) are tagged CollisionSfx_Table so the ball's
    /// CollisionAudio "Rim" mapping (BasketballHitRim01) plays on them; every other collider is the backboard.
    /// </summary>
    public sealed class RrHoop : MonoBehaviour
    {
        public static readonly Vector3 RimCenterLocal = new Vector3(0f, -0.335f, 0.371f);
        public static readonly string[] RimNodes = { "Rim.ProxyInner", "Rim.ProxyOuter", "BrickCollision" };

        private static readonly Dictionary<Collider, RrHoop> ByCollider = new Dictionary<Collider, RrHoop>();
        private static readonly List<RrHoop> Hoops = new List<RrHoop>();

        public readonly List<Collider> Rim = new List<Collider>();
        public Collider ScoreTrigger;

        public int Scores { get; private set; }

        /// <summary>A ball went through (the ball, the hoop).</summary>
        public event Action<RrBall> OnScored;

        public static IReadOnlyList<RrHoop> All
        {
            get { return Hoops; }
        }

        /// <summary>World position of the rim's centre.</summary>
        public Vector3 RimCenter
        {
            get { return ScoreTrigger != null ? ScoreTrigger.transform.position : transform.TransformPoint(RimCenterLocal); }
        }

        public void Setup(RrInstance inst)
        {
            Rim.Clear();
            foreach (var nodeName in RimNodes)
            {
                var node = inst != null ? inst.Find(nodeName) : transform.Find(nodeName);
                if (node != null)
                {
                    Rim.AddRange(node.GetComponents<Collider>());
                }
            }

            foreach (var c in Rim)
            {
                RrRefs.SetTag(c, RrRefs.Table);
            }

            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                ByCollider[c] = this;
            }

            if (ScoreTrigger == null)
            {
                var go = new GameObject("Rr Score Trigger");
                go.layer = RrLayers.Grabbable;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = RimCenterLocal;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.36f, 0.06f, 0.36f);
                ScoreTrigger = box;
                ByCollider[box] = this;
            }

            RrTools.Once("Hoop", "setup", "hoop ready: " + Rim.Count + " rim colliders tagged " + RrRefs.Table + ", score trigger at the rim");
        }

        private void OnEnable()
        {
            if (!Hoops.Contains(this))
            {
                Hoops.Add(this);
            }
        }

        private void OnDisable()
        {
            Hoops.Remove(this);
        }

        private void OnDestroy()
        {
            var dead = new List<Collider>();
            foreach (var kv in ByCollider)
            {
                if (kv.Value == this || kv.Key == null)
                {
                    dead.Add(kv.Key);
                }
            }

            foreach (var c in dead)
            {
                ByCollider.Remove(c);
            }
        }

        /// <summary>The hoop a collider belongs to (rim, board or score trigger), or null.</summary>
        public static RrHoop Of(Collider c)
        {
            RrHoop h;
            return c != null && ByCollider.TryGetValue(c, out h) ? h : null;
        }

        public bool IsRim(Collider c)
        {
            return Rim.Contains(c);
        }

        internal void NotifyScored(RrBall ball)
        {
            Scores++;
            if (OnScored != null)
            {
                OnScored(ball);
            }
        }

        internal static void ClearStatics()
        {
            ByCollider.Clear();
            Hoops.Clear();
        }
    }
}
