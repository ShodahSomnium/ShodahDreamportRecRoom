using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// The People screen: everyone in the world, "In this room" (the local player's instance) first and then
    /// "Elsewhere in Dreamport", each with where they are, in Rec Room's player-list style (white rounded rows,
    /// a round profile picture on #B2A8A2, the name in bold, a teal pill for yourself). A player somewhere this
    /// client may join gets an orange "Join", which travels to their activity and instance. Dorm rooms are
    /// private, as in Rec Room: no Join there.
    /// </summary>
    public sealed class RrWatchPeoplePage : RrWatchPage
    {
        private const float Top = 140f;
        private const float RowHeight = 110f;
        private const float RowWidth = Width - Margin * 2f - 30f;

        private TextMeshProUGUI _count;
        private ScrollRect _scroll;
        private RectTransform _content;
        private string _key = "";
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly List<RrWatchPerson> _with = new List<RrWatchPerson>();
        private readonly List<RrWatchPerson> _elsewhere = new List<RrWatchPerson>();

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.People; }
        }

        public override void Build()
        {
            Title("People");
            _count = RrUiKit.Label(Root, "Count", "", 330f, 50f, 900f, 60f, 32f, RrUiKit.InkSoft);
            _scroll = RrUiKit.Scroll(Root, "Scroll", 0f, Top, Width, Height - Top, out _content);
        }

        public override void Show()
        {
            _key = "";
            base.Show();
            _scroll.verticalNormalizedPosition = 1f;
        }

        public override void Refresh()
        {
            var everyone = RrWatchWorld.Everyone();
            _sb.Length = 0;
            foreach (var p in everyone)
            {
                _sb.Append(p.Id).Append(p.Nick).Append((int)p.Activity).Append('/').Append(p.Instance).Append('/').Append((int)p.Status).Append(p.WithMe ? 'w' : '-').Append(RrTravel.Busy ? 'b' : '-').Append(';');
            }

            string key = _sb.ToString();
            if (key == _key)
            {
                return;
            }

            _key = key;
            _count.text = everyone.Count == 1 ? "1 person in Dreamport" : everyone.Count + " people in Dreamport";
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_content.GetChild(i).gameObject);
            }

            _with.Clear();
            _elsewhere.Clear();
            foreach (var p in everyone)
            {
                (p.WithMe ? _with : _elsewhere).Add(p);
            }

            float y = 0f;
            y = Section("In this room", _with, y);
            if (_elsewhere.Count > 0)
            {
                y = Section("Elsewhere in Dreamport", _elsewhere, y + 20f);
            }

            _content.sizeDelta = new Vector2(0f, y + 60f);
            RrUiKit.UiLayer(_content.gameObject);
        }

        private float Section(string title, List<RrWatchPerson> people, float y)
        {
            RrUiKit.Label(_content, "Section " + title, title, Margin, y + 6f, 1200f, 60f, 32f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            y += 76f;
            foreach (var p in people)
            {
                Row(p, y);
                y += RowHeight + 14f;
            }

            return y;
        }

        private void Row(RrWatchPerson p, float y)
        {
            var row = RrUiKit.TopLeft(_content, "Player " + p.Id, Margin, y, RowWidth, RowHeight);
            RrUiKit.Rounded(row, Color.white, 20f);

            // Profile picture: Rec Room's placeholder face on the grey backdrop, in a circle of radius 50.
            var photo = RrUiKit.TopLeft(row, "Photo", 16f, 12f, 86f, 86f);
            RrUiKit.Circle(photo, RrUiKit.ProfileBack);
            RrUiKit.Icon(RrUiKit.Fill(photo, "Face", 14f, 14f, 14f, 14f), "icon_ProfilePic", Color.white);
            if (p.InVr)
            {
                var vr = RrUiKit.Pill(row, "VR", "VR", 70f, 66f, 34f, RrUiKit.Ink, Color.white, 18f);
                vr.overflowMode = TextOverflowModes.Overflow;
            }

            RrUiKit.Label(row, "Name", p.Nick, 128f, 12f, RowWidth - 560f, 50f, 32f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            RrUiKit.Label(row, "Where", RrWatchWorld.Where(p), 128f, 58f, RowWidth - 560f, 40f, 26f, RrUiKit.InkSoft);

            if (p.IsLocal)
            {
                var you = RrUiKit.Pill(row, "You", "You", RowWidth - 150f, 33f, 44f, RrUiKit.Badge, Color.white, 24f);
                you.overflowMode = TextOverflowModes.Overflow;
            }
            else if (p.WithMe)
            {
                var here = RrUiKit.Pill(row, "Here", "Here with you", RowWidth - 240f, 33f, 44f, RrUiKit.Badge, Color.white, 22f);
                here.overflowMode = TextOverflowModes.Overflow;
            }
            else if (RrWatchWorld.CanJoin(p))
            {
                var activity = p.Activity;
                int instance = p.Instance;
                var join = RrUiKit.Primary(row, "Join", "Join", RowWidth - 236f, 20f, 210f, 70f, 28f);
                join.interactable = !RrTravel.Busy;
                join.onClick.AddListener(() => Ui.Travel(activity, instance));
            }
        }
    }
}
