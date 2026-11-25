using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's bundles through Unity's own loader, as measured in the 6000.3 load test (rr_runtime_facts.md):
    ///
    /// - Only Addressables container entries can be loaded by the API (LoadAsset(key)); loading by object name
    ///   and LoadAllAssets of a type return nothing for the rest.
    /// - Loading a container asset makes its whole preload range resident - including clips, textures and
    ///   effect prefabs that only Rec Room's own (inert) scripts reference - and resident assets are found by
    ///   name with Resources.FindObjectsOfTypeAll.
    ///
    /// So every catalog entry carries a loader (its own key, or the smallest container asset that preloads it),
    /// and a load is: work out on the worker which bundles Unity needs for the loader and the target
    /// (<see cref="RrReach"/>: meshes, materials, shaders, textures, clips - not Rec Room's script graph), open
    /// them asynchronously, LoadAssetAsync the loader key, then take the target itself (container) or find it
    /// resident by name. Everything is unloaded in OnDestroy (the client keeps assemblies, not our bundles,
    /// between visits).
    ///
    /// <b>Room scopes.</b> Only the current room exists, so what a room alone used can go when it is left. RrTravel
    /// sets <see cref="Scope"/> to a room's number while that room is built and visited (0 otherwise: the player,
    /// the watch, fonts and sounds asked for outside a room). Every request tags its asset key and the bundles it
    /// needed with the scope; a key or bundle asked for under two live scopes becomes shared (0) for good. On
    /// leaving, <see cref="Release"/> forgets the room's assets and parsed prefab data (this cache is what kept
    /// them alive), runs Resources.UnloadUnusedAssets - which frees only what nothing references any more, so a
    /// shared cache holding one of them keeps a valid object - and then unloads with Unload(false) the bundles
    /// only that room opened, provided every asset we handed out from them is dead, no load is in flight and the
    /// bundle holds no audio, video, shader or font (a streamed clip or a shader is what an orphaned object could
    /// still need from its file). A later room that needs one reopens it, as on a first visit. Bundles are LZ4
    /// chunked, so an open bundle costs its header only; the memory is in the assets, freed by the first two steps.
    ///
    /// Failures are remembered per entry so a missing asset costs one warning, not one per frame.
    /// </summary>
    public sealed class RrAssets
    {
        public static RrAssets Instance { get; private set; }

        private readonly RrMain _main;
        private readonly RrCatalog _catalog;

        // Worker-owned: RrReach caches serialized files and is not thread safe; only the worker touches it.
        private readonly RrReach _reach;

        private readonly Dictionary<int, AssetBundle> _bundles = new Dictionary<int, AssetBundle>();
        private readonly Dictionary<int, AssetBundleCreateRequest> _opening = new Dictionary<int, AssetBundleCreateRequest>();
        private readonly HashSet<int> _badBundles = new HashSet<int>();
        private readonly Dictionary<string, Object> _loaded = new Dictionary<string, Object>();
        private readonly HashSet<string> _failed = new HashSet<string>();
        private readonly Dictionary<string, List<Action<Object>>> _waiting = new Dictionary<string, List<Action<Object>>>();
        private readonly Dictionary<string, RrPrefabData> _prefabData = new Dictionary<string, RrPrefabData>();
        private readonly Dictionary<long, List<int>> _reachCache = new Dictionary<long, List<int>>();
        private readonly Dictionary<string, AssetBundleRequest> _loaderRequests = new Dictionary<string, AssetBundleRequest>();
        private int _loads;
        private int _residentHits;

        // Room scopes: which room (RrTravel's number, 0 = shared) asked for each asset key and bundle.
        private readonly Dictionary<string, int> _keyScope = new Dictionary<string, int>();
        private readonly Dictionary<int, int> _bundleScope = new Dictionary<int, int>();
        private readonly HashSet<int> _releasedScopes = new HashSet<int>();
        private HashSet<int> _keepBundles;
        private int _lastScope;
        private int _bundlesUnloaded;

        public RrAssets(RrMain main)
        {
            _main = main;
            _catalog = main.Catalog;
            _reach = new RrReach(_catalog) { IncludeUiScripts = true };
            Instance = this;
        }

        public RrCatalog Catalog
        {
            get { return _catalog; }
        }

        public string Describe()
        {
            return "bundles open " + _bundles.Count + ", assets loaded " + _loaded.Count + " (" + _loads + " loads, " + _residentHits + " found resident), failed " + _failed.Count +
                   (_bundlesUnloaded > 0 ? ", bundles unloaded on leaving rooms " + _bundlesUnloaded : "");
        }

        /// <summary>Bundles open now.</summary>
        public int OpenBundles
        {
            get { return _bundles.Count; }
        }

        /// <summary>Assets cached now.</summary>
        public int LoadedAssets
        {
            get { return _loaded.Count; }
        }

        /// <summary>Loads in flight (asked for, not finished).</summary>
        public int LoadsInFlight
        {
            get { return _waiting.Count; }
        }

        // ---- lookups ----

        public bool TryFind(string name, int classId, out RrEntry entry)
        {
            return _catalog.TryFind(name, classId, out entry);
        }

        private Dictionary<string, int> _entryIndex;

        /// <summary>The catalog entry of an object, when it is a named asset or a prefab root.</summary>
        public RrEntry? Entry(int bundle, long pathId)
        {
            if (_entryIndex == null)
            {
                _entryIndex = new Dictionary<string, int>(_catalog.Entries.Count);
                for (int i = 0; i < _catalog.Entries.Count; i++)
                {
                    _entryIndex[KeyOf(_catalog.Entries[i])] = i;
                }
            }

            int index;
            if (_entryIndex.TryGetValue(bundle + ":" + pathId, out index))
            {
                return _catalog.Entries[index];
            }

            return null;
        }

        private static string KeyOf(RrEntry e)
        {
            return e.Bundle + ":" + e.PathId;
        }

        /// <summary>An already-loaded asset by name (any bundle), or null.</summary>
        public T Resident<T>(string name) where T : Object
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var all = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }

        // ---- loading ----

        public void Load<T>(string name, int classId, Action<T> done) where T : Object
        {
            RrEntry e;
            if (!_catalog.TryFind(name, classId, out e))
            {
                RrLog.WarnOnce("Assets", "nf:" + classId + ":" + name, "not in Rec Room's bundles: " + name + " (class " + classId + ")");
                if (done != null)
                {
                    done(null);
                }

                return;
            }

            Load(e, done);
        }

        public IEnumerator LoadRoutine<T>(string name, int classId, Action<T> done) where T : Object
        {
            bool finished = false;
            Load<T>(name, classId, o =>
            {
                finished = true;
                if (done != null)
                {
                    done(o);
                }
            });
            while (!finished)
            {
                yield return null;
            }
        }

        /// <summary>Loads a catalog entry; the callback runs on the main thread with the object or null.</summary>
        public void Load<T>(RrEntry e, Action<T> done) where T : Object
        {
            string key = KeyOf(e);
            int scope = Scope;
            Tag(_keyScope, key, scope);
            Object ready;
            if (_loaded.TryGetValue(key, out ready) && ready != null)
            {
                if (done != null)
                {
                    done(ready as T);
                }

                return;
            }

            if (_failed.Contains(key) || e.LoaderBundle < 0)
            {
                if (e.LoaderBundle < 0)
                {
                    RrLog.WarnOnce("Assets", "noloader:" + key, "nothing in the bundles loads " + e.Name);
                }

                if (done != null)
                {
                    done(null);
                }

                return;
            }

            List<Action<Object>> list;
            bool first = !_waiting.TryGetValue(key, out list);
            if (first)
            {
                list = new List<Action<Object>>();
                _waiting[key] = list;
            }

            if (done != null)
            {
                list.Add(o => done(o as T));
            }

            if (first)
            {
                _main.StartCoroutine(LoadEntry(e, typeof(T), scope));
            }
        }

        /// <summary>
        /// The prefab root that owns an object: Rec Room's configs often point at a component of an effect prefab
        /// (a PooledParticle on "(WaterBottleSplashFx)") rather than at its root GameObject. Climbs the owning
        /// GameObject's transforms to the root on the worker; the callback gets the root's catalog entry or null.
        /// </summary>
        public void PrefabRootOf(RrTarget target, Action<RrEntry?> done)
        {
            if (target.IsNull || target.Bundle < 0)
            {
                done(null);
                return;
            }

            var direct = Entry(target.Bundle, target.PathId);
            if (direct.HasValue && direct.Value.ClassId == 1)
            {
                done(direct);
                return;
            }

            int bundle = target.Bundle;
            long pathId = target.PathId;
            _main.Worker.Post(
                () =>
                {
                    var f = _reach.File(bundle);
                    RrObjectInfo o;
                    if (!f.TryGet(pathId, out o))
                    {
                        return 0L;
                    }

                    long go = pathId;
                    if (o.ClassId != 1)
                    {
                        // Every component starts with m_GameObject (a PPtr).
                        var r = f.At(o);
                        go = RrSerializedFile.ReadPPtr(r).PathId;
                    }

                    for (int guard = 0; guard < 64 && go != 0; guard++)
                    {
                        RrObjectInfo goInfo;
                        if (!f.TryGet(go, out goInfo))
                        {
                            return 0L;
                        }

                        var comps = new List<long>();
                        int layer;
                        string name;
                        bool active;
                        f.ReadGameObject(goInfo, comps, out layer, out name, out active);
                        RrObjectInfo tr;
                        if (comps.Count == 0 || !f.TryGet(comps[0], out tr))
                        {
                            return go;
                        }

                        long owner;
                        long parent;
                        f.ReadTransform(tr, out owner, null, null, null, null, out parent);
                        if (parent == 0)
                        {
                            return go;
                        }

                        RrObjectInfo parentTr;
                        if (!f.TryGet(parent, out parentTr))
                        {
                            return go;
                        }

                        long parentGo;
                        long grand;
                        f.ReadTransform(parentTr, out parentGo, null, null, null, null, out grand);
                        go = parentGo;
                    }

                    return go;
                },
                (result, error) =>
                {
                    long root = error == null && result is long ? (long)result : 0L;
                    done(root != 0 ? Entry(bundle, root) : null);
                });
        }

        /// <summary>A config reference (e.g. a clip or an effect prefab) resolved to a loaded object.</summary>
        public void Resolve<T>(RrTarget target, Action<T> done) where T : Object
        {
            if (target.IsNull || target.Bundle < 0)
            {
                if (done != null)
                {
                    done(null);
                }

                return;
            }

            var entry = Entry(target.Bundle, target.PathId);
            if (entry.HasValue)
            {
                Load(entry.Value, done);
                return;
            }

            // Not indexed by name (a non-root GameObject, an unnamed object): nothing to load it by.
            RrLog.WarnOnce("Assets", "unresolved:" + target.Bundle + ":" + target.PathId, "reference not in the catalog: bundle " + _catalog.Bundles[target.Bundle].File + " path " + target.PathId);
            if (done != null)
            {
                done(null);
            }
        }

        private IEnumerator LoadEntry(RrEntry e, Type type, int scope)
        {
            string key = KeyOf(e);
            _loads++;

            // 1. On the worker: the bundles Unity needs for the loader asset and for the target.
            List<int> needed = null;
            long loaderPathId = LoaderPathId(e);
            bool reached = false;
            long reachKey = RrCatalog.Key(e.Bundle, e.PathId);
            if (_reachCache.TryGetValue(reachKey, out needed))
            {
                reached = true;
            }
            else
            {
                _main.Worker.Post(
                    () =>
                    {
                        var set = new List<int>();
                        if (loaderPathId != 0)
                        {
                            Merge(set, _reach.Bundles(e.LoaderBundle, loaderPathId));
                        }

                        Merge(set, _reach.Bundles(e.Bundle, e.PathId));
                        if (!set.Contains(e.LoaderBundle))
                        {
                            set.Add(e.LoaderBundle);
                        }

                        return set;
                    },
                    (result, error) =>
                    {
                        if (error != null)
                        {
                            RrLog.Warn("Assets", "dependency walk failed for " + e.Name + ": " + error.Message);
                            needed = new List<int> { e.LoaderBundle, e.Bundle };
                        }
                        else
                        {
                            needed = (List<int>)result;
                        }

                        _reachCache[reachKey] = needed;
                        reached = true;
                    });
            }

            while (!reached)
            {
                yield return null;
            }

            // 2. Open the bundles (in parallel; Unity resolves cross-bundle references at asset load time).
            foreach (int b in needed)
            {
                Tag(_bundleScope, b, scope);
                Open(b);
            }

            while (true)
            {
                bool pending = false;
                foreach (int b in needed)
                {
                    if (_opening.ContainsKey(b))
                    {
                        pending = true;
                        break;
                    }
                }

                if (!pending)
                {
                    break;
                }

                yield return null;
            }

            AssetBundle loaderBundle;
            if (!_bundles.TryGetValue(e.LoaderBundle, out loaderBundle) || loaderBundle == null)
            {
                Finish(key, e, null, scope);
                yield break;
            }

            // 3. Load the loader asset (shared between requests for the same loader).
            string requestKey = e.LoaderBundle + "|" + e.LoaderKey;
            AssetBundleRequest request;
            if (!_loaderRequests.TryGetValue(requestKey, out request))
            {
                request = loaderBundle.LoadAssetAsync(e.LoaderKey);
                _loaderRequests[requestKey] = request;
            }

            while (!request.isDone)
            {
                yield return null;
            }

            Object result = null;
            bool isOwnContainer = e.LoaderBundle == e.Bundle && loaderPathId == e.PathId;
            if (isOwnContainer && request.asset != null && type.IsInstanceOfType(request.asset))
            {
                result = request.asset;
            }
            else
            {
                result = FindResident(type, e.Name);
                if (result != null)
                {
                    _residentHits++;
                }
            }

            Finish(key, e, result, scope);
        }

        private long LoaderPathId(RrEntry e)
        {
            var info = _catalog.Bundles[e.LoaderBundle];
            foreach (var kv in info.ContainerKeyByPathId)
            {
                if (kv.Value == e.LoaderKey)
                {
                    return kv.Key;
                }
            }

            return 0;
        }

        private static void Merge(List<int> into, List<int> from)
        {
            foreach (int b in from)
            {
                if (!into.Contains(b))
                {
                    into.Add(b);
                }
            }
        }

        private static Object FindResident(Type type, string name)
        {
            var all = Resources.FindObjectsOfTypeAll(type);
            Object fallback = null;
            for (int i = 0; i < all.Length; i++)
            {
                var o = all[i];
                if (o == null || o.name != name)
                {
                    continue;
                }

                // Prefab roots: prefer an asset (no scene) over a live instance with the same name.
                var go = o as GameObject;
                if (go != null && go.scene.IsValid())
                {
                    fallback = fallback ?? o;
                    continue;
                }

                return o;
            }

            return fallback;
        }

        private void Finish(string key, RrEntry e, Object result, int scope)
        {
            if (result == null)
            {
                _failed.Add(key);
                RrLog.WarnOnce("Assets", "fail:" + key, "could not load " + e.Name + " (class " + e.ClassId + ", bundle " + _catalog.Bundles[e.Bundle].File + ")");
            }
            else if (!_releasedScopes.Contains(scope) || _keyScope.ContainsKey(key))
            {
                // A load whose room was left before it finished is handed over but not cached, so it can be freed.
                _loaded[key] = result;
            }

            List<Action<Object>> list;
            if (_waiting.TryGetValue(key, out list))
            {
                _waiting.Remove(key);
                foreach (var cb in list)
                {
                    try
                    {
                        cb(result);
                    }
                    catch (Exception ex)
                    {
                        RrLog.Error("Assets", "load callback for " + e.Name + " failed", ex);
                    }
                }
            }
        }

        private void Open(int bundle)
        {
            if (_bundles.ContainsKey(bundle) || _opening.ContainsKey(bundle) || _badBundles.Contains(bundle))
            {
                return;
            }

            string path = _catalog.BundlePath(bundle);
            var existing = FindAlreadyLoaded(bundle);
            if (existing != null)
            {
                _bundles[bundle] = existing;
                return;
            }

            try
            {
                _opening[bundle] = AssetBundle.LoadFromFileAsync(path);
            }
            catch (Exception ex)
            {
                _badBundles.Add(bundle);
                RrLog.Warn("Assets", "cannot open " + path + ": " + ex.Message);
            }
        }

        // A previous visit that ended without OnDestroy (crash, editor domain reload off) may have left bundles open.
        private static Dictionary<string, AssetBundle> _openedByFile = new Dictionary<string, AssetBundle>();

        private AssetBundle FindAlreadyLoaded(int bundle)
        {
            AssetBundle b;
            if (_openedByFile.TryGetValue(_catalog.Bundles[bundle].File, out b) && b != null)
            {
                return b;
            }

            return null;
        }

        /// <summary>Called every frame by RrMain: collects opened bundles.</summary>
        public void Pump()
        {
            if (_opening.Count == 0)
            {
                return;
            }

            List<int> done = null;
            foreach (var kv in _opening)
            {
                if (!kv.Value.isDone)
                {
                    continue;
                }

                if (done == null)
                {
                    done = new List<int>();
                }

                done.Add(kv.Key);
            }

            if (done == null)
            {
                return;
            }

            foreach (int b in done)
            {
                var request = _opening[b];
                _opening.Remove(b);
                var bundle = request.assetBundle;
                if (bundle == null)
                {
                    _badBundles.Add(b);
                    RrLog.WarnOnce("Assets", "open:" + b, "bundle did not open: " + _catalog.Bundles[b].File);
                    continue;
                }

                _bundles[b] = bundle;
                _openedByFile[_catalog.Bundles[b].File] = bundle;
            }
        }

        // ---- prefab data ----

        /// <summary>
        /// The parsed hierarchy and configs of a prefab. Parsing reads the bundle's serialized file (tens of ms for
        /// the big bundles), so prefer <see cref="PrefabDataAsync"/>; this blocking form is for small bundles.
        /// </summary>
        public RrPrefabData PrefabData(RrEntry prefab)
        {
            RrPrefabData data;
            string key = KeyOf(prefab);
            Tag(_keyScope, key, Scope);
            if (_prefabData.TryGetValue(key, out data))
            {
                return data;
            }

            var fs = new RrUnityFs(_catalog.BundlePath(prefab.Bundle));
            int sn = fs.SerializedNodeIndex;
            var sf = new RrSerializedFile(fs.Nodes[sn].Name, fs.ReadNode(sn));
            data = new RrPrefabData(_catalog, prefab.Bundle, sf, prefab.PathId);
            _prefabData[key] = data;
            return data;
        }

        /// <summary>Parses a prefab's data on the worker; callback on the main thread (null on failure).</summary>
        public void PrefabDataAsync(RrEntry prefab, Action<RrPrefabData> done)
        {
            RrPrefabData data;
            string key = KeyOf(prefab);
            int scope = Scope;
            Tag(_keyScope, key, scope);
            if (_prefabData.TryGetValue(key, out data))
            {
                done(data);
                return;
            }

            _main.Worker.Post(
                () => new RrPrefabData(_catalog, prefab.Bundle, _reach.File(prefab.Bundle), prefab.PathId),
                (result, error) =>
                {
                    if (error != null)
                    {
                        RrLog.WarnOnce("Assets", "data:" + key, "could not read the data of " + prefab.Name + ": " + error.Message);
                        done(null);
                        return;
                    }

                    var d = (RrPrefabData)result;
                    if (!_releasedScopes.Contains(scope) || _keyScope.ContainsKey(key))
                    {
                        _prefabData[key] = d;
                    }

                    done(d);
                });
        }

        /// <summary>Loads a prefab asset and its parsed data together.</summary>
        public void LoadPrefab(string name, Action<GameObject, RrPrefabData> done)
        {
            RrEntry e;
            if (!_catalog.TryFind(name, 1, out e))
            {
                RrLog.WarnOnce("Assets", "nf:1:" + name, "no prefab named " + name + " in Rec Room's bundles");
                done(null, null);
                return;
            }

            LoadPrefab(e, done);
        }

        public void LoadPrefab(RrEntry e, Action<GameObject, RrPrefabData> done)
        {
            GameObject asset = null;
            RrPrefabData data = null;
            int remaining = 2;
            Action check = () =>
            {
                remaining--;
                if (remaining == 0)
                {
                    done(asset, data);
                }
            };
            PrefabDataAsync(e, d =>
            {
                data = d;
                check();
            });
            Load<GameObject>(e, go =>
            {
                asset = go;
                check();
            });
        }

        // ---- room scopes ----

        /// <summary>
        /// The room requests are tagged with: RrTravel's number for the room being built and visited, 0 for
        /// everything asked for outside a room (shared). Only RrTravel sets it.
        /// </summary>
        public int Scope { get; set; }

        /// <summary>A number for a new room's scope (1, 2, 3...; never reused within the visit).</summary>
        public int NewScope()
        {
            return ++_lastScope;
        }

        // A key or bundle asked for again: by a later room once its first room is gone it moves on; asked for by
        // anyone else (another live scope or shared code) it is shared for good.
        private void Tag<TKey>(Dictionary<TKey, int> scopes, TKey key, int scope)
        {
            int was;
            if (!scopes.TryGetValue(key, out was))
            {
                scopes[key] = scope;
                return;
            }

            if (was == scope || was == 0)
            {
                return;
            }

            scopes[key] = _releasedScopes.Contains(was) ? scope : 0;
        }

        // Released assets per bundle, as weak references: a bundle is unloaded only once all of them are dead.
        private readonly Dictionary<int, List<WeakReference>> _releasedObjects = new Dictionary<int, List<WeakReference>>();

        /// <summary>
        /// Frees what the room of a scope alone used (class summary, "Room scopes"): forgets its assets and prefab
        /// data, Resources.UnloadUnusedAssets, then Unload(false) of the bundles only it opened that are safe to
        /// close. Run behind the loading card after the room is destroyed (UnloadUnusedAssets is a long main-thread
        /// step). report gets one line of numbers.
        /// </summary>
        public IEnumerator Release(int scope, Action<string> report)
        {
            if (scope <= 0)
            {
                if (report != null)
                {
                    report("nothing to release (shared scope)");
                }

                yield break;
            }

            _releasedScopes.Add(scope);
            if (Scope == scope)
            {
                Scope = 0;
            }

            // 1. Forget the room's assets and parsed prefab data; keep a weak reference to each asset.
            var keys = new List<string>();
            foreach (var kv in _keyScope)
            {
                if (kv.Value == scope)
                {
                    keys.Add(kv.Key);
                }
            }

            int assets = 0;
            int datas = 0;
            foreach (string key in keys)
            {
                _keyScope.Remove(key);
                Object o;
                if (_loaded.TryGetValue(key, out o))
                {
                    _loaded.Remove(key);
                    assets++;
                    int b = BundleOfKey(key);
                    if (o != null && b >= 0)
                    {
                        List<WeakReference> list;
                        if (!_releasedObjects.TryGetValue(b, out list))
                        {
                            list = new List<WeakReference>();
                            _releasedObjects[b] = list;
                        }

                        list.Add(new WeakReference(o));
                    }
                }

                if (_prefabData.Remove(key))
                {
                    datas++;
                }
            }

            // Shared loader requests are only a de-duplication of concurrent loads; a stale one would hand out a
            // freed asset, so they all go (a later load of a live asset gets it back at once).
            _loaderRequests.Clear();
            int materials = RrMaterials.Release(scope);

            // 2. Free whatever nothing references any more (this includes the room's meshes and materials).
            float t0 = Time.realtimeSinceStartup;
            var op = Resources.UnloadUnusedAssets();
            while (op != null && !op.isDone)
            {
                yield return null;
            }

            float unloadMs = (Time.realtimeSinceStartup - t0) * 1000f;

            // 3. Loads still in flight (the old room's last spawns) keep their bundles open: give them 2 s.
            float until = Time.realtimeSinceStartup + 2f;
            while ((_waiting.Count > 0 || _opening.Count > 0) && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            bool idle = _waiting.Count == 0 && _opening.Count == 0;
            var keep = KeepOpenBundles();
            var drop = new List<int>();
            int candidates = 0;
            int special = 0;
            int inUse = 0;
            foreach (var kv in _bundleScope)
            {
                if (kv.Value == 0 || !_releasedScopes.Contains(kv.Value) || !_bundles.ContainsKey(kv.Key))
                {
                    continue;
                }

                candidates++;
                if (keep.Contains(kv.Key))
                {
                    special++;
                    continue;
                }

                if (!idle || StillAlive(kv.Key))
                {
                    inUse++;
                    continue;
                }

                drop.Add(kv.Key);
            }

            int unloaded = 0;
            foreach (int b in drop)
            {
                _bundleScope.Remove(b);
                _releasedObjects.Remove(b);
                AssetBundle bundle;
                if (!_bundles.TryGetValue(b, out bundle))
                {
                    continue;
                }

                _bundles.Remove(b);
                _openedByFile.Remove(_catalog.Bundles[b].File);
                if (bundle == null)
                {
                    continue;
                }

                try
                {
                    bundle.Unload(false);
                    unloaded++;
                }
                catch (Exception e)
                {
                    RrLog.WarnOnce("Assets", "unload:" + b, "could not unload " + _catalog.Bundles[b].File + ": " + e.Message);
                }
            }

            _bundlesUnloaded += unloaded;
            if (report != null)
            {
                report("released " + assets + " assets, " + datas + " prefab data, " + materials + " replacement materials; UnloadUnusedAssets " + unloadMs.ToString("0") + " ms; " +
                       "bundles only this room opened: " + candidates + ", unloaded " + unloaded + ", kept " + special + " (audio/video/shader/font) + " + inUse +
                       (idle ? " (an asset still in use)" : " (loads still in flight)") + "; open now " + _bundles.Count);
            }
        }

        private static int BundleOfKey(string key)
        {
            int colon = key.IndexOf(':');
            int b;
            return colon > 0 && int.TryParse(key.Substring(0, colon), out b) ? b : -1;
        }

        private bool StillAlive(int bundle)
        {
            List<WeakReference> list;
            if (!_releasedObjects.TryGetValue(bundle, out list))
            {
                return false;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var o = list[i].Target as Object;
                if (o != null)
                {
                    return true;
                }

                list.RemoveAt(i);
            }

            return false;
        }

        /// <summary>Bundles never closed while the world runs: any holding an AudioClip, VideoClip, Shader, Font or TMP font.</summary>
        private HashSet<int> KeepOpenBundles()
        {
            if (_keepBundles != null)
            {
                return _keepBundles;
            }

            _keepBundles = new HashSet<int>();
            foreach (var e in _catalog.Entries)
            {
                bool special = e.ClassId == 83 || e.ClassId == 329 || e.ClassId == 48 || e.ClassId == 128 ||
                               (e.Script != null && (e.Script.EndsWith("TMP_FontAsset", StringComparison.Ordinal) || e.Script.EndsWith("TMP_SpriteAsset", StringComparison.Ordinal)));
                if (special)
                {
                    _keepBundles.Add(e.Bundle);
                }
            }

            RrLog.Info("Assets", _keepBundles.Count + " of " + _catalog.Bundles.Count + " bundles hold audio, video, shaders or fonts and stay open once opened");
            return _keepBundles;
        }

        // ---- lifetime ----

        public void UnloadAll()
        {
            foreach (var kv in _bundles)
            {
                if (kv.Value != null)
                {
                    try
                    {
                        kv.Value.Unload(true);
                    }
                    catch (Exception)
                    {
                        // Already gone.
                    }
                }
            }

            _bundles.Clear();
            _openedByFile.Clear();
            _loaded.Clear();
            _prefabData.Clear();
            _loaderRequests.Clear();
            _keyScope.Clear();
            _bundleScope.Clear();
            _releasedScopes.Clear();
            _releasedObjects.Clear();
            Scope = 0;
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
