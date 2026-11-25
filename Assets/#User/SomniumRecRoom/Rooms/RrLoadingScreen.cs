using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// The fade and the loading card shown while the player travels between rooms, rebuilt in Rec Room's look.
    ///
    /// Rec Room's own [LoadingScreen] (Startup bundle 063b6b66, rr_reccenter.md §2.2) fades to black in 0.15 s,
    /// shows the destination - "GOING TO", the room name written with a caret ("^RecCenter"), the sub-room, an
    /// animated "LOADING..." (0.3 s per dot), a progress bar, a tip every 2-5 s - over the room's picture, then
    /// fades out in 0.15 s into the new room. Its rounded panels and gradients are Rec Room scripts that do not
    /// run here, so the card is drawn with UGUI + TMP from its texts and colours: Rec Room orange
    /// (0.992, 0.510, 0.302) and the warm off-white (0.945, 0.878, 0.847), Rec Room's beige logo pattern behind,
    /// its logo, and the activity's cover art when the bundles have one (rec.net served the real room pictures).
    ///
    /// Desktop: a screen-space overlay canvas (sorting 900, over the world's own status label at 100), no
    /// raycaster, so it never takes the client's clicks. VR: screen overlays are not drawn into a headset
    /// (conventions §10.2), so the black is an inside-out sphere of 0.5 m around the head, drawn after
    /// everything (queue 3990, depth test off), and the card is a 1.6 m world-space panel 2 m ahead of the eyes,
    /// yaw-locked, turning to follow only when the head looks more than 40 degrees away. Its graphics get
    /// copies of their materials with depth testing off in the overlay queue - the way AoM keeps its arm panel
    /// readable in the client. Neither touches the client's camera: both only follow its position.
    /// </summary>
    public sealed class RrLoadingScreen : MonoBehaviour
    {
        /// <summary>Rec Room's LoadingScreen.fadeInDuration / fadeOutDuration.</summary>
        public const float FadeSeconds = 0.15f;

        /// <summary>Rec Room's displayDurationPerElipse.</summary>
        public const float DotSeconds = 0.3f;

        /// <summary>Rec Room's min/maxDisplayDurationPerTip.</summary>
        public const float TipMinSeconds = 2f;
        public const float TipMaxSeconds = 5f;

        public static readonly Color Orange = new Color(0.992f, 0.510f, 0.302f);
        public static readonly Color OffWhite = new Color(0.945f, 0.878f, 0.847f);
        private static readonly Color BarColor = new Color(0.13f, 0.12f, 0.16f, 0.95f);

        private const float FrameW = 1920f;
        private const float FrameH = 1080f;
        private const float PanelWidth = 1.6f;
        private const float PanelDistance = 2f;
        private const float SphereRadius = 0.5f;
        private const int SortingOrder = 900;
        private const int SphereQueue = 3990;
        private const int PanelQueue = (int)RenderQueue.Overlay;

        private const string LogoSprite = "RecRoomLogo_Sprite";
        private const string PatternTexture = "Graphic_RRLogoTilingBackground_16x9_Beige";

        // Our own tips: Rec Room's came from a localisation table that is not in the plain bundles.
        private static readonly string[] Tips =
        {
            "Walk into an activity door, or click it, to go there.",
            "Your dorm is yours alone: nobody else can come in.",
            "The Rec Center is where everyone meets between games.",
            "In VR, grab with the grip and use things with the trigger.",
            "Most things you can pick up can also be thrown.",
            "Games fill up before new ones open: you join the fullest one first.",
            "Everything here is loaded from your own Rec Room install."
        };

        public static RrLoadingScreen Instance { get; private set; }

        private Card _screenCard;
        private Card _worldCard;
        private Card _active;
        private GameObject _sphere;
        private Material _sphereMaterial;
        private Mesh _sphereMesh;
        private readonly List<Material> _lifted = new List<Material>();
        private readonly HashSet<Material> _liftedSet = new HashSet<Material>();

        private bool _forceWorld;
        private bool _showing;
        private float _black;
        private float _card;
        private float _progress;
        private float _shownProgress;
        private int _shownPercent = -1;
        private int _dots;
        private float _nextDot;
        private int _tip;
        private float _nextTip;
        private Vector3 _panelDir = Vector3.forward;
        private bool _turning;
        private string _coverSpec;
        private RrArt _logo;
        private RrArt _pattern;
        private RrArt _cover;

        /// <summary>
        /// Polled every frame while the card is up (the bar only moves forward): the trip points it at the room's
        /// own build progress so the bar keeps moving through a long build without the room calling back.
        /// </summary>
        [System.NonSerialized] public System.Func<float> ProgressSource;

        /// <summary>The view is fully black or the card is up: the world behind can change unseen.</summary>
        public static bool Covered
        {
            get { return Instance != null && Instance._showing && Instance._black >= 0.999f; }
        }

        /// <summary>Anything of the fade or card is on screen.</summary>
        public static bool Showing
        {
            get { return Instance != null && Instance._showing; }
        }

        public static RrLoadingScreen Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("Rr Loading Screen");
            if (RrMain.Instance != null && RrMain.Instance.WorldRoot != null)
            {
                go.transform.SetParent(RrMain.Instance.WorldRoot, false);
            }

            return go.AddComponent<RrLoadingScreen>();
        }

        private void Awake()
        {
            Instance = this;
            Application.onBeforeRender += OnBeforeRender;
            RrCoverArt.Load(LogoSprite, art =>
            {
                _logo = art;
                Refresh();
            });
            RrCoverArt.Load(PatternTexture, art =>
            {
                _pattern = art;
                Refresh();
            });
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
            if (Instance == this)
            {
                Instance = null;
            }

            foreach (var m in _lifted)
            {
                if (m != null)
                {
                    Destroy(m);
                }
            }

            _lifted.Clear();
            _liftedSet.Clear();
            if (_sphereMaterial != null)
            {
                Destroy(_sphereMaterial);
            }

            if (_sphereMesh != null)
            {
                Destroy(_sphereMesh);
            }
        }

        // ---- the trip's calls ----

        /// <summary>Fades to black (0.15 s), then brings the card up for the destination.</summary>
        public IEnumerator Cover(RrRoomInfo info, RrActivity activity)
        {
            Show(info, activity);
            yield return Animate(true, 1f, FadeSeconds);
            yield return Animate(false, 1f, FadeSeconds);
        }

        /// <summary>Takes the card down and fades from black into the room (0.15 s each).</summary>
        public IEnumerator Uncover()
        {
            if (!_showing)
            {
                yield break;
            }

            _progress = 1f;
            yield return Animate(false, 0f, FadeSeconds);
            yield return Animate(true, 0f, FadeSeconds);
            Hide();
        }

        /// <summary>Drives the progress bar (0..1); the bar eases towards it.</summary>
        public void SetProgress(float value)
        {
            _progress = Mathf.Max(_progress, Mathf.Clamp01(value));
        }

        /// <summary>A line on the card in Rec Room orange (a failed load, a redirect); null clears it.</summary>
        public void SetMessage(string text)
        {
            if (_active == null)
            {
                return;
            }

            _active.Message.text = text ?? "";
            _active.MessagePill.gameObject.SetActive(!string.IsNullOrEmpty(text));
            Lift();
        }

        /// <summary>Changes the destination shown (the trip was redirected, e.g. to the dorm after a failure).</summary>
        public void SetDestination(RrRoomInfo info, RrActivity activity)
        {
            if (_active != null)
            {
                Fill(info, activity);
            }
        }

        // ---- diagnostics (play-mode harness) ----

        /// <summary>
        /// Shows the card fully, in its VR form (sphere and world panel ahead of the current head), and keeps it
        /// up until <see cref="DebugPreviewEnd"/>: the harness renders its own cameras, which never see a screen
        /// overlay, so this is how the card's look is checked outside a headset.
        /// </summary>
        public static void DebugPreview()
        {
            var s = Ensure();
            s._forceWorld = true;
            var info = RrRooms.Get(RrActivity.RecCenter);
            s.Show(info, RrActivity.RecCenter);
            s._black = 1f;
            s._card = 1f;
            s._progress = 0.42f;
            s._shownProgress = 0.42f;
            s.Apply();
            s.Place(true);
            RrLog.Info("Loading", "preview (VR look) around the head at " + Fmt(RrPlayer.HeadPosition) + ", panel at " + Fmt(s._worldCard.Canvas.transform.position));
        }

        public static void DebugPreviewEnd()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.Hide();
            Instance._forceWorld = false;
        }

        // ---- showing ----

        private void Show(RrRoomInfo info, RrActivity activity)
        {
            bool world = _forceWorld || RrPlayer.InVr;
            var card = world ? (_worldCard ?? (_worldCard = Build(true))) : (_screenCard ?? (_screenCard = Build(false)));
            if (_active != null && _active != card)
            {
                _active.Canvas.gameObject.SetActive(false);
            }

            _active = card;
            _showing = true;
            _progress = 0f;
            _shownProgress = 0f;
            _shownPercent = -1;
            card.Canvas.gameObject.SetActive(true);
            if (world)
            {
                EnsureSphere();
                _sphere.SetActive(true);
                _turning = false;
                _panelDir = FlatForward(RrPlayer.HeadRotation, Vector3.forward);
            }
            else if (_sphere != null)
            {
                _sphere.SetActive(false);
            }

            Fill(info, activity);
            SetMessage(null);
            _tip = Random.Range(0, Tips.Length);
            _nextTip = Time.unscaledTime + Random.Range(TipMinSeconds, TipMaxSeconds);
            card.Tip.text = Tips[_tip];
            _dots = 0;
            _nextDot = Time.unscaledTime + DotSeconds;
            Apply();
            Place(true);
            Lift();
        }

        private void Hide()
        {
            _showing = false;
            _black = 0f;
            _card = 0f;
            if (_screenCard != null)
            {
                _screenCard.Canvas.gameObject.SetActive(false);
            }

            if (_worldCard != null)
            {
                _worldCard.Canvas.gameObject.SetActive(false);
            }

            if (_sphere != null)
            {
                _sphere.SetActive(false);
            }
        }

        private void Fill(RrRoomInfo info, RrActivity activity)
        {
            var c = _active;
            c.GoingTo.text = "GOING TO";
            c.Room.text = CaretName(info, activity);
            c.Sub.text = info != null && !string.IsNullOrEmpty(info.Subtitle) ? info.Subtitle : (info != null ? info.Title : "");
            _coverSpec = CoverFor(info, activity);
            _cover = null;
            string spec = _coverSpec;
            RrCoverArt.Load(spec, art =>
            {
                if (spec == _coverSpec)
                {
                    _cover = art;
                    Refresh();
                }
            });
            Refresh();
        }

        /// <summary>Rec Room wrote room names as ^Name: the title's words run together ("^RecCenter", "^DormRoom").</summary>
        public static string CaretName(RrRoomInfo info, RrActivity activity)
        {
            string title = info != null && !string.IsNullOrEmpty(info.Title) ? info.Title : (activity == RrActivity.Dorm ? "Dorm Room" : activity.ToString());
            var sb = new System.Text.StringBuilder("^");
            bool upper = true;
            foreach (char ch in title)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(upper ? char.ToUpperInvariant(ch) : ch);
                    upper = false;
                }
                else
                {
                    upper = true;
                }
            }

            return sb.ToString();
        }

        /// <summary>The info's cover, else Rec Room's own menu illustrations for the two social places.</summary>
        private static string CoverFor(RrRoomInfo info, RrActivity activity)
        {
            if (info != null && !string.IsNullOrEmpty(info.CoverSprite))
            {
                return info.CoverSprite;
            }

            if (activity == RrActivity.RecCenter)
            {
                return "illustration_HomeButton_RecCenter";
            }

            if (activity == RrActivity.Dorm)
            {
                return "illustration_HomeButton_DormRoom";
            }

            return null;
        }

        /// <summary>Puts the loaded pictures in place (cover, logo, pattern).</summary>
        private void Refresh()
        {
            foreach (var c in new[] { _screenCard, _worldCard })
            {
                if (c == null)
                {
                    continue;
                }

                c.Pattern.Set(_pattern, new Vector2(FrameW, FrameH), true);
                if (_pattern != null)
                {
                    c.Pattern.Raw.color = new Color(1f, 1f, 1f, 0.55f);
                    c.Pattern.Img.color = new Color(1f, 1f, 1f, 0.55f);
                }

                bool hasCover = _cover != null && c == _active;
                var coverSize = c.Cover.Set(hasCover ? _cover : null, new Vector2(1000f, 520f), false);
                c.CoverFrame.gameObject.SetActive(hasCover);
                if (hasCover)
                {
                    c.CoverFrame.sizeDelta = coverSize + new Vector2(28f, 28f);
                    c.CoverShadow.sizeDelta = c.CoverFrame.sizeDelta;
                }

                c.BigLogo.Set(!hasCover ? _logo : null, new Vector2(620f, 460f), false);
                c.SmallLogo.Set(hasCover ? _logo : null, new Vector2(230f, 170f), false);
            }

            Lift();
        }

        // ---- animation ----

        private IEnumerator Animate(bool black, float target, float seconds)
        {
            float from = black ? _black : _card;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float v = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds)));
                if (black)
                {
                    _black = v;
                }
                else
                {
                    _card = v;
                }

                Apply();
                yield return null;
            }

            if (black)
            {
                _black = target;
            }
            else
            {
                _card = target;
            }

            Apply();
        }

        private void Apply()
        {
            if (_active == null)
            {
                return;
            }

            if (_active.Black != null)
            {
                _active.Black.color = new Color(0f, 0f, 0f, _black);
            }

            if (_sphereMaterial != null)
            {
                _sphereMaterial.color = new Color(0f, 0f, 0f, _black);
            }

            _active.Group.alpha = _card;
        }

        private void Update()
        {
            if (!_showing || _active == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now >= _nextDot)
            {
                _nextDot = now + DotSeconds;
                _dots = (_dots + 1) % 4;
                _active.Loading.text = Dots[_dots];
            }

            if (now >= _nextTip)
            {
                _nextTip = now + Random.Range(TipMinSeconds, TipMaxSeconds);
                _tip = (_tip + 1) % Tips.Length;
                _active.Tip.text = Tips[_tip];
                Lift();
            }

            var source = ProgressSource;
            if (source != null)
            {
                try
                {
                    SetProgress(source());
                }
                catch (System.Exception e)
                {
                    ProgressSource = null;
                    RrLog.WarnOnce("Loading", "progress-source", "the progress source threw " + e.GetType().Name + ": " + e.Message);
                }
            }

            _shownProgress = Mathf.MoveTowards(_shownProgress, _progress, Time.unscaledDeltaTime * 1.5f);
            _active.Fill.anchorMax = new Vector2(_shownProgress, 1f);
            int percent = Mathf.RoundToInt(_shownProgress * 100f);
            if (percent != _shownPercent)
            {
                _shownPercent = percent;
                _active.Percent.text = percent + "%";
            }
        }

        private static readonly string[] Dots = { "LOADING", "LOADING.", "LOADING..", "LOADING..." };

        private void LateUpdate()
        {
            Place(false);
        }

        private void OnBeforeRender()
        {
            Place(false);
        }

        /// <summary>VR: the sphere on the head, the panel ahead of it (yaw-locked, turning when looked away from).</summary>
        private void Place(bool snap)
        {
            if (!_showing || _active == null || !_active.World)
            {
                return;
            }

            Vector3 head = RrPlayer.HeadPosition;
            Vector3 look = FlatForward(RrPlayer.HeadRotation, _panelDir);
            if (snap)
            {
                _panelDir = look;
                _turning = false;
            }
            else
            {
                float angle = Vector3.Angle(look, _panelDir);
                if (angle > 40f)
                {
                    _turning = true;
                }

                if (_turning)
                {
                    _panelDir = Vector3.Slerp(_panelDir, look, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime)).normalized;
                    if (angle < 3f)
                    {
                        _turning = false;
                    }
                }
            }

            if (_sphere != null)
            {
                _sphere.transform.position = head;
            }

            var t = _active.Canvas.transform;
            t.position = head + _panelDir * PanelDistance;
            t.rotation = Quaternion.LookRotation(_panelDir, Vector3.up);
        }

        private static Vector3 FlatForward(Quaternion rotation, Vector3 fallback)
        {
            Vector3 f = rotation * Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f)
            {
                return fallback.sqrMagnitude > 1e-4f ? fallback.normalized : Vector3.forward;
            }

            return f.normalized;
        }

        // ---- VR: depth-free materials ----

        /// <summary>
        /// World card only: every graphic draws over everything (a copy made once per graphic). TMP labels are
        /// handled through their shared material: reading a TMP label's .material makes a new instance each time.
        /// The labels are rebuilt first, so glyphs that went to a fallback font's sub-mesh are lifted too.
        /// </summary>
        private void Lift()
        {
            if (_worldCard == null || _active != _worldCard)
            {
                return;
            }

            var root = _worldCard.Canvas;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
            {
                t.ForceMeshUpdate();
            }

            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null)
                {
                    continue;
                }

                var text = g as TMP_Text;
                var sub = g as TMP_SubMeshUI;
                Material current;
                Material source;
                if (text != null)
                {
                    current = text.fontSharedMaterial;
                    source = current;
                }
                else if (sub != null)
                {
                    current = sub.sharedMaterial;
                    source = current;
                }
                else
                {
                    current = g.material;
                    source = g.materialForRendering != null ? g.materialForRendering : current;
                }

                if (source == null || (current != null && _liftedSet.Contains(current)))
                {
                    continue;
                }

                var over = new Material(source) { name = source.name + " (over)" };
                over.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                over.renderQueue = PanelQueue;
                _lifted.Add(over);
                _liftedSet.Add(over);
                if (text != null)
                {
                    text.fontSharedMaterial = over;
                }
                else if (sub != null)
                {
                    sub.sharedMaterial = over;
                }
                else
                {
                    g.material = over;
                }
            }
        }

        private void EnsureSphere()
        {
            if (_sphere != null)
            {
                return;
            }

            _sphere = new GameObject("Rr Fade Sphere");
            _sphere.transform.SetParent(transform, false);
            _sphere.transform.localScale = Vector3.one * SphereRadius;
            _sphereMesh = MakeSphere(24, 16);
            _sphere.AddComponent<MeshFilter>().sharedMesh = _sphereMesh;
            var r = _sphere.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // UI/Default: transparent, both faces, depth test from unity_GUIZTestMode, stereo-instanced.
            _sphereMaterial = new Material(Canvas.GetDefaultCanvasMaterial()) { name = "Rr Fade Sphere" };
            _sphereMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            _sphereMaterial.renderQueue = SphereQueue;
            _sphereMaterial.color = new Color(0f, 0f, 0f, 0f);
            r.sharedMaterial = _sphereMaterial;
            _sphere.SetActive(false);
        }

        /// <summary>A unit-radius UV sphere with white vertex colours (UI/Default multiplies by them).</summary>
        private static Mesh MakeSphere(int lon, int lat)
        {
            var verts = new List<Vector3>();
            var colors = new List<Color32>();
            var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float a = Mathf.PI * y / lat;
                for (int x = 0; x <= lon; x++)
                {
                    float b = 2f * Mathf.PI * x / lon;
                    verts.Add(new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b)));
                    colors.Add(new Color32(255, 255, 255, 255));
                }
            }

            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int i = y * (lon + 1) + x;
                    tris.Add(i);
                    tris.Add(i + lon + 1);
                    tris.Add(i + 1);
                    tris.Add(i + 1);
                    tris.Add(i + lon + 1);
                    tris.Add(i + lon + 2);
                }
            }

            var mesh = new Mesh { name = "Rr Fade Sphere" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        // ---- building the card ----

        /// <summary>One card: the screen overlay (desktop) or the world panel (VR). Same layout, 1920 x 1080.</summary>
        private sealed class Card
        {
            public bool World;
            public Canvas Canvas;
            public CanvasGroup Group;
            public Image Black;
            public Pic Pattern;
            public RectTransform CoverFrame;
            public RectTransform CoverShadow;
            public Pic Cover;
            public Pic BigLogo;
            public Pic SmallLogo;
            public TextMeshProUGUI GoingTo;
            public TextMeshProUGUI Room;
            public TextMeshProUGUI Sub;
            public TextMeshProUGUI Loading;
            public TextMeshProUGUI Percent;
            public TextMeshProUGUI Tip;
            public TextMeshProUGUI Message;
            public RectTransform MessagePill;
            public RectTransform Fill;
        }

        /// <summary>A picture slot: an Image for sprites (UGUI handles atlas packing), a RawImage for textures.</summary>
        private sealed class Pic
        {
            public RectTransform Rect;
            public Image Img;
            public RawImage Raw;

            /// <summary>Shows the art fitted into box (or stretched to fill it), returns the size used.</summary>
            public Vector2 Set(RrArt art, Vector2 box, bool fill)
            {
                if (art == null)
                {
                    Rect.gameObject.SetActive(false);
                    return Vector2.zero;
                }

                Rect.gameObject.SetActive(true);
                Vector2 size = box;
                if (!fill)
                {
                    float aspect = art.Aspect > 0.01f ? art.Aspect : 1f;
                    size = box.x / box.y > aspect ? new Vector2(box.y * aspect, box.y) : new Vector2(box.x, box.x / aspect);
                }

                if (Rect.anchorMin == Rect.anchorMax)
                {
                    Rect.sizeDelta = size;
                }

                bool sprite = art.Sprite != null && !fill;
                Img.enabled = sprite;
                Raw.enabled = !sprite;
                if (sprite)
                {
                    Img.sprite = art.Sprite;
                }
                else
                {
                    Raw.texture = art.Texture;
                    Raw.uvRect = art.Uv;
                }

                return size;
            }
        }

        private Card Build(bool world)
        {
            var c = new Card { World = world };
            var go = new GameObject(world ? "Rr Loading Card (VR)" : "Rr Loading Card", typeof(RectTransform));
            go.layer = RrLayers.Ui;
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var canvas = go.AddComponent<Canvas>();
            c.Canvas = canvas;
            var rect = (RectTransform)go.transform;
            if (world)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                rect.sizeDelta = new Vector2(FrameW, FrameH);
                rect.localScale = Vector3.one * (PanelWidth / FrameW);
                canvas.sortingOrder = SortingOrder;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SortingOrder;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(FrameW, FrameH);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                var black = Node(rect, "Black");
                Stretch(black);
                c.Black = black.gameObject.AddComponent<Image>();
                c.Black.color = new Color(0f, 0f, 0f, 0f);
                c.Black.raycastTarget = false;
            }

            var cardRoot = Node(rect, "Card");
            Stretch(cardRoot);
            c.Group = cardRoot.gameObject.AddComponent<CanvasGroup>();
            c.Group.alpha = 0f;
            c.Group.interactable = false;
            c.Group.blocksRaycasts = false;

            // Backdrop: the warm off-white of Rec Room's loading panels, its beige logo pattern over it.
            var fill = Node(cardRoot, "Backdrop");
            Stretch(fill);
            Solid(fill, OffWhite);
            var patternRect = Node(cardRoot, "Pattern");
            Stretch(patternRect);
            c.Pattern = MakePic(patternRect, true);

            // The frame the layout is drawn in (centred, 1920 x 1080), so other screen shapes only widen the backdrop.
            var frame = Node(cardRoot, "Frame");
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            // Cover art in a white mat with a soft shadow, centred above the bar.
            c.CoverShadow = At(frame, "Cover Shadow", 972f, 648f, 100f, 100f, new Vector2(0.5f, 0.5f));
            Solid(c.CoverShadow, new Color(0f, 0f, 0f, 0.22f));
            c.CoverFrame = At(frame, "Cover Frame", 960f, 660f, 100f, 100f, new Vector2(0.5f, 0.5f));
            Solid(c.CoverFrame, Color.white);
            var coverRect = At(c.CoverFrame, "Cover", 0f, 0f, 100f, 100f, new Vector2(0.5f, 0.5f));
            coverRect.anchorMin = coverRect.anchorMax = new Vector2(0.5f, 0.5f);
            coverRect.anchoredPosition = Vector2.zero;
            c.Cover = MakePic(coverRect, false);
            c.CoverFrame.gameObject.SetActive(false);

            c.BigLogo = MakePic(At(frame, "Logo", 960f, 660f, 620f, 460f, new Vector2(0.5f, 0.5f)), false);
            c.SmallLogo = MakePic(At(frame, "Logo (corner)", 60f, FrameH - 40f, 230f, 170f, new Vector2(0f, 1f)), false);

            // The bottom row: "GOING TO", the room, the sub-room; loading, bar and tip on the right.
            var bar = At(frame, "Bottom Row", 0f, 0f, FrameW, 300f, Vector2.zero);
            Solid(bar, BarColor);
            var stripe = At(bar, "Accent", 0f, 300f, FrameW, 8f, new Vector2(0f, 1f));
            Solid(stripe, Orange);

            c.GoingTo = Label(bar, "Going To", 110f, 205f, 900f, 50f, 34f, Orange, TextAlignmentOptions.BottomLeft, true);
            c.Room = Label(bar, "Room Name", 106f, 108f, 1040f, 96f, 80f, Color.white, TextAlignmentOptions.BottomLeft, true);
            c.Sub = Label(bar, "Sub Room", 110f, 52f, 1040f, 52f, 38f, OffWhite, TextAlignmentOptions.BottomLeft, false);

            c.Loading = Label(bar, "Loading", 1250f, 205f, 400f, 50f, 34f, Color.white, TextAlignmentOptions.BottomLeft, true);
            c.Loading.text = "LOADING";
            c.Percent = Label(bar, "Percent", 1650f, 205f, 160f, 50f, 34f, Color.white, TextAlignmentOptions.BottomRight, true);
            var track = At(bar, "Progress", 1250f, 176f, 560f, 16f, Vector2.zero);
            Solid(track, new Color(1f, 1f, 1f, 0.18f));
            var fillBar = Node(track, "Fill");
            fillBar.anchorMin = Vector2.zero;
            fillBar.anchorMax = new Vector2(0f, 1f);
            fillBar.offsetMin = Vector2.zero;
            fillBar.offsetMax = Vector2.zero;
            Solid(fillBar, Orange);
            c.Fill = fillBar;
            c.Tip = Label(bar, "Tip", 1250f, 40f, 560f, 120f, 28f, OffWhite, TextAlignmentOptions.TopLeft, false);
            c.Tip.textWrappingMode = TextWrappingModes.Normal;

            // A message line above the bar (failed loads, redirects).
            var pill = At(frame, "Message", 960f, 340f, 1200f, 64f, new Vector2(0.5f, 0.5f));
            Solid(pill, new Color(0.13f, 0.12f, 0.16f, 0.85f));
            c.Message = Label(pill, "Text", 0f, 0f, 1200f, 64f, 32f, Orange, TextAlignmentOptions.Center, true);
            c.Message.rectTransform.anchorMin = Vector2.zero;
            c.Message.rectTransform.anchorMax = Vector2.one;
            c.Message.rectTransform.offsetMin = Vector2.zero;
            c.Message.rectTransform.offsetMax = Vector2.zero;
            pill.gameObject.SetActive(false);
            c.MessagePill = pill;

            RrLayers.Set(go, RrLayers.Ui);
            RrLog.Info("Loading", "card built (" + (world ? "VR panel " + PanelWidth + " m at " + PanelDistance + " m, fade sphere" : "screen overlay, sorting " + SortingOrder) + ")");
            return c;
        }

        private static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = RrLayers.Ui;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            return r;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        /// <summary>A rectangle at frame pixels (x, y from the parent's bottom-left) with the given pivot.</summary>
        private static RectTransform At(Transform parent, string name, float x, float y, float w, float h, Vector2 pivot)
        {
            var r = Node(parent, name);
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = pivot;
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        private static Image Solid(RectTransform r, Color c)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private static Pic MakePic(RectTransform r, bool fill)
        {
            var p = new Pic { Rect = r };
            var a = Node(r, "Sprite");
            Stretch(a);
            p.Img = a.gameObject.AddComponent<Image>();
            p.Img.raycastTarget = false;
            p.Img.preserveAspect = !fill;
            var b = Node(r, "Texture");
            Stretch(b);
            p.Raw = b.gameObject.AddComponent<RawImage>();
            p.Raw.raycastTarget = false;
            r.gameObject.SetActive(false);
            return p;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align, bool title)
        {
            var r = At(parent, name, x, y, w, h, Vector2.zero);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            RrText.Style(t, title);
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.text = "";
            return t;
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";
        }
    }
}
