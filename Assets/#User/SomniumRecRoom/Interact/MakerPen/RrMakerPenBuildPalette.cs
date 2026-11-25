using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// Pictures for the build palette from the player's install: Rec Room's Maker Pen menu icons (Sprites
    /// <c>MakerPen_Sprites_*</c>, cut from their atlas through the material's tiling) and the palette thumbnails
    /// of props (Texture2D <c>SpawnableToolTex_*</c>). Each gets one unlit alpha-clipped material, made at once and
    /// filled when the picture has loaded (<see cref="Art.Ready"/>); a missing picture leaves the key's label.
    /// </summary>
    public static class RrMakerPenArt
    {
        public sealed class Art
        {
            public Material Material;
            public bool Ready;
            public bool Failed;
        }

        private static readonly Dictionary<string, Art> Cache = new Dictionary<string, Art>();
        private static readonly Color IconTint = new Color(0.16f, 0.17f, 0.2f);

        private static Material NewMaterial(string name, Color tint)
        {
            var m = new Material(RrLook.Unlit(Color.white)) { name = "Rr MakerPen " + name };
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.35f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = 2450;
            return m;
        }

        /// <summary>A prop thumbnail by texture name.</summary>
        public static Art Thumb(string texture)
        {
            Art a;
            if (string.IsNullOrEmpty(texture))
            {
                return null;
            }

            if (Cache.TryGetValue(texture, out a))
            {
                return a;
            }

            a = new Art { Material = NewMaterial(texture, Color.white) };
            Cache[texture] = a;
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                a.Failed = true;
                return a;
            }

            assets.Load<Texture2D>(texture, 28, t =>
            {
                if (t == null || a.Material == null)
                {
                    a.Failed = true;
                    return;
                }

                a.Material.SetTexture("_BaseMap", t);
                a.Material.SetTexture("_MainTex", t);
                a.Ready = true;
            });
            return a;
        }

        /// <summary>A Maker Pen menu icon by sprite name (without the MakerPen_Sprites_ prefix).</summary>
        public static Art Icon(string sprite)
        {
            if (string.IsNullOrEmpty(sprite))
            {
                return null;
            }

            string name = RrMakerPenCatalog.IconPrefix + sprite;
            Art a;
            if (Cache.TryGetValue(name, out a))
            {
                return a;
            }

            a = new Art { Material = NewMaterial(name, IconTint) };
            Cache[name] = a;
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                a.Failed = true;
                return a;
            }

            assets.Load<Sprite>(name, 213, s =>
            {
                if (s == null || s.texture == null || a.Material == null)
                {
                    a.Failed = true;
                    return;
                }

                var tex = s.texture;
                Rect r;
                try
                {
                    r = s.textureRect;
                }
                catch (System.Exception)
                {
                    r = s.rect;
                }

                a.Material.SetTexture("_BaseMap", tex);
                a.Material.SetTexture("_MainTex", tex);
                var scale = new Vector2(r.width / tex.width, r.height / tex.height);
                var offset = new Vector2(r.x / tex.width, r.y / tex.height);
                a.Material.SetTextureScale("_BaseMap", scale);
                a.Material.SetTextureOffset("_BaseMap", offset);
                a.Material.SetTextureScale("_MainTex", scale);
                a.Material.SetTextureOffset("_MainTex", offset);
                a.Ready = true;
            });
            return a;
        }

        /// <summary>Destroys the materials (world shutdown); the pictures belong to RrAssets.</summary>
        public static void Shutdown()
        {
            foreach (var kv in Cache)
            {
                if (kv.Value.Material != null)
                {
                    Object.Destroy(kv.Value.Material);
                }

                kv.Value.Material = null;
            }

            Cache.Clear();
        }
    }

    /// <summary>
    /// The Maker Pen's palette in a maker room the local player may build in (in place of
    /// <see cref="RrMakerPenPalette"/>): a cream board in Rec Room's palette style with
    /// - a readout (mode and item, then what the pen points at) and the current item's picture,
    /// - the 8 mode keys with Rec Room's own pen icons (Draw, Shapes, Props, Select, Clone, Recolor, Delete, Physics),
    /// - the tool keys Snap, Undo, Redo, - and + (size) and Save,
    /// - the mode's page: colours (Draw, Recolor), shapes + colours, the props catalogue (8 category tabs and a 6 x 3
    ///   grid of install thumbnails with paging), or the physics settings,
    /// - a toast line (saved, undone, frozen...) and the controls.
    /// VR: above the other hand, facing the head, touched with the pen tip (as <see cref="RrMakerPenPalette"/>; no
    /// colliders, so the empty hand under it presses nothing). Screen mode: only the readout, mode and tool rows,
    /// toast and key hints, small in the lower left of the view (world-space, placed in front of the camera every
    /// frame, never parented to it); the keys are on the keyboard.
    /// </summary>
    public sealed class RrMakerPenBuildPalette : MonoBehaviour
    {
        public const float Width = 0.33f;
        public const float Height = 0.44f;
        public const float TouchSlack = 0.012f;
        public const float HudScale = 0.65f;
        public static readonly Vector3 HudOffset = new Vector3(-0.33f, -0.16f, 0.45f);
        public const int PropColumns = 6;
        public const int PropRows = 3;
        public const int PropsPerPage = PropColumns * PropRows;

        private const float KeyZ = -0.006f;
        private const float HudTop = 0.23f;
        private const float HudBottom = 0.015f;

        private enum K : byte
        {
            Mode,
            Snap,
            Undo,
            Redo,
            Minus,
            Plus,
            Save,
            Colour,
            Shape,
            Tab,
            Prop,
            PrevPage,
            NextPage,
            Brush
        }

        private sealed class Key
        {
            public K Kind;
            public int Arg;
            public Transform T;
            public Renderer Tile;
            public Renderer Picture;
            public RrMakerPenArt.Art Art;
            public TextMeshPro Label;
            public float Radius;
            public Vector3 Rest;
            public Vector3 Scale;
            public bool On;
        }

        public RrMakerPenBuild Build;

        private readonly List<Key> _keys = new List<Key>();
        private readonly List<Key> _propKeys = new List<Key>();
        private Transform _board;
        private GameObject _colours, _shapes, _props, _brushes;
        private TextMeshPro _readout, _info, _toast, _hints, _context, _pageLabel;
        private Renderer _current;
        private Renderer _currentColour;
        private RrMakerPenArt.Art _currentArt;
        private bool _screen;
        private bool _laidOutScreen = true;
        private RrHand _penHand;
        private int _touching = -1;
        private float _pressedAt = -10f;
        private int _page;
        private int _shownCategory = -1, _shownProp = -1, _shownPage = -1;
        private int _shownMode = -1;
        private string _shownReadout, _shownInfo, _shownToast, _shownHints;
        private int _shownColour = -1;

        public int Presses { get; private set; }

        public static RrMakerPenBuildPalette Create(Transform parent, RrMakerPenBuild build)
        {
            var go = new GameObject("Maker Pen Build Palette");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<RrMakerPenBuildPalette>();
            p.Build = build;
            p.Construct();
            go.SetActive(false);
            return p;
        }

        // ------------------------------------------------------------------ building the board

        private void Construct()
        {
            var t = transform;
            _board = RrBuild.Box(t, "Board", new Vector3(0f, 0f, 0.004f), new Vector3(Width, Height, 0.006f), RrLook.Solid(RrLook.Cream), false).transform;

            _readout = Text(t, "Readout", new Vector3(-0.02f, 0.205f, -0.002f), 0.27f, 0.02f, 0.09f, true);
            _info = Text(t, "Info", new Vector3(-0.02f, 0.186f, -0.002f), 0.27f, 0.016f, 0.065f, false);
            _current = Quad(t, "Current", new Vector3(0.138f, 0.196f, -0.004f), 0.034f);
            _currentColour = RrBuild.Sphere(t, "Current Colour", new Vector3(0.108f, 0.196f, -0.006f), 0.008f, RrLook.Solid(Color.white), false).GetComponent<Renderer>();

            // the 8 modes
            for (int i = 0; i < 8; i++)
            {
                var pos = new Vector3(-0.133f + i * 0.038f, 0.15f, KeyZ);
                var k = Tile(t, K.Mode, i, pos, new Vector2(0.034f, 0.034f), RrMakerPenCatalog.ModeColours[i], null);
                k.Art = RrMakerPenArt.Icon(RrMakerPenCatalog.ModeIcons[i]);
                k.Picture = Quad(k.T.parent, "Icon " + i, pos + new Vector3(0f, 0f, -0.0045f), 0.024f);
                k.Label = Text(t, "Mode Label " + i, new Vector3(pos.x, 0.124f, -0.002f), 0.038f, 0.012f, 0.045f, false);
                k.Label.text = RrMakerPenCatalog.ModeNames[i];
            }

            // tools
            string[] tools = { "Snap", "Undo", "Redo", "-", "+", "Save" };
            K[] kinds = { K.Snap, K.Undo, K.Redo, K.Minus, K.Plus, K.Save };
            string[] icons = { "Snap", "Undo", "Redo", null, null, null };
            for (int i = 0; i < tools.Length; i++)
            {
                var pos = new Vector3(-0.13f + i * 0.052f, 0.095f, KeyZ);
                var colour = kinds[i] == K.Save ? new Color(0.55f, 0.85f, 0.55f) : new Color(0.86f, 0.88f, 0.92f);
                var k = Tile(t, kinds[i], 0, pos, new Vector2(0.048f, 0.026f), colour, icons[i] == null ? tools[i] : null);
                if (icons[i] != null)
                {
                    k.Art = RrMakerPenArt.Icon(icons[i]);
                    k.Picture = Quad(t, "Tool Icon " + i, pos + new Vector3(-0.012f, 0f, -0.0045f), 0.02f);
                    var label = Text(t, "Tool Label " + i, pos + new Vector3(0.008f, 0f, -0.0045f), 0.03f, 0.02f, 0.06f, false);
                    label.text = tools[i];
                }
            }

            // context pages
            _context = Text(t, "Hint", new Vector3(0f, 0.045f, -0.002f), 0.3f, 0.05f, 0.07f, false);
            _colours = Group(t, "Colours");
            var palette = RrMakerPenWire.Palette;
            for (int i = 0; i < palette.Length; i++)
            {
                var pos = new Vector3(-0.126f + (i % 8) * 0.036f, -0.025f - (i / 8) * 0.036f, KeyZ);
                var s = RrBuild.Sphere(_colours.transform, "Swatch " + i, pos, 0.012f, RrLook.Solid(palette[i]), false);
                _keys.Add(new Key { Kind = K.Colour, Arg = i, T = s.transform, Tile = s.GetComponent<Renderer>(), Radius = 0.012f, Rest = pos, Scale = s.transform.localScale });
            }

            _shapes = Group(t, "Shapes");
            for (int i = 0; i < RrMakerPenCatalog.Shapes.Length; i++)
            {
                var e = RrMakerPenCatalog.Shapes[i];
                var pos = new Vector3(-0.125f + (i % 6) * 0.05f, 0.05f - (i / 6) * 0.042f, KeyZ);
                var k = Tile(_shapes.transform, K.Shape, i, pos, new Vector2(0.046f, 0.036f), new Color(0.80f, 0.90f, 0.98f), null);
                if (e.Icon != null)
                {
                    k.Art = RrMakerPenArt.Icon(e.Icon);
                    k.Picture = Quad(_shapes.transform, "Shape Icon " + i, pos + new Vector3(0f, 0.004f, -0.0045f), 0.022f);
                }

                k.Label = Text(_shapes.transform, "Shape Label " + i, pos + new Vector3(0f, e.Icon != null ? -0.012f : 0f, -0.0045f), 0.044f, 0.012f, e.Icon != null ? 0.04f : 0.055f, false);
                k.Label.text = e.Label;
            }

            _props = Group(t, "Props");
            var cats = RrMakerPenCatalog.Categories;
            for (int i = 0; i < cats.Count && i < 8; i++)
            {
                var pos = new Vector3(-0.117f + (i % 4) * 0.078f, 0.062f - (i / 4) * 0.024f, KeyZ);
                var k = Tile(_props.transform, K.Tab, i, pos, new Vector2(0.074f, 0.021f), new Color(0.93f, 0.86f, 0.70f), cats[i].Name);
                k.Label.fontSizeMax = 0.06f;
            }

            for (int i = 0; i < PropsPerPage; i++)
            {
                var pos = new Vector3(-0.125f + (i % PropColumns) * 0.05f, -0.002f - (i / PropColumns) * 0.05f, KeyZ);
                var k = Tile(_props.transform, K.Prop, i, pos, new Vector2(0.047f, 0.047f), new Color(0.97f, 0.97f, 0.95f), null);
                k.Picture = Quad(_props.transform, "Prop Picture " + i, pos + new Vector3(0f, 0.004f, -0.0045f), 0.036f);
                k.Label = Text(_props.transform, "Prop Label " + i, pos + new Vector3(0f, -0.0185f, -0.0045f), 0.046f, 0.009f, 0.035f, false);
                _propKeys.Add(k);
            }

            Tile(_props.transform, K.PrevPage, 0, new Vector3(-0.1f, -0.15f, KeyZ), new Vector2(0.05f, 0.02f), new Color(0.86f, 0.88f, 0.92f), "<");
            Tile(_props.transform, K.NextPage, 0, new Vector3(0.1f, -0.15f, KeyZ), new Vector2(0.05f, 0.02f), new Color(0.86f, 0.88f, 0.92f), ">");
            _pageLabel = Text(_props.transform, "Page", new Vector3(0f, -0.15f, -0.002f), 0.12f, 0.02f, 0.06f, false);

            _brushes = Group(t, "Physics");
            for (int i = 0; i < RrMakerPenCatalog.BrushNames.Length; i++)
            {
                var pos = new Vector3(-0.103f + (i % 3) * 0.103f, 0.05f - (i / 3) * 0.038f, KeyZ);
                Color c = i < 4 ? new Color(1f, 0.92f, 0.62f) : new Color(0.75f, 0.85f, 1f);
                Tile(_brushes.transform, K.Brush, i, pos, new Vector2(0.098f, 0.032f), c, RrMakerPenCatalog.BrushNames[i]);
            }

            _toast = Text(t, "Toast", new Vector3(0f, -0.19f, -0.002f), 0.31f, 0.016f, 0.07f, true);
            _hints = Text(t, "Hints", new Vector3(0f, -0.207f, -0.002f), 0.31f, 0.014f, 0.05f, false);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private static GameObject Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private TextMeshPro Text(Transform parent, string name, Vector3 pos, float width, float height, float size, bool title)
        {
            var tmp = RrBuild.Sign(parent, name, "", pos, Quaternion.identity, size, RrLook.DarkGrey, title, width);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = size * 0.4f;
            tmp.fontSizeMax = size;
            tmp.rectTransform.sizeDelta = new Vector2(width, height);
            tmp.overflowMode = TextOverflowModes.Truncate;
            return tmp;
        }

        private static Renderer Quad(Transform parent, string name, Vector3 pos, float size)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            var c = q.GetComponent<Collider>();
            if (c != null)
            {
                Object.Destroy(c);
            }

            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = Quaternion.identity;
            q.transform.localScale = new Vector3(size, size, 1f);
            var r = q.GetComponent<Renderer>();
            r.enabled = false;
            return r;
        }

        private Key Tile(Transform parent, K kind, int arg, Vector3 pos, Vector2 size, Color colour, string label)
        {
            var box = RrBuild.Box(parent, kind + " " + arg, pos, new Vector3(size.x, size.y, 0.008f), RrLook.Solid(colour), false);
            var k = new Key
            {
                Kind = kind,
                Arg = arg,
                T = box.transform,
                Tile = box.GetComponent<Renderer>(),
                Radius = Mathf.Min(size.x, size.y) * 0.5f + 0.004f,
                Rest = pos,
                Scale = box.transform.localScale
            };
            if (label != null)
            {
                k.Label = Text(parent, kind + " " + arg + " Label", pos + new Vector3(0f, 0f, -0.0045f), size.x - 0.004f, size.y - 0.004f, 0.06f, false);
                k.Label.text = label;
            }

            _keys.Add(k);
            return k;
        }

        // ------------------------------------------------------------------ showing

        public void Show(bool screen, RrHand penHand)
        {
            _screen = screen;
            _penHand = penHand;
            bool visible = !(screen && RrInput.Blocked);
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
                _touching = -1;
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            _touching = -1;
        }

        private void LateUpdate()
        {
            if (Build == null)
            {
                return;
            }

            if (_screen)
            {
                var cam = RrPlayer.Camera;
                if (cam == null)
                {
                    return;
                }

                var ct = cam.transform;
                float mid = (HudTop + HudBottom) * 0.5f * HudScale;
                transform.SetPositionAndRotation(ct.position + ct.rotation * HudOffset - ct.rotation * (Vector3.up * mid), ct.rotation);
                transform.localScale = Vector3.one * HudScale;
            }
            else
            {
                var off = _penHand != null && _penHand.Side == RrHandSide.Left ? RrHand.Right : RrHand.Left;
                Vector3 head = RrPlayer.HeadPosition;
                Vector3 anchor = off != null && off.Tracked
                    ? off.Position + Vector3.up * (0.06f + Height * 0.5f)
                    : head + RrPlayer.HeadRotation * new Vector3(-0.2f, -0.15f, 0.42f);
                Vector3 look = anchor - head;
                transform.position = anchor;
                transform.rotation = look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look.normalized, Vector3.up) : Quaternion.identity;
                transform.localScale = Vector3.one;
            }

            Layout();
            Refresh();
        }

        /// <summary>Which page shows (VR) and the board size: the whole palette in VR, its top strip on a screen.</summary>
        private void Layout()
        {
            var mode = Build.Mode;
            int m = _screen ? -2 : (int)mode;
            if (m == _shownMode && _laidOutScreen == _screen)
            {
                return;
            }

            _shownMode = m;
            _laidOutScreen = _screen;
            bool vr = !_screen;
            bool colours = vr && (mode == RrMakerMode.Draw || mode == RrMakerMode.Shapes || mode == RrMakerMode.Recolor);
            _colours.SetActive(colours);
            _shapes.SetActive(vr && mode == RrMakerMode.Shapes);
            _props.SetActive(vr && mode == RrMakerMode.Props);
            _brushes.SetActive(vr && mode == RrMakerMode.Physics);
            string hint = "";
            switch (mode)
            {
                case RrMakerMode.Draw:
                    hint = "Pull the trigger to draw. Pull harder for a thicker tube.";
                    break;
                case RrMakerMode.Select:
                    hint = "Touch or point at something and hold the trigger to move it.\nThumbstick up/down or - + to resize.";
                    break;
                case RrMakerMode.Clone:
                    hint = "Touch or point at something and hold the trigger to pull out a copy.";
                    break;
                case RrMakerMode.Recolor:
                    hint = "Pick a colour, then touch or point at something and pull the trigger.";
                    break;
                case RrMakerMode.Delete:
                    hint = "Touch or point at something and pull the trigger to delete it.";
                    break;
            }

            _context.gameObject.SetActive(vr && hint.Length > 0);
            _context.text = hint;

            if (_screen)
            {
                _board.localPosition = new Vector3(0f, (HudTop + HudBottom) * 0.5f, 0.004f);
                _board.localScale = new Vector3(Width, HudTop - HudBottom, 0.006f);
                _toast.transform.localPosition = new Vector3(0f, 0.066f, -0.002f);
                _hints.transform.localPosition = new Vector3(0f, 0.036f, -0.002f);
                _hints.rectTransform.sizeDelta = new Vector2(Width - 0.02f, 0.034f);
                _hints.fontSizeMax = 0.065f;
            }
            else
            {
                _board.localPosition = new Vector3(0f, 0f, 0.004f);
                _board.localScale = new Vector3(Width, Height, 0.006f);
                _toast.transform.localPosition = new Vector3(0f, -0.19f, -0.002f);
                _hints.transform.localPosition = new Vector3(0f, -0.207f, -0.002f);
                _hints.rectTransform.sizeDelta = new Vector2(Width - 0.02f, 0.014f);
                _hints.fontSizeMax = 0.05f;
            }
        }

        private void Refresh()
        {
            var b = Build;
            SetText(_readout, b.Readout(), ref _shownReadout);
            SetText(_info, b.Info(), ref _shownInfo);
            bool toast = Time.unscaledTime < b.ToastUntil && b.ToastText.Length > 0;
            string toastText = toast ? b.ToastText : "";
            if (toastText != _shownToast)
            {
                _toast.color = b.ToastBad ? new Color(0.78f, 0.16f, 0.14f) : new Color(0.12f, 0.45f, 0.18f);
            }

            SetText(_toast, toastText, ref _shownToast);
            string hints;
            if (_screen)
            {
                hints = b.Dragging
                    ? "[ ] turn   , . tilt   wheel / - + size   Shift+wheel distance   RMB cancel"
                    : "1-8 mode   LMB use   Z undo   Y redo   C colour   wheel item   T category   G snap   F5 save";
            }
            else
            {
                hints = b.Dragging ? "Thumbstick up/down: size   Let go: place" : "Touch a key with the pen tip   Trigger: use   A/X: undo";
            }

            SetText(_hints, hints, ref _shownHints);

            if (_shownColour != b.Colour)
            {
                _shownColour = b.Colour;
                _currentColour.sharedMaterial = RrLook.Solid(RrMakerPenWire.ColourOf(b.Colour));
            }

            _currentColour.enabled = b.Mode == RrMakerMode.Draw || b.Mode == RrMakerMode.Shapes || b.Mode == RrMakerMode.Recolor;

            // the current item's picture
            RrMakerPenArt.Art art = null;
            if (b.Mode == RrMakerMode.Props)
            {
                var p = b.CurrentProp;
                art = p != null ? RrMakerPenArt.Thumb(p.ThumbTexture) : null;
            }
            else if (b.Mode == RrMakerMode.Shapes)
            {
                art = RrMakerPenArt.Icon(b.CurrentShape.Icon);
            }
            else
            {
                art = RrMakerPenArt.Icon(RrMakerPenCatalog.ModeIcons[(int)b.Mode]);
            }

            if (art != _currentArt || (art != null && art.Ready != _current.enabled))
            {
                _currentArt = art;
                bool show = art != null && art.Ready && art.Material != null;
                _current.enabled = show;
                if (show)
                {
                    _current.sharedMaterial = art.Material;
                }
            }

            RefreshProps();
            foreach (var k in _keys)
            {
                if (!k.T.gameObject.activeInHierarchy)
                {
                    continue;
                }

                bool on;
                switch (k.Kind)
                {
                    case K.Mode:
                        on = (int)b.Mode == k.Arg;
                        break;
                    case K.Snap:
                        on = b.Snap;
                        break;
                    case K.Colour:
                        on = b.Colour == k.Arg;
                        break;
                    case K.Shape:
                        on = b.Shape == k.Arg;
                        break;
                    case K.Tab:
                        on = b.Category == k.Arg;
                        break;
                    case K.Prop:
                        on = b.Category == _shownCategory && b.Prop == _page * PropsPerPage + k.Arg;
                        break;
                    case K.Brush:
                        on = (int)b.Brush == k.Arg;
                        break;
                    default:
                        on = false;
                        break;
                }

                if (on != k.On)
                {
                    k.On = on;
                    float lift = k.Kind == K.Colour ? -0.006f : -0.004f;
                    k.T.localScale = on ? Vector3.Scale(k.Scale, k.Kind == K.Colour ? Vector3.one * 1.4f : new Vector3(1.12f, 1.12f, 1f)) : k.Scale;
                    k.T.localPosition = on ? k.Rest + new Vector3(0f, 0f, lift) : k.Rest;
                }

                if (k.Picture != null && k.Art != null)
                {
                    bool show = k.Art.Ready && k.Art.Material != null;
                    if (k.Picture.enabled != show)
                    {
                        k.Picture.enabled = show;
                        if (show)
                        {
                            k.Picture.sharedMaterial = k.Art.Material;
                        }
                    }
                }
            }
        }

        private static void SetText(TextMeshPro t, string s, ref string shown)
        {
            s = s ?? "";
            if (s == shown)
            {
                return;
            }

            shown = s;
            t.text = s;
        }

        /// <summary>The props page: the category's items from the current page, with their install thumbnails.</summary>
        private void RefreshProps()
        {
            var b = Build;
            if (b.Category != _shownCategory || b.Prop != _shownProp)
            {
                _page = b.Prop / PropsPerPage;
                _shownCategory = b.Category;
                _shownProp = b.Prop;
            }

            var items = b.CurrentCategory.Items;
            int pages = Mathf.Max(1, (items.Count + PropsPerPage - 1) / PropsPerPage);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            int key = b.Category * 1000 + _page;
            if (key == _shownPage)
            {
                return;
            }

            _shownPage = key;
            _pageLabel.text = b.CurrentCategory.Name + "  " + (_page + 1) + " / " + pages;
            for (int i = 0; i < _propKeys.Count; i++)
            {
                var k = _propKeys[i];
                int index = _page * PropsPerPage + i;
                bool has = index < items.Count;
                k.T.gameObject.SetActive(has);
                k.Label.gameObject.SetActive(has);
                k.Picture.gameObject.SetActive(has);
                k.On = false;
                k.T.localScale = k.Scale;
                k.T.localPosition = k.Rest;
                if (!has)
                {
                    continue;
                }

                var e = items[index];
                k.Label.text = e.Label;
                k.Art = RrMakerPenArt.Thumb(e.ThumbTexture);
                k.Picture.enabled = false;
            }
        }

        // ------------------------------------------------------------------ touching (VR)

        /// <summary>
        /// VR: the pen tip at this world point; a key it enters is pressed (once per touch). Returns true while the
        /// tip is at the board, so the pen does not act on it.
        /// </summary>
        public bool Touch(Vector3 tip, RrHand hand)
        {
            if (_screen || !gameObject.activeInHierarchy)
            {
                return false;
            }

            var local = transform.InverseTransformPoint(tip);
            bool near = Mathf.Abs(local.x) < Width * 0.5f + 0.02f && Mathf.Abs(local.y) < Height * 0.5f + 0.02f && local.z > -0.05f && local.z < 0.03f;
            if (!near)
            {
                _touching = -1;
                return false;
            }

            int hit = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _keys.Count; i++)
            {
                var k = _keys[i];
                if (!k.T.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float d = (k.T.position - tip).magnitude - k.Radius;
                if (d < TouchSlack && d < best)
                {
                    best = d;
                    hit = i;
                }
            }

            if (hit != _touching)
            {
                _touching = hit;
                if (hit >= 0 && Time.unscaledTime - _pressedAt > 0.25f)
                {
                    _pressedAt = Time.unscaledTime;
                    Press(_keys[hit]);
                    if (hand != null)
                    {
                        hand.Haptic(0.3f, 0.04f);
                    }
                }
            }

            return true;
        }

        private void Press(Key k)
        {
            Presses++;
            RrSound.Play(RrPressable.DefaultSound, k.T.position, 0.3f);
            var b = Build;
            if (b == null)
            {
                return;
            }

            switch (k.Kind)
            {
                case K.Mode:
                    b.SetMode((RrMakerMode)k.Arg);
                    break;
                case K.Snap:
                    b.ToggleSnap();
                    break;
                case K.Undo:
                    b.UndoStep();
                    break;
                case K.Redo:
                    b.RedoStep();
                    break;
                case K.Minus:
                    b.ScaleStep(-1);
                    break;
                case K.Plus:
                    b.ScaleStep(1);
                    break;
                case K.Save:
                    b.Save();
                    break;
                case K.Colour:
                    b.PickColour((byte)k.Arg);
                    break;
                case K.Shape:
                    b.PickShape(k.Arg);
                    break;
                case K.Tab:
                    b.PickCategory(k.Arg);
                    _page = 0;
                    break;
                case K.Prop:
                    b.PickProp(_page * PropsPerPage + k.Arg);
                    break;
                case K.PrevPage:
                    _page--;
                    break;
                case K.NextPage:
                    _page++;
                    break;
                case K.Brush:
                    b.SetBrush((RrMakerPhysicsBrush)k.Arg);
                    break;
            }
        }
    }
}
