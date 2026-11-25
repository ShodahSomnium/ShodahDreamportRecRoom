using System;
using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>One component of a parsed prefab node.</summary>
    public sealed class RrComponentData
    {
        public long PathId;
        public int ClassId;

        /// <summary>For MonoBehaviours: "Namespace.Class" of the script, or null when unknown.</summary>
        public string Script;
        public bool Enabled = true;

        /// <summary>The short class name ("DrinkableBottle"), or the built-in class id as text.</summary>
        public string ShortName
        {
            get
            {
                if (Script == null)
                {
                    return ClassId == 114 ? "MonoBehaviour" : "#" + ClassId;
                }

                int dot = Script.LastIndexOf('.');
                return dot < 0 ? Script : Script.Substring(dot + 1);
            }
        }
    }

    /// <summary>One GameObject of a parsed prefab, in the prefab's own child order.</summary>
    public sealed class RrNodeData
    {
        public long PathId;
        public long TransformPathId;
        public string Name;
        public int Layer;
        public bool Active;
        public float[] LocalRotation = new float[4];
        public float[] LocalPosition = new float[3];
        public float[] LocalScale = new float[3];
        public RrNodeData Parent;
        public readonly List<RrNodeData> Children = new List<RrNodeData>();
        public readonly List<RrComponentData> Components = new List<RrComponentData>();

        /// <summary>Child-index path from the prefab root, e.g. "0/2/1"; empty for the root.</summary>
        public string IndexPath;
    }

    /// <summary>
    /// A Rec Room prefab as its serialized file describes it: the hierarchy in child order, each
    /// GameObject's components with their script names, and the fields of any component on demand.
    ///
    /// This is the half of the picture Unity cannot give once the prefab is instantiated in
    /// Somnium: Rec Room's MonoBehaviours have no code here, so the instance carries them as
    /// missing scripts with their data unreachable. The data is still in the file, readable
    /// through the type trees, and the instance keeps the same child order as the file - so a
    /// node found here by path id is found in the instance by walking the same child indices
    /// (<see cref="IndexPath"/>). That is how a config's reference to "Cork" or "MouthTrigger"
    /// becomes a Transform in the live object.
    /// </summary>
    public sealed class RrPrefabData
    {
        public readonly RrCatalog Catalog;
        public readonly int Bundle;
        public readonly RrSerializedFile File;
        public readonly RrNodeData Root;
        public readonly Dictionary<long, RrNodeData> NodesByGameObject = new Dictionary<long, RrNodeData>();
        public readonly Dictionary<long, RrNodeData> NodesByTransform = new Dictionary<long, RrNodeData>();
        public readonly Dictionary<long, RrNodeData> NodesByComponent = new Dictionary<long, RrNodeData>();

        public RrPrefabData(RrCatalog catalog, int bundle, RrSerializedFile file, long rootGameObject)
        {
            Catalog = catalog;
            Bundle = bundle;
            File = file;
            Root = ReadNode(rootGameObject, null, "");
        }

        private RrNodeData ReadNode(long goPathId, RrNodeData parent, string indexPath)
        {
            RrObjectInfo go;
            if (!File.TryGet(goPathId, out go))
            {
                throw new KeyNotFoundException("GameObject " + goPathId + " not in " + File.Name);
            }

            var comps = new List<long>();
            int layer;
            string name;
            bool active;
            File.ReadGameObject(go, comps, out layer, out name, out active);
            var node = new RrNodeData { PathId = goPathId, Name = name, Layer = layer, Active = active, Parent = parent, IndexPath = indexPath };
            NodesByGameObject[goPathId] = node;
            var children = new List<long>();
            foreach (long c in comps)
            {
                RrObjectInfo co;
                if (!File.TryGet(c, out co))
                {
                    continue;
                }

                if (co.ClassId == 4 || co.ClassId == 224)
                {
                    long owner;
                    long father;
                    File.ReadTransform(co, out owner, node.LocalRotation, node.LocalPosition, node.LocalScale, children, out father);
                    node.TransformPathId = c;
                    NodesByTransform[c] = node;
                    NodesByComponent[c] = node;
                    continue;
                }

                var data = new RrComponentData { PathId = c, ClassId = co.ClassId };
                if (co.ClassId == 114)
                {
                    long owner;
                    bool enabled;
                    RrPPtr script;
                    string mbName;
                    File.ReadMonoBehaviourHead(co, out owner, out enabled, out script, out mbName);
                    data.Enabled = enabled;
                    int sb = ResolveBundle(script.FileId);
                    data.Script = sb >= 0 ? Catalog.ScriptName(sb, script.PathId) : null;
                }

                node.Components.Add(data);
                NodesByComponent[c] = node;
            }

            for (int i = 0; i < children.Count; i++)
            {
                RrObjectInfo tr;
                if (!File.TryGet(children[i], out tr))
                {
                    continue;
                }

                long childGo;
                long father;
                File.ReadTransform(tr, out childGo, null, null, null, null, out father);
                var child = ReadNode(childGo, node, indexPath.Length == 0 ? i.ToString() : indexPath + "/" + i);
                node.Children.Add(child);
            }

            return node;
        }

        /// <summary>The bundle a file id of this file points into: 0 is this bundle.</summary>
        public int ResolveBundle(int fileId)
        {
            if (fileId == 0)
            {
                return Bundle;
            }

            if (fileId < 1 || fileId > File.Externals.Count)
            {
                return -1;
            }

            return Catalog.BundleByCab(File.Externals[fileId - 1]);
        }

        /// <summary>All components on the prefab whose script short name matches.</summary>
        public List<RrComponentData> FindComponents(string shortName)
        {
            var result = new List<RrComponentData>();
            Collect(Root, shortName, result);
            return result;
        }

        private static void Collect(RrNodeData n, string shortName, List<RrComponentData> result)
        {
            foreach (var c in n.Components)
            {
                if (c.ShortName == shortName)
                {
                    result.Add(c);
                }
            }

            foreach (var k in n.Children)
            {
                Collect(k, shortName, result);
            }
        }

        public RrComponentData FindComponent(string shortName)
        {
            var all = FindComponents(shortName);
            return all.Count > 0 ? all[0] : null;
        }

        /// <summary>The first node (depth first) with this GameObject name.</summary>
        public RrNodeData FindNode(string name)
        {
            return FindNode(Root, name);
        }

        private static RrNodeData FindNode(RrNodeData n, string name)
        {
            if (n.Name == name)
            {
                return n;
            }

            foreach (var k in n.Children)
            {
                var f = FindNode(k, name);
                if (f != null)
                {
                    return f;
                }
            }

            return null;
        }

        /// <summary>A component's serialized fields, typed by <see cref="RrConfig"/>.</summary>
        public RrConfig Config(RrComponentData component)
        {
            RrObjectInfo o;
            if (component == null || !File.TryGet(component.PathId, out o))
            {
                return null;
            }

            return new RrConfig(this, File.Read(o));
        }

        public RrConfig Config(string shortName)
        {
            return Config(FindComponent(shortName));
        }

        /// <summary>
        /// Where a reference in this prefab's data points: a node of this prefab (GameObject,
        /// Transform or component), or an asset in some bundle.
        /// </summary>
        public RrTarget Resolve(RrPPtr p)
        {
            var t = new RrTarget { PPtr = p };
            if (p.IsNull)
            {
                return t;
            }

            t.Bundle = ResolveBundle(p.FileId);
            t.PathId = p.PathId;
            if (p.FileId == 0)
            {
                RrNodeData node;
                if (NodesByGameObject.TryGetValue(p.PathId, out node) || NodesByComponent.TryGetValue(p.PathId, out node))
                {
                    t.Node = node;
                }

                RrObjectInfo o;
                if (File.TryGet(p.PathId, out o))
                {
                    t.ClassId = o.ClassId;
                    if (o.ClassId == 114 || node == null)
                    {
                        t.Name = TryName(File, o);
                    }
                }
            }
            else
            {
                t.ClassId = -1;
            }

            return t;
        }

        private static string TryName(RrSerializedFile f, RrObjectInfo o)
        {
            try
            {
                if (o.ClassId == 1)
                {
                    int layer;
                    string name;
                    bool active;
                    f.ReadGameObject(o, null, out layer, out name, out active);
                    return name;
                }

                if (o.ClassId == 114)
                {
                    long go;
                    bool en;
                    RrPPtr s;
                    string name;
                    f.ReadMonoBehaviourHead(o, out go, out en, out s, out name);
                    return name;
                }

                if (o.ClassId != 4 && o.ClassId != 224)
                {
                    return f.ReadName(o);
                }
            }
            catch (Exception)
            {
                // Not every class starts with a name; the path id still identifies it.
            }

            return null;
        }
    }

    /// <summary>A resolved reference: a node of the prefab, or an asset (bundle + path id).</summary>
    public struct RrTarget
    {
        public RrPPtr PPtr;
        public int Bundle;
        public long PathId;
        public int ClassId;
        public string Name;
        public RrNodeData Node;

        public bool IsNull
        {
            get { return PPtr.IsNull; }
        }

        public bool IsInPrefab
        {
            get { return Node != null; }
        }
    }
}
