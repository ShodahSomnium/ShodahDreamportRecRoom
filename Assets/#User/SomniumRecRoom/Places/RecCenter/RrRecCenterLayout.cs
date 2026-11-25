using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>How a Rec Room prefab is spawned in the hub.</summary>
    internal enum RrRcKind : byte
    {
        /// <summary>Furniture and architecture: layer 23, kinematic (RrSpawnOptions.Scenery).</summary>
        Scenery,

        /// <summary>No physics at all (lights hanging from the ceiling, wall posters).</summary>
        Visual,

        /// <summary>A local pick-up prop (RrTools.MakeTool), not shared: mugs, plates, food.</summary>
        Tool,

        /// <summary>A shared pick-up prop: MakeTool + RrNetBody with a unique LocalPropId.</summary>
        NetTool
    }

    /// <summary>Where the given position sits on the spawned prefab.</summary>
    internal enum RrRcPlace : byte
    {
        /// <summary>Renderer bounds bottom at Pos.y (floor contact or counter top), pivot x/z at Pos.</summary>
        Snap,

        /// <summary>The prefab root at Pos exactly (hoops, hanging pendants, the mirror).</summary>
        Pivot,

        /// <summary>The renderer bounds centre at Pos (wall boards and posters).</summary>
        Centre
    }

    /// <summary>One prefab of the hub (room frame: +Z from the entrance toward the far end, +X to the right).</summary>
    internal struct RrRcItem
    {
        public string Prefab;
        public Vector3 Pos;
        public float Yaw;
        public RrRcKind Kind;
        public RrRcPlace Place;

        /// <summary>Keep the prefab root's own rotation after the yaw (ping-pong table: its design needs it).</summary>
        public bool KeepRootRotation;

        /// <summary>LocalPropId for NetTool (unique within the Rec Center), else -1.</summary>
        public int NetId;

        /// <summary>What the room wires onto it after spawning ("bell", "radio.kiosk", "hoop", ...), or null.</summary>
        public string Role;

        public RrRcItem(string prefab, float x, float y, float z, float yaw, RrRcKind kind = RrRcKind.Scenery, RrRcPlace place = RrRcPlace.Snap, string role = null, int netId = -1, bool keepRoot = false)
        {
            Prefab = prefab;
            Pos = new Vector3(x, y, z);
            Yaw = yaw;
            Kind = kind;
            Place = place;
            Role = role;
            NetId = netId;
            KeepRootRotation = keepRoot;
        }
    }

    /// <summary>A door of the final Rec Center (doors agent's <see cref="RrCategoryDoor"/>): floor centre + facing.</summary>
    internal struct RrRcDoorSpot
    {
        public RrDoorCategory Category;
        public bool Magic;
        public Vector3 Pos;
        public float Yaw;

        public RrRcDoorSpot(RrDoorCategory category, bool magic, float x, float y, float z, float yaw)
        {
            Category = category;
            Magic = magic;
            Pos = new Vector3(x, y, z);
            Yaw = yaw;
        }
    }

    /// <summary>
    /// The final (July 2025, pre-shutdown) Rec Center, in the room frame of rr_reccenter_cache.md §1.1: origin = the
    /// kiosk's floor centre, the Dorm entrance at -Z looking +Z, +X to the right, main floor y 0. The numbers are
    /// read from the cached Studio scene (about 1 cm) unless marked GUESS:
    /// <list type="bullet">
    /// <item>main hall 25.4 x 33.4 m: left wall (balcony back wall) x -13.9, right wall x 11.5, entrance wall
    /// z -15.6, far end a curve from (11.5, 7.3) to (-1.5, 17.8); timber beams' underside y 8.4, ceiling y 9.0;</item>
    /// <item>upstairs balcony on the left at y 3.10, x -13.9..-9.4, z -15.6..8.7, front stairs along the entrance wall,
    /// back stairs at its far end;</item>
    /// <item>the gym at the back left (hoop wall x -18, SPORTS & PVP on its back wall), the theatre behind the stage
    /// opening in the right wall (z -8.7..-1.8, floor -0.79, QUESTS on its far wall x 21.5), the left wing with the
    /// cafe downstairs and an upper floor (FEATURED ROOMS at x -24.9), the creative door on the balcony wall
    /// (MakerWorld's spot, the user calls it CREATIVE CAMPUS), and the hallway to the Dorm door at z -25.1.</item>
    /// </list>
    /// </summary>
    internal static class RrRecCenterLayout
    {
        public const float Wall = 0.3f;

        // Main hall (inner faces).
        public const float HallLeft = -13.9f;
        public const float HallRight = 11.5f;
        public const float Entrance = -15.6f;
        public const float BackLeftZ = 13.0f;
        public const float BeamBottom = 8.4f;
        public const float CeilingY = 9.0f;

        /// <summary>GUESS: the roof over the balcony rises to the left wall (posts at x -13.8 reach 12.0-12.2, the 3 clerestory windows sit at 9.53-10.86).</summary>
        public const float SlopeTop = 11.9f;

        /// <summary>The far end's curve: a circle tangent to the right wall at (11.5, 7.3), through (-1.5, 17.8) (fit to the curved windows, GUESS radius).</summary>
        public static readonly Vector2 CurveCenter = new Vector2(0.76f, 7.3f);

        public const float CurveRadius = 10.74f;

        /// <summary>Angle (deg, 0 = +X, toward +Z) where the curve meets x -1.5.</summary>
        public static readonly float CurveEnd = Mathf.Acos((-1.5f - 0.76f) / 10.74f) * Mathf.Rad2Deg;

        // Windows in the curve (angles fitted to Window_Curved_Wide X 6.25..11.67 and _Narrow X -0.61..4.28).
        public const float WideFrom = 15f;
        public const float WideTo = 56f;
        public const float NarrowFrom = 71f;
        public const float NarrowTo = 97f;
        public const float CurveSill = 0.55f;
        public const float CurveWindowTop = 5.7f;

        // Skylights (Window_Skylight): X -3.30..3.48, glass y 9.25..9.57.
        public const float SkyX0 = -3.30f;
        public const float SkyX1 = 3.48f;
        public static readonly Vector2 SkyFront = new Vector2(-9.57f, -3.45f);
        public static readonly Vector2 SkyBack = new Vector2(4.42f, 10.54f);

        // Balcony.
        public const float BalconyY = 3.10f;
        public const float BalconyUnder = 2.90f;
        public const float BalconyEdge = -9.4f;
        public const float BalconyEnd = 8.7f;
        public const float BalconyExtEnd = 11.2f;
        public const float RailTop = 4.15f;

        // Front stairs: along the entrance wall, climbing toward -X from x -3.9 (y 0) to the balcony edge.
        public const float FrontStairLow = -3.9f;
        public const float FrontStairZ1 = -12.9f;

        // Back stairs (GUESS layout inside the measured stairwell): climbing toward -X along the gym's front wall.
        public const float BackStairLow = -7.0f;
        public const float BackStairHigh = -12.7f;

        // Gym (floor kept at 0: the scene's gym floor is 0.2 higher; GUESS to avoid a step).
        public const float GymLeft = -18.0f;
        public const float GymRight = -1.8f;
        public const float GymFront = 13.3f;
        public const float GymBack = 25.9f;
        public const float GymCeiling = 6.5f;
        public const float GymOpening0 = -7.0f;

        // Theatre (sunk 0.79 m behind the stage opening).
        public const float StageZ0 = -8.7f;
        public const float StageZ1 = -1.8f;
        public const float StageTop = 4.4f;
        public const float TheatreX0 = 11.8f;
        public const float TheatreX1 = 21.6f;
        public const float TheatreZ0 = -10.3f;
        public const float TheatreZ1 = 1.8f;
        public const float TheatreFloor = -0.79f;
        public const float TheatreCeiling = 5.5f;
        public const float TheatreStepsEnd = 13.4f;

        // Left wing: cafe downstairs, an upper floor over its front part.
        public const float WingLeft = -25.0f;
        public const float WingRight = -14.2f;
        public const float WingFront = -15.6f;
        public const float WingBack = 5.0f;
        public const float WingRoof = 8.4f;
        public const float CafeBackWall = -7.4f;
        public const float UpperEdge = -5.9f;
        public const float CafeCounterTop = 0.95f;

        /// <summary>
        /// The cafe's outer corner is a curved bay window (Window_Cafe AABB x -25.18..-18.32, z -3.60..4.72, y 0.58..5.93;
        /// bushes stand just outside it at (-24.29, 2.01) and (-21.35, 5.04)): a quarter ellipse from the far-left wall at
        /// z -3.6 to the back wall at x -17.6 (GUESS centre and radii fitted to that box).
        /// </summary>
        public static readonly Vector2 CafeBayCenter = new Vector2(-17.6f, -3.6f);

        public const float CafeBayRx = 7.4f;
        public const float CafeBayRz = 8.6f;
        public const float CafeBaySill = 0.58f;
        public const float CafeBayTop = 5.93f;

        // Hallway to the Dorm.
        public const float HallwayX0 = -2.1f;
        public const float HallwayX1 = 0.8f;
        public const float HallwayEnd = -25.1f;
        public const float HallwayCeiling = 3.1f;

        /// <summary>Kiosk centre (the four posts' centre, measured) and the front counter's top.</summary>
        public static readonly Vector3 Kiosk = new Vector3(-0.07f, 0f, 0.1f);

        public const float KioskCounterTop = 1.05f;

        /// <summary>Hanging cafe pendants (pivot at the lamp's top), GUESS positions over the tables.</summary>
        public static readonly Vector3[] CafePendants = { new Vector3(-20.6f, 5.2f, -0.6f), new Vector3(-17.8f, 5.2f, 1.6f), new Vector3(-22.4f, 5.2f, 2.2f) };

        /// <summary>
        /// Arrival spots for players coming from the Dorm or the world: in the Dorm hallway just short of the hall,
        /// facing +Z, so the first view is the rec.net picture's (the kiosk and the REC ROOM sign ahead).
        /// </summary>
        public static readonly Vector3[] HallwaySpawns =
        {
            new Vector3(-0.65f, 0f, -16.6f), new Vector3(-1.45f, 0f, -16.6f), new Vector3(0.15f, 0f, -16.6f),
            new Vector3(-0.65f, 0f, -17.6f), new Vector3(-1.45f, 0f, -17.6f), new Vector3(0.15f, 0f, -17.6f)
        };

        /// <summary>The Dorm door at the hallway's end (sign "DORM ROOM" at -0.64, 2.47, -25.12).</summary>
        public static readonly Vector3 DormDoor = new Vector3(-0.65f, 0f, -25.04f);

        /// <summary>
        /// Category doors at the measured door-sign spots (sign centre 2.5-2.95 m above the door's floor), 0.06 m
        /// in front of the wall (doors.md), with the July 2025 scene's own signs (user decision 2026-09-24): SPORTS &
        /// PVP in the gym, QUESTS in the theatre, FEATURED ROOMS upstairs in the wing, MAKERWORLD on the balcony wall
        /// and CAMPUS JAMS at the coral-framed door under the balcony's end (frame X -9.28..-8.97, Z 7.91..10.24).
        /// The Magic Door is in no sign or object of the July 2025 scene; it stays as a Dreamport door on the blue
        /// wall under the balcony, between the front lockers and the cafe openings (GUESS).
        /// </summary>
        public static readonly RrRcDoorSpot[] Doors =
        {
            new RrRcDoorSpot(RrDoorCategory.SportsPvp, false, -7.45f, 0f, GymBack - 0.06f, 180f),
            new RrRcDoorSpot(RrDoorCategory.Quests, false, TheatreX1 - 0.06f, TheatreFloor, -4.36f, 270f),
            new RrRcDoorSpot(RrDoorCategory.MakerWorld, false, HallLeft + 0.06f, BalconyY, 5.60f, 90f),
            new RrRcDoorSpot(RrDoorCategory.FeaturedRooms, false, WingLeft + 0.06f, BalconyY, -11.93f, 90f),
            new RrRcDoorSpot(RrDoorCategory.CampusJams, false, -9.39f, 0f, 9.08f, 90f),
            new RrRcDoorSpot(RrDoorCategory.FeaturedRooms, true, HallLeft + 0.06f, 0f, -6.5f, 90f)
        };

        /// <summary>
        /// Our props that stay when Rec Room's own building is loaded from the player's cache (iteration 6): everything
        /// people use - the loose and shared toys, the bell, the radios (and the table the lounge radio stands on), the
        /// hoops, ping-pong, the vending machine, the Friend-O-Tron, the board and the Puzzles & Art boards.
        /// The furniture the building already has (couches, chairs, side tables, stools, pendants, plants, lockers, the
        /// punching bag, the welcome mat and the code of conduct poster) is left out. So is the hallway mirror
        /// (iteration 12): that building's hallway walls are plain, its mirror is the theatre's frame
        /// (<see cref="TheatreMirrorPos"/>).
        /// </summary>
        public static bool KeepWithCachedBuilding(RrRcItem item)
        {
            if (item.Kind == RrRcKind.Tool || item.Kind == RrRcKind.NetTool)
            {
                return true;
            }

            switch (item.Role)
            {
                case "bell":
                case "radio.kiosk":
                case "radio.cafe":
                case "radio.lounge":
                case "radio.table":
                case "hoop":
                case "pingpong.table":
                case "vending":
                case "friendotron":
                case "board":
                    return true;
            }

            return item.Prefab == "(MoveableWhiteboard)" || item.Prefab == "(ArtCanvas)";
        }

        /// <summary>
        /// The Magic Door when Rec Room's own building is loaded: the rebuild's spot (x -13.9, z -6.5) is an opening from
        /// the hall into the cafe there, so it goes on the entrance wall right of the hallway, facing the hall, where
        /// players arriving from the Dorm see it (the wall's painted face is at z -15.50: its poster hangs at -15.48).
        /// GUESS spot: the July 2025 scene has no Magic Door.
        /// </summary>
        public static readonly RrRcDoorSpot MagicWithCachedBuilding = new RrRcDoorSpot(RrDoorCategory.FeaturedRooms, true, 2.6f, 0f, -15.44f, 0f);

        /// <summary>
        /// The cached building's one mirror frame (iteration 12, rc_mirrors.md): the theatre's dark-wood frame on the wall
        /// between curtains (2) and (3), part of the building's walls mesh (Wood_Horizontal_DarkBrown_3x_Mat). The frame
        /// face is at z 1.383; the opening is x 15.122..18.958, y -0.259..1.849 (3.84 x 2.11 m, landscape, 0.53..2.64 m
        /// above the theatre floor), a 0.23 m deep recess with red brick at its back (z 1.611). Rec Room's SANDBOX_MIRROR
        /// stood there on its side (the 2019 and 2020 room exports); the July 2025 room data has no mirror. The glass
        /// centre sits 5 mm in front of the brick and faces -z, into the theatre.
        /// </summary>
        public static readonly Vector3 TheatreMirrorPos = new Vector3(17.040f, 0.795f, 1.606f);

        /// <summary>The theatre glass faces -z: the mirror's +z turned half round.</summary>
        public const float TheatreMirrorYaw = 180f;

        /// <summary>
        /// The recess at its back is 3.829 x 2.108 m; the glass runs 3 mm into the dark-wood reveals all round (hidden
        /// there), as a glass 1 cm smaller left a line of brick showing round it (harness, iteration 12).
        /// </summary>
        public const float TheatreMirrorWidth = 3.835f;

        public const float TheatreMirrorHeight = 2.115f;

        /// <summary>From the couches at the back of the theatre (z -8.1, 9.7 m in front of the glass).</summary>
        public const float TheatreMirrorRange = 10f;

        /// <summary>Layout changes for Rec Room's own building (applied to kept items by the room).</summary>
        public static RrRcItem AdjustForCachedBuilding(RrRcItem item)
        {
            switch (item.Prefab)
            {
                // The building's pastry cases stand on the counter where the rebuild's cakes were (x -21.25..-17.63).
                case "(FoodConsumable_RedVelvetCake)":
                    item.Pos.x = -16.8f;
                    break;
                case "(Consumables_Cake_Chocolate_Tray)":
                    item.Pos.x = -15.3f;
                    break;
            }

            // The second hoop: on the gym's right wall, but clear of the building's punching bag (z 19.26) under it.
            if (item.Role == "hoop" && item.Pos.x > GymLeft + 5f)
            {
                item.Pos.z = 22.0f;
            }

            return item;
        }

        public static List<RrRcItem> Items()
        {
            const RrRcKind S = RrRcKind.Scenery;
            const RrRcKind V = RrRcKind.Visual;
            const RrRcKind T = RrRcKind.Tool;
            const RrRcKind N = RrRcKind.NetTool;
            const RrRcPlace Pv = RrRcPlace.Pivot;
            const RrRcPlace C = RrRcPlace.Centre;
            var l = new List<RrRcItem>(128);
            float top = KioskCounterTop;

            // A. Kiosk: the reception bell mid-counter facing the entrance, the kiosk radio at its right end.
            l.Add(new RrRcItem("(Sandbox_Bell)", Kiosk.x, top, -2.62f, 180f, S, role: "bell"));
            l.Add(new RrRcItem("(Radio)", 1.75f, top, -2.62f, 200f, S, role: "radio.kiosk"));

            // B. Gym: two hoops facing each other across the gym (the scene's painted key is on the x -18 wall,
            // Rec Room had two hoops since Jan 2019), basketballs, dodgeballs, the punching bag, the vending machine.
            l.Add(new RrRcItem("(Basketballhoop)", GymLeft + 0.05f, 3.085f, 19.5f, 90f, S, Pv, "hoop"));
            l.Add(new RrRcItem("(Basketballhoop)", GymRight - 0.05f, 3.085f, 19.5f, 270f, S, Pv, "hoop"));
            l.Add(new RrRcItem("(Basketball)", -10.3f, 0f, 15.2f, 0f, N, netId: 1));
            l.Add(new RrRcItem("(Basketball)", -9.8f, 0f, 15.2f, 0f, N, netId: 2));
            l.Add(new RrRcItem("(Basketball)", -10.3f, 0f, 15.7f, 0f, N, netId: 3));
            l.Add(new RrRcItem("(Basketball)", -9.8f, 0f, 15.7f, 0f, N, netId: 4));
            l.Add(new RrRcItem("(DodgeballBall)", -5.2f, 0f, 21.0f, 0f, N, netId: 5));
            l.Add(new RrRcItem("(DodgeballBall)", -4.7f, 0f, 21.4f, 0f, N, netId: 6));
            l.Add(new RrRcItem("(DodgeballBall)", -5.4f, 0f, 21.6f, 0f, N, netId: 7));
            l.Add(new RrRcItem("(PunchingBag)_SpawnableTool", -16.6f, 0f, 24.4f, 127f, S));
            l.Add(new RrRcItem("(VendingMachine)_SpawnableTool", GymRight - 0.6f, 0f, 24.0f, 270f, S, role: "vending"));
            l.Add(new RrRcItem("(WaterBottle)", -3.4f, 0f, 25.2f, 0f, T));
            l.Add(new RrRcItem("(WaterBottle)", -3.15f, 0f, 25.35f, 30f, T));

            // C. Ping-pong: two tables in the hall's back-left, between the kiosk rug and the back stairs (GUESS spot:
            // the tables are room data in the scene; the rec.net picture shows open floor in the front), long axis
            // along X (root rotation kept), 2 paddles and a ball each.
            Vector2[] pp = { new Vector2(-5.8f, 6.4f), new Vector2(-3.4f, 9.7f) };
            for (int i = 0; i < 2; i++)
            {
                var c = pp[i];
                l.Add(new RrRcItem("(Sandbox_Table_PingPong)", c.x, 0f, c.y, 0f, S, role: "pingpong.table", keepRoot: true));
                l.Add(new RrRcItem("(Pingpong_Paddle)", c.x - 1.1f, 0.78f, c.y, 90f, N, role: "pingpong.paddle", netId: 10 + i * 2));
                l.Add(new RrRcItem("(Pingpong_Paddle)", c.x + 1.1f, 0.78f, c.y, 270f, N, role: "pingpong.paddle", netId: 11 + i * 2));
                l.Add(new RrRcItem("(PingpongBall)", c.x, 0.80f, c.y + 0.3f, 0f, N, role: "pingpong.ball", netId: 8 + i));
            }

            // D. Lounge by the curved windows (measured couches, chairs and side tables; `RecCenter_Chair` is not
            // in the install, so the couch chair stands in), frisbees on the side tables.
            l.Add(new RrRcItem("(RecCenter_Couch_Sandbox)Variant", 1.13f, 0f, 16.95f, 279f, S, role: "couch"));
            l.Add(new RrRcItem("(RecCenter_Couch_Sandbox)Variant", 6.82f, 0f, 13.43f, 224f, S, role: "couch"));
            l.Add(new RrRcItem("(Sandbox_CouchChair)", 1.89f, 0f, 14.61f, 30f));
            l.Add(new RrRcItem("(Sandbox_CouchChair)", -0.9f, 0f, 16.2f, 229f));
            l.Add(new RrRcItem("(Sandbox_CouchChair)", 7.62f, 0f, 10.33f, 99f));
            l.Add(new RrRcItem("(Sandbox_CouchChair)", 9.42f, 0f, 11.32f, 39f));
            l.Add(new RrRcItem("(Sandbox_Table_Short)", 8.04f, 0f, 12.40f, 141f));
            l.Add(new RrRcItem("(Sandbox_Table_Short)", 0.31f, 0f, 15.54f, 193f));
            l.Add(new RrRcItem("(DiscGolfDisc)", 8.04f, 0.44f, 12.40f, 0f, N, netId: 14));
            l.Add(new RrRcItem("(DiscGolfDisc)", 0.1f, 0.44f, 15.5f, 0f, N, netId: 15));
            l.Add(new RrRcItem("(DiscGolfDisc)", 0.5f, 0.44f, 15.6f, 40f, N, netId: 16));
            l.Add(new RrRcItem("(Sandbox_Houseplant_Dracaena)", 3.8f, 0f, 16.6f, 0f));
            l.Add(new RrRcItem("(Sandbox_Friendotron)", 4.75f, 0f, 15.81f, 205f, S, role: "friendotron"));

            // E. Community board on the blue wall under the balcony (the picture's notice board; the scene's stands
            // angled at x -12.9..-10.3, z 4.8..6.7), filled with our notes by the room.
            l.Add(new RrRcItem("(BulletinBoard_Sandbox)Variant", HallLeft + 0.06f, 1.45f, 6.4f, 0f, S, C, "board"));

            // F. Cafe (left wing): coffee, mugs, plates and cakes on the counter, the cafe radio, stools, 5 tables
            // at the measured spots with 2 chairs each, pendants over them.
            float ct = CafeCounterTop;
            l.Add(new RrRcItem("(RecCenter_Cafe_CoffeePot)", -16.2f, ct, -5.0f, 0f, T));
            l.Add(new RrRcItem("(RecCenter_Cafe_CoffeePot)", -15.75f, ct, -5.0f, 0f, T));
            for (int i = 0; i < 6; i++)
            {
                l.Add(new RrRcItem("(RecCenter_Cafe_Mug)", -23.9f + i * 0.28f, ct, -4.85f, 180f + i * 23f, T));
            }

            for (int i = 0; i < 4; i++)
            {
                l.Add(new RrRcItem("(RecCenter_Cafe_Plate)", -22.0f, ct + i * 0.03f, -5.2f, 0f, T));
            }

            l.Add(new RrRcItem("(Consumables_Donuts_OpenBox)", -17.3f, ct, -5.1f, 0f, T));
            l.Add(new RrRcItem("(FoodConsumable_RedVelvetCake)", -19.4f, ct, -5.15f, 0f, T));
            l.Add(new RrRcItem("(Consumables_Cake_Chocolate_Tray)", -20.6f, ct, -5.15f, 0f, T));
            l.Add(new RrRcItem("(Radio)", -14.85f, ct, -5.25f, 0f, S, role: "radio.cafe"));
            float[] stools = { -23.6f, -22.5f, -16.4f, -15.3f };
            for (int i = 0; i < stools.Length; i++)
            {
                l.Add(new RrRcItem("(Stool_Wood_Round_Sandbox)Variant", stools[i], 0f, -3.95f, 0f));
            }

            Vector3[] tables = { new Vector3(-19.93f, -1.65f, 90f), new Vector3(-17.03f, 0.03f, 124f), new Vector3(-21.18f, 1.62f, 72f), new Vector3(-18.23f, 3.07f, 97f), new Vector3(-23.30f, -1.45f, 90f) };
            for (int i = 0; i < tables.Length; i++)
            {
                var c = tables[i];
                l.Add(new RrRcItem("(Table_Cafe_Short_Sandbox)Variant", c.x, 0f, c.y, c.z));
                var r = Quaternion.Euler(0f, c.z, 0f) * Vector3.right;
                l.Add(new RrRcItem("(Chair_Cafe_Round_Sandbox)Variant", c.x + r.x * 0.95f, 0f, c.y + r.z * 0.95f, c.z + 270f));
                l.Add(new RrRcItem("(Chair_Cafe_Round_Sandbox)Variant", c.x - r.x * 0.95f, 0f, c.y - r.z * 0.95f, c.z + 90f));
            }

            for (int i = 0; i < CafePendants.Length; i++)
            {
                var p = CafePendants[i];
                l.Add(new RrRcItem("(Light_Pendant_Sandbox)Variant", p.x, p.y, p.z, 0f, V, Pv, "pendant"));
            }

            // G. Upper floor of the wing: a lounge along the front wall (measured couches at z -14.92, a table with
            // the third radio), and the Puzzles & Art corner (GUESS spot) at the back by the railing.
            float up = BalconyY;
            l.Add(new RrRcItem("(RecCenter_Couch_Sandbox)Variant", -17.03f, up, -14.92f, 90f, S, role: "couch"));
            l.Add(new RrRcItem("(RecCenter_Couch_Sandbox)Variant", -21.53f, up, -14.92f, 90f, S, role: "couch"));
            l.Add(new RrRcItem("(Sandbox_Table_Short)", -19.25f, up, -14.3f, 180f, S, role: "radio.table"));
            l.Add(new RrRcItem("(Radio)", -19.25f, up + 0.44f, -14.3f, 0f, S, role: "radio.lounge"));
            l.Add(new RrRcItem("(MoveableWhiteboard)", -24.3f, up, -9.3f, 90f));
            l.Add(new RrRcItem("(MoveableWhiteboard)", -21.6f, up, -6.9f, 180f));
            l.Add(new RrRcItem("(ArtCanvas)", -15.6f, up, -9.9f, 270f));
            l.Add(new RrRcItem("(ArtCanvas)", -15.6f, up, -8.3f, 270f));
            l.Add(new RrRcItem("(Sandbox_Table_Short)", -19.3f, up, -8.6f, 0f));
            l.Add(new RrRcItem("(SandboxMarkerSet)", -19.3f, up + 0.44f, -8.6f, 0f, T));
            l.Add(new RrRcItem("(DrawableClipboard)", -19.75f, up + 0.44f, -8.5f, 10f, T));
            l.Add(new RrRcItem("(DrawableClipboard)", -18.85f, up + 0.44f, -8.7f, -15f, T));
            l.Add(new RrRcItem("(Sandbox_Houseplant_Dracaena)", -14.8f, up, -7.2f, 90f));
            l.Add(new RrRcItem("(Sandbox_Houseplant_Fig)", -24.4f, up, -15.0f, 90f));
            l.Add(new RrRcItem("(Sandbox_Houseplant_SnakePlant)", -14.7f, up, -15.1f, 90f));

            // H. Theatre: couches facing its little stage (measured), figs, the rug is procedural.
            float tf = TheatreFloor;
            l.Add(new RrRcItem("(RecCenter_Couch_Sandbox)Variant", 15.37f, tf, -8.05f, 90f, S, role: "couch"));
            l.Add(new RrRcItem("(RecCenter_Couch_Sandbox)Variant", 18.88f, tf, -8.11f, 90f, S, role: "couch"));
            l.Add(new RrRcItem("(Sandbox_Houseplant_Fig)", 13.28f, tf, 0.9f, 268f));
            l.Add(new RrRcItem("(Sandbox_Houseplant_Fig)", 12.96f, tf, -9.67f, 0f));

            // I. Hallway: the mirror (Jan 2019 "Puzzles & Art and a Mirror"), welcome mat, code of conduct.
            l.Add(new RrRcItem("(Dorm_FreeMirror)", HallwayX0 + 0.05f, 1.35f, -20.6f, 90f, S, Pv, "mirror"));
            l.Add(new RrRcItem("(SandboxWelcomeMat)", -0.65f, 0f, -24.4f, 0f));
            l.Add(new RrRcItem("(Sandbox_CodeOfConduct_Poster)", HallwayX1 - 0.03f, 1.6f, -20.6f, 270f, V, C));

            // K. Lockers: the July 2025 scene's six [Lockers] (rr_reccenter_cache.md 3, appendix A 275-278 and the
            // three by the back stairs), kept by the user's decision (2026-09-24). Spawned from the install's
            // (Sandbox_Lockers), which carries the same Lockers_LOD meshes; snapped to the floor at the scene's yaw.
            l.Add(new RrRcItem("(Sandbox_Lockers)", -15.84f, 0f, 13.49f, 0f, S, role: "lockers"));
            l.Add(new RrRcItem("(Sandbox_Lockers)", -13.71f, 0f, 13.49f, 0f, S, role: "lockers"));
            l.Add(new RrRcItem("(Sandbox_Lockers)", -11.58f, 0f, 13.49f, 0f, S, role: "lockers"));
            l.Add(new RrRcItem("(Sandbox_Lockers)", -17.18f, 0f, 24.31f, 90f, S, role: "lockers"));
            l.Add(new RrRcItem("(Sandbox_Lockers)", -11.98f, 0f, -8.36f, 43f, S, role: "lockers"));
            l.Add(new RrRcItem("(Sandbox_Lockers)", -10.41f, 0f, -9.80f, 43f, S, role: "lockers"));

            // J. Hall plants (measured spots).
            l.Add(new RrRcItem("(Sandbox_Houseplant_Fig)", 5.61f, 0f, -15.01f, 0f));
            l.Add(new RrRcItem("(Sandbox_Houseplant_Fig)", 11.0f, 0f, -0.9f, 205f));
            l.Add(new RrRcItem("(Sandbox_Houseplant_SnakePlant)", -8.65f, 0f, -12.27f, 90f));
            l.Add(new RrRcItem("(Sandbox_Houseplant_Fig)", -12.9f, 0f, 3.0f, 0f));
            return l;
        }
    }
}
