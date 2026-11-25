using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// The Play screen, Rec Room's (PlayDiscoverySections): titled sections of room cards, one per kind of
    /// activity, for every place registered with <see cref="RrRooms"/> in the registry's order. Rec Room lays
    /// each section out as a sideways carousel; here a section wraps its cards into rows of six (6 x 304 fills
    /// the 1920-unit page), which needs no sideways scrolling with a VR ray. A card opens the room's details.
    /// </summary>
    public sealed class RrWatchPlayPage : RrWatchPage
    {
        private const int PerRow = 6;
        private const float Top = 140f;

        private ScrollRect _scroll;
        private RectTransform _content;
        private TextMeshProUGUI _empty;
        private readonly List<RrWatchCard> _cards = new List<RrWatchCard>();
        private readonly List<string> _categories = new List<string>();
        private string _key = "";

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.Play; }
        }

        public override void Build()
        {
            Title("Play");
            RrUiKit.Label(Root, "Subtitle", "Rec Room Originals", 260f, 50f, 800f, 60f, 32f, RrUiKit.InkSoft);
            _scroll = RrUiKit.Scroll(Root, "Scroll", 0f, Top, Width, Height - Top, out _content);
            _empty = RrUiKit.Label(Root, "Empty", "No activities are available yet.", 0f, 400f, Width, 80f, 36f, RrUiKit.InkSoft, TextAlignmentOptions.Center);
            _empty.gameObject.SetActive(false);
        }

        /// <summary>Section of a room: its registered category, else the kind of game Rec Room filed it under.</summary>
        public static string CategoryOf(RrRoomInfo info)
        {
            if (info.Activity == RrActivity.RecCenter || info.Activity == RrActivity.Dorm)
            {
                return "Hangouts";
            }

            if (!string.IsNullOrEmpty(info.Category) && info.Category != "Games")
            {
                return info.Category;
            }

            var art = RrWatchArt.For(info.Activity);
            return art != null && !string.IsNullOrEmpty(art.Genre) ? art.Genre : "Games";
        }

        public override void Refresh()
        {
            // Rebuild the cards only when the list of rooms changed (other modules register theirs at start).
            var all = RrRooms.All;
            var sb = new System.Text.StringBuilder();
            foreach (var info in all)
            {
                sb.Append((int)info.Activity).Append(',');
            }

            string key = sb.ToString();
            if (key != _key)
            {
                _key = key;
                Rebuild(all);
            }

            var everyone = RrWatchWorld.Everyone();
            var here = RrWatchWorld.Here;
            foreach (var c in _cards)
            {
                c.SetCount(RrWatchWorld.CountIn(c.Activity, everyone), c.Activity == here);
            }
        }

        private void Rebuild(IReadOnlyList<RrRoomInfo> all)
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_content.GetChild(i).gameObject);
            }

            _cards.Clear();
            _categories.Clear();
            foreach (var info in all)
            {
                if (!Shows(info))
                {
                    continue;
                }

                string cat = CategoryOf(info);
                if (!_categories.Contains(cat))
                {
                    _categories.Add(cat);
                }
            }

            // Hangouts first, as the Rec Center leads Rec Room's own lists.
            int hangouts = _categories.IndexOf("Hangouts");
            if (hangouts > 0)
            {
                _categories.RemoveAt(hangouts);
                _categories.Insert(0, "Hangouts");
            }

            float y = 0f;
            foreach (var cat in _categories)
            {
                RrUiKit.Label(_content, "Section " + cat, cat, Margin, y + 6f, 1200f, 60f, 32f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
                y += 70f;
                int n = 0;
                foreach (var info in all)
                {
                    if (!Shows(info) || CategoryOf(info) != cat)
                    {
                        continue;
                    }

                    float x = Margin - RrWatchCard.ArtInset + (n % PerRow) * RrWatchCard.Width;
                    float cy = y + (n / PerRow) * RrWatchCard.Height;
                    _cards.Add(RrWatchCard.Make(_content, info.Activity, x, cy, a => Ui.ShowRoom(a)));
                    n++;
                }

                y += ((n + PerRow - 1) / PerRow) * RrWatchCard.Height + 20f;
            }

            _content.sizeDelta = new Vector2(0f, y + 60f);
            _content.anchoredPosition = Vector2.zero;
            _empty.gameObject.SetActive(_cards.Count == 0);
            RrUiKit.UiLayer(_content.gameObject);
        }

        /// <summary>A card on this page: every registered room but the dorm and the unlisted ones (the maker rooms: Rooms page).</summary>
        private static bool Shows(RrRoomInfo info)
        {
            return info != null && info.Activity != RrActivity.Dorm && info.Create != null && !info.Unlisted;
        }

        public override void Show()
        {
            base.Show();
            _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
