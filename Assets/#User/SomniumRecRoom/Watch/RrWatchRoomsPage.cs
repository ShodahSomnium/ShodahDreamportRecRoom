using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// Rooms: the player-made rooms this player knows of, from the local catalogue (<see cref="RrMakerCatalog"/>, filled by
    /// every player's announcements): MY ROOMS (owned or co-owned) and PUBLIC ROOMS (everyone else's public ones; private
    /// rooms are not listed for others). Each row: the name, owner, base room, version and last save, who is in it now, JOIN
    /// (downloads the newest version from a player who holds it when this PC has none or an older one) and, for the owner,
    /// DELETE (tap twice). The room the player is in shows SETTINGS instead of JOIN. The status line follows downloads.
    /// </summary>
    public sealed class RrWatchRoomsPage : RrWatchPage
    {
        private const float Top = 210f;
        private const float RowH = 120f;

        private bool _public;
        private RrUiButton _mineTab;
        private RrUiButton _publicTab;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _empty;
        private ScrollRect _scroll;
        private RectTransform _content;
        private string _key = "";
        private string _confirm;
        private float _confirmUntil;
        private bool _closeOnTrip;
        private float _closeSince;
        private float _pollAt;
        private readonly StringBuilder _sb = new StringBuilder();

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.Rooms; }
        }

        public override void Build()
        {
            Title("Rooms");
            _mineTab = RrWatchMakerUi.Chip(Root, "My Rooms", 300f, 44f, 260f);
            _publicTab = RrWatchMakerUi.Chip(Root, "Public Rooms", 580f, 44f, 300f);
            _mineTab.onClick.AddListener(() => SetTab(false));
            _publicTab.onClick.AddListener(() => SetTab(true));
            var create = RrUiKit.Primary(Root, "Create", "Create", Width - Margin - 300f, 40f, 300f, 76f, 30f);
            create.onClick.AddListener(() => Ui.ShowPage(RrWatchPageId.Create));
            _status = RrUiKit.Label(Root, "Status", "", Margin, 136f, Width - 2f * Margin, 50f, 28f, RrUiKit.CardTitle);
            _scroll = RrUiKit.Scroll(Root, "List", 0f, Top, Width, Height - Top, out _content);
            _empty = RrUiKit.Label(Root, "Empty", "", Margin, 420f, Width - 2f * Margin, 140f, 32f, RrUiKit.InkSoft, TextAlignmentOptions.Center, false, false, true);
            SetTab(false);
        }

        private void SetTab(bool pub)
        {
            _public = pub;
            RrWatchMakerUi.Select(_mineTab, !pub);
            RrWatchMakerUi.Select(_publicTab, pub);
            _key = "";
            _confirm = null;
            Refresh();
            if (_scroll != null)
            {
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        public override void Show()
        {
            _key = "";
            base.Show();
        }

        public override void Refresh()
        {
            if (_content == null)
            {
                return;
            }

            string line = RrMakerRooms.DownloadLine();
            if (line == null && Time.realtimeSinceStartup - RrMakerRooms.StatusAt < 20f)
            {
                line = RrMakerRooms.Status;
            }

            _status.text = line ?? "";

            var rooms = _public ? RrMakerRooms.PublicRooms() : RrMakerRooms.MyRooms();
            var here = RrMakerRoom.Current != null && RrTravel.Current == RrActivity.MakerRoom ? RrMakerRoom.Current.RoomId : "";
            _sb.Length = 0;
            _sb.Append(_public).Append('|').Append(RrMakerRooms.Serial).Append('|').Append(here).Append('|').Append(_confirm).Append('|').Append(RrTravel.Busy).Append('|');
            var counts = new Dictionary<string, int>();
            foreach (var r in rooms)
            {
                int n = RrMakerRooms.PlayersIn(r.Info.Id).Count;
                counts[r.Info.Id] = n;
                _sb.Append(r.Info.Id, 0, 6).Append(n).Append(RrMakerRooms.CannotOpen(r) == null ? 'y' : 'n').Append(',');
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

            _empty.gameObject.SetActive(rooms.Count == 0);
            _empty.text = _public
                ? "No public rooms known yet. Rooms appear here when players who hold them are in the world (and stay listed afterwards)."
                : "You have no rooms yet. Open Create to make one on a base room.";

            float y = 0f;
            float w = Width - 2f * Margin;
            foreach (var r in rooms)
            {
                Row(r, y, w, counts[r.Info.Id], r.Info.Id == here);
                y += RowH + 14f;
            }

            _content.sizeDelta = new Vector2(0f, y + 40f);
            RrUiKit.UiLayer(_content.gameObject);
        }

        private void Row(RrMakerKnownRoom r, float y, float w, int playing, bool here)
        {
            var info = r.Info;
            string id = info.Id;
            var row = RrUiKit.TopLeft(_content, "Room " + id, Margin, y, w, RowH);
            RrUiKit.Rounded(row, Color.white, 20f);
            var t = RrMakerTemplates.Get(info.Template);
            var thumb = RrUiKit.TopLeft(row, "Art", 14f, 12f, 170f, 96f);
            var art = t != null ? RrWatchCreatePage.ArtOf(t) : new RrActivityArt { BackFrom = RrUiKit.PrimaryFrom, BackTo = RrUiKit.PrimaryTo };
            var back = RrUiKit.Rounded(thumb, Color.white, 14f);
            RrUiKit.Gradient(back, art.BackFrom, art.BackTo, -90f);
            thumb.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RrWatchCard.Picture(thumb, art, 6f);

            RrUiKit.Label(row, "Name", info.Name, 206f, 10f, w - 1000f, 54f, 34f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            bool mine = RrMakerRooms.IsMine(info);
            string owner = mine ? "you" : string.IsNullOrEmpty(info.OwnerName) ? "someone" : info.OwnerName;
            string held = r.HeldVersion > 0 ? (r.HeldVersion < RrMakerCatalog.LatestVersion(r) ? "  ·  you have v" + r.HeldVersion : "") : "  ·  not on this PC";
            string detail = "by " + owner + "  ·  " + RrMakerTemplates.TitleOf(info.Template) + "  ·  v" + info.Version + ", saved " + RrMakerWire.Ago(info.SavedAt) +
                            (info.Public ? "" : "  ·  private") + (info.EveryoneCanBuild ? "  ·  everyone builds" : "") + held;
            RrUiKit.Label(row, "Detail", detail, 206f, 64f, w - 1000f, 44f, 24f, RrUiKit.InkSoft);

            float right = w - 24f;
            if (here)
            {
                var settings = RrUiKit.Secondary(row, "Settings", "Settings", right - 250f, 24f, 250f, 72f, 26f);
                RrWatchMakerUi.OnPage(settings);
                settings.onClick.AddListener(() => Ui.ShowPage(RrWatchPageId.MakerRoom));
                right -= 270f;
                var pill = RrUiKit.Pill(row, "Here", "You are here", right - 190f, 38f, 44f, RrUiKit.PrimaryTo, Color.white, 22f);
                pill.overflowMode = TextOverflowModes.Overflow;
                right -= 210f;
            }
            else
            {
                string why = RrMakerRooms.CannotOpen(r);
                var join = RrUiKit.Secondary(row, "Join", why == null ? (playing > 0 ? "Join" : "Go") : why, right - 300f, 24f, 300f, 72f, why == null ? 30f : 20f);
                RrWatchMakerUi.OnPage(join);
                join.interactable = why == null && !RrTravel.Busy && !RrMakerRooms.Opening;
                join.onClick.AddListener(() => Join(id));
                right -= 320f;
            }

            if (mine && !here)
            {
                bool sure = _confirm == id && Time.realtimeSinceStartup < _confirmUntil;
                var del = RrUiKit.NegativeButton(row, "Delete", sure ? "Sure?" : "Delete", right - 190f, 24f, 190f, 72f, 26f);
                del.onClick.AddListener(() => Delete(id));
                right -= 210f;
            }
            else if (!mine && !here && r.HeldVersion > 0 && !_public)
            {
                var drop = RrUiKit.Secondary(row, "Remove", "Remove copy", right - 230f, 24f, 230f, 72f, 22f);
                RrWatchMakerUi.OnPage(drop);
                drop.onClick.AddListener(() => RrMakerRooms.Delete(id));
                right -= 250f;
            }

            if (playing > 0 && !here)
            {
                var pill = RrUiKit.Pill(row, "Playing", playing == 1 ? "1 here now" : playing + " here now", right - 170f, 38f, 44f, RrUiKit.Badge, Color.white, 22f);
                pill.overflowMode = TextOverflowModes.Overflow;
            }
        }

        private void Join(string id)
        {
            if (RrMakerRooms.Open(id))
            {
                _closeOnTrip = true;
                _closeSince = Time.realtimeSinceStartup;
            }

            _key = "";
            Refresh();
        }

        private void Delete(string id)
        {
            if (_confirm == id && Time.realtimeSinceStartup < _confirmUntil)
            {
                _confirm = null;
                RrMakerRooms.Delete(id);
            }
            else
            {
                _confirm = id;
                _confirmUntil = Time.realtimeSinceStartup + 4f;
            }

            _key = "";
            Refresh();
        }

        public override void Tick()
        {
            // The trip starts once the room is downloaded: the menu goes with it.
            if (_closeOnTrip)
            {
                if (RrTravel.Busy)
                {
                    _closeOnTrip = false;
                    RrWatch.Close();
                    return;
                }

                if (!RrMakerRooms.Opening && Time.realtimeSinceStartup - _closeSince > 2f)
                {
                    _closeOnTrip = false;
                }
            }

            if (_confirm != null && Time.realtimeSinceStartup >= _confirmUntil)
            {
                _confirm = null;
                _key = "";
            }

            if (Time.realtimeSinceStartup >= _pollAt)
            {
                _pollAt = Time.realtimeSinceStartup + 0.25f;
                Refresh();
            }
        }
    }

    /// <summary>Small pieces shared by the maker-room pages: chips (Rec Room's two-choice buttons) and on-page button fills.</summary>
    public static class RrWatchMakerUi
    {
        /// <summary>A chip button (white with orange text; selected: orange with white text), as the Settings page's choices.</summary>
        public static RrUiButton Chip(Transform parent, string text, float x, float y, float w, float h = 76f)
        {
            var rt = RrUiKit.TopLeft(parent, "Chip " + text, x, y, w, h);
            var img = RrUiKit.Rounded(RrUiKit.Fill(rt, "Background"), Color.white, 100f, true);
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Fill = img;
            b.Sound = RrUiSound.Default;
            b.Label = RrUiKit.Text(RrUiKit.Fill(rt, "Label", 12f, 0f, 12f, 0f), text.ToUpperInvariant(), 24f, RrUiKit.SecondaryText, TextAlignmentOptions.Center, true);
            Select(b, false);
            return b;
        }

        public static void Select(RrUiButton b, bool selected)
        {
            if (b == null)
            {
                return;
            }

            b.FillStates = selected
                ? new[] { RrUiKit.PrimaryTo, RrUiKit.PrimaryHoverTo, RrUiKit.PrimaryPressedTo }
                : new[] { Color.white, RrUiKit.SecondaryHover, RrUiKit.SecondaryPressed };
            if (b.Label != null)
            {
                b.Label.color = selected ? Color.white : RrUiKit.SecondaryText;
            }

            b.Refresh();
        }

        /// <summary>A secondary button sitting on a white row: cream fill so it shows (as the room page's Join).</summary>
        public static void OnPage(RrUiButton b)
        {
            if (b == null)
            {
                return;
            }

            ((Image)b.targetGraphic).color = RrUiKit.Page;
            b.FillStates = new[] { RrUiKit.Page, RrUiKit.SecondaryHover, RrUiKit.SecondaryPressed };
            b.Refresh();
        }
    }
}
