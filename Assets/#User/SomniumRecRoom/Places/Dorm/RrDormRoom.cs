using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm
{
    /// <summary>
    /// The Dorm Room, rebuilt from Rec Room's own Maker-Pen dorm kit (rr_dorm_core.md §2-3).
    /// <para>
    /// <b>Architecture.</b> The 47 <c>(Dorm_*)</c> pieces of bundle 43ea1176... store their meshes in the original
    /// dorm's coordinates; Rec Room only re-centred each piece on a child offset. Placing each root at the inverse of
    /// that child's local transform snaps the room back together exactly (computed at run time from the loaded
    /// prefab; the §3.2 table is the fallback and the sanity check). The room frame is this transform: y 0 = floor top,
    /// interior x -4.94..4.77, z -2.96..2.96, loft at the -x end (floor 2.75), large slanted window at +x, arched
    /// window in the front wall (+z), skylight in the flat ceiling. The architecture renders with
    /// <c>AG/MakerPenShape</c> whose serialized colour is orange (Rec Room recolours it from palettes that are not in
    /// the bundles), so every piece gets a colour of ours (§2.1 table) through <see cref="RrLook.Solid"/>, which is a
    /// copy of that same maker-pen material.
    /// </para>
    /// <para>
    /// <b>Furniture: the real 2018 "Spring Cleaning" layout</b> (iteration 4). REF1's <c>DormRoom-2018</c> subroom
    /// (rr_room_exports.md §3.1/3.5) is the archivist's prop layer on a port of the official dorm scene, and its props
    /// sit exactly in this kit's room frame (markers on the loft floor at y 2.768, desk props at y 0.85, the ball in
    /// front of the pillar where the data-backed rim is), so their positions and yaws are used as they are:
    /// exit door on the left wall under the loft facing +x, the welcome mat (spawn) in front of it, armchair, coffee
    /// table with a ping-pong paddle and ball, fig and snake plant under the loft, dracaena at the loft edge, bunk bed
    /// and desk against the back wall with the radio, two cups, the water bottle and two darts on it, the mirror on the
    /// front wall under the arched window, bucket and clipboard on the floor, whiteboard + eraser + clipboard on the
    /// loft. What the export does not hold (it has no board, map or posters) goes on free wall space: the Play board on
    /// the back wall between the loft wall and the bed, the Code of Conduct, help panel and our mirror panel beside the
    /// mirror, the campus map on the front wall's right part, the notebook posters on the loft's back wall. The hoop is
    /// data-backed (the rim VFX prefabs were saved at the rim position (3.905, 1.097, -1.090)) with the office bin
    /// under it. Props people pick up are made by <see cref="RrTools.MakeTool(RrInstance)"/> and respawn home
    /// (ToolCleanup: 15 s after being displaced with nobody within 3 m, or at once when they fall out of the world).
    /// <see cref="RrDormTest.Layout"/> checks the result against the walls, the loft and each other.
    /// </para>
    /// <para>
    /// <b>The final default dorm</b> (rr_latest_places.md §2.1.2-2.3, iteration 3): the wiki's "An unmodified Dorm Room
    /// contained" list (Oct 2023 - Oct 2024) has one armchair and no dresser, "a marker in the loft", the Play board
    /// (since Jul 2023), buttons for the Privacy Policy and Help Center next to the Code of Conduct, and two social
    /// posters that are NOT palette items - the notebook-paper NUX posters <c>Nux_Stop_Gesture2</c> and
    /// <c>Nux_Shake_hands2</c> (standalone 1024^2 textures; GUESS, medium, that they are the dorm's), drawn here on
    /// our own 0.62 m quads. The loft armchair and the dresser are gone (the dresser's root beers, bell and book live
    /// on the desk) and the marker set stands by the loft whiteboard (the wiki's "the white board").
    /// </para>
    /// <para>
    /// Offline and online alike the dorm is local-only (Networked = false): nothing here is drawn for other players.
    /// </para>
    /// </summary>
    public sealed class RrDormRoom : RrRoom
    {
        /// <summary>The live dorm (null when the player is elsewhere). Used by the harness hooks.</summary>
        public static RrDormRoom Current { get; private set; }

        /// <summary>
        /// Newcomer spawn: DormRoom-2018's welcome mat (WELCOME_MAT_V2 (-3.974, 0.044, -0.921) ry 75) under the loft,
        /// 0.9 m in front of the exit door, facing into the room (+x): the hoop on the pillar straight ahead, the bunk
        /// bed on the right, the mirror on the left.
        /// </summary>
        public static readonly Vector3 SpawnPoint = new Vector3(-3.97f, 0.05f, -0.92f);
        public const float SpawnYaw = 90f;
        public static readonly Vector3 WelcomeMatPosition = new Vector3(-3.974f, 0f, -0.921f);
        public const float WelcomeMatYaw = 75.03f;

        /// <summary>
        /// The door to the Rec Center on the left wall under the loft, facing into the room (+x). DormRoom-2019's
        /// ROOM_DOOR_V2 (-4.775, 0.02, -0.425) ry 89.5 and DormRoom2021's ROOM_DOOR (-4.821, -0.01, -0.39) ry 90.6 give
        /// Rec Room's hinge-side pivots (the single door's mesh runs x -0.12..1.02 from it, iter3/doors.md), so the
        /// doorway's centre is 0.45 m further along -z, at z -0.84..-0.875 - where DormRoom-2018's "Go to Room-i-verse"
        /// INTERACTION_VOLUME (-4.681, 1.046, -0.876) and its welcome mat (z -0.921) are. RrPortal's pivot is the doorway
        /// centre; x -4.88 puts its solid box (back face 0.0535 m behind the pivot) 6.5 mm off the wall (x -4.94). The
        /// 2.05 m double door spans z -1.885..0.165: clear of the snake plant (z -2.22) and the coffee table (z 0.09,
        /// 9 cm in front of the frame).
        /// </summary>
        public static readonly Vector3 DoorPosition = new Vector3(-4.88f, 0f, -0.86f);
        public const float DoorYaw = 90f;

        /// <summary>
        /// Hoop pivot at scale 0.94, so the rim lands on the data point, 2.5 cm proud of the pillar face (z -1.44): the
        /// wall stripe (Dorm_Wall_Stripe, Box5: z -1.424..-1.418, y 1.43..1.85) stands 2.2 cm off that face and covered the
        /// backboard frame of iteration 3's flush pose; x 3.893 ends the 1.72 m backboard (x 3.033..4.753) 3 mm short of
        /// the right wall's stripe (x 4.756). The rim lands 27 mm from the data point (measured in the harness).
        /// </summary>
        public static readonly Vector3 HoopPivot = new Vector3(3.893f, 1.41f, -1.415f);
        public const float HoopScale = 0.94f;
        public static readonly Vector3 RimFromData = new Vector3(3.905f, 1.097f, -1.090f);

        public const float LoftFloorY = 2.75f;

        /// <summary>Underside of the loft floor (Dorm_Loft_Floor y 2.64..2.75, x -4.99..-2.63).</summary>
        public const float LoftUndersideY = 2.64f;

        /// <summary>
        /// The ladder in the railing gap (z 0.94..2.72) just in front of the loft edge (x -2.63). DormRoom-2018 has no
        /// ladder object (it was scene geometry), but its dracaena stands at (-2.663, 0.017, 2.227) with a 1.1 m crown in
        /// the gap's +z half, so the ladder takes the -z half: rails z 1.03..1.51, 9 cm from the railing's end and 9 cm
        /// from the plant's bounds. (Iteration 3 guessed z 1.83.)
        /// </summary>
        public static readonly Vector3 LadderPosition = new Vector3(-2.60f, 0f, 1.27f);

        /// <summary>
        /// The mirror on the front wall under the arched window, facing -z: DormRoom-2018's SANDBOX_MIRROR (the 2021
        /// Mirror prop, the same meshes as Dorm_FreeMirror) at (0.882, 1.339, 3.011) ry 180, scale 1.02; all four archive
        /// versions agree within 0.3 m. Its pivot is 5 cm behind our wall face (z 2.96) in the export; z 2.92 keeps the
        /// frame 2.5 cm off the wall as on iteration 3's back wall. Our frame spans x -0.22..1.98, y 0.16..2.53.
        /// </summary>
        public static readonly Vector3 MirrorPosition = new Vector3(0.88f, 1.34f, 2.92f);

        /// <summary>
        /// The community board (our Play board) on the back wall, facing +z. The export holds no board (it was scene
        /// geometry), the front wall now carries the mirror, and DormRoom2021 has a flat UGC piece on the back wall at
        /// (-0.825, 1.544, -2.855). The 3.05 m board is centred in the free span between the loft wall's end (x -2.57)
        /// and the bunk bed (x 0.615): x -2.495..0.555, y 0.80..2.61, 7-8 cm clear on each side.
        /// </summary>
        public static readonly Vector3 BoardPosition = new Vector3(-0.97f, 1.70f, -2.90f);

        /// <summary>
        /// The bunk bed against the back wall, long side along x. DormRoom-2018's desk props lie on a y 0.85 surface (the
        /// bed's desk top, 0.77-0.85) over x 1.33..2.47, z -2.06..-2.53, and UGC c695cd3e (GUESS the bed) stands at
        /// (2.002, 0.001, -2.140) ry 0. At yaw 0 the desk top (bed-local z -0.71..0.33) spans z -2.88..-1.84 and holds
        /// every one of them; yaw 180 would leave the radio (z -2.529) on the desk's back edge. x 1.86 ends the bed
        /// (x 0.615..3.105) 3.5 cm short of the pillar (x 3.14); z -2.17 keeps it 2 cm behind the pillar face (-1.44)
        /// and 8 cm off the wall.
        /// </summary>
        public static readonly Vector3 BedPosition = new Vector3(1.86f, 0f, -2.17f);
        public const float DeskTopY = 0.85f;

        /// <summary>The notebook-paper social posters (6a69c4f2... key 0 #-3241244070201691738 and 3c03b87a... key a #4656361576300778954, 1024^2).</summary>
        public const string StopGesturePoster = "Nux_Stop_Gesture2";
        public const string ShakeHandsPoster = "Nux_Shake_hands2";
        public const float PosterSize = 0.62f;

        /// <summary>
        /// Posters on the loft's back wall (z -2.72) under the loft: free wall in the export; x -3.15 / -3.95 keep them
        /// clear of the snake plant (x -4.71..-4.32, 1.39 m tall) in that corner.
        /// </summary>
        public static readonly Vector3 StopGesturePosterPosition = new Vector3(-3.15f, 1.45f, -2.72f);
        public static readonly Vector3 ShakeHandsPosterPosition = new Vector3(-3.95f, 1.45f, -2.72f);

        /// <summary>B43 key 1e4: ten markers standing in a 0.27 m row (Rec Room's "marker in the loft").</summary>
        public const string MarkerSet = "(SandboxMarkerSet)";

        /// <summary>At DormRoom-2018's MARKER_ERASER (-4.119, 2.768, 0.499), beside the whiteboard's +z end.</summary>
        public static readonly Vector3 MarkerSetPosition = new Vector3(-4.12f, LoftFloorY, 0.50f);

        /// <summary>
        /// DormRoom-2018's MOVEABLE_WHITEBOARD (-4.347, 3.696, -1.239) ry 91.35 on the loft: the prefab's pivot is 0.938 m
        /// above its wheels (Whiteboard_Rolling_fbx at y -0.938), so the export's pivot height puts the wheels on the loft
        /// floor (2.758). Board 1.68 x 1.12 m (DrawingSurface 3 x 2 at scale 0.56), centre 0.554 m above the pivot.
        /// </summary>
        public const string Whiteboard = "(MoveableWhiteboard)";
        public static readonly Vector3 WhiteboardPosition = new Vector3(-4.347f, LoftFloorY, -1.239f);
        public const float WhiteboardYaw = 91.35f;

        // Front wall (normal -z), left to right as seen from the room: Code of Conduct (0.71 m wide), the help panel
        // (0.32 m), the mirror (x -0.22..1.98), our mirror panel (0.46 m), the campus map (1.49 m). Gaps 7.5, 6, 14 and
        // 22 cm; the map ends 0.47 m before the right wall. The wall stripe (Dorm_Wall_Stripe Box1: z 2.952..2.958 on
        // the front wall, Box4: -2.958..-2.952 on the back wall) stands 8 mm proud: the panels hang 1 cm off the wall
        // (their backs at 2.95) and the paper-thin Code of Conduct 2 cm, or the stripe draws over them (run 1).
        public const float StripeClearance = 0.02f;

        /// <summary>The help buttons (Rec Room's Dorm_Help_Buttons: Help, Privacy Policy, Terms of Service) left of the mirror.</summary>
        public static readonly Vector3 HelpPanelPosition = new Vector3(-0.44f, 1.35f, 2.93f);
        public const float HelpPanelWidth = 0.32f;
        public static readonly Vector3 CodeOfConductPosition = new Vector3(-1.03f, 1.45f, 2.96f);
        public static readonly Vector3 MirrorPanelPosition = new Vector3(2.35f, 1.35f, 2.93f);
        public static readonly Vector3 CampusMapPosition = new Vector3(3.55f, 1.60f, 2.96f);

        /// <summary>What each help button says (Rec Room's pages went offline with its servers on 1 June 2026).</summary>
        public static readonly string[] HelpNotes =
        {
            "Rec Room's Help Center closed with its servers on 1 June 2026. Here: walk through the door to the Rec Center, or open your watch (H, or look at your wrist in VR).",
            "Rec Room's privacy policy ended with Rec Room. In this world Somnium Space's own privacy policy applies.",
            "Somnium Space's terms apply here. Rec Room's art and sounds are read from your own Rec Room install; none of it ships with this world."
        };

        // §2.1 colours (GUESS: Rec Room's palettes are not in the bundles).
        public static readonly Color WallColor = new Color(0.663f, 0.749f, 0.816f);      // #A9BFD0 pale blue-grey
        public static readonly Color CeilingColor = new Color(0.788f, 0.839f, 0.878f);   // #C9D6E0
        public static readonly Color FrameColor = new Color(0.949f, 0.949f, 0.949f);     // #F2F2F2 windows, shelves
        public static readonly Color FloorColor = new Color(0.659f, 0.455f, 0.290f);     // #A8744A warm wood
        public static readonly Color TrimColor = new Color(0.929f, 0.929f, 0.929f);      // #EDEDED skirting
        public static readonly Color BeamColor = new Color(0.431f, 0.290f, 0.188f);      // #6E4A30 dark wood
        public static readonly Color StripeColor = new Color(0.961f, 0.961f, 0.961f);    // #F5F5F5

        private static readonly Quaternion Y90 = new Quaternion(0f, 0.7071068f, 0f, 0.7071068f);
        private static readonly Quaternion YMinus90 = new Quaternion(0f, -0.7071068f, 0f, 0.7071068f);
        private static readonly Quaternion Y180 = new Quaternion(0f, 1f, 0f, 0f);
        private static readonly Quaternion X90 = new Quaternion(0.7071068f, 0f, 0f, 0.7071068f);

        private struct Piece
        {
            public string Name;
            public Vector3 Pos;
            public Quaternion Rot;
            public Color Color;

            public Piece(string name, Vector3 pos, Quaternion rot, Color color)
            {
                Name = name;
                Pos = pos;
                Rot = rot;
                Color = color;
            }
        }

        /// <summary>§3.2: the architecture set (full walls, not the _Down/_Up split; the whole skirting, not _1.._7).</summary>
        private static readonly Piece[] Architecture =
        {
            new Piece("(Dorm_Floor)", Vector3.zero, Quaternion.identity, FloorColor),
            new Piece("(Dorm_Floor_Trim)", new Vector3(-0.080f, 0.061f, 0f), Quaternion.identity, TrimColor),
            new Piece("(Dorm_Wall_1)", new Vector3(-4.998f, 0f, -0.002f), Y90, WallColor),
            new Piece("(Dorm_Wall_2)", Vector3.zero, Quaternion.identity, WallColor),
            new Piece("(Dorm_Wall_3)", new Vector3(4.998f, 0f, -0.002f), YMinus90, WallColor),
            new Piece("(Dorm_Wall_4)", new Vector3(0f, 0f, 3f), Y180, WallColor),
            new Piece("(Dorm_Wall_Loft_1)", new Vector3(-4f, 0f, -3f), Quaternion.identity, WallColor),
            new Piece("(Dorm_Wall_Loft_2)", new Vector3(-4f, 0f, 3f), Y180, WallColor),
            new Piece("(Dorm_Wall_Pillar)", Vector3.zero, Quaternion.identity, WallColor),
            new Piece("(Dorm_Wall_Stripe)", new Vector3(0f, 1.35f, 0f), Quaternion.identity, StripeColor),
            new Piece("(Dorm_Ceiling_Flat)", new Vector3(-2f, 5.998f, -0.002f), X90, CeilingColor),
            new Piece("(Dorm_Ceiling_Angled)", new Vector3(2.991f, 5.2f, -0.002f), new Quaternion(0.354f, -0.612f, 0.354f, 0.612f), CeilingColor),
            new Piece("(Dorm_Skylight_Frame)", new Vector3(-1.75f, 6.498f, 0.798f), X90, FrameColor),
            new Piece("(Dorm_Skylight_Window)", new Vector3(-1.8f, 7f, 0.8f), Quaternion.identity, FrameColor),
            new Piece("(Dorm_Window_Large_Frame)", new Vector3(2.999f, 2.5f, 0.699f), YMinus90, FrameColor),
            new Piece("(Dorm_Window_Large_Panes)", new Vector3(3.53f, 4f, 0.76f), Quaternion.identity, FrameColor),
            new Piece("(Dorm_Window_Small_Frame)", new Vector3(0f, 3.6f, 3f), Y180, FrameColor),
            new Piece("(Dorm_Window_Small_Panes)", new Vector3(0f, 3.8f, 3f), Y180, FrameColor),
            new Piece("(Dorm_Loft_Floor)", new Vector3(-3.805f, 2.696f, 0.001f), Y90, FloorColor),
            new Piece("(Dorm_Loft_Beam)", new Vector3(-2.719f, 2.5f, -0.001f), Y90, BeamColor),
            new Piece("(Dorm_Loft_Railing)", new Vector3(-2.71f, 3.083f, -0.878f), Y90, WallColor),
            new Piece("(Dorm_Loft_Shelves)", new Vector3(-3.999f, 3.5f, -0.001f), Y90, FrameColor)
        };

        /// <summary>True for the kit's architecture pieces (walls, floors, ceiling, windows, loft) and the light shafts.</summary>
        public static bool IsArchitecture(string prefab)
        {
            foreach (var p in Architecture)
            {
                if (p.Name == prefab)
                {
                    return true;
                }
            }

            return prefab == "(Dorm_Light_Shafts)";
        }

        private readonly List<string> _missing = new List<string>();
        private readonly List<RrGrabbable> _props = new List<RrGrabbable>();
        private readonly Dictionary<string, RrInstance> _named = new Dictionary<string, RrInstance>();
        private int _pending;
        private int _pieces;
        private float _maxSnapError;
        private RrPortal _door;
        private AudioSource _ambience;
        private Transform _ambienceAnchor;
        private Material _floorMaterial;
        private readonly List<Material> _owned = new List<Material>();
        private TMPro.TextMeshPro _helpNote;
        private float _helpUntil;

        public RrPortal Door { get { return _door; } }

        /// <summary>Notebook posters whose texture arrived (of 2), and those that fell back to the 2018 Sandbox posters.</summary>
        public int PostersShown { get; private set; }
        public int PostersFallback { get; private set; }

        /// <summary>The help panel's note as shown ("" when idle).</summary>
        public string HelpText { get { return _helpNote != null && _helpNote.gameObject.activeSelf ? _helpNote.text : ""; } }
        public RrDormMirror Mirror { get; private set; }
        public RrDormRadio Radio { get; private set; }
        public RrDormLadder Ladder { get; private set; }
        public RrDormBoard Board { get; private set; }
        public IReadOnlyList<RrGrabbable> Props { get { return _props; } }
        public IReadOnlyList<string> Missing { get { return _missing; } }

        /// <summary>A spawned Rec Room object by prefab name (first one), or null.</summary>
        public RrInstance Named(string prefab)
        {
            RrInstance inst;
            return _named.TryGetValue(prefab, out inst) ? inst : null;
        }

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            StopAmbience();
            if (_floorMaterial != null)
            {
                Destroy(_floorMaterial);
            }

            foreach (var m in _owned)
            {
                if (m != null)
                {
                    Destroy(m);
                }
            }

            _owned.Clear();

            if (Current == this)
            {
                Current = null;
            }
        }

        public override Pose Spawn(int index)
        {
            // Index 0 (the dorm is single-player, so everyone) stands dead centre - on the welcome mat, or straight in
            // front of the door; 1 and 2 go 0.6 m to either side. (Iteration 3's (index % 3 - 1) put index 0 0.6 m aside.)
            float lateral = ((index + 1) % 3 - 1) * 0.6f;
            // Back from somewhere: step out of the door (it plays its arrival swing). First login: the spawn point.
            if (RrTravel.Previous != RrActivity.None && RrTravel.Previous != RrActivity.Dorm && _door != null && _door.Built)
            {
                var world = _door.FrontPose(1.5f, lateral);
                return new Pose(transform.InverseTransformPoint(world.position) + Vector3.up * 0.05f,
                    Quaternion.Inverse(transform.rotation) * world.rotation);
            }

            // Side by side on the welcome mat: the offsets run across the facing direction (along z here).
            var facing = Quaternion.Euler(0f, SpawnYaw, 0f);
            return new Pose(SpawnPoint + facing * new Vector3(lateral, 0f, 0f), facing);
        }

        public override void OnLocalEnter()
        {
            if (_door != null && RrTravel.Previous != RrActivity.None && RrTravel.Previous != RrActivity.Dorm)
            {
                _door.PlayArrive();
            }

            StartAmbience();
            if (Radio != null)
            {
                Radio.PlayOnEnter();
            }
        }

        public override void OnLocalExit()
        {
            StopAmbience();
            if (Radio != null)
            {
                Radio.Stop();
            }
        }

        public override IEnumerator Build()
        {
            float started = Time.realtimeSinceStartup;
            BuildProgress = 0f;
            var portalLoad = RrPortal.Prepare();
            while (portalLoad.MoveNext())
            {
                BuildProgress = 0.05f;
                yield return portalLoad.Current;
            }

            // 1. The campus outside the windows (procedural, a few ms).
            RrDormOutdoors.Build(transform);
            BuildProgress = 0.1f;
            yield return null;

            // 2. Architecture from the kit. The floors try Rec Room's own dorm wood (Wood_Floor_Matte_mat, §2.1: in the
            //    kit's bundle, implicit); without it they are painted wood-coloured maker-pen surfaces.
            if (RrAssets.Instance != null)
            {
                Material wood = null;
                yield return RrAssets.Instance.LoadRoutine<Material>("Wood_Floor_Matte_mat", 21, m => wood = m);
                if (wood != null)
                {
                    _floorMaterial = RrMaterials.Fix(new Material(wood) { name = "Dorm Floor (Wood_Floor_Matte_mat)" });
                }

                RrLog.Info("Dorm", "floor: " + (_floorMaterial != null ? "Rec Room's Wood_Floor_Matte_mat (" + (_floorMaterial.shader != null ? _floorMaterial.shader.name : "?") + ")" : "painted wood colour (Wood_Floor_Matte_mat not loadable)"));
            }

            foreach (var p in Architecture)
            {
                var piece = p;
                SpawnPiece(piece.Name, piece.Pos, piece.Rot, RrSpawnOptions.Scenery(), inst =>
                {
                    SnapToRoomFrame(inst, piece.Pos, piece.Rot);
                    Paint(inst, piece.Color, piece.Name == "(Dorm_Floor)" ? _floorMaterial : null); // the loft floor's UVs show green edges with it
                    CleanScenery(inst);
                });
            }

            SpawnPiece("(Dorm_Light_Shafts)", Vector3.zero, Quaternion.identity, RrSpawnOptions.Visual(), inst =>
            {
                SnapToRoomFrame(inst, Vector3.zero, Quaternion.identity);
                HideEditorMeshes(inst);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            });

            yield return WaitPending(60f, 0.1f, 0.45f);

            // 3. Furniture (static scenery).
            BuildFurniture();
            yield return WaitPending(60f, 0.45f, 0.7f);

            // 4. Our own parts: lights, door, board, mirror, ladder climb.
            BuildLights();
            _door = RrPortal.Create(transform, RrActivity.RecCenter, DoorPosition, DoorYaw, "REC CENTER");
            StartCoroutine(FitDoorUnderLoft());
            yield return null;
            // The board's face is 5 cm in front of its pivot (mesh depth 0.10): the UI 1 cm in front of that, facing +z.
            Board = RrDormBoard.Build(transform, BoardPosition + new Vector3(0f, 0f, 0.06f), Y180);
            BuildProgress = 0.75f;
            yield return null;

            // 5. Props people pick up.
            BuildProps();
            yield return WaitPending(60f, 0.75f, 0.95f);

            BuildMirrorPanel();
            BuildHelpPanel();
            StartCoroutine(SettleProps());
            BuildProgress = 1f;
            RrLog.Info("Dorm", "layout: DormRoom-2018 (REF1) - door " + DoorPosition.ToString("F2") + " facing +x, spawn on the welcome mat " +
                SpawnPoint.ToString("F2") + ", bed " + BedPosition.ToString("F2") + " on the back wall, mirror " + MirrorPosition.ToString("F2") +
                " on the front wall, Play board " + BoardPosition.ToString("F2") + ", ladder z " + LadderPosition.z.ToString("0.00"));
            RrLog.Info("Dorm", "built at (" + Origin.x.ToString("0") + ", " + Origin.z.ToString("0") + "): " + _pieces +
                " Rec Room objects, " + _props.Count + " props, max kit snap error " + (_maxSnapError * 1000f).ToString("0") + " mm, " +
                (Time.realtimeSinceStartup - started).ToString("0.0") + " s" +
                (_missing.Count > 0 ? "; missing: " + string.Join(", ", _missing) : "") +
                (RrPortal.TemplateReady ? "" : " (procedural door)"));
        }

        // ------------------------------------------------------------------ furniture

        private void BuildFurniture()
        {
            var scenery = RrSpawnOptions.Scenery();
            // Positions and yaws below without a comment of their own are DormRoom-2018's (rr_room_exports §3.1).
            Scenery("(Dorm_Bunk_Bed)", BedPosition, 0f, 0f, null);
            Scenery("(Dorm_Rug)", new Vector3(0.30f, 0f, 0.10f), 0f, 0f, null); // iteration 3: the middle of the main floor
            SpawnPiece("(Dorm_Ladder)", LadderPosition, Quaternion.Euler(0f, 90f, 0f), scenery, inst =>
            {
                CleanScenery(inst);
                Ladder = inst.gameObject.AddComponent<RrDormLadder>();
                Ladder.Setup(this, inst);
            });

            // Fluorescent fixtures: pivot at the top face (hangs 0.11 m down).
            Scenery("(Dorm_Fluorescent_Light)", new Vector3(-3.80f, 2.64f, 0f), 0f, float.NaN, null);
            Scenery("(Dorm_Fluorescent_Light)", new Vector3(-1.20f, 6.13f, -1.95f), 0f, float.NaN, null);
            Scenery("(Dorm_Fluorescent_Light)", new Vector3(-3.80f, 6.13f, -1.95f), 0f, float.NaN, null);

            // Front wall (normal -z): the mirror under the arched window, the Code of Conduct and the campus map
            // (the help and mirror panels are ours, built after the props).
            SpawnPiece("(Dorm_FreeMirror)", MirrorPosition, Y180, scenery, inst =>
            {
                CleanScenery(inst);
                Mirror = inst.gameObject.AddComponent<RrDormMirror>();
                Mirror.Setup(inst);
            });
            Wall("(Sandbox_CodeOfConduct_Poster)", CodeOfConductPosition, Vector3.back, StripeClearance);
            Wall("(Dorm_Campus_Map)", CampusMapPosition, Vector3.back, 0.012f);

            // Back wall (normal +z): the community board = our Play board.
            SpawnPiece("(Dorm_Community_Board)", BoardPosition, Quaternion.identity, scenery, CleanScenery);

            // Under the loft, back wall (z -2.72): the two social posters (Block/Mute and Friends), notebook style.
            Poster(StopGesturePoster, StopGesturePosterPosition, "(Sandbox_MuteGesture_Poster)");
            Poster(ShakeHandsPoster, ShakeHandsPosterPosition, "(Sandbox_ShakeHands_Poster)");

            // The hoop over the dustbin (data-backed rim position).
            var hoopOptions = RrSpawnOptions.Scenery();
            hoopOptions.Scale = HoopScale;
            SpawnPiece("(Basketballhoop)", HoopPivot, Quaternion.identity, hoopOptions, inst =>
            {
                CleanScenery(inst);
                var hoop = inst.gameObject.AddComponent<RrHoop>();
                hoop.Setup(inst);
                float err = Vector3.Distance(transform.InverseTransformPoint(hoop.RimCenter), RimFromData);
                RrLog.Info("Dorm", "hoop rim at room " + transform.InverseTransformPoint(hoop.RimCenter).ToString("F3") +
                    " (Rec Room's rim VFX: " + RimFromData.ToString("F3") + ", off by " + (err * 1000f).ToString("0") + " mm)");
            });

            // Under the loft: one armchair (the wiki's "an armchair") facing the room diagonally, the coffee table
            // (TABLE_SHORT at scale 0.815: 0.99 x 0.36 x 0.51 m), fig and snake plant against the left wall, the
            // dracaena at the loft edge, the welcome mat in front of the door. The dracaena's z is 2.10, not 2.227: its
            // crown (radius 0.555 m, centred 5.5 cm to +z of the pivot at this yaw, measured) would reach 11 cm into the
            // loft's front wall (z 2.72); at 2.10 it ends 1 cm short of it and 6 cm from the ladder.
            Scenery("(Sandbox_CouchChair)", new Vector3(-3.733f, 0f, 1.962f), 132.64f, 0f, null);
            Scenery("(Sandbox_Table_Short)", new Vector3(-4.39f, 0f, 0.585f), 90f, 0f, null, 0.815f);
            Scenery("(Sandbox_Houseplant_Fig)", new Vector3(-4.422f, 0f, 1.429f), 129.18f, 0f, null);
            Scenery("(Sandbox_Houseplant_SnakePlant)", new Vector3(-4.511f, 0f, -2.427f), 89.73f, 0f, null);
            Scenery("(Sandbox_Houseplant_Dracaena)", new Vector3(-2.663f, 0f, 2.10f), 146.86f, 0f, null);
            SpawnPiece("(SandboxWelcomeMat)", WelcomeMatPosition, Quaternion.Euler(0f, WelcomeMatYaw, 0f), RrSpawnOptions.Visual(), inst =>
            {
                HideEditorMeshes(inst);
                Ground(inst, 0.002f); // 1 cm thick, no collider: nothing to trip the avatar
            });

            // The loft: the whiteboard on its left wall and the marker set ("pick up a marker in the loft", the
            // new-player challenge) beside it. No dresser in the final dorm (absent from the 2023/2024 wiki list).
            Scenery(Whiteboard, WhiteboardPosition, WhiteboardYaw, LoftFloorY, null);
            Scenery(MarkerSet, MarkerSetPosition, 90f, LoftFloorY, null);
        }

        private void BuildProps()
        {
            // The bunk bed's desk top (y 0.85) spans x 0.615..3.105, z -2.88..-1.84; its posts stand at the corners
            // (x 0.615..0.765 / 2.955..3.105, z -2.885..-2.735 / -1.605..-1.455).
            const float desk = DeskTopY;

            // Floor (export): the ball in front of the pillar, the bucket by the back wall, a clipboard near the front
            // wall; the office bin under the data-backed rim and the frisbee on the rug are iteration 3's.
            Prop("(Basketball)", new Vector3(3.409f, 0f, -1.142f), 270f, 0f, null);
            Prop("(TrashCan_Office)SandboxVariant", new Vector3(3.905f, 0f, -1.10f), 0f, 0f, null);
            Prop("(Bucket)", new Vector3(-2.163f, 0f, -2.636f), 306.16f, 0f, null);
            Prop("(DrawableClipboard)", new Vector3(2.887f, 0f, 1.913f), 193f, 0f, null);
            Prop("(DiscGolfDisc)", new Vector3(0.9f, 0f, 0.4f), 0f, 0.012f, null);

            // The desk (export): radio, two cups, the water bottle, two darts. The darts were stuck in at angles
            // (y 0.95 / 1.03); lying flat they would touch end to end, so the second moves 0.1 m along z.
            Prop("(Radio)", new Vector3(1.381f, 0f, -2.529f), 308.04f, desk, inst =>
            {
                Radio = inst.gameObject.AddComponent<RrDormRadio>();
                Radio.Setup(inst);
            });
            Prop("(SoloCup)", new Vector3(2.157f, 0f, -2.25f), 182.11f, desk, null);
            Prop("(SoloCup)", new Vector3(2.469f, 0f, -2.256f), 168.37f, desk, null);
            Prop("(WaterBottle)", new Vector3(1.331f, 0f, -2.055f), 133.02f, desk, null);
            Prop("(Dart)", new Vector3(1.906f, 0f, -2.10f), 86.01f, desk, null);
            Prop("(Dart)", new Vector3(2.057f, 0f, -2.00f), 86.01f, desk, null);

            // Iteration 3's desk things (the dresser's root beers, bell and book; lamp, pizza, apple, dice) on the
            // desk's two free ends, clear of the posts and of the radio (x 1.22..1.54, z -2.675..-2.383).
            Prop("(Consumables_RootBeer_Bottle)", new Vector3(0.90f, 0f, -2.45f), 0f, desk, null);
            Prop("(Consumables_RootBeer_Bottle)", new Vector3(0.98f, 0f, -2.68f), 40f, desk, null);
            Prop("(Sandbox_Bell)", new Vector3(1.08f, 0f, -2.28f), 0f, desk, null);
            Prop("(Sandbox_Book_Green)", new Vector3(0.82f, 0f, -2.06f), 90f, desk, null);
            Prop("(Dorm_Desk_Lamp)", new Vector3(2.80f, 0f, -2.68f), 0f, desk, inst =>
            {
                var bulb = inst.Find("Desk_Lamp_Bulb_LOD0");
                var at = bulb != null ? bulb : inst.transform;
                var light = RrBuild.PointLight(at, "Lamp Light", bulb != null ? Vector3.zero : new Vector3(0f, 0.3f, 0.15f),
                    new Color(1f, 0.86f, 0.62f), 2.2f, 0.9f);
                light.renderMode = LightRenderMode.ForcePixel;
            });
            Prop("(Consumables_CheesePizza_Slice)", new Vector3(2.72f, 0f, -2.05f), 20f, desk, null);
            Prop("(Consumables_Apple_Red)", new Vector3(2.60f, 0f, -2.45f), 0f, desk, null);
            Prop("(SandboxDiceSet)", new Vector3(2.86f, 0f, -2.28f), 15f, desk, null);

            // The coffee table (export): a ping-pong paddle and ball on its top.
            float table = SurfaceTop("(Sandbox_Table_Short)", 0.36f);
            Prop("(Pingpong_Paddle)", new Vector3(-4.396f, 0f, 0.861f), 278.69f, table, null);
            Prop("(PingpongBall)", new Vector3(-4.337f, 0f, 0.382f), 90.51f, table, null);

            // The loft (export): the whiteboard's eraser and a clipboard; iteration 3's trophy and book by the back wall.
            Prop("(Eraser)", new Vector3(-3.355f, 0f, -2.136f), 253.03f, LoftFloorY, null);
            Prop("(DrawableClipboard)", new Vector3(-3.757f, 0f, 1.31f), 72f, LoftFloorY, null);
            Prop("(Trophy_Oscar)", new Vector3(-3.35f, 0f, -2.45f), 90f, LoftFloorY, null);
            Prop("(Sandbox_Book_Yellow)", new Vector3(-3.9f, 0f, -2.5f), 90f, LoftFloorY, null);
        }

        /// <summary>The room-frame top of a spawned piece (its renderers' bounds), or <paramref name="fallback"/>.</summary>
        private float SurfaceTop(string prefab, float fallback)
        {
            var inst = Named(prefab);
            if (inst == null)
            {
                return fallback;
            }

            var b = inst.WorldBounds();
            return b.size == Vector3.zero ? fallback : transform.InverseTransformPoint(b.max).y;
        }

        /// <summary>
        /// The activity door's sign box spans y 2.45..2.80 (centre 2.625, 0.346 m tall), but under the loft the ceiling
        /// is the loft floor's underside at 2.64: the sign would be cut in half and poke 5 cm through the loft floor.
        /// Once the look is built, the sign (the template's <c>[Door_Sign]</c>, or the procedural Sign box and Label)
        /// drops until its top is 3 cm under the underside (the doorway's frame tops out at 2.14 m), and the door's
        /// transient notice moves from 3.1 m (on the loft) to eye height in front of the leaves.
        /// </summary>
        private IEnumerator FitDoorUnderLoft()
        {
            float until = Time.realtimeSinceStartup + 60f;
            while (_door != null && !_door.Built && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            if (_door == null || !_door.Built)
            {
                yield break;
            }

            var parts = new List<Transform>();
            var sign = FindDeep(_door.transform, "[Door_Sign]");
            if (sign != null)
            {
                parts.Add(sign);
            }
            else
            {
                var look = FindDeep(_door.transform, "Look (procedural)");
                var box = look != null ? look.Find("Sign") : null;
                var label = _door.transform.Find("Label");
                if (box != null)
                {
                    parts.Add(box);
                }

                if (label != null)
                {
                    parts.Add(label);
                }
            }

            float top = float.MinValue;
            foreach (var part in parts)
            {
                foreach (var r in part.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.enabled && r.GetComponent<TMPro.TMP_Text>() == null)
                    {
                        top = Mathf.Max(top, transform.InverseTransformPoint(r.bounds.max).y);
                    }
                }
            }

            if (parts.Count == 0 || top == float.MinValue)
            {
                RrLog.Warn("Dorm", "door sign not found: left as built");
                yield break;
            }

            float drop = Mathf.Max(0f, top - (LoftUndersideY - 0.03f));
            foreach (var part in parts)
            {
                part.position -= transform.up * drop;
            }

            var notice = _door.transform.Find("Notice");
            if (notice != null)
            {
                notice.localPosition = new Vector3(0f, 1.75f, 0.35f);
            }

            RrLog.Info("Dorm", "door sign lowered " + (drop * 100f).ToString("0") + " cm under the loft (top y " + (top - drop).ToString("0.00") +
                ", underside " + LoftUndersideY.ToString("0.00") + ", " + (_door.Procedural ? "procedural" : "Rec Room's") + " door)");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ our own parts

        private void BuildLights()
        {
            var group = RrBuild.Group(transform, "Lights").transform;
            var warm = new Color(1f, 0.95f, 0.86f);
            // Under the loft (the fixture at y 2.64), the main ceiling fixture, a soft fill under the slope.
            RrBuild.PointLight(group, "Loft Underside", new Vector3(-3.8f, 2.35f, 0f), warm, 5.5f, 1.1f);
            RrBuild.PointLight(group, "Ceiling", new Vector3(-1.2f, 5.6f, -1.2f), warm, 10f, 1.3f);
            RrBuild.PointLight(group, "Fill", new Vector3(1.8f, 3.2f, 0.4f), new Color(0.92f, 0.95f, 1f), 9f, 0.7f);

            // The sun through the large window, along the light shafts' direction (-0.68, -0.73, 0) (§3.1).
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
        }

        private void BuildMirrorPanel()
        {
            // Rec Room's mirror buttons (Customize, Outfits, Store...) would edit the avatar, which Dreamport never does.
            // Beside the mirror we keep a small panel of our own: mirror on/off, props home, Rec Center. On the front
            // wall its front (local -z) faces the room with no turn.
            var panel = RrBuild.Group(transform, "Mirror Panel", MirrorPanelPosition, Quaternion.identity).transform;
            RrBuild.Box(panel, "Back", new Vector3(0f, 0f, 0.01f), new Vector3(0.46f, 1.10f, 0.02f), RrLook.Solid(new Color(0.95f, 0.94f, 0.93f)), false);
            RrBuild.Box(panel, "Header", new Vector3(0f, 0.49f, -0.002f), new Vector3(0.46f, 0.12f, 0.01f), RrLook.Solid(new Color(0.878f, 0.424f, 0.129f)), false);
            RrBuild.Sign(panel, "Title", "MIRROR", new Vector3(0f, 0.49f, -0.01f), Quaternion.identity, 0.55f, Color.white, true, 0.44f);
            var down = Quaternion.Euler(-90f, 0f, 0f);
            var text = new Color(0.224f, 0.224f, 0.224f);
            RrPressable.Create(panel, "Mirror Toggle", new Vector3(0f, 0.25f, 0f), down, new Color(0.878f, 0.424f, 0.129f), 0.14f, () =>
            {
                if (Mirror != null)
                {
                    Mirror.Enabled = !Mirror.Enabled;
                }
            });
            RrBuild.Sign(panel, "Mirror Label", "MIRROR ON / OFF", new Vector3(0f, 0.14f, -0.01f), Quaternion.identity, 0.34f, text, false, 0.44f);
            RrPressable.Create(panel, "Props Home", new Vector3(0f, -0.06f, 0f), down, RrLook.Teal, 0.14f, RespawnProps);
            RrBuild.Sign(panel, "Props Label", "PUT THINGS BACK", new Vector3(0f, -0.17f, -0.01f), Quaternion.identity, 0.34f, text, false, 0.44f);
            RrPressable.Create(panel, "Rec Center", new Vector3(0f, -0.37f, 0f), down, RrLook.Blue, 0.14f, () => RrTravel.GoTo(RrActivity.RecCenter));
            RrBuild.Sign(panel, "Rec Center Label", "GO TO REC CENTER", new Vector3(0f, -0.48f, -0.01f), Quaternion.identity, 0.34f, text, false, 0.44f);
        }

        /// <summary>
        /// Rec Room's help buttons (<c>(Dorm_Help_Buttons)</c>, B43 0ed: Help, Privacy Policy, Terms of Service, next
        /// to the Code of Conduct). Rec Room's own is an invisible volume with an unthemed canvas, so this is a panel
        /// in the MIRROR panel's style, 0.32 x 1.10 m, between the mirror and the Code of Conduct; each button shows a short
        /// note for 12 s (the pages they opened went offline with Rec Room).
        /// </summary>
        private void BuildHelpPanel()
        {
            const float w = HelpPanelWidth;
            var panel = RrBuild.Group(transform, "Help Panel", HelpPanelPosition, Quaternion.identity).transform;
            var accent = new Color(0.878f, 0.424f, 0.129f);
            var text = new Color(0.224f, 0.224f, 0.224f);
            RrBuild.Box(panel, "Back", new Vector3(0f, 0f, 0.01f), new Vector3(w, 1.10f, 0.02f), RrLook.Solid(new Color(0.95f, 0.94f, 0.93f)), false);
            RrBuild.Box(panel, "Header", new Vector3(0f, 0.49f, -0.002f), new Vector3(w, 0.12f, 0.01f), RrLook.Solid(accent), false);
            RrBuild.Sign(panel, "Title", "HELP", new Vector3(0f, 0.49f, -0.01f), Quaternion.identity, 0.55f, Color.white, true, w - 0.02f);
            var down = Quaternion.Euler(-90f, 0f, 0f);
            string[] labels = { "HELP", "PRIVACY POLICY", "TERMS OF SERVICE" };
            Color[] caps = { accent, RrLook.Teal, RrLook.Blue };
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i;
                float y = 0.30f - i * 0.22f;
                RrPressable.Create(panel, "Help " + labels[i], new Vector3(0f, y, 0f), down, caps[i], 0.12f, () => ShowHelp(k));
                var label = RrBuild.Sign(panel, "Help Label " + i, labels[i], new Vector3(0f, y - 0.1f, -0.01f), Quaternion.identity, 0.28f, text, false, w - 0.02f);
                label.rectTransform.sizeDelta = new Vector2(w - 0.02f, 0.05f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.14f;
                label.fontSizeMax = 0.28f;
                label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            }

            _helpNote = RrBuild.Sign(panel, "Help Note", "", new Vector3(0f, -0.39f, -0.012f), Quaternion.identity, 0.16f, text, false, w - 0.03f);
            _helpNote.rectTransform.sizeDelta = new Vector2(w - 0.03f, 0.26f);
            _helpNote.alignment = TMPro.TextAlignmentOptions.Top;
            _helpNote.enableAutoSizing = true;
            _helpNote.fontSizeMin = 0.09f;
            _helpNote.fontSizeMax = 0.16f;
            _helpNote.gameObject.SetActive(false);
        }

        /// <summary>A help button's note (0 Help, 1 Privacy Policy, 2 Terms of Service), shown for 12 s.</summary>
        public void ShowHelp(int which)
        {
            if (_helpNote == null || which < 0 || which >= HelpNotes.Length)
            {
                return;
            }

            _helpNote.text = HelpNotes[which];
            _helpNote.gameObject.SetActive(true);
            _helpUntil = Time.realtimeSinceStartup + 12f;
            RrTools.Once("Dorm", "help:" + which, "help button " + which + " pressed: note shown");
        }

        private void Update()
        {
            if (_helpNote != null && _helpNote.gameObject.activeSelf && Time.realtimeSinceStartup >= _helpUntil)
            {
                _helpNote.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// A notebook poster on the wall: a 0.62 m quad 1 cm off it, facing into the room, lit like the walls (a
        /// copy of <see cref="RrLook.Emissive"/>'s URP Lit with no emission, so no new shader variant). When the
        /// texture cannot be loaded, the 2018 Sandbox poster of the same gesture takes its place.
        /// </summary>
        private void Poster(string texture, Vector3 wallPoint, string fallbackPrefab)
        {
            var baseMat = RrLook.Emissive(Color.white, 0f);
            var m = new Material(baseMat) { name = "Dorm Poster " + texture };
            m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.93f));
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", Color.black);
            }

            _owned.Add(m);
            var quad = RrBuild.Primitive(PrimitiveType.Quad, transform, "Poster " + texture, wallPoint + new Vector3(0f, 0f, 0.01f), Y180,
                new Vector3(PosterSize, PosterSize, 1f), m, false, RrLayers.Environment);
            quad.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (RrAssets.Instance == null)
            {
                Destroy(quad);
                Wall(fallbackPrefab, wallPoint, Vector3.forward);
                PostersFallback++;
                return;
            }

            RrAssets.Instance.Load<Texture2D>(texture, 28, tex =>
            {
                if (this == null || quad == null)
                {
                    return;
                }

                if (tex == null)
                {
                    Destroy(quad);
                    Wall(fallbackPrefab, wallPoint, Vector3.forward);
                    PostersFallback++;
                    RrLog.Warn("Dorm", "poster " + texture + " not loadable: the 2018 " + fallbackPrefab + " instead");
                    return;
                }

                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
                PostersShown++;
                RrTools.Once("Dorm", "poster:" + texture, "poster " + texture + " (" + tex.width + "x" + tex.height + ", " + tex.format + ") on the loft wall");
            });
        }

        /// <summary>Sends every prop home at once (the mirror panel's button; ToolCleanup does it one by one).</summary>
        public void RespawnProps()
        {
            int n = 0;
            foreach (var g in _props)
            {
                if (g == null || g.IsHeld)
                {
                    continue;
                }

                var r = g.GetComponent<RrRespawnable>();
                if (r != null && r.Displaced())
                {
                    r.Respawn(true);
                    n++;
                }
            }

            RrLog.Info("Dorm", "put " + n + " prop(s) back");
        }

        private IEnumerator SettleProps()
        {
            // Props were dropped from 1 cm above their surfaces: let them come to rest, then make that their home.
            yield return new WaitForSeconds(2.5f);
            int n = 0;
            foreach (var g in _props)
            {
                var r = g != null ? g.GetComponent<RrRespawnable>() : null;
                if (r != null && !g.IsHeld)
                {
                    r.SetHome();
                    n++;
                }
            }

            RrTools.Once("Dorm", "settled", n + " prop homes set after settling");
        }

        private void StartAmbience()
        {
            StopAmbience();
            if (_ambienceAnchor == null)
            {
                _ambienceAnchor = RrBuild.Group(transform, "Ambience (window)", new Vector3(3.6f, 4.2f, 0.7f)).transform;
            }

            // rr_reccenter §2.6: AmbienceRadio's "Outdoor Birds" = AmbianceSummerOutdoor (117.8 s loop), kept low.
            _ambience = RrSound.Loop("AmbianceSummerOutdoor", _ambienceAnchor, 0.22f, 22f);
        }

        private void StopAmbience()
        {
            if (_ambience != null)
            {
                RrSound.Stop(_ambience);
                _ambience = null;
            }
        }

        // ------------------------------------------------------------------ spawning helpers

        private void SpawnPiece(string prefab, Vector3 localPos, Quaternion localRot, RrSpawnOptions options, Action<RrInstance> then)
        {
            _pending++;
            RrSpawn.Spawn(prefab, transform, localPos, localRot, inst =>
            {
                _pending--;
                if (this == null)
                {
                    if (inst != null)
                    {
                        Destroy(inst.gameObject);
                    }

                    return;
                }

                if (inst == null)
                {
                    _missing.Add(prefab);
                    return;
                }

                _pieces++;
                if (!_named.ContainsKey(prefab))
                {
                    _named[prefab] = inst;
                }

                try
                {
                    if (then != null)
                    {
                        then(inst);
                    }
                }
                catch (Exception e)
                {
                    RrLog.Warn("Dorm", prefab + ": " + e.GetType().Name + " " + e.Message);
                }
            }, options);
        }

        /// <summary>Static furniture. <paramref name="groundY"/>: NaN keeps the pivot, else the lowest point lands on it.</summary>
        private void Scenery(string prefab, Vector3 pos, float yaw, float groundY, Action<RrInstance> then, float scale = 1f)
        {
            var options = RrSpawnOptions.Scenery();
            options.Scale = scale;
            SpawnPiece(prefab, pos, Quaternion.Euler(0f, yaw, 0f), options, inst =>
            {
                CleanScenery(inst);
                if (!float.IsNaN(groundY))
                {
                    Ground(inst, groundY);
                }

                if (then != null)
                {
                    then(inst);
                }
            });
        }

        /// <summary>A wall-mounted piece: centred on <paramref name="wallPoint"/>, its back <paramref name="standoff"/> m off the wall.</summary>
        private void Wall(string prefab, Vector3 wallPoint, Vector3 normal, float standoff = 0.006f)
        {
            SpawnPiece(prefab, wallPoint, Quaternion.LookRotation(normal), RrSpawnOptions.Scenery(), inst =>
            {
                CleanScenery(inst);
                Mount(inst, wallPoint, normal, standoff);
            });
        }

        /// <summary>A prop people pick up: dropped 1 cm above <paramref name="surfaceY"/> and made a Rec Room tool.</summary>
        private void Prop(string prefab, Vector3 pos, float yaw, float surfaceY, Action<RrInstance> then)
        {
            SpawnPiece(prefab, pos + Vector3.up * (surfaceY + 0.2f), Quaternion.Euler(0f, yaw, 0f), RrSpawnOptions.Prop(), inst =>
            {
                Ground(inst, surfaceY + 0.01f);
                var g = RrTools.MakeTool(inst);
                if (g != null)
                {
                    _props.Add(g);
                    var r = g.GetComponent<RrRespawnable>();
                    if (r != null)
                    {
                        r.SetHome();
                    }
                }

                if (then != null)
                {
                    then(inst);
                }
            });
        }

        private IEnumerator WaitPending(float timeout, float from, float to)
        {
            int total = Mathf.Max(1, _pending);
            float until = Time.realtimeSinceStartup + timeout;
            while (_pending > 0 && Time.realtimeSinceStartup < until)
            {
                BuildProgress = Mathf.Lerp(from, to, 1f - _pending / (float)total);
                yield return null;
            }

            if (_pending > 0)
            {
                RrLog.Warn("Dorm", _pending + " spawn(s) still pending after " + timeout.ToString("0") + " s; building on");
                _pending = 0;
            }

            BuildProgress = to;
        }

        /// <summary>
        /// §3.1: root = inverse of the mesh child's local transform (the child that carries the renderers, not the
        /// Maker-Pen <c>PropContainer</c>). Falls back to the §3.2 pose.
        /// </summary>
        private void SnapToRoomFrame(RrInstance inst, Vector3 fallbackPos, Quaternion fallbackRot)
        {
            Transform child = null;
            var root = inst.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == "PropContainer" || c.GetComponentInChildren<Renderer>(true) == null)
                {
                    continue;
                }

                child = c;
                break;
            }

            if (child == null || Mathf.Abs(child.localScale.x - 1f) > 0.01f)
            {
                root.localPosition = fallbackPos;
                root.localRotation = fallbackRot;
                RrTools.Once("Dorm", "snapfallback:" + inst.PrefabName, inst.PrefabName + ": no unit-scale mesh child, table pose used");
                return;
            }

            var rot = Quaternion.Inverse(child.localRotation);
            root.localRotation = rot;
            root.localPosition = -(rot * child.localPosition);
            _maxSnapError = Mathf.Max(_maxSnapError, Vector3.Distance(root.localPosition, fallbackPos));
        }

        /// <summary>Replaces the orange maker-pen material with our colour; two-sided shadows so the sun stays outside.</summary>
        private static void Paint(RrInstance inst, Color color, Material replacement)
        {
            var solid = replacement != null ? replacement : RrLook.Solid(color);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m != null && (m.name.IndexOf("MakerPen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      (m.shader != null && m.shader.name.IndexOf("MakerPen", StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        mats[i] = solid;
                        changed = true;
                    }
                }

                if (changed)
                {
                    r.sharedMaterials = mats;
                }

                r.shadowCastingMode = ShadowCastingMode.TwoSided;
            }
        }

        /// <summary>
        /// Scenery keeps its solid colliders only: triggers (padding, selection volumes) would block clicks. Maker-Pen
        /// selection meshes (<c>UGC_MakerPen_Holographic_InteractionVolume_Mat</c>, <c>AG/DoNotRender</c>) are only
        /// shown in Rec Room's maker mode: hidden (the light shafts' one tinted the big window lavender).
        /// </summary>
        private static void CleanScenery(RrInstance inst)
        {
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger)
                {
                    Destroy(c);
                }
            }

            HideEditorMeshes(inst);
        }

        private static void HideEditorMeshes(RrInstance inst)
        {
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null)
                    {
                        continue;
                    }

                    string n = m.name + "|" + (m.shader != null ? m.shader.name : "");
                    if (n.IndexOf("Holographic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("InteractionVolume", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("DoNotRender", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("InvisibleOutline", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        r.enabled = false;
                        break;
                    }
                }
            }
        }

        private static void Ground(RrInstance inst, float localY)
        {
            var b = inst.WorldBounds();
            if (b.size == Vector3.zero)
            {
                return;
            }

            float targetY = inst.transform.parent != null ? inst.transform.parent.TransformPoint(new Vector3(0f, localY, 0f)).y : localY;
            inst.transform.position += Vector3.up * (targetY - b.min.y);
        }

        private void Mount(RrInstance inst, Vector3 wallPointLocal, Vector3 normalLocal, float standoff)
        {
            var b = inst.WorldBounds();
            if (b.size == Vector3.zero)
            {
                return;
            }

            Vector3 p = transform.TransformPoint(wallPointLocal);
            Vector3 n = transform.TransformDirection(normalLocal).normalized;
            Vector3 delta = p - b.center;
            delta -= n * Vector3.Dot(delta, n);
            float extent = Mathf.Abs(n.x) * b.extents.x + Mathf.Abs(n.y) * b.extents.y + Mathf.Abs(n.z) * b.extents.z;
            float back = Vector3.Dot(b.center - p, n) - extent;
            delta += n * (standoff - back);
            inst.transform.position += delta;
        }
    }
}
