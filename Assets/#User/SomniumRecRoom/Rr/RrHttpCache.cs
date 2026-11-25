using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>A scene found in Rec Room's download cache, with the cache files to load for it.</summary>
    public sealed class RrCachedScene
    {
        /// <summary>The HTTPCache folder that was scanned.</summary>
        public string CacheDir;

        /// <summary>The scene's addressable path, e.g. "Assets/s/Assets/Rooms/RecCenter/RecCenter-RecCenter_071825.unity".</summary>
        public string ScenePath;

        /// <summary>The scene bundle (full path of its .data file).</summary>
        public string SceneFile;

        /// <summary>Every other cache file the scene needs (its dependency closure, by CAB name), full paths.</summary>
        public readonly List<string> Dependencies = new List<string>();

        /// <summary>CAB names the closure refers to that no cache file holds (the Studio shader bundle, cab-634...).</summary>
        public readonly List<string> MissingCabs = new List<string>();

        /// <summary>Material name to its shader reference as "cab#pathId" (the shader's bundle is usually missing).</summary>
        public readonly Dictionary<string, string> MaterialShaders = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Bytes of the scene bundle and its dependencies.</summary>
        public long Bytes;

        public int FilesScanned;
        public int UnityFsFiles;
        public int FilesReread;
        public int SceneBundles;
        public double Ms;

        /// <summary>Why nothing was found (null when found).</summary>
        public string Error;

        public bool Found
        {
            get { return SceneFile != null && Error == null; }
        }

        /// <summary>The scene name (file name without extension), e.g. "RecCenter-RecCenter_071825".</summary>
        public string SceneName
        {
            get { return ScenePath == null ? null : System.IO.Path.GetFileNameWithoutExtension(ScenePath); }
        }
    }

    /// <summary>
    /// Reads Rec Room's own download cache (%USERPROFILE%\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache) to find a
    /// scene bundle the game downloaded there, by content: the files have hashed names ("1AF.data") and the HTTP header
    /// files next to them carry no URL.
    ///
    /// Each "*.data" that starts with "UnityFS" is opened for its directory (<see cref="RrUnityFs"/>): the serialized
    /// files' names are "CAB-" plus a hash, and every PPtr to another bundle names one of them in the file's externals
    /// ("archive:/CAB-x/CAB-x"). A scene bundle also has a ".sharedAssets" file, whose AssetBundle object lists the
    /// scene path in m_Container. So: the scene bundle is the one whose container key contains the wanted text
    /// ("RecCenter-RecCenter_"), and its dependencies are the closure of its externals mapped to the files holding
    /// those CAB names. Only the serialized files' metadata (types, objects, externals) and two small object kinds
    /// (the AssetBundle object and each Material's name and shader reference) are decompressed, never a texture.
    ///
    /// The user's cache (Sep 2026): 1,011 files, 344 UnityFS (Unity 2021.3.29f1, LZ4HC; one LZMA scene, 35C, which is
    /// skipped), 4 scene bundles. The per-file results are kept in an index keyed by file size and write time, so a
    /// later visit only lists the folder. Pure .NET: run it on <see cref="RrWorker"/>.
    /// </summary>
    public static class RrHttpCache
    {
        public const string IndexVersion = "RRHC 1";

        /// <summary>The cache folder relative to LocalLow.</summary>
        public static readonly string RelativeToLocalLow = System.IO.Path.Combine("Against Gravity", "Rec Room", "HTTPCache");

        private sealed class Entry
        {
            public long Size;
            public long Ticks;
            public bool UnityFs;
            public string Problem;
            public readonly List<string> Cabs = new List<string>();
            public readonly List<string> Externals = new List<string>();
            public readonly List<string> Scenes = new List<string>();
            public List<KeyValuePair<string, string>> Materials;
        }

        /// <summary>
        /// The HTTPCache folder: the environment variable RR_HTTPCACHE (tests), LocalLow next to the given
        /// persistentDataPath (…\LocalLow\Company\Product), then %USERPROFILE%\AppData\LocalLow. Null if none exists.
        /// </summary>
        public static string Locate(string persistentDataPath)
        {
            var candidates = new List<string>();
            try
            {
                string env = Environment.GetEnvironmentVariable("RR_HTTPCACHE");
                if (!string.IsNullOrEmpty(env))
                {
                    candidates.Add(env);
                }
            }
            catch (Exception)
            {
                // No environment access: the other candidates remain.
            }

            try
            {
                if (!string.IsNullOrEmpty(persistentDataPath))
                {
                    var product = new DirectoryInfo(persistentDataPath);
                    var localLow = product.Parent != null ? product.Parent.Parent : null;
                    if (localLow != null)
                    {
                        candidates.Add(System.IO.Path.Combine(localLow.FullName, RelativeToLocalLow));
                    }
                }
            }
            catch (Exception)
            {
                // A malformed path: try the profile folder.
            }

            try
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(profile))
                {
                    candidates.Add(System.IO.Path.Combine(profile, "AppData", "LocalLow", RelativeToLocalLow));
                }

                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(local))
                {
                    var parent = Directory.GetParent(local);
                    if (parent != null)
                    {
                        candidates.Add(System.IO.Path.Combine(parent.FullName, "LocalLow", RelativeToLocalLow));
                    }
                }
            }
            catch (Exception)
            {
                // No special folders here.
            }

            foreach (string c in candidates)
            {
                try
                {
                    if (Directory.Exists(c))
                    {
                        return c;
                    }
                }
                catch (Exception)
                {
                    // Unreadable candidate.
                }
            }

            return null;
        }

        /// <summary>
        /// Scans cacheDir for the scene bundle whose scene path contains sceneKey (newest file wins) and resolves its
        /// dependencies. indexFile (may be null) keeps per-file results between runs. Never throws: failures are in
        /// the result's Error.
        /// </summary>
        public static RrCachedScene FindScene(string cacheDir, string sceneKey, string indexFile)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new RrCachedScene { CacheDir = cacheDir };
            try
            {
                Find(result, cacheDir, sceneKey, indexFile);
            }
            catch (Exception e)
            {
                result.Error = e.GetType().Name + ": " + e.Message;
            }

            result.Ms = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static void Find(RrCachedScene result, string cacheDir, string sceneKey, string indexFile)
        {
            if (string.IsNullOrEmpty(cacheDir) || !Directory.Exists(cacheDir))
            {
                result.Error = "no HTTPCache folder";
                return;
            }

            var index = Load(indexFile);
            var live = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            bool dirty = false;
            foreach (string path in Directory.GetFiles(cacheDir, "*.data"))
            {
                result.FilesScanned++;
                string name = System.IO.Path.GetFileName(path);
                FileInfo fi;
                try
                {
                    fi = new FileInfo(path);
                }
                catch (Exception)
                {
                    continue;
                }

                Entry e;
                if (!index.TryGetValue(name, out e) || e.Size != fi.Length || e.Ticks != fi.LastWriteTimeUtc.Ticks)
                {
                    e = Scan(path, fi);
                    result.FilesReread++;
                    dirty = true;
                }

                live[name] = e;
                if (e.UnityFs)
                {
                    result.UnityFsFiles++;
                }

                if (e.Scenes.Count > 0)
                {
                    result.SceneBundles++;
                }
            }

            if (index.Count != live.Count)
            {
                dirty = true;
            }

            // The scene bundle: newest file whose scene path contains the key.
            string sceneName = null;
            Entry scene = null;
            foreach (var kv in live)
            {
                foreach (string s in kv.Value.Scenes)
                {
                    if (s.IndexOf(sceneKey, StringComparison.OrdinalIgnoreCase) >= 0 && (scene == null || kv.Value.Ticks > scene.Ticks))
                    {
                        scene = kv.Value;
                        sceneName = kv.Key;
                        result.ScenePath = s;
                    }
                }
            }

            if (scene == null)
            {
                result.Error = "no scene matching \"" + sceneKey + "\" among " + result.SceneBundles + " cached scene bundles";
                if (dirty)
                {
                    Save(indexFile, live);
                }

                return;
            }

            // CAB name -> file (newest wins when two files hold the same CAB).
            var byCab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in live)
            {
                foreach (string cab in kv.Value.Cabs)
                {
                    string had;
                    if (!byCab.TryGetValue(cab, out had) || live[had].Ticks < kv.Value.Ticks)
                    {
                        byCab[cab] = kv.Key;
                    }
                }
            }

            // Closure over externals.
            var files = new List<string> { sceneName };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sceneName };
            var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Count; i++)
            {
                foreach (string ext in live[files[i]].Externals)
                {
                    string cab = CabOf(ext);
                    if (cab == null)
                    {
                        continue;
                    }

                    string file;
                    if (!byCab.TryGetValue(cab, out file))
                    {
                        missing.Add(cab);
                        continue;
                    }

                    if (seen.Add(file))
                    {
                        files.Add(file);
                    }
                }
            }

            result.SceneFile = System.IO.Path.Combine(cacheDir, sceneName);
            foreach (string f in files)
            {
                var e = live[f];
                result.Bytes += e.Size;
                if (!string.Equals(f, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    result.Dependencies.Add(System.IO.Path.Combine(cacheDir, f));
                }

                if (e.Problem != null)
                {
                    result.Error = f + ": " + e.Problem;
                }

                if (e.Materials == null)
                {
                    e.Materials = ReadMaterials(System.IO.Path.Combine(cacheDir, f));
                    dirty = true;
                }

                foreach (var m in e.Materials)
                {
                    result.MaterialShaders[m.Key] = m.Value;
                }
            }

            result.MissingCabs.AddRange(missing);
            result.MissingCabs.Sort(StringComparer.Ordinal);
            if (dirty)
            {
                Save(indexFile, live);
            }
        }

        /// <summary>"archive:/CAB-x/CAB-x.sharedAssets" -> "cab-x.sharedassets"; null for built-in files.</summary>
        private static string CabOf(string external)
        {
            if (string.IsNullOrEmpty(external))
            {
                return null;
            }

            int slash = external.LastIndexOf('/');
            string last = slash >= 0 ? external.Substring(slash + 1) : external;
            return last.StartsWith("CAB-", StringComparison.OrdinalIgnoreCase) ? last.ToLowerInvariant() : null;
        }

        private static bool IsSerializedNode(string name)
        {
            return !name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase);
        }

        private static Entry Scan(string path, FileInfo fi)
        {
            var e = new Entry { Size = fi.Length, Ticks = fi.LastWriteTimeUtc.Ticks };
            try
            {
                var head = new byte[8];
                using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64))
                {
                    if (s.Read(head, 0, 8) < 8)
                    {
                        return e;
                    }
                }

                if (head[0] != 'U' || head[1] != 'n' || head[2] != 'i' || head[3] != 't' || head[4] != 'y' || head[5] != 'F' || head[6] != 'S')
                {
                    return e;
                }

                e.UnityFs = true;
                var fs = new RrUnityFs(path);
                bool isScene = false;
                for (int i = 0; i < fs.Nodes.Count; i++)
                {
                    string n = fs.Nodes[i].Name;
                    if (!IsSerializedNode(n))
                    {
                        continue;
                    }

                    e.Cabs.Add(n.ToLowerInvariant());
                    if (n.EndsWith(".sharedAssets", StringComparison.OrdinalIgnoreCase))
                    {
                        isScene = true;
                    }
                }

                for (int i = 0; i < fs.Nodes.Count; i++)
                {
                    if (!IsSerializedNode(fs.Nodes[i].Name))
                    {
                        continue;
                    }

                    var sf = Metadata(fs, i);
                    foreach (string x in sf.Externals)
                    {
                        if (!e.Externals.Contains(x))
                        {
                            e.Externals.Add(x);
                        }
                    }

                    if (isScene)
                    {
                        foreach (string scene in ScenePaths(fs, i, sf))
                        {
                            if (!e.Scenes.Contains(scene))
                            {
                                e.Scenes.Add(scene);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // LZMA blocks (not read by this reader) or a damaged file: remembered, so it is not retried every visit.
                e.Problem = ex.GetType().Name + ": " + ex.Message;
            }

            return e;
        }

        /// <summary>The serialized file's header and metadata only (types, objects, externals).</summary>
        private static RrSerializedFile Metadata(RrUnityFs fs, int node)
        {
            var header = fs.ReadNodeRange(node, 0, 48);
            var r = new RrReader(header, 20, true);
            long metadataSize = r.U32();
            var prefix = fs.ReadNodeRange(node, 0, 48 + metadataSize);
            return new RrSerializedFile(fs.Nodes[node].Name, prefix);
        }

        /// <summary>Reads the objects' bytes [first start, last end) in one range: offset of object o is o.Start - from.</summary>
        private static byte[] ObjectRange(RrUnityFs fs, int node, RrSerializedFile sf, List<RrObjectInfo> objects, out long from)
        {
            from = long.MaxValue;
            long to = 0;
            foreach (var o in objects)
            {
                from = Math.Min(from, o.Start);
                to = Math.Max(to, o.Start + o.Size);
            }

            if (objects.Count == 0)
            {
                from = 0;
                return new byte[0];
            }

            return fs.ReadNodeRange(node, sf.DataOffset + from, to - from);
        }

        /// <summary>AssetBundle objects (class 142): m_Name, m_PreloadTable, then m_Container keys ending in ".unity".</summary>
        private static List<string> ScenePaths(RrUnityFs fs, int node, RrSerializedFile sf)
        {
            var list = new List<string>();
            var bundles = new List<RrObjectInfo>();
            foreach (var o in sf.Objects)
            {
                if (o.ClassId == 142)
                {
                    bundles.Add(o);
                }
            }

            if (bundles.Count == 0)
            {
                return list;
            }

            long from;
            var bytes = ObjectRange(fs, node, sf, bundles, out from);
            foreach (var o in bundles)
            {
                var r = new RrReader(bytes, (int)(o.Start - from), sf.BigEndian);
                r.AlignedString();
                int preload = r.I32();
                r.Position += preload * 12;
                int container = r.I32();
                for (int i = 0; i < container; i++)
                {
                    string key = r.AlignedString();
                    r.Position += 4 + 4 + 12;
                    if (key.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(key);
                    }
                }
            }

            return list;
        }

        /// <summary>Every Material of the bundle's serialized files: name and shader as "cab#pathId" ("self#id" in-file).</summary>
        private static List<KeyValuePair<string, string>> ReadMaterials(string path)
        {
            var list = new List<KeyValuePair<string, string>>();
            try
            {
                var fs = new RrUnityFs(path);
                for (int i = 0; i < fs.Nodes.Count; i++)
                {
                    if (!IsSerializedNode(fs.Nodes[i].Name))
                    {
                        continue;
                    }

                    var sf = Metadata(fs, i);
                    var mats = new List<RrObjectInfo>();
                    foreach (var o in sf.Objects)
                    {
                        if (o.ClassId == 21)
                        {
                            mats.Add(o);
                        }
                    }

                    if (mats.Count == 0)
                    {
                        continue;
                    }

                    long from;
                    var bytes = ObjectRange(fs, i, sf, mats, out from);
                    foreach (var o in mats)
                    {
                        var r = new RrReader(bytes, (int)(o.Start - from), sf.BigEndian);
                        string name = r.AlignedString();
                        int fileId = r.I32();
                        long pathId = r.I64();
                        string cab = fileId == 0 ? "self" : fileId - 1 < sf.Externals.Count ? CabOf(sf.Externals[fileId - 1]) ?? sf.Externals[fileId - 1] : "?";
                        list.Add(new KeyValuePair<string, string>(name, cab + "#" + pathId.ToString(CultureInfo.InvariantCulture)));
                    }
                }
            }
            catch (Exception)
            {
                // Unreadable: no material hints from this file.
            }

            return list;
        }

        // ------------------------------------------------------------------ index file

        private static Dictionary<string, Entry> Load(string file)
        {
            var map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                return map;
            }

            try
            {
                Entry e = null;
                bool first = true;
                foreach (string line in File.ReadAllLines(file, Encoding.UTF8))
                {
                    if (first)
                    {
                        first = false;
                        if (line != IndexVersion)
                        {
                            return map;
                        }

                        continue;
                    }

                    var p = line.Split('\t');
                    if (p.Length < 2)
                    {
                        continue;
                    }

                    switch (p[0])
                    {
                        case "F":
                            e = new Entry
                            {
                                Size = long.Parse(p[2], CultureInfo.InvariantCulture),
                                Ticks = long.Parse(p[3], CultureInfo.InvariantCulture),
                                UnityFs = p[4] == "1",
                                Problem = p.Length > 5 && p[5].Length > 0 ? p[5] : null
                            };
                            map[p[1]] = e;
                            break;
                        case "C":
                            e?.Cabs.Add(p[1]);
                            break;
                        case "E":
                            e?.Externals.Add(p[1]);
                            break;
                        case "S":
                            e?.Scenes.Add(p[1]);
                            break;
                        case "N":
                            if (e != null)
                            {
                                e.Materials = new List<KeyValuePair<string, string>>();
                            }

                            break;
                        case "M":
                            if (e != null && p.Length >= 3)
                            {
                                if (e.Materials == null)
                                {
                                    e.Materials = new List<KeyValuePair<string, string>>();
                                }

                                e.Materials.Add(new KeyValuePair<string, string>(p[1], p[2]));
                            }

                            break;
                    }
                }
            }
            catch (Exception)
            {
                map.Clear();
            }

            return map;
        }

        private static void Save(string file, Dictionary<string, Entry> map)
        {
            if (string.IsNullOrEmpty(file))
            {
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.Append(IndexVersion).Append('\n');
                foreach (var kv in map)
                {
                    var e = kv.Value;
                    sb.Append("F\t").Append(kv.Key).Append('\t').Append(e.Size.ToString(CultureInfo.InvariantCulture)).Append('\t')
                      .Append(e.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(e.UnityFs ? "1" : "0").Append('\t')
                      .Append(e.Problem != null ? e.Problem.Replace('\t', ' ').Replace('\n', ' ') : "").Append('\n');
                    foreach (string c in e.Cabs)
                    {
                        sb.Append("C\t").Append(c).Append('\n');
                    }

                    foreach (string x in e.Externals)
                    {
                        sb.Append("E\t").Append(x).Append('\n');
                    }

                    foreach (string s in e.Scenes)
                    {
                        sb.Append("S\t").Append(s).Append('\n');
                    }

                    if (e.Materials != null)
                    {
                        sb.Append("N\t-\n");
                        foreach (var m in e.Materials)
                        {
                            sb.Append("M\t").Append(m.Key.Replace('\t', ' ')).Append('\t').Append(m.Value).Append('\n');
                        }
                    }
                }

                string dir = System.IO.Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string tmp = file + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(file))
                {
                    File.Delete(file);
                }

                File.Move(tmp, file);
            }
            catch (Exception)
            {
                // The index is an optimisation: the next visit scans again.
            }
        }
    }
}
