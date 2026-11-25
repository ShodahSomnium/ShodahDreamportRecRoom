using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm;
using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// The Rec Center's category doors: each opens a list of activities. The final room (the July 2025 scene measured in
    /// rr_reccenter_cache.md) signs SPORTS & PVP, QUESTS, FEATURED ROOMS, MAKERWORLD and CAMPUS JAMS. CreativeCampus
    /// is the older (2018-2024) door, kept so layouts that still name it compile; the final room does not use it.
    /// </summary>
    public enum RrDoorCategory
    {
        SportsPvp,
        Quests,
        CreativeCampus,
        FeaturedRooms,
        MakerWorld,
        CampusJams
    }

    /// <summary>
    /// The final Rec Center's doors (Rec Room since Jun 2021, rr_latest_places.md §3.3.3): a category door opens a
    /// floating list of that category's activities in front of it (<see cref="RrRoomListPanel"/>), and the Magic Door
    /// picks a random activity and goes there.
    /// <para>
    /// The look is Rec Room's own SINGLE room door, the one the 2024 wiki photo "Rec center quests door" shows: one
    /// wooden leaf with a tall wired-glass window and a pull bar, in a thick recolourable frame, with the backlit sign
    /// box above. Measured with rrtool in B43: <c>(Sandbox_RoomDoor)</c> (key 2ac), <c>(Sandbox_RoomDoorV2)</c> (09fb)
    /// and <c>(Sandbox_MagicDoor)</c> (2a9) all carry the same meshes - frame <c>ActivityDoorSingle_Frame</c> 1.15 x
    /// 2.14 m in <c>ActivityDoor_Recolorable_Mat</c>, one leaf <c>ActivityDoor_Left</c> 0.91 x 1.99 m in
    /// <c>ActivityDoor_Mat</c> (the light-oak leaf texture), the additive glow <c>ActivityDoor_Glow</c> and a
    /// 1.355 x 0.346 m <c>Door_Sign</c>. Only the prefab's "Door" and "[Door_Sign]" subtrees are copied (the room
    /// door also carries Maker-Pen circuit sockets), with <see cref="RrClone"/> so none of Rec Room's scripts come
    /// along. Rec Room's pivot is the hinge-side edge of the frame; the copy is re-centred on the frame mesh's bounds
    /// so a door's root is the floor centre of its doorway.
    /// </para>
    /// <para>
    /// Colours: the photo's frame is teal (the recolourable atlas' frame texel is grey 0.76, so base colour = wanted /
    /// 0.76, as <see cref="RrPortal"/>); the photo's leaf is yellow-orange wood, warmer than the texture's light oak,
    /// so the leaf gets a warm tint (1.15, 1.0, 0.78) - GUESS from the photo. The Magic Door's frame is purple (GUESS:
    /// no picture of the final one was available).
    /// </para>
    /// <para>
    /// Behaviour as <see cref="RrPortal"/> (Rec Room's ActivityDoor clips: the leaf swings 21.44 deg ajar and the
    /// warm glow fades in to alpha 0.834 over 0.25 s when the player comes within 3 m or points at it). Using it:
    /// desktop click, VR trigger ray or a hand on the leaf, or walking into the doorway. While a door's list is open
    /// the leaf stays shut (the list floats 0.25 m in front of it) and only the glow stays on.
    /// </para>
    /// </summary>
    public static class RrCategoryDoor
    {
        public const string DoorPrefab = "(Sandbox_RoomDoor)";
        public const string MagicPrefab = "(Sandbox_MagicDoor)";

        /// <summary>The Magic Door's destinationRefreshAudio (B43 #-2651044525266811996, 3.21 s mono).</summary>
        public const string MagicClip = "MagicDoor_Refresh";

        /// <summary>Rec Room's Magic Door sign text (its TMP "NewRoomText").</summary>
        public const string MagicSign = "DELIGHT ME!";

        /// <summary>Frame colour of the category doors (the 2024 wiki photo's teal).</summary>
        public static readonly Color Teal = new Color(0.07f, 0.50f, 0.52f);

        /// <summary>The Magic Door's frame (GUESS).</summary>
        public static readonly Color MagicFrame = new Color(0.46f, 0.22f, 0.70f);

        // rr_latest_places.md §3.3.3 (the wiki list plus Showdown; Creative Campus and Featured Rooms are adaptations).
        // The games the Dreamport has come first; the ones it does not have yet close the list as COMING SOON cards.
        private static readonly RrActivity[] SportsList =
        {
            RrActivity.Paintball, RrActivity.LaserTag, RrActivity.Dodgeball, RrActivity.Paddleball, RrActivity.Soccer,
            RrActivity.DiscGolf, RrActivity.Bowling, RrActivity.RecRoyale, RrActivity.Showdown
        };

        private static readonly RrActivity[] QuestList =
        {
            RrActivity.GoldenTrophy, RrActivity.CrimsonCauldron, RrActivity.Jumbotron, RrActivity.LostSkulls, RrActivity.Crescendo
        };

        // Punch Up is left out everywhere: Rec Room never released it (an Easter egg only, rr_latest_rgames_story.md s4).
        private static readonly RrActivity[] CreativeList =
        {
            RrActivity.Charades, RrActivity.Midnight, RrActivity.LittleMonsters
        };

        /// <summary>
        /// The Dreamport's party and racing games (user decision 2026-09-24): behind FEATURED ROOMS, as Rec Room's own
        /// Originals were featured there. MAKERWORLD and CAMPUS JAMS list player-made rooms instead (<see cref="MakerRoomsOf"/>).
        /// </summary>
        private static readonly RrActivity[] FeaturedList =
        {
            RrActivity.Charades, RrActivity.RecRally, RrActivity.StuntRunner, RrActivity.Midnight, RrActivity.LittleMonsters
        };

        private static readonly RrActivity[] NoneList = new RrActivity[0];

        /// <summary>
        /// A category door on a wall: door leaf + frame + sign; using it opens the room list in front of it.
        /// localPos = floor centre of the doorway, 0.06 m in front of the wall; the door's +Z faces into the room.
        /// </summary>
        public static GameObject Create(Transform parent, Vector3 localPos, float yaw, RrDoorCategory category)
        {
            var door = Make(parent, localPos, yaw, "Door " + Sign(category));
            door.Setup(category, false);
            return door.gameObject;
        }

        /// <summary>The Magic Door (Sandbox_MagicDoor, key 2a9): picks a random built activity and travels there.</summary>
        public static GameObject CreateMagicDoor(Transform parent, Vector3 localPos, float yaw)
        {
            var door = Make(parent, localPos, yaw, "Magic Door");
            door.Setup(RrDoorCategory.FeaturedRooms, true);
            return door.gameObject;
        }

        private static RrCategoryDoorView Make(Transform parent, Vector3 localPos, float yaw, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go.AddComponent<RrCategoryDoorView>();
        }

        /// <summary>
        /// The activities behind a door, in the order the list shows them (built or not; an activity registered as
        /// <see cref="RrRoomInfo.Unlisted"/> is left out by the list). MAKERWORLD and CAMPUS JAMS have none: they list
        /// player-made rooms (<see cref="MakerRoomsOf"/>).
        /// </summary>
        public static IReadOnlyList<RrActivity> Activities(RrDoorCategory category)
        {
            switch (category)
            {
                case RrDoorCategory.SportsPvp:
                    return SportsList;
                case RrDoorCategory.Quests:
                    return QuestList;
                case RrDoorCategory.CreativeCampus:
                    return CreativeList;
                case RrDoorCategory.MakerWorld:
                case RrDoorCategory.CampusJams:
                    return NoneList;
                default:
                    return FeaturedList;
            }
        }

        /// <summary>A door that lists player-made rooms (Places/MakerRooms): MAKERWORLD and CAMPUS JAMS.</summary>
        public static bool ListsMakerRooms(RrDoorCategory category)
        {
            return category == RrDoorCategory.MakerWorld || category == RrDoorCategory.CampusJams;
        }

        /// <summary>
        /// The player-made rooms behind a door (null: the door lists activities). MAKERWORLD (Rec Room's community rooms):
        /// the other players' public rooms this player knows of, the busy ones first (<see cref="RrMakerRooms.PublicRooms"/>).
        /// CAMPUS JAMS (rooms made on campus): the player's own and co-owned rooms, newest first (<see cref="RrMakerRooms.MyRooms"/>).
        /// Both lists start with a "Maker Rooms" card that opens the watch's Rooms page (Create, downloads, settings).
        /// </summary>
        public static List<RrMakerKnownRoom> MakerRoomsOf(RrDoorCategory category)
        {
            if (!ListsMakerRooms(category))
            {
                return null;
            }

            try
            {
                return category == RrDoorCategory.MakerWorld ? RrMakerRooms.PublicRooms() : RrMakerRooms.MyRooms();
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Doors", "makerlist", "the maker rooms list failed: " + e.Message);
                return new List<RrMakerKnownRoom>();
            }
        }

        /// <summary>The sign text: "SPORTS & PVP", "QUESTS", "FEATURED ROOMS", "MAKERWORLD", "CAMPUS JAMS" (or the old "CREATIVE CAMPUS").</summary>
        public static string Sign(RrDoorCategory category)
        {
            switch (category)
            {
                case RrDoorCategory.SportsPvp:
                    return "SPORTS & PVP";
                case RrDoorCategory.Quests:
                    return "QUESTS";
                case RrDoorCategory.CreativeCampus:
                    return "CREATIVE CAMPUS";
                case RrDoorCategory.MakerWorld:
                    return "MAKERWORLD";
                case RrDoorCategory.CampusJams:
                    return "CAMPUS JAMS";
                default:
                    return "FEATURED ROOMS";
            }
        }

        /// <summary>The list's title, in the watch's title case ("Sports & PvP").</summary>
        public static string Title(RrDoorCategory category)
        {
            switch (category)
            {
                case RrDoorCategory.SportsPvp:
                    return "Sports & PvP";
                case RrDoorCategory.Quests:
                    return "Quests";
                case RrDoorCategory.CreativeCampus:
                    return "Creative Campus";
                case RrDoorCategory.MakerWorld:
                    return "MakerWorld";
                case RrDoorCategory.CampusJams:
                    return "Campus Jams";
                default:
                    return "Featured Rooms";
            }
        }

        /// <summary>An activity can be picked: registered with a room to build (else the list says COMING SOON).</summary>
        public static bool IsBuilt(RrActivity activity)
        {
            var info = RrRooms.Get(activity);
            return info != null && info.Create != null;
        }

        /// <summary>What the Magic Door may pick: every built, listed activity but the dorm, the Rec Center and where you are.</summary>
        public static List<RrActivity> MagicCandidates()
        {
            var list = new List<RrActivity>();
            foreach (var info in RrRooms.All)
            {
                if (info == null || info.Create == null || info.Unlisted || info.Activity == RrActivity.Dorm || info.Activity == RrActivity.RecCenter || info.Activity == RrTravel.Current)
                {
                    continue;
                }

                list.Add(info.Activity);
            }

            return list;
        }

        /// <summary>An activity's title as the watch shows it.</summary>
        public static string TitleOf(RrActivity activity)
        {
            return Watch.RrWatchArt.TitleOf(activity);
        }
    }

    /// <summary>One category door or the Magic Door (made by <see cref="RrCategoryDoor"/>; see its notes).</summary>
    internal sealed class RrCategoryDoorView : MonoBehaviour
    {
        public const float OpenSeconds = 0.25f;
        public const float LeafOpenYaw = 21.44f;
        public const float GlowOpenAlpha = 0.834f;
        public const float HoverDepth = 3f;
        public const float HoverHalfWidth = 1.4f;
        public const float WalkHalfWidth = 0.6f;
        public const float WalkDepth = 0.6f;
        public const float ClickReach = 12f;
        public const float ArrivalDebounce = 1.5f;
        public const float HoverVolume = 0.18f;

        /// <summary>Frame footprint (the single door's frame mesh, re-centred).</summary>
        public static readonly Vector3 SolidCenter = new Vector3(0f, 1.072f, 0.048f);
        public static readonly Vector3 SolidSize = new Vector3(1.15f, 2.144f, 0.19f);

        /// <summary>Rec Room's single-door frame centre in prefab space (x of the frame mesh's bounds), if bounds fail.</summary>
        private const float FallbackCentreDoor = 0.45f;
        private const float FallbackCentreMagic = 0.50f;

        private static readonly Color GlowTint = new Color(1f, 0.972f, 0.816f, 0f);
        private static readonly Color LeafTint = new Color(1.15f, 1.0f, 0.78f, 1f);
        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private static readonly List<RrCategoryDoorView> Live = new List<RrCategoryDoorView>();
        private static int _rayFrame = -1;
        private static RrCategoryDoorView _pointed;

        public RrDoorCategory Category;
        public bool Magic;

        /// <summary>Test switch: the Magic Door rolls but does not travel.</summary>
        public bool RollOnly;

        public bool Built { get; private set; }
        public bool Procedural { get; private set; }
        public bool Rolling { get; private set; }
        public int Uses { get; private set; }
        public RrActivity LastRoll { get; private set; }

        public string Label
        {
            get { return Magic ? RrCategoryDoor.MagicSign : RrCategoryDoor.Sign(Category); }
        }

        private readonly List<Material> _owned = new List<Material>();
        private Transform _leaf;
        private Quaternion _leafBase = Quaternion.identity;
        private Renderer _glow;
        private MaterialPropertyBlock _block;
        private Light _light;
        private TextMeshPro _sign;
        private float _leafT;
        private float _glowT;
        private float _usedAt = -100f;
        private float _lastHoverSound = -100f;
        private bool _hovered;
        private bool _inDoorway;
        private bool _touching;
        private AudioClip _magicClip;

        internal void Setup(RrDoorCategory category, bool magic)
        {
            Category = category;
            Magic = magic;
        }

        private void Awake()
        {
            Live.Add(this);
            _block = new MaterialPropertyBlock();
            var solid = new GameObject("Solid");
            solid.layer = RrLayers.Environment;
            solid.transform.SetParent(transform, false);
            var box = solid.AddComponent<BoxCollider>();
            box.center = SolidCenter;
            box.size = SolidSize;

            var click = new GameObject("Click");
            click.layer = RrLayers.Click;
            click.transform.SetParent(transform, false);
            var clickBox = click.AddComponent<BoxCollider>();
            clickBox.center = new Vector3(0f, 1.07f, 0.12f);
            clickBox.size = new Vector3(1.15f, 2.14f, 0.26f);
            clickBox.excludeLayers = ~0;
        }

        private void Start()
        {
            var assets = RrAssets.Instance;
            string prefab = Magic ? RrCategoryDoor.MagicPrefab : RrCategoryDoor.DoorPrefab;
            if (assets == null)
            {
                FinishLook(null);
                return;
            }

            float t0 = Time.realtimeSinceStartup;
            assets.LoadPrefab(prefab, (asset, data) =>
            {
                if (this == null)
                {
                    return;
                }

                FinishLook(asset);
                RrTools.Once("Doors", "look:" + prefab, "door look from " + prefab + (Procedural ? " failed: procedural doors" : " (" + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms)"));
            });
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_pointed == this)
            {
                _pointed = null;
            }

            if (Live.Count == 0)
            {
                _rayFrame = -1;
                _pointed = null;
            }

            foreach (var m in _owned)
            {
                if (m != null)
                {
                    Destroy(m);
                }
            }

            _owned.Clear();
            var panel = RrRoomListPanel.Current;
            if (panel != null && panel.Door == transform)
            {
                panel.Close("door removed");
            }
        }

        // ---- look ----

        private void FinishLook(GameObject asset)
        {
            try
            {
                if (asset == null || !FromPrefab(asset))
                {
                    ClearLook();
                    MakeProcedural();
                }
            }
            catch (Exception e)
            {
                RrLog.Error("Doors", "building " + Label + " failed; procedural door", e);
                ClearLook();
                try
                {
                    MakeProcedural();
                }
                catch (Exception e2)
                {
                    RrLog.Error("Doors", "the procedural " + Label + " failed too", e2);
                }
            }

            _light = RrBuild.PointLight(transform, "Hover Light", new Vector3(0f, 1.4f, 0.9f), new Color(1f, 0.86f, 0.66f), 3.5f, 0f);
            _light.enabled = false;
            Built = true;
            Apply(0f, 0f);
        }

        private bool FromPrefab(GameObject asset)
        {
            var doorNode = FindDeep(asset.transform, "Door");
            var signNode = FindDeep(asset.transform, "[Door_Sign]");
            if (doorNode == null)
            {
                RrLog.WarnOnce("Doors", "nodoor:" + asset.name, asset.name + " has no Door node: procedural doors");
                return false;
            }

            var look = RrBuild.Group(transform, "Look").transform;
            var door = RrClone.Clone(doorNode.gameObject, look);
            GameObject sign = signNode != null ? RrClone.Clone(signNode.gameObject, look) : null;
            foreach (var c in look.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }

            foreach (var a in look.GetComponentsInChildren<Animator>(true))
            {
                a.enabled = false;
            }

            // The sign's text nodes are a legacy TextMesh and TextMeshPros (not copied); their renderers would draw a stale
            // baked text mesh ("Delight Me!") or a font atlas over ours: only the sign box itself stays.
            if (sign != null)
            {
                foreach (var r in sign.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject != sign)
                    {
                        r.enabled = false;
                    }
                }
            }

            RrMaterials.FixTree(look.gameObject);
            RrLayers.Set(look.gameObject, RrLayers.Environment);

            // Re-centre on the frame mesh (Rec Room's pivot is the frame's hinge-side edge).
            var geo = FindDeep(door.transform, "DoorGeo");
            float centre = Magic ? FallbackCentreMagic : FallbackCentreDoor;
            var mf = geo != null ? geo.GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null)
            {
                centre = transform.InverseTransformPoint(geo.TransformPoint(mf.sharedMesh.bounds.center)).x;
            }

            look.localPosition = new Vector3(-centre, 0f, 0f);

            var frameRenderer = geo != null ? geo.GetComponent<Renderer>() : null;
            if (frameRenderer != null && frameRenderer.sharedMaterial != null)
            {
                frameRenderer.sharedMaterial = Tinted(frameRenderer.sharedMaterial, Magic ? RrCategoryDoor.MagicFrame : RrCategoryDoor.Teal, true);
            }

            _leaf = FindDeep(door.transform, "ActivityDoor_Left");
            var leafRenderer = _leaf != null ? _leaf.GetComponent<Renderer>() : null;
            if (leafRenderer != null && leafRenderer.sharedMaterial != null)
            {
                leafRenderer.sharedMaterial = Tinted(leafRenderer.sharedMaterial, LeafTint, false);
            }

            var glow = FindDeep(door.transform, "ActivityDoor_Glow");
            _glow = glow != null ? glow.GetComponent<Renderer>() : null;
            if (_glow != null && (_glow.sharedMaterial == null || !_glow.sharedMaterial.HasProperty(TintId)))
            {
                _glow.enabled = false;
                _glow = null;
                RrLog.WarnOnce("Doors", "glow", "the door glow's material has no _TintColor: glow off");
            }

            // Our label on the sign box's face (the box is 0.069 m deep, its front at +Z).
            Vector3 signCentre = new Vector3(0f, 2.36f, 0.05f);
            float signFront = 0.09f;
            var signRenderer = sign != null ? sign.GetComponent<Renderer>() : null;
            if (signRenderer != null)
            {
                var b = signRenderer.bounds;
                signCentre = transform.InverseTransformPoint(b.center);
                signFront = transform.InverseTransformPoint(b.center + transform.forward * Vector3.Dot(b.extents, Abs(transform.forward))).z;
            }

            MakeLabel(new Vector3(0f, signCentre.y, signFront + 0.004f));
            _leafBase = _leaf != null ? _leaf.localRotation : Quaternion.identity;
            RrTools.Once("Doors", "measure:" + asset.name, asset.name + ": frame centre x " + centre.ToString("0.000") + " m from Rec Room's pivot, sign at y " + signCentre.y.ToString("0.00") + ", front z " + signFront.ToString("0.000") + (_leaf != null ? "" : ", no leaf (no swing)"));
            return true;
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        /// <summary>An instance of a Rec Room material with its base colour replaced (frame: wanted / 0.76) or multiplied (leaf).</summary>
        private Material Tinted(Material source, Color c, bool frame)
        {
            var m = new Material(source) { name = source.name + (frame ? " (frame " : " (tint ") + ColorUtility.ToHtmlStringRGB(c) + ")" };
            Color baseColor = frame
                ? new Color(Mathf.Min(1.3f, c.r / RrPortal.FrameTexel), Mathf.Min(1.3f, c.g / RrPortal.FrameTexel), Mathf.Min(1.3f, c.b / RrPortal.FrameTexel), 1f)
                : c;
            if (!frame && source.HasProperty("_BaseColor"))
            {
                Color was = source.GetColor("_BaseColor");
                baseColor = new Color(was.r * c.r, was.g * c.g, was.b * c.b, was.a);
            }

            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", baseColor);
            }

            if (m.HasProperty("_Color"))
            {
                m.SetColor("_Color", baseColor);
            }

            _owned.Add(m);
            return m;
        }

        private void MakeProcedural()
        {
            Procedural = true;
            var look = RrBuild.Group(transform, "Look (procedural)").transform;
            var frame = RrLook.Solid(Magic ? RrCategoryDoor.MagicFrame : RrCategoryDoor.Teal);
            RrBuild.Box(look, "Frame L", new Vector3(-0.518f, 1.072f, 0.048f), new Vector3(0.115f, 2.144f, 0.19f), frame, false);
            RrBuild.Box(look, "Frame R", new Vector3(0.518f, 1.072f, 0.048f), new Vector3(0.115f, 2.144f, 0.19f), frame, false);
            RrBuild.Box(look, "Frame Top", new Vector3(0f, 2.086f, 0.048f), new Vector3(1.15f, 0.116f, 0.19f), frame, false);
            RrBuild.Box(look, "Backlight", new Vector3(0f, 1.03f, 0f), new Vector3(0.92f, 2.0f, 0.02f), RrLook.Emissive(new Color(1f, 0.985f, 0.9f), 1.2f), false);
            _leaf = RrBuild.Group(look, "ActivityDoor_Left", new Vector3(0.455f, 1.008f, 0.094f)).transform;
            RrBuild.Box(_leaf, "Leaf", new Vector3(-0.455f, 0.02f, 0f), new Vector3(0.9f, 1.99f, 0.05f), RrLook.Solid(new Color(0.78f, 0.55f, 0.27f)), false);
            RrBuild.Box(_leaf, "Window", new Vector3(-0.72f, 0.45f, 0.028f), new Vector3(0.1f, 0.45f, 0.01f), RrLook.Solid(new Color(0.72f, 0.8f, 0.86f)), false);
            RrBuild.Box(_leaf, "Pull", new Vector3(-0.8f, 0.0f, 0.045f), new Vector3(0.03f, 0.5f, 0.03f), RrLook.Solid(RrLook.DarkGrey), false);
            RrBuild.Box(look, "Sign", new Vector3(0f, 2.38f, 0.05f), new Vector3(1.3f, 0.3f, 0.07f), RrLook.Emissive(new Color(0.991f, 0.897f, 0.808f), 0.4f), false);
            RrLayers.Set(look.gameObject, RrLayers.Environment);
            _leafBase = _leaf.localRotation;
            MakeLabel(new Vector3(0f, 2.38f, 0.09f));
        }

        private void MakeLabel(Vector3 localPos)
        {
            _sign = RrBuild.Sign(transform, "Label", Label, localPos, Quaternion.Euler(0f, 180f, 0f), 2f, Magic ? new Color(0.30f, 0.10f, 0.42f) : Color.black, true, 1.24f);
            _sign.rectTransform.sizeDelta = new Vector2(1.24f, 0.28f);
            _sign.enableAutoSizing = true;
            _sign.fontSizeMin = 0.8f;
            _sign.fontSizeMax = 2.3f;
            _sign.textWrappingMode = TextWrappingModes.NoWrap;
            _sign.gameObject.layer = RrLayers.Environment;
        }

        private void ClearLook()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Look") || child.name == "Label")
                {
                    Destroy(child.gameObject);
                }
            }

            _leaf = null;
            _glow = null;
            _sign = null;
        }

        // ---- use ----

        /// <summary>Uses the door as a player would (how: for the log).</summary>
        internal void Use(string how)
        {
            float now = Time.realtimeSinceStartup;
            if (now - _usedAt < 0.8f || Rolling)
            {
                return;
            }

            _usedAt = now;
            Uses++;
            if (Magic)
            {
                StartCoroutine(Roll(how));
                return;
            }

            var panel = RrRoomListPanel.OpenForDoor(transform, Category);
            RrLog.Info("Doors", Label + " door used (" + how + "): list of " + (panel != null ? panel.CardCount + " activities, " + panel.PickableCount + " built" : "nothing"));
        }

        private IEnumerator Roll(string how)
        {
            var pool = RrCategoryDoor.MagicCandidates();
            if (pool.Count == 0)
            {
                SetSign("NO ROOMS YET");
                RrLog.Info("Doors", "Magic Door used (" + how + "): no built activity to pick");
                yield return new WaitForSecondsRealtime(1.5f);
                SetSign(RrCategoryDoor.MagicSign);
                yield break;
            }

            Rolling = true;
            PlayRefresh();
            var pick = pool[Random.Range(0, pool.Count)];

            // A slot-machine roll through the candidates, slowing down, over the first ~1.6 s of the refresh sound.
            float t = 0f;
            float step = 0.07f;
            int last = -1;
            while (t < 1.6f)
            {
                int i = Random.Range(0, pool.Count);
                if (pool.Count > 1 && i == last)
                {
                    i = (i + 1) % pool.Count;
                }

                last = i;
                SetSign(RrCategoryDoor.TitleOf(pool[i]).ToUpperInvariant());
                yield return new WaitForSecondsRealtime(step);
                t += step;
                step *= 1.18f;
            }

            LastRoll = pick;
            SetSign(RrCategoryDoor.TitleOf(pick).ToUpperInvariant());
            RrLog.Info("Doors", "Magic Door rolled " + pick + " out of " + pool.Count + " built activit" + (pool.Count == 1 ? "y" : "ies") + " (" + how + ")");
            yield return new WaitForSecondsRealtime(0.9f);
            Rolling = false;
            if (!RollOnly && !RrTravel.Busy && RrCategoryDoor.IsBuilt(pick))
            {
                RrTravel.GoTo(pick);
                yield break;
            }

            yield return new WaitForSecondsRealtime(RollOnly ? 30f : 2f);
            SetSign(RrCategoryDoor.MagicSign);
        }

        private void PlayRefresh()
        {
            Vector3 at = transform.position + transform.up * 2.3f + transform.forward * 0.2f;
            if (_magicClip == null && RrAssets.Instance != null)
            {
                // Not a container entry: it is resident once the Magic Door prefab (which references it) is loaded.
                _magicClip = RrAssets.Instance.Resident<AudioClip>(RrCategoryDoor.MagicClip);
            }

            if (_magicClip != null)
            {
                RrSound.Play(_magicClip, at, 0.8f, 1f, 18f);
            }
            else
            {
                RrSound.Play(RrCategoryDoor.MagicClip, at, 0.8f, 1f, 18f);
                RrLog.WarnOnce("Doors", "magicclip", RrCategoryDoor.MagicClip + " not resident; asked the catalog for it");
            }
        }

        private void SetSign(string text)
        {
            if (_sign != null)
            {
                _sign.text = text;
            }
        }

        // ---- every frame ----

        private void Update()
        {
            if (!Built)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            Vector3 local = transform.InverseTransformPoint(Feet());
            bool level = Mathf.Abs(local.y) < 2f;
            bool near = level && local.z > -0.3f && local.z < HoverDepth && Mathf.Abs(local.x) < HoverHalfWidth;
            bool doorway = level && local.z > -0.3f && local.z < WalkDepth && Mathf.Abs(local.x) < WalkHalfWidth;
            bool pointed = Pointed() == this;
            bool touching = RrPlayer.InVr && HandTouching();
            var panel = RrRoomListPanel.Current;
            bool listOpen = panel != null && panel.IsOpen && panel.Door == transform;
            bool hover = near || pointed;
            bool ready = !RrTravel.Busy && !RrLoadingScreen.Showing && now - RrTravel.ArrivedAt >= ArrivalDebounce && !Rolling;
            if (ready)
            {
                if (doorway && !_inDoorway && !listOpen)
                {
                    Use("walked in");
                }
                else if (pointed && RrInput.UseDown && !RrInput.PointerOverUi && !(panel != null && panel.ContainsRay(RrInput.AimRay)))
                {
                    Use(RrPlayer.InVr ? "trigger" : "click");
                }
                else if (touching && !_touching && !listOpen)
                {
                    Use("touched");
                }
            }

            _inDoorway = doorway;
            _touching = touching;
            if (hover && !_hovered && ready && now - _lastHoverSound > 0.6f)
            {
                _lastHoverSound = now;
                RrSound.Play(RrPortal.HoverClip, transform.position + Vector3.up * 1.2f, HoverVolume, Magic ? 1.2f : 1f, 12f);
            }

            _hovered = hover;
            float dt = Time.unscaledDeltaTime / OpenSeconds;
            _leafT = Mathf.MoveTowards(_leafT, (hover || Rolling) && !listOpen ? 1f : 0f, dt);
            _glowT = Mathf.MoveTowards(_glowT, hover || listOpen || Rolling ? 1f : 0f, dt);
            Apply(_leafT, _glowT);
        }

        private void Apply(float leaf, float glow)
        {
            float s = leaf * leaf * (3f - 2f * leaf);
            float g = glow * glow * (3f - 2f * glow);
            if (_leaf != null)
            {
                _leaf.localRotation = _leafBase * Quaternion.Euler(0f, LeafOpenYaw * s, 0f);
            }

            if (_glow != null)
            {
                bool on = g > 0.003f;
                if (_glow.enabled != on)
                {
                    _glow.enabled = on;
                }

                if (on)
                {
                    _glow.GetPropertyBlock(_block);
                    _block.SetColor(TintId, new Color(GlowTint.r, GlowTint.g, GlowTint.b, GlowOpenAlpha * g));
                    _glow.SetPropertyBlock(_block);
                }
            }

            if (_light != null)
            {
                _light.enabled = g > 0.01f;
                _light.intensity = 0.9f * g;
            }
        }

        private bool HandTouching()
        {
            return Touches(RrHand.Left) || Touches(RrHand.Right);
        }

        private bool Touches(RrHand hand)
        {
            if (hand == null || !hand.Tracked)
            {
                return false;
            }

            Vector3 p = transform.InverseTransformPoint(hand.Position);
            return Mathf.Abs(p.x) < 0.6f && p.y > 0.2f && p.y < 2.1f && p.z > -0.05f && p.z < 0.3f;
        }

        private static Vector3 Feet()
        {
            try
            {
                return RrPlayer.FeetPosition;
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Doors", "feet", "RrPlayer.FeetPosition failed: " + e.Message);
                return new Vector3(0f, -10000f, 0f);
            }
        }

        /// <summary>The door under the aim ray (one raycast a frame for all these doors; walls in front block it).</summary>
        private static RrCategoryDoorView Pointed()
        {
            if (_rayFrame == Time.frameCount)
            {
                return _pointed;
            }

            _rayFrame = Time.frameCount;
            _pointed = null;
            try
            {
                RaycastHit hit;
                if (Physics.Raycast(RrInput.AimRay, out hit, ClickReach, (1 << RrLayers.Click) | (1 << RrLayers.Environment), QueryTriggerInteraction.Ignore)
                    && hit.collider.gameObject.layer == RrLayers.Click)
                {
                    _pointed = hit.collider.GetComponentInParent<RrCategoryDoorView>();
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Doors", "ray", "the door aim ray failed: " + e.Message);
            }

            return _pointed;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
