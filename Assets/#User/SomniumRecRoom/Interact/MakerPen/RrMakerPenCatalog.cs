using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>The Maker Pen's build modes in a maker room (Rec Room's pen menu tools, basic form).</summary>
    public enum RrMakerMode : byte
    {
        Draw = 0,
        Shapes = 1,
        Props = 2,
        Select = 3,
        Clone = 4,
        Recolor = 5,
        Delete = 6,
        Physics = 7
    }

    /// <summary>What the Physics mode's trigger applies (Rec Room's Configure page, reduced).</summary>
    public enum RrMakerPhysicsBrush : byte
    {
        Decoration = 0,
        Environment = 1,
        Physical = 2,
        Sticky = 3,
        Freeze = 4,
        Grabbable = 5
    }

    /// <summary>A palette entry of the Props page: an install prefab (or a light gadget) and its thumbnail.</summary>
    public sealed class RrMakerPropEntry
    {
        public RrMakerKind Kind = RrMakerKind.Prop;

        /// <summary>Install prefab root name (Kind Prop) or "Point" / "Spot" (Kind Light).</summary>
        public string Asset;

        public string Label;

        /// <summary>The install's palette thumbnail (Texture2D <c>SpawnableToolTex_&lt;Thumb&gt;</c>).</summary>
        public string Thumb;

        /// <summary>Small things you pick up (cups, balls, toys): physical and grabbable, as Rec Room spawns them.</summary>
        public bool Handheld;

        public string ThumbTexture
        {
            get { return RrMakerPenCatalog.ThumbPrefix + Thumb; }
        }
    }

    /// <summary>A palette category (Rec Room's carousels: Furniture, Decor, Kitchen, Sports, Toys...).</summary>
    public sealed class RrMakerPropCategory
    {
        public string Name;
        public readonly List<RrMakerPropEntry> Items = new List<RrMakerPropEntry>();
    }

    /// <summary>A Maker Pen shape: its contract name (the core resolves <c>ExtrusionShape_&lt;Name&gt;</c>) and menu icon.</summary>
    public sealed class RrMakerShapeEntry
    {
        public string Name;
        public string Label;

        /// <summary>A <c>MakerPen_Sprites_*</c> sprite, or null (the label shows instead).</summary>
        public string Icon;
    }

    /// <summary>
    /// The build palette's data: modes, the 12 install shapes, a curated props catalogue in Rec Room's categories,
    /// colours and the physics brush. Everything named here exists in the player's install (checked against the
    /// rrtool index, iter14 makerrooms_tools.md): prefabs are <c>SpawnableTool</c> roots, thumbnails are
    /// <c>SpawnableToolTex_*</c> textures, icons are <c>MakerPen_Sprites_*</c> sprites (atlas MakerPen_Sprites).
    /// Rec Room's prop categories live on its server, so the grouping here is ours (in its carousel names).
    /// </summary>
    public static class RrMakerPenCatalog
    {
        public const string ThumbPrefix = "SpawnableToolTex_";
        public const string IconPrefix = "MakerPen_Sprites_";

        public static readonly string[] ModeNames = { "Draw", "Shapes", "Props", "Select", "Clone", "Recolor", "Delete", "Physics" };

        /// <summary>Menu icon per mode (MakerPen_Sprites_*).</summary>
        public static readonly string[] ModeIcons = { "Tube", "Cube", "Create", "Select", "Clone", "ReColor", "Delete", "Configure" };

        /// <summary>Key and outline colour per mode.</summary>
        public static readonly Color[] ModeColours =
        {
            new Color(0.98f, 0.80f, 0.35f), new Color(0.55f, 0.80f, 0.98f), new Color(0.70f, 0.90f, 0.55f), new Color(0.45f, 0.75f, 1f),
            new Color(0.45f, 0.92f, 0.60f), new Color(0.98f, 0.62f, 0.85f), new Color(1f, 0.38f, 0.33f), new Color(1f, 0.86f, 0.30f)
        };

        public static readonly string[] BrushNames = { "Decoration", "Environment", "Physical", "Sticky", "Freeze", "Grabbable" };

        public static string ModeName(RrMakerMode m)
        {
            int i = (int)m;
            return i >= 0 && i < ModeNames.Length ? ModeNames[i] : m.ToString();
        }

        public static Color ModeColour(RrMakerMode m)
        {
            int i = (int)m;
            return i >= 0 && i < ModeColours.Length ? ModeColours[i] : Color.white;
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>The install's ExtrusionShape_* meshes (bundle 1df8e0ef); the first five are Rec Room's default shapes.</summary>
        public static readonly RrMakerShapeEntry[] Shapes =
        {
            new RrMakerShapeEntry { Name = "Cube", Label = "Cube", Icon = "Cube" },
            new RrMakerShapeEntry { Name = "Sphere", Label = "Sphere", Icon = "Sphere" },
            new RrMakerShapeEntry { Name = "Cylinder", Label = "Cylinder", Icon = "Cylinder" },
            new RrMakerShapeEntry { Name = "Wedge", Label = "Wedge", Icon = "Wedge" },
            new RrMakerShapeEntry { Name = "Pyramid", Label = "Pyramid", Icon = "Pyramid" },
            new RrMakerShapeEntry { Name = "Cone", Label = "Cone" },
            new RrMakerShapeEntry { Name = "Hemisphere", Label = "Dome" },
            new RrMakerShapeEntry { Name = "Donut", Label = "Donut" },
            new RrMakerShapeEntry { Name = "Pipe", Label = "Pipe" },
            new RrMakerShapeEntry { Name = "HalfPipe", Label = "Half Pipe" },
            new RrMakerShapeEntry { Name = "Diamond", Label = "Diamond" },
            new RrMakerShapeEntry { Name = "Mound", Label = "Mound" }
        };

        // ------------------------------------------------------------------ colours

        /// <summary>The swatches (the ink palette, <see cref="RrMakerPenWire.Palette"/>, so a stroke keeps its colour).</summary>
        public static Color[] Colours
        {
            get { return RrMakerPenWire.Palette; }
        }

        public static Color32 Colour32(byte index)
        {
            return RrMakerPenWire.ColourOf(index);
        }

        /// <summary>The swatch nearest to a colour (the readout and eyedropping).</summary>
        public static byte NearestColour(Color32 c)
        {
            var p = RrMakerPenWire.Palette;
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < p.Length; i++)
            {
                Color32 q = p[i];
                float dr = q.r - c.r, dg = q.g - c.g, db = q.b - c.b;
                float d = dr * dr + dg * dg + db * db;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return (byte)best;
        }

        // ------------------------------------------------------------------ props

        private static List<RrMakerPropCategory> _categories;

        /// <summary>Furniture, Decor, Nature, Lights, Kitchen, Sports, Toys, Building - about 100 install props.</summary>
        public static List<RrMakerPropCategory> Categories
        {
            get
            {
                if (_categories == null)
                {
                    _categories = Build();
                }

                return _categories;
            }
        }

        public static int PropCount
        {
            get
            {
                int n = 0;
                foreach (var c in Categories)
                {
                    n += c.Items.Count;
                }

                return n;
            }
        }

        private static List<RrMakerPropCategory> Build()
        {
            var list = new List<RrMakerPropCategory>();
            var c = Category(list, "Furniture");
            Add(c, "(Sandbox_Couch)", "Couch", "COUCH");
            Add(c, "(Sandbox_CouchChair)", "Armchair", "COUCHCHAIR");
            Add(c, "(Chair_Moveable)", "Chair", "CHAIR");
            Add(c, "(Chair_Beanbag_Sandbox)Variant", "Beanbag", "BEAN_BAG");
            Add(c, "(Chair_Cafe_Round_Sandbox)Variant", "Cafe Chair", "CAFE_CHAIR_ROUND");
            Add(c, "(LawnChair_Sandbox)Variant", "Lawn Chair", "LAWN_CHAIR");
            Add(c, "(Sandbox_Stool)", "Stool", "STOOL");
            Add(c, "(BowlingAlley_Stool)SandboxVariant", "Bar Stool", "STOOL_BOWLING_ALLEY");
            Add(c, "(Sandbox_Table_Short)", "Table", "TABLE_SHORT");
            Add(c, "(Sandbox_Table_Tall)", "Tall Table", "TABLE_TALL");
            Add(c, "(Sandbox_Table_Coffee_Large)", "Coffee Table", "TABLE_COFFEE_LARGE");
            Add(c, "(Table_Cafe_Short_Sandbox)Variant", "Cafe Table", "CAFE_TABLE_SHORT");
            Add(c, "(Table_Picnic_Sandbox)Variant", "Picnic Table", "PICNIC_TABLE");
            Add(c, "(Umbrella_Table_Sandbox)Variant", "Umbrella", "TABLE_UMBRELLA");
            Add(c, "(Booth_Diner_Sandbox)Variant", "Booth Seat", "BOOTH_SEAT");
            Add(c, "(Table_Diner_Sandbox)Variant", "Booth Table", "BOOTH_TABLE");
            Add(c, "(Sandbox_Shelves_Tall)", "Bookshelf", "BOOKSHELF_TALL");
            Add(c, "(Dorm_Bunk_Bed)", "Bunk Bed", "DORM_BUNK_BED");

            c = Category(list, "Decor");
            Add(c, "(Sandbox_Houseplant_Dracaena)", "Dracaena", "PLANT_DRACAENA");
            Add(c, "(Sandbox_Houseplant_Fig)", "Fig Plant", "PLANT_FIG");
            Add(c, "(Sandbox_Houseplant_SnakePlant)", "Snake Plant", "PLANT_SNAKE");
            Add(c, "(Curtain_NoTassel_Sandbox)Variant", "Curtain", "CURTAIN_NO_TASSELS");
            Add(c, "(Dorm_Rug)", "Rug", "DORM_RUG");
            Add(c, "(Cardboard_box_Sandbox)Variant", "Box", "CARDBOARD_BOX");
            Add(c, "(Sandbox_Barrel)", "Barrel", "WINEBARREL");
            Add(c, "(MetalBarrel_Sandbox)Variant", "Metal Barrel", "METAL_BARREL");
            Add(c, "(TrashCan_Metal_Sandbox)Variant", "Trash Can", "TRASH_CAN");
            Add(c, "(Dumpster_Sandbox)Variant", "Dumpster", "DUMPSTER");
            Add(c, "(Wood_Pallet_Sandbox)Variant", "Pallet", "WOOD_PALLET");
            Add(c, "(Tire_Car_Sandbox)Variant", "Tire", "CAR_TIRE");
            Add(c, "(Car_50s_Sandbox)Variant", "50s Car", "CLASSIC_CAR");
            Add(c, "(Truck_Sandbox)Variant", "Truck", "TRUCK");
            Add(c, "(Standee_Bear01)", "Bear Standee", "STANDEE_BEAR_01");
            Add(c, "(Standee_Female01)", "Standee", "STANDEE_FEMALE_01");
            Add(c, "(Sandbox_JackOLantern)", "Pumpkin", "JACKOLANTERN");

            c = Category(list, "Nature");
            Add(c, "(Sandbox_FlatTreeLarge)", "Big Tree", "FLATTREELARGE");
            Add(c, "(Sandbox_FlatTreeMedium)", "Tree", "FLATTREEMEDIUM");
            Add(c, "(Sandbox_FlatTreeSmall)", "Small Tree", "FLATTREESMALL");
            Add(c, "(PineTree_Dead_Sandbox)Variant", "Dead Pine", "PINE_TREE_DEAD");
            Add(c, "(Tree_Stump_Sandbox)Variant", "Stump", "TREE_STUMP");
            Add(c, "(Sandbox_SittingLog)", "Log", "SITTINGLOG");
            Add(c, "(LogPile_Sandbox)Variant", "Log Pile", "LOG_PILE");
            Add(c, "(Hay_Bale_Sandbox)Variant", "Hay Bale", "HAY_BALE");
            Add(c, "(Rock_01)Variant_Sandbox)Variant", "Rock", "ROCK_01");
            Add(c, "(Rock_04)Variant_Sandbox)Variant", "Boulder", "ROCK_04");
            Add(c, "(Rock_Cliff)Variant_Sandbox)Variant", "Cliff", "ROCK_CLIFF");
            Add(c, "(Rock_Plateau)Variant_Sandbox)Variant", "Plateau", "ROCK_PLATEAU");
            Add(c, "(Rock_Tall)Variant_Sandbox)Variant", "Tall Rock", "ROCK_TALL");
            Add(c, "(Rock_Wall)Variant_Sandbox)Variant", "Rock Wall", "ROCK_WALL");
            Add(c, "(Sandbox_Campfire)", "Campfire", "CAMPFIRE");

            c = Category(list, "Lights");
            Light(c, "Point", "Point Light", "DYNAMINC_POINT_LIGHT");
            Light(c, "Spot", "Spotlight", "DYNAMIC_SPOTLIGHT");
            Add(c, "(Light_Pendant_Sandbox)Variant", "Pendant Lamp", "LIGHT_PENDANT");
            Add(c, "(Dorm_Desk_Lamp)", "Desk Lamp", "DORM_DESK_LAMP");
            Add(c, "(Dorm_Fluorescent_Light)", "Ceiling Light", "DORM_FLUORESCENT_LIGHT");
            Add(c, "(StageLight_Can_Sandbox)", "Stage Light", "STAGELIGHT_CAN");
            Add(c, "(Light_Strip_Short_Sandbox)", "Light Strip", "LIGHT_STRIP_SHORT");
            Add(c, "(Light_Marquee_Long_Sandbox)", "Marquee", "LIGHT_MARQUEE");
            Add(c, "(Light_Chevron_Sandbox)", "Chevrons", "LIGHT_CHEVRONS");
            Add(c, "(Sandbox_Candle)", "Candle", "CANDLE", true);
            Add(c, "(Sandbox_Glowstick_Blue)", "Glowstick", "GLOWSTICK_BLUE", true);

            c = Category(list, "Kitchen");
            Add(c, "(RecCenter_Cafe_Mug)", "Mug", "COFFEE_MUG", true);
            Add(c, "(RecCenter_Cafe_CoffeePot)", "Coffee Pot", "COFFEE_POT", true);
            Add(c, "(RecCenter_Cafe_Plate)", "Plate", "CAFE_PLATE", true);
            Add(c, "(SoloCup)", "Cup", "SOLOCUP", true);
            Add(c, "(Sandbox_Glass)", "Glass", "SANDBOX_GLASS", true);
            Add(c, "(Sandbox_RootBeerBottle)", "Root Beer", "ROOT_BEER_BOTTLE", true);
            Add(c, "(WaterJug_Sandbox)Variant", "Water Jug", "WATER_JUG", true);
            Add(c, "(Sandbox_Minifridge)", "Mini Fridge", "MINIFRIDGE");
            Add(c, "(Sandbox_Mocktail_Milkshake)", "Milkshake", "MOCKTAIL_MILKSHAKE", true);
            Add(c, "(Quest_Pirate1_FryingPan)", "Frying Pan", "PIRATE_FRYING_PAN", true);

            c = Category(list, "Sports");
            Add(c, "(Basketball)", "Basketball", "BASKETBALL", true);
            Add(c, "(Basketballhoop)", "Hoop", "BASKETBALLHOOP");
            Add(c, "(DodgeballBall)", "Dodgeball", "DODGEBALL", true);
            Add(c, "(SoccerBall)", "Soccer Ball", "SOCCER_BALL", true);
            Add(c, "(BowlingBall)", "Bowling Ball", "BOWLING_BALL", true);
            Add(c, "(SandboxBowlingPin)", "Bowling Pin", "BOWLING_PIN", true);
            Add(c, "(DiscGolfDisc)", "Disc", "DISCGOLF_DISC", true);
            Add(c, "(Sandbox_DiscGolfGoal)", "Disc Goal", "DISCGOLF_GOAL");
            Add(c, "(PoolTable)", "Pool Table", "POOL_TABLE");
            Add(c, "(Sandbox_Table_PingPong)", "Ping Pong", "TABLE_PINGPONG");
            Add(c, "(Pingpong_Paddle)", "Paddle", "PING_PONG_PADDLE", true);
            Add(c, "(Paintball_Target_Sandbox)Variant", "Target", "TARGET");
            Add(c, "(Bounce_Pad_Sandbox)", "Bounce Pad", "BOUNCE_PAD");
            Add(c, "(CrashPad_1x2_Thick_Sandbox)", "Crash Pad", "CRASHPAD_THICK");

            c = Category(list, "Toys");
            Add(c, "(Sandbox_Meeple)", "Meeple", "SANDBOX_MEEPLE", true);
            Add(c, "(Die)", "Die", "DIE", true);
            Add(c, "(Sandbox_D20)", "D20", "D20", true);
            Add(c, "(Sandbox_Bell)", "Bell", "BELL", true);
            Add(c, "(StackableBlock)", "Block", "STACKABLE_BLOCK", true);
            Add(c, "(Sandbox_Snowball)", "Snowball", "SNOWBALL", true);
            Add(c, "(TennisBall)", "Tennis Ball", "TENNIS_BALL", true);
            Add(c, "(Quest_Pirate1_BreakableFish)", "Fish Plush", "PIRATE_PLUSH_FISH", true);
            Add(c, "(Quest_Dracula1_Bat_Plushy)", "Bat Plush", "DRACULA_BOSS_PLUSHY", true);

            c = Category(list, "Building");
            Add(c, "(Sandbox_Wall)", "Wall", "SANDBOX_WALL");
            Add(c, "(Sandbox_Wall_Window)", "Window Wall", "SANDBOX_WALL_WINDOW");
            Add(c, "(Sandbox_Floor)", "Floor", "SANDBOX_FLOOR");
            Add(c, "(Sandbox_VerticalWall)", "Wall Panel", "VERTICALWALL");
            Add(c, "(Sandbox_HorizontalWall)", "Floor Panel", "HORIZONTALWALL");
            Add(c, "(Dorm_Ladder)", "Ladder", "DORM_LADDER");
            Add(c, "(Railing_Guardrail_Auto_Sandbox)Variant", "Guard Rail", "GUARD_RAIL");
            Add(c, "(Fence_Wood_Frame_5m_Sandbox)Variant", "Fence", "FENCE_FRAME");
            Add(c, "(Concrete_Barrier_Sandbox)Variant", "Barrier", "CONCRETE_BARRIER");
            Add(c, "(Sandbox_StonePillar)", "Pillar", "STONEPILLAR");
            Add(c, "(Sandbox_Cone)", "Traffic Cone", "CONE", true);
            Add(c, "(IBeam_5M_Sandbox)Variant", "I-Beam", "IBEAM_5M");
            Add(c, "(Scaffolding_Shiny_Sandbox)", "Scaffolding", "SCAFFOLDING_SHINY");
            Add(c, "(Sign_Diner_Sandbox)Variant", "Diner Sign", "DINER_SIGN");
            Add(c, "(Sign_DriveIn_Sandbox)Variant", "Drive-In Sign", "DRIVEIN_SIGN");
            Add(c, "(Sandbox_CodeOfConduct_Poster)", "Poster", "SANDBOX_COC_POSTER");
            Add(c, "(BulletinBoard_Sandbox)Variant", "Notice Board", "BULLETIN_BOARD");
            return list;
        }

        private static RrMakerPropCategory Category(List<RrMakerPropCategory> list, string name)
        {
            var c = new RrMakerPropCategory { Name = name };
            list.Add(c);
            return c;
        }

        private static void Add(RrMakerPropCategory c, string prefab, string label, string thumb, bool handheld = false)
        {
            c.Items.Add(new RrMakerPropEntry { Kind = RrMakerKind.Prop, Asset = prefab, Label = label, Thumb = thumb, Handheld = handheld });
        }

        private static void Light(RrMakerPropCategory c, string type, string label, string thumb)
        {
            c.Items.Add(new RrMakerPropEntry { Kind = RrMakerKind.Light, Asset = type, Label = label, Thumb = thumb });
        }

        /// <summary>The catalogue entry of an install prefab (labels in the readout), or null.</summary>
        public static RrMakerPropEntry FindProp(string asset)
        {
            if (string.IsNullOrEmpty(asset))
            {
                return null;
            }

            foreach (var c in Categories)
            {
                foreach (var e in c.Items)
                {
                    if (e.Asset == asset)
                    {
                        return e;
                    }
                }
            }

            return null;
        }

        /// <summary>A short human name for an object (the hover readout).</summary>
        public static string Describe(RrMakerObject o)
        {
            if (o == null)
            {
                return "";
            }

            switch (o.Kind)
            {
                case RrMakerKind.Shape:
                    foreach (var s in Shapes)
                    {
                        if (s.Name == o.Asset)
                        {
                            return s.Label;
                        }
                    }

                    return string.IsNullOrEmpty(o.Asset) ? "Shape" : o.Asset;
                case RrMakerKind.Ink:
                    return "Ink";
                case RrMakerKind.Light:
                    return o.Asset == "Spot" ? "Spotlight" : "Point Light";
                case RrMakerKind.Text:
                    return "Text";
                default:
                    var e = FindProp(o.Asset);
                    if (e != null)
                    {
                        return e.Label;
                    }

                    return Pretty(o.Asset);
            }
        }

        /// <summary>"(Sandbox_Table_Short)" -> "Table Short", "(Dorm_Wall_1)" -> "Dorm Wall 1".</summary>
        public static string Pretty(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
            {
                return "Prop";
            }

            int open = prefab.IndexOf('(');
            int close = prefab.IndexOf(')');
            string s = open >= 0 && close > open ? prefab.Substring(open + 1, close - open - 1) : prefab;
            if (s.StartsWith("Sandbox_", System.StringComparison.Ordinal))
            {
                s = s.Substring(8);
            }

            s = s.Replace("_Sandbox", "").Replace('_', ' ').Trim();
            return s.Length > 0 ? s : "Prop";
        }

        internal static void Shutdown()
        {
            _categories = null;
        }
    }
}
