using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// A room's details, opened from a card as in Rec Room (the room details sub-page): the big 16:9 picture,
    /// title, kind and player range, the description, who is playing it now in which instance (each with its
    /// own "Join"), and the orange primary "Go" that joins the fullest open instance (Rec Room's matchmaking,
    /// done by <see cref="RrTravel.GoTo"/> with instance -1).
    /// </summary>
    public sealed class RrWatchRoomPage : RrWatchPage
    {
        private const float ArtX = Margin;
        private const float ArtY = 150f;
        private const float ArtW = 960f;
        private const float ArtH = 540f;
        private const float ColX = ArtX + ArtW + 60f;
        private const float ColW = Width - Margin - ColX;

        private RrActivity _activity;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _kind;
        private TextMeshProUGUI _description;
        private TextMeshProUGUI _playing;
        private RectTransform _art;
        private RrUiGradient _artBack;
        private RectTransform _instances;
        private RrUiButton _go;
        private CanvasGroup _goGroup;
        private string _instancesKey = "";

        private static readonly Dictionary<RrActivity, string> Blurbs = new Dictionary<RrActivity, string>
        {
            { RrActivity.RecCenter, "The heart of Rec Room: hang out, shoot hoops, play pool and find your next game." },
            { RrActivity.Dorm, "Your own room. Nobody comes in unless you invite them." },
            { RrActivity.Charades, "Draw it in 3D and let your team guess before the time runs out." },
            { RrActivity.Bowling, "Ten pins, one ball and a lane full of friends." },
            { RrActivity.Crescendo, "A quest in Count Dracula's castle: survive until the Blood Moon sets." },
            { RrActivity.DiscGolf, "Throw your disc across the course and into the basket in as few throws as you can." },
            { RrActivity.Dodgeball, "Two teams, a pile of balls, one rule: don't get hit." },
            { RrActivity.LostSkulls, "Sail to a cursed island, fight skeleton pirates and face Ghostbeard." },
            { RrActivity.LaserTag, "Team laser battles in the dark." },
            { RrActivity.Midnight, "Hide from Bonky the clown and make it to midnight." },
            { RrActivity.LittleMonsters, "Raise your own little monster in the woods of Cryptid Creek." },
            { RrActivity.Paddleball, "Fast one-on-one paddle duels." },
            { RrActivity.Paintball, "Rec Room's classic team shooter: team battle and capture the flag." },
            { RrActivity.PunchUp, "Put on the gloves and step into the ring." },
            { RrActivity.GoldenTrophy, "The first quest: fight through the goblin kingdom to win the Golden Trophy." },
            { RrActivity.RecRally, "Race buggies with your friends around rally tracks." },
            { RrActivity.RecRoyale, "Drop in, gear up and be the last squad standing." },
            { RrActivity.Showdown, "Three against three in the Wild West town of Sarsaparilla Springs." },
            { RrActivity.Soccer, "Kick, pass and score with your whole team." },
            { RrActivity.StuntRunner, "Run, slide and wall-run through the obstacle course." },
            { RrActivity.CrimsonCauldron, "A quest for a witch's cursed cauldron, deep in a haunted forest." },
            { RrActivity.Jumbotron, "Stop the robot Jumbotron and its army in the sci-fi quest." }
        };

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.Room; }
        }

        public override void Build()
        {
            var back = RrUiKit.IconButton(Root, "Back", "icon_backArrow", Margin, 34f, 80f, RrUiKit.Chevron);
            back.onClick.AddListener(() => Ui.Back());
            _title = Title("", Margin + 110f);
            _title.rectTransform.sizeDelta = new Vector2(Width - Margin * 2f - 110f, 80f);

            _art = RrUiKit.TopLeft(Root, "Art", ArtX, ArtY, ArtW, ArtH);
            var back2 = RrUiKit.Rounded(_art, Color.white, 20f);
            _artBack = RrUiKit.Gradient(back2, Color.white, Color.white, -90f);
            _art.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _kind = RrUiKit.Label(Root, "Kind", "", ColX, ArtY, ColW, 50f, 32f, RrUiKit.InkSoft);
            _description = RrUiKit.Label(Root, "Description", "", ColX, ArtY + 70f, ColW, 200f, 30f, RrUiKit.Ink, TextAlignmentOptions.TopLeft, false, false, true);
            _playing = RrUiKit.Label(Root, "Playing", "", ColX, ArtY + 290f, ColW, 50f, 30f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            _instances = RrUiKit.TopLeft(Root, "Instances", ColX, ArtY + 350f, ColW, 180f);

            _go = RrUiKit.Primary(Root, "Go", "Go", ColX, ArtY + ArtH - 110f, ColW, 110f, 44f);
            _go.onClick.AddListener(() => Ui.Travel(_activity, -1));
            _goGroup = _go.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>Shows a room (called before the page is shown).</summary>
        public void Set(RrActivity activity)
        {
            _activity = activity;
            var info = RrRooms.Get(activity);
            var art = RrWatchArt.For(activity, info);
            _title.text = info != null && !string.IsNullOrEmpty(info.Title) ? info.Title : art.Title;
            _kind.text = RrWatchCard.SubtitleOf(activity, info, art);
            string blurb;
            _description.text = info != null && !string.IsNullOrEmpty(info.Description) ? info.Description : Blurbs.TryGetValue(activity, out blurb) ? blurb : "";

            for (int i = _art.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_art.GetChild(i).gameObject);
            }

            _artBack.Set(art.BackFrom, art.BackTo);
            RrWatchCard.Picture(_art, art, 40f);
            RrUiKit.UiLayer(_art.gameObject);
            _instancesKey = "";
        }

        public override void Refresh()
        {
            var everyone = RrWatchWorld.Everyone();
            var here = RrWatchWorld.Here;
            bool isHere = here == _activity;
            var info = RrRooms.Get(_activity);
            int max = info != null ? info.MaxPlayers : 8;

            // Instances of this activity with their head counts, fullest first (Rec Room lists joinable ones).
            var counts = new SortedDictionary<int, int>();
            int total = 0;
            foreach (var p in everyone)
            {
                if (p.Activity != _activity || (p.IsLocal && !isHere))
                {
                    continue;
                }

                int n;
                counts.TryGetValue(p.Instance, out n);
                counts[p.Instance] = n + 1;
                total++;
            }

            _playing.text = total == 0 ? "Nobody is playing right now" : total == 1 ? "1 player here now" : total + " players here now";

            var sb = new System.Text.StringBuilder();
            foreach (var kv in counts)
            {
                sb.Append(kv.Key).Append(':').Append(kv.Value).Append(',');
            }

            string key = sb.ToString() + isHere;
            if (key != _instancesKey)
            {
                _instancesKey = key;
                for (int i = _instances.childCount - 1; i >= 0; i--)
                {
                    Object.Destroy(_instances.GetChild(i).gameObject);
                }

                float y = 0f;
                int shown = 0;
                bool networked = info == null || info.Networked;
                foreach (var kv in counts)
                {
                    if (shown == 2 || !networked)
                    {
                        break;
                    }

                    bool mine = isHere && kv.Key == RrWatchWorld.HereInstance;
                    var row = RrUiKit.TopLeft(_instances, "Instance " + kv.Key, 0f, y, ColW, 80f);
                    RrUiKit.Rounded(row, Color.white, 20f);
                    string label = (kv.Key > 0 ? "#" + kv.Key + "   " : "") + kv.Value + " / " + max + " players";
                    RrUiKit.Label(row, "Label", label, 24f, 0f, ColW - 260f, 80f, 30f, RrUiKit.Ink);
                    if (mine)
                    {
                        var pill = RrUiKit.Pill(row, "Here", "You are here", ColW - 220f, 18f, 44f, RrUiKit.Badge, Color.white, 22f);
                        pill.overflowMode = TextOverflowModes.Overflow;
                    }
                    else
                    {
                        int instance = kv.Key;
                        var join = RrUiKit.Secondary(row, "Join", "Join", ColW - 200f, 10f, 180f, 60f, 26f);
                        join.onClick.AddListener(() => Ui.Travel(_activity, instance));
                        ((Image)join.targetGraphic).color = RrUiKit.Page;
                        join.FillStates = new[] { RrUiKit.Page, RrUiKit.SecondaryHover, RrUiKit.SecondaryPressed };
                        join.Refresh();
                    }

                    y += 90f;
                    shown++;
                }

                RrUiKit.UiLayer(_instances.gameObject);
            }

            bool busy = RrTravel.Busy;
            _go.Label.text = isHere ? "YOU ARE HERE" : busy ? "TRAVELLING..." : "GO";
            _go.interactable = !isHere && !busy;
            _goGroup.alpha = _go.interactable ? 1f : 0.45f;
        }

        public override void Tick()
        {
            // Travel can start or end while the page is up (another button, a portal).
            bool canGo = RrWatchWorld.Here != _activity && !RrTravel.Busy;
            if (canGo != _go.interactable)
            {
                Refresh();
            }
        }
    }
}
