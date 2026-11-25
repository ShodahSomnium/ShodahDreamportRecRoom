using System;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// The frame one RPC carries (somnium_networking.md §5.7): a batch of small events, flushed every 50 ms or when
    /// full, so a busy client stays near 20 RPCs a second whatever its games send. The Bridge RPC handlers get no
    /// RpcInfo (CM\NetworkBridgeEvents.cs:15-18), so the sender's PlayerId travels in the frame; and every event names
    /// the instance it belongs to, so receivers in other instances drop it. Pure (see <see cref="RrWire"/>).
    ///
    ///   frame:  [0] 'R' 0x52  [1] wire version  [2..3] sender PlayerId (u16 LE)  then events:
    ///   event:  [type u8] [length u16 LE] [instance object k+1 u8, 0 = global] [instance generation u8] [length bytes]
    ///
    /// Size: Fusion's RpcAttribute.MaxPayloadSize is 512 bytes (measured, §2.1) and a byte[] argument carries its
    /// length too; frames stop at 480 like the siblings' messages (ETS 400, Quake 480, AoM 500). One event's payload
    /// can therefore be at most 480 - 4 - 5 = 471 bytes.
    /// </summary>
    public static class RrEventWire
    {
        public const byte Marker = 0x52;
        public const byte Version = 1;
        public const int MaxFrame = 480;
        public const int FrameHeader = 4;
        public const int EventHeader = 5;
        public const int MaxPayload = MaxFrame - FrameHeader - EventHeader;

        /// <summary>One event inside a frame: where its payload lies.</summary>
        public struct Event
        {
            public byte Type;
            public int Instance;
            public int Generation;
            public int Offset;
            public int Length;
        }

        /// <summary>A frame being filled. Reused: Take copies the bytes out and starts over.</summary>
        public sealed class Batch
        {
            private readonly byte[] _buffer = new byte[MaxFrame];
            private int _length;
            private int _sender = -1;

            public int Count { get; private set; }

            public bool Empty
            {
                get { return Count == 0; }
            }

            public int Length
            {
                get { return _length; }
            }

            public void Begin(int sender)
            {
                _sender = sender;
                _buffer[0] = Marker;
                _buffer[1] = Version;
                _buffer[2] = (byte)(sender & 0xFF);
                _buffer[3] = (byte)((sender >> 8) & 0xFF);
                _length = FrameHeader;
                Count = 0;
            }

            public bool Fits(int payload)
            {
                return _length + EventHeader + payload <= MaxFrame;
            }

            /// <summary>Appends an event; false when it does not fit (flush first) or is too big for any frame.</summary>
            public bool Add(int sender, byte type, int instance, int generation, byte[] payload, int offset, int count)
            {
                if (count < 0 || count > MaxPayload)
                {
                    return false;
                }

                if (_length == 0 || sender != _sender)
                {
                    if (Count > 0)
                    {
                        return false;
                    }

                    Begin(sender);
                }

                if (!Fits(count))
                {
                    return false;
                }

                _buffer[_length++] = type;
                _buffer[_length++] = (byte)(count & 0xFF);
                _buffer[_length++] = (byte)((count >> 8) & 0xFF);
                _buffer[_length++] = (byte)(instance & 0xFF);
                _buffer[_length++] = (byte)(generation & 0xFF);

                if (count > 0)
                {
                    Buffer.BlockCopy(payload, offset, _buffer, _length, count);
                }

                _length += count;
                Count++;
                return true;
            }

            /// <summary>The frame's bytes (a new array, never empty) and a fresh start; null when nothing is queued.</summary>
            public byte[] Take()
            {
                if (Count == 0)
                {
                    return null;
                }

                var bytes = new byte[_length];
                Buffer.BlockCopy(_buffer, 0, bytes, 0, _length);
                Begin(_sender);
                return bytes;
            }
        }

        /// <summary>The frame's sender, or false when it is not a frame of this build.</summary>
        public static bool TryHeader(byte[] frame, out int sender)
        {
            sender = -1;

            if (frame == null || frame.Length < FrameHeader || frame[0] != Marker || frame[1] != Version)
            {
                return false;
            }

            sender = frame[2] | (frame[3] << 8);
            return true;
        }

        /// <summary>Reads the event at offset; returns the offset of the next one, or -1 at the end or on a damaged frame.</summary>
        public static int Next(byte[] frame, int offset, out Event e)
        {
            e = default;

            if (frame == null || offset < FrameHeader || offset + EventHeader > frame.Length)
            {
                return -1;
            }

            int length = frame[offset + 1] | (frame[offset + 2] << 8);

            if (offset + EventHeader + length > frame.Length)
            {
                return -1;
            }

            e.Type = frame[offset];
            e.Instance = frame[offset + 3];
            e.Generation = frame[offset + 4];
            e.Offset = offset + EventHeader;
            e.Length = length;
            return offset + EventHeader + length;
        }
    }
}
