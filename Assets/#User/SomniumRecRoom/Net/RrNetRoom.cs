using System;
using System.Collections.Generic;
using Fusion;
using SomniumSpace.Network.Bridge;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// The Fusion side of <see cref="RrNet"/>: the scene objects found by name once a second until all are there
    /// (networking §4.1), their ints read into the core's mirrors (all of them at 10 Hz, the bound instance and the
    /// leased props/blocks every frame), the core stepped at 10 Hz, its requests and writes carried out (§4.2: only
    /// changed ints, only with state authority). Only server-proven members are used (§10).
    /// </summary>
    public static partial class RrNet
    {
        public const double ReadEvery = 0.1;
        public const double PresentEvery = 1.0;
        public const double FindEvery = 1.0;

        private static NetworkBridgeEvents _events;
        private static readonly NetworkBridgeData[] SlotObjects = new NetworkBridgeData[RrPresenceWire.Slots];
        private static readonly NetworkBridgeData[] InstanceObjects = new NetworkBridgeData[RrInstanceWire.Objects];
        private static readonly NetworkBridgeData[] BlockObjects = new NetworkBridgeData[RrActorWire.Objects];
        private static readonly NetworkBridgeData[] PropObjects = new NetworkBridgeData[RrPropWire.Objects];
        private static readonly Action<byte[]>[] ControllerHandlers = new Action<byte[]>[RrInstanceWire.Objects];
        private static bool _slotsFound, _allFound, _hooked, _wasAuthority = true, _wasOnline;
        private static double _findAt, _readAt, _presentAt, _stepAt, _flagsAt;
        private static int _wroteInts, _membersHash;
        private static readonly List<int> PresentScratch = new List<int>();

        // ------------------------------------------------------------------ the per-frame driver

        internal static void Tick()
        {
            double now = Now;
            bool ready = SceneNetworking.IsNetworkReady && SceneNetworking.NetworkRunnerRef != null;

            if (ready && !_allFound && now >= _findAt)
            {
                _findAt = now + FindEvery;
                Find();
            }

            bool online = ready && _slotsFound;

            if (online != _wasOnline)
            {
                _wasOnline = online;
                _stepSoon = true;
                _membersDirty = true;
                _presentAt = 0.0;
                _readAt = 0.0;
                RrLog.Info("Net", online ? "online as player " + LocalId + " (" + FoundSummary() + ")" : "offline");
            }

            _core.LocalId = LocalId;
            _core.Up = online;

            if (now >= _flagsAt)
            {
                _flagsAt = now + 1.0;
                RefreshOwnFlags();
            }

            if (online)
            {
                if (now >= _presentAt)
                {
                    _presentAt = now + PresentEvery;
                    ReadPresent();
                }

                if (now >= _readAt)
                {
                    _readAt = now + ReadEvery;
                    ReadAll();
                    _stepSoon = true;
                }
                else
                {
                    ReadHot();
                }
            }

            if (_stepSoon || now >= _stepAt)
            {
                _stepSoon = false;
                _stepAt = now + ReadEvery;
                RrNetBody.FillNeeds(_core.NeedProps);
                RrNetActors.FillNeeds(_core.NeedBlocks);
                _core.Step(now);
                Carry();
                CheckChanges();
            }

            CheckJoin();
            RrNetActors.TickAll(now);
            TickEvents(now);
        }

        private static void Carry()
        {
            for (int i = 0; i < _core.Notes.Count; i++)
            {
                Info(_core.Notes[i]);
            }

            _core.Notes.Clear();

            if (!_core.Up)
            {
                return;
            }

            for (int i = 0; i < _core.Asks.Count; i++)
            {
                RrNetCore.Ask ask = _core.Asks[i];
                Request(ObjectOf(ask.Kind, ask.Index));
            }

            for (int i = 0; i < _core.Writes.Count; i++)
            {
                RrNetCore.Write w = _core.Writes[i];
                NetworkBridgeData data = ObjectOf(w.Kind, w.Index);

                if (Push(data, w.Record))
                {
                    ReadInto(data, MirrorOf(w.Kind, w.Index));
                }
            }

            for (int i = 0; i < _core.LeasedProps.Count; i++)
            {
                RrNetBody.OnLeased(_core.LeasedProps[i]);
            }
        }

        private static void CheckChanges()
        {
            bool authority = IsInstanceAuthority;

            if (authority != _wasAuthority)
            {
                _wasAuthority = authority;
                Raise(OnAuthorityChanged, authority);
            }

            int hash = 17;

            if (Online)
            {
                for (int i = 0; i < _core.Present.Count; i++)
                {
                    hash = hash * 31 + _core.Present[i];
                }

                for (int s = 0; s < RrPresenceWire.Slots; s++)
                {
                    int[] r = _core.Slot[s];

                    if (RrPresenceWire.Listed(r, _core.Present))
                    {
                        hash = hash * 31 + r[RrPresenceWire.AtOwner];
                        hash = hash * 31 + r[RrPresenceWire.AtWhere];
                    }
                }

                for (int i = 0; i < _core.Members.Count; i++)
                {
                    hash = hash * 31 + _core.Members[i];
                }
            }

            if (hash != _membersHash || _membersDirty)
            {
                _membersHash = hash;
                _membersDirty = false;
                Raise(OnPlayersChanged);
            }
        }

        private static void Raise(Action handlers)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action h in handlers.GetInvocationList())
            {
                try
                {
                    h();
                }
                catch (Exception e)
                {
                    RrLog.Error("Net", "OnPlayersChanged handler failed", e);
                }
            }
        }

        private static void Raise(Action<bool> handlers, bool value)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action<bool> h in handlers.GetInvocationList())
            {
                try
                {
                    h(value);
                }
                catch (Exception e)
                {
                    RrLog.Error("Net", "OnAuthorityChanged handler failed", e);
                }
            }
        }

        private static void RefreshOwnFlags()
        {
            int flags = _core.Own[RrPresenceWire.AtOwner];

            try
            {
                flags = RrWire.WithBit(flags, RrPresenceWire.FlagVr, RrPlayer.InVr);
            }
            catch (Exception)
            {
                // the player module is not up
            }

            _core.Own[RrPresenceWire.AtOwner] = flags;

            if (_core.Own[RrPresenceWire.AtUser] == 0)
            {
                try
                {
                    var player = SomniumSpace.Bridge.SomniumBridge.PlayersContainer?.LocalPlayer;

                    if (player != null && player.Properties != null)
                    {
                        _core.Own[RrPresenceWire.AtUser] = RrWire.Fnv1a(Convert.ToString(player.Properties.Id));
                    }
                }
                catch (Exception)
                {
                    // no client (editor)
                }
            }
        }

        // ------------------------------------------------------------------ reading

        private static void ReadPresent()
        {
            PresentScratch.Clear();

            try
            {
                var runner = SceneNetworking.NetworkRunnerRef;

                if (runner != null)
                {
                    foreach (PlayerRef player in runner.ActivePlayers)
                    {
                        if (!player.IsNone)
                        {
                            PresentScratch.Add(player.PlayerId);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Net", "active-players", "reading the room's players failed: " + e.Message);
            }

            int local = LocalId;

            if (!PresentScratch.Contains(local))
            {
                PresentScratch.Add(local);
            }

            PresentScratch.Sort();
            bool same = PresentScratch.Count == _core.Present.Count;

            for (int i = 0; same && i < PresentScratch.Count; i++)
            {
                same = PresentScratch[i] == _core.Present[i];
            }

            if (!same)
            {
                _core.Present.Clear();
                _core.Present.AddRange(PresentScratch);
                _stepSoon = true;
            }
        }

        private static void ReadAll()
        {
            for (int s = 0; s < SlotObjects.Length; s++)
            {
                ReadObject(SlotObjects[s], _core.Slot[s], _core.SlotValid, _core.SlotAuth, null, s);
            }

            for (int k = 0; k < InstanceObjects.Length; k++)
            {
                ReadObject(InstanceObjects[k], _core.Inst[k], _core.InstValid, _core.InstAuth, null, k);
            }

            for (int p = 0; p < PropObjects.Length; p++)
            {
                ReadObject(PropObjects[p], _core.Prop[p], _core.PropValid, _core.PropAuth, _core.PropHolder, p);
            }

            for (int a = 0; a < BlockObjects.Length; a++)
            {
                ReadObject(BlockObjects[a], _core.Block[a], _core.BlockValid, _core.BlockAuth, null, a);
            }
        }

        /// <summary>Every frame: the bound instance object and the props/blocks the current instance uses.</summary>
        private static void ReadHot()
        {
            int k = _core.Bound;

            if (k >= 0)
            {
                ReadObject(InstanceObjects[k], _core.Inst[k], _core.InstValid, _core.InstAuth, null, k);
            }

            for (int i = 0; i < _core.NeedProps.Count; i++)
            {
                int p = _core.PropOf[_core.NeedProps[i] & 0xFF];

                if (p >= 0)
                {
                    ReadObject(PropObjects[p], _core.Prop[p], _core.PropValid, _core.PropAuth, _core.PropHolder, p);
                }
            }

            for (int i = 0; i < _core.NeedBlocks.Count; i++)
            {
                int a = _core.BlockOf[_core.NeedBlocks[i] & 0xFF];

                if (a >= 0)
                {
                    ReadObject(BlockObjects[a], _core.Block[a], _core.BlockValid, _core.BlockAuth, null, a);
                }
            }
        }

        private static void ReadObject(NetworkBridgeData data, int[] mirror, bool[] valid, bool[] authority, int[] holder, int index)
        {
            bool ok = Valid(data);
            valid[index] = ok;
            authority[index] = ok && data.Object.HasStateAuthority;

            if (holder != null)
            {
                holder[index] = -1;
            }

            if (!ok)
            {
                return;
            }

            ReadInto(data, mirror);

            if (holder != null)
            {
                PlayerRef a = data.Object.StateAuthority;
                holder[index] = a.IsNone ? -1 : a.PlayerId;
            }
        }

        private static void ReadInto(NetworkBridgeData data, int[] mirror)
        {
            if (!Valid(data) || mirror == null)
            {
                return;
            }

            NetworkArray<int> ints = data.IntArray;
            int n = Math.Min(ints.Length, mirror.Length);

            for (int i = 0; i < n; i++)
            {
                mirror[i] = ints.Get(i);
            }
        }

        // ------------------------------------------------------------------ writing

        internal static bool Valid(NetworkBehaviour behaviour)
        {
            return behaviour != null && behaviour.Object != null && behaviour.Object.IsValid;
        }

        internal static bool HasAuthority(NetworkBridgeData data)
        {
            return Valid(data) && data.Object.HasStateAuthority;
        }

        internal static void Request(NetworkBridgeData data)
        {
            if (Valid(data) && !data.Object.HasStateAuthority)
            {
                data.Object.RequestStateAuthority();
            }
        }

        /// <summary>Writes the ints that differ, only with state authority (ETS Push); false without authority.</summary>
        internal static bool Push(NetworkBridgeData data, int[] record)
        {
            if (!HasAuthority(data) || record == null)
            {
                return false;
            }

            NetworkArray<int> ints = data.IntArray;
            int n = Math.Min(ints.Length, record.Length);

            for (int i = 0; i < n; i++)
            {
                if (ints.Get(i) != record[i])
                {
                    ints.Set(i, record[i]);
                    _wroteInts++;
                }
            }

            return true;
        }

        private static NetworkBridgeData ObjectOf(int kind, int index)
        {
            NetworkBridgeData[] table = kind == RrNetCore.KindSlot ? SlotObjects
                : kind == RrNetCore.KindInstance ? InstanceObjects
                : kind == RrNetCore.KindProp ? PropObjects
                : BlockObjects;
            return index >= 0 && index < table.Length ? table[index] : null;
        }

        private static int[] MirrorOf(int kind, int index)
        {
            int[][] table = kind == RrNetCore.KindSlot ? _core.Slot
                : kind == RrNetCore.KindInstance ? _core.Inst
                : kind == RrNetCore.KindProp ? _core.Prop
                : _core.Block;
            return index >= 0 && index < table.Length ? table[index] : null;
        }

        // For RrNetBody / RrNetActors.
        internal static NetworkBridgeData PropObject(int p)
        {
            return p >= 0 && p < PropObjects.Length ? PropObjects[p] : null;
        }

        internal static NetworkBridgeData BlockObject(int a)
        {
            return a >= 0 && a < BlockObjects.Length ? BlockObjects[a] : null;
        }

        /// <summary>Writes a prop/actor record at once (changed ints, with authority) and refreshes the mirror.</summary>
        internal static bool PushNow(int kind, int index, int[] record)
        {
            NetworkBridgeData data = ObjectOf(kind, index);

            if (!Push(data, record))
            {
                return false;
            }

            ReadInto(data, MirrorOf(kind, index));
            return true;
        }

        internal static void RequestNow(int kind, int index)
        {
            Request(ObjectOf(kind, index));
        }

        // ------------------------------------------------------------------ finding the scene objects

        private static void Find()
        {
            int slots = 0, instances = 0, blocks = 0, props = 0;

            foreach (NetworkBridgeData data in UnityEngine.Object.FindObjectsByType<NetworkBridgeData>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string name = data.name;
                Place(data, name, RrPresenceWire.ObjectPrefix, RrPresenceWire.Ints, SlotObjects);
                Place(data, name, RrInstanceWire.ObjectPrefix, RrInstanceWire.Ints, InstanceObjects);
                Place(data, name, RrActorWire.ObjectPrefix, RrActorWire.Ints, BlockObjects);
                Place(data, name, RrPropWire.ObjectPrefix, RrPropWire.Ints, PropObjects);
            }

            if (_events == null)
            {
                foreach (NetworkBridgeEvents events in UnityEngine.Object.FindObjectsByType<NetworkBridgeEvents>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (events.name == EventsObjectName && !(events is NetworkBridgeData))
                    {
                        _events = events;
                    }
                }
            }

            slots = Count(SlotObjects);
            instances = Count(InstanceObjects);
            blocks = Count(BlockObjects);
            props = Count(PropObjects);
            _slotsFound = slots == SlotObjects.Length && instances == InstanceObjects.Length;
            _allFound = _slotsFound && blocks == BlockObjects.Length && props == PropObjects.Length && _events != null;
            Hook();

            if (_allFound)
            {
                Info("all scene network objects found (" + FoundSummary() + ")");
            }
            else
            {
                RrLog.WarnOnce("Net", "objects-missing", "scene network objects missing (" + FoundSummary() + "); the scene needs the editor's network setup - " +
                                                          (_slotsFound ? "props/actors/events stay local" : "playing offline"));
            }
        }

        private static string FoundSummary()
        {
            return "slots " + Count(SlotObjects) + "/" + SlotObjects.Length + ", instances " + Count(InstanceObjects) + "/" + InstanceObjects.Length +
                   ", actor blocks " + Count(BlockObjects) + "/" + BlockObjects.Length + ", props " + Count(PropObjects) + "/" + PropObjects.Length +
                   ", events " + (_events != null ? "yes" : "no");
        }

        private static void Place(NetworkBridgeData data, string name, string prefix, int ints, NetworkBridgeData[] table)
        {
            if (name == null || !name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return;
            }

            int k;

            if (!int.TryParse(name.Substring(prefix.Length), out k) || k < 0 || k >= table.Length)
            {
                return;
            }

            if (data.IntCount < ints)
            {
                RrLog.WarnOnce("Net", "size-" + name, "'" + name + "' holds " + data.IntCount + " ints, needs " + ints + ": ignored (wrong NetworkBridgeData size in the scene)");
                return;
            }

            table[k] = data;
        }

        private static int Count(NetworkBridgeData[] table)
        {
            int n = 0;

            for (int i = 0; i < table.Length; i++)
            {
                n += table[i] != null ? 1 : 0;
            }

            return n;
        }

        private static void Hook()
        {
            if (_events != null && !_hooked)
            {
                _hooked = true;
                _events.OnMessageToAll += OnFrameAll;
                _events.OnMessageToPlayer += OnFramePlayer;
            }

            for (int k = 0; k < InstanceObjects.Length; k++)
            {
                if (InstanceObjects[k] != null && ControllerHandlers[k] == null)
                {
                    int index = k;
                    ControllerHandlers[k] = bytes => OnFrameController(index, bytes);
                    InstanceObjects[k].OnMessageToController += ControllerHandlers[k];
                }
            }
        }

        private static void Unhook()
        {
            try
            {
                if (_events != null && _hooked)
                {
                    _events.OnMessageToAll -= OnFrameAll;
                    _events.OnMessageToPlayer -= OnFramePlayer;
                }

                for (int k = 0; k < InstanceObjects.Length; k++)
                {
                    if (InstanceObjects[k] != null && ControllerHandlers[k] != null)
                    {
                        InstanceObjects[k].OnMessageToController -= ControllerHandlers[k];
                    }
                }
            }
            catch (Exception)
            {
                // objects already destroyed with the scene
            }

            for (int k = 0; k < ControllerHandlers.Length; k++)
            {
                ControllerHandlers[k] = null;
            }

            _hooked = false;
            _events = null;
            Array.Clear(SlotObjects, 0, SlotObjects.Length);
            Array.Clear(InstanceObjects, 0, InstanceObjects.Length);
            Array.Clear(BlockObjects, 0, BlockObjects.Length);
            Array.Clear(PropObjects, 0, PropObjects.Length);
            _slotsFound = false;
            _allFound = false;
            _findAt = 0.0;
        }

        private static void OnRoomPlayer(PlayerRef player)
        {
            _presentAt = 0.0;
            _readAt = 0.0;
        }

        private static void OnLocalJoined()
        {
            _findAt = 0.0;
            _presentAt = 0.0;
            _readAt = 0.0;
        }
    }
}
