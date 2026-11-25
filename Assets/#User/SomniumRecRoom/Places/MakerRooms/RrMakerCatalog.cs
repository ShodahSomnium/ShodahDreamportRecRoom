using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>A player known to hold a copy of a room (by Somnium user hash; their PlayerId is looked up live).</summary>
    [Serializable]
    public sealed class RrMakerHolder
    {
        public int Hash;
        public string Name = "";
        public int Version;
        public long SavedAt;

        /// <summary>When they last said so (UTC unix seconds).</summary>
        public long Seen;
    }

    /// <summary>A room this player knows of: its latest known settings, the version stored here and who holds which.</summary>
    [Serializable]
    public sealed class RrMakerKnownRoom
    {
        /// <summary>The newest settings heard of (Version / SavedAt = the newest version anyone announced).</summary>
        public RrMakerRoomInfo Info = new RrMakerRoomInfo();

        /// <summary>The version stored on this PC (0 = none).</summary>
        public int HeldVersion;

        public long HeldSavedAt;

        /// <summary>Its owner deleted it (a tombstone: older copies announced by others are ignored).</summary>
        public bool Deleted;

        /// <summary>Last time anything was heard of it.</summary>
        public long Seen;

        public List<RrMakerHolder> Holders = new List<RrMakerHolder>();
    }

    /// <summary>The catalogue file (MakerRooms/catalog.json).</summary>
    [Serializable]
    public sealed class RrMakerCatalogFile
    {
        public int Format = 1;
        public List<RrMakerKnownRoom> Rooms = new List<RrMakerKnownRoom>();
    }

    /// <summary>
    /// Every maker room this player knows of - their own and everyone else's - with who holds which version (task 4a).
    /// There is no server: each player broadcasts the rooms they hold (event 24, <see cref="RrMakerEvents.Catalog"/>) when
    /// they join, after a save, when someone new arrives and every 60 s, and every client merges what it hears into its
    /// own catalogue file. A room's newest version wins (SavedAt breaks ties); an owner's deletion is a tombstone.
    ///
    /// Message: [u8 format 1][i32 sender hash][str8 sender name][u8 n] then n entries
    /// [16 id][i32 version][i64 savedAt][i32 owner hash][u8 flags 1 public, 2 everyone builds, 4 deleted, 8 sender holds it]
    /// [u8 max players][str8 template][str8 name][str8 owner name][u8 co-owners][i32]*; as many entries as fit in one event.
    /// </summary>
    public static class RrMakerCatalog
    {
        public const byte Format = 1;

        /// <summary>Holders not heard from for this long are forgotten (s).</summary>
        public const long HolderTtl = 30L * 86400L;

        private static readonly Dictionary<string, RrMakerKnownRoom> Rooms = new Dictionary<string, RrMakerKnownRoom>();
        private static bool _dirty;
        private static float _savedAt;

        /// <summary>Bumped on every change (the watch redraws when it moves).</summary>
        public static int Serial { get; private set; }

        public static IEnumerable<RrMakerKnownRoom> All
        {
            get { return Rooms.Values; }
        }

        public static RrMakerKnownRoom Get(string id)
        {
            RrMakerKnownRoom r;
            return id != null && Rooms.TryGetValue(id, out r) ? r : null;
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Reads the catalogue file and the stored rooms (the files win for what is held here).</summary>
        public static void Load()
        {
            Rooms.Clear();
            var file = RrMakerStore.LoadCatalog();
            if (file.Rooms != null)
            {
                foreach (var r in file.Rooms)
                {
                    if (r == null || r.Info == null || !RrMakerWire.IsId(r.Info.Id))
                    {
                        continue;
                    }

                    r.Info.Id = r.Info.Id.ToLowerInvariant();
                    r.Info.CoOwners = r.Info.CoOwners ?? new List<int>();
                    r.Holders = r.Holders ?? new List<RrMakerHolder>();
                    r.HeldVersion = 0;
                    r.HeldSavedAt = 0;
                    Rooms[r.Info.Id] = r;
                }
            }

            int held = 0;
            foreach (var id in RrMakerStore.Ids())
            {
                var data = RrMakerStore.Load(id);
                if (data == null)
                {
                    continue;
                }

                NoteHeld(data.Info, false);
                held++;
            }

            // Forget holders nobody has heard of in a month.
            long now = RrMakerWire.Now();
            foreach (var r in Rooms.Values)
            {
                r.Holders.RemoveAll(h => h == null || now - h.Seen > HolderTtl);
            }

            Serial++;
            RrLog.Info("MakerRooms", "catalogue: " + Rooms.Count + " room(s) known, " + held + " stored on this PC (" + (RrMakerStore.Root ?? "no folder") + ")");
        }

        public static void Shutdown()
        {
            Flush();
            Rooms.Clear();
            _dirty = false;
        }

        /// <summary>Writes the file when something changed (at most every 5 s; at once with force).</summary>
        public static void Flush(bool force = true)
        {
            if (!_dirty || (!force && Time.realtimeSinceStartup - _savedAt < 5f))
            {
                return;
            }

            _dirty = false;
            _savedAt = Time.realtimeSinceStartup;
            var file = new RrMakerCatalogFile();
            file.Rooms.AddRange(Rooms.Values);
            RrMakerStore.SaveCatalog(file);
        }

        private static void Changed()
        {
            _dirty = true;
            Serial++;
        }

        // ------------------------------------------------------------------ local facts

        /// <summary>A version of a room is stored on this PC now.</summary>
        public static RrMakerKnownRoom NoteHeld(RrMakerRoomInfo info, bool log = true)
        {
            if (info == null || !RrMakerWire.IsId(info.Id))
            {
                return null;
            }

            var r = Get(info.Id);
            if (r == null)
            {
                r = new RrMakerKnownRoom();
                Rooms[info.Id] = r;
            }

            if (r.Info == null || !RrMakerWire.IsId(r.Info.Id) || Newer(info.Version, info.SavedAt, r.Info.Version, r.Info.SavedAt) || r.Info.Id == info.Id && r.Info.Version == info.Version)
            {
                r.Info = Copy(info);
            }

            if (Newer(info.Version, info.SavedAt, r.HeldVersion, r.HeldSavedAt) || r.HeldVersion == 0)
            {
                r.HeldVersion = info.Version;
                r.HeldSavedAt = info.SavedAt;
            }

            r.Seen = RrMakerWire.Now();
            if (r.Deleted && info.OwnerHash != 0 && Newer(info.Version, info.SavedAt, r.Info.Version, r.Info.SavedAt))
            {
                r.Deleted = false;
            }

            Changed();
            if (log)
            {
                RrLog.Info("MakerRooms", "stored \"" + info.Name + "\" v" + info.Version + " (" + info.Id + ")");
            }

            return r;
        }

        /// <summary>The settings changed in the room (no new version): the catalogue's copy follows.</summary>
        public static void NoteSettings(RrMakerRoomInfo info)
        {
            var r = Get(info != null ? info.Id : null);
            if (r == null)
            {
                return;
            }

            int v = r.Info.Version;
            long at = r.Info.SavedAt;
            r.Info = Copy(info);
            if (Newer(v, at, r.Info.Version, r.Info.SavedAt))
            {
                r.Info.Version = v;
                r.Info.SavedAt = at;
            }

            Changed();
        }

        /// <summary>The owner deleted the room here: tombstone + the local copy goes.</summary>
        public static void MarkDeleted(string id)
        {
            var r = Get(id);
            if (r == null)
            {
                return;
            }

            r.Deleted = true;
            r.HeldVersion = 0;
            r.HeldSavedAt = 0;
            r.Seen = RrMakerWire.Now();
            Changed();
        }

        /// <summary>This PC's copy was removed (not the owner's deletion: the room stays known).</summary>
        public static void ForgetCopy(string id)
        {
            var r = Get(id);
            if (r == null)
            {
                return;
            }

            r.HeldVersion = 0;
            r.HeldSavedAt = 0;
            Changed();
        }

        public static bool Newer(int v, long at, int thanV, long thanAt)
        {
            return v > thanV || (v == thanV && at > thanAt);
        }

        public static RrMakerRoomInfo Copy(RrMakerRoomInfo i)
        {
            if (i == null)
            {
                return new RrMakerRoomInfo();
            }

            return new RrMakerRoomInfo
            {
                Id = i.Id,
                Name = i.Name,
                Description = i.Description,
                Template = i.Template,
                OwnerHash = i.OwnerHash,
                OwnerName = i.OwnerName,
                CoOwners = i.CoOwners != null ? new List<int>(i.CoOwners) : new List<int>(),
                EveryoneCanBuild = i.EveryoneCanBuild,
                Public = i.Public,
                MaxPlayers = i.MaxPlayers,
                Version = i.Version,
                SavedAt = i.SavedAt
            };
        }

        // ------------------------------------------------------------------ queries

        public static bool IsOwnerOrCo(RrMakerRoomInfo info, int hash)
        {
            return info != null && hash != 0 && (info.OwnerHash == hash || (info.CoOwners != null && info.CoOwners.Contains(hash)));
        }

        /// <summary>Listed for this player: not deleted, and public or theirs (owner / co-owner).</summary>
        public static bool Visible(RrMakerKnownRoom r, int myHash)
        {
            return r != null && !r.Deleted && r.Info != null && (r.Info.Public || IsOwnerOrCo(r.Info, myHash));
        }

        /// <summary>The newest version anyone is known to hold (the stored one included).</summary>
        public static int LatestVersion(RrMakerKnownRoom r)
        {
            if (r == null)
            {
                return 0;
            }

            int v = Math.Max(r.HeldVersion, r.Info != null ? r.Info.Version : 0);
            foreach (var h in r.Holders)
            {
                v = Math.Max(v, h.Version);
            }

            return v;
        }

        /// <summary>Present players' Somnium user hashes (presence int 6), by PlayerId; the local one excluded.</summary>
        public static Dictionary<int, int> PresentHashes()
        {
            var map = new Dictionary<int, int>();
            if (!RrNet.Online)
            {
                return map;
            }

            int local = RrNet.LocalId;
            foreach (int id in RrNet.Present)
            {
                if (id == local)
                {
                    continue;
                }

                var rec = RrNet.PresenceRecord(id);
                if (rec != null && rec.Length > Net.RrPresenceWire.AtUser && rec[Net.RrPresenceWire.AtUser] != 0)
                {
                    map[id] = rec[Net.RrPresenceWire.AtUser];
                }
            }

            return map;
        }

        /// <summary>PlayerIds of present holders of at least <paramref name="minVersion"/>, newest copy first.</summary>
        public static List<int> OnlineHolders(RrMakerKnownRoom r, int minVersion)
        {
            var list = new List<int>();
            if (r == null)
            {
                return list;
            }

            var present = PresentHashes();
            var byVersion = new List<KeyValuePair<int, int>>();
            foreach (var kv in present)
            {
                foreach (var h in r.Holders)
                {
                    if (h.Hash == kv.Value && h.Version >= minVersion)
                    {
                        byVersion.Add(new KeyValuePair<int, int>(kv.Key, h.Version));
                        break;
                    }
                }
            }

            byVersion.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in byVersion)
            {
                list.Add(kv.Key);
            }

            return list;
        }

        // ------------------------------------------------------------------ the broadcast (event 24)

        /// <summary>The catalogue messages this player sends: every room stored here, and the tombstones of rooms they deleted.</summary>
        public static List<byte[]> Messages(int myHash, string myName)
        {
            var list = new List<byte[]>();
            var entries = new List<byte[]>();
            long now = RrMakerWire.Now();
            foreach (var r in Rooms.Values)
            {
                bool held = r.HeldVersion > 0 && !r.Deleted;
                bool tomb = r.Deleted && r.Info.OwnerHash == myHash && now - r.Seen < HolderTtl;
                if (!held && !tomb)
                {
                    continue;
                }

                var w = new RrMakerWriter();
                WriteEntry(w, r, held);
                entries.Add(w.ToArray());
            }

            int i = 0;
            while (i < entries.Count)
            {
                var w = new RrMakerWriter();
                w.U8(Format);
                w.I32(myHash);
                w.Str8(Short(myName, 24));
                int countAt = w.Length;
                w.U8(0);
                var body = w.ToArray();
                var msg = new List<byte>(body);
                int n = 0;
                while (i < entries.Count && msg.Count + entries[i].Length <= RrMakerWire.MaxEvent && n < 255)
                {
                    msg.AddRange(entries[i]);
                    i++;
                    n++;
                }

                if (n == 0)
                {
                    // One entry alone too big (a huge co-owner list): skip it.
                    i++;
                    continue;
                }

                msg[countAt] = (byte)n;
                list.Add(msg.ToArray());
            }

            return list;
        }

        private static void WriteEntry(RrMakerWriter w, RrMakerKnownRoom r, bool held)
        {
            var info = r.Info;
            int version = held ? r.HeldVersion : info.Version;
            long savedAt = held ? r.HeldSavedAt : info.SavedAt;
            w.Bytes(RrMakerWire.IdBytes(info.Id));
            w.I32(version);
            w.I64(savedAt);
            w.I32(info.OwnerHash);
            w.U8((byte)((info.Public ? 1 : 0) | (info.EveryoneCanBuild ? 2 : 0) | (r.Deleted ? 4 : 0) | (held ? 8 : 0)));
            w.U8((byte)Mathf.Clamp(info.MaxPlayers, 1, 40));
            w.Str8(Short(info.Template, 24));
            w.Str8(Short(info.Name, 48));
            w.Str8(Short(info.OwnerName, 24));
            int n = info.CoOwners != null ? Math.Min(info.CoOwners.Count, 16) : 0;
            w.U8((byte)n);
            for (int k = 0; k < n; k++)
            {
                w.I32(info.CoOwners[k]);
            }
        }

        private static string Short(string s, int max)
        {
            s = s ?? "";
            return s.Length > max ? s.Substring(0, max) : s;
        }

        /// <summary>
        /// Merges a received catalogue message. Returns the ids of rooms whose newest known version rose (the room page and
        /// an open maker room may want them).
        /// </summary>
        public static List<string> Merge(int sender, byte[] body, int myHash)
        {
            var risen = new List<string>();
            var r = new RrMakerReader(body);
            if (r.U8() != Format)
            {
                return risen;
            }

            int senderHash = r.I32();
            string senderName = r.Str8();
            int n = r.U8();
            if (!r.Ok || senderHash == 0)
            {
                return risen;
            }

            // The sender's presence must say who they are (the hash in the message is theirs, not someone else's).
            var rec = RrNet.Online ? RrNet.PresenceRecord(sender) : null;
            if (rec != null && rec.Length > RrPresenceWire.AtUser && rec[RrPresenceWire.AtUser] != 0 && rec[RrPresenceWire.AtUser] != senderHash)
            {
                RrLog.WarnOnce("MakerRooms", "catalog-hash:" + sender, "a room catalogue from player " + sender + " names another player's hash: ignored");
                return risen;
            }

            long now = RrMakerWire.Now();
            bool changed = false;
            for (int k = 0; k < n && r.Ok; k++)
            {
                var info = new RrMakerRoomInfo();
                info.Id = RrMakerWire.IdFrom(r.Bytes(16), 0);
                info.Version = r.I32();
                info.SavedAt = r.I64();
                info.OwnerHash = r.I32();
                int flags = r.U8();
                info.MaxPlayers = r.U8();
                info.Template = r.Str8();
                info.Name = RrMakerRooms.CleanName(r.Str8());
                info.OwnerName = r.Str8();
                int co = r.U8();
                info.CoOwners = new List<int>(co);
                for (int c = 0; c < co && r.Ok; c++)
                {
                    info.CoOwners.Add(r.I32());
                }

                if (!r.Ok || !RrMakerWire.IsId(info.Id))
                {
                    break;
                }

                info.Public = (flags & 1) != 0;
                info.EveryoneCanBuild = (flags & 2) != 0;
                bool deleted = (flags & 4) != 0;
                bool held = (flags & 8) != 0;

                var known = Get(info.Id);
                if (deleted)
                {
                    // Only the owner can delete a room.
                    if (info.OwnerHash != senderHash || info.OwnerHash == myHash)
                    {
                        continue;
                    }

                    if (known == null)
                    {
                        known = new RrMakerKnownRoom { Info = info };
                        Rooms[info.Id] = known;
                    }

                    if (!known.Deleted)
                    {
                        bool hadCopy = known.HeldVersion > 0;
                        known.Info = info;
                        known.Deleted = true;
                        known.HeldVersion = 0;
                        known.Holders.Clear();
                        if (hadCopy)
                        {
                            RrMakerStore.Delete(info.Id);
                        }

                        RrLog.Info("MakerRooms", "\"" + info.Name + "\" was deleted by its owner " + info.OwnerName + (hadCopy ? ": the copy on this PC is removed" : ""));
                        changed = true;
                    }

                    continue;
                }

                if (known != null && known.Deleted && !Newer(info.Version, info.SavedAt, known.Info.Version, known.Info.SavedAt))
                {
                    continue;
                }

                if (known == null)
                {
                    known = new RrMakerKnownRoom { Info = info };
                    Rooms[info.Id] = known;
                    changed = true;
                    risen.Add(info.Id);
                }
                else if (Newer(info.Version, info.SavedAt, known.Info.Version, known.Info.SavedAt))
                {
                    known.Info = info;
                    known.Deleted = false;
                    changed = true;
                    risen.Add(info.Id);
                }

                known.Seen = now;
                if (held)
                {
                    RrMakerHolder h = null;
                    foreach (var x in known.Holders)
                    {
                        if (x.Hash == senderHash)
                        {
                            h = x;
                            break;
                        }
                    }

                    if (h == null)
                    {
                        h = new RrMakerHolder { Hash = senderHash };
                        known.Holders.Add(h);
                        changed = true;
                    }

                    if (h.Version != info.Version || h.SavedAt != info.SavedAt || h.Name != senderName)
                    {
                        changed = true;
                    }

                    h.Name = senderName;
                    h.Version = info.Version;
                    h.SavedAt = info.SavedAt;
                    h.Seen = now;
                }
            }

            if (changed)
            {
                Changed();
            }

            return risen;
        }
    }
}
