using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// Room Settings of the maker room the player is in (Rec Room's This Room > Save / Settings / Permissions): the name
    /// (RENAME opens the on-watch keyboard), SAVE (a new version; auto-save runs every 2 min anyway), who can join (Private:
    /// only the owner and co-owners, and the room is not listed for anyone else), who can build (owners, or everyone), the
    /// Maker Pen, and the co-owners: the players here, each with MAKE CO-OWNER / REMOVE for the owner. Every change is sent to
    /// the room and saved as a new version. Visitors see the settings without the controls.
    /// </summary>
    public sealed class RrWatchMakerPage : RrWatchPage
    {
        private const float ColW = 900f;
        private const float RightX = 1020f;
        private const float RowH = 84f;

        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;
        private TextMeshProUGUI _nameValue;
        private TextMeshProUGUI _saveValue;
        private TextMeshProUGUI _note;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _penLabel;
        private RrUiButton _rename;
        private RrUiButton _save;
        private RrUiButton _private;
        private RrUiButton _public;
        private RrUiButton _owners;
        private RrUiButton _everyone;
        private RrUiButton _pen;
        private RectTransform _controls;
        private RectTransform _people;
        private TextMeshProUGUI _nobody;
        private RrWatchKeyboard _keyboard;
        private string _peopleKey = "";
        private float _pollAt;
        private readonly StringBuilder _sb = new StringBuilder();

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.MakerRoom; }
        }

        public override void Build()
        {
            var back = RrUiKit.IconButton(Root, "Back", "icon_backArrow", Margin, 34f, 80f, RrUiKit.Chevron);
            back.onClick.AddListener(() => Ui.Back());
            _title = Title("", Margin + 110f);
            _title.rectTransform.sizeDelta = new Vector2(Width - Margin * 2f - 110f, 80f);
            _subtitle = RrUiKit.Label(Root, "Subtitle", "", Margin + 110f, 110f, Width - 2f * Margin - 110f, 50f, 28f, RrUiKit.InkSoft);

            _controls = RrUiKit.TopLeft(Root, "Controls", 0f, 0f, Width, Height);
            float y = 186f;
            var row = Row("Name", y);
            _nameValue = RrUiKit.Label(row, "Value", "", 200f, 0f, ColW - 460f, RowH, 30f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            _rename = RrUiKit.Secondary(row, "Rename", "Rename", ColW - 230f, 10f, 210f, RowH - 20f, 26f);
            RrWatchMakerUi.OnPage(_rename);
            _rename.onClick.AddListener(OpenRename);
            y += RowH + 12f;

            row = Row("Save", y);
            _saveValue = RrUiKit.Label(row, "Value", "", 200f, 0f, ColW - 460f, RowH, 26f, RrUiKit.InkSoft);
            _save = RrUiKit.Primary(row, "Save", "Save", ColW - 230f, 10f, 210f, RowH - 20f, 28f);
            _save.onClick.AddListener(() =>
            {
                var room = RrMakerRoom.Current;
                if (room != null)
                {
                    room.Save();
                }

                Refresh();
            });
            y += RowH + 12f;

            row = Row("Who can join", y);
            _private = RrWatchMakerUi.Chip(row, "Private", ColW - 470f, 10f, 220f, RowH - 20f);
            _public = RrWatchMakerUi.Chip(row, "Public", ColW - 230f, 10f, 210f, RowH - 20f);
            _private.onClick.AddListener(() => Set(r => r.SetPublic(false)));
            _public.onClick.AddListener(() => Set(r => r.SetPublic(true)));
            y += RowH + 12f;

            row = Row("Who can build", y);
            _owners = RrWatchMakerUi.Chip(row, "Owners", ColW - 470f, 10f, 220f, RowH - 20f);
            _everyone = RrWatchMakerUi.Chip(row, "Everyone", ColW - 230f, 10f, 210f, RowH - 20f);
            _owners.onClick.AddListener(() => Set(r => r.SetEveryoneCanBuild(false)));
            _everyone.onClick.AddListener(() => Set(r => r.SetEveryoneCanBuild(true)));
            y += RowH + 12f;

            row = Row("Maker Pen", y);
            _pen = RrUiKit.Secondary(row, "Pen", "Get the pen", ColW - 330f, 10f, 310f, RowH - 20f, 26f);
            RrWatchMakerUi.OnPage(_pen);
            _penLabel = _pen.Label;
            _pen.onClick.AddListener(() =>
            {
                bool giving = !Interact.MakerPen.RrMakerPen.IsOut;
                Interact.MakerPen.RrMakerPen.Toggle();
                if (giving)
                {
                    RrWatch.Close();
                }
            });
            y += RowH + 20f;

            _note = RrUiKit.Label(Root, "Note", "", Margin, y, ColW, 200f, 26f, RrUiKit.InkSoft, TextAlignmentOptions.TopLeft, false, false, true);
            _status = RrUiKit.Label(Root, "Status", "", Margin, Height - 70f, Width - 2f * Margin, 50f, 28f, RrUiKit.CardTitle);

            RrUiKit.Label(Root, "People Title", "Co-owners", RightX, 186f, 800f, 50f, 32f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            RrUiKit.Label(Root, "People Hint", "Co-owners build, save and change these settings.", RightX, 234f, 840f, 40f, 24f, RrUiKit.InkSoft);
            _people = RrUiKit.TopLeft(Root, "People", RightX, 286f, Width - Margin - RightX, Height - 286f - 90f);
            _nobody = RrUiKit.Label(Root, "Nobody", "", RightX, 300f, Width - Margin - RightX, 200f, 26f, RrUiKit.InkSoft, TextAlignmentOptions.TopLeft, false, false, true);
            _keyboard = RrWatchKeyboard.Build(Root);
        }

        private RectTransform Row(string label, float y)
        {
            var row = RrUiKit.TopLeft(_controls, "Row " + label, Margin, y, ColW, RowH);
            RrUiKit.Rounded(row, Color.white, 20f);
            RrUiKit.Label(row, "Label", label, 30f, 0f, 200f, RowH, 28f, RrUiKit.Ink);
            return row;
        }

        private void Set(System.Func<RrMakerRoom, bool> change)
        {
            var room = RrMakerRoom.Current;
            if (room != null && room.CanSave)
            {
                change(room);
            }

            _peopleKey = "";
            Refresh();
        }

        private void OpenRename()
        {
            var room = RrMakerRoom.Current;
            if (room == null || !room.CanSave)
            {
                return;
            }

            _keyboard.Open("Rename the room", room.RoomInfo.Name, text =>
            {
                var r = RrMakerRoom.Current;
                if (r != null)
                {
                    r.SetName(text);
                }

                Refresh();
            });
        }

        public override void Show()
        {
            _peopleKey = "";
            base.Show();
        }

        public override void Refresh()
        {
            if (_title == null)
            {
                return;
            }

            var room = RrMakerRoom.Current;
            bool inRoom = room != null && room.Built && room.RoomInfo != null && RrTravel.Current == RrActivity.MakerRoom;
            _controls.gameObject.SetActive(inRoom);
            _people.gameObject.SetActive(inRoom);
            if (!inRoom)
            {
                _title.text = "Room Settings";
                _subtitle.text = "You are not in a maker room. Open Rooms or Create to go to one.";
                _note.text = "";
                _nobody.text = "";
                _status.text = "";
                if (_keyboard != null && _keyboard.IsOpen)
                {
                    _keyboard.Close();
                }

                return;
            }

            var info = room.RoomInfo;
            bool owner = room.IsOwner;
            bool can = room.CanSave;
            _title.text = info.Name;
            _subtitle.text = "by " + (owner ? "you" : info.OwnerName) + "  ·  " + RrMakerTemplates.TitleOf(info.Template) + "  ·  instance #" + room.InstanceNumber + "  ·  " + room.ObjectCount + " objects";
            _nameValue.text = info.Name;
            _saveValue.text = "v" + info.Version + ", saved " + RrMakerWire.Ago(info.SavedAt) + (room.Dirty ? "  ·  unsaved changes" : "");
            Enable(_rename, can);
            Enable(_save, can);
            Enable(_private, can);
            Enable(_public, can);
            Enable(_owners, can);
            Enable(_everyone, can);
            RrWatchMakerUi.Select(_private, !info.Public);
            RrWatchMakerUi.Select(_public, info.Public);
            RrWatchMakerUi.Select(_owners, !info.EveryoneCanBuild);
            RrWatchMakerUi.Select(_everyone, info.EveryoneCanBuild);
            _penLabel.text = Interact.MakerPen.RrMakerPen.IsOut ? "PUT THE PEN AWAY" : room.CanEdit ? "GET THE PEN" : "GET THE PEN (DRAW ONLY)";

            _note.text = owner
                ? "This is your room. It is saved on your PC (the last 10 versions are kept) and copied to everyone who visits, so they can open it when you are offline. Private rooms are hidden from everyone but you and your co-owners."
                : can
                    ? "You are a co-owner: you can build, save and change these settings. The owner manages the co-owners."
                    : room.CanEdit
                        ? "Everyone can build here, but only the owner and co-owners save and change settings."
                        : "You are visiting. Only the owner and co-owners build here.";
            string line = Time.realtimeSinceStartup - RrMakerRooms.StatusAt < 20f ? RrMakerRooms.Status : "";
            _status.text = line;
            People(room, owner);
        }

        private static void Enable(RrUiButton b, bool on)
        {
            if (b == null)
            {
                return;
            }

            b.interactable = on;
            var g = b.GetComponent<CanvasGroup>();
            if (g == null)
            {
                g = b.gameObject.AddComponent<CanvasGroup>();
            }

            g.alpha = on ? 1f : 0.45f;
        }

        /// <summary>The players in this room, with their co-owner state (and, for the owner, the buttons).</summary>
        private void People(RrMakerRoom room, bool owner)
        {
            var info = room.RoomInfo;
            var everyone = RrWatchWorld.Everyone();
            var here = new List<KeyValuePair<RrWatchPerson, int>>();
            _sb.Length = 0;
            _sb.Append(owner).Append('|').Append(string.Join(",", info.CoOwners)).Append('|');
            foreach (var p in everyone)
            {
                if (p.IsLocal || !p.WithMe)
                {
                    continue;
                }

                var rec = RrNet.PresenceRecord(p.Id);
                int hash = rec != null && rec.Length > RrPresenceWire.AtUser ? rec[RrPresenceWire.AtUser] : 0;
                var copy = new RrWatchPerson { Id = p.Id, Nick = p.Nick };
                here.Add(new KeyValuePair<RrWatchPerson, int>(copy, hash));
                _sb.Append(p.Id).Append(p.Nick).Append(hash).Append(';');
            }

            string key = _sb.ToString();
            if (key == _peopleKey)
            {
                return;
            }

            _peopleKey = key;
            for (int i = _people.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_people.GetChild(i).gameObject);
            }

            float w = Width - Margin - RightX;
            float y = 0f;
            int presentCo = 0;
            foreach (var kv in here)
            {
                var p = kv.Key;
                int hash = kv.Value;
                bool isOwner = hash != 0 && hash == info.OwnerHash;
                bool co = hash != 0 && info.CoOwners.Contains(hash);
                if (co)
                {
                    presentCo++;
                }

                var row = RrUiKit.TopLeft(_people, "Player " + p.Id, 0f, y, w, 86f);
                RrUiKit.Rounded(row, Color.white, 20f);
                RrUiKit.Label(row, "Name", p.Nick, 26f, 0f, w - 420f, 86f, 28f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
                if (isOwner || co)
                {
                    var pill = RrUiKit.Pill(row, "Role", isOwner ? "Owner" : "Co-owner", w - 400f, 22f, 42f, RrUiKit.Badge, Color.white, 20f);
                    pill.overflowMode = TextOverflowModes.Overflow;
                }

                if (owner && !isOwner && hash != 0)
                {
                    int h = hash;
                    bool on = !co;
                    var b = co
                        ? RrUiKit.NegativeButton(row, "Remove", "Remove", w - 220f, 12f, 200f, 62f, 24f)
                        : RrUiKit.Secondary(row, "Add", "Make co-owner", w - 260f, 12f, 240f, 62f, 22f);
                    if (!co)
                    {
                        RrWatchMakerUi.OnPage(b);
                    }

                    b.onClick.AddListener(() => Set(r => r.SetCoOwner(h, on)));
                }

                y += 96f;
                if (y > _people.rect.height - 90f)
                {
                    break;
                }
            }

            int away = info.CoOwners.Count - presentCo;
            _nobody.gameObject.SetActive(here.Count == 0 || away > 0);
            _nobody.rectTransform.anchoredPosition = new Vector2(RightX, -(286f + y + 10f));
            _nobody.text = (here.Count == 0 ? "Nobody else is here. Players who come in are listed here" + (owner ? ", and you can make them co-owners." : ".") : "") +
                           (away > 0 ? (here.Count == 0 ? "\n" : "") + away + " co-owner" + (away == 1 ? " is" : "s are") + " not here." : "");
            RrUiKit.UiLayer(_people.gameObject);
        }

        public override void Tick()
        {
            if (_keyboard != null)
            {
                _keyboard.Tick();
            }

            if (Time.realtimeSinceStartup >= _pollAt)
            {
                _pollAt = Time.realtimeSinceStartup + 0.5f;
                Refresh();
            }
        }
    }
}
