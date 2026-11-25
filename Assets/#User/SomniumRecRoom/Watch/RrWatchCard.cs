using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// A room card of the Play and Home pages, Rec Room's (Card_Room) (304 x 290 canvas units, measured):
    /// a transparent card whose picture is inset 16 units, 16:9 (272 x 153) with corners of radius 16; the title
    /// below in bold #AC4306, a line of genre and player range, and a "n playing" pill over the picture. On
    /// hover the card grows to 1.03x and an orange outline (#E15A0C, Border_Weight4_Radius20) appears; the card
    /// sounds are the Card audio type.
    /// </summary>
    public sealed class RrWatchCard
    {
        public const float Width = 304f;
        public const float Height = 290f;
        public const float ArtInset = 16f;
        public const float ArtWidth = 272f;
        public const float ArtHeight = 153f;

        public RrActivity Activity;
        public RectTransform Root;
        public RrUiButton Button;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Subtitle;
        private TextMeshProUGUI _badge;
        private RectTransform _badgeRoot;
        private int _shownCount = -1;
        private bool _shownHere;

        /// <summary>Builds a card at (x, y) from the parent's top-left.</summary>
        public static RrWatchCard Make(Transform parent, RrActivity activity, float x, float y, System.Action<RrActivity> onClick)
        {
            var info = RrRooms.Get(activity);
            var art = RrWatchArt.For(activity, info);
            string title = info != null && !string.IsNullOrEmpty(info.Title) ? info.Title : art.Title;
            return Make(parent, activity, x, y, onClick, art, title, SubtitleOf(activity, info, art), "Card " + activity);
        }

        /// <summary>A card with its own picture and lines (a player-made room on a door list); onClick gets <paramref name="activity"/>.</summary>
        public static RrWatchCard Make(Transform parent, RrActivity activity, float x, float y, System.Action<RrActivity> onClick, RrActivityArt art, string title, string subtitle, string name)
        {
            var card = new RrWatchCard { Activity = activity };
            var root = RrUiKit.TopLeft(parent, name, x, y, Width, Height);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(x + Width * 0.5f, -y - Height * 0.5f);
            card.Root = root;

            // The whole card is the hit area; it has no visible background of its own.
            var hit = RrUiKit.Rounded(RrUiKit.Fill(root, "Hit"), new Color(1f, 1f, 1f, 0f), 20f, true);
            var b = root.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = hit;
            b.Sound = RrUiSound.Card;
            b.HoverScale = 1.03f;
            card.Button = b;

            // Picture: rounded mask over the activity's background colours, the art inside.
            var frame = RrUiKit.TopLeft(root, "Art", ArtInset, ArtInset, ArtWidth, ArtHeight);
            var back = RrUiKit.Rounded(frame, Color.white, 16f);
            RrUiKit.Gradient(back, art.BackFrom, art.BackTo, -90f);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Picture(frame, art, 10f);

            var outline = RrUiKit.Fill(root, "Focus", ArtInset - 6f, ArtInset - 6f, ArtInset - 6f, Height - ArtInset - ArtHeight - 6f);
            var outlineImg = outline.gameObject.AddComponent<Image>();
            outlineImg.color = RrUiKit.Focus;
            outlineImg.type = Image.Type.Sliced;
            outlineImg.raycastTarget = false;
            outlineImg.enabled = false;
            RrWatchArt.Get("Border_Weight4_Radius20", s =>
            {
                // Without its sprite the outline would be a solid orange block: only use it once it has one.
                if (outlineImg != null && s != null)
                {
                    outlineImg.sprite = s;
                    b.Outline = outlineImg;
                }
            });

            card.Title = RrUiKit.Label(root, "Title", title ?? "", ArtInset, ArtInset + ArtHeight + 8f, ArtWidth, 48f, 32f, RrUiKit.CardTitle, TextAlignmentOptions.MidlineLeft, true);
            card.Title.characterSpacing = -0.25f;
            card.Title.enableAutoSizing = true;
            card.Title.fontSizeMin = 22f;
            card.Title.fontSizeMax = 32f;
            card.Subtitle = RrUiKit.Label(root, "Subtitle", subtitle ?? "", ArtInset, ArtInset + ArtHeight + 56f, ArtWidth, 40f, 24f, RrUiKit.Ink);

            b.Refresh();
            if (onClick != null)
            {
                b.onClick.AddListener(() => onClick(activity));
            }

            return card;
        }

        /// <summary>
        /// Draws an activity's picture into a frame: covering it (16:9 renders, gift-box prints) or fitted inside
        /// with a margin (logos, stickers). Bowling shows its pin and ball side by side.
        /// </summary>
        public static void Picture(RectTransform frame, RrActivityArt art, float margin)
        {
            if (art == null || string.IsNullOrEmpty(art.Asset))
            {
                return;
            }

            if (!string.IsNullOrEmpty(art.Asset2))
            {
                var left = RrUiKit.Node(frame, "Picture");
                left.anchorMin = new Vector2(0f, 0f);
                left.anchorMax = new Vector2(0.5f, 1f);
                left.offsetMin = new Vector2(margin, margin);
                left.offsetMax = new Vector2(-margin * 0.5f, -margin);
                RrUiKit.Icon(left, art.Asset, art.Tint);
                var right = RrUiKit.Node(frame, "Picture 2");
                right.anchorMin = new Vector2(0.5f, 0f);
                right.anchorMax = new Vector2(1f, 1f);
                right.offsetMin = new Vector2(margin * 0.5f, margin);
                right.offsetMax = new Vector2(-margin, -margin);
                RrUiKit.Icon(right, art.Asset2, art.Tint);
                return;
            }

            if (art.Fit == RrArtFit.Fit)
            {
                var fit = RrUiKit.Fill(frame, "Picture", margin, margin, margin, margin);
                var img = fit.gameObject.AddComponent<Image>();
                img.color = art.Tint;
                img.preserveAspect = true;
                img.raycastTarget = false;
                RrWatchArt.Apply(img, art.Asset, art.Crop);
                return;
            }

            var cover = RrUiKit.Centered(frame, "Picture", 0f, 0f, 100f, 100f);
            var coverImg = cover.gameObject.AddComponent<Image>();
            coverImg.color = art.Tint;
            coverImg.raycastTarget = false;
            coverImg.enabled = false;
            var fitter = cover.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            RrWatchArt.Get(art.Asset, s =>
            {
                if (coverImg == null)
                {
                    return;
                }

                coverImg.sprite = s;
                coverImg.enabled = s != null;
                if (s != null && s.rect.height > 0f)
                {
                    fitter.aspectRatio = s.rect.width / s.rect.height;
                }
            }, art.Crop);
        }

        public static string SubtitleOf(RrActivity activity, RrRoomInfo info, RrActivityArt art)
        {
            if (info != null && !string.IsNullOrEmpty(info.Subtitle))
            {
                return info.Subtitle;
            }

            string genre = info != null && !string.IsNullOrEmpty(info.Category) && info.Category != "Games" ? info.Category : art.Genre;
            if (activity == RrActivity.RecCenter || activity == RrActivity.Dorm)
            {
                return genre;
            }

            int min = info != null ? info.MinPlayers : 1;
            int max = info != null ? info.MaxPlayers : 8;
            string players = min == max ? max + " players" : min + "-" + max + " players";
            return string.IsNullOrEmpty(genre) ? players : genre + "  ·  " + players;
        }

        /// <summary>Shows how many are playing and whether the local player is there.</summary>
        public void SetCount(int count, bool here)
        {
            if (count == _shownCount && here == _shownHere)
            {
                return;
            }

            _shownCount = count;
            _shownHere = here;
            if (_badgeRoot == null)
            {
                _badge = RrUiKit.Pill(Root, "Badge", "", ArtInset + 10f, ArtInset + 10f, 40f, RrUiKit.Badge, Color.white, 22f);
                _badgeRoot = (RectTransform)_badge.transform.parent;
            }

            bool show = here || count > 0;
            _badgeRoot.gameObject.SetActive(show);
            if (show)
            {
                _badge.text = here ? "You are here" : count == 1 ? "1 playing" : count + " playing";
                ((Image)_badgeRoot.GetComponent<Image>()).color = here ? RrUiKit.PrimaryTo : RrUiKit.Badge;
            }
        }
    }
}
