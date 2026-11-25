using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.LaserTag;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>A one-time fix-up of a template object in a brand-new room: put it on a surface or against a wall.</summary>
    public sealed class RrMakerSettle
    {
        /// <summary>Lowest point lands on this room-local height (NaN: no grounding).</summary>
        public float GroundY = float.NaN;

        /// <summary>Mount against a wall: centred on WallPoint, its back Standoff metres off the wall whose normal is Normal.</summary>
        public bool Mount;

        public Vector3 WallPoint;
        public Vector3 Normal;
        public float Standoff;
    }

    /// <summary>What a template's fixed base made: where players appear (room-local), and the base's own material cache.</summary>
    public sealed class RrMakerBase
    {
        public Transform Root;
        public readonly List<Pose> Spawns = new List<Pose>();
        public readonly Dictionary<string, Material> Surfaces = new Dictionary<string, Material>();
        public RrLaserTagLevel Level;

        /// <summary>
        /// The base's lights for the room's Rec Room light grid (room-local, already set up for URP): the maker objects are
        /// lit by these and by their own lights. Light components under the base that are not listed here are not put in
        /// the grid (a kit scene's own lights, already in its own grid; the Dorm's URP-only lights).
        /// </summary>
        public readonly List<RrLightGrid.Source> Sources = new List<RrLightGrid.Source>();

        /// <summary>Copies a scene kit grid's sources into <see cref="Sources"/> (from its host's frame to the room's).</summary>
        public void TakeGrid(RrLightGrid grid, Transform room)
        {
            if (grid == null || room == null)
            {
                return;
            }

            var host = grid.transform;
            foreach (var s in grid.Sources)
            {
                var c = s;
                c.Position = room.InverseTransformPoint(host.TransformPoint(s.Position));
                c.Rotation = Quaternion.Inverse(room.rotation) * (host.rotation * s.Rotation);
                Sources.Add(c);
            }
        }
    }

    /// <summary>
    /// A base map a maker room is made from (Rec Room's "pick a base room" card, rr_maker_rooms.md 1.2 and 5.1): the
    /// deletable base pieces every new room starts with (<see cref="Objects"/>: ordinary maker objects, saved with the room,
    /// as Rec Room's Maker Room walls and Dorm kit were), and/or a fixed base the room always rebuilds (<see cref="BuildBase"/>:
    /// a converted Rec Room level, outdoor scenery, the level's lights), its lighting key and where players appear.
    /// </summary>
    public sealed class RrMakerTemplate
    {
        public string Key;
        public string Title;
        public string Blurb;

        /// <summary>False: shown on the Create page as "coming soon" (<see cref="Missing"/> says why).</summary>
        public bool Available = true;

        public string Missing;
        public int MaxPlayers = 12;

        /// <summary>The card picture: a Rec Room sprite/texture name (null: the art of <see cref="ArtOf"/>).</summary>
        public string Art;

        public RrActivity ArtOf;

        /// <summary>The picture fits inside the card over the background colours (thumbnails) instead of covering it.</summary>
        public bool ArtFit = true;

        public Color BackFrom = new Color(0.86f, 0.9f, 0.95f);
        public Color BackTo = new Color(0.62f, 0.7f, 0.8f);

        /// <summary>RrEnvironment lighting key (sun, ambient, fog, sky) while in the room; null = the world's own.</summary>
        public string EnvKey;

        /// <summary>The base pieces of a new room (ids 1..n).</summary>
        public Func<List<RrMakerObject>> Objects;

        /// <summary>One-time fix-ups of base pieces in a brand-new room, by object id.</summary>
        public readonly Dictionary<uint, RrMakerSettle> Settle = new Dictionary<uint, RrMakerSettle>();

        /// <summary>Install pieces stored in their original room's coordinates (Rec Room's Maker Pen kits): the template pose of each.</summary>
        public readonly Dictionary<string, Pose> KitPoses = new Dictionary<string, Pose>();

        /// <summary>Builds the fixed base under base.Root (spread over frames); may be null.</summary>
        public Func<RrMakerRoom, RrMakerBase, IEnumerator> BuildBase;

        /// <summary>Where players appear when the base names no spawn (room-local).</summary>
        public Pose DefaultSpawn = new Pose(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
    }

    /// <summary>
    /// The templates (task 1). Available: the Maker Room (Rec Room's ^DefaultMakerRoom from its six install pieces), the
    /// Dorm Room (the Maker Pen dorm kit, as Places/Dorm lays it out), The Ink Space (the Charades hull + stage spawnables,
    /// Rec Room's 2020 base room), and as fixed scene bases built from Rec Room's own Android levels the way the games build
    /// theirs (RrJumbotronArea over an RRSK from RrSceneBank): the Park (Data/Maker_Park.bytes, level75, with its terrain),
    /// the Performance Hall (Data/Maker_PerformanceHall.bytes, level73), the Bowling Alley (Data/Bowling.bytes, level91) and
    /// the Hangar (Data/LaserTag_Hangar.bytes, level76). A template with Available = false is shown as "coming soon".
    /// </summary>
    public static class RrMakerTemplates
    {
        public const string MakerRoom = "MakerRoom";
        public const string Dorm = "Dorm";
        public const string InkSpace = "InkSpace";
        public const string Bowling = "Bowling";
        public const string Hangar = "Hangar";
        public const string Park = "Park";
        public const string PerformanceHall = "PerformanceHall";

        private static List<RrMakerTemplate> _all;

        public static IReadOnlyList<RrMakerTemplate> All
        {
            get
            {
                if (_all == null)
                {
                    Define();
                }

                return _all;
            }
        }

        /// <summary>A template by key (case-insensitive), or null.</summary>
        public static RrMakerTemplate Get(string key)
        {
            foreach (var t in All)
            {
                if (string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            return null;
        }

        /// <summary>The template's title, or the key when it is unknown.</summary>
        public static string TitleOf(string key)
        {
            var t = Get(key);
            return t != null ? t.Title : string.IsNullOrEmpty(key) ? "Room" : key;
        }

        public static void Shutdown()
        {
            _all = null;
        }

        private static void Define()
        {
            _all = new List<RrMakerTemplate>
            {
                DefineMakerRoom(),
                DefineDorm(),
                DefineInkSpace(),
                DefinePark(),
                DefinePerformanceHall(),
                DefineBowling(),
                DefineHangar()
            };
        }

        /// <summary>
        /// The template pose of a Maker Pen kit piece stored in its original room's coordinates (the Dorm kit), whatever
        /// room it is placed in: the object's container is that pose, the piece is snapped to the kit's frame under it.
        /// </summary>
        public static bool KitPose(string asset, out Pose pose)
        {
            pose = default(Pose);
            if (string.IsNullOrEmpty(asset) || !asset.StartsWith("(Dorm_", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var t in All)
            {
                if (t.KitPoses.TryGetValue(asset, out pose))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ helpers

        private static RrMakerObject Piece(uint id, string asset, Vector3 pos, Quaternion rot, RrMakerPhysics physics = RrMakerPhysics.Environment, float scale = 1f)
        {
            return new RrMakerObject
            {
                Id = id,
                Kind = RrMakerKind.Prop,
                Asset = asset,
                Position = pos,
                Rotation = rot,
                Scale = Vector3.one * scale,
                Color = new Color32(255, 255, 255, 255),
                Physics = physics,
                Frozen = false
            };
        }

        private static Color32 C(Color c)
        {
            return c;
        }

        private static Quaternion Yaw(float y)
        {
            return Quaternion.Euler(0f, y, 0f);
        }

        // ------------------------------------------------------------------ the Maker Room

        /// <summary>
        /// Rec Room's ^DefaultMakerRoom: a 15 x 15 x 8.2 m box of six install spawnables (rr_maker_rooms.md 2.3), measured
        /// with rrtool: (Sandbox_Floor) a 15 x 0.4 x 15 slab centred on its pivot; (Sandbox_Wall) 15 x 8.38 x 0.4 from 0.09
        /// under its pivot, its mesh 0.19 m along -x; (Sandbox_Wall_Window) the same with windows, prefab yaw -90;
        /// (Sandbox_Ceiling) the floor mesh with six fluorescent tubes, a hemisphere Room Fill, six Ceiling Lights and two
        /// Bounce Lights, prefab root at y 8.19. The prefab roots keep Rec Room's frame (floor 0, ceiling 8.19), shifted down
        /// 0.2 m so the floor top is y 0. GUESS: which side has the window wall (-x here, as the floor's AnimateInOut bounds
        /// suggest) - the Basement level itself was never converted. A welcome mat marks the spawn, as in Rec Room's default save.
        /// </summary>
        private static RrMakerTemplate DefineMakerRoom()
        {
            var t = new RrMakerTemplate
            {
                Key = MakerRoom,
                Title = "Maker Room",
                Blurb = "Rec Room's blank slate: a simple 15 x 15 m room. Its walls, floor and ceiling are objects too - move them, recolour them or delete them all.",
                Art = "SpawnableToolTex_SANDBOX_WALL_WINDOW",
                BackFrom = new Color(0.88f, 0.92f, 0.96f),
                BackTo = new Color(0.58f, 0.68f, 0.8f),
                EnvKey = "Dorm_Builtin",
                MaxPlayers = 12,
                DefaultSpawn = new Pose(new Vector3(0f, 0.05f, -5f), Quaternion.identity)
            };

            t.Objects = () =>
            {
                const float y = -0.2f;
                const float half = 7.5f;
                var wallShift = new Vector3(0.19f, 0f, 0f);
                var list = new List<RrMakerObject>
                {
                    Piece(1, "(Sandbox_Floor)", new Vector3(0f, y, 0f), Quaternion.identity),
                    Piece(2, "(Sandbox_Wall)", new Vector3(0f, y, -half) + Yaw(0f) * wallShift, Yaw(0f)),
                    Piece(3, "(Sandbox_Wall)", new Vector3(0f, y, half) + Yaw(180f) * wallShift, Yaw(180f)),
                    Piece(4, "(Sandbox_Wall)", new Vector3(half, y, 0f) + Yaw(90f) * wallShift, Yaw(90f)),
                    Piece(5, "(Sandbox_Wall_Window)", new Vector3(-half, y, 0f), Yaw(-90f)),
                    Piece(6, "(Sandbox_Ceiling)", new Vector3(0f, 8.19f + y, 0f), Quaternion.identity),
                    Piece(7, "(SandboxWelcomeMat)", new Vector3(0f, 0.002f, -5f), Quaternion.identity, RrMakerPhysics.Decoration)
                };
                return list;
            };
            t.Settle[7] = new RrMakerSettle { GroundY = 0.002f };
            return t;
        }

        // ------------------------------------------------------------------ the Dorm

        /// <summary>
        /// Rec Room's dorm, a Maker Pen room since 2019: the 22 kit pieces of RrDormRoom's architecture table (copied: it is
        /// private there; the poses are its §3.2 table and every piece is snapped to the kit's own frame when built, as the
        /// Dorm does), its furniture and the desk / loft props, all ordinary maker objects. The fixed base is what the Dorm
        /// builds around them: the campus outside the windows (<see cref="RrDormOutdoors"/>), the Dorm's own lights and the
        /// Android level's light rig (Dorm_Builtin). Not carried over: the mirror's reflection, the radio, the ladder climb,
        /// the Play board and the door (Rec Room's maker rooms are left through the watch).
        /// </summary>
        private static RrMakerTemplate DefineDorm()
        {
            var t = new RrMakerTemplate
            {
                Key = Dorm,
                Title = "Dorm Room",
                Blurb = "Rec Room's dorm, piece by piece: walls, loft, bunk bed, desk and all - every piece can be moved, recoloured or deleted.",
                ArtOf = RrActivity.Dorm,
                Art = "illustration_HomeButton_DormRoom",
                ArtFit = true,
                BackFrom = new Color(0.98f, 0.83f, 0.62f),
                BackTo = new Color(0.86f, 0.55f, 0.3f),
                EnvKey = "Dorm_Builtin",
                MaxPlayers = 8,
                DefaultSpawn = new Pose(RrDormRoom.SpawnPoint, Quaternion.Euler(0f, RrDormRoom.SpawnYaw, 0f))
            };

            var y90 = new Quaternion(0f, 0.7071068f, 0f, 0.7071068f);
            var yMinus90 = new Quaternion(0f, -0.7071068f, 0f, 0.7071068f);
            var y180 = new Quaternion(0f, 1f, 0f, 0f);
            var x90 = new Quaternion(0.7071068f, 0f, 0f, 0.7071068f);
            var wall = RrDormRoom.WallColor;
            var floor = RrDormRoom.FloorColor;
            var frame = RrDormRoom.FrameColor;

            // RrDormRoom.Architecture (§3.2), copied.
            var arch = new List<KeyValuePair<string, KeyValuePair<Pose, Color>>>();
            Action<string, Vector3, Quaternion, Color> a = (n, p, r, c) => arch.Add(new KeyValuePair<string, KeyValuePair<Pose, Color>>(n, new KeyValuePair<Pose, Color>(new Pose(p, r), c)));
            a("(Dorm_Floor)", Vector3.zero, Quaternion.identity, floor);
            a("(Dorm_Floor_Trim)", new Vector3(-0.080f, 0.061f, 0f), Quaternion.identity, RrDormRoom.TrimColor);
            a("(Dorm_Wall_1)", new Vector3(-4.998f, 0f, -0.002f), y90, wall);
            a("(Dorm_Wall_2)", Vector3.zero, Quaternion.identity, wall);
            a("(Dorm_Wall_3)", new Vector3(4.998f, 0f, -0.002f), yMinus90, wall);
            a("(Dorm_Wall_4)", new Vector3(0f, 0f, 3f), y180, wall);
            a("(Dorm_Wall_Loft_1)", new Vector3(-4f, 0f, -3f), Quaternion.identity, wall);
            a("(Dorm_Wall_Loft_2)", new Vector3(-4f, 0f, 3f), y180, wall);
            a("(Dorm_Wall_Pillar)", Vector3.zero, Quaternion.identity, wall);
            a("(Dorm_Wall_Stripe)", new Vector3(0f, 1.35f, 0f), Quaternion.identity, RrDormRoom.StripeColor);
            a("(Dorm_Ceiling_Flat)", new Vector3(-2f, 5.998f, -0.002f), x90, RrDormRoom.CeilingColor);
            a("(Dorm_Ceiling_Angled)", new Vector3(2.991f, 5.2f, -0.002f), new Quaternion(0.354f, -0.612f, 0.354f, 0.612f), RrDormRoom.CeilingColor);
            a("(Dorm_Skylight_Frame)", new Vector3(-1.75f, 6.498f, 0.798f), x90, frame);
            a("(Dorm_Skylight_Window)", new Vector3(-1.8f, 7f, 0.8f), Quaternion.identity, frame);
            a("(Dorm_Window_Large_Frame)", new Vector3(2.999f, 2.5f, 0.699f), yMinus90, frame);
            a("(Dorm_Window_Large_Panes)", new Vector3(3.53f, 4f, 0.76f), Quaternion.identity, frame);
            a("(Dorm_Window_Small_Frame)", new Vector3(0f, 3.6f, 3f), y180, frame);
            a("(Dorm_Window_Small_Panes)", new Vector3(0f, 3.8f, 3f), y180, frame);
            a("(Dorm_Loft_Floor)", new Vector3(-3.805f, 2.696f, 0.001f), y90, floor);
            a("(Dorm_Loft_Beam)", new Vector3(-2.719f, 2.5f, -0.001f), y90, RrDormRoom.BeamColor);
            a("(Dorm_Loft_Railing)", new Vector3(-2.71f, 3.083f, -0.878f), y90, wall);
            a("(Dorm_Loft_Shelves)", new Vector3(-3.999f, 3.5f, -0.001f), y90, frame);
            foreach (var kv in arch)
            {
                t.KitPoses[kv.Key] = kv.Value.Key;
            }

            t.KitPoses["(Dorm_Light_Shafts)"] = new Pose(Vector3.zero, Quaternion.identity);

            t.Objects = () =>
            {
                var list = new List<RrMakerObject>();
                uint id = 1;
                foreach (var kv in arch)
                {
                    var o = Piece(id++, kv.Key, kv.Value.Key.position, kv.Value.Key.rotation);
                    o.Color = C(kv.Value.Value);
                    list.Add(o);
                }

                list.Add(Piece(id++, "(Dorm_Light_Shafts)", Vector3.zero, Quaternion.identity, RrMakerPhysics.Decoration));

                // Furniture (RrDormRoom.BuildFurniture), static.
                Func<string, Vector3, float, float, float, RrMakerPhysics, RrMakerObject> s = (asset, pos, yaw, ground, scale, physics) =>
                {
                    var o = Piece(id++, asset, pos, Yaw(yaw), physics, scale);
                    if (!float.IsNaN(ground))
                    {
                        o.Position = new Vector3(pos.x, ground, pos.z);
                        t.Settle[o.Id] = new RrMakerSettle { GroundY = ground };
                    }

                    list.Add(o);
                    return o;
                };
                Func<string, Vector3, Vector3, float, RrMakerObject> w = (asset, point, normal, standoff) =>
                {
                    var o = Piece(id++, asset, point, Quaternion.LookRotation(normal));
                    t.Settle[o.Id] = new RrMakerSettle { Mount = true, WallPoint = point, Normal = normal, Standoff = standoff };
                    list.Add(o);
                    return o;
                };

                const RrMakerPhysics env = RrMakerPhysics.Environment;
                const RrMakerPhysics deco = RrMakerPhysics.Decoration;
                s("(Dorm_Bunk_Bed)", RrDormRoom.BedPosition, 0f, 0f, 1f, env);
                s("(Dorm_Rug)", new Vector3(0.30f, 0f, 0.10f), 0f, 0f, 1f, env);
                s("(Dorm_Ladder)", RrDormRoom.LadderPosition, 90f, float.NaN, 1f, env);
                s("(Dorm_Fluorescent_Light)", new Vector3(-3.80f, 2.64f, 0f), 0f, float.NaN, 1f, deco);
                s("(Dorm_Fluorescent_Light)", new Vector3(-1.20f, 6.13f, -1.95f), 0f, float.NaN, 1f, deco);
                s("(Dorm_Fluorescent_Light)", new Vector3(-3.80f, 6.13f, -1.95f), 0f, float.NaN, 1f, deco);
                list.Add(Piece(id++, "(Dorm_FreeMirror)", RrDormRoom.MirrorPosition, y180));
                w("(Sandbox_CodeOfConduct_Poster)", RrDormRoom.CodeOfConductPosition, Vector3.back, RrDormRoom.StripeClearance);
                w("(Dorm_Campus_Map)", RrDormRoom.CampusMapPosition, Vector3.back, 0.012f);
                list.Add(Piece(id++, "(Dorm_Community_Board)", RrDormRoom.BoardPosition, Quaternion.identity));
                w("(Sandbox_MuteGesture_Poster)", RrDormRoom.StopGesturePosterPosition, Vector3.forward, 0.006f);
                w("(Sandbox_ShakeHands_Poster)", RrDormRoom.ShakeHandsPosterPosition, Vector3.forward, 0.006f);
                list.Add(Piece(id++, "(Basketballhoop)", RrDormRoom.HoopPivot, Quaternion.identity, env, RrDormRoom.HoopScale));
                s("(Sandbox_CouchChair)", new Vector3(-3.733f, 0f, 1.962f), 132.64f, 0f, 1f, env);
                s("(Sandbox_Table_Short)", new Vector3(-4.39f, 0f, 0.585f), 90f, 0f, 0.815f, env);
                s("(Sandbox_Houseplant_Fig)", new Vector3(-4.422f, 0f, 1.429f), 129.18f, 0f, 1f, env);
                s("(Sandbox_Houseplant_SnakePlant)", new Vector3(-4.511f, 0f, -2.427f), 89.73f, 0f, 1f, env);
                s("(Sandbox_Houseplant_Dracaena)", new Vector3(-2.663f, 0f, 2.10f), 146.86f, 0f, 1f, env);
                s("(SandboxWelcomeMat)", RrDormRoom.WelcomeMatPosition, RrDormRoom.WelcomeMatYaw, 0.002f, 1f, deco);
                s(RrDormRoom.Whiteboard, RrDormRoom.WhiteboardPosition, RrDormRoom.WhiteboardYaw, RrDormRoom.LoftFloorY, 1f, env);
                s(RrDormRoom.MarkerSet, RrDormRoom.MarkerSetPosition, 90f, RrDormRoom.LoftFloorY, 1f, deco);

                // The props (RrDormRoom.BuildProps): decorations on the floor, the desk, the coffee table and the loft.
                const float desk = RrDormRoom.DeskTopY;
                const float table = 0.36f;
                const float loft = RrDormRoom.LoftFloorY;
                s("(Basketball)", new Vector3(3.409f, 0f, -1.142f), 270f, 0.01f, 1f, deco);
                s("(TrashCan_Office)SandboxVariant", new Vector3(3.905f, 0f, -1.10f), 0f, 0.01f, 1f, deco);
                s("(Bucket)", new Vector3(-2.163f, 0f, -2.636f), 306.16f, 0.01f, 1f, deco);
                s("(DrawableClipboard)", new Vector3(2.887f, 0f, 1.913f), 193f, 0.01f, 1f, deco);
                s("(DiscGolfDisc)", new Vector3(0.9f, 0f, 0.4f), 0f, 0.022f, 1f, deco);
                s("(Radio)", new Vector3(1.381f, 0f, -2.529f), 308.04f, desk + 0.01f, 1f, deco);
                s("(SoloCup)", new Vector3(2.157f, 0f, -2.25f), 182.11f, desk + 0.01f, 1f, deco);
                s("(SoloCup)", new Vector3(2.469f, 0f, -2.256f), 168.37f, desk + 0.01f, 1f, deco);
                s("(WaterBottle)", new Vector3(1.331f, 0f, -2.055f), 133.02f, desk + 0.01f, 1f, deco);
                s("(Dart)", new Vector3(1.906f, 0f, -2.10f), 86.01f, desk + 0.01f, 1f, deco);
                s("(Dart)", new Vector3(2.057f, 0f, -2.00f), 86.01f, desk + 0.01f, 1f, deco);
                s("(Consumables_RootBeer_Bottle)", new Vector3(0.90f, 0f, -2.45f), 0f, desk + 0.01f, 1f, deco);
                s("(Consumables_RootBeer_Bottle)", new Vector3(0.98f, 0f, -2.68f), 40f, desk + 0.01f, 1f, deco);
                s("(Sandbox_Bell)", new Vector3(1.08f, 0f, -2.28f), 0f, desk + 0.01f, 1f, deco);
                s("(Sandbox_Book_Green)", new Vector3(0.82f, 0f, -2.06f), 90f, desk + 0.01f, 1f, deco);
                s("(Dorm_Desk_Lamp)", new Vector3(2.80f, 0f, -2.68f), 0f, desk + 0.01f, 1f, deco);
                s("(Consumables_CheesePizza_Slice)", new Vector3(2.72f, 0f, -2.05f), 20f, desk + 0.01f, 1f, deco);
                s("(Consumables_Apple_Red)", new Vector3(2.60f, 0f, -2.45f), 0f, desk + 0.01f, 1f, deco);
                s("(SandboxDiceSet)", new Vector3(2.86f, 0f, -2.28f), 15f, desk + 0.01f, 1f, deco);
                s("(Pingpong_Paddle)", new Vector3(-4.396f, 0f, 0.861f), 278.69f, table + 0.01f, 1f, deco);
                s("(PingpongBall)", new Vector3(-4.337f, 0f, 0.382f), 90.51f, table + 0.01f, 1f, deco);
                s("(Eraser)", new Vector3(-3.355f, 0f, -2.136f), 253.03f, loft + 0.01f, 1f, deco);
                s("(DrawableClipboard)", new Vector3(-3.757f, 0f, 1.31f), 72f, loft + 0.01f, 1f, deco);
                s("(Trophy_Oscar)", new Vector3(-3.35f, 0f, -2.45f), 90f, loft + 0.01f, 1f, deco);
                s("(Sandbox_Book_Yellow)", new Vector3(-3.9f, 0f, -2.5f), 90f, loft + 0.01f, 1f, deco);

                // The desk lamp's bulb (the Dorm adds the same light to it).
                list.Add(new RrMakerObject
                {
                    Id = id++,
                    Kind = RrMakerKind.Light,
                    Asset = "Point",
                    Position = new Vector3(2.80f, desk + 0.32f, -2.53f),
                    Scale = new Vector3(2.2f, 0.9f, 0f),
                    Color = C(new Color(1f, 0.86f, 0.62f)),
                    Physics = RrMakerPhysics.Decoration,
                    Frozen = false
                });
                return list;
            };

            t.BuildBase = BuildDormBase;
            return t;
        }

        private static IEnumerator BuildDormBase(RrMakerRoom room, RrMakerBase b)
        {
            try
            {
                RrDormOutdoors.Build(b.Root);
            }
            catch (Exception e)
            {
                RrLog.Error("MakerRooms", "the Dorm's outdoors failed", e);
            }

            yield return null;

            // RrDormRoom.BuildLights (copied): under the loft, the main ceiling fixture, a fill under the slope, the sun through the big window.
            // URP lights only (avatars, URP-shaded things), as in the Dorm: the kit pieces take the rig below from the grid.
            var group = RrBuild.Group(b.Root, "Dorm Lights (URP)").transform;
            var warm = new Color(1f, 0.95f, 0.86f);
            RrBuild.PointLight(group, "Loft Underside", new Vector3(-3.8f, 2.35f, 0f), warm, 5.5f, 1.1f);
            RrBuild.PointLight(group, "Ceiling", new Vector3(-1.2f, 5.6f, -1.2f), warm, 10f, 1.3f);
            RrBuild.PointLight(group, "Fill", new Vector3(1.8f, 3.2f, 0.4f), new Color(0.92f, 0.95f, 1f), 9f, 0.7f);
            var sunGo = new GameObject("Window Sun");
            sunGo.transform.SetParent(group, false);
            var dir = new Vector3(-0.68f, -0.73f, 0f).normalized;
            sunGo.transform.localPosition = new Vector3(0f, 0f, 0.8f) - dir * 11f;
            sunGo.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Spot;
            sun.spotAngle = 44f;
            sun.innerSpotAngle = 20f;
            sun.range = 16f;
            sun.intensity = 2.6f;
            sun.color = new Color(1f, 0.87f, 0.66f);
            sun.shadows = LightShadows.None;

            // The Android level's own rig (RrEnvironment places it only for RrActivity.Dorm), as RrEnvironment.BuildRig does:
            // a Unity twin for URP objects and the same light in the room's Rec Room light grid.
            var s = RrEnvironment.Find("Dorm_Builtin");
            if (s != null)
            {
                var rig = RrBuild.Group(b.Root, "Dorm Rig").transform;
                foreach (var r in s.Rig)
                {
                    var go = new GameObject("Rig " + r.Type);
                    go.transform.SetParent(rig, false);
                    go.transform.localPosition = r.Position;
                    go.transform.localRotation = r.Rotation;
                    var l = go.AddComponent<Light>();
                    l.type = r.Type;
                    l.color = r.Color;
                    l.intensity = r.Intensity;
                    l.range = Mathf.Max(0.1f, r.Range);
                    if (r.Type == LightType.Spot)
                    {
                        l.spotAngle = Mathf.Clamp(r.SpotAngle, 1f, 179f);
                    }

                    l.shadows = LightShadows.None;
                    var src = new RrLightGrid.Source
                    {
                        Name = "Rig " + r.Type,
                        Type = r.Type,
                        Position = r.Position,
                        Rotation = r.Rotation,
                        Color = r.Color,
                        Intensity = r.Intensity,
                        Range = Mathf.Max(0.1f, r.Range),
                        SpotAngle = r.SpotAngle,
                        InnerSpotAngle = -1f,
                        Specular = 1f,
                        HemiOuter = 180f,
                        Unity = l
                    };
                    RrEnvironmentSetting.UrpLight u;
                    if (s.FindUrp(r.Position, out u))
                    {
                        src.Specular = u.Specular;
                        src.Hemisphere = u.Hemisphere;
                        src.HemiInner = u.HemiInner;
                        src.HemiOuter = u.HemiOuter > 0f ? u.HemiOuter : 180f;
                        src.InnerSpotAngle = u.InnerSpot;
                    }

                    RrLightGrid.ConfigureUnity(l, src);
                    b.Sources.Add(src);
                }
            }

            b.Spawns.Add(new Pose(RrDormRoom.SpawnPoint, Quaternion.Euler(0f, RrDormRoom.SpawnYaw, 0f)));
        }

        // ------------------------------------------------------------------ The Ink Space

        /// <summary>
        /// ^TheInkSpace (2020): "a new base room you can clone and customize". Its venue is one spawnable, the hull
        /// (INK_SPACE_ROOM_HULL = (Charades_InkSpace_MainHull_Sandbox)Variant, with its 26 lights) plus the stage
        /// (INKSPACE_STAGE), placed as 3D Charades places them (hull lifted 3.24 m so the main hall floor is y 0).
        /// </summary>
        private static RrMakerTemplate DefineInkSpace()
        {
            var t = new RrMakerTemplate
            {
                Key = InkSpace,
                Title = "The Ink Space",
                Blurb = "The 3D Charades cafe venue: a cosy hall with a stage, ready to be made your own.",
                Art = "SpawnableToolTex_INK_SPACE_ROOM_HULL",
                BackFrom = new Color(0.99f, 0.8f, 0.55f),
                BackTo = new Color(0.78f, 0.42f, 0.22f),
                EnvKey = "Charades_InkSpace",
                MaxPlayers = 8,
                DefaultSpawn = new Pose(new Vector3(-5f, 0.7f, 3f), Quaternion.Euler(0f, 90f, 0f))
            };

            t.Objects = () => new List<RrMakerObject>
            {
                Piece(1, "(Charades_InkSpace_MainHull_Sandbox)Variant", new Vector3(0f, 3.24f, 0f), Quaternion.identity),
                Piece(2, "(Charades_Stage)", new Vector3(2.0f, 0.62f, 4.0f), Quaternion.identity)
            };
            return t;
        }

        // ------------------------------------------------------------------ scene bases

        private static readonly string[] ActivityDoor = { "ActivityDoor_Frame", "ActivityDoor_Glow", "ActivityDoor_Emit", "ActivityDoor_Left", "ActivityDoor_Right" };

        /// <summary>
        /// ^Park (170147): Rec Room's level75 (Android build, re-supplied 2026-10-14) from Data/Maker_Park.bytes (gtscene jt=1,
        /// thinned and cut to the park: terrace, fountain, playfield, gazebo, court, pond, cave, campfire, stage; the terrain as
        /// an H grid that RrJumbotronArea builds as floors). A fixed base. Its activity door is left out (it led to the Rec
        /// Center); players arrive where it stood, in front of the fountain terrace (-13.5, 71), facing into the park. The
        /// level has no spawn markers: the height comes from a ray down onto the built floor.
        /// </summary>
        private static RrMakerTemplate DefinePark()
        {
            var t = new RrMakerTemplate
            {
                Key = Park,
                Title = "Park",
                Blurb = "Rec Room's park: a terrace and fountain, a playfield, a gazebo, a basketball court, a pond with a waterfall and a cave, a campfire and an open-air stage.",
                Art = "SpawnableToolTex_PICNIC_TABLE",
                BackFrom = new Color(0.67f, 0.86f, 0.55f),
                BackTo = new Color(0.33f, 0.6f, 0.32f),
                EnvKey = "Maker_Park",
                MaxPlayers = 20,
                DefaultSpawn = new Pose(new Vector3(-13.5f, -3.8f, 75f), Quaternion.identity)
            };
            t.Objects = () => new List<RrMakerObject>();
            t.BuildBase = (room, b) => BuildScene(b, "Maker_Park", ActivityDoor, t.DefaultSpawn);
            return t;
        }

        /// <summary>
        /// ^PerformanceHall (170145): "a large, empty room with a stage", Rec Room's level73 from Data/Maker_PerformanceHall.bytes
        /// (72 % install meshes). A fixed base; the level's own microphone stand stays on the stage. Players arrive in the
        /// audience, facing the stage (+z, where the level's spectator cameras look).
        /// </summary>
        private static RrMakerTemplate DefinePerformanceHall()
        {
            var t = new RrMakerTemplate
            {
                Key = PerformanceHall,
                Title = "Performance Hall",
                Blurb = "A large hall with a stage, curtains and speakers: shows, talks and concerts.",
                Art = "SpawnableToolTex_STAGELIGHT_CAN",
                BackFrom = new Color(0.55f, 0.42f, 0.75f),
                BackTo = new Color(0.22f, 0.14f, 0.36f),
                EnvKey = "Maker_PerformanceHall",
                MaxPlayers = 12,
                DefaultSpawn = new Pose(new Vector3(0f, 0.05f, -9f), Quaternion.identity)
            };
            t.Objects = () => new List<RrMakerObject>();
            t.BuildBase = (room, b) => BuildScene(b, "Maker_PerformanceHall", ActivityDoor, t.DefaultSpawn);
            return t;
        }

        /// <summary>
        /// ^BowlingAlley (269686): Rec Room's own level91 from Data/Bowling.bytes, built the way Games/Bowling builds it
        /// (<see cref="RrJumbotronArea"/>) without the game: a fixed base, objects are added on top.
        /// </summary>
        private static RrMakerTemplate DefineBowling()
        {
            var t = new RrMakerTemplate
            {
                Key = Bowling,
                Title = "Bowling Alley",
                Blurb = "Lone Shoe Lanes without the game: the lanes, the seating, the lounge and the pool table, a fixed base to build in.",
                ArtOf = RrActivity.Bowling,
                ArtFit = true,
                EnvKey = "Bowling",
                MaxPlayers = 8,
                DefaultSpawn = new Pose(new Vector3(0f, 0.1f, 13.1f), Quaternion.Euler(0f, 180f, 0f))
            };
            t.Objects = () => new List<RrMakerObject>();
            t.BuildBase = (room, b) => BuildScene(b, "Bowling", ActivityDoor, new Pose(new Vector3(0f, 0.1f, 13.1f), Quaternion.Euler(0f, 180f, 0f)));
            return t;
        }

        /// <summary>
        /// ^Hangar (170158): Laser Tag's level76 from Data/LaserTag_Hangar.bytes, built by Games/LaserTag's own
        /// <see cref="RrLaserTagLevel"/> (composition: the map only - no pickups, console or game). Players arrive in its lobby.
        /// </summary>
        private static RrMakerTemplate DefineHangar()
        {
            var t = new RrMakerTemplate
            {
                Key = Hangar,
                Title = "Hangar",
                Blurb = "The Laser Tag hangar with its lobby: catwalks, ramps and the centre pit, a fixed base to build in.",
                ArtOf = RrActivity.LaserTag,
                ArtFit = false,
                EnvKey = "LaserTag_Hangar",
                MaxPlayers = 12
            };
            t.Objects = () => new List<RrMakerObject>();
            t.BuildBase = BuildHangar;
            return t;
        }

        private static IEnumerator BuildScene(RrMakerBase b, string scene, string[] skip, Pose spawn)
        {
            string raw = RrSceneBank.Text(scene);
            if (raw == null)
            {
                RrLog.Warn("MakerRooms", "no scene data \"" + scene + "\": a plain floor stands in");
                RrBuild.Floor(b.Root, "Fallback Floor", Vector3.zero, new Vector2(30f, 40f), RrLook.Solid(RrLook.Grey));
                b.Spawns.Add(spawn);
                yield break;
            }

            var area = new RrJumbotronArea(scene, Vector3.zero);
            foreach (var n in skip)
            {
                area.Skip.Add(n);
            }

            yield return area.Build(raw, b.Root, b.Surfaces);
            if (b.Root == null)
            {
                yield break;
            }

            if (area.Kit != null)
            {
                b.TakeGrid(area.Kit.Grid, b.Root.parent);
            }

            b.Spawns.Add(Grounded(b.Root.parent, spawn));
        }

        /// <summary>
        /// A spawn put on the built floor under it (a ray from 2 m above the given height - the floor's rough height, so
        /// a roof or a tree crown above it is not taken for the floor - Environment layer, 15 m down), else as given.
        /// </summary>
        public static Pose Grounded(Transform room, Pose spawn)
        {
            if (room == null)
            {
                return spawn;
            }

            Physics.SyncTransforms();
            Vector3 from = room.TransformPoint(spawn.position + Vector3.up * 2f);
            RaycastHit hit;
            if (Physics.Raycast(from, -room.up, out hit, 15f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore))
            {
                return new Pose(room.InverseTransformPoint(hit.point) + Vector3.up * 0.05f, spawn.rotation);
            }

            RrLog.Info("MakerRooms", "no floor under the spawn " + spawn.position.ToString("F1") + ": used as given");
            return spawn;
        }

        private static IEnumerator BuildHangar(RrMakerRoom room, RrMakerBase b)
        {
            var level = new RrLaserTagLevel(RrLaserTagMaps.Hangar);
            b.Level = level;
            yield return level.Build(b.Root, b.Surfaces);
            if (level.Area.Kit != null && b.Root != null)
            {
                b.TakeGrid(level.Area.Kit.Grid, b.Root.parent);
            }

            if (level.Lobby.Count > 0)
            {
                b.Spawns.AddRange(level.Lobby);
            }
            else
            {
                b.Spawns.Add(new Pose(level.Console.position + level.Console.rotation * new Vector3(0f, 0.05f, 4f), Quaternion.Euler(0f, level.Console.rotation.eulerAngles.y + 180f, 0f)));
            }
        }
    }
}
