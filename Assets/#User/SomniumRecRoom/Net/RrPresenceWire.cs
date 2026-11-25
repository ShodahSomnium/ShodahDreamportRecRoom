using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// A presence slot's 64 ints, "Rr Net Player 0..49" (NetworkBridgeData64; somnium_networking.md §5.4). One per
    /// player, claimed with <see cref="RrSlotClaim"/> the way ETS claims its player slots; only the owner writes it.
    /// It says where the player is (activity, instance, status, team) and carries their per-player game state,
    /// so a late joiner reads everyone straight out of the arrays with no census. Pure (see <see cref="RrWire"/>).
    ///
    ///   0  magic 'RRP1' (0 = never written; anything else is not a slot of this build)
    ///   1  seq, bumped when ints 3..19 change or on a claim
    ///   2  stamp: the writer's Time.fixedTimeAsDouble in ms when seq moved
    ///   3  owner PlayerId+1 (0-15) | claim generation (16-23) | flags (24 VR, 25 NoGame, 26 menu open, 27 AFK)
    ///   4  activity (0-7) | instance number (8-15) | status (16-19) | team (20-23, 15 = none) | instance object k+1 (24-31)
    ///   5  instance generation seen (0-7) | spawn index (8-15) | role/loadout (16-23) | presence epoch (24-31, +1 per trip)
    ///   6  UserId hash (FNV-1a 32 of the Bridge's Properties.Id): who this is across a rejoin
    ///   7  party leader PlayerId+1 (0-15) | spare
    ///   8  right hand: item id (0-11) | item state (12-23) | held prop p+1 (24-31)
    ///   9  left hand: the same
    ///  10  right grip offset x | y (S16 mm, the held thing relative to the Bridge hand)
    ///  11  right grip offset z (S16 mm) | spare
    ///  12  right grip rotation, smallest-three 32
    ///  13-15  left grip offset: the same
    ///  16  right aim yaw | pitch (Angle16; screen mode and fallback only)
    ///  17  left aim yaw | pitch
    ///  18  health (0-7) | ammo (8-15) | kills (16-23) | deaths (24-31), each 0..255
    ///  19  activity-specific per player (bowling frame/ball, disc golf strokes...)
    ///  20-28  vehicle: seq, stamp, position mm x3, rotation 32, velocity x|y S16 cm/s, velocity z S16 | spare, flags
    ///  29-31  spare
    ///  32-47  activity-specific per player (Rec Royale inventory...)
    ///  48-58  Rec Room avatar (iteration 15, Player/Avatar/RrAvatarOutfit.cs RrAvatarWire): wear | skin | eyes | mouth,
    ///         then one int per outfit slot (item | dye | design)
    ///  59-63  spare
    ///
    /// Membership (§5.4): a player is a participant of instance (a, n) when their slot is written, they are in the
    /// room, not NoGame, and it names a and n with a status other than None; a member once the status is Lobby,
    /// Playing, Spectating or Down. Written at 4 Hz or on change, through "only changed ints" pushes: a player
    /// standing still costs nothing on the wire.
    /// </summary>
    public static class RrPresenceWire
    {
        public const string ObjectPrefix = "Rr Net Player ";
        public const int Magic = 0x52525031;
        public const int Ints = 64;
        public const int Slots = 50;

        public const int AtMagic = 0;
        public const int AtSeq = 1;
        public const int AtStamp = 2;
        public const int AtOwner = 3;
        public const int AtWhere = 4;
        public const int AtSpawn = 5;
        public const int AtUser = 6;
        public const int AtParty = 7;
        public const int AtRightHand = 8;
        public const int AtLeftHand = 9;
        public const int AtRightGrip = 10;
        public const int AtLeftGrip = 13;
        public const int AtRightAim = 16;
        public const int AtLeftAim = 17;
        public const int AtCombat = 18;
        public const int AtActivityInt = 19;
        public const int AtVehicle = 20;
        public const int AtActivityInts = 32;
        public const int ActivityInts = 16;

        /// <summary>The Rec Room avatar's ints (48-58), written by Player.Avatars.RrAvatars.</summary>
        public const int AtAvatar = 48;
        public const int AvatarInts = 11;

        /// <summary>The ints whose change bumps seq (the header and per-player state; aims and vehicle have their own rate).</summary>
        public const int HeaderEnd = 20;

        public const int FlagVr = 24;
        public const int FlagNoGame = 25;
        public const int FlagMenu = 26;
        public const int FlagAfk = 27;

        public const int NoTeam = 15;

        /// <summary>Instance number reserved for a private solo copy (never chosen, never bound).</summary>
        public const int SoloNumber = 255;

        // Status values (RrStatus, Rooms/RrActivity.cs).
        public const int StatusNone = 0;
        public const int StatusLoading = 1;
        public const int StatusLobby = 2;
        public const int StatusPlaying = 3;
        public const int StatusSpectating = 4;
        public const int StatusDown = 5;

        // ------------------------------------------------------------------ reading

        public static bool Written(int[] r)
        {
            return r != null && r.Length >= Ints && r[AtMagic] == Magic;
        }

        /// <summary>The owner's PlayerId, -1 when unwritten or written free.</summary>
        public static int OwnerOf(int[] r)
        {
            return Written(r) ? RrWire.FromId16(r[AtOwner]) : -1;
        }

        public static int GenerationOf(int[] r)
        {
            return Written(r) ? RrWire.Field(r[AtOwner], 16, 8) : 0;
        }

        public static bool Flag(int[] r, int bit)
        {
            return Written(r) && RrWire.Bit(r[AtOwner], bit);
        }

        public static int Activity(int[] r)
        {
            return RrWire.Field(r[AtWhere], 0, 8);
        }

        public static int Number(int[] r)
        {
            return RrWire.Field(r[AtWhere], 8, 8);
        }

        public static int Status(int[] r)
        {
            return RrWire.Field(r[AtWhere], 16, 4);
        }

        public static int Team(int[] r)
        {
            int t = RrWire.Field(r[AtWhere], 20, 4);
            return t == NoTeam ? -1 : t;
        }

        /// <summary>The instance object the player's instance is bound to, -1 none.</summary>
        public static int InstanceObject(int[] r)
        {
            return RrWire.Field(r[AtWhere], 24, 8) - 1;
        }

        public static int InstanceGeneration(int[] r)
        {
            return RrWire.Field(r[AtSpawn], 0, 8);
        }

        public static int SpawnIndex(int[] r)
        {
            return RrWire.Field(r[AtSpawn], 8, 8);
        }

        public static int Role(int[] r)
        {
            return RrWire.Field(r[AtSpawn], 16, 8);
        }

        public static int Epoch(int[] r)
        {
            return RrWire.Field(r[AtSpawn], 24, 8);
        }

        public static int PartyLeader(int[] r)
        {
            return RrWire.FromId16(r[AtParty]);
        }

        public static int HandAt(bool left)
        {
            return left ? AtLeftHand : AtRightHand;
        }

        public static int GripAt(bool left)
        {
            return left ? AtLeftGrip : AtRightGrip;
        }

        public static int HandItem(int[] r, bool left)
        {
            return RrWire.Field(r[HandAt(left)], 0, 12);
        }

        public static int HandItemState(int[] r, bool left)
        {
            return RrWire.Field(r[HandAt(left)], 12, 12);
        }

        /// <summary>The prop object p the hand holds, -1 none.</summary>
        public static int HandProp(int[] r, bool left)
        {
            return RrWire.Field(r[HandAt(left)], 24, 8) - 1;
        }

        public static Vector3 GripPosition(int[] r, bool left)
        {
            int at = GripAt(left);
            return new Vector3(RrWire.Lo(r[at]) / 1000f, RrWire.Hi(r[at]) / 1000f, RrWire.Lo(r[at + 1]) / 1000f);
        }

        public static Quaternion GripRotation(int[] r, bool left)
        {
            int packed = r[GripAt(left) + 2];
            return packed == 0 ? new Quaternion(0f, 0f, 0f, 1f) : RrWire.UnpackRot(packed);
        }

        public static void PutGrip(int[] r, bool left, Vector3 position, Quaternion rotation)
        {
            int at = GripAt(left);
            r[at] = RrWire.Pair(RrWire.S16(position.x * 1000.0), RrWire.S16(position.y * 1000.0));
            r[at + 1] = RrWire.S16(position.z * 1000.0);
            r[at + 2] = RrWire.PackRot(rotation);
        }

        public static void ClearGrip(int[] r, bool left)
        {
            int at = GripAt(left);
            r[at] = 0;
            r[at + 1] = 0;
            r[at + 2] = 0;
        }

        public static int Health(int[] r)
        {
            return RrWire.Field(r[AtCombat], 0, 8);
        }

        public static int Ammo(int[] r)
        {
            return RrWire.Field(r[AtCombat], 8, 8);
        }

        public static int Kills(int[] r)
        {
            return RrWire.Field(r[AtCombat], 16, 8);
        }

        public static int Deaths(int[] r)
        {
            return RrWire.Field(r[AtCombat], 24, 8);
        }

        /// <summary>Activity-specific per-player int: index 0 = int 19, 1..16 = ints 32..47; -1 out of range.</summary>
        public static int ActivityIntAt(int index)
        {
            if (index == 0)
            {
                return AtActivityInt;
            }

            return index >= 1 && index <= ActivityInts ? AtActivityInts + index - 1 : -1;
        }

        public static bool IsMemberStatus(int status)
        {
            return status >= StatusLobby && status <= StatusDown;
        }

        // ------------------------------------------------------------------ writing the own record

        public static void SetWhere(int[] r, int activity, int number, int status, int team, int instanceObject)
        {
            r[AtWhere] = (activity & 0xFF) | ((number & 0xFF) << 8) | ((status & 0xF) << 16) |
                         ((team < 0 || team >= NoTeam ? NoTeam : team) << 20) | (((instanceObject + 1) & 0xFF) << 24);
        }

        // ------------------------------------------------------------------ slot rules (ETS EtsSlotRules, adapted)

        /// <summary>
        /// A slot this client may claim (ETS FreeFor): never written, written free (owner 0), owned by a player no
        /// longer in the room, or already this client's (a claim that outlived a room blink).
        /// </summary>
        public static bool FreeFor(int[] slot, int localId, IReadOnlyList<int> present)
        {
            if (!Written(slot))
            {
                return true;
            }

            int owner = OwnerOf(slot);
            return owner < 0 || owner == localId || !RrWire.Contains(present, owner);
        }

        /// <summary>A slot whose owner is in the room (listed in the People page), the local player's own included.</summary>
        public static bool Listed(int[] slot, IReadOnlyList<int> present)
        {
            int owner = OwnerOf(slot);
            return owner >= 0 && RrWire.Contains(present, owner);
        }

        /// <summary>
        /// Slot housekeeping by whoever holds a slot's authority (4 Hz; ETS ToFree): write it free when it names a
        /// player who has left, or this client itself while it is not the slot this client claims (a record left
        /// behind when a simultaneous claim moved this client on; without this rule the player is listed twice).
        /// </summary>
        public static bool ToFree(int[] slot, bool haveAuthority, bool claimedHere, int localId, IReadOnlyList<int> present)
        {
            if (!haveAuthority || claimedHere || !Written(slot))
            {
                return false;
            }

            int owner = OwnerOf(slot);
            return owner >= 0 && (owner == localId || !RrWire.Contains(present, owner));
        }

        /// <summary>The free record: 'RRP1', seq + 1, the stamp, owner 0 with the claim generation kept, all else 0.</summary>
        public static void WriteFree(int[] into, int stampMs)
        {
            int seq = into[AtSeq] + 1;
            int generation = GenerationOf(into);
            Array.Clear(into, 0, into.Length);
            into[AtMagic] = Magic;
            into[AtSeq] = seq;
            into[AtStamp] = stampMs;
            into[AtOwner] = generation << 16;
            SetWhere(into, 0, 0, StatusNone, -1, -1);
        }

        /// <summary>
        /// Stamps a claim onto the own record (the rest of it - where the player is, hands, scores - is what the owner
        /// already composed): magic, seq + 1, owner and the new generation, flags kept.
        /// </summary>
        public static void StampClaim(int[] own, int localId, int generation, int stampMs)
        {
            own[AtMagic] = Magic;
            own[AtSeq] = own[AtSeq] + 1;
            own[AtStamp] = stampMs;
            own[AtOwner] = (own[AtOwner] & unchecked((int)0xFF000000)) | RrWire.Id16(localId) | ((generation & 0xFF) << 16);
        }

        /// <summary>
        /// A participant of instance (activity, number): written, owner in the room, not NoGame, status not None,
        /// naming that activity and number. instanceObject >= 0 also requires the slot to name that object or none
        /// (a joiner that has not found the object yet); -1 accepts any.
        /// </summary>
        public static bool Participant(int[] slot, IReadOnlyList<int> present, int activity, int number, int instanceObject)
        {
            if (!Listed(slot, present) || Flag(slot, FlagNoGame))
            {
                return false;
            }

            if (Activity(slot) != activity || Number(slot) != number || Status(slot) == StatusNone)
            {
                return false;
            }

            if (instanceObject < 0)
            {
                return true;
            }

            int k = InstanceObject(slot);
            return k < 0 || k == instanceObject;
        }
    }
}
