using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>One bundle file: its name, the CAB files it holds, the bundles it depends on.</summary>
    public sealed class RrBundleInfo
    {
        public int Index;
        public string File;
        public long Size;
        public string[] Cabs;
        public int[] Dependencies;

        /// <summary>Addressable container keys of this bundle's explicit assets, by path id.</summary>
        public readonly Dictionary<long, string> ContainerKeyByPathId = new Dictionary<long, string>();
    }

    /// <summary>One findable object: a prefab root, a named asset, or a named MonoBehaviour asset.</summary>
    public struct RrEntry
    {
        public int Bundle;
        public long PathId;
        public int ClassId;
        public string Name;

        /// <summary>For MonoBehaviour assets (TMP fonts, themes, configs): the script, "Namespace.Class".</summary>
        public string Script;

        /// <summary>
        /// The container asset to load to make this object resident: its own key when it is addressable, else the
        /// smallest addressable asset whose preload table lists it. LoaderBundle -1: nothing loads it.
        /// </summary>
        public int LoaderBundle;
        public string LoaderKey;
    }

    /// <summary>
    /// The index over every Rec Room bundle: which bundle holds which prefab, sound, sprite, mesh,
    /// material or font, and which bundles each one needs loaded first.
    ///
    /// Rec Room names its bundles by content hash and its Addressables keys are short internal ids
    /// ("d", "f", "6d5"...), so neither tells what is inside. The objects' own names do: a prefab
    /// root is a GameObject whose Transform has no parent ("(Sandbox_RootBeerBottle)",
    /// "DodgeBall_fbx"), and every asset class starts with m_Name. This index is built by reading
    /// the serialized file of every bundle once - only the blocks that hold it, never the texture
    /// and audio payloads - on a worker thread, and is cached under the world's data folder keyed
    /// by the list of bundle files (their names are content hashes, so the list is the version).
    /// Measured with the research build of this reader: 2,787 bundles in about 20 s cold under
    /// .NET, the cache loads in well under a second.
    ///
    /// Dependencies: every serialized file lists its externals by CAB name ("archive:/CAB-.../CAB-..."),
    /// and every bundle's directory names its CAB, so externals map to bundles without the
    /// Addressables catalog.
    /// </summary>
    public sealed class RrCatalog
    {
        public const int CacheVersion = 4;

        // Classes worth indexing by name (all start with m_Name).
        private static readonly HashSet<int> NamedClasses = new HashSet<int>
        {
            21, 28, 43, 48, 49, 74, 83, 89, 90, 91, 117, 128, 187, 213, 221, 319, 329, 687078895, 241, 243, 273
        };

        public readonly string BundleDirectory;
        public readonly List<RrBundleInfo> Bundles = new List<RrBundleInfo>();
        public readonly List<RrEntry> Entries = new List<RrEntry>();

        /// <summary>MonoScripts: "bundleIndex:pathId" to "Namespace.Class".</summary>
        public readonly Dictionary<long, string> ScriptsByKey = new Dictionary<long, string>();

        private readonly Dictionary<string, int> _bundleByCab = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _bundleByFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<int>> _byName = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> _byNameLower = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        public string Report { get; private set; }

        private RrCatalog(string bundleDirectory)
        {
            BundleDirectory = bundleDirectory;
        }

        /// <summary>Builds or loads the index. Pure work: safe on a worker thread.</summary>
        public static RrCatalog Open(string bundleDirectory, string cacheDirectory, Action<string> progress = null)
        {
            var catalog = new RrCatalog(bundleDirectory);
            var files = Directory.GetFiles(bundleDirectory, "*.bundle");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            string key = ListKey(files);
            string cachePath = string.IsNullOrEmpty(cacheDirectory) ? null : Path.Combine(cacheDirectory, "rr-catalog-" + key + ".bin");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (cachePath != null && File.Exists(cachePath))
            {
                try
                {
                    catalog.Load(cachePath);
                    catalog.Finish();
                    catalog.Report = "catalog: " + catalog.Bundles.Count + " bundles, " + catalog.Entries.Count + " entries from cache in " + watch.ElapsedMilliseconds + " ms";
                    return catalog;
                }
                catch (Exception e)
                {
                    catalog = new RrCatalog(bundleDirectory);
                    if (progress != null)
                    {
                        progress("catalog cache unreadable (" + e.Message + "), rebuilding");
                    }
                }
            }

            catalog.Build(files, progress);
            catalog.Finish();
            if (cachePath != null)
            {
                try
                {
                    Directory.CreateDirectory(cacheDirectory);
                    string tmp = cachePath + ".tmp";
                    catalog.Save(tmp);
                    if (File.Exists(cachePath))
                    {
                        File.Delete(cachePath);
                    }

                    File.Move(tmp, cachePath);
                }
                catch (Exception e)
                {
                    if (progress != null)
                    {
                        progress("catalog cache not written: " + e.Message);
                    }
                }
            }

            catalog.Report = "catalog: " + catalog.Bundles.Count + " bundles, " + catalog.Entries.Count + " entries built in " + watch.ElapsedMilliseconds + " ms";
            return catalog;
        }

        private static string ListKey(string[] files)
        {
            var sb = new StringBuilder();
            sb.Append(CacheVersion).Append('|');
            foreach (var f in files)
            {
                long size = 0;
                try
                {
                    size = new FileInfo(f).Length;
                }
                catch (Exception)
                {
                    // Size is only part of the key.
                }

                sb.Append(Path.GetFileName(f)).Append(':').Append(size).Append('|');
            }

            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder();
                for (int i = 0; i < 8; i++)
                {
                    hex.Append(hash[i].ToString("x2"));
                }

                return hex.ToString();
            }
        }

        private void Build(string[] files, Action<string> progress)
        {
            // Pre-pass: every bundle's directory (a few hundred bytes each), so that externals resolve to
            // bundles while the files are indexed and the preload tables can be followed across bundles.
            var archives = new RrUnityFs[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                var info = new RrBundleInfo { Index = i, File = Path.GetFileNameWithoutExtension(files[i]), Cabs = new string[0] };
                Bundles.Add(info);
                try
                {
                    info.Size = new FileInfo(files[i]).Length;
                    var fs = new RrUnityFs(files[i]);
                    archives[i] = fs;
                    info.Cabs = new string[fs.Nodes.Count];
                    for (int n = 0; n < fs.Nodes.Count; n++)
                    {
                        info.Cabs[n] = fs.Nodes[n].Name;
                        _bundleByCab[fs.Nodes[n].Name] = i;
                    }
                }
                catch (Exception e)
                {
                    if (progress != null)
                    {
                        progress("bundle " + info.File + " unreadable: " + e.Message);
                    }
                }
            }

            _bestLoader = new Dictionary<BundleObject, Loader>();
            var externalsPerBundle = new List<List<string>>();
            int failed = 0;
            for (int i = 0; i < files.Length; i++)
            {
                var info = Bundles[i];
                var externals = new List<string>();
                externalsPerBundle.Add(externals);
                try
                {
                    var fs = archives[i];
                    archives[i] = null;
                    if (fs == null)
                    {
                        continue;
                    }

                    int sn = fs.SerializedNodeIndex;
                    if (sn < 0)
                    {
                        continue;
                    }

                    var sf = new RrSerializedFile(fs.Nodes[sn].Name, fs.ReadNode(sn));
                    foreach (var e in sf.Externals)
                    {
                        externals.Add(Path.GetFileName(e));
                    }

                    IndexFile(i, info, sf);
                }
                catch (Exception e)
                {
                    failed++;
                    if (info.Cabs == null)
                    {
                        info.Cabs = new string[0];
                    }

                    if (progress != null && failed < 20)
                    {
                        progress("bundle " + info.File + " skipped: " + e.Message);
                    }
                }

                if (progress != null && (i + 1) % 250 == 0)
                {
                    progress("indexed " + (i + 1) + " / " + files.Length + " bundles");
                }
            }

            for (int i = 0; i < Bundles.Count; i++)
            {
                var deps = new List<int>();
                foreach (var cab in externalsPerBundle[i])
                {
                    int d;
                    if (_bundleByCab.TryGetValue(cab, out d) && d != i && !deps.Contains(d))
                    {
                        deps.Add(d);
                    }
                }

                Bundles[i].Dependencies = deps.ToArray();
            }

            // Named MonoBehaviour assets: resolve their scripts now that every bundle is known.
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e.ClassId == 114 && e.Script != null && e.Script.StartsWith("@", StringComparison.Ordinal))
                {
                    e.Script = ResolveScriptToken(e.Bundle, e.Script);
                }

                // How to make it resident: itself when it is a container asset, else the smallest container
                // asset (in any bundle) whose preload table lists it.
                string own;
                Loader best;
                if (Bundles[e.Bundle].ContainerKeyByPathId.TryGetValue(e.PathId, out own))
                {
                    e.LoaderBundle = e.Bundle;
                    e.LoaderKey = own;
                }
                else if (_bestLoader.TryGetValue(new BundleObject(e.Bundle, e.PathId), out best))
                {
                    e.LoaderBundle = best.Bundle;
                    e.LoaderKey = best.Key;
                }
                else
                {
                    e.LoaderBundle = -1;
                    e.LoaderKey = null;
                }

                Entries[i] = e;
            }

            _pendingScriptExternals = null;
            _bestLoader = null;
        }

        private struct BundleObject : IEquatable<BundleObject>
        {
            public readonly int Bundle;
            public readonly long PathId;

            public BundleObject(int bundle, long pathId)
            {
                Bundle = bundle;
                PathId = pathId;
            }

            public bool Equals(BundleObject other)
            {
                return Bundle == other.Bundle && PathId == other.PathId;
            }

            public override bool Equals(object obj)
            {
                return obj is BundleObject && Equals((BundleObject)obj);
            }

            public override int GetHashCode()
            {
                return PathId.GetHashCode() * 397 ^ Bundle;
            }
        }

        private struct Loader
        {
            public int Bundle;
            public string Key;
            public int Size;
        }

        // While building: for every object some container asset preloads, the smallest such container.
        private Dictionary<BundleObject, Loader> _bestLoader;

        /// <summary>
        /// Records, for each object in a container asset's preload range, that loading this container makes it
        /// resident. Unity loads an asset's whole preload range - including objects only Rec Room's own
        /// MonoBehaviours reference - so this is the way to reach the many clips, textures and effect prefabs
        /// that are not addressable themselves (measured: after loading the root beer bottle, all 13 clips its
        /// scripts reference were resident).
        /// </summary>
        private void IndexPreload(int bundle, RrSerializedFile sf, Dictionary<string, object> assetBundle)
        {
            var table = assetBundle["m_PreloadTable"] as List<object>;
            var container = assetBundle["m_Container"] as List<object>;
            if (table == null || container == null)
            {
                return;
            }

            var externalBundles = new int[sf.Externals.Count + 1];
            externalBundles[0] = bundle;
            for (int x = 0; x < sf.Externals.Count; x++)
            {
                int b;
                externalBundles[x + 1] = _bundleByCab.TryGetValue(Path.GetFileName(sf.Externals[x]), out b) ? b : -1;
            }

            foreach (var item in container)
            {
                var pair = item as Dictionary<string, object>;
                var info = pair["second"] as Dictionary<string, object>;
                string key = pair["first"] as string;
                int start = Convert.ToInt32(info["preloadIndex"]);
                int size = Convert.ToInt32(info["preloadSize"]);
                for (int j = start; j < start + size && j < table.Count; j++)
                {
                    var p = (RrPPtr)table[j];
                    if (p.PathId == 0 || p.FileId < 0 || p.FileId >= externalBundles.Length || externalBundles[p.FileId] < 0)
                    {
                        continue;
                    }

                    var target = new BundleObject(externalBundles[p.FileId], p.PathId);
                    Loader current;
                    if (!_bestLoader.TryGetValue(target, out current) || size < current.Size)
                    {
                        _bestLoader[target] = new Loader { Bundle = bundle, Key = key, Size = size };
                    }
                }
            }
        }

        // While building: for each bundle, its externals (to resolve MonoBehaviour script references).
        private Dictionary<int, List<string>> _pendingScriptExternals = new Dictionary<int, List<string>>();

        private void IndexFile(int bundle, RrBundleInfo info, RrSerializedFile sf)
        {
            var externals = new List<string>();
            foreach (var e in sf.Externals)
            {
                externals.Add(Path.GetFileName(e));
            }

            _pendingScriptExternals[bundle] = externals;
            var components = new List<long>(8);
            foreach (var o in sf.Objects)
            {
                try
                {
                    if (o.ClassId == 1)
                    {
                        components.Clear();
                        int layer;
                        string name;
                        bool active;
                        sf.ReadGameObject(o, components, out layer, out name, out active);
                        if (components.Count == 0)
                        {
                            continue;
                        }

                        RrObjectInfo tr;
                        if (!sf.TryGet(components[0], out tr) || (tr.ClassId != 4 && tr.ClassId != 224))
                        {
                            continue;
                        }

                        long owner;
                        long parent;
                        sf.ReadTransform(tr, out owner, null, null, null, null, out parent);
                        if (parent == 0)
                        {
                            Entries.Add(new RrEntry { Bundle = bundle, PathId = o.PathId, ClassId = 1, Name = name });
                        }
                    }
                    else if (o.ClassId == 115)
                    {
                        var ms = sf.Read(o);
                        string ns = ms["m_Namespace"] as string;
                        string cls = ms["m_ClassName"] as string;
                        ScriptsByKey[Key(bundle, o.PathId)] = string.IsNullOrEmpty(ns) ? cls : ns + "." + cls;
                    }
                    else if (o.ClassId == 114)
                    {
                        long go;
                        bool enabled;
                        RrPPtr script;
                        string name;
                        sf.ReadMonoBehaviourHead(o, out go, out enabled, out script, out name);
                        if (go == 0 && !string.IsNullOrEmpty(name))
                        {
                            Entries.Add(new RrEntry { Bundle = bundle, PathId = o.PathId, ClassId = 114, Name = name, Script = "@" + script.FileId + ":" + script.PathId });
                        }
                    }
                    else if (o.ClassId == 142)
                    {
                        var ab = sf.Read(o);
                        var container = ab["m_Container"] as List<object>;
                        if (container == null)
                        {
                            continue;
                        }

                        foreach (var item in container)
                        {
                            var pair = item as Dictionary<string, object>;
                            var second = pair["second"] as Dictionary<string, object>;
                            var asset = (RrPPtr)second["asset"];
                            if (asset.FileId == 0 && !info.ContainerKeyByPathId.ContainsKey(asset.PathId))
                            {
                                info.ContainerKeyByPathId[asset.PathId] = pair["first"] as string;
                            }
                        }

                        IndexPreload(bundle, sf, ab);
                    }
                    else if (NamedClasses.Contains(o.ClassId))
                    {
                        string name;
                        if (o.ClassId == 48)
                        {
                            var shader = sf.Read(o);
                            var parsed = shader["m_ParsedForm"] as Dictionary<string, object>;
                            name = parsed != null ? parsed["m_Name"] as string : "?";
                        }
                        else
                        {
                            name = sf.ReadName(o);
                        }

                        Entries.Add(new RrEntry { Bundle = bundle, PathId = o.PathId, ClassId = o.ClassId, Name = name });
                    }
                }
                catch (Exception)
                {
                    // One unreadable object does not spoil the bundle.
                }
            }
        }

        private string ResolveScriptToken(int bundle, string token)
        {
            int colon = token.IndexOf(':');
            int fileId = int.Parse(token.Substring(1, colon - 1));
            long pathId = long.Parse(token.Substring(colon + 1));
            int target = bundle;
            if (fileId > 0)
            {
                List<string> externals;
                if (_pendingScriptExternals == null || !_pendingScriptExternals.TryGetValue(bundle, out externals) || fileId > externals.Count)
                {
                    return null;
                }

                if (!_bundleByCab.TryGetValue(externals[fileId - 1], out target))
                {
                    return null;
                }
            }

            string s;
            return ScriptsByKey.TryGetValue(Key(target, pathId), out s) ? s : null;
        }

        public static long Key(int bundle, long pathId)
        {
            // Path ids are 64-bit; bundles are few. Mix, do not concatenate: collisions only cost a wrong script name.
            return pathId * 31 + bundle * 1000003L;
        }

        private void Finish()
        {
            _byName.Clear();
            _byNameLower.Clear();
            _bundleByFile.Clear();
            _bundleByCab.Clear();
            foreach (var b in Bundles)
            {
                _bundleByFile[b.File] = b.Index;
                foreach (var cab in b.Cabs)
                {
                    _bundleByCab[cab] = b.Index;
                }
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                string n = Entries[i].Name ?? "";
                List<int> list;
                if (!_byName.TryGetValue(n, out list))
                {
                    list = new List<int>(1);
                    _byName[n] = list;
                }

                list.Add(i);
                if (!_byNameLower.TryGetValue(n, out list))
                {
                    list = new List<int>(1);
                    _byNameLower[n] = list;
                }

                list.Add(i);
            }
        }

        // ---- queries ----

        /// <summary>Every entry with this exact name (and class, when classId is not 0).</summary>
        public List<RrEntry> Find(string name, int classId = 0, bool ignoreCase = false)
        {
            var result = new List<RrEntry>();
            List<int> list;
            var map = ignoreCase ? _byNameLower : _byName;
            if (name != null && map.TryGetValue(name, out list))
            {
                foreach (int i in list)
                {
                    if (classId == 0 || Entries[i].ClassId == classId)
                    {
                        result.Add(Entries[i]);
                    }
                }
            }

            return result;
        }

        /// <summary>The first entry with this name and class, preferring container (addressable) assets.</summary>
        public bool TryFind(string name, int classId, out RrEntry entry)
        {
            var all = Find(name, classId);
            if (all.Count == 0)
            {
                all = Find(name, classId, true);
            }

            entry = default(RrEntry);
            if (all.Count == 0)
            {
                return false;
            }

            foreach (var e in all)
            {
                if (Bundles[e.Bundle].ContainerKeyByPathId.ContainsKey(e.PathId))
                {
                    entry = e;
                    return true;
                }
            }

            entry = all[0];
            return true;
        }

        /// <summary>Entries whose name contains the text (case-insensitive), for menus and diagnostics.</summary>
        public List<RrEntry> Search(string text, int classId = 0, int max = 200)
        {
            var result = new List<RrEntry>();
            foreach (var e in Entries)
            {
                if ((classId == 0 || e.ClassId == classId) && e.Name != null && e.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(e);
                    if (result.Count >= max)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        public int BundleByCab(string cab)
        {
            int b;
            return _bundleByCab.TryGetValue(Path.GetFileName(cab), out b) ? b : -1;
        }

        public int BundleByFile(string file)
        {
            int b;
            return _bundleByFile.TryGetValue(file, out b) ? b : -1;
        }

        public string BundlePath(int bundle)
        {
            return Path.Combine(BundleDirectory, Bundles[bundle].File + ".bundle");
        }

        public string ScriptName(int bundle, long pathId)
        {
            string s;
            return ScriptsByKey.TryGetValue(Key(bundle, pathId), out s) ? s : null;
        }

        /// <summary>The bundle and all bundles it needs, dependencies first.</summary>
        public List<int> Closure(int bundle)
        {
            var order = new List<int>();
            var seen = new HashSet<int>();
            Visit(bundle, order, seen);
            return order;
        }

        private void Visit(int bundle, List<int> order, HashSet<int> seen)
        {
            if (!seen.Add(bundle))
            {
                return;
            }

            var deps = Bundles[bundle].Dependencies;
            if (deps != null)
            {
                foreach (int d in deps)
                {
                    Visit(d, order, seen);
                }
            }

            order.Add(bundle);
        }

        // ---- cache ----

        private void Save(string path)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(CacheVersion);
                w.Write(Bundles.Count);
                foreach (var b in Bundles)
                {
                    w.Write(b.File);
                    w.Write(b.Size);
                    w.Write(b.Cabs.Length);
                    foreach (var c in b.Cabs)
                    {
                        w.Write(c);
                    }

                    var deps = b.Dependencies ?? new int[0];
                    w.Write(deps.Length);
                    foreach (int d in deps)
                    {
                        w.Write(d);
                    }

                    w.Write(b.ContainerKeyByPathId.Count);
                    foreach (var kv in b.ContainerKeyByPathId)
                    {
                        w.Write(kv.Key);
                        w.Write(kv.Value ?? "");
                    }
                }

                w.Write(Entries.Count);
                foreach (var e in Entries)
                {
                    w.Write(e.Bundle);
                    w.Write(e.PathId);
                    w.Write(e.ClassId);
                    w.Write(e.Name ?? "");
                    w.Write(e.Script ?? "");
                    w.Write(e.LoaderBundle);
                    w.Write(e.LoaderKey ?? "");
                }

                w.Write(ScriptsByKey.Count);
                foreach (var kv in ScriptsByKey)
                {
                    w.Write(kv.Key);
                    w.Write(kv.Value ?? "");
                }
            }
        }

        private void Load(string path)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (r.ReadInt32() != CacheVersion)
                {
                    throw new InvalidDataException("cache version");
                }

                int nb = r.ReadInt32();
                for (int i = 0; i < nb; i++)
                {
                    var b = new RrBundleInfo { Index = i, File = r.ReadString(), Size = r.ReadInt64() };
                    b.Cabs = new string[r.ReadInt32()];
                    for (int c = 0; c < b.Cabs.Length; c++)
                    {
                        b.Cabs[c] = r.ReadString();
                    }

                    b.Dependencies = new int[r.ReadInt32()];
                    for (int d = 0; d < b.Dependencies.Length; d++)
                    {
                        b.Dependencies[d] = r.ReadInt32();
                    }

                    int nk = r.ReadInt32();
                    for (int k = 0; k < nk; k++)
                    {
                        long pid = r.ReadInt64();
                        b.ContainerKeyByPathId[pid] = r.ReadString();
                    }

                    Bundles.Add(b);
                }

                int ne = r.ReadInt32();
                for (int i = 0; i < ne; i++)
                {
                    var e = new RrEntry { Bundle = r.ReadInt32(), PathId = r.ReadInt64(), ClassId = r.ReadInt32(), Name = r.ReadString(), Script = r.ReadString(), LoaderBundle = r.ReadInt32(), LoaderKey = r.ReadString() };
                    if (e.LoaderKey.Length == 0)
                    {
                        e.LoaderKey = null;
                    }

                    if (e.Script.Length == 0)
                    {
                        e.Script = null;
                    }

                    Entries.Add(e);
                }

                int ns = r.ReadInt32();
                for (int i = 0; i < ns; i++)
                {
                    long k = r.ReadInt64();
                    ScriptsByKey[k] = r.ReadString();
                }
            }
        }
    }
}
