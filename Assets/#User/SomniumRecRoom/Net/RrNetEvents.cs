using System;
using System.Collections.Generic;
using Fusion;
using SomniumSpace.Network.Bridge;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// Events (somnium_networking.md §5.7): small messages batched into one RPC frame per 50 ms (<see cref="RrEventWire"/>),
    /// scoped to the sender's instance - every event carries the instance object k+1 and its generation, and
    /// receivers in other instances drop it. Broadcasts go through "Rr Net Events".RPC_SendMessageToAll; requests to
    /// the instance authority through the bound instance object's RPC_SendMessageToController (it reaches whoever holds
    /// that object, the authority; during a hand-over it may be lost, so senders repeat requests until their effect
    /// shows in the ints). The sender's own events are delivered to it locally (loop-back) and ignored when the RPC
    /// comes back. Offline everything loops back.
    /// </summary>
    public static partial class RrNet
    {
        public const string EventsObjectName = "Rr Net Events";

        /// <summary>Frames are flushed this often, s (≈ 20 RPCs/s per busy client at most).</summary>
        public const double FlushEvery = 0.05;

        private struct Pending
        {
            public byte Type;
            public int Sender;
            public byte[] Body;
        }

        private static readonly Dictionary<byte, Action<int, byte[]>> Handlers = new Dictionary<byte, Action<int, byte[]>>();
        private static readonly RrEventWire.Batch BatchAll = new RrEventWire.Batch();
        private static readonly RrEventWire.Batch BatchAuthority = new RrEventWire.Batch();
        private static readonly List<Pending> LoopBack = new List<Pending>();
        private static readonly List<Pending> Delivering = new List<Pending>();
        private static int _batchAuthorityObject = -1;
        private static double _flushAt;
        private static int _framesSent, _framesReceived, _eventsDropped;

        /// <summary>Sends an event (batched RPC) to the members of my instance, or to its authority.</summary>
        public static void Send(byte type, byte[] body, bool toAuthority = false)
        {
            body = body ?? new byte[0];

            if (body.Length > RrEventWire.MaxPayload)
            {
                RrLog.Warn("Net", "event " + type + " dropped: " + body.Length + " bytes > " + RrEventWire.MaxPayload);
                return;
            }

            int k = Online ? _core.Bound : -1;

            if (toAuthority)
            {
                if (IsInstanceAuthority || k < 0)
                {
                    if (!IsInstanceAuthority)
                    {
                        RrLog.WarnOnce("Net", "to-authority-unbound", "event " + type + " to the authority while the instance is not bound: dropped");
                        return;
                    }

                    Deliver(type, LocalId, body);
                    return;
                }

                Queue(BatchAuthority, k, type, body, k);
                return;
            }

            Deliver(type, LocalId, body);

            if (k >= 0 && _events != null)
            {
                Queue(BatchAll, k + 1, type, body, -1);
            }
        }

        /// <summary>Sends an event to every client in the world, whatever instance they are in (instance 0).</summary>
        public static void SendGlobal(byte type, byte[] body)
        {
            body = body ?? new byte[0];
            Deliver(type, LocalId, body);

            if (Online && _events != null && body.Length <= RrEventWire.MaxPayload)
            {
                Queue(BatchAll, 0, type, body, -1);
            }
        }

        /// <summary>Sends one event to one player at once (one RPC; census replies, private notices), scoped to my instance.</summary>
        public static void SendToPlayer(int playerId, byte type, byte[] body)
        {
            body = body ?? new byte[0];

            if (playerId == LocalId || !Online)
            {
                Deliver(type, LocalId, body);
                return;
            }

            if (!Valid(_events) || body.Length > RrEventWire.MaxPayload)
            {
                return;
            }

            var batch = new RrEventWire.Batch();
            batch.Begin(LocalId);
            int k = _core.Bound;

            if (!batch.Add(LocalId, type, k >= 0 ? k + 1 : 0, k >= 0 ? _core.BoundGeneration : 0, body, 0, body.Length))
            {
                return;
            }

            try
            {
                var runner = SceneNetworking.NetworkRunnerRef;

                if (runner == null)
                {
                    return;
                }

                foreach (PlayerRef player in runner.ActivePlayers)
                {
                    if (player.PlayerId == playerId)
                    {
                        _events.RPC_SendMessageToPlayer(player, batch.Take());
                        _framesSent++;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Net", "to-player", "sending to player " + playerId + " failed: " + e.Message);
            }
        }

        public static void On(byte type, Action<int, byte[]> handler)
        {
            if (handler == null)
            {
                return;
            }

            Action<int, byte[]> existing;
            Handlers.TryGetValue(type, out existing);
            Handlers[type] = existing + handler;
        }

        public static void Off(byte type, Action<int, byte[]> handler)
        {
            Action<int, byte[]> existing;

            if (handler == null || !Handlers.TryGetValue(type, out existing))
            {
                return;
            }

            existing -= handler;

            if (existing == null)
            {
                Handlers.Remove(type);
            }
            else
            {
                Handlers[type] = existing;
            }
        }

        // ------------------------------------------------------------------ sending

        private static void Queue(RrEventWire.Batch batch, int instance, byte type, byte[] body, int authorityObject)
        {
            int generation = instance > 0 ? _core.BoundGeneration : 0;

            if (batch == BatchAuthority && _batchAuthorityObject != authorityObject && !batch.Empty)
            {
                Flush();
            }

            if (batch == BatchAuthority)
            {
                _batchAuthorityObject = authorityObject;
                instance = authorityObject + 1;
            }

            if (!batch.Add(LocalId, type, instance, generation, body, 0, body.Length))
            {
                Flush();

                if (!batch.Add(LocalId, type, instance, generation, body, 0, body.Length))
                {
                    _eventsDropped++;
                }
            }
        }

        private static void Flush()
        {
            byte[] all = BatchAll.Take();

            if (all != null && Valid(_events))
            {
                _events.RPC_SendMessageToAll(all);
                _framesSent++;
            }

            byte[] toAuthority = BatchAuthority.Take();
            NetworkBridgeData target = _batchAuthorityObject >= 0 && _batchAuthorityObject < InstanceObjects.Length ? InstanceObjects[_batchAuthorityObject] : null;

            if (toAuthority != null && Valid(target))
            {
                target.RPC_SendMessageToController(toAuthority);
                _framesSent++;
            }
        }

        private static void TickEvents(double now)
        {
            if (now >= _flushAt)
            {
                _flushAt = now + FlushEvery;

                if (Online)
                {
                    Flush();
                }
            }

            DeliverLoopBack();
        }

        // ------------------------------------------------------------------ receiving

        private static void Deliver(byte type, int sender, byte[] body)
        {
            if (_driver == null)
            {
                Dispatch(type, sender, body);
                return;
            }

            LoopBack.Add(new Pending { Type = type, Sender = sender, Body = body });
        }

        private static void DeliverLoopBack()
        {
            if (LoopBack.Count == 0)
            {
                return;
            }

            Delivering.Clear();
            Delivering.AddRange(LoopBack);
            LoopBack.Clear();

            for (int i = 0; i < Delivering.Count; i++)
            {
                Dispatch(Delivering[i].Type, Delivering[i].Sender, Delivering[i].Body);
            }

            Delivering.Clear();
        }

        private static void OnFrameAll(byte[] frame)
        {
            Receive(frame, false, -1);
        }

        private static void OnFramePlayer(byte[] frame)
        {
            Receive(frame, true, -1);
        }

        private static void OnFrameController(int k, byte[] frame)
        {
            // Only the running authority of that instance acts on requests; anyone else holding the object ignores them.
            if (!Online || k != _core.Bound || !_core.IsAuthority)
            {
                _eventsDropped++;
                return;
            }

            Receive(frame, true, k);
        }

        private static void Receive(byte[] frame, bool direct, int controllerObject)
        {
            int sender;

            if (!RrEventWire.TryHeader(frame, out sender))
            {
                RrLog.WarnOnce("Net", "bad-frame", "an event frame of another build or damaged: ignored");
                return;
            }

            if (sender == LocalId && !direct)
            {
                return; // our own broadcast coming back: delivered locally already
            }

            _framesReceived++;
            int offset = RrEventWire.FrameHeader;
            int bound = _core.Bound;

            while (offset >= 0)
            {
                RrEventWire.Event e;
                int next = RrEventWire.Next(frame, offset, out e);

                if (next < 0)
                {
                    break;
                }

                offset = next;
                bool mine = controllerObject >= 0 ||
                            e.Instance == 0 ||
                            (bound >= 0 && e.Instance == bound + 1 && e.Generation == (_core.BoundGeneration & 0xFF));

                if (!mine)
                {
                    _eventsDropped++;
                    continue;
                }

                var body = new byte[e.Length];
                Buffer.BlockCopy(frame, e.Offset, body, 0, e.Length);
                Dispatch(e.Type, sender, body);
            }
        }

        private static void Dispatch(byte type, int sender, byte[] body)
        {
            Action<int, byte[]> handlers;

            if (!Handlers.TryGetValue(type, out handlers) || handlers == null)
            {
                return;
            }

            foreach (Action<int, byte[]> h in handlers.GetInvocationList())
            {
                try
                {
                    h(sender, body);
                }
                catch (Exception e)
                {
                    RrLog.Error("Net", "handler of event " + type + " failed", e);
                }
            }
        }

        private static void ClearEvents()
        {
            Handlers.Clear();
            LoopBack.Clear();
            Delivering.Clear();
            BatchAll.Begin(0);
            BatchAuthority.Begin(0);
            _batchAuthorityObject = -1;
        }

        /// <summary>Counters for diagnostics: frames sent/received, events dropped (other instances, no room).</summary>
        public static string EventStats
        {
            get { return "frames sent " + _framesSent + ", received " + _framesReceived + ", events dropped " + _eventsDropped + ", ints written " + _wroteInts; }
        }
    }
}
