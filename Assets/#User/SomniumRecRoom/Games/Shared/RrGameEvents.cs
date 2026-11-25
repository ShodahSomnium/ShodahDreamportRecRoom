namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Event type ids for <see cref="Net.RrNet.Send"/>. Events are scoped to the sender's instance, and one
    /// instance runs one activity, so games may reuse the per-game range freely.
    ///
    ///   1-31   network/shared catalogue (somnium_networking.md §5.7): Fire, Hit, Death, Impact, Catch/HitClaim,
    ///          Request, ActorHit, Effect, Toast, PinStream, Hello, Census, Sound
    ///          20-23 Maker Pen (Interact/MakerPen/RrMakerPenWire: EvPoints 20, EvDelete 21, EvResend 22, EvPen 23; it runs in
    ///          every place, so no game may reuse them)
    ///   32-63  the shared game kit (this file)
    ///   64-255 each game's own
    /// </summary>
    public static class RrGameEvents
    {
        public const byte Fire = 1;
        public const byte Hit = 2;
        public const byte Death = 3;
        public const byte Impact = 4;
        public const byte Claim = 5;
        public const byte Request = 6;
        public const byte ActorHit = 7;
        public const byte Effect = 8;
        public const byte Toast = 9;
        public const byte PinStream = 10;
        public const byte Hello = 11;
        public const byte Census = 12;
        public const byte Sound = 13;

        /// <summary>Authority -> all: [playerId u16, team u8]*. Each client applies its own entry.</summary>
        public const byte TeamAssign = 32;

        /// <summary>Player -> authority: [request u8, arg u8] (see <see cref="Requests"/>).</summary>
        public const byte GameRequest = 33;

        /// <summary>Authority -> all: [clip id u16] announcer line, so every member hears it at once.</summary>
        public const byte Announce = 34;

        /// <summary>Authority -> all: [playerId u16, points i16] a scored point (floating "+1" and sound).</summary>
        public const byte Scored = 35;

        public const byte FirstGameSpecific = 64;

        public static class Requests
        {
            public const byte Start = 1;
            public const byte Ready = 2;
            public const byte SwitchTeam = 3;
            public const byte VoteMap = 4;
            public const byte Leave = 5;
        }
    }
}
