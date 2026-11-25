using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>
    /// The room over the network (task 4c), all scoped to the instance by RrNet:
    ///
    /// - Edits (event 27): [op][...] - upsert (the whole object), transform (id, rev, pose), delete (id, rev); messages
    ///   larger than one event are split (OpPart). Every object carries a revision (rev, author PlayerId): an edit is applied
    ///   only when it is newer than what this client has, deletions leave tombstones, so edits that cross a late joiner's
    ///   state transfer, or each other, settle the same way on every client. Edits from players without the right to build
    ///   are ignored.
    /// - Live state (event 28 asks the instance authority, event 26 kind 2 answers): settings, objects, revisions, tombstones.
    /// - Meta (event 29): settings changed (name, public, everyone can build, co-owners, max players) and "saved version v";
    ///   the others then fetch that version's file from the saver, so everyone who was there holds the room too.
    /// - Instance state (written by the instance authority): custom ints 0-3 the room id, 4 its version, 5 flags (1 public,
    ///   2 everyone builds); option Private for a private room (RrNet's matchmaking skips it).
    /// - Auto-save every <see cref="AutoSaveSeconds"/> when something changed, by one designated saver (the owner when
    ///   present, else the co-owner with the lowest PlayerId), and on leave by any owner/co-owner with unsaved changes.
    /// - Conflict: if the instance turns out to play another room (two rooms picked the same free number at once), the
    ///   player who is not its authority re-opens their room in another instance.
    /// </summary>
    public sealed partial class RrMakerRoom
    {
        public const int IntId0 = 0;
        public const int IntVersion = 4;
        public const int IntFlags = 5;

        public const float AutoSaveSeconds = 120f;

        private bool _dirty;
        private float _dirtySince;
        private float _lastSaveAt;
        private List<int> _savers;
        private readonly RrMakerJoiner _joiner = new RrMakerJoiner();
        private readonly List<KeyValuePair<int, byte[]>> _buffered = new List<KeyValuePair<int, byte[]>>();
        private readonly List<KeyValuePair<int, ushort>> _asks = new List<KeyValuePair<int, ushort>>();
        private ushort _msgId;
        private float _checkAt;
        private float _conflictSince = -1f;
        private bool _reopened;

        /// <summary>Changes since the last save.</summary>
        public bool Dirty
        {
            get { return _dirty; }
        }

        /// <summary>Realtime of the last save here (0 none).</summary>
        public float LastSaveAt
        {
            get { return _lastSaveAt; }
        }

        private void MarkDirty()
        {
            if (!_dirty)
            {
                _dirty = true;
                _dirtySince = Time.realtimeSinceStartup;
            }
        }

        private static bool Newer(uint rev, int author, uint thanRev, int thanAuthor)
        {
            return rev > thanRev || (rev == thanRev && author > thanAuthor);
        }

        // ------------------------------------------------------------------ sending

        private void SendMessage(byte type, byte[] message)
        {
            unchecked
            {
                _msgId++;
            }

            foreach (var part in RrMakerWire.Split(message, _msgId))
            {
                RrNet.Send(type, part);
            }
        }

        private void SendUpsert(Entry e)
        {
            var w = new RrMakerWriter();
            w.U8(RrMakerWire.OpUpsert);
            RrMakerWire.WriteObject(w, e.O, e.Rev, e.Author);
            SendMessage(RrMakerEvents.Edit, w.ToArray());
        }

        private void SendTransform(Entry e)
        {
            var w = new RrMakerWriter();
            w.U8(RrMakerWire.OpTransform);
            w.U32(e.O.Id);
            w.U32(e.Rev);
            w.U16(e.Author);
            w.Vec3(e.O.Position);
            w.Quat(e.O.Rotation);
            w.Vec3(e.O.Scale);
            SendMessage(RrMakerEvents.Edit, w.ToArray());
        }

        private void SendDelete(uint id, uint rev)
        {
            var w = new RrMakerWriter();
            w.U8(RrMakerWire.OpDelete);
            w.U32(id);
            w.U32(rev);
            w.U16(RrNet.LocalId);
            SendMessage(RrMakerEvents.Edit, w.ToArray());
        }

        private void SendSaved(int version, long savedAt)
        {
            var w = new RrMakerWriter();
            w.U8(RrMakerWire.MetaSaved);
            w.I32(version);
            w.I64(savedAt);
            w.I32(RrMakerRooms.MyHash);
            SendMessage(RrMakerEvents.Meta, w.ToArray());
        }

        private void SendSettings()
        {
            var w = new RrMakerWriter();
            w.U8(RrMakerWire.MetaSettings);
            RrMakerWire.WriteSettings(w, _info);
            SendMessage(RrMakerEvents.Meta, w.ToArray());
        }

        // ------------------------------------------------------------------ who may do what (other players)

        private static int HashOf(int player)
        {
            if (player == RrNet.LocalId)
            {
                return RrMakerRooms.MyHash;
            }

            var rec = RrNet.PresenceRecord(player);
            return rec != null && rec.Length > RrPresenceWire.AtUser ? rec[RrPresenceWire.AtUser] : 0;
        }

        private bool SenderMayBuild(int sender)
        {
            return _info != null && (_info.EveryoneCanBuild || RrMakerCatalog.IsOwnerOrCo(_info, HashOf(sender)));
        }

        private bool SenderMaySave(int sender)
        {
            return _info != null && RrMakerCatalog.IsOwnerOrCo(_info, HashOf(sender));
        }

        // ------------------------------------------------------------------ edits

        private void OnEdit(int sender, byte[] body)
        {
            if (sender == RrNet.LocalId || body == null || body.Length == 0)
            {
                return;
            }

            if (body[0] == RrMakerWire.OpPart)
            {
                body = _joiner.Add(sender, body);
                if (body == null)
                {
                    return;
                }
            }

            if (!Built)
            {
                if (_buffered.Count < 5000)
                {
                    _buffered.Add(new KeyValuePair<int, byte[]>(sender, body));
                }

                return;
            }

            if (!SenderMayBuild(sender))
            {
                RrLog.WarnOnce("MakerRooms", "edit-denied:" + sender, "an edit from player " + sender + ", who may not build in \"" + (_info != null ? _info.Name : "?") + "\", was ignored");
                return;
            }

            try
            {
                ApplyEdit(sender, body);
            }
            catch (Exception e)
            {
                RrLog.Error("MakerRooms", "applying an edit from player " + sender + " failed", e);
            }
        }

        /// <summary>Edits that came while the room was building, on top of its state (the revisions sort them out).</summary>
        private void ApplyBuffered()
        {
            if (_buffered.Count == 0)
            {
                return;
            }

            var list = new List<KeyValuePair<int, byte[]>>(_buffered);
            _buffered.Clear();
            foreach (var kv in list)
            {
                if (!SenderMayBuild(kv.Key))
                {
                    continue;
                }

                try
                {
                    ApplyEdit(kv.Key, kv.Value);
                }
                catch (Exception e)
                {
                    RrLog.Error("MakerRooms", "applying a buffered edit failed", e);
                }
            }

            RrLog.Info("MakerRooms", list.Count + " edit(s) that came during the build applied");
        }

        private void ApplyEdit(int sender, byte[] body)
        {
            var r = new RrMakerReader(body);
            byte op = r.U8();
            switch (op)
            {
                case RrMakerWire.OpUpsert:
                {
                    uint rev;
                    int author;
                    var o = RrMakerWire.ReadObject(r, out rev, out author);
                    if (!r.Ok || o.Id == 0)
                    {
                        return;
                    }

                    ApplyUpsert(Sanitize(o), rev, sender);
                    return;
                }

                case RrMakerWire.OpTransform:
                {
                    uint id = r.U32();
                    uint rev = r.U32();
                    r.U16();
                    var pos = r.Vec3();
                    var rot = r.Quat();
                    var scale = r.Vec3();
                    Entry e;
                    if (!r.Ok || !_entries.TryGetValue(id, out e) || !Newer(rev, sender, e.Rev, e.Author))
                    {
                        return;
                    }

                    var moved = e.O.Clone();
                    moved.Position = pos;
                    moved.Rotation = rot;
                    moved.Scale = scale;
                    moved = Sanitize(moved);
                    e.O = moved;
                    e.Rev = rev;
                    e.Author = sender;
                    Glide(e);
                    MarkDirty();
                    RrMakerEdits.RaiseChanged(id);
                    return;
                }

                case RrMakerWire.OpDelete:
                {
                    uint id = r.U32();
                    uint rev = r.U32();
                    if (!r.Ok)
                    {
                        return;
                    }

                    Entry e;
                    if (_entries.TryGetValue(id, out e))
                    {
                        if (!Newer(rev, sender, e.Rev, e.Author))
                        {
                            return;
                        }

                        Remove(e);
                    }

                    Tomb t;
                    if (!_dead.TryGetValue(id, out t) || Newer(rev, sender, t.Rev, t.Author))
                    {
                        _dead[id] = new Tomb { Rev = rev, Author = sender };
                    }

                    MarkDirty();
                    RrMakerEdits.RaiseChanged(id);
                    return;
                }
            }
        }

        private void ApplyUpsert(RrMakerObject o, uint rev, int author)
        {
            Entry e;
            if (_entries.TryGetValue(o.Id, out e))
            {
                if (!Newer(rev, author, e.Rev, e.Author))
                {
                    return;
                }

                bool look = !RrMakerWire.SameLook(e.O, o);
                e.O = o;
                e.Rev = rev;
                e.Author = author;
                if (look)
                {
                    BuildVisual(e);
                }
                else
                {
                    Glide(e);
                }
            }
            else
            {
                Tomb t;
                if (_dead.TryGetValue(o.Id, out t) && !Newer(rev, author, t.Rev, t.Author))
                {
                    return;
                }

                if (_entries.Count >= MaxObjects)
                {
                    RrLog.WarnOnce("MakerRooms", "full-remote:" + RoomId, "the room is full (" + MaxObjects + " objects): remote creations are dropped");
                    return;
                }

                _dead.Remove(o.Id);
                e = new Entry { O = o, Rev = rev, Author = author };
                _entries[o.Id] = e;
                BuildVisual(e);
            }

            MarkDirty();
            RrMakerEdits.RaiseChanged(o.Id);
        }

        /// <summary>A remote move: the container glides to the new pose (updates arrive at about 5 Hz while someone drags).</summary>
        private void Glide(Entry e)
        {
            if (e.Go == null)
            {
                return;
            }

            e.ToPos = e.O.Position;
            e.ToRot = e.O.Rotation;
            e.ToScale = e.O.Kind == RrMakerKind.Light ? Vector3.one : e.O.Scale;
            e.Lerp = true;
            if (e.HasLights)
            {
                LightsChanged();
            }
        }

        // ------------------------------------------------------------------ the live state (late joiners)

        private void OnAskState(int sender, byte[] body)
        {
            var r = new RrMakerReader(body);
            ushort transfer = (ushort)r.U16();
            if (!r.Ok || sender == RrNet.LocalId)
            {
                return;
            }

            if (!Built || _info == null)
            {
                // Answered once the room is built (a joiner can ask while the authority itself is still loading).
                _asks.Add(new KeyValuePair<int, ushort>(sender, transfer));
                return;
            }

            SendState(sender, transfer);
        }

        private void SendState(int target, ushort transfer)
        {
            var s = new RrMakerWireState { Info = RrMakerCatalog.Copy(_info) };
            var ids = new List<uint>(_entries.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                var e = _entries[id];
                s.Objects.Add(e.O.Clone());
                s.Revs.Add(e.Rev);
                s.Authors.Add(e.Author);
            }

            foreach (var kv in _dead)
            {
                s.DeadIds.Add(kv.Key);
                s.DeadRevs.Add(kv.Value.Rev);
                s.DeadAuthors.Add(kv.Value.Author);
            }

            RrMakerTransfer.Send(target, RrMakerWire.KindState, transfer, s.Pack());
        }

        // ------------------------------------------------------------------ settings and saves

        private void OnMeta(int sender, byte[] body)
        {
            if (sender == RrNet.LocalId || body == null || body.Length == 0 || _info == null)
            {
                return;
            }

            if (body[0] == RrMakerWire.OpPart)
            {
                body = _joiner.Add(sender, body);
                if (body == null)
                {
                    return;
                }
            }

            if (!SenderMaySave(sender))
            {
                RrLog.WarnOnce("MakerRooms", "meta-denied:" + sender, "a settings/save message from player " + sender + ", who is not an owner of \"" + _info.Name + "\", was ignored");
                return;
            }

            var r = new RrMakerReader(body);
            byte op = r.U8();
            if (op == RrMakerWire.MetaSettings)
            {
                var next = RrMakerCatalog.Copy(_info);
                RrMakerWire.ReadSettings(r, next);
                if (!r.Ok)
                {
                    return;
                }

                // Only the owner changes the co-owners.
                if (HashOf(sender) != _info.OwnerHash)
                {
                    next.CoOwners = new List<int>(_info.CoOwners);
                }

                bool couldEdit = CanEdit;
                _info.Name = next.Name;
                _info.Public = next.Public;
                _info.EveryoneCanBuild = next.EveryoneCanBuild;
                _info.MaxPlayers = next.MaxPlayers;
                _info.CoOwners = next.CoOwners;
                RrMakerCatalog.NoteSettings(_info);
                RrMakerRooms.SetTravelTitle(_info.Name);
                WriteInstanceState();
                RrLog.Info("MakerRooms", "player " + sender + " changed the settings of \"" + _info.Name + "\": " + (_info.Public ? "public" : "private") +
                                         (_info.EveryoneCanBuild ? ", everyone can build" : "") + ", " + _info.CoOwners.Count + " co-owner(s)" +
                                         (couldEdit != CanEdit ? "; you " + (CanEdit ? "can build now" : "can no longer build") : ""));
                RrMakerRooms.Touch();
            }
            else if (op == RrMakerWire.MetaSaved)
            {
                int version = r.I32();
                long savedAt = r.I64();
                r.I32();
                if (!r.Ok)
                {
                    return;
                }

                if (RrMakerCatalog.Newer(version, savedAt, _info.Version, _info.SavedAt))
                {
                    _info.Version = version;
                    _info.SavedAt = savedAt;
                    _dirty = false;
                }

                RrLog.Info("MakerRooms", "player " + sender + " saved \"" + _info.Name + "\" as v" + version);
                RrMakerRooms.FetchLater(_info.Id, version, sender);
                RrMakerRooms.Touch();
            }
        }

        /// <summary>Renames the room (owner / co-owner). Saved at once as a new version.</summary>
        public bool SetName(string name)
        {
            name = RrMakerRooms.CleanName(name);
            if (!CanSave || name == _info.Name)
            {
                return false;
            }

            _info.Name = name;
            SettingsChanged("renamed to \"" + name + "\"");
            return true;
        }

        public bool SetPublic(bool on)
        {
            if (!CanSave || on == _info.Public)
            {
                return false;
            }

            _info.Public = on;
            SettingsChanged(on ? "public" : "private");
            return true;
        }

        public bool SetEveryoneCanBuild(bool on)
        {
            if (!CanSave || on == _info.EveryoneCanBuild)
            {
                return false;
            }

            _info.EveryoneCanBuild = on;
            SettingsChanged(on ? "everyone can build" : "only owners build");
            return true;
        }

        /// <summary>Adds or removes a co-owner by Somnium user hash (owner only; Rec Room: the creator manages the roles).</summary>
        public bool SetCoOwner(int hash, bool on)
        {
            if (!IsOwner || !Built || hash == 0 || hash == _info.OwnerHash)
            {
                return false;
            }

            bool has = _info.CoOwners.Contains(hash);
            if (has == on)
            {
                return false;
            }

            if (on)
            {
                _info.CoOwners.Add(hash);
            }
            else
            {
                _info.CoOwners.Remove(hash);
            }

            SettingsChanged((on ? "co-owner added " : "co-owner removed ") + hash.ToString("x8"));
            return true;
        }

        private void SettingsChanged(string what)
        {
            SendSettings();
            RrMakerCatalog.NoteSettings(_info);
            RrMakerRooms.SetTravelTitle(_info.Name);
            WriteInstanceState();
            RrLog.Info("MakerRooms", "\"" + _info.Name + "\": " + what);
            RrMakerRooms.SaveRoom(this, false);
        }

        // ------------------------------------------------------------------ instance state

        /// <summary>The instance authority writes which room the instance plays (others read it when joining without a pick).</summary>
        private void WriteInstanceState()
        {
            if (_info == null || !RrNet.IsInstanceAuthority || (RrNet.Online && RrNet.InstanceObjectIndex < 0))
            {
                return;
            }

            var s = RrNet.Instance;
            if (!s.CanWrite)
            {
                return;
            }

            var ids = new int[4];
            RrMakerWire.IdInts(_info.Id, ids);
            for (int i = 0; i < 4; i++)
            {
                if (s.Get(IntId0 + i) != ids[i])
                {
                    s.Set(IntId0 + i, ids[i]);
                }
            }

            if (s.Get(IntVersion) != _info.Version)
            {
                s.Set(IntVersion, _info.Version);
            }

            int flags = (_info.Public ? 1 : 0) | (_info.EveryoneCanBuild ? 2 : 0);
            if (s.Get(IntFlags) != flags)
            {
                s.Set(IntFlags, flags);
            }

            if (s.Option(RrInstanceWire.OptionPrivate) == _info.Public)
            {
                s.SetOption(RrInstanceWire.OptionPrivate, !_info.Public);
            }
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!Built || _info == null)
            {
                return;
            }

            TickObjects();
            float now = Time.realtimeSinceStartup;
            if (_asks.Count > 0)
            {
                var asks = new List<KeyValuePair<int, ushort>>(_asks);
                _asks.Clear();
                foreach (var kv in asks)
                {
                    SendState(kv.Key, kv.Value);
                }
            }

            if (now < _checkAt)
            {
                return;
            }

            _checkAt = now + 1f;
            _joiner.Expire();
            if (_lastSaveAt <= 0f)
            {
                _lastSaveAt = now;
            }

            // Auto-save (Rec Room's backup frequency; 2 min here), by one designated saver.
            if (_dirty && CanSave && now - _lastSaveAt >= AutoSaveSeconds && now - _dirtySince >= 10f && IsDesignatedSaver())
            {
                RrMakerRooms.SaveRoom(this, true);
            }

            CheckConflict(now);
        }

        /// <summary>The owner when present in the instance, else the co-owner with the lowest PlayerId.</summary>
        private bool IsDesignatedSaver()
        {
            if (_savers == null)
            {
                _savers = new List<int>();
                int owner = -1;
                int co = -1;
                foreach (int m in RrNet.MembersOfMyInstance())
                {
                    int h = HashOf(m);
                    if (h != 0 && h == _info.OwnerHash)
                    {
                        owner = m;
                    }
                    else if (h != 0 && _info.CoOwners.Contains(h) && (co < 0 || m < co))
                    {
                        co = m;
                    }
                }

                if (owner >= 0 || co >= 0)
                {
                    _savers.Add(owner >= 0 ? owner : co);
                }
            }

            return _savers.Count == 0 || _savers[0] == RrNet.LocalId;
        }

        private void CheckConflict(float now)
        {
            if (!RrNet.Online || RrNet.IsInstanceAuthority || _reopened)
            {
                _conflictSince = -1f;
                return;
            }

            var s = RrNet.Instance;
            string named = RrMakerWire.IdFromInts(s.Get(IntId0), s.Get(IntId0 + 1), s.Get(IntId0 + 2), s.Get(IntId0 + 3));
            // The authority itself says it plays this room (presence int 19): the ints only lag behind (it writes them
            // after resolving its room; an earlier room's ints can stand until then).
            int authority = RrNet.InstanceAuthorityId;
            bool authorityPlaysMine = authority != RrNet.LocalId && RrNet.PlayerInt(authority, 0) == RrMakerWire.RoomHash(_info.Id);
            if (!RrMakerWire.IsId(named) || named == _info.Id || authorityPlaysMine)
            {
                _conflictSince = -1f;
                return;
            }

            if (_conflictSince < 0f)
            {
                _conflictSince = now;
                return;
            }

            if (now - _conflictSince < 3f)
            {
                return;
            }

            _reopened = true;
            RrLog.Warn("MakerRooms", "instance #" + InstanceNumber + " plays room " + named + ", not \"" + _info.Name + "\" (both picked the same free instance): opening it in another instance");
            RrMakerRooms.Open(_info.Id, InstanceNumber);
        }
    }
}
