using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// A synced physics prop's 12 ints, "Rr Net Prop 0..47" (NetworkBridgeData12; somnium_networking.md §5.5.2).
    /// Owner-simulated: the holder or thrower (or, when nobody, the instance authority) writes; everyone else draws a
    /// kinematic puppet. Held props cost nothing on the wire (they are drawn at the holder's hand with the grip offset
    /// from the holder's presence slot); flying props stream at up to 30 Hz with a forced sample at every collision.
    /// Pure (see <see cref="RrWire"/>).
    ///
    ///   0  lease: tag 0xA5 (0-7) | instance object k+1 (8-15) | instance generation (16-23) | local prop id (24-31)
    ///   1  state (0-3: Free, Resting, Held, Flying, Hidden) | hand (4: 0 right, 1 left) | epoch (8-15, bumped on reset,
    ///      adoption and throw: receivers snap) | writer (the holder, thrower or authority) PlayerId+1 (16-31)
    ///   2  seq, bumped per motion sample
    ///   3  stamp ms (the writer's Time.fixedTimeAsDouble)
    ///   4-6  position, world mm
    ///   7  rotation, smallest-three 32
    ///   8  velocity x | y (S16 cm/s)
    ///   9  velocity z (S16 cm/s) | throw id (16-31, +1 per throw: catch/hit arbitration)
    ///  10  angular velocity x, y, z (3 x signed 10 bits, 0.1 rad/s)
    ///  11  kind-specific (dodgeball live/team, disc flight flags, bottle fill...)
    ///
    /// A lease is live while the instance object it names still carries its generation and somebody present names
    /// that object; otherwise the prop object is free for any instance's authority to lease.
    /// </summary>
    public static class RrPropWire
    {
        public const string ObjectPrefix = "Rr Net Prop ";
        public const int Ints = 12;
        public const int Objects = 48;
        public const int Tag = 0xA5;

        public const int AtLease = 0;
        public const int AtState = 1;
        public const int AtSeq = 2;
        public const int AtStamp = 3;
        public const int AtPos = 4;
        public const int AtRot = 7;
        public const int AtVelXY = 8;
        public const int AtVelZ = 9;
        public const int AtAngular = 10;
        public const int AtKind = 11;

        public const int StateFree = 0;
        public const int StateResting = 1;
        public const int StateHeld = 2;
        public const int StateFlying = 3;
        public const int StateHidden = 4;

        public static bool Leased(int[] r)
        {
            return r != null && r.Length >= Ints && (r[AtLease] & 0xFF) == Tag;
        }

        public static int LeaseObject(int[] r)
        {
            return Leased(r) ? RrWire.Field(r[AtLease], 8, 8) - 1 : -1;
        }

        public static int LeaseGeneration(int[] r)
        {
            return RrWire.Field(r[AtLease], 16, 8);
        }

        public static int LeaseLocalId(int[] r)
        {
            return RrWire.Field(r[AtLease], 24, 8);
        }

        public static int PackLease(int instanceObject, int generation, int localPropId)
        {
            return Tag | (((instanceObject + 1) & 0xFF) << 8) | ((generation & 0xFF) << 16) | ((localPropId & 0xFF) << 24);
        }

        public static bool LeaseMatches(int[] r, int instanceObject, int generation, int localPropId)
        {
            return Leased(r) && r[AtLease] == PackLease(instanceObject, generation, localPropId);
        }

        public static int State(int[] r)
        {
            return RrWire.Field(r[AtState], 0, 4);
        }

        public static bool LeftHand(int[] r)
        {
            return RrWire.Bit(r[AtState], 4);
        }

        public static int Epoch(int[] r)
        {
            return RrWire.Field(r[AtState], 8, 8);
        }

        public static int Writer(int[] r)
        {
            return RrWire.FromId16(r[AtState] >> 16);
        }

        public static int ThrowId(int[] r)
        {
            return RrWire.Field(r[AtVelZ], 16, 16);
        }

        public static int PackState(int state, bool left, int epoch, int writer)
        {
            return (state & 0xF) | (left ? 1 << 4 : 0) | ((epoch & 0xFF) << 8) | (RrWire.Id16(writer) << 16);
        }

        /// <summary>
        /// A prop object a lease may take (§5.5.2): never leased, or its lease names an instance object that no longer
        /// carries that generation or that nobody present names any more.
        /// </summary>
        public static bool FreeForLease(int[] r, int[] instanceGeneration, bool[] instanceLive)
        {
            if (!Leased(r))
            {
                return true;
            }

            int k = LeaseObject(r);

            if (k < 0 || instanceGeneration == null || k >= instanceGeneration.Length)
            {
                return true;
            }

            return instanceGeneration[k] != LeaseGeneration(r) || !instanceLive[k];
        }

        public static void PutMotion(int[] r, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angular)
        {
            RrWire.PutMm(r, AtPos, position);
            r[AtRot] = RrWire.PackRot(rotation);
            r[AtVelXY] = RrWire.Pair(RrWire.S16(velocity.x * 100.0), RrWire.S16(velocity.y * 100.0));
            r[AtVelZ] = (r[AtVelZ] & unchecked((int)0xFFFF0000)) | RrWire.S16(velocity.z * 100.0);
            r[AtAngular] = RrWire.PackAngular(angular);
        }

        public static void SetThrowId(int[] r, int throwId)
        {
            r[AtVelZ] = (r[AtVelZ] & 0xFFFF) | ((throwId & 0xFFFF) << 16);
        }

        /// <summary>The record as a timeline sample (position, rotation, velocities, epoch, seq, stamp).</summary>
        public static RrNetState ToState(int[] r)
        {
            return new RrNetState
            {
                Seq = r[AtSeq],
                StampMs = r[AtStamp],
                Epoch = Epoch(r),
                Position = RrWire.GetMm(r, AtPos),
                Rotation = r[AtRot] == 0 ? new Quaternion(0f, 0f, 0f, 1f) : RrWire.UnpackRot(r[AtRot]),
                Velocity = new Vector3(RrWire.Lo(r[AtVelXY]) / 100f, RrWire.Hi(r[AtVelXY]) / 100f, RrWire.Lo(r[AtVelZ]) / 100f),
                AngularVelocity = RrWire.UnpackAngular(r[AtAngular])
            };
        }
    }
}
