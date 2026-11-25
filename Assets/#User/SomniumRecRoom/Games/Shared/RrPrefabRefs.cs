using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Follows the references inside Rec Room's weapon configs to the prefabs and configs they name.
    ///
    /// A RangedWeapon does not name its bullet, muzzle flash or splat: it points at a component inside another
    /// prefab ("Bullet on [Paintball]", "PooledParticle on [VFX_PaintBall_Shoot]", "ImpactDecal on
    /// (PaintballImpactDecal)@1df8e0ef"). To spawn those we need the prefab root that owns the component, and to
    /// fly a paintball we need the Bullet component's own fields (radius 0.02, gravity, drag, 4 s life). Both come
    /// from the bundle's serialized file: the component's header names its GameObject, the GameObject's Transform
    /// names its father, and so on up to the root, whose catalog entry is what RrAssets loads. When the reference
    /// is in another bundle, the file of that bundle is reached through the data of any prefab root in it (RrAssets
    /// parses and caches files on its worker), so nothing here reads a bundle on the main thread.
    ///
    /// Also reads named MonoBehaviour assets (the game-rule configs such as "PaintballCaptureTheFlag_Config") the
    /// same way. Core has no call for that yet (requested); this is the games kit's private adapter.
    /// </summary>
    public static class RrPrefabRefs
    {
        private static readonly Dictionary<int, RrEntry> AnyPrefabOfBundle = new Dictionary<int, RrEntry>();
        private static readonly Dictionary<long, RrEntry?> RootCache = new Dictionary<long, RrEntry?>();

        /// <summary>The prefab root entry that owns a referenced object (component, child GameObject or root), or null.</summary>
        public static void RootOf(RrPrefabData from, RrTarget target, Action<RrEntry?> done)
        {
            if (done == null)
            {
                return;
            }

            if (target.IsNull || target.Bundle < 0 || RrAssets.Instance == null)
            {
                done(null);
                return;
            }

            long key = RrCatalog.Key(target.Bundle, target.PathId);
            RrEntry? cached;
            if (RootCache.TryGetValue(key, out cached))
            {
                done(cached);
                return;
            }

            WithFile(from, target.Bundle, file =>
            {
                RrEntry? result = null;
                if (file != null)
                {
                    long root;
                    string name;
                    if (Walk(file, target.PathId, out root, out name))
                    {
                        result = RrAssets.Instance != null ? RrAssets.Instance.Entry(target.Bundle, root) : null;
                    }
                }

                RootCache[key] = result;
                done(result);
            });
        }

        /// <summary>The fields of a referenced component (a Bullet, an ImpactDecal...), or null.</summary>
        public static void ConfigOf(RrPrefabData from, RrTarget target, Action<RrConfig> done)
        {
            if (done == null)
            {
                return;
            }

            if (target.IsNull || target.Bundle < 0)
            {
                done(null);
                return;
            }

            WithPrefabData(from, target.Bundle, data =>
            {
                RrObjectInfo o;
                if (data == null || !data.File.TryGet(target.PathId, out o))
                {
                    done(null);
                    return;
                }

                RrConfig cfg = null;
                try
                {
                    cfg = new RrConfig(data, data.File.Read(o));
                }
                catch (Exception e)
                {
                    RrLog.WarnOnce("Refs", "cfg" + target.PathId, "could not read a referenced config: " + e.Message);
                }

                done(cfg);
            });
        }

        /// <summary>A named MonoBehaviour asset's fields (e.g. "PaintballCaptureTheFlag_Config"), or null.</summary>
        public static void ReadAsset(string name, Action<RrConfig> done)
        {
            if (done == null)
            {
                return;
            }

            RrEntry e;
            if (RrAssets.Instance == null || !RrAssets.Instance.TryFind(name, 114, out e))
            {
                RrLog.WarnOnce("Refs", "asset:" + name, "no config asset named " + name);
                done(null);
                return;
            }

            ConfigOf(null, new RrTarget { PPtr = new RrPPtr { FileId = 0, PathId = e.PathId }, Bundle = e.Bundle, PathId = e.PathId, ClassId = 114 }, done);
        }

        /// <summary>The parsed data of the prefab a root entry names (cached by RrAssets).</summary>
        public static void DataOf(RrEntry prefab, Action<RrPrefabData> done)
        {
            if (RrAssets.Instance == null)
            {
                done(null);
                return;
            }

            RrAssets.Instance.PrefabDataAsync(prefab, done);
        }

        private static void WithFile(RrPrefabData from, int bundle, Action<RrSerializedFile> done)
        {
            WithPrefabData(from, bundle, d => done(d != null ? d.File : null));
        }

        private static void WithPrefabData(RrPrefabData from, int bundle, Action<RrPrefabData> done)
        {
            if (from != null && from.Bundle == bundle)
            {
                done(from);
                return;
            }

            RrEntry any;
            if (!TryAnyPrefab(bundle, out any))
            {
                done(null);
                return;
            }

            RrAssets.Instance.PrefabDataAsync(any, done);
        }

        private static bool TryAnyPrefab(int bundle, out RrEntry entry)
        {
            if (AnyPrefabOfBundle.TryGetValue(bundle, out entry))
            {
                return true;
            }

            var assets = RrAssets.Instance;
            if (assets == null || assets.Catalog == null)
            {
                return false;
            }

            var entries = assets.Catalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Bundle == bundle && entries[i].ClassId == 1)
                {
                    entry = entries[i];
                    AnyPrefabOfBundle[bundle] = entry;
                    return true;
                }
            }

            RrLog.WarnOnce("Refs", "noprefab" + bundle, "bundle " + bundle + " has no prefab root to read its file through");
            return false;
        }

        /// <summary>
        /// From any object of a serialized file to the root GameObject of its hierarchy: component header -> its
        /// GameObject -> that GameObject's Transform -> father Transform -> its GameObject, until a Transform
        /// without a father.
        /// </summary>
        public static bool Walk(RrSerializedFile file, long pathId, out long rootGameObject, out string rootName)
        {
            rootGameObject = 0;
            rootName = null;
            RrObjectInfo o;
            if (file == null || !file.TryGet(pathId, out o))
            {
                return false;
            }

            try
            {
                long go;
                long parent;
                if (o.ClassId == 1)
                {
                    go = pathId;
                }
                else if (o.ClassId == 114)
                {
                    bool enabled;
                    RrPPtr script;
                    string mbName;
                    file.ReadMonoBehaviourHead(o, out go, out enabled, out script, out mbName);
                }
                else if (o.ClassId == 4 || o.ClassId == 224)
                {
                    file.ReadTransform(o, out go, null, null, null, null, out parent);
                }
                else
                {
                    var fields = file.Read(o);
                    object v;
                    if (!fields.TryGetValue("m_GameObject", out v) || !(v is RrPPtr))
                    {
                        return false;
                    }

                    go = ((RrPPtr)v).PathId;
                }

                var comps = new List<long>();
                for (int guard = 0; guard < 64; guard++)
                {
                    RrObjectInfo goInfo;
                    if (!file.TryGet(go, out goInfo))
                    {
                        return false;
                    }

                    comps.Clear();
                    int layer;
                    string name;
                    bool active;
                    file.ReadGameObject(goInfo, comps, out layer, out name, out active);
                    long transform = 0;
                    foreach (long c in comps)
                    {
                        RrObjectInfo ci;
                        if (file.TryGet(c, out ci) && (ci.ClassId == 4 || ci.ClassId == 224))
                        {
                            transform = c;
                            break;
                        }
                    }

                    RrObjectInfo ti;
                    if (transform == 0 || !file.TryGet(transform, out ti))
                    {
                        return false;
                    }

                    long owner;
                    file.ReadTransform(ti, out owner, null, null, null, null, out parent);
                    if (parent == 0)
                    {
                        rootGameObject = go;
                        rootName = name;
                        return true;
                    }

                    RrObjectInfo pi;
                    if (!file.TryGet(parent, out pi))
                    {
                        return false;
                    }

                    file.ReadTransform(pi, out go, null, null, null, null, out owner);
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Refs", "walk" + pathId, "could not walk to the root of " + pathId + ": " + e.Message);
            }

            return false;
        }

        internal static void ResetStatics()
        {
            AnyPrefabOfBundle.Clear();
            RootCache.Clear();
        }
    }
}
