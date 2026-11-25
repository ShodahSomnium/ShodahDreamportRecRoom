using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>
    /// A player-made room (RrActivity.MakerRoom), Rec Room's custom room (rr_maker_rooms.md 1, 5): a template's fixed base
    /// (<see cref="RrMakerTemplate.BuildBase"/>) plus the room's objects, everything under this transform at the world origin.
    /// It is the build tools' host (<see cref="IRrMakerRoomHost"/>, set on <see cref="RrMakerEdits.Host"/> while the local
    /// player is inside): edits are applied here at once and sent to the instance (event 27); a late joiner gets the live
    /// state from the instance authority (events 28 / 26); settings and saves travel as event 29.
    ///
    /// Which room: the one <see cref="RrMakerRooms"/> picked (Create / Open on the watch), else the one the instance names
    /// (its state's custom ints 0-3, or the members' presence int 19), else the player's newest own room, else a new Maker
    /// Room for them (a GoTo without a pick: the Play page, a door, joining a friend).
    ///
    /// Files: this one (lifecycle, which room, the host API), RrMakerRoom.Objects.cs (the objects' visuals and lights),
    /// RrMakerRoom.Net.cs (edits, live state, settings, saves, instance state, auto-save).
    /// </summary>
    public sealed partial class RrMakerRoom : RrRoom, IRrMakerRoomHost
    {
        /// <summary>The live maker room (null elsewhere).</summary>
        public static RrMakerRoom Current { get; private set; }

        /// <summary>A room's objects at most (Rec Room's ink limit, as a count; RrMakerStore.MaxObjects for files).</summary>
        public const int MaxObjects = 3000;

        /// <summary>Waiting for the instance authority's live state, s.</summary>
        public const float StateWait = 25f;

        /// <summary>Waiting for spawns (props, meshes), s.</summary>
        public const float SpawnWait = 60f;

        /// <summary>
        /// The whole build stays inside this many seconds (travel gives a room up after RrTravel.BuildTimeout = 90 s and falls
        /// back to the Dorm): the base gets up to <see cref="BaseBudget"/> s from the start (then it is left as far as it got),
        /// the waits for the live state and the spawns end by this time whatever their own limits say.
        /// </summary>
        public const float BuildBudget = 84f;

        public const float BaseBudget = 72f;

        private sealed class Entry
        {
            public RrMakerObject O;
            public uint Rev;
            public int Author;
            public GameObject Go;
            public int Serial;
            public GameObject Bulb;
            public Light Light;
            public Mesh OwnMesh;
            public bool HasLights;
            public bool Lerp;
            public Vector3 ToPos;
            public Quaternion ToRot;
            public Vector3 ToScale;
        }

        private struct Tomb
        {
            public uint Rev;
            public int Author;
        }

        private readonly Dictionary<uint, Entry> _entries = new Dictionary<uint, Entry>();
        private readonly Dictionary<uint, Tomb> _dead = new Dictionary<uint, Tomb>();
        private RrMakerRoomInfo _info;
        private Transform _objects;
        private RrMakerBase _base;
        private int _pending;
        private uint _counter;
        private bool _hostSet;

        /// <summary>The template this room stands on.</summary>
        public RrMakerTemplate Template { get; private set; }

        /// <summary>Created by this player for this visit: the Maker Pen is handed over on arrival, base pieces are settled.</summary>
        public bool Fresh { get; private set; }

        /// <summary>Where the room's content came from (for the log and the watch).</summary>
        public string Source { get; private set; } = "";

        /// <summary>The room id ("" until resolved).</summary>
        public string RoomId
        {
            get { return _info != null ? _info.Id : ""; }
        }

        public int ObjectCount
        {
            get { return _entries.Count; }
        }

        // ------------------------------------------------------------------ IRrMakerRoomHost

        /// <summary>The local player may build: owner, co-owner, or everyone when the room says so.</summary>
        public bool CanEdit
        {
            get { return Built && _info != null && (_info.EveryoneCanBuild || RrMakerRooms.IsOwnerOrCo(_info)); }
        }

        /// <summary>The local player may save and change the settings: owner or co-owner.</summary>
        public bool CanSave
        {
            get { return Built && _info != null && RrMakerRooms.IsOwnerOrCo(_info); }
        }

        /// <summary>The local player created the room.</summary>
        public bool IsOwner
        {
            get { return _info != null && _info.OwnerHash != 0 && _info.OwnerHash == RrMakerRooms.MyHash; }
        }

        /// <summary>The room's live settings (read only; change them through the setters in RrMakerRoom.Net.cs).</summary>
        public RrMakerRoomInfo RoomInfo
        {
            get { return _info; }
        }

        // Explicit: RrRoom.Info (the registry entry) keeps its name for travel.
        RrMakerRoomInfo IRrMakerRoomHost.Info
        {
            get { return _info; }
        }

        // Explicit: a MonoBehaviour method named Update with a parameter would confuse Unity's message lookup.
        void IRrMakerRoomHost.Update(RrMakerObject o)
        {
            UpdateObject(o);
        }

        public IEnumerable<uint> Ids
        {
            get { return new List<uint>(_entries.Keys); }
        }

        public int Remaining
        {
            get { return Math.Max(0, MaxObjects - _entries.Count); }
        }

        public uint Create(RrMakerObject o)
        {
            if (o == null || !Allowed("create"))
            {
                return 0;
            }

            if (_entries.Count >= MaxObjects)
            {
                RrLog.WarnOnce("MakerRooms", "full:" + RoomId, "the room holds " + MaxObjects + " objects: no more can be made");
                return 0;
            }

            o = Sanitize(o.Clone());
            if (o.Id == 0 || _entries.ContainsKey(o.Id))
            {
                o.Id = NextId();
            }

            Tomb tomb;
            uint rev = _dead.TryGetValue(o.Id, out tomb) ? tomb.Rev + 1 : 1;
            _dead.Remove(o.Id);
            var e = new Entry { O = o, Rev = rev, Author = RrNet.LocalId };
            _entries[o.Id] = e;
            BuildVisual(e);
            SendUpsert(e);
            MarkDirty();
            RrMakerEdits.RaiseChanged(o.Id);
            return o.Id;
        }

        /// <summary>IRrMakerRoomHost.Update: changes an object (by Id).</summary>
        public void UpdateObject(RrMakerObject o)
        {
            if (o == null || !Allowed("update"))
            {
                return;
            }

            Entry e;
            if (!_entries.TryGetValue(o.Id, out e))
            {
                return;
            }

            o = Sanitize(o.Clone());
            bool look = !RrMakerWire.SameLook(e.O, o);
            e.O = o;
            e.Rev++;
            e.Author = RrNet.LocalId;
            e.Lerp = false;
            if (look)
            {
                BuildVisual(e);
                SendUpsert(e);
            }
            else
            {
                ApplyTransform(e);
                SendTransform(e);
            }

            MarkDirty();
            RrMakerEdits.RaiseChanged(o.Id);
        }

        public void Delete(uint id)
        {
            if (!Allowed("delete"))
            {
                return;
            }

            Entry e;
            if (!_entries.TryGetValue(id, out e))
            {
                return;
            }

            uint rev = e.Rev + 1;
            Remove(e);
            _dead[id] = new Tomb { Rev = rev, Author = RrNet.LocalId };
            SendDelete(id, rev);
            MarkDirty();
            RrMakerEdits.RaiseChanged(id);
        }

        public RrMakerObject Get(uint id)
        {
            Entry e;
            return _entries.TryGetValue(id, out e) ? e.O.Clone() : null;
        }

        public GameObject Find(uint id)
        {
            Entry e;
            return _entries.TryGetValue(id, out e) ? e.Go : null;
        }

        public uint IdOf(Component c)
        {
            if (c == null)
            {
                return 0;
            }

            var mark = c.GetComponentInParent<RrMakerObjectMark>(true);
            return mark != null && mark.Room == this ? mark.Id : 0;
        }

        public bool Save()
        {
            return RrMakerRooms.SaveRoom(this, false);
        }

        private bool Allowed(string what)
        {
            if (CanEdit)
            {
                return true;
            }

            RrLog.WarnOnce("MakerRooms", "denied:" + what + ":" + RoomId, "a " + what + " in \"" + (_info != null ? _info.Name : "?") + "\" was refused: only its owner and co-owners build here" +
                                                                         (Built ? "" : " (the room is still building)"));
            return false;
        }

        /// <summary>A new object id: (PlayerId &amp; 0xFF) &lt;&lt; 24 | counter, above every id this room has with that prefix.</summary>
        private uint NextId()
        {
            uint prefix = (uint)(RrNet.LocalId & 0xFF) << 24;
            for (int guard = 0; guard < 1 << 20; guard++)
            {
                _counter = (_counter + 1) & 0xFFFFFF;
                if (_counter == 0)
                {
                    continue;
                }

                uint id = prefix | _counter;
                if (!_entries.ContainsKey(id) && !_dead.ContainsKey(id))
                {
                    return id;
                }
            }

            return prefix | 0xFFFFFF;
        }

        /// <summary>Starts this player's counter above every id the room already has with their prefix.</summary>
        private void SeedCounter()
        {
            uint prefix = (uint)(RrNet.LocalId & 0xFF) << 24;
            uint max = 0;
            foreach (var id in _entries.Keys)
            {
                if ((id & 0xFF000000u) == prefix)
                {
                    max = Math.Max(max, id & 0xFFFFFF);
                }
            }

            foreach (var id in _dead.Keys)
            {
                if ((id & 0xFF000000u) == prefix)
                {
                    max = Math.Max(max, id & 0xFFFFFF);
                }
            }

            _counter = max;
        }

        /// <summary>Keeps an object inside sane limits (a hostile or broken edit must not hang the room).</summary>
        private static RrMakerObject Sanitize(RrMakerObject o)
        {
            o.Asset = o.Asset ?? "";
            if (o.Asset.Length > 128)
            {
                o.Asset = o.Asset.Substring(0, 128);
            }

            o.Position = Clamp(o.Position, 2000f);
            if (o.Rotation.x == 0f && o.Rotation.y == 0f && o.Rotation.z == 0f && o.Rotation.w == 0f)
            {
                o.Rotation = Quaternion.identity;
            }

            o.Rotation = Quaternion.Normalize(o.Rotation);
            if (o.Kind == RrMakerKind.Light)
            {
                o.Scale = new Vector3(Mathf.Clamp(o.Scale.x, 0f, 60f), Mathf.Clamp(o.Scale.y, 0f, 20f), Mathf.Clamp(o.Scale.z, 0f, 179f));
            }
            else
            {
                o.Scale = new Vector3(ClampScale(o.Scale.x), ClampScale(o.Scale.y), ClampScale(o.Scale.z));
            }

            if (o.Points != null)
            {
                if (o.Points.Count > 2000)
                {
                    o.Points.RemoveRange(2000, o.Points.Count - 2000);
                }

                for (int i = 0; i < o.Points.Count; i++)
                {
                    o.Points[i] = Clamp(o.Points[i], 500f);
                }

                if (o.Points.Count == 0)
                {
                    o.Points = null;
                }
            }

            o.Width = Mathf.Clamp(o.Width, 0f, 2f);
            if (o.Text != null && o.Text.Length > 500)
            {
                o.Text = o.Text.Substring(0, 500);
            }

            return o;
        }

        private static Vector3 Clamp(Vector3 v, float limit)
        {
            return new Vector3(Mathf.Clamp(v.x, -limit, limit), Mathf.Clamp(v.y, -limit, limit), Mathf.Clamp(v.z, -limit, limit));
        }

        private static float ClampScale(float s)
        {
            if (float.IsNaN(s) || Mathf.Abs(s) < 0.001f)
            {
                return 0.001f;
            }

            return Mathf.Clamp(s, -200f, 200f);
        }

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            Current = this;
        }

        private void OnEnable()
        {
            RrNet.On(RrMakerEvents.Edit, OnEdit);
            RrNet.On(RrMakerEvents.AskState, OnAskState);
            RrNet.On(RrMakerEvents.Meta, OnMeta);
        }

        private void OnDisable()
        {
            RrNet.Off(RrMakerEvents.Edit, OnEdit);
            RrNet.Off(RrMakerEvents.AskState, OnAskState);
            RrNet.Off(RrMakerEvents.Meta, OnMeta);
        }

        private void OnDestroy()
        {
            // The world closing while the player is inside: Unity destroys this object in any order relative to RrMain, so
            // RrMakerRooms.Shutdown may find no Current room any more. A trip saved in OnLocalExit already (not dirty then).
            if (Built && _dirty && CanSaveRaw())
            {
                try
                {
                    RrLog.Info("MakerRooms", "\"" + _info.Name + "\" goes with unsaved changes: saving");
                    RrMakerRooms.SaveRoom(this, true);
                }
                catch (Exception e)
                {
                    RrLog.Warn("MakerRooms", "saving \"" + _info.Name + "\" on the way out failed: " + e.Message);
                }
            }

            ClearHost();
            DestroyOwned();
            if (Current == this)
            {
                Current = null;
            }
        }

        public override RrStatus ArrivalStatus
        {
            get { return RrStatus.Playing; }
        }

        public override Pose Spawn(int index)
        {
            Pose p = Template != null ? Template.DefaultSpawn : new Pose(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            if (_base != null && _base.Spawns.Count > 0)
            {
                p = _base.Spawns[Mathf.Abs(index) % _base.Spawns.Count];
                if (_base.Spawns.Count > 1)
                {
                    return p;
                }
            }

            // Side by side across the facing direction.
            float lateral = ((Mathf.Abs(index) % 4) - 1.5f) * 0.8f * (index == 0 ? 0f : 1f);
            return new Pose(p.position + p.rotation * new Vector3(lateral, 0f, 0f), p.rotation);
        }

        public override IEnumerator Build()
        {
            float started = Time.realtimeSinceStartup;
            BuildProgress = 0f;
            var pending = RrMakerRooms.TakePending();
            string id = pending != null ? pending.Id : null;
            RrMakerRoomData data = pending != null ? pending.Data : null;
            Fresh = pending != null && pending.Fresh;
            Source = data != null ? (Fresh ? "new room" : "stored copy") : "";

            // 1. Which room: picked on the watch, else what the instance says (a join without a pick).
            if (string.IsNullOrEmpty(id))
            {
                float until = Time.realtimeSinceStartup + (OthersHere() ? 4f : 0.5f);
                while (Time.realtimeSinceStartup < until && string.IsNullOrEmpty(id))
                {
                    id = RoomOfInstance();
                    if (string.IsNullOrEmpty(id))
                    {
                        yield return null;
                    }
                }

                if (!string.IsNullOrEmpty(id))
                {
                    RrLog.Info("MakerRooms", "joined instance #" + InstanceNumber + " without a pick: it plays room " + id);
                }
            }

            BuildProgress = 0.05f;

            // 2. The live state from whoever runs the instance, when someone else is already in it.
            RrMakerWireState state = null;
            if (OthersHere() && !RrNet.IsInstanceAuthority)
            {
                byte[] got = null;
                string error = null;
                int askedOf = RrNet.InstanceAuthorityId;
                bool reasked = false;
                var job = RrMakerTransfer.RequestState(id ?? "", b => got = b, e => error = e);
                float until = Mathf.Min(Time.realtimeSinceStartup + StateWait, started + BuildBudget - SpawnWait * 0.5f);
                while (got == null && error == null && Time.realtimeSinceStartup < until && this != null)
                {
                    BuildProgress = 0.05f + 0.25f * job.Progress;

                    // The authority left meanwhile: this client runs the instance now (no one to ask), or ask the new one once.
                    if (RrNet.IsInstanceAuthority)
                    {
                        error = "this client runs the instance now";
                        break;
                    }

                    if (!reasked && job.Count == 0 && RrNet.InstanceAuthorityId != askedOf)
                    {
                        reasked = true;
                        RrMakerTransfer.Cancel(job);
                        askedOf = RrNet.InstanceAuthorityId;
                        job = RrMakerTransfer.RequestState(id ?? "", b => got = b, e => error = e);
                    }

                    yield return null;
                }

                RrMakerTransfer.Cancel(job);
                if (got != null)
                {
                    state = RrMakerWireState.FromPacked(got);
                    // Without a pick the id came from the instance ints, which can still name an earlier room while the
                    // authority is building: its live state is the truth then.
                    if (state != null && (pending == null || string.IsNullOrEmpty(id) || state.Info.Id == id))
                    {
                        id = state.Info.Id;
                        Source = "live state from player " + RrNet.InstanceAuthorityId;
                    }
                    else
                    {
                        RrLog.Warn("MakerRooms", "the live state did not parse or names another room: using the stored copy");
                        state = null;
                    }
                }
                else
                {
                    RrLog.Warn("MakerRooms", "no live state from the instance authority (" + (error ?? "timed out after " + StateWait + " s") + "): using the stored copy");
                }
            }

            // 3. Else the stored copy; else (no pick, nothing named) the player's newest room or a new Maker Room.
            if (state == null)
            {
                if (data == null && !string.IsNullOrEmpty(id))
                {
                    data = RrMakerStore.Load(id);
                    if (data != null)
                    {
                        Source = "stored copy v" + data.Info.Version;
                    }
                }

                // A join without a pick into an empty instance whose state still names an earlier room this PC does not hold:
                // that room is gone from here, the player gets their own room instead of a failed trip.
                if (data == null && !string.IsNullOrEmpty(id) && pending == null && !OthersHere())
                {
                    RrLog.Warn("MakerRooms", "instance #" + InstanceNumber + " still names room " + id + ", which is not on this PC and nobody is here: opening your own room");
                    id = null;
                }

                if (data == null && string.IsNullOrEmpty(id))
                {
                    var mine = RrMakerRooms.MyLatestRoom();
                    data = mine != null ? RrMakerStore.Load(mine.Info.Id) : null;
                    if (data != null)
                    {
                        Source = "your newest room";
                    }
                    else
                    {
                        data = RrMakerRooms.NewRoom(RrMakerTemplates.MakerRoom);
                        Fresh = data != null;
                        Source = "a new Maker Room";
                    }
                }

                if (data == null)
                {
                    throw new InvalidOperationException("maker room " + (id ?? "?") + " is not on this PC and nobody in its instance could send it");
                }

                state = RrMakerWireState.FromData(data);
            }

            if (this == null)
            {
                yield break;
            }

            // 4. Settings, template, presence. A private room keeps out everyone but its owner and co-owners, however they
            //    came (the watch's Rooms page does not offer it; joining a friend through People does).
            _info = RrMakerCatalog.Copy(state.Info);
            if (!_info.Public && !CanSaveRaw())
            {
                RrLog.Warn("MakerRooms", "\"" + _info.Name + "\" is private: only its owner and co-owners can go in");
                _info = null;
                throw new InvalidOperationException("this maker room is private");
            }

            Template = RrMakerTemplates.Get(_info.Template) ?? RrMakerTemplates.Get(RrMakerTemplates.MakerRoom);
            RrMakerRooms.SetTravelTitle(_info.Name);

            RrNet.SetPlayerInt(0, RrMakerWire.RoomHash(_info.Id));

            // The instance names its room at once (not only once built): a member who finishes first must not take stale
            // ints from an earlier room for a conflict, and joiners without a pick find the room.
            WriteInstanceState();
            RrLog.Info("MakerRooms", "building \"" + _info.Name + "\" (" + _info.Id + ") v" + _info.Version + " on " + Template.Title + " from " + Source + ": " + state.Objects.Count + " object(s), owner " + _info.OwnerName +
                                     (CanSaveRaw() ? " (you may build and save)" : _info.EveryoneCanBuild ? " (everyone may build)" : " (visiting)"));
            BuildProgress = 0.3f;

            // 5. The template's fixed base.
            _objects = RrBuild.Group(transform, "Objects").transform;
            _base = new RrMakerBase { Root = RrBuild.Group(transform, "Base " + Template.Key).transform };
            if (Template.BuildBase != null)
            {
                IEnumerator routine = null;
                try
                {
                    routine = Template.BuildBase(this, _base);
                }
                catch (Exception e)
                {
                    RrLog.Error("MakerRooms", Template.Key + "'s base failed to start", e);
                }

                // Stepped here, nested routines included (RrJumbotronArea / RrSceneKit yield theirs): an exception anywhere
                // in the base only loses the base, and a base that runs past its share of the budget is left as far as it
                // got - the objects still come and the trip does not time out (travel's guard would drop the whole room).
                var stack = new Stack<IEnumerator>();
                if (routine != null)
                {
                    stack.Push(routine);
                }

                float baseUntil = started + BaseBudget;
                while (stack.Count > 0)
                {
                    if (Time.realtimeSinceStartup > baseUntil)
                    {
                        RrLog.Warn("MakerRooms", Template.Key + "'s base took longer than " + BaseBudget + " s: left as far as it got");
                        break;
                    }

                    var top = stack.Peek();
                    bool more;
                    object current = null;
                    try
                    {
                        more = top.MoveNext();
                        if (more)
                        {
                            current = top.Current;
                        }
                    }
                    catch (Exception e)
                    {
                        RrLog.Error("MakerRooms", Template.Key + "'s base failed", e);
                        break;
                    }

                    if (!more)
                    {
                        stack.Pop();
                        continue;
                    }

                    var nested = current as IEnumerator;
                    if (nested != null)
                    {
                        stack.Push(nested);
                        continue;
                    }

                    BuildProgress = Mathf.Min(0.55f, BuildProgress + 0.002f);
                    yield return current;
                    if (this == null)
                    {
                        yield break;
                    }
                }
            }

            BuildProgress = 0.55f;

            // A base that failed or was cut short named no spawn: the template's, put on whatever floor did get built.
            if (Template.BuildBase != null && _base.Spawns.Count == 0)
            {
                _base.Spawns.Add(RrMakerTemplates.Grounded(transform, Template.DefaultSpawn));
            }

            // 6. The objects.
            for (int i = 0; i < state.Objects.Count; i++)
            {
                var o = state.Objects[i];
                if (o == null || o.Id == 0 || _entries.ContainsKey(o.Id))
                {
                    continue;
                }

                var e = new Entry
                {
                    O = Sanitize(o),
                    Rev = state.Revs != null && i < state.Revs.Count ? state.Revs[i] : 1,
                    Author = state.Authors != null && i < state.Authors.Count ? state.Authors[i] : 0
                };
                _entries[o.Id] = e;
                BuildVisual(e);
                if (i % 40 == 39)
                {
                    yield return null;
                }
            }

            if (state.DeadIds != null)
            {
                for (int i = 0; i < state.DeadIds.Count; i++)
                {
                    _dead[state.DeadIds[i]] = new Tomb
                    {
                        Rev = state.DeadRevs != null && i < state.DeadRevs.Count ? state.DeadRevs[i] : 1,
                        Author = state.DeadAuthors != null && i < state.DeadAuthors.Count ? state.DeadAuthors[i] : 0
                    };
                }
            }

            int total = Mathf.Max(1, _pending);
            float spawnUntil = Mathf.Min(Time.realtimeSinceStartup + SpawnWait, started + BuildBudget);
            while (_pending > 0 && Time.realtimeSinceStartup < spawnUntil && this != null)
            {
                BuildProgress = Mathf.Lerp(0.6f, 0.95f, 1f - _pending / (float)total);
                yield return null;
            }

            if (_pending > 0)
            {
                RrLog.Warn("MakerRooms", _pending + " object(s) still loading after " + SpawnWait + " s; going in");
            }

            SeedCounter();
            if (Fresh)
            {
                SettleFresh();
            }

            Built = true;
            ApplyBuffered();
            WriteInstanceState();
            LightsChanged();
            BuildProgress = 1f;
            RrLog.Info("MakerRooms", "built \"" + _info.Name + "\" in " + (Time.realtimeSinceStartup - started).ToString("0.0") + " s: " + _entries.Count + " object(s)" +
                                     (_missingAssets.Count > 0 ? ", missing: " + string.Join(", ", _missingAssets) : ""));
            if (Fresh && CanSaveRaw())
            {
                RrMakerRooms.SaveRoom(this, true);
            }
        }

        private bool CanSaveRaw()
        {
            return _info != null && RrMakerRooms.IsOwnerOrCo(_info);
        }

        public override void OnLocalEnter()
        {
            if (_info == null)
            {
                return;
            }

            RrMakerEdits.Host = this;
            _hostSet = true;
            if (Template != null && !string.IsNullOrEmpty(Template.EnvKey))
            {
                RrEnvironment.SetArea(Template.EnvKey);
            }

            RrMakerRooms.NoteEntered(this);
            RrLog.Info("MakerRooms", "in \"" + _info.Name + "\" #" + InstanceNumber + ": " + (CanEdit ? "you can build here" : "visiting") + (CanSave ? " and save" : ""));
            if (Fresh && CanEdit)
            {
                // Rec Room since 2020: "The Maker Pen is now automatically equipped after creating a new room".
                try
                {
                    if (!Interact.MakerPen.RrMakerPen.IsOut)
                    {
                        Interact.MakerPen.RrMakerPen.Give();
                    }
                }
                catch (Exception e)
                {
                    RrLog.Warn("MakerRooms", "handing over the Maker Pen failed: " + e.Message);
                }
            }

            foreach (var id in new List<uint>(_entries.Keys))
            {
                RrMakerEdits.RaiseChanged(id);
            }
        }

        public override void OnLocalExit()
        {
            if (_dirty && CanSave)
            {
                RrLog.Info("MakerRooms", "leaving \"" + (_info != null ? _info.Name : "?") + "\" with unsaved changes: saving");
                RrMakerRooms.SaveRoom(this, true);
            }

            ClearHost();
            try
            {
                RrNet.SetPlayerInt(0, 0);
            }
            catch (Exception)
            {
                // the network is going
            }

            RrMakerRooms.NoteLeft(this);
        }

        private void ClearHost()
        {
            if (_hostSet && ReferenceEquals(RrMakerEdits.Host, this))
            {
                RrMakerEdits.Host = null;
            }

            _hostSet = false;
        }

        public override void OnAuthorityChanged(bool isAuthority)
        {
            if (isAuthority)
            {
                WriteInstanceState();
            }
        }

        public override void OnMembersChanged()
        {
            _savers = null;
        }

        /// <summary>Someone else is in this instance (a member other than the local player).</summary>
        private static bool OthersHere()
        {
            var members = RrNet.MembersOfMyInstance();
            foreach (int m in members)
            {
                if (m != RrNet.LocalId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The room the instance plays: its state's custom ints, else a member's presence int 19 matched in the catalogue.</summary>
        private string RoomOfInstance()
        {
            var s = RrNet.Instance;
            string id = RrMakerWire.IdFromInts(s.Get(IntId0), s.Get(IntId0 + 1), s.Get(IntId0 + 2), s.Get(IntId0 + 3));
            if (RrMakerWire.IsId(id))
            {
                return id;
            }

            foreach (int m in RrNet.MembersOfMyInstance())
            {
                if (m == RrNet.LocalId)
                {
                    continue;
                }

                int hash = RrNet.PlayerInt(m, 0);
                if (hash == 0)
                {
                    continue;
                }

                foreach (var r in RrMakerCatalog.All)
                {
                    if (RrMakerWire.RoomHash(r.Info.Id) == hash)
                    {
                        return r.Info.Id;
                    }
                }
            }

            return null;
        }

        /// <summary>A brand-new room: put the template's settle-listed pieces on their surfaces / walls, then they are saved.</summary>
        private void SettleFresh()
        {
            if (Template == null || Template.Settle.Count == 0)
            {
                return;
            }

            Physics.SyncTransforms();
            int moved = 0;
            foreach (var kv in Template.Settle)
            {
                Entry e;
                if (!_entries.TryGetValue(kv.Key, out e) || e.Go == null)
                {
                    continue;
                }

                var b = VisualBounds(e.Go);
                if (b.size == Vector3.zero)
                {
                    continue;
                }

                var s = kv.Value;
                Vector3 delta = Vector3.zero;
                if (s.Mount)
                {
                    Vector3 p = transform.TransformPoint(s.WallPoint);
                    Vector3 n = transform.TransformDirection(s.Normal).normalized;
                    delta = p - b.center;
                    delta -= n * Vector3.Dot(delta, n);
                    float extent = Mathf.Abs(n.x) * b.extents.x + Mathf.Abs(n.y) * b.extents.y + Mathf.Abs(n.z) * b.extents.z;
                    float back = Vector3.Dot(b.center - p, n) - extent;
                    delta += n * (s.Standoff - back);
                }
                else if (!float.IsNaN(s.GroundY))
                {
                    float target = transform.TransformPoint(new Vector3(0f, s.GroundY, 0f)).y;
                    delta = Vector3.up * (target - b.min.y);
                }

                if (delta.sqrMagnitude < 1e-8f)
                {
                    continue;
                }

                e.O.Position += transform.InverseTransformVector(delta);
                ApplyTransform(e);
                moved++;
            }

            if (moved > 0)
            {
                MarkDirty();
                RrLog.Info("MakerRooms", "settled " + moved + " base piece(s) of the new " + Template.Title + " on their surfaces and walls");
            }
        }

        /// <summary>World bounds of an object's renderers (zero size when it has none yet).</summary>
        private static Bounds VisualBounds(GameObject go)
        {
            var b = new Bounds();
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return any ? b : new Bounds(go.transform.position, Vector3.zero);
        }

        /// <summary>The room as it is now, for a save (objects sorted by id).</summary>
        public RrMakerRoomData Snapshot()
        {
            var data = new RrMakerRoomData { Info = RrMakerCatalog.Copy(_info) };
            var ids = new List<uint>(_entries.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                data.Objects.Add(_entries[id].O.Clone());
            }

            return data;
        }

        /// <summary>The save went through (RrMakerRooms.SaveRoom): the room's version, the instance and the room's members hear of it.</summary>
        internal void Saved(int version, long savedAt)
        {
            _info.Version = version;
            _info.SavedAt = savedAt;
            _dirty = false;
            _lastSaveAt = Time.realtimeSinceStartup;
            WriteInstanceState();
            SendSaved(version, savedAt);
        }
    }

    /// <summary>A room's live state as it travels to a late joiner (event 26 kind 2): settings, objects with revisions, deletions.</summary>
    [Serializable]
    public sealed class RrMakerWireState
    {
        public RrMakerRoomInfo Info = new RrMakerRoomInfo();
        public List<RrMakerObject> Objects = new List<RrMakerObject>();
        public List<uint> Revs = new List<uint>();
        public List<int> Authors = new List<int>();
        public List<uint> DeadIds = new List<uint>();
        public List<uint> DeadRevs = new List<uint>();
        public List<int> DeadAuthors = new List<int>();

        public static RrMakerWireState FromData(RrMakerRoomData data)
        {
            var s = new RrMakerWireState { Info = data.Info };
            foreach (var o in data.Objects)
            {
                s.Objects.Add(o);
                s.Revs.Add(1);
                s.Authors.Add(0);
            }

            return s;
        }

        public byte[] Pack()
        {
            return RrMakerWire.Pack(RrMakerWire.Utf8(JsonUtility.ToJson(this)));
        }

        public static RrMakerWireState FromPacked(byte[] packed)
        {
            var raw = RrMakerWire.Unpack(packed);
            if (raw == null)
            {
                return null;
            }

            try
            {
                var s = JsonUtility.FromJson<RrMakerWireState>(RrMakerWire.FromUtf8(raw));
                if (s == null)
                {
                    return null;
                }

                // Objects, revisions and authors are parallel lists: drop broken objects here, together with their
                // revision and author (RrMakerStore.Check would remove them from the objects alone and shift the rest).
                var objects = new List<RrMakerObject>();
                var revs = new List<uint>();
                var authors = new List<int>();
                for (int i = 0; s.Objects != null && i < s.Objects.Count && objects.Count < RrMakerStore.MaxObjects; i++)
                {
                    var o = s.Objects[i];
                    if (o == null || o.Id == 0)
                    {
                        continue;
                    }

                    objects.Add(o);
                    revs.Add(s.Revs != null && i < s.Revs.Count ? s.Revs[i] : 1u);
                    authors.Add(s.Authors != null && i < s.Authors.Count ? s.Authors[i] : 0);
                }

                var data = new RrMakerRoomData { Info = s.Info, Objects = objects };
                if (!RrMakerStore.Check(data, null) || data.Objects.Count != revs.Count)
                {
                    return null;
                }

                s.Info = data.Info;
                s.Objects = data.Objects;
                s.Revs = revs;
                s.Authors = authors;
                return s;
            }
            catch (Exception e)
            {
                RrLog.Warn("MakerRooms", "a live state did not parse: " + e.Message);
                return null;
            }
        }
    }
}
