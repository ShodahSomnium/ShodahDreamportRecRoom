using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using SomniumSpace.Worlds.SomniumRecRoom.Watch;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// A world-space room list in the watch's style (Rec Room's RRUI, rr_watch_ui.md), made from the watch's own
    /// parts (<see cref="RrUiKit"/>, <see cref="RrWatchCard"/>, <see cref="RrWatchArt"/>, <see cref="RrWatchSounds"/>),
    /// in the same 1920-wide canvas units. Two uses:
    /// <list type="bullet">
    /// <item>
    /// <b>Door list</b> (<see cref="OpenForDoor"/>): what a Rec Center category door opens, as the 2024 wiki photo
    /// shows it - a floating white panel in front of the door, tabs along the top (the four categories, the door's
    /// own selected), a row of room cards (cover art + title), and the watch's orange navigation bar at the bottom
    /// (location + Close). 1.6 m wide (1920 x 1170 units), centred 1.35 m up and 0.25 m in front of the door (just
    /// clear of its frame, 0.19 m deep), facing into the room. It opens and closes with the watch's PlayerMenu_Show /
    /// _Hide scale keys and paper sounds; it closes on Close, when the player walks more than 4 m away (after having
    /// been near it), when travel starts, or when another door's list opens. One door list at a time.
    /// </item>
    /// <item>
    /// <b>Board</b> (<see cref="CreateBoard"/>): a fixed panel with the same cards and tabs and no navigation bar
    /// (the Dorm's Play board, Rec Room's <c>(PlayCommunityBoard)</c>: top bar + content).
    /// </item>
    /// </list>
    /// A card per activity: its cover art, title, "N here" / "You are here" (<see cref="RrWatchWorld"/>, refreshed
    /// every 1.5 s) and, for activities not built yet (not registered with a room), a veil with "COMING SOON" and no
    /// pick; activities registered as <see cref="RrRoomInfo.Unlisted"/> get no card. Picking a card closes the list and
    /// calls <see cref="RrTravel.GoTo"/>.
    /// <para>
    /// A tab with <see cref="Tab.MakerRooms"/> (MAKERWORLD, CAMPUS JAMS) lists player-made rooms instead: first a "Maker
    /// Rooms" card that opens the watch's Rooms page (Create, downloads), then a card per room (its base map's picture,
    /// name, owner, "N here"); picking one calls <see cref="RrMakerRooms.Open"/> (which may download the room first: the
    /// subtitle shows the download, and the list closes when the trip starts). A room that cannot be opened now (private,
    /// nobody online holds it) is veiled with the reason. The cards follow the maker catalogue as it changes.
    /// </para>
    /// <para>
    /// <b>Input</b> reaches the cards three ways, all ending in the card's <see cref="RrUiButton"/>:
    /// (1) as the watch: a GraphicRaycaster and a TrackedDeviceGraphicRaycaster on the canvas (whole tree on layer 5,
    /// worldCamera re-read every 0.5 s) give the client's XR ray and free desktop cursor their hover and click;
    /// (2) our own ray: <see cref="RrInput.AimRay"/> (the crosshair in play mode, the right hand in VR) meets the
    /// canvas plane, the button under the point is hovered (desktop with a locked cursor, which the EventSystem does
    /// not see) and clicked on <see cref="RrInput.UseDown"/>; walls in front block it; the mouse wheel scrolls;
    /// (3) VR poke: a fingertip (<see cref="RrWatchModel.TryPokePoints"/>) inside a button, 3 cm in front to 5 cm
    /// behind the canvas, presses it once. A press seen twice (the XR ray and our trigger ray in one pull) counts
    /// once: actions closer than 0.35 s are dropped.
    /// </para>
    /// </summary>
    public sealed class RrRoomListPanel : MonoBehaviour
    {
        /// <summary>One tab: its label, the page title and subtitle when selected, and its activities (asked for each time it is shown).</summary>
        public sealed class Tab
        {
            public string Label;
            public string Title;
            public string Subtitle;
            public Func<IReadOnlyList<RrActivity>> Items;

            /// <summary>Player-made rooms to list after <see cref="Items"/>, with a leading "Maker Rooms" card (null: none).</summary>
            public Func<List<RrMakerKnownRoom>> MakerRooms;
        }

        public const float CanvasWidth = 1920f;
        public const float DoorCanvasHeight = 1170f;
        public const float NavTop = 1040f;
        public const float NavHeight = 130f;

        /// <summary>Door list size and place (metres, door-local).</summary>
        public const float DoorWidth = 1.6f;
        public static readonly Vector3 DoorOffset = new Vector3(0f, 1.35f, 0.25f);

        public const float WalkAway = 4f;
        public const float DoorReach = 6f;
        public const float BoardReach = 9f;
        public const float PokeFront = 0.03f;
        public const float PokeBack = 0.05f;
        public const float ActionDebounce = 0.35f;
        public const float RefreshSeconds = 1.5f;

        private const int PerRow = 6;

        /// <summary>A board is seen from 2-4 m: 5 cards a row at 1.2x (0.54 m wide on the 2.86 m Dorm board), 2 rows in view.</summary>
        private const int BoardPerRow = 5;
        private const float BoardCardScale = 1.2f;
        private const float Margin = 60f;
        private const float TabsTop = 186f;
        private const float TabWidth = 300f;
        private const float TabHeight = 68f;
        private const float TabGap = 16f;
        private const float CardsTop = 276f;
        private const int CardsPerFrame = 3;

        // PlayerMenu_Show / _Hide scale keys (t, x, y, z), as the watch decoded them from Rec Room's clips (rr_watch_ui.md §3.4).
        private static readonly Vector4[] ShowKeys = { new Vector4(0f, 0.01f, 0.01f, 1f), new Vector4(0.056f, 1f, 0.046f, 1f), new Vector4(0.211f, 1.2f, 1.2f, 1.2f), new Vector4(0.422f, 1f, 1f, 1f) };
        private static readonly Vector4[] HideKeys = { new Vector4(0f, 1f, 1f, 1f), new Vector4(0.2f, 1.2f, 1.2f, 1.2f), new Vector4(0.367f, 1f, 0.046f, 1f), new Vector4(0.417f, 0.01f, 0.01f, 1f) };

        /// <summary>The door list that is open (null: none). Boards are not listed here.</summary>
        public static RrRoomListPanel Current { get; private set; }

        private sealed class CardView
        {
            public RrActivity Activity;
            public RrWatchCard Card;
            public bool Built;
            public TextMeshProUGUI Badge;
            public RectTransform BadgeRoot;
            public string Shown = "";

            /// <summary>A player-made room's card (its id), or the leading "Maker Rooms" card (<see cref="Lead"/>).</summary>
            public string MakerId;
            public bool Lead;
        }

        private struct Target
        {
            public RrUiButton Button;
            public RectTransform Clip;
        }

        private readonly List<Tab> _tabs = new List<Tab>();
        private readonly List<CardView> _cards = new List<CardView>();
        private readonly List<Target> _targets = new List<Target>();
        private readonly List<RrUiButton> _tabButtons = new List<RrUiButton>();
        private readonly RrUiButton[] _poking = new RrUiButton[2];

        private bool _board;
        private Transform _door;
        private float _width;
        private float _height;
        private string _location = "";
        private int _tab;
        private RectTransform _canvasRect;
        private RectTransform _menu;
        private RectTransform _page;
        private RectTransform _content;
        private RectTransform _viewport;
        private ScrollRect _scroll;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;
        private TextMeshProUGUI _empty;
        private Canvas _canvas;
        private GraphicRaycaster _raycaster;
        private TrackedDeviceGraphicRaycaster _tracked;
        private PointerEventData _ped;
        private RrUiButton _hovered;
        private bool _testHover;
        private Coroutine _filling;
        private float _animT = -1f;
        private bool _hiding;
        private bool _wasNear;
        private float _lastAction = -10f;
        private float _refreshAt;
        private float _cameraAt;
        private int _cardsTotal;
        private int _pickable;
        private int _makerSerial = -1;
        private string _subtitleText = "";
        private string _statusShown;

        /// <summary>Open (false once it starts closing).</summary>
        public bool IsOpen { get; private set; }

        private int Columns
        {
            get { return _board ? BoardPerRow : PerRow; }
        }

        private float CardScale
        {
            get { return _board ? BoardCardScale : 1f; }
        }

        /// <summary>The door this list belongs to (null for a board).</summary>
        public Transform Door
        {
            get { return _door; }
        }

        public bool IsBoard
        {
            get { return _board; }
        }

        public int SelectedTab
        {
            get { return _tab; }
        }

        /// <summary>Cards on the selected tab, and how many of them can be picked (built).</summary>
        public int CardCount
        {
            get { return _cardsTotal; }
        }

        public int PickableCount
        {
            get { return _pickable; }
        }

        /// <summary>The activities of the selected tab, in order.</summary>
        public List<RrActivity> Shown()
        {
            var list = new List<RrActivity>();
            foreach (var c in _cards)
            {
                list.Add(c.Activity);
            }

            return list;
        }

        /// <summary>Cards whose cover picture has arrived.</summary>
        public int ArtShown
        {
            get
            {
                int n = 0;
                foreach (var c in _cards)
                {
                    if (c.Card == null || c.Card.Root == null)
                    {
                        continue;
                    }

                    foreach (var img in c.Card.Root.GetComponentsInChildren<Image>(true))
                    {
                        if (img.name.StartsWith("Picture") && img.sprite != null)
                        {
                            n++;
                            break;
                        }
                    }
                }

                return n;
            }
        }

        // ---- making ----

        /// <summary>The final Rec Center's five category tabs.</summary>
        public static List<Tab> CategoryTabs()
        {
            var tabs = new List<Tab>();
            foreach (RrDoorCategory c in new[] { RrDoorCategory.SportsPvp, RrDoorCategory.Quests, RrDoorCategory.FeaturedRooms, RrDoorCategory.MakerWorld, RrDoorCategory.CampusJams })
            {
                var cat = c;
                bool maker = RrCategoryDoor.ListsMakerRooms(cat);
                tabs.Add(new Tab
                {
                    Label = RrCategoryDoor.Title(cat),
                    Title = RrCategoryDoor.Title(cat),
                    Subtitle = !maker ? "Pick an activity" : cat == RrDoorCategory.MakerWorld ? "Public rooms made by players here" : "Your rooms  ·  make one with Maker Rooms",
                    Items = () => RrCategoryDoor.Activities(cat),
                    MakerRooms = maker ? () => RrCategoryDoor.MakerRoomsOf(cat) : (Func<List<RrMakerKnownRoom>>)null
                });
            }

            return tabs;
        }

        /// <summary>
        /// Opens (or keeps open) the list of a category door, in front of it. Another door's list closes. Returns
        /// the list, or null when it could not be made.
        /// </summary>
        public static RrRoomListPanel OpenForDoor(Transform door, RrDoorCategory category)
        {
            if (door == null)
            {
                return null;
            }

            var open = Current;
            if (open != null && open.IsOpen && open._door == door)
            {
                if (open._tab != (int)category)
                {
                    open.SelectTab((int)category);
                }

                return open;
            }

            if (open != null)
            {
                open.Close("another door");
            }

            try
            {
                var go = new GameObject("Room List " + RrCategoryDoor.Sign(category), typeof(RectTransform));
                go.layer = RrLayers.Ui;
                var panel = go.AddComponent<RrRoomListPanel>();
                panel._door = door;
                panel._board = false;
                panel._width = DoorWidth;
                panel._height = DoorCanvasHeight;
                panel._location = RrTravel.Current == RrActivity.RecCenter || RrTravel.Current == RrActivity.None ? "Rec Center" : RrCategoryDoor.TitleOf(RrTravel.Current);
                panel._tabs.AddRange(CategoryTabs());
                go.transform.SetParent(door.parent != null ? door.parent : door, false);
                go.transform.SetPositionAndRotation(door.TransformPoint(DoorOffset), door.rotation * Quaternion.Euler(0f, 180f, 0f));
                panel.Build((int)category);
                Current = panel;
                panel.Show();
                return panel;
            }
            catch (Exception e)
            {
                RrLog.Error("Doors", "the room list of " + RrCategoryDoor.Sign(category) + " failed", e);
                return null;
            }
        }

        /// <summary>
        /// A fixed board (always open, no navigation bar) at a local pose whose +Z points at the viewer, sized in
        /// metres (the canvas keeps 1920 units across; its height follows the aspect).
        /// </summary>
        public static RrRoomListPanel CreateBoard(Transform parent, Vector3 localPos, Quaternion facing, float widthMetres, float heightMetres, List<Tab> tabs, int selected = 0)
        {
            var go = new GameObject("Room Board", typeof(RectTransform));
            go.layer = RrLayers.Ui;
            var panel = go.AddComponent<RrRoomListPanel>();
            panel._board = true;
            panel._width = widthMetres;
            panel._height = CanvasWidth * heightMetres / Mathf.Max(0.1f, widthMetres);
            if (tabs != null)
            {
                panel._tabs.AddRange(tabs);
            }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = facing * Quaternion.Euler(0f, 180f, 0f);
            panel.Build(Mathf.Clamp(selected, 0, Mathf.Max(0, panel._tabs.Count - 1)));
            panel.IsOpen = true;
            panel.SetRaycasters(true);
            return panel;
        }

        private void Build(int tab)
        {
            _canvasRect = (RectTransform)transform;
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvasRect.sizeDelta = new Vector2(CanvasWidth, _height);
            float s = _width / CanvasWidth;
            _canvasRect.localScale = new Vector3(s, s, s);
            _raycaster = gameObject.AddComponent<GraphicRaycaster>();
            _tracked = gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            _canvas.worldCamera = RrPlayer.Camera;
            _ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };

            _menu = RrUiKit.Centered(_canvasRect, "Menu", 0f, 0f, CanvasWidth, _height);
            float pageH = _board ? _height : NavTop + 20f;
            _page = RrUiKit.TopLeft(_menu, "Page", 0f, 0f, CanvasWidth, pageH);
            RrUiKit.Rounded(_page, RrUiKit.Page, 20f, true);

            // Jost's line is ~1.45 x its size: a 60 title needs ~87 units of height (a label that cannot fit its line shows nothing).
            _title = RrUiKit.Label(_page, "Title", "", Margin, 28f, 1300f, 100f, 60f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, false, true);
            _subtitle = RrUiKit.Label(_page, "Subtitle", "", Margin, 118f, 1400f, 50f, 30f, RrUiKit.InkSoft);

            float bottom = _board ? _height - 16f : NavTop - 6f;
            _scroll = RrUiKit.Scroll(_page, "Cards", 0f, CardsTop, CanvasWidth, bottom - CardsTop, out _content);
            _viewport = _scroll.viewport;
            _content.pivot = new Vector2(0f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.localScale = Vector3.one * CardScale;
            _empty = RrUiKit.Label(_page, "Empty", "Nothing here yet - coming soon.", 0f, CardsTop + 200f, CanvasWidth, 80f, 36f, RrUiKit.InkSoft, TextAlignmentOptions.Center);
            _empty.gameObject.SetActive(false);

            if (!_board)
            {
                BuildNav();
            }

            RrUiKit.UiLayer(gameObject);
            SelectTab(tab);
        }

        private void BuildNav()
        {
            var shadow = RrUiKit.TopLeft(_menu, "NavBarShadow", 0f, NavTop - 48f, CanvasWidth, 48f);
            RrUiKit.Gradient(RrUiKit.Solid(shadow, Color.white), new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0f), 90f);
            var nav = RrUiKit.TopLeft(_menu, "NavBar", 0f, NavTop, CanvasWidth, NavHeight);
            RrUiKit.Gradient(RrUiKit.Rounded(nav, Color.white, 20f, true), RrUiKit.NavFrom, RrUiKit.NavTo, 160f, 0.3f, 1f);

            var where = RrUiKit.TopLeft(nav, "Location Icon", 40f, 33f, 64f, 64f);
            RrUiKit.Icon(where, "icon_AppNav_Location", Color.white);
            RrUiKit.Label(nav, "Location", _location, 118f, 25f, 800f, 80f, 36f, Color.white, TextAlignmentOptions.MidlineLeft, true);

            // Close: a nav tab like the watch's (icon over a Roboto 24 label on a #602121 plate: alpha 0 / 0.3 hover / 0.4 pressed).
            var rt = RrUiKit.TopLeft(nav, "Tab Close", CanvasWidth - 10f - 150f, 15f, 150f, NavHeight - 25f);
            var close = RrUiKit.PlateButton(rt, RrUiKit.TabPlate, 20f, new Vector4(0f, 0.3f, 0.4f, 0f), RrUiSound.Sidebar);
            RrUiKit.Icon(RrUiKit.TopLeft(rt, "Icon", (150f - 64f) * 0.5f, 6f, 64f, 64f), "icon_Exit", Color.white);
            RrUiKit.Text(RrUiKit.TopLeft(rt, "Label", 4f, 72f, 142f, 30f), "Close", 24f, Color.white, TextAlignmentOptions.Center);
            close.onClick.AddListener(() => Act(() => Close("close button")));
            _targets.Add(new Target { Button = close });
        }

        /// <summary>Shows a tab's activities (rebuilds the tabs and cards).</summary>
        public void SelectTab(int index)
        {
            if (_tabs.Count == 0)
            {
                return;
            }

            _tab = Mathf.Clamp(index, 0, _tabs.Count - 1);
            var tab = _tabs[_tab];
            _title.text = tab.Title ?? "";
            _subtitleText = _board ? tab.Subtitle ?? "" : _location + "  ·  " + (tab.Subtitle ?? "");
            _subtitle.text = _subtitleText;
            _statusShown = null;

            // Tabs: the selected one is Rec Room's primary orange, the others white secondary buttons.
            foreach (var b in _tabButtons)
            {
                if (b != null)
                {
                    _targets.RemoveAll(t => t.Button == b);
                    Destroy(b.gameObject);
                }
            }

            _tabButtons.Clear();
            for (int i = 0; i < _tabs.Count; i++)
            {
                int k = i;
                float x = Margin + i * (TabWidth + TabGap);
                var b = i == _tab
                    ? RrUiKit.Primary(_page, "Tab " + _tabs[i].Label, _tabs[i].Label, x, TabsTop, TabWidth, TabHeight, 26f)
                    : RrUiKit.Secondary(_page, "Tab " + _tabs[i].Label, _tabs[i].Label, x, TabsTop, TabWidth, TabHeight, 26f);
                if (i != _tab && b.FillStates != null && b.FillStates.Length >= 3)
                {
                    // A white tab on the cream page barely shows: its resting fill is a shade darker (#EFE4DB), white on hover.
                    b.FillStates = new[] { RrUiKit.Hex(0xEFE4DB), Color.white, RrUiKit.SecondaryPressed };
                    b.Refresh();
                }

                if (b.Label != null)
                {
                    b.Label.enableAutoSizing = true;
                    b.Label.fontSizeMin = 18f;
                    b.Label.fontSizeMax = 26f;
                }

                b.onClick.AddListener(() => Act(() => SelectTab(k)));
                _tabButtons.Add(b);
                _targets.Add(new Target { Button = b });
            }

            // Cards: a few per frame (each is ~15 UI nodes; rule: no main-thread step over ~4 ms).
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Destroy(_content.GetChild(i).gameObject);
            }

            _targets.RemoveAll(t => t.Clip != null);
            _cards.Clear();

            // Activities registered as Unlisted get no card; unregistered ones stay as COMING SOON (not pickable).
            var items = new List<RrActivity>();
            if (tab.Items != null)
            {
                var all = tab.Items();
                if (all != null)
                {
                    foreach (var a in all)
                    {
                        var info = RrRooms.Get(a);
                        if (info == null || !info.Unlisted)
                        {
                            items.Add(a);
                        }
                    }
                }
            }

            List<RrMakerKnownRoom> rooms = null;
            if (tab.MakerRooms != null)
            {
                rooms = tab.MakerRooms() ?? new List<RrMakerKnownRoom>();
                _makerSerial = RrMakerRooms.Serial;
            }

            _cardsTotal = items.Count + (rooms != null ? 1 + rooms.Count : 0);
            _pickable = 0;
            foreach (var a in items)
            {
                if (RrCategoryDoor.IsBuilt(a))
                {
                    _pickable++;
                }
            }

            if (rooms != null)
            {
                _pickable++;
                foreach (var r in rooms)
                {
                    if (RrMakerRooms.CannotOpen(r) == null)
                    {
                        _pickable++;
                    }
                }
            }

            int rows = (_cardsTotal + Columns - 1) / Columns;
            _content.sizeDelta = new Vector2(0f, rows * RrWatchCard.Height + 30f);
            _content.anchoredPosition = Vector2.zero;
            _empty.gameObject.SetActive(_cardsTotal == 0);
            if (_filling != null)
            {
                StopCoroutine(_filling);
            }

            _filling = isActiveAndEnabled ? StartCoroutine(Fill(items, rooms)) : null;
            if (!isActiveAndEnabled)
            {
                var e = Fill(items, rooms);
                while (e.MoveNext())
                {
                }
            }

            RrUiKit.UiLayer(_page.gameObject);
        }

        private IEnumerator Fill(List<RrActivity> items, List<RrMakerKnownRoom> rooms)
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                AddCard(items[i], n++);
                if (n % CardsPerFrame == 0)
                {
                    RrUiKit.UiLayer(_content.gameObject);
                    yield return null;
                }
            }

            if (rooms != null)
            {
                AddLeadCard(n++);
                for (int i = 0; i < rooms.Count; i++)
                {
                    AddMakerCard(rooms[i], n++);
                    if (n % CardsPerFrame == 0)
                    {
                        RrUiKit.UiLayer(_content.gameObject);
                        yield return null;
                    }
                }
            }

            RrUiKit.UiLayer(_content.gameObject);
            _refreshAt = 0f;
            _filling = null;
        }

        private void AddCard(RrActivity activity, int index)
        {
            float x = Margin / CardScale - RrWatchCard.ArtInset + (index % Columns) * RrWatchCard.Width;
            float y = 6f + (index / Columns) * RrWatchCard.Height;
            bool built = RrCategoryDoor.IsBuilt(activity);
            var card = RrWatchCard.Make(_content, activity, x, y, a => Act(() => Pick(a, "card")));
            var view = new CardView { Activity = activity, Card = card, Built = built };
            if (!built)
            {
                // Not built yet: the art is veiled, "COMING SOON" over it, and the card cannot be picked.
                Veil(card, "COMING SOON", "Not in Dreamport yet");
            }

            _cards.Add(view);
            _targets.Add(new Target { Button = card.Button, Clip = _viewport });
        }

        /// <summary>Greys a card's picture with a pill over it and makes it unpickable.</summary>
        private static void Veil(RrWatchCard card, string pillText, string subtitle)
        {
            card.Button.interactable = false;
            var veil = RrUiKit.TopLeft(card.Root, "Coming Soon", RrWatchCard.ArtInset, RrWatchCard.ArtInset, RrWatchCard.ArtWidth, RrWatchCard.ArtHeight);
            RrUiKit.Rounded(veil, RrUiKit.WithAlpha(RrUiKit.Page, 0.62f), 16f);
            var pill = RrUiKit.Centered(veil, "Pill", 0f, 0f, 236f, 56f);
            RrUiKit.Rounded(pill, RrUiKit.Grey, 100f);
            var text = RrUiKit.Text(RrUiKit.Fill(pill, "Text"), pillText, 24f, Color.white, TextAlignmentOptions.Center, true);
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 24f;
            if (card.Subtitle != null && subtitle != null)
            {
                card.Subtitle.text = subtitle;
            }
        }

        private static Vector2 CardSpot(int index, int columns, float scale)
        {
            return new Vector2(Margin / scale - RrWatchCard.ArtInset + (index % columns) * RrWatchCard.Width, 6f + (index / columns) * RrWatchCard.Height);
        }

        /// <summary>The maker tabs' first card: the watch's Rooms page (Create, every room, downloads).</summary>
        private void AddLeadCard(int index)
        {
            var at = CardSpot(index, Columns, CardScale);
            var info = RrRooms.Get(RrActivity.MakerRoom);
            var art = RrWatchArt.For(RrActivity.MakerRoom, info);
            var card = RrWatchCard.Make(_content, RrActivity.MakerRoom, at.x, at.y, a => Act(() => OpenRoomsPage("card")), art, RrMakerRooms.DefaultTitle, "Create a room  ·  all rooms", "Card Maker Rooms");
            _cards.Add(new CardView { Activity = RrActivity.MakerRoom, Card = card, Built = true, Lead = true });
            _targets.Add(new Target { Button = card.Button, Clip = _viewport });
        }

        /// <summary>A player-made room: its base map's picture, name and owner; picking it opens the room.</summary>
        private void AddMakerCard(RrMakerKnownRoom r, int index)
        {
            var at = CardSpot(index, Columns, CardScale);
            var info = r.Info;
            string id = info.Id;
            var t = RrMakerTemplates.Get(info.Template);
            var art = t != null ? RrWatchCreatePage.ArtOf(t) : RrWatchArt.For(RrActivity.MakerRoom, RrRooms.Get(RrActivity.MakerRoom));
            string owner = RrMakerRooms.IsMine(info) ? "you" : string.IsNullOrEmpty(info.OwnerName) ? "someone" : info.OwnerName;
            string sub = "by " + owner + "  ·  " + RrMakerTemplates.TitleOf(info.Template);
            var card = RrWatchCard.Make(_content, RrActivity.MakerRoom, at.x, at.y, a => Act(() => PickMaker(id, "card")), art, info.Name, sub, "Card Room " + id);
            string why = RrMakerRooms.CannotOpen(r);
            if (why != null)
            {
                Veil(card, why.ToUpperInvariant(), null);
            }

            _cards.Add(new CardView { Activity = RrActivity.MakerRoom, Card = card, Built = why == null, MakerId = id });
            _targets.Add(new Target { Button = card.Button, Clip = _viewport });
        }

        private void Show()
        {
            IsOpen = true;
            _hiding = false;
            _animT = 0f;
            _menu.localScale = new Vector3(0.01f, 0.01f, 1f);
            SetRaycasters(true);
            RrWatchSounds.Play(RrWatchSounds.Open);
            RrWatchSounds.Play(RrWatchSounds.OpenStereo);
        }

        /// <summary>Closes a door list (boards stay).</summary>
        public void Close(string why)
        {
            if (_board || !IsOpen)
            {
                return;
            }

            IsOpen = false;
            _hiding = true;
            _animT = 0f;
            SetHover(null);
            SetRaycasters(false);
            if (Current == this)
            {
                Current = null;
            }

            if (this != null && isActiveAndEnabled)
            {
                RrWatchSounds.Play(RrWatchSounds.Close);
                RrWatchSounds.Play(RrWatchSounds.CloseStereo);
            }

            RrTools.Once("Doors", "closed:" + why, "room list closed (" + why + ")");
        }

        private void SetRaycasters(bool on)
        {
            if (_raycaster != null)
            {
                _raycaster.enabled = on;
            }

            if (_tracked != null)
            {
                _tracked.enabled = on;
            }
        }

        // ---- actions ----

        /// <summary>Runs a button's action unless one ran within 0.35 s (one press seen by two input paths).</summary>
        private void Act(Action action)
        {
            float now = Time.unscaledTime;
            if (now - _lastAction < ActionDebounce || !IsOpen)
            {
                return;
            }

            _lastAction = now;
            action();
        }

        /// <summary>Picks an activity as a card would: travels there when it is built (closing the list); false otherwise.</summary>
        public bool Pick(RrActivity activity, string how)
        {
            if (!RrCategoryDoor.IsBuilt(activity))
            {
                RrLog.Info("Doors", "picked " + activity + " (" + how + "): coming soon, not built");
                return false;
            }

            if (activity == RrActivity.MakerRoom)
            {
                // Many rooms under one activity: travel needs a room, so this opens the watch's Rooms page.
                return OpenRoomsPage(how);
            }

            if (activity == RrTravel.Current)
            {
                Close("already here");
                return false;
            }

            if (RrTravel.Busy)
            {
                return false;
            }

            string tab = _tabs.Count > 0 ? _tabs[_tab].Title : "";
            RrLog.Info("Doors", "picked " + activity + " from " + (_board ? "the board" : "the " + tab + " list") + " (" + how + ")");
            Close("travel");
            RrTravel.GoTo(activity);
            return true;
        }

        /// <summary>The "Maker Rooms" card: closes a door list and opens the watch on its Rooms page.</summary>
        public bool OpenRoomsPage(string how)
        {
            if (RrWatch.Ui == null)
            {
                RrLog.WarnOnce("Doors", "nowatch", "Maker Rooms card (" + how + "): the watch is not ready");
                return false;
            }

            RrLog.Info("Doors", "Maker Rooms card (" + how + "): the watch's Rooms page");
            Close("watch rooms page");
            RrWatch.Show(RrWatchPageId.Rooms);
            return true;
        }

        /// <summary>Opens a player-made room from its card (it may be downloaded first; the list closes when the trip starts).</summary>
        public bool PickMaker(string id, string how)
        {
            var r = RrMakerCatalog.Get(id);
            string why = RrMakerRooms.CannotOpen(r);
            if (why != null)
            {
                RrLog.Info("Doors", "picked maker room " + id + " (" + how + "): " + why);
                return false;
            }

            var current = RrMakerRoom.Current;
            if (current != null && current.RoomId == id && RrTravel.Current == RrActivity.MakerRoom)
            {
                Close("already here");
                return false;
            }

            if (RrTravel.Busy || RrMakerRooms.Opening)
            {
                return false;
            }

            RrLog.Info("Doors", "picked maker room \"" + r.Info.Name + "\" (" + id + ") from " + (_board ? "the board" : "the " + (_tabs.Count > 0 ? _tabs[_tab].Title : "") + " list") + " (" + how + ")");
            return RrMakerRooms.Open(id);
        }

        /// <summary>True when the ray reaches the panel's face (within reach, nothing solid in front).</summary>
        public bool ContainsRay(Ray ray)
        {
            Vector3 hit;
            return IsOpen && RayOnPanel(ray, _board ? BoardReach : DoorReach, out hit);
        }

        /// <summary>Test hook: hovers the card of an activity (the focus outline and growth), or nothing.</summary>
        public void TestHover(RrActivity activity)
        {
            RrUiButton b = null;
            foreach (var c in _cards)
            {
                if (c.Activity == activity && c.Card != null)
                {
                    b = c.Card.Button;
                }
            }

            _testHover = b != null;
            SetHover(b);
        }

        /// <summary>
        /// Test hook: aims our ray at the centre of an activity's card from <paramref name="distance"/> in front and
        /// says which button it meets, and whether a fingertip 2 cm in front of that point would poke it.
        /// </summary>
        public string TestAim(RrActivity activity, float distance)
        {
            RrWatchCard card = null;
            foreach (var c in _cards)
            {
                if (c.Activity == activity)
                {
                    card = c.Card;
                }
            }

            if (card == null || card.Root == null)
            {
                return activity + ": no card";
            }

            Vector3 centre = card.Root.TransformPoint(card.Root.rect.center);
            Vector3 toViewer = -_canvasRect.forward;
            Vector3 hit;
            var ray = new Ray(centre + toViewer * distance, -toViewer);
            var rayButton = RayOnPanel(ray, _board ? BoardReach : DoorReach, out hit) ? ButtonAt(hit) : null;
            Vector3 tip = centre + toViewer * 0.02f;
            Vector3 local = _canvasRect.InverseTransformPoint(tip);
            float depth = local.z * _canvasRect.lossyScale.z;
            var pokeButton = depth > -PokeFront && depth < PokeBack && InsideCanvas(local) ? ButtonAt(tip) : null;
            return activity + ": ray " + (rayButton != null ? rayButton.name + (rayButton.IsInteractable() ? "" : " (inactive)") : "nothing") +
                   ", poke depth " + depth.ToString("0.000") + " m -> " + (pokeButton != null ? pokeButton.name : "nothing");
        }

        // ---- every frame ----

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_animT >= 0f)
            {
                _animT += dt;
                var keys = _hiding ? HideKeys : ShowKeys;
                float end = keys[keys.Length - 1].x;
                _menu.localScale = Evaluate(keys, Mathf.Min(_animT, end));
                if (_animT >= end)
                {
                    _animT = -1f;
                    if (_hiding)
                    {
                        Destroy(gameObject);
                        return;
                    }

                    _menu.localScale = Vector3.one;
                }
            }

            if (!IsOpen)
            {
                return;
            }

            _cameraAt -= dt;
            if (_cameraAt <= 0f)
            {
                _cameraAt = 0.5f;
                var cam = RrPlayer.Camera;
                if (_canvas.worldCamera != cam)
                {
                    _canvas.worldCamera = cam;
                }
            }

            if (!_board)
            {
                if (RrTravel.Busy)
                {
                    Close("travel");
                    return;
                }

                if (_door == null)
                {
                    Close("door gone");
                    return;
                }

                Vector3 feet;
                try
                {
                    feet = RrPlayer.FeetPosition;
                }
                catch (Exception e)
                {
                    feet = _door.position;
                    RrLog.WarnOnce("Doors", "panel feet", "RrPlayer.FeetPosition failed: " + e.Message);
                }

                Vector3 d = feet - _door.position;
                float up = Mathf.Abs(d.y);
                d.y = 0f;
                bool near = d.magnitude <= WalkAway && up < 3f;
                if (near)
                {
                    _wasNear = true;
                }
                else if (_wasNear)
                {
                    Close("walked away");
                    return;
                }
            }

            UpdatePointer();
            if (_tabs.Count > 0 && _tabs[_tab].MakerRooms != null)
            {
                MakerStep();
            }

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + RefreshSeconds;
                RefreshCounts();
            }
        }

        /// <summary>A maker tab: the cards follow the catalogue; the subtitle shows a room being opened (downloads).</summary>
        private void MakerStep()
        {
            // At most once per refresh period (the catalogue can change in bursts while players announce their rooms).
            if (_filling == null && RrMakerRooms.Serial != _makerSerial && !RrMakerRooms.Opening && Time.unscaledTime >= _refreshAt)
            {
                SelectTab(_tab);
                return;
            }

            string line = RrMakerRooms.Opening ? RrMakerRooms.DownloadLine() ?? RrMakerRooms.Status : null;
            if (line == _statusShown)
            {
                return;
            }

            _statusShown = line;
            _subtitle.text = string.IsNullOrEmpty(line) ? _subtitleText : line;
        }

        private void UpdatePointer()
        {
            bool vr = RrPlayer.InVr;
            RrUiButton over = null;
            Vector3 hit = Vector3.zero;
            Ray ray = RrInput.AimRay;
            bool onPanel = !RrInput.Blocked && RayOnPanel(ray, _board ? BoardReach : DoorReach, out hit);
            if (onPanel)
            {
                over = ButtonAt(hit);
            }

            // A free desktop cursor is the EventSystem's (it hovers and clicks through the GraphicRaycaster); a locked
            // one (play mode, the crosshair) is not seen by it, so we hover for it.
            bool freeCursor = !vr && Cursor.lockState == CursorLockMode.None && Cursor.visible;
            if (!_testHover)
            {
                SetHover(!vr && !freeCursor ? over : null);
            }

            if (onPanel && !vr)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f && _content.rect.height > _viewport.rect.height + 1f)
                {
                    float step = 120f / Mathf.Max(1f, _content.rect.height - _viewport.rect.height);
                    _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + wheel * step);
                }
            }

            if (RrInput.UseDown && over != null)
            {
                Click(over, vr ? "trigger" : "click");
            }

            if (vr)
            {
                Poke(0, RrHandSide.Left);
                Poke(1, RrHandSide.Right);
            }
            else
            {
                _poking[0] = _poking[1] = null;
            }
        }

        private void Poke(int i, RrHandSide side)
        {
            Vector3 tip;
            Vector3 palm;
            RrUiButton b = null;
            if (RrWatchModel.TryPokePoints(side, out tip, out palm))
            {
                Vector3 local = _canvasRect.InverseTransformPoint(tip);
                float depth = local.z * _canvasRect.lossyScale.z;  // + is behind the face (the canvas' +Z faces away from the viewer)
                if (depth > -PokeFront && depth < PokeBack && InsideCanvas(local))
                {
                    b = ButtonAt(tip);
                }
            }

            if (b != null && b != _poking[i])
            {
                Click(b, "poke");
                var hand = RrHand.Get(side);
                if (hand != null)
                {
                    hand.Haptic(0.3f, 0.05f);
                }
            }

            _poking[i] = b;
        }

        private void Click(RrUiButton b, string how)
        {
            if (b == null || Time.unscaledTime - _lastAction < ActionDebounce)
            {
                return;
            }

            if (!b.IsInteractable())
            {
                RrWatchSounds.Down(RrUiSound.Inactive);
                RrTools.Once("Doors", "inactive", "a COMING SOON card was used (" + how + "): nothing to do");
                return;
            }

            RrTools.Once("Doors", "input:" + how, "room list pressed by " + how + " (" + b.name + ")");
            ExecuteEvents.Execute(b.gameObject, _ped, ExecuteEvents.pointerClickHandler);
        }

        private void SetHover(RrUiButton b)
        {
            if (b == _hovered)
            {
                return;
            }

            if (_hovered != null)
            {
                ExecuteEvents.Execute(_hovered.gameObject, _ped, ExecuteEvents.pointerExitHandler);
            }

            _hovered = b;
            if (b != null)
            {
                ExecuteEvents.Execute(b.gameObject, _ped, ExecuteEvents.pointerEnterHandler);
            }
        }

        private bool RayOnPanel(Ray ray, float reach, out Vector3 hit)
        {
            hit = Vector3.zero;
            if (_canvasRect == null)
            {
                return false;
            }

            var plane = new Plane(_canvasRect.forward, _canvasRect.position);
            float d;
            if (!plane.Raycast(ray, out d) || d < 0f || d > reach)
            {
                return false;
            }

            hit = ray.GetPoint(d);
            if (!InsideCanvas(_canvasRect.InverseTransformPoint(hit)))
            {
                return false;
            }

            // A wall between the pointer and the panel hides it.
            RaycastHit block;
            return !(d > 0.05f && Physics.Raycast(ray, out block, d - 0.03f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore));
        }

        private bool InsideCanvas(Vector3 local)
        {
            var r = _canvasRect.rect;
            return local.x >= r.xMin && local.x <= r.xMax && local.y >= r.yMin && local.y <= r.yMax;
        }

        private RrUiButton ButtonAt(Vector3 world)
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                var t = _targets[i];
                if (t.Button == null || !t.Button.isActiveAndEnabled)
                {
                    continue;
                }

                if (t.Clip != null && !t.Clip.rect.Contains((Vector2)t.Clip.InverseTransformPoint(world)))
                {
                    continue;
                }

                var rt = (RectTransform)t.Button.transform;
                if (rt.rect.Contains((Vector2)rt.InverseTransformPoint(world)))
                {
                    return t.Button;
                }
            }

            return null;
        }

        private void RefreshCounts()
        {
            if (_cards.Count == 0)
            {
                return;
            }

            var everyone = RrWatchWorld.Everyone();
            var here = RrWatchWorld.Here;
            var inMaker = RrMakerRoom.Current != null && here == RrActivity.MakerRoom ? RrMakerRoom.Current.RoomId : null;
            foreach (var c in _cards)
            {
                if (!c.Built || c.Lead || c.Card == null || c.Card.Root == null)
                {
                    continue;
                }

                bool maker = c.MakerId != null;
                int n = maker ? RrMakerRooms.PlayersIn(c.MakerId).Count : RrWatchWorld.CountIn(c.Activity, everyone);
                bool mine = maker ? c.MakerId == inMaker : c.Activity == here;
                string text = mine ? "You are here" : n > 0 ? n + " here" : "";
                if (text == c.Shown)
                {
                    continue;
                }

                c.Shown = text;
                if (c.BadgeRoot == null)
                {
                    c.Badge = RrUiKit.Pill(c.Card.Root, "Here", "", RrWatchCard.ArtInset + 10f, RrWatchCard.ArtInset + 10f, 40f, RrUiKit.Badge, Color.white, 22f);
                    c.BadgeRoot = (RectTransform)c.Badge.transform.parent;
                    RrUiKit.UiLayer(c.BadgeRoot.gameObject);
                }

                c.BadgeRoot.gameObject.SetActive(text.Length > 0);
                c.Badge.text = text;
                var img = c.BadgeRoot.GetComponent<Image>();
                if (img != null)
                {
                    img.color = mine ? RrUiKit.PrimaryTo : RrUiKit.Badge;
                }
            }
        }

        private static Vector3 Evaluate(Vector4[] keys, float t)
        {
            if (t <= keys[0].x)
            {
                return new Vector3(keys[0].y, keys[0].z, keys[0].w);
            }

            for (int i = 1; i < keys.Length; i++)
            {
                if (t <= keys[i].x)
                {
                    var a = keys[i - 1];
                    var b = keys[i];
                    float u = Mathf.SmoothStep(0f, 1f, (t - a.x) / Mathf.Max(0.0001f, b.x - a.x));
                    return new Vector3(Mathf.Lerp(a.y, b.y, u), Mathf.Lerp(a.z, b.z, u), Mathf.Lerp(a.w, b.w, u));
                }
            }

            var last = keys[keys.Length - 1];
            return new Vector3(last.y, last.z, last.w);
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            _hovered = null;
            _poking[0] = _poking[1] = null;
        }
    }
}
