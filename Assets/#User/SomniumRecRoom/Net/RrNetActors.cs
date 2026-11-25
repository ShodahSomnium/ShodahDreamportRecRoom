using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// A block of 15 AI actors (quest enemies...) or a 1,920-bit bitset (loot taken, breakables destroyed), shared
    /// through a "Rr Net Actors a" leased by the instance authority (somnium_networking.md §5.8; ints in
    /// <see cref="RrActorWire"/>). Games ask for a block by their own number (<see cref="Get"/>); the authority writes
    /// (<see cref="Put"/>, <see cref="SetBit"/>) and the block goes out at 10 Hz, only changed ints, with a heartbeat
    /// each second; watchers read <see cref="Sample"/> (interpolated on <see cref="RrTimeline"/>) or <see cref="Actor"/>.
    /// When the authority moves, the new one adopts every actor from its last written state (<see cref="OnAdopted"/>).
    /// Positions are world positions here and millimetres relative to the room origin on the wire (every room is
    /// built at the world origin, RrPlacement.RoomOrigin, by every member). Offline, or before the lease, the block
    /// is local and the local player writes it.
    /// </summary>
    public sealed class RrNetActors
    {
        public const double WriteEvery = 0.1;
        public const double HeartbeatEvery = 1.0;
        public const int Actors = RrActorWire.ActorsPerBlock;
        public const int RawInts = RrActorWire.Ints - RrActorWire.AtActors;
        public const int Bits = RawInts * 32;

        private static readonly Dictionary<int, RrNetActors> Blocks = new Dictionary<int, RrNetActors>();

        public readonly int LocalBlock;
        private readonly int[] _rec = new int[RrActorWire.Ints];
        private readonly RrTimeline[] _timelines = new RrTimeline[Actors];
        private readonly Vector3[] _lastPos = new Vector3[Actors];
        private readonly int[] _lastStamp = new int[Actors];
        private readonly int[] _serial = new int[Actors];
        private int _lastSeq = int.MinValue;
        private double _writeAt, _beatAt;
        private bool _dirty, _wasWriter;

        private RrNetActors(int localBlock)
        {
            LocalBlock = localBlock;

            for (int i = 0; i < Actors; i++)
            {
                _timelines[i] = new RrTimeline { LeadIn = true };
            }
        }

        /// <summary>The block with this game-local number (0..255), created and registered on first use.</summary>
        public static RrNetActors Get(int localBlock)
        {
            localBlock &= 0xFF;
            RrNetActors block;

            if (!Blocks.TryGetValue(localBlock, out block))
            {
                block = new RrNetActors(localBlock);
                Blocks.Add(localBlock, block);
            }

            return block;
        }

        /// <summary>The game no longer needs this block (the lease lapses with the instance).</summary>
        public static void Release(int localBlock)
        {
            Blocks.Remove(localBlock & 0xFF);
        }

        /// <summary>This client adopted the block from the previous authority; the actors' last states are in it.</summary>
        public event Action OnAdopted;

        /// <summary>The actor object leased for this block (-1: local).</summary>
        public int BlockObject
        {
            get { return RrNet.Online ? RrNet.Core.BlockOf[LocalBlock] : -1; }
        }

        public bool Networked
        {
            get { return BlockObject >= 0; }
        }

        /// <summary>This client writes the block (the instance authority, once it holds the object; always offline).</summary>
        public bool IsWriter
        {
            get
            {
                int a = BlockObject;
                return a < 0 ? RrNet.IsInstanceAuthority : RrNet.IsInstanceAuthority && RrNet.Core.BlockAuth[a];
            }
        }

        /// <summary>The frame of positions on the wire: the room origin, the same for every room and every member.</summary>
        private static Vector3 Origin
        {
            get { return RrPlacement.RoomOrigin; }
        }

        // ------------------------------------------------------------------ actors

        /// <summary>Writer: actor i's state (world position; <see cref="RrActorState.Flags"/> should carry FlagUsed).</summary>
        public void Put(int actor, RrActorState state)
        {
            if (actor < 0 || actor >= Actors || !Writable())
            {
                return;
            }

            state.Position -= Origin;
            RrActorWire.PutActor(_rec, actor, state);
            _dirty = true;
        }

        /// <summary>Actor i's last written state (world position).</summary>
        public RrActorState Actor(int actor)
        {
            if (actor < 0 || actor >= Actors)
            {
                return default;
            }

            RrActorState s = RrActorWire.GetActor(_rec, actor);
            s.Position += Origin;
            return s;
        }

        /// <summary>Actor i as a watcher should draw it now (interpolated; the writer gets its own state). False when unused.</summary>
        public bool Sample(int actor, out RrActorState state)
        {
            state = Actor(actor);

            if (actor < 0 || actor >= Actors || !state.Used)
            {
                return false;
            }

            RrNetState s;

            if (!IsWriter && _timelines[actor].Sample(RrNet.Now, out s) && RrWire.Finite(s.Position))
            {
                state.Position = s.Position + Origin;
                state.Yaw = RrWire.YawOf(s.Rotation);
            }

            return true;
        }

        // ------------------------------------------------------------------ bitset / raw

        public bool GetBit(int bit)
        {
            return bit >= 0 && bit < Bits && RrWire.Bit(_rec[RrActorWire.AtActors + (bit >> 5)], bit & 31);
        }

        public void SetBit(int bit, bool on)
        {
            if (bit < 0 || bit >= Bits || !Writable())
            {
                return;
            }

            int at = RrActorWire.AtActors + (bit >> 5);
            int v = RrWire.WithBit(_rec[at], bit & 31, on);

            if (v != _rec[at])
            {
                _rec[at] = v;
                _dirty = true;
            }
        }

        /// <summary>Raw int i of the 60 data ints (4..63).</summary>
        public int Raw(int i)
        {
            return i >= 0 && i < RawInts ? _rec[RrActorWire.AtActors + i] : 0;
        }

        public void SetRaw(int i, int value)
        {
            if (i >= 0 && i < RawInts && Writable() && _rec[RrActorWire.AtActors + i] != value)
            {
                _rec[RrActorWire.AtActors + i] = value;
                _dirty = true;
            }
        }

        /// <summary>Writer: clears every actor and bit.</summary>
        public void Clear()
        {
            if (Writable())
            {
                Array.Clear(_rec, RrActorWire.AtActors, RawInts);
                _dirty = true;
            }
        }

        private bool Writable()
        {
            if (IsWriter)
            {
                return true;
            }

            RrLog.WarnOnce("Net", "actors-write-" + LocalBlock, "actor block " + LocalBlock + " written by a client that is not its writer: ignored");
            return false;
        }

        // ------------------------------------------------------------------ tick

        private void Tick(double now)
        {
            int a = BlockObject;

            if (a < 0)
            {
                _wasWriter = false;
                return;
            }

            int[] net = RrNet.Core.Block[a];
            bool writer = IsWriter;

            if (writer)
            {
                if (!_wasWriter)
                {
                    _wasWriter = true;

                    if (RrActorWire.LeaseMatches(net, a >= 0 ? RrNet.Core.Bound : -1, RrNet.Core.BoundGeneration, LocalBlock) && net[RrActorWire.AtSeq] > 1)
                    {
                        Array.Copy(net, RrActorWire.AtActors, _rec, RrActorWire.AtActors, RawInts);
                        RrNet.Info("actor block " + LocalBlock + " (object " + a + ") adopted from player " + RrActorWire.Writer(net));

                        try
                        {
                            OnAdopted?.Invoke();
                        }
                        catch (Exception e)
                        {
                            RrLog.Error("Net", "actor block adoption handler failed", e);
                        }
                    }

                    _dirty = true;
                }

                if ((_dirty && now >= _writeAt) || now >= _beatAt)
                {
                    Array.Copy(net, _rec, RrActorWire.AtActors);
                    _rec[RrActorWire.AtSeq] = net[RrActorWire.AtSeq] + 1;
                    _rec[RrActorWire.AtStamp] = RrNetCore.Stamp(Time.timeAsDouble);
                    _rec[RrActorWire.AtLease] = RrActorWire.PackLease(RrNet.LocalId, RrActorWire.LeaseObject(net), RrActorWire.LeaseGeneration(net));

                    if (RrNet.PushNow(RrNetCore.KindBlock, a, _rec))
                    {
                        _dirty = false;
                        _writeAt = now + WriteEvery;
                        _beatAt = now + HeartbeatEvery;
                    }
                }

                return;
            }

            _wasWriter = false;

            if (net[RrActorWire.AtSeq] == _lastSeq)
            {
                return;
            }

            _lastSeq = net[RrActorWire.AtSeq];
            Array.Copy(net, _rec, _rec.Length);
            int stamp = net[RrActorWire.AtStamp];

            for (int i = 0; i < Actors; i++)
            {
                RrActorState s = RrActorWire.GetActor(net, i);

                if (!s.Used)
                {
                    continue;
                }

                if (s.Serial != _serial[i])
                {
                    _serial[i] = s.Serial;
                    _timelines[i].Clear();
                    _lastStamp[i] = 0;
                }

                float dt = _lastStamp[i] != 0 ? (stamp - _lastStamp[i]) / 1000f : 0f;
                Vector3 velocity = dt > 0.001f && dt < 2f ? (s.Position - _lastPos[i]) / dt : Vector3.zero;
                _lastPos[i] = s.Position;
                _lastStamp[i] = stamp;
                _timelines[i].Add(new RrNetState
                {
                    Seq = _lastSeq,
                    StampMs = stamp,
                    Epoch = s.Serial,
                    Position = s.Position,
                    Rotation = RrWire.FromYaw(s.Yaw),
                    Velocity = velocity
                }, now);
            }
        }

        internal static void TickAll(double now)
        {
            foreach (KeyValuePair<int, RrNetActors> pair in Blocks)
            {
                pair.Value.Tick(now);
            }
        }

        internal static void FillNeeds(List<int> needs)
        {
            needs.Clear();
            needs.AddRange(Blocks.Keys);
        }

        internal static void ClearAll()
        {
            Blocks.Clear();
        }
    }
}
