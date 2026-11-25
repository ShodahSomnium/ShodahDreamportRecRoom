using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// Building blocks for Rec Room's interface ("RRUI", the 2021-2025 watch) made at runtime with UnityEngine.UI
    /// and TextMeshPro, in Rec Room's canvas units so the measured layout (rr_watch_ui.md 2.8) can be copied
    /// number for number.
    ///
    /// Rec Room's own UI prefabs load but draw as grey blocks: their theme, rounded-corner and gradient scripts
    /// are missing at runtime. So the look is rebuilt here from the measurements - the colours of
    /// Palette_LightTheme (all sRGB, as stored), the corner radii (<see cref="Rounded"/>), the gradients
    /// (<see cref="RrUiGradient"/>) - and Rec Room's typefaces: Roboto for labels and body text, Jost for
    /// headings, both made into dynamic TextMeshPro fonts from the Font assets in the bundles (the same route
    /// as <see cref="RrText"/>, whose Jost is reused).
    ///
    /// Every node is created on the UI layer (5): the client's VR ray only answers graphics on that layer.
    /// </summary>
    public static class RrUiKit
    {
        // ---- Palette_LightTheme (rr_watch_ui.md 2.4), sRGB ----

        /// <summary>Surface 0: the cream page every screen sits on.</summary>
        public static readonly Color Page = Hex(0xF9F3EF);

        /// <summary>Foreground 0: dark text and icons on cream.</summary>
        public static readonly Color Ink = Hex(0x393939);
        public static readonly Color InkSoft = Hex(0x6F6A67);
        public static readonly Color White = Color.white;

        /// <summary>Surface 8: the navigation bar's gradient, #FF5F2F from 30% of the way, to #FF7D31.</summary>
        public static readonly Color NavFrom = Hex(0xFF5F2F);
        public static readonly Color NavTo = Hex(0xFF7D31);

        /// <summary>Toggle palette 24: the plate behind a nav tab (alpha 0 idle, 0.3 hover, 0.5 active).</summary>
        public static readonly Color TabPlate = Hex(0x602121);

        /// <summary>Surface 12: notification badges and pills.</summary>
        public static readonly Color Badge = Hex(0x10524B);

        /// <summary>Surface 6: the vertical separator of the home page.</summary>
        public static readonly Color Separator = Hex(0xF15A25);

        /// <summary>Button palette 11: the orange glow behind a home tile on hover.</summary>
        public static readonly Color TilePlate = Hex(0xE06C21);

        /// <summary>Surface 18: the illustration tiles' gradient, white to #CAC8C6.</summary>
        public static readonly Color TileTo = Hex(0xCAC8C6);

        /// <summary>Room card titles.</summary>
        public static readonly Color CardTitle = Hex(0xAC4306);

        /// <summary>Button palette 0: the primary orange call to action, and its hover and pressed gradients.</summary>
        public static readonly Color PrimaryFrom = Hex(0xF76B1C);
        public static readonly Color PrimaryTo = Hex(0xF63E16);
        public static readonly Color PrimaryHoverFrom = Hex(0xF48507);
        public static readonly Color PrimaryHoverTo = Hex(0xF44807);
        public static readonly Color PrimaryPressedFrom = Hex(0xFFBF77);
        public static readonly Color PrimaryPressedTo = Hex(0xFF642A);

        /// <summary>Button palette 4: negative (leave, remove) - normal, hover, pressed.</summary>
        public static readonly Color Negative = Hex(0xC34441);
        public static readonly Color NegativeHover = Hex(0xE24F4C);
        public static readonly Color NegativePressed = Hex(0xE06865);

        /// <summary>Button palette 9/13: white secondary buttons with orange bold text.</summary>
        public static readonly Color SecondaryText = Hex(0xD45427);
        public static readonly Color SecondaryHover = Hex(0xF1F1F1);
        public static readonly Color SecondaryPressed = Hex(0xE5E5E5);

        /// <summary>Focus / hover outline of cards (Border_Weight4_Radius20).</summary>
        public static readonly Color Focus = Hex(0xE15A0C);

        /// <summary>Surface 7: the backdrop of a profile picture.</summary>
        public static readonly Color ProfileBack = Hex(0xB2A8A2);

        /// <summary>Carousel chevrons.</summary>
        public static readonly Color Chevron = Hex(0xBF4A08);

        public static readonly Color Green = Hex(0x0AA975);
        public static readonly Color Lime = Hex(0x7CE00E);
        public static readonly Color Grey = Hex(0x555555);
        public static readonly Color Track = Hex(0xDAD3CE);
        public static readonly Color PeachFrom = Hex(0xF5D2B8);

        public static Color Hex(uint rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // ---- fonts ----

        private static readonly List<Object> OwnedFonts = new List<Object>();

        /// <summary>Roboto Regular: every label and body text of the watch (Roboto-RegularSDFLocalized in Rec Room).</summary>
        public static TMP_FontAsset Body { get; private set; }

        /// <summary>Jost Medium: page titles and headings (text styles 0-2).</summary>
        public static TMP_FontAsset Heading { get; private set; }

        public static IEnumerator LoadFonts()
        {
            TMP_FontAsset roboto = null;
            yield return MakeFont("Roboto-Regular", f => roboto = f);
            TMP_FontAsset jost = RrText.Font != null && RrText.Font.name.StartsWith("Jost") ? RrText.Font : null;
            if (jost == null)
            {
                yield return MakeFont("Jost-500-Medium", f => jost = f);
            }

            var fallback = RrText.Font != null ? RrText.Font : RrText.Fallback;
            Body = roboto != null ? roboto : fallback;
            Heading = jost != null ? jost : Body;
            RrLog.Info("Watch", "fonts: body " + (Body != null ? Body.name : "none") + ", headings " + (Heading != null ? Heading.name : "none"));
        }

        private static IEnumerator MakeFont(string fontName, System.Action<TMP_FontAsset> done)
        {
            Font font = null;
            if (RrAssets.Instance != null)
            {
                yield return RrAssets.Instance.LoadRoutine<Font>(fontName, 128, f => font = f);
            }

            TMP_FontAsset asset = null;
            if (font != null)
            {
                try
                {
                    asset = TMP_FontAsset.CreateFontAsset(font);
                    if (asset != null)
                    {
                        asset.name = fontName + " (watch)";
                        var fallback = RrText.Fallback;
                        if (fallback != null)
                        {
                            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                        }

                        OwnedFonts.Add(asset);
                    }
                }
                catch (System.Exception e)
                {
                    RrLog.Warn("Watch", "font " + fontName + " could not be made into a TMP font: " + e.Message);
                }
            }

            done(asset);
        }

        // ---- nodes ----

        /// <summary>A new RectTransform child on the UI layer.</summary>
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = RrLayers.Ui;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>A child placed from its parent's top-left corner, y downwards (Rec Room's layout numbers).</summary>
        public static RectTransform TopLeft(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A child centred on a point of its parent (offset from the centre, y up).</summary>
        public static RectTransform Centered(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A child stretched over its parent with insets.</summary>
        public static RectTransform Fill(Transform parent, string name, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>A flat rectangle.</summary>
        public static Image Solid(RectTransform rt, Color c, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>A rounded rectangle with Rec Room's corner radius in canvas units (RRUI.RoundedCorners).</summary>
        public static Image Rounded(RectTransform rt, Color c, float radius, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            SetRadius(img, radius);
            img.color = c;
            img.raycastTarget = raycast;
            return img;
        }

        public static void SetRadius(Image img, float radius)
        {
            img.sprite = RrWatchArt.Rounded;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = RrWatchArt.SpriteRadius / Mathf.Max(0.5f, radius);
        }

        /// <summary>A circle (profile pictures, dots).</summary>
        public static Image Circle(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RrWatchArt.Rounded;
            img.type = Image.Type.Simple;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static RrUiGradient Gradient(Graphic g, Color from, Color to, float angle, float start = 0f, float end = 1f)
        {
            var grad = g.gameObject.AddComponent<RrUiGradient>();
            grad.From = from;
            grad.To = to;
            grad.Angle = angle;
            grad.KeyStart = start;
            grad.KeyEnd = end;
            g.SetVerticesDirty();
            return grad;
        }

        /// <summary>A Rec Room sprite (icon, illustration) drawn tinted, keeping its aspect.</summary>
        public static Image Icon(RectTransform rt, string sprite, Color tint)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = tint;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RrWatchArt.Apply(img, sprite);
            return img;
        }

        /// <summary>
        /// A label. Rec Room's watch labels: Roboto, character spacing 2, no wrapping, ellipsis on overflow;
        /// headings Jost. Sizes are canvas units (text sizes 24, 32, 40, 55 in the palette).
        /// </summary>
        public static TextMeshProUGUI Text(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool bold = false, bool heading = false, bool wrap = false)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = heading ? Heading : Body;
            if (font != null)
            {
                t.font = font;
            }

            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = heading ? 1f : 2f;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = wrap ? TextOverflowModes.Truncate : TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.richText = false;
            t.text = text ?? "";
            return t;
        }

        /// <summary>A label on its own node placed from the parent's top-left.</summary>
        public static TextMeshProUGUI Label(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool bold = false, bool heading = false, bool wrap = false)
        {
            return Text(TopLeft(parent, name, x, y, w, h), text, size, color, align, bold, heading, wrap);
        }

        /// <summary>Puts a whole tree on the UI layer (late children included).</summary>
        public static void UiLayer(GameObject go)
        {
            RrLayers.Set(go, RrLayers.Ui);
        }

        // ---- buttons ----

        /// <summary>
        /// A button whose visible state is a plate fading in on hover, the way most RRUI buttons work (nav tabs,
        /// home tiles, quick access, list rows): the plate's colour is fixed and only its alpha changes.
        /// </summary>
        public static RrUiButton PlateButton(RectTransform rt, Color plate, float radius, Vector4 alphas, RrUiSound sound)
        {
            var img = Rounded(Fill(rt, "Plate"), plate, radius, true);
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Plate = img;
            b.PlateAlpha = alphas;
            b.Sound = sound;
            b.Refresh();
            return b;
        }

        /// <summary>Rec Room's primary call-to-action: orange gradient, white bold upper-case Roboto.</summary>
        public static RrUiButton Primary(Transform parent, string name, string text, float x, float y, float w, float h, float textSize = 32f)
        {
            var rt = TopLeft(parent, name, x, y, w, h);
            var img = Rounded(Fill(rt, "Background"), Color.white, 20f, true);
            var grad = Gradient(img, PrimaryFrom, PrimaryTo, -90f);
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Gradient = grad;
            b.GradientStates = new[] { PrimaryFrom, PrimaryTo, PrimaryHoverFrom, PrimaryHoverTo, PrimaryPressedFrom, PrimaryPressedTo };
            b.Sound = RrUiSound.Primary;
            b.Label = Text(Fill(rt, "Label", 10f, 0f, 10f, 0f), text.ToUpperInvariant(), textSize, White, TextAlignmentOptions.Center, true);
            b.Refresh();
            return b;
        }

        /// <summary>A white secondary button with orange bold upper-case text (button palette 9).</summary>
        public static RrUiButton Secondary(Transform parent, string name, string text, float x, float y, float w, float h, float textSize = 28f)
        {
            var rt = TopLeft(parent, name, x, y, w, h);
            var img = Rounded(Fill(rt, "Background"), Color.white, 20f, true);
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Fill = img;
            b.FillStates = new[] { Color.white, SecondaryHover, SecondaryPressed };
            b.Sound = RrUiSound.Default;
            b.Label = Text(Fill(rt, "Label", 10f, 0f, 10f, 0f), text.ToUpperInvariant(), textSize, SecondaryText, TextAlignmentOptions.Center, true);
            b.Refresh();
            return b;
        }

        /// <summary>A red negative button (button palette 4): leave, remove.</summary>
        public static RrUiButton NegativeButton(Transform parent, string name, string text, float x, float y, float w, float h, float textSize = 32f)
        {
            var rt = TopLeft(parent, name, x, y, w, h);
            var img = Rounded(Fill(rt, "Background"), Negative, 20f, true);
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Fill = img;
            b.FillStates = new[] { Negative, NegativeHover, NegativePressed };
            b.Sound = RrUiSound.Negative;
            b.Label = Text(Fill(rt, "Label", 10f, 0f, 10f, 0f), text.ToUpperInvariant(), textSize, White, TextAlignmentOptions.Center, true);
            b.Refresh();
            return b;
        }

        /// <summary>A round white icon button (back arrow, close).</summary>
        public static RrUiButton IconButton(Transform parent, string name, string sprite, float x, float y, float size, Color iconTint, float iconScale = 0.6f)
        {
            var rt = TopLeft(parent, name, x, y, size, size);
            var img = Rounded(Fill(rt, "Background"), Color.white, 20f, true);
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Fill = img;
            b.FillStates = new[] { Color.white, SecondaryHover, SecondaryPressed };
            b.Sound = RrUiSound.Default;
            Icon(Centered(rt, "Icon", 0f, 0f, size * iconScale, size * iconScale), sprite, iconTint);
            b.Refresh();
            return b;
        }

        /// <summary>A pill badge ("3 playing", "You"): #10524B, white text, radius 100.</summary>
        public static TextMeshProUGUI Pill(Transform parent, string name, string text, float x, float y, float h, Color back, Color fore, float textSize = 24f)
        {
            var rt = TopLeft(parent, name, x, y, 10f, h);
            Rounded(rt, back, 100f);
            var fitter = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            fitter.padding = new RectOffset(18, 18, 0, 0);
            fitter.childAlignment = TextAnchor.MiddleCenter;
            fitter.childControlWidth = true;
            fitter.childControlHeight = true;
            fitter.childForceExpandWidth = false;
            var size = rt.gameObject.AddComponent<ContentSizeFitter>();
            size.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var label = Text(Node(rt, "Text"), text, textSize, fore, TextAlignmentOptions.Center, true);
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        /// <summary>
        /// A vertical scroll area (mouse wheel, drag, the VR ray's drag) with a thin scrollbar on its right, as
        /// Rec Room's lists: elastic ends (0.1), inertia. Content is anchored to the top; set its height.
        /// </summary>
        public static ScrollRect Scroll(Transform parent, string name, float x, float y, float w, float h, out RectTransform content)
        {
            var rt = TopLeft(parent, name, x, y, w, h);
            var viewport = Fill(rt, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();

            // Something must catch the pointer between the items, or dragging on empty space scrolls nothing.
            Solid(viewport, new Color(1f, 1f, 1f, 0f), true);
            content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, h);

            var scroll = rt.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 60f;

            var bar = TopLeft(rt, "Scrollbar", w - 24f, 20f, 12f, h - 40f);
            Rounded(bar, WithAlpha(Grey, 0.18f), 6f, true);
            var area = Fill(bar, "Sliding Area");
            var handle = Fill(area, "Handle");
            var handleImg = Rounded(handle, InkSoft, 6f, true);
            var sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle;
            sb.targetGraphic = handleImg;
            sb.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        public static void Shutdown()
        {
            foreach (var o in OwnedFonts)
            {
                var f = o as TMP_FontAsset;
                if (f != null)
                {
                    if (f.atlasTextures != null)
                    {
                        foreach (var t in f.atlasTextures)
                        {
                            if (t != null)
                            {
                                Object.Destroy(t);
                            }
                        }
                    }

                    if (f.material != null)
                    {
                        Object.Destroy(f.material);
                    }

                    Object.Destroy(f);
                }
            }

            OwnedFonts.Clear();
            Body = null;
            Heading = null;
        }
    }
}
