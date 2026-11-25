using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>A room picked on the watch, waiting for its <see cref="RrMakerRoom"/> to be built.</summary>
    public sealed class RrMakerPending
    {
        public string Id;
        public RrMakerRoomData Data;
        public bool Fresh;
        public float At;
    }

    /// <summary>
    /// Player-made rooms: the front door (tasks 2-6). There is no server, so every room lives in its players' local storage
    /// (<see cref="RrMakerStore"/>) and travels between them (<see cref="RrMakerCatalog"/>, <see cref="RrMakerTransfer"/>).
    ///
    /// <b>Create</b> (<see cref="Create"/>, the watch's Create page): a new room "&lt;Name&gt;'s &lt;Template&gt;" owned by this
    /// player (their Somnium user hash, presence int 6, and nickname), version 1 stored at once, announced, and travelled to.
    /// <b>Open</b> (<see cref="Open"/>, the Rooms page): private rooms only for their owner and co-owners; when someone is in
    /// it the player joins that instance (the live state comes from its authority); else the newest known version is
    /// fetched from an online holder if this PC has an older one or none; then the instance number: the fullest instance
    /// already playing that room, else a bound instance that names it, else a free number found from the room's hash.
    /// <b>Save</b> (<see cref="SaveRoom"/>): owners and co-owners; a new version, written here (last 10 kept), announced to
    /// the instance (others fetch it and become holders) and to the world.
    ///
    /// Started by the registry hook (Places/MakerRooms/RrRegistryMakerRooms.cs), stopped by RrMain (<see cref="Shutdown"/>).
    /// </summary>
    public static class RrMakerRooms
    {
        public const string DefaultTitle = "Maker Rooms";

        /// <summary>Every player re-announces the rooms they hold this often (s).</summary>
        public const float CatalogEvery = 60f;

        public const int MaxNameLength = 40;

        private sealed class Fetch
        {
            public string Id;
            public int Version;
            public int From;
            public float At;
        }

        private static RrMakerDriver _driver;
        private static RrMakerPending _pending;
        private static bool _initialised;
        private static int _myHash;
        private static bool _hashFromClient;
        private static float _broadcastAt = -1f;
        private static float _nextRegular;
        private static bool _wasOnline;
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static readonly List<Fetch> Fetches = new List<Fetch>();
        private static readonly HashSet<string> Fetching = new HashSet<string>();
        private static RrMakerTransfer.Incoming _download;
        private static string _downloadName = "";
        private static bool _opening;
        private static int _serial;
        private static float _presentAt;

        /// <summary>A line for the watch about the last thing that happened (created, saved, downloading, refused).</summary>
        public static string Status { get; private set; } = "";

        /// <summary>Realtime of <see cref="Status"/>.</summary>
        public static float StatusAt { get; private set; }

        /// <summary>Bumped on every change the watch should redraw for.</summary>
        public static int Serial
        {
            get { return _serial + RrMakerCatalog.Serial; }
        }

        /// <summary>A room is being opened (downloaded, then travelled to).</summary>
        public static bool Opening
        {
            get { return _opening; }
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>From the registry hook at start: the catalogue, the events, the driver.</summary>
        public static void Init()
        {
            if (_initialised)
            {
                return;
            }

            _initialised = true;
            RrMakerCatalog.Load();
            RrNet.On(RrMakerEvents.Catalog, OnCatalog);
            RrNet.On(RrMakerEvents.Request, RrMakerTransfer.OnRequest);
            RrNet.On(RrMakerEvents.Chunk, RrMakerTransfer.OnChunk);
            RrMakerTransfer.FileSource = ServeFile;
            var go = new GameObject("Rr Maker Rooms");
            var main = RrMain.Instance;
            if (main != null)
            {
                go.transform.SetParent(main.WorldRoot != null ? main.WorldRoot : main.transform, false);
            }

            _driver = go.AddComponent<RrMakerDriver>();
            _nextRegular = Time.realtimeSinceStartup + CatalogEvery;
            RrLog.Info("MakerRooms", "started: " + RrMakerTemplates.All.Count + " templates, events " + RrMakerEvents.Catalog + "-" + RrMakerEvents.Meta + ", rooms in " + (RrMakerStore.Root ?? "(no folder)"));
        }

        /// <summary>RrMain.OnDestroy: everything back to nothing (the client keeps the assembly loaded between visits).</summary>
        public static void Shutdown()
        {
            // Leaving the world from inside a room skips its OnLocalExit: its unsaved changes are kept here.
            try
            {
                var room = RrMakerRoom.Current;
                if (room != null && room.Built && room.Dirty && room.CanSave)
                {
                    RrLog.Info("MakerRooms", "the world closes with unsaved changes in \"" + room.RoomInfo.Name + "\": saving");
                    SaveRoom(room, true);
                }
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "saving at shutdown failed: " + e.Message);
            }

            try
            {
                RrMakerCatalog.Flush();
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "writing the catalogue at shutdown failed: " + e.Message);
            }

            RrNet.Off(RrMakerEvents.Catalog, OnCatalog);
            RrNet.Off(RrMakerEvents.Request, RrMakerTransfer.OnRequest);
            RrNet.Off(RrMakerEvents.Chunk, RrMakerTransfer.OnChunk);
            if (_driver != null)
            {
                var go = _driver.gameObject;
                _driver = null;
                UnityEngine.Object.Destroy(go);
            }

            RrMakerTransfer.Shutdown();
            RrMakerCatalog.Shutdown();
            RrMakerStore.Shutdown();
            RrMakerTemplates.Shutdown();
            RrMakerEdits.Reset();
            _pending = null;
            _initialised = false;
            _myHash = 0;
            _hashFromClient = false;
            _broadcastAt = -1f;
            _nextRegular = 0f;
            _wasOnline = false;
            Seen.Clear();
            Fetches.Clear();
            Fetching.Clear();
            _download = null;
            _downloadName = "";
            _opening = false;
            _serial = 0;
            Status = "";
            StatusAt = 0f;
        }

        internal static void DriverGone(RrMakerDriver driver)
        {
            if (_driver == driver)
            {
                _driver = null;
            }
        }

        /// <summary>Something the watch shows changed.</summary>
        public static void Touch()
        {
            _serial++;
        }

        private static void Say(string text, bool warn = false)
        {
            Status = text ?? "";
            StatusAt = Time.realtimeSinceStartup;
            _serial++;
            if (warn)
            {
                RrLog.Warn("MakerRooms", text);
            }
            else
            {
                RrLog.Info("MakerRooms", text);
            }
        }

        // ------------------------------------------------------------------ identity

        /// <summary>
        /// This player's Somnium user hash (FNV-1a of the Bridge's Properties.Id, exactly as presence int 6 carries it); in
        /// the editor without a client, a hash of the nickname (logged once).
        /// </summary>
        public static int MyHash
        {
            get
            {
                if (_hashFromClient && _myHash != 0)
                {
                    return _myHash;
                }

                int h = 0;
                try
                {
                    var rec = RrNet.PresenceRecord(RrNet.LocalId);
                    if (rec != null && rec.Length > RrPresenceWire.AtUser)
                    {
                        h = rec[RrPresenceWire.AtUser];
                    }
                }
                catch (Exception)
                {
                    // the network is not up
                }

                if (h == 0)
                {
                    try
                    {
                        var p = RrPlayer.Local;
                        // An id the client has not filled in yet would hash the same for everyone (and stick): not yet.
                        string uid = p != null && p.Properties != null ? Convert.ToString(p.Properties.Id) : null;
                        if (!string.IsNullOrEmpty(uid))
                        {
                            h = RrWire.Fnv1a(uid);
                        }
                    }
                    catch (Exception)
                    {
                        // no client (editor)
                    }
                }

                if (h != 0)
                {
                    _myHash = h;
                    _hashFromClient = true;
                    return h;
                }

                if (_myHash == 0)
                {
                    _myHash = RrWire.Fnv1a("offline:" + RrPlayer.Nick);
                    if (_myHash == 0)
                    {
                        _myHash = 1;
                    }

                    RrLog.Info("MakerRooms", "no Somnium user id (editor?): rooms are owned by a hash of the nickname (" + _myHash.ToString("x8") + ")");
                }

                return _myHash;
            }
        }

        public static string MyName
        {
            get { return RrPlayer.Nick; }
        }

        public static bool IsOwnerOrCo(RrMakerRoomInfo info)
        {
            return RrMakerCatalog.IsOwnerOrCo(info, MyHash);
        }

        public static bool IsMine(RrMakerRoomInfo info)
        {
            return info != null && info.OwnerHash != 0 && info.OwnerHash == MyHash;
        }

        /// <summary>A room name as Rec Room allowed them: printable, single spaces, at most 40 characters; never empty.</summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Maker Room";
            }

            var sb = new StringBuilder(name.Length);
            bool space = false;
            foreach (char c in name)
            {
                if (char.IsControl(c) || c == '<' || c == '>')
                {
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    space = sb.Length > 0;
                    continue;
                }

                if (space)
                {
                    sb.Append(' ');
                    space = false;
                }

                sb.Append(c);
                if (sb.Length >= MaxNameLength)
                {
                    break;
                }
            }

            string s = sb.ToString().Trim();
            return s.Length == 0 ? "Maker Room" : s;
        }

        // ------------------------------------------------------------------ queries for the watch

        /// <summary>Rooms this player owns or co-owns (not deleted), newest first.</summary>
        public static List<RrMakerKnownRoom> MyRooms()
        {
            var list = new List<RrMakerKnownRoom>();
            int me = MyHash;
            foreach (var r in RrMakerCatalog.All)
            {
                if (!r.Deleted && RrMakerCatalog.IsOwnerOrCo(r.Info, me))
                {
                    list.Add(r);
                }
            }

            list.Sort((a, b) => b.Info.SavedAt.CompareTo(a.Info.SavedAt));
            return list;
        }

        /// <summary>Other players' public rooms this player knows of: the ones with people in them first, then the newest.</summary>
        public static List<RrMakerKnownRoom> PublicRooms()
        {
            var list = new List<RrMakerKnownRoom>();
            int me = MyHash;
            var counts = new Dictionary<string, int>();
            foreach (var r in RrMakerCatalog.All)
            {
                if (r.Deleted || !r.Info.Public || RrMakerCatalog.IsOwnerOrCo(r.Info, me))
                {
                    continue;
                }

                list.Add(r);
                counts[r.Info.Id] = PlayersIn(r.Info.Id).Count;
            }

            list.Sort((a, b) =>
            {
                int c = counts[b.Info.Id].CompareTo(counts[a.Info.Id]);
                return c != 0 ? c : b.Info.SavedAt.CompareTo(a.Info.SavedAt);
            });
            return list;
        }

        /// <summary>The newest room this player owns that is stored here, or null.</summary>
        public static RrMakerKnownRoom MyLatestRoom()
        {
            RrMakerKnownRoom best = null;
            foreach (var r in RrMakerCatalog.All)
            {
                if (!r.Deleted && r.HeldVersion > 0 && IsMine(r.Info) && (best == null || r.HeldSavedAt > best.HeldSavedAt))
                {
                    best = r;
                }
            }

            return best;
        }

        /// <summary>PlayerIds in a room now (presence: activity MakerRoom + int 19 = the room's hash), the local player included.</summary>
        public static List<int> PlayersIn(string id)
        {
            var list = new List<int>();
            if (string.IsNullOrEmpty(id))
            {
                return list;
            }

            var current = RrMakerRoom.Current;
            if (current != null && current.RoomId == id && RrTravel.Current == RrActivity.MakerRoom)
            {
                list.Add(RrNet.LocalId);
            }

            if (!RrNet.Online)
            {
                return list;
            }

            int hash = RrMakerWire.RoomHash(id);
            foreach (int p in RrNet.Present)
            {
                if (p == RrNet.LocalId)
                {
                    continue;
                }

                var v = RrNet.Presence(p);
                if (v.Valid && v.Activity == RrActivity.MakerRoom && RrNet.PlayerInt(p, 0) == hash)
                {
                    list.Add(p);
                }
            }

            return list;
        }

        /// <summary>Instance number -> players in it, for one room (others only).</summary>
        private static Dictionary<int, int> LiveInstances(string id)
        {
            var map = new Dictionary<int, int>();
            if (!RrNet.Online)
            {
                return map;
            }

            int hash = RrMakerWire.RoomHash(id);
            foreach (int p in RrNet.Present)
            {
                if (p == RrNet.LocalId)
                {
                    continue;
                }

                var v = RrNet.Presence(p);
                if (v.Valid && v.Activity == RrActivity.MakerRoom && v.Status != RrStatus.None && RrNet.PlayerInt(p, 0) == hash)
                {
                    int n;
                    map.TryGetValue(v.Instance, out n);
                    map[v.Instance] = n + 1;
                }
            }

            return map;
        }

        /// <summary>Why a room cannot be opened now (null: it can).</summary>
        public static string CannotOpen(RrMakerKnownRoom r)
        {
            if (r == null || r.Deleted)
            {
                return "Deleted";
            }

            if (!r.Info.Public && !IsOwnerOrCo(r.Info))
            {
                return "Private";
            }

            if (r.HeldVersion > 0 || LiveInstances(r.Info.Id).Count > 0)
            {
                return null;
            }

            return RrMakerCatalog.OnlineHolders(r, 1).Count > 0 ? null : "Nobody online has it";
        }

        /// <summary>A line about the download in progress, or null.</summary>
        public static string DownloadLine()
        {
            string d = RrMakerTransfer.Describe(_download);
            return d == null ? null : "Getting \"" + _downloadName + "\": " + d;
        }

        // ------------------------------------------------------------------ create, open, delete

        /// <summary>Makes a new room on a template, stores it as version 1 and goes there.</summary>
        public static bool Create(string templateKey)
        {
            if (RrTravel.Busy || _opening)
            {
                Say("Wait until the current trip is over");
                return false;
            }

            var t = RrMakerTemplates.Get(templateKey);
            if (t == null || !t.Available)
            {
                Say((t != null ? t.Title : templateKey) + " is not available yet");
                return false;
            }

            var data = NewRoom(t.Key);
            if (data == null)
            {
                Say("Could not create the room", true);
                return false;
            }

            Say("Created \"" + data.Info.Name + "\"");
            Go(data.Info.Id, data, true, -1, data.Info.MaxPlayers);
            return true;
        }

        /// <summary>A new room on a template, owned by this player, stored as version 1 (null when the template is unknown).</summary>
        public static RrMakerRoomData NewRoom(string templateKey)
        {
            var t = RrMakerTemplates.Get(templateKey);
            if (t == null || !t.Available)
            {
                return null;
            }

            var data = new RrMakerRoomData
            {
                Info = new RrMakerRoomInfo
                {
                    Id = RrMakerWire.NewId(),
                    Name = UniqueName(CleanName(MyName + "'s " + t.Title)),
                    Description = "",
                    Template = t.Key,
                    OwnerHash = MyHash,
                    OwnerName = MyName,
                    Public = true,
                    EveryoneCanBuild = false,
                    MaxPlayers = t.MaxPlayers,
                    Version = 1,
                    SavedAt = RrMakerWire.Now()
                }
            };

            try
            {
                if (t.Objects != null)
                {
                    data.Objects = t.Objects() ?? new List<RrMakerObject>();
                }
            }
            catch (Exception e)
            {
                RrLog.Error("MakerRooms", t.Key + "'s base pieces failed", e);
            }

            if (RrMakerStore.Write(data) != null)
            {
                RrMakerCatalog.NoteHeld(data.Info);
            }
            else
            {
                RrLog.Warn("MakerRooms", "the new room could not be stored on this PC: it lives until it is left unless it is saved later");
            }

            BroadcastSoon(2f);
            RrLog.Info("MakerRooms", "new room \"" + data.Info.Name + "\" (" + data.Info.Id + ") on " + t.Title + ": " + data.Objects.Count + " base piece(s)");
            return data;
        }

        private static string UniqueName(string name)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in MyRooms())
            {
                taken.Add(r.Info.Name);
            }

            if (!taken.Contains(name))
            {
                return name;
            }

            for (int i = 2; i < 1000; i++)
            {
                string candidate = CleanName(name.Length > MaxNameLength - 4 ? name.Substring(0, MaxNameLength - 4) : name) + " " + i;
                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }

            return name;
        }

        /// <summary>Opens a room (downloading it first when needed) and goes there; avoid = an instance number not to use.</summary>
        public static bool Open(string id, int avoid = -1)
        {
            if (_driver == null || !RrMakerWire.IsId(id))
            {
                return false;
            }

            if (_opening || RrTravel.Busy)
            {
                Say("Wait until the current trip is over");
                return false;
            }

            var current = RrMakerRoom.Current;
            if (avoid < 0 && current != null && current.RoomId == id && RrTravel.Current == RrActivity.MakerRoom)
            {
                Say("You are in it");
                return false;
            }

            _driver.StartCoroutine(OpenRoutine(id, avoid));
            return true;
        }

        private static IEnumerator OpenRoutine(string id, int avoid)
        {
            _opening = true;
            try
            {
                var known = RrMakerCatalog.Get(id);
                var info = known != null ? known.Info : RrMakerStore.PeekInfo(id);
                if (info == null)
                {
                    Say("That room is not known here", true);
                    yield break;
                }

                if (known != null && known.Deleted)
                {
                    Say("\"" + info.Name + "\" was deleted by its owner");
                    yield break;
                }

                if (!info.Public && !IsOwnerOrCo(info))
                {
                    Say("\"" + info.Name + "\" is private: only its owner and co-owners can go in");
                    yield break;
                }

                // Someone is in it: join them; the live state comes from their instance.
                var live = LiveInstances(id);
                if (live.Count > 0)
                {
                    Say("Joining \"" + info.Name + "\"");
                    Go(id, RrMakerStore.Load(id), false, avoid, info.MaxPlayers);
                    yield break;
                }

                // Nobody in it: the newest version known, from whoever holds it.
                int held = known != null ? known.HeldVersion : RrMakerStore.Has(id) ? info.Version : 0;
                int latest = known != null ? RrMakerCatalog.LatestVersion(known) : held;
                if (held < latest || held == 0)
                {
                    var holders = RrMakerCatalog.OnlineHolders(known, held + 1);
                    if (holders.Count > 0)
                    {
                        byte[] got = null;
                        string error = null;
                        _downloadName = info.Name;
                        _download = RrMakerTransfer.RequestFile(id, held + 1, holders, b => got = b, e => error = e);
                        Say("Getting \"" + info.Name + "\" v" + latest + " from another player...");
                        while (got == null && error == null)
                        {
                            yield return null;
                        }

                        _download = null;
                        if (got != null)
                        {
                            var data = RrMakerStore.Unpacked(got, id);
                            if (data != null && RrMakerStore.Write(data) != null)
                            {
                                RrMakerCatalog.NoteHeld(data.Info);
                                held = data.Info.Version;
                                BroadcastSoon(2f);
                            }
                            else
                            {
                                error = "the copy did not check out";
                            }
                        }

                        if (error != null)
                        {
                            if (held == 0)
                            {
                                Say("Could not get \"" + info.Name + "\": " + error, true);
                                yield break;
                            }

                            Say("Could not get the newest \"" + info.Name + "\" (" + error + "): opening v" + held, true);
                        }
                    }
                    else if (held == 0)
                    {
                        Say("Nobody who has \"" + info.Name + "\" is online right now");
                        yield break;
                    }
                }

                var stored = RrMakerStore.Load(id);
                if (stored == null)
                {
                    Say("Could not read \"" + info.Name + "\" from this PC", true);
                    yield break;
                }

                Say("Going to \"" + stored.Info.Name + "\"");
                Go(id, stored, false, avoid, stored.Info.MaxPlayers);
            }
            finally
            {
                _opening = false;
                _download = null;
                _serial++;
            }
        }

        private static void Go(string id, RrMakerRoomData data, bool fresh, int avoid, int cap)
        {
            int number = ChooseInstanceNumber(id, Mathf.Max(1, cap), avoid);
            _pending = new RrMakerPending { Id = id, Data = data, Fresh = fresh, At = Time.realtimeSinceStartup };
            SetTravelTitle(data != null ? data.Info.Name : (RrMakerCatalog.Get(id) != null ? RrMakerCatalog.Get(id).Info.Name : DefaultTitle));
            RrLog.Info("MakerRooms", "opening room " + id + " in instance #" + number + (fresh ? " (new)" : "") + (data == null ? " (from the people in it)" : " (v" + data.Info.Version + ")"));
            RrTravel.GoTo(RrActivity.MakerRoom, number);
        }

        /// <summary>The picked room, for the RrMakerRoom being built (once; null when none or stale).</summary>
        public static RrMakerPending TakePending()
        {
            var p = _pending;
            _pending = null;
            if (p != null && Time.realtimeSinceStartup - p.At > 180f)
            {
                RrLog.Warn("MakerRooms", "a picked room waited too long for its trip: ignored");
                return null;
            }

            return p;
        }

        /// <summary>
        /// The instance number for a room: the fullest instance already playing it (below its cap), else a bound instance
        /// object naming it, else a free number found from the room's hash (so two rooms rarely pick the same one).
        /// </summary>
        public static int ChooseInstanceNumber(string id, int cap, int avoid)
        {
            var live = LiveInstances(id);
            int best = -1;
            foreach (var kv in live)
            {
                if (kv.Key != avoid && kv.Key < RrPresenceWire.SoloNumber && kv.Value < cap && (best < 0 || kv.Value > live[best]))
                {
                    best = kv.Key;
                }
            }

            if (best >= 0)
            {
                return best;
            }

            var used = new HashSet<int>();
            int named = -1;
            if (RrNet.Online)
            {
                var core = RrNet.Core;
                for (int k = 0; k < RrInstanceWire.Objects; k++)
                {
                    var r = core.Inst[k];
                    if (!core.InstValid[k] || !RrInstanceWire.Written(r))
                    {
                        continue;
                    }

                    int key = RrInstanceWire.KeyOf(r);
                    if (RrInstanceWire.KeyActivity(key) != (int)RrActivity.MakerRoom)
                    {
                        continue;
                    }

                    int n = RrInstanceWire.KeyNumber(key);
                    used.Add(n);
                    int at = RrInstanceWire.AtCustom + RrMakerRoom.IntId0;
                    if (n != avoid && RrMakerWire.IdFromInts(r[at], r[at + 1], r[at + 2], r[at + 3]) == id)
                    {
                        named = n;
                    }
                }

                foreach (int p in RrNet.Present)
                {
                    var v = RrNet.Presence(p);
                    if (v.Valid && v.Activity == RrActivity.MakerRoom)
                    {
                        used.Add(v.Instance);
                    }
                }
            }

            if (named >= 0)
            {
                return named;
            }

            if (avoid >= 0)
            {
                used.Add(avoid);
            }

            if (RrTravel.Current == RrActivity.MakerRoom)
            {
                used.Add(RrTravel.CurrentInstance);
            }

            int start = (int)((uint)RrMakerWire.RoomHash(id) % (uint)RrPresenceWire.SoloNumber);
            for (int i = 0; i < RrPresenceWire.SoloNumber; i++)
            {
                int n = (start + i) % RrPresenceWire.SoloNumber;
                if (!used.Contains(n))
                {
                    return n;
                }
            }

            return start;
        }

        /// <summary>
        /// Deletes a room from this PC. The owner's deletion is announced (everyone else drops their copy and hides it);
        /// a co-owner or visitor only removes their own copy.
        /// </summary>
        public static bool Delete(string id)
        {
            var r = RrMakerCatalog.Get(id);
            if (r == null)
            {
                return false;
            }

            var current = RrMakerRoom.Current;
            if (current != null && current.RoomId == id)
            {
                Say("Leave \"" + r.Info.Name + "\" before deleting it");
                return false;
            }

            RrMakerStore.Delete(id);
            if (IsMine(r.Info))
            {
                RrMakerCatalog.MarkDeleted(id);
                BroadcastSoon(1f);
                Say("Deleted \"" + r.Info.Name + "\"");
            }
            else
            {
                RrMakerCatalog.ForgetCopy(id);
                Say("Removed your copy of \"" + r.Info.Name + "\"");
            }

            return true;
        }

        // ------------------------------------------------------------------ saving

        /// <summary>
        /// Saves the room as a new version (owners and co-owners): stored here (the last 10 kept), the room's members and
        /// the world hear of it. auto: the 2-minute auto-save or the save on leave.
        /// </summary>
        public static bool SaveRoom(RrMakerRoom room, bool auto)
        {
            if (room == null || room.RoomInfo == null)
            {
                return false;
            }

            if (!IsOwnerOrCo(room.RoomInfo))
            {
                if (!auto)
                {
                    Say("Only the owner and co-owners can save \"" + room.RoomInfo.Name + "\"");
                }

                return false;
            }

            var data = room.Snapshot();
            var known = RrMakerCatalog.Get(data.Info.Id);
            int version = Math.Max(data.Info.Version, RrMakerCatalog.LatestVersion(known)) + 1;
            long savedAt = Math.Max(RrMakerWire.Now(), data.Info.SavedAt + 1);
            data.Info.Version = version;
            data.Info.SavedAt = savedAt;
            if (RrMakerStore.Write(data) == null)
            {
                Say("Could not save \"" + data.Info.Name + "\" on this PC", true);
                return false;
            }

            RrMakerCatalog.NoteHeld(data.Info, false);
            room.Saved(version, savedAt);
            BroadcastSoon(1f);
            Say((auto ? "Auto-saved" : "Saved") + " \"" + data.Info.Name + "\" v" + version + " (" + data.Objects.Count + " objects)");
            return true;
        }

        /// <summary>Someone saved the room this player is in: fetch that version from them after a moment (become a holder).</summary>
        public static void FetchLater(string id, int version, int from)
        {
            var r = RrMakerCatalog.Get(id);
            if (r != null && r.HeldVersion >= version)
            {
                return;
            }

            Fetches.RemoveAll(f => f.Id == id);
            Fetches.Add(new Fetch { Id = id, Version = version, From = from, At = Time.realtimeSinceStartup + UnityEngine.Random.Range(1f, 5f) });
        }

        private static void RunFetches()
        {
            if (Fetches.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            for (int i = Fetches.Count - 1; i >= 0; i--)
            {
                var f = Fetches[i];
                if (now < f.At)
                {
                    continue;
                }

                Fetches.RemoveAt(i);
                var r = RrMakerCatalog.Get(f.Id);
                if ((r != null && r.HeldVersion >= f.Version) || Fetching.Contains(f.Id))
                {
                    continue;
                }

                var holders = new List<int> { f.From };
                foreach (int h in RrMakerCatalog.OnlineHolders(r, f.Version))
                {
                    if (!holders.Contains(h))
                    {
                        holders.Add(h);
                    }
                }

                string id = f.Id;
                Fetching.Add(id);
                RrMakerTransfer.RequestFile(id, f.Version, holders, b =>
                {
                    Fetching.Remove(id);
                    var data = RrMakerStore.Unpacked(b, id);
                    if (data != null && RrMakerStore.Write(data) != null)
                    {
                        RrMakerCatalog.NoteHeld(data.Info);
                        BroadcastSoon(2f);
                    }
                }, e => Fetching.Remove(id));
            }
        }

        /// <summary>Answers a request for a room file: the stored version when it is new enough (private rooms: owners only).</summary>
        private static byte[] ServeFile(string id, int minVersion, int requester)
        {
            var r = RrMakerCatalog.Get(id);
            if (r == null || r.Deleted || r.HeldVersion < minVersion)
            {
                return null;
            }

            if (!r.Info.Public)
            {
                var rec = RrNet.PresenceRecord(requester);
                int hash = rec != null && rec.Length > RrPresenceWire.AtUser ? rec[RrPresenceWire.AtUser] : 0;
                if (!RrMakerCatalog.IsOwnerOrCo(r.Info, hash))
                {
                    RrLog.Info("MakerRooms", "player " + requester + " asked for the private room \"" + r.Info.Name + "\": refused");
                    return null;
                }
            }

            int version;
            var bytes = RrMakerStore.Packed(id, out version);
            return bytes != null && version >= minVersion ? bytes : null;
        }

        // ------------------------------------------------------------------ the catalogue broadcast

        /// <summary>Announce the rooms held here within <paramref name="seconds"/>.</summary>
        public static void BroadcastSoon(float seconds = 1.5f)
        {
            float at = Time.realtimeSinceStartup + seconds;
            if (_broadcastAt < 0f || at < _broadcastAt)
            {
                _broadcastAt = at;
            }
        }

        private static void Broadcast()
        {
            var messages = RrMakerCatalog.Messages(MyHash, MyName);
            foreach (var m in messages)
            {
                RrNet.SendGlobal(RrMakerEvents.Catalog, m);
            }

            if (messages.Count > 0)
            {
                RrLog.Info("MakerRooms", "announced the rooms held here (" + messages.Count + " message(s))");
            }
        }

        private static void OnCatalog(int sender, byte[] body)
        {
            if (sender == RrNet.LocalId)
            {
                return;
            }

            try
            {
                RrMakerCatalog.Merge(sender, body, MyHash);
            }
            catch (Exception e)
            {
                RrLog.Error("MakerRooms", "a room catalogue from player " + sender + " failed to merge", e);
            }
        }

        // ------------------------------------------------------------------ the tick

        internal static void Tick()
        {
            RrMakerTransfer.Tick();
            RunFetches();
            RrMakerCatalog.Flush(false);
            float now = Time.realtimeSinceStartup;
            bool online = RrNet.Online && RrNet.SlotIndex >= 0;
            if (!online)
            {
                _wasOnline = false;
                return;
            }

            if (!_wasOnline)
            {
                _wasOnline = true;
                BroadcastSoon(3f);
            }

            // Someone new in the world: they need to hear of the rooms (everyone answers within a few seconds).
            if (now >= _presentAt)
            {
                _presentAt = now + 1f;
                foreach (int p in RrNet.Present)
                {
                    if (Seen.Add(p) && p != RrNet.LocalId)
                    {
                        BroadcastSoon(UnityEngine.Random.Range(2f, 5f));
                    }
                }
            }

            if ((_broadcastAt >= 0f && now >= _broadcastAt) || now >= _nextRegular)
            {
                _broadcastAt = -1f;
                _nextRegular = now + CatalogEvery;
                Broadcast();
            }
        }

        // ------------------------------------------------------------------ the room's hooks

        /// <summary>The registry's title for MakerRoom (the loading card's "GOING TO", the watch's This Room) = the room's name.</summary>
        public static void SetTravelTitle(string name)
        {
            var info = RrRooms.Get(RrActivity.MakerRoom);
            if (info != null)
            {
                info.Title = string.IsNullOrEmpty(name) ? DefaultTitle : name;
            }
        }

        internal static void NoteEntered(RrMakerRoom room)
        {
            _serial++;
        }

        internal static void NoteLeft(RrMakerRoom room)
        {
            SetTravelTitle(null);
            _serial++;
        }
    }
}
