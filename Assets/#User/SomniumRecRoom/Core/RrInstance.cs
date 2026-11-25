using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// On the root of every spawned Rec Room prefab: the prefab's parsed data (hierarchy, script names, configs)
    /// and the way from that data to the live objects. Unity keeps a prefab's child order when it instantiates,
    /// so a node's child-index path in the file is its path in the instance.
    /// </summary>
    public sealed class RrInstance : MonoBehaviour
    {
        /// <summary>Parsed prefab data; null when the prefab was spawned without it.</summary>
        public RrPrefabData Data;

        /// <summary>The catalog name of the prefab ("(Basketball)").</summary>
        public string PrefabName;

        public Transform NodeOf(RrNodeData node)
        {
            if (node == null)
            {
                return null;
            }

            Transform t = transform;
            if (string.IsNullOrEmpty(node.IndexPath))
            {
                return t;
            }

            foreach (string part in node.IndexPath.Split('/'))
            {
                int i;
                if (!int.TryParse(part, out i) || i < 0 || i >= t.childCount)
                {
                    return null;
                }

                t = t.GetChild(i);
            }

            return t;
        }

        /// <summary>A config reference into this prefab (a GameObject, Transform or component) as a live Transform.</summary>
        public Transform NodeOf(RrTarget target)
        {
            return target.IsInPrefab ? NodeOf(target.Node) : null;
        }

        /// <summary>The first node with this GameObject name, depth first.</summary>
        public Transform Find(string nodeName)
        {
            if (Data != null)
            {
                var n = Data.FindNode(nodeName);
                if (n != null)
                {
                    return NodeOf(n);
                }
            }

            return FindDeep(transform, nodeName);
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name)
            {
                return t;
            }

            for (int i = 0; i < t.childCount; i++)
            {
                var f = FindDeep(t.GetChild(i), name);
                if (f != null)
                {
                    return f;
                }
            }

            return null;
        }

        /// <summary>The fields of the first Rec Room component with this script name ("DrinkableBottle"), or null.</summary>
        public RrConfig Config(string scriptShortName)
        {
            return Data == null ? null : Data.Config(scriptShortName);
        }

        public bool Has(string scriptShortName)
        {
            return Data != null && Data.FindComponent(scriptShortName) != null;
        }

        /// <summary>The live Transform carrying a Rec Room component.</summary>
        public Transform NodeOf(RrComponentData component)
        {
            if (Data == null || component == null)
            {
                return null;
            }

            RrNodeData node;
            return Data.NodesByComponent.TryGetValue(component.PathId, out node) ? NodeOf(node) : null;
        }

        public Bounds WorldBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            bool any = false;
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled)
                {
                    continue;
                }

                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return b;
        }
    }
}
