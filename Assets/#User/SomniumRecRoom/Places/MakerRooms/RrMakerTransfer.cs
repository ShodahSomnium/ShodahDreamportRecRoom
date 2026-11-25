using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>
    /// Room data from player to player in chunks (task 4b, 4c): a room file someone holds (event 25 asks, event 26 carries,
    /// kind <see cref="RrMakerWire.KindFile"/>) and the live state of a room from its instance authority to a late joiner
    /// (event 28 asks, event 26 carries, kind <see cref="RrMakerWire.KindState"/>).
    ///
    /// A chunk is [u16 target][u8 kind][u16 transfer][u16 index][u16 count][i32 total][u32 crc][up to 444 bytes]; count 0
    /// means "I do not have it". The whole blob is the packed (deflated) JSON, checked with CRC-32 and parsed before it is
    /// used. Senders pace themselves: at most <see cref="ChunksPerTick"/> chunks every <see cref="TickSeconds"/> over all
    /// transfers (~18 KB/s), so a 20 KB room takes about a second. A request that makes no progress for
    /// <see cref="StallSeconds"/> moves on to the next holder.
    ///
    /// Routing. RrNet.SendToPlayer stamps the event with the sender's instance, so a player in another instance drops it.
    /// When both players are in the same instance, or the sender is in none, it is used; otherwise the message goes out
    /// with RrNet.SendGlobal and everyone but the target ignores it (the target is in the payload either way).
    /// </summary>
    public static class RrMakerTransfer
    {
        public const float TickSeconds = 0.05f;
        public const int ChunksPerTick = 2;
        public const float StallSeconds = 6f;

        /// <summary>Blobs larger than this are refused (a room of 4000 objects packs to well under 1 MB).</summary>
        public const int MaxBlob = 4 << 20;

        private sealed class Outgoing
        {
            public int Target;
            public byte Kind;
            public ushort Transfer;
            public byte[] Data;
            public uint Crc;
            public int Next;
            public int Count;
        }

        /// <summary>One download in progress (a room file or a live state).</summary>
        public sealed class Incoming
        {
            public string RoomId;
            public byte Kind;
            public ushort Transfer;
            public int MinVersion;

            /// <summary>The player sending now (-1 before the first chunk of a state answer).</summary>
            public int From = -1;

            public readonly List<int> Candidates = new List<int>();
            public byte[][] Parts;
            public int Have;
            public int Count;
            public int Total;
            public uint Crc;
            public float LastProgress;
            public Action<byte[]> Done;
            public Action<string> Failed;
            public bool Finished;

            /// <summary>0..1 for the watch.</summary>
            public float Progress
            {
                get { return Count > 0 ? Have / (float)Count : 0f; }
            }
        }

        private static readonly List<Outgoing> Out = new List<Outgoing>();
        private static readonly Dictionary<ushort, Incoming> In = new Dictionary<ushort, Incoming>();
        private static ushort _nextTransfer = 1;
        private static float _tickAt;

        /// <summary>Serves a file request: (room id, minimum version, asking PlayerId) to the packed bytes held here, or null (set by the manager).</summary>
        public static Func<string, int, int, byte[]> FileSource;

        public static int Sending
        {
            get { return Out.Count; }
        }

        public static int Receiving
        {
            get { return In.Count; }
        }

        public static void Shutdown()
        {
            Out.Clear();
            In.Clear();
            FileSource = null;
            _nextTransfer = 1;
        }

        private static ushort NewTransfer()
        {
            ushort t = _nextTransfer++;
            if (_nextTransfer == 0)
            {
                _nextTransfer = 1;
            }

            return t;
        }

        // ------------------------------------------------------------------ asking

        /// <summary>
        /// Downloads a room file of at least <paramref name="minVersion"/> from the first of <paramref name="holders"/> that
        /// has it (the next one on a refusal or a stall). done gets the packed bytes (checked), failed a reason.
        /// </summary>
        public static Incoming RequestFile(string roomId, int minVersion, List<int> holders, Action<byte[]> done, Action<string> failed)
        {
            var job = new Incoming { RoomId = roomId, Kind = RrMakerWire.KindFile, MinVersion = minVersion, Done = done, Failed = failed, Transfer = NewTransfer() };
            if (holders != null)
            {
                foreach (int h in holders)
                {
                    if (h != RrNet.LocalId && !job.Candidates.Contains(h))
                    {
                        job.Candidates.Add(h);
                    }
                }
            }

            In[job.Transfer] = job;
            AskNext(job, "start");
            return job;
        }

        /// <summary>Asks the instance authority for the room's live state (event 28); done gets the packed state.</summary>
        public static Incoming RequestState(string roomId, Action<byte[]> done, Action<string> failed)
        {
            var job = new Incoming { RoomId = roomId, Kind = RrMakerWire.KindState, Done = done, Failed = failed, Transfer = NewTransfer(), LastProgress = Time.realtimeSinceStartup };
            In[job.Transfer] = job;
            var w = new RrMakerWriter();
            w.U16(job.Transfer);
            RrNet.Send(RrMakerEvents.AskState, w.ToArray(), true);
            RrLog.Info("MakerRooms", "asked the instance authority (player " + RrNet.InstanceAuthorityId + ") for the live state of room " + roomId + " (transfer " + job.Transfer + ")");
            return job;
        }

        /// <summary>Gives up a download (its callbacks are not called).</summary>
        public static void Cancel(Incoming job)
        {
            if (job != null)
            {
                job.Finished = true;
                In.Remove(job.Transfer);
            }
        }

        private static void AskNext(Incoming job, string why)
        {
            if (job.Finished)
            {
                return;
            }

            if (job.Candidates.Count == 0)
            {
                Fail(job, why == "start" ? "nobody online holds it" : "no holder could send it (" + why + ")");
                return;
            }

            job.From = job.Candidates[0];
            job.Candidates.RemoveAt(0);
            job.Parts = null;
            job.Have = 0;
            job.Count = 0;
            job.LastProgress = Time.realtimeSinceStartup;
            var w = new RrMakerWriter();
            w.U16(job.From);
            w.U8(job.Kind);
            w.Bytes(RrMakerWire.IdBytes(job.RoomId));
            w.I32(job.MinVersion);
            w.U16(job.Transfer);
            SendDirect(job.From, RrMakerEvents.Request, w.ToArray());
            RrLog.Info("MakerRooms", "asked player " + job.From + " for room " + job.RoomId + " v" + job.MinVersion + "+ (transfer " + job.Transfer + (why != "start" ? ", after: " + why : "") + ")");
        }

        private static void Fail(Incoming job, string why)
        {
            if (job.Finished)
            {
                return;
            }

            job.Finished = true;
            In.Remove(job.Transfer);
            RrLog.Warn("MakerRooms", (job.Kind == RrMakerWire.KindFile ? "download of room " : "live state of room ") + job.RoomId + " failed: " + why);
            if (job.Failed != null)
            {
                try
                {
                    job.Failed(why);
                }
                catch (Exception e)
                {
                    RrLog.Error("MakerRooms", "transfer failure handler threw", e);
                }
            }
        }

        // ------------------------------------------------------------------ serving

        /// <summary>Queues a blob to a player in chunks.</summary>
        public static void Send(int target, byte kind, ushort transfer, byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > MaxBlob)
            {
                Decline(target, kind, transfer);
                return;
            }

            // A newer request for the same transfer replaces the old one.
            Out.RemoveAll(o => o.Target == target && o.Transfer == transfer && o.Kind == kind);
            int count = (data.Length + RrMakerWire.ChunkData - 1) / RrMakerWire.ChunkData;
            Out.Add(new Outgoing { Target = target, Kind = kind, Transfer = transfer, Data = data, Crc = RrMakerWire.Crc32(data), Count = count });
            RrLog.Info("MakerRooms", "sending " + (kind == RrMakerWire.KindFile ? "a room file" : "the live state") + " to player " + target + ": " + data.Length + " bytes in " + count + " chunk(s)");
        }

        public static void Decline(int target, byte kind, ushort transfer)
        {
            var w = new RrMakerWriter();
            w.U16(target);
            w.U8(kind);
            w.U16(transfer);
            w.U16(0);
            w.U16(0);
            w.I32(0);
            w.U32(0);
            SendDirect(target, RrMakerEvents.Chunk, w.ToArray());
        }

        /// <summary>Event 25 (a player asks this one for a room file).</summary>
        public static void OnRequest(int sender, byte[] body)
        {
            var r = new RrMakerReader(body);
            int target = r.U16();
            byte kind = r.U8();
            string id = RrMakerWire.IdFrom(r.Bytes(16), 0);
            int minVersion = r.I32();
            ushort transfer = (ushort)r.U16();
            if (!r.Ok || target != (RrNet.LocalId & 0xFFFF) || sender == RrNet.LocalId)
            {
                return;
            }

            byte[] data = null;
            if (kind == RrMakerWire.KindFile && FileSource != null)
            {
                try
                {
                    data = FileSource(id, minVersion, sender);
                }
                catch (Exception e)
                {
                    RrLog.Error("MakerRooms", "serving room " + id + " failed", e);
                }
            }

            if (data == null)
            {
                RrLog.Info("MakerRooms", "player " + sender + " asked for room " + id + " v" + minVersion + "+: not held here");
                Decline(sender, kind, transfer);
                return;
            }

            Send(sender, kind, transfer, data);
        }

        /// <summary>Event 26 (a chunk for one of this player's downloads).</summary>
        public static void OnChunk(int sender, byte[] body)
        {
            var r = new RrMakerReader(body);
            int target = r.U16();
            byte kind = r.U8();
            ushort transfer = (ushort)r.U16();
            int index = r.U16();
            int count = r.U16();
            int total = r.I32();
            uint crc = r.U32();
            if (!r.Ok || target != (RrNet.LocalId & 0xFFFF))
            {
                return;
            }

            Incoming job;
            if (!In.TryGetValue(transfer, out job) || job.Finished || job.Kind != kind)
            {
                return;
            }

            if (job.From >= 0 && sender != job.From)
            {
                return;
            }

            if (count == 0)
            {
                if (kind == RrMakerWire.KindFile)
                {
                    AskNext(job, "player " + sender + " does not have it");
                }
                else
                {
                    Fail(job, "the authority has no state yet");
                }

                return;
            }

            if (total <= 0 || total > MaxBlob || index >= count || count > (MaxBlob / 64))
            {
                return;
            }

            if (job.Parts == null || job.Count != count || job.Total != total || job.Crc != crc)
            {
                job.Parts = new byte[count][];
                job.Count = count;
                job.Total = total;
                job.Crc = crc;
                job.Have = 0;
                job.From = sender;
            }

            if (job.Parts[index] == null)
            {
                job.Parts[index] = r.Rest();
                job.Have++;
                job.LastProgress = Time.realtimeSinceStartup;
            }

            if (job.Have < job.Count)
            {
                return;
            }

            var blob = new byte[total];
            int at = 0;
            bool fits = true;
            foreach (var p in job.Parts)
            {
                if (at + p.Length > total)
                {
                    fits = false;
                    break;
                }

                Buffer.BlockCopy(p, 0, blob, at, p.Length);
                at += p.Length;
            }

            if (!fits || at != total || RrMakerWire.Crc32(blob) != crc)
            {
                RrLog.Warn("MakerRooms", "transfer " + transfer + " from player " + sender + " arrived damaged (" + at + " of " + total + " bytes, crc " + (RrMakerWire.Crc32(blob) == crc ? "ok" : "bad") + ")");
                if (kind == RrMakerWire.KindFile)
                {
                    AskNext(job, "damaged data");
                }
                else
                {
                    Fail(job, "damaged data");
                }

                return;
            }

            job.Finished = true;
            In.Remove(transfer);
            RrLog.Info("MakerRooms", "received " + (kind == RrMakerWire.KindFile ? "room file " : "live state of ") + job.RoomId + " from player " + sender + ": " + total + " bytes in " + count + " chunk(s)");
            if (job.Done != null)
            {
                try
                {
                    job.Done(blob);
                }
                catch (Exception e)
                {
                    RrLog.Error("MakerRooms", "transfer handler threw", e);
                }
            }
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Sends queued chunks and watches downloads for stalls (from the manager's driver).</summary>
        public static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            if (now >= _tickAt)
            {
                _tickAt = now + TickSeconds;
                int budget = ChunksPerTick;
                for (int i = 0; i < Out.Count && budget > 0;)
                {
                    var o = Out[i];
                    if (!PlayerPresent(o.Target))
                    {
                        Out.RemoveAt(i);
                        continue;
                    }

                    SendChunk(o);
                    budget--;
                    if (o.Next >= o.Count)
                    {
                        Out.RemoveAt(i);
                        continue;
                    }

                    i++;
                }

                // Round robin: the transfer served last goes to the back.
                if (Out.Count > 1)
                {
                    var first = Out[0];
                    Out.RemoveAt(0);
                    Out.Add(first);
                }
            }

            if (In.Count == 0)
            {
                return;
            }

            List<Incoming> stalled = null;
            foreach (var job in In.Values)
            {
                // A live state is answered only once the authority's own room is built (a Park takes a minute): before its
                // first chunk the asking room's own wait (RrMakerRoom.StateWait) decides, not the 6 s stall.
                float stall = job.Kind == RrMakerWire.KindState && job.Count == 0 ? 60f : StallSeconds;
                if (!job.Finished && now - job.LastProgress > stall)
                {
                    (stalled ?? (stalled = new List<Incoming>())).Add(job);
                }
            }

            if (stalled != null)
            {
                foreach (var job in stalled)
                {
                    if (job.Kind == RrMakerWire.KindFile)
                    {
                        AskNext(job, "player " + job.From + " stopped answering");
                    }
                    else
                    {
                        Fail(job, "no answer from the instance authority");
                    }
                }
            }
        }

        private static void SendChunk(Outgoing o)
        {
            int at = o.Next * RrMakerWire.ChunkData;
            int n = Math.Min(RrMakerWire.ChunkData, o.Data.Length - at);
            var w = new RrMakerWriter();
            w.U16(o.Target);
            w.U8(o.Kind);
            w.U16(o.Transfer);
            w.U16(o.Next);
            w.U16(o.Count);
            w.I32(o.Data.Length);
            w.U32(o.Crc);
            var piece = new byte[n];
            Buffer.BlockCopy(o.Data, at, piece, 0, n);
            w.Bytes(piece);
            SendDirect(o.Target, RrMakerEvents.Chunk, w.ToArray());
            o.Next++;
        }

        private static bool PlayerPresent(int playerId)
        {
            if (!RrNet.Online)
            {
                return false;
            }

            foreach (int id in RrNet.Present)
            {
                if (id == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// One message to one player whatever instance they are in: SendToPlayer when it will be accepted (this client is in
        /// no bound instance, or both are in the same one), else a global broadcast that only the target acts on.
        /// </summary>
        public static void SendDirect(int target, byte type, byte[] body)
        {
            if (!RrNet.Online || target == RrNet.LocalId)
            {
                return;
            }

            int mine = RrNet.InstanceObjectIndex;
            bool direct = mine < 0;
            if (!direct)
            {
                var them = RrNet.Presence(target);
                var me = RrNet.Presence(RrNet.LocalId);
                direct = them.Valid && me.Valid && them.Activity == me.Activity && them.Instance == me.Instance && them.InstanceObject == mine;
            }

            if (direct)
            {
                RrNet.SendToPlayer(target, type, body);
            }
            else
            {
                RrNet.SendGlobal(type, body);
            }
        }

        /// <summary>A line for the watch about the download in progress, or null.</summary>
        public static string Describe(Incoming job)
        {
            if (job == null || job.Finished)
            {
                return null;
            }

            if (job.Count == 0)
            {
                return job.From >= 0 ? "asking player " + job.From + "..." : "asking...";
            }

            return Mathf.RoundToInt(job.Progress * 100f) + "%";
        }
    }
}
