using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// The watch menu (rr_watch_ui.md §3.2): one world-space canvas of 2032 x 1230 Rec Room canvas units holding a
    /// 1920-wide cream page (#F9F3EF, r 20) over the orange navigation bar (gradient #FF5F2F -> #FF7D31 at 160 deg,
    /// 150 tall in a headset, 130 on a screen, a 48-tall shadow above it), the page stack and the animations.
    ///
    /// <b>Navigation bar</b>, left to right as the RRUI watch: Profile (the player's name), Detach (headset only),
    /// then Home, Play, People and This Room centred (150 wide each, 20 apart), Settings at the right end. Each tab
    /// is an icon (atlas RRUI_Icon_Atlas) over a Roboto 24 label on a #602121 plate whose alpha is 0 idle, 0.3
    /// hover, 0.4 pressed and 0.5 for the current page. Rec Room's Customize, Create, Store, Chat and Notifications
    /// tabs are left out (nothing behind them in Dreamport; §3.1 allows it).
    ///
    /// <b>Pages</b> are built once, one per frame after the fonts load, and hidden; a tab shows its page, a card
    /// pushes the room details page, <see cref="Back"/> (Backspace on a keyboard) pops. Switching pages plays
    /// PlayerMenu_Transition on the page (Y dips to 0.5 and back with a 1.05/1.10 overshoot, 0.411 s, content swapped
    /// at its lowest) with the transition sound.
    ///
    /// <b>Open / close</b>: PlayerMenu_Show (0.422 s: a horizontal line snaps out, the panel opens vertically with
    /// a 20% overshoot) and PlayerMenu_Hide (0.417 s, reversed), with Rec Room's paper and stereo open/close sounds;
    /// in a headset the world behind dims to 80% black in 0.1 s. Gameplay input is blocked while open; on a screen
    /// the cursor is freed and restored.
    ///
    /// <b>Placement</b>: screen mode 0.8 m in front of the camera, 0.9 m wide, following the view; headset on the
    /// wrist: above the watch face (Rec Room's WatchMenuOrigin is 15 cm off the face), 0.6 m wide (GUESS), facing
    /// the eyes and following the wrist smoothly; detached: world-locked 0.75 m ahead, 0.9 m wide. Without a
    /// tracked wrist in a headset (or the VR test switch) it floats like a detached menu. The pose is written again
    /// just before the player's camera renders, so it never lags the head. On the wrist it hangs off
    /// <see cref="RrWatchModel.FacePosition"/>, which sits on the avatar's drawn wrist; this component runs at
    /// execution order 10020, after the model (10010), so its LateUpdate reads this frame's solved wrist.
    ///
    /// <b>Pointer</b>: GraphicRaycaster + TrackedDeviceGraphicRaycaster, the whole tree on layer 5, worldCamera
    /// re-read every 0.5 s (conventions §10.2, §24 item 17). Clicks come from the client's XRUIInputModule; in the
    /// editor, where there is none, a StandaloneInputModule EventSystem is made.
    /// </summary>
    [DefaultExecutionOrder(10020)]
    public sealed class RrWatchUi : MonoBehaviour
    {
        public const float CanvasWidth = 2032f;
        public const float CanvasHeight = 1230f;
        public const float SurfaceWidth = 1920f;
        public const float NavHeightVr = 150f;
        public const float NavHeightDesktop = 130f;
        public const float TabWidth = 150f;
        public const float TabSpacing = 20f;

        public const float DesktopDistance = 0.8f;
        public const float DesktopWidth = 0.9f;
        public const float WristWidth = 0.6f;
        public const float DetachedWidth = 0.9f;
        public const float DetachedDistance = 0.75f;
        public const float DimAlpha = 0.8f;
        public const float DimFade = 0.1f;

        public const float ShowTime = 0.422f;
        public const float HideTime = 0.417f;
        public const float TransitionTime = 0.411f;
        private const float SwapAt = 0.2f;

        // PlayerMenu_Show / _Hide / _Transition scale keys (t, x, y, z), decoded from the animator's clips (§3.4).
        private static readonly Vector4[] ShowKeys = { new Vector4(0f, 0.01f, 0.01f, 1f), new Vector4(0.056f, 1f, 0.046f, 1f), new Vector4(0.211f, 1.2f, 1.2f, 1.2f), new Vector4(0.422f, 1f, 1f, 1f) };
        private static readonly Vector4[] HideKeys = { new Vector4(0f, 1f, 1f, 1f), new Vector4(0.2f, 1.2f, 1.2f, 1.2f), new Vector4(0.367f, 1f, 0.046f, 1f), new Vector4(0.417f, 0.01f, 0.01f, 1f) };
        private static readonly Vector4[] TransitionKeys = { new Vector4(0f, 1f, 1f, 1f), new Vector4(0.044f, 1.05f, 1.1f, 1.05f), new Vector4(0.133f, 1f, 0.5f, 1f), new Vector4(0.267f, 1f, 0.5f, 1f), new Vector4(0.367f, 1.05f, 1.1f, 1.05f), new Vector4(0.411f, 1f, 1f, 1f) };

        /// <summary>Test switch: hold the menu at this world pose (the canvas faces +Z away from the viewer).</summary>
        public static Pose? TestPose;

        public static RrWatchUi Instance { get; private set; }

        public Canvas Canvas { get; private set; }

        public bool Built { get; private set; }

        /// <summary>Open or opening (false as soon as it starts closing).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Headset: the menu was detached from the wrist and floats world-locked.</summary>
        public bool Detached { get; private set; }

        /// <summary>The layout in use: headset (true) or screen.</summary>
        public bool VrLayout { get; private set; }

        public RrWatchPageId Current
        {
            get { return _current != null ? _current.Id : RrWatchPageId.Home; }
        }

        private struct Entry
        {
            public RrWatchPageId Id;
            public RrActivity Activity;
        }

        private sealed class Tab
        {
            public RrWatchPageId Page;
            public RrUiButton Button;
            public RectTransform Rect;
            public RectTransform Icon;
            public RectTransform Label;
            public TextMeshProUGUI Text;
        }

        private readonly Dictionary<RrWatchPageId, RrWatchPage> _pages = new Dictionary<RrWatchPageId, RrWatchPage>();
        private readonly List<Tab> _tabs = new List<Tab>();
        private readonly List<Entry> _history = new List<Entry>();

        private RectTransform _canvasRect;
        private RectTransform _menu;
        private RectTransform _surface;
        private RectTransform _page;
        private RectTransform _pageContent;
        private RectTransform _nav;
        private RectTransform _navShadow;
        private Image _dim;
        private Tab _profile;
        private Tab _detach;
        private Image _detachIcon;
        private GraphicRaycaster _raycaster;
        private TrackedDeviceGraphicRaycaster _trackedRaycaster;
        private GameObject _eventSystem;

        private RrWatchPage _current;
        private RrWatchPage _pending;
        private RrActivity _roomActivity;
        private float _transT = -1f;
        private float _animT = -1f;
        private bool _hiding;
        private bool _openWhenBuilt;
        private float _dimAlpha;
        private float _cameraCheck;
        private float _refreshAt;
        private bool _placedOnce;
        private Vector3 _pos;
        private Quaternion _rot = Quaternion.identity;
        private float _width = DesktopWidth;
        private Pose _floating;
        private bool _floatingSet;
        private CursorLockMode _savedLock;
        private bool _savedVisible;
        private bool _cursorSaved;
        private bool _loggedOpen;
        private string _nick = "";

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        private IEnumerator Start()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            yield return RrUiKit.LoadFonts();
            RrWatchSounds.Init(transform);
            RrWatchArt.Preload();
            yield return null;

            BuildFrame();
            yield return null;

            AddPage(new RrWatchHomePage());
            yield return null;
            AddPage(new RrWatchPlayPage());
            yield return null;
            AddPage(new RrWatchPeoplePage());
            yield return null;
            AddPage(new RrWatchThisRoomPage());
            yield return null;
            AddPage(new RrWatchSettingsPage());
            yield return null;
            AddPage(new RrWatchRoomPage());
            yield return null;
            AddPage(new RrWatchCreatePage());
            yield return null;
            AddPage(new RrWatchRoomsPage());
            yield return null;
            AddPage(new RrWatchMakerPage());
            yield return null;
            AddPage(new RrWatchAvatarPage());
            yield return null;

            RrUiKit.UiLayer(Canvas.gameObject);
            ApplyLayout(RrWatch.VrLayout);
            Swap(_pages[RrWatchPageId.Home]);
            Canvas.enabled = false;
            SetRaycasters(false);
            Built = true;

            RrNet.OnPlayersChanged += OnWorldChanged;
            RrTravel.OnArrived += OnArrived;

            int graphics = Canvas.GetComponentsInChildren<Graphic>(true).Length;
            RrLog.Info("Watch", "menu built: " + _pages.Count + " pages, " + graphics + " graphics, " + sw.ElapsedMilliseconds + " ms (layout " + (VrLayout ? "VR" : "desktop") + ")");
            if (_openWhenBuilt)
            {
                _openWhenBuilt = false;
                Open();
            }
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RrNet.OnPlayersChanged -= OnWorldChanged;
            RrTravel.OnArrived -= OnArrived;
            if (IsOpen)
            {
                RrInput.SetBlocked(RrWatch.InputOwner, false);
                RestoreCursor();
            }

            if (_eventSystem != null)
            {
                Destroy(_eventSystem);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---- building ----

        private void BuildFrame()
        {
            var go = new GameObject("Rr Watch Menu", typeof(RectTransform));
            go.layer = RrLayers.Ui;
            _canvasRect = (RectTransform)go.transform;
            _canvasRect.SetParent(transform, false);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.WorldSpace;
            _canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _canvasRect.localScale = Vector3.one * (DesktopWidth / CanvasWidth);
            _raycaster = go.AddComponent<GraphicRaycaster>();
            _trackedRaycaster = go.AddComponent<TrackedDeviceGraphicRaycaster>();

            // The fade plane (WatchMenuFadePlane): black behind the menu, 80% in a headset. Far larger than the view.
            var dim = RrUiKit.Centered(_canvasRect, "Dim", 0f, 0f, 60000f, 60000f);
            _dim = RrUiKit.Solid(dim, new Color(0f, 0f, 0f, 0f));
            _dim.enabled = false;

            _menu = RrUiKit.Centered(_canvasRect, "Menu", 0f, 0f, CanvasWidth, CanvasHeight);
            _surface = RrUiKit.TopLeft(_menu, "Surface", (CanvasWidth - SurfaceWidth) * 0.5f, 0f, SurfaceWidth, CanvasHeight);

            _page = RrUiKit.TopLeft(_surface, "Page", 0f, 0f, SurfaceWidth, 1100f);
            _page.pivot = new Vector2(0.5f, 0.5f);
            RrUiKit.Rounded(_page, RrUiKit.Page, 20f, true);
            _pageContent = RrUiKit.TopLeft(_page, "Content", 0f, 0f, RrWatchPage.Width, RrWatchPage.Height);
            _pageContent.gameObject.AddComponent<RectMask2D>();

            _navShadow = RrUiKit.TopLeft(_surface, "NavBarShadow", 0f, 1080f - 48f, SurfaceWidth, 48f);
            var shadow = RrUiKit.Solid(_navShadow, Color.white);
            RrUiKit.Gradient(shadow, new Color(0f, 0f, 0f, 0.5f), new Color(0f, 0f, 0f, 0f), 90f);

            _nav = RrUiKit.TopLeft(_surface, "NavBar", 0f, 1080f, SurfaceWidth, NavHeightVr);
            var bar = RrUiKit.Rounded(_nav, Color.white, 20f, true);
            RrUiKit.Gradient(bar, RrUiKit.NavFrom, RrUiKit.NavTo, 160f, 0.3f, 1f);

            _profile = MakeTab(RrWatchPageId.People, "Profile", "icon_ProfilePic", RrWatchSounds.FlavorProfile, true);
            _profile.Button.onClick.AddListener(() => TabClicked(RrWatchPageId.People));
            _detach = MakeTab(RrWatchPageId.Home, "Detach", "icon_Expand", null, false);
            _detachIcon = _detach.Icon.GetComponent<Image>();
            _detach.Label.gameObject.SetActive(false);
            _detach.Button.onClick.AddListener(ToggleDetach);

            AddTab(RrWatchPageId.Home, "Home", "icon_Home", RrWatchSounds.FlavorHome);
            AddTab(RrWatchPageId.Play, "Play", "icon_Nav_Play", null);
            AddTab(RrWatchPageId.People, "People", "icon_Players", RrWatchSounds.FlavorPeople);
            AddTab(RrWatchPageId.ThisRoom, "This Room", "icon_nav_ThisRoom", RrWatchSounds.FlavorThisRoom);
            AddTab(RrWatchPageId.Settings, "Settings", "icon_Gear", RrWatchSounds.FlavorSettings);
        }

        private Tab MakeTab(RrWatchPageId page, string label, string icon, string flavor, bool profile)
        {
            var rt = RrUiKit.TopLeft(_nav, "Tab " + label, 0f, 15f, TabWidth, NavHeightVr - 25f);
            var b = RrUiKit.PlateButton(rt, RrUiKit.TabPlate, 20f, new Vector4(0f, 0.3f, 0.4f, 0f), RrUiSound.Sidebar);
            b.Flavor = flavor;
            var tab = new Tab { Page = page, Button = b, Rect = rt };
            tab.Icon = RrUiKit.TopLeft(rt, "Icon", 0f, 0f, 100f, 100f);
            if (profile)
            {
                // Rec Room's profile button: the photo in a round mask; here the empty-profile icon on #B2A8A2.
                RrUiKit.Circle(tab.Icon, RrUiKit.ProfileBack);
                RrUiKit.Icon(RrUiKit.Fill(tab.Icon, "Picture", 14f, 10f, 14f, 18f), icon, Color.white);
            }
            else
            {
                RrUiKit.Icon(tab.Icon, icon, Color.white);
            }

            tab.Label = RrUiKit.TopLeft(rt, "Label", 4f, 0f, TabWidth - 8f, 30f);
            tab.Text = RrUiKit.Text(tab.Label, label, 24f, Color.white, TextAlignmentOptions.Center);
            tab.Text.characterSpacing = 2f;
            tab.Text.enableAutoSizing = true;
            tab.Text.fontSizeMin = 18f;
            tab.Text.fontSizeMax = 24f;
            return tab;
        }

        private void AddTab(RrWatchPageId page, string label, string icon, string flavor)
        {
            var tab = MakeTab(page, label, icon, flavor, false);
            tab.Button.onClick.AddListener(() => TabClicked(page));
            _tabs.Add(tab);
        }

        private void AddPage(RrWatchPage page)
        {
            page.Ui = this;
            page.Root = RrUiKit.TopLeft(_pageContent, "Page " + page.Id, 0f, 0f, RrWatchPage.Width, RrWatchPage.Height);
            try
            {
                page.Build();
            }
            catch (System.Exception e)
            {
                RrLog.Error("Watch", "page " + page.Id + " failed to build", e);
            }

            page.Root.gameObject.SetActive(false);
            _pages[page.Id] = page;
        }

        /// <summary>Lays out the navigation bar for a headset (150 tall, detach button) or a screen (130 tall).</summary>
        private void ApplyLayout(bool vr)
        {
            VrLayout = vr;
            float navH = vr ? NavHeightVr : NavHeightDesktop;
            float navTop = CanvasHeight - navH;
            _nav.anchoredPosition = new Vector2(0f, -navTop);
            _nav.sizeDelta = new Vector2(SurfaceWidth, navH);
            _navShadow.anchoredPosition = new Vector2(0f, -(navTop - 48f));
            _page.sizeDelta = new Vector2(SurfaceWidth, navTop + 20f);
            _page.anchoredPosition = new Vector2(SurfaceWidth * 0.5f, -(navTop + 20f) * 0.5f);
            _pageContent.anchoredPosition = Vector2.zero;

            float tabH = navH - 25f;
            float icon = vr ? 80f : 64f;
            float labelTop = vr ? 88f : 72f;

            // Centre group: Home, Play, People, This Room; Settings alone at the right end (RRUI nav bar).
            int centre = _tabs.Count - 1;
            float groupWidth = centre * TabWidth + (centre - 1) * TabSpacing;
            float x = (SurfaceWidth - groupWidth) * 0.5f;
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                float tx = i < centre ? x + i * (TabWidth + TabSpacing) : SurfaceWidth - 10f - TabWidth;
                PlaceTab(t, tx, tabH, icon, labelTop);
            }

            PlaceTab(_profile, 10f, tabH, icon, labelTop);
            _detach.Rect.gameObject.SetActive(vr);
            _detach.Rect.anchoredPosition = new Vector2(10f + TabWidth + TabSpacing, -(navH - 110f) * 0.5f);
            _detach.Rect.sizeDelta = new Vector2(110f, 110f);
            _detach.Icon.anchoredPosition = new Vector2(20f, -20f);
            _detach.Icon.sizeDelta = new Vector2(70f, 70f);
            if (!vr && Detached)
            {
                Detached = false;
            }
        }

        private static void PlaceTab(Tab t, float x, float h, float icon, float labelTop)
        {
            t.Rect.anchoredPosition = new Vector2(x, -15f);
            t.Rect.sizeDelta = new Vector2(TabWidth, h);
            t.Icon.anchoredPosition = new Vector2((TabWidth - icon) * 0.5f, -6f);
            t.Icon.sizeDelta = new Vector2(icon, icon);
            t.Label.anchoredPosition = new Vector2(4f, -labelTop);
        }

        // ---- navigation ----

        private void TabClicked(RrWatchPageId page)
        {
            _history.Clear();
            ShowPage(page, false);
        }

        /// <summary>Shows a page (pushing the current one for <see cref="Back"/>), with the transition when the menu is open.</summary>
        public void ShowPage(RrWatchPageId id)
        {
            ShowPage(id, true);
        }

        private void ShowPage(RrWatchPageId id, bool remember)
        {
            RrWatchPage page;
            if (!_pages.TryGetValue(id, out page))
            {
                return;
            }

            var shown = _pending ?? _current;
            if (shown == page && id != RrWatchPageId.Room)
            {
                return;
            }

            if (remember && shown != null)
            {
                _history.Add(new Entry { Id = shown.Id, Activity = _roomActivity });
                if (_history.Count > 16)
                {
                    _history.RemoveAt(0);
                }
            }

            if (IsOpen && _current != null && Canvas.enabled)
            {
                _pending = page;
                if (_transT < 0f || _transT >= SwapAt)
                {
                    _transT = 0f;
                    RrWatchSounds.Play(RrWatchSounds.Transition);
                }
            }
            else
            {
                Swap(page);
            }

            SetActiveTab(id);
        }

        /// <summary>Opens the room details page of an activity (a card was clicked).</summary>
        public void ShowRoom(RrActivity activity)
        {
            // Player-made rooms are many rooms under one activity: their card opens the Rooms page.
            if (activity == RrActivity.MakerRoom)
            {
                ShowPage(RrWatchPageId.Rooms);
                return;
            }

            RrWatchPage page;
            if (!_pages.TryGetValue(RrWatchPageId.Room, out page))
            {
                return;
            }

            var shown = _pending ?? _current;
            if (shown != null)
            {
                _history.Add(new Entry { Id = shown.Id, Activity = _roomActivity });
            }

            _roomActivity = activity;
            ((RrWatchRoomPage)page).Set(activity);
            ShowPage(RrWatchPageId.Room, false);
        }

        /// <summary>Goes back to the previous page (Backspace, the room page's back arrow).</summary>
        public void Back()
        {
            if (_history.Count == 0)
            {
                if (Current != RrWatchPageId.Home)
                {
                    ShowPage(RrWatchPageId.Home, false);
                }

                return;
            }

            var e = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            if (e.Id == RrWatchPageId.Room)
            {
                _roomActivity = e.Activity;
                var room = _pages[RrWatchPageId.Room] as RrWatchRoomPage;
                if (room != null)
                {
                    room.Set(e.Activity);
                }
            }

            ShowPage(e.Id, false);
        }

        /// <summary>Closes the menu and travels (a "Go", "Join", "Leave" or quick-access button).</summary>
        public void Travel(RrActivity activity, int instance)
        {
            if (RrTravel.Busy)
            {
                RrLog.WarnOnce("Watch", "travel busy", "travel requested while another is running; ignored");
                return;
            }

            bool here = activity == RrWatchWorld.Here && (instance < 0 || instance == RrWatchWorld.HereInstance);
            Close();
            if (here)
            {
                return;
            }

            RrLog.Info("Watch", "travel to " + activity + (instance >= 0 ? " #" + instance : ""));
            RrTravel.GoTo(activity, instance);
        }

        private void Swap(RrWatchPage page)
        {
            if (_current != null && _current != page)
            {
                _current.Root.gameObject.SetActive(false);
            }

            _current = page;
            _pending = null;
            page.Root.gameObject.SetActive(true);
            try
            {
                page.Show();
            }
            catch (System.Exception e)
            {
                RrLog.Error("Watch", "page " + page.Id + " failed to show", e);
            }
        }

        private void SetActiveTab(RrWatchPageId id)
        {
            var tabId = id == RrWatchPageId.Room ? RrWatchPageId.Play : id;
            foreach (var t in _tabs)
            {
                t.Button.Active = t.Page == tabId;
            }
        }

        // ---- open / close ----

        public void Open()
        {
            if (!Built)
            {
                _openWhenBuilt = true;
                return;
            }

            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            _hiding = false;
            _animT = 0f;
            _placedOnce = false;
            _floatingSet = false;
            bool vr = RrWatch.VrLayout;
            if (vr != VrLayout)
            {
                ApplyLayout(vr);
            }

            RefreshCamera();
            Place(true);
            Canvas.enabled = true;
            SetRaycasters(true);
            _menu.localScale = new Vector3(0.01f, 0.01f, 1f);
            UpdateProfile();
            if (_current != null)
            {
                Swap(_current);
                SetActiveTab(_current.Id);
            }

            RrWatchSounds.Play(RrWatchSounds.Open);
            RrWatchSounds.Play(RrWatchSounds.OpenStereo);
            RrInput.SetBlocked(RrWatch.InputOwner, true);
            if (!VrLayout)
            {
                FreeCursor();
                EnsureEventSystem();
            }

            if (!_loggedOpen)
            {
                _loggedOpen = true;
                RrLog.Info("Watch", "menu opened (" + Mode() + ") at " + _pos.ToString("F2") + ", " + _width.ToString("F2") + " m wide, camera " + (RrPlayer.Camera != null ? RrPlayer.Camera.name : "none"));
            }

            RrWatch.RaiseOpenChanged(true);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                _openWhenBuilt = false;
                return;
            }

            IsOpen = false;
            _hiding = true;
            _animT = 0f;
            if (RrWatch.Model != null)
            {
                RrWatch.Model.OpenedByWrist = false;
            }

            RrWatchSounds.Play(RrWatchSounds.Close);
            RrWatchSounds.Play(RrWatchSounds.CloseStereo);
            RrInput.SetBlocked(RrWatch.InputOwner, false);
            SetRaycasters(false);
            RestoreCursor();
            RrWatch.RaiseOpenChanged(false);
        }

        private string Mode()
        {
            if (TestPose.HasValue)
            {
                return VrLayout ? "VR layout, test pose" : "desktop, test pose";
            }

            if (!VrLayout)
            {
                return "desktop";
            }

            return Detached ? "VR detached" : WristReady() ? "VR wrist" : "VR floating";
        }

        /// <summary>Headset: detach the menu from the wrist (it stays where it is) or bring it back.</summary>
        public void ToggleDetach()
        {
            if (!VrLayout || !IsOpen)
            {
                return;
            }

            Detached = !Detached;
            if (Detached)
            {
                Vector3 head = RrPlayer.HeadPosition;
                Vector3 dir = _pos - head;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = RrPlayer.HeadRotation * Vector3.forward;
                    dir.y = 0f;
                }

                dir.Normalize();
                Vector3 pos = head + dir * DetachedDistance;
                _floating = new Pose(pos, Quaternion.LookRotation(dir, Vector3.up));
                _floatingSet = true;
            }

            if (_detachIcon != null)
            {
                RrWatchArt.Apply(_detachIcon, Detached ? "icon_Collapse" : "icon_Expand");
            }

            RrLog.Info("Watch", Detached ? "menu detached" : "menu back on the wrist");
        }

        private void FreeCursor()
        {
            if (!_cursorSaved)
            {
                _savedLock = Cursor.lockState;
                _savedVisible = Cursor.visible;
                _cursorSaved = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreCursor()
        {
            if (!_cursorSaved)
            {
                return;
            }

            _cursorSaved = false;
            Cursor.lockState = _savedLock;
            Cursor.visible = _savedVisible;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null || _eventSystem != null)
            {
                return;
            }

            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            _eventSystem = new GameObject("Rr Watch EventSystem");
            _eventSystem.transform.SetParent(transform, false);
            _eventSystem.AddComponent<EventSystem>();
            _eventSystem.AddComponent<StandaloneInputModule>();
            RrLog.Info("Watch", "no EventSystem in the scene (editor): made one with a StandaloneInputModule");
        }

        private void SetRaycasters(bool on)
        {
            if (_raycaster != null)
            {
                _raycaster.enabled = on;
            }

            if (_trackedRaycaster != null)
            {
                _trackedRaycaster.enabled = on;
            }
        }

        // ---- per frame ----

        private void Update()
        {
            if (!Built)
            {
                if (RrInput.MenuDown)
                {
                    _openWhenBuilt = !_openWhenBuilt;
                }

                return;
            }

            bool vr = RrWatch.VrLayout;
            if (vr != VrLayout && !IsOpen && !_hiding)
            {
                ApplyLayout(vr);
            }

            // While the on-watch keyboard takes typing (desktop), H and Backspace are letters, not the menu's keys.
            bool typing = RrWatchKeyboard.Typing;
            if (RrInput.MenuDown && !typing)
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
            else if (IsOpen && RrInput.BackDown && !typing)
            {
                Back();
            }

            Animate(Time.unscaledDeltaTime);

            if (IsOpen)
            {
                _cameraCheck -= Time.unscaledDeltaTime;
                if (_cameraCheck <= 0f)
                {
                    _cameraCheck = 0.5f;
                    RefreshCamera();
                }

                if (_current != null && _pending == null)
                {
                    _current.Tick();
                    if (Time.unscaledTime >= _refreshAt)
                    {
                        _refreshAt = Time.unscaledTime + 2f;
                        _current.Refresh();
                        UpdateProfile();
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (Built && Canvas.enabled)
            {
                Place(false);
            }
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (Built && Canvas.enabled && cam == RrPlayer.Camera)
            {
                Place(false);
            }
        }

        private void Animate(float dt)
        {
            if (_animT >= 0f)
            {
                _animT += dt;
                var keys = _hiding ? HideKeys : ShowKeys;
                float end = _hiding ? HideTime : ShowTime;
                _menu.localScale = Evaluate(keys, Mathf.Min(_animT, end));
                if (_animT >= end)
                {
                    _animT = -1f;
                    _menu.localScale = _hiding ? new Vector3(0.01f, 0.01f, 1f) : Vector3.one;
                    if (_hiding)
                    {
                        _hiding = false;
                        Canvas.enabled = false;
                        Detached = false;
                        if (_detachIcon != null)
                        {
                            RrWatchArt.Apply(_detachIcon, "icon_Expand");
                        }

                        if (_pending != null)
                        {
                            Swap(_pending);
                        }

                        _transT = -1f;
                        _page.localScale = Vector3.one;
                    }
                }
            }

            if (_transT >= 0f)
            {
                float before = _transT;
                _transT += dt;
                if (before < SwapAt && _transT >= SwapAt && _pending != null)
                {
                    Swap(_pending);
                }

                _page.localScale = Evaluate(TransitionKeys, Mathf.Min(_transT, TransitionTime));
                if (_transT >= TransitionTime)
                {
                    _transT = -1f;
                    _page.localScale = Vector3.one;
                    if (_pending != null)
                    {
                        Swap(_pending);
                    }
                }
            }

            float dimTarget = (IsOpen && VrLayout) ? DimAlpha : 0f;
            if (!Mathf.Approximately(_dimAlpha, dimTarget))
            {
                _dimAlpha = Mathf.MoveTowards(_dimAlpha, dimTarget, dt * DimAlpha / DimFade);
                _dim.color = new Color(0f, 0f, 0f, _dimAlpha);
            }

            // A clear full-view quad still costs fill rate: draw it only while it darkens something.
            bool dimOn = _dimAlpha > 0.001f;
            if (_dim.enabled != dimOn)
            {
                _dim.enabled = dimOn;
            }
        }

        /// <summary>Samples scale keys (t, x, y, z) with ease-in-out between keys, like the clips' smooth tangents.</summary>
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

        private bool WristReady()
        {
            var model = RrWatch.Model;
            return model != null && model.OnWrist && !RrWatchModel.TestPose.HasValue;
        }

        /// <summary>Puts the canvas where it belongs this frame (see the class notes).</summary>
        private void Place(bool snap)
        {
            Vector3 pos;
            Quaternion rot;
            float width;
            var cam = RrPlayer.Camera;
            if (TestPose.HasValue)
            {
                pos = TestPose.Value.position;
                rot = TestPose.Value.rotation;
                width = VrLayout ? DetachedWidth : DesktopWidth;
                snap = true;
            }
            else if (!VrLayout)
            {
                if (cam == null)
                {
                    return;
                }

                var ct = cam.transform;
                pos = ct.position + ct.forward * DesktopDistance;
                rot = ct.rotation;
                width = DesktopWidth;
                snap = true;
            }
            else if (Detached || !WristReady())
            {
                if (!_floatingSet)
                {
                    Vector3 head = RrPlayer.HeadPosition;
                    Vector3 fwd = RrPlayer.HeadRotation * Vector3.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.0001f)
                    {
                        fwd = Vector3.forward;
                    }

                    fwd.Normalize();
                    _floating = new Pose(head + fwd * DetachedDistance, Quaternion.LookRotation(fwd, Vector3.up));
                    _floatingSet = true;
                }

                pos = _floating.position;
                rot = _floating.rotation;
                width = DetachedWidth;
            }
            else
            {
                // Above the watch face, facing the eyes; follows the wrist with a little smoothing against tremor.
                var model = RrWatch.Model;
                Vector3 head = RrPlayer.HeadPosition;
                Vector3 face = model.FacePosition;
                Vector3 away = face - head;
                away.y = 0f;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
                pos = face + Vector3.up * 0.22f + away * 0.06f;
                Vector3 look = pos - head;
                rot = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity;
                width = WristWidth;
            }

            if (snap || !_placedOnce)
            {
                _pos = pos;
                _rot = rot;
                _width = width;
                _placedOnce = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
                _pos = Vector3.Lerp(_pos, pos, k);
                _rot = Quaternion.Slerp(_rot, rot, k);
                _width = Mathf.Lerp(_width, width, k);
            }

            _canvasRect.SetPositionAndRotation(_pos, _rot);
            float s = _width / CanvasWidth;
            _canvasRect.localScale = new Vector3(s, s, s);
        }

        private void RefreshCamera()
        {
            var cam = RrPlayer.Camera;
            if (Canvas != null && Canvas.worldCamera != cam)
            {
                Canvas.worldCamera = cam;
            }
        }

        private void UpdateProfile()
        {
            string nick = RrPlayer.Nick;
            if (string.IsNullOrEmpty(nick))
            {
                nick = "Profile";
            }

            if (nick != _nick && _profile != null)
            {
                _nick = nick;
                _profile.Text.text = nick;
            }
        }

        private void OnWorldChanged()
        {
            if (IsOpen && _current != null)
            {
                _refreshAt = 0f;
            }
        }

        private void OnArrived(RrActivity activity)
        {
            if (_current != null)
            {
                _refreshAt = 0f;
            }
        }
    }
}
