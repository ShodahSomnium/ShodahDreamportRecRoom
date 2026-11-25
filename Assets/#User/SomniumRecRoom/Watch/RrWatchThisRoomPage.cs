using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// The "This Room" screen (Rec Room's ThisRoomScreen): the place the player is in - its picture, name and
    /// instance - the players in the same instance, and the ways out: "Leave" (red, Rec Room's negative
    /// button) back to the player's Dorm Room, and "Go to Rec Center".
    /// </summary>
    public sealed class RrWatchThisRoomPage : RrWatchPage
    {
        private const float ArtW = 640f;
        private const float ArtH = 360f;
        private const float ListX = Margin + ArtW + 60f;
        private const float ListW = Width - Margin - ListX - 30f;

        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;
        private RectTransform _art;
        private RrUiGradient _artBack;
        private ScrollRect _scroll;
        private RectTransform _content;
        private RrUiButton _leave;
        private RrUiButton _recCenter;
        private RrUiButton _makerSettings;
        private RrActivity _shownActivity = (RrActivity)255;
        private string _key = "";
        private readonly StringBuilder _sb = new StringBuilder();

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.ThisRoom; }
        }

        public override void Build()
        {
            _title = Title("");
            _title.rectTransform.sizeDelta = new Vector2(Width - Margin * 2f, 80f);
            _subtitle = RrUiKit.Label(Root, "Subtitle", "", Margin, 110f, 1400f, 50f, 30f, RrUiKit.InkSoft);

            _art = RrUiKit.TopLeft(Root, "Art", Margin, 180f, ArtW, ArtH);
            var back = RrUiKit.Rounded(_art, Color.white, 20f);
            _artBack = RrUiKit.Gradient(back, Color.white, Color.white, -90f);
            _art.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _leave = RrUiKit.NegativeButton(Root, "Leave", "Leave", Margin, 180f + ArtH + 40f, ArtW, 100f, 36f);
            _leave.onClick.AddListener(() => Ui.Travel(RrActivity.Dorm, -1));
            _recCenter = RrUiKit.Secondary(Root, "Rec Center", "Go to Rec Center", Margin, 180f + ArtH + 160f, ArtW, 90f, 30f);
            _recCenter.onClick.AddListener(() => Ui.Travel(RrActivity.RecCenter, -1));

            // Player-made rooms (Places/MakerRooms): save, privacy, builders, co-owners, rename.
            _makerSettings = RrUiKit.Primary(Root, "Room Settings", "Room Settings", Margin, 180f + ArtH + 270f, ArtW, 90f, 30f);
            _makerSettings.onClick.AddListener(() => Ui.ShowPage(RrWatchPageId.MakerRoom));

            RrUiKit.Label(Root, "Players Title", "Players here", ListX, 180f, 800f, 50f, 32f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            _scroll = RrUiKit.Scroll(Root, "Players", ListX - 10f, 240f, ListW + 40f, Height - 240f, out _content);
        }

        public override void Show()
        {
            _key = "";
            base.Show();
        }

        public override void Refresh()
        {
            var here = RrWatchWorld.Here;
            int instance = RrWatchWorld.HereInstance;
            if (here != _shownActivity)
            {
                _shownActivity = here;
                var info = RrRooms.Get(here);
                var art = RrWatchArt.For(here, info);
                for (int i = _art.childCount - 1; i >= 0; i--)
                {
                    Object.Destroy(_art.GetChild(i).gameObject);
                }

                _artBack.Set(art.BackFrom, art.BackTo);
                if (here != RrActivity.None)
                {
                    RrWatchCard.Picture(_art, art, 30f);
                }
                else
                {
                    RrUiKit.Icon(RrUiKit.Fill(_art, "Logo", 60f, 40f, 60f, 40f), "Illustration_RecRoomLogo", Color.white);
                }

                RrUiKit.UiLayer(_art.gameObject);
            }

            _title.text = here == RrActivity.None ? "Dreamport: Rec Room" : here == RrActivity.Dorm ? "Your Dorm Room" : RrWatchArt.TitleOf(here);
            var everyone = RrWatchWorld.Everyone();
            int with = 0;
            foreach (var p in everyone)
            {
                if (p.WithMe)
                {
                    with++;
                }
            }

            string who = with == 1 ? "just you" : with + " players";
            _subtitle.text = here == RrActivity.Dorm ? "Private room  ·  " + who
                : here == RrActivity.None ? "The arrival room"
                : (instance > 0 ? "Instance #" + instance + "  ·  " : "") + who;

            bool busy = RrTravel.Busy;
            _leave.gameObject.SetActive(here != RrActivity.Dorm);
            _leave.interactable = !busy;
            _recCenter.gameObject.SetActive(here != RrActivity.RecCenter);
            _recCenter.interactable = !busy;
            _makerSettings.gameObject.SetActive(here == RrActivity.MakerRoom);
            _makerSettings.interactable = !busy;
            var rc = (RectTransform)_recCenter.transform;
            rc.anchoredPosition = new Vector2(rc.anchoredPosition.x, here == RrActivity.Dorm ? -(180f + ArtH + 40f) - 45f : -(180f + ArtH + 160f) - 45f);

            _sb.Length = 0;
            foreach (var p in everyone)
            {
                if (p.WithMe)
                {
                    _sb.Append(p.Id).Append(p.Nick).Append(p.Team).Append(';');
                }
            }

            string key = _sb.ToString();
            if (key == _key)
            {
                return;
            }

            _key = key;
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_content.GetChild(i).gameObject);
            }

            float y = 0f;
            foreach (var p in everyone)
            {
                if (!p.WithMe)
                {
                    continue;
                }

                var row = RrUiKit.TopLeft(_content, "Player " + p.Id, 10f, y, ListW, 90f);
                RrUiKit.Rounded(row, Color.white, 20f);
                var photo = RrUiKit.TopLeft(row, "Photo", 14f, 10f, 70f, 70f);
                RrUiKit.Circle(photo, p.Team == 1 ? RrLook.TeamBlue : p.Team == 2 ? RrLook.TeamRed : RrUiKit.ProfileBack);
                RrUiKit.Icon(RrUiKit.Fill(photo, "Face", 12f, 12f, 12f, 12f), "icon_ProfilePic", Color.white);
                RrUiKit.Label(row, "Name", p.Nick, 104f, 0f, ListW - 300f, 90f, 30f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
                if (p.IsLocal)
                {
                    var you = RrUiKit.Pill(row, "You", "You", ListW - 130f, 23f, 44f, RrUiKit.Badge, Color.white, 24f);
                    you.overflowMode = TextOverflowModes.Overflow;
                }

                y += 104f;
            }

            _content.sizeDelta = new Vector2(0f, y + 40f);
            RrUiKit.UiLayer(_content.gameObject);
        }
    }
}
