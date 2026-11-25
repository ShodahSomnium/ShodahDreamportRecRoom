using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    // The contract between the maker rooms' core (rooms, saves, sharing, the room runtime: Places/MakerRooms) and the Maker
    // Pen's build tools (Interact/MakerPen). The tools never touch saves or the network: they call RrMakerEdits, and the
    // room runtime applies each edit locally, sends it to everyone in the room and keeps it for the next save.
    // Rec Room's model (DevNotes/research/rr_maker_rooms.md): a room is a base map (template) plus a list of objects - props
    // (install prefabs by name), Maker Pen shapes and ink - each with a transform, colour, physics mode, frozen and grabbable.

    /// <summary>What a maker-room object is.</summary>
    public enum RrMakerKind : byte
    {
        /// <summary>An install prefab, by name (Asset), e.g. "(Sandbox_Chair)".</summary>
        Prop = 1,

        /// <summary>A Maker Pen shape (Asset = shape name: "Cube", "Sphere", "Cylinder", "Wedge", "Pyramid", ...).</summary>
        Shape = 2,

        /// <summary>A Maker Pen ink tube (Points, Width).</summary>
        Ink = 3,

        /// <summary>A light (Asset = "Point" or "Spot"; Color, Scale.x = range, Scale.y = intensity).</summary>
        Light = 4,

        /// <summary>A text sign (Text, Color; Scale = size).</summary>
        Text = 5
    }

    /// <summary>Rec Room's physics modes for maker objects.</summary>
    public enum RrMakerPhysics : byte
    {
        /// <summary>No collision (the default for ink and decorations).</summary>
        Decoration = 0,

        /// <summary>Solid, static (walls, floors).</summary>
        Environment = 1,

        /// <summary>A dynamic rigidbody (falls, can be thrown when grabbable).</summary>
        Physical = 2,

        /// <summary>Static until grabbed, stays where it is let go.</summary>
        Sticky = 3
    }

    /// <summary>
    /// One object of a maker room. Ids are unique per room: (author player id &lt;&lt; 24) | counter, or loaded ids.
    /// Conventions of the core (DevNotes/iter14/makerrooms_core.md, "Conventions"): Position / Rotation are room-local, which
    /// is world space (rooms stand at the origin) and exactly the local pose of the container <see cref="IRrMakerRoomHost.Find"/>
    /// returns; Scale is that container's localScale (Shape: the size in metres of a unit shape centred on Position), except
    /// for a Light, whose Scale holds range / intensity / spot angle.
    /// </summary>
    [Serializable]
    public sealed class RrMakerObject
    {
        public uint Id;
        public RrMakerKind Kind;
        public string Asset = "";
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;

        /// <summary>The container's scale; Light: x = range (m), y = intensity, z = spot angle (degrees, 0 = 60).</summary>
        public Vector3 Scale = Vector3.one;

        public Color32 Color = new Color32(255, 255, 255, 255);
        public byte Material;
        public RrMakerPhysics Physics = RrMakerPhysics.Environment;
        public bool Frozen = true;
        public bool Grabbable;

        /// <summary>Ink: the tube's diameter in metres (radius = Width / 2, at least 2 mm).</summary>
        public float Width;

        /// <summary>Ink: the stroke's points in the container's local space.</summary>
        public List<Vector3> Points;

        public string Text;

        public RrMakerObject Clone()
        {
            var c = (RrMakerObject)MemberwiseClone();
            c.Points = Points != null ? new List<Vector3>(Points) : null;
            return c;
        }
    }

    /// <summary>A room's settings (Rec Room: name, description, privacy, max players, roles).</summary>
    [Serializable]
    public sealed class RrMakerRoomInfo
    {
        /// <summary>Unique id (32 hex characters), made by the creator's client.</summary>
        public string Id = "";

        public string Name = "";
        public string Description = "";

        /// <summary>The base map (RrMakerTemplates key, e.g. "MakerRoom", "Dorm", "Park").</summary>
        public string Template = "";

        /// <summary>Owner: the Somnium user id hash (presence int 6) and display name.</summary>
        public int OwnerHash;

        public string OwnerName = "";

        /// <summary>Co-owners (user id hashes): may build and save too.</summary>
        public List<int> CoOwners = new List<int>();

        /// <summary>Anyone in the room may build (Rec Room's "Everyone" role with the Maker Pen permission).</summary>
        public bool EveryoneCanBuild;

        /// <summary>Listed to everyone (public) or only joinable by the owner and co-owners (private).</summary>
        public bool Public = true;

        public int MaxPlayers = 8;

        /// <summary>Save number, +1 on every save; the highest wins everywhere.</summary>
        public int Version;

        /// <summary>When the version was saved (UTC unix seconds): breaks version ties.</summary>
        public long SavedAt;
    }

    /// <summary>A saved room: its settings and objects (the base map's own pieces are not stored unless edited).</summary>
    [Serializable]
    public sealed class RrMakerRoomData
    {
        public RrMakerRoomInfo Info = new RrMakerRoomInfo();
        public List<RrMakerObject> Objects = new List<RrMakerObject>();
    }

    /// <summary>Network event ids of the maker rooms (the shared catalogue's free range 24-31, RrGameEvents header).</summary>
    public static class RrMakerEvents
    {
        /// <summary>Global: a player's catalogue of rooms they hold (id, name, owner, template, version, public).</summary>
        public const byte Catalog = 24;

        /// <summary>To a player: please send room id at version v (or newer).</summary>
        public const byte Request = 25;

        /// <summary>To a player: one chunk of a room's data.</summary>
        public const byte Chunk = 26;

        /// <summary>Instance: an object created / changed / deleted.</summary>
        public const byte Edit = 27;

        /// <summary>Instance: a late joiner asks for the current state.</summary>
        public const byte AskState = 28;

        /// <summary>Instance: the room's settings changed, or it was saved (new version).</summary>
        public const byte Meta = 29;

        public const byte Spare30 = 30;
        public const byte Spare31 = 31;
    }

    /// <summary>What the room runtime offers the build tools (implemented by the maker room core).</summary>
    public interface IRrMakerRoomHost
    {
        /// <summary>The local player may build here (owner, co-owner, or everyone-can-build).</summary>
        bool CanEdit { get; }

        RrMakerRoomInfo Info { get; }

        /// <summary>Adds an object (Id 0 = assign one); returns its id. Applied locally at once, sent to the room.</summary>
        uint Create(RrMakerObject o);

        /// <summary>Changes an object (by Id): transform, colour, physics, frozen, grabbable, points.</summary>
        void Update(RrMakerObject o);

        void Delete(uint id);

        /// <summary>The object's data (a copy), or null.</summary>
        RrMakerObject Get(uint id);

        /// <summary>The built GameObject of an object, or null.</summary>
        GameObject Find(uint id);

        /// <summary>The object id a collider or renderer belongs to (0 = none).</summary>
        uint IdOf(Component c);

        IEnumerable<uint> Ids { get; }

        /// <summary>Objects that can still be made before the room is full (Rec Room's ink, as a count; added for the palette's bar).</summary>
        int Remaining { get; }

        /// <summary>Saves the room now (owner/co-owner): a new version, written locally and announced.</summary>
        bool Save();
    }

    /// <summary>The build tools' entry point: the current maker room's host, or null outside a maker room.</summary>
    public static class RrMakerEdits
    {
        /// <summary>Set by the room runtime while the player is in a maker room; null elsewhere.</summary>
        public static IRrMakerRoomHost Host { get; set; }

        /// <summary>Raised after any object was created, changed or deleted (by anyone): the id.</summary>
        public static event Action<uint> Changed;

        public static bool InMakerRoom
        {
            get { return Host != null; }
        }

        public static bool CanEdit
        {
            get { return Host != null && Host.CanEdit; }
        }

        /// <summary>The room runtime calls this after applying an edit.</summary>
        public static void RaiseChanged(uint id)
        {
            var h = Changed;
            if (h != null)
            {
                h(id);
            }
        }

        /// <summary>RrMain shutdown.</summary>
        public static void Reset()
        {
            Host = null;
            Changed = null;
        }
    }
}
