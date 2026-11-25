using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Watch;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// The hologram a <see cref="RrGameConsole"/> projects: Rec Room's own <c>[GameScoreboardView]</c> layout rebuilt
    /// cell for cell with UnityEngine.UI, because the prefab's own view scripts (GameScoreboardView, AutoScaledText,
    /// the header / body / footer views) are Rec Room code and missing at run time - loaded as it is, the timer, the
    /// button labels and every player row would stay empty.
    ///
    /// Measured from the install (bundle 43ea1176..., <c>[GameScoreboardView]</c> #7808896619519225561 and the row
    /// prefabs in 1df8e0ef...): a 1333.33 x 1000 world canvas. The header is 265 tall: the title row (title cell 1033
    /// + timer cell 300, Futura T OT Bold Oblique 84 / 53), the subtitle row 65 (the mode toggle, 40) and the team
    /// header row 65 ("PLAYER" / "SCORE", 40). The body takes what is left (its preferred 900 is squeezed to 485),
    /// then the page-control row 50 and the footer 200 whose button row holds START (Futura oblique 75, BorderFill
    /// button, 25 padding), SWITCH TEAM, "NEXT MAP: X" and VOTE. Every cell is the view's primaryColor
    /// rgba(1, 0.953, 0.412) at defaultBackgroundAlpha 0.5 (highlightBackgroundAlpha 1 marks the local player's
    /// row here) with a #1A1A1A SquareBorder outline (a 64 px sprite with 2 px borders, sliced) and #1A1A1A text;
    /// player names are legacy_FuturaT_Bold, the rest legacy_Futura-T-OT-Bold-Oblique; a white Border sprite frames
    /// the whole view. The page-control row carries our status line.
    ///
    /// Every node is on the UI layer (5): the client's VR ray only answers graphics on that layer.
    /// </summary>
    internal sealed class RrGameConsolePanel
    {
        public const float Width = 1333.33f;
        public const float Height = 1000f;

        // ---- [GameScoreboardView] layout (canvas units, y down from the top) ----
        private const float SideW = 300f;
        private const float MainW = Width - SideW;
        private const float TitleH = 135f;
        private const float SubtitleY = 135f;
        private const float RowH = 65f;
        private const float HeaderY = 200f;
        private const float BodyY = 265f;
        private const float StatusY = 750f;
        private const float StatusH = 50f;
        private const float FooterY = 800f;
        private const float FooterH = 200f;
        private const float Pad = 25f;
        private const float MaxRow = 64f;
        private const float MinRow = 34f;

        public static readonly Color Yellow = new Color(1f, 0.952941f, 0.411765f, 1f);
        public static readonly Color Ink = new Color(0.101961f, 0.101961f, 0.101961f, 1f);
        public const float CellAlpha = 0.5f;
        public const float HighlightAlpha = 1f;

        public const string ObliqueFont = "legacy_Futura-T-OT-Bold-Oblique_Static";
        public const string BoldFont = "legacy_FuturaT_Bold";
        public const string SquareBorder = "SquareBorder";
        public const string BorderFill = "BorderFill";
        public const string Border = "Border";

        // ---- fonts, shared by every console, made once and destroyed with the last console ----
        private static TMP_FontAsset _oblique;
        private static TMP_FontAsset _bold;
        private static bool _fontsLoading;
        private static bool _fontsDone;
        private static readonly List<Object> OwnedFonts = new List<Object>();

        public static bool FontsReady
        {
            get { return _fontsDone; }
        }

        public static IEnumerator LoadFonts()
        {
            if (_fontsDone)
            {
                yield break;
            }

            if (_fontsLoading)
            {
                while (_fontsLoading)
                {
                    yield return null;
                }

                yield break;
            }

            _fontsLoading = true;
            yield return MakeFont(ObliqueFont, f => _oblique = f);
            yield return MakeFont(BoldFont, f => _bold = f);
            var fallback = RrText.Title != null ? RrText.Title : RrText.Fallback;
            if (_oblique == null)
            {
                _oblique = fallback;
            }

            if (_bold == null)
            {
                _bold = _oblique;
            }

            _fontsLoading = false;
            _fontsDone = true;
            RrLog.Info("Console", "fonts: labels " + (_oblique != null ? _oblique.name : "none") + ", names " + (_bold != null ? _bold.name : "none"));
        }

        private static IEnumerator MakeFont(string fontName, Action<TMP_FontAsset> done)
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
                        asset.name = fontName + " (console)";
                        var fallback = RrText.Fallback;
                        if (fallback != null)
                        {
                            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                        }

                        OwnedFonts.Add(asset);
                    }
                }
                catch (Exception e)
                {
                    RrLog.Warn("Console", "font " + fontName + " could not be made into a TMP font: " + e.Message);
                }
            }

            done(asset);
        }

        /// <summary>Destroys the shared fonts (the last console is gone: nothing may outlive the room).</summary>
        public static void ReleaseFonts()
        {
            foreach (var o in OwnedFonts)
            {
                var f = o as TMP_FontAsset;
                if (f == null)
                {
                    continue;
                }

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

            OwnedFonts.Clear();
            _oblique = null;
            _bold = null;
            _fontsDone = false;
            _fontsLoading = false;
        }

        // ---- the model a refresh hands over ----

        public struct Row
        {
            public bool Header;
            public string Name;
            public string Side;
            public int Team;
            public bool Local;
        }

        public sealed class Model
        {
            public string Title = "";
            public string Timer = "";
            public string Subtitle = "";
            public bool SubtitleIsButton;
            public string Count = "";
            public string NameHeader = "PLAYER";
            public string SideHeader = "";
            public readonly List<Row> Rows = new List<Row>();
            public string Status = "";
            public string Start = "START";
            public bool StartEnabled;
            public string Map;
            public bool MapButton;
        }

        // ---- the built canvas ----

        public RectTransform Rect;
        public Canvas Canvas;
        public RrUiButton StartButton;
        public RrUiButton ModeButton;
        public RrUiButton MapButton;
        public int RowsShown;

        private TextMeshProUGUI _title;
        private TextMeshProUGUI _timer;
        private TextMeshProUGUI _subtitle;
        private TextMeshProUGUI _count;
        private TextMeshProUGUI _nameHeader;
        private TextMeshProUGUI _sideHeader;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _startLabel;
        private TextMeshProUGUI _mapLabel;
        private RectTransform _body;
        private RectTransform _subtitleCell;
        private Image _startFill;
        private string _rowsKey = "";
        private bool _hasMaps;

        /// <summary>Builds the canvas (inactive layout decisions: modes and maps are known up front).</summary>
        public static RrGameConsolePanel Build(Transform parent, bool hasModes, bool hasMaps)
        {
            var p = new RrGameConsolePanel();
            var go = new GameObject("[GameScoreboardView]", typeof(RectTransform));
            go.layer = RrLayers.Ui;
            p.Rect = (RectTransform)go.transform;
            p.Rect.SetParent(parent, false);
            p.Rect.sizeDelta = new Vector2(Width, Height);
            p.Rect.pivot = new Vector2(0.5f, 0.5f);
            p.Canvas = go.AddComponent<Canvas>();
            p.Canvas.renderMode = RenderMode.WorldSpace;
            p._hasMaps = hasMaps;

            var grid = RrUiKit.Fill(p.Rect, "Grid");

            // Header: title + timer, subtitle (mode toggle) + count, team header.
            var titleCell = Cell(grid, "TitleColumn", 0f, 0f, MainW, TitleH, CellAlpha);
            p._title = Label(titleCell, "TitleText", 84f, TextAlignmentOptions.MidlineLeft, true, 30f);
            var timerCell = Cell(grid, "TimerColumn", MainW, 0f, SideW, TitleH, CellAlpha);
            p._timer = Label(timerCell, "TimerText", 53f, TextAlignmentOptions.Center, true);

            p._subtitleCell = Cell(grid, "SubtitleColumn", 0f, SubtitleY, MainW, RowH, CellAlpha);
            if (hasModes)
            {
                p.ModeButton = Button(p._subtitleCell, "ModeToggleButton", 6f, 6f, MainW - 12f, RowH - 12f, 40f, out p._subtitle);
            }
            else
            {
                p._subtitle = Label(p._subtitleCell, "SubtitleText", 40f, TextAlignmentOptions.MidlineLeft, true, 30f);
            }

            var countCell = Cell(grid, "SubtitleScoreColumn", MainW, SubtitleY, SideW, RowH, CellAlpha);
            p._count = Label(countCell, "SubtitleScoreText", 40f, TextAlignmentOptions.Center, true);

            var nameHead = Cell(grid, "NameColumn", 0f, HeaderY, MainW, RowH, CellAlpha);
            p._nameHeader = Label(nameHead, "Text", 40f, TextAlignmentOptions.MidlineLeft, true, 30f);
            var sideHead = Cell(grid, "ScoreColumn", MainW, HeaderY, SideW, RowH, CellAlpha);
            p._sideHeader = Label(sideHead, "ScoreText", 40f, TextAlignmentOptions.Center, true);

            // Body: rows made on refresh.
            p._body = RrUiKit.TopLeft(grid, "Body", 0f, BodyY, Width, StatusY - BodyY);

            // Page-control row: our status line.
            var status = Cell(grid, "PageControls", 0f, StatusY, Width, StatusH, CellAlpha);
            p._status = Label(status, "Text", 34f, TextAlignmentOptions.Center, true);

            // Footer: START (the whole row, or the right half beside the map).
            var footer = Cell(grid, "Footer", 0f, FooterY, Width, FooterH, CellAlpha);
            float startX = hasMaps ? Width * 0.5f + Pad * 0.5f : Pad;
            float startW = hasMaps ? Width * 0.5f - Pad * 1.5f : Width - Pad * 2f;
            p.StartButton = Button(footer, "StartGameButton", startX, Pad, startW, FooterH - Pad * 2f, 75f, out p._startLabel);
            p._startFill = p.StartButton.Fill as Image;
            if (hasMaps)
            {
                p._mapLabel = Label(RrUiKit.TopLeft(footer, "MapLabelColumn", Pad, Pad, Width * 0.5f - Pad * 1.5f, 70f), "LabelText", 44f, TextAlignmentOptions.MidlineLeft, true, 10f);
                p.MapButton = Button(footer, "VoteMapButton", Pad, Pad + 80f, Width * 0.5f - Pad * 1.5f, FooterH - Pad * 2f - 80f, 44f, out _);
                p.MapButton.Label.text = "CHANGE MAP";
            }

            // The white frame around the whole view (its root's Outline, Border sprite).
            var frame = RrUiKit.Fill(p.Rect, "Outline");
            var frameImg = frame.gameObject.AddComponent<Image>();
            frameImg.color = Color.white;
            frameImg.raycastTarget = false;
            frameImg.type = Image.Type.Sliced;
            frameImg.fillCenter = false;
            Sprite(frameImg, Border);

            RrUiKit.UiLayer(go);
            return p;
        }

        /// <summary>Shows a model; rows are only rebuilt when their content changed.</summary>
        public void Apply(Model m)
        {
            Set(_title, m.Title);
            Set(_timer, m.Timer);
            Set(_subtitle, m.Subtitle);
            Set(_count, m.Count);
            Set(_nameHeader, m.NameHeader);
            Set(_sideHeader, m.SideHeader);
            Set(_status, m.Status);
            Set(_startLabel, m.Start);
            if (_mapLabel != null)
            {
                Set(_mapLabel, m.Map ?? "");
            }

            if (MapButton != null)
            {
                MapButton.interactable = m.MapButton;
            }

            if (StartButton.interactable != m.StartEnabled)
            {
                StartButton.interactable = m.StartEnabled;
            }

            // Greyed START: the plate fades to a third, the label to grey (Rec Room hides the button; we keep it,
            // so the player sees why nothing starts).
            if (_startFill != null)
            {
                var c = _startFill.color;
                c.a = m.StartEnabled ? 1f : 0.35f;
                _startFill.color = c;
            }

            if (_startLabel != null)
            {
                _startLabel.color = m.StartEnabled ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.45f);
            }

            var key = new System.Text.StringBuilder();
            foreach (var r in m.Rows)
            {
                key.Append(r.Header ? 'H' : 'P').Append(r.Team).Append(r.Local ? '*' : ' ').Append(r.Name).Append('|').Append(r.Side).Append('\n');
            }

            string k = key.ToString();
            if (k != _rowsKey)
            {
                _rowsKey = k;
                BuildRows(m.Rows);
            }
        }

        private void BuildRows(List<Row> rows)
        {
            for (int i = _body.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_body.GetChild(i).gameObject);
            }

            float room = StatusY - BodyY;
            int n = Mathf.Max(1, rows.Count);
            float h = Mathf.Clamp(room / n, MinRow, MaxRow);
            int fit = Mathf.Max(1, Mathf.FloorToInt(room / h));
            int shown = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (shown == fit - 1 && rows.Count > fit)
                {
                    int more = rows.Count - shown;
                    var moreCell = Cell(_body, "More", 0f, shown * h, Width, h, CellAlpha);
                    Label(moreCell, "Text", h * 0.55f, TextAlignmentOptions.Center, true).text = "+" + more + " MORE";
                    shown++;
                    break;
                }

                var r = rows[i];
                Color back = r.Team >= 0 ? RrTeams.Color(r.Team) : Yellow;
                float alpha = r.Local ? HighlightAlpha : CellAlpha;
                if (r.Header)
                {
                    // (GameScoreboardTeamRow): the team's colour across the row, its name left, its count right.
                    var cell = Cell(_body, "TeamRow " + r.Name, 0f, shown * h, Width, h, 0.8f, back);
                    Label(cell, "Name", h * 0.6f, TextAlignmentOptions.MidlineLeft, true, 30f).text = r.Name;
                    var side = Label(RrUiKit.TopLeft(cell, "Side", MainW, 0f, SideW, h), "Text", h * 0.6f, TextAlignmentOptions.Center, true);
                    side.text = r.Side;
                }
                else
                {
                    // (GameScoreboardPlayerRow): NameColumn (Futura T Bold) + a stat column.
                    var nameCell = Cell(_body, "PlayerRow " + r.Name, 0f, shown * h, MainW, h, alpha, back);
                    var name = Label(nameCell, "Text", h * 0.62f, TextAlignmentOptions.MidlineLeft, false, 30f);
                    name.text = r.Name;
                    var statCell = Cell(_body, "Stat " + r.Name, MainW, shown * h, SideW, h, alpha, back);
                    Label(statCell, "Text", h * 0.5f, TextAlignmentOptions.Center, true).text = r.Side;
                }

                shown++;
            }

            RowsShown = shown;
            RrUiKit.UiLayer(_body.gameObject);
        }

        // ---- pieces ----

        private static void Set(TextMeshProUGUI t, string text)
        {
            if (t != null && t.text != (text ?? ""))
            {
                t.text = text ?? "";
            }
        }

        /// <summary>A yellow (or team-coloured) cell with the #1A1A1A SquareBorder outline.</summary>
        private static RectTransform Cell(Transform parent, string name, float x, float y, float w, float h, float alpha, Color? colour = null)
        {
            var rt = RrUiKit.TopLeft(parent, name, x, y, w, h);
            var c = colour ?? Yellow;
            c.a = alpha;
            RrUiKit.Solid(rt, c);
            var outline = RrUiKit.Fill(rt, "Outline");
            var img = outline.gameObject.AddComponent<Image>();
            img.color = Ink;
            img.raycastTarget = false;
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            Sprite(img, SquareBorder);
            return rt;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, float size, TextAlignmentOptions align, bool oblique, float inset = 8f)
        {
            var rt = RrUiKit.Fill(parent, name, inset, 0f, inset, 0f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = oblique ? _oblique : _bold;
            if (font != null)
            {
                t.font = font;
            }

            t.fontSize = size;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Max(10f, size * 0.45f);
            t.color = Ink;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.richText = false;
            t.text = "";
            return t;
        }

        /// <summary>A white BorderFill button with a dark oblique label (the view's START / VOTE / mode toggle).</summary>
        private static RrUiButton Button(RectTransform parent, string name, float x, float y, float w, float h, float textSize, out TextMeshProUGUI label)
        {
            var rt = RrUiKit.TopLeft(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.type = Image.Type.Sliced;
            Sprite(img, BorderFill);
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Fill = img;
            b.FillStates = new[] { Color.white, new Color(1f, 0.97f, 0.72f), new Color(0.85f, 0.85f, 0.85f) };
            b.Sound = RrUiSound.Primary;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            label = Label(rt, "Text", textSize, TextAlignmentOptions.Center, true);
            b.Label = label;
            b.Refresh();
            return b;
        }

        /// <summary>Puts one of the view's sprites on an image when it arrives (the image hides until then).</summary>
        private static void Sprite(Image img, string sprite)
        {
            img.enabled = false;
            RrWatchArt.Get(sprite, s =>
            {
                if (img == null)
                {
                    return;
                }

                img.sprite = s;
                img.type = Image.Type.Sliced;

                // Without its sprite a frame would be a solid block: it stays hidden. A fill keeps its colour.
                img.enabled = s != null || img.fillCenter;
            });

            if (img.fillCenter)
            {
                img.enabled = true;
            }
        }
    }
}
