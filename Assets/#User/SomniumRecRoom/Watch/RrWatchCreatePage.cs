using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// Create (Rec Room's watch Create tab, rr_maker_rooms.md 1.1): "pick a base room" cards - one per maker-room template
    /// (<see cref="RrMakerTemplates"/>) with the install's art - then the picked one's details and CREATE ROOM, which makes
    /// "&lt;Name&gt;'s &lt;Template&gt;" owned by this player, saves it and goes there (the Maker Pen is handed over on
    /// arrival). Templates that are not available yet show "Coming soon". MY ROOMS opens the Rooms page.
    /// </summary>
    public sealed class RrWatchCreatePage : RrWatchPage
    {
        private const int PerRow = 6;
        private const float Top = 140f;

        private readonly List<KeyValuePair<RrMakerTemplate, Image>> _outlines = new List<KeyValuePair<RrMakerTemplate, Image>>();
        private RrMakerTemplate _picked;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _blurb;
        private TextMeshProUGUI _status;
        private RrUiButton _create;
        private CanvasGroup _createGroup;
        private int _seen = -1;

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.Create; }
        }

        public override void Build()
        {
            Title("Create");
            RrUiKit.Label(Root, "Subtitle", "Pick a base room for your new room", 300f, 50f, 1000f, 60f, 32f, RrUiKit.InkSoft);
            var mine = RrUiKit.Secondary(Root, "My Rooms", "My Rooms", Width - Margin - 300f, 40f, 300f, 76f, 26f);
            mine.onClick.AddListener(() => Ui.ShowPage(RrWatchPageId.Rooms));

            var content = RrUiKit.TopLeft(Root, "Cards", 0f, Top, Width, 2f * RrWatchCard.Height);
            int n = 0;
            foreach (var t in RrMakerTemplates.All)
            {
                float x = Margin - RrWatchCard.ArtInset + (n % PerRow) * RrWatchCard.Width;
                float y = (n / PerRow) * RrWatchCard.Height;
                Card(content, t, x, y);
                n++;
            }

            float panelY = Top + 2f * RrWatchCard.Height + 10f;
            var panel = RrUiKit.TopLeft(Root, "Details", Margin, panelY, Width - 2f * Margin, Height - panelY - 30f);
            RrUiKit.Rounded(panel, Color.white, 20f);
            float pw = Width - 2f * Margin;
            _name = RrUiKit.Label(panel, "Name", "", 40f, 20f, pw - 560f, 60f, 40f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true, true);
            _blurb = RrUiKit.Label(panel, "Blurb", "", 40f, 86f, pw - 560f, 120f, 28f, RrUiKit.InkSoft, TextAlignmentOptions.TopLeft, false, false, true);
            _status = RrUiKit.Label(panel, "Status", "", 40f, 236f, pw - 560f, 44f, 26f, RrUiKit.CardTitle, TextAlignmentOptions.MidlineLeft);
            _create = RrUiKit.Primary(panel, "Create", "Create Room", pw - 480f, 60f, 440f, 120f, 40f);
            _create.onClick.AddListener(CreatePicked);
            _createGroup = _create.gameObject.AddComponent<CanvasGroup>();
            RrUiKit.UiLayer(Root.gameObject);

            foreach (var t in RrMakerTemplates.All)
            {
                if (t.Available)
                {
                    Pick(t);
                    break;
                }
            }
        }

        /// <summary>A template's card art (its own picture, else the art of the activity it comes from).</summary>
        public static RrActivityArt ArtOf(RrMakerTemplate t)
        {
            if (string.IsNullOrEmpty(t.Art))
            {
                var a = RrWatchArt.For(t.ArtOf, RrRooms.Get(t.ArtOf));
                return new RrActivityArt { Title = t.Title, Asset = a.Asset, Asset2 = a.Asset2, Crop = a.Crop, Fit = t.ArtFit ? RrArtFit.Fit : a.Fit, BackFrom = a.BackFrom, BackTo = a.BackTo, Tint = a.Tint };
            }

            return new RrActivityArt { Title = t.Title, Asset = t.Art, Fit = t.ArtFit ? RrArtFit.Fit : RrArtFit.Cover, BackFrom = t.BackFrom, BackTo = t.BackTo };
        }

        private void Card(RectTransform parent, RrMakerTemplate t, float x, float y)
        {
            var root = RrUiKit.TopLeft(parent, "Card " + t.Key, x, y, RrWatchCard.Width, RrWatchCard.Height);
            var hit = RrUiKit.Rounded(RrUiKit.Fill(root, "Hit"), new Color(1f, 1f, 1f, 0f), 20f, true);
            var b = root.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = hit;
            b.Sound = t.Available ? RrUiSound.Card : RrUiSound.Inactive;
            b.HoverScale = 1.03f;

            var art = ArtOf(t);
            var frame = RrUiKit.TopLeft(root, "Art", RrWatchCard.ArtInset, RrWatchCard.ArtInset, RrWatchCard.ArtWidth, RrWatchCard.ArtHeight);
            var back = RrUiKit.Rounded(frame, Color.white, 16f);
            RrUiKit.Gradient(back, art.BackFrom, art.BackTo, -90f);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RrWatchCard.Picture(frame, art, 12f);
            if (!t.Available)
            {
                var veil = RrUiKit.Fill(frame, "Veil");
                RrUiKit.Solid(veil, new Color(1f, 1f, 1f, 0.55f));
                RrUiKit.Pill(frame, "Soon", t.Missing ?? "Coming soon", 10f, 10f, 40f, RrUiKit.Grey, Color.white, 22f);
            }

            var outline = RrUiKit.Fill(root, "Picked", RrWatchCard.ArtInset - 6f, RrWatchCard.ArtInset - 6f, RrWatchCard.ArtInset - 6f, RrWatchCard.Height - RrWatchCard.ArtInset - RrWatchCard.ArtHeight - 6f);
            var outlineImg = outline.gameObject.AddComponent<Image>();
            outlineImg.color = RrUiKit.Focus;
            outlineImg.type = Image.Type.Sliced;
            outlineImg.raycastTarget = false;
            outlineImg.enabled = false;
            RrWatchArt.Get("Border_Weight4_Radius20", s =>
            {
                if (outlineImg != null)
                {
                    outlineImg.sprite = s;
                }
            });
            _outlines.Add(new KeyValuePair<RrMakerTemplate, Image>(t, outlineImg));

            var title = RrUiKit.Label(root, "Title", t.Title, RrWatchCard.ArtInset, RrWatchCard.ArtInset + RrWatchCard.ArtHeight + 8f, RrWatchCard.ArtWidth, 48f, 32f, RrUiKit.CardTitle, TextAlignmentOptions.MidlineLeft, true);
            title.enableAutoSizing = true;
            title.fontSizeMin = 22f;
            title.fontSizeMax = 32f;
            string kind = !t.Available ? "Coming soon" : t.BuildBase == null || t.Key == RrMakerTemplates.Dorm ? "All editable" : "Rec Room level";
            RrUiKit.Label(root, "Subtitle", kind + "  ·  " + t.MaxPlayers + " players", RrWatchCard.ArtInset, RrWatchCard.ArtInset + RrWatchCard.ArtHeight + 56f, RrWatchCard.ArtWidth, 40f, 24f, RrUiKit.Ink);
            b.Refresh();
            b.onClick.AddListener(() => Pick(t));
        }

        private void Pick(RrMakerTemplate t)
        {
            _picked = t;
            foreach (var kv in _outlines)
            {
                kv.Value.enabled = kv.Key == t && kv.Value.sprite != null;
            }

            _name.text = t.Title;
            _blurb.text = t.Blurb ?? "";
            Refresh();
        }

        private void CreatePicked()
        {
            if (_picked == null || !_picked.Available || RrTravel.Busy || RrMakerRooms.Opening)
            {
                return;
            }

            RrLog.Info("Watch", "create a room on " + _picked.Title);
            RrWatch.Close();
            RrMakerRooms.Create(_picked.Key);
        }

        public override void Refresh()
        {
            if (_picked == null || _create == null)
            {
                return;
            }

            bool can = _picked.Available && !RrTravel.Busy && !RrMakerRooms.Opening;
            _create.interactable = can;
            _createGroup.alpha = can ? 1f : 0.45f;
            _create.Label.text = !_picked.Available ? "COMING SOON" : RrTravel.Busy ? "TRAVELLING..." : "CREATE ROOM";
            string name = RrMakerRooms.CleanName(RrMakerRooms.MyName + "'s " + _picked.Title);
            string status = Time.realtimeSinceStartup - RrMakerRooms.StatusAt < 20f ? RrMakerRooms.Status : "";
            _status.text = !string.IsNullOrEmpty(status) ? status : _picked.Available ? "Your room will be called \"" + name + "\" (rename it later in This Room > Room Settings)." : "";
            foreach (var kv in _outlines)
            {
                kv.Value.enabled = kv.Key == _picked && kv.Value.sprite != null;
            }
        }

        public override void Tick()
        {
            int serial = RrMakerRooms.Serial + (RrTravel.Busy ? 1 << 30 : 0);
            if (serial != _seen)
            {
                _seen = serial;
                Refresh();
            }
        }
    }
}
