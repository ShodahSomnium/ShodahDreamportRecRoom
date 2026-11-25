using System;
using System.Collections.Generic;
using System.IO;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>
    /// Which bundles Unity must have loaded for an object to come out whole: meshes, materials,
    /// shaders, textures, clips, sub-prefabs - and nothing that is there only for Rec Room's code.
    ///
    /// The bundles' own dependency lists are far wider than that. Measured: the root beer bottle's
    /// bundle lists dependencies that close over 128 bundles, the sandbox bottle's over 398,
    /// because every MonoBehaviour references its MonoScript and its configs reference half the
    /// game (quest potions, damage types, subtitles...). None of that matters here - the scripts
    /// have no code in Somnium and their data is read by <see cref="RrPrefabData"/> instead - so
    /// this walks references the way Unity's loader resolves them, but skips MonoBehaviours (and
    /// MonoScripts) unless their script is one Somnium can actually run (UnityEngine.UI, TMP),
    /// when <see cref="IncludeUiScripts"/> is set.
    ///
    /// Pure work: reads serialized files through a small cache; safe on a worker thread as long
    /// as each thread has its own RrReach.
    /// </summary>
    public sealed class RrReach
    {
        public readonly RrCatalog Catalog;
        public bool IncludeUiScripts;
        private readonly Dictionary<int, RrSerializedFile> _files = new Dictionary<int, RrSerializedFile>();
        private readonly LinkedList<int> _lru = new LinkedList<int>();
        public int MaxCachedFiles = 48;

        public RrReach(RrCatalog catalog)
        {
            Catalog = catalog;
        }

        public RrSerializedFile File(int bundle)
        {
            RrSerializedFile f;
            if (_files.TryGetValue(bundle, out f))
            {
                _lru.Remove(bundle);
                _lru.AddFirst(bundle);
                return f;
            }

            var fs = new RrUnityFs(Catalog.BundlePath(bundle));
            int sn = fs.SerializedNodeIndex;
            if (sn < 0)
            {
                throw new InvalidDataException("no serialized file in bundle " + Catalog.Bundles[bundle].File);
            }

            f = new RrSerializedFile(fs.Nodes[sn].Name, fs.ReadNode(sn));
            _files[bundle] = f;
            _lru.AddFirst(bundle);
            while (_lru.Count > MaxCachedFiles)
            {
                int old = _lru.Last.Value;
                _lru.RemoveLast();
                _files.Remove(old);
            }

            return f;
        }

        /// <summary>The bundles needed for one object, in an order where dependencies come first.</summary>
        public List<int> Bundles(int bundle, long pathId)
        {
            var needed = new HashSet<int>();
            var visited = new HashSet<long>();
            var queue = new Queue<KeyValuePair<int, long>>();
            queue.Enqueue(new KeyValuePair<int, long>(bundle, pathId));
            needed.Add(bundle);
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                long key = RrCatalog.Key(item.Key, item.Value);
                if (!visited.Add(key))
                {
                    continue;
                }

                RrSerializedFile f;
                try
                {
                    f = File(item.Key);
                }
                catch (Exception)
                {
                    continue;
                }

                RrObjectInfo o;
                if (!f.TryGet(item.Value, out o))
                {
                    continue;
                }

                if (o.ClassId == 115)
                {
                    continue;
                }

                if (o.ClassId == 114)
                {
                    int scriptBundle;
                    if (!Wanted(item.Key, f, o, out scriptBundle))
                    {
                        continue;
                    }

                    // A kept (UI) behaviour binds through its MonoScript, which may live in another bundle.
                    needed.Add(scriptBundle);
                }

                Dictionary<string, object> fields;
                try
                {
                    fields = f.Read(o);
                }
                catch (Exception)
                {
                    continue;
                }

                var refs = new List<RrPPtr>();
                // A Transform leads down to its children's GameObjects, and through them to their components.
                bool isTransform = o.ClassId == 4 || o.ClassId == 224;
                if (isTransform)
                {
                    object owner;
                    if (fields.TryGetValue("m_GameObject", out owner) && owner is RrPPtr)
                    {
                        refs.Add((RrPPtr)owner);
                    }
                }

                Collect(fields, refs);
                foreach (var p in refs)
                {
                    if (p.IsNull)
                    {
                        continue;
                    }

                    int target = item.Key;
                    if (p.FileId > 0)
                    {
                        if (p.FileId > f.Externals.Count)
                        {
                            continue;
                        }

                        target = Catalog.BundleByCab(f.Externals[p.FileId - 1]);
                        if (target < 0)
                        {
                            continue;
                        }
                    }

                    // Only follow into another bundle when the target is something Unity itself resolves.
                    RrSerializedFile tf;
                    try
                    {
                        tf = target == item.Key ? f : File(target);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    RrObjectInfo to;
                    if (!tf.TryGet(p.PathId, out to))
                    {
                        continue;
                    }

                    int ignored;
                    if (to.ClassId == 115 || to.ClassId == 114 && !Wanted(target, tf, to, out ignored))
                    {
                        continue;
                    }

                    needed.Add(target);
                    queue.Enqueue(new KeyValuePair<int, long>(target, p.PathId));
                }
            }

            // Order: dependencies first, following the bundles' own dependency lists.
            var order = new List<int>();
            var seen = new HashSet<int>();
            foreach (int b in needed)
            {
                Order(b, needed, order, seen);
            }

            return order;
        }

        private void Order(int b, HashSet<int> needed, List<int> order, HashSet<int> seen)
        {
            if (!seen.Add(b))
            {
                return;
            }

            var deps = Catalog.Bundles[b].Dependencies;
            if (deps != null)
            {
                foreach (int d in deps)
                {
                    if (needed.Contains(d))
                    {
                        Order(d, needed, order, seen);
                    }
                }
            }

            order.Add(b);
        }

        private bool Wanted(int bundle, RrSerializedFile f, RrObjectInfo o, out int scriptBundle)
        {
            scriptBundle = -1;
            if (!IncludeUiScripts)
            {
                return false;
            }

            try
            {
                long go;
                bool en;
                RrPPtr script;
                string name;
                f.ReadMonoBehaviourHead(o, out go, out en, out script, out name);
                int sb = bundle;
                if (script.FileId > 0)
                {
                    sb = script.FileId <= f.Externals.Count ? Catalog.BundleByCab(f.Externals[script.FileId - 1]) : -1;
                }

                scriptBundle = sb;
                string s = sb >= 0 ? Catalog.ScriptName(sb, script.PathId) : null;
                return s != null && (s.StartsWith("UnityEngine.UI.", StringComparison.Ordinal) || s.StartsWith("TMPro.", StringComparison.Ordinal));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Collect(object v, List<RrPPtr> refs)
        {
            if (v is RrPPtr)
            {
                refs.Add((RrPPtr)v);
                return;
            }

            var d = v as Dictionary<string, object>;
            if (d != null)
            {
                foreach (var kv in d)
                {
                    // Editor-only back references, and the owner pointer every component carries.
                    if (kv.Key == "m_GameObject" || kv.Key == "m_Father" || kv.Key == "m_PrefabInstance" || kv.Key == "m_PrefabAsset" || kv.Key == "m_CorrespondingSourceObject" || kv.Key == "m_Script")
                    {
                        continue;
                    }

                    Collect(kv.Value, refs);
                }

                return;
            }

            var l = v as List<object>;
            if (l != null)
            {
                foreach (var item in l)
                {
                    Collect(item, refs);
                }
            }
        }
    }
}
