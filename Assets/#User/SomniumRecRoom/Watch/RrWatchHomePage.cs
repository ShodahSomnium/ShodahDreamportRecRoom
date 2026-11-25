using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// The Home screen, Rec Room's "MainMenu" page (MainMenu-SizeExperiment-SmallerButtons, measured): a
    /// centred row of big illustrated tiles (190 x 220, orange glow on hover, the illustration on a white to
    /// grey tile of radius 30, a 32-unit label), a thin orange separator, and "Quick Access" to the Dorm Room
    /// and the Rec Center (380 wide, "Go to" over the place's name in bold, an illustration and a chevron).
    /// Rec Room's tiles were Play, Create, Store, Events, Clubs, Challenges and Backpack; here they are the
    /// screens this world has - Play, People, This Room, Settings, Create and Rooms (player-made rooms, iteration 14) - and
    /// "Maker Pen" (Rec Room's Create illustration),
    /// which gives the Maker Pen or puts it away (<see cref="Interact.MakerPen.RrMakerPen.Toggle"/>). Below, as on the newer Rec Room home, a row
    /// of the activities with the most players.
    /// </summary>
    public sealed class RrWatchHomePage : RrWatchPage
    {
        private const float TileWidth = 170f;

        /// <summary>Gap between tiles (Rec Room: 20); 14 since iteration 15 so eight tiles (Avatar added) fit beside Quick Access.</summary>
        private const float TileGap = 14f;
        private const float TileHeight = 220f;
        private const float RowTop = 150f;
        private const int PopularCount = 6;

        private TextMeshProUGUI _where;
        private RectTransform _whereRoot;
        private RectTransform _popular;
        private readonly List<RrWatchCard> _cards = new List<RrWatchCard>();
        private readonly List<RrActivity> _order = new List<RrActivity>();
        private string _cardsKey = "";
        private TextMeshProUGUI _penLabel;

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.Home; }
        }

        public override void Build()
        {
            Title("Home");

            // Where the player is, top right (the location pin of Rec Room's widget watch).
            _whereRoot = RrUiKit.TopLeft(Root, "Location", 0f, 44f, 10f, 60f);
            _whereRoot.anchorMin = _whereRoot.anchorMax = new Vector2(1f, 1f);
            _whereRoot.pivot = new Vector2(1f, 1f);
            _whereRoot.anchoredPosition = new Vector2(-Margin, -44f);
            RrUiKit.Rounded(_whereRoot, Color.white, 100f);
            var h = _whereRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 26, 8, 8);
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            _whereRoot.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var pin = RrUiKit.Node(_whereRoot, "Pin");
            var pinLayout = pin.gameObject.AddComponent<LayoutElement>();
            pinLayout.preferredWidth = 40f;
            pinLayout.preferredHeight = 40f;
            RrUiKit.Icon(pin, "icon_AppNav_Location", RrUiKit.PrimaryTo);
            _where = RrUiKit.Text(RrUiKit.Node(_whereRoot, "Text"), "", 28f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft);
            _where.overflowMode = TextOverflowModes.Overflow;

            // The main row, centred as Rec Room's (HLG spacing 20, align middle centre).
            float total = 8 * TileWidth + 7 * TileGap + 20f + 3f + 20f + 380f;
            float x = (Width - total) * 0.5f;
            Tile(x, "Play", "Illustration_Menu_Play", () => Ui.ShowPage(RrWatchPageId.Play));
            x += TileWidth + TileGap;
            Tile(x, "People", "Illustration_Friends", () => Ui.ShowPage(RrWatchPageId.People));
            x += TileWidth + TileGap;
            Tile(x, "This Room", "Illustration_Menu_ThisRoom", () => Ui.ShowPage(RrWatchPageId.ThisRoom));
            x += TileWidth + TileGap;
            Tile(x, "Settings", "Illustration_Headphone", () => Ui.ShowPage(RrWatchPageId.Settings));
            x += TileWidth + TileGap;
            // Player-made rooms (Places/MakerRooms): Rec Room's Create tab, and the rooms players made.
            Tile(x, "Create", "Illustration_CreateSubRoom", () => Ui.ShowPage(RrWatchPageId.Create));
            x += TileWidth + TileGap;
            Tile(x, "Rooms", "Illustration_PublishRoom", () => Ui.ShowPage(RrWatchPageId.Rooms));
            x += TileWidth + TileGap;
            _penLabel = Tile(x, "Maker Pen", "Illustration_Menu_Create", ToggleMakerPen);
            x += TileWidth + TileGap;
            // Iteration 15: be a Rec Room avatar (or keep the Somnium one) and dress it.
            Tile(x, "Avatar", "Illustration_ClothingCustomizer", () => Ui.ShowPage(RrWatchPageId.Avatar));
            x += TileWidth + 20f;

            var line = RrUiKit.TopLeft(Root, "Separator", x, RowTop + 15f, 3f, TileHeight - 30f);
            RrUiKit.Solid(line, RrUiKit.Separator);
            x += 23f;

            float quickTop = RowTop + TileHeight * 0.5f - 135f;
            Quick(x, quickTop, "Dorm Room", "Illustration_Menu_Dorm", () => Ui.Travel(RrActivity.Dorm, -1));
            Quick(x, quickTop + 140f, "Rec Center", "Illustration_Menu_RecCenter", () => Ui.Travel(RrActivity.RecCenter, -1));

            RrUiKit.Label(Root, "Popular Title", "Popular right now", Margin, 470f, 900f, 60f, 32f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            var more = RrUiKit.Secondary(Root, "More", "All activities", Width - Margin - 300f, 468f, 300f, 64f, 24f);
            more.onClick.AddListener(() => Ui.ShowPage(RrWatchPageId.Play));
            _popular = RrUiKit.TopLeft(Root, "Popular", 0f, 540f, Width, RrWatchCard.Height);
        }

        private TextMeshProUGUI Tile(float x, string label, string illustration, System.Action onClick)
        {
            var rt = RrUiKit.TopLeft(Root, "Tile " + label, x, RowTop, TileWidth, TileHeight);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + TileWidth * 0.5f, -RowTop - TileHeight * 0.5f);
            var b = RrUiKit.PlateButton(rt, RrUiKit.TilePlate, 40f, new Vector4(0f, 0.3f, 0.3f, 0f), RrUiSound.Default);
            var tile = RrUiKit.TopLeft(rt, "Icon", 15f, 15f, TileWidth - 30f, 140f);
            var tileImg = RrUiKit.Rounded(tile, Color.white, 30f);
            RrUiKit.Gradient(tileImg, Color.white, RrUiKit.TileTo, -45f);
            RrUiKit.Icon(RrUiKit.Fill(tile, "Illustration", 10f, 6f, 10f, 6f), illustration, Color.white);
            var text = RrUiKit.Label(rt, "Label", label, 4f, 165f, TileWidth - 8f, 50f, 32f, RrUiKit.Ink, TextAlignmentOptions.Center);
            b.onClick.AddListener(() => onClick());
            return text;
        }

        /// <summary>The Maker Pen tile: gives the pen (and closes the menu so it can be used) or puts it away.</summary>
        private void ToggleMakerPen()
        {
            bool giving = !Interact.MakerPen.RrMakerPen.IsOut;
            Interact.MakerPen.RrMakerPen.Toggle();
            if (giving)
            {
                RrWatch.Close();
            }

            Tick();
        }

        public override void Tick()
        {
            if (_penLabel != null)
            {
                string want = Interact.MakerPen.RrMakerPen.IsOut ? "Put Away" : "Maker Pen";
                if (_penLabel.text != want)
                {
                    _penLabel.text = want;
                }
            }
        }

        private void Quick(float x, float y, string place, string illustration, System.Action onClick)
        {
            var rt = RrUiKit.TopLeft(Root, "Quick " + place, x, y, 380f, 130f);
            var b = RrUiKit.PlateButton(rt, RrUiKit.TilePlate, 30f, new Vector4(0f, 0.3f, 0.3f, 0f), RrUiSound.Default);
            var inner = RrUiKit.Fill(rt, "Background", 10f, 10f, 10f, 10f);
            var innerImg = RrUiKit.Rounded(inner, Color.white, 20f);
            RrUiKit.Gradient(innerImg, Color.white, RrUiKit.TileTo, -45f);
            RrUiKit.Icon(RrUiKit.TopLeft(inner, "Illustration", 10f, 5f, 100f, 100f), illustration, Color.white);
            RrUiKit.Label(inner, "Go to", "Go to", 122f, 14f, 190f, 32f, 24f, RrUiKit.Ink);
            RrUiKit.Label(inner, "Place", place, 122f, 44f, 200f, 50f, 32f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            var chevron = RrUiKit.Centered(inner, "Chevron", 142f, 0f, 40f, 40f);
            chevron.localRotation = Quaternion.Euler(0f, 0f, 180f);
            RrUiKit.Icon(chevron, "icon_backArrow", RrUiKit.Ink);
            b.onClick.AddListener(() => onClick());
        }

        public override void Refresh()
        {
            var everyone = RrWatchWorld.Everyone();
            var here = RrWatchWorld.Here;
            _where.text = here == RrActivity.None ? "Arriving" : here == RrActivity.Dorm ? "Your Dorm Room" : RrWatchArt.TitleOf(here);

            // The most played activities, the registry's order breaking ties (and filling the row when empty).
            _order.Clear();
            foreach (var info in RrRooms.All)
            {
                if (info.Activity != RrActivity.Dorm && info.Activity != RrActivity.RecCenter && RrRooms.Listed(info.Activity))
                {
                    _order.Add(info.Activity);
                }
            }

            for (int i = 1; i < _order.Count; i++)
            {
                var a = _order[i];
                int ca = RrWatchWorld.CountIn(a, everyone);
                int j = i - 1;
                while (j >= 0 && RrWatchWorld.CountIn(_order[j], everyone) < ca)
                {
                    _order[j + 1] = _order[j];
                    j--;
                }

                _order[j + 1] = a;
            }

            if (_order.Count > PopularCount)
            {
                _order.RemoveRange(PopularCount, _order.Count - PopularCount);
            }

            string key = string.Join(",", _order);
            if (key != _cardsKey)
            {
                _cardsKey = key;
                foreach (var c in _cards)
                {
                    Object.Destroy(c.Root.gameObject);
                }

                _cards.Clear();
                float x = Margin - RrWatchCard.ArtInset;
                foreach (var a in _order)
                {
                    _cards.Add(RrWatchCard.Make(_popular, a, x, 0f, act => Ui.ShowRoom(act)));
                    x += RrWatchCard.Width;
                }

                RrUiKit.UiLayer(_popular.gameObject);
            }

            foreach (var c in _cards)
            {
                c.SetCount(RrWatchWorld.CountIn(c.Activity, everyone), c.Activity == here);
            }
        }
    }
}
