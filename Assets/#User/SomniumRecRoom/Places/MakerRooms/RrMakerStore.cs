using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>
    /// The maker rooms on this PC (local storage, nothing on a server): Application.persistentDataPath/SomniumRecRoom/
    /// MakerRooms/ - the folder the world already keeps its catalogue cache in.
    ///
    ///   &lt;id&gt;.json                   the latest version of a room (RrMakerRoomData through JsonUtility; readable)
    ///   history/&lt;id&gt;/v&lt;version&gt;.rrz  the last <see cref="KeepVersions"/> versions, packed (RRZ1 = deflate), newest kept
    ///   catalog.json                 every room this player knows of (theirs and others'), with who holds which version
    ///
    /// The same packed bytes are what travels to another player (<see cref="RrMakerTransfer"/>), so a received room is
    /// stored exactly as its holder had it. Writes go to a temporary file first and then replace the old one. All IO is
    /// small (a room of a few hundred objects is ~100 KB of JSON, ~15 KB packed) and runs on the main thread.
    /// </summary>
    public static class RrMakerStore
    {
        /// <summary>Saves kept per room (Rec Room kept a save history per subroom).</summary>
        public const int KeepVersions = 10;

        /// <summary>Objects a stored room may hold (anything beyond is dropped on load: a damaged or hostile file).</summary>
        public const int MaxObjects = 4000;

        private static string _root;

        /// <summary>The folder (created on first use); null when the platform gives none.</summary>
        public static string Root
        {
            get
            {
                if (_root != null)
                {
                    return _root;
                }

                try
                {
                    _root = Path.Combine(Path.Combine(Application.persistentDataPath, "SomniumRecRoom"), "MakerRooms");
                    Directory.CreateDirectory(_root);
                    Directory.CreateDirectory(Path.Combine(_root, "history"));
                }
                catch (Exception e)
                {
                    RrLog.Error("MakerRooms", "no local storage folder", e);
                    _root = null;
                }

                return _root;
            }
        }

        public static void Shutdown()
        {
            _root = null;
        }

        private static string FileOf(string id)
        {
            return Root == null || !RrMakerWire.IsId(id) ? null : Path.Combine(Root, id + ".json");
        }

        private static string HistoryOf(string id)
        {
            return Root == null || !RrMakerWire.IsId(id) ? null : Path.Combine(Path.Combine(Root, "history"), id);
        }

        // ------------------------------------------------------------------ JSON

        public static string ToJson(RrMakerRoomData data)
        {
            return JsonUtility.ToJson(data);
        }

        /// <summary>A room from its JSON, checked (id, lists, sizes), or null.</summary>
        public static RrMakerRoomData FromJson(string json, string expectId)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            RrMakerRoomData data;
            try
            {
                data = JsonUtility.FromJson<RrMakerRoomData>(json);
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "a room file did not parse: " + e.Message);
                return null;
            }

            return Check(data, expectId) ? data : null;
        }

        /// <summary>Repairs what JsonUtility leaves null and rejects a room that is not one.</summary>
        public static bool Check(RrMakerRoomData data, string expectId)
        {
            if (data == null || data.Info == null || !RrMakerWire.IsId(data.Info.Id))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(expectId) && !string.Equals(data.Info.Id, expectId, StringComparison.OrdinalIgnoreCase))
            {
                RrLog.Warn("MakerRooms", "a room file names room " + data.Info.Id + " instead of " + expectId + ": ignored");
                return false;
            }

            data.Info.Id = data.Info.Id.ToLowerInvariant();
            data.Info.Name = RrMakerRooms.CleanName(data.Info.Name);
            data.Info.Description = data.Info.Description ?? "";
            data.Info.Template = data.Info.Template ?? "";
            data.Info.OwnerName = data.Info.OwnerName ?? "";
            data.Info.CoOwners = data.Info.CoOwners ?? new List<int>();
            data.Info.MaxPlayers = Mathf.Clamp(data.Info.MaxPlayers <= 0 ? 12 : data.Info.MaxPlayers, 1, 40);
            data.Objects = data.Objects ?? new List<RrMakerObject>();
            if (data.Objects.Count > MaxObjects)
            {
                RrLog.Warn("MakerRooms", "room " + data.Info.Id + " holds " + data.Objects.Count + " objects: only the first " + MaxObjects + " are kept");
                data.Objects.RemoveRange(MaxObjects, data.Objects.Count - MaxObjects);
            }

            data.Objects.RemoveAll(o => o == null || o.Id == 0);
            foreach (var o in data.Objects)
            {
                o.Asset = o.Asset ?? "";
                if (o.Points != null && o.Points.Count == 0)
                {
                    o.Points = null;
                }

                if (string.IsNullOrEmpty(o.Text))
                {
                    o.Text = null;
                }

                if (o.Rotation.x == 0f && o.Rotation.y == 0f && o.Rotation.z == 0f && o.Rotation.w == 0f)
                {
                    o.Rotation = Quaternion.identity;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ rooms

        /// <summary>The ids of the rooms stored here.</summary>
        public static List<string> Ids()
        {
            var list = new List<string>();
            if (Root == null)
            {
                return list;
            }

            try
            {
                foreach (var f in Directory.GetFiles(Root, "*.json"))
                {
                    string id = Path.GetFileNameWithoutExtension(f);
                    if (RrMakerWire.IsId(id))
                    {
                        list.Add(id.ToLowerInvariant());
                    }
                }
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "listing the stored rooms failed: " + e.Message);
            }

            return list;
        }

        public static bool Has(string id)
        {
            string f = FileOf(id);
            return f != null && File.Exists(f);
        }

        /// <summary>The latest stored version of a room, or null.</summary>
        public static RrMakerRoomData Load(string id)
        {
            string f = FileOf(id);
            if (f == null || !File.Exists(f))
            {
                return null;
            }

            try
            {
                return FromJson(File.ReadAllText(f, Encoding.UTF8), id);
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "reading room " + id + " failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Stores a version of a room: the latest file (when it is not older than what is stored) and its packed copy in
        /// the history (the oldest beyond <see cref="KeepVersions"/> go). Returns the packed bytes (what is shared), or null.
        /// </summary>
        public static byte[] Write(RrMakerRoomData data)
        {
            if (data == null || data.Info == null || !RrMakerWire.IsId(data.Info.Id) || Root == null)
            {
                return null;
            }

            string id = data.Info.Id;
            try
            {
                string json = ToJson(data);
                var raw = RrMakerWire.Utf8(json);
                var packed = RrMakerWire.Pack(raw);

                // What is stored now: the catalogue knows it (kept in step with every write and with the files at start);
                // only a room it does not know is parsed from its file (a whole parse per auto-save was a hitch in big rooms).
                int haveVersion = 0;
                long haveSavedAt = 0;
                bool have = false;
                var known = RrMakerCatalog.Get(id);
                if (known != null && known.HeldVersion > 0 && File.Exists(FileOf(id)))
                {
                    have = true;
                    haveVersion = known.HeldVersion;
                    haveSavedAt = known.HeldSavedAt;
                }
                else
                {
                    var existing = PeekInfo(id);
                    if (existing != null)
                    {
                        have = true;
                        haveVersion = existing.Version;
                        haveSavedAt = existing.SavedAt;
                    }
                }

                bool newer = !have || data.Info.Version > haveVersion || (data.Info.Version == haveVersion && data.Info.SavedAt >= haveSavedAt);
                if (newer)
                {
                    Replace(FileOf(id), raw);
                }

                string dir = HistoryOf(id);
                Directory.CreateDirectory(dir);
                Replace(Path.Combine(dir, "v" + data.Info.Version + ".rrz"), packed);
                Prune(dir);
                return packed;
            }
            catch (Exception e)
            {
                RrLog.Error("MakerRooms", "storing room " + id + " v" + data.Info.Version + " failed", e);
                return null;
            }
        }

        /// <summary>
        /// The packed bytes of the stored latest version (the history copy when there is one, else packed now), and its
        /// version; null when the room is not stored.
        /// </summary>
        public static byte[] Packed(string id, out int version)
        {
            version = 0;
            var info = PeekInfo(id);
            if (info == null)
            {
                return null;
            }

            version = info.Version;
            try
            {
                string dir = HistoryOf(id);
                string h = dir != null ? Path.Combine(dir, "v" + info.Version + ".rrz") : null;
                if (h != null && File.Exists(h))
                {
                    var bytes = File.ReadAllBytes(h);
                    var check = RrMakerWire.Unpack(bytes);
                    var parsed = check != null ? FromJson(RrMakerWire.FromUtf8(check), id) : null;
                    if (parsed != null && parsed.Info.Version == info.Version && parsed.Info.SavedAt == info.SavedAt)
                    {
                        return bytes;
                    }
                }

                return RrMakerWire.Pack(File.ReadAllBytes(FileOf(id)));
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "packing room " + id + " failed: " + e.Message);
                return null;
            }
        }

        /// <summary>A room from packed bytes (a transfer), checked, or null.</summary>
        public static RrMakerRoomData Unpacked(byte[] packed, string expectId)
        {
            var raw = RrMakerWire.Unpack(packed);
            return raw == null ? null : FromJson(RrMakerWire.FromUtf8(raw), expectId);
        }

        /// <summary>The settings of the stored latest version (the whole file is parsed; cache it).</summary>
        public static RrMakerRoomInfo PeekInfo(string id)
        {
            var data = Load(id);
            return data != null ? data.Info : null;
        }

        /// <summary>The versions in a room's history, newest first.</summary>
        public static List<int> Versions(string id)
        {
            var list = new List<int>();
            string dir = HistoryOf(id);
            if (dir == null || !Directory.Exists(dir))
            {
                return list;
            }

            try
            {
                foreach (var f in Directory.GetFiles(dir, "v*.rrz"))
                {
                    int v;
                    if (int.TryParse(Path.GetFileNameWithoutExtension(f).Substring(1), out v))
                    {
                        list.Add(v);
                    }
                }
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "listing the history of " + id + " failed: " + e.Message);
            }

            list.Sort((a, b) => b.CompareTo(a));
            return list;
        }

        /// <summary>One version from the history, or null.</summary>
        public static RrMakerRoomData LoadVersion(string id, int version)
        {
            string dir = HistoryOf(id);
            if (dir == null)
            {
                return null;
            }

            string f = Path.Combine(dir, "v" + version + ".rrz");
            try
            {
                return File.Exists(f) ? Unpacked(File.ReadAllBytes(f), id) : null;
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "reading " + id + " v" + version + " failed: " + e.Message);
                return null;
            }
        }

        /// <summary>Removes a room and its history from this PC.</summary>
        public static void Delete(string id)
        {
            string f = FileOf(id);
            string dir = HistoryOf(id);
            try
            {
                if (f != null && File.Exists(f))
                {
                    File.Delete(f);
                }

                if (dir != null && Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }

                RrLog.Info("MakerRooms", "removed room " + id + " from this PC");
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "removing room " + id + " failed: " + e.Message);
            }
        }

        private static void Prune(string dir)
        {
            var files = new List<KeyValuePair<int, string>>();
            foreach (var f in Directory.GetFiles(dir, "v*.rrz"))
            {
                int v;
                if (int.TryParse(Path.GetFileNameWithoutExtension(f).Substring(1), out v))
                {
                    files.Add(new KeyValuePair<int, string>(v, f));
                }
            }

            if (files.Count <= KeepVersions)
            {
                return;
            }

            files.Sort((a, b) => b.Key.CompareTo(a.Key));
            for (int i = KeepVersions; i < files.Count; i++)
            {
                try
                {
                    File.Delete(files[i].Value);
                }
                catch (Exception e)
                {
                    RrLog.Warn("MakerRooms", "pruning " + files[i].Value + " failed: " + e.Message);
                }
            }
        }

        // ------------------------------------------------------------------ the catalogue file

        private static string CatalogFile
        {
            get { return Root == null ? null : Path.Combine(Root, "catalog.json"); }
        }

        public static RrMakerCatalogFile LoadCatalog()
        {
            string f = CatalogFile;
            if (f == null || !File.Exists(f))
            {
                return new RrMakerCatalogFile();
            }

            try
            {
                var c = JsonUtility.FromJson<RrMakerCatalogFile>(File.ReadAllText(f, Encoding.UTF8));
                return c ?? new RrMakerCatalogFile();
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "the room catalogue did not parse (" + e.Message + "): starting a new one");
                return new RrMakerCatalogFile();
            }
        }

        public static void SaveCatalog(RrMakerCatalogFile catalog)
        {
            string f = CatalogFile;
            if (f == null || catalog == null)
            {
                return;
            }

            try
            {
                Replace(f, RrMakerWire.Utf8(JsonUtility.ToJson(catalog)));
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "writing the room catalogue failed: " + e.Message);
            }
        }

        private static void Replace(string path, byte[] bytes)
        {
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tmp, path);
        }
    }
}
